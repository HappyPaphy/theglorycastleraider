using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class CameraBob : MonoBehaviour
{
    [Header("Bob Settings")]
    [SerializeField] private float walkFrequency = 12f;
    [SerializeField] private float walkAmplitude = 0.05f;
    [SerializeField] private float sprintFrequency = 18f;
    [SerializeField] private float sprintAmplitude = 0.1f;

    [SerializeField] private float stepIntervalMultiplier = 1.5f; // Increase this to make steps slower (e.g., 1.5 or 2.0)
    private float stepTimer = 0f;

    [SerializeField] private float defaultFOV = 75f;
    private Coroutine zoomCoroutine;

    private float currentShakeDuration = 0f;
    private float currentShakeMagnitude = 0f;

    [Header("References")]
    [SerializeField] private PlayerController playerController;
    [SerializeField] private Camera[] allCameras;
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private CharacterController controller;

    private float timer = 0f;

    private Vector3 attackOffset = Vector3.zero;
    private Vector3 currentBobOffset = Vector3.zero;

    private void Awake()
    {
        allCameras[0].fieldOfView = defaultFOV;
    }

    void Update()
    {
        if (PauseGame.instance.IsPaused || EquipmentLoadOut.instance.isPanelActive || playerController.CharacterHealthComponent.CurrentHP <= 0) return;

        HandleMovementBob();
        RecoverFromAttack();
        ApplyFinalCameraPosition();
    }

    private void HandleMovementBob()
    {
        // 1. Check if any movement keys are actively pressed
        Vector2 moveValue = Vector2.zero;
        bool isSprinting = false;

        // Ensure playerController is assigned and active before reading
        if (playerController != null && playerController.playerControls != null)
        {
            moveValue = playerController.playerControls.Player.Move.ReadValue<Vector2>();

            // Only count as sprinting if the player actually has stamina remaining
            isSprinting = playerController.isSprintHeld && playerController.CharacterStaminaComponent.CurrentStamina > 0f;
        }

        bool isMoving = moveValue.sqrMagnitude > 0.01f;

        // 2. Only bob and play steps if grounded, actively moving, and NOT sliding
        if (controller.isGrounded && isMoving && !playerController.isSliding)
        {
            float currentFreq = isSprinting ? sprintFrequency : walkFrequency;
            float currentAmp = isSprinting ? sprintAmplitude : walkAmplitude;

            float previousTimer = timer;
            timer += Time.deltaTime * currentFreq;

            stepTimer += Time.deltaTime * currentFreq * (1f / stepIntervalMultiplier);
            float previousStepTimer = stepTimer - (Time.deltaTime * currentFreq * (1f / stepIntervalMultiplier));

            // 3. FOOTSTEP SYNC: Trigger footstep every time the timer crosses a multiple of PI (half a sine wave cycle)
            if (Mathf.FloorToInt(stepTimer / Mathf.PI) > Mathf.FloorToInt(previousStepTimer / Mathf.PI))
            {
                if (SoundManager.instance != null)
                {
                    // Detect the ground type right before playing the sound
                    SurfaceType currentSurface = DetermineSurfaceType();

                    // Play the sound at the camera's location
                    SoundManager.instance.PlayFootStep(cameraTransform.position, currentSurface);
                }
            }

            float xOffset = Mathf.Cos(timer / 2) * currentAmp;
            float yOffset = Mathf.Sin(timer) * currentAmp;

            currentBobOffset = new Vector3(xOffset, yOffset, 0);
        }
        else
        {
            // Reset timer so the first step plays immediately when moving again
            timer = 0f;
            currentBobOffset = Vector3.Lerp(currentBobOffset, Vector3.zero, Time.deltaTime * 10f);
        }
    }

    public void TriggerAttackJolt(float intensity = 0.15f)
    {
        attackOffset = new Vector3(0, -intensity, intensity * 0.5f);
    }

    private void RecoverFromAttack()
    {
        if (attackOffset != Vector3.zero)
        {
            attackOffset = Vector3.Lerp(attackOffset, Vector3.zero, Time.deltaTime * 10f);
        }
    }

    private void ApplyFinalCameraPosition()
    {
        // 1. Start with the default center position
        Vector3 finalPosition = currentBobOffset + attackOffset;

        // 2. Apply Screen Shake ON TOP of the normal movement
        if (currentShakeDuration > 0)
        {
            finalPosition += Random.insideUnitSphere * currentShakeMagnitude;
            currentShakeDuration -= Time.deltaTime;
        }
        else
        {
            currentShakeDuration = 0f;
        }

        // 3. Set the camera position once per frame
        cameraTransform.localPosition = finalPosition + new Vector3(0f, 0.5f, 0f);
    }

    public void TriggerShake(float duration, float magnitude)
    {
        currentShakeDuration = duration;
        currentShakeMagnitude = magnitude;
    }

    public void TriggerZoomEffect(float punchFOV, float inDuration, float outDuration)
    {
        // Stop any active zooms so they don't fight
        if (zoomCoroutine != null)
        {
            StopCoroutine(zoomCoroutine);
        }

        zoomCoroutine = StartCoroutine(ZoomPunchRoutine(punchFOV, inDuration, outDuration));
    }

    private IEnumerator ZoomPunchRoutine(float punchFOV, float inDuration, float outDuration)
    {
        float startFOV = allCameras[0].fieldOfView;
        float elapsed = 0f;

        // --- PHASE 1: ZOOM IN ---
        while (elapsed < inDuration)
        {
            elapsed += Time.deltaTime;

            foreach (Camera cam in allCameras)
            {
                cam.fieldOfView = Mathf.Lerp(startFOV, punchFOV, elapsed / inDuration);
            }

            yield return null;
        }

        foreach (Camera cam in allCameras)
        {
            cam.fieldOfView = punchFOV;
        }

        elapsed = 0f;

        // --- PHASE 2: ZOOM OUT (Return to Normal) ---
        while (elapsed < outDuration)
        {
            elapsed += Time.deltaTime;
            // Notice we lerp back to defaultFOV here!

            foreach (Camera cam in allCameras)
            {
                cam.fieldOfView = Mathf.Lerp(punchFOV, defaultFOV, elapsed / outDuration);
            }

            yield return null;
        }

        foreach (Camera cam in allCameras)
        {
            cam.fieldOfView = defaultFOV;
        }

    }

    private SurfaceType DetermineSurfaceType()
    {
        // Shoot a ray from slightly above the player's feet straight down
        Vector3 rayStart = playerController.transform.position + (Vector3.up * 0.5f);

        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 2f))
        {
            // Check the tag of the floor we hit
            switch (hit.collider.tag)
            {
                case "Wood": return SurfaceType.Wood;
                case "Dirt": return SurfaceType.Dirt;
                case "Water": return SurfaceType.Water;
                case "Brick": return SurfaceType.Brick;
                default: return SurfaceType.Brick; // Default to brick if no specific tag is found
            }
        }

        return SurfaceType.Brick; // Fallback
    }
}