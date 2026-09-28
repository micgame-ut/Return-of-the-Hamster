using UnityEngine;

// Shared settings for asteroids. Create one asset and assign it to every
// asteroid prefab so these values can be changed in one place.
[CreateAssetMenu(fileName = "AsteroidTemplate", menuName = "Return of the Hamster/Asteroid Template")]
public class AsteroidTemplate : ScriptableObject
{
    [Header("Warning Glow")]
    [ColorUsage(false, true)] public Color linearGlow = Color.red;
    [ColorUsage(false, true)] public Color waveGlow = Color.cyan;
    public float glowIntensity = 3f;

    [Header("Warning Timing")]
    public float warningTime = 1f;     // total blink duration before moving
    public float blinkInterval = 0.1f; // time between glow flips

    [Header("Spin")]
    public float spinSpeed = 45f; // degrees per second

    [Header("Destruction")]
    public ParticleSystem explosionPrefab;
    public float shakeDuration = 0.2f;  // seconds
    public float shakeMagnitude = 0.3f; // max camera offset in units

    [Header("Destructible Outline")]
    public Material outlineMaterial;
    [ColorUsage(false, true)] public Color outlineTooSlow = Color.red;
    [ColorUsage(false, true)] public Color outlineCanBreak = Color.green;

    [Header("Despawn")]
    public float despawnBehindCamera = 5f; // world units past the camera before removal

    [Header("Player Hit")]
    public float knockbackForce = 4f;      // sideways push when the player can't break it
    public float speedLoss = 3f;           // how much Speed drops on a hit
    public float hitShakeDuration = 0.15f; // seconds
    public float hitShakeMagnitude = 0.2f; // max camera offset in units

    [Header("Player Speed")]
    public float speedMultiplier = 15f; // extra units/sec of approach per point of player Speed

    public Color GetGlow(AsteroidBehavior.MovementType type) => type switch
    {
        AsteroidBehavior.MovementType.Wave => waveGlow,
        // Linear and any other unhandled types use the linear color.
        _ => linearGlow,
    } * glowIntensity;
}
