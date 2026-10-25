// req-102, Editor-side proofs (hand-runnable, no owner's Editor needed, no Play started by hand):
//   print run: Unity.exe -batchmode -nographics -projectPath <P> -executeMethod LauncherArgsProbe.Run
//              -logFile <log> -quit   -> LAUNCHARGS_REAL / _EXTRA / _RESOLVED (EXTRA must equal K_FLAGS)
//   take run : Unity.exe -batchmode -nographics -projectPath <P> -executeMethod LauncherArgsProbe.RunTake
//              -edTake <tag> -logFile <log> + the trial's own args (-trial -duration <s> -label <lab> ...)
//              -> enters Play mode and exits when Trials/Runs/<lab>.json appears (rc 0) or times out (rc 2).
// req-105, EdPlay observer: Unity.exe -batchmode -nographics -projectPath <P> -executeMethod
//   LauncherArgsProbe.RunPlay -edTake <tag> -edSeconds <s> [-edKey is|hold -edKeyFrom A -edKeyTo B]
//        [-edKey2From A2 -edKey2To B2] - a second window: held / W on inside either window
//   -logFile <log>  -> enters Play WITHOUT -trial; every 0.5 s of game time logs t, pelvis y, comX,
//   ankle gap (foot to foot), grounded L/R; catches LAUNCHARGS/REFWALK as they appear; exits at
//   -edSeconds. -edKey is = an Input System keyboard state event (W held); hold = drives
//   PlayerInputSource's own uiWalkHeld, the on-screen Walk button's field.
// req-117/req-122 ED rate: -edFps N sets Application.targetFrameRate = N with vSync 0 on every
//   Play tick; the take's end logs EDPLAY_RATE with the Play-loop frames (Time.frameCount delta
//   over 2.0 .. 6.0 s; ~240 at -edFps 60) and the physics ticks of the same window, plus one
//   EDPLAY_MANARGS line with the tokens the man runs on (LauncherArgs.Args()).
using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public static class LauncherArgsProbe
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string MarkerDir = "Temp";
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static void Run()
    {
        string[] real = Environment.GetCommandLineArgs();
        string[] resolved = LauncherArgs.Args();
        string extra = resolved.Length > real.Length
            ? string.Join(" ", resolved, real.Length, resolved.Length - real.Length) : "";
        Debug.Log("LAUNCHARGS_REAL " + string.Join(" ", real));
        Debug.Log("LAUNCHARGS_EXTRA " + extra);
        Debug.Log("LAUNCHARGS_RESOLVED " + string.Join(" ", resolved));
        EditorApplication.Exit(0);
    }

    public static void RunTake()
    {
        string tag = Arg("-edTake", "take");
        string label = Arg("-label", "edtake");
        double end = DateTime.UtcNow.AddSeconds(ArgFloat("-edTakeTimeout", 600f)).Ticks;
        File.WriteAllText(MarkerPath(tag), label + "\n" + ((long)end).ToString(Inv));
        EditorApplication.update += Tick;
        EditorSceneManager.OpenScene(ScenePath);
        EditorApplication.EnterPlaymode();
    }

    public static void RunPlay()
    {
        string tag = Arg("-edTake", "edplay");
        string marker = PlayMarkerPath(tag);
        string[] lines =
        {
            tag,
            ArgFloat("-edSeconds", 22f).ToString("R", Inv),
            Arg("-edKey", ""),
            ArgFloat("-edKeyFrom", -1f).ToString("R", Inv),
            ArgFloat("-edKeyTo", -1f).ToString("R", Inv),
            ((long)DateTime.UtcNow.AddSeconds(ArgFloat("-edTakeTimeout", 600f)).Ticks).ToString(Inv),
            ArgFloat("-edFps", 0f).ToString("R", Inv),
            ArgFloat("-edKey2From", -1f).ToString("R", Inv),
            ArgFloat("-edKey2To", -1f).ToString("R", Inv)
        };
        File.WriteAllLines(marker, lines);
        EnsureLogHook();
        EditorApplication.update += PlayTick;
        EditorSceneManager.OpenScene(ScenePath);
        EditorApplication.EnterPlaymode();
    }

    [InitializeOnLoadMethod]
    private static void Hook()
    {
        // EnterPlaymode and stopping Play both reload the domain; re-arm the tick from the marker.
        if (PendingMarker() != null)
            EditorApplication.update += Tick;
        if (PendingPlayMarker() != null)
        {
            EnsureLogHook();   // play-start logs (REFWALK) fire before the first tick: hook now
            EditorApplication.update += PlayTick;
        }
    }

    private static void Tick()
    {
        string marker = PendingMarker();
        if (marker == null)
        {
            EditorApplication.update -= Tick;
            return;
        }
        string[] lines = File.ReadAllLines(marker);
        string label = lines.Length > 0 ? lines[0].Trim() : "edtake";
        long endTicks = lines.Length > 1 ? long.Parse(lines[1].Trim(), Inv) : 0L;
        bool done = File.Exists(Path.Combine(ProjectRoot(), "Trials", "Runs", label + ".json"));
        bool timeout = endTicks > 0 && DateTime.UtcNow.Ticks > endTicks;
        if (done && EditorApplication.isPlaying)
        {
            EditorApplication.isPlaying = false;   // stop; the next tick sees it stopped
            return;
        }
        if ((done && !EditorApplication.isPlaying) || timeout)
        {
            File.Delete(marker);
            Debug.Log((done ? "EDTAKE_DONE " : "EDTAKE_TIMEOUT ") + label +
                " playing=" + EditorApplication.isPlaying);
            EditorApplication.Exit(done ? 0 : 2);
        }
    }

    // ─── req-105 EdPlay: the Play-mode observer ───

    private static float nextSample = 0.5f;
    private static bool manArgsLogged;
    private static int rateFrame0;
    private static int rateFrame1;
    private static bool rateFrame0Seen;
    private static bool rateFrame1Seen;
    private static bool rateFixed0Seen;
    private static bool rateFixed1Seen;
    private static float rateFixed0;
    private static float rateFixed1;
    private static bool logHooked;
    private static Keyboard kbDevice;
    private static bool kbReleased;
    private static FieldInfo holdField;
    private static FieldInfo kbHeldField;
    private static FieldInfo uiHeldField;

    private static void PlayTick()
    {
        string marker = PendingPlayMarker();
        if (marker == null)
        {
            EditorApplication.update -= PlayTick;
            return;
        }
        string[] d = File.ReadAllLines(marker);
        double end = At(d, 1, 22.0);
        string keyMode = d.Length > 2 ? d[2].Trim() : "";
        double keyFrom = At(d, 3, -1.0);
        double keyTo = At(d, 4, -1.0);
        long endTicks = (long)At(d, 5, 0.0);
        double fps = At(d, 6, 0.0);
        double key2From = At(d, 7, -1.0);
        double key2To = At(d, 8, -1.0);
        bool stopped = Array.IndexOf(d, "stopped") >= 0;
        if (endTicks > 0 && DateTime.UtcNow.Ticks > endTicks && !stopped)
        {
            AppendMarker(marker, "timeout");
            EditorApplication.update -= PlayTick;
            File.Delete(marker);
            Debug.Log("EDPLAY_TIMEOUT " + (d.Length > 0 ? d[0] : "edplay"));
            EditorApplication.Exit(2);
            return;
        }
        if (!EditorApplication.isPlaying)
        {
            if (stopped)
                FinalizePlay(marker, d);
            return;
        }
        EnsureLogHook();
        float t = Time.time;
        if (fps > 0.0)
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = (int)fps;
        }
        if (!manArgsLogged)
        {
            manArgsLogged = true;
            Debug.Log("EDPLAY_MANARGS " + string.Join(" ", LauncherArgs.Args()));
        }
        if (!rateFrame0Seen && t >= 2f) { rateFrame0Seen = true; rateFrame0 = Time.frameCount; }
        if (!rateFrame1Seen && t >= 6f) { rateFrame1Seen = true; rateFrame1 = Time.frameCount; }
        if (!rateFixed0Seen && t >= 2f) { rateFixed0Seen = true; rateFixed0 = Time.fixedTime; }
        if (!rateFixed1Seen && t >= 6f) { rateFixed1Seen = true; rateFixed1 = Time.fixedTime; }
        if (end > 0.0 && t >= end)
        {
            int frames = rateFrame0Seen && rateFrame1Seen ? rateFrame1 - rateFrame0 : -1;
            int fixedTicks = rateFixed0Seen && rateFixed1Seen
                ? Mathf.RoundToInt((rateFixed1 - rateFixed0) / Time.fixedDeltaTime) : -1;
            Debug.Log("EDPLAY_RATE frames=" + frames + " fixed=" + fixedTicks);
            AppendMarker(marker, "stopped");
            EditorApplication.isPlaying = false;   // stop; the next tick finalizes
            return;
        }
        while (t >= nextSample)
        {
            Sample(nextSample);
            nextSample += 0.5f;
        }
        KeyTick(t, keyMode, keyFrom, keyTo, key2From, key2To);
    }

    private static void Sample(float t)
    {
        GameObject h = FindPlayedHuman();
        if (h == null)
        {
            Debug.Log("EDPLAY SAMPLE t=" + F(t) + " NOHUMAN");
            return;
        }
        float py = BodyY(h, "Pelvis");
        CenterOfMassCalculator com = h.GetComponent<CenterOfMassCalculator>();
        string cx = com != null ? F4(com.GetCenterOfMass().x) : "NaN";
        BodyStateEstimator bs = h.GetComponent<BodyStateEstimator>();
        int gL = bs != null && bs.leftFootGrounded ? 1 : 0;
        int gR = bs != null && bs.rightFootGrounded ? 1 : 0;
        // dAnk = the trial's ankle gap: (supportMaxX - supportMinX) - 0.26 (lab_runner.py:745)
        string dAnk = bs != null ? F4(bs.supportMaxX - bs.supportMinX - 0.26f) : "NaN";
        PlayerInputSource src = h.GetComponent<PlayerInputSource>();
        int kW = 0, uW = 0;
        if (src != null)
        {
            if (kbHeldField == null)
                kbHeldField = typeof(PlayerInputSource).GetField("keyboardWalkHeld",
                    BindingFlags.Instance | BindingFlags.NonPublic);
            if (uiHeldField == null)
                uiHeldField = typeof(PlayerInputSource).GetField("uiWalkHeld",
                    BindingFlags.Instance | BindingFlags.NonPublic);
            kW = kbHeldField != null && (bool)kbHeldField.GetValue(src) ? 1 : 0;
            uW = uiHeldField != null && (bool)uiHeldField.GetValue(src) ? 1 : 0;
        }
        Debug.Log("EDPLAY SAMPLE t=" + F(t) + " py=" + F4(py) + " cx=" + cx +
            " dAnk=" + dAnk + " gap=" + F4(FootGap(h)) + " gL=" + gL + " gR=" + gR +
            " kW=" + kW + " uW=" + uW);
    }

    private static void KeyTick(float t, string mode, double from, double to, double from2, double to2)
    {
        if (mode.Length == 0)
            return;
        bool in1 = from >= 0.0 && to > from;
        bool in2 = from2 >= 0.0 && to2 > from2;
        if (!in1 && !in2)
            return;
        bool want = (in1 && t >= from && t < to) || (in2 && t >= from2 && t < to2);
        if (mode == "is")
        {
            if (kbDevice == null || !kbDevice.added)
                kbDevice = Keyboard.current != null ? Keyboard.current : InputSystem.AddDevice<Keyboard>();
            if (want)
                InputSystem.QueueStateEvent(kbDevice, new KeyboardState(Key.W));
            else if (!kbReleased)
            {
                kbReleased = true;
                InputSystem.QueueStateEvent(kbDevice, new KeyboardState());
            }
            return;
        }
        if (mode == "hold")
        {
            GameObject h = FindPlayedHuman();
            if (h == null)
                return;
            PlayerInputSource src = h.GetComponent<PlayerInputSource>();
            if (src == null)
                return;
            if (holdField == null)
                holdField = typeof(PlayerInputSource).GetField("uiWalkHeld",
                    BindingFlags.Instance | BindingFlags.NonPublic);
            if (holdField != null)
                holdField.SetValue(src, want);
        }
    }

    private static void FinalizePlay(string marker, string[] d)
    {
        string tag = d.Length > 0 ? d[0].Trim() : "edplay";
        string seen = "none";
        for (int i = 0; i < d.Length; i++)
            if (d[i].StartsWith("seen "))
            {
                string s = d[i].Substring(5).Trim();
                seen = seen == "none" ? s : seen + " | " + s;
            }
        // The hook can miss very early logs; scan the log file itself as the authority.
        string log = Arg("-logFile", "");
        try
        {
            if (log.Length > 0 && File.Exists(log))
            {
                int la = 0, rw = 0;
                string rwLine = "";
                using (FileStream fs = new FileStream(log, FileMode.Open, FileAccess.Read,
                           FileShare.ReadWrite))
                using (StreamReader sr = new StreamReader(fs))
                {
                    string line;
                    while ((line = sr.ReadLine()) != null)
                    {
                        if (line.StartsWith("LAUNCHARGS:"))
                            la++;
                        else if (line.StartsWith("REFWALK:"))
                        {
                            rw++;
                            if (rwLine.Length == 0)
                                rwLine = line;
                        }
                    }
                }
                seen = seen + " | log: LAUNCHARGS lc=" + la + ", REFWALK lc=" + rw +
                    (rwLine.Length > 0 ? " [" + rwLine + "]" : "");
            }
        }
        catch (Exception e)
        {
            seen = seen + " | log-scan failed: " + e.GetType().Name;
        }
        File.Delete(marker);
        Debug.Log("EDPLAY_END tag=" + tag + " seen=[" + seen + "]");
        EditorApplication.Exit(0);
    }

    private static void EnsureLogHook()
    {
        if (logHooked)
            return;
        Application.logMessageReceived += OnLog;
        logHooked = true;
    }

    private static void OnLog(string msg, string stack, LogType type)
    {
        string marker = PendingPlayMarker();
        if (marker == null || msg.StartsWith("EDPLAY"))
            return;
        if (msg.StartsWith("LAUNCHARGS:"))
            AppendMarker(marker, "seen LAUNCHARGS: " + msg);
        else if (msg.StartsWith("REFWALK:"))
            AppendMarker(marker, "seen " + msg);
    }

    private static GameObject FindPlayedHuman()
    {
        Human[] humans = UnityEngine.Object.FindObjectsByType<Human>(FindObjectsSortMode.None);
        for (int i = 0; i < humans.Length; i++)
            if (humans[i].gameObject.GetComponent<PlayerInputSource>() != null)
                return humans[i].gameObject;
        return humans.Length > 0 ? humans[0].gameObject : null;
    }

    private static float BodyY(GameObject h, string name)
    {
        Transform t = h.transform.Find(name);
        if (t == null)
            return float.NaN;
        Rigidbody2D rb = t.GetComponent<Rigidbody2D>();
        return rb != null ? rb.position.y : t.position.y;
    }

    private static float FootGap(GameObject h)
    {
        Transform l = h.transform.Find("LeftLegFoot");
        Transform r = h.transform.Find("RightLegFoot");
        if (l == null || r == null)
            return float.NaN;
        Rigidbody2D lr = l.GetComponent<Rigidbody2D>();
        Rigidbody2D rr = r.GetComponent<Rigidbody2D>();
        Vector2 a = lr != null ? lr.position : (Vector2)l.position;
        Vector2 b = rr != null ? rr.position : (Vector2)r.position;
        return Vector2.Distance(a, b);
    }

    private static string F(float v)
    {
        return v.ToString("F3", Inv);
    }

    private static string F4(float v)
    {
        return v.ToString("F4", Inv);
    }

    private static double At(string[] d, int i, double fallback)
    {
        double v;
        return i < d.Length && double.TryParse(d[i].Trim(), NumberStyles.Float, Inv, out v)
            ? v : fallback;
    }

    private static void AppendMarker(string marker, string line)
    {
        try { File.AppendAllText(marker, line + "\n"); } catch (IOException) { }
    }

    private static string MarkerPath(string tag)
    {
        return Path.Combine(ProjectRoot(), MarkerDir, "edtake-" + tag + ".txt");
    }

    private static string PlayMarkerPath(string tag)
    {
        return Path.Combine(ProjectRoot(), MarkerDir, "edplay-" + tag + ".txt");
    }

    private static string PendingMarker()
    {
        try
        {
            string[] found = Directory.GetFiles(Path.Combine(ProjectRoot(), MarkerDir), "edtake-*.txt");
            return found.Length > 0 ? found[0] : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static string PendingPlayMarker()
    {
        try
        {
            string[] found = Directory.GetFiles(Path.Combine(ProjectRoot(), MarkerDir), "edplay-*.txt");
            return found.Length > 0 ? found[0] : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static string ProjectRoot()
    {
        return Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
    }

    private static string Arg(string name, string fallback)
    {
        string[] args = Environment.GetCommandLineArgs();
        int i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback;
    }

    private static float ArgFloat(string name, float fallback)
    {
        float v;
        return float.TryParse(Arg(name, ""), NumberStyles.Float, Inv, out v)
            ? v : fallback;
    }
}
