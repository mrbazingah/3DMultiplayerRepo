using System.Linq;
using Unity.Netcode;
using UnityEngine;

public class PlayerAbilityManager : NetworkBehaviour
{
    [SerializeField] GameObject[] abilitySlots;
    [SerializeField] string[] propAbilities;
    [SerializeField] string[] hunterAbilities;
    [SerializeField] int selectedAbilitySlot;

    PlayerData myData;

    // Props
    int invisCount;
    int instaSwapCount;

    // Hunters
    int bombCount;
    int trackGogglesCount;
    int forcedTauntCount;

    // Select ability slot based on index
    // Use ability with mouse click

    // Player pickup ability
    // Add ability to the first empty slot
    public override void OnNetworkSpawn()
    {
        myData = GetComponent<PlayerData>();
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Ability"))
        {
            AbilityItem itemScript = other.GetComponent<AbilityItem>();
            PickUpItemServerRpc(itemScript.NetworkObject, itemScript);
        }    
    }

    [Rpc(SendTo.Server)]
    void PickUpItemServerRpc(NetworkObjectReference itemRef, AbilityItem itemScript)
    {
        if (!itemRef.TryGet(out NetworkObject itemNetObj)) { return; }

        if (itemScript == null) { return; }

        // Read the item's data before despawning it.
        string itemName = itemScript.itemName;

        string[] myTeamAbilites = myData.GetPlayerTeam().Value == GameManager.Team.Hunters ? hunterAbilities : propAbilities;
        if (!myTeamAbilites.Contains(itemName)) { return; }

        // Remove the world object (the real NetworkObject) from every client.
        itemNetObj.Despawn(true);
    }
}
