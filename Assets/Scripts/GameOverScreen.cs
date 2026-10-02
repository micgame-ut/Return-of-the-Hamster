using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

// The screen shown after the death sequence: the Apollo's name, this run's
// stats, and buttons to restart, go to the menu, or quit.
public class GameOverScreen : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private TMP_Text hamsterNameText;
    [SerializeField] private TMP_Text statsText;
    [SerializeField] private string menuSceneName = "Menu";

    private void Awake()
    {
        panel.SetActive(false);
    }

    public void Show()
    {
        FillStats();
        panel.SetActive(true);

        // PlayerMovement locked the cursor for flying; the buttons need it back.
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void FillStats()
    {
        GameManager gm = GameManager.Instance;
        RunStats stats = gm != null ? gm.RunStats : null;
        if (stats == null) return;

        hamsterNameText.text = stats.ApolloName;

        int minutes = (int)(stats.TimeSurvived / 60f);
        int seconds = (int)(stats.TimeSurvived % 60f);
        int progress = gm.LevelTimer != null ? Mathf.FloorToInt(gm.LevelTimer.Progress * 100f) : 0;

        // Values start at the same point on every line, so they form a column.
        string col = "<pos=70%>";
        statsText.text =
            $"Asteroids destroyed{col}{stats.AsteroidsDestroyed}\n" +
            $"Power-ups{col}{stats.PowerUpsPickedUp}\n" +
            $"Time in overdrive{col}{stats.OverdriveTime:0.0} s\n" +
            $"Time survived{col}{minutes}:{seconds:00}\n" +
            $"Progress{col}{progress}%";
    }

    public void Restart()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
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
