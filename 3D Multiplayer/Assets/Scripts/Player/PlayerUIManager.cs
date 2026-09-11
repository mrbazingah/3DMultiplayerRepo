using TMPro;
using Unity.Netcode;
using UnityEngine;

public class PlayerUIManager : NetworkBehaviour
{
    [SerializeField] TextMeshProUGUI healthText;
    [SerializeField] TextMeshProUGUI ammoText;
    [SerializeField] GameObject playerCanvas;
    [SerializeField] GameObject interactField;
    [SerializeField] GameObject lockRotField;

    TextMeshProUGUI interactFieldText;
    TextMeshProUGUI lockRotFieldText;

    void Awake()
    {
        interactFieldText = interactField.GetComponent<TextMeshProUGUI>();
        lockRotFieldText = lockRotField.GetComponent<TextMeshProUGUI>();

        SetAmmoTextActive(false);
        SetHealthTextActive(false);
        SetInteractField(false, "");
        SetLockRotField(false, "");
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
}
