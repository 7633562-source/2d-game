using UnityEngine;
using UnityEngine.SceneManagement;
using System.Runtime.InteropServices;

// WebGL scene selector: reads ?scene=<name> from the URL and loads that scene.
// Each scene in the project is a separate work direction, so the Grok Bot tester
// can exercise each one through the browser: ?scene=Bird, ?scene=Dog, ?scene=Campfire, ?scene=Level1, etc.
// No param -> stays on the default first scene (World).
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
            case "World": case "Level1": case "Bird": case "BirdFlock50":
            case "Chicken": case "Dog": case "Tree": case "Campfire":
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
