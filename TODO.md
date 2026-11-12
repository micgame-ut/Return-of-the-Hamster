# To Do

Planned work, in no particular order. Each item is a **new feature**, so follow [AGENTS.md](AGENTS.md):
ask the user for the variables first (section 5), explain the logic before any code (section 2), and work
one snippet at a time (section 3).

**The "End-product needs" section below takes priority over everything else on this list.**

## End-product needs (top priority)

Needed for the finished game. All of these come before any other item.

**Effects and death**
- [x] **Explosion VFX:** add the `vfx_explosion` prefab to the player explosion and the asteroid explosion.
  **Done and tested.** `vfx_explosion` has a timer `ParticleSystem` on its root so the pool and `PlayerDeath` can use
  it, and **Use Unscaled Time** is on for all five systems (the death explosion spawns while time is frozen). It's
  in `PlayerDeath`, `SpawnableTemplate`, the Bonus prefab and the Twin's destroyed-body effect. The old
  `Player Explosion`, `Asteroid Explosion` and `Gold Explosion` prefabs are now unused and can be deleted.
- [x] **Fix the player death animation** (`PlayerDeath`): turn the player to face the camera, and shake the whole
  player (ball, hamster and ring) all at once.
  **Done and tested.** The whole root shakes and `Slerp`s to face the camera over the approach. The exhaust flame
  also now trails behind the ship and thins out over `deathFadeTime` (`ExhaustFlame`, on unscaled time). The ring
  tips down `ringTilt` (25°) toward the camera while the hamster is moved out from under it and keeps facing the
  camera. New `PlayerDeath.OnArrived` and `OnExploded` events. See `PlayerDeath.md` and `ExhaustFlame.md`.

**Audio and dialogue**
- [x] **Audio on every event that needs it.** Drag-and-drop: each event gets a sound-effect slot in the Inspector,
  and an empty slot just plays nothing. (`SFXManager` and `ButtonSFX` already exist in `Assets/Scripts/Audio`;
  build on those.)
  **Done and tested.** `GameSFX.cs` (on the `AudioManager`) has 23 slots for the player, power-up, asteroid and
  level events. New events: `PlayerMovement.OnBoostReady`, `PlayerTwins.OnBodyLost`, `AsteroidBehavior.OnSmashed` /
  `OnSplit` / `OnCloseCall`, `PlayerDeath.OnExploded`. Docs: `GameSFX.md` (**waiting for your review**).
  Not covered yet: the shield (`PlayerShell`).
  UI sounds are now in `UISFX.cs` (13 slots: menu move/select/blocked, start, credits, end screen, stats, score
  breakdown, score popup). `ButtonSFX` and `SFXManager`'s ID list were removed. Docs: `UISFX.md`
  (**waiting for your review**). **Not tested in Unity yet.**
- [x] **Small dialogue that reacts to in-game events:** being hit, getting a power-up, reaching the end, starting
  the level and a close call.
  **Done and tested.** It's in `GameSFX.cs`: a `DialogueSlot` (clips, volume, pitch and a `chance`, default 0.3)
  for hit, level start, close call, win, game over and each power-up type, played alongside the normal sound. A
  shared `minLineGap` (2 s) stops him talking over himself. The last hit uses `gameOverLine` instead of
  `hitLine`, and it plays when the hamster reaches the screen in the death animation (`PlayerDeath.OnArrived`).
  Docs: `GameSFX.md` (**waiting for your review**).

**Menu and flow**
- [ ] **Tutorial phase before Level 1** that the player can choose to skip.
  **Built, waiting for your test.** Decided: it runs inside `Level_1`, with live prompts, and a "Play tutorial?"
  popup after Start (shown every time, no `PlayerPrefs`). Close calls are not taught. `GameManager` has a tutorial
  state (`InTutorial`, `IsFlying`, `StartTutorial`, `OnTutorialStarted`) so the player can fly while the level, score
  and HUD wait. `MainMenu` shows the `TutorialPopup`; `MenuTransition.Play(bool)` ends in the tutorial or the level.
  `TutorialManager` (on the `Tutorial` object) runs 7 stages: orbit, speed, sideways dash, forward boost, dodge an
  asteroid, smash one, collect a power-up, then calls `StartGame()`. Tutorial hits cost no lives, and no points or
  stats count. Docs: `TutorialManager.md` (**waiting for your review**), plus `GameManager`, `MenuTransition`,
  `PlayerMovement`, `PlayerHealth`, `Score`, `RunStats` and the four "world is alive" scripts updated.
  To check in Play mode: the popup, Skip (unchanged level), each tutorial stage and its retry, and the handoff
  into the level (spawner, HUD and score should start fresh).
- [ ] **Leaderboard replacing the stats overlay:** the highest scores, submitted with a three-letter arcade name on
  game end (win or loss).
  **Built, waiting for your test.** Decided: local scores (saved with `PlayerPrefs`, per machine), top 10, the name
  page only opens for a score that makes the list, and the stats (asteroids, power-ups, time, progress) are dropped.
  Name input: roll down/up to change the letter, the button locks it and moves on, roll left to step back.
  `LeaderboardStore` (static: `Qualifies`, `Submit`, `Clear`), `NameEntry` (the name page), `LeaderboardPanel` (the
  window, with the new row highlighted). `EndScreen` opens the name page when `ScoreBreakdown.OnFinished` fires, then
  the leaderboard; the STATS buttons became SCORES. `UISFX` has new `leaderboardOpen/Close`, `letterChange`,
  `letterLock` and `nameSubmitted` slots. Docs: `LeaderboardStore.md`, `NameEntry.md`, `LeaderboardPanel.md`
  (**waiting for your review**), `EndScreen.md`, `UISFX.md` and `MenuNavigator.md` updated.
  To check in Play mode: finish a run with a score above 0, enter a name, see the board; the SCORES buttons; a
  run that doesn't qualify; sounds in the new `UISFX` slots.
- [x] **Menu navigation with mouse input:** every menu item selectable by scrolling between the options using input
  from the mouse (not the keyboard), and pressing Space to choose, instead of clicking. Decided: the trackball movement
  (the mouse delta), no mouse pointer at all.
  **Done and tested.** New `MenuNavigator` (one per page) plus `EndScreen.Show` no longer shows
  the cursor. Needs a `Menu Navigator` on the main menu, credits, Game Over, Win and stats pages, with each Button's
  Selected Color set. The main menu shows a triangle `marker` beside the selected button instead (added in Unity as
  an Image under Menu Canvas). Docs: new `MenuNavigator.md` (**waiting for your review**), `EndScreen.md` updated.
- [ ] **Anchor the menu selection items** so they move with the window. **Handled by a teammate; skipped here.**
- [x] **More dynamic menu float:** adjust `HamsterMenuFloat` so the player moves in a livelier way on the menu.
  **Done and tested.** He now drifts in a box on all three axes (`boxHalfHeight`, `boxHalfDepth`, `ySpeed`,
  `zSpeed`), turning slowly and overshooting the walls (`turnEase`), with a small extra wobble (`wobbleAmount`,
  `wobbleSpeed`), a swelling sideways speed (`speedVariation`, `speedVariationRate`), and per-axis spin paces with a
  lean (`tumbleSway`, `tumbleSwaySpeed`). Docs: new `HamsterMenuFloat.md` (**waiting for your review**).

**HUD and text**
- [x] **Reformat the stats and the other text items** with the new font.
  **Done and tested.** The end-screen stats, score total, name and score rows were resized for PublicPixel (one
  character is as wide as the font size) and set to no wrapping. Inspector edits only. Still on other fonts: the
  `Exclamation` prefab (LiberationSans) and `OptionsText` in `Level_1` (Press Start 2P).
- [x] **Score popups stack instead of overlapping each other** (`ScorePopups`), so each one stays readable. (Today
  popups that come at the same time, such as a smash plus a close call plus a power-up label, land on top of one
  another.)
  **Done and tested.** New popups start above any popup they'd overlap (`stackSpacing`, `stackWidth`), and
  each stays fully opaque for `holdTime` before fading. Set `lifetime` to about 1.1 on the HUD canvas. Docs:
  `ScorePopups.md` updated.

**Look and feel of the player**
- [x] **Player shell:** adjust `PlayerShell` so only the back faces show to the player.
  **Done and tested.** Done in the graph, not the script: `Rainbow Overlay.shadergraph`'s Render Face
  is now Back, so the shield and the rainbow both show only the far side of the bubble. Docs: `RainbowOverlay.md` and
  `PlayerShell.md` updated.
- [x] **Ring brightness gradient:** adjust it (the Ring Glow graph) so the glow is more noticeable and the player
  can truly tell when a dodge is ready.
  **Done and tested.** Material values brighter (step 1, in the Inspector). In `Ring Glow.shadergraph`:
  the charging glow follows Charge² (dark until near the end), a flash when the boost becomes ready (`Ready Pulse` /
  `Pulse Glow`, set by `BoostRing` from the new `readyPulseTime` and `OnBoostReady`), the ready glow breathes
  (`Breathe Amount` / `Breathe Speed`), and two sharp bright rings (`Ring 1/2 Radius`, `Ring Core`, `Ring Edge`,
  two HDR colors, `Ring Brightness`). Docs: `RingGlow.md`, `BoostRing.md` (**waiting for your review**).
- [x] **Forward boost:** adjust it so it's noticeable.
  **Done and tested.** On a forward boost: an FOV punch (`boostFovKick` 8°, `boostFovTime` 0.4) and a
  camera shake (`boostShakeDuration` 0.3, `boostShakeMagnitude` 0.12) in `PlayerCamera`, and a star burst
  (`boostBurstSpeed` 120, `boostBurstTime` 0.5) in `StarfieldStreamer`. Still to do in Unity: set `speedBoost` on the
  player from 3 to about 4.5, then update the `speedBoost` row in `PlayerMovement.md`. Docs: `PlayerCamera.md` and
  `StarfieldStreamer.md` updated.

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
  Preset, Delete Preset). The builder's "ideas" list is otherwise done.
- **Wave sweep preview: built and tested.** For a Wave asteroid prefab, each previewed sphere gets a cyan
  arc showing the stretch of the orbit it swings along (`AsteroidBehavior.Amplitude`). Visual only; the fairness
  checks still skip Wave asteroids.
- **Timing readout: built, not yet tested.** The panel's Timing section shows approach speed, appears →
  reaches player, shortest / longest wait between steps and total length at Speed 0, a chosen Speed and the
  player's max, and the Scene labels show `+x.xx s` per step. `PatternGizmos` now has `GetSteps` and timing
  helpers.
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
- **Runtime rotate / mirror: built, not yet tested.** Each wave has `randomRotation`, `rotationStep` and
  `mirrorChance`; the spawner picks a turn and a flip once per spawn and applies them as each child is revealed.
  Mirrored Wave asteroids swing the other way (`AsteroidBehavior.MirrorSwing`). Test This Pattern ignores the
  variation. See `ObjectSpawner.md`.

## New asteroid types (AsteroidBehavior)

Built one feature at a time, each with a test point.

- **`AsteroidKind` replaces `destructible`: built and tested.** Hazard, Destructible, BonusCarrier. BonusCarrier
  doesn't do anything of its own yet. (A Nebula kind was removed: the Frost effect replaced it.)
- **Spiral movement: built and tested.** Own orbit radius, `spiralSpeed`, flips with `MirrorSwing`.
- **Homing movement: built and tested.** Turns toward the nearest player body (`homingSpeed`), locks and
  flies straight at `homingStopDistance`, keeps its own radius (`startRadius`, renamed from `spiralRadius`).
- **Homing spacing: built and tested.** A Homing asteroid holds its angle rather than turn within
  `(HitRadius + other.HitRadius) × (1 + homingGapScale)` of another active asteroid (3D distance; phased
  asteroids are skipped).
- **StopAndGo movement (replaces the Orbiter idea): built and tested.** Approaches, eases to one stop at
  `stopDistance`, hangs for `stopTime` (the mesh shakes for the last `shakeTime`), then dashes at
  `dashMultiplier` × the approach speed.
- **Magnet effect (pull only): built and tested.** `approachScale` (per-asteroid speed), `AddPull` on
  `PlayerMovement`, `UpdateMagnet` (`magnetRadius`, `magnetStrength`, pull along the orbit, stronger up close),
  skipped by close-call tracking. The prefab needs its Collider component removed.
- **Black-hole shader (`Black Hole.shadergraph`): built and tested.** Shadow, bent background, thin ring, an
  edge-on streaked accretion disc with a top and bottom arch, a curved near-side crossing, and the far half hidden
  behind the shadow. Documented in `BlackHole.md` (**waiting for your review**, per AGENTS.md section 13).
- **MagnetMirror (`MagnetMirror.cs`): built and tested.** Scene Color is black on the gameplay (Overlay) camera,
  so it moves the Magnet's mesh child to the matching spot in front of the background camera (the mesh child is on
  the **Background** layer), and turns the effect so its arch points at the orbit's center on screen: it sets the
  material's `_Disk_Angle` per Magnet (a `MaterialPropertyBlock` on the mesh's first slot) to `90 − φ +
  angleOffset`. Documented in `MagnetMirror.md` (**waiting for your review**).
- **Idea, needs scoping:** give `PlayerTwins` a single "main player" identifier that swaps places with a copy.
- **Growing effect: built and tested.** Pulses (mesh only) for `pulseDistance` before `growDistance`, then
  the root pops to `growScale` (hitbox too) with a visual overshoot that settles over `settleTime`. Scale is
  restored in `OnEnable`.
- **Splitter effect: built and tested.** At `splitDistance` it bursts into `splitCount` smaller pieces
  (`splitPiece`, `pieceScale`) fanned `splitSpread` apart. Pieces are pooled by `EffectPool` (`GetAsteroid`,
  `PrewarmAsteroids`, `ReturnAsteroid`), skip the warning wait, and `PatternCopy.Despawn` sends them back.
- **Frost effect (the Frost comet): built and tested.** A comet leaves patches on the player's orbit ring
  near the player's depth (`PaintFrost`: `dropSpacing`, `paintDistance`), each lasting `patchLifetime`.
  Standing in one slows the player's forward `Speed` (a drag of `speedSlow`) and orbit movement
  (`orbitSlow`) through `PlayerMovement.ApplyFrost`, the strongest request winning. A finished comet retires
  (hidden, off `Active`, particles stopped) until its patches expire (`Finish`). Gizmos draw the patches.
  Documented in `AsteroidBehavior.md` and `PlayerMovement.md`.
- **Frost look: built and tested.** `Frost Rock.shadergraph` (the frosted rock, one **Frost Amount** slider),
  `Frost Gas.shadergraph` (soft ragged puffs for the mist and the cyan dust, two materials), and
  `Frost Trail.shadergraph` (the ribbon). Two particle layers (`Frost Clouds`, with `Frost Dust` as its child) and
  a Trail Renderer, all set to last `patchLifetime` (`OnEnable` sets the ribbon's Time and the mist's lifetime).
  Documented in `FrostRock.md`, `FrostGas.md` and `FrostTrail.md` (**waiting for your review**, per AGENTS.md
  section 13).
- **BonusCarrier kind: code built, waiting for your test.** Smashing one adds `bonusPoints` flat (200) with a
  `bonusLabel` ("BONUS") pop-up, counted as Asteroids, and plays its own `bonusExplosion` if set. Still to do in
  Unity: the prefab (Kind = BonusCarrier), a gold material, and a gold explosion prefab.
- **Scene view:** draw the Magnet radius (and the Frost patch radius) in the pattern builder.
- **Frost, loose ends:** `Frost Dust`'s lifetime isn't set from `patchLifetime` (only `Frost Clouds` is); delete the
  unused `Nebulous` prefab in Unity.

## Small cleanup

- `Rainbow Overlay` shader graph has unused leftover nodes (a Length of Position, a Split into an Add, a
  Vector1 × 2 into an Add). Harmless; can be deleted. See `ProgrammingDocs/RainbowOverlay.md`.
