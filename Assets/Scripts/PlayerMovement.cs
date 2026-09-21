using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera mainCamera;
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

    // Speed
    public static float Speed;

    // Movement
    private Vector2 moveInput;

    // Roll
    private Vector2 rollInput;

    // Boost
    private float boostVelocity;

    void Start()
    {
        // Lock the cursor to the center of the screen and make it invisible
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // If the mainCamera is not assigned in the inspector, try to find it in the scene
        if (mainCamera == null)
            mainCamera = Camera.main;
    }

    void Update()
    {
        Vector2 mouseDelta = moveInput * Time.deltaTime;

        Roll(mouseDelta);
        UpdateSpeed(mouseDelta);
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

        if (mouseDelta.y != 0f)
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

        Debug.Log($"Current Speed: {Speed}");
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

    private void OnButton(InputValue value)
    {
        if (!value.isPressed)
            return;

        if (moveInput.x != 0f)
        {
            // Boost sideways in whichever direction the mouse was last moving horizontally
            boostVelocity = Mathf.Sign(moveInput.x) * boostForce;
        }
        else
        {
            // No horizontal input: boost forward speed instead
            Speed = Mathf.Clamp(Speed + speedBoost, 0f, maxSpeed);
        }
    }
}
