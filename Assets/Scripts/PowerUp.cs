using System.Collections.Generic;
using UnityEngine;

// A pickup that flies toward the camera like a Linear asteroid, using the
// same SpawnableTemplate settings. Touching the player (or a twin copy) counts the
// pickup and applies the effect for its type.
public class PowerUp : MonoBehaviour
{
    public enum PowerUpType { Health, Invincible, Twin }

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

    // Same formula as asteroids, so a power-up keeps its spacing inside a pattern.
    public float ApproachSpeed => speed + PlayerMovement.Speed * (template != null ? template.speedMultiplier : 0f);

    // Every revealed power-up, so GameManager can tell when the field is clear.
    public static readonly List<PowerUp> Active = new List<PowerUp>();

    private void Awake()
    {
        Camera gameCam = GameManager.Instance != null ? GameManager.Instance.GameplayCamera : null;
        cam = gameCam != null ? gameCam.transform : null;
        player = GameManager.Instance != null ? GameManager.Instance.Player : null;
        health = GameManager.Instance != null ? GameManager.Instance.PlayerHealth : null;
        invincibility = GameManager.Instance != null ? GameManager.Instance.PlayerInvincibility : null;
        twins = GameManager.Instance != null ? GameManager.Instance.PlayerTwins : null;
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

    private void ApplyEffect()
    {
        switch (type)
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

        ApplyEffect();

        if (template != null)
        {
            EffectPool.Play(template.pickupEffectPrefab, transform.position);

            if (template.pickupShakeDuration > 0f)
                CameraShake.Shake(template.pickupShakeDuration, template.pickupShakeMagnitude);
        }

        PatternCopy.Despawn(gameObject);
    }
}
