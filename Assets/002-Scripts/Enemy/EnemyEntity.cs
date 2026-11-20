using UnityEngine;
using UnityEngine.AI;

public class EnemyEntity : CharacterEntity
{
    public GameObject decapitatedHeadPrefab;
    [SerializeField] protected FaceToCamera faceToCamera;
    public Transform executeTransform;

    [SerializeField] protected SpriteRenderer goreSprite_34HP;
    [SerializeField] protected SpriteRenderer goreSprite_67HP;
    [SerializeField] protected Animator animGore34;
    [SerializeField] protected Animator animGore67;

    [SerializeField] protected RuntimeAnimatorController animationController_34HP;
    [SerializeField] protected RuntimeAnimatorController animationController_67HP;
    [SerializeField] protected RuntimeAnimatorController animationController_100HP;
    [SerializeField] protected NavMeshAgent agent;

    [Header("Knockback Settings")]
    [SerializeField] protected float blockKnockbackForce = 3f;  // How hard they get pushed when blocking
    [SerializeField] protected float knockbackDecay = 10f;      // How quickly they stop sliding
    protected Vector3 currentKnockback = Vector3.zero;

    [Header("CombatBalance")]
    [SerializeField] protected float maxBalance = 100f;
    [SerializeField] protected float currentBalance = 100f;
    [SerializeField] protected float balanceRecoveryRate = 4f;
    public float attackDamage = 10f;
    public float staminaDamage = 18f;

    [Header("Detection Settings")]
    [SerializeField] protected float detectionRadius = 10f;     // Circle overlap range
    [SerializeField] protected float viewAngle = 90f;           // Human eye of sight cone (e.g., 90 degrees)
    public Transform eyesTransform;         // Where the raycast originates (head/eyes)
    [SerializeField] protected LayerMask playerLayer;
    [SerializeField] protected LayerMask obstacleLayer;         // Walls that block vision

    [Header("Backstab Settings")]
    [SerializeField] protected float backstabAngle = 60f;       // Cone behind enemy considered "the back"
    [SerializeField] protected float backstabRange = 2f;        // Max distance to register a stab
    [SerializeField] protected int backstabDamage = 100;        // Instant kill or massive damage

    [Header("State")]
    [SerializeField] protected bool isEnableAgentOnStartOnce = false;
    [HideInInspector] public bool isStunned = false;
    [SerializeField] protected bool isPlayerDetected = false;
    [SerializeField] protected bool isDied = false;
    [SerializeField] protected bool isDiedOnce = false; 

    protected Transform playerTransform;

    /// <summary>Set when this enemy is an elite (see EliteEnemy).</summary>
    [HideInInspector] public EliteEnemy Elite;

    /// <summary>Movement speed multiplier from elite traits (Swift).</summary>
    protected float SpeedMultiplier => (Elite != null ? Elite.SpeedMultiplier : 1f) * bonusSpeedMultiplier;

    /// <summary>Extra speed on top of elite traits (e.g. a boss enrage). Scales movement, animation and attack timings.</summary>
    [HideInInspector] public float bonusSpeedMultiplier = 1f;

    [Tooltip("Can this enemy roll as an elite (spawners, challenge rooms)? Off = always a normal enemy.")]
    public bool canBeElite = true;
    public bool IsPlayerDetected => isPlayerDetected;
    public bool IsDead => isDied;

    protected void OnEnable()
    {
        if (RoguelikeManager.instance != null)
        {
            if (RoguelikeManager.IsDungeonReady)
            {
                EnableAgent();
            }
            else
            {
                RoguelikeManager.OnDungeonReady += EnableAgent;
            }
        }
    }

    private void OnDisable()
    {
        if (RoguelikeManager.instance != null)
            RoguelikeManager.OnDungeonReady -= EnableAgent;
    }

    private void EnableAgent()
    {
        if (agent != null)
        {
            if (!isEnableAgentOnStartOnce)
            {
                isEnableAgentOnStartOnce = true;
            }

            agent.enabled = true;
        }
    }

    protected override void Awake()
    {
        if (RoguelikeManager.instance != null)
        {
            if (!RoguelikeManager.IsDungeonReady)
            {
                agent.enabled = false;
            }
        }
            
        base.Awake();
    }

    protected override void Start()
    {
        GameObject playerObj = PlayerController.instance.gameObject;

        if (playerObj != null)
        {
            playerTransform = playerObj.transform;
        }

        if(goreSprite_34HP != null && goreSprite_67HP != null)
        {
            goreSprite_34HP.enabled = false;
            goreSprite_67HP.enabled = false;
        }

        base.Start();
    }

    protected override void Update()
    {
        if (RoguelikeManager.IsDungeonReady && agent.enabled == false && !isEnableAgentOnStartOnce)
        {
            EnableAgent();
        }

        HandleGoreSprite();
        CheckPlayerVision();
        HandleKnockback();

        base.Update();
    }

    public virtual void GotStunned()
    {

    }

    public virtual void GotParried(bool isLeftHand)
    {

    }

    public virtual void GotExecuted()
    {

    }

    public virtual void GotKicked(Vector3 hitPoint, float damageValue, float knockBackValue)
    {

    }

    protected void SafeStopAgent(bool stopStatus)
    {
        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            agent.isStopped = stopStatus;
        }
    }

    protected void SafeMoveAgent(Vector3 offset)
    {
        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            agent.Move(offset);
        }
    }

    protected void SafeSetDesitinationAgent(Vector3 pos)
    {
        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            agent.SetDestination(pos);
        }
    }

    // =========================================================
    // OBSTACLE BREAKING
    // While chasing, if the path to the player is blocked by a breakable obstacle (closed door,
    // furniture), the enemy attacks the obstacle with its normal attack until it breaks.
    // =========================================================

    [Header("Coin Drop")]
    [Tooltip("Coins dropped on death (random between min and max, before the Greedy Raider perk bonus).")]
    [SerializeField] protected int coinDropMin = 3;
    [SerializeField] protected int coinDropMax = 7;
    [Tooltip("Most coin pickups one death spawns; the total is split across them.")]
    [SerializeField] protected int coinDropMaxPieces = 5;
    private bool hasDroppedCoins;

    [Header("Obstacle Breaking")]
    [SerializeField] protected bool canBreakObstacles = true;
    [Tooltip("Damage per hit on doors/furniture (door = 20 HP, crate = 5 HP).")]
    [SerializeField] protected float obstacleDamagePerHit = 4f;
    [Tooltip("How close the obstacle must be to attack it.")]
    [SerializeField] protected float obstacleReach = 1.7f;
    [SerializeField] protected LayerMask obstacleBreakLayers = 1 << 10; // Destructable
    [Tooltip("Seconds of not making progress before nearby furniture counts as blocking.")]
    [SerializeField] protected float obstacleStuckTime = 0.8f;

    protected Fracture blockingObstacle;
    private Collider blockingObstacleCollider;
    private float obstacleCheckTimer;
    private float obstacleStuckTimer;

    /// <summary>
    /// Call every frame while chasing the target. Returns true when a breakable obstacle is blocking the way
    /// and is within reach (the caller should face it and start its attack).
    /// </summary>
    protected bool UpdateBlockingObstacle(Vector3 targetPosition)
    {
        if (!canBreakObstacles || isDied || isStunned)
        {
            ClearBlockingObstacle();
            return false;
        }

        // Track "trying to move but not getting anywhere" (e.g. furniture in a corridor that doesn't carve)
        bool wantsToMove = agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh && !agent.isStopped &&
                           agent.hasPath && agent.remainingDistance > obstacleReach;
        obstacleStuckTimer = wantsToMove && agent.velocity.sqrMagnitude < 0.04f ? obstacleStuckTimer + Time.deltaTime : 0f;

        obstacleCheckTimer -= Time.deltaTime;
        if (obstacleCheckTimer <= 0f)
        {
            obstacleCheckTimer = 0.25f;

            bool pathBlocked = agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh && !agent.pathPending &&
                               agent.pathStatus != NavMeshPathStatus.PathComplete; // closed doors carve the NavMesh
            bool stuck = obstacleStuckTimer >= obstacleStuckTime;

            if (blockingObstacle == null || !IsObstacleUsable(blockingObstacle))
            {
                ClearBlockingObstacle();
                if (pathBlocked || stuck || IsLineToTargetBlocked(targetPosition))
                {
                    FindBlockingObstacle(targetPosition);
                }
            }
            else if (!pathBlocked && !stuck && !IsLineToTargetBlocked(targetPosition))
            {
                // The way opened up (door opened by the player, etc.)
                ClearBlockingObstacle();
            }
        }

        return blockingObstacle != null && DistanceToObstacle() <= obstacleReach;
    }

    /// <summary>Turn to face the current obstacle (call while attacking it).</summary>
    protected void FaceBlockingObstacle(float turnSpeed)
    {
        if (blockingObstacleCollider == null) { return; }

        Vector3 dir = blockingObstacleCollider.ClosestPoint(transform.position) - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.0001f)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * turnSpeed);
        }
    }

    /// <summary>
    /// Call at the moment an attack lands and missed the player. Damages the blocking obstacle if it's in reach.
    /// </summary>
    protected bool HitBlockingObstacle(float damageMultiplier = 1f)
    {
        if (blockingObstacle == null || !IsObstacleUsable(blockingObstacle)) { return false; }
        if (DistanceToObstacle() > obstacleReach + 0.3f) { return false; }

        Vector3 point = blockingObstacleCollider.ClosestPoint(eyesTransform != null ? eyesTransform.position : transform.position);
        blockingObstacle.TakeDamage(obstacleDamagePerHit * damageMultiplier, blockingObstacleCollider, point, point - transform.position);

        FractureTrigger trigger = blockingObstacle.GetComponent<FractureTrigger>();
        if (trigger != null && blockingObstacle.currentHealth > 0f) { trigger.TriggerMaterialSound_Hit(); }

        if (ObjectPoolingManager.instance != null) { ObjectPoolingManager.instance.SpawnObject("Spark", point); }

        if (blockingObstacle.currentHealth <= 0f) { ClearBlockingObstacle(); }
        return true;
    }

    protected bool HasBlockingObstacle => blockingObstacle != null;

    private void FindBlockingObstacle(Vector3 targetPosition)
    {
        Vector3 origin = transform.position + Vector3.up * 0.9f;
        Vector3 toTarget = targetPosition - transform.position;
        toTarget.y = 0f;
        Vector3 toTargetDir = toTarget.sqrMagnitude > 0.001f ? toTarget.normalized : transform.forward;

        Fracture best = null;
        Collider bestCol = null;
        float bestScore = float.MaxValue;

        int found = PhysicsQuery.Overlap(origin, obstacleReach + 1.5f, obstacleBreakLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < found; i++)
        {
            Collider col = PhysicsQuery.Buffer[i];
            Fracture f = col.GetComponentInParent<Fracture>();
            if (f == null || !IsObstacleUsable(f)) { continue; }

            Vector3 closest = col.ClosestPoint(transform.position);
            Vector3 toObstacle = closest - transform.position;
            toObstacle.y = 0f;
            float dist = toObstacle.magnitude;

            // Must be roughly between us and the target
            float facing = dist > 0.05f ? Vector3.Dot(toObstacle / dist, toTargetDir) : 1f;
            if (facing < 0.2f) { continue; }

            // Prefer close obstacles that are straight ahead toward the target
            float score = dist - facing;
            if (score < bestScore)
            {
                bestScore = score;
                best = f;
                bestCol = col;
            }
        }

        blockingObstacle = best;
        blockingObstacleCollider = bestCol;
    }

    // A closed door sits on the straight line to the target. (Only doors: furniture is walked around
    // unless it actually blocks the path or gets the enemy stuck.)
    private bool IsLineToTargetBlocked(Vector3 targetPosition)
    {
        Vector3 origin = transform.position + Vector3.up * 0.9f;
        Vector3 dir = (targetPosition + Vector3.up * 0.9f) - origin;
        float dist = Mathf.Min(dir.magnitude, obstacleReach + 1.5f);
        if (dist < 0.05f) { return false; }

        if (!Physics.Raycast(origin, dir.normalized, out RaycastHit hit, dist, obstacleBreakLayers, QueryTriggerInteraction.Ignore)) { return false; }

        DoorInteractable door = hit.collider.GetComponentInParent<DoorInteractable>();
        return door != null && !door.IsOpen && hit.collider.GetComponentInParent<Fracture>() != null;
    }

    private static bool IsObstacleUsable(Fracture f)
    {
        if (f == null || !f.isActiveAndEnabled || !f.isCanBeDamage || f.currentHealth <= 0f) { return false; }

        // An open door isn't in the way
        DoorInteractable door = f.GetComponent<DoorInteractable>();
        return door == null || !door.IsOpen;
    }

    private float DistanceToObstacle()
    {
        if (blockingObstacleCollider == null) { return float.MaxValue; }
        Vector3 closest = blockingObstacleCollider.ClosestPoint(transform.position);
        closest.y = transform.position.y;
        return Vector3.Distance(transform.position, closest);
    }

    protected void ClearBlockingObstacle()
    {
        blockingObstacle = null;
        blockingObstacleCollider = null;
    }

    protected float SafeGetRemainingDistance()
    {
        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            return agent.remainingDistance;
        }

        return 0f;
    }

    protected void HandleGoreSprite()
    {
        if(goreSprite_34HP == null || goreSprite_67HP == null) { return; }

        if (CharacterHealthComponent.CurrentHP <= CharacterHealthComponent.MaxHP * 0.34f)
        {
            if (!goreSprite_34HP.enabled)
            {
                goreSprite_34HP.enabled = true;
            }

            if (!goreSprite_67HP.enabled)
            {
                goreSprite_67HP.enabled = true;
            }
        }
        else if (CharacterHealthComponent.CurrentHP <= CharacterHealthComponent.MaxHP * 0.67f)
        {
            if (!goreSprite_67HP.enabled)
            {
                goreSprite_67HP.enabled = true;
            }

            if (goreSprite_34HP.enabled)
            {
                goreSprite_34HP.enabled = false;
            }
        }
        else if(CharacterHealthComponent.CurrentHP > CharacterHealthComponent.MaxHP * 0.67f)
        {
            if (goreSprite_67HP.enabled || goreSprite_34HP.enabled)
            {
                goreSprite_34HP.enabled = false;
                goreSprite_67HP.enabled = false;
            }
        }
    }


    protected virtual void HandleKnockback()
    {
        if (isDied) { return; }

        if (currentKnockback.magnitude > 0.1f)
        {
            SafeMoveAgent(currentKnockback * Time.deltaTime);

            currentKnockback = Vector3.Lerp(currentKnockback, Vector3.zero, Time.deltaTime * knockbackDecay);
        }
    }

    protected void ApplyKnockback(float force)
    {
        if (playerTransform != null)
        {
            Vector3 pushDirection = (transform.position - playerTransform.position).normalized;
            pushDirection.y = 0;
            currentKnockback = pushDirection * force;
        }
    }

    protected void CheckPlayerVision()
    {
        if(playerTransform == null) { return; }
        if(isPlayerDetected) { return; }

        // 1. Circle Overlap Check (Is player inside the general radius?)
        bool playerInRadius = PhysicsQuery.Any(transform.position, detectionRadius, playerLayer);

        if (playerInRadius)
        {
            // Player is inside the circle, now check "Eye of Sight" cone
            Vector3 directionToPlayer = (playerTransform.position - eyesTransform.position).normalized;
            float angleToPlayer = Vector3.Angle(eyesTransform.forward, directionToPlayer);

            // 2. Check if player is within the field of view angle
            if (angleToPlayer < viewAngle / 2f)
            {
                // 3. Raycast to ensure no walls are blocking line of sight
                float distanceToPlayer = Vector3.Distance(eyesTransform.position, playerTransform.position);

                if (!Physics.Raycast(eyesTransform.position, directionToPlayer, distanceToPlayer, obstacleLayer))
                {
                    if(slider_HP != null)
                    {
                        slider_HP.gameObject.SetActive(true);
                    }

                    isPlayerDetected = true;
                    Debug.Log("Player spotted in line of sight!");
                    return;
                }
            }
        }

        // If player successfully hid or left the range
        // isPlayerDetected = false; 
    }

    /// <summary>
    /// Called by the player's attack script to check if this enemy can be backstabbed.
    /// </summary>
    public bool CanBeBackstabbed(Transform playerCamTransform)
    {
        // Check distance
        float distanceToPlayer = Vector3.Distance(transform.position, playerCamTransform.position);
        if (distanceToPlayer > backstabRange) return false;

        // Check if player is behind the enemy
        // Vector pointing from player to enemy
        Vector3 toEnemy = transform.position - playerCamTransform.position;
        float angleFromBehind = Vector3.Angle(transform.forward, playerCamTransform.forward);

        // If enemy and player are facing roughly the same direction, player is behind them
        if (angleFromBehind < backstabAngle)
        {
            return true;
        }

        return false;
    }

    public void PlayerDetected()
    {
        isPlayerDetected = true;
    }

    public virtual void TakeSwordHit(bool isLeft, Vector3 hitPoint, float damageValue, bool isBlockable, bool isLeftHand, DamageImpactSound damageImpactSound)
    {

    }

    public virtual void TakeSpellHit(bool isLeft, Vector3 hitPoint, float damageValue, bool isBlockable, float knockBackValue, DamageImpactSound damageImpactSound)
    {

    }

    public virtual void AttackSuccessful()
    {

    }

    public virtual void TakeDamage(float damage)
    {
        if (Elite != null) { damage *= Elite.IncomingDamageMultiplier; } // Warded elites

        // Any damage wakes a knocked-down enemy up
        if (isKnockedDown)
        {
            isKnockedDown = false;
            OnKnockDownInterrupted();
        }

        CharacterHealthComponent.TakeDamage(damage);
    }

    // =========================================================
    // KNOCKDOWN (e.g. Battering Ram): stays in its Kicked state for a while, ends early when damaged.
    // Unlike a stun it does NOT open the enemy up for an execution.
    // =========================================================

    private bool isKnockedDown;

    /// <summary>Keeps the enemy in its Kicked state for <paramref name="duration"/> seconds, or until it takes damage.</summary>
    public void KnockDown(float duration)
    {
        if (CharacterHealthComponent.CurrentHP <= 0f || isStunned) { return; }
        if (OnKnockDown(duration)) { isKnockedDown = true; }
    }

    /// <summary>Enter / extend the Kicked state. Return false if this enemy can't be knocked down.</summary>
    protected virtual bool OnKnockDown(float duration) { return false; }

    /// <summary>Took damage while knocked down: end the Kicked state now.</summary>
    protected virtual void OnKnockDownInterrupted() { }

    public override void Die()
    {
        // Die() runs every frame while HP is 0: report the kill to the perks only once
        // (an execution already reported it as an execution kill)
        if (!hasReportedKill)
        {
            hasReportedKill = true;
            if (PlayerPerkManager.Instance != null) { PlayerPerkManager.Instance.OnEnemyKilled(false); }
        }

        DropCoins();
        base.Die();
    }

    private bool hasReportedKill;

    /// <summary>Called when the kill was already reported to the perks (e.g. as an execution).</summary>
    public void MarkKillReported() => hasReportedKill = true;

    /// <summary>A random coin amount for this enemy, including the Greedy Raider bonus.</summary>
    public int RollCoinDrop()
    {
        int coins = Random.Range(Mathf.Min(coinDropMin, coinDropMax), Mathf.Max(coinDropMin, coinDropMax) + 1);
        if (PlayerPerkManager.Instance != null) { coins = PlayerPerkManager.Instance.CalculateGoldDrop(coins); } // Greedy Raider
        coins = Mathf.RoundToInt(coins * PlayerCurses.CoinDropMultiplier); // curses (e.g. Greedless)
        return coins;
    }

    // Die() runs every frame while HP is 0, so drop only once
    private void DropCoins()
    {
        if (hasDroppedCoins) { return; }
        hasDroppedCoins = true;

        int coins = RollCoinDrop();
        if (coins <= 0) { return; }

        Transform from = eyesTransform != null ? eyesTransform : transform;
        Vector3 origin = new Vector3(transform.position.x, Mathf.Max(transform.position.y + 0.6f, from.position.y - 0.4f), transform.position.z);
        CoinPickup.Drop(origin, coins, coinDropMaxPieces);
    }

    private void OnDrawGizmosSelected()
    {
        Transform eyeRef = eyesTransform != null ? eyesTransform : transform;

        // 1. Draw the Outer Detection Radius (Yellow Circle)
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);

        // 2. Draw the Backstab Range (Red Circle)
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, backstabRange);

        // 3. Draw the Field of View Cone (Cyan Lines)
        Gizmos.color = Color.cyan;

        // Calculate the left and right boundaries of the vision cone
        Vector3 leftRayDirection = Quaternion.Euler(0, -viewAngle / 2f, 0) * eyeRef.forward;
        Vector3 rightRayDirection = Quaternion.Euler(0, viewAngle / 2f, 0) * eyeRef.forward;

        Gizmos.DrawRay(eyeRef.position, leftRayDirection * detectionRadius);
        Gizmos.DrawRay(eyeRef.position, rightRayDirection * detectionRadius);

        // 4. Draw Backstab Angle Cone (Magenta Lines behind the enemy)
        Gizmos.color = Color.magenta;
        Vector3 backLeftRay = Quaternion.Euler(0, -backstabAngle / 2f, 0) * (-transform.forward);
        Vector3 backRightRay = Quaternion.Euler(0, backstabAngle / 2f, 0) * (-transform.forward);

        Gizmos.DrawRay(transform.position, backLeftRay * backstabRange);
        Gizmos.DrawRay(transform.position, backRightRay * backstabRange);
    }
}
