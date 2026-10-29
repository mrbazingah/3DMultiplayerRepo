using Unity.Netcode;
using UnityEngine;

public class InstantSwapAbility : Ability
{
    [SerializeField] float swapRadius;
    [SerializeField] float maxDistance;
    [SerializeField] LayerMask propLayer;
    [SerializeField] GameObject smokeParticles;

    public void SwapProp()
    {
        GameObject propObj = GetRandomProp();
        if (propObj == null) { return; }

        Prop prop = propObj.GetComponent<Prop>();
        myModelManager.SwapModel(prop);

        SpawnSmokeParticlesRpc(transform.position);

        propObj.transform.GetPositionAndRotation(out Vector3 propPos, out Quaternion propRot);
        DespawnPropRpc(propObj);

        myModelManager.ToggleLockServerRpc(propRot, !myModelManager.GetLockRotation());

        myMovement.TeleportTo(propPos, propRot);
    }

    [Rpc(SendTo.Everyone)]
    void SpawnSmokeParticlesRpc(Vector3 pos)
    {
        GameObject smoke = Instantiate(smokeParticles, pos, Quaternion.identity);
        Destroy(smoke, 5f);
    }

    GameObject GetRandomProp()
    {
        RaycastHit[] hits = Physics.SphereCastAll(transform.position, swapRadius, transform.forward, maxDistance, propLayer);
        if (hits.Length == 0) { return null; }

        int randomIndex = Random.Range(0, hits.Length);
        Prop prop = hits[randomIndex].collider.GetComponentInParent<Prop>();

        return prop != null ? prop.gameObject : null;
    }

    [Rpc(SendTo.Server)]
    void DespawnPropRpc(NetworkObjectReference propRef)
    {
        if (!propRef.TryGet(out NetworkObject propNetObj)) { return; }

        propNetObj.gameObject.SetActive(false);
        propNetObj.Despawn();
    }
}
