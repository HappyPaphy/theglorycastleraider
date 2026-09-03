using UnityEngine;
using System.Collections;

public class ReturnToPool : MonoBehaviour
{
    [Tooltip("Must match the exact string name used in the ObjectPoolingManager (e.g., 'Spark')")]
    public string poolName;

    [Tooltip("How long the effect lasts before disappearing")]
    public float lifetime = 2f;

    private void OnEnable()
    {
        StartCoroutine(DeactivateRoutine());
    }

    private IEnumerator DeactivateRoutine()
    {
        yield return new WaitForSeconds(lifetime);

        if (ObjectPoolingManager.instance != null)
        {
            // Tell the manager exactly which pool to put this back into
            ObjectPoolingManager.instance.ReturnObject(poolName, this.gameObject);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }
}