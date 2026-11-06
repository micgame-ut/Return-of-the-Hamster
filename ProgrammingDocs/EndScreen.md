# EndScreen

## Purpose

The end-of-level screens. One component handles both:

- **Game Over**, shown after the player loses all their lives and the death sequence has played out:
  "GAME OVER", the Apollo's name, their stats, and Restart / Menu / Quit.
- **Win**, shown when the level is cleared: "MISSION ACCOMPLISHED", the Apollo's name, their stats (no
  progress line), and Next Level / Menu / Quit.

The stats are the Apollo's totals so far, across every level they've played. The name also tells you how
many deaths there have been: on the Game Over screen, "Apollo 4" is the fourth death.

It was called `GameOverScreen` until the win screen was added.

## Where it lives

- **Attached to:** the `EndScreen` canvas in `Game.unity`. The canvas is always active; each screen is a
  panel underneath it (`Game Over Panel`, `Win Panel`), which this script switches on. It can't go on a
  panel: a switched-off object's scripts don't run, so it could never switch itself back on.
- **Shown by:** GameManager, through its `endScreen` slot. `DeathSequenceFinished()` calls
  `ShowGameOver()`, and `Win()` calls `ShowWin()`.
- **Requires:**
  - Both groups (Game Over and Win) filled in, in the Inspector.
  - `GameManager.Instance` with RunStats (and LevelTimer, for progress). Without RunStats, the texts are
    left as they are.
  - Every button's **On Click ()** pointing at this component on the EndScreen canvas, calling `Restart`,
    `NextLevel`, `Menu` or `Quit`.
  - Build Settings with **Menu at index 0** and the levels after it (`Game` at 1).

### Layout (set up in the Unity Editor)

Each panel has the same layout:

- **Vertical Layout** (800 × 640, spacing 30, Control Child Size on) stacks Title, Hamster Name Text and
  Stats Text. Because it controls the children's height, Stats Text grows to fit its lines.
- **Horizontal Layout** (1000 × 80, spacing 60) holds the three buttons, left to right in their Hierarchy
  order.
- Stats Text is **left + top** aligned with wrapping off, which the columns rely on.
- Buttons are sized, not scaled. A scale above 1 makes them overlap, because layout groups ignore scale.

## Inspector settings

| Field | Default | What it changes |
| --- | --- | --- |
| `gameOver` | — | The Game Over screen's parts (see below) |
| `win` | — | The Win screen's parts (see below) |
| `menuSceneName` | Level_1 | The scene the Menu button loads, and Next Level's fallback. The menu lives in Level 1. Must match the scene file's name and be in Build Settings |

Each group (`ScreenParts`) has:

| Field | Game Over | Win | What it is |
| --- | --- | --- | --- |
| `panel` | Game Over Panel | Win Panel | The panel switched on to show this screen |
| `hamsterNameText` | its name text | its name text | Text that gets the Apollo name |
| `statsText` | its stats text | its stats text | Text that gets the stats |
| `showProgress` | ✓ | ✗ | Whether to add the Progress line |

The titles are typed straight into their texts in the Inspector; the script doesn't touch them.

## How it works

### `ScreenParts` (`[System.Serializable]`)

Both screens need the same four settings, so they're bundled into a small class inside EndScreen and used
twice. `[System.Serializable]` lets Unity save a plain class's fields and show them in the Inspector, as a
collapsible group. ObjectSpawner's `Wave` uses the same idea. The class is `private` because only
EndScreen uses it.

### `Awake`

Switches both panels off. They should already be off in the scene; this makes sure neither covers the game
from the start if someone leaves one on by accident.

### `ShowGameOver`, `ShowWin` and `Show`

`ShowGameOver()` and `ShowWin()` are one-line methods (`=> Show(gameOver)`, `=> Show(win)`), so the name
says which screen you want. Both use one private `Show(parts)`:

1. **`FillStats(parts)`** first, so the panel never shows a frame of placeholder text.
2. Switches that screen's panel on.
3. **Doesn't touch the cursor.** The game has no mouse pointer: the buttons are used with the trackball and the
   one button through each panel's `MenuNavigator`, which keeps the cursor hidden and locked. (`Show` used to
   unlock and show the cursor so the buttons could be clicked.)

Time is frozen on both screens (`Time.timeScale = 0`). `MenuNavigator` runs on real input, so it still works.

### `FillStats`

Gets RunStats through GameManager (null-safe; with no RunStats it does nothing) and fills in the given
screen's texts:

- **Hamster name:** `ApolloName`.
- **Stats**, one per line:

| Line | Comes from | Format |
| --- | --- | --- |
| Asteroids destroyed | `AsteroidsDestroyed` | whole number |
| Power-ups | `PowerUpsPickedUp` | whole number |
| Time in overdrive | `OverdriveTime` | one decimal, e.g. `4.2 s` |
| Time survived | `TimeSurvived` | minutes and seconds, e.g. `1:23` |
| Progress (only if `showProgress`) | `LevelTimer.Progress` | whole percentage, rounded down, e.g. `63%` |

The first four are the Apollo's totals across levels. Progress is the current level only. Levels beaten is
tracked by RunStats but not shown.

**Time survived:** `minutes = (int)(TimeSurvived / 60)` and `seconds = (int)(TimeSurvived % 60)`. The
`(int)` cast cuts off the decimals and `%` is the remainder after dividing. For 83.7 seconds: 1 minute,
23 seconds. `{seconds:00}` always shows two digits, so 5 seconds is `1:05`.

**Progress** is rounded down (`Mathf.FloorToInt`) so the screen never says 100% for a level that wasn't
finished. It's added with `+=` only when `showProgress` is on; its `\n` is at the front so there's no empty
line when it's left off.

**Columns:** each line is `label<pos=70%>value`. `<pos=70%>` is a TextMesh Pro rich text tag that moves the
drawing point to 70% of the box's width, so every value starts at the same spot and they line up. The tag
is kept in one local variable, `col`, so moving the column is a one-number change. If a label is ever
longer than 70% of the box, its value is drawn over the end of the label; raise the percentage or shorten
the label.

### `Restart` (Game Over)

First it sets `GameManager.SkipMenuOnce` (a static flag, so it survives the reload), so a restarted Level 1
goes straight into play instead of showing the menu. Then it reloads the current scene by its build index,
so it still works if the scene is renamed. GameManager's `Awake` reads and clears the flag, unfreezes time
and starts the game; PlayerHealth goes back to full lives, RunStats loads the next Apollo (with totals reset
to 0), and PlayerMovement locks the cursor again. On Level 2 and 3 the flag changes nothing (they start
straight away anyway).

### `NextLevel` (Win)

Sets `Time.timeScale` back to 1, then loads the scene **after** the current one in Build Settings
(build index + 1). If there isn't one (`next < SceneManager.sceneCountInBuildSettings` is false), it loads
the menu instead. The build order is Level_1 = 0, Level_2 = 1, Level_3 = 2, so Level 3's Next Level goes back
to the menu (Level 1). A new level added after Level_3 is picked up with no code change.

### `Menu`

Sets `Time.timeScale` back to 1, then loads `menuSceneName` (`Level_1`, where the menu lives). It doesn't set
`SkipMenuOnce`, so Level 1 opens on the menu. GameManager's `Awake` also resets the time scale; setting it
here as well keeps Menu working if `menuSceneName` ever points at a scene without a GameManager.

### `Quit`

Closes the game. In the Unity Editor, `Application.Quit()` does nothing, so inside `#if UNITY_EDITOR` it
stops Play mode instead (`EditorApplication.isPlaying = false`). The `#else` part is what a built game uses.

## Talks to

- **GameManager**: calls `ShowGameOver()` and `ShowWin()`. FillStats reads `RunStats` and `LevelTimer`
  through it. Restart sets the static `SkipMenuOnce`.
- **RunStats**: reads `ApolloName` and every stat.
- **LevelTimer**: reads `Progress`.
- **MenuNavigator**: one per panel (Game Over, Win and the stats overlay) selects and presses these buttons, and
  keeps the cursor hidden. No direct link: it just presses whichever `Button` is selected.
- **SceneManager** (Unity): loads scenes for Restart, Next Level and Menu.

## Gotchas

- **Fill in both groups.** `Awake` switches off both panels without a null check, so an empty `panel` in
  either group throws an error when the scene starts.
- **Duplicated buttons keep their old On Click targets.** Every button should point at the EndScreen
  canvas, and Next Level must call `NextLevel`, not `Restart`.
- **Build order matters.** Levels must be in play order, starting with Level_1. Next Level just loads the
  next index.
- **Every level's `menuSceneName` must be `Level_1`,** including `LevelTemplate`. A new `EndScreen` defaults to
  it, but a scene keeps whatever value it saved.
- **Keep the EndScreen object switched on.** The panels are its children, so if the `EndScreen` object is
  off, `ShowWin` / `ShowGameOver` switch a panel on that can't be seen. `Awake` already hides both panels at
  load, so there's no need to switch the parent off while editing; switch off the panels instead.
- **Stats Text must stay left-aligned with wrapping off,** or the `<pos>` columns stop lining up.
- **UI buttons need an EventSystem using the Input System UI module.** The scene has one; a new scene
  would need its own.
- **Long titles need a smaller font.** "MISSION ACCOMPLISHED" doesn't fit the 800-wide layout at the Game
  Over title's size.
