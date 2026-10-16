using Unity.Netcode;
using UnityEngine;

public class AbilityItem : NetworkBehaviour
{
    [field: SerializeField] public string itemName { get; private set; }
    [field: SerializeField] public int itemId { get; private set; }
    [field: SerializeField] public GameManager.Team itemTeam { get; private set; }


}
