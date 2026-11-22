using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runs a Challenge Room: rolls a challenge for the altar, and when the player starts it, closes the gates,
/// spawns the waves (with elites), then opens the gates and gives the reward chest.
/// While a challenge runs, its modifiers apply (see <see cref="DamageTakenMultiplier"/>).
/// </summary>
public class ChallengeRoom : MonoBehaviour
{
    public enum State { Ready, Running, Cleared }

    [SerializeField] private ChallengeAltar altar;
    [Tooltip("Where enemies appear.")]
    [SerializeField] private List<Transform> spawnPoints = new List<Transform>();
    [Tooltip("Where the reward chest appears.")]
    [SerializeField] private Transform chestPoint;
    [Tooltip("Optional: a challenge for this room. Empty = rolled from ChallengeSettings.")]
    [SerializeField] private ChallengeDefinition challenge;

    private ChallengeSettings settings;
    private State state = State.Ready;
    private readonly List<EnemyEntity> alive = new List<EnemyEntity>();
    private readonly List<ChallengeGate> gates = new List<ChallengeGate>();
    private int currentWave;
    private float startTime;

    private static ChallengeRoom active;

    // ---------- Modifiers of the running challenge (neutral when none runs) ----------
    public static float DamageTakenMultiplier => active != null && active.challenge != null ? active.challenge.damageTakenMultiplier : 1f;

    public ChallengeDefinition Challenge => challenge;
    public State CurrentState => state;
    public int CurrentWave => currentWave;
    public int AliveCount => alive.Count;

    private void Start()
    {
        settings = ChallengeSettings.Load();
        if (challenge == null && settings != null) { challenge = Roll(); }

        if (challenge == null)
        {
            if (altar != null) { altar.gameObject.SetActive(false); }
            return;
        }

        if (altar != null) { altar.Setup(this); }
    }

    private void OnDisable()
    {
        // Room unloaded mid-fight (new floor): drop the modifiers
        if (active == this) { active = null; ChallengeHud.Hide(); }
    }

    private ChallengeDefinition Roll()
    {
        int floor = RoguelikeManager.instance != null ? RoguelikeManager.instance.dungeonIndex + 1 : 1;
        float total = 0f;
        var options = new List<ChallengeDefinition>();
        foreach (ChallengeDefinition c in settings.challengePool)
        {
            if (c == null || c.weight <= 0f || floor < c.minFloor || c.waves.Count == 0) { continue; }
            options.Add(c);
            total += c.weight;
        }
        if (options.Count == 0) { return null; }

        float roll = Random.value * total;
        foreach (ChallengeDefinition c in options)
        {
            roll -= c.weight;
            if (roll <= 0f) { return c; }
        }
        return options[options.Count - 1];
    }

    // =========================================================
    // RUN
    // =========================================================

    /// <summary>Called by the altar when the player accepts.</summary>
    public void Begin()
    {
        if (state != State.Ready || challenge == null) { return; }

        state = State.Running;
        active = this;
        startTime = Time.time;

        // Gates sit in this tile's doorways (DunGen spawns them as connectors)
        gates.Clear();
        Transform tileRoot = GetComponentInParent<DunGen.Tile>() != null ? GetComponentInParent<DunGen.Tile>().transform : transform.root;
        gates.AddRange(tileRoot.GetComponentsInChildren<ChallengeGate>(true));
        foreach (ChallengeGate gate in gates) { gate.Close(); }

        StartCoroutine(RunWaves());
    }

    private IEnumerator RunWaves()
    {
        int floorIndex = RoguelikeManager.instance != null ? RoguelikeManager.instance.dungeonIndex : 0;
        yield return new WaitForSeconds(1f);

        for (currentWave = 1; currentWave <= challenge.waves.Count; currentWave++)
        {
            ChallengeDefinition.Wave wave = challenge.waves[currentWave - 1];
            ChallengeHud.Show(this, $"Wave {currentWave} / {challenge.waves.Count}");

            for (int i = 0; i < wave.enemyCount; i++)
            {
                bool elite = challenge.allElites || i < wave.guaranteedElites || Random.value < wave.eliteChance;
                SpawnEnemy(floorIndex, elite);
                yield return new WaitForSeconds(challenge.spawnInterval);
            }

            // Wait for the wave to die
            while (true)
            {
                alive.RemoveAll(e => e == null || !e.gameObject.activeInHierarchy || e.CharacterHealthComponent.CurrentHP <= 0f);
                ChallengeHud.Refresh(this);
                if (alive.Count == 0) { break; }
                if (PlayerController.instance == null || PlayerController.instance.CharacterHealthComponent.CurrentHP <= 0f)
                {
                    ChallengeHud.Hide();
                    yield break; // player died: the run is over anyway
                }
                yield return null;
            }

            if (currentWave < challenge.waves.Count)
            {
                ChallengeHud.Show(this, "Wave cleared!");
                yield return new WaitForSeconds(challenge.delayBetweenWaves);
            }
        }

        Complete();
    }

    private void SpawnEnemy(int floorIndex, bool elite)
    {
        GameObject prefab = PickEnemy(floorIndex + 1);
        if (prefab == null || spawnPoints.Count == 0) { return; }

        Transform point = spawnPoints[Random.Range(0, spawnPoints.Count)];
        Vector3 pos = point.position + new Vector3(Random.Range(-0.3f, 0.3f), 0f, Random.Range(-0.3f, 0.3f));

        if (ObjectPoolingManager.instance != null) { ObjectPoolingManager.instance.SpawnObject("Spark", pos + Vector3.up * 0.8f); }

        Vector3 lookDir = PlayerController.instance != null ? PlayerController.instance.transform.position - pos : Vector3.forward;
        lookDir.y = 0f;
        GameObject go = Instantiate(prefab, pos, lookDir.sqrMagnitude > 0.01f ? Quaternion.LookRotation(lookDir) : Quaternion.identity);
        if (RoguelikeManager.instance != null) { RoguelikeManager.instance.obj_Enemies.Add(go); }

        if (elite) { EliteEnemy.MakeElite(go, floorIndex); }

        EnemyEntity enemy = go.GetComponentInChildren<EnemyEntity>();
        if (enemy != null)
        {
            enemy.PlayerDetected(); // straight for the player
            alive.Add(enemy);
        }
    }

    // Enemies the normal spawners use on this floor (cached per floor)
    private List<EnemySpawnData> roster;
    private int rosterFloor = -1;

    private GameObject PickEnemy(int floor)
    {
        int floorIndex = floor - 1;
        if (roster == null || rosterFloor != floorIndex)
        {
            roster = FloorEnemyTable.Roster(floorIndex);
            rosterFloor = floorIndex;
        }
        var allowed = new HashSet<GameObject>();
        foreach (EnemySpawnData d in roster) { allowed.Add(d.enemyPrefab); }

        List<ChallengeDefinition.EnemyOption> pool = challenge.enemies != null && challenge.enemies.Count > 0 ? challenge.enemies : settings.defaultEnemies;
        float total = 0f;
        var options = new List<ChallengeDefinition.EnemyOption>();
        foreach (ChallengeDefinition.EnemyOption o in pool)
        {
            if (o == null || o.prefab == null || o.weight <= 0f || floor < o.minFloor) { continue; }
            // Only enemies that appear on this floor
            if (allowed.Count > 0 && !allowed.Contains(o.prefab)) { continue; }
            options.Add(o);
            total += o.weight;
        }

        // The challenge's own list has none of this floor's enemies: use the floor's enemies directly
        if (options.Count == 0 && roster.Count > 0)
        {
            float sum = 0f;
            foreach (EnemySpawnData d in roster) { sum += Mathf.Max(1f, d.spawnChance); }
            float r = Random.value * sum;
            foreach (EnemySpawnData d in roster)
            {
                r -= Mathf.Max(1f, d.spawnChance);
                if (r <= 0f) { return d.enemyPrefab; }
            }
            return roster[roster.Count - 1].enemyPrefab;
        }
        if (options.Count == 0) { return null; }

        float roll = Random.value * total;
        foreach (ChallengeDefinition.EnemyOption o in options)
        {
            roll -= o.weight;
            if (roll <= 0f) { return o.prefab; }
        }
        return options[options.Count - 1].prefab;
    }

    private void Complete()
    {
        MetaRun.ChallengeCleared(); // meta progression: Emerald reward
        state = State.Cleared;
        if (active == this) { active = null; }

        foreach (ChallengeGate gate in gates) { if (gate != null) gate.Open(); }

        float time = Time.time - startTime;
        bool beatTime = challenge.timeLimit > 0f && time <= challenge.timeLimit;
        ChallengeHud.Show(this, beatTime ? $"Challenge complete! ({time:0}s - time bonus!)" : "Challenge complete!");
        ChallengeHud.HideAfter(3f);

        // Reward chest
        GameObject chest = settings.GetChest(challenge.rewardChest);
        Transform at = chestPoint != null ? chestPoint : transform;
        if (chest != null) { Instantiate(chest, at.position, at.rotation); }

        int coins = challenge.bonusCoins + (beatTime ? challenge.timeBonusCoins : 0);
        if (coins > 0) { CoinPickup.Drop(at.position + Vector3.up * 1f, coins, 8); }

        if (altar != null) { altar.OnCleared(); }
        if (PlayerController.instance != null && PlayerController.instance.cameraBob != null) { PlayerController.instance.cameraBob.TriggerShake(0.3f, 0.1f); }
    }

    public float Elapsed => state == State.Running ? Time.time - startTime : 0f;
}
