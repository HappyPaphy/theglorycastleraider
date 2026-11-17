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

    [Header("Fracture Pieces")]
    [Tooltip("Most fracture pieces alive at once. When more spawn, the oldest are recycled early. " +
             "Bounds memory (each piece has its own mesh + convex collider) and physics cost.")]
    [SerializeField] private int maxActiveFragments = 120;

    private const string FragmentPool = "Fragment";
    private readonly LinkedList<GameObject> activeFragments = new LinkedList<GameObject>();
    private readonly Dictionary<GameObject, LinkedListNode<GameObject>> activeFragmentNodes = new Dictionary<GameObject, LinkedListNode<GameObject>>();

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

        if (poolName == FragmentPool) { TrackFragment(objectToSpawn); }

        return objectToSpawn;
    }

    public void ReturnObject(string poolName, GameObject obj)
    {
        if (obj == null || !obj.activeSelf) return; // Prevent double-enqueuing

        if (poolName == FragmentPool)
        {
            UntrackFragment(obj);

            // Free the generated mesh now that the piece is done (see PooledFragment)
            var pooled = obj.GetComponent<PooledFragment>();
            if (pooled != null) { pooled.ReleaseMesh(); }
        }

        var pooledFragCollider = obj.GetComponent<Collider>();

        if(pooledFragCollider != null )
            pooledFragCollider.enabled = true;

        obj.SetActive(false);
        obj.transform.SetParent(transform);
        poolDictionary[poolName].Enqueue(obj); // Put it back at the end of the line
    }

    // Keep a list of live pieces, oldest first; recycle the oldest when over the cap
    private void TrackFragment(GameObject obj)
    {
        UntrackFragment(obj);
        activeFragmentNodes[obj] = activeFragments.AddLast(obj);

        while (activeFragments.Count > Mathf.Max(1, maxActiveFragments))
        {
            GameObject oldest = activeFragments.First.Value;
            if (oldest == obj) { break; }

            if (oldest == null || !oldest.activeSelf)
            {
                activeFragmentNodes.Remove(oldest);
                activeFragments.RemoveFirst();
                continue;
            }

            ReturnObject(FragmentPool, oldest); // also untracks it
        }
    }

    private void UntrackFragment(GameObject obj)
    {
        if (obj != null && activeFragmentNodes.TryGetValue(obj, out var node))
        {
            activeFragments.Remove(node);
            activeFragmentNodes.Remove(obj);
        }
    }

    public void ReturnObjectDelayed(string poolName, GameObject obj, float delay, float colliderDelay)
    {
        var pooledFrag = obj.GetComponent<PooledFragment>();
        int targetSession = pooledFrag != null ? pooledFrag.spawnSessionId : 0;
        StartCoroutine(ReturnRoutine(poolName, obj, delay, targetSession, colliderDelay));
    }

    private IEnumerator ReturnRoutine(string poolName, GameObject obj, float delay, int targetSession, float colliderDelay)
    {
        yield return new WaitForSeconds(colliderDelay);

        // Recycled early (fragment cap) or destroyed meanwhile: nothing to do
        if (obj == null) { yield break; }
        var sessionCheck = obj.GetComponent<PooledFragment>();
        if (sessionCheck != null && sessionCheck.spawnSessionId != targetSession) { yield break; }

        var pooledFragCollider = obj.GetComponent<Collider>();
        if (pooledFragCollider != null) { pooledFragCollider.enabled = false; }

        yield return new WaitForSeconds(delay);

        if (obj == null) { yield break; }
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
