using UnityEngine;

// Single place for scene-wide references, so other scripts don't each need
// their own Inspector fields or Camera.main lookups. Runs before other scripts
// so Instance is ready by the time their Awake/Start runs.
[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [SerializeField] private PlayerMovement player;
    [SerializeField] private Camera gameplayCamera;
    [SerializeField] private Renderer earth;
    [SerializeField] private Camera earthCamera;
    [SerializeField] private LevelTimer levelTimer;

    public PlayerMovement Player => player;
    public Camera GameplayCamera => gameplayCamera;
    public Renderer Earth => earth;
    public Camera EarthCamera => earthCamera;
    public LevelTimer LevelTimer => levelTimer;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Multiple GameManagers in the scene; removing the extra one.", this);
            Destroy(this);
            return;
        }
        Instance = this;

        // Level-wide scripts normally sit on this same object, so fill them in if unassigned.
        if (levelTimer == null) levelTimer = GetComponent<LevelTimer>();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
