using System.Collections.Generic;
using UnityEngine;

// Заливки из Resources/Art. Нет файла — null, вызывающий рисует белое как раньше.
// Физику не трогает: это только пиксели для спрайтов.
public static class ArtLibrary
{
    public const string Skin = "skin";
    public const string Fabric = "fabric";
    public const string Denim = "denim";
    public const string Dirt = "dirt";
    public const string Grass = "grass";
    public const string Bark = "bark";
    public const string Leaf = "leaf";
    public const string HumanHead = "Human/head";
    public const string HumanNeck = "Human/neck";
    public const string HumanTorso = "Human/torso";
    public const string HumanPelvis = "Human/pelvis";
    public const string HumanUpper = "Human/upper";
    public const string HumanLower = "Human/lower";
    public const string HumanHand = "Human/hand";
    public const string HumanThigh = "Human/thigh";
    public const string HumanShin = "Human/shin";
    public const string HumanFoot = "Human/foot";
    public const string DogChest = "Dog/chest";
    public const string DogPelvis = "Dog/pelvis";
    public const string DogNeck = "Dog/neck";
    public const string DogHead = "Dog/head";
    public const string DogJaw = "Dog/jaw";
    public const string DogTail = "Dog/tail";
    public const string DogFrontUpper = "Dog/front_upper";
    public const string DogFrontLower = "Dog/front_lower";
    public const string DogThigh = "Dog/thigh";
    public const string DogShin = "Dog/shin";
    public const string DogPaw = "Dog/paw";

    public static bool IsHumanPart(string key)
    {
        return !string.IsNullOrEmpty(key) && key.StartsWith("Human/");
    }

    // Painted segment sprite: full PNG stretched onto the collider box.
    public static bool IsPaintedPart(string key)
    {
        if (string.IsNullOrEmpty(key))
            return false;
        return key.StartsWith("Human/")
            || key.StartsWith("Dog/")
            || key.StartsWith("Crow/")
            || key.StartsWith("Chicken/");
    }

    // Крупный повтор: на сегменте виден цвет ткани, не поры.
    public const float BodyTileMeters = 0.38f;

    // Земля: один повтор на несколько метров.
    public const float GroundTileMeters = 2.5f;
    public const float GrassTileMeters = 0.5f;

    // 64px на 2.5 м при камере 2.5 давало ~8× растяжение — «пиксель-арт».
    private const int SoftSize = 128;
    private const int TileSize = 512;

    private static readonly Dictionary<string, Texture2D> Source = new Dictionary<string, Texture2D>();
    private static readonly Dictionary<string, Texture2D> Soft = new Dictionary<string, Texture2D>();
    private static readonly Dictionary<string, Texture2D> Tiles = new Dictionary<string, Texture2D>();
    private static readonly Dictionary<string, Sprite> TiledSprites = new Dictionary<string, Sprite>();

    public static Texture2D Albedo(string key)
    {
        return SoftMap(key);
    }

    public static Color Sample(string key, float xMeters, float yMeters)
    {
        Texture2D tex = SoftMap(key);
        if (tex == null || !tex.isReadable)
            return Color.white;

        float u = xMeters / BodyTileMeters;
        float v = yMeters / BodyTileMeters;
        return tex.GetPixelBilinear(u, v);
    }

    public static Sprite Tiled(string key, float metersPerTile = GroundTileMeters)
    {
        if (string.IsNullOrEmpty(key))
            return null;

        string cacheKey = key + ":" + metersPerTile.ToString("F3");
        if (TiledSprites.TryGetValue(cacheKey, out Sprite cached) && cached != null)
            return cached;

        Texture2D tex = TileMap(key);
        if (tex == null)
            return null;

        float ppu = tex.width / Mathf.Max(0.05f, metersPerTile);
        Sprite sprite = Sprite.Create(
            tex,
            new Rect(0f, 0f, tex.width, tex.height),
            new Vector2(0.5f, 0.5f),
            ppu,
            0,
            SpriteMeshType.FullRect);
        TiledSprites[cacheKey] = sprite;
        return sprite;
    }

    private static Texture2D LoadSource(string key)
    {
        if (string.IsNullOrEmpty(key))
            return null;
        if (Source.TryGetValue(key, out Texture2D cached))
            return cached;
        Texture2D src = Resources.Load<Texture2D>("Art/" + key);
        if (src != null)
            src.wrapMode = TextureWrapMode.Repeat;
        Source[key] = src;
        return src;
    }

    private static Texture2D SoftMap(string key)
    {
        if (string.IsNullOrEmpty(key))
            return null;
        if (Soft.TryGetValue(key, out Texture2D cached))
            return cached;
        Texture2D src = LoadSource(key);
        Texture2D working = src != null && src.isReadable ? Downsample(src, SoftSize) : src;
        Soft[key] = working;
        return working;
    }

    private static Texture2D TileMap(string key)
    {
        if (string.IsNullOrEmpty(key))
            return null;
        if (Tiles.TryGetValue(key, out Texture2D cached))
            return cached;
        Texture2D src = LoadSource(key);
        Texture2D working = src != null && src.isReadable ? Downsample(src, TileSize) : src;
        Tiles[key] = working;
        return working;
    }

    private static Texture2D Downsample(Texture2D src, int size)
    {
        int w = Mathf.Min(size, src.width);
        int h = Mathf.Min(size, src.height);
        Texture2D dst = new Texture2D(w, h, TextureFormat.RGBA32, true);
        dst.filterMode = FilterMode.Bilinear;
        dst.wrapMode = TextureWrapMode.Repeat;
        dst.name = src.name + "_w" + w;

        Color[] pixels = new Color[w * h];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float u = (x + 0.5f) / w;
                float v = (y + 0.5f) / h;
                pixels[y * w + x] = src.GetPixelBilinear(u, v);
            }
        }

        dst.SetPixels(pixels);
        dst.Apply(true, false);
        return dst;
    }
}
