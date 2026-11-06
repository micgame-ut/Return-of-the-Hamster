using System;
using System.Collections.Generic;
using UnityEngine;

// The twin power-up: adds copies of the player spaced evenly around the orbit. A hit
// during the twin destroys one body (lives don't change); the player keeps going as a survivor.
public class PlayerTwins : MonoBehaviour
{
    [Tooltip("The copy that's created. It's a TwinCopy, so a prefab without the script can't be assigned.")]
    [SerializeField] private TwinCopy twinPrefab;
    [Tooltip("Most bodies at once (player + copies). Pickups past this do nothing.")]
    [SerializeField, Min(2)] private int maxBodies = 4;
    [Tooltip("Seconds for copies to ease to new spacing.")]
    [SerializeField] private float respaceDuration = 0.3f;

    [Header("Destroyed Body")]
    [Tooltip("Effect played where a body is destroyed, through EffectPool.")]
    [SerializeField] private ParticleSystem destroyEffect;
    [Tooltip("Camera shake length when a body is destroyed. 0 = no shake.")]
    [SerializeField] private float destroyShakeDuration = 0.2f;
    [Tooltip("Camera shake strength when a body is destroyed.")]
    [SerializeField] private float destroyShakeMagnitude = 0.3f;

    private PlayerMovement movement;
    private readonly List<TwinCopy> copies = new List<TwinCopy>();
    private TwinCopy jumpedOnto; // the copy the player last jumped onto, and the frame; same-frame hits on it count as hits on the player
    private int jumpFrame = -1;

    public bool IsTwinned => copies.Count > 0;
    public bool CanAddBody => twinPrefab != null && copies.Count + 1 < maxBodies; // false at the cap, so a Twin pickup would do nothing
    public IReadOnlyList<TwinCopy> Copies => copies;
    public event Action<bool> OnTwinChanged; // true when the twin starts, false when the last copy is gone
    public event Action OnBodyLost; // a twin body was destroyed by a hit

    private void Awake()
    {
        movement = GetComponent<PlayerMovement>();
    }

    // The body a collider belongs to: the player, a copy, or null. A hit on the copy the player just jumped onto counts as the player.
    public Transform FindBody(Transform hit)
    {
        if (hit.IsChildOf(transform)) return transform;
        if (jumpedOnto != null && jumpFrame == Time.frameCount && hit.IsChildOf(jumpedOnto.transform)) return transform;
        foreach (TwinCopy copy in copies)
            if (hit.IsChildOf(copy.transform)) return copy.transform;
        return null;
    }

    // A twin pickup: a new copy splits off the player, and all bodies re-space.
    public void AddBody()
    {
        if (!CanAddBody) return;

        bool wasTwinned = IsTwinned;
        TwinCopy copy = Instantiate(twinPrefab, transform.position, transform.rotation);
        copies.Add(copy);
        Respace();

        if (!wasTwinned) OnTwinChanged?.Invoke(true);
    }

    // A body was hit during the twin: it blows up and one body is removed. Lives don't change.
    public void LoseBody(Transform body)
    {
        if (!IsTwinned || body == null) return;

        Vector3 where = body.position;
        TwinCopy lost = body == transform ? JumpToNearestCopy() : copies.Find(c => c.transform == body);
        if (lost == null) return;

        EffectPool.Play(destroyEffect, where);
        OnBodyLost?.Invoke();
        if (destroyShakeDuration > 0f) CameraShake.Shake(destroyShakeDuration, destroyShakeMagnitude);

        copies.Remove(lost);
        Destroy(lost.gameObject);

        if (IsTwinned) Respace();
        else OnTwinChanged?.Invoke(false);
    }

    // The player was hit: it takes over the nearest copy's spot, and that copy is the one removed.
    private TwinCopy JumpToNearestCopy()
    {
        TwinCopy nearest = copies[0];
        foreach (TwinCopy copy in copies)
            if (AngleFromPlayer(copy) < AngleFromPlayer(nearest)) nearest = copy;

        float jump = nearest.Offset;
        movement.ShiftOrbit(jump);
        foreach (TwinCopy copy in copies)
            if (copy != nearest) copy.ShiftOffset(-jump);

        jumpedOnto = nearest;
        jumpFrame = Time.frameCount;
        return nearest;
    }

    // How far a copy is from the player around the ring, either way, in degrees.
    private static float AngleFromPlayer(TwinCopy copy) => Mathf.Abs(Mathf.DeltaAngle(0f, copy.Offset * Mathf.Rad2Deg));

    // Spreads the copies evenly after the player, keeping their order around the ring.
    private void Respace()
    {
        float fullTurn = Mathf.PI * 2f;
        foreach (TwinCopy copy in copies)
            copy.ShiftOffset(Mathf.Repeat(copy.Offset, fullTurn) - copy.Offset);

        copies.Sort((a, b) => a.Offset.CompareTo(b.Offset));

        float spacing = fullTurn / (copies.Count + 1);
        for (int i = 0; i < copies.Count; i++)
            copies[i].MoveTo(spacing * (i + 1), respaceDuration);
    }
}
