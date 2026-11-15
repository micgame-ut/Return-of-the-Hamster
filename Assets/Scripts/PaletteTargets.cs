using System.Collections.Generic;
using UnityEngine;

// The list of material colours that follow a palette. PaletteApplier goes through it.
[CreateAssetMenu(fileName = "PaletteTargets", menuName = "Hamster/Palette Targets")]
public class PaletteTargets : ScriptableObject
{
    [System.Serializable]
    public class Entry
    {
        [Tooltip("The material whose colour follows the palette.")]
        public Material material;
        [Tooltip("The shader property to set, for example _BaseColor.")]
        public string property = "_BaseColor";
        [Tooltip("The palette role this colour follows. None = the entry does nothing.")]
        public PaletteRole role;
        [Tooltip("A second role to blend toward. None = no blend.")]
        public PaletteRole role2;
        [Tooltip("How far to blend from the first role toward the second: 0 = the first role, 1 = the second.")]
        [Range(0f, 1f)] public float blend;
        [Tooltip("Multiplies the colour's brightness. 1 = as the palette has it, 0.8 = a bit darker.")]
        public float brightness = 1f;
        [Tooltip("Shifts the hue, as a share of the colour wheel: 0.1 = a tenth of the way round. Used to make the rock colours differ from each other.")]
        [Range(-0.5f, 0.5f)] public float hueShift;
        [Tooltip("An emission property to set along with this colour, for example _EmissionColor. It keeps the original glow strength. Empty = none.")]
        public string emissionProperty;
    }

    [Tooltip("Every material colour that follows a palette.")]
    public List<Entry> materials = new List<Entry>();
}
