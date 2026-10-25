using UnityEngine;

// Lives on a Magnet. The gameplay camera can't see the background behind the Magnet, so this moves the Magnet's
// mesh to the matching spot in front of the background camera, where Scene Color can see the sky and planets.
public class MagnetMirror : MonoBehaviour
{
    [Tooltip("Turn the effect so its top (the thick arch) points at the orbit's center, as seen on screen. Off = the material's Disk Angle is used as it is.")]
    [SerializeField] private bool faceCenter = true;
    [Tooltip("Degrees added to the turn. Use it to nudge the aim, or 180 to flip it.")]
    [SerializeField] private float angleOffset = 0f;

    private static readonly int DiskAngleId = Shader.PropertyToID("_Disk_Angle");
    private Camera gameplayCam;
    private Renderer meshRenderer;
    private MaterialPropertyBlock block;
    private Transform gameplayCamera;
    private Transform backgroundCamera;
    private Transform mesh;
    private Vector3 meshHome;  // where the mesh child normally sits, so it can be put back

    private void Awake()
    {
        GameManager manager = GameManager.Instance;
        if (manager != null)
        {
            gameplayCam = manager.GameplayCamera;
            if (gameplayCam != null) gameplayCamera = gameplayCam.transform;
            if (manager.EarthCamera != null) backgroundCamera = manager.EarthCamera.transform;
        }

        Renderer rend = GetComponentInChildren<Renderer>();
        if (rend != null && rend.transform != transform)
        {
            meshRenderer = rend;
            mesh = rend.transform;
            meshHome = mesh.localPosition;
        }

        block = new MaterialPropertyBlock();
    }

    private void LateUpdate()
    {
        if (gameplayCamera == null || backgroundCamera == null || mesh == null) return;

        Vector3 inCameraSpace = gameplayCamera.InverseTransformPoint(transform.position);
        mesh.position = backgroundCamera.TransformPoint(inCameraSpace);
        AimAtCenter();
    }

    // Turns the effect so its top points at the orbit's center, as seen on screen.
    private void AimAtCenter()
    {
        if (!faceCenter || gameplayCam == null || meshRenderer == null || OrbitSettings.Instance == null) return;

        OrbitSettings orbit = OrbitSettings.Instance;
        Vector3 centerWorld = new Vector3(orbit.OrbitCenter.x, orbit.OrbitCenter.y, orbit.PlayerZ);
        Vector3 toCenter = gameplayCam.WorldToScreenPoint(centerWorld) - gameplayCam.WorldToScreenPoint(transform.position);
        if (toCenter.x * toCenter.x + toCenter.y * toCenter.y < 1f) return;

        float phi = Mathf.Atan2(toCenter.y, toCenter.x) * Mathf.Rad2Deg;
        block.SetFloat(DiskAngleId, 90f - phi + angleOffset);
        meshRenderer.SetPropertyBlock(block, 0);
    }

    private void OnDisable()
    {
        if (mesh != null) mesh.localPosition = meshHome;
    }
}
