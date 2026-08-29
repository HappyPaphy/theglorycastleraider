using UnityEngine;
using TMPro;
using System.Collections.Generic;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Components;
using System.Collections;
using DG.Tweening;
using UnityEngine.SceneManagement;

public class LocalizedFontChangerManager : MonoBehaviour
{
    public LocalizedAsset<TMP_FontAsset> localizedFont;
    public List<TextMeshProUGUI> allTextMeshes = new List<TextMeshProUGUI>();
    [SerializeField] private CanvasGroup blackFadeUI;

    private TMP_FontAsset currentFontAsset;

    public static LocalizedFontChangerManager instance;

    private void Awake()
    {
        if(instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(instance);
        }
    }

    private void Start()
    {
        //allTextMeshes.AddRange(FindObjectsOfType<TextMeshProUGUI>());
        localizedFont.AssetChanged += UpdateAllFonts;
    }

    private void Update()
    {
        allTextMeshes.RemoveAll(text => text == null);
    }

    private void OnDestroy()
    {
        localizedFont.AssetChanged -= UpdateAllFonts;
    }

    public void AddTextMeshToList(TextMeshProUGUI text)
    {
        allTextMeshes.Add(text);

        if (currentFontAsset != null)
        {
            if (text.gameObject.activeSelf)
            {
                text.font = currentFontAsset;
            }
            else
            {
                text.gameObject.SetActive(true);
                text.font = currentFontAsset;
                text.gameObject.SetActive(false);
            }
        }
    }

    public void UpdateAllFonts(TMP_FontAsset fontAsset)
    {
        if (fontAsset == null) return;

        currentFontAsset = fontAsset;

        foreach (var textMesh in allTextMeshes)
        {
            if(textMesh.gameObject.activeSelf)
            {
                textMesh.font = fontAsset;
            }
            else
            {
                textMesh.gameObject.SetActive(true);
                textMesh.font = fontAsset;
                textMesh.gameObject.SetActive(false);
            }
            
        }
    }

    private IEnumerator SetLocale(string languageCode)
    {
        // Wait until Localization system is ready
        blackFadeUI.DOFade(1f, 0.5f);
        yield return LocalizationSettings.InitializationOperation;

        foreach (var locale in LocalizationSettings.AvailableLocales.Locales)
        {
            if (locale.Identifier.Code == languageCode)
            {
                LocalizationSettings.SelectedLocale = locale;
                AsyncLoaderManager.instance.LoadLevel(SceneManager.GetActiveScene().buildIndex, false);
                yield break;
            }
        }

        Debug.LogWarning($"Locale with code {languageCode} not found!");
    }

    public void ChangeLanguage(string languageCode)
    {
        StartCoroutine(SetLocale(languageCode));
    }
}