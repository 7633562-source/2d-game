using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

// ARENA-1 (Yury, 18.09.2026): the parameter-sweep arena.
//
// ONE point, no lanes: every human appears at the same x, so the distance of a
// human is its own displacement from that shared spawn x and nothing else.
// Humans must NOT collide with each other: their rigidbodies go on the dedicated
// `ArenaHuman` layer, and that layer's self pair is switched OFF in
// ProjectSettings/Physics2DSettings, while ArenaHuman x Ground stays on. The
// project setting is the mechanism and the arena reads the engine state back
// (Physics2D.GetIgnoreLayerCollision) so the proof is a measurement, not a claim.
//
// A new human spawns every 500 ms with a different genome of the pose-drive walk
// parameters, carries the accepted walk (WALK CYCLE 50 below), is measured 15 s
// later and destroyed 30 s after its spawn, so the alive count oscillates instead
// of growing.
//
// WALK CYCLE 50 (side round, 22.09): THE SPAWN'S OWN WALK CONFIGURATION.
// Until this cycle the arena's bodies were commanded to walk on the frame they
// spawned, with the pose drive's own shipped values, and every run ended with 0
// walkers: the plain arena run and the SAME run carrying the accepted walk's
// switches (-captureWalk 1 -poseDrive 1 -poseWalk 1 -poseTailHold 0.010
// -poseAnkleCarry -14 -poseAnkleRamp 0.09) produced identical rows, 51 of 51
// measured humans bit for bit. The reason is measured, not assumed: with no foot
// down yet the pose drive only ever runs its STAND pose (PoseDriveController.Tick
// needs standCaptured and a grounded foot before TickWalk), so every body
// collapsed in ~1.3 s and no walk switch could reach a live walk. The accepted
// walk (Trials/Runs/c6w38n_walk, d76e2943) is verified from a SETTLED quiet stand:
// the trial opens the walk key at -walkTime 5 s. On the tree, the accepted command
// line with -walkTime 0 collapses in 1.47 s while the same line with the same
// genome and -walkTime 5 walks 3.9 m (c50_probe_g0 / c50_probe_g5).
// So the spawn now carries the accepted start AND the accepted walk values below;
// a fresh run's first body (id 1) is the reference genome, so every run contains
// the one body the walk was verified on.
//
// Evolution (documented in the run summary as well): fitness =
// distance_at_min(15, fallTime) + 0.4*min(fallTime,15) - 3.0*[fallTime<15], a
// tournament of 5 inside the best third of a sliding window of the last 120
// measured humans, one family of genes jittered per child (the whole genome at
// p=0.3), 25 pct fresh random blood, and the best ELIGIBLE body of the window
// re-spawned unchanged every EliteEverySpawns spawns. The unchanged re-spawn is
// also the collision proof: two copies, same genome, same spawn point, different
// ages. WALK CYCLE 53 rewrote selection, fitness and mutation; the previous law
// and its numbers are named in the summary (previousLawFallPenaltyMeters) so an
// older run can be told from a new one.
//
// Files (append-only where the card says so):
//   Trials/Runs/arena-param-log.csv  fixed-contract rows, appended, never rewritten
//   Trials/Runs/<label>.json         run summary + one JSON row per human
//   Trials/Runs/<label>.csv          the same rows as a table
public class ArenaRunner : MonoBehaviour
{
    public const string ArenaLayerName = "ArenaHuman";
    public const int EliteMinMeasured = 20;
    // WALK CYCLE 53: 8 -> 24. The twin pair (the unchanged re-spawn, the collision
    // proof) is real value and stays; paying for it every 8 spawns kept a quarter of
    // the population out of the search. Every 24 spawns ~= once per generation.
    public const int EliteEverySpawns = 24;
    public const float ChildShare = 0.75f;
    // WALK CYCLE 53: 3 -> 5 draws. Against a 13-body quartile of the CUMULATIVE pool
    // (145 bodies by t=300) three draws carried almost no pressure; five draws from
    // the best THIRD of a 120-body WINDOW do.
    public const float TournamentSize = 5f;

    // ─── WALK CYCLE 53: THE SLIDING WINDOW ─────────────────────────────────
    // The tournament used to run against every measured body of the run, so the
    // 45-s champion ruled to the end and a 900-body blob was one generation. The
    // selection pool is now the LAST SelectionWindowMeasured measured bodies: the
    // elite's ancestor space moves forward with the run, ~7 generations per 900
    // bodies, and the champion's turnover is what the report reads.
    public const int SelectionWindowMeasured = 120;
    // The tournament's own pool: the best THIRD of the window (the report keeps the
    // old "quartile" language in its field names to stay comparable).
    public const float SelectionPoolFraction = 1f / 3f;

    // WALK CYCLE 53: THE MUTATION MIX. One family per child at p=0.7, the whole
    // genome at p=0.3: a uniform jitter of all 13 genes is a random walk in 13-D,
    // the child stops resembling its parent and selection goes blind.
    public const float OneFamilyShare = 0.70f;
    // The step schedule's two gears, as shares of each gene's own range.
    public const float CoarseJitterMin = 0.05f;
    public const float CoarseJitterMax = 0.10f;
    public const float FineJitterMin = 0.03f;
    public const float FineJitterMax = 0.05f;
    // The plateau test: the BEST-THIRD mean is sampled every QuartileCheckpointMeasured
    // measured bodies (never the total mean - half the population is fresh blood and
    // elites, so the total mean cannot rise while the search does its job). When the
    // mean has not risen by PlateauRiseMeters over three checkpoints, the step
    // schedule drops to the fine gear.
    public const int QuartileCheckpointMeasured = 25;
    public const float PlateauRiseMeters = 0.05f;

    // ─── WALK CYCLE 50: THE WALK THE ARENA'S BODIES CARRY ──────────────────
    // The accepted trial line's own walk configuration, named in ONE place so a
    // reader can diff it against the line the accepted row was produced by
    // (Trials/Runs/c6w38n_walk: `-walk 1 -walkTime 5 -walkDuration 35 -captureWalk 1
    // -poseDrive 1 -poseWalk 1 -poseTailHold 0.010 -poseAnkleCarry -14
    // -poseAnkleRamp 0.09`).
    //
    // WHERE EACH PIECE COMES FROM (no third source is invented - the trial path is
    // not read, not edited, and not forked):
    //   -poseDrive 1 -poseWalk 1   the arena's genome entry already turns both on
    //                              (PoseDriveController.ApplyArenaGenome(genome, true)).
    //   -captureWalk 1             BalanceController.useCaptureWalker ships as 1 and
    //                              the arena never overrides it; while the pose drive
    //                              is active it is the single writer of the joints, so
    //                              the level 1 capture walker is not the walk here.
    //   -walkTime 5                WalkOnsetSeconds: the quiet stand window the
    //                              accepted walk starts from (ArenaRunner.Tick opens
    //                              each body's key once its own window is over).
    //   -poseTailHold 0.010        WalkTailHoldSeconds
    //   -poseAnkleCarry -14        WalkAnkleCarryDeg
    //   -poseAnkleRamp 0.09        WalkAnkleRampSeconds
    // The last three are the ONLY accepted values with no shipped initialiser equal
    // to them (PoseDriveController ships swingAnkleDeg 0, swingAnkleRampSeconds
    // 0.045, poseTailHoldSeconds 0), so the spawn applies them explicitly through
    // ApplyArenaWalkConfig - the same one-call discipline as the genome. Changing a
    // shipped initialiser instead would move the trial's own rows, which this cycle
    // must not do.
    public const float WalkOnsetSeconds = 5f;
    public const float WalkAnkleCarryDeg = -14f;
    public const float WalkAnkleRampSeconds = 0.09f;
    public const float WalkTailHoldSeconds = 0.010f;

    // ─── WALK CYCLE 58: THE VISUAL LAYER (visual runs only; no physics) ────
    // The dog arena's c69 pattern, on the human arena: one HUD line (sim time,
    // spawns, measured, the best window distance and its body id) drawn from a
    // TextMesh parented to the arena camera - no collider, no rigidbody, never
    // read by the measurement, and built only when a camera exists, so a
    // headless -nographics run neither builds it nor pays for it. The numbers on
    // the heads are the c50-era digits, unchanged.
    public const float HudDigitMeters = 0.34f;
    public const int HudUpdateSteps = 20;          // 0.1 s at the 0.005 s fixed step
    public const float ShotAspect = 16f / 9f;      // the take's shot size (1920x1080)
    public const float HudMarginMeters = 0.5f;     // the HUD's inset from the frame corner

    // ─── ARENA CYCLE 59 (Yury, 23.09): THE VIEW RIDES WITH THE LEADER ──────
    // Yury's word: "the edge of the view screen must shift following the furthest
    // gone; there must be no dead zones". Until this cycle the lens stood still at
    // spawnX + cameraOffsetX for the whole take, so the leading edge of the picture
    // sat at spawnX + cameraOffsetX + halfWidth = 11.389 m (orthographicSize 5, a
    // 16:9 frame): the 180 s take of record (arena_c58vis3, seed 1) has 16 bodies
    // past that line and its furthest body reached 14.2386 m, so the run's own
    // leaders were invisible for their last seconds - the dead zone Yury names.
    //
    // THE RULE. The LEADING edge of the frame is pinned cameraLeadMeters ahead of the
    // FURTHEST ALIVE body, and the lens never sits further left than the accepted
    // framing (spawnX + cameraOffsetX), so the take's early seconds - the standing
    // crowd at the spawn - are framed exactly as accepted. The frame is 17.7778 m
    // wide at this lens (2 x 5 x 16/9; read back from the shot: the HUD's own 0.5 m
    // inset lands on pixel 54 of the 1920 px capture), so the pin keeps 15.28 m of
    // WALKED ground behind the leader - wider than the whole field's own spread (the
    // same take: spawn line at 0, furthest body 14.24 m), which is why the spawn line
    // stays in the picture while the leader is pinned at the edge. And no frame is
    // spent on floor the run has not reached: the leading edge is 2.5 m ahead of the
    // leader, never more. The smooth approach is clamped so the leader can never come
    // closer than ArenaCameraMinLeadMeters to the edge, whatever the step does.
    public const float ArenaCameraMinLeadMeters = 2.0f;      // the hard guard on the smooth approach
    public const float ArenaCameraFollowTauSeconds = 0.10f;  // the follow's own smoothing
    public const float ArenaCameraReturnTauSeconds = 0.75f;  // when the leader drops back (a cull)
    public const float ArenaCameraSampleSeconds = 0.5f;      // the take's own trace cadence
    public bool cameraFollowLeader = true;                   // -arenaCamFollow 0 = the fixed-lens control
    public float cameraLeadMeters = 2.5f;                    // -arenaLead, the floor ahead of the leader

    // The child_kind of the first body of a run: the REFERENCE genome
    // (ArenaGenome.Defaults - the values the walk was verified on), spawned once at
    // id 1 so every run holds the one body whose walk is comparable with the trial's
    // row. 0 fresh random / 1 child of a parent / 2 elite copy keep their meaning.
    public const int ReferenceChildKind = 3;

    // ─── configuration: command line in headless, Inspector in the scene ───
    public int seed = 1;
    public string label = "arena";
    public string runId = "";
    public string sourceFingerprint = "";
    public string buildGuid = "";
    public int seedUsedByRun;
    public float spawnInterval = 0.5f;
    // WALK CYCLE 58: 15 -> 20 s. The fitness window starts at the walk onset
    // (WalkOnsetSeconds = 5), and Yury's onset rule requires the POST-onset window
    // to be a full 15 s - the measure mark therefore sits at spawnTime + 20 s.
    public float measureSeconds = 20f;
    // WALK CYCLE 58/61: ArenaHumanAgent.LawQuality (the line's own band, default since
    // c61), LawLine (the c58 bars, kept as the round's own before) or LawControl (the
    // c54 shape over the same window - the c58 round's control).
    public int lawMode = ArenaHumanAgent.LawQuality;
    // WALK CYCLE 61: the band's own knobs (the round's sensitivity reads; the landed
    // values are the constants' - the accepted line's own floor and shape share).
    public float liftFloorMeters = ArenaHumanAgent.SweepLiftFloorMeters;
    public float clearShareBar = ArenaHumanAgent.SweepClearShareBar;
    public float cullSeconds = 30f;
    public int aliveCap = 60;
    public float durationSeconds = 90f;
    public float spawnX = 0f;
    public float spawnY = -0.82f;
    public float groundWidth = 200f;
    public float groundFriction = 0.4f;
    public float numberHeight = 0.34f;
    public float cameraSize = 5f;
    public float cameraOffsetX = 2.5f;
    public float photoCameraSize = 2.2f;
    public float photoOffsetX = 1.2f;
    public bool externalTick;
    public bool headless;
    public string outputFolder = "";
    public bool buildGroundOnBegin = true;

    // ─── state ─────────────────────────────────────────────────────────────
    private readonly List<ArenaHumanAgent> alive = new List<ArenaHumanAgent>(96);
    private readonly List<ArenaHumanAgent> measured = new List<ArenaHumanAgent>(256);
    private readonly List<ArenaHumanAgent> all = new List<ArenaHumanAgent>(256);
    private readonly List<ArenaHumanAgent> eliteCopies = new List<ArenaHumanAgent>(16);
    private readonly List<string> aliveCurve = new List<string>(256);
    private readonly List<string> wallCurve = new List<string>(256);

    private ArenaRng rng;
    private bool begun;
    private float simTime;
    private float nextSpawnTime;
    private int spawnIndex;
    private int spawnCount;
    private int measuredCount;
    private int culledCount;
    private int maxAlive;
    private float lastSpawnTime = -1f;
    private float intervalSum;
    private float intervalMin = float.MaxValue;
    private float intervalMax;
    private int intervalSamples;
    private int maxRigidbodies;
    private int overlapMax;
    private float overlapMaxTime = -1f;
    private int inheritedChildren;
    private int parentInQuartileCount;
    private readonly List<float> parentPercentiles = new List<float>(256);

    // ─── WALK CYCLE 53: the selection / mutation bookkeeping ───────────────
    // CLAMP PRESSURE: per gene, the jittered values it received and how many of
    // them hit a box wall. The share of these two is what tells "gene at its
    // optimum" apart from "gene stuck in the wall".
    private readonly int[] jitterAttempts = new int[ArenaGenome.Count];
    private readonly int[] jitterClamped = new int[ArenaGenome.Count];
    private int oneFamilyChildren;                       // children whose move touched ONE family
    private int wholeGenomeChildren;                     // children whose move touched all 13 genes
    private readonly System.Collections.Generic.Dictionary<string, int> moveFamilyCounts =
        new System.Collections.Generic.Dictionary<string, int>();   // per-child family census
    private readonly List<float> bestThirdMeanSeries = new List<float>(64);
    private readonly List<int> bestThirdMeanAtMeasured = new List<int>(64);
    private bool fineSchedule;                           // the step schedule's gear
    private int fallbackPicks;                           // window had no ELIGIBLE body: the pool fell back
    private int eligibleMeasured;
    private int ineligibleMeasured;
    private readonly List<int> elitePickIds = new List<int>(64);   // the champion's turnover, in order
    private int lastEliteId = -1;
    private float nextAliveSample;
    private float nextWallSample;
    private float nextRigidbodySample;
    private float lastWallSampleTime;
    private Camera arenaCamera;
    // ARENA CYCLE 59: the follow's own state. Visual runs only - no physics object
    // reads it, so the recorded rows cannot move (the round's fence re-runs the same
    // seed and compares body for body).
    private bool cameraPinned;
    private float cameraPinX;
    private float nextCameraSample;
    private float cameraLeaderMax = float.MinValue;
    private float cameraLeadMin = float.MaxValue;
    private float cameraLeadMax = float.MinValue;
    private float cameraFrameRightMax = float.MinValue;
    private float cameraFrameLeftMin = float.MaxValue;
    private float cameraCenterMin = float.MaxValue;
    private float cameraCenterMax = float.MinValue;
    private int cameraSamples;
    private readonly List<string> cameraRows = new List<string>(512);
    private GameObject groundHost;
    private float[] pendingEliteCopyGenome;
    private int pendingEliteCopyParentId = -1;
    // WALK CYCLE 58: the visual layer's own state (never read by the measurement).
    private TextMesh hudMesh;
    private MeshRenderer hudRenderer;
    private int hudSteps;
    private float bestWindowDistance;
    private int bestWindowId = -1;

    public bool Done => begun && simTime >= durationSeconds;
    public bool Begun => begun;
    public int AliveCount => alive.Count;
    public int MeasuredCount => measured.Count;
    public Camera RunnerCamera => arenaCamera;
    public IReadOnlyList<ArenaHumanAgent> MeasuredHumans => measured;

    public static int ArenaLayer
    {
        get
        {
            int layer = LayerMask.NameToLayer(ArenaLayerName);
            return layer >= 0 ? layer : 0;
        }
    }

    // ─── lifecycle ─────────────────────────────────────────────────────────
    private void Start()
    {
        if (externalTick) return;
        Begin(Time.fixedDeltaTime);
    }

    private void FixedUpdate()
    {
        if (externalTick || !begun) return;
        simTime += Time.fixedDeltaTime;
        Tick(simTime);
    }

    private void OnDestroy()
    {
        // Play mode: the scene run has no Finish(), so the summary is written when
        // the object goes away. A headless run is written by HeadlessTrial.Finish.
        if (externalTick || !begun) return;
        WriteFiles(simTime, -1f);
    }

    public void Begin(float fixedDelta)
    {
        if (begun) return;
        begun = true;
        seedUsedByRun = seed;
        rng = new ArenaRng(seed);
        simTime = 0f;
        nextSpawnTime = 0f;
        lastWallSampleTime = Time.realtimeSinceStartup;

        if (string.IsNullOrEmpty(runId))
            runId = label + "_s" + seed.ToString(CultureInfo.InvariantCulture);

        if (buildGroundOnBegin)
            BuildGroundOnly();

        LogLayerState();

        nextAliveSample = 0f;
        nextWallSample = 0f;
        if (!headless)
            EnsureArenaCamera(cameraSize, cameraOffsetX);
    }

    // The arena scene is ground and nothing else.
    private void BuildGroundOnly()
    {
        groundHost = new GameObject("ArenaGround");
        GroundBuilder ground = groundHost.AddComponent<GroundBuilder>();
        ground.groundSize = new Vector2(Mathf.Max(20f, groundWidth), 2f);
        ground.groundPosition = new Vector2(spawnX, -3f);
        ground.groundFriction = groundFriction;
        ground.BuildGround();
    }

    // Read the engine back: the ProjectSettings pair is the mechanism, this line is
    // the evidence. If the layer does not exist or the pair is live, the run is not
    // a no-collision run and that must be visible in the log, not silent.
    private void LogLayerState()
    {
        // WALK CYCLE 53: THE BOX AUDIT RULE, at Start. Every gene's shipped default
        // must lie in the middle band of its own box (AuditDefaultBand); a default on
        // an edge is a one-sided mutation that no row of the log can show. The
        // verdict rides in the run summary as well, so a reader of the JSON never has
        // to trust this console line. The audit REPORTS, it does not abort: the arena
        // is exactly the place where one learns whether the box can express the walk
        // the tree ships.
        System.Collections.Generic.List<string> boxBad = ArenaGenome.AuditDefaultBand();
        if (boxBad.Count == 0)
        {
            Debug.Log("ARENA_BOX_AUDIT OK - every default lies in 0.15..0.85 of its own range");
        }
        else
        {
            for (int i = 0; i < boxBad.Count; i++)
                Debug.LogWarning("ARENA_BOX_AUDIT " + boxBad[i]);
        }
        int layer = LayerMask.NameToLayer(ArenaLayerName);
        if (layer < 0)
        {
            Debug.LogError("ARENA_LAYER missing layer " + ArenaLayerName
                + " - humans would collide with each other");
            return;
        }
        bool selfIgnored = Physics2D.GetIgnoreLayerCollision(layer, layer);
        bool groundIgnored = Physics2D.GetIgnoreLayerCollision(layer, GroundLayers.Index);
        Debug.Log("ARENA_LAYER " + ArenaLayerName + "=" + layer
            + " selfPairIgnored=" + selfIgnored
            + " groundPairIgnored=" + groundIgnored);
        if (!selfIgnored)
            Debug.LogError("ARENA_LAYER self pair is LIVE - human-human collisions are on");
        if (groundIgnored)
            Debug.LogError("ARENA_LAYER ground pair is IGNORED - humans would fall through");
    }

    // ─── one fixed step ────────────────────────────────────────────────────
    public void Tick(float time)
    {
        if (!begun) return;
        simTime = time;

        // Cull BEFORE spawning: the cap (60) and the 30 s life exactly meet at
        // 0.5 s intervals, so spawning first made every spawn from t=30 s on sit one
        // body over the cap and force-cull the oldest, which is the same body the
        // 30 s rule removes a frame later. Order is the whole fix.
        CullDue(time);

        while (time + 1e-4f >= nextSpawnTime)
        {
            Spawn(nextSpawnTime);
            nextSpawnTime += spawnInterval;
        }

        // WALK CYCLE 50: the walk key. Until this cycle the arena's bodies were
        // commanded to walk on the frame they spawned (intent.moveX = 1 in
        // SpawnBody); the accepted law is verified from a settled quiet stand, so a
        // body's key opens only once its own window (WalkOnsetSeconds = the accepted
        // line's -walkTime 5) is over. Closed key = the pose drive's stand pose,
        // which is the same stand the accepted trial stands in for its first 5 s.
        for (int i = 0; i < alive.Count; i++)
        {
            ArenaHumanAgent agent = alive[i];
            if (!agent.WalkKeyOpen && time + 1e-4f >= agent.spawnTime + WalkOnsetSeconds)
                agent.OpenWalkKey(agent.spawnTime + WalkOnsetSeconds);
            agent.TickWalkDrive(time);
            agent.Step(time);
        }

        MeasureDue(time);
        SampleCurves(time);
        // WALK CYCLE 58: the HUD, visual runs only (no camera = no work at all).
        EnsureVisualLayer();
        // ARENA CYCLE 59: the lens, visual runs only, after the bodies have moved.
        UpdateLeaderCamera(Time.fixedDeltaTime);
    }

    // ─── spawn + evolution ─────────────────────────────────────────────────
    private void Spawn(float time)
    {
        int id = spawnCount + 1;
        float[] genome;
        int parentId = -1;
        int childKind = 0;
        int mutationSeed = 0;
        int parentRank = -1;
        int poolSize = measured.Count;
        float parentFitness = 0f;
        bool parentBest = false;
        string mutationFamily = "";
        int selectionPoolSize = 0;

        bool eliteTurn = measured.Count >= EliteMinMeasured
            && spawnIndex % EliteEverySpawns == 0
            && measured.Count > 0;

        if (spawnIndex == 0)
        {
            // WALK CYCLE 50: the run's REFERENCE body - the first spawn of every run
            // carries ArenaGenome.Defaults, the values the walk was verified on. The
            // sweep still measures it like any other body (child_kind 3 names it in
            // the log), so the arena's own reference row is comparable with the trial
            // row of record. The rng draw the fresh branch would have made for id 1 is
            // not made, so the genomes of the later bodies shift by one draw: the
            // arena's rows are not the pre-50 rows, but the same seed still replays
            // the same rows (A4's determinism).
            genome = ArenaGenome.Copy(ArenaGenome.Defaults);
            childKind = ReferenceChildKind;
            mutationSeed = -1;
        }
        else if (pendingEliteCopyGenome != null)
        {
            // The second half of the elite pair: the identical genome, the identical
            // spawn point, a different age. Two copies are spawned back to back so the
            // collision proof always has a pair, even when the best genome moves on.
            genome = ArenaGenome.Copy(pendingEliteCopyGenome);
            parentId = pendingEliteCopyParentId;
            childKind = 2;
            mutationSeed = -1;
            parentRank = 0;
            parentBest = true;
            pendingEliteCopyGenome = null;
        }
        else if (eliteTurn)
        {
            ArenaHumanAgent best = BestEligibleOfWindow();
            genome = ArenaGenome.Copy(best.genome);
            parentId = best.id;
            childKind = 2;
            mutationSeed = -1;
            parentRank = RankOf(best);
            parentFitness = best.Fitness;
            parentBest = true;
            pendingEliteCopyGenome = ArenaGenome.Copy(genome);
            pendingEliteCopyParentId = best.id;
            // WALK CYCLE 53: the champion's turnover over the run - the generations
            // the report reads. A new id here means the window's best moved on.
            if (best.id != lastEliteId) { elitePickIds.Add(best.id); lastEliteId = best.id; }
        }
        else if (poolSize > 0 && rng.Next01() < ChildShare)
        {
            List<ArenaHumanAgent> pool = SelectionPool();
            ArenaHumanAgent parent = TournamentPick(pool);
            genome = ArenaGenome.Copy(parent.genome);
            mutationSeed = rng.NextSeed();
            // WALK CYCLE 53: one family or the whole genome; the family's members
            // share one signed share of their own ranges (see ArenaGenome).
            int family = -1;
            float jMin = fineSchedule ? FineJitterMin : CoarseJitterMin;
            float jMax = fineSchedule ? FineJitterMax : CoarseJitterMax;
            if (rng.Next01() < OneFamilyShare)
            {
                family = rng.NextInt(ArenaGenome.FamilyCount);
                ArenaGenome.JitterFamily(genome, new ArenaRng(mutationSeed), family, jMin, jMax,
                    jitterAttempts, jitterClamped);
                oneFamilyChildren++;
            }
            else
            {
                ArenaGenome.Jitter(genome, new ArenaRng(mutationSeed), jMin, jMax,
                    jitterAttempts, jitterClamped);
                wholeGenomeChildren++;
            }
            mutationFamily = ArenaGenome.FamilyName(family);
            int seen;
            moveFamilyCounts.TryGetValue(mutationFamily, out seen);
            moveFamilyCounts[mutationFamily] = seen + 1;
            selectionPoolSize = pool.Count;
            parentId = parent.id;
            childKind = 1;
            parentRank = RankOf(parent);
            parentFitness = parent.Fitness;
            parentBest = pool.Contains(parent);
            inheritedChildren++;
            if (parentBest) parentInQuartileCount++;
            if (poolSize > 1)
                parentPercentiles.Add(100f * (1f - parentRank / (float)(poolSize - 1)));
        }
        else
        {
            genome = ArenaGenome.Random(rng);
            mutationSeed = rng.NextSeed();
            childKind = 0;
        }

        spawnIndex++;
        spawnCount++;
        if (intervalSamples > 0)
        {
            float gap = time - lastSpawnTime;
            intervalSum += gap;
            intervalMin = Mathf.Min(intervalMin, gap);
            intervalMax = Mathf.Max(intervalMax, gap);
        }
        lastSpawnTime = time;
        intervalSamples++;

        if (alive.Count >= aliveCap)
        {
            Debug.LogWarning("ARENA_CAP alive cap " + aliveCap + " reached at t=" +
                time.ToString("0.###", CultureInfo.InvariantCulture) + " - culling the oldest");
            ArenaHumanAgent oldest = alive[0];
            Cull(oldest, time);
        }

        ArenaHumanAgent agent = SpawnBody(id, genome, childKind, mutationSeed, parentId,
            parentRank, poolSize, parentFitness, parentBest, time, mutationFamily, selectionPoolSize);
        if (agent == null) return;
        alive.Add(agent);
        all.Add(agent);
        if (childKind == 2) eliteCopies.Add(agent);
        maxAlive = Mathf.Max(maxAlive, alive.Count);
    }

    private ArenaHumanAgent SpawnBody(int id, float[] genome, int childKind, int mutationSeed,
        int parentId, int parentRank, int poolSize, float parentFitness, bool parentBest, float time,
        string mutationFamily, int selectionPoolSize)
    {
        GameObject humanObject = new GameObject("ArenaHuman_" + id);
        humanObject.transform.position = new Vector2(spawnX, spawnY);

        Human human = humanObject.AddComponent<Human>();
        // No build override is applied here on purpose: geometry, masses, friction,
        // muscle ceilings and gravity keep the rig defaults, so two humans of
        // different genomes differ only by the 13 genes.
        MotionIntent intent = humanObject.AddComponent<MotionIntent>();
        StepPhaseDriver stepDriver = humanObject.AddComponent<StepPhaseDriver>();
        stepDriver.externalDrive = true;

        human.BuildHuman();
        FactionStamp.Player(humanObject);

        PoseDriveController pose = humanObject.GetComponent<PoseDriveController>();
        if (pose == null)
        {
            Debug.LogError("ARENA_SPAWN no PoseDriveController on human " + id);
            Destroy(humanObject);
            return null;
        }
        pose.ApplyArenaGenome(genome, true);
        // WALK CYCLE 50: the accepted walk's own values, through the genome entry's
        // sibling call. Read with the class header's table: the onset is opened by
        // ArenaRunner.Tick once this body's window is over, so the key starts closed.
        pose.ApplyArenaWalkConfig(WalkAnkleCarryDeg, WalkAnkleRampSeconds, WalkTailHoldSeconds);
        intent.moveX = 0f;                  // the key opens at WalkOnsetSeconds

        SetLayerRecursively(humanObject, ArenaLayer);
        ArenaHumanAgent.AttachNumber(humanObject, id, numberHeight);

        ArenaHumanAgent agent = humanObject.AddComponent<ArenaHumanAgent>();
        agent.id = id;
        agent.childKind = childKind;
        agent.mutationSeed = mutationSeed;
        agent.mutationFamily = mutationFamily;
        agent.selectionPoolSize = selectionPoolSize;
        agent.parentId = parentId;
        agent.parentRank = parentRank;
        agent.poolSize = poolSize;
        agent.parentFitness = parentFitness;
        agent.parentInBestQuartile = parentBest;
        agent.genome = genome;
        agent.lawMode = lawMode;            // WALK CYCLE 58: the round's control/law switch
        agent.liftFloorMeters = liftFloorMeters;   // WALK CYCLE 61: the band's own knobs
        agent.clearShareBar = clearShareBar;
        agent.Bind(human, time, spawnX, Time.fixedDeltaTime, measureSeconds);
        return agent;
    }

    private static void SetLayerRecursively(GameObject go, int layer)
    {
        if (go == null) return;
        go.layer = layer;
        Transform t = go.transform;
        for (int i = 0; i < t.childCount; i++)
            SetLayerRecursively(t.GetChild(i).gameObject, layer);
    }

    // ─── WALK CYCLE 53: THE SLIDING WINDOW ─────────────────────────────────
    // The last SelectionWindowMeasured measured bodies. The pool is a WINDOW, not a
    // heap: a champion from the run's first minute is not a parent in its fifth
    // unless it is still inside the window (and still eligible). That is what turns
    // a 900-body blob into ~7 generations.
    private List<ArenaHumanAgent> SelectionWindow()
    {
        if (measured.Count <= SelectionWindowMeasured) return measured;
        return measured.GetRange(measured.Count - SelectionWindowMeasured, SelectionWindowMeasured);
    }

    // The pool's ELIGIBLE bodies (the pre-filter), best first. An ineligible body -
    // walked under 0.3 m in 15 s, or walked a non-monotone path - is never a parent:
    // no weight of the fitness can buy it a seat, which is the point of a pre-filter.
    private List<ArenaHumanAgent> EligibleSorted(List<ArenaHumanAgent> pool)
    {
        List<ArenaHumanAgent> sorted = new List<ArenaHumanAgent>(pool.Count);
        for (int i = 0; i < pool.Count; i++)
            if (pool[i].Eligible) sorted.Add(pool[i]);
        sorted.Sort((a, b) => b.Fitness.CompareTo(a.Fitness));
        return sorted;
    }

    // The tournament's pool: the best THIRD of the window's eligible bodies.
    private List<ArenaHumanAgent> SelectionPool()
    {
        List<ArenaHumanAgent> window = SelectionWindow();
        List<ArenaHumanAgent> eligible = EligibleSorted(window);
        if (eligible.Count == 0)
        {
            // No eligible body in the window. Selection falls back to the window's
            // fitness order, and the fallback is COUNTED, never silent: the summary's
            // fallbackPicks is the number that says the pre-filter may be too tight
            // for this population. (The alternative - no parent at all - would make
            // every spawn fresh blood, i.e. a random restart, without saying so.)
            fallbackPicks++;
            List<ArenaHumanAgent> byFitness = new List<ArenaHumanAgent>(window);
            byFitness.Sort((a, b) => b.Fitness.CompareTo(a.Fitness));
            return byFitness;
        }
        int size = Mathf.Max(1, Mathf.CeilToInt(eligible.Count * SelectionPoolFraction));
        if (size > eligible.Count) size = eligible.Count;
        return eligible.GetRange(0, size);
    }

    // The elite's source: the best ELIGIBLE body inside the window. The old code took
    // the best of the whole run, so the 45-s champion ruled to the end.
    private ArenaHumanAgent BestEligibleOfWindow()
    {
        List<ArenaHumanAgent> eligible = EligibleSorted(SelectionWindow());
        if (eligible.Count > 0) return eligible[0];
        return BestMeasured();
    }

    private ArenaHumanAgent BestMeasured()
    {
        ArenaHumanAgent best = null;
        float bestFitness = float.NegativeInfinity;
        for (int i = 0; i < measured.Count; i++)
        {
            float fitness = measured[i].Fitness;
            if (fitness > bestFitness)
            {
                bestFitness = fitness;
                best = measured[i];
            }
        }
        return best;
    }

    // WALK CYCLE 53: THE BEST-THIRD MEAN, sampled on the measured count. This series
    // is the step schedule's trigger - never the run's total mean: half the
    // population is fresh blood and elite twins, so the total mean cannot rise while
    // the search does its job. The gear drops to 3-5 pct once the mean has not risen
    // by PlateauRiseMeters over three checkpoints (75 measured bodies).
    private void SampleBestThirdMean()
    {
        List<ArenaHumanAgent> eligible = EligibleSorted(SelectionWindow());
        if (eligible.Count == 0) return;
        int size = Mathf.Max(1, Mathf.CeilToInt(eligible.Count * SelectionPoolFraction));
        if (size > eligible.Count) size = eligible.Count;
        float sum = 0f;
        for (int i = 0; i < size; i++) sum += eligible[i].Fitness;
        bestThirdMeanSeries.Add(sum / size);
        bestThirdMeanAtMeasured.Add(measuredCount);
        int n = bestThirdMeanSeries.Count;
        if (!fineSchedule && n >= 4 && bestThirdMeanSeries[n - 1] - bestThirdMeanSeries[n - 4] < PlateauRiseMeters)
            fineSchedule = true;
    }

    private ArenaHumanAgent ById(int id)
    {
        return (id >= 1 && id <= all.Count) ? all[id - 1] : null;
    }

    // WALK CYCLE 53: THE EFFECTIVE POPULATION - distinct ancestors among the best
    // third of the window. It replaces the concept's 15 pct dominance cap: that rule
    // needed a variance measure the cull + elite + fresh mix does not leave defined,
    // while a count of ancestors cannot be gamed by one lucky body. <5 says the
    // lineage has collapsed (add blood), >30 says the pool is a lottery (selection
    // is not choosing).
    private void EffectivePopulation(out int distinctParents, out int distinctRoots, out int poolBodies)
    {
        List<ArenaHumanAgent> eligible = EligibleSorted(SelectionWindow());
        int size = Mathf.Max(1, Mathf.CeilToInt(eligible.Count * SelectionPoolFraction));
        if (size > eligible.Count) size = eligible.Count;
        HashSet<int> parents = new HashSet<int>();
        HashSet<int> roots = new HashSet<int>();
        for (int i = 0; i < size && i < eligible.Count; i++)
        {
            ArenaHumanAgent a = eligible[i];
            if (a.parentId >= 0) parents.Add(a.parentId);
            int id = a.id;
            int depth = 0;
            ArenaHumanAgent node = a;
            while (node != null && node.parentId >= 0 && depth < 64)
            {
                id = node.parentId;
                node = ById(node.parentId);
                depth++;
            }
            roots.Add(id);
        }
        distinctParents = parents.Count;
        distinctRoots = roots.Count;
        poolBodies = eligible.Count;
    }

    // Tournament: draw TournamentSize candidates from the pool, keep the fittest.
    private ArenaHumanAgent TournamentPick(List<ArenaHumanAgent> pool)
    {
        if (pool == null || pool.Count == 0) return measured[measured.Count - 1];
        int draws = Mathf.Max(1, Mathf.RoundToInt(TournamentSize));
        ArenaHumanAgent best = null;
        float bestFitness = float.NegativeInfinity;
        for (int i = 0; i < draws; i++)
        {
            ArenaHumanAgent candidate = pool[rng.NextInt(pool.Count)];
            if (candidate.Fitness > bestFitness)
            {
                bestFitness = candidate.Fitness;
                best = candidate;
            }
        }
        return best;
    }

    private int RankOf(ArenaHumanAgent agent)
    {
        int rank = 0;
        for (int i = 0; i < measured.Count; i++)
        {
            if (measured[i] == agent) continue;
            if (measured[i].Fitness > agent.Fitness) rank++;
        }
        return rank;
    }

    // ─── measurement and cull ──────────────────────────────────────────────
    private void MeasureDue(float time)
    {
        for (int i = 0; i < alive.Count; i++)
        {
            ArenaHumanAgent agent = alive[i];
            if (agent.measuredTime >= 0f) continue;
            if (time - agent.spawnTime + 1e-4f < measureSeconds) continue;
            agent.MarkMeasured(time);
            measuredCount++;
            measured.Add(agent);
            // WALK CYCLE 58: the HUD's own best (the window distance, the rank's
            // tiebreak territory read out loud) - visual state, never a weight.
            if (bestWindowId < 0 || agent.windowDistance > bestWindowDistance)
            {
                bestWindowDistance = agent.windowDistance;
                bestWindowId = agent.id;
            }
            AppendInfluenceRow(agent);
            // WALK CYCLE 53: the pre-filter's own census and the step schedule's
            // trigger, both read off the measured bodies as they arrive.
            if (agent.Eligible) eligibleMeasured++; else ineligibleMeasured++;
            if (measuredCount % QuartileCheckpointMeasured == 0) SampleBestThirdMean();
        }
    }

    private void CullDue(float time)
    {
        for (int i = alive.Count - 1; i >= 0; i--)
        {
            ArenaHumanAgent agent = alive[i];
            if (time - agent.spawnTime + 1e-4f < cullSeconds) continue;
            Cull(agent, time);
        }
    }

    private void Cull(ArenaHumanAgent agent, float time)
    {
        agent.MarkCulled(time, agent.UprightNow());
        culledCount++;
        alive.Remove(agent);
        // DestroyImmediate, not Destroy: a deferred Destroy lands at the end of whatever
        // rendered frame happens to be current, so the number of bodies in the physics
        // world then depends on the frame rate and two runs of one seed can drift apart.
        // Removing the body inside the fixed step keeps the world identical for both runs.
        if (agent.gameObject != null)
            DestroyImmediate(agent.gameObject);
    }

    private void SampleCurves(float time)
    {
        if (time + 1e-4f < nextAliveSample) return;
        nextAliveSample += 0.5f;
        aliveCurve.Add("[" + time.ToString("0.##", CultureInfo.InvariantCulture) + ","
            + alive.Count.ToString(CultureInfo.InvariantCulture) + "]");

        int overlaps = CountOverlaps();
        if (overlaps > overlapMax)
        {
            overlapMax = overlaps;
            overlapMaxTime = time;
        }

        // The RAM claim needs a body count, not an opinion: count the real
        // Rigidbody2D objects in the world every 5 s (a full scan is not cheap).
        if (time + 1e-4f >= nextRigidbodySample)
        {
            nextRigidbodySample += 5f;
            Rigidbody2D[] bodies = FindObjectsByType<Rigidbody2D>(FindObjectsSortMode.None);
            maxRigidbodies = Mathf.Max(maxRigidbodies, bodies.Length);
        }

        if (time + 1e-4f < nextWallSample) return;
        nextWallSample += 1f;
        float wall = Time.realtimeSinceStartup;
        float wallDelta = wall - lastWallSampleTime;
        lastWallSampleTime = wall;
        wallCurve.Add("[" + time.ToString("0.##", CultureInfo.InvariantCulture) + ","
            + (wallDelta / Mathf.Max(0.001f, 1f)).ToString("0.###", CultureInfo.InvariantCulture) + "]");
    }

    // Humans whose pelvis stands within 0.4 m of another human's pelvis: the
    // numeric side of "several humans visibly overlap without pushing each other".
    private int CountOverlaps()
    {
        int count = 0;
        for (int i = 0; i < alive.Count; i++)
        {
            float xi = alive[i].Distance;
            for (int j = i + 1; j < alive.Count; j++)
            {
                float xj = alive[j].Distance;
                if (Mathf.Abs(xi - xj) < 0.4f)
                {
                    count++;
                    break;
                }
            }
        }
        return count;
    }

    // ─── camera ────────────────────────────────────────────────────────────
    public Camera EnsureArenaCamera(float size, float offsetX)
    {
        if (arenaCamera == null)
        {
            arenaCamera = Camera.main;
            if (arenaCamera == null)
                arenaCamera = FindFirstObjectByType<Camera>();
        }
        if (arenaCamera == null)
        {
            GameObject go = new GameObject("ArenaCamera");
            arenaCamera = go.AddComponent<Camera>();
            go.tag = "MainCamera";
            arenaCamera.clearFlags = CameraClearFlags.SolidColor;
            arenaCamera.backgroundColor = SceneLighting.NightSky;
        }
        arenaCamera.orthographic = true;
        SetLens(size, spawnX + offsetX);
        return arenaCamera;
    }

    public void SetLens(float size, float centerX)
    {
        if (arenaCamera == null) return;
        arenaCamera.orthographic = true;
        arenaCamera.orthographicSize = size;
        arenaCamera.transform.position = new Vector3(centerX, -0.2f, -10f);
    }

    // ─── ARENA CYCLE 59: THE FOLLOW (visual runs only) ─────────────────────
    // One call per simulated step, from Tick, once the bodies have moved. Nothing
    // here is read by the measurement and no RNG draw happens here: the lens is not
    // a physics object, so the recorded rows cannot move (proved by the round's own
    // fence: the same seed re-run on the edit's player, body for body). With no
    // camera - a -nographics run - the whole block is skipped and costs nothing.
    private void UpdateLeaderCamera(float dt)
    {
        if (arenaCamera == null) return;
        float halfW = arenaCamera.orthographicSize
            * (Application.isBatchMode ? ShotAspect : arenaCamera.aspect);
        float floorX = spawnX + cameraOffsetX;      // the accepted framing: never further left
        float leaderX = spawnX;
        bool anyAlive = false;
        for (int i = 0; i < alive.Count; i++)
        {
            float x = spawnX + alive[i].Distance;
            if (!anyAlive || x > leaderX) leaderX = x;
            anyAlive = true;
        }
        if (!cameraPinned)
        {
            cameraPinX = floorX;                    // the first step places the accepted lens
            cameraPinned = true;
        }
        if (!cameraFollowLeader)
        {
            SetLens(arenaCamera.orthographicSize, floorX);   // the control take: the fixed lens
            cameraPinX = floorX;
            TrackLens(leaderX, halfW, anyAlive);
            return;
        }
        if (anyAlive)
        {
            float pinX = Mathf.Max(floorX, leaderX + cameraLeadMeters - halfW);
            float limitX = Mathf.Max(floorX, leaderX + ArenaCameraMinLeadMeters - halfW);
            float tau = pinX >= cameraPinX ? ArenaCameraFollowTauSeconds : ArenaCameraReturnTauSeconds;
            cameraPinX = Mathf.Lerp(cameraPinX, pinX, 1f - Mathf.Exp(-Mathf.Max(0.0005f, dt) / tau));
            if (cameraPinX < limitX) cameraPinX = limitX;    // the leader never comes closer than the guard
            if (cameraPinX < floorX) cameraPinX = floorX;
        }
        SetLens(arenaCamera.orthographicSize, cameraPinX);
        TrackLens(leaderX, halfW, anyAlive);
    }

    // The take's own trace: what the lens did and where the frame's edges stood
    // relative to the furthest body. The numbers a reader needs to check "the view
    // rode with the leader" without watching the clip. Visual runs only; the rows
    // land beside the run's table and in the summary.
    private void TrackLens(float leaderX, float halfW, bool anyAlive)
    {
        float left = cameraPinX - halfW;
        float right = cameraPinX + halfW;
        cameraFrameLeftMin = Mathf.Min(cameraFrameLeftMin, left);
        cameraFrameRightMax = Mathf.Max(cameraFrameRightMax, right);
        cameraCenterMin = Mathf.Min(cameraCenterMin, cameraPinX);
        cameraCenterMax = Mathf.Max(cameraCenterMax, cameraPinX);
        if (anyAlive)
        {
            cameraLeaderMax = Mathf.Max(cameraLeaderMax, leaderX);
            cameraLeadMin = Mathf.Min(cameraLeadMin, right - leaderX);
            cameraLeadMax = Mathf.Max(cameraLeadMax, right - leaderX);
        }
        if (simTime + 1e-4f < nextCameraSample) return;
        nextCameraSample += ArenaCameraSampleSeconds;
        cameraSamples++;
        cameraRows.Add(simTime.ToString("0.##", CultureInfo.InvariantCulture)
            + "," + alive.Count.ToString(CultureInfo.InvariantCulture)
            + "," + Num(leaderX) + "," + Num(cameraPinX)
            + "," + Num(left) + "," + Num(right) + "," + Num(right - leaderX));
    }

    // ─── WALK CYCLE 58: THE VISUAL LAYER (visual runs only; no physics) ────
    // One HUD line for Yury's own runs: t / spawned / measured / best window
    // distance + id. Built only when a camera exists (a -nographics run builds
    // nothing), a TextMesh with no collider and no rigidbody, never read by any
    // measurement - the same discipline the dog arena's c69 take proved.
    private void EnsureVisualLayer()
    {
        if (arenaCamera == null) return;
        if (hudMesh == null) AttachHud();
        hudSteps++;
        if (hudSteps >= HudUpdateSteps)
        {
            hudSteps = 0;
            UpdateHud(simTime);
        }
    }

    private void AttachHud()
    {
        if (arenaCamera == null) return;
        GameObject hud = new GameObject("ArenaHud");
        hud.layer = arenaCamera.gameObject.layer;
        hud.transform.SetParent(arenaCamera.transform, false);
        hud.transform.localRotation = Quaternion.identity;
        hud.transform.localPosition = new Vector3(0f, 0f, 1.5f);   // in front of the camera's plane; UpdateHud pins x/y

        TextMesh textMesh = hud.AddComponent<TextMesh>();
        Font font = ArenaHumanAgent.LabelFont();
        if (font != null) textMesh.font = font;
        textMesh.text = "t 0.0s";
        textMesh.fontSize = 72;
        textMesh.characterSize = Mathf.Max(0.005f, HudDigitMeters * 10f / textMesh.fontSize);
        textMesh.anchor = TextAnchor.UpperLeft;
        textMesh.alignment = TextAlignment.Left;
        textMesh.color = new Color(0.95f, 0.95f, 1f, 1f);

        MeshRenderer renderer = hud.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            renderer.sortingOrder = 600;
            if (font != null && font.material != null)
                renderer.sharedMaterial = font.material;
        }
        hudMesh = textMesh;
        UpdateHud(0f);
    }

    private void UpdateHud(float simulated)
    {
        if (hudMesh == null || arenaCamera == null) return;
        hudMesh.text = "t " + simulated.ToString("0.0", CultureInfo.InvariantCulture) + "s"
            + "   spawn " + spawnCount.ToString(CultureInfo.InvariantCulture)
            + "   meas " + measured.Count.ToString(CultureInfo.InvariantCulture)
            + "   best " + (bestWindowId >= 0
                ? bestWindowDistance.ToString("0.00", CultureInfo.InvariantCulture) : "-")
            + "m id " + (bestWindowId >= 0 ? bestWindowId.ToString(CultureInfo.InvariantCulture) : "-");
        // Placed from the DRAWN bounds, not from the transform: the TextMesh anchor
        // is not the mesh's corner (the dog take's own measurement). One correction
        // per update keeps the left edge pinned while the clock's digits grow.
        if (hudRenderer == null) hudRenderer = hudMesh.GetComponent<MeshRenderer>();
        if (hudRenderer != null)
        {
            Bounds b = hudRenderer.bounds;
            if (b.size.x > 0.001f)
            {
                float camX = arenaCamera.transform.position.x;
                float camY = arenaCamera.transform.position.y;
                float halfH = arenaCamera.orthographicSize;
                float halfW = halfH * (Application.isBatchMode ? ShotAspect : arenaCamera.aspect);
                Vector3 p = hudMesh.transform.position;
                p.x += (camX - halfW + HudMarginMeters) - b.min.x;
                p.y += (camY + halfH - HudMarginMeters) - b.max.y;
                hudMesh.transform.position = p;
            }
        }
    }

    // ─── files ─────────────────────────────────────────────────────────────
    private string InfluenceLogPath => Path.Combine(outputFolder, "arena-param-log.csv");
    private string HumanCsvPath => Path.Combine(outputFolder, label + ".csv");
    private string CameraCsvPath => Path.Combine(outputFolder, label + "_camera.csv");
    private string SummaryPath => Path.Combine(outputFolder, label + ".json");
    private string FallbackRowPath => Path.Combine(outputFolder, label + "_paramrows.csv");
    private bool influenceHeaderChecked;
    private bool appendToInfluenceLog = true;

    private static string InfluenceHeader()
    {
        return "run_id,seed,human_id,parent_id,child_kind,spawn_t,distance_m,upright,fallen,"
            + "destroyed_t," + ArenaGenome.CsvHeader();
    }

    private bool CanAppendToInfluenceLog()
    {
        if (influenceHeaderChecked) return appendToInfluenceLog;
        influenceHeaderChecked = true;
        try
        {
            Directory.CreateDirectory(outputFolder);
            if (File.Exists(InfluenceLogPath))
            {
                string first = null;
                using (StreamReader reader = new StreamReader(InfluenceLogPath))
                    first = reader.ReadLine();
                if (first != null && first.Trim() != InfluenceHeader())
                {
                    // WALK CYCLE 64: the genome's ask-side tier appended three genes,
                    // so the header moved. The contract keeps the log append-only and
                    // never rewritten: the 13-gene epoch is ROTATED (its bytes kept
                    // under a new name) and a fresh log opens with the tier's header,
                    // so the arena's cross-run memory survives the expansion instead
                    // of falling to per-run paramrows files for every future run.
                    string epoch = InfluenceLogPath + ".epoch-13gene";
                    try
                    {
                        if (!File.Exists(epoch)) File.Move(InfluenceLogPath, epoch);
                        else File.Copy(InfluenceLogPath, epoch, true);
                        File.WriteAllText(InfluenceLogPath, InfluenceHeader() + "\n");
                        Debug.LogError("ARENA_CSV_HEADER_MISMATCH " + InfluenceLogPath
                            + " header moved with the genome tier - the old epoch is kept at "
                            + epoch + "; a fresh log opens with the 16-gene header");
                    }
                    catch (System.Exception e)
                    {
                        appendToInfluenceLog = false;
                        Debug.LogError("ARENA_CSV_ROTATE_FAILED " + e.Message
                            + " - rows go to " + FallbackRowPath);
                    }
                }
            }
            else
            {
                File.WriteAllText(InfluenceLogPath, InfluenceHeader() + "\n");
            }
        }
        catch (System.Exception e)
        {
            appendToInfluenceLog = false;
            Debug.LogError("ARENA_CSV_OPEN_FAILED " + e.Message + " - rows go to " + FallbackRowPath);
        }
        return appendToInfluenceLog;
    }

    private void AppendInfluenceRow(ArenaHumanAgent agent)
    {
        if (string.IsNullOrEmpty(outputFolder)) return;
        string path = CanAppendToInfluenceLog() ? InfluenceLogPath : FallbackRowPath;
        try
        {
            string line = string.Join(",",
                runId,
                seed.ToString(CultureInfo.InvariantCulture),
                agent.id.ToString(CultureInfo.InvariantCulture),
                agent.parentId.ToString(CultureInfo.InvariantCulture),
                agent.childKind.ToString(CultureInfo.InvariantCulture),
                agent.spawnTime.ToString("0.###", CultureInfo.InvariantCulture),
                agent.distanceAt15.ToString("0.#####", CultureInfo.InvariantCulture),
                agent.uprightAt15 ? "1" : "0",
                agent.fallen ? "1" : "0",
                "",                       // destroyed_t is filled into the CSV at cull time
                ArenaGenome.CsvValues(agent.genome));
            // destroyed_t unknown at 15 s: the human is still walking. The row is
            // rewritten by RewriteInfluenceRow at cull with the real time.
            File.AppendAllText(path, line + "\n");
        }
        catch (System.Exception e)
        {
            Debug.LogError("ARENA_CSV_WRITE_FAILED " + e.Message);
        }
    }

    // At cull the destroyed_t of an already appended row is filled in place: the
    // contract wants one row per measured human, and the row must not be written
    // twice. Only that one field of that one line is touched.
    private void FillDestroyedTime(ArenaHumanAgent agent)
    {
        if (string.IsNullOrEmpty(outputFolder)) return;
        string path = CanAppendToInfluenceLog() ? InfluenceLogPath : FallbackRowPath;
        try
        {
            if (!File.Exists(path)) return;
            string[] lines = File.ReadAllLines(path);
            string prefix = runId + "," + seed.ToString(CultureInfo.InvariantCulture) + ","
                + agent.id.ToString(CultureInfo.InvariantCulture) + ",";
            for (int i = 1; i < lines.Length; i++)
            {
                if (!lines[i].StartsWith(prefix, System.StringComparison.Ordinal)) continue;
                string[] fields = lines[i].Split(',');
                if (fields.Length < 10) return;
                // -1 means "never destroyed inside this run": the contract column is
                // left empty rather than filled with a number that did not happen.
                fields[9] = agent.destroyedTime >= 0f
                    ? agent.destroyedTime.ToString("0.###", CultureInfo.InvariantCulture)
                    : "";
                lines[i] = string.Join(",", fields);
                // WALK CYCLE 58: File.WriteAllLines normalised every line ending in the
                // file to CRLF (Environment.NewLine on Windows), so the log's byte prefix
                // no longer survived a cull fill and the lane's append-only proof
                // (startswith, byte for byte) failed at the first filled row. Splice the
                // one changed line back in place instead: every other byte - line
                // endings included - is left exactly as it was.
                SpliceLine(path, i, lines[i]);
                return;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("ARENA_CSV_FILL_FAILED " + e.Message);
        }
    }

    // WALK CYCLE 58: replace ONE line of a text file without touching any other byte.
    // The append-only log is proven by prefix (a byte startswith against the round's
    // own before-copy), so a cull fill may not re-encode its neighbours: the line's own
    // ending (LF or CRLF, whatever it was) is copied back verbatim.
    private static void SpliceLine(string path, int lineIndex, string newLine)
    {
        byte[] data = File.ReadAllBytes(path);
        List<int> starts = new List<int>();
        starts.Add(0);
        for (int i = 0; i < data.Length; i++)
            if (data[i] == (byte)'\n' && i + 1 < data.Length) starts.Add(i + 1);
        if (lineIndex < 0 || lineIndex >= starts.Count) return;
        int lineStart = starts[lineIndex];
        int lineEnd = lineIndex + 1 < starts.Count ? starts[lineIndex + 1] : data.Length;
        int eolStart = lineEnd;
        while (eolStart > lineStart && (data[eolStart - 1] == (byte)'\n' || data[eolStart - 1] == (byte)'\r'))
            eolStart--;
        byte[] replacement = System.Text.Encoding.UTF8.GetBytes(newLine);
        using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write))
        {
            stream.Write(data, 0, lineStart);
            stream.Write(replacement, 0, replacement.Length);
            stream.Write(data, eolStart, data.Length - eolStart);
        }
    }

    // ARENA CYCLE 59: the take's own trace, one row per ArenaCameraSampleSeconds.
    // Visual runs only (the caller writes it only when samples exist).
    public string BuildCameraCsv()
    {
        StringBuilder sb = new StringBuilder(1024 * 16);
        sb.Append("t,alive,leader_x,camera_x,frame_left,frame_right,lead_m\n");
        for (int i = 0; i < cameraRows.Count; i++)
            sb.Append(cameraRows[i]).Append('\n');
        return sb.ToString();
    }

    public string BuildHumanCsv()
    {
        StringBuilder sb = new StringBuilder(1024 * 64);
        sb.Append("run_id,seed,human_id,parent_id,child_kind,mutation_seed,spawn_t,measured_t,");
        sb.Append("distance_15s,upright_15s,fallen,fall_t,tilt_at_fall,destroyed_t,distance_30s,");
        sb.Append("upright_at_cull,alive_at_cull,parent_rank,pool_size,parent_fitness,parent_best_quartile,");
        sb.Append("trajectory_samples,");
        sb.Append(ArenaGenome.CsvHeader());
        // WALK CYCLE 53: appended AFTER the fixed gene block, so a positional reader
        // of the 13 genes keeps working. The family that moved is per child (the
        // reviewers asked for the pair, not the tier); eligible/monotone_share are the
        // pre-filter's own inputs, kept next to its verdict.
        sb.Append(",mutation_family,selection_pool_size,eligible,monotone_share,fall_distance");
        // WALK CYCLE 58: the window's own columns (the line's bars). Appended after
        // the c53 block so a positional reader of the older columns keeps working.
        sb.Append(",law,on_walk_census,forward_walk,walk_steps,swing_lift,swing_lift_l,swing_lift_r");
        sb.Append(",window_seconds,window_distance,monotone_share_window");
        // WALK CYCLE 61: the band's own measured columns (the shape half).
        sb.Append(",swing_clear_share,swing_clear_share_l,swing_clear_share_r");
        sb.Append('\n');
        for (int i = 0; i < all.Count; i++)
        {
            ArenaHumanAgent a = all[i];
            sb.Append(runId).Append(',');
            sb.Append(seed.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append(a.id.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append(a.parentId.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append(a.childKind.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append(a.mutationSeed.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append(a.spawnTime.ToString("0.###", CultureInfo.InvariantCulture)).Append(',');
            sb.Append(a.measuredTime.ToString("0.###", CultureInfo.InvariantCulture)).Append(',');
            sb.Append(Fmt(a.distanceAt15)).Append(',');
            sb.Append(a.uprightAt15 ? "1" : "0").Append(',');
            sb.Append(a.fallen ? "1" : "0").Append(',');
            sb.Append(a.fallTime.ToString("0.###", CultureInfo.InvariantCulture)).Append(',');
            sb.Append(Fmt(a.tiltAtFall)).Append(',');
            sb.Append(a.destroyedTime.ToString("0.###", CultureInfo.InvariantCulture)).Append(',');
            sb.Append(Fmt(a.distanceAtCull)).Append(',');
            sb.Append(a.uprightAtCull ? "1" : "0").Append(',');
            sb.Append(a.aliveAtCull ? "1" : "0").Append(',');
            sb.Append(a.parentRank.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append(a.poolSize.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append(Fmt(a.parentFitness)).Append(',');
            sb.Append(a.parentInBestQuartile ? "1" : "0").Append(',');
            sb.Append(a.TrajectorySamples.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append(ArenaGenome.CsvValues(a.genome));
            sb.Append(',').Append(a.mutationFamily);
            sb.Append(',').Append(a.selectionPoolSize.ToString(CultureInfo.InvariantCulture));
            sb.Append(',').Append(a.Eligible ? "1" : "0");
            sb.Append(',').Append(Fmt(a.monotoneShare));
            sb.Append(',').Append(Fmt(a.fallDistance));
            sb.Append(',').Append(a.lawMode.ToString(CultureInfo.InvariantCulture));
            sb.Append(',').Append(a.onWalkCensus ? "1" : "0");
            sb.Append(',').Append(a.forwardWalk ? "1" : "0");
            sb.Append(',').Append(a.walkSteps.ToString(CultureInfo.InvariantCulture));
            sb.Append(',').Append(Fmt(a.swingLift));
            sb.Append(',').Append(Fmt(a.swingLiftL));
            sb.Append(',').Append(Fmt(a.swingLiftR));
            sb.Append(',').Append(Fmt(a.windowSeconds));
            sb.Append(',').Append(Fmt(a.windowDistance));
            sb.Append(',').Append(Fmt(a.monotoneShareWindow));
            sb.Append(',').Append(Fmt(a.swingClearShare));
            sb.Append(',').Append(Fmt(a.swingClearShareL));
            sb.Append(',').Append(Fmt(a.swingClearShareR));
            sb.Append('\n');
        }
        return sb.ToString();
    }

    private static string Fmt(float value)
    {
        if (float.IsNaN(value)) return "";
        return value.ToString("0.#####", CultureInfo.InvariantCulture);
    }

    // JSON has no empty number: an unmeasured value is null, never "".
    private static string Jnum(float value)
    {
        if (float.IsNaN(value)) return "null";
        return value.ToString("0.#####", CultureInfo.InvariantCulture);
    }

    public string BuildSummaryJson(float simulatedSeconds, float wallSeconds)
    {
        StringBuilder json = new StringBuilder(1024 * 64);
        json.Append('{');
        json.Append("\"label\":\"").Append(label).Append('"');
        json.Append(",\"runId\":\"").Append(runId).Append('"');
        json.Append(",\"arena\":true");
        json.Append(",\"headless\":").Append(headless ? "true" : "false");
        json.Append(",\"seed\":").Append(seed.ToString(CultureInfo.InvariantCulture));
        json.Append(",\"sourceFingerprint\":\"").Append(sourceFingerprint).Append('"');
        json.Append(",\"buildGuid\":\"").Append(buildGuid).Append('"');
        json.Append(",\"simulatedSeconds\":").Append(Num(simulatedSeconds));
        json.Append(",\"wallSeconds\":").Append(Num(wallSeconds));
        if (simulatedSeconds > 0f && wallSeconds >= 0f)
            json.Append(",\"wallPerSim\":").Append(Num(wallSeconds / simulatedSeconds));
        json.Append(",\"fixedDeltaTime\":").Append(Num(Time.fixedDeltaTime));

        json.Append(",\"config\":{");
        json.Append("\"spawnIntervalSeconds\":").Append(Num(spawnInterval));
        json.Append(",\"measureSeconds\":").Append(Num(measureSeconds));
        json.Append(",\"cullSeconds\":").Append(Num(cullSeconds));
        json.Append(",\"aliveCap\":").Append(aliveCap.ToString(CultureInfo.InvariantCulture));
        json.Append(",\"durationSeconds\":").Append(Num(durationSeconds));
        json.Append(",\"spawnPoint\":{\"x\":").Append(Num(spawnX)).Append(",\"y\":").Append(Num(spawnY)).Append('}');
        json.Append(",\"groundWidthMeters\":").Append(Num(groundWidth));
        json.Append(",\"numberHeightMeters\":").Append(Num(numberHeight));
        json.Append('}');

        // WALK CYCLE 50: the walk the spawned bodies were configured with, in the
        // artifact. Every value here is what ArenaRunner.SpawnBody wrote into each
        // body, so a reader can check the spawn's configuration against the accepted
        // trial line without reading the code.
        json.Append(",\"walk\":{\"owner\":\"PoseDriveController\"");
        json.Append(",\"onsetSeconds\":").Append(Num(WalkOnsetSeconds));
        json.Append(",\"ankleCarryDeg\":").Append(Num(WalkAnkleCarryDeg));
        json.Append(",\"ankleRampSeconds\":").Append(Num(WalkAnkleRampSeconds));
        json.Append(",\"tailHoldSeconds\":").Append(Num(WalkTailHoldSeconds));
        json.Append(",\"captureWalker\":1");
        json.Append(",\"referenceGenomeChildKind\":").Append(ReferenceChildKind.ToString(CultureInfo.InvariantCulture));
        json.Append(",\"acceptedTrialRow\":\"c6w38n_walk\"");
        json.Append('}');

        json.Append(",\"spawn\":{");
        json.Append("\"spawns\":").Append(spawnCount.ToString(CultureInfo.InvariantCulture));
        json.Append(",\"measured\":").Append(measuredCount.ToString(CultureInfo.InvariantCulture));
        json.Append(",\"culled\":").Append(culledCount.ToString(CultureInfo.InvariantCulture));
        json.Append(",\"maxAlive\":").Append(maxAlive.ToString(CultureInfo.InvariantCulture));
        json.Append(",\"aliveAtEnd\":").Append(alive.Count.ToString(CultureInfo.InvariantCulture));
        if (intervalSamples > 1)
        {
            float mean = intervalSum / (intervalSamples - 1);
            json.Append(",\"intervalMeanSeconds\":").Append(Num(mean));
            json.Append(",\"intervalMinSeconds\":").Append(Num(intervalMin));
            json.Append(",\"intervalMaxSeconds\":").Append(Num(intervalMax));
            json.Append(",\"intervalNominalSeconds\":").Append(Num(spawnInterval));
            json.Append(",\"intervalAssert\":\"").Append(
                Mathf.Abs(mean - spawnInterval) <= 0.001f ? "OK" : "DRIFT").Append('"');
        }
        if (measuredCount > 0)
        {
            // The card wants the sample exactly 15 s after the spawn, with the one
            // frame tolerance stated: report what the loop actually achieved.
            float minAge = float.MaxValue, maxAge = 0f, sumAge = 0f;
            for (int i = 0; i < measured.Count; i++)
            {
                float age = measured[i].measuredTime - measured[i].spawnTime;
                minAge = Mathf.Min(minAge, age);
                maxAge = Mathf.Max(maxAge, age);
                sumAge += age;
            }
            json.Append(",\"measureAgeMeanSeconds\":").Append(Num(sumAge / measured.Count));
            json.Append(",\"measureAgeMinSeconds\":").Append(Num(minAge));
            json.Append(",\"measureAgeMaxSeconds\":").Append(Num(maxAge));
        }
        json.Append(",\"measureToleranceSeconds\":").Append(Num(Time.fixedDeltaTime));
        json.Append(",\"aliveCurve\":[").Append(string.Join(",", aliveCurve)).Append(']');
        json.Append(",\"wallPerSimCurve\":[").Append(string.Join(",", wallCurve)).Append(']');
        json.Append(",\"maxRigidbodies\":").Append(maxRigidbodies.ToString(CultureInfo.InvariantCulture));
        json.Append(",\"overlapMaxHumans\":").Append(overlapMax.ToString(CultureInfo.InvariantCulture));
        json.Append(",\"overlapMaxTime\":").Append(Num(overlapMaxTime));
        json.Append('}');

        json.Append(",\"layer\":{");
        int layer = LayerMask.NameToLayer(ArenaLayerName);
        json.Append("\"name\":\"").Append(ArenaLayerName).Append('"');
        json.Append(",\"index\":").Append(layer.ToString(CultureInfo.InvariantCulture));
        json.Append(",\"selfPairIgnored\":").Append(
            (layer >= 0 && Physics2D.GetIgnoreLayerCollision(layer, layer)) ? "true" : "false");
        json.Append(",\"groundPairIgnored\":").Append(
            (layer >= 0 && Physics2D.GetIgnoreLayerCollision(layer, GroundLayers.Index)) ? "true" : "false");
        json.Append(",\"groundLayer\":").Append(GroundLayers.Index.ToString(CultureInfo.InvariantCulture));
        json.Append('}');

        // ARENA CYCLE 59: what the lens did. Written for every visual run, including a
        // control take with the follow switched off - a control that states its own
        // fixed lens is evidence, a silent one is a gap.
        json.Append(",\"camera\":{");
        json.Append("\"follow\":").Append(cameraFollowLeader ? "true" : "false");
        json.Append(",\"leadMeters\":").Append(Num(cameraLeadMeters));
        json.Append(",\"minLeadMeters\":").Append(Num(ArenaCameraMinLeadMeters));
        json.Append(",\"fixedCenterXMeters\":").Append(Num(spawnX + cameraOffsetX));
        json.Append(",\"lensSizeMeters\":").Append(Num(arenaCamera != null ? arenaCamera.orthographicSize : cameraSize));
        json.Append(",\"samples\":").Append(cameraSamples.ToString(CultureInfo.InvariantCulture));
        if (cameraSamples > 0)
        {
            json.Append(",\"leaderMaxMeters\":").Append(Num(cameraLeaderMax));
            json.Append(",\"frameRightMaxMeters\":").Append(Num(cameraFrameRightMax));
            json.Append(",\"frameLeftMinMeters\":").Append(Num(cameraFrameLeftMin));
            json.Append(",\"lensCenterMinMeters\":").Append(Num(cameraCenterMin));
            json.Append(",\"lensCenterMaxMeters\":").Append(Num(cameraCenterMax));
            json.Append(",\"leadMinMeters\":").Append(Num(cameraLeadMin));
            json.Append(",\"leadMaxMeters\":").Append(Num(cameraLeadMax));
        }
        json.Append(",\"curveFormat\":\"t,alive,leader_x,camera_x,frame_left,frame_right,lead_m\"");
        json.Append(",\"curve\":[");
        for (int i = 0; i < cameraRows.Count; i++)
        {
            if (i > 0) json.Append(',');
            json.Append('[').Append(cameraRows[i]).Append(']');
        }
        json.Append("]}");

        int distinctParents, distinctRoots, poolBodies;
        EffectivePopulation(out distinctParents, out distinctRoots, out poolBodies);

        json.Append(",\"evolution\":{");
        if (lawMode == ArenaHumanAgent.LawControl)
        {
            json.Append("\"fitness\":\"[law control: the c54 shape over the onset window] ")
                .Append("windowDistance + ").Append(Num(ArenaHumanAgent.StandRewardMetersPerSecond))
                .Append("*windowSeconds - ").Append(Num(ArenaHumanAgent.FallPriceMeters))
                .Append("*[fell inside window]\"");
        }
        else
        {
            json.Append("\"fitness\":\"[the line's own bars, cycle ")
                .Append(lawMode == ArenaHumanAgent.LawQuality ? "61" : "58").Append("] ")
                .Append(Num(ArenaHumanAgent.SurvivalWeight)).Append("*windowSeconds + ")
                .Append(Num(ArenaHumanAgent.DistanceTiebreakWeight)).Append("*windowDistance - ")
                .Append(Num(ArenaHumanAgent.FallPriceMeters))
                .Append("*[fell inside window] for the walk class; ")
                .Append(Num(ArenaHumanAgent.StandResidueWeight))
                .Append("*windowDistance outside it\"");
        }
        json.Append(",\"lawMode\":").Append(lawMode.ToString(CultureInfo.InvariantCulture));
        json.Append(",\"windowRule\":\"[spawn + onset ").Append(Num(WalkOnsetSeconds))
            .Append(" s, min(fallOnset, spawn + measure ").Append(Num(measureSeconds))
            .Append(" s)]; post-onset window ").Append(Num(ArenaHumanAgent.PostOnsetWindowSeconds)).Append(" s\"");
        if (lawMode == ArenaHumanAgent.LawQuality)
        {
            json.Append(",\"walkCensus\":\"steps >= ").Append(ArenaHumanAgent.WalkStepBar.ToString(CultureInfo.InvariantCulture))
                .Append(" by the plant clock AND swingLift >= ").Append(Num(liftFloorMeters))
                .Append(" m (min of the feet's peak sole clearance - the accepted line's own weaker leg)"
                    + " AND swingClearShare >= ")
                .Append(Num(clearShareBar))
                .Append(" per foot (the share of the window each foot spends clear of the ")
                .Append(Num(ArenaHumanAgent.SweepClearCeilingMeters)).Append(" m shuffle ceiling)\"");
        }
        else
        {
            json.Append(",\"walkCensus\":\"steps >= ").Append(ArenaHumanAgent.WalkStepBar.ToString(CultureInfo.InvariantCulture))
                .Append(" by the plant clock AND swingLift >= ").Append(Num(ArenaHumanAgent.SwingLiftBarMeters))
                .Append(" m (min of the feet's peak sole clearance)\"");
        }
        json.Append(",\"fitnessFallPriceMeters\":").Append(Num(ArenaHumanAgent.FallPriceMeters));
        json.Append(",\"fitnessStandRewardMetersPerSecond\":").Append(Num(ArenaHumanAgent.StandRewardMetersPerSecond));
        json.Append(",\"previousLawFallPenaltyMeters\":").Append(Num(ArenaGenome.FallPenaltyMeters));
        json.Append(",\"childShare\":").Append(Num(ChildShare));
        json.Append(",\"freshShare\":").Append(Num(1f - ChildShare));
        json.Append(",\"jitterShareOfRange\":\"").Append(fineSchedule ? "0.03-0.05" : "0.05-0.10").Append('"');
        json.Append(",\"stepSchedule\":\"").Append(fineSchedule ? "fine" : "coarse").Append('"');
        json.Append(",\"stepScheduleTrigger\":\"best-third mean over 3 checkpoints of ")
            .Append(QuartileCheckpointMeasured.ToString(CultureInfo.InvariantCulture))
            .Append(" measured < ").Append(Num(PlateauRiseMeters)).Append(" m\"");
        json.Append(",\"selection\":\"tournament of ").Append(Num(TournamentSize))
            .Append(" inside the best third of the last ")
            .Append(SelectionWindowMeasured.ToString(CultureInfo.InvariantCulture))
            .Append(" measured\"");
        json.Append(",\"selectionWindowMeasured\":").Append(SelectionWindowMeasured.ToString(CultureInfo.InvariantCulture));
        json.Append(",\"selectionPoolFraction\":").Append(Num(SelectionPoolFraction));
        json.Append(",\"tournamentSize\":").Append(Num(TournamentSize));
        if (lawMode == ArenaHumanAgent.LawControl)
        {
            json.Append(",\"preFilter\":\"distance < ").Append(Num(ArenaHumanAgent.MinDistance15Meters))
                .Append(" m OR monotone share < ").Append(Num(ArenaHumanAgent.MonotoneShareMin))
                .Append(" -> never a parent\"");
        }
        else
        {
            json.Append(",\"preFilter\":\"the walk class: steps >= ")
                .Append(ArenaHumanAgent.WalkStepBar.ToString(CultureInfo.InvariantCulture))
                .Append(" AND swingLift >= ").Append(Num(lawMode == ArenaHumanAgent.LawQuality
                    ? liftFloorMeters : ArenaHumanAgent.SwingLiftBarMeters))
                .Append(" m");
            if (lawMode == ArenaHumanAgent.LawQuality)
                json.Append(" AND swingClearShare >= ").Append(Num(clearShareBar)).Append(" per foot");
            json.Append(" AND window monotone share >= ").Append(Num(ArenaHumanAgent.MonotoneShareMin))
                .Append(" AND window distance >= ").Append(Num(ArenaHumanAgent.MinDistance15Meters))
                .Append(" m -> never a parent\"");
        }
        json.Append(",\"eligibleMeasured\":").Append(eligibleMeasured.ToString(CultureInfo.InvariantCulture));
        json.Append(",\"ineligibleMeasured\":").Append(ineligibleMeasured.ToString(CultureInfo.InvariantCulture));
        json.Append(",\"selectionFallbackPicks\":").Append(fallbackPicks.ToString(CultureInfo.InvariantCulture));
        json.Append(",\"eliteSource\":\"the best ELIGIBLE body of the window\"");
        json.Append(",\"eliteMinMeasured\":").Append(EliteMinMeasured.ToString(CultureInfo.InvariantCulture));
        json.Append(",\"eliteEverySpawns\":").Append(EliteEverySpawns.ToString(CultureInfo.InvariantCulture));
        json.Append(",\"eliteCopies\":").Append(eliteCopies.Count.ToString(CultureInfo.InvariantCulture));
        json.Append(",\"championIds\":[").Append(string.Join(",", ChampionIdStrings())).Append(']');
        json.Append(",\"championTurns\":").Append(elitePickIds.Count.ToString(CultureInfo.InvariantCulture));
        json.Append(",\"inheritedChildren\":").Append(inheritedChildren.ToString(CultureInfo.InvariantCulture));
        json.Append(",\"oneFamilyChildren\":").Append(oneFamilyChildren.ToString(CultureInfo.InvariantCulture));
        json.Append(",\"wholeGenomeChildren\":").Append(wholeGenomeChildren.ToString(CultureInfo.InvariantCulture));
        json.Append(",\"mutationFamilies\":").Append(MoveFamilyCensusJson());
        json.Append(",\"bestThirdMean\":[").Append(string.Join(",", MeanSeriesStrings())).Append(']');
        json.Append(",\"bestThirdMeanAtMeasured\":[")
            .Append(string.Join(",", bestThirdMeanAtMeasured.ConvertAll(v => v.ToString(CultureInfo.InvariantCulture)).ToArray()))
            .Append(']');
        json.Append(",\"bestThirdMeanFirst\":").Append(Num(bestThirdMeanSeries.Count > 0 ? bestThirdMeanSeries[0] : float.NaN));
        json.Append(",\"bestThirdMeanLast\":").Append(Num(bestThirdMeanSeries.Count > 0 ? bestThirdMeanSeries[bestThirdMeanSeries.Count - 1] : float.NaN));
        int n3 = bestThirdMeanSeries.Count;
        json.Append(",\"bestThirdMeanRiseLast3\":").Append(Num(n3 >= 4 ? bestThirdMeanSeries[n3 - 1] - bestThirdMeanSeries[n3 - 4] : float.NaN));
        json.Append(",\"effectivePopulation\":{\"bodies\":").Append(poolBodies.ToString(CultureInfo.InvariantCulture));
        json.Append(",\"distinctParents\":").Append(distinctParents.ToString(CultureInfo.InvariantCulture));
        json.Append(",\"distinctRootAncestors\":").Append(distinctRoots.ToString(CultureInfo.InvariantCulture));
        json.Append(",\"rule\":\"<5 distinct ancestors = add blood, >30 = selection is a lottery\"}");
        json.Append(",\"parentsInBestQuartile\":").Append(parentInQuartileCount.ToString(CultureInfo.InvariantCulture));
        json.Append(",\"parentQuartileFraction\":").Append(Num(inheritedChildren > 0
            ? parentInQuartileCount / (float)inheritedChildren : 0f));
        json.Append(",\"parentQuartileChanceFraction\":").Append(Num(SelectionPoolFraction));
        json.Append(",\"parentPercentileMean\":").Append(Num(MeanPercentile()));
        json.Append(",\"ranges\":").Append(ArenaGenome.RangesJson());
        json.Append('}');

        // WALK CYCLE 53: THE BOX AUDIT and the CLAMP PRESSURE, in the artifact. The
        // audit is the reviewer's rule (every default in the middle band of its box);
        // the pressure is the number that tells a gene at its optimum from a gene
        // stuck in the wall - per gene, the jittered values it received and how many
        // of them hit a bound.
        System.Collections.Generic.List<string> boxBad = ArenaGenome.AuditDefaultBand();
        json.Append(",\"boxAudit\":{\"rule\":\"(default-min)/(max-min) in [0.15,0.85]\"");
        json.Append(",\"ok\":").Append(boxBad.Count == 0 ? "true" : "false");
        json.Append(",\"fractions\":").Append(BoxFractionsJson());
        json.Append(",\"violations\":[");
        for (int i = 0; i < boxBad.Count; i++)
        {
            if (i > 0) json.Append(',');
            json.Append('"').Append(boxBad[i].Replace("\\", "/").Replace("\"", "'")).Append('"');
        }
        json.Append("]}");

        json.Append(",\"clampPressure\":{\"note\":\"share of a gene's jittered values that hit a box bound\"");
        json.Append(",\"perGene\":{");
        for (int i = 0; i < ArenaGenome.Count; i++)
        {
            if (i > 0) json.Append(',');
            json.Append('"').Append(ArenaGenome.Names[i]).Append("\":{\"attempts\":")
                .Append(jitterAttempts[i].ToString(CultureInfo.InvariantCulture))
                .Append(",\"clamped\":").Append(jitterClamped[i].ToString(CultureInfo.InvariantCulture))
                .Append(",\"share\":").Append(Num(jitterAttempts[i] > 0
                    ? jitterClamped[i] / (float)jitterAttempts[i] : 0f)).Append('}');
        }
        json.Append("}}");

        json.Append(",\"best\":");
        ArenaHumanAgent best = BestMeasured();
        // ReferenceEquals, not ==: a best human that has already been culled is a
        // destroyed Unity object, and `== null` would be true for it. Its data is
        // plain fields and is still the best genome of the run.
        if (ReferenceEquals(best, null))
        {
            json.Append("null");
        }
        else
        {
            json.Append('{');
            json.Append("\"humanId\":").Append(best.id.ToString(CultureInfo.InvariantCulture));
            json.Append(",\"fitness\":").Append(Num(best.Fitness));
            json.Append(",\"distance15\":").Append(Jnum(best.distanceAt15));
            json.Append(",\"eligible\":").Append(best.Eligible ? "true" : "false");
            json.Append(",\"monotoneShare\":").Append(Jnum(best.monotoneShare));
            json.Append(",\"windowDistance\":").Append(Jnum(best.windowDistance));
            json.Append(",\"windowSeconds\":").Append(Jnum(best.windowSeconds));
            json.Append(",\"walkSteps\":").Append(best.walkSteps.ToString(CultureInfo.InvariantCulture));
            json.Append(",\"swingLift\":").Append(Jnum(best.swingLift));
            json.Append(",\"swingClearShare\":").Append(Jnum(best.swingClearShare));
            json.Append(",\"swingClearShareL\":").Append(Jnum(best.swingClearShareL));
            json.Append(",\"swingClearShareR\":").Append(Jnum(best.swingClearShareR));
            json.Append(",\"onWalkCensus\":").Append(best.onWalkCensus ? "true" : "false");
            json.Append(",\"forwardWalk\":").Append(best.forwardWalk ? "true" : "false");
            json.Append(",\"fallen\":").Append(best.fallen ? "true" : "false");
            json.Append(",\"genome\":").Append(ArenaGenome.ToJson(best.genome));
            json.Append('}');
        }

        json.Append(",\"elitePairs\":[").Append(ElitePairsJson()).Append(']');
        json.Append(",\"humans\":[").Append(HumansJson()).Append(']');
        json.Append('}');
        return json.ToString();
    }

    private float MeanPercentile()
    {
        if (parentPercentiles.Count == 0) return 0f;
        float sum = 0f;
        for (int i = 0; i < parentPercentiles.Count; i++) sum += parentPercentiles[i];
        return sum / parentPercentiles.Count;
    }

    // WALK CYCLE 53 helpers for the artifact: the champion's turnover (the elite's
    // distinct ids in the order they were picked - the "generations observed" the
    // report reads), the best-third mean series, the box audit's per-gene fraction,
    // and the per-child family census.
    private string[] ChampionIdStrings()
    {
        string[] ids = new string[elitePickIds.Count];
        for (int i = 0; i < elitePickIds.Count; i++)
            ids[i] = elitePickIds[i].ToString(CultureInfo.InvariantCulture);
        return ids;
    }

    private string[] MeanSeriesStrings()
    {
        string[] v = new string[bestThirdMeanSeries.Count];
        for (int i = 0; i < bestThirdMeanSeries.Count; i++)
            v[i] = Num(bestThirdMeanSeries[i]);
        return v;
    }

    private string BoxFractionsJson()
    {
        StringBuilder sb = new StringBuilder(512);
        sb.Append('{');
        for (int i = 0; i < ArenaGenome.Count; i++)
        {
            if (i > 0) sb.Append(',');
            float range = ArenaGenome.Max[i] - ArenaGenome.Min[i];
            float f = range > 0f ? (ArenaGenome.Defaults[i] - ArenaGenome.Min[i]) / range : 0f;
            sb.Append('"').Append(ArenaGenome.Names[i]).Append("\":").Append(Num(f));
        }
        sb.Append('}');
        return sb.ToString();
    }

    private string MoveFamilyCensusJson()
    {
        StringBuilder sb = new StringBuilder(256);
        sb.Append('{');
        bool first = true;
        System.Collections.Generic.Dictionary<string, int> copy = moveFamilyCounts;
        for (int i = 0; i < ArenaGenome.FamilyCount; i++)
        {
            string name = ArenaGenome.FamilyNames[i];
            int c;
            copy.TryGetValue(name, out c);
            if (!first) sb.Append(',');
            first = false;
            sb.Append('"').Append(name).Append("\":").Append(c.ToString(CultureInfo.InvariantCulture));
        }
        int whole;
        copy.TryGetValue(ArenaGenome.WholeGenomeFamily, out whole);
        if (!first) sb.Append(',');
        sb.Append('"').Append(ArenaGenome.WholeGenomeFamily).Append("\":").Append(whole.ToString(CultureInfo.InvariantCulture));
        sb.Append('}');
        return sb.ToString();
    }

    // The collision proof. Each group is one genome re-spawned unchanged; the first
    // copy is the reference and every later copy must repeat its distance and its
    // early path. A younger copy that is deflected means humans still touch.
    private string ElitePairsJson()
    {
        StringBuilder sb = new StringBuilder(1024);
        bool first = true;
        for (int i = 0; i < eliteCopies.Count - 1; i++)
        {
            ArenaHumanAgent reference = eliteCopies[i];
            for (int j = i + 1; j < eliteCopies.Count; j++)
            {
                ArenaHumanAgent other = eliteCopies[j];
                if (!SameGenome(reference.genome, other.genome)) continue;
                if (!first) sb.Append(',');
                first = false;
                float maxPath = 0f, maxDx = 0f, maxDy = 0f;
                int tracked = 0;
                for (float age = 0.5f; age <= 15f; age += 0.5f)
                {
                    float x1, y1, x2, y2;
                    if (!reference.TrajectoryAt(age, out x1, out y1)) continue;
                    if (!other.TrajectoryAt(age, out x2, out y2)) continue;
                    float dx = Mathf.Abs(x2 - x1);
                    float dy = Mathf.Abs(y2 - y1);
                    maxDx = Mathf.Max(maxDx, dx);
                    maxDy = Mathf.Max(maxDy, dy);
                    maxPath = Mathf.Max(maxPath, Mathf.Sqrt(dx * dx + dy * dy));
                    tracked++;
                }
                sb.Append("{\"referenceId\":").Append(reference.id.ToString(CultureInfo.InvariantCulture));
                sb.Append(",\"copyId\":").Append(other.id.ToString(CultureInfo.InvariantCulture));
                sb.Append(",\"referenceAgeAtMeasure\":").Append(Num(reference.measuredTime - reference.spawnTime));
                sb.Append(",\"copyAgeAtMeasure\":").Append(Num(other.measuredTime - other.spawnTime));
                sb.Append(",\"referenceDistance15\":").Append(Jnum(reference.distanceAt15));
                sb.Append(",\"copyDistance15\":").Append(Jnum(other.distanceAt15));
                sb.Append(",\"distanceDeltaMeters\":").Append(Jnum(
                    Mathf.Abs(other.distanceAt15 - reference.distanceAt15)));
                sb.Append(",\"trajectorySamples\":").Append(tracked.ToString(CultureInfo.InvariantCulture));
                sb.Append(",\"maxTrajectoryDeltaMeters\":").Append(Num(maxPath));
                sb.Append(",\"maxTrajectoryDeltaX\":").Append(Num(maxDx));
                sb.Append(",\"maxTrajectoryDeltaY\":").Append(Num(maxDy));
                sb.Append(",\"sameGenome\":true}");
            }
        }
        return sb.ToString();
    }

    private static bool SameGenome(float[] a, float[] b)
    {
        if (a == null || b == null || a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++)
            if (Mathf.Abs(a[i] - b[i]) > 1e-6f) return false;
        return true;
    }

    private string HumansJson()
    {
        StringBuilder sb = new StringBuilder(1024 * 32);
        // Trajectories are heavy: every elite copy carries one (they are the collision
        // proof), the rest carry their sample count only.
        List<int> heavy = new List<int>();
        for (int i = 0; i < eliteCopies.Count; i++) heavy.Add(eliteCopies[i].id);
        for (int i = 0; i < all.Count; i++)
        {
            ArenaHumanAgent a = all[i];
            if (i > 0) sb.Append(',');
            sb.Append('{');
            sb.Append("\"id\":").Append(a.id.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"spawnT\":").Append(Num(a.spawnTime));
            sb.Append(",\"genome\":").Append(ArenaGenome.ToJson(a.genome));
            sb.Append(",\"parentId\":").Append(a.parentId.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"childKind\":").Append(a.childKind.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"mutationSeed\":").Append(a.mutationSeed.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"mutationFamily\":\"").Append(a.mutationFamily).Append('"');
            sb.Append(",\"selectionPoolSize\":").Append(a.selectionPoolSize.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"parentRank\":").Append(a.parentRank.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"poolSize\":").Append(a.poolSize.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"parentBestQuartile\":").Append(a.parentInBestQuartile ? "true" : "false");
            sb.Append(",\"measuredT\":").Append(Num(a.measuredTime));
            sb.Append(",\"ageAtMeasure\":").Append(Num(a.measuredTime - a.spawnTime));
            sb.Append(",\"distance15\":").Append(Jnum(a.distanceAt15));
            sb.Append(",\"eligible\":").Append(a.Eligible ? "true" : "false");
            sb.Append(",\"monotoneShare\":").Append(Jnum(a.monotoneShare));
            sb.Append(",\"fallDistance\":").Append(Jnum(a.fallDistance));
            // WALK CYCLE 58: the window's own terms (the line's bars).
            sb.Append(",\"law\":").Append(a.lawMode.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"onWalkCensus\":").Append(a.onWalkCensus ? "true" : "false");
            sb.Append(",\"forwardWalk\":").Append(a.forwardWalk ? "true" : "false");
            sb.Append(",\"walkSteps\":").Append(a.walkSteps.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"swingLift\":").Append(Jnum(a.swingLift));
            sb.Append(",\"swingLiftL\":").Append(Jnum(a.swingLiftL));
            sb.Append(",\"swingLiftR\":").Append(Jnum(a.swingLiftR));
            sb.Append(",\"swingClearShare\":").Append(Jnum(a.swingClearShare));
            sb.Append(",\"swingClearShareL\":").Append(Jnum(a.swingClearShareL));
            sb.Append(",\"swingClearShareR\":").Append(Jnum(a.swingClearShareR));
            sb.Append(",\"windowSeconds\":").Append(Jnum(a.windowSeconds));
            sb.Append(",\"windowDistance\":").Append(Jnum(a.windowDistance));
            sb.Append(",\"monotoneShareWindow\":").Append(Jnum(a.monotoneShareWindow));
            sb.Append(",\"upright15\":").Append(a.uprightAt15 ? "true" : "false");
            sb.Append(",\"fallen\":").Append(a.fallen ? "true" : "false");
            sb.Append(",\"fallT\":").Append(Num(a.fallTime));
            sb.Append(",\"tiltAtFallDeg\":").Append(Jnum(a.tiltAtFall));
            sb.Append(",\"destroyedT\":").Append(Num(a.destroyedTime));
            sb.Append(",\"distance30\":").Append(Jnum(a.distanceAtCull));
            sb.Append(",\"uprightAtCull\":").Append(a.uprightAtCull ? "true" : "false");
            sb.Append(",\"aliveAtCull\":").Append(a.aliveAtCull ? "true" : "false");
            sb.Append(",\"fitness\":").Append(Num(a.Fitness));
            sb.Append(",\"trajectorySamples\":").Append(a.TrajectorySamples.ToString(CultureInfo.InvariantCulture));
            if (heavy.Contains(a.id))
                sb.Append(",\"trajectory\":").Append(a.TrajectoryJson());
            sb.Append('}');
        }
        return sb.ToString();
    }

    private static string Num(float value)
    {
        if (float.IsNaN(value)) return "null";
        return value.ToString("0.######", CultureInfo.InvariantCulture);
    }

    // Write the run artifacts and return the summary. Safe to call twice: the second
    // call only refreshes the summary and the table, the append-only influence log is
    // written as rows are measured and is never rewritten.
    public string WriteFiles(float simulatedSeconds, float wallSeconds)
    {
        string summary = BuildSummaryJson(simulatedSeconds, wallSeconds);
        if (string.IsNullOrEmpty(outputFolder)) return summary;
        try
        {
            Directory.CreateDirectory(outputFolder);
            for (int i = 0; i < measured.Count; i++)
                FillDestroyedTime(measured[i]);
            File.WriteAllText(HumanCsvPath, BuildHumanCsv());
            File.WriteAllText(SummaryPath, summary);
            if (cameraSamples > 0)
            {
                File.WriteAllText(CameraCsvPath, BuildCameraCsv());
                Debug.Log("ARENA_CAMERA_CSV " + CameraCsvPath);
            }
            Debug.Log("ARENA_SUMMARY " + summary);
            Debug.Log("ARENA_CSV " + HumanCsvPath);
            Debug.Log("ARENA_LOG " + InfluenceLogPath);
        }
        catch (System.Exception e)
        {
            Debug.LogError("ARENA_WRITE_FAILED " + e.Message);
        }
        return summary;
    }

    // Called by HeadlessTrial for the frame markers.
    public string HumanCsvFilePath => HumanCsvPath;
}
