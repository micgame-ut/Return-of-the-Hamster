# UISFX

## Purpose

Plays a sound for each UI event: moving the menu selection, pressing a button, the credits, the end screens and the
score breakdown. Like `GameSFX`, it only listens. Each event is raised by the script that owns it, and `UISFX` turns
it into a call to `GameSFX.Play`, which plays the slot through `SFXManager`.

## Where it lives

- **Attached to:** the `AudioManager` in `Level_1`, next to `SFXManager`, `GameSFX` and `MusicManager`.
- **Requires:**
  - **`GameSFX`** in the project (its `Slot` class and `Play` method are reused).
  - **`SFXManager`** in the scene (it does the actual playing). Without one, nothing plays and nothing breaks.

## Inspector settings

Every field is a `GameSFX.Slot`: clips (one is picked at random, never the same twice in a row), volume (0 to 1) and a
random pitch range. An empty slot plays nothing.

| Slot | Plays when |
| --- | --- |
| `menuMove` | The selection moves to another button (`MenuNavigator.OnMoved`) |
| `buttonHover` | A button becomes highlighted: on a move, when a page opens, and when returning to a page below (`MenuNavigator.OnHighlighted`). On a move it plays together with `menuMove` |
| `menuSelect` | A button is pressed (`MenuNavigator.OnPressed`) |
| `buttonBlocked` | A press lands on a button that can't be pressed (`MenuNavigator.OnBlocked`) |
| `startPressed` | Start is pressed (`MainMenu.OnStartClicked`). Plays together with `menuSelect` |
| `creditsOpen` / `creditsClose` | The credits panel opens / hides (`CreditsPanel.OnOpened` / `OnClosed`) |
| `endScreenShown` | The Game Over or Win screen appears (`EndScreen.OnShown`) |
| `statsOpen` / `statsClose` | The stats overlay opens / closes (`EndScreen.OnStatsOpened` / `OnStatsClosed`) |
| `scoreRow` | Each score row counts into the total (`ScoreBreakdown.OnRowCounted`) |
| `scoreTotal` | The score breakdown finishes (`ScoreBreakdown.OnFinished`) |
| `scorePopup` | A score popup appears (`Score.OnPointsAdded`). Happens often, so keep it quiet or empty |

## How it works

### Awake / OnDestroy

A reloaded `Level_1` brings a second `AudioManager`. The first `UISFX` stays as `instance`, and a later copy calls
`Destroy(this)`, which removes only that component. `OnEnable` and `OnDisable` return early for the extra copy, so
no sound ever plays twice.

### OnEnable / OnDisable

Subscribes to the static events of `MenuNavigator`, `MainMenu`, `CreditsPanel`, `EndScreen` and `ScoreBreakdown`
(they are `static`, so one subscription covers every scene), then calls `Bind`. `OnDisable` undoes all of it.

### Bind / Unbind

`Score` is made again with every level, so its `OnPointsAdded` event is subscribed in `Bind`, which runs on enable
and on every `sceneLoaded`. It unbinds first, so calling it twice can't subscribe twice. The menu scene has no
`GameManager`, so `score` stays `null` there.

### Handlers

Each handler is one line: `GameSFX.Play(slot)`. `HandlePointsAdded` ignores the event's four values, because only the
fact that a popup appeared matters.

## Talks to

- **MenuNavigator**: `OnMoved`, `OnHighlighted`, `OnPressed`, `OnBlocked`.
- **MainMenu**: `OnStartClicked`.
- **CreditsPanel**: `OnOpened`, `OnClosed`.
- **EndScreen**: `OnShown`, `OnStatsOpened`, `OnStatsClosed`.
- **ScoreBreakdown**: `OnRowCounted`, `OnFinished`.
- **Score** (through `GameManager.Instance.Score`): `OnPointsAdded`.
- **GameSFX**: its `Slot` class and `Play` method.

## Gotchas

- **A move plays `buttonHover` and `menuMove` together**, because every highlight goes through `Select`. Leave one
  empty if you only want one sound.
- **Start plays two slots.** The Start button also raises `MenuNavigator.OnPressed`, so `menuSelect` and
  `startPressed` both play. Leave one empty if you only want one sound.
- **A press on the credits panel's own buttons** is covered by `menuSelect` too, because the top page raises the event.
- **`scoreTotal` plays after the last row**, also when the player skips the breakdown. It doesn't play if there is
  no `Score` (the breakdown exits early).
- **Only the first `UISFX` stays.** A second one on the scene is removed, so fill the slots on the `AudioManager`
  that comes first.
- **`ButtonSFX` and `SFXManager`'s ID list are gone.** All UI sounds come from `UISFX`.
