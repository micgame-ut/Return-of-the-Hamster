using UnityEngine;

// Shared settings for asteroids. Create one asset and assign it to every
// asteroid prefab so these values can be changed in one place.
[CreateAssetMenu(fileName = "AsteroidTemplate", menuName = "Return of the Hamster/Asteroid Template")]
public class AsteroidTemplate : ScriptableObject
{
    [Header("Warning Glow")]
    [ColorUsage(false, true)] public Color linearGlow = Color.red;
    [ColorUsage(false, true)] public Color waveGlow = Color.cyan;
    [ColorUsage(false, true)] public Color spiralGlow = Color.magenta;
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

    public Color GetGlow(AsteroidBehavior.MovementType type) => type switch
    {
        AsteroidBehavior.MovementType.Wave => waveGlow,
        AsteroidBehavior.MovementType.Spiral => spiralGlow,
        // Linear and any other unhandled types use the linear color.
        _ => linearGlow,
    } * glowIntensity;
}
