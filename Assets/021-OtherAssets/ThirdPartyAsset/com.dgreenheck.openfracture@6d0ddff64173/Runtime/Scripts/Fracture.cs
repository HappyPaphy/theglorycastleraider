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
    public float timeBeforeFragmentDestroy = 2f;

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

        System.Action onFractureComplete = () =>
        {
            if (fragmentTemplate != null)
            {
                Destroy(fragmentTemplate);
            }

            gameObject.SetActive(false);
            ApplyForceToFragments(impactPoint);

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

    private void ApplyForceToFragments(Vector3 hitPosition)
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
                Destroy(fragment.gameObject);
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
                fragRb.WakeUp(); // Ensure the rigidbody isn't sleeping from pool reuse

                // Clear any leftover velocity from its previous lifecycle
                fragRb.linearVelocity = Vector3.zero;
                fragRb.angularVelocity = Vector3.zero;

                fragRb.AddExplosionForce(fragmentExplosionForce, hitPosition, fragmentExplosionRadius, 0.5f, ForceMode.Impulse);
            }

            ObjectPoolingManager.instance.ReturnObjectDelayed("Fragment", fragment.gameObject, timeBeforeFragmentDestroy);
        }
    }

    private GameObject CreateFragmentTemplate()
    {
        GameObject obj = new GameObject("Fragment");
        obj.tag = tag;
        if (destructableLayer != -1) obj.layer = destructableLayer;

        obj.AddComponent<MeshFilter>();

        var fragRenderer = obj.AddComponent<MeshRenderer>();
        fragRenderer.sharedMaterials = new Material[] { meshRenderer.sharedMaterial, fractureOptions.insideMaterial };

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