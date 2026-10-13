using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Tracks this level's score: time points from LevelTimer progress plus points
// from events (AddPoints), and shows it on the HUD.
public class Score : MonoBehaviour
{
    public enum ScoreSource { Progress, Asteroids, CloseCalls, Waves, Overdrive }

    [SerializeField] private float pointsPerSecond = 1f;
    [SerializeField] private float maxSpeedMultiplier = 2f;
    [SerializeField] private float comboWindow = 1.5f;
    [SerializeField] private float comboStepBonus = 50f;
    [SerializeField] private float comboStepGrowth = 0.5f;
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private float pulseScale = 1.15f;
    [SerializeField] private float pulseDuration = 0.15f;
    [SerializeField] private string perfectWaveLabel = "PERFECT WAVE";
    [SerializeField] private string waveClearLabel = "WAVE CLEAR";
    [SerializeField] private float closeCallPoints = 50f;
    [SerializeField] private string closeCallLabel = "CLOSE CALL";
    [SerializeField] private float powerUpBonus = 2f;
    [SerializeField] private float powerUpDuration = 10f;

    public float Total { get; private set; }
    public int LivesLostCount { get; private set; }
    public int ActivePowerUps => powerUpTimers.Count;
    public float NextPowerUpFraction => powerUpTimers.Count > 0 ? powerUpTimers[0] / powerUpDuration : 0f;
    public float PowerUpMultiplier => powerUpBonus * ActivePowerUps;
    private float TimePoints => timer.Progress * timer.LevelTime * pointsPerSecond;
    public event Action<float, float, Vector3, string> OnPointsAdded; // base points, total multiplier, world position, label (null for none)

    private float eventPoints;
    private int shownPoints = -1;
    private LevelTimer timer;
    private int comboCount;
    private float comboTimer;
    private float comboBonus;
    private float pulseTimer;
    private readonly List<float> powerUpTimers = new List<float>();
    private readonly float[] sourceTotals = new float[Enum.GetValues(typeof(ScoreSource)).Length];
    private PlayerMovement player;
    private PlayerHealth health;

    private void Awake()
    {
        timer = GameManager.Instance.LevelTimer;
        player = GameManager.Instance.Player;
        health = GameManager.Instance.PlayerHealth;
    }

    private void OnEnable()
    {
        if (health != null) health.OnLifeLost += HandleLifeLost;
    }

    private void OnDisable()
    {
        if (health != null) health.OnLifeLost -= HandleLifeLost;
    }

    private void Update()
    {
        if (comboTimer > 0f)
        {
            comboTimer -= Time.deltaTime;
            if (comboTimer <= 0f) BreakCombo();
        }

        if (pulseTimer > 0f)
        {
            pulseTimer -= Time.unscaledDeltaTime;
            if (scoreText != null)
                scoreText.rectTransform.localScale = Vector3.one * Mathf.Lerp(1f, pulseScale, pulseTimer / pulseDuration);
        }

        for (int i = powerUpTimers.Count - 1; i >= 0; i--)
        {
            powerUpTimers[i] -= Time.deltaTime;
            if (powerUpTimers[i] <= 0f) powerUpTimers.RemoveAt(i);
        }

        Total = TimePoints + eventPoints;

        int whole = Mathf.FloorToInt(Total);
        if (whole == shownPoints || scoreText == null) return;

        shownPoints = whole;
        scoreText.text = whole.ToString("D6");
    }

    private void HandleLifeLost()
    {
        LivesLostCount++;
        BreakCombo();
    }

    private void BreakCombo()
    {
        comboCount = 0;
        comboBonus = 0f;
    }

    public void AddPoints(float amount, Vector3 position, ScoreSource source, string label = null)
    {
        float speedMultiplier = Mathf.Lerp(1f, maxSpeedMultiplier, PlayerMovement.Speed01);
        float multiplier = speedMultiplier + PowerUpMultiplier;
        Award(amount, multiplier, position, label, source);
    }

    public void AddBonusPoints(float amount, Vector3 position, string label, ScoreSource source)
    {
        Award(amount, 1f, position, label, source);
    }

    public void AddWaveClear(float points, bool perfect)
    {
        Vector3 position = player != null ? player.transform.position : Vector3.zero;
        AddBonusPoints(points, position, perfect ? perfectWaveLabel : waveClearLabel, ScoreSource.Waves);
    }

    private void Award(float amount, float multiplier, Vector3 position, string label, ScoreSource source)
    {
        GameManager gm = GameManager.Instance;
        if (gm.IsGameOver || gm.HasWon) return;

        eventPoints += amount * multiplier;
        sourceTotals[(int)source] += amount * multiplier;
        OnPointsAdded?.Invoke(amount, multiplier, position, label);
        pulseTimer = pulseDuration;
    }

    public void AddComboKill(float points, Vector3 position)
    {
        ComboKill(points, position, null, ScoreSource.Asteroids);
    }

    public void AddCloseCall(Vector3 position)
    {
        ComboKill(closeCallPoints, position, closeCallLabel, ScoreSource.CloseCalls);
    }

    public void AddMultiplierPowerUp()
    {
        powerUpTimers.Add(powerUpDuration);
    }

    // Banks the overdrive tally. No game-over guard: it's called once per overdrive,
    // including right as the level ends.
    public void AddOverdriveBonus(float amount)
    {
        eventPoints += amount;
        sourceTotals[(int)ScoreSource.Overdrive] += amount;
        pulseTimer = pulseDuration;
    }

    public float GetSourceTotal(ScoreSource source) =>
        source == ScoreSource.Progress ? TimePoints : sourceTotals[(int)source];

    private void ComboKill(float points, Vector3 position, string label, ScoreSource source)
    {
        AddPoints(points + comboBonus, position, source, label);
        comboBonus += comboStepBonus * (1f + comboStepGrowth * comboCount);
        comboCount++;
        comboTimer = comboWindow;
    }
}
