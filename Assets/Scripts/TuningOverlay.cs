using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// A playtest overlay: Tab opens it, and it edits the movement values live. The values save to a JSON file
// next to the game, which is loaded again at the start of each level.
public class TuningOverlay : MonoBehaviour
{
    private const bool Enabled = true;   // set to false to leave the overlay out of a build
    private const string FileName = "MovementTuning.json";

    // True while the overlay is open. PlayerMovement ignores input then.
    public static bool IsOpen { get; private set; }

    // The file's contents: the player's values, the other values, and each level's minimum speed.
    [System.Serializable]
    public class World
    {
        public float knockbackForce, speedLoss, baseApproachSpeed, speedMultiplier, speedTimeFactor, orbitRadius;
    }

    [System.Serializable]
    public class LevelMin
    {
        public string level;
        public float minSpeed;
    }

    [System.Serializable]
    public class SaveData
    {
        public PlayerMovement.Tuning player = new PlayerMovement.Tuning();
        public World world = new World();
        public List<LevelMin> minSpeeds = new List<LevelMin>();
    }

    private SaveData current = new SaveData();    // what the sliders edit, and what gets saved
    private SaveData defaults = new SaveData();   // the values the game started with, for Reset

    // Next to the game's exe. In the Editor that is the project root.
    private static string FilePath =>
        System.IO.Path.Combine(System.IO.Directory.GetParent(Application.dataPath).FullName, FileName);

    private void Save()
    {
        try { System.IO.File.WriteAllText(FilePath, JsonUtility.ToJson(current, true)); }
        catch (System.Exception e) { Debug.LogWarning($"Tuning: could not save {FilePath}: {e.Message}"); }
    }

    // Copies the file's values over the ones in target. A value missing from the file keeps what target has.
    private static bool LoadInto(SaveData target)
    {
        if (!System.IO.File.Exists(FilePath)) return false;
        try { JsonUtility.FromJsonOverwrite(System.IO.File.ReadAllText(FilePath), target); return true; }
        catch (System.Exception e) { Debug.LogWarning($"Tuning: could not read {FilePath}: {e.Message}"); return false; }
    }

    private PlayerMovement player;
    private LevelTimer timer;
    private SpawnableTemplate template;
    private OrbitSettings orbit;
    private string levelName;
    private bool assetsCaptured;   // the two assets keep their changes between levels, so their starting values are read only once

    // A separate copy of data, made by writing it to JSON text and reading it back.
    private static SaveData Clone(SaveData data) => JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(data));

    // Reads the values the game starts with, before any file is applied.
    private void CaptureDefaults()
    {
        defaults.player = player.GetTuning();
        defaults.world.speedTimeFactor = timer.SpeedTimeFactor;

        if (!assetsCaptured)
        {
            World w = defaults.world;
            w.knockbackForce = template.knockbackForce;
            w.speedLoss = template.speedLoss;
            w.baseApproachSpeed = template.baseApproachSpeed;
            w.speedMultiplier = template.speedMultiplier;
            w.orbitRadius = orbit.OrbitRadius;
            assetsCaptured = true;
        }

        LevelConfig config = GameManager.Instance.LevelConfig;
        defaults.minSpeeds.RemoveAll(m => m.level == levelName);
        defaults.minSpeeds.Add(new LevelMin { level = levelName, minSpeed = config != null ? config.minSpeed : 0f });
    }

    // Pushes the values in current into the game. Called after loading the file, and on every slider change.
    private void Apply()
    {
        player.SetTuning(current.player);
        timer.SpeedTimeFactor = current.world.speedTimeFactor;

        template.knockbackForce = current.world.knockbackForce;
        template.speedLoss = current.world.speedLoss;
        template.baseApproachSpeed = current.world.baseApproachSpeed;
        template.speedMultiplier = current.world.speedMultiplier;
        orbit.OrbitRadius = current.world.orbitRadius;

        LevelMin min = current.minSpeeds.Find(m => m.level == levelName);
        if (min != null) player.SetMinSpeed(min.minSpeed);
    }

    // Runs when a level loads: finds what the overlay changes, reads the starting values, then applies the file.
    private void BindLevel()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null || gm.Player == null || gm.LevelTimer == null) { player = null; return; }   // menu and other scenes

        player = gm.Player;
        timer = gm.LevelTimer;
        template = Resources.Load<SpawnableTemplate>("SpawnableTemplate");
        orbit = OrbitSettings.Instance;
        levelName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

        CaptureDefaults();
        current = Clone(defaults);
        LoadInto(current);
        if (current.minSpeeds.Find(m => m.level == levelName) == null)
            current.minSpeeds.Add(new LevelMin { level = levelName, minSpeed = defaults.minSpeeds.Find(m => m.level == levelName).minSpeed });
        Apply();
    }

    // Makes the overlay once, as the game starts, so no scene needs an object for it.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Create()
    {
        if (!Enabled) return;
        DontDestroyOnLoad(new GameObject("TuningOverlay", typeof(TuningOverlay)));
    }

    private void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
    private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

    // The first level was already loaded before this object existed, so it binds itself too.
    private void Start() => BindLevel();

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => BindLevel();

    // The assets keep changes after Play stops in the Editor, so put their original values back.
    private void OnApplicationQuit()
    {
        if (!assetsCaptured) return;
        World w = defaults.world;
        template.knockbackForce = w.knockbackForce;
        template.speedLoss = w.speedLoss;
        template.baseApproachSpeed = w.baseApproachSpeed;
        template.speedMultiplier = w.speedMultiplier;
        orbit.OrbitRadius = w.orbitRadius;
    }

    private void Update()
    {
        if (player == null || Keyboard.current == null || !Keyboard.current.tabKey.wasPressedThisFrame) return;
        if (IsOpen) Close();
        else if (CanOpen) Open();
    }

    // Only while flying: not in the menu, before the start, or after a game over or win.
    private bool CanOpen => GameManager.Instance.IsFlying && !GameManager.Instance.IsGameOver && !GameManager.Instance.HasWon;

    private void Open()
    {
        IsOpen = true;
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void Close()
    {
        IsOpen = false;
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        Save();
    }

    private readonly Dictionary<string, string> typed = new Dictionary<string, string>();   // text being typed in a number box

    // One row: a label with a tooltip, a slider and a number box. Returns true when the value changed.
    private bool Row(string label, string tip, ref float value, float min, float max)
    {
        float before = value;
        GUILayout.BeginHorizontal();
        GUILayout.Label(new GUIContent(label, tip), GUILayout.Width(170f));
        value = GUILayout.HorizontalSlider(value, min, max);

        GUI.SetNextControlName(label);
        bool editing = GUI.GetNameOfFocusedControl() == label;
        if (!editing) typed.Remove(label);
        string shown = editing && typed.TryGetValue(label, out string text) ? text : value.ToString("0.###", CultureInfo.InvariantCulture);
        string entered = GUILayout.TextField(shown, GUILayout.Width(64f));
        if (entered != shown)
        {
            typed[label] = entered;
            if (float.TryParse(entered, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed)) value = parsed;
        }
        GUILayout.EndHorizontal();
        return value != before;
    }

    // The steering and speed rows. Returns true when any value changed.
    private bool DrawSteeringAndSpeed()
    {
        PlayerMovement.Tuning t = current.player;
        LevelMin min = current.minSpeeds.Find(m => m.level == levelName);
        bool changed = false;

        GUILayout.Label("STEERING");
        changed |= Row("mouseSensitivity", "Orbit distance per pixel of mouse movement, in thousandths of a world unit. 5 = 0.005.", ref t.mouseSensitivity, 0.5f, 20f);
        changed |= Row("forwardThreshold", "Degrees either side of straight up inside which sideways mouse movement is ignored. 0 = off.", ref t.forwardThreshold, 0f, 90f);
        changed |= Row("minInputForAngle", "Pixels per frame. Movement shorter than this is too small to have a reliable direction, so the threshold isn't applied.", ref t.minInputForAngle, 0f, 10f);

        GUILayout.Label("SPEED");
        changed |= Row("acceleration", "Speed gained per pixel of upward mouse movement, in thousandths. 12 = 0.012.", ref t.acceleration, 1f, 60f);
        changed |= Row("speedDecay", "Speed lost per second with no input.", ref t.speedDecay, 0f, 60f);
        changed |= Row("maxSpeed", "The top speed. The sky, starfield, level timer and asteroids all use Speed directly, so changing this shifts them too.", ref t.maxSpeed, 20f, 200f);
        changed |= Row("overdriveFraction", "Fraction of max speed where overdrive starts. 0.8 = the top 20%.", ref t.overdriveFraction, 0.5f, 1f);
        changed |= Row("topSpeedDegrade", "The most extra Speed lost per second in overdrive, reached at max speed.", ref t.topSpeedDegrade, 0f, 40f);
        changed |= Row("minSpeed (this level)", "The slowest the player can fly in this level. They can't slow below it. 0 = no minimum.", ref min.minSpeed, 0f, 100f);
        return changed;
    }

    // The boost and hit rows. Returns true when any value changed.
    private bool DrawBoostAndHit()
    {
        PlayerMovement.Tuning t = current.player;
        bool changed = false;

        GUILayout.Label("BOOST");
        changed |= Row("boostForce", "Sideways boost strength around the circle. The distance it covers is boostForce divided by boostDecay.", ref t.boostForce, 0f, 40f);
        changed |= Row("boostDecay", "How fast a sideways boost or knockback fades out, per second.", ref t.boostDecay, 0.5f, 10f);
        changed |= Row("speedBoost", "How much Speed a forward boost adds.", ref t.speedBoost, 0f, 100f);
        changed |= Row("boostCooldown", "Seconds after a boost (side or forward) before the next one can be used.", ref t.boostCooldown, 0f, 10f);
        changed |= Row("sideInputMemory", "Seconds after the last sideways mouse movement that a click still gives a side boost.", ref t.sideInputMemory, 0f, 0.5f);

        GUILayout.Label("HIT");
        changed |= Row("knockbackForce", "How hard the player is pushed along their circle when hit.", ref current.world.knockbackForce, 0f, 20f);
        changed |= Row("speedLoss", "How much the player's Speed drops when hit.", ref current.world.speedLoss, 0f, 100f);
        changed |= Row("invulnerableTime", "Seconds of no hits after being hit.", ref t.invulnerableTime, 0f, 5f);
        return changed;
    }

    // The world rows: how speed feels in the level. Returns true when any value changed.
    private bool DrawWorld()
    {
        World w = current.world;
        bool changed = false;

        GUILayout.Label("WORLD");
        changed |= Row("baseApproachSpeed", "Approach speed (units/sec) at player Speed 0. Shared by asteroids and power-ups.", ref w.baseApproachSpeed, 5f, 60f);
        changed |= Row("speedMultiplier", "Extra approach speed (units/sec) for each point of the player's Speed.", ref w.speedMultiplier, 0f, 1f);
        changed |= Row("speedTimeFactor", "How much the player's Speed shortens the level. Level time divides by 1 + Speed x this.", ref w.speedTimeFactor, 0f, 0.05f);
        changed |= Row("orbitRadius", "Size of the player's orbit, in world units. Patterns already on screen keep their old size.", ref w.orbitRadius, 1.5f, 6f);
        return changed;
    }

    private Vector2 scroll;

    private void OnGUI()
    {
        if (!IsOpen) return;

        float scale = Screen.height / 1080f;   // sized for 1080p, scaled for other screens
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
        GUILayout.BeginArea(new Rect(20f, 20f, 560f, Screen.height / scale - 40f), GUI.skin.box);
        GUILayout.Label("MOVEMENT TUNING     Tab: close and save");

        scroll = GUILayout.BeginScrollView(scroll);
        bool changed = DrawSteeringAndSpeed();
        changed |= DrawBoostAndHit();
        changed |= DrawWorld();
        GUILayout.EndScrollView();
        if (changed) Apply();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Reset to defaults")) { current = Clone(defaults); Apply(); }
        if (GUILayout.Button("Save")) Save();
        if (GUILayout.Button("Copy to clipboard")) GUIUtility.systemCopyBuffer = JsonUtility.ToJson(current, true);
        GUILayout.EndHorizontal();
        GUILayout.Label(GUI.tooltip);
        GUILayout.EndArea();
    }
}
