# Asteroid Rock

## Purpose

A Lit shader for ordinary asteroids that adds a little surface texture to a flat color: a fine rocky speckle plus
coarser darker patches. Make one material per color from it, and list those materials in the SpawnableTemplate's
`asteroidMaterials` so each asteroid picks one at random.

## Where it lives

- **File:** `Assets/Shaders/Asteroid Rock.shadergraph` (URP, Lit, opaque, smoothness 0, metallic 0)
- **Materials:** `Assets/Materials/Asteroid Rock Gray.mat` (the first one, with the old Asteroid material's gray). Copy it
  and change `Base Color` for each new color.
- **Used by:** AsteroidBehavior, which swaps one of the template's materials into each ordinary asteroid. See
  [AsteroidBehavior.md](AsteroidBehavior.md).

## Properties

| Property | Reference | Default | What it changes |
| --- | --- | --- | --- |
| Base Color | `_BaseColor` | gray (0.37, 0.36, 0.34) | The rock's main color. This is the one to change per material. |
| Grain Scale | `_Grain_Scale` | 6 | Size of the fine speckle. Higher = smaller, busier speckle |
| Pit Scale | `_Pit_Scale` | 2.5 | Size of the darker patches. Higher = smaller patches |
| Grain Strength | `_Grain_Strength` | 0.25 | How much the speckle lightens the rock. 0 = none |
| Pit Strength | `_Pit_Strength` | 0.35 | How much the patches darken the rock. 0 = none |

## How it works

Built in this order, each value in a group of the same name.

### `grain`

A `Triplanar Noise` Sub Graph (see [TriplanarNoise.md](TriplanarNoise.md)) at `Grain Scale`: a 0–1 value that varies
quickly across the surface. Triplanar means it uses the object-space position instead of UVs, so there are no seams
or stretching on the asteroid mesh.

### `pits`

The same Sub Graph at `Pit Scale`: a 0–1 value that varies slowly, so it makes big soft patches.

### `shade`

A brightness multiplier made from the two noises:

`shade = (1 − pits × Pit Strength) + grain × Grain Strength`

With the defaults it goes from 0.65 (inside a pit, no grain) to 1.25 (no pit, full grain), and averages about 0.95.
Only Multiply, One Minus and Add nodes are used, so there are no typed-in constants to lose.

### `color`

`Base Color × shade` goes to the Base Color output. Smoothness is 0 and Metallic is 0 (matte, like the old Asteroid
material). Lighting still comes from the scene, since it's a Lit graph.

## Talks to

- **AsteroidBehavior**: reads `_BaseColor` from each material slot at `Awake` and writes it back through a
  MaterialPropertyBlock for the depth fade, so the Base Color property **must keep the reference `_BaseColor`**.
- **SpawnableTemplate**: holds the `asteroidMaterials` list these materials go in.
- **Triplanar Noise**: provides both noise values.

## Gotchas

- **Keep the `_BaseColor` reference.** Renaming it breaks the depth fade for every asteroid using this shader.
- **The texture is in object space.** A scaled-up asteroid gets bigger speckle too.
- **Avoid green materials.** The destructible outline is green and would be hard to see on a green rock.
- **Frost, Magnet and Bonus asteroids don't use this**: they keep their own materials.
