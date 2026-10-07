# CloudNoiseBaker

## Purpose

Generates the baked texture for use in the skybox. It's an Editor tool: you run it from the menu, and it
saves `Assets/Shaders/CloudNoise3D.asset`, a 64 × 64 × 64 texture with one random value per cloud cell.
Nebula Volume reads that texture once per noise read, instead of hashing 8 corners every time, which makes
the sky's clouds much cheaper to draw.

## Where it lives

- **File:** `Assets/Scripts/Editor/CloudNoiseBaker.cs`. It's in an `Editor` folder, so Unity leaves it out of
  game builds (it uses `UnityEditor`, which doesn't exist in a build).
- **Attached to:** nothing. It's a `static class` with a menu item, not a component.
- **Run it from:** **Return of the Hamster → Bake Cloud Noise Texture**. Only needed again if `Size` changes.

## Settings

There's no Inspector: the settings are `const`s in the code. Change one, then bake again.

| Constant | Value | What it changes |
| --- | --- | --- |
| `Size` | 64 | Cells per side. The noise repeats every `Size` cells. Must match the `64.0`s in Nebula Volume |
| `Format` | `R8` | 1 byte per value (0–255), 256 KB in total |
| `AssetPath` | `Assets/Shaders/CloudNoise3D.asset` | Where the texture is saved |

## How it works

You click the menu item → `Bake` runs → it calls `CreateTexture` → which calls `Hash` (which uses `Frac`) for
every cell → `Bake` saves the result.

### `Hash`: decides what number goes in one slot

Takes a cell's position (x, y, z) and gives back a number from 0 to 1 that looks random. It's fixed arithmetic,
not a random roll, so the same cell always gets the same number, and baking twice gives the same texture.

It's the exact formula the shader's math noise used before it was baked, written out in C#, so the clouds keep
the look they were tuned with. Example: cell (1, 2, 3) always gets about 0.31.

### `Frac`: a helper

Keeps only the part after the decimal point: `Frac(3.72)` = 0.72. HLSL has `frac` built in, but C# doesn't, so
`Hash` uses this one.

### `CreateTexture`: builds the texture

1. Three nested loops visit all 64 × 64 × 64 = 262,144 cells, and store each one's `Hash` as a byte (× 255,
   rounded).
2. The 3D grid is stored as one long list. A cell's place in it is `x + y × Size + z × Size × Size`: like a book
   with 64 cells per line and 64 lines per page. Cell (1, 2, 0) is at 1 + 2 × 64 = 129.
3. The texture is created without mipmaps, with **Repeat** (after cell 63 comes cell 0 again, so the noise
   tiles) and **Bilinear** (the GPU blends the 8 surrounding cells when the shader reads between them).

### `Bake`: saves the texture

- **First bake:** no asset exists yet, so it saves the texture as a new asset.
- **Rebake:** the asset already exists, so it copies the new data into it (`EditorUtility.CopySerialized`)
  instead of replacing the file. The asset keeps its GUID, so the material doesn't lose its Noise Texture.
  The temporary texture is then destroyed with `DestroyImmediate`, because outside Play mode `Destroy` doesn't
  run.
- It logs `Baked cloud noise to ...` in the Console.

## Talks to

- **Nebula Volume** (Sub Graph): reads the texture through its promoted **Noise Texture** property, set on
  `StarrySkyVolumeMat`. See [NebulaVolume.md](NebulaVolume.md).
- **SkyboxSpeedController**: `randomStartRange` (427) is based on `Size` ÷ Cloud Scale.

## Gotchas

- **`Size` must match the shader.** Nebula Volume wraps and reads at `64.0`. Rebake at another size without
  changing those, and the clouds get seams and wrong blending. Update `randomStartRange` in
  SkyboxSpeedController too.
- **Changing `Hash` changes the sky.** The shader no longer has its own hash, so after a rebake you get a
  different (but valid) cloud look, not the one the material was tuned with.
- **An empty Noise Texture slot reads as white.** If the asset is deleted, the sky turns into solid fog. Bake
  again and reassign it on `StarrySkyVolumeMat`.
- **Deleting the asset and baking again gives it a new GUID**, so the material loses it. Rebake over the
  existing asset instead.
