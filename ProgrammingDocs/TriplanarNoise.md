# Triplanar Noise

## Purpose

A Sub Graph that puts Gradient Noise on a 3D object **without seams or stretching**, by projecting the
noise from three directions and blending them by which way each part of the surface faces.

## Where it lives

- **File:** `Assets/Shaders/Triplanar Noise.shadersubgraph`
- **Used by:** the `Rainbow Overlay` shader graph, twice (once for the rainbow stripes, once for the
  ripples). See [RainbowOverlay.md](RainbowOverlay.md). Also the `Frost Rock` shader graph, three times (the
  frost grain, the frost patches and the sparkle). See [FrostRock.md](FrostRock.md).

## Inputs

| Input | Type | Default | What it changes |
| --- | --- | --- | --- |
| Scale | Float | 8 | Noise frequency. Higher = smaller, busier noise |

**Output:** `Noise` (Float, about 0–1).

## How it works

### Why triplanar

Normal noise needs 2D coordinates (UVs). On a sphere-like mesh, UVs stretch near the poles and have a seam
where they wrap. Triplanar noise skips UVs and uses the **object-space position** instead.

### 1. Three projections

The object-space Position goes into three Swizzle nodes, each picking two of x, y, z, so the noise is read
as if projected straight onto the object from the **side**, **top** and **front**. Each projection gets its
own Gradient Noise (all with `Scale`).

A flat projection only looks right on surfaces facing it: on surfaces at a steep angle, the noise smears into
streaks.

### 2. Weights from the surface direction

The object-space Normal Vector says which way each point faces.

| Step | Nodes | Meaning |
| --- | --- | --- |
| Absolute | `abs(normal)` | How much the surface faces each axis, ignoring the sign |
| Divide by the sum | `abs(normal) / (x + y + z)` | Weights that add up to 1 |
| Split | x, y, z weights | One weight per projection |

### 3. Blend

`Noise = noise₁ × weight₁ + noise₂ × weight₂ + noise₃ × weight₃`

Each part of the surface mostly uses the projection that faces it, so there's no smearing, and the blends
between them are smooth, so there are no seams.

## Talks to

- **Rainbow Overlay**: uses it for the stripe pattern and the ripple pattern.
- **Frost Rock**: uses it for the frost grain, the frost patches and the sparkle.

## Gotchas

- **Object space:** the noise sticks to the object as it moves and rotates. That's intended for an overlay
  that should move with the player. In world space, it would slide across the surface.
- **Three noise reads per use.** Rainbow Overlay uses it twice, so 6 Gradient Noise reads per pixel. Fine
  for a small object on screen, but worth knowing if it's ever used on something large.
