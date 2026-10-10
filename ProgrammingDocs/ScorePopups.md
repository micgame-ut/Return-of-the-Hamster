# ScorePopups

## Purpose

Controls the visual HUD popups that appear when the player earns points. It listens to Score's
`OnPointsAdded` event and shows a `+points` popup above where the points were earned, which rises and fades
out. Popups are reused from a pool.

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
| `lifetime` | 0.8 | Seconds to rise and fade out |
| `minScale` | 1 | Size for a tiny gain |
| `maxScale` | 2 | Size at `pointsForMaxScale` and above |
| `pointsForMaxScale` | 1000 | The final gain (points × multiplier) that reaches `maxScale` |
| `showMultiplierAbove` | 1.5 | The multiplier is added to the text (`+325x2.0`) only when it's above this |

## How it works

### `Popup` and the two lists

`Popup` is a small nested class bundling one text object with its own `start` position and `age`. Several
popups can be on screen at once, each started at a different time and place, so each needs its own. It's a
class (a reference type), so changing `popup.age` changes the one object, not a copy.

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
`(float, float, Vector3)` match the event's `Action<float, float, Vector3>`. With no Score, both are skipped
and no popups show.

### `Show`

Called by the event, right when Score adds points. It:

1. Takes a free popup, or creates one if they're all busy.
2. Converts the world position to a screen point (`WorldToScreenPoint`), then to a point on the canvas
   (`ScreenPointToLocalPointInRectangle`, with `null` for the camera because the canvas is Overlay). The
   second step handles the Canvas Scaler. `start` is that point plus `startOffset` up, and `age` is 0.
3. Sets the text: `+` and the points rounded to a whole number, plus `x` and the multiplier to one decimal
   if the multiplier is above `showMultiplierAbove`. The check uses the exact multiplier, so ×1.50 shows
   no multiplier while ×1.53 shows `x1.5`.
4. Sets the size: `Lerp(minScale, maxScale, points × multiplier ÷ pointsForMaxScale)`, capped at `maxScale`.
5. Places it, sets full opacity (a reused popup was faded out), switches it on, and adds it to `active`.

| Gain | Text | Final | Size |
| --- | --- | --- | --- |
| 100 at ×1.2 | `+100` | 120 | ×1.12 |
| 225 at ×1.5 | `+225` | 337 | ×1.34 |
| 325 at ×2.0 | `+325x2.0` | 650 | ×1.65 |
| 900 at ×1.8 | `+900x1.8` | 1620 | ×2 (capped) |

### `Update`

Animates every active popup. For each one, its age grows by `Time.unscaledDeltaTime`, and
`t = age ÷ lifetime` goes from 0 to 1:

- Position: `start + up × riseDistance × t`, calculated fresh from `start` each frame.
- Opacity: `1 − t`.
- When `t` reaches 1, it's switched off, removed from `active`, and pushed back onto `free`.

Unscaled time lets popups finish rising and fading during the game-over freeze. The loop runs backward,
because removing an element shifts the later ones down one index, and a forward loop would skip the one that
slid into the removed slot.

## Talks to

- **Score**: subscribes to `OnPointsAdded`. Score raises it from `AddPoints` (after the game-over guard), so
  every gain makes a popup and nothing appears after the level ends.
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
- **Overdrive won't use these popups.** It will get its own running-tally popup (stage 6 in TODO.md).
