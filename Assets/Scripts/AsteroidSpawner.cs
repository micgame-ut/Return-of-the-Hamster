using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Spawns a list of wave patterns one after another until the earth arrives.
// Each pattern's children are revealed one Z step at a time, closest to the
// camera first; children that share the same Z appear together, so a row
// spawns as a row.
public class AsteroidSpawner : MonoBehaviour
{
    public enum WaveOrder { Sequential, Random }

    [Serializable]
    public class Wave
    {
        public GameObject pattern;
        public float spawnInterval = 0.5f; // seconds between each Z step in this pattern
        public float delayAfter = 2f;      // seconds to wait before the next wave starts
    }

    [SerializeField] private List<Wave> waves = new List<Wave>();
    [SerializeField] private WaveOrder order = WaveOrder.Sequential;
    [SerializeField] private bool loop = true;
    [SerializeField] private float startDelay = 1f; // seconds before the first wave

    private const float SameZTolerance = 0.01f;

    private LevelTimer levelTimer;

    // Once the earth has arrived, no more waves or asteroids are spawned.
    private bool LevelComplete => levelTimer != null && levelTimer.IsComplete;

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
                if (LevelComplete) yield break;
                if (wave.pattern == null) continue;

                // Wait for the whole pattern to finish revealing, then pause.
                yield return SpawnPattern(wave.pattern, wave.spawnInterval);
                yield return new WaitForSeconds(wave.delayAfter);
            }
        }
        while (loop && !LevelComplete);
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

    private IEnumerator SpawnPattern(GameObject prefab, float spawnInterval)
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

        float previousDistance = 0f;
        for (int i = 0; i < children.Count; i++)
        {
            float distance = Mathf.Abs(children[i].position.z - camZ);

            // Wait only when stepping to a further Z; equal Zs spawn together.
            if (i > 0 && distance - previousDistance > SameZTolerance)
                yield return new WaitForSeconds(spawnInterval);
            previousDistance = distance;

            // Earth arrived mid-pattern: stop revealing. The hidden children
            // are still under the pattern root and are removed with it below.
            if (LevelComplete) break;

            // Detach so each asteroid moves on its own, then reveal it. Its
            // OnEnable starts the warning blink and records its start point.
            children[i].SetParent(null, true);
            children[i].gameObject.SetActive(true);
        }

        // Only the empty root (plus any unrevealed children) is left.
        Destroy(pattern);
    }
}
