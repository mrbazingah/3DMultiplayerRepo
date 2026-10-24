using TMPro;
using Unity.Netcode;
using UnityEngine;

public class LobbyUIManager : NetworkBehaviour
{
    [SerializeField] GameObject uiCanvas;
    [SerializeField] TextMeshProUGUI roomCodeText;
    [SerializeField] GameObject startGameButtonObject;

    string roomCode;
    bool isOpen;

    RelayManager relayManager;
    PlayerMovement playerMovement;

    void Awake()
    {
        uiCanvas.SetActive(isOpen);
    }

    void Start()
    {
        relayManager = FindFirstObjectByType<RelayManager>();

        if (relayManager != null)
        {
            roomCode = relayManager.JoinCode;
            roomCodeText.text = roomCode;
        }
    }

    void Update()
    {
        startGameButtonObject.SetActive(IsServer && IsOwner);
    }

    public void OpenCloseCanvas(GameObject newMovement = null)
    {
        if (newMovement.TryGetComponent(out PlayerMovement movement))
        {
            playerMovement = movement;
        }
        
        OpenCloseUiCanvas();
    }

    public void OpenCloseUiCanvas()
    {
        isOpen = !isOpen;
        uiCanvas.SetActive(isOpen);

        playerMovement.SetCanMove(!isOpen);
    }

    public void OnColorButtonClicked(int colorIndex)
    {
        if (!IsOwner) { return; }

        Color selectedColor = GameManager.Instance.GetPlayerColors()[colorIndex];
        playerMovement.GetComponent<PlayerData>().SetPlayerColor(selectedColor);
    }

    public void StartGame()
    {
        if (!IsOwner || !IsServer) { return; }

        Debug.Log("Game Started");

        OpenCloseUiCanvas();
        GameManager.Instance.StartGame();
    }

    public bool GetIsOpen()
    {
        return isOpen;
    }
}
