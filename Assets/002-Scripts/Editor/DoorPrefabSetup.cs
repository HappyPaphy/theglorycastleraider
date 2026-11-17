using UnityEditor;
using UnityEngine;

/// <summary>
/// Adds DoorInteractable + an Interact button prompt to the WoodenDoor prefab, so every door
/// placed in the DunGen rooms can be opened/closed. Runs once automatically, or via the menu.
/// </summary>
public static class DoorPrefabSetup
{
    private const string DoorPrefabPath = "Assets/003-Prefabs/WoodenDoor.prefab";
    private const string PromptSourcePrefabPath = "Assets/003-Prefabs/Items/Consumables/HealthPotion_Small.prefab";

    private static string AutoKey => "DoorPrefabSetup.Done.v1." + Application.dataPath;
    private static string FragmentDelayKey => "DoorPrefabSetup.FragmentDelay.v1." + Application.dataPath;

    // Door pieces fly clear of the door frame before they start colliding with the scene
    private const float DoorFragmentCollisionDelay = 0.2f;

    [InitializeOnLoadMethod]
    private static void AutoRunOnce()
    {
        bool needSetup = !EditorPrefs.GetBool(AutoKey, false);
        bool needDelay = !EditorPrefs.GetBool(FragmentDelayKey, false);
        if (!needSetup && !needDelay) { return; }

        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { return; }

            if (needSetup)
            {
                EditorPrefs.SetBool(AutoKey, true);
                SetUpDoor();
            }

            if (needDelay)
            {
                EditorPrefs.SetBool(FragmentDelayKey, true);
                SetDoorFragmentDelay();
            }
        };
    }

    [MenuItem("Tools/Doors/Set Door Fragment Collision Delay")]
    public static void SetDoorFragmentDelay()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(DoorPrefabPath);
        try
        {
            Fracture fracture = root.GetComponent<Fracture>();
            if (fracture == null)
            {
                Debug.LogWarning("[DoorPrefabSetup] WoodenDoor has no Fracture component.");
                return;
            }

            fracture.fragmentSceneCollisionDelay = DoorFragmentCollisionDelay;
            PrefabUtility.SaveAsPrefabAsset(root, DoorPrefabPath);
            Debug.Log($"[DoorPrefabSetup] WoodenDoor pieces now ignore the scene for {DoorFragmentCollisionDelay}s after breaking.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    [MenuItem("Tools/Doors/Set Up Interactable WoodenDoor")]
    public static void SetUpDoor()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(DoorPrefabPath);
        try
        {
            DoorInteractable door = root.GetComponent<DoorInteractable>();
            if (door == null) { door = root.AddComponent<DoorInteractable>(); }

            SerializedObject so = new SerializedObject(door);

            // Interact prompt (same billboard prompt the item pickups use)
            Transform existing = root.transform.Find("UIButtonPrompt");
            GameObject prompt = existing != null ? existing.gameObject : CopyPrompt(root.transform, out int playerMask);
            if (prompt != null)
            {
                so.FindProperty("uiButtonPrompt").objectReferenceValue = prompt;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, DoorPrefabPath);
            Debug.Log($"[DoorPrefabSetup] {DoorPrefabPath} is now interactable (open/close with Interact).");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static GameObject CopyPrompt(Transform parent, out int playerLayerMask)
    {
        playerLayerMask = 1 << 6;

        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(PromptSourcePrefabPath);
        if (source == null) { return null; }

        Transform sourcePrompt = FindChildRecursive(source.transform, "UIButtonPrompt");
        if (sourcePrompt == null) { return null; }

        GameObject prompt = Object.Instantiate(sourcePrompt.gameObject, parent, false);
        prompt.name = "UIButtonPrompt";
        prompt.transform.localPosition = new Vector3(0f, 1.15f, 0.26f);
        prompt.transform.localRotation = Quaternion.identity;
        prompt.SetActive(true);

        // The prompt is purely visual: make sure it can't block hits or be fractured
        foreach (Collider c in prompt.GetComponentsInChildren<Collider>(true)) { Object.DestroyImmediate(c); }

        return prompt;
    }

    private static Transform FindChildRecursive(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name) { return child; }
            Transform found = FindChildRecursive(child, name);
            if (found != null) { return found; }
        }
        return null;
    }
}
