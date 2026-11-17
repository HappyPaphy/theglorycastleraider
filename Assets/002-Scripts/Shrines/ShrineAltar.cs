using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;

/// <summary>
/// One shrine altar. Shows its deal floating above it; press Interact while facing it to choose the deal,
/// then press again (within a few seconds) to accept. Each altar works once.
/// Its ShrineDefinition is set by the ShrineRoom (or can be assigned by hand).
/// </summary>
public class ShrineAltar : MonoBehaviour
{
    [SerializeField] private ShrineDefinition definition;

    [Header("Parts")]
    [Tooltip("Floating icon above the altar (bobs and turns).")]
    [SerializeField] private SpriteRenderer iconRenderer;
    [Tooltip("Soft glow behind the icon, tinted with the shrine colour.")]
    [SerializeField] private SpriteRenderer glowRenderer;
    [SerializeField] private TextMeshPro label;
    [SerializeField] private GameObject uiButtonPrompt;
    [SerializeField] private LayerMask playerLayer = 1 << 6;

    private ShrineSettings settings;
    private bool used;
    private float armedUntil;
    private Vector3 iconBasePos;
    private float bobPhase;

    public ShrineDefinition Definition => definition;
    public bool IsUsed => used;

    private void Awake()
    {
        settings = ShrineSettings.Load();

        // Heights come from ShrineSettings so they can be tuned in one place
        if (settings != null)
        {
            if (iconRenderer != null) { iconRenderer.transform.localPosition = new Vector3(0f, settings.iconHeight, 0f); }
            if (glowRenderer != null) { glowRenderer.transform.localPosition = new Vector3(0f, settings.iconHeight, 0f); }
            if (label != null) { label.transform.localPosition = new Vector3(0f, settings.labelHeight, 0f); }
        }
        if (iconRenderer != null) { iconBasePos = iconRenderer.transform.localPosition; }
        if (uiButtonPrompt != null) { uiButtonPrompt.SetActive(false); }
        bobPhase = Random.Range(0f, Mathf.PI * 2f);
        ApplyDefinition();
    }

    public void SetDefinition(ShrineDefinition shrine)
    {
        definition = shrine;
        ApplyDefinition();
    }

    private void ApplyDefinition()
    {
        bool has = definition != null;
        if (iconRenderer != null)
        {
            iconRenderer.sprite = has ? definition.icon : null;
            iconRenderer.enabled = has && definition.icon != null;
        }
        if (glowRenderer != null)
        {
            glowRenderer.enabled = has;
            if (has) { Color c = definition.color; c.a = 0.55f; glowRenderer.color = c; }
        }
        RefreshLabel(false);
    }

    // =========================================================
    // UPDATE
    // =========================================================

    private void Update()
    {
        AnimateIcon();
        FaceCamera(label != null ? label.transform : null);

        if (used || definition == null || settings == null) { return; }

        Vector3 center = transform.position + Vector3.up * 1f;
        bool near = IsGameplayActive() &&
                    PhysicsQuery.Any(center, settings.interactRadius, playerLayer, QueryTriggerInteraction.Collide) &&
                    InteractFacing.IsPlayerFacing(center);

        if (uiButtonPrompt != null && uiButtonPrompt.activeSelf != near) { uiButtonPrompt.SetActive(near); }

        bool armed = Time.time < armedUntil;
        if (!near && armed) { armedUntil = 0f; armed = false; }
        RefreshLabel(near);

        if (near && InputSchemeManager.instance != null && InputSchemeManager.instance.IsInteractPressed)
        {
            InputSchemeManager.instance.IsInteractPressed = false;

            if (!armed)
            {
                armedUntil = Time.time + settings.confirmWindow;
                if (iconRenderer != null) { iconRenderer.transform.DOPunchScale(Vector3.one * 0.25f, 0.25f, 6).SetLink(gameObject); }
                RefreshLabel(true);
            }
            else
            {
                armedUntil = 0f;
                TryAccept();
            }
        }
    }

    // =========================================================
    // THE DEAL
    // =========================================================

    private void TryAccept()
    {
        PlayerController player = PlayerController.instance;
        if (player == null) { return; }

        // Can the player pay?
        if (definition.coinCost > 0 && (player.coinWallet == null || player.coinWallet.Coins < definition.coinCost))
        {
            Refuse("Not enough coins");
            return;
        }

        // Pay
        if (definition.coinCost > 0) { player.coinWallet.TrySpend(definition.coinCost); }

        if (definition.maxHealthCost > 0f)
        {
            float max = player.CharacterHealthComponent.MaxHP * (1f - definition.maxHealthCost);
            player.CharacterHealthComponent.SetMaxHP(Mathf.Max(1f, max));
            if (player.CharacterHealthComponent.CurrentHP > max) { player.CharacterHealthComponent.SetHP(max); }
        }

        if (definition.maxStaminaCost > 0f)
        {
            float max = player.CharacterStaminaComponent.MaxStamina * (1f - definition.maxStaminaCost);
            player.CharacterStaminaComponent.SetMaxStamina(Mathf.Max(1f, max));
            if (player.CharacterStaminaComponent.CurrentStamina > max) { player.CharacterStaminaComponent.SetStamina(max); }
        }

        var newCurses = new List<CurseDefinition>();
        if (definition.curseCount > 0) { newCurses = PlayerCurses.Instance.AddRandomCurses(definition.curseCount, settings.cursePool); }

        // Gain (cleanse first, so a deal can't remove the curse it just gave)
        if (definition.cleanseCurses && newCurses.Count == 0 && PlayerCurses.HasInstance) { PlayerCurses.Instance.ClearAll(); }

        if (definition.maxHealthBonus > 0f)
        {
            float extra = player.CharacterHealthComponent.MaxHP * definition.maxHealthBonus;
            player.CharacterHealthComponent.SetMaxHP(player.CharacterHealthComponent.MaxHP + extra);
            player.CharacterHealthComponent.Heal(extra);
        }

        if (definition.healFraction > 0f)
        {
            player.CharacterHealthComponent.Heal(player.CharacterHealthComponent.MaxHP * definition.healFraction);
        }

        if (definition.damageBonus > 0f) { PlayerUpgrades.Instance.AddDamageBonus(definition.damageBonus); }

        EnergyOrbDefinition orb = settings.GetOrb(definition.energyOrb);
        if (orb != null && settings.energyOrbPrefab != null)
        {
            EnergyOrbPickup pickup = Instantiate(settings.energyOrbPrefab, transform.position + transform.forward * -1.1f + Vector3.up * 0.9f, Quaternion.identity);
            pickup.Initialize(orb);
        }

        string result = string.Empty;
        if (definition.IsGamble)
        {
            float multiplier = RollGamble();
            int payout = Mathf.RoundToInt(definition.coinCost * multiplier);
            if (payout > 0) { CoinPickup.Drop(transform.position + Vector3.up * 1.3f - transform.forward * 0.4f, payout, 8); }
            result = payout > 0 ? $"x{multiplier:0.#}!" : "Nothing...";
        }

        if (newCurses.Count > 0)
        {
            var names = new List<string>();
            foreach (CurseDefinition c in newCurses) { names.Add($"<color=#{ColorUtility.ToHtmlStringRGB(c.color)}>{c.displayName}</color>"); }
            result = (result.Length > 0 ? result + "\n" : "") + "Cursed: " + string.Join(", ", names);
        }

        Spend(result);
    }

    private float RollGamble()
    {
        float total = 0f;
        foreach (ShrineDefinition.GambleOutcome o in definition.gambleOutcomes) { if (o != null) total += o.weight; }
        if (total <= 0f) { return 0f; }

        float roll = Random.value * total;
        foreach (ShrineDefinition.GambleOutcome o in definition.gambleOutcomes)
        {
            if (o == null) { continue; }
            roll -= o.weight;
            if (roll <= 0f) { return o.payoutMultiplier; }
        }
        return 0f;
    }

    private void Refuse(string reason)
    {
        transform.DOKill(true);
        transform.DOShakePosition(0.3f, new Vector3(0.05f, 0f, 0.05f), 20).SetLink(gameObject);
        if (label != null)
        {
            label.text = $"<b>{definition.displayName}</b>\n<color=#E36B5B>{reason}</color>";
        }
        if (SoundManager.instance != null) { SoundManager.instance.DoorSound_Impact(transform.position); }
    }

    // The altar goes dark after its one use
    private void Spend(string result)
    {
        used = true;
        if (uiButtonPrompt != null) { uiButtonPrompt.SetActive(false); }

        if (iconRenderer != null)
        {
            iconRenderer.transform.DOPunchScale(Vector3.one * 0.5f, 0.4f, 6).SetLink(gameObject);
            iconRenderer.DOColor(settings.spentColor, 0.6f).SetDelay(0.3f).SetLink(gameObject);
        }
        if (glowRenderer != null) { glowRenderer.DOFade(0f, 0.8f).SetLink(gameObject); }

        if (label != null)
        {
            label.text = $"<b>{definition.displayName}</b>" + (string.IsNullOrEmpty(result) ? "" : "\n" + result);
            label.DOFade(0.35f, 1.5f).SetDelay(2.5f).SetLink(gameObject);
        }

        PlayerController player = PlayerController.instance;
        if (player != null && player.cameraBob != null) { player.cameraBob.TriggerShake(0.25f, 0.08f); }
        if (RumbleManager.instance != null) { RumbleManager.instance.RumblePulse(0.6f, 1.2f, 0.25f); }
    }

    // =========================================================
    // LOOK
    // =========================================================

    private void RefreshLabel(bool near)
    {
        if (label == null || used) { return; }
        if (definition == null) { label.text = string.Empty; return; }

        string head = $"<b><color=#{ColorUtility.ToHtmlStringRGB(definition.color)}>{definition.displayName}</color></b>";
        if (!near)
        {
            label.text = head; // just the name from afar
            return;
        }

        string flavour = string.IsNullOrEmpty(definition.flavour) ? "" : $"\n<size=70%><i>{definition.flavour}</i></size>";
        string confirm = Time.time < armedUntil ? "\n<color=#FFD45A>Press again to accept</color>" : "\n<size=75%>Press to choose</size>";
        label.text = head + flavour + "\n<size=85%>" + definition.BuildOfferText() + "</size>" + confirm;
    }

    private void AnimateIcon()
    {
        if (iconRenderer == null) { return; }
        Transform t = iconRenderer.transform;
        t.localPosition = iconBasePos + Vector3.up * (Mathf.Sin(Time.time * 2f + bobPhase) * 0.06f);
        FaceCamera(t);
        if (glowRenderer != null) { FaceCamera(glowRenderer.transform); }
    }

    private static void FaceCamera(Transform t)
    {
        if (t == null) { return; }
        Camera cam = Camera.main;
        if (cam != null) { t.rotation = Quaternion.LookRotation(t.position - cam.transform.position); }
    }

    private static bool IsGameplayActive()
    {
        if (PauseGame.instance != null && PauseGame.instance.IsPaused) { return false; }
        if (PerkSelectionManager.instance != null && PerkSelectionManager.instance.isPanelActive) { return false; }
        if (TutorialManager.instance != null && TutorialManager.instance.isPanelActive) { return false; }
        if (ShopUI.IsOpen) { return false; }
        return true;
    }
}
