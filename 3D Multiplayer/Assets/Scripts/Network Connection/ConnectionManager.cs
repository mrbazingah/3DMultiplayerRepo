using System;
using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ConnectionManager : MonoBehaviour
{
    public static ConnectionManager Instance { get; private set; }

    [SerializeField] string gameSceneName;
    [SerializeField] string mainMenuSceneName;
    [SerializeField] int maxConnections;

    bool isConnecting;

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
        }
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
        HostGameWrapper();
    }

    async void HostGameWrapper()
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
        JoinGameWrapper(roomCode);
    }

    async void JoinGameWrapper(string roomCode)
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
        NetworkManager.Singleton.Shutdown();

        if (SceneManager.GetActiveScene().name != mainMenuSceneName)
        {
            SceneManager.LoadScene(mainMenuSceneName);
        }
    }

    void OnClientConnected(ulong clientId)
    {
        if (clientId != NetworkManager.Singleton.LocalClientId) { return; }

        isConnecting = false;
    }

    void OnDestroy()
    {
        if (!NetworkManager.Singleton) { return; }

        NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
    }
}