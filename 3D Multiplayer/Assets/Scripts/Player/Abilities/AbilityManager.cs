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
                if (player == null || player.GetComponent<PlayerData>().GetPlayerTeam().Value != GameManager.Team.Props) { continue; }

                if (player.TryGetComponent(out FootstepAbility footsteps))
                {
                    propFootsteps.Add(footsteps);
                }
            }

            StartCoroutine(FootstepTimer(propFootsteps));

            Debug.Log("Hunter Ability 1");
        }
        else if (myData.GetPlayerTeam().Value == GameManager.Team.Props)
        {
            StartCoroutine(InvisTimer());
            PlayPropInvisSfxRpc();

            Debug.Log("Prop ability 1");
        }
    }

    [Rpc(SendTo.Everyone)]
    void PlayPropInvisSfxRpc()
    {
        AudioManager.Instance.PlaySfx(AudioManager.Instance.propInvisSfx, transform);
    }

    IEnumerator FootstepTimer(List<FootstepAbility> propFootsteps)
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

    IEnumerator InvisTimer()
    {
        ability1Active = true;
        GetComponent<InvisAbility>().HandleInvisRpc(true);

        yield return new WaitForSeconds(propAbilities[0].GetDuration());

        ability1Active = false;
        GetComponent<InvisAbility>().HandleInvisRpc(false);
    }
}
