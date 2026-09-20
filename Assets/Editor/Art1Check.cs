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
        CheckFireLogs();
        CheckFlowerParts();
        CheckFlowerFoliage();
        CheckMaterialPeriod();
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

    // ART-0 (nineteenth pass): the campfire's three logs are painted art. The check
    // follows the code path the way CheckFlameCore does: `Campfire.LogArtKeys` is the
    // list `Campfire.DressLog` asks `ArtParts.Dress` -> `ArtLibrary.Tiled` for, the
    // canvas must be readable (Sprite.Create), its ASPECT must match the log's own box
    // (ArtParts.Dress maps the whole canvas Sliced onto it - a mismatched canvas is a
    // stretched log), and dressing a real log renderer must land the canvas on that
    // box with `localScale` untouched and the flat LogWood tint dropped.
    private static void CheckFireLogs()
    {
        for (int i = 0; i < Campfire.LogArtKeys.Length; i++)
        {
            string key = Campfire.LogArtKeys[i];
            Vector2 box = Campfire.LogBox(i);

            Texture2D tex = ArtLibrary.Albedo(key);
            Report("fire log Art/" + key + ".png (Albedo, Campfire.DressLog)", tex != null);
            if (tex == null)
                continue;

            Debug.Log("ART-1A-CHECK INFO Art/" + key + " " + tex.width + "x" + tex.height
                + " readable=" + tex.isReadable + " box=" + box.x.ToString("0.00") + "x"
                + box.y.ToString("0.00") + "m");
            Report("fire log Art/" + key + " readable (Sprite.Create needs CPU pixels)", tex.isReadable);

            float canvasAspect = tex.width / (float)tex.height;
            float boxAspect = box.x / box.y;
            Report("fire log " + key + " canvas aspect " + canvasAspect.ToString("0.000")
                + " vs log box " + boxAspect.ToString("0.000"),
                Mathf.Abs(canvasAspect - boxAspect) < 0.01f);

            // The sprite ArtParts.Dress builds for a Sliced log: tile period = the
            // part's own longest side, so one repeat spans the log and the imported
            // sprite IS the box.
            Sprite sprite = ArtLibrary.Tiled(key, Mathf.Max(box.x, box.y));
            Report("fire log " + key + " Sliced sprite maps onto the box", sprite != null
                && Mathf.Abs(sprite.bounds.size.x - box.x) < 0.01f
                && Mathf.Abs(sprite.bounds.size.y - box.y) < 0.01f);

            // End to end, on a renderer built exactly like BuildLogs builds one.
            GameObject go = new GameObject("Art1CheckLog_" + i);
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            HumanSegment.ApplyPicture(sr, box, HumanVisualShape.Box, null,
                Campfire.LogWood, Campfire.LogOrder);
            Vector3 scale = go.transform.localScale;
            bool dressed = Campfire.DressLog(sr, i);
            Report("fire log " + key + " DressLog puts the canvas on a log renderer",
                dressed && sr.sprite != null && sr.drawMode == SpriteDrawMode.Sliced
                && Mathf.Abs(sr.size.x - box.x) < 0.01f && Mathf.Abs(sr.size.y - box.y) < 0.01f
                && sr.color == Color.white);
            Report("fire log " + key + " dressing moves no localScale", go.transform.localScale == scale);
            // Rule zero, per part: whatever the dress does to a picture, the transform it
            // writes to must not carry the body. A Collider2D / Rigidbody2D on a dressed
            // renderer would make every picture change a physics change.
            Report("fire log " + key + " dressed renderer carries no body (rule zero)",
                sr.GetComponent<Collider2D>() == null && sr.GetComponent<Rigidbody2D>() == null);
            // ... and if it does move, say by how much: the assertion above was failing on
            // 20.09 with the logs dressing correctly (Sliced, right size, white tint), which
            // is a check that tells its reader nothing about the cause.
            if (go.transform.localScale != scale)
                Debug.Log("ART-1A-CHECK INFO fire log " + key + " localScale "
                    + scale.ToString("0.#####") + " -> " + go.transform.localScale.ToString("0.#####"));
            if (dressed && sr.sprite != null)
                Debug.Log("ART-1A-CHECK INFO fire log " + key + " dressed sprite=" + sr.sprite.name
                    + " mode=" + sr.drawMode + " size=" + sr.size.x.ToString("0.000") + "x"
                    + sr.size.y.ToString("0.000") + "m tint=" + sr.color.r.ToString("0.00") + ","
                    + sr.color.g.ToString("0.00") + "," + sr.color.b.ToString("0.00") + ","
                    + sr.color.a.ToString("0.00"));

            ArtParts.Forget(sr);
            Object.DestroyImmediate(go);
        }

        // The flat box the logs fall back to must stay the old picture: LogWood is a
        // half-transparent brown, which is the defect this pass replaced.
        Report("fire log fallback tint is still LogWood alpha 0.5",
            Mathf.Abs(Campfire.LogWood.a - 0.5f) < 0.001f);
    }

    // ─── diagnostic entry (ART-0 21st pass) ──────────────────────────────────────
    // The fire-log check reported `dressing moves no localScale` FAIL on all three logs
    // while every other assertion about them passed, so this walks the same mutations one
    // at a time on scratch renderers and logs the transform after each. Finding (20.09):
    // Unity keeps the current world box when a Simple renderer is converted to
    // Sliced/Tiled, and it does that by writing the TRANSFORM's scale. Sequence A (the
    // audit's own order) never touches `size`, so the factor is 1; sequence C (the order
    // ArtParts.Dress used to write: picture, size, then mode) writes 0.99849 / 1.00810 /
    // 0.99375 = the new canvas' own height over the height the old placeholder reported;
    // sequence B (mode first, then picture, then size) is the order the dress writes now
    // and leaves the scale at 1. Run:
    //   Unity.exe -batchmode -projectPath <proj> -executeMethod Art1Check.ScaleAudit -quit
    public static void ScaleAudit()
    {
        Debug.Log("ART-SCALE-AUDIT begin");
        for (int i = 0; i < Campfire.LogArtKeys.Length; i++)
        {
            string key = Campfire.LogArtKeys[i];
            Vector2 box = Campfire.LogBox(i);
            Debug.Log("ART-SCALE-AUDIT === log " + i + " key=" + key + " LogBox="
                + box.x.ToString("0.00000") + "x" + box.y.ToString("0.00000") + "m");

            // A: the order ArtParts.Dress really uses - sprite, size, then drawMode.
            GameObject a = new GameObject("ScaleAuditA_" + i);
            SpriteRenderer sa = a.AddComponent<SpriteRenderer>();
            HumanSegment.ApplyPicture(sa, box, HumanVisualShape.Box, null, Campfire.LogWood,
                Campfire.LogOrder);
            LogScale("A0 after ApplyPicture", sa, box);
            sa.sprite = ArtLibrary.Tiled(key, Mathf.Max(box.x, box.y));
            LogScale("A1 after sprite = Tiled(canvas)", sa, box);
            sa.size = ArtParts.BoxOf(sa);
            LogScale("A2 after size = BoxOf(sr)", sa, box);
            sa.drawMode = SpriteDrawMode.Sliced;
            LogScale("A3 after drawMode = Sliced", sa, box);
            sa.transform.localScale = Vector3.one;
            LogScale("A4 after localScale = one", sa, box);
            Object.DestroyImmediate(a);

            // B: drawMode first, then the sprite, then the size - the same end state
            // without ever assigning `size` while the renderer is Simple.
            GameObject b = new GameObject("ScaleAuditB_" + i);
            SpriteRenderer sb = b.AddComponent<SpriteRenderer>();
            HumanSegment.ApplyPicture(sb, box, HumanVisualShape.Box, null, Campfire.LogWood,
                Campfire.LogOrder);
            sb.drawMode = SpriteDrawMode.Sliced;
            LogScale("B1 after drawMode = Sliced", sb, box);
            sb.sprite = ArtLibrary.Tiled(key, Mathf.Max(box.x, box.y));
            LogScale("B2 after sprite = Tiled(canvas)", sb, box);
            sb.size = ArtParts.BoxOf(sb);
            LogScale("B3 after size = BoxOf(sr)", sb, box);
            Object.DestroyImmediate(b);

            // C: the real ArtParts.Dress order - the box is read BEFORE the sprite swap,
            // so `size` is assigned while drawMode is still Simple and the sprite on the
            // renderer is the tile, not the placeholder the size came from.
            GameObject c = new GameObject("ScaleAuditC_" + i);
            SpriteRenderer sc = c.AddComponent<SpriteRenderer>();
            HumanSegment.ApplyPicture(sc, box, HumanVisualShape.Box, null, Campfire.LogWood,
                Campfire.LogOrder);
            Vector2 pre = ArtParts.BoxOf(sc);
            Debug.Log("ART-SCALE-AUDIT ScaleAuditC_" + i + " pre-swap BoxOf="
                + pre.x.ToString("0.00000") + "x" + pre.y.ToString("0.00000"));
            sc.sprite = ArtLibrary.Tiled(key, Mathf.Max(pre.x, pre.y));
            LogScale("C1 after sprite = Tiled(canvas)", sc, box);
            sc.size = pre;
            LogScale("C2 after size = pre-swap BoxOf", sc, box);
            sc.drawMode = SpriteDrawMode.Sliced;
            LogScale("C3 after drawMode = Sliced", sc, box);
            Object.DestroyImmediate(c);

            // D: the candidate fix - the box is still read before the swap, but `size` is
            // assigned AFTER the renderer is Sliced, so nothing is written to the transform.
            GameObject d = new GameObject("ScaleAuditD_" + i);
            SpriteRenderer sd = d.AddComponent<SpriteRenderer>();
            HumanSegment.ApplyPicture(sd, box, HumanVisualShape.Box, null, Campfire.LogWood,
                Campfire.LogOrder);
            Vector2 preD = ArtParts.BoxOf(sd);
            sd.sprite = ArtLibrary.Tiled(key, Mathf.Max(preD.x, preD.y));
            sd.drawMode = SpriteDrawMode.Sliced;
            LogScale("D1 after sprite + drawMode = Sliced", sd, box);
            sd.size = preD;
            LogScale("D2 after size = pre-swap BoxOf", sd, box);
            Object.DestroyImmediate(d);
        }
        Debug.Log("ART-SCALE-AUDIT context HeadlessTrial.Active=" + HeadlessTrial.Active
            + " VisualCapture=" + HeadlessTrial.VisualCapture);

        // E: the case the tree/apple/flower rows are. A limb box is NOT the tile's own size,
        // so the two orders cannot end in the same place: the old order (size assigned while
        // Simple) let Unity preserve the world box of the TILE CANVAS, which is
        // tex.width/meters wide - 0.5 x 0.5 m for `bark` at the tree's 0.5 m period - while
        // the new order lands the canvas on the limb's own box. Measured, not argued.
        LimbAudit("bark", new Vector2(0.10f, 0.30f), ArtParts.BarkTileMeters);
        LimbAudit(ArtParts.FlowerPetalKey, new Vector2(0.029f, 0.036f), 0f);

        Debug.Log("ART-SCALE-AUDIT end");
        EditorApplication.Exit(0);
    }

    // One limb, both orders, end to end, on the real key and the real tile period.
    private static void LimbAudit(string key, Vector2 box, float tileMeters)
    {
        Debug.Log("ART-SCALE-AUDIT === limb key=" + key + " box=" + box.x.ToString("0.000")
            + "x" + box.y.ToString("0.000") + "m tileMeters=" + tileMeters.ToString("0.00"));

        GameObject oldGo = new GameObject("LimbAuditOld");
        SpriteRenderer oldSr = oldGo.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(oldSr, box, HumanVisualShape.Box, null, Color.white, 0);
        Vector2 preOld = ArtParts.BoxOf(oldSr);
        float localOld = tileMeters > 0.01f ? tileMeters : Mathf.Max(preOld.x, preOld.y);
        oldSr.sprite = ArtLibrary.Tiled(key, localOld);                  // old order: picture
        oldSr.size = preOld;                                             // size
        oldSr.drawMode = SpriteDrawMode.Sliced;                          // mode last
        LogScale("E-old (picture, size, mode)", oldSr, box);
        Object.DestroyImmediate(oldGo);

        GameObject newGo = new GameObject("LimbAuditNew");
        SpriteRenderer newSr = newGo.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(newSr, box, HumanVisualShape.Box, null, Color.white, 0);
        Vector2 preNew = ArtParts.BoxOf(newSr);
        float localNew = tileMeters > 0.01f ? tileMeters : Mathf.Max(preNew.x, preNew.y);
        newSr.drawMode = SpriteDrawMode.Sliced;                          // new order: mode
        newSr.sprite = ArtLibrary.Tiled(key, localNew);                  // picture
        newSr.size = preNew;                                             // size
        LogScale("E-new (mode, picture, size)", newSr, box);
        Object.DestroyImmediate(newGo);
    }

    private static void LogScale(string step, SpriteRenderer sr, Vector2 box)
    {
        Vector2 local = ArtParts.BoxOf(sr);
        Vector2 world = ArtParts.WorldBoxOf(sr);
        Debug.Log("ART-SCALE-AUDIT " + sr.gameObject.name + " " + step
            + " scale=" + sr.transform.localScale.x.ToString("0.00000") + ","
            + sr.transform.localScale.y.ToString("0.00000")
            + " sr.size=" + sr.size.x.ToString("0.00000") + "x" + sr.size.y.ToString("0.00000")
            + " BoxOf=" + local.x.ToString("0.00000") + "x" + local.y.ToString("0.00000")
            + " WorldBox=" + world.x.ToString("0.00000") + "x" + world.y.ToString("0.00000")
            + " mode=" + sr.drawMode
            + " LogBox=" + box.x.ToString("0.00000") + "x" + box.y.ToString("0.00000"));
    }

    // ART-0 gap 3 (20.09): the flower's bloom is painted art. The check follows the code
    // path the way CheckFlameCore / CheckFireLogs do: the five keys PlantFlower's petals
    // ask ArtParts.DressFlower for must resolve, be readable (Sprite.Create on the
    // Albedo path), and a real rig must end up carrying them - on the petal's own box,
    // with localScale untouched and the KIND's colour kept, because the canvases are grey
    // and the tint is what makes a rose red and a daisy white.
    private static void CheckFlowerParts()
    {
        // The rig is built here, in the editor, so arm real art for this check: the same
        // switch the gallery flips.
        ArtLibrary.RealArtEnabled = true;

        for (int i = 0; i < ArtParts.FlowerKeys.Length; i++)
        {
            string key = ArtParts.FlowerKeys[i];
            Texture2D tex = ArtLibrary.Albedo(key);
            Report("flower art Art/" + key + ".png (Albedo, ArtParts.DressFlower)", tex != null);
            if (tex == null)
                continue;
            Debug.Log("ART-1A-CHECK INFO Art/" + key + " " + tex.width + "x" + tex.height
                + " readable=" + tex.isReadable + " canvasAspect="
                + (tex.width / (float)tex.height).ToString("0.000"));
            Report("flower art Art/" + key + " readable (Sprite.Create needs CPU pixels)", tex.isReadable);
        }

        // A rose is an open fan (5 petals + the far-half PetalR + sepals, no disk); a daisy
        // is an 8-petal ring with a head plate. Two shapes, both matter.
        CheckFlowerRig(FlowerKind.Rose, 6);
        CheckFlowerRig(FlowerKind.Daisy, 8);

        // No silent fallback: a key that is not there must stay null.
        Report("flower art has no silent fallback (Albedo(\"Flower/no_such\") == null)",
            ArtLibrary.Albedo("Flower/no_such") == null);
    }

    private static void CheckFlowerRig(FlowerKind kind, int expectPetals)
    {
        GameObject go = new GameObject("Art1CheckFlower_" + kind);
        PlantFlower plant = (PlantFlower)go.AddComponent(PlantFlower.ComponentType(kind));
        plant.seed = 5;
        plant.BuildFlower();      // BuildFlower dresses itself while real art is armed
        Vector3 scale = go.transform.localScale;

        SpriteRenderer[] srs = go.GetComponentsInChildren<SpriteRenderer>(true);
        int petals = 0, realPetals = 0, badBox = 0, whiteTint = 0, tips = 0, sepals = 0, others = 0;
        for (int i = 0; i < srs.Length; i++)
        {
            SpriteRenderer sr = srs[i];
            string key = ArtParts.FlowerKeyForName(sr.gameObject.name);
            if (key == ArtParts.FlowerPetalKey)
            {
                petals++;
                string want = key.Substring(key.LastIndexOf('/') + 1);
                bool carriesCanvas = sr.sprite != null && sr.sprite.texture != null
                    && sr.sprite.texture.name == want;
                if (!carriesCanvas)
                    continue;
                realPetals++;
                if (sr.drawMode != SpriteDrawMode.Sliced)
                    badBox++;
                Vector2 box = ArtParts.BoxOf(sr);
                if (Mathf.Abs(sr.size.x - box.x) > 0.001f || Mathf.Abs(sr.size.y - box.y) > 0.001f)
                    badBox++;
                if (sr.color == Color.white)
                    whiteTint++;    // the kind's petal colour must survive the swap
            }
            else if (key == ArtParts.FlowerTipKey)
                tips++;
            else if (key == ArtParts.FlowerCalyxKey)
                sepals++;
            else
                others++;
        }

        Debug.Log("ART-1A-CHECK INFO flower " + kind + " renderers=" + srs.Length
            + " petals=" + petals + " withCanvas=" + realPetals + " tips=" + tips
            + " sepals=" + sepals + " other(spot/stem/leaf)=" + others
            + " parts=" + plant.PartCount + " stem=" + plant.StemHeight.ToString("0.00") + " m");
        Report("flower " + kind + " has its petal renderers (" + petals + ")", petals == expectPetals);
        Report("flower " + kind + " every petal carries Art/Flower/petal.png", realPetals == petals);
        Report("flower " + kind + " every petal keeps its own box (Sliced, size == box)", badBox == 0);
        Report("flower " + kind + " every petal keeps the kind colour (tint not white)", whiteTint == 0);
        Report("flower " + kind + " dressing moves no localScale", go.transform.localScale == scale);
        // Rule zero: a dressed flower renderer must not carry the body either (the plant is
        // decorative - a Collider2D here would make the petal canvas a physics object).
        int bodies = 0;
        for (int i = 0; i < srs.Length; i++)
        {
            if (srs[i].GetComponent<Collider2D>() != null || srs[i].GetComponent<Rigidbody2D>() != null)
                bodies++;
        }
        Report("flower " + kind + " dressed renderers carry no body (rule zero)", bodies == 0);

        // The gallery's comparison toggle has to be symmetric for flowers too: real=false
        // puts the flat collider box back, exactly as it was built.
        int restored = ArtParts.DressFlower(go.transform, false);
        int backToBox = 0;
        for (int i = 0; i < srs.Length; i++)
        {
            SpriteRenderer sr = srs[i];
            if (sr != null && ArtParts.FlowerKeyForName(sr.gameObject.name) == ArtParts.FlowerPetalKey
                && sr.sprite != null && sr.sprite.texture != null
                && sr.sprite.texture.name != "petal")
                backToBox++;
        }
        Debug.Log("ART-1A-CHECK INFO flower " + kind + " restore touched " + restored + " renderers, "
            + backToBox + " back on a box sprite");
        Report("flower " + kind + " real=false restores the flat box", backToBox == petals);

        for (int i = 0; i < srs.Length; i++)
            ArtParts.Forget(srs[i]);
        Object.DestroyImmediate(go);
    }

    // ART-0 gap 3b (20.09): the flower's FOLIAGE. The leaf part is a HumanSegment whose own
    // sprite the rig switches off behind a collage of 16 flat cards; the canvas takes that
    // sprite back and the cards step aside (all but the two aimed Petiole cards). Checked on
    // two kinds, one from each family: the canvas must be the one its box aspect asks for,
    // the part must keep its own box (Sliced, size == the box), the transform must not move,
    // and the gallery's toggle must put the collage back exactly as it was.
    private static void CheckFlowerFoliage()
    {
        ArtLibrary.RealArtEnabled = true;
        CheckLeafRig(FlowerKind.Rose, ArtParts.FlowerLeafKey, 2);
        CheckLeafRig(FlowerKind.Iris, ArtParts.FlowerLeafErectKey, 2);
        CheckLeafRig(FlowerKind.Sunflower, ArtParts.FlowerLeafKey, 4);
    }

    private static void CheckLeafRig(FlowerKind kind, string want, int expectLeaves)
    {
        GameObject go = new GameObject("Art1CheckLeaf_" + kind);
        PlantFlower plant = (PlantFlower)go.AddComponent(PlantFlower.ComponentType(kind));
        plant.seed = 11;
        plant.BuildFlower();
        Vector3 scale = go.transform.localScale;

        // The leaf PART is a HumanSegment named "Leaf_i"; its picture lives on a child
        // GameObject called "Visual" (HumanSegment.InitializeVisualOnly), so the part is
        // found by its component - the same way ArtParts.DressFlowerLeaves finds it.
        HumanSegment[] parts = go.GetComponentsInChildren<HumanSegment>(true);
        int leaves = 0, withCanvas = 0, badFamily = 0, badBox = 0, whiteTint = 0;
        int hiddenCards = 0, keptPetioles = 0, bodies = 0, petioleOff = 0, stillOn = 0;
        float aspect = 0f;
        Vector2 leafWorld = Vector2.zero;
        for (int i = 0; i < parts.Length; i++)
        {
            HumanSegment part = parts[i];
            if (part == null || part.visual == null || part.sprite == null)
                continue;
            if (!part.gameObject.name.StartsWith("Leaf", System.StringComparison.Ordinal))
                continue;

            leaves++;
            SpriteRenderer sr = part.sprite;
            Vector2 box = ArtParts.BoxOf(sr);
            aspect = box.y > 0.0001f ? box.x / box.y : 0f;
            leafWorld = ArtParts.WorldBoxOf(sr);
            if (ArtParts.FlowerLeafKeyFor(aspect) != want)
                badFamily++;
            string canvas = want.Substring(want.LastIndexOf('/') + 1);
            if (!sr.enabled || sr.sprite == null || sr.sprite.texture == null
                || sr.sprite.texture.name != canvas)
                continue;
            withCanvas++;
            if (sr.drawMode != SpriteDrawMode.Sliced)
                badBox++;
            if (Mathf.Abs(sr.size.x - box.x) > 0.001f || Mathf.Abs(sr.size.y - box.y) > 0.001f)
                badBox++;
            if (sr.color == Color.white)
                whiteTint++;

            // The collage under this part's Visual: every card off but the aimed stalk.
            SpriteRenderer[] cards = part.visual.GetComponentsInChildren<SpriteRenderer>(true);
            for (int c = 0; c < cards.Length; c++)
            {
                SpriteRenderer card = cards[c];
                if (card == null || card.gameObject == part.visual.gameObject)
                    continue;   // the leaf's own picture: the canvas lives on the Visual itself
                bool petiole = card.gameObject.name.IndexOf("Petiole",
                    System.StringComparison.OrdinalIgnoreCase) >= 0;
                if (petiole)
                {
                    if (card.enabled)
                        keptPetioles++;
                    else
                        petioleOff++;      // the aimed stalk must survive the dress
                }
                else if (card.enabled)
                    stillOn++;
                else
                    hiddenCards++;
                if (card.GetComponent<Collider2D>() != null
                    || card.GetComponent<Rigidbody2D>() != null)
                    bodies++;
            }
        }

        Debug.Log("ART-1A-CHECK INFO leaf " + kind + " leaves=" + leaves + " withCanvas="
            + withCanvas + " boxAspect=" + aspect.ToString("0.000") + " canvas=" + want
            + " cardsHidden=" + hiddenCards + " petiolesKept=" + keptPetioles
            + " leafBox=" + leafWorld.x.ToString("0.0000") + "x"
            + leafWorld.y.ToString("0.0000") + "m");
        Report("flower " + kind + " has its leaf parts (" + leaves + ")", leaves == expectLeaves);
        Report("flower " + kind + " every leaf part carries Art/" + want + ".png",
            withCanvas == leaves);
        Report("flower " + kind + " every leaf part draws the canvas its box aspect asks for",
            badFamily == 0 && withCanvas > 0);
        Report("flower " + kind + " every leaf part keeps its own box (Sliced, size == box)",
            badBox == 0);
        Report("flower " + kind + " every leaf part keeps the kind colour (tint not white)",
            whiteTint == 0);
        Report("flower " + kind + " the collage stepped aside (" + hiddenCards
            + " cards off, " + keptPetioles + " petioles kept, " + petioleOff + " stalks lost, "
            + stillOn + " still on)",
            stillOn == 0 && hiddenCards > 0 && keptPetioles > 0 && petioleOff == 0);
        Report("flower " + kind + " leaf dressing moves no localScale", go.transform.localScale == scale);
        Report("flower " + kind + " leaf renderers carry no body (rule zero)", bodies == 0);

        // The gallery's toggle, in reverse: the collage comes back, the canvas goes.
        ArtParts.DressFlower(go.transform, false);
        int backOn = 0, canvasOff = 0;
        for (int i = 0; i < parts.Length; i++)
        {
            HumanSegment part = parts[i];
            if (part == null || part.visual == null || part.sprite == null)
                continue;
            if (!part.gameObject.name.StartsWith("Leaf", System.StringComparison.Ordinal))
                continue;   // only the collage matters here: other parts' children are
                            // their own business (the bloom's spot cards, the disk plate)
            if (!part.sprite.enabled)
                canvasOff++;
            SpriteRenderer[] cards = part.visual.GetComponentsInChildren<SpriteRenderer>(true);
            for (int c = 0; c < cards.Length; c++)
            {
                if (cards[c] != null && cards[c].enabled
                    && cards[c].gameObject != part.visual.gameObject)
                    backOn++;
            }
        }
        Debug.Log("ART-1A-CHECK INFO leaf " + kind + " restore: canvasOff=" + canvasOff
            + " cardsBackOn=" + backOn);
        Report("flower " + kind + " real=false puts the collage back", backOn == hiddenCards
            + keptPetioles && canvasOff == leaves);

        SpriteRenderer[] all = go.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < all.Length; i++)
            ArtParts.Forget(all[i]);
        Object.DestroyImmediate(go);
    }

    // ART-0 22nd pass: a MATERIAL keeps its authored world period, so a part smaller than
    // one tile shows a CROP of the canvas at the canvas' own scale instead of the whole
    // canvas squeezed into the part. Measured on the shipped canvases: which draw mode the
    // dress chose, and how many source pixels of the canvas one part width shows (the
    // canvas' own density = no stretch; the whole 512 = Sliced's stretch).
    private static void CheckMaterialPeriod()
    {
        // The rig is built here, in the editor, so arm real art: the same switch the
        // gallery flips.
        ArtLibrary.RealArtEnabled = true;

        // A material part smaller than one tile (the tree lane's limb, a small leaf pad)
        // and a painted part of the same size (a flower petal): the first must be Tiled,
        // the second must stay Sliced, because its single canvas IS its picture.
        MaterialCase(ArtLibrary.Bark, new Vector2(0.10f, 0.30f), ArtParts.BarkTileMeters, true);
        MaterialCase(ArtLibrary.Leaf, new Vector2(0.12f, 0.08f), ArtParts.LeafTileMeters, true);
        MaterialCase(ArtParts.FlowerPetalKey, new Vector2(0.029f, 0.036f), 0f, false);

        // The real rig, built the way GalleryScene.BuildTreeRow builds its oak (Static rig,
        // the same dress call: wood bark, leaf leaf, no fruit).
        GameObject go = new GameObject("Art1CheckOak");
        PlantTree tree = go.AddComponent<PlantTree>();
        tree.kind = TreeKind.Oak;
        tree.seed = 11;
        tree.rig = TreeRig.Static;
        tree.BuildTree();

        SpriteRenderer[] srs = go.GetComponentsInChildren<SpriteRenderer>(true);
        int bodiesBefore = BodyCount(srs);
        Vector3 rootScale = go.transform.localScale;
        Vector3[] partScale = new Vector3[srs.Length];
        for (int i = 0; i < srs.Length; i++)
            partScale[i] = srs[i].transform.localScale;

        int dressed = ArtParts.DressAll(go.transform, true, ArtLibrary.Bark, ArtLibrary.Leaf, "");

        int barkParts = 0, leafParts = 0, notTiled = 0, movedScale = 0, empty = 0;
        int tiny = 0, small = 0, upper = 0, mid = 0, big = 0;
        float smallestBox = float.MaxValue, smallestCropPx = float.MaxValue;
        string[] smallestName = { "-", "-", "-" };
        float[] smallestOf = { float.MaxValue, float.MaxValue, float.MaxValue };
        for (int i = 0; i < srs.Length; i++)
        {
            SpriteRenderer sr = srs[i];
            if (sr == null || sr.sprite == null || sr.sprite.texture == null)
                continue;
            string tex = sr.sprite.texture.name;
            bool isBark = tex == ArtLibrary.Bark;
            if (!isBark && tex != ArtLibrary.Leaf)
                continue;

            Vector2 box = ArtParts.WorldBoxOf(sr);
            if (box.x <= 0.0001f || box.y <= 0.0001f)
            {
                empty++;
                continue;
            }
            if (isBark)
                barkParts++;
            else
                leafParts++;
            if (sr.drawMode != SpriteDrawMode.Tiled)
                notTiled++;
            if (sr.transform.localScale != partScale[i])
                movedScale++;
            float minSide = Mathf.Min(box.x, box.y);
            // The bands say how much of the rig the rule can even reach: a part under 0.02 m
            // of the 0.5 m period shows under 20 of the canvas' 512 px, so it reads as one
            // bark/leaf tone whichever draw mode it takes. Recorded so the claim is not wider
            // than the fix.
            if (minSide < 0.02f)
                tiny++;
            else if (minSide < 0.05f)
                small++;
            else if (minSide >= 0.30f)
                big++;
            else if (minSide >= 0.10f)
                mid++;
            else
                upper++;
            if (minSide < smallestBox)
                smallestBox = minSide;
            float period = isBark ? ArtParts.BarkTileMeters : ArtParts.LeafTileMeters;
            float cropPx = minSide * (sr.sprite.texture.width / period);
            if (cropPx < smallestCropPx)
                smallestCropPx = cropPx;
            for (int s = 0; s < smallestOf.Length; s++)
            {
                if (minSide < smallestOf[s])
                {
                    for (int t = smallestOf.Length - 1; t > s; t--)
                    {
                        smallestOf[t] = smallestOf[t - 1];
                        smallestName[t] = smallestName[t - 1];
                    }
                    smallestOf[s] = minSide;
                    smallestName[s] = sr.gameObject.name + "(" + (isBark ? "bark" : "leaf") + ")";
                    break;
                }
            }
        }

        Debug.Log("ART-1A-CHECK INFO oak dressed=" + dressed + " barkParts=" + barkParts
            + " leafParts=" + leafParts + " notTiled=" + notTiled + " empty=" + empty
            + " smallestBox=" + smallestBox.ToString("0.000") + "m"
            + " itsCrop=" + smallestCropPx.ToString("0.#") + "px of the 512 canvas"
            + " (a squeezed one would show all 512) bands=<0.02m:" + tiny
            + " 0.02-0.05m:" + small + " 0.05-0.10m:" + upper + " 0.10-0.30m:" + mid
            + " >=0.30m:" + big + " smallest=" + smallestName[0] + " "
            + smallestOf[0].ToString("0.000") + "m/" + smallestName[1] + " "
            + smallestOf[1].ToString("0.000") + "m/" + smallestName[2] + " "
            + smallestOf[2].ToString("0.000") + "m");

        Report("oak every bark/leaf part is Tiled (" + (barkParts + leafParts)
            + " parts, 0 in Sliced)", barkParts + leafParts > 0 && notTiled == 0);
        Report("oak dressing moves no localScale", go.transform.localScale == rootScale
            && movedScale == 0);
        Report("oak dressing adds no body (rule zero)",
            BodyCount(go.GetComponentsInChildren<SpriteRenderer>(true)) == bodiesBefore);

        for (int i = 0; i < srs.Length; i++)
            ArtParts.Forget(srs[i]);
        Object.DestroyImmediate(go);
    }

    // One part, dressed with the real rule. Prints the two numbers the picture depends on:
    // the draw mode, and the share of the canvas the part shows (natural scale vs stretch).
    private static void MaterialCase(string key, Vector2 box, float tileMeters, bool expectTiled)
    {
        Texture2D tex = ArtLibrary.Albedo(key);
        Report("material period " + key + " canvas resolves (Albedo)", tex != null);
        if (tex == null)
            return;

        GameObject go = new GameObject("MaterialCase_" + key.Replace("/", "_"));
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(sr, box, HumanVisualShape.Box, null, Color.white, 0);
        Vector3 before = sr.transform.localScale;
        // The box the dress will write: the placeholder's own reported box, which is the
        // declared box snapped to the placeholder's pixels (the 21st pass recorded the
        // 0.21-1.09 mm snap). The assertion below is that the dress adds NO transform
        // factor on top of it - the old bug wrote a 0.99849 scale here.
        Vector2 pre = ArtParts.BoxOf(sr);
        ArtParts.Dress(sr, key, tileMeters, true);

        Vector2 world = ArtParts.WorldBoxOf(sr);
        float period = tileMeters > 0.01f ? tileMeters : Mathf.Max(box.x, box.y);
        float canvasMeters = sr.sprite != null ? sr.sprite.bounds.size.x : 0f;
        float naturalPxPerM = tex.width / period;
        float shownPx = world.x > 0.0001f ? world.x * naturalPxPerM : 0f;
        float slicedPxPerM = world.x > 0.0001f ? tex.width / world.x : 0f;

        Debug.Log("ART-1A-CHECK INFO " + key + " box=" + box.x.ToString("0.000") + "x"
            + box.y.ToString("0.000") + "m period=" + tileMeters.ToString("0.00") + "m mode="
            + sr.drawMode + " canvas=" + canvasMeters.ToString("0.000") + "m drawn="
            + world.x.ToString("0.000") + "x" + world.y.ToString("0.000") + "m shows="
            + shownPx.ToString("0") + "/" + tex.width + "px at " + naturalPxPerM.ToString("0")
            + " px/m (Sliced would show " + tex.width + "px at " + slicedPxPerM.ToString("0")
            + " px/m, " + (slicedPxPerM / naturalPxPerM).ToString("0.0")
            + "x the canvas' scale) placeholderBox=" + pre.x.ToString("0.00000") + "x"
            + pre.y.ToString("0.00000") + "m");

        Report(key + " " + box.x.ToString("0.000") + "x" + box.y.ToString("0.000") + "m period "
            + tileMeters.ToString("0.00") + "m is drawn " + (expectTiled ? "Tiled" : "Sliced"),
            expectTiled ? sr.drawMode == SpriteDrawMode.Tiled : sr.drawMode == SpriteDrawMode.Sliced);
        Report(key + " dressing writes the box the part already had (no transform factor)",
            Mathf.Abs(world.x - pre.x) <= 0.0005f && Mathf.Abs(world.y - pre.y) <= 0.0005f);
        Report(key + " dressing moves no localScale", sr.transform.localScale == before);
        if (expectTiled)
            Report(key + " keeps the canvas' own scale (canvas " + canvasMeters.ToString("0.000")
                + " m > box " + Mathf.Min(box.x, box.y).ToString("0.000") + " m: a crop, not a squeeze)",
                canvasMeters >= Mathf.Min(box.x, box.y) - 0.0001f);

        ArtParts.Forget(sr);
        Object.DestroyImmediate(go);
    }

    private static int BodyCount(SpriteRenderer[] srs)
    {
        int n = 0;
        for (int i = 0; i < srs.Length; i++)
        {
            if (srs[i] == null)
                continue;
            if (srs[i].GetComponent<Collider2D>() != null
                || srs[i].GetComponent<Rigidbody2D>() != null)
                n++;
        }
        return n;
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
        Row(sb, "campfire log A (long)", "Art/Campfire/log_a.png");
        Row(sb, "campfire log B (medium)", "Art/Campfire/log_b.png");
        Row(sb, "campfire log C (short)", "Art/Campfire/log_c.png");
        sb.AppendLine("  campfire tongues -> procedural soft discs (intent: painted ones lost"
            + " the blind A/B, not a gap)");
        Row(sb, "flower petal", "Art/Flower/petal.png");
        Row(sb, "flower petal tip (distal lip)", "Art/Flower/petal_tip.png");
        Row(sb, "flower cup (tulip/lily/crocus/daffodil vessel)", "Art/Flower/cup.png");
        Row(sb, "flower calyx (sepal + sepal tip)", "Art/Flower/calyx.png");
        Row(sb, "flower disk (daisy/sunflower head plate)", "Art/Flower/disk.png");
        Row(sb, "flower foliage, broad leaflet pair (rose, daisy, sunflower, poppy, violet, lavender)",
            "Art/Flower/leaf.png");
        Row(sb, "flower foliage, erect blade pair (tulip, lily, daffodil, crocus, iris, bluebell)",
            "Art/Flower/leaf_erect.png");
        sb.AppendLine("  flower lavender spike (Spike_i) -> flat box, no canvas yet (ART-0 gap 3b)");
        sb.AppendLine("  flower autumn hip (Spent*) -> flat box, no canvas yet (ART-0 gap 3b)");
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
