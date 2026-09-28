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
  - `GameManager.Instance` with `earth` assigned, so stars can be removed when they fly into the earth. It
    still works without one, but stars would pass straight through the earth.
  - Optionally a `starMaterial`. If empty (it is in the scene), it makes one itself.

### Which camera sees the stars

The scene is split into two areas, far apart:

- **Gameplay** is near z = 0: the player (z = -4), the asteroids, and `Main Camera` (z ≈ -11), looking
  toward +z.
- **The background** is around z = -2500: `Camera Rig`, `Earth Camera`, the earth, and the stars.
  `Earth Camera` sits at the rig's position and looks the other way, toward -z, where the earth is.

`Camera Rig` is turned 180°, so its "forward" is the direction `Earth Camera` looks. The stars are
children of the rig, so they spawn ahead of **Earth Camera**, stream toward it, and fly past the earth
on the way. Main Camera is about 2,500 units away, facing the other direction, and never sees them.

In the rest of this doc, "the camera" means Earth Camera. All positions are in the rig's own space
(see `simulationSpace` below): +z is straight ahead of Earth Camera, and 0 is the rig, where Earth
Camera sits.

## Inspector settings

Only `baseSpeed` and `speedMultiplier` are read while the game runs. Everything else is only used once, in
`Awake`, to build the particle system. Changing them in Play mode does nothing.

### Speed

| Field | Default | What it changes |
| --- | --- | --- |
| `baseSpeed` | 30 | How fast stars stream past when the player's `Speed` is 0, in units/sec |
| `speedMultiplier` | 15 | How much faster they go for each point of player `Speed` |
| `sidewaysDrift` | 2 | Each star also drifts sideways/up/down at a random speed up to this, so they don't all move in perfect parallel lines |

### Field Shape

| Field | Default | What it changes |
| --- | --- | --- |
| `fieldWidth` | 500 | Width of the box stars spawn in |
| `fieldHeight` | 320 | Height of the box stars spawn in |
| `fieldDepth` | 40 | Depth of the box stars spawn in, so they don't all start at exactly the same distance |
| `spawnDistance` | 300 | How far ahead of the camera the spawn box is |
| `despawnDistance` | -50 | Stars are removed once they're this far behind the camera |

### Stars

| Field | Default | What it changes |
| --- | --- | --- |
| `emissionRate` | 180 | New stars per second |
| `lifetime` | 8 | Roughly how long each star lasts, in seconds (each gets a random 85%–115% of this) |
| `minSize` / `maxSize` | 0.2 / 0.8 | Each star gets a random size between these |
| `colorA` / `colorB` | white / pale blue | Each star gets a random color between these |
| `streakLengthScale` | 2 | How long the streaks are |
| `starMaterial` | none | Material for the stars. If empty, one is made in code |

### Occluder

| Field | Default | What it changes |
| --- | --- | --- |
| `earthPadding` | 1 | Extra margin around the earth when removing stars that fly into it |

The scene uses all of the default values.

## How it works

### A quick particle system primer

A **particle system** draws lots of small images (particles) and moves them around, without needing a
GameObject for each one. Unity's `ParticleSystem` is split into **modules**, each controlling one part of
the particles' behavior: how many spawn, where they spawn, how they move, how their color changes, and
how they're drawn. Normally you'd set these in the Inspector. This script sets them all in code instead.

### `Awake`: building the particle system (`BuildParticleSystem`)

1. **Make a child GameObject** under the rig and add a ParticleSystem to it.

2. **Main module:** the basics.
   - `loop` and `playOnAwake`: keep spawning forever, starting right away.
   - `simulationSpace = Local`: particle positions are measured relative to the rig, not the world. That's
     what makes "z = 300" mean "300 ahead of the camera".
   - Random lifetime (85%–115% of `lifetime`), size, and color per star.
   - `startSpeed = 0`: the particles don't get an initial push. All their movement comes from the velocity
     module below.
   - `maxParticles = 3000`: a cap. At 180 per second, living up to about 9 seconds, there are at most
     about 1,650 alive at once, so it's never hit.

3. **Emission module:** `emissionRate` new stars per second.

4. **Shape module:** where stars spawn. A box `fieldWidth` × `fieldHeight` × `fieldDepth`, centered
   `spawnDistance` ahead of the camera. Each new star appears at a random point inside it. The box is much
   wider and taller than the screen, so stars also stream past the edges of the view.

5. **Velocity over Lifetime module:** how stars move.
   - **z** is set to `-baseSpeed` for now, straight toward the camera. `Update` changes it every frame.
   - **x** and **y** are each a random speed between `-sidewaysDrift` and `+sidewaysDrift`, picked once per
     star.
   - Unity requires all three axes to use the same kind of value. x and y are "random between two
     numbers", so z has to be written as "random between two numbers" too, even though both are the same.

6. **Color over Lifetime module:** fades each star in over the first 15% of its life and out over the last
   15%, so stars don't suddenly pop in or out.

7. **Renderer:** `Stretch` mode draws each star stretched along the direction it's moving, as a streak
   instead of a dot. The length depends on `streakLengthScale` and on how fast it's moving
   (`velocityScale`), so **streaks get longer as the player speeds up**.

8. **Material:** uses `starMaterial` if assigned. Otherwise `CreateFallbackStarMaterial` makes one. It tries
   the URP particle shader first, then older built-in ones, and uses the first it finds (`??` means "if
   this is null, use the next one"). It then gives it Unity's built-in soft round dot texture, so stars
   look like glowing dots instead of squares.

It also saves the velocity module and makes a `particleBuffer` array big enough for every particle, so
`Update` and `LateUpdate` don't have to create anything new each frame.

### `Start`

Gets the earth from GameManager.

### `Update`: matching the player's speed

`speed = baseSpeed + PlayerMovement.Speed × speedMultiplier`

| Player `Speed` | Star speed |
| --- | --- |
| 0 | 30 units/sec |
| 5 | 105 units/sec |
| 10 (max) | 180 units/sec |

The z velocity is set to `-speed` for every star. The particle system moves each star by its velocity
every frame, so each star's position is already a running total. Changing the speed only changes how fast
they move from now on, and nothing jumps.

### `LateUpdate`: removing stars early

Stars normally disappear at the end of their lifetime. This removes them sooner in two cases:

1. **Behind the camera.** Once a star is past `despawnDistance` (50 units behind), it can never be seen
   again.
2. **Inside the earth.** The earth sits out in the starfield. Without this, stars would fly into the back
   of it and come out the front, drawn over it.

How it checks each star:

1. Get the earth's center and turn it from world space into the rig's space
   (`InverseTransformPoint`), so it can be compared to the star positions.
2. Treat the earth as a sphere, with a radius of half its width plus `earthPadding`.
3. Copy every living star into `particleBuffer` with `GetParticles`.
4. For each star, if it's behind the camera or inside the earth's sphere, set its remaining lifetime to 0.
   The particle system removes it next update.
5. If any were changed, copy them all back with `SetParticles`.

**Squared distances.** "Is the star inside the sphere?" means "is its distance from the center less than
the radius?" Working out a distance needs a square root, which is relatively slow, and this runs for
every star every frame. So it compares **squared** values instead: `sqrMagnitude < radius²`. If one
distance is smaller than another, its square is smaller too, so the answer is the same without the
square root.

The earth moves toward the camera as the level goes on (see LevelTimer), so its position is checked fresh
every frame.

## Talks to

- **GameManager**: gets the earth in `Start`.
- **PlayerMovement**: reads the static `Speed` every frame.

## Gotchas

- **At low speed, stars fade out before they reach the camera.** Stars spawn about 300 units ahead but
  only live about 8 seconds. At 30 units/sec, they get about 240 units before fading out. Stars only
  stream all the way past the camera once the player is moving. Raising `lifetime` fixes this, but keeps
  more stars alive at once.
- **Most settings only apply at startup.** Only `baseSpeed` and `speedMultiplier` can be tuned in Play mode.
  Everything else is baked into the particle system in `Awake`.
- **The earth is treated as a sphere, using its width.** That's fine for a round planet, but a different
  shaped or rotated object would get the wrong size.
- **The fallback material uses `Shader.Find`.** The same as OrbitRing: a shader only found this way can be
  left out of a build. If stars are missing or pink in a build, assign `starMaterial`.
