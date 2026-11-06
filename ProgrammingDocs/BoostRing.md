# BoostRing

## Purpose

Handles the ring on the player (and on each twin copy) for the boost. It has two jobs:

- **The cooldown glow:** it sets the Ring Glow shader's **Charge** property from the player's boost cooldown,
  so the ring is dark right after a boost, brightens as the cooldown counts down, and jumps to a full glow when
  the boost is ready.
- **The ready flash:** when the boost becomes ready, it sets the shader's **Ready Pulse** to 1 and fades it to
  0 over `readyPulseTime`, a quick bright flash on top of the ready glow.
- **The spin:** on a side boost, it rolls the ring 360° in the dodge's direction, easing with the same fade as
  the dodge itself so both finish together (a barrel roll).

## Where it lives

- **Attached to:** the `ring` object in the `hamter` prefab and in the `Twin Copy` prefab.
- **Requires:**
  - A Renderer on the same object using the **Ring Glow** material (see [RingGlow.md](RingGlow.md)).
  - A `PlayerMovement` on a parent (the player), or, on a twin copy, `GameManager.Instance.Player`.

## Inspector settings

| Field | Default | In the prefabs | What it changes |
| --- | --- | --- | --- |
| `spinAxis` | (0, 0, 1) | (0, 0, −1) | The ring's local axis to roll around. Its sign sets which way the ring rolls for a given dodge |
| `readyPulseTime` | 0.5 | 0.5 | Seconds the ring's flash takes to fade out after the boost becomes ready. 0 = no flash |

## How it works

### `Awake`: assigning references

Runs once, when the ring loads. It assigns the references it uses every frame, so nothing is looked up again:

- **`movement`**: `GetComponentInParent<PlayerMovement>()` searches the ring's object and then each parent
  upward, which finds the player's PlayerMovement on the player's ring. A twin copy has no PlayerMovement above
  it, so the search returns null and it falls back to `GameManager.Instance.Player`, the same way ExhaustFlame
  does on copies. So every ring follows the **player's** boosts and cooldown.
- **`ringRenderer`** (the Renderer on this object) and **`props`**, a `MaterialPropertyBlock` made once and
  reused.
- **`restRotation`**: not a reference but a saved copy of the ring's starting local rotation. Every spin turns
  from it, so the ring always ends exactly where it started.

### `OnEnable` / `OnDisable`: subscribing

`OnEnable` subscribes `StartSpin` to the player's `OnSideBoost` event and `StartPulse` to its `OnBoostReady`
event; `OnDisable` unsubscribes both. The unsubscribe matters here: a twin copy's ring is subscribed to the
player's events, and copies are destroyed when hit while the player lives on. Without it, the player's events
would keep calling into a destroyed ring.

### `StartPulse`: starting the ready flash

Called by `OnBoostReady` on the frame the cooldown finishes. It sets `pulseTimer` to `readyPulseTime`; `UpdateGlow`
does the fading. Every ring (the player's and each copy's) subscribes, so they all flash together.

### `StartSpin`: starting the spin

Called by `OnSideBoost` with the dodge's direction (+1 counter-clockwise, −1 clockwise). It starts the spin
timer (`spinTime = 0`) and records `spinDirection`. A non-zero direction is what marks a spin as running. The
event can't run code over many frames, so this only sets the starting state; `UpdateSpin` does the turning.

A new side boost mid-spin restarts the spin: the angle is worked out fresh from `spinTime` (see below), so
resetting it snaps the ring back to its rest rotation before it rolls again. With the 3 s cooldown that only
happens if `boostDecay` is below about 1.5.

### `Update`

Calls `UpdateSpin`, then `UpdateGlow`.

### `UpdateSpin`: advancing the spin every frame

Does nothing unless a spin is running. Otherwise:

1. `spinTime += Time.deltaTime`.
2. `share = 1 − e^(−BoostDecay × spinTime)`: the share of the dodge's distance covered so far, the same fade
   PlayerMovement applies to `boostVelocity`, read live so retuning `boostDecay` keeps them matched.
3. At 99% it rounds up to exactly 1 (the fade only approaches 100%). The last jump is 3.6°, too small to see.
4. `transform.localRotation = restRotation * AngleAxis(share × 360° × spinDirection, spinAxis)`. Multiplying
   applies the turn on top of the rest pose, around the ring's **own** `spinAxis`. A negative direction rolls
   the other way.
5. At 360° the ring is back at its rest pose, so the spin ends (`spinDirection = 0`) with no jump.

With `boostDecay` 3:

| Time | `share` | Dodge right (+1) | Dodge left (−1) |
| --- | --- | --- | --- |
| 0.1 s | 0.26 | +93° | −93° |
| 0.25 s | 0.53 | +190° | −190° |
| 0.5 s | 0.78 | +279° | −279° |
| ~1.5 s | 1 | +360° (rest) | −360° (rest) |

Pausing (`Time.deltaTime` 0) freezes a spin mid-roll.

### `UpdateGlow`: sending Charge and the ready flash to the shader

Reads `movement.BoostCharge01` and works out this frame's flash. Then, **only if either changed** since last
sent (`shownCharge`, `shownPulse`), it sends both to the shader: `GetPropertyBlock` reads the renderer's block
into `props`, `SetFloat(ChargeId, charge)` sets `_Charge`, `SetFloat(PulseId, pulse)` sets `_Ready_Pulse`, and
`SetPropertyBlock` hands it back. `ChargeId` and `PulseId` are `Shader.PropertyToID(...)`, worked out once.

**The flash.** If `charge` is below 1 the flash is cancelled (`pulseTimer = 0`), because it only belongs to a ready
boost: boosting again mid-flash would otherwise make a dark ring flash. While `pulseTimer` is above 0 it counts
down, and `pulse = t²` where `t = pulseTimer ÷ readyPulseTime`. Squaring makes it drop away fast after the first
flash. With `readyPulseTime` 0.5:

| Time since ready | `t` | `pulse` |
| --- | --- | --- |
| 0 s | 1 | 1 |
| 0.1 s | 0.8 | 0.64 |
| 0.25 s | 0.5 | 0.25 |
| 0.5 s | 0 | 0 |

During a 3 s cooldown the charge changes every frame, so it's sent about 180 times at 60 fps. Once ready it
sits at exactly 1 and is only re-sent while the flash fades, then not again until the next boost. `shownCharge`
and `shownPulse` start at −1, values no real one can have, so the first frame always sends.

**Why a property block:** it sets a value for one renderer on top of the shared material, without copying or
changing the material. `renderer.material` would quietly make a copy that must be destroyed in `OnDestroy`. A
property block isn't a Unity object, so there's nothing to clean up.

## Talks to

- **PlayerMovement**: subscribes to `OnSideBoost` and `OnBoostReady`; reads `BoostDecay` (spin) and
  `BoostCharge01` (glow) every frame.
- **GameManager**: gets `Player` in `Awake` on a twin copy's ring.
- **Ring Glow shader**: sets `_Charge` and `_Ready_Pulse` through the property block.

## Gotchas

- **The ring must use the Ring Glow material.** On another material, `_Charge` and `_Ready_Pulse` don't exist and
  nothing shows.
- **Keep the references `_Ready_Pulse` and `_Charge` in the graph.** The names are text, so renaming one in the
  graph silently stops it from being set.
- **Keep the unsubscribe in `OnDisable`,** or destroyed twin copies stay subscribed to the player's event.
- **Wrong roll direction:** flip `spinAxis` (e.g. (0, 0, 1) ↔ (0, 0, −1)) on both prefabs. Wrong axis entirely
  (it flips instead of rolling): use the ring model's own centre axis.
- **The ring glows at full on the menu,** because the boost counts as ready there.
- **Knockback doesn't spin the ring.** It sets `boostVelocity` in `TakeHit`, not through a boost, so no
  `OnSideBoost` is raised.
