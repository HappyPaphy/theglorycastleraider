using DG.Tweening;
using UnityEngine;

/// <summary>
/// World pickup for an Energy Orb. Interact opens the perk selection for its rarity.
/// The rolled perk offers are stored on the orb, so cancelling and interacting again shows the
/// same cards until the player shuffles. Choosing a card consumes the orb.
/// </summary>
public class EnergyOrbPickup : MonoBehaviour
{
    [Header("Orb")]
    [Tooltip("Which rarity this orb is. Set automatically when dropped from a box.")]
    [SerializeField] private EnergyOrbDefinition orbDefinition;

    [Header("Interaction")]
    [SerializeField] private float pickupRadius = 1.5f;
    [SerializeField] private LayerMask playerLayer = 1 << 6;
    [SerializeField] private GameObject uiButtonPrompt;

    [Header("Visual")]
    [Tooltip("Spins and bobs. Shrinks away when the orb is consumed.")]
    [SerializeField] private Transform visualRoot;
    [Tooltip("Particle systems tinted with the tier color.")]
    [SerializeField] private ParticleSystem[] tintedParticles;
    [SerializeField] private Light orbLight;
    [SerializeField] private float rotationSpeed = 90f;
    [SerializeField] private float hoverSpeed = 2f;
    [SerializeField] private float hoverHeight = 0.14f;

    [Header("Tier Colors")]
    [SerializeField] private Color commonColor = new Color(0.20f, 0.60f, 0.20f);
    [SerializeField] private Color rareColor = new Color(0.20f, 0.80f, 0.90f);
    [SerializeField] private Color uniqueColor = new Color(0.85f, 0.75f, 0.15f);

    [Header("Consume (DOTween)")]
    [SerializeField] private float consumeDuration = 0.4f;
    [Tooltip("Optional effect spawned when the orb is consumed.")]
    [SerializeField] private GameObject consumeEffectPrefab;
    [SerializeField] private AudioClip consumeSound;

    private PerkOffer[] savedOffers;
    private bool isConsumed;
    private Vector3 visualStartLocalPos;

    public EnergyOrbDefinition Definition => orbDefinition;
    public bool HasSavedOffers => savedOffers != null;

    private void Awake()
    {
        if (visualRoot != null) { visualStartLocalPos = visualRoot.localPosition; }
        if (uiButtonPrompt != null) { uiButtonPrompt.SetActive(false); }
        ApplyTierColor();
    }

    /// <summary>Used by drops to set the rarity after spawning.</summary>
    public void Initialize(EnergyOrbDefinition definition)
    {
        orbDefinition = definition;
        savedOffers = null;
        ApplyTierColor();
    }

    private void Update()
    {
        if (isConsumed) { return; }

        AnimateOrb();
        HandleInteract();
    }

    private void HandleInteract()
    {
        PerkSelectionManager perkManager = PerkSelectionManager.instance;

        bool canInteract = orbDefinition != null && perkManager != null && !perkManager.isPanelActive;
        bool isPlayerNear = canInteract && Physics.OverlapSphere(transform.position, pickupRadius, playerLayer).Length > 0;

        if (uiButtonPrompt != null && uiButtonPrompt.activeSelf != isPlayerNear)
        {
            uiButtonPrompt.SetActive(isPlayerNear);
        }

        if (isPlayerNear && InputSchemeManager.instance != null && InputSchemeManager.instance.IsInteractPressed)
        {
            InputSchemeManager.instance.IsInteractPressed = false;
            perkManager.Open(this);
        }
    }

    // =========================================================
    // SAVED OFFERS
    // =========================================================

    /// <summary>Copy of the offers rolled for this orb, or null if it hasn't been opened yet.</summary>
    public PerkOffer[] GetSavedOffers()
    {
        return savedOffers != null ? (PerkOffer[])savedOffers.Clone() : null;
    }

    public void SaveOffers(PerkOffer[] offers)
    {
        savedOffers = offers != null ? (PerkOffer[])offers.Clone() : null;
    }

    // =========================================================
    // CONSUME
    // =========================================================

    /// <summary>Called by PerkSelectionManager once a card from this orb is taken.</summary>
    public void Consume()
    {
        if (isConsumed) { return; }
        isConsumed = true;

        if (uiButtonPrompt != null) { uiButtonPrompt.SetActive(false); }

        if (consumeEffectPrefab != null)
        {
            Instantiate(consumeEffectPrefab, transform.position, Quaternion.identity);
        }

        if (consumeSound != null)
        {
            AudioSource.PlayClipAtPoint(consumeSound, transform.position);
        }

        // Let existing particles fade out instead of popping
        foreach (ParticleSystem ps in tintedParticles)
        {
            if (ps != null) { ps.Stop(true, ParticleSystemStopBehavior.StopEmitting); }
        }

        // Unscaled so it still plays while the perk UI is closing at timeScale 0
        Sequence seq = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);

        if (visualRoot != null)
        {
            seq.Join(visualRoot.DOScale(Vector3.zero, consumeDuration).SetEase(Ease.InBack));
            seq.Join(visualRoot.DOLocalMoveY(visualRoot.localPosition.y + 0.5f, consumeDuration).SetEase(Ease.OutQuad));
        }

        if (orbLight != null)
        {
            seq.Join(orbLight.DOIntensity(0f, consumeDuration));
        }

        seq.AppendInterval(1f);
        seq.OnComplete(() => Destroy(gameObject));
    }

    // =========================================================
    // VISUALS
    // =========================================================

    private void AnimateOrb()
    {
        if (visualRoot == null) { return; }

        visualRoot.Rotate(Vector3.up * rotationSpeed * Time.deltaTime, Space.World);

        Vector3 pos = visualStartLocalPos;
        pos.y += Mathf.Sin(Time.time * hoverSpeed) * hoverHeight;
        visualRoot.localPosition = pos;
    }

    private void ApplyTierColor()
    {
        if (orbDefinition == null) { return; }

        Color color = GetTierColor(orbDefinition.Tier);

        if (tintedParticles != null)
        {
            foreach (ParticleSystem ps in tintedParticles)
            {
                if (ps == null) { continue; }

                ParticleSystem.MainModule main = ps.main;
                main.startColor = color;

                // Restart so already-spawned particles don't keep the old color
                if (Application.isPlaying)
                {
                    ps.Clear(true);
                    ps.Play(true);
                }
            }
        }

        if (orbLight != null) { orbLight.color = color; }
    }

    private Color GetTierColor(OrbTier tier)
    {
        switch (tier)
        {
            case OrbTier.Common: return commonColor;
            case OrbTier.Rare: return rareColor;
            case OrbTier.Unique: return uniqueColor;
            default: return Color.white;
        }
    }

    private void OnValidate()
    {
        ApplyTierColor();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, pickupRadius);
    }
}
