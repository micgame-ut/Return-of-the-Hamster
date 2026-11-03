# Frost Trail

## Purpose

The material for the ribbon behind the Frost comet: a soft-edged band of pale blue-white with streaks flowing along
it. The ribbon's own **Color gradient** fades it out toward the tail.

## Where it lives

- **File:** `Assets/Shaders/Frost Trail.shadergraph` (URP, **Unlit**, **transparent**, **alpha** blending, depth write
  off, front faces)
- **Material:** `Frost Trail Mat`, on the **Trail Renderer** of the `Frost Trail` child of the `Frost` comet prefab.
- **Needs:** a **Trail Renderer** (or another mesh with UVs along its length and vertex colors). The shader reads the
  UV and the Vertex Color, which a Trail Renderer supplies.

## Properties

| Property | Reference | Default | What it changes |
| --- | --- | --- | --- |
| Edge Softness | `_Edge_Softness` | 0.6 (slider 0.01–1) | How much of the width fades at the edges. Low = a solid ribbon with sharp sides, 1 = fades from the centre |
| Streak Stretch | `_Streak_Stretch` | 4 | How much longer the streaks are than they are wide. Higher = longer streaks |
| Streak Detail | `_Streak_Detail` | 8 | How many streaks fit across the width. Higher = finer, more streaks |
| Streak Speed | `_Streak_Speed` | 0.5 | How fast the pattern slides along the ribbon. Negative = the other way, 0 = frozen |
| Streak Strength | `_Streak_Strength` | 0.8 (slider 0–1) | How strongly the streaks cut into the ribbon. 0 = a plain band |
| Trail Color | `_Trail_Color` | pale blue-white (0.8, 0.93, 1.0), HDR | The ribbon's colour. Above 1 it blooms if Bloom is on |
| Trail Density | `_Trail_Density` | 1 (slider 0–2) | How solid the ribbon is overall. 0 = invisible, 2 = very solid |

## How it works

Laid out in the order the values are built, one group per value.

### `across`

On a Trail Renderer, the UV's **V** runs across the width of the ribbon (0 at one side, 1 at the other) and **U**
runs along its length.

| Nodes | Meaning |
| --- | --- |
| UV → Split (V) → × 2 → − 1 → Absolute | The distance from the ribbon's centre line: 0 along the middle, 1 at either edge. The "− 1" uses a connected "One (constant)" node |
| Smoothstep(0, Edge Softness, 1 − that) | `across`: a solid core with soft edges |

Example, Edge Softness 0.6: V = 0.5 (the middle) gives 1. V = 0.8 gives `1 − 0.6 = 0.4`, which the smoothstep makes
about 0.74. V = 1 (the very edge) gives 0.

### `flow`

| Nodes | Meaning |
| --- | --- |
| U ÷ Streak Stretch + Time × Streak Speed, combined with V | A coordinate pair for the noise. Dividing U by the stretch makes features longer than they are wide, and the Time term slides the whole pattern along the ribbon |

### `streaks`

| Nodes | Meaning |
| --- | --- |
| Gradient Noise(`flow`, Scale = Streak Detail) | Noise (about 0–1) over the stretched, sliding coordinates |
| 1 − (1 − noise) × Streak Strength | `streaks`: 1 where there is no cut, lower where the noise is low. At Strength 0 it's 1 everywhere |
| `across` × `streaks` | The ribbon's shape and streaks together |

Example, Strength 0.8: noise 0.9 gives 0.92 (nearly solid), noise 0.2 gives `1 − 0.8 × 0.8 = 0.36` (a thin, see-through
streak).

### `trail`

| Nodes | Meaning |
| --- | --- |
| Trail Color × Vertex Color → **Base Color** | The colour. The Trail Renderer's **Color gradient** arrives as the Vertex Color, so it tints the ribbon |
| (`across` × `streaks`) × Vertex Color alpha × Trail Density → **Alpha** | How see-through each point is. A gradient alpha of 0.3 near the tail, `across` 1, `streaks` 0.8, Density 1 gives 0.24 |

## Output

- **Base Color** = Trail Color × the ribbon's vertex colour. **Alpha** = `across` × `streaks` × the vertex alpha × Trail
  Density.
- **Transparent**, alpha blending, depth write off, so the ribbon blends with what's behind it.

## The Trail Renderer settings it expects

These are on the prefab's Trail Renderer, not in the graph:

- **Texture Mode: Stretch**, so U runs 0 to 1 along the whole ribbon. In Tile mode the streaks repeat instead.
- **Color gradient:** the fade. Alpha 0.9 at the head, 0.6 at 35% and 0 at the tail, with the colour going from white
  to a slightly cooler blue-white. With alpha 0 at the end, the ribbon just fades to nothing.
- **Width:** a flat width curve (0.8 × the Width Multiplier). The Width Multiplier is in world units, so it has to be
  changed along with the prefab's scale.
- **Time:** set from `patchLifetime` by AsteroidBehavior when the comet spawns, so the ribbon lasts as long as a patch.
- **Shadows off**, since it's unlit.

## Talks to

- **A Trail Renderer** (the `Frost Trail` child of the Frost comet): supplies the UV and the vertex colour. See
  [AsteroidBehavior.md](AsteroidBehavior.md) (the `ribbon` field).
- **FrostRock / FrostGas**: the other two Frost materials. They share the pale blue-white palette. See
  [FrostRock.md](FrostRock.md) and [FrostGas.md](FrostGas.md).

## Gotchas

- **Needs a ribbon.** On a normal mesh the Vertex Color is white and the UV isn't along the length, so the gradient
  fade and the streaks won't look right.
- **Keep Texture Mode on Stretch.** The streaks are stretched along U assuming it runs the length of the ribbon.
- **The streaks' size depends on the ribbon's length.** With Stretch, a longer ribbon (a higher Time, or a faster
  comet) stretches the same streaks over a greater distance. Tune Streak Stretch by eye.
- **If the streaks slide the wrong way,** make Streak Speed negative.
- **Trail Color only blooms** if its brightness is above 1 and Bloom is on in the project.
- **A ribbon comes from a pooled comet.** If a respawned comet ever draws a streak across the screen from its old
  position, the Trail Renderer needs a `Clear()` when it's reused. It hasn't been needed so far.
