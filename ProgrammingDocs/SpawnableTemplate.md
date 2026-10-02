# SpawnableTemplate

## Purpose

Holds the settings shared by everything the spawner sends at the player, asteroids and power-ups:
warning glow colors and timing, spin, explosion, outline colors, despawn distance, what happens when the
player gets hit, how much the player's speed affects approach speed, and the power-up spin and pickup
effect. Every asteroid and power-up prefab points at the same template asset, so changing a value there
changes it for all of them at once.

It was called `AsteroidTemplate` until power-ups started using it. Asteroid-only settings and power-up-only
settings sit in their own sections; `speedMultiplier` and `despawnBehindCamera` are shared by both.

It's only data. It has no `Update` or other logic, apart from one helper that picks the glow color.

## Where it lives

- **Attached to:** nothing. It's a **ScriptableObject**, which is saved as an asset file in the project
  instead of being attached to a GameObject.
- **The asset:** `Assets/Scripts/SpawnableTemplate.asset`.
- **Used by:** the `template` field on every asteroid prefab in `Assets/Prefabs/Asteroid Types/`, and on
  power-up prefabs (`Assets/Prefabs/Health PowerUp.prefab`).
- **Making a new one:** right-click in the Project window → Create → Return of the Hamster → Spawnable
  Template. The `[CreateAssetMenu]` line at the top of the script adds that menu option.

### Why a ScriptableObject

The other option would be putting all these fields on AsteroidBehavior itself. Then every asteroid would
carry its own copy, and changing the knockback would mean editing every prefab and making sure they
match. With a ScriptableObject, there's one copy of the settings and every asteroid just points to it.

The per-asteroid settings (movement type, speed, wave, destructible) stay on AsteroidBehavior because
they're meant to differ between asteroids.

## Inspector settings

"Default" is what the script starts a new template with. "In the asset" is what the project's
`SpawnableTemplate.asset` is actually set to, which is what the game uses.

### Warning Glow

| Field | Default | In the asset | What it changes |
| --- | --- | --- | --- |
| `linearGlow` | red | red | Glow color for Linear asteroids during the warning |
| `waveGlow` | cyan | cyan | Glow color for Wave asteroids during the warning |
| `glowIntensity` | 3 | 3 | Brightness multiplier on both glow colors |

### Warning Timing

| Field | Default | In the asset | What it changes |
| --- | --- | --- | --- |
| `warningTime` | 1 | 1 | Seconds an asteroid blinks before it starts moving |
| `blinkInterval` | 0.1 | 0.1 | Seconds between each blink on/off |

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
| `speedMultiplier` | 15 | **0.5** | Extra approach speed (units/sec) for each point of the player's `Speed`. See `ApproachSpeed` in AsteroidBehavior. Power-ups use it too, so they keep their place in a pattern |

### Power-Up

| Field | Default | What it changes |
| --- | --- | --- |
| `powerUpSpinSpeed` | 90 | How fast power-ups spin around the world's up axis, in degrees per second. Separate from the asteroids' `spinSpeed` |
| `pickupEffectPrefab` | none | Particle effect spawned when a power-up is picked up. Currently set to Asteroid Explosion in the asset |
| `pickupShakeDuration` | 0 | Seconds the camera shakes on a pickup. 0 means no shake (PowerUp skips the call entirely, so it can't cut a running shake short) |
| `pickupShakeMagnitude` | 0 | How far the camera shakes on a pickup, in world units |

`despawnBehindCamera` (Despawn) is shared by asteroids and power-ups.

## How it works

### HDR colors (`[ColorUsage(false, true)]`)

The glow and outline colors have `[ColorUsage(false, true)]` above them. It changes the color picker in
the Inspector:

- **`false`**: hides the alpha (transparency) slider, since these colors are never see-through.
- **`true`**: makes it an **HDR** color, which can be brighter than normal white. The picker gets an
  Intensity slider. Values above 1 are what make things glow when the camera has bloom turned on.

### `GetGlow`

Takes an asteroid's movement type and returns its glow color:

1. The `switch` picks `waveGlow` for Wave. Anything else, including Linear, gets `linearGlow`.
2. The result is multiplied by `glowIntensity`. Multiplying a color by a number scales its brightness,
   so 3 makes it three times as bright. This is what makes it bright enough to glow.

AsteroidBehavior calls this during the warning blink.

## Talks to

- **AsteroidBehavior**: reads every asteroid field and calls `GetGlow`.
- **PowerUp**: reads `speedMultiplier`, `despawnBehindCamera` and the Power-Up section.

The template doesn't read from or call anything itself.

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
- **Adding a movement type needs a glow color here too.** A new type with no case in `GetGlow` quietly
  glows the Linear color.
- **An asteroid with no template is broken.** See AsteroidBehavior's gotchas.
