using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ObjectPoolingManager : MonoBehaviour
{
    [System.Serializable]
    public class Pool
    {
        public string poolName; // e.g., "Spark", "Blood", "EnemyBullet"
        public GameObject prefab;
        public int initialSize;
    }

    public static ObjectPoolingManager instance;

    [Header("Pool Settings")]
    public List<Pool> pools;

    // Dictionaries let us look up the correct queue and prefab using the poolName string
    private Dictionary<string, Queue<GameObject>> poolDictionary;
    private Dictionary<string, GameObject> prefabDictionary;

    private void Awake()
    {
        // Fixed capitalization from your draft (instance vs Instance)
        if (instance == null) instance = this;
        else { Destroy(gameObject); return; }

        poolDictionary = new Dictionary<string, Queue<GameObject>>();
        prefabDictionary = new Dictionary<string, GameObject>();

        // Loop through the list you set in the Inspector and build a queue for each one
        foreach (Pool pool in pools)
        {
            Queue<GameObject> objectPool = new Queue<GameObject>();
            prefabDictionary.Add(pool.poolName, pool.prefab);

            for (int i = 0; i < pool.initialSize; i++)
            {
                GameObject obj = Instantiate(pool.prefab);
                obj.SetActive(false);
                obj.transform.SetParent(transform);

                if(pool.poolName == "Fragment")
                {
                    if (obj.GetComponent<MeshRenderer>().enabled == false)
                        obj.GetComponent<MeshRenderer>().enabled = true;
                }

                objectPool.Enqueue(obj);
            }

            poolDictionary.Add(pool.poolName, objectPool);
        }
    }

    public GameObject SpawnObject(string poolName, Vector3 position)
    {
        if (!poolDictionary.ContainsKey(poolName))
        {
            Debug.LogWarning($"Pool with name {poolName} doesn't exist.");
            return null;
        }

        // If the pool is empty (too many effects on screen), expand it automatically
        if (poolDictionary[poolName].Count == 0)
        {
            GameObject newObj = Instantiate(prefabDictionary[poolName]);
            newObj.SetActive(false);
            newObj.transform.SetParent(transform);

            if (poolName == "Fragment")
            {
                if (newObj.GetComponent<MeshRenderer>().enabled == false)
                    newObj.GetComponent<MeshRenderer>().enabled = true;
            }

            poolDictionary[poolName].Enqueue(newObj);
        }

        // Pull the next available object from the front of the specific queue
        GameObject objectToSpawn = poolDictionary[poolName].Dequeue();

        var pooledFrag = objectToSpawn.GetComponent<PooledFragment>();
        if (pooledFrag != null)
        {
            pooledFrag.spawnSessionId++;
        }

        objectToSpawn.transform.position = position;
        objectToSpawn.SetActive(true);

        return objectToSpawn;
    }

    public void ReturnObject(string poolName, GameObject obj)
    {
        if (!obj.activeSelf) return; // Prevent double-enqueuing

        obj.SetActive(false);
        obj.transform.SetParent(transform);
        poolDictionary[poolName].Enqueue(obj); // Put it back at the end of the line
    }

    public void ReturnObjectDelayed(string poolName, GameObject obj, float delay)
    {
        var pooledFrag = obj.GetComponent<PooledFragment>();
        int targetSession = pooledFrag != null ? pooledFrag.spawnSessionId : 0;
        StartCoroutine(ReturnRoutine(poolName, obj, delay, targetSession));
    }

    private IEnumerator ReturnRoutine(string poolName, GameObject obj, float delay, int targetSession)
    {
        yield return new WaitForSeconds(delay);

        var pooledFrag = obj.GetComponent<PooledFragment>();
        if (pooledFrag != null && pooledFrag.spawnSessionId != targetSession)
        {
            yield break; // Abort stale return call
        }

        if (obj.activeSelf)
        {
            ReturnObject(poolName, obj);
        }
    }
}
