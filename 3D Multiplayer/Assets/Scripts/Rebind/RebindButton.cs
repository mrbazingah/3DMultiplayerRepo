using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class RebindButton : MonoBehaviour
{
    [SerializeField] InputActionReference actionReference;
    [SerializeField] string compositePart;
    [SerializeField] string displayName;
    [Space]
    [SerializeField] TextMeshProUGUI titleText;
    [SerializeField] Button rebindButton;
    [SerializeField] TextMeshProUGUI bindingText;
    [SerializeField] GameObject waitingText;

    InputAction action;
    int bindingIndex = -1;
    bool actionWasEnabled;

    InputActionRebindingExtensions.RebindingOperation rebindOperation;

    void Awake()
    {
        action = actionReference.action;
        bindingIndex = FindBindingIndex();

        titleText.text = displayName;
        rebindButton.onClick.AddListener(StartRebind);
    }

    void OnEnable()
    {
        InputRebindManager.LoadBindings(action.actionMap.asset);

        waitingText.SetActive(false);
        rebindButton.gameObject.SetActive(true);
        UpdateBindingText();
    }

    void OnDisable()
    {
        // Menu closed in the middle of a rebind
        if (rebindOperation == null) { return; }

        InputActionRebindingExtensions.RebindingOperation operation = rebindOperation;
        rebindOperation = null;
        operation.Dispose();

        if (actionWasEnabled)
        {
            action.Enable();
        }
    }

    // Finds the keyboard/mouse binding, so gamepad bindings are left alone
    int FindBindingIndex()
    {
        bool wantsPart = !string.IsNullOrEmpty(compositePart);

        for (int i = 0; i < action.bindings.Count; i++)
        {
            InputBinding binding = action.bindings[i];

            if (binding.isComposite) { continue; }
            if (wantsPart && (!binding.isPartOfComposite || binding.name != compositePart)) { continue; }
            if (!wantsPart && binding.isPartOfComposite) { continue; }

            if (binding.path.StartsWith("<Keyboard>") || binding.path.StartsWith("<Mouse>"))
            {
                return i;
            }
        }

        Debug.LogError("No keyboard/mouse binding found for " + action.name + " " + compositePart, this);
        return -1;
    }

    void StartRebind()
    {
        if (bindingIndex < 0) { return; }

        rebindButton.gameObject.SetActive(false);
        waitingText.SetActive(true);

        // Actions can't be rebound while they're enabled
        actionWasEnabled = action.enabled;
        action.Disable();

        rebindOperation = action.PerformInteractiveRebinding(bindingIndex)
            .WithControlsExcluding("<Mouse>/position")
            .WithControlsExcluding("<Mouse>/delta")
            .WithControlsExcluding("<Mouse>/scroll")
            .WithControlsExcluding("<Gamepad>")
            .WithCancelingThrough("<Keyboard>/escape")
            .OnMatchWaitForAnother(0.1f)
            .OnComplete(operation => FinishRebind(true))
            .OnCancel(operation => FinishRebind(false))
            .Start();
    }

    void FinishRebind(bool completed)
    {
        // Already cleaned up by OnDisable
        if (rebindOperation == null) { return; }

        rebindOperation.Dispose();
        rebindOperation = null;

        if (actionWasEnabled)
        {
            action.Enable();
        }

        if (completed)
        {
            InputRebindManager.SaveBindings(action.actionMap.asset);
        }

        UpdateBindingText();
        waitingText.SetActive(false);
        rebindButton.gameObject.SetActive(true);
    }

    void UpdateBindingText()
    {
        if (bindingIndex < 0) { return; }

        bindingText.text = action.GetBindingDisplayString(bindingIndex);
    }
}