# GameSFX

## Purpose

Plays a sound effect for each game event. Every event has a **slot** in the Inspector: drag clips onto it,
set a volume and a pitch range, and that's the sound. An empty slot plays nothing, so a sound can be left out
without touching any code.

It only listens. Each event is raised by the script that owns it, and `GameSFX` turns it into a call to
`SFXManager.PlayClip`.

It also plays one **looping** sound, the **low-life loop**, which fades in while the player has one life left (see below). It's the only
sound here that isn't a one-shot.

It also plays the hamster's **dialogue**. Some events have a second, separate **dialogue slot** that plays
*alongside* the normal sound, but only with a set **chance**, so the same line isn't said on every interaction.

## Where it lives

- **Attached to:** the `AudioManager` in `Level_1`, next to `SFXManager`, `MusicManager` and `MusicSpeedDriver`.
  That object survives into later levels (`MusicManager` calls `DontDestroyOnLoad`), so `GameSFX` does too.
- **Requires:**
  - **`SFXManager`** in the scene (it does the actual playing). Without one, nothing plays and nothing breaks.
  - **`MusicSpeedDriver`** on the same object, found with `GetComponent` in `Awake`. Without it only the two
    overdrive sounds are skipped.
  - **A `GameManager` in each level**, for the per-level events and the player's components.

## Inspector settings

Every slot is a `Slot`: **Clips** (a list, one picked at random each time and never the same one twice in a
row), **Volume** (0 to 1, default 1) and **Pitch Range** (X to Y, a random pitch each time, default 1 to 1 = no
variation). The sound sits on top of `SFXManager`'s own master volume.

| Group | Slot | Plays when |
| --- | --- | --- |
| Player | `hit` | the player loses a life (`PlayerHealth.OnLifeLost`) |
| | `lifeGained` | the life count goes up (`PlayerHealth.OnLivesChanged`) |
| | `sideBoost` | a boost around the orbit (`PlayerMovement.OnSideBoost`) |
| | `forwardBoost` | a forward boost (`PlayerMovement.OnSpeedBoost`) |
| | `boostReady` | the boost cooldown finishes (`PlayerMovement.OnBoostReady`) |
| Power-ups | `healthPickup`, `invinciblePickup`, `twinPickup`, `scorePickup` | that type of power-up is collected (`PowerUp.OnCollected`; a Random power-up plays the sound of the type it rolled) |
| | `invincibleStart`, `invincibleEnd` | invincibility starts / runs out (`PlayerInvincibility.OnInvincibilityChanged`) |
| | `twinStart`, `twinEnd` | the first copy appears / the last copy is gone (`PlayerTwins.OnTwinChanged`) |
| | `twinBodyLost` | a twin body is destroyed (`PlayerTwins.OnBodyLost`) |
| | `boostRechargePickup`, `jackpotPickup`, `slimPickup`, `bombPickup`, `lancePickup`, `warpPickup` | that type of power-up is collected (`PowerUp.OnCollected`; a Random power-up plays the sound of the type it rolled) |
| | `jackpotStart`, `jackpotEnd` | Jackpot starts / runs out (`PlayerJackpot.OnJackpotChanged`) |
| | `slimStart`, `slimEnd` | Slim starts / runs out (`PlayerSlim.OnSlimChanged`) |
| | `lanceFire` | a Lance shot is fired, once per click even with twin copies (`PlayerLance.OnFired`) |
| | `bombBlast` | a Bomb goes off with at least one asteroid on screen (`PlayerBomb.OnDetonated`). A Bomb collected with nothing on screen is silent, apart from `bombPickup` |
| | `warpOut`, `warpIn` | the Warp pops the ship out / back in (`PlayerWarp.OnWarpChanged`) |
| Asteroids | `asteroidSmashed` | the player smashes an asteroid (`AsteroidBehavior.OnSmashed`) |
| | `bonusSmashed` | the player smashes a BonusCarrier. Plays **instead of** `asteroidSmashed` |
| | `splitterBurst` | a Splitter bursts (`AsteroidBehavior.OnSplit`) |
| | `closeCall` | a close call is scored (`AsteroidBehavior.OnCloseCall`) |
| Level | `levelStart` | the level starts, after the menu transition (`GameManager.OnGameStarted`) |
| | `win` | the level is won (`GameManager.OnWin`) |
| | `gameOver` | the last life is lost, as the death sequence starts (`GameManager.OnGameOver`) |
| | `playerExplosion` | the death explosion, at the end of the death sequence (`PlayerDeath.OnExploded`) |
| | `overdriveStart`, `overdriveEnd` | the player reaches / drops out of overdrive (`MusicSpeedDriver.OverdriveChanged`) |

### Dialogue

A **`DialogueSlot`** is a normal slot (clips, volume, pitch range) plus **Chance**, from 0 to 1 (default 0.3):
0 = never, 0.3 = about 3 times in 10, 1 = every time. It plays *in addition to* the event's sound slot.

| Field | Default | What it changes |
| --- | --- | --- |
| `minLineGap` | 2 | Seconds that must pass after a line starts before another can play, so he never talks over himself. Set it longer than your longest line. A line that would start too soon is skipped |

| Slot | Plays when |
| --- | --- |
| `hitLine` | the player loses a life, **except the last one** (the last hit's line is `gameOverLine`) |
| `startLine` | the level starts, after the menu transition |
| `closeCallLine` | a close call is scored (frequent, so a low chance suits it) |
| `winLine` | the level is won |
| `gameOverLine` | the hamster reaches the screen during the death animation (`PlayerDeath.OnArrived`, about 1.25 s after the last hit), not at the instant of the last hit |
| `healthLine`, `invincibleLine`, `twinLine`, `scoreLine`, `boostRechargeLine`, `jackpotLine`, `slimLine`, `bombLine`, `lanceLine`, `warpLine` | that type of power-up is collected |

### Low life

| Field | Default | What it changes |
| --- | --- | --- |
| `lowLifeClip` | none | The sound that loops while the player has one life left. Empty = no loop |
| `lowLifeVolume` | 0.6 (0 to 1) | How loud the loop is. It is multiplied by `SFXManager`'s master volume |
| `lowLifeFadeTime` | 0.5 | Seconds the loop takes to fade in or out. 0 = instant |

## How it works

### Two kinds of event

- **Permanent:** the static events (`PowerUp.OnCollected`, `AsteroidBehavior.OnSmashed` / `OnSplit` /
  `OnCloseCall`, `PlayerDeath.OnExploded`), `SceneManager.sceneLoaded`, and `MusicSpeedDriver.OverdriveChanged`.
  They belong to a class or to the `AudioManager`, so they're subscribed once, in `OnEnable`.
- **Per level:** `GameManager`, `PlayerHealth`, `PlayerMovement`, `PlayerInvincibility` and `PlayerTwins` are
  recreated with every level and every Restart, but `GameSFX` isn't. So these are subscribed in `Bind`, which
  runs again each time a scene loads.

### `Awake`, `OnDestroy`

Reloading Level 1 (Restart or Menu) brings a second `AudioManager`. If an instance already exists, the new one
calls `Destroy(this)`, which removes only this component, so there's never more than one `GameSFX`, and no sound
plays twice. Otherwise it stores itself in the static `instance` and finds the `MusicSpeedDriver`.
`OnDestroy` clears `instance` if it's this one.

### `OnEnable` / `OnDisable`

Return straight away if this isn't the real instance (a duplicate still gets one `OnEnable` before its
`Destroy` takes effect). Otherwise they subscribe or unsubscribe the permanent events, and call `Bind` or
`Unbind`.

### `Bind` and `Unbind`

`Bind` calls `Unbind` first, so it's safe to call twice. This happens on the first level, where it runs from
`OnEnable` and again from the scene-loaded event. It then stores `GameManager.Instance` and the player's
`PlayerHealth`, `PlayerMovement`, `PlayerInvincibility`, `PlayerTwins`, `PlayerJackpot`, `PlayerSlim`, `PlayerLance`,
`PlayerBomb` and `PlayerWarp` (taken from `GameManager`, since this
script isn't on the player), subscribes to their events, and resets `lastLives` to the current life count.
Anything that's missing is skipped. `Unbind` removes the same subscriptions one for one. A destroyed object reads
as `null`, so the previous level's objects are skipped. If you add an event to `Bind`, add the matching line to
`Unbind`.

### `Play`

Called by every handler, and by `UISFX` (it's `public static`). It does nothing for a missing slot, no `SFXManager`, or no clips. Otherwise it picks a
random clip. If that's the same one as last time (`lastIndex`), it steps to a different one: ahead by 1 plus a
random 0 to (count − 2), wrapping round. Then it calls `SFXManager.PlayClip(clip, volume, pitch)`. `PlayClip`
ignores an empty clip entry.

There are two overloads (the same name with different parameters):

- **`Play(slot)`** rolls a random pitch from the slot's pitch range (1 for a missing slot) and calls the other one.
  Every handler uses this.
- **`Play(slot, pitch)`** does the work above with the pitch it is given, ignoring the slot's pitch range.
  `UISFX` uses it for the count sounds, whose pitch comes from how far the count has got.

### `Say`: dialogue with a chance

Called by a handler right after it plays the event's sound. It does nothing if the slot is missing or has no
clips (so an empty slot never starts the gap), if its `chance` is 0 or less, or if `Time.unscaledTime` hasn't
reached `nextLineTime` (the gap from the last line). Then
it rolls `Random.value` against `chance`: a bigger roll means no line. Only when all of those pass does it call
`Play` (so it also picks a random clip, never the same as last time, with the slot's volume and pitch) and set
`nextLineTime` to now plus `minLineGap`. The gap starts only when a line actually plays, and it uses unscaled time
so it still counts after game over. `chance <= 0` is checked first because `Random.value` can return exactly 0.

### The low-life loop: `Awake` and `Update`

`SFXManager`'s voices can't loop, so `GameSFX` makes its own `AudioSource` in `Awake` (after the duplicate check, so there is only one), set to
loop, not play on awake, and 2D. It is a component on the `AudioManager`, so it goes away with it and needs no clean-up.

`Update` runs every frame and works out whether the loop is **wanted**: there is a `lowLifeClip`, the `GameManager` says the game has started
(so not in the menu), the player has **exactly 1 life** (0 after dying, so death stops it), `Time.timeScale > 0` (so the Tab overlay, game
over and win stop it), and no warp is running (`PlayerWarp.IsWarping`, so the grace after a warp still counts as playing). It then moves a
fade level `lowLifeLevel` toward 1 (wanted) or 0 (not) with `Mathf.MoveTowards`, by `unscaledDeltaTime ÷ lowLifeFadeTime` a frame. Unscaled
time means the fade still finishes while the game is frozen. The source plays while the level is above 0 and stops at 0, and its volume is
`lowLifeLevel × lowLifeVolume × SFXManager.MasterVolume`. A fade time of 0 gives a step of 1, so it jumps straight to the target.

Example: fade time 0.5 at 60 fps. The step is 1/60 ÷ 0.5 ≈ 0.033 a frame, so the fade takes 30 frames.

A level whose max lives is 1 plays the loop all the way through.

### The handlers

Each plays a slot, apart from these. Handlers for events that have a dialogue slot also call `Say` straight
after their sound.

- **`HandleLifeLost`:** says `hitLine` only if lives are left. The last hit's line is `gameOverLine`, which plays
  later (see `HandleDeathArrived`), so a hit line there would only talk over it.
- **`HandleGameOver`:** plays the `gameOver` sound only, at the instant of the last hit.
- **`HandleDeathArrived`:** says `gameOverLine`. `PlayerDeath.OnArrived` fires once, when the ship reaches the
  camera and the hold begins, so the line starts as he arrives on screen.
- **`HandleLivesChanged`:** the event fires for gains and losses and only carries the new count, so
  `lifeGained` plays only when the count is higher than `lastLives`.
- **`HandlePowerUpCollected`:** a `switch` on the type.
- **`HandleSmashed`:** `bonusSmashed` for a BonusCarrier, `asteroidSmashed` for every other kind.
- **`HandleInvincibilityChanged`, `HandleTwinChanged`, `HandleOverdriveChanged`:** one event covers the start
  and the end, so the bool picks between two slots.

## Talks to

- **SFXManager**: calls `PlayClip`, and reads `Instance`.
- **GameManager**: reads `Instance`, `PlayerHealth`, `Player`, `PlayerInvincibility`, `PlayerTwins`, `PlayerJackpot`,
  `PlayerSlim`, `PlayerLance`, `PlayerBomb` and `PlayerWarp`, and `HasStarted` for the low-life loop; subscribes
  to `OnGameStarted`, `OnWin` and `OnGameOver`.
- **PlayerJackpot**, **PlayerSlim**, **PlayerLance**, **PlayerBomb**, **PlayerWarp**: subscribe to `OnJackpotChanged`, `OnSlimChanged`,
  `OnFired`, `OnDetonated` and `OnWarpChanged`. `PlayerWarp.IsWarping` is also read each frame for the loop.
- **PlayerHealth**: subscribes to `OnLifeLost` and `OnLivesChanged`; reads `CurrentLives` (each frame, for the low-life loop).
- **PlayerMovement**: subscribes to `OnSideBoost`, `OnSpeedBoost` and `OnBoostReady`.
- **PlayerInvincibility**: subscribes to `OnInvincibilityChanged`.
- **PlayerTwins**: subscribes to `OnTwinChanged` and `OnBodyLost`.
- **PowerUp**, **AsteroidBehavior**, **PlayerDeath**: subscribe to their static events (`PlayerDeath`'s are
  `OnExploded` and `OnArrived`).
- **MusicSpeedDriver**: subscribes to `OverdriveChanged`.

## Gotchas

- **It only works if it's on the `AudioManager`.** `SFXManager` is what plays the sounds, and the `AudioManager`
  is what survives level loads. Level 2 and 3 have no `AudioManager` of their own, so they only have sound when
  you arrive from Level 1 (the same limit the music has).
- **Two `GameSFX` components on the scene means one is removed.** `Destroy(this)` takes out only the extra
  component. Don't be surprised if a duplicate disappears from the Inspector at run time.
- **New events must be added in two places.** A per-level event needs a line in `Bind` *and* in `Unbind`. A
  missing `Unbind` line leaves a subscription behind, and the sound plays twice after a level reload.
- **The twin's last body plays two sounds at once.** `twinBodyLost` and `twinEnd` fire on the same frame.
  Leave one empty if that's too much.
- **A BonusCarrier plays only `bonusSmashed`.** If you want it to layer on top of the normal smash sound,
  `HandleSmashed` would need to play both.
- **Sounds keep playing when the game is frozen.** At game over `Time.timeScale` is 0, but audio ignores it, so
  `gameOver` and `playerExplosion` play normally.
- **The shield (`PlayerShell`) has no sound yet.** Its one event doesn't say what changed.
- **Slots start empty.** Until clips are dragged in, the script is silent. That's on purpose.
- **The new pickups also play their start sound together with the pickup sound.** Collecting Jackpot or Slim plays `jackpotPickup` / `slimPickup`
  and `jackpotStart` / `slimStart` at the same moment. Leave one empty if that's too much. A second pickup restarts the timer and plays no end sound.
- **`lowLifeClip` should be a clip that loops cleanly,** since the source loops it as it is. It's read once in `Awake`, so changing the clip
  while playing in the Editor takes a restart.
- **The loop uses `GameSFX`'s own source, not `SFXManager`'s voices.** So `SFXManager.StopAll()` doesn't stop it, and only the volume
  multiplier is shared. It stops by itself through the fade conditions.
- **The loop is silent in the menu, in the tutorial and in a warp's out, hold and pop-in,** but plays again in the grace after the pop-in.
- **`minLineGap` doesn't know how long a line is.** It's a fixed number of seconds from when a line starts. If a
  line is longer than the gap, the next line can start while it's still playing. Set the gap longer than your
  longest line.
- **A skipped line is lost.** A line that comes up during the gap isn't queued, it's just dropped. Close calls
  right after a hit, say, will often get no line.
- **Dialogue shares `SFXManager`'s 8 voices.** In a very busy moment a line could take over the oldest sound's
  voice, or the other way round.
