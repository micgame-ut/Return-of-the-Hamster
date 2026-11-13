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
  - `GameManager.Instance`, which provides the `LevelTimer`, `HasStarted` and `IsGameOver`
  - A `PlayerHealth` on the same GameObject, for lives (read by AsteroidBehavior, not this script)
  - A `PlayerDeath` on the same GameObject, which moves the ship after game over (no direct link either)
  - The `OrbitSettings` asset, for the orbit's radius and center and the player's depth

## Inspector settings

| Field | Default | What it changes |
| --- | --- | --- |
| `mouseSensitivity` | 1.7 | Orbit distance per pixel of horizontal mouse movement, **in thousandths** of a world unit (5 = 0.005). TestGame uses 5 |
| `forwardThreshold` | 30 (slider 0–90) | Degrees either side of straight up inside which the sideways part of the trackball movement is ignored. 0 = off |
| `minInputForAngle` | 2 | Pixels per frame. Movement shorter than this is too small to have a reliable direction, so the threshold isn't applied |
| `startAngle` | -90 | Where on the circle the ship starts, in degrees. -90 = bottom |
| `faceCenter` | true | Rotates the ship so its top always points at the center |
| `rotationSpeed` | 1.7 | Degrees the mesh tilts/rolls per pixel of mouse movement. TestGame uses 1.25 |
| `rollDamping` | 5 | How fast the mesh settles back to level when input stops |
| `acceleration` | 83 | `Speed` gained per pixel of upward mouse movement, **in thousandths** (33 = 0.033). TestGame uses 12 |
| `speedDecay` | 2 | How fast `Speed` drops per second with no input |
| `maxSpeed` | 10 | The top speed |
| `topSpeedDegrade` | 3 | The most extra drain per second in overdrive, reached at max speed. It ramps up from 0 at the start of overdrive |
| `overdriveFraction` | 0.9 (slider 0.5–1) | Fraction of max speed where overdrive starts. 0.9 = the top 10% |
| `boostForce` | 5 | Sideways boost strength (around the circle) |
| `boostDecay` | 3 | How fast a sideways boost or knockback fades out |
| `speedBoost` | 3 | How much `Speed` a forward boost adds |
| `sideInputMemory` | 0.1 | Seconds after the last sideways mouse movement that a click still gives a side boost |
| `boostCooldown` | 3 | Seconds after a boost (side or forward) before the next one can be used. 0 = no cooldown |
| `invulnerableTime` | 1.5 | Seconds of no hits after being hit |
| `hitBlinkInterval` | 0.1 | Seconds between blink flips while invulnerable |

The orbit's size and center, and the player's depth (z), aren't set here. They're in the `OrbitSettings`
asset, shared with the pattern builder so the two always match. The orbit's radius is called
`CircleRadius` below.

## How it works

### Everything in `Update`, at any frame rate

`Update` runs once per drawn frame and does it all, in order: `Roll`, `UpdateSpeed`, `Movement` (the orbit)
and `UpdateInvulnerability`. The ship's Rigidbody is kinematic and its position is set directly, so there's
no physics simulation that would need `FixedUpdate`.

The orbit used to run in `FixedUpdate` (50 steps a second). On a 180 Hz screen that left the ship still on
most frames (a stutter), and each step only used the latest frame's mouse delta, throwing the rest away.

**Frame-rate independence.** The number of `Update` calls changes with the frame rate, so each value is
handled by what kind it is:

| Kind | Examples | How it's used |
| --- | --- | --- |
| An amount already per frame | the mouse delta (`moveInput`) | **As is.** The deltas of all frames add up to the real mouse movement, however many frames there are |
| A rate (per second) | `boostVelocity`, `speedDecay`, the roll speed | **× `Time.deltaTime`** |
| A fade toward zero | the boost, the roll | `Lerp(value, 0, 1 − e^(−k·dt))`. Plain `Lerp(…, k × dt)` fades slightly differently at different frame rates; `1 − e^(−k·dt)` compounds correctly |

Example, a 360 px mouse sweep with sensitivity 5: at 60 fps that's 60 × 6 px, at 180 fps 180 × 2 px, both
360 px × 0.005 = 1.8 units around the circle.

`mouseSensitivity` and `acceleration` are entered **in thousandths** (the constant `SensitivityScale` = 0.001
multiplies them), so the Inspector shows friendly numbers like 5 instead of 0.005.

### Before the start, game over and win (`InputLocked`)

`InputLocked` is a private property that's true **before flying begins** (GameManager's `IsFlying` is
`false`: neither the level nor the tutorial has started), and once GameManager's `IsGameOver` **or** `HasWon` is set.
The brackets around
`!IsFlying || IsGameOver || HasWon` make sure the null check on `GameManager.Instance` comes first.

- **Before the start** (Level 1's menu), HamsterMenuFloat and then MenuTransition's glide move the player.
- **On a death,** GameManager freezes time (`Time.timeScale = 0`) and PlayerDeath takes over the ship.
- **On a win,** time freezes too, and the ship just stays put while the win screen is up.

While it's true, PlayerMovement gets out of the way:

- **`Update` returns straight away.** Roll, speed, the orbit and the invulnerability blink all stop where
  they are, so `Movement` doesn't put the ship on the ring while something else moves it. The blink could
  freeze with the ship hidden, which is why PlayerDeath switches the renderers back on.
- **`OnButton` returns straight away,** so no boosts. Clicking the menu's Start button can't trigger one.

The name stayed `InputLocked`, although it now stops movement as well as input.

### `Start`: the cursor lock and the start angle

`Start` gets the LevelTimer and sets `orbitAngle` from `startAngle`. It also locks and hides the cursor
(`LockCursor`), but **only once play begins**, because the menu needs a visible cursor for its buttons:

- If the game has already started (Level 2 / 3, a restart), or there's no GameManager, it locks now.
- Otherwise it subscribes `LockCursor` to GameManager's `OnGameStarted`.

The check comes first because on levels without a menu, `StartGame` has already run in GameManager's `Awake`,
so subscribing would wait for an event that already happened. The subscription isn't removed: GameManager and
the player are destroyed together with the scene. (MainMenu also locks the cursor the moment Start is pressed.)

### Where the player starts (`StartPosition`, `StartRotation`)

Two read-only properties give the position and rotation `PlaceOnOrbit` will use at `startAngle`. They use the
same maths as the orbit below (centre + cos/sin × radius, at `playerZ`; rotation `startAngle + 90` on z if
`faceCenter`, otherwise the current rotation). With the default settings that's (0, −2, −4) and (0, 0, 0).
MenuTransition glides the menu hamster to exactly this pose, so when play begins and `Update` places the
player on the orbit, it doesn't move.

### The forward threshold (`OnMove`)

A trackball flick meant to go straight forward usually drifts a little sideways. `OnMove` filters that out
right after reading the input: if the movement is at least `minInputForAngle` long and its angle from straight
up (`Vector2.Angle(Vector2.up, moveInput)`, unsigned, 0–180) is at most `forwardThreshold`, `moveInput.x` is set
to 0. `y` is never touched, so `Speed` rises as usual.

Because `moveInput` is filtered once, everything that reads it sees the filtered value: the orbit slide in
`Movement` and the side-boost memory (`lastSideDirection`, `lastSideInputTime`). So a straight forward flick
doesn't slide the ship or turn the next click into a side boost.

`Roll` is the exception. It reads `rawMoveInput`, a copy of the input saved in `OnMove` before the filter. The
roll is only visual, and cutting its sideways part inside the cone made the tilt switch off and on as the angle
crossed the threshold, which felt choppy. So the mesh still tilts with every movement, even when the ship doesn't slide.

With a threshold of 30 and `minInputForAngle` 2:

| Raw input (x, y) | Angle from up | Result |
| --- | --- | --- |
| (0, 5) | 0° | Forward only |
| (2, 5) | 21.8° | Inside the cone: `x` becomes 0 |
| (-2, 5) | 21.8° | Same, the angle is unsigned |
| (4, 5) | 38.7° | Outside: slides and moves forward |
| (5, 0) | 90° | Sideways, unaffected |
| (0, -5) | 180° | Backwards, unaffected |
| (1, 0.5) | not measured | Shorter than 2 px, left alone |

The setting is only an Inspector field for now. The menu option to change it is still on the TODO list.

### The orbit (`Movement`)

Picture a clock hand pinned to the center of the screen. The hand is `CircleRadius` long, and the ship
sits on its tip. `orbitAngle` is the direction the hand points. The ship starts at -90°, which is
6 o'clock (bottom of the screen).

Every frame:

1. **How far to slide.** `arc = (moveInput.x × mouseSensitivity × 0.001 + boostVelocity × Time.deltaTime) ×
   (1 − frostOrbitSlow)`: the horizontal mouse movement this frame (already a distance, so not scaled by time)
   plus the boost or knockback (a speed, so scaled by time), reduced by any Frost slow (0 = no slow, so the
   factor is 1). Positive = counter-clockwise.
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
5. **Fade the boost.** `boostVelocity` is eased toward zero with the exact fade (`1 − e^(−boostDecay·dt)`),
   so boosts and knockbacks drift to a stop instead of cutting off, the same at any frame rate.

`OrbitTangent` is the direction along the rim (counter-clockwise) at the ship's current spot. Other scripts
use it to work out which way "along the circle" is, e.g. which way to knock the player.

Steps 3 and 4 live in their own method, **`PlaceOnOrbit`**, so `ShiftOrbit` (below) can place the ship the
same way.

**Magnet pull.** `AddPull(radiansPerSecond)` adds to `pullRate`. A Magnet asteroid calls it once a frame, and
pulls from several Magnets add up. In step 1 (`Movement`), the angle gets `pullRate × Time.deltaTime` radians on
top of the input's arc, and `pullRate` is then reset to 0, so each pull is spent exactly once, like a boost's
speed but without any fade. It's added to the input, not instead of it, so the player can fight it with the
trackball or boost out. When input is locked (before the start, after a death), `Update` returns early before
`Movement`, so it resets `pullRate` there too. Otherwise a pull added meanwhile would pile up and fire all at once.

**Frost slow.** `ApplyFrost(speedSlow, orbitSlow)` is called by a Frost comet's patches each frame the player is
inside one. It keeps the **strongest** request of the frame with `Mathf.Max` into `frostSpeedSlow` and
`frostOrbitSlow` (a pull adds up, but a slow doesn't, so two comets never freeze the player). The two values
are used like this:
- **Orbit:** step 1 above multiplies the mouse and boost movement by `1 − frostOrbitSlow`. The Magnet's pull isn't
  slowed. At `orbitSlow` 0.5 the ship slides at half speed, at 1 it can't slide at all.
- **Speed:** `UpdateSpeed` subtracts a drag, `Speed × frostSpeedSlow × Time.deltaTime`. That is a fraction of `Speed`
  per second, so it bites harder the faster you go: at `Speed` 8 and a slow of 0.5 it costs about 4 per second, at
  `Speed` 2 about 1 per second. Forward input still adds speed against it, so the player settles at a lower
  speed instead of stopping dead.

Both values are reset to 0 in `Movement`, after `UpdateSpeed` has used them (the same frame-by-frame pattern as
`pullRate`), and in the locked branch of `Update`, so a request made while input is locked never fires later.

**For the twin power-up:** `OrbitAngle` (read only) and `MeshTransform` let TwinCopy sit at an offset from the
player's angle and copy the mesh's roll. **`ShiftOrbit(radians)`** turns the player around the orbit at once
and places it straight away, without touching speed, boosts or roll. PlayerTwins uses it when the player is
hit during the twin, to take over the nearest copy's spot. It then raises **`OnOrbitShifted`**, which
ExhaustFlame uses to snap its drag points instead of sweeping them across the screen.

`CircleRadius` and `OrbitCenter` are read from `OrbitSettings` every time they're used, so changing the
asset in Play mode takes effect straight away. In `Awake`, the player also moves itself to the asset's
`playerZ`. That's done in `Awake` so any other script's `Start` already sees the right depth.

### Roll (`Roll`)

Purely visual. Only the mesh rotates, never the root or collider.

- Forward/back input pitches the nose toward/away from the center.
- Left/right input rolls the mesh around its own axis.
- With no input, the roll eases back to level (`rollDamping`, with the exact fade).

The mouse is treated as a **speed** here: `rollInput = rawMoveInput ÷ Time.deltaTime` (pixels per second), and
each frame rotates by `rollInput × rotationSpeed × Time.deltaTime`. While moving, that's simply
`rotationSpeed` degrees per pixel; after you stop, the fading "momentum" also turns the same amount at any
frame rate. The `Time.deltaTime > 0` check avoids dividing by zero if time is ever frozen. At high frame
rates the mouse sometimes reports zero for a frame mid-movement; roll briefly fades on those frames, which
adds only a sliver of extra roll.

### Speed (`UpdateSpeed`)

`Speed` is a **static** variable. It lives in PlayerMovement and only this script can change it
(`private set`). Any other script can read it without needing a reference to the player. Other
scripts that want to change it have to go through a method like `TakeHit`, so all the clamping to
0–`maxSpeed` stays in one place.

Every frame:

- Forward/back mouse input adds to or removes from `Speed`: `moveInput.y × acceleration × 0.001`. The mouse
  delta is already per frame, so it isn't scaled by time. 360 px of upward movement at acceleration 33
  gives 12 speed at any frame rate.
- No input means `Speed` drains at a flat rate (`speedDecay`).
- In **overdrive** (from `overdriveFraction` of max speed up), an extra drain is applied on top. It
  **ramps** across the overdrive band: `topSpeedDegrade × InverseLerp(OverdriveFraction, 1, Speed01)`.
  With max speed 10 and overdrive at 0.9:

  | Speed | Extra drain per second |
  | --- | --- |
  | 9 (overdrive starts) | 0 |
  | 9.5 | 1.5 |
  | 10 (max) | 3 |

  So it's easy to stay in overdrive, but holding the very top still takes effort.
- Where overdrive starts is defined once. The `overdriveFraction` slider is copied in `Awake` into the
  static property `OverdriveFraction` (like `maxSpeed` into `MaxSpeed`), and the public `InOverdrive`
  property uses it (`Speed >= maxSpeed * OverdriveFraction`). `UpdateSpeed` copies `InOverdrive` into a
  local `nearMaxSpeed` at the **start** of the method, before this frame's speed change, and RunStats
  reads it to time overdrive. SkyboxSpeedController reads `OverdriveFraction` directly (it's static, so
  no reference to the player is needed) to know where to start fading in the streams.
- **Frost drag:** after the overdrive drain, `Speed -= Speed × frostSpeedSlow × Time.deltaTime` (see "Frost slow"
  above). It is before the clamp, so the usual `SpeedFloor`–`maxSpeed` limits still apply, and before the invincibility lock,
  so an invincible player isn't slowed.
- Once the level is complete (earth has arrived), forward input stops counting. The player can only
  slow down.
- **Invincibility lock:** last of all, if PlayerInvincibility's `LocksMaxSpeed` is true (the power-up is
  active and its `lockMaxSpeed` is on), `Speed` is set to `maxSpeed`. It runs after input, decay and the
  overdrive drain, so nothing that frame can lower it. It's skipped once the level is complete
  (`CanAccelerate` is false), so the level-end rule always wins. The PlayerInvincibility on the same object
  is found once in `Awake` (`invincibility`). Side effects: the player is in overdrive while invincible, so
  the skybox streams fade in and RunStats counts it as overdrive time.

`Speed` is reset in `Awake` because static values survive scene reloads. It is reset to **`minSpeed`**, not 0.

**Minimum speed (`minSpeed`, `SetMinSpeed`, `SpeedFloor`).** Each level can set a floor for the player's speed
(`LevelConfig.minSpeed`, 0 to 100). `GameManager.ApplyLevelConfig` calls `SetMinSpeed` in its `Awake`, which runs
before this script's `Awake`, so the player starts at the floor. The three places that used to clamp `Speed` to 0
(`UpdateSpeed`, `TakeHit` and the forward boost in `OnButton`) clamp to **`SpeedFloor`** instead, so decay, braking,
Frost and hits can't take the player below it. `SpeedFloor` is a property: it is `minSpeed` while the level can still
be accelerated, and 0 once the Earth has arrived (`CanAccelerate` is false), so the player can slow down for the finish.
With `minSpeed` at 0 nothing changes. Levels are set to 0, 20 and 40.

#### What `Speed` drives

This one variable is how forward motion is simulated. The player never actually moves forward;
everything else reacts to `Speed`:

| Script | What `Speed` does there |
| --- | --- |
| **AsteroidBehavior** | Asteroids approach faster. Also decides whether the player is fast enough to smash an asteroid (`destroySpeed`), and turns on the asteroid's outline when it can be broken |
| **LevelTimer** | Faster = the level finishes sooner (the earth arrives sooner) |
| **PlayerCamera** | Widens the camera's field of view as speed goes up (uses `Speed01`, speed as 0–1) |
| **SkyboxSpeedController** | Makes the skybox clouds drift and the radial streams flow faster, and fades the streams in across the overdrive range (uses `Speed01` and `OverdriveFraction`) |
| **StarfieldStreamer** | Makes the star particles stream past faster |

`MaxSpeed` and `Speed01` are also static, so other scripts can compare against max speed.

### Boost (`OnButton`)

Pressing the button boosts, depending on the current input, unless the boost is cooling down (below):

- **Moved the mouse sideways in the last `sideInputMemory` seconds:** boosts around the circle in that
  direction (`lastSideDirection`). This *sets* `boostVelocity` to `boostForce`. It raises
  **`OnSideBoost(direction)`** (+1 counter-clockwise, −1 clockwise), which BoostRing uses to roll the ring.
- **Otherwise:** boosts forward instead, adding `speedBoost` to `Speed` (capped at max). It does nothing once
  the level is complete. It raises **`OnSpeedBoost`**, which ExhaustFlame uses to stretch the flame.

**Boost ready.** `Update` counts `cooldownTimer` down, and on the frame it reaches 0 it raises
**`OnBoostReady`** once (GameSFX uses it for the "boost ready" sound). It sits after the input-locked return, so
it stays silent on the menu and after game over.

**How far a side boost goes.** `boostVelocity` fades by `boostDecay` per second (the `1 − e^(−k·dt)` fade in
`Movement`), and a speed fading like that covers a total distance of **`boostForce ÷ boostDecay`**: 5 ÷ 3 ≈ 1.7
units along the orbit, about 32° on a radius-3 orbit. After `t` seconds it has covered `1 − e^(−boostDecay·t)`
of that. `BoostDecay` is exposed (read-only) so BoostRing can roll the ring by the same share.

### Boost cooldown

Both boosts share one cooldown:

1. **`cooldownTimer`** counts down by `Time.deltaTime` in `Update`, after the `InputLocked` check, so it's
   frozen at game over and win, and 0 (ready) during the menu.
2. **`OnButton` ignores presses while `cooldownTimer > 0`.** Each boost that happens sets it to
   `boostCooldown`. A forward press that does nothing (after the earth has arrived) doesn't start it.
3. **`BoostCharge01`** = `1 − cooldownTimer ÷ boostCooldown`, clamped to 0–1: 0 just after a boost, 1 when
   ready. BoostRing sends it to the ring's Ring Glow shader.

| Time since boost (cooldown 3) | `cooldownTimer` | `BoostCharge01` |
| --- | --- | --- |
| 0 s | 3 | 0 |
| 1 s | 2 | 0.33 |
| 2 s | 1 | 0.67 |
| 3 s | 0 or just below | 1 (ready) |

The last frame usually takes `cooldownTimer` slightly below 0; the `> 0` check counts that as ready, and the
clamp keeps `BoostCharge01` at exactly 1, which matters to the shader's "below 1 means charging" test.
Knockback sets `boostVelocity` in `TakeHit`, not through `OnButton`, so a hit neither starts the cooldown nor
spins the ring.

`OnMove` records `lastSideDirection` (−1 or +1) and `lastSideInputTime` whenever the mouse moves sideways.
The memory is needed because at high frame rates many frames report zero sideways movement even mid-sweep;
checking only this frame's input turned some side boosts into forward boosts. `lastSideInputTime` starts at
negative infinity, meaning "long ago", so the first 0.1 s of a level doesn't count as recent.

After game over, the button does nothing at all (see `InputLocked`).

### Getting hit (`TakeHit`)

Called by `AsteroidBehavior` when the player hits an asteroid they are too slow to break.

1. If the player is invulnerable, the hit is ignored and `TakeHit` returns `false`.
2. The knockback is put into `boostVelocity`. Reusing the boost means the player gets "booped" along the
   circle, out of the way, and it fades the same way a boost does. AsteroidBehavior picks the direction
   based on which side of the asteroid the player was on.
3. `Speed` drops by `speedLoss`.
4. The invulnerability timer starts, and `TakeHit` returns `true`. AsteroidBehavior uses this to decide
   whether to shake the camera and take a life (`PlayerHealth.LoseLife`).

### Invulnerability blink (`UpdateInvulnerability`)

While the invulnerability timer is running, the ship's renderers flash on and off every
`hitBlinkInterval` seconds. It divides the time left by the interval and rounds down, so the result
counts 0, 1, 2, 3... and the ship is visible on even counts and hidden on odd ones. It always ends
visible. The current state is public as **`MeshVisible`**, so ExhaustFlame can blink the flame in step.

Only the renderers are hidden. The collider stays active, so collisions still happen: the asteroid's
`OnTriggerEnter` fires and it calls `TakeHit` like normal. `TakeHit` checks `IsInvulnerable` first and
returns `false` straight away, so none of the knockback, speed loss, camera shake, or life loss
happens. The collision is detected, then ignored.

Side effects of doing it this way:

- The player can still **smash** asteroids while invulnerable, because the asteroid checks the smash
  (`Speed >= destroySpeed`) before it ever calls `TakeHit`.
- An asteroid the player is still overlapping when invulnerability ends will **not** hit them.
  `OnTriggerEnter` only fires on first contact, not while staying inside.

## Talks to

- **GameManager**: gets the LevelTimer from it, and reads `HasStarted`, `IsGameOver` and `HasWon` (through
  `InputLocked`) every frame and on the boost button. Subscribes `LockCursor` to `OnGameStarted` if the game
  hasn't started yet
- **MenuTransition**: reads `StartPosition` and `StartRotation` for the menu glide's target
- **HamsterMenuFloat**: no direct link, but on Level 1 it moves the same object during the menu, while
  `InputLocked` keeps PlayerMovement out of the way
- **OrbitSettings**: reads the orbit's radius and center every frame, and the player's depth in `Awake`
- **LevelTimer**: checks `IsComplete` to stop acceleration after the earth arrives
- **PlayerInvincibility**: found in `Awake` on the same object; reads `LocksMaxSpeed` every frame to hold max speed
- **AsteroidBehavior**: calls `TakeHit` and reads `OrbitTangent`; a Magnet asteroid also calls `AddPull`, and a
  Frost comet calls `ApplyFrost`
- **OrbitRing**: reads `OrbitCenter` and `CircleRadius` to draw the circle
- **OverdriveTally**: reads `InOverdrive` every frame (through GameManager's `Player`) to count the overdrive
  score tally
- **RunStats**: reads `InOverdrive` every frame (through GameManager's `Player`) to time overdrive
- **PowerUp**: checks whether what touched it is the player (through GameManager's `Player`)
- **TwinCopy**: reads `OrbitAngle`, `OrbitCenter`, `CircleRadius` and `MeshTransform` every frame to follow the player
- **PlayerTwins**: calls `ShiftOrbit` when the player is hit during the twin
- **ExhaustFlame**: reads `Speed01` and `MeshVisible`; subscribes to `OnSpeedBoost` and (on the player's own
  flame) `OnOrbitShifted`
- **BoostRing**: subscribes to `OnSideBoost` to roll the ring, and reads `BoostDecay` and `BoostCharge01` (on
  the player's ring and every twin copy's)
- **GameSFX**: subscribes to `OnSideBoost`, `OnSpeedBoost` and `OnBoostReady` to play their sounds
- **Score**: reads the static `Speed01` for its speed multiplier
- **PlayerDeath**: no direct link, but both move the same ship. PlayerMovement stops at game over so
  PlayerDeath can take over
- **Anything reading `Speed`**: see the table above

## Gotchas

- `Speed`, `MaxSpeed` are static: they are shared across the whole game and must be reset in `Awake`.
- **Never scale the mouse delta by time.** It's already distance per frame; `× Time.deltaTime` makes the
  game respond less the higher the frame rate. Rates get `× Time.deltaTime`, fades get `1 − e^(−k·dt)`.
- **`mouseSensitivity` and `acceleration` are in thousandths.** Old-style values (0.3, 2) from before the
  change would make the ship barely move. Check the prefab and any scene overrides.
- **The forward threshold works on one frame's input.** At high frame rates the trackball reports only a pixel
  or two per frame, so the angle comes in steps (for example (1, 2) is 26.6° and (1, 1) is 45°). A slow, wobbly
  forward roll can flicker between filtered and unfiltered. Raise `minInputForAngle` if it does.
- The ship is moved in `Update`, not `FixedUpdate`. Its Rigidbody must stay kinematic; a simulated
  Rigidbody would need its movement in `FixedUpdate` instead.
- The player's z comes from `OrbitSettings`. Moving the player in the scene does nothing, because it moves
  itself back to the asset's `playerZ` when the game starts.
- `meshTransform` must be assigned. `Roll` uses it without a null check.
- **`StartPosition` must match `PlaceOnOrbit`.** If the orbit maths changes, change both, or the menu glide
  will end in the wrong spot and the player will jump when play begins.
- **Low `boostDecay` makes the ring's spin outlast the cooldown.** The spin takes about 4.6 ÷ `boostDecay`
  seconds, so below about 1.5 (with a 3 s cooldown) a new side boost can land mid-spin and BoostRing snaps the
  ring back to start a new roll.
- After game over, PlayerMovement does nothing. Anything that should happen to the ship then belongs in
  PlayerDeath. The collider stays on, but physics is frozen, so no hits can happen.
- **A Frost slow lasts one frame.** `ApplyFrost` has to be called every frame the player is slowed. If the comet
  stops calling it (the player leaves the patch, or the comet is switched off), the slow ends the next frame.
- **An invincible player's `Speed` isn't slowed** (the max-speed lock overrides the drag), but their orbit movement
  still is.
- Invulnerability ignores hits in code, it doesn't disable the collider. Anything else that reacts to
  touching the player (not through `TakeHit`) will still react during invulnerability.
