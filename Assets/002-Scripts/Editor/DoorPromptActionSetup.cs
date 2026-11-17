using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// One-shot: points the button prompt on every door prefab at the "InteractDoor" action,
/// so doors show their own key (V / hold North) instead of the item Interact key.
/// Re-run from Tools > Input > Use Door Action On Door Prompts.
/// </summary>
public static class DoorPromptActionSetup
{
    private const string AutoKey = "DoorPromptActionSetup_v1";
    private const string ActionsPath = "Assets/020-Others/PlayerInputActions.inputactions";

    private static readonly string[] DoorPrefabs =
    {
        "Assets/003-Prefabs/WoodenDoor.prefab",
        "Assets/003-Prefabs/Treasure/TreasureDoor_Bronze.prefab",
        "Assets/003-Prefabs/Treasure/TreasureDoor_Silver.prefab",
        "Assets/003-Prefabs/Treasure/TreasureDoor_Gold.prefab",
    };

    [InitializeOnLoadMethod]
    private static void AutoRun()
    {
        if (EditorPrefs.GetBool(AutoKey + Application.dataPath, false)) { return; }
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { return; }
            EditorPrefs.SetBool(AutoKey + Application.dataPath, true);
            Apply();
        };
    }

    [MenuItem("Tools/Input/Use Door Action On Door Prompts")]
    public static void Apply()
    {
        var log = new StringBuilder();

        InputActionReference doorAction = AssetDatabase.LoadAllAssetsAtPath(ActionsPath)
            .OfType<InputActionReference>()
            .FirstOrDefault(r => r.action != null && r.action.name == "InteractDoor");

        if (doorAction == null)
        {
            Debug.LogError("[DoorPrompt] InteractDoor action not found in " + ActionsPath);
            return;
        }

        int changed = 0;
        foreach (string path in DoorPrefabs)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) { log.AppendLine("missing " + path); continue; }

            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                int count = 0;
                foreach (UIButtonPrompt prompt in root.GetComponentsInChildren<UIButtonPrompt>(true))
                {
                    SerializedObject so = new SerializedObject(prompt);
                    so.FindProperty("actionReference").objectReferenceValue = doorAction;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    count++;
                }

                if (count > 0)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    changed++;
                }
                log.AppendLine($"{Path.GetFileName(path)}: {count} prompt(s)");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        Directory.CreateDirectory("Library/PrisonDecor");
        File.WriteAllText("Library/PrisonDecor/door_prompt.txt", log.ToString());
        Debug.Log($"[DoorPrompt] Done: {changed} door prefab(s) updated. Report: Library/PrisonDecor/door_prompt.txt");
    }
}
