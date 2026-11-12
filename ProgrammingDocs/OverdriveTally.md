# OverdriveTally

## Purpose

Keeps track of the running overdrive tally and displays it for the player to see. While the player is in
overdrive, the tally grows by `overdrivePointsPerSecond`. Nothing reaches the score while it counts. It's
banked into Score all at once when the player leaves overdrive (or the level ends), then the text pops,
rises and fades.

The tally is standalone: no speed or power-up multiplier, not part of the combo, and no normal `+N` popup.

## Where it lives

- **Attached to:** the HUD canvas object, which is always active. Not on the tally text itself (hiding the
  text would stop this script's `Update`).
- **Requires:** a GameManager with `Player` and `Score`, and a TextMeshPro text at a fixed HUD spot for
  `tallyText` (center pivot, Raycast Target off).

## Inspector settings

| Field | Default | What it changes |
| --- | --- | --- |
| `tallyText` | (none) | The text at the fixed HUD spot |
| `label` | OVERDRIVE | Shown in front of the number: `OVERDRIVE +70` |
| `overdrivePointsPerSecond` | 20 | How fast the tally grows while in overdrive |
| `minShownTally` | 10 | Smaller tallies are banked but never shown |
| `exitScale` | 1.4 | How big the text snaps to when overdrive ends |
| `exitColor` | yellow | The flash color when overdrive ends |
| `emphasisTime` | 0.3 | Seconds for the pop to ease back to normal |
| `riseDistance` | 60 | Canvas units it rises while fading |
| `fadeTime` | 0.8 | Seconds to rise and fade out |
| `tickInterval` | 0.05 | Seconds between count sounds while the tally is shown |
| `pitchFullAt` | 200 | The tally at which the count sound reaches its highest pitch. It stays there as the tally grows past it |

## How it works

### The three states

`State` is a private enum: **Idle** (hidden), **Counting** (in overdrive, the tally growing) and **Ending**
(the pop, rise and fade). One enum instead of two bools makes "counting and ending at once" impossible to
write.

| Overdrive time | Tally | Banked? | Shown? |
| --- | --- | --- | --- |
| 0.3 s flick | 6 | yes | no |
| 3.5 s | 70 | yes | `OVERDRIVE +70`, then the pop |
| Hit out after 2 s | 40 | yes (kept) | yes |

### `Awake`

Gets the references for the player and Score from GameManager, saves the text's resting `anchoredPosition`
and color, and hides it. The resting values are saved once, before anything moves them, because the Ending
animation changes both. Reading them later would make each tally start where the last one finished.

### `OnEnable` / `OnDisable`

Subscribe `HandleLevelEnd` to GameManager's `OnGameOver` and `OnWin`, and unsubscribe again. `OnDisable` checks
for `null` because GameManager can be destroyed first when the scene unloads.

### `Update`

Handles the frame-to-frame changes, depending on whether the player is in overdrive (`InOverdrive`, and the
level hasn't ended) and the current state:

- **Entering** (in overdrive, not Counting): `StartCounting`. This covers Idle and Ending, so a new tally
  makes a fading one vanish.
- **Staying in:** `tally += overdrivePointsPerSecond × Time.deltaTime`, then `ShowTally`, then `Tick`. Scaled
  time, so nothing is earned while the game is frozen.
- **Leaving** (not in overdrive, still Counting): `Bank`. Being hit out of overdrive takes this same path,
  so the tally is kept.
- **Ending:** `Animate`.

### `StartCounting`

Starts a fresh counter: the state becomes Counting, the tally goes to 0, `shownTally` goes to −1, `tickTimer`
goes to 0 (so the first tick plays as soon as the number appears), and the text is hidden and reset to its resting position, scale 1 and resting color.

### `ShowTally`

Visually displays the text object once the tally is big enough. It rounds the tally down and skips (guard
clause) if the whole number hasn't changed, if it's below `minShownTally`, or if there's no text. Otherwise it
writes `label +N` and makes sure the text is visible. So the text rebuilds once per whole point (about 20
times a second), not every frame.

### `Tick`

Raises the static event `OnTick(float)` at a fixed interval while the tally is counting, for the count sound.
It does nothing until the tally reaches `minShownTally`, so the sound starts when the number first appears.
`tickTimer` counts down with `Time.deltaTime`; at 0 it resets to `tickInterval` and raises the event with
`Mathf.Clamp01(tally ÷ pitchFullAt)`.

| Tally (`pitchFullAt` 200) | Value sent |
| --- | --- |
| 10 | 0.05 |
| 100 | 0.5 |
| 250 | 1 (held at the top) |

`UISFX` turns that 0 to 1 value into a pitch.

### `Bank`

Adds the tally to the score total with `Score.AddOverdriveBonus`, **always**, even under the minimum. Then:
if the tally is at least `minShownTally`, switch to Ending (timer at 0); otherwise straight back to Idle,
still hidden.

### `HandleLevelEnd`

On game over or win, banks a tally that's still **Counting**, then sets `levelEnded` so no new tally starts
during the freeze. It only banks while Counting: during Ending or Idle, `tally` still holds the last value,
which was already banked, so banking again would pay it twice.

### `Animate`

The animation after leaving overdrive, timed with `Time.unscaledDeltaTime` so it plays during the
end-of-level freeze too.

1. **Pop** (the first `emphasisTime`): `pop = 1 − endTimer ÷ emphasisTime` runs from 1 to 0, so the text
   snaps to `exitScale` and `exitColor` (`Color.Lerp`) and eases back to normal, the same snappy shape as
   Score's HUD pulse.
2. **Rise and fade** (the next `fadeTime`): `t` runs from 0 to 1. The position is the saved resting spot
   plus `riseDistance × t` up, and alpha is `1 − t` (on a copy of `startColor`, a value type, so the saved
   color isn't changed).
3. **Done:** hide it, back to Idle.

| Moment | Scale | Color |
| --- | --- | --- |
| First frame of Ending | 1.4 | `exitColor` |
| `emphasisTime` | 1.0 | resting color |
| End of the fade | 1.0 | transparent, then hidden |

## Talks to

- **PlayerMovement**: reads `InOverdrive` every frame (through GameManager's `Player`).
- **Score**: calls `AddOverdriveBonus` to bank the tally.
- **GameManager**: gets `Player` and `Score` in `Awake`; subscribes to `OnGameOver` and `OnWin`.
- **UISFX**: subscribes to the static event `OnTick`, raised in `Tick`.

## Gotchas

- **Don't put it on the tally text.** `Bank` and `Animate` hide the text, which would switch off this
  script's `Update`.
- **Score's `AddOverdriveBonus` has no game-over guard**, so banking at the level end works. This script must
  only bank once per overdrive, which the Counting checks guarantee.
- **The shown number is rounded down; the banked amount isn't.** A tally of 70.6 shows `+70` but adds 70.6.
  The HUD rounds the total down too, so it's at most a fraction of a point off.
- **Flicking in and out at the top of overdrive** makes many small tallies. Each one is banked; the ones under
  `minShownTally` stay invisible.
