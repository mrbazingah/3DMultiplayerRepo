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
            roomCodeText.text = roomCode;
        }
    }

    void Update()
    {
        startGameButtonObject.SetActive(IsServer && IsOwner);
    }

    public void OpenCloseCanvas(GameObject newMovement = null)
    {
        if (newMovement.TryGetComponent<PlayerMovement>(out PlayerMovement movement))
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
