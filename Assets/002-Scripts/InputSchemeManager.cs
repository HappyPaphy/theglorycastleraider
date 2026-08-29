using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public enum InputMode
{
    PC,
    Xbox,
    PlayStation
}

[System.Serializable]
public class UINavigation
{
    public Sprite pc;
    public Sprite xbox;
    public Sprite playStation;
}

public class InputSchemeManager : MonoBehaviour
{
    public static event Action<InputMode> OnInputModeChanged;

    [Tooltip("The currently active input mode.")]
    [SerializeField] private InputMode currentInputMode = InputMode.PC;

    // A small dead zone value to prevent accidental switching from minor joystick drift.
    private const float AxisThreshold = 0.1f;
    private const float MouseMovementThreshold = 0.01f;

    public const string id_Navigation_ButtonSouth = "Navigation_ButtonSouth";
    public const string id_Navigation_ButtonEast = "Navigation_ButtonEast";
    public const string id_Navigation_ButtonWest = "Navigation_ButtonWest";
    public const string id_Navigation_ButtonNorth = "Navigation_ButtonNorth";

    private Sprite spr_ButtonSouth;
    private Sprite spr_ButtonEast;
    private Sprite spr_ButtonWest;
    private Sprite spr_ButtonNorth;

    // --- Properties ---
    public InputMode CurrentInputMode => currentInputMode;

    public List<UINavigation> uiNavigations;

    public static InputSchemeManager instance;

    void Awake()
    {
        if(instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(this);
        }

        SetInputMode(InputMode.PC);
    }

    void Start()
    {
        StartCoroutine(UnscaledInputCheck());
    }

    private IEnumerator UnscaledInputCheck()
    {
        // Coroutine runs until the GameObject is destroyed
        while (true)
        {
            if (IsGamepadActive())
            {
                Cursor.visible = false;
                string gamepadType = GetGamepadType();

                switch (gamepadType)
                {
                    case "Xbox":
                        SetInputMode(InputMode.Xbox);
                        break;

                    case "PlayStation":
                        SetInputMode(InputMode.PlayStation);
                        break;

                    default:
                        SetInputMode(InputMode.Xbox);
                        break;
                }
            }
            else if (IsAnyKeyOnKeyboardPressed() || IsAnyMouseInputActive())
            {
                bool instancesValid = /*GameResultRoguelike.instance != null &&
                          PauseGame.instance != null &&*/
                          PlayerController.instance != null;

                if (instancesValid)
                {
                    //bool isIdle = GameResultRoguelike.instance.currentState == GameResultState.Idle;
                    //bool isNotPaused = !PauseGame.instance.IsPaused;
                    bool isAlive = PlayerController.instance.CharacterHealthComponent.CurrentHP > 0f;

                    if (/*isIdle && isNotPaused &&*/ isAlive)
                    {
                        //Cursor.visible = PlayerController.instance.isMouseVisible;
                        Cursor.visible = false;
                    }
                    else
                    {
                        Cursor.visible = true;
                    }
                }
                else
                {
                    // Default behavior if instances are missing (e.g., during loading)
                    Cursor.visible = true;
                }

                SetInputMode(InputMode.PC);
            }

            // Wait one frame using real-time (unscaled time)
            yield return new WaitForSecondsRealtime(0.1f); // Check every 0.01 seconds (100 times per second)
        }
    }

    public string GetGamepadType()
    {
        var gamepad = Gamepad.current;
        if (gamepad == null) return "PC"; // No gamepad, assume PC input sprites

        // Check for common Xbox identifiers
        if (gamepad.description.deviceClass.Contains("Xbox") ||
            gamepad.description.deviceClass.Contains("XInput"))
        {
            return "Xbox";
        }

        // Check for common PlayStation identifiers
        if (gamepad.description.deviceClass.Contains("DualShock") ||
            gamepad.description.deviceClass.Contains("DualSense"))
        {
            return "PlayStation";
        }

        return "GenericGamepad";
    }

    public bool IsGamepadActive()
    {
        var gamepad = Gamepad.current;
        if (gamepad == null) return false;

        // Iterate through all buttons on the gamepad and check if any are pressed.
        foreach (var button in gamepad.allControls)
        {
            // Filter for controls that are buttons and not axes/sticks.
            if (button.IsPressed() || button.IsActuated())
            {
                return true;
            }
        }
        return false;
    }

    public void MakeButtonInteracable(Button button, bool isTrue)
    {
        Navigation nav = button.navigation;

        if (isTrue)
        {
            nav.mode = Navigation.Mode.Automatic;
        }
        else
        {
            nav.mode = Navigation.Mode.None;
        }

        button.navigation = nav;
        button.interactable = isTrue;
    }

    public bool IsAnyKeyOnKeyboardPressed()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return false;

        // Checks if ANY key on the keyboard is currently pressed (held down)
        return keyboard.anyKey.isPressed;
    }

    public bool IsAnyMouseInputActive()
    {
        var mouse = Mouse.current;
        if (mouse == null) return false;

        // Check 1: Any Button Press
        // This checks if the left, right, or middle button is pressed.
        if (mouse.leftButton.isPressed ||
            mouse.rightButton.isPressed ||
            mouse.middleButton.isPressed)
        {
            return true;
        }

        // Check 2: Movement (Delta)
        // Check if the mouse has moved significantly this frame (above a small threshold).
        // The delta is the change in screen position.
        if (mouse.delta.ReadValue().sqrMagnitude > 0.1f) // Use a small threshold like 0.1f
        {
            return true;
        }

        // Check 3: Scroll Wheel
        // Check if the scroll wheel has been moved significantly this frame.
        if (Mathf.Abs(mouse.scroll.ReadValue().y) > 0.01f) // Check the Y-axis for vertical scroll
        {
            return true;
        }

        return false;
    }

    private void SetInputMode(InputMode newMode)
    {
        if (currentInputMode != newMode)
        {
            currentInputMode = newMode;
            Debug.Log($"Input mode switched to: {currentInputMode}");

            // Notify all subscribed UI elements
            OnInputModeChanged?.Invoke(currentInputMode);
        }
    }
}
