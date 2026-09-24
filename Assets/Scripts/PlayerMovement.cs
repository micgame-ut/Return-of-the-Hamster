using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform meshTransform;

    [Header("Movement")]
    [SerializeField] private float mouseSensitivity = 0.1f;

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

    // Camera used for screen bounds, supplied by the GameManager
    private Camera mainCamera;

    // Movement
    private Vector2 moveInput;

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

    void Awake()
    {
        // Statics survive scene reloads, so reset them for a fresh run.
        Speed = 0f;
        MaxSpeed = maxSpeed;

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

        mainCamera = GameManager.Instance != null ? GameManager.Instance.GameplayCamera : null;
        levelTimer = GameManager.Instance != null ? GameManager.Instance.LevelTimer : null;
    }

    void Update()
    {
        Vector2 mouseDelta = moveInput * Time.deltaTime;

        Roll(mouseDelta);
        UpdateSpeed(mouseDelta);
        UpdateInvulnerability();
    }

    void FixedUpdate()
    {
        Vector2 mouseDelta = moveInput * Time.fixedDeltaTime;

        Movement(mouseDelta);
        ClampToCameraBounds();
    }

    private void Roll(Vector2 mouseDelta)
    {
        // If there is mouse input, use it for roll; otherwise, gradually return to zero roll
        rollInput = moveInput.sqrMagnitude > 0f
            ? mouseDelta
            : Vector2.Lerp(rollInput, Vector2.zero, rollDamping * Time.deltaTime);

        // Rotate only the visual mesh, not the collider/root
        meshTransform.Rotate(rollInput.y * rotationSpeed, 0f, -rollInput.x * rotationSpeed, Space.World);
    }

    private void Movement(Vector2 mouseDelta)
    {
        transform.Translate((mouseDelta.x * mouseSensitivity) + (boostVelocity * Time.fixedDeltaTime), 0f, 0f);

        // Boost fades out over time instead of cutting off instantly
        boostVelocity = Mathf.Lerp(boostVelocity, 0f, boostDecay * Time.fixedDeltaTime);
    }

    private void UpdateSpeed(Vector2 mouseDelta)
    {
        bool nearMaxSpeed = Speed >= maxSpeed * 0.9f;

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

    private void ClampToCameraBounds()
    {
        // Calculate the distance from the camera to the player
        float distance = transform.position.z - mainCamera.transform.position.z;

        // Get the world position of the left and right edges of the camera's viewport at the player's distance
        Vector3 leftEdge = mainCamera.ViewportToWorldPoint(new Vector3(0f, 0.5f, distance));
        Vector3 rightEdge = mainCamera.ViewportToWorldPoint(new Vector3(1f, 0.5f, distance));

        // Clamp the player's x position to stay within the camera's bounds
        float clampX = Mathf.Clamp(transform.position.x, leftEdge.x, rightEdge.x);
        transform.position = new Vector3(clampX, transform.position.y, transform.position.z);
    }

    private void OnMove(InputValue value)
    {
        moveInput = value.Get<Vector2>();
    }

    public bool TakeHit(float knockbackVelocity, float speedLoss)
    {
        // Hits during the invulnerability window are ignored entirely.
        if (IsInvulnerable) return false;

        // Reuses the boost velocity so the push decays the same way a boost does
        // and ClampToCameraBounds keeps the player on screen.
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
        if (!value.isPressed)
            return;

        if (moveInput.x != 0f)
        {
            // Boost sideways in whichever direction the mouse was last moving horizontally
            boostVelocity = Mathf.Sign(moveInput.x) * boostForce;
        }
        else if (CanAccelerate)
        {
            // No horizontal input: boost forward speed instead
            Speed = Mathf.Clamp(Speed + speedBoost, 0f, maxSpeed);
        }
    }
}
