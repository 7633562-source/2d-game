using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

// ─── CLEARING-1: the capture component of the clearing scene ───
//
// Assets/Scenes/Clearing.unity is the first real game location: the Test1 yard
// tape, the Static grove, the yard's berry bushes, a bed of the yard's flowers,
// the clearing's own oak, the campfire at its drawn spot and the yard clock -
// no creatures. Opening the scene and pressing Play stays interactive: the
// human stands on the yard's ground and walks with W / up.
//
// This component wakes only when the command line asks:
//
//   -capture 1 -label clearing1 -out <folder> -captureWidth 1920 -captureHeight 1080
//       -panSeconds 16.5 -panFps 10 -stillPause 1
//   -clearingWalk 1 -label clearing1walk -walkSeconds 10
//
// Stills and pan frames are 1920x1080 PNGs through PhotoCapture. The report
// JSON names the buildGuid, what the scene actually contains (grove, berry
// bushes, flowers, the clearing's own tree, the fire, the clock) and the fit of
// every still's window into its frame - the same numeric proof the catalogue
// uses. The per-frame cost of the same build is in
// Trials/Runs/world_perf_capture_Clearing.json (GameProcess attaches the
// WorldPerformanceMonitor in batch mode).
//
// The walk run drives the same pair the keyboard path drives (MotionIntent +
// StepPhaseDriver.ArmWalk), with PlayerInputSource disabled so nothing wipes
// the intent in a keyboard-less batch run, and records the project's own
// FallCriterion - "the man walks the clearing" becomes a measured line.
public class ClearingScene : MonoBehaviour
{
    [Serializable]
    private class StillDto
    {
        public string name;
        public string file;
        public bool fits;
        public float paintedFraction;
        public float orthoSize;
        public float minVx;
        public float minVy;
        public float maxVx;
        public float maxVy;
        public float outsideMeters;
        public float night01;
        public bool nightShift;
    }

    [Serializable]
    private class ContentDto
    {
        public string scene;
        public string unityVersion;
        public string buildGuid;
        public string capturedUtc;
        public string argv;
        public string generatorTitle;
        public float playerSpawnX;
        public bool plantDrawnLevel;
        public bool clearingProps;
        public int groveTrees;
        public int swayTrees;
        public string groveXs;
        public string swayXs;
        public int berryBushes;
        public string berryXs;
        public int flowers;
        public string flowerXs;
        public int birds;
        public int chickens;
        public int dogs;
        public int hedgehogs;
        public int appleTrees;
        public int humans;
        public float humanX;
        public bool campfire;
        public string campfireAt;
        public bool dayNightCycle;
        public float dayNightSeconds;
        public float night01Start;
        public float night01End;
    }

    [Serializable]
    private class CaptureReport
    {
        public string label;
        public string kind = "stills+pan";
        public int width;
        public int height;
        public float panSeconds;
        public int panFps;
        public int panFrames;
        public float panOrtho;
        public float panFromX;
        public float panToX;
        public float panY;
        public ContentDto content;
        public StillDto[] stills;
        public int failures;
    }

    [Serializable]
    private class WalkReport
    {
        public string label;
        public string kind = "walk";
        public float walkSeconds;
        public float walkedMeters;
        public int swaps;
        public bool fell;
        public float fallSeconds;
        public bool uprightAtEnd;
        public float tiltAtEnd;
        public float pelvisYAtEnd;
        public float standHeight;
        public bool baselineValid;
        // The walker's own record: with the capture walker the gait steps are
        // counted here, not in StepPhaseDriver.SwapCount.
        public bool walkActiveAtEnd;
        public int walkerSteps;
        public int walkerStateAtEnd;
        public int locomotionAtEnd;
        public int restoreAtEnd;
        public int frameCount;
        public string still;
        public string csv;
        public ContentDto content;
        public int failures;
    }

    private struct Window
    {
        public float minX;
        public float maxX;
        public float minY;
        public float maxY;

        public Window(float minX, float maxX, float minY, float maxY)
        {
            this.minX = minX;
            this.maxX = maxX;
            this.minY = minY;
            this.maxY = maxY;
        }
    }

    [Header("Capture")]
    public bool captureStill = false;
    public string label = "clearing1";
    public int photoWidth = 1920;
    public int photoHeight = 1080;
    public float settleSeconds = 3f;
    public float stillPauseSeconds = 1f;

    [Header("Pan (the gallery's own pan, over the clearing)")]
    public float panSeconds = 16.5f;
    public int panFps = 10;
    public float panOrtho = 5f;
    public float panY = 1f;
    public float panFromX = -9f;
    public float panToX = 7f;

    [Header("Walk run")]
    public bool clearingWalk = false;
    public float walkSeconds = 10f;
    public float walkStopSeconds = 0f; // > 0: stop recording frames early (clip), the run still ends at walkSeconds

    // The clearing's content windows: what must be inside a still's frame. The
    // camera is put at the window's centre and the lens fits the window with
    // `margin` of the half-height kept free; the fit is then checked on the
    // rendered frame with PhotoCapture.FitViewport.
    private static readonly Window WholeClearing = new Window(-17f, 15f, -2.2f, 5.5f);
    private static readonly Window CampfireClose = new Window(-9.2f, -0.2f, -2.3f, 1.2f);
    private static readonly Window TreeClose = new Window(-18f, -7f, -2.9f, 3.3f);
    // The west half again, for the clock's own midnight: spawn, the fire, the
    // oak, the berries and the bed under the night sky.
    private static readonly Window MidnightView = new Window(-17f, 2f, -2.4f, 4.4f);

    public float margin = 0.06f;

    private Camera cam;
    private string outputFolder;
    private bool armedCapture;
    private bool armedWalk;

    void Start()
    {
        armedCapture = GetFloat("-capture", 0f) > 0.5f;
        armedWalk = GetFloat("-clearingWalk", 0f) > 0.5f;
        Debug.Log("CLEARING-ARGS capture=" + armedCapture + " walk=" + armedWalk
            + " scene=" + gameObject.scene.name);
        if (!armedCapture && !armedWalk)
            return; // interactive Play: Yury opens the scene and walks with W / up
        if (!SceneLaunch.ActivatesThisScene())
            return;
        if (armedCapture)
            StartCoroutine(Capture());
        if (armedWalk)
            StartCoroutine(WalkRun());
    }

    // ─── stills + pan ─────────────────────────────────────────────────────

    private IEnumerator Capture()
    {
        yield return new WaitForSeconds(Mathf.Max(0.1f, settleSeconds));

        label = GetString("-label", label);
        outputFolder = GetString("-out", DefaultOutputFolder());
        photoWidth = Mathf.RoundToInt(GetFloat("-captureWidth", photoWidth));
        photoHeight = Mathf.RoundToInt(GetFloat("-captureHeight", photoHeight));
        panSeconds = GetFloat("-panSeconds", panSeconds);
        panFps = Mathf.Max(1, Mathf.RoundToInt(GetFloat("-panFps", panFps)));
        float stillPause = GetFloat("-stillPause", stillPauseSeconds);

        cam = Camera.main;
        if (cam == null)
        {
            Debug.LogError("CLEARING-CAPTURE FAIL no Main Camera");
            Finish(1);
            yield break;
        }

        // The follow camera would fight the lenses below.
        CameraFollow follow = cam.GetComponent<CameraFollow>();
        if (follow != null)
            follow.enabled = false;

        if (!Directory.Exists(outputFolder))
            Directory.CreateDirectory(outputFolder);

        int failures = 0;
        List<StillDto> stills = new List<StillDto>();

        // One still at a time: each frame must render under its own lens, so the
        // loop yields inside and the write happens before the next lens. The
        // last still is the yard clock's own midnight: startPhase is read every
        // frame, so shifting it moves the clock and the record keeps the Night01
        // the frame was shot at; the shift is restored so the pan stays day.
        string[] stillNames =
        {
            "whole clearing", "campfire close", "clearing tree close",
            "the same clearing at the clock's midnight"
        };
        Window[] stillWindows = { WholeClearing, CampfireClose, TreeClose, MidnightView };
        string[] stillFiles =
        {
            label + "_photo.png", label + "_fire.png", label + "_tree.png", label + "_night.png"
        };
        bool[] stillNights = { false, false, false, true };
        DayNightCycle clock = DayNightCycle.Instance;
        for (int i = 0; i < stillWindows.Length; i++)
        {
            float savedStartPhase = 0f;
            if (stillNights[i] && clock != null)
            {
                savedStartPhase = clock.startPhase;
                clock.startPhase = 0.5f - Time.time / Mathf.Max(8f, clock.cycleSeconds);
            }
            FrameWindow(stillWindows[i]);
            if (stillNights[i])
                yield return new WaitForSeconds(0.4f); // the clock applies in LateUpdate
            yield return new WaitForEndOfFrame();
            failures += ShootStill(stillNames[i], stillWindows[i], stillFiles[i], stills, stillNights[i]);
            if (stillNights[i] && clock != null)
                clock.startPhase = savedStartPhase;
            if (stillPause > 0.01f)
                yield return new WaitForSeconds(stillPause);
        }

        int panFrames = Mathf.Max(2, Mathf.RoundToInt(panSeconds * panFps));
        float panStepX = (panToX - panFromX) / (panFrames - 1);
        for (int i = 0; i < panFrames; i++)
        {
            float x = panFromX + panStepX * i;
            cam.aspect = photoWidth / (float)photoHeight;
            cam.orthographic = true;
            cam.orthographicSize = panOrtho;
            cam.transform.position = new Vector3(x, panY, -10f);
            yield return new WaitForEndOfFrame();

            string path = Path.Combine(outputFolder,
                "shot_" + label + "_" + i.ToString("00", CultureInfo.InvariantCulture) + ".png");
            string error;
            if (!PhotoCapture.WritePng(cam, photoWidth, photoHeight, path, out error))
            {
                Debug.LogError("CLEARING-CAPTURE FAIL pan frame " + i + ": " + error);
                failures++;
            }
        }

        CaptureReport report = new CaptureReport();
        report.label = label;
        report.width = photoWidth;
        report.height = photoHeight;
        report.panSeconds = panSeconds;
        report.panFps = panFps;
        report.panFrames = panFrames;
        report.panOrtho = panOrtho;
        report.panFromX = panFromX;
        report.panToX = panToX;
        report.panY = panY;
        report.content = Describe();
        report.stills = stills.ToArray();
        report.failures = failures;
        WriteReport(report, label + "-clearing.json");

        Debug.Log(string.Format(CultureInfo.InvariantCulture,
            "CLEARING-CAPTURE DONE label={0} stills={1} panFrames={2} panSeconds={3:0.0} "
            + "grove={4} sway={5} berries={6} flowers={7} birds={8} failures={9}",
            label, stills.Count, panFrames, panSeconds, report.content.groveTrees,
            report.content.swayTrees, report.content.berryBushes, report.content.flowers,
            report.content.birds, failures));
        Finish(failures > 0 ? 1 : 0);
    }

    // The still is written on the frame the caller already yielded for, so its
    // own lens is the one in force. Returns the failure count for the report.
    private int ShootStill(string name, Window window, string file, List<StillDto> stills, bool nightShift)
    {
        string path = Path.Combine(outputFolder, file);
        string error;
        int failures = 0;
        bool wrote = PhotoCapture.WritePng(cam, photoWidth, photoHeight, path, out error);
        if (!wrote)
        {
            Debug.LogError("CLEARING-CAPTURE FAIL " + name + ": " + error);
            failures++;
        }

        Texture2D small = PhotoCapture.Render(cam, Mathf.Min(640, photoWidth), Mathf.Min(360, photoHeight));
        float painted = PhotoCapture.PaintedFraction(small);
        if (small != null)
            Destroy(small);

        Bounds bounds = new Bounds(
            new Vector3((window.minX + window.maxX) * 0.5f, (window.minY + window.maxY) * 0.5f, 0f),
            new Vector3(window.maxX - window.minX, window.maxY - window.minY, 0f));
        Vector2 min, max;
        float outside;
        bool fits = PhotoCapture.FitViewport(cam, bounds, out min, out max, out outside);

        StillDto dto = new StillDto();
        dto.name = name;
        dto.file = file;
        dto.fits = fits;
        dto.paintedFraction = painted;
        dto.orthoSize = cam.orthographicSize;
        dto.minVx = min.x;
        dto.minVy = min.y;
        dto.maxVx = max.x;
        dto.maxVy = max.y;
        dto.outsideMeters = outside;
        dto.night01 = DayNightCycle.Night01;
        dto.nightShift = nightShift;
        stills.Add(dto);

        if (!fits)
        {
            Debug.LogError("CLEARING-CAPTURE FAIL " + name + " does not fit: min="
                + min.ToString("0.000") + " max=" + max.ToString("0.000")
                + " outsideMeters=" + outside.ToString("0.000"));
            failures++;
        }
        else
        {
            Debug.Log(string.Format(CultureInfo.InvariantCulture,
                "CLEARING-CAPTURE still {0} target={1} fits min={2:0.000} max={3:0.000} "
                + "painted={4:0.0000} ortho={5:0.00} night01={6:0.000}",
                file, name, min, max, painted, cam.orthographicSize, DayNightCycle.Night01));
        }
        return failures;
    }

    private void FrameWindow(Window w)
    {
        float aspect = photoWidth / (float)photoHeight;
        float halfW = (w.maxX - w.minX) * 0.5f;
        float halfH = (w.maxY - w.minY) * 0.5f;
        cam.aspect = aspect;
        cam.orthographic = true;
        cam.orthographicSize = Mathf.Max(halfH, halfW / aspect) * (1f + margin);
        cam.transform.position = new Vector3((w.minX + w.maxX) * 0.5f, (w.minY + w.maxY) * 0.5f, -10f);
    }

    // ─── the walk run ─────────────────────────────────────────────────────

    private IEnumerator WalkRun()
    {
        yield return new WaitForSeconds(Mathf.Max(0.1f, settleSeconds));

        label = GetString("-label", label + "walk");
        outputFolder = GetString("-out", DefaultOutputFolder());
        photoWidth = Mathf.RoundToInt(GetFloat("-captureWidth", photoWidth));
        photoHeight = Mathf.RoundToInt(GetFloat("-captureHeight", photoHeight));
        walkSeconds = GetFloat("-walkSeconds", walkSeconds);
        walkStopSeconds = GetFloat("-walkStopSeconds", walkStopSeconds);

        if (!Directory.Exists(outputFolder))
            Directory.CreateDirectory(outputFolder);

        int failures = 0;
        cam = Camera.main;
        // CameraFollow stays on: the walk clip is the game view.

        Human human = FindHuman();
        if (human == null)
        {
            Debug.LogError("CLEARING-WALK FAIL no human in the scene");
            Finish(1);
            yield break;
        }

        Transform pelvisTf = human.transform.Find("Pelvis");
        Rigidbody2D pelvis = pelvisTf != null ? pelvisTf.GetComponent<Rigidbody2D>() : null;
        MotionIntent intent = human.GetComponent<MotionIntent>();
        StepPhaseDriver driver = human.GetComponent<StepPhaseDriver>();
        if (pelvis == null || intent == null || driver == null)
        {
            Debug.LogError("CLEARING-WALK FAIL human is missing pelvis / intent / step driver");
            Finish(1);
            yield break;
        }

        // The settled stand height of the project's own criterion, measured
        // while the man still stands.
        FallCriterion.Baseline baseline = new FallCriterion.Baseline();
        FallCriterion.Latch latch = new FallCriterion.Latch();
        latch.Reset();
        float settleStart = Time.time;
        while (Time.time - settleStart < 1.2f)
        {
            baseline.Feed(pelvis.position.y, Mathf.Abs(Tilt(human)));
            yield return new WaitForFixedUpdate();
        }
        baseline.Freeze();

        // The trial's arrangement: this component owns the walk clock
        // (externalDrive stops the driver's own FixedUpdate tick, and
        // PlayerInputSource stands down on the same flag — a keyboard-less
        // batch run must not wipe the intent). ArmWalk is BeginWalk + Tick,
        // the pair the trial uses.
        driver.externalDrive = true;
        intent.lean = 0f;
        intent.run = 0f;
        intent.moveX = 1f;

        float startX = pelvis.position.x;
        float armTime = Time.time;
        float lastTime = armTime;
        if (!driver.walkActive)
            driver.ArmWalk();

        List<string> csv = new List<string>();
        csv.Add("t,walkActive,walkerState,walkerSteps,locomotion,restore,surrender,swaps,x,y,tiltDeg,down");
        BalanceController balance = human.GetComponent<BalanceController>();
        int frames = 0;
        float nextFrameTime = armTime;
        float nextSampleTime = armTime;
        bool stillWritten = false;
        float stopFramesAt = walkStopSeconds > 0.01f ? walkStopSeconds : walkSeconds;

        while (true)
        {
            float now = Time.time;
            float t = now - armTime;
            if (t >= walkSeconds)
                break;

            // The walker reads the intent every step; hold it, and tick the
            // driver after the step (the trial's ordering).
            intent.moveX = 1f;
            intent.lean = 0f;
            driver.Tick(Time.fixedTime);

            float tilt = Mathf.Abs(Tilt(human));
            float pelvisY = pelvis.position.y;
            float standHeight = baseline.valid ? baseline.height : float.NegativeInfinity;
            bool down = FallCriterion.Step(ref latch, standHeight, pelvisY, tilt, now - lastTime, t);
            lastTime = now;

            if (now >= nextSampleTime)
            {
                nextSampleTime += 0.5f;
                csv.Add(string.Format(CultureInfo.InvariantCulture,
                    "{0:0.000},{1},{2},{3},{4},{5},{6},{7},{8:0.0000},{9:0.0000},{10:0.000},{11}",
                    t,
                    driver.walkActive ? 1 : 0,
                    balance != null ? balance.CaptureWalkStateCode : -1,
                    balance != null ? balance.CaptureWalkStepCount : -1,
                    balance != null ? (int)balance.locomotionState : -1,
                    driver.StandingRestore ? 1 : 0,
                    driver.StandingSurrender ? 1 : 0,
                    driver.SwapCount, pelvis.position.x, pelvisY, tilt, down ? 1 : 0));
            }

            if (now >= nextFrameTime && t <= stopFramesAt)
            {
                nextFrameTime += 1f / 10f;
                string path = Path.Combine(outputFolder,
                    "shot_" + label + "_" + frames.ToString("00", CultureInfo.InvariantCulture) + ".png");
                string error;
                if (!PhotoCapture.WritePng(cam, photoWidth, photoHeight, path, out error))
                {
                    Debug.LogError("CLEARING-WALK FAIL frame " + frames + ": " + error);
                    failures++;
                }
                frames++;
            }

            if (!stillWritten && t >= 3f)
            {
                stillWritten = true;
                string path = Path.Combine(outputFolder, label + "_walkstill.png");
                string error;
                if (!PhotoCapture.WritePng(cam, photoWidth, photoHeight, path, out error))
                {
                    Debug.LogError("CLEARING-WALK FAIL walk still: " + error);
                    failures++;
                }
                else
                {
                    Debug.Log("CLEARING-WALK still " + path);
                }
            }

            yield return new WaitForFixedUpdate();
        }

        driver.EndWalk();

        string csvName = label + "-walk.csv";
        File.WriteAllLines(Path.Combine(outputFolder, csvName), csv);

        WalkReport report = new WalkReport();
        report.label = label;
        report.walkSeconds = walkSeconds;
        report.walkedMeters = pelvis.position.x - startX;
        report.swaps = driver.SwapCount;
        report.fell = latch.fired;
        report.fallSeconds = latch.fireTime;
        report.uprightAtEnd = !FallCriterion.Down(
            baseline.valid ? baseline.height : float.NegativeInfinity,
            pelvis.position.y, Mathf.Abs(Tilt(human)));
        report.tiltAtEnd = Mathf.Abs(Tilt(human));
        report.pelvisYAtEnd = pelvis.position.y;
        report.standHeight = baseline.height;
        report.baselineValid = baseline.valid;
        report.walkActiveAtEnd = driver.walkActive;
        report.walkerSteps = balance != null ? balance.CaptureWalkStepCount : -1;
        report.walkerStateAtEnd = balance != null ? balance.CaptureWalkStateCode : -1;
        report.locomotionAtEnd = balance != null ? (int)balance.locomotionState : -1;
        report.restoreAtEnd = (driver.StandingRestore ? 1 : 0) + (driver.StandingSurrender ? 2 : 0);
        report.frameCount = frames;
        report.still = label + "_walkstill.png";
        report.csv = csvName;
        report.content = Describe();
        report.failures = failures;
        WriteReport(report, label + "-walk.json");

        Debug.Log(string.Format(CultureInfo.InvariantCulture,
            "CLEARING-WALK DONE label={0} walked={1:0.000} m swaps={2} walkerSteps={3} walkerState={4} "
            + "fell={5} fallAt={6:0.000} uprightAtEnd={7} stand={8:0.0000} frames={9} failures={10}",
            label, report.walkedMeters, report.swaps, report.walkerSteps, report.walkerStateAtEnd,
            report.fell, report.fallSeconds, report.uprightAtEnd, report.standHeight, frames, failures));
        Finish(failures > 0 ? 1 : 0);
    }

    private static float Tilt(Human human)
    {
        return human.vestibularSystem != null ? human.vestibularSystem.GetBodyTilt() : 0f;
    }

    private static Human FindHuman()
    {
        Human[] humans = FindObjectsByType<Human>(FindObjectsSortMode.None);
        return humans.Length > 0 ? humans[0] : null;
    }

    // ─── the scene's own contents, for the record ─────────────────────────

    private ContentDto Describe()
    {
        ContentDto dto = new ContentDto();
        dto.scene = gameObject.scene.name;
        dto.unityVersion = Application.unityVersion;
        dto.buildGuid = Application.buildGUID;
        dto.capturedUtc = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
        dto.argv = string.Join(" ", Environment.GetCommandLineArgs());

        LevelGenerator generator = GetComponent<LevelGenerator>();
        if (generator != null)
        {
            dto.generatorTitle = generator.Title();
            dto.playerSpawnX = generator.playerSpawnX;
            dto.plantDrawnLevel = generator.plantDrawnLevel;
            dto.clearingProps = generator.clearingProps;
        }
        else
        {
            dto.generatorTitle = "(none)";
        }

        PlantTree[] trees = FindObjectsByType<PlantTree>(FindObjectsSortMode.None);
        List<string> grove = new List<string>();
        List<string> sway = new List<string>();
        for (int i = 0; i < trees.Length; i++)
        {
            if (trees[i] == null)
                continue;
            string at = trees[i].name + "@" + trees[i].transform.position.x.ToString("0.0", CultureInfo.InvariantCulture);
            if (trees[i].rig == TreeRig.Static)
                grove.Add(at);
            else
                sway.Add(at);
        }
        dto.groveTrees = grove.Count;
        dto.swayTrees = sway.Count;
        dto.groveXs = string.Join(" ", grove.ToArray());
        dto.swayXs = string.Join(" ", sway.ToArray());

        PlantStrawberry[] berries = FindObjectsByType<PlantStrawberry>(FindObjectsSortMode.None);
        List<string> berryXs = new List<string>();
        for (int i = 0; i < berries.Length; i++)
            if (berries[i] != null)
                berryXs.Add(berries[i].transform.position.x.ToString("0.00", CultureInfo.InvariantCulture));
        dto.berryBushes = berries.Length;
        dto.berryXs = string.Join(" ", berryXs.ToArray());

        PlantFlower[] flowers = FindObjectsByType<PlantFlower>(FindObjectsSortMode.None);
        List<string> flowerXs = new List<string>();
        for (int i = 0; i < flowers.Length; i++)
            if (flowers[i] != null)
                flowerXs.Add(flowers[i].kind + "@"
                    + flowers[i].transform.position.x.ToString("0.00", CultureInfo.InvariantCulture));
        dto.flowers = flowers.Length;
        dto.flowerXs = string.Join(" ", flowerXs.ToArray());

        Bird[] birds = FindObjectsByType<Bird>(FindObjectsSortMode.None);
        int chickens = 0;
        for (int i = 0; i < birds.Length; i++)
            if (birds[i] != null && birds[i].kind == BirdKind.Chicken)
                chickens++;
        dto.birds = birds.Length - chickens;
        dto.chickens = chickens;
        dto.dogs = FindObjectsByType<Dog>(FindObjectsSortMode.None).Length;
        dto.hedgehogs = FindObjectsByType<Hedgehog>(FindObjectsSortMode.None).Length;
        dto.appleTrees = FindObjectsByType<AppleTree>(FindObjectsSortMode.None).Length;

        Human human = FindHuman();
        dto.humans = human != null ? 1 : 0;
        dto.humanX = human != null ? human.transform.position.x : 0f;

        Campfire[] fires = FindObjectsByType<Campfire>(FindObjectsSortMode.None);
        dto.campfire = fires.Length > 0;
        if (fires.Length > 0 && fires[0] != null)
        {
            Vector3 p = fires[0].transform.position;
            dto.campfireAt = p.x.ToString("0.00", CultureInfo.InvariantCulture) + ","
                + p.y.ToString("0.00", CultureInfo.InvariantCulture);
        }

        DayNightCycle clock = DayNightCycle.Instance;
        dto.dayNightCycle = clock != null;
        dto.dayNightSeconds = clock != null ? clock.cycleSeconds : 0f;
        dto.night01Start = DayNightCycle.Night01;
        dto.night01End = DayNightCycle.Night01;

        // The contract of the first slice: no creatures, 9 grove trees, the
        // clearing's own oak, the yard's berries, the bed, the fire, the man.
        int bad = 0;
        if (dto.birds != 0 || dto.chickens != 0 || dto.dogs != 0 || dto.hedgehogs != 0)
        {
            Debug.LogError("CLEARING-CONTENT FAIL creatures: birds=" + dto.birds + " chickens=" + dto.chickens
                + " dogs=" + dto.dogs + " hedgehogs=" + dto.hedgehogs);
            bad++;
        }
        if (dto.groveTrees != 9)
        {
            Debug.LogError("CLEARING-CONTENT FAIL grove trees=" + dto.groveTrees + " (the yard's Static grove is 9)");
            bad++;
        }
        if (dto.swayTrees != 1)
        {
            Debug.LogError("CLEARING-CONTENT FAIL sway trees=" + dto.swayTrees + " (the clearing's own oak is 1)");
            bad++;
        }
        if (dto.berryBushes != 3)
        {
            Debug.LogError("CLEARING-CONTENT FAIL berry bushes=" + dto.berryBushes + " (the yard's own 3)");
            bad++;
        }
        if (dto.flowers != 7)
        {
            Debug.LogError("CLEARING-CONTENT FAIL flowers=" + dto.flowers + " (the bed is 7)");
            bad++;
        }
        if (!dto.campfire || !dto.dayNightCycle || dto.humans != 1)
        {
            Debug.LogError("CLEARING-CONTENT FAIL campfire=" + dto.campfire
                + " clock=" + dto.dayNightCycle + " humans=" + dto.humans);
            bad++;
        }
        Debug.Log("CLEARING-CONTENT " + JsonUtility.ToJson(dto));
        if (bad > 0)
            Debug.LogError("CLEARING-CONTENT FAILURES " + bad);
        return dto;
    }

    private void WriteReport(object report, string fileName)
    {
        string path = Path.Combine(outputFolder, fileName);
        File.WriteAllText(path, JsonUtility.ToJson(report, true));
        Debug.Log("CLEARING-REPORT " + path);
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
