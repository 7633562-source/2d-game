using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

// ART-1 gallery capture: 1080p stills plus a >= 15 s panning sequence, from the
// scene that is on screen. Runs in Play mode (the Unity editor in batchmode, or
// the built player): flags are read from the command line the same way the rest
// of the project reads them.
//
//   -capture 1            arm the capture (otherwise the scene is interactive)
//   -label gallery1       output prefix
//   -out <folder>         default <project>/Trials/Runs
//   -captureWidth 1920 -captureHeight 1080
//   -panSeconds 16.5 -panFps 10 -stillFps 1
//
// Every still is checked numerically: the world box of the thing being shown has
// to land inside the frame (PhotoCapture.FitViewport), and the numbers go into
// <label>-gallery.json next to the PNGs.
public class GalleryCapture : MonoBehaviour
{
    [Serializable]
    private class ItemDto
    {
        public string name;
        public string texture;
        public string state;
        public string note;
    }

    [Serializable]
    private class FitDto
    {
        public string target;
        public string file;
        public bool fits;
        public float minViewportX;
        public float minViewportY;
        public float maxViewportX;
        public float maxViewportY;
        public float outsideMeters;
        public float paintedFraction;
        public float orthoSize;
    }

    [Serializable]
    private class ReportDto
    {
        public string label;
        public string scene;
        public string unityVersion;
        public string buildGuid;
        public string capturedUtc;
        public bool realArtOn;
        public int width;
        public int height;
        public float panSeconds;
        public int panFps;
        public int panFrames;
        public float panStep;
        public int realItems;
        public int missingItems;
        /// <summary>MISSING-ART tiles actually drawn (a subset of what the eye sees on the
        /// missing row), told apart from `missingItems`, which counts INVENTORY rows.</summary>
        public int missingTiles;
        public string[] missingTileNames;
        public int dressedRenderers;
        public string boundsBeforeDress;
        public string boundsAfterDress;
        public float boundsDeltaMeters;
        public string argv;
        public string[] rowBoxes;
        public FitDto[] fits;
        public ItemDto[] inventory;
    }

    public float warmupSeconds = 2.5f;

    private GalleryScene gallery;
    private Camera cam;
    private string label = "gallery";
    private string outputFolder;
    private string dressBefore = string.Empty;
    private string dressAfter = string.Empty;
    private float dressDelta;
    private int width = 1920;
    private int height = 1080;
    private float panSeconds = 16.5f;
    private int panFps = 10;
    private bool armed;

    void Start()
    {
        gallery = GetComponent<GalleryScene>();
        armed = GetFloat("-capture", 0f) > 0.5f;
        Debug.Log("GALLERY-ARGS armed=" + armed + " label=" + label + " scene=" + gameObject.scene.name);
        if (!armed)
            return;
        if (!SceneLaunch.ActivatesThisScene())
            return;
        StartCoroutine(Capture());
    }

    private IEnumerator Capture()
    {
        yield return new WaitForSeconds(Mathf.Max(0.1f, warmupSeconds));

        if (gallery == null)
            gallery = FindFirstObjectByType<GalleryScene>();
        if (gallery == null)
        {
            Debug.LogError("GALLERY-CAPTURE FAIL no GalleryScene in the scene");
            Finish(1);
            yield break;
        }

        label = GetString("-label", label);
        outputFolder = GetString("-out", DefaultOutputFolder());
        width = Mathf.RoundToInt(GetFloat("-captureWidth", width));
        height = Mathf.RoundToInt(GetFloat("-captureHeight", height));
        panSeconds = GetFloat("-panSeconds", panSeconds);
        panFps = Mathf.Max(1, Mathf.RoundToInt(GetFloat("-panFps", panFps)));
        float stillPause = GetFloat("-stillFps", 0.25f);

        cam = gallery.Lens();
        if (cam == null)
        {
            Debug.LogError("GALLERY-CAPTURE FAIL no camera");
            Finish(1);
            yield break;
        }

        if (!Directory.Exists(outputFolder))
            Directory.CreateDirectory(outputFolder);

        List<FitDto> fits = new List<FitDto>();
        List<string> rowBoxes = new List<string>();
        foreach (KeyValuePair<string, Bounds> pair in gallery.RowBounds)
            rowBoxes.Add(pair.Key + " " + Describe(pair.Value));

        // 1..n: the stills. One camera move, one frame, one numeric fit check.
        yield return Still("whole gallery", gallery.ContentBounds, label + "_photo.png", fits, stillPause);

        // Frames have passed, so the sprite meshes are rebuilt: the catalogue's
        // own box must be exactly where it was before the real textures landed.
        Bounds before = gallery.PreDressBounds;
        Bounds after = gallery.LiveBounds();
        dressDelta = Mathf.Max(
            Mathf.Max(Mathf.Abs(after.size.x - before.size.x), Mathf.Abs(after.size.y - before.size.y)),
            Mathf.Max(Mathf.Abs(after.center.x - before.center.x), Mathf.Abs(after.center.y - before.center.y)));
        dressBefore = Describe(before);
        dressAfter = Describe(after);
        Debug.Log("GALLERY-CAPTURE dress geometry before=" + dressBefore + " after=" + dressAfter
            + " delta=" + dressDelta.ToString("0.0000") + " m");
        yield return Still("human parts", RowBoundsOr(gallery, "human"), label + "_human.png", fits, stillPause);
        yield return Still("oak close", RowBoundsOr(gallery, "tree_oak"), label + "_tree.png", fits, stillPause);
        yield return Still("apple 2 m + 5 m", RowBoundsOr(gallery, "apple"), label + "_apple.png", fits, stillPause);
        yield return Still("materials", RowBoundsOr(gallery, "materials"), label + "_materials.png", fits, stillPause);
        yield return Still("ground / sky / sign", RowBoundsOr(gallery, "level"), label + "_level.png", fits, stillPause);
        yield return Still("campfire flame core", RowBoundsOr(gallery, "fire"), label + "_fire.png", fits, stillPause);
        // The flower bed (ART-0 gap 3, 20.09) has its own tighter lens: the berry row's
        // lens draws a 0.03 m petal 3 px wide, which is not a picture to judge.
        yield return Still("flower bed", RowBoundsOr(gallery, "flower"), label + "_flower.png", fits, stillPause);

        // The comparison toggle, on the same lens: the difference is the whole point.
        gallery.SetRealArt(false);
        yield return Still("whole gallery, placeholder art", gallery.ContentBounds,
            label + "_placeholder.png", fits, stillPause);
        // ... and the same flower bed with the flat collider boxes back: one build, one
        // lens, the only difference is the art. This pair is what the blind read judges.
        yield return Still("flower bed, placeholder boxes", RowBoundsOr(gallery, "flower"),
            label + "_flowerbox.png", fits, stillPause);
        gallery.SetRealArt(true);

        // The new row's two lenses, and deliberately LAST: the gallery's Sway trees move
        // with the wind, so a still's picture depends on WHEN in the capture it is taken.
        // Stamping the creature row right after the human row (the first `art1dog14` run)
        // shifted the oak/apple/materials/level/fire/flower stills of the same build by
        // two stamps' worth of simulation and the oak and apple crowns came out 33-35 dB
        // apart from gallery12 instead of byte-identical. Placing the new stamps after the
        // ten old ones keeps every earlier still on its own time slot - the same-build
        // control stays byte-exact for everything this cycle did not touch.
        yield return Still("creature row", RowBoundsOr(gallery, "creatures"), label + "_creatures.png", fits, stillPause);
        yield return Still("dog parts (creature row)", RowBoundsOr(gallery, "creatures_dog"), label + "_dog.png", fits, stillPause);

        // The pan: >= 15 s of the whole strip at the capture frame rate.
        int panFrames = Mathf.Max(2, Mathf.RoundToInt(panSeconds * panFps));
        Bounds content = gallery.ContentBounds;
        float halfWidth = LensForBounds(content, width, height, 0.06f) * (width / (float)height);
        float fromX = content.center.x - (content.size.x * 0.5f) + halfWidth * 0.9f;
        float toX = content.center.x + (content.size.x * 0.5f) - halfWidth * 0.9f;
        float panStart = Time.time;

        for (int i = 0; i < panFrames; i++)
        {
            float t = i / (float)(panFrames - 1);
            float x = Mathf.Lerp(fromX, toX, t);
            cam.transform.position = new Vector3(x, PanCentreY(content), -10f);
            EnsureLens(content);
            yield return new WaitForEndOfFrame();

            string path = Path.Combine(outputFolder,
                "shot_" + label + "_" + i.ToString("00", CultureInfo.InvariantCulture) + ".png");
            string error;
            if (!PhotoCapture.WritePng(cam, width, height, path, out error))
                Debug.LogError("GALLERY-CAPTURE FAIL pan frame " + i + ": " + error);

            float target = panStart + (i + 1) / (float)panFps;
            while (Time.time < target)
                yield return null;
        }

        ReportDto report = BuildReport(fits, rowBoxes);
        string jsonPath = Path.Combine(outputFolder, label + "-gallery.json");
        File.WriteAllText(jsonPath, JsonUtility.ToJson(report, true));

        Debug.Log(string.Format(CultureInfo.InvariantCulture,
            "GALLERY-CAPTURE DONE label={0} stills={1} panFrames={2} panSeconds={3:0.0} "
            + "real={4} missingRows={5} missingTiles={6} dressed={7} json={8} drawnTiles=[{9}]",
            label, fits.Count, panFrames, panSeconds, gallery.RealItemCount, gallery.MissingItemCount,
            gallery.MissingTileCount, gallery.DressedRendererCount, jsonPath,
            string.Join(", ", gallery.MissingTiles)));

        int failures = 0;
        for (int i = 0; i < fits.Count; i++)
        {
            if (!fits[i].fits)
                failures++;
            if (fits[i].paintedFraction < 0.02f)
            {
                Debug.LogError("GALLERY-CAPTURE FAIL empty frame " + fits[i].file
                    + " paintedFraction=" + fits[i].paintedFraction.ToString("0.0000"));
                failures++;
            }
        }
        if (failures > 0)
            Debug.LogError("GALLERY-CAPTURE FAILURES " + failures);
        Finish(failures > 0 ? 1 : 0);
    }

    private IEnumerator Still(string name, Bounds bounds, string file, List<FitDto> fits, float pause)
    {
        CameraTo(bounds);
        yield return new WaitForEndOfFrame();

        string path = Path.Combine(outputFolder, file);
        string error;
        if (!PhotoCapture.WritePng(cam, width, height, path, out error))
        {
            Debug.LogError("GALLERY-CAPTURE FAIL " + name + ": " + error);
            yield break;
        }

        Texture2D tex = PhotoCapture.Render(cam, Mathf.Min(640, width), Mathf.Min(360, height));
        float painted = PhotoCapture.PaintedFraction(tex);
        if (tex != null)
            Destroy(tex);

        Vector2 min, max;
        float outside;
        bool ok = PhotoCapture.FitViewport(cam, bounds, out min, out max, out outside);

        FitDto dto = new FitDto();
        dto.target = name;
        dto.file = file;
        dto.fits = ok;
        dto.minViewportX = min.x;
        dto.minViewportY = min.y;
        dto.maxViewportX = max.x;
        dto.maxViewportY = max.y;
        dto.outsideMeters = outside;
        dto.paintedFraction = painted;
        dto.orthoSize = cam.orthographicSize;
        fits.Add(dto);

        if (!ok)
            Debug.LogError("GALLERY-CAPTURE FAIL " + name + " does not fit: min="
                + min.ToString("0.000") + " max=" + max.ToString("0.000")
                + " outsideMeters=" + outside.ToString("0.000"));
        else
            Debug.Log("GALLERY-CAPTURE still " + file + " target=" + name + " fits min="
                + min.ToString("0.000") + " max=" + max.ToString("0.000")
                + " painted=" + painted.ToString("0.0000") + " ortho=" + cam.orthographicSize.ToString("0.00"));

        if (pause > 0.01f)
            yield return new WaitForSeconds(pause);
    }

    private ReportDto BuildReport(List<FitDto> fits, List<string> rowBoxes)
    {
        ReportDto report = new ReportDto();
        report.label = label;
        report.scene = gameObject.scene.name;
        report.unityVersion = Application.unityVersion;
        report.buildGuid = Application.buildGUID;
        report.capturedUtc = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
        report.realArtOn = gallery.realArt;
        report.width = width;
        report.height = height;
        report.panSeconds = panSeconds;
        report.panFps = panFps;
        report.panFrames = Mathf.Max(2, Mathf.RoundToInt(panSeconds * panFps));
        report.panStep = 1f / panFps;
        report.realItems = gallery.RealItemCount;
        report.missingItems = gallery.MissingItemCount;
        report.missingTiles = gallery.MissingTileCount;
        report.missingTileNames = gallery.MissingTiles.ToArray();
        report.dressedRenderers = gallery.DressedRendererCount;
        report.boundsBeforeDress = dressBefore;
        report.boundsAfterDress = dressAfter;
        report.boundsDeltaMeters = dressDelta;
        report.argv = string.Join(" ", Environment.GetCommandLineArgs());
        report.rowBoxes = rowBoxes.ToArray();
        report.fits = fits.ToArray();

        ItemDto[] items = new ItemDto[gallery.Inventory.Count];
        for (int i = 0; i < items.Length; i++)
        {
            GalleryScene.Item src = gallery.Inventory[i];
            ItemDto dto = new ItemDto();
            dto.name = src.name;
            dto.texture = src.texture;
            dto.state = src.real ? "real" : "missing";
            dto.note = src.note;
            items[i] = dto;
        }
        report.inventory = items;
        return report;
    }

    private void CameraTo(Bounds bounds)
    {
        cam.aspect = width / (float)height;
        cam.transform.position = new Vector3(bounds.center.x, bounds.center.y, -10f);
        cam.orthographic = true;
        cam.orthographicSize = LensSize(bounds, width, height, 0.06f);
    }

    private void EnsureLens(Bounds content)
    {
        cam.aspect = width / (float)height;
        cam.orthographic = true;
        cam.orthographicSize = LensSize(content, width, height, 0.06f);
    }

    private float PanCentreY(Bounds content)
    {
        return content.center.y;
    }

    private static float LensSize(Bounds bounds, int w, int h, float margin)
    {
        float aspect = w / (float)h;
        float halfH = bounds.extents.y * (1f + margin);
        float halfW = bounds.extents.x * (1f + margin) / Mathf.Max(0.1f, aspect);
        return Mathf.Max(halfH, halfW);
    }

    private static float LensForBounds(Bounds bounds, int w, int h, float margin)
    {
        return LensSize(bounds, w, h, margin);
    }

    private static Bounds RowBoundsOr(GalleryScene scene, string key)
    {
        Bounds bounds;
        if (scene.RowBounds.TryGetValue(key, out bounds))
            return bounds;
        return scene.ContentBounds;
    }

    private static string Describe(Bounds b)
    {
        return "centre=" + b.center.x.ToString("0.0", CultureInfo.InvariantCulture) + ","
            + b.center.y.ToString("0.0", CultureInfo.InvariantCulture)
            + " size=" + b.size.x.ToString("0.0", CultureInfo.InvariantCulture) + "x"
            + b.size.y.ToString("0.0", CultureInfo.InvariantCulture);
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

    private static bool Has(string name)
    {
        return Array.IndexOf(Environment.GetCommandLineArgs(), name) >= 0;
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
