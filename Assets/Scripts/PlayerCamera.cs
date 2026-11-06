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

    [Header("Boost")]
    [Tooltip("Extra degrees of field of view added the instant a forward boost fires, then eased back out.")]
    [SerializeField] private float boostFovKick = 8f;
    [Tooltip("Seconds the extra field of view takes to fade out after a forward boost. 0 = no kick.")]
    [SerializeField] private float boostFovTime = 0.4f;
    [Tooltip("Seconds the camera shakes when a forward boost fires. 0 = no shake.")]
    [SerializeField] private float boostShakeDuration = 0.3f;
    [Tooltip("How far the camera shakes on a forward boost, in world units. A hit is 0.2, a smash 0.3.")]
    [SerializeField] private float boostShakeMagnitude = 0.12f;

    private PlayerMovement movement;
    private float kickTimer; // seconds left on the boost FOV kick; 0 = none
    private float currentFov; // the smoothed field of view, before the boost kick is added

    void Awake()
    {
        movement = GameManager.Instance != null ? GameManager.Instance.Player : null;
    }

    void OnEnable()
    {
        if (movement != null) movement.OnSpeedBoost += StartKick;
    }

    void OnDisable()
    {
        if (movement != null) movement.OnSpeedBoost -= StartKick;
    }

    private void StartKick()
    {
        kickTimer = boostFovTime;
        if (boostShakeDuration > 0f) CameraShake.Shake(boostShakeDuration, boostShakeMagnitude);
    }

    void Start()
    {
        if (cam == null)
            cam = GetComponent<Camera>();
        currentFov = cam.fieldOfView;
    }

    void Update()
    {
        float targetFOV = Mathf.Lerp(baseFOV, maxFOV, PlayerMovement.Speed01);

        // 1 - e^(-k·dt) eases toward the target the same at any frame rate.
        currentFov = Mathf.Lerp(currentFov, targetFOV, 1f - Mathf.Exp(-fovLerpSpeed * Time.deltaTime));

        float kick = 0f;
        if (kickTimer > 0f)
        {
            kickTimer -= Time.deltaTime;
            float t = Mathf.Clamp01(kickTimer / boostFovTime);
            kick = boostFovKick * t * t; // squared, so it snaps wide and falls away fast
        }
        cam.fieldOfView = currentFov + kick;
    }
}
