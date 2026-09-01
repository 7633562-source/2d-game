using UnityEngine;

// Один проход актюаторов: сначала вязкость, потом мышцы.
// Иначе при одном DefaultExecutionOrder(50) порядок FixedUpdate
// между JointFriction и Muscle не определён.
[DefaultExecutionOrder(50)]
public class ActuatorDriver : MonoBehaviour
{
    private JointFriction[] frictions = System.Array.Empty<JointFriction>();
    private Muscle[] muscles = System.Array.Empty<Muscle>();

    // Собирает дочерние актюаторы после сборки тела.
    public void RebuildCache()
    {
        frictions = GetComponentsInChildren<JointFriction>(true);
        muscles = GetComponentsInChildren<Muscle>(true);
        // Имена в иерархии — стабильный порядок; GetComponentsInChildren
        // формально не обещает порядок между прогонами.
        System.Array.Sort(frictions, CompareByHierarchyPath);
        System.Array.Sort(muscles, CompareByHierarchyPath);

        for (int i = 0; i < frictions.Length; i++)
        {
            if (frictions[i] != null)
                frictions[i].drivenExternally = true;
        }

        for (int i = 0; i < muscles.Length; i++)
        {
            if (muscles[i] != null)
                muscles[i].drivenExternally = true;
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
        for (int i = 0; i < frictions.Length; i++)
        {
            JointFriction friction = frictions[i];
            if (friction != null)
                friction.Apply();
        }

        for (int i = 0; i < muscles.Length; i++)
        {
            Muscle muscle = muscles[i];
            if (muscle != null)
                muscle.Apply();
        }
    }
}
