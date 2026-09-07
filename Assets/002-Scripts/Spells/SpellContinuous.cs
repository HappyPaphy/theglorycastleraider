using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class SpellContinuous : MonoBehaviour
{
    public DamageImpactSound damageImpactSound = DamageImpactSound.None;

    [Header("Continuous Settings")]
    public Vector3 spawnOffSet = new Vector3(0f, -0.35f, 0.5f);
    public float damagePerTick = 10f;
    public float tickRate = 0.25f; // Applies damage every 0.25 seconds
    public bool isBlockable = false;
    public float knockBackForce = 1f;

    [Header("Forward Travel (Spit Effect)")]
    [Tooltip("How fast the flame burst travels forward upon being spat.")]
    public float forwardSpeed = 8f;
    [Tooltip("How long (in seconds) the flame burst continues moving forward before holding position.")]
    public float travelDuration = 0.2f;

    public float lifeTime = 1f; // Prevents the fireball from floating forever
    private float nextTickTime;
    private float travelTimer;
    private List<Collider> targetsInFire = new List<Collider>();

    private void Start()
    {
        Rigidbody rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        rb.isKinematic = true;

        // Ensure the collider is set to Trigger so enemies don't get pushed by the flames
        GetComponent<Collider>().isTrigger = true;
        travelTimer = travelDuration;
        transform.localPosition += spawnOffSet;
        transform.parent = null;

        Destroy(gameObject, lifeTime);
    }

    private void Update()
    {
        // Handle forward spit movement for the initial duration
        if (travelTimer > 0f)
        {
            transform.Translate(Vector3.forward * forwardSpeed * Time.deltaTime);
            travelTimer -= Time.deltaTime;
        }

        // Handle damage ticks
        if (Time.time >= nextTickTime)
        {
            nextTickTime = Time.time + tickRate;
            ApplyDamageTick();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player")) return;

        // Add enemy to the burn list when they touch the flames
        if (!targetsInFire.Contains(other))
        {
            targetsInFire.Add(other);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        // Remove enemy when they escape the flames
        if (targetsInFire.Contains(other))
        {
            targetsInFire.Remove(other);
        }
    }

    private void ApplyDamageTick()
    {
        // Clean the list in case an enemy died and its GameObject was destroyed
        targetsInFire.RemoveAll(item => item == null);

        foreach (Collider col in targetsInFire)
        {
            EnemyEntity targetEnemy = col.GetComponent<EnemyEntity>();
            Fracture destructible = col.GetComponent<Fracture>();

            if (targetEnemy != null)
            {
                targetEnemy.TakeSpellHit(false, col.ClosestPoint(transform.position), damagePerTick, isBlockable, knockBackForce, damageImpactSound);
            }
            else if (destructible != null)
            {
                destructible.TakeDamage(damagePerTick, col, col.ClosestPoint(transform.position));
            }
        }
    }
}