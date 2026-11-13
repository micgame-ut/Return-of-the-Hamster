# TwinCopy

## Purpose

A copy of the player made by the twin power-up. It has no input, health or speed of its own: it sits at an
offset around the orbit from the player and copies the player's movement, roll and shell every frame. PlayerTwins
creates copies, tells them where to go, and removes them.

## Where it lives

- **Attached to:** the root of the `Twin Copy` prefab, created at runtime by PlayerTwins.
- **Requires:**
  - The copy's own parts assigned in the Inspector (below).
  - `GameManager.Instance` with `Player` and `PlayerShell`, since a copy isn't on the player.
  - A kinematic **Rigidbody** on the prefab, so asteroids' triggers notice its colliders.
  - The prefab mirrors the player's structure but **without** PlayerMovement, PlayerHealth, PlayerInvincibility,
    PlayerShell, PlayerDeath, PlayerTwins or Player Input. ExhaustFlame stays (it gets the player from
    GameManager on a copy).

## Inspector settings

| Field | What it is |
| --- | --- |
| `meshTransform` | The copy's ship mesh, which copies the player mesh's roll |
| `shellMesh` | The copy's shell, shown and hidden like the player's |
| `shellCollider` | The copy's bigger hitbox (`roll_mesh/Shell`), on while the player's shell hitbox is on |
| `bodyCollider` | The copy's body hitbox (`roll_mesh/Sphere`), on otherwise |

The root `Twin Copy` object has **no collider of its own**, like the player. Its Rigidbody uses the colliders of
its children.

## How it works

### `Awake`

Gets the player (`PlayerMovement`) and its `PlayerShell` from GameManager, through a local variable `gm`, and
stores them in fields. It also finds the shell's renderer with `GetComponentInChildren<Renderer>(true)`. The
`true` includes inactive objects; the shell is often switched off, and without it the renderer would be `null`
and the copy's shell would show the wrong material.

Copies are made with `Instantiate` during play, and `Awake` runs at that moment, long after GameManager has
set itself up.

### `OnEnable` / `OnDisable`

Subscribe to and unsubscribe from PlayerShell's `OnShellChanged`. `OnEnable` also calls `ApplyShell` once, so a
copy made during invincibility starts with the rainbow showing instead of waiting for the next change.
Unsubscribing in `OnDisable` means PlayerShell never calls into a destroyed copy.

### `ApplyShell`

Copies the player's shell onto this copy: which collider is on (`Shielded`: shell on and body off, or the
reverse), whether the shell shows (`ShellShowing`), and its material (`ShellMaterial`). The chain when
invincibility starts: PowerUp → PlayerInvincibility raises its events → PlayerShell `Refresh`, which raises
`OnShellChanged` last → every copy's `ApplyShell` reads the new state. The same chain runs on every rainbow
flicker, so copies flicker in step.

### `LateUpdate`: following the player every frame

`LateUpdate` runs after every `Update`, so it uses this frame's player angle (PlayerMovement moves in `Update`).

1. **Re-space**, if one is running (`moveDuration > 0`): advance `moveTime`, then
   `offset = Lerp(fromOffset, toOffset, SmoothStep(0, 1, moveTime ÷ moveDuration))`. `SmoothStep` eases in and
   out, so the copy glides instead of sliding and stopping dead. When `moveTime` reaches the duration,
   `moveDuration` is set to 0 and the easing stops.

   | `moveTime` (of 0.3) | `SmoothStep` | offset (180° → 120°) |
   | --- | --- | --- |
   | 0 | 0 | 180° |
   | 0.075 | 0.16 | 170.6° |
   | 0.15 | 0.5 | 150° |
   | 0.225 | 0.84 | 129.4° |
   | 0.3 | 1 | 120° |

2. **Place** the copy at `angle = player.OrbitAngle + offset`: orbit center + `(cos, sin) × radius`, at the
   player's depth. Everything that moves the player (sliding, boosts, knockback) moves the copy the same way.
3. **Turn** it: `Euler(0, 0, offset) × player.rotation`, the player's rotation turned a further `offset` around
   z, so it faces the center from its own spot.
4. **Roll**: the copy's mesh copies the player mesh's **local** rotation (relative to its parent). The world
   rotation would make every copy point the same way on screen, so a copy at the top would face away from the
   center.

### `Offset`

A read-only property that returns the current offset (radians around the orbit from the player). PlayerTwins
reads it to find the nearest copy and to sort copies when re-spacing.

### `MoveTo`

Starts an eased move to a new offset: records the current offset as `fromOffset` (so a move that interrupts
another starts from wherever the copy is, with no jump), sets `toOffset`, and resets `moveTime`. `LateUpdate`
does the easing over `duration`. A duration of 0 sets the offset straight away.

### `ShiftOffset`

Called by PlayerTwins when the player jumps onto another copy's spot. Offsets are measured from the player, so
without this the copy would be dragged along by the jump. It adds the same amount to `offset`, `fromOffset` and
`toOffset`, so the copy stays put in the world and a re-space in progress carries on smoothly. Also used by
`Respace` to wrap offsets into 0–360° without moving the copy.

## Talks to

- **GameManager**: gets `Player` and `PlayerShell` in `Awake`.
- **PlayerMovement**: reads `OrbitAngle`, `OrbitCenter`, `CircleRadius`, its rotation and `MeshTransform`
  every frame.
- **PlayerShell**: subscribes to `OnShellChanged`; reads `Shielded`, `ShellShowing` and `ShellMaterial`.
- **PlayerTwins**: creates and destroys it; calls `MoveTo` and `ShiftOffset`, reads `Offset`.

## Gotchas

- **Offset is an angle, not a position.** It stays the same while the copy's position changes with the player.
- **Don't destroy a copy directly.** PlayerTwins keeps a list of copies; go through `LoseBody`.
- **The hit blink isn't copied.** Copies only exist during the twin, where there's no hit-invulnerability.
- **No Rigidbody, no hits.** Without a kinematic Rigidbody on the prefab, asteroids and pickups pass through
  copies.
- **Every collider on the copy must be assigned.** Both fields must be filled in, and there must be no other collider.
  The copy used to have an empty `shellCollider` and an extra root collider, so its shell and `Sphere` colliders were
  always on beside the body one. One asteroid hit on a copy then fired up to three times: the first removed the copy,
  and the others ran the "hit the player" branch, which cost the player a life as well.
