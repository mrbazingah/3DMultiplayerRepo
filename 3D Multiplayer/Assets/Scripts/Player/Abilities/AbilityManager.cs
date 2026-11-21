using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class AbilityManager : NetworkBehaviour
{
    [SerializeField] GameObject[] abilitySlots;
    [SerializeField] Ability[] hunterAbilities;
    [SerializeField] Ability[] propAbilities;
    [SerializeField] int selectedAbilitySlot;

    PlayerData myData;
    PlayerUIManager myUiManager;

    void Awake()
    {
        myData = GetComponent<PlayerData>();
        myUiManager = GetComponent<PlayerUIManager>();
    }

    void Update()
    {
        myUiManager.SetTauntField(myData.GetPlayerTeam().Value == GameManager.Team.Props, "Taunt");
    }

    public void OnAbility1(InputValue value) { UseAbility(value, 0); }
    public void OnAbility2(InputValue value) { UseAbility(value, 1); }
    public void OnAbility3(InputValue value) { UseAbility(value, 2); }

    void UseAbility(InputValue value, int index)
    {
        if (!IsOwner || !value.isPressed) { return; }
        if (myData.GetPlayerTeam().Value == GameManager.Team.None) { return; }

        Ability[] abilities = myData.GetPlayerTeam().Value == GameManager.Team.Hunters ? hunterAbilities : propAbilities;
        if (index >= abilities.Length || abilities[index] == null) { return; }

        abilities[index].TryActivate();
    }

    public void OnTaunt(InputValue value)
    {
        if (!IsOwner || !value.isPressed || myData.GetPlayerTeam().Value != GameManager.Team.Props) { return; }

        PlayPropTauntSfxRpc();
    }

    [Rpc(SendTo.Everyone)]
    public void PlayPropTauntSfxRpc()
    {
        AudioManager.Instance.PlaySfx(AudioManager.Instance.propTauntSfx, transform);
    }
}
