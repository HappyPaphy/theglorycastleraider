using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// One-shot: builds Resources/Pickups/CoinPickup.prefab (billboard coin sprite + CoinPickup) and sets
/// per-enemy coin drop amounts. Re-run from Tools > Shop > Build Coin Drops.
/// Swap the coin's sprite on the prefab's "Visual" child whenever you like.
/// </summary>
public static class CoinDropSetup
{
    private const string AutoKey = "CoinDropSetup_v1";
    private const string PrefabPath = "Assets/Resources/Pickups/CoinPickup.prefab";
    private const string CoinSpritePath = "Assets/013-Datas/Shop/Icons/Icon_Coin.png";
    private const int ItemLayer = 14;

    // prefab, min, max, max pieces
    private static readonly (string path, int min, int max, int pieces)[] EnemyCoins =
    {
        ("Assets/003-Prefabs/Enemy/Enemy_Soldier (1).prefab", 4, 8, 4),
        ("Assets/003-Prefabs/Enemy/Enemy_Thief (1).prefab", 5, 9, 4),
        ("Assets/003-Prefabs/Enemy/Enemy_Archer (1).prefab", 4, 8, 4),
        ("Assets/003-Prefabs/Enemy/Enemy_Stray Dog (1).prefab", 2, 5, 3),
        ("Assets/003-Prefabs/Enemy/Enemy_BigKnight (1).prefab", 25, 40, 8),
    };

    [InitializeOnLoadMethod]
    private static void AutoRun()
    {
        if (EditorPrefs.GetBool(AutoKey + Application.dataPath, false)) { return; }
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { return; }
            EditorPrefs.SetBool(AutoKey + Application.dataPath, true);
            Build();
        };
    }

    [MenuItem("Tools/Shop/Build Coin Drops")]
    public static void Build()
    {
        var log = new StringBuilder();
        int errors = 0;

        try { BuildPrefab(log); }
        catch (System.Exception e) { errors++; log.AppendLine("!! prefab: " + e); }

        foreach (var (path, min, max, pieces) in EnemyCoins)
        {
            try { SetEnemyCoins(path, min, max, pieces, log); }
            catch (System.Exception e) { errors++; log.AppendLine("!! " + path + ": " + e); }
        }

        AssetDatabase.SaveAssets();
        Directory.CreateDirectory("Library/PrisonDecor");
        File.WriteAllText("Library/PrisonDecor/coin_drops.txt", log.ToString());
        Debug.Log($"[CoinDrops] Done with {errors} error(s). Report: Library/PrisonDecor/coin_drops.txt");
    }

    private static void BuildPrefab(StringBuilder log)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));

        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(CoinSpritePath);
        if (sprite == null) { log.AppendLine("coin sprite missing at " + CoinSpritePath + " (assign one on the prefab)"); }

        GameObject root = new GameObject("CoinPickup");
        try
        {
            root.layer = ItemLayer;

            GameObject visual = new GameObject("Visual");
            visual.layer = ItemLayer;
            visual.transform.SetParent(root.transform, false);
            // The coin icon is 32 px at 32 px/unit (1 m): scale it to a ~0.3 m coin
            visual.transform.localScale = Vector3.one * 0.3f;

            SpriteRenderer sr = visual.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            CoinPickup coin = root.AddComponent<CoinPickup>();
            SerializedObject so = new SerializedObject(coin);
            so.FindProperty("visual").objectReferenceValue = visual.transform;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            log.AppendLine("saved " + PrefabPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static void SetEnemyCoins(string path, int min, int max, int pieces, StringBuilder log)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) { log.AppendLine("missing " + path); return; }

        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            EnemyEntity enemy = root.GetComponentInChildren<EnemyEntity>(true);
            if (enemy == null) { log.AppendLine("no EnemyEntity in " + path); return; }

            SerializedObject so = new SerializedObject(enemy);
            so.FindProperty("coinDropMin").intValue = min;
            so.FindProperty("coinDropMax").intValue = max;
            so.FindProperty("coinDropMaxPieces").intValue = pieces;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, path);
            log.AppendLine($"{Path.GetFileNameWithoutExtension(path)}: {min}-{max} coins, up to {pieces} pickups");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
