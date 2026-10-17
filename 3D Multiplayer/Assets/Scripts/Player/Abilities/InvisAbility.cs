using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering;

public class InvisAbility : Ability
{
    [SerializeField] Material outlineMat;
    [SerializeField] Material depthMask;

    readonly Dictionary<Renderer, Material[]> savedMats = new Dictionary<Renderer, Material[]>();

    bool abilityActive;

    PlayerModelManager myModelManger;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        myModelManger = GetComponent<PlayerModelManager>();
    }

    public void SetAbilityActive(bool isActive)
    {
        abilityActive = isActive;
        HandleInvisRpc();
    }

    [Rpc(SendTo.Everyone)]
    void HandleInvisRpc()
    {
        GameObject model = myModelManger.GetCurrentModel();

        foreach (Renderer r in model.GetComponentsInChildren<Renderer>(true))
        {
            if (abilityActive)
            {
                if (savedMats.ContainsKey(r)) { continue; }
                savedMats[r] = r.sharedMaterials;
                r.sharedMaterials = IsOwner ? new Material[] { depthMask, outlineMat } : new Material[] { depthMask };
                r.shadowCastingMode = ShadowCastingMode.Off;

                Debug.Log("Removed mat");
            }
            else if (savedMats.TryGetValue(r, out Material[] original))
            {
                r.sharedMaterials = original;
                r.shadowCastingMode = ShadowCastingMode.On;

                Debug.Log("Applied mat");
            }
        }

        if (!abilityActive)
        {
            savedMats.Clear();
        }
    }
}
