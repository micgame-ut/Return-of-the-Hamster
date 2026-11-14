using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Tracks this level's score: time points from LevelTimer progress plus points
// from events (AddPoints), and shows it on the HUD.
public class Score : MonoBehaviour
{
    public enum ScoreSource { Progress, Asteroids, CloseCalls, Waves, Overdrive, Previous }

    [Tooltip("Time points per second of levelTime. A full level gives levelTime times pointsPerSecond time points, however fast the player flies.")]
    [SerializeField] private float pointsPerSecond = 1f;
    [Tooltip("What points from AddPoints are multiplied by at max speed. At speed 0 it's x1, and in between it rises in a straight line. Doesn't affect the time score.")]
    [SerializeField] private float maxSpeedMultiplier = 2f;
    [Tooltip("Seconds after a kill to make the next one and keep the combo going. Each kill resets it.")]
    [SerializeField] private float comboWindow = 1.5f;
    [Tooltip("How much the combo bonus grows after the first kill.")]
    [SerializeField] private float comboStepBonus = 50f;
    [Tooltip("How much bigger each later step is, as a fraction of comboStepBonus (0.5 gives steps of 50, 75, 100...). 0 makes every step the same.")]
    [SerializeField] private float comboStepGrowth = 0.5f;
    [Tooltip("The HUD text the score is written to, as six zero-padded digits. Can be left empty: the score still counts, it just isn't shown.")]
    [SerializeField] private TMP_Text scoreText;
    [Tooltip("How big the HUD score snaps to when points are added through AddPoints. 1 turns the pulse off.")]
    [SerializeField] private float pulseScale = 1.15f;
    [Tooltip("Seconds to shrink back to normal size after a pulse.")]
    [SerializeField] private float pulseDuration = 0.15f;
    [Tooltip("Popup label for a wave cleared with no life lost. Empty shows just the number.")]
    [SerializeField] private string perfectWaveLabel = "PERFECT WAVE";
    [Tooltip("Popup label for any other wave clear.")]
    [SerializeField] private string waveClearLabel = "WAVE CLEAR";
    [Tooltip("Base points for a close call, before the combo bonus and speed multiplier.")]
    [SerializeField] private float closeCallPoints = 50f;
    [Tooltip("Popup label for a close call.")]
    [SerializeField] private string closeCallLabel = "CLOSE CALL";
    [Tooltip("Added to the multiplier for each active score power-up.")]
    [SerializeField] private float powerUpBonus = 2f;
    [Tooltip("Seconds each score power-up lasts. Each pickup has its own timer.")]
    [SerializeField] private float powerUpDuration = 10f;

    public float Total { get; private set; }
    public int LivesLostCount { get; private set; }
    public int ActivePowerUps => powerUpTimers.Count;
    public float NextPowerUpFraction => powerUpTimers.Count > 0 ? powerUpTimers[0] / powerUpDuration : 0f;
    public float PowerUpMultiplier => powerUpBonus * ActivePowerUps;
    private float TimePoints => timer.Progress * timer.LevelTime * pointsPerSecond - timePointsBase;
    public event Action<float, float, Vector3, string> OnPointsAdded; // base points, total multiplier, world position, label (null for none)

    private float eventPoints;
    // Points banked from the levels already won this run. Static, so it survives the scene load. Zeroed on Restart, Menu, Credits and Continue.
    public static float CarriedScore;
    // The time points the level started with, taken off so a level that resumes part-way starts from 0.
    private float timePointsBase;
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

    private void Start()
    {
        timePointsBase = TimePoints;
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

        Total = CarriedScore + TimePoints + eventPoints;

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
        if (gm.IsGameOver || gm.HasWon || gm.InTutorial) return;

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
        if (GameManager.Instance.InTutorial) return;

        eventPoints += amount;
        sourceTotals[(int)ScoreSource.Overdrive] += amount;
        pulseTimer = pulseDuration;
    }

    public float GetSourceTotal(ScoreSource source) =>
        source == ScoreSource.Progress ? TimePoints :
        source == ScoreSource.Previous ? CarriedScore :
        sourceTotals[(int)source];

    // Called on a win: this level's total becomes the carried score for the next one.
    public void Bank()
    {
        CarriedScore = Total;
    }

    private void ComboKill(float points, Vector3 position, string label, ScoreSource source)
    {
        if (GameManager.Instance.InTutorial) return;

        AddPoints(points + comboBonus, position, source, label);
        comboBonus += comboStepBonus * (1f + comboStepGrowth * comboCount);
        comboCount++;
        comboTimer = comboWindow;
    }
}
