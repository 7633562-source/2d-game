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
                c = FanFill;
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
