# Tube Streams

## Purpose

A Sub Graph that draws the overdrive warp streaks as if they're painted on the walls of a **tunnel**: dashes
start small and slow near the vanishing point and stretch out and speed up toward the screen edges. On top
of the basic streaks it has a soft, deep-colored **glow** behind them and random colored **sparkles** at some
dash fronts.

It started as a copy of Radial Streams (see [RadialStreams.md](RadialStreams.md)). Everything not listed
here works the same way.

## Where it lives

- **File:** `Assets/Shaders/Tube Streams.shadersubgraph`
- **Used by:** the `Starry Sky Volume` shader graph, as its only stream layer. See
  [StarrySkyVolume.md](StarrySkyVolume.md).
- **Values set at runtime:** `Offset` (Stream Offset × Stream Speed in Starry Sky Volume).

## Inputs

| Section | Input | Default | What it changes |
| --- | --- | --- | --- |
| Sky | Stream Center | (0, 0, 1) | Direction of the vanishing point |
| Streams: Shared | Coverage | 0.5 | How much of the circle has streaks |
| | Center Fade | 0.02 | How far from the vanishing point streaks fade out |
| | Head Softness | 0.1 | Fade-in at the front of each dash |
| | Dash Scatter | 10 | How far apart neighbouring streaks' dashes are shifted |
| | Slot Offset | (2.3, 1.3) | Noise step per dash (not for tuning) |
| Streams: Color | Stream Color 1–4 | orange / red-orange / magenta / violet | The palette, looped 1 → 2 → 3 → 4 → 1 |
| | Color Scale, Hue Spread, Hue Offset, Hue Drift | 3 / 3 / (12.3, 45.6) / 0.02 | Color regions and how they cycle (as in Radial Streams) |
| Streams: Glow | Wide Scale | 0.25 | Wide bands per streak. Lower = fewer, wider |
| | Wide Coverage | 0.6 | How much of the circle the bands fill |
| | Wide Softness | 0.5 | How soft the band edges are |
| | Wide Opacity | 0.25 | Strength of the glow (0 = off) |
| | Glow Depth | 2 | Color ^ this for the glow. 1 = same as the streaks, higher = deeper and darker |
| | Highlight Confinement | 0.7 | How strictly streaks stay on the bands (0 = anywhere, 1 = only on them) |
| Streams: Sparkles | Sparkle Amount | 0.15 | Roughly what fraction of dashes sparkle |
| | Sparkle Size | 0.3 | How much of the dash front lights up |
| | Sparkle Softness | 0.15 | How gradually sparkles fade in at their edges |
| | Sparkle Brightness | 3 | Sparkle brightness **relative to the stream** (1 = same) |
| | Sparkle Saturation | 0.7 | 1 = the chosen colors, 0 = white-hot |
| | Sparkle Color 1–4 | cyan / green / pink / white-gold | Each sparkle is one of these, at random |
| Streams: Streaks | Stream Count, Streak Softness, Dash Frequency, Dash Length, Brightness | 40 / 0.3 / 6 / 0.3 / 1.5 | As in Radial Streams |
| Streams: Tube | Tube Depth Scale | 0.5 | How long the dashes are along the tunnel |
| | Tube Max Depth | 8 | Where the tunnel is cut off near the center. Higher = reaches closer to it |
| Set by Script | Offset | 0 | Moves the dashes outward |

**Output:** `Color`.

## How it works

### 1. The tunnel: `depth`

`depth` = Min(`along` ÷ √(max(1 − `along`², 0.0001)), Tube Max Depth) × Tube Depth Scale

That's how far down a tunnel the pixel looks (large near the vanishing point, small at the edges). It
replaces `along` in **`phase`**, so dashes are packed tight and slow near the center, and long and fast at
the edges. Everything that uses `phase` (`cycle`, `slot`, the per-dash roll) follows.

### 2. Color: the 4-color loop (`color`)

`hue` (as in Radial Streams) × 4 drives four Lerps: Color 1 → 2 → 3 → 4 → back to 1, each blending by
Saturate(t − k). The loop means Hue Drift cycles forever without a seam. (It replaced a Gradient, which a
material can't show.)

### 3. Glow (`wideNoise`, `wideMask`, `wide`, `glowColor`)

| Group | Nodes | Meaning |
| --- | --- | --- |
| `wideNoise` | Gradient Noise at `circle`, Scale = Stream Count × Wide Scale | Bands that depend on angle only, so they stay put and fill the gaps between dashes |
| `wideMask` | Smoothstep(1 − Wide Coverage, + Wide Softness, wide noise) | Where the bands are |
| `wide` | `wideMask` × center fade × Wide Opacity | The translucent glow. No dashes: motion comes from the streaks on top |
| `glowColor` | Power(color, Glow Depth) × `wide` | A deeper, more saturated version of the stream color. Raising 0–1 channels to a power above 1 darkens the weaker channels most |

### 4. Highlights (`highlights`)

The normal streak mask × Lerp(1, `wideMask`, Highlight Confinement): the streaks mostly sit on top of the
bands, as bright accents.

### 5. Sparkles (`sparkleNoise`, `sparkleSelect`, `sparklePeak`, `sparkleColor`, `sparkles`)

| Group | Nodes | Meaning |
| --- | --- | --- |
| `sparkleNoise` | Gradient Noise at `slotUV` (shifted), Stream Count × 0.5 | A random value per dash, re-rolled with every dash |
| `sparkleSelect` | Smoothstep(0.8 − Sparkle Amount, + Sparkle Softness, value) | Which dashes sparkle, with soft edges |
| `sparklePeak` | Smoothstep(1 − Sparkle Size, 1, `head`) × `tail` | The dash front: from where the fade-in completes, fading out along the tail. Measured from `head` so it works with any softness and dash length |
| `sparkleColor` | A slow noise (also per dash) × 3 → Fraction → soft switches at 0.25 / 0.5 / 0.75 between Sparkle Colors 1–4, then Lerp toward white by Sparkle Saturation | One color per sparkle, no stripes |
| `sparkles` | Lerp(stream color, sparkle color × Sparkle Brightness, select × peak × fade) | **Recolors** the stream there, instead of adding light on top |

**Why recolor instead of add:** the streams can be very bright (Brightness well above 1). Adding a different
hue on top of an already-bright color pushes every channel high, which tonemaps to white. Swapping the color
keeps the sparkle at the stream's brightness, in its own hue.

### 6. Result

`(sparkle-recolored color × highlights) + glowColor` → × Brightness → Output Color.

## Talks to

- **Starry Sky Volume** (shader graph): uses this Sub Graph and passes in every input.
- **SkyboxSpeedController** (indirectly): `_StreamOffset` (→ Offset via Stream Speed) and
  `_StreamIntensity` (fades the whole output in across overdrive).

## Gotchas

- **Extreme stream settings break the soft look's assumptions.** With very high Coverage and Streak
  Softness (for example 10 and 40) the streak mask flattens to a low constant, and you end up compensating
  with a huge Brightness. Colors then wash toward white under the Neutral tonemapping. Prefer Coverage
  0.5–0.9, Softness 0.3–0.6, Brightness 1.5–4.
- **Cost:** 6 Gradient Noise reads per pixel (streak, spoke, color, wide, sparkle select, sparkle color) on
  top of the math. It's still much cheaper than the volume clouds.
- **Tube Max Depth vs Center Fade:** if streaks look busy near the center, lower Tube Max Depth or raise
  Center Fade.
- **Don't regenerate this file.** Starry Sky Volume finds each input by its ID. Edit it in place.
