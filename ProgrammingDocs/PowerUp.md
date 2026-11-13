# PowerUp

## Purpose

A pickup that works like an asteroid: it flies toward the camera (and the player) and, when the player's
collider touches it, does something. Each power-up type has its own effect. Every pickup also adds one to
the power-ups stat in RunStats.

There are four types:

- **Health** gives the player a life back, up to their max. During the twin power-up it gives nothing. It is only
  ever a fixed pickup, placed by hand in chosen waves: a Random pickup never gives it.
- **Invincible** makes the player invincible for a few seconds: every asteroid they touch is busted, and a
  rainbow overlay shows on the ship. The timer and overlay live in PlayerInvincibility on the player.
- **Twin** adds a copy of the player on the orbit (see PlayerTwins). A twin copy can also pick power-ups up.
- **ScoreMultiplier** adds +2 to the score multiplier for 10 seconds. Each pickup has its own timer, so
  overlapping pickups stack (see Score).

A fifth type, **Random**, is the `Mystery PowerUp`: on pickup it rolls one of **three** at random (Invincible,
Twin or ScoreMultiplier, never Health), using adjustable chances from the SpawnableTemplate, and skips any that
would do nothing right now. Every pickup announces what it gave (`OnCollected`), and ScorePopups shows a label
for it ("+1 LIFE", "INVINCIBLE", ...).

## Where it lives

- **Attached to:** each power-up prefab: `Mystery PowerUp` (type Random, the one the patterns use) and the
  fixed ones (`Health PowerUp`, `Invincibility PowerUp`, `Score Powerup`). `Mystery PowerUp` is the old
  `Twin PowerUp` renamed (same GUID), so patterns that placed the Twin prefab now place it; there's no fixed
  Twin prefab any more. The prefab also needs
  a collider with **Is Trigger** on, and no Rigidbody (the player has the Rigidbody, the same as with
  asteroids).
- **Placed in:** wave pattern prefabs, as **direct children** of the pattern root, using the pattern
  builder's Power-Up slot. ObjectSpawner reveals them like asteroids.
- **Requires:**
  - The SpawnableTemplate asset in `template`, for the shared base approach speed and speed multiplier, despawn distance, and the
    Power-Up section (spin, pickup effect, pickup shake).
  - `GameManager.Instance`, for the gameplay camera (despawning), `HasWon`, and RunStats. Without one, it
    still flies and works, but never despawns and isn't counted.

## Inspector settings

| Field | Default | What it changes |
| --- | --- | --- |
| `type` | Health | Which effect this power-up has. A dropdown of `PowerUpType`. Random rolls Invincible, Twin or Score Multiplier on pickup, never Health |
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

To make a new type rollable by Random as well, also add it to `RollTypes` (`rollWeights` is sized from its
length, so it follows), give it a chance in SpawnableTemplate and a line in `ChanceOf`, a line in `IsWasted` if it can be
wasted, and a label in ScorePopups.

**Add new types at the end of the enum.** Unity saves an enum field as its position number (Health = 0,
Invincible = 1, Twin = 2, ScoreMultiplier = 3, Random = 4). Inserting a type earlier in the list would shift
those numbers and quietly change the type of every existing power-up prefab. That's why Random went at the
end, even though it's the main one now.

### `RollType`: the random pick (Random type)

A **weighted random pick**. Picture each type's chance laid end to end on a ruler: each type owns a stretch
as long as its chance, and a random number is a dart thrown at the ruler.

1. **`FillWeights(true)`** fills `rollWeights` with each type's chance (`ChanceOf`, from the template), or 0
   if picking it up right now would be wasted (`IsWasted`). Negative chances count as 0. It returns the
   total, the ruler's length.
2. **If the total is 0** (everything with a chance is wasted), `FillWeights(false)` fills it again from the
   plain chances, ignoring the wasted rule, so the pickup still does something.
3. **The dart:** `roll = Random.Range(0, total)`.
4. **The walk:** for each type in `RollTypes` order, skipping weights of 0: if `roll` is under its weight,
   that type wins; otherwise subtract its weight and move on. `Random.Range` with decimals can return the
   total itself, which no stretch contains, so if the walk ends without a winner, the last type with a
   weight wins.

With the code's default chances (20 / 20 / 20):

| Situation | Invincible | Twin | Score |
| --- | --- | --- | --- |
| Normal | 0 – 20 (33%) | 20 – 40 (33%) | 40 – 60 (33%) |
| Twin at the cap (Twin wasted) | 0 – 20 (50%) | — | 20 – 40 (50%) |

A dart at 57.3 in the normal case: Invincible? 57.3 isn't under 20, so 37.3 is left. Twin? 37.3 isn't under 20,
so 17.3 is left. Score? 17.3 is under 20, so Score wins. The chances are **shares of the total**, so they don't
have to add up to 100; when one is wasted the others keep their sizes relative to each other. (The scene's asset
uses 10 / 25 / 25.)

**`IsWasted`** uses the same conditions that make the effect do nothing:

| Type | Wasted when |
| --- | --- |
| Twin | no PlayerTwins, or it's at the cap (`!CanAddBody`) |
| Invincible, ScoreMultiplier | never (a second pickup restarts or stacks) |

`RollTypes` is `static readonly` (the same for every power-up). `rollWeights` is one array per power-up,
made once and refilled each roll; power-ups are pooled, so a pickup makes no garbage. `ChanceOf` and
`IsWasted` are **switch expressions** (`t switch { case => value, _ => default }`).

### `ApproachSpeed`

`template.baseApproachSpeed + PlayerMovement.Speed × template.speedMultiplier`, worked out fresh each time
it's read, so it rises and falls with the player's speed. It's the same formula and the same template
values as asteroids, so a power-up laid out between two asteroids stays between them in flight.
ObjectSpawner reads it to time when to reveal it. Without a template it's 0, so the power-up doesn't move.

### `Awake` and `OnEnable`

- **`Awake`** gets the gameplay camera, the player's PlayerMovement, PlayerHealth, PlayerInvincibility and
  PlayerTwins, and the level's Score, from GameManager, once. It also sets up the depth tint: it finds every
  `MeshRenderer` under the power-up (the Health and Score ones have several), and for each material slot saves
  the renderer, the slot and the slot's own `_BaseColor` in `tintSlots` (a `TintSlot` struct), plus a
  `tintBlock` to apply them with.
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

0. **Depth tint (`UpdateDepthTint`)**, first of all, so it applies during the warning wait too. Works out
   `distance = its z − the player's z`, asks `template.GetDepthTint(distance)` for a color between white and
   `farColor`, and, if that changed since last frame (`lastTint`), sets `_BaseColor` = each slot's saved color ×
   the tint with a property block, slot by slot. A property block replaces a value instead of multiplying it,
   which is why the original colors are saved in `Awake`. Without a template or a cached player it does
   nothing.
1. **Move.** `distance += ApproachSpeed × Time.deltaTime`, then `position = origin + back × distance`.
   `distance` is a running total, so a change in the player's speed changes how fast it grows from then
   on, instead of making the power-up jump.
2. **Spin** around the world's up axis at `template.powerUpSpinSpeed`.
3. **Despawn.** Once it's `template.despawnBehindCamera` units past the camera, it's been missed, so it
   calls `PatternCopy.Despawn(gameObject)`. Its spin is undone too: going home restores its authored
   rotation, so a reused power-up starts upright.

Without a template it doesn't move (its `ApproachSpeed` is 0), spin, despawn or tint. Like an asteroid, it
waits `warningTime` seconds before it starts moving. At game over the freeze sets `Time.deltaTime` to 0, so it stops by itself.

### `OnTriggerEnter`

When something touches it:

1. **Is it the player or a twin copy?** PlayerTwins' `FindBody(other.transform)` returns the body that was
   touched (using `IsChildOf`, so colliders on children count), or null. Without a PlayerTwins, it falls back
   to `other.transform.IsChildOf(player.transform)`. Anything else, or no cached player, is ignored.
2. **Is the level already won?** It reads `GameManager.Instance`. If `HasWon` is set, it ignores the
   touch, and the power-up flies on through the ship without being used up.
3. **Count it.** Adds one to RunStats' `PowerUpsPickedUp`. This happens before the effect, so a pickup
   that does nothing (a fixed Health pickup at full lives) still counts.
4. **Pick the type and apply it.** `picked` is `RollType()` for a Random power-up (with a template), or
   its own `type` otherwise. `ApplyEffect(picked)` runs that effect, then `OnCollected` is raised with
   `picked` and the power-up's position. A Random power-up with no template passes through as Random, which
   has no effect and no label.
5. **Feedback** from the template's Power-Up section: play `pickupEffectPrefab` through `EffectPool.Play`,
   and shake the camera only if `pickupShakeDuration` is above 0. (Skipping a 0 shake matters: calling
   `Shake` would restart the timer of a shake already running.)
6. **Despawn** the power-up with `PatternCopy.Despawn`.

It runs once per pickup because the player only ever has **one** collider on (PlayerShell switches the body
off while the shield is on). With two, a pickup healed twice, counted twice and, with pooling, played a
second explosion at the power-up's home spot.

### `ApplyEffect`, `Heal`, `MakeInvincible`, `AddTwin` and `AddScoreMultiplier`

### `OnCollected`

A `public static event System.Action<PowerUpType, Vector3>`, raised on every pickup with the type that was
applied and where. Static, so a listener subscribes once to the class and hears every power-up, like the
static `Active` list. ScorePopups listens to show the label. It's written `System.Action` in full because
`using System;` would make `Random` ambiguous (`System.Random` vs `UnityEngine.Random`). A static event
outlives the scene, so every subscriber must unsubscribe in `OnDisable`.

`ApplyEffect(picked)` runs the effect method for the type it's given (the rolled one for Random). The effect methods take no
parameters: whatever they need from the player was already cached from GameManager in `Awake`. A new
effect that needs another player component gets it the same way (expose it on GameManager if it isn't yet).

**`Heal`** calls `GainLife()` on the cached PlayerHealth, which adds one life but never goes
past the max:

| Lives before | After | Counted? |
| --- | --- | --- |
| 1 | 2 | yes |
| 2 | 3 | yes |
| 3 (max) | 3 | yes (used up and wasted) |
| any, while twinned | unchanged | yes (used up and wasted) |

**`MakeInvincible`** calls `Activate(template.invincibleDuration)` on the cached PlayerInvincibility.
It does nothing if either the component or the template is missing (the duration comes from the template).

The timer can't live in PowerUp, because the power-up is switched off on the same frame it's picked up, so
its `Update` would never count down. Instant effects like `Heal` can finish inside the effect method;
effects that last over time hand off to a script on the player.

A second pickup while invincible resets the timer to the full duration rather than adding to it.

**`AddTwin`** calls `AddBody()` on the cached PlayerTwins, which adds a copy and re-spaces the bodies (up to
`maxBodies`; past that it does nothing).

**`AddScoreMultiplier`** calls `AddMultiplierPowerUp()` on the cached Score, which starts another 10-second
timer. Like invincibility, the timer lives in Score, not here, because the power-up is switched off as soon
as it's picked up.

## Talks to

- **ObjectSpawner**: reveals it and reads `ApproachSpeed` to time the reveal.
- **SpawnableTemplate**: calls `GetDepthTint` every frame for the depth fade, and reads `baseApproachSpeed`,
  `speedMultiplier`, `despawnBehindCamera`, `powerUpSpinSpeed`,
  `pickupEffectPrefab`, the pickup shake settings, `invincibleDuration` and the four Random chances.
- **ScorePopups**: listens to `OnCollected` and shows the picked type's label.
- **GameManager**: gets the gameplay camera, `Player`, `PlayerHealth`, `PlayerInvincibility`,
  `PlayerTwins` and `Score` in `Awake`, reads `HasWon`, and reaches RunStats through it. GameManager
  reads `PowerUp.Active` to decide the win.
- **RunStats**: calls `AddPowerUpPickedUp()`.
- **PlayerMovement**: checks whether what touched the power-up is the player, and reads its static `Speed`.
- **PlayerHealth**: calls `GainLife()` (Health type, a fixed pickup only).
- **PlayerInvincibility**: calls `Activate(duration)` (Invincible type).
- **PlayerTwins**: `FindBody` decides whether the player or a twin copy touched it; calls `AddBody()` (Twin
  type); reads `IsTwinned` so Health gives no life during the twin, and `CanAddBody` to skip Twin in a roll at the
  cap.
- **Score**: calls `AddMultiplierPowerUp()` (ScoreMultiplier type).
- **CameraShake**: calls `Shake` on pickup, if a pickup shake is set.
- **PatternGizmos / PatternGizmosEditor**: the builder places power-ups, and the gizmo labels count them.
- **PatternCopy**: `Despawn` takes it back into its pattern copy.
- **EffectPool**: `Play` shows the pickup effect from the effect pool.

## Gotchas

- **It must be a direct child of a pattern,** or the spawner won't reveal it on its own.
- **The depth tint covers every `MeshRenderer` under the power-up, found once in `Awake`.** A renderer added
  later isn't tinted. A material without `_BaseColor` is multiplied from white, and a particle or other
  non-mesh renderer is skipped.
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
- **A Random power-up needs the template.** The chances live there; without it, a Random pickup does nothing.
- **`IsWasted` must match the effects.** If an effect's "does nothing" rule changes (say Twin stops working
  during invincibility), update `IsWasted` too, or the roll will hand out wasted picks.
- **Health is never rolled.** It was taken out of `RollTypes`, so a Random pickup can't heal. Health pickups are
  placed by hand with the fixed `Health PowerUp` prefab, in chosen waves.
- **Every chance at 0 always gives Invincible** (the walk finds no stretch and falls back to the first slot).
- **It can't be smashed or hit.** Asteroids' smash and knockback logic doesn't apply; touching it is always
  a pickup (unless the level is won).
