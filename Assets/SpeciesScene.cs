using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

// One-species plant scene (Yury 18.09: a scene per species, apple first). The
// scene copies Tree.unity's spawn config and narrows it to ONE kind; this
// component owns the camera, so the whole tree - not its trunk, not its crown -
// is inside the frame whatever height the seed produced (2 to 5 m).
//
// The framing is recomputed every frame from the live sprite bounds, so a sway
// cannot walk the crown out of the picture. Picture only: the camera and one
// optional PNG are all this touches.
//
//   -capture 1 -label treeapple -out <folder> -w 1920 -h 1080 -observe 6
public class SpeciesScene : MonoBehaviour
{
    [Serializable]
    private class ReportDto
    {
        public string label;
        public string scene;
        public string species;
        public string unityVersion;
        public string buildGuid;
        public string capturedUtc;
        public int treeCount;
        public int plantTreeCount;
        public int appleTreeCount;
        public float heightMeters;
        public float widthMeters;
        public float groundY;
        public float orthoSize;
        public bool fits;
        public float minViewportX;
        public float minViewportY;
        public float maxViewportX;
        public float maxViewportY;
        public float outsideMeters;
        public float paintedFraction;
        public float fitFractionOverRun;
        public string argv;
        public int observedFrames;
        public int missFrames;
        public int warmupFrames;
        public float firstMissSeconds;
        public float firstMissOutsideMeters;
        public int missFramesAfterOneSecond;
        public float boundsGrowthMeters;
        public float dressOutsideMeters;
        public float statsFromSeconds;
        public int width;
        public int height;
    }

    [Header("Species")]
    [Tooltip("Which rig the spawn config builds. \"apple\" expects AppleTree.")]
    public string speciesName = "apple";
    public float margin = 0.10f;

    [Header("Capture")]
    public bool captureStill = false;
    public string label = "treeapple";
    public int photoWidth = 1920;
    public int photoHeight = 1080;
    public float settleSeconds = 3f;
    public float observeSeconds = 6f;
    public bool realArt = true;

    // The one tree this scene spawned: the apple rig first, the plant gallery
    // rig second, so the same component serves TreeOak, TreePine...
    private Transform FindTree()
    {
        AppleTree[] apples = FindObjectsByType<AppleTree>(FindObjectsSortMode.None);
        if (apples.Length > 0 && apples[0] != null)
            return apples[0].transform;
        PlantTree[] plants = FindObjectsByType<PlantTree>(FindObjectsSortMode.None);
        if (plants.Length > 0 && plants[0] != null)
            return plants[0].transform;
        return null;
    }

    private Camera cam;
    private Transform framed;
    private int missFrames;
    private int observedFrames;
    private int warmupFrames;
    private bool dressed;
    private int dressedCount;

    // The frame the real art lands on is warm-up, not a framing failure: the
    // dress pass replaces a collider-sized rectangle with the shipping plate, the
    // tree's box grows, and the camera still holds the frame it computed for the
    // placeholder. It is re-framed in the same LateUpdate; the next frame counts.
    // Both the warm-up count and how far outside the old frame the dressed box
    // reached are recorded, so nothing is hidden by the exclusion.
    private float firstMissSeconds = -1f;
    private float firstMissOutsideMeters;
    private int missFramesAfterOneSecond;
    private float firstMeasuredSizeY;
    private float firstMeasuredSizeX;
    private float dressOutsideMeters;
    private float statsFromSeconds = -1f;

    void Start()
    {
        captureStill = GetFloat("-capture", captureStill ? 1f : 0f) > 0.5f;
        label = GetString("-label", label);
        photoWidth = Mathf.RoundToInt(GetFloat("-w", photoWidth));
        photoHeight = Mathf.RoundToInt(GetFloat("-h", photoHeight));
        observeSeconds = GetFloat("-observe", observeSeconds);
        Debug.Log(string.Format(CultureInfo.InvariantCulture,
            "SPECIES-ARGS label={0} capture={1} size={2}x{3} species={4}",
            label, captureStill, photoWidth, photoHeight, speciesName));

        // Only the scene this run asked for (-scene). A component in the other
        // catalogue scene must stay silent while the switch happens.
        if (!SceneLaunch.ActivatesThisScene())
            return;

        if (captureStill)
            StartCoroutine(Capture());
    }

    // Keep the whole tree in the frame while the scene plays.
    void LateUpdate()
    {
        if (cam == null)
            cam = Camera.main;
        if (cam == null)
            return;
        if (framed == null)
            framed = FindTree();
        if (framed == null)
            return;

        // Real art on the tree: the same pass the gallery uses, at runtime, so
        // this scene shows the shipping textures without touching the rig's own
        // source (the apple stream owns that file).
        bool dressedNow = false;
        if (realArt && !dressed)
        {
            dressed = true;
            dressedNow = true;
            ArtParts.ResetSkips();
            dressedCount = ArtParts.DressAll(framed, true, ArtLibrary.Bark, ArtLibrary.Leaf, ArtParts.FruitKey);
            Debug.Log("SPECIES-DRESS renderers=" + dressedCount + " giantSkips=" + ArtParts.GiantSkips
                + " emptySkips=" + ArtParts.EmptySkips + " root=" + framed.name);
        }

        Bounds bounds;
        if (!PhotoCapture.TryRendererBounds(framed, out bounds))
            return;

        Vector2 min, max;
        float outside;
        bool fits = PhotoCapture.FitViewport(cam, bounds, out min, out max, out outside);

        if (dressedNow)
        {
            // Warm-up frame: the box just grew into the shipping plates. Record
            // how far outside the placeholder frame it reached, re-frame, and let
            // the next frame be the first measured one.
            warmupFrames++;
            dressOutsideMeters = outside;
            statsFromSeconds = Time.time;
            ApplyFraming(bounds);
            return;
        }

        observedFrames++;
        if (observedFrames == 1)
        {
            firstMeasuredSizeX = bounds.size.x;
            firstMeasuredSizeY = bounds.size.y;
        }
        if (!fits)
        {
            missFrames++;
            if (firstMissSeconds < 0f)
            {
                firstMissSeconds = Time.time;
                firstMissOutsideMeters = outside;
            }
            if (Time.time - statsFromSeconds > 1f)
                missFramesAfterOneSecond++;
        }

        ApplyFraming(bounds);
    }

    private IEnumerator Capture()
    {
        yield return new WaitForSeconds(Mathf.Max(0.1f, settleSeconds));

        cam = Camera.main;
        if (cam == null)
        {
            Debug.LogError("SPECIES-CAPTURE FAIL no Main Camera");
            Finish(1);
            yield break;
        }

        cam.orthographic = true;
        cam.clearFlags = CameraClearFlags.SolidColor;

        PlantTree[] plants = FindObjectsByType<PlantTree>(FindObjectsSortMode.None);
        AppleTree[] apples = FindObjectsByType<AppleTree>(FindObjectsSortMode.None);
        int treeCount = plants.Length + apples.Length;
        framed = apples.Length > 0 ? apples[0].transform
            : plants.Length > 0 ? plants[0].transform : null;

        Debug.Log(string.Format(CultureInfo.InvariantCulture,
            "SPECIES-CAPTURE scene={0} species={1} PlantTree={2} AppleTree={3} treeCount={4}",
            gameObject.scene.name, speciesName, plants.Length, apples.Length, treeCount));

        if (framed == null)
        {
            Debug.LogError("SPECIES-CAPTURE FAIL no tree in the scene");
            Finish(1);
            yield break;
        }
        if (treeCount != 1)
            Debug.LogError("SPECIES-CAPTURE FAIL tree count is " + treeCount + ", the card demands exactly 1");

        // Watch the framing across a full sway window before writing the still.
        float until = Time.time + Mathf.Max(1f, observeSeconds);
        while (Time.time < until)
            yield return new WaitForEndOfFrame();

        Bounds bounds;
        if (!PhotoCapture.TryRendererBounds(framed, out bounds))
        {
            Debug.LogError("SPECIES-CAPTURE FAIL the tree has no painted sprite");
            Finish(1);
            yield break;
        }

        ApplyFraming(bounds);
        yield return new WaitForEndOfFrame();

        string folder = GetString("-out", DefaultOutputFolder());
        if (!Directory.Exists(folder))
            Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, label + "_photo.png");

        string error;
        bool wrote = PhotoCapture.WritePng(cam, photoWidth, photoHeight, path, out error);
        if (!wrote)
            Debug.LogError("SPECIES-CAPTURE FAIL " + error);

        Texture2D small = PhotoCapture.Render(cam, 640, 360);
        float painted = PhotoCapture.PaintedFraction(small);
        if (small != null)
            Destroy(small);

        Vector2 min, max;
        float outside;
        bool fits = PhotoCapture.FitViewport(cam, bounds, out min, out max, out outside);

        ReportDto report = new ReportDto();
        report.label = label;
        report.scene = gameObject.scene.name;
        report.species = speciesName;
        report.unityVersion = Application.unityVersion;
        report.buildGuid = Application.buildGUID;
        report.capturedUtc = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
        report.treeCount = treeCount;
        report.plantTreeCount = plants.Length;
        report.appleTreeCount = apples.Length;
        report.heightMeters = bounds.size.y;
        report.widthMeters = bounds.size.x;
        report.groundY = bounds.min.y;
        report.orthoSize = cam.orthographicSize;
        report.fits = fits;
        report.minViewportX = min.x;
        report.minViewportY = min.y;
        report.maxViewportX = max.x;
        report.maxViewportY = max.y;
        report.outsideMeters = outside;
        report.paintedFraction = painted;
        report.observedFrames = observedFrames;
        report.missFrames = missFrames;
        report.warmupFrames = warmupFrames;
        report.firstMissSeconds = firstMissSeconds;
        report.firstMissOutsideMeters = firstMissOutsideMeters;
        report.missFramesAfterOneSecond = missFramesAfterOneSecond;
        report.boundsGrowthMeters = bounds.size.y - firstMeasuredSizeY;
        report.dressOutsideMeters = dressOutsideMeters;
        report.statsFromSeconds = statsFromSeconds;
        report.fitFractionOverRun = observedFrames > 0 ? 1f - missFrames / (float)observedFrames : 0f;
        report.width = photoWidth;
        report.height = photoHeight;
        report.argv = string.Join(" ", Environment.GetCommandLineArgs());

        string jsonPath = Path.Combine(folder, label + "-tree.json");
        File.WriteAllText(jsonPath, JsonUtility.ToJson(report, true));

        Debug.Log(string.Format(CultureInfo.InvariantCulture,
            "SPECIES-CAPTURE DONE label={0} photo={1} trees={2} height={3:0.00}m width={4:0.00}m "
            + "ortho={5:0.00} fits={6} viewport=[{7:0.000},{8:0.000}]x[{9:0.000},{10:0.000}] "
            + "painted={11:0.0000} fitFraction={12:0.000} warmup={13} dressOutside={14:0.000}m "
            + "json={15}",
            label, path, treeCount, bounds.size.y, bounds.size.x, cam.orthographicSize, fits,
            min.x, min.y, max.x, max.y, painted, report.fitFractionOverRun, warmupFrames,
            dressOutsideMeters, jsonPath));

        int code = (wrote && fits && treeCount == 1 && painted > 0.02f) ? 0 : 1;
        if (code != 0)
            Debug.LogError("SPECIES-CAPTURE FAILURES: wrote=" + wrote + " fits=" + fits
                + " treeCount=" + treeCount + " painted=" + painted.ToString("0.0000"));
        Finish(code);
    }

    // Whole tree inside the frame, with `margin` of the half-height kept free.
    private void ApplyFraming(Bounds bounds)
    {
        if (cam == null)
            return;
        int w = photoWidth > 8 ? photoWidth : Mathf.Max(8, Screen.width);
        int h = photoHeight > 8 ? photoHeight : Mathf.Max(8, Screen.height);
        float aspect = cam.aspect > 0.1f ? cam.aspect : w / (float)h;
        if (captureStill)
            cam.aspect = w / (float)h;
        aspect = w / (float)h;
        float halfH = bounds.extents.y * (1f + margin);
        float halfW = bounds.extents.x * (1f + margin) / Mathf.Max(0.1f, aspect);
        cam.orthographic = true;
        cam.orthographicSize = Mathf.Max(halfH, halfW);
        cam.transform.position = new Vector3(bounds.center.x, bounds.center.y, -10f);
    }

    private void Finish(int code)
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.Exit(code);
#else
        Application.Quit(code);
#endif
    }

    private static string DefaultOutputFolder()
    {
        return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Trials", "Runs"));
    }

    private static string GetString(string name, string fallback)
    {
        string[] args = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : fallback;
    }

    private static float GetFloat(string name, float fallback)
    {
        string raw = GetString(name, null);
        if (string.IsNullOrEmpty(raw))
            return fallback;
        float value;
        return float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value) ? value : fallback;
    }
}
