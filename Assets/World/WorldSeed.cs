// WORLD-1 (req-285): SplitMix64 derivation of one stream per actor.
// A pure function with no static fields, so an absent -world creates no state at all.
public enum WorldStream : int { World = 0, Hero = 1, Dog = 2, Cat = 3, Crow = 4 }

public static class WorldSeed
{
    public static int Derive(int L, WorldStream k, int index, int salt = 0)
    {
        unchecked
        {
            ulong z = (ulong)(uint)L * 0x9E3779B97F4A7C15UL
                      + ((ulong)(uint)k << 40) + ((ulong)(uint)index << 16) + (uint)salt;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            z ^= z >> 31;
            int s = (int)(z & 0x7FFFFFFF);
            return s == 0 ? 1 : s;   // never 0, never int.MinValue
        }
    }
}
