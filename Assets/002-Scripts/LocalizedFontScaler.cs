using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.Localization.Settings;

[System.Serializable]
public class FontScalePerLanguage
{
    public string LanguageCode;
    public float SizeFactor;
    public float SpacingLine;
    public float SpacingCharacter;

    public FontScalePerLanguage(string languageCode, float sizeFactor, float spacingLine, float spacingCharacter)
    {
        LanguageCode = languageCode;
        SizeFactor = sizeFactor;
        SpacingLine = spacingLine;
        SpacingCharacter = spacingCharacter;
    }
}

[RequireComponent(typeof(TMP_Text))]
public class LocalizedFontScaler : MonoBehaviour
{
    [SerializeField] private float defaultFontSize_Max = 135f;
    [SerializeField] private float defaultFontSize_Min = 20f;
    [SerializeField] private float defaultLineSpacing = 0f;
    [SerializeField] private string defaultLanguageCode = "en";

    [SerializeField] private bool isAutoSizeApplyToCurrentSize = true;
    [SerializeField] private bool isAutoSizeApplyForDialogue = false;

    [Tooltip("Customize min font scale per language (min size = defaultFontSize * factor)")]
    public List<FontScalePerLanguage> languageScales = new List<FontScalePerLanguage>
    { 
        new ("en",1f, 0f, 0f),
        new ("th",1f, -100f, 5f)
    };

    private TMP_Text tmpText;

    void Awake()
    {
        tmpText = GetComponent<TMP_Text>();
        tmpText.enableAutoSizing = true;

        if (isAutoSizeApplyToCurrentSize)
        {
            defaultFontSize_Max = tmpText.fontSize;
            defaultFontSize_Min = tmpText.fontSize;
        }
        else if(isAutoSizeApplyForDialogue)
        {
            defaultFontSize_Max = tmpText.fontSize;
            defaultFontSize_Min = defaultFontSize_Max / 2;
        }

            ApplyFontSize(LocalizationSettings.SelectedLocale.Identifier.Code);

        LocalizationSettings.SelectedLocaleChanged += locale =>
        {
            ApplyFontSize(locale.Identifier.Code);
        };

        
    }

    void ApplyFontSize(string langCode)
    {
        float scaleFactor = GetScaleFactor(langCode);

        tmpText.fontSizeMax = defaultFontSize_Max * scaleFactor;
        tmpText.fontSizeMin = defaultFontSize_Min * scaleFactor;

        tmpText.lineSpacing = GetLineSpacingFactor(langCode);
        tmpText.characterSpacing = GetCharacterSpacingFactor(langCode);

#if UNITY_EDITOR
        //Debug.Log($"[LocalizedFontScaler] Lang: {langCode}, MinSize: {tmpText.fontSizeMin}, MaxSize: {tmpText.fontSizeMax}");
#endif
    }

    float GetScaleFactor(string langCode)
    {
        foreach (var entry in languageScales)
        {
            if (entry.LanguageCode == langCode)
                return entry.SizeFactor;
        }

        return 1f;
    }

    float GetLineSpacingFactor(string langCode)
    {
        foreach (var entry in languageScales)
        {
            if (entry.LanguageCode == langCode)
                return entry.SpacingLine;
        }

        return 0f;
    }

    float GetCharacterSpacingFactor(string langCode)
    {
        foreach (var entry in languageScales)
        {
            if (entry.LanguageCode == langCode)
                return entry.SpacingCharacter;
        }

        return 0f;
    }
}
