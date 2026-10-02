using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform meshTransform;

    [Header("Movement")]
    [SerializeField] private float mouseSensitivity = 0.1f;

    [Header("Orbit")]
    // Radius, center, and depth come from OrbitSettings, shared with the pattern builder
    [SerializeField] private float startAngle = -90f; // degrees; -90 = bottom of the circle, like Gyruss
    [SerializeField] private bool faceCenter = true; // rotate so the ship's top points at the center

    [Header("Roll")]
    [SerializeField] private float rotationSpeed = 100f;
    [SerializeField] private float rollDamping = 5f;

    [Header("Speed")]
    [SerializeField] private float acceleration = 5f;
    [SerializeField] private float speedDecay = 2f;
    [SerializeField] private float maxSpeed = 10f;
    [SerializeField] private float topSpeedDegrade = 3f;

    [Header("Boost")]
    [SerializeField] private float boostForce = 5f;
    [SerializeField] private float boostDecay = 3f;
    [SerializeField] private float speedBoost = 3f;

    [Header("Hit")]
    [SerializeField] private float invulnerableTime = 1.5f; // seconds of no hits after being hit
    [SerializeField] private float hitBlinkInterval = 0.1f; // time between visibility flips

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

    // Hit
    public bool IsInvulnerable => invulnerableTimer > 0f;
    private float invulnerableTimer;
    private Renderer[] meshRenderers;

    // Level
    private LevelTimer levelTimer;

    // Once the earth has arrived the player can only lose speed, not gain it.
    private bool CanAccelerate => levelTimer == null || !levelTimer.IsComplete;

    // Once the game is over, all input is ignored.
    private bool InputLocked => GameManager.Instance != null && GameManager.Instance.IsGameOver;

    // Top 10% of max speed, where the extra drain applies. RunStats times how long it's held.
    public bool InOverdrive => Speed >= maxSpeed * 0.9f;

    void Awake()
    {
        // Statics survive scene reloads, so reset them for a fresh run.
        Speed = 0f;
        MaxSpeed = maxSpeed;

        // Depth comes from OrbitSettings so it always matches the pattern builder.
        // Done in Awake so other scripts' Start already sees the right z.
        orbit = OrbitSettings.Instance;
        Vector3 p = transform.position;
        transform.position = new Vector3(p.x, p.y, orbit.PlayerZ);

        // Blink only the visual mesh; the collider stays active.
        meshRenderers = meshTransform != null
            ? meshTransform.GetComponentsInChildren<Renderer>()
            : GetComponentsInChildren<Renderer>();
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
        if (InputLocked) return;

        Vector2 mouseDelta = moveInput * Time.deltaTime;

        Roll(mouseDelta);
        UpdateSpeed(mouseDelta);
        UpdateInvulnerability();
    }

    void FixedUpdate()
    {
        // Once the game is over, PlayerDeath moves the ship instead.
        if (InputLocked) return;

        Vector2 mouseDelta = moveInput * Time.fixedDeltaTime;

        Movement(mouseDelta);
    }

    private void Roll(Vector2 mouseDelta)
    {
        // If there is mouse input, use it for roll; otherwise, gradually return to zero roll
        rollInput = moveInput.sqrMagnitude > 0f
            ? mouseDelta
            : Vector2.Lerp(rollInput, Vector2.zero, rollDamping * Time.deltaTime);

        // Rotate only the visual mesh, not the collider/root
        // Pitch around the orbit tangent (root's local x) so forward input always tilts the nose toward the center
        meshTransform.Rotate(transform.right, rollInput.y * rotationSpeed, Space.World);

        // Roll around the mesh's own axis as the player moves around the circle
        meshTransform.Rotate(0f, 0f, -rollInput.x * rotationSpeed, Space.Self);
    }

    private void Movement(Vector2 mouseDelta)
    {
        // Distance travelled along the circle this tick; positive = counter-clockwise
        // Distance divided by radius = angle in radians, so we can add it to the orbitAngle directly.
        float arc = (mouseDelta.x * mouseSensitivity) + (boostVelocity * Time.fixedDeltaTime);
        orbitAngle += arc / CircleRadius;

        // Offset is defined by the cosine (left/right) and sine (up/down) of the angle, multiplied by the radius to get world units
        Vector2 offset = new Vector2(Mathf.Cos(orbitAngle), Mathf.Sin(orbitAngle)) * CircleRadius;
        Vector2 center = OrbitCenter;
        transform.position = new Vector3(center.x + offset.x, center.y + offset.y, transform.position.z);

        if (faceCenter)
            transform.rotation = Quaternion.Euler(0f, 0f, orbitAngle * Mathf.Rad2Deg + 90f);

        // Boost fades out over time instead of cutting off instantly
        boostVelocity = Mathf.Lerp(boostVelocity, 0f, boostDecay * Time.fixedDeltaTime);
    }

    private void UpdateSpeed(Vector2 mouseDelta)
    {
        bool nearMaxSpeed = InOverdrive;

        // Forward input after the level ends counts as no input, so the normal
        // decay still applies instead of speed being held in place.
        bool hasInput = mouseDelta.y < 0f || (mouseDelta.y > 0f && CanAccelerate);

        if (hasInput)
        {
            // Speeds up on positive mouseDelta.y, slows down on negative
            Speed += mouseDelta.y * acceleration;
        }
        else
        {
            // No input: decay at a flat rate toward zero
            Speed -= speedDecay * Time.deltaTime;
        }

        if (nearMaxSpeed)
        {
            // Within the top 10% of max, extra constant degrade applies on top - makes it harder, not impossible, to hold near max
            Speed -= topSpeedDegrade * Time.deltaTime;
        }

        Speed = Mathf.Clamp(Speed, 0f, maxSpeed);
    }

    private void OnMove(InputValue value)
    {
        moveInput = value.Get<Vector2>();
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
        bool visible = invulnerableTimer <= 0f ||
                       Mathf.FloorToInt(invulnerableTimer / hitBlinkInterval) % 2 == 0;

        foreach (Renderer r in meshRenderers)
            r.enabled = visible;
    }

    private void OnButton(InputValue value)
    {
        if (!value.isPressed || InputLocked)
            return;

        if (moveInput.x != 0f)
        {
            // Boost around the circle in whichever direction the mouse was last moving horizontally
            boostVelocity = Mathf.Sign(moveInput.x) * boostForce;
        }
        else if (CanAccelerate)
        {
            // No horizontal input: boost forward speed instead
            Speed = Mathf.Clamp(Speed + speedBoost, 0f, maxSpeed);
        }
    }
}
