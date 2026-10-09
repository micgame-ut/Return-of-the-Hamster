# DangerWarning

## Purpose

Controls the UI on the HUD when the player is at 1 life left. Red glows pulse in from the left and right
screen edges, growing wider and brighter, then narrower and dimmer, and a warning sign blinks in each corner
in time with the pulse. It turns off at 2 lives (a Health pickup), at 0 (death), and on a win.

## Where it lives

- **Attached to:** the `HUD` canvas in `TestGame.unity` (Screen Space Overlay, Canvas Scaler set to Scale
  With Screen Size, reference 1280 × 1024). Its Sort Order is lower than the EndScreen canvas, so the end
  screens draw on top.
- **Requires:**
  - `GameManager.Instance` with a PlayerHealth, to know the lives. Without it, the warning never turns on.
  - `warningPrefab`: a UI prefab (`Assets/Prefabs/Exclamation.prefab`, currently a placeholder "!", to be
    replaced with a warning-sign graphic). Without it, only the glows show.

## Inspector settings

| Field | Default | What it changes |
| --- | --- | --- |
| `warningPrefab` | none | The sign copied into each corner. Its anchor and pivot are set by code |
| `cornerInset` | 40 | Pixels in from both edges of each corner (on the 1280 × 1024 reference) |
| `blinkInterval` | 0.4 | Seconds the signs are on, then off. One full pulse lasts two intervals. `[Min(0.01)]` stops 0, which would break the math |
| `overlayColor` | red | The glows' color (multiplied with the white texture) |
| `overlayAlphaMin` / `overlayAlphaMax` | 0.4 / 0.8 | How opaque the glow's edge is at the pulse's low and high points |
| `overlayWidthMin` / `overlayWidthMax` | 0.12 / 0.22 | How wide each glow is at the pulse's low and high points, as a fraction of the canvas width (about 154 / 282 px) |

## How it works

### `Awake`

Finds PlayerHealth through GameManager, builds the gradient texture, creates both glows, then creates and
pins a sign in each corner (all hidden).

- The glows are created **before** the signs. UI draws later children on top, so the signs stay in front
  of the red.
- **Pinning to a corner:** each sign's `anchorMin`, `anchorMax` and `pivot` are all set to its corner from
  `Corners` ((0,0), (0,1), (1,0), (1,1)), then it's pushed inward by `cornerInset`: `+` on the left/bottom,
  `−` on the right/top. Corners are fractions of the canvas, so they stay in the corners at any resolution
  or aspect ratio. The pivot on the sign's own corner means the inset is measured to its edge, so it can't
  poke off-screen.
- The signs are made once and reused (object pooling), not created and destroyed each blink.

### `CreateGradient`

Makes a 256×1 white texture whose alpha fades `(1 − t)²` from the edge inward, once.

- **White** so the RawImage's `color` can tint it to any color without rebuilding the texture.
- **Squared** so it stays strong near the edge and fades softly inward.
- **Clamp** wrapping stops the ends bleeding into each other, and **no mipmaps** since flat UI never needs
  them. `SetPixels` then `Apply` writes everything in one go.

| t (how far in) | Alpha |
| --- | --- |
| 0 (edge) | 1.00 |
| 0.25 | 0.56 |
| 0.5 | 0.25 |
| 0.75 | 0.06 |
| 1 (inner edge) | 0 |

### `CreateOverlay`

Makes one side glow: pinned to its edge, full height, mirrored on the right, hidden.

- Its anchor x is the edge (0 or 1), and its anchor y runs from 0 to 1, which **stretches** it to full height.
  The pivot is on the edge, so changing the width grows it inward.
- `sizeDelta = zero` means exactly screen height and zero width. `Update` sets the width.
- The right side reads the texture backwards (`uvRect` x = 1, width = −1), so the opaque end is at the right
  edge. It's one texture for both sides.
- `raycastTarget = false`, so it doesn't block clicks.

### `OnDestroy`

Destroys the gradient texture. A `Texture2D` made with `new` isn't part of the scene, so it isn't cleaned up
when the scene unloads (see AGENTS.md section 12).

### `Start`

Turns the warning on if you start at 1 life. It runs after every `Awake`, so `CurrentLives` is already set.

### `OnEnable` / `OnDisable`, `HandleLivesChanged` and `HandleWin`

Sign up to and leave `OnLivesChanged` and GameManager's `OnWin`; `OnDisable` also turns the warning off, so
a disabled HUD can't leave the red up. The lives handler is `SetDanger(lives == 1)`. It uses the value
passed in directly, because the decision depends on nothing else.

**During the twin power-up** the warning is paused. `HandleLivesChanged` adds `&& !Twinned`, and
`HandleTwinChanged` (subscribed to PlayerTwins' `OnTwinChanged`) turns it off when the twin starts and back to
`CurrentLives == 1` when it ends. Lives don't change during the twin, so the warning comes back exactly as it
was before the pickup.

`HandleWin` is `SetDanger(false)`. A win doesn't change lives, so `OnLivesChanged` can't catch it. It only
listens for a win: a game over already turns the warning off, because lives reach 0 on the same hit.

### `SetDanger`

Turns the warning on or off: resets the blink, hides the signs, and shows or hides the glows. A guard skips
it when the state isn't changing (for example, a Health pickup at 2 lives).

The signs always start hidden, even when turning on. The first `Update` sees an "on" moment and shows them.
The glows appear at zero width, then the first `Update` puts them at the top of the pulse (widest and
brightest) as the signs appear.

### `Update`

While on: advances the timer, blinks the signs (only when they flip), and pulses the glows' width and alpha
together.

1. **Not on?** Returns straight away, which is most frames, including every frame after a win (see
   `HandleWin`).
2. **Blink:** the signs are on when `FloorToInt(blinkTimer / blinkInterval)` is even. `SetWarnings` is only
   called when that flips.
3. **Pulse:** `pulse = 0.5 + 0.5 × cos(π × blinkTimer / blinkInterval)` goes 1 → 0 → 1 over one full blink
   cycle. It drives both Lerps: width (as a fraction of the canvas width) and alpha. `color` is a **copy** of
   `overlayColor` (a value type), so changing its alpha doesn't change the field.

| `blinkTimer` | `pulse` | Width (px) | Edge alpha | Signs |
| --- | --- | --- | --- | --- |
| 0.0 | 1 | 282 | 0.8 | on |
| 0.2 | 0.5 | 218 | 0.6 | on |
| 0.4 | 0 | 154 | 0.4 | off |
| 0.6 | 0.5 | 218 | 0.6 | off |
| 0.8 | 1 | 282 | 0.8 | on |

### `SetOverlayLook`

Applies one width and color to one glow (`sizeDelta = (width, 0)`, so the height stays full). It's `static`,
because it only uses what's passed in.

### `SetWarnings`

Shows or hides all four signs, and records which in `warningsOn`.

## Talks to

- **GameManager**: gets PlayerHealth and PlayerTwins in `Awake`; subscribes to `OnWin`.
- **PlayerHealth**: subscribes to `OnLivesChanged`; reads `CurrentLives` in `Start` and when the twin ends.
- **PlayerTwins**: subscribes to `OnTwinChanged`; reads `IsTwinned`. The warning stays off during the twin.

## Gotchas

- **It doesn't use the Global Volume.** An earlier version tinted the URP Vignette, but a vignette multiplies
  the colors already on screen, so it barely showed over dark space. The overlay paints red on top instead.
- **The warning prefab's anchor and pivot are overwritten** by code, so set its size, not its position.
- **It keeps blinking on game over only until lives hit 0**, which happens on the same hit, so it's already
  off by the time the game freezes.
- **Sort Order matters.** If the HUD canvas is above the EndScreen canvas, the red glows draw over the end
  screens.
