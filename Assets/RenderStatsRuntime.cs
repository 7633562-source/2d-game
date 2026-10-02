using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

// Runtime rendering-stats sampler for the optim-8 s5 draw-call pass.
// Inert unless the process was started with
//   -renderStatsTag <tag> [-renderStatsSeconds 24] [-renderStatsSplit 12] [-renderStatsOut <path>]
// (standalone player) or the page URL carried ?renderStats=<tag> (WebGL).
//
// Plays the scene in two phases and samples the Rendering Profiler counters:
//   phase A: plain loop
//   phase B: plain loop + explicit Camera.Render() into a RenderTexture each frame
//     (a headless player does not render its own window, so phase B is the measurable one;
//      in the browser both phases render and phase A is the plain demo)
// JSON (mean/median/p95/min/max of the counters + frame ms) goes to -renderStatsOut
// (default persistentDataPath/render_stats_<tag>.json). In WebGL it is mirrored into
// document.title and a hidden #renderstats element via RS_Report (.jslib plugin).
// Log lines: RENDER_STATS_RT start / _OK / _FAIL.
public class RenderStatsRuntime : MonoBehaviour
{
    private static readonly string[][] CounterCandidates =
    {
        new[] { "Standard Draw Calls Count" },
        new[] { "SRP Batcher Draw Calls Count" },
        new[] { "BRG Draw Calls Count" },
        new[] { "SetPass Calls Count", "SetPass Calls" },
        new[] { "Dynamic Batches Count" },
        new[] { "Static Batches Count" },
        new[] { "Instanced Batches Count" },
        new[] { "Triangles Count", "Triangles" },
        new[] { "Vertices Count", "Vertices" },
        new[] { "CPU Main Thread Frame Time" },
    };

    private string tag = "probe";
    private float totalSeconds = 24f;
    private float splitSeconds = 12f;
    private string outPath = "";
    private readonly System.Diagnostics.Stopwatch watch = new System.Diagnostics.Stopwatch();
    private readonly List<ProfilerRecorder> recorders = new List<ProfilerRecorder>();
    private readonly List<string> recorderNames = new List<string>();
    private readonly List<string> recorderSources = new List<string>();
    private readonly List<List<double>> phaseA = new List<List<double>>();
    private readonly List<List<double>> phaseB = new List<List<double>>();
    private readonly List<double> frameMsA = new List<double>(8192);
    private readonly List<double> frameMsB = new List<double>(8192);
    private readonly List<double> mainMsA = new List<double>(8192);
    private readonly List<double> mainMsB = new List<double>(8192);
    private ProfilerRecorder mainThread;
    private readonly List<string> availableRenderCounters = new List<string>();
    private int framesA, framesB;
    private int startRenderers, startSprites, endRenderers, endSprites;
    private int spriteMaterialCount = -1;
    private Camera probeCamera;
    private RenderTexture probeRt;
    private bool armed;
    private bool finished;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if (Application.isEditor)
            return; // editor runs are owned by Assets/Editor/RenderStatsProbe.cs
        string tag = ArgValue("-renderStatsTag");
#if UNITY_WEBGL && !UNITY_EDITOR
        if (string.IsNullOrEmpty(tag))
            tag = UrlParam(Application.absoluteURL, "renderStats");
#endif
        if (string.IsNullOrEmpty(tag))
            return;
        GameObject go = new GameObject("RenderStatsSampler");
        UnityEngine.Object.DontDestroyOnLoad(go);
        go.AddComponent<RenderStatsRuntime>().Begin(tag);
    }

    private static string ArgValue(string name)
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == name)
                return args[i + 1];
        return "";
    }

    private static string UrlParam(string url, string key)
    {
        int q = url.IndexOf('?');
        if (q < 0)
            return "";
        string[] parts = url.Substring(q + 1).Split('&');
        for (int i = 0; i < parts.Length; i++)
        {
            string p = parts[i];
            int eq = p.IndexOf('=');
            if (eq > 0 && p.Substring(0, eq) == key)
                return Uri.UnescapeDataString(p.Substring(eq + 1));
        }
        return "";
    }

    private void Begin(string requestTag)
    {
        tag = requestTag;
        float.TryParse(ArgValue("-renderStatsSeconds"), NumberStyles.Float, CultureInfo.InvariantCulture, out totalSeconds);
        if (totalSeconds <= 0f) totalSeconds = 24f;
        float.TryParse(ArgValue("-renderStatsSplit"), NumberStyles.Float, CultureInfo.InvariantCulture, out splitSeconds);
        if (splitSeconds <= 0f) splitSeconds = totalSeconds * 0.5f;
        outPath = ArgValue("-renderStatsOut");
        if (string.IsNullOrEmpty(outPath))
            outPath = Path.Combine(Application.persistentDataPath, "render_stats_" + tag + ".json");

        CollectAvailableCounters();

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
                Debug.LogWarning("RENDER_STATS_RT counter unavailable: " + string.Join("/", names));
            }
        }

        mainThread = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 1);
        if (!mainThread.Valid)
            Debug.LogWarning("RENDER_STATS_RT counter unavailable: Internal/Main Thread");

        Camera[] cams = Camera.allCameras;
        probeCamera = Camera.main;
        if (probeCamera == null && cams.Length > 0)
            probeCamera = cams[0];
        CollectSceneStats(ref startRenderers, ref startSprites, false);

        if (probeCamera != null)
        {
            probeRt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
            probeRt.Create();
        }

        Debug.Log(string.Format(CultureInfo.InvariantCulture,
            "RENDER_STATS_RT start tag={0} counters={1} cameras={2} renderers={3} out={4}",
            tag, recorders.Count, cams.Length, startRenderers, outPath));
        watch.Restart();
        armed = true;
    }

    private void CollectAvailableCounters()
    {
        try
        {
            List<ProfilerRecorderHandle> handles = new List<ProfilerRecorderHandle>(512);
            ProfilerRecorderHandle.GetAvailable(handles);
            for (int i = 0; i < handles.Count; i++)
            {
                ProfilerRecorderDescription d = ProfilerRecorderHandle.GetDescription(handles[i]);
                if (d.Category == ProfilerCategory.Render)
                    availableRenderCounters.Add(d.Name);
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("RENDER_STATS_RT counter dump failed: " + e.Message);
        }
    }

    private void CollectSceneStats(ref int renderers, ref int sprites, bool countSprites)
    {
        renderers = 0;
        sprites = 0;
        HashSet<Material> mats = countSprites ? new HashSet<Material>() : null;
        Renderer[] rends = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
        renderers = rends.Length;
        for (int i = 0; i < rends.Length; i++)
        {
            SpriteRenderer sr = rends[i] as SpriteRenderer;
            if (sr == null)
                continue;
            sprites++;
            if (mats != null)
                mats.Add(sr.sharedMaterial);
        }
        if (mats != null)
        {
            spriteMaterialCount = mats.Count;
            Debug.Log(string.Format(CultureInfo.InvariantCulture,
                "RENDER_STATS_RT scene renderers={0} sprites={1} distinctSpriteMaterials={2}",
                renderers, sprites, spriteMaterialCount));
        }
    }

    private void Update()
    {
        if (!armed || finished)
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
        double mainMs = mainThread.Valid ? mainThread.LastValue / 1e6 : 0.0;
        double frameMs = Time.unscaledDeltaTime * 1000.0;
        if (phaseBTime) { framesB++; frameMsB.Add(frameMs); mainMsB.Add(mainMs); }
        else { framesA++; frameMsA.Add(frameMs); mainMsA.Add(mainMs); }

        if (t >= totalSeconds)
        {
            finished = true;
            CollectSceneStats(ref endRenderers, ref endSprites, true);
            string json = BuildJson();
            bool ok = false;
            if (json != null)
            {
                ok = SinkOutput(json);
                Debug.Log(ok ? "RENDER_STATS_RT_OK " + outPath : "RENDER_STATS_RT_FAIL");
            }
#if UNITY_WEBGL && !UNITY_EDITOR
            // stay in the demo; the page carries the numbers now
#else
            Application.Quit(ok ? 0 : 5);
#endif
        }
    }

    private string BuildJson()
    {
        try
        {
            StringBuilder sb = new StringBuilder(16384);
            sb.Append("{\n");
            sb.Append("  \"tag\": \"").Append(Escape(tag)).Append("\",\n");
            sb.Append("  \"scene\": \"").Append(Escape(SceneManager.GetActiveScene().name)).Append("\",\n");
            sb.Append("  \"env\": \"").Append(Escape(Application.platform.ToString())).Append("\",\n");
            sb.Append("  \"utc\": \"").Append(DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture)).Append("\",\n");
            sb.Append("  \"unityVersion\": \"").Append(Escape(Application.unityVersion)).Append("\",\n");
            sb.Append("  \"totalSeconds\": ").Append(F(totalSeconds)).Append(",\n");
            sb.Append("  \"splitSeconds\": ").Append(F(splitSeconds)).Append(",\n");
            sb.Append("  \"framesA\": ").Append(framesA).Append(", \"framesB\": ").Append(framesB).Append(",\n");
            sb.Append("  \"renderersStart\": ").Append(startRenderers).Append(", \"spritesStart\": ").Append(startSprites).Append(",\n");
            sb.Append("  \"renderersEnd\": ").Append(endRenderers).Append(", \"spritesEnd\": ").Append(endSprites)
              .Append(", \"distinctSpriteMaterials\": ").Append(spriteMaterialCount).Append(",\n");
            sb.Append("  \"counterSources\": {");
            for (int i = 0; i < recorderNames.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append('"').Append(Escape(recorderNames[i])).Append("\": \"").Append(Escape(recorderSources[i])).Append('"');
            }
            sb.Append("},\n");
            sb.Append("  \"availableRenderCounters\": [");
            for (int i = 0; i < availableRenderCounters.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append('"').Append(Escape(availableRenderCounters[i])).Append('"');
            }
            sb.Append("],\n");
            sb.Append("  \"phaseA\": {");
            AppendPhase(sb, phaseA, frameMsA, mainMsA);
            sb.Append("},\n  \"phaseB\": {");
            AppendPhase(sb, phaseB, frameMsB, mainMsB);
            sb.Append("}\n}\n");
            return sb.ToString();
        }
        catch (Exception e)
        {
            Debug.LogError("RENDER_STATS_RT_BUILD_FAIL " + e.Message);
            return null;
        }
    }

    private void AppendPhase(StringBuilder sb, List<List<double>> phase, List<double> frameMs, List<double> mainMs)
    {
        for (int i = 0; i < recorders.Count; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append('"').Append(Escape(recorderNames[i])).Append("\": {");
            AppendStats(sb, phase[i]);
            sb.Append('}');
        }
        if (recorders.Count > 0) sb.Append(", ");
        sb.Append("\"FrameMs\": {");
        AppendStats(sb, frameMs);
        sb.Append("}, \"MainThreadMs\": {");
        AppendStats(sb, mainMs);
        sb.Append('}');
    }

    private static void AppendStats(StringBuilder sb, List<double> values)
    {
        if (values.Count == 0)
        {
            sb.Append("\"n\": 0");
            return;
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
    }

    private bool SinkOutput(string json)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        try
        {
            RS_Report(json);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError("RENDER_STATS_RT_SINK_FAIL " + e.Message);
            return false;
        }
#else
        try
        {
            string dir = Path.GetDirectoryName(outPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(outPath, json);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError("RENDER_STATS_RT_SINK_FAIL " + e.Message);
            return false;
        }
#endif
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void RS_Report(string json);
#endif

    private static string F(double v)
    {
        return v.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private static string Escape(string s)
    {
        return string.IsNullOrEmpty(s) ? "" : s.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
