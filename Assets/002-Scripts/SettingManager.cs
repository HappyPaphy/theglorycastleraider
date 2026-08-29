using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[System.Serializable]
public class ResItem
{
    public int horizontal, vertical;
}

[System.Serializable]
public class QualityItem
{
    public string quality;
}

public class SettingManager : MonoBehaviour
{
    [SerializeField] private GameObject screenSetting;
    [SerializeField] private GameObject soundSetting;
    [SerializeField] private GameObject languageSetting;
    [SerializeField] private GameObject rebindSetting;

    [SerializeField] private Button button_ScreenSetting;
    [SerializeField] private Button button_SoundSetting;
    [SerializeField] private Button button_LanguageSetting;
    [SerializeField] private Button button_RebindSetting;

    [SerializeField] private Button button_ResLeft;
    [SerializeField] private Button button_ResRight;
    [SerializeField] private Button button_QuaLeft;
    [SerializeField] private Button button_QuaRight;
    [SerializeField] private Button button_ApplyGraphic;

    public Toggle fullScreenTog, vsyncTog;
    public List<ResItem> resolutions = new List<ResItem>();
    public List<QualityItem> qualities = new List<QualityItem>();
    private int selectedResolution;
    private int selectedQuality = 5;

    public TMPro.TMP_Text resolutionLabel;
    public TMPro.TMP_Text qualityLabel;

    private int currentPageIndex = 0;
    private GameObject[] settingPanels;
    private Button[] buttonPanels;

    [SerializeField] private GameObject xboxSwitchTab;
    [SerializeField] private GameObject playstationSwitchTab;

    [SerializeField] private bool isLanguageChangable = false;
    [HideInInspector] public bool isSettingActive = false;

    public static SettingManager instance;

    private void Awake()
    {
        instance = this;
        SetUpButtons();

        if(isLanguageChangable)
        {
            settingPanels = new GameObject[] { screenSetting, rebindSetting, soundSetting, languageSetting };
            buttonPanels = new Button[] { button_ScreenSetting, button_RebindSetting, button_SoundSetting, button_LanguageSetting };
        }
        else
        {
            settingPanels = new GameObject[] { screenSetting, rebindSetting, soundSetting };
            buttonPanels = new Button[] { button_ScreenSetting, button_RebindSetting, button_SoundSetting };
        }
    }

    void Start()
    {
        isSettingActive = false;

        LoadKeyBinding();

        screenSetting.SetActive(true);
        soundSetting.SetActive(false);
        languageSetting.SetActive(false);
        rebindSetting.SetActive(false);

        fullScreenTog.isOn = Screen.fullScreen;
        QualitySettings.SetQualityLevel(selectedQuality);

        if (QualitySettings.vSyncCount == 0)
        {
            vsyncTog.isOn = false;
        }
        else
        {
            vsyncTog.isOn = true;
        }

        bool foundRes = false;
        for (int i = 0; i < resolutions.Count; i++)
        {
            if (Screen.width == resolutions[i].horizontal && Screen.height == resolutions[i].vertical)
            {
                foundRes = true;

                selectedResolution = i;

                UpdateResLabel();
            }
        }
    }

    void Update()
    {
        HandleGamepadTabNavigation();
    }

    private void HandleGamepadTabNavigation()
    {
        if(!isSettingActive) { return; }

        if (EventSystem.current.currentSelectedGameObject == buttonPanels[0].gameObject)
        {
            currentPageIndex = 0;
        }
        else if (EventSystem.current.currentSelectedGameObject == buttonPanels[1].gameObject)
        {
            currentPageIndex = 1;
        }
        else if (EventSystem.current.currentSelectedGameObject == buttonPanels[2].gameObject)
        {
            currentPageIndex = 2;
        }
        else if (isLanguageChangable)
        {
            if(EventSystem.current.currentSelectedGameObject == buttonPanels[3].gameObject)
            {
                currentPageIndex = 3;
            }
        }

        if(InputSchemeManager.instance.CurrentInputMode == InputMode.Xbox)
        {
            if(!xboxSwitchTab.activeInHierarchy)
            {
                xboxSwitchTab.SetActive(true);
                playstationSwitchTab.SetActive(false);
            }
        }
        else if (InputSchemeManager.instance.CurrentInputMode == InputMode.PlayStation)
        {
            if (!playstationSwitchTab.activeInHierarchy)
            {
                playstationSwitchTab.SetActive(true);
                xboxSwitchTab.SetActive(false);
            }
        }

        if (InputSchemeManager.instance.CurrentInputMode == InputMode.PC)
        {
            if (playstationSwitchTab.activeInHierarchy || xboxSwitchTab.activeInHierarchy)
            {
                playstationSwitchTab.SetActive(false);
                xboxSwitchTab.SetActive(false);
            }

            return;
        }

        // Get the current active gamepad
        Gamepad gamepad = Gamepad.current;
        if (gamepad == null) return;

        // Check for LB / L1 press
        if (gamepad.leftShoulder.wasPressedThisFrame)
        {
            ChangeTab(-1);
        }
        // Check for RB / R1 press
        else if (gamepad.rightShoulder.wasPressedThisFrame)
        {
            ChangeTab(1);
        }
    }

    private void ChangeTab(int direction)
    {
        currentPageIndex += direction;

        // Loop around if indexing out of bounds
        if (currentPageIndex < 0)
        {
            currentPageIndex = settingPanels.Length - 1;
        }
        else if (currentPageIndex >= settingPanels.Length)
        {
            currentPageIndex = 0;
        }

        SwitchTab(currentPageIndex);
    }

    private void SwitchTab(int index)
    {
        currentPageIndex = index;

        // Enable the selected panel and disable all others
        for (int i = 0; i < settingPanels.Length; i++)
        {
            if (settingPanels[i] != null)
            {
                settingPanels[i].SetActive(i == index);

                if(i == index)
                {
                    EventSystem.current.SetSelectedGameObject(buttonPanels[i].gameObject);
                }
            }
        }

        // Optional: Auto-select the first interactive button/UI element of the new panel 
        // using UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject()
        // so gamepad users can immediately navigate the contents of the new tab.
    }

    private void ResLeft()
    {
        selectedResolution--;

        if (selectedResolution < 0)
        {
            selectedResolution = 0;
        }

        UpdateResLabel();
    }

    private void ResRight()
    {
        selectedResolution++;

        if (selectedResolution > resolutions.Count - 1)
        {
            selectedResolution = resolutions.Count - 1;
        }

        UpdateResLabel();
    }

    private void QuaLeft()
    {
        selectedQuality--;

        if (selectedQuality < 0)
        {
            selectedQuality = 0;
        }

        UpdateQuaLabel();
    }

    private void QuaRight()
    {
        selectedQuality++;

        if (selectedQuality > qualities.Count - 1)
        {
            selectedQuality = qualities.Count - 1;
        }

        UpdateQuaLabel();
    }

    private void UpdateResLabel()
    {
        resolutionLabel.text = resolutions[selectedResolution].horizontal.ToString() + " x " + resolutions[selectedResolution].vertical.ToString();
    }

    private void UpdateQuaLabel()
    {
        qualityLabel.text = qualities[selectedQuality].quality.ToString();
    }

    private void ApplyGraphics()
    {
        Screen.fullScreen = fullScreenTog.isOn;

        if (vsyncTog.isOn)
        {
            QualitySettings.vSyncCount = 1;
        }
        else
        {
            QualitySettings.vSyncCount = 0;
        }

        Screen.SetResolution(resolutions[selectedResolution].horizontal, resolutions[selectedResolution].vertical, fullScreenTog.isOn);
        QualitySettings.SetQualityLevel(selectedQuality);
    }

    public void SaveKeyBinding()
    {
        if (RebindSaveLoad.instance != null)
        {
            RebindSaveLoad.instance.Save();
        }
    }

    public void LoadKeyBinding()
    {
        if(RebindSaveLoad.instance != null)
        {
            RebindSaveLoad.instance.Load();
        }
    }

    private void SetUpButtons()
    {
        button_ScreenSetting.onClick.AddListener(() => {
            currentPageIndex = 0;
            SwitchTab(0);
        });

        button_SoundSetting.onClick.AddListener(() => {
            currentPageIndex = 2;
            SwitchTab(2);
        });

        button_LanguageSetting.onClick.AddListener(() => {
            currentPageIndex = 3;
            SwitchTab(3);
        });

        button_RebindSetting.onClick.AddListener(() => {
            currentPageIndex = 1;
            SwitchTab(1);
        });

        button_ResLeft.onClick.AddListener(() => {
            ResLeft();
        });

        button_ResRight.onClick.AddListener(() => {
            ResRight();
        });

        button_QuaLeft.onClick.AddListener(() => {
            QuaLeft();
        });

        button_QuaRight.onClick.AddListener(() => {
            QuaRight();
        });

        button_ApplyGraphic.onClick.AddListener(() => {
            ApplyGraphics();
        });
    }
}
