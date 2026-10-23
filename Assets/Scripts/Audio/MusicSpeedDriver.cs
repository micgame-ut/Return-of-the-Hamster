using System;
using UnityEngine;

/// <summary>
/// Drives MusicManager's speed AND pitch together from the player's speed.
///
/// HOOK-UP (one line in your player/movement script, every frame):
///     [SerializeField] private MusicSpeedDriver musicDriver;
///     ...
///     musicDriver.ReportSpeed(currentSpeed);   // positive = forward, 0 = idle, negative = backward
///
/// Behaviour:
///  - Moving forward  -> music speeds up and pitches up (rises at "Rise Rate").
///  - Idle / slowing  -> music eases back down (falls at "Fall Rate").
///  - Going backwards -> music drops faster (falls at "Reverse Fall Rate").
///  - Reaching the overdrive threshold sets InOverdrive and fires OverdriveChanged.
///
/// Don't combine with MusicManager.StartContinuousSpeedUp / SpeedUpTo, since both would fight over the speed.
/// </summary>
public class MusicSpeedDriver : MonoBehaviour
{
    [Header("Player speed mapping")]
    [Tooltip("Player speed at which the music reaches its maximum (your overdrive speed).")]
    [SerializeField] private float overdriveSpeed = 30f;
    [Tooltip("Shapes the response. X = player speed / overdrive speed, Y = music intensity. " +
             "Your speed grows exponentially, so a curve that starts steep and flattens can feel more even.")]
    [SerializeField] private AnimationCurve response = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    [Header("Music range (idle -> overdrive)")]
    [SerializeField] private float idleSpeed = 1f;
    [SerializeField] private float overdriveMusicSpeed = 1.4f;
    [SerializeField] private float idlePitch = 1f;
    [SerializeField] private float overdriveMusicPitch = 1.15f;

    [Header("Smoothing (intensity change per second, 0-1 scale)")]
    [SerializeField] private float riseRate = 0.5f;
    [SerializeField] private float fallRate = 0.25f;
    [SerializeField] private float reverseFallRate = 1.0f;

    [Header("Overdrive")]
    [Range(0f, 1f)] [SerializeField] private float overdriveThreshold = 0.95f;

    public bool InOverdrive { get; private set; }
    public float Intensity => intensity;
    public event Action<bool> OverdriveChanged;

    private float reportedSpeed;
    private float intensity;

    /// <summary>Call every frame from your movement code. Negative values mean pedaling backwards.</summary>
    public void ReportSpeed(float signedSpeed) => reportedSpeed = signedSpeed;

    private void Update()
    {
        reportedSpeed = PlayerMovement.Speed01;   // add this line

        var music = MusicManager.Instance;
        if (music == null) return;
        if (music.IsFadingOut) return;

        // Idle and backward movement both map to 0 intensity
        float normalized = Mathf.Clamp01(reportedSpeed / Mathf.Max(0.0001f, overdriveSpeed));
        float target = Mathf.Clamp01(response.Evaluate(normalized));

        bool reversing = reportedSpeed < -0.01f;
        float rate = target > intensity ? riseRate : (reversing ? reverseFallRate : fallRate);
        intensity = Mathf.MoveTowards(intensity, target, rate * Time.unscaledDeltaTime);

        // Speed and pitch change together
        music.SetSpeed(Mathf.Lerp(idleSpeed, overdriveMusicSpeed, intensity));
        music.SetPitch(Mathf.Lerp(idlePitch, overdriveMusicPitch, intensity));

        bool overdrive = intensity >= overdriveThreshold;
        if (overdrive != InOverdrive)
        {
            InOverdrive = overdrive;
            OverdriveChanged?.Invoke(InOverdrive);
        }
    }
}