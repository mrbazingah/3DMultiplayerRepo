using TMPro;
using Unity.Netcode;
using UnityEngine;

public class PlayerUIManager : NetworkBehaviour
{
    [SerializeField] TextMeshProUGUI healthText;
    [SerializeField] TextMeshProUGUI ammoText;
    [SerializeField] GameObject playerCanvas;
    [SerializeField] GameObject interactField;

    TextMeshProUGUI interactFieldText;

    public override void OnNetworkSpawn()
    {
        interactFieldText = interactField.GetComponent<TextMeshProUGUI>();

        playerCanvas.SetActive(IsOwner);

        SetAmmoTextActive(false);
        SetHealthTextActive(false);
        SetInteractField(false, "");
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
        string prefix = maxAmmo == "" ? "" : "/";
        ammoText.text = newAmmo + prefix + maxAmmo;
    }

    public void SetInteractField(bool isActive, string action)
    {
        interactField.SetActive(isActive);
        interactFieldText.text = "[E] " + action;
    }
}
