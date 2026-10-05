# Rainbow Overlay

## Purpose

The glowing shell drawn over the player: a **rainbow** while invincible, and a **shield** color at full
health. It has rippling bands of light that move across the surface, and a gentle wobble of the surface
itself.

## Where it lives

- **File:** `Assets/Shaders/Rainbow Overlay.shadergraph` (URP, Unlit, transparent, **additive** blending,
  front faces)
- **Materials:** `RainbowOverlayMat` (Rainbow 1, Opacity 1) and `ShieldOverlayMat` (Rainbow 0.1, so mostly
  Shield Color with a hint of rainbow, and Opacity 0.4). Both use this graph with different values.
- **Shown by:** PlayerShell, which picks which material the overlay uses. See [PlayerShell.md](PlayerShell.md).

## Properties

| Property | Default | What it changes |
| --- | --- | --- |
| Rainbow | 1 | 0 = Shield Color only, 1 = full rainbow. In between blends them |
| Shield Color | light blue | The color when Rainbow is 0 |
| Stripe Scale | 2 | How many rainbow bands across the surface |
| Scroll Speed | 0.5 | How fast the rainbow bands scroll |
| Noise Scale | 2 | Size of the noise pattern behind the stripes and ripples |
| Ripple Frequency | 20 | How many ripple bands |
| Ripple Speed | 3 | How fast the ripples move |
| Ripple Height | 0.02 | How far the surface wobbles out and in |
| Strength | 0.6 | Overall brightness |
| Opacity | 1 (slider 0–1) | How strongly the overlay shows. The shield uses a lower value so it's less intrusive |

## How it works

Two **Triplanar Noise** Sub Graphs (Scale = Noise Scale) give a smooth noise pattern stuck to the player
mesh. See [TriplanarNoise.md](TriplanarNoise.md).

### 1. Rainbow color

| Step | Nodes | Meaning |
| --- | --- | --- |
| Hue | Fraction(noise × Stripe Scale + Time × Scroll Speed) | A hue that bands across the surface and scrolls over time |
| Rainbow | Combine(hue, 1, 1) → Colorspace Conversion **HSV → RGB** | Full-saturation, full-brightness rainbow color |
| Color | Lerp(Shield Color, rainbow, Rainbow) | Pick shield, rainbow, or a blend |

### 2. Ripples

| Step | Nodes | Meaning |
| --- | --- | --- |
| Wave | Sine(noise × Ripple Frequency − Time × Ripple Speed) | Bands that travel across the surface |
| Ripple | Remap(−1…1 → 0…1) | The wave as 0–1 |
| Brightness | (Ripple + 0.5) × Strength | Ripples brighten and dim the color |

### 3. Output

- **Base Color** = Color × Brightness × **Opacity**.
- **Vertex Position** = Position + Normal × Ripple × Ripple Height, so the surface swells slightly where the
  ripple is bright.

### Why additive, and why Opacity scales the color

The overlay **adds** light on top of the player instead of covering it, so it reads as a glow. With additive
blending, the cleanest "opacity" is to scale the color itself: 0.4 adds 40% as much light. Alpha stays at 1.

## Talks to

- **PlayerShell**: shows the overlay and switches between the rainbow and shield materials.
- **Triplanar Noise** (Sub Graph): used twice for the noise patterns.

## Gotchas

- **Unused leftover nodes:** a Length of Position, a Split of Position into an Add, and a Vector1 (0.2) × 2
  into an Add aren't connected to the output. They do nothing (Shader Graph skips them) and can be deleted.
- **Additive on a bright background** can wash out. Opacity is the knob to calm it down.
- **Six noise reads** (two Triplanar Noise uses × three projections). Fine on the small player mesh.
