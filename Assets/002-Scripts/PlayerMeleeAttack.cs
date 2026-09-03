using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Playables;

public enum MeleeAttackState
{
    Idle,
    Attack1,
    Attack2,
    Attack3,
    Block,
    Kick,
    Execute
}

public class PlayerMeleeAttack : MonoBehaviour
{
    [SerializeField] private CameraFollow cameraFollow;
    [SerializeField] private CameraBob cameraBob;
    [SerializeField] private MeleeAttackState meleeAttackState;
    [SerializeField] private Animator anim;

    [Header("Melee Settings")]
    [SerializeField] private float executeStaminaCost = 20f;
    [SerializeField] private float kickStaminaCost = 15f;
    [SerializeField] private float meleeStaminaCost = 18f;
    [SerializeField] private float meleeDamage = 10f;
    [SerializeField] private float kickDamage = 5f;
    [SerializeField] private float attackRange = 1.8f;
    [SerializeField] private float attackRadius = 1f;
    [SerializeField] private float attackCooldown = 0.6f;
    [SerializeField] private float lungeForce = 12f;

    [Tooltip("Set this to the layer your enemies are on")]
    [SerializeField] private LayerMask attackLayerMask;

    [Header("References")]
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private PlayerController playerController;

    private float lastAttackTime;
    private int comboStep = 0;
    private float attackStateResetTimer = 0f;

    [HideInInspector] public bool isBlocking = false;
    [HideInInspector] public float currentParry = 0f;
    private float parryDuration = 0.3f;
    private float blockCooldownDuration = 0.15f;
    private float currentBlockCooldown = 0f;
    private bool isKicking = false;

    private void Start()
    {
        meleeAttackState = MeleeAttackState.Idle;
    }

    void Update()
    {
        if (PauseGame.instance.IsPaused || playerController.CharacterHealthComponent.CurrentHP <= 0) return;

        ChooseAnimation();

        if(currentBlockCooldown > 0f)
        {
            currentBlockCooldown -= Time.deltaTime;
        }

        if (currentParry > 0f)
        {
            currentParry -= Time.deltaTime;
        }

        if (playerController.isAttackPressed && currentBlockCooldown <= 0f && !isKicking)
        {
            if (playerController.CharacterStaminaComponent.CurrentStamina > 0f)
            {
                if (Time.time >= lastAttackTime + attackCooldown)
                {
                    PerformAttack();
                }
            }
        }

        if (playerController.isBlockPressed && currentBlockCooldown <= 0f && !isBlocking && !isKicking)
        {
            if (playerController.CharacterStaminaComponent.CurrentStamina > 0f)
            {
                RumbleManager.instance.RumblePulse(1f, 2f, 0.15f);
                isBlocking = true;
                meleeAttackState = MeleeAttackState.Block;
                anim.speed = 1f;
                currentParry = parryDuration;
            }
        }

        if (!playerController.isBlockHeld && isBlocking)
        {
            anim.speed = 1f;
            isBlocking = false;
            currentBlockCooldown = blockCooldownDuration;
        }

        if (playerController.isKickPressed && meleeAttackState == MeleeAttackState.Idle && !isKicking)
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
                    meleeAttackState = MeleeAttackState.Kick;
                    StartCoroutine(KickSequence());
                }
            }
        }

        if (meleeAttackState != MeleeAttackState.Idle && !isBlocking && !isKicking && !playerController.isExecuting)
        {
            attackStateResetTimer -= Time.deltaTime;
            if (attackStateResetTimer <= 0f && currentBlockCooldown <= 0f)
            {
                meleeAttackState = MeleeAttackState.Idle;
            }
        }
    }

    private IEnumerator ExecutionSequence(EnemyEntity targetEnemy)
    {
        // 1. Lock the player state so they can't attack or WASD move
        playerController.isAttacking = true;
        playerController.isExecuting = true;

        // 2. Calculate the Execution Position (1.2 units directly in front of the enemy)
        Vector3 directionToPlayer = (transform.position - targetEnemy.transform.position).normalized;
        directionToPlayer.y = 0; // Keep dash purely horizontal

        // Use executeTransform if it exists, otherwise fallback to the enemy's root
        Vector3 executeCenter = targetEnemy.executeTransform != null ? targetEnemy.executeTransform.position : targetEnemy.transform.position;
        Vector3 targetPos = executeCenter + (directionToPlayer * 1.2f);

        // 3. Calculate PERFECT YAW for Player Body (Left/Right only)
        float startYaw = transform.eulerAngles.y;
        Vector3 dirToEnemyBody = executeCenter - targetPos;
        dirToEnemyBody.y = 0; // Force it to be perfectly flat!
        float targetYaw = Quaternion.LookRotation(dirToEnemyBody).eulerAngles.y;

        // 4. Calculate PERFECT PITCH for Camera (Up/Down only)
        float startPitch = playerController.verticalRotation;

        // FIX 1: Tell the camera to look exactly at the executeTransform!
        Vector3 lookTargetPos = targetEnemy.executeTransform != null ? targetEnemy.executeTransform.position : targetEnemy.transform.position + (Vector3.up * 1.5f);

        // FIX 2: Calculate the angle from where the camera WILL be at the end of the dash!
        float cameraHeight = cameraTransform.position.y - transform.position.y;
        Vector3 finalCameraPos = targetPos + (Vector3.up * cameraHeight);

        Vector3 dirToLookTarget = (lookTargetPos - finalCameraPos).normalized;

        float targetPitch = Quaternion.LookRotation(dirToLookTarget).eulerAngles.x;
        if (targetPitch > 180f) targetPitch -= 360f; // Fixes wrapping issues

        // 5. Smoothly Dash and Look over 0.2 seconds
        float dashDuration = 0.2f;
        float elapsed = 0f;
        CharacterController charController = playerController.GetComponent<CharacterController>();

        while (elapsed < dashDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / dashDuration;

            // Apply Position via CharacterController
            Vector3 currentLerpPos = Vector3.Lerp(transform.position, targetPos, t);
            Vector3 moveDelta = currentLerpPos - transform.position;
            charController.Move(moveDelta);

            // Apply ISOLATED Rotations
            float currentYaw = Mathf.LerpAngle(startYaw, targetYaw, t);
            transform.eulerAngles = new Vector3(0f, currentYaw, 0f);

            // Camera only rotates vertically
            playerController.verticalRotation = Mathf.LerpAngle(startPitch, targetPitch, t);

            yield return null;
        }

        transform.eulerAngles = new Vector3(0f, targetYaw, 0f);
        playerController.verticalRotation = targetPitch;

        // 5. Trigger the Execution Logic!
        // Play your execution animation here
        // anim.Play("Execute");
        meleeAttackState = MeleeAttackState.Execute;

        Debug.Log("EXECUTED ENEMY!");

        // Example: Deal massive damage, or call a specific Execute() method on the enemy
        targetEnemy.GotExecuted();
        cameraFollow.isSmoothing = false;
        cameraBob.TriggerShake(0.293f, 0.05f);
        cameraBob.TriggerZoomEffect(40f, 0.293f, 0.25f);
        // Wait for your execution animation to finish before returning control to the player
        yield return new WaitForSeconds(0.293f);

        // 6. Release the player back to normal
        cameraFollow.isSmoothing = false;
        playerController.isExecuting = false;
        playerController.isAttacking = false;
        meleeAttackState = MeleeAttackState.Idle;
    }

    private IEnumerator KickSequence()
    {
        yield return new WaitForSeconds(0.213f);
        PerformKick();

        yield return new WaitForSeconds(0.225f);
        isKicking = false;
        meleeAttackState = MeleeAttackState.Idle;
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

        Vector3 joltDirection = Vector3.zero;
        joltDirection = new Vector3(-20f, 0f, 0f);

        Vector3 castStart = cameraTransform.position - (cameraTransform.forward * attackRadius);
        Ray rayAttack = new Ray(castStart, cameraTransform.forward);

        float totalRange = attackRange + attackRadius;

        RaycastHit[] hits = Physics.SphereCastAll(rayAttack, attackRadius, totalRange, attackLayerMask);
        bool hitSomething = false;

        if (hits.Length > 0)
        {
            // Keep track of enemies we've already hit in this swing so we don't double-hit them
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
                    // Fracture Hit
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

    public void PerformBlock()
    {
        anim.Play("Block", -1, 0f);
        anim.speed = 1f;
    }

    private void PerformAttack()
    {
        if(!playerController.isGrounded || playerController.isSliding) { return; }

        // 1. Determine swing type and apply the physical Camera Jolt
        ApplyAttackJolt();

        playerController.TriggerAttackMovement(attackCooldown, lungeForce);
        
        lastAttackTime = Time.time;

        attackStateResetTimer = attackCooldown;
    }

    private void ApplyAttackJolt()
    {
        RumbleManager.instance.RumblePulse(1f, 2.5f, 0.2f);
        playerController.StaminaDepleted(kickStaminaCost);

        // Reset combo back to step 1 if the player pauses their attacks
        if (Time.time > lastAttackTime + attackCooldown + 0.5f)
        {
            comboStep = 0;
        }

        Vector3 joltDirection = Vector3.zero;

        Vector3 castStart = cameraTransform.position - (cameraTransform.forward * attackRadius);
        Ray rayAttack = new Ray(castStart, cameraTransform.forward);
        float totalRange = attackRange + attackRadius;
        bool isLeftAttack = false;

        switch (comboStep)
        {
            case 0: // Right to Left sweep
                {
                    joltDirection = new Vector3(2f, -6f, 3f);
                    meleeAttackState = MeleeAttackState.Attack1;
                    isLeftAttack = false;
                }
                break;

            case 1: // Left to Right sweep
                {
                    joltDirection = new Vector3(2f, 6f, -3f);
                    meleeAttackState = MeleeAttackState.Attack2;
                    isLeftAttack = true;
                }
                break;

            /*case 2: // Heavy Overhead Chop
             
                break;*/
        }

        RaycastHit[] hits = Physics.SphereCastAll(rayAttack, attackRadius, totalRange, attackLayerMask);
        bool hitSomething = false;

        if (hits.Length > 0)
        {
            // Keep track of enemies we've already hit in this swing so we don't double-hit them
            HashSet<Component> processedTargets = new HashSet<Component>();

            foreach (RaycastHit hit in hits)
            {
                EnemyEntity targetEnemy = hit.collider.GetComponent<EnemyEntity>();
                Fracture destructible = hit.collider.GetComponent<Fracture>();
                FractureTrigger fractureTrigger = hit.collider.GetComponent<FractureTrigger>();

                if (targetEnemy != null && processedTargets.Add(targetEnemy))
                {
                    targetEnemy.TakeSwordHit(isLeftAttack, hit.point, meleeDamage);
                    hitSomething = true;
                }
                else if (destructible != null && processedTargets.Add(destructible))
                {
                    destructible.TakeDamage(meleeDamage, hit.collider, hit.point);
                    hitSomething = true;

                    if(fractureTrigger != null)
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

        // Advance combo to the next swing, looping back to 0 after the chop
        comboStep++;
        if (comboStep > 1) comboStep = 0;
    }

    private static readonly Dictionary<MeleeAttackState, int> StateToHash = new Dictionary<MeleeAttackState, int>
    {
        { MeleeAttackState.Idle, Animator.StringToHash("IsIdle") },
        { MeleeAttackState.Attack1, Animator.StringToHash("IsAttack1") },
        { MeleeAttackState.Attack2, Animator.StringToHash("IsAttack2") },
        { MeleeAttackState.Attack3, Animator.StringToHash("IsAttack3") },
        { MeleeAttackState.Block, Animator.StringToHash("IsBlock") },
        { MeleeAttackState.Kick, Animator.StringToHash("IsKick") },
        { MeleeAttackState.Execute, Animator.StringToHash("IsExecute") }
    };

    private int lastStateHash; // Keep track of the last active hash

    private void ChooseAnimation()
    {
        // 1. Get the hash for the current state
        if (StateToHash.TryGetValue(meleeAttackState, out int currentHash))
        {
            // 2. Only update if the state actually changed
            if (currentHash == lastStateHash) return;

            // 3. Reset the previous animation and set the new one
            if (lastStateHash != 0) anim.SetBool(lastStateHash, false);

            anim.SetBool(currentHash, true);
            lastStateHash = currentHash;
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (cameraTransform == null) return;

        Gizmos.color = Color.red;

        // FIX: Visualize the pulled-back raycast accurately in the scene view
        Vector3 castStart = cameraTransform.position - (cameraTransform.forward * attackRadius);
        float totalRange = attackRange + attackRadius;

        // 1. Draw the center line showing the direction and max range
        Gizmos.DrawRay(castStart, cameraTransform.forward * totalRange);

        // 2. Draw the sphere at the starting position (Now safely behind the camera!)
        Gizmos.DrawWireSphere(castStart, attackRadius);

        // 3. Draw the sphere at the maximum attack range
        Vector3 endPosition = castStart + (cameraTransform.forward * totalRange);
        Gizmos.DrawWireSphere(endPosition, attackRadius);
    }
}
