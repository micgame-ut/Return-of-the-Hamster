using UnityEngine;

public class PlayerCamera : MonoBehaviour
{
    [SerializeField] private Camera cam;
    [SerializeField] private float baseFOV = 60f;
    [SerializeField] private float maxFOV = 80f;
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
