# To Do

Planned work, in no particular order. Each item is a **new feature**, so follow [AGENTS.md](AGENTS.md):
ask the user for the variables first (section 5), explain the logic before any code (section 2), and work
one snippet at a time (section 3).

## Twin power-up

**Built and tested** (branch `twin-powerup`). Each Twin pickup adds a copy of the player, spaced evenly around
the orbit (up to 4 bodies), easing into place. Copies mirror everything (movement, boosts, roll, speed, flame,
invincibility shell). A hit during the twin destroys one body and lives don't change; when the player itself is
hit, it takes over the nearest copy's spot (`ShiftOrbit`) and that copy is removed. The danger warning and the
full-health shield pause during the twin. Health gives no life while twinned. A same-frame hit on the copy the
player just jumped onto counts as a normal hit.

New: `PlayerTwins.cs` (on the player), `TwinCopy.cs` (on the `Twin Copy` prefab), `Twin PowerUp` prefab.
Changed: PlayerMovement (`OrbitAngle`, `MeshTransform`, `ShiftOrbit`, `OnOrbitShifted`), PlayerShell (shell
state + `OnShellChanged`, paused while twinned), GameManager (`PlayerTwins`), AsteroidBehavior, PowerUp (`Twin`
type), DangerWarning, ExhaustFlame (works on copies, snaps after a jump).

**Docs:** `PlayerTwins.md` and `TwinCopy.md` written, with
README rows; existing scripts' docs updated.

## Exhaust flame, boost cooldown and ring spin

**Done and tested** (branch `feature/boost-rework`).

- **Exhaust flame** (earlier work): `ExhaustFlame.cs` draws two LineRenderers (an outer flame plus an HDR inner
  core) on the Ring, with speed scaling, flicker, boost stretch and drag. See `ExhaustFlame.md`. If the look
  ever needs more life, a Shader Graph core with scrolling noise ("option C") is the next idea.
- **Boost cooldown** (`PlayerMovement`): `boostCooldown` (3 s), shared by the side and forward boosts.
  `BoostCharge01` goes from 0 just after a boost to 1 when ready. A new `OnSideBoost(direction)` event, and a
  read-only `BoostDecay`. The dodge distance is still tuned with the existing `boostForce` / `boostDecay`
  (distance = force ÷ decay).
- **Ring Glow** Shader Graph and material on the `ring` (player and Twin Copy): the glow rises with the charge
  and jumps to full when ready (`charging`, `ready`, `glow`, `emission`). Transparent, fading toward
  `Fade Axis` (`axisDistance`, `alpha`), with the glow faded by alpha too. See `RingGlow.md`.
- **BoostRing.cs** on each `ring`: sends `_Charge` through a MaterialPropertyBlock, and on `OnSideBoost` rolls
  the ring 360° eased by the dodge's own fade (`1 − e^(−boostDecay·t)`). `spinAxis` is (0, 0, −1) in the
  prefabs. See `BoostRing.md`.
- Possible follow-ups: dark ring on the menu (it shows Ready Glow there); a mid-spin boost (only possible with
  `boostDecay` below ~1.5) snaps the ring back. It could carry on from the current angle instead.


## Score system

**Done and tested** (branch `score-system`, PR to `main`). Score on the Game Manager: time score from
LevelTimer progress, asteroid smashes with combos, wave clears and perfect waves, close calls
(CloseCallZone), score multiplier power-ups (MultiplierDisplay), and an overdrive tally (OverdriveTally).
Popups through ScorePopups; HUD score pulses. Only losing a life breaks the combo or a perfect wave. See
`Score.md` and the docs for each script.

**In progress: end-screen score breakdown** (branch `score-breakdown`, uncommitted). Between the title and
the stats on both end screens: `SCORE 000000`, then one row per source that earned points (Progress,
Asteroids, Close calls, Waves, Overdrive; zero sources skipped). Each row's label pops in, then its value,
then the total ticks up (`countUpTime` 0.3) before the next row (`rowDelay` 0.4). Real time (the game is
frozen). Space (boost, `skipAction`) finishes it instantly.
- Done: `Score.ScoreSource`, per-source totals (`sourceTotals`, filled in `Award` and `AddOverdriveBonus`),
  `GetSourceTotal` (Progress = `TimePoints`). `ScoreBreakdown.cs` fields, `Awake`, `OnEnable` (enables
  the action), `Update` (skip flag), `Wait`, `Pop`, `CountUp`, `ShowRow`, `Play`, `Reveal` (float running
  total shown rounded down, so it matches the HUD; each source read at its row's turn, so the overdrive
  tally banked by `OnWin` is included).
- Next: `EndScreen`: per-screen
  total text + row container, call `Play`; layout B (one shared stats overlay with Close, a Stats button
  per panel; both `statsText` slots point at the overlay text); hide a `scoreHud` object in `Show`.
- Scene: group the score text, multiplier indicator and tally text under a stretched `Score HUD` child and
  move `ScorePopups` onto it (scripts that must keep running stay on the canvas). Row prefab: label left,
  value right, no Horizontal Layout Group. Watch for Space also "submitting" a selected UI button.
- Later experiment: combo / multiplier bonus rows, for the user to judge in Play mode.

**Follow-up: per-level leaderboard**, ranked by score, with a details view (hamster name + RunStats
snapshot at that moment). Round `Score.Total` down, like the HUD, when saving it.

## Menu inside Level 1

**Done and tested.** The menu lives in `Level_1`; there's no separate Menu scene any more. Pressing Start
plays a transition (MenuTransition), and then the level starts with no scene change. See `MenuTransition.md`
for the full walkthrough, and `GameManager.md` ("Starting: `HasStarted` and `StartGame`").

- **Waiting at the menu:** GameManager has `HasStarted`, `StartGame()`, `OnGameStarted`, `TimeSinceStart`,
  `startWithMenu` (ticked on Level 1 only), `SkipMenuOnce` (set by Restart) and `hud`. PlayerMovement
  (`InputLocked`), LevelTimer, ObjectSpawner, RunStats, SkyboxSpeedController (sky still, no glow),
  StarfieldStreamer (no stars) and OrbitRing wait for the start. The cursor stays visible on the menu.
- **The transition:** the planets and UI blend over `duration`; the hamster glides over `hamsterDuration` to
  `PlayerMovement.StartPosition` / `StartRotation`; the far planet goes to a `Far Planet Target` marker (a
  stripped duplicate of `Planet`); the near planet goes to a `Near Planet Target` marker. Then `startDelay`,
  then `StartGame()`. The cursor hides the moment Start is pressed.
- **After the start:** the HUD and ring appear; the flame (`growTime`), stars (`rampTime`) and core glow
  (`glowGrowTime`) ease in from `TimeSinceStart`. LevelTimer records the Earth's origin on the first frame
  of play.
- **Restart** skips the menu; **MENU** and Level 3's Next Level go back to it (`menuSceneName` = `Level_1`).
- **HamsterMenuFloat** now spins on all three axes at `rotationSpeed`.

**Still to do:**

- **Unity:** take `Menu` out of Build Settings, set `LevelTemplate`'s (and `TestGame`'s) EndScreen Menu Scene
  Name to `Level_1`, then delete `Assets/Scenes/Menu.unity`.
- **Maybe later:** a noise wobble for the floating hamster instead of the steady spin (`wobbleAngles`
  (25, 25, 25) max degrees per axis, `wobbleSpeed` 0.3, a random `noiseSeed`; Perlin noise per axis mapped to
  ± the max angle).
- **Backburner:** docs for `MainMenu`, `HamsterMenuFloat`, `PlanetMovement`, `TitleFaceFlash` and
  `CreditsPanel`.

## Random power-up

**Done and tested** (branch `feature/random-powerup`). The patterns use one `Mystery PowerUp` prefab
(`PowerUp` type **Random**) that rolls Health, Invincible, Twin or Score Multiplier on pickup.

- Chances: `healthChance` / `invincibleChance` / `twinChance` / `scoreChance` on the SpawnableTemplate
  (40/20/20/20), as shares of the total.
- Wasted picks are skipped (Health at full lives or while twinned, Twin at the cap via the new
  `PlayerTwins.CanAddBody`). If everything is wasted, the roll falls back to the plain chances.
- Every pickup raises the static `PowerUp.OnCollected`; ScorePopups shows a label for it (`healthLabel` etc.).
- `Mystery PowerUp` is the old `Twin PowerUp` prefab renamed (same GUID). The Health, Invincibility and Score
  prefabs still work as fixed types. See `PowerUp.md` (`RollType`).

## Phase asteroid

**Done and tested** (branch `feature/phase-asteroid`). An asteroid that phases in and out: while phased, its
collider is off and it shows a hologram look. Otherwise it's a normal asteroid.

- **`AsteroidBehavior`**: a new `EffectType { None, Phase }` dropdown (`effectType`), separate from
  `MovementType`, so any movement can have any effect.
  - Phase timing: `solidTime` 1.5, `phasedTime` 1, `flickerTime` 0.3, `flickerInterval` 0.05.
  - It runs from `elapsed`, so it starts solid once moving, and a wave revealed together phases in sync.
  - While phased: `IsPhased` is true, `CanBreak` is false, there are no close calls, and no outline.
  - `HitRadius` is measured once in `Awake`. The material arrays are prepared once.
- **`SpawnableTemplate.phasedMaterial`**: the `Asteroid Phase` hologram Shader Graph (fresnel `rim` plus
  world-space `scan` lines, additive). See `AsteroidPhase.md`.
- **`OrbitRing.phasedColor`**: a phased asteroid's arc is blue.
- Also on this branch:
  - `HamsterMenuFloat` switches itself off if the game has already started (it was on the `hamter` prefab,
    so it fought PlayerMovement in non-menu levels).
  - `PlayerDeath` hides the ring when the ship explodes.
- **Possible follow-up:** remove `HamsterMenuFloat` from the `hamter` prefab and add it only to Level 1's
  player (the guard covers it meanwhile).

## Options menu: forward threshold

**Built and tested** (`PlayerMovement`): `forwardThreshold` (30°, slider 0–90) and `minInputForAngle` (2 px).
If the trackball movement is within `forwardThreshold` of straight up, `OnMove` zeroes its sideways part, so
the slide, the sideways roll and the side-boost memory all ignore it. See `PlayerMovement.md`.

**Still to do:** an options screen on the menu where the player changes `forwardThreshold`. The menu's
Options button is being removed until this exists.
- Needs: a slider UI, saving the value (probably `PlayerPrefs`), and `PlayerMovement.Awake` reading it.
- Maybe later: smooth the angle over a few frames if slow forward rolls flicker between filtered and
  unfiltered.

## Depth fade (asteroids and power-ups darker with distance)

**Built, not yet tested.** `SpawnableTemplate` has `darkenStart` (0.2), `darkenEnd` (1) and `farColor`, measured
as a share of the new `OrbitSettings.SpawnDistance` (so it follows `spawnerPosition`). `GetDepthTint` turns a
distance into a color between white and `farColor`. AsteroidBehavior and PowerUp apply it every frame with a
property block (`_BaseColor`; the outline slot is left alone), and the Asteroid Phase graph has a new `Tint`
property (`depth` group). The asteroid's warning glow was removed in the same change. See `SpawnableTemplate.md`.

**Still to do:**
- Test it in Play mode, then tune `darkenStart`, `darkenEnd` and `farColor` by eye.
- Maybe later: the tint ignores emission, so an emissive material would keep glowing at a distance.

## Pattern builder improvements

The Random tab is built (see `PatternGizmosEditor.md`). More ideas, all agreed as worth doing, roughly in order:

- **Workflow: built and tested** (the slowest part used to be: duplicate a pattern prefab, clear it, build
  it, add it to the spawner's waves, then play the level to test it). The builder's **Waves** section has Save
  Pattern As... (with Clear After Save), Test This Pattern (plays only that pattern on a loop in its own
  `PatternTest` scene, with the full range of speed) and Add to Spawner, all also under Tools → Pattern Builder. New script
  `PatternTest.cs`. Changed: ObjectSpawner, GameManager (editor-only lines), PatternGizmosEditor. Docs written,
  including `PatternTest.md`.
- **Editing: Gap / safe lane on Ring and Arc, Rotate and Mirror are built and tested.** New, not yet tested:
  the Zigzag and Twist shapes (a Twist gap turns with each ring), Shift Z and Reverse Z (all and selected), and
  Distribute Selected by Depth / by Angle (Tools menu). Lane and Burst are Spiral with Angle Step 0 / Z Step 0
  (a note in the panel says so). Possible follow-up: draw the red gap wedge on a gapped Twist.
- **Jitter and Prefab Mix: built, not yet tested.** Jitter Angle / Jitter Z on every shape except Random (shared
  seed and Reroll with Random), and a weighted mix of up to 4 prefabs per slot (Asteroid and Power-Up), remembered
  between sessions. Preview spheres stay one colour.
- **Auto-naming: built, not yet tested.** Children are named `Step N - Prefab K` and put in step order, after each
  Add (a checkbox, on by default) or with the **Name Children** button.
- **Presets: built, not yet tested.** A Presets section at the top of the builder saves the placement settings to
  `Assets/Scripts/Editor/PatternPresets/<name>.json` and loads them back by name (dropdown, Load, Name + Save
  Preset, Delete Preset). The builder's "ideas" list is otherwise done; what's left is the Wave sweep preview.
- **Timing readout: built, not yet tested.** The panel's Timing section shows approach speed, appears →
  reaches player, shortest / longest wait between steps and total length at Speed 0, a chosen Speed and the
  player's max, and the Scene labels show `+x.xx s` per step. `PatternGizmos` now has `GetSteps` and timing
  helpers. Still to do: a Wave sweep preview.
- **Fairness checks (warnings only): built and tested.** New editor script `PatternFairness.cs` plus a
  Fairness section in the builder: overlapping asteroids, a step with no safe lane, a gap too far from the one
  before (lane reach), and power-ups that are too far from the orbit, blocked or out of reach. Red rings mark
  the objects in the Scene view. Wave asteroids aren't checked yet. `AsteroidBehavior` has a new read-only
  `Movement` property. Docs written, including `PatternFairness.md`. The patterns stay within the player's limits for now; harder, limit-testing waves come later.
- **Presets** for builder settings, and **auto-naming** children ("Step 3, Asteroid 2").

## Wave phases and runtime variation (ObjectSpawner)

Not started. Waves should be available only within certain points of level progress, so more waves unlock as
the player progresses and old ones can drop out.

- **Phases: built, not yet tested.** Each wave has `fromProgress` / `untilProgress` (0–1, from `LevelTimer`
  progress) and is skipped outside that range. When no wave is available but some have patterns, the spawner
  waits 0.5 s and rechecks. Still to do: set the ranges on the real levels' wave lists. Possible follow-up: a
  preview of which waves are active at a given progress.
- **Runtime rotate / mirror:** rotate or mirror a pattern as it spawns, so one authored pattern can come from
  several sides. Needs a look at how Wave asteroids and power-ups follow the rotation.

## Small cleanup

- `Rainbow Overlay` shader graph has unused leftover nodes (a Length of Position, a Split into an Add, a
  Vector1 × 2 into an Add). Harmless; can be deleted. See `ProgrammingDocs/RainbowOverlay.md`.
