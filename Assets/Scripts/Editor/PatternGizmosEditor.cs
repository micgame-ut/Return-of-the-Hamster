// ---------------------------------------------------------------------------
// PatternGizmosEditor
//
// A "custom inspector": Unity lets you replace the panel it draws in the
// Inspector window for a component. This one draws PatternGizmos' normal
// fields and then adds a Pattern Builder underneath, with buttons that place
// asteroid and power-up prefabs on the player's orbit circle.
//
// Because this file lives in a folder named "Editor", Unity compiles it only
// for the editor and leaves it out of game builds. That is also why it may
// use the UnityEditor namespace freely.
//
// Coordinates: every position here is in "spawner space", the same space the
// PatternGizmos rings are drawn in. In the Game scene that is the Asteroid
// Spawner's local space, so what you see in Prefab Mode is what you get in
// play.
//
// Angles are in degrees, using the same convention as PlayerMovement:
//   0 = right, 90 = top, -90 = bottom (where the player starts),
//   and increasing angles go counter-clockwise.
// ---------------------------------------------------------------------------

using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// This attribute tells Unity: "whenever a PatternGizmos component is shown in
// the Inspector, use this class to draw it."
[CustomEditor(typeof(PatternGizmos)), InitializeOnLoad]
public class PatternGizmosEditor : Editor
{
    // The shapes the builder knows how to lay out. The tab bar in the
    // inspector picks one, and both the preview and the Add button use it.
    private enum Shape { Ring, Arc, Row, Spiral, Zigzag, Twist, Random }
    private static readonly string[] ShapeNames = { "Ring", "Arc", "Row", "Spiral", "Zigzag", "Twist", "Random" };

    // The same tabs with hover text, for the tab bar.
    private static readonly GUIContent[] ShapeTabs =
    {
        new GUIContent("Ring", "Evenly spaced all the way round the orbit."),
        new GUIContent("Arc", "Spread along part of the orbit, including both ends."),
        new GUIContent("Row", "A straight horizontal line across the orbit."),
        new GUIContent("Spiral", "Each object steps round the orbit and deeper. Angle Step 0 = a Lane, Z Step 0 = a Burst."),
        new GUIContent("Zigzag", "Alternates between two angles, going deeper each time."),
        new GUIContent("Twist", "Rings stacked in depth, each turned a bit more than the last."),
        new GUIContent("Random", "Random angles and depths, repeatable from a seed."),
    };

    // EditorPrefs is a small key/value store saved on your machine (not in the
    // project). We use it to remember which asteroid prefab you picked, so you
    // don't have to re-pick it every time Unity restarts.
    private const string PrefabPrefKey = "PatternBuilder.AsteroidPrefab";
    private const string DefaultPrefabPath = "Assets/Prefabs/Asteroid Types/Asteroid.prefab";
    private const string PowerUpPrefKey = "PatternBuilder.PowerUpPrefab";
    private const string DefaultPowerUpPath = "Assets/Prefabs/Health PowerUp.prefab";
    private const string PatternFolder = "Assets/Prefabs/Patterns";
    private const string PlayerPrefabPath = "Assets/Prefabs/hamter.prefab";
    private const string TestScenePath ="Assets/Scenes/PatternTest.unity";
    private const string ReopenKey = "PatternBuilder.ReopenPattern"; // the pattern to reopen in Prefab Mode when the test ends
    private const string ReturnSceneKey = "PatternBuilder.ReturnScene"; // the scene to go back to when the test ends

    // Preview colors: translucent green reads as "not placed yet".
    private static readonly Color PreviewColor = new Color(0.3f, 1f, 0.4f, 0.5f);
    private static readonly Color PreviewLabelColor = new Color(0.3f, 1f, 0.4f, 1f);

    // Power-up previews are yellow, so you can tell which kind the next Add places.
    private static readonly Color PowerUpPreviewColor = new Color(1f, 0.9f, 0.2f, 0.5f);
    private static readonly Color PowerUpPreviewLabelColor = new Color(1f, 0.9f, 0.2f, 1f);

    // The gap on a Ring or Arc is drawn as a translucent red wedge.
    private static readonly Color GapColor = new Color(1f, 0.25f, 0.25f, 0.3f);

    // Rings drawn on the objects a Fairness warning is about.
    private static readonly Color WarningColor = new Color(1f, 0.15f, 0.15f, 1f);

    // The arc a Wave asteroid swings along, drawn around its preview sphere.
    private static readonly Color SweepColor = new Color(0.3f, 0.8f, 1f, 1f);

    // -----------------------------------------------------------------------
    // Builder settings
    //
    // An Editor object is created fresh each time you select something, so
    // normal fields would reset whenever you click away. Making them static
    // keeps them for the whole editor session, which also means your settings
    // carry over when you move from one pattern prefab to another.
    // -----------------------------------------------------------------------

    private static Shape shape = Shape.Ring;
    private static GameObject asteroidPrefab;
    private static GameObject powerUpPrefab;

    // Prefab mix: each slot can pick between up to 4 prefabs at random. Entry 0 is the slot's own prefab field above,
    // so only entries 1 to 3 are kept here. Entry 0's weight starts at 1, the rest at 0 (unused).
    private static readonly GameObject[] asteroidExtras = new GameObject[3];
    private static readonly GameObject[] powerUpExtras = new GameObject[3];
    private static readonly float[] asteroidWeights = { 1f, 0f, 0f, 0f };
    private static readonly float[] powerUpWeights = { 1f, 0f, 0f, 0f };
    private static bool mixLoaded; // true once the saved mix has been read

    // Which slot the Add button places from.
    private enum Placing { Asteroid, PowerUp }
    private static readonly string[] PlacingNames = { "Asteroid", "Power-Up" };
    private static readonly GUIContent[] PlacingTabs =
    {
        new GUIContent("Asteroid", "Which of the two prefabs Add places. Also sets the preview colour (green or yellow)."),
        new GUIContent("Power-Up", "Which of the two prefabs Add places. Also sets the preview colour (green or yellow)."),
    };
    private static Placing placing = Placing.Asteroid;

    private static GameObject SelectedPrefab => placing == Placing.Asteroid ? asteroidPrefab : powerUpPrefab;

    // Shared by every shape. The radius is always the orbit's, from OrbitSettings.
    private static float z;                // depth; lower Z spawns first
    private static int count = 8;          // how many objects to place
    private static float previewSize = 1f; // diameter of the preview spheres

    // Ring: count objects evenly spaced all the way around.
    private static float ringStartAngle = -90f;

    // Arc: count objects spread evenly from one angle to another (inclusive).
    private static float arcFrom = -135f;
    private static float arcTo = -45f;

    // Gap: an empty wedge on a Ring or Arc, so there's always a lane to fly through. 0 width = off.
    private static float gapWidth;         // degrees
    private static float gapCenter = -90f; // degrees; -90 = the bottom, where the player starts

    // Row: a straight horizontal line centered on the circle. Its height is
    // measured up from the bottom of the circle (where the player sits), so it
    // follows the orbit if OrbitSettings changes.
    private static float rowYOffset;
    private static float rowSpacing = 1f;

    // Spiral: each object steps around by an angle AND back by a Z amount,
    // so each one is its own spawn step and they appear one after another.
    private static float spiralStartAngle = -90f;
    private static float spiralAngleStep = 30f;
    private static float spiralZStep = 2f;

    // Zigzag: objects alternate between two angles, going deeper each time.
    private static float zigzagAngleA = -135f;
    private static float zigzagAngleB = -45f;
    private static float zigzagZStep = 2f;

    // Twist: rings stacked in depth, each turned a bit more than the one before. Uses the Ring's start angle and gap.
    private static int twistRings = 5;
    private static float twistZStep = 3f;
    private static float twistAngle = 15f; // degrees each ring is turned more than the one before

    // Jitter: a small random nudge on every object (all shapes except Random). 0 = off. Uses randomSeed.
    private static float jitterAngle; // degrees either way round the orbit
    private static float jitterZ;     // depth either way

    // Wave tools: Save Pattern As, Test This Pattern, Add to Spawner.
    private static bool clearAfterSave = true; // Save Pattern As empties the open pattern afterwards
    private static float testLoopDelay = 3f;   // seconds between test runs
    private static float rotateBy = 30f;       // degrees; positive = counter-clockwise
    private static float shiftZBy = 5f;        // depth added by Shift Z; positive = further away
    private static bool autoNameOnAdd = true;  // rename every child "Step N - Prefab K" after each Add
    private static int selectedPreset;         // which preset the dropdown shows
    private static string presetName = "";     // what the name box holds
    private static float timingSpeed = 50f;    // player Speed the middle Timing column and the Scene labels use
    private static float playerRadius = 0.3f;      // the player's hitbox radius; 0.3 is the shield's, the biggest
    private static float laneMargin = 5f;          // degrees of room a gap needs, on top of the bare minimum
    private static float playerAngularSpeed = 90f; // degrees per second the player is assumed to move around the orbit
    private static PatternFairness.Warning[] fairnessWarnings = new PatternFairness.Warning[0]; // the latest result, for the Scene view markers

    static PatternGizmosEditor()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;

        // A request left over from an editor crash would turn the next normal Play into a test.
        // Done on the next editor update, because Unity doesn't allow this check inside a constructor.
        EditorApplication.delayCall += () =>
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode)
                PatternTest.Clear();
        };
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredEditMode) return;

        PatternTest.Clear();

        // Back where you were: reopen the pattern in Prefab Mode.
        string path = EditorPrefs.GetString(ReopenKey, "");
        string scenePath = EditorPrefs.GetString(ReturnSceneKey, "");
        EditorPrefs.DeleteKey(ReopenKey);
        EditorPrefs.DeleteKey(ReturnSceneKey);
        if (path != "")
            EditorApplication.delayCall += () =>
            {
                // Back where you were: the scene first, then the pattern in Prefab Mode.
                if (scenePath != "" && EditorSceneManager.GetActiveScene().path != scenePath
                    && EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    EditorSceneManager.OpenScene(scenePath);
                PrefabStageUtility.OpenPrefab(path);
            };
    }

    // Random: objects at random angles and depths, repeatable from a seed.
    private static float randomFromAngle = -180f;
    private static float randomToAngle = 180f;
    private static float randomZRange;       // each object's depth is Z plus a random amount up to this
    private static float randomMinGap = 20f; // least angle between any two objects, in degrees
    private static int randomSeed = 1;

    // What the seed produced: one angle and one 0-1 depth fraction per placed object.
    private static float[] randomAngles = new float[0];
    private static float[] randomDepths = new float[0];
    private static bool randomDirty = true;  // true = the lists above must be rebuilt

    // A snapshot of the builder's placement settings, saved to and loaded from a .json file. Prefabs are kept as asset paths.
    [Serializable]
    private class Preset
    {
        public int placing, shape;
        public float z;
        public int count;
        public float ringStartAngle, arcFrom, arcTo, rowYOffset, rowSpacing;
        public float gapWidth, gapCenter;
        public float spiralStartAngle, spiralAngleStep, spiralZStep;
        public float zigzagAngleA, zigzagAngleB, zigzagZStep;
        public int twistRings;
        public float twistZStep, twistAngle;
        public float randomFromAngle, randomToAngle, randomZRange, randomMinGap;
        public int randomSeed;
        public float jitterAngle, jitterZ;
        public string asteroidPrefab, powerUpPrefab;
        public string[] asteroidExtras, powerUpExtras;
        public float[] asteroidWeights, powerUpWeights;
    }

    // A prefab's asset path, or "" for an empty slot.
    private static string PathOf(GameObject prefab) => prefab != null ? AssetDatabase.GetAssetPath(prefab) : "";

    // Copies the builder's current placement settings into a Preset. (In each `name = name`, the left side is the
    // Preset's field and the right side is the builder's setting.)
    private static Preset CapturePreset()
    {
        return new Preset
        {
            placing = (int)placing, shape = (int)shape, z = z, count = count,
            ringStartAngle = ringStartAngle, arcFrom = arcFrom, arcTo = arcTo, rowYOffset = rowYOffset, rowSpacing = rowSpacing,
            gapWidth = gapWidth, gapCenter = gapCenter,
            spiralStartAngle = spiralStartAngle, spiralAngleStep = spiralAngleStep, spiralZStep = spiralZStep,
            zigzagAngleA = zigzagAngleA, zigzagAngleB = zigzagAngleB, zigzagZStep = zigzagZStep,
            twistRings = twistRings, twistZStep = twistZStep, twistAngle = twistAngle,
            randomFromAngle = randomFromAngle, randomToAngle = randomToAngle, randomZRange = randomZRange,
            randomMinGap = randomMinGap, randomSeed = randomSeed,
            jitterAngle = jitterAngle, jitterZ = jitterZ,
            asteroidPrefab = PathOf(asteroidPrefab), powerUpPrefab = PathOf(powerUpPrefab),
            asteroidExtras = Array.ConvertAll(asteroidExtras, PathOf), powerUpExtras = Array.ConvertAll(powerUpExtras, PathOf),
            asteroidWeights = (float[])asteroidWeights.Clone(), powerUpWeights = (float[])powerUpWeights.Clone()
        };
    }

    // A prefab loaded from an asset path. An empty or missing path gives an empty slot.
    private static GameObject LoadPrefab(string path) =>
        string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<GameObject>(path);

    // Puts a Preset's values into the builder's settings. Prefabs come back from their paths, and the prefab choices and the
    // mix are saved again so they survive a restart.
    private static void ApplyPreset(Preset p)
    {
        placing = (Placing)p.placing; shape = (Shape)p.shape; z = p.z; count = Mathf.Max(1, p.count);
        ringStartAngle = p.ringStartAngle; arcFrom = p.arcFrom; arcTo = p.arcTo; rowYOffset = p.rowYOffset; rowSpacing = p.rowSpacing;
        gapWidth = p.gapWidth; gapCenter = p.gapCenter;
        spiralStartAngle = p.spiralStartAngle; spiralAngleStep = p.spiralAngleStep; spiralZStep = p.spiralZStep;
        zigzagAngleA = p.zigzagAngleA; zigzagAngleB = p.zigzagAngleB; zigzagZStep = p.zigzagZStep;
        twistRings = Mathf.Max(1, p.twistRings); twistZStep = p.twistZStep; twistAngle = p.twistAngle;
        randomFromAngle = p.randomFromAngle; randomToAngle = p.randomToAngle; randomZRange = p.randomZRange;
        randomMinGap = p.randomMinGap; randomSeed = p.randomSeed;
        jitterAngle = p.jitterAngle; jitterZ = p.jitterZ;

        asteroidPrefab = LoadPrefab(p.asteroidPrefab);
        powerUpPrefab = LoadPrefab(p.powerUpPrefab);
        for (int n = 0; n < asteroidExtras.Length; n++)
        {
            asteroidExtras[n] = LoadPrefab(p.asteroidExtras != null && n < p.asteroidExtras.Length ? p.asteroidExtras[n] : "");
            powerUpExtras[n] = LoadPrefab(p.powerUpExtras != null && n < p.powerUpExtras.Length ? p.powerUpExtras[n] : "");
        }
        for (int n = 0; n < asteroidWeights.Length; n++)
        {
            asteroidWeights[n] = p.asteroidWeights != null && n < p.asteroidWeights.Length ? p.asteroidWeights[n] : (n == 0 ? 1f : 0f);
            powerUpWeights[n] = p.powerUpWeights != null && n < p.powerUpWeights.Length ? p.powerUpWeights[n] : (n == 0 ? 1f : 0f);
        }

        EditorPrefs.SetString(PrefabPrefKey, PathOf(asteroidPrefab));
        EditorPrefs.SetString(PowerUpPrefKey, PathOf(powerUpPrefab));
        SaveMix("Asteroid", asteroidExtras, asteroidWeights);
        SaveMix("PowerUp", powerUpExtras, powerUpWeights);
        randomDirty = true;
        SceneView.RepaintAll();
    }

    private const string PresetFolder = "Assets/Scripts/Editor/PatternPresets";

    // The path of a preset's file. Characters Windows doesn't allow in a file name are replaced with an underscore.
    private static string PresetPath(string name)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return $"{PresetFolder}/{name.Trim()}.json";
    }

    // The names of the saved presets: the .json files in the folder, without the extension, in alphabetical order.
    private static string[] PresetNames()
    {
        if (!Directory.Exists(PresetFolder)) return new string[0];

        string[] files = Directory.GetFiles(PresetFolder, "*.json");
        Array.Sort(files);
        return Array.ConvertAll(files, f => Path.GetFileNameWithoutExtension(f));
    }

    // Saves the builder's current settings as a preset file. A preset of the same name is overwritten (the panel asks first).
    private static void SavePreset(string name)
    {
        if (!Directory.Exists(PresetFolder)) Directory.CreateDirectory(PresetFolder);
        File.WriteAllText(PresetPath(name), JsonUtility.ToJson(CapturePreset(), true));
        AssetDatabase.Refresh(); // so Unity notices the new file
    }

    // Loads a preset file into the builder's settings. Returns false if the file is missing.
    private static bool LoadPreset(string name)
    {
        string path = PresetPath(name);
        if (!File.Exists(path)) return false;

        ApplyPreset(JsonUtility.FromJson<Preset>(File.ReadAllText(path)));
        return true;
    }

    private static void DeletePreset(string name)
    {
        AssetDatabase.DeleteAsset(PresetPath(name));
    }

    // Called when this inspector is created (i.e. when you select a pattern).
    private void OnEnable()
    {
        // Load the remembered prefab path, falling back to the default one.
        if (asteroidPrefab == null)
            asteroidPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                EditorPrefs.GetString(PrefabPrefKey, DefaultPrefabPath));
        if (powerUpPrefab == null)
            powerUpPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                EditorPrefs.GetString(PowerUpPrefKey, DefaultPowerUpPath));

        if (!mixLoaded)
        {
            LoadMix("Asteroid", asteroidExtras, asteroidWeights);
            LoadMix("PowerUp", powerUpExtras, powerUpWeights);
            mixLoaded = true;
        }
    }

    // -----------------------------------------------------------------------
    // Inspector panel
    //
    // OnInspectorGUI runs every time the Inspector redraws. Unity's editor UI
    // is "immediate mode": each EditorGUILayout call both draws a field AND
    // returns its current value, so the pattern is always
    //     value = EditorGUILayout.SomeField("Label", value);
    // -----------------------------------------------------------------------
    public override void OnInspectorGUI()
    {
        // Draws PatternGizmos' own serialized fields (the ring colors) as usual.
        DrawDefaultInspector();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Pattern Builder", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Angles in degrees: 0 = right, 90 = top, -90 = bottom (player start), increasing counter-clockwise. " +
            "Z is in spawner space; lower Z spawns first. Green (asteroid) or yellow (power-up) spheres in the Scene view preview the next Add.",
            MessageType.None);

        DrawPresets();

        // BeginChangeCheck / EndChangeCheck wraps a group of fields and tells
        // us whether any of them changed this frame.
        EditorGUI.BeginChangeCheck();
        asteroidPrefab = (GameObject)EditorGUILayout.ObjectField(
            new GUIContent("Asteroid Prefab", "The asteroid prefab Add places when the Asteroid tab is selected. With a Prefab Mix, this is entry 1."), asteroidPrefab, typeof(GameObject), false); // false = assets only, no scene objects
        if (EditorGUI.EndChangeCheck())
            EditorPrefs.SetString(PrefabPrefKey, asteroidPrefab != null ? AssetDatabase.GetAssetPath(asteroidPrefab) : "");

        EditorGUI.BeginChangeCheck();
        powerUpPrefab = (GameObject)EditorGUILayout.ObjectField(
            new GUIContent("Power-Up Prefab", "The power-up prefab Add places when the Power-Up tab is selected."), powerUpPrefab, typeof(GameObject), false);
        if (EditorGUI.EndChangeCheck())
            EditorPrefs.SetString(PowerUpPrefKey, powerUpPrefab != null ? AssetDatabase.GetAssetPath(powerUpPrefab) : "");

        // Everything below affects the preview, so if any of it changes we ask
        // the Scene view to redraw right away instead of waiting for the mouse
        // to move over it.
        EditorGUI.BeginChangeCheck();

        EditorGUILayout.Space();
        placing = (Placing)GUILayout.Toolbar((int)placing, PlacingTabs); // what Add places
        shape = (Shape)GUILayout.Toolbar((int)shape, ShapeTabs); // the tab bar

        z = EditorGUILayout.FloatField(new GUIContent("Z", "Depth in spawner space. Lower Z spawns first."), z);
        count = Mathf.Max(1, EditorGUILayout.IntField(new GUIContent("Count", "How many objects to place (at least 1). In Twist, the number in each ring."), count)); // never fewer than 1
        previewSize = Mathf.Max(0.05f, EditorGUILayout.FloatField(new GUIContent("Preview Size", "Size of the preview spheres. Only affects the preview."), previewSize));

        // Only show the settings for the selected shape.
        switch (shape)
        {
            case Shape.Ring:
                ringStartAngle = EditorGUILayout.FloatField(new GUIContent("Start Angle", "The angle of the first object, in degrees. 0 = right, 90 = top, -90 = bottom. Ignored while Gap Width is above 0."), ringStartAngle);
                DrawGapFields();
                if (gapWidth > 0f)
                    EditorGUILayout.HelpBox("Start Angle is ignored while Gap Width is above 0: the gap sets where the ring begins.", MessageType.None);
                break;
            case Shape.Arc:
                arcFrom = EditorGUILayout.FloatField(new GUIContent("From Angle", "Where the arc starts, in degrees. The first object lands exactly on it."), arcFrom);
                arcTo = EditorGUILayout.FloatField(new GUIContent("To Angle", "Where the arc ends, in degrees. The last object lands exactly on it."), arcTo);
                DrawGapFields();
                if (gapWidth > 0f && (gapCenter + gapWidth / 2f < Mathf.Min(arcFrom, arcTo) || gapCenter - gapWidth / 2f > Mathf.Max(arcFrom, arcTo)))
                    EditorGUILayout.HelpBox("The gap is outside the arc, so it has no effect.", MessageType.Warning);
                break;
            case Shape.Row:
                rowYOffset = EditorGUILayout.FloatField(new GUIContent("Height Above Bottom", "Height of the row above the bottom of the orbit, where the player starts. 0 = level with the bottom."), rowYOffset);
                rowSpacing = EditorGUILayout.FloatField(new GUIContent("Spacing", "Distance between neighbouring objects in the row."), rowSpacing);
                break;
            case Shape.Spiral:
                spiralStartAngle = EditorGUILayout.FloatField(new GUIContent("Start Angle", "The angle of the first object, in degrees. 0 = right, 90 = top, -90 = bottom."), spiralStartAngle);
                spiralAngleStep = EditorGUILayout.FloatField(new GUIContent("Angle Step", "Degrees each object turns round the orbit from the one before. 0 = a Lane (the same angle, going deeper)."), spiralAngleStep);
                spiralZStep = EditorGUILayout.FloatField(new GUIContent("Z Step", "Depth each object goes back from the one before. 0 = a Burst (all at the same depth)."), spiralZStep);
                EditorGUILayout.HelpBox("Angle Step 0 = a Lane (same angle, going deeper). Z Step 0 = a Burst (same depth, spaced by Angle Step).", MessageType.None);
                break;
            case Shape.Zigzag:
                zigzagAngleA = EditorGUILayout.FloatField(new GUIContent("Angle A", "The angle of the 1st, 3rd, 5th... objects, in degrees."), zigzagAngleA);
                zigzagAngleB = EditorGUILayout.FloatField(new GUIContent("Angle B", "The angle of the 2nd, 4th, 6th... objects, in degrees."), zigzagAngleB);
                zigzagZStep = EditorGUILayout.FloatField(new GUIContent("Z Step", "Depth each object goes back from the one before."), zigzagZStep);
                break;
            case Shape.Twist:
                EditorGUILayout.HelpBox("Count is the number of objects in each ring.", MessageType.None);
                twistRings = Mathf.Max(1, EditorGUILayout.IntField(new GUIContent("Rings", "How many rings to stack."), twistRings));
                twistZStep = EditorGUILayout.FloatField(new GUIContent("Z Step", "Depth between one ring and the next."), twistZStep);
                twistAngle = EditorGUILayout.FloatField(new GUIContent("Twist Per Ring", "Degrees each ring is turned more than the one before."), twistAngle);
                ringStartAngle = EditorGUILayout.FloatField(new GUIContent("Start Angle", "The angle of the first object, in degrees. 0 = right, 90 = top, -90 = bottom. Ignored while Gap Width is above 0."), ringStartAngle);
                DrawGapFields();
                if (gapWidth > 0f)
                    EditorGUILayout.HelpBox("Start Angle is ignored while Gap Width is above 0: the gap sets where each ring begins, and it turns with the ring.", MessageType.None);
                break;
            case Shape.Random:
                randomFromAngle = EditorGUILayout.FloatField(new GUIContent("From Angle", "The start of the range random angles fall in, in degrees. -180 to 180 is the whole circle."), randomFromAngle);
                randomToAngle = EditorGUILayout.FloatField(new GUIContent("To Angle", "The end of the range random angles fall in, in degrees."), randomToAngle);
                randomZRange = EditorGUILayout.FloatField(new GUIContent("Z Range", "Each object's depth is Z plus a random amount up to this. 0 = all at the same depth."), randomZRange);
                randomMinGap = Mathf.Max(0f, EditorGUILayout.FloatField(new GUIContent("Min Gap", "The least angle between any two objects, in degrees."), randomMinGap));
                randomSeed = EditorGUILayout.IntField(new GUIContent("Seed", "The same seed always gives the same layout. Shared by Random, Jitter and the Prefab Mix."), randomSeed);
                if (GUILayout.Button(new GUIContent("Reroll", "Picks a new seed, for a different layout.")))
                {
                    randomSeed = Environment.TickCount; // a different number each click
                    GUI.changed = true;                 // so the change check below sees it
                }
                break;
        }

        // Jitter works on every shape except Random, which is already random.
        if (shape != Shape.Random)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Jitter", EditorStyles.miniBoldLabel);
            jitterAngle = Mathf.Max(0f, EditorGUILayout.FloatField(new GUIContent("Jitter Angle", "Each object is moved round the orbit by a random amount up to this many degrees either way. 0 = off."), jitterAngle));
            jitterZ = Mathf.Max(0f, EditorGUILayout.FloatField(new GUIContent("Jitter Z", "Each object's depth is moved by a random amount up to this either way. 0 = off. Above 0.01, each object becomes its own step."), jitterZ));
            if (jitterAngle > 0f || jitterZ > 0f)
            {
                randomSeed = EditorGUILayout.IntField(new GUIContent("Seed", "The same seed always gives the same layout. Shared by Random, Jitter and the Prefab Mix."), randomSeed);
                if (GUILayout.Button(new GUIContent("Reroll", "Picks a new seed, for a different layout.")))
                {
                    randomSeed = Environment.TickCount; // a different number each click
                    GUI.changed = true;                 // so the change check below sees it
                }
                if (jitterZ > ObjectSpawner.SameZTolerance)
                    EditorGUILayout.HelpBox("Jitter Z gives each object its own depth, so each becomes its own step and they arrive one after another.", MessageType.None);
            }
        }

        if (EditorGUI.EndChangeCheck())
        {
            randomDirty = true;
            SceneView.RepaintAll();
        }

        if (shape == Shape.Random && PlacedCount < count)
            EditorGUILayout.HelpBox($"Placed {PlacedCount} of {count}: the minimum gap doesn't fit in the angle range.", MessageType.Warning);

        autoNameOnAdd = EditorGUILayout.Toggle(new GUIContent("Name Children After Add", "Renames every object in the pattern 'Step N - Prefab K' after each Add, and puts them in step order in the Hierarchy."), autoNameOnAdd);

        // DisabledScope greys out (and blocks) everything inside it while the
        // condition is true, so you can't click Add without a prefab.
        using (new EditorGUI.DisabledScope(SelectedPrefab == null))
        {
            string addName = $"Add {PlacingNames[(int)placing]} {ShapeNames[(int)shape]}";
            if (GUILayout.Button(new GUIContent(addName, "Places the previewed objects into the pattern, in one undo step.")))
                Build(addName);
        }

        DrawMix();
        DrawTiming((PatternGizmos)target);
        DrawFairness((PatternGizmos)target);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Waves", EditorStyles.miniBoldLabel);
        clearAfterSave = EditorGUILayout.Toggle(new GUIContent("Clear After Save", "Empties the open pattern after Save Pattern As, ready for the next one."), clearAfterSave);
        if (GUILayout.Button(new GUIContent("Save Pattern As...", "Saves a copy of the open pattern as a new prefab."))) SavePatternAs();
        testLoopDelay = Mathf.Max(0f, EditorGUILayout.FloatField(new GUIContent("Test Loop Delay", "Seconds to wait between test runs, after everything has flown past."), testLoopDelay));
        if (GUILayout.Button(new GUIContent("Test This Pattern", "Opens the test scene and plays only this pattern on a loop, with full control of the player. Returns you here when you stop."))) TestPattern();
        if (GUILayout.Button(new GUIContent("Add to Spawner", "Adds the open pattern as a new wave on the ObjectSpawner in the open scene."))) AddToSpawner();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Existing Children", EditorStyles.miniBoldLabel);

        if (GUILayout.Button(new GUIContent("Snap All to Circle", "Moves every object straight in or out from the orbit centre until it sits on the orbit.")))
        {
            var pattern = (PatternGizmos)target;
            var children = new Transform[pattern.transform.childCount];
            for (int i = 0; i < children.Length; i++)
                children[i] = pattern.transform.GetChild(i);
            Snap(pattern, children, "Snap All to Circle");
        }

        if (GUILayout.Button(new GUIContent("Name Children", "Names every object 'Step N - Prefab K' and puts them in step order in the Hierarchy.")))
            NameChildren((PatternGizmos)target);

        rotateBy =EditorGUILayout.FloatField(new GUIContent("Rotate By", "Degrees to turn, for Rotate All and Rotate Selected. Positive = counter-clockwise."), rotateBy);
        var openPattern = (PatternGizmos)target;
        if (GUILayout.Button(new GUIContent("Rotate All", "Turns every object round the orbit centre by Rotate By.")))
            RotateChildren(openPattern, ChildrenOf(openPattern.transform));
        if (GUILayout.Button(new GUIContent("Mirror All", "Flips every object left-right across the orbit centre.")))
            MirrorChildren(openPattern, ChildrenOf(openPattern.transform));

        shiftZBy = EditorGUILayout.FloatField(new GUIContent("Shift Z By", "Depth added by Shift Z. Positive = further away."), shiftZBy);
        if (GUILayout.Button(new GUIContent("Shift Z All", "Moves every object's depth by Shift Z By.")))
            ShiftChildrenZ(ChildrenOf(openPattern.transform));
        if (GUILayout.Button(new GUIContent("Reverse Z All", "Flips the depth order, so the nearest becomes the furthest.")))
            ReverseChildrenZ(ChildrenOf(openPattern.transform));

        if (GUILayout.Button(new GUIContent("Delete All Children", "Deletes every object in the pattern (it asks first). Undo brings them back.")))
            DeleteAll();
    }

    // The player's top Speed, read from the player prefab so it stays in sync. 10 if the prefab can't be read.
    private static float PlayerMaxSpeed()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        PlayerMovement movement = prefab != null ? prefab.GetComponent<PlayerMovement>() : null;
        if (movement == null) return 10f;

        using var movementObject = new SerializedObject(movement);
        return movementObject.FindProperty("maxSpeed").floatValue;
    }

    // Saves a slot's extra prefabs (as asset paths) and weights, so they survive restarts. `name` is "Asteroid" or "PowerUp".
    private static void SaveMix(string name, GameObject[] extras, float[] weights)
    {
        for (int n = 0; n < extras.Length; n++)
            EditorPrefs.SetString($"PatternBuilder.{name}Extra{n}", extras[n] != null ? AssetDatabase.GetAssetPath(extras[n]) : "");
        for (int n = 0; n < weights.Length; n++)
            EditorPrefs.SetFloat($"PatternBuilder.{name}Weight{n}", weights[n]);
    }

    // Reads them back. Entry 0's weight defaults to 1 and the others to 0.
    private static void LoadMix(string name, GameObject[] extras, float[] weights)
    {
        for (int n = 0; n < extras.Length; n++)
            extras[n] = AssetDatabase.LoadAssetAtPath<GameObject>(EditorPrefs.GetString($"PatternBuilder.{name}Extra{n}", ""));
        for (int n = 0; n < weights.Length; n++)
            weights[n] = EditorPrefs.GetFloat($"PatternBuilder.{name}Weight{n}", n == 0 ? 1f : 0f);
    }

    // One row of the Timing table: a label, then the value at each of the three speeds.
    private static void TimingRow(string label, float[] speeds, Func<float, string> value)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField(label, GUILayout.Width(170));
            foreach (float speed in speeds)
                EditorGUILayout.LabelField(value(speed));
        }
    }

    // The shortest or longest wait between two steps in a row, in seconds, at a player Speed ("-" for a one-step pattern).
    private static string StepWait(PatternGizmos.Step[] steps, float speed, bool longest)
    {
        if (steps.Length < 2) return "-";

        float result = longest ? 0f : float.MaxValue;
        for (int k = 1; k < steps.Length; k++)
        {
            float wait = PatternGizmos.SecondsBetween(steps[k - 1], steps[k], speed);
            result = longest ? Mathf.Max(result, wait) : Mathf.Min(result, wait);
        }
        return result.ToString("0.00") + " s";
    }

    // The Timing section: step times for the children already in the pattern, at a slow, a chosen and a fast player Speed.
    private static void DrawTiming(PatternGizmos pattern)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Timing", EditorStyles.miniBoldLabel);

        float maxSpeed = PlayerMaxSpeed();
        EditorGUI.BeginChangeCheck();
        timingSpeed = EditorGUILayout.Slider(new GUIContent("Speed", "The player Speed the Timing table's middle column and the Scene labels use, from 0 to the player's top speed."),Mathf.Clamp(timingSpeed, 0f, maxSpeed), 0f, maxSpeed);
        if (EditorGUI.EndChangeCheck()) SceneView.RepaintAll();
        PatternGizmos.PreviewSpeed = timingSpeed; // the Scene view labels use this too

        PatternGizmos.Step[] steps = pattern.GetSteps();
        if (steps.Length == 0 || PatternGizmos.ApproachSpeedAt(0f) <= 0f)
        {
            EditorGUILayout.HelpBox(steps.Length == 0 ? "No children yet." : "No SpawnableTemplate found in Assets/Resources, so no times can be worked out.", MessageType.None);
            return;
        }

        float[] speeds = { 0f, timingSpeed, maxSpeed };
        TimingRow("", speeds, s => $"Speed {s:0}");
        TimingRow("Approach speed (units/s)", speeds, s => PatternGizmos.ApproachSpeedAt(s).ToString("0.0"));
        TimingRow("Appears → reaches player", speeds, s => PatternGizmos.SecondsToPlayer(steps[0].z, s).ToString("0.00") + " s");
        TimingRow("Between steps, shortest", speeds, s => StepWait(steps, s, false));
        TimingRow("Between steps, longest", speeds, s => StepWait(steps, s, true));
        TimingRow("Total length", speeds, s => PatternGizmos.SecondsBetween(steps[0], steps[steps.Length - 1], s).ToString("0.00") + " s");
    }

    // The Fairness section: the assumptions about the player, then any problems found in the children already in the pattern.
    private static void DrawFairness(PatternGizmos pattern)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Fairness", EditorStyles.miniBoldLabel);
        playerRadius = Mathf.Max(0f, EditorGUILayout.FloatField(new GUIContent("Player Radius", "The player's hitbox radius. 0.3 is the shield's, the biggest, so it's the worst case."), playerRadius));
        laneMargin = Mathf.Max(0f, EditorGUILayout.FloatField(new GUIContent("Lane Margin (degrees)", "How wide a free stretch must be to count as a lane: room for the player's centre to wiggle in."), laneMargin));
        playerAngularSpeed = Mathf.Max(0f, EditorGUILayout.FloatField(new GUIContent("Player Move Speed (deg/s)", "How fast the player is assumed to move round the orbit. Sets whether they can reach the next gap in time."), playerAngularSpeed));

        if (pattern.transform.childCount == 0)
        {
            fairnessWarnings = new PatternFairness.Warning[0];
            EditorGUILayout.HelpBox("No children yet.", MessageType.None);
            return;
        }

        var settings = new PatternFairness.Settings
        {
            playerRadius = playerRadius,
            laneMargin = laneMargin,
            playerAngularSpeed = playerAngularSpeed,
            playerSpeed = timingSpeed // the Timing section's Speed
        };
        var found = PatternFairness.Check(pattern, settings);
        fairnessWarnings = found.ToArray();

        if (found.Count == 0)
            EditorGUILayout.HelpBox("No problems found.", MessageType.Info);
        foreach (var warning in found)
            EditorGUILayout.HelpBox(warning.message, MessageType.Warning);
    }

    // Gap Width and Gap Center, shared by Ring and Arc.
    private static void DrawGapFields()
    {
        gapWidth = Mathf.Clamp(EditorGUILayout.FloatField(new GUIContent("Gap Width", "Width of an empty wedge on a Ring, Arc or Twist, in degrees, so there's always a lane to fly through. 0 = off."), gapWidth), 0f, 359f);
        gapCenter = EditorGUILayout.FloatField(new GUIContent("Gap Center", "The middle of the gap, in degrees. -90 = the bottom, where the player starts."), gapCenter);
    }

    // The Prefab Mix section for the selected slot: the prefab field above is entry 1, then up to 3 extras, each with a weight.
    // Each placed object picks one at random, in proportion to the weights.
    private static void DrawMix()
    {
        bool asteroid = placing == Placing.Asteroid;
        GameObject[] extras = asteroid ? asteroidExtras : powerUpExtras;
        float[] weights = asteroid ? asteroidWeights : powerUpWeights;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField($"Prefab Mix ({PlacingNames[(int)placing]})", EditorStyles.miniBoldLabel);

        EditorGUI.BeginChangeCheck();
        weights[0] = Mathf.Max(0f, EditorGUILayout.FloatField(new GUIContent("Weight of the prefab above", "How likely the prefab above is picked, compared with the others. 0 = never."), weights[0]));
        for (int n = 0; n < extras.Length; n++)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                extras[n] = (GameObject)EditorGUILayout.ObjectField(new GUIContent($"Prefab {n + 2}", "An extra prefab to mix in. The box on its right is its weight (its relative chance); an empty slot or weight 0 is never picked."), extras[n], typeof(GameObject), false);
                weights[n + 1] = Mathf.Max(0f, EditorGUILayout.FloatField(weights[n + 1], GUILayout.Width(50)));
            }
        }
        if (EditorGUI.EndChangeCheck())
            SaveMix(asteroid ? "Asteroid" : "PowerUp", extras, weights);

        EditorGUILayout.HelpBox("Each object picks one prefab at random, in proportion to the weights. A prefab with no weight, or an empty slot, is never picked. With only the first weight above 0, nothing changes.", MessageType.None);
    }

    // The Presets section: pick a saved preset to load or delete, or type a name to save the current settings under.
    private static void DrawPresets()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Presets", EditorStyles.miniBoldLabel);

        string[] names = PresetNames();
        if (names.Length == 0)
        {
            EditorGUILayout.HelpBox("No presets saved yet.", MessageType.None);
        }
        else
        {
            selectedPreset = Mathf.Clamp(selectedPreset, 0, names.Length - 1);
            selectedPreset = EditorGUILayout.Popup(new GUIContent("Preset", "The saved presets, from Assets/Scripts/Editor/PatternPresets."), selectedPreset, names);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("Load", "Puts the chosen preset's settings into the builder. Objects already in the pattern aren't touched.")))
                {
                    LoadPreset(names[selectedPreset]);
                    presetName = names[selectedPreset]; // so saving again updates the same preset
                    GUIUtility.ExitGUI();               // the settings just changed, so stop drawing this pass
                }
                if (GUILayout.Button(new GUIContent("Delete Preset", "Deletes the chosen preset's file (it asks first)."))
                    && EditorUtility.DisplayDialog("Delete Preset", $"Delete the preset '{names[selectedPreset]}'?", "Delete", "Cancel"))
                {
                    DeletePreset(names[selectedPreset]);
                    GUIUtility.ExitGUI();
                }
            }
        }

        presetName = EditorGUILayout.TextField(new GUIContent("Name", "The name to save the current settings under."), presetName);
        using (new EditorGUI.DisabledScope(presetName.Trim() == ""))
        {
            if (GUILayout.Button(new GUIContent("Save Preset", "Saves the builder's current settings under this name. If the name is already used, it asks before overwriting.")))
            {
                if (!File.Exists(PresetPath(presetName))
                    || EditorUtility.DisplayDialog("Save Preset", $"A preset called '{presetName}' already exists. Overwrite it?", "Overwrite", "Cancel"))
                    SavePreset(presetName);
            }
        }
    }

    // -----------------------------------------------------------------------
    // Scene view preview
    //
    // OnSceneGUI runs whenever the Scene view redraws while this component's
    // object is selected. "Handles" is the editor's drawing API for the Scene
    // view (like Gizmos, but meant for editor tools).
    // -----------------------------------------------------------------------
    private void OnSceneGUI()
    {
        // Scene GUI runs for several event types (mouse moves, layout, etc).
        // Drawing only needs to happen on the Repaint event.
        if (Event.current.type != EventType.Repaint) return;

        var pattern = (PatternGizmos)target;

        // Draw in spawner space, same as the gizmo rings. DrawingScope sets
        // Handles' matrix and color and puts them back when the block ends,
        // so we don't leak our settings into other tools' drawing.
        Color previewColor = placing == Placing.Asteroid ? PreviewColor : PowerUpPreviewColor;
        using (new Handles.DrawingScope(previewColor, SpawnerSpace(pattern)))
        {
            Handles.color = WarningColor;
            foreach (var warning in fairnessWarnings)
                foreach (Transform t in warning.objects)
                    if (t != null)
                        Handles.DrawWireDisc(pattern.transform.localPosition + t.localPosition, Vector3.forward, previewSize * 0.7f);
            Handles.color = previewColor;

            if (gapWidth > 0f && (shape == Shape.Ring || shape == Shape.Arc))
            {
                var center = new Vector3(PatternGizmos.OrbitCenter.x, PatternGizmos.OrbitCenter.y, z);
                Vector3 gapStart = Quaternion.Euler(0f, 0f, gapCenter - gapWidth / 2f) * Vector3.right;

                Handles.color = GapColor;
                Handles.DrawSolidArc(center, Vector3.forward, gapStart, gapWidth, PatternGizmos.OrbitRadius);
                Handles.color = previewColor; // the spheres below use this color
            }

            for (int i = 0; i < PlacedCount; i++)
            {
                Vector3 p = ShapePosition(i);

                // SphereHandleCap draws a solid sphere. The first argument is a
                // control ID, which only matters for clickable handles; 0 is
                // fine for a picture like this.
                Handles.SphereHandleCap(0, p, Quaternion.identity, previewSize, EventType.Repaint);

                // Number each object so you can see the order it was
                // generated in (most useful for spirals).
                Handles.Label(p + Vector3.up * previewSize, (i + 1).ToString(), PreviewLabelStyle);

                // A Wave asteroid swings round the orbit, so show the whole stretch it covers.
                AsteroidBehavior wave = placing == Placing.Asteroid ? PickPrefab(i)?.GetComponent<AsteroidBehavior>() : null;
                if (wave != null && wave.Movement == AsteroidBehavior.MovementType.Wave)
                {
                    var center = new Vector3(PatternGizmos.OrbitCenter.x, PatternGizmos.OrbitCenter.y, p.z);
                    float angle = Mathf.Atan2(p.y - center.y, p.x - center.x) * Mathf.Rad2Deg;
                    Vector3 from = Quaternion.Euler(0f, 0f, angle - wave.Amplitude) * Vector3.right;

                    Handles.color = SweepColor;
                    Handles.DrawWireArc(center, Vector3.forward, from, wave.Amplitude * 2f, PatternGizmos.OrbitRadius);
                    Handles.color = previewColor; // the next sphere uses this color
                }
            }
        }
    }

    // Label style for the preview numbers. Created on first use because GUI
    // styles can't be built before the editor's GUI system is ready. Its color
    // follows the Asteroid / Power-Up toggle.
    private static GUIStyle previewLabelStyle;
    private static GUIStyle PreviewLabelStyle
    {
        get
        {
            if (previewLabelStyle == null)
                previewLabelStyle = new GUIStyle(EditorStyles.boldLabel);

            previewLabelStyle.normal.textColor =
                placing == Placing.Asteroid ? PreviewLabelColor : PowerUpPreviewLabelColor;
            return previewLabelStyle;
        }
    }

    // -----------------------------------------------------------------------
    // Shape math
    // -----------------------------------------------------------------------

    // How many objects the selected shape places. Twist places Count per ring, and Random can place fewer than Count if the gap doesn't fit.
    private static int PlacedCount
    {
        get
        {
            if (shape == Shape.Twist) return count * twistRings;
            if (shape != Shape.Random) return count;
            if (randomDirty) BuildRandomLayout();
            return randomAngles.Length;
        }
    }

    // Where object number i (0-based) goes for the selected shape, in spawner
    // space. Both the preview and Build call this, so they always agree.
    private static Vector3 ShapePosition(int i)
    {
        Vector3 p = BasePosition(i);
        return shape == Shape.Random ? p : Jitter(p, i);
    }

    // The shape's own position for object i, before any jitter.
    private static Vector3 BasePosition(int i)
    {
        switch (shape)
        {
            case Shape.Ring:
                if (gapWidth > 0f) return OnCircle(RingAngleWithGap(i), z);
                // 360 / count degrees apart, so the last one doesn't land on the first.
                return OnCircle(ringStartAngle + 360f * i / count, z);

            case Shape.Arc:
                if (gapWidth > 0f) return OnCircle(ArcAngleWithGap(i), z);
                // Lerp spreads them from arcFrom to arcTo, landing exactly on both
                // ends. A single object just goes at arcFrom.
                float t = count > 1 ? i / (float)(count - 1) : 0f;
                return OnCircle(Mathf.Lerp(arcFrom, arcTo, t), z);

            case Shape.Row:
                // (i - (count - 1) / 2) runs from -half to +half, centering the
                // row on the circle's x.
                float x = PatternGizmos.OrbitCenter.x + (i - (count - 1) * 0.5f) * rowSpacing;
                float y = PatternGizmos.OrbitCenter.y - PatternGizmos.OrbitRadius + rowYOffset;
                return new Vector3(x, y, z);

            case Shape.Spiral:
                return OnCircle(spiralStartAngle + spiralAngleStep * i, z + spiralZStep * i);

            case Shape.Zigzag:
                return OnCircle(i % 2 == 0 ? zigzagAngleA : zigzagAngleB, z + zigzagZStep * i);

            case Shape.Twist:
                int ring = i / count;   // which ring this object is in
                int inRing = i % count; // its place within that ring
                float ringAngle = gapWidth > 0f ? RingAngleWithGap(inRing) : ringStartAngle + 360f * inRing / count;
                return OnCircle(ringAngle + twistAngle * ring, z + twistZStep * ring);

            case Shape.Random:
                if (randomDirty) BuildRandomLayout();
                return OnCircle(randomAngles[i], z + randomDepths[i] * randomZRange);

            default:
                return Vector3.zero;
        }
    }

    // A point on the orbit circle. Cos gives the x part and Sin the y part of a
    // direction at that angle; multiplying by the radius pushes it out to the
    // circle's edge.
    private static Vector3 OnCircle(float angleDeg, float z)
    {
        float a = angleDeg * Mathf.Deg2Rad; // Mathf's trig functions work in radians
        float r = PatternGizmos.OrbitRadius;
        return new Vector3(
            PatternGizmos.OrbitCenter.x + Mathf.Cos(a) * r,
            PatternGizmos.OrbitCenter.y + Mathf.Sin(a) * r,
            z);
    }

    // Nudges a position by a small random amount round the orbit and in depth. The same seed and object number
    // always give the same nudge, and an object's nudge doesn't change when Count does.
    private static Vector3 Jitter(Vector3 p, int i)
    {
        if (jitterAngle <= 0f && jitterZ <= 0f) return p;

        var rng = new System.Random(randomSeed);
        for (int k = 0; k < i; k++)
        {
            rng.NextDouble(); // skip the numbers the earlier objects used
            rng.NextDouble();
        }
        float turn = ((float)rng.NextDouble() * 2f - 1f) * jitterAngle;
        float deeper = ((float)rng.NextDouble() * 2f - 1f) * jitterZ;

        Vector2 center = PatternGizmos.OrbitCenter;
        Vector2 turned = (Vector2)(Quaternion.Euler(0f, 0f, turn) * ((Vector2)p - center));
        return new Vector3(center.x + turned.x, center.y + turned.y, p.z + deeper);
    }

    // Ring with a gap: the objects fill the circle outside the gap, first and last at the gap's two edges.
    private static float RingAngleWithGap(int i)
    {
        float start = gapCenter + gapWidth / 2f;
        float step = count > 1 ? (360f - gapWidth) / (count - 1) : 0f;
        return start + step * i;
    }

    // Arc with a gap: the wedge is cut out of the arc, and the objects are spread evenly along what is left.
    private static float ArcAngleWithGap(int i)
    {
        float length = Mathf.Abs(arcTo - arcFrom);
        float dir = Mathf.Sign(arcTo - arcFrom); // which way round the arc runs

        // Where the wedge starts and ends, as distances along the arc from arcFrom, cut off at the arc's ends.
        float a = (gapCenter - gapWidth / 2f - arcFrom) * dir;
        float b = (gapCenter + gapWidth / 2f - arcFrom) * dir;
        float wedgeStart = Mathf.Clamp(Mathf.Min(a, b), 0f, length);
        float wedgeEnd = Mathf.Clamp(Mathf.Max(a, b), 0f, length);
        float removed = wedgeEnd - wedgeStart;

        float t = count > 1 ? i / (float)(count - 1) : 0f;
        float along = (length - removed) * t; // distance along what is left
        if (along > wedgeStart) along += removed; // jump over the wedge
        return arcFrom + dir * along;
    }

    // Fills randomAngles and randomDepths from the seed. The same seed and settings always give the same layout.
    private static void BuildRandomLayout()
    {
        var rng = new System.Random(randomSeed);
        var angles = new float[count];
        var depths = new float[count];
        int placed = 0;

        for (int i = 0; i < count; i++)
        {
            // Up to 50 tries to find an angle far enough from every one already placed.
            for (int attempt = 0; attempt < 50; attempt++)
            {
                float angle = Mathf.Lerp(randomFromAngle, randomToAngle, (float)rng.NextDouble());
                if (!FarEnough(angle, angles, placed)) continue;

                angles[placed] = angle;
                depths[placed] = (float)rng.NextDouble();
                placed++;
                break;
            }
        }

        // Keep only the ones that fit.
        Array.Resize(ref angles, placed);
        Array.Resize(ref depths, placed);
        randomAngles = angles;
        randomDepths = depths;
        randomDirty = false;
    }

    // True if the angle is at least randomMinGap degrees from each of the first `placed` angles.
    // DeltaAngle takes the short way round the circle, so -170 and 170 are 20 apart.
    private static bool FarEnough(float angle, float[] angles, int placed)
    {
        for (int i = 0; i < placed; i++)
            if (Mathf.Abs(Mathf.DeltaAngle(angle, angles[i])) < randomMinGap) return false;
        return true;
    }

    // The matrix that turns spawner-space positions into world positions for
    // drawing. In play the pattern's parent is the spawner; in Prefab Mode the
    // pattern root has no parent, so spawner space is just world space.
    private static Matrix4x4 SpawnerSpace(PatternGizmos pattern)
    {
        Transform parent = pattern.transform.parent;
        return parent != null ? parent.localToWorldMatrix : Matrix4x4.identity;
    }

    // -----------------------------------------------------------------------
    // Actions
    //
    // Undo: editor code has to tell Unity what it's about to change, or Ctrl+Z
    // won't know how to reverse it. Each action below opens an Undo "group",
    // records its changes, then collapses the group so the whole action undoes
    // with a single Ctrl+Z instead of one object at a time.
    // -----------------------------------------------------------------------

    // The prefab object number i is made from: a random pick weighted by the mix, or just the selected prefab if
    // nothing else is weighted. The same seed and object number always give the same pick.
    private static GameObject PickPrefab(int i)
    {
        bool asteroid = placing == Placing.Asteroid;
        GameObject[] extras = asteroid ? asteroidExtras : powerUpExtras;
        float[] weights = asteroid ? asteroidWeights : powerUpWeights;

        // The prefab field is entry 0, then the extras. An empty slot is never picked.
        GameObject[] options = { SelectedPrefab, extras[0], extras[1], extras[2] };
        float total = 0f;
        for (int n = 0; n < options.Length; n++)
            if (options[n] != null) total += weights[n];
        if (total <= 0f) return SelectedPrefab;

        // The pick's own stream of random numbers (not the jitter's), skipping the numbers earlier objects used.
        var rng = new System.Random(randomSeed + 4242);
        for (int k = 0; k < i; k++)
            rng.NextDouble();
        float roll = (float)rng.NextDouble() * total;

        // Walk down the entries until the roll falls inside one's share.
        for (int n = 0; n < options.Length; n++)
        {
            if (options[n] == null) continue;
            if (roll < weights[n]) return options[n];
            roll -= weights[n];
        }
        return SelectedPrefab; // only reached when the roll lands exactly on the total
    }

    private void Build(string undoName)
    {
        // "target" is the component this inspector is showing.
        var pattern = (PatternGizmos)target;

        // Children are positioned relative to the pattern root, so subtract the
        // root's own offset to turn spawner space into the child's localPosition.
        Vector3 rootOffset = pattern.transform.localPosition;

        Undo.SetCurrentGroupName(undoName);
        int group = Undo.GetCurrentGroup();

        for (int i = 0; i < PlacedCount; i++)
        {
            // InstantiatePrefab (unlike plain Instantiate) keeps the link to the
            // prefab, so later edits to it still apply to these copies.
            var placed = (GameObject)PrefabUtility.InstantiatePrefab(PickPrefab(i), pattern.transform);

            // Tells Undo "this object was just created"; undoing deletes it.
            Undo.RegisterCreatedObjectUndo(placed, undoName);

            placed.transform.localPosition = ShapePosition(i) - rootOffset;
        }

        if (autoNameOnAdd) NameChildren(pattern);

        Undo.CollapseUndoOperations(group);
    }

    // The pattern open in Prefab Mode, or null (after telling you) if there isn't one.
    private static PrefabStage OpenPatternStage()
    {
        PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
        if (stage != null && stage.prefabContentsRoot.GetComponent<PatternGizmos>() != null)
            return stage;

        EditorUtility.DisplayDialog("Pattern Builder", "Open a pattern prefab in Prefab Mode first.", "OK");
        return null;
    }

    // Deletes every child of a pattern root, in one undo step.
    private static void ClearChildren(Transform root)
    {
        Undo.SetCurrentGroupName("Clear Pattern");
        int group = Undo.GetCurrentGroup();

        // Backwards: deleting child 0 first would shift the others down and skip every second one.
        for (int i = root.childCount - 1; i >= 0; i--)
            Undo.DestroyObjectImmediate(root.GetChild(i).gameObject);

        Undo.CollapseUndoOperations(group);
    }

    // Names every child "Step N - Prefab K" (N = its step, K = its number within the step) and puts them in step order
    // in the Hierarchy, so the list reads like the Scene labels. One undo step.
    private static void NameChildren(PatternGizmos pattern)
    {
        Undo.RegisterFullObjectHierarchyUndo(pattern.gameObject, "Name Children");

        int sibling = 0;
        PatternGizmos.Step[] steps = pattern.GetSteps();
        for (int s = 0; s < steps.Length; s++)
        {
            for (int k = 0; k < steps[s].children.Length; k++)
            {
                Transform child = steps[s].children[k];
                GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(child.gameObject);
                string prefabName = source != null ? source.name : child.name;

                child.name = $"Step {s + 1} - {prefabName} {k + 1}";
                child.SetSiblingIndex(sibling++);
            }
        }
    }

    // Saves a copy of the open pattern as a new prefab, then (if ticked) empties the open one for the next pattern.
    [MenuItem("Tools/Pattern Builder/Save Pattern As...")]
    private static void SavePatternAs()
    {
        PrefabStage stage = OpenPatternStage();
        if (stage == null) return;

        string path = EditorUtility.SaveFilePanelInProject("Save Pattern As", "Pattern_New", "prefab", "Name for the new pattern.", PatternFolder);
        if (path == "") return; // cancelled

        PrefabUtility.SaveAsPrefabAsset(stage.prefabContentsRoot, path);
        if (clearAfterSave)
            ClearChildren(stage.prefabContentsRoot.transform);
    }

    // Saves the open pattern, opens Level 1 and plays it with only this pattern spawning, in a loop.
    [MenuItem("Tools/Pattern Builder/Test This Pattern")]
    private static void TestPattern()
    {
        PrefabStage stage = OpenPatternStage();
        if (stage == null) return;

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(TestScenePath) == null)
        {
            EditorUtility.DisplayDialog("Pattern Builder", $"Can't find {TestScenePath}.", "OK");
            return;
        }

        string path = stage.assetPath;
        PrefabUtility.SaveAsPrefabAsset(stage.prefabContentsRoot, path); // so the test uses what's on screen
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        StageUtility.GoToMainStage(); // leave Prefab Mode
        EditorPrefs.SetString(ReturnSceneKey, EditorSceneManager.GetActiveScene().path);
        PatternTest.Request(path, testLoopDelay);
        EditorPrefs.SetString(ReopenKey, path);
        EditorSceneManager.OpenScene(TestScenePath);
        EditorApplication.EnterPlaymode();
    }

    // The ObjectSpawner in any open scene. Prefab Mode hides the scene it came from but leaves it loaded.
    private static ObjectSpawner FindSpawner()
    {
        for (int i = 0; i < EditorSceneManager.sceneCount; i++)
        {
            var scene = EditorSceneManager.GetSceneAt(i);
            if (!scene.isLoaded) continue;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                ObjectSpawner spawner = root.GetComponentInChildren<ObjectSpawner>(true);
                if (spawner != null) return spawner;
            }
        }
        return null;
    }

    // Adds the open pattern as a new wave on the ObjectSpawner in the open scene (once).
    [MenuItem("Tools/Pattern Builder/Add to Spawner")]
    private static void AddToSpawner()
    {
        PrefabStage stage = OpenPatternStage();
        if (stage == null) return;

        ObjectSpawner spawner = FindSpawner();
        if (spawner == null)
        {
            EditorUtility.DisplayDialog("Pattern Builder", "No ObjectSpawner found in the open scenes.", "OK");
            return;
        }

        PrefabUtility.SaveAsPrefabAsset(stage.prefabContentsRoot, stage.assetPath); // so the wave uses what's on screen
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(stage.assetPath);

        var spawnerObject = new SerializedObject(spawner);
        SerializedProperty waves = spawnerObject.FindProperty("waves");
        for (int i = 0; i < waves.arraySize; i++)
        {
            if (waves.GetArrayElementAtIndex(i).FindPropertyRelative("pattern").objectReferenceValue == prefab)
            {
                EditorUtility.DisplayDialog("Pattern Builder", "That pattern is already in the spawner's waves.", "OK");
                return;
            }
        }

        waves.arraySize++;
        waves.GetArrayElementAtIndex(waves.arraySize - 1).FindPropertyRelative("pattern").objectReferenceValue = prefab;
        spawnerObject.ApplyModifiedProperties();
        EditorSceneManager.MarkSceneDirty(spawner.gameObject.scene);
    }

    private void DeleteAll()
    {
        var pattern = (PatternGizmos)target;

        // DisplayDialog shows a popup and returns true if the first button was clicked.
        if (!EditorUtility.DisplayDialog("Delete All Children",
                $"Delete all {pattern.transform.childCount} children of {pattern.name}?", "Delete", "Cancel"))
            return;

        ClearChildren(pattern.transform);
    }

    // Selecting individual children deselects the pattern root, which hides
    // this inspector and its buttons. So snapping a selection lives in the
    // menu bar instead: Tools > Pattern Builder > Snap Selected to Circle.
    // MenuItem methods must be static; Unity finds them automatically.
    [MenuItem("Tools/Pattern Builder/Snap Selected to Circle")]
    private static void SnapSelected()
    {
        foreach (Transform t in Selection.transforms)
        {
            // Only snap direct children of a pattern that has PatternGizmos.
            var pattern = t.parent != null ? t.parent.GetComponent<PatternGizmos>() : null;
            if (pattern != null)
                Snap(pattern, new[] { t }, "Snap Selected to Circle");
        }
    }

    // Moves each child straight out (or in) from the orbit center until it
    // sits on the circle, keeping its angle and Z.
    private static void Snap(PatternGizmos pattern, Transform[] children, string undoName)
    {
        Vector3 rootOffset = pattern.transform.localPosition;

        foreach (Transform t in children)
        {
            Vector3 p = rootOffset + t.localPosition; // this child in spawner space
            Vector2 fromCenter = (Vector2)p - PatternGizmos.OrbitCenter;

            // A child exactly at the center has no direction to push along.
            if (fromCenter.sqrMagnitude < 1e-6f) continue;

            // Atan2 turns an (x, y) direction back into an angle.
            float angle = Mathf.Atan2(fromCenter.y, fromCenter.x) * Mathf.Rad2Deg;

            // RecordObject snapshots the transform so the move can be undone.
            Undo.RecordObject(t, undoName);
            t.localPosition = OnCircle(angle, p.z) - rootOffset;
        }
    }

    // Moves each child to a new spot, keeping its depth. `move` takes a child's offset from the orbit center
    // (in spawner space) and returns its new offset. One undo step for the whole move.
    private static void MoveChildren(PatternGizmos pattern, Transform[] children, string undoName, Func<Vector2, Vector2> move)
    {
        Vector3 rootOffset = pattern.transform.localPosition;

        Undo.SetCurrentGroupName(undoName);
        int group = Undo.GetCurrentGroup();

        foreach (Transform t in children)
        {
            Vector3 p = rootOffset + t.localPosition; // this child in spawner space
            Vector2 moved = PatternGizmos.OrbitCenter + move((Vector2)p - PatternGizmos.OrbitCenter);

            Undo.RecordObject(t, undoName);
            t.localPosition = new Vector3(moved.x, moved.y, p.z) - rootOffset;
        }

        Undo.CollapseUndoOperations(group);
    }

    // Rotates the given children around the orbit center by rotateBy degrees.
    private static void RotateChildren(PatternGizmos pattern, Transform[] children)
    {
        Quaternion turn = Quaternion.Euler(0f, 0f, rotateBy);
        MoveChildren(pattern, children, "Rotate Pattern", offset => (Vector2)(turn * offset));
    }

    // The direct children of a transform, as an array.
    private static Transform[] ChildrenOf(Transform root)
    {
        var children = new Transform[root.childCount];
        for (int i = 0; i < children.Length; i++)
            children[i] = root.GetChild(i);
        return children;
    }

    // The selected direct children of one pattern (the first one found), or none. Same rule as Snap Selected.
    private static Transform[] SelectedPatternChildren(out PatternGizmos pattern)
    {
        Transform[] direct = Array.FindAll(Selection.transforms, t => t.parent != null && t.parent.GetComponent<PatternGizmos>() != null);
        PatternGizmos found = direct.Length > 0 ? direct[0].parent.GetComponent<PatternGizmos>() : null;
        pattern = found;
        return found != null ? Array.FindAll(direct, t => t.parent == found.transform) : direct;
    }

    [MenuItem("Tools/Pattern Builder/Rotate Selected")]
    private static void RotateSelected()
    {
        Transform[] selected = SelectedPatternChildren(out PatternGizmos pattern);
        if (pattern != null) RotateChildren(pattern, selected);
    }

    // Flips the given children left-right across the vertical line through the orbit center.
    private static void MirrorChildren(PatternGizmos pattern, Transform[] children)
    {
        MoveChildren(pattern, children, "Mirror Pattern", offset => new Vector2(-offset.x, offset.y));
    }

    [MenuItem("Tools/Pattern Builder/Mirror Selected")]
    private static void MirrorSelected()
    {
        Transform[] selected = SelectedPatternChildren(out PatternGizmos pattern);
        if (pattern != null) MirrorChildren(pattern, selected);
    }

    // Gives each child a new depth (its local Z), keeping its angle and distance from the center. One undo step.
    private static void ChangeDepth(Transform[] children, string undoName, Func<float, float> newZ)
    {
        Undo.SetCurrentGroupName(undoName);
        int group = Undo.GetCurrentGroup();

        foreach (Transform t in children)
        {
            Undo.RecordObject(t, undoName);
            Vector3 p = t.localPosition;
            t.localPosition = new Vector3(p.x, p.y, newZ(p.z));
        }

        Undo.CollapseUndoOperations(group);
    }

    // Moves the given children shiftZBy deeper.
    private static void ShiftChildrenZ(Transform[] children)
    {
        ChangeDepth(children, "Shift Pattern Z", z => z + shiftZBy);
    }

    // Flips the depth order of the given children: the nearest becomes the furthest, and the middle stays put.
    private static void ReverseChildrenZ(Transform[] children)
    {
        if (children.Length == 0) return;

        float min = float.MaxValue;
        float max = float.MinValue;
        foreach (Transform t in children)
        {
            min = Mathf.Min(min, t.localPosition.z);
            max = Mathf.Max(max, t.localPosition.z);
        }
        ChangeDepth(children, "Reverse Pattern Z", z => min + max - z);
    }

    [MenuItem("Tools/Pattern Builder/Shift Selected Z")]
    private static void ShiftSelectedZ()
    {
        Transform[] selected = SelectedPatternChildren(out PatternGizmos pattern);
        if (pattern != null) ShiftChildrenZ(selected);
    }

    [MenuItem("Tools/Pattern Builder/Reverse Selected Z")]
    private static void ReverseSelectedZ()
    {
        Transform[] selected = SelectedPatternChildren(out PatternGizmos pattern);
        if (pattern != null) ReverseChildrenZ(selected);
    }

    // Spaces the given children evenly in depth: the nearest and furthest stay put, and the ones between are respaced.
    private static void DistributeDepth(Transform[] children)
    {
        if (children.Length < 3) return;

        Transform[] sorted = (Transform[])children.Clone();
        Array.Sort(sorted, (a, b) => a.localPosition.z.CompareTo(b.localPosition.z));
        float first = sorted[0].localPosition.z;
        float last = sorted[sorted.Length - 1].localPosition.z;

        Undo.SetCurrentGroupName("Distribute Z");
        int group = Undo.GetCurrentGroup();
        for (int i = 1; i < sorted.Length - 1; i++)
        {
            Undo.RecordObject(sorted[i], "Distribute Z");
            Vector3 p = sorted[i].localPosition;
            sorted[i].localPosition = new Vector3(p.x, p.y, Mathf.Lerp(first, last, i / (float)(sorted.Length - 1)));
        }
        Undo.CollapseUndoOperations(group);
    }

    // Spaces the given children evenly round the circle, starting at the one with the lowest angle (-180 to 180).
    // Each keeps its depth and its distance from the orbit center.
    private static void DistributeAngle(PatternGizmos pattern, Transform[] children)
    {
        if (children.Length < 2) return;

        Vector3 rootOffset = pattern.transform.localPosition;
        Vector2 center = PatternGizmos.OrbitCenter;

        // Each child's angle and distance from the center, in spawner space.
        var polar = new (Transform t, float angle, float distance)[children.Length];
        for (int i = 0; i < children.Length; i++)
        {
            Vector2 offset = (Vector2)(rootOffset + children[i].localPosition) - center;
            polar[i] = (children[i], Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg, offset.magnitude);
        }
        Array.Sort(polar, (a, b) => a.angle.CompareTo(b.angle));

        Undo.SetCurrentGroupName("Distribute Angle");
        int group = Undo.GetCurrentGroup();
        for (int k = 0; k < polar.Length; k++)
        {
            float angle = (polar[0].angle + 360f * k / polar.Length) * Mathf.Deg2Rad;
            Vector2 spot = center + polar[k].distance * new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

            Undo.RecordObject(polar[k].t, "Distribute Angle");
            polar[k].t.localPosition = new Vector3(spot.x - rootOffset.x, spot.y - rootOffset.y, polar[k].t.localPosition.z);
        }
        Undo.CollapseUndoOperations(group);
    }

    [MenuItem("Tools/Pattern Builder/Distribute Selected by Depth")]
    private static void DistributeSelectedDepth()
    {
        Transform[] selected = SelectedPatternChildren(out PatternGizmos pattern);
        if (pattern != null) DistributeDepth(selected);
    }

    [MenuItem("Tools/Pattern Builder/Distribute Selected by Angle")]
    private static void DistributeSelectedAngle()
    {
        Transform[] selected = SelectedPatternChildren(out PatternGizmos pattern);
        if (pattern != null) DistributeAngle(pattern, selected);
    }
}
