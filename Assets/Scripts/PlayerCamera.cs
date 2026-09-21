using UnityEngine;

public class PlayerCamera : MonoBehaviour
{
    [SerializeField] private Camera cam;
    [SerializeField] private float baseFOV = 60f;
    [SerializeField] private float maxFOV = 80f;
    [SerializeField] private float maxSpeedForFOV = 10f;
    [SerializeField] private float fovLerpSpeed = 5f;

    void Start()
    {
        if (cam == null)
            cam = GetComponent<Camera>();
    }

    void Update()
    {
        float speedRatio = Mathf.Clamp01(PlayerMovement.Speed / maxSpeedForFOV);
        float targetFOV = Mathf.Lerp(baseFOV, maxFOV, speedRatio);

        cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFOV, fovLerpSpeed * Time.deltaTime);
    }
}
