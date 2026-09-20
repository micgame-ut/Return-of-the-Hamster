using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    [SerializeField] private float mouseSensitivity = 0.1f;
    [SerializeField] private Camera mainCamera;

    private Vector2 moveInput;

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (mainCamera == null)
            mainCamera = Camera.main;
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        Vector2 mouseDelta = moveInput * Time.fixedDeltaTime;

        transform.Translate(mouseDelta.x * mouseSensitivity, 0f, 0f);
        ClampToCameraBounds();
    }

    private void ClampToCameraBounds()
    {
        float distance = transform.position.z - mainCamera.transform.position.z;
        Vector3 leftEdge = mainCamera.ViewportToWorldPoint(new Vector3(0f, 0.5f, distance));
        Vector3 rightEdge = mainCamera.ViewportToWorldPoint(new Vector3(1f, 0.5f, distance));

        float clampedX = Mathf.Clamp(transform.position.x, leftEdge.x, rightEdge.x);
        transform.position = new Vector3(clampedX, transform.position.y, transform.position.z);
    }

    private void OnMove(InputValue value)
    {
        moveInput = value.Get<Vector2>();
    }

    private void OnButton(InputValue value)
    {
        if (value.isPressed)
        {
            Debug.Log("Button Pressed");
        }
    }
}
