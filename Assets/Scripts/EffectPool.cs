using System.Collections.Generic;
using UnityEngine;

// Reuses particle effects, and asteroids that don't belong to a pattern (a Splitter's pieces), instead of
// creating and destroying one each time. Call EffectPool.Play(prefab, position) from anywhere, like CameraShake.Shake.
[DisallowMultipleComponent]
public class EffectPool : MonoBehaviour
{
    [Tooltip("The SpawnableTemplate whose explosionPrefab and pickupEffectPrefab are prewarmed.")]
    [SerializeField] private SpawnableTemplate template;
    [Tooltip("Copies of each effect made at level start.")]
    [SerializeField] private int prewarmEffects = 5;

    private static EffectPool instance;
    private readonly Dictionary<ParticleSystem, Stack<PooledEffect>> freeEffects = new Dictionary<ParticleSystem, Stack<PooledEffect>>();
    private readonly Dictionary<GameObject, Stack<AsteroidBehavior>> freeAsteroids = new Dictionary<GameObject, Stack<AsteroidBehavior>>();

    private void Awake()
    {
        instance = this;

        // Make the template's effects now, so the first explosions don't stutter.
        if (template == null) return;
        Prewarm(template.explosionPrefab);
        Prewarm(template.pickupEffectPrefab);
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    // Plays an effect at a position, reusing a free copy if there is one.
    // With no pool in the scene, it falls back to a plain Instantiate.
    public static void Play(ParticleSystem prefab, Vector3 position)
    {
        if (prefab == null) return;
        if (instance == null)
        {
            Instantiate(prefab, position, Quaternion.identity);
            return;
        }

        Stack<PooledEffect> free = instance.GetFreeList(prefab);
        PooledEffect effect = free.Count > 0 ? free.Pop() : instance.CreateEffect(prefab);
        effect.transform.SetPositionAndRotation(position, Quaternion.identity);
        effect.gameObject.SetActive(true);
        effect.Particles.Play(true);
    }

    // Gets a switched-off asteroid made from the prefab, reusing a free one if there is one, at the position.
    // The caller switches it on. With no pool in the scene, it falls back to a plain Instantiate.
    public static AsteroidBehavior GetAsteroid(GameObject prefab, Vector3 position)
    {
        AsteroidBehavior asteroid;
        if (instance == null)
        {
            asteroid = Instantiate(prefab).GetComponent<AsteroidBehavior>();
            asteroid.gameObject.SetActive(false);
        }
        else
        {
            Stack<AsteroidBehavior> free = instance.GetFreeAsteroids(prefab);
            asteroid = free.Count > 0 ? free.Pop() : instance.CreateAsteroid(prefab);
        }

        asteroid.transform.SetPositionAndRotation(position, prefab.transform.rotation);
        return asteroid;
    }

    // Makes free asteroids ahead of time, so the first splits don't stutter. Does nothing without a pool.
    public static void PrewarmAsteroids(GameObject prefab, int count)
    {
        if (instance == null || prefab == null) return;

        Stack<AsteroidBehavior> free = instance.GetFreeAsteroids(prefab);
        while (free.Count < count)
            free.Push(instance.CreateAsteroid(prefab));
    }

    // Takes a finished asteroid back if it's one of the pool's pieces: switches it off and puts it on its prefab's
    // pile. Returns false for anything else, so the caller can deal with it.
    public static bool ReturnAsteroid(GameObject obj)
    {
        AsteroidBehavior asteroid = obj.GetComponent<AsteroidBehavior>();
        if (instance == null || asteroid == null || asteroid.PoolSource == null) return false;

        if (obj.activeSelf)
        {
            obj.SetActive(false);
            instance.GetFreeAsteroids(asteroid.PoolSource).Push(asteroid);
        }
        return true;
    }

    private void Prewarm(ParticleSystem prefab)
    {
        if (prefab == null) return;
        Stack<PooledEffect> free = GetFreeList(prefab);
        while (free.Count < prewarmEffects)
            free.Push(CreateEffect(prefab));
    }

    // The free list for one effect prefab, made the first time it's needed.
    private Stack<PooledEffect> GetFreeList(ParticleSystem prefab)
    {
        if (!freeEffects.TryGetValue(prefab, out Stack<PooledEffect> list))
            freeEffects[prefab] = list = new Stack<PooledEffect>();
        return list;
    }

    // Makes a new, switched-off copy of an effect, kept under the pool's object.
    private PooledEffect CreateEffect(ParticleSystem prefab)
    {
        ParticleSystem system = Instantiate(prefab, transform);
        system.gameObject.SetActive(false);

        PooledEffect effect = system.gameObject.AddComponent<PooledEffect>();
        effect.Init(prefab);
        effect.OnFinished += HandleFinished;
        return effect;
    }

    // PooledEffect.OnFinished: the particles are done, so the copy waits for its next use.
    private void HandleFinished(PooledEffect effect) => GetFreeList(effect.Prefab).Push(effect);

    // The free pile for one asteroid prefab, made the first time it's needed.
    private Stack<AsteroidBehavior> GetFreeAsteroids(GameObject prefab)
    {
        if (!freeAsteroids.TryGetValue(prefab, out Stack<AsteroidBehavior> list))
            freeAsteroids[prefab] = list = new Stack<AsteroidBehavior>();
        return list;
    }

    // Makes a new, switched-off copy of an asteroid, kept under the pool's object, marked with the prefab it came from.
    private AsteroidBehavior CreateAsteroid(GameObject prefab)
    {
        AsteroidBehavior asteroid = Instantiate(prefab, transform).GetComponent<AsteroidBehavior>();
        asteroid.gameObject.SetActive(false);
        asteroid.PoolSource = prefab;
        return asteroid;
    }
}
