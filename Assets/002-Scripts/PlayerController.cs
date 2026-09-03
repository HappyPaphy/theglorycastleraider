using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Playables;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : PlayerEntity
{
    [Header("Components")]
    [SerializeField] private CameraFollow cameraFollow;
    [SerializeField] private GameObject deadCamPrefab;
    [SerializeField] private CameraPostProcessEffect postProcressEffect;
    [SerializeField] private CameraBob cameraBob;
    [SerializeField] private PlayerMeleeAttack playerMeleeAttack;
    [SerializeField] private WeaponSway weaponSway;

    [Header("Input Actions")]
    public PlayerInputActions playerControls;
    [SerializeField] private InputAction input_Jump;
    [SerializeField] private InputAction input_Sprint;
    [SerializeField] private InputAction input_Move;
    [SerializeField] private InputAction input_Look;
    [SerializeField] private InputAction input_Interact;
    [SerializeField] private InputAction input_Reload;
    [SerializeField] private InputAction input_Dash;
    [SerializeField] private InputAction input_Minimap;
    [SerializeField] private InputAction input_Pause;
    [SerializeField] private InputAction input_SwitchWeapon_Up;
    [SerializeField] private InputAction input_SwitchWeapon_Right;
    [SerializeField] private InputAction input_SwitchWeapon_Down;
    [SerializeField] private InputAction input_SwitchWeapon_Left;
    public InputAction input_Kick;
    public InputAction input_Attack;
    public InputAction input_Block;

    public Sprite InteractSprite_Keyboard;
    public Sprite InteractSprite_XBox;

    [HideInInspector] public bool IsInteractHeld = false;
    [HideInInspector] public bool IsInteractPressed = false;
    private bool isDashHeld = false;
    private bool isDashPressed = false;
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

    [HideInInspector] public bool isMouseVisible = false;

    [Header("Combat")]
    [HideInInspector] public bool isJumpHeld = false;
    [HideInInspector] public bool isJumpPressed = false;
    [HideInInspector] public bool isSprintHeld = false;
    [HideInInspector] public bool isSprintPressed = false;
    [HideInInspector] public bool isKickHeld = false;
    [HideInInspector] public bool isKickPressed = false;
    [HideInInspector] public bool isAttackHeld = false;
    [HideInInspector] public bool isAttackPressed = false;
    [HideInInspector] public bool isBlockHeld = false;
    [HideInInspector] public bool isBlockPressed = false;
    [HideInInspector] public bool IsReloadHeld = false;
    [HideInInspector] public bool IsReloadPressed = false;

    [Header("Movement Settings")]
    [SerializeField] private float walkSpeed = 6f;
    [SerializeField] private float sprintSpeed = 10f;
    [SerializeField] private float sprintStaminaCost = 0.1f;
    [SerializeField] private float gravity = -19.62f;
    [SerializeField] private float jumpHeight = 1.5f;

    [Header("Slide Settings")]
    [SerializeField] private float slideStaminaCost = 10f;
    [SerializeField] private float slideSpeed = 15f;
    [SerializeField] private float slideDuration = 0.6f;
    [SerializeField] private float normalHeight = 2f;
    [SerializeField] private float slideHeight = 1f;

    [Header("Look Settings")]
    public bool isThisMainmenu = false;
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private float mouseSensitivity = 2f;
    [SerializeField] private float maxLookAngle = 85f;

    [Header("Melee Jolt Settings")]
    [SerializeField] private float joltRecoverySpeed = 12f;
    [SerializeField] private float joltOnsetSpeed = 30f;
    private Vector3 currentJolt;
    private Vector3 targetJolt;

    private float reStaminaSpeed = 100f;
    private float reStaminaRate = 1f;
    private float curReStaminaCoolDownTime = 0f;
    private float reStaminaCoolDownWaitTime = 1.25f;
    private bool isStaminaCoolDown;
    private bool isStaminaRecover;

    [HideInInspector] public bool isAttacking = false;
    [HideInInspector] public bool isExecuting = false;
    [HideInInspector] public float verticalRotation = 0f;
    private float attackLockTimer = 0f;
    private Vector3 attackLungeVelocity;

    private CharacterController controller;
    private Vector3 velocity;
    [HideInInspector] public bool isGrounded;
    private bool wasGroundedLastFrame = true;

    // Slide State Variables
    [HideInInspector] public bool isSliding = false;
    private float slideTimer = 0f;
    private Vector3 slideDirection;

    public static PlayerController instance;

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


        input_Kick = playerControls.Player.Kick;
        input_Kick.Enable();
        input_Kick.performed += OnKickPerformed;
        input_Kick.canceled += OnKickCanceled;

        input_Attack = playerControls.Player.Attack;
        input_Attack.Enable();
        input_Attack.performed += OnAttackPerformed;
        input_Attack.canceled += OnAttackCanceled;

        input_Block = playerControls.Player.Block;
        input_Block.Enable();
        input_Block.performed += OnBlockPerformed;
        input_Block.canceled += OnBlockCanceled;

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
        input_Attack.Disable();
        input_Block.Disable();
        input_Interact.Disable();
        input_Reload.Disable();
        input_Dash.Disable();
        input_Minimap.Disable();
        input_Pause.Disable();
        input_SwitchWeapon_Up.Disable();
        input_SwitchWeapon_Right.Disable();
        input_SwitchWeapon_Down.Disable();
        input_SwitchWeapon_Left.Disable();
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
        isJumpHeld = true;
        isJumpPressed = true;
    }

    private void OnJumpCanceled(InputAction.CallbackContext context)
    {
        isJumpHeld = false;
    }
    private void OnSprintPerformed(InputAction.CallbackContext context)
    {
        isSprintHeld = true;
        isSprintPressed = true;
    }

    private void OnSprintCanceled(InputAction.CallbackContext context)
    {
        isSprintHeld = false;
    }

    private void OnKickPerformed(InputAction.CallbackContext context)
    {
        isKickHeld = true;
        isKickPressed = true;
    }

    private void OnKickCanceled(InputAction.CallbackContext context)
    {
        isKickHeld = false;
    }

    private void OnAttackPerformed(InputAction.CallbackContext context)
    {
        isAttackHeld = true;
        isAttackPressed = true;
    }

    private void OnAttackCanceled(InputAction.CallbackContext context)
    {
        isAttackHeld = false;
    }

    private void OnBlockPerformed(InputAction.CallbackContext context)
    {
        isBlockHeld = true;
        isBlockPressed = true;
    }

    private void OnBlockCanceled(InputAction.CallbackContext context)
    {
        isBlockHeld = false;
    }

    private void OnInteractPerformed(UnityEngine.InputSystem.InputAction.CallbackContext context)
    {
        // When the button is first pressed down
        IsInteractHeld = true;
        IsInteractPressed = true;
    }

    private void OnInteractCanceled(UnityEngine.InputSystem.InputAction.CallbackContext context)
    {
        // When the button is released
        IsInteractHeld = false;
    }

    private void OnDashPerformed(UnityEngine.InputSystem.InputAction.CallbackContext context)
    {
        // When the button is first pressed down
        isDashHeld = true;
        isDashPressed = true;
    }

    private void OnDashCanceled(UnityEngine.InputSystem.InputAction.CallbackContext context)
    {
        // When the button is released
        isDashHeld = false;
    }

    private void OnMinimapPerformed(UnityEngine.InputSystem.InputAction.CallbackContext context)
    {
        // When the button is first pressed down
        IsMinimapHeld = true;
        IsMinimapPressed = true;
    }

    private void OnMinimapCanceled(UnityEngine.InputSystem.InputAction.CallbackContext context)
    {
        // When the button is released
        IsMinimapHeld = false;
    }

    private void OnReloadPerformed(UnityEngine.InputSystem.InputAction.CallbackContext context)
    {
        // When the button is first pressed down
        IsReloadHeld = true;
        IsReloadPressed = true;
    }

    private void OnReloadCanceled(UnityEngine.InputSystem.InputAction.CallbackContext context)
    {
        // When the button is released
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
        // When the button is released
        IsPauseHeld = false;
    }

    private void OnSwitchWeaponUpPerformed(UnityEngine.InputSystem.InputAction.CallbackContext context)
    {
        // When the button is first pressed down
        IsSwitchWeaponPressed_Up = true;
        IsSwitchWeaponHeld_Up = true;
    }

    private void OnSwitchWeaponUpCanceled(UnityEngine.InputSystem.InputAction.CallbackContext context)
    {
        // When the button is released
        IsSwitchWeaponHeld_Up = false;
    }

    private void OnSwitchWeaponRightPerformed(UnityEngine.InputSystem.InputAction.CallbackContext context)
    {
        // When the button is first pressed down
        IsSwitchWeaponPressed_Right = true;
        IsSwitchWeaponHeld_Right = true;
    }

    private void OnSwitchWeaponRightCanceled(UnityEngine.InputSystem.InputAction.CallbackContext context)
    {
        // When the button is released
        IsSwitchWeaponHeld_Right = false;
    }
    private void OnSwitchWeaponDownPerformed(UnityEngine.InputSystem.InputAction.CallbackContext context)
    {
        // When the button is first pressed down
        IsSwitchWeaponPressed_Down = true;
        IsSwitchWeaponHeld_Down = true;
    }

    private void OnSwitchWeaponDownCanceled(UnityEngine.InputSystem.InputAction.CallbackContext context)
    {
        // When the button is released
        IsSwitchWeaponHeld_Down = false;
    }
    private void OnSwitchWeaponLeftPerformed(UnityEngine.InputSystem.InputAction.CallbackContext context)
    {
        // When the button is first pressed down
        IsSwitchWeaponPressed_Left = true;
        IsSwitchWeaponHeld_Left = true;
    }

    private void OnSwitchWeaponLeftCanceled(UnityEngine.InputSystem.InputAction.CallbackContext context)
    {
        // When the button is released
        IsSwitchWeaponHeld_Left = false;
    }


    protected override void Awake()
    {
        instance = this;

        playerControls = new PlayerInputActions();

        base.Awake();
    }

    protected override void Start()
    {
        controller = GetComponent<CharacterController>();

        if (cameraTransform == null)
            cameraTransform = GetComponentInChildren<Camera>().transform;

        controller.height = normalHeight;

        base.Start();
    }

    protected override void Update()
    {
        if(!PauseGame.instance.IsPaused && CharacterHealthComponent.CurrentHP > 0)
        {
            HandleMouseLook();
            HandleMovement();
            RecoverFromJolt();
            StaminaRecover();
        }

        base.Update();
    }

    private void LateUpdate()
    {
        ResetActionInputPressed();
    }

    private void HandleMouseLook()
    {
        if(!isExecuting)
        {
            Vector2 lookValue = input_Look.ReadValue<Vector2>();

            float adjustedSensitivity = mouseSensitivity;

            if (InputSchemeManager.instance.CurrentInputMode == InputMode.PC)
            {
                adjustedSensitivity *= 0.05f;
            }

            float mouseX = lookValue.x * adjustedSensitivity;
            float mouseY = lookValue.y * adjustedSensitivity;

            verticalRotation -= mouseY;
            verticalRotation = Mathf.Clamp(verticalRotation, -maxLookAngle, maxLookAngle);

            transform.Rotate(Vector3.up * mouseX);
        }
        
        cameraTransform.localRotation = Quaternion.Euler(
            verticalRotation + currentJolt.x,
            currentJolt.y,
            currentJolt.z
        );

    }

    private void HandleMovement()
    {
        isGrounded = controller.isGrounded;

        if (isGrounded && !wasGroundedLastFrame && !isSliding)
        {
            // Only trigger if we were falling downward with significant speed
            if (velocity.y < -1f && weaponSway != null)
            {
                weaponSway.TriggerJumpImpulse(); // Reusing the heavy slam effect for landing
                TriggerMeleeJolt(new Vector3(-10f, 0f, 0f));
            }
        }
        wasGroundedLastFrame = isGrounded;

        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
        }

        Vector2 moveValue = Vector2.zero;
        if (!isAttacking && !isExecuting)
        {
            moveValue = input_Move.ReadValue<Vector2>();
        }

        Vector3 standardMove = transform.right * moveValue.x + transform.forward * moveValue.y;

        if (isDashPressed && isGrounded && !isSliding && !isAttacking && !isExecuting && standardMove.sqrMagnitude > 0.01f)
        {
            if(CharacterStaminaComponent.CurrentStamina > 0f)
            {
                RumbleManager.instance.RumblePulse(1f, 1.5f, slideDuration);
                StaminaDepleted(slideStaminaCost);
                isSliding = true;
                slideTimer = slideDuration;
                slideDirection = standardMove.normalized;
                controller.height = slideHeight;

                weaponSway.TriggerSlideImpulse();
            }
        }

        // 3. Apply Speed and Direction
        float currentSpeed;
        Vector3 finalMove;

        if (isAttacking)
        {
            attackLockTimer -= Time.deltaTime;
            attackLungeVelocity = Vector3.Lerp(attackLungeVelocity, Vector3.zero, Time.deltaTime * 10f);

            finalMove = attackLungeVelocity;
            currentSpeed = 1f; // The speed is already baked into the lunge vector

            if (attackLockTimer <= 0)
            {
                isAttacking = false;
            }
        }
        else if (isSliding)
        {
            slideTimer -= Time.deltaTime;
            currentSpeed = Mathf.Lerp(walkSpeed, slideSpeed, slideTimer / slideDuration);
            finalMove = slideDirection;

            if (slideTimer <= 0)
            {
                isSliding = false;
                controller.height = normalHeight;
            }
        }
        else
        {
            bool isSprinting = isSprintHeld && CharacterStaminaComponent.CurrentStamina > 0f;

            if (isSprinting && moveValue.sqrMagnitude > 0.01f)
            {
                currentSpeed = sprintSpeed;
                StaminaDepleted(sprintStaminaCost);
            }
            else
            {
                currentSpeed = walkSpeed;
            }

            finalMove = standardMove;
        }

        controller.Move(finalMove * currentSpeed * Time.deltaTime);

        if (isJumpPressed && isGrounded && !isSliding && !isAttacking)
        {
            weaponSway.TriggerJumpImpulse();
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            wasGroundedLastFrame = false; // Prevent immediate landing trigger right after leaving the ground

            TriggerMeleeJolt(new Vector3(-10f, 0f, 0f));
        }

        // 5. Apply Gravity
        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }

    public void TakeSwordHit(EnemyEntity enemy)
    {
        if (playerMeleeAttack.currentParry > 0f)
        {
            //SoundManager.instance.ParriedSound();
            enemy.GotParried();
            SoundManager.instance.ParriedSound(transform.position);
            BlockedOrParriedEffect(enemy.eyesTransform.position);
            StaminaDepleted(enemy.staminaDamage / 3);
            RumbleManager.instance.RumblePulse(1f, 3f, 0.4f);
            cameraBob.TriggerShake(0.293f, 0.05f);
        }
        else
        {
            enemy.AttackSuccessful();

            if (playerMeleeAttack.isBlocking)
            {
                BlockedOrParriedEffect(enemy.eyesTransform.position);
                playerMeleeAttack.PerformBlock();
                SoundManager.instance.SwordSound_Metal(transform.position);
                cameraBob.TriggerShake(0.293f, 0.05f);
                StaminaDepleted(enemy.staminaDamage);
                RumbleManager.instance.RumblePulse(1f, 3f, 0.4f);
            }
            else
            {
                postProcressEffect.TriggerDamageEffect();
                SoundManager.instance.SwordSound_Flesh(transform.position);
                SoundManager.instance.PlayerHurtSound(transform.position);
                cameraBob.TriggerShake(0.293f, 0.1f);
                CharacterHealthComponent.TakeDamage(enemy.attackDamage);
                TriggerMeleeJolt(new Vector3(30f, 0f, 0f));
                RumbleManager.instance.RumblePulse(1f, 2.5f, 0.2f);
            }
        }
    }

    public void TakeArrowHit(ArrowProjectile arrow)
    {
        if (playerMeleeAttack.currentParry > 0f)
        {
            BlockedOrParriedEffect(arrow.gameObject.transform.position);
            SoundManager.instance.ParriedSound(transform.position);
            StaminaDepleted(arrow.staminaCost / 3);
            RumbleManager.instance.RumblePulse(1f, 2.5f, 0.1f);
            cameraBob.TriggerShake(0.293f, 0.05f);
        }
        else
        {
            if (playerMeleeAttack.isBlocking)
            {
                BlockedOrParriedEffect(arrow.gameObject.transform.position);
                playerMeleeAttack.PerformBlock();
                SoundManager.instance.SwordSound_Metal(transform.position);
                cameraBob.TriggerShake(0.293f, 0.05f);
                StaminaDepleted(arrow.staminaCost);
                RumbleManager.instance.RumblePulse(1f, 2.5f, 0.1f);
            }
            else
            {
                postProcressEffect.TriggerDamageEffect();
                SoundManager.instance.SwordSound_Flesh(transform.position);
                SoundManager.instance.BowSound_Hit(transform.position);
                SoundManager.instance.PlayerHurtSound(transform.position);
                cameraBob.TriggerShake(0.293f, 0.1f);
                CharacterHealthComponent.TakeDamage(arrow.damage);
                TriggerMeleeJolt(new Vector3(30f, 0f, 0f));
                RumbleManager.instance.RumblePulse(1f, 2.5f, 0.1f);
            }
        }
    }

    public void StaminaDepleted(float value)
    {
        curReStaminaCoolDownTime = 0f;
        isStaminaRecover = false;
        isStaminaCoolDown = true;
        CharacterStaminaComponent.DepleteStamina(value);
    }

    private void StaminaRecover()
    {
        if (isStaminaCoolDown)
        {
            curReStaminaCoolDownTime += Time.deltaTime;

            if (curReStaminaCoolDownTime >= reStaminaCoolDownWaitTime)
            {
                isStaminaCoolDown = false;
                isStaminaRecover = true;
            }
        }

        if (isStaminaRecover)
        {
            if (CharacterStaminaComponent.CurrentStamina < CharacterStaminaComponent.MaxStamina)
            {
                float staminaToRecover = reStaminaRate * Time.deltaTime * reStaminaSpeed;
                CharacterStaminaComponent.Recover(staminaToRecover);
            }
        }

        if (CharacterStaminaComponent.CurrentStamina > CharacterStaminaComponent.MaxStamina)
        {
            CharacterStaminaComponent.SetStamina(CharacterStaminaComponent.MaxStamina);
        }
        /*else if(CharacterStaminaComponent.CurrentStamina < 0f)
        {
            CharacterStaminaComponent.SetStamina(0f);
        }*/
    }

    private void BlockedOrParriedEffect(Vector3 pos)
    {
        if(ObjectPoolingManager.instance != null)
        {
            ObjectPoolingManager.instance.SpawnObject("Spark", pos);
        }
    }

    public void TriggerMeleeJolt(Vector3 joltAngles)
    {
        // Adds to the current jolt so rapid attacks stack the physical effect slightly
        targetJolt += joltAngles;
    }

    private void RecoverFromJolt()
    {
        targetJolt = Vector3.Lerp(targetJolt, Vector3.zero, Time.deltaTime * joltRecoverySpeed);

        currentJolt = Vector3.Lerp(currentJolt, targetJolt, Time.deltaTime * joltOnsetSpeed);
    }

    public void TriggerAttackMovement(float lockDuration, float initialLungeSpeed)
    {
        isAttacking = true;
        attackLockTimer = lockDuration;

        // If the player attacks while sliding, immediately cancel the slide
        if (isSliding)
        {
            isSliding = false;
            controller.height = normalHeight;
        }

        // Thrust the player forward based on where the camera is looking
        attackLungeVelocity = cameraTransform.forward * initialLungeSpeed;

        // Prevent the lunge from pushing the player up into the air or down into the floor
        attackLungeVelocity.y = 0f;
    }

    public override void Die()
    {
        if(isDiedOnce) { return; }

        //controller.enabled = false;
        GameObject deadCam = Instantiate(deadCamPrefab, cameraTransform.transform.position, 
        cameraTransform.transform.rotation * Quaternion.Euler(-55f, 0f, 40f));
        cameraFollow.cameraPos = deadCam.transform;
        

        base.Die();
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
        isBlockPressed = false;
        isAttackPressed = false;
        isKickPressed = false;
        isJumpPressed = false;
        isSprintPressed = false;
    }
}