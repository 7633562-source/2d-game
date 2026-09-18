using UnityEngine;

// Picture keys stay for callers (ground, campfire). This layer no longer
// loads Resources/Art PNGs: every sprite is a flat color box, same as Human.
// Dog / Crow / Chicken replace the debug rainbow tints at bake time.
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
        _ = objectName;
        _ = node;
        return null;
    }

    public static Texture2D Albedo(string key)
    {
        _ = key;
        return null;
    }

    public static Color Sample(string key, float xMeters, float yMeters)
    {
        _ = key;
        _ = xMeters;
        _ = yMeters;
        return Color.white;
    }

    // GroundBuilder / LevelArt already fall back to a white square + tint.
    public static Sprite Tiled(string key, float metersPerTile = GroundTileMeters)
    {
        _ = key;
        _ = metersPerTile;
        return null;
    }
}
