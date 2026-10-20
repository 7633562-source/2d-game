using UnityEngine;

// The labelled gap. Where an object has no real texture yet, the gallery shows
// a magenta/black chequer tile with the words MISSING ART and the object's name
// - never a silent placeholder that a viewer would read as shipping art.
// Picture only: SpriteRenderer + TextMesh, no collider, no Rigidbody2D.
public static class ArtMissing
{
    public const string NoticeKey = "MISSING ART";
    public const int SortingOrder = 90;

    private static Sprite _checker;

    // 64x64, 8 px squares, PPU 64: one chequer of four squares is 1 m, so a
    // Tiled renderer repeats a 1 m magenta/black pattern whatever the tile size.
    public static Sprite Checker()
    {
        if (_checker != null)
            return _checker;

        const int size = 64;
        const int square = 8;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Repeat;
        Color magenta = new Color(1f, 0.05f, 0.75f, 1f);
        Color dark = new Color(0.08f, 0.06f, 0.08f, 1f);
        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool on = ((x / square) + (y / square)) % 2 == 0;
                pixels[y * size + x] = on ? magenta : dark;
            }
        }
        tex.SetPixels(pixels);
        tex.Apply(false, false);
        _checker = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size,
            0, SpriteMeshType.FullRect);
        _checker.name = "MissingArtChecker";
        return _checker;
    }

    // A gap tile: chequer box, the notice, the object, and where its art would
    // come from. localPosition is the box centre in the parent's space.
    public static GameObject Tile(
        Transform parent,
        string objectName,
        string sourceNote,
        Vector2 size,
        Vector3 localPosition,
        float labelHeightMeters = 0.12f,
        int sortingOrder = SortingOrder)
    {
        GameObject box = new GameObject("MissingArt_" + Text.Slug(objectName));
        box.transform.SetParent(parent, false);
        box.transform.localPosition = localPosition;
        box.transform.localScale = Vector3.one;

        SpriteRenderer sr = box.AddComponent<SpriteRenderer>();
        sr.sprite = Checker();
        sr.color = Color.white;
        sr.drawMode = SpriteDrawMode.Tiled;
        sr.tileMode = SpriteTileMode.Continuous;
        sr.size = size;
        sr.sortingOrder = sortingOrder;
        SpriteLighting.ApplyUnlit(sr);

        float half = size.y * 0.5f;
        GalleryLabels.Readable(box.transform, NoticeKey, new Vector3(0f, half - labelHeightMeters * 1.6f, -0.01f),
            labelHeightMeters * 1.15f, Color.white, sortingOrder + 2);
        GalleryLabels.Readable(box.transform, objectName, new Vector3(0f, half - labelHeightMeters * 3.0f, -0.01f),
            labelHeightMeters, Color.white, sortingOrder + 2);
        if (!string.IsNullOrEmpty(sourceNote))
            GalleryLabels.Readable(box.transform, sourceNote, new Vector3(0f, -half + labelHeightMeters, -0.01f),
                labelHeightMeters * 0.82f, new Color(1f, 1f, 1f, 0.92f), sortingOrder + 2);
        return box;
    }

    // "ART-1b pending" / "ART-3 pending" slots: the object exists in the game,
    // its art lane has not delivered yet. Same tile, explicit about the lane.
    public static GameObject PendingTile(
        Transform parent,
        string objectName,
        string lane,
        Vector2 size,
        Vector3 localPosition,
        float labelHeightMeters = 0.12f)
    {
        GameObject box = Tile(parent, objectName, lane + " pending", size, localPosition, labelHeightMeters);
        box.name = "Pending_" + Text.Slug(objectName);
        return box;
    }

    // Small helper so a name with spaces/slashes still gives a stable GO name.
    private static class Text
    {
        public static string Slug(string raw)
        {
            if (string.IsNullOrEmpty(raw))
                return "unnamed";
            char[] chars = raw.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                if (!char.IsLetterOrDigit(chars[i]) && chars[i] != '_' && chars[i] != '-')
                    chars[i] = '_';
            }
            return new string(chars);
        }
    }
}
