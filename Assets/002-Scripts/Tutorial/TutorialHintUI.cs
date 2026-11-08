using DG.Tweening;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Hint label in the middle of the screen, shown by TutorialManager after a tutorial panel closes.
/// A tutorial can have several hints: each one fades out when the player performs its dismiss action
/// (or after its auto-hide time), then the next one fades in, until none are left.
/// </summary>
public class TutorialHintUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Canvas canvas;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private RectTransform animatedRect;
    [SerializeField] private TextMeshProUGUI hintText;

    [Header("Transition (DOTween)")]
    [SerializeField] private float fadeInDuration = 0.4f;
    [SerializeField] private float fadeOutDuration = 0.3f;
    [SerializeField] private float startScale = 0.85f;
    [SerializeField] private float hideScale = 1.15f;
    [Tooltip("Pause between one hint fading out and the next fading in.")]
    [SerializeField] private float delayBetweenHints = 0.15f;

    [Header("Idle Pulse")]
    [SerializeField] private bool pulse = true;
    [SerializeField] private float pulseScale = 1.05f;
    [SerializeField] private float pulseDuration = 0.8f;

    [Header("Dismiss")]
    [Tooltip("Ignore the dismiss action for this long after showing, so a held key can't skip the hint instantly.")]
    [SerializeField] private float minDisplayTime = 0.5f;

    private const string KeyboardGroup = "Keyboard&Mouse";
    private const string GamepadGroup = "Gamepad";
    private const string KeyPlaceholder = "{key}";

    private readonly List<TutorialHint> hintSequence = new List<TutorialHint>();
    private int currentHintIndex = -1;
    private string sourceName;

    private string rawText;
    private InputAction dismissAction;
    private float autoHideTime;
    private float shownTimer;
    private bool isShowing;
    private System.Action onDismissed;
    private Tween nextHintTween;

    /// <summary>True while a hint is on screen or the next one is about to show.</summary>
    public bool IsShowing => isShowing || (nextHintTween != null && nextHintTween.IsActive());

    // Something (e.g. a blocker) is waiting for the key press, so auto-hide must not skip it
    private bool IsWaitingForKey => onDismissed != null && dismissAction != null;

    private void Awake()
    {
        HideImmediate();
    }

    private void OnEnable()
    {
        InputSchemeManager.OnInputModeChanged += HandleInputModeChanged;
    }

    private void OnDisable()
    {
        InputSchemeManager.OnInputModeChanged -= HandleInputModeChanged;
    }

    private void OnDestroy()
    {
        KillTweens();
    }

    public void HideImmediate()
    {
        KillTweens();
        isShowing = false;
        dismissAction = null;
        hintSequence.Clear();
        currentHintIndex = -1;

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        if (animatedRect != null) { animatedRect.localScale = Vector3.one; }
    }

    /// <summary>
    /// Shows the tutorial's hints one after another. onDismissed runs once the last hint is dismissed
    /// (or when Hide() / a newer Show() cuts the sequence short).
    /// While onDismissed is set, a hint with a dismiss action ignores its auto-hide time,
    /// so anything waiting on it (e.g. blockers) only opens after the key is pressed.
    /// </summary>
    public void Show(TutorialData data, System.Action onDismissed = null)
    {
        // A new sequence replaces the old one, so release whatever the old one was holding
        InvokeDismissed();
        KillTweens();
        hintSequence.Clear();

        if (data != null && data.Hints != null)
        {
            foreach (TutorialHint hint in data.Hints)
            {
                if (hint != null && hint.HasText) { hintSequence.Add(hint); }
            }
        }

        if (hintSequence.Count == 0)
        {
            onDismissed?.Invoke();
            return;
        }

        this.onDismissed = onDismissed;
        sourceName = data.name;
        currentHintIndex = -1;
        ShowNextHint();
    }

    /// <summary>Skips every remaining hint and fades out.</summary>
    public void Hide()
    {
        if (!IsShowing) { return; }

        hintSequence.Clear();
        currentHintIndex = -1;
        FadeOutCurrent();
        InvokeDismissed();
    }

    private void Update()
    {
        // Hidden while the game is paused (pause menu, loadout, another tutorial panel...)
        bool isGameRunning = Time.timeScale > 0f;
        if (canvas != null && canvas.enabled != isGameRunning) { canvas.enabled = isGameRunning; }

        if (!isShowing || !isGameRunning) { return; }

        shownTimer += Time.unscaledDeltaTime;
        if (shownTimer < minDisplayTime) { return; }

        if (dismissAction != null && dismissAction.WasPerformedThisFrame())
        {
            DismissCurrentHint();
        }
        else if (autoHideTime > 0f && shownTimer >= autoHideTime && !IsWaitingForKey)
        {
            DismissCurrentHint();
        }
    }

    // =========================================================
    // SEQUENCE
    // =========================================================

    private void ShowNextHint()
    {
        nextHintTween = null;
        currentHintIndex++;

        if (currentHintIndex >= hintSequence.Count)
        {
            isShowing = false;
            InvokeDismissed();
            return;
        }

        TutorialHint hint = hintSequence[currentHintIndex];
        rawText = hint.text;
        dismissAction = FindAction(hint.dismissAction);
        autoHideTime = hint.autoHideTime;
        shownTimer = 0f;
        isShowing = true;

        if (dismissAction == null && autoHideTime <= 0f)
        {
            Debug.LogWarning($"[TutorialHintUI] Hint {currentHintIndex + 1} of '{sourceName}' has no Dismiss Action and no " +
                             "Auto Hide Time, so it will never hide (and any blockers stay closed).");
        }

        RefreshText();

        KillTweens();
        canvasGroup.alpha = 0f;
        animatedRect.localScale = Vector3.one * startScale;

        canvasGroup.DOFade(1f, fadeInDuration).SetUpdate(true);
        animatedRect.DOScale(1f, fadeInDuration).SetEase(Ease.OutBack).SetUpdate(true)
            .OnComplete(StartPulse);
    }

    private void DismissCurrentHint()
    {
        bool isLastHint = currentHintIndex >= hintSequence.Count - 1;

        FadeOutCurrent();

        if (isLastHint)
        {
            // Release blockers the moment the final key is pressed, not after the fade
            currentHintIndex = hintSequence.Count;
            InvokeDismissed();
        }
        else
        {
            nextHintTween = DOVirtual.DelayedCall(fadeOutDuration + delayBetweenHints, ShowNextHint, true);
        }
    }

    private void FadeOutCurrent()
    {
        isShowing = false;
        dismissAction = null;

        KillTweens();
        canvasGroup.DOFade(0f, fadeOutDuration).SetUpdate(true);
        animatedRect.DOScale(hideScale, fadeOutDuration).SetEase(Ease.OutQuad).SetUpdate(true);
    }

    private void InvokeDismissed()
    {
        System.Action callback = onDismissed;
        onDismissed = null;
        callback?.Invoke();
    }

    // =========================================================
    // TWEENS
    // =========================================================

    private void StartPulse()
    {
        if (!pulse || !isShowing) { return; }

        animatedRect.DOScale(pulseScale, pulseDuration)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo)
            .SetUpdate(true);
    }

    private void KillTweens()
    {
        if (canvasGroup != null) { canvasGroup.DOKill(); }
        if (animatedRect != null) { animatedRect.DOKill(); }

        if (nextHintTween != null && nextHintTween.IsActive()) { nextHintTween.Kill(); }
        nextHintTween = null;
    }

    // =========================================================
    // TEXT / INPUT
    // =========================================================

    private void HandleInputModeChanged(InputMode mode)
    {
        if (isShowing) { RefreshText(); }
    }

    // Replaces {key} with the current binding, e.g. "Space" on keyboard or "A" on gamepad (respects rebinds).
    private void RefreshText()
    {
        if (hintText == null) { return; }

        string text = rawText;
        if (dismissAction != null && text.Contains(KeyPlaceholder))
        {
            text = text.Replace(KeyPlaceholder, GetBindingText(dismissAction));
        }

        hintText.text = text;
    }

    private static string GetBindingText(InputAction action)
    {
        bool isGamepad = InputSchemeManager.instance != null && InputSchemeManager.instance.currentInputMode != InputMode.PC;
        string binding = action.GetBindingDisplayString(group: isGamepad ? GamepadGroup : KeyboardGroup);

        if (string.IsNullOrEmpty(binding)) { binding = action.GetBindingDisplayString(); }
        return string.IsNullOrEmpty(binding) ? action.name : binding;
    }

    private static InputAction FindAction(TutorialHintAction hintAction)
    {
        if (hintAction == TutorialHintAction.None) { return null; }
        if (InputSchemeManager.instance == null || InputSchemeManager.instance.playerControls == null) { return null; }

        InputAction action = InputSchemeManager.instance.playerControls.asset.FindAction("Player/" + hintAction, false);
        if (action == null)
        {
            Debug.LogWarning($"[TutorialHintUI] No action 'Player/{hintAction}' found in PlayerInputActions.");
        }
        return action;
    }
}
