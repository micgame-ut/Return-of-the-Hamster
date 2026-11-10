# StarfieldStreamer

## Purpose

Makes it look like the player is flying forward through space. It builds a particle system that
constantly spawns stars far ahead of the camera and streams them past as streaks. The faster the player
goes, the faster the stars stream.

Like the asteroids and the sky, this is part of faking forward motion. The player never actually moves
forward. Everything else moves past them.

## Where it lives

- **Attached to:** `Camera Rig` in `Game.unity`, the parent of `Camera Shake` (and the cameras under it).
  When the game starts, it creates a child object called `StarfieldParticles` with the particle system on
  it.
- **Requires:**
  - `Camera Rig` on a layer that **Earth Camera's Culling Mask** includes (currently **Background**, the layer the earth is on too). The stars
    are put on the rig's layer; if Earth Camera doesn't draw that layer, no stars show at all.
  - Optionally a `starMaterial`. If empty (it is in the scene), it makes one itself.

### Which camera sees the stars

The scene is split into two areas, far apart:

- **Gameplay** is near z = 0: the player (z = -4), the asteroids, and `Main Camera` (z ≈ -11), looking
  toward +z.
- **The background** is around z = -2500: `Camera Rig`, `Earth Camera`, the earth, and the stars.
  `Earth Camera` sits at the rig's position and looks the other way, toward -z, where the earth is.

`Camera Rig` is turned 180°, so its "forward" is the direction `Earth Camera` looks. The stars are
children of the rig, so they spawn ahead of **Earth Camera** and stream toward it. Main Camera is about 2,500 units away, facing the other direction, and never sees them.

In the rest of this doc, "the camera" means Earth Camera. All positions are in the rig's own space
(see `simulationSpace` below): +z is straight ahead of Earth Camera, and 0 is the rig, where Earth
Camera sits.

## Inspector settings

Only `baseSpeed`, `speedMultiplier`, `rampTime`, `boostBurstSpeed` and `boostBurstTime` are read while the game
runs. Everything else is only used once, in `Awake`, to build the particle system. Changing them in Play mode
does nothing.

### Speed

| Field | Default | What it changes |
| --- | --- | --- |
| `baseSpeed` | 30 | How fast stars stream past when the player's `Speed` is 0, in units/sec |
| `speedMultiplier` | 15 | How much faster they go for each point of player `Speed` |
| `rampTime` | 1 | Seconds for the stars to speed up from still to normal once the game starts. Read every frame |
| `sidewaysDrift` | 2 | Each star also drifts **outward** (away from the middle) at a random speed up to this, so they don't all move in perfect parallel lines |
| `boostBurstSpeed` | 120 | Extra star speed, in units/sec, the instant a forward boost fires. It fades back to normal over `boostBurstTime`. Read every frame |
| `boostBurstTime` | 0.5 | Seconds the star burst takes to fade out after a forward boost. 0 = no burst. Read every frame |

### Field Shape

| Field | Default | What it changes |
| --- | --- | --- |
| `fieldRadius` | 300 | Stars spawn up to this far from the middle |
| `clearRadius` | 20 | …and no closer than this, so no star streams past the camera through the middle of the screen. Tune by eye |
| `spawnDistance` | 300 | How far ahead of the camera the spawn ring is |
| `despawnDistance` | -50 | Stars are removed once they're this far behind the camera (the despawn plane) |

### Stars

| Field | Default | What it changes |
| --- | --- | --- |
| `emissionRate` | 180 | New stars per second |
| `lifetime` | 8 | Roughly how long each star lasts, in seconds (each gets a random 85%–115% of this) |
| `minSize` / `maxSize` | 0.2 / 0.8 | Each star gets a random size between these |
| `colorA` / `colorB` | white / pale blue | Each star gets a random color between these |
| `streakLengthScale` | 2 | How long the streaks are |
| `starMaterial` | none | Material for the stars. If empty, one is made in code |

The scene uses all of the default values.

## How it works

### A quick particle system primer

A **particle system** draws lots of small images (particles) and moves them around, without needing a
GameObject for each one. Unity's `ParticleSystem` is split into **modules**, each controlling one part of
the particles' behavior: how many spawn, where they spawn, how they move, how their color changes, and
how they're drawn. Normally you'd set these in the Inspector. This script sets them all in code instead.

### `Awake`: building the particle system (`BuildParticleSystem`)

1. **Make a child GameObject** under the rig, on the **rig's layer** (so Earth Camera draws it), and add a
   ParticleSystem to it.

2. **Main module:** the basics.
   - `loop` and `playOnAwake`: keep spawning forever, starting right away.
   - `simulationSpace = Local`: particle positions are measured relative to the rig, not the world. That's
     what makes "z = 300" mean "300 ahead of the camera".
   - Random lifetime (85%–115% of `lifetime`), size, and color per star.
   - `startSpeed` = random 0 to `sidewaysDrift`: a small push **outward** from the middle (the circle shape
     fires particles away from its center). Forward movement comes from the velocity module below.
   - `maxParticles = 3000`: a cap. At 180 per second, living up to about 9 seconds, there are at most
     about 1,650 alive at once, so it's never hit.

3. **Emission module:** `emissionRate` new stars per second.

4. **Shape module:** where stars spawn. A **Circle** of radius `fieldRadius`, `spawnDistance` ahead of the
   camera, facing it. **Radius Thickness** decides how much of the circle spawns stars: 1 = the whole disc,
   0 = only the edge. It's set to `1 − clearRadius ÷ fieldRadius` (clamped to 0–1), so stars spawn in a
   **ring** from `clearRadius` out to `fieldRadius`, and the middle stays empty. With 300 and 20 that's
   1 − 20/300 = 0.933. The ring is much wider than the screen, so stars also stream past the edges.

   What the empty middle does: perspective still makes far stars start near the screen's center and
   stream outward as they approach (the warp look). What it guarantees is that no star passes close to the
   camera through the middle, so the big, fast streaks near the end of their flight never cross the middle
   of the screen. The ring is flat, but stars still end up spread out in depth, because they're emitted
   continuously while flying.

5. **Velocity over Lifetime module:** how stars move.
   - **z** is set to `-baseSpeed` for now, straight toward the camera. `Update` changes it every frame.
   - **x** and **y** are 0. The sideways drift is the outward start speed instead, so it can never carry a
     star into the empty middle.
   - Unity requires all three axes to use the same kind of value, so all three are written as "random
     between two numbers", even though each pair is the same.

6. **Collision module:** removes stars behind the camera (see below).

7. **Color over Lifetime module:** fades each star in over the first 15% of its life and out over the last
   15%, so stars don't suddenly pop in or out.

8. **Renderer:** `Stretch` mode draws each star stretched along the direction it's moving, as a streak
   instead of a dot. The length depends on `streakLengthScale` and on how fast it's moving
   (`velocityScale`), so **streaks get longer as the player speeds up**.

9. **Material:** uses `starMaterial` if assigned. Otherwise `CreateFallbackStarMaterial` makes one. It tries
   the URP particle shader first, then older built-in ones, and uses the first it finds (`??` means "if
   this is null, use the next one"). It then gives it Unity's built-in soft round dot texture, so stars
   look like glowing dots instead of squares. The made material is also stored in `fallbackMaterial`, and
   `OnDestroy` destroys it. A material made with `new` isn't cleaned up with the scene, and the scene
   leaves `starMaterial` empty, so this runs every scene load.

It also saves the velocity and emission modules, so `Update` doesn't have to look them up each frame.

### `Update`: no stars on the menu, then ramping in

During Level 1's menu there are no stars at all. Once the game starts they come in gently instead of
arriving at full rush:

1. **`started`** is true once GameManager's `IsFlying` is set, meaning the level or the tutorial has begun
   (or if there's no GameManager).
2. **Emission follows `started`:** off on the menu, on from the start. `emitting` remembers the last value
   set, so the emission module is only touched on the frame it changes (ExhaustFlame does the same with
   `coreVisible`). The particle system builds itself in `Awake`, but `Update` runs before Unity's first
   particle update, so not even one frame of stars gets out.
3. **`ramp = SmoothStep(0, 1, GameManager.TimeSinceStart / rampTime)`**, from 0 to 1 over `rampTime`, eased.
   The stream speed below is multiplied by it. So when play begins, stars appear out at the spawn ring
   (fading in, as always) and gather speed over the first second.

On levels without a menu, and restarts, `started` is true from the first frame and the same ramp plays from
the load. A `rampTime` of 0 is kept at 0.01, so the stars start at full speed without dividing by zero.

### The boost burst (`Awake`, `OnEnable` / `OnDisable`, `StartBurst`)

`Awake` also gets the player's `PlayerMovement` from `GameManager.Instance.Player` (this script isn't on the
player). `OnEnable` subscribes `StartBurst` to its `OnSpeedBoost` event and `OnDisable` unsubscribes. `StartBurst`
runs when a forward boost fires and sets `burstTimer` to `boostBurstTime`. `Update` turns that into extra speed
(next section). Without a `GameManager` there's no burst.

### `Update`: matching the player's speed

`speed = (baseSpeed + PlayerMovement.Speed × speedMultiplier + burst) × ramp`

`burst` is 0 normally. While `burstTimer` is above 0 it counts down, and `burst = boostBurstSpeed × t²` where
`t = burstTimer ÷ boostBurstTime` (1 at the instant of the boost, 0 at the end). Squaring makes the rush sharp and
quick to fade. The velocity applies to every star on screen, so the whole field surges at once, and the streaks
stretch because their length follows their speed. With 120 over 0.5 s:

| Time since boost | `burst` |
| --- | --- |
| 0 s | +120 units/sec |
| 0.1 s | +77 |
| 0.25 s | +30 |
| 0.5 s | 0 |

The `ramp` still multiplies the total, so a burst can't make stars rush before the game starts.

Once the ramp has finished (`ramp` = 1):

| Player `Speed` | Star speed |
| --- | --- |
| 0 | 30 units/sec |
| 5 | 105 units/sec |
| 10 (max) | 180 units/sec |

The z velocity is set to `-speed` for every star. The particle system moves each star by its velocity
every frame, so each star's position is already a running total. Changing the speed only changes how fast
they move from now on, and nothing jumps.

### The despawn plane: removing stars behind the camera

Stars normally disappear at the end of their lifetime. A star that has flown past the camera can never be
seen again, but at high speed its streak tail is long (around 30 units at top speed), so it's removed once
it's `despawnDistance` (50 units) behind, before the tail could poke back into view.

The **Collision module** does this in **Planes** mode: an invisible, infinite plane (a child object called
`Star Despawn Plane`) sits `despawnDistance` behind the camera. A plane faces along its Transform's **up**
axis, so it's turned (`FromToRotation(up, forward)`) to face the incoming stars. A star that crosses it has
collided; **Lifetime Loss = 1** takes all its remaining lifetime, so it dies there, and **Bounce = 0**.

**Why not check in a script:** this used to be done in `LateUpdate` by copying every living star (around
1,400) into a C# array, checking each, and copying them all back, about 140 KB each way per frame. The
Collision module does the same check inside the engine with no copying. The old code also removed stars
flying into the earth; the starfield never reaches the earth, so that check was removed.

## Talks to

- **PlayerMovement**: reads the static `Speed` every frame, and subscribes to `OnSpeedBoost` for the burst.
- **GameManager**: reads `HasStarted` and `TimeSinceStart` every frame for the menu and the ramp, and gets `Player`
  in `Awake`.
- **Earth Camera** (indirectly): draws the stars, through the Camera Rig's layer.

## Gotchas

- **At low speed, stars fade out before they reach the camera.** Stars spawn about 300 units ahead but
  only live about 8 seconds. At 30 units/sec, they get about 240 units before fading out. Stars only
  stream all the way past the camera once the player is moving. Raising `lifetime` fixes this, but keeps
  more stars alive at once.
- **Most settings only apply at startup.** Only `baseSpeed`, `speedMultiplier`, `rampTime`, `boostBurstSpeed` and
  `boostBurstTime` can be tuned in Play mode.
  Everything else is baked into the particle system in `Awake`.
- **No stars at all?** Check Earth Camera's Culling Mask includes the Camera Rig's layer (Background). The stars
  are put on the rig's layer.
- **Stars aren't removed at the earth any more.** If stars ever show in front of the earth, that's why; the
  old per-star earth check was removed because the stars never reach it.
- **`clearRadius` is in starfield units, not the orbit's.** The stars are in their own space, seen by a
  different camera, so it's tuned by eye, not tied to `OrbitSettings`.
- **The fallback material uses `Shader.Find`.** The same as OrbitRing: a shader only found this way can be
  left out of a build. If stars are missing or pink in a build, assign `starMaterial`.
