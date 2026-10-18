using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// ART-H1 probe - THE JOINT OVERLAP ON FRAMES (Yury's sheet: "не должно быть разрывов").
//
// In Level 1, in edit mode, the mannequin is built the way the game builds it, dressed with
// the painted canvases, and then each joint is turned through its OWN limit range (read from
// the HingeJoint2D, never hard-coded). Two things come out:
//   * look frames  - the joint as the eye sees it, background = the game's night sky;
//   * measure      - the same pose with every other renderer off and a magenta sky: the
//                    frame is sampled inside the joint's own disc (radius = the narrowest
//                    neighbour's half-width at the joint) and the magenta pixels are counted.
//                    A count of 0 is the sheet's property: no background inside the joint.
//
// Edit mode on purpose: no FixedUpdate can fight the pose, and the level's own physics never
// starts. The scene is never saved.
public static class ArtHumanProbe
{
    private const string ScenePath = "Assets/Scenes/Level1.unity";
    private const string OutDir = "Trials/Runs/frames-arth1";
    private const int Width = 1920;
    private const int Height = 1080;
    private static readonly Color GapSky = new Color(1f, 0f, 1f, 1f);

    private sealed class Job
    {
        public string label;
        public string bone;
        public float degrees;
        public float ortho;
        public string jointA;      // the two neighbours of the joint
        public string jointB;
        public bool wholeBody;
    }

    private static Human human;
    private static Camera cam;
    private static readonly List<Job> jobs = new List<Job>();
    private static readonly List<string> report = new List<string>();
    private static int next;
    private static int warmup = 3;

    public static void Run()
    {
        Directory.CreateDirectory(OutDir);
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        GameObject go = new GameObject("ArtHumanProbe_Human");
        human = go.AddComponent<Human>();
        human.transform.position = new Vector3(0f, 0f, 0f);
        human.BuildHuman();

        string missingFirst;
        int missing = HumanArt.MissingCanvases(out missingFirst);
        int dressed = HumanArt.DressHuman(human.transform, true);
        report.Add("dress=" + dressed + " missing=" + missing + (missing > 0 ? " first=" + missingFirst : ""));
        foreach (HumanSegment part in human.GetComponentsInChildren<HumanSegment>(true))
        {
            SpriteRenderer sr = part.sprite;
            string drawn = sr != null && sr.sprite != null
                ? sr.sprite.bounds.size.x.ToString("0.####", CultureInfo.InvariantCulture) + "x" +
                  sr.sprite.bounds.size.y.ToString("0.####", CultureInfo.InvariantCulture)
                : "none";
            report.Add(string.Format(CultureInfo.InvariantCulture,
                "part {0,-14} box {1:0.####}x{2:0.####} drawn {3} ppu {4:0.#}",
                part.gameObject.name, part.size.x, part.size.y, drawn,
                sr != null && sr.sprite != null ? sr.sprite.pixelsPerUnit : 0f));
        }
        foreach (KeyValuePair<string, float[]> joint in Joints())
            report.Add(string.Format(CultureInfo.InvariantCulture,
                "joint {0,-16} limits {1:0.#} .. {2:0.#} deg", joint.Key, joint.Value[0], joint.Value[1]));

        cam = MakeCamera();
        PlanJobs();
        EditorApplication.update += Tick;
    }

    // The joint ranges come from the rig itself, so a probe frame can never leave the range
    // the physics will actually use.
    private static Dictionary<string, float[]> Joints()
    {
        Dictionary<string, float[]> limits = new Dictionary<string, float[]>();
        if (human == null)
            return limits;
        foreach (HumanSegment part in human.GetComponentsInChildren<HumanSegment>(true))
        {
            if (part.joint == null)
                continue;
            JointAngleLimits2D l = part.joint.limits;
            limits[part.gameObject.name] = new float[] { l.min, l.max };
        }
        return limits;
    }

    private static Camera MakeCamera()
    {
        GameObject go = new GameObject("ArtHumanProbe_Camera");
        Camera c = go.AddComponent<Camera>();
        c.orthographic = true;
        c.clearFlags = CameraClearFlags.SolidColor;
        c.backgroundColor = GapSky;
        c.nearClipPlane = 0.01f;
        c.farClipPlane = 100f;
        c.transform.position = new Vector3(0f, 0f, -10f);
        return c;
    }

    private static void PlanJobs()
    {
        Dictionary<string, float[]> limits = Joints();
        AddRange("knee", "RightLegShin", "RightLegThigh", "RightLegShin", limits, 5, 0.20f);
        AddRange("ankle", "RightLegFoot", "RightLegShin", "RightLegFoot", limits, 5, 0.15f);
        AddRange("hip", "RightLegThigh", "Pelvis", "RightLegThigh", limits, 4, 0.32f);
        AddRange("elbow", "RightArmLower", "RightArmUpper", "RightArmLower", limits, 4, 0.20f);
        AddRange("shoulder", "RightArmUpper", "Torso", "RightArmUpper", limits, 3, 0.32f);
        AddRange("toe", "RightLegToe", "RightLegFoot", "RightLegToe", limits, 3, 0.14f);

        for (int i = 0; i < 2; i++)
            jobs.Add(new Job
            {
                label = i == 0 ? "body-rest" : "body-step",
                bone = null,
                ortho = 1.15f,
                wholeBody = true,
                jointA = "Human",
                jointB = "Human",
            });
    }

    private static void AddRange(string label, string bone, string a, string b,
                                 Dictionary<string, float[]> limits, int count, float ortho)
    {
        float lo = 0f, hi = 0f;
        if (limits.ContainsKey(bone))
        {
            lo = limits[bone][0];
            hi = limits[bone][1];
        }
        for (int i = 0; i < count; i++)
        {
            float t = count == 1 ? 0f : (float)i / (count - 1);
            jobs.Add(new Job
            {
                label = label,
                bone = bone,
                degrees = Mathf.Lerp(lo, hi, t),
                ortho = ortho,
                jointA = a,
                jointB = b,
            });
        }
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
        PoseRig(job);
        Vector3 focus = FocusOf(job);
        cam.orthographicSize = job.ortho;
        cam.transform.position = new Vector3(focus.x, focus.y, -10f);

        // 1) the look frame, the game's own sky
        cam.backgroundColor = SceneLighting.NightSky;
        SetHideOthers(false);
        PhotoCapture.WritePng(cam, Width, Height, Path.Combine(OutDir, FrameName(job, "look")), out _);

        // 2) the measurement: only the mannequin, magenta sky, count what shows through
        cam.backgroundColor = GapSky;
        SetHideOthers(true);
        int gap = CountGap(focus, RadiusOf(job));
        SetHideOthers(false);

        report.Add(string.Format(CultureInfo.InvariantCulture,
            "frame {0,-22} deg {1,7:0.#} gap {2,6} px disc {3:0} px",
            FrameName(job, "").TrimEnd('-'), job.degrees, gap, DiscPixels(RadiusOf(job))));
        if (gap > 0)
            PhotoCapture.WritePng(cam, Width, Height, Path.Combine(OutDir, FrameName(job, "gap")), out _);
    }

    private static string FrameName(Job job, string suffix)
    {
        string name = job.label + "-" + job.degrees.ToString("0", CultureInfo.InvariantCulture);
        if (suffix.Length > 0)
            name += "-" + suffix;
        return name + ".png";
    }

    private static void PoseRig(Job job)
    {
        if (human == null)
            return;
        foreach (HumanSegment part in human.GetComponentsInChildren<HumanSegment>(true))
        {
            if (part.joint == null)
                continue;
            part.transform.localRotation = Quaternion.identity;
        }
        if (job.wholeBody)
        {
            if (job.label == "body-step")
            {
                SetBone("RightLegThigh", -32f);
                SetBone("RightLegShin", 46f);
                SetBone("RightLegFoot", -8f);
                SetBone("LeftLegThigh", 24f);
                SetBone("LeftLegShin", 12f);
                SetBone("RightArmUpper", 28f);
                SetBone("RightArmLower", -34f);
                SetBone("LeftArmUpper", -26f);
                SetBone("LeftArmLower", -20f);
            }
            return;
        }
        SetBone(job.bone, job.degrees);
    }

    private static void SetBone(string name, float degrees)
    {
        if (name == null || human == null)
            return;
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
        if (name == null || human == null)
            return null;
        foreach (HumanSegment part in human.GetComponentsInChildren<HumanSegment>(true))
            if (part.gameObject.name == name)
                return part;
        return null;
    }

    // Where the camera looks: the joint's own anchor in world space, so the frame is the
    // joint and not a guessed point on the limb.
    private static Vector3 FocusOf(Job job)
    {
        if (job.wholeBody)
            return human != null ? human.transform.position + new Vector3(0f, 0.75f, 0f) : Vector3.zero;
        HumanSegment bone = Segment(job.bone);
        if (bone == null)
            return Vector3.zero;
        HumanSegment parent = bone.joint != null ? bone.joint.connectedBody.GetComponent<HumanSegment>() : null;
        if (bone.joint == null || parent == null)
            return bone.transform.position;
        return bone.transform.TransformPoint(bone.joint.anchor);
    }

    // The joint's disc: radius = the NARROWER neighbour's half-width where it meets the
    // joint (the model's R_eye), so the sample is the area the two pictures must cover.
    private static float RadiusOf(Job job)
    {
        HumanSegment a = Segment(job.jointA);
        HumanSegment b = Segment(job.jointB);
        float ra = a != null && a.size.x > 0f ? a.size.x * 0.5f : 0.03f;
        float rb = b != null && b.size.x > 0f ? b.size.x * 0.5f : 0.03f;
        if (job.wholeBody)
            return 0f;
        return Mathf.Min(ra, rb);
    }

    private static float DiscPixels(float radiusMeters)
    {
        if (radiusMeters <= 0f)
            return 0f;
        return Mathf.PI * Mathf.Pow(radiusMeters * Height / (2f * cam.orthographicSize), 2f);
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
        Renderer[] renderers = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
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
        Vector3 sp = cam.WorldToScreenPoint(world);
        float cx = sp.x;
        float cy = sp.y;
        float r = radiusMeters * Height / (2f * cam.orthographicSize);
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
        Object.DestroyImmediate(tex);
        return gap;
    }

    private static void Finish()
    {
        File.WriteAllLines(Path.Combine(OutDir, "probe-report.txt"), report.ToArray(), Encoding.UTF8);
        int gaps = 0;
        foreach (string line in report)
        {
            if (!line.StartsWith("frame ", System.StringComparison.Ordinal))
                continue;
            string[] words = line.Split(' ');
            for (int i = 0; i < words.Length - 1; i++)
                if (words[i] == "gap" && words[i + 1] != "0")
                {
                    gaps++;
                    break;
                }
        }
        Debug.Log("ART-HUMAN-PROBE frames=" + jobs.Count + " gaps=" + gaps);
        foreach (string line in report)
            Debug.Log("ART-HUMAN-PROBE " + line);
        EditorApplication.update -= Tick;
        EditorApplication.Exit(0);
    }
}
