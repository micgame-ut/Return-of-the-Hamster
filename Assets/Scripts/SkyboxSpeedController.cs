using UnityEngine;

// Scrolls the skybox shader's clouds and radial streams, faster as PlayerMovement.Speed
// goes up, fades the streams in during overdrive, and sizes the core glow to the Earth.
// Both scrolls are running totals (_ScrollOffset, _StreamOffset), so a change in speed
// only changes how fast they grow and the sky never jumps.
[DisallowMultipleComponent]
public class SkyboxSpeedController : MonoBehaviour
{
    private static readonly int ScrollOffsetId = Shader.PropertyToID("_ScrollOffset");
    private static readonly int StreamOffsetId = Shader.PropertyToID("_StreamOffset");
    private static readonly int StreamIntensityId = Shader.PropertyToID("_StreamIntensity");
    private static readonly int CoreGlowSizeId = Shader.PropertyToID("_Core_Glow_Size");

    [SerializeField] private float baseTimeSpeed = 0.3f;
    [SerializeField] private float speedInfluence = 0.05f;
    [SerializeField] private float baseStreamSpeed = 0.5f;
    [SerializeField] private float streamSpeedInfluence = 0.2f;
    [SerializeField] private float glowEarthScale = 2f;
    [SerializeField] private float streamFadeTime = 1.5f;
    [SerializeField] private float streamIntensityCurve = 2f;
    [Tooltip("Highest random starting point for the clouds, in world units. 427 = one full repeat of the baked noise (64 cells / Cloud Scale 0.15); 0 = same sky every load.")]
    [SerializeField] private float randomStartRange = 427f;

    private Material skyboxInstance;
    private float scrollOffset;
    private float streamOffset;
    private float streamIntensity;

    private void Awake()
    {
        // Instantiate from the mat currently assigned to the skybox, so we don't permanently modify the shared .mat asset.
        Material source = RenderSettings.skybox;
        if (source != null)
        {
            skyboxInstance = Instantiate(source);
            RenderSettings.skybox = skyboxInstance;
        }
        // If there's no skybox assigned, skyboxInstance stays null and
        // Update simply no-ops every frame.

        // A different starting point in the noise each load, so every level's sky looks different.
        scrollOffset = Random.Range(0f, randomStartRange);
    }

    private void Update()
    {
        // Nothing to drive if there was no skybox to copy in Awake.
        if (skyboxInstance == null)
            return;

        // Accumulated per frame so a change in speed alters the pace without making
        // the sky jump. deltaTime is 0 while paused, so the sky freezes where it is.
        float timeSpeed = baseTimeSpeed + PlayerMovement.Speed * speedInfluence;
        scrollOffset += timeSpeed * Time.deltaTime;

        skyboxInstance.SetFloat(ScrollOffsetId, scrollOffset);

        float streamSpeed = baseStreamSpeed + PlayerMovement.Speed * streamSpeedInfluence;
        streamOffset += streamSpeed * Time.deltaTime;
        skyboxInstance.SetFloat(StreamOffsetId, streamOffset);

        float overdriveDepth = Mathf.InverseLerp(PlayerMovement.OverdriveFraction, 1f, PlayerMovement.Speed01);
        float targetIntensity = Mathf.Pow(overdriveDepth, streamIntensityCurve);
        streamIntensity = Mathf.MoveTowards(streamIntensity, targetIntensity, Time.deltaTime / Mathf.Max(streamFadeTime, 0.01f));
        skyboxInstance.SetFloat(StreamIntensityId, streamIntensity);

        GameManager gm = GameManager.Instance;
        if (gm != null && gm.Earth != null && gm.EarthCamera != null)
        {
            float earthDistance = Vector3.Distance(gm.EarthCamera.transform.position, gm.Earth.bounds.center);
            float glowRadius = gm.Earth.bounds.extents.x * glowEarthScale;
            float glowAngle = Mathf.Asin(Mathf.Clamp01(glowRadius / earthDistance));
            skyboxInstance.SetFloat(CoreGlowSizeId, 1f - Mathf.Cos(glowAngle));
        }
    }

    private void OnDestroy()
    {
        if (skyboxInstance != null) Destroy(skyboxInstance);
    }
}
