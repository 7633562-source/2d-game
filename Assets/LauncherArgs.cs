// One reader for the command line: the real arguments untouched in the player; in the Editor, when
// no launcher switch is on the command line, the launcher's flag line (Assets/StreamingAssets/
// launcher-flags.txt, written by F:/hermes-data/scripts/scene-cmd-update.py) is appended, so Play
// runs the same human the game launcher runs. The player path never reads the file.
using System;
using System.IO;
using UnityEngine;

public static class LauncherArgs
{
    private const string FileName = "launcher-flags.txt";
    private static bool logged;

    // The launcher's flag line ("" when it is absent or unreadable). Editor only.
    public static string EditorLine()
    {
        if (!Application.isEditor)
            return "";
        try
        {
            string[] real = Environment.GetCommandLineArgs();
            int k = Array.IndexOf(real, "-launcherLine");
            string path = k >= 0 && k + 1 < real.Length ? real[k + 1]
                : Path.Combine(Application.streamingAssetsPath, FileName);
            return File.Exists(path) ? File.ReadAllText(path).Trim() : "";
        }
        catch (Exception)
        {
            return "";
        }
    }

    // True when Play in the Editor runs on the launcher's line (Args() appended it).
    public static bool EditorLineInForce()
    {
        return Application.isEditor && Args().Length > Environment.GetCommandLineArgs().Length;
    }

    // A float switch of the merged line; fallback when it is absent or unreadable.
    public static float Float(string name, float fallback)
    {
        string[] a = Args();
        int i = Array.IndexOf(a, name);
        float v;
        return i >= 0 && i + 1 < a.Length && float.TryParse(a[i + 1],
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out v) ? v : fallback;
    }

    public static string[] Args()
    {
        string[] real = Environment.GetCommandLineArgs();
        if (!Application.isEditor)
            return real;
        string line = EditorLine();
        if (line.Length == 0)
            return real;
        string[] extra = line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < extra.Length; i++)
            if (extra[i].Length > 0 && extra[i][0] == '-' && Array.IndexOf(real, extra[i]) >= 0)
                return real;   // the game's switches are already on the command line
        string[] merged = new string[real.Length + extra.Length];
        real.CopyTo(merged, 0);
        extra.CopyTo(merged, real.Length);
        if (!logged)
        {
            logged = true;
            Debug.Log("LAUNCHARGS: editor Play appends the launcher flag line (" +
                extra.Length + " tokens)");
        }
        return merged;
    }
}
