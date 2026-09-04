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

    public static PlantTree[] Planted { get; private set; }

    public static void Plant(Transform levelRoot, Transform human)
    {
        // Signature stays for LevelGenerator. The grove is picture-only.
        _ = levelRoot;
        _ = human;

        GameObject grove = GameObject.Find("YardGrove");
        if (grove == null)
            grove = new GameObject("YardGrove");

        PlantTree[] planted = new PlantTree[Xs.Length];
        for (int i = 0; i < Xs.Length; i++)
        {
            float x = Xs[i];
            float groundY = LevelTest1.SurfaceAt(x);
            GameObject go = new GameObject("YardTree_" + (i + 1));
            // Stay in world space, not under Level chunks.
            go.transform.SetParent(grove.transform, true);
            go.transform.position = new Vector3(x, PlantTree.RootY(groundY), 0f);

            PlantTree tree = go.AddComponent<PlantTree>();
            tree.rig = TreeRig.Static;
            tree.kind = PlantTree.KindFromPlantIndex(i);
            tree.seed = Seeds[i];
            tree.BuildTree();
            go.name = "YardTree_" + (i + 1) + "_" + tree.ResolvedKind;
            planted[i] = tree;
        }

        Planted = planted;
    }
}
