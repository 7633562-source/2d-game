using System.Collections.Generic;
using UnityEngine;

// Картинки уровня. Сначала контракт Level/* (visul). Пока своих PNG нет —
// бесшовный Art/dirt и Art/grass через ArtLibrary.Tiled. Коллайдер не зависит.
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
        // Свои PNG из Resources/Level/ важнее: контракт visul.
        // Пока их нет — бесшовная заливка из Art/dirt.
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

    // true, когда нет своей 9-slice из Level/, но есть бесшовный Art/dirt.
    public static bool UseTiledFill()
    {
        return !HasFill() && ArtLibrary.Albedo(ArtLibrary.Dirt) != null;
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

        // visul импортировал Multiple (dirt_0, grass_0) — Load<Sprite> молчит.
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
        tex.Apply(false, true);

        _white = Sprite.Create(
            tex,
            new Rect(0f, 0f, 32f, 32f),
            new Vector2(0.5f, 0.5f),
            32f,
            0,
            SpriteMeshType.FullRect,
            new Vector4(4f, 4f, 4f, 4f));
        return _white;
    }
}
