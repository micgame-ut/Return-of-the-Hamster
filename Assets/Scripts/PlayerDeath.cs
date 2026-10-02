using System.Collections;
using UnityEngine;

// Plays the player's death once the game is over: the ship moves in front of
// the camera while shaking harder and harder, then explodes. Runs on unscaled
// time, since GameManager freezes everything else at game over.
public class PlayerDeath : MonoBehaviour
{
    [SerializeField] private Transform meshTransform;

    [Header("Approach")]
    [SerializeField] private float distanceFromCamera = 3f; // where the ship stops, in front of the camera
    [SerializeField] private float holdTime = 0.75f;        // seconds at the camera before exploding

    [Header("Shake")]
    [SerializeField] private float timeToExplode = 2f;      // seconds from game over to the explosion
    [SerializeField] private float maxShake = 0.3f;         // mesh offset at the moment it explodes

    [Header("Explosion")]
    [SerializeField] private ParticleSystem explosionPrefab;
    [SerializeField] private float explodeShakeDuration = 0.4f;
    [SerializeField] private float explodeShakeMagnitude = 0.5f;
    [SerializeField] private float endDelay = 1.5f;          // seconds after exploding before DeathSequenceFinished

    private bool started;

    private void Update()
    {
        if (started) return;

        GameManager gm = GameManager.Instance;
        if (gm == null || !gm.IsGameOver) return;

        started = true;
        StartCoroutine(DeathSequence());
    }

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

        for (float elapsed = 0f; elapsed < timeToExplode; elapsed += Time.unscaledDeltaTime)
        {
            float moveT = approachTime > 0f ? Mathf.Clamp01(elapsed / approachTime) : 1f;
            transform.position = Vector3.Lerp(startPosition, target, moveT);

            // Same trick as CameraShake: undo last frame's offset before adding a new one.
            meshTransform.localPosition -= lastOffset;
            lastOffset = Random.insideUnitSphere * maxShake * (elapsed / timeToExplode);
            meshTransform.localPosition += lastOffset;

            yield return null;
        }

        meshTransform.localPosition -= lastOffset;
        transform.position = target;

        if (explosionPrefab != null)
            Instantiate(explosionPrefab, transform.position, Quaternion.identity);
        CameraShake.Shake(explodeShakeDuration, explodeShakeMagnitude);

        meshTransform.gameObject.SetActive(false);

        yield return new WaitForSecondsRealtime(endDelay);

        GameManager.Instance.DeathSequenceFinished();
    }
}
