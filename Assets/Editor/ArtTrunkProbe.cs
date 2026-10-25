using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// ART-0 (30th pass) - NAME the owner of the green chips on the oak's trunk.
//
// The 29th pass closed the crown (the painted leaf card) and left one thing open with a
// concrete next step: "an editor probe that prints the leaf-material parts (>= 0.12 m)
// whose world box overlaps the trunk column in the tree camera, so the owner has a name
// before anything is moved". A world box alone cannot answer it: a leaf part BEHIND the
// bark is invisible, and one in FRONT of it is a chip. So this probe does not guess from
// geometry - it PAINTS THE PART OUT and asks the pixels.
//
//   1. the gallery is built in EDIT MODE through the same code the scene runs
//      (GalleryScene.Build + SetRealArt, the GalleryProbe.cs recipe - a reflection call,
//      no play mode), and the tree lens of the shipped still is reproduced:
//      RowBounds["tree_oak"], 1920x1080, aspect 16:9. The probe prints its ortho size so
//      it can be compared with the `orthoSize` in Trials/Runs/<label>-gallery.json;
//   2. `base`      : every renderer of the oak as the dress left it;
//      `barkOnly`  : the same frame with every leaf-MATERIAL renderer switched off
//                    (sprite name "tile_leaf@<period>" - a card is "tile_Tree/leaf_card@"
//                    and stays, so this isolates the material, not the crown);
//   3. `trunk` = bark mask of `barkOnly`, `chips` = green mask of `base` AND trunk -
//      i.e. green ink drawn ON TOP of the bole, which is what the reads named;
//   4. every leaf-material renderer whose projected box reaches the chips is switched
//      off on its own and re-rendered; the pixels of `chips` it owns are counted, so
//      the owner is the renderer whose absence removes chips, measured, not inferred.
//
// Read-only with respect to the dress and to physics: it only writes Trials/Runs PNGs,
// like every other ART-0 probe. It never writes a scene, a prefab or a sprite.
//
//   "F:/6000.5.8f1/Editor/Unity.exe" -batchmode -projectPath "E:/Games/2D Game/My project" \
//     -executeMethod ArtTrunkProbe.Run -logFile <log>
//
// No -quit: the probe exits the editor itself, after the frames it needs have passed
// (a Sliced/Tiled conversion only rebuilds the sprite mesh on a later editor tick, so
// the render cannot happen inside Run()).
public static class ArtTrunkProbe
{
    private const float WarmupSeconds = 2f;
    private const int MinTicks = 5;
    private const int Width = 1920;
    private const int Height = 1080;
    private const string Label = "art0trunk30";

    // The reads' own colour tests, as fractions of 1 so Color32 needs no casting:
    //   green: g > r + 24/255 and g > b + 24/255 and g > 60/255
    //   bark : r > g + 12/255 and g > b + 8/255 and 60/255 < r < 210/255
    private const float GreenMargin = 24f / 255f;
    private const float GreenFloor = 60f / 255f;
    private const float BarkRg = 12f / 255f;
    private const float BarkGb = 8f / 255f;

    private const float CardMinMeters = 0.02f;
    private const float CardMaxMeters = 0.12f;

    private static float startedAt;
    private static int ticks;
    private static bool ran;

    // Edit mode, the GalleryProbe.cs recipe: no play mode, the scene is opened and the
    // catalogue built through GalleryScene's own Build + SetRealArt.
    public static void Run()
    {
        EditorSceneManager.OpenScene(GalleryBuild.GalleryPath, OpenSceneMode.Single);

        GameObject holder = new GameObject("ArtTrunkProbe");
        GalleryScene gallery = holder.AddComponent<GalleryScene>();
        MethodInfo build = typeof(GalleryScene).GetMethod(
            "Build", BindingFlags.NonPublic | BindingFlags.Instance);
        if (build == null)
        {
            Debug.LogError("TRUNK-PROBE FAIL GalleryScene.Build not found");
            EditorApplication.Exit(1);
            return;
        }
        build.Invoke(gallery, null);
        gallery.SetRealArt(true);
        ArtLibrary.RealArtEnabled = true;

        startedAt = (float)EditorApplication.timeSinceStartup;
        EditorApplication.update += Tick;
        Debug.Log("TRUNK-PROBE begin scene=" + GalleryBuild.GalleryPath
            + " (edit mode, no play mode)");
    }

    // The frames have to pass outside this method: Dress writes sprite + size, and Unity
    // rebuilds a Sliced/Tiled sprite mesh on a later editor tick.
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
            Debug.LogError("TRUNK-PROBE FAIL exception " + e);
        }
        Debug.Log("TRUNK-PROBE end ticks=" + ticks + " exit=" + code);
        EditorApplication.Exit(code);
    }

    private static int Probe()
    {
        GalleryScene gallery = UnityEngine.Object.FindFirstObjectByType<GalleryScene>();
        if (gallery == null)
        {
            Debug.LogError("TRUNK-PROBE FAIL no GalleryScene in the scene");
            return 1;
        }

        Camera cam = gallery.Lens();
        if (cam == null)
        {
            Debug.LogError("TRUNK-PROBE FAIL no camera");
            return 1;
        }

        Bounds bounds;
        if (!gallery.RowBounds.TryGetValue("tree_oak", out bounds))
        {
            Debug.LogError("TRUNK-PROBE FAIL no RowBounds[tree_oak]");
            return 1;
        }

        // The shipped still's own lens, reproduced: aspect 16:9 then CameraTo, which is
        // what GalleryCapture.Still does for "oak close". orthoSize has to come out
        // 1.303697 (art0leaf29-gallery.json) or the frames are not comparable.
        cam.aspect = Width / (float)Height;
        gallery.CameraTo(bounds);
        float pxPerMeter = Height / (2f * cam.orthographicSize);
        Debug.Log(string.Format(CultureInfo.InvariantCulture,
            "TRUNK-PROBE lens ortho={0:0.000000} (shipped still 1.303697) centre=({1:0.000},{2:0.000}) box={3:0.000}x{4:0.000} pxPerMeter={5:0.00}",
            cam.orthographicSize, cam.transform.position.x, cam.transform.position.y,
            bounds.size.x, bounds.size.y, pxPerMeter));

        GameObject oak = FindOak();
        if (oak == null)
        {
            Debug.LogError("TRUNK-PROBE FAIL no oak under the gallery");
            return 1;
        }

        SpriteRenderer[] all = oak.GetComponentsInChildren<SpriteRenderer>(true);
        Dictionary<string, int> names = new Dictionary<string, int>();
        List<SpriteRenderer> leaves = new List<SpriteRenderer>();
        List<SpriteRenderer> cards = new List<SpriteRenderer>();
        int live = 0;
        for (int i = 0; i < all.Length; i++)
        {
            SpriteRenderer sr = all[i];
            if (sr == null || sr.sprite == null)
                continue;
            string name = sr.sprite.name ?? "";
            int n;
            names.TryGetValue(name, out n);
            names[name] = n + 1;
            if (!sr.enabled)
                continue;
            live++;
            if (name.StartsWith("tile_leaf@", StringComparison.Ordinal))
                leaves.Add(sr);
            else if (name.StartsWith("tile_Tree/leaf_card@", StringComparison.Ordinal))
                cards.Add(sr);
        }
        Debug.Log("TRUNK-PROBE oak renderers=" + all.Length + " drawn=" + live
            + " leafMaterial=" + leaves.Count + " leafCards=" + cards.Count);
        int shown = 0;
        foreach (KeyValuePair<string, int> kv in Sorted(names))
        {
            if (shown++ >= 8)
                break;
            Debug.Log("TRUNK-PROBE sprite " + kv.Key + " x" + kv.Value);
        }
        if (shown < names.Count)
            Debug.Log("TRUNK-PROBE sprite ... " + (names.Count - shown) + " more names");

        // 1. the frame as shipped.
        Texture2D baseTex = PhotoCapture.Render(cam, Width, Height);
        if (baseTex == null)
        {
            Debug.LogError("TRUNK-PROBE FAIL base render failed");
            return 1;
        }
        Color32[] basePx = baseTex.GetPixels32();
        Write(baseTex, Label + "-probe.png");

        // 2. the same frame with the leaf MATERIAL off (the cards stay: their sprite name
        //    is tile_Tree/leaf_card@, which the filter above does not match).
        SetEnabled(leaves, false);
        Texture2D barkTex = PhotoCapture.Render(cam, Width, Height);
        SetEnabled(leaves, true);
        if (barkTex == null)
        {
            Debug.LogError("TRUNK-PROBE FAIL barkOnly render failed");
            return 1;
        }
        Color32[] barkPx = barkTex.GetPixels32();
        Write(barkTex, Label + "-barkonly.png");

        // 2b. the same frame with the leaf CARDS off and the material on: the two leaf
        //     languages the rig mixes, measured apart. This pass found that 30 % of the
        //     green ink on the bole survives the material being switched off, so the
        //     cards' share has to be measured, not assumed.
        SetEnabled(cards, false);
        Texture2D cardTex = PhotoCapture.Render(cam, Width, Height);
        SetEnabled(cards, true);
        if (cardTex == null)
        {
            Debug.LogError("TRUNK-PROBE FAIL cardsOff render failed");
            return 1;
        }
        Color32[] cardPx = cardTex.GetPixels32();
        Write(cardTex, Label + "-cardsoff.png");

        // 2c. both leaf languages off: what is left green on the bole is drawn by neither,
        //     so the rig's own decor is named as the remainder instead of left as "unknown".
        SetEnabled(leaves, false);
        SetEnabled(cards, false);
        Texture2D bareTex = PhotoCapture.Render(cam, Width, Height);
        SetEnabled(cards, true);
        SetEnabled(leaves, true);
        if (bareTex == null)
        {
            Debug.LogError("TRUNK-PROBE FAIL bothOff render failed");
            return 1;
        }
        Write(bareTex, Label + "-bothoff.png");

        // 3. green ink drawn on the bole.
        bool[] chips = new bool[Width * Height];
        int chipCount = 0;
        int barkCount = 0;
        int materialChip = 0;
        int x0 = Width, x1 = -1, y0 = Height, y1 = -1;
        for (int y = 0; y < Height; y++)
        {
            int row = y * Width;
            for (int x = 0; x < Width; x++)
            {
                Color32 b = barkPx[row + x];
                if (IsBark(b))
                    barkCount++;
                if (IsGreen(cardPx[row + x]))
                    materialChip++;      // green that is the MATERIAL's own work: the
                                         // cards are off in that frame
                if (!IsGreen(basePx[row + x]) || !IsBark(b))
                    continue;
                chips[row + x] = true;
                chipCount++;
                if (x < x0) x0 = x;
                if (x > x1) x1 = x;
                if (y < y0) y0 = y;
                if (y > y1) y1 = y;
            }
        }
        Debug.Log("TRUNK-PROBE barkPixels=" + barkCount + " chipPixels=" + chipCount
            + " greenWithCardsOff=" + materialChip
            + " (the leaf material's own share of the green in this frame)");
        if (chipCount == 0)
        {
            Debug.Log("TRUNK-PROBE note no green ink on the bole in this frame: nothing to name");
            return 0;
        }
        Debug.Log("TRUNK-PROBE chipBox x " + x0 + ".." + x1 + " y " + y0 + ".." + y1
            + "  (" + (x1 - x0 + 1) + "x" + (y1 - y0 + 1) + " px)");
        foreach (int[] box in Clusters(chips))
            Debug.Log("TRUNK-PROBE chipCluster box " + box[2] + "x" + box[3] + " px at ("
                + box[0] + "," + box[1] + ") ink=" + box[4]);

        // 4. every renderer of the oak that can reach the bole, painted out alone. Not only
        //    the leaf material: this pass measured that 30 % of the green ink on the bole
        //    SURVIVES the leaf material being switched off, so its owner is another
        //    renderer and the probe has to be able to name that one too.
        int cx0 = Mathf.Max(0, x0 - 8), cx1 = Mathf.Min(Width - 1, x1 + 8);
        int cy0 = Mathf.Max(0, y0 - 8), cy1 = Mathf.Min(Height - 1, y1 + 8);
        List<SpriteRenderer> candidates = new List<SpriteRenderer>();
        for (int i = 0; i < all.Length; i++)
        {
            SpriteRenderer sr = all[i];
            if (sr == null || sr.sprite == null || !sr.enabled)
                continue;
            int rx0, rx1, ry0, ry1;
            if (!Project(cam, sr, out rx0, out rx1, out ry0, out ry1))
                continue;
            if (rx1 < cx0 || rx0 > cx1 || ry1 < cy0 || ry0 > cy1)
                continue;
            candidates.Add(sr);
        }
        Debug.Log("TRUNK-PROBE candidates=" + candidates.Count + " of " + all.Length
            + " renderers of the oak reach the bole (leaf material "
            + leaves.Count + " of them)");

        List<Owner> owners = new List<Owner>();
        for (int i = 0; i < candidates.Count; i++)
        {
            SpriteRenderer sr = candidates[i];
            sr.enabled = false;
            Texture2D offTex = PhotoCapture.Render(cam, Width, Height);
            sr.enabled = true;
            Color32[] offPx = offTex.GetPixels32();

            int owned = 0, changed = 0;
            int ox0 = Width, ox1 = -1, oy0 = Height, oy1 = -1;
            for (int y = cy0; y <= cy1; y++)
            {
                int row = y * Width;
                for (int x = cx0; x <= cx1; x++)
                {
                    Color32 a = basePx[row + x];
                    Color32 o = offPx[row + x];
                    if (chips[row + x] && !IsGreen(o))
                    {
                        owned++;
                        if (x < ox0) ox0 = x;
                        if (x > ox1) ox1 = x;
                        if (y < oy0) oy0 = y;
                        if (y > oy1) oy1 = y;
                    }
                    if (Mathf.Abs(a.r - o.r) + Mathf.Abs(a.g - o.g) + Mathf.Abs(a.b - o.b)
                        + Mathf.Abs(a.a - o.a) > 20)
                        changed++;
                }
            }
            UnityEngine.Object.Destroy(offTex);

            Vector2 box = ArtParts.WorldBoxOf(sr);
            string line = string.Format(CultureInfo.InvariantCulture,
                "TRUNK-PROBE cand path={0} sprite={1} world={2:0.000}x{3:0.000}m min={4:0.000}m order={5} ownedChipPixels={6} changedPixels={7}",
                PathOf(sr), sr.sprite != null ? sr.sprite.name : "?", box.x, box.y,
                Mathf.Min(box.x, box.y), sr.sortingOrder, owned, changed);
            if (owned > 0)
                line += " ownedBox x " + ox0 + ".." + ox1 + " y " + oy0 + ".." + oy1;
            Debug.Log(line);

            if (owned > 0)
                owners.Add(new Owner
                {
                    sr = sr,
                    owned = owned,
                    changed = changed,
                    ox0 = ox0,
                    ox1 = ox1,
                    oy0 = oy0,
                    oy1 = oy1,
                    box = box
                });
        }

        owners.Sort((a, b) => b.owned.CompareTo(a.owned));
        int sum = 0;
        for (int i = 0; i < owners.Count; i++)
            sum += owners[i].owned;
        Debug.Log("TRUNK-PROBE owners=" + owners.Count + " chipPixels=" + chipCount
            + " attributed=" + sum + " (" + (100f * sum / Mathf.Max(1, chipCount)).ToString("0.0")
            + " %)");

        int rank = 0;
        for (int i = 0; i < owners.Count && rank < 12; i++, rank++)
        {
            Owner o = owners[i];
            Vector2 box = o.box;
            Debug.Log(string.Format(CultureInfo.InvariantCulture,
                "TRUNK-PROBE OWNER {0} owned={1} px box={2:0.000}x{3:0.000}m min={4:0.000}m cardBand={5} sprite={6} at x {7}..{8} y {9}..{10} path={11}",
                rank + 1, o.owned, box.x, box.y, Mathf.Min(box.x, box.y),
                Mathf.Min(box.x, box.y) >= CardMinMeters && Mathf.Min(box.x, box.y) < CardMaxMeters,
                o.sr.sprite != null ? o.sr.sprite.name : "?", o.ox0, o.ox1, o.oy0, o.oy1, PathOf(o.sr)));
        }

        // 5. the eye's copy of the only frame that matters: the bole with the owners off.
        if (owners.Count > 0)
        {
            for (int i = 0; i < owners.Count; i++)
                owners[i].sr.enabled = false;
            Texture2D noOwner = PhotoCapture.Render(cam, Width, Height);
            Write(noOwner, Label + "-owners-off.png");
            for (int i = 0; i < owners.Count; i++)
                owners[i].sr.enabled = true;
        }

        return 0;
    }

    private struct Owner
    {
        public SpriteRenderer sr;
        public int owned;
        public int changed;
        public int ox0, ox1, oy0, oy1;
        public Vector2 box;
    }

    private static void SetEnabled(List<SpriteRenderer> list, bool on)
    {
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] != null)
                list[i].enabled = on;
        }
    }

    private static bool IsGreen(Color32 c)
    {
        float r = c.r / 255f, g = c.g / 255f, b = c.b / 255f;
        return g > r + GreenMargin && g > b + GreenMargin && g > GreenFloor;
    }

    private static bool IsBark(Color32 c)
    {
        float r = c.r / 255f, g = c.g / 255f, b = c.b / 255f;
        return r > g + BarkRg && g > b + BarkGb && r > 60f / 255f && r < 210f / 255f;
    }

    private static List<KeyValuePair<string, int>> Sorted(Dictionary<string, int> map)
    {
        List<KeyValuePair<string, int>> list = new List<KeyValuePair<string, int>>(map);
        list.Sort((a, b) => b.Value.CompareTo(a.Value));
        return list;
    }

    private static GameObject FindOak()
    {
        GameObject[] all = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].name == "Tree_Oak")
                return all[i];
        }
        return null;
    }

    // World AABB of the renderer, in frame pixels (row 0 = top, like the PNG).
    private static bool Project(Camera cam, SpriteRenderer sr, out int x0, out int x1, out int y0, out int y1)
    {
        x0 = Width; x1 = -1; y0 = Height; y1 = -1;
        if (sr == null || sr.sprite == null)
            return false;
        Bounds b = sr.bounds;
        for (int xi = -1; xi <= 1; xi += 2)
        {
            for (int yi = -1; yi <= 1; yi += 2)
            {
                Vector3 v = cam.WorldToViewportPoint(
                    new Vector3(b.center.x + xi * b.extents.x, b.center.y + yi * b.extents.y, 0f));
                int px = Mathf.RoundToInt(v.x * Width);
                int py = Mathf.RoundToInt((1f - v.y) * Height);
                if (px < x0) x0 = px;
                if (px > x1) x1 = px;
                if (py < y0) y0 = py;
                if (py > y1) y1 = py;
            }
        }
        return x1 >= 0 && y1 >= 0;
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
            return;
        string path = Path.Combine(
            Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Trials"),
            "Runs");
        Directory.CreateDirectory(path);
        File.WriteAllBytes(Path.Combine(path, file), tex.EncodeToPNG());
        Debug.Log("TRUNK-PROBE wrote " + Path.Combine(path, file));
    }

    // 4-connected boxes of the chip mask (the mask is a few thousand pixels, so a stack
    // flood fill is cheap; the frame is 2 MP and would not be).
    private static List<int[]> Clusters(bool[] mask)
    {
        List<int[]> boxes = new List<int[]>();
        bool[] seen = new bool[mask.Length];
        int[] stack = new int[mask.Length];
        for (int start = 0; start < mask.Length; start++)
        {
            if (!mask[start] || seen[start])
                continue;
            int top = 0;
            stack[top++] = start;
            seen[start] = true;
            int minX = Width, maxX = -1, minY = Height, maxY = -1, count = 0;
            while (top > 0)
            {
                int index = stack[--top];
                int y = index / Width;
                int x = index - y * Width;
                count++;
                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
                if (x > 0) Push(mask, seen, stack, ref top, index - 1);
                if (x < Width - 1) Push(mask, seen, stack, ref top, index + 1);
                if (y > 0) Push(mask, seen, stack, ref top, index - Width);
                if (y < Height - 1) Push(mask, seen, stack, ref top, index + Width);
            }
            if (count >= 12)
                boxes.Add(new[] { minX, minY, maxX - minX + 1, maxY - minY + 1, count });
        }
        boxes.Sort((a, b) => b[4].CompareTo(a[4]));
        return boxes;
    }

    private static void Push(bool[] mask, bool[] seen, int[] stack, ref int top, int index)
    {
        if (!mask[index] || seen[index])
            return;
        seen[index] = true;
        stack[top++] = index;
    }
}
