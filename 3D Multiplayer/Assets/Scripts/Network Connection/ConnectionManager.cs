using System;
using System.Collections;
using System.Threading.Tasks;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ConnectionManager : MonoBehaviour
{
    public static ConnectionManager Instance { get; private set; }

    [field: SerializeField] public string gameSceneName { get; private set; }
    [field: SerializeField] public string mainMenuSceneName { get; private set; }

    [SerializeField] int maxConnections;
    [SerializeField] GameObject errorMessageObject;
    [SerializeField] TextMeshProUGUI errorMessageText;

    bool isConnecting;
    bool isLeaving;

    RelayManager relayManager;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            gameObject.SetActive(false);
            Destroy(gameObject);
        }
    }

    void Start()
    {
        relayManager = GetComponent<RelayManager>();

        TryInitializeRelay();

        if (NetworkManager.Singleton)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        }

        errorMessageObject.SetActive(false);
    }

    async void TryInitializeRelay()
    {
        try
        {
            await relayManager.InitializeAsync();
        }
        catch (Exception e)
        {
            Debug.LogError("Failed to initialize relay: " + e);
        }
    }

    public void HostGame()
    {
        if (isConnecting || !NetworkManager.Singleton) { return; }

        isConnecting = true;
        TryHostGame();
    }

    async void TryHostGame()
    {
        try
        {
            await HostGameAsync();
        }
        catch (Exception e)
        {
            Debug.LogError("Failed to host game: " + e);
            Abort();
        }
    }

    async Task HostGameAsync()
    {
        await relayManager.InitializeAsync();

        string code = await relayManager.CreateRelayAsync(maxConnections);
        Debug.Log("Join code: " + code);

        AsyncOperation load = SceneManager.LoadSceneAsync(gameSceneName);
        while (!load.isDone)
        {
            await Task.Yield();
        }

        if (!NetworkManager.Singleton.StartHost())
        {
            Debug.LogError("Failed to start host.");
            Abort();
        }
    }

    public void JoinGame(string roomCode)
    {
        if (isConnecting || !NetworkManager.Singleton) { return; }

        isConnecting = true;
        TryJoinGame(roomCode);
    }

    async void TryJoinGame(string roomCode)
    {
        try
        {
            await JoinGameAsync(roomCode);
        }
        catch (Exception e)
        {
            Debug.LogError("Failed to join game: " + e);
            Abort();
        }
    }

    async Task JoinGameAsync(string roomCode)
    {
        await relayManager.InitializeAsync();
        await relayManager.JoinRelayAsync(roomCode);

        if (!NetworkManager.Singleton.StartClient())
        {
            Debug.LogError("Failed to start client.");
            Abort();
        }
    }

    void Abort()
    {
        isConnecting = false;
        LeaveGame(mainMenuSceneName);
    }

    // Runs here and not on the player since this object survives both the shutdown and the scene load
    public void LeaveGame(string sceneName = null)
    {
        // Stops a double click starting two shutdowns
        if (isLeaving) { return; }

        StartCoroutine(LeaveGameRoutine(sceneName));
    }

    IEnumerator LeaveGameRoutine(string sceneName)
    {
        isLeaving = true;

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.Shutdown();

            // Loading the scene mid-shutdown can throw while network objects are still being despawned
            yield return new WaitUntil(() => NetworkManager.Singleton == null || !NetworkManager.Singleton.ShutdownInProgress);
        }

        if (sceneName != null && SceneManager.GetActiveScene().name != sceneName)
        {
            SceneManager.LoadScene(sceneName);
        }

        // A player kicked mid-game still has the cursor locked from SetCanMove(true), which would leave the menu unclickable
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        isLeaving = false;
    }

    void OnClientConnected(ulong clientId)
    {
        if (clientId != NetworkManager.Singleton.LocalClientId) { return; }

        isConnecting = false;
    }

    void OnClientDisconnected(ulong clientId)
    {
        if (isLeaving) { return; }

        // The server gets this every time any client leaves, the host should stay in the game
        if (NetworkManager.Singleton.IsServer) { return; }

        errorMessageObject.SetActive(true);

        // Only reached on a client that lost the host, or one whose connection attempt timed out
        isConnecting = false;
        LeaveGame(mainMenuSceneName);
    }

    public void CloseErrorMessage()
    {
        errorMessageObject.SetActive(false);
    }

    void OnDestroy()
    {
        if (!NetworkManager.Singleton) { return; }

        NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
    }
}