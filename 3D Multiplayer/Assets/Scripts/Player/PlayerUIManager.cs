using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

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

    public void OnPauseGame(InputValue value)
    {
        if (!IsOwner) { return; }

        OpenClosePauseMenu();
    }

    void OpenClosePauseMenu()
    {
        pauseMenuActive = !pauseMenuActive;
        pauseMenu.SetActive(pauseMenuActive);
        myMovement.SetCanMove(!pauseMenuActive);
    }

    public void OnMainMenuButtonClicked()
    {
        if (!IsOwner) { return; }

        string sceneName = ConnectionManager.Instance.mainMenuSceneName;
        DisconnectClient();
        SceneManager.LoadScene(sceneName);
    }

    public void OnQuitButtonClicked()
    {
        if (!IsOwner) { return; }

        DisconnectClient();
        Application.Quit();
    }

    void DisconnectClient()
    {
        NetworkManager.Singleton.DisconnectClient(NetworkManager.Singleton.LocalClientId);

        if (IsHost)
        {
            // Shutdown server and disconnect all clients
        }
    }
}
