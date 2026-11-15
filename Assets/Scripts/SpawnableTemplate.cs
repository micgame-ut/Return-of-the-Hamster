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

    [Header("Asteroid Look")]
    [Tooltip("Materials the ordinary asteroids pick from, one at random when each is first created. Frost, Magnet and Bonus asteroids keep their own. Empty = every asteroid keeps the material on its prefab.")]
    public Material[] asteroidMaterials;

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
    [Tooltip("Free boosts a Boost Recharge power-up gives. They skip the cooldown and are used up one at a time. Picking another up refills to this number, it doesn't add to it.")]
    public int rechargeBoosts = 2;
    [Tooltip("Seconds a Jackpot power-up lasts. While it does, every asteroid (except Frost and Magnet comets) can be smashed for its bonus points, at the speed in jackpotDestroySpeed.")]
    public float jackpotDuration = 8f;
    [Tooltip("The player Speed needed to smash any asteroid while Jackpot lasts. It replaces each asteroid's own smash speed, including the Destructible ones. Compare to maxSpeed (100).")]
    public float jackpotDestroySpeed = 50f;
    [Tooltip("The material every asteroid's body swaps to during Jackpot, normally the same Gold as the Bonus asteroids. Empty = the asteroids keep their own look. The depth fade still darkens it with distance.")]
    public Material jackpotMaterial;
    [Tooltip("Seconds before the end of Jackpot that the asteroids start to flicker between gold and their own color.")]
    public float jackpotWarningTime = 1f;
    [Tooltip("Seconds a Slim power-up lasts in total, from pickup until the player is back to full size. It includes the slow grow-back at the end.")]
    public float slimDuration = 6f;
    [Tooltip("How small the player and its twin copies get during Slim, as a multiple of normal size. 0.5 = half size. The hit area, boost ring and exhaust shrink with it.")]
    public float slimScale = 0.5f;
    [Tooltip("Seconds the player takes to shrink at the start of Slim.")]
    public float slimShrinkTime = 0.25f;
    [Tooltip("Seconds at the end of Slim during which the player grows slowly back to full size, so the end can be seen coming. Part of slimDuration.")]
    public float slimGrowTime = 1f;
    [Tooltip("Points for each asteroid a Bomb power-up removes. Flat: not multiplied by speed or power-ups, and it doesn't touch the combo.")]
    public float bombPoints = 50f;
    [Tooltip("Seconds a Bomb takes to go off across all the asteroids, nearest first, like a shockwave moving out. 0 = all at once. No asteroid goes off later than half the time it needs to reach the player.")]
    public float bombSweepTime = 0.2f;
    [Tooltip("Seconds the camera shakes when a Bomb goes off.")]
    public float bombShakeDuration = 0.4f;
    [Tooltip("How far the camera shakes when a Bomb goes off, in world units.")]
    public float bombShakeMagnitude = 0.5f;
    [Tooltip("Lance shots a Lance power-up gives. The lance stays active until they are all fired.")]
    public int lanceShots = 1;
    [Tooltip("The most lance shots the player can hold. A Recharge picked up while Lance is active adds its free boosts as shots, up to this.")]
    public int lanceMaxShots = 3;
    [Tooltip("Seconds between lance shots while there are more to fire. The ring charges during it.")]
    public float lanceCooldown = 0.6f;
    [Tooltip("Width of the lane a lance shot destroys, in world units, measured across the player's lane. About 3 rocks of a ring at the orbit radius.")]
    public float lanceWidth = 1.4f;
    [Tooltip("How far ahead of the player a lance shot reaches, in world units.")]
    public float lanceRange = 80f;
    [Tooltip("Points for each asteroid a lance shot hits. Flat: not multiplied by speed or power-ups, and it doesn't touch the combo.")]
    public float lancePoints = 50f;
    [Tooltip("Seconds the lance beam flash stays on screen before it fades out.")]
    public float lanceBeamTime = 0.2f;
    [Tooltip("The color of the lance beam's glow. HDR, so values above 1 shine with Bloom. The core is a lighter version of it.")]
    [ColorUsage(false, true)] public Color lanceBeamColor = new Color(3f, 0.35f, 1.6f, 1f);
    [Tooltip("Width of the lance beam's bright core, in world units. Thin, like a railgun. The lane it destroys is lanceWidth, which is wider.")]
    public float lanceBeamWidth = 0.3f;
    [Tooltip("Seconds of game time a Warp power-up skips ahead. The level, the rocks and the spawner run fast for this long while the player is gone.")]
    public float warpSeconds = 10f;
    [Tooltip("Seconds the player takes to pop out at the start of Warp. The game runs at normal speed during it.")]
    public float warpOutTime = 0.5f;
    [Tooltip("Real seconds the player stays gone. The game runs warpSeconds ÷ warpHoldTime times faster during it (10 seconds in 1 = 10 times).")]
    public float warpHoldTime = 1f;
    [Tooltip("Seconds the player takes to pop back in at the end of Warp. The game runs at normal speed during it.")]
    public float warpInTime = 0.5f;
    [Tooltip("Seconds after a Warp ends during which rocks can't hurt the player, and the ship wears the phase look (Phased Material). 0 = none.")]
    public float warpGraceTime = 1.5f;

    [Header("Random Power-Up Chances")]          // shares of the total, so 10/25/25 = 17%, 42%, 42%
    [Tooltip("How often a Random power-up gives Invincible. The chances are shares of their total.")]
    public float invincibleChance = 20f;
    [Tooltip("How often a Random power-up gives Twin.")]
    public float twinChance = 20f;
    [Tooltip("How often a Random power-up gives Score Multiplier.")]
    public float scoreChance = 20f;
    [Tooltip("How often a Random power-up gives Boost Recharge.")]
    public float rechargeChance = 20f;
    [Tooltip("How often a Random power-up gives Jackpot.")]
    public float jackpotChance = 20f;
    [Tooltip("How often a Random power-up gives Slim.")]
    public float slimChance = 20f;
    [Tooltip("How often a Random power-up gives Bomb.")]
    public float bombChance = 20f;
    [Tooltip("How often a Random power-up gives Lance.")]
    public float lanceChance = 20f;
    [Tooltip("How often a Random power-up gives Warp.")]
    public float warpChance = 20f;

    // How much to multiply a color by to fade it with depth. White = no change, farColor = fully faded.
    public Color GetDepthTint(float distanceAhead)
    {
        float t = Mathf.InverseLerp(darkenStart, darkenEnd, distanceAhead / OrbitSettings.Instance.SpawnDistance);
        return Color.Lerp(Color.white, farColor, t);
    }
}
