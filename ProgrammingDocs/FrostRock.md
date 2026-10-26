# Frost Rock

## Purpose

The look of the Frost comet's rock: a plain grey asteroid covered in granular, pale blue-white frost, with a cold
glow around its edge and tiny twinkling glints. One slider, **Frost Amount**, takes it from bare rock to fully
frosted, and it drives the frost, the glow and the glints together.

## Where it lives

- **File:** `Assets/Shaders/Frost Rock.shadergraph` (URP, **Lit**, opaque, front faces)
- **Material:** `Frost Rock Mat`, on the mesh of the `Frost` comet prefab (in place of `Asteroid.mat`).
- **Uses:** the `Triplanar Noise` Sub Graph, three times (see [TriplanarNoise.md](TriplanarNoise.md)).

## Properties

| Property | Reference | Default | What it changes |
| --- | --- | --- | --- |
| Rock Color | `_BaseColor` | grey (0.372, 0.357, 0.343) | The bare rock's colour. AsteroidBehavior's depth tint overwrites it every frame, so the reference name must stay `_BaseColor` |
| Grain Scale | `_Grain_Scale` | 40 | How fine the frost granules are. Higher = smaller, busier grain |
| Grain Threshold | `_Grain_Threshold` | 0.55 (slider 0–1) | How much of the noise counts as a granule. Higher = fewer granules |
| Grain Softness | `_Grain_Softness` | 0.15 (slider 0.01–0.5) | How soft each granule's edge is |
| Patch Scale | `_Patch_Scale` | 3 | How big the frosted areas are. Higher = more, smaller patches |
| Frost Amount | `_Frost_Amount` | 0.6 (slider 0–1) | **The intensity slider.** 0 = bare rock, 1 = fully frosted. Also scales the glow and the sparkle |
| Frost Crust | `_Frost_Crust` | 0.4 (slider 0–1) | How white the frost is between the granules. 0 = only the granules show, 1 = a solid white coat |
| Frost Color | `_Frost_Color` | pale blue-white (0.82, 0.92, 1.0) | The colour of the frost |
| Frost Smoothness | `_Frost_Smoothness` | 0.35 (slider 0–1) | How shiny the frosted parts are. Bare rock stays matte |
| Tint | `_Tint` | white | The depth tint. AsteroidBehavior sets it every frame (white near the player, `farColor` far away). White = no change |
| Rim Power | `_Rim_Power` | 3 | How thin the edge glow is. Higher = hugs the silhouette more tightly |
| Rim Strength | `_Rim_Strength` | 1.5 | How bright the edge glow is |
| Glow Color | `_Glow_Color` | cold blue (0.35, 0.75, 1.0), HDR | The glow's colour. Above 1 it blooms if Bloom is on |
| Sparkle Scale | `_Sparkle_Scale` | 150 | How small the glints are. Higher = tinier and more of them |
| Sparkle Threshold | `_Sparkle_Threshold` | 0.62 (slider 0–1) | How rare the glints are. Higher = fewer |
| Sparkle Speed | `_Sparkle_Speed` | 2 | How fast the glints blink |
| Sparkle Strength | `_Sparkle_Strength` | 4 | How bright the glints are. Above 1 they bloom if Bloom is on |

## How it works

Laid out in the order the values are built, one group per value.

### `rock`

| Nodes | Meaning |
| --- | --- |
| Rock Color | The bare rock's colour: the same grey the old asteroid material had |

### `noise` and `grain`

| Value | Nodes | Meaning |
| --- | --- | --- |
| `noise` | Triplanar Noise(Scale = Grain Scale) | Fine noise (about 0–1) on the rock's surface, in object space, so it sticks to the rock as it tumbles and has no seams |
| `grain` | Smoothstep(Grain Threshold, Grain Threshold + Grain Softness, `noise`) | The speckle mask: 0 below the threshold, 1 above it plus the softness, a smooth ramp between |

Example: Threshold 0.55, Softness 0.15. Noise 0.50 gives 0 (bare), 0.62 gives about 0.46 (a soft half-speck), 0.75
or more gives 1 (a full speck).

### `coarse` and `patches`

| Value | Nodes | Meaning |
| --- | --- | --- |
| `coarse` | Triplanar Noise(Scale = Patch Scale) | Big, slow noise over the rock |
| `patches` | Smoothstep(0.35, 0.65, `coarse`) | Where the frost is thick (1) or thin (0). The two edge values are typed into the node, so they are not properties |

### `cover`

| Nodes | Meaning |
| --- | --- |
| Saturate(`patches` − (1 − Frost Amount) + Frost Amount) | How much frost a spot gets. It equals `patches + 2 × Amount − 1`, kept between 0 and 1 |

At Amount 0 it is 0 everywhere (bare rock). At 0.5 it is `patches`. At 1 it is 1 everywhere (fully frosted). It is
built from One Minus, Subtract and Add so no number is typed into a node.

### `frost`

| Nodes | Meaning |
| --- | --- |
| `cover` × Lerp(Frost Crust, 1, `grain`) | The frost strength at each point. Inside a covered area a granule is full strength, and between granules there is still a thin haze at the Crust level. The 1 comes from a connected "One (constant)" node |

Example: Amount 0.6, `patches` 0.5, Crust 0.4: `cover` is 0.7, so a granule gives 0.7 and the space between gives 0.28.

### `finish`

| Nodes | Meaning |
| --- | --- |
| Lerp(`rock`, Frost Color, `frost`) → Base Color | Bare rock where `frost` is 0, frost colour where it is 1 |
| `frost` × Frost Smoothness → Smoothness | Frosted parts get a little shine, bare rock stays matte |

### `depth`

| Nodes | Meaning |
| --- | --- |
| (the finished colour) × Tint → Base Color | The colour, darkened by distance |

AsteroidBehavior's depth tint sets both `_BaseColor` and `_Tint` on every material slot of a comet. `_BaseColor` is
already the rock colour, so `rock` darkens by itself. Without Tint, the frost colour would stay bright on a far-away
comet. The glow and the sparkle are also multiplied by Tint, for the same reason.

### `rim`

| Nodes | Meaning |
| --- | --- |
| Fresnel Effect(Power = Rim Power) × Rim Strength × Frost Amount | 0 where the surface faces the camera, up to its full strength at the silhouette. At Amount 0 there is no glow |

### `glow`

| Nodes | Meaning |
| --- | --- |
| Glow Color × `rim` × Tint | The cold glow around the edge, added into **Emission** (light the surface gives off, so it shows even in shadow) |

### `flecks`

| Nodes | Meaning |
| --- | --- |
| Triplanar Noise(Scale = Sparkle Scale) → Smoothstep(Sparkle Threshold, Threshold × 1.12, noise) | Sparse tiny points: only the highest peaks of the noise pass. The 1.12 is typed into a Multiply node |

### `twinkle`

| Nodes | Meaning |
| --- | --- |
| Saturate(Sine(`noise` × 40 + Time × Sparkle Speed)) | Each fleck blinks on its own rhythm. Its noise value shifts its phase, so neighbours do not blink together. The 40 is typed into a Multiply node |

### `sparkle`

| Nodes | Meaning |
| --- | --- |
| `flecks` × `twinkle` × `frost` × Sparkle Strength × Tint, added to `glow` → Emission | The glints. `frost` is in there so glints only appear where there is frost, and Frost Amount scales them |

## Output

- **Base Color** = Lerp(`rock`, Frost Color, `frost`) × Tint
- **Smoothness** = `frost` × Frost Smoothness
- **Emission** = `glow` + `sparkle`
- Metallic 0, no normal map.

## Talks to

- **AsteroidBehavior**: sets `_BaseColor` and `_Tint` on the comet's renderer every frame for the depth tint. See
  [AsteroidBehavior.md](AsteroidBehavior.md).
- **Triplanar Noise** Sub Graph: used three times (grain, patches, sparkle). See [TriplanarNoise.md](TriplanarNoise.md).
- **The `Frost` comet prefab**: `Frost Rock Mat` goes on its mesh child.

## Gotchas

- **Keep the reference names `_BaseColor` and `_Tint`.** AsteroidBehavior sets them by name. If either is renamed,
  the comet stops darkening with distance.
- **Nine noise reads per pixel** (three Triplanar Noise uses, three reads each). Fine for a small object on screen,
  but worth knowing if the shader is ever used on something large.
- **The noise is in object space.** Its size depends on the mesh's own units, so a bigger or smaller mesh makes the
  grain look coarser or finer. Tune Grain Scale and Sparkle Scale by eye.
- **Frost Amount drives three things at once** (coverage, glow and sparkle), so it cannot be turned down for just one.
- **The patch edge values (0.35 and 0.65), the Sparkle 1.12 and the twinkle 40 are typed into nodes**, not
  properties. Change them in the graph if the look needs it.
- **Glow Color and Sparkle Strength only bloom** if Bloom is on in the project.
