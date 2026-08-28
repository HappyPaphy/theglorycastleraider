using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Playables;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : PlayerEntity
{
    [Header("Components")]
    [SerializeField] private CameraPostProcessEffect postProcressEffect;
    [SerializeField] private CameraBob cameraBob;
    [SerializeField] private PlayerMeleeAttack playerMeleeAttack;
    [SerializeField] private WeaponSway weaponSway;

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

    [SerializeField] private GameObject sparkEffect;

    public static PlayerController instance;

    protected override void Awake()
    {
        instance = this;

        base.Awake();
    }

    protected override void Start()
    {
        controller = GetComponent<CharacterController>();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (cameraTransform == null)
            cameraTransform = GetComponentInChildren<Camera>().transform;

        controller.height = normalHeight;

        base.Start();
    }

    protected override void Update()
    {
        HandleMouseLook();
        HandleMovement();
        RecoverFromJolt();
        StaminaRecover();

        base.Update();
    }

    private void HandleMouseLook()
    {
        if(!isExecuting)
        {
            Vector2 mouseDelta = Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;
            float adjustedSensitivity = mouseSensitivity * 0.1f;

            float mouseX = mouseDelta.x * adjustedSensitivity;
            float mouseY = mouseDelta.y * adjustedSensitivity;

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

        // 1. Check WASD Inputs (ONLY if not locked by an attack)
        float x = 0f;
        float z = 0f;

        if (Keyboard.current != null && !isAttacking && !isExecuting)
        {
            if (Keyboard.current.dKey.isPressed) x += 1f;
            if (Keyboard.current.aKey.isPressed) x -= 1f;
            if (Keyboard.current.wKey.isPressed) z += 1f;
            if (Keyboard.current.sKey.isPressed) z -= 1f;
        }

        Vector3 standardMove = transform.right * x + transform.forward * z;

        // 2. Slide Activation Logic (Disabled while attacking)
        bool cPressed = Keyboard.current != null && Keyboard.current.cKey.wasPressedThisFrame;

        if (cPressed && isGrounded && !isSliding && !isAttacking && !isExecuting && standardMove.sqrMagnitude > 0.01f)
        {
            if(CharacterStaminaComponent.CurrentStamina > 0f)
            {
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
            // Count down the attack lock
            attackLockTimer -= Time.deltaTime;

            // Smoothly decay the physical lunge momentum over time
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
            bool isSprinting = Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed && CharacterStaminaComponent.CurrentStamina > 0f;

            if (isSprinting && (Keyboard.current.dKey.isPressed 
                || Keyboard.current.sKey.isPressed 
                || Keyboard.current.aKey.isPressed 
                || Keyboard.current.wKey.isPressed))
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

        // 4. Jump Logic (Disabled while sliding or attacking)
        bool jumpPressed = Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
        if (jumpPressed && isGrounded && !isSliding && !isAttacking)
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
            SoundManager.instance.ParriedSound();
            BlockedOrParriedEffect(enemy.eyesTransform.position);
            StaminaDepleted(enemy.staminaDamage / 3);
        }
        else
        {
            enemy.AttackSuccessful();

            if (playerMeleeAttack.isBlocking)
            {
                BlockedOrParriedEffect(enemy.eyesTransform.position);
                playerMeleeAttack.PerformBlock();
                SoundManager.instance.SwordSound_Metal();
                cameraBob.TriggerShake(0.293f, 0.05f);
                StaminaDepleted(enemy.staminaDamage);
            }
            else
            {
                postProcressEffect.TriggerDamageEffect();
                SoundManager.instance.SwordSound_Flesh();
                SoundManager.instance.PlayerHurtSound();
                cameraBob.TriggerShake(0.293f, 0.1f);
                CharacterHealthComponent.TakeDamage(enemy.attackDamage);
                TriggerMeleeJolt(new Vector3(30f, 0f, 0f));
            }
        }
    }

    public void TakeArrowHit(ArrowProjectile arrow)
    {
        if (playerMeleeAttack.currentParry > 0f)
        {
            SoundManager.instance.ParriedSound();
            StaminaDepleted(arrow.staminaCost / 3);
        }
        else
        {
            if (playerMeleeAttack.isBlocking)
            {
                BlockedOrParriedEffect(arrow.gameObject.transform.position);
                playerMeleeAttack.PerformBlock();
                SoundManager.instance.SwordSound_Metal();
                cameraBob.TriggerShake(0.293f, 0.05f);
                StaminaDepleted(arrow.staminaCost);
            }
            else
            {
                postProcressEffect.TriggerDamageEffect();
                SoundManager.instance.SwordSound_Flesh();
                SoundManager.instance.PlayerHurtSound();
                cameraBob.TriggerShake(0.293f, 0.1f);
                CharacterHealthComponent.TakeDamage(arrow.damage);
                TriggerMeleeJolt(new Vector3(30f, 0f, 0f));
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
        GameObject sparkObj = Instantiate(sparkEffect);
        sparkObj.transform.position = pos;
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
}