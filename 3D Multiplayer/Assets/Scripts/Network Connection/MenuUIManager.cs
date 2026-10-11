using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

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

    public void OnEnter(InputValue value)
    {
        Debug.Log("Enter clicked");
        OnCodeSubmitButtonClicked();
    }

    public void OnCodeSubmitButtonClicked()
    {
        string code = codeInputField.text;
        if (!string.IsNullOrEmpty(code))
        {
            ConnectionManager.Instance.JoinGame(code);
        }
        else
        {
            Debug.LogWarning("Room code is empty. Please enter a valid code.");
        }
    }

    public void OnExitClicked()
    {
        codePanel.SetActive(false);
    }
}
