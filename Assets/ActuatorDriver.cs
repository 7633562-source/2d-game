using UnityEngine;

// One actuator pass: viscosity first, then muscles.
// Otherwise with the same DefaultExecutionOrder(50) the FixedUpdate
// order between JointFriction and Muscle is undefined.
[DefaultExecutionOrder(50)]
public class ActuatorDriver : MonoBehaviour
{
    private JointFriction[] frictions = System.Array.Empty<JointFriction>();
    private Muscle[] muscles = System.Array.Empty<Muscle>();

    // World off-camera rest. NpcRest writes this. Skip the 200 Hz pass.
    [System.NonSerialized]
    public bool energyRest;

    // Collect child actuators after the body is built.
    public void RebuildCache()
    {
        frictions = GetComponentsInChildren<JointFriction>(true);
        muscles = GetComponentsInChildren<Muscle>(true);
        // Hierarchy names give a stable order; GetComponentsInChildren
        // does not formally promise order across runs.
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
        if (energyRest) return;

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
