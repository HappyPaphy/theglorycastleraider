using DG.Tweening;
using TMPro;
using UnityEngine;

/// <summary>
/// The altar in the middle of a Challenge Room. Shows the challenge; press Interact while facing it,
/// then press again to start (the gates close and the waves begin).
/// </summary>
public class ChallengeAltar : MonoBehaviour
{
    [SerializeField] private SpriteRenderer iconRenderer;
    [SerializeField] private SpriteRenderer glowRenderer;
    [SerializeField] private TextMeshPro label;
    [SerializeField] private GameObject uiButtonPrompt;
    [SerializeField] private LayerMask playerLayer = 1 << 6;

    private ChallengeRoom room;
    private ChallengeSettings settings;
    private float armedUntil;
    private Vector3 iconBase;

    private void Awake()
    {
        SharpWorldText.Attach(label); // full-resolution text (the world renders at low resolution)
        settings = ChallengeSettings.Load();
        if (settings != null)
        {
            if (iconRenderer != null) { iconRenderer.transform.localPosition = new Vector3(0f, settings.iconHeight, 0f); }
            if (glowRenderer != null) { glowRenderer.transform.localPosition = new Vector3(0f, settings.iconHeight, 0f); }
            if (label != null) { label.transform.localPosition = new Vector3(0f, settings.labelHeight, 0f); }
        }
        if (iconRenderer != null) { iconBase = iconRenderer.transform.localPosition; }
        if (uiButtonPrompt != null) { uiButtonPrompt.SetActive(false); }
    }

    public void Setup(ChallengeRoom challengeRoom)
    {
        room = challengeRoom;
        ChallengeDefinition c = room.Challenge;
        if (iconRenderer != null && c.icon != null) { iconRenderer.sprite = c.icon; }
        if (glowRenderer != null) { Color g = c.color; g.a = 0.55f; glowRenderer.color = g; }
        RefreshLabel(false);
    }

    public void OnCleared()
    {
        if (uiButtonPrompt != null) { uiButtonPrompt.SetActive(false); }
        if (glowRenderer != null) { glowRenderer.DOFade(0f, 1f).SetLink(gameObject); }
        if (label != null) { label.text = $"<b>{room.Challenge.displayName}</b>\n<color=#9BE37B>Cleared</color>"; }
    }

    private void Update()
    {
        if (iconRenderer != null)
        {
            iconRenderer.transform.localPosition = iconBase + Vector3.up * (Mathf.Sin(Time.time * 2f) * 0.06f);
            FaceCamera(iconRenderer.transform);
        }
        if (glowRenderer != null) { FaceCamera(glowRenderer.transform); }
        if (label != null) { FaceCamera(label.transform); }

        if (room == null || settings == null) { return; }

        if (room.CurrentState == ChallengeRoom.State.Running)
        {
            if (uiButtonPrompt != null && uiButtonPrompt.activeSelf) { uiButtonPrompt.SetActive(false); }
            if (label != null) { label.text = $"<b>{room.Challenge.displayName}</b>\nWave {room.CurrentWave} / {room.Challenge.waves.Count}"; }
            return;
        }
        if (room.CurrentState != ChallengeRoom.State.Ready) { return; }

        Vector3 center = transform.position + Vector3.up;
        bool near = IsGameplayActive() &&
                    PhysicsQuery.Any(center, settings.interactRadius, playerLayer, QueryTriggerInteraction.Collide) &&
                    InteractFacing.IsPlayerFacing(center);

        if (uiButtonPrompt != null && uiButtonPrompt.activeSelf != near) { uiButtonPrompt.SetActive(near); }
        if (!near) { armedUntil = 0f; }
        RefreshLabel(near);

        if (near && InputSchemeManager.instance != null && InputSchemeManager.instance.IsInteractPressed)
        {
            InputSchemeManager.instance.IsInteractPressed = false;

            if (Time.time >= armedUntil)
            {
                armedUntil = Time.time + settings.confirmWindow;
                if (iconRenderer != null) { iconRenderer.transform.DOPunchScale(Vector3.one * 0.25f, 0.25f, 6).SetLink(gameObject); }
            }
            else
            {
                armedUntil = 0f;
                if (uiButtonPrompt != null) { uiButtonPrompt.SetActive(false); }
                room.Begin();
            }
        }
    }

    private void RefreshLabel(bool near)
    {
        if (label == null || room == null) { return; }
        ChallengeDefinition c = room.Challenge;
        string head = $"<b><color=#{ColorUtility.ToHtmlStringRGB(c.color)}>{c.displayName}</color></b>";
        if (!near) { label.text = head; return; }

        string flavour = string.IsNullOrEmpty(c.flavour) ? "" : $"\n<size=70%><i>{c.flavour}</i></size>";
        string confirm = Time.time < armedUntil ? "\n<color=#FFD45A>Press again to begin - the doors will lock!</color>" : "\n<size=75%>Press to choose</size>";
        label.text = head + flavour + "\n<size=85%>" + c.BuildDescription() + "</size>" + confirm;
    }

    private static void FaceCamera(Transform t)
    {
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
