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
            isSprinting = playerController.isSprintHeld;
        }

        bool isMoving = moveValue.sqrMagnitude > 0.01f;

        // 2. Only bob if grounded AND actively moving
        if (controller.isGrounded && isMoving)
        {
            float currentFreq = isSprinting ? sprintFrequency : walkFrequency;
            float currentAmp = isSprinting ? sprintAmplitude : walkAmplitude;

            timer += Time.deltaTime * currentFreq;

            float xOffset = Mathf.Cos(timer / 2) * currentAmp;
            float yOffset = Mathf.Sin(timer) * currentAmp;

            currentBobOffset = new Vector3(xOffset, yOffset, 0);
        }
        else
        {
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
}