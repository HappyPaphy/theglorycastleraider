using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public enum EnemyBigKnightState
{
    Idle,
    FallOpening,
    Run,
    Flank,
    Attack1,
    Attack2,
    Attack3,
    BlockLeft,
    BlockRight,
    DamageLeft,
    DamageRight,
    Parried,
    Tired,
    Kick,
    Stunned,
    Executed,
    Died
}

public class EnemyBigKnight : EnemyEntity
{
    [SerializeField] private CapsuleCollider capsuleCol;
    [SerializeField] private SpriteRenderer sprRndr;
    [SerializeField] private EnemyBigKnightState currentState;
    [SerializeField] private Animator anim;
    [SerializeField] private Vector3 offsetLanding;
    //[SerializeField] private Fracture woodenPlank;

    [Header("Movement & Speed")]
    [SerializeField] private float moveSpeed = 4.5f;
    [SerializeField] private float flankSpeed = 3.5f;

    [Header("Combat Dynamics")]
    [SerializeField] private int maxHitsBeforeBlock = 3;
    [SerializeField] private int currentHitCount = 0;
    [SerializeField] private float comboResetTime = 2f;
    [SerializeField] private float attackRadius = 1f;
    [SerializeField] private float attackRange = 2f;
    private float hitResetTimer = 0f;
    private float flankTimer = 0f;
    private Vector3 currentFlankDestination;

    [Header("Detection")]
    [SerializeField] protected float preAttackRadius = 10f;
    [SerializeField] protected float preAttackAngle = 90f;

    [Header("Boss Specific Settings")]
    [SerializeField] private float floatDownSpeed = 5f;
    [SerializeField] private float tiredDuration = 4f;
    [SerializeField] private float kickTriggerRange = 2.5f;
    [SerializeField] private float kickKnockbackPower = 20f;
    [SerializeField] private float kickDamage = 15f;

    [Header("Attack Timings")]
    [SerializeField] private float preAttackDuration = 0.5f;
    [SerializeField] private float comboDelay = 0.35f; // Gap between the 3 combo hits
    [SerializeField] private float attackCooldownDuration = 2.5f;
    private int comboStep = 0; // Tracks which hit of the 3-hit combo we are on

    [Header("Recovery Timers")]
    [SerializeField] private float parryRecoveryTime = 1.167f;
    [SerializeField] private float kickRecoveryTime = 1f;
    [SerializeField] private float stunRecoveryTime = 4f;
    [SerializeField] private float damageStunDuration = 0.5f;
    [SerializeField] private float blockDuration = 1f;

    [Header("Execute")]
    [SerializeField] private float executeDuration = 0.1466f;

    private float stateTimer = 0f;
    private float currentAttackCooldown = 0f;
    private float engagementTimer = 0f;
    private float turnCooldownTimer = 0f;

    private bool isPreAttackOnce = false;
    private bool isExecuted = false;
    private bool isExecutedOnce = false;
    private bool isLowHealthStunnedOnce = false;
    private bool isBlockingActive = false;
    private float executeTimer = 0f;

    private bool isParried = false;

    protected override void Awake()
    {
        agent.speed = moveSpeed;
        agent.updateRotation = false;
        base.Awake();
    }

    protected override void Start()
    {
        moveSpeed *= GameManager.instance != null ? GameManager.instance.enemyMoveSpeed : 1f;
        executeTimer = executeDuration;
        faceToCamera.isFaceYAxis = false;

        slider_HP.gameObject.SetActive(false);

        // Start completely idle and disabled to allow for the drop-down intro
        currentState = EnemyBigKnightState.Idle;
        agent.enabled = false;

        base.Start();
    }

    protected override void Update()
    {
        ChooseAnimation();
        HandleState();
        HandleBalance();
        HandleExecute();
        base.Update();
    }

    private void HandleBalance()
    {
        if (isDied || !isPlayerDetected) return;
        if (stateTimer > 0) { return; }
        if(currentState == EnemyBigKnightState.Kick) { return; }

        if (turnCooldownTimer > 0f) turnCooldownTimer -= Time.deltaTime;

        if (hitResetTimer > 0 && !isParried)
        {
            hitResetTimer -= Time.deltaTime;
            if (hitResetTimer <= 0) currentHitCount = 0;
        }

        // Adjust Agent Speed based on state
        if (currentState == EnemyBigKnightState.Run)
        {
            anim.speed = 1.2f;
            agent.speed = moveSpeed;
        }
        else if (currentState == EnemyBigKnightState.Flank)
        {
            anim.speed = 1f;
            agent.speed = flankSpeed;
        }
        else
        {
            anim.speed = 1f;
        }
    }

    private void HandleState()
    {
        if (isDied) return;

        // Manage Duration-Based States
        if (stateTimer > 0 && currentState != EnemyBigKnightState.Stunned)
        {
            engagementTimer -= Time.deltaTime;
            stateTimer -= Time.deltaTime;

            if (stateTimer <= 0)
            {
                // Boss Special Logic: Transition to Kick if player is too close after Blocking or Tired state
                if (currentState == EnemyBigKnightState.BlockLeft ||
                    currentState == EnemyBigKnightState.BlockRight ||
                    currentState == EnemyBigKnightState.DamageLeft ||
                    currentState == EnemyBigKnightState.DamageRight ||
                    currentState == EnemyBigKnightState.Tired)
                {
                    isParried = false;
                    isBlockingActive = false;

                    if (playerTransform != null && Vector3.Distance(transform.position, playerTransform.position) <= kickTriggerRange)
                    {
                        currentState = EnemyBigKnightState.Kick;
                        StartCoroutine(KickSequence());
                        return;
                    }
                }

                isParried = false;
                isBlockingActive = false;
                SoundManager.instance.BigKnightSound_Tired(sprRndr.transform.position, false);
                currentState = EnemyBigKnightState.Run;
            }
        }

        // Prevent state overrides if currently in a locked animation sequence
        if (currentState == EnemyBigKnightState.DamageLeft ||
            currentState == EnemyBigKnightState.DamageRight ||
            currentState == EnemyBigKnightState.FallOpening ||
            currentState == EnemyBigKnightState.Kick ||
            currentState == EnemyBigKnightState.Attack1 ||
            currentState == EnemyBigKnightState.Attack2 ||
            currentState == EnemyBigKnightState.Attack3 ||
            currentState == EnemyBigKnightState.BlockLeft ||
            currentState == EnemyBigKnightState.BlockRight ||
            currentState == EnemyBigKnightState.Tired)
        {
            return;
        }

        if (currentAttackCooldown > 0f) currentAttackCooldown -= Time.deltaTime;

        switch (currentState)
        {
            case EnemyBigKnightState.Idle:
                if (isPlayerDetected)
                {
                    //woodenPlank.ComputeFracture(woodenPlank.transform.position);
                    // Trigger the Fall Down Intro once the player spots the boss
                    currentState = EnemyBigKnightState.FallOpening;
                    StartCoroutine(FallOpeningSequence());

                    if (slider_HP != null)
                    {
                        slider_HP.gameObject.SetActive(true);
                    }
                }
                break;

            case EnemyBigKnightState.Run:
                SafeStopAgent(false);
                isPreAttackOnce = false;

                if (PlayerController.instance != null)
                {
                    Transform target = PlayerController.instance.transform;
                    SafeSetDesitinationAgent(target.position);

                    Vector3 lookDir = (target.position - transform.position).normalized;
                    lookDir.y = 0;
                    if (lookDir != Vector3.zero)
                    {
                        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lookDir), Time.deltaTime * 8f);
                    }

                    if (Vector3.Distance(transform.position, target.position) <= 10f)
                    {
                        engagementTimer -= Time.deltaTime;
                        if (engagementTimer <= 0f)
                        {
                            YieldTurn();
                            break;
                        }
                    }
                }

                if (currentAttackCooldown <= 0f) CheckAttackVision();
                break;

            case EnemyBigKnightState.Flank:
                SafeStopAgent(false);

                if (turnCooldownTimer <= 0f && EnemyDirector.instance != null && EnemyDirector.instance.RequestAttackPermission(this))
                {
                    engagementTimer = Random.Range(2, 3);
                    currentState = EnemyBigKnightState.Run;
                    break;
                }

                if (playerTransform != null)
                {
                    Vector3 lookDir = (playerTransform.position - transform.position).normalized;
                    lookDir.y = 0;
                    if (lookDir != Vector3.zero)
                    {
                        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lookDir), Time.deltaTime * 6f);
                    }

                    flankTimer -= Time.deltaTime;
                    if (flankTimer <= 0f || SafeGetRemainingDistance() < 1f)
                    {
                        float randomAngleOffset = Random.Range(-60f, 60f);
                        Vector3 rotatedDirection = Quaternion.Euler(0f, randomAngleOffset, 0f) * playerTransform.forward;
                        currentFlankDestination = playerTransform.position + (rotatedDirection * Random.Range(5, 8));
                        flankTimer = Random.Range(2.0f, 3.5f);
                    }
                    SafeSetDesitinationAgent(currentFlankDestination);
                }
                break;
        }
    }

    // ==========================================
    // BOSS SPECIFIC SEQUENCES
    // ==========================================

    private IEnumerator FallOpeningSequence()
    {
        Vector3 startPos = transform.position;
        Vector3 groundPos = startPos + offsetLanding;

        /*// Raycast down to find the floor
        if (Physics.Raycast(startPos, Vector3.down, out RaycastHit hit, 50f, obstacleLayer))
        {
            groundPos = hit.point;
        }*/

        // Float down
        float distance = Vector3.Distance(startPos, groundPos);
        float fallDuration = distance / floatDownSpeed;
        float elapsed = 0f;

        while (elapsed < fallDuration)
        {
            transform.position = Vector3.Lerp(startPos, groundPos, elapsed / fallDuration);
            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.position = groundPos;

        PlayerController.instance.cameraBob.TriggerShake(0.6f, 0.1f);
        SoundManager.instance.WoodenSound_Break(PlayerController.instance.transform.position);

        // Play heavy landing effect
        if (ObjectPoolingManager.instance != null) ObjectPoolingManager.instance.SpawnObject("Spark", transform.position); // Replace with dust if available
        //SoundManager.instance.KickSound_Air(transform.position); // Heavy thud

        // Stand Idle for 1 Second
        yield return new WaitForSeconds(4f);

        // Enter combat
        if (agent != null) agent.enabled = true;
        if (EnemyDirector.instance != null) EnemyDirector.instance.RequestAttackPermission(this);

        

        engagementTimer = Random.Range(4, 5);
        currentState = EnemyBigKnightState.Run;
    }

    private void CheckAttackVision()
    {
        if (isDied || playerTransform == null || PlayerController.instance.CharacterHealthComponent.CurrentHP <= 0) return;

        Collider[] hits = Physics.OverlapSphere(transform.position, preAttackRadius, playerLayer);
        if (hits.Length > 0)
        {
            Vector3 directionToPlayer = (playerTransform.position - eyesTransform.position).normalized;
            float angleToPlayer = Vector3.Angle(transform.forward, directionToPlayer);

            if (angleToPlayer < preAttackAngle / 2)
            {
                float distanceToPlayer = Vector3.Distance(eyesTransform.position, playerTransform.position);
                if (!Physics.Raycast(eyesTransform.position, directionToPlayer, distanceToPlayer, obstacleLayer))
                {
                    if (distanceToPlayer <= attackRange)
                    {
                        currentState = EnemyBigKnightState.Attack1;
                        SafeStopAgent(true);
                        StartCoroutine(AttackSequence());
                    }
                }
            }
        }
    }

    private IEnumerator AttackSequence()
    {
        for (comboStep = 1; comboStep <= 3; comboStep++)
        {
            SoundManager.instance.BigKnightSound_Attack(sprRndr.transform.position);
            yield return new WaitForSeconds(preAttackDuration);

            // Cancel if interrupted by parry/stagger
            if (currentState != EnemyBigKnightState.Attack1 &&
                currentState != EnemyBigKnightState.Attack2 &&
                currentState != EnemyBigKnightState.Attack3) yield break;

            // Damage Hitbox
            Vector3 startPos = sprRndr.gameObject.transform.position;
            Vector3 endPos = startPos + (sprRndr.gameObject.transform.forward * attackRange);
            Collider[] hitPlayers = Physics.OverlapCapsule(startPos, endPos, attackRadius, playerLayer);

            if (hitPlayers.Length > 0)
            {
                hitPlayers[0].GetComponent<PlayerController>().TakeSwordHit(this);
            }

            // Gap between swings
            if (comboStep < 3)
            {
                switch(comboStep)
                {
                    case 1:
                        currentState = EnemyBigKnightState.Attack2;
                        break;

                    case 2:
                        currentState = EnemyBigKnightState.Attack3;
                        break;
                }
            }
        }

        if (currentState == EnemyBigKnightState.Attack1 ||
            currentState == EnemyBigKnightState.Attack2 ||
            currentState == EnemyBigKnightState.Attack3)
        {
            currentAttackCooldown = attackCooldownDuration;
            currentState = EnemyBigKnightState.Run;
        }

        comboStep = 0;
    }

    private IEnumerator KickSequence()
    {
        SafeStopAgent(true);
        SoundManager.instance.BigKnightSound_Attack(transform.position);

        yield return new WaitForSeconds(0.4f);

        if (currentState == EnemyBigKnightState.Kick)
        {
            // Re-evaluate distance
            if (playerTransform != null && Vector3.Distance(transform.position, playerTransform.position) <= kickTriggerRange)
            {
                SoundManager.instance.KickSound_Human(transform.position);
                PlayerController.instance.TakeSwordHit(this);

                // Push Player backwards
                Vector3 pushDir = playerTransform.position - transform.position;
                pushDir.y = 0;
                StartCoroutine(PushPlayer(pushDir.normalized, kickKnockbackPower, 0.25f));
            }
            else
            {
                SoundManager.instance.KickSound_Air(transform.position);
            }

            yield return new WaitForSeconds(kickRecoveryTime);

            if (currentState == EnemyBigKnightState.Kick)
            {
                currentState = EnemyBigKnightState.Run;
            }
        }
    }

    private IEnumerator PushPlayer(Vector3 direction, float power, float duration)
    {
        float elapsed = 0f;
        CharacterController playerCC = PlayerController.instance.GetComponent<CharacterController>();

        while (elapsed < duration)
        {
            if (playerCC != null)
            {
                playerCC.Move(direction * power * Time.deltaTime);
            }
            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private void YieldTurn()
    {
        if (EnemyDirector.instance != null) EnemyDirector.instance.ReleaseAttackPermission(this);
        currentState = EnemyBigKnightState.Flank;
        turnCooldownTimer = Random.Range(1, 4);
    }

    // ==========================================
    // COMBAT REACTIONS
    // ==========================================

    public override void TakeSwordHit(bool isLeft, Vector3 hitPoint, float damageValue, bool isBlockable, bool isLeftHand, DamageImpactSound damageImpactSound)
    {
        if (isDied) return;

        // ONLY allow damage if the boss is tired (or locked into execution/stunned state)
        if (currentState == EnemyBigKnightState.Tired || 
            currentState == EnemyBigKnightState.Stunned || 
            currentState == EnemyBigKnightState.Executed ||
            currentState == EnemyBigKnightState.DamageLeft ||
            currentState == EnemyBigKnightState.DamageRight)
        {
            ApplyDamageState(isLeft, damageValue, isLeftHand, damageImpactSound);
        }
        else
        {
            // Fully block EVERYTHING else, ignoring block counters
            isBlockingActive = true;
            ApplyBlockState(isLeft);
        }
    }

    public override void TakeSpellHit(bool isLeft, Vector3 hitPoint, float damageValue, bool isBlockable, float knockBackValue, DamageImpactSound damageImpactSound)
    {
        if (isDied) return;

        if (currentState == EnemyBigKnightState.Tired ||
            currentState == EnemyBigKnightState.Stunned ||
            currentState == EnemyBigKnightState.Executed ||
            currentState == EnemyBigKnightState.DamageLeft ||
            currentState == EnemyBigKnightState.DamageRight)
        { 
            ApplyDamageState(isLeft, damageValue, false, damageImpactSound, knockBackValue);
        }
        else
        {
            isBlockingActive = true;
            ApplyBlockState(isLeft);
        }
    }

    private void ApplyDamageState(bool isLeft, float damageValue, bool isLeftHand, DamageImpactSound damageImpactSound, float manualKnockback = -1f)
    {
        if (!isStunned && currentState != EnemyBigKnightState.Executed)
        {
            currentState = isLeft ? EnemyBigKnightState.DamageLeft : EnemyBigKnightState.DamageRight;
            //stateTimer = damageStunDuration;
        }

        if (ObjectPoolingManager.instance != null) ObjectPoolingManager.instance.SpawnObject("Blood", eyesTransform.position);
        SoundManager.instance.BigKnightSound_Tired(sprRndr.transform.position, false);

        switch (damageImpactSound)
        {
            case DamageImpactSound.MetalFlesh:
                {
                    SoundManager.instance.SwordSound_Flesh(sprRndr.transform.position);
                    SoundManager.instance.BigKnightSound_Grunt(sprRndr.transform.position);
                }
                break;

            case DamageImpactSound.FireFlesh:
                {
                    SoundManager.instance.FireSound_Impact(sprRndr.transform.position);
                }
                break;
        }

        SafeStopAgent(true);

        float force = manualKnockback >= 0 ? manualKnockback : (isLeftHand ? PlayerWeaponManager.instance.leftHandWeapon.damageKnockbackForce : PlayerWeaponManager.instance.rightHandWeapon.damageKnockbackForce);
        ApplyKnockback(force / 5);
        TakeDamage(damageValue);
    }

    private void ApplyBlockState(bool isLeft)
    {
        if (isStunned) return;

        currentState = isLeft ? EnemyBigKnightState.BlockLeft : EnemyBigKnightState.BlockRight;
        stateTimer = blockDuration;

        if (ObjectPoolingManager.instance != null) ObjectPoolingManager.instance.SpawnObject("Spark", eyesTransform.position);
        SoundManager.instance.SwordSound_Metal(sprRndr.transform.position);

        SafeStopAgent(true);
        ApplyKnockback(blockKnockbackForce / 5);
    }

    public override void GotParried(bool isLeftHand)
    {
        SoundManager.instance.BigKnightSound_Tired(sprRndr.transform.position, true);

        // Boss Mechanic: Only tired if player successfully parries the 3rd final hit
        if (currentState == EnemyBigKnightState.Attack3 && comboStep == 3)
        {
            ApplyKnockback(isLeftHand ? PlayerWeaponManager.instance.leftHandWeapon.damageKnockbackForce : PlayerWeaponManager.instance.rightHandWeapon.damageKnockbackForce);
            currentState = EnemyBigKnightState.Tired;
            stateTimer = tiredDuration;
            isParried = true;
            currentHitCount = 0;
            SafeStopAgent(true);
        }
    }

    public override void GotKicked(Vector3 hitPoint, float damageValue, float knockBackValue)
    {
        if (!isStunned && !isExecuted)
        {
            //currentState = EnemyBigKnightState.Kicked;
            //stateTimer = kickRecoveryTime;
        }

        SoundManager.instance.BigKnightSound_Grunt(sprRndr.transform.position);
        SafeStopAgent(true);
        ApplyKnockback(knockBackValue / 4);

        if (currentState == EnemyBigKnightState.Tired || currentState == EnemyBigKnightState.Stunned)
        {
            TakeDamage(damageValue);
            if (ObjectPoolingManager.instance != null) ObjectPoolingManager.instance.SpawnObject("Blood", eyesTransform.position);
        }
        else
        {
            ApplyBlockState(true);
        }
    }

    public override void GotStunned()
    {
        currentState = EnemyBigKnightState.Stunned;
        isStunned = true;
        stateTimer = stunRecoveryTime;

        if (EnemyDirector.instance != null) EnemyDirector.instance.UnregisterEnemy(this);
        SafeStopAgent(true);
    }

    public override void GotExecuted()
    {
        currentState = EnemyBigKnightState.Executed;
        isExecuted = true;
        SafeStopAgent(true);
    }

    private void HandleExecute()
    {
        if (CharacterHealthComponent.CurrentHP <= CharacterHealthComponent.MaxHP * CharacterHealthComponent.ExecutePercentage && !isLowHealthStunnedOnce)
        {
            isLowHealthStunnedOnce = true;
            GotStunned();
        }

        if (isExecuted && !isExecutedOnce)
        {
            if (executeTimer <= 0f)
            {
                isExecutedOnce = true;
                SoundManager.instance.SwordSound_Flesh(sprRndr.transform.position);
                SoundManager.instance.BigKnightSound_Grunt(sprRndr.transform.position);
                CharacterHealthComponent.SetHP(0f);
            }
            else
            {
                executeTimer -= Time.deltaTime;
            }
        }
    }

    public override void Die()
    {
        if (!isDiedOnce)
        {
            isDied = true;
            isDiedOnce = true;
            capsuleCol.enabled = false;
            if (agent != null) agent.enabled = false;

            slider_HP.gameObject.SetActive(false);  

            if (EnemyDirector.instance != null) EnemyDirector.instance.UnregisterEnemy(this);

            if (currentState != EnemyBigKnightState.Executed)
            {
                SoundManager.instance.BigKnightSound_Grunt(sprRndr.transform.position);
                currentState = EnemyBigKnightState.Died;
            }
        }
        base.Die();
    }

    // ==========================================
    // ANIMATION HASHING
    // ==========================================

    private static readonly Dictionary<EnemyBigKnightState, int> StateToHash = new Dictionary<EnemyBigKnightState, int>
    {
        { EnemyBigKnightState.Idle, Animator.StringToHash("IsIdle") },
        { EnemyBigKnightState.FallOpening, Animator.StringToHash("IsFall") },
        { EnemyBigKnightState.Flank, Animator.StringToHash("IsWalk") },
        { EnemyBigKnightState.Run, Animator.StringToHash("IsRun") },
        { EnemyBigKnightState.Attack1, Animator.StringToHash("IsAttack1") },
        { EnemyBigKnightState.Attack2, Animator.StringToHash("IsAttack2") },
        { EnemyBigKnightState.Attack3, Animator.StringToHash("IsAttack3") },
        { EnemyBigKnightState.BlockLeft, Animator.StringToHash("IsBlockLeft") },
        { EnemyBigKnightState.BlockRight, Animator.StringToHash("IsBlockRight") },
        { EnemyBigKnightState.DamageLeft, Animator.StringToHash("IsDamageLeft") },
        { EnemyBigKnightState.DamageRight, Animator.StringToHash("IsDamageRight") },
        { EnemyBigKnightState.Parried, Animator.StringToHash("IsParried") },
        { EnemyBigKnightState.Tired, Animator.StringToHash("IsTired") },
        { EnemyBigKnightState.Kick, Animator.StringToHash("IsKick") },
        { EnemyBigKnightState.Stunned, Animator.StringToHash("IsStunned") },
        { EnemyBigKnightState.Executed, Animator.StringToHash("IsExecuted") },
        { EnemyBigKnightState.Died, Animator.StringToHash("IsDied") }
    };

    private int lastStateHash;

    private void ChooseAnimation()
    {
        if (StateToHash.TryGetValue(currentState, out int currentHash))
        {
            if (currentHash == lastStateHash) return;

            if (lastStateHash != 0)
            {
                anim.SetBool(lastStateHash, false);
                if (animGore34 != null) animGore34.SetBool(lastStateHash, false);
                if (animGore67 != null) animGore67.SetBool(lastStateHash, false);
            }

            anim.SetBool(currentHash, true);
            if (animGore34 != null) animGore34.SetBool(currentHash, true);
            if (animGore67 != null) animGore67.SetBool(currentHash, true);

            lastStateHash = currentHash;
        }
    }
}