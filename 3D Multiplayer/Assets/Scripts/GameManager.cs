using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : NetworkBehaviour
{
    public static GameManager Instance { get; private set; }

    [SerializeField] Transform mapSpawnTransform;
    [SerializeField] Transform lobbySpawnTransform;
    [SerializeField] int maxPlayerCount;
    [SerializeField] int hunterValue;
    [SerializeField] List<PlayerMovement> playerList = new List<PlayerMovement>();

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            gameObject.SetActive(false);
            Destroy(gameObject);
        }
    }

    void Update()
    {
        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("No Network Manager found");
            SceneManager.LoadScene("Main Menu Scene");
        }    
    }

    public enum Team
    {
        None,
        Hunters,
        Props,
    }

    public void RegisterPlayer(PlayerMovement player)
    {
        if (player == null || playerList.Contains(player)) { return; }

        playerList.Add(player);

        // Only position the player that just joined, everyone else stays put
        //player.TeleportTo(lobbySpawnTransform.position);
    }

    public void UnregisterPlayer(PlayerMovement player)
    {
        playerList.Remove(player);
    }

    public void StartGame()
    {
        if (!IsServer /*|| playerList.Count < 2*/) { return; }

        AssignTeam();
        SetPlayerPositionsRpc(mapSpawnTransform.position);
    }

    void AssignTeam()
    {
        // Randomise player list indexes
        for (int i = playerList.Count - 1; i > 0; i--)
        {
            int randomIndex = Random.Range(0, i + 1);

            // Assign value to the random index in list
            (playerList[i], playerList[randomIndex]) = (playerList[randomIndex], playerList[i]);
        }

        // Divide hunter count
        int hunterCount = Mathf.Max(1, playerList.Count / hunterValue);
        for (int i = 0; i < playerList.Count; i++)
        {
            playerList[i].SetPlayerTeam(i < hunterCount ? Team.Hunters : Team.Props);
        }
    }

    [Rpc(SendTo.Everyone)]
    void SetPlayerPositionsRpc(Vector3 pos)
    {
        if (playerList.Count == 0)
        {
            Debug.LogWarning("No players registered in GameManager.");
            return;
        }

        foreach (PlayerMovement player in playerList)
        {
            if (player != null)
            {
                player.TeleportTo(pos);
            }
        }
    }
}