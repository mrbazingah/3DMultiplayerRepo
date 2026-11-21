using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class Ability : NetworkBehaviour
{
    [SerializeField] protected string abilityName;
    [SerializeField] protected float cooldown;
    [SerializeField] protected float duration;
    [SerializeField] protected GameManager.Team requiredTeam;

    protected bool abilityActive;

    protected PlayerData myData;
    protected PlayerModelManager myModelManager;
    protected PlayerMovement myMovement;

    NetworkVariable<double> activeUntil = new NetworkVariable<double>();
    NetworkVariable<double> cooldownUntil = new NetworkVariable<double>();

    double ServerTime => NetworkManager.ServerTime.Time;

    protected virtual void Awake()
    {
        myData = GetComponent<PlayerData>();
        myModelManager = GetComponent<PlayerModelManager>();
        myMovement = GetComponent<PlayerMovement>();
    }

    public void TryActivate()
    {
        if (!IsOwner) { return; }

        if (IsActive() || IsOnCooldown()) { return; }

        ActivateServerRpc();
    }

    [Rpc(SendTo.Server)]
    void ActivateServerRpc()
    {
        if (IsActive() || IsOnCooldown()) { return; }
        if (myData.GetPlayerTeam().Value != requiredTeam) { return; }
        if (!CanActivate()) { return; }

        activeUntil.Value = ServerTime + duration;
        cooldownUntil.Value = ServerTime + duration + cooldown;

        OnActivateServer();
        ActivateOwnerRpc();

        if (duration > 0)
        {
            StartCoroutine(EndAfterDuration());
        }
    }

    IEnumerator EndAfterDuration()
    {
        yield return new WaitForSeconds(duration);

        OnEndServer();
        EndOwnerRpc();
    }

    [Rpc(SendTo.Owner)]
    void ActivateOwnerRpc()
    {
        OnActivateOwner();
    }

    [Rpc(SendTo.Owner)]
    void EndOwnerRpc()
    {
        OnEndOwner();
    }

    protected virtual bool CanActivate() { return true; }
    protected virtual void OnActivateServer() { }
    protected virtual void OnEndServer() { }
    protected virtual void OnActivateOwner() { }
    protected virtual void OnEndOwner() { }

    public bool IsActive()
    {
        return ServerTime < activeUntil.Value;
    }

    public bool IsOnCooldown()
    {
        return ServerTime < cooldownUntil.Value;
    }

    public float GetCooldownRemaining()
    {
        return Mathf.Max(0f, (float)(cooldownUntil.Value - ServerTime));
    }

    public bool GetAbilityActive()
    {
        return abilityActive;
    }
}