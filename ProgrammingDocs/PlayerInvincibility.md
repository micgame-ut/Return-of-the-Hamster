# PlayerInvincibility

## Purpose

Makes the player invincible for a set time after picking up an Invincible power-up. While it's active,
every asteroid the player touches is busted (it explodes and counts as destroyed) instead of hitting them,
and a rippling rainbow shader shows over the ship. In the last `warningTime` seconds the rainbow flickers,
so the player knows it's about to end.

## Where it lives

- **Attached to:** the player object (the one with `PlayerMovement`), next to `PlayerHealth`.
- **Requires:**
  - `meshTransform`: the ship's mesh, to find its renderers. Without it, it uses every renderer under the
    player object.
  - `rainbowMaterial`: `Assets/Shaders/RainbowOverlayMat.mat`. Without it, invincibility still works, just
    with no rainbow.
  - `GameManager.Instance`, to switch off on game over or a win. Without one, it still runs, but only ends
    when the timer runs out.

## Inspector settings

| Field | Default | What it changes |
| --- | --- | --- |
| `meshTransform` | none | The ship's mesh. Assign the same object as PlayerMovement's `meshTransform` |
| `rainbowMaterial` | none | The overlay material added to the ship while invincible |
| `warningTime` | 1 | Seconds before the end when the rainbow starts flickering |
| `warningBlinkInterval` | 0.1 | Seconds between each flicker on/off |

The duration isn't set here. It's `invincibleDuration` in the SpawnableTemplate (default 5), passed in by
the power-up.

## How it works

The whole feature, start to finish:

1. The player touches an Invincible power-up. PowerUp's `ApplyEffect` calls `MakeInvincible`, which finds
   this component and calls `Activate(template.invincibleDuration)`. The power-up then destroys itself,
   which is why the timer has to live here on the player instead.
2. Each frame, `Update` counts the timer down. While `IsInvincible` is true, AsteroidBehavior explodes any
   asteroid that touches the player, with no knockback, speed loss or lost life.
3. In the last `warningTime` seconds, the rainbow flickers.
4. It ends when the timer runs out, or straight away on game over or a win.

### `IsInvincible`

A read-only property: `timer > 0f`. It's worked out each time it's read, so it can never get out of sync
with the timer. AsteroidBehavior reads it.

### `Awake`

Finds the ship's mesh renderers, saves each one's original materials, and builds a second set of arrays
with the rainbow added, once, so nothing is built during play.

- `originalMaterials` and `rainbowMaterials` are **arrays of arrays** (`Material[][]`): one entry per
  renderer, each holding that renderer's own materials array, since renderers can have different numbers of
  material slots.
- It reads `sharedMaterials`, not `materials`. `materials` would make a private copy of each material, and
  we only need to swap which materials are in the slots.
- Each rainbow array is the original plus one extra slot at the end holding `rainbowMaterial`, the same way
  AsteroidBehavior adds its outline.

Saving the originals in `Awake`, before anything changes, matters: reading them later (say, in `Activate`)
could pick up the rainbow too, if it was already on.

### `SetOverlay`

Turns the rainbow material on or off. It only touches the renderers when the state flips.

1. **Guard:** if the overlay is already in the requested state, or there's no `rainbowMaterial`, it returns
   straight away. `overlayOn` tracks the current state.
2. Each renderer's `sharedMaterials` is set to either its rainbow array or its original array.

`Update` calls it every frame during the flicker, but the guard means the renderers only change when the
flicker flips. With the defaults, that's about 10 changes a second instead of 60.

### `Activate`

Sets the timer to the full duration (a second pickup resets it) and turns the rainbow on. It's `public`
so PowerUp can call it.

| Event | `timer` after |
| --- | --- |
| 1st pickup | 5 |
| 3 seconds later | 2 |
| 2nd pickup | 5 (reset, not 7) |

A pickup during the flicker turns the rainbow back on straight away, and the flicker stops because the
timer is no longer inside `warningTime`.

### `Update`

Checks for game over or a win first (and switches everything off), then counts the timer down, handles the
timer running out, and handles the warning flicker.

1. **Game over or won?** Sets `timer` to 0 and switches the overlay off. This has to come first:
   `GameOver()` and `Win()` set `Time.timeScale` to 0, so `Time.deltaTime` is 0 and the countdown would
   never finish, leaving the rainbow stuck on.
2. **Not active?** Returns. The timer may be slightly below 0 after it ran out (say −0.01), which counts as
   not active.
3. **Count down.** If that takes the timer to 0 or below, it switches the overlay off and returns.
4. **Flicker.** The rainbow is on if there's more than `warningTime` left. Otherwise it uses the same
   formula as PlayerMovement's hit blink: on when `Mathf.FloorToInt(timer / warningBlinkInterval)` is even.

With `warningTime` 1 and `warningBlinkInterval` 0.25:

| `timer` | Rounded down (÷ 0.25) | Rainbow |
| --- | --- | --- |
| 0.99 to 0.75 | 3 | off |
| 0.74 to 0.50 | 2 | on |
| 0.49 to 0.25 | 1 | off |
| 0.24 to 0.01 | 0 | on |
| 0 or less | — | off (ended) |

### The Rainbow Overlay shader

`Assets/Shaders/Rainbow Overlay.shadergraph` is an **Unlit**, **Transparent**, **Additive** Shader Graph.
Additive means it only brightens the ship (black adds nothing), so it reads as light shining over it.

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

| Property | Default | In `RainbowOverlayMat` | What it changes |
| --- | --- | --- | --- |
| `Scroll Speed` | 0.5 | | How fast the colors move |
| `Stripe Scale` | 2 | | How many rainbows fit across the noise range |
| `Strength` | 0.6 | | Overall brightness. 0 makes it invisible |
| `Noise Scale` | 8 | **2** | Size of the noise blobs (bigger = more, smaller blobs) |
| `Ripple Frequency` | 20 | | How many ripples are stacked around each blob |
| `Ripple Speed` | 3 | | How fast the ripples crawl |
| `Ripple Height` | 0.02 | | How far crests rise off the surface, in object units |

## Talks to

- **PowerUp**: calls `Activate(duration)` from `MakeInvincible` (Invincible type).
- **SpawnableTemplate**: not directly; PowerUp reads `invincibleDuration` from it and passes it in.
- **AsteroidBehavior**: reads `IsInvincible` in its `CanBreak`. While it's true, any asteroid explodes on
  contact, and destructible outlines turn green.
- **OrbitRing**: not directly; it reads `CanBreak`, so every danger arc turns green while invincible.
- **GameManager**: reads `IsGameOver` and `HasWon` every frame. GameManager also keeps a reference to this
  component (auto-filled from the player), which is how asteroids find it.

## Gotchas

- **Assign `rainbowMaterial` before pressing Play.** The rainbow arrays are built in `Awake`, so changing
  the material during Play mode doesn't change what shows. Filling an empty field during Play leaves an
  empty material slot.
- **Material values override the graph's defaults.** Changing a default in the Shader Graph doesn't
  update `RainbowOverlayMat`; set the value on the material.
- **The hit blink hides the rainbow too.** If the player was hit just before picking it up, PlayerMovement's
  blink switches the whole ship's renderers off and on, rainbow included, until the blink ends.
- **GameManager needs `player` assigned** (or `playerInvincibility` filled in). Asteroids get this
  component through GameManager, so without it, invincibility shows the rainbow but doesn't bust asteroids
  or turn them green.
- **The ripple height depends on the mesh.** Height moves vertices, so a low-poly mesh ripples roughly,
  and hard edges can split apart if `Ripple Height` is large.
- **Noise scale follows the object.** It's object space, so scaling the ship's Transform scales the
  pattern with it.
