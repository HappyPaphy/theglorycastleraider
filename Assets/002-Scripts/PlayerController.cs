using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Playables;
using UnityEngine.UI;

public enum FootState
{
    Idle,
    Kick,
    Slide
}

[RequireComponent(typeof(CharacterController))]
public class PlayerController : PlayerEntity
{
    [Header("Components")]
    public CoinWallet coinWallet;
    [SerializeField] private CameraFollow cameraFollow;
    [SerializeField] private GameObject deadCamPrefab;
    [SerializeField] private CameraPostProcessEffect postProcressEffect;
    public CameraBob cameraBob;
    [SerializeField] private PlayerWeaponManager playerWeaponManager;
    [SerializeField] private WeaponSway weaponSway;

    [Header("Kick")]
    public FootState footState;
    [SerializeField] private float kickRange = 1f;
    [SerializeField] private float kickRadius = 0.5f;
    [SerializeField] private LayerMask kickLayerMask;
    [SerializeField] private Image image_Foot;
    [SerializeField] private Animator anim_Foot;

    [Header("Movement Settings")]
    [SerializeField] private float walkSpeed = 6f;
    [SerializeField] private float sprintSpeed = 10f;
    [SerializeField] private float sprintStaminaCost = 0.1f;
    [SerializeField] private float gravity = -19.62f;
    [SerializeField] private float jumpHeight = 1.5f;

    [Header("Slide Settings")]
    [SerializeField] private float kickStaminaCost = 15f;
    [SerializeField] private float kickDamage = 5f;
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

    public bool isKicking = false;

    public static PlayerController instance;

    


    protected override void Awake()
    {
        instance = this;

        base.Awake();
    }

    protected override void Start()
    {
        controller = GetComponent<CharacterController>();

        if (cameraTransform == null)
            cameraTransform = GetComponentInChildren<Camera>().transform;

        controller.height = normalHeight;
        image_Foot.enabled = false;

        base.Start();
    }

    protected override void Update()
    {
        if(CharacterHealthComponent.CurrentHP > 0)
        {
            if(PauseGame.instance != null && EquipmentLoadOut.instance != null)
            {
                if (!PauseGame.instance.IsPaused && !EquipmentLoadOut.instance.isPanelActive)
                {
                    ChooseAnimations();
                    HandleKick();
                    HandleMouseLook();
                    HandleMovement();
                    RecoverFromJolt();
                    StaminaRecover();
                }
            }
        }

        base.Update();
    }

    private void HandleKick()
    {
        if (InputSchemeManager.instance.isKickPressed && !isKicking && PlayerWeaponManager.instance.currentState_Righthand == PerformActionState.Idle
            && PlayerWeaponManager.instance.currentState_LeftHand == PerformActionState.Idle)
        {
            if (CharacterStaminaComponent.CurrentStamina > 0f)
            {
                // 1. Immediately trigger the Kick visuals and state
                

                // 2. Check if there is an enemy in front we can execute
                EnemyEntity targetEnemy = PlayerWeaponManager.instance.GetEnemyInFront();

                if (targetEnemy != null && targetEnemy.isStunned)
                {
                    // Branch A: Execution!
                    RumbleManager.instance.RumblePulse(1f, 2.5f, 0.3f);
                    PlayerWeaponManager.instance.StartExecutionRoutine(targetEnemy);
                }
                else
                {
                    EnableKick();
                    // Branch B: Standard Kick Raycast
                    StartCoroutine(KickSequence());
                }
            }
        }
    }

    public void EnableKick()
    {
        image_Foot.enabled = true;
        isKicking = true;
        anim_Foot.Play("Kick", 0, 0f);
        footState = FootState.Kick;
    }

    private IEnumerator KickSequence()
    {
        yield return new WaitForSeconds(0.213f);
        PerformKick();

        yield return new WaitForSeconds(0.225f);
        EndKickVisuals(); // Replaced individual resets with the unified cleanup
    }

    public void EndKickVisuals()
    {
        // Safely resets the foot states and hand states when either a normal kick OR an execution finishes
        isKicking = false;
        image_Foot.enabled = false;
        footState = FootState.Idle;
        PlayerWeaponManager.instance.currentState_LeftHand = PerformActionState.Idle;
        PlayerWeaponManager.instance.currentState_Righthand = PerformActionState.Idle;
    }

    private void PerformKick()
    {
        StaminaEffect(false);
        RumbleManager.instance.RumblePulse(1f, 2.5f, 0.1f);
        StaminaDepleted(kickStaminaCost);

        Vector3 joltDirection = new Vector3(-20f, 0f, 0f);
        Vector3 castStart = cameraTransform.position - (cameraTransform.forward * kickRadius);
        Ray rayAttack = new Ray(castStart, cameraTransform.forward);

        float totalRange = kickRange + kickRadius;
        RaycastHit[] hits = Physics.SphereCastAll(rayAttack, kickRadius, totalRange, kickLayerMask);
        bool hitSomething = false;

        if (hits.Length > 0)
        {
            HashSet<Component> processedTargets = new HashSet<Component>();

            foreach (RaycastHit hit in hits)
            {
                EnemyEntity targetEnemy = hit.collider.GetComponent<EnemyEntity>();
                Fracture destructible = hit.collider.GetComponent<Fracture>();
                FractureTrigger fractureTrigger = hit.collider.GetComponent<FractureTrigger>();

                if (targetEnemy != null && processedTargets.Add(targetEnemy))
                {
                    SoundManager.instance.KickSound_Human(transform.position);
                    targetEnemy.GotKicked(hit.point, kickDamage, 8f);
                    hitSomething = true;
                }
                else if (destructible != null && processedTargets.Add(destructible))
                {
                    destructible.TakeDamage(kickDamage, hit.collider, hit.point);
                    hitSomething = true;

                    if (fractureTrigger != null)
                    {
                        fractureTrigger.TriggerMaterialSound_Hit();
                    }
                }
            }
        }

        if (!hitSomething)
        {
            SoundManager.instance.KickSound_Air(transform.position);
        }

        TriggerMeleeJolt(joltDirection);
    }

    public void StaminaEffect(bool isPositiveEffect)
    {
        StartCoroutine(GameManager.instance.GainObjectScaleFeedback(slider_Stamina.gameObject, isPositiveEffect, new Vector3(0.63f, 0.63f, 0.63f)));
    }

    public void HealthEffect(bool isPositiveEffect)
    {
        StartCoroutine(GameManager.instance.GainObjectScaleFeedback(slider_HP.gameObject, isPositiveEffect, new Vector3(0.7f, 0.7f, 0.7f)));
    }

    public void ManaEffect(bool isPositiveEffect)
    {
        StartCoroutine(GameManager.instance.GainObjectScaleFeedback(slider_Ultimate.gameObject, isPositiveEffect, new Vector3(0.57f, 0.57f, 0.57f)));
    }

    private void HandleMouseLook()
    {
        if(playerWeaponManager.isQuickMenuActive) { return; }

        if(!isExecuting)
        {
            Vector2 lookValue = InputSchemeManager.instance.input_Look.ReadValue<Vector2>();

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
            moveValue = InputSchemeManager.instance.input_Move.ReadValue<Vector2>();
        }

        Vector3 standardMove = transform.right * moveValue.x + transform.forward * moveValue.y;

        if (InputSchemeManager.instance.isDashPressed && isGrounded && !isSliding && !isAttacking && !isExecuting && standardMove.sqrMagnitude > 0.01f)
        {
            if(CharacterStaminaComponent.CurrentStamina > 0f)
            {
                StaminaEffect(false);
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
            bool isSprinting = InputSchemeManager.instance.isSprintHeld && CharacterStaminaComponent.CurrentStamina > 0f;

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

        if (InputSchemeManager.instance.isJumpPressed && isGrounded && !isSliding && !isAttacking)
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
        if (playerWeaponManager.currentParryLeft > 0f || playerWeaponManager.currentParryRight > 0f)
        {
            BlockedOrParriedEffect(enemy.eyesTransform.position);
            RumbleManager.instance.RumblePulse(1f, 3f, 0.4f);
            cameraBob.TriggerShake(0.293f, 0.05f);

            if (playerWeaponManager.currentParryLeft > 0f)
            {
                enemy.GotParried(true);
                SoundManager.instance.BlockOrParrySound(playerWeaponManager.leftHandWeapon.audioClips_ParrySound, transform.position);
            }
            else if (playerWeaponManager.currentParryRight > 0f)
            {
                enemy.GotParried(false);
                SoundManager.instance.BlockOrParrySound(playerWeaponManager.rightHandWeapon.audioClips_ParrySound, transform.position);
            }
        }
        else
        {
            enemy.AttackSuccessful();

            if (playerWeaponManager.isBlockingLeft || playerWeaponManager.isBlockingRight)
            {
                BlockedOrParriedEffect(enemy.eyesTransform.position);
                //playerWeaponManager.PerformBlock();
                cameraBob.TriggerShake(0.293f, 0.05f);
                
                RumbleManager.instance.RumblePulse(1f, 3f, 0.4f);

                if(playerWeaponManager.isBlockingLeft)
                {
                    StaminaEffect(false);
                    StaminaDepleted(enemy.staminaDamage * playerWeaponManager.leftHandWeapon.blockStaminaDamageModifier * GameManager.instance.playerTakeStaminaDamage);
                    SoundManager.instance.BlockOrParrySound(playerWeaponManager.leftHandWeapon.audioClips_BlockSound ,transform.position);
                }
                else if(playerWeaponManager.isBlockingRight)
                {
                    StaminaEffect(false);
                    StaminaDepleted(enemy.staminaDamage * playerWeaponManager.rightHandWeapon.blockStaminaDamageModifier * GameManager.instance.playerTakeStaminaDamage);
                    SoundManager.instance.BlockOrParrySound(playerWeaponManager.rightHandWeapon.audioClips_BlockSound, transform.position);
                }
            }
            else
            {
                if (AsyncLoaderManager.instance != null)
                {
                    if (AsyncLoaderManager.instance.isTransitioning) { return; }
                }

                postProcressEffect.TriggerDamageEffect();
                SoundManager.instance.SwordSound_Flesh(transform.position);
                SoundManager.instance.PlayerHurtSound(transform.position);
                cameraBob.TriggerShake(0.293f, 0.1f);
                HealthEffect(false);
                CharacterHealthComponent.TakeDamage(enemy.attackDamage * GameManager.instance.playerTakeHPDamage);
                TriggerMeleeJolt(new Vector3(30f, 0f, 0f));
                RumbleManager.instance.RumblePulse(1f, 2.5f, 0.2f);
            }
        }
    }

    public void TakeArrowHit(ArrowProjectile arrow)
    {
        if (playerWeaponManager.currentParryLeft > 0f || playerWeaponManager.currentParryRight > 0f)
        {
            BlockedOrParriedEffect(arrow.gameObject.transform.position);
            RumbleManager.instance.RumblePulse(1f, 2.5f, 0.1f);
            cameraBob.TriggerShake(0.293f, 0.05f);

            if (playerWeaponManager.currentParryLeft > 0f)
            {
                SoundManager.instance.BlockOrParrySound(playerWeaponManager.leftHandWeapon.audioClips_ParrySound, transform.position);
            }
            else if(playerWeaponManager.currentParryRight > 0f)
            {
                SoundManager.instance.BlockOrParrySound(playerWeaponManager.rightHandWeapon.audioClips_ParrySound, transform.position);
            }
        }
        else
        {
            if (playerWeaponManager.isBlockingLeft ||playerWeaponManager.isBlockingRight)
            {
                BlockedOrParriedEffect(arrow.gameObject.transform.position);
                //playerWeaponManager.PerformBlock();
                cameraBob.TriggerShake(0.293f, 0.05f);
                RumbleManager.instance.RumblePulse(1f, 2.5f, 0.1f);

                if(playerWeaponManager.isBlockingLeft)
                {
                    StaminaEffect(false);
                    StaminaDepleted(arrow.staminaDamage * playerWeaponManager.leftHandWeapon.blockStaminaDamageModifier * GameManager.instance.playerTakeStaminaDamage);
                    SoundManager.instance.BlockOrParrySound(playerWeaponManager.leftHandWeapon.audioClips_BlockSound ,transform.position);
                }
                else if(playerWeaponManager.isBlockingRight)
                {
                    StaminaEffect(false);
                    StaminaDepleted(arrow.staminaDamage * playerWeaponManager.rightHandWeapon.blockStaminaDamageModifier * GameManager.instance.playerTakeStaminaDamage);
                    SoundManager.instance.BlockOrParrySound(playerWeaponManager.rightHandWeapon.audioClips_BlockSound, transform.position);
                }
            }
            else
            {
                if(AsyncLoaderManager.instance != null)
                {
                    if(AsyncLoaderManager.instance.isTransitioning) { return; }
                }

                postProcressEffect.TriggerDamageEffect();
                SoundManager.instance.SwordSound_Flesh(transform.position);
                SoundManager.instance.BowSound_Hit(transform.position);
                SoundManager.instance.PlayerHurtSound(transform.position);
                cameraBob.TriggerShake(0.293f, 0.1f);
                HealthEffect(false);
                CharacterHealthComponent.TakeDamage(arrow.damage * GameManager.instance.playerTakeHPDamage);
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

    public void DepleteUltimate(float value)
    {
        CharacterUltimateComponent.DepleteUltimate(value);
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

    public void BlockedOrParriedEffect(Vector3 pos)
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
        playerWeaponManager.image_LeftHand.enabled = false;
        playerWeaponManager.image_RightHand.enabled = false;

        base.Die();
    }

    private static readonly Dictionary<FootState, int> StateToHash = new Dictionary<FootState, int>
    {
        { FootState.Idle, Animator.StringToHash("IsIdle") },
        { FootState.Kick, Animator.StringToHash("IsKick") },
        { FootState.Slide, Animator.StringToHash("IsSlide") }
    };

    private int lastKickStateHash;

    private void ChooseAnimations()
    {
        // Handle Left Hand Animator
        if (StateToHash.TryGetValue(footState, out int lastHash))
        {
            if (lastHash != lastKickStateHash)
            {
                if (lastKickStateHash != 0 && anim_Foot != null) anim_Foot.SetBool(lastKickStateHash, false);
                if (anim_Foot != null) anim_Foot.SetBool(lastHash, true);
                lastKickStateHash = lastHash;
            }
        }
    }
}