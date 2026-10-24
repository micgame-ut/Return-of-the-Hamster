# Asteroid Phase

## Purpose

The hologram look of a Phase asteroid while it's phased out (and during its warning flicker): a hollow,
see-through glow that's bright around the silhouette, with thin scan lines drifting upward. It tells the player
the asteroid can't hit them right now.

## Where it lives

- **File:** `Assets/Shaders/Asteroid Phase.shadergraph` (URP, **Unlit**, **transparent**, **additive**
  blending, front faces, no shadows)
- **Material:** an `Asteroid Phase` material made from it, assigned to the SpawnableTemplate's
  `phasedMaterial`. AsteroidBehavior swaps a Phase asteroid's materials to it while phased (see
  [AsteroidBehavior.md](AsteroidBehavior.md)).

## Properties

| Property | Reference | Default | What it changes |
| --- | --- | --- | --- |
| Holo Color | `_Holo_Color` | light blue (0.3, 0.8, 1), HDR | The hologram's color. Above 1 glows with Bloom |
| Rim Power | `_Rim_Power` | 2 | How tightly the glow hugs the silhouette. Higher = thinner rim |
| Rim Strength | `_Rim_Strength` | 2 | How bright the rim is |
| Line Density | `_Line_Density` | 8 | Scan lines per world unit, going up |
| Scroll Speed | `_Scroll_Speed` | 1 | How fast the lines move upward, in line spacings per second |
| Line Strength | `_Line_Strength` | 0.6 | How bright the lines are |
| Opacity | `_Opacity` | 0.7 (slider 0–1) | Overall strength of the whole effect |
| Tint | `_Tint` | white | The depth tint. AsteroidBehavior sets it every frame (white near the player, `farColor` far away). White = no change |

## How it works

Laid out in the order the values are built, one group per value.

### `rim`

| Nodes | Meaning |
| --- | --- |
| Fresnel Effect(Power = Rim Power) × Rim Strength | Bright where the surface turns away from the camera (the silhouette), fading to 0 where it faces you |

The Fresnel Effect compares the surface's direction with the direction to the camera: facing the camera gives
0, edge-on gives 1. Power sharpens that falloff, so higher values squeeze the glow into a thinner rim. With the
middle dark and the edges bright, the shape reads as hollow.

### `scan`

| Step | Nodes | Meaning |
| --- | --- | --- |
| Height | Position (**World**) → Split → G | How high this point is in the world |
| Bands | height × Line Density + Time × Scroll Speed | Counts up 8 per world unit, sliding upward over time |
| Repeat | Fraction | Keeps only the part after the decimal point, so each band ramps 0 → 1 and starts again |
| Lines | Smoothstep(0.8, 1) | Turns each ramp into a thin line at its top end |
| Strength | × Line Strength | How bright the lines are |

**World** space keeps the lines level and steady while the rock tumbles (only the mesh spins), like a
projected hologram. With Line Density 8, lines are 1/8 = 0.125 world units apart, and each is the top 20% of
its band.

### `holo`

| Nodes | Meaning |
| --- | --- |
| Holo Color × (`rim` + `scan`) × Opacity → **Base Color** | The final glow |

### `depth`

| Nodes | Meaning |
| --- | --- |
| `holo` × Tint → **Base Color** | The glow, darkened by distance |

Tint is a Color property, white by default, so the graph looks the same as before until something sets it.
AsteroidBehavior sets `_Tint` on the renderer every frame with a property block, using the same depth tint it
applies to the normal asteroid material (a color between white and the template's `farColor`). Because the
blending is additive, a darker color adds less light: far holograms are dimmer. The `depth` group sits below
`holo`; the Multiply is the last node before the output.

### Output

- **Base Color** = `holo` × Tint (the `depth` group). Alpha is 1.
- **Additive** blending adds the color on top of what's behind it, so black means invisible and brighter means
  more glow. That's why there's no separate transparency value: Opacity scales the color itself.

## Talks to

- **AsteroidBehavior**: swaps a Phase asteroid's materials to this one (through the SpawnableTemplate's
  `phasedMaterial`) while it's phased and during the flicker, and sets `_Tint` on the renderer every frame
  for the depth fade.

## Gotchas

- **Assign the material on the SpawnableTemplate's `phasedMaterial`.** Without it, Phase asteroids still turn
  their collider off but never change look.
- **Additive makes it vanish on bright backgrounds.** It only adds light, so it shows best against dark space.
- **Line Density is per world unit.** On a much bigger or smaller asteroid the lines will look denser or sparser.
- **It glows only with Bloom** if Holo Color is pushed above 1.
- **Don't change the `Tint` reference name.** AsteroidBehavior sets it by the name `_Tint`. If the property is
  renamed or hidden, the hologram simply stops darkening with distance.
