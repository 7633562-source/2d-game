using UnityEngine;
using UnityEngine.SceneManagement;
using System.Runtime.InteropServices;

// WebGL scene selector: reads ?scene=<name> from the URL and loads that scene.
// Each scene kept in the project is a separate work direction the browser tester
// can exercise: ?scene=Level1, ?scene=Campfire, ?scene=World, etc.
// No param -> stays on the default first scene (World).
// 27.09: the old demo scenes (Bird, BirdFlock50, Chicken, Dog, Tree) were removed
// from the project by the owner's order and dropped from the known list below;
// an unknown ?scene= value now just stays where it is (graceful no-op).
public static class SceneSelector
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern string GetUrlSearch();
#endif

    private static string _target;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AfterSceneLoad()
    {
        try
        {
            if (string.IsNullOrEmpty(_target))
            {
                _target = GetSceneFromUrl();
            }
            if (!string.IsNullOrEmpty(_target) &&
                SceneManager.GetActiveScene().name != _target &&
                IsKnownScene(_target))
            {
                SceneManager.LoadScene(_target);
            }
        }
        catch (System.Exception e)
        {
            // Missing UrlQuery.jslib used to throw here and skip every
            // MonoBehaviour.Start — empty WebGL World, no hero, no Walk.
            Debug.LogWarning("SceneSelector: URL scene probe skipped (" + e.GetType().Name + ").");
        }
    }

    private static bool IsKnownScene(string name)
    {
        switch (name)
        {
            case "World": case "Level1": case "Campfire":
                return true;
            default:
                return false;
        }
    }

    private static string GetSceneFromUrl()
    {
        string query = null;
#if UNITY_WEBGL && !UNITY_EDITOR
        query = GetUrlSearch();          // window.location.search — reliable in WebGL
#else
        query = System.Uri.UnescapeDataString(Application.absoluteURL ?? string.Empty);
        int q = query.IndexOf('?');
        if (q >= 0) query = query.Substring(q);
#endif
        if (string.IsNullOrEmpty(query)) return null;
        int idx = query.IndexOf("scene=", System.StringComparison.Ordinal);
        if (idx < 0) return null;
        string val = query.Substring(idx + "scene=".Length);
        int amp = val.IndexOf('&');
        if (amp >= 0) val = val.Substring(0, amp);
        val = System.Uri.UnescapeDataString(val);
        return string.IsNullOrEmpty(val) ? null : val;
    }
}
