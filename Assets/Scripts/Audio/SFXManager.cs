using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Sound effect player. Put this on the same object as MusicManager (your AudioManager).
///
/// It only plays clips. GameSFX and UISFX hold the sounds (clips, volume, pitch) and call PlayClip.
/// It uses a small pool of AudioSources so effects can overlap and each one can have
/// its own random pitch without affecting the others.
/// </summary>
[DisallowMultipleComponent]
public class SFXManager : MonoBehaviour
{
    public static SFXManager Instance { get; private set; }

    [Header("Output")]
    [Range(0f, 1f)] [SerializeField] private float masterVolume = 1f;
    [Tooltip("How many effects can play at the same time.")]
    [SerializeField] private int voices = 8;
    [Tooltip("Optional: route effects through a mixer group (e.g. for an SFX volume slider).")]
    [SerializeField] private AudioMixerGroup sfxMixerGroup;

    private AudioSource[] pool;
    private int nextVoice;

    public float MasterVolume => masterVolume;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);   // remove only this component, not the whole AudioManager
            return;
        }
        Instance = this;

        // Build the voice pool
        pool = new AudioSource[Mathf.Max(1, voices)];
        for (int i = 0; i < pool.Length; i++)
        {
            var child = new GameObject("SFXVoice" + i);
            child.transform.SetParent(transform, false);
            var src = child.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = false;
            src.spatialBlend = 0f;   // 2D
            src.outputAudioMixerGroup = sfxMixerGroup;
            pool[i] = src;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // =====================================================
    //  PLAYING
    // =====================================================

    /// <summary>Play a clip on a free voice.</summary>
    public void PlayClip(AudioClip clip, float volume = 1f, float pitch = 1f)
    {
        if (clip == null) return;
        PlayOnVoice(clip, volume, pitch);
    }

    /// <summary>Cut off every effect that is currently playing.</summary>
    public void StopAll()
    {
        foreach (var src in pool) src.Stop();
    }

    /// <summary>For a volume slider (0-1). Affects effects played from now on.</summary>
    public void SetMasterVolume(float volume)
    {
        masterVolume = Mathf.Clamp01(volume);
    }

    private void PlayOnVoice(AudioClip clip, float volume, float pitch)
    {
        // Prefer a free voice; if all are busy, reuse the next one in rotation
        AudioSource chosen = null;
        for (int i = 0; i < pool.Length; i++)
        {
            int index = (nextVoice + i) % pool.Length;
            if (!pool[index].isPlaying)
            {
                chosen = pool[index];
                nextVoice = (index + 1) % pool.Length;
                break;
            }
        }
        if (chosen == null)
        {
            chosen = pool[nextVoice];
            nextVoice = (nextVoice + 1) % pool.Length;
        }

        chosen.Stop();
        chosen.clip = clip;
        chosen.pitch = pitch;
        chosen.volume = Mathf.Clamp01(volume * masterVolume);
        chosen.Play();
    }
}