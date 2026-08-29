using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public enum UINavigtionMode
{
    Pointer,
    Select
}

public enum GameDifficultyState
{
    Easy,
    Medium,
    Hard,
    Impossible
};


public class GameManager : MonoBehaviour
{
    [SerializeField] private PlayerInputActions playerInputActions;
    public EventSystem eventSystem;

    public UINavigtionMode navigationMode; 

    public GameDifficultyState gameDifficultyState;
    public int gameDifficultyIndex = 1;
    public static GameManager instance;

    [HideInInspector] public float enemyAnimationSpeed = 1f;
    [HideInInspector] public float enemyMoveSpeed = 1f;
    [HideInInspector] public float enemyAttackSpeed = 1f;
    [HideInInspector] public float playerTakeDamage = 1f;
    [HideInInspector] public int goldGained = 0;

    public int powerUpIndex_MaxHealth = 0;
    public int powerUpIndex_RegenHealth = 0;
    public int powerUpIndex_FireRate = 0;
    public int powerUpIndex_FirePower = 0;
    public int powerUpIndex_MaxStamina = 0;
    public int powerUpIndex_MoveSpeed = 0;
    public int powerUpIndex_MagazineCapacity = 0;
    public int powerUpIndex_MaxUltimate = 0;

    public int powerUpIndexCalculate_MaxHealth = 0;
    public int powerUpIndexCalculate_RegenHealth = 0;
    public int powerUpIndexCalculate_FireRate = 0;
    public int powerUpIndexCalculate_FirePower = 0;
    public int powerUpIndexCalculate_MaxStamina = 0;
    public int powerUpIndexCalculate_MoveSpeed = 0;
    public int powerUpIndexCalculate_MagazineCapacity = 0;
    public int powerUpIndexCalculate_MaxUltimate = 0;

    public float enemyHealthMultiplier = 1f;
    public float enemyAttackPowerMultiplier = 1f;

    public float playerBaseMaxHealth = 100f;
    public float playerMaxHealth = 100f;
    public float playerMaxStamina = 100f;
    public float playerBaseMaxUltimate = 100f;
    public float playerMaxUltimate = 100f;
    public float playerCurrentUltimate = 0f;

    public float playerMaxHealthMultiplier = 1f;
    public float playerStaminaCostMultiplier = 1f;
    public float playerAttackPowerMultiplier = 1f;
    public float playerAttackSpeedMultiplier = 1f;
    public float playerMoveSpeedMultiplier = 1f;
    public float playerMaxUltimateMultiplier = 1f;

    public float enemyHealthMultiplier_Curse = 0.5f;
    public float enemyAttackPowerMultiplier_Curse = 0.5f;
    public float enemyMoveSpeedMultiplier_Curse = 1f;

    public bool isFlashLightUltimateIsUnlock = false;
    public bool isInvisibleUltimateIsUnlock = false;
    public float flashLightUltimateDuration = 10f;
    public float invisibleUltimateDuration = 4f;

    public float kickDamage;
    public bool isMagicBodyShield = false;

    private void OnDisable()
    {
        this.enabled = true;
    }

    private void Awake()
    {
        GetDifficulty();

        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(instance);
        }

        playerInputActions = GetComponent<PlayerInputActions>();
        OnControlsChanged(playerInputActions);
    }

    private void Start()
    {
    }

    public void OnControlsChanged(PlayerInputActions input)
    {
        string currentScheme = input.controlSchemes.ToString();
        Debug.LogError($"CurrentScheme : " + currentScheme);
        // 

        if (currentScheme == "Gamepad") // Use the exact name of your Gamepad scheme
        {
            // --- Enable Controller Navigation ---

            // 2. Force Select a UI Element
            // This starts the gamepad navigation chain.
            if (eventSystem != null && eventSystem.firstSelectedGameObject != null)
            {
                eventSystem.SetSelectedGameObject(eventSystem.firstSelectedGameObject);
            }
        }
        else if (currentScheme == "Keyboard&Mouse") // Assumes "Keyboard & Mouse" or any other non-Gamepad scheme
        {
            // --- Disable Controller Navigation (Enable Mouse/KBM) --

            // 2. Clear the UI Selection
            // This stops the highlight from being stuck on a button after switching.
            if (eventSystem != null)
            {
                eventSystem.SetSelectedGameObject(null);
            }
        }
    }


    private void Update()
    {
        if (eventSystem == null)
        {
            eventSystem = EventSystem.current;
        }

        if (InputSchemeManager.instance != null)
        {
            if (InputSchemeManager.instance.IsAnyMouseInputActive())
            {
                navigationMode = UINavigtionMode.Pointer;
            }
            else if (InputSchemeManager.instance.IsGamepadActive() || InputSchemeManager.instance.IsAnyKeyOnKeyboardPressed())
            {
                navigationMode = UINavigtionMode.Select;
            }
        }
        
        /*if (PlayerController.instance != null && EquipmentManager.instance != null)
        {
            float finalHealth = playerBaseMaxHealth * playerMaxHealthMultiplier * EquipmentManager.instance.totalMultiplier_Health + EquipmentManager.instance.totalHealthIncrease;

            if(UpgradeCharacterCardManager.instance != null)
            {
                finalHealth *= UpgradeCharacterCardManager.instance.playerMaxHealth;
            }

            PlayerController.instance.CharacterHealthComponent.SetMaxHP(finalHealth);
            PlayerController.instance.CharacterUltimateComponent.SetMaxUltimate(playerBaseMaxUltimate * playerMaxUltimateMultiplier);

            playerMaxHealth = playerBaseMaxHealth * playerMaxHealthMultiplier;
        }

        if (Input.GetKey(KeyCode.LeftShift) && Input.GetKey(KeyCode.K))
        {
            if(Input.GetKeyDown(KeyCode.L))
            {
                AsyncLoaderManager.instance.LoadLevel(SceneManager.GetActiveScene().buildIndex, false);
                SaveSystem.ResetAllData();
            }

            if (Input.GetKeyDown(KeyCode.J))
            {
                AsyncLoaderManager.instance.LoadLevel(SceneManager.GetActiveScene().buildIndex, false);
                SaveSystem.ResetTutorialData();
            }

            if (Input.GetKeyDown(KeyCode.M))
            {
                AsyncLoaderManager.instance.LoadLevel(SceneManager.GetActiveScene().buildIndex, false);
                SaveSystem.ResetBlueprintData();
            }
        }*/
    }

    /*public bool IsEnemyUnlocked(string enemyID)
    {
        return enemyUnlockData.IsUnlocked(enemyID);
    }

    public bool IsItemUnlocked(string weaponID)
    {
        return itemUnlockData.IsUnlocked(weaponID);
    }

    public bool IsExtraModeUnlocked(string extraModeID)
    {
        return extraModeUnlockData.IsUnlocked(extraModeID);
    }*/

    public void AdjustDifficulty(int index)
    {
        PlayerPrefs.SetInt("difficultyIndex", index);
    }

    public void GetDifficulty()
    {
        gameDifficultyIndex = PlayerPrefs.GetInt("difficultyIndex");

        switch (gameDifficultyIndex)
        {
            case 0:
                {
                    gameDifficultyState = GameDifficultyState.Easy;

                    playerTakeDamage = 0.1f;
                    enemyAnimationSpeed = 0.75f;
                    enemyMoveSpeed = 0.75f;
                    enemyAttackSpeed = 1.25f;
                    goldGained = 2;
                }
                break;

            case 1:
                {
                    gameDifficultyState = GameDifficultyState.Medium;

                    playerTakeDamage = 0.15f;
                    enemyAnimationSpeed = 1f;
                    enemyMoveSpeed = 1f;
                    enemyAttackSpeed = 1f;
                    goldGained = 1;
                }
                break;

            case 2:
                {
                    gameDifficultyState = GameDifficultyState.Hard;

                    playerTakeDamage = 0.25f;
                    enemyAnimationSpeed = 1.15f;
                    enemyMoveSpeed = 1.15f;
                    enemyAttackSpeed = 0.85f;
                    goldGained = 0;
                }
                break;

            case 3:
                {
                    gameDifficultyState = GameDifficultyState.Impossible;

                    playerTakeDamage = 0.35f;
                    enemyAnimationSpeed = 1.3f;
                    enemyMoveSpeed = 1.3f;
                    enemyAttackSpeed = 0.7f;
                    goldGained = 0;
                }
                break;
        }
    }
}
