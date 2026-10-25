using System.Collections.Generic;
using UnityEngine;

// Level pictures. Level/* contract first (visul). Until own PNGs exist —
// seamless Art/dirt and Art/grass via ArtLibrary.Tiled. Collider does not depend.
public static class LevelArt
{
    public const string FillKey = "ground_fill";
    public const string CapKey = "ground_cap";
    public const string SkyKey = "sky";
    public const string SignKey = "sign_finish";

    private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();
    private static Sprite _white;

    public static Sprite Fill()
    {
        // Own PNGs from Resources/Level/ win: visul contract.
        // ArtLibrary.Tiled is off: White() + FillTint is the flat-color path.
        return Load(FillKey) ?? ArtLibrary.Tiled(ArtLibrary.Dirt, ArtLibrary.GroundTileMeters) ?? White();
    }

    public static Sprite Cap()
    {
        return Load(CapKey) ?? ArtLibrary.Tiled(ArtLibrary.Grass, ArtLibrary.GrassTileMeters) ?? White();
    }

    public static Sprite Sign()
    {
        return Load(SignKey) ?? White();
    }

    public static Sprite Sky()
    {
        return Load(SkyKey);
    }

    public static bool HasFill()
    {
        return Load(FillKey) != null;
    }

    public static bool HasCap()
    {
        return Load(CapKey) != null;
    }

    public static bool HasSign()
    {
        return Load(SignKey) != null;
    }

    public static Color FillTint()
    {
        if (HasFill() || ArtLibrary.Albedo(ArtLibrary.Dirt) != null)
            return Color.white;
        return new Color(0.42f, 0.30f, 0.16f, 1f);
    }

    public static Color CapTint()
    {
        if (HasCap() || ArtLibrary.Albedo(ArtLibrary.Grass) != null)
            return Color.white;
        return new Color(0.30f, 0.58f, 0.28f, 1f);
    }

    // true when the fill is a real texture that can repeat: the Level PNG or
    // the seamless Art/dirt tile. White() is a flat colour and cannot tile, so
    // a slab keeps the Sliced stretch then. StaticPlatform asks this before it
    // draws a slab wider than one native tile (the 32 m "Behind" plate).
    public static bool UseTiledFill()
    {
        return HasFill()
            || ArtLibrary.Tiled(ArtLibrary.Dirt, ArtLibrary.GroundTileMeters) != null;
    }

    public static Color SignTint()
    {
        return HasSign() ? Color.white : new Color(0.82f, 0.22f, 0.18f, 1f);
    }

    public static Color SkyTint()
    {
        return Sky() != null ? Color.white : new Color(0.55f, 0.78f, 0.94f, 1f);
    }

    public static Sprite Load(string key)
    {
        if (string.IsNullOrEmpty(key))
            return null;
        if (Cache.TryGetValue(key, out Sprite cached))
            return cached;

        Sprite found = null;
        string[] paths = PathsFor(key);
        for (int i = 0; i < paths.Length && found == null; i++)
            found = LoadAt(paths[i]);

        Cache[key] = found;
        return found;
    }

    private static string[] PathsFor(string key)
    {
        if (key == FillKey)
            return new[] { "Level/ground_fill" };
        if (key == CapKey)
            return new[] { "Level/ground_cap" };
        if (key == SkyKey)
            return new[] { "Level/sky", "Art/sky" };
        if (key == SignKey)
            return new[] { "Level/sign_finish", "Art/sign_finish" };
        return new[] { "Level/" + key };
    }

    private static Sprite LoadAt(string path)
    {
        Sprite single = Resources.Load<Sprite>(path);
        if (single != null)
            return single;

        // visul imported Multiple (dirt_0, grass_0) — Load<Sprite> stays silent.
        Sprite[] many = Resources.LoadAll<Sprite>(path);
        if (many != null && many.Length > 0)
            return many[0];
        return null;
    }

    public static Sprite White()
    {
        if (_white != null)
            return _white;

        Texture2D tex = new Texture2D(32, 32, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        Color[] pixels = new Color[32 * 32];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = Color.white;
        tex.SetPixels(pixels);
        // Keep CPU pixels: Sprite.Create on WebGL rejects an unreadable
        // texture and then Sliced reports Failed to create texture.
        tex.Apply(false, false);

        // Solid white, no 9-slice border. 4 px at 32 PPU is 0.125 m per
        // side, so Sliced needs size ≥ 0.25 m. Woods trunks are 0.10–0.18 m
        // and curb faces 0.14 × 0.05 m; Unity then builds an inner texture
        // of width ≤ 0 and throws Failed to create texture.
        _white = Sprite.Create(
            tex,
            new Rect(0f, 0f, 32f, 32f),
            new Vector2(0.5f, 0.5f),
            32f,
            0,
            SpriteMeshType.FullRect,
            Vector4.zero);
        return _white;
    }
}
