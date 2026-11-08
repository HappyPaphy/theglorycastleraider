using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Builds Assets/003-Prefabs/TutorialPopup.prefab and an example TutorialData asset.
/// Runs automatically once per project when the prefab doesn't exist, or via Tools > Tutorial.
/// </summary>
public static class TutorialPopupPrefabBuilder
{
    private const string PrefabPath = "Assets/003-Prefabs/TutorialPopup.prefab";
    private const string DataFolder = "Assets/013-Datas/Tutorials";
    private const string ExampleDataPath = DataFolder + "/Tutorial_Example.asset";

    private const string HintPrefabPath = "Assets/003-Prefabs/TutorialHint.prefab";

    private static string AutoBuildKey => "TutorialPopupPrefabBuilder.AutoBuilt." + Application.dataPath;
    private static string HintAutoBuildKey => "TutorialPopupPrefabBuilder.HintAutoBuilt." + Application.dataPath;

    [InitializeOnLoadMethod]
    private static void AutoBuildOnce()
    {
        bool needPopup = !EditorPrefs.GetBool(AutoBuildKey, false);
        bool needHint = !EditorPrefs.GetBool(HintAutoBuildKey, false);
        if (!needPopup && !needHint) { return; }

        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { return; }

            if (needPopup)
            {
                EditorPrefs.SetBool(AutoBuildKey, true);
                if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null) { BuildPrefab(); }
                CreateExampleDataIfMissing();
            }

            if (needHint)
            {
                EditorPrefs.SetBool(HintAutoBuildKey, true);
                if (AssetDatabase.LoadAssetAtPath<GameObject>(HintPrefabPath) == null) { BuildHintPrefab(); }
            }
        };
    }

    [MenuItem("Tools/Tutorial/Rebuild Tutorial Hint Prefab")]
    private static void RebuildHintMenu()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(HintPrefabPath) != null &&
            !EditorUtility.DisplayDialog("Rebuild Tutorial Hint",
                $"Overwrite {HintPrefabPath}? Any manual changes to the prefab will be lost.", "Overwrite", "Cancel"))
        {
            return;
        }

        BuildHintPrefab();
    }

    private static void BuildHintPrefab()
    {
        Color boxColor = new Color(0f, 0f, 0f, 0.65f);
        Color textColor = new Color(0.95f, 0.92f, 0.85f, 1f);

        GameObject root = new GameObject("TutorialHint", typeof(RectTransform));
        int uiLayer = Mathf.Max(0, LayerMask.NameToLayer("UI"));

        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 400; // below the tutorial panel (500)

        CanvasScaler scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        CanvasGroup group = root.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;

        TutorialHintUI hintUI = root.AddComponent<TutorialHintUI>();

        // Box auto-sizes to the text
        Image box = CreateImage("HintBox", root.transform, boxColor);
        box.raycastTarget = false;
        SetRect(box.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(400, 80), Vector2.zero);

        HorizontalLayoutGroup layout = box.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(48, 48, 18, 18);
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = box.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        TextMeshProUGUI label = CreateText("HintText", box.transform, "Press {key} to Jump", 40, FontStyles.Bold, textColor);
        label.textWrappingMode = TextWrappingModes.NoWrap;

        SerializedObject so = new SerializedObject(hintUI);
        so.FindProperty("canvas").objectReferenceValue = canvas;
        so.FindProperty("canvasGroup").objectReferenceValue = group;
        so.FindProperty("animatedRect").objectReferenceValue = box.rectTransform;
        so.FindProperty("hintText").objectReferenceValue = label;
        so.ApplyModifiedPropertiesWithoutUndo();

        SetLayerRecursive(root, uiLayer);

        PrefabUtility.SaveAsPrefabAsset(root, HintPrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();

        Debug.Log($"[TutorialPopupPrefabBuilder] Created {HintPrefabPath}");
    }

    [MenuItem("Tools/Tutorial/Rebuild Tutorial Popup Prefab")]
    private static void RebuildMenu()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null &&
            !EditorUtility.DisplayDialog("Rebuild Tutorial Popup",
                $"Overwrite {PrefabPath}? Any manual changes to the prefab will be lost.", "Overwrite", "Cancel"))
        {
            return;
        }

        BuildPrefab();
        CreateExampleDataIfMissing();
    }

    private static void BuildPrefab()
    {
        Color panelColor = new Color(0.08f, 0.07f, 0.06f, 0.96f);
        Color frameColor = new Color(0f, 0f, 0f, 0.6f);
        Color buttonColor = new Color(0.25f, 0.2f, 0.15f, 1f);
        Color textColor = new Color(0.95f, 0.92f, 0.85f, 1f);

        // ---------- Root ----------
        GameObject root = new GameObject("TutorialPopup", typeof(RectTransform));
        root.layer = Mathf.Max(0, LayerMask.NameToLayer("UI"));

        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;

        CanvasScaler scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        root.AddComponent<GraphicRaycaster>();

        CanvasGroup rootGroup = root.AddComponent<CanvasGroup>();
        rootGroup.alpha = 0f;
        rootGroup.interactable = false;
        rootGroup.blocksRaycasts = false;

        TutorialPopupUI popupUI = root.AddComponent<TutorialPopupUI>();

        // ---------- Dim background ----------
        Image dim = CreateImage("Dim", root.transform, new Color(0f, 0f, 0f, 0.75f));
        Stretch(dim.rectTransform);

        // ---------- Panel ----------
        Image panel = CreateImage("Panel", root.transform, panelColor);
        SetRect(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(1100, 860), Vector2.zero);

        GameObject content = new GameObject("Content", typeof(RectTransform));
        content.transform.SetParent(panel.transform, false);
        Stretch((RectTransform)content.transform);
        CanvasGroup contentGroup = content.AddComponent<CanvasGroup>();

        TextMeshProUGUI header = CreateText("Header", content.transform, "Tutorial Header", 52, FontStyles.Bold, textColor);
        SetRect(header.rectTransform, new Vector2(0.5f, 1f), new Vector2(1000, 80), new Vector2(0, -70));

        Image gifFrame = CreateImage("GifFrame", content.transform, frameColor);
        SetRect(gifFrame.rectTransform, new Vector2(0.5f, 1f), new Vector2(960, 440), new Vector2(0, -350));

        Image gif = CreateImage("GifImage", gifFrame.transform, Color.white);
        Stretch(gif.rectTransform, 8f);
        gif.preserveAspect = true;
        gif.raycastTarget = false;

        TextMeshProUGUI description = CreateText("Description", content.transform,
            "Tutorial description goes here.", 30, FontStyles.Normal, textColor);
        SetRect(description.rectTransform, new Vector2(0.5f, 1f), new Vector2(960, 170), new Vector2(0, -665));
        description.alignment = TextAlignmentOptions.Top;
        description.textWrappingMode = TextWrappingModes.Normal;

        // ---------- Footer ----------
        TextMeshProUGUI pageCounter = CreateText("PageCounter", panel.transform, "1 / 1", 28, FontStyles.Normal, textColor);
        SetRect(pageCounter.rectTransform, new Vector2(0.5f, 0f), new Vector2(200, 50), new Vector2(0, 55));

        Button previous = CreateButton("PreviousButton", panel.transform, "Back", buttonColor, textColor, out _);
        SetRect((RectTransform)previous.transform, new Vector2(0f, 0f), new Vector2(220, 64), new Vector2(160, 55));

        Button next = CreateButton("NextButton", panel.transform, "Next", buttonColor, textColor, out TextMeshProUGUI nextLabel);
        SetRect((RectTransform)next.transform, new Vector2(1f, 0f), new Vector2(220, 64), new Vector2(-160, 55));

        // ---------- Wire up serialized fields ----------
        SerializedObject so = new SerializedObject(popupUI);
        so.FindProperty("canvasGroup").objectReferenceValue = rootGroup;
        so.FindProperty("contentGroup").objectReferenceValue = contentGroup;
        so.FindProperty("gifImage").objectReferenceValue = gif;
        so.FindProperty("headerText").objectReferenceValue = header;
        so.FindProperty("descriptionText").objectReferenceValue = description;
        so.FindProperty("pageCounterText").objectReferenceValue = pageCounter;
        so.FindProperty("previousButton").objectReferenceValue = previous;
        so.FindProperty("nextButton").objectReferenceValue = next;
        so.FindProperty("nextButtonLabel").objectReferenceValue = nextLabel;
        so.ApplyModifiedPropertiesWithoutUndo();

        SetLayerRecursive(root, root.layer);

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();

        Debug.Log($"[TutorialPopupPrefabBuilder] Created {PrefabPath}");
        EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
    }

    private static void CreateExampleDataIfMissing()
    {
        if (AssetDatabase.LoadAssetAtPath<TutorialData>(ExampleDataPath) != null) { return; }

        if (!AssetDatabase.IsValidFolder(DataFolder))
        {
            AssetDatabase.CreateFolder(Path.GetDirectoryName(DataFolder).Replace('\\', '/'), Path.GetFileName(DataFolder));
        }

        TutorialData data = ScriptableObject.CreateInstance<TutorialData>();
        AssetDatabase.CreateAsset(data, ExampleDataPath);

        SerializedObject so = new SerializedObject(data);
        SerializedProperty pages = so.FindProperty("pages");
        pages.arraySize = 2;

        SerializedProperty page1 = pages.GetArrayElementAtIndex(0);
        page1.FindPropertyRelative("header").stringValue = "Movement";
        page1.FindPropertyRelative("description").stringValue = "Use the movement keys or the left stick to move around.";
        page1.FindPropertyRelative("framesPerSecond").floatValue = 12f;

        SerializedProperty page2 = pages.GetArrayElementAtIndex(1);
        page2.FindPropertyRelative("header").stringValue = "Jump";
        page2.FindPropertyRelative("description").stringValue = "Press Jump to leap over gaps and obstacles.";
        page2.FindPropertyRelative("framesPerSecond").floatValue = 12f;

        so.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets();

        Debug.Log($"[TutorialPopupPrefabBuilder] Created example data {ExampleDataPath}");
    }

    // =========================================================
    // HELPERS
    // =========================================================

    private static Image CreateImage(string name, Transform parent, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.color = color;
        return image;
    }

    private static TextMeshProUGUI CreateText(string name, Transform parent, string text, float size, FontStyles style, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.fontStyle = style;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        return tmp;
    }

    private static Button CreateButton(string name, Transform parent, string label, Color bg, Color textColor, out TextMeshProUGUI labelText)
    {
        Image image = CreateImage(name, parent, bg);
        Button button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;

        // Keyboard/gamepad go through InputSchemeManager (Submit/Cancel), so the EventSystem never navigates here
        button.navigation = new Navigation { mode = Navigation.Mode.None };

        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(1.25f, 1.25f, 1.25f, 1f);
        colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.4f);
        button.colors = colors;

        labelText = CreateText("Label", image.transform, label, 30, FontStyles.Bold, textColor);
        Stretch(labelText.rectTransform);
        return button;
    }

    private static void SetRect(RectTransform rt, Vector2 anchor, Vector2 size, Vector2 anchoredPosition)
    {
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = anchoredPosition;
    }

    private static void Stretch(RectTransform rt, float padding = 0f)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(padding, padding);
        rt.offsetMax = new Vector2(-padding, -padding);
    }

    private static void SetLayerRecursive(GameObject go, int layer)
    {
        if (layer < 0) { return; }
        go.layer = layer;
        foreach (Transform child in go.transform) { SetLayerRecursive(child.gameObject, layer); }
    }
}
