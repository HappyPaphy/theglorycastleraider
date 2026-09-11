using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

public enum QuickMenuType
{
    Spell,
    Item,
    LeftWeapon,
    RightWeapon,
}

public class QuickRadialMenu : MonoBehaviour
{
    public QuickMenuType menuType;

    [Header("UI Panels")]
    public GameObject panel_RadialMenu;

    [Header("Radial Slices (Start Top, go Clockwise)")]
    [Tooltip("0: Top, 1: Top-Right, 2: Bottom-Right, 3: Bottom(Cancel), 4: Bottom-Left, 5: Top-Left")]
    public UIFeedback[] uiFeedback = new UIFeedback[6];
    public Image[] itemIcons = new Image[6];
    public TextMeshProUGUI[] quantityTexts = new TextMeshProUGUI[6];

    [SerializeField] private int cancelWeaponIndex = 3;

    [Header("Details Panel (Right Side)")]
    public Image detailLargeIcon;
    public TextMeshProUGUI detailNameText;
    public TextMeshProUGUI detailDescriptionText;

    [Tooltip("How far the stick/mouse needs to move to register a direction")]
    public float inputDeadzone = 0.2f;

    [Tooltip("Higher value = faster mouse response, Lower value = smoother/less sensitive")]
    [SerializeField] private float inputSmoothing = 25f;
    private Vector2 smoothedInput;

    private int hoveredSliceIndex = -1;

    [HideInInspector] public bool isMenuActive = false;

    private void Awake()
    {
        panel_RadialMenu.SetActive(false);
    }

    /// <summary>
    /// Opens the menu, slows time, and populates the icons based on the loadout.
    /// </summary>
    public void OpenMenu()
    {
        if (panel_RadialMenu.activeInHierarchy) return;

        isMenuActive = true;
        panel_RadialMenu.SetActive(true);
        Time.timeScale = 0.15f;

        hoveredSliceIndex = -1;
        smoothedInput = Vector2.zero; // Reset momentum on open
        RefreshWheelVisuals();
    }

    /// <summary>
    /// Closes the menu, restores time, and returns the equipped loadout array index.
    /// Returns -1 if cancelled or empty.
    /// </summary>
    public int CloseMenu()
    {
        isMenuActive = false;
        panel_RadialMenu.SetActive(false);
        Time.timeScale = 1f;

        // --- FIX: Clear the selection so background UI doesn't break ---
        EventSystem.current.SetSelectedGameObject(null);

        if (hoveredSliceIndex == cancelWeaponIndex || hoveredSliceIndex == -1) return -1; // Cancelled

        return GetLoadoutIndex(hoveredSliceIndex);
    }

    /// <summary>
    /// Call this every frame while the button is held to calculate the stick/mouse angle.
    /// </summary>
    public void UpdateMenu(Vector2 inputVector)
    {
        smoothedInput = Vector2.Lerp(smoothedInput, inputVector, Time.unscaledDeltaTime * inputSmoothing);

        if (smoothedInput.magnitude > inputDeadzone)
        {
            float angle = -Vector2.SignedAngle(Vector2.up, smoothedInput);
            if (angle < 0) angle += 360f;

            int newHoverIndex = -1;

            if (menuType == QuickMenuType.Item || menuType == QuickMenuType.Spell)
            {
                // --- 6-WAY MATH (60 degrees) for Items & Spells ---
                float offsetAngle = angle + 30f;
                if (offsetAngle >= 360f) offsetAngle -= 360f;
                newHoverIndex = Mathf.FloorToInt(offsetAngle / 60f);
            }
            else
            {
                // --- 4-WAY MATH (90 degrees) for Weapons ---
                float offsetAngle = angle + 45f;
                if (offsetAngle >= 360f) offsetAngle -= 360f;
                int quadrant = Mathf.FloorToInt(offsetAngle / 90f);

                switch (quadrant)
                {
                    case 0: newHoverIndex = 0; break;
                    case 1: newHoverIndex = 1; break;
                    case 2: newHoverIndex = cancelWeaponIndex; break;
                    case 3: newHoverIndex = 3; break;
                }
            }

            if (newHoverIndex != hoveredSliceIndex)
            {
                hoveredSliceIndex = newHoverIndex;
                UpdateHighlights();
                UpdateDetailsPanel();
            }
        }
    }

    private void UpdateHighlights()
    {
        // Clear the selection if the stick/mouse is in the center deadzone
        if (hoveredSliceIndex == -1)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }

        for (int i = 0; i < uiFeedback.Length; i++)
        {
            if (i == hoveredSliceIndex)
            {
                // --- FIX: Tell the EventSystem this slice is actively selected ---
                EventSystem.current.SetSelectedGameObject(uiFeedback[i].gameObject);
                uiFeedback[i].SelectedUI();
            }
            else
            {
                uiFeedback[i].UnSelectedUI();
            }
        }
    }

    private void RefreshWheelVisuals()
    {
        for (int i = 0; i < uiFeedback.Length; i++)
        {
            if (i == cancelWeaponIndex) // Bottom Cancel Slice
            {
                itemIcons[i].gameObject.SetActive(true); // Put your red X sprite here in inspector
                //quantityTexts[i].gameObject.SetActive(false);
                continue;
            }

            int loadoutIndex = GetLoadoutIndex(i);
            Item equippedItem = GetItemAtLoadoutIndex(loadoutIndex);

            if (equippedItem != null && equippedItem.spr_Icon != null)
            {
                itemIcons[i].sprite = equippedItem.spr_Icon;
                itemIcons[i].gameObject.SetActive(true);

                if (menuType == QuickMenuType.Item && InventoryManager.instance.consumables.ContainsKey(equippedItem.itemType))
                {
                    if (quantityTexts.Length > 0)
                    {
                        quantityTexts[i].text = InventoryManager.instance.consumables[equippedItem.itemType].ToString();
                        quantityTexts[i].gameObject.SetActive(true);
                    }
                }
                else
                {
                    if (quantityTexts.Length > 0)
                        quantityTexts[i].gameObject.SetActive(false);
                }
            }
            else
            {
                itemIcons[i].gameObject.SetActive(false);

                if (quantityTexts.Length > 0)
                    quantityTexts[i].gameObject.SetActive(false);
            }
        }
        UpdateHighlights();
        UpdateDetailsPanel();
    }

    private void UpdateDetailsPanel()
    {
        if (hoveredSliceIndex == cancelWeaponIndex || hoveredSliceIndex == -1) // Cancel Slot
        {
            detailLargeIcon.gameObject.SetActive(false);
            detailNameText.text = "CANCEL";
            detailDescriptionText.text = "Close quick select.";
            return;
        }

        int loadoutIndex = GetLoadoutIndex(hoveredSliceIndex);
        Item hoveredItem = GetItemAtLoadoutIndex(loadoutIndex);

        if (hoveredItem != null)
        {
            detailLargeIcon.gameObject.SetActive(true);
            detailLargeIcon.sprite = hoveredItem.spr_Icon;
            detailNameText.text = hoveredItem.equipmentName;
            detailDescriptionText.text = "Description mapped from item data...";
        }
        else
        {
            detailLargeIcon.gameObject.SetActive(false);
            detailNameText.text = "EMPTY";
            detailDescriptionText.text = "";
        }
    }

    // Maps the 6-slice hexagon to your 5 Items or 4 Spells arrays
    private int GetLoadoutIndex(int sliceIndex)
    {
        if (sliceIndex == cancelWeaponIndex) return -1;

        // 2. Route the mapping based on the menu type
        switch (menuType)
        {
            case QuickMenuType.Item:
            case QuickMenuType.Spell:
                // 5 Slots Mapping (Fills all slices except Cancel)
                switch (sliceIndex)
                {
                    case 0: return 0; // Top
                    case 1: return 1; // Top-Right
                    case 2: return 2; // Bottom-Right
                    case 4: return 3; // Bottom-Left
                    case 5: return 4; // Top-Left
                    default: return -1;
                }

            case QuickMenuType.LeftWeapon:
            case QuickMenuType.RightWeapon:
                // 3 Slots Mapping (Symmetric: Top, Top-Right, Top-Left)
                switch (sliceIndex)
                {
                    case 0: return 0; // Top
                    case 1: return 1; // Top-Right
                    case 3: return 2; // Top-Left
                    default: return -1; // Slices 2 and 4 remain empty
                }

            default:
                return -1;
        }
    }

    private Item GetItemAtLoadoutIndex(int index)
    {
        if (index < 0) return null;

        // Route the query based on the enum type assigned in the inspector
        switch (menuType)
        {
            case QuickMenuType.Item:
                if (index < PlayerWeaponManager.instance.equippedItems.Length)
                    return PlayerWeaponManager.instance.equippedItems[index];
                break;
            case QuickMenuType.Spell:
                if (index < PlayerWeaponManager.instance.equippedSpells.Length)
                    return PlayerWeaponManager.instance.equippedSpells[index];
                break;
            case QuickMenuType.LeftWeapon:
                if (index < PlayerWeaponManager.instance.leftHandWeapons.Length)
                    return PlayerWeaponManager.instance.leftHandWeapons[index];
                break;
            case QuickMenuType.RightWeapon:
                if (index < PlayerWeaponManager.instance.rightHandWeapons.Length)
                    return PlayerWeaponManager.instance.rightHandWeapons[index];
                break;
        }
        return null;
    }
}