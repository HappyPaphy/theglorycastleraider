using TMPro;
using UnityEngine;
using System;
using System.Linq;
using System.Collections.Generic;
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

    private GameObject firstSelected_Inventory_Weapons;
    private GameObject firstSelected_Inventory_Items;
    private GameObject firstSelected_Inventory_Rings;
    private GameObject firstSelected_Inventory_Spells;

    [HideInInspector] public bool isPanelActive = false;

    [Header("Inventory Scroll Views & Text")]
    public TextMeshProUGUI text_CategoryName; // Maps to "Text (Category Name)"
    public GameObject scrollView_Weapons;
    public GameObject scrollView_Items;
    public GameObject scrollView_Rings;
    public GameObject scrollView_Spells;

    [Header("Scroll View Content Holders")]
    [Tooltip("The 'Content' transform inside your Scroll Views where slots are spawned")]
    public Transform content_Weapons;
    public Transform content_Items;
    public Transform content_Rings;
    public Transform content_Spells;

    [Header("Prefabs")]
    public GameObject inventorySlotPrefab; // Assign the prefab with InventorySlotUI.cs here

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
    public Image detailLargeIcon_Loadout;
    public TextMeshProUGUI detailCategoryText_Loadout;    // Maps to "Equipment Category"
    public TextMeshProUGUI detailNameLeftText_Loadout;    // Maps to "Equipment Name +1"
    public TextMeshProUGUI detailNameRightText_Loadout;   // Maps to "Equipment Name" (above description)
    public TextMeshProUGUI detailDescriptionText_Loadout; // Maps to the long description text

    public Image detailLargeIcon_Inventory;
    public TextMeshProUGUI detailNameRightText_Inventory;   // Maps to "Equipment Name" (above description)
    public TextMeshProUGUI detailDescriptionText_Inventory; // Maps to the long description text

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
        if(PlayerController.instance.CharacterHealthComponent.CurrentHP <= 0f || PauseGame.instance.IsPaused)
        {
            return;
        }

        if (PlayerController.instance.IsToggleLoadoutPressed)
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
            rightHandSlots[i].SetNativeSize();
        }

        // 2. Left Hand Weapons
        for (int i = 0; i < leftHandSlots.Length; i++)
        {
            Weapon w = PlayerWeaponManager.instance.leftHandWeapons[i];
            leftHandSlots[i].sprite = (w != null && w.spr_Weapon != null) ? w.spr_Weapon : emptyWeaponSprite;
            leftHandSlots[i].SetNativeSize();
        }

        // 3. Items & Quantities
        for (int i = 0; i < itemSlots.Length; i++)
        {
            Item item = PlayerWeaponManager.instance.equippedItems[i];
            if (item != null && item.spr_Icon_Frame != null)
            {
                itemSlots[i].sprite = item.spr_Icon_Frame;
                itemSlots[i].SetNativeSize();

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
                itemSlots[i].SetNativeSize();
                itemQuantityTexts[i].gameObject.SetActive(false);
            }
        }

        // 4. Unified Spells
        for (int i = 0; i < spellSlots.Length; i++)
        {
            Item spell = PlayerWeaponManager.instance.equippedSpells[i];
            spellSlots[i].sprite = (spell != null && spell.spr_Icon_Frame != null) ? spell.spr_Icon_Frame : emptySpellSprite;
            spellSlots[i].SetNativeSize();
        }

        // 5. Rings
        for (int i = 0; i < ringSlots.Length; i++)
        {
            Item ring = PlayerWeaponManager.instance.equippedRings[i];
            ringSlots[i].sprite = (ring != null && ring.spr_Icon_Frame != null) ? ring.spr_Icon_Frame : emptyRingSprite;
            ringSlots[i].SetNativeSize();
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
                PopulateGrid(content_Weapons, InventoryManager.instance.weapons.Cast<Item>().ToList(), type);
                break;
            case EquipmentSlotType.Item:
                text_CategoryName.text = "ITEMS";
                scrollView_Items.SetActive(true);
                PopulateGrid(content_Items, InventoryManager.instance.consumablesList, type);
                break;
            case EquipmentSlotType.Ring:
                text_CategoryName.text = "RINGS";
                scrollView_Rings.SetActive(true);
                PopulateGrid(content_Rings, InventoryManager.instance.rings, type);
                break;
            case EquipmentSlotType.Spell:
                text_CategoryName.text = "SPELLS";
                scrollView_Spells.SetActive(true);
                List<Item> allSpells = InventoryManager.instance.magics.Concat(InventoryManager.instance.pyromancies).ToList();
                PopulateGrid(content_Spells, allSpells, type);
                break;
        }
    }

    private void PopulateGrid(Transform contentParent, List<Item> itemsToDisplay, EquipmentSlotType slotType)
    {
        // 1. Clear existing slots
        foreach (Transform child in contentParent)
        {
            Destroy(child.gameObject);
        }

        GameObject firstSlot = null;

        // 2. Instantiate a slot for every item
        foreach (Item item in itemsToDisplay)
        {
            GameObject slotObj = Instantiate(inventorySlotPrefab, contentProviderCheck(contentParent));
            InventorySlotUI slotUI = slotObj.GetComponent<InventorySlotUI>();

            bool isEquipped = CheckIfEquipped(item, slotType);
            slotUI.SetupSlot(item, isEquipped, 1);

            if (firstSlot == null) firstSlot = slotObj;
        }

        // CRITICAL FIX: Force Unity to calculate grid positions immediately 
        // so automatic navigation detects adjacent neighbors
        Canvas.ForceUpdateCanvases();
        if (contentParent is RectTransform rectTransform)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
        }

        // 3. Assign to the respective category reference and select it if available
        if (firstSlot != null)
        {
            switch (slotType)
            {
                case EquipmentSlotType.RightHand:
                case EquipmentSlotType.LeftHand:
                    firstSelected_Inventory_Weapons = firstSlot;
                    break;
                case EquipmentSlotType.Item:
                    firstSelected_Inventory_Items = firstSlot;
                    break;
                case EquipmentSlotType.Ring:
                    firstSelected_Inventory_Rings = firstSlot;
                    break;
                case EquipmentSlotType.Spell:
                    firstSelected_Inventory_Spells = firstSlot;
                    break;
            }

            EventSystem.current.SetSelectedGameObject(firstSlot);
            firstSlot.GetComponent<InventorySlotUI>().OnSlotHovered();
        }
        else
        {
            switch (slotType)
            {
                case EquipmentSlotType.RightHand:
                case EquipmentSlotType.LeftHand:
                    firstSelected_Inventory_Weapons = null;
                    break;
                case EquipmentSlotType.Item:
                    firstSelected_Inventory_Items = null;
                    break;
                case EquipmentSlotType.Ring:
                    firstSelected_Inventory_Rings = null;
                    break;
                case EquipmentSlotType.Spell:
                    firstSelected_Inventory_Spells = null;
                    break;
            }
            ClearDetailsPanel();
        }
    }

    private Transform contentProviderCheck(Transform t) => t; // Helper wrapper if needed

    private bool CheckIfEquipped(Item item, EquipmentSlotType slotType)
    {
        if (PlayerWeaponManager.instance == null) return false;

        switch (slotType)
        {
            case EquipmentSlotType.RightHand:
            case EquipmentSlotType.LeftHand:
                return PlayerWeaponManager.instance.rightHandWeapons.Contains(item as Weapon) ||
                       PlayerWeaponManager.instance.leftHandWeapons.Contains(item as Weapon);
            case EquipmentSlotType.Ring:
                return PlayerWeaponManager.instance.equippedRings.Contains(item);
            case EquipmentSlotType.Spell:
                return PlayerWeaponManager.instance.equippedSpells.Contains(item);
            case EquipmentSlotType.Item:
                return PlayerWeaponManager.instance.equippedItems.Contains(item);
            default:
                return false;
        }
    }

    public void PreviewInventoryItem(Item item)
    {
        if (item != null)
        {
            detailLargeIcon_Inventory.gameObject.SetActive(true);

            detailLargeIcon_Inventory.sprite = item.spr_Icon_Frame;
            detailLargeIcon_Inventory.SetNativeSize();
            detailNameRightText_Inventory.text = item.equipmentName;
            detailDescriptionText_Inventory.text = "Description mapped from Item data...";
        }
        else
        {
            ClearDetailsPanel();
        }
    }

    public void EquipSelectedItem(Item itemToEquip)
    {
        // Route through PlayerWeaponManager to handle duplicate checks and un-equipping
        PlayerWeaponManager.instance.EquipItem(itemToEquip, pendingSlotType, pendingSlotIndex);

        CloseInventoryCategory();
        RefreshUI();
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
            detailLargeIcon_Loadout.gameObject.SetActive(true);

            detailLargeIcon_Loadout.sprite = targetItem.spr_Icon_Frame;
            detailLargeIcon_Loadout.SetNativeSize();
            detailCategoryText_Loadout.text = targetItem.itemCategory.ToString();
            detailNameLeftText_Loadout.text = targetItem.equipmentName; // Can append upgrade levels later
            detailNameRightText_Loadout.text = targetItem.equipmentName;
            detailDescriptionText_Loadout.text = "Description mapped from Item data...";
        }
        else
        {
            ClearDetailsPanel();
        }
    }


    private void ClearDetailsPanel()
    {
        detailLargeIcon_Loadout.gameObject.SetActive(false);
        detailCategoryText_Loadout.text = "";
        detailNameLeftText_Loadout.text = "";
        detailNameRightText_Loadout.text = "";
        detailDescriptionText_Loadout.text = "";

        detailLargeIcon_Inventory.gameObject.SetActive(false);
        detailNameRightText_Inventory.text = "";
        detailDescriptionText_Inventory.text = "";
    }

    private void HandleSlotClick(EquipmentSlotType type, int index)
    {
        // 1. Fire the event (if anything else needs to listen to it)
        onSlotClicked?.Invoke(type, index);

        // 2. Automatically transition the UI to the correct Inventory category
        OpenInventoryCategory(type, index);
    }
}
