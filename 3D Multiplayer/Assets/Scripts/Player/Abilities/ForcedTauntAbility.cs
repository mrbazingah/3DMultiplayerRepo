using System.Collections.Generic;
using UnityEngine;

public class ForcedTauntAbility : Ability
{
    [SerializeField] float playerDetectionRadius;
    [SerializeField] float maxDistance;
    [SerializeField] LayerMask playerLayer;

    public void ForcePropTaunt()
    {
        List<AbilityManager> abilityManagers = GetDetectedPlayers();
        foreach (AbilityManager am in abilityManagers)
        {
            am.PlayPropTauntSfxRpc();
        }
    }

    List<AbilityManager> GetDetectedPlayers()
    {
        RaycastHit[] hits = Physics.SphereCastAll(transform.position, playerDetectionRadius, transform.forward, maxDistance, playerLayer);

        List<AbilityManager> playerList = new List<AbilityManager>();
        foreach (RaycastHit hit in hits)
        {
            if (!hit.transform.TryGetComponent(out PlayerData pd)) { continue; }
            if (pd.GetPlayerTeam().Value != GameManager.Team.Props) { continue; }

            playerList.Add(hit.transform.GetComponent<AbilityManager>());
        }

        return playerList;
    }
}
