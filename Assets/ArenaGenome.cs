using System.Globalization;
using System.Text;
using UnityEngine;

// ARENA-1 (Yury's parameter-sweep arena, 18.09.2026).
//
// The genome is exactly the 13 pose-drive walk parameters of PoseDriveController.
// The bodies must stay comparable, so nothing else is swept: rig geometry, masses,
// contact materials, muscle ceilings, gravity and fixedDelta keep the rig's own
// values and the arena never touches them. See .cursor/tasks/ARENA-1-param-sweep.md.
//
// Evolution principle implemented here (and written into every run summary):
//   fitness   = distance walked at 15 s minus a fall penalty (FallPenaltyMeters)
//   selection = tournament among the BEST QUARTILE of the measured humans
//   mutation  = jitter each gene by 5-10 pct of its own range
//   fresh     = 25 pct of spawns draw a fully random genome
//   elitism   = the best genome of the run is re-spawned unchanged once the log
//               holds 20 or more measured humans (that copy is the collision proof:
//               same genome, same spawn point, different age)
// Every child logs its parent id and its mutation seed, so inheritance is provable
// from the log and the jitter is reproducible from the seed alone.
//
// Deterministic: the only generator is ArenaRng (xorshift64*), seeded from
// -arenaSeed. UnityEngine.Random is never used here, and no other unseeded
// source exists, so the same seed replays the same genome sequence.
public static class ArenaGenome
{
    public const int Count = 13;

    // A fall costs this much of the fitness: 1 m of walking. Separation (do not
    // walk) stays possible but costly, which is what the sweep wants to learn.
    public const float FallPenaltyMeters = 1.0f;

    // Names are the CSV column names and the JSON keys. The ORDER IS A CONTRACT:
    // Trials/Runs/arena-param-log.csv is append-only, so a new gene is appended
    // at the end and never inserted in the middle.
    public static readonly string[] Names =
    {
        "swingDuration", "stanceDuration", "swingLiftMeters", "swingLandShare",
        "heightGainDegPerMeter", "heightDampDegPerMeterPerSecond",
        "placementMarginMeters", "comLeadSwapMeters",
        "stepMinMeters", "stepMaxMeters", "armSwingDeg",
        "ankleBalanceP", "ankleBalanceD",
    };

    // The search box. Every value written into a body is clamped to it, so a
    // jittered copy can never leave the box the command-line switches accept.
    public static readonly float[] Min =
    {
        0.15f, 0.15f, 0.05f, 0.40f, 100f, 0f, 0f, 0.05f, 0f, 0.20f, 0f, 0.2f, 0.2f,
    };

    public static readonly float[] Max =
    {
        0.60f, 0.80f, 0.40f, 0.95f, 800f, 80f, 0.12f, 0.35f, 0.10f, 0.70f, 45f, 2.0f, 2.0f,
    };

    // Defaults are the values the walk was verified on, so generation 0 is never
    // an unexplored corner of the box.
    public static readonly float[] Defaults =
    {
        0.35f, 0.45f, 0.22f, 0.80f, 400f, 25f, 0.04f, 0.16f, 0.02f, 0.45f, 22f, 1.0f, 1.0f,
    };

    // Appended tail of the influence-log contract: nothing. The CSV keeps the
    // fixed 10 meta columns plus the 13 genes; every extra proof (mutation seed,
    // parent quartile, trajectory) lives in the run JSON, not in this log.
    public static string CsvHeader()
    {
        StringBuilder sb = new StringBuilder(256);
        for (int i = 0; i < Count; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append(Names[i]);
        }
        return sb.ToString();
    }

    public static string CsvValues(float[] g)
    {
        StringBuilder sb = new StringBuilder(256);
        for (int i = 0; i < Count; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append((i < g.Length ? g[i] : 0f).ToString("0.######", CultureInfo.InvariantCulture));
        }
        return sb.ToString();
    }

    public static string ToJson(float[] g)
    {
        StringBuilder sb = new StringBuilder(320);
        sb.Append('{');
        for (int i = 0; i < Count; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append('"').Append(Names[i]).Append("\":");
            sb.Append(g[i].ToString("0.######", CultureInfo.InvariantCulture));
        }
        sb.Append('}');
        return sb.ToString();
    }

    public static string RangesJson()
    {
        StringBuilder sb = new StringBuilder(512);
        sb.Append('{');
        for (int i = 0; i < Count; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append('"').Append(Names[i]).Append("\":{\"min\":");
            sb.Append(Min[i].ToString("0.######", CultureInfo.InvariantCulture));
            sb.Append(",\"max\":");
            sb.Append(Max[i].ToString("0.######", CultureInfo.InvariantCulture));
            sb.Append('}');
        }
        sb.Append('}');
        return sb.ToString();
    }

    // A fresh genome: uniform over the box. Not the defaults - the whole point of
    // the 25 pct fresh share is to keep visiting the box the jitter cannot reach.
    public static float[] Random(ArenaRng rng)
    {
        float[] g = new float[Count];
        for (int i = 0; i < Count; i++)
            g[i] = rng.Range(Min[i], Max[i]);
        Clamp(g);
        return g;
    }

    public static float[] Copy(float[] src)
    {
        float[] g = new float[Count];
        for (int i = 0; i < Count && i < src.Length; i++)
            g[i] = src[i];
        return g;
    }

    // Jitter every gene by shareMin..shareMax of its OWN range, then clamp. The
    // step size is drawn per gene, so a child is never a uniform translation of
    // its parent.
    public static void Jitter(float[] g, ArenaRng rng, float shareMin = 0.05f, float shareMax = 0.10f)
    {
        for (int i = 0; i < Count; i++)
        {
            float range = Max[i] - Min[i];
            float share = rng.Range(shareMin, shareMax);
            float step = range * share * (rng.Next01() * 2f - 1f);
            g[i] += step;
        }
        Clamp(g);
    }

    public static void Clamp(float[] g)
    {
        for (int i = 0; i < Count; i++)
            g[i] = i < g.Length ? Mathf.Clamp(g[i], Min[i], Max[i]) : Defaults[i];
        // Constraint from the card: the step can never be longer than the box.
        if (g[9] < g[8] + 0.01f)
            g[9] = Mathf.Clamp(g[8] + 0.01f, Min[9], Max[9]);
        if (g[8] > g[9])
            g[8] = Mathf.Max(Min[8], g[9] - 0.01f);
    }
}

// xorshift64* - the arena's only random source. It is version independent, so the
// genome sequence of a seed cannot drift with a .NET or Unity upgrade, which
// System.Random cannot promise.
public struct ArenaRng
{
    private ulong state;

    public ArenaRng(int seed)
    {
        // SplitMix64 of the seed: neighbouring seeds must not produce
        // neighbouring streams.
        ulong z = (ulong)(uint)seed + 0x9E3779B97F4A7C15UL;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        state = (z ^ (z >> 31)) | 1UL;
    }

    public ulong NextUInt64()
    {
        ulong x = state;
        x ^= x >> 12;
        x ^= x << 25;
        x ^= x >> 27;
        state = x;
        return x * 0x2545F4914F6CDD1DUL;
    }

    public uint NextUInt()
    {
        return (uint)(NextUInt64() >> 32);
    }

    public float Next01()
    {
        return (NextUInt() >> 8) * (1f / 16777216f);   // 24 bits, in [0,1)
    }

    public float Range(float lo, float hi)
    {
        return lo + (hi - lo) * Next01();
    }

    public int NextInt(int maxExclusive)
    {
        if (maxExclusive <= 1) return 0;
        return (int)(NextUInt() % (uint)maxExclusive);
    }

    public int NextSeed()
    {
        return (int)(NextUInt() & 0x7FFFFFFF);
    }
}
