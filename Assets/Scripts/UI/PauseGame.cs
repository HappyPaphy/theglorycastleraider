using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public enum PauseGameState
{
    NotActive,
    Pause,
    RestartConfirm,
    MainMenuConfirm
}

public class PauseGame : MonoBehaviour
{
    [SerializeField] private PauseGameState currentState;

    [Header("CanvasGroups")]
    [SerializeField] private CanvasGroup canvasGroup_BlackFade;
    [SerializeField] private CanvasGroup canvasGroup_PausedMenu;
    [SerializeField] private CanvasGroup canvasGroup_Setting;

    [Header("FirstSelected-GameObjects")]
    [SerializeField] private GameObject firstSelected_PauseMenu;
    [SerializeField] private GameObject firstSelected_RestartConfirm;
    [SerializeField] private GameObject firstSelected_MainMenuConfirm;

    [Header("Control-Tips")]
    [SerializeField] private GameObject playStationControllerTip;
    [SerializeField] private GameObject xboxControllerTip;
    [SerializeField] private GameObject keyboardAndMouseTip;

    [Header("Buttons")]
    [SerializeField] private Button button_Resume;
    [SerializeField] private Button button_Restart;
    [SerializeField] private Button button_MainMenu;
    [SerializeField] private Button button_Setting;
    [SerializeField] private Button button_ReturnFromSettingToPauseMenu;

    [SerializeField] private Button button_CancelRestart;
    [SerializeField] private Button button_ConfirmRestart;

    [SerializeField] private Button button_CancelMainMenu;
    [SerializeField] private Button button_ConfirmMainMenu;

    [Header("RectTransform")]
    [SerializeField] private RectTransform pauseMenuPanel;
    [SerializeField] private RectTransform restartConfirmPanel;
    [SerializeField] private RectTransform mainMenuConfirmPanel;

    [Header("Variables")]
    [SerializeField] private bool isPaused = false;
    public bool IsPaused { get { return isPaused; } set { isPaused = value; } }

    [SerializeField] private bool isHowToPlayOn = false;

    private bool isReadingScroll = false;
    public bool IsReadingScroll { get { return isReadingScroll; } set { isReadingScroll = value; } }

    [SerializeField] private bool isThisRoguelikeMode = false;
    [SerializeField] private bool isThisDemoVersion = false;
    public bool isThisMainmenu = false;

    [Header("Others")]
    [SerializeField] private TextMeshProUGUI currentTimeLimitText;
    [SerializeField] private TextMeshProUGUI killCount;
    [SerializeField] private TextMeshProUGUI gloricCount;
    [SerializeField] private TextMeshProUGUI parryCount;

    //[SerializeField] private RectTransform[] textObjectives;
    //[SerializeField] private RectTransform 

    public static PauseGame instance;

    private void Awake()
    {
        instance = this;

        SetUpButton();
        SetUpUI();
    }

    void Start()
    {
        
    }

    void Update()
    {
        if (PlayerController.instance.IsPausePressed && !isPaused && !isHowToPlayOn && !isReadingScroll 
            /*&& !EquipmentManager.instance.isReplacePanelActive*/ && !isThisMainmenu && !GameOver.instance.isTriggerOnce
            )
        {
            if(TutorialManager.instance != null)
            {
                if (TutorialManager.instance.currentState != TutorialState.Idle) { return; }
            }

            PlayerController.instance.IsPausePressed = false;

            if (RoguelikeManager.instance != null)
            {
                /*if (RoguelikeManager.instance.roguelikePhase != RoguelikePhase.PlaySequence)
                {
                    return;
                }*/
            }

            PauseTheGame();
        }
        else if(PlayerController.instance.IsPausePressed && isPaused && !isHowToPlayOn && !isReadingScroll)
        {
            PlayerController.instance.IsPausePressed = false;
            SettingManager.instance.isSettingActive = false;

            MakePanelActive(mainMenuConfirmPanel, false);
            MakePanelActive(restartConfirmPanel, false);
            ResumeTheGame();
        }
        
        if(isPaused)
        {
            UpdateUI();
            HandleButtons();
        }
    }

    private void HandleButtons()
    {
        InputSchemeManager.instance.MakeButtonInteracable(button_Resume, (currentState == PauseGameState.Pause && !SettingManager.instance.isSettingActive));
        InputSchemeManager.instance.MakeButtonInteracable(button_Restart, (currentState == PauseGameState.Pause && !SettingManager.instance.isSettingActive));
        InputSchemeManager.instance.MakeButtonInteracable(button_MainMenu, (currentState == PauseGameState.Pause && !SettingManager.instance.isSettingActive));

        InputSchemeManager.instance.MakeButtonInteracable(button_ConfirmRestart, (currentState == PauseGameState.RestartConfirm && !SettingManager.instance.isSettingActive));
        InputSchemeManager.instance.MakeButtonInteracable(button_CancelRestart, (currentState == PauseGameState.RestartConfirm && !SettingManager.instance.isSettingActive));

        InputSchemeManager.instance.MakeButtonInteracable(button_ConfirmMainMenu, (currentState == PauseGameState.MainMenuConfirm && !SettingManager.instance.isSettingActive));
        InputSchemeManager.instance.MakeButtonInteracable(button_CancelMainMenu, (currentState == PauseGameState.MainMenuConfirm && !SettingManager.instance.isSettingActive));
    }

    private void UpdateUI()
    {
        /*if (InputSchemeManager.instance.CurrentInputMode == InputMode.Xbox)
        {
            //playStationControllerTip.SetActive(false);
            keyboardAndMouseTip.SetActive(false);
            xboxControllerTip.SetActive(true);
        }
        else if (InputSchemeManager.instance.CurrentInputMode == InputMode.PC)
        {
            //playStationControllerTip.SetActive(false);
            keyboardAndMouseTip.SetActive(true);
            xboxControllerTip.SetActive(false);
        }
        else if (InputSchemeManager.instance.CurrentInputMode == InputMode.PlayStation)
        {
            //playStationControllerTip.SetActive(true);
            keyboardAndMouseTip.SetActive(false);
            xboxControllerTip.SetActive(false);
        }*/
    }

    private void PauseTheGame()
    {
        /*if (PlayerUpgradeManager.instance != null)
        {
            if (PlayerUpgradeManager.instance.IsSkillTreePanelActive) { return; }
        }*/

       /* if (UpgradeCharacterCardManager.instance != null)
        {
            if (UpgradeCharacterCardManager.instance.upgradeCharacterCards_InPanel.Count > 0) { return; }
        }*/

        //if (PlayerController.instance.CharacterHealthComponent.CurrentHP <= 0) { return; }

        currentState = PauseGameState.Pause;

        UpdateRoguelikeText();

        pauseMenuPanel.gameObject.SetActive(true);
        EventSystem.current.SetSelectedGameObject(firstSelected_PauseMenu);
        /*if(scrollMessagePanel != null)
            scrollMessagePanel.SetActive(true);*/
        isPaused = true;
        Time.timeScale = 0f;
        MakePanelActive(pauseMenuPanel, true);

        canvasGroup_PausedMenu.blocksRaycasts = true;
        canvasGroup_PausedMenu.interactable = true;
    }

    private void UpdateRoguelikeText()
    {
        /*if (RoguelikeModeManager.instance == null) { return; }

        if (currentTimeLimitText != null)
        {
            float totalSeconds = RoguelikeModeManager.instance.timeCount;

            int hours = (int)(totalSeconds / 3600f);
            int minutes = (int)((totalSeconds % 3600f) / 60f);
            int seconds = (int)(totalSeconds % 60f);

            // "D2" ensures 2 digits with leading zeros (e.g., 5 -> "05")
            string formattedTime = $"{hours:D2}:{minutes:D2}:{seconds:D2}";

            currentTimeLimitText.text = $"{formattedTime} / 1:00:00";
        }

        if (killCount != null)
        {
            killCount.text = $"{RoguelikeModeManager.instance.EnemyKillCount}";
        }

        if (gloricCount != null)
        {
            gloricCount.text = $"{RoguelikeModeManager.instance.totalGloric}";
        }

        if (parryCount != null)
        {
            parryCount.text = $"{PlayerController_New.instance.totalParryCount}";
        }*/
    }

    private void ResumeTheGame()
    {
        currentState = PauseGameState.NotActive;

        EventSystem.current.SetSelectedGameObject(null);
        isPaused = false;
        Time.timeScale = 1f;
        StartCoroutine(MakeCanvasGroupActive(false, canvasGroup_Setting));

        canvasGroup_PausedMenu.blocksRaycasts = false;
        canvasGroup_PausedMenu.interactable = false;

        if (SettingManager.instance != null)
        {
            SettingManager.instance.SaveKeyBinding();
        }

        MakePanelActive(pauseMenuPanel, false);
        Invoke("SetActivePauseMenuPanel", 0.2f);
    }

    public void ResumeButton()
    {
        if (isPaused && !isHowToPlayOn)
        {
            isPaused = false;
            Time.timeScale = 1f;
            StartCoroutine(MakeCanvasGroupActive(false, canvasGroup_Setting));

            canvasGroup_PausedMenu.blocksRaycasts = false;
            canvasGroup_PausedMenu.interactable = false;

            if (SettingManager.instance != null)
            {
                SettingManager.instance.SaveKeyBinding();
            }

            pauseMenuPanel.DOScaleX(0, 0.2f).SetEase(Ease.InBack).SetUpdate(true);
        }
    }

    /*public void PauseGameButton()
    {
        if (!isPaused && !isHowToPlayOn)
        {
            isPaused = true;
            Time.timeScale = 0f;
            pauseMenuPanel.DOScaleX(1, 0.2f).SetEase(Ease.OutBack).SetUpdate(true);
            EventSystem.current.SetSelectedGameObject(firstSelected_PauseMenu);
        }
        else if (isPaused && !isHowToPlayOn)
        {
            isPaused = false;
            Time.timeScale = 1f;
            pauseMenuPanel.DOScaleX(0, 0.2f).SetEase(Ease.InBack).SetUpdate(true);
            EventSystem.current.SetSelectedGameObject(null);
        }
    }*/

    private void SetActivePauseMenuPanel()
    {
        pauseMenuPanel.gameObject.SetActive(false);
    }

    /*public void HowToPlay()
    {
        isHowToPlayOn = true;
        howToPlayCanvaGroup.DOFade(1, 0.2f).SetEase(Ease.OutBack).SetUpdate(true);
        howToPlayCanvaGroup.blocksRaycasts = true;
        howToPlayCanvaGroup.interactable = true;
    }*/

    public void BackToPausedMenu()
    {
        isHowToPlayOn = false;
    }

    public void RestartScene()
    {
        StartCoroutine(MakeCanvasGroupActive(false, canvasGroup_Setting));
        MakePanelActive(pauseMenuPanel, false);

        if (isThisRoguelikeMode)
        {
            //ObjectiveManager.instance.SaveHighScore();
        }

        StartCoroutine(ChangeScene(2));
    }

    public void BackToMainMenu()
    {
        StartCoroutine(MakeCanvasGroupActive(false, canvasGroup_Setting));
        MakePanelActive(pauseMenuPanel, false);

        if (isThisRoguelikeMode)
        {
            //ObjectiveManager.instance.SaveHighScore();
        }

        StartCoroutine(ChangeScene(3));
    }

    public IEnumerator ChangeScene(int index) // 0 = StartScene, 1 = NextLevel, 2 = Restart, 3 = MainMenu
    {
        switch (index)
        {
            case 0:
                yield return new WaitForSecondsRealtime(0.3f);
                break;

            case 1:
                AsyncLoaderManager.instance.LoadLevel(SceneManager.GetActiveScene().buildIndex + 1, false);
                //SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex + 1);
                break;

            case 2:
                AsyncLoaderManager.instance.LoadLevel(SceneManager.GetActiveScene().buildIndex, true);
                //SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
                break;

            case 3:

                if (!isThisDemoVersion)
                {
                    //SceneManager.LoadScene("MainMenu");
                    AsyncLoaderManager.instance.LoadLevel("MainMenu", false);
                }    
                else
                {
                    //SceneManager.LoadScene("MainMenu Demo");
                    AsyncLoaderManager.instance.LoadLevel("MainMenu", false);
                }

                break;
        }

        Time.timeScale = 1f;
    }

    private void CanvasGroupActive(CanvasGroup canvas, bool isTrue)
    {
        float alpha;

        if (isTrue) { alpha = 1; }
        else {  alpha = 0; }

        canvas.DOFade(alpha, 0.5f).SetUpdate(true);
        canvas.interactable = isTrue;
        canvas.blocksRaycasts = isTrue;
    }

    private void MakePanelActive(RectTransform panel, bool isTrue)
    {
        if(isTrue)
        {
            panel.DOScaleX(1, 0.2f).SetEase(Ease.OutBack).SetUpdate(true);
        }
        else
        {
            panel.DOScaleX(0, 0.2f).SetEase(Ease.InBack).SetUpdate(true);
        }
    }

    private void SetUpButton()
    {
        button_Resume.onClick.AddListener(() => {
            ResumeTheGame();
        });

        button_Restart.onClick.AddListener(() => {
            MakePanelActive(restartConfirmPanel, true);
            currentState = PauseGameState.RestartConfirm;
            EventSystem.current.SetSelectedGameObject(button_CancelRestart.gameObject);
        });

        button_MainMenu.onClick.AddListener(() => {
            MakePanelActive(mainMenuConfirmPanel, true);
            currentState = PauseGameState.MainMenuConfirm;
            EventSystem.current.SetSelectedGameObject(button_CancelMainMenu.gameObject);
        });

        button_ConfirmRestart.onClick.AddListener(() => {
            RestartScene();
        });

        button_CancelRestart.onClick.AddListener(() => {
            MakePanelActive(restartConfirmPanel, false);
            currentState = PauseGameState.Pause;
            EventSystem.current.SetSelectedGameObject(button_Resume.gameObject);
        });

        button_ConfirmMainMenu.onClick.AddListener(() => {
            BackToMainMenu();
        });

        button_CancelMainMenu.onClick.AddListener(() => {
            MakePanelActive(mainMenuConfirmPanel, false);
            currentState = PauseGameState.Pause;
            EventSystem.current.SetSelectedGameObject(button_Resume.gameObject);
        });

        if(button_Setting != null)
        {
            button_Setting.onClick.AddListener(() => {
                SettingManager.instance.isSettingActive = true;
                StartCoroutine(MakeCanvasGroupActive(true, canvasGroup_Setting));
            });
        }

        button_ReturnFromSettingToPauseMenu.onClick.AddListener(() => {
            SettingManager.instance.isSettingActive = false;
            StartCoroutine(MakeCanvasGroupActive(false, canvasGroup_Setting));
        });
    }

    private IEnumerator MakeCanvasGroupActive(bool isActive, CanvasGroup canvasGroup)
    {
        if(isActive)
        {
            canvasGroup.DOFade(1, 0.2f).SetUpdate(true);
            yield return new WaitForSecondsRealtime(0.2f);
            canvasGroup.blocksRaycasts = true;
            canvasGroup.interactable = true;
        }
        else
        {
            canvasGroup.DOFade(0, 0.2f).SetUpdate(true);
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }
    }

    private void SetUpUI()
    {
        pauseMenuPanel.localScale = new Vector3(0, 1, 1);
        restartConfirmPanel.localScale = new Vector3(0, 1, 1);
        mainMenuConfirmPanel.localScale = new Vector3(0, 1, 1);

        if (canvasGroup_PausedMenu != null)
        {
            canvasGroup_PausedMenu.blocksRaycasts = false;
            canvasGroup_PausedMenu.interactable = false;
        }

        if (canvasGroup_BlackFade != null)
        {
            canvasGroup_BlackFade.alpha = 0f;
            canvasGroup_BlackFade.blocksRaycasts = false;
            canvasGroup_BlackFade.interactable = false;
        }

        if (canvasGroup_Setting != null)
        {
            canvasGroup_Setting.alpha = 0f;
            canvasGroup_Setting.blocksRaycasts = false;
            canvasGroup_Setting.interactable = false;
        }
    }
}
