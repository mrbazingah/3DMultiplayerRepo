using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerModelManager : NetworkBehaviour
{
    static readonly int SilhouetteColorId = Shader.PropertyToID("_Silhouette_Color");

    [SerializeField] GameObject defaultVisuals;
    [SerializeField] float detectionRange;
    [SerializeField] LayerMask propLayer;
    [SerializeField] Transform camPivot;
    [SerializeField] Prop detectedProp;
    [SerializeField] GameObject currentPropModel;
    [SerializeField] bool canSwap;
    [Space]
    [SerializeField] GameObject ownerGunModel;
    [SerializeField] GameObject clientGunModel;
    [Space]
    [SerializeField] Material silhouetteMat;

    [Header("Spine Lean")]
    [SerializeField] Animator rigAnimator;
    [SerializeField] Transform[] spineBones;
    [SerializeField] float[] spineWeights = { 0.3f, 0.3f, 0.4f };
    [SerializeField] float spineSmoothSpeed = 12f;
    [SerializeField] bool invertSpineLean;

    NetworkVariable<Prop.PropType> currentPropType = new NetworkVariable<Prop.PropType>();

    NetworkVariable<bool> lockRotation = new NetworkVariable<bool>();
    NetworkVariable<Quaternion> savedRotation = new NetworkVariable<Quaternion>();

    Quaternion[] spineRestRotations;
    float smoothedSpinePitch;

    PropRegistry propRegistry;
    PlayerMovement myMovement;
    PlayerHealth myHealth;
    Collider myCollider;
    Rigidbody myRigidbody;
    PlayerUIManager myUiManager;
    PlayerData myData;

    void Awake()
    {
        myMovement = GetComponent<PlayerMovement>();
        myHealth = GetComponent<PlayerHealth>();
        myCollider = GetComponent<Collider>();
        myRigidbody = GetComponent<Rigidbody>();
        myUiManager = GetComponent<PlayerUIManager>();
        myData = GetComponent<PlayerData>();

        SetupSpineBones();
        UpdateSilhouetteColor();
    }

    public override void OnNetworkSpawn()
    {
        // Subscribes to network variable changes
        currentPropType.OnValueChanged += OnCurrentPropTypeChanged;
        lockRotation.OnValueChanged += OnLockRotationChanged;

        if (IsOwner)
        {
            // Makes sure local player model has correct layer
            SetLayerRecursively(defaultVisuals, LayerMask.NameToLayer("Player Visuals"));
        }
    }

    void Start()
    {
        propRegistry = FindFirstObjectByType<PropRegistry>();

        // Follows the team from PlayerMovement so canSwap stays correct on every client, and applies the current value for players who spawn with a team already set
        NetworkVariable<GameManager.Team> team = myData.GetPlayerTeam();
        team.OnValueChanged += OnTeamChanged;
        SetCanSwap(team.Value == GameManager.Team.Props);
    }

    public void UpdateSilhouetteColor()
    {
        foreach (Renderer r in defaultVisuals.GetComponentsInChildren<Renderer>(true))
        {
            UpdateSilhouette(r);
        }

        if (currentPropModel != null)
        {
            foreach (Renderer r in currentPropModel.GetComponentsInChildren<Renderer>(true))
            {
                UpdateSilhouette(r);
            }
        }
    }

    void SetupSpineBones()
    {
        // Humanoid rigs expose the bones through the animator, generic rigs have to be assigned in the inspector
        if (spineBones == null || spineBones.Length == 0)
        {
            if (rigAnimator == null)
            {
                rigAnimator = GetComponentInChildren<Animator>();
            }

            if (rigAnimator == null || !rigAnimator.isHuman)
            {
                Debug.LogWarning("No humanoid rig found, assign spine bones manually for spine lean");
                return;
            }

            List<Transform> found = new List<Transform>();
            AddSpineBone(found, HumanBodyBones.Spine);
            AddSpineBone(found, HumanBodyBones.Chest);
            AddSpineBone(found, HumanBodyBones.UpperChest);
            spineBones = found.ToArray();
        }

        // Every frame's rotation is built from the rest pose instead of the previous frame's, otherwise the lean drifts
        spineRestRotations = new Quaternion[spineBones.Length];
        for (int i = 0; i < spineBones.Length; i++)
        {
            spineRestRotations[i] = spineBones[i].localRotation;
        }
    }

    void AddSpineBone(List<Transform> bones, HumanBodyBones bone)
    {
        Transform boneTransform = rigAnimator.GetBoneTransform(bone);
        if (boneTransform != null)
        {
            bones.Add(boneTransform);
        }
    }

    // Only the team decides this, pausing goes through PlayerMovement.IsInputBlocked() instead
    void SetCanSwap(bool newCanSwap)
    {
        canSwap = newCanSwap;

        // Updates both ways so the lock prompt gets hidden again if the player stops being a prop
        if (IsOwner)
        {
            myUiManager.SetLockRotField(canSwap, GetLockText(lockRotation.Value));
        }
    }

    // Updates canSwap when team changes
    void OnTeamChanged(GameManager.Team previousTeam, GameManager.Team newTeam)
    {
        SetCanSwap(newTeam == GameManager.Team.Props);
    }

    public void SetGunModelsActive(bool ownerActive, bool clientActive)
    {
        ownerGunModel.SetActive(ownerActive);
        clientGunModel.SetActive(clientActive);
    }

    public void SwapModel(Prop newDetectedProp)
    {
        if (!canSwap || myMovement.IsInputBlocked()) { return; }

        detectedProp = newDetectedProp;
        SwapModelServerRpc(detectedProp.propType);
    }

    // Server RPC to swap the player's model which triggers OnCurrentPropChanged() to update the model on all clients
    [Rpc(SendTo.Server)]
    void SwapModelServerRpc(Prop.PropType propType)
    {
        // Checks the team on the server too since canSwap only exists on the client
        if (myData.GetPlayerTeam().Value != GameManager.Team.Props) { return; }

        // Stops model swap if the prop prefab isn't found in the registry
        if (propRegistry.GetPrefab(propType) == null && propType != Prop.PropType.None) { return; }

        currentPropType.Value = propType;

        // Falls back to the player's own max health when discarding, since None has no registry entry
        int newMaxHealth = propType != Prop.PropType.None ? propRegistry.GetHealth(propType) : myHealth.GetDefaultMaxHealth();
        myHealth.ApplyMaxHealth(newMaxHealth);
    }

    // Updates player model on each client when variable changes
    void OnCurrentPropTypeChanged(Prop.PropType oldValue, Prop.PropType newValue)
    {
        ApplyPropModel();
        Debug.Log("Prop changed from " + oldValue + " to " + newValue);
    }

    void ApplyPropModel()
    {
        Debug.Log("Applied prop model");

        // Initializes prop model for player
        GameObject spawnedProp = null;

        if (currentPropType.Value != Prop.PropType.None)
        {
            // Gets the prop from registry and spawns it as a child of the player
            GameObject propPrefab = propRegistry.GetPrefab(currentPropType.Value);
            spawnedProp = Instantiate(propPrefab, transform);

            float yOffset = propRegistry.GetYOffset(currentPropType.Value);
            spawnedProp.transform.localPosition += new Vector3(0, yOffset, 0);

            // Only done when a prop was spawned, spawnedProp is null when discarding
            Collider propCollider = spawnedProp.GetComponent<Collider>();
            propCollider.sharedMaterial = myCollider.sharedMaterial;

            defaultVisuals.SetActive(false);
            myCollider.enabled = false;
        }
        else
        {
            defaultVisuals.SetActive(true);
            myCollider.enabled = true;
        }

        // If the player has a prop assigned and the prop model isn't null then use the model's collider, otherwise use the player's collider
        Collider spawnedCollider = currentPropType.Value != Prop.PropType.None ? spawnedProp.GetComponent<Collider>() : myCollider;
        Collider previousCollider = currentPropModel != null ? currentPropModel.GetComponent<Collider>() : myCollider;

        AlignPlayerToGround(previousCollider, spawnedCollider);

        if (currentPropModel != null)
        {
            currentPropModel.SetActive(false);
            Destroy(currentPropModel);

            // Destroy() only happens at the end of the frame, so the reference is cleared now for GetCurrentModel()
            currentPropModel = null;
        }

        if (spawnedProp != null)
        {
            // Applies new prop model, sets layer and assigns new collider
            currentPropModel = spawnedProp;
            SetLayerRecursively(currentPropModel, LayerMask.NameToLayer("Player Prop"));

            // ApplyPropModel() runs on every machine, so only the owner sends the sound RPC
            if (IsOwner)
            {
                PlaySwapPropSfxRpc();
            }

            Collider newCol = GetCurrentModelCollider();
            myMovement.SetPlayerCollider(newCol);

            // Updates renderers
            foreach (Renderer r in spawnedProp.GetComponentsInChildren<Renderer>(true))
            {
                UpdateSilhouette(r);
            }
        }

        InvisAbility invis = GetComponent<InvisAbility>();
        if (invis.GetAbilityActive())
        {
            invis.ApplyInvis(true);
        }
    }

    void UpdateSilhouette(Renderer renderer)
    {
        if (renderer == null || myData == null || IsOwner) { return; }

        // Checks by shader since the renderer holds copies of the materials, not the asset itself
        List<Material> shared = new List<Material>();
        renderer.GetSharedMaterials(shared);
        if (!shared.Exists(m => m != null && m.shader == silhouetteMat.shader))
        {
            shared.Add(silhouetteMat);
            renderer.SetSharedMaterials(shared);
        }

        foreach (Material mat in renderer.materials)
        {
            if (mat.shader == silhouetteMat.shader)
            {
                mat.SetColor(SilhouetteColorId, myData.GetPlayerColor().Value);
                mat.renderQueue = 2001;
            }
            else
            {
                mat.renderQueue = 2002;
            }
        }
    }

    [Rpc(SendTo.Everyone)]
    void PlaySwapPropSfxRpc()
    {
        AudioManager.Instance.PlaySfx(AudioManager.Instance.propSwapSfx, transform);
    }

    // Sets layer on each child of an object
    public void SetLayerRecursively(GameObject obj, int layer)
    {
        if (obj == ownerGunModel) { return; }

        obj.layer = layer;

        foreach (Transform child in obj.transform)
        {
            SetLayerRecursively(child.gameObject, layer);
        }
    }

    // Uses the previous and current model's collider's lowest point to calculate the distance between them and moves the player to allign to the ground
    void AlignPlayerToGround(Collider previousCollider, Collider newCollider)
    {
        float previousLowPoint = previousCollider.bounds.min.y;
        float newLowPoint = newCollider.bounds.min.y;

        float distance = previousLowPoint - newLowPoint;

        myRigidbody.position += new Vector3(0, distance, 0);
    }

    public void OnDiscard(InputValue value)
    {
        if (!IsOwner || !canSwap || myMovement.IsInputBlocked()) { return; }

        SwapModelServerRpc(Prop.PropType.None);

        Debug.Log("Discarded prop");
    }

    public void OnLock(InputValue value)
    {
        if (!IsOwner || !canSwap || myMovement.IsInputBlocked()) { return; }

        // Assigns prop's model or player's model before locking it
        Transform modelTransform = currentPropModel != null ? currentPropModel.transform : defaultVisuals.transform;
        ToggleLockServerRpc(modelTransform.rotation);

        // The prompt text is updated in OnLockRotationChanged(), reading lockRotation here would give the value from before the toggle
    }

    // Server RPC to toggle lock rotation and save the current rotation if locking
    [Rpc(SendTo.Server)]
    public void ToggleLockServerRpc(Quaternion currentRotation, bool changeState = true)
    {
        if (myData.GetPlayerTeam().Value != GameManager.Team.Props) { return; }

        lockRotation.Value = changeState? !lockRotation.Value : lockRotation.Value;

        if (lockRotation.Value)
        {
            savedRotation.Value = currentRotation;
        }
    }

    void OnLockRotationChanged(bool previous, bool current)
    {
        if (!IsOwner || !canSwap) { return; }

        myUiManager.SetLockRotField(true, GetLockText(current));
    }

    string GetLockText(bool isLocked)
    {
        return isLocked ? "Unlock rotation" : "Lock rotation";
    }

    // Runs on every client and not just the owner, otherwise other players wouldn't see the locked rotation or the snap back
    void LateUpdate()
    {
        LockRotation();

        // Runs after LockRotation() so the model root is already facing where it should be this frame
        ApplySpineLean();
    }

    // Only reads the network variables and applies them to the local model, all writes go through ToggleLockServerRpc()
    void LockRotation()
    {
        // Uses the rotation assigned when locked or player's current rotation
        Quaternion targetRotation = lockRotation.Value ? savedRotation.Value : myMovement.GetPlayerRotation();

        if (currentPropModel != null)
        {
            currentPropModel.transform.rotation = targetRotation;
        }
        else
        {
            defaultVisuals.transform.rotation = targetRotation;
        }
    }

    void ApplySpineLean()
    {
        if (spineRestRotations == null) { return; }

        // The humanoid mesh is hidden while a prop model is active so there is nothing to bend
        if (currentPropType.Value != Prop.PropType.None) { return; }

        float target = myMovement.GetSpinePitch().Value;
        if (invertSpineLean)
        {
            target = -target;
        }

        // Smooths toward the target so the send threshold isn't visible as steps
        smoothedSpinePitch = Mathf.Lerp(smoothedSpinePitch, target, Time.deltaTime * spineSmoothSpeed);

        for (int i = 0; i < spineBones.Length; i++)
        {
            Transform bone = spineBones[i];
            if (bone == null || bone.parent == null) { continue; }

            float angle = smoothedSpinePitch * GetSpineWeight(i);

            // Imported bones rarely have a usable local axis, so the model's right vector is converted into the bone's parent space
            Vector3 axis = bone.parent.InverseTransformDirection(defaultVisuals.transform.right);
            bone.localRotation = Quaternion.AngleAxis(angle, axis) * spineRestRotations[i];
        }
    }

    float GetSpineWeight(int index)
    {
        if (spineWeights == null || spineWeights.Length != spineBones.Length)
        {
            return 1f / spineBones.Length;
        }

        return spineWeights[index];
    }

    public override void OnNetworkDespawn()
    {
        currentPropType.OnValueChanged -= OnCurrentPropTypeChanged;
        lockRotation.OnValueChanged -= OnLockRotationChanged;
        myData.GetPlayerTeam().OnValueChanged -= OnTeamChanged;
    }

    public Collider GetCurrentModelCollider()
    {
        // Returns either prop model's collider or player model's collider
        Collider cmCol = currentPropModel != null ? currentPropModel.GetComponent<Collider>() : myMovement.GetComponent<Collider>();
        return cmCol;
    }

    public GameObject GetDefaultVisuals()
    {
        return defaultVisuals;
    }

    public GameObject GetCurrentModel()
    {
        return currentPropModel != null ? currentPropModel : defaultVisuals;
    }

    public Prop.PropType GetCurrentPropType()
    {
        return currentPropType.Value;
    }

    public bool GetLockRotation()
    {
        return lockRotation.Value;
    }
}