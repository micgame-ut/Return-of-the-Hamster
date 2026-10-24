using UnityEngine;

// Briefly jitters the camera. Call CameraShake.Shake() from anywhere.
// The offset is removed and reapplied each frame, rather than snapping back
// to a stored position, so it won't fight other scripts that move the camera.
[DisallowMultipleComponent]
public class CameraShake : MonoBehaviour
{
    private static CameraShake instance;

    [Tooltip("New shake directions per second. 60 matches how the shake looked at 60 fps. Lower = a slower, rougher rumble.")]
    [SerializeField, Min(1f)] private float shakeFrequency = 60f;

    private float duration;
    private float timeLeft;
    private float magnitude;
    private Vector3 lastOffset;
    private Vector3 shakeDirection;
    private float directionTimer;

    private void Awake() => instance = this;

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    public static void Shake(float duration, float magnitude)
    {
        if (instance == null) return;

        // Overlapping shakes restart the timer and keep the stronger magnitude,
        // so a small hit can't cut a big one short.
        instance.duration = duration;
        instance.timeLeft = duration;
        instance.magnitude = Mathf.Max(instance.magnitude, magnitude);
    }

    private void LateUpdate()
    {
        transform.localPosition -= lastOffset;
        lastOffset = Vector3.zero;

        if (timeLeft <= 0f) return;

        timeLeft -= Time.unscaledDeltaTime;

        // A new direction shakeFrequency times a second, not every frame, so the shake looks the same at any frame rate.
        directionTimer -= Time.unscaledDeltaTime;
        if (directionTimer <= 0f)
        {
            shakeDirection = Random.insideUnitSphere;
            directionTimer = 1f / shakeFrequency;
        }

        // Fade the shake out over its duration instead of stopping abruptly.
        float strength = magnitude * Mathf.Clamp01(timeLeft / duration);
        lastOffset = shakeDirection * strength;
        transform.localPosition += lastOffset;

        if (timeLeft <= 0f) magnitude = 0f;
    }
}
