# SkyboxSpeedController

## Purpose

Drives the moving parts of the skybox from the player's `Speed`:

- The nebula clouds drift faster as the player speeds up.
- The radial streams (warp streaks from the `Radial Streams` sub graph) flow outward faster as the
  player speeds up.
- The radial streams only exist in overdrive: they fade in across the overdrive band (from PlayerMovement's `overdriveFraction`, 0.9 by default, up to max speed).

Every frame it moves the clouds and the streams a little further, by amounts based on `Speed`, and sends
the totals and the streams' visibility to the skybox shader.

## Where it lives

- **Attached to:** `Main Camera` in `TestGame.unity`. It doesn't use the camera. It could sit on any
  GameObject.
- **Requires:** a skybox material set in the scene's Lighting settings (currently `StarrySkyMat`, which uses
  the `Starry Sky` shader graph). The shader needs float properties with the reference names
  `_ScrollOffset`, `_StreamOffset` and `_StreamIntensity`.

## Inspector settings

| Field | Default | In the scene | What it changes |
| --- | --- | --- | --- |
| `baseTimeSpeed` | 0.3 | 0.3 | How fast the clouds drift when the player's `Speed` is 0 |
| `speedInfluence` | 0.05 | 0.01 | How much each point of player `Speed` adds to the drift speed |
| `baseStreamSpeed` | 0.5 | 0.01 | How fast the streams flow when the player's `Speed` is 0 |
| `streamSpeedInfluence` | 0.2 | 0.05 | How much each point of player `Speed` adds to the stream speed |
| `glowEarthScale` | 2 | 2 | How far the core glow reaches past the Earth's edge, in Earth radii |
| `streamFadeTime` | 1.5 | 1.5 | Seconds for the streams to fade fully in or out |
| `streamIntensityCurve` | 2 | 2 | Ease-in for stream brightness across overdrive. 1 = linear, higher = slower start |

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

`scrollOffset += timeSpeed × Time.deltaTime`

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
`streamOffset += streamSpeed × Time.deltaTime`

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

### What the shader does with it

Inside the `Starry Sky` shader graph:

- `_ScrollOffset` is the offset of the noise that makes the clouds. As it grows, the clouds slide across
  the sky.
- `_StreamOffset` goes to both `Radial Streams` layers (layer 2 gets it multiplied by `Layer 2 Speed`).
  As it grows, the dashes move outward from the vanishing point.
- `_StreamIntensity` multiplies both stream layers before they're added onto the clouds. At 0 nothing
  is added, so the sky is only clouds.

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
- **The `Starry Sky` shader**: sets its `_ScrollOffset`, `_StreamOffset` and `_StreamIntensity` properties
  on a copy of the skybox material.

## Gotchas

- **Tweak the sky in the material, not in Play mode.** While the game runs, the scene uses the copy. Edits to
  `StarrySkyMat` in Play mode change the asset, but not what's on screen.
- **Stream Intensity on the material is only for editing.** The script overwrites it every frame in Play
  mode. Its default of 1 is there so the streams show in the Scene view while you tune them.
- **The reference names must match.** If `_StreamOffset` or `_StreamIntensity` is renamed in Starry Sky,
  `SetFloat` silently sets nothing and the streams stop moving or never appear. No error is shown.
