# Black Hole

## Purpose

The look of a **Magnet** asteroid: a black shadow with the background bent around it like a gravitational lens, a thin glowing ring on the shadow's edge, and a flat, streaked, orange-yellow accretion disc seen almost edge-on, with the bent far side of the disc arching over the top and under the bottom. It's meant for a big, slow void that sits just outside the player's orbit and pulls the ship toward it (see [AsteroidBehavior.md](AsteroidBehavior.md), Magnet).

## Where it lives

- **File:** `Assets/Shaders/Black Hole.shadergraph` (URP, **Unlit**, **transparent**, **alpha** blending, front faces, no shadows).
- **Material:** a `Black Hole Mat` made from it, on the Magnet prefab's **mesh child** (a sphere).
- **Needs:**
  - **Opaque Texture** on in the URP asset (`PC_RPAsset` and `PC_RP_LowAsset` have it; `Mobile_RPAsset` doesn't, so the bending shows nothing there, though the shadow and glow still draw). Scene Color reads that texture.
  - **MagnetMirror** on the Magnet, and its mesh child on the **Background** layer. Scene Color is black on the gameplay camera (an Overlay camera has no Opaque Texture), so MagnetMirror draws the mesh through the background camera, where it works. See [AsteroidBehavior.md](AsteroidBehavior.md).
  - A **large** sphere. The disc reaches about three times as wide as the shadow, and everything is measured as a fraction of the sphere's radius.
- **Not used for:** anything else yet. Its properties are all per-material.

## Properties

All the sliders and colours are on the material. "Sphere radius" below means the radius of the mesh as it appears on screen: 0 is its middle, 1 is its silhouette.

| Property | Reference | Default | What it changes |
| --- | --- | --- | --- |
| Horizon Size | `_Horizon_Size` | 0.4 (0–1) | The black shadow's radius. About **0.25** works well with the disc |
| Horizon Softness | `_Horizon_Softness` | 0.05 (0–0.3) | How soft the shadow's edge is |
| Bend Strength | `_Bend_Strength` | 0.15 (0–0.5) | How far the background is shifted at its strongest |
| Bend Falloff | `_Bend_Falloff` | 2 (0.5–6) | How fast the bending dies away going outward. Higher = hugs the shadow |
| Ring Width | `_Ring_Width` | 0.03 (0.005–0.2) | Thickness of the thin ring's sharp core |
| Fringe Width | `_Fringe_Width` | 0.12 (0.01–0.4) | How far the ring's soft glow reaches |
| Ring Color | `_Ring_Color` | HDR yellow (4, 3.2, 0.6) | The ring's sharp core |
| Fringe Color | `_Fringe_Color` | HDR orange (2.5, 0.9, 0.05) | The ring's soft glow |
| Edge Fade Start | `_Edge_Fade_Start` | 0.75 (0.3–0.99) | Where the sphere starts turning transparent toward its silhouette |
| Disk Inner | `_Disk_Inner` | 0.32 (0–1) | The disc's inner edge |
| Disk Outer | `_Disk_Outer` | 0.92 (0–1) | The disc's outer edge. How wide it reaches |
| Disk Squash | `_Disk_Squash` | 0.12 (0.02–1) | How flat the disc is. 1 = a full circle |
| Disk Angle | `_Disk_Angle` | 0 (−180–180) | Turns the whole disc. **MagnetMirror sets this per Magnet** to point the arch at the orbit's centre |
| Disk Softness | `_Disk_Softness` | 0.06 (0.005–0.3) | How soft the disc's edges are |
| Arch Width | `_Arch_Width` | 0.30 (0.05–0.6) | How far the top arch reaches outward from the shadow |
| Arch Rise | `_Arch_Rise` | 0.06 (0.01–0.3) | How smoothly the arches fade in above and below the disc's plane |
| Arch Softness | `_Arch_Softness` | 0.04 (0.005–0.2) | How soft the arch's inner edge is |
| Bottom Arch Scale | `_Bottom_Arch_Scale` | 0.35 (0–1) | Size and brightness of the lower arc compared with the top one. 0 removes it |
| Crossing Width | `_Crossing_Width` | 0.03 (0.005–0.1) | Vertical thickness of the curved line across the shadow |
| Streak Density | `_Streak_Density` | 40 (5–150) | How many fine rings fit across the disc |
| Streak Contrast | `_Streak_Contrast` | 0.7 (0–1) | How strongly the rings stand out |
| Streak Flow | `_Streak_Flow` | 0.6 (−3–3) | How fast the rings drift. Positive = inward |
| Streak Drift | `_Streak_Drift` | 1.5 (0–6) | How quickly the pattern changes along the ring |
| Mirror Softness | `_Mirror_Softness` | 0.08 (0.01–0.4) | Width of the rounded strip where the left and right streak patterns meet |
| Hot Color | `_Hot_Color` | HDR white-yellow (6, 4.5, 1.8) | The colour at the inner edge of the disc and arches |
| Cool Color | `_Cool_Color` | HDR red-orange (1.6, 0.22, 0.03) | The colour at the outer edge |
| Heat Falloff | `_Heat_Falloff` | 1.5 (0.3–4) | How quickly the colour cools going outward |
| Doppler Strength | `_Doppler_Strength` | 0.6 (−0.9–0.9) | How much brighter both outer sides are than the middle. Negative = dimmer |
| Disk Brightness | `_Disk_Brightness` | 1.2 (0–5) | Overall intensity of the disc and arches |

## How it works

Laid out in the order the values are built, one Shader Graph group per value. Everything works in the **sphere's own screen-aligned coordinates**, so it needs no camera maths, and it looks the same wherever the Magnet is on screen.

### `radius`

How far from the middle of the visible sphere each pixel is, and which way it leans.

| Value | Meaning | Nodes |
| --- | --- | --- |
| `facing` | How directly the surface looks at the camera | Dot(Normal Vector (View), View Direction (View)) |
| `lean` | The part of the surface normal that leans **away** from the line to the camera. It points outward from the middle of the visible cap | Normal − View Direction × `facing` |
| `radius` | 0 in the middle, up to about 1 at the silhouette | Length(`lean`) |

Using the direction to the camera (View Direction), not the camera's axis, is what keeps the middle of the effect on the middle of the sphere you can see when the Magnet is off to the side of the screen. (An earlier version used the camera axis and the disc looked turned toward the screen centre.)

### `horizon`

The black disc.

| Value | Meaning | Nodes |
| --- | --- | --- |
| `edgeStart` | Where the soft edge begins | Horizon Size − Horizon Softness |
| `outside` | 0 inside the shadow, 1 outside, with a soft ramp | Smoothstep(`edgeStart`, Horizon Size, `radius`) |

### `pull`

How strongly to bend the background at each pixel.

| Value | Meaning | Nodes |
| --- | --- | --- |
| `shape` | 1 in the middle falling to 0 at the silhouette | One Minus(`radius`) → Power(Bend Falloff) |
| `pull` | The bend strength here | `shape` × Bend Strength |

### `lens`

The bent picture of what's behind the sphere.

| Value | Meaning | Nodes |
| --- | --- | --- |
| `offset` | How far to shift where we look, outward from the middle | `lean` × `pull` |
| `lens` | The scene behind, seen through the shifted point | Scene Color(Screen Position (Default) − `offset`) |

Looking at a point *closer to the middle* than the pixel really is makes background features appear pushed outward, like a real lens. `pull` is zero at the silhouette, so the effect blends into the unbent background with no seam.

### `disc`

`body` = `lens` × `outside`: the bent picture, with the middle blacked out.

### `ring`

The thin ring on the shadow's edge, as two masks.

| Value | Meaning | Nodes |
| --- | --- | --- |
| `ringDist` | Distance from the shadow's edge, either way | Absolute(`radius` − Horizon Size) |
| `core` | 1 on the edge, 0 at Ring Width. The sharp band | One Minus(Smoothstep(0, Ring Width, `ringDist`)) |
| `fringe` | 1 on the edge, 0 at Fringe Width. The soft glow | One Minus(Smoothstep(0, Fringe Width, `ringDist`)) |

### `glow`

`glow` = Ring Color × `core` + Fringe Color × `fringe`: the coloured ring. It's a full circle round the shadow.

### `edge`

The sphere's alpha: One Minus(Smoothstep(Edge Fade Start, 1, `radius`)). It's 1 in the middle and fades to 0 at the silhouette, so the sphere's outline can't be seen.

### `disk`

The flat disc's shape.

| Value | Meaning | Nodes |
| --- | --- | --- |
| `p` | The pixel's (x, y) from the middle, turned by Disk Angle | `lean` → Split → rotate by sine and cosine of Disk Angle (degrees to radians) |
| `x'`, `y'` | The turned coordinates | x cos − y sin, and x sin + y cos |
| `q` | `p` with y divided by Disk Squash, so a circle in `q` is a flat ellipse on screen | Combine(`x'`, `y'` ÷ Squash) |
| `d` | Distance from the middle in that squashed space | Length(`q`) |
| `diskMask` | 1 on the band, 0 in the hole and outside | Smoothstep(Inner, Inner + Softness, `d`) × (1 − Smoothstep(Outer − Softness, Outer, `d`)) |

The rotation is built from sine and cosine (not a Rotate node), and a positive Disk Angle turns the disc **clockwise** on screen.

### `arch`

The bent far side of the disc, over the top and under the bottom.

| Value | Meaning | Nodes |
| --- | --- | --- |
| `inner` | 0 inside the shadow, 1 just outside its edge | Smoothstep(Horizon Size, Horizon Size + Arch Softness, `radius`) |
| `riseUp`, `riseDown` | 1 above (or below) the disc's plane, 0 on it | Smoothstep(0, Arch Rise, `y'`) and the same on −`y'` |
| `topFall`, `bottomFall` | 1 at the shadow's edge fading to 0 at Arch Width (or Arch Width × Bottom Arch Scale) | One Minus(Smoothstep(Horizon Size, Horizon Size + width, `radius`)) |
| `archTop`, `archBottom` | The two arches | `inner` × `topFall` × `riseUp`; `inner` × `bottomFall` × `riseDown` × Bottom Arch Scale |
| `arch` | Both | `archTop` + `archBottom` |

### `front`

The near half of the disc crossing in front of the shadow. `front` is a thin line along the **lower half of the inner-edge ellipse**:

- Its centre height: `y_c` = −Disk Squash × √(Disk Inner² − `x'`²). The Square Root's input is clamped at 0 with a Maximum.
- The line: One Minus(Smoothstep(0, Crossing Width, |`y'` − `y_c`|)), then × the disc's outer cut so it doesn't run past the band.
- `diskAll` = Maximum(`diskMask`, `front`): the band plus the curved crossing, which bows slightly below the band's ends (by Squash × Disk Inner).

### `streaks`

Fine rings along the disc and arches, drifting inward.

| Value | Meaning | Nodes |
| --- | --- | --- |
| `shift` | How far the pattern has moved by now | Time × Streak Flow |
| `along` | Slow variation along the ring | the smooth mirror (below) × Streak Drift |
| `noiseDisk`, `noiseArch` | Fine rings | Gradient Noise(UV = (`d` or `radius` × Streak Density + `shift`, `along`)) |
| `streakDisk`, `streakArch` | Brightness 1 − Contrast to 1 | Lerp(1, noise, Streak Contrast) |

The pattern slides inward over time (positive Streak Flow). The **smooth mirror** is √(`x'`² + Mirror Softness²): a rounded version of |`x'`|, so the left and right halves mirror each other through the middle without a sharp crease.

### `heat`

Colour, brightness, and the final mix.

| Value | Meaning | Nodes |
| --- | --- | --- |
| `heatDisk`, `heatArch` | 1 at the inner edge falling to 0 outward, curved by Heat Falloff | Power(One Minus(Smoothstep(Inner, Outer, `d`)), Heat Falloff); Power(`topFall`, Heat Falloff) |
| `colorDisk`, `colorArch` | The colour | Lerp(Cool Color, Hot Color, `heat…`) |
| `doppler` | 1 in the middle, brighter toward both sides | 1 + `x'`² × Doppler Strength |
| `discLight` | All the disc and arch light | (`colorDisk` × `bandVisible` × `streakDisk` + `colorArch` × `arch` × `streakArch`) × `doppler` × Disk Brightness |
| `result` | The final colour, which goes to **Base Color** | `body` + `glow` + `discLight` |

The `1` in `doppler` comes from a node that outputs 1: **One Minus with nothing plugged in** (its input defaults to 0). See Gotchas.

### `occlusion`

The far half of the disc passes **behind** the black hole. `shadowFront` = Maximum(`outside`, `riseDown`) is 1 everywhere except on the shadow's upper half, and `bandVisible` = the band's brightness × `shadowFront`. The near half still crosses in front (the curved line); the far half inside the shadow is hidden. Outside the shadow nothing changes.

### Output

- **Base Color** = `result` (the group `heat`).
- **Alpha** = `edge`.

## Talks to

- **MagnetMirror**: sets `_Disk_Angle` on the mesh's first material slot every frame, through a `MaterialPropertyBlock`, so each Magnet points its arch at the orbit's centre.
- **AsteroidBehavior** (the Magnet effect): owns the pull and the approach. The look is only this shader. `AsteroidBehavior` also sets `_BaseColor` and `_Tint` for the depth fade, which this shader doesn't have, so it ignores them (the Magnet doesn't fade with distance).
- **The URP asset and the camera stack**: Scene Color needs Opaque Texture, and the Base camera.

## Gotchas

- **Scene Color is black on an Overlay camera.** That's why the mesh is shown through the background camera. Without MagnetMirror, the shader renders a black disc where the bending should be.
- **The sphere has to be big, and the mesh must be a real sphere.** `radius` assumes a round shape. The Magnet prefab's **Meshes** list on `AsteroidBehavior` must be empty, or Awake swaps the sphere for a random rock.
- **Remove the Collider component** on the Magnet prefab. Unticking it isn't enough: `WarnThenMove` switches it back on.
- **Disk Angle turns clockwise.** MagnetMirror's `90 − φ` accounts for that.
- **Doppler Strength** no longer means "one side brighter". It brightens both outer sides equally, and the name is just kept so that saved materials still work.
- **Editing the graph file by hand has traps** (all hit while building this):
  - **Add and Subtract nodes** must have **DynamicVector** slots and **can't hold a typed-in value** in a pasted JSON. Multiply, Divide's value slots and the like are different. A constant has to come from a connected node (One Minus with nothing plugged in outputs 1).
  - **Property names that collide** with PowerShell built-ins (`$pid`) or a prefix variable differing only in case (`$p` and `$P`) silently break a build script.
  - A **list of IDs** pasted into the wrong list (`m_ActiveTargets` instead of the property category) makes the whole graph fail to import with an `InvalidCastException`. Check the Editor log (`Editor.log`) first.
- **The `x'`² and the smooth mirror** keep the brightness and the streaks symmetric. A plain `x'` made the left side go negative (blue), and a plain |`x'`| made a visible seam in the middle.
