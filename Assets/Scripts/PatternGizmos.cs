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

    [SerializeField] private Color ringColor = new Color(0.3f, 0.8f, 1f, 0.6f);
    [SerializeField] private Color playerRingColor = new Color(1f, 0.8f, 0.2f, 0.8f);

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        // Only for building patterns: skipped in Play, where pooled copies would redraw every frame.
        if (Application.isPlaying) return;

        // Draw in spawner space, so the pattern root's own offset is respected
        // both in Prefab Mode (no parent) and when the pattern sits under the spawner in a scene.
        // The scope restores Handles' matrix and color for other gizmos afterwards.
        Matrix4x4 space = transform.parent != null ? transform.parent.localToWorldMatrix : Matrix4x4.identity;
        using var scope = new Handles.DrawingScope(space);
        Vector3 rootOffset = transform.localPosition;

        // Nearest the camera (lowest Z) first, same order as ObjectSpawner.
        var items = new List<(float z, bool powerUp)>();
        foreach (Transform child in transform)
            items.Add((rootOffset.z + child.localPosition.z, child.GetComponent<PowerUp>() != null));
        items.Sort((a, b) => a.z.CompareTo(b.z));

        // Group equal Zs into one step, since they spawn together.
        var stepZ = new List<float>();
        var stepCount = new List<int>();
        var stepPowerUps = new List<int>();
        for (int i = 0; i < items.Count; i++)
        {
            int powerUp = items[i].powerUp ? 1 : 0;
            if (i > 0 && items[i].z - items[i - 1].z <= ObjectSpawner.SameZTolerance)
            {
                stepCount[stepCount.Count - 1]++;
                stepPowerUps[stepPowerUps.Count - 1] += powerUp;
            }
            else
            {
                stepZ.Add(items[i].z);
                stepCount.Add(1);
                stepPowerUps.Add(powerUp);
            }
        }

        Handles.color = playerRingColor;
        DrawRing(PlayerZ, "Player");

        Handles.color = ringColor;
        for (int i = 0; i < stepZ.Count; i++)
        {
            int pu = stepPowerUps[i];
            string extra = pu == 0 ? "" : pu == 1 ? ", 1 power-up" : $", {pu} power-ups";
            DrawRing(stepZ[i], $"Step {i + 1} ({stepCount[i]}{extra})");
        }

        // Spine through the ring centers shows the pattern's depth at a glance.
        if (stepZ.Count > 1)
            Handles.DrawDottedLine(Center(stepZ[0]), Center(stepZ[stepZ.Count - 1]), 4f);
    }

    private static Vector3 Center(float z) => new Vector3(OrbitCenter.x, OrbitCenter.y, z);

    private static void DrawRing(float z, string label)
    {
        Handles.DrawWireDisc(Center(z), Vector3.forward, OrbitRadius);
        Handles.Label(Center(z) + Vector3.up * OrbitRadius, label);
    }
#endif
}
