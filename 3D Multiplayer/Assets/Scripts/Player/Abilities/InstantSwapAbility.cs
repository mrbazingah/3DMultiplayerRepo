using Unity.Netcode;
using UnityEngine;

public class InstantSwapAbility : Ability
{
    [SerializeField] float swapRadius;
    [SerializeField] float maxDistance;
    [SerializeField] LayerMask propLayer;

    public void SwapProp()
    {
        GameObject propObj = GetRandomProp();
        if (propObj == null) { return; }

        Prop prop = propObj.GetComponent<Prop>();
        myModelManager.SwapModel(prop);

        Vector3 propPos = propObj.transform.position;
        Quaternion propRot = propObj.transform.rotation;

        propObj.SetActive(false);

        if (!myModelManager.GetLockRotation())
        {
            myModelManager.ToggleLockServerRpc(propRot);
        }

        DespawnPropRpc(propObj);
        myMovement.TeleportTo(propPos, propRot);
    }

    GameObject GetRandomProp()
    {
        RaycastHit[] hits = Physics.SphereCastAll(transform.position, swapRadius, transform.forward, maxDistance, propLayer);

        int randomIndex = Random.Range(0, hits.Length);
        GameObject randomProp = hits[randomIndex].transform.gameObject;

        return randomProp; 
    }

    [Rpc(SendTo.Server)]
    void DespawnPropRpc(NetworkObjectReference propRef)
    {
        if (!propRef.TryGet(out NetworkObject propNetObj)) { return; }

        propNetObj.Despawn();
    }
}
