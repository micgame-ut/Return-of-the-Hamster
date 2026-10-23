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
    private enum Shape { Ring, Arc, Row, Spiral, Random }
    private static readonly string[] ShapeNames = { "Ring", "Arc", "Row", "Spiral", "Random" };

    // EditorPrefs is a small key/value store saved on your machine (not in the
    // project). We use it to remember which asteroid prefab you picked, so you
    // don't have to re-pick it every time Unity restarts.
    private const string PrefabPrefKey = "PatternBuilder.AsteroidPrefab";
    private const string DefaultPrefabPath = "Assets/Prefabs/Asteroid Types/Asteroid.prefab";
    private const string PowerUpPrefKey = "PatternBuilder.PowerUpPrefab";
    private const string DefaultPowerUpPath = "Assets/Prefabs/Health PowerUp.prefab";
    private const string PatternFolder = "Assets/Prefabs/Patterns";
    private const string TestScenePath = "Assets/Scenes/PatternTest.unity";
    private const string ReopenKey = "PatternBuilder.ReopenPattern"; // the pattern to reopen in Prefab Mode when the test ends

    // Preview colors: translucent green reads as "not placed yet".
    private static readonly Color PreviewColor = new Color(0.3f, 1f, 0.4f, 0.5f);
    private static readonly Color PreviewLabelColor = new Color(0.3f, 1f, 0.4f, 1f);

    // Power-up previews are yellow, so you can tell which kind the next Add places.
    private static readonly Color PowerUpPreviewColor = new Color(1f, 0.9f, 0.2f, 0.5f);
    private static readonly Color PowerUpPreviewLabelColor = new Color(1f, 0.9f, 0.2f, 1f);

    // The gap on a Ring or Arc is drawn as a translucent red wedge.
    private static readonly Color GapColor = new Color(1f, 0.25f, 0.25f, 0.3f);

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

    // Which slot the Add button places from.
    private enum Placing { Asteroid, PowerUp }
    private static readonly string[] PlacingNames = { "Asteroid", "Power-Up" };
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

    // Wave tools: Save Pattern As, Test This Pattern, Add to Spawner.
    private static bool clearAfterSave = true; // Save Pattern As empties the open pattern afterwards
    private static float testLoopDelay = 3f;   // seconds between test runs
    private static float rotateBy = 30f;       // degrees; positive = counter-clockwise

    static PatternGizmosEditor()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;

        // A request left over from an editor crash would turn the next normal Play into a test.
        if (!EditorApplication.isPlayingOrWillChangePlaymode)
            PatternTest.Clear();
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredEditMode) return;

        PatternTest.Clear();

        // Back where you were: reopen the pattern in Prefab Mode.
        string path = EditorPrefs.GetString(ReopenKey, "");
        EditorPrefs.DeleteKey(ReopenKey);
        if (path != "")
            EditorApplication.delayCall += () => PrefabStageUtility.OpenPrefab(path);
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

        // BeginChangeCheck / EndChangeCheck wraps a group of fields and tells
        // us whether any of them changed this frame.
        EditorGUI.BeginChangeCheck();
        asteroidPrefab = (GameObject)EditorGUILayout.ObjectField(
            "Asteroid Prefab", asteroidPrefab, typeof(GameObject), false); // false = assets only, no scene objects
        if (EditorGUI.EndChangeCheck())
            EditorPrefs.SetString(PrefabPrefKey, asteroidPrefab != null ? AssetDatabase.GetAssetPath(asteroidPrefab) : "");

        EditorGUI.BeginChangeCheck();
        powerUpPrefab = (GameObject)EditorGUILayout.ObjectField(
            "Power-Up Prefab", powerUpPrefab, typeof(GameObject), false);
        if (EditorGUI.EndChangeCheck())
            EditorPrefs.SetString(PowerUpPrefKey, powerUpPrefab != null ? AssetDatabase.GetAssetPath(powerUpPrefab) : "");

        // Everything below affects the preview, so if any of it changes we ask
        // the Scene view to redraw right away instead of waiting for the mouse
        // to move over it.
        EditorGUI.BeginChangeCheck();

        EditorGUILayout.Space();
        placing = (Placing)GUILayout.Toolbar((int)placing, PlacingNames); // what Add places
        shape = (Shape)GUILayout.Toolbar((int)shape, ShapeNames); // the tab bar

        z = EditorGUILayout.FloatField("Z", z);
        count = Mathf.Max(1, EditorGUILayout.IntField("Count", count)); // never fewer than 1
        previewSize = Mathf.Max(0.05f, EditorGUILayout.FloatField("Preview Size", previewSize));

        // Only show the settings for the selected shape.
        switch (shape)
        {
            case Shape.Ring:
                ringStartAngle = EditorGUILayout.FloatField("Start Angle", ringStartAngle);
                DrawGapFields();
                if (gapWidth > 0f)
                    EditorGUILayout.HelpBox("Start Angle is ignored while Gap Width is above 0: the gap sets where the ring begins.", MessageType.None);
                break;
            case Shape.Arc:
                arcFrom = EditorGUILayout.FloatField("From Angle", arcFrom);
                arcTo = EditorGUILayout.FloatField("To Angle", arcTo);
                DrawGapFields();
                if (gapWidth > 0f && (gapCenter + gapWidth / 2f < Mathf.Min(arcFrom, arcTo) || gapCenter - gapWidth / 2f > Mathf.Max(arcFrom, arcTo)))
                    EditorGUILayout.HelpBox("The gap is outside the arc, so it has no effect.", MessageType.Warning);
                break;
            case Shape.Row:
                rowYOffset = EditorGUILayout.FloatField("Height Above Bottom", rowYOffset);
                rowSpacing = EditorGUILayout.FloatField("Spacing", rowSpacing);
                break;
            case Shape.Spiral:
                spiralStartAngle = EditorGUILayout.FloatField("Start Angle", spiralStartAngle);
                spiralAngleStep = EditorGUILayout.FloatField("Angle Step", spiralAngleStep);
                spiralZStep = EditorGUILayout.FloatField("Z Step", spiralZStep);
                break;
            case Shape.Random:
                randomFromAngle = EditorGUILayout.FloatField("From Angle", randomFromAngle);
                randomToAngle = EditorGUILayout.FloatField("To Angle", randomToAngle);
                randomZRange = EditorGUILayout.FloatField("Z Range", randomZRange);
                randomMinGap = Mathf.Max(0f, EditorGUILayout.FloatField("Min Gap", randomMinGap));
                randomSeed = EditorGUILayout.IntField("Seed", randomSeed);
                if (GUILayout.Button("Reroll"))
                {
                    randomSeed = Environment.TickCount; // a different number each click
                    GUI.changed = true;                 // so the change check below sees it
                }
                break;
        }

        if (EditorGUI.EndChangeCheck())
        {
            randomDirty = true;
            SceneView.RepaintAll();
        }

        if (shape == Shape.Random && PlacedCount < count)
            EditorGUILayout.HelpBox($"Placed {PlacedCount} of {count}: the minimum gap doesn't fit in the angle range.", MessageType.Warning);

        // DisabledScope greys out (and blocks) everything inside it while the
        // condition is true, so you can't click Add without a prefab.
        using (new EditorGUI.DisabledScope(SelectedPrefab == null))
        {
            string addName = $"Add {PlacingNames[(int)placing]} {ShapeNames[(int)shape]}";
            if (GUILayout.Button(addName))
                Build(addName);
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Waves", EditorStyles.miniBoldLabel);
        clearAfterSave = EditorGUILayout.Toggle("Clear After Save", clearAfterSave);
        if (GUILayout.Button("Save Pattern As...")) SavePatternAs();
        testLoopDelay = Mathf.Max(0f, EditorGUILayout.FloatField("Test Loop Delay", testLoopDelay));
        if (GUILayout.Button("Test This Pattern")) TestPattern();
        if (GUILayout.Button("Add to Spawner")) AddToSpawner();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Existing Children", EditorStyles.miniBoldLabel);

        if (GUILayout.Button("Snap All to Circle"))
        {
            var pattern = (PatternGizmos)target;
            var children = new Transform[pattern.transform.childCount];
            for (int i = 0; i < children.Length; i++)
                children[i] = pattern.transform.GetChild(i);
            Snap(pattern, children, "Snap All to Circle");
        }

        rotateBy = EditorGUILayout.FloatField("Rotate By", rotateBy);
        var openPattern = (PatternGizmos)target;
        if (GUILayout.Button("Rotate All"))
            RotateChildren(openPattern, ChildrenOf(openPattern.transform));
        if (GUILayout.Button("Mirror All"))
            MirrorChildren(openPattern, ChildrenOf(openPattern.transform));

        if (GUILayout.Button("Delete All Children"))
            DeleteAll();
    }

    // Gap Width and Gap Center, shared by Ring and Arc.
    private static void DrawGapFields()
    {
        gapWidth = Mathf.Clamp(EditorGUILayout.FloatField("Gap Width", gapWidth), 0f, 359f);
        gapCenter = EditorGUILayout.FloatField("Gap Center", gapCenter);
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

    // How many objects the selected shape places. Random can place fewer than Count if the gap doesn't fit.
    private static int PlacedCount
    {
        get
        {
            if (shape != Shape.Random) return count;
            if (randomDirty) BuildRandomLayout();
            return randomAngles.Length;
        }
    }

    // Where object number i (0-based) goes for the selected shape, in spawner
    // space. Both the preview and Build call this, so they always agree.
    private static Vector3 ShapePosition(int i)
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
            var placed = (GameObject)PrefabUtility.InstantiatePrefab(SelectedPrefab, pattern.transform);

            // Tells Undo "this object was just created"; undoing deletes it.
            Undo.RegisterCreatedObjectUndo(placed, undoName);

            placed.transform.localPosition = ShapePosition(i) - rootOffset;
        }

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
}
