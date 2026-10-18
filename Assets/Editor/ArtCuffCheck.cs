using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// ART-H5f R4 probe - THE CUFF, IN THE ENGINE.
//
// Two questions, answered with the real renderer, not with a model:
//
//   A. THE BAND.  What does a Sliced SpriteRenderer actually draw when the sprite carries a
//      border on one end and the renderer's size is scaled down?  The thigh's own canvas is
//      rendered through a hand-built sprite (Sprite.Create with the border) at k=1 and k=0.6,
//      at 1:1 with the texture's own ppu, so the drawn rows can be matched against the
//      texture's rows row by row (Tools/ah5fr4-band-read.py reads the PNGs).
//
//   B. THE DISC.  With the dress at v=0 (control) and at v=0.4 (the flag's ceiling, covers
//      off = the cuff alone), each joint is turned through the walk-of-record's own range
//      (both signs, so the reading of the rig's sign convention cannot hide a pose) and the
//      magenta (background) pixels are counted inside the joint's own disc - ArtHumanProbe's
//      instrument, per value, same camera, same scene.
//
// Run (bash, editor closed):
//   TAG=baseline "F:/6000.5.8f1/Editor/Unity.exe" -batchmode -projectPath "E:/Games/2D Game/My project" \
//     -executeMethod ArtCuffCheck.Verify -logFile <log> -quit
// The tag names the output folder Trials/Runs/cuff-r4/<TAG>-*.
public static class ArtCuffCheck
{
    private const int Width = 1920;
    private const int Height = 1080;
    private static readonly Color GapSky = new Color(1f, 0f, 1f, 1f);

    private static string tag = "r4";
    private static string outDir;

    private sealed class Job
    {
        public float v;             // HeadlessTrial.Foreshorten for this phase
        public bool covers;         // HeadlessTrial.CoverJoints for this phase
        public string name;         // knee / elbow / hip / shoulder
        public string bone;         // the part whose localRotation is set
        public string parent;       // its parent (the joint's other neighbour)
        public float lo, hi;        // the walk of record's own range, degrees
        public float flip;          // +1 / -1
        public int step, steps;
        public float deg;
    }

    private static Human human;
    private static Camera cam;
    private static readonly List<Job> jobs = new List<Job>();
    private static readonly List<string> report = new List<string>();
    private static int next;
    private static int warmup = 4;
    private static int phase = -1;

    // Per-phase summaries: key = "v|joint|side|flip" -> (worst gap, deg, disc)
    private static readonly Dictionary<string, float[]> worst = new Dictionary<string, float[]>();
    private static readonly HashSet<string> savedExtreme = new HashSet<string>();
    private static int gapSaved;

    private static readonly string[] Sides = { "Right", "Left" };

    public static void Verify()
    {
        ParseArgs();
        outDir = Path.Combine("Trials", "Runs", "cuff-r4");
        Directory.CreateDirectory(outDir);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        cam = MakeCamera();
        report.Add("tag " + tag + " unity " + Application.unityVersion);
        BandTest();
        PlanHumanJobs();
        EditorApplication.update += Tick;
    }

    private static void ParseArgs()
    {
        string env = Environment.GetEnvironmentVariable("CUFF_TAG");
        if (!string.IsNullOrEmpty(env))
            tag = env;
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == "-cuffTag")
                tag = args[i + 1];
    }

    private static Camera MakeCamera()
    {
        GameObject go = new GameObject("ArtCuffCheck_Camera");
        Camera c = go.AddComponent<Camera>();
        c.orthographic = true;
        c.clearFlags = CameraClearFlags.SolidColor;
        c.backgroundColor = GapSky;
        c.nearClipPlane = 0.01f;
        c.farClipPlane = 100f;
        c.transform.position = new Vector3(0f, 0f, -10f);
        return c;
    }

    // ---------------------------------------------------------------- A. the band

    // The thigh's canvas is the sprite the game draws; a copy of it is built with a hand-set
    // border on the KNEE side (the texture's last rows) and rendered at 1:1 with its own ppu,
    // one frame per (border, k), with the renderer's bounds printed - the geometry the eye
    // cannot see.
    private static void BandTest()
    {
        Sprite canvas = Resources.Load<Sprite>(HumanArt.Home + "thigh");
        if (canvas == null)
        {
            report.Add("BAND FAILED: no thigh sprite at " + HumanArt.Home + "thigh");
            return;
        }
        report.Add(string.Format(CultureInfo.InvariantCulture,
            "band canvas {0}x{1} px ppu {2:0.#} pivot {3:0.#####},{4:0.#####} border {5} bounds {6:0.#####}x{7:0.#####}",
            canvas.texture.width, canvas.texture.height, canvas.pixelsPerUnit,
            canvas.pivot.x, canvas.pivot.y, Border(canvas), canvas.bounds.size.x, canvas.bounds.size.y));

        GameObject go = new GameObject("ArtCuffCheck_Band");
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.drawMode = SpriteDrawMode.Sliced;
        cam.backgroundColor = new Color(0f, 0f, 0f, 0f);   // transparent: the alpha is the picture
        //Sprite.pivot is read the way HumanArt reads it (box metres); Sprite.Create wants it
        // normalised inside the rect, so divide per axis when the value came in pixels.
        float px = canvas.pivot.x > 1.001f ? canvas.pivot.x / canvas.rect.width : canvas.pivot.x;
        float py = canvas.pivot.y > 1.001f ? canvas.pivot.y / canvas.rect.height : canvas.pivot.y;
        Vector2 pivot = new Vector2(px, py);
        report.Add(string.Format(CultureInfo.InvariantCulture, "band pivot normalized {0:0.#####},{1:0.#####}", px, py));

        int[] borders = { 0, 104, 150 };
        float[] ks = { 1.0f, 0.6f };
        for (int bi = 0; bi < borders.Length; bi++)
        {
            for (int ki = 0; ki < ks.Length; ki++)
            {
                int b = borders[bi];
                float k = ks[ki];
                Sprite sprite = b == 0
                    ? canvas
                    : Sprite.Create(canvas.texture, canvas.rect, pivot, canvas.pixelsPerUnit, 0,
                                    SpriteMeshType.FullRect, new Vector4(0f, b, 0f, 0f));
                sr.sprite = sprite;
                sr.size = new Vector2(canvas.bounds.size.x, canvas.bounds.size.y * k);
                cam.orthographicSize = Height / (2f * canvas.pixelsPerUnit);
                cam.transform.position = new Vector3(0f, 0f, -10f);
                string name = string.Format(CultureInfo.InvariantCulture, "band-b{0}-k{1:0.00}", b, k);
                PhotoCapture.WritePng(cam, Width, Height, Path.Combine(outDir, tag + "-" + name + ".png"), out string err);
                report.Add(string.Format(CultureInfo.InvariantCulture,
                    "band {0,-12} border {1,4} k {2:0.00} sr.size {3:0.#####}x{4:0.#####} bounds c {5:0.#####},{6:0.#####} s {7:0.#####}x{8:0.#####} {9}",
                    name, b, k, sr.size.x, sr.size.y,
                    sr.bounds.center.x, sr.bounds.center.y, sr.bounds.size.x, sr.bounds.size.y,
                    err == null ? "png ok" : "PNG FAIL " + err));
                if (b != 0)
                    UnityEngine.Object.DestroyImmediate(sprite);
            }
        }
        sr.sprite = null;
        UnityEngine.Object.DestroyImmediate(go);
        cam.backgroundColor = GapSky;
        Debug.Log("ART-CUFF-CHECK band test done");
    }

    private static string Border(Sprite s)
    {
        Vector4 b = s.border;
        return string.Format(CultureInfo.InvariantCulture, "{0:0.#},{1:0.#},{2:0.#},{3:0.#}", b.x, b.y, b.z, b.w);
    }

    // ---------------------------------------------------------------- B. the disc

    private static void PlanHumanJobs()
    {
        // The walk of record's own ranges (Tools/ah5fr2-angles-probe.py, c65post_walk.csv row
        // of record; the same table the bake and the sweep use).
        float[][] ranges = { new[] { 0f, 66f }, new[] { -54f, -15f }, new[] { -33f, 18f }, new[] { -14f, 28f } };
        string[] names = { "knee", "elbow", "hip", "shoulder" };
        string[] bones = { "LegShin", "ArmLower", "LegThigh", "ArmUpper" };
        string[] parents = { "LegThigh", "ArmUpper", "Pelvis", "Torso" };
        bool[] parentsSided = { true, true, false, false };
        float[] ortho = { 0.20f, 0.20f, 0.32f, 0.32f };
        float[] vs = EnvFloats("CUFF_VS", new float[] { 0f, 0.4f });
        bool[] covers = { false, true };
        HashSet<string> only = EnvSet("CUFF_JOINTS");
        int steps = Mathf.Max(2, EnvInt("CUFF_STEPS", 9));

        for (int p = 0; p < vs.Length; p++)
        {
            for (int c = 0; c < covers.Length; c++)
            {
                if (vs[p] <= 0f && c > 0)
                    continue;
                for (int j = 0; j < names.Length; j++)
                {
                    if (only != null && !only.Contains(names[j]))
                        continue;
                    for (int s = 0; s < Sides.Length; s++)
                    {
                        string sideBone = Sides[s] + bones[j];
                        string sideParent = parentsSided[j] ? Sides[s] + parents[j] : parents[j];
                        float sign = 1f;
                        for (int f = 0; f < 2; f++)
                        {
                            for (int i = 0; i < steps; i++)
                            {
                                float t = i / (float)(steps - 1);
                                jobs.Add(new Job
                                {
                                    v = vs[p], covers = covers[c], name = names[j],
                                    bone = sideBone, parent = sideParent,
                                    lo = ranges[j][0], hi = ranges[j][1],
                                    flip = sign, step = i, steps = steps,
                                    deg = Mathf.Lerp(ranges[j][0], ranges[j][1], t) * sign,
                                });
                            }
                            sign = -1f;
                        }
                    }
                }
            }
        }
    }

    private static HashSet<string> EnvSet(string name)
    {
        string v = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrEmpty(v))
            return null;
        return new HashSet<string>(v.Split(','));
    }

    private static int EnvInt(string name, int fallback)
    {
        string v = Environment.GetEnvironmentVariable(name);
        int parsed;
        return !string.IsNullOrEmpty(v) && int.TryParse(v, out parsed) ? parsed : fallback;
    }

    private static float[] EnvFloats(string name, float[] fallback)
    {
        string v = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrEmpty(v))
            return fallback;
        string[] parts = v.Split(',');
        float[] outv = new float[parts.Length];
        for (int i = 0; i < parts.Length; i++)
        {
            float parsed;
            if (!float.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out parsed))
                return fallback;
            outv[i] = parsed;
        }
        return outv;
    }

    private static void Tick()
    {
        if (warmup > 0)
        {
            warmup--;
            return;
        }
        if (next >= jobs.Count)
        {
            Finish();
            return;
        }

        Job job = jobs[next++];
        if (phase != PhaseOf(job))
        {
            BuildPhase(job);
            phase = PhaseOf(job);
        }
        if (human == null)
            return;

        PoseRig(job);
        Measure(job);
    }

    private static int PhaseOf(Job job)
    {
        int p = Mathf.RoundToInt(job.v * 100f);
        return job.covers ? p + 1000 : p;
    }

    private static void BuildPhase(Job job)
    {
        if (human != null)
        {
            UnityEngine.Object.DestroyImmediate(human.gameObject);
            human = null;
        }
        SetStatic("Foreshorten", job.v);
        SetStatic("CoverJoints", job.covers);
        SetStatic("RigOverlay", false);   // no joint markers / skeleton bars in the picture
        GameObject go = new GameObject("ArtCuffCheck_Human");
        human = go.AddComponent<Human>();
        human.transform.position = Vector3.zero;
        human.BuildHuman();
        int dressed = HumanArt.DressHuman(human.transform, true);
        report.Add(string.Format(CultureInfo.InvariantCulture,
            "phase v={0:0.00} covers={1} dressed={2} parts:", job.v, job.covers, dressed));
        foreach (HumanSegment part in human.GetComponentsInChildren<HumanSegment>(true))
        {
            SpriteRenderer sr = part.sprite;
            if (sr == null || sr.sprite == null)
                continue;
            string lim = "";
            if (part.joint != null)
            {
                JointAngleLimits2D l = part.joint.limits;
                lim = string.Format(CultureInfo.InvariantCulture, " limits {0:0.#}..{1:0.#} anchor {2:0.####},{3:0.####} cAnchor {4:0.####},{5:0.####}",
                    l.min, l.max, part.joint.anchor.x, part.joint.anchor.y,
                    part.joint.connectedAnchor.x, part.joint.connectedAnchor.y);
            }
            report.Add(string.Format(CultureInfo.InvariantCulture,
                "part {0,-14} boxY {1:0.#####} drawn {2:0.#####}x{3:0.#####} border {4} bounds c {5:0.#####},{6:0.#####} s {7:0.#####}x{8:0.#####} far {9}{10}",
                part.gameObject.name, part.size.y, sr.size.x, sr.size.y, Border(sr.sprite),
                sr.bounds.center.x, sr.bounds.center.y, sr.bounds.size.x, sr.bounds.size.y,
                ArtLibrary.IsFarSide(part.gameObject.name), lim));
        }
        // the eyes' own reference: the whole body per phase
        cam.orthographicSize = 1.15f;
        Vector3 focus = human.transform.position + new Vector3(0f, 0.75f, 0f);
        cam.transform.position = new Vector3(focus.x, focus.y, -10f);
        cam.backgroundColor = SceneLighting.NightSky;
        SetHideOthers(true);
        PhotoCapture.WritePng(cam, Width, Height,
            Path.Combine(outDir, tag + string.Format(CultureInfo.InvariantCulture, "-body-v{0:0.00}-c{1}.png", job.v, job.covers ? 1 : 0)), out string berr);
        SetHideOthers(false);
        report.Add(string.Format(CultureInfo.InvariantCulture, "body v={0:0.00} c={1} {2}", job.v, job.covers ? 1 : 0, berr == null ? "png ok" : "PNG FAIL " + berr));
        Debug.Log("ART-CUFF-CHECK phase v=" + job.v + " covers=" + job.covers + " dressed=" + dressed);
    }

    private static void PoseRig(Job job)
    {
        foreach (HumanSegment part in human.GetComponentsInChildren<HumanSegment>(true))
        {
            if (part.joint == null)
                continue;
            part.transform.localRotation = Quaternion.identity;
        }
        SetBone(job.bone, job.deg);
        SolveChain();
    }

    // The segments are SIBLINGS under the root (the physics re-solves the chain every step);
    // in edit mode nothing does, so rotating one part must be followed by a forward solve:
    // each segment's world transform is recomputed from its parent's anchor pair, exactly as
    // the hinge would hold it (reference angle 0, the build's own anchors).
    private static void SolveChain()
    {
        HumanSegment[] parts = human.GetComponentsInChildren<HumanSegment>(true);
        List<HumanSegment> order = new List<HumanSegment>();
        for (int i = 0; i < parts.Length; i++)
            if (parts[i].joint == null || parts[i].joint.connectedBody == null)
                order.Add(parts[i]);
        bool grew = true;
        while (grew)
        {
            grew = false;
            for (int i = 0; i < parts.Length; i++)
            {
                HumanSegment p = parts[i];
                if (order.Contains(p))
                    continue;
                HumanSegment parent = p.joint.connectedBody.GetComponent<HumanSegment>();
                if (parent == null || order.Contains(parent))
                {
                    order.Add(p);
                    grew = true;
                }
            }
        }
        for (int i = 0; i < order.Count; i++)
        {
            HumanSegment p = order[i];
            if (p.joint == null || p.joint.connectedBody == null)
                continue;   // the root keeps the built transform
            HumanSegment parent = p.joint.connectedBody.GetComponent<HumanSegment>();
            Quaternion rot = parent.transform.rotation * p.transform.localRotation;
            Vector3 hinge = parent.transform.TransformPoint(p.joint.connectedAnchor);
            Vector3 pos = hinge - rot * (Vector3)p.joint.anchor;
            p.transform.SetPositionAndRotation(pos, rot);
        }
    }

    private static void SetBone(string name, float degrees)
    {
        foreach (HumanSegment part in human.GetComponentsInChildren<HumanSegment>(true))
        {
            if (part.gameObject.name == name)
            {
                part.transform.localRotation = Quaternion.Euler(0f, 0f, degrees);
                return;
            }
        }
    }

    private static HumanSegment Segment(string name)
    {
        foreach (HumanSegment part in human.GetComponentsInChildren<HumanSegment>(true))
            if (part.gameObject.name == name)
                return part;
        return null;
    }

    private static void Measure(Job job)
    {
        HumanSegment bone = Segment(job.bone);
        HumanSegment parent = Segment(job.parent);
        if (bone == null || parent == null)
        {
            report.Add("gap MISSING PART " + job.bone + "/" + job.parent);
            return;
        }
        Vector3 world = bone.joint != null ? bone.transform.TransformPoint(bone.joint.anchor) : bone.transform.position;
        float radius = Mathf.Min(bone.size.x, parent.size.x) * 0.5f;
        float ortho = job.name == "knee" || job.name == "elbow" ? 0.20f : 0.32f;
        cam.orthographicSize = ortho;
        cam.transform.position = new Vector3(world.x, world.y, -10f);
        cam.backgroundColor = GapSky;
        SetHideOthers(true);
        int gap = CountGap(world, radius);
        float ppmDbg = Height / (2f * ortho);
        string dbg = string.Format(CultureInfo.InvariantCulture, " center {0:0.0},{1:0.0} world {2:0.#####},{3:0.#####} r {4:0.#####}",
            Width * 0.5f + (world.x - cam.transform.position.x) * ppmDbg,
            Height * 0.5f + (world.y - cam.transform.position.y) * ppmDbg,
            world.x, world.y, radius);
        if (gapSaved < 8 && gap * 2 > (int)(Mathf.PI * Mathf.Pow(radius * Height / (2f * ortho), 2f)))
        {
            gapSaved++;
            PhotoCapture.WritePng(cam, Width, Height,
                Path.Combine(outDir, tag + string.Format(CultureInfo.InvariantCulture, "-gapframe{0:00}.png", gapSaved)), out string ge);
            dbg += " gapframe" + gapSaved + (ge == null ? " ok" : " FAIL " + ge);
        }
        SetHideOthers(false);
        float disc = Mathf.PI * Mathf.Pow(radius * Height / (2f * ortho), 2f);
        string side = job.bone.StartsWith("Right") ? "Right" : "Left";
        string key = string.Format(CultureInfo.InvariantCulture, "{0:0.00}|{1}|{2}|{3}|{4:+0;-0}", job.v, job.covers ? 1 : 0, job.name, side, job.flip);
        float[] w;
        if (!worst.TryGetValue(key, out w))
            w = new float[] { -1f, 0f, disc };
        if (gap > w[0])
        {
            w[0] = gap; w[1] = job.deg; w[2] = disc;
        }
        worst[key] = w;
        report.Add(string.Format(CultureInfo.InvariantCulture,
            "gap v={0:0.00} c={1} {2,-8} {3,-5} flip{4:+0;-0} deg {5,8:0.0} gap {6,6} disc {7,0}{8}",
            job.v, job.covers ? 1 : 0, job.name, side, job.flip, job.deg, gap, disc, dbg));

        // one look frame per phase/joint/side at the range's far end, and the worst frame
        string ext = string.Format(CultureInfo.InvariantCulture, "v{0:0.00}-c{1}-{2}-{3}-deg{4:0}", job.v, job.covers ? 1 : 0, job.name, side, job.deg);
        if (job.flip > 0f && (job.step == 8 || job.step == 0) && savedExtreme.Add(ext))
        {
            cam.backgroundColor = SceneLighting.NightSky;
            PhotoCapture.WritePng(cam, Width, Height, Path.Combine(outDir, tag + "-look-" + ext + ".png"), out string e1);
            cam.backgroundColor = GapSky;
        }
    }

    private static readonly List<Renderer> Hidden = new List<Renderer>();

    private static void SetHideOthers(bool hidden)
    {
        if (human == null)
            return;
        if (!hidden)
        {
            for (int i = 0; i < Hidden.Count; i++)
                if (Hidden[i] != null)
                    Hidden[i].enabled = true;
            Hidden.Clear();
            return;
        }
        Hidden.Clear();
        Renderer[] renderers = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i].transform.IsChildOf(human.transform))
                continue;
            if (!renderers[i].enabled)
                continue;
            renderers[i].enabled = false;
            Hidden.Add(renderers[i]);
        }
    }

    private static int CountGap(Vector3 world, float radiusMeters)
    {
        if (radiusMeters <= 0f)
            return 0;
        Texture2D tex = PhotoCapture.Render(cam, Width, Height);
        if (tex == null)
            return -1;
        Color32[] px = tex.GetPixels32();
        // The frame is our own RT (Width x Height, square pixels): a metre is
        // Height/(2*ortho) pixels on BOTH axes. WorldToScreenPoint is useless here - it
        // measures the camera's own (editor default) pixel size, not the render target's.
        float ppm = Height / (2f * cam.orthographicSize);
        Vector3 cpos = cam.transform.position;
        float cx = Width * 0.5f + (world.x - cpos.x) * ppm;
        float cy = Height * 0.5f + (world.y - cpos.y) * ppm;
        float r = radiusMeters * ppm;
        int gap = 0;
        for (int y = Mathf.Max(0, (int)(cy - r)); y <= Mathf.Min(Height - 1, (int)(cy + r)); y++)
        {
            for (int x = Mathf.Max(0, (int)(cx - r)); x <= Mathf.Min(Width - 1, (int)(cx + r)); x++)
            {
                float dx = x + 0.5f - cx;
                float dy = y + 0.5f - cy;
                if (dx * dx + dy * dy > r * r)
                    continue;
                Color32 c = px[y * Width + x];
                if (c.r > 200 && c.g < 60 && c.b > 200)
                    gap++;
            }
        }
        UnityEngine.Object.DestroyImmediate(tex);
        return gap;
    }

    private static void SetStatic(string name, object value)
    {
        FieldInfo f = typeof(HeadlessTrial).GetField("<" + name + ">k__BackingField",
            BindingFlags.NonPublic | BindingFlags.Static);
        if (f != null)
        {
            f.SetValue(null, value);
            return;
        }
        PropertyInfo p = typeof(HeadlessTrial).GetProperty(name, BindingFlags.Public | BindingFlags.Static);
        if (p != null)
            p.GetSetMethod(true).Invoke(null, new object[] { value });
    }

    private static void Finish()
    {
        report.Add("--- worst per (v|covers|joint|side|flip) ---");
        List<string> keys = new List<string>(worst.Keys);
        keys.Sort();
        foreach (string key in keys)
        {
            float[] w = worst[key];
            report.Add(string.Format(CultureInfo.InvariantCulture,
                "worst {0,-32} gap {1,6:0} @ {2,8:0.0} deg disc {3,0}", key, w[0], w[1], w[2]));
        }
        File.WriteAllLines(Path.Combine(outDir, tag + "-report.txt"), report.ToArray(), Encoding.UTF8);
        Debug.Log("ART-CUFF-CHECK end jobs=" + jobs.Count);
        foreach (string line in report)
            Debug.Log("ART-CUFF-CHECK " + line);
        EditorApplication.update -= Tick;
        EditorApplication.Exit(0);
    }
}
