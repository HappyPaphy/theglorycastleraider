using DG.Tweening;
using TMPro;
using UnityEngine;

/// <summary>
/// A coin dropped by an enemy. Bursts out of the body, bounces to the floor, then flies to the player when
/// they come close and adds its value to the CoinWallet (no button press needed).
/// Spawn with <see cref="Drop"/>. The prefab lives at Resources/Pickups/CoinPickup (swap its sprite freely).
/// </summary>
public class CoinPickup : MonoBehaviour
{
    private const string PrefabPath = "Pickups/CoinPickup";

    [Header("Visual")]
    [Tooltip("The coin sprite. Billboarded towards the camera and spun.")]
    [SerializeField] private Transform visual;
    [SerializeField] private float spinSpeed = 540f;
    [SerializeField] private float bobHeight = 0.05f;

    [Header("Burst (when dropped)")]
    [SerializeField] private float burstSpeed = 2.4f;
    [SerializeField] private float burstUpSpeed = 3.2f;
    [SerializeField] private float gravity = 14f;
    [SerializeField] private float bounce = 0.35f;
    [SerializeField] private float restHeight = 0.18f;
    [SerializeField] private LayerMask groundLayers = (1 << 0) | (1 << 8);

    [Header("Collect")]
    [Tooltip("Coins can't be collected for this long after dropping (so the burst is visible).")]
    [SerializeField] private float collectDelay = 0.45f;
    [Tooltip("Player within this distance pulls the coin in.")]
    [SerializeField] private float magnetRadius = 3.5f;
    [SerializeField] private float magnetAcceleration = 30f;
    [SerializeField] private float collectDistance = 0.55f;
    [SerializeField] private AudioClip collectSound;
    [SerializeField] [Range(0f, 1f)] private float collectVolume = 0.6f;
    [Tooltip("Where the '+N' text appears above the coin, and how far it rises.")]
    [SerializeField] private float textHeight = 0.1f;
    [SerializeField] private float textRise = 0.3f;

    [Header("Lifetime")]
    [Tooltip("Uncollected coins disappear after this many seconds (0 = never).")]
    [SerializeField] private float lifetime = 90f;

    private int value = 1;
    private Vector3 velocity;
    private bool grounded;
    private bool collected;
    private float age;
    private float magnetSpeed;
    private float spinAngle;
    private float bobPhase;

    private static CoinPickup prefab;
    private static bool prefabMissingLogged;

    // =========================================================
    // SPAWNING
    // =========================================================

    /// <summary>
    /// Drops <paramref name="totalValue"/> coins at <paramref name="position"/>, split over up to
    /// <paramref name="maxPieces"/> pickups (so big rewards look like a shower, not one giant coin).
    /// </summary>
    public static void Drop(Vector3 position, int totalValue, int maxPieces = 6)
    {
        if (totalValue <= 0) { return; }

        if (prefab == null) { prefab = Resources.Load<CoinPickup>(PrefabPath); }
        if (prefab == null)
        {
            if (!prefabMissingLogged) { Debug.LogWarning("[CoinPickup] Missing prefab at Resources/" + PrefabPath + ". Coins go straight to the wallet."); prefabMissingLogged = true; }
            AddToWallet(totalValue);
            return;
        }

        int pieces = Mathf.Clamp(totalValue, 1, Mathf.Max(1, maxPieces));
        int baseValue = totalValue / pieces;
        int remainder = totalValue % pieces;

        for (int i = 0; i < pieces; i++)
        {
            CoinPickup coin = Instantiate(prefab, position, Quaternion.identity);
            coin.value = baseValue + (i < remainder ? 1 : 0);
            coin.Launch(i, pieces);
        }
    }

    private void Launch(int index, int count)
    {
        // Spread evenly around a circle with a bit of randomness
        float angle = (index / (float)count) * 360f + Random.Range(-25f, 25f);
        Vector3 dir = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
        velocity = dir * burstSpeed * Random.Range(0.6f, 1.1f) + Vector3.up * burstUpSpeed * Random.Range(0.85f, 1.15f);
        spinAngle = Random.Range(0f, 360f);
        bobPhase = Random.Range(0f, Mathf.PI * 2f);

        transform.localScale = Vector3.one * 0.2f;
        transform.DOScale(1f, 0.2f).SetEase(Ease.OutBack).SetLink(gameObject);
    }

    // =========================================================
    // UPDATE
    // =========================================================

    private void Update()
    {
        if (collected) { return; }

        float dt = Time.deltaTime;
        age += dt;

        if (lifetime > 0f && age > lifetime)
        {
            collected = true;
            transform.DOScale(0f, 0.25f).SetLink(gameObject).OnComplete(() => Destroy(gameObject));
            return;
        }

        PlayerController player = PlayerController.instance;
        bool canCollect = age >= collectDelay && player != null && player.CharacterHealthComponent.CurrentHP > 0f;
        Vector3 target = player != null ? player.transform.position + Vector3.up * 0.9f : transform.position;
        float distance = Vector3.Distance(transform.position, target);

        if (canCollect && distance <= collectDistance)
        {
            Collect();
            return;
        }

        if (canCollect && distance <= magnetRadius)
        {
            // Fly to the player, faster and faster
            magnetSpeed += magnetAcceleration * dt;
            transform.position = Vector3.MoveTowards(transform.position, target, magnetSpeed * dt);
            grounded = false;
        }
        else
        {
            magnetSpeed = 0f;
            Fall(dt);
        }

        AnimateVisual(dt);
    }

    // Simple ballistic fall with a bounce, resting a little above the floor
    private void Fall(float dt)
    {
        if (grounded)
        {
            return;
        }

        velocity.y -= gravity * dt;
        Vector3 next = transform.position + velocity * dt;

        // Stop sideways at walls
        Vector3 flat = new Vector3(velocity.x, 0f, velocity.z) * dt;
        if (flat.sqrMagnitude > 0f && Physics.Raycast(transform.position, flat.normalized, flat.magnitude + 0.1f, groundLayers, QueryTriggerInteraction.Ignore))
        {
            velocity.x = velocity.z = 0f;
            next = new Vector3(transform.position.x, next.y, transform.position.z);
        }

        // Land on the floor
        if (velocity.y < 0f &&
            Physics.Raycast(next + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, 0.5f + restHeight, groundLayers, QueryTriggerInteraction.Ignore))
        {
            next.y = hit.point.y + restHeight;
            if (-velocity.y > 1.5f)
            {
                velocity.y = -velocity.y * bounce;
                velocity.x *= 0.5f;
                velocity.z *= 0.5f;
            }
            else
            {
                velocity = Vector3.zero;
                grounded = true;
            }
        }

        transform.position = next;
    }

    private void AnimateVisual(float dt)
    {
        if (visual == null) { return; }

        // Face the camera, then spin around the vertical axis (flat coin look)
        Camera cam = Camera.main;
        if (cam != null)
        {
            Vector3 toCam = cam.transform.position - visual.position;
            toCam.y = 0f;
            if (toCam.sqrMagnitude > 0.0001f) { visual.rotation = Quaternion.LookRotation(-toCam.normalized, Vector3.up); }
        }

        spinAngle += spinSpeed * dt;
        Vector3 s = visual.localScale;
        s.x = Mathf.Abs(s.y) * Mathf.Cos(spinAngle * Mathf.Deg2Rad);
        visual.localScale = s;

        if (grounded)
        {
            visual.localPosition = Vector3.up * (Mathf.Sin(age * 4f + bobPhase) * bobHeight);
        }
    }

    // =========================================================
    // COLLECT
    // =========================================================

    private void Collect()
    {
        collected = true;
        AddToWallet(value);

        if (collectSound != null) { AudioSource.PlayClipAtPoint(collectSound, transform.position, collectVolume); }
        SpawnFloatingText($"+{value}", transform.position + Vector3.up * textHeight, textRise);

        transform.DOKill();
        transform.DOScale(0f, 0.12f).SetLink(gameObject).OnComplete(() => Destroy(gameObject));
    }

    private static void AddToWallet(int amount)
    {
        PlayerController player = PlayerController.instance;
        if (player != null && player.coinWallet != null) { player.coinWallet.AddCoins(amount); }
    }

    private static void SpawnFloatingText(string text, Vector3 position, float rise)
    {
        GameObject go = new GameObject("Coin Text");
        go.transform.position = position;
        TextMeshPro tmp = go.AddComponent<TextMeshPro>();
        GameFontSettings.Apply(tmp);
        tmp.text = text;
        tmp.fontSize = 2.2f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(1f, 0.85f, 0.35f);
        tmp.rectTransform.sizeDelta = new Vector2(1.5f, 0.4f);

        Camera cam = Camera.main;
        if (cam != null) { go.transform.rotation = Quaternion.LookRotation(position - cam.transform.position); }

        go.transform.DOMoveY(position.y + rise, 0.6f).SetEase(Ease.OutCubic).SetLink(go);
        tmp.DOFade(0f, 0.3f).SetDelay(0.3f).SetLink(go).OnComplete(() => Destroy(go));
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.85f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, magnetRadius);
    }
}
