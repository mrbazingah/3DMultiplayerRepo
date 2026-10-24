using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering;

public class InvisAbility : Ability
{
    [SerializeField] Material outlineMat;
    [SerializeField] Material depthMask;

    readonly Dictionary<Renderer, Material[]> savedMats = new Dictionary<Renderer, Material[]>();

    [Rpc(SendTo.Everyone)]
    public void HandleInvisRpc(bool isActive)
    {
        ApplyInvis(isActive);
    }

    public void ApplyInvis(bool isActive)
    {
        abilityActive = isActive;

        GameObject model = myModelManager.GetCurrentModel();
        foreach (Renderer r in model.GetComponentsInChildren<Renderer>(true))
        {
            if (!IsOwner)
            {
                r.enabled = !abilityActive;
                continue;
            }

            if (abilityActive)
            {
                if (savedMats.ContainsKey(r)) { continue; }
                savedMats[r] = r.sharedMaterials;
                r.sharedMaterials = new Material[] { depthMask, outlineMat };
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
