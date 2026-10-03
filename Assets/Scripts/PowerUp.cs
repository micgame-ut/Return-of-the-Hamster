using System.Collections.Generic;
using UnityEngine;

// A pickup that flies toward the camera like a Linear asteroid, using the
// same SpawnableTemplate settings. Touching the player counts the pickup and
// applies the effect for its type.
public class PowerUp : MonoBehaviour
{
    public enum PowerUpType { Health }

    [SerializeField] private PowerUpType type = PowerUpType.Health;
    [SerializeField] private float speed = 20f;
    [SerializeField] private SpawnableTemplate template;

    private Vector3 origin;
    private float distance;
    private Transform cam;

    // Same formula as asteroids, so a power-up keeps its spacing inside a pattern.
    public float ApproachSpeed => speed + PlayerMovement.Speed * (template != null ? template.speedMultiplier : 0f);

    // Every revealed power-up, so GameManager can tell when the field is clear.
    public static readonly List<PowerUp> Active = new List<PowerUp>();

    private void Awake()
    {
        Camera gameCam = GameManager.Instance != null ? GameManager.Instance.GameplayCamera : null;
        cam = gameCam != null ? gameCam.transform : null;
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
            Destroy(gameObject);
    }

    private void Heal(PlayerMovement player)
    {
        PlayerHealth health = player.GetComponent<PlayerHealth>();
        if (health != null) health.GainLife();
    }

    private void ApplyEffect(PlayerMovement player)
    {
        switch (type)
        {
            case PowerUpType.Health:
                Heal(player);
                break;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        PlayerMovement player = other.GetComponentInParent<PlayerMovement>();
        if (player == null) return;

        GameManager gm = GameManager.Instance;
        if (gm != null && gm.HasWon) return;

        RunStats stats = gm != null ? gm.RunStats : null;
        if (stats != null) stats.AddPowerUpPickedUp();

        ApplyEffect(player);

        if (template != null)
        {
            if (template.pickupEffectPrefab != null)
                Instantiate(template.pickupEffectPrefab, transform.position, Quaternion.identity);

            if (template.pickupShakeDuration > 0f)
                CameraShake.Shake(template.pickupShakeDuration, template.pickupShakeMagnitude);
        }

        Destroy(gameObject);
    }
}
