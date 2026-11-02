# SpawnableTemplate

## Purpose

Holds the settings shared by everything the spawner sends at the player, asteroids and power-ups:
warning timing, spin, explosion, outline colors, despawn distance, what happens when the
player gets hit, how much the player's speed affects approach speed, how they darken with distance, and the
power-up spin, pickup effect and invincibility duration. Every asteroid and power-up prefab points at the
same template asset, so changing a value there changes it for all of them at once.

It was called `AsteroidTemplate` until power-ups started using it. Asteroid-only settings and power-up-only
settings sit in their own sections; `baseApproachSpeed`, `speedMultiplier` and `despawnBehindCamera` are
shared by both.

It's only data. It has no `Update` or other logic, apart from one small helper, `GetDepthTint`.

## Where it lives

- **Attached to:** nothing. It's a **ScriptableObject**, which is saved as an asset file in the project
  instead of being attached to a GameObject.
- **The asset:** `Assets/Resources/SpawnableTemplate.asset`.
- **Used by:** the `template` field on every asteroid prefab in `Assets/Prefabs/Asteroid Types/`, and on
  power-up prefabs (`Assets/Prefabs/Health PowerUp.prefab`).
- **Making a new one:** right-click in the Project window → Create → Return of the Hamster → Spawnable
  Template. The `[CreateAssetMenu]` line at the top of the script adds that menu option.

### Why a ScriptableObject

The other option would be putting all these fields on AsteroidBehavior itself. Then every asteroid would
carry its own copy, and changing the knockback would mean editing every prefab and making sure they
match. With a ScriptableObject, there's one copy of the settings and every asteroid just points to it.

The per-asteroid settings (kind, movement type, wave) stay on AsteroidBehavior because
they're meant to differ between asteroids.

## Inspector settings

"Default" is what the script starts a new template with. "In the asset" is what the project's
`SpawnableTemplate.asset` is actually set to, which is what the game uses.

### Warning Timing

| Field | Default | In the asset | What it changes |
| --- | --- | --- | --- |
| `warningTime` | 1 | 1 | Seconds an asteroid waits (collider off) before it starts moving. A power-up waits the same time |

The warning used to blink a glow (`linearGlow`, `waveGlow`, `glowIntensity`, `blinkInterval` and `GetGlow`).
That was removed, so these fields are gone. The old values may still sit in the asset file until Unity next
saves it; they're ignored.

### Spin

| Field | Default | In the asset | What it changes |
| --- | --- | --- | --- |
| `spinSpeed` | 45 | 45 | How fast asteroids tumble, in degrees per second |

### Destruction

| Field | Default | In the asset | What it changes |
| --- | --- | --- | --- |
| `explosionPrefab` | none | Asteroid Explosion | Particle effect spawned when the player smashes an asteroid |
| `shakeDuration` | 0.2 | 0.2 | Seconds the camera shakes on a smash |
| `shakeMagnitude` | 0.3 | 0.3 | How far the camera shakes on a smash, in world units |

### Destructible Outline

| Field | Default | In the asset | What it changes |
| --- | --- | --- | --- |
| `outlineMaterial` | none | AsteroidOutlineMat | Material added to destructible asteroids to draw the outline |
| `outlineTooSlow` | red | red | Outline color when the player is too slow to smash it |
| `outlineCanBreak` | green | green | Outline color when the player is fast enough to smash it |

### Phase

| Field | Default | In the asset | What it changes |
| --- | --- | --- | --- |
| `phasedMaterial` | none | Asteroid Phase | The hologram look a Phase asteroid swaps to while phased (and during its flicker). See [AsteroidPhase.md](AsteroidPhase.md) |

### Despawn

| Field | Default | In the asset | What it changes |
| --- | --- | --- | --- |
| `despawnBehindCamera` | 5 | 5 | How far past the camera an asteroid goes before it's destroyed, in world units |

### Player Hit

| Field | Default | In the asset | What it changes |
| --- | --- | --- | --- |
| `knockbackForce` | 4 | 4 | How hard the player is pushed along their circle when hit |
| `speedLoss` | 3 | 3 | How much the player's `Speed` drops when hit |
| `hitShakeDuration` | 0.15 | 0.15 | Seconds the camera shakes on a hit |
| `hitShakeMagnitude` | 0.2 | 0.2 | How far the camera shakes on a hit, in world units |

### Player Speed

| Field | Default | In the asset | What it changes |
| --- | --- | --- | --- |
| `baseApproachSpeed` | 5 | 5 (the asset has no saved value until Unity re-saves it, so it uses the default) | Approach speed (units/sec) at player `Speed` 0. Shared by asteroids and power-ups, so one change tunes them all. Used to be a `speed` field on each prefab |
| `speedMultiplier` | 15 | **0.5** | Extra approach speed (units/sec) for each point of the player's `Speed`. See `ApproachSpeed` in AsteroidBehavior. Power-ups use it too, so they keep their place in a pattern |

### Depth Fade

Asteroids and power-ups get darker the further ahead of the player they are, so depth is easier to read.
Distances are measured as a share of `OrbitSettings.SpawnDistance` (the distance from the player to the
spawner), so the fade follows the spawner if it moves.

| Field | Default | What it changes |
| --- | --- | --- |
| `darkenStart` | 0.2 (slider 0–1) | Where the fade begins, as a share of the spawn distance. Closer than this = full brightness |
| `darkenEnd` | 1 (slider 0–1) | Where the fade is complete, as a share of the spawn distance. 1 = at the spawner |
| `farColor` | (0.25, 0.25, 0.3) | The color they fade toward. It's multiplied into their base color, so a dark color darkens them |

These three are new, so the asset uses the defaults until they're changed.

### Power-Up

| Field | Default | What it changes |
| --- | --- | --- |
| `powerUpSpinSpeed` | 90 | How fast power-ups spin around the world's up axis, in degrees per second. Separate from the asteroids' `spinSpeed` |
| `pickupEffectPrefab` | none | Particle effect spawned when a power-up is picked up. Currently set to Asteroid Explosion in the asset |
| `pickupShakeDuration` | 0 | Seconds the camera shakes on a pickup. 0 means no shake (PowerUp skips the call entirely, so it can't cut a running shake short) |
| `pickupShakeMagnitude` | 0 | How far the camera shakes on a pickup, in world units |
| `invincibleDuration` | 5 | Seconds of invincibility from an Invincible power-up. Passed to `PlayerInvincibility.Activate` |

### Random Power-Up Chances

| Field | Default | What it changes |
| --- | --- | --- |
| `healthChance` | 40 | How often a Random power-up gives Health |
| `invincibleChance` | 20 | How often it gives Invincible |
| `twinChance` | 20 | How often it gives Twin |
| `scoreChance` | 20 | How often it gives Score Multiplier |

These are **shares of the total**, not strict percentages: 40/20/20/20 is 40%, 20%, 20%, 20%, and 2/1/1/1
would mean the same. 0 means that type never comes up. Types that would be wasted at pickup (Health at full
lives, Twin at the cap) are left out of that roll, and the rest share their chance. Only Random power-ups use
these; the fixed prefabs ignore them. See PowerUp's `RollType`.

`despawnBehindCamera` (Despawn) is shared by asteroids and power-ups.

## How it works

### HDR colors (`[ColorUsage(false, true)]`)

The outline colors have `[ColorUsage(false, true)]` above them. It changes the color picker in
the Inspector:

- **`false`**: hides the alpha (transparency) slider, since these colors are never see-through.
- **`true`**: makes it an **HDR** color, which can be brighter than normal white. The picker gets an
  Intensity slider. Values above 1 are what make things glow when the camera has bloom turned on.

### `GetDepthTint`

Takes how far ahead of the player something is (in world units) and returns the color to multiply it by:

1. `distance ÷ OrbitSettings.SpawnDistance` turns the distance into a share of the spawn distance.
2. `InverseLerp(darkenStart, darkenEnd, share)` turns that share into 0–1: 0 at or below `darkenStart`, 1 at or
   above `darkenEnd`, a straight line between.
3. `Color.Lerp(white, farColor, t)` mixes white toward `farColor` by that amount.

With the defaults and a spawn distance of 44 (spawner z 40, player z −4): the fade starts at 8.8 units, so an
asteroid 8.8 or less ahead is white (no change). 26.4 units ahead gives `t` = 0.5, halfway between white and
`farColor`. 44 or more gives `farColor`. A passed asteroid has a negative distance, which clamps to white.

AsteroidBehavior and PowerUp call this every frame and apply the result to their materials.

## Talks to

- **AsteroidBehavior**: reads every asteroid field and calls `GetDepthTint`.
- **PowerUp**: reads `baseApproachSpeed`, `speedMultiplier`, `despawnBehindCamera` and the Power-Up section, including
  `invincibleDuration` for the Invincible type, and the four Random Power-Up Chances for a Random pickup. It also
  calls `GetDepthTint`.
- **OrbitSettings**: `GetDepthTint` reads `SpawnDistance` from it.

## Gotchas

- **The asset's values are what count, not the script's defaults.** The defaults are only used when a
  new template asset is created. Changing a default in the script doesn't change the existing asset.
  `speedMultiplier` is already different: 15 in the script, 0.5 in the asset.
- **Changes made in Play mode are kept.** Edits to GameObjects in Play mode are undone when you stop, but
  a ScriptableObject is an asset, so edits to it are saved for real. That's handy for tuning, but it's
  easy to forget and lose the old values.
- **A new `pickupEffectPrefab` field starts empty.** Until it's assigned in the asset, pickups have no effect.
- **One template changes every asteroid.** To give one group of asteroids different settings (say, a
  harder knockback), make a second template asset and assign it to just those prefabs.
- **The depth fade uses shares, not world units.** Change `spawnerPosition` or `playerZ` on OrbitSettings and the
  fade stretches to match. `darkenStart` equal to `darkenEnd` turns the fade off, because `InverseLerp` returns 0
  instead of dividing by zero.
- **An asteroid with no template is broken.** See AsteroidBehavior's gotchas.
