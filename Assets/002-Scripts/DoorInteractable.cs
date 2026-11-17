using DG.Tweening;
using UnityEngine;

/// <summary>
/// Lets the player open/close a door with Interact. The door swings around its hinge, always away from
/// the player, using DOTween. It can still be broken (Fracture) like before.
/// Also keeps DunGen's Door.IsOpen in sync so room culling knows whether you can see through it.
/// </summary>
public class DoorInteractable : MonoBehaviour
{
    [Header("Interaction")]
    [SerializeField] private float interactRadius = 1.6f;
    [SerializeField] private LayerMask playerLayer = 1 << 6;
    [SerializeField] private GameObject uiButtonPrompt;
    [Tooltip("How far the prompt sits in front of the door, on the player's side.")]
    [SerializeField] private float promptOffset = 0.35f;

    [Header("Hinge (local space of the door)")]
    [Tooltip("Point on the hinge edge. The door mesh spans x -0.52..0.50, so -0.5 is its left edge.")]
    [SerializeField] private Vector3 hingeLocalPosition = new Vector3(-0.5f, 0f, -0.09f);

    [Header("Swing (DOTween)")]
    [SerializeField] private float openAngle = 95f;
    [SerializeField] private float openDuration = 0.22f;
    [SerializeField] private float closeDuration = 0.18f;
    [Tooltip("In-eases speed up into the stop, so the door slams instead of easing to rest.")]
    [SerializeField] private Ease openEase = Ease.InQuad;
    [SerializeField] private Ease closeEase = Ease.InQuad;

    [Header("Impact (when the door hits its stop)")]
    [Tooltip("Degrees the door bounces back off the stop after slamming open.")]
    [SerializeField] private float openReboundAngle = 9f;
    [Tooltip("Degrees the door bounces back off the frame after slamming shut.")]
    [SerializeField] private float closeReboundAngle = 4f;
    [SerializeField] private float reboundDuration = 0.16f;
    [SerializeField] private float impactShakeDuration = 0.12f;
    [SerializeField] private float impactShakeMagnitude = 0.05f;
    [Tooltip("Camera shake only happens if the player is within this distance.")]
    [SerializeField] private float impactShakeRadius = 6f;

    [Header("Closing")]
    [Tooltip("Characters that stop the door from closing on them.")]
    [SerializeField] private LayerMask blockingLayers = (1 << 6) | (1 << 7);

    [Header("Lock (optional, e.g. treasure rooms)")]
    [Tooltip("A locked door only opens by spending a key of the required type.")]
    [SerializeField] private bool isLocked;
    [SerializeField] private TreasureKeyType requiredKey = TreasureKeyType.Bronze;
    [Tooltip("Padlocks etc. They drop off when the door is unlocked.")]
    [SerializeField] private GameObject[] lockVisuals;
    [Tooltip("Shows 'Requires Gold Key' / 'Unlock' while the player is near a locked door.")]
    [SerializeField] private TMPro.TMP_Text lockLabel;
    [SerializeField] private Color canUnlockColor = new Color(1f, 0.85f, 0.35f);
    [SerializeField] private Color missingKeyColor = new Color(0.9f, 0.35f, 0.3f);
    [Tooltip("How far above the button prompt the lock label floats.")]
    [SerializeField] private float lockLabelHeight = 0.2f;

    [Header("Hold Progress Bar (gamepad: hold North to use doors)")]
    [Tooltip("Width of the bar compared to the prompt icon.")]
    [SerializeField] private float holdBarWidth = 0.9f;
    [Tooltip("Height of the bar compared to the prompt icon.")]
    [SerializeField] private float holdBarHeight = 0.12f;
    [Tooltip("Gap between the bottom of the prompt icon and the bar, compared to the icon height.")]
    [SerializeField] private float holdBarGap = 0.08f;
    [SerializeField] private Color holdBarBackColor = new Color(0f, 0f, 0f, 0.65f);
    [SerializeField] private Color holdBarFillColor = new Color(1f, 0.85f, 0.35f, 1f);

    private DunGen.Door dunGenDoor;
    private GameObject holdBar;
    private Transform holdBarFill;
    private float holdBarFullWidth;
    private static Sprite whiteSprite;
    private Rigidbody rb;
    private Collider doorCollider;

    private Vector3 closedLocalPosition;
    private Quaternion closedLocalRotation;
    private bool isOpen;
    private bool isMoving;
    private float currentAngle;
    private Tween swingTween;

    public bool IsOpen => isOpen;
    public bool IsLocked => isLocked;
    public TreasureKeyType RequiredKey => requiredKey;

    private void Awake()
    {
        dunGenDoor = GetComponent<DunGen.Door>();
        rb = GetComponent<Rigidbody>();
        doorCollider = GetComponent<Collider>();

        // The door is moved by script, so physics must not fight it (it was fully frozen anyway)
        if (rb != null) { rb.isKinematic = true; }

        closedLocalPosition = transform.localPosition;
        closedLocalRotation = transform.localRotation;

        if (uiButtonPrompt != null) { uiButtonPrompt.SetActive(false); }
        if (lockLabel != null) { lockLabel.gameObject.SetActive(false); }

        BuildHoldBar();
    }

    private void Start()
    {
        // DunGen places doors after Awake, so capture the real closed pose here
        closedLocalPosition = transform.localPosition;
        closedLocalRotation = transform.localRotation;
        SetDunGenOpen(false);
    }

    private void OnDisable()
    {
        swingTween?.Kill();
        isMoving = false;
        if (uiButtonPrompt != null) { uiButtonPrompt.SetActive(false); }
        if (lockLabel != null) { lockLabel.gameObject.SetActive(false); }

        // Fracture disables the door when it breaks: a broken door is an open doorway for DunGen culling.
        // (Checked via health so rooms being deactivated for other reasons don't count as "broken".)
        Fracture fracture = GetComponent<Fracture>();
        if (fracture != null && fracture.currentHealth <= 0f) { SetDunGenOpen(true); }
    }

    private void Update()
    {
        Transform player = FindPlayerInRange();
        bool canInteract = player != null && !isMoving && IsGameplayActive() && InteractFacing.IsPlayerFacing(DoorwayCenter);

        if (uiButtonPrompt != null)
        {
            if (uiButtonPrompt.activeSelf != canInteract) { uiButtonPrompt.SetActive(canInteract); }
            if (canInteract) { PlacePrompt(player.position); }
        }

        UpdateLockLabel(canInteract && isLocked);
        UpdateHoldBar(canInteract);

        if (canInteract && InputSchemeManager.instance != null && InputSchemeManager.instance.IsDoorInteractPressed)
        {
            InputSchemeManager.instance.IsDoorInteractPressed = false;

            if (isLocked) { TryUnlock(player.position); }
            else if (isOpen) { TryClose(); }
            else { Open(player.position); }
        }
    }

    // =========================================================
    // LOCK
    // =========================================================

    /// <summary>Spends a key of the required type and swings the door open. Rattles if the player has none.</summary>
    public bool TryUnlock(Vector3 openerPosition)
    {
        if (!isLocked || isMoving) { return false; }

        if (!PlayerUpgrades.Instance.TryUseKey(requiredKey))
        {
            // No key: rattle the door and flash the label
            transform.DOKill(true);
            transform.DOPunchRotation(Vector3.up * 2.5f, 0.3f, 14).SetLink(gameObject);
            if (lockLabel != null)
            {
                lockLabel.transform.DOKill(true);
                lockLabel.transform.DOPunchScale(Vector3.one * 0.25f, 0.3f, 8).SetLink(lockLabel.gameObject);
            }
            if (SoundManager.instance != null) { SoundManager.instance.DoorSound_Impact(SoundPosition); }
            return false;
        }

        isLocked = false;
        UpdateLockLabel(false);
        DropLockVisuals();
        if (RumbleManager.instance != null) { RumbleManager.instance.RumblePulse(0.4f, 0.8f, 0.15f); }

        // Let the padlock fall first, then swing open
        isMoving = true;
        DOVirtual.DelayedCall(0.3f, () =>
        {
            isMoving = false;
            Open(openerPosition);
        }).SetLink(gameObject);
        return true;
    }

    private void DropLockVisuals()
    {
        if (lockVisuals == null) { return; }

        foreach (GameObject lockVisual in lockVisuals)
        {
            if (lockVisual == null) { continue; }

            Transform t = lockVisual.transform;
            t.SetParent(null, true); // stay put while the door swings away
            t.DOPunchRotation(new Vector3(0f, 0f, 25f), 0.2f, 8);
            t.DOMoveY(t.position.y - 0.9f, 0.45f).SetDelay(0.15f).SetEase(Ease.InQuad);
            t.DOScale(0f, 0.2f).SetDelay(0.55f).OnComplete(() => Destroy(lockVisual));
        }
    }

    // =========================================================
    // HOLD PROGRESS BAR
    // =========================================================

    // A thin bar under the button prompt: two SpriteRenderers (background + left-anchored fill) parented to the
    // prompt, so they follow its position, billboard rotation and scale. Built at runtime: no prefab setup needed.
    private void BuildHoldBar()
    {
        if (uiButtonPrompt == null || holdBar != null) { return; }

        SpriteRenderer promptRenderer = uiButtonPrompt.GetComponent<SpriteRenderer>();
        if (promptRenderer == null || promptRenderer.sprite == null) { return; }

        if (whiteSprite == null)
        {
            whiteSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 4f, 4f), new Vector2(0f, 0.5f), 4f);
            whiteSprite.name = "DoorHoldBar_White";
        }

        Bounds icon = promptRenderer.sprite.bounds; // prompt-local units
        float width = icon.size.x * holdBarWidth;
        float height = icon.size.y * holdBarHeight;
        float y = icon.min.y - icon.size.y * holdBarGap - height * 0.5f;

        holdBar = new GameObject("Hold Bar");
        holdBar.layer = uiButtonPrompt.layer;
        holdBar.transform.SetParent(uiButtonPrompt.transform, false);
        holdBar.transform.localPosition = new Vector3(icon.center.x - width * 0.5f, y, 0f);

        SpriteRenderer back = CreateBarPart("Back", holdBar.transform, promptRenderer, holdBarBackColor, 1);
        back.transform.localScale = new Vector3(width, height, 1f);

        // Fill sits 1px-ish inside the background and slightly in front of it
        float inset = height * 0.18f;
        SpriteRenderer fill = CreateBarPart("Fill", holdBar.transform, promptRenderer, holdBarFillColor, 2);
        fill.transform.localPosition = new Vector3(inset, 0f, -0.001f);
        holdBarFill = fill.transform;
        holdBarFullWidth = width - inset * 2f;
        holdBarFill.localScale = new Vector3(0f, height - inset * 2f, 1f);

        holdBar.SetActive(false);
    }

    private static SpriteRenderer CreateBarPart(string name, Transform parent, SpriteRenderer promptRenderer, Color color, int orderOffset)
    {
        GameObject go = new GameObject(name);
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = whiteSprite;
        sr.color = color;
        // Same material / sorting as the prompt so it draws the same way (e.g. in front of the scene)
        sr.sharedMaterial = promptRenderer.sharedMaterial;
        sr.sortingLayerID = promptRenderer.sortingLayerID;
        sr.sortingOrder = promptRenderer.sortingOrder + orderOffset;
        return sr;
    }

    private void UpdateHoldBar(bool canInteract)
    {
        if (holdBar == null) { return; }

        float progress = canInteract && InputSchemeManager.instance != null ? InputSchemeManager.instance.DoorHoldProgress : 0f;
        bool show = progress > 0f;

        if (holdBar.activeSelf != show) { holdBar.SetActive(show); }
        if (!show) { return; }

        Vector3 s = holdBarFill.localScale;
        s.x = holdBarFullWidth * progress;
        holdBarFill.localScale = s;
    }

    private void UpdateLockLabel(bool show)
    {
        if (lockLabel == null) { return; }

        if (lockLabel.gameObject.activeSelf != show) { lockLabel.gameObject.SetActive(show); }
        if (!show) { return; }

        int keys = PlayerUpgrades.Instance.GetKeyCount(requiredKey);
        bool hasKey = keys > 0;
        lockLabel.text = hasKey ? $"Unlock ({requiredKey} Key x{keys})" : $"Requires {requiredKey} Key";
        lockLabel.color = hasKey ? canUnlockColor : missingKeyColor;

        // Hover above the prompt and face the camera (TMP is readable when its forward points away from the viewer)
        if (uiButtonPrompt != null) { lockLabel.transform.position = uiButtonPrompt.transform.position + Vector3.up * lockLabelHeight; }
        Camera cam = Camera.main;
        if (cam != null) { lockLabel.transform.rotation = Quaternion.LookRotation(lockLabel.transform.position - cam.transform.position); }
    }

    // =========================================================
    // OPEN / CLOSE
    // =========================================================

    public void Open(Vector3 openerPosition)
    {
        if (isOpen || isMoving) { return; }

        float target = ChooseSwingAngle(openerPosition);
        isOpen = true;
        SetDunGenOpen(true);
        if (SoundManager.instance != null) { SoundManager.instance.DoorSound_Open(SoundPosition); }
        // Bounce back toward closed after hitting the open stop
        float rebound = target - Mathf.Sign(target) * openReboundAngle;
        SwingTo(target, openDuration, openEase, rebound);
    }

    public bool TryClose()
    {
        if (!isOpen || isMoving) { return false; }

        if (IsDoorwayBlocked())
        {
            // Someone is standing in the doorway: give a small nudge instead of closing through them
            transform.DOPunchRotation(Vector3.up * 3f, 0.25f, 8).SetLink(gameObject);
            return false;
        }

        isOpen = false;
        // Bounce back a little off the frame, on the side it came from
        float rebound = Mathf.Sign(currentAngle) * closeReboundAngle;
        SwingTo(0f, closeDuration, closeEase, rebound, () => SetDunGenOpen(false));
        return true;
    }

    // Slam into targetAngle, then bounce out to reboundAngle and settle back
    private void SwingTo(float targetAngle, float duration, Ease ease, float reboundAngle, TweenCallback onDone = null)
    {
        swingTween?.Kill();
        isMoving = true;
        bool closing = Mathf.Approximately(targetAngle, 0f);

        float half = reboundDuration * 0.5f;
        Sequence seq = DOTween.Sequence().SetLink(gameObject);
        seq.Append(DOVirtual.Float(currentAngle, targetAngle, duration, ApplyAngle).SetEase(ease));
        seq.AppendCallback(() => OnDoorImpact(closing));
        seq.Append(DOVirtual.Float(targetAngle, reboundAngle, half, ApplyAngle).SetEase(Ease.OutQuad));
        seq.Append(DOVirtual.Float(reboundAngle, targetAngle, half, ApplyAngle).SetEase(Ease.InQuad));

        swingTween = seq
            .OnComplete(() =>
            {
                isMoving = false;
                onDone?.Invoke();
            });
    }

    // Rotate the closed pose around the hinge axis (the door's local up through the hinge point)
    private void ApplyAngle(float angle)
    {
        currentAngle = angle;

        Transform parent = transform.parent;
        Quaternion closedWorldRot = parent != null ? parent.rotation * closedLocalRotation : closedLocalRotation;
        Vector3 closedWorldPos = parent != null ? parent.TransformPoint(closedLocalPosition) : closedLocalPosition;

        Vector3 hingeWorld = closedWorldPos + closedWorldRot * Vector3.Scale(hingeLocalPosition, transform.lossyScale);
        Vector3 axis = closedWorldRot * Vector3.up;
        Quaternion swing = Quaternion.AngleAxis(angle, axis);

        Vector3 worldPos = hingeWorld + swing * (closedWorldPos - hingeWorld);
        Quaternion worldRot = swing * closedWorldRot;

        if (rb != null && rb.isKinematic && Application.isPlaying)
        {
            rb.MovePosition(worldPos);
            rb.MoveRotation(worldRot);
        }
        transform.SetPositionAndRotation(worldPos, worldRot);
    }

    // Swing away from whoever opens it
    private float ChooseSwingAngle(Vector3 openerPosition)
    {
        Transform parent = transform.parent;
        Quaternion closedWorldRot = parent != null ? parent.rotation * closedLocalRotation : closedLocalRotation;
        Vector3 closedWorldPos = parent != null ? parent.TransformPoint(closedLocalPosition) : closedLocalPosition;
        Vector3 hingeWorld = closedWorldPos + closedWorldRot * Vector3.Scale(hingeLocalPosition, transform.lossyScale);
        Vector3 axis = closedWorldRot * Vector3.up;

        Vector3 doorCenter = closedWorldPos + Vector3.up * 0.9f;
        Vector3 toOpener = openerPosition - closedWorldPos;
        toOpener.y = 0f;

        Vector3 swungPositive = hingeWorld + Quaternion.AngleAxis(openAngle, axis) * (doorCenter - hingeWorld);
        Vector3 swingDir = swungPositive - doorCenter;
        swingDir.y = 0f;

        return Vector3.Dot(swingDir, toOpener) > 0f ? -openAngle : openAngle;
    }

    private bool IsDoorwayBlocked()
    {
        if (doorCollider == null) { return false; }

        Transform parent = transform.parent;
        Quaternion closedWorldRot = parent != null ? parent.rotation * closedLocalRotation : closedLocalRotation;
        Vector3 closedWorldPos = parent != null ? parent.TransformPoint(closedLocalPosition) : closedLocalPosition;

        // Box where the closed door would be, slightly thicker so we don't trap anyone
        Vector3 center = closedWorldPos + closedWorldRot * new Vector3(0f, 0.9f, -0.09f);
        Vector3 halfExtents = new Vector3(0.5f, 0.85f, 0.3f);

        foreach (Collider c in Physics.OverlapBox(center, halfExtents, closedWorldRot, blockingLayers, QueryTriggerInteraction.Ignore))
        {
            if (c != doorCollider && !c.transform.IsChildOf(transform)) { return true; }
        }
        return false;
    }

    // =========================================================
    // HELPERS
    // =========================================================

    // Middle of the doorway (where the door is when closed), so you look at the same spot to open and to close it
    private Vector3 DoorwayCenter
    {
        get
        {
            Transform parent = transform.parent;
            Vector3 closedWorldPos = parent != null ? parent.TransformPoint(closedLocalPosition) : closedLocalPosition;
            return closedWorldPos + Vector3.up * 0.9f;
        }
    }

    private Transform FindPlayerInRange()
    {
        Collider hit = PhysicsQuery.First(transform.position + Vector3.up * 0.9f, interactRadius, playerLayer, QueryTriggerInteraction.Collide);
        return hit != null ? hit.transform : null;
    }

    private static bool IsGameplayActive()
    {
        if (PauseGame.instance != null && PauseGame.instance.IsPaused) { return false; }
        if (PerkSelectionManager.instance != null && PerkSelectionManager.instance.isPanelActive) { return false; }
        if (TutorialManager.instance != null && TutorialManager.instance.isPanelActive) { return false; }
        if (ShopUI.IsOpen) { return false; }
        return true;
    }

    // Keep the prompt at handle height on the player's side of the (closed or open) door
    private void PlacePrompt(Vector3 playerPosition)
    {
        Vector3 local = transform.InverseTransformPoint(playerPosition);
        float side = local.z >= hingeLocalPosition.z ? 1f : -1f;
        uiButtonPrompt.transform.localPosition = new Vector3(0f, 1.15f, hingeLocalPosition.z + side * promptOffset);
    }

    private void SetDunGenOpen(bool open)
    {
        if (dunGenDoor != null) { dunGenDoor.IsOpen = open; }
    }

    // The moment the door hits its stop: slam sound + a small camera kick if the player is near
    private void OnDoorImpact(bool closing)
    {
        if (SoundManager.instance != null)
        {
            if (closing) { SoundManager.instance.DoorSound_Close(SoundPosition); }
            SoundManager.instance.DoorSound_Impact(SoundPosition);
        }

        PlayerController player = PlayerController.instance;
        if (player != null && player.cameraBob != null &&
            (player.transform.position - transform.position).sqrMagnitude <= impactShakeRadius * impactShakeRadius)
        {
            player.cameraBob.TriggerShake(impactShakeDuration, impactShakeMagnitude);
        }
    }

    // Middle of the door at handle height
    private Vector3 SoundPosition => transform.position + Vector3.up * 0.9f;

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.9f, interactRadius);

        Gizmos.color = Color.red;
        Vector3 hinge = transform.TransformPoint(hingeLocalPosition);
        Gizmos.DrawLine(hinge, hinge + transform.up * 1.8f);
    }
}
