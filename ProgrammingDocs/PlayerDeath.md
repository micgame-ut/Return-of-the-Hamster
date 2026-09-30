# PlayerDeath

## Purpose

Plays the sequence that starts when the player loses all their lives: the ship moves in front of the
camera while shaking harder and harder, then explodes. When it's done, it tells GameManager, which is where
the game-over screen will start.

GameManager freezes everything tied to `Time.deltaTime` at game over. This script runs on unscaled time, so
it keeps going while the rest of the level stands still.

## Where it lives

- **Attached to:** the player object, alongside `PlayerMovement` and `PlayerHealth`.
- **Requires:**
  - `meshTransform` assigned to the ship's mesh child (the same one `PlayerMovement` uses).
  - `GameManager.Instance`, for `IsGameOver`, the gameplay camera, and `DeathSequenceFinished()`.
  - An explosion prefab whose particle system uses **Unscaled** delta time (`Player Explosion`, a copy of
    `Asteroid Explosion`), or it spawns frozen.

## Inspector settings

| Field | Default | What it changes |
| --- | --- | --- |
| `meshTransform` | none | The ship's visual child. It gets shaken, then hidden |
| `distanceFromCamera` | 3 | How far in front of the camera the ship stops. The ship's normal distance is about 7 |
| `holdTime` | 0.75 | Seconds the ship sits in front of the camera, shaking, before it explodes |
| `timeToExplode` | 2 | Seconds from game over to the explosion. The approach takes `timeToExplode − holdTime` |
| `maxShake` | 0.3 | How far the mesh jitters at the moment it explodes. It grows from 0 |
| `explosionPrefab` | none | The particle effect spawned at the explosion (`Player Explosion`) |
| `explodeShakeDuration` | 0.4 | How long the camera shakes at the explosion |
| `explodeShakeMagnitude` | 0.5 | How hard the camera shakes at the explosion |
| `endDelay` | 1.5 | Seconds after the explosion before `DeathSequenceFinished()` is called |

## How it works

With the defaults:

| Time | What happens |
| --- | --- |
| 0 s | Game over. Everything else freezes and the sequence starts |
| 0 → 1.25 s | The ship moves to the camera while shaking |
| 1.25 → 2 s | It holds in front of the camera, shaking harder |
| 2 s | Explosion and camera shake; the ship disappears |
| 3.5 s | `DeathSequenceFinished()` |

### `Update` and `started`

`Update` runs every frame, checking whether the game is over. On the first frame it is, it sets `started`
to true and starts the `DeathSequence` coroutine. On every frame after that, the first line exits the
method, so the sequence only starts once. Without `started`, a new coroutine would start every frame.

`Update` still runs while time is frozen. `Time.timeScale = 0` only makes `Time.deltaTime` 0.

### `DeathSequence`: setup

1. **Switch the renderers back on.** If the player was hit and blinking when the game froze, the blink
   could freeze with the ship hidden.
2. **`startPosition`** stores where the ship is at game over, the "from" point of the move.
3. **`target`** is the camera's position plus its forward direction × `distanceFromCamera`: a point
   straight ahead, in the middle of the screen. `cam.forward` is length 1, so the result is exactly
   `distanceFromCamera` units away.

### `DeathSequence`: the approach and shake loop

The loop counts `elapsed` up by real frame time (`Time.unscaledDeltaTime`) until it reaches
`timeToExplode`, pausing one frame each time with `yield return null` (which still works when time is
frozen). Each frame:

- **Move.** `moveT` is a fraction from 0 to 1 of how far through the approach the ship is:
  `elapsed / approachTime`, capped at 1. The root is lerped from `startPosition` to `target` by `moveT`.
  Because the approach is shorter than `timeToExplode`, `moveT` reaches 1 early and the ship holds there.
  If `holdTime` is `timeToExplode` or more, `approachTime` is 0 or less and `moveT` is set straight to 1
  instead of dividing by it (dividing by 0 would give `NaN` and break the position).
- **Shake.** The mesh child's last offset is removed, and a new random one is added. Its size is
  `maxShake × (elapsed / timeToExplode)`, so the shake grows as the explosion gets closer. Removing the
  old offset first keeps the jitter centered instead of wandering off (the same idea as CameraShake).

The root moves and the mesh child shakes, so the two never fight.

| Seconds in | `moveT` | Position | Shake |
| --- | --- | --- | --- |
| 0 | 0 | where it was | 0 |
| 0.625 | 0.5 | halfway | 0.09 |
| 1.25 | 1 | at the camera | 0.19 |
| 2 | 1 | holding | 0.3, then explode |

After the loop, the last offset is removed so the mesh is back at rest, and the ship is placed exactly on
`target` (the last frame can stop just short).

### `DeathSequence`: explosion and finish

1. Spawns `explosionPrefab` at the ship, if one is assigned.
2. Shakes the camera with `CameraShake.Shake`.
3. Hides the ship by switching off the **mesh child**. The root stays active, because switching off the
   object this coroutine runs on would stop the coroutine.
4. Waits `endDelay` with `WaitForSecondsRealtime`. A plain `WaitForSeconds` counts scaled time and would
   wait forever while frozen.
5. Calls `GameManager.Instance.DeathSequenceFinished()`, where the game-over screen will start.

## Talks to

- **GameManager**: reads `IsGameOver` every frame until the sequence starts, gets `GameplayCamera` for the
  target, and calls `DeathSequenceFinished()` at the end.
- **CameraShake**: calls `Shake` at the explosion.
- **PlayerMovement**: no direct link. PlayerMovement stops moving the ship at game over so this script
  can take over.

## Gotchas

- **Everything in the sequence must use unscaled time.** A `Time.deltaTime`, `WaitForSeconds`, or a
  particle system on Scaled time freezes along with the level.
- **`meshTransform` must be assigned.** It's used without a null check, so the sequence throws on its
  first line if it's empty.
- **Too small a `distanceFromCamera` clips the ship through the camera.** It has to be less than the ship's
  normal distance (about 7) for the ship to come closer.
- **`timeToExplode` of 0 skips straight to the explosion,** with no move or shake.
- **The shake is in the root's local space.** If the ship's root is scaled, `maxShake` is scaled with it.
- **The ship stays hidden.** Nothing brings the mesh back; a scene reload resets it.
