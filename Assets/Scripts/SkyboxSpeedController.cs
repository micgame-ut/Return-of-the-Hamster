using UnityEngine;

// Scrolls the skybox shader's clouds, faster as PlayerMovement.Speed goes up.
// The scroll is a running total sent to the shader as _ScrollOffset, so a change
// in speed only changes how fast it grows and the sky never jumps.
[DisallowMultipleComponent]
public class SkyboxSpeedController : MonoBehaviour
{
    private static readonly int ScrollOffsetId = Shader.PropertyToID("_ScrollOffset");

    [SerializeField] private float baseTimeSpeed = 0.3f;
    [SerializeField] private float speedInfluence = 0.05f;

    private Material skyboxInstance;
    private float scrollOffset;

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
    }

    private void OnDestroy()
    {
        if (skyboxInstance != null) Destroy(skyboxInstance);
    }
}
