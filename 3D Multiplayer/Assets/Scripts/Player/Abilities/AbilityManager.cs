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
        if (!IsOwner || !value.isPressed || ability1Active) { return; }

        if (myData.GetPlayerTeam().Value == GameManager.Team.Hunters)
        {
            List<FootstepAbility> propFootsteps = new List<FootstepAbility>();

            foreach (PlayerMovement player in GameManager.Instance.GetPlayerList())
            {
                if (player.GetComponent<PlayerData>().GetPlayerTeam().Value != GameManager.Team.Props || player == null) { continue; }

                if (player.TryGetComponent(out FootstepAbility footsteps))
                {
                    propFootsteps.Add(footsteps);
                }
            }

            StartCoroutine(Ability1Timer(propFootsteps));

            Debug.Log("Hunter Ability 1");
        }
        else
        {
            Debug.Log("Prop ability 1");
        }
    }

    IEnumerator Ability1Timer(List<FootstepAbility> propFootsteps)
    {
        ability1Active = true;

        foreach (FootstepAbility footsteps in propFootsteps)
        {
            footsteps.SetAbilityActive(true);
        }

        yield return new WaitForSeconds(hunterAbilities[0].GetDuration());

        foreach (FootstepAbility footsteps in propFootsteps)
        {
            if (footsteps != null)
            {
                footsteps.SetAbilityActive(false);
            }
        }

        ability1Active = false;
    }
}
