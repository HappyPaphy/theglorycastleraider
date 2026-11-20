using System.Collections;
using UnityEngine;

/// <summary>
/// The Castle Lord's second phase, on top of the Big Knight's fighting.
/// Below <see cref="phase2HealthFraction"/> of its HP it enrages (faster moves, animations and attack timings)
/// and every few seconds slams the ground: a ring expands from it and hits you if you're standing on the
/// ground when it passes. Jump over it, or be out of its reach.
/// </summary>
[RequireComponent(typeof(EnemyBigKnight))]
public class CastleLordPhases : MonoBehaviour
{
    [Header("Phase 2")]
    [Range(0.05f, 0.95f)] public float phase2HealthFraction = 0.5f;
    [Tooltip("Speed of everything once enraged (1.3 = 30% faster).")]
    [Min(1f)] public float enrageSpeed = 1.3f;
    public Color enrageTint = new Color(1f, 0.55f, 0.45f);

    [Header("Shockwave Slam")]
    [Tooltip("Seconds between slams (counted only while he is free to slam).")]
    [Min(1f)] public float slamInterval = 7f;
    [Tooltip("Wind-up before the slam: he glows red and the warning shows.")]
    [Min(0.1f)] public float slamWindup = 0.9f;
    public float slamDamage = 22f;
    [Tooltip("How fast the ring grows (metres per second).")]
    public float ringSpeed = 8f;
    public float ringMaxRadius = 11f;
    [Tooltip("How thick the ring's hit band is (metres).")]
    public float ringHitWidth = 0.7f;
    public Material ringMaterial;
    public Color ringColor = new Color(1f, 0.4f, 0.15f, 1f);

    private EnemyBigKnight boss;
    private SpriteRenderer body;
    private Color baseTint = Color.white;
    private bool phase2;
    private bool slamming;
    private float slamTimer;

    private void Awake()
    {
        boss = GetComponent<EnemyBigKnight>();
    }

    private void Start()
    {
        body = boss.BodySprite;
        if (body != null) { baseTint = body.color; }
        slamTimer = slamInterval * 0.5f; // first slam comes a little sooner
    }

    private void Update()
    {
        if (boss == null || boss.IsDead) { return; }

        HealthComponent hp = boss.CharacterHealthComponent;
        if (!phase2 && hp.MaxHP > 0f && hp.CurrentHP > 0f && hp.CurrentHP <= hp.MaxHP * phase2HealthFraction)
        {
            EnterPhase2();
        }

        if (!phase2 || slamming || !boss.IsPlayerDetected) { return; }

        if (boss.CanUseSpecial) { slamTimer -= Time.deltaTime; }
        if (slamTimer <= 0f && boss.CanUseSpecial)
        {
            slamTimer = slamInterval;
            StartCoroutine(Slam());
        }
    }

    private void EnterPhase2()
    {
        phase2 = true;
        boss.bonusSpeedMultiplier = enrageSpeed;
        baseTint = enrageTint;
        if (body != null) { body.color = enrageTint; }

        if (SoundManager.instance != null) { SoundManager.instance.DoorSound_Impact(transform.position); }
        if (RumbleManager.instance != null) { RumbleManager.instance.RumblePulse(1f, 3f, 0.5f); }
        if (ObjectPoolingManager.instance != null) { ObjectPoolingManager.instance.SpawnObject("Spark", transform.position + Vector3.up * 1.5f); }
    }

    private IEnumerator Slam()
    {
        slamming = true;

        // Wind-up: glow red
        IncomingAttackIndicator.Warn(transform, slamWindup);
        for (float t = 0f; t < slamWindup; t += Time.deltaTime)
        {
            if (boss.IsDead) { slamming = false; yield break; }
            if (body != null) { body.color = Color.Lerp(baseTint, Color.red, Mathf.PingPong(t * 8f, 1f)); }
            yield return null;
        }
        if (body != null) { body.color = baseTint; }

        // Impact
        Vector3 origin = transform.position;
        if (SoundManager.instance != null)
        {
            SoundManager.instance.DoorSound_Impact(origin);
            SoundManager.instance.KickSound_Human(origin);
        }
        if (RumbleManager.instance != null) { RumbleManager.instance.RumblePulse(1f, 3f, 0.3f); }

        // Expanding ring
        LineRenderer ring = MakeRing(origin);
        bool hitPlayer = false;
        for (float radius = 0.5f; radius <= ringMaxRadius; radius += ringSpeed * Time.deltaTime)
        {
            SetRing(ring, origin, radius, 1f - radius / ringMaxRadius);

            PlayerController player = PlayerController.instance;
            if (!hitPlayer && player != null)
            {
                Vector3 toPlayer = player.transform.position - origin;
                toPlayer.y = 0f;
                if (Mathf.Abs(toPlayer.magnitude - radius) <= ringHitWidth && player.isGrounded)
                {
                    hitPlayer = true;
                    player.TakeUnblockableHit(slamDamage, origin);
                }
            }
            yield return null;
        }
        Destroy(ring.gameObject);

        slamming = false;
    }

    private LineRenderer MakeRing(Vector3 origin)
    {
        var go = new GameObject("Shockwave");
        go.transform.position = origin;
        LineRenderer line = go.AddComponent<LineRenderer>();
        line.loop = true;
        line.useWorldSpace = true;
        line.positionCount = 64;
        line.widthMultiplier = 0.3f;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        if (ringMaterial != null) { line.sharedMaterial = ringMaterial; }
        return line;
    }

    private void SetRing(LineRenderer line, Vector3 origin, float radius, float alpha)
    {
        Color c = ringColor;
        c.a = Mathf.Clamp01(alpha);
        line.startColor = c;
        line.endColor = c;
        for (int i = 0; i < line.positionCount; i++)
        {
            float a = i * Mathf.PI * 2f / line.positionCount;
            line.SetPosition(i, origin + new Vector3(Mathf.Cos(a) * radius, 0.12f, Mathf.Sin(a) * radius));
        }
    }
}
