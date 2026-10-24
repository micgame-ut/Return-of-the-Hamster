# AsteroidBehavior

## Purpose

Controls a single asteroid from the moment it appears until it's gone. It waits out a short warning, then flies
toward the camera, either in a straight line or swinging back and forth around the player's orbit (a
wave). When it touches the player, it
either breaks (if the player is fast enough) or knocks the player back.

Each asteroid has a **movement type** (how it travels) and, separately, an **effect type** (extra behavior on
top), so any movement can have any effect. The one effect so far is **Phase**: the asteroid phases in and out,
and while phased its collider is off and it shows a hologram look.

## Where it lives

- **Attached to:** the root of each asteroid prefab (in `Assets/Prefabs/Asteroid Types/`).
  Asteroids are placed as children of pattern prefabs, which ObjectSpawner reveals one at a time.
- **Requires:**
  - A **Renderer on a child object** (the mesh). That child is what spins and what the depth tint is applied to.
  - A **Collider set to Is Trigger** on the root. The player has the Rigidbody, which Unity needs on at
    least one side for trigger events to fire.
  - An **SpawnableTemplate** asset assigned to `template`. Most of the tuning lives there (see Gotchas
    for what breaks without it).
  - `GameManager.Instance` for the gameplay camera, used to despawn asteroids once they're behind it.
  - The `OrbitSettings` asset, for the orbit that Wave asteroids swing along.

## Inspector settings

These are per-asteroid. Settings shared by every asteroid (warning time, depth fade, spin, knockback,
explosion, etc.) are on the SpawnableTemplate asset instead.

| Field | Default | What it changes |
| --- | --- | --- |
| `movementType` | Linear | The path it takes: Linear or Wave |
| `amplitude` | 30 | Wave: how many degrees it swings each way around the player's circle. Unused by Linear |
| `frequency` | 1 | Wave: full back-and-forth swings per second. Unused by Linear |
| `effectType` | None | Extra behavior on top of the movement: None or Phase. A separate dropdown from `movementType` |
| `solidTime` | 1.5 | Phase: seconds solid in each cycle |
| `phasedTime` | 1 | Phase: seconds phased (collider off, hologram look) in each cycle |
| `flickerTime` | 0.3 | Phase: seconds of flicker just before each switch, as a warning |
| `flickerInterval` | 0.05 | Phase: seconds between flicker flips. 0 = no flicker |
| `destructible` | false | Whether the player can smash it by being fast enough. Also gives it the outline |
| `destroySpeed` | 5 | The player `Speed` needed to smash it (compare to `maxSpeed`, 10 by default) |
| `scorePoints` | 10 | Base points for smashing it, before Score's combo bonus and speed multiplier. Set per prefab so each asteroid type can be worth a different amount. Indestructible asteroids smashed while invincible give theirs too |
| `template` | none | The shared SpawnableTemplate asset |

The base approach speed is no longer set per asteroid. It's `baseApproachSpeed` on the SpawnableTemplate, so
every asteroid and power-up shares it. It's unrelated to the player's `PlayerMovement.Speed`, though both
feed into how fast the asteroid approaches.

## How it works

### Lifecycle at a glance

1. **`Awake`**: grabs its components once and adds the outline material if it's destructible.
2. **`OnEnable`**: the spawner reveals it. It records its start point, resets its state, and starts the
   warning.
3. **`WarnThenMove`**: waits `warningTime` seconds with its collider off, then starts moving.
4. **`Update`**: every frame it spins, updates the outline color and the depth tint, and (once moving)
   moves toward the camera.
5. It **despawns** when the player **smashes** it, or when it gets far enough **behind the camera**:
   `PatternCopy.Despawn` switches it off and puts it back at its spot in its pattern copy, ready to be
   revealed again on the pattern's next use (object pooling). An asteroid that isn't in a pooled pattern is
   destroyed as before.

### `Awake`: setup

- Finds the Renderer on its child and the Collider on itself. The child mesh is what spins, so the
  collider (on the root) doesn't rotate with it.
- Gets the gameplay camera from GameManager, the player's PlayerMovement, PlayerHealth and
  PlayerInvincibility, the level's Score, and the player's CloseCallZone. Like every script that isn't on the player, it gets player components from
  GameManager once instead of looking them up on each hit.
- Creates `warnWait`, one `WaitForSeconds(warningTime)` that every warning reuses (see `WarnThenMove`).
  Only when there's a template, since that's where `warningTime` lives.
- **Destructible outline:** if `destructible` is on, it adds the template's `outlineMaterial` as an extra
  material slot on the renderer. A mesh with two materials gets drawn twice, and the outline shader
  draws the second copy slightly larger and inside out (an "inverted hull"), so only a rim shows around
  the edge. Doing this in code means ticking `destructible` is all it takes to get an outline.
- **`hitRadius`:** the collider's on-screen footprint (`Max(bounds.extents.x, bounds.extents.y)`), measured
  once here while the collider is still on. The collider is on the root, which never spins, so its size never
  changes. `HitRadius` returns this, so it stays right while the collider is off (warning, phased).
- **The two looks for Phase:** `solidMaterials` is the renderer's materials after the outline is added, and
  `phasedMaterials` is just the template's `phasedMaterial` (only for a Phase asteroid with one assigned;
  otherwise null, and the look never swaps). They're made once so swapping costs no allocation.
  `rend.sharedMaterials` returns a new copy of the array each time it's read, which is why it's read once.
- **The depth tint's setup:** creates `tintBlock` (a `MaterialPropertyBlock`) and fills `baseColors` with the
  `_BaseColor` of every material slot **except the outline's** (white for a material without that property).
  The outline is always the last slot, so the slots before `outlineIndex` are the body. See `UpdateDepthTint`.

### `OnEnable`: reset when revealed

ObjectSpawner hides every asteroid in a pattern, moves each one to the spawn point, then switches it on
with `SetActive(true)`. That fires `OnEnable`, which:

- Adds this asteroid to the static `Active` list (see below).
- Stores its current position as `origin`. All movement is measured from here.
- Resets its timers and sets `isMoving = false`.
- Resets the close-call state (`passedPlayer`, `closeCallJudged`, `hitPlayer`). Asteroids are pooled, so
  each reveal is a fresh flight that gets its own close-call check.
- Resets the phase: `IsPhased = false` and `SetLook(false)` (solid materials), in case it was despawned while
  phased. The collider needs no reset: `WarnThenMove` switches it off and back on.
- Picks a random `spinAxis` so every asteroid tumbles differently.
- Works out `baseAngle`, the angle it sits at around the orbit center, for Wave movement. It uses
  `Mathf.Atan2(y, x)`, the reverse of cos/sin: cos/sin take an angle and give you an x/y, and Atan2 takes
  an x/y and gives you the angle back (in radians). The orbit center comes from `OrbitSettings`.
- Starts the `WarnThenMove` coroutine.

### `WarnThenMove`: the warning wait

A **coroutine** is a method that can pause partway through and continue later. `yield return new
WaitForSeconds(x)` means "stop here and pick up again in x seconds." That makes timed sequences easy to
write as plain code.

1. **Turn the collider off.** The asteroid can't hit the player while it's still warning.
2. **Wait `warningTime` seconds.** It yields the same `warnWait` made in `Awake` instead of a `new
   WaitForSeconds`, so each warning creates no garbage. Reusing it is safe because it only holds the wait
   length; Unity tracks each wait's timing separately.
3. **Collider on, `isMoving = true`.**

The warning used to blink a red or cyan glow. That was removed (along with the template's glow fields and
`GetGlow`), so the warning is now just the wait: the asteroid sits at the far end of the field, dark from the
depth tint, tumbling.

### `Update`: every frame

**Spin.** The mesh rotates around its random axis at `spinSpeed` degrees per second. This runs during
the warning too, so asteroids appear already tumbling.

**Outline color (`UpdateOutline`).** Destructible asteroids only. The outline is green when `CanBreak` is
true (the player is invincible, or fast enough to smash it) and red when it isn't. It only touches the
renderer when that changes, not every frame. It's skipped while the hologram look is showing
(`showingPhased`), which has no outline slot; when the solid look comes back, `outlineState = -1` makes it
re-apply the color.

**Depth tint (`UpdateDepthTint`).** Makes asteroids darker the further they are from the player, so depth is
easier to read. It runs every frame, including during the warning, so a new asteroid shows dark at once:

1. `distance = its z − the player's z`.
2. `template.GetDepthTint(distance)` turns that into a color between white (near the player) and
   `farColor` (far away). The distance is measured as a share of `OrbitSettings.SpawnDistance`, so it follows
   `spawnerPosition` and `playerZ` if they change.
3. If that tint is the same as last frame's (`lastTint`), it stops. Otherwise it fills `tintBlock` for each body
   slot with `_BaseColor` = that slot's saved color × the tint, plus `_Tint` = the tint (for the Asteroid
   Phase graph), and applies it to just that slot with `SetPropertyBlock(tintBlock, i)`.

A property block replaces a value, it doesn't multiply it, which is why `baseColors` saves each slot's own
base color in `Awake`. The **outline slot is never touched**, so the outline stays at full brightness.
`SetPropertyBlock` is per slot, so the same tint carries over when the phase look swaps the materials. A
passed asteroid has a negative distance, which clamps to white.

**Phase (`UpdatePhase`).** For a Phase asteroid, once moving, right after `elapsed` is advanced:

1. `cycle = solidTime + phasedTime`, and `t = elapsed % cycle` (the remainder, so it repeats).
2. **Real state:** `IsPhased = t >= solidTime`. The **collider** follows it exactly (off while phased),
   touched only when it needs to change.
3. **Look:** `untilSwitch` is the time to the next switch. In the last `flickerTime`, `untilSwitch ÷
   flickerInterval` (rounded down) counts down in steps, and on every even step the look shows the
   **opposite** of the real state. Otherwise the look matches the real state. The collider doesn't flicker.
4. `SetLook` assigns `phasedMaterials` or `solidMaterials`, only on frames the look changes.

`elapsed` only runs while moving, so every Phase asteroid starts solid when it starts moving, and a wave
revealed together phases in sync. With 1.5 / 1 / 0.3 / 0.05 (cycle 2.5 s):

| `elapsed` | `t` | Real state (collider) | Look |
| --- | --- | --- | --- |
| 0.5 s | 0.5 | solid (on) | solid |
| 1.25 s | 1.25 | solid (on) | flickering, 0.25 s before the switch |
| 1.6 s | 1.6 | phased (off) | hologram |
| 2.3 s | 2.3 | phased (off) | flickering |
| 2.6 s | 0.1 | solid (on) | solid, next cycle |

If it turns solid while overlapping the player, that's a normal hit; the flicker is the warning.

**Moving.** Once `isMoving` is true:

1. **How fast to approach.** `ApproachSpeed = template.baseApproachSpeed + PlayerMovement.Speed × template.speedMultiplier`.
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
5. **Place it.** `transform.position = position`. Then, until the close call has been judged, it calls
   `TrackCloseCall` (see below).
6. **Despawn.** Once it's more than `despawnBehindCamera` units behind the camera, it calls
   `PatternCopy.Despawn(gameObject)`. It's flying away from the player at that point, so it can never come
   back into view.

### `TrackCloseCall`: near misses

A close call is a threatening asteroid that passes within CloseCallZone's `Distance` of a body's hitbox edge
without touching it. `depth` is how far in front of the player the asteroid is (positive in front, since
asteroids fly toward −Z).

1. **Measure once, on the frame it crosses the player's depth** (`depth` reaches 0 or less, `passedPlayer`
   not yet set): store the gap from `CloseCallZone.NearestGap`, the nearest body's position, and whether it
   was a threat (`!CanBreak && !IsPhased`, so invincibility, breakable-at-this-speed asteroids, and phased
   asteroids that couldn't have hurt the player don't count).
2. **Wait** until it's more than its `HitRadius` + the player's `Reach` behind the player. Before that it could
   still touch, and a physics hit can be reported a frame after the crossing.
3. **Judge once** (`closeCallJudged`): if it never touched a body (`hitPlayer`), was a threat, and the gap is
   above 0 and at most `Distance`, it calls `Score.AddCloseCall(body position)`.

Measuring at the crossing, rather than every frame while it's level with the player, matters at high speed.
At max speed an asteroid moves about 2.8 units a frame, so it could be level with the player for one frame
or none, but the crossing frame always exists. A gap of 0 or less means the hitboxes overlapped, so it isn't
rewarded even if physics missed the contact.

### `OnTriggerEnter`: touching the player (or a twin copy)

1. **Is it the player or a twin copy?** PlayerTwins' `FindBody(other.transform)` returns the body that was
   touched: the player, a copy, or null. It uses `IsChildOf`, so it still works if a collider is on a child.
   A hit on the copy the player jumped onto in this same frame counts as the player. Without a PlayerTwins,
   it falls back to `other.transform.IsChildOf(player.transform)`. Anything else, or no player at all, is
   ignored. Any touch sets `hitPlayer`, so this flight can't score a close call.
2. **Smash.** It explodes if `CanBreak` is true (see the `Active` list section below): the player is
   **invincible**, or it's `destructible` and the player's `Speed` is at least `destroySpeed`.

   Exploding plays the template's `explosionPrefab` through `EffectPool.Play` (a reused effect copy),
   shakes the camera, adds one to `RunStats.AsteroidsDestroyed` through GameManager (null-safe), adds
   `scorePoints` with `Score.AddComboKill` (null-safe; Score adds the combo bonus and applies the speed
   multiplier), and despawns the asteroid with `PatternCopy.Despawn`. `Explode` takes the touching body's
   position and passes it to Score, so the score popup appears above the body that smashed it (the player
   or a twin copy), not above the asteroid. Since this branch
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
| `Movement` | Its `MovementType` (Linear or Wave). Read-only. The pattern builder's fairness checks read it to skip Wave asteroids |
| `MirrorSwing` | Settable. The spawner sets it when it flips a pattern left-right; a Wave asteroid then swings the other way (the swing is multiplied by -1). Not saved in the Inspector or prefab, starts false. Linear asteroids ignore it |
| `ApproachSpeed` | How fast it's closing in right now |
| `CanBreak` | Whether touching it right now would smash it |
| `HitRadius` | How wide its collider is on screen, measured once in `Awake`, so it's right even while the collider is off |
| `IsPhased` | Whether a Phase asteroid is phased out right now (collider off). OrbitRing draws its arc blue then |

`CanBreak` is the one rule for "would this break?": `!IsPhased && ((invincible) || (destructible && Speed >=
destroySpeed))`. A phased asteroid can never be broken.
The outline color, OrbitRing's arc color and `OnTriggerEnter` all use it, so what the player sees always
matches what happens. "Invincible" comes from the PlayerInvincibility reference each asteroid grabs from
GameManager once in `Awake`, so checking it costs no searching. If there's no GameManager or no
PlayerInvincibility, that part is just false.

## Talks to

- **PlayerMovement**: reads the static `Speed`. On a hit, reads `OrbitTangent` and calls `TakeHit`.
- **OrbitSettings**: reads the orbit center (in `OnEnable`) and the center and radius (every frame, Wave only).
- **SpawnableTemplate**: reads almost all of its shared settings from it, and `GetDepthTint` for the depth fade.
  A Phase asteroid gets its hologram look from `phasedMaterial` (the Asteroid Phase shader, see
  [AsteroidPhase.md](AsteroidPhase.md)).
- **CameraShake**: calls `Shake` on a smash, and on a hit that `TakeHit` accepted.
- **PlayerHealth**: calls `LoseLife()` on a hit that `TakeHit` accepted.
- **PlayerInvincibility**: reads `IsInvincible` in `CanBreak`. While it's true, every asteroid's outline and
  arc are green and touching it explodes it.
- **PlayerTwins**: `FindBody` on every contact; `LoseBody` on a hit during the twin; reads `IsTwinned`.
- **GameManager**: gets the gameplay camera, `Player`, `PlayerHealth`, `PlayerInvincibility`, `PlayerTwins` and `Score` in `Awake`, and `RunStats` when an
  asteroid is smashed.
- **RunStats**: calls `AddAsteroidDestroyed()` on a smash.
- **Score**: calls `AddComboKill(scorePoints, bodyPosition)` on a smash, and `AddCloseCall(body position)` on a
  close call.
- **CloseCallZone**: calls `NearestGap` when it crosses the player's depth; reads `Reach` and `Distance` to
  judge the close call.
- **ObjectSpawner**: reveals asteroids (triggering `OnEnable`) and reads `ApproachSpeed` to time the
  gaps between them.
- **OrbitRing**: reads the `Active` list and the properties above.
- **PatternCopy**: `Despawn` takes the asteroid back into its pattern copy instead of destroying it.
- **EffectPool**: `Play` shows the explosion from a pool of reused effect copies.

## Gotchas

- **No template means a broken asteroid.** Without one, there's no warning (and the collider stays on the
  whole time), no spin, no knockback or camera shake when it hits the player, its `ApproachSpeed` is 0 so
  it never moves, and it never despawns.
- **`amplitude` is in degrees, the rest of the angles in the script are radians.** It's converted with
  `Deg2Rad` right where it's used.
- **Wave speed is based on time, not distance.** At higher player speed, the asteroid covers more
  distance per swing, so its wave looks stretched out along the Z axis.
- **`effectType` is only read in `Awake` for the materials.** Switching a live asteroid to Phase in Play mode
  makes it phase, but without the hologram look until the next Play.
- **A Phase asteroid needs the template's `phasedMaterial`** for its look. Without it, it still phases (the
  collider switches) but never looks different, which is unfair to the player.
- **`phasedTime` 0** means it never phases, but it still flickers near the end of each cycle.
- **`destructible` is only read in `Awake`.** Ticking it on a live asteroid in Play mode lets it be smashed,
  but it won't get an outline.
- **`warningTime` is only read into `warnWait` in `Awake`.** Changing it on the template in Play mode won't
  change how long existing asteroids wait. Stop and press Play again after tuning it.
- **The depth tint needs `_BaseColor`.** It works on URP Lit materials (the asteroid material). A body
  material without a `_BaseColor` property is multiplied from white instead, and shows no tint unless it
  has its own tint property, like the Asteroid Phase graph's `Tint`.
- **Slots added after `Awake` aren't tinted.** `baseColors` is filled once, so a body material slot added later
  by another script keeps its own color.
- **The tint ignores emission.** A body material with a strong emission color keeps glowing at full brightness
  at a distance.
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
