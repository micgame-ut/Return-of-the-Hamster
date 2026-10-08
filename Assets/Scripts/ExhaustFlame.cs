using UnityEngine;

// Exhaust flame behind the player: longer, faster and narrower with speed,
// spurts on a forward boost, rainbow while invincible.
public class ExhaustFlame : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ParticleSystem flame;

    [Header("Speed Scaling")]
    [SerializeField] private float minLength = 0.5f;   // at Speed01 = 0
    [SerializeField] private float maxLength = 2.5f;   // at Speed01 = 1
    [SerializeField] private float minWidth = 0.2f;    // at Speed01 = 1 (narrowest)
    [SerializeField] private float maxWidth = 0.5f;    // at Speed01 = 0 (widest)
    [SerializeField] private float flameLifetime = 0.2f;
    [SerializeField] private float minEmission = 20f;
    [SerializeField] private float maxEmission = 80f;

    [Header("Sputter")]
    [SerializeField, Range(0f, 1f)] private float flickerAmount = 0.15f;
    [SerializeField] private float flickerSpeed = 10f;

    [Header("Boost")]
    [SerializeField] private int boostBurstCount = 30;
    [SerializeField] private float boostSizeMultiplier = 2f;

    [Header("Colors")]
    [SerializeField] private Gradient normalColor;
    [SerializeField] private Gradient rainbowColor;

    private PlayerMovement movement;
    private PlayerInvincibility invincibility;
    private ParticleSystemRenderer flameRenderer;
    private ParticleSystem.MainModule main;
    private ParticleSystem.EmissionModule emission;
    private ParticleSystem.ColorOverLifetimeModule colorOverLifetime;
    private bool stopped;

    private void Awake()
    {
        movement = GetComponentInParent<PlayerMovement>();
        invincibility = GetComponentInParent<PlayerInvincibility>();
        flameRenderer = flame.GetComponent<ParticleSystemRenderer>();

        main = flame.main;
        emission = flame.emission;
        colorOverLifetime = flame.colorOverLifetime;
        colorOverLifetime.enabled = true;
        colorOverLifetime.color = normalColor;
    }

    private void OnEnable()
    {
        if (movement != null) movement.OnSpeedBoost += HandleSpeedBoost;
        if (invincibility != null) invincibility.OnRainbowChanged += HandleRainbowChanged;
    }

    private void OnDisable()
    {
        if (movement != null) movement.OnSpeedBoost -= HandleSpeedBoost;
        if (invincibility != null) invincibility.OnRainbowChanged -= HandleRainbowChanged;
    }

    private void Update()
    {
        GameManager gm = GameManager.Instance;
        if (gm != null && (gm.IsGameOver || gm.HasWon))
        {
            if (!stopped)
            {
                flame.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                stopped = true;
            }
            return;
        }

        flameRenderer.enabled = movement.MeshVisible;

        float t = PlayerMovement.Speed01;
        float length = Mathf.Lerp(minLength, maxLength, t);
        float width = Mathf.Lerp(maxWidth, minWidth, t);

        float noise = Mathf.PerlinNoise(Time.time * flickerSpeed, 0f) * 2f - 1f;
        width *= 1f + noise * flickerAmount;

        main.startLifetime = flameLifetime;
        main.startSpeed = length / flameLifetime;
        main.startSize = width;
        emission.rateOverTime = Mathf.Lerp(minEmission, maxEmission, t);
    }

    private void HandleSpeedBoost()
    {
        ParticleSystem.EmitParams burst = new ParticleSystem.EmitParams();
        burst.startSize = main.startSize.constant * boostSizeMultiplier;
        flame.Emit(burst, boostBurstCount);
    }

    private void HandleRainbowChanged(bool on)
    {
        colorOverLifetime.color = on ? rainbowColor : normalColor;
    }
}
