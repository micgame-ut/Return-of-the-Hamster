using System.Collections;
using UnityEngine;

// Plays the player's death once the game is over: the ship moves in front of
// the camera while shaking harder and harder, then explodes. Runs on unscaled
// time, since GameManager freezes everything else at game over.
public class PlayerDeath : MonoBehaviour
{
    public static event System.Action OnExploded; // the death sequence's explosion has just played
    public static event System.Action OnArrived; // the ship has reached the camera, after the approach and before the hold

    [Tooltip("The ship's visual child. It is hidden at the explosion.")]
    [SerializeField] private Transform meshTransform;

    [Header("Approach")]
    [Tooltip("How far in front of the camera the ship stops.")]
    [SerializeField] private float distanceFromCamera = 3f;
    [Tooltip("Seconds the ship sits in front of the camera, shaking, before it explodes.")]
    [SerializeField] private float holdTime = 0.75f;

    [Header("Shake")]
    [Tooltip("Seconds from game over to the explosion. The approach takes timeToExplode minus holdTime.")]
    [SerializeField] private float timeToExplode = 2f;
    [Tooltip("How far the whole player (ball, hamster and ring) jitters at the moment it explodes. It grows from 0.")]
    [SerializeField] private float maxShake = 0.3f;
    [Tooltip("New shake directions per second, so the shake looks the same at any frame rate.")]
    [SerializeField, Min(1f)] private float shakeFrequency = 60f;

    [Header("Ring")]
    [Tooltip("Degrees the ring tips down toward the camera as the ship arrives, so the hamster inside can be seen better. 0 = no tilt. A negative number tips it the other way.")]
    [SerializeField] private float ringTilt = 25f;
    [Tooltip("The hamster (Apollo, under the ring). When the death starts it's taken out from under the ring so he keeps facing the camera while the ring tilts. Empty = he tilts with the ring.")]
    [SerializeField] private Transform hamster;

    [Header("Explosion")]
    [Tooltip("The particle effect spawned at the explosion.")]
    [SerializeField] private ParticleSystem explosionPrefab;
    [Tooltip("How long the camera shakes at the explosion.")]
    [SerializeField] private float explodeShakeDuration = 0.4f;
    [Tooltip("How hard the camera shakes at the explosion.")]
    [SerializeField] private float explodeShakeMagnitude = 0.5f;
    [Tooltip("Seconds after the explosion before DeathSequenceFinished is called.")]
    [SerializeField] private float endDelay = 1.5f;

    private BoostRing ring;

    private void Awake()
    {
        // The ring isn't under the mesh, so it's found separately to vanish with the ship when it explodes.
        ring = GetComponentInChildren<BoostRing>(true);
    }

    private void OnEnable()
    {
        if (GameManager.Instance != null) GameManager.Instance.OnGameOver += HandleGameOver;
    }

    private void OnDisable()
    {
        if (GameManager.Instance != null) GameManager.Instance.OnGameOver -= HandleGameOver;
    }

    private void HandleGameOver() => StartCoroutine(DeathSequence());

    private IEnumerator DeathSequence()
    {
        // The last hit may have frozen the blink with the ship hidden.
        foreach (Renderer r in meshTransform.GetComponentsInChildren<Renderer>())
            r.enabled = true;

        Transform cam = GameManager.Instance.GameplayCamera.transform;
        Vector3 startPosition = transform.position;
        Quaternion startRotation = transform.rotation;
        Vector3 target = cam.position + cam.forward * distanceFromCamera;
        Quaternion faceCamera = Quaternion.LookRotation(-cam.forward, cam.up);

        // Take the hamster out from under the ring (keeping where he is), so the ring can tilt without tilting him.
        if (hamster != null) hamster.SetParent(transform, true);
        // BoostRing would keep setting the ring's rotation if the death came mid-roll, and fight the tilt.
        if (ring != null) ring.enabled = false;
        Quaternion ringStart = ring != null ? ring.transform.localRotation : Quaternion.identity;

        float approachTime = timeToExplode - holdTime;
        Vector3 shakeDirection = Vector3.zero;
        float directionTimer = 0f;
        bool arrived = false;

        for (float elapsed = 0f; elapsed < timeToExplode; elapsed += Time.unscaledDeltaTime)
        {
            float moveT = approachTime > 0f ? Mathf.Clamp01(elapsed / approachTime) : 1f;
            if (!arrived && moveT >= 1f)
            {
                arrived = true;
                OnArrived?.Invoke();
            }
            // A new direction shakeFrequency times a second, not every frame, so the shake looks the same at any frame rate.
            directionTimer -= Time.unscaledDeltaTime;
            if (directionTimer <= 0f)
            {
                shakeDirection = Random.insideUnitSphere;
                directionTimer = 1f / shakeFrequency;
            }
            Vector3 shakeOffset = shakeDirection * maxShake * (elapsed / timeToExplode);
            transform.position = Vector3.Lerp(startPosition, target, moveT) + shakeOffset;
            transform.rotation = Quaternion.Slerp(startRotation, faceCamera, moveT);
            if (ring != null) ring.transform.localRotation = Quaternion.AngleAxis(ringTilt * moveT, Vector3.right) * ringStart;

            yield return null;
        }

        transform.position = target;

        if (explosionPrefab != null)
            Instantiate(explosionPrefab, transform.position, Quaternion.identity);
        OnExploded?.Invoke();
        CameraShake.Shake(explodeShakeDuration, explodeShakeMagnitude);

        meshTransform.gameObject.SetActive(false);
        if (ring != null) ring.gameObject.SetActive(false);
        if (hamster != null) hamster.gameObject.SetActive(false);

        yield return new WaitForSecondsRealtime(endDelay);

        GameManager.Instance.DeathSequenceFinished();
    }
}
