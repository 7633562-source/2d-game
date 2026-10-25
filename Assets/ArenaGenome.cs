using System.Globalization;
using System.Text;
using UnityEngine;

// ARENA-1 (Yury's parameter-sweep arena, 18.09.2026).
//
// The genome is the 13 pose-drive walk parameters of PoseDriveController plus,
// since WALK CYCLE 64, the 3 ASK-SIDE genes (the drawn stance leg's knee at the
// SwingFold / FootPlace / PlantLoad keys - the pose itself, made searchable).
// The bodies must stay comparable, so nothing else is swept: rig geometry, masses,
// contact materials, muscle ceilings, gravity and fixedDelta keep the rig's own
// values and the arena never touches them. See .cursor/tasks/ARENA-1-param-sweep.md.
//
// Evolution principle implemented here (and written into every run summary):
//   fitness   = distance_at_min(15, fallTime) + 0.1 * min(fallTime, 15)
//               - 3.0 * [fallTime < 15]      (WALK CYCLE 53; stand reward cut in
//               WALK CYCLE 54 from 0.4 to 0.1 - standing is worth 1.5 m, not 6.0)
//   selection = tournament of 5 among the BEST THIRD of a SLIDING WINDOW of the
//               last SelectionWindowMeasured measured humans (WALK CYCLE 53)
//   pre-filter= distance15 < 1.5 m (WALK CYCLE 54; was 0.3), or a non-monotone
//               path, never enters selection
//   mutation  = ONE family per child at p=0.7 (coupled members share one signed
//               share), the whole genome at p=0.3; 5-10 pct of each gene's range,
//               3-5 pct once the best-third mean stops rising
//   fresh     = 25 pct of spawns draw a fully random genome
//   elitism   = the best ELIGIBLE genome of the window is re-spawned unchanged
//               every EliteEverySpawns spawns (that copy is the collision proof:
//               same genome, same spawn point, different age)
// Every child logs its parent id, its mutation seed and WHICH FAMILY moved, so
// inheritance is provable from the log and the jitter is reproducible from the
// seed alone.
//
// Deterministic: the only generator is ArenaRng (xorshift64*), seeded from
// -arenaSeed. UnityEngine.Random is never used here, and no other unseeded
// source exists, so the same seed replays the same genome sequence.
public static class ArenaGenome
{
    public const int Count = 16;   // 13 loop-side + 3 ask-side (WALK CYCLE 64)

    // WALK CYCLE 53: the OLD law's price - a fall cost 1 m of walking. The fitness
    // no longer uses it (distance_at_min + 0.4*standTime - 3*[fell] replaced the
    // binary penalty); the constant stays in the log so a reader can tell the two
    // laws apart in an older summary.
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
        // WALK CYCLE 64: the ask-side tier - appended, never inserted.
        "stanceFoldKneeDeg", "stancePlaceKneeDeg", "stanceLoadKneeDeg",
    };

    // The search box. Every value written into a body is clamped to it, so a
    // jittered copy can never leave the box the command-line switches accept.
    // INVARIANT, checked offline by Tools/arena-genome-box-audit.py: every
    // Defaults[i] below AND every shipped initialiser of the same field in
    // PoseDriveController lies INSIDE [Min[i], Max[i]] - a box that cannot
    // express the value the tree ships is not a search box, it is a wall, and
    // the influence log cannot show the difference.
    // WALK CYCLE 6r: Min[8] was 0f while the shipped band became -0.45 .. +0.45
    // (PoseDriveController.cs stepMinMeters), so the genome could only ever place
    // the step pivot IN FRONT of the stance foot - the negative half of the band,
    // the half the law now needs to put the pivot behind a CoM falling backwards,
    // was unreachable through this path. Signed genes get a signed box.
    // WALK CYCLE 53: Min[8] = -0.45 still held Defaults[8] ON the edge (the shipped
    // walk's own stepMinMeters is -0.45), so every negative jitter of that gene
    // clamped straight back to the default: a one-sided mutation that no row of the
    // log could show (the log stores the gene the sampler wanted, the body received
    // the wall). The floor moves to -0.65, the shipped -0.45 now sits 0.27 into the
    // range, and AuditDefaultBand() below fails the run if any gene's default ever
    // sits on an edge again. PoseDriveController applies g[8] unclamped (line 849),
    // so nothing between the box and the joint hides the deeper floor.
    public static readonly float[] Min =
    {
        0.15f, 0.15f, 0.05f, 0.40f, 100f, 0f, 0f, 0.05f, -0.65f, 0.20f, 0f, 0.2f, 0.2f,
        // WALK CYCLE 64 ask-side: the box is the MEASURED LEDGE, not a guess. The wide
        // first box [4, 26] flooded the field's fresh/whole-move channel with stillbirths
        // (arena_c64: 347 fell vs 313, the champion 8454 vs 15007) because the walk's ask
        // is a ledge - the probes below all died INSIDE the arena's 20 s window:
        //   fold 13: 17.4 s, place 10: 14.4 s, load 7: 18.8 s.
        // Every value the round probed inside this box carried a full window:
        //   fold 7 (40 s, 16.94 m) .. 12 (38.9 s), place 11 (40 s, 17.30 m) .. 14 (28.3 s),
        //   load 8 (21.2 s) .. 13 (26.8 s); the drawn keys 10 / 12 / 10 sit mid-box.
        7f, 11f, 8f,
    };

    public static readonly float[] Max =
    {
        0.60f, 0.80f, 0.40f, 0.95f, 800f, 80f, 0.12f, 0.35f, 0.10f, 0.70f, 45f, 2.0f, 2.0f,
        12f, 14f, 13f,
    };

    // Defaults are the values the walk was verified on, so generation 0 is never
    // an unexplored corner of the box. WALK CYCLE 6r: Defaults[8] was 0.02 (the
    // pre-6r floor), so the reference genome did not reproduce the walk the tree
    // ships - it reproduced the old +0.02 band. It follows the field.
    public static readonly float[] Defaults =
    {
        0.35f, 0.45f, 0.22f, 0.80f, 400f, 25f, 0.04f, 0.16f, -0.45f, 0.45f, 22f, 1.0f, 1.0f,
        // WALK CYCLE 64 ask-side: the DRAWN keys themselves (PoseLibrary: SwingFold
        // stance knee 10, FootPlace 12, PlantLoad 10) - generation 0 reproduces the
        // walk the tree ships, and the box can express it exactly.
        10f, 12f, 10f,
    };

    // ─── WALK CYCLE 53: THE BOX AUDIT RULE ────────────────────────────────
    // The lesson of Min[8]: a gene whose SHIPPED value sits on a box edge is not a
    // searched gene - half of its jitter clamps straight back and the log cannot
    // tell that from an optimum. The rule the reviewers left behind: every default
    // must lie in the middle band of its own box, (default - min) / (max - min) in
    // [0.15, 0.85]. Audited at Start by the runner and printed in the run summary;
    // a violation is a fact of the run, not a crash (the arena is the place where
    // one learns whether the box can express the walk the tree ships).
    public const float DefaultBandLow = 0.15f;
    public const float DefaultBandHigh = 0.85f;

    // Empty list = the box obeys the rule. Each entry is one violation, already
    // formatted for the summary: "stepMinMeters default -0.45 sits at 0.000 of its
    // range (allowed 0.150..0.850)".
    public static System.Collections.Generic.List<string> AuditDefaultBand()
    {
        System.Collections.Generic.List<string> bad = new System.Collections.Generic.List<string>();
        for (int i = 0; i < Count; i++)
        {
            float range = Max[i] - Min[i];
            if (range <= 0f) { bad.Add(Names[i] + " box is empty (min " + Min[i] + " >= max " + Max[i] + ")"); continue; }
            float f = (Defaults[i] - Min[i]) / range;
            if (f < DefaultBandLow || f > DefaultBandHigh)
                bad.Add(Names[i] + " default " + Defaults[i].ToString("0.######", CultureInfo.InvariantCulture)
                    + " sits at " + f.ToString("0.000", CultureInfo.InvariantCulture) + " of its range (allowed "
                    + DefaultBandLow.ToString("0.000", CultureInfo.InvariantCulture) + ".."
                    + DefaultBandHigh.ToString("0.000", CultureInfo.InvariantCulture) + ")");
        }
        return bad;
    }

    // ─── WALK CYCLE 53: THE FAMILIES (one moves per child) ────────────────
    // Uniform jitter in 13-D is a random walk: after a few generations the child
    // stops resembling its parent and selection has no signal to follow. A move
    // therefore touches ONE family (p=0.7) or the whole genome (p=0.3). Inside a
    // family the members share ONE signed share of their own ranges, so a real pair
    // (*P with *D, stepMin/stepMax, the swing/stance lengths, the land share) moves
    // in a coordinated direction instead of drifting apart - the reviewers' point
    // ("report the pair, not the tier").
    public static readonly string[] FamilyNames =
    {
        "gaitTiming",     // swingDuration, stanceDuration, swingLandShare
        "gaitShape",      // swingLiftMeters, placementMarginMeters, comLeadSwapMeters
        "heightLaw",      // heightGainDegPerMeter, heightDampDegPerMeterPerSecond
        "stepBand",       // stepMinMeters, stepMaxMeters
        "arm",            // armSwingDeg
        "ankleBalance",   // ankleBalanceP, ankleBalanceD
        "stanceAsk",      // stanceFoldKneeDeg, stancePlaceKneeDeg, stanceLoadKneeDeg
                          //   (WALK CYCLE 64: the ask-side tier - the drawn stance
                          //   knee keys; the family keeps the three moving together)
    };

    public static readonly int[][] Families =
    {
        new[] { 0, 1, 3 },
        new[] { 2, 6, 7 },
        new[] { 4, 5 },
        new[] { 8, 9 },
        new[] { 10 },
        new[] { 11, 12 },
        new[] { 13, 14, 15 },
    };

    public const int FamilyCount = 7;
    public const string WholeGenomeFamily = "whole";

    public static string FamilyName(int family)
    {
        if (family < 0 || family >= FamilyCount) return WholeGenomeFamily;
        return FamilyNames[family];
    }

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
    // its parent. WALK CYCLE 53: this is the p=0.3 whole-genome move; the usual
    // move is JitterFamily below. `attempts` and `clamped` (both optional, both
    // Count long, owned by the caller) are the CLAMP PRESSURE counters: for every
    // gene they count the jittered values it received and how many of those hit a
    // wall. Without them "gene at its optimum" and "gene stuck in the wall" are
    // the same row in the log.
    public static void Jitter(float[] g, ArenaRng rng, float shareMin = 0.05f, float shareMax = 0.10f,
        int[] attempts = null, int[] clamped = null)
    {
        for (int i = 0; i < Count; i++)
        {
            float range = Max[i] - Min[i];
            float share = rng.Range(shareMin, shareMax);
            float step = range * share * (rng.Next01() * 2f - 1f);
            ApplyGene(g, i, step, attempts, clamped);
        }
        Clamp(g);
    }

    // WALK CYCLE 53: ONE FAMILY MOVES. The members share ONE signed share of their
    // own ranges, so a coupled pair keeps its relationship (stepMin travels with
    // stepMax, *P with *D, swing with stance) instead of drifting apart gene by
    // gene. A family outside the table falls back to the whole-genome move, so a
    // bad index can never silently do nothing.
    public static void JitterFamily(float[] g, ArenaRng rng, int family, float shareMin, float shareMax,
        int[] attempts = null, int[] clamped = null)
    {
        if (family < 0 || family >= FamilyCount)
        {
            Jitter(g, rng, shareMin, shareMax, attempts, clamped);
            return;
        }
        int[] members = Families[family];
        float share = rng.Range(shareMin, shareMax);
        float sign = rng.Next01() < 0.5f ? -1f : 1f;
        for (int k = 0; k < members.Length; k++)
        {
            int i = members[k];
            float range = Max[i] - Min[i];
            ApplyGene(g, i, range * share * sign, attempts, clamped);
        }
        Clamp(g);
    }

    // One gene's move and the pressure it met. The value is clamped here (not by
    // Clamp()) so the counter sees exactly which moves died on the wall.
    private static void ApplyGene(float[] g, int i, float step, int[] attempts, int[] clamped)
    {
        float v = g[i] + step;
        if (attempts != null && i < attempts.Length) attempts[i]++;
        if ((v < Min[i] || v > Max[i]) && clamped != null && i < clamped.Length) clamped[i]++;
        g[i] = Mathf.Clamp(v, Min[i], Max[i]);
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
