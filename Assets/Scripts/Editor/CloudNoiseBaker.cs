// ---------------------------------------------------------------------------
// CloudNoiseBaker
//
// Bakes the cloud noise's random corner values into a 3D texture, once, so
// the sky shader can read them with one texture sample instead of hashing
// 8 corners for every noise read. Run it from the menu; it saves an asset.
//
// Lives in an "Editor" folder, so it is left out of game builds.
// ---------------------------------------------------------------------------

using UnityEditor;
using UnityEngine;

public static class CloudNoiseBaker
{
    private const int Size = 64;                                     // cells per side; the noise repeats every 64 cells
    private const TextureFormat Format = TextureFormat.R8;           // 1 byte per value, 256 KB in total
    private const string AssetPath = "Assets/Shaders/CloudNoise3D.asset";

    // Same hash the shader's math noise used before it was baked, so the clouds keep the look they were tuned with.
    private static float Hash(int x, int y, int z)
    {
        float hx = Frac(x * 0.1031f), hy = Frac(y * 0.1031f), hz = Frac(z * 0.1031f);
        float d = hx * (hz + 31.32f) + hy * (hy + 31.32f) + hz * (hx + 31.32f);
        hx += d; hy += d; hz += d;
        return Frac((hx + hy) * hz);
    }

    private static float Frac(float v) => v - Mathf.Floor(v);

    private static Texture3D CreateTexture()
    {
        var values = new byte[Size * Size * Size];
        for (int z = 0; z < Size; z++)
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                    values[x + y * Size + z * Size * Size] = (byte)Mathf.RoundToInt(Hash(x, y, z) * 255f);

        var texture = new Texture3D(Size, Size, Size, Format, false); // false = no mipmaps
        texture.wrapMode = TextureWrapMode.Repeat;   // past cell 63 it starts again at cell 0
        texture.filterMode = FilterMode.Bilinear;    // the GPU blends the 8 surrounding cells
        texture.SetPixelData(values, 0);
        texture.Apply(false);
        return texture;
    }

    [MenuItem("Return of the Hamster/Bake Cloud Noise Texture")]
    private static void Bake()
    {
        Texture3D texture = CreateTexture();
        var existing = AssetDatabase.LoadAssetAtPath<Texture3D>(AssetPath);

        if (existing != null)
        {
            // Overwrite the existing asset in place, so materials that use it keep their reference.
            EditorUtility.CopySerialized(texture, existing);
            Object.DestroyImmediate(texture);
            EditorUtility.SetDirty(existing);
            AssetDatabase.SaveAssets();
        }
        else
        {
            AssetDatabase.CreateAsset(texture, AssetPath);
        }

        Debug.Log($"Baked cloud noise to {AssetPath}");
    }
}
