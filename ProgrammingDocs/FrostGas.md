# Frost Gas

## Purpose

The particle material for the Frost comet's cold mist and fine dust: a soft, ragged-edged puff of gas. The same
shader draws both layers, from two materials with different colours.

## Where it lives

- **File:** `Assets/Shaders/Frost Gas.shadergraph` (URP, **Unlit**, **transparent**, **alpha** blending, depth write
  off, front faces)
- **Materials:** `Frost Gas Mat` (the pale blue-white mist, on the `Frost Clouds` system) and `Ice Specks Mat` (the
  icy cyan dust, on `Frost Dust`, a child system of `Frost Clouds`). Each goes on the Renderer of a Particle System
  on the `Frost` comet prefab.
- **Needs:** a Particle System renderer. The shader reads the particle's UV and Vertex Color, which only a particle
  (or mesh with vertex colors) supplies.

## Properties

| Property | Reference | Default | What it changes |
| --- | --- | --- | --- |
| Blob Softness | `_Blob_Softness` | 0.8 (slider 0.01–1) | How soft the puff's edge is. Low = a hard disc, 1 = fades from the very centre |
| Wisp Scale | `_Wisp_Scale` | 3 | How big the ragged detail on the edge is. Higher = finer, busier edges |
| Wisp Speed | `_Wisp_Speed` | 0.1 | How fast the edge pattern drifts. 0 = frozen |
| Wisp Erosion | `_Wisp_Erosion` | 0.6 (slider 0–1) | How much the noise eats the puff. 0 = a clean soft disc, 1 = heavily broken up |
| Gas Color | `_Gas_Color` | pale blue-white (0.78, 0.92, 1.0), HDR | The colour of the gas. Above 1 it blooms if Bloom is on |
| Gas Density | `_Gas_Density` | 1 (slider 0–2) | How thick the cloud is. 0 = invisible, 2 = very dense |

## How it works

Laid out in the order the values are built, one group per value.

### `blob`

| Nodes | Meaning |
| --- | --- |
| UV → Split → Combine (X, Y) → × 2 → − 1 → Length | The distance of each point from the middle of the particle: 0 in the centre, 1 at the edge of the square |
| Smoothstep(0, Blob Softness, 1 − distance) | A soft round puff: 1 in the middle, fading to 0 at the edge |

The "− 1" uses a connected "One (constant)" node. Example: at the centre the distance is 0, so `blob` is 1. Halfway
out, distance 0.5 gives `1 − 0.5 = 0.5`, and with softness 0.8 that is about 0.77. At the edge it is 0.

### `wisp`

| Nodes | Meaning |
| --- | --- |
| Gradient Noise(UV + Time × Wisp Speed, Scale = Wisp Scale) | Slow, drifting noise across the particle (about 0–1) |

### `shape`

| Nodes | Meaning |
| --- | --- |
| Saturate(`blob` − (1 − `wisp`) × Wisp Erosion) | The puff with its edge eaten by the noise. Where `wisp` is high little is removed, where it is low up to Wisp Erosion is |

Example, Erosion 0.6: a spot with `blob` 0.77 and `wisp` 0.5 loses 0.3 and ends at 0.47. A spot near the rim with
`blob` 0.2 and the same noise goes to 0, which cuts a notch in the edge. The centre (`blob` 1) always keeps at least
0.4. Every puff has the same noise pattern, so the variety comes from each particle's random rotation (set Start
Rotation to a random range in the Particle System).

### `gas`

| Nodes | Meaning |
| --- | --- |
| Gas Color × Vertex Color → **Base Color** | The colour. The Vertex Color is the particle's own colour, so the Particle System's Start Color and Color over Lifetime tint and fade the cloud |
| `shape` × Vertex Color alpha × Gas Density → **Alpha** | How see-through each point is. Alpha 0.5 from Color over Lifetime, `shape` 0.8, Density 1 gives 0.4 |

## Output

- **Base Color** = Gas Color × particle colour. **Alpha** = `shape` × particle alpha × Gas Density.
- **Transparent**, alpha blending, depth write off, so overlapping puffs blend into a cloud instead of cutting each
  other out.

## Talks to

- **A Particle System renderer** (on the `Frost` comet prefab): supplies the UV and the particle colour. See
  [AsteroidBehavior.md](AsteroidBehavior.md) for how the comet stops its system when it retires.

## Gotchas

- **Needs a particle.** On a normal mesh the Vertex Color is white, so the colour and alpha controls from the Particle
  System do nothing.
- **Set Start Rotation to random**, or every puff has the same ragged edge.
- **The two materials differ only in their values**, not in a second graph. `Frost Gas Mat`: Gas Color (0.78, 0.92,
  1.0), Wisp Erosion 0.55, Blob Softness 0.7, Gas Density 1. `Ice Specks Mat`: Gas Color icy cyan (0.45, 0.9, 1.0),
  Wisp Erosion 0.55, Blob Softness 0.7. Change a colour on the material, not the graph.
- **Gas Color multiplies the particle system's own colour.** The mist's Color over Lifetime (white to a cooler
  blue-white) tints it too, so the final colour is the product of the two.
- **Alpha blending sorts by distance**, so puffs very close together can swap draw order. Particle sorting is off by
  default; turn on Sort Mode: By Distance in the Renderer if puffs flicker through each other.
- **Gas Color only blooms** if its brightness is above 1 and Bloom is on in the project.
