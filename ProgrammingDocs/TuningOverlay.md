# TuningOverlay

## Purpose

A playtest tool. Press **Tab** while flying and a window opens with sliders for the movement values (steering,
speed, boost, hits, and how fast the world comes at the player). The game pauses while it's open. Changes apply live,
and are saved to a JSON file next to the game. That file is loaded again at the start of every level, so playtesters'
tuning carries over, and the values in it can be copied into the project (or the file dropped into a build) to set the
final numbers.

## Where it lives

- **Attached to:** nothing in a scene. It makes its own GameObject once, as the game starts
  (`[RuntimeInitializeOnLoadMethod]`), and keeps it between scenes (`DontDestroyOnLoad`). No scene or prefab needs it.
- **Requires:** in a level, `GameManager` with a `Player` and a `LevelTimer`; the `SpawnableTemplate` asset at
  `Resources/SpawnableTemplate`; the `OrbitSettings` asset. In the menu or any scene without them, it does nothing.
- **Switch:** the constant `Enabled` at the top. Set it to `false` to leave the overlay out of a build.

## Settings

There are no Inspector fields. The two constants are `Enabled` and `FileName` (`MovementTuning.json`).

### The rows

| Section | Value | Where it lives | Range |
| --- | --- | --- | --- |
| Steering | `mouseSensitivity` | PlayerMovement | 0.5 – 20 |
| | `forwardThreshold` | PlayerMovement | 0 – 90 |
| | `minInputForAngle` | PlayerMovement | 0 – 10 |
| Speed | `acceleration` | PlayerMovement | 1 – 60 |
| | `speedDecay` | PlayerMovement | 0 – 60 |
| | `maxSpeed` | PlayerMovement | 20 – 200 |
| | `overdriveFraction` | PlayerMovement | 0.5 – 1 |
| | `topSpeedDegrade` | PlayerMovement | 0 – 40 |
| | `minSpeed` (this level) | LevelConfig, applied through PlayerMovement | 0 – 100 |
| Boost | `boostForce` | PlayerMovement | 0 – 40 |
| | `boostDecay` | PlayerMovement | 0.5 – 10 |
| | `speedBoost` | PlayerMovement | 0 – 100 |
| | `boostCooldown` | PlayerMovement | 0 – 10 |
| | `sideInputMemory` | PlayerMovement | 0 – 0.5 |
| Hit | `knockbackForce` | SpawnableTemplate | 0 – 20 |
| | `speedLoss` | SpawnableTemplate | 0 – 100 |
| | `invulnerableTime` | PlayerMovement | 0 – 5 |
| World | `baseApproachSpeed` | SpawnableTemplate | 5 – 60 |
| | `speedMultiplier` | SpawnableTemplate | 0 – 1 |
| | `speedTimeFactor` | LevelTimer | 0 – 0.05 |
| | `orbitRadius` | OrbitSettings | 1.5 – 6 |

Each row is a label (hover it for a tooltip, shown at the bottom of the window), a slider and a number box.

## How it works

### The file (`MovementTuning.json`)

Next to the exe in a build, and in the project root in the Editor (`Application.dataPath`'s parent). It is a
`SaveData`: `player` (a `PlayerMovement.Tuning`), `world` (`knockbackForce`, `speedLoss`, `baseApproachSpeed`,
`speedMultiplier`, `speedTimeFactor`, `orbitRadius`) and `minSpeeds`, a list of `{ level, minSpeed }` entries named
after each scene. A value missing from the file keeps the game's own value, because loading uses
`JsonUtility.FromJsonOverwrite`. A file that can't be read gives a warning in the log and the game carries on.

### `current` and `defaults`

Two `SaveData` copies. `defaults` is what the game started with (read before the file); `current` is what the sliders
edit and what is saved. They must be separate objects (a class is a reference type), so `Clone` copies one by writing
it to JSON and reading it back. Reset replaces `current` with a clone of `defaults`.

### `BindLevel` (every level load)

Runs in `Start` for the first scene and from `SceneManager.sceneLoaded` for later ones. If there's no player or timer,
it clears `player` and stops (menus). Otherwise it:

1. finds the player and timer (through GameManager), the template and the orbit asset, and the scene's name;
2. `CaptureDefaults`: reads the player's `Tuning`, the timer's `SpeedTimeFactor` and this level's `minSpeed`. The
   template and orbit values are read only the first time (`assetsCaptured`), because assets keep their changes
   between levels and a later read would pick up the tuned values;
3. sets `current` to a clone of `defaults`, then loads the file over it;
4. adds this level's `minSpeed` to `current` if the file doesn't have it;
5. `Apply`: pushes `current` into the game.

### `Apply`

`player.SetTuning`, `timer.SpeedTimeFactor`, the four `SpawnableTemplate` values, `orbit.OrbitRadius`, and
`player.SetMinSpeed` for this level. Called after loading, and whenever a row changes.

### Tab, pause and cursor

`Update` reads `Keyboard.current.tabKey` directly (a developer tool, so it isn't an action in the input asset). It only
works with a bound level, and only while `CanOpen`: the game is flying and isn't over or won.

- **Open:** `IsOpen = true`, `Time.timeScale = 0`, cursor free and visible.
- **Close:** `IsOpen = false`, `Time.timeScale = 1`, cursor locked and hidden, and `Save`.

`PlayerMovement.InputLocked` includes `IsOpen`, so the ship ignores the mouse behind the window.

### The window (`OnGUI`)

Immediate-mode GUI, scaled by screen height from a 1080p layout. A scroll view holds three methods' rows
(`DrawSteeringAndSpeed`, `DrawBoostAndHit`, `DrawWorld`). Each `Row` draws a label with tooltip, a slider and a number
box, and returns whether the value changed; `Apply` runs once if any did. The number box keeps its own text while it
has focus (the `typed` dictionary) so a decimal point can be typed. Buttons: **Reset to defaults**, **Save**, **Copy to
clipboard** (the same JSON as the file, on the clipboard).

### `OnApplicationQuit`

Puts the original `SpawnableTemplate` values and orbit radius back. In the Editor, changes to an asset made in Play mode
stay after you stop, so this keeps the project's real values safe. Scene objects (player, timer) are thrown away anyway.

## Talks to

- **PlayerMovement**: `Tuning`, `GetTuning`, `SetTuning`, `SetMinSpeed`; PlayerMovement reads `IsOpen`.
- **GameManager**: `Player`, `LevelTimer`, `LevelConfig`, and the `IsFlying`, `IsGameOver` and `HasWon` flags.
- **LevelTimer**: `SpeedTimeFactor`.
- **OrbitSettings**: `OrbitRadius` (read and set).
- **SpawnableTemplate**: four fields, read and set.

## Gotchas

- **Asteroids already on screen** change approach speed at once (they read the template each frame), but the timing of a
  wave that has already started was worked out at the old speed, so spacing can look off for a few seconds. New waves are fine.
- **`orbitRadius`** moves the player's ring straight away, but patterns already spawned keep their size.
- **Reset** replaces `current` with `defaults`, which only knows the levels seen this session, so saving afterwards writes only those
  levels' `minSpeed`. Other levels fall back to their own values, which is what a reset means.
- **A typed value outside a slider's range** may be pulled back to the range by the slider.
- **The file is per machine.** To set the final numbers, copy its values into the Inspector fields and assets, or ship the file
  with the build. Add the file to `.gitignore` (done) so playtest tweaks don't land in commits.
- **`minSpeed` writes nothing to the `LevelConfig` assets.** It only calls `SetMinSpeed`, so those stay as they are.
- **Turning it off:** set `Enabled` to `false`. `PlayerMovement` still compiles against `TuningOverlay.IsOpen`, which stays `false`.
