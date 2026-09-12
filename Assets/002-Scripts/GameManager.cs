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
    [HideInInspector] public float playerTakeHPDamage = 1f;
    [HideInInspector] public float playerTakeStaminaDamage = 1f;
    [HideInInspector] public int goldGained = 0;


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

                    playerTakeHPDamage = 0.5f;
                    playerTakeStaminaDamage = 0.5f;
                    enemyAnimationSpeed = 0.75f;
                    enemyMoveSpeed = 0.75f;
                    enemyAttackSpeed = 1.25f;
                    goldGained = 2;
                }
                break;

            case 1:
                {
                    gameDifficultyState = GameDifficultyState.Medium;

                    playerTakeHPDamage = 1f;
                    playerTakeStaminaDamage = 1f;
                    enemyAnimationSpeed = 1f;
                    enemyMoveSpeed = 1f;
                    enemyAttackSpeed = 1f;
                    goldGained = 1;
                }
                break;

            case 2:
                {
                    gameDifficultyState = GameDifficultyState.Hard;

                    playerTakeHPDamage = 1.5f;
                    playerTakeStaminaDamage = 1.5f;
                    enemyAnimationSpeed = 1.15f;
                    enemyMoveSpeed = 1.15f;
                    enemyAttackSpeed = 0.85f;
                    goldGained = 0;
                }
                break;

            case 3:
                {
                    gameDifficultyState = GameDifficultyState.Impossible;

                    playerTakeHPDamage = 2f;
                    playerTakeStaminaDamage = 2f;
                    enemyAnimationSpeed = 1.3f;
                    enemyMoveSpeed = 1.3f;
                    enemyAttackSpeed = 0.7f;
                    goldGained = 0;
                }
                break;
        }
    }

    public IEnumerator GainObjectScaleFeedback(GameObject obj, bool isPositiveFeedback, Vector3 originalScale)
    {
        Vector3 targetScale = new Vector3(1f, 1f, 1f);
        float pulseDuration = 0.2f;

        if (isPositiveFeedback)
        {
            targetScale = originalScale * 1.3f;
        }
        else
        {
            targetScale = originalScale * 0.7f;
        }

        float halfDuration = pulseDuration / 2f;

        float timeElapsed = 0f;
        while (timeElapsed < halfDuration)
        {
            float t = timeElapsed / halfDuration;

            obj.transform.localScale = Vector3.Lerp(originalScale, targetScale, t);
            timeElapsed += Time.deltaTime;
            yield return null;
        }

        obj.transform.localScale = targetScale;

        timeElapsed = 0f;

        while (timeElapsed < halfDuration)
        {
            float t = timeElapsed / halfDuration;

            obj.transform.localScale = Vector3.Lerp(targetScale, originalScale, t);
            timeElapsed += Time.deltaTime;
            yield return null;
        }

        obj.transform.localScale = originalScale;
    }
}
