using DG.Tweening;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public enum MainmenuState
{
    None,
    MainMenu,
    Start,
    Setting,
    Extras,
    Credit,
    Quit,
    Difficulty
}

[System.Serializable]
public class GifSpriteCharacter
{
    public string str_CharacterName;
    public Image image_Character;
    public List<Sprite> sprites_Character;
    public int currentSpriteIndex = 0;
}

[System.Serializable]
public class MenuElement
{
    public RectTransform rectMenuElement;
    public RectTransform rectTransform_Show;
    public RectTransform rectTransform_Hide;
}

public class MainMenu : MonoBehaviour
{
    [SerializeField] private CanvasGroup canvasGroup_AllElements;
    [SerializeField] private MainmenuState currentState;
    [SerializeField] private List<GifSpriteCharacter> characterGifs;

    [SerializeField] private Button button_Start;
    [SerializeField] private Button button_GameplayLoop;
    [SerializeField] private Button button_Setting;
    [SerializeField] private Button button_Extras;
    [SerializeField] private Button button_Credit;
    [SerializeField] private Button button_ExitGame;

    [SerializeField] private Button button_ConfirmExit;
    [SerializeField] private Button button_CancelExit;

    [SerializeField] private Button button_TutorialScene;
    [SerializeField] private Button button_RoguelikeDemoScene;
    [SerializeField] private Button button_BackToMainMenu_StartGame;

    [SerializeField] private Button button_Difficulty_Assisted;
    [SerializeField] private Button button_Difficulty_Standard;
    [SerializeField] private Button button_Difficulty_Hardcore;
    [SerializeField] private Button button_Difficulty_SamuelMustDie;
    [SerializeField] private Button button_BackToStartGame_Difficulty;

    [SerializeField] private Button button_BackToMainMenu_Setting;

    [SerializeField] private CanvasGroup canvasGroup_BlackfadeUI;

    [SerializeField] private List<MenuElement> menuElement_MainMenu;
    [SerializeField] private List<MenuElement> menuElement_GamePanel;
    [SerializeField] private List<MenuElement> menuElement_Characters;
    [SerializeField] private GameObject firstSelectedGameObject_MainMenu;
    [SerializeField] private CanvasGroup canvasGroup_MainMenu;

    [SerializeField] private List<MenuElement> menuElement_Start;
    [SerializeField] private GameObject firstSelectedGameObject_Start;
    [SerializeField] private CanvasGroup canvasGroup_Start;

    [SerializeField] private List<MenuElement> menuElement_Setting;
    [SerializeField] private GameObject firstSelectedGameObject_Setting;
    [SerializeField] private CanvasGroup canvasGroup_Setting;

    [SerializeField] private List<MenuElement> menuElement_Extras;
    [SerializeField] private GameObject firstSelectedGameObject_Extras;
    [SerializeField] private CanvasGroup canvasGroup_Extras;

    [SerializeField] private List<MenuElement> menuElement_Difficulty;
    [SerializeField] private GameObject firstSelectedGameObject_Difficulty;
    [SerializeField] private CanvasGroup canvasGroup_Difficulty;

    [SerializeField] private List<MenuElement> menuElement_Quit;
    [SerializeField] private List<MenuElement> menuElement_SamuelSad;
    [SerializeField] private GameObject firstSelectedGameObject_Quit;
    [SerializeField] private CanvasGroup canvasGroup_Quit;

    private string sceneNameToLoad = "";
    private CanvasGroup currentCanvasGroup;
    private bool isPlayingAnimation = false;
    private float timer = 0f;

    public static MainMenu instance; 

    private void Awake()
    {
        instance = this;

        SetUpUIs();
        SetUpButton();
    }

    void Start()
    {
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        canvasGroup_BlackfadeUI.DOFade(0f, 0.5f).SetUpdate(true);
        StartCoroutine(SetCanvasGroupActive(canvasGroup_MainMenu, 1.5f));
        StartCoroutine(SetMenuElementActive(menuElement_Characters, true, 1f));
        StartCoroutine(SetMenuElementActive(menuElement_GamePanel, true, 1f));
        StartCoroutine(SetMenuElementActive(menuElement_MainMenu, true, 1.25f, MainmenuState.MainMenu));
    }

    void Update()
    {
        PlayAnimationOnImage();
        HandleState();
        HandleButton();
    }

    private void HandleButton()
    {
        InputSchemeManager.instance.MakeButtonInteracable(button_Start, (currentState == MainmenuState.MainMenu));
        InputSchemeManager.instance.MakeButtonInteracable(button_GameplayLoop, (currentState == MainmenuState.MainMenu));
        InputSchemeManager.instance.MakeButtonInteracable(button_Setting, (currentState == MainmenuState.MainMenu));
        InputSchemeManager.instance.MakeButtonInteracable(button_Extras, (currentState == MainmenuState.MainMenu));
        InputSchemeManager.instance.MakeButtonInteracable(button_Credit, (currentState == MainmenuState.MainMenu));
        InputSchemeManager.instance.MakeButtonInteracable(button_ExitGame, (currentState == MainmenuState.MainMenu));
    }

    private void HandleState()
    {
        if (GameManager.instance.navigationMode == UINavigtionMode.Select)
        {
            if (EventSystem.current.currentSelectedGameObject != null) { return; }

            GameObject firstSelectedGameObject = null;

            switch (currentState)
            {
                case MainmenuState.MainMenu: firstSelectedGameObject = firstSelectedGameObject_MainMenu; break;
                case MainmenuState.Start: firstSelectedGameObject = firstSelectedGameObject_Start; break;
                case MainmenuState.Setting: firstSelectedGameObject = firstSelectedGameObject_Setting; break;
                case MainmenuState.Credit: firstSelectedGameObject = null; break;
                case MainmenuState.Extras: firstSelectedGameObject = firstSelectedGameObject_Extras; break;
                case MainmenuState.Quit: firstSelectedGameObject = firstSelectedGameObject_Quit; break;
                case MainmenuState.Difficulty: firstSelectedGameObject = firstSelectedGameObject_Difficulty; break;
            }

            if (firstSelectedGameObject != null && EventSystem.current.currentSelectedGameObject != firstSelectedGameObject)
            {
                EventSystem.current.SetSelectedGameObject(firstSelectedGameObject);
            }
        }
    }

    public void CreditToMainMenu()
    {
        StartCoroutine(SetCanvasGroupActive(canvasGroup_MainMenu, 1.5f));
        StartCoroutine(SetMenuElementActive(menuElement_Characters, true, 1f));
        StartCoroutine(SetMenuElementActive(menuElement_GamePanel, true, 1f));
        StartCoroutine(SetMenuElementActive(menuElement_MainMenu, true, 1.25f, MainmenuState.MainMenu));
    }

    private void SetUpButton()
    {
        button_Start.onClick.AddListener(() => {
            StartCoroutine(SetCanvasGroupActive(canvasGroup_Start, 0.525f));
            StartCoroutine(SetMenuElementActive(menuElement_MainMenu, false));
            StartCoroutine(SetMenuElementActive(menuElement_Start, true, 0.375f, MainmenuState.Start));
        });

        button_GameplayLoop.onClick.AddListener(() => {
            canvasGroup_AllElements.DOFade(0f, 0.5f).SetUpdate(true);
            AsyncLoaderManager.instance.LoadLevel("GameplayLoop", false);
        });

        button_Credit.onClick.AddListener(() => {
            //StartCoroutine(CreditManager.instance.PlayCredit(0.375f));
            StartCoroutine(SetMenuElementActive(menuElement_MainMenu, false));
            StartCoroutine(SetMenuElementActive(menuElement_Characters, false));
            currentState = MainmenuState.Credit;
        });

        button_ExitGame.onClick.AddListener(() => {
            StartCoroutine(SetCanvasGroupActive(canvasGroup_Quit, 0.525f));
            StartCoroutine(SetMenuElementActive(menuElement_MainMenu, false));
            StartCoroutine(SetMenuElementActive(menuElement_Characters, false));
            StartCoroutine(SetMenuElementActive(menuElement_SamuelSad, true, 0.375f));
            StartCoroutine(SetMenuElementActive(menuElement_Quit, true, 0.375f, MainmenuState.Quit));
        });

        button_Setting.onClick.AddListener(() => {

            SettingManager.instance.isSettingActive = true;

            StartCoroutine(SetCanvasGroupActive(canvasGroup_Setting, 0.45f));
            StartCoroutine(SetMenuElementActive(menuElement_MainMenu, false));
            StartCoroutine(SetMenuElementActive(menuElement_Characters, false));
            StartCoroutine(SetMenuElementActive(menuElement_GamePanel, false));
            StartCoroutine(SetMenuElementActive(menuElement_Setting, true, 0.375f, MainmenuState.Setting));
        });

        button_CancelExit.onClick.AddListener(() => {
            StartCoroutine(SetCanvasGroupActive(canvasGroup_MainMenu, 0.525f));
            StartCoroutine(SetMenuElementActive(menuElement_Quit, false));
            StartCoroutine(SetMenuElementActive(menuElement_SamuelSad, false));
            StartCoroutine(SetMenuElementActive(menuElement_Characters, true, 0.15f));
            StartCoroutine(SetMenuElementActive(menuElement_MainMenu, true, 0.15f, MainmenuState.MainMenu));
        });

        button_BackToMainMenu_StartGame.onClick.AddListener(() => {
            StartCoroutine(SetCanvasGroupActive(canvasGroup_MainMenu, 0.6f));
            StartCoroutine(SetMenuElementActive(menuElement_Start, false));
            StartCoroutine(SetMenuElementActive(menuElement_MainMenu, true, 0.225f, MainmenuState.MainMenu));
        });

        button_BackToMainMenu_Setting.onClick.AddListener(() => {

            SettingManager.instance.isSettingActive = false;

            StartCoroutine(SetCanvasGroupActive(canvasGroup_MainMenu, 0.3f));
            StartCoroutine(SetMenuElementActive(menuElement_Setting, false));
            StartCoroutine(SetMenuElementActive(menuElement_GamePanel, true, 0.075f));
            StartCoroutine(SetMenuElementActive(menuElement_Characters, true, 0.075f));
            StartCoroutine(SetMenuElementActive(menuElement_MainMenu, true, 0.150f, MainmenuState.MainMenu));
        });

        button_ConfirmExit.onClick.AddListener(() => {
            canvasGroup_Quit.interactable = false;
            canvasGroup_Quit.blocksRaycasts = false;

            canvasGroup_BlackfadeUI.DOFade(1f, 0.5f).SetUpdate(true);
            StartCoroutine(QuitGame());
        });

        button_TutorialScene.onClick.AddListener(() => {
            canvasGroup_Start.interactable = false;
            canvasGroup_Start.blocksRaycasts = false;

            StartCoroutine(SetMenuElementActive(menuElement_Start, false));
            StartCoroutine(SetCanvasGroupActive(canvasGroup_Difficulty, 0.450f));
            StartCoroutine(SetMenuElementActive(menuElement_Difficulty, true, 0.450f, MainmenuState.Difficulty));

            sceneNameToLoad = "TutorialScene";
        });

        button_RoguelikeDemoScene.onClick.AddListener(() => {
            canvasGroup_Start.interactable = false;
            canvasGroup_Start.blocksRaycasts = false;

            StartCoroutine(SetMenuElementActive(menuElement_Start, false));
            StartCoroutine(SetCanvasGroupActive(canvasGroup_Difficulty, 0.450f));
            StartCoroutine(SetMenuElementActive(menuElement_Difficulty, true, 0.450f, MainmenuState.Difficulty));

            sceneNameToLoad = "RoguelikeScene";
        });

        button_Difficulty_Assisted.onClick.AddListener(() => {
            GameManager.instance.AdjustDifficulty(0);
            canvasGroup_Difficulty.interactable = false;
            canvasGroup_Difficulty.blocksRaycasts = false;
            canvasGroup_AllElements.DOFade(0f, 0.5f).SetUpdate(true);
            AsyncLoaderManager.instance.LoadLevel(sceneNameToLoad, true);
        });

        button_Difficulty_Standard.onClick.AddListener(() => {
            GameManager.instance.AdjustDifficulty(1);
            canvasGroup_Difficulty.interactable = false;
            canvasGroup_Difficulty.blocksRaycasts = false;
            canvasGroup_AllElements.DOFade(0f, 0.5f).SetUpdate(true);
            AsyncLoaderManager.instance.LoadLevel(sceneNameToLoad, true);
        });

        button_Difficulty_Hardcore.onClick.AddListener(() => {
            GameManager.instance.AdjustDifficulty(2);
            canvasGroup_Difficulty.interactable = false;
            canvasGroup_Difficulty.blocksRaycasts = false;
            canvasGroup_AllElements.DOFade(0f, 0.5f).SetUpdate(true);
            AsyncLoaderManager.instance.LoadLevel(sceneNameToLoad, true);
        });

        button_Difficulty_SamuelMustDie.onClick.AddListener(() => {
            GameManager.instance.AdjustDifficulty(3);
            canvasGroup_Difficulty.interactable = false;
            canvasGroup_Difficulty.blocksRaycasts = false;
            canvasGroup_AllElements.DOFade(0f, 0.5f).SetUpdate(true);
            AsyncLoaderManager.instance.LoadLevel(sceneNameToLoad, true);
        });

        button_BackToStartGame_Difficulty.onClick.AddListener(() => {
            canvasGroup_Difficulty.interactable = false;
            canvasGroup_Difficulty.blocksRaycasts = false;

            StartCoroutine(SetMenuElementActive(menuElement_Difficulty, false));
            StartCoroutine(SetCanvasGroupActive(canvasGroup_Start, 0.450f));
            StartCoroutine(SetMenuElementActive(menuElement_Start, true, 0.450f, MainmenuState.Start));
        });
    }

    private IEnumerator QuitGame()
    {
        yield return new WaitForSecondsRealtime(2f);
        Application.Quit();
    }

    private IEnumerator SetCanvasGroupActive(CanvasGroup newCannasGroup, float delay = 0f)
    {
        if(currentCanvasGroup != null)
        {
            currentCanvasGroup.interactable = false;
            currentCanvasGroup.blocksRaycasts = false;
        }
        //currentCanvasGroup.DOFade(0f, 0.25f).SetUpdate(true);
        //yield return new WaitForSecondsRealtime(0.25f);

        //newCannasGroup.DOFade(1f, 0.5f).SetUpdate(true);
        yield return new WaitForSecondsRealtime(delay);
        newCannasGroup.interactable = true;
        newCannasGroup.blocksRaycasts = true;

        currentCanvasGroup = newCannasGroup;
    }

    private IEnumerator SetMenuElementActive(List<MenuElement> menuElements, bool isActive, float delay = 0f, MainmenuState state = MainmenuState.None)
    {
        yield return new WaitForSecondsRealtime(delay);

        foreach (MenuElement m in menuElements)
        {
            RectTransform targetRectTransform;

            if (isActive)
            {
                targetRectTransform = m.rectTransform_Show;
            }
            else
            {
                targetRectTransform = m.rectTransform_Hide;
            }

            m.rectMenuElement.DOMoveX(targetRectTransform.position.x, 0.5f).SetUpdate(true);
            yield return new WaitForSecondsRealtime(0.075f);
        }

        if(state != MainmenuState.None)
        currentState = state;
    }

    private void PlayAnimationOnImage()
    {
        //if (!isPlayingAnimation) { return; }

        timer += Time.unscaledDeltaTime;

        if (timer >= 1f / 15f)
        {
            timer = 0f;

            foreach(GifSpriteCharacter gif in characterGifs)
            {
                gif.currentSpriteIndex++;

                if(gif.currentSpriteIndex >= gif.sprites_Character.Count)
                {
                    gif.currentSpriteIndex = 0;
                }

                gif.image_Character.sprite = gif.sprites_Character[gif.currentSpriteIndex];
            }
        }
    }

    private void SetUpUIs()
    {
        canvasGroup_BlackfadeUI.alpha = 1f;
        canvasGroup_MainMenu.alpha = 1f;
        canvasGroup_Start.alpha = 1f;
        canvasGroup_Setting.alpha = 1f;
        //canvasGroup_Extras.alpha = 1f;
        //canvasGroup_Credit.alpha = 1f;
        canvasGroup_Quit.alpha = 1f;

        HideUIOnStart(menuElement_MainMenu);
        HideUIOnStart(menuElement_Start);
        HideUIOnStart(menuElement_GamePanel);
        HideUIOnStart(menuElement_Characters);
        HideUIOnStart(menuElement_SamuelSad);
        HideUIOnStart(menuElement_Setting);
        //HideUIOnStart(menuElement_Extras);
        //HideUIOnStart(menuElement_Credit);
        HideUIOnStart(menuElement_Quit);
        HideUIOnStart(menuElement_Difficulty);
    }

    private void HideUIOnStart(List<MenuElement> menuElements)
    {
        foreach (MenuElement m in menuElements)
        {
            m.rectMenuElement.position = new Vector3(m.rectTransform_Hide.position.x, m.rectMenuElement.position.y, m.rectMenuElement.position.z);
        }
    }
}
