using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// One-shot: the Brute. A big, slow Soldier variant that grabs instead of swinging (BruteGrab):
/// wind-up opening (kick to cancel + stun him) -> crushing hug (mash Jump to break free).
/// Placeholder art: the Soldier's sprites, 1.5x bigger and tinted. Spawns on the Castle floors.
/// Re-run from Tools > Enemies > Set Up Brute (existing prefab kept).
/// </summary>
public static class BruteSetup
{
    private const string AutoKey = "BruteSetup_v1";
    private const string SoldierPath = "Assets/003-Prefabs/Enemy/Enemy_Soldier (1).prefab";
    private const string BrutePath = "Assets/003-Prefabs/Enemy/Enemy_Brute.prefab";
    private const string TablePath = "Assets/Resources/Enemies/FloorEnemyTables.asset";

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

    [MenuItem("Tools/Enemies/Set Up Brute")]
    public static void Build()
    {
        var log = new StringBuilder();
        int errors = 0;
        try
        {
            GameObject brute = AssetDatabase.LoadAssetAtPath<GameObject>(BrutePath) ?? BuildPrefab(log);
            AddToCastle(brute, log);
            AssetDatabase.SaveAssets();
        }
        catch (System.Exception e)
        {
            errors++;
            log.AppendLine("!! " + e);
        }

        Directory.CreateDirectory("Library/PrisonDecor");
        File.WriteAllText("Library/PrisonDecor/brute.txt", log.ToString());
        Debug.Log($"[BruteSetup] Done with {errors} error(s). Report: Library/PrisonDecor/brute.txt");
    }

    private static GameObject BuildPrefab(StringBuilder log)
    {
        GameObject soldier = AssetDatabase.LoadAssetAtPath<GameObject>(SoldierPath);
        if (soldier == null) { throw new System.Exception("missing " + SoldierPath); }

        Scene scene = EditorSceneManager.NewPreviewScene();
        try
        {
            GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(soldier, scene);
            go.name = "Enemy_Brute";
            go.transform.localScale = Vector3.one * 1.5f; // big

            EnemyThief thief = go.GetComponent<EnemyThief>();
            Set(thief, "_maxHP", 260f, log);              // soldier 150
            Set(thief, "_currentHP", 260f, log);
            Set(thief, "moveSpeed", 1.1f, log);           // soldier 2: slow walk
            Set(thief, "damageStunDuration", 0.15f, log); // barely flinches
            SetInt(thief, "maxHitsBeforeBlock", 0, log);  // never blocks: all muscle

            SpriteRenderer body = new SerializedObject(thief).FindProperty("sprRndr")?.objectReferenceValue as SpriteRenderer;
            if (body != null) { body.color = new Color(0.9f, 0.76f, 0.68f); }
            else { log.AppendLine("!! no body sprite"); }

            BruteGrab grab = go.AddComponent<BruteGrab>();
            grab.body = body;

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, BrutePath);
            log.AppendLine("created " + BrutePath);
            return prefab;
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    private static void AddToCastle(GameObject brute, StringBuilder log)
    {
        FloorEnemyTable tables = AssetDatabase.LoadAssetAtPath<FloorEnemyTable>(TablePath);
        FloorEnemyTable.Table castle = tables != null ? tables.tables.FirstOrDefault(t => t.fromFloor == 6) : null;
        if (castle == null) { log.AppendLine("!! no Castle table in " + TablePath); return; }
        if (castle.enemies.Any(e => e.enemyPrefab == brute)) { log.AppendLine("castle already spawns the Brute"); return; }

        castle.enemies.Insert(0, new EnemySpawnData { enemyPrefab = brute, spawnChance = 20f });
        EditorUtility.SetDirty(tables);
        log.AppendLine("castle floors: Brute 20% (rolled first)");
    }

    private static void Set(Object target, string field, float value, StringBuilder log)
    {
        var so = new SerializedObject(target);
        SerializedProperty it = so.GetIterator();
        while (it.Next(true))
        {
            if (it.name == field && it.propertyType == SerializedPropertyType.Float)
            {
                it.floatValue = value;
                so.ApplyModifiedPropertiesWithoutUndo();
                return;
            }
        }
        log.AppendLine($"!! {target.name}: no float '{field}'");
    }

    private static void SetInt(Object target, string field, int value, StringBuilder log)
    {
        var so = new SerializedObject(target);
        SerializedProperty p = so.FindProperty(field);
        if (p == null || p.propertyType != SerializedPropertyType.Integer) { log.AppendLine($"!! {target.name}: no int '{field}'"); return; }
        p.intValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
