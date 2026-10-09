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

## Small cleanup

- `Rainbow Overlay` shader graph has unused leftover nodes (a Length of Position, a Split into an Add, a
  Vector1 × 2 into an Add). Harmless; can be deleted. See `ProgrammingDocs/RainbowOverlay.md`.
