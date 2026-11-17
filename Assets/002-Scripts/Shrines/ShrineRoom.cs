using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gives each altar in the Shrine Room a different shrine type from the pool (weighted, respecting each
/// shrine's earliest floor). Altars that already have a shrine assigned by hand keep it.
/// </summary>
public class ShrineRoom : MonoBehaviour
{
    [SerializeField] private List<ShrineAltar> altars = new List<ShrineAltar>();

    private void Start()
    {
        ShrineSettings settings = ShrineSettings.Load();
        if (settings == null) { return; }

        int floor = RoguelikeManager.instance != null ? RoguelikeManager.instance.dungeonIndex + 1 : 1;

        var used = new List<ShrineDefinition>();
        foreach (ShrineAltar altar in altars)
        {
            if (altar != null && altar.Definition != null) { used.Add(altar.Definition); }
        }

        foreach (ShrineAltar altar in altars)
        {
            if (altar == null || altar.Definition != null) { continue; }

            ShrineDefinition pick = Pick(settings.shrinePool, used, floor);
            if (pick == null) { pick = Pick(settings.shrinePool, null, floor); } // more altars than shrine types
            if (pick == null) { altar.gameObject.SetActive(false); continue; }

            used.Add(pick);
            altar.SetDefinition(pick);
        }
    }

    private static ShrineDefinition Pick(List<ShrineDefinition> pool, List<ShrineDefinition> exclude, int floor)
    {
        float total = 0f;
        var options = new List<ShrineDefinition>();
        foreach (ShrineDefinition s in pool)
        {
            if (s == null || s.weight <= 0f || floor < s.minFloor) { continue; }
            if (exclude != null && exclude.Contains(s)) { continue; }
            options.Add(s);
            total += s.weight;
        }
        if (options.Count == 0) { return null; }

        float roll = Random.value * total;
        foreach (ShrineDefinition s in options)
        {
            roll -= s.weight;
            if (roll <= 0f) { return s; }
        }
        return options[options.Count - 1];
    }
}
