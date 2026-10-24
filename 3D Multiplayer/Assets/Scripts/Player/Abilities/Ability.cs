using Unity.Netcode;
using UnityEngine;

public class Ability : NetworkBehaviour
{
    [SerializeField] protected string abilityName;
    [SerializeField] protected float cooldown;
    [SerializeField] protected float duration;

    protected bool abilityActive;

    protected PlayerData myData;
    protected PlayerModelManager myModelManager;
    protected PlayerMovement myMovement;

    void Awake()
    {
        myData = GetComponent<PlayerData>();
        myModelManager = GetComponent<PlayerModelManager>();
        myMovement = GetComponent<PlayerMovement>();
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
