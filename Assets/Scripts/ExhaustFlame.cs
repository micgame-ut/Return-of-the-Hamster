using UnityEngine;

// Exhaust flame behind the player: a glowing line that gets longer and narrower
// with speed, stretches on a forward boost, drags behind as the player moves
// around the orbit, and turns rainbow while invincible.
public class ExhaustFlame : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The outer flame's LineRenderer.")]
    [SerializeField] private LineRenderer core;
    [Tooltip("The inner core's LineRenderer.")]
    [SerializeField] private LineRenderer innerCore;

    [Header("Speed Scaling")]
    [Tooltip("Flame length at Speed01 0.")]
    [SerializeField] private float minLength = 0.5f;
    [Tooltip("Flame length at Speed01 1.")]
    [SerializeField] private float maxLength = 2.5f;
    [Tooltip("Head width at Speed01 1 (narrowest). Multiplies the Width curve.")]
    [SerializeField] private float minWidth = 0.2f;
    [Tooltip("Head width at Speed01 0 (widest). Multiplies the Width curve.")]
    [SerializeField] private float maxWidth = 0.5f;

    [Header("Sputter")]
    [Tooltip("How much the width wobbles at Speed01 1 (steadiest), as a fraction of the width.")]
    [SerializeField, Range(0f, 1f)] private float minFlicker = 0.15f;
    [Tooltip("How much the width wobbles at Speed01 0 (most sputter), as a fraction of the width.")]
    [SerializeField, Range(0f, 1f)] private float maxFlicker = 0.4f;
    [Tooltip("How fast the wobble changes.")]
    [SerializeField] private float flickerSpeed = 10f;

    [Header("Boost")]
    [Tooltip("Length multiplier at the instant of a forward boost (1.5 = 50% longer).")]
    [SerializeField] private float boostLengthMultiplier = 1.5f;
    [Tooltip("Seconds for the stretch to ease back to normal.")]
    [SerializeField] private float boostDuration = 0.3f;

    [Header("Drag")]
    [Tooltip("Seconds the tail takes to catch up when the player moves. 0 = always straight.")]
    [SerializeField] private float dragIntensity = 0.1f;
    [Tooltip("Points along each line. More = a smoother curve.")]
    [SerializeField, Min(2)] private int dragSegments = 8;

    [Header("Inner Core")]
    [Tooltip("Inner core width, as a fraction of the outer flame's.")]
    [SerializeField, Range(0f, 1f)] private float innerWidthFraction = 0.35f;
    [Tooltip("Inner core length, as a fraction of the outer flame's.")]
    [SerializeField, Range(0f, 1f)] private float innerLengthFraction = 0.6f;
    [Tooltip("The inner core's gradient, head to tail. Never changes, even while invincible.")]
    [SerializeField, GradientUsage(true)] private Gradient innerColor;

    [Header("Colors")]
    [Tooltip("The outer flame's gradient, head to tail.")]
    [SerializeField, GradientUsage(true)] private Gradient normalColor;
    [Tooltip("The outer flame's gradient while the rainbow shows.")]
    [SerializeField, GradientUsage(true)] private Gradient rainbowColor;

    [Header("Start")]
    [Tooltip("Seconds for the flame to grow from nothing to its normal size once the game starts.")]
    [SerializeField] private float growTime = 0.6f;

    private PlayerMovement movement;
    private PlayerInvincibility invincibility;
    private bool coreVisible; // last value given to both lines' enabled
    private float boostTimer; // seconds left on the current stretch
    private Vector3[] dragPoints; // world position of each point, head first
    private Vector3[] innerPoints; // world position of each inner point, head first
    private bool onPlayer; // false on a twin copy, which follows the player through GameManager
    private bool snapNext; // set when the player jumps; the next Update places the points straight

    private void Awake()
    {
        movement = GetComponentInParent<PlayerMovement>();
        invincibility = GetComponentInParent<PlayerInvincibility>();
        onPlayer = movement != null;
        GameManager gm = GameManager.Instance;
        if (movement == null && gm != null) movement = gm.Player;
        if (invincibility == null && gm != null) invincibility = gm.PlayerInvincibility;
        coreVisible = core.enabled;

        core.positionCount = dragSegments;
        dragPoints = new Vector3[dragSegments];
        FollowLine(dragPoints, minLength, 1f, true);
        core.colorGradient = normalColor;

        innerCore.positionCount = dragSegments;
        innerPoints = new Vector3[dragSegments];
        FollowLine(innerPoints, minLength, innerLengthFraction, true);
        innerCore.colorGradient = innerColor;
        innerCore.enabled = coreVisible;
    }

    private void OnEnable()
    {
        if (movement != null) movement.OnSpeedBoost += HandleSpeedBoost;
        if (onPlayer) movement.OnOrbitShifted += HandleOrbitShifted;
        if (invincibility != null) invincibility.OnRainbowChanged += HandleRainbowChanged;
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameOver += HandleLevelEnded;
            GameManager.Instance.OnWin += HandleLevelEnded;
        }
    }

    private void OnDisable()
    {
        if (movement != null) movement.OnSpeedBoost -= HandleSpeedBoost;
        if (onPlayer) movement.OnOrbitShifted -= HandleOrbitShifted;
        if (invincibility != null) invincibility.OnRainbowChanged -= HandleRainbowChanged;
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameOver -= HandleLevelEnded;
            GameManager.Instance.OnWin -= HandleLevelEnded;
        }
    }

    private void Update()
    {
        // Hidden on the menu, then grows from nothing over growTime once the game starts.
        GameManager gm = GameManager.Instance;
        float grow = gm == null ? 1f : Mathf.SmoothStep(0f, 1f, gm.TimeSinceStart / Mathf.Max(growTime, 0.01f));

        bool visible = movement.MeshVisible && grow > 0f;
        if (visible != coreVisible)
        {
            core.enabled = visible;
            innerCore.enabled = visible;
            coreVisible = visible;
        }

        float stretch = 1f;
        if (boostTimer > 0f)
        {
            boostTimer -= Time.deltaTime;
            stretch = Mathf.Lerp(1f, boostLengthMultiplier, boostTimer / boostDuration);
        }

        float t = PlayerMovement.Speed01;
        float length = Mathf.Lerp(minLength, maxLength, t) * stretch * grow;
        float width = Mathf.Lerp(maxWidth, minWidth, t) * grow;

        float noise = Mathf.PerlinNoise(Time.time * flickerSpeed, 0f) * 2f - 1f;
        width *= 1f + noise * Mathf.Lerp(maxFlicker, minFlicker, t);
        core.widthMultiplier = width;
        innerCore.widthMultiplier = width * innerWidthFraction;
        FollowLine(dragPoints, length, 1f, snapNext);
        core.SetPositions(dragPoints);
        FollowLine(innerPoints, length, innerLengthFraction, snapNext);
        innerCore.SetPositions(innerPoints);
        snapNext = false;
    }

    // Eases each point toward its spot on a straight line; points further back follow more lazily.
    private void FollowLine(Vector3[] points, float lineLength, float lengthFraction, bool snap)
    {
        for (int i = 0; i < points.Length; i++)
        {
            float f = i / (points.Length - 1f) * lengthFraction;
            Vector3 target = core.transform.TransformPoint(Vector3.forward * lineLength * f);
            float tau = dragIntensity * f;
            points[i] = snap || tau <= 0f ? target : Vector3.Lerp(points[i], target, 1f - Mathf.Exp(-Time.deltaTime / tau));
        }
    }

    private void HandleOrbitShifted() => snapNext = true;

    private void HandleSpeedBoost()
    {
        boostTimer = boostDuration;
    }

    private void HandleRainbowChanged(bool on)
    {
        core.colorGradient = on ? rainbowColor : normalColor;
    }

    private void HandleLevelEnded()
    {
        core.enabled = false;
        innerCore.enabled = false;
        enabled = false;
    }
}
