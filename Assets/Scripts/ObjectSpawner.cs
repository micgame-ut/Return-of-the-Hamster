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
        public GameObject pattern;
        public float delayAfter = 2f;      // seconds to wait before the next wave starts
    }

    [SerializeField] private List<Wave> waves = new List<Wave>();
    [SerializeField] private WaveOrder order = WaveOrder.Sequential;
    [SerializeField] private bool loop = true;
    [SerializeField] private float startDelay = 1f; // seconds before the first wave
    [SerializeField] private int prewarmPerPattern = 1; // copies of each pattern made at level start

    // Pooling: free copies per pattern prefab, and each copy's children in reveal order.
    private readonly Dictionary<GameObject, Stack<PatternCopy>> freeCopies = new Dictionary<GameObject, Stack<PatternCopy>>();
    private readonly Dictionary<PatternCopy, Transform[]> revealOrder = new Dictionary<PatternCopy, Transform[]>();

    // Children this close in Z spawn together. Public so PatternGizmos groups them the same way.
    public const float SameZTolerance = 0.01f;

    private LevelTimer levelTimer;

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
        yield return new WaitForSeconds(startDelay);

        do
        {
            foreach (Wave wave in GetWaveOrder())
            {
                if (StopSpawning) yield break;
                if (wave.pattern == null) continue;

                // Wait for the whole pattern to finish revealing, then pause.
                yield return SpawnPattern(wave.pattern);
                yield return new WaitForSeconds(wave.delayAfter);
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

    // PatternCopy.OnReleased: every child is home, so the copy waits for its next wave.
    private void HandleReleased(PatternCopy copy) => GetFreeList(copy.Prefab).Push(copy);

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

    private IEnumerator SpawnPattern(GameObject prefab)
    {
        PatternCopy copy = GetCopy(prefab);
        Transform[] children = revealOrder[copy];
        copy.BeginReveal();

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
            if (StopSpawning) break;

            // Reveal it at the spawn plane. Its OnEnable records its start point
            // (and, for asteroids, starts the warning blink).
            Vector3 p = children[i].position;
            children[i].position = new Vector3(p.x, p.y, spawnZ);
            copy.ChildRevealed();
            children[i].gameObject.SetActive(true);
        }

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
