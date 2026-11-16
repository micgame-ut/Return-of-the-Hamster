# PaletteTargets

## Purpose

The list of material colours that follow a palette. Each entry says which material and which shader property, and which
palette role it follows. `PaletteApplier` goes through the list when a palette is chosen. Keeping it as data means you can
open it and see "Health_Green's `_BaseColor` follows Health", and change a mapping without code.

## Where it lives

- **It's a ScriptableObject:** `Assets/Resources/PaletteTargets.asset` (in `Resources`, so it's loaded at runtime and its
  material references are included in a build). Make a new one with **Create → Hamster → Palette Targets**.
- **Requires:** the materials it points at.

## Inspector settings (one `Entry`)

| Field | Default | What it changes |
| --- | --- | --- |
| `material` | none | The material whose colour follows the palette |
| `property` | `_BaseColor` | The shader property to set, for example `_BaseColor`, `_OutlineColor` or `_Cloud_Color_Mid` |
| `role` | None | The palette role the colour follows. None = the entry does nothing |
| `role2` | None | A second role to blend toward. None = no blend |
| `blend` | 0 (0–1) | How far to blend from `role` toward `role2` |
| `brightness` | 1 | Multiplies the colour's brightness. 0.8 = a bit darker |
| `hueShift` | 0 (−0.5 to 0.5) | Shifts the hue, as a share of the colour wheel. Used to make the rock colours differ |
| `emissionProperty` | empty | An emission property to set along with this colour, for example `_EmissionColor`. It keeps the original glow strength |

## The 56 entries

| Group | What follows what |
| --- | --- |
| Sky (`StarrySkyVolumeMat`, 18) | `_SpaceColor` ← Space; `_Cloud_Color_*` ← NebDeep / NebMid / NebBright / NebCore; `_Alt_Color_*` ← Phased toward Space, Phased, Danger toward NebMid, NebCore toward NebMid; `_Core_Glow_Color` ← NebCore × 0.85; `_Stream_Color_1..4` ← NebBright × 0.7, NebBright, Player × 0.5, NebDeep toward NebMid; `_Sparkle_Color_1..4` ← Health, Danger, Player, Lance |
| Rocks (6) | `Asteroid Rock Gray` ← Rock; Gray 1 / 2 / 3 / 4 ← Rock with brightness 0.82 / 0.6 / 0.8 / 0.68 and hue shift +0.07 / +0.04 / −0.08 / −0.18; `Asteroid` ← Rock × 0.78 |
| Phase rock (1) | `Asteroid Phase` `_Holo_Color` ← Phased |
| Player ring (8) | `Ring Glow` and `Ring Glow Small`: `_Base_Color` ← Player × 0.3; `_Glow_Color` and `_Ring_1_Color` ← Player; `_Ring_2_Color` ← Player halfway to Ring2 |
| Shields (2) | `RainbowOverlayMat` and `ShieldOverlayMat` `_Shield_Color` ← Phased |
| Mystery star (6) | `Mystery Star` `_StarA` / `_StarB` / `_StarC` ← StarA / StarB / StarC. The explosion's three materials `Mystery_Star_Particle` / `_B` / `_C` `_BaseColor` ← StarA / StarB / StarC (they are glowing HDR, so they keep their strength and only take the palette's hue) |
| Power-up bodies (11) | Health_Green and Health_Plus_Particle ← Health; Mystery_Gold (no longer on the Mystery prefab), Jackpot Gold and Gold (× 0.75) ← Wild; Slim Violet ← Slim; Bomb Spark ← Ring2; Lance Pink ← Lance; Boost Recharge Orange ← NebBright; Warp Lime ← Warp. Those with a glow have `_EmissionColor` too |
| Pickup popups (10) | `_OutlineColor` of Popup Health / Jackpot / Lance / Slim / Warp / Bomb / Recharge / Twin / Invincible / Score ← Health / Wild / Lance / Slim / Warp / Ring2 / NebBright / Player / Phased / NebMid |

## How it works

It holds data only. `PaletteApplier` reads each entry's original colour once, then sets the entry's property to the
palette's colour for the entry's role: blended toward `role2` by `blend`, shifted by `hueShift`, multiplied by
`brightness`. A colour whose original is brighter than 1 (a glow) keeps that strength and only takes the hue.

## Talks to

- **PaletteApplier**: goes through `materials`.
- **ColorPalette** and **PaletteRole**: the entry's roles are answered by `ColorPalette.Get`.

## Gotchas

- **A wrong `property` name is skipped,** not an error. The applier only uses entries where the material has that property.
- **Entries point at the material assets themselves.** In the Editor their colours change while a palette is on, and are
  put back when you stop Play.
- **Not in the list:** the black hole, the frost materials, the planet atmosphere, `Bomb Body`, `Lance Beam` (white), the
  exhaust flame gradients and the particle bursts on pickup. Add an entry to bring a material in.
- **Twin has no material of its own,** so only its popup follows the palette.
