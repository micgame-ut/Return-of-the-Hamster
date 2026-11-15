# ScorePopups

## Purpose

Controls the visual HUD popups that appear when the player earns points. It listens to Score's
`OnPointsAdded` event and shows a `+points` popup above where the points were earned, which rises and fades
out. It also listens to PowerUp's `OnCollected` event and shows a label for the power-up that was picked
up ("+1 LIFE", "INVINCIBLE", ...) the same way, without any points. Popups are reused from a pool.

## Where it lives

- **Attached to:** the HUD canvas object itself (the one with the Canvas component), next to the score and
  danger warning. `Awake` treats its own transform as the canvas, so it must sit on a UI object.
- **Requires:** a popup prefab (a TextMeshPro UI text), and a GameManager with `GameplayCamera` and `Score`.
- **Canvas:** an Overlay canvas. The scene's Canvas Scaler is Scale With Screen Size at 1280 × 1024, so on
  the target monitor the pixel values below are exact screen pixels, and on other screens they scale with
  the rest of the HUD.

## Inspector settings

| Field | Default | What it changes |
| --- | --- | --- |
| `popupPrefab` | (none) | The popup's look: font, size, color, glow. Center alignment and a center pivot, Raycast Target off |
| `prewarmCount` | 5 | Popups made at level start. More are made if needed |
| `startOffset` | 40 | Canvas units above the earning body where a popup appears |
| `riseDistance` | 80 | Canvas units it rises over its life |
| `lifetime` | 0.8 (1.1 on the HUD canvas) | Seconds to rise and fade out, including the `holdTime` |
| `holdTime` | 0.3 | Seconds a popup stays fully opaque before it starts to fade. Keep it below `lifetime` |
| `crowdSpeedUp` | 0.25 | How much faster popups hold and fade for each popup showing beyond the first. 0.25 means 3 popups fade 1.5 times as fast. 0 turns it off |
| `maxSpeedUp` | 3 | The most the hold and fade can speed up, as a multiple of normal speed. 1 turns it off |
| `stackSpacing` | 50 | Canvas units between stacked popups, times the popup's scale. A new popup kicks any popup it would overlap up so they end this far above it |
| `stackWidth` | 300 | How close sideways two popups must be to count as overlapping, in canvas units. Keep it larger than a label's width |
| `minScale` | 1 | Size for a tiny gain |
| `maxScale` | 2 | Size at `pointsForMaxScale` and above |
| `pointsForMaxScale` | 1000 | The final gain (points × multiplier) that reaches `maxScale` |
| `showMultiplierAbove` | 1.5 | The multiplier is added to the text (`+325x2.0`) only when it's above this |
| `healthLabel` | +1 LIFE | Shown when a Health power-up (a fixed one, since a Random pickup never gives Health) is picked up. Empty = no popup |
| `invincibleLabel` | INVINCIBLE | Shown for Invincible |
| `twinLabel` | TWIN | Shown for Twin |
| `scoreLabel` | SCORE x2 | Shown for Score Multiplier |
| `rechargeLabel` | RECHARGE | Shown for Boost Recharge |
| `jackpotLabel` | JACKPOT | Shown for Jackpot |

## How it works

### `Popup` and the two lists

`Popup` is a small nested class bundling one text object with its own `start` position, `age`, `fadeAge`, `lift` and `scale`.
Several popups can be on screen at once, each started at a different time and place, so each needs its own. It's a
class (a reference type), so changing `popup.age` changes the one object, not a copy. `fadeAge` is a second age for the
hold and fade, which runs faster when the screen is crowded (see `Update`). `lift` is the extra height a popup has been
kicked up by newer popups, and `scale` is its size, used for the gap above it.

- `free` is a `Stack` of popups waiting to be used (like EffectPool's pool).
- `active` is a `List` of popups currently showing, walked every frame.

### `Awake`

Finds its references: the canvas (`(RectTransform)transform`, a cast that only works on a UI object), the
gameplay camera and Score from GameManager. Then prewarms the pool with `prewarmCount` popups so the first
gains don't stutter.

### `CreatePopup`

Makes a new, hidden popup copy for the pool: instantiates the prefab as a child of the canvas, switches it
off, and wraps it in a `Popup` (`new Popup { text = text }`, an object initializer). Called by `Awake` to
prewarm, and by `Show` when every pooled popup is busy. The copies belong to the scene, so they're cleaned
up with it.

### `OnEnable` / `OnDisable`

Subscribes `Show` to `Score.OnPointsAdded`, and unsubscribes it. `Show`'s parameters
`(float, float, Vector3, string)` match the event's `Action<float, float, Vector3, string>`. With no Score, both are skipped
and no points popups show.

It also subscribes `ShowPowerUp` to the static `PowerUp.OnCollected`, and unsubscribes in `OnDisable`. That
unsubscribe really matters: a static event belongs to the class and outlives the scene, so without it a
restart would leave the old, destroyed ScorePopups subscribed, and the next pickup would call into it.

### `Show`

Called by Score's event, right when Score adds points. It works out the text and size (steps 3 and 4 below),
then hands them to `Spawn`, which does steps 1, 2 and 5.

1. `Spawn` takes a free popup, or creates one if they're all busy.
2. `Spawn` converts the world position to a screen point (`WorldToScreenPoint`), then to a point on the canvas
   (`ScreenPointToLocalPointInRectangle`, with `null` for the camera because the canvas is Overlay). The
   second step handles the Canvas Scaler. `start` is that point plus `startOffset` up, and `age` is 0.
3. Sets the text: `+` and the points rounded to a whole number, plus `x` and the multiplier to one decimal
   if the multiplier is above `showMultiplierAbove`. The check uses the exact multiplier, so ×1.50 shows
   no multiplier while ×1.53 shows `x1.5`. If there's a label, it goes in front (`PERFECT WAVE +500`).
   `string.IsNullOrEmpty` treats both `null` (asteroids pass no label) and `""` (a label cleared in the
   Inspector) as no label, so there's never a stray space in front of the number.
4. Sets the size: `Lerp(minScale, maxScale, points × multiplier ÷ pointsForMaxScale)`, capped at `maxScale`.
5. `Spawn` sets the text and size, places it, sets full opacity (a reused popup was faded out), switches it
   on, and adds it to `active`.

### `ShowPowerUp`

Called by `PowerUp.OnCollected` with the type that was applied (for a Random power-up, the rolled one) and
where. A switch expression picks that type's label; any other type (Random with no template) gets `null`.
If the label isn't empty, it calls `Spawn(label, minScale, position)`: the same rise and fade as a points
popup, at the smallest size, with no `+points` part. Clearing a label in the Inspector hides that popup.

### `Spawn`

The shared part of every popup: take one from the pool, place it `startOffset` above the world position on
the canvas (it always takes that spot), call `KickUp` so any popup already showing there moves up out of its way, reset its
`age` and `lift`, store its `scale`, set its text and size, full opacity, switch it on and add it to `active`. `Show` and `ShowPowerUp` both use it, so points
popups and labels behave the same.

### `KickUp`

Stops popups covering each other, so each one stays readable, by moving the **older** popups instead of the new one.
It sorts `active` from lowest to highest (current `localPosition.y`, because older popups have been rising; `Sort` takes
a lambda, as in `AsteroidSpawner`), then walks up with a running minimum height `minY`, which starts at the new popup's
start plus `stackSpacing × scale`. It skips popups more than `stackWidth` sideways away, and popups well below the new
spot. A popup lower than `minY` gets its `lift` raised by the difference, so it sits at `minY`, and the next popup has to
clear that one by its own `stackSpacing × scale`. The new popup isn't in `active` yet, so it never checks itself.

Example (spacing 50, scale 1): `+250` is at y = 47. A close call spawns at 40 and stays there, and `+250` is kicked up
to 90. A power-up label then spawns at 40, the close call is kicked to 90 and `+250` to 140. A popup more than
`stackWidth` away sideways is ignored. With nothing showing, nothing moves.

| Gain | Text | Final | Size |
| --- | --- | --- | --- |
| 100 at ×1.2 | `+100` | 120 | ×1.12 |
| 225 at ×1.5 | `+225` | 337 | ×1.34 |
| 325 at ×2.0 | `+325x2.0` | 650 | ×1.65 |
| 900 at ×1.8 | `+900x1.8` | 1620 | ×2 (capped) |

### `Update`

Animates every active popup. First it works out one speed-up for the frame:
`speedUp = min(1 + crowdSpeedUp × (active popups − 1), maxSpeedUp)`. With one popup it is 1, and with `crowdSpeedUp` 0.25
and `maxSpeedUp` 3 it is 1.5 for 3 popups, 2 for 5 and 3 for 9 or more. Then, for each popup, `age` grows by
`Time.unscaledDeltaTime` and `fadeAge` by that times `speedUp`, and `t = age ÷ lifetime` goes from 0 to 1:

- Position: `start + up × (riseDistance × t + lift)`, calculated fresh from `start` each frame. `lift` is how far newer
  popups have kicked it up. The rise uses the normal `age`, so a crowd doesn't make popups climb faster.
- Opacity: fully opaque until `fadeAge` reaches `holdTime`, then it fades to 0 over the rest of `lifetime`:
  `fadeT = Clamp01((fadeAge − holdTime) ÷ max(lifetime − holdTime, 0.01))`, opacity `1 − fadeT`. The `max` avoids
  dividing by zero if `holdTime` is `lifetime` or more.
- When `fadeAge` reaches `lifetime` (fully faded), it's switched off, removed from `active`, and pushed back onto `free`.
  `speedUp` is never below 1, so `fadeAge` is never behind `age`: the popup is always gone by the time it would have
  finished rising, and with one popup the two ages are equal, so nothing changes.

Unscaled time lets popups finish rising and fading during the game-over freeze. The loop runs backward,
because removing an element shifts the later ones down one index, and a forward loop would skip the one that
slid into the removed slot.

## Talks to

- **Score**: subscribes to `OnPointsAdded`. Score raises it for every gain (after the game-over guard), so
  every gain makes a popup and nothing appears after the level ends. Wave clears come with a label and a
  ×1 multiplier, so they never show an `x` part.
- **PowerUp**: subscribes to the static `OnCollected` to show power-up labels.
- **GameManager**: gets `GameplayCamera` and `Score` in `Awake`.
- **AsteroidBehavior**: no direct link, but it passes the smashing body's position through Score, which is
  why popups appear above the body that earned them.

## Gotchas

- **Must be on the canvas object (or another UI object).** On a non-UI object, the `RectTransform` cast in
  `Awake` throws.
- **`popupPrefab` left empty throws in `Awake`** when it tries to prewarm.
- **No null check on GameManager or the camera.** With no GameManager, `Awake` throws. With no gameplay
  camera, `Show` throws on the first gain.
- **The multiplier check uses the exact value**, so the text can show `x1.5` for ×1.53 but nothing for
  ×1.50. To change that, compare the rounded value instead.
- **The prefab's pivot matters.** Scaling and placement happen around it, so a non-center pivot makes big
  popups grow off to one side.
- **Labels make popups much wider.** If `PERFECT WAVE +500` wraps onto two lines, widen the prefab's Rect
  Transform or turn off Wrapping.
- **Keep the `OnCollected` unsubscribe in `OnDisable`.** It's a static event; removing the `-=` leaves
  destroyed popups subscribed after every restart.
- **A new power-up type needs a label here** (a field and a line in `ShowPowerUp`), or it shows no popup.
- **Stacking only looks at popups sideways-close and still showing.** A popup that has faded out and returned to the
  pool no longer blocks anything. A busy moment can push the older popups high up the screen, because each new one
  kicks the whole stack up. The kick is instant, so older popups jump up when a new one arrives.
- **Set `lifetime` to about 1.1 on the canvas.** With the 0.8 default and a 0.3 hold, the fade lasts only 0.5 s.
- **Overdrive won't use these popups.** It will get its own running-tally popup (stage 6 in TODO.md).
