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

    [SerializeField] private PlayerMovement player;
    [SerializeField] private Camera gameplayCamera;
    [SerializeField] private Renderer earth;
    [SerializeField] private Camera earthCamera;
    [SerializeField] private LevelTimer levelTimer;
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private PlayerInvincibility playerInvincibility;
    [SerializeField] private PlayerShell playerShell;
    [SerializeField] private PlayerTwins playerTwins;
    [SerializeField] private CloseCallZone closeCallZone;
    [SerializeField] private RunStats runStats;
    [SerializeField] private Score score;
    [SerializeField] private EndScreen endScreen;
    [SerializeField] private bool showOrbitRing = true;
    [SerializeField] private GameObject hud;
    [SerializeField] private bool startWithMenu; // tick on Level 1 only; other levels start straight away

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
    public bool ShowOrbitRing => showOrbitRing;
    public bool IsGameOver { get; private set; }
    public bool HasWon { get; private set; }
    public event Action OnGameOver;
    public event Action OnWin;

    // Set by EndScreen's Restart so the reloaded level skips the menu. Static, so it survives the reload.
    public static bool SkipMenuOnce;

    public bool HasStarted { get; private set; }
    public float TimeSinceStart => HasStarted ? Time.time - startTime : 0f; // 0 until StartGame
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

        // No menu on this level, or a restart: start straight away.
        if (!startWithMenu || SkipMenuOnce) StartGame();
        else if (hud != null) hud.SetActive(false);
        SkipMenuOnce = false;
    }

    private void Update()
    {
        if (!HasWon && !IsGameOver && LevelCleared) Win();
    }

    public void StartGame()
    {
        if (HasStarted) return;

        HasStarted = true;
        startTime = Time.time;
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
