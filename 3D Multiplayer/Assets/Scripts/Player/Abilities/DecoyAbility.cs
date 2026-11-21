using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class DecoyAbility : Ability
{
    [SerializeField] int maxDecoys;
    [SerializeField] NetworkVariable<int> decoyCount = new NetworkVariable<int>();
    [SerializeField] List<NetworkObject> spawnedDecoys;

    PropRegistry propRegistry;

    public override void OnNetworkSpawn()
    {
        decoyCount.OnValueChanged += OnDecoyCountChanged;
    }

    void Start()
    {
        propRegistry = FindFirstObjectByType<PropRegistry>();    
    }

    protected override void OnActivateOwner()
    {
        SpawnDecoy();
    }

    protected override bool CanActivate()
    {
        return myModelManager.GetCurrentPropType() != Prop.PropType.None && decoyCount.Value < maxDecoys;
    }

    void OnDecoyCountChanged(int previousCount, int newCount)
    {
        if (!IsOwner) { return; }
    }

    public void SpawnDecoy()
    {
        if (!IsOwner) { return; }

        Transform model = myModelManager.GetCurrentModel().transform;
        SpawnDecoyServerRpc(model.position, model.rotation);
    }

    [Rpc(SendTo.Server)]
    void SpawnDecoyServerRpc(Vector3 pos, Quaternion rot)
    {
        if (myData.GetPlayerTeam().Value != GameManager.Team.Props) { return; }
        if (decoyCount.Value >= maxDecoys) { return; }

        Prop.PropType propType = myModelManager.GetCurrentPropType();
        if (propType == Prop.PropType.None) { return; }

        GameObject prefab = propRegistry.GetPrefab(propType);
        if (prefab == null) { return; }

        GameObject decoy = Instantiate(prefab, pos, rot);
        NetworkObject decoyNetObj = decoy.GetComponent<NetworkObject>();
        decoyNetObj.Spawn();

        spawnedDecoys.Add(decoyNetObj);
        decoyCount.Value++;
    }

    public override void OnNetworkDespawn()
    {
        decoyCount.OnValueChanged -= OnDecoyCountChanged;

        if (NetworkManager.ShutdownInProgress) { return; }
        
        DespawnAllDecoys();
    }

    public void DespawnAllDecoys()
    {
        if (!IsServer) { return; }

        foreach (NetworkObject decoy in spawnedDecoys)
        {
            if (decoy != null && decoy.IsSpawned)
            {
                decoy.Despawn();
            }
        }

        spawnedDecoys.Clear();
        decoyCount.Value = 0;
    }
}
