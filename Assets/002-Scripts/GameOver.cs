using UnityEngine;
using DG.Tweening;
using System.Collections;

public class GameOver : MonoBehaviour
{
    [SerializeField] private CanvasGroup canvasGroup_GameOver;
    [Tooltip("The old blinking 'press to restart' text. Kept hidden: the Restart / End Run buttons replace it.")]
    [SerializeField] private CanvasGroup canvasGroup_PressToRestart;

    [HideInInspector] public bool isTriggerOnce = false;

    public static GameOver instance;

    private void Awake()
    {
        instance = this;
    }

    void Start()
    {
        SetUpUI();
    }

    void Update()
    {
        if(PlayerController.instance.CharacterHealthComponent.CurrentHP <= 0f && !isTriggerOnce)
        {
            isTriggerOnce = true;
            MetaRun.EndRun(false);          // meta progression: run over, Emeralds kept
            StartCoroutine(TriggerGameOver());
        }
    }

    public IEnumerator TriggerGameOver()
    {
        yield return new WaitForSecondsRealtime(1f);

        canvasGroup_GameOver.DOFade(1f, 0.5f).SetUpdate(true);
        yield return new WaitForSecondsRealtime(0.5f);

        // Choose: try again now, or end the run (result panel, then the main menu)
        GameOverMenu.Show(Restart, EndRun);
    }

    private void Restart()
    {
        canvasGroup_GameOver.DOKill();
        canvasGroup_GameOver.DOFade(0f, 0.5f).SetUpdate(true);
        PauseGame.instance.RestartScene();
    }

    private void EndRun()
    {
        canvasGroup_GameOver.DOKill();
        canvasGroup_GameOver.DOFade(0f, 0.3f).SetUpdate(true);
        RunSummaryUI.Show(false, () => PauseGame.instance.BackToMainMenu());
    }

    private void SetUpUI()
    {
        canvasGroup_GameOver.alpha = 0f;
        if (canvasGroup_PressToRestart != null) { canvasGroup_PressToRestart.alpha = 0f; }
    }
}
