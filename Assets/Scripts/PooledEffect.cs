using System;
using UnityEngine;

// Sits on one pooled effect copy. When its particles finish, it switches the
// effect off and announces it, instead of the effect destroying itself.
public class PooledEffect : MonoBehaviour
{
    public ParticleSystem Prefab { get; private set; } // which effect this is a copy of
    public ParticleSystem Particles { get; private set; }
    public event Action<PooledEffect> OnFinished;      // the copy is free to reuse

    // Called once by EffectPool, right after it creates this copy.
    public void Init(ParticleSystem prefab)
    {
        Prefab = prefab;
        Particles = GetComponent<ParticleSystem>();

        // Callback instead of Destroy, so OnParticleSystemStopped runs when it finishes.
        ParticleSystem.MainModule main = Particles.main;
        main.stopAction = ParticleSystemStopAction.Callback;
    }

    // Unity calls this when the particles finish (Stop Action = Callback).
    private void OnParticleSystemStopped()
    {
        gameObject.SetActive(false);
        OnFinished?.Invoke(this);
    }
}
