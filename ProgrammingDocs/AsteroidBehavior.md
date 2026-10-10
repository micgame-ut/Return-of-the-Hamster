# AsteroidBehavior

## Purpose

Controls a single asteroid from the moment it appears until it's gone. It blinks a warning glow, then flies
toward the camera, either in a straight line or swinging back and forth around the player's orbit (a
wave). When it touches the player, it
either breaks (if the player is fast enough) or knocks the player back.

## Where it lives

- **Attached to:** the root of each asteroid prefab (in `Assets/Prefabs/Asteroid Types/`).
  Asteroids are placed as children of pattern prefabs, which ObjectSpawner reveals one at a time.
- **Requires:**
  - A **Renderer on a child object** (the mesh). That child is what spins and glows.
  - A **Collider set to Is Trigger** on the root. The player has the Rigidbody, which Unity needs on at
    least one side for trigger events to fire.
  - An **SpawnableTemplate** asset assigned to `template`. Most of the tuning lives there (see Gotchas
    for what breaks without it).
  - `GameManager.Instance` for the gameplay camera, used to despawn asteroids once they're behind it.
  - The `OrbitSettings` asset, for the orbit that Wave asteroids swing along.

## Inspector settings

These are per-asteroid. Settings shared by every asteroid (glow colors, warning time, spin, knockback,
explosion, etc.) are on the SpawnableTemplate asset instead.

| Field | Default | What it changes |
| --- | --- | --- |
| `movementType` | Linear | The path it takes: Linear or Wave. Warning glow is tied to the type |
| `speed` | 20 | Base approach speed in units/sec, before the player's `Speed` is added on |
| `amplitude` | 30 | Wave: how many degrees it swings each way around the player's circle. Unused by Linear |
| `frequency` | 1 | Wave: full back-and-forth swings per second. Unused by Linear |
| `destructible` | false | Whether the player can smash it by being fast enough. Also gives it the outline |
| `destroySpeed` | 5 | The player `Speed` needed to smash it (compare to `maxSpeed`, 10 by default) |
| `scorePoints` | 10 | Base points for smashing it, before Score's combo bonus and speed multiplier. Set per prefab so each asteroid type can be worth a different amount. Indestructible asteroids smashed while invincible give theirs too |
| `template` | none | The shared SpawnableTemplate asset |

Note that `speed` here is the asteroid's own speed. It's unrelated to the player's `PlayerMovement.Speed`,
though both feed into how fast the asteroid approaches.

## How it works

### Lifecycle at a glance

1. **`Awake`**: grabs its components once and adds the outline material if it's destructible.
2. **`OnEnable`**: the spawner reveals it. It records its start point, resets its state, and starts the
   warning.
3. **`WarnThenMove`**: blinks for `warningTime` seconds with its collider off, then starts moving.
4. **`Update`**: every frame it spins, updates the outline color, and (once moving) moves toward the
   camera.
5. It **despawns** when the player **smashes** it, or when it gets far enough **behind the camera**:
   `PatternCopy.Despawn` switches it off and puts it back at its spot in its pattern copy, ready to be
   revealed again on the pattern's next use (object pooling). An asteroid that isn't in a pooled pattern is
   destroyed as before.

### `Awake`: setup

- Finds the Renderer on its child and the Collider on itself. The child mesh is what spins, so the
  collider (on the root) doesn't rotate with it.
- Gets the gameplay camera from GameManager, the player's PlayerMovement, PlayerHealth and
  PlayerInvincibility, and the level's Score. Like every script that isn't on the player, it gets player components from
  GameManager once instead of looking them up on each hit.
- Creates `blinkWait`, one `WaitForSeconds(blinkInterval)` that every blink of the warning reuses (see
  `WarnThenMove`). Only when there's a template, since that's where `blinkInterval` lives.
- **Destructible outline:** if `destructible` is on, it adds the template's `outlineMaterial` as an extra
  material slot on the renderer. A mesh with two materials gets drawn twice, and the outline shader
  draws the second copy slightly larger and inside out (an "inverted hull"), so only a rim shows around
  the edge. Doing this in code means ticking `destructible` is all it takes to get an outline.

### `OnEnable`: reset when revealed

ObjectSpawner hides every asteroid in a pattern, moves each one to the spawn point, then switches it on
with `SetActive(true)`. That fires `OnEnable`, which:

- Adds this asteroid to the static `Active` list (see below).
- Stores its current position as `origin`. All movement is measured from here.
- Resets its timers and sets `isMoving = false`.
- Picks a random `spinAxis` so every asteroid tumbles differently.
- Works out `baseAngle`, the angle it sits at around the orbit center, for Wave movement. It uses
  `Mathf.Atan2(y, x)`, the reverse of cos/sin: cos/sin take an angle and give you an x/y, and Atan2 takes
  an x/y and gives you the angle back (in radians). The orbit center comes from `OrbitSettings`.
- Starts the `WarnThenMove` coroutine.

### `WarnThenMove`: the warning blink

A **coroutine** is a method that can pause partway through and continue later. `yield return new
WaitForSeconds(x)` means "stop here and pick up again in x seconds." That makes timed sequences like a
blink easy to write as a simple loop.

1. **Turn the collider off.** The asteroid can't hit the player until it has visibly warned them.
2. **Blink.** Every `blinkInterval` seconds, flip the glow on or off until `warningTime` has passed.
   With the defaults (1s and 0.1s), that's 10 flips. Each wait yields the same `blinkWait` made in `Awake`
   instead of a `new WaitForSeconds`, so blinking creates no garbage. Reusing it is safe because it only
   holds the wait length; Unity tracks each wait's timing separately.
3. **Glow off, collider on, `isMoving = true`.**

The glow color depends on `movementType` (red for Linear, cyan for Wave by default), so the player can
tell how the asteroid will move before it does.

**How the glow works (`SetGlow`):** it uses a `MaterialPropertyBlock`, which overrides a material setting
(here, the emission color) on this one renderer only. Changing the material directly would either change
every asteroid sharing it, or make Unity quietly copy the material for each asteroid. Setting the
block to `null` removes the override. The material needs **Emission** turned on for this to show.

### `Update`: every frame

**Spin.** The mesh rotates around its random axis at `spinSpeed` degrees per second. This runs during
the warning too, so asteroids appear already tumbling.

**Outline color (`UpdateOutline`).** Destructible asteroids only. The outline is green when `CanBreak` is
true (the player is invincible, or fast enough to smash it) and red when it isn't. It only touches the
renderer when that changes, not every frame.

**Moving.** Once `isMoving` is true:

1. **How fast to approach.** `ApproachSpeed = speed + PlayerMovement.Speed × template.speedMultiplier`.
   The player never really moves forward, so asteroids coming in faster is what makes flying faster feel
   faster.
2. **How far it has come.** `distance += ApproachSpeed × Time.deltaTime`. Like `orbitAngle` in
   PlayerMovement, this is a running total built up each frame. If it were calculated fresh as
   `ApproachSpeed × total time`, any change in the player's speed would make every asteroid jump.
3. **Forward.** `position = origin + Vector3.back * distance`. Every asteroid moves toward -Z (toward the
   camera). For **Linear**, that's all it does.
4. **Wave: snap to the player's circle and swing along it.** Wave asteroids sit on the same circle as
   the player and move back and forth along it, like the player sliding around the ring. This replaces
   the x/y from step 3:
   1. **How far to swing.** `swing = sin(elapsed × frequency × 2π) × amplitude × Deg2Rad`. The part
      inside `sin()` turns time into an angle, and one full turn (2π) is one full swing, so
      `frequency = 1` means one swing per second. `sin` goes smoothly from 0 up to 1, back down to -1,
      and back to 0, so the swing goes between `+amplitude` and `-amplitude` degrees. `Deg2Rad`
      converts that to radians, because cos/sin need radians.
   2. **Add it to the starting angle.** `angle = baseAngle + swing`.
   3. **Turn the angle into a position.** `center + (cos(angle), sin(angle)) × radius`, the same as how
      PlayerMovement places the ship. The center and radius come from `OrbitSettings`, the same values the
      player uses. They're read every frame, so they follow the asset if it's tuned in Play mode.

   Because `sin(0)` is 0, the swing starts at the same angle the asteroid was placed at.
5. **Place it.** `transform.position = position`.
6. **Despawn.** Once it's more than `despawnBehindCamera` units behind the camera, it calls
   `PatternCopy.Despawn(gameObject)`. It's flying away from the player at that point, so it can never come
   back into view.

### `OnTriggerEnter`: touching the player (or a twin copy)

1. **Is it the player or a twin copy?** PlayerTwins' `FindBody(other.transform)` returns the body that was
   touched: the player, a copy, or null. It uses `IsChildOf`, so it still works if a collider is on a child.
   A hit on the copy the player jumped onto in this same frame counts as the player. Without a PlayerTwins,
   it falls back to `other.transform.IsChildOf(player.transform)`. Anything else, or no player at all, is
   ignored.
2. **Smash.** It explodes if `CanBreak` is true (see the `Active` list section below): the player is
   **invincible**, or it's `destructible` and the player's `Speed` is at least `destroySpeed`.

   Exploding plays the template's `explosionPrefab` through `EffectPool.Play` (a reused effect copy),
   shakes the camera, adds one to `RunStats.AsteroidsDestroyed` through GameManager (null-safe), adds
   `scorePoints` with `Score.AddComboKill` (null-safe; Score adds the combo bonus and applies the speed
   multiplier), and despawns the asteroid with `PatternCopy.Despawn`. Since this branch
   comes first, an invincible player gets no knockback, speed loss or lost life. Twin copies smash
   asteroids the same way.
3. **Twinned?** If PlayerTwins `IsTwinned`, it calls `LoseBody(body)`: that body blows up and one body is
   removed, with no knockback, speed loss or lost life (see PlayerTwins).
4. **Otherwise, hit the player.**
   - **Which way to push.** `OrbitTangent` is the counter-clockwise direction along the player's circle.
     The **dot product** of (player position − asteroid position) with that direction tells you whether
     the player is ahead of the asteroid (positive) or behind it (negative) along the circle. The player
     gets pushed that way, away from the asteroid. A perfect dead-center hit (exactly 0) picks a side at
     random.
   - It calls `player.TakeHit(knockback, speedLoss)`. If the player wasn't invulnerable, `TakeHit`
     returns `true`, meaning the hit counted. Only then does the camera shake and the player lose a life:
     it calls `LoseLife()` on the PlayerHealth cached in `Awake`. Both are inside braces under the `if`, so neither happens during invulnerability.

The asteroid isn't destroyed when it hits the player. It keeps flying past.

### The `Active` list

`Active` is a **static** list of every asteroid that's currently switched on. Asteroids add themselves in
`OnEnable` and remove themselves in `OnDisable`. Despawning switches the asteroid off (and `Destroy`
also triggers `OnDisable`), so a despawned asteroid always leaves the list.

OrbitRing loops over this list to draw danger arcs on the player's ring, without having to search the
scene for asteroids every frame. The public properties exist for it:

| Property | What it tells you |
| --- | --- |
| `IsMoving` | Whether it has finished warning and started moving |
| `ApproachSpeed` | How fast it's closing in right now |
| `CanBreak` | Whether touching it right now would smash it |
| `HitRadius` | How wide its collider is on screen (0 while the collider is off during the warning) |

`CanBreak` is the one rule for "would this break?": `(invincible) || (destructible && Speed >= destroySpeed)`.
The outline color, OrbitRing's arc color and `OnTriggerEnter` all use it, so what the player sees always
matches what happens. "Invincible" comes from the PlayerInvincibility reference each asteroid grabs from
GameManager once in `Awake`, so checking it costs no searching. If there's no GameManager or no
PlayerInvincibility, that part is just false.

## Talks to

- **PlayerMovement**: reads the static `Speed`. On a hit, reads `OrbitTangent` and calls `TakeHit`.
- **OrbitSettings**: reads the orbit center (in `OnEnable`) and the center and radius (every frame, Wave only).
- **SpawnableTemplate**: reads almost all of its shared settings from it, and `GetGlow` for the warning color.
- **CameraShake**: calls `Shake` on a smash, and on a hit that `TakeHit` accepted.
- **PlayerHealth**: calls `LoseLife()` on a hit that `TakeHit` accepted.
- **PlayerInvincibility**: reads `IsInvincible` in `CanBreak`. While it's true, every asteroid's outline and
  arc are green and touching it explodes it.
- **PlayerTwins**: `FindBody` on every contact; `LoseBody` on a hit during the twin; reads `IsTwinned`.
- **GameManager**: gets the gameplay camera, `Player`, `PlayerHealth`, `PlayerInvincibility`, `PlayerTwins` and `Score` in `Awake`, and `RunStats` when an
  asteroid is smashed.
- **RunStats**: calls `AddAsteroidDestroyed()` on a smash.
- **Score**: calls `AddComboKill(scorePoints)` on a smash.
- **ObjectSpawner**: reveals asteroids (triggering `OnEnable`) and reads `ApproachSpeed` to time the
  gaps between them.
- **OrbitRing**: reads the `Active` list and the properties above.
- **PatternCopy**: `Despawn` takes the asteroid back into its pattern copy instead of destroying it.
- **EffectPool**: `Play` shows the explosion from a pool of reused effect copies.

## Gotchas

- **No template means a broken asteroid.** Without one, there's no warning (and the collider stays on the
  whole time), no spin, no knockback or camera shake when it hits the player, it ignores the player's
  speed, and it never despawns.
- **`amplitude` is in degrees, the rest of the angles in the script are radians.** It's converted with
  `Deg2Rad` right where it's used.
- **Wave speed is based on time, not distance.** At higher player speed, the asteroid covers more
  distance per swing, so its wave looks stretched out along the Z axis.
- **`destructible` is only read in `Awake`.** Ticking it on a live asteroid in Play mode lets it be smashed,
  but it won't get an outline.
- **`blinkInterval` is only read into `blinkWait` in `Awake`.** Changing it on the template in Play mode
  won't change how long existing asteroids wait between blinks, though their loop already steps by the new
  value, so the number of blinks changes. Stop and press Play again after tuning it.
- **The glow needs Emission turned on** in the asteroid's material, or the blink is invisible.
- **`OnEnable` runs every time it's revealed, and the asteroid is reused.** Creating a pattern copy switches
  its children on once (running `Awake` and `OnEnable`) before the spawner switches them off, and with
  pooling the same asteroid is revealed again on every later use of its pattern. So `OnEnable` has to reset
  everything, which it does. Any new state added to the script should be reset there too. `Awake` runs only
  once per copy, so per-copy setup like the outline material is done once.
- **Never `Destroy` it in code; call `PatternCopy.Despawn`.** Destroying a pooled asteroid leaves a hole in
  its pattern copy.
- **Smashing works while invulnerable.** The smash check happens before `TakeHit`, so it ignores the
  player's invulnerability window. Smashing never costs a life.
- **Non-destructible asteroids have no outline.** While the player is invincible, their arcs turn green,
  but there's no outline to change, since only destructible asteroids get the outline material.
- **PlayerHealth comes from GameManager.** If GameManager has none (not assigned and not on the player
  object), hits still push and shake but never cost a life, with no error.
- **No GameManager or no `player` on it means no contact at all.** The player check needs the cached
  player, so without one the asteroid flies through the ship without hitting or breaking.
- **No template means no lost lives either.** The hit branch is skipped entirely without a template, so
  `TakeHit` and `LoseLife` are never called.
- **An overlapping asteroid won't hit twice.** `OnTriggerEnter` only fires on first contact, so an asteroid
  the player is still inside won't hit again when invulnerability ends.
