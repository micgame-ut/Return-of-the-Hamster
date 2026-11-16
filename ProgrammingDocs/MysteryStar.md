# Mystery Star

## Purpose

The look of the Mystery pickup: a see-through star with a pastel rainbow that follows the colour palette, a staggered grid of lighter dots, a
bright rim and a little glow. The "?" inside it is a separate object (see [QuestionMarkBillboard.md](QuestionMarkBillboard.md)).

## Where it lives

- **File:** `Assets/Shaders/Mystery Star.shadergraph` (URP, Unlit, **transparent**, alpha blending, **Render Face: Both**)
- **Material:** `Assets/Materials/Mystery Star.mat`, on the root of `Mystery PowerUp.prefab` (mesh: `Star` from `Assets/Models/Mystery_Star.fbx`)
- **Palettes:** `_StarA`, `_StarB` and `_StarC` are listed in `PaletteTargets`, so a palette switch recolours the star (see
  [PaletteTargets.md](PaletteTargets.md)).

## Properties

Defaults are the graph's. Where the material changes one, the material's value is given.

| Property | Default (material) | What it changes |
| --- | --- | --- |
| Hue Scale | 0.5 | How many colour bands fit across the star |
| Scroll Speed | 0 | How fast the bands drift. 0 = fixed on the star |
| Star A / B / C | `#FF59CC` / `#FFF259` / `#59E6FF` | The three colours the bands blend through. Reference names `_StarA` / `_StarB` / `_StarC`; set by the palette |
| Dot Density | 4 | Grid squares per unit (about 7 dots across the star) |
| Dot Size | 0.3 | Dot radius in grid squares (0.5 would touch) |
| Dot Softness | 0.05 | Width of the soft dot edge |
| Dot Brightness | 0.5 (0.25) | How much lighter the dots are, 0 to 1 |
| Body Opacity | 0.35 (0.25) | How solid the plain body is |
| Dot Opacity | 0.35 | Extra solidity on the dots |
| Rim Power | 3 | How narrow the rim is (higher = thinner) |
| Rim Strength | 0.7 (0.5) | How much the rim adds opacity and whiteness |
| Base Color | white (0.85) | Multiplies the colour. Reference name **`_BaseColor`**, because `PowerUp.cs` writes its depth tint there |
| Glow | 1.5 | Multiplies the final colour above 1 so the bloom picks up the rim and dots |

## How it works

The graph is built in groups, in this order.

### `pos`, `hue`

| Group | Value | Meaning |
| --- | --- | --- |
| `pos` | Position (Object space), split into x and y | Where a point is on the star, so the colours stay on the star as it spins |
| `hue` | Fraction((x + 0.6 y) × Hue Scale + Time × Scroll Speed) | A 0 to 1 number rising diagonally across the star, wrapping from 1 to 0 |

### `star colours`, `blend`

`t` = `hue` × 3. `w1` = Saturate(`t`), `w2` = Saturate(`t` − 1), `w3` = Saturate(`t` − 2). The colour is
`Lerp(Lerp(Lerp(A, B, w1), C, w2), A, w3)`: A → B for `t` 0 to 1, B → C for 1 to 2, and C back to A for 2 to 3, so it wraps without a seam.
Example: hue 0.5 gives `t` 1.5, so halfway between B and C.

### `grid`, `dot`, `facing`, `dots`

- **`grid`:** x and y × Dot Density; every other row is shifted half a square (Floor, Modulo); Fraction − 0.5 gives `cell`, the position inside a grid
  square from −0.5 to 0.5.
- **`dot`:** 1 − Smoothstep(Dot Size − Dot Softness, Dot Size, Length(`cell`)): 1 inside a dot, 0 outside.
- **`facing`:** Smoothstep(0.6, 0.9, |object-space normal z|): 1 on the flat front and back, 0 on the thin sides. The star has no UV map, so dots are drawn
  from the front only, and the thin edge has none.
- **`dots`:** `dot` × `facing`. The colour is Lerp(blend colour, white, `dots` × Dot Brightness).

### `fresnel`, `alpha`, `colour`

- **`fresnel`:** `rim` = Fresnel Effect(Power = Rim Power) × Rim Strength. It is 0 facing the camera and rises toward the silhouette.
- **`alpha`:** Saturate(Body Opacity + `rim` + `dots` × Dot Opacity).
- **`colour`:** Lerp(the dots colour, white, `rim`): the rim is whiter.

### `tint`, `glow`

Base Color multiplies the colour (the depth tint), then Glow multiplies it again. Both feed Base Color on the output; `alpha` feeds Alpha.

## Talks to

- **PowerUp**: sets `_BaseColor` through a property block for the distance fade, using the material's own colour as the starting value.
- **PaletteApplier / PaletteTargets**: set `_StarA`, `_StarB`, `_StarC` for each palette. The original palette puts back the material's own colours.
- **QuestionMarkBillboard**: the "?" is a separate object inside the star.

## Gotchas

- **Transparent sorting:** with Render Face Both and one mesh, Unity can draw the star's faces in the wrong order. The star is mostly convex, so it
  looks fine, but a very different mesh might not.
- **Bloom threshold:** the glow only shows where a pixel is brighter than the volume's bloom threshold (0.9). The body is far below it, so only the rim
  and dots glow. Raise Glow for more, or lower the shared threshold (which changes every other glow too).
- **A pickup reads `_BaseColor` once, in `Awake`.** Changing the material colour while playing only shows on pickups spawned afterwards.
- **Leftover `_Saturation`:** the graph no longer has this property, but the material file may still hold an old value. It does nothing.
- **Dot Brightness, Body Opacity and Rim Strength are overridden on the material.** Resetting the material to the graph's defaults gives a milkier star.
