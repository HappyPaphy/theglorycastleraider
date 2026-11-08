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

public enum PlayerInputState
{
    Active,
    Inactive
}

public enum UIInputState
{
    Active,
    Inactive
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
    public InputMode currentInputMode = InputMode.PC;
    public PlayerInputState currentPlayerInputState;
    public UIInputState currentUIInputState;

    [Header("Player Input Actions")]
    public PlayerInputActions playerControls;
    public InputAction input_Jump;
    public InputAction input_Sprint;
    public InputAction input_Move;
    public InputAction input_Look;
    public InputAction input_Interact;
    public InputAction input_Reload;
    public InputAction input_Dash;
    public InputAction input_Minimap;
    public InputAction input_Pause;
    public InputAction input_ToggleTwoHanded_Left;
    public InputAction input_ToggleTwoHanded_Right;
    public InputAction input_SwitchWeapon_Up;
    public InputAction input_SwitchWeapon_Right;
    public InputAction input_SwitchWeapon_Down;
    public InputAction input_SwitchWeapon_Left;
    public InputAction input_ToggleLoadout;
    public InputAction input_Kick;
    public InputAction input_PerformRightHand;
    public InputAction input_PerformLeftHand;

    [Header("UI Input Actions")]
    public InputAction input_Submit;
    public InputAction input_Cancel;
    public InputAction input_UIReload;


    public Sprite InteractSprite_Keyboard;
    public Sprite InteractSprite_XBox;

    [HideInInspector] public bool IsInteractHeld = false;
    [HideInInspector] public bool IsInteractPressed = false;
    [HideInInspector] public bool isDashHeld = false;
    [HideInInspector] public bool isDashPressed = false;
    [HideInInspector] public bool IsMinimapHeld = false;
    [HideInInspector] public bool IsMinimapPressed = false;
    [HideInInspector] public bool IsPauseHeld = false;
    [HideInInspector] public bool IsPausePressed = false;
    [HideInInspector] public bool IsSwitchWeaponPressed_Up = false;
    [HideInInspector] public bool IsSwitchWeaponPressed_Right = false;
    [HideInInspector] public bool IsSwitchWeaponPressed_Down = false;
    [HideInInspector] public bool IsSwitchWeaponPressed_Left = false;
    [HideInInspector] public bool IsSwitchWeaponHeld_Up = false;
    [HideInInspector] public bool IsSwitchWeaponHeld_Right = false;
    [HideInInspector] public bool IsSwitchWeaponHeld_Down = false;
    [HideInInspector] public bool IsSwitchWeaponHeld_Left = false;
    [HideInInspector] public bool isJumpHeld = false;
    [HideInInspector] public bool isJumpPressed = false;
    [HideInInspector] public bool isSprintHeld = false;
    [HideInInspector] public bool isSprintPressed = false;
    [HideInInspector] public bool isKickHeld = false;
    [HideInInspector] public bool isKickPressed = false;
    [HideInInspector] public bool isRightHandHeld = false;
    [HideInInspector] public bool isRightHandPressed = false;
    [HideInInspector] public bool isLeftHandHeld = false;
    [HideInInspector] public bool isLeftHandPressed = false;
    [HideInInspector] public bool IsReloadHeld = false;
    [HideInInspector] public bool IsReloadPressed = false;
    [HideInInspector] public bool IsToggleTwoHandedHeld_Left = false;
    [HideInInspector] public bool IsToggleTwoHandedPressed_Left = false;
    [HideInInspector] public bool IsToggleTwoHandedHeld_Right = false;
    [HideInInspector] public bool IsToggleTwoHandedPressed_Right = false;
    [HideInInspector] public bool IsToggleLoadoutHeld = false;
    [HideInInspector] public bool IsToggleLoadoutPressed = false;

    [HideInInspector] public bool IsSubmitPressed = false;
    [HideInInspector] public bool IsSubmitHeld = false;
    [HideInInspector] public bool IsCancelPressed = false;
    [HideInInspector] public bool IsCancelHeld = false;
    [HideInInspector] public bool IsUIReloadPressed = false;
    [HideInInspector] public bool IsUIReloadHeld = false;

    [HideInInspector] public bool isMouseVisible = false;

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
    public PlayerInputState CurrentPlayerInputState => currentPlayerInputState;
    public UIInputState CurrentUIInputState => currentUIInputState;

    public List<UINavigation> uiNavigations;

    public static InputSchemeManager instance;

    public Action OnInputActiveChange;

    private void OnEnable()
    {
        // 1. Subscribe to the binding update event
        RebindSaveLoad.OnBindingsLoaded += ApplyLoadedBindings;

        // 2. Load the overrides immediately into playerControls before maps are activated
        ApplyLoadedBindings();

        // 3. Set up and enable actions normally
        input_Move = playerControls.Player.Move;
        input_Move.Enable();

        input_Look = playerControls.Player.Look;
        input_Look.Enable();

        input_Jump = playerControls.Player.Jump;
        input_Jump.Enable();
        input_Jump.performed += OnJumpPerformed;
        input_Jump.canceled += OnJumpCanceled;

        input_Sprint = playerControls.Player.Sprint;
        input_Sprint.Enable();
        input_Sprint.performed += OnSprintPerformed;
        input_Sprint.canceled += OnSprintCanceled;

        input_ToggleTwoHanded_Left = playerControls.Player.ToggleTwoHanded_Left;
        input_ToggleTwoHanded_Left.Enable();
        input_ToggleTwoHanded_Left.performed += OnToggleTwoHandedPerformed_Left;
        input_ToggleTwoHanded_Left.canceled += OnToggleTwoHandedCanceled_Left;

        input_ToggleTwoHanded_Right = playerControls.Player.ToggleTwoHanded_Right;
        input_ToggleTwoHanded_Right.Enable();
        input_ToggleTwoHanded_Right.performed += OnToggleTwoHandedPerformed_Right;
        input_ToggleTwoHanded_Right.canceled += OnToggleTwoHandedCanceled_Right;

        input_Kick = playerControls.Player.Kick;
        input_Kick.Enable();
        input_Kick.performed += OnKickPerformed;
        input_Kick.canceled += OnKickCanceled;

        input_PerformRightHand = playerControls.Player.RightHand;
        input_PerformRightHand.Enable();
        input_PerformRightHand.performed += OnRightHandPerformed;
        input_PerformRightHand.canceled += OnRightHandCanceled;

        input_PerformLeftHand = playerControls.Player.LeftHand;
        input_PerformLeftHand.Enable();
        input_PerformLeftHand.performed += OnLeftHandPerformed;
        input_PerformLeftHand.canceled += OnLeftHandCanceled;

        input_Interact = playerControls.Player.Interact;
        input_Interact.Enable();
        input_Interact.performed += OnInteractPerformed;
        input_Interact.canceled += OnInteractCanceled;

        input_Reload = playerControls.Player.Reload;
        input_Reload.Enable();
        input_Reload.performed += OnReloadPerformed;
        input_Reload.canceled += OnReloadCanceled;

        input_Dash = playerControls.Player.Dash;
        input_Dash.Enable();
        input_Dash.performed += OnDashPerformed;
        input_Dash.canceled += OnDashCanceled;

        input_Minimap = playerControls.Player.Minimap;
        input_Minimap.Enable();
        input_Minimap.performed += OnMinimapPerformed;
        input_Minimap.canceled += OnMinimapCanceled;

        input_Pause = playerControls.Player.Pause;
        input_Pause.Enable();
        input_Pause.performed += OnPausePerformed;
        input_Pause.canceled += OnPauseCanceled;

        input_SwitchWeapon_Up = playerControls.Player.SwitchWeaponUp;
        input_SwitchWeapon_Up.Enable();
        input_SwitchWeapon_Up.performed += OnSwitchWeaponUpPerformed;
        input_SwitchWeapon_Up.canceled += OnSwitchWeaponUpCanceled;

        input_SwitchWeapon_Right = playerControls.Player.SwitchWeaponRight;
        input_SwitchWeapon_Right.Enable();
        input_SwitchWeapon_Right.performed += OnSwitchWeaponRightPerformed;
        input_SwitchWeapon_Right.canceled += OnSwitchWeaponRightCanceled;

        input_SwitchWeapon_Down = playerControls.Player.SwitchWeaponDown;
        input_SwitchWeapon_Down.Enable();
        input_SwitchWeapon_Down.performed += OnSwitchWeaponDownPerformed;
        input_SwitchWeapon_Down.canceled += OnSwitchWeaponDownCanceled;

        input_SwitchWeapon_Left = playerControls.Player.SwitchWeaponLeft;
        input_SwitchWeapon_Left.Enable();
        input_SwitchWeapon_Left.performed += OnSwitchWeaponLeftPerformed;
        input_SwitchWeapon_Left.canceled += OnSwitchWeaponLeftCanceled;

        input_ToggleLoadout = playerControls.Player.Loadout;
        input_ToggleLoadout.Enable();
        input_ToggleLoadout.performed += OnToggleLoadoutPerformed;
        input_ToggleLoadout.canceled += OnToggleLoadoutCanceled;

        input_Submit = playerControls.UI.Submit;
        input_Submit.Enable();
        input_Submit.performed += OnSubmitPerformed;
        input_Submit.canceled += OnSubmitCanceled;

        input_Cancel = playerControls.UI.Cancel;
        input_Cancel.Enable();
        input_Cancel.performed += OnCancelPerformed;
        input_Cancel.canceled += OnCancelCanceled;

        input_UIReload = playerControls.UI.Reload;
        input_UIReload.Enable();
        input_UIReload.performed += OnUIReloadPerformed;
        input_UIReload.canceled += OnUIReloadCanceled;
    }

    private void OnDisable()
    {
        // Unsubscribe from the event to avoid memory leaks
        RebindSaveLoad.OnBindingsLoaded -= ApplyLoadedBindings;

        input_Move.Disable();
        input_Look.Disable();
        input_Jump.Disable();
        input_Sprint.Disable();
        input_Kick.Disable();
        input_PerformLeftHand.Disable();
        input_PerformRightHand.Disable();
        input_Interact.Disable();
        input_Reload.Disable();
        input_Dash.Disable();
        input_Minimap.Disable();
        input_Pause.Disable();
        input_ToggleTwoHanded_Left.Disable();
        input_ToggleTwoHanded_Right.Disable();
        input_SwitchWeapon_Up.Disable();
        input_SwitchWeapon_Right.Disable();
        input_SwitchWeapon_Down.Disable();
        input_SwitchWeapon_Left.Disable();
        input_ToggleLoadout.Disable();

        input_Submit.Disable();
        input_Cancel.Disable();
        input_UIReload.Disable();
    }

    private void ApplyLoadedBindings()
    {
        if (playerControls == null) return;

        // Pull the key directly from your active RebindSaveLoad instance, or default back to a safe fallback string
        string key = RebindSaveLoad.instance != null ? RebindSaveLoad.instance.playerPreferenceKey : "YOUR_DEFAULT_PREF_KEY";

        string rebinds = PlayerPrefs.GetString(key);
        if (!string.IsNullOrEmpty(rebinds))
        {
            // If bindings are modified mid-game, input maps must cycle off and on to apply changes cleanly
            bool wasEnabled = playerControls.asset.enabled;
            if (wasEnabled) playerControls.Disable();

            playerControls.LoadBindingOverridesFromJson(rebinds);

            if (wasEnabled) playerControls.Enable();
        }
    }

    private void OnJumpPerformed(InputAction.CallbackContext context)
    {
        if(currentPlayerInputState == PlayerInputState.Inactive) { return; }

        isJumpHeld = true;
        isJumpPressed = true;
    }

    private void OnJumpCanceled(InputAction.CallbackContext context)
    {
        isJumpHeld = false;
    }

    private void OnToggleLoadoutPerformed(InputAction.CallbackContext context)
    {
        IsToggleLoadoutHeld = true;
        IsToggleLoadoutPressed = true;
    }

    private void OnToggleLoadoutCanceled(InputAction.CallbackContext context)
    {
        IsToggleLoadoutHeld = false;
    }

    private void OnSubmitPerformed(InputAction.CallbackContext context)
    {
        if (currentUIInputState == UIInputState.Inactive) { return; }

        IsSubmitHeld = true;
        IsSubmitPressed = true;
    }

    private void OnSubmitCanceled(InputAction.CallbackContext context)
    {
        IsSubmitHeld = false;
    }

    private void OnCancelPerformed(InputAction.CallbackContext context)
    {
        if (currentUIInputState == UIInputState.Inactive) { return; }

        IsCancelHeld = true;
        IsCancelPressed = true;
    }

    private void OnCancelCanceled(InputAction.CallbackContext context)
    {
        IsCancelHeld = false;
    }

    private void OnUIReloadPerformed(InputAction.CallbackContext context)
    {
        if (currentUIInputState == UIInputState.Inactive) { return; }

        IsUIReloadHeld = true;
        IsUIReloadPressed = true;
    }

    private void OnUIReloadCanceled(InputAction.CallbackContext context)
    {
        IsUIReloadHeld = false;
    }

    private void OnSprintPerformed(InputAction.CallbackContext context)
    {
        if (currentPlayerInputState == PlayerInputState.Inactive) { return; }

        isSprintHeld = true;
        isSprintPressed = true;
    }

    private void OnSprintCanceled(InputAction.CallbackContext context)
    {
        isSprintHeld = false;
    }

    private void OnKickPerformed(InputAction.CallbackContext context)
    {
        if (currentPlayerInputState == PlayerInputState.Inactive) { return; }

        isKickHeld = true;
        isKickPressed = true;
    }

    private void OnKickCanceled(InputAction.CallbackContext context)
    {
        isKickHeld = false;
    }

    private void OnRightHandPerformed(InputAction.CallbackContext context)
    {
        if (currentPlayerInputState == PlayerInputState.Inactive) { return; }

        isRightHandHeld = true;
        isRightHandPressed = true;
    }

    private void OnRightHandCanceled(InputAction.CallbackContext context)
    {
        isRightHandHeld = false;
    }

    private void OnLeftHandPerformed(InputAction.CallbackContext context)
    {
        if (currentPlayerInputState == PlayerInputState.Inactive) { return; }

        isLeftHandHeld = true;
        isLeftHandPressed = true;
    }

    private void OnLeftHandCanceled(InputAction.CallbackContext context)
    {
        isLeftHandHeld = false;
    }

    private void OnInteractPerformed(UnityEngine.InputSystem.InputAction.CallbackContext context)
    {
        if (currentPlayerInputState == PlayerInputState.Inactive) { return; }

        // When the button is first pressed down
        IsInteractHeld = true;
        IsInteractPressed = true;
    }

    private void OnInteractCanceled(UnityEngine.InputSystem.InputAction.CallbackContext context)
    {
        IsInteractHeld = false;
    }

    private void OnDashPerformed(UnityEngine.InputSystem.InputAction.CallbackContext context)
    {
        if (currentPlayerInputState == PlayerInputState.Inactive) { return; }

        // When the button is first pressed down
        isDashHeld = true;
        isDashPressed = true;
    }

    private void OnDashCanceled(UnityEngine.InputSystem.InputAction.CallbackContext context)
    {
        isDashHeld = false;
    }

    private void OnMinimapPerformed(UnityEngine.InputSystem.InputAction.CallbackContext context)
    {
        if (currentPlayerInputState == PlayerInputState.Inactive) { return; }

        // When the button is first pressed down
        IsMinimapHeld = true;
        IsMinimapPressed = true;
    }

    private void OnMinimapCanceled(UnityEngine.InputSystem.InputAction.CallbackContext context)
    {
        IsMinimapHeld = false;
    }

    private void OnReloadPerformed(UnityEngine.InputSystem.InputAction.CallbackContext context)
    {
        if (currentPlayerInputState == PlayerInputState.Inactive) { return; }

        // When the button is first pressed down
        IsReloadHeld = true;
        IsReloadPressed = true;
    }

    private void OnReloadCanceled(UnityEngine.InputSystem.InputAction.CallbackContext context)
    {
        IsReloadHeld = false;
    }

    private void OnPausePerformed(UnityEngine.InputSystem.InputAction.CallbackContext context)
    {
        // When the button is first pressed down
        IsPauseHeld = true;
        IsPausePressed = true;
    }

    private void OnPauseCanceled(UnityEngine.InputSystem.InputAction.CallbackContext context)
    {
        IsPauseHeld = false;
    }

    private void OnToggleTwoHandedPerformed_Left(UnityEngine.InputSystem.InputAction.CallbackContext context)
    {
        if (currentPlayerInputState == PlayerInputState.Inactive) { return; }

        IsToggleTwoHandedHeld_Left = true;
        IsToggleTwoHandedPressed_Left = true;
    }

    private void OnToggleTwoHandedCanceled_Left(UnityEngine.InputSystem.InputAction.CallbackContext context)
    {
        IsToggleTwoHandedHeld_Left = false;
    }

    private void OnToggleTwoHandedPerformed_Right(UnityEngine.InputSystem.InputAction.CallbackContext context)
    {
        if (currentPlayerInputState == PlayerInputState.Inactive) { return; }

        // When the button is first pressed down
        IsToggleTwoHandedHeld_Right = true;
        IsToggleTwoHandedPressed_Right = true;
    }

    private void OnToggleTwoHandedCanceled_Right(UnityEngine.InputSystem.InputAction.CallbackContext context)
    {
        IsToggleTwoHandedHeld_Right = false;
    }

    private void OnSwitchWeaponUpPerformed(UnityEngine.InputSystem.InputAction.CallbackContext context)
    {
        if (currentPlayerInputState == PlayerInputState.Inactive) { return; }

        // When the button is first pressed down
        IsSwitchWeaponPressed_Up = true;
        IsSwitchWeaponHeld_Up = true;
    }

    private void OnSwitchWeaponUpCanceled(UnityEngine.InputSystem.InputAction.CallbackContext context)
    {
        IsSwitchWeaponHeld_Up = false;
    }

    private void OnSwitchWeaponRightPerformed(UnityEngine.InputSystem.InputAction.CallbackContext context)
    {
        if (currentPlayerInputState == PlayerInputState.Inactive) { return; }

        // When the button is first pressed down
        IsSwitchWeaponPressed_Right = true;
        IsSwitchWeaponHeld_Right = true;
    }

    private void OnSwitchWeaponRightCanceled(UnityEngine.InputSystem.InputAction.CallbackContext context)
    {
        IsSwitchWeaponHeld_Right = false;
    }
    private void OnSwitchWeaponDownPerformed(UnityEngine.InputSystem.InputAction.CallbackContext context)
    {
        if (currentPlayerInputState == PlayerInputState.Inactive) { return; }

        // When the button is first pressed down
        IsSwitchWeaponPressed_Down = true;
        IsSwitchWeaponHeld_Down = true;
    }

    private void OnSwitchWeaponDownCanceled(UnityEngine.InputSystem.InputAction.CallbackContext context)
    {
        IsSwitchWeaponHeld_Down = false;
    }
    private void OnSwitchWeaponLeftPerformed(UnityEngine.InputSystem.InputAction.CallbackContext context)
    {
        if (currentPlayerInputState == PlayerInputState.Inactive) { return; }

        // When the button is first pressed down
        IsSwitchWeaponPressed_Left = true;
        IsSwitchWeaponHeld_Left = true;
    }

    private void OnSwitchWeaponLeftCanceled(UnityEngine.InputSystem.InputAction.CallbackContext context)
    {
        IsSwitchWeaponHeld_Left = false;
    }

    void Awake()
    {
        if(instance == null)
        {
            instance = this;
        }

        playerControls = new PlayerInputActions();

        SetInputMode(InputMode.PC);

        OnInputActiveChange += InputActiveCheck;
    }

    void Start()
    {
        StartCoroutine(UnscaledInputCheck());
    }
    private void LateUpdate()
    {
        ResetActionInputPressed();
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
                bool instancesValid = /*GameResultRoguelike.instance != null &&*/
                          PauseGame.instance != null &&
                          PlayerController.instance != null;

                if (instancesValid)
                {
                    //bool isIdle = GameResultRoguelike.instance.currentState == GameResultState.Idle;
                    bool isNotPaused = !PauseGame.instance.IsPaused && !EquipmentLoadOut.instance.isPanelActive && !PerkSelectionManager.instance.isPanelActive;
                    bool isAlive = PlayerController.instance.CharacterHealthComponent.CurrentHP > 0f;

                    if (/*isIdle &&*/ isNotPaused && isAlive && !PauseGame.instance.isThisMainmenu)
                    {
                        //Cursor.visible = PlayerController.instance.isMouseVisible;
                        Cursor.visible = false;
                        Cursor.lockState = CursorLockMode.Locked;
                    }
                    else
                    {
                        if(GameManager.instance.navigationMode == UINavigtionMode.Pointer && !isNotPaused)
                        {
                            Cursor.visible = true;
                            Cursor.lockState = CursorLockMode.None;
                        }
                        else
                        {
                            Cursor.visible = false;
                            Cursor.lockState = CursorLockMode.Locked;
                        }
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

    private void InputActiveCheck()
    {
        bool isNotPaused = !PauseGame.instance.IsPaused && !EquipmentLoadOut.instance.isPanelActive && !PerkSelectionManager.instance.isPanelActive;
        var playerInputActions_Player = playerControls.Player;
        var playerInputActions_UI = playerControls.UI;


        if (isNotPaused && !PauseGame.instance.isThisMainmenu)
        {
            input_Look.Enable();

            if(currentPlayerInputState == PlayerInputState.Inactive)
                EnablePlayerInputActions(true);

            if(currentUIInputState == UIInputState.Active)
                EnableUIInputActions(false);
        }
        else
        {
            input_Look.Disable();

            if (currentPlayerInputState == PlayerInputState.Active)
                EnablePlayerInputActions(false);


            if (currentUIInputState == UIInputState.Inactive)
                EnableUIInputActions(true);
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

    public void EnablePlayerInputActions(bool isEnable)
    {
        if(isEnable)
        {
            currentPlayerInputState = PlayerInputState.Active;
            playerControls.Player.Enable();
        }
        else
        {
            currentPlayerInputState = PlayerInputState.Inactive;
            playerControls.Player.Disable();
        }
    }

    public void EnableUIInputActions(bool isEnable)
    {
        if (isEnable)
        {
            currentUIInputState = UIInputState.Active;
            playerControls.UI.Enable();
        }
        else
        {
            currentUIInputState = UIInputState.Inactive;
            playerControls.UI.Disable();
        }
    }
    private void ResetActionInputPressed()
    {
        IsInteractPressed = false;
        isDashPressed = false;
        IsMinimapPressed = false;
        IsReloadPressed = false;
        IsPausePressed = false;
        IsSwitchWeaponPressed_Up = false;
        IsSwitchWeaponPressed_Right = false;
        IsSwitchWeaponPressed_Down = false;
        IsSwitchWeaponPressed_Left = false;
        isRightHandPressed = false;
        isLeftHandPressed = false;
        isKickPressed = false;
        isJumpPressed = false;
        isSprintPressed = false;
        IsToggleTwoHandedPressed_Left = false;
        IsToggleTwoHandedPressed_Right = false;
        IsToggleLoadoutPressed = false;

        IsSubmitPressed = false;
        IsCancelPressed = false;
        IsUIReloadPressed = false;
    }
}
