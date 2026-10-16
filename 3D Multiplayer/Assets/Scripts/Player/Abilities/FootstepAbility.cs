using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class FootstepAbility : Ability
{
    [SerializeField] GameObject footstepPrefab;
    [SerializeField] Transform spawnPoint;
    [SerializeField] float offset;
    [SerializeField] float footstepTime;
    [SerializeField] float maxFootstepCount;
    [SerializeField] float maxVisableFootsteps;
    [SerializeField] LayerMask groundLayer;
    [SerializeField] float maxGroundDistance = 10f;
    [SerializeField] bool abilityActive;
    [SerializeField] List<GameObject> spawnedFootsteps = new List<GameObject>();

    float currentFootstepTime;

    PlayerMovement myMovement;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        myMovement = GetComponent<PlayerMovement>();

        SetAbilityActive(false);
    }

    void Update()
    {
        if (!IsOwner) { return; }

        HandleFootsteps();
    }

    public void SetAbilityActive(bool isActive)
    {
        abilityActive = isActive;
        RefreshVisibility();
    }
    
    void HandleFootsteps()
    {
        if (myMovement == null || !myMovement.GetIsWalking()) { return; }

        float speedMultiplier = (myMovement.GetWalkSpeed() / myMovement.GetRunSpeed()) + 1;

        if (currentFootstepTime > 0)
        {
            currentFootstepTime -= Time.deltaTime;
        }
        else
        {
            SpawnFootstepRpc(spawnPoint.position, transform.rotation);

            currentFootstepTime = myMovement.GetIsRunning() ? footstepTime / speedMultiplier : footstepTime;
        }
    }

    [Rpc(SendTo.Everyone)]
    void SpawnFootstepRpc(Vector3 spawnPos, Quaternion spawnRot)
    {
        GameObject spawned = Instantiate(footstepPrefab, spawnPos, spawnRot);
        SetFootstepColor(spawned);
        spawnedFootsteps.Add(spawned);

        if (spawnedFootsteps.Count > maxFootstepCount)
        {
            Destroy(spawnedFootsteps[0]);
            spawnedFootsteps.RemoveAt(0);
        }

        RefreshVisibility();
    }

    void RefreshVisibility()
    {
        for (int i = 0; i < spawnedFootsteps.Count; i++)
        {
            bool isVisible = i + maxVisableFootsteps < spawnedFootsteps.Count && abilityActive;
            spawnedFootsteps[i].SetActive(isVisible);
        }
    }

    void SetFootstepColor(GameObject footstep)
    {
        MeshRenderer footstepRenderer = footstep.GetComponent<MeshRenderer>();
        if (footstepRenderer == null || myData == null) { return; }

        foreach (Material mat in footstepRenderer.materials)
        {
            if (mat.HasProperty("_BaseColor"))
            {
                mat.SetColor("_BaseColor", myData.GetPlayerColor().Value);
            }
            if (mat.HasProperty("_Silhouette_Color"))
            {
                mat.SetColor("_Silhouette_Color", myData.GetPlayerColor().Value);
            }
        }
    }

    public override void OnNetworkDespawn()
    {
        foreach (GameObject footstep in spawnedFootsteps)
        {
            Destroy(footstep);
        }

        spawnedFootsteps.Clear();
    }
}