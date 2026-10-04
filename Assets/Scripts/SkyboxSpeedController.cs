using UnityEngine;

// Scrolls the skybox shader's clouds and radial streams, faster as PlayerMovement.Speed
// goes up, and fades the streams in during overdrive. Both scrolls are running totals
// (_ScrollOffset, _StreamOffset), so a change in speed only changes how fast they grow
// and the sky never jumps.
[DisallowMultipleComponent]
public class SkyboxSpeedController : MonoBehaviour
{
    private static readonly int ScrollOffsetId = Shader.PropertyToID("_ScrollOffset");
    private static readonly int StreamOffsetId = Shader.PropertyToID("_StreamOffset");
    private static readonly int StreamIntensityId = Shader.PropertyToID("_StreamIntensity");

    [SerializeField] private float baseTimeSpeed = 0.3f;
    [SerializeField] private float speedInfluence = 0.05f;
    [SerializeField] private float baseStreamSpeed = 0.5f;
    [SerializeField] private float streamSpeedInfluence = 0.2f;

    private Material skyboxInstance;
    private float scrollOffset;
    private float streamOffset;

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

        float streamIntensity = Mathf.InverseLerp(PlayerMovement.OverdriveFraction, 1f, PlayerMovement.Speed01);
        skyboxInstance.SetFloat(StreamIntensityId, streamIntensity);
    }

    private void OnDestroy()
    {
        if (skyboxInstance != null) Destroy(skyboxInstance);
    }
}
