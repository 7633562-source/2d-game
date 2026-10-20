using System.Globalization;
using System.Text;

// ─── DOG ARENA (cycle 64, 23.09.2026) — THE DOG'S OWN GENOME ─────────────────
//
// Yury's process decision (22.09 ~23:05): the evolutionary flow is a first-class instrument. This
// is the dog lane's own box, built on the pattern the human arena proved (Trials/Runs/walk-cycle50-
// the-arena-gets-todays-walk.md and Assets/ArenaGenome.cs) and NOT forked from it: the genes are
// the dog's own law's levers, the boxes are the shipped initialisers' own neighbourhoods.
//
// CYCLE 69 (23.09) — THE GENOME EXPANSION, and the order that moved it: F:/hermes-data/ds-dog-
// genome-order.md (Yury's word: "увеличь перечень изменяемых параметров собак"). The best travel
// had stood at 7.636 m across c64/c65/c66 and the user's own runs; the 15-gene box was exhausted.
// 15 -> 41 genes: the walk clock/stride/flex family, the stance gains, THE WALK'S OWN P/D (the
// front limb's own servo, named by c63), the trunk's lumbar family and the pad/dwell levers.
// Every new gene's box is the [min,max] THE LAW'S OWN CODE ACCEPTS, quoted with its line below; a
// value beyond a law repair (EnsureBoundGait, l.4129-4166) is silently rewritten, so no box may
// cross one. THE SHIPPED CHAMPION (Defaults) lies inside every box; the per-gene fraction
// (default-min)/(max-min) is audited at Start and the edge cases are DISCLOSED, never hidden:
//   walkStanceKnee/Elbow 1.000 — the law's own cap WalkStanceDistalMax (l.1986, repair l.4147-4150)
//   walkSwingLiftFrac 0.000 — the law's own reach floor (l.4153-4154)
//   lumbarPelvisP / lumbarRestOn 0.000 — coupling/rest OFF positions (0 = off by construction).
// THE REFERENCE SCALES STAY FROZEN (errorReferenceDegrees, speedReferenceDegPerSec,
// comOffsetReference, comVelocityReference, runSpeedRef, muscleActivationSpeed, useStablePd): a
// mutated scale re-means every gain. foldLimDeg is a CONSTANT (Dog.LumbarAcceptedMaxDeg, Dog.cs
// l.177) and NOT spawn-overridable — a wall, not a gene.
//
// WHY THE DEFAULTS ARE THE ACCEPTED DOG. The c50 lesson on the human side: the arena's bodies ran
// the walk's SHIPPED DEFAULTS while the accepted walk needed its accepted switches, and every body
// fell. The dog's accepted line (row of record dog_c44_w04, md5 bc7a1625757d00ec0d987e052a61f98f)
// is produced by the SHIPPED law plus four flags (-dog 1 -dogWalk 1 -dogWalkTime 1 -dogPawFriction
// 0.4): so on this lane the shipped initialiser IS today's accepted configuration. Every Defaults[i]
// below is the field's own initialiser in Assets/DogStanceController.cs (md5
// ca4104613fe431c9ab175ae99735d189), quoted with its line, and the arena's reference body (id 1)
// spawns exactly that genome. Where the law REPAIRS its initialiser (walkStanceKnee/Elbow 24 -> 15;
// walkComMix-style floors), the default here is the EFFECTIVE value the law runs, so applying it at
// spawn is the accepted dog to the last digit — proven by the reference body's own row and
// per-step trace against the pre-expansion build (dog_c69pre / dog_c69arena, c69 report §3).
//
// THE BOX RULE (the human side's own, kept + the band): every Defaults[i] must lie INSIDE
// [Min[i], Max[i]] (AuditBox) and the runner publishes the per-gene band fraction (AuditDefaultBand,
// the human's [0.15, 0.85]) — a gene whose default sits ON an edge is a one-sided mutation, and the
// log must be able to tell "at its optimum" from "against the wall".
//
// THE ONE PAIR: `lumbarRestOn` and `lumbarRestDeg`. The law holds its joint rest value behind the
// gate `useLumbarJointRest` (DogStanceController.cs l.81, ships false) and the value itself in
// `lumbarJointRestDeg` (l.82, ships 8f). Sweeping the value alone would sweep a DEAD field whenever
// the gate is off, so the gate travels with the value: one switch gene (0/1, the box is the flag's
// own domain) plus one value gene. The default genome carries gate 0 — the accepted dog to the last
// digit — and a mutant that opens the gate carries a real degree with it.
//
// THE MUTATION LAW (cycle 69, the reviewers' condensed rules — the human side's implemented shape):
//   * ONE family per child at p = 0.70; the whole genome at 0.30 (Families below; every gene in
//     exactly one family). Inside a family the members share ONE signed share of their own ranges,
//     so a real pair moves together (*P with *D, the flex band, the pad pair).
//   * the step = 5-10 % of each gene's own range (3-5 % when the best-quartile mean stalls — the
//     runner's schedule, coarse -> fine, NEVER triggered by the population mean).
//   * CLAMP PRESSURE per gene: attempts/clamped over every jittered component (ApplyGene).
//
// DETERMINISM: the only generator is ArenaRng (xorshift64*, already the project's own arena RNG —
// reused, never re-derived). UnityEngine.Random is never used here, so one seed replays one genome
// sequence.
public static class DogArenaGenome
{
    public const int Count = 41;

    // ─── the fitness' posture constants (the runner's formula; stated here so the box and the
    // score are read in one place). THE CARD'S OWN ORDER, PAID IN METRES (cycle 65, the price in
    // Trials/Runs/dog-cycle65-arena-fitness.md §0): the card's PRIMARY clauses are the four-paw
    // stand (fourFeetGroundedFraction ~ 0.997) and the trunk band (trunkDrop ~ 0.02), the lane is
    // 14x away from the travel clause (the record row's comSpeedX 0.0986 against the card's 1.389),
    // so a full-window posture clause is worth 3.0 m of walking - more than every measured body's
    // whole travel in the c64 census except its top two, both of which fell. The stay-up/get-up
    // fraction (`up`, the card's GET UP AFTER A FALL clause) pays the same 3.0. A fall costs 6.0 m
    // = the whole posture pair with the stay-up term, so it can never be paid for by travel - but a
    // BAR cannot be priced: the card's first clause (fell = false) is carried by the RUNNER'S POOL
    // GATE (the selection pool is the non-fallers), and this weight only orders the fallback pool.
    public const float StandBonusMeters = 3.0f;
    public const float BandBonusMeters = 3.0f;
    public const float UpBonusMeters = 3.0f;
    public const float FallPenaltyMeters = 6.0f;
    // The card's own trunk clause: trunkDrop near 0.02 m standing; the band allows the stand's own
    // noise (the lane's c54-c59 trunk work measured this band).
    public const float TrunkBandMeters = 0.06f;

    // Names are the CSV column names and the JSON keys. THE ORDER IS A CONTRACT:
    // Trials/Runs/dog-arena-param-log.csv is append-only (the header is never rewritten), so a new
    // gene is APPENDED at the end and never inserted in the middle. Indices 0-14 are the c64 set and
    // keep their meanings; 15-40 are the c69 expansion.
    public static readonly string[] Names =
    {
        // 0-14 — the c64 genome (unchanged)
        "pawComP", "pawComD", "chestPGain", "chestDGain", "chestTargetTilt",
        "pelvisPGain", "pelvisDGain", "pelvisTargetTilt",
        "heightP", "heightD", "heightMaxDeg",
        "lumbarRestOn", "lumbarRestDeg",
        "walkPeriod", "walkDuty",
        // 15-40 — the c69 expansion
        "hipPGain", "hipDGain", "kneePGain", "kneeDGain",           // the limb servo P/D pairs
        "walkHipP", "walkKneeP",                                    // the walk's own P (all four limbs)
        "walkStanceKnee", "walkStanceElbow", "walkSwingLiftFrac",   // the stance hold / reach gate
        "walkHipFlex", "walkShoulderFlex", "walkHipExtend", "walkShoulderExtend",
        "walkKneeFlex", "walkElbowFlex",                            // the swing/stance flexion band
        "walkPawPlant", "walkFrontPlantScale", "walkStanceSweepDeg", "walkRearLag",
        "lumbarPGain", "lumbarDGain", "lumbarPelvisP", "lumbarPelvisD",
        "pawGroundHoldSeconds", "pawComDeadzone", "pawComVelDeadzone",
    };

    // The search box. Every value written into a body is clamped to it. Each line names the
    // law's own bound (repair = EnsureBoundGait l.4129-4166; use = the consuming line).
    public static readonly float[] Min =
    {
        // 0-14 (c64; Min[3] and Min[6] move 0.05 -> 0 in c69: the audit's own repair - the shipped
        // 0.30 sat at 0.128 of its range, below the human band's 0.15 floor, only because of the
        // 0.05 floor; a D gain of 0 is legal (no damping), so the floor moves, the box stays honest)
        2f, 1f, 0.2f, 0f, -30f,
        0.2f, 0f, -15f,
        5f, 0f, 2f,
        0f, 0f,
        0.30f, 0.25f,
        // 15-40 (c69)
        0.5f, 0.08f, 0.8f, 0.1f,      // hipPGain, hipDGain, kneePGain, kneeDGain
        0.5f, 0.5f,                   // walkHipP, walkKneeP
        6f, 6f, 0.70f,                // walkStanceKnee, walkStanceElbow, walkSwingLiftFrac
        4f, 4f, 6f, 6f,               // walkHipFlex, walkShoulderFlex, walkHipExtend, walkShoulderExtend
        30f, 30f,                     // walkKneeFlex, walkElbowFlex
        0.30f, 0.10f, 10f, 0.02f,     // walkPawPlant, walkFrontPlantScale, walkStanceSweepDeg, walkRearLag
        0.3f, 0.05f, 0f, 0.05f,       // lumbarPGain, lumbarDGain, lumbarPelvisP, lumbarPelvisD
        0.005f, 0.02f, 0.05f,         // pawGroundHoldSeconds, pawComDeadzone, pawComVelDeadzone
    };

    public static readonly float[] Max =
    {
        30f, 20f, 8f, 2f, 10f,
        8f, 2f, 15f,
        180f, 40f, 60f,
        1f, 24f,
        0.60f, 0.50f,   // walkDuty's max moves 0.55 -> 0.50 (c69): the law's own repair at 0.50
                        // (l.4133-4134 rewrites walkDuty > 0.50 to 0.38) - the old box crossed a wall
        8f, 1.2f, 12f, 1.6f,
        12f, 16f,
        15f, 15f, 0.80f,
        24f, 24f, 12f, 12f,
        60f, 60f,
        0.90f, 0.80f, 70f, 0.20f,
        8f, 1.2f, 3f, 1.2f,
        0.15f, 0.22f, 1.5f,
    };

    // Defaults = the shipped initialisers of Assets/DogStanceController.cs (each line quoted),
    // or the EFFECTIVE value where the law repairs its own initialiser (noted).
    public static readonly float[] Defaults =
    {
        12f,    // pawComP            l.38  public float pawComP = 12f;
        7f,     // pawComD            l.39  public float pawComD = 7f;
        2f,     // chestPGain         l.60  public float chestPGain = 2f;
        0.3f,   // chestDGain         l.61  public float chestDGain = 0.3f;
        -8f,    // chestTargetTilt    l.62  public float chestTargetTilt = -8f;
        2f,     // pelvisPGain        l.64  public float pelvisPGain = 2f;
        0.3f,   // pelvisDGain        l.65  public float pelvisDGain = 0.3f;
        0f,     // pelvisTargetTilt   l.66  public float pelvisTargetTilt = 0f;
        60f,    // heightP            l.89  public float heightP = 60f;
        8f,     // heightD            l.90  public float heightD = 8f;
        22f,    // heightMaxDeg       l.91  public float heightMaxDeg = 22f;
        0f,     // lumbarRestOn       l.81  public bool useLumbarJointRest = false;
        8f,     // lumbarRestDeg      l.82  public float lumbarJointRestDeg = 8f;
        0.40f,  // walkPeriod         l.478 public float walkPeriod = 0.40f;
        0.38f,  // walkDuty           l.791 public float walkDuty = 0.38f;
        // ─── c69 ───
        2f,     // hipPGain           l.247 public float hipPGain = 2f;       (l.2732/3251/3566 calls)
        0.3f,   // hipDGain           l.248 public float hipDGain = 0.3f;     (l.3553)
        3f,     // kneePGain          l.261 public float kneePGain = 3f;      (l.2761/6971)
        0.4f,   // kneeDGain          l.262 public float kneeDGain = 0.4f;    (l.3554)
        3.5f,   // walkHipP           l.831 public float walkHipP = 3.5f;     (l.6339: >0 gates it live)
        5f,     // walkKneeP          l.832 public float walkKneeP = 5f;      (l.6340)
        15f,    // walkStanceKnee     l.802 ships 24; EFFECTIVE 15 (cap WalkStanceDistalMax l.1986,
                //                          repair l.4147-4150) — the value the accepted walk runs
        15f,    // walkStanceElbow    l.803 ships 24; EFFECTIVE 15 (same repair)
        0.70f,  // walkSwingLiftFrac  l.805 public float walkSwingLiftFrac = 0.70f; (use clamp l.5836;
                //                          repair l.4153-4154 rewrites < 0.70 to 0.75 -> the floor)
        8f,     // walkHipFlex        l.795 public float walkHipFlex = 8f;
        8f,     // walkShoulderFlex   l.796 public float walkShoulderFlex = 8f;
        10f,    // walkHipExtend      l.798 public float walkHipExtend = 10f;  (repair > 12, l.4135)
        10f,    // walkShoulderExtend l.799 public float walkShoulderExtend = 10f; (repair > 12, l.4137)
        42f,    // walkKneeFlex       l.800 public float walkKneeFlex = 42f;   (repair < 30, l.4143)
        42f,    // walkElbowFlex      l.801 public float walkElbowFlex = 42f;  (repair < 30, l.4145)
        0.70f,  // walkPawPlant       l.806 public float walkPawPlant = 0.70f; (l.6735)
        0.40f,  // walkFrontPlantScale l.808 public float walkFrontPlantScale = 0.40f; (Clamp01 l.6315/6737)
        45f,    // walkStanceSweepDeg l.826 public float walkStanceSweepDeg = 45f; (l.6759-6772)
        0.06f,  // walkRearLag        l.792 public float walkRearLag = 0.06f; (floor at use l.5332)
        2f,     // lumbarPGain        l.68  public float lumbarPGain = 2f;    (walk override l.6352)
        0.3f,   // lumbarDGain        l.69  public float lumbarDGain = 0.3f;  (l.3605)
        0f,     // lumbarPelvisP      l.71  public float lumbarPelvisP = 0f;  (0 = the coupling off)
        0.3f,   // lumbarPelvisD      l.72  public float lumbarPelvisD = 0.3f;
        0.03f,  // pawGroundHoldSeconds l.57 public float pawGroundHoldSeconds = 0.03f; (latch l.7312-7316)
        0.10f,  // pawComDeadzone     l.51  public float pawComDeadzone = 0.10f; (l.7338/5835/5934)
        0.50f,  // pawComVelDeadzone  l.52  public float pawComVelDeadzone = 0.50f; (l.7342)
    };

    // ─── cycle 69: THE DEFAULT-BAND AUDIT (the human side's rule, verbatim) ───
    // A gene whose SHIPPED value sits on a box edge is not a searched gene: half of its jitter
    // clamps straight back and the log cannot tell that from an optimum. Every default must lie in
    // (default-min)/(max-min) in [0.15, 0.85]. The runner publishes the fractions at Start; a
    // violation is a fact of the run, not a crash. The DISCLOSED one-sided genes (law caps/floors
    // and 0 = off switches) are listed in the header block above.
    public const float DefaultBandLow = 0.15f;
    public const float DefaultBandHigh = 0.85f;

    public static System.Collections.Generic.List<string> AuditDefaultBand()
    {
        System.Collections.Generic.List<string> bad = new System.Collections.Generic.List<string>();
        for (int i = 0; i < Count; i++)
        {
            float range = Max[i] - Min[i];
            if (range <= 0f) { bad.Add(Names[i] + " box is empty"); continue; }
            float f = (Defaults[i] - Min[i]) / range;
            if (f < DefaultBandLow || f > DefaultBandHigh)
                bad.Add(Names[i] + " default " + F(Defaults[i]) + " sits at " + f.ToString("0.000", CultureInfo.InvariantCulture)
                    + " of its range (allowed " + DefaultBandLow.ToString("0.000", CultureInfo.InvariantCulture) + ".."
                    + DefaultBandHigh.ToString("0.000", CultureInfo.InvariantCulture) + ")");
        }
        return bad;
    }

    public static string BandFractionsJson()
    {
        StringBuilder sb = new StringBuilder(1024);
        sb.Append('{');
        for (int i = 0; i < Count; i++)
        {
            if (i > 0) sb.Append(',');
            float range = Max[i] - Min[i];
            float f = range > 0f ? (Defaults[i] - Min[i]) / range : 0f;
            sb.Append('"').Append(Names[i]).Append("\":").Append(f.ToString("0.000", CultureInfo.InvariantCulture));
        }
        sb.Append('}');
        return sb.ToString();
    }

    // ─── cycle 69: THE FAMILIES (one moves per child; every gene in exactly one) ───
    // Uniform jitter in 41-D is a random walk: after a few generations the child stops resembling
    // its parent and selection has no signal to follow. A move therefore touches ONE family
    // (p = 0.70) or the whole genome (p = 0.30). Inside a family the members share ONE signed share
    // of their own ranges, so a real pair (*P with *D, the walk's own P pair, the pad pair) moves
    // in a coordinated direction instead of drifting apart.
    public static readonly string[] FamilyNames =
    {
        "posture",     // chest/pelvis/height gains and tilts
        "pad",         // the paw CoM gains and the contact deadzones / dwell
        "limbServo",   // the limb servo P/D pairs + the walk's own P
        "flex",        // the swing/stance flexion and the stance distal hold
        "gait",        // the walk clock, plant, sweep, rear lag
        "trunk",       // the lumbar pair + the rest gate and value
    };

    public static readonly int[][] Families =
    {
        new[] { 2, 3, 4, 5, 6, 7, 8, 9, 10 },
        new[] { 0, 1, 38, 39, 40 },
        new[] { 15, 16, 17, 18, 19, 20 },
        new[] { 21, 22, 23, 24, 25, 26, 27, 28, 29 },
        new[] { 13, 14, 30, 31, 32, 33 },
        new[] { 11, 12, 34, 35, 36, 37 },
    };

    public const int FamilyCount = 6;
    public const string WholeGenomeFamily = "whole";

    public static string FamilyName(int family)
    {
        if (family < 0 || family >= FamilyCount) return WholeGenomeFamily;
        return FamilyNames[family];
    }

    public static float[] DefaultsCopy()
    {
        float[] g = new float[Count];
        for (int i = 0; i < Count; i++) g[i] = Defaults[i];
        return g;
    }

    public static float[] Copy(float[] src)
    {
        float[] g = new float[Count];
        for (int i = 0; i < Count && i < src.Length; i++) g[i] = src[i];
        return g;
    }

    // A fresh genome: uniform over the box. Not the defaults — the point of the fresh share is to
    // keep visiting the box the jitter cannot reach. THE RNG IS BY REF (cycle 65): ArenaRng is a
    // struct, so the by-value signature this method shipped with (c64) left the CALLER'S state
    // untouched — every fresh draw during the pre-measurement phase returned the same 15 numbers
    // (measured: bodies 2..31 of dog_c64arena2_s1 carry one genome, 30 of 60 measured bodies). The
    // ref is the whole fix: one draw, one advance.
    public static float[] Random(ref ArenaRng rng)
    {
        float[] g = new float[Count];
        for (int i = 0; i < Count; i++)
            g[i] = rng.Range(Min[i], Max[i]);
        Clamp(g);
        return g;
    }

    // Jitter every gene by shareMin..shareMax of its OWN range, then clamp. The step size is drawn
    // per gene, so a child is never a uniform translation of its parent. This is the p = 0.30
    // whole-genome move; `attempts`/`clamped` are the cycle-69 CLAMP-PRESSURE counters (per gene:
    // how many jittered components it received and how many hit a wall), so "at its optimum" and
    // "stuck in the wall" become different rows in the log.
    public static void Jitter(float[] g, ref ArenaRng rng, float shareMin = 0.05f, float shareMax = 0.10f,
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

    // cycle 69: ONE FAMILY MOVES (the usual move). The members share ONE signed share of their own
    // ranges, so a coupled pair keeps its relationship instead of drifting apart gene by gene. A
    // family outside the table falls back to the whole-genome move, so a bad index can never
    // silently do nothing.
    public static void JitterFamily(float[] g, ref ArenaRng rng, int family, float shareMin, float shareMax,
        int[] attempts = null, int[] clamped = null)
    {
        if (family < 0 || family >= FamilyCount)
        {
            Jitter(g, ref rng, shareMin, shareMax, attempts, clamped);
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

    // One gene's move and the pressure it met. The value is clamped here (not by Clamp()) so the
    // counter sees exactly which moves died on the wall.
    private static void ApplyGene(float[] g, int i, float step, int[] attempts, int[] clamped)
    {
        float v = g[i] + step;
        if (attempts != null && i < attempts.Length) attempts[i]++;
        if ((v < Min[i] || v > Max[i]) && clamped != null && i < clamped.Length) clamped[i]++;
        g[i] = UnityEngine.Mathf.Clamp(v, Min[i], Max[i]);
    }

    public static void Clamp(float[] g)
    {
        for (int i = 0; i < Count; i++)
            g[i] = i < g.Length ? UnityEngine.Mathf.Clamp(g[i], Min[i], Max[i]) : Defaults[i];
        // The pair's own constraint: a gate carries a value that can act. Clamp alone would let a
        // fresh draw land the value anywhere; nothing else here is forced.
        if (g[11] > 0.5f && g[12] < Min[12]) g[12] = Min[12];
    }

    // ─── the one entry the bodies are built with ─────────────────────────────
    // Field-for-field, the same fields HeadlessTrial.ApplyDogGains writes through -pawComP ...
    // -dogWalkDuty. Nothing else is touched: rig, masses, friction, gravity and fixedDelta keep the
    // accepted line's own values (the comparability rule).
    public static void Apply(DogStanceController c, float[] g)
    {
        if (c == null || g == null) return;
        c.pawComP = g[0];
        c.pawComD = g[1];
        c.chestPGain = g[2];
        c.chestDGain = g[3];
        c.chestTargetTilt = g[4];
        c.pelvisPGain = g[5];
        c.pelvisDGain = g[6];
        c.pelvisTargetTilt = g[7];
        c.heightP = g[8];
        c.heightD = g[9];
        c.heightMaxDeg = g[10];
        c.useLumbarJointRest = g[11] > 0.5f;
        c.lumbarJointRestDeg = g[12];
        c.walkPeriod = g[13];
        c.walkDuty = g[14];
        // ─── c69 expansion ───
        c.hipPGain = g[15];
        c.hipDGain = g[16];
        c.kneePGain = g[17];
        c.kneeDGain = g[18];
        c.walkHipP = g[19];
        c.walkKneeP = g[20];
        c.walkStanceKnee = g[21];
        c.walkStanceElbow = g[22];
        c.walkSwingLiftFrac = g[23];
        c.walkHipFlex = g[24];
        c.walkShoulderFlex = g[25];
        c.walkHipExtend = g[26];
        c.walkShoulderExtend = g[27];
        c.walkKneeFlex = g[28];
        c.walkElbowFlex = g[29];
        c.walkPawPlant = g[30];
        c.walkFrontPlantScale = g[31];
        c.walkStanceSweepDeg = g[32];
        c.walkRearLag = g[33];
        c.lumbarPGain = g[34];
        c.lumbarDGain = g[35];
        c.lumbarPelvisP = g[36];
        c.lumbarPelvisD = g[37];
        c.pawGroundHoldSeconds = g[38];
        c.pawComDeadzone = g[39];
        c.pawComVelDeadzone = g[40];
    }

    // The flag list the same genome is transplanted through on the accepted trial line (the
    // transplant attempt; the rank proposes, the line decides). Emitted in the law's own switch
    // names, so a reader can diff it against HeadlessTrial.ApplyDogGains. THREE c69 genes have NO
    // switch there (walkStanceKnee, walkStanceElbow, walkSwingLiftFrac): they are DISCLOSED by
    // TransplantUnmapped(), never silently dropped.
    public static string TransplantFlags(float[] g)
    {
        StringBuilder sb = new StringBuilder(512);
        sb.Append("-pawComP ").Append(F(g[0]))
          .Append(" -pawComD ").Append(F(g[1]))
          .Append(" -chestP ").Append(F(g[2]))
          .Append(" -chestD ").Append(F(g[3]))
          .Append(" -chestTilt ").Append(F(g[4]))
          .Append(" -pelvisP ").Append(F(g[5]))
          .Append(" -pelvisD ").Append(F(g[6]))
          .Append(" -pelvisTilt ").Append(F(g[7]))
          .Append(" -heightP ").Append(F(g[8]))
          .Append(" -heightD ").Append(F(g[9]))
          .Append(" -heightMax ").Append(F(g[10]))
          .Append(" -lumbarJointRest ").Append(g[11] > 0.5f ? "1" : "0")
          .Append(" -lumbarRestDeg ").Append(F(g[12]))
          .Append(" -dogWalkPeriod ").Append(F(g[13]))
          .Append(" -dogWalkDuty ").Append(F(g[14]))
          .Append(" -hipP ").Append(F(g[15]))
          .Append(" -hipD ").Append(F(g[16]))
          .Append(" -kneeP ").Append(F(g[17]))
          .Append(" -kneeD ").Append(F(g[18]))
          .Append(" -dogWalkHipP ").Append(F(g[19]))
          .Append(" -dogWalkKneeP ").Append(F(g[20]))
          .Append(" -dogWalkHipFlex ").Append(F(g[24]))
          .Append(" -dogWalkShoulderFlex ").Append(F(g[25]))
          .Append(" -dogWalkHipExt ").Append(F(g[26]))
          .Append(" -dogWalkShoulderExt ").Append(F(g[27]))
          .Append(" -dogWalkKneeFlex ").Append(F(g[28]))
          .Append(" -dogWalkElbowFlex ").Append(F(g[29]))
          .Append(" -dogWalkPlant ").Append(F(g[30]))
          .Append(" -dogWalkFrontPlant ").Append(F(g[31]))
          .Append(" -dogWalkSweep ").Append(F(g[32]))
          .Append(" -dogWalkRearLag ").Append(F(g[33]))
          .Append(" -lumbarP ").Append(F(g[34]))
          .Append(" -lumbarD ").Append(F(g[35]))
          .Append(" -lumbarPelvisP ").Append(F(g[36]))
          .Append(" -lumbarPelvisD ").Append(F(g[37]))
          .Append(" -pawGroundHold ").Append(F(g[38]))
          .Append(" -pawComDead ").Append(F(g[39]))
          .Append(" -pawComVelDead ").Append(F(g[40]));
        return sb.ToString();
    }

    // The genes this genome carries that the trial's flag list cannot carry yet (no switch in
    // HeadlessTrial.ApplyDogGains): named, so a transplant round can rule on them.
    public static string TransplantUnmapped()
    {
        return "walkStanceKnee;walkStanceElbow;walkSwingLiftFrac";
    }

    private static string F(float v)
    {
        return v.ToString("0.######", CultureInfo.InvariantCulture);
    }

    public static string CsvHeader()
    {
        StringBuilder sb = new StringBuilder(512);
        for (int i = 0; i < Count; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append(Names[i]);
        }
        return sb.ToString();
    }

    public static string CsvValues(float[] g)
    {
        StringBuilder sb = new StringBuilder(512);
        for (int i = 0; i < Count; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append((i < g.Length ? g[i] : 0f).ToString("0.######", CultureInfo.InvariantCulture));
        }
        return sb.ToString();
    }

    public static string ToJson(float[] g)
    {
        StringBuilder sb = new StringBuilder(1024);
        sb.Append('{');
        for (int i = 0; i < Count; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append('"').Append(Names[i]).Append("\":");
            sb.Append((i < g.Length ? g[i] : 0f).ToString("0.######", CultureInfo.InvariantCulture));
        }
        sb.Append('}');
        return sb.ToString();
    }

    public static string BoxesJson()
    {
        StringBuilder sb = new StringBuilder(2048);
        sb.Append('{');
        for (int i = 0; i < Count; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append('"').Append(Names[i]).Append("\":{\"min\":");
            sb.Append(Min[i].ToString("0.######", CultureInfo.InvariantCulture));
            sb.Append(",\"default\":");
            sb.Append(Defaults[i].ToString("0.######", CultureInfo.InvariantCulture));
            sb.Append(",\"max\":");
            sb.Append(Max[i].ToString("0.######", CultureInfo.InvariantCulture));
            sb.Append('}');
        }
        sb.Append('}');
        return sb.ToString();
    }

    // The box audit the runner publishes: which gene would exclude its own shipped value. Empty
    // list == a usable box.
    public static string AuditBox()
    {
        StringBuilder sb = new StringBuilder(128);
        int bad = 0;
        for (int i = 0; i < Count; i++)
        {
            if (Defaults[i] < Min[i] || Defaults[i] > Max[i])
            {
                if (bad > 0) sb.Append(';');
                sb.Append(Names[i]);
                bad++;
            }
        }
        return bad == 0 ? "" : sb.ToString();
    }
}
