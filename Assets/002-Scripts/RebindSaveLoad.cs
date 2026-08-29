using UnityEngine;
using UnityEngine.InputSystem;
using System;

public class RebindSaveLoad : MonoBehaviour
{
    [Tooltip("The associated input action asset to be serialized to player preferences (Required).")]
    public InputActionAsset actions;

    [Tooltip("The player preference key to be used when serializing binding overrides to player preferences (Required).")]
    public string playerPreferenceKey;

    [Tooltip("Specifies whether to load and apply binding overrides when the component is enabled")]
    public bool loadOnEnable = true;

    [Tooltip("Specifies whether to save binding overrides when the component is disabled")]
    public bool saveOnDisable = true;

    public static event Action OnBindingsLoaded;

    public static RebindSaveLoad instance;

    /// <summary>
    /// Loads binding overrides from player preferences and applies them to the associated input action asset.
    /// </summary>
    /// 

    private void Awake()
    {
        instance = this;
    }

    public void Load()
    {
        if (!IsValidConfiguration())
            return;

        var rebinds = PlayerPrefs.GetString(playerPreferenceKey);
        if (string.IsNullOrEmpty(rebinds))
            return;
        
        actions.Disable();
        actions.LoadBindingOverridesFromJson(rebinds);
        actions.Enable();

        // This alerts the player controller (and anything else) to load bindings
        OnBindingsLoaded?.Invoke();
    }

    public void Save()
    {
        if (!IsValidConfiguration())
            return;

        var rebinds = actions.SaveBindingOverridesAsJson();
        PlayerPrefs.SetString(playerPreferenceKey, rebinds);

        OnBindingsLoaded?.Invoke();
    }

    public void ResetAllBindings()
    {
        if (actions == null) return;

        // 1. CRITICAL: Disable all active gameplay inputs before altering mappings mid-game
        actions.Disable();

        // 2. Remove all custom overrides, restoring the default controls configured in the editor
        actions.RemoveAllBindingOverrides();

        // 3. Clear out the saved player preferences so they don't load the old data on the next launch
        if (!string.IsNullOrEmpty(playerPreferenceKey))
        {
            PlayerPrefs.DeleteKey(playerPreferenceKey);
        }

        // 4. Re-enable the system with clean default states
        actions.Enable();

        // 5. Notify the rest of the game (and UI) to update their displays and internal systems
        OnBindingsLoaded?.Invoke();
    }

    private void OnEnable()
    {
        if (loadOnEnable)
            Load();
    }

    private void OnDisable()
    {
        if (saveOnDisable)
            Save();
    }

    private bool IsValidConfiguration()
    {
        if (actions == null)
        {
            Debug.LogWarning("Unable to apply binding overrides from player preferences without an associated action asset.");
            return false;
        }

        if (string.IsNullOrEmpty(playerPreferenceKey))
        {
            Debug.LogWarning("Unable to load binding overrides from player preferences without a non-empty preference key.");
            return false;
        }

        return true;
    }
}
