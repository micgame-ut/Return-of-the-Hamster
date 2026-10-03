# PlayerShell

## Purpose

Controls the visual shield mesh and larger hitbox around the player. At full health, it shows the shield.
While invincible, at any health, the shell shows the rainbow instead. The rainbow takes priority over the
shield, and the shield comes back afterwards if the player is still at full health. While the shield or
invincibility is active, the shell's collider makes the player's hitbox bigger: more asteroids are busted
while invincible, but the shield also gets hit more easily, which is the intended downside for the first
life. It responds to both changes in player lives and the invincibility pickup.

## Where it lives

- **Attached to:** the player object (the one with `PlayerMovement`), next to `PlayerHealth` and
  `PlayerInvincibility`.
- **The shell objects** (set up in the Editor, on `hamter.prefab`):
  - `Shell`: a child of the player's **Mesh Transform**, so it rolls and pitches with the ship. It has a
    Sphere Collider, sized slightly bigger than the ship (the hitbox).
  - `ShellMesh`: a child of `Shell`. It has a sphere mesh with **no collider** and the overlay material.
- **Requires:**
  - `PlayerHealth` on the same object. Without it, `Refresh` does nothing.
  - `PlayerInvincibility` on the same object (optional). Without it, the shell only follows health.

## Inspector settings

| Field | Default | What it changes |
| --- | --- | --- |
| `shellMesh` | none | The visible shell, switched on and off as a whole GameObject |
| `shellCollider` | none | The shell's hitbox, enabled and disabled on its own |
| `shieldMaterial` | none | `ShieldOverlayMat` |
| `rainbowMaterial` | none | `RainbowOverlayMat` |

## How it works

| Priority | Condition | Shell shows | Hitbox |
| --- | --- | --- | --- |
| 1 | Rainbow showing (invincible, including the flicker's "on" moments) | rainbow material | on |
| 2 | Full health | shield material | on |
| 3 | Otherwise | nothing | off |

The hitbox follows `IsInvincible`, not the flicker, so during the flicker's "off" moments it stays on even
when nothing is visible.

### `Refresh`

Decides the hitbox (full health or invincible), whether the shell shows (rainbow or full health), and its
material (rainbow first, otherwise shield). It reads three facts fresh every time:

- **Full health?** `CurrentLives >= MaxLives`
- **Invincible?** `IsInvincible` (false without a PlayerInvincibility)
- **Rainbow showing?** `RainbowShowing` (false without a PlayerInvincibility)

The hitbox is set before the mesh check, so a missing `shellMesh` doesn't stop it working. The material is
swapped with `sharedMaterial` (singular), which doesn't make a copy.

### `Awake`

Grabs the components: PlayerHealth, PlayerInvincibility, and the shell's renderer. The renderer search
uses `GetComponentInChildren<Renderer>(true)`, where `true` includes inactive objects, in case `ShellMesh`
starts switched off. `Awake` only **finds** components, which is safe because components exist as soon as
the object loads. It doesn't **read** their data, which might not be set yet.

### `Start`

Draws the starting state once every `Awake` has run. PlayerHealth sets `CurrentLives` in its own `Awake`,
and Unity doesn't guarantee which `Awake` runs first, so reading lives any earlier could see 0.

### `OnEnable` / `OnDisable` and the handlers

Sign up to and leave the three events: `OnLivesChanged`, `OnInvincibilityChanged` and `OnRainbowChanged`.
The handlers just call `Refresh`. They ignore the value passed in, because `Refresh` needs all three facts
every time and re-reads them itself. They still take a parameter (`int lives`, `bool on`) because a method
has to match the event's type (`Action<int>`, `Action<bool>`) to sign up.

Nothing is checked every frame. The shell only changes when one of the events fires.

### `HitRadius`

Gives the hitbox's size while it's on (0 while off), for OrbitRing's arcs. It's the bigger of the
collider's x and y **bounds extents**, so it includes any scaling on the parents. A disabled collider's
bounds aren't updated, so it returns 0 then instead of reading them.

### Why the hitbox and the mesh are separate objects

- **The flicker:** if the collider lived on `ShellMesh` and switched off with it, the hitbox would flicker
  too, and asteroids could slip through the "off" moments while the player is still invincible.
- **The hit blink:** PlayerMovement's hit blink flips every renderer under Mesh Transform and **always
  switches them back on** at the end. Switching `ShellMesh`'s **GameObject** on and off, rather than its
  renderer, means a blink can't make a hidden shell show.

## The overlay shader

`Assets/Shaders/Rainbow Overlay.shadergraph` is used by both materials. It's an **Unlit**,
**Transparent**, **Additive** Shader Graph. Additive means it only brightens what's behind it (black adds
nothing), so it reads as light shining over the ship.

- **The rainbow moves** because of the time and speed properties. The noise sets the hue at each point,
  and `Time × Scroll Speed` slides the hue along over time.
- **The surface ripples** from the same idea run through the noise: `noise × Ripple Frequency −
  Time × Ripple Speed` goes into a Sine, which gives moving crests. The crests push the surface out along
  its normals (Vertex stage) by up to `Ripple Height`, and make the color brighter (Fragment stage,
  `ripple + 0.5`, times `Strength`).
- **Triplanar noise** is used to get rid of seams. UVs have a seam where their edges meet, so the
  `Triplanar Noise` Sub Graph samples Gradient Noise three times from object-space position (yz, xz and
  xy), and blends them by the surface normal, with weights that always add up to 1. It's a Sub Graph so
  the same logic can be used twice (hue and ripple) without building it twice.
- **The `Rainbow` slider** differentiates the shield and rainbow, letting the shield look the same but be
  colored with one color you choose. It's a Lerp between `Shield Color` (at 0) and the rainbow hue (at 1).
  The ripples and brightness come from a separate branch, so they're the same in both.

| Property | Default | `RainbowOverlayMat` | `ShieldOverlayMat` | What it changes |
| --- | --- | --- | --- | --- |
| `Rainbow` | 1 | 1 | **0** | 1 = full hue spectrum, 0 = `Shield Color` |
| `Shield Color` | light blue (HDR) | | your choice | The single color used when `Rainbow` is 0 |
| `Scroll Speed` | 0.5 | | | How fast the colors move |
| `Stripe Scale` | 2 | | | How many rainbows fit across the noise range |
| `Strength` | 0.6 | | | Overall brightness. 0 makes it invisible |
| `Noise Scale` | 8 | **2** | **2** | Size of the noise blobs (bigger = more, smaller blobs) |
| `Ripple Frequency` | 20 | | | How many ripples are stacked around each blob |
| `Ripple Speed` | 3 | | | How fast the ripples crawl |
| `Ripple Height` | 0.02 | | | How far crests rise off the surface, in object units |

## Talks to

- **PlayerHealth**: subscribes to `OnLivesChanged`; reads `CurrentLives` and `MaxLives`.
- **PlayerInvincibility**: subscribes to `OnInvincibilityChanged` and `OnRainbowChanged`; reads
  `IsInvincible` and `RainbowShowing`.
- **AsteroidBehavior**: not directly. Asteroids find the player with `GetComponentInParent<PlayerMovement>()`,
  so the shell's collider counts as the player with no asteroid changes.
- **OrbitRing**: reads `HitRadius` once per frame to widen the arcs.

## Gotchas

- **Remove the default Sphere Collider from `ShellMesh`.** Unity adds one to new spheres, and it would be
  a second hitbox that's always on.
- **The shell must be under Mesh Transform** to roll with the ship. It's switched on and off as a
  GameObject so the hit blink and death sequence (which loop over renderers there) can't make it show.
- **Material values override the graph's defaults.** Changing a default in the Shader Graph doesn't update
  either material; set the value on the material.
- **A bigger hitbox at full health means more hits.** That's intended, but it makes the first life easier
  to lose than the others.
- **The ripple height depends on the mesh.** Height moves vertices, so a low-poly mesh ripples roughly.
- **Noise scale follows the object.** It's object space, so scaling the shell's Transform scales the
  pattern with it.
