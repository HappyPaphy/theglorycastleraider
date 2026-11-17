using UnityEngine;

/// <summary>
/// Non-allocating overlap checks for code that runs every frame (interactables, enemy detection...).
/// Physics.OverlapSphere allocates a new array on every call; these reuse one shared buffer instead.
/// The buffer is only valid until the next query, so read it right away (don't keep it).
/// </summary>
public static class PhysicsQuery
{
    public static readonly Collider[] Buffer = new Collider[64];

    /// <summary>Fills <see cref="Buffer"/> and returns how many colliders were found.</summary>
    public static int Overlap(Vector3 center, float radius, int layerMask,
        QueryTriggerInteraction triggers = QueryTriggerInteraction.UseGlobal)
    {
        return Physics.OverlapSphereNonAlloc(center, radius, Buffer, layerMask, triggers);
    }

    /// <summary>Is anything on these layers inside the sphere?</summary>
    public static bool Any(Vector3 center, float radius, int layerMask,
        QueryTriggerInteraction triggers = QueryTriggerInteraction.UseGlobal)
    {
        return Overlap(center, radius, layerMask, triggers) > 0;
    }

    /// <summary>The first collider inside the sphere, or null.</summary>
    public static Collider First(Vector3 center, float radius, int layerMask,
        QueryTriggerInteraction triggers = QueryTriggerInteraction.UseGlobal)
    {
        return Overlap(center, radius, layerMask, triggers) > 0 ? Buffer[0] : null;
    }
}
