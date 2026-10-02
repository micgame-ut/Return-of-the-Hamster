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

    private IEnumerator SpawnPattern(GameObject prefab)
    {
        // Parenting to the spawner keeps the pattern's own offset from it.
        GameObject pattern = Instantiate(prefab, transform);

        // Snapshot and hide the children before the first frame renders.
        var children = new List<Transform>(pattern.transform.childCount);
        foreach (Transform child in pattern.transform)
            children.Add(child);
        foreach (Transform child in children)
            child.gameObject.SetActive(false);

        // Order by Z distance from the camera only, closest first.
        Camera cam = GameManager.Instance != null ? GameManager.Instance.GameplayCamera : null;
        float camZ = cam != null ? cam.transform.position.z : 0f;
        children.Sort((a, b) =>
            Mathf.Abs(a.position.z - camZ).CompareTo(Mathf.Abs(b.position.z - camZ)));

        // Every child appears at the first step's Z (the spawn plane). The wait
        // before each later step is how long it takes to fly that step's Z gap, so
        // in flight each step trails the one before by exactly its authored gap.
        float spawnZ = children.Count > 0 ? children[0].position.z : 0f;

        float previousDistance = 0f;
        for (int i = 0; i < children.Count; i++)
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
            // are still under the pattern root and are removed with it below.
            if (StopSpawning) break;

            // Detach so each child moves on its own, then reveal it. Its OnEnable
            // records its start point (and, for asteroids, starts the warning blink).
            Vector3 p = children[i].position;
            children[i].SetParent(null, true);
            children[i].position = new Vector3(p.x, p.y, spawnZ);
            children[i].gameObject.SetActive(true);
        }

        // Only the empty root (plus any unrevealed children) is left.
        Destroy(pattern);
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
