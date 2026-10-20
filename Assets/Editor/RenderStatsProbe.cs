#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Unity.Profiling;

// A/B rendering-stats probe for the optim-8 s5 draw-call pass (Rendering Statistics
// equivalents: Draw Calls / SetPass Calls / Batches / Triangles / Vertices).
//
// Entering play mode reloads the script domain, so nothing may rely on static state
// across EnterPlayMode. The request travels in SessionState (survives the reload) and
// [InitializeOnLoadMethod] Bootstrap re-registers the handlers after every reload.
//
// Batchmode run: opens World, plays ~N s in two phases:
//   phase A: plain play loop
//   phase B: plain loop + explicit Camera.Render() into a RenderTexture each frame
//            (batch has no Game view; the explicit render guarantees the counters tick)
// Writes <project>/Trials/Runs/render_stats_<tag>.json, then exits the editor.
// Log lines: RENDER_STATS start / RENDER_STATS_OK <path>.
//
// CLI: -executeMethod RenderStatsProbe.Run -tag before [-probeSeconds 24] [-renderSplitSeconds 12]
public static class RenderStatsProbe
{
    private const string ScenePath = "Assets/Scenes/World.unity";
    private const string RequestKey = "RenderStatsProbe.Request";
    private const string DoneKey = "RenderStatsProbe.Done";

    // Canonical name + aliases, tried in order (first Valid wins). Counter names come
    // from the Rendering Profiler module (ProfilerCategory.Render).
    private static readonly string[][] CounterCandidates =
    {
        new[] { "Draw Calls Count", "Draw Calls" },
        new[] { "SetPass Calls Count", "SetPass Calls" },
        new[] { "Batches Count", "Batches" },
        new[] { "Triangles Count", "Triangles" },
        new[] { "Vertices Count", "Vertices" },
    };

    private static string tag = "probe";
    private static float totalSeconds = 24f;
    private static float splitSeconds = 12f;
    private static readonly System.Diagnostics.Stopwatch watch = new System.Diagnostics.Stopwatch();
    private static readonly List<ProfilerRecorder> recorders = new List<ProfilerRecorder>();
    private static readonly List<string> recorderNames = new List<string>();
    private static readonly List<string> recorderSources = new List<string>();
    private static readonly List<List<double>> phaseA = new List<List<double>>();
    private static readonly List<List<double>> phaseB = new List<List<double>>();
    private static int framesA, framesB;
    private static int camCount, rendererCount, spriteCount;
    private static Camera probeCamera;
    private static RenderTexture probeRt;
    private static bool sampling;
    private static bool finished;

    [InitializeOnLoadMethod]
    private static void Bootstrap()
    {
        EditorApplication.playModeStateChanged -= OnPlayState;
        EditorApplication.playModeStateChanged += OnPlayState;
        // Safety net for event orderings across the play-mode reload: if a request is
        // pending and play is already running, arm the sampler on the next editor tick.
        EditorApplication.delayCall -= LateArm;
        EditorApplication.delayCall += LateArm;
    }

    private static void LateArm()
    {
        if (SessionState.GetString(RequestKey, "") != "" && EditorApplication.isPlaying && !sampling)
            StartSampling(SessionState.GetString(RequestKey, ""));
    }

    public static void Run()
    {
        ParseArgs();
        string sceneFull = Path.Combine(Application.dataPath, "..", ScenePath);
        if (!File.Exists(sceneFull))
        {
            Debug.LogError("RENDER_STATS_FAIL missing scene " + ScenePath);
            EditorApplication.Exit(2);
            return;
        }

        SessionState.SetString(RequestKey, tag + "|" + F(totalSeconds) + "|" + F(splitSeconds));
        SessionState.EraseString(DoneKey);
        EditorSceneManager.OpenScene(ScenePath);
        EditorApplication.playModeStateChanged -= OnPlayState;
        EditorApplication.playModeStateChanged += OnPlayState;
        EditorApplication.isPlaying = true;
    }

    private static void ParseArgs()
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
        {
            switch (args[i])
            {
                case "-tag": tag = args[i + 1]; break;
                case "-probeSeconds":
                    float.TryParse(args[i + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out totalSeconds);
                    break;
                case "-renderSplitSeconds":
                    float.TryParse(args[i + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out splitSeconds);
                    break;
            }
        }
    }

    private static void OnPlayState(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            string req = SessionState.GetString(RequestKey, "");
            if (req != "" && !sampling)
                StartSampling(req);
            return;
        }
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            string done = SessionState.GetString(DoneKey, "");
            if (done != "")
            {
                SessionState.EraseString(DoneKey);
                SessionState.EraseString(RequestKey);
                EditorApplication.Exit(done == "FAIL" ? 5 : 0);
            }
        }
    }

    private static void StartSampling(string request)
    {
        // request = tag|totalSeconds|splitSeconds
        string[] parts = request.Split('|');
        if (parts.Length == 3)
        {
            tag = parts[0];
            float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out totalSeconds);
            float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out splitSeconds);
        }
        sampling = true;
        finished = false;

        for (int g = 0; g < CounterCandidates.Length; g++)
        {
            string[] names = CounterCandidates[g];
            ProfilerRecorder found = default;
            string source = "";
            for (int i = 0; i < names.Length; i++)
            {
                ProfilerRecorder rec = ProfilerRecorder.StartNew(ProfilerCategory.Render, names[i], 1);
                if (rec.Valid)
                {
                    found = rec;
                    source = names[i];
                    break;
                }
                rec.Dispose();
            }
            if (source != "")
            {
                recorders.Add(found);
                recorderNames.Add(names[0]);
                recorderSources.Add(source);
                phaseA.Add(new List<double>(8192));
                phaseB.Add(new List<double>(8192));
            }
            else
            {
                Debug.LogWarning("RENDER_STATS counter unavailable: " + string.Join("/", names));
            }
        }

        Camera[] cams = Camera.allCameras;
        camCount = cams.Length;
        probeCamera = Camera.main;
        if (probeCamera == null && cams.Length > 0)
            probeCamera = cams[0];
        Renderer[] rends = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
        rendererCount = rends.Length;
        for (int i = 0; i < rends.Length; i++)
            if (rends[i] is SpriteRenderer) spriteCount++;

        if (probeCamera != null)
        {
            probeRt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
            probeRt.Create();
        }

        Debug.Log(string.Format(CultureInfo.InvariantCulture,
            "RENDER_STATS start tag={0} counters={1} cameras={2} renderers={3} sprites={4}",
            tag, recorders.Count, camCount, rendererCount, spriteCount));
        watch.Restart();
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    private static void Tick()
    {
        if (!sampling || finished || !EditorApplication.isPlaying)
            return;
        double t = watch.Elapsed.TotalSeconds;
        bool phaseBTime = t >= splitSeconds;

        if (phaseBTime && probeCamera != null && probeRt != null)
        {
            probeCamera.targetTexture = probeRt;
            probeCamera.Render();
            probeCamera.targetTexture = null;
        }

        for (int i = 0; i < recorders.Count; i++)
        {
            double v = recorders[i].LastValue;
            if (phaseBTime) phaseB[i].Add(v);
            else phaseA[i].Add(v);
        }
        if (phaseBTime) framesB++; else framesA++;

        if (t >= totalSeconds)
        {
            finished = true;
            EditorApplication.update -= Tick;
            bool ok = WriteJson();
            if (!ok) Debug.LogError("RENDER_STATS_WRITE_FAIL " + OutPath());
            SessionState.SetString(DoneKey, ok ? OutPath() : "FAIL");
            SessionState.EraseString(RequestKey);
            EditorApplication.isPlaying = false; // -> reload -> EnteredEditMode -> Exit
        }
    }

    private static string OutPath()
    {
        string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        return Path.Combine(root, "Trials", "Runs", "render_stats_" + tag + ".json");
    }

    private static bool WriteJson()
    {
        try
        {
            string path = OutPath();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            StringBuilder sb = new StringBuilder(8192);
            sb.Append("{\n");
            sb.Append("  \"tag\": \"").Append(Escape(tag)).Append("\",\n");
            sb.Append("  \"scene\": \"World\",\n");
            sb.Append("  \"utc\": \"").Append(DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture)).Append("\",\n");
            sb.Append("  \"unityVersion\": \"").Append(Escape(Application.unityVersion)).Append("\",\n");
            sb.Append("  \"totalSeconds\": ").Append(F(totalSeconds)).Append(",\n");
            sb.Append("  \"splitSeconds\": ").Append(F(splitSeconds)).Append(",\n");
            sb.Append("  \"framesA\": ").Append(framesA).Append(", \"framesB\": ").Append(framesB).Append(",\n");
            sb.Append("  \"cameras\": ").Append(camCount).Append(", \"renderers\": ").Append(rendererCount)
              .Append(", \"sprites\": ").Append(spriteCount).Append(",\n");
            sb.Append("  \"counterSources\": {");
            for (int i = 0; i < recorderNames.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append('"').Append(Escape(recorderNames[i])).Append("\": \"").Append(Escape(recorderSources[i])).Append('"');
            }
            sb.Append("},\n");
            sb.Append("  \"phaseA\": {");
            AppendPhase(sb, phaseA);
            sb.Append("},\n  \"phaseB\": {");
            AppendPhase(sb, phaseB);
            sb.Append("}\n}\n");
            File.WriteAllText(path, sb.ToString());
            Debug.Log("RENDER_STATS_OK " + path);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError("RENDER_STATS_WRITE_FAIL " + e.Message);
            return false;
        }
    }

    private static void AppendPhase(StringBuilder sb, List<List<double>> phase)
    {
        for (int i = 0; i < recorders.Count; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append('"').Append(Escape(recorderNames[i])).Append("\": {");
            List<double> values = phase[i];
            if (values.Count == 0)
            {
                sb.Append("\"n\": 0}");
                continue;
            }
            List<double> sorted = new List<double>(values);
            sorted.Sort();
            double sum = 0;
            for (int k = 0; k < sorted.Count; k++) sum += sorted[k];
            double mean = sum / sorted.Count;
            double median = sorted[sorted.Count / 2];
            int p95i = (int)(0.95 * (sorted.Count - 1));
            sb.Append("\"n\": ").Append(sorted.Count);
            sb.Append(", \"mean\": ").Append(F(mean));
            sb.Append(", \"median\": ").Append(F(median));
            sb.Append(", \"p95\": ").Append(F(sorted[p95i]));
            sb.Append(", \"min\": ").Append(F(sorted[0]));
            sb.Append(", \"max\": ").Append(F(sorted[sorted.Count - 1]));
            sb.Append('}');
        }
    }

    private static string F(double v)
    {
        return v.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private static string Escape(string s)
    {
        return string.IsNullOrEmpty(s) ? "" : s.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
#endif
