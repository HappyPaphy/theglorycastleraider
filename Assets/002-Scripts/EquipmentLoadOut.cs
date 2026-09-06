using TMPro;
using UnityEngine;
using UnityEngine.Events;
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
    public static EquipmentLoadOut instance;

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
    public TMP_Text detailCategoryText;    // Maps to "Equipment Category"
    public TMP_Text detailNameLeftText;    // Maps to "Equipment Name +1"
    public TMP_Text detailNameRightText;   // Maps to "Equipment Name" (above description)
    public TMP_Text detailDescriptionText; // Maps to the long description text

    [Header("Events")]
    [Tooltip("Fires when a slot is clicked. Use this to open your Inventory Selection menu.")]
    public UnityEvent<EquipmentSlotType, int> onSlotClicked;

    private void Awake()
    {
        if (instance == null) instance = this;
    }

    private void OnEnable()
    {
        // Automatically refresh the UI whenever the loadout menu is opened
        RefreshUI();
        ClearDetailsPanel();
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

    // ==========================================
    // UI BUTTON CLICK METHODS
    // ==========================================
    // Link these methods in the Unity Inspector to the OnClick() event of the specific slot buttons.

    public void OnRightHandSlotClicked(int index) => HandleSlotInteraction(EquipmentSlotType.RightHand, index);
    public void OnLeftHandSlotClicked(int index) => HandleSlotInteraction(EquipmentSlotType.LeftHand, index);
    public void OnItemSlotClicked(int index) => HandleSlotInteraction(EquipmentSlotType.Item, index);
    public void OnSpellSlotClicked(int index) => HandleSlotInteraction(EquipmentSlotType.Spell, index);
    public void OnRingSlotClicked(int index) => HandleSlotInteraction(EquipmentSlotType.Ring, index);

    // ==========================================
    // ASSIGNMENT METHODS (DATA SAVING)
    // ==========================================
    // Call these from your Inventory UI when an item is chosen for a specific slot.

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

    private void HandleSlotInteraction(EquipmentSlotType type, int index)
    {
        PreviewItemDetails(type, index);
        onSlotClicked?.Invoke(type, index);
    }
}
