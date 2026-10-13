# Asteroid Outline

## Purpose

The outline drawn around destructible asteroids: green when the player can smash it, another color when
they're too slow. It uses the **inverted hull** technique: draw the mesh a second time, slightly inflated,
showing only its back faces, in a flat color.

## Where it lives

- **File:** `Assets/Shaders/Asteroid Outline.shadergraph` (URP, Unlit, opaque, **back faces only**)
- **Material:** `AsteroidOutlineMat`, set as `outlineMaterial` on the SpawnableTemplate asset.
- **Applied by:** AsteroidBehavior, which adds it as an extra material slot on destructible asteroids and
  sets its color per asteroid. See [AsteroidBehavior.md](AsteroidBehavior.md).

## Properties

| Property | Reference | Default | What it changes |
| --- | --- | --- | --- |
| OutlineColor | `_OutlineColor` | black | The outline color. AsteroidBehavior overwrites it per asteroid (green / too slow) |
| OutlineWidth | `_OutlineWidth` | 0.05 | How far the hull is pushed out, so how thick the outline looks |

## How it works

### Vertex: inflate the mesh

`Position = Position + Normal × OutlineWidth` (both in object space)

Every vertex is pushed out along its normal, so the second copy of the mesh is a slightly bigger shell
around the asteroid.

### Back faces only

The graph's Render Face is set to **Back**. On the inflated shell, the front faces would cover the whole
asteroid. Only drawing the back faces (the inside of the shell, which faces away from the camera) means
the shell only shows where it sticks out past the real asteroid: a rim around its silhouette.

### Fragment: a flat color

Base Color = `OutlineColor`. No lighting, so the rim reads clearly against the sky.

## Talks to

- **AsteroidBehavior**: adds `AsteroidOutlineMat` to destructible asteroids and sets `_OutlineColor`
  through a MaterialPropertyBlock (so each asteroid can have its own color without copying the material).
- **SpawnableTemplate**: holds `outlineMaterial` and the two outline colors.

## Gotchas

- **Width is in object units.** A scaled-up asteroid gets a thicker outline too.
- **Hard edges split.** On meshes with sharp edges (split normals), the shell pulls apart at the corners and
  gaps can show. Smooth-shaded meshes outline cleanly.
- **The reference name `_OutlineColor` is used by code.** Renaming it breaks the color switch silently.
