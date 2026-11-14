using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

// The end-of-level screens: the Apollo's name, their stats so far, and buttons.
// One component holds both the Game Over and the Win screen, each with its own
// panel and texts. Every button on either panel calls a method here.
public class EndScreen : MonoBehaviour
{
    // The parts each screen needs. Shows in the Inspector as a group per screen.
    [System.Serializable]
    private class ScreenParts
    {
        [Tooltip("The panel switched on to show this screen.")]
        public GameObject panel;
        [Tooltip("Text that gets the Apollo name.")]
        public TMP_Text hamsterNameText;
        [Tooltip("The text that shows this screen's score total.")]
        public TMP_Text scoreTotalText;
        [Tooltip("The parent the score breakdown rows are created under.")]
        public Transform scoreRowContainer;
    }

    [Tooltip("The Game Over screen's parts.")]
    [SerializeField] private ScreenParts gameOver;
    [Tooltip("The Win screen's parts.")]
    [SerializeField] private ScreenParts win;
    [Tooltip("The name page shown after a run that made the leaderboard.")]
    [SerializeField] private NameEntry nameEntry;
    [Tooltip("The leaderboard window opened by the Scores buttons, and after a name is submitted.")]
    [SerializeField] private LeaderboardPanel leaderboard;
    [Tooltip("The score HUD object, hidden when an end screen is shown. Optional.")]
    [SerializeField] private GameObject scoreHud;
    [Tooltip("The scene the Menu button loads, and Next Level's fallback. The menu lives in Level 1. Must match the scene file's name and be in Build Settings.")]
    [SerializeField] private string menuSceneName = "Level_1";
    [Tooltip("Show the Continue button on the Game Over screen. Turn off for the real release.")]
    [SerializeField] private bool allowContinue = true;
    [Tooltip("The Continue button on the Game Over panel. Hidden when Allow Continue is off. Optional.")]
    [SerializeField] private GameObject continueButton;

    public static event System.Action OnShown;
    public static event System.Action OnLeaderboardOpened;
    public static event System.Action OnLeaderboardClosed;
    public static event System.Action OnQuitClicked;

    // Set by Credits so the menu scene opens the credits when it loads. Static, so it survives the scene load.
    public static bool OpenCreditsOnMenu;

    private ScoreBreakdown scoreBreakdown;
    private int finalScore;   // this run's score, kept from the end of the score breakdown until the name is submitted
    private bool quitting;

    private void Awake()
    {
        gameOver.panel.SetActive(false);
        win.panel.SetActive(false);
        if (nameEntry != null) nameEntry.gameObject.SetActive(false);
        if (leaderboard != null) leaderboard.gameObject.SetActive(false);
        scoreBreakdown = GetComponent<ScoreBreakdown>();
    }

    private void OnEnable()
    {
        ScoreBreakdown.OnFinished += HandleBreakdownFinished;
        if (nameEntry != null) nameEntry.OnSubmitted += HandleNameSubmitted;
    }

    private void OnDisable()
    {
        ScoreBreakdown.OnFinished -= HandleBreakdownFinished;
        if (nameEntry != null) nameEntry.OnSubmitted -= HandleNameSubmitted;
    }

    private void HandleBreakdownFinished()
    {
        GameManager gm = GameManager.Instance;
        Score score = gm != null ? gm.Score : null;
        if (score == null || nameEntry == null) return;

        bool lastLevel = SceneManager.GetActiveScene().buildIndex + 1 >= SceneManager.sceneCountInBuildSettings;
        if (!gm.IsGameOver && !lastLevel) return;

        finalScore = Mathf.FloorToInt(score.Total);
        if (LeaderboardStore.Qualifies(finalScore)) nameEntry.gameObject.SetActive(true);
    }

    private void HandleNameSubmitted(string name)
    {
        int rank = LeaderboardStore.Submit(name, finalScore);
        ShowLeaderboard(rank);
    }

    public void ShowGameOver()
    {
        if (continueButton != null) continueButton.SetActive(allowContinue);
        Show(gameOver);
    }
    public void ShowWin() => Show(win);

    private void Show(ScreenParts parts)
    {
        FillName(parts);
        parts.panel.SetActive(true);
        OnShown?.Invoke();
        if (scoreHud != null) scoreHud.SetActive(false);
        if (scoreBreakdown != null) scoreBreakdown.Play(parts.scoreTotalText, parts.scoreRowContainer);
    }

    private void FillName(ScreenParts parts)
    {
        GameManager gm = GameManager.Instance;
        RunStats stats = gm != null ? gm.RunStats : null;
        if (stats == null) return;

        parts.hamsterNameText.text = stats.ApolloName;
    }

    // Scores button on either panel, and the leaderboard opened after a name is submitted.
    public void ShowLeaderboard(int highlightRank = -1)
    {
        leaderboard.Show(highlightRank);
        OnLeaderboardOpened?.Invoke();
    }

    // Opens the leaderboard with no highlight. The Scores buttons call this one, because a button's click can't pass the rank.
    public void ShowLeaderboardPlain() => ShowLeaderboard();

    public void CloseLeaderboard()
    {
        leaderboard.Close();
        OnLeaderboardClosed?.Invoke();
    }

    public void Restart()
    {
        Score.CarriedScore = 0f;
        GameManager.SkipMenuOnce = true;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void Continue()
    {
        GameManager gm = GameManager.Instance;
        if (gm != null && gm.LevelTimer != null) LevelTimer.StartProgress = gm.LevelTimer.Progress;
        Restart();
    }

    public void NextLevel()
    {
        // The menu has no GameManager to unfreeze time, so do it here.
        Time.timeScale = 1f;
        if (GameManager.Instance != null && GameManager.Instance.Score != null) GameManager.Instance.Score.Bank();

        int next = SceneManager.GetActiveScene().buildIndex + 1;
        if (next < SceneManager.sceneCountInBuildSettings)
            SceneManager.LoadScene(next);
        else
            SceneManager.LoadScene(menuSceneName);
    }

    public void Menu()
    {
        // The menu scene has no GameManager to unfreeze time, so do it here.
        Time.timeScale = 1f;
        Score.CarriedScore = 0f;
        SceneManager.LoadScene(menuSceneName);

        MusicManager.Instance.GoToTitle();
    }

    public void Credits()
    {
        // The menu scene has no GameManager to unfreeze time, so do it here.
        Time.timeScale = 1f;
        Score.CarriedScore = 0f;
        OpenCreditsOnMenu = true;
        SceneManager.LoadScene(menuSceneName);

        MusicManager.Instance.GoToTitle();
    }

    public void Quit()
    {
        if (quitting) return;
        quitting = true;
        OnQuitClicked?.Invoke();
        StartCoroutine(QuitAfterSound());
    }

    private IEnumerator QuitAfterSound()
    {
        yield return new WaitForSecondsRealtime(UISFX.QuitSoundLength);
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
