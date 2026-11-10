# RunStats

## Purpose

The hub for every stat tracked for the current Apollo. Each stat is a property with a public getter and a
private setter, so other scripts (and the end screens) can read them, but only RunStats changes them.
Other scripts call its methods to add to individual stats.

The stats follow the **Apollo, not the level**: they're saved when a level is won, carried into the next
level, and reset when the Apollo dies. The hamster's name is "Apollo" plus an attempt number, which is also
saved and goes up by one each time the player dies.

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

| Property | Type | Saved under | What it counts |
| --- | --- | --- | --- |
| `Attempt` | `int` | `Attempt` | Which Apollo this is: deaths so far + 1 |
| `ApolloName` | `string` | — | `hamsterName` + `Attempt`, e.g. "Apollo 3". Built each time it's read |
| `AsteroidsDestroyed` | `int` | `AsteroidsDestroyed` | Asteroids smashed by this Apollo |
| `PowerUpsPickedUp` | `int` | `PowerUpsPickedUp` | Power-ups picked up, including ones wasted at full health |
| `OverdriveTime` | `float` | `OverdriveTime` | Seconds spent in overdrive (from PlayerMovement's `overdriveFraction` of max speed up, 0.9 by default) |
| `TimeSurvived` | `float` | `TimeSurvived` | Seconds survived, across all levels |
| `LevelsBeaten` | `int` | `LevelsBeaten` | Levels won. Not shown on any screen yet; kept for future Apollo records |

Everything is stored in `PlayerPrefs`, Unity's small key-value store saved on the computer. Each key is a
`const` (`AttemptKey`, `AsteroidsKey`, ...), so loading and saving can't use two different spellings. A
typo in one of them would silently read and write different entries.

### The life of the stats

| Moment | What happens |
| --- | --- |
| **Level starts** (`Awake`) | Loads the Apollo's saved totals. A new Apollo has nothing saved, so everything is 0 |
| **During the level** | `Update` and the `Add...` methods keep adding to those totals |
| **Win** (`RecordWin`) | `LevelsBeaten` + 1, then all totals are saved, so the next level carries on from them |
| **Death** (`RecordDeath`) | The next Apollo number is saved and every total is deleted, so the next Apollo starts at 0 |
| **Quit mid-level** | Nothing is saved for that level. The next launch is the same Apollo, with their totals as of the last level they won |

Worked example, Apollo 3:

| | Level 1 | Level 2 | Dies in level 3 |
| --- | --- | --- | --- |
| Asteroids smashed in that level | 10 | 7 | 4 |
| Saved total afterwards | 10 | 17 | deleted |
| Shown on the end screen | win: 10 | win: 17 | death: 21 |

### `Awake`: loading

Reads every saved value with `PlayerPrefs.GetInt` / `GetFloat`. The second argument is the **default**,
returned when nothing has been saved under that key: 1 for `Attempt` (so a brand-new player is Apollo 1)
and 0 for everything else. Nothing is written here.

### `Update`: the running totals

Each frame it adds `Time.deltaTime` to `TimeSurvived`, and also to `OverdriveTime` when the player's
`InOverdrive` is true. These are running totals: 10 seconds in overdrive, 5 slower, then 3 more in
overdrive gives 13 seconds of overdrive and 18 survived.

It doesn't count during Level 1's menu: `Update` returns early while GameManager's `HasStarted` is false.
These totals are saved between runs, so without this, every second on the menu would be added to the
Apollo's time survived.

It stops counting in two ways:

- **On a win:** `Update` returns early once GameManager's `HasWon` is set.
- **On a death:** GameManager freezes time (`Time.timeScale = 0`), so `Time.deltaTime` is 0 and both
  totals stop on their own. (A win freezes time too, so either way they stop.)

### `AddAsteroidDestroyed` and `AddPowerUpPickedUp`

Each adds one to its count (`++`), unless the tutorial is running (`GameManager.InTutorial`), so practice
smashes and pickups don't count. AsteroidBehavior calls the first when an asteroid is smashed, and
PowerUp calls the second on every pickup.

They're methods instead of public setters so that the counts can only ever go **up by one**. Any script can
call them, but no script can set a count to 0 or 9999.

### `RecordWin` and `SaveStats`

Called by `GameManager.Win()`. Adds one to `LevelsBeaten`, then `SaveStats()` writes all five totals and
calls `PlayerPrefs.Save()` to write to disk straight away. `SaveStats` is a private helper so all the saving
is in one place.

### `RecordDeath`

Called by `GameManager.GameOver()`, once, when the player dies:

1. Saves `Attempt + 1`, so the next run is the next Apollo. `Attempt` itself doesn't change, so the death
   screen still shows the Apollo who died.
2. Deletes every saved total (`PlayerPrefs.DeleteKey`). A deleted key gives back its default (0) on the next
   load, which is what a new Apollo should have.
3. `PlayerPrefs.Save()` writes to disk straight away. Otherwise Unity only writes on a normal quit, and a
   crash would bring the same Apollo (and totals) back.

It only touches what's saved, not the values in memory, so the death screen (shown after this) still shows
the totals the Apollo died with. This is also where saving each Apollo's full record would go, for the
planned "top 5 Apollos" screen.

## Talks to

- **GameManager**: reads `HasStarted`, `HasWon` and `Player` every frame. GameManager calls `RecordWin()` and
  `RecordDeath()`, and keeps a `RunStats` reference that other scripts use to reach it.
- **PlayerMovement**: reads `InOverdrive` (through GameManager's `Player`).
- **AsteroidBehavior**: calls `AddAsteroidDestroyed()`.
- **PowerUp**: calls `AddPowerUpPickedUp()`.
- **EndScreen**: reads `ApolloName` and every stat to fill in both end screens.

## Gotchas

- **Testing piles up for real.** Every win saves totals and every death raises the Apollo number. Use
  Edit → Clear All PlayerPrefs in Unity to start again from Apollo 1 with nothing saved.
- **Quitting mid-level loses that level's stats.** Only wins save.
- **`PlayerPrefs` is per computer,** not part of the project, so each person has their own Apollo and totals.
- **A new stat needs four things:** a key, a line in `Awake`, a line in `SaveStats`, and a `DeleteKey` in
  `RecordDeath`. Missing the last one carries it over to the next Apollo.
- **Overdrive is defined in PlayerMovement.** If `InOverdrive`'s threshold changes, this stat changes with it.
