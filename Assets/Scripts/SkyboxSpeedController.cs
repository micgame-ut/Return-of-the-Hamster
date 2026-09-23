using UnityEngine;

// Drives the skybox shader's Time Speed from gameplay state: scales with
// PlayerMovement.Speed so the sky drifts faster as the player speeds up,
// and freezes entirely while the game is paused (Time.timeScale == 0).
[DisallowMultipleComponent]
public class SkyboxSpeedController : MonoBehaviour
{
    private static readonly int TimeSpeedId = Shader.PropertyToID("_TimeSpeed");

    [SerializeField] private float baseTimeSpeed = 0.3f;
    [SerializeField] private float speedInfluence = 0.05f;

    private Material skyboxInstance;

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

        // Paused (or timeScale otherwise zeroed): hold the sky perfectly
        // still instead of just slowing it, since some drift would still
        // read as "frozen but wrong" during a pause.
        float timeSpeed = Time.timeScale <= 0f
            ? 0f
            : baseTimeSpeed + PlayerMovement.Speed * speedInfluence;

        skyboxInstance.SetFloat(TimeSpeedId, timeSpeed);
    }
}
