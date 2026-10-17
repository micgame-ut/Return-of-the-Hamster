# SkyboxSpeedController

## Purpose

Drives the moving parts of the skybox from the player's `Speed`:

- The nebula clouds drift (flow toward the player) faster as the player speeds up.
- The warp streams flow outward faster as the player speeds up.
- The streams only exist in overdrive: they ease in across the overdrive band (from PlayerMovement's
  `overdriveFraction` up to max speed), smoothed over time.
- The core glow at the vanishing point is sized to how big the Earth looks.

Every frame it moves the clouds and the streams a little further, by amounts based on `Speed`, and sends
the totals, the streams' visibility and the glow size to the skybox shader.

## Where it lives

- **Attached to:** `Main Camera` in each level scene (`Level_1`, `Level_2`, `Level_3`). It doesn't use the
  camera. It could sit on any GameObject. Level 1's menu uses the same one: the sky holds still and has no
  glow until the game starts.
- **Requires:** a skybox material set in the scene's Lighting settings (currently `StarrySkyVolumeMat`, which
  uses the `Starry Sky Volume` shader graph; `StarrySkyMat` with `Starry Sky` also works), and GameManager's
  `Earth` and `EarthCamera` for the glow. The shader needs float properties with the reference names
  `_ScrollOffset`, `_StreamOffset`, `_StreamIntensity` and `_Core_Glow_Size`.

## Inspector settings

| Field | Default | In the scene | What it changes |
| --- | --- | --- | --- |
| `baseTimeSpeed` | 0.3 | 0.3 | How fast the clouds drift when the player's `Speed` is 0 |
| `speedInfluence` | 0.05 | 0.01 | How much each point of player `Speed` adds to the drift speed |
| `baseStreamSpeed` | 0.5 | 0.01 | How fast the streams flow when the player's `Speed` is 0 |
| `streamSpeedInfluence` | 0.2 | 0.05 | How much each point of player `Speed` adds to the stream speed |
| `glowEarthScale` | 2 | 1.1 | How far the core glow reaches past the Earth's edge, in Earth radii |
| `streamFadeTime` | 1.5 | 1.5 | Seconds for the streams to fade fully in or out |
| `streamIntensityCurve` | 2 | 2 | Ease-in for stream brightness across overdrive. 1 = linear, higher = slower start |
| `randomStartRange` | 427 | 427 | Highest random starting value for the cloud scroll, so each load shows a different sky. 0 = the same sky every load |
| `glowGrowTime` | 2 | 2 | Seconds for the core glow to grow from nothing to its normal size after the game starts |

The "In the scene" column is the same in all three level scenes.

With the scene's values, the drift speed goes from 0.3 (stopped) to 0.4 (max speed 10), and the stream
speed from 0.01 to 0.51. The streams are only visible from 90% of max speed, so in practice they flow at
0.46 to 0.51.

How much of the sky the streams cover is set on the material (`Stream Coverage`), not here.

## How it works

### `Awake`: making a copy of the skybox material

`RenderSettings.skybox` is the skybox material the scene uses. It's the actual `StarrySkyMat` asset in the
project.

Changing a material asset while the game runs changes the **asset file itself**, and those changes stay
after you stop Play mode (the same as with ScriptableObjects, see SpawnableTemplate). If the script changed
the asset directly, the material would be left with whatever value it had when you stopped playing.

To avoid that, `Awake` makes a copy of the material with `Instantiate`, and tells the scene to use the copy
as its skybox. The script only ever changes the copy.

A material copied with `Instantiate` isn't part of the scene, so Unity doesn't remove it when the scene
unloads. `OnDestroy` destroys the copy, so restarting or loading the next level doesn't leave another
orphaned material in memory each time.

If the scene has no skybox, there's nothing to copy, and `Update` does nothing.

### `Awake`: a random starting sky

The clouds are a fixed 3D noise field: the same position always gives the same clouds, with both the math
noise and the baked noise. Which clouds you see depends on where the camera starts in that field, and
`scrollOffset` is that starting point. Starting it at 0 every time gave the same sky on every load.

So `Awake` starts it at `Random.Range(0, randomStartRange)` instead. `Update` keeps adding to it as before,
so the sky still flows smoothly from there. One noise cell (about one cloud feature) is 1 ÷ Cloud Scale world
units, so with Cloud Scale 0.15:

| Starting `scrollOffset` | Noise cells moved | Result |
| --- | --- | --- |
| 0 | 0 | The old fixed sky |
| 7 | about 1 | A visibly different layout |
| 100 | 15 | A completely different sky |
| 427 | 64 | One full repeat of the baked noise, back to where 0 started |

That's why the default is 427 (64 ÷ 0.15): every distinct baked sky can come up. It moves the clouds, the dust
lanes and the second-palette regions, which all read the same position. The stars don't use the offset, so
their layout stays the same. Unity seeds `Random` differently each launch, so even the first level varies.
The menu is part of Level 1, so the menu's sky *is* the level's sky, and it carries straight on when play
begins.

### `Update`: holding still on the menu

At the top of `Update`:

`drift = (no GameManager, or HasStarted) ? Time.deltaTime : 0`

Both running totals (the clouds and the streams) add `speed × drift` instead of `speed × Time.deltaTime`.
Before the start, `drift` is 0, so nothing is added and the sky holds perfectly still: the hamster isn't
travelling yet. From the start, it's the normal frame time. The stream fade below still uses
`Time.deltaTime`, but during the menu `Speed` is 0, so its target is 0 anyway.

### `Update`: scrolling the clouds

Every frame:

**1. How fast to drift right now.**

`timeSpeed = baseTimeSpeed + PlayerMovement.Speed × speedInfluence`

| Player `Speed` | Drift speed (scene values) |
| --- | --- |
| 0 | 0.3 |
| 5 | 0.35 |
| 10 (max) | 0.4 |

**2. Add this frame's drift to the running total.**

`scrollOffset += timeSpeed × drift` (`drift` is `Time.deltaTime` once the game has started)

This is the same running total idea as `orbitAngle` in PlayerMovement, `distance` in AsteroidBehavior, and
`Progress` in LevelTimer. A speed change only changes how fast the total grows from now on, so the clouds
never jump.

**3. Send it to the shader.** `SetFloat` sets `_ScrollOffset` on the copied material.

`SetFloat` uses `ScrollOffsetId` instead of the name `"_ScrollOffset"`. `Shader.PropertyToID` turns the name
into a number once, when the game starts, which is faster than Unity looking up the name every frame.
AsteroidBehavior does the same for its emission and outline colors.

**Pausing.** When `Time.timeScale` is 0, `Time.deltaTime` is 0, so nothing gets added and the clouds
freeze where they are. It needs no special case.

### `Update`: moving the radial streams

**4. Stream offset.** The same running total as the clouds, with its own speeds:

`streamSpeed = baseStreamSpeed + PlayerMovement.Speed × streamSpeedInfluence`
`streamOffset += streamSpeed × drift`

The total is sent as `_StreamOffset`. `baseStreamSpeed` is never 0, so the streams always flow outward,
even at low speed. Pausing freezes them the same way as the clouds.

### `Update`: fading the streams in during overdrive

**5. Stream intensity**, in three steps.

**How far into overdrive:**
`overdriveDepth = Mathf.InverseLerp(PlayerMovement.OverdriveFraction, 1, PlayerMovement.Speed01)`

`InverseLerp(a, b, value)` is the reverse of `Lerp`: it says how far `value` is between `a` and `b`, as
0–1, clamped. `OverdriveFraction` is PlayerMovement's shared overdrive threshold (its
`overdriveFraction` slider, 0.9 by default), so the streams start exactly where overdrive starts.

**Ease-in curve:** `targetIntensity = Mathf.Pow(overdriveDepth, streamIntensityCurve)`. Added light
(and Bloom) makes low values already look bright, so the curve keeps the start of overdrive faint.

| `Speed01` | `overdriveDepth` | `targetIntensity` (curve 2) |
| --- | --- | --- |
| below 0.9 | 0 | 0 (streams don't exist) |
| 0.9 (overdrive starts) | 0 | 0 |
| 0.95 | 0.5 | 0.25 |
| 1.0 (max) | 1 | 1 |

**Smoothing over time:** `streamIntensity = Mathf.MoveTowards(streamIntensity, targetIntensity, Time.deltaTime / streamFadeTime)`.
Speed can jump across the whole overdrive band in one frame (a boost adds `speedBoost`), so the shown
intensity moves toward the target at a steady rate instead of snapping: a full 0 → 1 (or back) takes
`streamFadeTime` seconds. `MoveTowards` moves at a constant rate and arrives exactly, unlike a `Lerp`
toward the target, which slows down and never quite gets there. `streamIntensity` is a field so it's
remembered between frames. Pausing freezes it, because `Time.deltaTime` is 0.

It's sent as `_StreamIntensity`. `streamOffset` keeps growing while the streams are invisible, so they
fade in mid-flow.

### `Update`: sizing the core glow to the Earth

**6. Core glow size.** If GameManager, its `Earth` and its `EarthCamera` all exist:

1. `earthDistance` = distance from Earth Camera to the Earth's center.
2. `glowRadius` = the Earth's radius (`bounds.extents.x`) × `glowEarthScale`.
3. `glowAngle = Mathf.Asin(Mathf.Clamp01(glowRadius / earthDistance))`. A line from the camera that just
   touches a sphere of that radius makes this angle with the line to its center (sin = radius ÷ distance).
   `Clamp01` keeps the ratio at 1 or below, so a camera inside the glow radius can't produce NaN.
4. `_Core_Glow_Size = 1 − cos(glowAngle)`. The shader measures distance from the vanishing point as
   1 − `along`, and `along` is the cosine of the angle, so this converts the angle into the same units.

| Distance to Earth (radius 10, scale 2) | Glow reaches | Core Glow Size |
| --- | --- | --- |
| 1000 | about 1.1° | 0.0002 |
| 200 | about 5.7° | 0.005 |
| 50 | about 24° | 0.083 |

So the glow hugs the Earth at any distance, growing as it approaches. Without a GameManager (or Earth /
camera), it's skipped and the material's value stays.

**Growing in at the start.** There's no glow on the menu. When play begins it grows in instead of popping
on: the size above is multiplied by `grow`, which eases from 0 to 1 over `glowGrowTime` seconds:

`grow = Mathf.SmoothStep(0, 1, GameManager.TimeSinceStart / glowGrowTime)`

| Time since the start (grow time 2) | `grow` | Glow size (normal 0.0015) |
| --- | --- | --- |
| 0 s | 0 | 0.00001 (the minimum) |
| 0.5 s | 0.156 | 0.00023 |
| 1 s | 0.5 | 0.00075 |
| 2 s and after | 1 | 0.0015, then follows the Earth |

During the menu `TimeSinceStart` is 0, so `grow` is 0 and the glow sits at the invisible minimum. On
levels without a menu (and restarts) the game starts on load, so the glow grows in from the load. `grow` is
worked out fresh each frame from GameManager's `TimeSinceStart`, not built up as a running total like the
scrolls. That's safe here because the grow time never changes during play, so there's nothing that could make
it jump. `TimeSinceStart` uses `Time.time`, which follows the time scale, so pausing pauses the growth too. `glowGrowTime` is kept at 0.01 or above to avoid
dividing by zero, like `streamFadeTime`.

The result is never allowed below `MinGlowSize` (0.00001). The shader's glow is
`Smoothstep(Core Glow Size, 0, distance)`, and with a size of exactly 0 both edges are 0. That turns into a
divide by zero, which makes the glow full brightness across the **whole sky**.

### What the shader does with it

Inside the sky shader (Starry Sky Volume or Starry Sky):

- `_ScrollOffset` is the clouds' Offset. As it grows, the cloud field flows toward the player.
- `_StreamOffset` drives the streams' dashes outward (scaled by Stream Speed, or Layer 2 Speed for the
  second layer in Starry Sky).
- `_StreamIntensity` multiplies the streams before they're added onto the clouds. At 0 nothing is added.
- `_Core_Glow_Size` sets how far the glow at the vanishing point spreads.

### Why it's a running total

The shader used to work out the offset itself as `Time × Time Speed` (Unity's clock multiplied by a speed
this script set). Because that's calculated fresh every frame, changing the speed moved the clouds to
where they *would* be if they'd been going at the new speed the whole time. 100 seconds in:

| | Time Speed | Cloud offset |
| --- | --- | --- |
| Stopped | 0.3 | 100 × 0.3 = 30 |
| After speeding up to max | 0.4 | 100 × 0.4 = 40 |

So the clouds rushed an extra 10 units while the player accelerated, scrolled backward while they slowed
down, and got worse the longer the level ran. Pausing would also have snapped the sky back to how it looked
at the start. Building the offset up here, a little each frame, fixes both.

## Talks to

- **PlayerMovement**: reads the static `Speed` and `Speed01` every frame, and the constant
  `OverdriveFraction`.
- **GameManager**: reads `Earth` and `EarthCamera` every frame for the glow size, `HasStarted` for the drift,
  and `TimeSinceStart` for the glow growing in.
- **The sky shader** (Starry Sky Volume or Starry Sky): sets `_ScrollOffset`, `_StreamOffset`,
  `_StreamIntensity` and `_Core_Glow_Size` on a copy of the skybox material.

## Gotchas

- **Tweak the sky in the material, not in Play mode.** While the game runs, the scene uses the copy. Edits to
  `StarrySkyMat` in Play mode change the asset, but not what's on screen.
- **Stream Intensity on the material is only for editing.** The script overwrites it every frame in Play
  mode. Its default of 1 is there so the streams show in the Scene view while you tune them.
- **Core Glow Size on the material is only for editing.** In Play mode it's always set by this script
  (hidden on the menu, then following the Earth). Keep it at a visible value like 0.0018 so the glow shows in
  the Scene view. **Never set it to exactly 0**: the whole sky glows (see "Growing in at the start").
- **`randomStartRange` depends on Cloud Scale** (and on CloudNoiseBaker's `Size`). The ideal value is
  `Size ÷ Cloud Scale`. If either changes, update it: too low gives less variety, too high repeats some skies.
  Neither breaks anything.
- **The reference names must match.** If `_StreamOffset`, `_StreamIntensity` or `_Core_Glow_Size` is renamed
  in the sky shader, `SetFloat` silently sets nothing. No error is shown.
