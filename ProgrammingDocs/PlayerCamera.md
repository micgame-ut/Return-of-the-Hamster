# PlayerCamera

## Purpose

Widens the gameplay camera's field of view as the player speeds up, and narrows it again as they slow
down. It's a common trick in racing games: a wider view stretches things out toward the edges of the
screen, which makes going fast *feel* faster.

Despite the name, it doesn't move or aim the camera. It only changes the field of view.

## Where it lives

- **Attached to:** `Main Camera` in `Game.unity`.
- **Requires:** a Camera, either assigned to `cam` or on the same GameObject.

## Inspector settings

| Field | Default | In the scene | What it changes |
| --- | --- | --- | --- |
| `cam` | none | none | The camera to change. If left empty, it uses the Camera on the same GameObject |
| `baseFOV` | 60 | 60 | Field of view (in degrees) when the player's `Speed` is 0 |
| `maxFOV` | 80 | 80 | Field of view when the player is at max speed |
| `fovLerpSpeed` | 5 | 10 | How quickly the field of view catches up to its target. Higher is snappier |

**Field of view (FOV)** is how wide the camera sees, as an angle from the top of the screen to the
bottom. A bigger number shows more of the scene, so everything looks smaller and further away.

## How it works

### `Start`

If `cam` wasn't assigned in the Inspector, it grabs the Camera on the same GameObject.

### `Update`

Every frame, two steps, both using **Lerp** (see LevelTimer):

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

`fieldOfView = Lerp(fieldOfView, targetFOV, fovLerpSpeed × Time.deltaTime)`

This doesn't jump straight to the target. Each frame it moves a **fraction of the remaining distance**.
With `fovLerpSpeed = 10` at 60 frames per second, that fraction is 10 × 0.0167 ≈ 0.17, so it closes 17%
of the gap every frame. The gap shrinks each time, so it moves quickly at first and slows as it gets close.
That gives a smooth ease-out, so a sudden speed boost widens the view smoothly instead of popping.

PlayerMovement uses the same easing to settle the roll (`rollDamping`) and fade out boosts (`boostDecay`).

## Talks to

- **PlayerMovement**: reads the static `Speed01` every frame.

## Gotchas

- **Only the gameplay camera changes.** Earth Camera keeps its own FOV, so the earth doesn't stretch with
  the rest of the view as the player speeds up.
- **The easing depends slightly on frame rate.** Lerping by `speed × deltaTime` each frame gives
  slightly different results at different frame rates. It isn't noticeable at these values.
- **A very high `fovLerpSpeed` just snaps.** Lerp's fraction can't go past 1. Once
  `fovLerpSpeed × deltaTime` reaches 1 (about 60 at 60 fps), it jumps straight to the target every frame.
- **No camera means an error every frame.** If `cam` is empty and there's no Camera on the same GameObject,
  `Update` throws an error every frame.
