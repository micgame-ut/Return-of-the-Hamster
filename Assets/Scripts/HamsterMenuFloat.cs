using UnityEngine;


/* this script makes the hamster in the menu float
up and down, rotate, and move back and forth in depth
for a neat dynamic effect */
public class HamsterMenuFloat : MonoBehaviour
{
    [Tooltip("Degrees per second the hamster spins, on all three axes.")]
    [SerializeField] private float rotationSpeed = 20f;

    [Header("Box")]
    [Tooltip("How far above and below the start point he can drift before turning around, in world units.")]
    [SerializeField] private float boxHalfHeight = 1.5f;
    [Tooltip("How far toward and away from the camera he can drift before turning around, in world units.")]
    [SerializeField] private float boxHalfDepth = 2f;
    [Tooltip("How far inside the screen's left and right edges the walls are, in world units. Raise it if he flies off the sides. He overshoots by roughly his top speed squared divided by (2 × turnEase).")]
    [SerializeField] private float xMargin = 2.5f;
    [Tooltip("How fast he drifts up and down, in units per second.")]
    [SerializeField] private float ySpeed = 1.3f;
    [Tooltip("How fast he drifts toward and away from the camera, in units per second.")]
    [SerializeField] private float zSpeed = 0.9f;
    [Tooltip("How quickly his speed changes when he turns around, in units per second squared. Low = slow, floaty turns that overshoot the wall. High = a hard bounce.")]
    [SerializeField] private float turnEase = 3f;

    [Header("Wobble")]
    [Tooltip("Size of a second, smaller bob layered on the height and depth, in world units. 0 = off.")]
    [SerializeField] private float wobbleAmount = 0.25f;
    [Tooltip("How fast the second bob cycles, in radians per second.")]
    [SerializeField] private float wobbleSpeed = 2.3f;
    [Tooltip("How far the hamster leans back and forth on top of the spin, in degrees. 0 = just the spin.")]
    [SerializeField] private float tumbleSway = 25f;
    [Tooltip("How fast the lean cycles, in radians per second.")]
    [SerializeField] private float tumbleSwaySpeed = 1.3f;

    [Tooltip("How fast the hamster drifts sideways, in units per second. It turns around at the screen edges.")]
    [SerializeField] private float xSpeed = 2f;
    [Tooltip("How much the sideways speed swells and eases, as a fraction of xSpeed. 0 = constant speed, 0.5 = between half and one and a half times xSpeed.")]
    [SerializeField, Range(0f, 1f)] private float speedVariation = 0.5f;
    [Tooltip("How fast the sideways speed swells and eases, in radians per second.")]
    [SerializeField] private float speedVariationRate = 0.7f;

    private Vector3 startPosition;
    private float elapsedTime;
    private float rotationAngle;
    private Vector3 driftOffset;                        // how far he's drifted from startPosition on each axis
    private Vector3 driftVelocity;                      // his current speed on each axis; it eases toward the target instead of jumping
    private Vector3 driftDirection = Vector3.one;       // +1 or -1 per axis: which way he's heading


    void Start()
    {
        // The menu float only belongs before play. If the game has already started (a level without
        // the menu, or a restart), switch off so it can't fight PlayerMovement.
        if (GameManager.Instance != null && GameManager.Instance.HasStarted)
        {
            enabled = false;
            return;
        }

        // all of these values set as their beginning values so that the hamster can float and rotate in a dynamic way
        startPosition = transform.position;

    }

    void Update()
    {
        elapsedTime += Time.deltaTime; // add time to elapsed time
        rotationAngle += rotationSpeed * Time.deltaTime; // add rotation along with its constant speed 

        // Only the small wobble is left of the old bob; the big movement now comes from the box drift below.
        // The depth wobble uses the cosine, so it's a quarter cycle off the height wobble and the two don't move in step.
        float yWobble = Mathf.Sin(elapsedTime * wobbleSpeed) * wobbleAmount;
        float zWobble = Mathf.Cos(elapsedTime * wobbleSpeed) * wobbleAmount;

        Camera mainCamera = Camera.main;
        float depth = transform.position.z - mainCamera.transform.position.z; // calculates the depth of the hamster from the camera
        float rightEdge = mainCamera.ViewportToWorldPoint(new Vector3(1f, 0.5f, depth)).x; // calculates the right edge of the camera's viewport in world space
        float leftEdge = mainCamera.ViewportToWorldPoint(new Vector3 (0, .5f, depth)).x; // calculates the left edge of the camera's viewport in world space

        // The speed swells and eases around xSpeed, so he drifts quickly, then lazily, instead of at one pace.
        float currentSpeed = xSpeed * (1f + Mathf.Sin(elapsedTime * speedVariationRate) * speedVariation);

        // Each axis flips its heading when he's past a wall: the screen edges for x, the box for y and z.
        float currentX = startPosition.x + driftOffset.x;
        // The walls sit xMargin inside the screen edges, leaving room for the overshoot and his own size.
        if (currentX > rightEdge - xMargin) driftDirection.x = -1f;
        else if (currentX < leftEdge + xMargin) driftDirection.x = 1f;
        if (driftOffset.y > boxHalfHeight) driftDirection.y = -1f;
        else if (driftOffset.y < -boxHalfHeight) driftDirection.y = 1f;
        if (driftOffset.z > boxHalfDepth) driftDirection.z = -1f;
        else if (driftOffset.z < -boxHalfDepth) driftDirection.z = 1f;

        // The speed he wants on each axis, in the direction he's heading. His real velocity eases toward it, so turns are slow and floaty.
        Vector3 targetVelocity = Vector3.Scale(driftDirection, new Vector3(currentSpeed, ySpeed, zSpeed));
        driftVelocity = Vector3.MoveTowards(driftVelocity, targetVelocity, turnEase * Time.deltaTime);
        driftOffset += driftVelocity * Time.deltaTime;

        transform.position = startPosition + driftOffset + new Vector3(0f, yWobble, zWobble);
        // Each axis spins at a different pace (x slower, z faster), and a slow lean sways on top, so it never looks like a flat spin.
        float sway = Mathf.Sin(elapsedTime * tumbleSwaySpeed) * tumbleSway;
        transform.rotation = Quaternion.Euler(rotationAngle * 0.7f + sway, rotationAngle, rotationAngle * 0.45f - sway);
    }

    // Draws the drift box in the Scene view (and the Game view, with Gizmos on) while the hamster is selected: yellow is
    // the wall he turns around at, and the width is the screen's edges at his depth. Anything outside it is overshoot.
    private void OnDrawGizmosSelected()
    {
        // While playing, the box is centred on startPosition; in edit mode, on where he is now.
        Vector3 center = Application.isPlaying ? startPosition : transform.position;

        float width = 8f; // fallback if there's no camera
        Camera cam = Camera.main;
        if (cam != null)
        {
            float depth = center.z - cam.transform.position.z;
            float right = cam.ViewportToWorldPoint(new Vector3(1f, 0.5f, depth)).x;
            float left = cam.ViewportToWorldPoint(new Vector3(0f, 0.5f, depth)).x;
            center.x = (left + right) * 0.5f; // the screen's middle, since x turns at the screen edges, not around the start point
            width = right - left - xMargin * 2f;
        }

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(center, new Vector3(width, boxHalfHeight * 2f, boxHalfDepth * 2f));
    }
}
