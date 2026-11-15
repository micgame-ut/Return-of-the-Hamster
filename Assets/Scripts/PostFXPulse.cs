using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Spikes post-processing on events (a hit, Warp, Jackpot) and eases it back to its resting look.
public class PostFXPulse : MonoBehaviour
{
    [Tooltip("The Global Volume whose look is pulsed. Its profile is copied at start, so the saved profile is never changed.")]
    [SerializeField] private Volume volume;

    [Tooltip("How fast a pulse fades, as a share of the full pulse per second. 4 = gone in about a quarter of a second.")]
    [SerializeField] private float decaySpeed = 4f;

    [Tooltip("The vignette intensity right after a hit (0 to 1). The resting value comes from the profile.")]
    [SerializeField] private float hitVignette = 0.55f;

    [Tooltip("The chromatic aberration right after a hit (0 to 1).")]
    [SerializeField] private float hitAberration = 0.6f;

    [Tooltip("The chromatic aberration while Warp is running (0 to 1).")]
    [SerializeField] private float warpAberration = 0.8f;

    [Tooltip("The lens distortion while Warp is running (-1 to 1). Negative pinches the picture in.")]
    [SerializeField] private float warpLensDistortion = -0.4f;

    [Tooltip("How much extra bloom intensity Jackpot adds while it's active.")]
    [SerializeField] private float jackpotBloomBoost = 1.5f;

    private VolumeProfile profileCopy;
    private Vignette vignette;
    private ChromaticAberration aberration;
    private LensDistortion lens;
    private Bloom bloom;

    private float restVignette, restAberration, restBloom;   // the profile's own values, read once

    private void Awake()
    {
        profileCopy = Instantiate(volume.sharedProfile);
        volume.profile = profileCopy;

        profileCopy.TryGet(out vignette);
        profileCopy.TryGet(out aberration);
        profileCopy.TryGet(out lens);
        profileCopy.TryGet(out bloom);

        restVignette = vignette.intensity.value;
        restAberration = aberration.intensity.value;
        restBloom = bloom.intensity.value;
    }

    private float hitPulse;       // 1 right after a hit, eases to 0
    private bool jackpotOn;       // true while Jackpot is active

    private void OnEnable()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null) return;
        gm.PlayerHealth.OnLifeLost += HandleLifeLost;
        gm.PlayerJackpot.OnJackpotChanged += HandleJackpotChanged;
    }

    private void OnDisable()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null) return;
        gm.PlayerHealth.OnLifeLost -= HandleLifeLost;
        gm.PlayerJackpot.OnJackpotChanged -= HandleJackpotChanged;
    }

    private void HandleLifeLost()
    {
        hitPulse = 1f;
    }

    private void HandleJackpotChanged(bool active)
    {
        jackpotOn = active;
    }

    private float warpShare;      // 0 to 1, eases toward 1 while Warp runs
    private float jackpotShare;   // 0 to 1, eases toward 1 while Jackpot runs

    private void Update()
    {
        // unscaledDeltaTime: Warp runs the game fast, and the pulses shouldn't speed up with it.
        float step = decaySpeed * Time.unscaledDeltaTime;

        hitPulse = Mathf.MoveTowards(hitPulse, 0f, step);

        PlayerWarp warp = GameManager.Instance != null ? GameManager.Instance.PlayerWarp : null;
        bool warping = warp != null && warp.IsWarping;
        warpShare = Mathf.MoveTowards(warpShare, warping ? 1f : 0f, step);
        jackpotShare = Mathf.MoveTowards(jackpotShare, jackpotOn ? 1f : 0f, step);

        vignette.intensity.value = Mathf.Lerp(restVignette, hitVignette, hitPulse);
        // The stronger of the two pushes wins, so a hit during Warp doesn't stack past either peak.
        aberration.intensity.value = restAberration + Mathf.Max(
            (hitAberration - restAberration) * hitPulse,
            (warpAberration - restAberration) * warpShare);
        lens.intensity.value = warpLensDistortion * warpShare;
        bloom.intensity.value = restBloom + jackpotBloomBoost * jackpotShare;
    }

    private void OnDestroy()
    {
        if (profileCopy != null) Destroy(profileCopy);
    }
}
