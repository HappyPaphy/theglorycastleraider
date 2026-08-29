using UnityEngine;

public class ArrowProjectile : MonoBehaviour
{
    public float damage = 8f;
    public float staminaCost = 8f;
    [SerializeField] private float speed = 25f;
    [SerializeField] private float lifeTime = 4f;
    [SerializeField] private LayerMask playerLayer;

    private void Start()
    {
        // Automatically clean up missed arrows
        Destroy(gameObject, lifeTime);
    }

    private void Update()
    {
        // Fly forward
        transform.position += transform.forward * speed * Time.deltaTime;
    }

    private void OnTriggerEnter(Collider other)
    {
        // 1. Check if we hit the player
        if (((1 << other.gameObject.layer) & playerLayer) != 0)
        {
            PlayerController player = other.GetComponent<PlayerController>();
            if (player != null)
            {
                // Trigger the same damage method your melee enemies use
                player.TakeArrowHit(this);
            }
            Destroy(gameObject); // Destroy arrow on hit
        }
        // 2. Destroy on walls/environment (assuming they aren't on the Enemy layer)
        else if (other.gameObject.layer != LayerMask.NameToLayer("Enemy"))
        {
            Destroy(gameObject);
        }
    }
}