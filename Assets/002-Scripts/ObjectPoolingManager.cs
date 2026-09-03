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
            poolDictionary[poolName].Enqueue(newObj);
        }

        // Pull the next available object from the front of the specific queue
        GameObject objectToSpawn = poolDictionary[poolName].Dequeue();

        objectToSpawn.transform.position = position;
        objectToSpawn.SetActive(true);

        return objectToSpawn;
    }

    public void ReturnObject(string poolName, GameObject obj)
    {
        obj.SetActive(false);
        poolDictionary[poolName].Enqueue(obj); // Put it back at the end of the line
    }
}
