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

    void Start()
    {
        relayManager = FindFirstObjectByType<RelayManager>();

        uiCanvas.SetActive(isOpen);

        if (relayManager != null)
        {
            roomCode = relayManager.JoinCode;
            roomCodeText.text = "Room code: " + roomCode;
        }
    }

    void Update()
    {
        startGameButtonObject.SetActive(IsServer && IsOwner);
    }

    public void OpenCloseUiCanvas(PlayerMovement newMovement = null)
    {
        if (playerMovement == null)
        {
            playerMovement = newMovement;
        }

        isOpen = !isOpen;
        uiCanvas.SetActive(isOpen);

        playerMovement.SetCanMove(!isOpen);
    }

    public void StartGame()
    {
        if (!IsOwner || !IsServer) { return; }

        Debug.Log("Game Started");

        GameManager.Instance.StartGame();
    }

    public bool GetIsOpen()
    {
        return isOpen;
    }
}
