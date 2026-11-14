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

`IsComplete` is just `Progress >= 1`. Other scripts check it to know when the earth has arrived.

`LevelTime` is a read-only property that hands back `levelTime`, so other scripts can read the setting
without being able to change it. Score uses it to work out the time score.

`StartProgress` is a `public static float` set by `EndScreen.Continue` to the progress the player died at. `Awake` copies it
into `Progress` and sets it back to 0, so it is read once: a later Restart or Menu starts from 0. `Update` then moves the
earth to `Lerp(origin, stopPoint, Progress)` as usual, so the earth appears at the right distance. Dying after the earth
has arrived (`Progress` 1) and continuing makes the level complete at once.

`SetLevelTime(seconds)` is the one way to change `levelTime` from another script. `GameManager` calls it in
`Awake` when the level has a `LevelConfig`, so the config's time replaces the Inspector value.

### `RecordOrigin`: working out the earth's path

The earth moves in a straight line from where it starts to a **stop point** just in front of the earth
camera. `RecordOrigin` works that line out, and runs once, on the **first frame of play** (called from
`Update` while `hasOrigin` is false).

It used to be `Start`. It moved because on Level 1 the menu can move the earth (MenuTransition's far planet
glides from its menu spot to its gameplay spot). `Start` would have recorded the menu spot, and the earth would
approach diagonally from there. On the first frame of play the earth is always at its gameplay spot: the glide
has finished, or on a restart MenuTransition snapped it there in `Awake`. On levels without a menu, the first
frame of play is the first frame after loading, so nothing changes there.

1. Remember the earth's starting position (`origin`).
2. Get the direction from the earth to the earth camera: `toCamera = camera − origin`.
3. `.normalized` shrinks that to a length of exactly 1 unit, keeping its direction. Multiplying it by
   `stopDistance` gives an arrow `stopDistance` long, pointing from the earth toward the camera.
4. `stopPoint = camera − that arrow`. Start at the camera and step back toward the earth by
   `stopDistance`.

So the earth stops `stopDistance` units in front of the camera, never passing it. The earth camera
doesn't move, so this is only worked out once.

### `Update`: moving the earth

Before the game starts (GameManager's `HasStarted` is false), once the level is complete, or once
GameManager's `IsGameOver` is set, it does nothing. So during the menu the earth hasn't started its approach
(`Progress` stays 0, which also holds back Score's time score), and at game over the earth stays where it is.
On the first frame of play it calls `RecordOrigin`. Then, every frame:

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

LevelTimer doesn't decide the win itself. The earth arriving (`IsComplete`) is one half of GameManager's
`LevelCleared`; the other half is that nothing spawned is still flying.

## Talks to

- **GameManager**: gets the earth and earth camera in `RecordOrigin`, and reads `HasStarted` and
  `IsGameOver` every frame. GameManager reads `IsComplete` as part of deciding the win, and calls
  `SetLevelTime` once in `Awake` when the level has a `LevelConfig`.
- **MenuTransition**: no direct link, but it moves the earth into place before the first frame of play, which
  is why the origin is recorded then.
- **PlayerMovement**: LevelTimer reads the static `Speed` every frame. PlayerMovement reads `IsComplete`
  to stop the player accelerating.
- **ObjectSpawner**: reads `IsComplete` to stop spawning.
- **Score**: reads `Progress` and `LevelTime` every frame for the time score.

## Gotchas

- **The earth arriving isn't the win.** `IsComplete` stops spawning and stops the player speeding up, but
  the level is only won once the field is clear too (GameManager's `LevelCleared`). Code that should run
  on the win should check `HasWon`, not `IsComplete`.
- **No null checks.** With no GameManager, `Update` throws every frame when it reads `HasStarted`. With
  `earth` / `earthCamera` left empty, `RecordOrigin` throws on the first frame of play, and every frame after,
  since `hasOrigin` never gets set.
- **Don't move the earth after the first frame of play** (other than through LevelTimer). The origin is only
  recorded once.
- **`stopDistance` must be less than the earth's starting distance from the camera.** If it's larger, the
  stop point ends up behind where the earth starts, and the earth moves away from the camera instead.
- **The earth camera must stay put.** The stop point is worked out once in `RecordOrigin`, so moving the camera
  afterward doesn't move where the earth stops.
