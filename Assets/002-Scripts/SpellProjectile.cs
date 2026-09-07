using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(Collider))]
public class SpellProjectile : MonoBehaviour
{
    [Header("Projectile Settings")]
    public Vector3 spawnOffSet = new Vector3(0f, -0.35f, 0.5f);
    public float knockBackValue;
    public float speed = 15f;
    public float damage = 25f;
    public float lifeTime = 5f; // Prevents the fireball from floating forever
    public bool isBlockable = false;
    public float knockBackForce = 8f;
    public DamageImpactSound damageImpactSound = DamageImpactSound.None;

    [Header("Visuals")]
    public GameObject impactEffectPrefab;

    private void Start()
    {
        // Ensure the Rigidbody doesn't fall due to gravity if it's a straight-shooting spell
        GetComponent<Rigidbody>().useGravity = false;
        GetComponent<Rigidbody>().isKinematic = true;

        transform.localPosition += spawnOffSet;
        transform.parent = null;

        Destroy(gameObject, lifeTime);
    }

    private void Update()
    {
        // Move the fireball forward based on its local rotation
        transform.Translate(Vector3.forward * speed * Time.deltaTime);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player")) return;

        EnemyEntity targetEnemy = other.GetComponent<EnemyEntity>();
        Fracture destructible = other.GetComponent<Fracture>();

        if (targetEnemy != null)
        {
            // False for 'isLeftAttack' as spells don't have a specific swing direction
            targetEnemy.TakeSpellHit(false, other.ClosestPoint(transform.position), damage, isBlockable, knockBackForce, damageImpactSound);
        }
        else if (destructible != null)
        {
            SoundManager.instance.WoodenSound_Hit(other.ClosestPoint(transform.position));
            destructible.TakeDamage(damage, other, other.ClosestPoint(transform.position));
        }

        // Spawn explosion/impact particles
        if (impactEffectPrefab != null)
        {
            Instantiate(impactEffectPrefab, transform.position, transform.rotation);
        }

        // Destroy the fireball immediately upon hitting anything
        Destroy(gameObject);
    }
}