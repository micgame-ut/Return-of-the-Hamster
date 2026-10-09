# ExhaustFlame

## Purpose

Handles the LineRenderer properties that need to change while the game runs, for the exhaust flame behind
the player: its length and width with speed, a flicker, a stretch on a forward boost, a drag that bends it as
the player moves around the orbit, the rainbow color while invincible, the hit blink, and turning it off when
the level ends.

The flame is two lines: a larger orange **outer flame**, and a thinner, shorter, brighter white **inner
core** inside it.

## Where it lives

- **Attached to:** the `Exhaust Flame` object in the `hamter` prefab, a child of the **Ring**. The Ring
  follows the ship but doesn't roll with the hamster mesh, so the flame always points straight back.
- **Requires:**
  - A **LineRenderer on `Exhaust Flame`** (the outer flame) assigned to `core`, and a **LineRenderer on its
    child `Exhaust Core`** (the inner core) assigned to `innerCore`. They need separate objects because a
    GameObject can only have one Renderer.
  - Both LineRenderers set up with **Use World Space on**, the `ExhaustCore` material (URP
    Particles/Unlit, Transparent, Additive, no Base Map), a Width curve drawn as the taper's *shape* (1 at the
    head, near 0 at the tail), and the inner one at **Order in Layer 1** so it draws on top.
  - **PlayerMovement** and **PlayerInvincibility** on a parent (the player), found with `GetComponentInParent`.
  - **HDR** on the URP asset (on for PC High and PC Low) and **Bloom** on the Global Volume, for the glow.

## Inspector settings

| Field | Default | In the prefab | What it changes |
| --- | --- | --- | --- |
| `core` | none | Exhaust Flame | The outer flame's LineRenderer |
| `innerCore` | none | Exhaust Core | The inner core's LineRenderer |
| `minLength` / `maxLength` | 0.5 / 2.5 | 2 / 5 | Flame length at `Speed01` 0 and 1 |
| `minWidth` / `maxWidth` | 0.2 / 0.5 | 0.2 / 0.5 | Head width at `Speed01` 1 (narrowest) and 0 (widest). Multiplies the Width curve |
| `minFlicker` / `maxFlicker` | 0.15 / 0.4 | 0.15 / 0.4 | How much the width wobbles at `Speed01` 1 (steadiest) and 0 (most sputter), as a fraction of the width |
| `flickerSpeed` | 10 | 10 | How fast the wobble changes |
| `boostLengthMultiplier` | 1.5 | 1.5 | Length multiplier at the instant of a forward boost |
| `boostDuration` | 0.3 | 0.3 | Seconds for the stretch to ease back to normal |
| `dragIntensity` | 0.1 | 0.1 | Seconds the tail takes to catch up when the player moves. 0 = always straight |
| `dragSegments` | 8 | 8 | Points along each line. More = a smoother curve. `[Min(2)]` |
| `innerWidthFraction` | 0.35 | 0.35 | Inner core width, as a fraction of the outer flame's |
| `innerLengthFraction` | 0.6 | 0.6 | Inner core length, as a fraction of the outer flame's |
| `innerColor` | white | white at HDR intensity 6, fading out | The inner core's gradient, head to tail. Never changes |
| `normalColor` | white | (set in the prefab) | The outer flame's gradient, head to tail |
| `rainbowColor` | white | (set in the prefab) | The outer flame's gradient while the rainbow shows |

The three gradients are `[GradientUsage(true)]` (HDR), so their colors can go brighter than white and glow
with Bloom. They replace the LineRenderers' own Color setting, which the script overwrites.

## How it works

### `Awake`: initializing both lines

Finds PlayerMovement and PlayerInvincibility on the player, then sets up each line once:

1. **Point count:** both lines get `dragSegments` points, and an array of the same size (`dragPoints`,
   `innerPoints`) that's created once and reused every frame.
2. **Straight start:** `FollowLine(..., snap: true)` places every point on a straight line behind the exhaust,
   at `minLength` (the inner core at 60% of it), so the first frame doesn't sweep in from the origin.
3. **Colors:** the outer line gets `normalColor`, the inner line `innerColor`.
4. **Visibility:** `coreVisible` remembers the outer line's `enabled`, and the inner line is set to match, so
   the blink check in `Update` can switch both from one value.

### `OnEnable` / `OnDisable`: subscribing to events

These are about **this script component** being switched on or off, not the lines. `OnEnable` subscribes to
four events, and `OnDisable` unsubscribes from them so nothing calls into a disabled or destroyed script:

| Event | From | Handler |
| --- | --- | --- |
| `OnSpeedBoost` | PlayerMovement | `HandleSpeedBoost` |
| `OnRainbowChanged` | PlayerInvincibility | `HandleRainbowChanged` |
| `OnGameOver` / `OnWin` | GameManager | `HandleLevelEnded` |

### `Update`: every frame

Reads the player's speed and blink state, and works out each line's size. The bending itself happens in
`FollowLine`.

1. **Blink:** copies PlayerMovement's `MeshVisible` onto both lines' `enabled`, only on frames where it
   changes.
2. **Boost stretch:** while `boostTimer` is above 0, it counts down and
   `stretch = Lerp(1, boostLengthMultiplier, boostTimer ÷ boostDuration)`. `Lerp` clamps, so the last
   frame's slightly negative timer gives exactly 1.

   | `boostTimer` | `stretch` |
   | --- | --- |
   | 0.3 (just boosted) | 1.5 |
   | 0.15 | 1.25 |
   | 0 | 1 |

3. **Length and width from speed:** `length = Lerp(minLength, maxLength, Speed01) × stretch` and
   `width = Lerp(maxWidth, minWidth, Speed01)`. Faster means longer and narrower.
4. **Flicker:** Perlin noise (−1 to 1) wobbles the width by up to `Lerp(maxFlicker, minFlicker, Speed01)`.

   | `Speed01` | Length (prefab) | Width before flicker | Flicker | Width range |
   | --- | --- | --- | --- | --- |
   | 0 | 2 | 0.5 | ±40% | 0.30 – 0.70 |
   | 0.5 | 3.5 | 0.35 | ±27.5% | 0.25 – 0.45 |
   | 1 | 5 | 0.2 | ±15% | 0.17 – 0.23 |

5. **Width onto the lines:** the outer line's `widthMultiplier` is `width`, the inner line's is
   `width × innerWidthFraction`. The multiplier scales the whole Width curve, so the taper keeps its shape.
6. **Points:** `FollowLine` for each line, then one `SetPositions` call per line.

### `FollowLine`: the drag, for both lines

Moves each point of a line toward where it would sit on a straight line behind the exhaust. Points further
back follow more lazily, which is what bends the flame when the player moves around the orbit.

For each point:

1. **Where along the outer line it sits:** `f = i ÷ (points − 1) × lengthFraction`. That's 0 at the head and 1
   at the outer tail. The inner core passes `lengthFraction` 0.6, so its tail lands at 0.6.
2. **Target:** `lineLength × f` along the `Exhaust Flame` object's +z, turned into a world position with
   `TransformPoint`. The object moves with the player, so this is how the player's position comes in.
3. **Catch-up time:** `τ = dragIntensity × f`. The head (τ = 0) snaps to the exhaust, so the line never
   detaches. Further back is lazier.
4. **Ease:** move `1 − e^(−dt/τ)` of the way to the target, which gives the same result at any frame rate. With
   `snap` true (from `Awake`), or τ = 0, it jumps straight there.

At 60 fps with `dragIntensity` 0.1, the outer tail closes 15% of the gap each frame, and is still about 0.37
units behind 0.1 s after a sudden 1-unit move.

Because an inner point at fraction 0.6 of the inner line gets the same target and catch-up time as an outer
point at 0.6 of the outer line, **the inner core traces the outer flame's curve exactly** and stays inside it.

### `HandleSpeedBoost`

Makes the flame react to a forward boost: restarts `boostTimer` at `boostDuration`, so the stretch starts again
from full. The points ease toward the longer length, so the tail grows out over a few frames.

### `HandleRainbowChanged`

Handles the invincibility power-up's change to the flame: swaps the **outer** line's gradient between
`rainbowColor` and `normalColor`. It follows the rainbow event (not invincibility itself), so it flickers in
step with the shell's warning blink. The inner core stays white.

### `HandleLevelEnded`

Runs when GameManager's `OnGameOver` or `OnWin` event announces the level has ended: hides both lines and
switches the script off (`enabled = false`), so `Update` stops. Switching off runs `OnDisable`, which
unsubscribes from the events.

## Talks to

- **PlayerMovement**: reads the static `Speed01` and `MeshVisible` every frame; subscribes to `OnSpeedBoost`.
- **PlayerInvincibility**: subscribes to `OnRainbowChanged`.
- **GameManager**: subscribes to `OnGameOver` and `OnWin`.

## Gotchas

- **`core` and `innerCore` must both be assigned.** There are no null checks, so a missing one throws errors
  from `Awake` on.
- **Both lines need Use World Space on.** The points are worked out in world space so they can lag behind the
  ship. With it off, the line is drawn offset by the object's position.
- **The flame points along `Exhaust Flame`'s +z.** Rotating that object turns both lines. The inner core
  object's own position and rotation don't matter.
- **The LineRenderers' Color setting is ignored.** The script overwrites it with the gradients. Tune the colors
  on this component.
- **Draw the Width curve as a shape, not a size.** It's multiplied by `widthMultiplier` (0.2–0.5), so a head key
  of 0.4 makes the flame only 0.08–0.2 wide.
- **`maxLength` isn't a hard cap.** A boost at full speed reaches `maxLength × boostLengthMultiplier`
  (5 × 1.5 = 7.5 in the prefab).
- **`dragSegments` is only read in `Awake`.** Changing it in Play mode does nothing until you press Play again.
  The loop uses the array's own length, so it can't run past the end.
- **The glow depends on HDR.** Keep `innerColor`'s intensity above `normalColor`'s head, or the core blends into
  the flame. Additive blending clips at white without HDR.
- **No Base Map on the material.** A round particle texture with Texture Mode Stretch makes the line fade at
  the head and show as broken pieces.
- **It switches itself off at the level end.** Re-enabling the component doesn't show the lines again.
