using UnityEngine;

public class PlayerCamera : MonoBehaviour
{
    [Tooltip("The camera to change. If left empty, it uses the Camera on the same GameObject.")]
    [SerializeField] private Camera cam;
    [Tooltip("Field of view (in degrees) when the player's Speed is 0.")]
    [SerializeField] private float baseFOV = 60f;
    [Tooltip("Field of view when the player is at max speed.")]
    [SerializeField] private float maxFOV = 80f;
    [Tooltip("How quickly the field of view catches up to its target. Higher is snappier.")]
    [SerializeField] private float fovLerpSpeed = 5f;

    void Start()
    {
        if (cam == null)
            cam = GetComponent<Camera>();
    }

    void Update()
    {
        float targetFOV = Mathf.Lerp(baseFOV, maxFOV, PlayerMovement.Speed01);

        // 1 - e^(-k·dt) eases toward the target the same at any frame rate.
        cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFOV, 1f - Mathf.Exp(-fovLerpSpeed * Time.deltaTime));
    }
}
