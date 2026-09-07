using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class SpellContinuous : MonoBehaviour
{
    [Header("Continuous Settings")]
    public float damagePerTick = 10f;
    public float tickRate = 0.25f; // Applies damage every 0.25 seconds

    private float nextTickTime;
    private List<Collider> targetsInFire = new List<Collider>();

    private void Start()
    {
        // Ensure the collider is set to Trigger so enemies don't get pushed by the flames
        GetComponent<Collider>().isTrigger = true;
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

    private void Update()
    {
        if (Time.time >= nextTickTime)
        {
            nextTickTime = Time.time + tickRate;
            ApplyDamageTick();
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
                targetEnemy.TakeSwordHit(false, col.ClosestPoint(transform.position), damagePerTick);
            }
            else if (destructible != null)
            {
                destructible.TakeDamage(damagePerTick, col, col.ClosestPoint(transform.position));
            }
        }
    }
}