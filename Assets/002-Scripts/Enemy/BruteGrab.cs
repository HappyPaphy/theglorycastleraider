using System.Collections;
using UnityEngine;

/// <summary>
/// The Brute's grab (replaces its normal attack).
///  1. Wind-up: when the player is in range he stops and exposes an opening (glows yellow). KICK him now to
///     cancel the grab: he's knocked down (Kicked -> Loop Kicked) for a follow-up. Kicks at other times are blocked.
///  2. If the player is still in range when the wind-up ends, he grabs: the player is locked in place, can't
///     move or attack, can't be hit by other enemies, and takes hug damage every tick.
///  3. Mash Jump (Space / gamepad South) to fill the struggle bar and break free.
/// </summary>
public class BruteGrab : MonoBehaviour
{
    private enum GrabState { Ready, Windup, Holding }

    [Header("Grab")]
    [Tooltip("Starts the grab when the player is this close.")]
    public float grabRange = 2.2f;
    [Tooltip("Seconds of opening before the grab lands: the kick window.")]
    [Min(0.1f)] public float windupTime = 1.1f;
    [Tooltip("Seconds before he can try another grab.")]
    public float grabCooldown = 4f;
    public Color openingColor = new Color(1f, 0.9f, 0.3f);

    [Header("Hug")]
    public float hugDamagePerSecond = 7f;
    [Tooltip("Hug damage is dealt in ticks this far apart.")]
    public float damageTick = 0.35f;
    [Tooltip("Where the player is held, in front of him (metres).")]
    public float holdDistance = 0.7f;
    [Tooltip("How high the player is lifted while hugged (metres), so he's face to face with the Brute.")]
    public float liftHeight = 0.9f;
    [Tooltip("Height of his face above his feet (metres): the player's camera locks onto it while hugged.")]
    public float faceHeight = 2.6f;

    public Vector3 FacePoint => transform.position + Vector3.up * faceHeight;

    [Header("Break free (mash Jump)")]
    [Tooltip("Struggle gained per Jump press (1 = free).")]
    [Range(0.02f, 1f)] public float strugglePerPress = 0.1f;
    [Tooltip("Struggle lost per second if you stop mashing.")]
    public float struggleDecay = 0.35f;
    [Tooltip("How hard the player is pushed away when breaking free.")]
    public float releasePush = 6f;

    [Header("Refs")]
    public SpriteRenderer body;

    private EnemyEntity owner;
    private GrabState state = GrabState.Ready;
    private float cooldown;
    private float struggle;
    private float tickTimer;
    private int ticks;
    private Vector3 holdDirection = Vector3.forward;
    private Coroutine windup;
    private Color baseColor = Color.white;

    /// <summary>Winding up or holding: the enemy's own AI should wait.</summary>
    public bool IsBusy => state != GrabState.Ready;
    public bool IsHolding => state == GrabState.Holding;
    public Vector3 HoldPoint => transform.position + holdDirection * holdDistance;

    private void Awake()
    {
        owner = GetComponent<EnemyEntity>();
        if (owner != null) { owner.canBeElite = false; } // Brutes are never elites (runs before the spawner's elite roll)
    }

    private void Start()
    {
        if (body != null) { baseColor = body.color; }
    }

    /// <summary>Called by the enemy AI while chasing: begins the wind-up if the player is close enough.</summary>
    public bool TryStart()
    {
        if (state != GrabState.Ready || cooldown > 0f) { return false; }
        PlayerController player = PlayerController.instance;
        if (player == null || player.isHeld || player.isExecuting || player.CharacterHealthComponent.CurrentHP <= 0f) { return false; }
        if (FlatDistance(player.transform.position) > grabRange) { return false; }

        windup = StartCoroutine(Windup());
        return true;
    }

    /// <summary>Kicked: cancels the grab if he's winding up. Returns true if it did.</summary>
    public bool InterruptByKick()
    {
        if (state != GrabState.Windup) { return false; }
        if (windup != null) { StopCoroutine(windup); }
        windup = null;
        RestoreColor();
        state = GrabState.Ready;
        cooldown = grabCooldown;
        return true;
    }

    /// <summary>Stunned / killed / executed: let go of everything.</summary>
    public void ForceRelease()
    {
        if (state == GrabState.Windup && windup != null) { StopCoroutine(windup); }
        windup = null;
        if (state == GrabState.Holding) { ReleasePlayer(Vector3.zero); }
        RestoreColor();
        state = GrabState.Ready;
        cooldown = grabCooldown;
    }

    private IEnumerator Windup()
    {
        state = GrabState.Windup;
        IncomingAttackIndicator.Warn(transform, windupTime); // red warning if he's behind the player
        if (SoundManager.instance != null) { SoundManager.instance.HumanSound_Attack(transform.position); }

        // Exposed: glow so the player knows to kick
        for (float t = 0f; t < windupTime; t += Time.deltaTime)
        {
            if (body != null) { body.color = Color.Lerp(baseColor, openingColor, Mathf.PingPong(t * 7f, 1f)); }
            FacePlayer();
            yield return null;
        }
        RestoreColor();
        windup = null;

        PlayerController player = PlayerController.instance;
        bool canGrab = player != null && !player.isHeld && !player.isExecuting && player.CharacterHealthComponent.CurrentHP > 0f
                       && FlatDistance(player.transform.position) <= grabRange + 0.3f && (owner == null || !owner.isStunned);
        if (!canGrab)
        {
            // Missed: short recovery
            state = GrabState.Ready;
            cooldown = grabCooldown * 0.5f;
            yield break;
        }

        // Grab!
        Vector3 toPlayer = player.transform.position - transform.position;
        toPlayer.y = 0f;
        holdDirection = toPlayer.sqrMagnitude > 0.01f ? toPlayer.normalized : transform.forward;
        state = GrabState.Holding;
        struggle = 0f;
        tickTimer = 0f;
        ticks = 0;
        player.BeginHeld(this);
        GrabStruggleUI.Show();
        if (SoundManager.instance != null) { SoundManager.instance.KickSound_Human(player.transform.position); }
        if (RumbleManager.instance != null) { RumbleManager.instance.RumblePulse(1f, 2f, 0.3f); }
    }

    private void Update()
    {
        if (cooldown > 0f) { cooldown -= Time.deltaTime; }
        if (state != GrabState.Holding) { return; }

        PlayerController player = PlayerController.instance;
        if (player == null || player.CharacterHealthComponent.CurrentHP <= 0f || owner == null || owner.IsDead)
        {
            ForceRelease();
            return;
        }

        // Crushing hug
        tickTimer += Time.deltaTime;
        if (tickTimer >= damageTick)
        {
            tickTimer -= damageTick;
            ticks++;
            player.TakeGrabDamage(hugDamagePerSecond * damageTick, ticks % 3 == 1);
        }

        // Struggle: mash Jump
        if (InputSchemeManager.instance != null && InputSchemeManager.instance.isJumpPressed)
        {
            struggle += strugglePerPress;
            if (RumbleManager.instance != null) { RumbleManager.instance.RumblePulse(0.3f, 0.6f, 0.05f); }
        }
        struggle = Mathf.Clamp01(struggle - struggleDecay * Time.deltaTime);
        GrabStruggleUI.SetProgress(struggle);

        if (struggle >= 1f) { BreakFree(); }
    }

    private void BreakFree()
    {
        ReleasePlayer(holdDirection * releasePush);
        state = GrabState.Ready;
        cooldown = grabCooldown;
        if (SoundManager.instance != null) { SoundManager.instance.KickSound_Human(transform.position); }
    }

    private void ReleasePlayer(Vector3 push)
    {
        GrabStruggleUI.Hide();
        PlayerController player = PlayerController.instance;
        if (player != null) { player.EndHeld(push); }
    }

    private void OnDisable()
    {
        if (state == GrabState.Holding) { ReleasePlayer(Vector3.zero); }
        state = GrabState.Ready;
    }

    private void FacePlayer()
    {
        PlayerController player = PlayerController.instance;
        if (player == null) { return; }
        Vector3 d = player.transform.position - transform.position;
        d.y = 0f;
        if (d.sqrMagnitude > 0.01f) { transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(d), Time.deltaTime * 8f); }
    }

    private void RestoreColor()
    {
        if (body != null) { body.color = baseColor; }
    }

    private float FlatDistance(Vector3 p)
    {
        Vector3 d = p - transform.position;
        d.y = 0f;
        return d.magnitude;
    }
}
