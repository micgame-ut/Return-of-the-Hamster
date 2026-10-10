using TMPro;
using UnityEngine;

// Tracks this level's score: time points from LevelTimer progress plus points
// from events (AddPoints), and shows it on the HUD.
public class Score : MonoBehaviour
{
    [SerializeField] private float pointsPerSecond = 1f;
    [SerializeField] private float maxSpeedMultiplier = 2f;
    [SerializeField] private TMP_Text scoreText;

    public float Total { get; private set; }

    private float eventPoints;
    private int shownPoints = -1;
    private LevelTimer timer;

    private void Awake()
    {
        timer = GameManager.Instance.LevelTimer;
    }

    private void Update()
    {
        float timePoints = timer.Progress * timer.LevelTime * pointsPerSecond;
        Total = timePoints + eventPoints;

        int whole = Mathf.FloorToInt(Total);
        if (whole == shownPoints || scoreText == null) return;

        shownPoints = whole;
        scoreText.text = whole.ToString("D6");
    }

    public void AddPoints(float amount)
    {
        GameManager gm = GameManager.Instance;
        if (gm.IsGameOver || gm.HasWon) return;

        float speedMultiplier = Mathf.Lerp(1f, maxSpeedMultiplier, PlayerMovement.Speed01);
        eventPoints += amount * speedMultiplier;
    }
}
