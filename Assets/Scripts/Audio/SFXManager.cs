using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Sound effect player. Put this on the same object as MusicManager (your AudioManager).
///
/// SETUP
///  1. Add this component to the AudioManager object.
///  2. In "Sounds", add one entry per effect: give it an ID (e.g. "click"),
///     drag in one or more clips (it picks a random one each time),
///     and optionally set volume and a random pitch range.
///  3. Play from code:  SFXManager.Instance.Play("click");
///     or add a ButtonSFX component to any UI button.
///
/// It uses a small pool of AudioSources so effects can overlap and each one can have
/// its own random pitch without affecting the others.
/// </summary>
[DisallowMultipleComponent]
public class SFXManager : MonoBehaviour
{
    public static SFXManager Instance { get; private set; }

    [System.Serializable]
    public class SFXEntry
    {
        public string id;
        [Tooltip("If you add several clips, one is picked at random each time.")]
        public AudioClip[] clips;
        [Range(0f, 1f)] public float volume = 1f;
        [Tooltip("Random pitch between X and Y each play. Use 1 and 1 for no variation.")]
        public Vector2 pitchRange = new Vector2(1f, 1f);

        [System.NonSerialized] public int lastIndex = -1;
    }

    [Header("Sounds")]
    [SerializeField] private SFXEntry[] sounds;

    [Header("Output")]
    [Range(0f, 1f)] [SerializeField] private float masterVolume = 1f;
    [Tooltip("How many effects can play at the same time.")]
    [SerializeField] private int voices = 8;
    [Tooltip("Optional: route effects through a mixer group (e.g. for an SFX volume slider).")]
    [SerializeField] private AudioMixerGroup sfxMixerGroup;

    private readonly Dictionary<string, SFXEntry> lookup = new Dictionary<string, SFXEntry>();
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

        // Build the ID lookup
        foreach (var s in sounds)
        {
            if (s == null || string.IsNullOrEmpty(s.id)) continue;
            if (lookup.ContainsKey(s.id))
            {
                Debug.LogWarning($"SFXManager: duplicate sound ID '{s.id}' (only the first is used).", this);
                continue;
            }
            lookup.Add(s.id, s);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // =====================================================
    //  PLAYING
    // =====================================================

    /// <summary>Play a named effect from the Sounds list.</summary>
    public void Play(string id, float volumeScale = 1f)
    {
        if (string.IsNullOrEmpty(id)) return;

        if (!lookup.TryGetValue(id, out var entry))
        {
            Debug.LogWarning($"SFXManager: no sound with ID '{id}'.", this);
            return;
        }
        if (entry.clips == null || entry.clips.Length == 0) return;

        int index = Random.Range(0, entry.clips.Length);

        if (entry.clips.Length > 1 && index == entry.lastIndex)
            index = (index + 1 + Random.Range(0, entry.clips.Length - 1)) % entry.clips.Length;
        entry.lastIndex = index;
        var clip = entry.clips[index];
        
        if (clip == null) return;

        float pitch = Random.Range(entry.pitchRange.x, entry.pitchRange.y);
        PlayOnVoice(clip, entry.volume * volumeScale, pitch);
    }

    /// <summary>Play any clip directly, without a Sounds entry.</summary>
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