using System.IO;
using UnityEngine;

// Frame capture for a scene: render a camera into a RenderTexture and write the
// bytes, plus the numeric proof that the content really sits inside that frame.
// This is the recipe ArtImportCheck and HeadlessTrial already use (a game
// window screenshot does NOT work in batchmode). No physics is touched.
public static class PhotoCapture
{
    public static Texture2D Render(Camera cam, int width, int height)
    {
        if (cam == null || width < 8 || height < 8)
            return null;

        RenderTexture rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
        rt.Create();
        RenderTexture prevTarget = cam.targetTexture;
        RenderTexture prevActive = RenderTexture.active;
        cam.targetTexture = rt;
        cam.Render();

        RenderTexture.active = rt;
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
        tex.Apply(false, false);

        cam.targetTexture = prevTarget;
        RenderTexture.active = prevActive;
        rt.Release();
        Object.Destroy(rt);
        return tex;
    }

    public static bool WritePng(Camera cam, int width, int height, string path, out string error)
    {
        error = null;
        Texture2D tex = Render(cam, width, height);
        if (tex == null)
        {
            error = "render failed (no camera or bad size)";
            return false;
        }

        string folder = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(folder) && !Directory.Exists(folder))
            Directory.CreateDirectory(folder);

        byte[] png = tex.EncodeToPNG();
        Object.Destroy(tex);
        if (png == null || png.Length == 0)
        {
            error = "EncodeToPNG produced nothing";
            return false;
        }

        File.WriteAllBytes(path, png);
        return true;
    }

    // Ortho viewport test: where the world box's corners land in the frame.
    // fits=true means every corner is inside [0,1]x[0,1], i.e. the whole object
    // is in the picture. This is the acceptance rule, not an eyeball.
    public static bool FitViewport(Camera cam, Bounds world, out Vector2 min, out Vector2 max, out float outsideMeters)
    {
        min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        outsideMeters = 0f;
        if (cam == null)
            return false;

        Vector3 c = world.center;
        Vector3 e = world.extents;
        for (int xi = -1; xi <= 1; xi += 2)
        {
            for (int yi = -1; yi <= 1; yi += 2)
            {
                Vector3 p = new Vector3(c.x + xi * e.x, c.y + yi * e.y, 0f);
                Vector3 v = cam.WorldToViewportPoint(p);
                min = new Vector2(Mathf.Min(min.x, v.x), Mathf.Min(min.y, v.y));
                max = new Vector2(Mathf.Max(max.x, v.x), Mathf.Max(max.y, v.y));
                outsideMeters += Mathf.Max(0f, -v.x) + Mathf.Max(0f, v.x - 1f)
                    + Mathf.Max(0f, -v.y) + Mathf.Max(0f, v.y - 1f);
            }
        }

        bool fits = min.x >= 0f && min.y >= 0f && max.x <= 1f && max.y <= 1f;
        // Convert the viewport overshoot into world metres for the report.
        float worldPerViewportY = cam.orthographicSize * 2f;
        outsideMeters *= worldPerViewportY;
        return fits;
    }

    // World bounds of everything drawn under root, from live sprite bounds -
    // what the camera actually paints, not a collider guess.
    public static bool TryRendererBounds(Transform root, out Bounds bounds)
    {
        bounds = new Bounds(Vector3.zero, Vector3.zero);
        if (root == null)
            return false;

        SpriteRenderer[] renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
        bool any = false;
        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer sr = renderers[i];
            if (sr == null || sr.sprite == null || !sr.enabled)
                continue;
            Bounds b = sr.bounds;
            if (b.size.x <= 0f && b.size.y <= 0f)
                continue;
            if (!any)
            {
                bounds = b;
                any = true;
            }
            else
                bounds.Encapsulate(b);
        }
        return any;
    }

    // Fraction of pixels that differ from the frame's corner colour: 0 means the
    // frame rendered an empty background, i.e. the capture is not evidence.
    public static float PaintedFraction(Texture2D tex, float threshold = 0.05f)
    {
        if (tex == null || tex.width < 4 || tex.height < 4)
            return 0f;

        Color bg = tex.GetPixel(1, 1);
        int painted = 0;
        int total = 0;
        for (int y = 2; y < tex.height - 2; y += 3)
        {
            for (int x = 2; x < tex.width - 2; x += 3)
            {
                Color c = tex.GetPixel(x, y);
                float d = Mathf.Abs(c.r - bg.r) + Mathf.Abs(c.g - bg.g) + Mathf.Abs(c.b - bg.b);
                if (d > threshold)
                    painted++;
                total++;
            }
        }
        return total > 0 ? painted / (float)total : 0f;
    }
}
