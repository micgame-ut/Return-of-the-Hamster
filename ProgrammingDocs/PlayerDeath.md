# PlayerDeath

## Purpose

Plays the sequence that starts when the player loses all their lives: the whole ship (ball, hamster and ring)
moves in front of the camera, turning to face it, while shaking harder and harder, then explodes. The ring tips down
toward the camera on the way in, so the hamster inside can be seen facing it. When it's done, it tells GameManager, which is where
the game-over screen will start.

GameManager freezes everything tied to `Time.deltaTime` at game over. This script runs on unscaled time, so
it keeps going while the rest of the level stands still.

## Where it lives

- **Attached to:** the player object, alongside `PlayerMovement` and `PlayerHealth`.
- **Requires:**
  - `meshTransform` assigned to the ship's mesh child (the same one `PlayerMovement` uses).
  - `hamster` assigned to the `Apollo` object under the ring (optional). Without it, the hamster tilts with the
    ring during the death instead of staying facing the camera.
  - `GameManager.Instance`, for the `OnGameOver` event, the gameplay camera, and `DeathSequenceFinished()`.
  - An explosion prefab whose particle systems all use **Unscaled** time (**Use Unscaled Time** ticked), or it
    spawns frozen. Now `vfx_explosion`: all five of its systems (the four children and the timer on its root)
    have it on. (The older `Player Explosion` also did; the pooled asteroid explosion doesn't need it.)

## Inspector settings

| Field | Default | What it changes |
| --- | --- | --- |
| `meshTransform` | none | The ship's visual child. It is hidden at the explosion |
| `distanceFromCamera` | 3 | How far in front of the camera the ship stops. The ship's normal distance is about 7 |
| `holdTime` | 0.75 | Seconds the ship sits in front of the camera, shaking, before it explodes |
| `timeToExplode` | 2 | Seconds from game over to the explosion. The approach takes `timeToExplode − holdTime` |
| `maxShake` | 0.3 | How far the whole player (ball, hamster and ring) jitters at the moment it explodes. It grows from 0 |
| `shakeFrequency` | 60 (min 1) | New shake directions per second, so the shake looks the same at any frame rate |
| `ringTilt` | 25 | Degrees the ring tips down toward the camera as the ship arrives, so the hamster inside can be seen better. 0 = no tilt. A negative number tips it the other way |
| `hamster` | none | The hamster (`Apollo`, under the ring). When the death starts he's taken out from under the ring so he keeps facing the camera while the ring tilts. Empty = he tilts with the ring |
| `explosionPrefab` | none | The particle effect spawned at the explosion (`Player Explosion`) |
| `explodeShakeDuration` | 0.4 | How long the camera shakes at the explosion |
| `explodeShakeMagnitude` | 0.5 | How hard the camera shakes at the explosion |
| `endDelay` | 1.5 | Seconds after the explosion before `DeathSequenceFinished()` is called |

## How it works

With the defaults:

| Time | What happens |
| --- | --- |
| 0 s | Game over. Everything else freezes and the sequence starts |
| 0 → 1.25 s | The ship moves to the camera and turns to face it, shaking |
| 1.25 → 2 s | It holds in front of the camera, facing it, shaking harder |
| 2 s | Explosion and camera shake; the ship disappears |
| 3.5 s | `DeathSequenceFinished()` |

### `Awake`

Finds the player's **ring** with `GetComponentInChildren<BoostRing>(true)` (the `true` also finds it if it's
switched off). The ring is a separate child of the player, not under `meshTransform`, so hiding the mesh
alone would leave it floating where the ship exploded.

### `OnEnable` / `OnDisable` and `HandleGameOver`

There's no `Update`. Instead it subscribes to GameManager's `OnGameOver` event in `OnEnable` and
unsubscribes in `OnDisable`, so GameManager never holds on to a destroyed PlayerDeath. GameManager's
`Awake` runs first (`DefaultExecutionOrder(-100)`), so `Instance` is ready by `OnEnable`.

When the game ends, `HandleGameOver` starts the `DeathSequence` coroutine. `OnGameOver` fires at most once
per level, so the sequence can only start once. It starts in the same frame as the hit that cost the last
life, from inside `GameOver()`.

The coroutine keeps running while time is frozen. `Time.timeScale = 0` only makes `Time.deltaTime` 0, and
the sequence uses unscaled time.

### `DeathSequence`: setup

1. **Switch the renderers back on.** If the player was hit and blinking when the game froze, the blink
   could freeze with the ship hidden.
2. **`startPosition`** and **`startRotation`** store where the ship is, and how it's turned, at game over:
   the "from" points of the move and the turn.
3. **`target`** is the camera's position plus its forward direction × `distanceFromCamera`: a point
   straight ahead, in the middle of the screen. `cam.forward` is length 1, so the result is exactly
   `distanceFromCamera` units away.
4. **`faceCamera`** is the rotation that makes the ship face the camera:
   `Quaternion.LookRotation(-cam.forward, cam.up)` points the root's +Z axis back along the camera's view
   direction and keeps its +Y matching the camera's up, so it stays upright on screen. The hamster runs along the
   root's +Z, so this turns his front to the camera.
5. **Free the hamster from the ring.** If `hamster` is assigned, `SetParent(transform, true)` moves him from under
   the ring to directly under the player root, keeping his world position, rotation and size. This is needed
   because the ring is squashed to 5% height and the hamster has a matching 20× height scale to cancel it. That
   cancelling only works while nothing is rotated between them, so counter-rotating him under a tilting ring would
   shear him. Out from under it, the ring can tilt without moving him.
6. **Switch off `BoostRing`** (`ring.enabled = false`). If the death came mid-dodge-roll, it would keep setting the
   ring's rotation every frame and fight the tilt. This also stops the ring glow updating, which doesn't matter,
   since the ring is hidden at the explosion.
7. **`ringStart`** stores the ring's local rotation at game over, the starting point of the tilt.

### `DeathSequence`: the approach and shake loop

The loop counts `elapsed` up by real frame time (`Time.unscaledDeltaTime`) until it reaches
`timeToExplode`, pausing one frame each time with `yield return null` (which still works when time is
frozen). Each frame:

- **Arrival.** On the first frame `moveT` reaches 1, the **static** `OnArrived` event fires once (an `arrived`
  flag stops it repeating during the hold). GameSFX plays the game-over dialogue line from it. With the defaults
  that's 1.25 s after game over.
- **Move and turn.** `moveT` is a fraction from 0 to 1 of how far through the approach the ship is:
  `elapsed / approachTime`, capped at 1. The root's position is lerped from `startPosition` to `target` by
  `moveT`, and its rotation is slerped from `startRotation` to `faceCamera` by the same `moveT` (`Slerp` blends
  two rotations along the shortest arc at a steady turning speed). So the turn finishes exactly when the ship
  arrives, and during the hold it stays facing the camera.
  Because the approach is shorter than `timeToExplode`, `moveT` reaches 1 early and the ship holds there.
  If `holdTime` is `timeToExplode` or more, `approachTime` is 0 or less and `moveT` is set straight to 1
  instead of dividing by it (dividing by 0 would give `NaN` and break the position).
- **Ring tilt.** The ring's local rotation is set to `AngleAxis(ringTilt × moveT, Vector3.right) × ringStart`. The
  `AngleAxis` is on the left, so the tilt is about the **player root's** X axis (the ring's parent), on top of
  whatever angle the ring already had. A positive angle dips the ring's near edge (toward the camera) down, so the
  camera looks onto the ring's face instead of along its edge, and the hamster inside is clear. It uses the same
  `moveT` as the move and the turn, so it eases in over the approach and holds during the hold. With 25°: 0° at
  the start, 12.5° halfway, 25° on arrival.
- **Shake.** A `shakeOffset` of size `maxShake × (elapsed / timeToExplode)` is added to the root's position,
  so the shake grows as the explosion gets closer. The ball, shell, ring, hamster and flame are all under the
  root, so the whole player shakes as one. The position is rebuilt from scratch every frame (the lerp plus this
  frame's offset), so there's nothing to undo: last frame's offset disappears by itself.
  The **direction** is a new `Random.insideUnitSphere` only `shakeFrequency` times a second (local
  `directionTimer` and `shakeDirection`), held in between, the same as CameraShake. Picking a new one every
  frame made the shake buzzier on faster screens.

| Seconds in | `moveT` | Position | Shake |
| --- | --- | --- | --- |
| 0 | 0 | where it was | 0 |
| 0.625 | 0.5 | halfway | 0.09 |
| 1.25 | 1 | at the camera | 0.19 |
| 2 | 1 | holding | 0.3, then explode |

After the loop, the ship is placed exactly on `target` (the last frame can stop just short). The rotation
needs no such step: `moveT` is already 1 during the hold, so it has been exactly `faceCamera` since then.

### `DeathSequence`: explosion and finish

1. Spawns `explosionPrefab` at the ship, if one is assigned. This one isn't pooled (EffectPool): it only
   happens once per level.
2. Raises the **static** `OnExploded` event (GameSFX plays the explosion sound from it; it's static because
   GameManager doesn't expose PlayerDeath). Then shakes the camera with `CameraShake.Shake`.
3. Hides the ship by switching off the **mesh child**, the **ring** (if there is one; the exhaust flame is a
   child of the ring, so it goes too) and the **hamster** (if `hamster` is assigned; he's no longer under the ring,
   so he has to be switched off himself). The root stays active, because switching off the object this coroutine
   runs on would stop the coroutine. During the approach the ring and the hamster are under the root, so they move,
   turn and shake along with the ball.
4. Waits `endDelay` with `WaitForSecondsRealtime`. A plain `WaitForSeconds` counts scaled time and would
   wait forever while frozen.
5. Calls `GameManager.Instance.DeathSequenceFinished()`, where the game-over screen will start.

## Talks to

- **GameManager**: subscribes to `OnGameOver` to start the sequence, gets `GameplayCamera` for the
  target, and calls `DeathSequenceFinished()` at the end.
- **CameraShake**: calls `Shake` at the explosion.
- **GameSFX**: subscribes to the static `OnExploded` and `OnArrived`.
- **PlayerMovement**: no direct link. PlayerMovement stops moving the ship at game over so this script
  can take over.
- **BoostRing**: found in `Awake` to locate the ring object. The script is switched off when the death starts (so
  it can't overwrite the tilt) and the ring object is switched off at the explosion.

## Gotchas

- **Everything in the sequence must use unscaled time.** A `Time.deltaTime`, `WaitForSeconds`, or a
  particle system on Scaled time freezes along with the level. With a multi-system effect like `vfx_explosion`,
  **every** system needs **Use Unscaled Time**, including the timer on the root: one left on scaled time
  stays frozen, and a frozen timer never finishes.
- **`meshTransform` must be assigned.** It's used without a null check, so the sequence throws on its
  first line if it's empty.
- **Too small a `distanceFromCamera` clips the ship through the camera.** It has to be less than the ship's
  normal distance (about 7) for the ship to come closer.
- **`timeToExplode` of 0 skips straight to the explosion,** with no move or shake.
- **The shake is in world units.** It's added to the root's world position, so `maxShake` is in world units
  whatever the root's scale.
- **The turn assumes the hamster's front is the root's +Z axis.** If he arrives showing his back, `faceCamera`
  needs a 180° turn around Y added.
- **The ship stays hidden.** Nothing brings the mesh back; a scene reload resets it.
- **Assign `hamster` on the player prefab.** Drag the `Apollo` object (the ring's child that carries the hamster)
  onto the field in `hamter.prefab`. If it's left empty the death still works, but the hamster tilts with the ring.
- **Don't try to counter-rotate the hamster under the ring.** The ring's squash and his matching stretch only
  cancel while nothing is rotated between them, so he'd come out sheared. That's why he's moved out from under it.
- **The hamster is moved for good.** He stays under the player root after the death sequence starts. A scene
  reload (Restart or Menu) rebuilds the player, so nothing carries over.
- **A negative `ringTilt` tips the ring the other way.** If the ring dips *away* from the camera instead of toward
  it, flip the sign.
