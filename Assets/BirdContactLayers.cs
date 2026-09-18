using UnityEngine;

// Bird-to-bird contact is torso and head only. Limbs stay on the
// ground and on the human, but they do not generate solver contacts
// against another bird. Layer matrix is applied once, not per pair.
public static class BirdContactLayers
{
    public const string CoreName = "BirdCore";
    public const string LimbName = "BirdLimb";

    private const int Unresolved = int.MinValue;
    private static int coreIndex = Unresolved;
    private static int limbIndex = Unresolved;
    private static bool matrixReady;

    public static bool Ready
    {
        get
        {
            Ensure();
            return coreIndex >= 0 && limbIndex >= 0;
        }
    }

    public static void Ensure()
    {
        if (coreIndex == Unresolved)
            coreIndex = LayerMask.NameToLayer(CoreName);
        if (limbIndex == Unresolved)
            limbIndex = LayerMask.NameToLayer(LimbName);
        if (matrixReady || coreIndex < 0 || limbIndex < 0)
            return;
        Physics2D.IgnoreLayerCollision(limbIndex, limbIndex, true);
        Physics2D.IgnoreLayerCollision(limbIndex, coreIndex, true);
        matrixReady = true;
    }

    public static void ApplyCore(GameObject go)
    {
        if (go == null) return;
        Ensure();
        if (coreIndex >= 0)
            go.layer = coreIndex;
    }

    public static void ApplyLimb(GameObject go)
    {
        if (go == null) return;
        Ensure();
        if (limbIndex >= 0)
            go.layer = limbIndex;
    }

    public static bool IsCore(GameObject go)
    {
        if (go == null) return false;
        Ensure();
        return coreIndex >= 0 && go.layer == coreIndex;
    }
}
