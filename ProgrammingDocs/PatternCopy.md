# PatternCopy

## Purpose

Sits on **one copy** of a wave pattern (an instance in the scene, made from a pattern prefab). It remembers
where each of the copy's children belongs, takes children back instead of destroying them, and tells
ObjectSpawner when the whole copy is free to be reused. This is the pattern half of the game's **object
pooling**: copies are reused instead of being created and destroyed every wave.

## Where it lives

- **Attached to:** the root of every pattern copy, **added at runtime** by ObjectSpawner's `CreateCopy`
  (`AddComponent<PatternCopy>()`). No prefab has it.
- **Requires:** the pattern's asteroids and power-ups to be **direct children** of the root, the same as
  ObjectSpawner expects. They stay under the root the whole time.

## Inspector settings

None. It's only ever added in code.

| Member | What it is |
| --- | --- |
| `Prefab` | Which pattern prefab this is a copy of (public get, private set). ObjectSpawner uses it to put the copy back in the right free list |
| `OnReleased` | Event fired when the copy is free to reuse |
| `children`, `homePositions`, `homeRotations` | Same-length arrays: `children[i]` goes home to `homePositions[i]` and `homeRotations[i]` (local, relative to the root) |
| `childrenOut` | Children revealed and not yet home |
| `revealing` | True while the spawner is still revealing this copy |

## How it works

### `Init`: recording the home spots

Called by ObjectSpawner right **after** it creates the copy and **before anything is revealed**. It records
which prefab the copy came from, then each child and its local position and rotation. Local means relative
to the root, which is how the pattern was authored, so restoring them puts each child exactly where it was
placed. It must run before the first reveal: by then the first row has moved, and its mid-flight spots would
be recorded as home.

It's a normal method rather than `Awake` because `Awake` runs inside `Instantiate`, before ObjectSpawner can
pass in the prefab.

### `BeginReveal`, `ChildRevealed`, `EndReveal`: tracking the reveal

- `BeginReveal` sets `revealing`: the spawner is using this copy.
- `ChildRevealed` counts each child as it's sent out (`childrenOut++`).
- `EndReveal` clears `revealing`, then checks `ReleaseIfDone` straight away, in case every child is already
  home.

### `ReleaseIfDone`: when the copy is free

Fires `OnReleased` only when **both** are true: the spawner has finished revealing (`!revealing`) **and**
every revealed child is home (`childrenOut == 0`). ObjectSpawner is **subscribed** to the event and pushes the
copy back onto its free list. `?.Invoke` skips it if nothing is listening.

Why both: if the player smashes the whole first row before the second row is revealed, `childrenOut` is 0,
but the spawner is still revealing. Releasing then would let the next wave take the same copy while it's
still in use, and two reveals would fight over the same children (like returning a library book while
someone is still reading the second half).

### `Return`: taking one child back

Switches the child off, puts it back at its home position and rotation, counts it in (`childrenOut--`),
then checks `ReleaseIfDone`. It's switched off **before** it's moved, so it never renders a frame at its
home spot. `Array.IndexOf` finds the child's slot (at most a couple of dozen to search).

### `Despawn` (static): what to call instead of `Destroy`

`PatternCopy.Despawn(gameObject)` is called by AsteroidBehavior and PowerUp whenever they're done (smashed,
picked up, or past the camera). It's static, so callers don't need a reference to any copy, like
`CameraShake.Shake`.

It looks up the hierarchy (`GetComponentInParent`) for a PatternCopy. That's why children stay parented to
their copy. If it finds one, it calls `Return`. If not, it asks `EffectPool.ReturnAsteroid`, which takes the
object back if it's one of the pool's Splitter pieces (it has a `PoolSource`). Anything else (an object placed
directly in a scene, a power-up, or any scene with no pool) falls back to `Destroy`, as before pooling.

## Talks to

- **ObjectSpawner**: creates it, calls `Init`, `BeginReveal`, `ChildRevealed` and `EndReveal`, and listens to
  `OnReleased`.
- **AsteroidBehavior** and **PowerUp**: call `Despawn` instead of `Destroy`.
- **EffectPool**: `Despawn` calls `ReturnAsteroid` for objects that aren't in a pattern copy.

## Gotchas

- **Anything a pattern spawns must leave through `Despawn`.** A `Destroy` leaves a hole in the copy, and its
  next reuse throws errors.
- **Each child must be counted exactly once.** A double `Return` makes `childrenOut` one too low: the copy is
  released while a child is still flying, or it never reaches 0 and is never released (the pool grows every
  time the pattern comes up). The player has only one collider on at a time, so a child can't be hit twice
  in one physics step.
- **Children must stay under the copy.** Detaching one stops `Despawn` from finding its copy, so it would be
  destroyed instead.
- **Reused children rely on their `OnEnable`** to reset their state on every reveal.
