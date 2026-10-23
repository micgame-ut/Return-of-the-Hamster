# PatternFairness

## Purpose

Rules whether there is enough room for the player to get through a wave pattern. A pattern is unfair if:

- a step has **no gap wide enough** for the player,
- the open gaps **can't be reliably moved between** (the next step's gap is too far from the last one for the player
  to reach in the time between the steps), or
- objects are in each other's way: two asteroids sit on top of each other, or a power-up is **covered by an
  asteroid** so it can't be collected without being hit.

It only gives warnings. It never changes the pattern. The pattern builder (PatternGizmosEditor) runs it and shows
the result.

It is an editor tool only: a static class in the `Editor` folder, so it isn't part of the game or any build.

## Where it lives

- **File:** `Assets/Scripts/Editor/PatternFairness.cs`
- **Attached to:** nothing. It is a static class, called by name from PatternGizmosEditor.
- **Requires:**
  - A pattern (a `PatternGizmos` root with its objects as direct children).
  - The `OrbitSettings` asset (through PatternGizmos) for the orbit's radius and centre.
  - The `SpawnableTemplate` in `Assets/Resources/` (through PatternGizmos) for the time between steps.

## Inspector settings

None of its own. The builder's **Fairness** section holds the settings and passes them in as a `Settings` struct:

| Field | Default | What it changes |
| --- | --- | --- |
| `playerRadius` | 0.3 | The player's hitbox radius. 0.3 is the shield's collider, the biggest, so it is the worst case |
| `laneMargin` | 5 (degrees) | How wide a free stretch must be before it counts as a lane (room to wiggle in) |
| `playerAngularSpeed` | 90 (degrees per second) | How fast the player is assumed to move around the orbit. Sets how far they can get between two steps |
| `playerSpeed` | the Timing slider's Speed | The player `Speed` the time between steps is worked out at |

Passing them in as a struct means the checks get a **copy**: they can't change the builder's values, and they don't
depend on the builder's code, so they could be run from anywhere.

## How it works

### The picture

The player is a bead on a circular wire, the orbit. Angles go round it: 0° is the right, 90° the top, -90° the
bottom where the player starts. Each **step** of a pattern is a group of objects that reach the player together.
For one step, the question is: *where on the wire can the bead's centre sit without being hit?*

- Each asteroid is a no-go patch. The player's centre can't be within about 10° of an asteroid (its size plus
  the player's size). An asteroid at 0° blocks -10° to 10°.
- What is left between the no-go patches are free stretches. A free stretch at least `laneMargin` wide is a
  **lane**.

Example: asteroids at 0° and 30°, each blocking 10° either side, block -10° to 10° and 20° to 40°. The free gap
between them is 10° wide. With a margin of 5° it is a lane; with a margin of 15° it is not.

Because the blocked patches already include the player's size, a lane is where the player's **centre** can sit.

There are four layers of code, from the objects up to the warnings.

### Layer 1: turning objects into blocked angles

The circle is stored as 720 true/false **cells**, 0.5° each (`CellCount`, `CellDegrees`). A "true" cell means the
player's centre would be hit there.

- **`IsObstacle(child)`** says whether an object blocks the player: asteroids do. Power-ups don't, and Wave
  asteroids are skipped because they swing sideways. It reads `AsteroidBehavior.Movement`.
- **`Obstacles(step)`** lists the blocking asteroids in a step.
- **`HitRadius(child)`** is the object's size: its collider's biggest half-size on screen (`bounds.extents`),
  in world units with scale included. 0 with no collider.
- **`BlockedHalfWidth(distance, orbitRadius, reach)`** says how many degrees either side of an object's angle the
  player's centre is blocked. `reach` is the object's radius plus the player's. It uses the law of cosines on
  the triangle (orbit centre, object, player). Three cases: an object far from the orbit blocks **0**; one that
  reaches the whole orbit blocks **180**; otherwise `Acos` of a ratio gives the angle. An asteroid on the orbit
  (radius 3) with reach 0.5 blocks about 9.6° each side.
- **`MarkArc(cells, angle, halfWidth)`** marks the cells from `angle - halfWidth` to `angle + halfWidth` as
  blocked, wrapping round 360°. The index `((i % 720) + 720) % 720` is there because `%` keeps the sign of its
  left side in C#, so a negative cell number needs 720 added first.
- **`BlockedCells(step, rootOffset, settings)`** puts it together: for each blocking asteroid in the step it
  finds the angle and distance from the orbit centre (`Atan2`, from the object's position in spawner space,
  which is the pattern root's offset plus the child's), asks `BlockedHalfWidth`, and calls `MarkArc`. It returns
  the 720 cells.

### Layer 2: lanes (`Lanes`)

`Lanes(cells, minWidth)` returns the free stretches at least `minWidth` wide, each as an `Arc` (a start angle and
a length). It starts counting at a blocked cell, so that a stretch crossing the 359.5° → 0° boundary isn't cut in
two (0° and 360° are the same point on the circle). It then counts free cells in a row, and each blocked cell ends
a run. If nothing is blocked, the whole circle is one lane of 360°.

Example: blocked 0°-100° and 110°-360°, margin 5° → one lane, 10° wide, from 100° to 110°.

### Layer 3: distances

How far the player has to move between lanes, or to a power-up:

- **`DistanceToLane(angle, lane)`**: from an angle to the nearest edge of a lane, 0 if it is inside. It works
  out how far round the angle is from the lane's start (`Mathf.Repeat`), then takes the shorter way: on past the
  far end, or back to the start. A lane from 100° to 120° and an angle of 90°: 350 round, so past the end is 330,
  back to the start is 10, so the answer is 10.
- **`DistanceBetweenLanes(a, b)`**: the shortest move between two lanes, found by checking all four ends. 0 if
  they overlap. Lanes 10°-80° and 100°-170° are 20° apart; 10°-80° and 60°-120° overlap, so 0.
- **`DistanceBetweenLaneSets(from, to)`**: the shortest move from any lane in one list to any lane in the other
  (the player's best case). Infinity if either list is empty.
- **`DistanceToCells(lanes, cells)`**: the shortest move from any of the lanes to any marked cell. Used for
  power-ups.

### Layer 4: the checks

- **`Check(pattern, settings)`** is the entry point. It gets the steps from `PatternGizmos.GetSteps()`, goes
  through them with the same step numbers as the Scene labels, and returns a list of `Warning`s (a message and the
  objects it is about). For each step it runs `CheckOverlaps`, finds its lanes, and warns "no gap wide enough"
  if there are none. Then it runs `CheckReach` and `CheckPowerUps`.
- **`CheckOverlaps`**: for every pair of blocking asteroids in a step (the second loop starts at `i + 1`, so each
  pair is compared once and never with itself), warns if the distance between their centres is less than their
  radii added together.
- **`CheckReach`**: step by step, works out which lanes the player could actually be in. Step 1: any lane. For
  each later step, the player can move `playerAngularSpeed × seconds` degrees, where `seconds` is the wait between
  the two steps at `playerSpeed` (`PatternGizmos.SecondsBetween`). A lane is reachable if it is within that distance
  of a lane they could be in. Only the reachable lanes carry forward, because a lane the player couldn't get to
  was never a place they could start from. If no lane is reachable, it warns with the shortest distance needed and
  what can be covered, then carries on from all that step's lanes, so one problem isn't repeated at every step
  after it. It returns the reachable lanes of every step (`reachableByStep`).
- **`TouchCells(powerUp, ...)`**: the cells where the player's centre touches a power-up. It is the same arc
  maths as for an asteroid, but with the meaning "collects it" instead of "is hit by it".
- **`CheckPowerUps`**: for each power-up, three problems, checked in order:
  1. Its touching cells are empty: **too far from the orbit** to collect.
  2. Its touching cells, minus the cells blocked by asteroids in the same step, are empty: **blocked by an
     asteroid**. Example: collectable from 80°-100° with an asteroid blocking 70°-105° is blocked; with an
     asteroid blocking only 85°-95° it isn't.
  3. The shortest move from the lanes reachable at the step before to the nearest free touching cell is more than
     the player can cover in the time between the steps: **too far to reach**. Not checked for step 1.

## Talks to

- **PatternGizmos**: calls `GetSteps()` (the objects grouped into steps, each with its children),
  `SecondsBetween` for the time between steps, and reads `OrbitRadius` and `OrbitCenter`.
- **AsteroidBehavior**: reads `Movement` (to skip Wave asteroids) and finds asteroids with `GetComponent`.
- **PowerUp**: finds power-ups with `GetComponent`.
- **PatternGizmosEditor**: calls `Check` with a `Settings` it builds from the Fairness section every time the
  Inspector draws, shows the warning messages, and draws red rings on `Warning.objects` in the Scene view.

## Gotchas

- **It checks the children already in the prefab, not the Add preview.** Place the objects, then read the warnings.
- **Wave asteroids aren't checked.** They swing sideways by `amplitude`, so they block a whole stretch of the
  orbit. For now they're skipped.
- **Destructible and Phase asteroids count as obstacles,** even though a fast player can smash a destructible
  one and a phased one is harmless for a moment. That is the worst case.
- **The lane width is the player's centre room, not the visible gap.** The blocked patches already include the
  player's size.
- **All the numbers are assumptions.** The player's radius, move speed and margin are settings to tune by feel.
  The reach check assumes a steady `Speed`, because the time between steps changes if the player speeds up or slows
  down in flight.
- **Resolution is 0.5°.** Blocked arcs are rounded outward to whole cells, so a lane can come out up to one cell
  (0.5°) narrower than the exact maths, which errs on the side of warning.
- **Lanes wrap round the circle.** A lane crossing 0° is one lane, not two, because the cells wrap properly.
- **Step 1 is never checked for reach,** and neither is its power-ups: the player sees the first step coming for
  the whole time it takes to arrive.
