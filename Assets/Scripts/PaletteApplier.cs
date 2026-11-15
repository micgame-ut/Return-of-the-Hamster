using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Switches the game's colours to a ColorPalette: the material colours listed in PaletteTargets, plus the scene objects that listen.
public class PaletteApplier : MonoBehaviour
{
    private const bool Enabled = true;   // set to false to leave palettes out of a build

    private ColorPalette[] palettes;     // from Resources/Palettes: the original one first, then by name
    private PaletteTargets targets;      // from Resources/PaletteTargets
    private int index;                   // which palette is on now

    // What each colour was before any palette changed it, read once. Neon Arcade and quitting put these back.
    private readonly Dictionary<PaletteTargets.Entry, Color> originalColors = new Dictionary<PaletteTargets.Entry, Color>();
    private readonly Dictionary<PaletteTargets.Entry, Color> originalEmissions = new Dictionary<PaletteTargets.Entry, Color>();

    private SpawnableTemplate template;   // from Resources: holds the rock outline colours, the far fade and the Lance beam
    private Color originalTooSlow, originalCanBreak, originalFar, originalBeam;

    // What each text's colour and each button's selected colour was before a palette changed it.
    private readonly Dictionary<TMP_Text, Color> originalText = new Dictionary<TMP_Text, Color>();
    private readonly Dictionary<Button, Color> originalSelected = new Dictionary<Button, Color>();

    // Makes the applier once, as the game starts, so no scene needs an object for it.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Create()
    {
        if (!Enabled) return;
        DontDestroyOnLoad(new GameObject("PaletteApplier", typeof(PaletteApplier)));
    }

    private void Awake()
    {
        palettes = Resources.LoadAll<ColorPalette>("Palettes");
        targets = Resources.Load<PaletteTargets>("PaletteTargets");

        // Without both, there's nothing to switch, so the applier switches itself off.
        if (palettes.Length == 0 || targets == null) { enabled = false; return; }

        System.Array.Sort(palettes, (a, b) =>
        {
            if (a.isOriginal != b.isOriginal) return a.isOriginal ? -1 : 1;   // the original palette first
            return string.Compare(a.displayName, b.displayName, System.StringComparison.Ordinal);
        });

        CaptureOriginals();
        CaptureTemplate();
        Instance = this;
    }

    public static PaletteApplier Instance { get; private set; }

    public string CurrentName => palettes[index].displayName;

    private int appliedIndex = -1;       // which palette is applied now, so asking for the same one again does nothing

    public void Next() => Select(index + 1);
    public void Previous() => Select(index - 1);

    // Switches to the palette at this position, wrapping round at either end.
    public void Select(int newIndex)
    {
        index = (newIndex % palettes.Length + palettes.Length) % palettes.Length;
        Apply(palettes[index]);
        appliedIndex = index;
    }

    // Switches to the palette with this name, if it isn't on already. An unknown or empty name gives the first (original) palette.
    public void SelectByName(string paletteName)
    {
        int found = System.Array.FindIndex(palettes, p => p.displayName == paletteName);
        int target = found >= 0 ? found : 0;
        if (target != appliedIndex) Select(target);
    }

    // The palette that's on now (null until one is chosen), and an event for objects that keep their own colours.
    public static ColorPalette Current { get; private set; }
    public static event System.Action<ColorPalette> OnChanged;

    private void Apply(ColorPalette palette)
    {
        ApplyMaterials(palette);
        ApplyTemplate(palette);
        ApplyUi(palette);
        Current = palette;
        OnChanged?.Invoke(palette);
    }

    private void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
    private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

    // A new scene's text and buttons start with their own colours, so the chosen palette is put on them.
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (Current != null) ApplyUi(Current);
    }

    // The materials are assets, so in the Editor they keep their changes after Play stops. Put the originals back.
    private void OnApplicationQuit()
    {
        foreach (KeyValuePair<PaletteTargets.Entry, Color> pair in originalColors)
            pair.Key.material.SetColor(pair.Key.property, pair.Value);
        foreach (KeyValuePair<PaletteTargets.Entry, Color> pair in originalEmissions)
            pair.Key.material.SetColor(pair.Key.emissionProperty, pair.Value);

        if (template != null)
        {
            template.outlineTooSlow = originalTooSlow;
            template.outlineCanBreak = originalCanBreak;
            template.farColor = originalFar;
            template.lanceBeamColor = originalBeam;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // White text follows the palette's text colour, and the coloured "selected" look of the end-screen buttons follows its accent.
    private void ApplyUi(ColorPalette palette)
    {
        foreach (TMP_Text t in FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!originalText.ContainsKey(t)) originalText[t] = t.color;
            Color original = originalText[t];
            if (original.r < 0.99f || original.g < 0.99f || original.b < 0.99f) continue;   // only white text follows

            Color c = palette.isOriginal ? original : palette.text;
            c.a = original.a;
            t.color = c;
        }

        foreach (Button b in FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!originalSelected.ContainsKey(b)) originalSelected[b] = b.colors.selectedColor;
            Color original = originalSelected[b];

            Color.RGBToHSV(original, out float hue, out float sat, out float val);
            if (original.a < 0.5f || sat < 0.2f) continue;   // see-through and grey looks are left alone

            Color accent = palette.isOriginal ? original : palette.uiAccent;
            accent.a = original.a;
            ColorBlock block = b.colors;
            block.selectedColor = accent;
            b.colors = block;
        }
    }

    private void CaptureTemplate()
    {
        template = Resources.Load<SpawnableTemplate>("SpawnableTemplate");
        if (template == null) return;

        originalTooSlow = template.outlineTooSlow;
        originalCanBreak = template.outlineCanBreak;
        originalFar = template.farColor;
        originalBeam = template.lanceBeamColor;
    }

    private void ApplyTemplate(ColorPalette palette)
    {
        if (template == null) return;

        bool own = palette.isOriginal;
        template.outlineTooSlow = own ? originalTooSlow : palette.danger;
        template.outlineCanBreak = own ? originalCanBreak : palette.breakable;
        template.farColor = own ? originalFar : Color.Lerp(palette.space, palette.rock, 0.3f);
        template.lanceBeamColor = own ? originalBeam : ColorPalette.AtStrength(palette.lance, originalBeam.maxColorComponent);
    }

    // Reads every listed colour as it is now, before any palette changes it.
    private void CaptureOriginals()
    {
        foreach (PaletteTargets.Entry e in targets.materials)
        {
            if (e.material == null || !e.material.HasProperty(e.property)) continue;

            originalColors[e] = e.material.GetColor(e.property);
            if (!string.IsNullOrEmpty(e.emissionProperty) && e.material.HasProperty(e.emissionProperty))
                originalEmissions[e] = e.material.GetColor(e.emissionProperty);
        }
    }

    // The colour an entry gets from a palette: its role (blended toward role2), shifted in hue and brightness.
    // A glowing original (brighter than 1) keeps its strength and only takes the palette's hue.
    private Color ColorFor(PaletteTargets.Entry e, ColorPalette palette)
    {
        Color c = palette.Get(e.role);
        if (e.role2 != PaletteRole.None) c = Color.Lerp(c, palette.Get(e.role2), e.blend);

        Color.RGBToHSV(c, out float hue, out float sat, out float val);
        c = Color.HSVToRGB(Mathf.Repeat(hue + e.hueShift, 1f), sat, Mathf.Clamp01(val * e.brightness));

        float glow = originalColors[e].maxColorComponent;
        if (glow > 1.001f) c = c / Mathf.Max(c.maxColorComponent, 0.001f) * glow;

        c.a = originalColors[e].a;
        return c;
    }

    // SkyboxSpeedController swaps the skybox for a clone of the sky material when a level starts,
    // and the clone is what's drawn. Returns it if m is the material it was cloned from.
    private static Material LiveCopy(Material m)
    {
        Material sky = RenderSettings.skybox;
        return sky != null && sky.name == m.name + "(Clone)" ? sky : null;
    }

    // Sets every listed material colour from the palette, or back to its original for the original palette.
    private void ApplyMaterials(ColorPalette palette)
    {
        foreach (PaletteTargets.Entry e in targets.materials)
        {
            if (!originalColors.ContainsKey(e)) continue;   // a missing material or property was left out when capturing

            bool useOriginal = palette.isOriginal || e.role == PaletteRole.None;
            Color c = useOriginal ? originalColors[e] : ColorFor(e, palette);
            e.material.SetColor(e.property, c);

            Material live = LiveCopy(e.material);
            if (live != null) live.SetColor(e.property, c);

            if (!originalEmissions.ContainsKey(e)) continue;

            // The glow keeps its original strength; only its hue follows the new colour.
            Color glow = originalEmissions[e];
            if (!useOriginal)
            {
                float strength = glow.maxColorComponent;
                glow = c / Mathf.Max(c.maxColorComponent, 0.001f) * strength;
                glow.a = 1f;
            }
            e.material.SetColor(e.emissionProperty, glow);
        }
    }
}
