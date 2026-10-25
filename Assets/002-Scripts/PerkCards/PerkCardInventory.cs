using System;
using UnityEngine;

[Serializable]
public class RuntimePerkCard
{
    public PerkCardDefinition definition;
    public int level;
}

public class PerkCardInventory : MonoBehaviour
{
    public const int MaxSlots = 9;

    [SerializeField]
    private RuntimePerkCard[] slots = new RuntimePerkCard[MaxSlots];

    [SerializeField]
    private bool[] slotUnlocked = new bool[MaxSlots]
    {
        true, true, true,
        true, false, false,
        false, false, false
    };

    public int SlotCount => MaxSlots;

    [SerializeField] private PerkSelectionUI ui;

    private void OnValidate()
    {
        if (slots == null || slots.Length != MaxSlots)
            Array.Resize(ref slots, MaxSlots);

        if (slotUnlocked == null || slotUnlocked.Length != MaxSlots)
            Array.Resize(ref slotUnlocked, MaxSlots);
    }

    public RuntimePerkCard GetSlot(int index)
    {
        if (!IsValidIndex(index))
            return null;

        return slots[index];
    }

    public bool IsSlotUnlocked(int index)
    {
        if (!IsValidIndex(index))
            return false;

        return slotUnlocked[index];
    }

    public bool IsSlotOccupied(int index)
    {
        if (!IsValidIndex(index))
            return false;

        return slots[index] != null;
    }

    public bool HasFreeUnlockedSlot()
    {
        for (int i = 0; i < MaxSlots; i++)
        {
            if (slotUnlocked[i] && slots[i].definition == null)
            {
                return true;
            }
        }

        return false;
    }

    public bool TryAdd(
        PerkCardDefinition definition,
        int level,
        out int slotIndex)
    {
        slotIndex = -1;

        for (int i = 0; i < MaxSlots; i++)
        {
            if (!slotUnlocked[i])
                continue;

            if (slots[i].definition != null)
                continue;

            slots[i].definition = definition;
            slots[i].level = level;

            slotIndex = i;
            ui.CurrentAddingPerkSlotIndex = slotIndex;
            return true;
        }

        return false;
    }

    public bool Replace(
        int slotIndex,
        PerkCardDefinition definition,
        int level)
    {
        if (!IsValidIndex(slotIndex))
            return false;

        if (!slotUnlocked[slotIndex])
            return false;

        if (slots[slotIndex] == null)
            return false;

        slots[slotIndex].definition = definition;
        slots[slotIndex].level = level;

        return true;
    }

    public void SetSlotUnlocked(
        int slotIndex,
        bool unlocked)
    {
        if (!IsValidIndex(slotIndex))
            return;

        slotUnlocked[slotIndex] = unlocked;
    }

    private bool IsValidIndex(int index)
    {
        return index >= 0 && index < MaxSlots;
    }
}