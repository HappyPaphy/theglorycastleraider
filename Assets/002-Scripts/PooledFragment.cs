using UnityEngine;

/// <summary>
/// A pooled fracture piece. Owns the mesh the Fragmenter generated for it and frees it when the piece goes
/// back to the pool (or is destroyed), never earlier.
///
/// The mesh used to be destroyed in OnDisable with DestroyImmediate. That also ran when DunGen hid the room
/// the piece was in, leaving the convex MeshCollider pointing at a destroyed mesh (a PhysX crash risk) and the
/// piece invisible when the room came back. It could even destroy mesh assets (allowDestroyingAssets = true).
/// </summary>
public class PooledFragment : MonoBehaviour
{
    private MeshFilter meshFilter;
    private MeshCollider meshCollider;
    private Mesh ownedMesh;
    [HideInInspector] public int spawnSessionId = 0;

    private void Awake()
    {
        meshFilter = GetComponent<MeshFilter>();
        meshCollider = GetComponent<MeshCollider>();
    }

    /// <summary>Gives this piece a newly generated mesh (any previous one is freed).</summary>
    public void SetMesh(Mesh mesh)
    {
        ReleaseMesh();
        ownedMesh = mesh;
        if (meshFilter != null) { meshFilter.sharedMesh = mesh; }
    }

    /// <summary>
    /// Frees the generated mesh. Unhooks the collider first so PhysX never holds a destroyed mesh,
    /// and uses deferred Destroy so it's safe even during physics callbacks.
    /// </summary>
    public void ReleaseMesh()
    {
        if (meshCollider != null) { meshCollider.sharedMesh = null; }
        if (meshFilter != null) { meshFilter.sharedMesh = null; }

        if (ownedMesh != null)
        {
            Destroy(ownedMesh);
            ownedMesh = null;
        }
    }

    private void OnDestroy()
    {
        if (ownedMesh != null) { Destroy(ownedMesh); }
    }
}
