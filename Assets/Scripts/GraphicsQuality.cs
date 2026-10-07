using UnityEngine;

// Sets the visual settings that differ between quality levels. Each setting has
// a High and a Low value; Low is used when the active level is lowQualityLevel.
// Applied once when the level starts.
public class GraphicsQuality : MonoBehaviour
{
    private static readonly int RayStepsId = Shader.PropertyToID("_Ray_Steps");

    [Tooltip("Position in Project Settings > Quality that uses the Low values (0 = top of the list, PC Low).")]
    [SerializeField] private int lowQualityLevel = 0;

    [Header("Sky")]
    [Tooltip("PC High. Samples per view ray through the nebula. Higher = smoother, softer clouds; each step costs GPU time.")]
    [SerializeField, Range(4, 48)] private int highRaySteps = 16;

    [Tooltip("PC Low. Fewer samples = cheaper sky (8 costs about half of 16), but clouds can look grainy or banded.")]
    [SerializeField, Range(4, 48)] private int lowRaySteps = 8;

    private void Start()
    {
        // Start, not Awake: SkyboxSpeedController swaps in its own copy of the sky in Awake,
        // so by now RenderSettings.skybox is that copy and the .mat asset stays untouched.
        Material sky = RenderSettings.skybox;
        if (sky == null) return;

        bool low = QualitySettings.GetQualityLevel() == lowQualityLevel;
        sky.SetFloat(RayStepsId, low ? lowRaySteps : highRaySteps);
    }
}
