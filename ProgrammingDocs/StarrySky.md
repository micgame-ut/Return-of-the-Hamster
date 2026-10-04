# Starry Sky

## Purpose

The skybox shader. It draws a dark space background with drifting nebula clouds, a faint second cloud
layer, and on top of that the warp-speed radial streams (two layers of the `Radial Streams` Sub Graph)
that fade in during overdrive.

## Where it lives

- **File:** `Assets/Shaders/Starry Sky.shadergraph` (URP, Unlit, opaque)
- **Material:** `StarrySkyMat`, set as the skybox in the scene's Lighting settings.
- **Drawn by:** Earth Camera in TestGame (the camera that renders the skybox). It faces −Z.
- **Driven by:** SkyboxSpeedController, which copies the material in `Awake` and sets `_ScrollOffset`,
  `_StreamOffset` and `_StreamIntensity` on the copy every frame.

## Properties

"Material" is the current value on `StarrySkyMat`. **(script)** means SkyboxSpeedController overwrites
it every frame in Play mode, so the material value only matters in the Scene view.

### Clouds

| Property | Reference | Material | What it changes |
| --- | --- | --- | --- |
| Space Color | `_SpaceColor` | dark purple | The empty background between clouds |
| Nebula Color B | `_NebulaColorB` | purple-red | The nebula cloud color (see Gotchas: Nebula Color A is almost never used) |
| Nebula Color A | `_NebulaColorA` | purple | Meant as a second nebula color. Almost never reached |
| Star Intensity | `_Intensity` | 0.12 | Brightness of the nebula clouds (despite the name, it isn't stars) |
| Cloud Noise | `_CloudNoise` | 1 | Size of the nebula clouds. Higher = smaller, busier clouds |
| Stream Color | `_StreamColor` | dark pink | Color of the faint second cloud layer (not the radial streams) |
| Stream Noise | `_StreamNoise` | 1 | Size of the second cloud layer's noise |
| Scroll Offset | `_ScrollOffset` | **(script)** | Running total that drifts both cloud layers |
| Time Speed | `_TimeSpeed` | 0 | **Unused** (left over from before the running total, see Gotchas) |

### Radial streams (shared by both layers)

| Property | Reference | Material | What it changes |
| --- | --- | --- | --- |
| Stream Center | `_Stream_Center` | (0, 0, −1) | Direction of the vanishing point |
| Stream Coverage | `_StreamCoverage` | 0.3 | How much of the sky has streaks |
| Center Fade | `_Center_Fade` | 0.1 | How far from the vanishing point streaks fade out |
| Head Softness | `_Head_Softness` | 0.1 | Fade-in at the front of each dash |
| Dash Scatter | `_Dash_Scatter` | 3 | How far apart neighbouring streaks' dashes are shifted |
| Slot Offset | `_Slot_Offset` | (2.3, 1.3) | Noise step per dash (not for tuning) |
| Stream Gradient | (not on material) | gold → orange → magenta → purple → blue → gold | The streak palette. Edited in the graph's Blackboard |
| Color Scale | `_Color_Scale` | 1 | Size of the color regions |
| Hue Spread | `_Hue_Spread` | 1 | How quickly colors change across a region |
| Hue Offset | `_Hue_Offset` | (12.3, 45.6) | Which area of the noise colors come from |
| Hue Drift | `_Hue_Drift` | 0.02 | How fast the palette cycles over time |
| Stream Offset | `_StreamOffset` | **(script)** | Running total that moves the dashes outward |
| Stream Intensity | `_StreamIntensity` | 1 **(script)** | Visibility of both layers: 0 below overdrive, up to 1 at max speed |

### Radial streams, layer 1 only

| Property | Reference | Material | What it changes |
| --- | --- | --- | --- |
| Stream Count | `_Stream_Count` | 2 | Number of streaks. Lower = wider |
| Streak Softness | `_Streak_Softness` | 0.8 | Softness of streak edges |
| Dash Frequency | `_Dash_Frequency` | 0.2 | Dash repeats along each streak |
| Dash Length | `_Dash_Length` | 0.3 | Length of each dash's fading tail |
| Stream Brightness | `_Stream_Brightness` | 0.8 | Layer 1 brightness |

### Radial streams, layer 2 only

| Property | Reference | Material | What it changes |
| --- | --- | --- | --- |
| Layer 2 Count | `_Layer_2_Count` | 5 | Number of streaks |
| Layer 2 Softness | `_Layer_2_Softness` | 0.8 | Softness of streak edges |
| Layer 2 Dash Frequency | `_Layer_2_Dash_Frequency` | 0.5 | Dash repeats along each streak |
| Layer 2 Dash Length | `_Layer_2_Dash_Length` | 0.7 | Length of each dash's fading tail |
| Layer 2 Brightness | `_Layer_2_Brightness` | 0.6 | Layer 2 brightness |
| Layer 2 Speed | `_Layer_2_Speed` | 1.5 | Multiplies Stream Offset for layer 2. How fast it *looks* is Speed ÷ Dash Frequency, so layer 2 (1.5 ÷ 0.5 = 3) looks slower than layer 1 (1 ÷ 0.2 = 5), which gives depth |

## How it works

Only the radial streams part of the graph has groups. The cloud part is described by following its
wires from the start to Base Color.

### 1. Mapping the sky onto a flat disc

The cloud noise needs 2D coordinates, but the sky is a sphere. The graph turns each sky direction into a
point on a flat disc:

| Step | Nodes | Meaning |
| --- | --- | --- |
| Direction | Position (World) → Normalize → Split | The sky direction as x, y, z |
| Angle around the Z axis | Arctangent2(y, x) | Like the clock position looking along Z |
| Angle from +Z | Arccosine(z) | 0 at +Z, π at −Z |
| Height | Remap(0…π → 1…0) → Fraction → Remap(0…1 → 0.35…1) | Turns that angle into a radius: 1 near +Z, 0.35 at −Z |
| Disc point | Combine(cos(angle) × radius, sin(angle) × radius) | The 2D point the noise reads |

**Why cos and sin, instead of using the angle directly:** the angle from Arctangent2 jumps from π to −π
at one side of the sky, which would make a visible seam in the noise. Cosine and sine of the angle are
the same on both sides of that jump, so there's no seam.

### 2. Nebula clouds

| Step | Nodes | Meaning |
| --- | --- | --- |
| Drift | Tiling And Offset (disc point, Offset = Scroll Offset) | Slides the disc as `_ScrollOffset` grows |
| Cloud noise | Gradient Noise (Scale = 0.55 × Cloud Noise) | Large, soft cloud shapes |
| Cloud mask | Smoothstep(0.45, 0.75, noise) × Saturate(noise × 2) | 0 in gaps, 1 inside clouds |
| Nebula color | Lerp(Nebula Color B, Nebula Color A, Saturate((noise − 1) × 2)) × Star Intensity | The clouds' color and brightness |
| Clouds | Lerp(Space Color, nebula color, cloud mask) | Background where there are no clouds, nebula where there are |

### 3. Second cloud layer

| Step | Nodes | Meaning |
| --- | --- | --- |
| Drift | Tiling And Offset (disc point, Offset = (0.4, −0.65) + Scroll Offset) | Same drift, starting from a different spot so it doesn't match the nebula |
| Noise | Gradient Noise (Scale = 8.3 × Stream Noise) | Finer detail than the nebula |
| Color | Sample Gradient (white → white) × Stream Color | Effectively just Stream Color |
| Blend | Lerp(clouds, color, Smoothstep(0.19, 3.44, noise)) | Mixes this layer in very lightly: at most about 12–15% |

Despite the names "Stream Color" and "Stream Noise", this layer is part of the clouds, not the radial
streams. It existed before Radial Streams was added.

### 4. Radial streams

| Group | Nodes | Meaning |
| --- | --- | --- |
| (layer 1) | Radial Streams Sub Graph | Layer 1, with Stream Count, Streak Softness, Dash Frequency, Dash Length, Stream Brightness |
| `layer2Offset` | Stream Offset × Layer 2 Speed | Layer 2's own offset, so it moves at a different pace |
| `layer2` | Radial Streams Sub Graph | Layer 2, with the Layer 2 properties and `layer2Offset` |
| `streams` | Add(layer 1, layer 2) | Both layers together |
| `streamsFaded` | `streams` × Stream Intensity | Fades both layers together. 0 below overdrive |

Both layers share Stream Center, Coverage, Center Fade, Head Softness, Dash Scatter, Slot Offset, and all
the color properties, so they flow from the same point and follow the same color regions. How each
layer works is in [RadialStreams.md](RadialStreams.md).

### 5. Base Color

Base Color = clouds (after the second layer blend) + `streamsFaded`.

The streams are **added** rather than blended in. Where a stream is black (no streak), adding 0 leaves
the sky unchanged. Where there's a streak, its color is added on top, so it looks like light rather
than paint covering the clouds. Values above 1 can glow through Bloom.

## Talks to

- **SkyboxSpeedController**: sets `_ScrollOffset`, `_StreamOffset` and `_StreamIntensity` on a copy of
  `StarrySkyMat` every frame.
- **Radial Streams** (Sub Graph): used twice, for layer 1 and layer 2.

## Gotchas

- **Edit the material outside Play mode.** In Play mode the scene uses SkyboxSpeedController's copy, so
  material edits don't show (and do get saved to the asset).
- **Nebula Color A is almost never used.** The blend between Nebula Color B and A uses
  `Saturate((noise − 1) × 2)`. Gradient Noise is almost always below 1, so that's almost always 0, and the
  clouds are almost always Nebula Color B. It looks like it was meant to be something like
  `noise × 2 − 1`. Left as it is until we decide to change it.
- **Time Speed is unused.** The Time × Time Speed nodes aren't connected to anything. They're left over
  from before the clouds used `_ScrollOffset` (see SkyboxSpeedController's "Why it's a running total").
- **The second cloud layer's gradient is plain white**, so Sample Gradient does nothing there and the
  color is just Stream Color.
- **The cloud disc has two pinch points.** The angle around Z is undefined straight ahead (−Z) and straight
  behind (+Z), so the clouds pinch together at those spots, and the Fraction makes a tiny seam exactly at
  +Z. Straight ahead is behind the Earth, and the camera never looks behind, so neither shows.
- **Stream Gradient isn't on the material.** Shader Graph can't put a Gradient on a material, so it's
  baked into the shader. Edit it in the graph's Blackboard.
- **`_Stream_Saturation` is still in the material file.** It was removed from the graph. Unity keeps old
  values on materials. It does nothing.
- **Reference names the script uses must not change:** `_ScrollOffset`, `_StreamOffset`,
  `_StreamIntensity`. A mismatch fails silently.
