using System.Collections;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

/// <summary>
/// Persistent music manager:
///  - Plays a looping title track on launch until a menu button is clicked.
///  - Plays a looping level track with live speed / pitch controls.
///
/// SETUP
///  1. Create an empty GameObject in your first (title) scene, name it "MusicManager",
///     and add this script.
///  2. Assign the Title Music and Level Music clips.
///  3. (Optional) Drag your UI buttons into "Start Buttons" / "Other Buttons",
///     or call StartGame() / StopTitleMusic() yourself from a Button's OnClick.
///  4. (Optional) For speed changes that DON'T alter musical pitch, see the
///     "Independent speed & pitch" note below.
///
/// USAGE FROM OTHER SCRIPTS
///  MusicManager.Instance.PlayLevelMusic();
///  MusicManager.Instance.SetSpeed(1.25f);
///  MusicManager.Instance.SetPitch(0.9f);
///  MusicManager.Instance.SpeedUpTo(1.5f, 10f);       // ramp over 10 seconds
///  MusicManager.Instance.StartContinuousSpeedUp(0.01f, 2f); // +0.01 per sec, max 2x
///  MusicManager.Instance.ResetSpeedAndPitch();
///
/// INDEPENDENT SPEED &amp; PITCH (optional)
///  Unity's AudioSource.pitch changes speed and pitch together (like a vinyl record).
///  To separate them:
///   - Create an AudioMixer, add a group (e.g. "LevelMusic") with a "Pitch Shifter" effect.
///   - Right-click the Pitch Shifter's "Pitch" parameter -> "Expose ... to script",
///     and rename the exposed parameter to "LevelPitchShift" (or change the field below).
///   - Assign the mixer to "Level Mixer" and the group to "Level Mixer Group".
///  With that set up: SetSpeed = tempo change, SetPitch = musical pitch change.
///  Without it, both values are multiplied and act on AudioSource.pitch.
/// </summary>
[DisallowMultipleComponent]
public class MusicManager : MonoBehaviour
{
    public static MusicManager Instance { get; private set; }

    [Header("Clips")]
    [SerializeField] private AudioClip titleMusic;
    [SerializeField] private AudioClip levelMusic;
    [SerializeField] private AudioClip creditsMusic;

    [Header("Volumes")]
    [Range(0f, 1f)] [SerializeField] private float titleVolume = 0.8f;
    [Range(0f, 1f)] [SerializeField] private float levelVolume = 0.7f;
    [Range(0f, 1f)] [SerializeField] private float creditsVolume = 1f;

    [Header("Behaviour")]
    [SerializeField] private bool playTitleOnStart = true;
    [SerializeField] private float titleFadeOutTime = 1.0f;
    [SerializeField] private float levelFadeInTime = 1.5f;
    [SerializeField] private float levelFadeOutTime = 1.0f;
    [SerializeField] private float creditsFadeInTime = 0.5f;
    [SerializeField] private float creditsFadeOutTime = 0.7f;
    [SerializeField] private float creditsStartDelay = 0.5f;
    private Coroutine creditsStartRoutine;

    [Header("Buttons (optional auto-wiring)")]
    [Tooltip("Stops title music AND starts level music.")]
    [SerializeField] private Button[] startButtons;
    [Tooltip("Only stops title music (e.g. Options, Quit, Credits).")]
    [SerializeField] private Button[] creditButtons;

    [Header("Speed / Pitch limits")]
    [SerializeField] private float minSpeed = 0.25f;
    [SerializeField] private float maxSpeed = 3f;
    [SerializeField] private float minPitch = 0.5f;
    [SerializeField] private float maxPitch = 2f;

    [Header("Independent speed & pitch (optional)")]
    [SerializeField] private AudioMixer levelMixer;
    [SerializeField] private AudioMixerGroup levelMixerGroup;
    [SerializeField] private string pitchShifterParam = "LevelPitchShift";

    private AudioSource titleSource;
    private AudioSource levelSource;
    private AudioSource creditsSource;

    private float speed = 1f;
    private float pitch = 1f;
    

    private Coroutine titleFadeRoutine;
    private Coroutine levelFadeRoutine;
    private Coroutine creditsFadeRoutine;
    private Coroutine speedRoutine;
    private Coroutine continuousSpeedRoutine;

    public bool GameStarted { get; private set; }
    private bool levelFadingOut;

    // ---------- Public read-only state ----------
    public float Speed => speed;
    public float Pitch => pitch;
    public bool IsTitlePlaying => titleSource != null && titleSource.isPlaying;
    public bool IsLevelPlaying => levelSource != null && levelSource.isPlaying;
    public bool IsFadingOut => levelFadingOut;

    // ---------- Unity lifecycle ----------
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        titleSource = CreateSource("TitleMusicSource", null);
        levelSource = CreateSource("LevelMusicSource", levelMixerGroup);
        creditsSource = CreateSource("CreditsMusicSource", null);

        titleSource.clip = titleMusic;
        levelSource.clip = levelMusic;
    }

    private void Start()
    {
        if (Instance != this) return;

        WireButtons(startButtons, StartGame);
        WireButtons(creditButtons, StartCredits);

        if (playTitleOnStart) PlayTitleMusic();
    }

    private void OnDestroy()
    {
        if (Instance != this) return;
        UnwireButtons(startButtons, StartGame);
        UnwireButtons(creditButtons, StartCredits);
        Instance = null;
    }

    private AudioSource CreateSource(string objName, AudioMixerGroup group)
    {
        var child = new GameObject(objName);
        child.transform.SetParent(transform, false);
        var src = child.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.loop = true;          // endless looping
        src.spatialBlend = 0f;    // 2D
        src.outputAudioMixerGroup = group;
        return src;
    }

    // ---------- Button helpers ----------
    private void WireButtons(Button[] buttons, UnityEngine.Events.UnityAction action)
    {
        if (buttons == null) return;
        foreach (var b in buttons)
            if (b != null) b.onClick.AddListener(action);
    }

    private void UnwireButtons(Button[] buttons, UnityEngine.Events.UnityAction action)
    {
        if (buttons == null) return;
        foreach (var b in buttons)
            if (b != null) b.onClick.RemoveListener(action);
    }

    // =====================================================
    //  TITLE MUSIC
    // =====================================================

    public void PlayTitleMusic()
    {
        GameStarted = false;
        if (titleMusic == null) return;
        if (titleSource.isPlaying) return;

        titleSource.clip = titleMusic;
        titleSource.volume = titleVolume;
        titleSource.Play();
    }

    /// <summary>Fades out and stops the title music (safe to call repeatedly).</summary>
    public void StopTitleMusic()
    {
        if (!titleSource.isPlaying) return;
        RestartRoutine(ref titleFadeRoutine, FadeOutAndStop(titleSource, titleFadeOutTime));
    }

    /// <summary>Hook this to your Credits button: stops title music and begins level music.</summary>
    public void StartGame()
    {
        GameStarted = true;
        StopTitleMusic();
        PlayLevelMusic();
    }

     // =====================================================
    //  CREDITS MUSIC
    // =====================================================

    public void PlayCreditsMusic()
    {
        if (creditsMusic == null) return;

        StopRoutine(ref creditsFadeRoutine);   // cancel any fade-out in progress
        StopRoutine(ref creditsStartRoutine);  // cancel any pending start

        if (creditsSource.isPlaying)
        {
            creditsSource.volume = creditsVolume;
            return;
        }
        creditsStartRoutine = StartCoroutine(StartCreditsAfterDelay());
    }

    private IEnumerator StartCreditsAfterDelay()
    {
        if (creditsStartDelay > 0f)
            yield return new WaitForSecondsRealtime(creditsStartDelay);

        creditsSource.clip = creditsMusic;
        creditsSource.volume = 0f;
        creditsSource.Play();

        yield return FadeVolume(creditsSource, creditsVolume, creditsFadeInTime);
        creditsStartRoutine = null;
    }

    public void StopCreditsMusic()
    {
        StopRoutine(ref creditsStartRoutine);  // also cancels a start that hasn't happened yet
        if (!creditsSource.isPlaying) return;
        RestartRoutine(ref creditsFadeRoutine, FadeOutAndStop(creditsSource, creditsFadeOutTime));
    }

    /// <summary>Hook this to your Credits button: stops title music and begins credits music.</summary>
    public void StartCredits()
    {
        StopTitleMusic();
        PlayCreditsMusic();
    }

    /// <summary>Stops the credits music and brings the title music back.</summary>
    public void ReturnToTitle()
    {
        StopCreditsMusic();
        PlayTitleMusic();
    }

    // =====================================================
    //  LEVEL MUSIC
    // =====================================================

    /// <summary>Starts the looping level track (fades in). Does nothing if already playing.</summary>
    public void PlayLevelMusic(bool fadeIn = true)
    {
        if (levelMusic == null) return;
        if (levelSource.isPlaying && !levelFadingOut) return;

        StopRoutine(ref levelFadeRoutine);
        levelSource.Stop();
        levelFadingOut = false;
        ResetSpeedAndPitch();

        levelSource.clip = levelMusic;
        levelSource.volume = fadeIn ? 0f : levelVolume;
        levelSource.Play();

        if (fadeIn)
            RestartRoutine(ref levelFadeRoutine, FadeVolume(levelSource, levelVolume, levelFadeInTime));
    }

    /// <summary>Swap to a different level track (e.g. when loading a new level).</summary>
    public void PlayLevelMusic(AudioClip newClip, bool restartIfSame = false)
    {
        if (newClip == null) return;
        if (levelSource.isPlaying && levelSource.clip == newClip && !restartIfSame) return;

        levelMusic = newClip;
        levelSource.Stop();
        PlayLevelMusic();
    }

    public void StopLevelMusic(bool fadeOut = true)
    {
        StopContinuousSpeedUp();
        if (!levelSource.isPlaying) return;

        if (fadeOut)
        {
            levelFadingOut = true;
            RestartRoutine(ref levelFadeRoutine, FadeOutAndStop(levelSource, levelFadeOutTime));
        }
        else
        {
            StopRoutine(ref levelFadeRoutine);
            levelSource.Stop();
        }
    }

    public void PauseLevelMusic()  => levelSource.Pause();
    public void ResumeLevelMusic() => levelSource.UnPause();

/// <summary>Death effect: speed and pitch wind down while the music fades out.</summary>
public void TrailOffAndStop(float duration = 1.5f, float endSpeed = 0.4f, float endPitch = 0.6f)
{
    StopContinuousSpeedUp();
    if (!levelSource.isPlaying) return;

    levelFadingOut = true;
    RestartRoutine(ref speedRoutine, TrailOff(duration, endSpeed, endPitch));
    RestartRoutine(ref levelFadeRoutine, FadeOutAndStop(levelSource, duration));
}

private IEnumerator TrailOff(float duration, float endSpeed, float endPitch)
{
    float startSpeed = speed, startPitch = pitch, t = 0f;
    endSpeed = Mathf.Clamp(endSpeed, minSpeed, maxSpeed);
    endPitch = Mathf.Clamp(endPitch, minPitch, maxPitch);
    duration = Mathf.Max(0.0001f, duration);

    while (t < duration)
    {
        t += Time.unscaledDeltaTime;
        float k = Mathf.SmoothStep(0f, 1f, t / duration);
        speed = Mathf.Lerp(startSpeed, endSpeed, k);
        pitch = Mathf.Lerp(startPitch, endPitch, k);
        ApplySpeedAndPitch();
        yield return null;
    }
    speedRoutine = null;
}
    public void SetLevelVolume(float volume)
    {
        levelVolume = Mathf.Clamp01(volume);
        StopRoutine(ref levelFadeRoutine);
        levelSource.volume = levelVolume;
    }

    // =====================================================
    //  SPEED & PITCH CONTROLS
    // =====================================================

    /// <summary>Set playback speed instantly (1 = normal).</summary>
    public void SetSpeed(float newSpeed)
    {
        StopRoutine(ref speedRoutine);
        speed = Mathf.Clamp(newSpeed, minSpeed, maxSpeed);
        ApplySpeedAndPitch();
    }

    /// <summary>Set musical pitch instantly (1 = normal).</summary>
    public void SetPitch(float newPitch)
    {
        pitch = Mathf.Clamp(newPitch, minPitch, maxPitch);
        ApplySpeedAndPitch();
    }

    /// <summary>Add (or subtract) from the current speed instantly.</summary>
    public void ChangeSpeedBy(float delta) => SetSpeed(speed + delta);

    /// <summary>Smoothly ramp speed to a target over a number of seconds.</summary>
    public void SpeedUpTo(float targetSpeed, float duration)
    {
        StopContinuousSpeedUp();
        RestartRoutine(ref speedRoutine, RampSpeed(Mathf.Clamp(targetSpeed, minSpeed, maxSpeed), duration));
    }

    /// <summary>
    /// Keeps the music gradually speeding up forever (until maxSpeedLimit is hit),
    /// e.g. rate 0.01 = +1% speed every second. Great for "it gets faster the longer you survive".
    /// </summary>
    public void StartContinuousSpeedUp(float ratePerSecond, float maxSpeedLimit)
    {
        StopRoutine(ref speedRoutine);
        RestartRoutine(ref continuousSpeedRoutine,
            ContinuousSpeedUp(ratePerSecond, Mathf.Clamp(maxSpeedLimit, minSpeed, maxSpeed)));
    }

    public void StopContinuousSpeedUp() => StopRoutine(ref continuousSpeedRoutine);

    public void ResetSpeedAndPitch()
    {
        StopRoutine(ref speedRoutine);
        StopContinuousSpeedUp();
        speed = 1f;
        pitch = 1f;
        ApplySpeedAndPitch();
    }

    private void ApplySpeedAndPitch()
    {
        if (levelSource == null) return;

        bool useMixer = levelMixer != null && levelMixerGroup != null;
        if (useMixer)
        {
            // Playback rate = speed (this also shifts pitch by 'speed'),
            // so the Pitch Shifter compensates: pitch / speed.
            levelSource.pitch = speed;
            float shifter = Mathf.Clamp(pitch / speed, 0.5f, 2f); // Unity's Pitch Shifter range
            levelMixer.SetFloat(pitchShifterParam, shifter);
        }
        else
        {
            // Coupled mode: speed and pitch both change the playback rate.
            levelSource.pitch = speed * pitch;
        }
    }

    // =====================================================
    //  COROUTINES (use unscaled time so they work while Time.timeScale = 0)
    // =====================================================

    private IEnumerator FadeVolume(AudioSource src, float target, float duration)
    {
        float start = src.volume;
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            src.volume = Mathf.Lerp(start, target, t / duration);
            yield return null;
        }
        src.volume = target;
    }

    private IEnumerator FadeOutAndStop(AudioSource src, float duration)
    {
        yield return FadeVolume(src, 0f, Mathf.Max(0.0001f, duration));
        src.Stop();
    }

    private IEnumerator RampSpeed(float target, float duration)
    {
        float start = speed;
        float t = 0f;
        duration = Mathf.Max(0.0001f, duration);
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            speed = Mathf.Lerp(start, target, t / duration);
            ApplySpeedAndPitch();
            yield return null;
        }
        speed = target;
        ApplySpeedAndPitch();
        speedRoutine = null;
    }

    private IEnumerator ContinuousSpeedUp(float rate, float limit)
    {
        while (speed < limit)
        {
            speed = Mathf.Min(limit, speed + rate * Time.unscaledDeltaTime);
            ApplySpeedAndPitch();
            yield return null;
        }
        continuousSpeedRoutine = null;
    }

    // ---------- Coroutine utilities ----------
    private void RestartRoutine(ref Coroutine handle, IEnumerator routine)
    {
        if (handle != null) StopCoroutine(handle);
        handle = StartCoroutine(routine);
    }

    private void StopRoutine(ref Coroutine handle)
    {
        if (handle == null) return;
        StopCoroutine(handle);
        handle = null;
    }
}