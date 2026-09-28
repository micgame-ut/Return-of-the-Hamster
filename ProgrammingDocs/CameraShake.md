# CameraShake

## Purpose

Makes the camera jitter briefly for impact: when the player smashes an asteroid, or gets hit by one.
Any script can trigger it with one line: `CameraShake.Shake(duration, magnitude)`.

## Where it lives

- **Attached to:** the `Camera Shake` GameObject in `Game.unity`. It's the parent of both `Main Camera`
  and `Earth Camera`, so shaking it shakes both cameras together.
- **Also on:** `Main Camera` itself. Only one of the two ever works (see Gotchas).
- **Requires:** nothing. It works on whatever GameObject it's attached to.

## Inspector settings

None. Every shake's strength and length come from whoever calls `Shake`. For asteroids, those values are
in the AsteroidTemplate asset:

| Event | Duration | Magnitude |
| --- | --- | --- |
| Smashing an asteroid | `shakeDuration` (0.2s) | `shakeMagnitude` (0.3) |
| Getting hit by an asteroid | `hitShakeDuration` (0.15s) | `hitShakeMagnitude` (0.2) |

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
3. **Count down.** `timeLeft -= Time.deltaTime`.
4. **Work out how strong it is now.** `strength = magnitude × (timeLeft / duration)`. `timeLeft / duration`
   goes from 1 down to 0 as the shake runs out, so the shake starts at full strength and fades to
   nothing instead of stopping suddenly.
5. **Pick a random offset.** `Random.insideUnitSphere` gives a random point inside a ball with a radius
   of 1. Multiplying by `strength` shrinks the ball to `strength` units. The offset can go in any
   direction: left/right, up/down, and forward/back.
6. **Apply it.** `localPosition += lastOffset`, and remember it for step 1 next frame.
7. **When the time runs out,** reset `magnitude` to 0, so the next shake's "keep the bigger one" starts
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
- **The shake rate depends on frame rate.** A new random offset is picked every frame, so at a higher
  frame rate the shake looks faster and more frantic.
