using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform meshTransform;

    [Header("Movement")]
    [Tooltip("Orbit distance per pixel of mouse movement, in thousandths of a world unit. 5 = 0.005.")]
    [SerializeField] private float mouseSensitivity = 1.7f;

    [Header("Orbit")]
    // Radius, center, and depth come from OrbitSettings, shared with the pattern builder
    [SerializeField] private float startAngle = -90f; // degrees; -90 = bottom of the circle, like Gyruss
    [SerializeField] private bool faceCenter = true; // rotate so the ship's top points at the center

    [Header("Roll")]
    [Tooltip("Degrees the mesh rolls per pixel of mouse movement.")]
    [SerializeField] private float rotationSpeed = 1.7f;
    [SerializeField] private float rollDamping = 5f;

    [Header("Speed")]
    [Tooltip("Speed gained per pixel of upward mouse movement, in thousandths. 33 = 0.033.")]
    [SerializeField] private float acceleration = 83f;
    [SerializeField] private float speedDecay = 2f;
    [SerializeField] private float maxSpeed = 10f;
    [SerializeField] private float topSpeedDegrade = 3f;
    [SerializeField, Range(0.5f, 1f)] private float overdriveFraction = 0.9f;

    [Header("Boost")]
    [SerializeField] private float boostForce = 5f;
    [SerializeField] private float boostDecay = 3f;
    [SerializeField] private float speedBoost = 3f;
    [Tooltip("Seconds after the last sideways mouse movement that a click still gives a side boost.")]
    [SerializeField] private float sideInputMemory = 0.1f;

    [Header("Hit")]
    [SerializeField] private float invulnerableTime = 1.5f; // seconds of no hits after being hit
    [SerializeField] private float hitBlinkInterval = 0.1f; // time between visibility flips

    private const float SensitivityScale = 0.001f; // Mouse Sensitivity and Acceleration are entered in thousandths, so 5 means 0.005

    // Speed
    // Readable anywhere, but only PlayerMovement changes it; other scripts go
    // through methods like TakeHit so clamping stays in one place.
    public static float Speed { get; private set; }
    public static float MaxSpeed { get; private set; }
    public static float Speed01 => MaxSpeed > 0f ? Speed / MaxSpeed : 0f;

    // Movement
    private Vector2 moveInput;

    // Orbit
    private OrbitSettings orbit;
    private float orbitAngle; // radians, increasing = counter-clockwise on screen

    // Direction a positive boost/knockback pushes the player (counter-clockwise tangent)
    public Vector2 OrbitTangent => new Vector2(-Mathf.Sin(orbitAngle), Mathf.Cos(orbitAngle));

    // Orbit shape, for anything that needs to draw or reason about the circle.
    // Read live from the asset, so tuning it in Play mode takes effect straight away.
    public Vector2 OrbitCenter => orbit.OrbitCenter;
    public float CircleRadius => orbit.OrbitRadius;

    // Roll
    private Vector2 rollInput;

    // Boost
    private float boostVelocity;
    public event Action OnSpeedBoost;
    private float lastSideDirection;
    private float lastSideInputTime = float.NegativeInfinity; // no sideways input yet

    // Hit
    public bool IsInvulnerable => invulnerableTimer > 0f;
    public bool MeshVisible { get; private set; } = true;
    private float invulnerableTimer;
    private Renderer[] meshRenderers;

    // Level
    private LevelTimer levelTimer;
    private PlayerInvincibility invincibility;

    // Once the earth has arrived the player can only lose speed, not gain it.
    private bool CanAccelerate => levelTimer == null || !levelTimer.IsComplete;

    // Once the game is over or won, all input is ignored.
    private bool InputLocked => GameManager.Instance != null &&
                                (GameManager.Instance.IsGameOver || GameManager.Instance.HasWon);

    // Fraction of max speed where overdrive starts, set from overdriveFraction. SkyboxSpeedController reads it too.
    public static float OverdriveFraction { get; private set; }

    // From OverdriveFraction of max speed up, where the extra drain applies. RunStats times how long it's held.
    public bool InOverdrive => Speed >= maxSpeed * OverdriveFraction;

    void Awake()
    {
        // Statics survive scene reloads, so reset them for a fresh run.
        Speed = 0f;
        MaxSpeed = maxSpeed;
        OverdriveFraction = overdriveFraction;

        // Depth comes from OrbitSettings so it always matches the pattern builder.
        // Done in Awake so other scripts' Start already sees the right z.
        orbit = OrbitSettings.Instance;
        Vector3 p = transform.position;
        transform.position = new Vector3(p.x, p.y, orbit.PlayerZ);

        // Blink only the visual mesh; the collider stays active.
        meshRenderers = meshTransform != null
            ? meshTransform.GetComponentsInChildren<Renderer>()
            : GetComponentsInChildren<Renderer>();

        invincibility = GetComponent<PlayerInvincibility>();
    }

    void Start()
    {
        // Lock the cursor to the center of the screen and make it invisible
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        levelTimer = GameManager.Instance != null ? GameManager.Instance.LevelTimer : null;

        orbitAngle = startAngle * Mathf.Deg2Rad;
    }

    void Update()
    {
        // Once the game is over or won, stop here. On a death, PlayerDeath moves the ship instead.
        if (InputLocked) return;

        Roll();
        UpdateSpeed();
        Movement();
        UpdateInvulnerability();
    }

    private void Roll()
    {
        // If there is mouse input, roll with its speed (pixels per second); otherwise ease back to zero.
        // The fade uses 1 - e^(-k·dt) so it eases out the same at any frame rate.
        rollInput = moveInput.sqrMagnitude > 0f && Time.deltaTime > 0f
            ? moveInput / Time.deltaTime
            : Vector2.Lerp(rollInput, Vector2.zero, 1f - Mathf.Exp(-rollDamping * Time.deltaTime));

        float degrees = rotationSpeed * Time.deltaTime;

        // Rotate only the visual mesh, not the collider/root
        // Pitch around the orbit tangent (root's local x) so forward input always tilts the nose toward the center
        meshTransform.Rotate(transform.right, rollInput.y * degrees, Space.World);

        // Roll around the mesh's own axis as the player moves around the circle
        meshTransform.Rotate(0f, 0f, -rollInput.x * degrees, Space.Self);
    }

    private void Movement()
    {
        // Distance travelled along the circle this frame; positive = counter-clockwise
        // The mouse delta is already distance per frame, so it isn't scaled by time; the boost is a speed, so it is.
        // Distance divided by radius = angle in radians, so we can add it to the orbitAngle directly.
        float arc = (moveInput.x * mouseSensitivity * SensitivityScale) + (boostVelocity * Time.deltaTime);
        orbitAngle += arc / CircleRadius;

        // Offset is defined by the cosine (left/right) and sine (up/down) of the angle, multiplied by the radius to get world units
        Vector2 offset = new Vector2(Mathf.Cos(orbitAngle), Mathf.Sin(orbitAngle)) * CircleRadius;
        Vector2 center = OrbitCenter;
        transform.position = new Vector3(center.x + offset.x, center.y + offset.y, transform.position.z);

        if (faceCenter)
            transform.rotation = Quaternion.Euler(0f, 0f, orbitAngle * Mathf.Rad2Deg + 90f);

        // Boost fades out over time instead of cutting off instantly. 1 - e^(-k·dt) fades the same at any frame rate.
        boostVelocity = Mathf.Lerp(boostVelocity, 0f, 1f - Mathf.Exp(-boostDecay * Time.deltaTime));
    }

    private void UpdateSpeed()
    {
        bool nearMaxSpeed = InOverdrive;

        // Forward input after the level ends counts as no input, so the normal
        // decay still applies instead of speed being held in place.
        bool hasInput = moveInput.y < 0f || (moveInput.y > 0f && CanAccelerate);

        if (hasInput)
        {
            // Speeds up on positive mouse y, slows down on negative. The mouse delta is already per frame, so no × time.
            Speed += moveInput.y * acceleration * SensitivityScale;
        }
        else
        {
            // No input: decay at a flat rate toward zero
            Speed -= speedDecay * Time.deltaTime;
        }

        if (nearMaxSpeed)
        {
            // Extra drain that grows across the overdrive band: none at its start, full topSpeedDegrade at max.
            // Easy to stay in overdrive, harder to stay at max.
            float overdriveDepth = Mathf.InverseLerp(OverdriveFraction, 1f, Speed01);
            Speed -= topSpeedDegrade * overdriveDepth * Time.deltaTime;
        }

        Speed = Mathf.Clamp(Speed, 0f, maxSpeed);

        // Invincibility holds the player at max speed, unless the level has ended.
        if (CanAccelerate && invincibility != null && invincibility.LocksMaxSpeed)
            Speed = maxSpeed;
    }

    private void OnMove(InputValue value)
    {
        moveInput = value.Get<Vector2>();

        if (moveInput.x != 0f)
        {
            lastSideDirection = Mathf.Sign(moveInput.x);
            lastSideInputTime = Time.time;
        }
    }

    public bool TakeHit(float knockbackVelocity, float speedLoss)
    {
        // Hits during the invulnerability window are ignored entirely.
        if (IsInvulnerable) return false;

        // Reuses the boost velocity so the push travels along the circle and
        // decays the same way a boost does.
        boostVelocity = knockbackVelocity;
        Speed = Mathf.Clamp(Speed - speedLoss, 0f, maxSpeed);

        invulnerableTimer = invulnerableTime;
        return true;
    }

    private void UpdateInvulnerability()
    {
        if (invulnerableTimer <= 0f) return;

        invulnerableTimer -= Time.deltaTime;

        // Flip visibility every hitBlinkInterval, and always end visible.
        MeshVisible = invulnerableTimer <= 0f ||
                      Mathf.FloorToInt(invulnerableTimer / hitBlinkInterval) % 2 == 0;

        foreach (Renderer r in meshRenderers)
            r.enabled = MeshVisible;
    }

    private void OnButton(InputValue value)
    {
        if (!value.isPressed || InputLocked)
            return;

        if (Time.time - lastSideInputTime <= sideInputMemory)
        {
            // Boost around the circle in whichever direction the mouse was last moving horizontally
            boostVelocity = lastSideDirection * boostForce;
        }
        else if (CanAccelerate)
        {
            // No horizontal input: boost forward speed instead
            Speed = Mathf.Clamp(Speed + speedBoost, 0f, maxSpeed);
            OnSpeedBoost?.Invoke();
        }
    }
}
