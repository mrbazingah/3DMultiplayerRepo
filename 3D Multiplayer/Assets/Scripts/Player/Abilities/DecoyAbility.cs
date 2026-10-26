using Unity.Netcode;
using UnityEngine;

public class DecoyAbility : Ability
{
    [SerializeField] int maxDecoys;
    [SerializeField] NetworkVariable<int> decoyCount;

    public override void OnNetworkSpawn()
    {
        decoyCount.OnValueChanged += OnDecoyCountChanged;
    }

    void OnDecoyCountChanged(int previousCount, int newCount)
    {

    }

    public void SpawnDecoy()
    {
        SpawnDecoyServerRpc(transform.position, transform.rotation);
    }

    [Rpc(SendTo.Server)]
    void SpawnDecoyServerRpc(Vector3 pos, Quaternion rot)
    {

    }
}
