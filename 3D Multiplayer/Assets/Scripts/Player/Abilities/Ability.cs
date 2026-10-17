using Unity.Netcode;
using UnityEngine;

public class Ability : NetworkBehaviour
{
    [SerializeField] protected string abilityName;
    [SerializeField] protected float cooldown;
    [SerializeField] protected float duration;

    protected bool abilityActive;

    protected PlayerData myData;

    public override void OnNetworkSpawn()
    {
        myData = GetComponent<PlayerData>();
    }

    public float GetDuration()
    {
        return duration;
    }

    public bool GetAbilityActive()
    {
        return abilityActive;
    }
}
