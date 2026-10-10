# Score

## Purpose

Keeps track of the player's score for this level and shows it on the HUD. The score has two parts: time
points that grow with the level timer's progress, and event points that other scripts add with
`AddPoints`. The score starts at 0 in every level.

## Where it lives

- **Attached to:** the `Game Manager` GameObject, next to LevelTimer and RunStats. GameManager finds it
  there automatically and exposes it as `GameManager.Instance.Score`.
- **Requires:** `GameManager.Instance` with a LevelTimer, and a TextMeshPro text on the HUD for
  `scoreText`.

## Inspector settings

| Field | Default | What it changes |
| --- | --- | --- |
| `pointsPerSecond` | 1 | Time points per second of `levelTime`. A full level gives `levelTime × pointsPerSecond` time points, however fast the player flies |
| `scoreText` | (none) | The HUD text the score is written to, as six zero-padded digits (`000046`). Can be left empty: the score still counts, it just isn't shown |

## How it works

### The two parts of the score

| Part | Kind | Where it comes from |
| --- | --- | --- |
| Time points | Calculated fresh every frame | `Progress × LevelTime × pointsPerSecond` |
| `eventPoints` | Running total | Added to by `AddPoints` |

`Total` (read-only from outside) is the two added together. Because the time part is calculated fresh from
`Progress`, it always matches how far through the level the player is. Flying faster makes `Progress`, and
so the score, grow faster, but a full level is always worth the same.

With `levelTime` 60 and `pointsPerSecond` 0.5, dying at `Progress` 0.7 with 25 event points:

| Part | Calculation | Points |
| --- | --- | --- |
| Time | 0.7 × 60 × 0.5 | 21 |
| Events | | 25 |
| Total | | 46, shown as `000046` |

### `Awake`

Finds the LevelTimer through GameManager and stores it in the `timer` field, so `Update` doesn't have to
look it up every frame. This is safe in `Awake` because GameManager runs first
(`[DefaultExecutionOrder(-100)]`).

### `Update`

Recalculates the score every frame: the time part from the level timer's progress, plus the event points.

If the whole number (rounded down with `Mathf.FloorToInt`) has changed since last frame, it updates the
HUD text with `ToString("D6")`. Only writing when the number changes avoids making a new string every
frame. `shownPoints` starts at -1, a value the score can never be, so the very first frame always writes
`000000` over the text's placeholder.

### `AddPoints`

The method other scripts call to add points. It ignores calls after game over or a win, and otherwise adds
the amount to `eventPoints`. Keeping the guard here means no caller can forget it.

### When the level ends

Nothing in Score stops the time part. LevelTimer stops moving `Progress` at game over and caps it at 1
when the earth arrives, so the time part stops by itself. `AddPoints` stops taking points. `Update` keeps
running, so the HUD always shows the real final score.

## Talks to

- **GameManager**: gets `LevelTimer` in `Awake`; reads `IsGameOver` and `HasWon` in `AddPoints`.
  GameManager finds Score with `GetComponent` and exposes it as `Score`.
- **LevelTimer**: reads `Progress` and `LevelTime` every frame.
- Nothing calls `AddPoints` yet. Asteroids, waves, close calls and the overdrive bonus will (see TODO.md).

## Gotchas

- **No null checks on GameManager or LevelTimer.** With no GameManager, `Awake` throws. With no LevelTimer,
  `Update` throws every frame.
- **Changing `pointsPerSecond` during Play mode rescales the whole time part**, not just from then on,
  because the time part is calculated fresh, not added up.
- **Update order isn't fixed.** Score may run before LevelTimer in a frame and read last frame's
  `Progress`. It's a sixtieth of a second behind at most, and the final score is still exact.
- **`Total` is a `float`; the HUD shows it rounded down.** Anything that saves or ranks the score (the
  future leaderboard) should round the same way so it matches what the player saw.
- **Points added after game over in the same frame are lost.** If something calls `AddPoints` after
  `GameOver()` or `Win()` has run, it's ignored, even in the same frame.
- **Past 999,999 the HUD shows 7 digits.** `D6` is a minimum width, not a limit.
- **Nothing is saved.** The score lives on this scene's Score object, so a reload starts at 0.
