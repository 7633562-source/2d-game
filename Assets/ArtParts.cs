using System.Collections.Generic;
using UnityEngine;

// Per-part real art (ART-1a). A part's picture is the shipping rectangle - the
// collider box or the renderer's own size - filled with the real texture. Size
// is never taken from localScale (project rule), and nothing here touches a
// Rigidbody2D, a collider, a joint or a mass: it is a SpriteRenderer redraw.
//
// A part thinner than one tile keeps Sliced draw mode, i.e. the tile is shown
// cropped to that part's box. That is the rectangle-equals-collider contract,
// not a missing texture: a missing texture is ArtLibrary.Albedo(key) == null and
// must be drawn as a labelled ArtMissing tile by the caller.
public static class ArtParts
{
    public const float BarkTileMeters = 0.5f;
    public const float LeafTileMeters = 0.5f;
    public const string FruitKey = "Apple/fruit";
    public const string BerryKey = "Strawberry/berry";
    public const string StrawberryLeafKey = "Strawberry/leaf";
    public const string StrawberryStemKey = "Strawberry/stem";

    // --- Flower parts (ART-0 gap 3, 20.09) --------------------------------------------
    // The one object that had no art at all: PlantFlower drew every petal, the distal
    // lip, the cup of a tulip, the sepals and the head plate as the flat collider box.
    // The canvases are LUMINANCE canvases (grey RGB, the shape lives in alpha), so the
    // part's own kind colour multiplies them: one canvas serves all twelve kinds, the
    // way one grass tile serves every tree. Canvas aspects match the BOX of a
    // representative kind (petal 0.63 = the rose/daisy petal box), and the box's own
    // aspect is what the part draws - a flower is 0.02-0.09 m, i.e. 10-30 px at the
    // gallery lens, so the silhouette and the shading ramp are what the eye gets.
    public const string FlowerPetalKey = "Flower/petal";
    public const string FlowerTipKey = "Flower/petal_tip";
    public const string FlowerCupKey = "Flower/cup";
    public const string FlowerCalyxKey = "Flower/calyx";
    public const string FlowerDiskKey = "Flower/disk";

    public static readonly string[] FlowerKeys =
    {
        FlowerPetalKey, FlowerTipKey, FlowerCupKey, FlowerCalyxKey, FlowerDiskKey
    };

    // A drawn part bigger than this is a unit mix-up, not a plant part.
    public const float MaxPartMeters = 40f;

    // Diagnostics for the capture report.
    public static int GiantSkips { get; private set; }
    public static int EmptySkips { get; private set; }
    public static int RevertedMoves { get; private set; }

    public static void ResetSkips()
    {
        GiantSkips = 0;
        EmptySkips = 0;
        RevertedMoves = 0;
    }

    private static string PathOf(SpriteRenderer sr)
    {
        Transform t = sr.transform;
        string p = t.name;
        while (t.parent != null)
        {
            t = t.parent;
            p = t.name + "/" + p;
        }
        return p;
    }

    private struct Saved
    {
        public Sprite sprite;
        public Color color;
        public SpriteDrawMode mode;
        public SpriteTileMode tileMode;
        public Vector2 size;
        public bool valid;
    }

    // Keyed by the renderer itself, so the toggle is symmetric: real=false puts
    // back exactly the sprite the part was built with.
    private static readonly Dictionary<SpriteRenderer, Saved> Originals =
        new Dictionary<SpriteRenderer, Saved>();

    // Name -> art key for the plant family only. Creature and human parts are
    // NOT matched here on purpose: their binding is ART-1b / ART-2 / ART-3 and
    // their names (Head, Paw, Torso) collide with the plant words.
    public static string KeyForName(string objectName)
    {
        if (string.IsNullOrEmpty(objectName))
            return null;

        if (Has(objectName, "Fruit") || Has(objectName, "Apple"))
            return FruitKey;
        if (Has(objectName, "Berry"))
            return BerryKey;
        if (Has(objectName, "Tuft") || Has(objectName, "Leaf") || Has(objectName, "Canopy")
            || Has(objectName, "Crown") || Has(objectName, "Pad") || Has(objectName, "Bloom"))
            return ArtLibrary.Leaf;
        if (Has(objectName, "Trunk") || Has(objectName, "Stump") || Has(objectName, "Wood")
            || Has(objectName, "Limb") || Has(objectName, "Branch") || Has(objectName, "Twig")
            || Has(objectName, "Bough") || Has(objectName, "Stick") || Has(objectName, "Bark")
            || Has(objectName, "Cone") || Has(objectName, "Taper") || Has(objectName, "Fork")
            || Has(objectName, "Leader") || Has(objectName, "Seg"))
            return ArtLibrary.Bark;
        return null;
    }

    private static bool Has(string name, string token)
    {
        return name.IndexOf(token, System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    // Name -> flower art key. Deliberately NOT part of KeyForName: that table answers for
    // the tree/berry family, where "Bloom" means a leaf and "Disk" means nothing, and the
    // yard's decor flowers (YardAmbient.AddHousePart builds PetalL/PetalUp/Disk of its own)
    // must not be caught here. `Spent` (autumn hip), `Spike_i` (lavender) and the leaflets
    // `Leaf_i` are not in this table: the bloom is this pass, the foliage is its own.
    public static string FlowerKeyForName(string objectName)
    {
        if (string.IsNullOrEmpty(objectName))
            return null;

        if (Has(objectName, "Sepal"))
            return FlowerCalyxKey;
        if (Has(objectName, "PetalTip"))
            return FlowerTipKey;
        if (Has(objectName, "Petal"))
            return FlowerPetalKey;
        if (Has(objectName, "Disk"))
            return FlowerDiskKey;
        if (Has(objectName, "Bloom"))
            return FlowerCupKey;
        return null;
    }

    // Real art for the flower rigs under root. Scoped by the HOST COMPONENT, not by the
    // name: a renderer is dressed only when a PlantFlower sits above it, so calling this on
    // a scene root (GalleryScene.SetRealArt does) cannot repaint a tree, a berry or the
    // yard's decor flowers, whose part names collide. Returns the number of parts redrawn.
    public static int DressFlower(Transform root, bool real)
    {
        if (root == null)
            return 0;

        SpriteRenderer[] renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
        int dressed = 0;
        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer sr = renderers[i];
            if (sr == null || !sr.enabled)
                continue;   // an open kind switches its bloom box off on purpose: leave it
            if (sr.GetComponentInParent<PlantFlower>() == null)
                continue;

            string key = FlowerKeyDeep(sr);
            if (string.IsNullOrEmpty(key))
                continue;

            // keepColor: the canvas is grey and the part keeps the kind's own petal /
            // sepal / disk colour. Nothing else about the part changes.
            if (Dress(sr, key, 0f, real, true))
                dressed++;
        }

        if (real && dressed > 0)
            Debug.Log("ART-PARTS flowers dressed=" + dressed + "/" + renderers.Length
                + " root=" + root.name);
        return dressed;
    }

    private static string FlowerKeyDeep(SpriteRenderer sr)
    {
        Transform t = sr.transform;
        for (int depth = 0; depth < 6 && t != null; depth++)
        {
            string key = FlowerKeyForName(t.gameObject.name);
            if (!string.IsNullOrEmpty(key))
                return key;
            t = t.parent;
        }
        return null;
    }

    // Real art on/off for every part under root. Returns the number of parts
    // redrawn. woodKey/leafKey/fruitKey decide the family; an empty key skips
    // that family. A part whose texture is missing is left alone - the caller
    // shows a labelled ArtMissing tile instead of a silent placeholder.
    public static int DressAll(
        Transform root,
        bool real,
        string woodKey = ArtLibrary.Bark,
        string leafKey = ArtLibrary.Leaf,
        string fruitKey = FruitKey,
        string stemKey = null,
        float woodTileMeters = BarkTileMeters,
        float leafTileMeters = LeafTileMeters)
    {
        if (root == null)
            return 0;

        SpriteRenderer[] renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
        int dressed = 0;
        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer sr = renderers[i];
            if (sr == null)
                continue;

            string key = FamilyKeyDeep(sr, woodKey, leafKey, fruitKey, stemKey);
            if (string.IsNullOrEmpty(key))
                continue;

            float tile = TileMetersFor(sr.gameObject.name, key, woodTileMeters, leafTileMeters);
            if (Dress(sr, key, tile, real))
                dressed++;
        }

        if (real)
            Debug.Log("ART-PARTS dressed=" + dressed + "/" + renderers.Length + " root=" + root.name
                + " giantSkips=" + GiantSkips + " emptySkips=" + EmptySkips
                + " reverted=" + RevertedMoves);
        return dressed;
    }

    // The renderer's own name first, then its parents: the rigs put the picture
    // on a child named "Visual" and keep the part's name on the segment above
    // it (Wood_1_2/Visual), so the name that matters is often one level up.
    private static string FamilyKeyDeep(
        SpriteRenderer sr,
        string woodKey,
        string leafKey,
        string fruitKey,
        string stemKey)
    {
        Transform t = sr.transform;
        for (int depth = 0; depth < 6 && t != null; depth++)
        {
            string key = FamilyKey(t.gameObject.name, woodKey, leafKey, fruitKey, stemKey);
            if (!string.IsNullOrEmpty(key))
                return key;
            t = t.parent;
        }
        return null;
    }

    private static string FamilyKey(string name, string woodKey, string leafKey, string fruitKey, string stemKey)
    {
        string key = KeyForName(name);
        if (key == ArtLibrary.Bark)
            return woodKey;
        if (key == ArtLibrary.Leaf)
            return leafKey;
        if (key == BerryKey)
            return ArtLibrary.HasArt(ArtLibrary.Leaf) && Has(name, "Leaf") ? leafKey : BerryKey;
        if (key == FruitKey)
            return fruitKey;
        if (stemKey != null && Has(name, "Stem"))
            return stemKey;
        if (key == null && stemKey != null && Has(name, "Stem"))
            return stemKey;
        _ = stemKey;
        return key;
    }

    private static float TileMetersFor(string name, string key, float woodTileMeters, float leafTileMeters)
    {
        if (key == ArtLibrary.Bark)
            return woodTileMeters;
        if (key == ArtLibrary.Leaf)
            return leafTileMeters;
        // Fruit / berry / stem are objects, not materials: one sprite per part,
        // so the tile period is the part's own size and Tiled is never chosen.
        return 0f;
    }

    // One renderer. real=false restores the built sprite. keepColor=true leaves the
    // renderer's own tint in place: the flower canvases carry the shading as grey and the
    // part's kind colour is the tint, so whiting it out would drop the kind (and the
    // canvas comes back through it). Every other caller keeps the old behaviour.
    public static bool Dress(SpriteRenderer sr, string key, float tileMeters, bool real, bool keepColor = false)
    {
        if (sr == null)
            return false;

        if (!Originals.TryGetValue(sr, out Saved saved) || !saved.valid)
        {
            saved = new Saved
            {
                sprite = sr.sprite,
                color = sr.color,
                mode = sr.drawMode,
                tileMode = sr.tileMode,
                size = sr.size,
                valid = true
            };
            Originals[sr] = saved;
        }

        if (!real)
        {
            sr.sprite = saved.sprite;
            sr.color = saved.color;
            sr.drawMode = saved.mode;
            sr.tileMode = saved.tileMode;
            sr.size = saved.size;
            return true;
        }

        if (string.IsNullOrEmpty(key) || ArtLibrary.Albedo(key) == null)
            return false;

        Vector2 box = BoxOf(sr);
        if (box.x <= 0.0001f || box.y <= 0.0001f)
        {
            EmptySkips++;
            return false;
        }

        // A monster box means the rig keeps its own units and a parent scale
        // shrinks them, or the renderer drew nothing at all: either way a real
        // sprite here would cover the whole scene, so refuse.
        Vector3 scale = sr.transform.lossyScale;
        float scaleMax = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
        Vector2 world = new Vector2(box.x * scaleMax, box.y * scaleMax);
        if (world.x > MaxPartMeters || world.y > MaxPartMeters)
        {
            GiantSkips++;
            if (GiantSkips <= 12)
                Debug.LogWarning("ART-PARTS skip giant " + PathOf(sr) + " world="
                    + world.x.ToString("0.00") + "x" + world.y.ToString("0.00") + " m key=" + key);
            return false;
        }

        // tileMeters is a world period, the box is in local units.
        float local = tileMeters > 0.01f ? tileMeters / Mathf.Max(0.001f, scaleMax) : 0f;
        float tile = local > 0.01f ? local : Mathf.Max(box.x, box.y);
        Sprite sprite = ArtLibrary.Tiled(key, tile);
        if (sprite == null)
            return false;

        // Draw mode FIRST (ART-0 21st pass). Unity keeps the current world box when a
        // Simple renderer is converted to Sliced/Tiled, and it does that by writing the
        // TRANSFORM's scale. With the old order (picture, size, mode) that factor came out
        // as the new canvas' own size over the size the old placeholder had reported -
        // log_a 0.14 / 0.14021 = 0.99849, log_b 1.00810, log_c 0.99375 - so the picture sat
        // on the box only because of a scale the dress never asked for, and the check
        // "dressing moves no localScale" failed on all three logs. Converting while the
        // renderer still carries its own placeholder (its world box equals its reported
        // size there) makes that factor exactly 1: nothing about the part moves, and
        // `sprite` / `size` / `color` are the only things the dress writes.
        // Evidence: Art1Check.ScaleAudit sequence B, gate assertion on every log.
        if (box.x > tile + 0.0001f || box.y > tile + 0.0001f)
        {
            sr.drawMode = SpriteDrawMode.Tiled;
            sr.tileMode = SpriteTileMode.Continuous;
        }
        else
        {
            sr.drawMode = SpriteDrawMode.Sliced;
        }

        sr.sprite = sprite;
        if (!keepColor)
            sr.color = Color.white;
        sr.size = box;

        // NOTE: sr.bounds read here is not trustworthy - Unity rebuilds the
        // sprite mesh on the next frame, so a Sliced/Tiled conversion still
        // reports the old Simple box. Geometry is proved end to end instead
        // (GalleryCapture compares the content box before and after dressing
        // once real frames have passed).
        return true;
    }

    // The shipping box in LOCAL units (the transform scale multiplies it): a
    // Simple renderer is its sprite's own size, a Sliced/Tiled one is sr.size.
    // A Simple renderer with no sprite draws nothing, so its stale sr.size must
    // not be trusted - it is usually a scaled-up number from the rig's own
    // local units and would draw a monster plate once a real sprite lands.
    public static Vector2 BoxOf(SpriteRenderer sr)
    {
        if (sr == null)
            return Vector2.zero;
        if (sr.drawMode == SpriteDrawMode.Simple)
            return sr.sprite != null ? Local(sr.sprite) : Vector2.zero;
        if (sr.size.x > 0.0001f && sr.size.y > 0.0001f)
            return sr.size;
        return sr.sprite != null ? Local(sr.sprite) : Vector2.zero;
    }

    private static Vector2 Local(Sprite sprite)
    {
        Bounds b = sprite.bounds;
        return new Vector2(b.size.x, b.size.y);
    }

    // Drawn size in metres after the whole parent chain has had its say.
    public static Vector2 WorldBoxOf(SpriteRenderer sr)
    {
        Vector2 local = BoxOf(sr);
        Vector3 scale = sr.transform.lossyScale;
        return new Vector2(local.x * Mathf.Abs(scale.x), local.y * Mathf.Abs(scale.y));
    }

    public static void Forget(SpriteRenderer sr)
    {
        if (sr != null)
            Originals.Remove(sr);
    }
}
