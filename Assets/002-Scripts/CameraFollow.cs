using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("Tracking Target")]
    [Tooltip("Drag the CameraPos empty GameObject here")]
    public Transform cameraPos;

    [Header("Smoothing Settings")]
    [Tooltip("Lower numbers mean faster snapping. 0.05 to 0.15 is ideal.")]
    public float normalSmoothTime = 0.08f;
    [HideInInspector] public bool isSmoothing = true;

    private float yVelocity = 0.0f;

    void LateUpdate()
    {
        if (cameraPos == null) return;
        if (PauseGame.instance.IsPaused) return;

        float smoothTime = normalSmoothTime;

        if (!isSmoothing)
        {
            smoothTime = 0.0f;
        }

        // 1. Match X and Z exactly
        float targetX = cameraPos.position.x;
        float targetZ = cameraPos.position.z;

        // 2. Smoothly damp the Y axis
        float smoothY = Mathf.SmoothDamp(transform.position.y, cameraPos.position.y, ref yVelocity, smoothTime);

        // 3. Apply the new position
        transform.position = new Vector3(targetX, smoothY, targetZ);

        // 4. Match the rotation exactly
        transform.rotation = cameraPos.rotation;
    }
}
