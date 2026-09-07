using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class InventorySlotUI : MonoBehaviour
{
    [Header("UI Components")]
    public Image iconImage;
    public TextMeshProUGUI quantityText;
    public GameObject equippedMarker; // Optional: A little 'E' icon or glowing border to show it's currently equipped

    [HideInInspector] public Item slottedItem;

    /// <summary>
    /// Configures the slot's visuals based on the item data
    /// </summary>
    public void SetupSlot(Item item, bool isEquipped, int defaultQuantity = 1)
    {
        slottedItem = item;

        if (item != null)
        {
            iconImage.sprite = item.spr_Icon;
            iconImage.SetNativeSize();
            iconImage.gameObject.SetActive(true);

            int actualQuantity = defaultQuantity;
            if (item.itemCategory == ItemCategory.Consumable && InventoryManager.instance.consumables.ContainsKey(item.itemType))
            {
                actualQuantity = InventoryManager.instance.consumables[item.itemType];
            }

            if (item.itemCategory == ItemCategory.Consumable && actualQuantity > 0)
            {
                quantityText.text = actualQuantity.ToString();
                quantityText.gameObject.SetActive(true);
            }
            else
            {
                if (quantityText != null) quantityText.gameObject.SetActive(false);
            }

            if (equippedMarker != null) equippedMarker.SetActive(isEquipped);
        }
    }

    // ==========================================
    // MAP THESE IN THE INSPECTOR (UIFeedback events)
    // ==========================================

    // Map to your UIFeedback -> On Hover
    public void OnSlotHovered()
    {
        if (slottedItem != null)
        {
            EquipmentLoadOut.instance.PreviewInventoryItem(slottedItem);
        }
    }

    // Map to your UIFeedback -> On Click
    public void OnSlotClicked()
    {
        if (slottedItem != null)
        {
            EquipmentLoadOut.instance.EquipSelectedItem(slottedItem);
        }
    }
}
