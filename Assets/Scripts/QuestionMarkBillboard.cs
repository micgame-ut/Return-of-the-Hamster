using UnityEngine;

// Keeps the "?" facing the camera while the star around it spins.
public class QuestionMarkBillboard : MonoBehaviour
{
    [Tooltip("Extra rotation in degrees, applied after facing the camera. Use it if the ? model faces the wrong way.")]
    [SerializeField] private Vector3 rotationOffset;

    private Transform cam;

    private void Start()
    {
        Camera gameCam = GameManager.Instance != null ? GameManager.Instance.GameplayCamera : null;
        cam = gameCam != null ? gameCam.transform : null;
    }

    private void LateUpdate()
    {
        if (cam == null) return;
        transform.rotation = Quaternion.LookRotation(-cam.forward, cam.up) * Quaternion.Euler(rotationOffset);
    }
}
