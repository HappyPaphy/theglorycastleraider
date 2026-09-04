using UnityEngine;
using DG.Tweening;
using System.Collections;

public class GameOver : MonoBehaviour
{
    [SerializeField] private CanvasGroup canvasGroup_GameOver;
    [SerializeField] private CanvasGroup canvasGroup_PressToRestart;

    [HideInInspector] public bool isTriggerOnce = false;
    private bool isRestartAvailable = false;

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
            StartCoroutine(TriggerGameOver());
        }

        if(isRestartAvailable)
        {
            if(PlayerController.instance.IsInteractPressed)
            {
                canvasGroup_GameOver.DOKill();
                canvasGroup_GameOver.DOFade(0f, 0.5f).SetUpdate(true);

                PauseGame.instance.RestartScene();
            }

            if(canvasGroup_PressToRestart.alpha == 0f)
            {
                canvasGroup_PressToRestart.DOFade(1f, 0.3f).SetUpdate(true);
            }
            else if(canvasGroup_PressToRestart.alpha == 1f)
            {
                canvasGroup_PressToRestart.DOFade(0f, 0.3f).SetUpdate(true);
            }
        }
    }

    public IEnumerator TriggerGameOver()
    {
        yield return new WaitForSecondsRealtime(1f);

        canvasGroup_GameOver.DOFade(1f, 0.5f).SetUpdate(true);
        yield return new WaitForSecondsRealtime(0.5f);
        isRestartAvailable = true;
    }

    private void SetUpUI()
    {
        canvasGroup_GameOver.alpha = 0f;
    }
}
