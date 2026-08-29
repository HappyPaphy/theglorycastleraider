using UnityEngine;
using UnityEngine.InputSystem;

public class WeaponSway : MonoBehaviour
{
    [SerializeField] private PlayerController playerController;

    [Header("Look Sway Settings")]
    [SerializeField] private float swayMultiplier = 40f; // How much the weapon moves
    [SerializeField] private float maxSway = 200f;      // The absolute limit it can move
    [SerializeField] private float smoothTime = 0.15f; // How fast it catches back up to center

    [Header("Movement Bob Settings")]
    [SerializeField] private float bobFrequency = 12f;  // How fast the hand bobs
    [SerializeField] private float bobAmplitudeX = 15f; // Side-to-side sway distance
    [SerializeField] private float bobAmplitudeY = 10f; // Up-and-down sway distance
    [SerializeField] private float sprintMultiplier = 1.5f; // Speeds up the bob when sprinting

    private Vector3 initialPosition;
    private Vector3 velocity = Vector3.zero;
    private float bobTimer = 0f;

    private Vector3 dynamicOffset = Vector3.zero;
    private float returnSpeed = 5f;

    void Start()
    {
        // Store the starting UI position
        initialPosition = transform.localPosition;
    }

    void Update()
    {
        Vector2 lookDelta = playerController.playerControls.Player.Look.ReadValue<Vector2>();

        float moveX = Mathf.Clamp(-lookDelta.x * swayMultiplier, -maxSway, maxSway);
        float moveY = Mathf.Clamp(-lookDelta.y * swayMultiplier, -maxSway, maxSway);

        Vector3 lookSwayPosition = new Vector3(moveX, moveY, 0);

        // 2. Calculate Movement Bob (Figure-8 pattern)
        Vector3 movementBobPosition = Vector3.zero;

        Vector2 moveValue = playerController.playerControls.Player.Move.ReadValue<Vector2>();
        bool isMoving = moveValue.sqrMagnitude > 0.01f;

        // Read the sprint bool directly from the controller
        bool isSprinting = playerController.isSprintHeld;

        if (isMoving && !playerController.isAttacking && playerController.isGrounded && !playerController.isSliding)
        {
            float currentFrequency = isSprinting ? bobFrequency * sprintMultiplier : bobFrequency;
            bobTimer += Time.deltaTime * currentFrequency;

            float bobX = Mathf.Cos(bobTimer / 2) * bobAmplitudeX;
            float bobY = Mathf.Sin(bobTimer) * bobAmplitudeY;

            movementBobPosition = new Vector3(bobX, Mathf.Abs(bobY) * -1f, 0);
        }
        else
        {
            bobTimer = 0f;
        }

        // 3. Recover dynamic impulses back to zero over time
        dynamicOffset = Vector3.Lerp(dynamicOffset, Vector3.zero, Time.deltaTime * returnSpeed);

        // 4. Combine all offsets with the original position
        Vector3 targetPosition = initialPosition + lookSwayPosition + movementBobPosition + dynamicOffset;

        // 5. Smoothly glide the UI to the final combined target
        transform.localPosition = Vector3.SmoothDamp(transform.localPosition, targetPosition, ref velocity, smoothTime);
    }

    // --- PUBLIC TRIGGERS FOR PLAYER MOVEMENT ---

    public void TriggerJumpImpulse()
    {
        // Push the weapon down sharply when jumping, then let it bounce back
        dynamicOffset += new Vector3(0f, -800f, 0f);
    }

    public void TriggerSlideImpulse()
    {
        // Slam the weapon down and slightly to the side when initiating a slide
        dynamicOffset += new Vector3(0f, 800f, 0f);
    }
}