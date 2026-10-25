using System;
using UnityEngine;
using UnityEngine.SceneManagement;

// Preview player scene switch. The ART-1 catalogue ships two scenes in one
// small preview player (Gallery, TreeApple), so -scene <name> picks the one
// that boots. The trial player never passes -scene, and its single-scene
// contract (SampleScene) is untouched.
public static class SceneLaunch
{
    public static string Requested
    {
        get
        {
            if (!_read)
            {
                _read = true;
                _requested = Read("-scene");
            }
            return _requested;
        }
    }

    private static bool _read;
    private static string _requested = string.Empty;

    // True for the scene this run was asked for - a component in the other
    // catalogue scene must stay silent.
    public static bool ActivatesThisScene()
    {
        return string.IsNullOrEmpty(Requested)
            || SceneManager.GetActiveScene().name == Requested;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if (string.IsNullOrEmpty(Requested))
            return;
        if (SceneManager.GetActiveScene().name == Requested)
            return;
        Debug.Log("SCENE-LAUNCH loading " + Requested);
        SceneManager.LoadScene(Requested, LoadSceneMode.Single);
    }

    private static string Read(string key)
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], key, StringComparison.OrdinalIgnoreCase))
                return ArgsAudit.Note(key, args[i + 1]);
        }
        return string.Empty;
    }
}
