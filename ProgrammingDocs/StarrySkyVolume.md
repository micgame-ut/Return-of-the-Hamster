# Starry Sky Volume

## Purpose

The scene's current skybox shader: a **volumetric nebula** the camera flies through, with a **star field**
behind it, and **tube streams** (with glow and sparkles) that fade in during overdrive.

## Where it lives

- **File:** `Assets/Shaders/Starry Sky Volume.shadergraph` (URP, Unlit, opaque)
- **Material:** `StarrySkyVolumeMat`, set in **Lighting → Environment → Skybox Material** in TestGame.
  Switch to `StarrySkyMat` to use the older tube sky instead (see [StarrySky.md](StarrySky.md)).
- **Drawn by:** Earth Camera, which faces −Z (the material's Stream Center is (0, 0, −1)).
- **Driven by:** SkyboxSpeedController (it works with either skybox material, because both use the same
  reference names).

## Properties

| Section | Feeds | Details |
| --- | --- | --- |
| Sky | Nebula Volume; Stream Center also Tube Streams | Space Color, Stream Center |
| Clouds: Shape / Volume / Dust / Main Palette / Second Palette, Core Glow, Stars | Nebula Volume | See [NebulaVolume.md](NebulaVolume.md) |
| Streams: Shared | Tube Streams | Center Fade, Head Softness, Dash Scatter, Slot Offset |
| Streams: Color | Tube Streams | Stream Color 1–4, Color Scale, Hue Spread, Hue Offset, Hue Drift |
| Streams: Glow | Tube Streams | Wide Scale, Wide Coverage, Wide Softness, Wide Opacity, Glow Depth, Highlight Confinement |
| Streams: Sparkles | Tube Streams | Sparkle Amount, Size, Softness, Brightness, Saturation, Sparkle Color 1–4 |
| Streams: Tube | Tube Streams | Stream Count, Stream Coverage, Streak Softness, Dash Frequency, Dash Length, Stream Brightness, Stream Speed, Tube Depth Scale, Tube Max Depth |
| Set by Script | — | Scroll Offset, Stream Offset, Stream Intensity (Core Glow Size is also set by script) |

What each stream setting does is in [TubeStreams.md](TubeStreams.md).

**Stream Speed** multiplies Stream Offset before it reaches Tube Streams, so it scales how fast the dashes
travel.

**Renamed properties:** the Streams: Tube properties were "Layer 2 …" when this sky had two stream layers.
Only their display names changed: their reference names are still `_Layer_2_Count`, `_Layer_2_Coverage`
and so on, which is what kept the tuned values on the material.

## How it works

| Group | Nodes | Meaning |
| --- | --- | --- |
| `clouds` | Nebula Volume Sub Graph (Offset ← Scroll Offset) | Space, volumetric nebula, stars, core glow |
| `streamOffset` | Stream Offset × Stream Speed | The streams' pace |
| `tubeStreams` | Tube Streams Sub Graph | Streaks, glow and sparkles |
| `streamsFaded` | Tube Streams × Stream Intensity | 0 below overdrive, easing up to 1 at max speed (set by script) |
| Base Color | clouds + `streamsFaded` | |

## Talks to

- **Nebula Volume** and **Tube Streams** (Sub Graphs).
- **SkyboxSpeedController**: `_ScrollOffset`, `_StreamOffset`, `_StreamIntensity`, `_Core_Glow_Size`.

## Gotchas

- **The most expensive shader in the game.** Nebula Volume's Ray Steps × Cloud Octaves dominates. On the mini
  PC, lower those first.
- **Edit the material outside Play mode** (Play uses a copy).
- **Script reference names must not change** (`_ScrollOffset`, `_StreamOffset`, `_StreamIntensity`,
  `_Core_Glow_Size`).
- **Old values stay on the material** for removed properties (layer 1's `_Stream_Count` etc.). They do nothing.
