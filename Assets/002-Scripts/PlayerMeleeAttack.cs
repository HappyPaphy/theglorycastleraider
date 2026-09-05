using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Playables;

public enum PerformActionState
{
    Idle,
    Press1,
    Press2,
    Press3,
    Hold1,
    Hold2,
    Hold3,
    Kick,
    Execute
}

public class PlayerMeleeAttack : MonoBehaviour
{
    [SerializeField] private CameraFollow cameraFollow;
    [SerializeField] private CameraBob cameraBob;
    [SerializeField] private PerformActionState currentState_LeftHand;
    [SerializeField] private PerformActionState currentState_Righthand;

    [SerializeField] private Animator anim_LeftHand;
    [SerializeField] private Animator anim_RightHand;

    [SerializeField] private Weapon rightHandWeapon;
    [SerializeField] private Weapon leftHandWeapon;

    [Header("Melee Settings")]
    [SerializeField] private float executeStaminaCost = 20f;
    [SerializeField] private float kickStaminaCost = 15f;
    [SerializeField] private float meleeStaminaCost = 18f;
    [SerializeField] private float meleeDamage = 10f;
    [SerializeField] private float kickDamage = 5f;
    [SerializeField] private float attackRange = 1.8f;
    [SerializeField] private float attackRadius = 1f;
    [SerializeField] private float lungeForce = 12f;

    [Tooltip("Set this to the layer your enemies are on")]
    [SerializeField] private LayerMask attackLayerMask;

    [Header("References")]
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private PlayerController playerController;

    private float lastAttackTimeRight;
    private float lastAttackTimeLeft;
    private int comboStepRight = 0;
    private int comboStepLeft = 0;
    private float attackStateResetTimerRight = 0f;
    private float attackStateResetTimerLeft = 0f;

    [HideInInspector] public bool isBlockingRight = false;
    [HideInInspector] public bool isBlockingLeft = false;
    [HideInInspector] public float currentParryRight = 0f;
    [HideInInspector] public float currentParryLeft = 0f;

    private float parryDuration = 0.3f;
    private float blockCooldownDuration = 0.15f;
    private float currentBlockCooldownRight = 0f;
    private float currentBlockCooldownLeft = 0f;
    private bool isKicking = false;

    private void Start()
    {
        currentState_LeftHand = PerformActionState.Idle;
        currentState_Righthand = PerformActionState.Idle;
    }

    void Update()
    {
        if (PauseGame.instance.IsPaused || playerController.CharacterHealthComponent.CurrentHP <= 0) return;

        ChooseAnimations();

        if (currentBlockCooldownRight > 0f) currentBlockCooldownRight -= Time.deltaTime;
        if (currentBlockCooldownLeft > 0f) currentBlockCooldownLeft -= Time.deltaTime;
        if (currentParryRight > 0f) currentParryRight -= Time.deltaTime;
        if (currentParryLeft > 0f) currentParryLeft -= Time.deltaTime;

        HandleRightHandInput();
        HandleLeftHandInput();


        if (playerController.isKickPressed && !isKicking && currentState_Righthand == PerformActionState.Idle && currentState_LeftHand == PerformActionState.Idle)
        {
            if (playerController.CharacterStaminaComponent.CurrentStamina > 0f)
            {
                EnemyEntity targetEnemy = GetEnemyInFront();

                if (targetEnemy != null && targetEnemy.isStunned)
                {
                    RumbleManager.instance.RumblePulse(1f, 2.5f, 0.3f);
                    playerController.StaminaDepleted(executeStaminaCost);
                    StartCoroutine(ExecutionSequence(targetEnemy));
                }
                else
                {
                    isKicking = true;
                    currentState_Righthand = PerformActionState.Kick;
                    StartCoroutine(KickSequence());
                }
            }
        }

        // State Reset Timers
        if (currentState_Righthand != PerformActionState.Idle && currentState_Righthand != PerformActionState.Hold1 && !isKicking && !playerController.isExecuting)
        {
            attackStateResetTimerRight -= Time.deltaTime;
            if (attackStateResetTimerRight <= 0f && currentBlockCooldownRight <= 0f)
            {
                currentState_Righthand = PerformActionState.Idle;
            }
        }

        if (currentState_LeftHand != PerformActionState.Idle && currentState_LeftHand != PerformActionState.Hold1 && !isKicking && !playerController.isExecuting)
        {
            attackStateResetTimerLeft -= Time.deltaTime;
            if (attackStateResetTimerLeft <= 0f && currentBlockCooldownLeft <= 0f)
            {
                currentState_LeftHand = PerformActionState.Idle;
            }
        }
    }

    private void HandleRightHandInput()
    {
        if (rightHandWeapon == null) return;

        // Shield / Blocking logic uses Hold1
        bool holdingShieldRight = rightHandWeapon.weaponCategory == WeaponCategory.Shield && playerController.isRightHandHeld;
        if (holdingShieldRight && currentBlockCooldownRight <= 0f && !isKicking)
        {
            if (playerController.CharacterStaminaComponent.CurrentStamina > 0f && !isBlockingRight)
            {
                RumbleManager.instance.RumblePulse(1f, 2f, 0.15f);
                isBlockingRight = true;
                currentState_Righthand = PerformActionState.Hold1;
                if (anim_RightHand != null) anim_RightHand.speed = 1f;
                currentParryRight = parryDuration;
            }
        }
        else if (!playerController.isRightHandHeld && isBlockingRight)
        {
            if (anim_RightHand != null) anim_RightHand.speed = 1f;
            isBlockingRight = false;
            currentBlockCooldownRight = blockCooldownDuration;
            currentState_Righthand = PerformActionState.Idle;
        }

        // Pyromancy / Magic logic uses Press1 (on press) and Hold1 (while holding)
        if (rightHandWeapon.weaponCategory == WeaponCategory.PyromancyFlame)
        {
            if (playerController.isRightHandPressed && currentBlockCooldownRight <= 0f && !isKicking)
            {
                if (playerController.CharacterStaminaComponent.CurrentStamina > 0f)
                    PerformPyromancy(rightHandWeapon, isLeftHand: false, isHold: false);
            }
            else if (playerController.isRightHandHeld && currentState_Righthand == PerformActionState.Press1)
            {
                // Transition or maintain magic channel state via Hold1 if desired
                currentState_Righthand = PerformActionState.Hold1;
            }
            else if (!playerController.isRightHandHeld && currentState_Righthand == PerformActionState.Hold1)
            {
                currentState_Righthand = PerformActionState.Idle;
            }
        }

        // Melee Attack Logic
        if (rightHandWeapon.weaponCategory == WeaponCategory.Melee && playerController.isRightHandPressed && currentBlockCooldownRight <= 0f && !isKicking)
        {
            if (playerController.CharacterStaminaComponent.CurrentStamina > 0f)
            {
                if (Time.time >= lastAttackTimeRight + rightHandWeapon.attackCooldown)
                {
                    PerformAttack(rightHandWeapon, isLeftHand: false);
                }
            }
        }
    }

    private void HandleLeftHandInput()
    {
        if (leftHandWeapon == null) return;

        // Shield / Blocking logic uses Hold1
        bool holdingShieldLeft = leftHandWeapon.weaponCategory == WeaponCategory.Shield && playerController.isLeftHandHeld;
        if (holdingShieldLeft && currentBlockCooldownLeft <= 0f && !isKicking)
        {
            if (playerController.CharacterStaminaComponent.CurrentStamina > 0f && !isBlockingLeft)
            {
                RumbleManager.instance.RumblePulse(1f, 2f, 0.15f);
                isBlockingLeft = true;
                currentState_LeftHand = PerformActionState.Hold1;
                if (anim_LeftHand != null) anim_LeftHand.speed = 1f;
                currentParryLeft = parryDuration;
            }
        }
        else if (!playerController.isLeftHandHeld && isBlockingLeft)
        {
            if (anim_LeftHand != null) anim_LeftHand.speed = 1f;
            isBlockingLeft = false;
            currentBlockCooldownLeft = blockCooldownDuration;
            currentState_LeftHand = PerformActionState.Idle;
        }

        // Pyromancy / Magic logic uses Press1 (on press) and Hold1 (while holding)
        if (leftHandWeapon.weaponCategory == WeaponCategory.PyromancyFlame)
        {
            if (playerController.isLeftHandPressed && currentBlockCooldownLeft <= 0f && !isKicking)
            {
                if (playerController.CharacterStaminaComponent.CurrentStamina > 0f)
                {
                    PerformPyromancy(leftHandWeapon, isLeftHand: true, isHold: false);
                }
            }
            else if (playerController.isLeftHandHeld && currentState_LeftHand == PerformActionState.Press1)
            {
                currentState_LeftHand = PerformActionState.Hold1;
            }
            else if (!playerController.isLeftHandHeld && currentState_LeftHand == PerformActionState.Hold1)
            {
                currentState_LeftHand = PerformActionState.Idle;
            }
        }

        // Melee Attack Logic
        if (leftHandWeapon.weaponCategory == WeaponCategory.Melee && playerController.isLeftHandPressed && currentBlockCooldownLeft <= 0f && !isKicking)
        {
            if (playerController.CharacterStaminaComponent.CurrentStamina > 0f)
            {
                if (Time.time >= lastAttackTimeLeft + leftHandWeapon.attackCooldown)
                {
                    PerformAttack(leftHandWeapon, isLeftHand: true);
                }
            }
        }
    }

    private IEnumerator ExecutionSequence(EnemyEntity targetEnemy)
    {
        playerController.isAttacking = true;
        playerController.isExecuting = true;

        Vector3 directionToPlayer = (transform.position - targetEnemy.transform.position).normalized;
        directionToPlayer.y = 0;

        Vector3 executeCenter = targetEnemy.executeTransform != null ? targetEnemy.executeTransform.position : targetEnemy.transform.position;
        Vector3 targetPos = executeCenter + (directionToPlayer * 1.2f);

        float startYaw = transform.eulerAngles.y;
        Vector3 dirToEnemyBody = executeCenter - targetPos;
        dirToEnemyBody.y = 0;
        float targetYaw = Quaternion.LookRotation(dirToEnemyBody).eulerAngles.y;

        float startPitch = playerController.verticalRotation;
        Vector3 lookTargetPos = targetEnemy.executeTransform != null ? targetEnemy.executeTransform.position : targetEnemy.transform.position + (Vector3.up * 1.5f);

        float cameraHeight = cameraTransform.position.y - transform.position.y;
        Vector3 finalCameraPos = targetPos + (Vector3.up * cameraHeight);

        Vector3 dirToLookTarget = (lookTargetPos - finalCameraPos).normalized;
        float targetPitch = Quaternion.LookRotation(dirToLookTarget).eulerAngles.x;
        if (targetPitch > 180f) targetPitch -= 360f;

        float dashDuration = 0.2f;
        float elapsed = 0f;
        CharacterController charController = playerController.GetComponent<CharacterController>();

        while (elapsed < dashDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / dashDuration;

            Vector3 currentLerpPos = Vector3.Lerp(transform.position, targetPos, t);
            Vector3 moveDelta = currentLerpPos - transform.position;
            charController.Move(moveDelta);

            float currentYaw = Mathf.LerpAngle(startYaw, targetYaw, t);
            transform.eulerAngles = new Vector3(0f, currentYaw, 0f);

            playerController.verticalRotation = Mathf.LerpAngle(startPitch, targetPitch, t);

            yield return null;
        }

        transform.eulerAngles = new Vector3(0f, targetYaw, 0f);
        playerController.verticalRotation = targetPitch;

        currentState_Righthand = PerformActionState.Execute;

        targetEnemy.GotExecuted();
        cameraFollow.isSmoothing = false;
        cameraBob.TriggerShake(0.293f, 0.05f);
        cameraBob.TriggerZoomEffect(40f, 0.293f, 0.25f);

        yield return new WaitForSeconds(0.293f);

        cameraFollow.isSmoothing = false;
        playerController.isExecuting = false;
        playerController.isAttacking = false;
        currentState_Righthand = PerformActionState.Idle;
    }

    public bool HasPyromancyEquipped()
    {
        bool rightIsPyro = rightHandWeapon != null && rightHandWeapon.weaponCategory == WeaponCategory.PyromancyFlame;
        bool leftIsPyro = leftHandWeapon != null && leftHandWeapon.weaponCategory == WeaponCategory.PyromancyFlame;
        return rightIsPyro || leftIsPyro;
    }

    private void PerformPyromancy(Weapon weapon, bool isLeftHand, bool isHold)
    {
        playerController.StaminaDepleted(weapon.staminaCost);

        if (isHold)
        {
            if (isLeftHand) currentState_LeftHand = PerformActionState.Hold1;
            else currentState_Righthand = PerformActionState.Hold1;
        }
        else
        {
            if (isLeftHand)
            {
                lastAttackTimeLeft = Time.time;
                currentState_LeftHand = PerformActionState.Press1;
                attackStateResetTimerLeft = weapon.attackCooldown;
            }
            else
            {
                lastAttackTimeRight = Time.time;
                currentState_Righthand = PerformActionState.Press1;
                attackStateResetTimerRight = weapon.attackCooldown;
            }
        }
    }

    private IEnumerator KickSequence()
    {
        yield return new WaitForSeconds(0.213f);
        PerformKick();

        yield return new WaitForSeconds(0.225f);
        isKicking = false;
        currentState_LeftHand = PerformActionState.Idle;
        currentState_Righthand = PerformActionState.Idle;
    }

    private EnemyEntity GetEnemyInFront()
    {
        Vector3 castStart = cameraTransform.position - (cameraTransform.forward * attackRadius);
        Ray rayAttack = new Ray(castStart, cameraTransform.forward);
        float totalRange = attackRange + attackRadius;

        if (Physics.SphereCast(rayAttack, attackRadius, out RaycastHit hit, totalRange, attackLayerMask))
        {
            return hit.collider.GetComponent<EnemyEntity>();
        }

        return null;
    }

    private void PerformKick()
    {
        RumbleManager.instance.RumblePulse(1f, 2.5f, 0.1f);
        playerController.StaminaDepleted(kickStaminaCost);

        Vector3 joltDirection = new Vector3(-20f, 0f, 0f);
        Vector3 castStart = cameraTransform.position - (cameraTransform.forward * attackRadius);
        Ray rayAttack = new Ray(castStart, cameraTransform.forward);

        float totalRange = attackRange + attackRadius;
        RaycastHit[] hits = Physics.SphereCastAll(rayAttack, attackRadius, totalRange, attackLayerMask);
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
                    targetEnemy.GotKicked(hit.point, kickDamage);
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

        playerController.TriggerMeleeJolt(joltDirection);
    }

    private void PerformAttack(Weapon weapon, bool isLeftHand)
    {
        if (!playerController.isGrounded || playerController.isSliding) { return; }

        ApplyAttackJolt(weapon, isLeftHand);

        playerController.TriggerAttackMovement(weapon.attackCooldown, lungeForce);

        if (isLeftHand)
        {
            lastAttackTimeLeft = Time.time;
            attackStateResetTimerLeft = weapon.attackCooldown;
        }
        else
        {
            lastAttackTimeRight = Time.time;
            attackStateResetTimerRight = weapon.attackCooldown;
        }
    }

    private void ApplyAttackJolt(Weapon weapon, bool isLeftHand)
    {
        RumbleManager.instance.RumblePulse(1f, 2.5f, 0.2f);
        playerController.StaminaDepleted(weapon.staminaCost);

        if (isLeftHand)
        {
            if (Time.time > lastAttackTimeLeft + weapon.attackCooldown + 0.5f) comboStepLeft = 0;
        }
        else
        {
            if (Time.time > lastAttackTimeRight + weapon.attackCooldown + 0.5f) comboStepRight = 0;
        }

        Vector3 joltDirection = Vector3.zero;
        Vector3 castStart = cameraTransform.position - (cameraTransform.forward * attackRadius);
        Ray rayAttack = new Ray(castStart, cameraTransform.forward);
        float totalRange = attackRange + attackRadius;
        bool isLeftAttack = isLeftHand;
        int currentCombo = isLeftHand ? comboStepLeft : comboStepRight;

        switch (currentCombo)
        {
            case 0:
                joltDirection = new Vector3(2f, -6f, 3f);
                if (isLeftHand) currentState_LeftHand = PerformActionState.Press1;
                else currentState_Righthand = PerformActionState.Press1;
                break;
            case 1:
                joltDirection = new Vector3(2f, 6f, -3f);
                if (isLeftHand) currentState_LeftHand = PerformActionState.Press2;
                else currentState_Righthand = PerformActionState.Press2;
                break;
            case 2:
                joltDirection = new Vector3(5f, 0f, 0f);
                if (isLeftHand) currentState_LeftHand = PerformActionState.Press3;
                else currentState_Righthand = PerformActionState.Press3;
                break;
        }

        RaycastHit[] hits = Physics.SphereCastAll(rayAttack, attackRadius, totalRange, attackLayerMask);
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
                    targetEnemy.TakeSwordHit(isLeftAttack, hit.point, weapon.damage);
                    hitSomething = true;
                }
                else if (destructible != null && processedTargets.Add(destructible))
                {
                    destructible.TakeDamage(weapon.damage, hit.collider, hit.point);
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
            SoundManager.instance.SwordSound_Air(transform.position);
        }

        playerController.TriggerMeleeJolt(joltDirection);

        if (isLeftHand)
        {
            comboStepLeft++;
            if (comboStepLeft > 2) comboStepLeft = 0;
        }
        else
        {
            comboStepRight++;
            if (comboStepRight > 2) comboStepRight = 0;
        }
    }

    private static readonly Dictionary<PerformActionState, int> StateToHash = new Dictionary<PerformActionState, int>
    {
        { PerformActionState.Idle, Animator.StringToHash("IsIdle") },
        { PerformActionState.Press1, Animator.StringToHash("IsPress1") },
        { PerformActionState.Press2, Animator.StringToHash("IsPress2") },
        { PerformActionState.Press3, Animator.StringToHash("IsPress3") },
        { PerformActionState.Hold1, Animator.StringToHash("IsHold1") },
        { PerformActionState.Hold2, Animator.StringToHash("IsHold2") },
        { PerformActionState.Hold3, Animator.StringToHash("IsHold3") },
        { PerformActionState.Kick, Animator.StringToHash("IsKick") },
        { PerformActionState.Execute, Animator.StringToHash("IsExecute") }
    };

    private int lastLeftStateHash;
    private int lastRightStateHash;

    private void ChooseAnimations()
    {
        // Handle Left Hand Animator
        if (StateToHash.TryGetValue(currentState_LeftHand, out int leftHash))
        {
            if (leftHash != lastLeftStateHash)
            {
                if (lastLeftStateHash != 0 && anim_LeftHand != null) anim_LeftHand.SetBool(lastLeftStateHash, false);
                if (anim_LeftHand != null) anim_LeftHand.SetBool(leftHash, true);
                lastLeftStateHash = leftHash;
            }
        }

        // Handle Right Hand Animator
        if (StateToHash.TryGetValue(currentState_Righthand, out int rightHash))
        {
            if (rightHash != lastRightStateHash)
            {
                if (lastRightStateHash != 0 && anim_RightHand != null) anim_RightHand.SetBool(lastRightStateHash, false);
                if (anim_RightHand != null) anim_RightHand.SetBool(rightHash, true);
                lastRightStateHash = rightHash;
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (cameraTransform == null) return;

        Gizmos.color = Color.red;

        Vector3 castStart = cameraTransform.position - (cameraTransform.forward * attackRadius);
        float totalRange = attackRange + attackRadius;

        Gizmos.DrawRay(castStart, cameraTransform.forward * totalRange);
        Gizmos.DrawWireSphere(castStart, attackRadius);

        Vector3 endPosition = castStart + (cameraTransform.forward * totalRange);
        Gizmos.DrawWireSphere(endPosition, attackRadius);
    }
}
