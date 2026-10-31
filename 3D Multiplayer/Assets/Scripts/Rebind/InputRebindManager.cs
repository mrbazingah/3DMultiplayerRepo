using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class InputRebindManager : MonoBehaviour
{
    const string SaveKey = "InputBindings";

    public static event Action BindingsChanged;

    [SerializeField] PlayerInput playerInput;

    void OnEnable()
    {
        BindingsChanged += ApplySavedBindings;
    }

    void OnDisable()
    {
        BindingsChanged -= ApplySavedBindings;
    }

    void Start()
    {
        ApplySavedBindings();
    }

    void ApplySavedBindings()
    {
        LoadBindings(playerInput.actions);
    }

    public static void SaveBindings(InputActionAsset asset)
    {
        PlayerPrefs.SetString(SaveKey, asset.SaveBindingOverridesAsJson());
        PlayerPrefs.Save();

        BindingsChanged?.Invoke();
    }

    public static void LoadBindings(InputActionAsset asset)
    {
        string json = PlayerPrefs.GetString(SaveKey, "");
        if (string.IsNullOrEmpty(json)) { return; }

        asset.LoadBindingOverridesFromJson(json);
    }
}