using System.Collections.Generic;
using UnityEngine;

// The Warp power-up on the player: the ship pops out, the game runs fast for a set number of seconds, then the ship pops back in.
public class PlayerWarp : MonoBehaviour
{
    private enum Phase { None, Out, Hold, In, Grace }

    public bool IsWarping => phase != Phase.None && phase != Phase.Grace;
    public bool IsProtected => phase != Phase.None;

    private Phase phase;
    private float phaseTime;    // game seconds spent in the current phase
    private float outTime;      // length of the pop out
    private float holdSeconds;  // game seconds to skip during the hold
    private float holdTimeScale; // how much faster the game runs during the hold
    private float inTime;       // length of the pop in
    private float graceTime;      // length of the protected time after the pop in
    private Material graceLook;   // the phase look the ship wears during it
    private PlayerSlim slim;
    private PlayerTwins twins;

    // The hold never runs faster than warpSeconds ÷ this, so a warpHoldTime of 0 can't ask for an endless time scale.
    private const float MinHoldTime = 0.1f;

    private void Awake()
    {
        slim = GetComponent<PlayerSlim>();
        twins = GetComponent<PlayerTwins>();
    }

    // Starts a Warp. Ignored if one is already running.
    public void Activate(float seconds, float outSeconds, float holdRealTime, float inSeconds, float graceSeconds, Material look)
    {
        if (IsWarping || seconds <= 0f) return;

        outTime = outSeconds;
        holdSeconds = seconds;
        holdTimeScale = seconds / Mathf.Max(holdRealTime, MinHoldTime);
        inTime = inSeconds;
        graceTime = graceSeconds;
        graceLook = look;
        SetLook(false);
        phase = Phase.Out;
        phaseTime = 0f;
    }

    private void Update()
    {
        if (phase == Phase.None) return;

        // Game time: during the hold this is the scaled deltaTime, so the hold ends after exactly holdSeconds of game time.
        phaseTime += Time.deltaTime;

        switch (phase)
        {
            case Phase.Out:
                if (phaseTime >= outTime) StartPhase(Phase.Hold);
                break;
            case Phase.Hold:
                if (phaseTime >= holdSeconds) StartPhase(Phase.In);
                break;
            case Phase.In:
                if (phaseTime >= inTime) StartPhase(graceTime > 0f ? Phase.Grace : Phase.None);
                break;
            case Phase.Grace:
                if (phaseTime >= graceTime) StartPhase(Phase.None);
                break;
        }
    }

    // Moves to a phase. Only the hold runs the game fast; every other phase, and the end, is back at normal speed.
    private void StartPhase(Phase next)
    {
        phase = next;
        phaseTime = 0f;
        SetLook(next == Phase.Grace);
        Time.timeScale = next == Phase.Hold ? holdTimeScale : 1f;
    }

    private readonly List<Renderer> lookRenderers = new List<Renderer>();     // every body renderer wearing the phase look
    private readonly List<Material[]> savedMaterials = new List<Material[]>(); // what each one wore before, same order
    private bool wearingLook;

    // Remembers every body renderer under one object (the player or a twin copy) and what it wears now.
    private void AddBody(Component body)
    {
        // The boost ring and the two free-boost rings share one material, so skipping that material skips all three.
        BoostRing boostRing = body.GetComponentInChildren<BoostRing>(true);
        Renderer ringRenderer = boostRing != null ? boostRing.GetComponent<Renderer>() : null;
        Material ringMaterial = ringRenderer != null ? ringRenderer.sharedMaterial : null;

        foreach (Renderer r in body.GetComponentsInChildren<Renderer>(true))
        {
            if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) continue;
            if (ringMaterial != null && r.sharedMaterial == ringMaterial) continue;
            lookRenderers.Add(r);
            savedMaterials.Add(r.sharedMaterials);
        }
    }

    private void SetLook(bool phased)
    {
        if (phased == wearingLook) return;
        wearingLook = phased;

        if (phased)
        {
            if (graceLook == null) { wearingLook = false; return; }
            AddBody(this);
            if (twins != null)
                for (int i = 0; i < twins.Copies.Count; i++) AddBody(twins.Copies[i]);
            for (int i = 0; i < lookRenderers.Count; i++)
            {
                Material[] phasedSet = new Material[savedMaterials[i].Length];
                for (int m = 0; m < phasedSet.Length; m++) phasedSet[m] = graceLook;
                lookRenderers[i].sharedMaterials = phasedSet;
            }
            return;
        }

        for (int i = 0; i < lookRenderers.Count; i++)
            if (lookRenderers[i] != null) lookRenderers[i].sharedMaterials = savedMaterials[i];
        lookRenderers.Clear();
        savedMaterials.Clear();
    }

    // The smallest size the ship shrinks to. Not 0, because a collider scaled to exactly 0 can give physics warnings.
    private const float MinScale = 0.001f;

    // How big the ship is as a share of normal: 1 = fully back, 0 = gone. Eased with SmoothStep, so a pop starts and ends gently.
    public float Pop => phase switch
    {
        Phase.Out => Mathf.SmoothStep(1f, 0f, outTime > 0f ? phaseTime / outTime : 1f),
        Phase.Hold => 0f,
        Phase.In => Mathf.SmoothStep(0f, 1f, inTime > 0f ? phaseTime / inTime : 1f),
        _ => 1f,
    };

    private bool scaled;    // true while the ship's size is changed, so the frame that puts it back to normal still runs

    private void LateUpdate()
    {
        if (phase == Phase.None && !scaled) return;

        // Runs after every Update, so it wins over Slim's own scaling, and Slim's size is still multiplied in.
        float size = (slim != null ? slim.Scale : 1f) * Mathf.Max(Pop, MinScale);
        transform.localScale = Vector3.one * size;
        if (twins != null)
            for (int i = 0; i < twins.Copies.Count; i++)
                twins.Copies[i].transform.localScale = Vector3.one * size;

        scaled = phase != Phase.None;
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

    // The level ended mid-warp: stop the phases but leave the time scale alone, because GameManager has just frozen it at 0.
    private void HandleLevelEnded()
    {
        phase = Phase.None;
        phaseTime = 0f;
        SetLook(false);
    }
}
