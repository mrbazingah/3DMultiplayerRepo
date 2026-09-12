using System.Collections;
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
        if (!NetworkManager.Singleton) { return; }

        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
    }

    public void HostGame()
    {
        if (isConnecting || !NetworkManager.Singleton) { return; }

        isConnecting = true;
        StartCoroutine(HostGameRoutine());
    }

    IEnumerator HostGameRoutine()
    {
        AsyncOperation load = SceneManager.LoadSceneAsync(gameSceneName);
        yield return load;

        bool hostStarted = NetworkManager.Singleton.StartHost();

        if (!hostStarted)
        {
            Debug.LogError("Failed to start host.");
            
            isConnecting = false;
            NetworkManager.Singleton.Shutdown();

            SceneManager.LoadScene(mainMenuSceneName);

            yield break;
        }
    }

    public void JoinGame(string roomCode)
    {
        if (isConnecting || !NetworkManager.Singleton) { return; }

        SceneManager.LoadScene(gameSceneName);

        isConnecting = true;

        bool clientStarted = NetworkManager.Singleton.StartClient();
        if (!clientStarted)
        {
            Debug.LogError("Failed to start client.");

            isConnecting = false;
            NetworkManager.Singleton.Shutdown();

            SceneManager.LoadScene(mainMenuSceneName);

            return;
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
