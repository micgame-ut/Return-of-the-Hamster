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

## Phase asteroid (planned, not started)

Do this after the boost rework is merged. A new branch from `main` (`feature/phase-asteroid`).

An asteroid that phases in and out. While phased, its collider is off and it shows a hologram material.
Otherwise it's a normal asteroid. **Variables are agreed; the names and defaults still need a final OK.** Then
explain the logic (section 2) before any code.

- **In `AsteroidBehavior`**, not a separate script: a new `public enum EffectType { None, Phase }` with an
  `effectType` field ("Effect" header), separate from `MovementType`, so any movement can have any effect.
- **Phase fields** ("Phase" header): `solidTime` 1.5, `phasedTime` 1, `flickerTime` 0.3 (flicker just
  **before** each switch), `flickerInterval` 0.05. A public `IsPhased` property.
- **Timing comes from `elapsed`** (time since it started moving). So it starts **solid** once moving, and a wave
  that spawns together phases **in sync**. There's no random offset.
- **While phased:** collider off, `CanBreak` false (not destructible), no close calls, no outline. Materials are
  two arrays prepared once in `Awake` (solid, including any outline; and phased), swapped with no allocation.
- **`HitRadius`** returns 0 while the collider is off. Remember the radius while it's on, so the danger arc
  keeps its width.
- **`SpawnableTemplate`**: `phasedMaterial` (shared, like `outlineMaterial`).
- **`OrbitRing`**: `phasedColor` (blue, 0.2 / 0.5 / 1). A phased asteroid's arc shows blue: safe but not
  breakable.
- **Hologram Shader Graph** (section 13): Unlit, transparent, additive. A fresnel rim plus scrolling scan lines,
  with editable color, strength and speed.
- **Prefabs:** a phase asteroid prefab per movement type wanted (Linear and/or Wave).

## Small cleanup

- `Rainbow Overlay` shader graph has unused leftover nodes (a Length of Position, a Split into an Add, a
  Vector1 × 2 into an Add). Harmless; can be deleted. See `ProgrammingDocs/RainbowOverlay.md`.
