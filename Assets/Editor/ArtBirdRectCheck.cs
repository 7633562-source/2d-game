using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEngine;

// ART-B1 gate (23.09): the birds as translucent rectangles — a PICTURE-ONLY change.
//
// Rule zero, measured the way it has to be measured here: the trial player is rebuilt by
// other lanes mid-cycle, so no "before" CSV can be promised across a rebuild. This gate
// builds the SAME bird rig twice in the editor — once plain, once dressed by
// BirdArtRect — and compares the whole physics surface part by part: names,
// localPosition, localScale, collider size/offset/enabled, rigidbody mass, joint anchors
// and limits, and the component census. It also snapshots the DETAILED look (the Coat
// collage path MarkPass used to run) so the same instrument is shown to be sensitive to
// the picture while the physics surface does not move.
//
// Run:  Unity.exe -batchmode -projectPath <proj> -executeMethod ArtBirdRectCheck.Verify
//       -logFile <log> -quit      (see scripts/bird-rect-gate.sh)
public static class ArtBirdRectCheck
{
    private static int failures;

    public static void Verify()
    {
        failures = 0;
        Debug.Log("ART-B1-CHECK begin");
        CheckRig("chicken-flock", BirdKind.Chicken, BirdRig.Flock);
        CheckRig("crow-flock", BirdKind.Crow, BirdRig.Flock);
        CheckRig("chicken-ragdoll", BirdKind.Chicken, BirdRig.Ragdoll);
        CheckRig("crow-ragdoll", BirdKind.Crow, BirdRig.Ragdoll);
        ArtLibrary.RealArtEnabled = false;
        Debug.Log("ART-B1-CHECK end failures=" + failures);
        EditorApplication.Exit(failures == 0 ? 0 : 1);
    }

    private static void CheckRig(string tag, BirdKind kind, BirdRig rig)
    {
        Bird plain = Build(kind, rig, tag + "_plain");
        Bird control = Build(kind, rig, tag + "_control");
        string[] a = Snapshot(plain.transform);
        string[] b = Snapshot(control.transform);

        // The instrument's own control: two rigs of the same kind must read identical
        // before any picture work, or a later "0 diffs" would mean nothing.
        int controlDiff = Diff(a, b, false);
        Report(tag + ": two rigs of the same kind read the same physics (" + controlDiff
            + " differing lines of " + a.Length + ")", controlDiff == 0);

        string census = Census(plain.transform);
        int renderersBefore = plain.GetComponentsInChildren<SpriteRenderer>(true).Length;
        string[] pictureBefore = PictureLines(plain.transform);

        // The change under test: the placeholder dress.
        BirdArtRect.SuppressMarks(plain.transform);
        BirdArtRect.SuppressFan(plain.transform);
        BirdArtRect.DressParts(plain.transform);
        string[] after = Snapshot(plain.transform);
        string[] pictureAfter = PictureLines(plain.transform);
        int moved = Diff(a, after, true);
        int repainted = Diff(pictureBefore, pictureAfter, true);
        string audit = BirdArtRect.Audit(plain.transform);
        int rects = CountName(plain.transform, "Visual") - CountPlates(plain.transform);
        int marksDrawn = CountDrawnMarks(plain.transform);
        int platesDrawn = CountDrawnPlates(plain.transform);

        Debug.Log("ART-B1-CHECK INFO " + tag + " census[bodies=" + census + "] "
            + "renderers=" + renderersBefore + " " + audit);
        Report(tag + ": the placeholder rectangles do not move one body ("
            + moved + " differing lines of " + after.Length + ")", moved == 0);
        // The rectangle IS the part's own box sprite: the dress may change its colour and
        // hide marks, never its sprite, bounds, draw mode or scale.
        Report(tag + ": the dress leaves every sprite, its bounds, draw mode and scale alone ("
            + repainted + " differing lines of " + pictureAfter.Length + ")", repainted == 0);
        Report(tag + ": every part is one rectangle — rects=" + rects
            + ", alpha " + BirdArtRect.FillAlpha + " on all of them, marks drawn "
            + marksDrawn, rects > 0 && audit.IndexOf("alphaBad=0") >= 0 && marksDrawn == 0);
        Report(tag + ": no face mark is drawn — marksDrawn=" + marksDrawn, marksDrawn == 0);
        // ART-B1 step 2 (req-183): under the rect look the wing is its three bone boxes;
        // no fan plate is drawn (the wedge and the fan stay for the dressed look).
        Report(tag + ": no fan plate is drawn under the rect look — platesDrawn=" + platesDrawn
            + " of " + CountPlates(plain.transform), platesDrawn == 0);

        // The same instrument on the DETAILED look (the collage MarkPass ran before this
        // round): it must be picture-heavy and physics-silent too, or the snapshot above
        // would only be proving that nothing happened at all.
        Bird detailed = Build(kind, rig, tag + "_detailed");
        string[] dBefore = Snapshot(detailed.transform);
        int detailedRenderers = DressDetailed(detailed);
        string[] dAfter = Snapshot(detailed.transform);
        int detailedMoved = Diff(dBefore, dAfter, false);
        Report(tag + ": the detailed look adds renderers (" + renderersBefore + " -> "
            + detailedRenderers + ") and still moves no body (" + detailedMoved
            + " differing lines)", detailedRenderers > renderersBefore && detailedMoved == 0);

        Object.DestroyImmediate(plain.gameObject);
        Object.DestroyImmediate(control.gameObject);
        Object.DestroyImmediate(detailed.gameObject);
    }

    // The MarkPass path for a real-art scene: ChickenArt for the hen, CrowArt for the
    // crow. Returns the renderer count so the log can say how much picture came back.
    private static int DressDetailed(Bird bird)
    {
        if (bird.kind == BirdKind.Chicken)
        {
            ChickenArt.PaintTree(bird.transform);
            ChickenArt.Dress(bird.transform);
        }
        else
        {
            CrowArt.PaintTree(bird.transform);
            CrowArt.Dress(bird.transform);
        }
        return bird.GetComponentsInChildren<SpriteRenderer>(true).Length;
    }

    private static Bird Build(BirdKind kind, BirdRig rig, string name)
    {
        GameObject go = new GameObject("ArtBirdRectCheck_" + name);
        Bird bird = go.AddComponent<Bird>();
        bird.kind = kind;
        bird.rig = rig;
        bird.BuildBird();
        return bird;
    }

    // Everything a solver reads: the transform of the part, its collider, its
    // rigidbody mass, the joint that holds it, and its lights-out flags.
    private static string[] Snapshot(Transform root)
    {
        List<string> lines = new List<string>();
        HumanSegment[] parts = root.GetComponentsInChildren<HumanSegment>(true);
        for (int i = 0; i < parts.Length; i++)
        {
            HumanSegment p = parts[i];
            StringBuilder sb = new StringBuilder();
            sb.Append(p.gameObject.name).Append('|');
            sb.Append(V(p.transform.localPosition)).Append('|');
            sb.Append(V(p.transform.localScale)).Append('|');
            BoxCollider2D box = p.GetComponent<BoxCollider2D>();
            sb.Append(box == null ? "nobox"
                : V(box.size) + "/" + V(box.offset) + "/" + (box.enabled ? "1" : "0")
                  + "/" + (box.isTrigger ? "t" : "-")).Append('|');
            Rigidbody2D rb = p.GetComponent<Rigidbody2D>();
            sb.Append(rb == null ? "norb"
                : rb.mass.ToString("R", CultureInfo.InvariantCulture) + "/"
                  + rb.inertia.ToString("R", CultureInfo.InvariantCulture) + "/"
                  + rb.gravityScale.ToString("R", CultureInfo.InvariantCulture) + "/"
                  + rb.linearDamping.ToString("R", CultureInfo.InvariantCulture)).Append('|');
            HingeJoint2D joint = p.GetComponent<HingeJoint2D>();
            if (joint == null)
                sb.Append("nojoint");
            else
            {
                sb.Append(V(joint.anchor)).Append('/').Append(V(joint.connectedAnchor));
                sb.Append('/').Append(joint.useLimits
                    ? joint.limits.min.ToString("R", CultureInfo.InvariantCulture) + ".."
                      + joint.limits.max.ToString("R", CultureInfo.InvariantCulture)
                    : "nolimit");
                sb.Append('/').Append(joint.connectedBody == null
                    ? "nobody" : joint.connectedBody.gameObject.name);
            }
            lines.Add(sb.ToString());
        }
        lines.Sort(System.StringComparer.Ordinal);
        return lines.ToArray();
    }

    // The picture surface: what the part draws (sprite, its own bounds) and how (draw mode,
    // scale, order). The placeholder dress must not touch any of it - only the colour.
    private static string[] PictureLines(Transform root)
    {
        List<string> lines = new List<string>();
        SpriteRenderer[] srs = root.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < srs.Length; i++)
        {
            SpriteRenderer sr = srs[i];
            if (sr == null || !BirdArtRect.IsPart(sr.gameObject.name))
                continue;
            StringBuilder sb = new StringBuilder();
            sb.Append(sr.transform.parent != null ? sr.transform.parent.gameObject.name : "?");
            sb.Append('|').Append(sr.sprite != null ? sr.sprite.name : "null");
            sb.Append('|').Append(sr.sprite != null ? V((Vector2)sr.sprite.bounds.size) : "-");
            sb.Append('|').Append(sr.drawMode.ToString());
            sb.Append('|').Append(V(sr.transform.localScale));
            sb.Append('|').Append(sr.sortingOrder);
            lines.Add(sb.ToString());
        }
        lines.Sort(System.StringComparer.Ordinal);
        return lines.ToArray();
    }

    private static string Census(Transform root)
    {
        Component[] all = root.GetComponentsInChildren<Component>(true);
        Dictionary<string, int> counts = new Dictionary<string, int>();
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] == null)
                continue;
            string type = all[i].GetType().Name;
            if (type == "Transform" || type == "HumanSegment" || type == "SpriteRenderer"
                || type == "MeshRenderer" || type == "MeshFilter" || type == "TextMesh"
                || type == "Mesh" || type == "SphereCollider" || type == "Bird")
                continue;
            counts.TryGetValue(type, out int n);
            counts[type] = n + 1;
        }
        List<string> keys = new List<string>(counts.Keys);
        keys.Sort(System.StringComparer.Ordinal);
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < keys.Count; i++)
        {
            if (i > 0)
                sb.Append(',');
            sb.Append(keys[i]).Append(':').Append(counts[keys[i]]);
        }
        return sb.ToString();
    }

    private static int Diff(string[] a, string[] b, bool logFirst)
    {
        if (a.Length != b.Length)
            return Mathf.Abs(a.Length - b.Length) + 1;
        int n = 0;
        for (int i = 0; i < a.Length; i++)
        {
            if (a[i] == b[i])
                continue;
            if (logFirst && n == 0)
                Debug.Log("ART-B1-CHECK INFO first difference: before[" + a[i]
                    + "] after[" + b[i] + "]");
            n++;
        }
        return n;
    }

    private static int CountName(Transform root, string goName)
    {
        SpriteRenderer[] srs = root.GetComponentsInChildren<SpriteRenderer>(true);
        int n = 0;
        for (int i = 0; i < srs.Length; i++)
            if (srs[i].gameObject.name == goName)
                n++;
        return n;
    }

    private static int CountDrawnMarks(Transform root)
    {
        SpriteRenderer[] srs = root.GetComponentsInChildren<SpriteRenderer>(true);
        int n = 0;
        for (int i = 0; i < srs.Length; i++)
        {
            if (!BirdArtRect.IsMark(srs[i].gameObject.name))
                continue;
            if (srs[i].enabled || !srs[i].forceRenderingOff)
                n++;
        }
        return n;
    }

    private static int CountPlates(Transform root)
    {
        SpriteRenderer[] srs = root.GetComponentsInChildren<SpriteRenderer>(true);
        int n = 0;
        for (int i = 0; i < srs.Length; i++)
            if (BirdArtRect.IsFanPlateVisual(srs[i]))
                n++;
        return n;
    }

    private static int CountDrawnPlates(Transform root)
    {
        SpriteRenderer[] srs = root.GetComponentsInChildren<SpriteRenderer>(true);
        int n = 0;
        for (int i = 0; i < srs.Length; i++)
        {
            if (!BirdArtRect.IsFanPlateVisual(srs[i]))
                continue;
            if (srs[i].enabled || !srs[i].forceRenderingOff)
                n++;
        }
        return n;
    }

    private static string V(Vector2 v)
    {
        return v.x.ToString("R", CultureInfo.InvariantCulture) + ","
            + v.y.ToString("R", CultureInfo.InvariantCulture);
    }

    private static string V(Vector3 v)
    {
        return v.x.ToString("R", CultureInfo.InvariantCulture) + ","
            + v.y.ToString("R", CultureInfo.InvariantCulture) + ","
            + v.z.ToString("R", CultureInfo.InvariantCulture);
    }

    private static void Report(string label, bool ok)
    {
        if (ok)
            Debug.Log("ART-B1-CHECK OK " + label);
        else
        {
            Debug.LogError("ART-B1-CHECK FAIL " + label);
            failures++;
        }
    }
}
