using System;
using System.Collections.Generic;
using UnityEngine;

// The Lance power-up: while it has shots left, a click fires a railgun-style beam down the player's lane, and each twin copy's, in place of the
// dodge. It owns the shots and the firing. PlayerMovement asks it first on every click.
public class PlayerLance : MonoBehaviour
{
    [Tooltip("The SpawnableTemplate asset. Assign the same one the asteroids use.")]
    [SerializeField] private SpawnableTemplate template;
    [Tooltip("The beam: a LineRenderer for the glow, with a child LineRenderer for the bright core. One is made for each body that fires, then reused.")]
    [SerializeField] private LineRenderer beamPrefab;

    public bool IsActive => shots > 0;
    public int ShotsLeft => shots;
    public float ShotCooldown => template != null ? template.lanceCooldown : 0f;   // seconds before the next shot, while there are more

    public event Action OnShotsChanged;   // the shot count went up or down, so the rings can update
    public event Action OnFired;   // one shot was fired, whatever it hit

    private int shots;
    private PlayerMovement movement;
    private PlayerTwins twins;
    private Score score;
    private readonly List<AsteroidBehavior> hits = new List<AsteroidBehavior>();   // reused for every shot, so firing makes no garbage

    private class Beam
    {
        public LineRenderer glow;   // the wide pink part; the core is its first child
        public LineRenderer core;
        public float age;           // seconds since it was fired; a beam is free again once this reaches lanceBeamTime
    }

    private readonly List<Beam> beams = new List<Beam>();   // every beam made so far, in use or free

    private void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        twins = GetComponent<PlayerTwins>();
        score = GameManager.Instance != null ? GameManager.Instance.Score : null;
    }

    // A Lance pickup: gives the template's shots (never fewer than you already hold) and starts from a ready ring.
    public void Activate()
    {
        if (template == null) return;

        movement.ReadyBoost();
        SetShots(Mathf.Max(shots, template.lanceShots));
    }

    // A Recharge picked up while Lance is active: adds shots, up to the most you can hold, and readies the next one.
    public void AddShots(int count)
    {
        if (template == null) return;

        movement.ReadyBoost();
        SetShots(Mathf.Min(shots + count, template.lanceMaxShots));
    }

    private void SetShots(int value)
    {
        if (value == shots) return;
        shots = value;
        OnShotsChanged?.Invoke();
    }

    // Fires one shot from the player and from every twin copy. Returns false if there is no shot to fire.
    public bool TryFire()
    {
        if (shots <= 0 || template == null) return false;

        int removed = FireFrom(transform.position);
        if (twins != null)
            for (int i = 0; i < twins.Copies.Count; i++)
                removed += FireFrom(twins.Copies[i].transform.position);

        if (template.shakeDuration > 0f) CameraShake.Shake(template.shakeDuration, template.shakeMagnitude);
        if (removed > 0 && template.lancePoints > 0f && score != null)
            score.AddBonusPoints(removed * template.lancePoints, transform.position, removed + " HIT", Score.ScoreSource.Asteroids);

        OnFired?.Invoke();
        SetShots(shots - 1);
        return true;
    }

    // Removes every asteroid in the lane straight ahead of origin. Returns how many were removed.
    private int FireFrom(Vector3 origin)
    {
        ShowBeam(origin);
        hits.Clear();
        float reach = template.lanceWidth / 2f;
        foreach (AsteroidBehavior asteroid in AsteroidBehavior.Active)
        {
            Vector3 p = asteroid.transform.position;
            float ahead = p.z - origin.z;
            if (ahead < -0.5f || ahead > template.lanceRange || asteroid.IsPhased) continue;
            if (Vector2.Distance(p, origin) > reach + asteroid.HitRadius) continue;
            hits.Add(asteroid);
        }

        int removed = 0;
        for (int i = 0; i < hits.Count; i++)
            if (hits[i].Blast()) removed++;
        return removed;
    }

    // Draws a beam from origin straight ahead, reusing a finished one if there is one.
    private void ShowBeam(Vector3 origin)
    {
        if (beamPrefab == null) return;

        Beam beam = null;
        foreach (Beam b in beams)
            if (b.age >= template.lanceBeamTime) { beam = b; break; }
        if (beam == null)
        {
            beam = MakeBeam();
            beams.Add(beam);
        }

        Vector3 end = origin + Vector3.forward * template.lanceRange;
        beam.glow.SetPosition(0, origin);
        beam.glow.SetPosition(1, end);
        beam.core.SetPosition(0, origin);
        beam.core.SetPosition(1, end);
        beam.age = 0f;
        beam.glow.gameObject.SetActive(true);
    }

    // Makes one beam, tinting the glow with the template's color and the core with a lighter version of it.
    private Beam MakeBeam()
    {
        LineRenderer glow = Instantiate(beamPrefab);
        LineRenderer core = glow.transform.GetChild(0).GetComponent<LineRenderer>();

        MaterialPropertyBlock block = new MaterialPropertyBlock();
        block.SetColor("_BaseColor", template.lanceBeamColor);
        glow.SetPropertyBlock(block);
        block.SetColor("_BaseColor", Color.Lerp(template.lanceBeamColor, new Color(4f, 3.5f, 4f), 0.7f));
        core.SetPropertyBlock(block);

        glow.gameObject.SetActive(false);
        return new Beam { glow = glow, core = core, age = template.lanceBeamTime };
    }

    // Ages every beam: it thins and fades over lanceBeamTime, then switches off.
    private void Update()
    {
        if (template == null) return;

        foreach (Beam beam in beams)
        {
            if (beam.age >= template.lanceBeamTime) continue;

            beam.age += Time.deltaTime;
            if (beam.age >= template.lanceBeamTime)
            {
                beam.glow.gameObject.SetActive(false);
                continue;
            }

            float t = beam.age / template.lanceBeamTime;
            float thin = 1f - t;
            float fade = 1f - t * t;
            beam.glow.widthMultiplier = template.lanceBeamWidth * 3f * thin;
            beam.core.widthMultiplier = template.lanceBeamWidth * thin;
            Color alpha = new Color(1f, 1f, 1f, fade);
            beam.glow.startColor = alpha;
            beam.glow.endColor = alpha;
            beam.core.startColor = alpha;
            beam.core.endColor = alpha;
        }
    }

    private void OnEnable()
    {
        if (GameManager.Instance == null) return;
        GameManager.Instance.OnGameOver += HandleLevelEnded;
        GameManager.Instance.OnWin += HandleLevelEnded;
    }

    private void OnDisable()
    {
        if (GameManager.Instance == null) return;
        GameManager.Instance.OnGameOver -= HandleLevelEnded;
        GameManager.Instance.OnWin -= HandleLevelEnded;
    }

    private void HandleLevelEnded()
    {
        SetShots(0);
    }
}
