# MultiplierDisplay

## Purpose

Handles the display of the score power-ups' part of the multiplier (`x2`, `x4`…) next to a bar showing the
time left on the pickup that runs out next. Hidden while no score power-ups are active.

It shows only the power-up part, not the total: at max speed with one pickup the HUD shows `x2`, while
popups show the full `x4.0`.

## Where it lives

- **Attached to:** the HUD canvas object, which is always active. Not on the indicator itself (see Gotchas).
- **Requires:** a GameManager with `Score`, and in the HUD:

```
HUD Canvas                  ← MultiplierDisplay
└── Multiplier Indicator    ← indicator (empty parent)
    ├── Multiplier Label    ← label (Text - TextMeshPro)
    ├── Bar Background      ← dark Image, Simple, drawn behind
    └── Multiplier Bar      ← bar (Image, Filled, Horizontal)
```

## Inspector settings

| Field | Default | What it changes |
| --- | --- | --- |
| `indicator` | (none) | The label and bar's parent. Switched on and off as a whole |
| `label` | (none) | Shows the power-up multiplier, for example `x4` |
| `bar` | (none) | An Image with Image Type set to Filled. Its `fillAmount` drains from 1 to 0 |

## How it works

### `Awake`

Gets the reference to the `Score` component from GameManager. It's a reference to the component, not the
score value, so `Update` can read live values every frame.

Then it hides `indicator`. On Level 1 the HUD (and this script with it) is switched off during the menu, so
nothing runs until GameManager's `StartGame` switches the HUD on. The indicator is switched on in the scene,
so without this it would show for a frame before `Update` hid it. A script on a switched-off object gets its
`Awake` the moment the object is switched on, before anything is drawn, so hiding it here prevents the flash.

### `Update`

1. **Read how many score power-ups are active** (`Score.ActivePowerUps`, 0 without a Score).
2. **Only when that count changes** (compared with `shownCount`, which starts at -1 so the first frame
   always updates): show or hide `indicator`, and rebuild the label as `x` + `Score.PowerUpMultiplier`
   formatted with `0.#` (2 → `x2`, 2.5 → `x2.5`). Rebuilding only on a change means no string garbage
   per frame.
3. **Every frame while any are active:** set the bar's `fillAmount` to `Score.NextPowerUpFraction`.

| Time | Event | Label | Bar |
| --- | --- | --- | --- |
| 0 s | 1st pickup | `x2` | 1.0, draining |
| 3 s | 2nd pickup | `x4` | 0.7 (still the 1st), draining |
| 10 s | 1st runs out | `x2` | jumps to 0.7 (the 2nd), draining |
| 13 s | 2nd runs out | hidden | hidden |

The bar follows the pickup that runs out next, because that's when the number drops. Score's timers use
scaled time, so the bar freezes during the game-over freeze.

## Talks to

- **Score**: reads `ActivePowerUps`, `PowerUpMultiplier` and `NextPowerUpFraction`.
- **GameManager**: gets `Score` in `Awake`.

## Gotchas

- **Don't put it on the indicator object.** Hiding the indicator would switch off this script too, and its
  `Update` would never run again to show it on the next pickup.
- **The bar needs Image Type set to Filled.** With Simple, `fillAmount` does nothing and the bar never
  drains.
- **Any field left empty is skipped**, so a missing label or bar just doesn't show, with no errors.
