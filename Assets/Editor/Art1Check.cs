using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// ART-1a gate: proves with Unity, not with a folder listing, that
//   1. every loader key the picture code asks for resolves (level, materials, parts);
//   2. Albedo / Tiled / Sample are wired, so the seven material tiles are really
//      consumed by drawing code (ART-0 left them as stubs);
//   3. a slab WIDER than one native tile is drawn Tiled and really repeats,
//      i.e. it is no longer stretched (ART-0 finding 3), while a slab that is
//      not wider keeps Sliced, exactly as before;
//   4. a key with no file returns null, which is what the labelled MISSING ART
//      tile keys off - there is no silent fallback.
//
// Run (bash, editor closed, WITHOUT -nographics: the repeat test is a render):
//   "F:/6000.5.8f1/Editor/Unity.exe" -batchmode -projectPath "E:/Games/2D Game/My project" \
//     -executeMethod Art1Check.Verify -logFile <log> -quit
// Exits 1 when anything is missing, so it can gate a build.
public static class Art1Check
{
    private static int failures;

    private static readonly string[] MaterialKeys =
    {
        ArtLibrary.Skin, ArtLibrary.Fabric, ArtLibrary.Denim, ArtLibrary.Dirt,
        ArtLibrary.Grass, ArtLibrary.Bark, ArtLibrary.Leaf
    };

    private static readonly string[] PartGroups = { "Human", "Dog", "Crow", "Chicken", "Hedgehog" };

    public static void Verify()
    {
        failures = 0;
        Debug.Log("ART-1A-CHECK begin");

        CheckLevelKeys();
        CheckMaterials();
        CheckParts();
        CheckStubsWired();
        CheckMissingContract();
        CheckFlameCore();
        CheckSlabStretch(8f);
        CheckSlabStretch(32f);
        CheckSlabStretch(6f);
        PrintInventory();

        Debug.Log("ART-1A-CHECK end failures=" + failures);
        EditorApplication.Exit(failures == 0 ? 0 : 1);
    }

    private static void CheckLevelKeys()
    {
        Report("Level: LevelArt.HasFill() (" + LevelArt.FillKey + ")", LevelArt.HasFill());
        Report("Level: LevelArt.HasCap() (" + LevelArt.CapKey + ")", LevelArt.HasCap());
        Report("Level: LevelArt.HasSign() (" + LevelArt.SignKey + ")", LevelArt.HasSign());
        Report("Level: LevelArt.Sky() != null (" + LevelArt.SkyKey + ")", LevelArt.Sky() != null);
        Report("Level: LevelArt.UseTiledFill()", LevelArt.UseTiledFill());
        Sprite fill = LevelArt.Fill();
        if (fill != null)
            Debug.Log("ART-1A-CHECK INFO fill sprite=" + fill.name
                + " native=" + fill.bounds.size.x.ToString("0.###") + "x"
                + fill.bounds.size.y.ToString("0.###") + "m ppu=" + fill.pixelsPerUnit);
    }

    private static void CheckMaterials()
    {
        for (int i = 0; i < MaterialKeys.Length; i++)
        {
            string key = MaterialKeys[i];
            Texture2D tex = ArtLibrary.Albedo(key);
            Report("material Art/" + key + ".png (Albedo)", tex != null);
            if (tex != null)
                Debug.Log("ART-1A-CHECK INFO Art/" + key + " " + tex.width + "x" + tex.height
                    + " readable=" + tex.isReadable);
            Sprite tile = ArtLibrary.Tiled(key, 0.5f);
            Report("material Art/" + key + " (Tiled 0.5 m)", tile != null);
            if (tile != null)
                Debug.Log("ART-1A-CHECK INFO tile " + key + " native="
                    + tile.bounds.size.x.ToString("0.###") + "m");
            Color sample = ArtLibrary.Sample(key, 0.25f, 0.75f);
            Debug.Log("ART-1A-CHECK INFO sample " + key + " @0.25,0.75 = "
                + sample.r.ToString("0.000") + "," + sample.g.ToString("0.000") + ","
                + sample.b.ToString("0.000"));
            if (tex != null)
                Report("material Art/" + key + " samples finite", sample.r >= 0f && sample.g >= 0f && sample.b >= 0f);
        }
    }

    private static void CheckParts()
    {
        string root = Path.Combine(Application.dataPath, "Resources", "Art");
        int files = Directory.Exists(root)
            ? Directory.GetFiles(root, "*.png", SearchOption.AllDirectories).Length
            : 0;
        Debug.Log("ART-1A-CHECK INFO Resources/Art PNG files=" + files);

        int missingParts = 0;
        int checkedParts = 0;
        for (int g = 0; g < PartGroups.Length; g++)
        {
            string groupDir = Path.Combine(root, PartGroups[g]);
            if (!Directory.Exists(groupDir))
            {
                Debug.LogError("ART-1A-CHECK FAIL missing group folder " + PartGroups[g]);
                failures++;
                continue;
            }
            string[] pngs = Directory.GetFiles(groupDir, "*.png");
            for (int i = 0; i < pngs.Length; i++)
            {
                string key = PartGroups[g] + "/" + Path.GetFileNameWithoutExtension(pngs[i]);
                checkedParts++;
                if (ArtLibrary.Albedo(key) == null)
                {
                    Debug.LogError("ART-1A-CHECK FAIL part key does not load: Art/" + key);
                    missingParts++;
                    failures++;
                }
            }
        }

        string[] extras = { ArtParts.FruitKey, "Apple/leaf", ArtParts.BerryKey,
                            ArtParts.StrawberryLeafKey, ArtParts.StrawberryStemKey };
        for (int i = 0; i < extras.Length; i++)
        {
            Report("part Art/" + extras[i] + ".png", ArtLibrary.Albedo(extras[i]) != null);
            checkedParts++;
        }

        Debug.Log("ART-1A-CHECK INFO part keys checked=" + checkedParts + " missing=" + missingParts);
    }

    private static void CheckStubsWired()
    {
        bool albedo = ArtLibrary.Albedo(ArtLibrary.Dirt) != null;
        bool tiled = ArtLibrary.Tiled(ArtLibrary.Dirt, ArtLibrary.GroundTileMeters) != null;
        Report("ART-0 stub ArtLibrary.Albedo(dirt) wired", albedo);
        Report("ART-0 stub ArtLibrary.Tiled(dirt) wired", tiled);
        Sprite tile = ArtLibrary.Tiled(ArtLibrary.Dirt, ArtLibrary.GroundTileMeters);
        if (tile != null)
            Report("ART-0 stub Tiled periods at " + ArtLibrary.GroundTileMeters + " m",
                Mathf.Abs(tile.bounds.size.x - ArtLibrary.GroundTileMeters) < 0.01f);
    }

    private static void CheckMissingContract()
    {
        Report("no silent fallback: Albedo(\"no/such/art\") == null",
            ArtLibrary.Albedo("no/such/art") == null);
        Report("no silent fallback: Tiled(\"no/such/art\") == null",
            ArtLibrary.Tiled("no/such/art", 1f) == null);
        Sprite checker = ArtMissing.Checker();
        Report("MISSING ART checker sprite exists", checker != null);
        if (checker != null)
        {
            Texture2D tex = checker.texture;
            Color a = tex.GetPixel(4, 4);
            Color b = tex.GetPixel(12, 4);
            bool twoTone = Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b) > 0.5f;
            Report("MISSING ART checker is two-tone (magenta/black)", twoTone);
        }
    }

    // ART-0 gap 2 (19.09): the campfire flame core is painted art. The check follows
    // the code path: "Campfire/core" is the key Campfire.CoreSprite() asks Albedo for,
    // and the texture must be readable, because the sprite is built with Sprite.Create.
    private static void CheckFlameCore()
    {
        const string key = "Campfire/core";
        const float boxW = 0.16f;
        const float boxH = 0.38f;

        Texture2D tex = ArtLibrary.Albedo(key);
        Report("flame core Art/" + key + ".png (Albedo, Campfire.CoreSprite)", tex != null);
        if (tex == null)
            return;

        Debug.Log("ART-1A-CHECK INFO Art/" + key + " " + tex.width + "x" + tex.height
            + " readable=" + tex.isReadable + " format=" + tex.format);
        Report("flame core readable (Sprite.Create needs CPU pixels)", tex.isReadable);

        // the core canvas must be in the core box's aspect, or the localScale stretch
        // (the campfire's own sizing device) distorts the silhouette
        float canvasAspect = tex.width / (float)tex.height;
        float boxAspect = boxW / boxH;
        Report("flame core canvas aspect " + canvasAspect.ToString("0.000") + " vs box "
            + boxAspect.ToString("0.000"), Mathf.Abs(canvasAspect - boxAspect) < 0.02f);

        // the tongues keep the soft disc on purpose - the painted ones lost the blind
        // A/B (Tools/gen-fire-textures.py). No tongue canvas may creep into the build.
        Report("flame tongues stay procedural (no Art/Campfire/tongue_*.png)",
            ArtLibrary.Albedo("Campfire/tongue_a") == null);

        // the exact call Campfire.CoreSprite() makes, on the imported texture: a readable
        // PNG becomes a 1 m sprite here, which is what makes the swap safe on WebGL
        Sprite core = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height),
            new Vector2(0.5f, 0.5f), tex.width, 0, SpriteMeshType.FullRect);
        Report("flame core Sprite.Create succeeds (the Campfire.CoreSprite path)", core != null);
        if (core != null)
        {
            Debug.Log("ART-1A-CHECK INFO flame core sprite native="
                + core.bounds.size.x.ToString("0.###") + "x" + core.bounds.size.y.ToString("0.###")
                + "m (localScale takes this to the 0.16x0.38 m core box)");
            Object.DestroyImmediate(core);
        }
    }

    // The ART-0 finding: a wide slab stretched the fill. Spawn the slab through
    // the real code path and ask the pixels whether the fill repeats at its
    // native 8 m period.
    private static void CheckSlabStretch(float width)
    {
        LevelPiece piece = default(LevelPiece);
        piece.name = "Art1CheckSlab_" + width;
        piece.center = Vector2.zero;
        piece.size = new Vector2(width, 2f);
        piece.color = Color.white;
        piece.kind = "";
        // A real slab carries a collider; without one StaticPlatform draws it as
        // decor (flat white) and the test would measure the wrong path.
        piece.hasCollider = true;

        GameObject go = StaticPlatform.Spawn(piece, null);
        SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
        Sprite sprite = sr.sprite;
        if (sprite == null)
        {
            Debug.LogError("ART-1A-CHECK FAIL slab " + width + "m has no sprite");
            failures++;
            Object.DestroyImmediate(go);
            return;
        }

        string mode = sr.drawMode.ToString();
        float native = sprite.bounds.size.x;
        bool widerThanTile = width > native + 0.001f;
        Report("slab " + width + "m drawMode " + (widerThanTile ? "Tiled (no stretch)" : "Sliced (unchanged)"),
            widerThanTile ? sr.drawMode == SpriteDrawMode.Tiled : sr.drawMode == SpriteDrawMode.Sliced);
        Debug.Log("ART-1A-CHECK INFO slab " + width + "m mode=" + mode + " native=" + native.ToString("0.###")
            + "m sprite=" + sprite.name);

        if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
        {
            Debug.Log("ART-1A-CHECK note repeat test for " + width + "m skipped: no graphics device");
            Object.DestroyImmediate(go);
            return;
        }

        if (!widerThanTile)
        {
            Object.DestroyImmediate(go);
            return;
        }

        float pxPerMeter = Mathf.Min(32f, 2048f / Mathf.Max(1f, width));
        int w = Mathf.RoundToInt(width * pxPerMeter);
        int h = Mathf.RoundToInt(2f * pxPerMeter);

        GameObject camGo = new GameObject("Art1CheckSlabCam");
        Camera cam = camGo.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 1f;
        cam.transform.position = new Vector3(0f, 0f, -10f);
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;

        RenderTexture rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
        rt.Create();
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        Texture2D read = new Texture2D(w, h, TextureFormat.RGBA32, false);
        read.ReadPixels(new Rect(0f, 0f, w, h), 0, 0);
        read.Apply();
        RenderTexture.active = null;
        cam.targetTexture = null;

        int period = Mathf.RoundToInt(native * pxPerMeter);
        int row = 3 * h / 4;
        float repeat = 0f;
        int samples = 0;
        for (int x = 2; x + period < w - 2; x += 7)
        {
            Color a = read.GetPixel(x, row);
            Color b = read.GetPixel(x + period, row);
            if (a.r + a.g + a.b < 0.02f || b.r + b.g + b.b < 0.02f)
                continue;
            repeat += Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b);
            samples++;
        }
        repeat = samples > 0 ? repeat / samples : -1f;
        bool repeats = period > 0 && period < w - 4 && repeat >= 0f && repeat < 0.02f;
        if (repeats)
            Debug.Log("ART-1A-CHECK OK slab " + width + "m repeats: period=" + period + "px ("
                + native.ToString("0.###") + "m) colourDiffAtOneTile=" + repeat.ToString("0.0000")
                + " samples=" + samples);
        else
        {
            Debug.LogError("ART-1A-CHECK FAIL slab " + width + "m does not repeat: period=" + period
                + "px colourDiffAtOneTile=" + repeat.ToString("0.0000") + " samples=" + samples);
            failures++;
        }

        Object.DestroyImmediate(read);
        Object.DestroyImmediate(go);
        Object.DestroyImmediate(camGo);
        rt.Release();
        Object.DestroyImmediate(rt);
    }

    // object -> texture -> real/missing, the same list the gallery prints.
    private static void PrintInventory()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("ART-1A-CHECK INVENTORY object -> texture -> real/missing");
        string[] parts = { "head", "neck", "torso", "pelvis", "upper", "lower", "hand", "thigh", "shin", "foot" };
        for (int i = 0; i < parts.Length; i++)
            Row(sb, "human part " + parts[i], "Art/Human/" + parts[i] + ".png");
        for (int g = 0; g < PartGroups.Length; g++)
        {
            if (PartGroups[g] == "Human")
                continue;
            string dir = Path.Combine(Application.dataPath, "Resources", "Art", PartGroups[g]);
            if (!Directory.Exists(dir))
                continue;
            string[] pngs = Directory.GetFiles(dir, "*.png");
            for (int i = 0; i < pngs.Length; i++)
            {
                string name = Path.GetFileNameWithoutExtension(pngs[i]);
                Row(sb, PartGroups[g].ToLowerInvariant() + " part " + name,
                    "Art/" + PartGroups[g] + "/" + name + ".png");
            }
        }
        Row(sb, "apple fruit", "Art/Apple/fruit.png");
        Row(sb, "apple leaf", "Art/Apple/leaf.png");
        Row(sb, "strawberry berry", "Art/Strawberry/berry.png");
        Row(sb, "strawberry leaf", "Art/Strawberry/leaf.png");
        Row(sb, "strawberry stem", "Art/Strawberry/stem.png");
        for (int i = 0; i < MaterialKeys.Length; i++)
            Row(sb, "material " + MaterialKeys[i], "Art/" + MaterialKeys[i] + ".png");
        Row(sb, "ground fill", "Level/ground_fill.png");
        Row(sb, "ground cap", "Level/ground_cap.png");
        Row(sb, "sky", "Level/sky.png");
        Row(sb, "finish sign", "Level/sign_finish.png");
        Row(sb, "campfire flame core", "Art/Campfire/core.png");
        sb.AppendLine("  campfire tongues -> procedural soft discs (intent: painted ones lost"
            + " the blind A/B, not a gap)");
        Row(sb, "flower", "MISSING (procedural)");
        Row(sb, "UI icons", "MISSING");
        Debug.Log(sb.ToString());
    }

    private static void Row(StringBuilder sb, string what, string texture)
    {
        // Resources.Load takes a path WITHOUT the file extension: asking for
        // "Art/Human/head.png" returns null and the row then says MISSING even
        // though the part loads (this inventory printed 65 false MISSING rows on
        // 19.09 while part keys checked=55 missing=0). Strip it, the same way
        // LevelKeyPresent does.
        string key = texture;
        if (key.StartsWith("Art/"))
            key = key.Substring("Art/".Length);
        else if (key.StartsWith("Level/"))
            key = key.Substring("Level/".Length);
        else
        {
            sb.AppendLine("  " + what + " -> " + texture + " -> MISSING");
            return;
        }

        if (key.EndsWith(".png", System.StringComparison.Ordinal))
            key = key.Substring(0, key.Length - 4);

        bool real = texture.StartsWith("Art/")
            ? ArtLibrary.Albedo(key) != null
            : LevelArt.Load(key) != null;
        sb.AppendLine("  " + what + " -> " + texture + " -> " + (real ? "real" : "MISSING"));
    }

    private static void Report(string label, bool ok)
    {
        if (ok)
            Debug.Log("ART-1A-CHECK OK " + label);
        else
        {
            Debug.LogError("ART-1A-CHECK FAIL " + label);
            failures++;
        }
    }
}
