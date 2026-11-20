using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
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

    [Header("Guard")]
    [Tooltip("Blocks and parries only stop attacks coming from within this many degrees left/right of where you look.")]
    [SerializeField] private float guardAngle = 70f;
    public float GuardAngle => guardAngle;
    [SerializeField] private CameraFollow cameraFollow;
    [SerializeField] private GameObject deadCamPrefab;
    [SerializeField] private CameraPostProcessEffect postProcressEffect;
    public CameraPostProcessEffect PostProcessEffect => postProcressEffect;
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

    // Held by a Brute: can't move, attack or be hit by others; mash Jump to break free
    [HideInInspector] public bool isHeld = false;
    private BruteGrab heldBy;
    private Vector3 pushVelocity;
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
        PlayerRings.EnsureOn(gameObject); // ring effects

        if (cameraTransform == null)
            cameraTransform = GetComponentInChildren<Camera>().transform;

        controller.height = normalHeight;
        image_Foot.enabled = false;

        base.Start();
    }

    protected override void Update()
    {
        if (CharacterHealthComponent.CurrentHP > 0)
        {
            if (PauseGame.instance != null && EquipmentLoadOut.instance != null)
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
        if (InputSchemeManager.instance.isKickPressed && !isKicking && !isHeld && PlayerWeaponManager.instance.currentState_Righthand == PerformActionState.Idle
            && PlayerWeaponManager.instance.currentState_LeftHand == PerformActionState.Idle)
        {
            if (CharacterStaminaComponent.CurrentStamina > 0f)
            {
                // 1. Immediately trigger the Kick visuals and state
                

                // 2. Check if there is an enemy in front we can execute
                EnemyEntity targetEnemy = PlayerWeaponManager.instance.GetEnemyInFront();

                if (targetEnemy != null && targetEnemy.isStunned && !isExecuting)
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

        // Ember Guard: stored embers burst out with the kick
        bool releasedEmbers = PlayerPerkManager.Instance.TryReleaseEmbers(cameraTransform.position, cameraTransform.forward);
        float finalKickDamage = kickDamage * PlayerPerkManager.Instance.OutgoingDamageMultiplier; // Blood Lust / Glass Jaw

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

                    float knockbackValue = PlayerPerkManager.Instance.GetJumpKickKnockback(11f);

                    targetEnemy.GotKicked(hit.point, finalKickDamage, knockbackValue);
                    if (targetEnemy.Elite != null) { targetEnemy.Elite.OnKicked(); } // breaks a Warded elite's ward
                    PlayerPerkManager.Instance.OnEnemyKicked(targetEnemy, transform.position, knockbackValue); // Warden's Boot
                    hitSomething = true;
                }
                else if (destructible != null && processedTargets.Add(destructible))
                {
                    hitSomething = true;

                    // Battering Ram: a kicked door blasts off its hinges
                    if (PlayerPerkManager.Instance.TryBatteringRam(destructible, hit.collider, hit.point, transform.position))
                    {
                        continue;
                    }

                    destructible.TakeDamage(finalKickDamage, hit.collider, hit.point, cameraTransform.forward);

                    if (fractureTrigger != null)
                    {
                        fractureTrigger.TriggerMaterialSound_Hit();
                    }
                }
            }
        }

        if (!hitSomething && !releasedEmbers)
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

            if (ControllerValueInputManager.instance)
            {
                mouseX *= ControllerValueInputManager.instance.modifiedValue;
                mouseY *= ControllerValueInputManager.instance.modifiedValue;
            }

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
        if (!isAttacking && !isExecuting && !isHeld)
        {
            moveValue = InputSchemeManager.instance.input_Move.ReadValue<Vector2>();
        }

        Vector3 standardMove = transform.right * moveValue.x + transform.forward * moveValue.y;

        if (InputSchemeManager.instance.isDashPressed && isGrounded && !isSliding && !isAttacking && !isExecuting && !isHeld && standardMove.sqrMagnitude > 0.01f)
        {
            if(CharacterStaminaComponent.CurrentStamina > 0f)
            {
                StaminaEffect(false);
                RumbleManager.instance.RumblePulse(1f, 1.5f, slideDuration);
                StaminaDepleted(slideStaminaCost);
                isSliding = true;
                slideTimer = slideDuration;
                slideDirection = standardMove.normalized;
                if (PlayerPerkManager.Instance != null) { PlayerPerkManager.Instance.OnSlideStarted(); } // Greased Slide
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
            if (PlayerPerkManager.Instance != null) { PlayerPerkManager.Instance.OnSliding(transform.position, slideDirection); } // Greased Slide

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

        /*if (PlayerPerkManager.Instance != null)
        {
            currentSpeed *= PlayerPerkManager.Instance.CurrentMoveSpeedMultiplier_OnEnemyKill;
        }*/

        currentSpeed *= PlayerRings.MoveSpeedMultiplier; // rings

        // Held by a Brute: pinned in front of him
        if (isHeld && heldBy != null)
        {
            finalMove = Vector3.zero;
            Vector3 toHold = heldBy.HoldPoint - transform.position;
            toHold.y = 0f;
            controller.Move(toHold * Mathf.Min(1f, 10f * Time.deltaTime));
        }

        // Shoved away (breaking free)
        if (pushVelocity.sqrMagnitude > 0.01f)
        {
            controller.Move(pushVelocity * Time.deltaTime);
            pushVelocity = Vector3.MoveTowards(pushVelocity, Vector3.zero, 14f * Time.deltaTime);
        }

        controller.Move(finalMove * currentSpeed * Time.deltaTime);

        if (InputSchemeManager.instance.isJumpPressed && isGrounded && !isSliding && !isAttacking && !isHeld)
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
        if(isExecuting || isHeld) { return; } // while a Brute holds you, nobody else can hit you

        // Blocks and parries only work against attacks from in front (the way the camera faces)
        bool isFacingAttacker = IsFacingAttack(enemy.transform.position);

        if (isFacingAttacker && (playerWeaponManager.currentParryLeft > 0f || playerWeaponManager.currentParryRight > 0f))
        {
            PlayerPerkManager.Instance.OnParrySuccess();
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

            if (isFacingAttacker && (playerWeaponManager.isBlockingLeft || playerWeaponManager.isBlockingRight))
            {
                PlayerPerkManager.Instance.OnHitBlocked(); // Ember Guard
                BlockedOrParriedEffect(enemy.eyesTransform.position);
                //playerWeaponManager.PerformBlock();
                cameraBob.TriggerShake(0.293f, 0.05f);
                
                RumbleManager.instance.RumblePulse(1f, 3f, 0.4f);

                if(playerWeaponManager.isBlockingLeft)
                {
                    StaminaEffect(false);
                    StaminaDepleted(enemy.staminaDamage * playerWeaponManager.GetBlockStaminaModifier(true) * GameManager.instance.playerTakeStaminaDamage);
                    SoundManager.instance.BlockOrParrySound(playerWeaponManager.leftHandWeapon.audioClips_BlockSound ,transform.position);
                }
                else if(playerWeaponManager.isBlockingRight)
                {
                    StaminaEffect(false);
                    StaminaDepleted(enemy.staminaDamage * playerWeaponManager.GetBlockStaminaModifier(false) * GameManager.instance.playerTakeStaminaDamage);
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

                float rawDamage = enemy.attackDamage * GameManager.instance.playerTakeHPDamage;
                float calculatedDamage = PlayerPerkManager.Instance.CalculateIncomingDamage(rawDamage);
                CharacterHealthComponent.TakeDamage(calculatedDamage);
                if (enemy.Elite != null) { enemy.Elite.OnDealtDamage(calculatedDamage); } // Vampiric elites
                TriggerMeleeJolt(new Vector3(30f, 0f, 0f));
                RumbleManager.instance.RumblePulse(1f, 2.5f, 0.2f);
            }
        }
    }

    public void TakeArrowHit(ArrowProjectile arrow)
    {
        if (isExecuting || isHeld) { return; }

        bool isFacingAttacker = IsFacingAttack(transform.position - arrow.transform.forward * 3f);

        if (isFacingAttacker && (playerWeaponManager.currentParryLeft > 0f || playerWeaponManager.currentParryRight > 0f))
        {
            PlayerPerkManager.Instance.OnParrySuccess();
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
            if (isFacingAttacker && (playerWeaponManager.isBlockingLeft || playerWeaponManager.isBlockingRight))
            {
                BlockedOrParriedEffect(arrow.gameObject.transform.position);
                //playerWeaponManager.PerformBlock();
                cameraBob.TriggerShake(0.293f, 0.05f);
                RumbleManager.instance.RumblePulse(1f, 2.5f, 0.1f);

                if(playerWeaponManager.isBlockingLeft)
                {
                    StaminaEffect(false);
                    StaminaDepleted(arrow.staminaDamage * playerWeaponManager.GetBlockStaminaModifier(true) * GameManager.instance.playerTakeStaminaDamage);
                    SoundManager.instance.BlockOrParrySound(playerWeaponManager.leftHandWeapon.audioClips_BlockSound ,transform.position);
                }
                else if(playerWeaponManager.isBlockingRight)
                {
                    StaminaEffect(false);
                    StaminaDepleted(arrow.staminaDamage * playerWeaponManager.GetBlockStaminaModifier(false) * GameManager.instance.playerTakeStaminaDamage);
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

                float rawDamage = arrow.damage * GameManager.instance.playerTakeHPDamage;
                float calculatedDamage = PlayerPerkManager.Instance.CalculateIncomingDamage(rawDamage);

                CharacterHealthComponent.TakeDamage(calculatedDamage);
                TriggerMeleeJolt(new Vector3(30f, 0f, 0f));
                RumbleManager.instance.RumblePulse(1f, 2.5f, 0.1f);
            }
        }
    }

    /// <summary>
    /// Damage that can't be blocked or parried (e.g. a Volatile elite exploding). Still goes through the
    /// damage perks (Iron Skin, Last Stand, Revive Totem...).
    /// </summary>
    public void TakeUnblockableHit(float rawDamage, Vector3 source)
    {
        if (isExecuting || isHeld || CharacterHealthComponent.CurrentHP <= 0f) { return; }
        if (AsyncLoaderManager.instance != null && AsyncLoaderManager.instance.isTransitioning) { return; }

        postProcressEffect.TriggerDamageEffect();
        SoundManager.instance.PlayerHurtSound(transform.position);
        cameraBob.TriggerShake(0.3f, 0.12f);
        HealthEffect(false);

        float calculatedDamage = PlayerPerkManager.Instance.CalculateIncomingDamage(rawDamage * GameManager.instance.playerTakeHPDamage);
        CharacterHealthComponent.TakeDamage(calculatedDamage);
        TriggerMeleeJolt(new Vector3(30f, 0f, 0f));
        RumbleManager.instance.RumblePulse(1f, 3f, 0.3f);
    }

    // ---------- Brute grab ----------

    public void BeginHeld(BruteGrab by)
    {
        isHeld = true;
        heldBy = by;
        pushVelocity = Vector3.zero;
        if (isSliding)
        {
            isSliding = false;
            controller.height = normalHeight;
        }
    }

    public void EndHeld(Vector3 push)
    {
        isHeld = false;
        heldBy = null;
        push.y = 0f;
        pushVelocity = push;
    }

    /// <summary>The Brute's crushing hug (only he can hurt you while you're held). Goes through the damage perks.</summary>
    public void TakeGrabDamage(float rawDamage, bool strongFeedback)
    {
        if (CharacterHealthComponent.CurrentHP <= 0f) { return; }
        if (AsyncLoaderManager.instance != null && AsyncLoaderManager.instance.isTransitioning) { return; }

        HealthEffect(false);
        cameraBob.TriggerShake(0.15f, strongFeedback ? 0.08f : 0.04f);
        if (strongFeedback)
        {
            postProcressEffect.TriggerDamageEffect();
            SoundManager.instance.PlayerHurtSound(transform.position);
        }

        float calculatedDamage = PlayerPerkManager.Instance.CalculateIncomingDamage(rawDamage * GameManager.instance.playerTakeHPDamage);
        CharacterHealthComponent.TakeDamage(calculatedDamage);
        RumbleManager.instance.RumblePulse(0.6f, 1.5f, 0.1f);
    }

    // Is the attack coming from inside the guard cone (in front of where the camera looks)?
    private bool IsFacingAttack(Vector3 attackOrigin)
    {
        return InteractFacing.IsPlayerFacing(attackOrigin, guardAngle);
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
                float staminaToRecover = reStaminaRate * Time.deltaTime * reStaminaSpeed * PlayerRings.StaminaRegenMultiplier;
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
        // Attacking mid-slide or mid-air: keep the slide / air movement, no lunge and no movement lock
        if (isSliding || !isGrounded) { return; }

        isAttacking = true;
        attackLockTimer = lockDuration;

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