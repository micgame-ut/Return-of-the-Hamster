using UnityEngine;

// The one place the orbit's shape and the Game scene's layout are set. Gameplay
// (PlayerMovement, AsteroidSpawner, AsteroidBehavior) and the pattern builder
// (PatternGizmos) all read from here, so the builder's preview always matches play.
//
// The asset lives at Assets/Resources/OrbitSettings.asset and is found by name,
// so nothing needs it assigned in the Inspector.
[CreateAssetMenu(fileName = "OrbitSettings", menuName = "Return of the Hamster/Orbit Settings")]
public class OrbitSettings : ScriptableObject
{
    private const string ResourcePath = "OrbitSettings"; // Assets/Resources/OrbitSettings.asset

    [Header("Orbit")]
    [SerializeField] private float orbitRadius = 3f;                    // world units
    [SerializeField] private Vector2 orbitCenter = new Vector2(0f, 1f); // world x/y; keep the gameplay camera's x/y matching so the circle sits mid-screen

    [Header("Layout")]
    [SerializeField] private float playerZ = -4f;                            // PlayerMovement moves itself here
    [SerializeField] private Vector3 spawnerPosition = new Vector3(0f, 0f, 15f); // AsteroidSpawner moves itself here

    public float OrbitRadius => orbitRadius;
    public Vector2 OrbitCenter => orbitCenter;
    public float PlayerZ => playerZ;
    public Vector3 SpawnerPosition => spawnerPosition;

    // The same values measured from the spawner, which is the space patterns are built in
    public Vector2 OrbitCenterFromSpawner => orbitCenter - (Vector2)spawnerPosition;
    public float PlayerZFromSpawner => playerZ - spawnerPosition.z;

    private static OrbitSettings instance;

    public static OrbitSettings Instance
    {
        get
        {
            if (instance == null)
            {
                instance = Resources.Load<OrbitSettings>(ResourcePath);

                // Missing asset: fall back to the defaults above so nothing breaks outright
                if (instance == null)
                {
                    Debug.LogError("OrbitSettings asset not found at Assets/Resources/OrbitSettings.asset; using defaults.");
                    instance = CreateInstance<OrbitSettings>();
                }
            }
            return instance;
        }
    }

    private void OnValidate()
    {
        // Radius is divided by in PlayerMovement, so it can't be zero
        orbitRadius = Mathf.Max(0.01f, orbitRadius);
    }
}
