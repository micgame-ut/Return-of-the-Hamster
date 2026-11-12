# UISFX

## Purpose

Plays a sound for each UI event: moving the menu selection, pressing a button, quitting, the credits, the end screens,
the score breakdown and the rising count sounds. Like `GameSFX`, it only listens. Each event is raised by the script that owns it, and `UISFX` turns
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
| `quitPressed` | Quit is pressed, in the menu or on an end screen (`MainMenu.OnQuitClicked`, `EndScreen.OnQuitClicked`). The game waits for the longest clip in the slot before it closes |
| `creditsOpen` / `creditsClose` | The credits panel opens / hides (`CreditsPanel.OnOpened` / `OnClosed`) |
| `endScreenShown` | The Game Over or Win screen appears (`EndScreen.OnShown`) |
| `leaderboardOpen` / `leaderboardClose` | The leaderboard window opens / closes (`EndScreen.OnLeaderboardOpened` / `OnLeaderboardClosed`). They were `statsOpen` / `statsClose`; `[FormerlySerializedAs]` keeps the assigned clips |
| `letterChange` | A name letter changes, or steps back a slot (`NameEntry.OnLetterChanged`) |
| `letterLock` | A name letter is locked in (`NameEntry.OnLetterLocked`) |
| `nameSubmitted` | The three-letter name is submitted (`NameEntry.OnNameSubmitted`). Plays together with `letterLock` |
| `scoreRow` | Each score row counts into the total (`ScoreBreakdown.OnRowCounted`) |
| `scoreTotal` | The score breakdown finishes (`ScoreBreakdown.OnFinished`) |
| `scorePopup` | A score popup appears (`Score.OnPointsAdded`). Happens often, so keep it quiet or empty |
| `overdriveCount` | Each tick of the overdrive tally (`OverdriveTally.OnTick`). Its pitch comes from the count, so the slot's own Pitch Range is ignored |
| `scoreCount` | Each tick while a score row counts into the total (`ScoreBreakdown.OnTick`). Its pitch comes from the count, so the slot's own Pitch Range is ignored |

The count sounds also use two plain fields:

| Field | Default | What it changes |
| --- | --- | --- |
| `minPitch` | 1 | The pitch of a count sound at the start of the count. 1 is normal, below 1 is lower |
| `maxPitch` | 2 | The pitch of a count sound at the end of the count. 2 is an octave above normal |

## How it works

### Awake / OnDestroy

A reloaded `Level_1` brings a second `AudioManager`. The first `UISFX` stays as `instance`, and a later copy calls
`Destroy(this)`, which removes only that component. `OnEnable` and `OnDisable` return early for the extra copy, so
no sound ever plays twice.

### OnEnable / OnDisable

Subscribes to the static events of `MenuNavigator`, `MainMenu`, `CreditsPanel`, `EndScreen`, `ScoreBreakdown` and
`OverdriveTally` (they are `static`, so one subscription covers every scene), then calls `Bind`. `OnDisable` undoes
all of it.

### `QuitSoundLength`

A `public static` property that returns the length in seconds of the longest clip in `quitPressed` (0 if there is no
`UISFX`, no slot or no clips). `MainMenu` and `EndScreen` wait that long before closing the game, so the sound isn't
cut off. It reads through `instance`, so callers don't need a reference to the `UISFX` object. The slot picks a clip
at random, so the longest one is the safe wait. The clip's pitch isn't factored in, so leave the slot's pitch at 1.

### Bind / Unbind

`Score` is made again with every level, so its `OnPointsAdded` event is subscribed in `Bind`, which runs on enable
and on every `sceneLoaded`. It unbinds first, so calling it twice can't subscribe twice. The menu scene has no
`GameManager`, so `score` stays `null` there.

### Handlers

Each handler is one line: `GameSFX.Play(slot)`. `HandlePointsAdded` ignores the event's four values, because only the
fact that a popup appeared matters. `HandleQuitClicked` serves both quit events.

The two tick handlers (`HandleOverdriveTick`, `HandleScoreTick`) get a progress value from 0 to 1 and play their slot
with `GameSFX.Play(slot, pitch)`, where `pitch = Mathf.Lerp(minPitch, maxPitch, progress)`. With 1 and 2, a progress of
0.5 plays at pitch 1.5. For the overdrive tally, progress is the tally divided by its `pitchFullAt`; for the score, it's
the running total divided by the final total, so the pitch keeps rising across all the rows.

## Talks to

- **MenuNavigator**: `OnMoved`, `OnHighlighted`, `OnPressed`, `OnBlocked`.
- **MainMenu**: `OnStartClicked`, `OnQuitClicked`. `MainMenu` reads `QuitSoundLength`.
- **CreditsPanel**: `OnOpened`, `OnClosed`.
- **EndScreen**: `OnShown`, `OnLeaderboardOpened`, `OnLeaderboardClosed`, `OnQuitClicked`. `EndScreen` reads `QuitSoundLength`.
- **NameEntry**: `OnLetterChanged`, `OnLetterLocked`, `OnNameSubmitted`.
- **ScoreBreakdown**: `OnRowCounted`, `OnFinished`, `OnTick(float)`.
- **OverdriveTally**: `OnTick(float)`.
- **Score** (through `GameManager.Instance.Score`): `OnPointsAdded`.
- **GameSFX**: its `Slot` class and both `Play` methods (`Play(slot)` and `Play(slot, pitch)`).

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
- **Quit also plays `menuSelect`** (the generic button press) in the menu, as Start does. Leave one empty if you only
  want the quit sound. The game stays open for the length of the longest `quitPressed` clip, so a long clip is a long wait.
- **Keep the count clips short.** The overdrive ticks arrive about 20 times a second, and each one takes a voice from
  `SFXManager`'s small pool. A clip of about 0.1 s or less is safe; a longer one can cut other sounds off.
- **The score ticks stop when the player skips the breakdown** (Boost), because the count-up loop ends.
- **`ButtonSFX` and `SFXManager`'s ID list are gone.** All UI sounds come from `UISFX`.
