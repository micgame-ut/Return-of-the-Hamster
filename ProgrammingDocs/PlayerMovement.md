# PlayerMovement

## Purpose

Moves the player around a circular orbit (Gyruss-style) and owns the game's `Speed` value.
Left/right input slides the ship around the circle. Forward/back input changes `Speed`, which
the rest of the game uses to fake forward motion through space.

The final game uses a trackball, which reports as raw mouse data, so all input is handled
the same way you would handle a mouse.

## Where it lives

- **Attached to:** the player ship's root GameObject
- **Requires:**
  - `meshTransform`: the visual mesh (a child), which is rotated and blinked separately from the root/collider
  - A `PlayerInput` component sending `OnMove` and `OnButton` messages
  - `GameManager.Instance`, which provides the `LevelTimer`
  - The `OrbitSettings` asset, for the orbit's radius and center and the player's depth

## Inspector settings

| Field | Default | What it changes |
| --- | --- | --- |
| `mouseSensitivity` | 0.1 | How far a horizontal mouse movement slides the ship around the circle |
| `startAngle` | -90 | Where on the circle the ship starts, in degrees. -90 = bottom |
| `faceCenter` | true | Rotates the ship so its top always points at the center |
| `rotationSpeed` | 100 | How strongly input tilts/rolls the visual mesh |
| `rollDamping` | 5 | How fast the mesh settles back to level when input stops |
| `acceleration` | 5 | How much forward/back input changes `Speed` |
| `speedDecay` | 2 | How fast `Speed` drops per second with no input |
| `maxSpeed` | 10 | The top speed |
| `topSpeedDegrade` | 3 | Extra drain per second while in the top 10% of max speed |
| `boostForce` | 5 | Sideways boost strength (around the circle) |
| `boostDecay` | 3 | How fast a sideways boost or knockback fades out |
| `speedBoost` | 3 | How much `Speed` a forward boost adds |
| `invulnerableTime` | 1.5 | Seconds of no hits after being hit |
| `hitBlinkInterval` | 0.1 | Seconds between blink flips while invulnerable |

The orbit's size and center, and the player's depth (z), aren't set here. They're in the `OrbitSettings`
asset, shared with the pattern builder so the two always match. The orbit's radius is called
`CircleRadius` below.

## How it works

### Update vs. FixedUpdate

- **FixedUpdate** runs on the physics tick and handles the orbit position (`Movement`). Nothing here
  uses physics forces. It runs on this tick so the ship moves at a steady rate and stays in sync with
  the physics step, which is when asteroid trigger collisions are checked.
- **Update** runs every frame and handles everything else: the mesh roll (visual only), `Speed`, and the
  invulnerability blink.

### The orbit (`Movement`)

Picture a clock hand pinned to the center of the screen. The hand is `CircleRadius` long, and the ship
sits on its tip. `orbitAngle` is the direction the hand points. The ship starts at -90°, which is
6 o'clock (bottom of the screen).

Every physics tick:

1. **How far to slide.** Horizontal mouse input (plus any boost/knockback) becomes a distance to travel
   along the rim of the circle. This is `arc`. Positive = counter-clockwise.
2. **Turn distance into an angle.** `orbitAngle += arc / CircleRadius`. Angles are stored in radians, and a
   radian is defined as "the angle you turn when you travel one radius along the rim." So dividing the
   distance by the radius gives how much to turn. Because of this, the ship covers the same on-screen
   distance for the same mouse movement no matter how big the circle is.
3. **Turn the angle into a position.** Imagine the hand is exactly 1 unit long. Drop a line straight down from
   its tip to the horizontal line through the center, and you get a right triangle. The bottom side
   (how far right/left) is `cos(angle)`, and the vertical side (how far up/down) is `sin(angle)`.
   Negative means left/down.

   | Angle | Clock position | cos (x) | sin (y) |
   | --- | --- | --- | --- |
   | 0° | 3 o'clock | 1 | 0 |
   | 90° | 12 o'clock | 0 | 1 |
   | 180° | 9 o'clock | -1 | 0 |
   | -90° | 6 o'clock (start) | 0 | -1 |

   Multiply both by `CircleRadius` to scale the hand up to its real length, then add the orbit center
   so the circle sits in the middle of the screen. The ship's z (depth) is left alone.
4. **Face the center.** If `faceCenter` is on, the ship is rotated to match the clock hand's angle
   (+90° so the ship's top points inward, not sideways).
5. **Fade the boost.** `boostVelocity` is eased toward zero, so boosts and knockbacks drift to a stop
   instead of cutting off.

`OrbitTangent` is the direction along the rim (counter-clockwise) at the ship's current spot. Other scripts
use it to work out which way "along the circle" is, e.g. which way to knock the player.

`CircleRadius` and `OrbitCenter` are read from `OrbitSettings` every time they're used, so changing the
asset in Play mode takes effect straight away. In `Awake`, the player also moves itself to the asset's
`playerZ`. That's done in `Awake` so any other script's `Start` already sees the right depth.

### Roll (`Roll`)

Purely visual. Only the mesh rotates, never the root or collider.

- Forward/back input pitches the nose toward/away from the center.
- Left/right input rolls the mesh around its own axis.
- With no input, the roll eases back to level (`rollDamping`).

### Speed (`UpdateSpeed`)

`Speed` is a **static** variable. It lives in PlayerMovement and only this script can change it
(`private set`). Any other script can read it without needing a reference to the player. Other
scripts that want to change it have to go through a method like `TakeHit`, so all the clamping to
0–`maxSpeed` stays in one place.

Every frame:

- Forward/back mouse input adds to or removes from `Speed`.
- No input means `Speed` drains at a flat rate (`speedDecay`).
- In the top 10% of max speed, an extra drain (`topSpeedDegrade`) is applied on top. Holding top
  speed is harder, but still possible.
- Once the level is complete (earth has arrived), forward input stops counting. The player can only
  slow down.

`Speed` is reset in `Awake` because static values survive scene reloads.

#### What `Speed` drives

This one variable is how forward motion is simulated. The player never actually moves forward;
everything else reacts to `Speed`:

| Script | What `Speed` does there |
| --- | --- |
| **AsteroidBehavior** | Asteroids approach faster. Also decides whether the player is fast enough to smash an asteroid (`destroySpeed`), and turns on the asteroid's outline when it can be broken |
| **LevelTimer** | Faster = the level finishes sooner (the earth arrives sooner) |
| **PlayerCamera** | Widens the camera's field of view as speed goes up (uses `Speed01`, speed as 0–1) |
| **SkyboxSpeedController** | Makes the skybox drift faster |
| **StarfieldStreamer** | Makes the star particles stream past faster |

`MaxSpeed` and `Speed01` are also static, so other scripts can compare against max speed.

### Boost (`OnButton`)

Pressing the button boosts, depending on the current input:

- **Moving the mouse sideways:** boosts around the circle in that direction. This *sets* `boostVelocity`
  to `boostForce`, so spamming it restarts the boost rather than stacking it.
- **No sideways input:** boosts forward instead, adding `speedBoost` to `Speed` (capped at max). This one
  does stack with each press. It does nothing once the level is complete.

There is currently no cooldown, so the player can spam boost endlessly.

### Getting hit (`TakeHit`)

Called by `AsteroidBehavior` when the player hits an asteroid they are too slow to break.

1. If the player is invulnerable, the hit is ignored and `TakeHit` returns `false`.
2. The knockback is put into `boostVelocity`. Reusing the boost means the player gets "booped" along the
   circle, out of the way, and it fades the same way a boost does. AsteroidBehavior picks the direction
   based on which side of the asteroid the player was on.
3. `Speed` drops by `speedLoss`.
4. The invulnerability timer starts, and `TakeHit` returns `true` (AsteroidBehavior uses this to decide
   whether to shake the camera).

### Invulnerability blink (`UpdateInvulnerability`)

While the invulnerability timer is running, the ship's renderers flash on and off every
`hitBlinkInterval` seconds. It divides the time left by the interval and rounds down, so the result
counts 0, 1, 2, 3... and the ship is visible on even counts and hidden on odd ones. It always ends
visible.

Only the renderers are hidden. The collider stays active, so collisions still happen: the asteroid's
`OnTriggerEnter` fires and it calls `TakeHit` like normal. `TakeHit` checks `IsInvulnerable` first and
returns `false` straight away, so none of the knockback, speed loss, or camera shake happens. The
collision is detected, then ignored.

Side effects of doing it this way:

- The player can still **smash** asteroids while invulnerable, because the asteroid checks the smash
  (`Speed >= destroySpeed`) before it ever calls `TakeHit`.
- An asteroid the player is still overlapping when invulnerability ends will **not** hit them.
  `OnTriggerEnter` only fires on first contact, not while staying inside.

## Talks to

- **GameManager**: gets the LevelTimer from it
- **OrbitSettings**: reads the orbit's radius and center every tick, and the player's depth in `Awake`
- **LevelTimer**: checks `IsComplete` to stop acceleration after the earth arrives
- **AsteroidBehavior**: calls `TakeHit` and reads `OrbitTangent`
- **OrbitRing**: reads `OrbitCenter` and `CircleRadius` to draw the circle
- **Anything reading `Speed`**: see the table above

## Gotchas

- `Speed`, `MaxSpeed` are static: they are shared across the whole game and must be reset in `Awake`.
- The player's z comes from `OrbitSettings`. Moving the player in the scene does nothing, because it moves
  itself back to the asset's `playerZ` when the game starts.
- `meshTransform` must be assigned. `Roll` uses it without a null check.
- Boost has no cooldown yet.
- Invulnerability ignores hits in code, it doesn't disable the collider. Anything else that reacts to
  touching the player (not through `TakeHit`) will still react during invulnerability.
