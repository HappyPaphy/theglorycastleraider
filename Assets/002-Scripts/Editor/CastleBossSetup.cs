using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// One-shot: the Castle Lord, the castle's boss (Floor 3-3).
///  - Enemy_CastleLord: Big Knight variant with more HP, a gold tint and CastleLordPhases
///    (phase 2 below 50% HP: enrage + shockwave slams you jump over).
///  - The castle boss room's Big Knight becomes the Castle Lord (same changes applied to it there),
///    so the boss-room doors and trigger keep working as before.
/// Placeholder art: the Big Knight's sprites. Re-run from Tools > Enemies > Set Up Castle Boss.
/// </summary>
public static class CastleBossSetup
{
    private const string AutoKey = "CastleBossSetup_v1";
    private const string KnightPath = "Assets/003-Prefabs/Enemy/Enemy_BigKnight (1).prefab";
    private const string LordPath = "Assets/003-Prefabs/Enemy/Enemy_CastleLord.prefab";
    private const string BossRoomPath = "Assets/012-MyDungenAsset/Room/Castle/Boss Room (1) (Castle).prefab";
    private const string RingMaterialPath = "Assets/006-Materials/Mat_Spark.mat";

    private const float LordHP = 1500f; // Big Knight 1000
    private static readonly Color LordTint = new Color(1f, 0.86f, 0.62f);

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

    [MenuItem("Tools/Enemies/Set Up Castle Boss")]
    public static void Build()
    {
        var log = new StringBuilder();
        int errors = 0;

        try
        {
            BuildPrefab(log);
            UpgradeBossRoom(log);
            AssetDatabase.SaveAssets();
        }
        catch (System.Exception e)
        {
            errors++;
            log.AppendLine("!! " + e);
        }

        Directory.CreateDirectory("Library/PrisonDecor");
        File.WriteAllText("Library/PrisonDecor/castle_boss.txt", log.ToString());
        Debug.Log($"[CastleBossSetup] Done with {errors} error(s). Report: Library/PrisonDecor/castle_boss.txt");
    }

    private static void BuildPrefab(StringBuilder log)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(LordPath) != null) { log.AppendLine("kept " + LordPath); return; }

        GameObject knight = AssetDatabase.LoadAssetAtPath<GameObject>(KnightPath);
        if (knight == null) { throw new System.Exception("missing " + KnightPath); }

        Scene scene = EditorSceneManager.NewPreviewScene();
        try
        {
            GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(knight, scene);
            go.name = "Enemy_CastleLord";
            MakeLord(go, log);
            PrefabUtility.SaveAsPrefabAsset(go, LordPath);
            log.AppendLine("created " + LordPath);
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    private static void UpgradeBossRoom(StringBuilder log)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(BossRoomPath);
        try
        {
            EnemyBigKnight knight = root.GetComponentInChildren<EnemyBigKnight>(true);
            if (knight == null) { log.AppendLine("!! no Big Knight in " + BossRoomPath); return; }
            if (knight.GetComponent<CastleLordPhases>() != null) { log.AppendLine("boss room already has the Castle Lord"); return; }

            knight.gameObject.name = "Castle Lord";
            MakeLord(knight.gameObject, log);
            PrefabUtility.SaveAsPrefabAsset(root, BossRoomPath);
            log.AppendLine("castle boss room: Big Knight -> Castle Lord");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void MakeLord(GameObject go, StringBuilder log)
    {
        EnemyBigKnight knight = go.GetComponent<EnemyBigKnight>();
        knight.CharacterHealthComponent.SetMaxHP(LordHP);
        knight.CharacterHealthComponent.SetHP(LordHP);
        EditorUtility.SetDirty(knight);
        PrefabUtility.RecordPrefabInstancePropertyModifications(knight);

        SpriteRenderer body = knight.BodySprite;
        if (body != null)
        {
            body.color = LordTint;
            EditorUtility.SetDirty(body);
            PrefabUtility.RecordPrefabInstancePropertyModifications(body);
        }
        else { log.AppendLine("!! no body sprite"); }

        CastleLordPhases phases = go.GetComponent<CastleLordPhases>() ?? go.AddComponent<CastleLordPhases>();
        phases.ringMaterial = AssetDatabase.LoadAssetAtPath<Material>(RingMaterialPath);
        if (phases.ringMaterial == null) { log.AppendLine("!! ring material missing: " + RingMaterialPath); }
        log.AppendLine($"  {go.name}: HP {LordHP}, gold tint, phase 2 at 50% (enrage x{phases.enrageSpeed}, slam every {phases.slamInterval}s)");
    }
}
