using UnityEngine;
using UnityEngine.AI;

public class EnemyEntity : CharacterEntity
{
    [SerializeField] protected FaceToCamera faceToCamera;
    public Transform executeTransform;

    [SerializeField] protected SpriteRenderer goreSprite_34HP;
    [SerializeField] protected SpriteRenderer goreSprite_67HP;
    [SerializeField] protected Animator animGore34;
    [SerializeField] protected Animator animGore67;
    [SerializeField] protected NavMeshAgent agent;

    [Header("Knockback Settings")]
    [SerializeField] protected float damageKnockbackForce = 8f; // How hard they get pushed when hurt
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
    [HideInInspector] public bool isStunned = false;
    protected bool isPlayerDetected = false;
    [SerializeField] protected bool isDied = false;
    [SerializeField] protected bool isDiedOnce = false; 

    protected Transform playerTransform;

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
        agent.enabled = true;
    }

    protected override void Awake()
    {
        if(RoguelikeManager.instance != null)
        {
            agent.enabled = false;
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

        goreSprite_34HP.enabled = false;
        goreSprite_67HP.enabled = false;


        base.Start();
    }

    protected override void Update()
    {
        HandleGoreSprite();
        CheckPlayerVision();
        HandleKnockback();

        base.Update();
    }

    public virtual void GotStunned()
    {

    }

    public virtual void GotParried()
    {

    }

    public virtual void GotExecuted()
    {

    }

    public virtual void GotKicked(Vector3 hitPoint, float damageValue)
    {

    }

    protected void SafeStopAgent(bool stopStatus)
    {
        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            agent.isStopped = stopStatus;
        }
    }

    protected void HandleGoreSprite()
    {
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
            agent.Move(currentKnockback * Time.deltaTime);
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

    private void CheckPlayerVision()
    {
        if(playerTransform == null) { return; }
        if(isPlayerDetected) { return; }

        // 1. Circle Overlap Check (Is player inside the general radius?)
        Collider[] hits = Physics.OverlapSphere(transform.position, detectionRadius, playerLayer);

        if (hits.Length > 0)
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
                    // Successfully spotted!
                    isPlayerDetected = true;
                    Debug.Log("Player spotted in line of sight!");
                    // TODO: Trigger enemy attack/chase state here
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

    public virtual void TakeSwordHit(bool isLeft, Vector3 hitPoint, float damageValue)
    {

    }

    public virtual void AttackSuccessful()
    {

    }

    public virtual void TakeDamage(float damage)
    {
        CharacterHealthComponent.TakeDamage(damage);

        
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
