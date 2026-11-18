using Newtonsoft.Json.Serialization;
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class PlayerUIManager : NetworkBehaviour
{
    [SerializeField] TextMeshProUGUI healthText;
    [SerializeField] TextMeshProUGUI ammoText;
    [SerializeField] GameObject playerCanvas;
    [SerializeField] GameObject interactField;
    [SerializeField] GameObject lockRotField;
    [SerializeField] GameObject tauntField;
    [Space]
    [SerializeField] GameObject pauseMenu;
    [Space]
    [SerializeField] GameObject pausePanel;
    [SerializeField] GameObject optionsPanel;
    [SerializeField] GameObject controlsPanel;
    [Space]
    [SerializeField] Vector3 startPos;
    [SerializeField] float yOffset;

    TextMeshProUGUI interactFieldText;
    TextMeshProUGUI lockRotFieldText;
    TextMeshProUGUI tauntFieldText;

    bool pauseMenuActive;

    List<RectTransform> keyFields;
    List<GameObject> menus;

    PlayerMovement myMovement;
    LobbyUIManager lobbyUIManager;

    void Awake()
    {
        myMovement = GetComponent<PlayerMovement>();

        interactFieldText = interactField.GetComponent<TextMeshProUGUI>();
        lockRotFieldText = lockRotField.GetComponent<TextMeshProUGUI>();
        tauntFieldText = tauntField.GetComponent<TextMeshProUGUI>();

        keyFields = new List<RectTransform> {interactField.GetComponent<RectTransform>(), lockRotField.GetComponent<RectTransform>(), tauntField.GetComponent<RectTransform>() };

        SetAmmoTextActive(false);
        SetHealthTextActive(false);
        SetInteractField(false, "");
        SetLockRotField(false, "");

        menus = new List<GameObject> { pausePanel, optionsPanel, controlsPanel };
        foreach (GameObject menu in menus)
        {
            if (menu == null || menu == pausePanel) { continue; }
            menu.SetActive(false);
        }

        pauseMenu.SetActive(false); 
        pausePanel.SetActive(true);
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
        SetField(interactField, interactFieldText, "E", isActive, action);
    }

    public void SetLockRotField(bool isActive, string action)
    {
        SetField(lockRotField, lockRotFieldText, "R", isActive, action);
    }

    public void SetTauntField(bool isActive, string action)
    {
        SetField(tauntField, tauntFieldText, "T", isActive, action);
    }

    void SetField(GameObject field, TextMeshProUGUI fieldText, string key, bool isActive, string action)
    {
        field.SetActive(isActive);
        fieldText.text = $"[{key}] {action}";
    }

    void Update()
    {
        SortFields();
    }

    void SortFields()
    {
        List<RectTransform> activeFields = new List<RectTransform>();
        foreach (RectTransform rt in keyFields)
        {
            if (rt == null) { continue; }

            if (rt.gameObject.activeInHierarchy)
            {
                activeFields.Add(rt);
            }
            else
            {
                activeFields.Remove(rt);
            }
        }

        for (int i = 0; i < activeFields.Count; i++)
        {
            Vector3 offset = new Vector3(0, i * -yOffset, 0);
            activeFields[i].anchoredPosition = startPos + offset;
        }
    }

    public void OnEscape(InputValue value)
    {
        if (!IsOwner) { return; }

        foreach (GameObject menu in menus)
        {
            if (menu == null) { continue; }
            if (menu.activeSelf)
            {
                menu.transform.Find("Return Button").GetComponent<Button>().onClick.Invoke();
                return;
            }
        }

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

    public void OnMenu(GameObject menu)
    {
        menu.SetActive(!menu.activeSelf);
    }

    public void OnQuitButtonClicked()
    {
        if (!IsOwner) { return; }

        ConnectionManager.Instance.LeaveGame();

        Application.Quit();
    }
}