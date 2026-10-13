# Starry Sky

## Purpose

The "tube" skybox shader: **Nebula Clouds** flowing out of the vanishing point, with two layers of
**Radial Streams** on top that fade in during overdrive. It was the main skybox before **Starry Sky Volume**,
which is now the scene's skybox. `StarrySkyMat` still uses this shader, so you can switch back to it.

## Where it lives

- **File:** `Assets/Shaders/Starry Sky.shadergraph` (URP, Unlit, opaque)
- **Material:** `StarrySkyMat`. To use it, set it as the skybox in **Lighting → Environment → Skybox
  Material**.
- **Drawn by:** Earth Camera (the camera that renders the skybox; it faces −Z, which is why the material's
  Stream Center is (0, 0, −1)).
- **Driven by:** SkyboxSpeedController, which copies the skybox material in `Awake` and sets
  `_ScrollOffset`, `_StreamOffset`, `_StreamIntensity` and `_Core_Glow_Size` on the copy every frame.

## Properties

The properties are grouped into sections on the material. Each section feeds one of the Sub Graphs:

| Section | Properties | Feeds |
| --- | --- | --- |
| Sky | Space Color, Stream Center | Nebula Clouds; Stream Center also both stream layers |
| Clouds: Shape / Dust / Main Palette / Second Palette, Core Glow | All the cloud settings | Nebula Clouds. See [NebulaClouds.md](NebulaClouds.md) |
| Streams: Shared | Stream Coverage, Center Fade, Head Softness, Dash Scatter, Slot Offset | Both stream layers |
| Streams: Color | Stream Gradient (graph only), Color Scale, Hue Spread, Hue Offset, Hue Drift | Both stream layers |
| Streams: Layer 1 | Stream Count, Streak Softness, Dash Frequency, Dash Length, Stream Brightness | Layer 1 |
| Streams: Layer 2 | Layer 2 Count, Softness, Dash Frequency, Dash Length, Brightness, Speed | Layer 2 |
| Set by Script | Scroll Offset, Stream Offset, Stream Intensity | Overwritten every frame in Play mode |

What each stream setting does is in [RadialStreams.md](RadialStreams.md).

**Stream Gradient** is a Gradient, which a material can't store, so it's baked into the shader. Edit it in
the graph's Blackboard (Streams: Color).

**Layer 2 Speed** multiplies Stream Offset for layer 2 only. How fast a layer *looks* is Speed ÷ Dash
Frequency, so a slower-looking layer 2 gives a sense of depth.

## How it works

| Group | Nodes | Meaning |
| --- | --- | --- |
| `clouds` | Nebula Clouds Sub Graph (Offset ← Scroll Offset) | The sky: space, nebula, core glow |
| (layer 1) | Radial Streams Sub Graph (Offset ← Stream Offset) | Fine streaks |
| `layer2Offset` | Stream Offset × Layer 2 Speed | Layer 2's own pace |
| `layer2` | Radial Streams Sub Graph with the Layer 2 values | A second set of streaks |
| `streams` | layer 1 + layer 2 | Both layers together |
| `streamsFaded` | `streams` × Stream Intensity | 0 below overdrive, up to 1 at max speed (set by script) |
| Base Color | clouds + `streamsFaded` | |

The streams are **added** onto the clouds: where a stream is black, nothing changes, and where there's a
streak, its light goes on top, which can glow through Bloom.

## Talks to

- **Nebula Clouds** and **Radial Streams** (Sub Graphs): used as above.
- **SkyboxSpeedController**: sets `_ScrollOffset`, `_StreamOffset`, `_StreamIntensity` and `_Core_Glow_Size`
  on a copy of the material.

## Gotchas

- **Edit the material outside Play mode.** In Play mode the scene uses SkyboxSpeedController's copy, so
  edits don't show (and still change the asset).
- **Stream Coverage** here still has the reference name `_StreamCoverage` from when the script set it. No
  script sets it now, so the material value is what's used.
- **Reference names the script uses must not change:** `_ScrollOffset`, `_StreamOffset`,
  `_StreamIntensity`, `_Core_Glow_Size`. A mismatch fails silently.
- **Old values stay on the material.** StarrySkyMat still holds values for properties that were removed
  (for example `_NebulaColorA`, `_Stream_Saturation`). Unity keeps them; they do nothing.
