# GraphicsQuality

## Purpose

Runs at Start and sets the sky's values for the active quality level: lower values for lower-spec PCs on
PC Low, the full values on PC High. It doesn't copy the sky material itself; it changes the copy
SkyboxSpeedController made.

Every setting that lowers visual fidelity for performance goes here, with a High and a Low value and a
tooltip (see [PERFORMANCE.md](../PERFORMANCE.md)). Right now that's only the sky's Ray Steps.

## Where it lives

- **Attached to:** the `Game Manager` object in `TestGame.unity`, with the other level-wide scripts.
- **Requires:** SkyboxSpeedController in the same scene (it makes the sky material copy this script
  changes), and a sky shader with a float property with the reference name `_Ray_Steps` (Starry Sky Volume).

## Inspector settings

| Field | Default | What it changes |
| --- | --- | --- |
| `lowQualityLevel` | 0 | Position in **Project Settings → Quality** that uses the Low values. 0 = top of the list (PC Low) |
| **Sky** | | |
| `highRaySteps` | 16 | Ray Steps on every other level (PC High). Slider 4–48 |
| `lowRaySteps` | 8 | Ray Steps on `lowQualityLevel` (PC Low). Slider 4–48 |

The slider range matches the shader, which clamps Ray Steps to 4–48.

Cost of the sky's clouds is about Ray Steps × 2 + 2 noise reads per pixel (Cloud Octaves is 1):

| Ray Steps | Noise reads per pixel | Compared with 16 |
| --- | --- | --- |
| 16 (PC High) | 34 | — |
| 8 (PC Low) | 18 | about 47% cheaper |

## How it works

### `Start`: setting the sky's Ray Steps

Start is the choice instead of Awake because, if the sky material hadn't been copied yet, the change would
overwrite the real `.mat` permanently. Start runs after every Awake, so SkyboxSpeedController's copy is
always ready by then. It then checks the active quality level's position and sets Ray Steps to the Low or
High value.

Step by step:

1. `RenderSettings.skybox` is the scene's skybox material, which by now is SkyboxSpeedController's copy.
   If the scene has no skybox, it stops here.
2. `QualitySettings.GetQualityLevel()` returns the active level's position. If it equals
   `lowQualityLevel`, `low` is true.
3. `SetFloat` sets `_Ray_Steps` to `lowRaySteps` or `highRaySteps`, chosen with the conditional operator
   (`low ? lowRaySteps : highRaySteps`). The fields are `int`, but the Shader Graph property is a Float, so
   C# converts the value to a float (exact for every whole number in 4–48).

`RayStepsId` is `_Ray_Steps` turned into a number once with `Shader.PropertyToID`, the same as in
SkyboxSpeedController and AsteroidBehavior.

It only runs once, so changing the fields or the quality level during Play mode does nothing until the next
time the level loads. Quality is only changed from the menu, so that's enough.

## Talks to

- **SkyboxSpeedController**: doesn't call it, but relies on it having swapped `RenderSettings.skybox` for its
  own copy in Awake.
- **The sky shader** (Starry Sky Volume): sets `_Ray_Steps` on the copied skybox material.
- **QualitySettings**: reads the active quality level's position.

## Gotchas

- **Needs SkyboxSpeedController in the scene.** Without it, `RenderSettings.skybox` is the real
  `StarrySkyVolumeMat` asset, and Play mode would change Ray Steps on the asset permanently.
- **Quality levels are matched by position, not name.** Reordering the list in Project Settings → Quality
  swaps which level gets the Low values. Move them back, or change `lowQualityLevel`.
- **Ray Steps on the material is only for editing.** In Play mode this script overwrites it on the copy, so
  the material's own value only shows in the Scene view and when this script is missing.
- **The reference name must match.** If `_Ray_Steps` is renamed in the shader, `SetFloat` silently sets
  nothing. No error is shown.
- **Only applies in scenes that have it.** The Menu scene has no Game Manager and uses Unity's default
  skybox, so it isn't affected.
