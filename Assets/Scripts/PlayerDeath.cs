using System.Collections;
using UnityEngine;

// Plays the player's death once the game is over: the ship moves in front of
// the camera while shaking harder and harder, then explodes. Runs on unscaled
// time, since GameManager freezes everything else at game over.
public class PlayerDeath : MonoBehaviour
{
    [Tooltip("The ship's visual child. It gets shaken, then hidden.")]
    [SerializeField] private Transform meshTransform;

    [Header("Approach")]
    [Tooltip("How far in front of the camera the ship stops.")]
    [SerializeField] private float distanceFromCamera = 3f;
    [Tooltip("Seconds the ship sits in front of the camera, shaking, before it explodes.")]
    [SerializeField] private float holdTime = 0.75f;

    [Header("Shake")]
    [Tooltip("Seconds from game over to the explosion. The approach takes timeToExplode minus holdTime.")]
    [SerializeField] private float timeToExplode = 2f;
    [Tooltip("How far the mesh jitters at the moment it explodes. It grows from 0.")]
    [SerializeField] private float maxShake = 0.3f;
    [Tooltip("New shake directions per second, so the shake looks the same at any frame rate.")]
    [SerializeField, Min(1f)] private float shakeFrequency = 60f;

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
        Vector3 target = cam.position + cam.forward * distanceFromCamera;

        float approachTime = timeToExplode - holdTime;
        Vector3 lastOffset = Vector3.zero;
        Vector3 shakeDirection = Vector3.zero;
        float directionTimer = 0f;

        for (float elapsed = 0f; elapsed < timeToExplode; elapsed += Time.unscaledDeltaTime)
        {
            float moveT = approachTime > 0f ? Mathf.Clamp01(elapsed / approachTime) : 1f;
            transform.position = Vector3.Lerp(startPosition, target, moveT);

            // Same trick as CameraShake: undo last frame's offset before adding a new one.
            meshTransform.localPosition -= lastOffset;
            // A new direction shakeFrequency times a second, not every frame, so the shake looks the same at any frame rate.
            directionTimer -= Time.unscaledDeltaTime;
            if (directionTimer <= 0f)
            {
                shakeDirection = Random.insideUnitSphere;
                directionTimer = 1f / shakeFrequency;
            }
            lastOffset = shakeDirection * maxShake * (elapsed / timeToExplode);
            meshTransform.localPosition += lastOffset;

            yield return null;
        }

        meshTransform.localPosition -= lastOffset;
        transform.position = target;

        if (explosionPrefab != null)
            Instantiate(explosionPrefab, transform.position, Quaternion.identity);
        CameraShake.Shake(explodeShakeDuration, explodeShakeMagnitude);

        meshTransform.gameObject.SetActive(false);
        if (ring != null) ring.gameObject.SetActive(false);

        yield return new WaitForSecondsRealtime(endDelay);

        GameManager.Instance.DeathSequenceFinished();
    }
}
