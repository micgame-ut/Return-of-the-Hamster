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
| Charging Glow | `_Charging_Glow` | 0.5 | How bright the glow gets just before the boost is ready. It rises from 0 to this during the cooldown, following Charge squared, so it stays dark early and brightens late |
| Ready Glow | `_Ready_Glow` | 3 | The full glow once the boost is ready. Above 1, Bloom makes it shine |
| Charge | `_Charge` | 1 (slider 0–1) | How far through the cooldown: 0 just after a boost, 1 when ready. Set by BoostRing in play; the material's value only shows in the editor |
| Ready Pulse | `_Ready_Pulse` | 0 (slider 0–1) | The ready flash: BoostRing sets it to 1 when the boost becomes ready and fades it to 0. The material's value only shows in the editor |
| Pulse Glow | `_Pulse_Glow` | 10 | The extra glow brightness at the peak of the flash (Ready Pulse 1). Added on top of the glow |
| Breathe Amount | `_Breathe_Amount` | 0.3 (slider 0–1) | How deep the ready glow's breathing goes. The glow swings between `1 − Breathe Amount` and 1 times Ready Glow. 0 = steady |
| Breathe Speed | `_Breathe_Speed` | 2 | How fast the ready glow breathes, in radians per second. One full swell and dip takes π ÷ Breathe Speed seconds (about 1.6 s at 2) |
| Ring 1 Radius | `_Ring_1_Radius` | 0.38 | Where the first sharp ring sits, as a distance from the Fade Axis in the mesh's own space. The ring mesh is a sphere of radius 0.5, so 0 is the middle and 0.5 the rim |
| Ring 2 Radius | `_Ring_2_Radius` | 0.47 | The same for the second ring |
| Ring Core | `_Ring_Core` | 0.008 | Half the width of each ring's fully bright line, in the same units. Smaller = thinner |
| Ring Edge | `_Ring_Edge` | 0.014 | Where each ring has faded to nothing. Keep it above Ring Core. The gap between the two is the softness of the edge; equal values give a hard, aliased edge |
| Ring 1 Color | `_Ring_1_Color` | bright cyan (0.2, 1.5, 2), HDR | The first ring's color. Values above 1 glow with Bloom |
| Ring 2 Color | `_Ring_2_Color` | warm yellow-white (2, 1.6, 0.4), HDR | The second ring's color |
| Ring Brightness | `_Ring_Brightness` | 2 | How bright both rings are all the time. The glow is added on top, so they flare when the boost is ready |
| Fade Axis | `_Fade_Axis` | (0, 1, 0) | The axis, in the mesh's own (object) space, that the ring fades toward. Doesn't need to be unit length |
| Fade Start | `_Fade_Start` | 0 | Distance from the axis where the ring is fully see-through |
| Fade End | `_Fade_End` | 0.5 | Distance from the axis where it's fully solid. Tune Start and End to the ring mesh's size |

All five can be edited on the material in the Inspector.

## How it works

The graph is laid out in the order its values are built, one group per value.

### `chargeSq`

| Nodes | Meaning |
| --- | --- |
| Multiply(Charge, Charge) | Charge squared. It stays low for most of the cooldown and climbs steeply at the end (0.33 → 0.11, 0.67 → 0.44, 1 → 1) |

Squaring Charge is what keeps the ring dark for the first two-thirds of the cooldown, so there's a big contrast
with the ready glow.

### `charging`

| Nodes | Meaning |
| --- | --- |
| Multiply(`chargeSq`, Charging Glow) | The rising glow during the cooldown: 0 at Charge 0, up to Charging Glow just before Charge reaches 1, but on a curve that stays dark early and brightens late |

### `ready`

| Nodes | Meaning |
| --- | --- |
| Floor(Charge) | 1 only when Charge is exactly 1 (the cooldown is done), otherwise 0 |

Floor rounds down, and BoostRing keeps Charge between 0 and 1 (PlayerMovement clamps it), so Floor(0.999) = 0
and Floor(1) = 1. That gives a clean on/off switch with no extra comparison node.

### `breathe`

| Nodes | Meaning |
| --- | --- |
| Time, Multiply(Time, Breathe Speed), Sine, Multiply(sine, sine), One Minus, Multiply(that, Breathe Amount), One Minus | `breathe = 1 − Breathe Amount × (1 − sin²(Time × Breathe Speed))`: a smooth factor that swings between `1 − Breathe Amount` and 1 |

`sin²` (the sine multiplied by itself) goes from 0 up to 1 and back, smoothly, so the dip has no sharp corner. It
also means every constant comes from a node rather than a typed-in number. The two One Minus nodes provide the
"1 −" parts.

| `sin²` | `breathe` (Breathe Amount 0.3) |
| --- | --- |
| 0 (dimmest point) | 0.7 |
| 0.5 | 0.85 |
| 1 (brightest point) | 1 |

### `glow`

| Nodes | Meaning |
| --- | --- |
| Multiply(Ready Glow, `breathe`) | The ready glow, breathing: between 70% and 100% of Ready Glow at the defaults |
| Lerp(A = `charging`, B = the breathing ready glow, T = `ready`) | The glow brightness: `charging` while the boost is cooling down, the breathing ready glow once it's ready |

Lerp with T = 0 gives A, and with T = 1 gives B. Since `ready` is only ever 0 or 1, this picks one or the
other rather than blending.

### `pulse`

| Nodes | Meaning |
| --- | --- |
| Multiply(Ready Pulse, Pulse Glow) | The flash's extra brightness: 0 normally, up to Pulse Glow at the instant the boost becomes ready, then fading back to 0 as BoostRing lowers Ready Pulse |

### `glow` (adding the flash)

The `glow` group also holds an **Add** node that adds `pulse` to the Lerp's result before it goes to `emission`:

| Nodes | Meaning |
| --- | --- |
| Add(Lerp result, `pulse`) | The final glow brightness: the steady glow, plus the flash |

### `emission`

| Nodes | Meaning |
| --- | --- |
| Multiply(Glow Color, `glow` + `pulse`), then Multiply(that, `alpha`) → **Emission** | The colored light the ring gives off, faded where the ring is see-through |

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
| Smoothstep(Fade Start, Fade End, `axisDistance`) | 0 (see-through) within Fade Start of the axis, easing up to 1 (solid) at Fade End and beyond |
| Maximum(that, `bandMask`) → **Alpha** | The same, but raised to 1 wherever a sharp ring is, so the rings show even where the ring is see-through |

On the preview sphere (radius 0.5), with Start 0 and End 0.5:

| Point | `axisDistance` | `alpha` |
| --- | --- | --- |
| A pole (0, 0.5, 0) | 0 | 0 (invisible) |
| Mid-latitude | 0.35 | ≈ 0.78 |
| The equator (0.5, 0, 0) | 0.5 | 1 (solid) |

### `band1` and `band2`: the two sharp rings

| Nodes (one set per ring) | Meaning |
| --- | --- |
| Subtract(`axisDistance`, Ring N Radius), Absolute | How far this point is from the ring's radius, whichever side it's on |
| Smoothstep(Ring Core, Ring Edge, that), One Minus | `bandN`: 1 on the ring's thin line, falling to 0 within a hair's width, and 0 everywhere else |

The Ring Core and Ring Edge property nodes sit in the `band1` group and feed both rings. `axisDistance` is the
same value the see-through fade uses, so the rings are circles around the ring's middle, fixed to the mesh (they
roll with it). With the defaults, a point 0.005 from a ring's radius is fully bright (inside Ring Core 0.008), 0.011
away it's about half bright, and 0.014 away it's gone.

### `bandMask`

| Nodes | Meaning |
| --- | --- |
| Maximum(`band1`, `band2`) | 1 on either ring's line, 0 elsewhere. Feeds the Alpha |

### `bands`

| Nodes | Meaning |
| --- | --- |
| Multiply(`band1`, Ring 1 Color), Multiply(`band2`, Ring 2 Color), Add | Each ring in its own color, added together |
| Add(Ring Brightness, `glow` + `pulse`) | How bright the rings are: a steady Ring Brightness, plus the glow, so they flare with the ready glow, the breathing and the flash |
| Multiply(the two) | The rings' emission, added to `emission` below |

### Output

- **Base Color** = the Base Color property.
- **Emission** = `emission` + the `bands` result. The rings' emission isn't multiplied by `alpha`, so they stay
  full strength even where the ring is see-through.
- **Alpha** = `alpha` raised to 1 on the rings (`bandMask`). With Alpha blending, the see-through parts let what's
  behind show. The glow fades with them because `emission` is multiplied by `alpha` too.
- Metallic 0, Smoothness 0.5, Normal and Ambient Occlusion at their defaults.

### Worked example (Charging Glow 1.5, Ready Glow 6, cooldown 3 s, on a fully solid part where `alpha` is 1)

| Time since boost | Charge | `chargeSq` | `charging` | `ready` | `glow` |
| --- | --- | --- | --- | --- | --- |
| 0 s | 0 | 0 | 0 | 0 | 0 (no glow) |
| 1 s | 0.33 | 0.11 | 0.17 | 0 | 0.17 |
| 2 s | 0.67 | 0.44 | 0.67 | 0 | 0.67 |
| 2.99 s | 0.997 | 0.994 | 1.49 | 0 | 1.49 |
| 3 s | 1 | 1 | 1.5 | **1** | **6** at the brightest point of the breathing (4.2 at the dimmest), plus the ready flash |

## Talks to

- **BoostRing**: sets `_Charge` through a MaterialPropertyBlock, so the material asset isn't copied or changed.
- **PlayerMovement** (indirectly): `BoostCharge01` is where Charge comes from.

## Gotchas

- **Keep Charge's reference as `_Charge`.** BoostRing sets it by that name; if it's renamed, nothing is set and
  no error is shown.
- **Ready Glow needs Bloom to look like a glow.** Without post-processing Bloom, values above 1 just clip to a
  flat bright color.
- **Charging Glow above Ready Glow** would make the ring dim when the boost becomes ready. Keep Ready Glow
  higher. The breathing can also dip Ready Glow down to `Ready Glow × (1 − Breathe Amount)`, so keep that above
  Charging Glow too.
- **Breathing uses the shader's `Time` node,** so every ring breathes in step. That time follows the game's
  scaled time, so the breathing freezes when time is frozen (for example at game over).
- **The Charge slider on the material does nothing in play.** BoostRing overrides it on each ring.
- **Keep the `alpha` multiply on the emission.** Removing it makes the glow ignore the transparency again (see
  `emission`).
- **Keep the rings' radii inside the mesh.** The ring mesh is a sphere of radius 0.5, so radii above 0.5 fall off
  the surface and show nothing. The visible band is Fade Start to Fade End (0.3 to 0.5 on the material); a ring
  outside it still shows, because `bandMask` raises the alpha, but it will look like it's floating on the
  see-through part.
- **Ring Core must stay below Ring Edge.** If they're equal, the edge is a hard cut and can shimmer; if Ring Core is
  above Ring Edge, Smoothstep has a reversed range and the rings can vanish or invert.
- **The rings need Bloom to look like light.** Ring Brightness plus the glow only reads as a glow above 1, the same
  as Ready Glow.
- **Fade Start must be below Fade End.** If they're equal, Smoothstep has no range and the edge becomes a hard
  cut.
- **Pick the Fade Axis for the ring model, not the preview.** The preview sphere's pole axis is y; the ring
  mesh may be set up around a different local axis (it rolls around z). If the wrong parts go see-through, try
  (0, 0, 1) or (1, 0, 0).
- **Transparent objects are sorted, not depth-tested against each other.** Where the ring overlaps other
  transparent effects (the exhaust flame, the shell), they may sometimes draw in the wrong order.
