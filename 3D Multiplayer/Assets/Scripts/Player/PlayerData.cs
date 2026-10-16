using Unity.Netcode;
using UnityEngine;

public class PlayerData : NetworkBehaviour
{
    [SerializeField] NetworkVariable<GameManager.Team> playerTeam;
    [SerializeField] NetworkVariable<Color> playerColor;

    PlayerMovement myMovement;
    PlayerModelManager myModelManager;
    AbilityManager myAbilityManager;

    public override void OnNetworkSpawn()
    {
        myMovement = GetComponent<PlayerMovement>();
        myModelManager = GetComponent<PlayerModelManager>();
        myAbilityManager = GetComponent<AbilityManager>();

        playerTeam.OnValueChanged += OnTeamChanged;
        OnTeamChanged(playerTeam.Value, playerTeam.Value);

        playerColor.OnValueChanged += OnColorChanged;

        if (IsServer)
        {
            playerColor.Value = GameManager.Instance.GetPlayerColors()[0];
        }
    }

    public void SetPlayerTeam(GameManager.Team newTeam)
    {
        // Only the server can write the team, the change replicates to the clients through OnTeamChanged()
        if (!IsServer) { return; }
        playerTeam.Value = newTeam;
    }

    public void SetPlayerColor(Color newColor)
    {
        // Only the server can write the color, the change replicates to the clients through OnColorChanged()
        if (!IsServer) { return; }
        playerColor.Value = newColor;

        Debug.Log($"Player {OwnerClientId} color changed to {newColor}");
    }

    void OnTeamChanged(GameManager.Team previous, GameManager.Team current)
    {
        if (!IsOwner) { return; }

        myMovement.UpdateCamLogic(current);
    }

    void OnColorChanged(Color previous, Color current)
    {
        myModelManager.UpdateSilhouetteColor();
    }

    public NetworkVariable<GameManager.Team> GetPlayerTeam()
    {
        return playerTeam;
    }

    public NetworkVariable<Color> GetPlayerColor()
    {
        return playerColor;
    }

    public override void OnNetworkDespawn()
    {
        playerTeam.OnValueChanged -= OnTeamChanged;
        playerColor.OnValueChanged -= OnColorChanged;
    }
}
