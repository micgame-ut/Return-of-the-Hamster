# Radial Streams

## Purpose

A Sub Graph that draws warp-speed streaks on the skybox: soft, colored dashes of light that spread
outward from a vanishing point, as if the player is flying through hyperspace. It outputs a color to
**add** onto the sky (black where there's no streak).

## Where it lives

- **File:** `Assets/Shaders/Radial Streams.shadersubgraph`
- **Used by:** the `Starry Sky` shader graph, twice (layer 1 and layer 2). See [StarrySky.md](StarrySky.md).
  Starry Sky Volume uses **Tube Streams** instead, a copy of this Sub Graph with tunnel-depth dashes, a glow
  layer and sparkles. See [TubeStreams.md](TubeStreams.md).
- **Blackboard:** inputs are grouped into sections (Sky, Streams: Shared, Streams: Color, Streams: Streaks,
  Set by Script).
- **Values set at runtime:** `Offset` comes from `_StreamOffset`, which SkyboxSpeedController grows every
  frame. Everything else is set on `StarrySkyMat` through Starry Sky's properties.

## Inputs

The defaults below are the Sub Graph's own, used only for its node previews. The real values come from
Starry Sky.

| Input | Type | Default | What it changes |
| --- | --- | --- | --- |
| Stream Center | Vector 3 | (0, 0, 1) | Direction of the vanishing point (world space) |
| Stream Count | Float | 40 | How many streaks around the circle. Lower = fewer, wider streaks |
| Coverage | Float | 0.5 | How much of the circle has streaks (0 = none, 1 = nearly all) |
| Streak Softness | Float | 0.3 | How gradually streak edges fade in. Higher = softer, dimmer, more brightness variety |
| Dash Frequency | Float | 6 | How many dashes repeat along each streak |
| Dash Length | Float | 0.3 | Length of each dash's fading tail, as a fraction of one repeat |
| Head Softness | Float | 0.1 | Length of each dash's fade-in at the front. Keep it well under Dash Length |
| Dash Scatter | Float | 10 | How much neighbouring streaks' dashes are shifted apart. 0 = dashes line up into rings |
| Slot Offset | Vector 2 | (2.3, 1.3) | How far the noise moves for each dash, so each dash gets its own random roll |
| Offset | Float | 0 | Running total that moves the dashes outward (set by script) |
| Center Fade | Float | 0.02 | How far from the vanishing point the streaks fade out (in `1 − along` units) |
| Stream Gradient | Gradient | — | The palette the streak colors are picked from. Should loop (same color at both ends) |
| Color Scale | Float | 3 | Size of the color regions. Lower = bigger regions |
| Hue Spread | Float | 3 | How many times the gradient repeats across the color noise. Lower = calmer color changes |
| Hue Offset | Vector 2 | (12.3, 45.6) | Which area of the noise the colors (and dash shifts) come from |
| Hue Drift | Float | 0.02 | How fast the palette cycles over time (0.02 = one full cycle every 50 seconds) |
| Brightness | Float | 1.5 | Overall brightness. Above 1 lets Bloom make the streaks glow |

**Output:** `Color` (Vector 3), the streak color to add onto the sky.

## How it works

Every value is built in its own group in the graph, named as below. The groups are described in the
order the logic flows.

### 1. Directions: `dir`, `c`, `right`, `up`

| Group | Nodes | Meaning |
| --- | --- | --- |
| `dir` | View Direction (World) → Negate → Normalize | The direction this pixel of the sky is in, looking out from the camera |
| `c` | Stream Center → Normalize | The direction of the vanishing point |
| `right` | Cross(`c`, (0, 1, 0)) → Normalize | A direction at right angles to `c`: the clock face's 3 o'clock |
| `up` | Cross(`right`, `c`) | The other direction at right angles to `c`: the clock face's 12 o'clock |

View Direction points from the sky toward the camera, so Negate flips it to point outward.

`right` and `up` are needed to measure **angle around the vanishing point**. `c` alone can only say how
close a pixel is to the center, not which way around it.

### 2. Position around the center: `along`, `circle`

| Group | Nodes | Meaning |
| --- | --- | --- |
| `along` | Dot(`dir`, `c`) | How close the pixel is to the vanishing point: 1 at the center, 0 at 90°, −1 directly behind |
| `circle` | Combine(Dot(`dir`, `right`), Dot(`dir`, `up`)) → Normalize | The pixel's clock position around the center, as a 2D point on a circle |

The Normalize in `circle` is what makes streaks straight. Without it, the 2D point would get longer
further from the center, so points on the same spoke would read different noise and the streak would
bend and flicker. Normalizing sets the length to 1, so every point on a spoke gets the same value.

### 3. Where each dash is: `hueUV`, `spokeNoise`, `desync`, `phase`, `cycle`, `slot`

| Group | Nodes | Meaning |
| --- | --- | --- |
| `hueUV` | `circle` + Hue Offset | `circle` moved to a separate area of the noise |
| `spokeNoise` | Gradient Noise(`hueUV`, Stream Count) | A random value per spoke, unrelated to whether the spoke is lit |
| `desync` | `spokeNoise` × Dash Scatter | How far this spoke's dash pattern is shifted |
| `phase` | `along` × Dash Frequency + Offset + `desync` | Position in the repeating dash pattern |
| `cycle` | Fraction(`phase`) | Position **inside** one dash repeat, 0–1 |
| `slot` | Modulo(Floor(`phase`), 256) | **Which** dash this is, as a whole number |

**Movement.** As Offset grows, a fixed `phase` value is found at a smaller `along`, which means further
from the center. So dashes move outward. At the point directly behind the camera (`along` = −1), the
same math makes dashes move inward. That point is never seen.

**Why `desync` uses `spokeNoise`, not the streak noise.** A streak only shows where its noise is high,
so every visible streak has nearly the same noise value. Shifting by that would barely shift anything,
and the dashes would line up into rings. `spokeNoise` reads a different area of the noise, so visible
streaks get the full range of shifts.

**Why the Modulo.** Offset grows forever. Without wrapping, the noise position in `slotUV` (`slot` ×
Slot Offset × Stream Count) reaches hundreds of thousands within an hour, where a `float` can't store
small differences anymore and the streaks go blocky. Modulo 256 relabels the dashes 0–255, so the noise
position stays small. A dash keeps its label for its whole life, so nothing visibly changes.

### 4. Which spokes are lit: `slotUV`, `noise`, `threshold`, `streak`

| Group | Nodes | Meaning |
| --- | --- | --- |
| `slotUV` | `circle` + `slot` × Slot Offset | A different noise area for each dash |
| `noise` | Gradient Noise(`slotUV`, Stream Count) | Random value per spoke, rolled again for every dash |
| `threshold` | One Minus(Coverage) | The lowest noise value that counts as a streak |
| `streak` | Smoothstep(`threshold`, `threshold` + Streak Softness, `noise`) | 0 on empty sky, rising to 1 on a streak |

Gradient Noise is a smooth landscape of hills, and `circle` walks a path around it. Hills above
`threshold` become streaks. Because each `slot` reads a different area, a spoke that's dark for one dash
can be lit for the next. Without that, the same spokes would be lit forever and some areas would never
get streaks.

Higher Coverage lowers `threshold`, so more of each hill passes and there are more, wider streaks.

**Streak Softness** is the length of the Smoothstep ramp. A long ramp gives soft edges, and also means
only the tallest hills reach full brightness, so streaks vary in brightness. If `threshold` + Streak
Softness goes above about 0.9, almost nothing reaches full brightness, because Gradient Noise rarely
goes that high.

### 5. Dash shape: `head`, `tail`, `dash`

| Group | Nodes | Meaning |
| --- | --- | --- |
| `head` | Smoothstep(0, Head Softness, `cycle`) | Fades in at the front of the dash |
| `tail` | Smoothstep(Dash Length, 0, `cycle`) | Fades out toward the back (edges reversed, so it ramps down) |
| `dash` | `head` × `tail` | The whole dash: soft front, long fading tail |

Small `cycle` is the outer end of each dash, the end leading the outward movement. Both ramps are 0 at
the slot boundaries, so the jump from one slot's noise to the next is never visible.

With Head Softness 0.1 and Dash Length 0.3:

| `cycle` | 0 | 0.05 | 0.1 | 0.2 | 0.3+ |
| --- | --- | --- | --- | --- | --- |
| `head` | 0 | 0.5 | 1 | 1 | 1 |
| `tail` | 1 | 0.93 | 0.74 | 0.26 | 0 |
| `dash` | 0 | 0.46 | 0.74 | 0.26 | 0 |

### 6. Center fade: `distance`, `fade`

| Group | Nodes | Meaning |
| --- | --- | --- |
| `distance` | One Minus(`along`) | 0 at the vanishing point, growing outward |
| `fade` | Smoothstep(0, Center Fade, `distance`) | 0 at the center, 1 once `distance` reaches Center Fade |

At the vanishing point every spoke squeezes into a few pixels and would flicker. The fade hides that.
`distance` follows the angle from the center: Center Fade 0.02 is about 11°, 0.1 about 26°, and 0.5 is
60°.

### 7. `mask`

`mask` = `streak` × `dash` × `fade`. A pixel is lit only if it's on a streak **and** in a dash **and**
outside the center. Multiplying 0–1 values works like `&&`.

### 8. Color: `colorNoise`, `hueTime`, `hue`, `color`

| Group | Nodes | Meaning |
| --- | --- | --- |
| `colorNoise` | Gradient Noise(`hueUV`, Color Scale) | A slowly changing value around the circle, so neighbouring streaks share colors |
| `hueTime` | Time × Hue Drift | How far the palette has cycled so far |
| `hue` | Fraction(`colorNoise` × Hue Spread + `hueTime`) | Position in the gradient, 0–1 |
| `color` | Sample Gradient(Stream Gradient, `hue`) | The streak's color |

Gradient Noise rarely goes near 0 or 1, so multiplying by Hue Spread and wrapping with Fraction spreads
it across the whole gradient. Fraction wraps 0.99 to 0, so the gradient should start and end on the same
color, or there's a hard line where the ends meet.

Color only depends on angle and time, not on `slot`. Consecutive dashes look like different colors only
because they light different angles.

`hueTime` uses Unity's shader Time, which follows `Time.timeScale`, so colors freeze while paused.

### 9. `result`

`result` = `color` × `mask` × Brightness, sent to the `Color` output.

## Talks to

- **Starry Sky** (shader graph): uses this Sub Graph twice and passes in all the inputs.
- **SkyboxSpeedController** (indirectly): sets `_StreamOffset` on the skybox material, which Starry Sky
  passes in as `Offset`.

## Gotchas

- **Stream Center must not point straight up or down.** `right` comes from a cross product with (0, 1, 0).
  If `c` is parallel to it, the result is zero, Normalize divides by zero, and the streaks break.
- **Stream Center depends on which camera draws the skybox.** In TestGame that's **Earth Camera**, which
  faces −Z, so the material uses (0, 0, −1). Main Camera is an overlay. If streaks flow inward, the
  vanishing point is behind the skybox camera.
- **Head Softness must stay well under Dash Length.** Otherwise the head is still fading in when the tail
  has already faded out, and the dashes become faint smudges.
- **Desync must use a noise that doesn't change over time.** If `spokeNoise` moved, every dash's shift
  would change mid-flight and the dashes would wobble.
- **Zero defaults break previews.** Inside the Sub Graph, a 0 default for Stream Center, Center Fade or
  Coverage makes previews black (divide by zero, or nothing above the threshold). The game isn't affected,
  because Starry Sky passes its own values.
