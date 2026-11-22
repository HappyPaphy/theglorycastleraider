using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One line of the top-right "item obtained" popup. Lives on the prefab Resources/UI/ItemPickupPopupEntry:
/// open the prefab to change sprites, layout, fonts and colours. The code only fills in the icon and texts.
/// The stacking and timing values below are read from the prefab too.
/// </summary>
[RequireComponent(typeof(RectTransform), typeof(CanvasGroup))]
public class ItemPickupPopupEntry : MonoBehaviour
{
    public enum IconSource { NoFrame, Frame }

    [Header("Parts (filled in by code)")]
    public Image icon;
    [Tooltip("Optional: hidden together with the icon when an entry has no icon (e.g. a frame behind it).")]
    public GameObject iconGroup;
    public TMP_Text title;
    public TMP_Text subtitle;

    [Header("Icon")]
    [Tooltip("Which of the item's icons to show (falls back to the other one if missing).")]
    public IconSource iconSource = IconSource.NoFrame;

    [Header("Stacking")]
    [Tooltip("Gap between two popups.")]
    public float spacing = 8f;
    [Tooltip("Gap between the minimap and the first popup.")]
    public float gapBelowMinimap = 16f;
    [Range(1, 10)] public int maxEntries = 5;

    [Header("Timing (real seconds, works while paused)")]
    public float slideTime = 0.25f;
    public float holdTime = 3f;
    public float fadeTime = 0.4f;

    [System.NonSerialized] public RectTransform rect;
    [System.NonSerialized] public CanvasGroup group;

    private void Awake()
    {
        rect = (RectTransform)transform;
        group = GetComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;
    }

    public void Fill(string titleText, string subtitleText, Sprite iconSprite)
    {
        if (title != null) { title.text = titleText ?? ""; }
        SetSubtitle(subtitleText);

        bool hasIcon = iconSprite != null && icon != null;
        if (icon != null) { icon.sprite = iconSprite; icon.enabled = hasIcon; }
        if (iconGroup != null) { iconGroup.SetActive(hasIcon); }
    }

    public void SetSubtitle(string text)
    {
        if (subtitle != null) { subtitle.text = text ?? ""; }
    }
}
