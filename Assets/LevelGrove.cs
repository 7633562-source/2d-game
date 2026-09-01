using UnityEngine;

// Роща на «Дворе». PlantTree коллег не меняем — только посадка.
// Дерево из HumanSegment: у каждого куска свой Rigidbody2D. Если оставить
// Dynamic хоть на один FixedUpdate, 2 м земли выбивает пень импульсом —
// «улетели». Sway в World по-прежнему нельзя.
public static class LevelGrove
{
    public static readonly float[] Xs = { -22f, -11f, 14f, 31f, 54f, 68f };
    public static readonly int[] Seeds = { 3, 19, 37, 53, 71, 89 };

    public static void Plant(Transform levelRoot, Transform human)
    {
        GameObject grove = GameObject.Find("YardGrove");
        if (grove == null)
            grove = new GameObject("YardGrove");

        for (int i = 0; i < Xs.Length; i++)
        {
            float x = Xs[i];
            float groundY = LevelTest1.SurfaceAt(x);
            GameObject go = new GameObject("YardTree_" + (i + 1));
            // Не под Level: чанки и холмы там живут, а у дерева свои RB.
            // worldStay — координаты двора, не локаль родителя.
            go.transform.SetParent(grove.transform, true);
            go.transform.position = new Vector3(x, PlantTree.RootY(groundY), 0f);

            PlantTree tree = go.AddComponent<PlantTree>();
            tree.rig = TreeRig.Static;
            tree.kind = PlantTree.KindFromPlantIndex(i);
            tree.seed = Seeds[i];
            tree.BuildTree();
            go.name = "YardTree_" + (i + 1) + "_" + tree.ResolvedKind;

            PinToYard(tree);
            IgnoreHuman(tree, human);
            // Tree and level colliders are both Static here; Box2D creates no static-static contacts.
        }
    }

    // Пока сегмент Dynamic, гравитация и контакт с площадкой дают разлёт.
    // Заморозка — посадка двора, не смена рига в PlantTree.
    private static void PinToYard(PlantTree tree)
    {
        Rigidbody2D[] bodies = tree.GetComponentsInChildren<Rigidbody2D>(true);
        for (int i = 0; i < bodies.Length; i++)
        {
            Rigidbody2D rb = bodies[i];
            if (rb == null)
                continue;
            if (rb.bodyType != RigidbodyType2D.Static)
            {
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }
            rb.gravityScale = 0f;
            rb.bodyType = RigidbodyType2D.Static;
            rb.constraints = RigidbodyConstraints2D.FreezeAll;
        }
    }

    private static void IgnoreHuman(PlantTree tree, Transform human)
    {
        if (tree == null || human == null)
            return;
        IgnorePairs(
            tree.GetComponentsInChildren<Collider2D>(true),
            human.GetComponentsInChildren<Collider2D>(true));
    }

    private static void IgnorePairs(Collider2D[] a, Collider2D[] b)
    {
        if (a == null || b == null)
            return;
        for (int i = 0; i < a.Length; i++)
        {
            if (a[i] == null)
                continue;
            for (int j = 0; j < b.Length; j++)
            {
                if (b[j] == null)
                    continue;
                Physics2D.IgnoreCollision(a[i], b[j], true);
            }
        }
    }
}
