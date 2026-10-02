using UnityEngine;

// Tracks this run's stats (the current Apollo) for the game-over screen, and
// saves the Apollo number so each death moves on to the next one.
public class RunStats : MonoBehaviour
{
    private const string AttemptKey = "Attempt";

    [SerializeField] private string hamsterName = "Apollo";

    public int Attempt { get; private set; }
    public string ApolloName => $"{hamsterName} {Attempt}";
    public int AsteroidsDestroyed { get; private set; }
    public float OverdriveTime { get; private set; }
    public float TimeSurvived { get; private set; }
    public int PowerUpsPickedUp { get; private set; }

    private void Awake()
    {
        Attempt = PlayerPrefs.GetInt(AttemptKey, 1);
    }

    private void Update()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null || gm.HasWon) return;

        TimeSurvived += Time.deltaTime;
        if (gm.Player != null && gm.Player.InOverdrive)
            OverdriveTime += Time.deltaTime;
    }

    public void AddAsteroidDestroyed()
    {
        AsteroidsDestroyed++;
    }

    public void AddPowerUpPickedUp()
    {
        PowerUpsPickedUp++;
    }

    public void RecordDeath()
    {
        PlayerPrefs.SetInt(AttemptKey, Attempt + 1);
        PlayerPrefs.Save();
        Debug.Log($"{ApolloName}: {AsteroidsDestroyed} asteroids, {PowerUpsPickedUp} power-ups, {OverdriveTime:0.0}s overdrive, {TimeSurvived:0.0}s survived");
    }
}
