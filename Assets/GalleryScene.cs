using System.Collections.Generic;
using UnityEngine;

// ART-1 gallery - the one scene where every object is shown with the real
// texture that will ship (Yury, 18.09). It builds its own objects; no gameplay
// script runs here: no human, no AI, no spawner, no levelled ground.
//
// Contract kept from the rest of the project: an object's picture is its own
// rectangle (collider box or renderer size) filled with the real texture, never
// localScale. Everything is drawn UNLIT on purpose - the gallery judges the
// texture, not the lighting, and an unlit sprite cannot go black for want of a
// Light2D.
//
// Where a texture does not exist the scene draws a labelled MISSING ART tile,
// and where the art exists but the rig binding belongs to another lane it draws
// an explicit "... pending" slot. Nothing is a silent placeholder.
public class GalleryScene : MonoBehaviour
{
    public struct Item
    {
        public string name;
        public string texture;
        public bool real;
        public string note;
    }

    [Header("Toggle")]
    [Tooltip("The real/placeholder comparison switch. T at run time, or SetRealArt().")]
    public bool realArt = true;

    [Header("Ground")]
    public float groundTopY = 0f;
    public float groundLeft = -6f;
    public float groundRight = 108f;
    public float groundHeight = 2f;

    [Header("Labels")]
    public float rowTitleHeight = 0.30f;
    public float objectLabelHeight = 0.13f;
    public float labelDrop = 0.26f;
    public Color labelColor = new Color(1f, 1f, 0.96f, 1f);

    [Header("Background")]
    public Color backgroundColor = new Color(0.30f, 0.32f, 0.36f, 1f);

    public Bounds ContentBounds { get; private set; }
    public readonly Dictionary<string, Bounds> RowBounds = new Dictionary<string, Bounds>();
    public readonly List<Item> Inventory = new List<Item>();

    public int RealItemCount { get; private set; }
    public int MissingItemCount { get; private set; }
    public int DressedRendererCount { get; private set; }

    private struct DressTarget
    {
        public Transform root;
        public string wood;
        public string leaf;
        public string fruit;
        public string stem;
    }

    private readonly List<DressTarget> dressTargets = new List<DressTarget>();
    private readonly List<Transform> galleryRoots = new List<Transform>();
    private Camera cam;

    private const int SortingObject = 0;
    private const int SortingLabel = 60;

    private static readonly string[] MaterialKeys =
    {
        ArtLibrary.Skin, ArtLibrary.Fabric, ArtLibrary.Denim, ArtLibrary.Dirt,
        ArtLibrary.Grass, ArtLibrary.Bark, ArtLibrary.Leaf
    };

    private static readonly TreeKind[] TreeRow =
    {
        TreeKind.Oak, TreeKind.Pine, TreeKind.Willow, TreeKind.Poplar, TreeKind.Bush
    };

    void Start()
    {
        cam = Camera.main;
        if (cam != null)
        {
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = backgroundColor;
        }

        ArtLibrary.RealArtEnabled = realArt;
        Build();
        SetRealArt(realArt);

        // Interactive Play (no -capture): show the whole catalogue and let the
        // viewer drive. The capture run takes the camera over instead.
        if (!CaptureArmed())
        {
            cam.orthographic = true;
            CameraTo(ContentBounds);
        }
    }

    public static bool CaptureArmed()
    {
        return System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-capture") >= 0;
    }

    void Update()
    {
        if (TogglePressed())
            SetRealArt(!realArt);

        if (CaptureArmed() || cam == null)
            return;

        // Cheap viewer controls: A/D or arrows pan, Q/E zoom, F frames the lot.
        float move = PanAxis();
        if (Mathf.Abs(move) > 0.001f)
            cam.transform.position += new Vector3(move * cam.orthographicSize * 0.8f * Time.deltaTime, 0f, 0f);

        float zoom = ZoomAxis();
        if (Mathf.Abs(zoom) > 0.001f)
            cam.orthographicSize = Mathf.Max(0.4f, cam.orthographicSize * (1f - zoom * 0.6f * Time.deltaTime));

        if (FramePressed())
            CameraTo(ContentBounds);
    }

    // This project runs the Input System package with legacy handling switched
    // off, so the old UnityEngine.Input class throws (InvalidOperationException)
    // on every frame it is touched.
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
    private static UnityEngine.InputSystem.Keyboard Kb()
    {
        return UnityEngine.InputSystem.Keyboard.current;
    }

    private static bool TogglePressed()
    {
        UnityEngine.InputSystem.Keyboard kb = Kb();
        return kb != null && (kb.tKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame);
    }

    private static float PanAxis()
    {
        UnityEngine.InputSystem.Keyboard kb = Kb();
        if (kb == null)
            return 0f;
        float right = kb.dKey.isPressed || kb.rightArrowKey.isPressed ? 1f : 0f;
        float left = kb.aKey.isPressed || kb.leftArrowKey.isPressed ? 1f : 0f;
        return right - left;
    }

    private static float ZoomAxis()
    {
        UnityEngine.InputSystem.Keyboard kb = Kb();
        if (kb == null)
            return 0f;
        return (kb.eKey.isPressed ? 1f : 0f) - (kb.qKey.isPressed ? 1f : 0f);
    }

    private static bool FramePressed()
    {
        UnityEngine.InputSystem.Keyboard kb = Kb();
        return kb != null && kb.fKey.wasPressedThisFrame;
    }
#else
    private static bool TogglePressed()
    {
        return Input.GetKeyDown(KeyCode.T) || Input.GetKeyDown(KeyCode.Space);
    }

    private static float PanAxis()
    {
        float right = Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) ? 1f : 0f;
        float left = Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) ? 1f : 0f;
        return right - left;
    }

    private static float ZoomAxis()
    {
        return (Input.GetKey(KeyCode.E) ? 1f : 0f) - (Input.GetKey(KeyCode.Q) ? 1f : 0f);
    }

    private static bool FramePressed()
    {
        return Input.GetKeyDown(KeyCode.F);
    }
#endif

    // Whole catalogue in the frame, for the scene's own Play mode.
    public void CameraTo(Bounds bounds)
    {
        Camera lens = Lens();
        if (lens == null)
            return;
        float aspect = lens.aspect > 0.1f ? lens.aspect : 16f / 9f;
        lens.orthographic = true;
        lens.orthographicSize = Mathf.Max(bounds.extents.y, bounds.extents.x / aspect) * 1.06f;
        lens.transform.position = new Vector3(bounds.center.x, bounds.center.y, -10f);
    }

    public Camera Lens()
    {
        if (cam == null)
            cam = Camera.main;
        return cam;
    }

    // ─── the comparison toggle ────────────────────────────────────────────
    public void SetRealArt(bool on)
    {
        realArt = on;
        ArtLibrary.RealArtEnabled = on;
        ArtParts.ResetSkips();

        int dressed = 0;
        for (int i = 0; i < dressTargets.Count; i++)
        {
            DressTarget t = dressTargets[i];
            if (t.root == null)
                continue;
            dressed += ArtParts.DressAll(t.root, on, t.wood, t.leaf, t.fruit, t.stem);
        }

        // Flowers are not dress targets: a petal is not a tree part and its host is the
        // PlantFlower itself, so one scoped call covers every flower row (and cannot touch
        // a tree, whose part names collide with the flower's).
        dressed += ArtParts.DressFlower(transform, on);
        DressedRendererCount = dressed;
    }

    // ─── build ────────────────────────────────────────────────────────────
    private void Build()
    {
        Transform root = new GameObject("GalleryRoot").transform;
        root.SetParent(transform, false);
        galleryRoots.Add(root);

        BuildGroundStrip(root);
        BuildLevelRow(root);
        BuildMaterialRow(root);
        BuildTreeRow(root);
        BuildAppleRow(root);
        BuildBerryRow(root);
        BuildHumanRow(root);
        BuildCreatureRow(root);
        BuildFireRow(root);
        BuildMissingRow(root);

        ContentBounds = new Bounds(
            new Vector3((groundLeft + groundRight) * 0.5f, 2.4f, 0f),
            new Vector3(groundRight - groundLeft, 9.2f, 0f));

        // The box every drawn part fills before any real texture lands. The
        // capture compares it with the box after the frames have passed: a
        // retexture must not move or resize a single part.
        PreDressBounds = LiveBounds();
        Debug.Log("GALLERY-BUILD predress bounds centre=(" + PreDressBounds.center.x.ToString("0.00")
            + "," + PreDressBounds.center.y.ToString("0.00") + ") size="
            + PreDressBounds.size.x.ToString("0.00") + "x" + PreDressBounds.size.y.ToString("0.00"));
    }

    public Bounds PreDressBounds { get; private set; }

    // Union of every drawn sprite under the gallery, right now.
    public Bounds LiveBounds()
    {
        SpriteRenderer[] all = GetComponentsInChildren<SpriteRenderer>(true);
        Bounds sum = default(Bounds);
        bool any = false;
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] == null || !all[i].enabled || all[i].sprite == null)
                continue;
            if (!any)
            {
                sum = all[i].bounds;
                any = true;
            }
            else
                sum.Encapsulate(all[i].bounds);
        }
        return sum;
    }

    private void BuildGroundStrip(Transform root)
    {
        LevelPiece piece = default(LevelPiece);
        piece.name = "GroundStrip";
        piece.center = new Vector2((groundLeft + groundRight) * 0.5f, groundTopY - groundHeight * 0.5f);
        piece.size = new Vector2(groundRight - groundLeft, groundHeight);
        piece.rotationZ = 0f;
        piece.color = Color.white;
        piece.kind = "";
        piece.hasCollider = true;

        GameObject strip = StaticPlatform.Spawn(piece, root);
        Unlit(strip);
        Add(root, "ground strip (dirt fill + grass cap)", "Level/ground_fill.png + Level/ground_cap.png",
            LevelArt.HasFill() && LevelArt.HasCap(), "StaticPlatform.Spawn, Tiled draw mode, 8 m fill period");
    }

    private void BuildLevelRow(Transform root)
    {
        float x = 3f;
        RowTitle(root, "LEVEL ART  (Assets/Resources/Level - the shipping ground, sky and finish sign)", x);

        // Sky swatch at its native size, so the period on screen is the real one.
        Sprite sky = LevelArt.Sky();
        if (sky != null)
        {
            GameObject go = Swatch(root, "SkySwatch", LevelArt.SkyKey, new Vector2(12f, 4f), new Vector2(x + 6f, 2f));
            // Deliberately at native 16x4 m: the sky is a ramp, not a tile.
            SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
            sr.sprite = sky;
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.size = new Vector2(12f, 4f);
            ItemLabel(go.transform, "sky  " + SkySize(sky));
            Add(root, "sky", "Level/sky.png", true, SkySize(sky) + " native, Sliced");
            x += 13f;
        }
        else
        {
            MissingTile(root, "sky", "Level/sky.png", new Vector2(4f, 4f), new Vector2(x + 2f, 2f));
            Add(root, "sky", "Level/sky.png", false, "LevelArt.Sky() null");
            x += 5f;
        }

        // Finish sign, exactly as StaticPlatform draws it.
        Sprite sign = LevelArt.Sign();
        if (sign != null)
        {
            LevelPiece piece = default(LevelPiece);
            piece.name = "FinishSign";
            piece.center = new Vector2(x + 0.4f, 1.6f);
            piece.size = new Vector2(0.64f, 3.2f);
            piece.color = Color.white;
            piece.kind = LevelArt.SignKey;
            piece.hasCollider = false;
            GameObject go = StaticPlatform.Spawn(piece, root);
            Unlit(go);
            ItemLabel(go.transform, "sign_finish  0.64 x 3.2 m");
            Add(root, "finish sign", "Level/sign_finish.png", true, "0.64x3.2 m native");
            x += 1.6f;
        }
        else
        {
            MissingTile(root, "finish sign", "Level/sign_finish.png", new Vector2(1f, 3.2f), new Vector2(x + 0.5f, 1.6f));
            Add(root, "finish sign", "Level/sign_finish.png", false, "LevelArt.Sign() null");
            x += 2f;
        }

        ScaleBarAt(root, x, "level");
        RowBounds["level"] = new Bounds(new Vector3(x * 0.5f + 6f, 2f, 0f), new Vector3(28f, 8f, 0f));
    }

    private void BuildMaterialRow(Transform root)
    {
        float x = 20f;
        RowTitle(root, "MATERIALS  (Assets/Resources/Art - one 1 m swatch each, Tiled at 0.5 m)", x);

        for (int i = 0; i < MaterialKeys.Length; i++)
        {
            string key = MaterialKeys[i];
            Vector2 size = new Vector2(1f, 1f);
            Vector2 center = new Vector2(x + i * 1.35f, 0.5f);
            Sprite tile = ArtLibrary.Tiled(key, 0.5f);
            if (tile == null)
            {
                MissingTile(root, key, "Art/" + key + ".png", size, center);
                Add(root, "material " + key, "Art/" + key + ".png", false, "ArtLibrary.Tiled null");
                continue;
            }

            GameObject go = Swatch(root, "Material_" + key, key, size, center);
            SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
            sr.sprite = tile;
            sr.drawMode = SpriteDrawMode.Tiled;
            sr.tileMode = SpriteTileMode.Continuous;
            sr.size = size;
            ItemLabel(go.transform, key + "  1 m, tile 0.5 m");
            Add(root, "material " + key, "Art/" + key + ".png", true, "1 m swatch, 0.5 m repeat");
        }

        ScaleBarAt(root, x, "materials");
        RowBounds["materials"] = new Bounds(new Vector3(x + 4f, 0.6f, 0f), new Vector3(12f, 2.6f, 0f));
    }

    private void BuildTreeRow(Transform root)
    {
        float x = 40f;
        RowTitle(root, "TREE KINDS  (PlantTree, rig Static = the Level1 picture rig)", x);

        for (int i = 0; i < TreeRow.Length; i++)
        {
            TreeKind kind = TreeRow[i];
            GameObject go = new GameObject("Tree_" + kind);
            go.transform.SetParent(root, false);
            go.transform.position = new Vector2(x + i * 3.4f, PlantTree.RootY(groundTopY));

            PlantTree tree = go.AddComponent<PlantTree>();
            tree.kind = kind;
            tree.seed = 1 + i * 17;
            tree.rig = TreeRig.Static;
            tree.BuildTree();

            Unlit(go);
            Dress(go.transform, ArtLibrary.Bark, ArtLibrary.Leaf, "");
            ItemLabel(go.transform, kind.ToString().ToLowerInvariant() + "  " + TreeHeightText(go.transform));
            Bounds treeBox;
            if (PhotoCapture.TryRendererBounds(go.transform, out treeBox))
                RowBounds["tree_" + kind.ToString().ToLowerInvariant()] = treeBox;
            Add(root, kind.ToString().ToLowerInvariant() + " tree", "Art/bark.png + Art/leaf.png", true,
                "PlantTree kind " + kind + ", Static rig");
        }

        ScaleBarAt(root, x - 8f, "trees");
        RowBounds["trees"] = new Bounds(new Vector3(x + 6.8f, 2.6f, 0f), new Vector3(20f, 9f, 0f));
    }

    private void BuildAppleRow(Transform root)
    {
        float x = 60f;
        RowTitle(root, "APPLE  (AppleTree - the apple lane's rig, crown 2-5 m from the seed)", x);

        float[] heights = { 2f, 5f };
        for (int i = 0; i < heights.Length; i++)
        {
            GameObject go = new GameObject("AppleTree_" + heights[i].ToString("0") + "m");
            go.transform.SetParent(root, false);
            go.transform.position = new Vector2(x + i * 4.5f, PlantTree.RootY(groundTopY));

            AppleTree apple = go.AddComponent<AppleTree>();
            apple.seed = 7;
            apple.appleHeightM = heights[i];
            apple.Build();

            Unlit(go);
            Dress(go.transform, ArtLibrary.Bark, ArtLibrary.Leaf, ArtParts.FruitKey);
            ItemLabel(go.transform, "apple " + heights[i].ToString("0") + " m  " + TreeHeightText(go.transform));
            Add(root, "apple tree " + heights[i].ToString("0") + " m", "Art/bark.png + Art/leaf.png + Art/Apple/fruit.png",
                true, "AppleTree.appleHeightM forced to " + heights[i].ToString("0") + " m");
        }

        ScaleBarAt(root, x - 6f, "apple");
        RowBounds["apple"] = new Bounds(new Vector3(x + 2.2f, 3f, 0f), new Vector3(12f, 10f, 0f));
    }

    private void BuildBerryRow(Transform root)
    {
        float x = 70f;
        RowTitle(root, "BERRY / FLOWER", x);

        GameObject berry = new GameObject("Strawberry");
        berry.transform.SetParent(root, false);
        berry.transform.position = new Vector2(x, PlantStrawberry.RootY(groundTopY));
        PlantStrawberry bush = berry.AddComponent<PlantStrawberry>();
        bush.seed = 3;
        bush.BuildBush();
        Unlit(berry);
        Dress(berry.transform, ArtParts.StrawberryStemKey, ArtParts.StrawberryLeafKey, ArtParts.BerryKey);
        ItemLabel(berry.transform, "strawberry  bush " + bush.BushHeight.ToString("0.00") + " m, "
            + bush.BerryCount + " berries");
        Add(root, "strawberry", "Art/Strawberry/berry.png + leaf.png + stem.png",
            ArtLibrary.HasArt(ArtParts.BerryKey), "PlantStrawberry.BuildBush, " + bush.BerryCount + " berries");

        // Real flower art (ART-0 gap 3, 20.09): four kinds at real size, the same bloom
        // the shipping scene draws. Until this pass every petal was the flat collider box,
        // so this row was two labelled gaps; PlantFlower now dresses its own petals from
        // Resources/Art/Flower/ (grey canvases - the kind's colour multiplies them) and
        // the row is the catalogue's own flower bed, one step right of the berry.
        // Six entries on 20.09 (24th pass): the four summer kinds, plus the LAVENDER (the only
        // kind whose rig builds `Spike_i` parts) and one AUTUMN ROSE (the only kind that draws
        // the spent hip in this row). Both new entries sit BETWEEN the summer kinds on purpose:
        // the row's `RowBounds` - and so the camera the `flower` still is taken with - is
        // unchanged, which is what makes this pass' before/after pair the same camera.
        FlowerKind[] flowerRow =
        {
            FlowerKind.Rose, FlowerKind.Lavender, FlowerKind.Daisy,
            FlowerKind.Tulip, FlowerKind.Rose, FlowerKind.Sunflower
        };
        float[] flowerAt = { 2.300f, 2.725f, 3.150f, 4.000f, 4.425f, 4.850f };
        PlantSeason[] flowerSeason =
        {
            PlantSeason.Summer, PlantSeason.Summer, PlantSeason.Summer,
            PlantSeason.Summer, PlantSeason.Autumn, PlantSeason.Summer
        };
        string[] flowerName =
        {
            "rose", "lavender (spike)", "daisy",
            "tulip", "rose (autumn hip)", "sunflower"
        };
        int flowerParts = 0;
        for (int i = 0; i < flowerRow.Length; i++)
        {
            GameObject flower = new GameObject("Flower_" + flowerRow[i] + "_" + i);
            flower.transform.SetParent(root, false);
            flower.transform.position = new Vector2(x + flowerAt[i], PlantFlower.RootY(groundTopY));
            PlantFlower plant = (PlantFlower)flower.AddComponent(PlantFlower.ComponentType(flowerRow[i]));
            plant.seed = 11 + i;   // a different lean and height per kind, as the yard gives them
            plant.season = flowerSeason[i];
            plant.BuildFlower();
            Unlit(flower);
            // The label rides a LEVEL anchor, not the flower: the flower's own transform
            // carries the picture lean (stemLean), and text parented to it came out tilted
            // and ran into the next flower's label.
            GameObject labelHost = new GameObject("FlowerLabel_" + i);
            labelHost.transform.SetParent(root, false);
            labelHost.transform.position = flower.transform.position;
            ItemLabel(labelHost.transform, flowerName[i]);
            flowerParts += ArtParts.DressFlower(flower.transform, true);
        }
        Add(root, "flower bed  (rose, lavender, daisy, tulip, autumn rose, sunflower)",
            "Art/Flower/petal.png + petal_tip.png + cup.png + calyx.png + disk.png"
            + " + leaf.png / leaf_erect.png + spike.png + hip.png",
            ArtLibrary.HasArt(ArtParts.FlowerPetalKey),
            "PlantFlower.BuildFlower dresses its own petals, foliage, spikes, hip and stem ("
            + flowerParts + " parts on the row): the lavender spike, the autumn hip and the stem are"
            + " painted canvases now - Art/Flower/spike.png, hip.png, stem.png and stem_stout.png"
            + " (ART-0 gap 3b closed)");

        ScaleBarAt(root, x - 1.4f, "berry");
        RowBounds["berry"] = new Bounds(new Vector3(x + 1.35f, 0.8f, 0f), new Vector3(7.6f, 3.4f, 0f));
        // The flower bed gets its own, tighter bound: at the berry lens a 0.03 m petal is
        // 3 px, and the whole point of this pass is what those 10-30 px look like.
        RowBounds["flower"] = new Bounds(new Vector3(x + 3.58f, 0.34f, 0f), new Vector3(2.95f, 1.05f, 0f));
    }

    private void BuildHumanRow(Transform root)
    {
        float x = 78f;
        RowTitle(root, "HUMAN  (part sprites imported by ART-1a; rig binding = ART-1b, walk stream)", x);

        // Shipping collider boxes from Human.cs, and the part file the human
        // lane will bind. Drawn at the box, the same rectangle the collider is.
        string[] parts = { "head", "neck", "torso", "pelvis", "upper", "lower", "hand", "thigh", "shin", "foot" };
        Vector2[] boxes =
        {
            new Vector2(0.20f, 0.23f), new Vector2(0.12f, 0.09f), new Vector2(0.24f, 0.34f),
            new Vector2(0.24f, 0.16f), new Vector2(0.10f, 0.33f), new Vector2(0.08f, 0.34f),
            new Vector2(0.06f, 0.16f), new Vector2(0.16f, 0.43f), new Vector2(0.11f, 0.43f),
            new Vector2(0.26f, 0.09f)
        };

        for (int i = 0; i < parts.Length; i++)
        {
            string key = "Human/" + parts[i];
            Vector2 box = boxes[i];
            GameObject go = new GameObject("HumanPart_" + parts[i]);
            go.transform.SetParent(root, false);
            go.transform.position = new Vector3(x + i * 0.42f, groundTopY + box.y * 0.5f, 0f);

            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            Sprite tile = ArtLibrary.Tiled(key, Mathf.Max(box.x, box.y));
            bool real = tile != null;
            if (real)
            {
                sr.sprite = tile;
                sr.drawMode = SpriteDrawMode.Sliced;
                sr.size = box;
            }
            else
            {
                sr.sprite = ArtMissing.Checker();
                sr.drawMode = SpriteDrawMode.Tiled;
                sr.tileMode = SpriteTileMode.Continuous;
                sr.size = box;
            }
            sr.color = Color.white;
            sr.sortingOrder = SortingObject;
            SpriteLighting.ApplyUnlit(sr);
            GalleryLabels.Readable(go.transform,
                parts[i] + "  " + box.x.ToString("0.00") + "x" + box.y.ToString("0.00"),
                new Vector3(0f, -box.y * 0.5f - 0.16f, -0.01f), 0.10f, labelColor, SortingLabel);
            Add(root, "human part " + parts[i], "Art/" + key + ".png", real,
                "shipping box " + box.x.ToString("0.00") + "x" + box.y.ToString("0.00") + " m, Sliced (collider contract)");
        }

        ArtMissing.PendingTile(root, "HUMAN RIG", "ART-1b", new Vector2(1.7f, 2.6f),
            new Vector2(x + 5.4f, 1.3f), objectLabelHeight);
        Add(root, "human rig", "ART-1b", false, "binding to HumanSegment belongs to the walk stream");

        ScaleBarAt(root, x - 1.6f, "human");
        RowBounds["human"] = new Bounds(new Vector3(x + 2.1f, 0.6f, 0f), new Vector3(6.6f, 1.8f, 0f));
    }

    private void BuildCreatureRow(Transform root)
    {
        float x = 88f;
        RowTitle(root, "CREATURES  (part sprites imported by ART-1a; rig binding = ART-3)", x);

        string[] creatures = { "dog", "crow", "chicken", "hedgehog" };
        for (int i = 0; i < creatures.Length; i++)
        {
            ArtMissing.PendingTile(root, creatures[i], "ART-3", new Vector2(1.5f, 2.4f),
                new Vector2(x + i * 2.2f, 1.2f), objectLabelHeight);
            Add(root, creatures[i], "Art/" + Capitalise(creatures[i]) + "/*.png imported", false,
                "ART-3 pending: rig binding (unchanged files this cycle)");
        }

        ScaleBarAt(root, x - 1.8f, "creatures");
        RowBounds["creatures"] = new Bounds(new Vector3(x + 3.3f, 1.2f, 0f), new Vector3(10f, 3.4f, 0f));
    }

    // The fire's own picture (ART-0). The row draws the ASSEMBLED fire the yard
    // draws - three log boxes, the ember disc, the 24 tongue discs of the pool and
    // the painted core - straight from Campfire.FrozenStack(): the pool's own
    // geometry, the pool's own sorting bands and the pool's own pose function, so
    // the catalogue cannot drift from the yard (Tools/art0-fire-stack-check.py
    // holds that contract).
    //
    // The sizing device is the yard's: the logs go through HumanSegment.ApplyPicture
    // (the flat translucent box the fire is built from), the ember, the tongues and
    // the core are the 1 m soft disc / the 1 m core sprite scaled to their box, as
    // Campfire.MakeDisc does it. The fire has no collider and no Rigidbody2D, so no
    // physics number is involved; the collider-equals-rectangle contract is what the
    // other rows demonstrate.
    //
    // Frozen on purpose: a live Campfire ticks a Perlin flicker every frame and this
    // scene's captures have to repeat frame for frame. Not drawn for the same
    // reason: the three ParticleSystems (sparks, smoke, embers) and the night glow.
    private void BuildFireRow(Transform root)
    {
        float x = 99f;
        RowTitle(root, "FIRE  (Campfire - the assembled fire)", x);

        GameObject stack = new GameObject("CampfireStack");
        stack.transform.SetParent(root, false);
        stack.transform.position = new Vector3(x, groundTopY, 0f);

        Sprite coreSprite = Campfire.CoreSprite();
        Campfire.FirePiece[] pieces = Campfire.FrozenStack();
        int drawn = 0;
        int lit = 0;
        int logDressed = 0;
        int logSeen = 0;
        string logRefused = null;
        for (int i = 0; i < pieces.Length; i++)
        {
            Campfire.FirePiece piece = pieces[i];
            if (piece.kind == Campfire.FirePieceKind.Core && coreSprite == null)
                continue;

            GameObject go = new GameObject(piece.name);
            go.transform.SetParent(stack.transform, false);
            go.transform.localPosition = piece.local;
            go.transform.localRotation = Quaternion.Euler(0f, 0f, piece.tilt);

            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            if (piece.kind == Campfire.FirePieceKind.Log)
            {
                HumanSegment.ApplyPicture(sr, piece.size, HumanVisualShape.Box, null, piece.color, piece.order);
                // The logs' painted canvases (ART-0 nineteenth pass). Same call the
                // yard's BuildLogs makes, so the catalogue cannot show a different
                // pile: ArtParts.Dress puts the whole canvas Sliced onto the box the
                // log already has, and a missing PNG leaves the flat box.
                // The count is that call's own verdict, not the box: ApplyPicture
                // runs first and leaves a pixel-snapped white body sprite
                // (CreateBodySprite, 256 px/m, 64-px minimum side), so the box
                // Dress reads and writes back sits 0.2-1.0 mm off piece.size and
                // Unity's Vector2 == is false for all three logs by construction.
                // The 20.09 finding: the old test read "logs dressed 0/3" on a
                // frame whose pile window had lost BOTH flat LogWood blends to
                // exactly 0 px. A refused dress returns false and is named.
                logSeen++;
                if (Campfire.DressLog(sr, Campfire.LogIndex(piece.name)))
                    logDressed++;
                else
                    logRefused = logRefused == null ? piece.name : logRefused + "+" + piece.name;
            }
            else
            {
                sr.sprite = piece.kind == Campfire.FirePieceKind.Core ? coreSprite : Campfire.SoftSprite();
                sr.drawMode = SpriteDrawMode.Simple;
                go.transform.localScale = new Vector3(piece.size.x, piece.size.y, 1f);
                sr.color = piece.color;
                sr.sortingOrder = piece.order;
            }

            SpriteLighting.ApplyUnlit(sr);
            sr.enabled = piece.on;
            drawn++;
            if (piece.on)
                lit++;
        }

        // The label goes ABOVE the flame, clear of it: the assembled fire stands
        // 0.75 m tall (measured on the still: the tongue tips at 0.744 m), a centred
        // label at the ground line ran over the logs, the embers and the flame, and
        // the same label 1.05 m up still touched the highest tongue by 1 px. 1.45 m
        // puts its ink at 1.15-1.24 m, a gap of 0.32 m, and it is inside the row's lens.
        GameObject labelHost = new GameObject("FireLabel");
        labelHost.transform.SetParent(stack.transform, false);
        labelHost.transform.localPosition = new Vector3(0f, 1.45f, 0f);
        ItemLabel(labelHost.transform, "campfire (assembled)  logs + ember + "
            + Campfire.TongueCount + " tongue discs + core "
            + Campfire.CoreBoxWidth.ToString("0.00") + " x " + Campfire.CoreBoxHeight.ToString("0.00") + " m");

        if (coreSprite == null)
        {
            MissingTile(root, "campfire flame core", "Art/Campfire/core.png",
                new Vector2(1.4f, 1.6f), new Vector2(x + 1.4f, 0.8f));
            Add(root, "campfire (assembled)", "logs + ember + " + Campfire.TongueCount + " discs, NO core", false,
                "ArtLibrary.Albedo(\"" + Campfire.CoreArtKey + "\") null - the painted core is missing, "
                + "the pool and the ember are procedural by design");
        }
        else
        {
            Add(root, "campfire (assembled)", "Art/Campfire/log_a|b|c.png + core.png + Campfire.SoftSprite() x "
                + (Campfire.TongueCount + 1), true,
                "the fire as the yard draws it: " + drawn + " pieces (" + lit + " lit), logs dressed "
                + logDressed + "/" + logSeen + " by Campfire.DressLog"
                + (logRefused == null ? "" : " (refused: " + logRefused + ")") + ", bands "
                + Campfire.LogOrder + "/" + Campfire.EmberOrder + "/" + Campfire.FireOrder
                + ", pose frozen at " + Campfire.FrozenStackTime.ToString("0.00") + " s, tint "
                + Campfire.CoreTint.r.ToString("0.00") + "," + Campfire.CoreTint.g.ToString("0.00") + ","
                + Campfire.CoreTint.b.ToString("0.00") + " - sparks/smoke/embers and the night glow are "
                + "time-driven and stay out of the catalogue");
        }

        ScaleBarAt(root, x - 1.5f, "fire");
        RowBounds["fire"] = new Bounds(new Vector3(x, 0.8f, 0f), new Vector3(3.2f, 3f, 0f));
    }

    private void BuildMissingRow(Transform root)
    {
        float x = 104f;
        RowTitle(root, "MISSING ART", x);

        // Campfire is no longer in this row: Art/Campfire/core.png landed in
        // ART-0 gap 2 and the fire is drawn for real in BuildFireRow.
        // The flower bed left this row on 20.09: ART-0 gap 3 is delivered as
        // Resources/Art/Flower/* and the bed is drawn for real in BuildBerryRow.
        MissingTile(root, "UI / HUD icons", "no art", new Vector2(1.1f, 1.2f),
            new Vector2(x, 0.6f));
        Add(root, "UI icons", "MISSING", false, "out of scope for the gallery (ART-0 item 5)");

        ScaleBarAt(root, x - 1.5f, "missing");
        RowBounds["missing"] = new Bounds(new Vector3(x + 0.65f, 0.7f, 0f), new Vector3(4.2f, 2.6f, 0f));
    }

    // ─── primitives ───────────────────────────────────────────────────────
    private GameObject Swatch(Transform root, string name, string key, Vector2 size, Vector2 center)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(root, false);
        go.transform.position = new Vector3(center.x, groundTopY + center.y, 0f);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = ArtLibrary.Tiled(key, 0.5f);
        sr.color = Color.white;
        sr.drawMode = SpriteDrawMode.Tiled;
        sr.tileMode = SpriteTileMode.Continuous;
        sr.size = size;
        sr.sortingOrder = SortingObject;
        SpriteLighting.ApplyUnlit(sr);
        return go;
    }

    private void MissingTile(Transform root, string what, string note, Vector2 size, Vector2 center)
    {
        GameObject go = ArtMissing.Tile(root, what, note, size,
            new Vector3(center.x, groundTopY + center.y, 0f), objectLabelHeight);
        galleryRoots.Add(go.transform);
        MissingItemCount++;
    }

    private void RowTitle(Transform root, string text, float x)
    {
        GameObject go = new GameObject("RowTitle");
        go.transform.SetParent(root, false);
        go.transform.position = new Vector3(x, groundTopY + 6.2f, 0f);
        GalleryLabels.Readable(go.transform, text, Vector3.zero, rowTitleHeight, labelColor, SortingLabel);
    }

    private void ItemLabel(Transform host, string text)
    {
        GalleryLabels.Readable(host, text, new Vector3(0f, -labelDrop, -0.01f),
            objectLabelHeight, labelColor, SortingLabel);
    }

    private void ScaleBarAt(Transform root, float x, string key)
    {
        GameObject bar = GalleryLabels.ScaleBar(root, new Vector3(x, groundTopY - 0.85f, 0f), 1f,
            Color.white, SortingLabel, 0.14f);
        RowBounds["bar_" + key] = new Bounds(bar.transform.position, new Vector3(1.4f, 0.6f, 0f));
    }

    private void Dress(Transform root, string wood, string leaf, string fruit, string stem = null)
    {
        DressTarget target = default(DressTarget);
        target.root = root;
        target.wood = wood;
        target.leaf = leaf;
        target.fruit = fruit;
        target.stem = stem;
        dressTargets.Add(target);
        galleryRoots.Add(root);
    }

    private void Add(Transform root, string name, string texture, bool real, string note)
    {
        Item item = default(Item);
        item.name = name;
        item.texture = texture;
        item.real = real;
        item.note = note;
        Inventory.Add(item);
        if (real)
            RealItemCount++;
        else
            MissingItemCount++;
    }

    private static void Unlit(GameObject go)
    {
        if (go == null)
            return;
        SpriteRenderer[] renderers = go.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
            SpriteLighting.ApplyUnlit(renderers[i]);
    }

    private static string SkySize(Sprite sky)
    {
        if (sky == null)
            return "-";
        return sky.bounds.size.x.ToString("0.#") + " x " + sky.bounds.size.y.ToString("0.#") + " m";
    }

    // Height of the tallest painted point under root, above the ground line.
    private static string TreeHeightText(Transform root)
    {
        Bounds bounds;
        if (!PhotoCapture.TryRendererBounds(root, out bounds))
            return "-";
        return (bounds.max.y - 0f).ToString("0.00") + " m tall";
    }

    private static string Capitalise(string raw)
    {
        if (string.IsNullOrEmpty(raw))
            return raw;
        return char.ToUpperInvariant(raw[0]) + raw.Substring(1);
    }
}
