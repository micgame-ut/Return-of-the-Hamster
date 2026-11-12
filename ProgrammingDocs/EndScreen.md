# EndScreen

## Purpose

The end-of-level screens. One component handles both:

- **Game Over**, shown after the player loses all their lives and the death sequence has played out:
  "GAME OVER", the Apollo's name, the score breakdown, and Restart / Menu / Scores / Quit.
- **Win**, shown when the level is cleared: "MISSION ACCOMPLISHED", the Apollo's name, the score breakdown, and
  Menu / Next Level / Scores / Quit.

When the score breakdown finishes, it also runs the leaderboard flow: if the run's score makes the top 10, the
three-letter **name page** opens (see NameEntry), and once a name is submitted the score is saved and the
**leaderboard window** opens with the new row highlighted (see LeaderboardPanel and LeaderboardStore). The
Scores buttons open the same window with no highlight.

The name also tells you how many deaths there have been: on the Game Over screen, "Apollo 4" is the fourth death.

It was called `GameOverScreen` until the win screen was added. The stats overlay (asteroids destroyed, power-ups,
time, progress) was replaced by the leaderboard window.

## Where it lives

- **Attached to:** the `EndScreen` canvas in `Level_1`. The canvas is always active; each screen is a
  panel underneath it (`Game Over Panel`, `Win Panel`), which this script switches on. It can't go on a
  panel: a switched-off object's scripts don't run, so it could never switch itself back on. The name page
  (`Name Entry`) and the leaderboard window (`Leaderboard Overlay`) are children too.
- **Shown by:** GameManager, through its `endScreen` slot. `DeathSequenceFinished()` calls
  `ShowGameOver()`, and `Win()` calls `ShowWin()`.
- **Requires:**
  - Both groups (Game Over and Win) filled in, in the Inspector, plus `nameEntry` and `leaderboard`.
  - `GameManager.Instance` with RunStats (for the Apollo name) and Score (for the leaderboard score). Without
    RunStats, the name texts are left as they are. Without Score, no name page opens.
  - Every button's **On Click ()** pointing at this component on the EndScreen canvas, calling `Restart`,
    `NextLevel`, `Menu`, `Quit`, `ShowLeaderboardPlain` (the Scores buttons) or `CloseLeaderboard` (the window's Close).
  - Build Settings with `Intro` at index 0, then the levels in play order (`Level_1` at 1, where the menu lives).

### Layout (set up in the Unity Editor)

Each panel has the same layout:

- **Vertical Layout** (800 × 640, spacing 30, Control Child Size on) stacks Title, Hamster Name Text, the
  score total and the score rows.
- **Horizontal Layout** (1000 × 80, spacing 60) holds the buttons, left to right in their Hierarchy
  order.
- Buttons are sized, not scaled. A scale above 1 makes them overlap, because layout groups ignore scale.

## Inspector settings

| Field | Default | What it changes |
| --- | --- | --- |
| `gameOver` | — | The Game Over screen's parts (see below) |
| `win` | — | The Win screen's parts (see below) |
| `nameEntry` | Name Entry | The name page shown after a run that made the leaderboard |
| `leaderboard` | Leaderboard Overlay | The leaderboard window opened by the Scores buttons, and after a name is submitted |
| `scoreHud` | the score HUD | The score HUD object, hidden when an end screen is shown. Optional |
| `menuSceneName` | Level_1 | The scene the Menu button loads, and Next Level's fallback. The menu lives in Level 1. Must match the scene file's name and be in Build Settings |

Each group (`ScreenParts`) has:

| Field | What it is |
| --- | --- |
| `panel` | The panel switched on to show this screen |
| `hamsterNameText` | Text that gets the Apollo name |
| `scoreTotalText` | The text that shows this screen's score total |
| `scoreRowContainer` | The parent the score breakdown rows are created under |

The titles are typed straight into their texts in the Inspector; the script doesn't touch them.

## How it works

### `ScreenParts` (`[System.Serializable]`)

Both screens need the same settings, so they're bundled into a small class inside EndScreen and used
twice. `[System.Serializable]` lets Unity save a plain class's fields and show them in the Inspector, as a
collapsible group. ObjectSpawner's `Wave` uses the same idea. The class is `private` because only
EndScreen uses it.

### `Awake`

Switches both panels, the name page and the leaderboard window off. They should already be off in the scene;
this makes sure none covers the game from the start if someone leaves one on by accident. The name page and the
leaderboard are skipped if their slot is empty.

### `OnEnable` and `OnDisable`

Subscribe to the static `ScoreBreakdown.OnFinished` and, if there is a name page, to its `OnSubmitted` event.
`OnDisable` undoes both.

### `ShowGameOver`, `ShowWin` and `Show`

`ShowGameOver()` and `ShowWin()` are one-line methods (`=> Show(gameOver)`, `=> Show(win)`), so the name
says which screen you want. Both use one private `Show(parts)`:

1. **`FillName(parts)`** first, so the panel never shows a frame of placeholder text. It writes the Apollo name.
2. Switches that screen's panel on, raises `OnShown`, hides the score HUD and starts the score breakdown.
3. **Doesn't touch the cursor.** The game has no mouse pointer: the buttons are used with the trackball and the
   one button through each panel's `MenuNavigator`, which keeps the cursor hidden and locked.

Time is frozen on both screens (`Time.timeScale = 0`). `MenuNavigator` runs on real input, so it still works.

### `HandleBreakdownFinished`

Called when `ScoreBreakdown.OnFinished` fires (also when the player skips the count). It reads the run's final
score, `Mathf.FloorToInt(Score.Total)`, into `finalScore`. If `LeaderboardStore.Qualifies(finalScore)`, it
switches the name page on. A run that doesn't qualify just stays on the end buttons. It does nothing if there
is no Score or no name page.

### `HandleNameSubmitted`

Called with the finished name. It saves the entry with `LeaderboardStore.Submit(name, finalScore)`, which
returns the place, and opens the leaderboard window with that place highlighted.

### `ShowLeaderboard`, `ShowLeaderboardPlain` and `CloseLeaderboard`

- **`ShowLeaderboard(int highlightRank = -1)`** opens the window (`LeaderboardPanel.Show`) and raises
  `OnLeaderboardOpened`. `highlightRank = -1` is an optional parameter: with no argument there is no highlight.
- **`ShowLeaderboardPlain()`** is `ShowLeaderboard()` with no argument. The Scores buttons call this one, because
  a button's On Click event can't pass the place.
- **`CloseLeaderboard()`** closes the window and raises `OnLeaderboardClosed`.

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
the menu instead. The build order is Intro = 0, Level_1 = 1, Level_2 = 2, Level_3 = 3, so Level 3's Next Level
goes back to the menu (Level 1). A new level added after Level_3 is picked up with no code change.

### `Menu`

Sets `Time.timeScale` back to 1, then loads `menuSceneName` (`Level_1`, where the menu lives). It doesn't set
`SkipMenuOnce`, so Level 1 opens on the menu. GameManager's `Awake` also resets the time scale; setting it
here as well keeps Menu working if `menuSceneName` ever points at a scene without a GameManager.

### `Quit`

Raises `OnQuitClicked` (which `UISFX` turns into the quit sound), then starts the `QuitAfterSound` coroutine, which
waits `UISFX.QuitSoundLength` seconds and closes the game. The wait uses `WaitForSecondsRealtime`, because time is
frozen (`Time.timeScale = 0`) on the end screens and a normal wait would never finish. The `quitting` flag makes a
second press during the wait do nothing, so the sound plays once. With no quit sound the length is 0, so it quits
after one frame.

In the Unity Editor, `Application.Quit()` does nothing, so inside `#if UNITY_EDITOR` it stops Play mode instead
(`EditorApplication.isPlaying = false`). The `#else` part is what a built game uses.

## Talks to

- **GameManager**: calls `ShowGameOver()` and `ShowWin()`. `HandleBreakdownFinished` reads `Score` through it, and
  `FillName` reads `RunStats`. Restart sets the static `SkipMenuOnce`.
- **RunStats**: reads `ApolloName`.
- **Score**: reads `Total`.
- **ScoreBreakdown**: subscribes to its static `OnFinished`, and calls `Play` from `Show`.
- **NameEntry**: switches the page on, and subscribes to its `OnSubmitted(string)`.
- **LeaderboardStore**: `Qualifies` and `Submit`.
- **LeaderboardPanel**: `Show` and `Close`.
- **MenuNavigator**: one per panel (Game Over, Win, the leaderboard window and the name page) selects and presses
  these buttons, and keeps the cursor hidden. No direct link: it just presses whichever `Button` is selected.
- **UISFX**: subscribes to the static events `OnShown` (raised in `Show`, after the panel is switched on),
  `OnLeaderboardOpened`, `OnLeaderboardClosed` and `OnQuitClicked`. `Quit` reads its static `QuitSoundLength`.
- **SceneManager** (Unity): loads scenes for Restart, Next Level and Menu.

## Gotchas

- **Fill in both groups.** `Awake` switches off both panels without a null check, so an empty `panel` in
  either group throws an error when the scene starts.
- **Duplicated buttons keep their old On Click targets.** Every button should point at the EndScreen
  canvas, and Next Level must call `NextLevel`, not `Restart`.
- **Renaming a public method breaks the buttons that call it.** The scene stores the method's name as text, so
  after the stats overlay became the leaderboard, the Scores and Close buttons had to be rewired by hand.
- **Leaving before the name page skips it.** A player who presses Menu or Restart before the score count
  finishes is never asked for a name, and the score isn't saved.
- **Build order matters.** `Intro` comes first, then the levels in play order, starting with Level_1. Next Level
  just loads the next index.
- **Every level's `menuSceneName` must be `Level_1`,** including `LevelTemplate`. A new `EndScreen` defaults to
  it, but a scene keeps whatever value it saved.
- **Keep the EndScreen object switched on.** The panels are its children, so if the `EndScreen` object is
  off, `ShowWin` / `ShowGameOver` switch a panel on that can't be seen. `Awake` already hides both panels at
  load, so there's no need to switch the parent off while editing; switch off the panels instead.
- **UI buttons need an EventSystem using the Input System UI module.** The scene has one; a new scene
  would need its own.
- **Long titles need a smaller font.** "MISSION ACCOMPLISHED" doesn't fit the 800-wide layout at the Game
  Over title's size.
