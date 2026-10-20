# Ring Glow

## Purpose

The material on the player's ring (and on each twin copy's ring). It shows the boost cooldown: right after a
boost the ring doesn't glow, the glow **rises** steadily as the cooldown counts down, and when the boost is
ready again it jumps to a **full glow**. It also fades to see-through toward a chosen axis through the
mesh's middle (by default the y axis, pole to pole on the preview sphere), and is solid away from it.

## Where it lives

- **File:** `Assets/Shaders/Ring Glow.shadergraph` (URP, **Lit**, **transparent** with Alpha blending, front
  faces)
- **Material:** a `Ring Glow` material made from it, on the `ring` object in the `hamter` and `Twin Copy`
  prefabs. (The ring used to use URP's built-in default `Lit` material, which is shared by many objects and
  lives inside the URP package, so it couldn't be changed.)
- **Driven by:** BoostRing, which sets **Charge** every frame it changes, from PlayerMovement's
  `BoostCharge01`.

## Properties

| Property | Reference | Default | What it changes |
| --- | --- | --- | --- |
| Base Color | `_Base_Color` | light grey (0.8) | The ring's normal, lit color |
| Glow Color | `_Glow_Color` | cyan (0, 0.9, 1), HDR | The color of the glow |
| Charging Glow | `_Charging_Glow` | 0.5 | How bright the glow gets just before the boost is ready. It rises from 0 to this during the cooldown |
| Ready Glow | `_Ready_Glow` | 3 | The full glow once the boost is ready. Above 1, Bloom makes it shine |
| Charge | `_Charge` | 1 (slider 0–1) | How far through the cooldown: 0 just after a boost, 1 when ready. Set by BoostRing in play; the material's value only shows in the editor |
| Fade Axis | `_Fade_Axis` | (0, 1, 0) | The axis, in the mesh's own (object) space, that the ring fades toward. Doesn't need to be unit length |
| Fade Start | `_Fade_Start` | 0 | Distance from the axis where the ring is fully see-through |
| Fade End | `_Fade_End` | 0.5 | Distance from the axis where it's fully solid. Tune Start and End to the ring mesh's size |

All five can be edited on the material in the Inspector.

## How it works

The graph is laid out in the order its values are built, one group per value.

### `charging`

| Nodes | Meaning |
| --- | --- |
| Multiply(Charge, Charging Glow) | The rising glow during the cooldown: 0 at Charge 0, up to Charging Glow just before Charge reaches 1 |

### `ready`

| Nodes | Meaning |
| --- | --- |
| Floor(Charge) | 1 only when Charge is exactly 1 (the cooldown is done), otherwise 0 |

Floor rounds down, and BoostRing keeps Charge between 0 and 1 (PlayerMovement clamps it), so Floor(0.999) = 0
and Floor(1) = 1. That gives a clean on/off switch with no extra comparison node.

### `glow`

| Nodes | Meaning |
| --- | --- |
| Lerp(A = `charging`, B = Ready Glow, T = `ready`) | The glow brightness: `charging` while the boost is cooling down, Ready Glow once it's ready |

Lerp with T = 0 gives A, and with T = 1 gives B. Since `ready` is only ever 0 or 1, this picks one or the
other rather than blending.

### `emission`

| Nodes | Meaning |
| --- | --- |
| Multiply(Glow Color, `glow`), then Multiply(that, `alpha`) → **Emission** | The colored light the ring gives off, faded where the ring is see-through |

The second Multiply is needed because the graph has **Preserve Specular Lighting** on (URP's default for
transparent surfaces): with it, URP fades only the base color by Alpha and leaves specular and **emission** at
full strength, so without this the glow would ignore the transparency. Fading it in the graph works whatever
that setting is. `alpha` is built further down the graph (see below) and also feeds the Alpha output.

### `axisDistance`

| Nodes | Meaning |
| --- | --- |
| Position (Object), Normalize(Fade Axis), Cross Product(position, axis), Length | How far this point is from the Fade Axis line through the mesh's centre |

For a unit-length axis, the length of `position × axis` is exactly the point's distance from that axis line.
Normalize makes the axis unit length, so any length typed into Fade Axis works. Object space means the fade is
fixed to the mesh: it rolls with the ring when it spins.

### `alpha`

| Nodes | Meaning |
| --- | --- |
| Smoothstep(Fade Start, Fade End, `axisDistance`) → **Alpha** | 0 (see-through) within Fade Start of the axis, easing up to 1 (solid) at Fade End and beyond |

On the preview sphere (radius 0.5), with Start 0 and End 0.5:

| Point | `axisDistance` | `alpha` |
| --- | --- | --- |
| A pole (0, 0.5, 0) | 0 | 0 (invisible) |
| Mid-latitude | 0.35 | ≈ 0.78 |
| The equator (0.5, 0, 0) | 0.5 | 1 (solid) |

### Output

- **Base Color** = the Base Color property.
- **Emission** = `emission`.
- **Alpha** = `alpha`. With Alpha blending, the see-through parts let what's behind show. The glow fades with
  them because `emission` is multiplied by `alpha` too.
- Metallic 0, Smoothness 0.5, Normal and Ambient Occlusion at their defaults.

### Worked example (Charging Glow 0.5, Ready Glow 3, cooldown 3 s, on a fully solid part where `alpha` is 1)

| Time since boost | Charge | `charging` | `ready` | `glow` |
| --- | --- | --- | --- | --- |
| 0 s | 0 | 0 | 0 | 0 (no glow) |
| 1 s | 0.33 | 0.17 | 0 | 0.17 |
| 2 s | 0.67 | 0.33 | 0 | 0.33 |
| 2.99 s | 0.997 | 0.498 | 0 | 0.498 |
| 3 s | 1 | 0.5 | **1** | **3** (full glow) |

## Talks to

- **BoostRing**: sets `_Charge` through a MaterialPropertyBlock, so the material asset isn't copied or changed.
- **PlayerMovement** (indirectly): `BoostCharge01` is where Charge comes from.

## Gotchas

- **Keep Charge's reference as `_Charge`.** BoostRing sets it by that name; if it's renamed, nothing is set and
  no error is shown.
- **Ready Glow needs Bloom to look like a glow.** Without post-processing Bloom, values above 1 just clip to a
  flat bright color.
- **Charging Glow above Ready Glow** would make the ring dim when the boost becomes ready. Keep Ready Glow
  higher.
- **The Charge slider on the material does nothing in play.** BoostRing overrides it on each ring.
- **Keep the `alpha` multiply on the emission.** Removing it makes the glow ignore the transparency again (see
  `emission`).
- **Fade Start must be below Fade End.** If they're equal, Smoothstep has no range and the edge becomes a hard
  cut.
- **Pick the Fade Axis for the ring model, not the preview.** The preview sphere's pole axis is y; the ring
  mesh may be set up around a different local axis (it rolls around z). If the wrong parts go see-through, try
  (0, 0, 1) or (1, 0, 0).
- **Transparent objects are sorted, not depth-tested against each other.** Where the ring overlaps other
  transparent effects (the exhaust flame, the shell), they may sometimes draw in the wrong order.
