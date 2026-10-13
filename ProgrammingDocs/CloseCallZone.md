# CloseCallZone

## Purpose

Holds the close-call range on the player and measures how close an asteroid came to the nearest body.
Asteroids use it to decide close calls, and it draws the range in the editor. It's passive: it doesn't watch
asteroids or add points itself. Each asteroid asks it for the gap and decides (see AsteroidBehavior's
`TrackCloseCall`).

A **close call** is a threatening asteroid that passes within `closeCallDistance` of a body's hitbox edge
without touching it. It scores `closeCallPoints` as a combo kill (see Score's `AddCloseCall`).

## Where it lives

- **Attached to:** the player. GameManager finds it there and exposes it as `CloseCallZone`, which is how
  asteroids get it.
- **Requires:** `PlayerShell` on the same object for the hitbox reach (without it, the gap is measured from
  the body's centre), and `PlayerTwins` to include twin copies (optional).

## Inspector settings

| Field | Default | What it changes |
| --- | --- | --- |
| `closeCallDistance` | 1.5 | The gap past the hitbox edge that still counts as close. Shown as the yellow gizmo sphere. 0 turns close calls off |

## How it works

### `Distance` and `Reach`

- **`Distance`**: the close-call setting (`closeCallDistance`), read-only for other scripts.
- **`Reach`**: the radius of the player's active hitbox, from `PlayerShell.Reach` (the body collider, or the
  bigger shell while shielded). 0 without a PlayerShell. Twin copies mirror the player's shell, so one reach
  fits every body.

### `Awake`

Sets up references: `PlayerShell` and `PlayerTwins`, found with `GetComponent` because they're on the same
object (the rule for scripts on the player).

### `NearestGap`

Measures the edge-to-edge gap between an asteroid and the nearest body (the player or a twin copy), and gives
back that body's position through `out bodyPosition`, for the popup.

1. Start with the player as the nearest body.
2. Loop over `PlayerTwins.Copies` with `for` and an index (no garbage), keeping whichever body is closer.
3. Return `nearest centre distance − asteroidRadius − Reach`.

Distances use `Vector2.Distance`, which ignores depth. The asteroid is measured as it crosses the player's
depth, and any small depth difference shouldn't make a near miss look farther than it is on screen.

| Centre distance | Asteroid radius | Reach | Gap | With `Distance` 1.5 |
| --- | --- | --- | --- | --- |
| 3.2 | 1 | 0.5 | 1.7 | too far |
| 2.8 | 1 | 0.5 | 1.3 | close call |
| 1.4 | 1 | 0.5 | −0.1 | overlapped, so not rewarded |

### `OnDrawGizmosSelected` and `DrawZone`

Visual representation in the editor, drawn only while the player is selected. `DrawZone` draws a yellow
wire sphere of radius `Reach + closeCallDistance` around a body. That's exactly where a close call stops
counting, because the gap is measured from the hitbox's edge. In Play mode it's drawn around every twin copy
too, and it grows with the shield.

`Awake` doesn't run in Edit mode, so the cached `shell` is still `null` there. The gizmo falls back to
`GetComponent<PlayerShell>()` to get the reach.

## Talks to

- **PlayerShell**: reads `Reach`.
- **PlayerTwins**: reads `Copies`.
- **AsteroidBehavior**: calls `NearestGap` when it crosses the player's depth, and reads `Reach` and
  `Distance` to judge the close call.
- **GameManager**: finds it on the player and exposes it as `CloseCallZone`.

## Gotchas

- **The range is measured from the hitbox edge, not the centre.** At full health the shield is the
  hitbox, so the close-call range grows with it.
- **No PlayerShell means a reach of 0**, so close calls are measured from the body's centre and the yellow
  sphere is too small for the real hitbox.
- **Only asteroids use it.** A new hazard would need its own crossing check to score close calls.
