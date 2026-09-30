using UnityEngine;

// Single place for scene-wide references and the level's end state (game over
// or won), so other scripts don't each need their own Inspector fields or
// Camera.main lookups. Runs before other scripts so Instance is ready by the
// time their Awake/Start runs.
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
    [SerializeField] private PlayerHealth playerHealth;

    public PlayerMovement Player => player;
    public Camera GameplayCamera => gameplayCamera;
    public Renderer Earth => earth;
    public Camera EarthCamera => earthCamera;
    public LevelTimer LevelTimer => levelTimer;
    public PlayerHealth PlayerHealth => playerHealth;
    public bool IsGameOver { get; private set; }
    public bool HasWon { get; private set; }

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

        // Player scripts sit on the player object, so look there instead.
        if (playerHealth == null && player != null) playerHealth = player.GetComponent<PlayerHealth>();
    }

    public void GameOver()
    {
        if (IsGameOver || HasWon) return;

        IsGameOver = true;
        Debug.Log("Game over");
    }

    public void Win()
    {
        if (IsGameOver || HasWon) return;

        HasWon = true;
        Debug.Log("Level complete");
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
