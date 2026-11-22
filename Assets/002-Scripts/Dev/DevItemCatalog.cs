using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Every item / weapon prefab in the game, for the F6 cheat panel (DevItemGiver).
/// Lives at Resources/Dev/DevItemCatalog and is rebuilt automatically by the editor (Tools > Dev > Rebuild Item Catalog).
/// </summary>
public class DevItemCatalog : ScriptableObject
{
    public const string ResourcePath = "Dev/DevItemCatalog";

    public List<Item> items = new List<Item>();
    public List<PerkCardDefinition> perks = new List<PerkCardDefinition>();

    private static DevItemCatalog cached;
    public static DevItemCatalog Load()
    {
        if (cached == null) { cached = Resources.Load<DevItemCatalog>(ResourcePath); }
        return cached;
    }
}
