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

## Exhaust flame + Ring spin (in progress)

Replaced the "player trail" idea. A trail wouldn't work: the player never moves forward (asteroids move along
`Vector3.back` instead), so a Trail Renderer would only trace sideways movement.

**Done:**

- `PlayerMovement`: `using System;`, `public event Action OnSpeedBoost` (fired in the forward-boost branch of
  `OnButton`), and `public bool MeshVisible { get; private set; } = true;` (the hit-blink state, set in
  `UpdateInvulnerability`).
- `ExhaustFlame.cs` (finished and tested): two LineRenderers instead of particles. The outer flame is on the
  `Exhaust Flame` object (child of the **Ring**), the white inner core on its `Exhaust Core` child (Order in
  Layer 1). Both use Use World Space and the `ExhaustCore` material (URP Particles/Unlit, Additive, no Base
  Map). Longer and narrower with `Speed01`, Perlin width flicker that's stronger at low speed
  (`minFlicker`/`maxFlicker`), boost stretch on `OnSpeedBoost` (`boostLengthMultiplier`, `boostDuration`), drag
  around the orbit (`dragIntensity`, `dragSegments`, shared `FollowLine` method), HDR gradients
  (`GradientUsage(true)`), rainbow on the outer line only, blinks with `MeshVisible`, hidden on
  `OnGameOver` / `OnWin`. Sparks (Particle System) were tried and removed.
- If the line look ever needs more life: **option C**, a Shader Graph core with scrolling noise, is the planned
  next step.

**Still to do:**

1. `PlayerMovement`: add `public event Action<float> OnSideBoost`, fired in the side-boost branch with
   `lastSideDirection` (the side boost now remembers the last sideways direction for `sideInputMemory`).
2. `RingSpin.cs` (new) on the Ring: `spinDuration` = 0.4 s for a full 360° around z, in the boost's direction;
   store the starting local rotation in `Awake` and spin on top of it; ignore side boosts while a spin is
   playing (the future boost cooldown will stop that anyway). Knockback doesn't go through `OnButton`, so it
   won't spin.
3. Docs: `ExhaustFlame.md` done. `RingSpin.md` (section 7: the user explains first), README rows, and update
   `PlayerMovement.md` for the new events and `MeshVisible`.

## Boost rework: cooldown and visual flair

The current boost lives in `PlayerMovement.OnButton`: sideways input gives a boost around the circle
(`boostForce`, decaying by `boostDecay`), and no sideways input adds forward speed (`speedBoost`). It has **no
cooldown** and **no visuals**.

Wanted:

- Boosting works **off a cooldown**.
- **More visual flair** when boosting (and probably a way to show the cooldown).

Questions to settle with the user first: cooldown length, whether both boost types share one cooldown, how
the cooldown is shown (HUD, on the player, the trail above), what the boost effect looks like, and whether
boosting should interact with overdrive.

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

## Small cleanup

- `Rainbow Overlay` shader graph has unused leftover nodes (a Length of Position, a Split into an Add, a
  Vector1 × 2 into an Add). Harmless; can be deleted. See `ProgrammingDocs/RainbowOverlay.md`.
