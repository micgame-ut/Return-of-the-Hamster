# TutorialManager

## Purpose

Runs the optional tutorial that sits between Level 1's menu and the start of the level. It shows one prompt at a
time, waits for the player to do the thing it asks for, moves on, and at the end hands over to the real level by
calling `GameManager.StartGame()`. It never starts by itself: it waits for `GameManager.OnTutorialStarted`, which
only happens if the player picks "Tutorial" in the popup.

## Where it lives

- **Attached to:** the `Tutorial` object in `Level_1`. That object is also a Screen Space Overlay canvas
  (Scale With Screen Size, 1280 × 1024), and its child `Prompt Text` shows the prompts.
- **Requires:**
  - **`GameManager`** in the scene: it gives the player, and raises `OnTutorialStarted`.
  - **`OrbitSettings`**: where the spawned objects appear.
  - **The popup:** `MainMenu`'s Tutorial Popup, which starts the tutorial (see "The flow").

## Inspector settings

| Field | Default | What it changes |
| --- | --- | --- |
| `stages` | 7 stages | The stages, run in order. Each has a `prompt`, a `goal`, an `amount` and an optional `spawnPrefab` |
| `promptText` | `Prompt Text` | The text the prompts are shown in |
| `stageDoneDelay` | 1 | Seconds after a stage is done before the next prompt appears |
| `respawnDelay` | 1.5 | Seconds before an object is spawned again when the player hit it, missed it or let it pass |
| `spawnLaneSpread` | 15 | How far either side of the player's current angle a spawned object may appear, in degrees. 0 = straight at the player |
| `finishPrompt` | HERE WE GO! | The text shown when the last stage is done |
| `finishDelay` | 2 | Seconds the finish text stays up before the level starts |

**The stages in the scene**

| # | Goal | `amount` | `spawnPrefab` | Done when |
| --- | --- | --- | --- | --- |
| 1 | Orbit | 180 | none | the player has travelled 180° round the orbit |
| 2 | Speed | 0.6 | none | `Speed01` reaches 0.6 |
| 3 | SideBoost | 2 | none | two sideways boosts |
| 4 | ForwardBoost | 2 | none | two forward boosts |
| 5 | Avoid | (1, unused) | `Linear` | the hazard flies past without hitting |
| 6 | Smash | (1, unused) | `Linear-Destructible` | the player smashes it |
| 7 | Collect | (1, unused) | `Health PowerUp` | the player collects it |

What `amount` means depends on the goal: degrees for Orbit, a fraction of top speed for Speed, a count for the two
boosts, and nothing for Avoid, Smash and Collect (each is done after one).

## The flow

1. The player presses Start on the main menu. `MainMenu` shows the popup instead of starting the glide.
2. **Skip** calls `MenuTransition.Play(false)`, which glides and then calls `StartGame()`, so the level starts as it
   always did. **Tutorial** calls `Play(true)`, which glides and then calls `GameManager.StartTutorial()`.
3. `StartTutorial()` sets `InTutorial` and raises `OnTutorialStarted`. The player can now fly (input, starfield,
   flame and orbit ring read `IsFlying`), but the level timer, spawner, score, HUD and the level-start sound wait.
4. This script's `HandleTutorialStarted` starts `RunTutorial`.
5. After the last stage, the finish text is shown and `StartGame()` is called, so the real level starts at the
   player's current speed. Level music has been playing since Start was pressed.

## How it works

### `Awake`, `OnEnable` and `OnDisable`

`Awake` finds `GameManager` and the player, and hides the prompt text. `OnEnable` subscribes to
`GameManager.OnTutorialStarted`, the player's `OnSideBoost` and `OnSpeedBoost`, `AsteroidBehavior.OnSmashed` and
`PowerUp.OnCollected`. `OnDisable` undoes all of it.

### The four handlers

`HandleSideBoost`, `HandleForwardBoost`, `HandleSmashed` and `HandleCollected` each add 1 to `progress`, but only
while a stage is running (`stageRunning`) and its goal is the matching one (`currentGoal`). A boost during the
Smash stage, or a smash after the tutorial, is ignored.

### `RunTutorial`

Goes through `stages`. For each one it waits for `RunStage` to finish (a **nested coroutine**), then waits
`stageDoneDelay`. After the last stage it shows `finishPrompt`, waits `finishDelay`, destroys any leftover spawned
object, hides the prompt and calls `GameManager.StartGame()`.

### `RunStage`

Shows the stage's prompt and sets `currentGoal`, `progress = 0` and `stageRunning = true`. If the stage has a
`spawnPrefab` it spawns one. Then it loops once a frame until `IsGoalMet`:

- **Orbit:** adds the shortest signed turn since last frame (`Mathf.DeltaAngle`, in degrees) to `progress`, so
  crossing from 359° to 1° counts as 2°, not 358°.
- **Hit tracking:** `hit` becomes true if the player is invulnerable (`PlayerMovement.IsInvulnerable`, which a
  hit sets). Only the Avoid stage reads it.
- **When the spawned object is gone** (it flew past, or it was smashed or collected): in the Avoid stage with no
  hit, the stage is done (`progress = 1`). Otherwise, if the goal isn't met, it waits until the player is no longer
  invulnerable (so the old hit can't count against the next attempt), waits `respawnDelay`, clears `hit` and spawns
  a new one.

### `IsGoalMet`

A `switch` on the goal. Speed compares `PlayerMovement.Speed01` to `amount`. Avoid, Smash and Collect are done at
`progress >= 1`. The rest (Orbit, SideBoost, ForwardBoost) are done at `progress >= amount`.

### `ShowPrompt`

Sets the text and switches the text object on, or off if the text is empty.

### `Spawn`

Works out an angle: the player's current `OrbitAngle` plus a random amount within `spawnLaneSpread` degrees
(converted to radians). The position is on the orbit ring at that angle, at the spawner's depth
(`OrbitSettings.SpawnerPosition.z`). The prefab is made there with `Instantiate`. The asteroid or power-up
then flies toward the camera on its own, and destroys itself when it's despawned.

## Talks to

- **GameManager**: reads `Player`, subscribes to `OnTutorialStarted` and calls `StartGame()`.
- **PlayerMovement**: reads `OrbitAngle`, `IsInvulnerable` and the static `Speed01`; subscribes to `OnSideBoost`
  and `OnSpeedBoost`.
- **AsteroidBehavior**: subscribes to the static `OnSmashed`. The spawned prefabs are asteroids.
- **PowerUp**: subscribes to the static `OnCollected`. One spawned prefab is a power-up.
- **OrbitSettings**: reads the orbit centre and radius, and the spawner's depth.
- **MainMenu / MenuTransition**: start it (through `GameManager.StartTutorial()`).
- **Score, RunStats, PlayerHealth**: ignore points, counts and hits while `InTutorial` is on.

## Gotchas

- **Nothing runs without the popup.** The tutorial only starts from `OnTutorialStarted`. If `MainMenu`'s Tutorial
  Popup field is empty, Start goes straight to the level and this script never runs.
- **A tutorial hit costs nothing, but still pushes the player.** The knockback and speed loss still happen. Only the
  life and the game over are skipped.
- **Close calls still raise `OnCloseCall` during the Avoid stage,** so `GameSFX` may play its close-call sound or
  dialogue. No points are given.
- **The Smash stage needs speed.** An asteroid only breaks at `destroySpeed` (5, so `Speed01` 0.5). A slower hit
  doesn't break it, so the stage waits and respawns it.
- **The Avoid stage counts a hit from `IsInvulnerable`.** Anything else that makes the player invulnerable during
  that stage also reads as a hit, and the hazard respawns.
- **Prompt width.** The prompt font is wide. Keep each line to about 30 characters, and use `\n` for a new line.
- **`stages` can't be empty:** with no stages it shows the finish text and starts the level straight away.
