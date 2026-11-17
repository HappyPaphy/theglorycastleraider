using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;



[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(Rigidbody))]
public class Fracture : MonoBehaviour
{
    [Header("Health Settings")]
    public bool isCanBeDamage = true;
    public float maxHealth = 100f;
    public float currentHealth;
    [Tooltip("How much collision force is converted into damage (e.g. 1 force = 1 damage)")]
    public float collisionDamageMultiplier = 1f;

    [Header("Fracture Settings")]
    public TriggerOptions triggerOptions;
    public FractureOptions fractureOptions;
    public RefractureOptions refractureOptions;
    public CallbackOptions callbackOptions;

    [Header("Explosion Settings")]
    [Tooltip("How hard the fragments fly away when broken")]
    public float fragmentExplosionForce = 15f;
    [Tooltip("The radius of the explosion force")]
    public float fragmentExplosionRadius = 5f;
    public float timeBeforeFragmentObjDestroyed = 0.8f;
    public float timeBeforeFragmentColliderDisable = 4f;

    [Header("Hit Direction Push")]
    [Tooltip("Speed (m/s) fragments get along the attack direction. Uses velocity change, so it's the same for any fragment mass.")]
    public float hitDirectionSpeed = 8f;
    [Tooltip("Extra upward speed so pieces arc instead of sliding along the floor.")]
    public float hitUpwardSpeed = 2f;
    [Tooltip("0 = every piece flies exactly along the hit, 1 = very scattered.")]
    [Range(0f, 1f)] public float hitDirectionSpread = 0.35f;
    [Tooltip("Random spin (rad/s) given to each piece.")]
    public float hitSpin = 16f;

    [Header("Fragment Safety")]
    [Tooltip("Hard cap on a fragment's launch speed (m/s). Very fast pieces can tunnel through thin/hollow walls.")]
    public float maxFragmentSpeed = 15f;
    [Tooltip("How fast PhysX may push a fragment out of something it spawned inside (e.g. furniture touching a wall).")]
    public float maxDepenetrationVelocity = 2f;
    [Tooltip("Seconds after breaking during which pieces ignore the scene (no collision, no gravity) and just fly " +
             "along the hit. Use for objects that sit tightly inside geometry, like a door in its frame. 0 = off.")]
    public float fragmentSceneCollisionDelay = 0f;

    // Direction of the blow that broke this object (zero = unknown, only the explosion force is used)
    private Vector3 lastHitDirection;

    [HideInInspector]
    public int currentRefractureCount = 0;

    private GameObject fragmentRoot;

    // Cached Components to avoid expensive GetComponent calls at runtime
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private Collider cachedCollider;
    private Rigidbody cachedRigidbody;
    private int destructableLayer;

    private void Awake()
    {
        // Cache components once upon initialization
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();
        cachedCollider = GetComponent<Collider>();
        cachedRigidbody = GetComponent<Rigidbody>();

        // Cache the layer integer instead of doing a string lookup every time a fragment is created
        destructableLayer = LayerMask.NameToLayer("Destructed");
    }

    private void Start()
    {
        currentHealth = maxHealth;
    }

    [ContextMenu("Print Mesh Info")]
    public void PrintMeshInfo()
    {
        // Changed to .sharedMesh to prevent Unity from permanently leaking a duplicate mesh instance into memory
        var mesh = meshFilter.sharedMesh;
        if (mesh == null) return;

        Debug.Log("Positions");
        var positions = mesh.vertices;
        var normals = mesh.normals;
        var uvs = mesh.uv;

        for (int i = 0; i < positions.Length; i++)
        {
            Debug.Log($"Vertex {i}\nPOS | X: {positions[i].x} Y: {positions[i].y} Z: {positions[i].z}\nNRM | X: {normals[i].x} Y: {normals[i].y} Z: {normals[i].z} LEN: {normals[i].magnitude}\nUV  | U: {uvs[i].x} V: {uvs[i].y}\n");
        }
    }

    /// <summary>
    /// Damage with the direction of the blow: if this breaks the object, the pieces fly along it.
    /// </summary>
    public void TakeDamage(float damage, Collider instigator, Vector3 hitPoint, Vector3 hitDirection)
    {
        if (currentHealth > 0f && hitDirection.sqrMagnitude > 0.0001f)
        {
            lastHitDirection = hitDirection.normalized;
        }

        TakeDamage(damage, instigator, hitPoint);
    }

    public void TakeDamage(float damage, Collider instigator = null, Vector3 hitPoint = default)
    {
        if (!isCanBeDamage) return;
        if (currentHealth <= 0) return;

        currentHealth -= damage;

        if (currentHealth <= 0)
        {
            currentHealth = 0;
            if (hitPoint == default) hitPoint = transform.position;

            callbackOptions.CallOnFracture(instigator, gameObject, hitPoint);
            ComputeFracture(hitPoint);
        }
    }



    public void CauseFracture()
    {
        TakeDamage(maxHealth, null, transform.position);
    }

    private void OnValidate()
    {
        if (transform.parent != null)
        {
            var scale = transform.parent.localScale;
            if (!Mathf.Approximately(scale.x, scale.y) || !Mathf.Approximately(scale.x, scale.z))
            {
                Debug.LogWarning("Warning: Parent transform of fractured object must be uniformly scaled in all axes or fragments will not render correctly.", transform);
            }
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (triggerOptions.triggerType != TriggerType.Collision || collision.contactCount == 0) return;

        var contact = collision.contacts[0];
        float collisionForce = collision.impulse.magnitude / Time.fixedDeltaTime;

        if (collisionForce > triggerOptions.minimumCollisionForce)
        {
            bool tagAllowed = triggerOptions.IsTagAllowed(contact.otherCollider.gameObject.tag);

            if (!triggerOptions.filterCollisionsByTag || tagAllowed)
            {
                TakeDamage(collisionForce * collisionDamageMultiplier, contact.otherCollider, contact.point);
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (triggerOptions.triggerType != TriggerType.Trigger) return;

        bool tagAllowed = triggerOptions.IsTagAllowed(other.gameObject.tag);
        if (!triggerOptions.filterCollisionsByTag || tagAllowed)
        {
            TakeDamage(maxHealth, other, transform.position);
        }
    }

    private void Update()
    {
        // Early exit avoids checking Input system every frame for non-keyboard objects
        if (triggerOptions.triggerType != TriggerType.Keyboard) return;

        if (Input.GetKeyDown(triggerOptions.triggerKey))
        {
            TakeDamage(maxHealth, null, transform.position);
        }
    }

    public void ComputeFracture(Vector3 impactPoint)
    {
        if (meshFilter.sharedMesh == null) return;

        if (fragmentRoot == null)
        {
            fragmentRoot = new GameObject($"{name}_Fragments");
            fragmentRoot.transform.SetParent(transform.parent);
            fragmentRoot.transform.SetPositionAndRotation(transform.position, transform.rotation);
            fragmentRoot.transform.localScale = Vector3.one;
        }

        var fragmentTemplate = CreateFragmentTemplate();

        // The object's centre is always on the room side of any wall it touches: overlapping pieces are moved toward it
        Vector3 safeCenter = meshRenderer != null ? meshRenderer.bounds.center : transform.position;

        System.Action onFractureComplete = () =>
        {
            if (fragmentTemplate != null)
            {
                Destroy(fragmentTemplate);
            }

            gameObject.SetActive(false);
            ApplyForceToFragments(impactPoint, safeCenter);

            if (currentRefractureCount == 0 || refractureOptions.invokeCallbacks)
            {
                if (callbackOptions.onCompleted != null)
                {
                    callbackOptions.onCompleted.Invoke();
                }
            }
        };

        if (fractureOptions.asynchronous)
        {
            StartCoroutine(Fragmenter.FractureAsync(
                gameObject, fractureOptions, fragmentTemplate, fragmentRoot.transform, onFractureComplete));
        }
        else
        {
            Fragmenter.Fracture(gameObject, fractureOptions, fragmentTemplate, fragmentRoot.transform);
            onFractureComplete();
        }
    }

    private void ApplyForceToFragments(Vector3 hitPosition, Vector3 safeCenter)
    {
        if (fragmentRoot == null) return;

        // CRITICAL: Force PhysX to update world positions of pooled objects immediately 
        // before applying explosion forces in the same frame.
        Physics.SyncTransforms();

        foreach (Transform fragment in fragmentRoot.transform)
        {
            // Safety check: destroy degenerate or empty fragments instantly
            var filter = fragment.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null || filter.sharedMesh.vertexCount == 0)
            {
                // Pooled pieces go back to the pool (destroying them would shrink the pool and leak their mesh)
                if (fragment.GetComponent<PooledFragment>() != null && ObjectPoolingManager.instance != null)
                {
                    ObjectPoolingManager.instance.ReturnObject("Fragment", fragment.gameObject);
                }
                else
                {
                    Destroy(fragment.gameObject);
                }
                continue;
            }

            if (fragment.gameObject.TryGetComponent<MeshCollider>(out var fragMesh))
            {
                fragMesh.convex = true;
                fragMesh.isTrigger = false;
            }

            if (fragment.gameObject.TryGetComponent<Rigidbody>(out var fragRb))
            {
                fragRb.isKinematic = false;
                fragRb.useGravity = true;

                // Walls/floors are hollow MeshColliders: with Discrete detection a small, fast piece can end up
                // between their faces and pass straight through. Continuous sweeps prevent that.
                fragRb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                fragRb.interpolation = RigidbodyInterpolation.Interpolate;

                fragRb.WakeUp(); // Ensure the rigidbody isn't sleeping from pool reuse

                // Clear any leftover velocity from its previous lifecycle
                fragRb.linearVelocity = Vector3.zero;
                fragRb.angularVelocity = Vector3.zero;

                bool delayCollision = fragmentSceneCollisionDelay > 0f && ObjectPoolingManager.instance != null;

                if (delayCollision)
                {
                    // Tight fits (door in its frame): fly clear of the geometry first, collide afterwards
                    if (fragMesh != null) { fragMesh.enabled = false; }
                    fragRb.useGravity = false;
                }
                else if (fragMesh != null)
                {
                    // Pieces of furniture that touch a wall spawn partly inside it. PhysX would push them out
                    // through whichever face is nearest - often the far side of a hollow wall - so do it ourselves first.
                    PushOutOfStaticGeometry(fragment, fragMesh, safeCenter);
                }

                // Gentle pop-out if the piece spawned overlapping a wall, instead of being fired through it
                fragRb.maxDepenetrationVelocity = maxDepenetrationVelocity;

                // VelocityChange (not Impulse): with Impulse the speed is force / mass, so tiny slivers of a 1 kg
                // object were launched at 100+ m/s and shot through walls. Now every piece gets the same burst.
                fragRb.AddExplosionForce(fragmentExplosionForce, hitPosition, fragmentExplosionRadius, 0.5f, ForceMode.VelocityChange);

                ApplyHitDirectionPush(fragRb, hitPosition);

                // Cap the final speed so nothing outruns the collision checks
                if (fragRb.linearVelocity.sqrMagnitude > maxFragmentSpeed * maxFragmentSpeed)
                {
                    fragRb.linearVelocity = fragRb.linearVelocity.normalized * maxFragmentSpeed;
                }

                if (delayCollision)
                {
                    // This object is already disabled, so run the timer on the (always active) pool manager
                    ObjectPoolingManager.instance.StartCoroutine(
                        EnableSceneCollisionAfterDelay(fragment.gameObject, fragMesh, fragRb, fragmentSceneCollisionDelay));
                }
            }

            ObjectPoolingManager.instance.ReturnObjectDelayed("Fragment", fragment.gameObject, timeBeforeFragmentObjDestroyed, timeBeforeFragmentColliderDisable);
        }
    }

    private static System.Collections.IEnumerator EnableSceneCollisionAfterDelay(GameObject fragment, Collider fragCollider, Rigidbody fragRb, float delay)
    {
        yield return new WaitForSeconds(delay);

        // The piece may already be back in the pool
        if (fragment == null || !fragment.activeInHierarchy) { yield break; }

        if (fragCollider != null) { fragCollider.enabled = true; }
        if (fragRb != null) { fragRb.useGravity = true; }
    }

    private static readonly Collider[] overlapBuffer = new Collider[16];
    private const float PushOutStep = 0.04f;
    private const int PushOutMaxSteps = 15; // up to 0.6 m

    // Step the piece toward the broken object's centre (room side) until it no longer overlaps static geometry
    private static void PushOutOfStaticGeometry(Transform fragment, Collider fragCollider, Vector3 safeCenter)
    {
        int mask = ~LayerMask.GetMask("Destructed", "Player", "Enemy");

        for (int step = 0; step < PushOutMaxSteps; step++)
        {
            if (!OverlapsStaticGeometry(fragment, fragCollider, mask)) { return; }

            Vector3 toSafe = safeCenter - fragCollider.bounds.center;
            if (toSafe.sqrMagnitude < 0.0001f) { return; }

            fragment.position += toSafe.normalized * Mathf.Min(PushOutStep, toSafe.magnitude);
            Physics.SyncTransforms();
        }
    }

    private static bool OverlapsStaticGeometry(Transform fragment, Collider fragCollider, int mask)
    {
        Bounds b = fragCollider.bounds;
        int count = Physics.OverlapBoxNonAlloc(b.center, b.extents, overlapBuffer, Quaternion.identity, mask, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < count; i++)
        {
            Collider other = overlapBuffer[i];
            if (other == null || other == fragCollider) { continue; }

            // Only walls/floors/static props: moving bodies (other furniture, debris) sort themselves out
            Rigidbody otherRb = other.attachedRigidbody;
            if (otherRb != null && !otherRb.isKinematic) { continue; }

            if (Physics.ComputePenetration(
                    fragCollider, fragment.position, fragment.rotation,
                    other, other.transform.position, other.transform.rotation,
                    out _, out float distance) && distance > 0.001f)
            {
                return true;
            }
        }

        return false;
    }

    // Pieces fly along the attack: closer to the impact = faster, with some spread, lift and spin
    private void ApplyHitDirectionPush(Rigidbody fragRb, Vector3 hitPosition)
    {
        if (lastHitDirection.sqrMagnitude < 0.0001f) { return; }

        // The object is already deactivated here, so use the mesh bounds rather than the renderer's
        float size = meshFilter != null && meshFilter.sharedMesh != null
            ? Mathf.Max(0.3f, Vector3.Scale(meshFilter.sharedMesh.bounds.extents, transform.lossyScale).magnitude)
            : 1f;
        float distance = Vector3.Distance(fragRb.worldCenterOfMass, hitPosition);
        float closeness = Mathf.Lerp(1.25f, 0.6f, Mathf.Clamp01(distance / (size * 2f)));

        Vector3 dir = (lastHitDirection + Random.insideUnitSphere * hitDirectionSpread).normalized;
        float speed = hitDirectionSpeed * closeness * Random.Range(0.8f, 1.2f);

        fragRb.AddForce(dir * speed + Vector3.up * hitUpwardSpeed * Random.Range(0.6f, 1.2f), ForceMode.VelocityChange);
        fragRb.AddTorque(Random.insideUnitSphere * hitSpin, ForceMode.VelocityChange);
    }

    /// <summary>
    /// Fragment materials: one per submesh of the source mesh (so multi-material objects keep every
    /// material on their pieces), followed by the inside material for the cut faces.
    /// </summary>
    public static Material[] BuildFragmentMaterials(Material[] sourceMaterials, Mesh sourceMesh, Material insideMaterial)
    {
        int surfaceCount = Mathf.Max(1, sourceMesh != null ? sourceMesh.subMeshCount : 1);
        var result = new Material[surfaceCount + 1];

        for (int i = 0; i < surfaceCount; i++)
        {
            // Fewer materials than submeshes: reuse the last one (same as Unity's renderer does)
            if (sourceMaterials != null && sourceMaterials.Length > 0)
            {
                result[i] = sourceMaterials[Mathf.Min(i, sourceMaterials.Length - 1)];
            }
        }

        // No inside material set: the cut faces use the first material
        result[surfaceCount] = insideMaterial != null ? insideMaterial : result[0];
        return result;
    }

    private GameObject CreateFragmentTemplate()
    {
        GameObject obj = new GameObject("Fragment");
        obj.tag = tag;
        if (destructableLayer != -1) obj.layer = destructableLayer;

        obj.AddComponent<MeshFilter>();

        var fragRenderer = obj.AddComponent<MeshRenderer>();
        fragRenderer.sharedMaterials = BuildFragmentMaterials(meshRenderer.sharedMaterials, meshFilter.sharedMesh, fractureOptions.insideMaterial);

        // SAFETY CHECK: Only add mesh collider if the mesh has valid triangles to prevent PhysX crashes
        var sharedMesh = meshFilter.sharedMesh;
        if (sharedMesh != null && sharedMesh.vertexCount > 2)
        {
            var fragCollider = obj.AddComponent<MeshCollider>();
            fragCollider.convex = true;
            fragCollider.sharedMaterial = cachedCollider.sharedMaterial;
            fragCollider.isTrigger = cachedCollider.isTrigger;
        }

        var fragRb = obj.AddComponent<Rigidbody>();
        fragRb.linearVelocity = cachedRigidbody.linearVelocity;
        fragRb.angularVelocity = cachedRigidbody.angularVelocity;
        fragRb.linearDamping = cachedRigidbody.linearDamping;
        fragRb.angularDamping = cachedRigidbody.angularDamping;
        fragRb.useGravity = cachedRigidbody.useGravity;

        if (refractureOptions.enableRefracturing && currentRefractureCount < refractureOptions.maxRefractureCount)
        {
            CopyFractureComponent(obj);
        }

        return obj;
    }

    private void CopyFractureComponent(GameObject obj)
    {
        var fractureComponent = obj.AddComponent<Fracture>();

        fractureComponent.triggerOptions = triggerOptions;
        fractureComponent.fractureOptions = fractureOptions;
        fractureComponent.refractureOptions = refractureOptions;
        fractureComponent.callbackOptions = callbackOptions;
        fractureComponent.currentRefractureCount = currentRefractureCount + 1;
        fractureComponent.fragmentRoot = fragmentRoot;
        fractureComponent.maxHealth = maxHealth;
        fractureComponent.collisionDamageMultiplier = collisionDamageMultiplier;
    }
}