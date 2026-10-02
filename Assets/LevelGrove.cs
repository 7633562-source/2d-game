using UnityEngine;

// Yard grove. Static trees are picture only. Sit pads appear only if
// a bird later calls EnsureBirdPerches. Sway in World is forbidden.
public static class LevelGrove
{
    public static readonly float[] Xs =
    {
        -22f, -11f, 14f, 31f,
        37.5f, 44.2f, 52f,
        54f, 68f
    };
    public static readonly int[] Seeds = { 3, 19, 37, 53, 11, 29, 61, 71, 89 };

    // Level1 drawn tableau. Apple is Static (not treeCount / Sway).
    // Player at −8, camera ortho 5 → half-width ≈ 8.9 m, so the opening
    // shot is about −17…+1. Courtyard Xs 32–38 spawn but sit off-screen,
    // and Campfire.CullX 18 also clears the flame. Keep the patch in view.
    // One woodpecker sits on the apple; the 20 crows stay GameProcess.birdCount.
    public const float AppleX = 0f;
    public const float DrawnCampfireX = -4.6f;
    public static readonly float[] StrawberryXs = { -2.8f, -2.0f, -1.2f };
    public const float HedgehogX = -6.4f;

    public static PlantTree[] Planted { get; private set; }
    public static PlantTree Apple { get; private set; }

    public static void Plant(Transform levelRoot, Transform human, bool drawnLevel)
    {
        // Signature stays for LevelGenerator. The grove is picture-only.
        _ = levelRoot;
        _ = human;

        // Domain reload is off in Play: clear leftovers from the previous scene.
        Apple = null;
        Planted = null;

        GameObject grove = GameObject.Find("YardGrove");
        if (grove == null)
            grove = new GameObject("YardGrove");

        if (drawnLevel)
            PlantDrawnLevel(grove.transform);
        else
            PlantShowcaseGrove(grove.transform);
    }

    private static void PlantShowcaseGrove(Transform parent)
    {
        PlantTree[] planted = new PlantTree[Xs.Length];
        for (int i = 0; i < Xs.Length; i++)
            planted[i] = PlantYardTree(parent, i);
        Planted = planted;
    }

    private static void PlantDrawnLevel(Transform parent)
    {
        Apple = PlantApple(parent);
        Planted = new[] { Apple };
        PlantStrawberries(parent);
        PlantHedgehog();
        PlantWoodpecker();
    }

    private static PlantTree PlantYardTree(Transform parent, int i)
    {
        float x = Xs[i];
        float groundY = LevelTest1.SurfaceAt(x);
        GameObject go = new GameObject("YardTree_" + (i + 1));
        // Stay in world space, not under Level chunks.
        go.transform.SetParent(parent, true);
        go.transform.position = new Vector3(x, PlantTree.RootY(groundY), 0f);

        PlantTree tree = go.AddComponent<PlantTree>();
        tree.rig = TreeRig.Static;
        tree.kind = PlantTree.KindFromPlantIndex(i);
        tree.seed = Seeds[i];
        tree.BuildTree();
        go.name = "YardTree_" + (i + 1) + "_" + tree.ResolvedKind;
        return tree;
    }

    private static PlantTree PlantApple(Transform parent)
    {
        float groundY = LevelTest1.SurfaceAt(AppleX);
        GameObject go = new GameObject("YardApple");
        go.transform.SetParent(parent, true);
        go.transform.position = new Vector3(AppleX, PlantTree.RootY(groundY), 0f);

        PlantTree tree = go.AddComponent<PlantTree>();
        tree.rig = TreeRig.Static;
        tree.kind = TreeKind.Apple;
        tree.seed = 101;
        tree.BuildTree();
        go.name = "YardApple_" + tree.ResolvedKind;
        return tree;
    }

    private static void PlantStrawberries(Transform parent)
    {
        for (int i = 0; i < StrawberryXs.Length; i++)
        {
            float x = StrawberryXs[i];
            float groundY = LevelTest1.SurfaceAt(x);
            GameObject go = new GameObject("YardStrawberry_" + (i + 1));
            go.transform.SetParent(parent, true);
            go.transform.position = new Vector2(x, PlantStrawberry.RootY(groundY));
            PlantStrawberry bush = go.AddComponent<PlantStrawberry>();
            bush.seed = 21 + i * 7;
            bush.BuildBush();
        }
    }

    // ─── CLEARING-1: the clearing's own props ─────────────────────────────
    // The clearing scene (Assets/Scenes/Clearing.unity) stands on the same
    // Test1 tape as World / Level1. LevelGenerator plants the showcase Static
    // grove for it and then calls this: the yard's own berry bushes, one bed of
    // the yard's own flowers (PlantFlower rigs - the yard's code, the art that
    // already ships) and the clearing's own tree. Nothing repainted.
    public static void PlantClearingProps(Transform parent)
    {
        PlantStrawberries(parent);
        PlantClearingFlowerBed(parent);
        PlantClearingTree(parent);
    }

    // The bed: one row east of the berry bushes, between them and the spawn
    // lamp post (3.25). The kinds are the catalogue's own flower bed - rose,
    // lavender, daisy, crocus, tulip, rose in autumn, sunflower - so the first
    // planted bed in the game is exactly the set the flower art pass painted.
    public const float ClearingFlowerFirstX = -0.15f;
    public const float ClearingFlowerSpacing = 0.50f;
    private static readonly FlowerKind[] ClearingFlowerKinds =
    {
        FlowerKind.Rose, FlowerKind.Lavender, FlowerKind.Daisy,
        FlowerKind.Crocus, FlowerKind.Tulip, FlowerKind.Rose, FlowerKind.Sunflower
    };
    private const int ClearingAutumnBloom = 5;

    private static void PlantClearingFlowerBed(Transform parent)
    {
        for (int i = 0; i < ClearingFlowerKinds.Length; i++)
        {
            FlowerKind kind = ClearingFlowerKinds[i];
            float x = ClearingFlowerFirstX + i * ClearingFlowerSpacing;
            float groundY = LevelTest1.SurfaceAt(x);
            GameObject go = new GameObject("ClearingFlower_" + (i + 1) + "_" + kind);
            go.transform.SetParent(parent, true);
            go.transform.position = new Vector2(x, PlantFlower.RootY(groundY));

            PlantFlower flower = (PlantFlower)go.AddComponent(PlantFlower.ComponentType(kind));
            flower.kind = kind;
            flower.seed = 11 + i * 7;
            if (i == ClearingAutumnBloom)
                flower.season = PlantSeason.Autumn;
            flower.BuildFlower();
        }
    }

    // The clearing's own tree: ONE dynamic species tree (TreeRig.Sway) that
    // joins the Static grove. CLEARING-1 allows it only while the frame budget
    // holds it; the capture writes the measured cost to
    // Trials/Runs/world_perf_capture_Clearing.json and the report quotes it.
    // The oak stands 3 m west of the grove tree at -11, on the clearing's west
    // edge, in the middle-fence bay.
    public const float ClearingTreeX = -14f;
    private const int ClearingTreeSeed = 7;

    private static PlantTree PlantClearingTree(Transform parent)
    {
        float groundY = LevelTest1.SurfaceAt(ClearingTreeX);
        GameObject go = new GameObject("ClearingTree_Oak");
        go.transform.SetParent(parent, true);
        go.transform.position = new Vector3(ClearingTreeX, PlantTree.RootY(groundY), 0f);

        PlantTree tree = go.AddComponent<PlantTree>();
        tree.rig = TreeRig.Sway;
        tree.kind = TreeKind.Oak;
        tree.seed = ClearingTreeSeed;
        tree.BuildTree();
        go.name = "ClearingTree_" + tree.ResolvedKind;
        return tree;
    }

    private static void PlantHedgehog()
    {
        // Ragdoll stays in world space, not under the picture grove.
        float groundY = LevelTest1.SurfaceAt(HedgehogX);
        Hedgehog.Spawn(
            new Vector2(HedgehogX, Hedgehog.StandingRootY(groundY)),
            "YardHedgehog");
    }

    private static void PlantWoodpecker()
    {
        // Drawn tableau only. Not GameProcess.birdKind and not the crow flock.
        // Seat on a later apple pad: SeatBirdsOnYardGrove later fills 0…19.
        if (Apple == null)
            return;

        float groundY = LevelTest1.SurfaceAt(AppleX);
        GameObject go = new GameObject("YardWoodpecker");
        Bird pecker = Bird.Attach(go, BirdKind.Woodpecker, BirdRig.Flock);
        go.transform.position = new Vector2(AppleX, pecker.StandingRootOffset(groundY));
        pecker.BuildBird();
        FactionStamp.Bird(go);
        if (pecker.controller != null)
        {
            pecker.controller.mode = BirdMode.Sit;
            pecker.controller.takeoffDelay = 0f;
            pecker.controller.SetFacing(1f);
        }

        BirdDrive drive = go.AddComponent<BirdDrive>();
        drive.kind = BirdDrive.Kind.Wander;
        drive.wanderSeed = 41;

        Bird[] pack = { pecker };
        Bird.GhostTreeWood(pack, Apple);
        Vector2[] slots = Apple.GetPerchSlots();
        if (slots != null && slots.Length > 0)
        {
            int padN = Mathf.Min(slots.Length, PlantTree.MaxAppleBirdPerches);
            pecker.PlaceOnSlot(slots[padN - 1], 1f);
        }
        else
            PlantTree.SeatBirds(pack, Apple);
    }

    // Campfire.EnsureOnYard still builds at Campfire.YardX (World courtyard).
    // Level1 then slides the same object into the spawn camera.
    public static void PlaceDrawnCampfire(Campfire fire)
    {
        if (fire == null)
            return;
        float groundY = LevelTest1.SurfaceAt(DrawnCampfireX);
        fire.transform.position = new Vector3(DrawnCampfireX, groundY, 0f);
    }
}
