# PlayerCamera

## Purpose

Widens the gameplay camera's field of view as the player speeds up, and narrows it again as they slow
down. It's a common trick in racing games: a wider view stretches things out toward the edges of the
screen, which makes going fast *feel* faster.

Despite the name, it doesn't move or aim the camera. It only changes the field of view. On a forward boost it
also adds a short extra widening (the **FOV punch**) and a small camera shake, so a boost has a sharp kick you feel.

## Where it lives

- **Attached to:** `Main Camera` in `Game.unity`.
- **Requires:** a Camera, either assigned to `cam` or on the same GameObject, and a `GameManager` with the
  player assigned (for the boost event; without one the boost effects are simply skipped).

## Inspector settings

| Field | Default | In the scene | What it changes |
| --- | --- | --- | --- |
| `cam` | none | none | The camera to change. If left empty, it uses the Camera on the same GameObject |
| `baseFOV` | 60 | 60 | Field of view (in degrees) when the player's `Speed` is 0 |
| `maxFOV` | 80 | 80 | Field of view when the player is at max speed |
| `fovLerpSpeed` | 5 | 10 | How quickly the field of view catches up to its target. Higher is snappier |
| `boostFovKick` | 8 | 8 | Extra degrees of field of view added the instant a forward boost fires, then eased back out |
| `boostFovTime` | 0.4 | 0.4 | Seconds the extra field of view takes to fade out after a forward boost. 0 = no kick |
| `boostShakeDuration` | 0.3 | 0.3 | Seconds the camera shakes when a forward boost fires. 0 = no shake |
| `boostShakeMagnitude` | 0.12 | 0.12 | How far the camera shakes on a forward boost, in world units. A hit is 0.2, a smash 0.3 |

**Field of view (FOV)** is how wide the camera sees, as an angle from the top of the screen to the
bottom. A bigger number shows more of the scene, so everything looks smaller and further away.

## How it works

### `Awake`, `OnEnable` / `OnDisable`, `StartKick`

`Awake` gets the player's `PlayerMovement` from `GameManager.Instance.Player` (this script isn't on the player).
`OnEnable` subscribes `StartKick` to its `OnSpeedBoost` event and `OnDisable` unsubscribes, so a destroyed camera
is never called. `StartKick` runs when a forward boost fires: it sets `kickTimer` to `boostFovTime` and, if
`boostShakeDuration` is above 0, starts a `CameraShake.Shake(boostShakeDuration, boostShakeMagnitude)`.
`CameraShake` keeps the stronger of overlapping shakes, so a boost never weakens a hit's shake.

### `Start`

If `cam` wasn't assigned in the Inspector, it grabs the Camera on the same GameObject, and stores its field of
view as `currentFov`.

### `Update`

Every frame, three steps, the first two using **Lerp** (see LevelTimer):

**1. Work out the target FOV.**

`targetFOV = Lerp(baseFOV, maxFOV, PlayerMovement.Speed01)`

`Speed01` is the player's `Speed` divided by `MaxSpeed`, so it goes from 0 (stopped) to 1 (max speed).
Lerp then picks the FOV that same fraction of the way from 60 to 80:

| Player `Speed` | `Speed01` | Target FOV |
| --- | --- | --- |
| 0 | 0 | 60 |
| 5 | 0.5 | 70 |
| 10 (max) | 1 | 80 |

**2. Ease toward it.**

`currentFov = Lerp(currentFov, targetFOV, 1 − e^(−fovLerpSpeed × Time.deltaTime))`

`currentFov` is the smoothed value, kept in its own field instead of reading the camera's FOV back. That keeps the
boost kick (step 3) from feeding into the smoothing.

This doesn't jump straight to the target. Each frame it moves a **fraction of the remaining distance**.
With `fovLerpSpeed = 10` at 60 frames per second, that fraction is 1 − e^(−10 × 0.0167) ≈ 0.15, so it closes
15% of the gap every frame. The gap shrinks each time, so it moves quickly at first and slows as it gets
close. That gives a smooth ease-out, so a sudden speed boost widens the view smoothly instead of popping.

**Why `1 − e^(−k·dt)` and not just `k × dt`:** plain `k × dt` gives slightly different results at different
frame rates, because many small steps don't add up to the same as fewer big ones. `1 − e^(−k·dt)` is the
fraction that compounds correctly: three frames at 180 fps close exactly as much of the gap as one frame at
60 fps.

PlayerMovement uses the same easing to settle the roll (`rollDamping`) and fade out boosts (`boostDecay`).

**3. Add the boost kick.**

While `kickTimer` is above 0 it counts down, and `kick = boostFovKick × t²` where `t = kickTimer ÷ boostFovTime`
(1 at the instant of the boost, 0 at the end). The camera's field of view is set to `currentFov + kick`.
Squaring makes it snap wide, then fall away fast. With 8° over 0.4 s:

| Time since boost | `t` | `kick` |
| --- | --- | --- |
| 0 s | 1 | +8° |
| 0.1 s | 0.75 | +4.5° |
| 0.2 s | 0.5 | +2° |
| 0.4 s | 0 | 0° |

## Talks to

- **PlayerMovement**: reads the static `Speed01` every frame, and subscribes to `OnSpeedBoost`.
- **GameManager**: gets `Player` in `Awake`.
- **CameraShake**: calls `Shake` when a forward boost fires.

## Gotchas

- **Only the gameplay camera changes.** Earth Camera keeps its own FOV, so the earth doesn't stretch with
  the rest of the view as the player speeds up.
- **A very high `fovLerpSpeed` gets close to snapping.** The fraction approaches 1 (but never passes it),
  so at very high values the view reaches the target almost immediately.
- **No camera means an error every frame.** If `cam` is empty and there's no Camera on the same GameObject,
  `Update` throws an error every frame.
