// ---------------------------------------------------------------------------
// PatternGizmosEditor
//
// A "custom inspector": Unity lets you replace the panel it draws in the
// Inspector window for a component. This one draws PatternGizmos' normal
// fields and then adds a Pattern Builder underneath, with buttons that place
// Asteroid prefabs on the player's orbit circle.
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
using UnityEngine;

// This attribute tells Unity: "whenever a PatternGizmos component is shown in
// the Inspector, use this class to draw it."
[CustomEditor(typeof(PatternGizmos))]
public class PatternGizmosEditor : Editor
{
    // The shapes the builder knows how to lay out. The tab bar in the
    // inspector picks one, and both the preview and the Add button use it.
    private enum Shape { Ring, Arc, Row, Spiral }
    private static readonly string[] ShapeNames = { "Ring", "Arc", "Row", "Spiral" };

    // EditorPrefs is a small key/value store saved on your machine (not in the
    // project). We use it to remember which asteroid prefab you picked, so you
    // don't have to re-pick it every time Unity restarts.
    private const string PrefabPrefKey = "PatternBuilder.AsteroidPrefab";
    private const string DefaultPrefabPath = "Assets/Prefabs/Asteroid Types/Asteroid.prefab";

    // Preview colors: translucent green reads as "not placed yet".
    private static readonly Color PreviewColor = new Color(0.3f, 1f, 0.4f, 0.5f);
    private static readonly Color PreviewLabelColor = new Color(0.3f, 1f, 0.4f, 1f);

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

    // Shared by every shape. The radius is always the orbit's, from OrbitSettings.
    private static float z;                // depth; lower Z spawns first
    private static int count = 8;          // how many asteroids to place
    private static float previewSize = 1f; // diameter of the preview spheres

    // Ring: count asteroids evenly spaced all the way around.
    private static float ringStartAngle = -90f;

    // Arc: count asteroids spread evenly from one angle to another (inclusive).
    private static float arcFrom = -135f;
    private static float arcTo = -45f;

    // Row: a straight horizontal line centered on the circle. Its height is
    // measured up from the bottom of the circle (where the player sits), so it
    // follows the orbit if OrbitSettings changes.
    private static float rowYOffset;
    private static float rowSpacing = 1f;

    // Spiral: each asteroid steps around by an angle AND back by a Z amount,
    // so each one is its own spawn step and they appear one after another.
    private static float spiralStartAngle = -90f;
    private static float spiralAngleStep = 30f;
    private static float spiralZStep = 2f;

    // Called when this inspector is created (i.e. when you select a pattern).
    private void OnEnable()
    {
        // Load the remembered prefab path, falling back to the default one.
        if (asteroidPrefab == null)
            asteroidPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                EditorPrefs.GetString(PrefabPrefKey, DefaultPrefabPath));
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
            "Z is in spawner space; lower Z spawns first. The green spheres in the Scene view preview the next Add.",
            MessageType.None);

        // BeginChangeCheck / EndChangeCheck wraps a group of fields and tells
        // us whether any of them changed this frame.
        EditorGUI.BeginChangeCheck();
        asteroidPrefab = (GameObject)EditorGUILayout.ObjectField(
            "Asteroid Prefab", asteroidPrefab, typeof(GameObject), false); // false = assets only, no scene objects
        if (EditorGUI.EndChangeCheck())
            EditorPrefs.SetString(PrefabPrefKey, asteroidPrefab != null ? AssetDatabase.GetAssetPath(asteroidPrefab) : "");

        // Everything below affects the preview, so if any of it changes we ask
        // the Scene view to redraw right away instead of waiting for the mouse
        // to move over it.
        EditorGUI.BeginChangeCheck();

        EditorGUILayout.Space();
        shape = (Shape)GUILayout.Toolbar((int)shape, ShapeNames); // the tab bar

        z = EditorGUILayout.FloatField("Z", z);
        count = Mathf.Max(1, EditorGUILayout.IntField("Count", count)); // never fewer than 1
        previewSize = Mathf.Max(0.05f, EditorGUILayout.FloatField("Preview Size", previewSize));

        // Only show the settings for the selected shape.
        switch (shape)
        {
            case Shape.Ring:
                ringStartAngle = EditorGUILayout.FloatField("Start Angle", ringStartAngle);
                break;
            case Shape.Arc:
                arcFrom = EditorGUILayout.FloatField("From Angle", arcFrom);
                arcTo = EditorGUILayout.FloatField("To Angle", arcTo);
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
        }

        if (EditorGUI.EndChangeCheck())
            SceneView.RepaintAll();

        // DisabledScope greys out (and blocks) everything inside it while the
        // condition is true, so you can't click Add without a prefab.
        using (new EditorGUI.DisabledScope(asteroidPrefab == null))
        {
            if (GUILayout.Button($"Add {ShapeNames[(int)shape]}"))
                Build($"Add {ShapeNames[(int)shape]}");
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Existing Asteroids", EditorStyles.miniBoldLabel);

        if (GUILayout.Button("Snap All to Circle"))
        {
            var pattern = (PatternGizmos)target;
            var children = new Transform[pattern.transform.childCount];
            for (int i = 0; i < children.Length; i++)
                children[i] = pattern.transform.GetChild(i);
            Snap(pattern, children, "Snap All to Circle");
        }

        if (GUILayout.Button("Delete All Asteroids"))
            DeleteAll();
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
        using (new Handles.DrawingScope(PreviewColor, SpawnerSpace(pattern)))
        {
            for (int i = 0; i < count; i++)
            {
                Vector3 p = ShapePosition(i);

                // SphereHandleCap draws a solid sphere. The first argument is a
                // control ID, which only matters for clickable handles; 0 is
                // fine for a picture like this.
                Handles.SphereHandleCap(0, p, Quaternion.identity, previewSize, EventType.Repaint);

                // Number each asteroid so you can see the order it was
                // generated in (most useful for spirals).
                Handles.Label(p + Vector3.up * previewSize, (i + 1).ToString(), PreviewLabelStyle);
            }
        }
    }

    // Label style for the preview numbers. Created on first use because GUI
    // styles can't be built before the editor's GUI system is ready.
    private static GUIStyle previewLabelStyle;
    private static GUIStyle PreviewLabelStyle
    {
        get
        {
            if (previewLabelStyle == null)
            {
                previewLabelStyle = new GUIStyle(EditorStyles.boldLabel);
                previewLabelStyle.normal.textColor = PreviewLabelColor;
            }
            return previewLabelStyle;
        }
    }

    // -----------------------------------------------------------------------
    // Shape math
    // -----------------------------------------------------------------------

    // Where asteroid number i (0-based) goes for the selected shape, in spawner
    // space. Both the preview and Build call this, so they always agree.
    private static Vector3 ShapePosition(int i)
    {
        switch (shape)
        {
            case Shape.Ring:
                // 360 / count degrees apart, so the last one doesn't land on the first.
                return OnCircle(ringStartAngle + 360f * i / count, z);

            case Shape.Arc:
                // Lerp spreads them from arcFrom to arcTo, landing exactly on both
                // ends. A single asteroid just goes at arcFrom.
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
    // with a single Ctrl+Z instead of one asteroid at a time.
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

        for (int i = 0; i < count; i++)
        {
            // InstantiatePrefab (unlike plain Instantiate) keeps the link to the
            // Asteroid prefab, so later edits to Asteroid.prefab still apply to
            // these copies.
            var asteroid = (GameObject)PrefabUtility.InstantiatePrefab(asteroidPrefab, pattern.transform);

            // Tells Undo "this object was just created"; undoing deletes it.
            Undo.RegisterCreatedObjectUndo(asteroid, undoName);

            asteroid.transform.localPosition = ShapePosition(i) - rootOffset;
        }

        Undo.CollapseUndoOperations(group);
    }

    private void DeleteAll()
    {
        var pattern = (PatternGizmos)target;

        // DisplayDialog shows a popup and returns true if the first button was clicked.
        if (!EditorUtility.DisplayDialog("Delete All Asteroids",
                $"Delete all {pattern.transform.childCount} children of {pattern.name}?", "Delete", "Cancel"))
            return;

        Undo.SetCurrentGroupName("Delete All Asteroids");
        int group = Undo.GetCurrentGroup();

        // Loop backwards: deleting child 0 would shift every other child's
        // index down by one and we'd skip every second asteroid.
        for (int i = pattern.transform.childCount - 1; i >= 0; i--)
            Undo.DestroyObjectImmediate(pattern.transform.GetChild(i).gameObject); // undoable delete

        Undo.CollapseUndoOperations(group);
    }

    // Selecting individual asteroids deselects the pattern root, which hides
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

    // Moves each asteroid straight out (or in) from the orbit center until it
    // sits on the circle, keeping its angle and Z.
    private static void Snap(PatternGizmos pattern, Transform[] asteroids, string undoName)
    {
        Vector3 rootOffset = pattern.transform.localPosition;

        foreach (Transform t in asteroids)
        {
            Vector3 p = rootOffset + t.localPosition; // this asteroid in spawner space
            Vector2 fromCenter = (Vector2)p - PatternGizmos.OrbitCenter;

            // An asteroid exactly at the center has no direction to push along.
            if (fromCenter.sqrMagnitude < 1e-6f) continue;

            // Atan2 turns an (x, y) direction back into an angle.
            float angle = Mathf.Atan2(fromCenter.y, fromCenter.x) * Mathf.Rad2Deg;

            // RecordObject snapshots the transform so the move can be undone.
            Undo.RecordObject(t, undoName);
            t.localPosition = OnCircle(angle, p.z) - rootOffset;
        }
    }
}
