# To Do

Planned work, in no particular order. Each item is a **new feature**, so follow [AGENTS.md](AGENTS.md):
ask the user for the variables first (section 5), explain the logic before any code (section 2), and work
one snippet at a time (section 3).

## Twin power-up

A new power-up that **duplicates the player on the opposite side of the orbit**. On the **next hit** (when the
player would take damage), **one of the two is destroyed** and the user **keeps control of the one left
alive**.

Questions to settle with the user first:

- Does the twin mirror the player's movement at +180° around the circle? Does it copy boosts and roll?
- On a hit: is the twin that was hit the one destroyed? Does the hit still cost a life, or does the twin act
  as the shield?
- How does it combine with invincibility (and its max-speed lock), the shield shell, and Health?
- Does it last until the next hit, or also have a time limit? What does the twin look like?

Code that currently assumes a single player and will need checking: `GameManager.Player`, `OrbitRing`
(danger arcs), `AsteroidBehavior` (collision, `TakeHit`), `PlayerDeath`, `RunStats`, `PlayerCamera`,
`PlayerShell`. Power-ups are defined in `SpawnableTemplate` and applied in `PowerUp`.

## Exhaust flame + Ring spin (in progress)

Replaced the "player trail" idea. A trail wouldn't work: the player never moves forward (asteroids move along
`Vector3.back` instead), so a Trail Renderer would only trace sideways movement.

**Done (not committed yet):**

- `PlayerMovement`: `using System;`, `public event Action OnSpeedBoost` (fired in the forward-boost branch of
  `OnButton`), and `public bool MeshVisible { get; private set; } = true;` (the hit-blink state, set in
  `UpdateInvulnerability`).
- `ExhaustFlame.cs` (new, finished): a Particle System on an `Exhaust Flame` child of the **Ring**, pointing back
  toward the camera, Simulation Space = Local. Longer/faster/narrower with `Speed01` (particle speed =
  length ÷ `flameLifetime`, so `maxLength` is a hard cap), Perlin-noise width sputter, burst of bigger particles
  on `OnSpeedBoost`, swaps Color over Lifetime to `rainbowColor` on `PlayerInvincibility.OnRainbowChanged`,
  blinks with `MeshVisible`, cleared with `StopEmittingAndClear` on game over / win.
- First Play-mode test: the flame showed in front of the ship with widths 1–4 / lengths 2–5. Going back to the
  default widths (0.2 / 0.5) fixed it for now. Not fully diagnosed: oversized billboards vs. the flame's
  rotation (the menu adds X = −90; the Ring may already be turned). Revisit.

**Still to do:**

1. Finish testing the flame (full checklist: speed scaling, boost burst only on forward boost, rainbow + warning
   flicker in sync, blink, game over and win clear it).
2. Open decisions: a comment on the `stopped` field (`// true once cleared for game over / win`), and a
   `[Min(0.01f)]` guard on `flameLifetime` (0 divides by zero).
3. `PlayerMovement`: add `public event Action<float> OnSideBoost`, fired in the side-boost branch with
   `lastSideDirection` (the side boost now remembers the last sideways direction for `sideInputMemory`).
4. `RingSpin.cs` (new) on the Ring: `spinDuration` = 0.4 s for a full 360° around z, in the boost's direction;
   store the starting local rotation in `Awake` and spin on top of it; ignore side boosts while a spin is
   playing (the future boost cooldown will stop that anyway). Knockback doesn't go through `OnButton`, so it
   won't spin.
5. Docs: `ExhaustFlame.md` and `RingSpin.md` (section 7: the user explains first), README rows, and update
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
