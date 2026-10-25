using UnityEngine;

// WORLD-1 (req-285): the world's own sound bus. Separate from the old static
// SoundBus: this one is owned by the host, step-stamped and deterministic.
public enum SoundKind : byte { Call = 0, Alarm = 1, Alarm2 = 2, Bark = 3, Mob = 4, Steal = 5 }

public struct WorldSoundEvent
{
    public int step;
    public float x, y;
    public SoundKind kind;
    public float radius;
    public int src;
}

public sealed class WorldSoundBus
{
    public const int Cap = 64;
    readonly int[] step = new int[Cap];
    readonly float[] px = new float[Cap];
    readonly float[] py = new float[Cap];
    readonly float[] radius = new float[Cap];
    readonly byte[] kind = new byte[Cap];
    readonly int[] src = new int[Cap];
    int write;
    int count;

    public int Count { get { return count; } }

    public static float RadiusOf(SoundKind k)
    {
        switch (k)
        {
            case SoundKind.Call: return 30f;
            case SoundKind.Alarm: return 10f;
            case SoundKind.Alarm2: return 10f;
            case SoundKind.Bark: return 8f;
            case SoundKind.Mob: return 12f;
            default: return 12f;   // Steal
        }
    }

    public static string NameOf(SoundKind k)
    {
        switch (k)
        {
            case SoundKind.Call: return "call";
            case SoundKind.Alarm: return "alarm";
            case SoundKind.Alarm2: return "alarm2";
            case SoundKind.Bark: return "bark";
            case SoundKind.Mob: return "mob";
            default: return "steal";
        }
    }

    public void Post(int stepNow, Vector2 p, SoundKind k, int sourceId)
    {
        step[write] = stepNow;
        px[write] = p.x;
        py[write] = p.y;
        kind[write] = (byte)k;
        radius[write] = RadiusOf(k);
        src[write] = sourceId;
        write = (write + 1) % Cap;
        if (count < Cap) count++;
    }

    // Sounds stamped in (sinceStepExcl, untilStepExcl) within their radius of p.
    // Stamps strictly below the current step only, so two brains on one step never
    // depend on the order they tick in. Returns the number written to dest.
    public int Heard(int sinceStepExcl, int untilStepExcl, Vector2 p, WorldSoundEvent[] dest)
    {
        int n = 0;
        for (int i = 0; i < count; i++)
        {
            int s = (write - 1 - i + Cap + Cap) % Cap;
            int st = step[s];
            if (st <= sinceStepExcl || st >= untilStepExcl) continue;
            float dx = p.x - px[s];
            float dy = p.y - py[s];
            if (dx * dx + dy * dy > radius[s] * radius[s]) continue;
            if (dest != null && n < dest.Length)
            {
                dest[n].step = st;
                dest[n].x = px[s];
                dest[n].y = py[s];
                dest[n].kind = (SoundKind)kind[s];
                dest[n].radius = radius[s];
                dest[n].src = src[s];
            }
            n++;
            if (dest != null) { if (n >= dest.Length) break; }
        }
        return n;
    }

    // Keeps one second of stamps (Stride steps) and drops the older tail.
    public void Prune(int nowStep, int stride)
    {
        while (count > 0)
        {
            int oldest = (write - count + Cap) % Cap;
            if (nowStep - step[oldest] > stride) count--;
            else break;
        }
    }
}
