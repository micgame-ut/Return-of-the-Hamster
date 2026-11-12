using System.Collections;
using TMPro;
using UnityEngine;

// Runs the optional tutorial: one prompt at a time, each waiting for the player to do the thing,
// then hands over to the real level with GameManager.StartGame.
[DisallowMultipleComponent]
public class TutorialManager : MonoBehaviour
{
    public enum Goal { Orbit, Speed, SideBoost, ForwardBoost, Avoid, Smash, Collect }

    [System.Serializable]
    public class Stage
    {
        [Tooltip("The text shown while this stage runs.")]
        public string prompt;
        [Tooltip("What the player has to do to finish the stage.")]
        public Goal goal;
        [Tooltip("Orbit: degrees to travel round the orbit. Speed: the fraction of top speed to reach, 0 to 1. SideBoost and ForwardBoost: how many boosts. Avoid, Smash and Collect: not used.")]
        public float amount = 1f;
        [Tooltip("Avoid, Smash and Collect: the prefab spawned for this stage (a hazard, a smashable asteroid, a power-up). Not used by the other goals.")]
        public GameObject spawnPrefab;
    }

    [Tooltip("The stages, run in order.")]
    [SerializeField] private Stage[] stages;
    [Tooltip("The text the prompts are shown in.")]
    [SerializeField] private TMP_Text promptText;
    [Tooltip("Seconds to wait after a stage is done before the next prompt appears.")]
    [SerializeField, Min(0f)] private float stageDoneDelay = 1f;
    [Tooltip("Seconds to wait before spawning the object again, when the player hit it, missed it or let it pass.")]
    [SerializeField, Min(0f)] private float respawnDelay = 1.5f;
    [Tooltip("How far either side of the player's current angle a spawned object may appear, in degrees. 0 = straight at the player.")]
    [SerializeField, Min(0f)] private float spawnLaneSpread = 15f;
    [Tooltip("The text shown when the last stage is done, just before the level starts.")]
    [SerializeField] private string finishPrompt = "HERE WE GO!";
    [Tooltip("Seconds the finish text stays up before the level starts.")]
    [SerializeField, Min(0f)] private float finishDelay = 2f;

    private GameManager gm;
    private PlayerMovement player;
    private Goal currentGoal;    // the goal of the stage running now
    private bool stageRunning;   // a stage is waiting for its goal
    private float progress;      // how far the running stage's goal has got: degrees, boosts, or 1 for a smash or pickup
    private GameObject spawned;  // the object spawned for the running stage, if any

    private void Awake()
    {
        gm = GameManager.Instance;
        player = gm != null ? gm.Player : null;
        if (promptText != null) promptText.gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        if (gm != null) gm.OnTutorialStarted += HandleTutorialStarted;
        if (player != null)
        {
            player.OnSideBoost += HandleSideBoost;
            player.OnSpeedBoost += HandleForwardBoost;
        }
        AsteroidBehavior.OnSmashed += HandleSmashed;
        PowerUp.OnCollected += HandleCollected;
    }

    private void OnDisable()
    {
        if (gm != null) gm.OnTutorialStarted -= HandleTutorialStarted;
        if (player != null)
        {
            player.OnSideBoost -= HandleSideBoost;
            player.OnSpeedBoost -= HandleForwardBoost;
        }
        AsteroidBehavior.OnSmashed -= HandleSmashed;
        PowerUp.OnCollected -= HandleCollected;
    }

    private void HandleTutorialStarted() => StartCoroutine(RunTutorial());

    // Each handler counts only while a stage with its goal is running, so a boost in the middle of
    // the Smash stage, or a smash outside the tutorial, is ignored.
    private void HandleSideBoost(float direction) { if (stageRunning && currentGoal == Goal.SideBoost) progress++; }
    private void HandleForwardBoost() { if (stageRunning && currentGoal == Goal.ForwardBoost) progress++; }
    private void HandleSmashed(AsteroidBehavior.AsteroidKind kind) { if (stageRunning && currentGoal == Goal.Smash) progress++; }
    private void HandleCollected(PowerUp.PowerUpType type, Vector3 position) { if (stageRunning && currentGoal == Goal.Collect) progress++; }

    private IEnumerator RunTutorial()
    {
        foreach (Stage stage in stages)
        {
            yield return RunStage(stage);
            yield return new WaitForSeconds(stageDoneDelay);
        }

        ShowPrompt(finishPrompt);
        yield return new WaitForSeconds(finishDelay);

        if (spawned != null) Destroy(spawned);
        ShowPrompt(null);
        gm.StartGame();
    }

    private IEnumerator RunStage(Stage stage)
    {
        ShowPrompt(stage.prompt);
        currentGoal = stage.goal;
        progress = 0f;
        stageRunning = true;
        float lastAngle = player.OrbitAngle * Mathf.Rad2Deg;
        bool hit = false;   // Avoid: the player was hit, so this attempt doesn't count
        bool spawnsObject = stage.spawnPrefab != null;
        if (spawnsObject) Spawn(stage.spawnPrefab);

        while (!IsGoalMet(stage))
        {
            if (stage.goal == Goal.Orbit)
            {
                // DeltaAngle is the shortest signed turn, so crossing 360 -> 0 doesn't read as a huge jump.
                float angle = player.OrbitAngle * Mathf.Rad2Deg;
                progress += Mathf.Abs(Mathf.DeltaAngle(lastAngle, angle));
                lastAngle = angle;
            }
            if (player.IsInvulnerable) hit = true;

            // The object is gone: it flew past, or it was smashed or collected.
            if (spawnsObject && spawned == null)
            {
                if (stage.goal == Goal.Avoid && !hit) progress = 1f;
                else if (!IsGoalMet(stage))
                {
                    yield return new WaitUntil(() => !player.IsInvulnerable);
                    yield return new WaitForSeconds(respawnDelay);
                    hit = false;
                    Spawn(stage.spawnPrefab);
                }
            }
            yield return null;
        }

        stageRunning = false;
    }

    private bool IsGoalMet(Stage stage)
    {
        switch (stage.goal)
        {
            case Goal.Speed: return PlayerMovement.Speed01 >= stage.amount;
            case Goal.Avoid:
            case Goal.Smash:
            case Goal.Collect: return progress >= 1f;
            default: return progress >= stage.amount;   // Orbit, SideBoost, ForwardBoost
        }
    }

    private void ShowPrompt(string text)
    {
        if (promptText == null) return;
        promptText.gameObject.SetActive(!string.IsNullOrEmpty(text));
        promptText.text = text;
    }

    // Makes one object at the spawn plane, on the player's circle, within spawnLaneSpread degrees of where the player is now.
    private void Spawn(GameObject prefab)
    {
        OrbitSettings orbit = OrbitSettings.Instance;
        float angle = player.OrbitAngle + Random.Range(-spawnLaneSpread, spawnLaneSpread) * Mathf.Deg2Rad;
        Vector3 position = new Vector3(orbit.OrbitCenter.x + Mathf.Cos(angle) * orbit.OrbitRadius,
                                       orbit.OrbitCenter.y + Mathf.Sin(angle) * orbit.OrbitRadius,
                                       orbit.SpawnerPosition.z);
        spawned = Instantiate(prefab, position, prefab.transform.rotation);
    }
}
