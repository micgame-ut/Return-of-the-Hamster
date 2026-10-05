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

## Player trail

Add a trail behind the player character (for example a Trail Renderer on the player, or on its mesh).

Questions to settle with the user first: what it looks like (width, length, color, fade), whether it reacts
to speed / overdrive / invincibility / boosting, and whether it should match the skybox stream colors.

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
