using DG.Tweening;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Multi-page tutorial popup. Lives on the TutorialPopup prefab and is driven by TutorialManager.
/// Keyboard/gamepad: UI/Submit = next page (closes on the last page), UI/Cancel = previous page.
/// Mouse: Back / Next buttons.
/// </summary>
public class TutorialPopupUI : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private CanvasGroup canvasGroup;
    [Tooltip("Optional. Fades between pages.")]
    [SerializeField] private CanvasGroup contentGroup;

    [Header("Page Content")]
    [SerializeField] private Image gifImage;
    [SerializeField] private TextMeshProUGUI headerText;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private TextMeshProUGUI pageCounterText;

    [Header("Buttons")]
    [SerializeField] private Button previousButton;
    [SerializeField] private Button nextButton;
    [SerializeField] private TextMeshProUGUI nextButtonLabel;
    [SerializeField] private string nextLabel = "Next";
    [SerializeField] private string closeLabel = "Continue";

    [Header("Fade (DOTween, unscaled time)")]
    [SerializeField] private float fadeInDuration = 0.35f;
    [SerializeField] private float fadeOutDuration = 0.25f;
    [SerializeField] private float pageFadeDuration = 0.15f;

    private TutorialData currentData;
    private int currentPageIndex;
    private bool isOpen;
    private bool isAcceptingInput;
    private int lastActionFrame = -1;

    // GIF playback
    private Sprite[] currentFrames;
    private float frameInterval;
    private float frameTimer;
    private int frameIndex;

    private void Awake()
    {
        if (previousButton != null) { previousButton.onClick.AddListener(PreviousPage); }
        if (nextButton != null) { nextButton.onClick.AddListener(NextPage); }

        HideImmediate();
    }

    public void HideImmediate()
    {
        isOpen = false;
        isAcceptingInput = false;

        if (canvasGroup != null)
        {
            canvasGroup.DOKill();
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
    }

    /// <summary>Shows every page of the tutorial and finishes once the player closes it.</summary>
    public IEnumerator Play(TutorialData data)
    {
        if (data == null || data.PageCount == 0) { yield break; }

        currentData = data;
        currentPageIndex = 0;
        isOpen = true;
        isAcceptingInput = false;
        ApplyPage(currentPageIndex);

        canvasGroup.DOKill();
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = true;
        yield return canvasGroup.DOFade(1f, fadeInDuration).SetUpdate(true).WaitForCompletion();

        canvasGroup.interactable = true;

        // Buttons are mouse only, keyboard/gamepad go through InputSchemeManager.
        // Keeping them unselected stops the EventSystem from also clicking them on Submit.
        if (EventSystem.current != null) { EventSystem.current.SetSelectedGameObject(null); }

        ClearBufferedInput();
        isAcceptingInput = true;

        yield return new WaitUntil(() => !isOpen);

        isAcceptingInput = false;
        canvasGroup.interactable = false;
        yield return canvasGroup.DOFade(0f, fadeOutDuration).SetUpdate(true).WaitForCompletion();
        canvasGroup.blocksRaycasts = false;

        currentData = null;
        currentFrames = null;
    }

    private void Update()
    {
        AnimateGif();

        if (!isAcceptingInput || InputSchemeManager.instance == null) { return; }

        if (InputSchemeManager.instance.IsSubmitPressed)
        {
            InputSchemeManager.instance.IsSubmitPressed = false;
            NextPage();
        }
        else if (InputSchemeManager.instance.IsCancelPressed)
        {
            InputSchemeManager.instance.IsCancelPressed = false;
            PreviousPage();
        }
    }

    public void NextPage()
    {
        if (!TryConsumeAction()) { return; }

        if (currentPageIndex >= currentData.PageCount - 1)
        {
            isOpen = false;
            return;
        }

        StartCoroutine(ChangePage(currentPageIndex + 1));
    }

    public void PreviousPage()
    {
        if (currentPageIndex <= 0) { return; }
        if (!TryConsumeAction()) { return; }

        StartCoroutine(ChangePage(currentPageIndex - 1));
    }

    // Only one page action per frame, so a mouse click and a Submit press can't both fire together.
    private bool TryConsumeAction()
    {
        if (!isAcceptingInput || currentData == null) { return false; }
        if (lastActionFrame == Time.frameCount) { return false; }

        lastActionFrame = Time.frameCount;
        return true;
    }

    private IEnumerator ChangePage(int newIndex)
    {
        isAcceptingInput = false;

        if (contentGroup != null && pageFadeDuration > 0f)
        {
            contentGroup.DOKill();
            yield return contentGroup.DOFade(0f, pageFadeDuration).SetUpdate(true).WaitForCompletion();
        }

        currentPageIndex = newIndex;
        ApplyPage(currentPageIndex);

        if (contentGroup != null && pageFadeDuration > 0f)
        {
            yield return contentGroup.DOFade(1f, pageFadeDuration).SetUpdate(true).WaitForCompletion();
        }

        ClearBufferedInput();
        isAcceptingInput = isOpen;
    }

    private void ApplyPage(int index)
    {
        TutorialPage page = currentData.Pages[index];
        int pageCount = currentData.PageCount;

        if (headerText != null) { headerText.text = page.header; }
        if (descriptionText != null) { descriptionText.text = page.description; }

        if (pageCounterText != null)
        {
            pageCounterText.gameObject.SetActive(pageCount > 1);
            pageCounterText.text = $"{index + 1} / {pageCount}";
        }

        if (previousButton != null)
        {
            previousButton.gameObject.SetActive(pageCount > 1);
            previousButton.interactable = index > 0;
        }

        if (nextButtonLabel != null)
        {
            nextButtonLabel.text = index >= pageCount - 1 ? closeLabel : nextLabel;
        }

        SetGif(page);

        if (contentGroup != null && pageFadeDuration <= 0f) { contentGroup.alpha = 1f; }
    }

    private void SetGif(TutorialPage page)
    {
        currentFrames = page.gifFrames;
        frameInterval = 1f / Mathf.Max(0.1f, page.framesPerSecond);
        frameTimer = 0f;
        frameIndex = 0;

        if (gifImage == null) { return; }

        bool hasFrames = currentFrames != null && currentFrames.Length > 0;
        gifImage.enabled = hasFrames;
        if (hasFrames) { gifImage.sprite = currentFrames[0]; }
    }

    // Uses unscaled time so the GIF keeps playing while Time.timeScale = 0.
    private void AnimateGif()
    {
        if (gifImage == null || currentFrames == null || currentFrames.Length <= 1) { return; }

        frameTimer += Time.unscaledDeltaTime;
        while (frameTimer >= frameInterval)
        {
            frameTimer -= frameInterval;
            frameIndex = (frameIndex + 1) % currentFrames.Length;
            gifImage.sprite = currentFrames[frameIndex];
        }
    }

    private void ClearBufferedInput()
    {
        if (InputSchemeManager.instance == null) { return; }

        InputSchemeManager.instance.IsSubmitPressed = false;
        InputSchemeManager.instance.IsCancelPressed = false;
    }
}
