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
