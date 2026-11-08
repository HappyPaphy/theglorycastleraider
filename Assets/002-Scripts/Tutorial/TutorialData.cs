using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class TutorialPage
{
    [Header("GIF (sprite frames)")]
    [Tooltip("Frames of the animated image, in order. Leave empty to hide the image. A single sprite shows a still image.")]
    public Sprite[] gifFrames;
    [Min(0.1f)] public float framesPerSecond = 12f;

    [Header("Text")]
    public string header;
    [TextArea(3, 8)] public string description;
}

/// <summary>Names must match the actions in the "Player" map of PlayerInputActions.</summary>
public enum TutorialHintAction
{
    None,
    Move,
    Look,
    Jump,
    Sprint,
    Dash,
    Kick,
    Interact,
    Reload,
    RightHand,
    LeftHand,
    ToggleTwoHanded_Right,
    ToggleTwoHanded_Left,
    Minimap,
    Loadout,
    Pause,
    SwitchWeaponUp,
    SwitchWeaponRight,
    SwitchWeaponDown,
    SwitchWeaponLeft,
}

[System.Serializable]
public class TutorialHint
{
    [Tooltip("Use {key} to insert the current binding of the dismiss action, e.g. \"Press {key} to Jump\".")]
    [TextArea(1, 3)] public string text;
    [Tooltip("The hint disappears when the player performs this action.")]
    public TutorialHintAction dismissAction = TutorialHintAction.None;
    [Tooltip("Seconds before the hint hides by itself. 0 = only hide on the dismiss action. " +
             "Ignored when the trigger has blockers and a dismiss action is set.")]
    [Min(0f)] public float autoHideTime = 0f;

    public bool HasText => !string.IsNullOrWhiteSpace(text);
}

[CreateAssetMenu(
    fileName = "Tutorial_",
    menuName = "Tutorial/Tutorial Data"
)]
public class TutorialData : ScriptableObject
{
    [Tooltip("If true, this tutorial only shows the first time its trigger is hit (per play session).")]
    [SerializeField] private bool showOnlyOnce = true;

    [SerializeField] private List<TutorialPage> pages = new List<TutorialPage>();

    [Header("Hints (shown one by one after the panel closes)")]
    [Tooltip("Each hint stays until the player performs its dismiss action, then the next one shows. Leave empty for no hints.")]
    [SerializeField] private List<TutorialHint> hints = new List<TutorialHint>();

    public bool ShowOnlyOnce => showOnlyOnce;
    public IReadOnlyList<TutorialHint> Hints => hints;

    public bool HasHint
    {
        get
        {
            if (hints == null) { return false; }
            foreach (TutorialHint hint in hints)
            {
                if (hint != null && hint.HasText) { return true; }
            }
            return false;
        }
    }

    public IReadOnlyList<TutorialPage> Pages => pages;
    public int PageCount => pages != null ? pages.Count : 0;
}
