# PlayerTwins

## Purpose

The twin power-up. When a Twin power-up is picked up, a copy of the player is made and the copies move to
equidistant points on the orbit, depending on how many bodies are alive. A hit during the twin destroys one
body and **lives don't change**. When the last copy is gone, the player carries on with the lives they had
before the pickup.

**The trick:** the real player is never destroyed. When the player itself is hit, it jumps onto the nearest
copy's spot and **that copy** is removed, with the explosion played where the player was hit. The bodies look
identical, so it reads as "my ship died and the copy carried on," while everything that holds on to the
player (GameManager, the camera, the shell, the flame) keeps working.

## Where it lives

- **Attached to:** the player (`hamter` prefab), next to PlayerMovement.
- **Requires:**
  - **PlayerMovement** on the same GameObject, found in `Awake`.
  - A **`Twin Copy` prefab** with TwinCopy on its root, the ship's visuals and flame, a shell, two trigger
    colliders, and a kinematic **Rigidbody** (trigger events need one on at least one side).
  - A **Twin PowerUp** (a PowerUp with Type = Twin) to start it.

## Inspector settings

| Field | Default | What it changes |
| --- | --- | --- |
| `twinPrefab` | none | The copy that's created. It's a `TwinCopy`, so a prefab without the script can't be assigned |
| `maxBodies` | 4 | Most bodies at once (player + copies). Pickups past this do nothing. `[Min(2)]` |
| `respaceDuration` | 0.3 | Seconds for copies to ease to new spacing |
| `destroyEffect` | none | Effect played where a body is destroyed, through EffectPool (e.g. `Player Explosion`) |
| `destroyShakeDuration` | 0.2 | Camera shake length when a body is destroyed. 0 = no shake |
| `destroyShakeMagnitude` | 0.3 | Camera shake strength |

## How it works

### `IsTwinned` and `OnTwinChanged`

`IsTwinned` is `copies.Count > 0`, worked out from the list every time, so it can't fall out of step with the
copies that actually exist. `OnTwinChanged(true)` is raised when the first copy is added, and
`OnTwinChanged(false)` when the last one is gone. DangerWarning and PlayerShell listen to pause and resume the
life-based visuals.

### `Awake`

Gets the PlayerMovement component on the same GameObject. PlayerTwins is on the player, so by the rule in
AGENTS.md section 5 it finds its sibling itself.

### `FindBody`: which body a hit landed on

Returns the transform of the body a collider belongs to, or `null`. Checks, in order:

1. **The player** (`IsChildOf`, so colliders on children count).
2. **The copy the player jumped onto this frame** (`jumpedOnto`, `jumpFrame == Time.frameCount`): counts as the
   **player**. Unity destroys objects at the end of the frame, so a second asteroid can still touch that copy's
   collider in the same frame, and the player is standing there now.
3. **Each live copy.**

It works when not twinned too (just the player check), so AsteroidBehavior and PowerUp use it for every
contact.

### `AddBody`: a twin pickup

1. If the bodies are already at `maxBodies`, do nothing.
2. Remember whether we were already twinned (read **before** adding, since `IsTwinned` comes from the list).
3. Create one copy at the player's position and rotation. Its offset starts at 0, so it's exactly on the player.
4. Add it to `copies` and `Respace()`: the new copy splits off the ship and glides out.
5. If this was the first copy, raise `OnTwinChanged(true)`.

### `Respace`: even spacing, counter-clockwise

1. **Wrap** every copy's offset into 0–360° (`Mathf.Repeat`, applied with `ShiftOffset`). 370° and 10° are the
   same spot, so nothing moves; only the number changes.
2. **Sort** the copies by offset, smallest first. `Sort` takes a lambda, `(a, b) => a.Offset.CompareTo(b.Offset)`,
   that says how to compare two copies (a higher-order function).
3. **Hand out slots** counter-clockwise from the player: spacing = 360° ÷ bodies, copy *i* gets
   `spacing × (i + 1)`, eased over `respaceDuration`.

| Bodies | Spacing | Copy slots |
| --- | --- | --- |
| 2 | 180° | 180° |
| 3 | 120° | 120°, 240° |
| 4 | 90° | 90°, 180°, 270° |

Without the wrap, a copy at −30° (just clockwise of the player) would sort first and travel 150° straight
through the player to reach 120°. Wrapped to 330°, it sorts last and takes a short move.

### `LoseBody`: a body is hit during the twin

Called by AsteroidBehavior when a body is hit while twinned.

1. Only while twinned, and only for a real body.
2. **Remember where the hit body is** (`where = body.position`) **before** anything moves. If the player is the
   body, the jump in the next step moves it, and the explosion has to play where it was hit.
3. **Pick the copy to remove:** the player was hit → `JumpToNearestCopy()`; a copy was hit → that copy
   (`copies.Find`, another lambda).
4. Play `destroyEffect` and the camera shake at `where`.
5. Remove the copy from the list and destroy it.
6. Copies left → `Respace()`. None → `OnTwinChanged(false)`.

### `JumpToNearestCopy`: the player was hit

1. Find the copy with the smallest `AngleFromPlayer`. On a tie, the first in the list wins (the loop only
   replaces it when another is strictly closer).
2. `movement.ShiftOrbit(nearest.Offset)`: the player jumps onto that copy's spot.
3. Every other copy gets `ShiftOffset(−jump)`, so it stays put in the world.
4. Remember `jumpedOnto` and `jumpFrame` for `FindBody`'s same-frame rule, and return the copy so `LoseBody`
   removes it.

Example: four bodies at 0° (player), 90°, 180°, 270°. The player is hit. 90° and 270° are both 90° away; 90° is
first in the list. The player jumps +90°, the others shift to 90° and 180°, then three bodies re-space to 120°
and 240°.

### `AngleFromPlayer`

How far a copy is from the player around the ring, either way, in degrees: `Mathf.DeltaAngle` gives the
shortest signed gap (−180° to 180°), and `Abs` drops the direction. A copy at 270° counts as 90° away. It's
`static` because it only uses the copy it's given.

## Talks to

- **PlayerMovement**: found in `Awake`; `ShiftOrbit` on a player hit.
- **TwinCopy**: creates and destroys copies; calls `MoveTo` and `ShiftOffset`, reads `Offset`.
- **EffectPool**: `Play(destroyEffect, where)`.
- **CameraShake**: `Shake` when a body is destroyed.
- **AsteroidBehavior**: calls `FindBody` on every contact and `LoseBody` on a hit while twinned.
- **PowerUp**: calls `FindBody` on contact, `AddBody` for the Twin type, and reads `IsTwinned` (Health gives
  nothing while twinned).
- **DangerWarning** and **PlayerShell**: subscribe to `OnTwinChanged`, read `IsTwinned`.
- **GameManager**: exposes it as `PlayerTwins` for scripts not on the player.

## Gotchas

- **Lives never change during the twin.** PlayerHealth still holds the pre-pickup count the whole time; any hit
  destroys a body instead of calling `LoseLife`. So there's no saved-lives copy to keep in step.
- **No hit-invulnerability in the twin.** `LoseBody` doesn't call `TakeHit`, and ending the twin doesn't give
  invulnerability, so a hit straight after losing the last copy costs a life.
- **Same-frame double hit:** two bodies hit in the same frame when only one copy is left → the first ends the
  twin, the second is a normal hit (a life is lost).
- **Copies are destroyed, not pooled.** Twin pickups are rare and there are at most three copies.
- **The copy prefab needs a Rigidbody** (kinematic), or asteroids never notice its colliders.
- **Never `Destroy` a copy from elsewhere.** It would stay in `copies` as a destroyed object. Go through
  `LoseBody`.
- **The player's flame snaps after a jump** through PlayerMovement's `OnOrbitShifted` (see ExhaustFlame).
