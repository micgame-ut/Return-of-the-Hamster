using UnityEngine;

// Shared settings for everything the spawner sends at the player (asteroids
// and power-ups). Create one asset and assign it to every such prefab so these
// values can be changed in one place.
[CreateAssetMenu(fileName = "SpawnableTemplate", menuName = "Return of the Hamster/Spawnable Template")]
public class SpawnableTemplate : ScriptableObject
{
    [Header("Warning Timing")]
    [Tooltip("Seconds an asteroid waits (collider off) before it starts moving. A power-up waits the same time.")]
    public float warningTime = 1f;

    [Header("Spin")]
    [Tooltip("How fast asteroids tumble, in degrees per second.")]
    public float spinSpeed = 45f;

    [Header("Destruction")]
    [Tooltip("Particle effect spawned when the player smashes an asteroid.")]
    public ParticleSystem explosionPrefab;
    [Tooltip("Seconds the camera shakes on a smash.")]
    public float shakeDuration = 0.2f;
    [Tooltip("How far the camera shakes on a smash, in world units.")]
    public float shakeMagnitude = 0.3f;

    [Header("Destructible Outline")]
    [Tooltip("Material added to destructible asteroids to draw the outline.")]
    public Material outlineMaterial;
    [Tooltip("Outline color when the player is too slow to smash it.")]
    [ColorUsage(false, true)] public Color outlineTooSlow = Color.red;
    [Tooltip("Outline color when the player is fast enough to smash it.")]
    [ColorUsage(false, true)] public Color outlineCanBreak = Color.green;

    [Header("Phase")]
    [Tooltip("The hologram look a Phase asteroid swaps to while phased (and during its flicker).")]
    public Material phasedMaterial;

    [Header("Despawn")]
    [Tooltip("How far past the camera an asteroid goes before it's destroyed, in world units.")]
    public float despawnBehindCamera = 5f;

    [Header("Player Hit")]
    [Tooltip("How hard the player is pushed along their circle when hit.")]
    public float knockbackForce = 4f;
    [Tooltip("How much the player's Speed drops when hit.")]
    public float speedLoss = 3f;
    [Tooltip("Seconds the camera shakes on a hit.")]
    public float hitShakeDuration = 0.15f;
    [Tooltip("How far the camera shakes on a hit, in world units.")]
    public float hitShakeMagnitude = 0.2f;

    [Header("Player Speed")]
    [Tooltip("Approach speed (units/sec) at player Speed 0. Shared by asteroids and power-ups, so one change tunes them all.")]
    public float baseApproachSpeed = 5f;
    [Tooltip("Extra approach speed (units/sec) for each point of the player's Speed. Power-ups use it too, so they keep their place in a pattern.")]
    public float speedMultiplier = 15f;

    [Header("Depth Fade")]
    [Tooltip("Where the fade begins, as a share of the distance from the player to the spawner. 0.2 = 20% of the way out.")]
    [Range(0f, 1f)] public float darkenStart = 0.2f;
    [Tooltip("Where the fade is complete, as a share of the distance from the player to the spawner. 1 = at the spawner.")]
    [Range(0f, 1f)] public float darkenEnd = 1f;
    [Tooltip("The color far asteroids and power-ups fade toward. Multiplied into their base color, so a dark color darkens them.")]
    public Color farColor = new Color(0.25f, 0.25f, 0.3f, 1f);

    [Header("Power-Up")]
    [Tooltip("How fast power-ups spin around the world's up axis, in degrees per second. Separate from the asteroids' spinSpeed.")]
    public float powerUpSpinSpeed = 90f;
    [Tooltip("Particle effect spawned when a power-up is picked up.")]
    public ParticleSystem pickupEffectPrefab;
    [Tooltip("Seconds the camera shakes on a pickup. 0 means no shake.")]
    public float pickupShakeDuration = 0f;
    [Tooltip("How far the camera shakes on a pickup, in world units.")]
    public float pickupShakeMagnitude = 0f;
    [Tooltip("Seconds of invincibility from an Invincible power-up.")]
    public float invincibleDuration = 5f;

    [Header("Random Power-Up Chances")]          // shares of the total, so 40/20/20/20 = 40%, 20%, 20%, 20%
    [Tooltip("How often a Random power-up gives Health. The four chances are shares of their total.")]
    public float healthChance = 40f;
    [Tooltip("How often a Random power-up gives Invincible.")]
    public float invincibleChance = 20f;
    [Tooltip("How often a Random power-up gives Twin.")]
    public float twinChance = 20f;
    [Tooltip("How often a Random power-up gives Score Multiplier.")]
    public float scoreChance = 20f;

    // How much to multiply a color by to fade it with depth. White = no change, farColor = fully faded.
    public Color GetDepthTint(float distanceAhead)
    {
        float t = Mathf.InverseLerp(darkenStart, darkenEnd, distanceAhead / OrbitSettings.Instance.SpawnDistance);
        return Color.Lerp(Color.white, farColor, t);
    }
}
