using TMPro;
using UnityEngine;

// Counts points while the player is in overdrive and shows the running tally at a fixed
// HUD spot. When overdrive ends, the tally is banked into Score, pops, then rises and fades.
public class OverdriveTally : MonoBehaviour
{
    private enum State { Idle, Counting, Ending }

    [Tooltip("The text at the fixed HUD spot.")]
    [SerializeField] private TMP_Text tallyText;
    [Tooltip("Shown in front of the number, for example OVERDRIVE +70.")]
    [SerializeField] private string label = "OVERDRIVE";
    [Tooltip("How fast the tally grows while in overdrive.")]
    [SerializeField] private float overdrivePointsPerSecond = 20f;
    [Tooltip("Smaller tallies are banked but never shown.")]
    [SerializeField] private float minShownTally = 10f;
    [Tooltip("How big the text snaps to when overdrive ends.")]
    [SerializeField] private float exitScale = 1.4f;
    [Tooltip("The flash color when overdrive ends.")]
    [SerializeField] private Color exitColor = Color.yellow;
    [Tooltip("Seconds for the pop to ease back to normal.")]
    [SerializeField] private float emphasisTime = 0.3f;
    [Tooltip("Canvas units it rises while fading.")]
    [SerializeField] private float riseDistance = 60f;
    [Tooltip("Seconds to rise and fade out.")]
    [SerializeField] private float fadeTime = 0.8f;

    private State state = State.Idle;
    private float tally;
    private int shownTally = -1;
    private float endTimer;
    private Vector2 startPosition;
    private Color startColor;
    private PlayerMovement player;
    private Score score;
    private bool levelEnded;

    private void Awake()
    {
        GameManager gm = GameManager.Instance;
        player = gm != null ? gm.Player : null;
        score = gm != null ? gm.Score : null;

        if (tallyText != null)
        {
            startPosition = tallyText.rectTransform.anchoredPosition;
            startColor = tallyText.color;
            tallyText.gameObject.SetActive(false);
        }
    }

    private void Update()
    {
        bool inOverdrive = !levelEnded && player != null && player.InOverdrive;

        if (inOverdrive)
        {
            if (state != State.Counting) StartCounting();
            tally += overdrivePointsPerSecond * Time.deltaTime;
            ShowTally();
        }
        else if (state == State.Counting)
        {
            Bank();
        }

        if (state == State.Ending) Animate();
    }

    // Snaps to exitScale and exitColor, eases back over emphasisTime, then rises and fades.
    private void Animate()
    {
        endTimer += Time.unscaledDeltaTime;
        RectTransform rect = tallyText.rectTransform;

        if (endTimer < emphasisTime)
        {
            float pop = 1f - endTimer / emphasisTime;
            rect.localScale = Vector3.one * Mathf.Lerp(1f, exitScale, pop);
            tallyText.color = Color.Lerp(startColor, exitColor, pop);
            return;
        }

        float t = (endTimer - emphasisTime) / fadeTime;
        if (t >= 1f)
        {
            tallyText.gameObject.SetActive(false);
            state = State.Idle;
            return;
        }

        rect.localScale = Vector3.one;
        rect.anchoredPosition = startPosition + Vector2.up * (riseDistance * t);
        Color faded = startColor;
        faded.a = startColor.a * (1f - t);
        tallyText.color = faded;
    }

    private void OnEnable()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null) return;
        gm.OnGameOver += HandleLevelEnd;
        gm.OnWin += HandleLevelEnd;
    }

    private void OnDisable()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null) return;
        gm.OnGameOver -= HandleLevelEnd;
        gm.OnWin -= HandleLevelEnd;
    }

    // Game over or win: bank whatever was counting, and stop counting for the rest of the level.
    private void HandleLevelEnd()
    {
        if (state == State.Counting) Bank();
        levelEnded = true;
    }

    // A fresh tally: any old one still fading vanishes, and the text goes back to its resting look.
    private void StartCounting()
    {
        state = State.Counting;
        tally = 0f;
        shownTally = -1;
        if (tallyText == null) return;

        tallyText.gameObject.SetActive(false);
        tallyText.rectTransform.anchoredPosition = startPosition;
        tallyText.rectTransform.localScale = Vector3.one;
        tallyText.color = startColor;
    }

    private void ShowTally()
    {
        int whole = Mathf.FloorToInt(tally);
        if (whole == shownTally || whole < minShownTally || tallyText == null) return;

        shownTally = whole;
        tallyText.text = $"{label} +{whole}";
        tallyText.gameObject.SetActive(true);
    }

    // Hands the tally to Score, then pops it if it was big enough to show.
    private void Bank()
    {
        if (score != null && tally > 0f) score.AddOverdriveBonus(tally);

        if (tally >= minShownTally && tallyText != null)
        {
            state = State.Ending;
            endTimer = 0f;
        }
        else
        {
            state = State.Idle;
            if (tallyText != null) tallyText.gameObject.SetActive(false);
        }
    }
}
