using UnityEngine;

/// <summary>
/// Shared "is the player looking at it" check for everything you can Interact with
/// (items, weapons, energy orbs, doors, the shopkeeper, treasure chests).
/// Uses the camera's horizontal direction, so looking up or down doesn't matter, only turning towards it.
/// </summary>
public static class InteractFacing
{
    /// <summary>Half-angle of the view cone, in degrees. 45 = the target must be within 45° left or right of centre.</summary>
    public const float DefaultMaxAngle = 45f;

    /// <summary>Closer than this (horizontally) always counts as facing: standing on top of it makes the angle meaningless.</summary>
    private const float AlwaysFacingDistance = 0.35f;

    private static Camera cachedCamera;

    public static bool IsPlayerFacing(Vector3 targetPoint, float maxAngle = DefaultMaxAngle)
    {
        Transform view = GetView();
        if (view == null) { return true; } // no camera yet: don't block interaction

        Vector3 toTarget = targetPoint - view.position;
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude < AlwaysFacingDistance * AlwaysFacingDistance) { return true; }

        Vector3 forward = view.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f) { return true; } // looking straight up/down

        return Vector3.Angle(forward, toTarget) <= maxAngle;
    }

    private static Transform GetView()
    {
        if (cachedCamera == null || !cachedCamera.isActiveAndEnabled) { cachedCamera = Camera.main; }
        return cachedCamera != null ? cachedCamera.transform : null;
    }
}
