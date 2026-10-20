using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// ART-0 (31st pass) - the tree's BASE: which layer owns the line of backdrop under the bole?
//
// `Tools/art0-bole-hole-probe.py` measured, on the shipped still, one row of backdrop
// (y=1045, x 867..895, 29 px, 100 % exactly the gallery's (76,82,92)) running across the
// oak's foot, one row above the ground strip's first row, plus three tall backdrop pockets
// inside the bole. A row of pixels cannot name its owner, so this probe prints the layers
// that meet there and renders the frame with each one switched off:
//
//   1. the gallery is built in EDIT MODE through GalleryScene's own Build + SetRealArt (the
//      ArtTrunkProbe.cs / GalleryProbe.cs recipe, no play mode) and the shipped "oak close"
//      lens is reproduced (RowBounds["tree_oak"], 1920x1080, aspect 16:9);
//   2. the screen row of world y = 0 is printed, i.e. exactly where the tree's root and the
//      ground strip's top edge are (GalleryScene.groundTopY = 0, PlantTree.RootY(groundY) =
//      groundY, so the Stump's box bottom and the dirt's top edge are the same world line);
//   3. every renderer whose projected rows reach the foot band is listed with its path,
//      sprite, drawMode, sortingOrder and world box (ArtParts.WorldBoxOf) - the layers in
//      the order the camera draws them;
//   4. frames: `-probe` (as shipped), `-groundoff` (every GroundStrip renderer off),
//      `-stumpoff` (the tree's Stump part off), `-bothoff`. If the backdrop row is still
//      there with the ground off, it is the tree's own; if it disappears, the ground owns
//      it.
//
// Read-only with respect to the dress, the rig and physics: it only writes Trials/Runs PNGs.
//
//   "F:/6000.5.8f1/Editor/Unity.exe" -batchmode -projectPath "E:/Games/2D Game/My project" \
//     -executeMethod ArtBoleProbe.Run -logFile <log>
//
// No -quit: the probe exits the editor itself after the frames it needs have ticked.
public static class ArtBoleProbe
{
    private const float WarmupSeconds = 2f;
    private const int MinTicks = 5;
    private const int Width = 1920;
    private const int Height = 1080;
    private const string Label = "art0bole31";
    private const string TreeName = "Tree_Oak";

    private static float startedAt;
    private static int ticks;
    private static bool ran;

    public static void Run()
    {
        EditorSceneManager.OpenScene(GalleryBuild.GalleryPath, OpenSceneMode.Single);

        GameObject holder = new GameObject("ArtBoleProbe");
        GalleryScene gallery = holder.AddComponent<GalleryScene>();
        MethodInfo build = typeof(GalleryScene).GetMethod(
            "Build", BindingFlags.NonPublic | BindingFlags.Instance);
        if (build == null)
        {
            Debug.LogError("BOLE-PROBE FAIL GalleryScene.Build not found");
            EditorApplication.Exit(1);
            return;
        }
        build.Invoke(gallery, null);
        gallery.SetRealArt(true);
        ArtLibrary.RealArtEnabled = true;

        startedAt = (float)EditorApplication.timeSinceStartup;
        EditorApplication.update += Tick;
        Debug.Log("BOLE-PROBE begin scene=" + GalleryBuild.GalleryPath + " (edit mode, no play mode)");
    }

    private static void Tick()
    {
        if (ran)
            return;
        ticks++;
        if (ticks < MinTicks
            || EditorApplication.timeSinceStartup - startedAt < WarmupSeconds)
            return;

        ran = true;
        EditorApplication.update -= Tick;
        int code = 1;
        try
        {
            code = Probe();
        }
        catch (Exception e)
        {
            Debug.LogError("BOLE-PROBE FAIL exception " + e);
        }
        Debug.Log("BOLE-PROBE end ticks=" + ticks + " exit=" + code);
        EditorApplication.Exit(code);
    }

    private static int Probe()
    {
        GalleryScene gallery = UnityEngine.Object.FindFirstObjectByType<GalleryScene>();
        if (gallery == null)
        {
            Debug.LogError("BOLE-PROBE FAIL no GalleryScene in the scene");
            return 1;
        }

        Camera cam = gallery.Lens();
        Bounds bounds;
        if (cam == null || !gallery.RowBounds.TryGetValue("tree_oak", out bounds))
        {
            Debug.LogError("BOLE-PROBE FAIL no camera or no RowBounds[tree_oak]");
            return 1;
        }

        cam.aspect = Width / (float)Height;
        gallery.CameraTo(bounds);
        float pxPerMeter = Height / (2f * cam.orthographicSize);

        // Screen ROW (0 = top, like the PNG) of a world Y.
        Func<float, float> rowOfY = delegate (float wy)
        {
            Vector3 v = cam.WorldToViewportPoint(new Vector3(0f, wy, 0f));
            return (1f - v.y) * Height;
        };

        Debug.Log(string.Format(CultureInfo.InvariantCulture,
            "BOLE-PROBE lens ortho={0:0.000000} (shipped still 1.303697) centre=({1:0.000},{2:0.000}) pxPerMeter={3:0.00}",
            cam.orthographicSize, cam.transform.position.x, cam.transform.position.y, pxPerMeter));
        Debug.Log(string.Format(CultureInfo.InvariantCulture,
            "BOLE-PROBE world y=0 row={0:0.00}  y=-0.02 row={1:0.00}  y=+0.02 row={2:0.00}",
            rowOfY(0f), rowOfY(-0.02f), rowOfY(0.02f)));

        GameObject oak = FindByName(TreeName);
        if (oak == null)
        {
            Debug.LogError("BOLE-PROBE FAIL no " + TreeName + " under the gallery");
            return 1;
        }

        // 1. the tree's own parts at the foot: the Stump first, then every renderer whose
        //    drawn box reaches below 0.05 m.
        SpriteRenderer[] all = oak.GetComponentsInChildren<SpriteRenderer>(true);
        List<SpriteRenderer> stump = new List<SpriteRenderer>();
        List<SpriteRenderer> foot = new List<SpriteRenderer>();
        for (int i = 0; i < all.Length; i++)
        {
            SpriteRenderer sr = all[i];
            if (sr == null || sr.sprite == null || !sr.enabled)
                continue;
            string path = PathOf(sr);
            Vector2 box = ArtParts.WorldBoxOf(sr);
            float bottom = sr.bounds.min.y;
            if (path.IndexOf("Stump", StringComparison.Ordinal) >= 0)
                stump.Add(sr);
            if (bottom < 0.08f)
                foot.Add(sr);
        }
        Debug.Log("BOLE-PROBE oak renderers=" + all.Length + " stump=" + stump.Count
            + " withBoxBelow0.08m=" + foot.Count);

        for (int i = 0; i < stump.Count; i++)
        {
            SpriteRenderer sr = stump[i];
            Vector2 box = ArtParts.WorldBoxOf(sr);
            Debug.Log(string.Format(CultureInfo.InvariantCulture,
                "BOLE-PROBE STUMP path={0} sprite={1} mode={2} order={3} world={4:0.0000}x{5:0.0000}m boxY {6:0.00000}..{7:0.00000} boundsY {8:0.00000}..{9:0.00000}",
                PathOf(sr), sr.sprite != null ? sr.sprite.name : "?", sr.drawMode, sr.sortingOrder,
                box.x, box.y, sr.transform.position.y - box.y * 0.5f, sr.transform.position.y + box.y * 0.5f,
                sr.bounds.min.y, sr.bounds.max.y));
        }

        foot.Sort((SpriteRenderer a, SpriteRenderer b) => a.bounds.min.y.CompareTo(b.bounds.min.y));
        for (int i = 0; i < foot.Count && i < 30; i++)
        {
            SpriteRenderer sr = foot[i];
            Vector2 box = ArtParts.WorldBoxOf(sr);
            Debug.Log(string.Format(CultureInfo.InvariantCulture,
                "BOLE-PROBE FOOT path={0} sprite={1} mode={2} order={3} world={4:0.000}x{5:0.000}m bottom={6:0.00000}m top={7:0.00000}m x {8:0.000}..{9:0.000}",
                PathOf(sr), sr.sprite != null ? sr.sprite.name : "?", sr.drawMode, sr.sortingOrder,
                box.x, box.y, sr.bounds.min.y, sr.bounds.max.y,
                sr.bounds.min.x, sr.bounds.max.x));
        }

        // 1b. EVERY renderer under the Stump's Visual, switched off ones included: the
        //     part's OWN sprite (the slot a canvas would take back) and the bark cards the
        //     cone fill draws over it. Sibling index = the draw order inside the Visual.
        for (int i = 0; i < all.Length; i++)
        {
            SpriteRenderer sr = all[i];
            if (sr == null)
                continue;
            string path = PathOf(sr);
            if (path.IndexOf("Stump", StringComparison.Ordinal) < 0)
                continue;
            Transform t = sr.transform.parent;
            Vector2 box = ArtParts.WorldBoxOf(sr);
            Debug.Log(string.Format(CultureInfo.InvariantCulture,
                "BOLE-PROBE SLOT path={0} on={1} sibling={2} parent={3} sprite={4} mode={5} order={6} box={7:0.0000}x{8:0.0000}m boxBottom={9:0.00000}m localPos=({10:0.0000},{11:0.0000})",
                path, sr.enabled, sr.transform.GetSiblingIndex(),
                t != null ? t.gameObject.name : "-",
                sr.sprite != null ? sr.sprite.name : "none", sr.drawMode, sr.sortingOrder,
                box.x, box.y, sr.transform.position.y - box.y * 0.5f,
                sr.transform.localPosition.x, sr.transform.localPosition.y));
        }

        // 1c. the same for every tree in the gallery: the Stump's own box aspect is what a
        //     bole canvas would be drawn in, and the family spread decides how many canvases.
        GameObject[] objects = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
        for (int i = 0; i < objects.Length; i++)
        {
            GameObject go = objects[i];
            if (go == null || !go.name.StartsWith("Tree_", StringComparison.Ordinal))
                continue;
            SpriteRenderer[] parts = go.GetComponentsInChildren<SpriteRenderer>(true);
            int wood = 0;
            SpriteRenderer own = null;
            for (int k = 0; k < parts.Length; k++)
            {
                SpriteRenderer sr = parts[k];
                if (sr == null || sr.sprite == null)
                    continue;
                string path = PathOf(sr);
                if (path.IndexOf("Stump", StringComparison.Ordinal) >= 0)
                    wood++;
                if (path.EndsWith("Stump/Visual", StringComparison.Ordinal))
                    own = sr;
            }
            if (own == null)
            {
                Debug.Log("BOLE-PROBE TREE " + go.name + " no Stump/Visual renderer (woodParts=" + wood + ")");
                continue;
            }
            Vector2 box = ArtParts.WorldBoxOf(own);
            Debug.Log(string.Format(CultureInfo.InvariantCulture,
                "BOLE-PROBE TREE {0} stumpBox={1:0.0000}x{2:0.0000}m aspect={3:0.0000} on={4} mode={5} sprite={6} stumpParts={7}",
                go.name, box.x, box.y, box.x / Mathf.Max(0.0001f, box.y), own.enabled, own.drawMode,
                own.sprite != null ? own.sprite.name : "none", wood));
        }


        // 2. the ground strip: the level's own slab, the other layer on that line.
        GameObject ground = FindByName("GroundStrip");
        if (ground == null)
        {
            Debug.LogWarning("BOLE-PROBE note no GameObject named GroundStrip");
        }
        else
        {
            SpriteRenderer[] gs = ground.GetComponentsInChildren<SpriteRenderer>(true);
            Debug.Log("BOLE-PROBE ground renderers=" + gs.Length + " position="
                + ground.transform.position.y.ToString("0.00000", CultureInfo.InvariantCulture));
            for (int i = 0; i < gs.Length; i++)
            {
                SpriteRenderer sr = gs[i];
                Vector2 box = ArtParts.WorldBoxOf(sr);
                Debug.Log(string.Format(CultureInfo.InvariantCulture,
                    "BOLE-PROBE GROUND path={0} sprite={1} mode={2} order={3} world={4:0.000}x{5:0.000}m top={6:0.00000}m bottom={7:0.00000}m",
                    PathOf(sr), sr.sprite != null ? sr.sprite.name : "?", sr.drawMode, sr.sortingOrder,
                    box.x, box.y, sr.bounds.max.y, sr.bounds.min.y));
            }
        }

        // 3. the frames: as shipped, then with each layer of the meeting point switched off.
        Texture2D baseTex = PhotoCapture.Render(cam, Width, Height);
        if (baseTex == null)
        {
            Debug.LogError("BOLE-PROBE FAIL render failed");
            return 1;
        }
        Write(baseTex, Label + "-probe.png");

        List<SpriteRenderer> groundR = ground != null
            ? new List<SpriteRenderer>(ground.GetComponentsInChildren<SpriteRenderer>(true))
            : new List<SpriteRenderer>();
        SetEnabled(groundR, false);
        Write(PhotoCapture.Render(cam, Width, Height), Label + "-groundoff.png");
        SetEnabled(groundR, true);

        SetEnabled(stump, false);
        Write(PhotoCapture.Render(cam, Width, Height), Label + "-stumpoff.png");
        SetEnabled(stump, true);

        SetEnabled(stump, false);
        SetEnabled(groundR, false);
        Write(PhotoCapture.Render(cam, Width, Height), Label + "-bothoff.png");
        SetEnabled(groundR, true);
        SetEnabled(stump, true);

        return 0;
    }

    private static void SetEnabled(List<SpriteRenderer> list, bool on)
    {
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] != null)
                list[i].enabled = on;
        }
    }

    private static GameObject FindByName(string name)
    {
        GameObject[] all = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].name == name)
                return all[i];
        }
        return null;
    }

    private static string PathOf(SpriteRenderer sr)
    {
        string path = sr.gameObject.name;
        Transform t = sr.transform.parent;
        for (int depth = 0; depth < 5 && t != null; depth++)
        {
            path = t.gameObject.name + "/" + path;
            if (t.parent == null)
                break;
            t = t.parent;
        }
        return path;
    }

    private static void Write(Texture2D tex, string file)
    {
        if (tex == null)
        {
            Debug.LogError("BOLE-PROBE FAIL no texture for " + file);
            return;
        }
        string path = Path.Combine(
            Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Trials"), "Runs");
        Directory.CreateDirectory(path);
        File.WriteAllBytes(Path.Combine(path, file), tex.EncodeToPNG());
        Debug.Log("BOLE-PROBE wrote " + Path.Combine(path, file));
    }
}
