using UnityEngine;

// Support layer: the foot/hand probe looks only here, not at body segments.
// IgnoreCollision inside the human stays as it was.
public static class GroundLayers
{
    public const string Name = "Ground";
    private const int Unresolved = int.MinValue;
    private static int resolvedIndex = Unresolved;

    public static int Index
    {
        get
        {
            Resolve();
            return resolvedIndex >= 0 ? resolvedIndex : 0;
        }
    }

    public static int Mask => 1 << Index;

    public static void Apply(GameObject go)
    {
        if (go == null) return;
        Resolve();
        if (resolvedIndex >= 0)
            go.layer = resolvedIndex;
    }

    private static void Resolve()
    {
        if (resolvedIndex == Unresolved)
            resolvedIndex = LayerMask.NameToLayer(Name);
    }
}
