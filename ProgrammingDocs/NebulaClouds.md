# Nebula Clouds

## Purpose

A Sub Graph that draws galaxy-style nebula clouds in the skybox, flowing out from the vanishing point as if
the player is flying down a tunnel of cloud. It has warped, swirly shapes, dark dust lanes, two color
palettes blended by region, and a glow at the vanishing point. It outputs the full sky color behind the
streams.

## Where it lives

- **File:** `Assets/Shaders/Nebula Clouds.shadersubgraph`
- **Used by:** the `Starry Sky` shader graph (the "tube" sky, `StarrySkyMat`). See [StarrySky.md](StarrySky.md).
- **Values set at runtime:** `Offset` comes from `_ScrollOffset` (SkyboxSpeedController's running total), and
  `Core Glow Size` from `_Core_Glow_Size` (sized to the Earth by SkyboxSpeedController).

## Inputs

The defaults below are the Sub Graph's own (for its previews). The real values come from Starry Sky.

| Section | Input | Default | What it changes |
| --- | --- | --- | --- |
| Sky | Space Color | very dark purple | Empty space behind the clouds |
| Sky | Stream Center | (0, 0, 1) | Direction of the vanishing point (shared with the streams) |
| Clouds: Shape | Cloud Scale | 2 | Size of the clouds around the circle. Lower = bigger |
| | Cloud Depth Scale | 0.5 | How stretched the clouds are along the tunnel |
| | Cloud Density | 0.5 | How much of the sky is cloud |
| | Cloud Softness | 0.4 | How gradually clouds fade into space |
| | Cloud Detail | 0.5 | Strength of each finer octave (0 = smooth blobs, 1 = gritty) |
| | Cloud Octaves | 4 | Layers of detail (1–6). Main quality/cost setting |
| | Warp Strength | 0.6 | How swirly the shapes are |
| | Cloud Center Fade | 0.04 | Clouds fade out within this distance of the vanishing point |
| Clouds: Dust | Dust Amount | 0.5 | How dark the dust lanes get |
| | Dust Scale | 3 | Size of the dust lanes |
| Clouds: Main Palette | Cloud Color Deep / Mid / Bright / Core | purple / pink / orange / pale gold | Color from thin to dense cloud |
| | Cloud Brightness | 1 | Overall cloud brightness |
| Clouds: Second Palette | Alt Color Deep / Mid / Bright / Core | blue / violet / crimson / pale pink | The second palette |
| | Hue Region Scale | 0.4 | Size of the palette regions. Lower = bigger |
| | Hue Region Balance | 0.5 | Share of the sky using the second palette (0.5 ≈ half) |
| | Hue Region Softness | 0.3 | How gradually the palettes blend at region edges |
| Core Glow | Core Glow Color | warm white | Glow at the vanishing point |
| | Core Glow Size | 0.1 | How far the glow spreads (set by script in game) |
| Set by Script | Offset | 0 | Flight distance; moves the clouds outward |

**Output:** `Color` (the sky behind the streams).

## How it works

### 1. Directions: `dir`, `c`, `right`, `up`, `along`, `circle`

The same as Radial Streams (see [RadialStreams.md](RadialStreams.md)): the pixel's direction, the vanishing
point, a frame around it, how close the pixel is to the center (`along`), and its clock position (`circle`).

### 2. The tunnel: `depth`, `cloudPos`

| Group | Nodes | Meaning |
| --- | --- | --- |
| `depth` | `along` ÷ √(max(1 − `along`², 0.0001)), then Min(…, 8) | How far down a tunnel the pixel looks (the cotangent of the angle from the center) |
| `cloudPos` | Combine(`circle` × Cloud Scale, `depth` × Cloud Depth Scale + Offset) | The 3D point the noise is read at: around the tunnel, and along it |

| Angle from center | `depth` |
| --- | --- |
| 10° | 5.7 (far down the tunnel) |
| 45° | 1 |
| 60° | 0.6 (beside you) |

As Offset grows, every cloud feature moves to a smaller `depth`, which means outward, and faster toward the
screen edges, like flying through a tunnel. The Min(…, 8) stops the tunnel squeezing endless detail into the
center (within about 7°). Cloud Center Fade hides that zone.

### 3. The noise: `nebulaNoise` (Custom Function `NebulaNoise`)

HLSL inside the graph, because Shader Graph's own noise is 2D only. It reads **3D value noise**: an invisible
grid with a random value at each corner (from a hash of the corner's coordinates), blended smoothly between
the 8 corners around a point. Each pass adds the noise to itself at twice the frequency (**fbm**, Cloud
Octaves times, each layer × Cloud Detail).

| Pass | Octaves | Output | Meaning |
| --- | --- | --- | --- |
| Warp | up to 2 | (internal) | A value turned into a direction that pushes the next pass sideways: swirls and filaments |
| Density | Cloud Octaves | `Density` | The cloud shapes, at the warped position |
| Dust | up to 2 | `Dust` | Dust lanes, at a separate position × Dust Scale |
| Tint | 1 | `Tint` | Slow noise for the palette regions (× Hue Region Scale) |

**Fade input:** where the center fade has removed the clouds completely, the function returns "no cloud"
straight away and skips all four passes. That saves the whole cost over the faded area.

**Endless mode:** the grid coordinates wrap every 289 cells, so a huge Offset never makes the noise lose
precision.

### 4. Shape and color

| Group | Nodes | Meaning |
| --- | --- | --- |
| `cloudMask` | Smoothstep(1 − Cloud Density, + Cloud Softness, Density) | 0 = space, 1 = thick cloud |
| `cloudColor` | `cloudMask` × 3, then three Lerps Deep → Mid → Bright → Core (each over a third), × Cloud Brightness | Thin cloud is deep purple, thick cloud pale gold |
| `altColor` | The same three Lerps with the Alt colors | The second palette |
| `tintMask` | Smoothstep((1 − Balance) − Softness/2, (1 − Balance) + Softness/2, Tint) | Where the second palette shows. Centered, so Balance 0.5 is about half |
| `paletteMix` | Lerp(main, alt, `tintMask`) | Two palettes blended by region |
| `dustMask` | Smoothstep(0.5, 0.8, Dust) × Dust Amount | Where the dark lanes are |
| `distance`, `cloudFade` | Smoothstep(0, Cloud Center Fade, 1 − `along`) | Clouds fade out near the vanishing point |
| `clouds` | Lerp(Space Color, color, `cloudMask` × (1 − `dustMask`) × `cloudFade`) | The finished cloud layer over space |

### 5. Glow and result

| Group | Nodes | Meaning |
| --- | --- | --- |
| `glow` | Core Glow Color × Smoothstep(Core Glow Size, 0, `distance`) | Bright at the center, fading out by Core Glow Size |
| `result` | `clouds` + `glow` → Output Color | |

## Talks to

- **Starry Sky** (shader graph): uses this Sub Graph and passes in every input.
- **SkyboxSpeedController** (indirectly): sets `_ScrollOffset` (→ Offset) and `_Core_Glow_Size`.

## Gotchas

- **It's a tube.** The clouds sit on a tunnel wall, so there's no real parallax between clouds. That's why
  Nebula Volume exists (see [NebulaVolume.md](NebulaVolume.md)).
- **Cost:** about 2 + Cloud Octaves + 2 + 1 noise reads per pixel outside the faded center (9 at Octaves
  4). Cloud Octaves is the main setting for a slow PC.
- **Stream Center must not point straight up or down** (the cross product used for `right` breaks).
- **Previews look odd:** the node preview looks straight into the vanishing point, so it mostly shows the
  glow and the clamped center. Judge it in the Scene view.
- **Don't regenerate this file.** Starry Sky finds each input by its ID. Edit it in place so the IDs stay.
