using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The visual mesh (a child of the player). It rolls and blinks separately from the root and collider.")]
    [SerializeField] private Transform meshTransform;

    [Header("Movement")]
    [Tooltip("Orbit distance per pixel of mouse movement, in thousandths of a world unit. 5 = 0.005.")]
    [SerializeField] private float mouseSensitivity = 1.7f;
    [Tooltip("Degrees either side of straight up inside which sideways trackball movement is ignored. 0 = off.")]
    [SerializeField, Range(0f, 90f)] private float forwardThreshold = 30f;
    [Tooltip("Pixels per frame. Movement shorter than this is too small to have a reliable direction, so the threshold isn't applied.")]
    [SerializeField] private float minInputForAngle = 2f;

    [Header("Orbit")]
    // Radius, center, and depth come from OrbitSettings, shared with the pattern builder
    [Tooltip("Where on the circle the ship starts, in degrees. -90 = bottom, like Gyruss.")]
    [SerializeField] private float startAngle = -90f;
    [Tooltip("Rotates the ship so its top always points at the center of the circle.")]
    [SerializeField] private bool faceCenter = true;

    [Header("Roll")]
    [Tooltip("Degrees the mesh rolls per pixel of mouse movement.")]
    [SerializeField] private float rotationSpeed = 1.7f;
    [Tooltip("How fast the mesh settles back to level when input stops. Higher = faster.")]
    [SerializeField] private float rollDamping = 5f;

    [Header("Speed")]
    [Tooltip("Speed gained per pixel of upward mouse movement, in thousandths. 33 = 0.033.")]
    [SerializeField] private float acceleration = 83f;
    [Tooltip("Speed lost per second with no input.")]
    [SerializeField] private float speedDecay = 2f;
    [Tooltip("The top speed. The sky, starfield, level timer and asteroids all use Speed directly, so changing this shifts them too.")]
    [SerializeField] private float maxSpeed = 10f;
    [Tooltip("The most extra Speed lost per second in overdrive, reached at max speed. It ramps up from 0 at the start of overdrive.")]
    [SerializeField] private float topSpeedDegrade = 3f;
    [Tooltip("Fraction of max speed where overdrive starts. 0.9 = the top 10%.")]
    [SerializeField, Range(0.5f, 1f)] private float overdriveFraction = 0.9f;

    [Header("Boost")]
    [Tooltip("Sideways boost strength around the circle. The distance it covers is boostForce ÷ boostDecay.")]
    [SerializeField] private float boostForce = 5f;
    [Tooltip("How fast a sideways boost or knockback fades out, per second.")]
    [SerializeField] private float boostDecay = 3f;
    [Tooltip("How much Speed a forward boost adds.")]
    [SerializeField] private float speedBoost = 3f;
    [Tooltip("Seconds after the last sideways mouse movement that a click still gives a side boost.")]
    [SerializeField] private float sideInputMemory = 0.1f;
    [Tooltip("Seconds after a boost (side or forward) before the next one can be used.")]
    [SerializeField] private float boostCooldown = 3f;

    [Header("Hit")]
    [Tooltip("Seconds of no hits after being hit.")]
    [SerializeField] private float invulnerableTime = 1.5f;
    [Tooltip("Seconds between visibility flips while invulnerable.")]
    [SerializeField] private float hitBlinkInterval = 0.1f;

    private const float SensitivityScale = 0.001f; // Mouse Sensitivity and Acceleration are entered in thousandths, so 5 means 0.005

    // Speed
    // Readable anywhere, but only PlayerMovement changes it; other scripts go
    // through methods like TakeHit so clamping stays in one place.
    public static float Speed { get; private set; }
    public static float MaxSpeed { get; private set; }
    public static float Speed01 => MaxSpeed > 0f ? Speed / MaxSpeed : 0f;

    // Movement
    private Vector2 moveInput;
    private Vector2 rawMoveInput; // trackball input before the forward threshold

    // Orbit
    private OrbitSettings orbit;
    private float orbitAngle; // radians, increasing = counter-clockwise on screen
    private float pullRate; // radians per second of pull collected this frame from Magnet asteroids
    private float frostSpeedSlow; // strongest speed slow asked for this frame
    private float frostOrbitSlow; // strongest orbit slow asked for this frame

    // Direction a positive boost/knockback pushes the player (counter-clockwise tangent)
    public Vector2 OrbitTangent => new Vector2(-Mathf.Sin(orbitAngle), Mathf.Cos(orbitAngle));

    // Orbit shape, for anything that needs to draw or reason about the circle.
    // Read live from the asset, so tuning it in Play mode takes effect straight away.
    public Vector2 OrbitCenter => orbit.OrbitCenter;
    public float CircleRadius => orbit.OrbitRadius;

    // For TwinCopy: copies sit at an offset from this angle and copy the mesh's roll.
    public float OrbitAngle => orbitAngle;
    public Transform MeshTransform => meshTransform;

    // Where PlaceOnOrbit puts the player at startAngle, so the menu's glide can end exactly there.
    public Vector3 StartPosition => new Vector3(
        OrbitCenter.x + Mathf.Cos(startAngle * Mathf.Deg2Rad) * CircleRadius,
        OrbitCenter.y + Mathf.Sin(startAngle * Mathf.Deg2Rad) * CircleRadius,
        orbit.PlayerZ);
    public Quaternion StartRotation => faceCenter ? Quaternion.Euler(0f, 0f, startAngle + 90f) : transform.rotation;

    // Roll
    private Vector2 rollInput;

    // Boost
    private float boostVelocity;
    public event Action OnSpeedBoost;
    private float cooldownTimer; // seconds until the next boost; 0 or below = ready
    public event Action<float> OnSideBoost; // direction: +1 counter-clockwise, -1 clockwise
    public float BoostCharge01 => boostCooldown > 0f ? Mathf.Clamp01(1f - cooldownTimer / boostCooldown) : 1f; // 0 just after a boost, 1 when ready
    public float BoostDecay => boostDecay;
    public event Action OnOrbitShifted;
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

    // Before the game starts, and once it's over or won, all input is ignored.
    private bool InputLocked => GameManager.Instance != null &&
                                (!GameManager.Instance.HasStarted || GameManager.Instance.IsGameOver || GameManager.Instance.HasWon);

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
        levelTimer = GameManager.Instance != null ? GameManager.Instance.LevelTimer : null;

        orbitAngle = startAngle * Mathf.Deg2Rad;

        // Lock the cursor when play begins. Levels without a menu have already started, so lock now.
        GameManager gm = GameManager.Instance;
        if (gm == null || gm.HasStarted) LockCursor();
        else gm.OnGameStarted += LockCursor;
    }

    private void LockCursor()
    {
        // Lock the cursor to the center of the screen and make it invisible
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        // Before the start, and once the game is over or won, stop here. On a death, PlayerDeath moves the ship instead.
        if (InputLocked)
        {
            pullRate = 0f;
            frostSpeedSlow = 0f;
            frostOrbitSlow = 0f;
            return;
        }

        if (cooldownTimer > 0f) cooldownTimer -= Time.deltaTime;

        Roll();
        UpdateSpeed();
        Movement();
        UpdateInvulnerability();
    }

    private void Roll()
    {
        // If there is mouse input, roll with its speed (pixels per second); otherwise ease back to zero.
        // The fade uses 1 - e^(-k·dt) so it eases out the same at any frame rate.
        rollInput = rawMoveInput.sqrMagnitude > 0f && Time.deltaTime > 0f
            ? rawMoveInput / Time.deltaTime
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
        float arc = ((moveInput.x * mouseSensitivity * SensitivityScale) + (boostVelocity * Time.deltaTime)) * (1f - frostOrbitSlow);
        orbitAngle += arc / CircleRadius + pullRate * Time.deltaTime;
        pullRate = 0f;
        frostSpeedSlow = 0f;
        frostOrbitSlow = 0f;
        PlaceOnOrbit();

        // Boost fades out over time instead of cutting off instantly. 1 - e^(-k·dt) fades the same at any frame rate.
        boostVelocity = Mathf.Lerp(boostVelocity, 0f, 1f - Mathf.Exp(-boostDecay * Time.deltaTime));
    }

    private void PlaceOnOrbit()
    {
        // Offset is defined by the cosine (left/right) and sine (up/down) of the angle, multiplied by the radius to get world units
        Vector2 offset = new Vector2(Mathf.Cos(orbitAngle), Mathf.Sin(orbitAngle)) * CircleRadius;
        Vector2 center = OrbitCenter;
        transform.position = new Vector3(center.x + offset.x, center.y + offset.y, transform.position.z);

        if (faceCenter)
            transform.rotation = Quaternion.Euler(0f, 0f, orbitAngle * Mathf.Rad2Deg + 90f);
    }

    // Magnet asteroids call this each frame to drag the player along the orbit. Pulls from several add up.
    public void AddPull(float radiansPerSecond) => pullRate += radiansPerSecond;

    // Frost patches call this each frame the player is inside one. Only the strongest request counts.
    public void ApplyFrost(float speedSlow, float orbitSlow)
    {
        frostSpeedSlow = Mathf.Max(frostSpeedSlow, speedSlow);
        frostOrbitSlow = Mathf.Max(frostOrbitSlow, orbitSlow);
    }

    // Jumps the player around the orbit at once (PlayerTwins), keeping speed and boosts.
    public void ShiftOrbit(float radians)
    {
        orbitAngle += radians;
        PlaceOnOrbit();
        OnOrbitShifted?.Invoke();
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

        // Frost: drags Speed down by a fraction of itself each second.
        Speed -= Speed * frostSpeedSlow * Time.deltaTime;

        Speed = Mathf.Clamp(Speed, 0f, maxSpeed);

        // Invincibility holds the player at max speed, unless the level has ended.
        if (CanAccelerate && invincibility != null && invincibility.LocksMaxSpeed)
            Speed = maxSpeed;
    }

    private void OnMove(InputValue value)
    {
        moveInput = value.Get<Vector2>();
        rawMoveInput = moveInput;

        if (moveInput.sqrMagnitude >= minInputForAngle * minInputForAngle &&
            Vector2.Angle(Vector2.up, moveInput) <= forwardThreshold)
            moveInput.x = 0f;

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
        if (!value.isPressed || InputLocked || cooldownTimer > 0f)
            return;

        if (Time.time - lastSideInputTime <= sideInputMemory)
        {
            // Boost around the circle in whichever direction the mouse was last moving horizontally
            boostVelocity = lastSideDirection * boostForce;
            cooldownTimer = boostCooldown;
            OnSideBoost?.Invoke(lastSideDirection);
        }
        else if (CanAccelerate)
        {
            // No horizontal input: boost forward speed instead
            Speed = Mathf.Clamp(Speed + speedBoost, 0f, maxSpeed);
            cooldownTimer = boostCooldown;
            OnSpeedBoost?.Invoke();
        }
    }
}
