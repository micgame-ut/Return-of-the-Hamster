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

        cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFOV, fovLerpSpeed * Time.deltaTime);
    }
}
