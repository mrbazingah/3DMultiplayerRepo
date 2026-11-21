using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : NetworkBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Game Start")]
    [SerializeField] Transform mapSpawnTransform;
    [SerializeField] Transform lobbySpawnTransform;
    [SerializeField] int maxPlayerCount;
    [SerializeField] int hunterValue;
    [SerializeField] float hideTime;
    [SerializeField] float searchTime;
    [SerializeField] List<PlayerData> props = new List<PlayerData>();
    [SerializeField] List<PlayerData> hunters = new List<PlayerData>();
    [Header("UI")]
    [SerializeField] TextMeshProUGUI timerText;
    [Space]
    [SerializeField] List<PlayerMovement> playerList = new List<PlayerMovement>();
    [SerializeField] Color[] playerColors;

    float currentHideTime;
    float currentSearchTime;
    bool startHideTime;
    bool gameStarted;

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

        timerText.gameObject.SetActive(false);
    }

    void Update()
    {
        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("No Network Manager found");
            SceneManager.LoadScene("Main Menu Scene");
        }

        if (gameStarted)
        {
            // Do stuff
        }

        HideDuration();
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

        //player.TeleportTo(lobbySpawnTransform.position);
    }

    public void UnregisterPlayer(PlayerMovement player)
    {
        playerList.Remove(player);
    }

    public void StartGame()
    {
        if (!IsServer /*|| playerList.Count < 2*/) { return; }

        gameStarted = true;

        AssignTeam();
    }

    void AssignTeam()
    {
        props.Clear();
        hunters.Clear();

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
            PlayerData pd = playerList[i].GetComponent<PlayerData>();

            // Players that already have a team keep it for debugging, but still go in the right list
            Team team = pd.GetPlayerTeam().Value;
            if (team == Team.None)
            {
                team = i < hunterCount ? Team.Hunters : Team.Props;
                pd.SetPlayerTeam(team);
            }

            if (team == Team.Hunters)
            {
                hunters.Add(pd);
            }
            else
            {
                props.Add(pd);
            }
        }

        currentHideTime = hideTime;
        startHideTime = true;

        TeleportPlayers(props, mapSpawnTransform.position);
    }

    void HideDuration()
    {
        if (!startHideTime) { return; }

        if (currentHideTime > 0)
        {
            currentHideTime -= Time.deltaTime;
            UpdateTimerTextRpc("Hunters coming in: ", Mathf.RoundToInt(currentHideTime).ToString());
        }
        else
        {
            TeleportPlayers(hunters, mapSpawnTransform.position);
            startHideTime = false;
        }
    }

    [Rpc(SendTo.Everyone)]
    void UpdateTimerTextRpc(string text, string time)
    {
        timerText.gameObject.SetActive(true);
        timerText.text = text + time;
    }

    void TeleportPlayers(List<PlayerData> players, Vector3 pos)
    {
        if (players.Count == 0)
        {
            Debug.LogWarning("No players to teleport");
            return;
        }

        foreach (PlayerData player in players)
        {
            if (player == null) { continue; }

            player.GetComponent<PlayerMovement>().TeleportOwnerRpc(pos, Quaternion.identity);
        }
    }

    public Color[] GetPlayerColors()
    {
        return playerColors;
    }

    public List<PlayerMovement> GetPlayerList()
    {
        return playerList;
    }
}