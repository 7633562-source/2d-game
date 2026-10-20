using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

// ─── DOG ARENA (cycle 64, 23.09.2026) — THE DOG LANE'S OWN EVOLUTIONARY FLOW ────────────────
//
// Yury's process decision (22.09 ~23:05): the arena is a first-class instrument. This runner is the
// dog lane's own, built on the PATTERN the human arena proved (Assets/ArenaRunner.cs) and not
// forked from it: the bodies are dogs, the genome is the dog's own law's levers, the fitness is
// the dog's own card's bars.
//
// THE CLOCK (the human side's own: a body every 500 ms, measured at a fixed window, destroyed after
// a longer one, so the alive count oscillates instead of growing):
//   spawn every `spawnInterval` (0.5 s) at one shared x;
//   every clock below runs on the BODY'S OWN LIFE, not on the absolute run time (measured: an
//   absolute cull emptied the field for the last 15 s and gave every late body a zero row);
//   the walk key opens 1.0 s after the body's own spawn — the accepted line's own `-dogWalkTime 1`;
//   the MEASURE window is the body's [1.0 s, `measureSeconds`] (the walk key to the mark, 15.0 s);
//   the body is culled `cullSeconds` (30.0 s) after its own spawn.
//
// THE SPAWN CARRIES TODAY'S LAW (the c50 lesson, learned on the human arena: the spawned bodies ran
// the walk's DEFAULTS while the accepted walk needed its accepted switches, and every body fell).
// The dog's accepted line is the SHIPPED law plus four flags (-dog 1 -dogWalk 1 -dogWalkTime 1
// -dogPawFriction 0.4, row of record dog_c44_w04 bc7a1625757d00ec0d987e052a61f98f), so every body
// here is built the trial's own way in the trial's own order: new GameObject → AddComponent<Dog> →
// startY = StandingRootOffset(-2) → position → BuildDog() → FactionStamp.Wolf → the genome applied
// to dog.stance → pawContactFriction = 0.4. The DEFAULT GENOME IS THE ACCEPTED DOG, and body id 1
// spawns exactly that genome (child_kind 3, the reference), so every run holds the one body whose
// walk is comparable with the trial's own row — its full trace is written beside the run.
//
// THE FITNESS = THE DOG'S OWN BARS (the card, dog-requirements.txt), not distance alone.
// ROUND TWO (cycle 65): the c64 weights crowned a genome the trial line rejected, and the price
// (Trials/Runs/dog-cycle65-arena-fitness.md §0) is that travel carried 91.5 pct of the crown's
// score with corr(fitness, travel) 0.969, while the card's own PRIMARY clauses sat at the floor
// (fourFeet mean 0.0706 against the card's 0.997) and the card's own first clause was an EDGE
// ARTIFACT: the crown's fall onset was 0.14 s before its own mark, its DECLARATION 0.01 s after it.
//
//   travel_m = comX(measure mark) - comX(walk key)             // the card's travel clause
//   fourFeet = fourFeetFrames / windowFrames                   // ALL FOUR paws grounded
//   band     = bandFrames / windowFrames                       // trunkDropNow <= 0.06 m
//   trunkDropNow = max(startChestY - chestY, startPelvisY - pelvisY)   // the recorder's definition
//   up       = upFrames / windowFrames                         // the criterion's own NOT-down frames
//   fellFlag = 1 if the fall ONSET lies inside the window      // the criterion's first sustained frame
//   fitness  = travel_m + 3.0 * fourFeet + 3.0 * band + 3.0 * up - 6.0 * fellFlag
//
// WHY: the card's own order, paid in metres (DogArenaGenome's constant block carries the numbers).
// The fall flag is the ONSET (latch.fireTime, or downSince when the declaration has not completed
// yet), not the DECLARATION (latch.fired, which needs the down-run to hold 0.15 s): the c64 crown's
// onset was 0.14 s before its mark and its declaration 0.01 s after it, so the declaration rule
// handed the crown the full travel of a body already going down. The onset rule can only ADD
// fallers (a declaration implies an earlier onset), so it is monotone and checkable from the
// logged fell_t against spawn_t.
// THE POOL GATE (the card's first clause is discrete): the selection pool is the MEASURED
// NON-FALLERS; only when fewer than MinNonFallerPool of them exist does the whole measured pool
// serve (the search must not stall), and then the -6.0 term orders it. A weight cannot carry a
// bar; the gate does.
//
// THE EVOLUTION (the human side's shape): 25 pct fresh uniform blood; the rest are children of a
// tournament pick (3 draws) inside the best quartile of the MEASURED bodies; the elite (the best
// genome so far) is re-spawned UNCHANGED once 20 bodies are measured and every 8th spawn — the
// unchanged copy is the determinism/collision proof. Every row carries its parent id and mutation
// seed.
//
// CYCLE 69 (the genome expansion): the mutation is ONE FAMILY per child at p = 0.70, the whole
// genome at 0.30 (DogArenaGenome.Families; the members share one signed share of their own ranges);
// the step is 5-10 pct of each gene's own range and shrinks to 3-5 pct ONLY when the best-quartile
// mean has not risen (QuartileCheckpointMeasured / PlateauRiseMeters, the human side's constants);
// per-gene CLAMP PRESSURE is counted over every jittered component and published in the summary;
// the effective population (distinct ancestors among the best quartile) is reported. THE VISUAL
// LAYER (visual runs only): a number over each dog (its body id) and a HUD line (sim time, spawned,
// measured, best travel + its id) — TextMesh objects with no colliders, created only when an arena
// camera exists, never read by physics, so the recorded columns cannot move.
//
// FILES (append-only where the contract says so):
//   Trials/Runs/dog-arena-param-log.csv   fixed-contract rows, appended, NEVER rewritten
//   Trials/Runs/<label>.json              run summary + one JSON row per body + the box audit
//   Trials/Runs/<label>.csv               the same rows as a table
//   Trials/Runs/<label>-rows.csv          the DETERMINISTIC body rows (no label column): two runs
//                                         of one seed must be byte-identical here
//   Trials/Runs/<label>-ref-trace.csv     the reference body's per-step trace, the trial recorder's
//                                         own column names, diffable against the record row's CSV
public class DogArenaRunner : MonoBehaviour
{
    // ─── the human side's own constants (same shape, same numbers) ───
    public const int EliteMinMeasured = 20;
    public const int EliteEverySpawns = 8;
    public const float ChildShare = 0.75f;
    public const float FreshShare = 0.25f;
    public const int TournamentSize = 3;

    // ─── CYCLE 69: the mutation policy (the reviewers' condensed law) ───
    public const float OneFamilyShare = 0.70f;      // p(one family moves) vs the whole genome
    public const float CoarseJitterMin = 0.05f;     // the step, share of each gene's own range
    public const float CoarseJitterMax = 0.10f;
    public const float FineJitterMin = 0.03f;       // after the best-quartile mean stalls
    public const float FineJitterMax = 0.05f;
    public const int QuartileCheckpointMeasured = 25;   // the stall test's cadence
    public const float PlateauRiseMeters = 0.05f;       // 4 checkpoints below this rise -> fine

    // ─── CYCLE 69: the visual layer (visual runs only; no physics) ───
    public const float LabelHeightMeters = 0.35f;   // the digit's own height above the trunk
    public const float LabelDigitMeters = 0.34f;    // drawn glyph height
    public const float LabelZ = -0.6f;              // in FRONT of the art plane (z=0) with room to spare
    public const int LabelSortingOrder = 500;       // above the dog art's orders, below the HUD
    private bool labelProbed;
    public const float HudDigitMeters = 0.34f;
    public const int HudUpdateSteps = 20;           // 0.1 s at the 0.005 s fixed step
    public const float ShotAspect = 16f / 9f;       // the take's shot size (1920x1080)
    public const float HudMarginMeters = 0.5f;      // the HUD's own inset from the frame corner
    // THE POOL GATE (cycle 65): the selection pool is the measured NON-FALLERS; below this count the
    // whole measured pool serves instead, so a field with almost no survivor still searches.
    // 8 = the c64 census's own non-faller count (8 of 60) — the floor that does not starve the pool.
    public const int MinNonFallerPool = 8;
    // The child_kind of the first body of a run: the REFERENCE genome (the shipped initialisers —
    // today's accepted dog), spawned once at id 1 so every run holds the comparable body.
    // 0 fresh random / 1 child of a parent / 2 elite copy / 3 reference / 4 INJECTED (-dogArenaInject).
    public const int ReferenceChildKind = 3;
    public const int InjectedChildKind = 4;

    // ─── the accepted line's own two numbers (the spawn carries today's law) ───
    public const float WalkKeySeconds = 1f;        // -dogWalkTime 1
    public const float PawContactFriction = 0.4f;  // -dogPawFriction 0.4

    // The settled-stand window the trial recorder uses for its own FallCriterion baseline
    // (DogTrialRecorder.DOG_BASELINE_WINDOW_END, mirrored here, not edited there).
    public const float BaselineWindowEnd = 5.0f;

    public const string InfluenceLogName = "dog-arena-param-log.csv";

    // THE FIELD'S OWN ISOLATION (cycle 66): the dedicated layer every body's hierarchy carries, its
    // self-pair OFF in the engine's layer matrix — the human side's own solution, an engine-level
    // exclusion instead of the c64/c65 per-pair registration (1 713 360 pairs in the c65 run) the
    // engine could silently drop.
    public const string ArenaLayerName = "ArenaDog";
    // The crowding measure's window: one body length (the pile's own shadow, see Tick).
    public const float CrowdWindowMeters = 0.8f;

    // ─── configuration: command line in headless, Inspector in the scene ───
    public int seed = 1;
    public string label = "dog_c64arena";
    public string runId = "";
    public string sourceFingerprint = "";
    public string buildGuid = "";
    public string outputFolder = "";
    public int seedUsedByRun;
    public float spawnInterval = 0.5f;
    public float measureSeconds = 15f;
    public float cullSeconds = 30f;
    public int aliveCap = 60;
    public float durationSeconds = 45f;
    public float spawnX = 0f;
    public float groundTopY = -2f;
    public float cameraSize = 5f;
    public float cameraOffsetX = 2.5f;
    // THE CLEAN-FIELD RE-MEASURE (cycle 65): 15 comma-separated values; when set, ONE body (id 1,
    // child_kind 4) carries them and nothing else ever spawns — the rank's candidate measured on its
    // own, before any transplant. Empty = the ordinary evolutionary run.
    public string injectGenome = "";
    // THE FIELD'S OWN ISOLATION (cycle 66): "layer" (default) tags every body with the dedicated
    // ArenaDog layer, whose self-pair is OFF in the engine's layer matrix (forced + read back at
    // Begin); "pairs" keeps the c64/c65 per-pair registration as the CONTROL mechanism, with the new
    // leak-hunt counters. ONE build holds both.
    public string isolationMode = "layer";

    private readonly List<DogArenaBody> bodies = new List<DogArenaBody>();
    private readonly List<DogArenaBody> measured = new List<DogArenaBody>();
    private ArenaRng rng;
    private int spawnIndex;          // 0-based spawn ordinal (id 1 == ordinal 0)
    private int nextBodyId = 1;
    private float nextSpawnAt;
    private float timeStep = 0.005f;
    private int step;
    private int spawnCount;
    private int ignorePairs;
    private int ignoreChecked;
    // the round's own counters (cycle 66): the layer state, its read-backs, the tagging census, the
    // leak hunt (pairs mode) and the crowding measure.
    private int isoLayer = -1;
    private bool isoSelfBaked;
    private bool isoSelfForced;
    private bool isoGroundIgnored;
    private int isoBodiesTagged;
    private int isoCollidersTagged;
    private int isoVerifyChecked;
    private int isoVerifyLost;
    private int isoMarkChecked;
    private int isoMarkLost;
    private string isoFirstLost = "";
    private long crowdPairSteps;
    private int crowdPairsMax;
    private int crowdSteps;
    private bool done;
    private bool ready;
    private bool reported;
    private bool injectOnly;
    private float[] injectParsed;
    private string boxAudit = "";
    private Camera arenaCamera;
    private readonly StringBuilder refTrace = new StringBuilder(1 << 21);
    private int refTraceRows;
    private float bestFitness = float.NegativeInfinity;
    private int bestId = -1;
    // ─── CYCLE 69: the mutation policy's own state ───
    private readonly int[] jitterAttempts = new int[DogArenaGenome.Count];
    private readonly int[] jitterClamped = new int[DogArenaGenome.Count];
    private int oneFamilyChildren;
    private int wholeGenomeChildren;
    private readonly System.Collections.Generic.Dictionary<string, int> moveFamilyCounts =
        new System.Collections.Generic.Dictionary<string, int>();
    private bool fineSchedule;                     // the step schedule's gear
    private readonly List<float> bestQuartileMeanSeries = new List<float>();
    private readonly List<int> bestQuartileMeanAtMeasured = new List<int>();
    private float bestTravel = float.NegativeInfinity;
    private int bestTravelId = -1;
    // ─── CYCLE 69: the visual layer (visual runs only) ───
    private TextMesh hudMesh;
    private MeshRenderer hudRenderer;   // cycle 69: the HUD's own renderer (its drawn bounds place it)
    private int labelCount;

    public bool Done { get { return done; } }
    public bool Ready { get { return ready; } }
    public int MeasuredCount { get { return measured.Count; } }
    public int SpawnCount { get { return spawnCount; } }
    public string BoxAudit { get { return boxAudit; } }
    public int IgnorePairs { get { return ignorePairs; } }
    public int RefTraceRows { get { return refTraceRows; } }

    // ─── one body ────────────────────────────────────────────────────────────
    private class DogArenaBody
    {
        public int id;
        public float[] genome;
        public int parentId = -1;
        public int childKind;
        public int mutationSeed;
        public int spawnIndex;
        public float spawnTime;
        public int spawnStep;
        public GameObject go;
        public Dog dog;
        public DogStanceController stance;
        public Transform chest;
        public Transform pelvis;
        public bool walkKey;
        public bool measured;
        public bool culled;
        public int measureStep = -1;
        public int cullStep = -1;
        public float measureTime = -1f;
        public float culledTime = -1f;
        public int sampleCount;
        public float startChestY, startPelvisY, minChestY, minPelvisY;
        public float comXAtWalkKey;
        public int winSamples, winFourFeet, winBand, winUp;
        public FallCriterion.Latch latch;
        public FallCriterion.Baseline baseline;
        public float fitness;
        public float travel;
        public float fourFeetFraction;
        public float bandFraction;
        public float upFraction;
        public float trunkDrop;
        public bool fellInWindow;
        public float fellTime = -1f;
        public int walkCycles;
        public string mutationFamily = "reference";   // CYCLE 69: which family moved ("" = none)
        public GameObject label;                      // CYCLE 69: the number over the dog (visual only)
    }

    public void Begin(float fixedDelta)
    {
        timeStep = fixedDelta;
        rng = new ArenaRng(seed);
        seedUsedByRun = seed;
        boxAudit = DogArenaGenome.AuditBox();
        if (boxAudit.Length > 0)
            Debug.LogError("DOG_ARENA genome box excludes its own shipped value: " + boxAudit);
        // THE FIELD'S OWN ISOLATION (cycle 66). The layer is resolved by NAME; a missing name is a
        // loud refusal (no measurement on an unisolated field), never a silent default. Two read-backs
        // are published: the BAKED one (before the runtime force - did the build carry the
        // ProjectSettings bake) and the FORCED one (the state this run measured under).
        if (IsolationUsesLayer)
        {
            isoLayer = LayerMask.NameToLayer(ArenaLayerName);
            if (isoLayer < 0)
            {
                Debug.LogError("DOG_ARENA_LAYER missing layer " + ArenaLayerName
                    + " - the field cannot be isolated; refusing to measure");
                done = true;
                ready = true;
                return;
            }
            isoSelfBaked = Physics2D.GetIgnoreLayerCollision(isoLayer, isoLayer);
            Physics2D.IgnoreLayerCollision(isoLayer, isoLayer, true);
            isoSelfForced = Physics2D.GetIgnoreLayerCollision(isoLayer, isoLayer);
            isoGroundIgnored = Physics2D.GetIgnoreLayerCollision(isoLayer, GroundLayers.Index);
            Debug.Log("DOG_ARENA_LAYER " + ArenaLayerName + "=" + isoLayer.ToString(CultureInfo.InvariantCulture)
                + " bakedSelfIgnored=" + (isoSelfBaked ? "1" : "0")
                + " selfIgnored=" + (isoSelfForced ? "1" : "0")
                + " groundIgnored=" + (isoGroundIgnored ? "1" : "0"));
            if (!isoSelfForced || isoGroundIgnored)
                Debug.LogError("DOG_ARENA_LAYER isolation is wrong: self=" + isoSelfForced + " ground=" + isoGroundIgnored);
        }
        bodies.Clear();
        measured.Clear();
        refTrace.Length = 0;
        refTraceRows = 0;
        refTrace.Append("t,chestPitch,pelvisTilt,lumbar,chestY,pelvisY,comX,comY,comOffset,frG,flG,rrG,rlG,state,paws,down\n");
        spawnIndex = 0;
        nextBodyId = 1;
        spawnCount = 0;
        ignorePairs = 0;
        ignoreChecked = 0;
        done = false;
        reported = false;
        bestFitness = float.NegativeInfinity;
        bestId = -1;
        step = 0;
        injectOnly = !string.IsNullOrEmpty(injectGenome);
        injectParsed = injectOnly ? ParseInjectGenome() : null;
        if (injectOnly && injectParsed == null)
            Debug.LogError("DOG_ARENA injected genome unreadable (want 15 comma-separated numbers): " + injectGenome);
        // The reference body exists BEFORE the first physics step, exactly as the trial's own dog
        // does (it is built during the scene build, not after a step) — so its trace aligns with
        // the trial recorder's rows step for step. With -dogArenaInject the one body IS the injected
        // candidate (kind 4) and the clock never spawns.
        SpawnBody(0f, 0);
        nextSpawnAt = spawnInterval;
        ready = true;
        Debug.Log("DOG_ARENA_BEGIN seed=" + seed.ToString(CultureInfo.InvariantCulture)
            + " label=" + label
            + " spawn=" + spawnInterval.ToString("0.###", CultureInfo.InvariantCulture)
            + " measure=" + measureSeconds.ToString("0.###", CultureInfo.InvariantCulture)
            + " cull=" + cullSeconds.ToString("0.###", CultureInfo.InvariantCulture)
            + " seconds=" + durationSeconds.ToString("0.###", CultureInfo.InvariantCulture)
            + " inject=" + (injectOnly ? "1" : "0")
            + " box=" + (boxAudit.Length == 0 ? "OK" : "BAD:" + boxAudit));
    }

    public void Tick(float simulated)
    {
        if (done) return;
        step++;
        // spawns (the clock, not the count); at the alive cap the clock keeps ticking and the spawn
        // is skipped, so a slow body can never wedge the evolution. In inject mode the one injected
        // body is the whole field (the clean-field re-measure).
        if (!injectOnly)
        {
            while (simulated >= nextSpawnAt)
            {
                if (AliveCount() < aliveCap)
                    SpawnBody(nextSpawnAt, step);
                nextSpawnAt += spawnInterval;
            }
        }
        for (int i = bodies.Count - 1; i >= 0; i--)
        {
            DogArenaBody b = bodies[i];
            StepBody(b, simulated);
            // the body's OWN clock: culled after cullSeconds of ITS life, not at an absolute t.
            // (Measured: an absolute cull emptied the field for the last 15 s and gave every late
            // body a zero row - it was killed before its own mark.)
            if (!b.culled && simulated - b.spawnTime >= cullSeconds)
            {
                b.culled = true;
                b.culledTime = simulated;
                b.cullStep = step;
                if (b.go != null) Destroy(b.go);
                bodies.RemoveAt(i);
            }
            // THE VISUAL LAYER (cycle 69): only when an arena camera exists (visual runs). The labels
            // and the HUD are TextMesh objects with no colliders - they are never read by physics and
            // never advance the RNG, so the recorded columns cannot move.
            if (arenaCamera != null)
            {
                EnsureVisualLayer();
                if (step % HudUpdateSteps == 0) UpdateHud(simulated);
            }
        }
        // THE CROWDING MEASURE (cycle 66): the pile's shadow - unordered ALIVE body-pairs within one
        // body length of each other in x - counted per step. The isolation does not move the field
        // (the spawn x is shared by design), so this number pins the GEOMETRY the isolation must
        // neutralize; the coupling numbers live beside it (iso*, and the crowded-vs-clean deltas).
        {
            int n = bodies.Count;
            int pairs = 0;
            for (int i = 0; i < n; i++)
            {
                float xi = bodies[i].stance != null ? bodies[i].stance.comPosition.x : spawnX;
                for (int j = i + 1; j < n; j++)
                {
                    float xj = bodies[j].stance != null ? bodies[j].stance.comPosition.x : spawnX;
                    if (Mathf.Abs(xi - xj) <= CrowdWindowMeters) pairs++;
                }
            }
            crowdPairSteps += pairs;
            if (pairs > crowdPairsMax) crowdPairsMax = pairs;
            crowdSteps++;
        }
        if (simulated >= durationSeconds) done = true;
    }

    private int AliveCount()
    {
        return bodies.Count;
    }

    // ─── the spawn ───────────────────────────────────────────────────────────
    private void SpawnBody(float time, int stepNow)
    {
        DogArenaBody b = new DogArenaBody();
        b.id = nextBodyId++;
        b.spawnIndex = spawnIndex++;
        b.spawnTime = time;
        b.spawnStep = stepNow;

        if (b.id == 1 && injectOnly)
        {
            b.childKind = InjectedChildKind;
            b.genome = injectParsed != null ? DogArenaGenome.Copy(injectParsed) : DogArenaGenome.DefaultsCopy();
        }
        else if (b.id == 1)
        {
            b.childKind = ReferenceChildKind;
            b.genome = DogArenaGenome.DefaultsCopy();
        }
        else if (measured.Count >= EliteMinMeasured && b.spawnIndex % EliteEverySpawns == 0 && bestId >= 0)
        {
            DogArenaBody elite = FindById(bestId);
            b.childKind = 2;
            b.parentId = bestId;
            b.mutationFamily = "elite";
            b.genome = elite != null ? DogArenaGenome.Copy(elite.genome) : DogArenaGenome.DefaultsCopy();
        }
        else if (measured.Count == 0 || rng.Next01() < FreshShare)
        {
            b.childKind = 0;
            b.mutationFamily = "fresh";
            b.genome = DogArenaGenome.Random(ref rng);
        }
        else
        {
            DogArenaBody parent = TournamentPick();
            b.childKind = 1;
            b.parentId = parent != null ? parent.id : -1;
            // The jitter is reproducible from the seed ALONE: its own generator, its own stream.
            // A local rng (by ref) keeps the field's own stream untouched by the child's mutation.
            if (parent != null)
            {
                b.genome = DogArenaGenome.Copy(parent.genome);
                b.mutationSeed = rng.NextSeed();
                ArenaRng jr = new ArenaRng(b.mutationSeed);
                // CYCLE 69: ONE family per child at p = 0.70, the whole genome at 0.30. The family
                // index is drawn from the FIELD's stream (after the seed), the move itself from the
                // child's own stream, so the jitter stays reproducible from the seed alone.
                int family = -1;
                float jMin = fineSchedule ? FineJitterMin : CoarseJitterMin;
                float jMax = fineSchedule ? FineJitterMax : CoarseJitterMax;
                if (rng.Next01() < OneFamilyShare)
                {
                    family = rng.NextInt(DogArenaGenome.FamilyCount);
                    DogArenaGenome.JitterFamily(b.genome, ref jr, family, jMin, jMax, jitterAttempts, jitterClamped);
                    oneFamilyChildren++;
                }
                else
                {
                    DogArenaGenome.Jitter(b.genome, ref jr, jMin, jMax, jitterAttempts, jitterClamped);
                    wholeGenomeChildren++;
                }
                b.mutationFamily = DogArenaGenome.FamilyName(family);
                int seen;
                moveFamilyCounts.TryGetValue(b.mutationFamily, out seen);
                moveFamilyCounts[b.mutationFamily] = seen + 1;
            }
            else
            {
                b.mutationFamily = "fresh";
                b.genome = DogArenaGenome.Random(ref rng);
            }
        }

        GameObject go = new GameObject("DogArenaBody_" + b.id.ToString(CultureInfo.InvariantCulture));
        b.go = go;
        b.dog = go.AddComponent<Dog>();
        // the accepted line's own -dogPawFriction 0.4, set BEFORE BuildDog() exactly as the trial's
        // ApplyDogBuildOverrides does: the pad material is created lazily on first use, so a value
        // written after the build would never reach the pads.
        b.dog.pawContactFriction = PawContactFriction;
        float startY = b.dog.StandingRootOffset(groundTopY);
        go.transform.position = new Vector2(spawnX, startY);
        b.dog.BuildDog();
        FactionStamp.Wolf(go);
        b.stance = b.dog.stance;
        DogArenaGenome.Apply(b.stance, b.genome);
        b.chest = go.transform.Find("Chest");
        b.pelvis = go.transform.Find("Pelvis");

        // THE FIELD'S OWN ISOLATION (cycle 66). Bodies share ONE spawn x, so the field is a pile: they
        // must never collide with EACH OTHER, or every measurement is a collision's, not a gait's (the
        // c64 crown's crowded row was the crowd's - 5.643835 m against its clean 0.101285 m).
        // "layer" (default): every body's hierarchy carries ArenaDog, whose self-pair is OFF in the
        // engine's own layer matrix — the engine never generates a dog-dog pair, so there is nothing to
        // drop. "pairs": the c64/c65 registration path, kept as the CONTROL, now with the leak hunt —
        // the engine's own read-back of the just-registered pairs.
        if (IsolationUsesLayer)
        {
            if (isoLayer >= 0)
            {
                SetLayerRecursively(go, isoLayer);
                isoBodiesTagged++;
                isoCollidersTagged += go.GetComponentsInChildren<Collider2D>(true).Length;
            }
        }
        else
        {
            Collider2D[] mine = go.GetComponentsInChildren<Collider2D>(true);
            for (int i = 0; i < bodies.Count; i++)
            {
                DogArenaBody other = bodies[i];
                if (other.go == null) continue;
                Collider2D[] theirs = other.go.GetComponentsInChildren<Collider2D>(true);
                for (int a = 0; a < mine.Length; a++)
                {
                    if (mine[a] == null) continue;
                    for (int c = 0; c < theirs.Length; c++)
                    {
                        if (theirs[c] == null) continue;
                        Physics2D.IgnoreCollision(mine[a], theirs[c], true);
                        ignorePairs++;
                    }
                }
                if (ignoreChecked == 0 && mine.Length > 0 && theirs.Length > 0)
                {
                    if (Physics2D.GetIgnoreCollision(mine[0], theirs[0])) ignoreChecked = 1;
                }
                // THE LEAK HUNT: read a sample of the just-registered pairs BACK from the engine. A
                // pair the engine no longer reports as ignored is a pair it dropped.
                if (mine.Length > 0 && theirs.Length > 0)
                {
                    VerifyPair(b, mine[0], other, theirs[0]);
                    VerifyPair(b, mine[mine.Length - 1], other, theirs[theirs.Length - 1]);
                }
            }
        }
        bodies.Add(b);
        spawnCount++;
    }

    private DogArenaBody FindById(int id)
    {
        for (int i = 0; i < bodies.Count; i++)
            if (bodies[i].id == id) return bodies[i];
        for (int i = 0; i < measured.Count; i++)
            if (measured[i].id == id) return measured[i];
        return null;
    }

    private bool IsolationUsesLayer { get { return isolationMode != "pairs"; } }

    // The layer tag goes down the WHOLE hierarchy (the human arena's own pattern): a body whose parts
    // span two layers would still collide with a sibling.
    private static void SetLayerRecursively(GameObject go, int layer)
    {
        if (go == null) return;
        go.layer = layer;
        Transform t = go.transform;
        for (int i = 0; i < t.childCount; i++)
            SetLayerRecursively(t.GetChild(i).gameObject, layer);
    }

    // THE LEAK HUNT (cycle 66, pairs mode): one engine read-back of one registered pair, counted.
    private void VerifyPair(DogArenaBody a, Collider2D ca, DogArenaBody b, Collider2D cb)
    {
        if (ca == null || cb == null) return;
        isoVerifyChecked++;
        if (!Physics2D.GetIgnoreCollision(ca, cb))
        {
            isoVerifyLost++;
            if (isoFirstLost.Length == 0)
                isoFirstLost = "spawn b" + a.id.ToString(CultureInfo.InvariantCulture)
                    + " x b" + b.id.ToString(CultureInfo.InvariantCulture);
        }
    }

    private void MarkVerify(DogArenaBody a, Collider2D ca, DogArenaBody b, Collider2D cb)
    {
        if (ca == null || cb == null) return;
        isoMarkChecked++;
        if (!Physics2D.GetIgnoreCollision(ca, cb))
        {
            isoMarkLost++;
            if (isoFirstLost.Length == 0)
                isoFirstLost = "mark b" + a.id.ToString(CultureInfo.InvariantCulture)
                    + " x b" + b.id.ToString(CultureInfo.InvariantCulture);
        }
    }

    // How many of the measured bodies are non-fallers under the ONSET rule (the selection pool's
    // own size, published in the summary).
    private int SelectionNonFallerCount()
    {
        int n = 0;
        for (int i = 0; i < measured.Count; i++)
            if (!measured[i].fellInWindow) n++;
        return n;
    }

    // -dogArenaInject: the clean-field re-measure's genome — 15 comma-separated numbers in the box
    // (DogArenaGenome's own order). Unreadable input is refused loudly, never silently defaulted.
    private float[] ParseInjectGenome()
    {
        if (string.IsNullOrEmpty(injectGenome)) return null;
        string[] parts = injectGenome.Split(',');
        if (parts.Length != DogArenaGenome.Count) return null;
        float[] g = new float[DogArenaGenome.Count];
        for (int i = 0; i < parts.Length; i++)
        {
            float v;
            if (!float.TryParse(parts[i].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out v))
                return null;
            g[i] = v;
        }
        DogArenaGenome.Clamp(g);
        return g;
    }

    // Tournament: draw TournamentSize candidates from the best quartile of the MEASURED bodies,
    // keep the fittest of the draws.
    private DogArenaBody TournamentPick()
    {
        List<DogArenaBody> quartile = BestQuartile();
        if (quartile.Count == 0) return null;
        DogArenaBody best = null;
        for (int i = 0; i < TournamentSize; i++)
        {
            DogArenaBody cand = quartile[(int)(rng.NextUInt() % (uint)quartile.Count)];
            if (best == null || cand.fitness > best.fitness) best = cand;
        }
        return best;
    }

    // THE POOL GATE (cycle 65): the selection pool is the MEASURED NON-FALLERS — the card's first
    // clause (fell = false) is a bar, and a bar cannot be priced. Below MinNonFallerPool survivors
    // the whole measured pool serves instead (the search must not stall on a field with no walkers);
    // the -6.0 fall term then orders that fallback pool.
    private List<DogArenaBody> SelectionPool()
    {
        List<DogArenaBody> pool = new List<DogArenaBody>();
        for (int i = 0; i < measured.Count; i++)
            if (!measured[i].fellInWindow) pool.Add(measured[i]);
        if (pool.Count < MinNonFallerPool) return new List<DogArenaBody>(measured);
        return pool;
    }

    private List<DogArenaBody> BestQuartile()
    {
        List<DogArenaBody> sorted = SelectionPool();
        sorted.Sort((x, y) => y.fitness.CompareTo(x.fitness));
        int n = Mathf.Max(1, sorted.Count / 4);
        if (sorted.Count == 0) return sorted;
        return sorted.GetRange(0, Mathf.Min(n, sorted.Count));
    }

    // ─── one step of one body ────────────────────────────────────────────────
    private void StepBody(DogArenaBody b, float simulated)
    {
        if (b.stance == null || b.chest == null) return;
        float chestY = b.chest.position.y;
        float pelvisY = b.pelvis != null ? b.pelvis.position.y : chestY;
        if (b.sampleCount == 0)
        {
            b.startChestY = chestY;
            b.startPelvisY = pelvisY;
            b.minChestY = chestY;
            b.minPelvisY = pelvisY;
        }
        if (chestY < b.minChestY) b.minChestY = chestY;
        if (pelvisY < b.minPelvisY) b.minPelvisY = pelvisY;

        int paws = (b.stance.frontRightGrounded ? 1 : 0) + (b.stance.frontLeftGrounded ? 1 : 0)
            + (b.stance.rearRightGrounded ? 1 : 0) + (b.stance.rearLeftGrounded ? 1 : 0);
        float tilt = Mathf.Max(Mathf.Abs(b.stance.chestPitch), Mathf.Abs(b.stance.pelvisTilt));

        // the body's own clock: the walk key, the baseline window, the measure window and the mark
        // all run on the body's OWN life, so a dog spawned at t=20 s is measured exactly as one
        // spawned at t=0 (the human arena's own rule; the spawn carries the accepted configuration).
        float local = simulated - b.spawnTime;

        // the walk key: the accepted line's own -dogWalkTime 1
        if (!b.walkKey && local >= WalkKeySeconds)
        {
            b.stance.Walk();
            b.walkKey = true;
            b.comXAtWalkKey = b.stance.comPosition.x;
        }

        // the settled stand height: the trial recorder's own window (first 5 s, 3+ paws planted,
        // tilt under 45 deg), then frozen — the baseline is an INPUT to FallCriterion, never the
        // spawn pose and never a running maximum.
        if (local < BaselineWindowEnd && paws >= 3) b.baseline.Feed(chestY, tilt);
        else if (local >= BaselineWindowEnd) b.baseline.Freeze();
        bool down = FallCriterion.Step(ref b.latch, b.baseline.Value(b.startChestY), chestY, tilt,
            timeStep, simulated);
        if (b.latch.fired && b.fellTime < 0f) b.fellTime = b.latch.fireTime;

        // the measure window: the walk key to the mark, both on the body's own clock
        if (b.walkKey && local <= measureSeconds)
        {
            b.winSamples++;
            if (paws == 4) b.winFourFeet++;
            if (!down) b.winUp++;
            float trunkNow = Mathf.Max(b.startChestY - chestY, b.startPelvisY - pelvisY);
            if (trunkNow <= DogArenaGenome.TrunkBandMeters) b.winBand++;
        }

        // the reference body's own trace (the recorder's own column names)
        if (b.id == 1)
        {
            refTrace.Append(G(b, simulated)).Append(',').Append(F(b.stance.chestPitch))
                .Append(',').Append(F(b.stance.pelvisTilt)).Append(',').Append(F(b.stance.lumbarAngle))
                .Append(',').Append(F(chestY)).Append(',').Append(F(pelvisY))
                .Append(',').Append(F(b.stance.comPosition.x)).Append(',').Append(F(b.stance.comPosition.y))
                .Append(',').Append(F(b.stance.comOffsetX))
                .Append(',').Append(b.stance.frontRightGrounded ? '1' : '0')
                .Append(',').Append(b.stance.frontLeftGrounded ? '1' : '0')
                .Append(',').Append(b.stance.rearRightGrounded ? '1' : '0')
                .Append(',').Append(b.stance.rearLeftGrounded ? '1' : '0')
                .Append(',').Append(((int)b.stance.currentState).ToString(CultureInfo.InvariantCulture))
                .Append(',').Append(paws.ToString(CultureInfo.InvariantCulture))
                .Append(',').Append(down ? '1' : '0')
                .Append('\n');
            refTraceRows++;
        }

        b.sampleCount++;

        // the mark
        if (!b.measured && local >= measureSeconds)
        {
            b.measured = true;
            b.measureTime = simulated;
            b.measureStep = step;
            b.travel = b.stance.comPosition.x - b.comXAtWalkKey;
            b.fourFeetFraction = b.winSamples > 0 ? b.winFourFeet / (float)b.winSamples : 0f;
            b.bandFraction = b.winSamples > 0 ? b.winBand / (float)b.winSamples : 0f;
            b.trunkDrop = Mathf.Max(b.startChestY - b.minChestY, b.startPelvisY - b.minPelvisY);
            b.upFraction = b.winSamples > 0 ? b.winUp / (float)b.winSamples : 0f;
            // THE ONSET RULE (cycle 65): the criterion's FIRST sustained frame, whether the 0.15 s
            // declaration completed or not — the c64 crown's onset sat 0.14 s before its own mark
            // while its declaration landed 0.01 s after it, and the old declaration rule crowned it.
            float onset = b.latch.fired ? b.latch.fireTime
                : (b.latch.downAge > 0f ? b.latch.downSince : -1f);
            b.fellInWindow = onset >= 0f && onset - b.spawnTime <= measureSeconds;
            if (onset >= 0f && b.fellTime < 0f) b.fellTime = onset;
            b.walkCycles = b.stance.walkCycleCount;
            b.fitness = b.travel
                + DogArenaGenome.StandBonusMeters * b.fourFeetFraction
                + DogArenaGenome.BandBonusMeters * b.bandFraction
                + DogArenaGenome.UpBonusMeters * b.upFraction
                - (b.fellInWindow ? DogArenaGenome.FallPenaltyMeters : 0f);
            measured.Add(b);
            // CYCLE 69: the HUD's own number (the best measured TRAVEL, not the fitness) and the
            // step schedule's checkpoint (the best-quartile mean, never the population mean).
            if (b.travel > bestTravel)
            {
                bestTravel = b.travel;
                bestTravelId = b.id;
            }
            if (measured.Count % QuartileCheckpointMeasured == 0) SampleBestQuartileMean();
            // THE LEAK HUNT AT THE MARK (cycle 66, pairs mode): the pair set that matters is the one
            // alive at the body's own mark, not only the one seen at its spawn.
            if (!IsolationUsesLayer && b.go != null)
            {
                Collider2D[] mine = b.go.GetComponentsInChildren<Collider2D>(true);
                for (int i = 0; i < bodies.Count; i++)
                {
                    DogArenaBody other = bodies[i];
                    if (other == b || other.go == null) continue;
                    Collider2D[] theirs = other.go.GetComponentsInChildren<Collider2D>(true);
                    if (mine.Length > 0 && theirs.Length > 0)
                    {
                        MarkVerify(b, mine[0], other, theirs[0]);
                        MarkVerify(b, mine[mine.Length - 1], other, theirs[theirs.Length - 1]);
                    }
                }
            }
            if (b.fitness > bestFitness)
            {
                bestFitness = b.fitness;
                bestId = b.id;
            }
        }
    }

    // ─── camera ──────────────────────────────────────────────────────────────
    public Camera EnsureArenaCamera(float size, float offsetX)
    {
        if (arenaCamera == null)
        {
            arenaCamera = Camera.main;
            if (arenaCamera == null) arenaCamera = FindFirstObjectByType<Camera>();
        }
        if (arenaCamera == null)
        {
            GameObject go = new GameObject("DogArenaCamera");
            arenaCamera = go.AddComponent<Camera>();
            go.tag = "MainCamera";
            arenaCamera.clearFlags = CameraClearFlags.SolidColor;
            arenaCamera.backgroundColor = SceneLighting.NightSky;
        }
        arenaCamera.orthographic = true;
        // The take's shots are 1920x1080 and the batchmode screen's aspect is NOT the frame's
        // (measured on the first takes: the HUD's leading "t" came out clipped). Pin the projection
        // to the shot's aspect in TAKE runs only - Yury's live window keeps its own window's.
        if (Application.isBatchMode) arenaCamera.aspect = ShotAspect;
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

    // ─── CYCLE 69: THE VISUAL LAYER (visual runs only; no physics) ───────────
    // A number over each dog (its body id - the human arena's numbered-label pattern) and one HUD
    // line. Both are TextMesh objects: NO collider, NO rigidbody, never read by the gait, so the
    // recorded columns cannot move; the objects are created only after an arena camera exists, so a
    // -nographics run never builds them (and never pays for them).
    private void EnsureVisualLayer()
    {
        if (arenaCamera == null) return;
        if (hudMesh == null) AttachHud();
        for (int i = 0; i < bodies.Count; i++)
        {
            DogArenaBody b = bodies[i];
            if (b.label == null)
            {
                if (b.go != null) AttachLabel(b);
                continue;
            }
            // THE LABEL FOLLOWS THE BODY: the arena's root object stays at the spawn point and the
            // rig moves in the root's LOCAL frame, so a label left at its attach position would
            // pile up in the spawn column (measured: every label of the first take sat at x ~ 0).
            // Re-anchored from the chest's own local position each tick: the digit rides above the
            // trunk and stays upright (the root carries no rotation).
            if (b.chest != null && b.go != null)
            {
                Vector3 chestLocal = b.go.transform.InverseTransformPoint(b.chest.position);
                b.label.transform.localPosition = new Vector3(chestLocal.x, chestLocal.y + LabelHeightMeters, LabelZ);
            }
        }
    }

    private void AttachLabel(DogArenaBody b)
    {
        if (b.go == null || b.label != null) return;
        GameObject label = new GameObject("DogArenaLabel");
        label.layer = b.go.layer;
        label.transform.SetParent(b.go.transform, false);
        float trunkY = 0.30f;
        if (b.chest != null)
            trunkY = b.go.transform.InverseTransformPoint(b.chest.position).y;
        label.transform.localPosition = new Vector3(0f, trunkY + LabelHeightMeters, LabelZ);
        label.transform.localRotation = Quaternion.identity;

        TextMesh textMesh = label.AddComponent<TextMesh>();
        Font font = ArenaHumanAgent.LabelFont();
        if (font != null) textMesh.font = font;
        textMesh.text = b.id.ToString(CultureInfo.InvariantCulture);
        textMesh.fontSize = 72;
        // world height = characterSize * fontSize / 10 (the human arena's own identity)
        textMesh.characterSize = Mathf.Max(0.005f, LabelDigitMeters * 10f / textMesh.fontSize);
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.alignment = TextAlignment.Center;
        textMesh.color = new Color(1f, 0.85f, 0.35f, 1f);

        MeshRenderer renderer = label.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            renderer.sortingOrder = LabelSortingOrder;
            if (font != null && font.material != null) renderer.sharedMaterial = font.material;
        }
        b.label = label;
        labelCount++;
    }

    private void AttachHud()
    {
        if (arenaCamera == null) return;
        GameObject hud = new GameObject("DogArenaHud");
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
            if (font != null && font.material != null) renderer.sharedMaterial = font.material;
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
            + "   best " + (measured.Count > 0 ? bestTravel.ToString("0.00", CultureInfo.InvariantCulture) : "-")
            + "m id " + (bestTravelId >= 0 ? bestTravelId.ToString(CultureInfo.InvariantCulture) : "-");
        // Placed from the DRAWN bounds, not from the transform: the TextMesh anchor is not the
        // mesh's corner (measured on the second take: the text started ~0.55 m left of the
        // transform even with UpperLeft). One correction per update keeps the left edge pinned
        // while the clock's digits grow.
        if (hudRenderer == null) hudRenderer = hudMesh.GetComponent<MeshRenderer>();
        if (hudRenderer != null)
        {
            Bounds b = hudRenderer.bounds;
            if (b.size.x > 0.001f)
            {
                // The view's own top-left corner in WORLD space: the camera is not at x=0 (the lens
                // centres on spawnX + offsetX), and the batchmode screen's aspect is not the shot's.
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
        // ─── TEMP PROBE (cycle 69, the label-visibility question): one shot, first label only ───
        if (!labelProbed && labelCount > 0)
        {
            for (int i = 0; i < bodies.Count; i++)
            {
                DogArenaBody pb = bodies[i];
                if (pb.label == null || pb.go == null) continue;
                MeshRenderer lr = pb.label.GetComponent<MeshRenderer>();
                TextMesh lt = pb.label.GetComponent<TextMesh>();
                SpriteRenderer art = pb.chest != null ? pb.chest.GetComponentInChildren<SpriteRenderer>() : null;
                Debug.Log("DOG_ARENA_LABEL_PROBE id=" + pb.id
                    + " labelWorldZ=" + pb.label.transform.position.z.ToString("0.####")
                    + " lossyScale=" + pb.label.transform.lossyScale.x.ToString("0.####")
                    + " charSize=" + lt.characterSize.ToString("0.#####")
                    + " font=" + (lt.font != null ? lt.font.name : "NULL")
                    + " lyr=" + lr.sortingLayerName + " order=" + lr.sortingOrder
                    + " visible=" + lr.isVisible
                    + " boundsC=" + lr.bounds.center.ToString("0.##") + " boundsS=" + lr.bounds.size.ToString("0.####")
                    + " artOrder=" + (art != null ? art.sortingOrder.ToString() : "-")
                    + " artLyr=" + (art != null ? art.sortingLayerName : "-")
                    + " artZ=" + (art != null ? art.transform.position.z.ToString("0.####") : "-")
                    + " camMask=" + arenaCamera.cullingMask + " camLayer=" + arenaCamera.gameObject.layer
                    + " camZ=" + arenaCamera.transform.position.z.ToString("0.#")
                    + " camOrtho=" + arenaCamera.orthographic + " size=" + arenaCamera.orthographicSize.ToString("0.##"));
                labelProbed = true;
                break;
            }
        }
    }

    // ─── CYCLE 69: the step schedule and the effective population ───────────
    // The stall test samples the BEST-QUARTILE mean (never the population mean: half the spawns are
    // fresh blood and elite twins, so the total mean cannot rise while the search works). Four
    // checkpoints rising by less than PlateauRiseMeters put the step to the fine gear.
    private void SampleBestQuartileMean()
    {
        List<DogArenaBody> quartile = BestQuartile();
        if (quartile.Count == 0) return;
        float sum = 0f;
        for (int i = 0; i < quartile.Count; i++) sum += quartile[i].fitness;
        bestQuartileMeanSeries.Add(sum / quartile.Count);
        bestQuartileMeanAtMeasured.Add(measured.Count);
        int n = bestQuartileMeanSeries.Count;
        if (!fineSchedule && n >= 4 && bestQuartileMeanSeries[n - 1] - bestQuartileMeanSeries[n - 4] < PlateauRiseMeters)
            fineSchedule = true;
    }

    // Distinct ancestors among the best quartile: the parents of its bodies and the roots their
    // chains end at. <5 means the lineage has collapsed (add blood), >30 means the pool is a
    // lottery (selection is not choosing).
    private void EffectivePopulation(out int distinctParents, out int distinctRoots, out int poolBodies)
    {
        List<DogArenaBody> quartile = BestQuartile();
        System.Collections.Generic.HashSet<int> parents = new System.Collections.Generic.HashSet<int>();
        System.Collections.Generic.HashSet<int> roots = new System.Collections.Generic.HashSet<int>();
        for (int i = 0; i < quartile.Count; i++)
        {
            DogArenaBody b = quartile[i];
            if (b.parentId >= 0) parents.Add(b.parentId);
            int cursor = b.parentId;
            int depth = 0;
            while (cursor >= 0 && depth < 64)
            {
                DogArenaBody p = FindById(cursor);
                if (p == null) { roots.Add(cursor); break; }
                if (p.parentId < 0) { roots.Add(p.id); break; }
                cursor = p.parentId;
                depth++;
            }
        }
        distinctParents = parents.Count;
        distinctRoots = roots.Count;
        poolBodies = quartile.Count;
    }

    private string MoveFamilyCensusJson()
    {
        StringBuilder sb = new StringBuilder(256);
        sb.Append('{');
        for (int i = 0; i < DogArenaGenome.FamilyCount; i++)
        {
            string name = DogArenaGenome.FamilyNames[i];
            int seen;
            moveFamilyCounts.TryGetValue(name, out seen);
            if (i > 0) sb.Append(',');
            sb.Append('"').Append(name).Append("\":").Append(seen.ToString(CultureInfo.InvariantCulture));
        }
        int whole;
        moveFamilyCounts.TryGetValue(DogArenaGenome.WholeGenomeFamily, out whole);
        sb.Append(",\"").Append(DogArenaGenome.WholeGenomeFamily).Append("\":").Append(whole.ToString(CultureInfo.InvariantCulture));
        sb.Append('}');
        return sb.ToString();
    }

    private string ClampPressureJson()
    {
        StringBuilder sb = new StringBuilder(1024);
        sb.Append("{\"note\":\"share of a gene's jittered values that hit a box bound\",\"perGene\":{");
        for (int i = 0; i < DogArenaGenome.Count; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append('"').Append(DogArenaGenome.Names[i]).Append("\":{\"attempts\":")
                .Append(jitterAttempts[i].ToString(CultureInfo.InvariantCulture))
                .Append(",\"clamped\":").Append(jitterClamped[i].ToString(CultureInfo.InvariantCulture))
                .Append(",\"share\":").Append(jitterAttempts[i] > 0
                    ? (jitterClamped[i] / (float)jitterAttempts[i]).ToString("0.######", CultureInfo.InvariantCulture) : "0")
                .Append('}');
        }
        sb.Append("}}");
        return sb.ToString();
    }

    // ─── files ───────────────────────────────────────────────────────────────
    private string SummaryPath { get { return Path.Combine(outputFolder, label + ".json"); } }
    private string CsvPath { get { return Path.Combine(outputFolder, label + ".csv"); } }
    private string RowsPath { get { return Path.Combine(outputFolder, label + "-rows.csv"); } }
    private string RefTracePath { get { return Path.Combine(outputFolder, label + "-ref-trace.csv"); } }
    private string InfluenceLogPath { get { return Path.Combine(outputFolder, InfluenceLogName); } }

    private static string F(float v)
    {
        return v.ToString("G9", CultureInfo.InvariantCulture);
    }

    private static string G(DogArenaBody b, float simulated)
    {
        return simulated.ToString("G9", CultureInfo.InvariantCulture);
    }

    private static string RowHeader()
    {
        return "body_id,spawn_index,spawn_step,parent_id,child_kind,mutation_seed,measured,measure_step,"
            + "cull_step,fitness,travel_m,four_feet,band,trunk_drop,fell,fell_t,walk_cycles,"
            + DogArenaGenome.CsvHeader();
    }

    // The DETERMINISTIC row (no label, no wall clock): two runs of one seed must produce identical
    // bytes here, which is the determinism proof the round quotes.
    private static string RowBody(DogArenaBody b)
    {
        StringBuilder sb = new StringBuilder(512);
        sb.Append(b.id.ToString(CultureInfo.InvariantCulture)).Append(',')
          .Append(b.spawnIndex.ToString(CultureInfo.InvariantCulture)).Append(',')
          .Append(b.spawnStep.ToString(CultureInfo.InvariantCulture)).Append(',')
          .Append(b.parentId.ToString(CultureInfo.InvariantCulture)).Append(',')
          .Append(b.childKind.ToString(CultureInfo.InvariantCulture)).Append(',')
          .Append(b.mutationSeed.ToString(CultureInfo.InvariantCulture)).Append(',')
          .Append(b.measured ? '1' : '0').Append(',')
          .Append(b.measureStep.ToString(CultureInfo.InvariantCulture)).Append(',')
          .Append(b.cullStep.ToString(CultureInfo.InvariantCulture)).Append(',')
          .Append(b.measured ? b.fitness.ToString("0.######", CultureInfo.InvariantCulture) : "").Append(',')
          .Append(b.measured ? b.travel.ToString("0.######", CultureInfo.InvariantCulture) : "").Append(',')
          .Append(b.measured ? b.fourFeetFraction.ToString("0.######", CultureInfo.InvariantCulture) : "").Append(',')
          .Append(b.measured ? b.bandFraction.ToString("0.######", CultureInfo.InvariantCulture) : "").Append(',')
          .Append(b.measured ? b.trunkDrop.ToString("0.######", CultureInfo.InvariantCulture) : "").Append(',')
          .Append(b.fellInWindow ? '1' : '0').Append(',')
          .Append(b.fellTime >= 0f ? b.fellTime.ToString("0.######", CultureInfo.InvariantCulture) : "").Append(',')
          .Append(b.measured ? b.walkCycles.ToString(CultureInfo.InvariantCulture) : "").Append(',')
          .Append(DogArenaGenome.CsvValues(b.genome));
        return sb.ToString();
    }

    private static string InfluenceHeader()
    {
        return "run_id,seed,body_id,spawn_index,parent_id,child_kind,spawn_t,measured,culled_t,fitness,"
            + "travel_m,four_feet,band,trunk_drop,fell,fell_t,walk_cycles," + DogArenaGenome.CsvHeader();
    }

    private string InfluenceRow(DogArenaBody b)
    {
        StringBuilder sb = new StringBuilder(512);
        sb.Append(runId).Append(',')
          .Append(seedUsedByRun.ToString(CultureInfo.InvariantCulture)).Append(',')
          .Append(b.id.ToString(CultureInfo.InvariantCulture)).Append(',')
          .Append(b.spawnIndex.ToString(CultureInfo.InvariantCulture)).Append(',')
          .Append(b.parentId.ToString(CultureInfo.InvariantCulture)).Append(',')
          .Append(b.childKind.ToString(CultureInfo.InvariantCulture)).Append(',')
          .Append(b.spawnTime.ToString("0.######", CultureInfo.InvariantCulture)).Append(',')
          .Append(b.measured ? '1' : '0').Append(',')
          .Append(b.culledTime >= 0f ? b.culledTime.ToString("0.######", CultureInfo.InvariantCulture) : "").Append(',')
          .Append(b.measured ? b.fitness.ToString("0.######", CultureInfo.InvariantCulture) : "").Append(',')
          .Append(b.measured ? b.travel.ToString("0.######", CultureInfo.InvariantCulture) : "").Append(',')
          .Append(b.measured ? b.fourFeetFraction.ToString("0.######", CultureInfo.InvariantCulture) : "").Append(',')
          .Append(b.measured ? b.bandFraction.ToString("0.######", CultureInfo.InvariantCulture) : "").Append(',')
          .Append(b.measured ? b.trunkDrop.ToString("0.######", CultureInfo.InvariantCulture) : "").Append(',')
          .Append(b.fellInWindow ? '1' : '0').Append(',')
          .Append(b.fellTime >= 0f ? b.fellTime.ToString("0.######", CultureInfo.InvariantCulture) : "").Append(',')
          .Append(b.measured ? b.walkCycles.ToString(CultureInfo.InvariantCulture) : "").Append(',')
          .Append(DogArenaGenome.CsvValues(b.genome));
        return sb.ToString();
    }

    // The parameter log is APPEND-ONLY: the header exists only when the file does not, and every
    // run appends. The round proves it by prefix, not by promise.
    private void AppendInfluenceLog(List<DogArenaBody> ordered)
    {
        bool exists = File.Exists(InfluenceLogPath);
        StringBuilder sb = new StringBuilder(1 << 16);
        if (!exists) sb.Append(InfluenceHeader()).Append('\n');
        for (int i = 0; i < ordered.Count; i++)
            sb.Append(InfluenceRow(ordered[i])).Append('\n');
        File.AppendAllText(InfluenceLogPath, sb.ToString());
    }

    // ─── the close: every file, once ─────────────────────────────────────────
    // The label.csv's own table (the run's identity columns plus the deterministic rows), so the
    // file lands where every other trial's table lands; HeadlessTrial.Finish writes it.
    public string BuildCsv()
    {
        List<DogArenaBody> all = Ordered();
        StringBuilder csv = new StringBuilder(1 << 16);
        csv.Append("run_id,seed,").Append(RowHeader()).Append('\n');
        for (int i = 0; i < all.Count; i++)
            csv.Append(runId).Append(',').Append(seedUsedByRun.ToString(CultureInfo.InvariantCulture))
               .Append(',').Append(RowBody(all[i])).Append('\n');
        return csv.ToString();
    }

    private List<DogArenaBody> Ordered()
    {
        List<DogArenaBody> all = new List<DogArenaBody>(bodies.Count + measured.Count);
        all.AddRange(measured);
        for (int i = 0; i < bodies.Count; i++)
            if (!bodies[i].measured) all.Add(bodies[i]);
        all.Sort((x, y) => x.id.CompareTo(y.id));
        return all;
    }

    public string FinishAndSummary(float simulatedSeconds, float wallSeconds)
    {
        if (reported) return "";
        reported = true;
        if (!Directory.Exists(outputFolder)) Directory.CreateDirectory(outputFolder);

        List<DogArenaBody> all = Ordered();

        // the deterministic rows (id order, no label, no wall clock)
        StringBuilder rows = new StringBuilder(1 << 16);
        rows.Append(RowHeader()).Append('\n');
        for (int i = 0; i < all.Count; i++) rows.Append(RowBody(all[i])).Append('\n');
        File.WriteAllText(RowsPath, rows.ToString());

        File.WriteAllText(RefTracePath, refTrace.ToString());

        AppendInfluenceLog(all);

        // the summary
        DogArenaBody best = FindById(bestId);
        StringBuilder json = new StringBuilder(1 << 16);
        json.Append('{');
        JStr(json, "subject", "dog-arena", false);
        JStr(json, "label", label, true);
        JStr(json, "runId", runId, true);
        JNum(json, "seed", seedUsedByRun);
        JNum(json, "spawnIntervalSeconds", spawnInterval);
        JNum(json, "measureSeconds", measureSeconds);
        JNum(json, "cullSeconds", cullSeconds);
        JNum(json, "durationSeconds", durationSeconds);
        JNum(json, "simulatedSeconds", simulatedSeconds);
        JNum(json, "wallSeconds", wallSeconds);
        JNum(json, "fixedDeltaTime", timeStep);
        JNum(json, "spawnX", spawnX);
        JNum(json, "aliveCap", aliveCap);
        JNum(json, "spawnCount", spawnCount);
        JNum(json, "measuredCount", measured.Count);
        JNum(json, "aliveAtEnd", bodies.Count);
        JNum(json, "ignorePairs", ignorePairs);
        JNum(json, "refTraceRows", refTraceRows);
        json.Append(",\"ignoreReadBack\":").Append(ignoreChecked == 1 ? "true" : "false");
        // THE FIELD'S OWN ISOLATION (cycle 66): the mechanism, its read-backs and the round's counters.
        JStr(json, "isolation", isolationMode, true);
        JNum(json, "isoLayer", isoLayer);
        JStr(json, "isoLayerName", ArenaLayerName, true);
        json.Append(",\"isoSelfIgnoredBaked\":").Append(isoSelfBaked ? "true" : "false");
        json.Append(",\"isoSelfIgnored\":").Append(isoSelfForced ? "true" : "false");
        json.Append(",\"isoGroundIgnored\":").Append(isoGroundIgnored ? "true" : "false");
        JNum(json, "isoBodiesTagged", isoBodiesTagged);
        JNum(json, "isoCollidersTagged", isoCollidersTagged);
        JNum(json, "isoVerifyChecked", isoVerifyChecked);
        JNum(json, "isoVerifyLost", isoVerifyLost);
        JNum(json, "isoMarkChecked", isoMarkChecked);
        JNum(json, "isoMarkLost", isoMarkLost);
        JStr(json, "isoFirstLost", isoFirstLost, true);
        JNum(json, "crowdSteps", crowdSteps);
        JNum(json, "crowdPairSteps", (float)crowdPairSteps);
        JNum(json, "crowdShadowMean", crowdSteps > 0 ? crowdPairSteps / (float)crowdSteps : 0f);
        JNum(json, "crowdShadowMax", crowdPairsMax);
        JNum(json, "crowdWindowMeters", CrowdWindowMeters);
        json.Append(",\"genomeBoxOk\":").Append(boxAudit.Length == 0 ? "true" : "false");
        JStr(json, "genomeBoxBad", boxAudit, true);
        JStr(json, "sourceFingerprint", sourceFingerprint, true);
        JStr(json, "buildGuid", buildGuid, true);
        JStr(json, "lawDogStanceController", "Assets/DogStanceController.cs", true);
        JStr(json, "selection", "tournament of 3 inside the best quartile of the NON-FALLER pool (fallback: all measured)", true);
        JNum(json, "eliteMinMeasured", EliteMinMeasured);
        JNum(json, "eliteEverySpawns", EliteEverySpawns);
        JNum(json, "freshShare", FreshShare);
        JNum(json, "minNonFallerPool", MinNonFallerPool);
        JNum(json, "nonFallerMeasured", SelectionNonFallerCount());
        JStr(json, "injectGenome", injectGenome, true);
        JStr(json, "fitnessFormula", "travel_m + 3.0*four_feet + 3.0*band + 3.0*up - 6.0*fell_onset; pool = non-fallers", true);
        JStr(json, "walkKeySeconds", WalkKeySeconds.ToString("0.###", CultureInfo.InvariantCulture), true);
        JStr(json, "pawContactFriction", PawContactFriction.ToString("0.###", CultureInfo.InvariantCulture), true);
        JStr(json, "influenceLog", InfluenceLogName, true);
        json.Append(",\"boxes\":").Append(DogArenaGenome.BoxesJson());
        // ─── CYCLE 69: the expansion's own reports ───
        JNum(json, "geneCount", DogArenaGenome.Count);
        json.Append(",\"defaultBand\":{\"low\":").Append(DogArenaGenome.DefaultBandLow.ToString("0.######", CultureInfo.InvariantCulture))
            .Append(",\"high\":").Append(DogArenaGenome.DefaultBandHigh.ToString("0.######", CultureInfo.InvariantCulture))
            .Append(",\"perGene\":").Append(DogArenaGenome.BandFractionsJson()).Append('}');
        json.Append(",\"defaultBandViolations\":[");
        System.Collections.Generic.List<string> bandBad = DogArenaGenome.AuditDefaultBand();
        for (int i = 0; i < bandBad.Count; i++)
        {
            if (i > 0) json.Append(',');
            json.Append('"').Append(bandBad[i].Replace("\\", "/").Replace("\"", "'")).Append('"');
        }
        json.Append(']');
        JNum(json, "oneFamilyChildren", oneFamilyChildren);
        JNum(json, "wholeGenomeChildren", wholeGenomeChildren);
        JNum(json, "oneFamilyShare", OneFamilyShare);
        json.Append(",\"mutationFamilies\":").Append(MoveFamilyCensusJson());
        JStr(json, "stepSchedule", fineSchedule ? "fine" : "coarse", true);
        JStr(json, "jitterShareOfRange", fineSchedule ? "0.03-0.05" : "0.05-0.10", true);
        JStr(json, "stepScheduleRule", "coarse 5-10 pct of range -> fine 3-5 pct when the best-quartile mean rises < 0.05 m over 4 checkpoints (every 25 measured); the population mean is never the trigger", true);
        json.Append(",\"bestQuartileMeanSeries\":[");
        for (int i = 0; i < bestQuartileMeanSeries.Count; i++)
        {
            if (i > 0) json.Append(',');
            json.Append(bestQuartileMeanSeries[i].ToString("0.######", CultureInfo.InvariantCulture));
        }
        json.Append("],\"bestQuartileMeanAtMeasured\":[");
        for (int i = 0; i < bestQuartileMeanAtMeasured.Count; i++)
        {
            if (i > 0) json.Append(',');
            json.Append(bestQuartileMeanAtMeasured[i].ToString(CultureInfo.InvariantCulture));
        }
        json.Append(']');
        int dp, dr, pb;
        EffectivePopulation(out dp, out dr, out pb);
        json.Append(",\"effectivePopulation\":{\"bodies\":").Append(pb.ToString(CultureInfo.InvariantCulture))
            .Append(",\"distinctParents\":").Append(dp.ToString(CultureInfo.InvariantCulture))
            .Append(",\"distinctRootAncestors\":").Append(dr.ToString(CultureInfo.InvariantCulture))
            .Append(",\"rule\":\"<5 distinct ancestors = add blood, >30 = selection is a lottery\"}");
        json.Append(',').Append("\"clampPressure\":").Append(ClampPressureJson());
        JNum(json, "bestTravelM", measured.Count > 0 ? bestTravel : 0f);
        JNum(json, "bestTravelId", bestTravelId);
        JNum(json, "labelCount", labelCount);
        json.Append(",\"hud\":").Append(hudMesh != null ? "true" : "false");
        JStr(json, "transplantUnmapped", DogArenaGenome.TransplantUnmapped(), true);
        if (best != null)
        {
            JNum(json, "bestBodyId", best.id);
            JNum(json, "bestFitness", best.fitness);
            JStr(json, "bestGenome", DogArenaGenome.ToJson(best.genome), true);
            JStr(json, "bestTransplantFlags", DogArenaGenome.TransplantFlags(best.genome), true);
        }
        json.Append(",\"rows\":[");
        for (int i = 0; i < all.Count; i++)
        {
            if (i > 0) json.Append(',');
            json.Append('{');
            json.Append("\"bodyId\":").Append(all[i].id.ToString(CultureInfo.InvariantCulture));
            JNum(json, "spawnIndex", all[i].spawnIndex);
            JNum(json, "parentId", all[i].parentId);
            JNum(json, "childKind", all[i].childKind);
            JNum(json, "mutationSeed", all[i].mutationSeed);
            JStr(json, "mutationFamily", all[i].mutationFamily ?? "", true);   // CYCLE 69
            JNum(json, "spawnT", all[i].spawnTime);
            json.Append(",\"measured\":").Append(all[i].measured ? "true" : "false");
            if (all[i].measured)
            {
                JNum(json, "fitness", all[i].fitness);
                JNum(json, "travelM", all[i].travel);
                JNum(json, "fourFeet", all[i].fourFeetFraction);
                JNum(json, "band", all[i].bandFraction);
                JNum(json, "up", all[i].upFraction);
                JNum(json, "trunkDrop", all[i].trunkDrop);
                JNum(json, "walkCycles", all[i].walkCycles);
            }
            json.Append(",\"fellInWindow\":").Append(all[i].fellInWindow ? "true" : "false");
            if (all[i].fellTime >= 0f) JNum(json, "fellT", all[i].fellTime);
            json.Append(",\"genome\":").Append(DogArenaGenome.ToJson(all[i].genome));
            json.Append('}');
        }
        json.Append("]}");
        File.WriteAllText(SummaryPath, json.ToString());

        Debug.Log("DOG_ARENA_DONE label=" + label + " sim=" + simulatedSeconds.ToString("0.###", CultureInfo.InvariantCulture)
            + " wall=" + wallSeconds.ToString("0.###", CultureInfo.InvariantCulture)
            + " spawned=" + spawnCount.ToString(CultureInfo.InvariantCulture)
            + " measured=" + measured.Count.ToString(CultureInfo.InvariantCulture)
            + " best=" + bestId.ToString(CultureInfo.InvariantCulture)
            + "@" + bestFitness.ToString("0.######", CultureInfo.InvariantCulture)
            + " iso=" + isolationMode + " layer=" + isoLayer.ToString(CultureInfo.InvariantCulture)
            + " lost=" + (isoVerifyLost + isoMarkLost).ToString(CultureInfo.InvariantCulture)
            + "/" + (isoVerifyChecked + isoMarkChecked).ToString(CultureInfo.InvariantCulture)
            + " crowdMean=" + (crowdSteps > 0 ? (crowdPairSteps / (float)crowdSteps) : 0f).ToString("0.###", CultureInfo.InvariantCulture)
            + " ignorePairs=" + ignorePairs.ToString(CultureInfo.InvariantCulture)
            + " refTraceRows=" + refTraceRows.ToString(CultureInfo.InvariantCulture)
            + " rows=" + RowsPath + " log=" + InfluenceLogPath);
        return json.ToString();
    }

    private static void JNum(StringBuilder sb, string key, float v)
    {
        sb.Append(",\"").Append(key).Append("\":").Append(v.ToString("0.######", CultureInfo.InvariantCulture));
    }

    private static void JNum(StringBuilder sb, string key, int v)
    {
        sb.Append(",\"").Append(key).Append("\":").Append(v.ToString(CultureInfo.InvariantCulture));
    }

    private static void JStr(StringBuilder sb, string key, string v, bool comma)
    {
        if (comma) sb.Append(',');
        sb.Append('"').Append(key).Append("\":\"").Append((v ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"")).Append('"');
    }
}
