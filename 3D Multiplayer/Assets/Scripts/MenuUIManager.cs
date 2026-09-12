using TMPro;
using UnityEngine;

public class MenuUIManager : MonoBehaviour
{
    [SerializeField] GameObject codePanel;
    [SerializeField] TMP_InputField codeInputField;

    void Start()
    {
        OnExitClicked();
    }

    public void OnHostButtonClicked()
    {
        ConnectionManager.Instance.HostGame();
    }

    public void OnJoinButtonClicked()
    {
        codePanel.SetActive(true);
    }

    public void OnCodeSubmitButtonClicked()
    {
        string code = codeInputField.text;
        if (!string.IsNullOrEmpty(code))
        {
            ConnectionManager.Instance.JoinGame(code);
        }
    }

    public void OnExitClicked()
    {
        codePanel.SetActive(false);
    }
}
