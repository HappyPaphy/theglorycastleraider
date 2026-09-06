using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public enum EnemyArcherState
{
    Idle,
    Reposition, // Replaces Flank/Run to maintain distance
    Attack,
    DamageLeft,
    DamageRight,
    Kicked,
    Stunned,
    Executed,
    Died
}

public class EnemyArcher : EnemyEntity
{
    [SerializeField] private CapsuleCollider capsuleCol;
    [SerializeField] private Animator anim;
    [SerializeField] private SpriteRenderer sprRndr;
    [SerializeField] private float moveSpeed = 3.5f;

    [Header("Ranged Combat Settings")]
    [SerializeField] private GameObject arrowPrefab;
    [SerializeField] private Transform firePoint;            // Where the arrow spawns (e.g., the bow)
    [SerializeField] private float preferredDistance = 12f;  // Ideal distance to shoot from
    [SerializeField] private float retreatDistance = 6f;     // Distance at which the archer runs away
    [SerializeField] private float shootCooldown = 2.5f;
    [SerializeField] private float preAttackDuration = 0.5f; // How long the draw-bow animation takes

    [Header("Kick & Recovery")]
    [SerializeField] private bool isKicked = false;
    [SerializeField] private float kickRecoveryTime = 0.8f; // How long enemy is vulnerable after being parried
    private float kickTimer = 0f;

    private float currentCooldown = 0f;
    private bool isShootingOnce = false;

    [Header("State Settings")]
    [SerializeField] private EnemyArcherState currentState;
    [SerializeField] private float damageStunDuration = 0.5f;
    private float stateTimer = 0f;
    private float stunTimer = 0f;
    [SerializeField] private float stunRecoveryTime = 4f;

    [Header("Gore & Execution")]
    [SerializeField] private Transform headSpawnTransform;
    private bool isExecuted = false;
    private bool isExecutedOnce = false;
    private float executeTimer = 0.1466f;

    private bool isLowHealthStunnedOnce = false;

    protected override void Awake()
    {
        agent.speed = moveSpeed;
        agent.updateRotation = false;
        base.Awake();
    }

    protected override void Start()
    {
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

    private void HandleState()
    {
        if (isDied) { return; }
        if (isExecuted || isStunned) { return; }

        // Tick down cooldowns
        if (currentCooldown > 0f) currentCooldown -= Time.deltaTime;
        if (stateTimer > 0f)
        {
            stateTimer -= Time.deltaTime;
            if (stateTimer <= 0f && !isDied) currentState = EnemyArcherState.Idle;
        }

        switch (currentState)
        {
            case EnemyArcherState.Idle:
                SafeStopAgent(true);
                if (isPlayerDetected && stateTimer <= 0f)
                {
                    currentState = EnemyArcherState.Reposition;
                }
                break;

            case EnemyArcherState.Reposition:
                SafeStopAgent(false);
                isShootingOnce = false;

                if (playerTransform != null)
                {
                    float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);
                    Vector3 lookDir = (playerTransform.position - transform.position).normalized;
                    lookDir.y = 0;

                    // Always look at the player
                    if (lookDir != Vector3.zero)
                    {
                        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lookDir), Time.deltaTime * 10f);
                    }

                    // Tactical Movement: Back away if too close, move forward if too far
                    if (distanceToPlayer < retreatDistance)
                    {
                        Vector3 retreatPos = transform.position - (lookDir * 5f);
                        SafeSetDesitinationAgent(retreatPos);
                    }
                    else if (distanceToPlayer > preferredDistance)
                    {
                        SafeSetDesitinationAgent(playerTransform.position);
                    }
                    else
                    {
                        // In the "Goldilocks" zone - stop moving and shoot
                        SafeStopAgent(true);

                        // Raycast to ensure we aren't shooting a wall[cite: 6]
                        if (currentCooldown <= 0f && !Physics.Raycast(eyesTransform.position, lookDir, distanceToPlayer, obstacleLayer))
                        {
                            currentState = EnemyArcherState.Attack;
                        }
                    }
                }
                break;

            case EnemyArcherState.Attack:
                SafeStopAgent(true);

                if (PlayerController.instance.CharacterHealthComponent.CurrentHP <= 0) { return; }

                if (!isShootingOnce)
                {
                    isShootingOnce = true;
                    StartCoroutine(ShootSequence());
                }
                break;
        }
    }

    private void HandleBalance()
    {
        if (isDied || !isPlayerDetected) { return; }

        if (!isKicked && !isStunned || isExecuted) { return; }

        if (isKicked)
        {
            kickTimer -= Time.deltaTime;
            if (kickTimer <= 0)
            {
                isKicked = false;
            }
        }

        if (isStunned)
        {
            stunTimer -= Time.deltaTime;
            if (stunTimer <= 0)
            {
                isStunned = false;
            }
        }

        if (!isKicked && !isStunned && !isExecuted && !isDied)
        {
            currentState = EnemyArcherState.Reposition;
        }
    }

    private IEnumerator ShootSequence()
    {
        if(isDied) { yield break; }

        // 1. Pre-attack: Draw the bow (Aiming phase)
        SoundManager.instance.BowSound_String(sprRndr.transform.position);

        yield return new WaitForSeconds(preAttackDuration);

        // 2. Fire if not interrupted by damage/death
        if (!isDied && currentState == EnemyArcherState.Attack && playerTransform != null)
        {
            // Calculate direction to player's center/chest
            Vector3 targetPos = playerTransform.position;
            targetPos.y = firePoint.position.y;

            Vector3 shootDir = (targetPos - firePoint.position).normalized;

            GameObject arrowObj = Instantiate(arrowPrefab, firePoint.position, Quaternion.LookRotation(shootDir));
            ArrowProjectile arrow = arrowObj.GetComponent<ArrowProjectile>();

            if (arrow != null)
            {
                arrow.damage = attackDamage;
                arrow.staminaDamage = staminaDamage;
            }

            // Optional: Play bow string sound
            // SoundManager.instance.BowSound_Release();

            currentCooldown = shootCooldown;
            currentState = EnemyArcherState.Reposition;
        }
    }

    // --- Damage & Execution Overrides (Mirrors EnemyThief[cite: 7]) ---

    public override void TakeSwordHit(bool isLeft, Vector3 hitPoint, float damageValue)
    {
        if (isDied) return;

        // Archers don't block. They just take the hit.
        if(!isStunned)
        {
            currentState = isLeft ? EnemyArcherState.DamageLeft : EnemyArcherState.DamageRight;
            stateTimer = damageStunDuration;
        }

        SafeStopAgent(true);

        if (ObjectPoolingManager.instance != null)
        {
            ObjectPoolingManager.instance.SpawnObject("Blood", eyesTransform.position);
        }

        SoundManager.instance.SwordSound_Flesh(sprRndr.transform.position);
        SoundManager.instance.HumanSound_Grunt(sprRndr.transform.position);
        ApplyKnockback(damageKnockbackForce);

        TakeDamage(damageValue); // Derived from EnemyEntity[cite: 6]
    }

    public override void GotStunned()
    {
        currentState = EnemyArcherState.Stunned;
        isStunned = true;
        stunTimer = stunRecoveryTime;
        SafeStopAgent(true);
    }

    public override void GotKicked(Vector3 hitPoint, float damageValue)
    {
        if (!isStunned)
        {
            currentState = EnemyArcherState.Kicked;
            isKicked = true;
            kickTimer = kickRecoveryTime;
        }

        SafeStopAgent(true);

        ApplyKnockback(damageKnockbackForce);

        if (ObjectPoolingManager.instance != null)
        {
            ObjectPoolingManager.instance.SpawnObject("Blood", eyesTransform.position);
        }

        SoundManager.instance.HumanSound_Grunt(sprRndr.transform.position);

        TakeDamage(damageValue);
    }


    public override void GotExecuted()
    {
        currentState = EnemyArcherState.Executed;
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

                if (ObjectPoolingManager.instance != null)
                {
                    ObjectPoolingManager.instance.SpawnObject("Blood", headSpawnTransform.position);
                }

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
        StopAllCoroutines();

        if (!isDiedOnce)
        {
            isDied = true;
            isDiedOnce = true;
            faceToCamera.isFaceYAxis = true;
            capsuleCol.enabled = false;
            agent.enabled = false;

            if (!isExecuted)
            {
                currentState = EnemyArcherState.Died;
                SoundManager.instance.HumanSound_Died(sprRndr.transform.position);
            }
        }
        base.Die();
    }

    // --- Animation Handling ---
    private static readonly Dictionary<EnemyArcherState, int> StateToHash = new Dictionary<EnemyArcherState, int>
    {
        { EnemyArcherState.Idle, Animator.StringToHash("IsIdle") },
        { EnemyArcherState.Reposition, Animator.StringToHash("IsRun") },
        { EnemyArcherState.Attack, Animator.StringToHash("IsAttack") },
        { EnemyArcherState.DamageLeft, Animator.StringToHash("IsDamageLeft") },
        { EnemyArcherState.DamageRight, Animator.StringToHash("IsDamageRight") },
        { EnemyArcherState.Kicked, Animator.StringToHash("IsKicked") },
        { EnemyArcherState.Stunned, Animator.StringToHash("IsStunned") },
        { EnemyArcherState.Executed, Animator.StringToHash("IsExecuted") },
        { EnemyArcherState.Died, Animator.StringToHash("IsDied") }
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
                animGore34.SetBool(lastStateHash, false);
                animGore67.SetBool(lastStateHash, false);
            }

            anim.SetBool(currentHash, true);
            lastStateHash = currentHash;
            animGore34.SetBool(currentHash, true);
            animGore67.SetBool(currentHash, true);
        }
    }
}