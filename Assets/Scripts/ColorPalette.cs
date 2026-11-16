using UnityEngine;

// One colour palette: a named set of roles. PaletteApplier pushes these colours into the game's materials, scene colours and UI.
[CreateAssetMenu(fileName = "Palette", menuName = "Hamster/Color Palette")]
public class ColorPalette : ScriptableObject
{
    [Tooltip("The name shown in the Tab overlay.")]
    public string displayName = "Palette";

    [Tooltip("Tick this on the palette that is the game's own colours. The applier then puts back the colours it captured at the start, and the colours below are ignored.")]
    public bool isOriginal;

    [Header("Space")]
    [Tooltip("The sky colour between the clouds.")]
    public Color space = new Color32(0x10, 0x0C, 0x1B, 255);
    [Tooltip("The darkest nebula cloud colour.")]
    public Color nebDeep = new Color32(0x3C, 0x14, 0x5A, 255);
    [Tooltip("The middle nebula cloud colour.")]
    public Color nebMid = new Color32(0xC8, 0x3C, 0x82, 255);
    [Tooltip("The bright nebula cloud colour.")]
    public Color nebBright = new Color32(0xFF, 0x82, 0x32, 255);
    [Tooltip("The hot centre of the nebula.")]
    public Color nebCore = new Color32(0xFF, 0xE1, 0xAA, 255);

    [Header("Hazards")]
    [Tooltip("The colour that marks a rock you can't smash: its outline and its place on the orbit ring.")]
    public Color danger = new Color32(0xFF, 0x33, 0x33, 255);
    [Tooltip("The colour that marks a rock you can smash.")]
    public Color breakable = new Color32(0x33, 0xFF, 0x4C, 255);
    [Tooltip("The colour that marks a phased rock, which you can pass through.")]
    public Color phased = new Color32(0x33, 0x80, 0xFF, 255);
    [Tooltip("A neutral grey that fits the palette. The five rock colours are small lightness and hue variations of it, and the far-distance fade is a blend of it and the sky.")]
    public Color rock = new Color32(0x78, 0x78, 0x78, 255);

    [Header("Player")]
    [Tooltip("The glow of the ship's boost ring.")]
    public Color player = new Color32(0x00, 0xE6, 0xFF, 255);
    [Tooltip("The second, accent ring on the ship.")]
    public Color ring2 = new Color32(0xFF, 0xD0, 0x40, 255);

    [Header("Power-ups")]
    [Tooltip("The Health power-up.")]
    public Color health = new Color32(0x19, 0xFF, 0x40, 255);
    [Tooltip("The Jackpot and Mystery power-ups, and the gold Jackpot rocks.")]
    public Color wild = new Color32(0xFF, 0xD9, 0x19, 255);
    [Tooltip("The Lance power-up and its beam.")]
    public Color lance = new Color32(0xFF, 0x19, 0x99, 255);
    [Tooltip("The Warp power-up.")]
    public Color warp = new Color32(0xB2, 0xFF, 0x19, 255);
    [Tooltip("The Slim power-up.")]
    public Color slim = new Color32(0x99, 0x4C, 0xFF, 255);
    [Tooltip("The first of the three colours the Mystery star and its explosion blend through.")]
    public Color starA = new Color32(0xFF, 0x59, 0xCC, 255);
    [Tooltip("The second of the three colours the Mystery star and its explosion blend through.")]
    public Color starB = new Color32(0xFF, 0xF2, 0x59, 255);
    [Tooltip("The third of the three colours the Mystery star and its explosion blend through.")]
    public Color starC = new Color32(0x59, 0xE6, 0xFF, 255);

    [Header("UI")]
    [Tooltip("The highlight colour on the UI: the selected button and the leaderboard highlight.")]
    public Color uiAccent = new Color32(0xDB, 0xD4, 0x44, 255);
    [Tooltip("The main text colour.")]
    public Color text = new Color32(0xFF, 0xFF, 0xFF, 255);

    // The palette's colour for a role. None, or a role without a colour, gives white.
    public Color Get(PaletteRole role) => role switch
    {
        PaletteRole.Space => space,
        PaletteRole.NebDeep => nebDeep,
        PaletteRole.NebMid => nebMid,
        PaletteRole.NebBright => nebBright,
        PaletteRole.NebCore => nebCore,
        PaletteRole.Danger => danger,
        PaletteRole.Breakable => breakable,
        PaletteRole.Phased => phased,
        PaletteRole.Rock => rock,
        PaletteRole.Player => player,
        PaletteRole.Ring2 => ring2,
        PaletteRole.Health => health,
        PaletteRole.Wild => wild,
        PaletteRole.Lance => lance,
        PaletteRole.Warp => warp,
        PaletteRole.Slim => slim,
        PaletteRole.StarA => starA,
        PaletteRole.StarB => starB,
        PaletteRole.StarC => starC,
        PaletteRole.UiAccent => uiAccent,
        PaletteRole.Text => text,
        _ => Color.white,
    };

    // The colour's hue, with its brightest channel set to strength. Glow colours are above 1 so Bloom picks them up.
    public static Color AtStrength(Color c, float strength)
    {
        Color result = c / Mathf.Max(c.maxColorComponent, 0.001f) * strength;
        result.a = 1f;
        return result;
    }
}
