using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class InputIconMapping
{
    [Tooltip("The path from the Input System (e.g., '<Keyboard>/e' or '<Gamepad>/buttonSouth')")]
    public string controlPath;
    public Sprite iconSprite;
}

public class InputIconDatabase : MonoBehaviour
{
    public static InputIconDatabase instance;

    public List<InputIconMapping> pcIcons;
    public List<InputIconMapping> xboxIcons;
    public List<InputIconMapping> playStationIcons;

    private void Awake()
    {
        instance = this;
    }

    // Searches our lists to find the matching sprite for the given path and device
    public Sprite GetSpriteForPath(string path, InputMode mode)
    {
        List<InputIconMapping> currentList = pcIcons;

        if (mode == InputMode.Xbox) currentList = xboxIcons;
        else if (mode == InputMode.PlayStation) currentList = playStationIcons;

        string targetKey = GetKeyNameFromPath(path);

        foreach (var mapping in currentList)
        {
            string mappingKey = GetKeyNameFromPath(mapping.controlPath);

            // We use EndsWith or Contains because paths can sometimes be tricky 
            // e.g., "/Keyboard/e" vs "<Keyboard>/e"
            if (targetKey.Equals(mappingKey, System.StringComparison.OrdinalIgnoreCase))
            {
                return mapping.iconSprite;
            }
        }

        return null; // Return null if no matching icon was found
    }

    private string GetKeyNameFromPath(string path)
    {
        if (string.IsNullOrEmpty(path)) return string.Empty;

        // Split by the slash and grab the last element (e.g., "upArrow" or "u")
        string[] parts = path.Split('/');
        string lastPart = parts[parts.Length - 1];

        // Strip away any stray formatting brackets just in case
        return lastPart.Replace("<", "").Replace(">", "").Trim();
    }
}
