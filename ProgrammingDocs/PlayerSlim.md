# PlayerSlim

## Purpose

The timer and size behind the Slim power-up. While it runs, the player and its twin copies shrink to `slimScale`, hold, then grow
slowly back to full size. It works like PlayerJackpot: the pickup disappears the moment it's collected, so the timer has to live on
the player. Unlike Jackpot, it also changes the player itself, by scaling its Transform.

## Where it lives

- **Attached to:** the player's root (`hamter`, in `LevelCore.prefab`), so the levels and the `PatternTest` scene have it.
- **Found by:** `GameManager` (the `playerSlim` field, or `GetComponent` on the player in `Awake`), exposed as `GameManager.PlayerSlim`.
  PowerUp reads it from there.
- **Requires:** a `PlayerTwins` on the same object (found with `GetComponent` in `Awake`) so the twin copies shrink too. The values
  come from the SpawnableTemplate, passed in by PowerUp.

## Inspector settings

None. Every value comes from `Activate`, so there is nothing to tune on the component.

## How it works

### Why scaling the root is enough

Nothing in the player scripts sets a scale, and every part is a child of the root: the mesh, the colliders, the boost ring, the
free-boost rings and the exhaust. A uniform `localScale` shrinks all of them together. The body collider (0.255 at full size, 0.1275 at
half) is what asteroids hit, and `PlayerShell.HitRadius`/`Reach` and `CloseCallZone` read the colliders' bounds live, so the hit area,
the shield and the close-call zone all follow with no extra code. `PlayerMovement` only sets the player's position and rotation, never the
scale, so the scale survives.

### `Activate(duration, scale, shrinkTime, growTime)`

Called by PowerUp. A duration of 0 or less does nothing. Sets `timer` to the full duration (a second pickup **restarts** it, it doesn't
add, and doesn't shrink further), stores `scale` as the size to hold, and works out two speeds: `shrinkRate = (1 − scale) / shrinkTime`
and `growRate = (1 − scale) / growTime`, so each phase takes exactly its time (a time of 0 means instant). Example (scale 0.5, shrink
0.25, grow 1): 2 per second down, 0.5 per second up. It raises `OnSlimChanged(true)` only if Slim wasn't already running.

### `Update`

Counts the timer down; at 0 it raises `OnSlimChanged(false)`. Each frame it picks a target: the held size while Slim is active and more
than `growSeconds` remain, otherwise 1. It then moves `current` toward it with `Mathf.MoveTowards` at the shrink or grow speed, so the
changes are at a fixed speed. It calls `ApplyScale` when the size changed **and every frame while the player is small**, so a twin copy
made in the middle of Slim shrinks at once instead of staying full size until the grow-back.

Measured in a run (duration 6, grow 1): 0.98 → 0.50 within 0.25 s, held at 0.50, then 0.51, 0.64, 0.77, 0.90, 1.00 over the last second.

### `ApplyScale`

Sets the player's `localScale` and each twin copy's (`PlayerTwins.Copies`) to `Vector3.one * current`. It uses an index loop, not a
`foreach` over the read-only list, because a `foreach` over an interface list makes a little garbage every frame.

### `OnEnable` / `OnDisable` and `HandleLevelEnded`

Subscribes to GameManager's `OnGameOver` and `OnWin` (and unsubscribes in `OnDisable`). When the level ends it stops Slim and puts the
player and its twins back to full size at once: time freezes then, so `Update` would never finish the grow-back, and the ship would stay
tiny behind the end screen. Unlike PlayerJackpot it has to reset the scale itself, because the scale lives on the object.

## Talks to

- **PowerUp**: calls `Activate`.
- **PlayerTwins**: reads `Copies` to scale the twin copies.
- **GameManager**: exposes it, and raises the game-over and win events it listens to.

## Gotchas

- **Keep `slimGrowTime` below `slimDuration`.** If it is longer, the grow-back starts at once and the player never reaches the small size.
- **The player's root scale must be 1 at rest.** Slim sets the scale to `Vector3.one * current`, not a multiple of what it was.
- **Twin copies' roots must also be scale 1** (they are, in `Twin Copy.prefab`).
- **A gap that Slim fits and normal size doesn't:** with Linear rocks at scale 2 (collider radius 0.4) on the radius-3 ring, a normal-size
  player (0.255) needs rocks at least 25.2° apart centre to centre, and a half-size player (0.1275) needs 20.2°. `Pattern_Slim` uses 24°.
- **`OnSlimChanged` has no listener yet.** A sound or HUD indicator can subscribe later.
