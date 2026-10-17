using UnityEngine;


/* this script makes the hamster in the menu float
up and down, rotate, and move back and forth in depth
for a neat dynamic effect */
public class HamsterMenuFloat : MonoBehaviour
{
    [SerializeField] private float rotationSpeed = 20f;
    [SerializeField] private float floatHeight = .5f; // size of the height range
    [SerializeField] private float floatSpeed = 1f;
    [SerializeField] private float depthDistance = .5f;
    [SerializeField] private float depthSpeed = 1f;

    [SerializeField] private float xSpeed = 2f;

    private Vector3 startPosition;
    private float elapsedTime;
    private float rotationAngle;
    private float xOffset;
    private float xDirection = 1f;


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

        float yOffset = Mathf.Sin(elapsedTime * floatSpeed) * floatHeight; // calculates the y offset with float height cuz its a size range
        float zOffset = Mathf.Sin(elapsedTime * depthSpeed) * depthDistance; // makes hamster dude move back and forth in depth
        
        Camera mainCamera = Camera.main;
        float depth = transform.position.z - mainCamera.transform.position.z; // calculates the depth of the hamster from the camera
        float rightEdge = mainCamera.ViewportToWorldPoint(new Vector3(1f, 0.5f, depth)).x; // calculates the right edge of the camera's viewport in world space
        float leftEdge = mainCamera.ViewportToWorldPoint(new Vector3 (0, .5f, depth)).x; // calculates the left edge of the camera's viewport in world space

        xOffset += xDirection * xSpeed * Time.deltaTime; // calculates the x offset based on the direction and speed of movement
        float currentX = startPosition.x + xOffset;

        if (currentX > rightEdge)
        {
            xDirection = -1f; // change direction to go left
        }
        else if (currentX < leftEdge)
        {
            xDirection = 1f; // change direction to go right
        }
        transform.position = startPosition + new Vector3(xOffset, yOffset, zOffset); // this updates the hamster's new position
        transform.rotation = Quaternion.Euler(rotationAngle, rotationAngle, rotationAngle); // updates the hamster's rotation on all three axes
    }
}
