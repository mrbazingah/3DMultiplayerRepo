using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class AbilityManager : NetworkBehaviour
{
    [SerializeField] GameObject[] abilitySlots;
    [SerializeField] Ability[] hunterAbilities;
    [SerializeField] Ability[] propAbilities;
    [SerializeField] int selectedAbilitySlot;

    bool ability1Active;
    bool ability2Active;
    bool ability3Active;

    PlayerData myData;

    public override void OnNetworkSpawn()
    {
        myData = GetComponent<PlayerData>();
    }

    public void OnAbility1(InputValue value)
    {
        if (!IsOwner) { return; }

        if (myData.GetPlayerTeam().Value == GameManager.Team.Hunters && hunterAbilities[0].TryGetComponent(out FootstepAbility footsteps))
        {
            float time = footsteps.GetDuration(); 
            Ability1TimerRpc(time, footsteps.GetComponent<NetworkObject>());

            Debug.Log("Hunter Ability 1");
        }
        else
        {
            Debug.Log("Prop ability 1");
        }
    }

    [Rpc(SendTo.Server)]
    void Ability1TimerRpc(float time, NetworkObjectReference footstepsRef)
    {
        if (!footstepsRef.TryGet(out NetworkObject footstepsNetObj)) { return; }
        if (!footstepsNetObj.TryGetComponent(out FootstepAbility abilityScript)) { return; }

        StartCoroutine(Ability1Timer(time, abilityScript));
    }

    IEnumerator Ability1Timer(float time, FootstepAbility footsteps)
    {
        ability1Active = true;
        footsteps.SetAbilityActive(ability1Active);

        yield return new WaitForSeconds(time);

        ability1Active = false;
        footsteps.SetAbilityActive(ability1Active);
    }
}
