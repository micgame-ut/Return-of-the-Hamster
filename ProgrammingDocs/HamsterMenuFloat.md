# HamsterMenuFloat

## Purpose

Makes the hamster (the whole player) drift around on the Level 1 menu like a ball floating in a box: he travels in all
three directions, turns around slowly at the walls, and tumbles as he goes. A small extra wobble and a swelling
sideways speed keep the path from repeating.

## Where it lives

- **Attached to:** the player object, which also has `PlayerMovement`.
- **Requires:** a camera tagged `MainCamera` (`Camera.main`), used to find the screen edges.
- **Switched off by:** `Start` if the game has already started (`GameManager.HasStarted`), and by `MenuTransition`
  when the player presses start. Both are so it can't fight `PlayerMovement` over the position.

## Inspector settings

| Field | Default | What it changes |
| --- | --- | --- |
| `rotationSpeed` | 20 | Degrees per second of the base spin. The x axis uses 0.7× this and z uses 0.45× |
| `boxHalfHeight` | 1.5 | How far above and below the start point he drifts before turning around, in world units |
| `boxHalfDepth` | 2 | How far toward and away from the camera he drifts before turning around, in world units |
| `xMargin` | 2.5 | How far inside the screen's left and right edges the x walls are, in world units. Raise it if he flies off the sides |
| `ySpeed` | 1.3 | How fast he drifts up and down, in units per second |
| `zSpeed` | 0.9 | How fast he drifts toward and away from the camera, in units per second |
| `turnEase` | 3 | How quickly his speed changes when he turns around (units per second squared). Low = slow, floaty turns that overshoot the wall. High = a hard bounce |
| `wobbleAmount` | 0.25 | Size of the small bob added to the height and depth. 0 = off |
| `wobbleSpeed` | 2.3 | How fast that bob cycles (radians per second) |
| `tumbleSway` | 25 | Degrees of back-and-forth lean added to the spin. 0 = just the spin |
| `tumbleSwaySpeed` | 1.3 | How fast the lean cycles (radians per second) |
| `xSpeed` | 2 | Average sideways speed, in units per second. He turns around at the screen edges |
| `speedVariation` | 0.5 (0–1) | How much the sideways speed swells and eases, as a fraction of `xSpeed`. 0 = constant |
| `speedVariationRate` | 0.7 | How fast the sideways speed swells and eases (radians per second) |

## How it works

### `Start`

If the game has already started, the script switches itself off and stops. Otherwise it stores `startPosition`, the
point the box is centred on.

### `Update`

Every frame:

1. **Time.** `elapsedTime` and `rotationAngle` are running totals, counted up by `Time.deltaTime`, so they only ever
   grow and nothing jumps.
2. **Wobble.** `yWobble = sin(t × wobbleSpeed) × wobbleAmount` and `zWobble` the same with a **cosine**, which is a
   quarter cycle behind, so height and depth wobble out of step. These are added to the position at the end.
3. **Walls.** The camera's viewport edges are converted to world X at the hamster's depth with
   `ViewportToWorldPoint`. Each axis keeps a heading in `driftDirection` (+1 or −1). When he's past a wall the
   heading flips: past a screen edge pulled in by `xMargin` for x (room for the overshoot and his size), past `boxHalfHeight` for y, past `boxHalfDepth` for z. y and z are
   measured from `startPosition` (they use `driftOffset`).
4. **Sideways speed.** `currentSpeed = xSpeed × (1 + sin(t × speedVariationRate) × speedVariation)`, so x moves
   between 1 and 3 units per second with the defaults. y and z use their own constant speeds.
5. **Floaty turns.** `targetVelocity` is each axis's speed in its heading. The real `driftVelocity` doesn't jump to it:
   `Vector3.MoveTowards` moves it by at most `turnEase × deltaTime` per frame. After a flip the velocity slows,
   passes through zero and speeds up the other way, so he overshoots the wall a little and hangs before returning.
   `driftOffset` is a running total of `driftVelocity × deltaTime`.

   Worked example, y axis with the defaults: he's moving up at +1.3 and crosses +1.5, so the heading flips to −1
   (target −1.3). The velocity falls by 3 per second, so it takes about 0.87 s to reach 0, during which he rises
   roughly 0.56 more. He peaks near 2.06, then falls back.
6. **Position** = `startPosition` + `driftOffset` + (0, `yWobble`, `zWobble`).
7. **Rotation.** `sway = sin(t × tumbleSwaySpeed) × tumbleSway`. The rotation is
   `Euler(angle × 0.7 + sway, angle, angle × 0.45 − sway)`. The three axes spin at different paces and the sway
   leans him back and forth on top, so it doesn't look like one flat spin.

### `OnDrawGizmosSelected`

Draws the drift box as a yellow wire cube while the hamster is selected, so the walls can be seen. Its height and depth
are 2 × `boxHalfHeight` and 2 × `boxHalfDepth`. Its width is the screen's left-to-right distance at his depth minus
2 × `xMargin` (x turns at the screen edges pulled in, not at a fixed distance). It's centred on the screen's middle in x and on `startPosition`
in y and z (or on his current position when not playing). He overshoots the walls a little, so he'll slightly leave
the box.

## Talks to

- **GameManager**: reads `HasStarted` in `Start`.
- **MenuTransition**: switches this script off when start is pressed.
- **HamsterAnimationDriver**: no direct link. The Float animation only poses the limbs; this script moves and turns
  the whole body.

## Gotchas

- **The box is fixed in world units.** y and z don't follow the screen. Check the box clears the menu text, and
  that the camera doesn't end up inside him at the near wall (`boxHalfDepth`).
- **The walls are overshot on purpose.** With a low `turnEase` he goes past the wall before coming back, by
  roughly `speed² / (2 × turnEase)`. Shrink the box or raise `turnEase` if he strays too far.
- **`speedVariation` is capped at 1.** Above that the x target speed would go negative and he'd head the wrong way.
- **The x edges are measured at his current depth.** As he moves toward the camera the screen edges move inward,
  so the x turnaround point shifts a little with his depth.
- **`Camera.main` is called every frame.** Fine for one menu object. Cache it if this ever runs on many objects.
- **The old sine bob is gone.** `floatHeight`, `floatSpeed`, `depthDistance` and `depthSpeed` were deleted, so any
  values set for them in the scene or prefab are dropped.
