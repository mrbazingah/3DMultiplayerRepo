using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

public class InvisAbility : Ability
{
    [SerializeField] Material outlineMat;
    [SerializeField] Material depthMask;

    List<Material> savedMats;

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

            r.GetSharedMaterials(savedMats);
            if (abilityActive)
            {
                savedMats.AddRange(r.materials);

                List<Material> m = new List<Material>(0);
                //r.SetSharedMaterials(m);

                Debug.Log("Removed mat");
            }
            else
            {
                r.SetSharedMaterials(savedMats);
                Debug.Log("Applied mat");
            }


            if (IsOwner)
            {
                // Currently doesnt work since it applies an outline mat on a disabled renderer

                List<Material> shared = new List<Material>();
                r.GetSharedMaterials(shared);
                if (!shared.Exists(m => m != null && m.shader == outlineMat.shader) && abilityActive)
                {
                    shared.Add(outlineMat);
                    shared.Add(depthMask);
                    r.SetSharedMaterials(shared);

                    Debug.Log("Applied mat");
                }
                else if (!abilityActive)
                {
                    shared.Remove(outlineMat);
                    shared.Remove(depthMask);
                    r.SetSharedMaterials(shared);

                    Debug.Log("Removed mat");

                }
            }
        }
    }
}
