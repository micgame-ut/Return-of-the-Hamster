# GameOverScreen

## Purpose

The end screen shown after the player loses all their lives and the death sequence has played out. It
shows the stats from that run, the hamster's name (which also tells you how many deaths there have been:
"Apollo 4" is the fourth death), and buttons to restart the level, go to the menu, or quit the game.

## Where it lives

- **Attached to:** the `EndScreen` canvas in `Game.unity`. The canvas is always active; the screen itself
  is the `Game Over Panel` underneath it, which this script switches on. It can't go on the panel: a
  switched-off object's scripts don't run, so it could never switch itself back on.
- **Shown by:** GameManager's `DeathSequenceFinished()`, through its `gameOverScreen` Inspector slot.
- **Requires:**
  - `panel`, `hamsterNameText` and `statsText` assigned in the Inspector.
  - `GameManager.Instance` with RunStats (and LevelTimer, for progress). Without RunStats, the texts are
    left as they are.
  - Each button's **On Click ()** set to call `Restart`, `Menu` or `Quit` on this component.
  - The menu scene in **Build Settings**, for the Menu button.

### Layout (set up in the Unity Editor)

- **Vertical Layout** (800 × 640, spacing 30, Control Child Size on) stacks Title, Hamster Name Text and
  Stats Text. Because it controls the children's height, Stats Text grows to fit its lines.
- **Horizontal Layout** (1000 × 80, spacing 60) holds MENU, RESTART and QUIT, left to right in their
  Hierarchy order.
- Stats Text is **left + top** aligned with wrapping off, which the columns rely on.
- Buttons are sized, not scaled. A scale above 1 makes them overlap, because layout groups ignore scale.

## Inspector settings

| Field | Default | What it changes |
| --- | --- | --- |
| `panel` | none | The `Game Over Panel` that's switched on to show the screen |
| `hamsterNameText` | none | Text that gets the Apollo name |
| `statsText` | none | Text that gets the run's stats |
| `menuSceneName` | Menu | The scene the Menu button loads. Must match the scene file's name and be in Build Settings |

The title ("GAME OVER") is typed straight into its text in the Inspector; the script doesn't touch it.

## How it works

### `Awake`

Switches the panel off. It should already be off in the scene; this makes sure it never covers the game
from the start if someone leaves it on by accident.

### `Show`

1. **`FillStats()`** first, so the panel never shows a frame of placeholder text.
2. Switches the panel on.
3. **Unlocks and shows the cursor.** PlayerMovement locks it to the center and hides it for flying.
   Without these two lines, the buttons couldn't be clicked.

Time is frozen by then (`Time.timeScale = 0`), but UI clicks still work.

### `FillStats`

Gets RunStats through GameManager (null-safe; with no RunStats it does nothing) and fills in:

- **Hamster name:** `ApolloName`.
- **Stats**, one per line:

| Line | Comes from | Format |
| --- | --- | --- |
| Asteroids destroyed | `AsteroidsDestroyed` | whole number |
| Power-ups | `PowerUpsPickedUp` | whole number |
| Time in overdrive | `OverdriveTime` | one decimal, e.g. `4.2 s` |
| Time survived | `TimeSurvived` | minutes and seconds, e.g. `1:23` |
| Progress | `LevelTimer.Progress` | whole percentage, rounded down, e.g. `63%` |

**Time survived:** `minutes = (int)(TimeSurvived / 60)` and `seconds = (int)(TimeSurvived % 60)`. The
`(int)` cast cuts off the decimals and `%` is the remainder after dividing. For 83.7 seconds: 1 minute,
23 seconds. `{seconds:00}` always shows two digits, so 5 seconds is `1:05`.

**Progress** is rounded down (`Mathf.FloorToInt`) so the screen never says 100% for a level that wasn't
finished.

**Columns:** each line is `label<pos=70%>value`. `<pos=70%>` is a TextMesh Pro rich text tag that moves the
drawing point to 70% of the box's width, so every value starts at the same spot and they line up. The tag
is kept in one local variable, `col`, so moving the column is a one-number change. If a label is ever
longer than 70% of the box, its value is drawn over the end of the label; raise the percentage or shorten
the label.

### `Restart`

Reloads the current scene by its build index, so it still works if the scene is renamed. Everything starts
fresh: GameManager's `Awake` unfreezes time, PlayerHealth goes back to full lives, RunStats loads the next
Apollo number, and PlayerMovement locks the cursor again.

### `Menu`

Sets `Time.timeScale` back to 1, then loads `menuSceneName`. The time scale is global and survives scene
loads. GameManager normally resets it, but the menu scene has no GameManager, so without this the menu
would open frozen.

### `Quit`

Closes the game. In the Unity Editor, `Application.Quit()` does nothing, so inside `#if UNITY_EDITOR` it
stops Play mode instead (`EditorApplication.isPlaying = false`). The `#else` part is what a built game uses.

## Talks to

- **GameManager**: calls `Show()` from `DeathSequenceFinished()`. FillStats reads `RunStats` and
  `LevelTimer` through it.
- **RunStats**: reads `ApolloName` and every stat.
- **LevelTimer**: reads `Progress`.
- **PlayerMovement**: no direct link, but undoes its cursor lock.
- **SceneManager** (Unity): loads scenes for Restart and Menu.

## Gotchas

- **The Menu button fails until the menu scene is in Build Settings.** Unity logs that the scene couldn't
  be loaded.
- **The menu scene needs its own way back,** such as a Play button that loads `Game`.
- **The screen only appears on a death.** Winning doesn't show it; `Win()` has no screen yet.
- **Stats Text must stay left-aligned with wrapping off,** or the `<pos>` columns stop lining up.
- **UI buttons need an EventSystem using the Input System UI module.** The scene has one; a new scene
  would need its own.
- **If `menuSceneName` doesn't match a scene exactly** (spelling, capitals), Menu fails the same way as a
  missing scene.
