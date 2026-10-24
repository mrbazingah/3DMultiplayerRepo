using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerUIManager : NetworkBehaviour
{
    [SerializeField] TextMeshProUGUI healthText;
    [SerializeField] TextMeshProUGUI ammoText;
    [SerializeField] GameObject playerCanvas;
    [SerializeField] GameObject interactField;
    [SerializeField] GameObject lockRotField;
    [Space]
    [SerializeField] GameObject pauseMenu;

    TextMeshProUGUI interactFieldText;
    TextMeshProUGUI lockRotFieldText;

    bool pauseMenuActive;

    PlayerMovement myMovement;
    LobbyUIManager lobbyUIManager;

    void Awake()
    {
        myMovement = GetComponent<PlayerMovement>();
        interactFieldText = interactField.GetComponent<TextMeshProUGUI>();
        lockRotFieldText = lockRotField.GetComponent<TextMeshProUGUI>();

        SetAmmoTextActive(false);
        SetHealthTextActive(false);
        SetInteractField(false, "");
        SetLockRotField(false, "");

        pauseMenu.SetActive(false);
    }

    void Start()
    {
        lobbyUIManager = FindFirstObjectByType<LobbyUIManager>();
    }

    public override void OnNetworkSpawn()
    {
        playerCanvas.SetActive(IsOwner);
    }

    public void SetHealthTextActive(bool isActive)
    {
        healthText.gameObject.SetActive(isActive);
    }

    public void SetAmmoTextActive(bool isActive)
    {
        ammoText.gameObject.SetActive(isActive);
    }

    public void UpdateHealthText(string currentHealth)
    {
        healthText.text = currentHealth;
    }

    public void UpdateAmmoText(string newAmmo, string maxAmmo)
    {
        ammoText.text = newAmmo + "/" + maxAmmo;
    }

    public void SetInteractField(bool isActive, string action)
    {
        interactField.SetActive(isActive);
        interactFieldText.text = "[E] " + action;
    }

    public void SetLockRotField(bool isActive, string action)
    {
        lockRotField.SetActive(isActive);
        lockRotFieldText.text = "[R] " + action;
    }

    public void OnEscape(InputValue value)
    {
        if (!IsOwner) { return; }

        if (lobbyUIManager != null && lobbyUIManager.GetIsOpen())
        {
            lobbyUIManager.OpenCloseCanvas(gameObject);
            return;
        }

        OpenClosePauseMenu();
    }

    void OpenClosePauseMenu()
    {
        pauseMenuActive = !pauseMenuActive;
        pauseMenu.SetActive(pauseMenuActive);

        // Only blocks input, what each team is allowed to do stays with PlayerShooting and PlayerModelManager
        myMovement.SetCanMove(!pauseMenuActive);
    }

    public void OnResumeButtonClicked()
    {
        if (!IsOwner) { return; }

        OpenClosePauseMenu();
    }

    public void OnOptionsButtonClicked()
    {
        if (!IsOwner) { return; }

        Debug.Log("Options Button Clicked");
    }

    public void OnMainMenuButtonClicked()
    {
        if (!IsOwner) { return; }

        Debug.Log("Main Menu Button Clicked");

        string sceneName = ConnectionManager.Instance.mainMenuSceneName;
        ConnectionManager.Instance.LeaveGame(sceneName);
    }

    public void OnQuitButtonClicked()
    {
        if (!IsOwner) { return; }

        ConnectionManager.Instance.LeaveGame();

        Application.Quit();
    }
}