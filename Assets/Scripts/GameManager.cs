using System;
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

    [Tooltip("The player ship.")]
    [SerializeField] private PlayerMovement player;
    [Tooltip("The main camera. Used to despawn asteroids once they're behind it, and to sort a pattern's asteroids by distance.")]
    [SerializeField] private Camera gameplayCamera;
    [Tooltip("The earth model that approaches as the level goes on.")]
    [SerializeField] private Renderer earth;
    [Tooltip("The camera that renders the earth.")]
    [SerializeField] private Camera earthCamera;
    [Tooltip("The level's timer. Can be left empty if LevelTimer is on the same GameObject.")]
    [SerializeField] private LevelTimer levelTimer;
    [Tooltip("The player's lives. Can be left empty if PlayerHealth is on the player object.")]
    [SerializeField] private PlayerHealth playerHealth;
    [Tooltip("The player's invincibility power-up state. Can be left empty if PlayerInvincibility is on the player object.")]
    [SerializeField] private PlayerInvincibility playerInvincibility;
    [Tooltip("The player's shield shell and its hitbox. Can be left empty if PlayerShell is on the player object.")]
    [SerializeField] private PlayerShell playerShell;
    [Tooltip("The twin power-up on the player. Can be left empty if PlayerTwins is on the player object.")]
    [SerializeField] private PlayerTwins playerTwins;
    [Tooltip("The close-call range on the player. Can be left empty if CloseCallZone is on the player object.")]
    [SerializeField] private CloseCallZone closeCallZone;
    [Tooltip("This run's stats and the Apollo number. Can be left empty if RunStats is on the same GameObject.")]
    [SerializeField] private RunStats runStats;
    [Tooltip("This level's score and its HUD counter. Can be left empty if Score is on the same GameObject.")]
    [SerializeField] private Score score;
    [Tooltip("The EndScreen component on the EndScreen canvas, which holds both the Game Over and Win screens. Must be assigned.")]
    [SerializeField] private EndScreen endScreen;
    [Tooltip("Accessibility setting: whether OrbitRing draws the orbit ring and its danger arcs. Read once when the level starts.")]
    [SerializeField] private bool showOrbitRing = true;
    [Tooltip("The HUD. Hidden while the menu shows, shown by StartGame. Optional. Keep the menu buttons out of it.")]
    [SerializeField] private GameObject hud;
    [Tooltip("Tick on Level 1 only: the level waits at the menu until MenuTransition calls StartGame. Off = starts straight away.")]
    [SerializeField] private bool startWithMenu;
    [Tooltip("This level's timing and finish planet. Empty = the level keeps the values already set in the scene.")]
    [SerializeField] private LevelConfig levelConfig;

    public PlayerMovement Player => player;
    public Camera GameplayCamera => gameplayCamera;
    public Renderer Earth => earth;
    public Camera EarthCamera => earthCamera;
    public LevelTimer LevelTimer => levelTimer;
    public PlayerHealth PlayerHealth => playerHealth;
    public PlayerInvincibility PlayerInvincibility => playerInvincibility;
    public PlayerShell PlayerShell => playerShell;
    public PlayerTwins PlayerTwins => playerTwins;
    public CloseCallZone CloseCallZone => closeCallZone;
    public RunStats RunStats => runStats;
    public Score Score => score;
    public LevelConfig LevelConfig => levelConfig;
    public bool ShowOrbitRing => showOrbitRing;
    public bool IsGameOver { get; private set; }
    public bool HasWon { get; private set; }
    public event Action OnGameOver;
    public event Action OnWin;

    // Set by EndScreen's Restart so the reloaded level skips the menu. Static, so it survives the reload.
    public static bool SkipMenuOnce;

    public bool HasStarted { get; private set; }
    public bool InTutorial { get; private set; }
    public bool IsFlying => HasStarted || InTutorial;   // the world is alive: input, starfield, flame, pose
    public event Action OnTutorialStarted;
    public float TimeSinceStart => IsFlying ?Time.time - startTime : 0f; // 0 until StartGame
    public event Action OnGameStarted;
    private float startTime;

    // Won once the earth has arrived and nothing spawned is still flying.
    // A boss fight would add its own condition here.
    private bool LevelCleared => levelTimer != null && levelTimer.IsComplete &&
                                 AsteroidBehavior.Active.Count == 0 && PowerUp.Active.Count == 0;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Multiple GameManagers in the scene; removing the extra one.", this);
            Destroy(this);
            return;
        }
        Instance = this;

        // Time scale is global and survives scene reloads, so start each scene unfrozen.
        Time.timeScale = 1f;

        // Level-wide scripts normally sit on this same object, so fill them in if unassigned.
        if (levelTimer == null) levelTimer = GetComponent<LevelTimer>();
        if (runStats == null) runStats = GetComponent<RunStats>();
        if (score == null) score = GetComponent<Score>();

        // Player scripts sit on the player object, so look there instead.
        if (playerHealth == null && player != null) playerHealth = player.GetComponent<PlayerHealth>();
        if (playerInvincibility == null && player != null) playerInvincibility = player.GetComponent<PlayerInvincibility>();
        if (playerShell == null && player != null) playerShell = player.GetComponent<PlayerShell>();
        if (playerTwins == null && player != null) playerTwins = player.GetComponent<PlayerTwins>();
        if (closeCallZone == null && player != null) closeCallZone = player.GetComponent<CloseCallZone>();

        ApplyLevelConfig();

        // No menu on this level, or a restart: start straight away.
#if UNITY_EDITOR
        if (PatternTest.Active) SkipMenuOnce = true; // Test This Pattern skips the menu
#endif
        if (!startWithMenu || SkipMenuOnce) StartGame();
        else if (hud != null) hud.SetActive(false);
        SkipMenuOnce = false;
    }

    private void ApplyLevelConfig()
    {
        if (levelConfig == null) return;

        if (levelTimer != null) levelTimer.SetLevelTime(levelConfig.levelTime);

        if (earth == null) return;
        if (levelConfig.planetMaterial != null) earth.sharedMaterial = levelConfig.planetMaterial;
        earth.transform.SetLocalPositionAndRotation(levelConfig.planetPosition, Quaternion.Euler(levelConfig.planetRotation));
        earth.transform.localScale = levelConfig.planetScale;
    }

    private void Update()
    {
        if (!HasWon && !IsGameOver && LevelCleared) Win();
    }

    public void StartTutorial()
    {
        if (HasStarted || InTutorial) return;

        InTutorial = true;
        startTime = Time.time;
        OnTutorialStarted?.Invoke();
    }

    public void StartGame()
    {
        if (HasStarted) return;

        if (!InTutorial) startTime = Time.time;
        InTutorial = false;
        HasStarted = true;
        if (hud != null) hud.SetActive(true);
        OnGameStarted?.Invoke();
    }

    public void GameOver()
    {
        if (IsGameOver || HasWon) return;

        IsGameOver = true;
        Time.timeScale = 0f;
        MusicManager.Instance.TrailOffAndStop();
        if (runStats != null) runStats.RecordDeath();
        OnGameOver?.Invoke();
    }

    public void Win()
    {
        if (IsGameOver || HasWon) return;

        HasWon = true;
        Time.timeScale = 0f;
        MusicManager.Instance.StopLevelMusic();
        if (runStats != null) runStats.RecordWin();
        if (endScreen != null) endScreen.ShowWin();
        OnWin?.Invoke();
    }

    public void DeathSequenceFinished()
    {
        if (!IsGameOver) return;

        if (endScreen != null) endScreen.ShowGameOver();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
