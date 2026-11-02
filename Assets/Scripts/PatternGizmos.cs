using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

// Editor-only guides for authoring wave patterns. Draws the player's orbit
// circle at every Z step so you can see what the player can reach, and labels
// each step in the order ObjectSpawner reveals them. Does nothing at runtime.
public class PatternGizmos : MonoBehaviour
{
    // The Game scene's layout in spawner space, read from OrbitSettings so the
    // preview always matches play.
    public static float OrbitRadius => OrbitSettings.Instance.OrbitRadius;
    public static Vector2 OrbitCenter => OrbitSettings.Instance.OrbitCenterFromSpawner;
    private static float PlayerZ => OrbitSettings.Instance.PlayerZFromSpawner;

    [Tooltip("Color of the rings drawn at each asteroid depth.")]
    [SerializeField] private Color ringColor = new Color(0.3f, 0.8f, 1f, 0.6f);
    [Tooltip("Color of the ring drawn at the player's depth.")]
    [SerializeField] private Color playerRingColor = new Color(1f, 0.8f, 0.2f, 0.8f);
    [Tooltip("Show a Magnet's pull reach and a Frost comet's patch circle in the Scene view, for the ones in the pattern and the one being placed.")]
    [SerializeField] private bool showEffectRadii = true;
    [Tooltip("Color of the circle showing a Magnet's pull reach at the player's depth.")]
    [SerializeField] private Color magnetColor = new Color(0.7f, 0.4f, 1f, 0.8f);
    [Tooltip("Color of the circle showing where a Frost comet's patch lands on the ring.")]
    [SerializeField] private Color frostColor = new Color(0.5f, 0.9f, 1f, 0.9f);

#if UNITY_EDITOR
    // One depth the pattern reveals at: its Z (spawner space), how many children share it, and how many are power-ups.
    public struct Step
    {
        public float z;
        public int count;
        public int powerUps;
        public Transform[] children; // the objects in this step
    }

    // The pattern's children grouped into steps, nearest the camera (lowest Z) first, same order as ObjectSpawner.
    public Step[] GetSteps()
    {
        Vector3 rootOffset = transform.localPosition;

        var items = new List<(float z, bool powerUp, Transform child)>();
        foreach (Transform child in transform)
            items.Add((rootOffset.z + child.localPosition.z, child.GetComponent<PowerUp>() != null, child));
        items.Sort((a, b) => a.z.CompareTo(b.z));

        // Group equal Zs into one step, since they spawn together.
        var steps = new List<Step>();
        var members = new List<List<Transform>>(); // the children of each step, in the same order as steps
        for (int i = 0; i < items.Count; i++)
        {
            int powerUp = items[i].powerUp ? 1 : 0;
            if (i > 0 && items[i].z - items[i - 1].z <= ObjectSpawner.SameZTolerance)
            {
                Step last = steps[steps.Count - 1];
                last.count++;
                last.powerUps += powerUp;
                steps[steps.Count - 1] = last;
                members[members.Count - 1].Add(items[i].child);
            }
            else
            {
                steps.Add(new Step { z = items[i].z, count = 1, powerUps = powerUp });
                members.Add(new List<Transform> { items[i].child });
            }
        }

        for (int s = 0; s < steps.Count; s++)
        {
            Step step = steps[s];
            step.children = members[s].ToArray();
            steps[s] = step;
        }
        return steps.ToArray();
    }

    // The player Speed the step times are worked out at. Set by the pattern builder's Timing section.
    public static float PreviewSpeed = 50f;

    private static SpawnableTemplate Template => Resources.Load<SpawnableTemplate>("SpawnableTemplate");

    // How fast things close on the player at a player Speed, in units per second, like AsteroidBehavior.ApproachSpeed.
    // 0 if the SpawnableTemplate asset isn't found.
    public static float ApproachSpeedAt(float playerSpeed)
    {
        SpawnableTemplate template = Template;
        return template != null ? template.baseApproachSpeed + playerSpeed * template.speedMultiplier : 0f;
    }

    // Seconds from one step appearing to the next: the Z gap divided by the approach speed, like ObjectSpawner.
    public static float SecondsBetween(Step from, Step to, float playerSpeed)
    {
        float speed = ApproachSpeedAt(playerSpeed);
        return speed > 0f ? (to.z - from.z) / speed : 0f;
    }

    // Seconds from a step appearing at the spawn plane until it reaches the player: the warning wait plus the flight.
    public static float SecondsToPlayer(float spawnZ, float playerSpeed)
    {
        SpawnableTemplate template = Template;
        float speed = ApproachSpeedAt(playerSpeed);
        return template != null && speed > 0f ? template.warningTime + (spawnZ - PlayerZ) / speed : 0f;
    }

    private void OnDrawGizmos()
    {
        // Only for building patterns: skipped in Play, where pooled copies would redraw every frame.
        if (Application.isPlaying) return;

        // Draw in spawner space, so the pattern root's own offset is respected
        // both in Prefab Mode (no parent) and when the pattern sits under the spawner in a scene.
        // The scope restores Handles' matrix and color for other gizmos afterwards.
        Matrix4x4 space = transform.parent != null ? transform.parent.localToWorldMatrix : Matrix4x4.identity;
        using var scope = new Handles.DrawingScope(space);
        Step[] steps = GetSteps();

        Handles.color = playerRingColor;
        DrawRing(PlayerZ, "Player");

        Handles.color = ringColor;
        for (int i = 0; i < steps.Length; i++)
        {
            int pu = steps[i].powerUps;
            string extra = pu == 0 ? "" : pu == 1 ? ", 1 power-up" : $", {pu} power-ups";
            string wait = i > 0 ? $", +{SecondsBetween(steps[i - 1], steps[i], PreviewSpeed):0.00} s" : "";
            DrawRing(steps[i].z, $"Step {i + 1} ({steps[i].count}{extra}){wait}");
        }

        // Spine through the ring centers shows the pattern's depth at a glance.
        if (steps.Length > 1)
            Handles.DrawDottedLine(Center(steps[0].z), Center(steps[steps.Length - 1].z), 4f);

        // Magnets and Frost comets show their reach at the player's depth.
        foreach (Transform child in transform)
            DrawEffectRadius(child.GetComponent<AsteroidBehavior>(), transform.localPosition + child.localPosition);
    }

    private static Vector3 Center(float z) => new Vector3(OrbitCenter.x, OrbitCenter.y, z);

    private static void DrawRing(float z, string label)
    {
        Handles.DrawWireDisc(Center(z), Vector3.forward, OrbitRadius);
        Handles.Label(Center(z) + Vector3.up * OrbitRadius, label);
    }

    // Draws a Magnet's pull reach or a Frost comet's patch circle, at the player's depth, for an asteroid sitting at
    // `position` (spawner space). Does nothing for any other asteroid. Call it inside a DrawingScope in spawner space.
    public void DrawEffectRadius(AsteroidBehavior asteroid, Vector3 position)
    {
        if (!showEffectRadii || asteroid == null) return;

        if (asteroid.Effect == AsteroidBehavior.EffectType.Magnet)
        {
            Handles.color = magnetColor;
            Handles.DrawWireDisc(new Vector3(position.x, position.y, PlayerZ), Vector3.forward, asteroid.MagnetRadius);
        }
        else if (asteroid.Effect == AsteroidBehavior.EffectType.Frost)
        {
            Vector2 fromCenter = (Vector2)position - OrbitCenter;
            if (fromCenter.sqrMagnitude < 1e-6f) return;

            Vector2 onRing = OrbitCenter + fromCenter.normalized * OrbitRadius;
            Handles.color = frostColor;
            Handles.DrawWireDisc(new Vector3(onRing.x, onRing.y, PlayerZ), Vector3.forward, asteroid.PatchRadius);
        }
    }
#endif
}
