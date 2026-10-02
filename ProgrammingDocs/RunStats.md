# RunStats

## Purpose

The hub for every stat tracked during a run. Each stat is a property with a public getter and a private
setter, so other scripts (and later the UI) can read them, but only RunStats changes them. Other scripts
call its methods to add to individual stats.

It also keeps the hamster's name: "Apollo" plus an attempt number. The attempt number is saved in
`PlayerPrefs`, so it lasts across the whole game save, and it goes up by one each time the player dies.

## Where it lives

- **Attached to:** the `Game Manager` GameObject, alongside GameManager and LevelTimer. GameManager finds it
  there automatically.
- **Requires:** `GameManager.Instance`, for `HasWon` and the player (`Player`, for `InOverdrive`). Without
  one, the timers don't run.

## Inspector settings

| Field | Default | What it changes |
| --- | --- | --- |
| `hamsterName` | Apollo | The base name. The attempt number is added after it ("Apollo 3") |

## How it works

### The stats

| Property | Type | Saved to disk? | What it counts |
| --- | --- | --- | --- |
| `Attempt` | `int` | **yes**, in `PlayerPrefs` | Which Apollo this run is: deaths so far + 1 |
| `ApolloName` | `string` | — | `hamsterName` + `Attempt`, e.g. "Apollo 3". Built each time it's read |
| `AsteroidsDestroyed` | `int` | no | Asteroids smashed this run |
| `PowerUpsPickedUp` | `int` | no | Power-ups picked up this run, including ones wasted at full health |
| `OverdriveTime` | `float` | no | Seconds spent in the top 10% of speed this run |
| `TimeSurvived` | `float` | no | Seconds from the start of the run until death or a win |

Only `Attempt` is saved. The other stats are instance properties held in memory, so they start at 0 every
time the scene loads. Saving each Apollo's full record is planned for a later "top 5 Apollos" screen.

### `Awake`: loading the Apollo number

`Attempt = PlayerPrefs.GetInt(AttemptKey, 1)`. It reads the number saved under the key `"Attempt"`. The
second argument, 1, is the **default**: what `GetInt` returns when nothing has been saved yet, so a brand
new player is Apollo 1. Nothing is written here; it only reads.

The key is a `const` (`AttemptKey`), so loading and saving can't use two different spellings. A typo in
one of them would silently read and write different entries, and every hamster would be Apollo 1.

| Run | Loaded | Shown on death | Saved on death |
| --- | --- | --- | --- |
| first ever | 1 (nothing saved) | Apollo 1 | 2 |
| second | 2 | Apollo 2 | 3 |
| quit mid-run | 3 | — | nothing, so the next launch is still Apollo 3 |

### `Update`: the running totals

Each frame it adds `Time.deltaTime` (this frame's length) to `TimeSurvived`, and also to `OverdriveTime`
when the player's `InOverdrive` is true. These are running totals: spending 10 seconds in overdrive,
slowing down for 5, then 3 more in overdrive gives 13 seconds of overdrive and 18 survived. Checking only
at the moment of death would lose all of that.

It stops counting in two ways:

- **On a win:** `Update` returns early once GameManager's `HasWon` is set.
- **On a death:** GameManager freezes time (`Time.timeScale = 0`), so `Time.deltaTime` is 0 and both
  totals stop on their own. No check is needed here.

### `AddAsteroidDestroyed` and `AddPowerUpPickedUp`

Each adds one to its count (`++`). AsteroidBehavior calls the first when an asteroid is smashed, and
PowerUp calls the second on every pickup.

They're methods instead of public setters so that the counts can only ever go **up by one**. Any script can
call them, but no script can set a count to 0 or 9999. It also gives one place to add more behavior later.

### `RecordDeath`

Called by `GameManager.GameOver()`, once, when the player dies:

1. Saves `Attempt + 1` under `AttemptKey`, so the next run is the next Apollo. `Attempt` itself doesn't
   change, so this run's screen still shows the Apollo who died.
2. `PlayerPrefs.Save()` writes to disk straight away. Otherwise Unity only writes on a normal quit, and a
   crash would bring the same Apollo back.
3. A **temporary** `Debug.Log` prints the run's stats, until the game-over screen shows them.

Calling it twice would be harmless today (it saves the same number both times, because it never changes
`Attempt`). That stops being true once it also saves full records, which is why it relies on
`GameOver()` only running once.

## Talks to

- **GameManager**: reads `HasWon` and `Player` every frame. GameManager calls `RecordDeath()` at game over,
  and keeps a `RunStats` reference that other scripts use to reach it.
- **PlayerMovement**: reads `InOverdrive` (through GameManager's `Player`).
- **AsteroidBehavior**: calls `AddAsteroidDestroyed()`.
- **PowerUp**: calls `AddPowerUpPickedUp()`.

## Gotchas

- **The run stats aren't saved.** Only the Apollo number survives a scene reload or quitting.
- **Testing raises the Apollo number for real.** Every death in Play mode saves. Use Edit → Clear All
  PlayerPrefs in Unity to start again from Apollo 1.
- **`PlayerPrefs` is per computer,** not part of the project, so each person has their own Apollo count.
- **Overdrive is defined in PlayerMovement.** If `InOverdrive`'s threshold changes, this stat changes with it.
- **The timers stop on a win but keep their values,** so `TimeSurvived` after a win is the time it took to
  win.
- **The temporary stats log** in `RecordDeath` should be removed once the game-over screen shows the stats.
