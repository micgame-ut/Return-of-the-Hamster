# AsteroidBehavior

## Purpose

Controls a single asteroid from the moment it appears until it's gone. It waits out a short warning, then flies
toward the camera, either in a straight line or swinging back and forth around the player's orbit (a
wave). When it touches the player, it
either breaks (if the player is fast enough) or knocks the player back.

Each asteroid has a **movement type** (how it travels) and, separately, an **effect type** (extra behavior on
top), so any movement can have any effect. The effects are **Phase** (the asteroid phases in and out, and while
phased its collider is off and it shows a hologram look), **Splitter**, **Growing**, **Magnet**, and **Frost** (a
comet that leaves slowing patches on the player's orbit ring).

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
| `kind` | Hazard | What it is: Hazard (hurts, only breakable by invincibility), or Destructible and BonusCarrier (smashable by speed, with the outline). BonusCarrier also gives bonus points and can have its own explosion when smashed (see `bonusPoints` and `bonusExplosion`). (A Nebula kind existed briefly and was removed: the Frost effect took its place) |
| `movementType` | Linear | The path it takes: Linear, Wave, Spiral, Homing or StopAndGo |
| `approachScale` | 1 | Multiplies this asteroid's approach speed (1 = normal, 0.3 = a slow drifter). It's folded into `ApproachSpeed`, so ObjectSpawner's reveal timing and OrbitRing see the scaled speed. The pattern builder's Timing panel doesn't (it uses the template) |
| `amplitude` | 30 | Wave: how many degrees it swings each way around the player's circle. Unused by Linear |
| `frequency` | 1 | Wave: full back-and-forth swings per second. Unused by Linear |
| `spiralSpeed` | 45 | Spiral: degrees per second it drifts round the orbit, at its own radius. Negative = the other way. Unused by Linear and Wave |
| `homingSpeed` | 40 | Homing: the fastest it can turn toward the nearest player body, in degrees per second. Unused by the other movements |
| `homingStopDistance` | 8 | Homing: how close to the player's depth it stops turning and flies straight, in units. 0 = never locks |
| `homingGapScale` | 1 | Homing: the clear space it keeps from other asteroids, as a fraction of a collider's width. 1 = a gap as wide as a collider, 0 = it only stops them touching |
| `stopDistance` | 30 | StopAndGo: how far ahead of the player's depth it ends up stopped, in units |
| `stopEaseDistance` | 10 | StopAndGo: it starts slowing this many units before `stopDistance`. 0 = an abrupt stop |
| `stopTime` | 0.75 | StopAndGo: seconds it hangs still before the dash, including the shake at the end |
| `shakeTime` | 0.3 | StopAndGo: seconds at the end of the stop when the mesh shakes, as a warning |
| `shakeMagnitude` | 0.15 | StopAndGo: how far the shake moves the mesh, in units |
| `dashMultiplier` | 2.5 | StopAndGo: the dash speed as a multiple of the normal approach speed |
| `effectType` | None | Extra behavior on top of the movement: None, Phase, Splitter, Growing, Magnet or Frost. A separate dropdown from `movementType` |
| `splitDistance` | 40 | Splitter: how far ahead of the player's depth it splits, in units |
| `splitCount` | 3 | Splitter: how many pieces |
| `splitSpread` | 20 | Splitter: degrees between neighbouring pieces round the orbit |
| `splitPiece` | none | Splitter: the asteroid prefab each piece is made from (a Linear one). Its `meshes` list gives each piece a random mesh. With none set, the Splitter does nothing |
| `pieceScale` | 0.5 | Splitter: how much smaller than the original each piece is |
| `magnetRadius` | 30 | Magnet: how close the player has to be to feel the pull, in units (3D, including depth) |
| `magnetStrength` | 60 | Magnet: the pull on the player at point-blank range, in degrees per second along the orbit |
| `dropSpacing` | 3 | Frost: how far the comet has to move round the orbit ring before it leaves another patch, in units. At or below twice `patchRadius` the patches overlap into a strip; above that they are separate spots |
| `paintDistance` | 20 | Frost: it only leaves patches while it is within this distance of the player's depth, in units. A small value means only around the moment it crosses |
| `patchLifetime` | 6 | Frost: seconds a patch lasts before it disappears. The ribbon and the mist are set to last this long too |
| `patchRadius` | 3 | Frost: how close the player has to be to a patch's centre to be slowed, in units. Patches sit on the orbit ring |
| `speedSlow` | 0.5 | Frost: the fraction of the player's forward `Speed` lost per second inside a patch (slider 0–1). 1 = all of it each second |
| `orbitSlow` | 0.5 | Frost: the fraction of the player's sideways movement along the orbit lost inside a patch (slider 0–1). 1 = frozen in place |
| `trail` | none | Frost: the particle system that draws the mist, a child of the comet. Its Start Lifetime is set to `patchLifetime` when the comet spawns. Child systems of it are stopped with it, but their lifetime isn't set. Empty = no mist |
| `ribbon` | none | Frost: the Trail Renderer that draws the ribbon, a child of the comet. Its Time is set to `patchLifetime` when the comet spawns. Empty = no ribbon |
| `growDistance` | 30 | Growing: how far ahead of the player's depth it pops to its full size, in units |
| `growScale` | 2 | Growing: its final size, as a multiple of its starting size |
| `pulseDistance` | 10 | Growing: it starts pulsing this many units before it pops, as a warning |
| `pulseSize` | 0.1 | Growing: how much the mesh swells and shrinks while pulsing, as a fraction of its size |
| `pulseRate` | 6 | Growing: pulses per second |
| `overshoot` | 0.25 | Growing: how far past its final size the pop goes, as a fraction (the mesh only) |
| `settleTime` | 0.2 | Growing: seconds the overshoot takes to ease back down to the final size |
| `solidTime` | 1.5 | Phase: seconds solid in each cycle |
| `phasedTime` | 1 | Phase: seconds phased (collider off, hologram look) in each cycle |
| `flickerTime` | 0.3 | Phase: seconds of flicker just before each switch, as a warning |
| `flickerInterval` | 0.05 | Phase: seconds between flicker flips. 0 = no flicker |
| `destroySpeed` | 5 | Destructible and BonusCarrier: the player `Speed` needed to smash it (compare to `maxSpeed`, 10 by default) |
| `scorePoints` | 10 | Base points for smashing it, before Score's combo bonus and speed multiplier. Set per prefab so each asteroid type can be worth a different amount. Indestructible asteroids smashed while invincible give theirs too |
| `bonusPoints` | 200 | BonusCarrier: flat extra points for smashing it, on top of `scorePoints`. Not multiplied by speed or power-ups, and it doesn't affect the combo. 0 = no bonus pop-up |
| `bonusLabel` | BONUS | BonusCarrier: the pop-up text shown with the bonus points |
| `bonusExplosion` | none | BonusCarrier: its own explosion. Empty uses the template's normal explosion |
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
   `Finish` calls `PatternCopy.Despawn`, which switches it off and puts it back at its spot in its pattern copy,
   ready to be revealed again on the pattern's next use (object pooling). An asteroid that isn't in a pooled
   pattern is destroyed as before. The one exception is a Frost comet that still has patches: it **retires**
   instead and despawns when its last patch is gone (see Frost below).

### `Awake`: setup

- Finds the Renderer on its child and the Collider on itself. The child mesh is what spins, so the
  collider (on the root) doesn't rotate with it.
- Gets the gameplay camera from GameManager, the player's PlayerMovement, PlayerHealth and
  PlayerInvincibility, the level's Score, and the player's CloseCallZone. Like every script that isn't on the player, it gets player components from
  GameManager once instead of looking them up on each hit.
- Creates `warnWait`, one `WaitForSeconds(warningTime)` that every warning reuses (see `WarnThenMove`).
  Only when there's a template, since that's where `warningTime` lives.
- **Destructible outline:** if the asteroid `BreaksBySpeed` (its `kind` is Destructible or BonusCarrier), it adds
  the template's `outlineMaterial` as an extra material slot on the renderer. A mesh with two materials gets
  drawn twice, and the outline shader draws the second copy slightly larger and inside out (an "inverted
  hull"), so only a rim shows around the edge. Doing this in code means choosing the `kind` is all it takes to
  get an outline.
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
- Works out `baseAngle`, the angle it sits at around the orbit center, for Wave, Spiral and Homing movement. It
  uses `Mathf.Atan2(y, x)`, the reverse of cos/sin: cos/sin take an angle and give you an x/y, and Atan2 takes
  an x/y and gives you the angle back (in radians). The orbit center comes from `OrbitSettings`.
- Records `startRadius`, its distance from the orbit center (the length of that same vector), for Spiral and
  Homing. Resets `homingAngle` to `baseAngle` and `homingLocked` to false for a Homing asteroid.
  (`WouldCrowd` reads the other asteroids from the static `Active` list, so it needs no setup.)
- Resets the Frost state: clears the `patches` list, sets `hasDropped` and `retired` to false, and switches the
  renderer back on (a retired comet hid it). For a Frost comet it then sets the `ribbon`'s `time` and the `trail`
  particle system's `main.startLifetime` to `patchLifetime`, so both last as long as a patch.
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

1. **How fast to approach.** `ApproachSpeed = (template.baseApproachSpeed + PlayerMovement.Speed × template.speedMultiplier) × approachScale`.
   The player never really moves forward, so asteroids coming in faster is what makes flying faster feel
   faster.
2. **How far it has come.** `distance += ApproachSpeed × Time.deltaTime × speedFactor`. `speedFactor` is 1 for
   every movement except StopAndGo (see below). Like `orbitAngle` in
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

   **Spiral: drift round the orbit at its own radius.** Also replaces the x/y from step 3, but it never swings
   back. `angle = baseAngle + elapsed × spiralSpeed × Deg2Rad`, then `center + (cos(angle), sin(angle)) ×
   startRadius`. `startRadius` is the asteroid's distance from the orbit center, measured in `OnEnable`, so a
   Spiral placed on the orbit stays on it, and one placed inside spirals in a smaller ring. The center is read from
   `OrbitSettings` each frame, but the radius isn't. A negative `spiralSpeed`, or `MirrorSwing` (set by the
   spawner when it flips a pattern), reverses the direction. Example: placed at -90°, `spiralSpeed` 45: 0° at
   2 s, 90° at 4 s.

   **Homing: turn toward the nearest player body, then lock.** `homingAngle` is a running total (it carries on from
   last frame, since the target keeps moving). While `homingLocked` is false and there's a player: if the
   asteroid's depth ahead of the player (`z` minus the player's `z`) is at most `homingStopDistance`, it sets
   `homingLocked` and never turns again. Otherwise `homingAngle` moves toward `ClosestBodyAngle()` by at most
   `homingSpeed × Time.deltaTime` degrees, the short way round (`Mathf.MoveTowardsAngle`, which works in degrees,
   so the radians are converted in and out). It's then placed at `startRadius` and `homingAngle`, like Spiral.
   `ClosestBodyAngle` reuses `CloseCallZone.NearestGap`'s `out` argument to get the position of the nearest body
   (the player or a twin copy), then turns it into an angle round the orbit center with `Atan2`. Example: the
   asteroid at 90°, the player at -90°, `homingSpeed` 40: it can turn 4° per 0.1 s frame, and reaches the player's
   angle after 4.5 s if the player hasn't moved. A player who slides away before the lock is missed, because after
   the lock it flies straight.

   **Keeping apart.** Before taking its new angle, a Homing asteroid works out where that would put it (at its
   current depth) and asks `WouldCrowd`. That method walks `Active` and skips itself and phased asteroids.
   For each other asteroid it needs a centre-to-centre distance of at least `(HitRadius + other.HitRadius) ×
   (1 + homingGapScale)`, using real 3D distance, so asteroids at very different depths never block each other.
   It says "crowded" only if the new spot is inside that distance **and** closer than the asteroid is now, so one
   that spawned overlapping, or is being left behind, can still move away. If crowded, `homingAngle` isn't
   changed this frame, so the asteroid holds still. Example: two asteroids with `HitRadius` 0.5 and scale 1 need
   2.0 between centres (touching is 1.0, so the gap is 1.0, one collider width). A locked asteroid skips all
   of this.

   **StopAndGo: approach, one stop, then a dash.** It doesn't change the x/y at all. It only changes how fast
   `distance` grows, through `speedFactor` from `StopAndGoFactor`, which moves it through `stopPhase`
   (Approaching, Stopped, Dashing, once each, reset in `OnEnable`). *Approaching:* the factor is 1 until it's within
   `stopEaseDistance` of `stopDistance` (measured as depth ahead of the player), then `√(remaining ÷
   stopEaseDistance)`, a steady deceleration that reaches 0 exactly at `stopDistance`. An ease distance of 0 stops
   it abruptly. *Stopped:* the factor is 0 and `stopTimer` counts up to `stopTime`. *Dashing:* the factor is
   `dashMultiplier`, abruptly, for the rest of the flight, so the dash is still faster when the player is. Example
   at approach speed 20, `stopDistance` 30, ease 10: factor 0.77 at depth 36, stopped at 30, then 50 units/s. An
   asteroid that spawns inside `stopDistance` stops on its first frame. For the last `shakeTime` of the stop,
   `UpdateShake` offsets the mesh child (not the collider) by a random amount up to `shakeMagnitude` each frame,
   from `meshHome` (its authored `localPosition`, remembered in `Awake`), and puts it back exactly on the frame
   the dash starts.

   **Splitter (an effect, so it works with any movement).** At the end of `Update`, a Splitter that hasn't
   split yet (`hasSplit`) and has a `splitPiece` checks its depth ahead of the player. At `splitDistance` or less
   it calls `Split`, which takes its angle and radius round the orbit center from where it is now, makes
   `splitCount` pieces with `EffectPool.GetAsteroid` at that radius and depth, fanned `splitSpread` degrees
   apart and centred on its angle (offset `(i − (count − 1) ÷ 2) × spread`; for 3 pieces at -90° with spread 20:
   -110°, -90°, -70°), calls `BecomePiece(pieceScale)` on each and switches it on, then plays the explosion
   effect and removes itself with `PatternCopy.Despawn`. It doesn't count as smashed (no score, no stats), but
   its wave counts it as gone. The pieces aren't part of the wave. In `OnEnable` a Splitter asks
   `EffectPool.PrewarmAsteroids` for `splitCount` free pieces, so they exist before the split.

   **Growing (an effect, so it works with any movement).** `UpdateGrowing` runs each frame while moving, and
   only changes scale. *Pulse:* from `growDistance + pulseDistance` inwards the **mesh child's** scale is
   `meshStartScale × (1 + sin(elapsed × pulseRate × 2π) × pulseSize)`, a constant-strength swell and shrink, so
   the collider doesn't change. *Pop:* the first frame at `growDistance` or closer, `hasGrown` is set, the
   **root's** scale becomes `startScale × growScale` (collider and mesh together) and `hitRadius` becomes
   `fullHitRadius × growScale`, so the hitbox is its final size from that instant. *Settle:* `settleTimer` runs
   from 0 to `settleTime`, with `t` = progress 0 to 1, and the mesh scale is `meshStartScale × (1 + overshoot ×
   (1 − t)²)`: it starts 25% too big and eases down to exactly 1. The overshoot is visual only, and it happens
   before the asteroid reaches the player. Example (approach speed 20, defaults): pulsing from 40 ahead (about 3
   pulses), the pop at 30 (mesh 2.5 times, hitbox 2 times), settled at 2 times 0.2 s later. An asteroid placed
   inside `growDistance` pops on its first frame with no pulsing. `startScale`, `meshStartScale` and
   `hitRadius` are put back in `OnEnable` for a Growing asteroid, because `PatternCopy.Return` doesn't restore
   scale; this is gated on the effect, so a Splitter piece's scale (set by `BecomePiece`) isn't undone.

   **Magnet (an effect, so it works with any movement).** Meant as a slow, large void (`approachScale` ~0.3)
   that sits just outside the orbit and has **no Collider component** on its prefab (unticking it isn't enough:
   `WarnThenMove` switches it back on). `UpdateMagnet` runs each frame while moving. It takes the 3D distance
   `d` to the player; at `magnetRadius` or more it does nothing. Otherwise `closeness = 1 − d ÷ magnetRadius`
   (0 at the edge, 1 at point-blank), and the pull is `magnetStrength × closeness² × alongOrbit`, converted to
   radians per second and passed to `PlayerMovement.AddPull`. `alongOrbit` is the dot product of the XY direction
   from the player to the Magnet with the player's `OrbitTangent`: how much of that direction runs round the orbit
   (1 = fully along, 0 = straight in or out, negative = clockwise). So a Magnet straight outside the player pulls
   nothing sideways, and one off to a side pulls toward it. Squaring `closeness` makes the pull build slowly and
   then fast. Example (radius 30, strength 60, `alongOrbit` 0.95): about 25°/s at distance 10 and 40°/s at
   distance 5. A Magnet is **skipped by `TrackCloseCall`**, since a void you can't hit isn't a near miss. Its
   look is the Black Hole shader ([BlackHole.md](BlackHole.md)). That shader's bending uses Scene Color, which is
   black on the gameplay camera (an Overlay camera in a stack, with no Opaque Texture of its own), so the Magnet also
   carries a **MagnetMirror** that moves its mesh child to the matching spot in front of the background camera, and
   turns the effect so its arch points at the orbit's centre. The mesh child goes on the Background layer.
   **Prefab setup:** a big sphere mesh child on the Background layer with the Black Hole material, **no Collider
   component** on the root, an empty **Meshes** list, `approachScale` about 0.3, and Effect set to Magnet.

   **Frost (an effect, so it works with any movement).** A comet that leaves slowing patches on the player's
   orbit ring. A patch is a `FrostPatch`: a small `struct` (a position and an age) stored directly in the
   `patches` list, so a patch is a few numbers and not an object. After the comet is placed each frame, a Frost
   comet calls `UpdateFrost` and then `PaintFrost`.
   - **`UpdateFrost`** ages every patch by the frame time and removes it once its age reaches `patchLifetime`
     (the list is walked backwards, so a removal doesn't make it skip the next patch). A patch is a `struct`, a
     value type, so the loop works on a copy and writes it back with `patches[i] = patch`. It then checks the
     player: if the player's position is within `patchRadius` of any patch (compared as squared distances, so
     there's no square root), it calls `PlayerMovement.ApplyFrost(speedSlow, orbitSlow)` once. Overlapping
     patches never stack: you are either in frost or not.
   - **`PaintFrost`** leaves patches while the comet is within `paintDistance` of the player's depth. It takes the
     comet's direction from the orbit center and scales it to the orbit radius, so the patch lands **on the ring**
     at the player's depth, under the comet. It only adds one if there is none yet, or the new spot is at least
     `dropSpacing` from the last one (`lastDrop`, compared as squared distances). A comet at the exact center has
     no direction and is skipped. A Linear comet's spot never moves, so it leaves one patch; a Spiral or Wave
     comet sweeps round the ring and leaves a line of them. Example: `dropSpacing` 4, `patchRadius` 1: patches 4
     apart with 1-unit reach are separate spots; at spacing 2 they join into one strip.
   - **Patches don't move.** They sit on the ring until they expire. (An earlier version moved them toward the
     camera at the comet's own speed, which kept every patch stuck on the comet and left no trail.)
   - **Retiring.** `Finish` is called wherever the comet used to despawn directly: flying behind the camera, and
     being smashed while the player is invincible. For anything but a Frost comet with patches left, it just calls
     `PatternCopy.Despawn`. For a Frost comet with patches, it sets `retired`, removes itself from `Active` (so the
     hidden comet doesn't hold up winning the level or draw an OrbitRing arc), hides its renderer, switches its
     collider off and calls `trail.Stop()`. From then on `Update` only runs `UpdateFrost` and despawns the comet
     once `patches` is empty.
   - **Look.** The `ribbon` (a Trail Renderer) and the `trail` particle system (the mist, with an optional child
     system for fine dust) are separate from the gameplay patches: they follow the comet's whole flight, and
     `OnEnable` sets both lifetimes from `patchLifetime`. The rock, mist and ribbon use the Frost Rock, Frost Gas
     and Frost Trail shaders ([FrostRock.md](FrostRock.md), [FrostGas.md](FrostGas.md),
     [FrostTrail.md](FrostTrail.md)).
   - **Gizmos.** `OnDrawGizmos` draws every patch as a wire sphere of `patchRadius`: cyan and fading with age,
     red while the player is inside it. It only shows while playing (the list is empty otherwise).
5. **Place it.** `transform.position = position`. A Frost comet then runs `UpdateFrost` and `PaintFrost`. Until the
   close call has been judged, it calls `TrackCloseCall` (see below).
6. **Despawn.** Once it's more than `despawnBehindCamera` units behind the camera, it calls `Finish`, which
   despawns it with `PatternCopy.Despawn(gameObject)`, or retires it if it's a Frost comet with patches left. It's
   flying away from the player at that point, so it can never come back into view.

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
   **invincible**, or it `BreaksBySpeed` and the player's `Speed` is at least `destroySpeed`.

   Exploding plays the template's `explosionPrefab` through `EffectPool.Play` (a reused effect copy; a
   BonusCarrier with a `bonusExplosion` plays that instead),
   shakes the camera, adds one to `RunStats.AsteroidsDestroyed` through GameManager (null-safe), adds
   `scorePoints` with `Score.AddComboKill` (null-safe; Score adds the combo bonus and applies the speed
   multiplier), then, for a BonusCarrier with `bonusPoints` above 0, adds `bonusPoints` with
   `Score.AddBonusPoints` (flat: no speed or power-up multiplier, no combo; it counts toward the Asteroids line of
   the score breakdown and shows its own pop-up with `bonusLabel`; an invincible player gets it too), and finishes with `Finish` (a despawn with `PatternCopy.Despawn`, or a retire for a Frost comet
   with patches left). `Explode` takes the touching body's
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
| `Movement` | Its `MovementType` (Linear, Wave, Spiral, Homing or StopAndGo). Read-only. The pattern builder's fairness checks read it to skip Wave asteroids (the others are checked as if they were Linear) |
| `Amplitude`, `Frequency` | Read-only copies of the Wave swing settings. The pattern builder reads `Amplitude` to draw the sweep arc in the Scene view |
| `PoolSource` | Settable. Set by `EffectPool` on a pooled piece to the prefab it was made from, so `Despawn` can send it back to the right pile. Null for every other asteroid |
| `BecomePiece(scale)` | Method. Called on a piece while it's still switched off: sets `isPiece` (it skips the warning wait in `WarnThenMove`), scales it to `scale` times the prefab's size, scales `HitRadius` to match (`fullHitRadius × scale`, measured in `Awake`), and makes sure its collider is on |
| `MirrorSwing` | Settable. The spawner sets it when it flips a pattern left-right; a Wave asteroid then swings the other way (the swing is multiplied by -1). Not saved in the Inspector or prefab, starts false. Linear asteroids ignore it |
| `ApproachSpeed` | How fast it's closing in right now |
| `CanBreak` | Whether touching it right now would smash it |
| `HitRadius` | How wide its collider is on screen, measured once in `Awake`, so it's right even while the collider is off |
| `IsPhased` | Whether a Phase asteroid is phased out right now (collider off). OrbitRing draws its arc blue then |

`CanBreak` is the one rule for "would this break?": `!IsPhased && ((invincible) || (BreaksBySpeed && Speed >=
destroySpeed))`. A phased asteroid can never be broken. `BreaksBySpeed` is a private property that's true for the
Destructible and BonusCarrier kinds.
The outline color, OrbitRing's arc color and `OnTriggerEnter` all use it, so what the player sees always
matches what happens. "Invincible" comes from the PlayerInvincibility reference each asteroid grabs from
GameManager once in `Awake`, so checking it costs no searching. If there's no GameManager or no
PlayerInvincibility, that part is just false.

## Talks to

- **PlayerMovement**: reads the static `Speed`. On a hit, reads `OrbitTangent` and calls `TakeHit`. A Magnet calls
  `AddPull` each frame, and a Frost comet calls `ApplyFrost` each frame the player is inside a patch.
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
- **A Particle System and a Trail Renderer** (a Frost comet's children): `OnEnable` sets their lifetimes from
  `patchLifetime`, and `Finish` stops the particle system.

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
- **`kind` decides the outline only in `Awake`.** Changing it on a live asteroid in Play mode changes whether it
  can be smashed, but it won't gain or lose the outline.
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
- **Never `Destroy` it in code; call `PatternCopy.Despawn` (or `Finish`).** Destroying a pooled asteroid leaves a
  hole in its pattern copy.
- **A Frost comet owns its patches.** They live in the comet's own list, so the comet has to stay switched on until
  they expire, which is what retiring does. Switching it off any other way deletes its patches.
- **Frost's `trail` and `ribbon` fields must be assigned in the Inspector.** If either is empty, that part is
  skipped without any message: no mist or ribbon lifetime sync (or no visual at all).
- **Only the `trail` system gets its lifetime set from `patchLifetime`.** A child system (such as the fine dust
  layer) is stopped with it but keeps its own saved Start Lifetime. Set that by hand to match.
- **Sizes don't follow the prefab's scale.** `patchRadius`, `dropSpacing` and `paintDistance`, the Trail Renderer's
  width and the particle sizes and shape radii are all in world units (the particle systems' Scaling Mode is
  Local). If you scale the Frost prefab's root, change these by the same factor.
- **`dropSpacing` above twice `patchRadius` leaves gaps** between patches (separate spots). Below it they overlap
  into a strip.
- **Patches are made near the player's depth.** A large `paintDistance` paints early and late too, and a very small
  one can miss a fast comet's crossing frames. If a Linear comet leaves no patch, raise it a little.
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
