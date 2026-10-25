using UnityEngine;

// Level 1 look switches (req-342). Every switch is default off: with none of
// them on the game builds and runs exactly as before.
public static class Level1LookFlags
{
    public delegate float FloatArg(string name, float dflt);

    // -level1FenceBack 1: every BackFence* decor piece goes behind the actors.
    public static bool FenceBehind;
    // -level1DogBehind 1: every dog renderer shifts behind the hero.
    public static bool DogBehind;
    // -level1FireBehind 1: the campfire's own bands move behind the actors.
    public static bool FireBehind;

    public static bool Any
    {
        get { return FenceBehind || DogBehind || FireBehind; }
    }

    // Below every actor (hero -8, dog -27..-10), above the clouds (-37).
    public const int BackFenceOrder = -34;
    public const int DogBehindShift = -21;

    public static void Parse(FloatArg get)
    {
        FenceBehind = get("-level1FenceBack", 0f) > 0.5f;
        DogBehind = get("-level1DogBehind", 0f) > 0.5f;
        FireBehind = get("-level1FireBehind", 0f) > 0.5f;
    }

    public static bool IsBackFence(string name)
    {
        return !string.IsNullOrEmpty(name) && name.StartsWith("BackFence", System.StringComparison.Ordinal);
    }
}
