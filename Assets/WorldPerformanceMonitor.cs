using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;

// Frame timing in World: main thread, physics steps, and if the marker is alive —
// Physics2D / scripts / render. College criterion: Main Thread p95 ≤ 16.7 ms.
public class WorldPerformanceMonitor : MonoBehaviour
{
    private const string DefaultLogFolder = @"E:\Games\Unity3D LOGS";
    private const float BudgetFrameMs = 16.7f;
    // One sample: SampleRecorders already means across warmed frames.
    private const int RecorderCapacity = 1;

    [Header("Timing")]
    [Tooltip("Skip the first seconds — spawn and first-step warmup.")]
    public float warmupSeconds = 5f;
    [Tooltip("0 — do not stop. Otherwise leave Play/the app after N seconds (batch World).")]
    public float autoStopSeconds = 0f;
    public float reportIntervalSeconds = 1f;
    public bool logToConsole = true;
    public bool writeToFile = true;
    public string logFileName = "world_perf.txt";
    public string logFolderPath = "";

    private GameProcess gameProcess;
    private NpcRest npcRest;
    private bool npcRestResolved;
    private string sceneName = "";
    private float sessionStart;
    private float nextReportTime;
    private int fixedStepsThisFrame;
    private int maxFixedStepsInWindow;
    private int maxFixedStepsSession;
    private float sumFrameMs;
    private float maxFrameMs;
    private int frameCountInWindow;
    private int framesOverBudget;
    private readonly List<float> frameMsSamples = new List<float>(2048);
    private double sumPhysicsNs;
    private double sumScriptsNs;
    private double sumScriptsUpdateNs;
    private double sumScriptsLateNs;
    private double sumRenderNs;
    private double sumMainNs;
    private double sumGcAlloc;
    private int recorderFrames;
    private bool summaryWritten;
    private bool warmupWindowCleared;

    private ProfilerRecorder mainThread;
    private ProfilerRecorder physics2d;
    private ProfilerRecorder scriptsFixed;
    private ProfilerRecorder scriptsUpdate;
    private ProfilerRecorder scriptsLate;
    private ProfilerRecorder waitForTarget;
    private ProfilerRecorder render;
    private ProfilerRecorder gcAlloc;
    private readonly List<ProfilerRecorder> renderCandidates = new List<ProfilerRecorder>(8);
    private readonly List<string> renderCandidateNames = new List<string>(8);
    // First URP name whose LastValue is non-zero. Empty Valid markers
    // (PERF-1 Camera.Render) stay 0; BestRenderNs must not walk a
    // sample ring on each of them every warmed frame. Capacity 1:
    // SampleRecorders already averages across warmed frames.
    private int latchedRenderIndex = -1;
    private double sumWaitNs;
    private double sumUrpCpuNs;
    private int urpCpuFrames;
    private double sumGpuFrameMs;
    private int gpuTimingFrames;
    private int renderNonZeroFrames;
    private string renderSourceName = "";
    private string scriptsFixedSourceName = "";
    private string scriptsUpdateSourceName = "";
    private string scriptsLateSourceName = "";
    private readonly System.Diagnostics.Stopwatch urpCpuWatch = new System.Diagnostics.Stopwatch();
    private readonly FrameTiming[] frameTimings = new FrameTiming[1];
    // One WORLD_PERF line per report frame — no new StringBuilder each time.
    // npc* fields from NpcRest need ~2 k; 1152 grew every 1 s report.
    private readonly StringBuilder reportLine = new StringBuilder(2048);
    private StreamWriter logWriter;

    // WorldPerfBatch sets this before EnterPlayMode.
    public static float PendingAutoStopSeconds;

    void OnEnable()
    {
        if (PendingAutoStopSeconds > 0f)
        {
            autoStopSeconds = PendingAutoStopSeconds;
            PendingAutoStopSeconds = 0f;
        }
#if UNITY_EDITOR
        // Play Mode reloads the domain: a static set before EnterPlayMode never arrives here.
        if (autoStopSeconds <= 0f)
            autoStopSeconds = UnityEditor.SessionState.GetFloat("WorldPerf.AutoStop", 0f);
        if (autoStopSeconds <= 0f)
            autoStopSeconds = UnityEditor.EditorPrefs.GetFloat("WorldPerf.AutoStop", 0f);
        if (autoStopSeconds > 0f)
        {
            UnityEditor.SessionState.EraseFloat("WorldPerf.AutoStop");
            UnityEditor.EditorPrefs.DeleteKey("WorldPerf.AutoStop");
        }
#endif

        gameProcess = GetComponent<GameProcess>();
        sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        npcRest = GetComponent<NpcRest>();
        if (npcRest != null)
            npcRestResolved = true;
        sessionStart = Time.unscaledTime;
        nextReportTime = sessionStart + warmupSeconds + reportIntervalSeconds;

        // Markers may be missing in batch/nographics — then Valid=false.
        // Capacity 1: LastValue is this frame. A 15-sample ring was a
        // second moving average on top of the session mean (6 Valid
        // markers × 15 GetSample every warmed Update).
        mainThread = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", RecorderCapacity);
        physics2d = StartFirstValid(
            ProfilerCategory.Physics2D,
            "Physics2D.Simulate",
            "Physics2D.FixedUpdate",
            "FixedUpdate.Physics2DFixedUpdate");
        scriptsFixed = StartFirstValid(
            ProfilerCategory.Scripts,
            out scriptsFixedSourceName,
            "FixedUpdate.ScriptRunBehaviourFixedUpdate",
            "ScriptRunBehaviourFixedUpdate");
        scriptsUpdate = StartFirstValid(
            ProfilerCategory.Scripts,
            out scriptsUpdateSourceName,
            "Update.ScriptRunBehaviourUpdate",
            "BehaviourUpdate");
        scriptsLate = StartFirstValid(
            ProfilerCategory.Scripts,
            out scriptsLateSourceName,
            "PreLateUpdate.ScriptRunBehaviourLateUpdate",
            "ScriptRunBehaviourLateUpdate");
        waitForTarget = StartFirstValid(
            ProfilerCategory.Internal,
            "WaitForTargetFPS",
            "Gfx.WaitForPresentOnGfxThread");
        // Camera.Render is Valid on URP but stays 0 — do not take it first.
        // Keep the Valid list so a later name can latch; BestRenderNs
        // probes LastValue and samples only the winner.
        StartRenderCandidates(
            "UniversalRenderTotal",
            "RenderCameraStack",
            "UniversalRenderPipeline.RenderSingleCameraInternal",
            "UniversalRenderPipeline.RenderSingleCamera",
            "RenderLoop.Draw",
            "Camera.Render");
        gcAlloc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC.Alloc", RecorderCapacity);
        RenderPipelineManager.beginCameraRendering += OnBeginUrpCamera;
        RenderPipelineManager.endCameraRendering += OnEndUrpCamera;
    }

    void OnDisable()
    {
        WriteSummaryIfNeeded();
        CloseLogWriter();
        RenderPipelineManager.beginCameraRendering -= OnBeginUrpCamera;
        RenderPipelineManager.endCameraRendering -= OnEndUrpCamera;
        mainThread.Dispose();
        physics2d.Dispose();
        scriptsFixed.Dispose();
        scriptsUpdate.Dispose();
        scriptsLate.Dispose();
        waitForTarget.Dispose();
        DisposeRenderCandidates();
        gcAlloc.Dispose();
    }

    void FixedUpdate()
    {
        fixedStepsThisFrame++;
    }

    void Update()
    {
        float frameMs = Time.unscaledDeltaTime * 1000f;
        sumFrameMs += frameMs;
        if (frameMs > maxFrameMs) maxFrameMs = frameMs;
        if (frameMs > BudgetFrameMs) framesOverBudget++;
        frameCountInWindow++;

        if (fixedStepsThisFrame > maxFixedStepsInWindow)
            maxFixedStepsInWindow = fixedStepsThisFrame;
        if (fixedStepsThisFrame > maxFixedStepsSession)
            maxFixedStepsSession = fixedStepsThisFrame;

        bool warmed = Time.unscaledTime - sessionStart >= warmupSeconds;
        // The first window must not include the Play-enter hitch (seconds, not ms).
        if (warmed && !warmupWindowCleared)
        {
            ResetWindow();
            warmupWindowCleared = true;
            nextReportTime = Time.unscaledTime + reportIntervalSeconds;
        }
        if (warmed)
        {
            frameMsSamples.Add(frameMs);
            SampleRecorders();
        }

        if (Time.unscaledTime < nextReportTime)
        {
            fixedStepsThisFrame = 0;
            TryAutoStop();
            return;
        }

        if (warmed)
            EmitReport();

        ResetWindow();
        nextReportTime = Time.unscaledTime + reportIntervalSeconds;
        fixedStepsThisFrame = 0;
        TryAutoStop();
    }

    private void SampleRecorders()
    {
        recorderFrames++;
        if (mainThread.Valid)
            sumMainNs += mainThread.LastValue;
        if (physics2d.Valid)
            sumPhysicsNs += physics2d.LastValue;
        if (scriptsFixed.Valid)
            sumScriptsNs += scriptsFixed.LastValue;
        if (scriptsUpdate.Valid)
            sumScriptsUpdateNs += scriptsUpdate.LastValue;
        if (scriptsLate.Valid)
            sumScriptsLateNs += scriptsLate.LastValue;
        if (waitForTarget.Valid)
            sumWaitNs += waitForTarget.LastValue;
        double renderNs = BestRenderNs(out string sourceName);
        if (renderNs > 0.0)
        {
            renderNonZeroFrames++;
            if (string.IsNullOrEmpty(renderSourceName))
                renderSourceName = sourceName;
        }
        sumRenderNs += renderNs;
        if (gcAlloc.Valid)
            sumGcAlloc += gcAlloc.LastValue;

        // Editor batch never returns timings; standalone and interactive Play may.
        if (!Application.isEditor || !Application.isBatchMode)
        {
            FrameTimingManager.CaptureFrameTimings();
            uint got = FrameTimingManager.GetLatestTimings(1, frameTimings);
            if (got > 0 && frameTimings[0].gpuFrameTime > 0.0)
            {
                sumGpuFrameMs += frameTimings[0].gpuFrameTime;
                gpuTimingFrames++;
            }
        }
    }

    private void TryAutoStop()
    {
        if (autoStopSeconds <= 0f) return;
        if (Time.unscaledTime - sessionStart < autoStopSeconds) return;

        WriteSummaryIfNeeded();
        Debug.Log("WORLD_PERF_BATCH_DONE seconds=" + autoStopSeconds.ToString("F1"));
#if UNITY_EDITOR
        if (Application.isBatchMode)
            UnityEditor.EditorApplication.Exit(0);
        else
            UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit(0);
#endif
    }

    private void ResetWindow()
    {
        sumFrameMs = 0f;
        maxFrameMs = 0f;
        frameCountInWindow = 0;
        maxFixedStepsInWindow = 0;
        framesOverBudget = 0;
    }

    private void EmitReport()
    {
        if (frameCountInWindow < 1) return;

        float avgFrameMs = sumFrameMs / frameCountInWindow;
        float fps = avgFrameMs > 0.001f ? 1000f / avgFrameMs : 0f;
        float overBudgetFrac = (float)framesOverBudget / frameCountInWindow;

        reportLine.Clear();
        reportLine.Append("WORLD_PERF t=").Append(Time.unscaledTime.ToString("F1"));
        reportLine.Append(" fps=").Append(fps.ToString("F1"));
        reportLine.Append(" frameMs_avg=").Append(avgFrameMs.ToString("F2"));
        reportLine.Append(" frameMs_max=").Append(maxFrameMs.ToString("F2"));
        reportLine.Append(" fixedSteps_max=").Append(maxFixedStepsInWindow);
        reportLine.Append(" overBudget16ms=").Append(overBudgetFrac.ToString("F3"));
        reportLine.Append(" fixedDelta=").Append(Time.fixedDeltaTime.ToString("F3"));
        if (gameProcess != null)
        {
            reportLine.Append(" humans=").Append(gameProcess.humanCount);
            reportLine.Append(" birds=").Append(gameProcess.birdCount);
            reportLine.Append(" chickens=").Append(gameProcess.chickenCount);
            reportLine.Append(" chickenNearby=").Append(gameProcess.chickenNearbyCount);
            reportLine.Append(" chickenParkX=").Append(gameProcess.chickenParkOffsetX.ToString("F0"));
            reportLine.Append(" dogs=").Append(gameProcess.dogCount);
        }
        if (!string.IsNullOrEmpty(sceneName))
            reportLine.Append(" scene=").Append(sceneName);
        NpcRest rest = CachedNpcRest();
        if (rest != null)
        {
            reportLine.Append(" npcRest=").Append(rest.RestingCount);
            reportLine.Append('/').Append(rest.SlotCount);
            reportLine.Append(" npcLive=").Append(rest.LiveCount);
            reportLine.Append(" npcKeepAwake=").Append(rest.KeepAwakeCount);
            reportLine.Append(" npcHeroSlots=").Append(rest.HeroSlotCount);
            reportLine.Append(" npcTight=").Append(rest.TightRestCount);
            reportLine.Append(" npcLiveTight=").Append(rest.LiveTightCount);
            // Includes world-root HuntBeak when simulated (not in slot.bodies).
            reportLine.Append(" npcLiveBodies=").Append(rest.LiveDynamicBodies);
            reportLine.Append(" npcLiveKin=").Append(rest.LiveKinematicBodies);
            reportLine.Append(" npcRestBodies=").Append(rest.RestingBodies);
            // Measured simulated=false, not assumed equal to RestingBodies.
            reportLine.Append(" npcRestUnsim=").Append(rest.RestingUnsimulatedBodies);
            reportLine.Append(" npcLiveCcd=").Append(rest.LiveCcdBodies);
            reportLine.Append(" npcRestCcd=").Append(rest.RestingCcdBodies);
            reportLine.Append(" npcRestCcdNow=").Append(rest.RestingActiveCcdBodies);
            reportLine.Append(" npcRestColOff=").Append(rest.RestingColliderOffCount);
            reportLine.Append(" npcRestJointOff=").Append(rest.RestingJointOffCount);
            reportLine.Append(" npcRestFarSkip=").Append(rest.FarRestSkipCount);
            // LateUpdate leftover-sit island skipped (camera still).
            reportLine.Append(" npcFarTickSkip=").Append(rest.FarRestTickSkipCount);
            // Resting slots whose HuntBeak is unsim at ParkY. Idle live
            // Sit is also unsim (Bird park); that is not this count.
            reportLine.Append(" npcRestHunt=").Append(rest.RestingHuntBeakCount);
            reportLine.Append(" npcHuntCache=").Append(rest.HuntBeakCacheCount);
            // Bind pairs world-root HuntBeak. Tick does not walk roots.
            reportLine.Append(" npcHuntScan=").Append(rest.HuntBeakScanCount);
            // 0: Tick does not poll scene roots.
            reportLine.Append(" npcHuntPoll=").Append(rest.HuntBeakPollArmed ? 1 : 0);
            reportLine.Append(" npcInsects=").Append(rest.InsectCount);
            reportLine.Append(" npcRestLoop=").Append(rest.RestingLoopCount);
            // Design-armed, not earlyTick.enabled this Update: extra
            // 200 Hz steps disarm Early until LateUpdate. World hens
            // do not arm this (npcNearHeavy=0).
            reportLine.Append(" npcEarly=").Append(rest.PhysicsRestArmed ? 1 : 0);
            reportLine.Append(" npcNearRest=").Append(rest.NearRestWalkCount);
            reportLine.Append(" npcNearHeavy=").Append(rest.NearRestHeavyCount);
            reportLine.Append(" npcRestDogs=").Append(rest.RestingDogCount);
            reportLine.Append(" npcRestFlyers=").Append(rest.RestingFlyerCount);
            reportLine.Append(" npcLiveHens=").Append(rest.LiveHenCount);
            // Walker-band leftover (Sit or Walk/Peck) past the clip.
            reportLine.Append(" npcLiveHenDriveOff=").Append(rest.LiveHenDriveOffCount);
            reportLine.Append(" npcLiveHenLoopOff=").Append(rest.LiveHenLoopOffCount);
            // Walker-band leftover: Dynamic probe left Simulate.
            // Sleep on dirt does not hold; unsim does.
            reportLine.Append(" npcLiveHenUnsim=").Append(rest.LiveHenUnsimCount);
            reportLine.Append(" npcLiveHenDriveSkip=").Append(rest.LiveHenDriveSkipCount);
            // Look-park HuntBeak: unsim Kinematic StartAwake trigger,
            // include Nothing (not leftover Peck / Bird Default include).
            reportLine.Append(" npcLiveHenHuntPark=").Append(rest.LiveHenHuntParkCount);
            reportLine.Append(" npcRestHens=").Append(rest.RestingHenCount);
            reportLine.Append(" npcLiveHogs=").Append(rest.LiveHedgehogCount);
            reportLine.Append(" npcRestHogs=").Append(rest.RestingHedgehogCount);
            reportLine.Append(" npcLiveFlock=").Append(rest.LiveFlockDriveCount);
            reportLine.Append(" npcLiveFlockOwned=").Append(rest.LiveFlockOwnedCount);
            reportLine.Append(" npcPhysRest=").Append(rest.PhysicsRestTicks);
            reportLine.Append(" npcPhysSkip=").Append(rest.PhysicsRestSkips);
            reportLine.Append(" npcPhysLiveWalk=").Append(rest.PhysicsLiveWalkCount);
            // Includes Dog.LateUpdate off (no energyRest gate on those Apply*).
            reportLine.Append(" npcRestDriveOff=").Append(rest.RestingDriveOffCount);
            reportLine.Append(" npcRestAt=").Append(rest.LastRestAt.ToString("F1"));
            reportLine.Append(" npcWakeAt=").Append(rest.LastWakeAt.ToString("F1"));
            reportLine.Append(" npcFlyRestAt=").Append(rest.LastFlyRestAt.ToString("F1"));
            reportLine.Append(" npcFlyWakeAt=").Append(rest.LastFlyWakeAt.ToString("F1"));
            reportLine.Append(" npcQuadRestAt=").Append(rest.LastQuadRestAt.ToString("F1"));
            reportLine.Append(" npcQuadWakeAt=").Append(rest.LastQuadWakeAt.ToString("F1"));
            reportLine.Append(" npcCamX=").Append(rest.LastCamX.ToString("F2"));
            reportLine.Append(" npcHalfView=").Append(rest.LastHalfView.ToString("F2"));
        }

        string line = reportLine.ToString();
        if (logToConsole) Debug.Log(line);
        WriteLogLine(line);
    }

    private void WriteLogLine(string line)
    {
        if (!writeToFile)
            return;
        if (logWriter == null)
        {
            string folder = string.IsNullOrEmpty(logFolderPath) ? DefaultLogFolder : logFolderPath;
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
            logWriter = new StreamWriter(Path.Combine(folder, logFileName), true, Encoding.UTF8);
            logWriter.AutoFlush = true;
        }

        logWriter.WriteLine(line);
    }

    private void CloseLogWriter()
    {
        if (logWriter == null)
            return;
        logWriter.Dispose();
        logWriter = null;
    }

    private void WriteSummaryIfNeeded()
    {
        if (summaryWritten) return;
        if (frameMsSamples.Count < 1) return;
        summaryWritten = true;

        float[] sorted = frameMsSamples.ToArray();
        System.Array.Sort(sorted);
        float p50 = Percentile(sorted, 50f);
        float p95 = Percentile(sorted, 95f);
        float p99 = Percentile(sorted, 99f);
        float avg = 0f;
        for (int i = 0; i < sorted.Length; i++)
            avg += sorted[i];
        avg /= sorted.Length;

        int nRec = Mathf.Max(1, recorderFrames);
        float mainAvg = (float)(sumMainNs / nRec * 1e-6);
        float physAvg = physics2d.Valid ? (float)(sumPhysicsNs / nRec * 1e-6) : -1f;
        float scriptAvg = scriptsFixed.Valid ? (float)(sumScriptsNs / nRec * 1e-6) : -1f;
        float scriptUpdateAvg = scriptsUpdate.Valid ? (float)(sumScriptsUpdateNs / nRec * 1e-6) : -1f;
        float scriptLateAvg = scriptsLate.Valid ? (float)(sumScriptsLateNs / nRec * 1e-6) : -1f;
        bool renderValid = renderCandidates.Count > 0;
        float renderAvg = renderValid ? (float)(sumRenderNs / nRec * 1e-6) : -1f;
        bool renderEmpty = renderValid && renderNonZeroFrames < 1;
        float waitAvg = waitForTarget.Valid ? (float)(sumWaitNs / nRec * 1e-6) : -1f;
        bool waitEmpty = waitForTarget.Valid && waitAvg < 0.05f;
        float gcAvg = gcAlloc.Valid ? (float)(sumGcAlloc / nRec) : -1f;
        float busyAvg = (mainAvg > 0f && waitAvg > 0f) ? Mathf.Max(0f, mainAvg - waitAvg) : -1f;
        bool busyTrusted = waitForTarget.Valid && !waitEmpty;
        float urpCpuAvg = urpCpuFrames > 0 ? (float)(sumUrpCpuNs / urpCpuFrames * 1e-6) : -1f;
        float gpuFrameAvg = gpuTimingFrames > 0 ? (float)(sumGpuFrameMs / gpuTimingFrames) : -1f;
        float physSafe = physAvg > 0f ? physAvg : 0f;
        float scriptSafe = scriptAvg > 0f ? scriptAvg : 0f;
        float scriptUpdateSafe = scriptUpdateAvg > 0f ? scriptUpdateAvg : 0f;
        float scriptLateSafe = scriptLateAvg > 0f ? scriptLateAvg : 0f;
        float renderSafe = (!renderEmpty && renderAvg > 0f) ? renderAvg : 0f;
        float urpSafe = urpCpuAvg > 0f ? urpCpuAvg : 0f;
        float knownWork = physSafe + scriptSafe + scriptUpdateSafe + scriptLateSafe
            + Mathf.Max(renderSafe, urpSafe);
        float remainderInclWait = avg - knownWork;

        // Frame ~16.7 ms at targetFrameRate 60 is the cap, not CPU busy.
        // busyMain in batch is untrusted when WaitForTargetFPS records ~0.
        bool green = maxFixedStepsSession <= 4 && (busyAvg < 0f || busyAvg <= BudgetFrameMs)
            && p95 <= BudgetFrameMs + 2f;
        bool greenKnownWork = maxFixedStepsSession <= 5 && p95 <= BudgetFrameMs + 2f
            && knownWork <= BudgetFrameMs;

        var json = new StringBuilder(3072);
        json.Append('{');
        AppendNum(json, "warmupSeconds", warmupSeconds, false);
        AppendNum(json, "autoStopSeconds", autoStopSeconds);
        AppendNum(json, "sampleFrames", sorted.Length);
        AppendNum(json, "avgFrameMs", avg);
        AppendNum(json, "p50FrameMs", p50);
        AppendNum(json, "p95FrameMs", p95);
        AppendNum(json, "p99FrameMs", p99);
        AppendNum(json, "maxFixedSteps", maxFixedStepsSession);
        AppendNum(json, "mainThreadAvgMs", mainAvg);
        AppendNum(json, "physics2dAvgMs", physAvg);
        AppendNum(json, "scriptsFixedAvgMs", scriptAvg);
        AppendNum(json, "scriptsUpdateAvgMs", scriptUpdateAvg);
        AppendNum(json, "scriptsLateAvgMs", scriptLateAvg);
        AppendNum(json, "renderAvgMs", renderAvg);
        AppendNum(json, "waitForTargetAvgMs", waitAvg);
        AppendNum(json, "busyMainAvgMs", busyAvg);
        AppendNum(json, "urpContextCpuAvgMs", urpCpuAvg);
        AppendNum(json, "gpuFrameAvgMs", gpuFrameAvg);
        AppendNum(json, "knownWorkAvgMs", knownWork);
        AppendNum(json, "remainderInclWaitMs", remainderInclWait);
        // OPT-3 gate vs cycle39 World Test1 (0.414 ms knownWork).
        // Capture JSON alone — no chat table. Delta is signed.
        const float cycle39KnownWorkMs = 0.414367765f;
        AppendNum(json, "knownWorkBaselineMs", cycle39KnownWorkMs);
        AppendNum(json, "knownWorkDeltaMs", knownWork - cycle39KnownWorkMs);
        AppendNum(json, "gcAllocAvgBytes", gcAvg);
        AppendBool(json, "physics2dMarkerValid", physics2d.Valid);
        AppendBool(json, "scriptsMarkerValid", scriptsFixed.Valid);
        AppendBool(json, "scriptsUpdateMarkerValid", scriptsUpdate.Valid);
        AppendBool(json, "scriptsLateMarkerValid", scriptsLate.Valid);
        AppendBool(json, "renderMarkerValid", renderValid);
        AppendBool(json, "renderMarkerEmpty", renderEmpty);
        AppendBool(json, "waitMarkerEmpty", waitEmpty);
        AppendBool(json, "busyMainTrusted", busyTrusted);
        AppendStr(json, "scriptsSourceName", scriptsFixedSourceName);
        AppendStr(json, "scriptsFixedSourceName", scriptsFixedSourceName);
        AppendStr(json, "scriptsUpdateSourceName", scriptsUpdateSourceName);
        AppendStr(json, "scriptsLateSourceName", scriptsLateSourceName);
        AppendStr(json, "renderSourceName", renderSourceName);
        AppendBool(json, "jobsMultithreading", Physics2D.jobOptions.useMultithreading);
        AppendNum(json, "fixedDelta", Time.fixedDeltaTime);
        AppendNum(json, "targetFrameRate", Application.targetFrameRate);
        NpcRest rest = CachedNpcRest();
        // Auto-stop writes from Update; NpcRest Tick is LateUpdate (110).
        // Refresh so OPT-3 gate tokens are this-frame, not last display.
        // NpcRest also re-latches far leftover CCD / drive-off (not the
        // rest-edge cache) so npcRestCcdNow / npcRestDriveOff stay honest.
        if (rest != null)
            rest.RefreshForCapture();
        AppendNum(json, "npcSlotCount", rest != null ? rest.SlotCount : 0);
        AppendNum(json, "npcRestingCount", rest != null ? rest.RestingCount : 0);
        AppendNum(json, "npcLiveCount", rest != null ? rest.LiveCount : 0);
        AppendNum(json, "npcKeepAwakeCount", rest != null ? rest.KeepAwakeCount : 0);
        // Contract: player Human is never a rest slot. Gate = 0.
        AppendNum(json, "npcHeroSlots", rest != null ? rest.HeroSlotCount : 0);
        AppendNum(json, "npcTightRestCount", rest != null ? rest.TightRestCount : 0);
        AppendNum(json, "npcLiveTightCount", rest != null ? rest.LiveTightCount : 0);
        // Includes world-root HuntBeak when simulated (not in slot.bodies).
        // Report line alias is npcLiveBodies — OPT-3 gate wants 0 on World spawn.
        AppendNum(json, "npcLiveDynamicBodies", rest != null ? rest.LiveDynamicBodies : 0);
        AppendNum(json, "npcLiveBodies", rest != null ? rest.LiveDynamicBodies : 0);
        AppendNum(json, "npcLiveKinematicBodies", rest != null ? rest.LiveKinematicBodies : 0);
        AppendNum(json, "npcRestingBodies", rest != null ? rest.RestingBodies : 0);
        AppendNum(json, "npcRestingUnsimulatedBodies", rest != null ? rest.RestingUnsimulatedBodies : 0);
        AppendNum(json, "npcLiveCcdBodies", rest != null ? rest.LiveCcdBodies : 0);
        AppendNum(json, "npcRestingCcdBodies", rest != null ? rest.RestingCcdBodies : 0);
        AppendNum(json, "npcRestingActiveCcdBodies", rest != null ? rest.RestingActiveCcdBodies : 0);
        AppendNum(json, "npcRestingColliderOffCount", rest != null ? rest.RestingColliderOffCount : 0);
        AppendNum(json, "npcRestingJointOffCount", rest != null ? rest.RestingJointOffCount : 0);
        AppendNum(json, "npcFarRestSkipCount", rest != null ? rest.FarRestSkipCount : 0);
        // LateUpdate leftover-sit island skipped (camera still).
        AppendNum(json, "npcFarRestTickSkipCount", rest != null ? rest.FarRestTickSkipCount : 0);
        // Resting-slot HuntBeak unsim at ParkY (Bird ParkHuntSlab).
        // Idle live Sit park is not this.
        AppendNum(json, "npcRestingHuntBeakCount", rest != null ? rest.RestingHuntBeakCount : 0);
        AppendNum(json, "npcHuntBeakCacheCount", rest != null ? rest.HuntBeakCacheCount : 0);
        // Bind pairs world-root HuntBeak. Not a per-frame root walk.
        AppendNum(json, "npcHuntBeakScanCount", rest != null ? rest.HuntBeakScanCount : 0);
        AppendBool(json, "npcHuntBeakPollArmed", rest != null && rest.HuntBeakPollArmed);
        AppendNum(json, "npcInsectCount", rest != null ? rest.InsectCount : 0);
        AppendNum(json, "npcRestingLoopCount", rest != null ? rest.RestingLoopCount : 0);
        // Live walkers exist — not earlyTick.enabled this Update.
        // npcEarly follows NearRestHeavyCount (dog/hog at the clip).
        AppendBool(json, "npcPhysicsRestArmed", rest != null && rest.PhysicsRestArmed);
        AppendNum(json, "npcNearRestWalkCount", rest != null ? rest.NearRestWalkCount : 0);
        AppendNum(json, "npcNearRestHeavyCount", rest != null ? rest.NearRestHeavyCount : 0);
        AppendNum(json, "npcRestingDogCount", rest != null ? rest.RestingDogCount : 0);
        AppendNum(json, "npcRestingFlyerCount", rest != null ? rest.RestingFlyerCount : 0);
        AppendNum(json, "npcLiveHenCount", rest != null ? rest.LiveHenCount : 0);
        // Walker-band leftover (Sit or Walk/Peck) past the clip.
        AppendNum(json, "npcLiveHenDriveOffCount", rest != null ? rest.LiveHenDriveOffCount : 0);
        AppendNum(json, "npcLiveHenLoopOffCount", rest != null ? rest.LiveHenLoopOffCount : 0);
        // Dynamic leftover still in the walker band, not in Simulate.
        AppendNum(json, "npcLiveHenUnsimCount", rest != null ? rest.LiveHenUnsimCount : 0);
        AppendNum(json, "npcLiveHenDriveSkipCount", rest != null ? rest.LiveHenDriveSkipCount : 0);
        // Look-park HuntBeak: unsim Kinematic StartAwake trigger, include 0.
        AppendNum(json, "npcLiveHenHuntParkCount", rest != null ? rest.LiveHenHuntParkCount : 0);
        // Short aliases match WORLD_PERF report tokens (OPT-3 gate).
        // cycle39 capture had zero npc*; cycle 70+ must gate from JSON alone.
        AppendNum(json, "npcRestDogs", rest != null ? rest.RestingDogCount : 0);
        AppendNum(json, "npcRestFlyers", rest != null ? rest.RestingFlyerCount : 0);
        AppendNum(json, "npcLiveHens", rest != null ? rest.LiveHenCount : 0);
        AppendNum(json, "npcLiveHenDriveOff", rest != null ? rest.LiveHenDriveOffCount : 0);
        AppendNum(json, "npcLiveHenLoopOff", rest != null ? rest.LiveHenLoopOffCount : 0);
        AppendNum(json, "npcLiveHenUnsim", rest != null ? rest.LiveHenUnsimCount : 0);
        AppendNum(json, "npcLiveHenDriveSkip", rest != null ? rest.LiveHenDriveSkipCount : 0);
        AppendNum(json, "npcLiveHenHuntPark", rest != null ? rest.LiveHenHuntParkCount : 0);
        AppendNum(json, "npcRestHens", rest != null ? rest.RestingHenCount : 0);
        AppendNum(json, "npcRestCcdNow", rest != null ? rest.RestingActiveCcdBodies : 0);
        AppendNum(json, "npcRestUnsim", rest != null ? rest.RestingUnsimulatedBodies : 0);
        AppendNum(json, "npcRestDriveOff", rest != null ? rest.RestingDriveOffCount : 0);
        AppendNum(json, "npcFarTickSkip", rest != null ? rest.FarRestTickSkipCount : 0);
        AppendNum(json, "npcPhysLiveWalk", rest != null ? rest.PhysicsLiveWalkCount : 0);
        AppendNum(json, "npcEarly", rest != null && rest.PhysicsRestArmed ? 1 : 0);
        AppendNum(json, "npcHuntPoll", rest != null && rest.HuntBeakPollArmed ? 1 : 0);
        AppendNum(json, "npcRestingHenCount", rest != null ? rest.RestingHenCount : 0);
        AppendNum(json, "npcLiveHedgehogCount", rest != null ? rest.LiveHedgehogCount : 0);
        AppendNum(json, "npcRestingHedgehogCount", rest != null ? rest.RestingHedgehogCount : 0);
        AppendNum(json, "npcLiveFlockDriveCount", rest != null ? rest.LiveFlockDriveCount : 0);
        AppendNum(json, "npcLiveFlockOwnedCount", rest != null ? rest.LiveFlockOwnedCount : 0);
        AppendNum(json, "npcPhysicsRestTicks", rest != null ? rest.PhysicsRestTicks : 0);
        AppendNum(json, "npcPhysicsRestSkips", rest != null ? rest.PhysicsRestSkips : 0);
        AppendNum(json, "npcPhysicsLiveWalkCount", rest != null ? rest.PhysicsLiveWalkCount : 0);
        // Dog.LateUpdate included: leftover sit must drop that look script.
        AppendNum(json, "npcRestingDriveOffCount", rest != null ? rest.RestingDriveOffCount : 0);
        AppendNum(json, "birdCount", gameProcess != null ? gameProcess.birdCount : 0);
        AppendNum(json, "chickenCount", gameProcess != null ? gameProcess.chickenCount : 0);
        AppendNum(json, "chickenNearbyCount", gameProcess != null ? gameProcess.chickenNearbyCount : 0);
        AppendNum(json, "chickenParkOffsetX", gameProcess != null ? gameProcess.chickenParkOffsetX : 0f);
        AppendNum(json, "dogCount", gameProcess != null ? gameProcess.dogCount : 0);
        AppendNum(json, "treeCount", gameProcess != null ? gameProcess.treeCount : 0);
        AppendStr(json, "sceneName", sceneName);
        AppendNum(json, "npcRestAt", rest != null ? rest.LastRestAt : 0f);
        AppendNum(json, "npcWakeAt", rest != null ? rest.LastWakeAt : 0f);
        AppendNum(json, "npcFlyRestAt", rest != null ? rest.LastFlyRestAt : 0f);
        AppendNum(json, "npcFlyWakeAt", rest != null ? rest.LastFlyWakeAt : 0f);
        AppendNum(json, "npcQuadRestAt", rest != null ? rest.LastQuadRestAt : 0f);
        AppendNum(json, "npcQuadWakeAt", rest != null ? rest.LastQuadWakeAt : 0f);
        AppendNum(json, "npcCamX", rest != null ? rest.LastCamX : 0f);
        AppendNum(json, "npcHalfView", rest != null ? rest.LastHalfView : 0f);
        AppendNum(json, "vSync", QualitySettings.vSyncCount);
        AppendBool(json, "batchMode", Application.isBatchMode);
        AppendBool(json, "greenMainThreadP95", green);
        AppendBool(json, "greenKnownWork", greenKnownWork);
        json.Append('}');

        string jsonText = json.ToString();
        string runs = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Trials", "Runs"));
        if (!Directory.Exists(runs)) Directory.CreateDirectory(runs);
        string jsonPath = Path.Combine(runs, "world_perf_capture.json");
        File.WriteAllText(jsonPath, jsonText, Encoding.UTF8);
        Debug.Log("WORLD_PERF_JSON " + jsonPath);
        // WorldPerfBatch always opens World.unity. A Level1 Play must not
        // be mistaken for that capture: keep the runner name and a scene copy.
        string sceneTag = SanitizeFileToken(sceneName);
        if (!string.IsNullOrEmpty(sceneTag) && sceneTag != "World")
        {
            string scenePath = Path.Combine(runs, "world_perf_capture_" + sceneTag + ".json");
            File.WriteAllText(scenePath, jsonText, Encoding.UTF8);
            Debug.Log("WORLD_PERF_JSON_SCENE " + scenePath);
        }

        string folder = string.IsNullOrEmpty(logFolderPath) ? DefaultLogFolder : logFolderPath;
        if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "world_perf_capture.json"), jsonText, Encoding.UTF8);
    }

    void Start()
    {
        // OnEnable can run before GameProcess.Start adds NpcRest when
        // this monitor is already on the object. BindNpcRest from
        // AttachNpcRest is the spawn cache; this is the one-shot backup.
        if (npcRest == null)
            npcRest = GetComponent<NpcRest>();
        if (npcRest != null)
            npcRestResolved = true;
    }

    // Spawn only. Does not run on the frame path.
    public void BindNpcRest(NpcRest rest)
    {
        npcRest = rest;
        npcRestResolved = true;
    }

    private NpcRest CachedNpcRest()
    {
        if (npcRestResolved)
            return npcRest;
        npcRest = GetComponent<NpcRest>();
        npcRestResolved = true;
        return npcRest;
    }

    private void OnBeginUrpCamera(ScriptableRenderContext context, Camera camera)
    {
        urpCpuWatch.Restart();
    }

    private void OnEndUrpCamera(ScriptableRenderContext context, Camera camera)
    {
        if (!urpCpuWatch.IsRunning)
            return;
        urpCpuWatch.Stop();
        if (!warmupWindowCleared)
            return;
        double ns = urpCpuWatch.ElapsedTicks * (1e9 / System.Diagnostics.Stopwatch.Frequency);
        sumUrpCpuNs += ns;
        urpCpuFrames++;
    }

    private void StartRenderCandidates(params string[] names)
    {
        DisposeRenderCandidates();
        for (int i = 0; i < names.Length; i++)
        {
            ProfilerRecorder rec = ProfilerRecorder.StartNew(ProfilerCategory.Render, names[i], RecorderCapacity);
            if (rec.Valid)
            {
                renderCandidates.Add(rec);
                renderCandidateNames.Add(names[i]);
            }
            else
                rec.Dispose();
        }

        render = renderCandidates.Count > 0 ? renderCandidates[0] : default;
    }

    private void DisposeRenderCandidates()
    {
        for (int i = 0; i < renderCandidates.Count; i++)
            renderCandidates[i].Dispose();
        renderCandidates.Clear();
        renderCandidateNames.Clear();
        latchedRenderIndex = -1;
        render = default;
    }

    private double BestRenderNs(out string sourceName)
    {
        sourceName = "";
        if (latchedRenderIndex >= 0 && latchedRenderIndex < renderCandidates.Count)
        {
            ProfilerRecorder latched = renderCandidates[latchedRenderIndex];
            if (latched.Valid)
            {
                sourceName = latchedRenderIndex < renderCandidateNames.Count
                    ? renderCandidateNames[latchedRenderIndex]
                    : "";
                return latched.LastValue;
            }

            latchedRenderIndex = -1;
        }

        double best = 0.0;
        int bestIndex = -1;
        for (int i = 0; i < renderCandidates.Count; i++)
        {
            ProfilerRecorder rec = renderCandidates[i];
            if (!rec.Valid)
                continue;
            // Empty URP names stay Valid at 0 (cycle 39 renderAvgMs=0).
            // LastValue is O(1). Do not walk a sample ring.
            if (rec.LastValue <= 0.0)
                continue;
            double ns = rec.LastValue;
            if (ns > best)
            {
                best = ns;
                bestIndex = i;
                sourceName = i < renderCandidateNames.Count ? renderCandidateNames[i] : "";
            }
        }

        if (bestIndex >= 0)
            latchedRenderIndex = bestIndex;
        return best;
    }

    private static ProfilerRecorder StartFirstValid(ProfilerCategory category, params string[] names)
    {
        return StartFirstValid(category, out _, names);
    }

    private static ProfilerRecorder StartFirstValid(
        ProfilerCategory category,
        out string sourceName,
        params string[] names)
    {
        sourceName = "";
        for (int i = 0; i < names.Length; i++)
        {
            ProfilerRecorder rec = ProfilerRecorder.StartNew(category, names[i], RecorderCapacity);
            if (rec.Valid)
            {
                sourceName = names[i];
                return rec;
            }
            rec.Dispose();
        }

        return default;
    }

    private static float Percentile(float[] sorted, float p)
    {
        if (sorted.Length == 1) return sorted[0];
        float idx = (p / 100f) * (sorted.Length - 1);
        int lo = Mathf.FloorToInt(idx);
        int hi = Mathf.CeilToInt(idx);
        if (lo == hi) return sorted[lo];
        float t = idx - lo;
        return sorted[lo] * (1f - t) + sorted[hi] * t;
    }

    private static string SanitizeFileToken(string name)
    {
        if (string.IsNullOrEmpty(name))
            return "";
        char[] chars = name.ToCharArray();
        int w = 0;
        for (int i = 0; i < chars.Length; i++)
        {
            char c = chars[i];
            if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z')
                || (c >= '0' && c <= '9') || c == '_' || c == '-')
                chars[w++] = c;
        }

        return w > 0 ? new string(chars, 0, w) : "";
    }

    private static void AppendNum(StringBuilder json, string key, float value, bool comma = true)
    {
        if (comma) json.Append(',');
        json.Append('"').Append(key).Append("\":");
        json.Append(value.ToString("G9", CultureInfo.InvariantCulture));
    }

    private static void AppendNum(StringBuilder json, string key, int value, bool comma = true)
    {
        if (comma) json.Append(',');
        json.Append('"').Append(key).Append("\":");
        json.Append(value.ToString(CultureInfo.InvariantCulture));
    }

    private static void AppendBool(StringBuilder json, string key, bool value, bool comma = true)
    {
        if (comma) json.Append(',');
        json.Append('"').Append(key).Append("\":");
        json.Append(value ? "true" : "false");
    }

    private static void AppendStr(StringBuilder json, string key, string value, bool comma = true)
    {
        if (comma) json.Append(',');
        json.Append('"').Append(key).Append("\":\"");
        if (!string.IsNullOrEmpty(value))
        {
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c == '"' || c == '\\')
                    json.Append('\\');
                json.Append(c);
            }
        }

        json.Append('"');
    }
}
