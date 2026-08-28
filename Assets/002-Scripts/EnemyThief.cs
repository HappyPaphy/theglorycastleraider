using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public enum EnemyThiefState
{
    Idle,
    Run,
    Flank,
    Attack,
    DamageLeft,
    DamageRight,
    BlockLeft,
    BlockRight,
    Parried,
    Kicked,
    Stunned,
    Executed,
    Died
}

public class EnemyThief : EnemyEntity
{
    [SerializeField] private CapsuleCollider capsuleCol;

    [SerializeField] private GameObject bloodEffect;
    [SerializeField] private GameObject sparkEffect;
    [SerializeField] private SpriteRenderer sprRndr;
    [SerializeField] private EnemyThiefState currentState;
    [SerializeField] private Animator anim;
    [SerializeField] private float moveSpeed;

    [Header("Combat Dynamics")]
    [SerializeField] private int maxHitsBeforeBlock = 3; // X times enemy can be hit before blocking
    [SerializeField] private int currentHitCount = 0;
    [SerializeField] private float comboResetTime = 1.5f; // How long before the hit counter resets
    [SerializeField] private float attackRadius = 0.5f;
    [SerializeField] private float attackRange = 1.4f;
    private float hitResetTimer = 0f;
    private float flankTimer = 0f;
    private Vector3 currentFlankDestination;

    [SerializeField] protected float preAttackRadius = 10f;     // Circle overlap range
    [SerializeField] protected float preAttackAngle = 90f;

    [Header("Parry & Recovery")]
    [SerializeField] private bool isParried = false;
    [SerializeField] private float parryRecoveryTime = 1.167f; // How long enemy is vulnerable after being parried
    private float parryTimer = 0f;

    [Header("Kick & Recovery")]
    [SerializeField] private bool isKicked = false;
    [SerializeField] private float kickRecoveryTime = 0.8f; // How long enemy is vulnerable after being parried
    private float kickTimer = 0f;

    [Header("Stunned & Recovery")]
    [SerializeField] private float stunRecoveryTime = 4f;
    private float stunTimer = 0f;

    [Header("Executed & Head")]
    [SerializeField] private GameObject headPrefab;
    [SerializeField] private Transform headSpawnTransform;
    [SerializeField] private RuntimeAnimatorController headAnimationController;

    private bool isExecuted = false;
    private bool isExecutedOnce = false;
    [SerializeField] private float executeDuration = 0.1466f; // All 0.293f
    private float executeTimer = 0f;

    [Header("State Timers")]
    [SerializeField] private float damageStunDuration = 0.5f; // How long the damage animation plays
    [SerializeField] private float blockDuration = 1f;        // How long the block animation plays
    private float stateTimer = 0f;

    [Header("Turn-Swapping Settings")]
    [SerializeField] private float turnCooldownDuration = 3f;   // Cooldown before they can volunteer to attack again
    private float engagementTimer = 0f;
    private float turnCooldownTimer = 0f;

    private float attackCooldownDuration = 1.5f;
    private float currentAttackCooldown = 0f;
    private float preAttackDuration = 0.438f;
    private bool isPreAttackOnce = false;
    private bool isLowHealthStunnedOnce = false;

    protected override void Awake()
    {
        agent.speed = moveSpeed;
        agent.updateRotation = false;

        base.Awake();
    }

    protected override void Start()
    {
        executeTimer = executeDuration;
        faceToCamera.isFaceYAxis = false;
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

    private void CheckAttackVision()
    {
        if (isDied) { return; }
        if (playerTransform == null) { return; }

        Collider[] hits = Physics.OverlapSphere(transform.position, preAttackRadius, playerLayer);

        if (hits.Length > 0)
        {
            Vector3 directionToPlayer = (playerTransform.position - eyesTransform.position).normalized;

            // FIX: Use transform.forward instead of eyesTransform.forward!
            float angleToPlayer = Vector3.Angle(transform.forward, directionToPlayer);

            if (angleToPlayer < preAttackAngle / 2)
            {
                float distanceToPlayer = Vector3.Distance(eyesTransform.position, playerTransform.position);

                if (!Physics.Raycast(eyesTransform.position, directionToPlayer, distanceToPlayer, obstacleLayer))
                {
                    currentState = EnemyThiefState.Attack;
                    return;
                }
            }
        }
    }

    private void HandleBalance()
    {
        if (isDied || !isPlayerDetected) { return; }

        if (turnCooldownTimer > 0f)
        {
            turnCooldownTimer -= Time.deltaTime;
        }

        // 1. Decay the combo hit counter if the player stops attacking
        if (hitResetTimer > 0 && !isParried)
        {
            hitResetTimer -= Time.deltaTime;
            if (hitResetTimer <= 0)
            {
                currentHitCount = 0; // Reset the hit counter
            }
        }

        if (!isParried && !isKicked && !isStunned || isExecuted) { return; }

        // 2. Handle the parry vulnerability timer
        if (isParried)
        {
            engagementTimer -= Time.deltaTime;
            parryTimer -= Time.deltaTime;
            if (parryTimer <= 0)
            {
                // Enemy recovered from parry, back to normal
                isParried = false;
                currentHitCount = 0;
            }
        }

        if(isKicked)
        {
            engagementTimer -= Time.deltaTime;
            kickTimer -= Time.deltaTime;
            if(kickTimer <= 0)
            {
                isKicked = false;
            }
        }

        if(isStunned)
        {
            engagementTimer -= Time.deltaTime;
            stunTimer -= Time.deltaTime;
            if(stunTimer <= 0)
            {
                isStunned = false;
            }
        }

        if(!isParried && !isKicked && !isStunned && !isExecuted)
        {
            currentState = EnemyThiefState.Run;
        }

        if (currentState == EnemyThiefState.Run)
        {
            anim.speed = 1.8f;
            agent.speed = moveSpeed * 1.8f;
        }
        else
        {
            anim.speed = 1f;
            agent.speed = moveSpeed;
        }
    }

    public override void TakeSwordHit(bool isLeft, Vector3 hitPoint, float damageValue)
    {
        if (isDied) { return; }

        // Refresh the combo reset timer every time a hit lands
        hitResetTimer = comboResetTime;

        if (isParried)
        {
            // If the enemy is in a parried state, they take the hit no matter what
            ApplyDamageState(isLeft, hitPoint, damageValue);
        }
        else
        {
            if (currentHitCount >= maxHitsBeforeBlock)
            {
                // Enemy has taken X hits and will now block
                ApplyBlockState(isLeft);
            }
            else
            {
                // Enemy takes the hit and increments the counter
                currentHitCount++;
                ApplyDamageState(isLeft, hitPoint, damageValue);
            }
        }
    }

    /// <summary>
    /// Call this method from your player's script when the player successfully parries this enemy.
    /// </summary>
    public override void GotParried()
    {
        currentState = EnemyThiefState.Parried;
        isParried = true;
        parryTimer = parryRecoveryTime;
        currentHitCount = 0;

        SoundManager.instance.HumanSound_Parried();

        agent.isStopped = true;

        ApplyKnockback(damageKnockbackForce);
    }

    public override void GotExecuted()
    {
        currentState = EnemyThiefState.Executed;
        isExecuted = true;

        agent.isStopped = true;
    }

    public override void GotStunned()
    {
        currentState = EnemyThiefState.Stunned;
        isStunned = true;
        stunTimer = stunRecoveryTime;
        currentHitCount = 0;

        if (EnemyDirector.Instance != null)
        {
            EnemyDirector.Instance.UnregisterEnemy(this);
        }

        agent.isStopped = true;
    }

    private void HandleExecute()
    {
        if(CharacterHealthComponent.CurrentHP <= CharacterHealthComponent.MaxHP * CharacterHealthComponent.ExecutePercentage && !isLowHealthStunnedOnce)
        {
            isLowHealthStunnedOnce = true;
            GotStunned();
        }

        if(!isExecutedOnce && isExecuted)
        {
            if(executeTimer <= 0f)
            {
                isExecutedOnce = true;

                SoundManager.instance.SwordSound_Flesh();
                SoundManager.instance.SwordSound_Execute();

                GameObject bloodObj = Instantiate(bloodEffect);
                bloodObj.transform.position = headSpawnTransform.position;

                /*GameObject headObj = Instantiate(headPrefab);
                headObj.transform.position = headSpawnTransform.position;
                headObj.GetComponent<Animator>().runtimeAnimatorController = headAnimationController;*/
                CharacterHealthComponent.SetHP(0f);
            }
            else
            {
                executeTimer -= Time.deltaTime;
            }
        }
    }

    public override void GotKicked(Vector3 hitPoint, float damageValue)
    {
        if (!isStunned)
        {
            currentState = EnemyThiefState.Kicked;
            isKicked = true;
            kickTimer = kickRecoveryTime;
        }
            
        if (currentHitCount > 0)
        {
            currentHitCount -= 2;

            if (currentHitCount < 0)
            {
                currentHitCount = 0;
            }
        }

        agent.isStopped = true;

        ApplyKnockback(damageKnockbackForce);

        GameObject bloodObj = Instantiate(bloodEffect);
        bloodObj.transform.position = eyesTransform.position;
        SoundManager.instance.HumanSound_Grunt();

        TakeDamage(damageValue);
    }

    private void ApplyDamageState(bool isLeft, Vector3 hitPoint, float damageValue)
    {
        if (isDied) { return; }

        if(!isStunned)
        {
            currentState = isLeft ? EnemyThiefState.DamageLeft : EnemyThiefState.DamageRight;
            stateTimer = damageStunDuration;
        }
        

        GameObject bloodObj = Instantiate(bloodEffect);
        bloodObj.transform.position = eyesTransform.position;
        SoundManager.instance.SwordSound_Flesh();
        SoundManager.instance.HumanSound_Grunt();

        agent.isStopped = true;
        ApplyKnockback(damageKnockbackForce);

        TakeDamage(damageValue);
    }

    public override void Die()
    {
        if(!isDiedOnce)
        {
            isDied = true;
            isDiedOnce = true;

            faceToCamera.isFaceYAxis = true;
            capsuleCol.enabled = false;
            agent.enabled = false;

            if (EnemyDirector.Instance != null)
            {
                EnemyDirector.Instance.UnregisterEnemy(this);
            }

            if (!isExecuted)
            {
                currentState = EnemyThiefState.Died;
                SoundManager.instance.HumanSound_Died();
            }
        }

        base.Die();
    }

    private void ApplyBlockState(bool isLeft)
    {
        if(isStunned) { return; }

        currentState = isLeft ? EnemyThiefState.BlockLeft : EnemyThiefState.BlockRight;
        stateTimer = blockDuration;

        GameObject sparkObj = Instantiate(sparkEffect);
        sparkObj.transform.position = eyesTransform.position;
        SoundManager.instance.SwordSound_Metal();

        agent.isStopped = true;

        // Apply Light Knockback (sliding backward while blocking)
        ApplyKnockback(blockKnockbackForce);
    }

    public override void AttackSuccessful()
    {
        currentState = EnemyThiefState.Run;
    }

    private void HandleState()
    {
        if(isDied) { return; }

        // Countdown the timer for temporary states (Damage, Block, etc.)
        if (stateTimer > 0 && !isStunned)
        {
            engagementTimer -= Time.deltaTime;
            stateTimer -= Time.deltaTime;
            if (stateTimer <= 0)
            {
                // Timer finished, return to default behavior
                currentState = EnemyThiefState.Run;
            }
        }

        if (currentAttackCooldown > 0f)
        {
            currentAttackCooldown -= Time.deltaTime;
        }

        switch (currentState)
        {
            case EnemyThiefState.Idle:
                {
                    agent.isStopped = true;

                    if (isPlayerDetected)
                    {
                        if (EnemyDirector.Instance != null && EnemyDirector.Instance.RequestAttackPermission(this))
                        {
                            engagementTimer = Random.Range(5f, 8f);
                            currentState = EnemyThiefState.Run;
                        }
                        else
                        {
                            // If slots are full, enter a flanking/waiting pattern instead of swarming!
                            currentState = EnemyThiefState.Flank;
                        }
                    }
                }
                break;

            case EnemyThiefState.Run:
                {
                    agent.isStopped = false;
                    isPreAttackOnce = false;

                    engagementTimer -= Time.deltaTime;
                    if (engagementTimer <= 0f)
                    {
                        YieldTurn();
                        break;
                    }

                    if (PlayerController.instance != null)
                    {
                        Transform target = PlayerController.instance.transform;
                        agent.SetDestination(target.position);

                        Vector3 lookDir = (target.position - transform.position).normalized;
                        lookDir.y = 0;
                        if (lookDir != Vector3.zero)
                        {
                            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lookDir), Time.deltaTime * 10f);
                        }

                        // FIX 2: Only tick the engagement timer if they are close enough to be a threat!
                        // (e.g., within 6 meters). If they are far away, the clock pauses so they can catch up.
                        if (Vector3.Distance(transform.position, target.position) <= 6f)
                        {
                            engagementTimer -= Time.deltaTime;

                            if (engagementTimer <= 0f)
                            {
                                YieldTurn();
                                break;
                            }
                        }
                    }

                    if(currentAttackCooldown <= 0f)
                    {
                        CheckAttackVision();
                    }
                }
                break;

            case EnemyThiefState.Flank:
                {
                    agent.isStopped = false;

                    // Periodically check if an attacker slot has opened up
                    if (turnCooldownTimer <= 0f && EnemyDirector.Instance != null && EnemyDirector.Instance.RequestAttackPermission(this))
                    {
                        engagementTimer = Random.Range(5f, 8f);
                        currentState = EnemyThiefState.Run;
                        break;
                    }

                    if (playerTransform != null)
                    {
                        Transform target = playerTransform;

                        // REALISM FIX: Always keep eyes on the player while flanking!
                        Vector3 lookDir = (target.position - transform.position).normalized;
                        lookDir.y = 0;
                        if (lookDir != Vector3.zero)
                        {
                            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lookDir), Time.deltaTime * 10f);
                        }

                        // REALISM FIX: Tactical Backpedaling. If player gets within 3 meters, back away!
                        if (Vector3.Distance(transform.position, target.position) < 3f)
                        {
                            Vector3 retreatPos = transform.position - lookDir * 4f; // Push destination backward
                            agent.SetDestination(retreatPos);
                        }
                        else
                        {
                            // Normal Strafe Logic
                            flankTimer -= Time.deltaTime;
                            if (flankTimer <= 0f || agent.remainingDistance < 1f)
                            {
                                float randomAngleOffset = Random.Range(-80f, 80f);
                                float randomRadius = Random.Range(3, 5);

                                Vector3 forwardDir = target.forward;
                                Vector3 rotatedDirection = Quaternion.Euler(0f, randomAngleOffset, 0f) * forwardDir;

                                Vector3 offset = rotatedDirection * randomRadius;
                                currentFlankDestination = target.position + offset;

                                flankTimer = Random.Range(1.8f, 2.5f);
                            }
                            agent.SetDestination(currentFlankDestination);
                        }
                    }
                }
                break;

            case EnemyThiefState.Attack:
                {
                    agent.isStopped = false;
                    engagementTimer -= Time.deltaTime;

                    if (!isPreAttackOnce)
                    {
                        isPreAttackOnce = true;
                        StartCoroutine(AttackSequence());
                    }
                }
                break;
        }
    }

    private void YieldTurn()
    {
        if (EnemyDirector.Instance != null)
        {
            // FIX: Always release the slot so the next enemy can take their turn!
            EnemyDirector.Instance.ReleaseAttackPermission(this);
        }

        currentState = EnemyThiefState.Flank;
        turnCooldownTimer = turnCooldownDuration; // Force them to wait before attacking again
    }

    private IEnumerator AttackSequence()
    {
        SoundManager.instance.HumanSound_Attack();

        yield return new WaitForSeconds(preAttackDuration);

        if (currentState == EnemyThiefState.Attack)
        {
            Vector3 startPos = sprRndr.gameObject.transform.position;
            Vector3 endPos = startPos + (sprRndr.gameObject.transform.forward * attackRange);

            Collider[] hitPlayers = Physics.OverlapCapsule(startPos, endPos, attackRadius, playerLayer);

            if (hitPlayers.Length > 0)
            {
                hitPlayers[0].GetComponent<PlayerController>().TakeSwordHit(this);
            }
            else
            {
                currentState = EnemyThiefState.Run;
            }

            // Apply the cooldown
            currentAttackCooldown = attackCooldownDuration;

            // 3. IMPORTANT: Free the enemy from the Attack state so they can move again
            
        }
    }

    private static readonly Dictionary<EnemyThiefState, int> StateToHash = new Dictionary<EnemyThiefState, int>
    {
        { EnemyThiefState.Idle, Animator.StringToHash("IsIdle") },
        { EnemyThiefState.Run, Animator.StringToHash("IsRun") },
        { EnemyThiefState.Flank, Animator.StringToHash("IsRun") },
        { EnemyThiefState.Attack, Animator.StringToHash("IsAttack") },
        { EnemyThiefState.BlockLeft, Animator.StringToHash("IsBlockLeft") },
        { EnemyThiefState.BlockRight, Animator.StringToHash("IsBlockRight") },
        { EnemyThiefState.DamageLeft, Animator.StringToHash("IsDamageLeft") },
        { EnemyThiefState.DamageRight, Animator.StringToHash("IsDamageRight") },
        { EnemyThiefState.Parried, Animator.StringToHash("IsParried") },
        { EnemyThiefState.Kicked, Animator.StringToHash("IsKicked") },
        { EnemyThiefState.Stunned, Animator.StringToHash("IsStunned") },
        { EnemyThiefState.Executed, Animator.StringToHash("IsExecuted") },
        { EnemyThiefState.Died, Animator.StringToHash("IsDied") }
    };

    private int lastStateHash; // Keep track of the last active hash

    private void ChooseAnimation()
    {
        // 1. Get the hash for the current state
        if (StateToHash.TryGetValue(currentState, out int currentHash))
        {
            // 2. Only update if the state actually changed
            if (currentHash == lastStateHash) return;

            // 3. Reset the previous animation and set the new one
            if (lastStateHash != 0)
            {
                anim.SetBool(lastStateHash, false);
                animGore34.SetBool(lastStateHash, false);
                animGore67.SetBool(lastStateHash, false);
            }

            anim.SetBool(currentHash, true);
            animGore34.SetBool(currentHash, true);
            animGore67.SetBool(currentHash, true);

            lastStateHash = currentHash;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 trueForward = sprRndr.gameObject.transform.forward;
        Vector3 originPos = sprRndr.gameObject.transform.position;

        // --- 1. PRE-ATTACK VISION CONE (Orange) ---
        Gizmos.color = new Color(1f, 0.5f, 0f);
        Gizmos.DrawWireSphere(originPos, preAttackRadius);

        Vector3 leftPreAttackRay = Quaternion.Euler(0, -preAttackAngle, 0) * trueForward;
        Vector3 rightPreAttackRay = Quaternion.Euler(0, preAttackAngle, 0) * trueForward;

        Gizmos.DrawRay(originPos, leftPreAttackRay * preAttackRadius);
        Gizmos.DrawRay(originPos, rightPreAttackRay * preAttackRadius);

        // --- 2. PHYSICAL ATTACK SPHERECAST (Red) ---
        Gizmos.color = Color.red;

        Gizmos.DrawRay(originPos, trueForward * attackRange);
        Gizmos.DrawWireSphere(originPos, attackRadius);

        Vector3 endPosition = originPos + (trueForward * attackRange);
        Gizmos.DrawWireSphere(endPosition, attackRadius);
    }
}
