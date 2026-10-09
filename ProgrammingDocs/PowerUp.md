# PowerUp

## Purpose

A pickup that works like an asteroid: it flies toward the camera (and the player) and, when the player's
collider touches it, does something. Each power-up type has its own effect. Every pickup also adds one to
the power-ups stat in RunStats.

There are two types:

- **Health** gives the player a life back, up to their max.
- **Invincible** makes the player invincible for a few seconds: every asteroid they touch is busted, and a
  rainbow overlay shows on the ship. The timer and overlay live in PlayerInvincibility on the player.

## Where it lives

- **Attached to:** each power-up prefab, e.g. `Assets/Prefabs/Health PowerUp.prefab`. The prefab also needs
  a collider with **Is Trigger** on, and no Rigidbody (the player has the Rigidbody, the same as with
  asteroids).
- **Placed in:** wave pattern prefabs, as **direct children** of the pattern root, using the pattern
  builder's Power-Up slot. ObjectSpawner reveals them like asteroids.
- **Requires:**
  - The SpawnableTemplate asset in `template`, for the shared speed multiplier, despawn distance, and the
    Power-Up section (spin, pickup effect, pickup shake).
  - `GameManager.Instance`, for the gameplay camera (despawning), `HasWon`, and RunStats. Without one, it
    still flies and works, but never despawns and isn't counted.

## Inspector settings

| Field | Default | What it changes |
| --- | --- | --- |
| `type` | Health | Which effect this power-up has. A dropdown of `PowerUpType` |
| `speed` | 20 | Base approach speed, before the player's speed is added |
| `template` | none | The SpawnableTemplate asset. Assign the same one the asteroids use |

## How it works

### `PowerUpType` and one method per effect

`PowerUpType` is an **enum** declared inside the class, so it shows as a dropdown. All power-up types live
in this one class. Each type gets its own private method for its effect, and `ApplyEffect` picks the right
one with a `switch`.

Adding a new type takes three things:

1. a new entry in the enum (it appears in the dropdown straight away),
2. a method for its effect,
3. a `case` in `ApplyEffect`'s switch that calls that method.

Without the method and the case, the new type can be picked in the Inspector but does nothing.

### `ApproachSpeed`

`speed + PlayerMovement.Speed × template.speedMultiplier`, worked out fresh each time it's read, so it
rises and falls with the player's speed. It's the same formula as asteroids, so a power-up laid out
between two asteroids stays between them in flight. ObjectSpawner reads it to time when to reveal it.

### `Awake` and `OnEnable`

- **`Awake`** gets the gameplay camera, the player's PlayerMovement, PlayerHealth and PlayerInvincibility
  from GameManager, once.
- **`OnEnable`** adds it to the static `Active` list, records where the power-up was revealed (`origin`)
  and resets `distance` to 0.
- **`OnDisable`** removes it from `Active`. Every way a power-up leaves (picked up or missed) goes through
  `PatternCopy.Despawn`, which switches it off (running `OnDisable`) and puts it back at its spot in its
  pattern copy for the pattern's next use. Outside a pooled pattern, `Despawn` destroys it as before.

`Active` is shared by the whole class, the same as `AsteroidBehavior.Active`. GameManager reads it to know
whether the field is clear before the level can be won.

The start point is recorded in `OnEnable`, not `Awake`, because of how the spawner reveals things. It
creates the pattern (running `Awake` and `OnEnable`), switches every child off, and later moves each one to
the spawn plane and switches it back **on**. Only `OnEnable` runs again at that point. Recording `origin` in
`Awake` would keep the laid-out depth, and the power-up would jump back to it and arrive late.

### `Update`

Each frame, the same as a Linear asteroid:

1. **Move.** `distance += ApproachSpeed × Time.deltaTime`, then `position = origin + back × distance`.
   `distance` is a running total, so a change in the player's speed changes how fast it grows from then
   on, instead of making the power-up jump.
2. **Spin** around the world's up axis at `template.powerUpSpinSpeed`.
3. **Despawn.** Once it's `template.despawnBehindCamera` units past the camera, it's been missed, so it
   calls `PatternCopy.Despawn(gameObject)`. Its spin is undone too: going home restores its authored
   rotation, so a reused power-up starts upright.

Without a template it still moves, but doesn't spin or despawn. There's no warning blink, unlike
asteroids. At game over the freeze sets `Time.deltaTime` to 0, so it stops by itself.

### `OnTriggerEnter`

When something touches it:

1. **Is it the player?** `other.transform.IsChildOf(player.transform)` is true when what it touched is the
   player or anywhere under it. Anything else, or no cached player, is ignored.
2. **Is the level already won?** It reads `GameManager.Instance`. If `HasWon` is set, it ignores the
   touch, and the power-up flies on through the ship without being used up.
3. **Count it.** Adds one to RunStats' `PowerUpsPickedUp`. This happens before the effect, so a pickup
   that does nothing (Health at full lives) still counts.
4. **Apply the effect** with `ApplyEffect()`.
5. **Feedback** from the template's Power-Up section: play `pickupEffectPrefab` through `EffectPool.Play`,
   and shake the camera only if `pickupShakeDuration` is above 0. (Skipping a 0 shake matters: calling
   `Shake` would restart the timer of a shake already running.)
6. **Despawn** the power-up with `PatternCopy.Despawn`.

It runs once per pickup because the player only ever has **one** collider on (PlayerShell switches the body
off while the shield is on). With two, a pickup healed twice, counted twice and, with pooling, played a
second explosion at the power-up's home spot.

### `ApplyEffect`, `Heal` and `MakeInvincible`

`ApplyEffect()` runs the effect method for this power-up's `type`. The effect methods take no
parameters: whatever they need from the player was already cached from GameManager in `Awake`. A new
effect that needs another player component gets it the same way (expose it on GameManager if it isn't yet).

**`Heal`** calls `GainLife()` on the cached PlayerHealth, which adds one life but never goes
past the max:

| Lives before | After | Counted? |
| --- | --- | --- |
| 1 | 2 | yes |
| 2 | 3 | yes |
| 3 (max) | 3 | yes (used up and wasted) |

**`MakeInvincible`** calls `Activate(template.invincibleDuration)` on the cached PlayerInvincibility.
It does nothing if either the component or the template is missing (the duration comes from the template).

The timer can't live in PowerUp, because the power-up is switched off on the same frame it's picked up, so
its `Update` would never count down. Instant effects like `Heal` can finish inside the effect method;
effects that last over time hand off to a script on the player.

A second pickup while invincible resets the timer to the full duration rather than adding to it.

## Talks to

- **ObjectSpawner**: reveals it and reads `ApproachSpeed` to time the reveal.
- **SpawnableTemplate**: reads `speedMultiplier`, `despawnBehindCamera`, `powerUpSpinSpeed`,
  `pickupEffectPrefab`, the pickup shake settings and `invincibleDuration`.
- **GameManager**: gets the gameplay camera, `Player`, `PlayerHealth` and `PlayerInvincibility` in
  `Awake`, reads `HasWon`, and reaches RunStats through it. GameManager
  reads `PowerUp.Active` to decide the win.
- **RunStats**: calls `AddPowerUpPickedUp()`.
- **PlayerMovement**: checks whether what touched the power-up is the player, and reads its static `Speed`.
- **PlayerHealth**: calls `GainLife()` (Health type).
- **PlayerInvincibility**: calls `Activate(duration)` (Invincible type).
- **CameraShake**: calls `Shake` on pickup, if a pickup shake is set.
- **PatternGizmos / PatternGizmosEditor**: the builder places power-ups, and the gizmo labels count them.
- **PatternCopy**: `Despawn` takes it back into its pattern copy.
- **EffectPool**: `Play` shows the pickup effect from the effect pool.

## Gotchas

- **It must be a direct child of a pattern,** or the spawner won't reveal it on its own.
- **It's reused.** Any new state must be reset in `OnEnable`, which runs on every reveal. Never `Destroy`
  it in code; call `PatternCopy.Despawn`.
- **The pickup effect must be assigned in the template** (`pickupEffectPrefab`), or there's no visual
  feedback.
- **A new type needs its `case`.** A type with no case in `ApplyEffect` is counted as picked up but does
  nothing.
- **It doesn't appear on the orbit ring.** OrbitRing only draws danger arcs for asteroids
  (`AsteroidBehavior.Active`).
- **Only Linear movement.** It doesn't support Wave like asteroids do.
- **New enum entries go at the end.** Unity saves the `type` dropdown as a position number (Health = 0,
  Invincible = 1). Inserting a new type before an existing one would quietly change which type every
  existing prefab is.
- **It can't be smashed or hit.** Asteroids' smash and knockback logic doesn't apply; touching it is always
  a pickup (unless the level is won).
