# FreeBoostPips

## Purpose

Shows how many free boosts the player has left from a Boost Recharge power-up, as small rings beside the boost ring.
One ring is lit per free boost, and a ring disappears when its boost is used.

## Where it lives

- **Attached to:** the player's root (`hamter`, in `LevelCore.prefab`) and the `Twin Copy` prefab's root.
- **Requires:** two small ring objects, `Free Boost Ring 1` and `Free Boost Ring 2`, as **siblings** of the main `ring` (not
  children, since the ring rolls on a dodge and would swing them round). Each uses the same `Ring Glow` material as the
  main ring (so the same cyan colors and bands, only smaller), has no collider, and starts inactive. No BoostRing drives
  them, so they keep the material's own `_Charge` of 1, the "ready" look. Ring 1 is at local (0, +0.3, 0), toward the orbit
  center, and Ring 2 at (0, −0.3, 0), away from it, both at scale (0.4, 0.025, 0.4) on the player. On the twin copy they
  are at (0, ±0.21, 0) and scale (0.28, 0.025, 0.28).
- **Finds:** `PlayerMovement` on a parent (the player), or through `GameManager.Instance.Player` (a twin copy).

## Inspector settings

| Field | Default | What it changes |
| --- | --- | --- |
| `pips` | empty | The small rings. Element 0 shows while at least 1 free boost is left, element 1 while at least 2 are. The last one in the list disappears first when a boost is used |

## How it works

### `Awake`

Looks for a `PlayerMovement` on this object or a parent (`GetComponentInParent`, which also checks the object itself).
A twin copy has none, so it falls back to `GameManager.Instance.Player`, the same two-step lookup as BoostRing.

### `Refresh`

Goes through `pips` once and sets ring `i` active when `i < count`. Normally `count` is `FreeBoosts`: with 2 free boosts both rings are on;
after one boost only ring 0 is; after both, neither. A refill turns the missing ring back on. An empty slot in the list is skipped.

**While Lance is active** `count` is `ShotsLeft − 1`: the center ring is the first shot and the two small rings are the extra ones (3 shots = both on).
`Tint` also paints each small ring pink with a property block filled by `BoostRing.WriteLanceColors`, and clears the block (`SetPropertyBlock(null)`) when Lance ends.
`Awake` finds the PlayerLance (`GameManager.PlayerLance`) and the ring (`GetComponentInChildren<BoostRing>(true)`); `OnEnable`/`OnDisable` also
subscribe to `PlayerLance.OnShotsChanged`.

### `OnEnable` / `OnDisable`

`OnEnable` subscribes `Refresh` to `PlayerMovement.OnFreeBoostsChanged` and calls it once straight away, so a twin copy that
appears while the player already has free boosts shows the right rings. `OnDisable` unsubscribes, because twin copies are
switched on and off as the twin power-up comes and goes. There is no `Update`: the rings only change on that event.

## Talks to

- **PlayerMovement**: reads `FreeBoosts` and `OnFreeBoostsChanged`.
- **PlayerLance**: reads `IsActive`, `ShotsLeft` and `OnShotsChanged`.
- **BoostRing**: `WriteLanceColors`, for the pink.

## Gotchas

- **The rings must start inactive** (they are saved that way); the first `Refresh` also sets them from the real count.
- **They are not on `roll_mesh`,** so they don't roll with the mouse or blink during hit invulnerability, like the main ring.
- **Unassigned slots** in `pips` are skipped, so a missing ring just doesn't show.
- **The ring positions and sizes are a first guess.** Move or resize the two ring objects in the prefab to taste.
- **Every player prefab needs them.** `LevelCore.prefab` (the levels and the `PatternTest` scene) and `Twin Copy.prefab`
  have them. The old standalone `hamter.prefab` does not, but no scene uses it any more (the pattern builder only reads
  its max speed).
