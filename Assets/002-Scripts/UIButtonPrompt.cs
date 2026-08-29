using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class UIButtonPrompt : MonoBehaviour
{
    public enum CompositePart { None, Up, Down, Left, Right }

    [SerializeField] private InputActionReference actionReference;
    [SerializeField] private CompositePart targetPart = CompositePart.None;

    [SerializeField] private Image image;
    [SerializeField] private SpriteRenderer spr;

    [SerializeField] private bool isAlwaysUpdate = false;
    [SerializeField] private bool isNativeSizeOn = false;

    private bool isActive = false;

    void Awake()
    {
        if (!image) TryGetComponent(out image);
        if (!spr) TryGetComponent(out spr);
    }

    void OnEnable()
    {
        UpdateIcon();

        if (actionReference != null && actionReference.action != null)
        {
            actionReference.action.Enable();
        }
    }

    void Update()
    {
        if (isAlwaysUpdate)
        {
            UpdateIcon();

            if (actionReference != null && actionReference.action != null)
            {
                if (actionReference.action.enabled == false)
                actionReference.action.Enable();
            }
        }
    }

    public void UpdateIcon()
    {
        if (actionReference == null || InputIconDatabase.instance == null)
            return;

        // 1. Load the player's preferred InputMode from your save system
        InputMode currentMode = LoadInputModeFromSave();

        // 2. Extract the correct binding path based on that mode (handles default & rebinds)
        string activePath = GetBindingPathForMode(actionReference.action, currentMode);

        // 3. Query your database
        Sprite icon = InputIconDatabase.instance.GetSpriteForPath(activePath, currentMode);

        // 4. Display or hide the sprite
        if (icon != null)
        {
            if(image != null)
            {
                image.sprite = icon;
                image.enabled = true;

                if(isNativeSizeOn)
                    image.SetNativeSize();
            }

            if (spr != null)
            {
                spr.sprite = icon;
                spr.enabled = true;
            }
        }
        else
        {
            if (image != null)
                image.enabled = false; // Hide the image component if no sprite matches

            if (spr != null)
                spr.enabled = false; // Hide the image component if no sprite matches

            Debug.LogWarning($"No icon found for path: {activePath} in mode: {currentMode}");
        }
    }

    private InputMode LoadInputModeFromSave()
    {
        return InputSchemeManager.instance.CurrentInputMode;
    }

    private string GetBindingPathForMode(InputAction action, InputMode mode)
    {
        if (action == null) return string.Empty;

        // Determine which Input System control scheme group to look for
        string targetGroup = "Keyboard&Mouse";
        if (mode == InputMode.Xbox || mode == InputMode.PlayStation)
        {
            targetGroup = "Gamepad"; // Or "Xbox"/"PlayStation" depending on your Input Actions setup
        }

        foreach (var binding in action.bindings)
        {
            if (binding.groups.Contains(targetGroup))
            {
                // CASE 1: We are looking for a specific composite part (W, A, S, or D)
                if (targetPart != CompositePart.None)
                {
                    if (binding.isPartOfComposite && binding.name.Equals(targetPart.ToString(), System.StringComparison.OrdinalIgnoreCase))
                    {
                        return !string.IsNullOrEmpty(binding.overridePath) ? binding.overridePath : binding.path;
                    }
                }
                // CASE 2: Normal single-button action (like 'E' or 'Space')
                else
                {
                    // Skip the composite header row itself (e.g., skip "WASD")
                    if (binding.isComposite) continue;

                    return !string.IsNullOrEmpty(binding.overridePath) ? binding.overridePath : binding.path;
                }
            }
        }

        return string.Empty;
    }
}
