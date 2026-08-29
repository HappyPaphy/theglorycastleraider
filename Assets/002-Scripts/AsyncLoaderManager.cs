using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Localization;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class AsyncLoaderManager : MonoBehaviour
{
    [Header("Loading Screens")]
    [SerializeField] private List<GifSpriteCharacter> characterGifs;
    [SerializeField] private CanvasGroup canvasGroup_LoadingScreen;
    [SerializeField] private CanvasGroup canvasGroup_ConfirmLoadScene;
    [SerializeField] private CanvasGroup canvasGroup_BlackFadeUI;
    [SerializeField] private CanvasGroup canvasGroup_TipText;

    [SerializeField] private GameObject rockyEye_LoadingNotCompleted;
    [SerializeField] private GameObject rockyEye_LoadingCompleted;

    [SerializeField] private TextMeshProUGUI text_Tip;
    [SerializeField] [TextArea] private List<string> str_Tips;
    public LocalizedString[] localizeStringTips;
    private LocalizedString previousString;

    [SerializeField] private Image image_Tip;
    [SerializeField] private List<Sprite> spr_Tips;
    [SerializeField] private Button confirmButton;

    [Header("Slider")]
    [SerializeField] private Slider slider_LoadlingSlider;

    private bool isReadyToActivate = false;
    private bool isTransitioning = false;
    private float timer = 0f;

    public static AsyncLoaderManager instance;

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
        isReadyToActivate = false;
        isTransitioning = false;

        canvasGroup_LoadingScreen.alpha = 0f;
        canvasGroup_LoadingScreen.interactable = false;
        canvasGroup_LoadingScreen.blocksRaycasts = false;
        canvasGroup_LoadingScreen.gameObject.SetActive(false);

        canvasGroup_ConfirmLoadScene.alpha = 0f;
        canvasGroup_BlackFadeUI.alpha = 0f;
        canvasGroup_TipText.alpha = 0f;

        rockyEye_LoadingNotCompleted.SetActive(true);
        rockyEye_LoadingCompleted.SetActive(false);
    }

    private void Update()
    {
        if(canvasGroup_LoadingScreen.alpha != 0f)
        {
            PlayAnimationOnImage();
        }

        if(rockyEye_LoadingCompleted.activeInHierarchy)
        {
            EventSystem.current.SetSelectedGameObject(confirmButton.gameObject);

            if (PlayerController.instance.IsInteractPressed)
            {
                ConfirmSceneChange();
            }
        }

        if(canvasGroup_LoadingScreen.alpha == 1f)
        {
            if(PlayerController.instance.IsReloadPressed)
            {
                StartCoroutine(ChangeTip());
            }
        }
    }

    private void PlayAnimationOnImage()
    {
        //if (!isPlayingAnimation) { return; }

        timer += Time.unscaledDeltaTime;

        if (timer >= 1f / 20f)
        {
            timer = 0f;

            foreach (GifSpriteCharacter gif in characterGifs)
            {
                gif.currentSpriteIndex++;

                if (gif.currentSpriteIndex >= gif.sprites_Character.Count)
                {
                    gif.currentSpriteIndex = 0;
                }

                gif.image_Character.sprite = gif.sprites_Character[gif.currentSpriteIndex];
            }
        }
    }

    public void ConfirmSceneChange()
    {
        isReadyToActivate = true;
    }

    public void LoadLevel(int levelToLoad, bool isNeedPlayerConfirm = false)
    {
        if(!isTransitioning)
        {
            isTransitioning = true;
            StartCoroutine(LoadLevelASync(levelToLoad, isNeedPlayerConfirm));
        }
    }

    public void LoadLevel(string levelToLoad, bool isNeedPlayerConfirm = false)
    {
        if (!isTransitioning)
        {
            isTransitioning = true;
            StartCoroutine(LoadLevelASync(levelToLoad, isNeedPlayerConfirm));
        }
    }

    private IEnumerator LoadLevelASync(string levelToLoad, bool isNeedPlayerConfirm)
    {
        canvasGroup_LoadingScreen.gameObject.SetActive(true);
        canvasGroup_LoadingScreen.interactable = true;
        canvasGroup_LoadingScreen.blocksRaycasts = true;

        int rnd = Random.Range(0, str_Tips.Count);
        image_Tip.sprite = spr_Tips[rnd];
        canvasGroup_LoadingScreen.DOFade(1f, 0.5f).SetUpdate(true);
        StartCoroutine(ChangeTip());
        yield return new WaitForSecondsRealtime(0.7f);
        AsyncOperation loadOperation = SceneManager.LoadSceneAsync(levelToLoad);

        StartCoroutine(LoadOperation(loadOperation, isNeedPlayerConfirm));
    }

    private IEnumerator LoadLevelASync(int levelToLoad, bool isNeedPlayerConfirm)
    {
        canvasGroup_LoadingScreen.gameObject.SetActive(true);
        canvasGroup_LoadingScreen.interactable = true;
        canvasGroup_LoadingScreen.blocksRaycasts = true;

        int rnd = Random.Range(0, str_Tips.Count);
        image_Tip.sprite = spr_Tips[rnd];
        canvasGroup_LoadingScreen.DOFade(1f, 0.5f).SetUpdate(true);
        StartCoroutine(ChangeTip());
        yield return new WaitForSecondsRealtime(0.7f);
        AsyncOperation loadOperation = SceneManager.LoadSceneAsync(levelToLoad);
        
        StartCoroutine(LoadOperation(loadOperation, isNeedPlayerConfirm));
    }

    private IEnumerator LoadOperation(AsyncOperation loadOperation, bool isNeedPlayerConfirm)
    {
        loadOperation.allowSceneActivation = false;

        while (loadOperation.progress < 0.9f)
        {
            float progressValue = Mathf.Clamp01(loadOperation.progress / 0.9f);
            slider_LoadlingSlider.value = progressValue;
            yield return null;
        }

        slider_LoadlingSlider.value = 1f;
        rockyEye_LoadingNotCompleted.SetActive(false);
        rockyEye_LoadingCompleted.SetActive(true);

        if (isNeedPlayerConfirm)
        {
            canvasGroup_ConfirmLoadScene.DOFade(1f, 0.5f).SetUpdate(true);
            yield return new WaitForSecondsRealtime(0.5f);
            canvasGroup_LoadingScreen.interactable = true;
            canvasGroup_LoadingScreen.blocksRaycasts = true;
            yield return new WaitUntil(() => isReadyToActivate);
        }

        canvasGroup_BlackFadeUI.DOFade(1f, 0.5f).SetUpdate(true);
        yield return new WaitForSecondsRealtime(0.7f);
        canvasGroup_LoadingScreen.interactable = false;

        while (canvasGroup_BlackFadeUI.alpha != 1f)
        {
            yield return null;
        }

        loadOperation.allowSceneActivation = true;
    }

    private IEnumerator ChangeTip()
    {
        RandomizeTips();
        canvasGroup_TipText.DOFade(0f, 0.15f).SetUpdate(true);
        yield return new WaitForSecondsRealtime(0.15f);
        canvasGroup_TipText.DOFade(1f, 0.15f).SetUpdate(true);
    }

    private async void RandomizeTips()
    {
        while(true)
        {
            int rnd = Random.Range(0, str_Tips.Count);

            var localizedString = localizeStringTips[rnd];

            if(previousString != null)
            {
                if (previousString == localizedString) { continue; }
            }

            previousString = localizedString;

            var handle = localizedString.GetLocalizedStringAsync();
            await handle.Task;

            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                text_Tip.text = handle.Result;
            }
            else
            {
                text_Tip.text = "[Missing Text]";
            }

            break;
        }
    }
}
