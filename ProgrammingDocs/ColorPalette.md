# ColorPalette

## Purpose

One colour palette: a named set of 18 colours, one for each role in the game's colour language (the sky, the hazard
colours, the player's glow, the power-ups, the UI accents). `PaletteApplier` pushes the colours of the chosen palette into
the game.

## Where it lives

- **It's a ScriptableObject** (a data asset, not attached to any object). Make one with **Create → Hamster → Color Palette**.
- **The six palettes** are in `Assets/Resources/Palettes` (they have to be in a `Resources` folder so the game can load them
  at runtime, also in a build): Neon Arcade, Retro Futuristic, Hail Mary, Colour-blind safe, Amber Terminal, Synthwave.
- **Requires:** nothing. `PaletteApplier` loads every palette in that folder.

## Inspector settings

| Field | Default | What it changes |
| --- | --- | --- |
| `displayName` | Palette | The name shown in the Tab overlay, and the name saved in `MovementTuning.json` |
| `isOriginal` | off | Tick it on the palette that is the game's own colours. The applier then puts back the colours it captured at the start and ignores the colours below. Only Neon Arcade has it on |
| `space` | `#100C1B` | The sky colour between the clouds |
| `nebDeep` / `nebMid` / `nebBright` / `nebCore` | `#3C145A` / `#C83C82` / `#FF8232` / `#FFE1AA` | The nebula's cloud colours, from the darkest to the hot centre |
| `danger` | `#FF3333` | A rock you can't smash: its outline and its place on the orbit ring |
| `breakable` | `#33FF4C` | A rock you can smash |
| `phased` | `#3380FF` | A phased rock, which you can pass through |
| `rock` | `#787878` | A neutral that fits the palette. The five rock colours are small lightness and hue variations of it, and the far-distance fade is a blend of it and `space` |
| `player` | `#00E6FF` | The glow of the ship's boost ring |
| `ring2` | `#FFD040` | The second, accent ring on the ship |
| `health` / `wild` / `lance` / `warp` / `slim` | `#19FF40` / `#FFD919` / `#FF1999` / `#B2FF19` / `#994CFF` | The Health, Jackpot (and Mystery), Lance, Warp and Slim power-ups |
| `starA` / `starB` / `starC` | `#FF59CC` / `#FFF259` / `#59E6FF` | The three colours the Mystery star and its explosion blend through (A → B → C → back to A). Pink, yellow and cyan blend through orange, green and purple, which reads as a rainbow. The other palettes pick their own three (for example Amber Terminal uses three ambers) |
| `uiAccent` | `#DBD444` | The highlight colour on the UI: the selected end-screen button, the leaderboard highlight, the overdrive flash |
| `text` | `#FFFFFF` | The main (white) text colour |

The defaults are Neon Arcade's values, so a new palette starts as the game's own colours.

## How it works

### `Get(role)`

Returns the palette's colour for a `PaletteRole`. It's a `switch` expression with one line per role, and `None` (or any
role without a colour) gives white. `PaletteTargets` entries use it to ask "what does this palette have for Danger?".

### `AtStrength(c, strength)` (static)

Returns the colour's hue with its brightest channel set to `strength`: `c ÷ brightest channel × strength`, with alpha 1.
It's how glow colours (brighter than 1, which Bloom makes shine) keep their strength when only the hue changes. Example:
a pink `(1, 0.2, 0.6)` at strength 3 becomes `(3, 0.6, 1.8)`.

## Talks to

- **PaletteApplier**: reads every palette, and calls `Get` and `AtStrength`.
- **PaletteTargets**: its entries say which role a material colour follows, and `Get` answers.
- **BoostRing**: calls `AtStrength` for the Lance ring colours.

## Gotchas

- **Five power-ups have no role of their own** (Bomb, Recharge, Twin, Invincible, Score). Their colours are taken from
  other roles by `PaletteTargets` entries: Bomb from `ring2`, Recharge from `nebBright`, Twin from `player`, Invincible from
  `phased`, Score from `nebMid`.
- **`isOriginal` ignores the colours.** Editing Neon Arcade's colours does nothing, because the applier restores what the game
  had at the start. To change the game's own colours, change the materials and scripts themselves.
- **A new palette must be in `Assets/Resources/Palettes`** to show up. Palettes are ordered with the original first, then by name.
- **Colours are stored as the Inspector shows them (sRGB hex).** Pasting a hex from the swatch page gives the same colour.
