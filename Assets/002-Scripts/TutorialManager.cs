using DG.Tweening;
using System.Collections;
using UnityEngine;

public enum TutorialState
{
    Idle,
    ChangeToRoguelike,
    FallOfToDeath
}

public class TutorialManager : MonoBehaviour
{
    public TutorialState currentState;

    [SerializeField] private GameObject enemySet_Easy;
    [SerializeField] private GameObject enemySet_Medium;
    [SerializeField] private GameObject enemySet_Hard;
    [SerializeField] private GameObject enemySet_Impossible;

    [SerializeField] private CanvasGroup canvasGroup_BlackFadeUI;
    [SerializeField] private CanvasGroup[] canvasGroup_ChangeScene;
    [SerializeField] private CanvasGroup[] canvasGroup_FallToDeath;

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
