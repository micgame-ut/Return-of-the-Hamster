# CRT Screen

## Purpose

Makes the whole picture look like an old CRT (tube) screen: the picture is bent outward like a curved tube with dark
rounded corners, crossed by scanlines and faint red/green/blue stripes, with a colour fringe toward the edges, a
darker vignette and a faint flicker. It's a full-screen effect, so it covers everything the cameras draw, including the
3D title and the menu buttons. The Tab overlay switches it on and off.

## Where it lives

- **File:** `Assets/Shaders/CRT Screen.shadergraph` (URP, **Fullscreen** graph)
- **Material:** `Assets/Materials/CRT Screen.mat`, made from it.
- **Renderer feature:** a **Full Screen Pass Renderer Feature** named `CRT Screen` on `Assets/Settings/PC_Renderer.asset`
  (both PC quality assets use that renderer). Injection point: After Rendering Post Processing. Fetch Color Buffer: on.
  Requirements: Color. Pass Material: `CRT Screen.mat`. Pass Index: 0.
- **Driven by:** `TuningOverlay`, which sets the global `_CRTIntensity` to 1 or 0 from its "CRT screen" checkbox.

## Properties

| Property | Reference | Default | What it changes |
| --- | --- | --- | --- |
| Curvature | `_Curvature` | 0.15 | How much the picture bulges like a tube. 0 = flat. The corners are cut off and go black as it rises |
| Fringe | `_Fringe` | 0.003 | How far the red and blue channels are pulled apart toward the edges, as a share of the screen width. 0 = none |
| Scanline Count | `_Scanline_Count` | 240 | How many scanlines fill the screen height. On a 1080-tall picture each is 4.5 pixels |
| Scanline Strength | `_Scanline_Strength` | 0.45 | How dark the gaps between scanlines get. 0 = none, 1 = black |
| Mask Count | `_Mask_Count` | 640 | How many red/green/blue stripe sets fill the screen width. Each set is three stripes |
| Mask Strength | `_Mask_Strength` | 0.25 | How strongly the stripes show. 0 = none |
| Vignette | `_Vignette` | 0.4 | How much the corners are darkened. At 0.4 the corners are at 0.6 of full brightness and the middle of each edge at 0.8 |
| Flicker | `_Flicker` | 0.03 | How much the brightness wobbles over time, as a fraction. 0.03 = between 0.97 and 1.03 |
| Brightness | `_Brightness` | 1.4 | Multiplies the CRT picture to make up for what the scanlines, stripes and vignette take away |
| CRT Intensity | `_CRTIntensity` | 0 | **Not exposed on the material.** A global float set from code (`Shader.SetGlobalFloat`). 0 = the plain picture, 1 = the full CRT |

The first nine can be edited on the material in the Inspector. `_CRTIntensity` can't, because it's a global.

## How it works

The graph is laid out in the order its values are built, one group per value.

### `uv`

Screen Position, split and put back together as a Vector2: where this pixel is on the screen, 0 to 1.

### `centered`

`uv × 2 − 1`. The middle of the screen is (0, 0) and the edges are −1 and 1.

### `curvedUV`

`r2` is `dot(centered, centered)`, the pixel's distance from the middle, squared (0 in the middle, 1 at the middle of an
edge, 2 at a corner). The bend is `(1 + _Curvature × r2) ÷ (1 + _Curvature)`, and `pushed = centered × bend`. Pixels
near the edge read from further out, which makes the picture look bulged. Dividing by `1 + _Curvature` keeps the middle
of each edge exactly at the screen edge (`bend` is 1 there), so only the corners are cut off. Then
`curvedUV = pushed × 0.5 + 0.5` goes back to 0 to 1. A Split of `curvedUV` (its x and y) is also in this group and is
used by `scan` and `mask`.

Worked example with `_Curvature` 0.15: at the corner `r2` is 2, so `bend` = (1 + 0.3) ÷ 1.15 = 1.13, and a corner pixel
reads from 13% further out than the screen, which is off-screen, so it goes black.

### `inside`

`1 − smoothstep(0.98, 1, |pushed|)` for x and y, multiplied together. It's 1 where the bent picture is still on the
screen and falls to 0 at the edge, so the rim is slightly soft. It makes the dark rounded corners.

### `color`

`edgeShift = centered × _Fringe`. The red channel is read at `curvedUV + edgeShift`, the green at `curvedUV`, and the
blue at `curvedUV − edgeShift`, using three **Sample Buffer** nodes set to **Blit Source** (the camera colour). The
three channels are put back together. In the middle `edgeShift` is 0, so there's no fringe there.

### `scan`

`lines = sin(curvedUV.y × _Scanline_Count × 2π) × 0.5 + 0.5`, a wave with one cycle per line. `scan = 1 − _Scanline_Strength × (1 − lines)`,
so it's 1 on the bright part of each line and dips to `1 − _Scanline_Strength` in the gaps. It uses `curvedUV`, so the lines bend with the screen.

### `mask`

`fraction(curvedUV.x × _Mask_Count)` is the position inside one stripe set (0 to 1). `Step` at one third and two thirds
splits it into red, green and blue stripes (only one channel is 1 at a time). `mask = 1 − _Mask_Strength × (1 − stripe)`,
so each channel is dimmed a little everywhere except in its own stripe.

### `vig`

`1 − _Vignette × r2 × 0.5`. It reuses `r2` from `curvedUV`.

### `flicker`

`1 + _Flicker × sin(Time × 95)`. About 15 wobbles a second. It uses the game's time, so it stops when the game is paused.

### `crt`

`color × scan × mask × vig × flicker × inside × _Brightness`.

### `original`

A Sample Buffer (Blit Source) at the plain screen position, with no bending: the picture as it was.

### `final`

`lerp(original, crt, _CRTIntensity)`, sent to Base Color. At 0 the output is exactly the plain picture. Alpha is 1.

## Talks to

- **TuningOverlay**: sets `_CRTIntensity` from the "CRT screen" checkbox, and saves the choice in `MovementTuning.json`.
- **PC_Renderer**: the Full Screen Pass feature runs this material every frame.

## Gotchas

- **The Sample Buffer nodes must be set to Blit Source.** A new Sample Buffer node defaults to Normal (World Space), and
  the picture then shows world normals (red and green gradients) instead of colour.
- **Screen Space - Overlay canvases are not included.** URP draws them after the effect. The Menu Canvas is Screen Space -
  Camera, so the menu is covered, but the HUD, Tutorial and End Screen canvases (Overlay) stay sharp.
- **The pass runs every frame, even at `_CRTIntensity` 0,** so it costs a little in every level. Switching the feature off
  in the renderer would remove that.
- **The camera stack is one Base and one Overlay camera.** The effect still comes out as a single bend. If it ever looks
  doubled, check the injection point.
- **`_CRTIntensity` is 0 until something sets it.** The material alone shows the plain picture.
- **Scanline and stripe counts are in screen units,** not pixels, so they scale with the window. At a different height each line
  is a different number of pixels, so very low resolutions can look noisy.
- **Only `PC_Renderer` has the feature.** `Mobile_Renderer` doesn't, so the CRT wouldn't show on the Mobile quality asset.
- **The time node follows the time scale,** so the flicker freezes while the Tab overlay is open.
