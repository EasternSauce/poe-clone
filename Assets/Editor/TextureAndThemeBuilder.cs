using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Generates a handful of small tileable procedural textures (no imported assets)
/// and re-themes every world material to the custom PoeClone/ToonLit shader with
/// one of those textures, replacing the default shiny PBR Lit look.
/// </summary>
public static class TextureAndThemeBuilder
{
    private const string TexDir = "Assets/Textures/Generated";
    private const string MatDir = "Assets/Materials/Stylized";

    [MenuItem("PoeClone/Apply Thematic Shader + Textures")]
    public static void Apply()
    {
        Directory.CreateDirectory(Path.Combine(Application.dataPath, "../" + TexDir).Replace("Assets/../Assets", "Assets"));
        if (!AssetDatabase.IsValidFolder(TexDir))
            AssetDatabase.CreateFolder("Assets/Textures", "Generated");

        Texture2D bark = GetOrBuild("Tex_Bark", 128, BarkPixel);
        Texture2D stone = GetOrBuild("Tex_Stone", 128, StonePixel);
        Texture2D foliage = GetOrBuild("Tex_Foliage", 128, FoliagePixel);
        Texture2D cloth = GetOrBuild("Tex_Cloth", 128, ClothPixel);
        Texture2D wall = GetOrBuild("Tex_Wall", 128, WallPixel);
        Texture2D roof = GetOrBuild("Tex_Roof", 128, RoofPixel);

        var mapping = new Dictionary<string, Texture2D>
        {
            { "Bark", bark }, { "Wood", bark }, { "Hair", bark },
            { "Rock", stone }, { "RockDark", stone }, { "Stone", stone }, { "Bone", stone }, { "Steel", stone },
            { "PineA", foliage }, { "PineB", foliage }, { "OakA", foliage }, { "OakB", foliage },
            { "Cloth", cloth }, { "Pants", cloth }, { "Hood", cloth }, { "Cloak", cloth }, { "Tunic", cloth },
            { "Leather", cloth }, { "EnemyCloth", cloth }, { "EnemyPants", cloth },
            { "Wall", wall },
            { "Roof", roof },
        };

        // World-units-per-tile for triplanar sampling (the custom meshes have no UVs,
        // so material tiling now comes from world scale, not a UV transform).
        var tileSize = new Dictionary<string, float>
        {
            { "Bark", 0.5f }, { "Wood", 0.5f }, { "Hair", 0.35f },
            { "Rock", 1.1f }, { "RockDark", 0.9f }, { "Stone", 1.3f }, { "Bone", 0.5f }, { "Steel", 0.8f },
            { "PineA", 1.4f }, { "PineB", 1.4f }, { "OakA", 1.2f }, { "OakB", 1.2f },
            { "Cloth", 0.3f }, { "Pants", 0.3f }, { "Hood", 0.3f }, { "Cloak", 0.4f }, { "Tunic", 0.3f },
            { "Leather", 0.35f }, { "EnemyCloth", 0.3f }, { "EnemyPants", 0.3f },
            { "Wall", 1.6f },
            { "Roof", 1.2f },
        };

        Shader toon = Shader.Find("PoeClone/ToonLit");
        if (toon == null) { Debug.LogError("PoeClone/ToonLit shader not found."); return; }

        string[] matPaths = AssetDatabase.FindAssets("t:Material", new[] { MatDir });
        int themed = 0;
        foreach (string guid in matPaths)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) continue;

            string matName = mat.name;

            // Ground keeps its own texture (already photographic-ish tiled grass);
            // just move it onto the new shader for consistent lighting.
            bool isGround = matName == "GroundStylized";

            Color baseColor = mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor") : Color.white;
            mat.shader = toon;
            mat.SetColor("_BaseColor", baseColor);

            // Shadow tint: darker and slightly cooler than the lit color, not just black.
            Color shadow = new Color(baseColor.r * 0.5f, baseColor.g * 0.5f, Mathf.Min(1f, baseColor.b * 0.62f), 1f);
            mat.SetColor("_ShadowColor", shadow);
            mat.SetFloat("_RimIntensity", isGround ? 0f : 0.16f);
            mat.SetFloat("_RimPower", 3.5f);
            mat.SetColor("_RimColor", Color.Lerp(baseColor, Color.white, 0.6f));
            mat.SetFloat("_AmbientBoost", 0.35f);

            if (isGround)
            {
                // The shader is fully triplanar now (the custom meshes have no UVs at
                // all), but for a flat horizontal plane the triplanar Y-projection
                // (world XZ) is exactly the same as a standard top-down UV projection,
                // so this still shows the existing generated ground texture correctly.
                mat.SetFloat("_TriplanarTileSize", 40f);
                mat.SetFloat("_TexInfluence", 1f);
            }
            else if (mapping.TryGetValue(matName, out Texture2D tex))
            {
                mat.SetTexture("_BaseMap", tex);
                float size = tileSize.TryGetValue(matName, out float sv) ? sv : 1f;
                mat.SetFloat("_TriplanarTileSize", size);
                mat.SetFloat("_TexInfluence", 1f);
            }
            else
            {
                mat.SetFloat("_TexInfluence", 0f);
            }

            EditorUtility.SetDirty(mat);
            themed++;
        }

        AssetDatabase.SaveAssets();
        Debug.Log("Thematic shader + textures applied to " + themed + " materials.");
    }

    private static Texture2D GetOrBuild(string name, int size, System.Func<int, int, int, float> pixelFn)
    {
        string path = TexDir + "/" + name + ".png";
        Texture2D existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (existing != null) return existing;

        Texture2D tex = new Texture2D(size, size, TextureFormat.RGB24, false);
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float v = pixelFn(x, y, size);
                pixels[y * size + x] = new Color(v, v, v, 1f);
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();

        byte[] png = tex.EncodeToPNG();
        File.WriteAllBytes(Path.Combine(Application.dataPath, "..", path), png);
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.filterMode = FilterMode.Bilinear;
        importer.mipmapEnabled = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // ---------------------------------------------------------------- noise helpers

    private static float Hash(int x, int y)
    {
        int h = x * 374761393 + y * 668265263;
        h = (h ^ (h >> 13)) * 1274126177;
        return ((h ^ (h >> 16)) & 0x7fffffff) / (float)int.MaxValue;
    }

    private static float ValueNoise(float x, float y)
    {
        int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
        int x1 = x0 + 1, y1 = y0 + 1;
        float tx = x - x0, ty = y - y0;
        float a = Hash(x0, y0), b = Hash(x1, y0), c = Hash(x0, y1), d = Hash(x1, y1);
        float ab = Mathf.Lerp(a, b, tx);
        float cd = Mathf.Lerp(c, d, tx);
        return Mathf.Lerp(ab, cd, ty);
    }

    private static float Fbm(float x, float y, int octaves)
    {
        float sum = 0f, amp = 0.5f, freq = 1f, norm = 0f;
        for (int i = 0; i < octaves; i++)
        {
            sum += ValueNoise(x * freq, y * freq) * amp;
            norm += amp;
            amp *= 0.5f;
            freq *= 2f;
        }
        return sum / norm;
    }

    // ---------------------------------------------------------------- texture categories

    private static float BarkPixel(int x, int y, int size)
    {
        // Vertical streaks: stretched noise along Y.
        float n = Fbm(x * 0.12f, y * 0.02f, 3);
        float fine = ValueNoise(x * 0.6f, y * 0.9f) * 0.25f;
        float v = n * 0.75f + fine;
        return Mathf.Lerp(0.72f, 1.15f, v);
    }

    private static float StonePixel(int x, int y, int size)
    {
        float n = Fbm(x * 0.06f, y * 0.06f, 3);
        float speck = ValueNoise(x * 0.5f, y * 0.5f) * 0.3f;
        float v = Mathf.Clamp01(n * 0.7f + speck);
        return Mathf.Lerp(0.78f, 1.2f, v);
    }

    private static float FoliagePixel(int x, int y, int size)
    {
        float n = Fbm(x * 0.08f, y * 0.08f, 4);
        float cluster = ValueNoise(x * 0.35f, y * 0.35f) * 0.35f;
        float v = Mathf.Clamp01(n * 0.65f + cluster);
        return Mathf.Lerp(0.72f, 1.28f, v);
    }

    private static float ClothPixel(int x, int y, int size)
    {
        float weave = (Mathf.Sin(x * 1.6f) * Mathf.Sin(y * 1.6f)) * 0.5f + 0.5f;
        float n = ValueNoise(x * 0.15f, y * 0.15f);
        float v = weave * 0.25f + n * 0.15f;
        return Mathf.Lerp(0.9f, 1.08f, v);
    }

    private static float WallPixel(int x, int y, int size)
    {
        // Coarse blotches plus faint brick seams.
        float n = Fbm(x * 0.05f, y * 0.05f, 3);
        int brickH = 16, brickW = 28;
        int row = y / brickH;
        int offset = (row % 2 == 0) ? 0 : brickW / 2;
        bool seam = (y % brickH < 1) || ((x + offset) % brickW < 1);
        float v = n;
        if (seam) v -= 0.18f;
        return Mathf.Clamp(Mathf.Lerp(0.8f, 1.15f, v), 0.55f, 1.2f);
    }

    private static float RoofPixel(int x, int y, int size)
    {
        int bandH = 10;
        float band = (y % bandH) / (float)bandH;
        float seam = band < 0.12f ? -0.15f : 0f;
        float n = ValueNoise(x * 0.3f, y * 0.3f) * 0.15f;
        float v = 1f + seam + n - 0.08f;
        return Mathf.Clamp(v, 0.6f, 1.15f);
    }
}
