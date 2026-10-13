# Heatwave

## Purpose

A saved experiment, **not used by any shader**. It's the "depth slices" version of the nebula clouds
(option A while we were exploring 3D clouds): the cloud field is read at a few fixed distances along each view
ray and layered near-over-far. It looked unusual as a skybox but was kept for possible later use (for
example a heat-haze or shimmer effect).

## Where it lives

- **File:** `Assets/Shaders/Heatwave.shadersubgraph`
- **Used by:** nothing yet.

## Inputs

The same as Nebula Clouds without Cloud Depth Scale (see [NebulaClouds.md](NebulaClouds.md)), plus:

| Input | Default | What it changes |
| --- | --- | --- |
| Slice Count | 3 | Number of depth slices (1–4) |
| Near Distance | 1 | Distance of the nearest slice |
| Far Distance | 4 | Distance of the farthest slice |
| Depth Haze | 0.6 | How much far slices fade toward Space Color |

## How it works

It's the same as [Nebula Volume](NebulaVolume.md), but simpler:

- **Slices instead of ray steps:** `HeatwaveNoise` reads the density at Slice Count evenly spaced distances
  (up to 4), and combines them near-over-far with the slice's Smoothstep mask as its opacity, rather than
  building up fog with Absorption.
- **Warp, dust and tint** are read once at the middle slice.
- Outputs Coverage, Density and Depth, used the same way as in Nebula Volume (opacity, color ramp, haze).

## Talks to

Nothing. It isn't in any shader.

## Gotchas

- **It's a snapshot:** it doesn't have the later improvements made to Nebula Clouds / Nebula Volume (the
  center-fade early exit, the 2-octave cap on warp and dust, the centered palette blend, Blackboard
  sections, stars). Copy those over if it's ever brought into use.
- **Function name:** its Custom Function is `HeatwaveNoise`, not `NebulaNoise`. Two different functions with
  the same name in one shader would fail to compile.
