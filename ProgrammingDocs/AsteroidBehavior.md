# AsteroidBehavior

## Purpose

Controls a single asteroid from the moment it appears until it's gone. It blinks a warning glow, then flies
toward the camera, either in a straight line or swinging back and forth around the player's orbit (a
wave). When it touches the player, it
either breaks (if the player is fast enough) or knocks the player back.

## Where it lives

- **Attached to:** the root of each asteroid prefab (in `Assets/Prefabs/Asteroid Types/`).
  Asteroids are placed as children of pattern prefabs, which AsteroidSpawner reveals one at a time.
- **Requires:**
  - A **Renderer on a child object** (the mesh). That child is what spins and glows.
  - A **Collider set to Is Trigger** on the root. The player has the Rigidbody, which Unity needs on at
    least one side for trigger events to fire.
  - An **AsteroidTemplate** asset assigned to `template`. Most of the tuning lives there (see Gotchas
    for what breaks without it).
  - `GameManager.Instance` for the gameplay camera, used to despawn asteroids once they're behind it.
  - The `OrbitSettings` asset, for the orbit that Wave asteroids swing along.

## Inspector settings

These are per-asteroid. Settings shared by every asteroid (glow colors, warning time, spin, knockback,
explosion, etc.) are on the AsteroidTemplate asset instead.

| Field | Default | What it changes |
| --- | --- | --- |
| `movementType` | Linear | The path it takes: Linear or Wave. Warning glow is tied to the type |
| `speed` | 20 | Base approach speed in units/sec, before the player's `Speed` is added on |
| `amplitude` | 30 | Wave: how many degrees it swings each way around the player's circle. Unused by Linear |
| `frequency` | 1 | Wave: full back-and-forth swings per second. Unused by Linear |
| `destructible` | false | Whether the player can smash it by being fast enough. Also gives it the outline |
| `destroySpeed` | 5 | The player `Speed` needed to smash it (compare to `maxSpeed`, 10 by default) |
| `template` | none | The shared AsteroidTemplate asset |

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
5. It's destroyed when the player **smashes** it, or when it gets far enough **behind the camera**.

### `Awake`: setup

- Finds the Renderer on its child and the Collider on itself. The child mesh is what spins, so the
  collider (on the root) doesn't rotate with it.
- Gets the gameplay camera from GameManager.
- **Destructible outline:** if `destructible` is on, it adds the template's `outlineMaterial` as an extra
  material slot on the renderer. A mesh with two materials gets drawn twice, and the outline shader
  draws the second copy slightly larger and inside out (an "inverted hull"), so only a rim shows around
  the edge. Doing this in code means ticking `destructible` is all it takes to get an outline.

### `OnEnable`: reset when revealed

AsteroidSpawner hides every asteroid in a pattern, moves each one to the spawn point, then switches it on
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
   With the defaults (1s and 0.1s), that's 10 flips.
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

**Outline color (`UpdateOutline`).** Destructible asteroids only. The outline is green when the player is
fast enough to smash it and red when they aren't. It only touches the renderer when that changes, not
every frame.

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
6. **Despawn.** Once it's more than `despawnBehindCamera` units behind the camera, it's destroyed. It's
   flying away from the player at that point, so it can never come back into view.

### `OnTriggerEnter`: touching the player

1. **Is it the player?** It looks for PlayerMovement on the object it touched or any of its parents
   (`GetComponentInParent`), so it still works if the player's collider is on a child. Anything else is
   ignored.
2. **Smash.** If it's `destructible` and the player's `Speed` is at least `destroySpeed`, it explodes:
   it spawns the template's `explosionPrefab`, shakes the camera, and destroys itself.
3. **Otherwise, hit the player.**
   - **Which way to push.** `OrbitTangent` is the counter-clockwise direction along the player's circle.
     The **dot product** of (player position − asteroid position) with that direction tells you whether
     the player is ahead of the asteroid (positive) or behind it (negative) along the circle. The player
     gets pushed that way, away from the asteroid. A perfect dead-center hit (exactly 0) picks a side at
     random.
   - It calls `player.TakeHit(knockback, speedLoss)`. If the player wasn't invulnerable, `TakeHit`
     returns `true` and the camera shakes.

The asteroid isn't destroyed when it hits the player. It keeps flying past.

### The `Active` list

`Active` is a **static** list of every asteroid that's currently switched on. Asteroids add themselves in
`OnEnable` and remove themselves in `OnDisable`. `Destroy` also triggers `OnDisable`, so destroyed
asteroids are removed too.

OrbitRing loops over this list to draw danger arcs on the player's ring, without having to search the
scene for asteroids every frame. The public properties exist for it:

| Property | What it tells you |
| --- | --- |
| `IsMoving` | Whether it has finished warning and started moving |
| `ApproachSpeed` | How fast it's closing in right now |
| `CanBreak` | Whether the player could smash it at their current speed |
| `HitRadius` | How wide its collider is on screen (0 while the collider is off during the warning) |

## Talks to

- **PlayerMovement**: reads the static `Speed`. On a hit, reads `OrbitTangent` and calls `TakeHit`.
- **OrbitSettings**: reads the orbit center (in `OnEnable`) and the center and radius (every frame, Wave only).
- **AsteroidTemplate**: reads almost all of its shared settings from it, and `GetGlow` for the warning color.
- **CameraShake**: calls `Shake` on a smash, and on a hit that `TakeHit` accepted.
- **GameManager**: gets the gameplay camera in `Awake`.
- **AsteroidSpawner**: reveals asteroids (triggering `OnEnable`) and reads `ApproachSpeed` to time the
  gaps between them.
- **OrbitRing**: reads the `Active` list and the properties above.

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
- **The glow needs Emission turned on** in the asteroid's material, or the blink is invisible.
- **`OnEnable` runs twice per asteroid.** Instantiating a pattern switches its children on (running
  `Awake` and `OnEnable`), then the spawner immediately switches them off again. Because of that,
  `OnEnable` has to reset everything, which it does. Any new state added to the script should be reset there too.
- **Smashing works while invulnerable.** The smash check happens before `TakeHit`, so it ignores the
  player's invulnerability window.
- **An overlapping asteroid won't hit twice.** `OnTriggerEnter` only fires on first contact, so an asteroid
  the player is still inside won't hit again when invulnerability ends.
