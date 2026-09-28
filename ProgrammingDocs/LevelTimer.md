# LevelTimer

## Purpose

Tracks how far through the level the player is, and shows it by moving the earth closer over time.
The faster the player flies, the sooner the earth arrives. When it arrives, the level is complete.

## Where it lives

- **Attached to:** the `Game Manager` GameObject in `Game.unity`, next to GameManager (which finds it
  there automatically).
- **Requires:** `GameManager.Instance` with `earth` and `earthCamera` both assigned. It doesn't check for
  them, so it throws an error if either is missing (see GameManager's gotchas).

## Inspector settings

| Field | Default | In the scene | What it changes |
| --- | --- | --- | --- |
| `levelTime` | 60 | 120 | Seconds the level takes if the player's `Speed` stays at 0 |
| `speedTimeFactor` | 0.1 | 0.1 | How much the player's `Speed` shortens the level (see below) |
| `stopDistance` | 20 | 76 | How far in front of the earth camera the earth stops, in world units |

With the scene's values, the level takes 120 seconds at speed 0 and 60 seconds at max speed (10).

## How it works

### `Progress` and `IsComplete`

`Progress` is a number from 0 (level just started) to 1 (earth has arrived). It's `private set`, so
only LevelTimer can change it, but any script can read it.

`IsComplete` is just `Progress >= 1`. Other scripts check it to know when the level is over.

### `Start`: working out the earth's path

The earth moves in a straight line from where it starts to a **stop point** just in front of the earth
camera.

1. Remember the earth's starting position (`origin`).
2. Get the direction from the earth to the earth camera: `toCamera = camera − origin`.
3. `.normalized` shrinks that to a length of exactly 1 unit, keeping its direction. Multiplying it by
   `stopDistance` gives an arrow `stopDistance` long, pointing from the earth toward the camera.
4. `stopPoint = camera − that arrow`. Start at the camera and step back toward the earth by
   `stopDistance`.

So the earth stops `stopDistance` units in front of the camera, never passing it. The earth camera
doesn't move, so this is only worked out once.

### `Update`: moving the earth

Once the level is complete, it does nothing. Otherwise, every frame:

**1. How long the level should take right now.**

`effectiveTime = levelTime / (1 + Speed × speedTimeFactor)`

The faster the player goes, the bigger the bottom of the fraction, so the shorter the level. With
`levelTime = 120` and `speedTimeFactor = 0.1`:

| Player `Speed` | Bottom of the fraction | `effectiveTime` |
| --- | --- | --- |
| 0 | 1 + 0 = 1 | 120s |
| 5 | 1 + 0.5 = 1.5 | 80s |
| 10 (max) | 1 + 1 = 2 | 60s |

**2. Add this frame's share of progress.**

`Progress += Time.deltaTime / effectiveTime`

If the level takes 120 seconds, each second adds 1/120 of the way. Capped at 1.

Like `distance` in AsteroidBehavior and `orbitAngle` in PlayerMovement, this is a running total built up
each frame. If it were calculated fresh as `total time / effectiveTime`, speeding up would make progress
suddenly jump forward and the earth would teleport. Adding a little each frame means a speed change only
changes how fast progress grows from then on.

**3. Place the earth.**

`earth.position = Vector3.Lerp(origin, stopPoint, Progress)`

**Lerp** (linear interpolation) finds the point a certain fraction of the way between two positions.
`Progress = 0` gives `origin`, `1` gives `stopPoint`, and `0.5` gives the point exactly halfway.

## Talks to

- **GameManager**: gets the earth and earth camera in `Start`.
- **PlayerMovement**: LevelTimer reads the static `Speed` every frame. PlayerMovement reads `IsComplete`
  to stop the player accelerating.
- **AsteroidSpawner**: reads `IsComplete` to stop spawning.

## Gotchas

- **Nothing happens when the level ends.** `IsComplete` only stops asteroids spawning and stops the player
  speeding up. There's no win screen or next level yet. Anything that should happen at the end needs to
  check `IsComplete` itself (there's no event to listen for).
- **No null checks.** A scene with LevelTimer but no GameManager, or with `earth` / `earthCamera` left
  empty, throws an error in `Start`. With no earth, `Update` also throws every frame. With no earth
  camera, the stop point is never set, so the earth drifts toward the world origin (0, 0, 0) instead.
- **`stopDistance` must be less than the earth's starting distance from the camera.** If it's larger, the
  stop point ends up behind where the earth starts, and the earth moves away from the camera instead.
- **The earth camera must stay put.** The stop point is worked out once in `Start`, so moving the camera
  afterward doesn't move where the earth stops.
