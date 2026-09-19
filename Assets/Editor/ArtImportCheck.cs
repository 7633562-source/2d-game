using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// ART-0 import check: prints what the picture loaders will actually find, so "the files are
// there" is proven by Unity rather than by a folder listing.
//
//   LevelArt.PathsFor   -> Resources.Load<Sprite>("Level/ground_fill" | "Level/ground_cap"
//                          | "Level/sky" | "Level/sign_finish")
//   LevelArt.LoadAt     -> the same paths through Resources.LoadAll<Sprite> as a fallback
//   ArtLibrary.Albedo   -> Resources.Load<Texture2D>("Art/" + key), key = skin, fabric, denim,
//                          dirt, grass, bark, leaf; needs isReadable for ArtLibrary.Sample
//
// It also renders the grass cap the way StaticPlatform draws it (Tiled draw mode, Continuous,
// 0.14 m tall) and measures whether the texture really repeats. The import stores wrapU/wrapV = 1,
// but a sprite texture reports wrapMode = Clamp at runtime, so "does Tiled tile?" has to be
// answered with pixels, not with the meta file.
//
// Run (bash, never PowerShell, and not while the editor is open):
//   "F:/6000.5.8f1/Editor/Unity.exe" -batchmode \
//     -projectPath "E:/Games/2D Game/My project" \
//     -executeMethod ArtImportCheck.VerifyArt -logFile <logfile> -quit
// Exits 1 when a loader cannot find something, so this can gate a build.
public static class ArtImportCheck
{
    private static int failures;

    public static void VerifyArt()
    {
        failures = 0;
        Debug.Log("ART-IMPORT-CHECK begin");

        string[] levelKeys = { LevelArt.FillKey, LevelArt.CapKey, LevelArt.SkyKey, LevelArt.SignKey };
        for (int i = 0; i < levelKeys.Length; i++)
            CheckLevel(levelKeys[i]);

        // The level's own API, the way the scenes call it.
        Report("LevelArt.HasFill()", LevelArt.HasFill());
        Report("LevelArt.HasCap()", LevelArt.HasCap());
        Report("LevelArt.HasSign()", LevelArt.HasSign());
        Report("LevelArt.Sky()!=null", LevelArt.Sky() != null);
        Report("LevelArt.Fill()!=null", LevelArt.Fill() != null);
        Report("LevelArt.Cap()!=null", LevelArt.Cap() != null);
        Report("LevelArt.Sign()!=null", LevelArt.Sign() != null);

        string[] materialKeys = { ArtLibrary.Skin, ArtLibrary.Fabric, ArtLibrary.Denim,
                                  ArtLibrary.Dirt, ArtLibrary.Grass, ArtLibrary.Bark, ArtLibrary.Leaf };
        for (int i = 0; i < materialKeys.Length; i++)
            CheckMaterial(materialKeys[i]);

        // Status of the code path, printed either way: wired since ART-1a (a missing key must
        // return null - that is what the labelled MISSING ART tile keys off).
        Debug.Log("ART-IMPORT-CHECK note ArtLibrary.Albedo(dirt) = "
            + (ArtLibrary.Albedo(ArtLibrary.Dirt) == null ? "null (no file)" : "texture"));
        Debug.Log("ART-IMPORT-CHECK note ArtLibrary.Tiled(dirt) = "
            + (ArtLibrary.Tiled(ArtLibrary.Dirt) == null ? "null (no file)" : "sprite"));

        CheckTiling(LevelArt.CapKey, 8f, 0.14f);        // the strip StaticPlatform draws today
        CheckTiling(LevelArt.FillKey, 32f, 2f);         // the widest slab ("Behind"), 4 tiles of 8 m

        Debug.Log("ART-IMPORT-CHECK end failures=" + failures);
        EditorApplication.Exit(failures == 0 ? 0 : 1);
    }

    private static void CheckLevel(string key)
    {
        string path = "Level/" + key;
        Sprite single = Resources.Load<Sprite>(path);
        Sprite[] many = Resources.LoadAll<Sprite>(path);
        Sprite found = single != null ? single : (many != null && many.Length > 0 ? many[0] : null);
        if (found == null)
        {
            Debug.LogError("ART-IMPORT-CHECK FAIL " + path + " no Sprite (Load and LoadAll both null)");
            failures++;
            return;
        }

        string wrap = "-";
        string format = "-";
        string readable = "-";
        if (found.texture != null)
        {
            wrap = found.texture.wrapMode.ToString();
            format = found.texture.format.ToString();
            readable = found.texture.isReadable.ToString();
        }

        Debug.Log("ART-IMPORT-CHECK OK " + path
            + " via " + (single != null ? "Load<Sprite>" : "LoadAll<Sprite>[0]")
            + " sprite=" + found.name
            + " rect=" + (int)found.rect.width + "x" + (int)found.rect.height
            + " ppu=" + found.pixelsPerUnit
            + " native=" + found.bounds.size.x.ToString("0.###") + "x" + found.bounds.size.y.ToString("0.###") + "m"
            + " wrap=" + wrap + " format=" + format + " readable=" + readable);
    }

    private static void CheckMaterial(string key)
    {
        Texture2D tex = Resources.Load<Texture2D>("Art/" + key);
        if (tex == null)
        {
            Debug.LogError("ART-IMPORT-CHECK FAIL Art/" + key + " no Texture2D");
            failures++;
            return;
        }

        Debug.Log("ART-IMPORT-CHECK OK Art/" + key
            + " tex=" + tex.name + " " + tex.width + "x" + tex.height
            + " readable=" + tex.isReadable + " format=" + tex.format
            + (tex.isReadable ? "" : "  <-- ArtLibrary.Sample/TileMap need isReadable"));
    }

    // Renders a box of the given size with the sprite in Tiled draw mode and asks the pixels
    // whether the texture repeats at its native period.
    private static void CheckTiling(string key, float boxWidth, float boxHeight)
    {
        Sprite sprite = Resources.Load<Sprite>("Level/" + key);
        if (sprite == null)
        {
            Debug.LogError("ART-IMPORT-CHECK FAIL tiling " + key + " no sprite");
            failures++;
            return;
        }

        if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
        {
            Debug.Log("ART-IMPORT-CHECK note tiling " + key + " skipped: no graphics device (-nographics)");
            return;
        }

        GameObject go = new GameObject("ArtImportCheckTile");
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.drawMode = SpriteDrawMode.Tiled;
        sr.tileMode = SpriteTileMode.Continuous;
        sr.size = new Vector2(boxWidth, boxHeight);
        sr.color = Color.white;

        GameObject camGo = new GameObject("ArtImportCheckTileCam");
        Camera cam = camGo.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = boxHeight * 0.5f;
        cam.transform.position = new Vector3(0f, 0f, -10f);
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0f, 0f, 0f, 0f);

        int w = 1024;
        int h = Mathf.Clamp(Mathf.RoundToInt(w * boxHeight / boxWidth), 8, 1024);
        RenderTexture rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
        rt.Create();
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        Texture2D read = new Texture2D(w, h, TextureFormat.RGBA32, false);
        read.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        read.Apply();
        RenderTexture.active = null;
        cam.targetTexture = null;

        // One tile is the sprite's native width, in render pixels.
        float native = sprite.bounds.size.x;
        int period = Mathf.RoundToInt(native / boxWidth * w);
        if (period >= w - 4)
        {
            Debug.Log("ART-IMPORT-CHECK note tiling " + key
                + " box " + boxWidth + "m is not wider than one native tile " + native.ToString("0.###")
                + "m: nothing to repeat");
            Object.DestroyImmediate(read);
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(camGo);
            rt.Release();
            Object.DestroyImmediate(rt);
            return;
        }

        int row = -1;
        float bestAlpha = 0f;
        for (int y = 0; y < h; y++)
        {
            float a = read.GetPixel(w / 2, y).a;
            if (a > bestAlpha)
            {
                bestAlpha = a;
                row = y;
            }
        }

        float near = RowStd(read, row, 0, Mathf.Min(period, w));
        float far = RowStd(read, row, Mathf.Min(period * 2, w - 1), w);
        float repeat = 0f;
        int samples = 0;
        for (int x = 2; x + period < w - 2; x += 7)
        {
            Color a = read.GetPixel(x, row);
            Color b = read.GetPixel(x + period, row);
            repeat += Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b);
            samples++;
        }
        repeat = samples > 0 ? repeat / samples : -1f;
        bool tiled = period > 0 && repeat >= 0f && repeat < 0.02f && far > near * 0.5f;

        string line = "ART-IMPORT-CHECK TILE " + key
            + " box=" + boxWidth + "x" + boxHeight + "m native=" + native.ToString("0.###") + "m"
            + " period=" + period + "px row=" + row
            + " rowStd near=" + near.ToString("0.0000") + " far=" + far.ToString("0.0000")
            + " colourDiffAtOneTile=" + repeat.ToString("0.0000")
            + " -> " + (tiled ? "REPEATS" : "DOES NOT REPEAT (stretched/clamped)");
        if (tiled)
            Debug.Log(line);
        else
        {
            Debug.LogError(line);
            failures++;
        }

        Object.DestroyImmediate(read);
        Object.DestroyImmediate(go);
        Object.DestroyImmediate(camGo);
        rt.Release();
        Object.DestroyImmediate(rt);
    }

    private static float RowStd(Texture2D tex, int row, int x0, int x1)
    {
        float sum = 0f, sum2 = 0f;
        int n = 0;
        for (int x = x0; x < x1; x++)
        {
            Color c = tex.GetPixel(x, row);
            float lum = (c.r + c.g + c.b) / 3f;
            sum += lum;
            sum2 += lum * lum;
            n++;
        }
        if (n == 0)
            return 0f;
        float mean = sum / n;
        return Mathf.Sqrt(Mathf.Max(0f, sum2 / n - mean * mean));
    }

    private static void Report(string label, bool ok)
    {
        if (ok)
            Debug.Log("ART-IMPORT-CHECK OK " + label);
        else
        {
            Debug.LogError("ART-IMPORT-CHECK FAIL " + label);
            failures++;
        }
    }
}
