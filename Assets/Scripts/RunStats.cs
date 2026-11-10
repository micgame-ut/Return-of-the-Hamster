using UnityEngine;

// Tracks the current Apollo's stats across levels. The totals are saved on
// each win and reset when the Apollo dies, which also moves on to the next
// Apollo number.
public class RunStats : MonoBehaviour
{
    private const string AttemptKey = "Attempt";
    private const string AsteroidsKey = "AsteroidsDestroyed";
    private const string PowerUpsKey = "PowerUpsPickedUp";
    private const string OverdriveKey = "OverdriveTime";
    private const string TimeKey = "TimeSurvived";
    private const string LevelsKey = "LevelsBeaten";

    [Tooltip("The base name. The attempt number is added after it (Apollo 3).")]
    [SerializeField] private string hamsterName = "Apollo";

    public int Attempt { get; private set; }
    public string ApolloName => $"{hamsterName} {Attempt}";
    public int AsteroidsDestroyed { get; private set; }
    public float OverdriveTime { get; private set; }
    public float TimeSurvived { get; private set; }
    public int PowerUpsPickedUp { get; private set; }
    public int LevelsBeaten { get; private set; }

    private void Awake()
    {
        Attempt = PlayerPrefs.GetInt(AttemptKey, 1);
        AsteroidsDestroyed = PlayerPrefs.GetInt(AsteroidsKey, 0);
        PowerUpsPickedUp = PlayerPrefs.GetInt(PowerUpsKey, 0);
        OverdriveTime = PlayerPrefs.GetFloat(OverdriveKey, 0f);
        TimeSurvived = PlayerPrefs.GetFloat(TimeKey, 0f);
        LevelsBeaten = PlayerPrefs.GetInt(LevelsKey, 0);
    }

    private void Update()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null || !gm.HasStarted || gm.HasWon) return;

        TimeSurvived += Time.deltaTime;
        if (gm.Player != null && gm.Player.InOverdrive)
            OverdriveTime += Time.deltaTime;
    }

    public void AddAsteroidDestroyed()
    {
        if (GameManager.Instance != null && GameManager.Instance.InTutorial) return;
        AsteroidsDestroyed++;
    }

    public void AddPowerUpPickedUp()
    {
        if (GameManager.Instance != null && GameManager.Instance.InTutorial) return;
        PowerUpsPickedUp++;
    }

    public void RecordWin()
    {
        LevelsBeaten++;
        SaveStats();
    }

    private void SaveStats()
    {
        PlayerPrefs.SetInt(AsteroidsKey, AsteroidsDestroyed);
        PlayerPrefs.SetInt(PowerUpsKey, PowerUpsPickedUp);
        PlayerPrefs.SetFloat(OverdriveKey, OverdriveTime);
        PlayerPrefs.SetFloat(TimeKey, TimeSurvived);
        PlayerPrefs.SetInt(LevelsKey, LevelsBeaten);
        PlayerPrefs.Save();
    }

    public void RecordDeath()
    {
        PlayerPrefs.SetInt(AttemptKey, Attempt + 1);

        // The next Apollo starts with no totals.
        PlayerPrefs.DeleteKey(AsteroidsKey);
        PlayerPrefs.DeleteKey(PowerUpsKey);
        PlayerPrefs.DeleteKey(OverdriveKey);
        PlayerPrefs.DeleteKey(TimeKey);
        PlayerPrefs.DeleteKey(LevelsKey);
        PlayerPrefs.Save();
    }
}
