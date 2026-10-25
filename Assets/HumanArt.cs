using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

// ART-H1 - THE MANNEQUIN'S SEGMENT TEXTURES AND THE JOINT OVERLAP (Yury's sheet, 23.09).
//
// The ten canvases under Resources/Art/Human carry the segments' textures. Each canvas is
// LONGER than its shipping collider box: Tools/gen-human-textures.py grew it, on the plan
// Tools/art-h1-joint-fit.py verified, so that at a joint the parent's paint reaches its own
// box end and the NARROWER neighbour reaches past it by a skirt with a round tip. At every
// angle inside the joint's own limit range the joint's disc is painted by one of the two
// pictures, so no gap can show between two segments.
//
// The mapping is read from the canvas itself: the imported Sprite's own bounds are the
// DRAWN box in metres and its pivot is where the collider box sits inside it, so the dress
// sets sprite + draw mode + size and nothing else. No size is repeated here: regenerate the
// art and the dress follows.
//
// View only. The picture lives on the segment's Visual child (HumanSegment keeps its
// collider, rigidbody, hinge and the visual offset), so a dress moves no physics.
public static class HumanArt
{
    public const string Home = "Art/Human/";

    // Human.cs names segments sideName + part: RightArmUpper, LeftLegShin, RightLegToe...
    private static readonly string[] Keys =
    {
        "Pelvis", "Torso", "Neck", "Head",
        "Thigh", "Shin", "Foot", "Toe",
        "Upper", "Lower", "Hand",
    };

    private sealed class Saved
    {
        public Sprite sprite;
        public Color color;
        public SpriteDrawMode mode;
        public SpriteTileMode tile;
        public Vector2 size;
        public bool valid;
    }

    private static readonly Dictionary<SpriteRenderer, Saved> Originals =
        new Dictionary<SpriteRenderer, Saved>();

    // Which canvas belongs to a segment. Null for anything that is not a mannequin part.
    public static string KeyFor(string objectName)
    {
        if (string.IsNullOrEmpty(objectName))
            return null;
        for (int i = 0; i < Keys.Length; i++)
            if (objectName.IndexOf(Keys[i], System.StringComparison.Ordinal) >= 0)
                return Keys[i].ToLowerInvariant();
        return null;
    }

    // Dress every segment of one mannequin. Returns how many canvases landed; a segment
    // whose canvas is missing keeps its placeholder and is not counted, so a short count is
    // the caller's signal to show the missing-art marker instead of a silence.
    public static int DressHuman(Transform root, bool real)
    {
        if (root == null)
            return 0;
        HumanSegment[] parts = root.GetComponentsInChildren<HumanSegment>(true);
        int dressed = 0;
        for (int i = 0; i < parts.Length; i++)
        {
            HumanSegment part = parts[i];
            if (part == null)
                continue;
            string key = KeyFor(part.gameObject.name);
            if (string.IsNullOrEmpty(key))
                continue;
            SpriteRenderer sr = part.sprite;
            if (sr == null)
                continue;
            if (Dress(sr, Home + key, real, part.gameObject.name))
                dressed++;
        }
        // Route A (ART-H5 R2): the knee patch skin rides the dress; opt-in via -skinJoints
        // (default off) and picture-only - HumanSkin carries its own capture gates.
        HumanSkin.EnsureOn(root);
        // ART-H5f: the foreshortening rides the same dress, opt-in via -foreshorten.
        ForeshortenVisuals(root, real);
        // ART-H5f R3: the joint covers ride the same gate - a cover node exists only while
        // -foreshorten > 0, so the default picture keeps exactly today's surfaces.
        HumanCover.EnsureOn(root);
        return dressed;
    }

    // ART-H5f - THE FORESHORTENING (the owner's idea, 24.09). Visual-only prototype.
    //
    // -foreshorten <0..0.4> (default 0 = today): the picture of each THIGH and UPPER ARM
    // shrinks along its own bone axis by (1 - v) about the sprite pivot (the collider box
    // centre), so the segment reads as pointing out of the plane while the figure stays
    // 2D. Nothing else moves: the collider, rigidbody and hinge are untouched (the picture
    // lives on the child Visual), and the dress moves no wall clock.
    //
    // THE JOINT THAT MUST STAY PAINTED is the one the segment DRIVES - the knee, the
    // elbow. From R4 on, it is painted by THE PARENT'S OWN CUFF and not by the neighbour:
    // the owner's rule is "no neighbouring segment may stretch" (24.09), so the shin / the
    // lower arm keep their own picture untouched (nk = 1) - no grow, no lift.
    //
    // The cuff is the canvas' own distal skirt: the fit tool already paints the knee's /
    // the elbow's covering band into the last rows of thigh.png / upper.png (the paint
    // below the collider box' rest end). The canvases are declared to Unity with that band
    // as a SPRITE BORDER on the joint side (spriteBorder.y, the importer honours it and
    // scales it through the 512 px platform clamp), and this method draws every segment
    // Sliced (HumanArt.Dress). The renderer then keeps the border band at its own size
    // while the body above it compresses by k - measured, in the engine, by
    // Assets/Editor/ArtCuffCheck.cs + Tools/ah5fr4-band-read.py: at v=0.4 the band's rows
    // come out 1:1 with the source rows, anchored at the drawn rect's bottom edge, and the
    // drawn rect itself is the SAME box as without the border. The hip and the shoulder
    // need nothing: the pelvis' skirt and the torso's paint cover their discs at every
    // angle of the joint's range - the numbers are in Trials/Runs/arth5f-foreshorten
    // (Tools/arth5f-coverage.py) and Trials/Runs/cuff-r4.
    public const float ForeshortenMax = 0.40f;

    private static readonly string[] ForeshortenKeys = { "thigh", "upper" };
    private static readonly string[] ForeshortenNeighbours = { "shin", "lower" };
    // A marker the take runner can grep for in the built player (the R4 build gate).
    private const string CuffLogTag = "ART-H5f foreshorten";

    private static void ForeshortenVisuals(Transform root, bool real)
    {
        float v = Mathf.Clamp(HeadlessTrial.Foreshorten, 0f, ForeshortenMax);
        if (!real || v <= 0f || root == null)
            return;

        // Both sides are dressed by the same numbers, so the canvases are collected by key:
        // the near side's segment answers for the pair and the change lands on every part
        // of the key (near and far carry the same pictures).
        Dictionary<string, List<HumanSegment>> byKey = new Dictionary<string, List<HumanSegment>>();
        HumanSegment[] parts = root.GetComponentsInChildren<HumanSegment>(true);
        for (int i = 0; i < parts.Length; i++)
        {
            HumanSegment part = parts[i];
            if (part == null)
                continue;
            string key = KeyFor(part.gameObject.name);
            if (string.IsNullOrEmpty(key))
                continue;
            List<HumanSegment> list;
            if (!byKey.TryGetValue(key, out list))
            {
                list = new List<HumanSegment>();
                byKey[key] = list;
            }
            list.Add(part);
        }

        float k = 1f - v;
        for (int p = 0; p < ForeshortenKeys.Length; p++)
        {
            string key = ForeshortenKeys[p];
            string neighbourKey = ForeshortenNeighbours[p];
            List<HumanSegment> list, neighbours;
            if (!byKey.TryGetValue(key, out list) || !byKey.TryGetValue(neighbourKey, out neighbours))
                continue;
            if (list.Count == 0 || neighbours.Count == 0)
                continue;

            // the numbers come from the canvas itself (any part of the key carries it)
            Sprite canvas = list[0].sprite != null ? list[0].sprite.sprite : null;
            if (canvas == null)
                continue;

            Vector2 box = canvas.bounds.size;                       // the drawn box, metres
            Vector4 border = canvas.border;                         // the cuff on the joint side
            for (int i = 0; i < list.Count; i++)
                if (list[i].sprite != null && list[i].sprite.sprite != null)
                    list[i].sprite.size = new Vector2(list[i].sprite.size.x, box.y * k);

            // THE NEIGHBOUR IS NOT TOUCHED (nk = 1, no lift): the joint is the parent's
            // own cuff's job - the border band the renderer holds at its own size.
            Debug.Log(CuffLogTag + " v=" + v.ToString("0.###", CultureInfo.InvariantCulture)
                + " " + key + " size.y " + box.y.ToString("0.0000", CultureInfo.InvariantCulture)
                + "->" + (box.y * k).ToString("0.0000", CultureInfo.InvariantCulture)
                + " cuff border.y " + border.y.ToString("0.#", CultureInfo.InvariantCulture)
                + " (" + (border.y / canvas.pixelsPerUnit).ToString("0.0000", CultureInfo.InvariantCulture)
                + " m) " + neighbourKey + " untouched nk 1 parts " + list.Count + "/" + neighbours.Count);
        }
    }

    // One canvas onto one renderer. The draw mode is set BEFORE the sprite and the size:
    // Unity carries the world box across a Simple -> Sliced conversion by writing
    // localScale, and that is how a dressed part came out at canvas size before.
    public static bool Dress(SpriteRenderer sr, string resourceKey, bool real, string objectName)
    {
        if (sr == null)
            return false;
        Save(sr);

        if (!real)
        {
            Restore(sr);
            return true;
        }

        Sprite sprite = Resources.Load<Sprite>(resourceKey);
        if (sprite == null)
            return false;

        sr.drawMode = SpriteDrawMode.Sliced;
        sr.sprite = sprite;
        sr.size = sprite.bounds.size;   // the drawn box, from the canvas itself
        sr.color = ArtLibrary.IsFarSide(objectName) ? FarTone : Color.white;
        return true;
    }

    // ART-H4e (the owner's bar: the texture quality itself).  The far side goes OPAQUE.
    // Every look read named the translucent limb among the visible artifacts
    // ("полупрозрачный гостинг" - the sky showing through the far arm/leg; in the ankle crop
    // the ghost filled a third to a half of the frame), and a limb the background reads
    // through is not a material.  The depth cue stays the way a cutout keeps it: the SAME
    // tone the set used (ArtLibrary.DimFar's 0.70 multiplier), with the alpha pinned to 1 -
    // a solid, darker far limb instead of a ghost.  Only the renderer tint changes: the
    // canvases and the joint paint are untouched, so the seam measure cannot move.
    private static readonly Color FarTone = new Color(0.70f, 0.70f, 0.70f, 1f);

    public static void Restore(SpriteRenderer sr)
    {
        if (sr == null)
            return;
        Saved saved;
        if (!Originals.TryGetValue(sr, out saved) || !saved.valid)
            return;
        sr.drawMode = saved.mode;
        sr.sprite = saved.sprite;
        sr.size = saved.size;
        sr.tileMode = saved.tile;
        sr.color = saved.color;
    }

    private static void Save(SpriteRenderer sr)
    {
        if (Originals.ContainsKey(sr))
            return;
        Originals[sr] = new Saved
        {
            sprite = sr.sprite,
            color = sr.color,
            mode = sr.drawMode,
            tile = sr.tileMode,
            size = sr.size,
            valid = true,
        };
    }

    // Is every canvas of the set present on disk? The probe prints the answer so a missing
    // file shows up as a number instead of a silently undressed segment.
    public static int MissingCanvases(out string first)
    {
        first = null;
        int missing = 0;
        for (int i = 0; i < Keys.Length; i++)
        {
            string key = Keys[i].ToLowerInvariant();
            if (Resources.Load<Sprite>(Home + key) == null)
            {
                missing++;
                if (first == null)
                    first = Home + key;
            }
        }
        return missing;
    }
}
