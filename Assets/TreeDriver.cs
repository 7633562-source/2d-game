using UnityEngine;

// One tree FixedUpdate: spring, damping, wind.
// Otherwise TreeSpring / JointFriction order at the same
// DefaultExecutionOrder is undefined — same reason as ActuatorDriver.
[DefaultExecutionOrder(50)]
public class TreeDriver : MonoBehaviour
{
    private PlantTree tree;
    private TreeSpring[] springs = System.Array.Empty<TreeSpring>();
    private JointFriction[] frictions = System.Array.Empty<JointFriction>();

    public void RebuildCache(PlantTree owner)
    {
        tree = owner;
        springs = GetComponentsInChildren<TreeSpring>(true);
        frictions = GetComponentsInChildren<JointFriction>(true);
        System.Array.Sort(springs, CompareByHierarchyPath);
        System.Array.Sort(frictions, CompareByHierarchyPath);

        for (int i = 0; i < springs.Length; i++)
        {
            if (springs[i] != null)
                springs[i].drivenExternally = true;
        }

        for (int i = 0; i < frictions.Length; i++)
        {
            if (frictions[i] != null)
                frictions[i].drivenExternally = true;
        }
    }

    private static int CompareByHierarchyPath(Component a, Component b)
    {
        if (ReferenceEquals(a, b)) return 0;
        if (a == null) return 1;
        if (b == null) return -1;
        return string.CompareOrdinal(GetHierarchyPath(a.transform), GetHierarchyPath(b.transform));
    }

    private static string GetHierarchyPath(Transform t)
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder(64);
        while (t != null)
        {
            sb.Insert(0, t.name);
            sb.Insert(0, '/');
            t = t.parent;
        }
        return sb.ToString();
    }

    void FixedUpdate()
    {
        for (int i = 0; i < springs.Length; i++)
        {
            if (springs[i] != null)
                springs[i].Apply();
        }

        for (int i = 0; i < frictions.Length; i++)
        {
            if (frictions[i] != null)
                frictions[i].Apply();
        }

        if (tree != null)
            tree.ApplyWind();
    }
}
