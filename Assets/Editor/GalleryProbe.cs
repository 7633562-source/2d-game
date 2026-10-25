using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Diagnostic for the ART-1 gallery: build the catalogue in edit mode (no play
// mode) and list the biggest drawn sprites, so a wrong sprite/size can be named
// instead of guessed. Editor only; touches nothing on disk.
public static class GalleryProbe
{
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Gallery.unity", OpenSceneMode.Single);
        GameObject holder = new GameObject("GalleryProbe");
        GalleryScene gs = holder.AddComponent<GalleryScene>();
        MethodInfo build = typeof(GalleryScene).GetMethod(
            "Build", BindingFlags.NonPublic | BindingFlags.Instance);
        build.Invoke(gs, null);

        // Snapshot the untouched geometry first: the dress pass must not move or
        // resize a single part.
        SpriteRenderer[] before = Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None);
        System.Collections.Generic.Dictionary<SpriteRenderer, Vector2> sized =
            new System.Collections.Generic.Dictionary<SpriteRenderer, Vector2>();
        foreach (SpriteRenderer sr in before)
            sized[sr] = new Vector2(sr.bounds.size.x, sr.bounds.size.y);

        gs.SetRealArt(true);

        SpriteRenderer[] all = Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None);
        System.Array.Sort(all, delegate (SpriteRenderer a, SpriteRenderer b)
        {
            return Area(b).CompareTo(Area(a));
        });
        Debug.Log("GALLERY-PROBE renderers=" + all.Length
            + " content=" + gs.ContentBounds.size.x.ToString("0.0") + "x"
            + gs.ContentBounds.size.y.ToString("0.0"));

        // Did the dress move anything? Compare the world box of every renderer
        // that is still alive.
        int moved = 0;
        float worst = 0f;
        string worstName = string.Empty;
        foreach (System.Collections.Generic.KeyValuePair<SpriteRenderer, Vector2> pair in sized)
        {
            SpriteRenderer sr = pair.Key;
            if (sr == null)
                continue;
            float dx = Mathf.Abs(sr.bounds.size.x - pair.Value.x);
            float dy = Mathf.Abs(sr.bounds.size.y - pair.Value.y);
            float d = Mathf.Max(dx, dy);
            if (d > 0.001f)
            {
                moved++;
                if (d > worst)
                {
                    worst = d;
                    worstName = Path(sr.transform);
                }
            }
        }
        Debug.Log("GALLERY-PROBE geometry moved=" + moved + "/" + sized.Count
            + " worst=" + worst.ToString("0.0000") + " m at " + worstName);

        // Show the movers, so the reason is in the log instead of guessed:
        // mode, before box, after box, tile period, transform scale.
        int listed = 0;
        foreach (System.Collections.Generic.KeyValuePair<SpriteRenderer, Vector2> pair in sized)
        {
            SpriteRenderer sr = pair.Key;
            if (sr == null || listed >= 6)
                continue;
            float d = Mathf.Max(Mathf.Abs(sr.bounds.size.x - pair.Value.x),
                Mathf.Abs(sr.bounds.size.y - pair.Value.y));
            if (d <= 0.001f)
                continue;
            listed++;
            Vector3 sc = sr.transform.lossyScale;
            Debug.Log(string.Format(
                "GALLERY-PROBE mover {0} before={1}x{2} after={3}x{4} mode={5} size={6}x{7} scale=({8},{9}) sprite={10}",
                Path(sr.transform), pair.Value.x.ToString("0.000"), pair.Value.y.ToString("0.000"),
                sr.bounds.size.x.ToString("0.000"), sr.bounds.size.y.ToString("0.000"),
                sr.drawMode, sr.size.x.ToString("0.000"), sr.size.y.ToString("0.000"),
                sc.x.ToString("0.0000"), sc.y.ToString("0.0000"),
                sr.sprite != null ? sr.sprite.name : "<null>"));
        }

        Debug.Log("GALLERY-PROBE skips giant=" + ArtParts.GiantSkips + " empty=" + ArtParts.EmptySkips);

        int shown = Mathf.Min(14, all.Length);
        for (int i = 0; i < shown; i++)
        {
            SpriteRenderer sr = all[i];
            Bounds b = sr.bounds;
            Debug.Log(string.Format(
                "GALLERY-PROBE big[{0}] {1} size={2}x{3} mode={4} order={5} sprite={6} centre=({7},{8})",
                i, Path(sr.transform), b.size.x.ToString("0.00"), b.size.y.ToString("0.00"),
                sr.drawMode, sr.sortingOrder, sr.sprite != null ? sr.sprite.name : "<null>",
                b.center.x.ToString("0.0"), b.center.y.ToString("0.0")));
        }

        foreach (System.Collections.Generic.KeyValuePair<string, Bounds> row in gs.RowBounds)
        {
            Debug.Log(string.Format("GALLERY-PROBE row {0} centre=({1},{2}) size={3}x{4}",
                row.Key, row.Value.center.x.ToString("0.0"), row.Value.center.y.ToString("0.0"),
                row.Value.size.x.ToString("0.0"), row.Value.size.y.ToString("0.0")));
        }

        EditorApplication.Exit(0);
    }

    private static float Area(SpriteRenderer sr)
    {
        Bounds b = sr.bounds;
        return b.size.x * b.size.y;
    }

    private static string Path(Transform t)
    {
        string p = t.name;
        while (t.parent != null)
        {
            t = t.parent;
            p = t.name + "/" + p;
        }
        return p;
    }
}
