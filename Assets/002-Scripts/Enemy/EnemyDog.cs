using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public enum EnemyDogState
{
    Idle,
    Chase,
    Flank,
    LeapAttack,
    DamageLeft,
    DamageRight,
    Parried, // Player can still swat them away
    Kicked,
    Stunned,
    Executed,
    Died
}

public class EnemyDog : EnemyEntity
{
    [SerializeField] private CapsuleCollider capsuleCol;
    [SerializeField] private SpriteRenderer sprRndr;
    [SerializeField] private EnemyDogState currentState;
    [SerializeField] private Animator anim;

    [Header("Movement & Speed")]
    [SerializeField] private float runSpeed = 8f; // Faster than human enemies
    [SerializeField] private float flankSpeed = 5f;

    [Header("Pounce / Leap Settings")]
    [SerializeField] private float attackRadius = 0.6f;
    [SerializeField] private float pounceRange = 3.5f;     // Jumps from further away
    [SerializeField] private float pounceSpeed = 15f;      // How fast the leap travels
    [SerializeField] private float preAttackDuration = 0.3f; // Shorter telegraph than human
    [SerializeField] private float attackCooldownDuration = 2f;

    [Header("Detection")]
    [SerializeField] protected float preAttackRadius = 12f;
    [SerializeField] protected float preAttackAngle = 120f; // Wider FOV for animals

    [Header("Recovery Timers")]
    [SerializeField] private float parryRecoveryTime = 1.5f;
    [SerializeField] private float kickRecoveryTime = 1f;
    [SerializeField] private float stunRecoveryTime = 3f;
    [SerializeField] private float damageStunDuration = 0.4f;

    [Header("Execute")]
    [SerializeField] private float executeDuration = 0.1466f;

    private float stateTimer = 0f;
    private float currentAttackCooldown = 0f;
    private float engagementTimer = 0f;
    private float turnCooldownTimer = 0f;
    private float flankTimer = 0f;
    private Vector3 currentFlankDestination;

    private bool isPreAttackOnce = false;
    private bool isExecuted = false;
    private bool isExecutedOnce = false;
    private bool isLowHealthStunnedOnce = false;
    private float executeTimer = 0f;

    protected override void Awake()
    {
        agent.speed = runSpeed;
        agent.updateRotation = false;
        base.Awake();
    }

    protected override void Start()
    {
        runSpeed *= GameManager.instance != null ? GameManager.instance.enemyMoveSpeed : 1f;
        executeTimer = executeDuration;
        faceToCamera.isFaceYAxis = false;
        base.Start();
    }

    protected override void Update()
    {
        ChooseAnimation();
        HandleState();
        HandleRecoveryTimers();
        HandleExecute();
        base.Update();
    }

    private void HandleRecoveryTimers()
    {
        if (isDied || !isPlayerDetected) return;

        if (turnCooldownTimer > 0f) turnCooldownTimer -= Time.deltaTime;

        if (currentState == EnemyDogState.Parried || currentState == EnemyDogState.Kicked || currentState == EnemyDogState.Stunned)
        {
            engagementTimer -= Time.deltaTime;
            stateTimer -= Time.deltaTime;

            if (stateTimer <= 0)
            {
                currentState = EnemyDogState.Chase;
            }
        }
        else if (currentState != EnemyDogState.Executed && currentState != EnemyDogState.LeapAttack)
        {
            // Reset to chase if no longer stunned/damaged
            if (stateTimer <= 0 && currentState != EnemyDogState.Flank && currentState != EnemyDogState.Idle)
            {
                currentState = EnemyDogState.Chase;
            }
        }

        // Speed adjustments based on state
        if (currentState == EnemyDogState.Chase)
        {
            anim.speed = 1.5f;
            agent.speed = runSpeed;
        }
        else if (currentState == EnemyDogState.Flank)
        {
            anim.speed = 1f;
            agent.speed = flankSpeed;
        }
        else
        {
            anim.speed = 1f;
            agent.speed = runSpeed;
        }
    }

    private void HandleState()
    {
        if (isDied) return;

        if (stateTimer > 0 &&
            currentState != EnemyDogState.Stunned &&
            currentState != EnemyDogState.Kicked &&
            currentState != EnemyDogState.Parried)
        {
            engagementTimer -= Time.deltaTime;
            stateTimer -= Time.deltaTime;
            if (stateTimer <= 0)
            {
                currentState = EnemyDogState.Chase;
            }
        }

        if(currentState == EnemyDogState.DamageLeft ||
            currentState == EnemyDogState.DamageRight)
        {
            return;
        }

        if (currentAttackCooldown > 0f) currentAttackCooldown -= Time.deltaTime;

        switch (currentState)
        {
            case EnemyDogState.Idle:
                SafeStopAgent(true);
                if (isPlayerDetected)
                {
                    if (EnemyDirector.instance != null && EnemyDirector.instance.RequestAttackPermission(this))
                    {
                        engagementTimer = Random.Range(3f, 6f);
                        currentState = EnemyDogState.Chase;
                    }
                    else
                    {
                        currentState = EnemyDogState.Flank;
                    }
                }
                break;

            case EnemyDogState.Chase:
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
                        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lookDir), Time.deltaTime * 12f);
                    }

                    if (Vector3.Distance(transform.position, target.position) <= 8f)
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

            case EnemyDogState.Flank:
                SafeStopAgent(false);

                if (turnCooldownTimer <= 0f && EnemyDirector.instance != null && EnemyDirector.instance.RequestAttackPermission(this))
                {
                    engagementTimer = Random.Range(3f, 6f);
                    currentState = EnemyDogState.Chase;
                    break;
                }

                if (playerTransform != null)
                {
                    Vector3 lookDir = (playerTransform.position - transform.position).normalized;
                    lookDir.y = 0;
                    if (lookDir != Vector3.zero)
                    {
                        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lookDir), Time.deltaTime * 10f);
                    }

                    if (Vector3.Distance(transform.position, playerTransform.position) < 4f)
                    {
                        SafeSetDesitinationAgent(transform.position - lookDir * 5f); // Back away quickly
                    }
                    else
                    {
                        flankTimer -= Time.deltaTime;
                        if (flankTimer <= 0f || SafeGetRemainingDistance() < 1f)
                        {
                            float randomAngleOffset = Random.Range(-90f, 90f);
                            Vector3 rotatedDirection = Quaternion.Euler(0f, randomAngleOffset, 0f) * playerTransform.forward;
                            currentFlankDestination = playerTransform.position + (rotatedDirection * Random.Range(4, 7));
                            flankTimer = Random.Range(1.0f, 1.8f); // Dogs flank and change direction faster
                        }
                        SafeSetDesitinationAgent(currentFlankDestination);
                    }
                }
                break;

            case EnemyDogState.LeapAttack:
                SafeStopAgent(true); // Stop NavMesh to allow physical leap
                engagementTimer -= Time.deltaTime;

                if (!isPreAttackOnce)
                {
                    isPreAttackOnce = true;
                    StartCoroutine(PounceSequence());
                }
                break;
        }
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
                    // If within pounce range, launch attack
                    if (distanceToPlayer <= pounceRange)
                    {
                        currentState = EnemyDogState.LeapAttack;
                    }
                }
            }
        }
    }

    private IEnumerator PounceSequence()
    {
        // 1. Telegraph (Snarl/Crouch)
        yield return new WaitForSeconds(preAttackDuration);

        SoundManager.instance.DogSound_Attack(sprRndr.transform.position); // Replace with dog growl
        if (currentState == EnemyDogState.LeapAttack)
        {
            // 2. The Leap (Physical movement forward)
            float leapDuration = 0.25f;
            float elapsed = 0f;
            Vector3 startPos = transform.position;
            Vector3 targetPos = startPos + (transform.forward * pounceRange);

            while (elapsed < leapDuration)
            {
                elapsed += Time.deltaTime;
                // Move the dog forward manually while the agent is stopped
                transform.position = Vector3.Lerp(startPos, targetPos, elapsed / leapDuration);

                // Damage Check during the leap
                Collider[] hitPlayers = Physics.OverlapSphere(transform.position + (transform.forward * 0.5f), attackRadius, playerLayer);
                if (hitPlayers.Length > 0)
                {
                    hitPlayers[0].GetComponent<PlayerController>().TakeSwordHit(this); // Takes generic physical hit
                    break; // Stop checking if hit connects
                }

                yield return null;
            }

            // 3. Recovery
            if (currentState == EnemyDogState.LeapAttack)
            {
                currentAttackCooldown = attackCooldownDuration;
                currentState = EnemyDogState.Chase;
                SafeSetDesitinationAgent(transform.position);
            }
        }
    }

    private void YieldTurn()
    {
        if (EnemyDirector.instance != null) EnemyDirector.instance.ReleaseAttackPermission(this);
        currentState = EnemyDogState.Flank;
        turnCooldownTimer = Random.Range(2f, 4f);
    }

    // ==========================================
    // COMBAT REACTIONS (No blocking, always takes damage)
    // ==========================================

    public override void TakeSwordHit(bool isLeft, Vector3 hitPoint, float damageValue, bool isBlockable, bool isLeftHand, DamageImpactSound damageImpactSound)
    {
        if (isDied) return;
        ApplyDamageState(isLeft, damageValue, isLeftHand, damageImpactSound);
    }

    public override void TakeSpellHit(bool isLeft, Vector3 hitPoint, float damageValue, bool isBlockable, float knockBackValue, DamageImpactSound damageImpactSound)
    {
        if (isDied) return;
        ApplyDamageState(isLeft, damageValue, false, damageImpactSound, knockBackValue);
    }

    private void ApplyDamageState(bool isLeft, float damageValue, bool isLeftHand, DamageImpactSound damageImpactSound, float manualKnockback = -1f)
    {
        if (!isStunned && currentState != EnemyDogState.Executed)
        {
            currentState = isLeft ? EnemyDogState.DamageLeft : EnemyDogState.DamageRight;
            stateTimer = damageStunDuration;
        }


        switch (damageImpactSound)
        {
            case DamageImpactSound.MetalFlesh:
                {
                    SoundManager.instance.SwordSound_Flesh(sprRndr.transform.position);
                    SoundManager.instance.DogSound_Grunt(sprRndr.transform.position);
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
        ApplyKnockback(force);
        TakeDamage(damageValue);

        if (ObjectPoolingManager.instance != null)
        {
            ObjectPoolingManager.instance.SpawnObject("Blood", eyesTransform.position);
        }
    }

    public override void GotKicked(Vector3 hitPoint, float damageValue, float knockBackValue)
    {
        if (!isStunned && !isExecuted)
        {
            currentState = EnemyDogState.Kicked;
            stateTimer = kickRecoveryTime;
        }

        SoundManager.instance.DogSound_Grunt(sprRndr.transform.position);
        SafeStopAgent(true);
        ApplyKnockback(knockBackValue);
        TakeDamage(damageValue);

        if (ObjectPoolingManager.instance != null)
        {
            ObjectPoolingManager.instance.SpawnObject("Blood", eyesTransform.position);
        }
    }

    public override void GotParried(bool isLeftHand)
    {
        SoundManager.instance.DogSound_Grunt(sprRndr.transform.position);
        currentState = EnemyDogState.Parried;
        isStunned = true;
        stateTimer = parryRecoveryTime;
        SafeStopAgent(true);
        ApplyKnockback(isLeftHand ? PlayerWeaponManager.instance.leftHandWeapon.damageKnockbackForce : PlayerWeaponManager.instance.rightHandWeapon.damageKnockbackForce);
    }

    public override void GotStunned()
    {
        currentState = EnemyDogState.Stunned;
        stateTimer = stunRecoveryTime;
        if (EnemyDirector.instance != null) EnemyDirector.instance.UnregisterEnemy(this);
        SafeStopAgent(true);
    }

    public override void GotExecuted()
    {
        currentState = EnemyDogState.Executed;
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
                SoundManager.instance.SwordSound_Execute(sprRndr.transform.position);
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
            //faceToCamera.isFaceYAxis = true;
            capsuleCol.enabled = false;
            agent.enabled = false;

            if (EnemyDirector.instance != null) EnemyDirector.instance.UnregisterEnemy(this);

            if (currentState != EnemyDogState.Executed)
            {
                SoundManager.instance.DogSound_Died(sprRndr.transform.position);
                currentState = EnemyDogState.Died;
            }
        }
        base.Die();
    }

    // ==========================================
    // ANIMATION HASHING
    // ==========================================

    private static readonly Dictionary<EnemyDogState, int> StateToHash = new Dictionary<EnemyDogState, int>
    {
        { EnemyDogState.Idle, Animator.StringToHash("IsIdle") },
        { EnemyDogState.Flank, Animator.StringToHash("IsWalk") },
        { EnemyDogState.Chase, Animator.StringToHash("IsRun") },
        { EnemyDogState.LeapAttack, Animator.StringToHash("IsAttack") },
        { EnemyDogState.DamageLeft, Animator.StringToHash("IsDamageLeft") },
        { EnemyDogState.DamageRight, Animator.StringToHash("IsDamageRight") },
        { EnemyDogState.Parried, Animator.StringToHash("IsParried") },
        { EnemyDogState.Kicked, Animator.StringToHash("IsKicked") },
        { EnemyDogState.Stunned, Animator.StringToHash("IsStunned") },
        { EnemyDogState.Executed, Animator.StringToHash("IsExecuted") },
        { EnemyDogState.Died, Animator.StringToHash("IsDied") }
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