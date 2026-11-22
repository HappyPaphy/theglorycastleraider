using UnityEditor;
using UnityEngine;

/// <summary>
/// One-shot: creates Resources/Audio/BiomeAudio with the three biomes (Prison, Maze Yard, Castle) and empty
/// clip slots. Select the asset and drop your music / ambience clips in. Tools > Audio > Set Up Biome Audio.
/// </summary>
public static class BiomeAudioSetup
{
    private const string AutoKey = "BiomeAudioSetup_v1";
    private const string Path = "Assets/Resources/Audio/BiomeAudio.asset";

    [InitializeOnLoadMethod]
    private static void AutoRun()
    {
        if (EditorPrefs.GetBool(AutoKey + Application.dataPath, false)) { return; }
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { return; }
            EditorPrefs.SetBool(AutoKey + Application.dataPath, true);
            Create();
        };
    }

    [MenuItem("Tools/Audio/Set Up Biome Audio")]
    public static void Create()
    {
        BiomeAudioSettings existing = AssetDatabase.LoadAssetAtPath<BiomeAudioSettings>(Path);
        if (existing != null)
        {
            Selection.activeObject = existing;
            Debug.Log("[BiomeAudio] Already set up: " + Path);
            return;
        }

        if (!AssetDatabase.IsValidFolder("Assets/Resources/Audio")) { AssetDatabase.CreateFolder("Assets/Resources", "Audio"); }

        var settings = ScriptableObject.CreateInstance<BiomeAudioSettings>();
        settings.biomes.Add(new BiomeAudioSettings.Biome { name = "Prison (Floors 1-1 to 1-3)", fromFloor = 0, toFloor = 2 });
        settings.biomes.Add(new BiomeAudioSettings.Biome { name = "Maze Yard (Floors 2-1 to 2-3)", fromFloor = 3, toFloor = 5 });
        settings.biomes.Add(new BiomeAudioSettings.Biome { name = "Castle (Floors 3-1 to 3-3)", fromFloor = 6, toFloor = 8 });
        AssetDatabase.CreateAsset(settings, Path);
        AssetDatabase.SaveAssets();
        Selection.activeObject = settings;
        Debug.Log("[BiomeAudio] Created " + Path + ": assign a music clip to each biome.");
    }
}
