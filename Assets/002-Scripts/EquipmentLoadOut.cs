using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public enum EquipmentSlotType
{
    RightHand,
    LeftHand,
    Item,
    Ring,
    Spell
}

public class EquipmentLoadOut : MonoBehaviour
{
    [SerializeField] private GameObject panel_Loadout;
    [SerializeField] private GameObject panel_Inventory;
    [SerializeField] private GameObject firstSelected_Loadout;
    [HideInInspector] public bool isPanelActive = false;

    [Header("Inventory Scroll Views & Text")]
    public TextMeshProUGUI text_CategoryName; // Maps to "Text (Category Name)"
    public GameObject scrollView_Weapons;
    public GameObject scrollView_Items;
    public GameObject scrollView_Rings;
    public GameObject scrollView_Spells;

    [Header("UI Slot Images (Icons)")]
    public Image[] rightHandSlots = new Image[2];
    public Image[] leftHandSlots = new Image[2];
    public Image[] itemSlots = new Image[5];
    public Image[] ringSlots = new Image[4];
    public Image[] spellSlots = new Image[4];

    public TextMeshProUGUI[] itemQuantityTexts = new TextMeshProUGUI[5];

    [Header("Empty State Sprites")]
    public Sprite emptyWeaponSprite;
    public Sprite emptyItemSprite;
    public Sprite emptyRingSprite;
    public Sprite emptySpellSprite;

    [Header("UI Details Panel (Right Side)")]
    public Image detailLargeIcon;
    public TextMeshProUGUI detailCategoryText;    // Maps to "Equipment Category"
    public TextMeshProUGUI detailNameLeftText;    // Maps to "Equipment Name +1"
    public TextMeshProUGUI detailNameRightText;   // Maps to "Equipment Name" (above description)
    public TextMeshProUGUI detailDescriptionText; // Maps to the long description text

    [Header("Events")]
    [Tooltip("Fires when a slot is clicked. Use this to open your Inventory Selection menu.")]
    public UnityEvent<EquipmentSlotType, int> onSlotClicked;

    [HideInInspector] public bool isInInventoryMenu = false;
    [HideInInspector] public EquipmentSlotType pendingSlotType;
    [HideInInspector] public int pendingSlotIndex;

    public static EquipmentLoadOut instance;

    private void Awake()
    {
        instance = this;
    }
    private void Start()
    {
        isPanelActive = false;
        isInInventoryMenu = false;
        panel_Loadout.SetActive(false);
        panel_Inventory.SetActive(false);
    }

    private void Update()
    {
        HandleToggleLoadoutPanel();
    }

    private void HandleToggleLoadoutPanel()
    {
        if(PlayerController.instance.IsToggleLoadoutPressed)
        {
            PlayerController.instance.IsToggleLoadoutPressed = false;

            if (panel_Inventory.activeInHierarchy)
            {
                CloseInventoryCategory();

                EventSystem.current.SetSelectedGameObject(firstSelected_Loadout);
            }
            else if (!panel_Loadout.activeInHierarchy)
            {
                Time.timeScale = 0f;
                RefreshUI();
                ClearDetailsPanel();

                panel_Loadout.SetActive(true);

                isPanelActive = true;
                EventSystem.current.SetSelectedGameObject(firstSelected_Loadout);
            }
            else if(panel_Loadout.activeInHierarchy)
            {
                Time.timeScale = 1f;
                panel_Loadout.SetActive(false);
                isPanelActive = false;
            }
        }
    }

    /// <summary>
    /// Reads the current state of the PlayerWeaponManager arrays and updates the UI sprites.
    /// </summary>
    public void RefreshUI()
    {
        if (PlayerWeaponManager.instance == null) return;

        // 1. Right Hand Weapons
        for (int i = 0; i < rightHandSlots.Length; i++)
        {
            Weapon w = PlayerWeaponManager.instance.rightHandWeapons[i];
            rightHandSlots[i].sprite = (w != null && w.spr_Weapon != null) ? w.spr_Weapon : emptyWeaponSprite;
        }

        // 2. Left Hand Weapons
        for (int i = 0; i < leftHandSlots.Length; i++)
        {
            Weapon w = PlayerWeaponManager.instance.leftHandWeapons[i];
            leftHandSlots[i].sprite = (w != null && w.spr_Weapon != null) ? w.spr_Weapon : emptyWeaponSprite;
        }

        // 3. Items & Quantities
        for (int i = 0; i < itemSlots.Length; i++)
        {
            Item item = PlayerWeaponManager.instance.equippedItems[i];
            if (item != null && item.spr_Icon != null)
            {
                itemSlots[i].sprite = item.spr_Icon;

                // Fetch quantity from InventoryManager dictionary
                int quantity = 0;
                if (InventoryManager.instance != null && InventoryManager.instance.consumables.ContainsKey(item.itemType))
                {
                    quantity = InventoryManager.instance.consumables[item.itemType];
                }
                itemQuantityTexts[i].text = quantity.ToString();
                itemQuantityTexts[i].gameObject.SetActive(true);
            }
            else
            {
                itemSlots[i].sprite = emptyItemSprite;
                itemQuantityTexts[i].gameObject.SetActive(false);
            }
        }

        // 4. Unified Spells
        for (int i = 0; i < spellSlots.Length; i++)
        {
            Item spell = PlayerWeaponManager.instance.equippedSpells[i];
            spellSlots[i].sprite = (spell != null && spell.spr_Icon != null) ? spell.spr_Icon : emptySpellSprite;
        }

        // 5. Rings
        for (int i = 0; i < ringSlots.Length; i++)
        {
            Item ring = PlayerWeaponManager.instance.equippedRings[i];
            ringSlots[i].sprite = (ring != null && ring.spr_Icon != null) ? ring.spr_Icon : emptyRingSprite;
        }
    }

    public void OpenInventoryCategory(EquipmentSlotType type, int index)
    {
        isInInventoryMenu = true;
        pendingSlotType = type;
        pendingSlotIndex = index;

        // Swap the left-side panels
        panel_Loadout.SetActive(false);
        panel_Inventory.SetActive(true);

        // Turn off all scroll views initially
        scrollView_Weapons.SetActive(false);
        scrollView_Items.SetActive(false);
        scrollView_Rings.SetActive(false);
        scrollView_Spells.SetActive(false);

        // Activate the correct scroll view and update the title text
        switch (type)
        {
            case EquipmentSlotType.RightHand:
            case EquipmentSlotType.LeftHand:
                text_CategoryName.text = "WEAPONS";
                scrollView_Weapons.SetActive(true);
                // TODO: Set EventSystem.current.SetSelectedGameObject to the first item in the weapon scroll view
                break;
            case EquipmentSlotType.Item:
                text_CategoryName.text = "ITEMS";
                scrollView_Items.SetActive(true);
                break;
            case EquipmentSlotType.Ring:
                text_CategoryName.text = "RINGS";
                scrollView_Rings.SetActive(true);
                break;
            case EquipmentSlotType.Spell:
                text_CategoryName.text = "SPELLS";
                scrollView_Spells.SetActive(true);
                break;
        }
    }

    public void CloseInventoryCategory()
    {
        isInInventoryMenu = false;
        panel_Inventory.SetActive(false);
        panel_Loadout.SetActive(true);

        // Reset focus back to the loadout grid
        EventSystem.current.SetSelectedGameObject(firstSelected_Loadout);
    }

    // ==========================================
    // UI BUTTON HOVER METHODS (Map to UIFeedback -> On Hover)
    // ==========================================
    public void OnRightHandSlotHovered(int index) => PreviewItemDetails(EquipmentSlotType.RightHand, index);
    public void OnLeftHandSlotHovered(int index) => PreviewItemDetails(EquipmentSlotType.LeftHand, index);
    public void OnItemSlotHovered(int index) => PreviewItemDetails(EquipmentSlotType.Item, index);
    public void OnSpellSlotHovered(int index) => PreviewItemDetails(EquipmentSlotType.Spell, index);
    public void OnRingSlotHovered(int index) => PreviewItemDetails(EquipmentSlotType.Ring, index);

    // ==========================================
    // UI BUTTON CLICK METHODS (Map to UIFeedback -> On Click)
    // ==========================================
    public void OnRightHandSlotClicked(int index) => HandleSlotClick(EquipmentSlotType.RightHand, index);
    public void OnLeftHandSlotClicked(int index) => HandleSlotClick(EquipmentSlotType.LeftHand, index);
    public void OnItemSlotClicked(int index) => HandleSlotClick(EquipmentSlotType.Item, index);
    public void OnSpellSlotClicked(int index) => HandleSlotClick(EquipmentSlotType.Spell, index);
    public void OnRingSlotClicked(int index) => HandleSlotClick(EquipmentSlotType.Ring, index);


public void PreviewItemDetails(EquipmentSlotType slotType, int index)
    {
        Item targetItem = null;

        switch (slotType)
        {
            case EquipmentSlotType.RightHand:
                targetItem = PlayerWeaponManager.instance.rightHandWeapons[index];
                break;
            case EquipmentSlotType.LeftHand:
                targetItem = PlayerWeaponManager.instance.leftHandWeapons[index];
                break;
            case EquipmentSlotType.Item:
                targetItem = PlayerWeaponManager.instance.equippedItems[index];
                break;
            case EquipmentSlotType.Spell:
                targetItem = PlayerWeaponManager.instance.equippedSpells[index];
                break;
            case EquipmentSlotType.Ring:
                targetItem = PlayerWeaponManager.instance.equippedRings[index];
                break;
        }

        if (targetItem != null)
        {
            // Assuming your base Item class has strings for name and description.
            // If they don't yet, you will need to add public string itemName and itemDescription to Item.cs

            // detailLargeIcon.sprite = targetItem.spr_LoadOutIcon; (or spr_Weapon)
            // detailCategoryText.text = targetItem.itemCategory.ToString();
            // detailNameLeftText.text = targetItem.name; // Can append upgrade levels later
            // detailNameRightText.text = targetItem.name;
            // detailDescriptionText.text = "Description mapped from Item data...";

            // Enable the large icon image component
            detailLargeIcon.gameObject.SetActive(true);
        }
        else
        {
            ClearDetailsPanel();
        }
    }

    private void ClearDetailsPanel()
    {
        detailLargeIcon.gameObject.SetActive(false);
        detailCategoryText.text = "";
        detailNameLeftText.text = "";
        detailNameRightText.text = "";
        detailDescriptionText.text = "";
    }

    private void HandleSlotClick(EquipmentSlotType type, int index)
    {
        // 1. Fire the event (if anything else needs to listen to it)
        onSlotClicked?.Invoke(type, index);

        // 2. Automatically transition the UI to the correct Inventory category
        OpenInventoryCategory(type, index);
    }
}
