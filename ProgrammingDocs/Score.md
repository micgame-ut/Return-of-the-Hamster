# Score

## Purpose

Keeps track of the player's score for this level and shows it on the HUD. The score has two parts: time
points that grow with the level timer's progress, and event points that other scripts add with
`AddPoints`. Asteroid smashes go through `AddComboKill`, which adds a growing combo bonus for quick
chains of kills. The score starts at 0 in every level.

## Where it lives

- **Attached to:** the `Game Manager` GameObject, next to LevelTimer and RunStats. GameManager finds it
  there automatically and exposes it as `GameManager.Instance.Score`.
- **Requires:** `GameManager.Instance` with a LevelTimer, and a TextMeshPro text on the HUD for
  `scoreText`.

## Inspector settings

| Field | Default | What it changes |
| --- | --- | --- |
| `pointsPerSecond` | 1 | Time points per second of `levelTime`. A full level gives `levelTime × pointsPerSecond` time points, however fast the player flies |
| `maxSpeedMultiplier` | 2 | What points from `AddPoints` are multiplied by at max speed. At speed 0 it's ×1, and in between it rises in a straight line. Doesn't affect the time score |
| `comboWindow` | 1.5 | Seconds after a kill to make the next one and keep the combo going. Each kill resets it |
| `comboStepBonus` | 50 | How much the combo bonus grows after the first kill |
| `comboStepGrowth` | 0.5 | How much bigger each later step is, as a fraction of `comboStepBonus` (0.5 → steps of 50, 75, 100…). 0 makes every step the same |
| `scoreText` | (none) | The HUD text the score is written to, as six zero-padded digits (`000046`). Can be left empty: the score still counts, it just isn't shown |
| `pulseScale` | 1.15 | How big the HUD score snaps to when points are added through `AddPoints`. 1 turns the pulse off |
| `pulseDuration` | 0.15 | Seconds to shrink back to normal size after a pulse |

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
look it up every frame. It also stores the player (`PlayerMovement`) and `PlayerTwins`, for the hit events.
This is safe in `Awake` because GameManager runs first (`[DefaultExecutionOrder(-100)]`).

### `OnEnable` / `OnDisable`

Subscribe `BreakCombo` to `PlayerMovement.OnHit` and `PlayerTwins.OnBodyLost`, and unsubscribe again. So a
hit that counts, or losing a body during the twin, ends the combo. Unsubscribing in `OnDisable` keeps the
events from calling a disabled or destroyed Score, and stops a re-enable from subscribing twice. Each one
is skipped if its reference is missing.

### `Update`

**Combo countdown first.** While `comboTimer` is above 0, it counts down by `Time.deltaTime`, and calls
`BreakCombo` when it runs out. This has to come before the HUD lines, because their early `return` leaves
`Update` on most frames, which would almost stop the countdown.

**HUD pulse next, for the same reason.** While `pulseTimer` is above 0, it counts down by
`Time.unscaledDeltaTime` and sets the HUD text's scale to `Lerp(1, pulseScale, pulseTimer ÷ pulseDuration)`.
The text snaps big when points land and shrinks back. Unscaled time lets it finish during the game-over
freeze. Lerp clamps a slightly negative timer to 0, so the last frame always sets the scale to exactly 1.
The scale is set fresh each frame, not multiplied, so it can't drift over many pulses.

Then it recalculates the score every frame: the time part from the level timer's progress, plus the event points.

If the whole number (rounded down with `Mathf.FloorToInt`) has changed since last frame, it updates the
HUD text with `ToString("D6")`. Only writing when the number changes avoids making a new string every
frame. `shownPoints` starts at -1, a value the score can never be, so the very first frame always writes
`000000` over the text's placeholder.

### `AddPoints`

The method other scripts call to add points, with the world position they came from. It ignores calls
after game over or a win. Otherwise it multiplies the amount by the speed multiplier, adds the result to
`eventPoints`, raises **`OnPointsAdded(amount, multiplier, position)`**, and starts the HUD pulse. Keeping
the guard and the multiplier here means no caller can forget either one.

`OnPointsAdded` is an `Action<float, float, Vector3>`: the base points and the multiplier are passed
separately so ScorePopups can show both (`+325x2.0`) and still work out the final amount. It's raised
after the guard, so no popups appear after the level ends.

The speed multiplier is `Mathf.Lerp(1, maxSpeedMultiplier, PlayerMovement.Speed01)`, worked out on each
call, so it's fixed at the speed the player had at that moment. With a 10-point asteroid and the default
`maxSpeedMultiplier` of 2:

| Speed when it's smashed | `Speed01` | Multiplier | Points added |
| --- | --- | --- | --- |
| 0 (invincible smash) | 0 | ×1 | 10 |
| 5 | 0.5 | ×1.5 | 15 |
| 10 (max) | 1 | ×2 | 20 |

A destructible asteroid can only be smashed at its `destroySpeed` or faster, so harder asteroids
automatically get a bigger multiplier.

### `AddComboKill`: asteroid smashes and the combo

Called by AsteroidBehavior with the asteroid's `scorePoints` and the position of the body that smashed it. It:

1. Adds `points + comboBonus` through `AddPoints` (passing the position on), so the guard and the speed multiplier apply to the
   bonus too.
2. Grows `comboBonus` by this step: `comboStepBonus × (1 + comboStepGrowth × comboCount)`, using the count
   before this kill.
3. Adds one to `comboCount`.
4. Resets `comboTimer` to `comboWindow`.

`comboBonus` is a running total: the bonus a kill gets is all the earlier steps added up. With the
defaults and a 100-point asteroid (speed ×1):

| Kill | Bonus given | Step added after | Points added |
| --- | --- | --- | --- |
| 1st | 0 | 50 | 100 |
| 2nd | 50 | 75 | 150 |
| 3rd | 125 | 100 | 225 |
| 4th | 225 | 125 | 325 |

There's no cap, so long chains (an invincibility run) keep growing.

### `BreakCombo`

Sets `comboCount` and `comboBonus` back to 0. Called by the hit events and when the combo timer runs out.
It doesn't need to reset `comboTimer`: the next kill sets it again, and with the count at 0 any leftover
time changes nothing.

### When the level ends

Nothing in Score stops the time part. LevelTimer stops moving `Progress` at game over and caps it at 1
when the earth arrives, so the time part stops by itself. `AddPoints` stops taking points. `Update` keeps
running, so the HUD always shows the real final score.

## Talks to

- **GameManager**: gets `LevelTimer`, `Player` and `PlayerTwins` in `Awake`; reads `IsGameOver` and
  `HasWon` in `AddPoints`. GameManager finds Score with `GetComponent` and exposes it as `Score`.
- **LevelTimer**: reads `Progress` and `LevelTime` every frame.
- **PlayerMovement**: reads the static `Speed01` in `AddPoints` for the speed multiplier; subscribes to
  `OnHit` to end the combo.
- **PlayerTwins**: subscribes to `OnBodyLost` to end the combo.
- **AsteroidBehavior**: calls `AddComboKill(scorePoints, bodyPosition)` when an asteroid is smashed.
  Waves, close calls and the overdrive bonus will call `AddPoints` (see TODO.md).
- **ScorePopups**: subscribes to `OnPointsAdded` to show a popup for each gain.

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
- **`maxSpeedMultiplier` below 1 punishes speed.** At 0.5, a player at max speed gets half the points of a
  slow one.
- **`comboWindow` of 0 means the combo never runs out.** The timer is set to 0, so the countdown never runs,
  and only a hit ends the combo.
- **The combo keeps counting after the level ends.** `AddComboKill` still grows `comboCount` and
  `comboBonus` after game over or a win, but `AddPoints` ignores the points, so nothing is scored.
- **Past 999,999 the HUD shows 7 digits.** `D6` is a minimum width, not a limit.
- **Nothing is saved.** The score lives on this scene's Score object, so a reload starts at 0.
