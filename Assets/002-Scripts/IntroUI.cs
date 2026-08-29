using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using UnityEngine.Localization;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public enum IntroState
{
    Intro,
    Warning,
    Recommend,
    GoingMainmenu
}

public class IntroUI : MonoBehaviour
{
    [SerializeField] private IntroState currentState;

    [SerializeField] private RectTransform rect_ChickenParent;
    [SerializeField] private RectTransform rect_EmojiParent;
    [SerializeField] private Image image_ChickenSmith;
    [SerializeField] private Image image_chickenEmojiA;
    [SerializeField] private Image image_chickenEmojiB;

    [SerializeField] private RectTransform rect_ChickenOnScreen;
    [SerializeField] private RectTransform rect_ChickenOffScreen;

    [SerializeField] private CanvasGroup canvasGroup_IntroUI;
    [SerializeField] private CanvasGroup canvasGroup_HappyPaphyText;
    [SerializeField] private CanvasGroup canvasGroup_Discalmer;
    [SerializeField] private CanvasGroup canvasGroup_Recommend;
    [SerializeField] private CanvasGroup canvasGroup_PressAnybutton;
    [SerializeField] private CanvasGroup canvasGroup_PressAnybutton_Text;
    [SerializeField] private List<Sprite> sprites_ChickenSmith;
    [SerializeField] private List<Sprite> sprites_ChickenEmoji;

    private int currentIndex_ChickenSmith = 0;
    private int currentIndex_chickenEmoji = 0;

    private bool isTextFadeOn = true;
    private bool isTransitioning = true;
    private bool isPlayingAnimation = false;
    private float timer = 0f;

    private void Start()
    {
        SetUpUI();
        currentState = IntroState.Intro;
        StartCoroutine(TransitioningToIntroUI());
    }

    private void Update()
    {
        PlayAnimationOnImage();
        HandleState();
        HandlePressAnyButtonText();
    }

    private void HandlePressAnyButtonText()
    {
        //if(canvasGroup_PressAnybutton.alpha <= 0f) { return; }

        if (canvasGroup_PressAnybutton_Text.alpha == 1f && isTextFadeOn)
        {
            isTextFadeOn = false;
            canvasGroup_PressAnybutton_Text.DOFade(0f, 0.3f).SetUpdate(true);
        }
        else if(canvasGroup_PressAnybutton_Text.alpha == 0f && !isTextFadeOn)
        {
            isTextFadeOn = true;
            canvasGroup_PressAnybutton_Text.DOFade(1f, 0.3f).SetUpdate(true);
        }
    }

    private bool IsAnyKeyDown()
    {
        bool isKeyboard = Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame;
        bool isMouse = Mouse.current != null && (Mouse.current.leftButton.wasPressedThisFrame || Mouse.current.rightButton.wasPressedThisFrame);

        bool isGamepad = false;
        if (Gamepad.current != null)
        {
            // Checks standard gamepad face buttons and start button
            isGamepad = Gamepad.current.buttonSouth.wasPressedThisFrame ||
                        Gamepad.current.buttonEast.wasPressedThisFrame ||
                        Gamepad.current.buttonWest.wasPressedThisFrame ||
                        Gamepad.current.buttonNorth.wasPressedThisFrame ||
                        Gamepad.current.startButton.wasPressedThisFrame;
        }

        return isKeyboard || isMouse || isGamepad;
    }

    private void HandleState()
    {
        switch (currentState)
        {
            case IntroState.Intro:
                {
                    if (IsAnyKeyDown() && !isTransitioning)
                    {
                        StopAllCoroutines();
                        isTransitioning = true;
                        StartCoroutine(TransitioningToWarning(true));
                    }
                }
                break;

            case IntroState.Warning:
                {
                    if (IsAnyKeyDown() && !isTransitioning)
                    {
                        StopAllCoroutines();
                        isTransitioning = true;
                        StartCoroutine(TransitioningToRecommend(true));
                    }
                }
                break;

            case IntroState.Recommend:
                {
                    if (IsAnyKeyDown() && !isTransitioning)
                    {
                        StopAllCoroutines();
                        isTransitioning = true;
                        StartCoroutine(TransitioningToMainmenu(true));
                    }
                }
                break;
        }
    }

    private IEnumerator TransitioningToIntroUI()
    {
        isPlayingAnimation = true;

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        yield return new WaitForSecondsRealtime(2f);
        rect_ChickenParent.DOLocalMove(rect_ChickenOnScreen.localPosition, 0.3f).SetUpdate(true);
        yield return new WaitForSecondsRealtime(0.5f);
        isTransitioning = false;
        yield return new WaitForSecondsRealtime(1.5f);
        SoundManager.instance.ChickenSound(0);
        rect_EmojiParent.DOScale(2f, 0.3f).SetUpdate(true);
        yield return new WaitForSecondsRealtime(2f);
        canvasGroup_HappyPaphyText.DOFade(1f, 0.5f).SetUpdate(true);
        yield return new WaitForSecondsRealtime(1.5f);
        canvasGroup_IntroUI.DOFade(0f, 0.5f).SetUpdate(true);
        yield return new WaitForSecondsRealtime(0.75f);
        StartCoroutine(TransitioningToWarning(false));
    }

    private IEnumerator TransitioningToWarning(bool isSkip)
    {
        currentState = IntroState.Warning;

        if (isSkip)
        {
            canvasGroup_IntroUI.DOFade(0f, 0.5f).SetUpdate(true);
            yield return new WaitForSecondsRealtime(0.75f);
        }

        canvasGroup_Discalmer.DOFade(1f, 0.5f).SetUpdate(true);
        yield return new WaitForSecondsRealtime(0.5f);
        isTransitioning = false;
        canvasGroup_PressAnybutton.DOFade(1f, 0.5f).SetUpdate(true);
        yield return new WaitForSecondsRealtime(10f);
        canvasGroup_Discalmer.DOFade(0f, 0.5f).SetUpdate(true);
        canvasGroup_PressAnybutton.DOFade(0f, 0.5f).SetUpdate(true);
        yield return new WaitForSecondsRealtime(0.75f);
        StartCoroutine(TransitioningToRecommend(false));
    }

    private IEnumerator TransitioningToRecommend(bool isSkip)
    {
        currentState = IntroState.Recommend;
        canvasGroup_PressAnybutton.DOKill();
        canvasGroup_PressAnybutton.alpha = 0f;

        if (isSkip)
        {
            canvasGroup_Discalmer.DOFade(0f, 0.5f).SetUpdate(true);
            yield return new WaitForSecondsRealtime(0.75f);
        }

        canvasGroup_Recommend.DOFade(1f, 0.5f).SetUpdate(true);
        yield return new WaitForSecondsRealtime(0.5f);
        isTransitioning = false;
        canvasGroup_PressAnybutton.DOFade(1f, 0.5f).SetUpdate(true);
        yield return new WaitForSecondsRealtime(10f);
        canvasGroup_Recommend.DOFade(0f, 0.5f).SetUpdate(true);
        canvasGroup_PressAnybutton.DOFade(0f, 0.5f).SetUpdate(true);
        yield return new WaitForSecondsRealtime(2f);
        StartCoroutine(TransitioningToMainmenu(false));
    }

    private IEnumerator TransitioningToMainmenu(bool isSkip)
    {
        currentState = IntroState.GoingMainmenu;
        canvasGroup_PressAnybutton.DOKill();
        canvasGroup_PressAnybutton.alpha = 0f;

        if (isSkip)
        {
            canvasGroup_Recommend.DOFade(0f, 0.5f).SetUpdate(true);
            yield return new WaitForSecondsRealtime(0.75f);
        }

        yield return new WaitForSecondsRealtime(0.5f);
        SceneManager.LoadScene("MainMenu");
    }

    private void PlayAnimationOnImage()
    {
        if (!isPlayingAnimation) { return; }

        timer += Time.unscaledDeltaTime;

        if (timer >= 1f / 10f)
        {
            timer = 0f;
            currentIndex_ChickenSmith++;
            currentIndex_chickenEmoji++;

            if (currentIndex_ChickenSmith >= sprites_ChickenSmith.Count)
            {
                currentIndex_ChickenSmith = 0;
            }
            if (currentIndex_chickenEmoji >= sprites_ChickenEmoji.Count)
            {
                currentIndex_chickenEmoji = 0;
            }

            image_ChickenSmith.sprite = sprites_ChickenSmith[currentIndex_ChickenSmith];
            image_chickenEmojiA.sprite = sprites_ChickenEmoji[currentIndex_chickenEmoji];
            image_chickenEmojiB.sprite = sprites_ChickenEmoji[currentIndex_chickenEmoji];
        }
    }

    private void SetUpUI()
    {
        canvasGroup_IntroUI.alpha = 1f;
        canvasGroup_PressAnybutton_Text.alpha = 1f;
        canvasGroup_PressAnybutton.alpha = 0f;
        canvasGroup_HappyPaphyText.alpha = 0f;
        canvasGroup_Discalmer.alpha = 0f;
        canvasGroup_Recommend.alpha = 0f;
        rect_EmojiParent.localScale = Vector3.zero;
        rect_ChickenParent.localPosition = rect_ChickenOffScreen.localPosition;
    }
}
