// req-303: the take's own audit of the command line. Off unless -argsAudit 1.
// When on, one line per -name token of the merged line (LauncherArgs.Args(), so the Editor
// launcher line is covered): "name;value;status" with status READ, DUP (a later repeat - the
// kept value is printed) or UNREAD (no parser asked for it during the take), plus a summary row.
// Note()/Hit() are the only touch points; both cost one int read when the audit is off.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

public static class ArgsAudit
{
    private const string Switch = "-argsAudit";
    private static int state;   // 0 = not asked yet, 1 = on, 2 = off, 3 = resolving
    private static readonly HashSet<string> read = new HashSet<string>(StringComparer.Ordinal);
    private static readonly List<string> clamps = new List<string>();

    public static bool On
    {
        get
        {
            if (state == 0)
            {
                state = 3;   // guard: a nested On() during resolution reads "off"
                string[] a = LauncherArgs.Args();
                int i = Array.IndexOf(a, Switch);
                bool on = i >= 0 && i + 1 < a.Length && a[i + 1] != "0";
                if (on)
                    read.Add(Switch);
                state = on ? 1 : 2;
            }
            return state == 1;
        }
    }

    // A helper asked for this name: it is READ (one-shot lookups).
    public static string Note(string name)
    {
        if (On)
            read.Add(name);
        return name;
    }

    // Same, but hands the value back so a loop can note only on a match.
    public static string Note(string name, string value)
    {
        if (On)
            read.Add(name);
        return value;
    }

    // Token vs sought name: notes the name only when the token really is it.
    public static bool Hit(string token, string name)
    {
        if (!string.Equals(token, name, StringComparison.Ordinal))
            return false;
        if (On)
            read.Add(name);
        return true;
    }

    // Round 2: a helper that already clamps records given -> used, nothing else.
    public static float Clamp(string name, float given, float used)
    {
        if (On && given != used)
            clamps.Add(name + ";" + F(given) + ";" + F(used));
        return used;
    }

    private static string F(float v)
    {
        return v.ToString("0.#####", System.Globalization.CultureInfo.InvariantCulture);
    }

    // A token is a value, not a flag, when it does not start with '-' or when it is a negative
    // number ("-poseStepMin -0.335024" must not invent a "-0.335024" switch).
    private static bool IsValue(string t)
    {
        if (string.IsNullOrEmpty(t) || t[0] != '-')
            return true;
        double d;
        return double.TryParse(t, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out d);
    }

    // Take end: the audit file beside the take's CSV, only when -argsAudit 1 was given.
    public static void Write(string label, string folder)
    {
        if (!On)
            return;
        string[] a = LauncherArgs.Args();
        Dictionary<string, string> first = new Dictionary<string, string>(StringComparer.Ordinal);
        List<string> rows = new List<string>();
        int tokens = 0, nRead = 0, nDup = 0, nUnread = 0;
        for (int i = 0; i < a.Length; i++)
        {
            string t = a[i];
            if (IsValue(t))
                continue;
            tokens++;
            string v = i + 1 < a.Length && IsValue(a[i + 1]) ? a[i + 1] : "";
            if (first.ContainsKey(t))
            {
                // a repeat: first wins, so the kept (first) value is what is printed
                rows.Add(t + ";" + first[t] + ";DUP");
                nDup++;
                continue;
            }
            first[t] = v;
            bool wasRead = read.Contains(t);
            if (wasRead) nRead++; else nUnread++;
            rows.Add(t + ";" + v + ";" + (wasRead ? "READ" : "UNREAD"));
        }
        foreach (string c in clamps)
            rows.Add(c + ";CLAMP");
        rows.Add("summary;n=" + tokens + ";read=" + nRead + ";dup=" + nDup + ";unread=" + nUnread);
        try
        {
            string path = Path.Combine(folder, label + "_args.txt");
            File.WriteAllText(path, string.Join("\n", rows.ToArray()) + "\n",
                new UTF8Encoding(false));
            Debug.Log("ARGS_AUDIT " + path);
        }
        catch (Exception e)
        {
            Debug.LogWarning("ArgsAudit: could not write the audit file: " + e.Message);
        }
    }
}
