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
        public GameObject panel;
        public TMP_Text hamsterNameText;
        public TMP_Text statsText;
        public bool showProgress;
    }

    [SerializeField] private ScreenParts gameOver;
    [SerializeField] private ScreenParts win;
    [SerializeField] private string menuSceneName = "Menu";

    private void Awake()
    {
        gameOver.panel.SetActive(false);
        win.panel.SetActive(false);
    }

    public void ShowGameOver() => Show(gameOver);
    public void ShowWin() => Show(win);

    private void Show(ScreenParts parts)
    {
        FillStats(parts);
        parts.panel.SetActive(true);

        // PlayerMovement locked the cursor for flying; the buttons need it back.
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void FillStats(ScreenParts parts)
    {
        GameManager gm = GameManager.Instance;
        RunStats stats = gm != null ? gm.RunStats : null;
        if (stats == null) return;

        parts.hamsterNameText.text = stats.ApolloName;

        int minutes = (int)(stats.TimeSurvived / 60f);
        int seconds = (int)(stats.TimeSurvived % 60f);
        int progress = gm.LevelTimer != null ? Mathf.FloorToInt(gm.LevelTimer.Progress * 100f) : 0;

        // Values start at the same point on every line, so they form a column.
        string col = "<pos=70%>";
        string text =
            $"Asteroids destroyed{col}{stats.AsteroidsDestroyed}\n" +
            $"Power-ups{col}{stats.PowerUpsPickedUp}\n" +
            $"Time in overdrive{col}{stats.OverdriveTime:0.0} s\n" +
            $"Time survived{col}{minutes}:{seconds:00}";
        if (parts.showProgress)
            text += $"\nProgress{col}{progress}%";
        parts.statsText.text = text;
    }

    public void Restart()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void NextLevel()
    {
        // The menu has no GameManager to unfreeze time, so do it here.
        Time.timeScale = 1f;

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
        SceneManager.LoadScene(menuSceneName);
    }

    public void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
