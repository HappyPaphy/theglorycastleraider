using UnityEngine;

public class PooledFragment : MonoBehaviour
{
    private MeshFilter meshFilter;
    private Collider meshCollider;
    [HideInInspector] public int spawnSessionId = 0;

    private void Awake()
    {
        meshFilter = GetComponent<MeshFilter>();
        meshCollider = GetComponent<Collider>();
    }

    private void OnDisable()
    {
        // CRITICAL: Destroy the dynamically generated slice mesh when returning to the pool
        // to prevent massive memory leaks.
        if (meshFilter != null && meshFilter.sharedMesh != null)
        {
            DestroyImmediate(meshFilter.sharedMesh, true);
            meshFilter.sharedMesh = null;
        }
    }
}