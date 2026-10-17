using System.Collections.Generic;
using UnityEngine;

// A pickup that flies toward the camera like a Linear asteroid, using the
// same SpawnableTemplate settings. Touching the player (or a twin copy) counts the
// pickup and applies the effect for its type.
public class PowerUp : MonoBehaviour
{
    public enum PowerUpType { Health, Invincible, Twin, ScoreMultiplier, Random }

    [SerializeField] private PowerUpType type = PowerUpType.Health;
    [SerializeField] private float speed = 20f;
    [SerializeField] private SpawnableTemplate template;

    private Vector3 origin;
    private float distance;
    private Transform cam;
    private PlayerMovement player;
    private PlayerHealth health;
    private PlayerInvincibility invincibility;
    private PlayerTwins twins;
    private Score score;

    // Same formula as asteroids, so a power-up keeps its spacing inside a pattern.
    public float ApproachSpeed => speed + PlayerMovement.Speed * (template != null ? template.speedMultiplier : 0f);

    // Every revealed power-up, so GameManager can tell when the field is clear.
    public static readonly List<PowerUp> Active = new List<PowerUp>();

    // Raised on every pickup with the type that was applied and where, so ScorePopups can show its label.
    public static event System.Action<PowerUpType, Vector3> OnCollected;

    // The types a Random power-up can roll, and each one's weight for the current roll (reused, so no allocation per pickup).
    private static readonly PowerUpType[] RollTypes =
        { PowerUpType.Health, PowerUpType.Invincible, PowerUpType.Twin, PowerUpType.ScoreMultiplier };
    private readonly float[] rollWeights = new float[4];

    private void Awake()
    {
        Camera gameCam = GameManager.Instance != null ? GameManager.Instance.GameplayCamera : null;
        cam = gameCam != null ? gameCam.transform : null;
        player = GameManager.Instance != null ? GameManager.Instance.Player : null;
        health = GameManager.Instance != null ? GameManager.Instance.PlayerHealth : null;
        invincibility = GameManager.Instance != null ? GameManager.Instance.PlayerInvincibility : null;
        twins = GameManager.Instance != null ? GameManager.Instance.PlayerTwins : null;
        score = GameManager.Instance != null ? GameManager.Instance.Score : null;
    }

    private void OnEnable()
    {
        Active.Add(this);
        origin = transform.position;
        distance = 0f;
    }

    private void OnDisable()
    {
        Active.Remove(this);
    }

    private void Update()
    {
        distance += ApproachSpeed * Time.deltaTime;
        transform.position = origin + Vector3.back * distance;

        if (template == null) return;

        transform.Rotate(Vector3.up, template.powerUpSpinSpeed * Time.deltaTime, Space.World);

        // Missed: once it's far enough behind the camera it can never be picked up.
        if (cam != null && transform.position.z < cam.position.z - template.despawnBehindCamera)
            PatternCopy.Despawn(gameObject);
    }

    private void Heal()
    {
        if (health != null && (twins == null || !twins.IsTwinned)) health.GainLife();
    }

    private void MakeInvincible()
    {
        if (invincibility != null && template != null) invincibility.Activate(template.invincibleDuration);
    }

    private void AddTwin()
    {
        if (twins != null) twins.AddBody();
    }

    private void AddScoreMultiplier()
    {
        if (score != null) score.AddMultiplierPowerUp();
    }

    private PowerUpType RollType()
    {
        float total = FillWeights(true);
        if (total <= 0f) total = FillWeights(false); // everything is wasted: roll from the plain chances instead

        float roll = Random.Range(0f, total);
        int last = 0;
        for (int i = 0; i < RollTypes.Length; i++)
        {
            if (rollWeights[i] <= 0f) continue;
            if (roll < rollWeights[i]) return RollTypes[i];
            roll -= rollWeights[i];
            last = i;
        }
        return RollTypes[last]; // the roll landed exactly on the total
    }

    // Fills rollWeights from the template's chances, with wasted types at 0 if skipWasted. Returns the total.
    private float FillWeights(bool skipWasted)
    {
        float total = 0f;
        for (int i = 0; i < RollTypes.Length; i++)
        {
            bool wasted = skipWasted && IsWasted(RollTypes[i]);
            rollWeights[i] = wasted ? 0f : Mathf.Max(0f, ChanceOf(RollTypes[i]));
            total += rollWeights[i];
        }
        return total;
    }

    private float ChanceOf(PowerUpType t) => t switch
    {
        PowerUpType.Health => template.healthChance,
        PowerUpType.Invincible => template.invincibleChance,
        PowerUpType.Twin => template.twinChance,
        _ => template.scoreChance,
    };

    // True when picking this type up right now would do nothing.
    private bool IsWasted(PowerUpType t) => t switch
    {
        PowerUpType.Health => health == null || health.CurrentLives >= health.MaxLives || (twins != null && twins.IsTwinned),
        PowerUpType.Twin => twins == null || !twins.CanAddBody,
        _ => false,
    };

    private void ApplyEffect(PowerUpType picked)
    {
        switch (picked)
        {
            case PowerUpType.Health:
                Heal();
                break;
            case PowerUpType.Invincible:
                MakeInvincible();
                break;
            case PowerUpType.Twin:
                AddTwin();
                break;
            case PowerUpType.ScoreMultiplier:
                AddScoreMultiplier();
                break;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // Only react to the player or a twin copy. FindBody also covers the
        // collider being on a child.
        if (player == null) return;
        Transform body = twins != null ? twins.FindBody(other.transform)
                       : other.transform.IsChildOf(player.transform) ? player.transform : null;
        if (body == null) return;

        GameManager gm = GameManager.Instance;
        if (gm != null && gm.HasWon) return;

        RunStats stats = gm != null ? gm.RunStats : null;
        if (stats != null) stats.AddPowerUpPickedUp();

        PowerUpType picked = type == PowerUpType.Random && template != null ? RollType() : type;
        ApplyEffect(picked);
        OnCollected?.Invoke(picked, transform.position);

        if (template != null)
        {
            EffectPool.Play(template.pickupEffectPrefab, transform.position);

            if (template.pickupShakeDuration > 0f)
                CameraShake.Shake(template.pickupShakeDuration, template.pickupShakeMagnitude);
        }

        PatternCopy.Despawn(gameObject);
    }
}
