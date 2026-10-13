# CameraShake

## Purpose

Makes the camera jitter briefly for impact: when the player smashes an asteroid, gets hit by one, or
explodes at game over.
Any script can trigger it with one line: `CameraShake.Shake(duration, magnitude)`.

## Where it lives

- **Attached to:** the `Camera Shake` GameObject in `Game.unity`. It's the parent of both `Main Camera`
  and `Earth Camera`, so shaking it shakes both cameras together.
- **Also on:** `Main Camera` itself. Only one of the two ever works (see Gotchas).
- **Requires:** nothing. It works on whatever GameObject it's attached to.

## Inspector settings

| Field | Default | What it changes |
| --- | --- | --- |
| `shakeFrequency` | 60 (min 1) | New shake directions per second. 60 matches how the shake looked at 60 fps. Lower = a slower, rougher rumble |

Every shake's strength and length come from whoever calls `Shake`. For asteroids, those values are in the
SpawnableTemplate asset:

| Event | Duration | Magnitude |
| --- | --- | --- |
| Smashing an asteroid | `shakeDuration` (0.2s) | `shakeMagnitude` (0.3) |
| Getting hit by an asteroid | `hitShakeDuration` (0.15s) | `hitShakeMagnitude` (0.2) |
| The player exploding (PlayerDeath) | `explodeShakeDuration` (0.4s) | `explodeShakeMagnitude` (0.5) |

## How it works

### Calling it from anywhere

`CameraShake` keeps a **static** `instance` pointing to itself, set in `Awake` and cleared in `OnDestroy`,
the same idea as GameManager. The difference is that `instance` is `private`. Other scripts can't reach
the component directly. All they can do is call the static `Shake` method, which passes the request on
to the instance.

If there's no CameraShake in the scene, `Shake` does nothing, so callers never need to check.

### `Shake(duration, magnitude)`

Starts a shake, or updates the one already running:

- **`duration`** and **`timeLeft`** are both set to the new duration. The timer restarts.
- **`magnitude`** keeps whichever is **bigger**, the current one or the new one. That way a small hit
  during a big smash doesn't make the shake suddenly weaker.

### `LateUpdate`: applying the shake

**LateUpdate** runs every frame, after every script's `Update` has finished. That makes it the place
for camera adjustments, since anything that moves the camera in `Update` has already done so.

Every frame:

1. **Undo last frame's shake.** `localPosition -= lastOffset`. The camera is back where it would be
   with no shake.
2. **If no shake is running, stop here.**
3. **Count down.** `timeLeft -= Time.unscaledDeltaTime`. That's real time, which ignores
   `Time.timeScale`. GameManager freezes time at game over, and the shake still has to play and end
   then. During normal play, time scale is 1, so this is the same as `Time.deltaTime`.
4. **Work out how strong it is now.** `strength = magnitude × (timeLeft / duration)`. `timeLeft / duration`
   goes from 1 down to 0 as the shake runs out, so the shake starts at full strength and fades to
   nothing instead of stopping suddenly.
5. **Pick a direction, at a fixed rate.** `directionTimer` counts down in real time. When it reaches 0,
   `shakeDirection = Random.insideUnitSphere` (a random point inside a ball with a radius of 1) and the
   timer is **set** to `1 / shakeFrequency`. Between picks, the same direction is held. Set rather than
   added to, so a long hitch doesn't make it fire several times to catch up.

   It used to pick a new offset **every frame**, so at 180 fps the camera jumped 180 times a second and the
   same shake looked three times buzzier than at 60. Picking at a fixed rate looks the same on any screen.
6. **Scale it.** `lastOffset = shakeDirection × strength`. The direction can point anywhere: left/right,
   up/down, and forward/back. The strength still fades every frame, so the shake dies away smoothly.
7. **Apply it.** `localPosition += lastOffset`, and remember it for step 1 next frame.
8. **When the time runs out,** reset `magnitude` to 0, so the next shake's "keep the bigger one" starts
   fresh.

### Why undo and redo instead of saving a home position

The simpler way would be to save the camera's position at the start and snap back to it after. But if
anything else moves the camera during the shake, snapping back would undo that movement too. Removing
only its own offset each frame means CameraShake never needs to know where the camera "should" be. It
only ever adds and removes its own jitter.

It uses `localPosition` (position relative to the parent) instead of `position` (position in the world),
so it still works if the parent moves.

## Talks to

- **AsteroidBehavior**: calls `Shake` when the player smashes an asteroid, and when `TakeHit` accepts a hit.
- **PlayerDeath**: calls `Shake` when the ship explodes.

CameraShake doesn't read from or call anything itself.

## Gotchas

- **There are two in the scene, and only one works.** Both `Camera Shake` (the parent) and `Main Camera`
  have the component. Each one sets `instance` in `Awake`, so whichever runs last replaces the other, and
  Unity doesn't guarantee which that is. If it's the parent, both cameras shake. If it's `Main Camera`,
  only the gameplay camera shakes and the earth stays perfectly still. There's also no warning about
  it, unlike GameManager's duplicate check.
- **A new shake can cut an old one short.** The code comment says a small hit can't cut a big one short,
  but that's only true for the strength. The timer is always reset to the *new* duration. A 0.15s hit
  landing just after a 0.2s smash ends the whole shake 0.15s from then.
- **It shakes forward and back too.** `insideUnitSphere` includes z, so the camera also moves toward and
  away from the scene. Use `Random.insideUnitCircle` for a flat, screen-only shake.
- **It ignores pauses.** Because it counts real time, a shake keeps playing if time is frozen, including
  any future pause menu that uses `Time.timeScale`.
- **The shake rate can't go above the frame rate.** `shakeFrequency` higher than the screen's frame rate
  just picks a new direction every frame.
- **PlayerDeath's mesh shake uses the same idea,** with its own `shakeFrequency` (also 60) for the ship's
  shake before it explodes.
