using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

// Picture keys stay for callers (ground, campfire). Albedo / Tiled / Sample
// read the real PNGs under Resources/Art/<key>.png (ART-1a): a key is a path
// fragment, so "bark" is Art/bark.png and "Human/head" is Art/Human/head.png.
// A key with no file returns null, and the caller must then show a labelled
// MISSING ART tile - never a silent placeholder.
// BindPicture stays a tint-only hook: the per-part redraw is ArtParts.Dress,
// which a scene calls explicitly (RealArtEnabled gates it, default OFF).
public static class ArtLibrary
{
    public const string Skin = "skin";
    public const string Fabric = "fabric";
    public const string Denim = "denim";
    public const string Dirt = "dirt";
    public const string Grass = "grass";
    public const string Bark = "bark";
    public const string Leaf = "leaf";

    public const float BodyTileMeters = 0.38f;
    public const float GroundTileMeters = 2.5f;
    public const float GrassTileMeters = 0.5f;

    const byte KindNone = 0;
    const byte KindDog = 1;
    const byte KindCrow = 2;
    const byte KindChicken = 3;

    // Unity 6: Object.GetInstanceID is obsolete (CS0619). Object == / !=
    // and if (obj) still call it. Cache the host Transform and compare
    // with ReferenceEquals (CURRENT.md Walk front rule 4).
    static Transform _host;
    static byte _hostKind;

    public static bool IsHumanPart(string key)
    {
        return !string.IsNullOrEmpty(key) && key.StartsWith("Human/");
    }

    // Human layout is a tinted white square. Painted PNG is off.
    public static bool IsPaintedPart(string key)
    {
        return false;
    }

    public static Color PaintedTint(Color layout)
    {
        return layout;
    }

    // Far limb in the sagittal pair (Left* names). Dim only the picture.
    public static bool IsFarSide(string objectName)
    {
        return !string.IsNullOrEmpty(objectName)
            && objectName.IndexOf("Left", System.StringComparison.Ordinal) >= 0;
    }

    public static Color DimFar(Color near)
    {
        return new Color(near.r * 0.70f, near.g * 0.70f, near.b * 0.70f, near.a * 0.62f);
    }

    // Do not attach Art/ PNG keys. Dog and bird get a species box tint.
    public static void BindPicture(string objectName, Transform node, ref string albedoKey, ref Color color)
    {
        albedoKey = null;
        if (ReferenceEquals(node, null))
            return;

        byte kind = HostKind(node);
        if (kind == KindDog)
            color = DogArt.Tint(objectName);
        else if (kind == KindChicken)
            color = ChickenArt.Tint(objectName);
        else if (kind == KindCrow)
            color = CrowArt.Tint(objectName);
    }

    // One GetComponentInParent per animal at build, not per physics step.
    static byte HostKind(Transform node)
    {
        Transform t = node;
        while (!ReferenceEquals(t, null))
        {
            if (ReferenceEquals(t, _host))
                return _hostKind;
            t = t.parent;
        }

        Dog dog = node.GetComponentInParent<Dog>();
        if (!ReferenceEquals(dog, null))
        {
            _host = dog.transform;
            _hostKind = KindDog;
            return KindDog;
        }

        Bird bird = node.GetComponentInParent<Bird>();
        if (!ReferenceEquals(bird, null))
        {
            _host = bird.transform;
            _hostKind = bird.kind == BirdKind.Chicken ? KindChicken : KindCrow;
            return _hostKind;
        }

        _host = node.root;
        _hostKind = KindNone;
        return KindNone;
    }

    public static string ResolveAlbedoKey(string objectName, Transform node)
    {
        _ = node;
        string key = ArtParts.KeyForName(objectName);
        return Albedo(key) != null ? key : null;
    }

    // ─── real art (ART-1a) ────────────────────────────────────────────────
    // OFF by default: a headless trial and every scene that does not ask for
    // real art stays on the collider-sized colour boxes, so no reference
    // number and no existing look changes. The gallery sets it in Start().
    public static bool RealArtEnabled { get; set; }

    private static readonly Dictionary<string, Texture2D> Sources = new Dictionary<string, Texture2D>();
    private static readonly Dictionary<string, Sprite> Tiles = new Dictionary<string, Sprite>();

    public static Texture2D Albedo(string key)
    {
        if (string.IsNullOrEmpty(key))
            return null;
        if (Sources.TryGetValue(key, out Texture2D cached))
            return cached;

        // Resources.Load handles the folder in the key: "Human/head" ->
        // Assets/Resources/Art/Human/head.png. Sprite assets are Texture2D
        // here as well, so a part PNG needs no second lookup.
        Texture2D tex = Resources.Load<Texture2D>("Art/" + key);
        Sources[key] = tex;
        return tex;
    }

    public static bool HasArt(string key)
    {
        return Albedo(key) != null;
    }

    // GroundBuilder / LevelArt already fall back to a white square + tint.
    public static Sprite Tiled(string key, float metersPerTile = GroundTileMeters)
    {
        if (string.IsNullOrEmpty(key) || metersPerTile <= 0.01f)
            return null;

        Texture2D tex = Albedo(key);
        if (tex == null)
            return null;

        string cacheKey = key + "@" + metersPerTile.ToString("0.####", CultureInfo.InvariantCulture);
        if (Tiles.TryGetValue(cacheKey, out Sprite cached) && cached != null)
            return cached;

        // ppu follows the requested period, so Tiled draw mode repeats at
        // metersPerTile in world metres and never scales the picture.
        float ppu = tex.width / metersPerTile;
        Sprite sprite = Sprite.Create(
            tex,
            new Rect(0f, 0f, tex.width, tex.height),
            new Vector2(0.5f, 0.5f),
            ppu,
            0,
            SpriteMeshType.FullRect);
        sprite.name = "tile_" + cacheKey;
        Tiles[cacheKey] = sprite;
        return sprite;
    }

    public static Color Sample(string key, float xMeters, float yMeters)
    {
        Texture2D tex = Albedo(key);
        if (tex == null || !tex.isReadable)
            return Color.white;

        float u = Mathf.Repeat(xMeters, 1f);
        float v = Mathf.Repeat(yMeters, 1f);
        return tex.GetPixelBilinear(u, v);
    }
}
