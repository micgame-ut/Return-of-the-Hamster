using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

// Editor-only guides for authoring asteroid patterns. Draws the player's orbit
// circle at every Z step so you can see what the player can reach, and labels
// each step in the order AsteroidSpawner reveals them. Does nothing at runtime.
public class PatternGizmos : MonoBehaviour
{
    // Mirrors the Game scene, in spawner space: the orbit center is the camera's
    // x/y and the radius is PlayerMovement.circleRadius. Update if those change.
    public const float OrbitRadius = 3f;
    public static readonly Vector2 OrbitCenter = new Vector2(0f, 1f);
    private const float PlayerZ = -19f;         // player z (-4) minus spawner z (15)
    private const float SameZTolerance = 0.01f; // matches AsteroidSpawner

    [SerializeField] private Color ringColor = new Color(0.3f, 0.8f, 1f, 0.6f);
    [SerializeField] private Color playerRingColor = new Color(1f, 0.8f, 0.2f, 0.8f);

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        // Draw in spawner space, so the pattern root's own offset is respected
        // both in Prefab Mode (no parent) and in play (parented to the spawner).
        // The scope restores Handles' matrix and color for other gizmos afterwards.
        Matrix4x4 space = transform.parent != null ? transform.parent.localToWorldMatrix : Matrix4x4.identity;
        using var scope = new Handles.DrawingScope(space);
        Vector3 rootOffset = transform.localPosition;

        // Nearest the camera (lowest Z) first, same order as AsteroidSpawner.
        var zs = new List<float>();
        foreach (Transform child in transform)
            zs.Add(rootOffset.z + child.localPosition.z);
        zs.Sort();

        // Group equal Zs into one step, since they spawn together.
        var stepZ = new List<float>();
        var stepCount = new List<int>();
        for (int i = 0; i < zs.Count; i++)
        {
            if (i > 0 && zs[i] - zs[i - 1] <= SameZTolerance)
            {
                stepCount[stepCount.Count - 1]++;
            }
            else
            {
                stepZ.Add(zs[i]);
                stepCount.Add(1);
            }
        }

        Handles.color = playerRingColor;
        DrawRing(PlayerZ, "Player");

        Handles.color = ringColor;
        for (int i = 0; i < stepZ.Count; i++)
            DrawRing(stepZ[i], $"Step {i + 1} ({stepCount[i]})");

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
