using System.Collections.Generic;
using UnityEngine;

// Reuses particle effects instead of creating and destroying one each time.
// Call EffectPool.Play(prefab, position) from anywhere, like CameraShake.Shake.
[DisallowMultipleComponent]
public class EffectPool : MonoBehaviour
{
    [SerializeField] private SpawnableTemplate template; // its explosion and pickup effects are prewarmed
    [SerializeField] private int prewarmEffects = 5;     // copies of each effect made at level start

    private static EffectPool instance;
    private readonly Dictionary<ParticleSystem, Stack<PooledEffect>> freeEffects = new Dictionary<ParticleSystem, Stack<PooledEffect>>();

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
}
