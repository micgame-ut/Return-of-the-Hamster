# Nebula Volume

## Purpose

A Sub Graph that draws the skybox's nebula as a **real 3D volume** the camera flies through, plus a
procedural **star field** behind it. Unlike Nebula Clouds (clouds on a tunnel wall), clouds here sit at
different distances, so near ones rush past and far ones drift slowly, and nearer clouds cover further ones.

## Where it lives

- **File:** `Assets/Shaders/Nebula Volume.shadersubgraph`
- **Used by:** the `Starry Sky Volume` shader graph (`StarrySkyVolumeMat`, the scene's current skybox). See
  [StarrySkyVolume.md](StarrySkyVolume.md).
- **Values set at runtime:** `Offset` from `_ScrollOffset`, and `Core Glow Size` from `_Core_Glow_Size`, both
  set by SkyboxSpeedController.

## Inputs

Everything Nebula Clouds has (Sky, Clouds: Shape, Dust, both palettes, Core Glow, Offset; see
[NebulaClouds.md](NebulaClouds.md)), **except** Cloud Depth Scale, plus:

| Section | Input | Default | What it changes |
| --- | --- | --- | --- |
| Clouds: Volume | Ray Steps | 16 | Samples per view ray (4–48). Main quality/cost setting |
| | Near Distance | 0.5 | Where the ray starts. Nothing closer is cloud |
| | Far Distance | 6 | Where the ray stops |
| | Absorption | 3 | How quickly cloud becomes opaque. Higher = thicker, more solid |
| | Depth Haze | 0.6 | How much far clouds fade toward Space Color |
| | Noise Texture | — | The baked `CloudNoise3D` 3D texture (from CloudNoiseBaker). **Promoted to final Shader**: it's set on the material, not a port on the Sub Graph node |
| Stars | Star Density | 150 | How finely the sky is divided into star cells. Higher = more, smaller stars |
| | Star Amount | 0.15 | Fraction of cells with a star or galaxy |
| | Star Size | 0.12 | Dot size, as a fraction of a cell |
| | Star Brightness | 2 | Overall star brightness (above 1 = Bloom) |
| | Star Twinkle | 0.3 | Small flicker while a star shines (0 = steady) |
| | Star Color Variation | 0.5 | 0 = all white, 1 = warm-to-blue tints (mostly warm) |
| | Star Spikes | 0.6 | 4-point spikes on the bright stars (0 = none) |
| | Galaxy Amount | 0.08 | Share of faint objects drawn as small warm galaxies |
| | Star Blink Speed | 0.1 | Life cycles per second: stars fade in and out and reappear elsewhere (0 = fixed) |

**Output:** `Color`.

## How it works

### 1. Directions and `localDir`

The same direction groups as Nebula Clouds (`dir`, `c`, `right`, `up`, `along`, `circle`). **`localDir`** =
Combine(`dir`·`right`, `dir`·`up`, `along`): the view direction in a frame where "forward" is the vanishing
point.

### 2. The raymarch: Custom Function `NebulaVolumeNoise`

For each pixel, a ray starts at the camera and samples the cloud field at **Ray Steps** evenly spaced points
from Near to Far Distance.

1. **Position:** `(localDir × distance + (0, 0, Offset)) × Cloud Scale`. The clouds never move: the sample
   points move forward as Offset grows, as if the camera flies through a fixed cloud world.
2. **Per step:** a 1-octave warp (swirl), then the cloud noise (Cloud Octaves of fbm) at the warped point,
   then a density through Smoothstep(Threshold, Threshold + Softness).
3. **Fog build-up:** each step adds `opacity = 1 − e^(−density × Absorption × step length)`. A running
   **transmittance** (how much light still gets through) is multiplied down each step. Near samples count
   more, because light from further back passes through them.
4. **Early exit:** once less than 3% of light gets through, the ray stops.
5. **Jitter:** each pixel starts at a small random offset (up to one step), which turns step "rings" into a
   fine grain.
6. **Shared passes:** dust and the palette tint are read once, at the middle of the ray.
7. **Fade input:** where the center fade has hidden the clouds, the function returns straight away.

Outputs: **Coverage** (1 − transmittance), **Density** (density of the visible cloud), **Depth** (0 near to
1 far, weighted by what's visible), **Dust**, **Tint**.

Worked example (Near 10, Far 40, Ray Steps 16, Absorption 0.05): step length 1.875, and a fully dense step
adds about 9% opacity, so 16 dense steps in a row let about 22% of the light through: soft, see-through
cloud.

**Rule of thumb:** keep step length × Cloud Scale under about 0.5 (half a noise feature per step), or the
clouds start to look grainy. Cost is about Ray Steps × (1 + Cloud Octaves) + 3 noise reads per pixel.

**Each noise read is one texture sample.** The noise used to be calculated: hash the 8 corners of the cell the
point is in, then blend them with 7 lerps, using `u`, the position inside the cell smoothed with
`f × f × (3 − 2f)`. CloudNoiseBaker now saves those same hash values into a 64 × 64 × 64 texture (one texel per
cell), and the shader reads it at `(cell + u + 0.5) ÷ 64`:

- **+ 0.5:** texel centers sit in the middle of each texel, so cell 3's value is at 3.5.
- **+ u:** the texture's Bilinear filter blends the 8 neighbouring texels by how far the point sits between
  their centers. Reading at `cell + u` makes the hardware use exactly the smoothed weights the math used, so
  the clouds look the same.
- **Wrap every 64 cells:** positions are wrapped with `q − floor(q ÷ 64) × 64` to match the texture's size
  (Repeat). That also keeps numbers small at large Offsets, so precision holds.

Because the baked hash matches the old math, the clouds keep the look they were tuned with. Only the repeat
changed (every 64 cells instead of 289), so the exact layout differs in places.

### 3. Shape and color

The same as Nebula Clouds (`cloudMask`, `cloudColor`, `altColor`, `tintMask`, `paletteMix`, `dustMask`,
`cloudFade`), except:

- **Coverage** replaces the cloud mask as opacity in `clouds`.
- **`haze`**: Lerp(cloud color, Space Color, Depth × Depth Haze). Far clouds look dim and distant.

### 4. Stars: Custom Function `StarField`

Drawn behind the clouds and hidden by them (× (1 − cloud cover)).

- **Cube-face grid:** the view direction is projected onto the face of a cube around the camera that it
  points at, and that face is split into flat cells. Each star sits inside its cell with a margin wider than
  itself, so it's **never clipped** at a cell edge.
- **Three layers:** faint (Star Density), medium (0.35×) and bright (0.12×, 2× brightness, with 4-point
  spikes). Most stars are dim and few are bright.
- **Galaxies:** some faint objects are small, soft, warm ellipses at a random angle.
- **Blinking:** each cell runs its own life cycle (fade in, shine, fade out). The cycle number is part of the
  random roll, so each new cycle puts a new star (or none) somewhere else in the cell. A fixed per-cell
  offset keeps cells from blinking together.
- Uses the **world** direction, so stars don't move with the flight. The particle stars handle that.

### 5. Result

`clouds` + core `glow` + visible stars → Output Color.

## Talks to

- **Starry Sky Volume** (shader graph): uses this Sub Graph and passes in every input.
- **SkyboxSpeedController** (indirectly): `_ScrollOffset` → Offset (starting at a random value each load), and
  `_Core_Glow_Size`.
- **GraphicsQuality** (indirectly): sets `_Ray_Steps` per quality level.
- **CloudNoiseBaker** (Editor): makes the `CloudNoise3D` texture read through Noise Texture.

## Gotchas

- **Near ≈ Far makes Ray Steps useless:** every step lands at the same point, so it behaves like a single
  flat shell. Spread Near and Far apart for depth and parallax.
- **Cost:** by far the most expensive part of the sky. Ray Steps is the main setting (GraphicsQuality sets
  it per quality level). Cloud Octaves only matters when Cloud Detail is above 0: at Detail 0, every octave
  after the first adds nothing but still costs a texture read.
- **The 64 in the shader must match CloudNoiseBaker's `Size`.** Rebaking at another size without changing the
  two `64.0`s in `NebulaVolumeNoise` gives wrong blending and seams.
- **An empty Noise Texture slot reads as white.** Every noise read returns 1, so the sky turns into solid
  fog. Check that `CloudNoise3D` is assigned on the material.
- **Twinkle and blink need Time:** in the Scene view outside Play, they only animate with Always Refresh on.
- **Stars near the cube's corners** are slightly larger and sparser (the grid stretches there).
- **Don't regenerate this file.** Starry Sky Volume finds each input by its ID. Edit it in place.
