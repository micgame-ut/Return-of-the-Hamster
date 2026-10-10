using TMPro;
using UnityEngine;

// Tracks this level's score: time points from LevelTimer progress plus points
// from events (AddPoints), and shows it on the HUD.
public class Score : MonoBehaviour
{
    [SerializeField] private float pointsPerSecond = 1f;
    [SerializeField] private float maxSpeedMultiplier = 2f;
    [SerializeField] private float comboWindow = 1.5f;
    [SerializeField] private float comboStepBonus = 50f;
    [SerializeField] private float comboStepGrowth = 0.5f;
    [SerializeField] private TMP_Text scoreText;

    public float Total { get; private set; }

    private float eventPoints;
    private int shownPoints = -1;
    private LevelTimer timer;
    private int comboCount;
    private float comboTimer;
    private float comboBonus;
    private PlayerMovement player;
    private PlayerTwins twins;

    private void Awake()
    {
        timer = GameManager.Instance.LevelTimer;
        player = GameManager.Instance.Player;
        twins = GameManager.Instance.PlayerTwins;
    }

    private void OnEnable()
    {
        if (player != null) player.OnHit += BreakCombo;
        if (twins != null) twins.OnBodyLost += BreakCombo;
    }

    private void OnDisable()
    {
        if (player != null) player.OnHit -= BreakCombo;
        if (twins != null) twins.OnBodyLost -= BreakCombo;
    }

    private void Update()
    {
        if (comboTimer > 0f)
        {
            comboTimer -= Time.deltaTime;
            if (comboTimer <= 0f) BreakCombo();
        }

        float timePoints = timer.Progress * timer.LevelTime * pointsPerSecond;
        Total = timePoints + eventPoints;

        int whole = Mathf.FloorToInt(Total);
        if (whole == shownPoints || scoreText == null) return;

        shownPoints = whole;
        scoreText.text = whole.ToString("D6");
    }

    private void BreakCombo()
    {
        comboCount = 0;
        comboBonus = 0f;
    }

    public void AddPoints(float amount)
    {
        GameManager gm = GameManager.Instance;
        if (gm.IsGameOver || gm.HasWon) return;

        float speedMultiplier = Mathf.Lerp(1f, maxSpeedMultiplier, PlayerMovement.Speed01);
        eventPoints += amount * speedMultiplier;
    }

    public void AddComboKill(float points)
    {
        AddPoints(points + comboBonus);
        comboBonus += comboStepBonus * (1f + comboStepGrowth * comboCount);
        comboCount++;
        comboTimer = comboWindow;
    }
}
