using UnityEngine;

// Exhaust flame behind the player: a glowing line that gets longer and narrower
// with speed, stretches on a forward boost, drags behind as the player moves
// around the orbit, and turns rainbow while invincible.
public class ExhaustFlame : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private LineRenderer core;
    [SerializeField] private LineRenderer innerCore;

    [Header("Speed Scaling")]
    [SerializeField] private float minLength = 0.5f;   // at Speed01 = 0
    [SerializeField] private float maxLength = 2.5f;   // at Speed01 = 1
    [SerializeField] private float minWidth = 0.2f;    // at Speed01 = 1 (narrowest)
    [SerializeField] private float maxWidth = 0.5f;    // at Speed01 = 0 (widest)

    [Header("Sputter")]
    [SerializeField, Range(0f, 1f)] private float minFlicker = 0.15f; // at Speed01 = 1 (steadiest)
    [SerializeField, Range(0f, 1f)] private float maxFlicker = 0.4f;  // at Speed01 = 0 (most sputter)
    [SerializeField] private float flickerSpeed = 10f;

    [Header("Boost")]
    [SerializeField] private float boostLengthMultiplier = 1.5f; // line length at the instant of a boost (1.5 = 50% longer)
    [SerializeField] private float boostDuration = 0.3f;         // seconds for the stretch to ease back to normal

    [Header("Drag")]
    [SerializeField] private float dragIntensity = 0.1f;   // seconds the tail takes to catch up; 0 = straight line
    [SerializeField, Min(2)] private int dragSegments = 8; // points along the line; more = smoother curve

    [Header("Inner Core")]
    [SerializeField, Range(0f, 1f)] private float innerWidthFraction = 0.35f;  // of the outer line's width
    [SerializeField, Range(0f, 1f)] private float innerLengthFraction = 0.6f;  // of the outer line's length
    [SerializeField, GradientUsage(true)] private Gradient innerColor;         // stays the same while invincible

    [Header("Colors")]
    [SerializeField, GradientUsage(true)] private Gradient normalColor;
    [SerializeField, GradientUsage(true)] private Gradient rainbowColor;

    [Header("Start")]
    [SerializeField] private float growTime = 0.6f; // seconds for the flame to grow from nothing once the game starts

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
