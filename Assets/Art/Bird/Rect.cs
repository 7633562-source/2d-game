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
    //  * a three-shape family by the plate's own index (PlateFamily: body taper, tip power,
    //    a curved leading side, the feather's own bend) - "minimal variation in the taper
    //    profile", the mechanical saw rhythm;
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
    public const float PlateWidthPrimary = 0.064f;
    public const float PlateWidthSecondary = 0.046f;
    // The feathered margin, metres: the ramp every plate's edge ends with. 3.2 mm is ~2 px
    // at the take's own 635 px/m and ~8 px on the x4 sheet - soft, without dissolving the
    // silhouette (a 4.3 mm ramp priced as "weapon/мыло" in the round's own pricing reads).
    public const float PlateFeatherMeters = 0.0032f;
    // The drawn-out point's coverage at its very end (0 = gone, 1 = a hard needle). The first
    // pass ran 0.45 and the reads called the tips washed, ghostly, motion-blurred; 0.72 keeps
    // the point's ink while the margin still feathers it.
    public const float PlateTipFloor = 0.72f;

    // One row per family slot: body taper (B, P), tip power, the side curve, the feather's
    // own bend (x the length), how much of the length the point fades over, and how deep
    // inside the box the point ends (the tips' own stagger - 0 for every slot; the stagger
    // was withdrawn, see the probe-14 note above).
    private static readonly float[][] PlateFamily =
    {
        new[] { 0.62f, 1.15f, 1.95f, 0.14f, 0.022f, 0.06f, 0.000f },
        new[] { 0.68f, 1.45f, 2.20f, 0.12f, -0.018f, 0.05f, 0.000f },
        new[] { 0.58f, 1.00f, 2.50f, 0.17f, 0.014f, 0.08f, 0.000f },
    };

    private static readonly Dictionary<string, Sprite> PlateSprites =
        new Dictionary<string, Sprite>();

    // The plate's canvas: white, the shape lives in alpha, drawn in the plate's own aspect
    // (picture width x length) at the wedge's 1024 texels/m, so the sprite measures exactly
    // what the wedge measured. Root row at the texture top, the tip at the bottom - the
    // wedge's own convention (the plate's local -Y is the tip).
    public static Sprite PlateCanvas(float width, float length, int variant)
    {
        const float pixelsPerMeter = 1024f;
        int w = Mathf.Clamp(Mathf.RoundToInt(width * pixelsPerMeter), 8, 512);
        int h = Mathf.Clamp(Mathf.RoundToInt(length * pixelsPerMeter), 16, 2048);
        int v = ((variant % PlateFamily.Length) + PlateFamily.Length) % PlateFamily.Length;
        string key = w + "x" + h + "v" + v;
        Sprite cached;
        if (PlateSprites.TryGetValue(key, out cached) && cached != null)
            return cached;

        float[] f = PlateFamily[v];
        float bodyB = f[0], bodyP = f[1], tipP = f[2], curve = f[3], bend = f[4], fade = f[5];
        float ins = f[6];
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
            float s = 1f - (float)y / (h - 1);   // 0 at the root edge, 1 at the box's tip edge
            float se = s / (1f - ins);            // the plate's own span: the point ends at se = 1
            if (se > 1f) se = 1f;
            float swell = Mathf.Sin(Mathf.PI * se);
            float hw = half * (1f - bodyB * Mathf.Pow(se, bodyP)) * (1f - Mathf.Pow(se, tipP));
            float off = bend * h * swell;                         // the feather's own curve
            float wide = hw * (1f + curve * swell);               // the leading side
            float narrow = hw * (1f - curve * 0.7f * swell);      // the trailing side
            float end = 1f;
            if (fade > 0.0001f && se > 1f - fade)
                end = Mathf.Lerp(PlateTipFloor, 1f, (1f - se) / fade);
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
        float width = primary ? PlateWidthPrimary : PlateWidthSecondary;
        float length = sr.sprite.bounds.size.y;
        if (length <= 0.001f)
            return;
        sr.sprite = PlateCanvas(width, length, PlateIndex(partName));
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
        DressParts(bird.transform);
    }

    // One translucent rectangle per part. The sprite is NOT swapped: the part's own
    // box sprite already measures the part's bounds, so changing it could only make
    // the rectangle wrong. Tint keeps the part's own hue, alpha becomes the
    // placeholder fill — except the fan's plates, whose hue is the fan's own tone
    // (ART-BW1): their pack stacks to the fill's full coverage, where the part hue
    // read as a near-black core.
    public static void DressParts(Transform root)
    {
        SpriteRenderer[] srs = root.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < srs.Length; i++)
        {
            SpriteRenderer sr = srs[i];
            if (sr == null || !IsPart(sr.gameObject.name))
                continue;
            Color c = sr.color;
            Transform part = sr.transform.parent;
            string partName = part != null ? part.name : sr.gameObject.name;
            if (IsFanPlate(partName))
            {
                c = FanFill;
                // ART-BW2: the plate's own picture - see PlateCanvas above.
                DressPlate(sr, partName);
            }
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
            + " otherRenderers=" + other + firstBoxBads;
    }
}
