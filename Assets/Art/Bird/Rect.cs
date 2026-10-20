using System.Collections.Generic;
using UnityEngine;

// ART-B1 (23.09, Yury): "У птицей должны быть текстуры в виде прозрачных прямоугольников."
// The birds render as SEMI-TRANSPARENT RECTANGLES — the placeholder idea of the owner's
// human-textures sheet: the simple scenes show the physics and the AI with no art noise,
// the real textures come back in "Уровень 1".
//
// One rectangle per visible part: the part's own white box sprite, which HumanSegment
// baked from the part's own bounds — so the rectangle IS the collider box and nothing is
// re-sized or re-scaled here. The part keeps its own hue (the species tint applied at
// build) and gets the placeholder fill alpha.
//
// The detailed bird look is the `Coat*` collage plus the face marks (eye, nictitating
// membrane, lid, crop, tongue, wattle, combs) that Bird.cs stacks on the same boxes:
//   * the collage is never built — the MarkPass in Art/Crow/Palette.cs asks this class
//     first and skips PaintTree/Dress when Enabled;
//   * the marks are hidden with `forceRenderingOff`, not with `enabled`: BirdController
//     writes `enabled` several times a second (blink, swallow, shout), so an `enabled`
//     write would come back on the next frame.
//
// PICTURE ONLY. No collider, joint, mass or Rigidbody2D is read or written here; the
// gate Assets/Editor/ArtBirdRectCheck.cs proves it by snapshotting a dressed and an
// undressed rig of the same kind.
public static class BirdArtRect
{
    // Owner's band is 0.35–0.5. The Bird.cs layout guide uses 0.5 for the plain
    // boxes; 0.42 leaves the parts readable over sky and grass without the parts
    // reading as one flat plate where they overlap.
    public const float FillAlpha = 0.42f;

    // ART-BW1: the fan's seams' tone. The spread is a pack of ~20 plates of ONE
    // hue and the fill alpha stacks: at the pack's own density the middle of the
    // fan reached near-black (the B-WING-11 reads' "dark overlap core") and every
    // crossing showed as a hard band (the "edge teeth"). The fan plates' fill now
    // carries the FAN'S OWN TONE — a light slate, the tone a single plate already
    // showed over the sky — instead of the near-black body hue, so the densest
    // stack cannot go darker than one faint plate and the seam steps shrink with
    // it. Hue only: the fill alpha (the owner's band), the geometry, the colliders
    // and the other parts' tints are untouched.
    //
    // This is the art lane's own fill and it is the LAST paint: DressParts runs
    // after CrowArt.Tint / BindPicture dressed the parts at build, so the tone set
    // here is final (the bW9 trap — a tint edit upstream, overridden at dress time
    // — cannot happen in this direction).
    //
    // BOTH wings' plates take the SAME tone. The rig's far/near rule (DimFar) is
    // not applied here: the two fans overlap on screen inside ~7 px, so a dimmed
    // far fan can only show as a dark sliver at the fan's own rim (measured
    // 24.09/25.09 — the far plates' single-layer tone already read equal to the
    // near ones' in the placeholder look, the dim showed in the dense areas only,
    // i.e. exactly where this round removes it).
    public static readonly Color FanFill = new Color(0.35f, 0.49f, 0.59f, 1f);

    // ART-BW3R: the LEGS' own side. Every part carries one hue, so the two legs rendered in
    // one tone and no still could say which leg was lifted (the bird lane's routed item,
    // 25.09: the step cycle reads in motion, but a still cannot name the leg). The
    // camera-side leg (Bird.cs names it "Right...") keeps its own hue; the far leg
    // ("Left...") takes this haze - the pale, low-contrast fill the universal 2D depth cue
    // gives the limb behind the body. Hue only: the alpha stays the owner's FillAlpha, the
    // boxes, the colliders and the fan's own tone (FanFill) are untouched.
    public static readonly Color LegFarFill = new Color(0.85f, 0.84f, 0.80f);

    // The Bird.cs marks that stand in for the chicken's face. Crow and woodpecker
    // build the eye pair only.
    private static readonly string[] Marks =
    {
        "Eye", "Nict", "Lid", "Crop", "Tongue", "Wattle", "CombFront", "CombBack"
    };

    // ─── ART-BW2: the fan plates' own FORM ───────────────────────────────────────────────
    // ART-BW1 closed with the tone landed and the reads naming the FORM instead: "blunt /
    // capsule plates", "blocky primitives", a serrated rear edge, and, verbatim from the
    // session's own files - "прямолинейные тупые прямоугольники без сужения к концам",
    // "острия - просто углы сдвинутых плит", "нет плавной градации формы", "absence of any
    // soft edge transition... only blocky primitives", "minimal variation in the taper
    // profile across the fan". So this pass draws the plate itself: the wedge silhouette
    // (Bird.cs, B-WING-5..10 - still the build's own picture and the physics runs' sprite)
    // is replaced for the DRESSED look by this canvas, the same last-paint slot the tone
    // uses. Sprite only - no collider, no joint, no drive value, no Bird.cs number is read
    // or written; the canvas honours the same contract the wedge did (picture width x the
    // plate's own length, pivot centred), so the plate's drawn box does not move.
    //
    // What the canvas answers, one item per read:
    //  * a feathered margin (PlateFeatherMeters, an anti-aliased band along every edge) -
    //    "no soft edge transition", the hard aliased tips;
    //  * a fuller root and a longer, drawn-out point whose last 10 % only fades to
    //    PlateTipFloor, so the tip stays readable - "no narrowing to the ends";
    //  * a three-shape family by the plate's own index (body taper, tip power, a curved
    //    leading side, the feather's own bend) - "minimal variation in the taper profile",
    //    the mechanical saw rhythm. ART-BW3 re-draws this family as the row's own RHYTHM -
    //    see the ART-BW3 block below;
    //  * each family slot's point could end a little deeper inside the box (tipInset, tried at
    //    1.2 % / 2.2 % of the length to stagger the tips' own line) - probe 14 priced the
    //    stagger out at the take's own scale (the rim's worst notch 19.9 -> 24.8 mm), so the
    //    7th element stands at 0: the row's rhythm is the family's job, not a sub-pixel shift.
    // The family was tuned twice in the round: the first pass took the reads' "washed /
    // ghost / motion-blur" verdict on its soft tips (the 0.45-coverage fade over the last
    // 10 %), so the point now holds PlateTipFloor = 0.72 and fades over 5-8 %; the feather
    // margin and every other element of the pass stand.
    //  * the picture width 58 -> 64 mm (40 -> 46 for the secondaries), so the neighbouring
    //    plates close in on each other - the visible gaps and the rim's notches. Priced on
    //    the bird lane's own notch instrument (Tools/_art-bw2-final.py, the tip insets at 0):
    //    mean 12.1 -> 8.8 mm, worst 19.4 -> 19.9 mm, 24 peaks for 20 plates.
    // The tone is untouched: FanFill, the fill alpha, the dark core.
    // ART-BW3: the picture width now lives in the beat's own rows (58/64/70 mm +- the
    // drift; priced on the same notch ruler: mean 8.8 -> 11.2 mm, worst 19.9 -> 18.9 mm); this
    // pair stays as the beat's centre and as the secondaries' own proportion (46/64).
    public const float PlateWidthPrimary = 0.064f;
    public const float PlateWidthSecondary = 0.046f;
    // The feathered margin, metres: the ramp every plate's edge ends with. 3.2 mm is ~2 px
    // at the take's own 635 px/m and ~8 px on the x4 sheet - soft, without dissolving the
    // silhouette (a 4.3 mm ramp priced as "weapon/мыло" in the round's own pricing reads).
    public const float PlateFeatherMeters = 0.0032f;
    // ART-BW3: the point's coverage floor (ART-BW2: one global 0.72) now lives in the beat's own
    // rows below - the needles hold 0.64, the full points 0.90 - and drifts down the row. The
    // band stays clear of the 0.45 the first ART-BW2 pass ran, which the reads called washed.

    // The ROW'S BEAT (ART-BW3), one row per slot: body taper (B, P), tip power, the leading-side
    // curve, the feather's own bend (x the length), how much of the length the point fades over,
    // the ink floor at the very point, and the picture's own width in mm (the drift moves it).
    // The three slots sit visibly apart so the contrast reads at the take's 635 px/m: A holds its
    // width then needles at the very end, C tapers early and stays wide and inky, B sits between.
    // ART-BW3's second pass: the tick the ROW is read by is the sliver's own WIDTH, so the width
    // ladder widened to 58/64/70 mm (+- the drift) - neighbouring slivers differ by ~4 mm, ~3 px
    // at the take's scale, and read as varied teeth. The other visible lever, the tip-length
    // ladder, was priced at worst-notch 24.8 mm (+4.9 mm - ART-BW2's own withdrawn amount) and
    // stays out.
    private static readonly float[][] RowBeat =
    {
        new[] { 0.62f, 1.15f, 3.10f, 0.17f, 0.028f, 0.085f, 0.64f, 58.0f },
        new[] { 0.68f, 1.45f, 2.20f, 0.12f, -0.020f, 0.050f, 0.76f, 64.0f },
        new[] { 0.58f, 0.95f, 1.40f, 0.22f, 0.018f, 0.030f, 0.90f, 70.0f },
    };

    // The DRIFT's own numbers, applied over the plate's index d (0 at the row's inner end, 1 at
    // the outer end): the point's power, the ink floor and the width move along the row.
    private const float TipPowerDrift = 0.50f;
    private const float InkFloorDrift = 0.10f;
    private const float WidthDriftMm = 1.0f;

    // ART-BW3: the beat's PHASE over the row. The old assignment was the plate's index mod three -
    // a perfect ABC stamp whose every third plate was identical. Here the beat advances one step
    // every four plates instead: 0 1 2 0 2 0 1 2 1 2 0 1 0 1 2 0 across the sixteen primaries
    // (counts 6/5/5), so no three-plate cycle repeats; with the drift, one slot four plates later
    // is never the same shape. Bird.BuildWingFan names the plates; the secondaries (four) run the
    // same beat over their own shorter row.
    public static int RowSlot(int k)
    {
        return (k + k / 4) % RowBeat.Length;
    }

    private static readonly Dictionary<string, Sprite> PlateSprites =
        new Dictionary<string, Sprite>();

    // The plate's canvas: white, the shape lives in alpha, drawn in the plate's own aspect
    // (picture width x length) at the wedge's 1024 texels/m, so the sprite measures exactly
    // what the wedge measured. Root row at the texture top, the tip at the bottom - the
    // wedge's own convention (the plate's local -Y is the tip). ART-BW3: the shape is the
    // plate's own place in the row - the beat slot from RowSlot(k), the drift from k over the
    // group's own row length (16 primaries, 4 secondaries).
    public static Sprite PlateCanvas(float length, int k, bool primary)
    {
        const float pixelsPerMeter = 1024f;
        int n = primary ? 16 : 4;
        float rowD = n > 1 ? k / (n - 1f) : 0f;
        float[] f = RowBeat[RowSlot(k)];
        float bodyB = f[0], bodyP = f[1], tipP = f[2], curve = f[3], bend = f[4], fade = f[5];
        float floor = f[6];
        float widthMm = f[7] + WidthDriftMm * (rowD - 0.5f);
        if (!primary)
            widthMm *= PlateWidthSecondary / PlateWidthPrimary;
        float width = widthMm * 0.001f;
        tipP += TipPowerDrift * (rowD - 0.5f);
        floor = Mathf.Clamp(floor + 0.05f - InkFloorDrift * rowD, 0.35f, 1f);
        int w = Mathf.Clamp(Mathf.RoundToInt(width * pixelsPerMeter), 8, 512);
        int h = Mathf.Clamp(Mathf.RoundToInt(length * pixelsPerMeter), 16, 2048);
        string key = w + "x" + h + (primary ? "P" : "S") + k;
        Sprite cached;
        if (PlateSprites.TryGetValue(key, out cached) && cached != null)
            return cached;

        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        // Bilinear, not the wedge's Point: the canvas' edge is a ramp a few texels wide and
        // the take samples it at 635 px/m, so the sampler must average, not pick.
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        Color[] px = new Color[w * h];
        float half = w * 0.5f;
        float ramp = Mathf.Max(1f, PlateFeatherMeters * pixelsPerMeter);
        for (int y = 0; y < h; y++)
        {
            float s = 1f - (float)y / (h - 1);    // 0 at the root edge, 1 at the box's tip edge
            float swell = Mathf.Sin(Mathf.PI * s);
            float hw = half * (1f - bodyB * Mathf.Pow(s, bodyP)) * (1f - Mathf.Pow(s, tipP));
            float off = bend * h * swell;                         // the feather's own curve
            float wide = hw * (1f + curve * swell);               // the leading side
            float narrow = hw * (1f - curve * 0.7f * swell);      // the trailing side
            float end = 1f;
            if (fade > 0.0001f && s > 1f - fade)
                end = Mathf.Lerp(floor, 1f, (1f - s) / fade);
            for (int x = 0; x < w; x++)
            {
                float d = x + 0.5f - half - off;
                float side = d >= 0f ? wide : narrow;
                float ad = Mathf.Abs(d);
                float a = ad <= side - ramp ? 1f : (ad >= side ? 0f : (side - ad) / ramp);
                px[y * w + x] = new Color(1f, 1f, 1f, a * end);
            }
        }
        tex.SetPixels(px);
        tex.Apply(false, false);
        Sprite created = Sprite.Create(tex, new Rect(0f, 0f, w, h), new Vector2(0.5f, 0.5f),
            w / width, 0, SpriteMeshType.FullRect);
        PlateSprites[key] = created;
        return created;
    }

    // The plate's own index off its name: "<side>Primary7" / "<side>Secondary2"; the family
    // slot is that index mod three, so neighbours differ and the fan's rhythm stops being
    // mechanical. Bird.BuildWingFan is the only namer.
    public static int PlateIndex(string partName)
    {
        if (string.IsNullOrEmpty(partName))
            return 0;
        int i = partName.Length;
        while (i > 0 && partName[i - 1] >= '0' && partName[i - 1] <= '9')
            i--;
        if (i >= partName.Length)
            return 0;
        int k;
        return int.TryParse(partName.Substring(i), out k) ? k : 0;
    }

    // The plate's picture swap. The plate's own length comes off the sprite it already
    // carries (the wedge measures picture width x length, the box sprite measures the box) -
    // no Bird.cs number is read. A physics-only run draws nothing, and the wedge is not
    // built there either, so no canvas is built: the picture costs a row take nothing.
    public static void DressPlate(SpriteRenderer sr, string partName)
    {
        if (sr == null || sr.sprite == null)
            return;
        if (HeadlessTrial.Active && !HeadlessTrial.VisualCapture)
            return;
        bool primary = partName.IndexOf("Primary", System.StringComparison.Ordinal) >= 0;
        float length = sr.sprite.bounds.size.y;
        if (length <= 0.001f)
            return;
        // ART-BW3: the canvas takes the plate's own row position - the beat slot and the drift
        // both come from the index and the group, and the picture's width with them.
        sr.sprite = PlateCanvas(length, PlateIndex(partName), primary);
        sr.drawMode = SpriteDrawMode.Simple;
    }

    // -1 = not forced (the RealArtEnabled flag decides), 0 = off, 1 = on.
    private static int _forced = -1;

    static BirdArtRect()
    {
        // A film or a still may ask for either look from the SAME player binary, so a
        // before/after pair is two runs of one build:  -birdRect 0 | -birdRect 1.
        string[] args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i + 1 < args.Length; i++)
        {
            if (args[i] != "-birdRect")
                continue;
            _forced = args[i + 1] == "0" ? 0 : 1;
            break;
        }
    }

    // ON: the birds are translucent rectangles. The "Уровень 1" flag
    // (ArtLibrary.RealArtEnabled, set by the gallery and by a real-art scene) brings the
    // detailed look back, and -birdRect overrides both for one run.
    public static bool Enabled
    {
        get { return _forced >= 0 ? _forced == 1 : !ArtLibrary.RealArtEnabled; }
        set { _forced = value ? 1 : 0; }
    }

    public static void Dress(Bird bird)
    {
        if (ReferenceEquals(bird, null))
            return;
        SuppressMarks(bird.transform);
        SuppressFan(bird.transform);
        DressParts(bird.transform);
    }

    // ART-B1 step 2 (req-183, owner 12.10): under the rect look the wing is its THREE
    // bone rectangles (humerus / ulna / hand) and nothing else. The fan's ~20 plates
    // per wing each got a plain box in step 1 (req-181), but twenty translucent boxes
    // stacked through each other read as a dark feathered smear, not as rectangles -
    // no stack of translucent rectangles can. So the plates are not drawn at all in
    // this look: renderer off AND forceRenderingOff, the marks' own pattern (the fan
    // keeps its transforms and its open/fold drive, so the dressed look - Уровень 1,
    // -birdRect 0 - still gets the wedge and the fan untouched). Picture only: no
    // collider, joint, mass or drive value is read or written here.
    public static void SuppressFan(Transform root)
    {
        SpriteRenderer[] srs = root.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < srs.Length; i++)
        {
            SpriteRenderer sr = srs[i];
            if (sr == null || !IsFanPlateVisual(sr))
                continue;
            sr.enabled = false;
            sr.forceRenderingOff = true;
        }
    }

    // The plate's own "Visual" box: the part is named <side>Primary<k> / <side>Secondary<k>
    // and its picture is the "Visual" child HumanSegment baked under it.
    public static bool IsFanPlateVisual(SpriteRenderer sr)
    {
        if (sr == null || !IsPart(sr.gameObject.name))
            return false;
        Transform parent = sr.transform.parent;
        return parent != null && IsFanPlate(parent.name);
    }

    // One translucent rectangle per part. The sprite is NOT swapped: the part's own
    // box sprite already measures the part's bounds, so changing it could only make
    // the rectangle wrong. Tint is the part's own hue, alpha is the placeholder fill.
    public static void DressParts(Transform root)
    {
        SpriteRenderer[] srs = root.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < srs.Length; i++)
        {
            SpriteRenderer sr = srs[i];
            if (sr == null || !IsPart(sr.gameObject.name))
                continue;
            Color c = sr.color;
            c.a = FillAlpha;
            sr.color = c;
        }
    }

    // The fan's plates: Bird.BuildWingFan names them <side>Primary<k> /
    // <side>Secondary<k> — the only parts whose own pack stacks to the fill's
    // full coverage, i.e. the only place the part hue becomes the picture's
    // darkest tone.
    public static bool IsFanPlate(string goName)
    {
        if (string.IsNullOrEmpty(goName))
            return false;
        return goName.IndexOf("Primary", System.StringComparison.Ordinal) >= 0
            || goName.IndexOf("Secondary", System.StringComparison.Ordinal) >= 0;
    }

    // ART-BW3R: the far leg's three parts. Bird.cs names them "<side>Leg<Thigh|Shank|Foot>"
    // with "Right" = the camera side (the Near hues) and "Left" = the far side (the Far
    // hues); the wing bones share the side prefix but no leg word, so they are not caught.
    public static bool IsFarLegPart(string goName)
    {
        if (string.IsNullOrEmpty(goName) || !goName.StartsWith("Left", System.StringComparison.Ordinal))
            return false;
        return goName.IndexOf("Thigh", System.StringComparison.Ordinal) >= 0
            || goName.IndexOf("Shank", System.StringComparison.Ordinal) >= 0
            || goName.IndexOf("Foot", System.StringComparison.Ordinal) >= 0;
    }

    public static void SuppressMarks(Transform root)
    {
        SpriteRenderer[] srs = root.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < srs.Length; i++)
        {
            SpriteRenderer sr = srs[i];
            if (sr == null || !IsMark(sr.gameObject.name))
                continue;
            sr.enabled = false;
            // BirdController re-enables the eye/lid/crop/tongue/wattle every frame it
            // animates them; forceRenderingOff is not a field it writes.
            sr.forceRenderingOff = true;
        }
    }

    public static bool IsMark(string goName)
    {
        if (string.IsNullOrEmpty(goName))
            return false;
        for (int i = 0; i < Marks.Length; i++)
            if (goName == Marks[i])
                return true;
        return false;
    }

    // The part rectangles are the "Visual" children HumanSegment bakes; every other
    // renderer under a bird is a mark, a Coat plate, a joint marker or a label.
    public static bool IsPart(string goName)
    {
        return goName == "Visual";
    }

    // What the artist reads at 1x: how many rectangles, which alpha, and that no mark
    // is still drawn. Used by the log line of the gate and by the report.
    public static string Audit(Transform root)
    {
        SpriteRenderer[] srs = root.GetComponentsInChildren<SpriteRenderer>(true);
        int parts = 0, marks = 0, drawn = 0, alphaBad = 0, boxBad = 0, other = 0;
        int plates = 0, platesDrawn = 0;
        float minA = 1f, maxA = 0f;
        float worstTexels = 0f;
        string worstBox = "none";
        string firstBoxBads = "";
        for (int i = 0; i < srs.Length; i++)
        {
            SpriteRenderer sr = srs[i];
            if (sr == null)
                continue;
            string goName = sr.gameObject.name;
            if (IsMark(goName))
            {
                marks++;
                if (sr.enabled || !sr.forceRenderingOff)
                    drawn++;
                continue;
            }
            if (!IsPart(goName))
            {
                other++;
                continue;
            }
            // ART-B1 step 2: a fan plate is suppressed like a mark, not counted as a rect.
            if (IsFanPlateVisual(sr))
            {
                plates++;
                if (sr.enabled || !sr.forceRenderingOff)
                    platesDrawn++;
                continue;
            }

            parts++;
            float a = sr.color.a;
            if (a < minA) minA = a;
            if (a > maxA) maxA = a;
            if (Mathf.Abs(a - FillAlpha) > 0.001f)
                alphaBad++;

            Transform parent = sr.transform.parent;
            BoxCollider2D box = parent != null ? parent.GetComponent<BoxCollider2D>() : null;
            if (box == null || sr.sprite == null)
                continue;
            Vector2 want = box.size;
            Vector2 have = sr.sprite.bounds.size;
            Vector3 s = sr.transform.lossyScale;
            have = new Vector2(have.x * Mathf.Abs(s.x), have.y * Mathf.Abs(s.y));
            float dx = Mathf.Abs(want.x - have.x);
            float dy = Mathf.Abs(want.y - have.y);
            float ppu = Mathf.Max(1f, sr.sprite.pixelsPerUnit);
            // HumanSegment bakes the box sprite on a 256 px/m grid and rounds the
            // texture to whole pixels, so the picture sits a texel or two off the
            // collider box (<= 2 mm at 1x). Judge the rectangle in the part's OWN
            // texels, not in metres, or the bake's rounding reads as a change.
            float texels = Mathf.Max(dx, dy) * ppu;
            if (texels > worstTexels)
            {
                worstTexels = texels;
                worstBox = (parent != null ? parent.name : "?") + " "
                    + (Mathf.Max(dx, dy) * 1000f).ToString("0.0") + "mm";
            }
            if (texels > 4f)
            {
                boxBad++;
                if (boxBad <= 3)
                    firstBoxBads += " [" + (parent != null ? parent.name : "?")
                        + " collider=" + want.x.ToString("0.0000") + "x" + want.y.ToString("0.0000")
                        + " sprite=" + have.x.ToString("0.0000") + "x" + have.y.ToString("0.0000")
                        + " texels=" + texels.ToString("0.0") + "]";
            }
        }
        return "rects=" + parts + " alpha=" + minA.ToString("0.00") + ".."
            + maxA.ToString("0.00") + " alphaBad=" + alphaBad + " boxBad=" + boxBad
            + " boxWorst=" + worstBox + "(" + worstTexels.ToString("0.0") + " texels)"
            + " marks=" + marks + " marksDrawn=" + drawn
            + " plates=" + plates + " platesDrawn=" + platesDrawn
            + " otherRenderers=" + other + firstBoxBads;
    }
}
