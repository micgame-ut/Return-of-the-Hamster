using System.Collections.Generic;
using UnityEngine;

// Editor-only fairness checks for a wave pattern: overlaps, steps with no safe lane, lanes too far apart in time,
// and power-ups the player can't get. Warnings only; nothing here changes the pattern.
public static class PatternFairness
{
    // What the checks assume about the player. The pattern builder fills this in from its Fairness settings.
    public struct Settings
    {
        public float playerRadius;       // the player's hitbox radius
        public float laneMargin;         // degrees of room a gap needs, on top of the bare minimum, to count as a lane
        public float playerAngularSpeed; // degrees per second the player is assumed to move around the orbit
        public float playerSpeed;        // the player Speed the time between steps is worked out at
    }

    // One problem found: what to tell you, and which objects it is about (so the Scene view can mark them).
    public struct Warning
    {
        public string message;
        public Transform[] objects;
    }

    // How far either side of an object's angle the player's centre is blocked, in degrees.
    // objectDistance is how far the object is from the orbit centre, and reach is its radius plus the player's.
    public static float BlockedHalfWidth(float objectDistance, float orbitRadius, float reach)
    {
        // An object at the very centre touches the orbit everywhere or nowhere.
        if (objectDistance <= 0.0001f) return reach >= orbitRadius ? 180f : 0f;

        // Law of cosines: a player a degrees around the orbit from the object is
        // d² = objectDistance² + orbitRadius² − 2 · objectDistance · orbitRadius · cos(a) away. It blocks while d < reach,
        // which is the same as cos(a) being bigger than c.
        float c = (objectDistance * objectDistance + orbitRadius * orbitRadius - reach * reach)
                  / (2f * objectDistance * orbitRadius);
        if (c >= 1f) return 0f;   // too far from the orbit to touch the player anywhere
        if (c <= -1f) return 180f; // reaches the whole orbit
        return Mathf.Acos(c) * Mathf.Rad2Deg;
    }

    // The circle is split into cells of CellDegrees each, so "is the player's centre blocked here?" is a true/false per cell.
    private const int CellCount = 720;
    private const float CellDegrees = 360f / CellCount; // 0.5 degrees

    // A stretch of the circle: where it starts (degrees, 0 to 360) and how long it is (degrees).
    public struct Arc
    {
        public float start;
        public float length;
    }

    // Marks the cells within halfWidth degrees of an angle as blocked, wrapping round the circle.
    private static void MarkArc(bool[] blocked, float centerAngle, float halfWidth)
    {
        if (halfWidth <= 0f) return;

        // 180 or more reaches the whole circle.
        if (halfWidth >= 180f)
        {
            for (int i = 0; i < CellCount; i++) blocked[i] = true;
            return;
        }

        int first = Mathf.FloorToInt((centerAngle - halfWidth) / CellDegrees);
        int last = Mathf.CeilToInt((centerAngle + halfWidth) / CellDegrees);
        for (int i = first; i <= last; i++)
            blocked[((i % CellCount) + CellCount) % CellCount] = true; // wrap, including negative cells
    }

    // The cells of the circle where the player's centre would hit an asteroid in this step.
    public static bool[] BlockedCells(PatternGizmos.Step step, Vector3 rootOffset, Settings settings)
    {
        var blocked = new bool[CellCount];
        Vector2 center = PatternGizmos.OrbitCenter;

        foreach (Transform child in step.children)
        {
            if (!IsObstacle(child)) continue;

            Vector2 offset = (Vector2)(rootOffset + child.localPosition) - center; // from the orbit center, in spawner space
            float angle = Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg;
            float reach = HitRadius(child) + settings.playerRadius;
            MarkArc(blocked, angle, BlockedHalfWidth(offset.magnitude, PatternGizmos.OrbitRadius, reach));
        }
        return blocked;
    }

    // Asteroids block the player. Power-ups don't, and Wave asteroids swing sideways, so they aren't checked.
    private static bool IsObstacle(Transform child)
    {
        AsteroidBehavior asteroid = child.GetComponent<AsteroidBehavior>();
        return asteroid != null && asteroid.Movement != AsteroidBehavior.MovementType.Wave;
    }

    // The object's hitbox radius in world units: its collider's biggest half-size on screen. 0 if it has no collider.
    private static float HitRadius(Transform child)
    {
        Collider collider = child.GetComponentInChildren<Collider>();
        return collider != null ? Mathf.Max(collider.bounds.extents.x, collider.bounds.extents.y) : 0f;
    }

    // The asteroids in a step that block the player (see IsObstacle).
    private static List<Transform> Obstacles(PatternGizmos.Step step)
    {
        var obstacles = new List<Transform>();
        foreach (Transform child in step.children)
            if (IsObstacle(child)) obstacles.Add(child);
        return obstacles;
    }

    // Adds a warning for every pair of asteroids in a step whose hitboxes overlap each other.
    private static void CheckOverlaps(PatternGizmos.Step step, int stepNumber, Vector3 rootOffset, List<Warning> warnings)
    {
        List<Transform> obstacles = Obstacles(step);
        for (int i = 0; i < obstacles.Count; i++)
        {
            for (int j = i + 1; j < obstacles.Count; j++)
            {
                float distance = Vector2.Distance(rootOffset + obstacles[i].localPosition, rootOffset + obstacles[j].localPosition);
                if (distance < HitRadius(obstacles[i]) + HitRadius(obstacles[j]))
                {
                    warnings.Add(new Warning
                    {
                        message = $"Step {stepNumber}: two asteroids overlap.",
                        objects = new[] { obstacles[i], obstacles[j] }
                    });
                }
            }
        }
    }

    // The free stretches of the circle that are at least minWidth degrees wide: the lanes the player could fly through.
    public static List<Arc> Lanes(bool[] blocked, float minWidth)
    {
        var lanes = new List<Arc>();

        // Start counting at a blocked cell, so no free stretch is cut in two by the end of the array.
        int anchor = System.Array.IndexOf(blocked, true);
        if (anchor < 0)
        {
            lanes.Add(new Arc { start = 0f, length = 360f }); // nothing blocks this step
            return lanes;
        }

        int run = 0; // free cells in a row
        for (int n = 1; n <= CellCount; n++)
        {
            int cell = (anchor + n) % CellCount;
            if (!blocked[cell])
            {
                run++;
                continue;
            }

            // A blocked cell ends the free stretch before it.
            float width = run * CellDegrees;
            if (run > 0 && width >= minWidth)
            {
                int firstCell = (cell - run + CellCount) % CellCount;
                lanes.Add(new Arc { start = firstCell * CellDegrees, length = width });
            }
            run = 0;
        }
        return lanes;
    }

    // How far the player's centre must move, in degrees, from an angle to the nearest point of a lane (0 if it's inside).
    private static float DistanceToLane(float angle, Arc lane)
    {
        float intoLane = Mathf.Repeat(angle - lane.start, 360f); // how far round from the lane's start the angle is
        if (intoLane <= lane.length) return 0f;

        float pastEnd = intoLane - lane.length; // going on from the lane's far end
        float beforeStart = 360f - intoLane;    // going back to the lane's start
        return Mathf.Min(pastEnd, beforeStart);
    }

    // The shortest move, in degrees, from anywhere in one lane to anywhere in another (0 if they overlap).
    private static float DistanceBetweenLanes(Arc a, Arc b)
    {
        return Mathf.Min(
            Mathf.Min(DistanceToLane(a.start, b), DistanceToLane(a.start + a.length, b)),
            Mathf.Min(DistanceToLane(b.start, a), DistanceToLane(b.start + b.length, a)));
    }

    // The shortest move from any lane in one list to any lane in another. Infinity if either list is empty.
    public static float DistanceBetweenLaneSets(List<Arc> from, List<Arc> to)
    {
        float best = float.PositiveInfinity;
        foreach (Arc a in from)
            foreach (Arc b in to)
                best = Mathf.Min(best, DistanceBetweenLanes(a, b));
        return best;
    }

    // Runs the fairness checks on a pattern and returns the problems found. An empty list means no problems.
    public static List<Warning> Check(PatternGizmos pattern, Settings settings)
    {
        var warnings = new List<Warning>();
        PatternGizmos.Step[] steps = pattern.GetSteps();
        Vector3 rootOffset = pattern.transform.localPosition;

        var lanes = new List<Arc>[steps.Length]; // each step's lanes, for the checks that compare steps
        for (int i = 0; i < steps.Length; i++)
        {
            CheckOverlaps(steps[i], i + 1, rootOffset, warnings);

            lanes[i] = Lanes(BlockedCells(steps[i], rootOffset, settings), settings.laneMargin);
            if (lanes[i].Count == 0)
            {
                warnings.Add(new Warning
                {
                    message = $"Step {i + 1}: no gap wide enough for the player.",
                    objects = Obstacles(steps[i]).ToArray()
                });
            }
        }

        List<Arc>[] reachable = CheckReach(steps, lanes, settings, warnings);
        CheckPowerUps(steps, reachable, rootOffset, settings, warnings);
        return warnings;
    }

    // Adds a warning wherever the player can't get from the gaps in one step to a gap in the next in time.
    private static List<Arc>[] CheckReach(PatternGizmos.Step[] steps, List<Arc>[] lanes, Settings settings, List<Warning> warnings)
    {
        var reachableByStep = new List<Arc>[steps.Length]; // the lanes the player could be in at each step
        if (steps.Length == 0) return reachableByStep;
        List<Arc> reachable = lanes[0]; // the first step: the player can already be in any of its gaps
        reachableByStep[0] = reachable;

        for (int i = 1; i < steps.Length; i++)
        {
            // No gap here (already warned), or nowhere to start from: begin again with this step's gaps.
            if (lanes[i].Count == 0 || reachable.Count == 0)
            {
                reachable = lanes[i];
                reachableByStep[i] = reachable;
                continue;
            }

            float seconds = PatternGizmos.SecondsBetween(steps[i - 1], steps[i], settings.playerSpeed);
            float allowed = settings.playerAngularSpeed * seconds;

            // Only the gaps the player could actually get to carry forward.
            var next = new List<Arc>();
            foreach (Arc lane in lanes[i])
                if (DistanceBetweenLaneSets(reachable, new List<Arc> { lane }) <= allowed) next.Add(lane);

            if (next.Count == 0)
            {
                float needed = DistanceBetweenLaneSets(reachable, lanes[i]);
                warnings.Add(new Warning
                {
                    message = $"Step {i} → {i + 1}: the nearest gap is {needed:0}° away, but only {allowed:0}° can be covered in {seconds:0.00} s.",
                    objects = Obstacles(steps[i]).ToArray()
                });
                reachable = lanes[i]; // carry on from here, so one problem isn't reported again at every later step
            }
            else
            {
                reachable = next;
            }
            reachableByStep[i] = reachable;
        }
        return reachableByStep;
    }

    // The cells where the player's centre would touch this power-up: the arc of angles around it that reaches it.
    private static bool[] TouchCells(Transform powerUp, Vector3 rootOffset, Settings settings)
    {
        var cells = new bool[CellCount];
        Vector2 offset = (Vector2)(rootOffset + powerUp.localPosition) - PatternGizmos.OrbitCenter;
        float angle = Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg;
        float reach = HitRadius(powerUp) + settings.playerRadius;
        MarkArc(cells, angle, BlockedHalfWidth(offset.magnitude, PatternGizmos.OrbitRadius, reach));
        return cells;
    }

    // The shortest move, in degrees, from any of the lanes to any marked cell. Infinity if there are no lanes or no marked cells.
    private static float DistanceToCells(List<Arc> lanes, bool[] cells)
    {
        float best = float.PositiveInfinity;
        for (int c = 0; c < CellCount; c++)
        {
            if (!cells[c]) continue;
            foreach (Arc lane in lanes)
                best = Mathf.Min(best, DistanceToLane(c * CellDegrees, lane));
        }
        return best;
    }

    // Adds a warning for every power-up the player can't collect: too far from the orbit, covered by asteroids,
    // or too far from where the player could be after the step before.
    private static void CheckPowerUps(PatternGizmos.Step[] steps, List<Arc>[] reachable, Vector3 rootOffset, Settings settings, List<Warning> warnings)
    {
        for (int i = 0; i < steps.Length; i++)
        {
            bool[] blocked = BlockedCells(steps[i], rootOffset, settings);

            foreach (Transform child in steps[i].children)
            {
                if (child.GetComponent<PowerUp>() == null) continue;

                // Where the player's centre touches it, then minus the cells that also hit an asteroid.
                bool[] touch = TouchCells(child, rootOffset, settings);
                if (System.Array.IndexOf(touch, true) < 0)
                {
                    warnings.Add(new Warning { message = $"Step {i + 1}: a power-up is too far from the orbit to collect.", objects = new[] { child } });
                    continue;
                }
                for (int c = 0; c < CellCount; c++)
                    touch[c] = touch[c] && !blocked[c];
                if (System.Array.IndexOf(touch, true) < 0)
                {
                    warnings.Add(new Warning { message = $"Step {i + 1}: a power-up is blocked by an asteroid.", objects = new[] { child } });
                    continue;
                }

                // Can the player get there from where they could be after the step before? (Not checked for step 1.)
                if (i == 0 || reachable[i - 1].Count == 0) continue;
                float seconds = PatternGizmos.SecondsBetween(steps[i - 1], steps[i], settings.playerSpeed);
                float allowed = settings.playerAngularSpeed * seconds;
                float needed = DistanceToCells(reachable[i - 1], touch);
                if (needed > allowed)
                {
                    warnings.Add(new Warning
                    {
                        message = $"Step {i + 1}: a power-up is {needed:0}° from the nearest gap before it, but only {allowed:0}° can be covered in {seconds:0.00} s.",
                        objects = new[] { child }
                    });
                }
            }
        }
    }
}
