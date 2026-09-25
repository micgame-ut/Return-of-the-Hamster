using System.Collections.Generic;
using UnityEngine;

// Draws the player's orbit on the player's own Z plane, plus a highlighted arc
// wherever an incoming asteroid will cross it. The camera's FOV shifts with
// speed, which makes depth hard to judge; these lines always sit at the
// hamster's depth, so they show exactly where on the circle is dangerous.
public class OrbitRing : MonoBehaviour
{
    [Header("Ring")]
    [SerializeField] private Material lineMaterial; // unlit/transparent; falls back to Sprites/Default
    [SerializeField] private Color ringColor = new Color(1f, 1f, 1f, 0.15f);
    [SerializeField] private float ringWidth = 0.05f;
    [SerializeField] private int ringSegments = 96;

    [Header("Danger Arcs")]
    [SerializeField] private Color dangerColor = new Color(1f, 0.2f, 0.2f, 1f);
    [SerializeField] private Color breakableColor = new Color(0.2f, 1f, 0.3f, 1f); // destructible and fast enough to break it
    [SerializeField] private float arcWidth = 0.15f;
    [SerializeField] private float arcOffset = 0.6f; // drawn this far outside the ring so arcs don't cover the hamster
    [SerializeField] private float warnTime = 1.5f; // seconds before impact the arc starts fading in
    [SerializeField] private float playerRadius = 0.5f; // added to the asteroid's radius; match the hamster's collider

    private PlayerMovement player;
    private LineRenderer ring;
    private readonly List<LineRenderer> arcs = new List<LineRenderer>();

    private void Start()
    {
        player = GameManager.Instance != null ? GameManager.Instance.Player : null;
        if (lineMaterial == null) lineMaterial = new Material(Shader.Find("Sprites/Default"));

        ring = CreateLine("Orbit Ring", ringWidth, 0);
        ring.loop = true;
        ring.startColor = ring.endColor = ringColor;
    }

    private void LateUpdate()
    {
        if (player == null) return;

        Vector2 center = player.OrbitCenter;
        float radius = player.CircleRadius;
        float z = player.transform.position.z;

        // Redrawn every frame so circleRadius can still be tuned live.
        SetArc(ring, center, radius, z, 0f, Mathf.PI * 2f, ringSegments, closed: true);

        int used = 0;
        foreach (AsteroidBehavior asteroid in AsteroidBehavior.Active)
        {
            if (!asteroid.IsMoving) continue;

            Vector3 pos = asteroid.transform.position;
            float reach = asteroid.HitRadius + playerRadius;

            // Hide once it has fully passed the player's plane.
            float dz = pos.z - z;
            if (dz < -reach) continue;

            // Fade by time to impact, not distance, so warnings feel the
            // same no matter how fast the player is flying.
            float timeToImpact = asteroid.ApproachSpeed > 0f ? Mathf.Max(dz, 0f) / asteroid.ApproachSpeed : Mathf.Infinity;
            float alpha = 1f - Mathf.Clamp01(timeToImpact / warnTime);
            if (alpha <= 0f) continue;

            if (!TryGetArc((Vector2)pos - center, radius, reach, out float midAngle, out float halfAngle)) continue;

            // Arcs are pooled and handed out in order each frame, not tied to one asteroid.
            if (used == arcs.Count) arcs.Add(CreateLine("Danger Arc", arcWidth, 1));
            LineRenderer arc = arcs[used++];
            arc.enabled = true;

            Color c = asteroid.CanBreak ? breakableColor : dangerColor;
            c.a *= alpha;
            arc.startColor = arc.endColor = c;

            int segments = Mathf.Max(2, Mathf.CeilToInt(ringSegments * halfAngle / Mathf.PI));
            // Angles come from the real orbit; only the drawn radius is pushed outward.
            SetArc(arc, center, radius + arcOffset, z, midAngle - halfAngle, midAngle + halfAngle, segments, closed: false);
        }

        // Hide leftover pooled arcs instead of destroying them, so waves don't churn garbage.
        for (int i = used; i < arcs.Count; i++)
            arcs[i].enabled = false;
    }

    // Finds the part of the orbit circle that lies within `reach` of the
    // asteroid's XY position (relative to the orbit center).
    private static bool TryGetArc(Vector2 offset, float radius, float reach, out float midAngle, out float halfAngle)
    {
        float d = offset.magnitude;
        midAngle = Mathf.Atan2(offset.y, offset.x);
        halfAngle = 0f;

        // Too far inside or outside the ring to touch it.
        if (Mathf.Abs(d - radius) >= reach) return false;

        // Big enough, or central enough, to cover the whole ring.
        if (d + radius <= reach || d < 0.0001f)
        {
            halfAngle = Mathf.PI;
            return true;
        }

        // Law of cosines: angle between the asteroid and the points where its
        // reach circle crosses the orbit.
        float cos = (d * d + radius * radius - reach * reach) / (2f * d * radius);
        halfAngle = Mathf.Acos(Mathf.Clamp(cos, -1f, 1f));
        return true;
    }

    private static void SetArc(LineRenderer line, Vector2 center, float radius, float z,
                               float fromAngle, float toAngle, int segments, bool closed)
    {
        // A closed loop repeats its first point itself, so it needs one fewer.
        int count = closed ? segments : segments + 1;
        line.positionCount = count;

        float step = (toAngle - fromAngle) / segments;
        for (int i = 0; i < count; i++)
        {
            float a = fromAngle + step * i;
            line.SetPosition(i, new Vector3(center.x + Mathf.Cos(a) * radius, center.y + Mathf.Sin(a) * radius, z));
        }
    }

    private LineRenderer CreateLine(string name, float width, int sortingOrder)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);

        var line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.sharedMaterial = lineMaterial;
        line.widthMultiplier = width;
        line.numCapVertices = 4;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.sortingOrder = sortingOrder; // arcs draw over the ring
        return line;
    }
}
