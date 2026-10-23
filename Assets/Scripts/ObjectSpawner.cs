using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Spawns a list of wave patterns one after another until the earth arrives.
// Each pattern's children are revealed one Z step at a time, closest to the
// camera first; children that share the same Z appear together, so a row
// spawns as a row.
public class ObjectSpawner : MonoBehaviour
{
    public enum WaveOrder { Sequential, Random }

    [Serializable]
    public class Wave
    {
        [Tooltip("The pattern prefab to spawn for this wave.")]
        public GameObject pattern;
        [Tooltip("Seconds to wait before the next wave starts.")]
        public float delayAfter = 2f;
        [Tooltip("Points when every object in the wave is gone.")]
        public float clearPoints = 300f;
        [Tooltip("Added on top if no life was lost during the wave.")]
        public float perfectBonus = 200f;
    }

    [Tooltip("The list of waves. Each has a pattern (the prefab to spawn) and a delayAfter.")]
    [SerializeField] private List<Wave> waves = new List<Wave>();
    [Tooltip("Sequential plays the list top to bottom. Random shuffles it each time through.")]
    [SerializeField] private WaveOrder order = WaveOrder.Sequential;
    [Tooltip("Start the list over after the last wave. If off, it plays through once and stops.")]
    [SerializeField] private bool loop = true;
    [Tooltip("Seconds before the first wave.")]
    [SerializeField] private float startDelay = 1f;
    [Tooltip("Copies of each pattern made at level start, so the first waves don't stutter.")]
    [SerializeField] private int prewarmPerPattern = 1;

    // Pooling: free copies per pattern prefab, and each copy's children in reveal order.
    private readonly Dictionary<GameObject, Stack<PatternCopy>> freeCopies = new Dictionary<GameObject, Stack<PatternCopy>>();
    private readonly Dictionary<PatternCopy, Transform[]> revealOrder = new Dictionary<PatternCopy, Transform[]>();

    // A revealed wave waiting to clear: its settings, Score's LivesLostCount when it
    // started, and whether the earth cut it short.
    private struct RunningWave
    {
        public Wave wave;
        public int livesLostAtStart;
        public bool cutShort;
    }

    private readonly Dictionary<PatternCopy, RunningWave> runningWaves = new Dictionary<PatternCopy, RunningWave>();

    // Children this close in Z spawn together. Public so PatternGizmos groups them the same way.
    public const float SameZTolerance = 0.01f;

    private LevelTimer levelTimer;
    private Score score;

    // Once the earth has arrived or the game is over, no more waves or objects are spawned.
    private bool StopSpawning => (levelTimer != null && levelTimer.IsComplete) ||
                                 (GameManager.Instance != null && GameManager.Instance.IsGameOver);

    private void Awake()
    {
        // Position comes from OrbitSettings so it always matches the pattern builder.
        transform.position = OrbitSettings.Instance.SpawnerPosition;
    }

    private void Start()
    {
        levelTimer = GameManager.Instance != null ? GameManager.Instance.LevelTimer : null;
        score = GameManager.Instance != null ? GameManager.Instance.Score : null;

#if UNITY_EDITOR
        // Test This Pattern: play only that pattern, on a loop. Only changes the list in memory, not the scene.
        if (PatternTest.Active && PatternTest.Pattern != null)
        {
            waves = new List<Wave> { new Wave { pattern = PatternTest.Pattern, delayAfter = PatternTest.LoopDelay, clearPoints = 0f, perfectBonus = 0f } };
            order = WaveOrder.Sequential;
            loop = true;
        }
#endif

        // Make each pattern's first copies now, so the first waves don't stutter.
        foreach (Wave wave in waves)
        {
            if (wave.pattern == null) continue;
            Stack<PatternCopy> free = GetFreeList(wave.pattern);
            while (free.Count < prewarmPerPattern)
                free.Push(CreateCopy(wave.pattern));
        }

        if (waves.Count > 0)
            StartCoroutine(RunWaves());
    }

    private IEnumerator RunWaves()
    {
        // Wait for the menu to start the game; levels without a menu have already started.
        yield return new WaitUntil(() => GameManager.Instance == null || GameManager.Instance.HasStarted);
        yield return new WaitForSeconds(startDelay);

        do
        {
            bool spawnedAny = false;
            foreach (Wave wave in GetWaveOrder())
            {
                if (StopSpawning) yield break;
                if (wave.pattern == null) continue;

                spawnedAny = true;

                // Wait for the whole pattern to finish revealing, then pause.
                yield return SpawnPattern(wave);
#if UNITY_EDITOR
                // Test This Pattern: let the whole run fly past before the next one.
                if (PatternTest.Active)
                    yield return new WaitUntil(() => AsteroidBehavior.Active.Count == 0 && PowerUp.Active.Count == 0);
#endif
                yield return new WaitForSeconds(wave.delayAfter);
            }

            if (!spawnedAny)
            {
                Debug.LogWarning("ObjectSpawner has no wave with a pattern assigned, so it is not spawning.", this);
                yield break;
            }
        }
        while (loop && !StopSpawning);
    }

    private List<Wave> GetWaveOrder()
    {
        var ordered = new List<Wave>(waves);

        // Random plays every wave once per pass in a shuffled order, so no
        // wave is skipped or repeated until the pass is done.
        if (order == WaveOrder.Random)
        {
            for (int i = ordered.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (ordered[i], ordered[j]) = (ordered[j], ordered[i]);
            }
        }

        return ordered;
    }

    // The free list for one pattern prefab, made the first time it's needed.
    private Stack<PatternCopy> GetFreeList(GameObject prefab)
    {
        if (!freeCopies.TryGetValue(prefab, out Stack<PatternCopy> list))
            freeCopies[prefab] = list = new Stack<PatternCopy>();
        return list;
    }

    // PatternCopy.OnReleased: every child is home, so the wave scores and the copy
    // waits for its next use.
    private void HandleReleased(PatternCopy copy)
    {
        if (runningWaves.TryGetValue(copy, out RunningWave run))
        {
            runningWaves.Remove(copy);
            if (score != null) AwardWave(run);
        }
        GetFreeList(copy.Prefab).Push(copy);
    }

    // Half points if the earth cut it short; the perfect bonus only if no life was lost during it.
    private void AwardWave(RunningWave run)
    {
        if (run.cutShort)
        {
            score.AddWaveClear(run.wave.clearPoints * 0.5f, false);
            return;
        }

        bool perfect = score.LivesLostCount == run.livesLostAtStart;
        score.AddWaveClear(run.wave.clearPoints + (perfect ? run.wave.perfectBonus : 0f), perfect);
    }

    // Makes a new copy of a pattern with its children hidden, ready to be revealed.
    private PatternCopy CreateCopy(GameObject prefab)
    {
        // Parenting to the spawner keeps the pattern's own offset from it.
        GameObject root = Instantiate(prefab, transform);

        var children = new Transform[root.transform.childCount];
        for (int i = 0; i < children.Length; i++)
        {
            children[i] = root.transform.GetChild(i);
            children[i].gameObject.SetActive(false);
        }

        // Order by Z distance from the camera only, closest first.
        Camera cam = GameManager.Instance != null ? GameManager.Instance.GameplayCamera : null;
        float camZ = cam != null ? cam.transform.position.z : 0f;
        Array.Sort(children, (a, b) =>
            Mathf.Abs(a.position.z - camZ).CompareTo(Mathf.Abs(b.position.z - camZ)));

        PatternCopy copy = root.AddComponent<PatternCopy>();
        copy.Init(prefab);
        copy.OnReleased += HandleReleased;
        revealOrder[copy] = children;
        return copy;
    }

    // A free copy of the pattern if there is one; a new one otherwise.
    private PatternCopy GetCopy(GameObject prefab)
    {
        Stack<PatternCopy> free = GetFreeList(prefab);
        return free.Count > 0 ? free.Pop() : CreateCopy(prefab);
    }

    private IEnumerator SpawnPattern(Wave wave)
    {
        PatternCopy copy = GetCopy(wave.pattern);
        Transform[] children = revealOrder[copy];
        copy.BeginReveal();
        int livesLostAtStart = score != null ? score.LivesLostCount : 0;
        bool cutShort = false;

        Camera cam = GameManager.Instance != null ? GameManager.Instance.GameplayCamera : null;
        float camZ = cam != null ? cam.transform.position.z : 0f;

        // Every child appears at the first step's Z (the spawn plane). The wait
        // before each later step is how long it takes to fly that step's Z gap, so
        // in flight each step trails the one before by exactly its authored gap.
        float spawnZ = children.Length > 0 ? children[0].position.z : 0f;

        float previousDistance = 0f;
        for (int i = 0; i < children.Length; i++)
        {
            float distance = Mathf.Abs(children[i].position.z - camZ);
            float gap = distance - previousDistance;

            // Equal Zs spawn together; a further Z waits for its gap.
            if (i > 0 && gap > SameZTolerance)
            {
                float approachSpeed = GetApproachSpeed(children[i]);
                if (approachSpeed > 0f)
                    yield return new WaitForSeconds(gap / approachSpeed);
            }
            previousDistance = distance;

            // Earth arrived or game over mid-pattern: stop revealing. The hidden children
            // stay home in the copy, ready for its next use.
            if (StopSpawning)
            {
                cutShort = true;
                break;
            }

            // Reveal it at the spawn plane. Its OnEnable records its start point
            // (and, for asteroids, starts the warning blink).
            Vector3 p = children[i].position;
            children[i].position = new Vector3(p.x, p.y, spawnZ);
            copy.ChildRevealed();
            children[i].gameObject.SetActive(true);
        }

        runningWaves[copy] = new RunningWave { wave = wave, livesLostAtStart = livesLostAtStart, cutShort = cutShort };
        copy.EndReveal();
    }

    // Asteroids and power-ups both report how fast they close on the camera.
    private float GetApproachSpeed(Transform child)
    {
        AsteroidBehavior asteroid = child.GetComponent<AsteroidBehavior>();
        if (asteroid != null) return asteroid.ApproachSpeed;

        PowerUp powerUp = child.GetComponent<PowerUp>();
        return powerUp != null ? powerUp.ApproachSpeed : 0f;
    }
}
