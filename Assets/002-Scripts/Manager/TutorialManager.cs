using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum TutorialState
{
    Idle,
    ChangeToRoguelike,
    FallOfToDeath,
    PickUpWeapon,
    ShowingPopup
}

public class TutorialManager : MonoBehaviour
{
    public TutorialState currentState;

    [Header("Tutorial Popup")]
    [Tooltip("The TutorialPopup prefab (Assets/003-Prefabs/TutorialPopup.prefab). Spawned once and reused.")]
    [SerializeField] private TutorialPopupUI tutorialPopupPrefab;
    [Tooltip("The TutorialHint prefab (Assets/003-Prefabs/TutorialHint.prefab). Shown after a panel closes if its TutorialData has a hint.")]
    [SerializeField] private TutorialHintUI tutorialHintPrefab;

    [HideInInspector] public bool isPanelActive = false;

    private TutorialPopupUI popupInstance;
    private TutorialHintUI hintInstance;
    private struct PendingTutorial
    {
        public TutorialData data;
        public System.Action onHintDismissed;
    }

    private readonly Queue<PendingTutorial> popupQueue = new Queue<PendingTutorial>();
    private readonly HashSet<TutorialData> shownTutorials = new HashSet<TutorialData>();
    private bool isPopupRoutineRunning = false;

    [SerializeField] private GameObject enemySet_Easy;
    [SerializeField] private GameObject enemySet_Medium;
    [SerializeField] private GameObject enemySet_Hard;
    [SerializeField] private GameObject enemySet_Impossible;

    [SerializeField] private CanvasGroup canvasGroup_BlackFadeUI;
    [SerializeField] private CanvasGroup[] canvasGroup_ChangeScene;
    [SerializeField] private CanvasGroup[] canvasGroup_FallToDeath;
    [SerializeField] private CanvasGroup[] canvasGroup_PickUpWeapon;

    private bool isSetDifficultyOnce = false;
    [HideInInspector] public bool isTriggerSceneChangeOnce = false;

    public static TutorialManager instance;

    private void Awake()
    {
        instance = this;
    }

    private void Start()
    {
        SetUpUI();
    }

    void Update()
    {
        if(GameManager.instance != null && !isSetDifficultyOnce)
        {
            isSetDifficultyOnce = true;
            SetDifficulty();
        }
    }

    // =========================================================
    // TUTORIAL POPUPS
    // =========================================================

    /// <summary>
    /// Pauses the game and shows every page of the tutorial. Queues it if another one is open.
    /// onHintDismissed runs once the player presses the hint's dismiss action
    /// (or right after the panel closes if there's no hint to wait for).
    /// Returns false if the tutorial wasn't shown (already seen, no pages, no prefab).
    /// </summary>
    public bool ShowTutorial(TutorialData data, System.Action onHintDismissed = null)
    {
        if (data == null || data.PageCount == 0)
        {
            Debug.LogWarning("[TutorialManager] ShowTutorial called with no TutorialData or no pages.");
            return false;
        }

        if (data.ShowOnlyOnce && shownTutorials.Contains(data)) { return false; }
        foreach (PendingTutorial pending in popupQueue)
        {
            if (pending.data == data) { return false; }
        }

        if (GetPopup() == null) { return false; }

        shownTutorials.Add(data);
        popupQueue.Enqueue(new PendingTutorial { data = data, onHintDismissed = onHintDismissed });

        if (!isPopupRoutineRunning)
        {
            StartCoroutine(PopupRoutine());
        }

        return true;
    }

    private TutorialPopupUI GetPopup()
    {
        if (popupInstance != null) { return popupInstance; }

        if (tutorialPopupPrefab == null)
        {
            Debug.LogWarning("[TutorialManager] Tutorial Popup Prefab is not assigned.");
            return null;
        }

        popupInstance = Instantiate(tutorialPopupPrefab);
        popupInstance.name = tutorialPopupPrefab.name;
        popupInstance.HideImmediate();
        return popupInstance;
    }

    private IEnumerator PopupRoutine()
    {
        isPopupRoutineRunning = true;

        TutorialState previousState = currentState;
        currentState = TutorialState.ShowingPopup;
        isPanelActive = true;
        Time.timeScale = 0f;
        NotifyInputActiveChange();

        // If several tutorials were queued, the last one with a hint wins.
        // Tutorials whose hint won't be shown release their callback (e.g. blockers) straight away.
        PendingTutorial hintEntry = default;

        while (popupQueue.Count > 0)
        {
            PendingTutorial entry = popupQueue.Dequeue();
            yield return popupInstance.Play(entry.data);

            if (entry.data.HasHint)
            {
                hintEntry.onHintDismissed?.Invoke();
                hintEntry = entry;
            }
            else
            {
                entry.onHintDismissed?.Invoke();
            }
        }

        currentState = previousState == TutorialState.ShowingPopup ? TutorialState.Idle : previousState;
        isPanelActive = false;
        Time.timeScale = 1f;
        NotifyInputActiveChange();

        isPopupRoutineRunning = false;

        // Show after input is re-enabled so the dismiss action can be detected
        if (hintEntry.data != null)
        {
            TutorialHintUI hint = GetHint();
            if (hint != null)
            {
                hint.Show(hintEntry.data, hintEntry.onHintDismissed);
            }
            else
            {
                hintEntry.onHintDismissed?.Invoke();
            }
        }
    }

    /// <summary>Hides the hint label early, e.g. from a scripted event.</summary>
    public void HideHint()
    {
        if (hintInstance != null) { hintInstance.Hide(); }
    }

    private TutorialHintUI GetHint()
    {
        if (hintInstance != null) { return hintInstance; }

        if (tutorialHintPrefab == null)
        {
            Debug.LogWarning("[TutorialManager] Tutorial Hint Prefab is not assigned.");
            return null;
        }

        hintInstance = Instantiate(tutorialHintPrefab);
        hintInstance.name = tutorialHintPrefab.name;
        hintInstance.HideImmediate();
        return hintInstance;
    }

    private void NotifyInputActiveChange()
    {
        if (InputSchemeManager.instance != null)
        {
            InputSchemeManager.instance.OnInputActiveChange?.Invoke();
        }
    }

    private void SetDifficulty()
    {
        switch (GameManager.instance.gameDifficultyState)
        {
            case GameDifficultyState.Easy:
                {
                    enemySet_Medium.SetActive(false);
                    enemySet_Hard.SetActive(false);
                    enemySet_Impossible.SetActive(false);
                }
                break;

            case GameDifficultyState.Medium:
                {
                    enemySet_Hard.SetActive(false);
                    enemySet_Impossible.SetActive(false);
                }
                break;

            case GameDifficultyState.Hard:
                {
                    enemySet_Impossible.SetActive(false);
                }
                break;
        }
    }

    public IEnumerator TriggerChangeScene()
    {
        if(isTriggerSceneChangeOnce) { yield break; }
        isTriggerSceneChangeOnce = true;

        currentState = TutorialState.ChangeToRoguelike;
        Time.timeScale = 0f;

        canvasGroup_BlackFadeUI.DOFade(1f, 0.4f).SetUpdate(true);
        yield return new WaitForSecondsRealtime(0.7f);

        for (int i = 0; i < canvasGroup_ChangeScene.Length; i++)
        {
            canvasGroup_ChangeScene[i].DOFade(1f, 0.8f).SetUpdate(true);
            yield return new WaitForSecondsRealtime(3f);
            canvasGroup_ChangeScene[i].DOFade(0f, 0.8f).SetUpdate(true);
            yield return new WaitForSecondsRealtime(1.5f);
        }

        StartCoroutine(PauseGame.instance.ChangeScene(1));
    }

    public IEnumerator TriggerFallToDeath()
    {
        if (isTriggerSceneChangeOnce) { yield break; }
        isTriggerSceneChangeOnce = true;

        currentState = TutorialState.FallOfToDeath;
        Time.timeScale = 0f;

        canvasGroup_BlackFadeUI.DOFade(1f, 0.4f).SetUpdate(true);
        yield return new WaitForSecondsRealtime(0.7f);

        for (int i = 0; i < canvasGroup_FallToDeath.Length; i++)
        {
            canvasGroup_FallToDeath[i].DOFade(1f, 0.8f).SetUpdate(true);
            yield return new WaitForSecondsRealtime(3f);
            canvasGroup_FallToDeath[i].DOFade(0f, 0.8f).SetUpdate(true);
            yield return new WaitForSecondsRealtime(1.5f);
        }

        StartCoroutine(GameOver.instance.TriggerGameOver());
    }

    public IEnumerator TriggerPickUpWeapon(bool isActive)
    {
        if (isTriggerSceneChangeOnce) { yield break; }
        isTriggerSceneChangeOnce = true;

        currentState = TutorialState.PickUpWeapon;

        for (int i = 0; i < canvasGroup_PickUpWeapon.Length; i++)
        {
            canvasGroup_PickUpWeapon[i].DOFade(1f, 0.8f).SetUpdate(true);
            yield return new WaitForSecondsRealtime(7f);
            canvasGroup_PickUpWeapon[i].DOFade(0f, 0.8f).SetUpdate(true);
            yield return new WaitForSecondsRealtime(1.5f);
        }
    }

    private void SetUpUI()
    {
        canvasGroup_BlackFadeUI.alpha = 1f;
        canvasGroup_BlackFadeUI.DOFade(0f, 0.5f);

        for (int i = 0; i < canvasGroup_FallToDeath.Length; i++)
        {
            canvasGroup_FallToDeath[i].alpha = 0f;
        }

        for (int i = 0; i < canvasGroup_ChangeScene.Length; i++)
        {
            canvasGroup_ChangeScene[i].alpha = 0f;
        }
    }
}
