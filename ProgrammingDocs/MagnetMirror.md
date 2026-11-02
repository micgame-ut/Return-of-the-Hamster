# MagnetMirror

## Purpose

Lets the Black Hole shader distort the stars and planets. The shader can only bend what the camera drawing it has
already drawn, and the gameplay camera hasn't drawn the sky or the planets (the planet camera does). So
`MagnetMirror` moves the Magnet's **mesh child** to the spot where the planet camera would see it, so that the shader
can see the background. It also **aims the black hole toward the center of the orbit ring**.

## Where it lives

- **Attached to:** the root of the Magnet prefab, next to `AsteroidBehavior`.
- **Requires:**
  - A `GameManager` in the scene with the gameplay camera and the Earth (planet) camera assigned. With none, the
    script does nothing.
  - The Magnet's mesh as a **separate child object** of the root, at the root's center, with the Black Hole
    material on it, and set to the **Background** layer (the planet camera draws that layer). If the Renderer is on
    the root itself, the script does nothing.
  - `OrbitSettings` in the scene, for the aiming. Without it, only the move happens.

## Inspector settings

| Field | Default | What it changes |
| --- | --- | --- |
| `faceCenter` | on | Turns the effect so its top (the thick arch) points at the orbit's center, as seen on screen. Off = the material's Disk Angle is used as it is |
| `angleOffset` | 0 | Degrees added to the turn. Use it to nudge the aim, or 180 to flip it |

## How it works

Every frame, after `AsteroidBehavior` has moved the Magnet, `MagnetMirror` places the mesh where the planet camera
would see it, and turns the effect toward the center of the orbit. The Magnet itself (its root, its position, its
pull on the player) isn't touched. It **moves** the mesh child; it doesn't make a copy.

### `Awake`

Runs once. It:
- gets the **gameplay camera** (as a `Camera`, and as its `Transform`) and the **planet camera** (`Transform`)
  from `GameManager`;
- finds the Magnet's **mesh child**: the first Renderer in its children, as long as it isn't on the root itself.
  It keeps the Renderer and its Transform, and remembers the mesh's resting position (`meshHome`);
- creates the `MaterialPropertyBlock` it will use for the aiming, **once**, so there's nothing to clean up every
  frame (a plain C# object, which the garbage collector handles).

It does not touch the material. The block only overrides one property on the Renderer.

### `LateUpdate`

Runs every frame, after every `Update`, so the Magnet has already moved this frame. It does nothing if either camera
or the mesh is missing. Otherwise:
1. It converts the Magnet root's world position into the **gameplay camera's own coordinates**
   (`InverseTransformPoint`): how far right, up and ahead of that camera it is.
2. It turns those coordinates back into a world position **relative to the planet camera** (`TransformPoint`):
   the point that is the same distance right, up and ahead of the *planet* camera.
3. It puts the mesh there. Only the **position** changes. The mesh's rotation (the spin) and size are
   `AsteroidBehavior`'s and the prefab's.
4. It calls `AimAtCenter`.

This works because the two cameras have the same field of view, so the mesh shows at the same place on screen as
the real Magnet would, and it stays behind every asteroid and the player (the planet camera draws first).

Example: a Magnet 120 units ahead of the gameplay camera and 10 to its right becomes a mesh 120 ahead of and 10 to
the right of the planet camera.

### `AimAtCenter`

Aims the black hole toward the center of the orbit ring, by setting the shader's **Disk Angle** for this Magnet:

1. It does nothing if `faceCenter` is off or anything it needs is missing (including `OrbitSettings`).
2. It takes the orbit's center, at the player's depth, and the Magnet, and converts both to **screen
   positions** with the gameplay camera. The difference is an arrow on screen from the Magnet to the center.
3. If the arrow is shorter than one pixel, it stops and keeps the last angle (a zero-length arrow has no
   direction, and `Atan2(0, 0)` would swing the effect round to a random angle).
4. `phi` = the arrow's angle (`Atan2`, converted to degrees; 0° = right, 90° = up).
5. It sets `_Disk_Angle` to `90 − phi + angleOffset` in the block and applies the block to the mesh's **first
   material slot**.

The shader turns its picture, not the object. A Magnet below the center gets 0 (the arch is on top, as normal),
one to the right of the center gets −90° (the arch points left), and one above the center gets 180°. A positive
Disk Angle turns the shader's picture clockwise, which the formula accounts for.

### `OnDisable`

Runs when the Magnet is switched off (pattern copies are pooled, so that's when it's done). It **puts the mesh back
at its resting position** (`meshHome`, relative to the root). Without it, a pooled Magnet would keep the mesh's last
position, about 2,500 units away, until `LateUpdate` fixed it on the next use. It doesn't unsubscribe from
anything; this script uses no events.

## Talks to

- **GameManager**: reads `GameplayCamera` and `EarthCamera` (the planet camera) once, in `Awake`.
- **OrbitSettings**: reads `OrbitCenter` and `PlayerZ` for the aiming.
- **The Black Hole shader** ([BlackHole.md](BlackHole.md)): sets its `_Disk_Angle` property through a
  `MaterialPropertyBlock`. The property reference must stay `_Disk_Angle`.
- **AsteroidBehavior** (the Magnet effect): moves the root, runs the pull, and spins the mesh. `MagnetMirror` runs
  after it each frame (`LateUpdate`). Both use property blocks on the mesh's first slot (AsteroidBehavior for its
  depth tint, which the Black Hole shader ignores); `MagnetMirror` runs last, so its block is the one applied.

## Gotchas

- **The mesh child must be on the Background layer.** On the gameplay layers, the gameplay camera draws it where
  Scene Color is black, and you get a black disc where the bending should be.
- **The mesh must be a child of the root, at its center.** The mapping uses the **root's** position, not the
  mesh's own offset. If the Renderer is on the root itself, the script does nothing, deliberately, so it can't
  move the whole Magnet away.
- **It only works as long as the two cameras share a field of view.** Different FOVs would put the picture in
  the wrong place, and it would no longer line up with the real Magnet.
- **The Magnet is always drawn behind every asteroid and the player**, because the planet camera draws before the
  gameplay camera does.
- **Aiming is measured on screen**, not in the world, so it follows perspective and matches what the player sees.
- **`faceCenter` off** leaves the material's own Disk Angle in use.
- **No `GameManager` (the pattern-building scene):** the script does nothing and logs nothing.
