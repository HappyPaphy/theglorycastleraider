using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(Collider))]
public class EnemyFloatingHead : MonoBehaviour
{
    [SerializeField] private float minUpwardForce = 4f;
    [SerializeField] private float maxUpwardForce = 7f;
    [SerializeField] private float horizontalSpread = 2f;
    [SerializeField] private float torqueAmount = 30f;
    [SerializeField] private float destroyDelay = 3f;

    private Rigidbody rb;
    private bool hitGround = false;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = true;
        rb.isKinematic = false;

        // Launch head into the air with a random arc
        Vector3 randomDir = new Vector3(
            Random.Range(-horizontalSpread, horizontalSpread),
            Random.Range(minUpwardForce, maxUpwardForce),
            Random.Range(-horizontalSpread, horizontalSpread)
        );
        rb.AddForce(randomDir, ForceMode.Impulse);

        // Add wild spinning rotation
        rb.AddTorque(Random.insideUnitSphere * torqueAmount, ForceMode.Impulse);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (hitGround) return;

        // Ignore collisions with the player or enemy bodies
        if (!other.gameObject.CompareTag("Player") && !other.gameObject.CompareTag("Enemy"))
        {
            hitGround = true;
            Destroy(gameObject, destroyDelay);
        }
    }
}