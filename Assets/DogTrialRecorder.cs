using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

// Dog stand summary. Reads only. New keys; does not touch human TrialRecorder fields.
public class DogTrialRecorder
{
    private const float SATURATION_THRESHOLD = 0.999f;
    private const float SETTLED_COM_OFFSET = 0.01f;

    // ─── GET UP AFTER A FALL ───
    // Measured physically, not from the stance automaton: dog_ds10-13 sat in
    // GettingUp for the whole run with four paws grounded 5 % of the time, and
    // dog_rep2 walked with the trunk 0.35 m down and 0.08 paws grounded. The
    // The dog's own down rule is gone (C1, 18.09): Assets/FallCriterion.cs
    // decides, on the SETTLED stand height below. What stays here is the get-up
    // side (DOG_UP_*) and the policy that a run ending on its feet is not a fall.
    // The controller's own get-up heap bar (DogStanceController.getUpConnectDrop)
    // and the number of limb columns the CSV tail carries.
    private const float DOG_HEAP_BAR = 0.20f;
    private const int GetUpLimbColumns = 8;
    // The get-up side keeps its own stand threshold: trunk back within 12 cm of
    // the standing pose with at least three paws planted.
    private const float DOG_UP_DROP = 0.12f;
    private const float DOG_UP_HOLD_SECONDS = 0.20f;
    // The settled stand height is measured over this much of the run (frames
    // with 3+ paws planted and the pitch under 45 deg), then frozen.
    private const float DOG_BASELINE_WINDOW_END = 5.0f;
    // A push is 12 cm of trunk travel off the lowest point of the heap: a
    // twitch under 2 cm is the body settling, not an attempt to stand.
    private const float DOG_PUSH_LIFT = 0.12f;

    private float dogFallTime = -1f;
    private float dogGetUpSeconds = -1f;
    private int dogGetUpAttempts;
    private bool dogGetUpSuccess;
    private int samplesDown;
    // C1: the shared criterion's per-dog latch and its measured settled stand
    // height (see Assets/FallCriterion.cs). Nothing here is a second rule.
    private FallCriterion.Latch fallLatch;
    private FallCriterion.Baseline standBaseline;
    private float upAge;
    // Deepest chest / pelvis position since the last counted push. Both, not
    // the trunk max: dog_q1 held pelvisDrop pinned at 0.32 through the whole
    // heap while the chest rose 0.13, and dog_gu4 sat on its haunches with the
    // chest 0.07 off the floor and the pelvis 0.32 down — the push can come
    // from either end, so each is measured against its own floor.
    private float pushFloorChestDrop;
    private float pushFloorPelvisDrop;
    private bool dogDownNow;

    private readonly Dog dog;
    private readonly TrialRunInfo info;
    private readonly DogStanceController stance;
    private readonly Transform chest;
    private readonly Transform pelvis;

    private readonly StringBuilder rows = new StringBuilder();
    private readonly char[] numberBuffer = new char[32];
    private int samples;
    // DOG STEP CLOCK (harness only, 19.09): the executed physics steps of the run
    // that owns this recorder, set by HeadlessTrial.RunDogTrial just before the
    // summary is written. The dog loop's clock is derived from this count, so a
    // healthy run reads `executedSteps == samples` and
    // `simulatedSeconds == executedSteps * fixedDeltaTime`; a run that sampled a
    // cadence the physics never ran (the 19.09 trap: 5002..5007 steps against
    // 5001 samples on one build) is visible in the JSON instead of being averaged
    // away.
    private int executedSteps = -1;
    private double sumComOffsetSquared;
    private float maxAbsChestPitch;
    private float maxAbsPelvisTilt;
    private float maxAbsLumbarAngle;
    private float maxAbsComOffset;
    private float fallTime = -1f;
    private int samplesSaturated;
    private int samplesFourFeet;
    private float startChestY;
    private float startPelvisY;
    private float minChestY = float.MaxValue;
    private float minPelvisY = float.MaxValue;
    private float lastChestPitch;
    private float lastPelvisTilt;
    private float lastLumbar;
    private float lastComOffset;
    private float lastChestY;
    private float lastPelvisY;
    private float pushTime = -1f;
    private float pushImpulse;
    private float lastUnsettledTime = -1f;
    private readonly JawStrike jawStrike;
    private readonly Damageable preyLife;
    private string lastState = "";
    private int lastLeapCount;
    private float lastJawAngle;
    private int maxLeapCount;
    private bool sawLeapCrouch;
    private bool sawLeapPush;
    private bool sawLeapAir;
    private bool sawLeapBite;
    private bool walkWindowOpen;
    private float walkStartComX;
    private float walkStartTime = -1f;
    private float walkEndComX;
    private float walkEndTime = -1f;
    // CYCLE 95: the walk window closes at the first fall. walkClosedByFall
    // latches the close; the ring holds the last 0.40 s of (time, comX) so
    // the close can take the com at the fall's OWN frame (the fall latch
    // reports the run's onset, up to FallCriterion.SustainSeconds + one
    // frame before it is declared).
    private bool walkClosedByFall;
    private const int WalkRingFrames = 80;
    private readonly float[] walkRingTime = new float[WalkRingFrames];
    private readonly float[] walkRingComX = new float[WalkRingFrames];
    private int walkRingCursor;
    private int walkRingFilled;
    // ─── CYCLE 99: THE LETTER'S SPAN ───
    // The accepted letter (comTravelX / comSpeedX) reads the MEAN over the
    // whole closed window, and that window's length is the dose's own
    // business (c98: a 13.4 s survival read 0.223 against a 2.2 s fast
    // fall's 0.390 - a slow-but-surviving walk read WORSE than a fast
    // fall), because the mean mixes the gate, the walk and the fall's
    // tail while its denominator moves with the dose. The letter gains a
    // SPAN: the first DOG_WALK_LETTER_SPAN seconds of the walk window,
    // clamped at the window's end - the same read for every row and every
    // dose, so pace is read like-for-like and endurance stays in the
    // window keys. Read-only: no control law reads any of this.
    private const float DOG_WALK_LETTER_SPAN = 2.0f;
    private bool walkSpanEndSet;
    private float walkSpanEndTime = -1f;
    private float walkSpanEndComX;
    private int samplesWalkActive;
    private int lastWalkCycleCount;
    private int lastSenseTick;
    private float sensorComXMin = float.MaxValue;
    private float sensorComXMax = float.MinValue;
    private float sensorMarginMin = float.MaxValue;
    private float sensorMarginMax = float.MinValue;
    private int samplesOnsetWindow;
    private int samplesOnsetCollapsed;
    private int samplesOnsetPosed;
    // Independent witness of the automaton's get-up flag: the trunk drop (from
    // THIS recorder's own start-of-run stance) and the paw count at the frame
    // DogStanceController first said getUpSuccess. The old flag was a state
    // name, so it turned true at 4.58 s with the trunk 0.325 m down; these two
    // numbers say whether the claim was physical.
    private bool sawAutomatonUp;
    private float automatonUpTime = -1f;
    private float automatonUpTrunkDrop;
    private int automatonUpPaws;
    // Get-up witness: the heap bar and the push half of the tuck-push cycle.
    private float maxTrunkBar;
    private float lastTrunkBar;
    private int samplesHeap;
    private int samplesPush;

    // ─── CYCLE 1 INSTRUMENTS (per-frame signals only; no decision is taken on
    // any of them in this cycle) ───
    // Each value below is published as a CSV column on every frame and summed up
    // in the JSON. Nothing here is read by a control law, so the physics of the
    // run must stay bit-identical: that is this cycle's acceptance.
    private bool trunkLiftBaseValid;
    private float trunkLiftBase;
    private float trunkLift;
    private float trunkLiftMax;
    private int trunkLiftMaxFrame = -1;
    // Per-frame capture-point margin (supportMargin, m) kept for p05; the count
    // of frames outside the support interval is a signal, not a flag.
    private readonly List<float> captureMargins = new List<float>();
    private float captureMarginMin = float.MaxValue;
    private int captureMarginMinFrame = -1;
    private double sumCaptureMargin;
    private int captureMarginNegativeFrames;
    private readonly double[] sumPawImpulse = new double[4];
    private double sumFnOverWeight;
    private float sumFnOverWeightMax;
    private float controllerTrunkDropMax;
    private float controllerTrunkDropFinal;
    private float controllerVsRecorderMax;
    private int controllerVsRecorderMaxFrame = -1;
    // Every new column, every frame: a non-zero count is a failed acceptance.
    private int cycle1NanCount;

    // ─── CYCLE 3 SENSOR (read-only; nothing here feeds a control law) ───
    // The extrapolated CoM (capture point) of DogStanceController: xi in m,
    // its height over the slab in m, and its signed margin against the support
    // interval in m. Kept BESIDE the projection margin above, never instead of
    // it: the two differ only by the momentum term, and the lead of the first
    // over the second is the number this cycle exists to publish.
    private readonly List<float> captureXiMargins = new List<float>();
    private float captureXiMarginMin = float.MaxValue;
    private int captureXiMarginMinFrame = -1;
    private double sumCaptureXiMargin;
    private int captureXiMarginNegativeFrames;
    private int captureXiFirstNegativeFrame = -1;
    private float captureXiFirstNegativeTime = -1f;
    // Same first-crossing witnesses for the CoM projection, so the lead is a
    // recorded pair of frames and not a re-derivation.
    private int projectionFirstNegativeFrame = -1;
    private float projectionFirstNegativeTime = -1f;
    private float captureXiHeightMin = float.MaxValue;
    private double sumCaptureXiHeight;

    // ─── CYCLE 21 SENSOR: the paw sole point (read-only; nothing below feeds a
    // control law) ───
    // The card's `slip in metres` could not be formed from this trace: the
    // grounded flags say THAT a paw is down, never WHERE (cycle 12, section 9).
    // pawSoleXFR/FL/RR/RL is the world X of each paw's sole point,
    // paw.transform.TransformPoint(paw.anchor) — the point the walk law itself
    // reads at DogStanceController.LatchWalkLiftPoint (:3172) and the point the
    // sole band is measured against (PawSoleCorners :5594-5601, WalkSoleBandM).
    // Paw order FR, FL, RR, RL — the CSV grounded-flag order (PawCollider :5560).
    // The slab does not move, so a GROUNDED paw's world X can only change by
    // sliding: the slip of a grounded run is the sum of |delta pawSoleX| over it.
    // The per-paw joint references come from the controller's own public fields
    // (frontRightPawJoint .. rearLeftPawJoint, DogStanceController :997-998);
    // nothing was added to the controller.
    private readonly float[] soleX = new float[4];
    private readonly float[] lastSoleX = new float[4];
    private readonly bool[] soleSeeded = new bool[4];
    private readonly bool[] lastSoleGrounded = new bool[4];
    private readonly bool[] soleRunOpen = new bool[4];
    private readonly float[] soleRunSlip = new float[4];
    private readonly double[] soleSlipRun = new double[4];
    private readonly double[] soleSlipWalk = new double[4];
    // ─── CYCLE 104: THE SUMS' WINDOW-CLOSED TWIN ───
    // The accepted slip sums (pawSoleSlipWalk*) accrue while walking and
    // run to the sim end, so they absorb the post-fall drift (measured on
    // the c103 rows: w04's walk sum 22.704 m against 3.767 m of grounded
    // slip inside the closed window - 83 % of the accepted number is after
    // the fall; c93 section 8, c96 section 8 item 3, c97's note). The twin
    // re-reads the SAME meter - the c21 grounded rule, one increment per
    // frame - over the two segments the recorder already publishes: the
    // walk window [walkStartTime, walkEndTime] (Window*, the c95 end at the
    // first fall's own time) and the letter's span [walkStartTime,
    // walkSpanEndTime] (Span*, the c99 2.00 s clamp), so the sums and the
    // letter read the same segment and pace and slip are both like-for-like.
    // The sums are taken at WRITE time from the per-frame list below, over
    // the published ends - so the recorded twin and an offline read of the
    // CSV's own columns give the same number. Read-only: no control law
    // reads any of this; no CSV column moves.
    // This frame's own increment, per paw (0 unless the c21 rule accrued).
    private readonly float[] soleStepSlip = new float[4];
    // 5 floats per frame from the window's open onward: t, FR, FL, RR, RL.
    private readonly List<float> slipStepRows = new List<float>();
    private readonly int[] soleRunCount = new int[4];
    private float soleSlipMaxRun;
    private int soleSlipMaxRunPaw = -1;
    // Non-finite readings of the four cycle-21 columns; kept separate from
    // cycle1NanCount so that this pre-existing key keeps reading the
    // pre-existing columns only. A non-zero count is a failed acceptance.
    private int pawSoleNanCount;

    // ─── CYCLE 91 SENSOR: the per-paw plant geometry (read-only; nothing below
    // feeds a control law) ───
    // The pad lag the c68 section 7.2 carried (the rear pads airborne 0.14-0.63 s
    // against the 0.40 s cap windows) was read off the grounded flags alone: the
    // flags say WHEN a pad left the floor, never how high it was or how fast it
    // came back. The front FK could give the front pads a clearance (noisy, p95
    // 0.065 m) and refuses the hind pair (c67 section 3.3), so the geometry is
    // read off the paws themselves. Per paw, per frame: pawClearFR/FL/RR/RL =
    // that paw's own pad face over the slab (its collider's bounds.min.y minus
    // the slab top, the rearClearM / frontClearM measure, read in EVERY phase,
    // not only the get-up); pawDropFR/FL/RR/RL = the rate that clearance closes,
    // (previous frame's clearance - this frame's) / dt, m/s, positive while the
    // pad descends toward the slab — the arrival speed the soft plant (c67
    // section 5.4) is priced against; 0 on the first sampled frame (no previous
    // frame to difference), NaN only when a reading is non-finite. The slab top
    // is probed once, the mirror of DogStanceController.CacheWalkGround
    // (:2176-2185: y = -2.0 unless a "Ground" object exists). The JSON publishes
    // the per-paw aggregates (clearance minimum, touch count, fastest and last
    // arrival speed, longest air run). No control law reads any of it: the run
    // must stay bit-identical on every pre-existing column (this cycle's
    // acceptance).
    private readonly float[] pawClear = new float[4];
    private readonly float[] pawDrop = new float[4];
    private readonly float[] lastPawClear = new float[4];
    private readonly bool[] pawClearSeeded = new bool[4];
    // One clock for all four paws: the drop is the frame-to-frame difference of
    // each clearance, so the interval is the SAMPLED frame's, not a per-paw one.
    private float lastPawClearFrameTime = -1f;
    private readonly Collider2D[] pawCols = new Collider2D[4];
    private bool slabTopProbed;
    private float slabTopY = -2f;
    // The touch tally: a touch = an airborne->grounded edge; the arrival speed of
    // that touch = the last airborne frame's drop value (the frame the pad was
    // still free); the air run = the seconds from the previous grounded->airborne
    // edge to the touch.
    private readonly int[] pawTouchCount = new int[4];
    private readonly float[] pawTouchDropMax = { -1f, -1f, -1f, -1f };
    private readonly float[] pawTouchDropLast = new float[4];
    private readonly float[] pawAirRunMax = new float[4];
    private readonly float[] pawCarryDrop = new float[4];
    private readonly float[] pawCarryAirStart = { -1f, -1f, -1f, -1f };
    private readonly bool[] pawSawTouch = new bool[4];
    private readonly bool[] lastPawPlanted = new bool[4];
    private readonly float[] pawClearMin = { 1000f, 1000f, 1000f, 1000f };
    // Non-finite readings of the cycle-91 clearance columns; kept separate from
    // pawSoleNanCount and cycle1NanCount so those pre-existing keys keep reading
    // the pre-existing columns only. A non-zero count is a failed acceptance.
    private int pawClearNanCount;

    // ─── CYCLE 25 SENSOR: the righting rock (read-only; nothing below feeds a
    // control law) ───
    // The cycle-25 pump is gated by the trunk's OWN rock, so the report has to
    // carry the rock itself: per frame the gate's rate, its latched direction
    // and the pair it drives (all three read from the controller's witnesses,
    // CSVs columns rockRate / rockDir / rockPair), and per run the rock's own
    // count, its deepest deviation off the flat-on-back pose and the crossing
    // (JSON rightingRocks / rightingMaxDevDeg / rightingCrossSeconds /
    // rightingGrowRocks). The per-run numbers are re-derived HERE from
    // stance.chestPitch - the same column every reader parses - with the same
    // unwrap and the same deadband the controller uses, so a reader can
    // re-derive all of them from the CSV alone and none of them depend on the
    // controller's internals. dev = 180 - |unwrapped pitch| (deg): the
    // displacement off the flat-on-back pose, sign = which way off the back.
    private float rockPrevPitch = float.NaN;
    private float rockUnwrapped;
    private float rockRate;
    private int rockDir;
    private float rockInvertedBorn = -1f;
    private float rockMaxAbsDev;
    private int rockRocks;
    private int rockGrowRun;
    private int rockGrowRunMax;
    private float rockPrevApexDev;
    private bool rockHaveApex;
    private int rockCrossStreak;
    private float rockCrossSeconds = -1f;
    // The card's own bars, copied (not read from the law): |chestPitch| >= 100
    // is a dog on its back (DOG-3 / card DOG-5), the crossing is |chestPitch|
    // <= 100 held 20 frames.
    private const float DOG_INVERTED_ABS_PITCH = 100f;
    private const int DOG_CROSS_HOLD_FRAMES = 20;
    // The rock's own construction, mirrored from the controller so the JSON's
    // rock count is the same measurement: the EMA tau and the reversal
    // deadband the gate latches on (DogStanceController GetUpRockTauSeconds /
    // GetUpRockDeadbandDegPerSec, cycle 25).
    private const float DOG_ROCK_TAU = 0.02f;
    private const float DOG_ROCK_DEADBAND = 60f;

    // ─── CYCLE 26 SENSOR: the catch at the line (read-only; nothing below feeds
    // a control law) ───
    // The catch's own witnesses, read from the controller: the band it armed in
    // (armed), whether a hind pad held the floor there (pad), which paw (paw:
    // 0 = RR, 1 = RL, 3 = both), the dev at that touch (dev, deg = |chestPitch|
    // - 180, negative at the card's line — the sign the cycle reports print),
    // the extension it commanded (push) and the hind pads' own measured
    // clearance over the slab (rearClearM, m — the servo's input, and the
    // geometry the report has to carry). Per run: the touches (a rising edge of
    // pad), the seconds the band / a pad / the press held, the deepest dev at a
    // touch, the lowest clearance seen in the band, the seconds to the card's
    // crossing (|chestPitch| <= 100 held 20 frames) measured EVERY frame — the
    // c25 streak lives inside the inverted branch and cannot see the band — and
    // whether that crossing fired at or after a catch touch.
    private int catchArmedSamples;
    private int catchPadSamples;
    private int catchPushSamples;
    private int catchTouchCount;
    private bool catchPadPrev;
    private float catchHoldSeconds;
    private float catchPushSeconds;
    private float catchDeepestDev = 1000f;
    private float catchMinClear = -1f;
    private int catchCrossStreak;
    private float catchCrossSeconds = -1f;
    private bool catchCrossed;
    // ─── CYCLE 27 SENSOR: the back-plant's own numbers (read-only) ───
    // The touch's side, and the sole / the hip pivot signed against the CoM at
    // that first touch (m, + = forward of the CoM), plus the most negative
    // signed sole distance the band reached. Nothing here feeds a control law.
    private bool catchSoleComSeeded;
    private float catchSoleComFirst;
    private float catchHipComFirst;
    private float catchSoleComMin = 1000f;
    private string catchSideFirst = "none";
    // ─── CYCLE 28 SENSOR: the rump corner (read-only) ───
    // The pelvis's own rear corner (the box's caudal-dorsal corner), the one
    // contact that can sit behind the CoM without any reach: the height over the
    // slab and the signed distance to the CoM at the band's first armed frame
    // (the hold's own start), and the deepest both that corner and any corner of
    // the pelvis got over the band. Nothing here feeds a control law.
    private bool rumpSeeded;
    private float rumpClearFirst;
    private float rumpComFirst;
    private float rumpClearMin = 1000f;
    private float rumpLowMin = 1000f;
    // ─── CYCLE 36 SENSOR: the front pads' own clearance (read-only) ───
    // The front pair's own geometry, the column the front limbs never had: the
    // lowest front pad's collider clearance at the get-up's first frame, and the
    // deepest that pair ever got over the whole get-up. Nothing here feeds a law.
    private bool frontClearSeeded;
    private float frontClearFirst;
    private float frontClearMin = 1000f;
    // ─── CYCLE 31 SENSOR: the claws (read-only) ───
    // The grip's own run-level witness, aggregated from the controller's
    // per-frame claw columns: the frames a grip was held (and their share of
    // the catch's armed frames), the first grip's frame/time and side, the
    // deepest anchored slip a grip held before letting go (m), the biggest
    // tangential load a grip carried (N) and the capacity beside it (N), the
    // biggest Tf/Cap (1 = the solver's own sliding limit), the frames the load
    // sat at the capacity (>= 0.98), the biggest |moment| about the CoM
    // (N*m), the tears (a slip release while the pad was still loaded) with the
    // first tear's frame, and the pair coefficient both the capacity and the
    // measured Tf/Fn the run actually reached. Nothing here feeds a control law.
    private int clawEngageFrames;
    private int clawEngageFirstFrame = -1;
    private float clawEngageFirstTime = -1f;
    private int clawSideMask;
    private float clawSlipMax;
    private float clawTfMax;
    private float clawCapAtTfMax;
    private float clawFracMax;
    private int clawAtCapFrames;
    private float clawMomentMax;
    private float clawMuPair;
    private float clawMuMeasured;
    private int clawRipouts;
    private int clawRipoutFirstFrame = -1;
    // ─── CYCLE 32 SENSOR: the fold (read-only) ───
    // The fold's own clock and command, aggregated from the controller's fold
    // witnesses, plus the lumbar's own extremes while the fold was running (the
    // `lumbar` column is the hinge's jointAngle, already in the row). Nothing
    // here feeds a control law.
    private int foldFrames;
    private int foldFirstFrame = -1;
    private float foldFirstTime = -1f;
    private float foldWeightMax;
    private int foldOpenFrames;
    private float foldLumbarMin = 1000f;
    private float foldLumbarMax = -1000f;
    private int foldLumbarMaxFrame = -1;
    private float foldLumbarMaxTime = -1f;
    private float foldNeckMin = 1000f;
    private float foldNeckMax = -1000f;
    private float foldLumbarAtPlantMax = -1000f;
    private int foldLumbarAtPlantFrame = -1;
    private int foldPastStopFrames;
    private float foldPastStopMax;

    public DogTrialRecorder(Dog dog, TrialRunInfo info)
    {
        this.dog = dog;
        this.info = info;
        stance = dog != null ? dog.stance : null;
        chest = dog != null ? dog.transform.Find("Chest") : null;
        pelvis = dog != null ? dog.transform.Find("Pelvis") : null;

        jawStrike = dog != null ? dog.GetComponent<JawStrike>() : null;
        DogPrey prey = UnityEngine.Object.FindFirstObjectByType<DogPrey>();
        preyLife = prey != null ? prey.GetComponent<Damageable>() : null;

        rows.AppendLine("t,chestPitch,pelvisTilt,lumbar,chestY,pelvisY,comX,comY,comOffset,frG,flG,rrG,rlG,sat,state,leapCount,jawAngle,paws,down,fell,upSuccess,hipL,hipR,kneeL,kneeR,shoL,shoR,elbL,elbR,trunkBar,push,trunkLift,capturePointMargin,pawNormalImpulseFR,pawNormalImpulseFL,pawNormalImpulseRR,pawNormalImpulseRL,sumFnOverWeight,controllerTrunkDrop,captureXi,captureHeight,captureMargin,pawSoleXFR,pawSoleXFL,pawSoleXRR,pawSoleXRL,rockRate,rockDir,rockPair,catchArmed,catchPad,catchPaw,catchDevDeg,catchPush,rearClearM,catchSoleComM,catchHipComM,rumpClearM,rumpComM,rumpLowClearM,rumpLowComM,clawGripR,clawGripL,clawPaw,clawSlipM,clawFnN,clawTfN,clawCapN,clawTfFrac,clawMomentNm,clawRipout,foldW,foldDeg,foldNeckDeg,neckJ,foldLimDeg,handOver,pike,frontClearM,climbCommit,pawClearFR,pawClearFL,pawClearRR,pawClearRL,pawDropFR,pawDropFL,pawDropRR,pawDropRL");
    }

    public void MarkPush(float time, float impulse)
    {
        pushTime = time;
        pushImpulse = impulse;
    }

    // Called by HeadlessTrial.RunDogTrial before the summary is written: how many
    // physics steps the run really executed (see the `dogStepping` block there).
    public void MarkExecutedSteps(int steps)
    {
        executedSteps = steps;
    }

    public void Sample(float time)
    {
        if (stance == null || chest == null) return;

        float chestY = chest.position.y;
        float pelvisY = pelvis != null ? pelvis.position.y : chestY;
        float pitch = stance.chestPitch;
        float pTilt = stance.pelvisTilt;
        float lumbar = stance.lumbarAngle;
        float comOff = stance.comOffsetX;

        // Get-up witness, read-only: the controller's heap bar (trunk below the
        // measured stand pose) and the push half of its tuck-push cycle.
        float trunkBarNow = stance.GetUpTrunkDropBar();
        lastTrunkBar = trunkBarNow;
        if (trunkBarNow > maxTrunkBar) maxTrunkBar = trunkBarNow;
        if (trunkBarNow >= DOG_HEAP_BAR) samplesHeap++;
        if (stance.getUpPushingNow) samplesPush++;

        if (samples == 0)
        {
            startChestY = chestY;
            startPelvisY = pelvisY;
            minChestY = chestY;
            minPelvisY = pelvisY;
        }

        if (chestY < minChestY) minChestY = chestY;
        if (pelvisY < minPelvisY) minPelvisY = pelvisY;
        lastChestY = chestY;
        lastPelvisY = pelvisY;
        lastChestPitch = pitch;
        lastPelvisTilt = pTilt;
        lastLumbar = lumbar;
        lastComOffset = comOff;

        float absPitch = Mathf.Abs(pitch);
        if (absPitch > maxAbsChestPitch) maxAbsChestPitch = absPitch;
        float absPelvis = Mathf.Abs(pTilt);
        if (absPelvis > maxAbsPelvisTilt) maxAbsPelvisTilt = absPelvis;
        float absLumbar = Mathf.Abs(lumbar);
        if (absLumbar > maxAbsLumbarAngle) maxAbsLumbarAngle = absLumbar;
        float absOff = Mathf.Abs(comOff);
        if (absOff > maxAbsComOffset) maxAbsComOffset = absOff;
        sumComOffsetSquared += comOff * comOff;

        if (stance.AllPawsGrounded()) samplesFourFeet++;
        float maxActivation = stance.MaxStanceActivation();
        if (maxActivation >= SATURATION_THRESHOLD) samplesSaturated++;

        if (fallTime < 0f && (stance.currentState == DogStanceController.StanceState.Falling
            || stance.currentState == DogStanceController.StanceState.Fallen))
            fallTime = time;

        // ─── PHYSICAL DOWN / GET UP (C1: the one project-wide criterion) ───
        // The local `trunk 0.25 m down AND <= 2 paws` rule is gone. It was a
        // second implementation of the same idea and it lied: 25 s of a dog
        // lying on its side read `fell` false because the stance automaton
        // stayed in Walk. Assets/FallCriterion.cs decides now - trunk 0.25 m
        // below its own SETTLED stand height, or |pitch| > 60 deg, held 0.15 s.
        float chestDropNow = startChestY - chestY;
        float pelvisDropNow = startPelvisY - pelvisY;
        float trunkDropNow = Mathf.Max(chestDropNow, pelvisDropNow);
        int pawsGrounded = (stance.frontRightGrounded ? 1 : 0) + (stance.frontLeftGrounded ? 1 : 0)
            + (stance.rearRightGrounded ? 1 : 0) + (stance.rearLeftGrounded ? 1 : 0);
        // The settled stand height is the trunk height of frames that are
        // evidence of standing (3+ paws planted, pitch under 45 deg), measured
        // over the first seconds of the run and then frozen. startChestY - the
        // spawn pose - is only the documented fallback for a run that never
        // stood; the spawn pose is exactly what made the arena's copy lie.
        float dogTilt = Mathf.Max(Mathf.Abs(pitch), Mathf.Abs(pTilt));
        if (time < DOG_BASELINE_WINDOW_END && pawsGrounded >= 3)
            standBaseline.Feed(chestY, dogTilt);
        else if (time >= DOG_BASELINE_WINDOW_END)
            standBaseline.Freeze();
        dogDownNow = FallCriterion.Step(ref fallLatch, standBaseline.Value(startChestY),
            chestY, dogTilt, Time.fixedDeltaTime, time);

        // The automaton's own claim, witnessed: recorded at the frame it turns
        // true, from this recorder's stance, never from the automaton's bar.
        if (!sawAutomatonUp && stance.getUpSuccess)
        {
            sawAutomatonUp = true;
            automatonUpTime = time;
            automatonUpTrunkDrop = trunkDropNow;
            automatonUpPaws = pawsGrounded;
        }

        if (dogDownNow)
        {
            samplesDown++;
            // This recorder's own POLICY, named as a policy and not as a second
            // criterion: a run that ends standing is not reported as a fall. The
            // down/up decision itself is the shared one.
            if (dogFallTime < 0f && fallLatch.fired && !dogGetUpSuccess)
                dogFallTime = fallLatch.fireTime;
            if (chestDropNow > pushFloorChestDrop) pushFloorChestDrop = chestDropNow;
            if (pelvisDropNow > pushFloorPelvisDrop) pushFloorPelvisDrop = pelvisDropNow;
        }

        // ─── CYCLE 1 INSTRUMENTS (measure only; no decision below reads them) ───
        // trunkLift: max(chestY, pelvisY) minus the same value in the frame the
        // fall was DECLARED — the first frame the shared criterion reads down,
        // i.e. the frame the CSV `fell` column turns 1. Zero before a fall: there
        // is nothing to lift off yet, and a run that never falls has no lift.
        if (dogDownNow && !trunkLiftBaseValid)
        {
            trunkLiftBaseValid = true;
            trunkLiftBase = Mathf.Max(chestY, pelvisY);
        }
        trunkLift = trunkLiftBaseValid ? Mathf.Max(chestY, pelvisY) - trunkLiftBase : 0f;
        if (trunkLift > trunkLiftMax)
        {
            trunkLiftMax = trunkLift;
            trunkLiftMaxFrame = samples;
        }

        // capturePointMargin: the support margin the controller computes every
        // step and feeds to no control law (its only consumer is a log line).
        // Sign as computed at DogStanceController.Sense: + inside the paw AABB,
        // 0 on the edge, - outside. p05 and min are summarised in the JSON.
        float marginNow = stance.supportMargin;
        captureMargins.Add(marginNow);
        sumCaptureMargin += marginNow;
        if (marginNow < captureMarginMin)
        {
            captureMarginMin = marginNow;
            captureMarginMinFrame = samples;
        }
        if (marginNow < 0f) captureMarginNegativeFrames++;

        // Per-paw ground reaction: the step's normal impulse (N*s) per paw, and
        // its weight fraction. The prediction a later cycle has to confirm or
        // refute: a dog carried by its belly reads well under 1.0 here.
        for (int paw = 0; paw < 4; paw++)
            sumPawImpulse[paw] += stance.pawNormalImpulse[paw];
        float fnOverWeightNow = stance.pawWeightFraction;
        sumFnOverWeight += fnOverWeightNow;
        if (fnOverWeightNow > sumFnOverWeightMax) sumFnOverWeightMax = fnOverWeightNow;

        // controllerTrunkDrop: the controller's OWN bar for the walk heap
        // (WalkStandDrop, m), logged beside this recorder's independent trunkDrop
        // (trunkBar already carries GetUpTrunkDropBar). Where the two disagree by
        // more than 0.05 m the automaton's own state cannot be trusted.
        float controllerDropNow = stance.walkStandDropBar;
        controllerTrunkDropFinal = controllerDropNow;
        if (controllerDropNow > controllerTrunkDropMax) controllerTrunkDropMax = controllerDropNow;

        // ─── CYCLE 3 SENSOR: the extrapolated CoM, read beside the projection ───
        // Same support interval, one extra term: captureXiMargin = the signed
        // margin of xi = x_com + v_x/omega. Where the projection says "inside"
        // and this says "outside", the body is leaving its support and the
        // projection is the one that is lying. The pairs of first crossings are
        // recorded together so the lead (in seconds) is measured, not argued.
        float captureXiNow = stance.capturePointX;
        float captureXiMarginNow = stance.capturePointMargin;
        float captureXiHeightNow = stance.capturePointHeight;
        captureXiMargins.Add(captureXiMarginNow);
        sumCaptureXiMargin += captureXiMarginNow;
        sumCaptureXiHeight += captureXiHeightNow;
        if (captureXiHeightNow < captureXiHeightMin) captureXiHeightMin = captureXiHeightNow;
        if (captureXiMarginNow < captureXiMarginMin)
        {
            captureXiMarginMin = captureXiMarginNow;
            captureXiMarginMinFrame = samples;
        }
        if (captureXiMarginNow < 0f)
        {
            captureXiMarginNegativeFrames++;
            if (captureXiFirstNegativeFrame < 0)
            {
                captureXiFirstNegativeFrame = samples;
                captureXiFirstNegativeTime = time;
            }
        }
        if (marginNow < 0f && projectionFirstNegativeFrame < 0)
        {
            projectionFirstNegativeFrame = samples;
            projectionFirstNegativeTime = time;
        }

        // ─── CYCLE 21 SENSOR: publish each paw's sole X, and price its slip ───
        // The instrument the card's `slip in metres` needs (cycle 12, section 9):
        // the world X of paw.transform.TransformPoint(paw.anchor). Read here,
        // every frame, in the CSV's own paw order (FR, FL, RR, RL — the grounded
        // flags' order, DogStanceController.PawCollider :5560). While a paw's
        // grounded flag is 1 its pad is on a slab that does not move, so every
        // metre its sole X travels is a metre SLID; the run tally below is the
        // card's field, and the JSON publishes it per paw. No control law reads
        // any of it: the run must stay bit-identical on every pre-existing
        // column (this cycle's acceptance).
        bool inWalkNow = stance.IsWalk() || stance.walkWanted;
        for (int paw = 0; paw < 4; paw++)
        {
            HingeJoint2D pawJoint = PawJoint(paw);
            // CYCLE 104: this frame's own increment (the twin's list is
            // pushed after the window and the span are current).
            soleStepSlip[paw] = 0f;
            soleX[paw] = pawJoint != null
                ? pawJoint.transform.TransformPoint(pawJoint.anchor).x
                : float.NaN;
            bool groundedNow = PawGroundedNow(paw);
            if (groundedNow && !soleRunOpen[paw])
            {
                // A run opens here; a run of one frame carries no delta.
                soleRunOpen[paw] = true;
                soleRunSlip[paw] = 0f;
                soleRunCount[paw]++;
            }
            if (groundedNow && soleRunOpen[paw] && lastSoleGrounded[paw]
                && soleSeeded[paw] && IsFinite(soleX[paw]) && IsFinite(lastSoleX[paw]))
            {
                float stepSlip = Mathf.Abs(soleX[paw] - lastSoleX[paw]);
                soleStepSlip[paw] = stepSlip;   // CYCLE 104: the twin's increment
                soleRunSlip[paw] += stepSlip;
                soleSlipRun[paw] += stepSlip;
                if (inWalkNow) soleSlipWalk[paw] += stepSlip;
            }
            if (!groundedNow && soleRunOpen[paw])
            {
                // The paw left the ground: the run's slip is final.
                soleRunOpen[paw] = false;
                if (soleRunSlip[paw] > soleSlipMaxRun)
                {
                    soleSlipMaxRun = soleRunSlip[paw];
                    soleSlipMaxRunPaw = paw;
                }
            }
            lastSoleGrounded[paw] = groundedNow;
            lastSoleX[paw] = soleX[paw];
            soleSeeded[paw] = true;
        }

        // ─── CYCLE 91 SENSOR: publish the per-paw plant geometry ───
        float slabTopNow = SlabTopY();
        float clearDtNow = lastPawClearFrameTime >= 0f ? time - lastPawClearFrameTime : -1f;
        for (int paw = 0; paw < 4; paw++)
        {
            Collider2D pawCol = PawCol(paw);
            pawClear[paw] = pawCol != null ? pawCol.bounds.min.y - slabTopNow : float.NaN;
            if (!IsFinite(pawClear[paw])) pawClearNanCount++;
            if (IsFinite(pawClear[paw]) && pawClear[paw] < pawClearMin[paw])
                pawClearMin[paw] = pawClear[paw];
            float dropNow = 0f;
            if (pawClearSeeded[paw] && clearDtNow > 0f)
            {
                dropNow = IsFinite(pawClear[paw]) && IsFinite(lastPawClear[paw])
                    ? (lastPawClear[paw] - pawClear[paw]) / clearDtNow
                    : float.NaN;
            }
            pawDrop[paw] = dropNow;
            bool plantedNow = PawGroundedNow(paw);
            if (plantedNow && !lastPawPlanted[paw])
            {
                // A touch: the arrival speed is the last airborne frame's drop.
                pawTouchCount[paw]++;
                float arrival = pawCarryDrop[paw];
                if (IsFinite(arrival))
                {
                    if (arrival > pawTouchDropMax[paw]) pawTouchDropMax[paw] = arrival;
                    pawTouchDropLast[paw] = arrival;
                }
                float airRun = pawCarryAirStart[paw] >= 0f ? time - pawCarryAirStart[paw] : -1f;
                if (pawSawTouch[paw] && airRun > pawAirRunMax[paw]) pawAirRunMax[paw] = airRun;
                pawSawTouch[paw] = true;
                pawCarryAirStart[paw] = -1f;
            }
            else if (!plantedNow && lastPawPlanted[paw] && pawCarryAirStart[paw] < 0f)
            {
                pawCarryAirStart[paw] = time;
            }
            lastPawPlanted[paw] = plantedNow;
            pawCarryDrop[paw] = dropNow;
            lastPawClear[paw] = pawClear[paw];
            pawClearSeeded[paw] = true;
        }
        lastPawClearFrameTime = time;

        float barGap = Mathf.Abs(controllerDropNow - trunkDropNow);
        if (barGap > controllerVsRecorderMax)
        {
            controllerVsRecorderMax = barGap;
            controllerVsRecorderMaxFrame = samples;
        }

        // NaN watch on every new column of every frame, so "no NaN" is a number a
        // checker can read and not an eyeball. The cycle-21 paw-sole columns get
        // their OWN counter (pawSoleNanCount) so that this pre-existing key stays
        // a function of the pre-existing columns only (cycle 21's rule zero).
        if (!IsFinite(trunkLift)) cycle1NanCount++;
        if (!IsFinite(marginNow)) cycle1NanCount++;
        for (int paw = 0; paw < 4; paw++)
        {
            if (!IsFinite(stance.pawNormalImpulse[paw])) cycle1NanCount++;
            if (!IsFinite(soleX[paw])) pawSoleNanCount++;
        }
        if (!IsFinite(fnOverWeightNow)) cycle1NanCount++;
        if (!IsFinite(controllerDropNow)) cycle1NanCount++;
        if (!IsFinite(captureXiNow)) cycle1NanCount++;
        if (!IsFinite(captureXiMarginNow)) cycle1NanCount++;
        if (!IsFinite(captureXiHeightNow)) cycle1NanCount++;

        // A push up: an end of the dog lifts 12 cm off the lowest point the
        // heap reached — the front legs driving the chest, or the hind legs
        // bringing the pelvis up off its haunches. Tested on EVERY frame, not
        // only once the dog is up (the push starts from the floor), and per
        // end, because one end can stay pinned while the other travels:
        // dog_q1 held pelvisDrop at 0.32 while the chest rose 0.13, dog_gu4
        // held the chest at 0.07 with the pelvis 0.32 down. Re-armed only after
        // 12 cm of new travel, so a squirm counts once, not on every frame.
        // The paw gate is deliberately NOT part of the attempt test. Measured
        // on dog_gu9 (18.09, 13:05 build): the chest rose 0.202 m off its own
        // lowest point in 14.91-16.25 s, yet `pawsGrounded >= 1` made the
        // attempt count 0, because the paws leave the floor exactly when the
        // trunk rises (front paws grounded 0.001 of the 25 s down window,
        // rear 0.10-0.11). A planted paw is the DOWN rule's business
        // (paws <= 2); a 12 cm trunk lift off the deepest point of the heap is
        // the attempt, whoever is pushing.
        if (dogFallTime >= 0f && !dogGetUpSuccess
            && (pushFloorChestDrop - chestDropNow >= DOG_PUSH_LIFT
                || pushFloorPelvisDrop - pelvisDropNow >= DOG_PUSH_LIFT))
        {
            dogGetUpAttempts++;
            pushFloorChestDrop = chestDropNow;
            pushFloorPelvisDrop = pelvisDropNow;
        }

        // Back on the paws: three or four planted with the trunk at the stance
        // height, HELD — the walk cycle passes through that pose every stride.
        if (trunkDropNow <= DOG_UP_DROP && pawsGrounded >= 3)
        {
            upAge += Time.fixedDeltaTime;
            if (upAge >= DOG_UP_HOLD_SECONDS && dogFallTime >= 0f && !dogGetUpSuccess)
            {
                dogGetUpSuccess = true;
                // Seconds from the frame the dog went down to the frame it
                // held four paws again. -1 while it never does.
                dogGetUpSeconds = time - dogFallTime;
            }
        }
        else
        {
            upAge = 0f;
        }

        if (absOff > SETTLED_COM_OFFSET)
            lastUnsettledTime = time;

        lastState = stance.currentState.ToString();
        lastLeapCount = stance.leapCount;
        lastJawAngle = stance.jawAngle;
        if (stance.leapCount > maxLeapCount)
            maxLeapCount = stance.leapCount;
        if (stance.currentState == DogStanceController.StanceState.LeapCrouch) sawLeapCrouch = true;
        if (stance.currentState == DogStanceController.StanceState.LeapPush) sawLeapPush = true;
        if (stance.currentState == DogStanceController.StanceState.LeapAir) sawLeapAir = true;
        if (stance.currentState == DogStanceController.StanceState.LeapBite) sawLeapBite = true;

        if (stance.IsWalk() || stance.walkWanted)
        {
            if (!walkWindowOpen)
            {
                walkStartComX = stance.comPosition.x;
                walkStartTime = time;
                walkWindowOpen = true;
            }
            // CYCLE 95: once the first fall has closed the window, the end
            // stays frozen at the fall frame (this update no longer runs).
            if (!walkClosedByFall)
            {
                walkEndComX = stance.comPosition.x;
                walkEndTime = time;
            }
            samplesWalkActive++;
        }
        // ─── CYCLE 95 SENSOR: CLOSE THE WALK WINDOW AT THE FIRST FALL ───
        // The accepted letter (comTravelX / comSpeedX / comSpeedKmh /
        // walkWindowSeconds / metresPerCycle*) used to read to the LAST
        // walk-active frame, and that condition outlives the fall: the window
        // absorbed the post-fall drift, so the letter stopped reading the walk
        // (c93/c94: two KEEPs flipped on w04 by the drift alone). The window
        // now ENDS at the first fall's OWN time — dogFallTime, the onset frame
        // the criterion reports (survivedSeconds), NOT the frame it is declared
        // on 0.15 s later — so the letter reads the walked segment only. The
        // end com comes from the ring at the frame nearest that time. The close
        // is latched: walk-active frames after the fall no longer extend the
        // window. Guard: a fall whose onset precedes the window's open does not
        // close it (that fall is outside the segment). Read-only: no control
        // law reads any of this.
        walkRingTime[walkRingCursor] = time;
        walkRingComX[walkRingCursor] = stance.comPosition.x;
        walkRingCursor = walkRingCursor + 1 < WalkRingFrames ? walkRingCursor + 1 : 0;
        if (walkRingFilled < WalkRingFrames)
            walkRingFilled++;
        if (!walkClosedByFall && walkWindowOpen && dogFallTime >= 0f
            && dogFallTime >= walkStartTime)
        {
            walkClosedByFall = true;
            walkEndTime = dogFallTime;
            int best = -1;
            float bestAbs = float.MaxValue;
            int scan = walkRingFilled < WalkRingFrames ? walkRingFilled : WalkRingFrames;
            for (int k = 0; k < scan; k++)
            {
                int idx = walkRingCursor - 1 - k;
                if (idx < 0) idx += WalkRingFrames;
                float d = Mathf.Abs(walkRingTime[idx] - dogFallTime);
                if (d < bestAbs)
                {
                    bestAbs = d;
                    best = idx;
                }
            }
            if (best >= 0)
                walkEndComX = walkRingComX[best];
            // CYCLE 99: if the fall cut the window short of the span's
            // end, the letter's span ends where the window ends.
            if (!walkSpanEndSet || walkEndTime < walkSpanEndTime)
            {
                walkSpanEndSet = true;
                walkSpanEndTime = walkEndTime;
                walkSpanEndComX = walkEndComX;
            }
        }
        // ─── CYCLE 99 SENSOR: THE LETTER'S SPAN ───
        // The span's end is the first IN-WINDOW frame at/after
        // DOG_WALK_LETTER_SPAN (the frame at walkStartTime + 2.00 s); a
        // walk window that ends - or is cut by a fall - earlier leaves the
        // span unset and the summary falls back to the window's own end.
        // Read-only: no control law reads any of this.
        if (walkWindowOpen && !walkSpanEndSet && time <= walkEndTime + 1e-4f
            && time - walkStartTime >= DOG_WALK_LETTER_SPAN - 1e-4f)
        {
            walkSpanEndSet = true;
            walkSpanEndTime = time;
            walkSpanEndComX = stance.comPosition.x;
        }
        // ─── CYCLE 104 SENSOR: THE SUMS' WINDOW-CLOSED TWIN ───
        // Every frame from the window's open onward pushes this frame's own
        // c21 increment (t, then the four paws). The sums are taken in
        // BuildSummaryJson over the published ends, so the recorded twin is
        // exactly what an offline read of the CSV's own columns computes -
        // frame by frame, in this order. Read-only: no control law reads
        // any of this; no CSV column moves.
        if (walkWindowOpen && walkStartTime >= 0f)
        {
            slipStepRows.Add(time);
            slipStepRows.Add(soleStepSlip[0]);
            slipStepRows.Add(soleStepSlip[1]);
            slipStepRows.Add(soleStepSlip[2]);
            slipStepRows.Add(soleStepSlip[3]);
        }
        lastWalkCycleCount = stance.walkCycleCount;
        lastSenseTick = stance.senseTick;
        float comX = stance.comPosition.x;
        if (comX < sensorComXMin) sensorComXMin = comX;
        if (comX > sensorComXMax) sensorComXMax = comX;
        float margin = stance.supportMargin;
        if (margin < sensorMarginMin) sensorMarginMin = margin;
        if (margin > sensorMarginMax) sensorMarginMax = margin;
        if (walkWindowOpen && walkStartTime >= 0f && time - walkStartTime <= 3.001f)
        {
            samplesOnsetWindow++;
            if (stance.WalkCollapsed) samplesOnsetCollapsed++;
            if (stance.WalkPosed) samplesOnsetPosed++;
        }

        // ─── CYCLE 25 SENSOR: the rock, frame by frame ───
        // This recorder's own unwrap of the pitch trace (the same one-frame
        // wrap rule the controller uses: at 200 Hz no step can be a wrap) feeds
        // the per-run rock metrics below. The gate's live readings are read
        // from the controller's witnesses into the row's last three columns.
        if (float.IsNaN(rockPrevPitch)) rockPrevPitch = pitch;
        float rockStep = pitch - rockPrevPitch;
        if (rockStep > 180f) rockStep -= 360f;
        else if (rockStep < -180f) rockStep += 360f;
        rockPrevPitch = pitch;
        rockUnwrapped += rockStep;
        rockRate += (rockStep / Time.fixedDeltaTime - rockRate)
            * Mathf.Clamp01(Time.fixedDeltaTime / DOG_ROCK_TAU);
        if (Mathf.Abs(pitch) >= DOG_INVERTED_ABS_PITCH)
        {
            if (rockInvertedBorn < 0f) rockInvertedBorn = time;
            float rockDev = 180f - Mathf.Abs(rockUnwrapped);
            if (Mathf.Abs(rockDev) > rockMaxAbsDev) rockMaxAbsDev = Mathf.Abs(rockDev);
            bool rockTurned = false;
            if (rockDir == 0)
            {
                if (rockRate >= DOG_ROCK_DEADBAND) rockDir = 1;
                else if (rockRate <= -DOG_ROCK_DEADBAND) rockDir = -1;
            }
            else if (rockDir > 0)
            {
                if (rockRate <= -DOG_ROCK_DEADBAND) { rockDir = -1; rockTurned = true; }
            }
            else if (rockRate >= DOG_ROCK_DEADBAND) { rockDir = 1; rockTurned = true; }
            if (rockTurned)
            {
                // A reversal closes a rock: compare the apex it just closed
                // against the previous apex on the SAME side of the back — a
                // deeper swing is what "the rock GREW over successive rocks"
                // means on the card. rockGrowRunMax is the longest chain of
                // deeper same-side rocks in the run.
                rockRocks++;
                if (rockHaveApex && Mathf.Sign(rockDev) == Mathf.Sign(rockPrevApexDev))
                {
                    if (Mathf.Abs(rockDev) > Mathf.Abs(rockPrevApexDev))
                    {
                        rockGrowRun++;
                        if (rockGrowRun > rockGrowRunMax) rockGrowRunMax = rockGrowRun;
                    }
                    else rockGrowRun = 0;
                }
                rockPrevApexDev = rockDev;
                rockHaveApex = true;
            }
            // The crossing, on the card's own bar: |chestPitch| <= 100 held 20
            // frames, timed from the first inverted frame of the run.
            if (Mathf.Abs(pitch) <= DOG_INVERTED_ABS_PITCH) rockCrossStreak++;
            else rockCrossStreak = 0;
            if (rockCrossStreak >= DOG_CROSS_HOLD_FRAMES && rockCrossSeconds < 0f)
                rockCrossSeconds = time - rockInvertedBorn;
        }

        // ─── CYCLE 26 SENSOR: the catch at the line, frame by frame ───
        // The catch's own witnesses (the controller's), plus the card's crossing
        // measured HERE on every frame: the c25 streak above sits inside the
        // inverted branch, so it can never see |chestPitch| < 100 — the band the
        // catch exists for.
        if (stance.getUpCatchArmed != 0)
        {
            catchArmedSamples++;
            float clearNow = stance.getUpCatchClear;
            if (clearNow > 0f && (catchMinClear < 0f || clearNow < catchMinClear))
                catchMinClear = clearNow;
            if (stance.getUpCatchPad != 0)
            {
                catchPadSamples++;
                catchHoldSeconds += Time.fixedDeltaTime;
                if (!catchPadPrev)
                {
                    catchTouchCount++;
                    float touchDev = stance.getUpCatchDev;
                    if (touchDev < catchDeepestDev) catchDeepestDev = touchDev;
                    // CYCLE 27: the touch's own side and the sole/hip against the
                    // CoM — the numbers the back-plant's report is written from.
                    if (!catchSoleComSeeded)
                    {
                        catchSoleComSeeded = true;
                        catchSoleComFirst = stance.getUpCatchSoleCom;
                        catchHipComFirst = stance.getUpCatchHipCom;
                        catchSideFirst = CatchSideName(stance.getUpCatchPaw);
                    }
                }
                // CYCLE 27: the deepest back-plant the band reached: the sole's
                // signed distance to the CoM (- = behind it, the side the cycle
                // needs). Read every armed frame, contact or not.
                float soleComNow = stance.getUpCatchSoleCom;
                if (soleComNow < catchSoleComMin) catchSoleComMin = soleComNow;
            }
            if (stance.getUpCatchPush != 0)
            {
                catchPushSamples++;
                catchPushSeconds += Time.fixedDeltaTime;
            }
            // ─── CYCLE 28: the rump corner, every armed frame ───
            // The hold's own witness: the pelvis's rear corner against the slab
            // and the CoM. Read while the band is armed and only then (the
            // columns read 0 everywhere else, the c27 pattern).
            if (!rumpSeeded)
            {
                rumpSeeded = true;
                rumpClearFirst = stance.getUpRumpClear;
                rumpComFirst = stance.getUpRumpCom;
            }
            if (stance.getUpRumpClear < rumpClearMin) rumpClearMin = stance.getUpRumpClear;
            if (stance.getUpRumpLowClear < rumpLowMin) rumpLowMin = stance.getUpRumpLowClear;
        }
        // ─── CYCLE 36: the front pads' own clearance, every get-up frame ───
        // The reach's own geometry, tracked where the reach is evaluated (the
        // GettingUp state — the belly side of the line, where the catch's own
        // armed block above has already let go).
        if (stance.currentState == DogStanceController.StanceState.GettingUp)
        {
            float frontClearNow = stance.getUpFrontClear;
            if (!frontClearSeeded)
            {
                frontClearSeeded = true;
                frontClearFirst = frontClearNow;
            }
            if (frontClearNow < frontClearMin) frontClearMin = frontClearNow;
        }
        // ─── CYCLE 31 SENSOR: the claws, frame by frame ───
        // The grip's own numbers, read from the controller's claw witnesses (all
        // of them zero while no grip is held — the columns read 0 there too).
        // The frames a grip is HELD are the frames getUpClawPaw names a side.
        if (stance.getUpClawPaw != -1)
        {
            clawEngageFrames++;
            if (clawEngageFirstFrame < 0)
            {
                clawEngageFirstFrame = samples;
                clawEngageFirstTime = time;
            }
            clawSideMask |= stance.getUpClawPaw == 3
                ? 3 : (stance.getUpClawPaw == 0 ? 1 : 2);
            if (stance.getUpClawSlip > clawSlipMax) clawSlipMax = stance.getUpClawSlip;
            float clawTfNow = stance.getUpClawTf;
            if (clawTfNow > clawTfMax)
            {
                clawTfMax = clawTfNow;
                clawCapAtTfMax = stance.getUpClawCap;
            }
            if (stance.getUpClawFrac > clawFracMax) clawFracMax = stance.getUpClawFrac;
            if (stance.getUpClawFrac >= 0.98f) clawAtCapFrames++;
            float clawMomentNow = Mathf.Abs(stance.getUpClawMoment);
            if (clawMomentNow > clawMomentMax) clawMomentMax = clawMomentNow;
            float clawFnNow = stance.getUpClawFn;
            if (clawFnNow > 1f)
            {
                // The measured coefficient the gripped contacts reached: a lower
                // bound on the pair the solver enforced (it saturates at the
                // capacity only while the contact slides).
                float clawMuNow = clawTfNow / clawFnNow;
                if (clawMuNow > clawMuMeasured) clawMuMeasured = clawMuNow;
            }
            clawMuPair = stance.getUpClawMuPair;
        }
        if (stance.getUpClawRipout != 0)
        {
            if (clawRipouts == 0) clawRipoutFirstFrame = samples;
            clawRipouts++;
        }
        // ─── CYCLE 32 SENSOR: the fold, frame by frame ───
        // The fold's own clock and command, read from the controller's fold
        // witnesses (all zero while the fold is not running — the columns read 0
        // there too). The lumbar's own angle is the pre-existing `lumbar` column;
        // the fold's excursion past the brick's stop is the controller's own
        // count (it owns the hinge's angle), re-published here.
        float foldWNow = stance.getUpFoldWeight;
        if (foldWNow > 0f)
        {
            foldFrames++;
            if (foldFirstFrame < 0)
            {
                foldFirstFrame = samples;
                foldFirstTime = time;
            }
            if (foldWNow > foldWeightMax) foldWeightMax = foldWNow;
            if (lumbar < foldLumbarMin) foldLumbarMin = lumbar;
            if (lumbar > foldLumbarMax)
            {
                foldLumbarMax = lumbar;
                foldLumbarMaxFrame = samples;
                foldLumbarMaxTime = time;
            }
            float neckNow = stance.neckAngle;
            if (neckNow < foldNeckMin) foldNeckMin = neckNow;
            if (neckNow > foldNeckMax) foldNeckMax = neckNow;
            // The curl against the PLANT: the deepest flexion while a hind pad
            // is on the slab (catchPaw names the pad, the same column the row
            // already carries) — the fold's own timing versus the plant.
            if (stance.getUpCatchPaw != -1 && lumbar > foldLumbarAtPlantMax)
            {
                foldLumbarAtPlantMax = lumbar;
                foldLumbarAtPlantFrame = samples;
            }
        }
        if (stance.getUpFoldRangeOpen != 0) foldOpenFrames++;
        foldPastStopFrames = stance.getUpFoldPastStopFrames;
        foldPastStopMax = stance.getUpFoldPastStopMaxDeg;
        catchPadPrev = stance.getUpCatchPad != 0;
        // The card's crossing bar, measured only AFTER the run has been inverted
        // (before the onset a dog that starts standing would "cross" at f0).
        if (rockInvertedBorn >= 0f)
        {
            if (Mathf.Abs(pitch) <= DOG_INVERTED_ABS_PITCH) catchCrossStreak++;
            else catchCrossStreak = 0;
            if (catchCrossStreak >= DOG_CROSS_HOLD_FRAMES && catchCrossSeconds < 0f)
            {
                catchCrossSeconds = time - rockInvertedBorn;
                if (catchTouchCount > 0) catchCrossed = true;
            }
        }

        AppendCsvFloat(time);
        rows.Append(',');
        AppendCsvFloat(pitch);
        rows.Append(',');
        AppendCsvFloat(pTilt);
        rows.Append(',');
        AppendCsvFloat(lumbar);
        rows.Append(',');
        AppendCsvFloat(chestY);
        rows.Append(',');
        AppendCsvFloat(pelvisY);
        rows.Append(',');
        AppendCsvFloat(stance.comPosition.x);
        rows.Append(',');
        AppendCsvFloat(stance.comPosition.y);
        rows.Append(',');
        AppendCsvFloat(comOff);
        rows.Append(',').Append(stance.frontRightGrounded ? '1' : '0');
        rows.Append(',').Append(stance.frontLeftGrounded ? '1' : '0');
        rows.Append(',').Append(stance.rearRightGrounded ? '1' : '0');
        rows.Append(',').Append(stance.rearLeftGrounded ? '1' : '0');
        rows.Append(',');
        AppendCsvFloat(maxActivation);
        rows.Append(',').Append((int)stance.currentState);
        rows.Append(',');
        AppendCsvFloat(stance.leapCount);
        rows.Append(',');
        AppendCsvFloat(stance.jawAngle);
        rows.Append(',').Append(pawsGrounded.ToString(CultureInfo.InvariantCulture));
        rows.Append(',').Append(dogDownNow ? '1' : '0');
        // down = on the ground this frame, fell = latched at the first down
        // frame (dogFallTime), so the frame the flag turns true is in the CSV.
        // Appended LAST: every reader indexes columns by name (DictReader).
        rows.Append(',').Append(dogFallTime >= 0f ? '1' : '0');
        rows.Append(',').Append(stance.getUpSuccess ? '1' : '0');
        // ─── GET-UP WITNESS (append-only, indices stable) ───
        // The dog CSV carried no limb angles, so a get-up frozen at the base
        // pose and a tuck that moved the joints could not be told apart.
        // trunkBar = the controller's own heap bar (trunk below the measured
        // stand pose, m); push = 1 while it commands the push half of the
        // tuck-push cycle. Read-only, nothing here feeds the control math.
        for (int i = 0; i < GetUpLimbColumns; i++)
        {
            rows.Append(',');
            AppendCsvFloat(stance.GetUpLimbAngle(i));
        }
        rows.Append(',');
        AppendCsvFloat(trunkBarNow);
        rows.Append(',').Append(stance.getUpPushingNow ? '1' : '0');
        // ─── CYCLE 1 INSTRUMENTS (append-only, after `push`, indices stable) ───
        // trunkLift m — trunk lift off the frame the fall was declared;
        // capturePointMargin m — support margin, + inside the paw AABB;
        // pawNormalImpulseFR/FL/RR/RL N*s — this step's normal impulse per paw;
        // sumFnOverWeight dimensionless — sum(Fn)/(m*g), 1 = the paws carry the
        // body weight; controllerTrunkDrop m — the controller's own
        // WalkStandDrop, beside this recorder's independent trunkDrop.
        rows.Append(',');
        AppendCsvFloat(trunkLift);
        rows.Append(',');
        AppendCsvFloat(marginNow);
        for (int col = 0; col < 4; col++)
        {
            rows.Append(',');
            AppendCsvFloat(stance.pawNormalImpulse[col]);
        }
        rows.Append(',');
        AppendCsvFloat(fnOverWeightNow);
        rows.Append(',');
        AppendCsvFloat(controllerDropNow);
        // ─── CYCLE 3 SENSOR (append-only, after controllerTrunkDrop) ───
        // captureXi m — the extrapolated CoM x (x_com + v_x/omega);
        // captureHeight m — its height over the slab top, the h inside omega;
        // captureMargin m — its signed margin against the support interval,
        // + inside. The column `capturePointMargin` above (39th) is the CoM
        // PROJECTION against the same interval; the two differ by the momentum
        // term only.
        rows.Append(',');
        AppendCsvFloat(captureXiNow);
        rows.Append(',');
        AppendCsvFloat(captureXiHeightNow);
        rows.Append(',');
        AppendCsvFloat(captureXiMarginNow);
        // ─── CYCLE 21 SENSOR (append-only, after captureMargin) ───
        // pawSoleXFR/FL/RR/RL m — the world X of each paw's sole point,
        // paw.transform.TransformPoint(paw.anchor) — the point the walk law
        // reads at DogStanceController.LatchWalkLiftPoint (:3172). A grounded
        // paw's sole X may not move while the slab holds, so the sum of
        // |delta pawSoleX| over a grounded run IS that run's slip, in metres:
        // the card's field. Per-paw run and walk-window totals are in the JSON
        // (pawSoleSlip* keys). Paw order FR, FL, RR, RL.
        for (int paw = 0; paw < 4; paw++)
        {
            rows.Append(',');
            AppendCsvFloat(soleX[paw]);
        }
        // ─── CYCLE 25 SENSOR (append-only, after pawSoleXRL) ───
        // rockRate deg/s — the gate's own smoothed, unwrapped trunk pitch rate
        // (sign = the way the trunk is rocking); rockDir -1/0/+1 — the
        // direction it latched (+1 = the trunk rocks toward the increasing
        // unwrapped pitch); rockPair 1 = the FRONT pair is the driving pair
        // this frame, 0 = the rear pair. All three are the controller's own
        // witnesses (getUpRockRate / getUpRockDir / getUpInvertedFrontHalf);
        // they read 0/0/0 while no inverted heap drives them.
        rows.Append(',');
        AppendCsvFloat(stance.getUpRockRate);
        rows.Append(',').Append(stance.getUpRockDir.ToString(CultureInfo.InvariantCulture));
        rows.Append(',').Append(stance.getUpInvertedFrontHalf ? '1' : '0');
        // ─── CYCLE 26 SENSOR (append-only, after rockPair) ───
        // catchArmed 1 = the catch's band owns this frame (a heap with
        // |chestPitch| in [line - 30, line)); catchPad 1 = a hind pad is on the
        // floor in that band; catchPaw 0 = RR, 1 = RL, 3 = both, -1 = none;
        // catchDevDeg = the controller's dev at the touch (|chestPitch| - 180;
        // 0 = no touch yet in this episode — the band's own devs are <= -80);
        // catchPush 1 = the extension is commanded (the pad holds, the servo's
        // sole target is the press); rearClearM = the lower hind pad's measured
        // clearance over the slab top, m (the servo's input, every get-up frame).
        rows.Append(',').Append(stance.getUpCatchArmed.ToString(CultureInfo.InvariantCulture));
        rows.Append(',').Append(stance.getUpCatchPad.ToString(CultureInfo.InvariantCulture));
        rows.Append(',').Append(stance.getUpCatchPaw.ToString(CultureInfo.InvariantCulture));
        rows.Append(',');
        AppendCsvFloat(stance.getUpCatchDev);
        rows.Append(',').Append(stance.getUpCatchPush.ToString(CultureInfo.InvariantCulture));
        rows.Append(',');
        AppendCsvFloat(stance.getUpCatchClear);
        // ─── CYCLE 27 SENSOR (append-only, after rearClearM) ───
        // The back-plant's own numbers, signed against the CoM, m:
        // catchSoleComM = the lower hind pad's sole point minus comX, + = the pad
        // sits FORWARD of the CoM (the side c26's touch landed on — the wall this
        // cycle was ordered to remove); catchHipComM = that leg's hip pivot minus
        // comX, the base the reach's whole lever hangs off. Both read 0 while the
        // catch's band is not armed.
        rows.Append(',');
        AppendCsvFloat(stance.getUpCatchSoleCom);
        rows.Append(',');
        AppendCsvFloat(stance.getUpCatchHipCom);
        // ─── CYCLE 28 SENSOR (append-only, after catchHipComM) ───
        // The rump corner, the pelvis's own rear corner: rumpClearM = its height
        // over the slab, rumpComM = its x minus comX (+ = forward of the CoM) —
        // the corner the hold presses; rumpLowClearM / rumpLowComM = the same two
        // numbers for the LOWEST of the pelvis's four corners, the one that can
        // actually hold the slab (the contact's own proof). All four read 0 while
        // the catch's band is not armed.
        rows.Append(',');
        AppendCsvFloat(stance.getUpRumpClear);
        rows.Append(',');
        AppendCsvFloat(stance.getUpRumpCom);
        rows.Append(',');
        AppendCsvFloat(stance.getUpRumpLowClear);
        rows.Append(',');
        AppendCsvFloat(stance.getUpRumpLowCom);
        // ─── CYCLE 31 SENSOR (append-only, after rumpLowComM) ───
        // The claws' own witness, every frame — all of it reads 0 while no grip
        // is held:
        // clawGripR / clawGripL — each hind pad's engage signal, dimensionless
        //   0..1: its measured normal load Fn/(m*g) against the engage
        //   threshold (0.25 body weights, -dogClawEngage), so 0 = the pad is in
        //   the air and 1 = it presses at or over the bar;
        // clawPaw — the GRIPPED pad: -1 none, 0 RR, 1 RL, 3 both (catchPaw's
        //   encoding);
        // clawSlipM — the gripped pad's anchored slip, m: its own sole point
        //   against the point the grip was set on (kept on a tear's frame);
        // clawFnN / clawTfN / clawCapN — the gripped contacts' normal force,
        //   tangential load and capacity, N (capacity = mu_pair * Fn; mu_pair
        //   is the engine's own combine of the pad's claw material and the
        //   slab's, published as clawMuPair in the JSON);
        // clawTfFrac — Tf/Cap, dimensionless (1 = the solver's own sliding
        //   limit — the most this grip can hold);
        // clawMomentNm — the gripped contacts' moment about the CoM, N*m
        //   (z = r x F, the engine's own contact normal/tangent and impulses);
        // clawRipout — 1 on the step a grip was TORN OUT (a slip release while
        //   the pad was still loaded), else 0.
        rows.Append(',');
        AppendCsvFloat(stance.getUpClawGripR);
        rows.Append(',');
        AppendCsvFloat(stance.getUpClawGripL);
        rows.Append(',').Append(stance.getUpClawPaw.ToString(CultureInfo.InvariantCulture));
        rows.Append(',');
        AppendCsvFloat(stance.getUpClawSlip);
        rows.Append(',');
        AppendCsvFloat(stance.getUpClawFn);
        rows.Append(',');
        AppendCsvFloat(stance.getUpClawTf);
        rows.Append(',');
        AppendCsvFloat(stance.getUpClawCap);
        rows.Append(',');
        AppendCsvFloat(stance.getUpClawFrac);
        rows.Append(',');
        AppendCsvFloat(stance.getUpClawMoment);
        rows.Append(',').Append(stance.getUpClawRipout.ToString(CultureInfo.InvariantCulture));
        // ─── CYCLE 32 SENSOR (append-only, after clawRipout) ───
        // The fold's own witness, every frame — all of it reads 0 while the
        // fold is not running (before the band's arm at GetUpCatchArmDeg):
        // foldW — the fold's own weight, dimensionless 0..1 (the ramp from the
        //   band's own arm; the signal the fold's whole block reads);
        // foldDeg — the commanded lumbar FLEXION in force, deg of the hinge's own
        //   jointAngle (0 while the accepted world law drives; +34 at full);
        // foldNeckDeg — the commanded neck flexion in force, deg (0..+34);
        // neckJ — the neck's own joint angle, deg (the follow: it reads the
        //   neck's hinge whether or not the fold is running);
        // foldLimDeg — the lumbar's ACTIVE flexion limit, deg (20 = the accepted
        //   range, 40 = the fold's range open — Dog.LumbarFlexionMaxDeg).
        rows.Append(',');
        AppendCsvFloat(stance.getUpFoldWeight);
        rows.Append(',');
        AppendCsvFloat(stance.getUpFoldDeg);
        rows.Append(',');
        AppendCsvFloat(stance.getUpFoldNeckDeg);
        rows.Append(',');
        AppendCsvFloat(stance.neckAngle);
        rows.Append(',');
        AppendCsvFloat(stance.getUpFoldFlexLimitDeg);
        // ─── CYCLE 34 SENSOR (append-only, after foldLimDeg) ───
        // The hand-over's own witness, every frame (both read 0 while the trunk is
        // not on the belly side of an episode that has been on its back):
        // handOver — the hand-over's own weight, dimensionless 0..1: the belly
        //   get-up's own trigger, live only while the plant's own hold is gone (no
        //   hind pad grounded, no claw gripping), ramped by the crossing's depth
        //   (0 at the card's line, 1 at the band's lower edge);
        // pike — 1 while the tuck half of the belly tuck-push cycle is commanded
        //   (the counterpart of `push`), so the belly get-up's own engagement is
        //   readable frame by frame instead of inferred from the joint angles.
        rows.Append(',');
        AppendCsvFloat(stance.getUpHandOver);
        rows.Append(',').Append(stance.getUpPikeNow ? '1' : '0');
        // ─── CYCLE 36 SENSOR (append-only, after pike) ───
        // frontClearM — the LOWEST front pad's own collider clearance over the
        // slab, m, read every get-up frame off the two front pads (the mirror of
        // rearClearM for the hind pair, which the front limbs never had). 0 while
        // the get-up is not running (the c28 pattern: the column reads 0 outside
        // its own phase). It is the geometry the front reach is judged by: the
        // reach is a position solve, so this column says whether the slab was ever
        // inside the limb's own 0.34 m — nothing in the control math reads it.
        rows.Append(',');
        AppendCsvFloat(stance.currentState == DogStanceController.StanceState.GettingUp
            ? stance.getUpFrontClear : 0f);
        // ─── CYCLE 37 SENSOR (append-only, after frontClearM) ───
        // climbCommit — the weight the pump's own stroke depth carries on the
        // committed half, 0..1 (cycle 37): the half that rotates the trunk toward
        // the tipping line (a negative rock rate) strokes GetUpClimbCommitGain
        // deeper, the half that brings it back keeps the accepted depth. 0 while
        // the get-up is not running and 0 while the rock comes back, so the column
        // names WHEN the deeper stroke was commanded — the attribute the climb's
        // before/after table is read with, frame by frame.
        rows.Append(',');
        AppendCsvFloat(stance.currentState == DogStanceController.StanceState.GettingUp
            ? stance.getUpClimbCommit : 0f);
        // ─── CYCLE 91 SENSOR (append-only, after climbCommit) ───
        // pawClearFR/FL/RR/RL m — each paw's own pad face over the slab (the
        // rearClearM / frontClearM measure, read in every phase); pawDropFR/FL/
        // RR/RL m/s — the rate that clearance closes, positive while the pad
        // descends. See the sensor block in Sample(): no control law reads
        // either family.
        for (int paw = 0; paw < 4; paw++)
        {
            rows.Append(',');
            AppendCsvFloat(pawClear[paw]);
        }
        for (int paw = 0; paw < 4; paw++)
        {
            rows.Append(',');
            AppendCsvFloat(pawDrop[paw]);
        }
        rows.AppendLine();

        samples++;
    }

    public string BuildCsv()
    {
        return rows.ToString();
    }

    public string BuildSummaryJson(float simulatedSeconds, float wallSeconds = -1f)
    {
        float trunkDrop = Mathf.Max(startChestY - minChestY, startPelvisY - minPelvisY);
        float rmsCom = samples > 0 ? Mathf.Sqrt((float)(sumComOffsetSquared / samples)) : 0f;
        // Physical down latch, not the DogStanceController automaton: the dog
        // rolls onto its side at 14.8 s in 21 of the 30 dog runs on disk while
        // the stance machine stays in Walk (state 4) and never enters
        // Falling/Fallen, so `fell` read false on a body that lay 25 s of 40.
        // Same defect and same fix as the human (TrialRecorder.physicalFallTime);
        // the automaton's own latch is still published as fallAutomatonTime.
        float survived = dogFallTime >= 0f ? dogFallTime : simulatedSeconds;
        float recovery = 0f;
        if (pushTime >= 0f && lastUnsettledTime >= pushTime)
            recovery = lastUnsettledTime - pushTime;

        StringBuilder json = new StringBuilder();
        json.Append("{");
        AppendStr(json, "subject", "dog", false);
        AppendStr(json, "label", info != null ? info.Label : "trial");
        AppendNum(json, "simulatedSeconds", simulatedSeconds);
        if (wallSeconds >= 0f)
        {
            AppendNum(json, "wallSeconds", wallSeconds);
            if (simulatedSeconds > 0f)
                AppendNum(json, "wallPerSim", wallSeconds / simulatedSeconds);
        }
        json.Append(",\"samples\":").Append(samples.ToString(CultureInfo.InvariantCulture));
        json.Append(",\"executedSteps\":").Append(executedSteps.ToString(CultureInfo.InvariantCulture));
        AppendNum(json, "fixedDeltaTime", Time.fixedDeltaTime);
        AppendNum(json, "survivedSeconds", survived);
        json.Append(",\"fell\":").Append(dogFallTime >= 0f ? "true" : "false");
        AppendNum(json, "fallAutomatonTime", fallTime);
        AppendNum(json, "maxAbsChestPitch", maxAbsChestPitch);
        AppendNum(json, "maxAbsPelvisTilt", maxAbsPelvisTilt);
        AppendNum(json, "maxAbsLumbarAngle", maxAbsLumbarAngle);
        AppendNum(json, "trunkDrop", trunkDrop);
        AppendNum(json, "chestDrop", startChestY - minChestY);
        AppendNum(json, "pelvisDrop", startPelvisY - minPelvisY);
        AppendNum(json, "startChestY", startChestY);
        AppendNum(json, "minChestY", minChestY);
        AppendNum(json, "finalChestY", lastChestY);
        AppendNum(json, "finalChestPitch", lastChestPitch);
        AppendNum(json, "finalPelvisTilt", lastPelvisTilt);
        AppendNum(json, "finalLumbarAngle", lastLumbar);
        AppendNum(json, "rmsComOffset", rmsCom);
        AppendNum(json, "maxAbsComOffset", maxAbsComOffset);
        AppendNum(json, "finalComOffset", lastComOffset);
        AppendNum(json, "fourFeetGroundedFraction", samples > 0 ? samplesFourFeet / (float)samples : 0f);
        AppendNum(json, "muscleSaturationFraction", samples > 0 ? samplesSaturated / (float)samples : 0f);
        AppendNum(json, "recoverySeconds", recovery);
        AppendStr(json, "currentState", lastState);
        AppendNum(json, "leapCount", lastLeapCount);
        AppendNum(json, "maxLeapCount", maxLeapCount);
        AppendNum(json, "jawAngle", lastJawAngle);
        json.Append(",\"sawLeapCrouch\":").Append(sawLeapCrouch ? "true" : "false");
        json.Append(",\"sawLeapPush\":").Append(sawLeapPush ? "true" : "false");
        json.Append(",\"sawLeapAir\":").Append(sawLeapAir ? "true" : "false");
        json.Append(",\"sawLeapBite\":").Append(sawLeapBite ? "true" : "false");
        // CYCLE 95: this window closes at the first fall (the close block in
        // the sample update); it is the walked segment, not the walk-active
        // run to the sim end. walkWindowClosedByFall says whether a fall cut it.
        float walkWindow = walkWindowOpen && walkEndTime > walkStartTime
            ? walkEndTime - walkStartTime
            : 0f;
        float travelX = walkWindowOpen ? walkEndComX - walkStartComX : 0f;
        float speedX = walkWindow > 0.01f ? travelX / walkWindow : 0f;
        AppendNum(json, "comTravelX", travelX);
        AppendNum(json, "comSpeedX", speedX);
        AppendNum(json, "comSpeedKmh", speedX * 3.6f);
        AppendNum(json, "walkWindowSeconds", walkWindow);
        json.Append(",\"walkWindowClosedByFall\":").Append(walkClosedByFall ? "true" : "false");
        // CYCLE 99: THE LETTER'S SPAN (the c98 section 8 item 1 fix). The
        // letter's own construction: the same first-2.00-s span for every
        // row and every dose, clamped at the window's end. comTravelX /
        // comSpeedX above stay untouched (the whole-window record); these
        // are the like-for-like pace keys. walkStartTime / walkEndTime are
        // published so every window key re-derives from the CSV alone.
        float letterSpanEnd = walkSpanEndSet ? walkSpanEndTime : walkEndTime;
        float letterSpanEndCom = walkSpanEndSet ? walkSpanEndComX : walkEndComX;
        float letterSpan = walkWindowOpen && walkStartTime >= 0f && letterSpanEnd > walkStartTime
            ? letterSpanEnd - walkStartTime
            : 0f;
        float letterTravel = letterSpan > 0f ? letterSpanEndCom - walkStartComX : 0f;
        float letterSpeed = letterSpan > 0.01f ? letterTravel / letterSpan : 0f;
        AppendNum(json, "comTravelLetter", letterTravel);
        AppendNum(json, "comSpeedLetter", letterSpeed);
        AppendNum(json, "walkLetterSpanSeconds", letterSpan);
        json.Append(",\"walkLetterSpanFull\":").Append(letterSpan >= DOG_WALK_LETTER_SPAN - 1e-3f ? "true" : "false");
        AppendNum(json, "walkStartTime", walkWindowOpen && walkStartTime >= 0f ? walkStartTime : -1f);
        AppendNum(json, "walkEndTime", walkWindowOpen && walkEndTime >= 0f ? walkEndTime : -1f);
        json.Append(",\"walkCycleCount\":").Append(lastWalkCycleCount.ToString(CultureInfo.InvariantCulture));
        AppendNum(json, "walkActiveFraction", samples > 0 ? samplesWalkActive / (float)samples : 0f);
        json.Append(",\"senseTick\":").Append(lastSenseTick.ToString(CultureInfo.InvariantCulture));
        AppendNum(json, "sensorComXMin",
            samples > 0 && sensorComXMin < float.MaxValue * 0.5f ? sensorComXMin : 0f);
        AppendNum(json, "sensorComXMax",
            samples > 0 && sensorComXMax > float.MinValue * 0.5f ? sensorComXMax : 0f);
        AppendNum(json, "sensorSupportMarginMin",
            samples > 0 && sensorMarginMin < float.MaxValue * 0.5f ? sensorMarginMin : 0f);
        AppendNum(json, "sensorSupportMarginMax",
            samples > 0 && sensorMarginMax > float.MinValue * 0.5f ? sensorMarginMax : 0f);
        AppendNum(json, "walkOnsetSeconds", samplesOnsetWindow * Time.fixedDeltaTime);
        AppendNum(json, "walkOnsetCollapsedFraction",
            samplesOnsetWindow > 0 ? samplesOnsetCollapsed / (float)samplesOnsetWindow : 0f);
        AppendNum(json, "walkOnsetPosedFraction",
            samplesOnsetWindow > 0 ? samplesOnsetPosed / (float)samplesOnsetWindow : 0f);
        json.Append(",\"walkOnsetCollapsed\":")
            .Append(samplesOnsetCollapsed > 0 ? "true" : "false");
        json.Append(",\"walkOnsetPosed\":")
            .Append(samplesOnsetPosed > 0 ? "true" : "false");
        AppendStr(json, "walkOnsetLog", FormatWalkOnsetLog());
        json.Append(",\"getUpAttempts\":").Append(stance.getUpAttempts.ToString(CultureInfo.InvariantCulture));
        json.Append(",\"getUpSuccess\":").Append(stance.getUpSuccess ? "true" : "false");
        AppendNum(json, "getUpSeconds", stance.getUpSeconds);
        AppendNum(json, "getUpFallTime", stance.getUpFallTime);
        // Witness of the automaton's claim: the trunk drop and the paw count at
        // the frame getUpSuccess first turned true, measured from this
        // recorder's start-of-run stance. A physical rise reads <= 0.12 m with
        // >= 3 paws; the old state-name flag read 0.325 m with 0 paws.
        AppendNum(json, "getUpSuccessTime", automatonUpTime);
        AppendNum(json, "getUpSuccessTrunkDrop", sawAutomatonUp ? automatonUpTrunkDrop : -1f);
        json.Append(",\"getUpSuccessPaws\":").Append(automatonUpPaws.ToString(CultureInfo.InvariantCulture));
        // The automaton's own standing bar (highest provable stance pose) —
        // published so a reader can check the flag against the value it used.
        json.Append(",\"getUpStandTrunkValid\":").Append(stance.getUpStandTrunkValid ? "true" : "false");
        AppendNum(json, "getUpStandChestY", stance.getUpStandChestY);
        AppendNum(json, "getUpStandPelvisY", stance.getUpStandPelvisY);
        // Witness of the tuck-push cycle: frames with the trunk on the heap bar
        // and frames with the push commanded (0 = the dog lay in one pose).
        AppendNum(json, "getUpTrunkBarFinal", lastTrunkBar);
        AppendNum(json, "getUpTrunkBarMax", maxTrunkBar);
        json.Append(",\"getUpHeapSamples\":").Append(samplesHeap.ToString(CultureInfo.InvariantCulture));
        json.Append(",\"getUpPushSamples\":").Append(samplesPush.ToString(CultureInfo.InvariantCulture));
        // Physical get-up measurement, beside the automaton's own counters:
        // the stance machine can call GettingUp while the dog lies flat.
        json.Append(",\"dogGetUpAttempts\":").Append(dogGetUpAttempts.ToString(CultureInfo.InvariantCulture));
        json.Append(",\"dogGetUpSuccess\":").Append(dogGetUpSuccess ? "true" : "false");
        AppendNum(json, "dogGetUpSeconds", dogGetUpSeconds);
        AppendNum(json, "dogDownSeconds", samplesDown * Time.fixedDeltaTime);
        AppendNum(json, "dogDownFraction", samples > 0 ? samplesDown / (float)samples : 0f);
        AppendNum(json, "dogFallTime", dogFallTime);
        json.Append(",\"jawStrikeHits\":").Append((jawStrike != null ? jawStrike.HitCount : 0).ToString(CultureInfo.InvariantCulture));
        json.Append(",\"preyHurtCount\":").Append((preyLife != null ? preyLife.HurtCount : 0).ToString(CultureInfo.InvariantCulture));
        json.Append(",\"preyPresent\":").Append(preyLife != null ? "true" : "false");
        if (info != null)
        {
            AppendNum(json, "duration", info.Duration);
            AppendNum(json, "startY", info.StartY);
            AppendNum(json, "pushImpulse", info.RequestedPushImpulse);
            AppendNum(json, "pushTime", info.RequestedPushTime);
            AppendStr(json, "sourceFingerprint", info.SourceFingerprint);
        }
        if (pushTime >= 0f)
        {
            AppendNum(json, "appliedPushTime", pushTime);
            AppendNum(json, "appliedPushImpulse", pushImpulse);
        }
        // ─── CYCLE 1 INSTRUMENTS (read-only; no decision is taken on them) ───
        // Units: trunkLift m; capturePointMargin m (+ inside the paw AABB);
        // pawNormalImpulseMeanFR/FL/RR/RL N*s — the per-paw mean of the step's
        // solver normal impulse, the reading the human lane publishes as fzL/fzR
        // (force = impulse / dt); sumFnOverWeightMean/Max dimensionless, where
        // 1.0 means the paws carry the whole body weight; controllerTrunkDrop m —
        // the controller's OWN WalkStandDrop bar, logged beside this recorder's
        // independent trunkDrop, with the frame of the largest disagreement;
        // metresPerCycle and nominalStrideFront/Rear m — the measured travel per
        // counted walk cycle against the stride the rig link geometry allows
        // (2 * L * sin(sweep/2), L = upper + lower + half a paw, sweep =
        // walkStanceSweepDeg); cycle1NanCount counts non-finite new-column values.
        AppendNum(json, "trunkLiftMax", trunkLiftMax);
        AppendNum(json, "trunkLiftFinal", trunkLift);
        json.Append(",\"trunkLiftMaxFrame\":").Append(trunkLiftMaxFrame.ToString(CultureInfo.InvariantCulture));
        AppendNum(json, "capturePointMarginMin", captureMargins.Count > 0 ? captureMarginMin : 0f);
        AppendNum(json, "capturePointMarginP05", CaptureMarginP05());
        AppendNum(json, "capturePointMarginMean", samples > 0 ? (float)(sumCaptureMargin / samples) : 0f);
        json.Append(",\"capturePointMarginMinFrame\":").Append(captureMarginMinFrame.ToString(CultureInfo.InvariantCulture));
        json.Append(",\"capturePointMarginNegativeFrames\":").Append(captureMarginNegativeFrames.ToString(CultureInfo.InvariantCulture));
        AppendNum(json, "pawNormalImpulseMeanFR", MeanPawImpulse(0));
        AppendNum(json, "pawNormalImpulseMeanFL", MeanPawImpulse(1));
        AppendNum(json, "pawNormalImpulseMeanRR", MeanPawImpulse(2));
        AppendNum(json, "pawNormalImpulseMeanRL", MeanPawImpulse(3));
        AppendNum(json, "sumFnOverWeightMean", samples > 0 ? (float)(sumFnOverWeight / samples) : 0f);
        AppendNum(json, "sumFnOverWeightMax", sumFnOverWeightMax);
        float cycle1MetresPerCycle = lastWalkCycleCount > 0
            ? travelX / lastWalkCycleCount
            : 0f;
        float nominalFront;
        float nominalRear;
        NominalStrides(out nominalFront, out nominalRear);
        AppendNum(json, "metresPerCycle", cycle1MetresPerCycle);
        AppendNum(json, "nominalStrideFront", nominalFront);
        AppendNum(json, "nominalStrideRear", nominalRear);
        float cycle1NominalWidest = Mathf.Max(nominalFront, nominalRear);
        AppendNum(json, "metresPerCycleOverNominal",
            cycle1NominalWidest > 0.01f ? cycle1MetresPerCycle / cycle1NominalWidest : 0f);
        AppendNum(json, "controllerTrunkDropMax", controllerTrunkDropMax);
        AppendNum(json, "controllerTrunkDropFinal", controllerTrunkDropFinal);
        AppendNum(json, "controllerTrunkDropVsRecorderMax", controllerVsRecorderMax);
        json.Append(",\"controllerTrunkDropVsRecorderFrame\":").Append(controllerVsRecorderMaxFrame.ToString(CultureInfo.InvariantCulture));
        // ─── CYCLE 3 SENSOR (read-only; no decision is taken on it) ───
        // captureMargin* : the extrapolated CoM (xi = x_com + v_x/omega) against
        // the same support interval the projection column uses, m, + inside.
        // captureMarginLeadSeconds = the projection's first out-of-support time
        // minus the extrapolated CoM's, s: positive means the momentum term saw
        // the body leaving its support that much earlier (negative = it left
        // later, 0 = one of them never crossed). captureHeightMin/Mean m — the h
        // inside omega, so the frames where the inverted-pendulum model is out
        // of range (a body on the floor, h pinned at its 0.10 m floor) are
        // visible. Projection* is the same witness for the CoM projection.
        AppendNum(json, "captureMarginMin", captureXiMargins.Count > 0 ? captureXiMarginMin : 0f);
        AppendNum(json, "captureMarginP05", CaptureXiMarginP05());
        AppendNum(json, "captureMarginMean", samples > 0 ? (float)(sumCaptureXiMargin / samples) : 0f);
        json.Append(",\"captureMarginMinFrame\":").Append(captureXiMarginMinFrame.ToString(CultureInfo.InvariantCulture));
        json.Append(",\"captureMarginNegativeFrames\":").Append(captureXiMarginNegativeFrames.ToString(CultureInfo.InvariantCulture));
        json.Append(",\"captureMarginFirstNegativeFrame\":").Append(captureXiFirstNegativeFrame.ToString(CultureInfo.InvariantCulture));
        AppendNum(json, "captureMarginFirstNegativeTime", captureXiFirstNegativeTime);
        json.Append(",\"projectionFirstNegativeFrame\":").Append(projectionFirstNegativeFrame.ToString(CultureInfo.InvariantCulture));
        AppendNum(json, "projectionFirstNegativeTime", projectionFirstNegativeTime);
        float cycle3Lead = captureXiFirstNegativeTime >= 0f && projectionFirstNegativeTime >= 0f
            ? projectionFirstNegativeTime - captureXiFirstNegativeTime
            : 0f;
        AppendNum(json, "captureMarginLeadSeconds", cycle3Lead);
        AppendNum(json, "captureHeightMin", captureXiMargins.Count > 0 ? captureXiHeightMin : 0f);
        AppendNum(json, "captureHeightMean", samples > 0 ? (float)(sumCaptureXiHeight / samples) : 0f);
        json.Append(",\"cycle1NanCount\":").Append(cycle1NanCount.ToString(CultureInfo.InvariantCulture));
        // ─── CYCLE 21 SENSOR (read-only; no decision is taken on it) ───
        // The card's `slip in metres`, formed from the pawSoleX* columns: per paw
        // the sum of |delta pawSoleX| over each maximal run of frames with that
        // paw's OWN grounded flag at 1 (a one-frame run carries no delta and
        // contributes 0; the run still counts in pawSoleSlipRunCount*). Run* = over
        // the whole run (including the quiet part); Walk* = over the frames of the
        // recorder's walk window (stance.IsWalk() || walkWanted — the condition
        // that opens the window written above); MaxRunM / MaxRunPaw = the largest
        // single grounded-run slip and the paw that made it (0 FR, 1 FL, 2 RR,
        // 3 RL; -1 when no run closed), so ONE bad run is visible beside the
        // total instead of being averaged away. Slip is a magnitude: the sign of
        // the slide is not kept. pawSoleNanCount counts the non-finite readings of
        // the four new columns (0 on an accepted run).
        float pawSoleMaxRun = soleSlipMaxRun;
        int pawSoleMaxRunPaw = soleSlipMaxRunPaw;
        for (int paw = 0; paw < 4; paw++)
        {
            if (soleRunOpen[paw] && soleRunSlip[paw] > pawSoleMaxRun)
            {
                pawSoleMaxRun = soleRunSlip[paw];
                pawSoleMaxRunPaw = paw;
            }
        }
        AppendNum(json, "pawSoleSlipRunFR", (float)soleSlipRun[0]);
        AppendNum(json, "pawSoleSlipRunFL", (float)soleSlipRun[1]);
        AppendNum(json, "pawSoleSlipRunRR", (float)soleSlipRun[2]);
        AppendNum(json, "pawSoleSlipRunRL", (float)soleSlipRun[3]);
        AppendNum(json, "pawSoleSlipRunM",
            (float)(soleSlipRun[0] + soleSlipRun[1] + soleSlipRun[2] + soleSlipRun[3]));
        AppendNum(json, "pawSoleSlipWalkFR", (float)soleSlipWalk[0]);
        AppendNum(json, "pawSoleSlipWalkFL", (float)soleSlipWalk[1]);
        AppendNum(json, "pawSoleSlipWalkRR", (float)soleSlipWalk[2]);
        AppendNum(json, "pawSoleSlipWalkRL", (float)soleSlipWalk[3]);
        AppendNum(json, "pawSoleSlipWalkM",
            (float)(soleSlipWalk[0] + soleSlipWalk[1] + soleSlipWalk[2] + soleSlipWalk[3]));
        // ─── CYCLE 104: THE SUMS' WINDOW-CLOSED TWIN ───
        // The same c21 meter over the closed walk window (Window*) and over
        // the letter's own span (Span*, the c99 2.00 s clamp), so the sums
        // and the letter read the same segment (the walk / run sums above
        // stay untouched; a segment that never opened reads 0).
        double[] twinWindow = new double[4];
        double[] twinSpan = new double[4];
        for (int k = 0; k + 4 < slipStepRows.Count; k += 5)
        {
            float tk = slipStepRows[k];
            if (walkStartTime < 0f || tk < walkStartTime - 1e-4f) continue;
            bool inTwinWindow = tk <= walkEndTime + 1e-4f;
            bool inTwinSpan = walkSpanEndSet && tk <= walkSpanEndTime + 1e-4f;
            if (!inTwinWindow && !inTwinSpan) continue;
            for (int paw = 0; paw < 4; paw++)
            {
                double v = slipStepRows[k + 1 + paw];
                if (inTwinWindow) twinWindow[paw] += v;
                if (inTwinSpan) twinSpan[paw] += v;
            }
        }
        AppendNum(json, "pawSoleSlipWindowFR", (float)twinWindow[0]);
        AppendNum(json, "pawSoleSlipWindowFL", (float)twinWindow[1]);
        AppendNum(json, "pawSoleSlipWindowRR", (float)twinWindow[2]);
        AppendNum(json, "pawSoleSlipWindowRL", (float)twinWindow[3]);
        AppendNum(json, "pawSoleSlipWindowM",
            (float)(twinWindow[0] + twinWindow[1] + twinWindow[2] + twinWindow[3]));
        AppendNum(json, "pawSoleSlipSpanFR", (float)twinSpan[0]);
        AppendNum(json, "pawSoleSlipSpanFL", (float)twinSpan[1]);
        AppendNum(json, "pawSoleSlipSpanRR", (float)twinSpan[2]);
        AppendNum(json, "pawSoleSlipSpanRL", (float)twinSpan[3]);
        AppendNum(json, "pawSoleSlipSpanM",
            (float)(twinSpan[0] + twinSpan[1] + twinSpan[2] + twinSpan[3]));
        json.Append(",\"pawSoleSlipRunCountFR\":").Append(soleRunCount[0].ToString(CultureInfo.InvariantCulture));
        json.Append(",\"pawSoleSlipRunCountFL\":").Append(soleRunCount[1].ToString(CultureInfo.InvariantCulture));
        json.Append(",\"pawSoleSlipRunCountRR\":").Append(soleRunCount[2].ToString(CultureInfo.InvariantCulture));
        json.Append(",\"pawSoleSlipRunCountRL\":").Append(soleRunCount[3].ToString(CultureInfo.InvariantCulture));
        AppendNum(json, "pawSoleSlipMaxRunM", pawSoleMaxRun);
        json.Append(",\"pawSoleSlipMaxRunPaw\":").Append(pawSoleMaxRunPaw.ToString(CultureInfo.InvariantCulture));
        json.Append(",\"pawSoleNanCount\":").Append(pawSoleNanCount.ToString(CultureInfo.InvariantCulture));
        // ─── CYCLE 25 SENSOR: the righting rock (read-only; see the fields) ───
        // rightingRocks — the trunk's own reversals measured while inverted
        // (the gate's deadband, re-derived here from the pitch trace);
        // rightingMaxDevDeg — the deepest |180 - |unwrapped pitch|| reached off
        // the flat-on-back pose; rightingCrossSeconds — the seconds from the
        // first inverted frame to the |chestPitch| <= 100 held-20-frames
        // crossing (-1 = the run never crossed); rightingGrowRocks — the
        // longest chain of successive same-side rocks whose apex was DEEPER
        // than the one before it (the card's "the rock grows >= 3 successive
        // rocks" milestone reads on this key).
        json.Append(",\"rightingRocks\":").Append(rockRocks.ToString(CultureInfo.InvariantCulture));
        AppendNum(json, "rightingMaxDevDeg", rockMaxAbsDev);
        AppendNum(json, "rightingCrossSeconds", rockCrossSeconds);
        json.Append(",\"rightingGrowRocks\":").Append(rockGrowRunMax.ToString(CultureInfo.InvariantCulture));
        // ─── CYCLE 26 SENSOR: the catch at the line (read-only) ───
        // catchArmedFrames — the frames the catch's band owned; catchTouches —
        // the distinct hind-pad touches in it (a rising edge of the pad column);
        // catchDevDeg — the deepest dev at a touch (|chestPitch| - 180, deg; -1
        // = the band never armed); catchPadSeconds / catchPushSeconds — the
        // seconds a hind pad held the floor in the band and the seconds the
        // extension (the press) was commanded; catchMinClearM — the lowest hind
        // pad clearance the servo saw in the band, m (-1 = never armed);
        // catchCrossSeconds — the seconds from the first inverted frame to the
        // card's crossing (|chestPitch| <= 100 held 20 frames) measured on every
        // frame, the band included (-1 = never crossed); catchHeld — that
        // crossing fired at or after a catch touch.
        json.Append(",\"catchArmedFrames\":").Append(catchArmedSamples.ToString(CultureInfo.InvariantCulture));
        json.Append(",\"catchTouches\":").Append(catchTouchCount.ToString(CultureInfo.InvariantCulture));
        AppendNum(json, "catchDevDeg", catchDeepestDev < 1000f ? catchDeepestDev : -1f);
        AppendNum(json, "catchPadSeconds", catchHoldSeconds);
        AppendNum(json, "catchPushSeconds", catchPushSeconds);
        AppendNum(json, "catchMinClearM", catchMinClear);
        AppendNum(json, "catchCrossSeconds", catchCrossSeconds);
        json.Append(",\"catchHeld\":").Append(catchCrossed ? "true" : "false");
        // ─── CYCLE 27 SENSOR: the back-plant (read-only) ───
        // catchTouchSide — the side of the first hind pad that held the floor in
        // the band ("RR"/"RL"/"both"/"none"); catchSoleComM — that touch's sole
        // point against the CoM, m (+ = FORWARD of the CoM, the side c26 landed
        // on; - = behind it, the side this cycle needs; -1 = no touch);
        // catchHipComM — that leg's hip pivot against the CoM at the same frame,
        // m (the base of the reach's lever); catchBackMinM — the most negative
        // signed sole distance the band reached on any frame, m (-1 = never
        // armed).
        AppendStr(json, "catchTouchSide", catchSideFirst);
        AppendNum(json, "catchSoleComM", catchSoleComSeeded ? catchSoleComFirst : -1f);
        AppendNum(json, "catchHipComM", catchSoleComSeeded ? catchHipComFirst : -1f);
        AppendNum(json, "catchBackMinM", catchSoleComMin < 1000f ? catchSoleComMin : -1f);
        // ─── CYCLE 28 SENSOR: the rump corner (read-only) ───
        // rumpClearM / rumpComM — the pelvis's rear corner's height over the slab
        // and its x against the CoM at the band's first armed frame (the hold's
        // own start), m (+ = the corner is FORWARD of the CoM; - = behind it);
        // rumpClearMinM — the deepest that corner got over the band;
        // rumpLowMinM — the deepest ANY corner of the pelvis got (0 = a corner
        // was on the slab). -1 = the band never armed.
        AppendNum(json, "rumpClearM", rumpSeeded ? rumpClearFirst : -1f);
        AppendNum(json, "rumpComM", rumpSeeded ? rumpComFirst : -1f);
        AppendNum(json, "rumpClearMinM", rumpClearMin < 1000f ? rumpClearMin : -1f);
        AppendNum(json, "rumpLowMinM", rumpLowMin < 1000f ? rumpLowMin : -1f);
        // ─── CYCLE 36 SENSOR: the front pads' own clearance (read-only) ───
        // frontClearM — the lowest front pad's clearance over the slab at the
        // get-up's first frame, m (the front limbs' own geometry, the mirror of
        // rearClearM; -1 = the get-up never ran);
        // frontClearMinM — the deepest that pair ever got over the get-up, m (the
        // number that says whether the slab was ever inside the reach at all).
        AppendNum(json, "frontClearM", frontClearSeeded ? frontClearFirst : -1f);
        AppendNum(json, "frontClearMinM", frontClearMin < 1000f ? frontClearMin : -1f);
        // ─── CYCLE 31 SENSOR: the claws (read-only) ───
        // clawEngageFrames — the frames a hind pad's grip was held (clawPaw
        // names a side); clawEngageFraction — those frames as a fraction of the
        // catch's armed frames; clawEngageSeconds — the same in seconds;
        // clawEngageFirstFrame / clawEngageFirstTime — the first grip of the
        // run (row index, s; -1 = never); clawEngageSide — "RR"/"RL"/"both"/
        // "none" (the sides a grip was held on); clawSlipMaxM — the deepest
        // anchored slip a grip held before letting go, m; clawTfMaxN — the
        // biggest tangential load a grip carried, N; clawCapAtTfMaxN — the
        // capacity beside it at that frame, N; clawTfFracMax — the biggest
        // Tf/Cap of the run (1 = the solver's own sliding limit);
        // clawAtCapFrames — the frames the load sat at the capacity (>= 0.98);
        // clawMomentMaxNm — the biggest |moment| a gripped contact made about
        // the CoM, N*m; clawRipouts — the grips torn out (a slip release while
        // the pad was still loaded), clawRipoutFirstFrame — the first tear's
        // row (-1 = none); clawMuPair — the pair coefficient the capacity
        // column was computed with (PhysicsMaterial2D.GetCombinedValues of the
        // pad's claw material and the ground's); clawMuMeasured — the biggest
        // Tf/Fn the gripped contacts actually reached (the measured check on
        // the pair above; it saturates at the capacity only while sliding).
        json.Append(",\"clawEngageFrames\":").Append(clawEngageFrames.ToString(CultureInfo.InvariantCulture));
        AppendNum(json, "clawEngageFraction",
            catchArmedSamples > 0 ? clawEngageFrames / (float)catchArmedSamples : 0f);
        AppendNum(json, "clawEngageSeconds", clawEngageFrames * Time.fixedDeltaTime);
        json.Append(",\"clawEngageFirstFrame\":").Append(clawEngageFirstFrame.ToString(CultureInfo.InvariantCulture));
        AppendNum(json, "clawEngageFirstTime", clawEngageFirstFrame >= 0 ? clawEngageFirstTime : -1f);
        AppendStr(json, "clawEngageSide", clawSideMask == 3 ? "both"
            : (clawSideMask == 1 ? "RR" : (clawSideMask == 2 ? "RL" : "none")));
        AppendNum(json, "clawSlipMaxM", clawSlipMax);
        AppendNum(json, "clawTfMaxN", clawTfMax);
        AppendNum(json, "clawCapAtTfMaxN", clawCapAtTfMax);
        AppendNum(json, "clawTfFracMax", clawFracMax);
        json.Append(",\"clawAtCapFrames\":").Append(clawAtCapFrames.ToString(CultureInfo.InvariantCulture));
        AppendNum(json, "clawMomentMaxNm", clawMomentMax);
        json.Append(",\"clawRipouts\":").Append(clawRipouts.ToString(CultureInfo.InvariantCulture));
        json.Append(",\"clawRipoutFirstFrame\":").Append(clawRipoutFirstFrame.ToString(CultureInfo.InvariantCulture));
        AppendNum(json, "clawMuPair", clawMuPair);
        AppendNum(json, "clawMuMeasured", clawMuMeasured);
        // ─── CYCLE 32 SENSOR: the fold (read-only) ───
        // The fold's own clock and the trunk's curl, all of it -1 / 0 when the
        // fold never ran (the get-up never reached its band):
        // foldFirstFrame / foldFirstTime — the first frame the fold's weight
        //   rose off zero (the band's arm + the ramp), row index / s;
        // foldFrames / foldOpenFrames — the frames the fold ran and the frames
        //   the rig's wide range was open (both end when the band lets go);
        // foldWeightMax — the biggest fold weight the run reached, 0..1;
        // foldLumbarMinDeg / foldLumbarMaxDeg — the lumbar's own hinge angle at
        //   its extremes while the fold ran, deg (the rest hold is +8; positive
        //   = flexion, toward the belly), with foldLumbarMaxFrame / Time;
        // foldPastStopFrames / foldPastStopMaxDeg — the frames the joint spent
        //   beyond the BRICK's own clamp (the accepted flexion stop, +20) and
        //   the deepest such angle: the fold's excursion where the old clamp
        //   bound, the number this cycle exists to move;
        // foldLumbarAtPlantDeg / foldLumbarAtPlantFrame — the deepest flexion
        //   reached while the fold ran AND a hind pad was planted (the curl
        //   against the plant's own clock);
        // foldNeckMinDeg / foldNeckMaxDeg — the neck's own hinge angle at its
        //   extremes while the fold ran, deg (the follow);
        // foldLumbarFlexLimitDeg / foldNeckFlexLimitDeg — the rig's own wide
        //   flexion limits (Dog.cs's LumbarFlexionMaxDeg / NeckFlexionMaxDeg),
        //   beside foldLumbarAcceptedMaxDeg — the clamp they replace.
        json.Append(",\"foldFirstFrame\":").Append(foldFirstFrame.ToString(CultureInfo.InvariantCulture));
        AppendNum(json, "foldFirstTime", foldFirstFrame >= 0 ? foldFirstTime : -1f);
        json.Append(",\"foldFrames\":").Append(foldFrames.ToString(CultureInfo.InvariantCulture));
        json.Append(",\"foldOpenFrames\":").Append(foldOpenFrames.ToString(CultureInfo.InvariantCulture));
        AppendNum(json, "foldWeightMax", foldWeightMax);
        AppendNum(json, "foldLumbarMinDeg", foldLumbarMin < 1000f ? foldLumbarMin : -1f);
        AppendNum(json, "foldLumbarMaxDeg", foldLumbarMax > -1000f ? foldLumbarMax : -1f);
        json.Append(",\"foldLumbarMaxFrame\":").Append(foldLumbarMaxFrame.ToString(CultureInfo.InvariantCulture));
        AppendNum(json, "foldLumbarMaxTime", foldLumbarMaxFrame >= 0 ? foldLumbarMaxTime : -1f);
        json.Append(",\"foldPastStopFrames\":").Append(foldPastStopFrames.ToString(CultureInfo.InvariantCulture));
        AppendNum(json, "foldPastStopMaxDeg", foldPastStopMax);
        AppendNum(json, "foldLumbarAtPlantDeg",
            foldLumbarAtPlantMax > -1000f ? foldLumbarAtPlantMax : -1f);
        json.Append(",\"foldLumbarAtPlantFrame\":").Append(foldLumbarAtPlantFrame.ToString(CultureInfo.InvariantCulture));
        AppendNum(json, "foldNeckMinDeg", foldNeckMin < 1000f ? foldNeckMin : -1f);
        AppendNum(json, "foldNeckMaxDeg", foldNeckMax > -1000f ? foldNeckMax : -1f);
        AppendNum(json, "foldLumbarFlexLimitDeg", Dog.LumbarFlexionMaxDeg);
        AppendNum(json, "foldNeckFlexLimitDeg", Dog.NeckFlexionMaxDeg);
        AppendNum(json, "foldLumbarAcceptedMaxDeg", Dog.LumbarAcceptedMaxDeg);
        AppendApplied(json);
        // ─── CYCLE 91 SENSOR aggregates: the plant geometry per paw ───
        // pawClearMinM* = the deepest that paw's pad face reached over the slab
        // (m; -1 = no finite reading); pawTouchCount* = the airborne->grounded
        // edges counted; pawTouchDropMaxMps* / pawTouchDropLastMps* = the
        // fastest and the most recent arrival speed (m/s, positive = descending;
        // -1 = no touch); pawAirRunMaxSeconds* = the longest air run between two
        // touches (s; -1 = no touch yet). Paw order FR, FL, RR, RL.
        // pawClearNanCount = the non-finite readings of the eight cycle-91
        // columns; 0 is the acceptance.
        AppendNum(json, "pawClearNanCount", pawClearNanCount);
        for (int paw = 0; paw < 4; paw++)
        {
            AppendNum(json, PawKey("pawClearMinM", paw), pawClearMin[paw] < 1000f ? pawClearMin[paw] : -1f);
            json.Append(",\"").Append(PawKey("pawTouchCount", paw)).Append("\":")
                .Append(pawTouchCount[paw].ToString(CultureInfo.InvariantCulture));
            AppendNum(json, PawKey("pawTouchDropMaxMps", paw), pawSawTouch[paw] ? pawTouchDropMax[paw] : -1f);
            AppendNum(json, PawKey("pawTouchDropLastMps", paw), pawSawTouch[paw] ? pawTouchDropLast[paw] : -1f);
            AppendNum(json, PawKey("pawAirRunMaxSeconds", paw), pawSawTouch[paw] ? pawAirRunMax[paw] : -1f);
        }
        json.Append("}");
        return json.ToString();
    }

    private void AppendApplied(StringBuilder json)
    {
        int bodyCount = 0;
        int continuous = 0;
        if (dog != null)
        {
            Rigidbody2D[] rbs = dog.GetComponentsInChildren<Rigidbody2D>();
            bodyCount = rbs.Length;
            for (int i = 0; i < rbs.Length; i++)
            {
                if (rbs[i] != null && rbs[i].collisionDetectionMode == CollisionDetectionMode2D.Continuous)
                    continuous++;
            }
        }

        int muscles = dog != null ? dog.GetComponentsInChildren<Muscle>().Length : 0;
        int frictions = dog != null ? dog.GetComponentsInChildren<JointFriction>().Length : 0;
        DogStanceController c = stance;

        json.Append(",\"applied\":{");
        json.Append("\"subject\":\"dog\"");
        if (dog != null)
        {
            AppendNum(json, "totalMass", dog.totalMass);
            AppendVec2(json, "chestSize", dog.chestSize);
            AppendVec2(json, "pelvisSize", dog.pelvisSize);
            AppendVec2(json, "pawSize", dog.pawSize);
            AppendNum(json, "spawnHipAngle", dog.spawnHipAngle);
            AppendNum(json, "spawnShoulderAngle", dog.spawnShoulderAngle);
            AppendNum(json, "spawnKneeAngle", dog.spawnKneeAngle);
            AppendNum(json, "spawnElbowAngle", dog.spawnElbowAngle);
            AppendNum(json, "tailSegmentCount", dog.tailSegmentCount);
            AppendNum(json, "lumbarFriction", dog.lumbarFriction);
            AppendNum(json, "hipFriction", dog.hipFriction);
            AppendNum(json, "kneeFriction", dog.kneeFriction);
            AppendNum(json, "shoulderFriction", dog.shoulderFriction);
            AppendNum(json, "elbowFriction", dog.elbowFriction);
            AppendNum(json, "pawFriction", dog.pawFriction);
            AppendNum(json, "neckFriction", dog.neckFriction);
            AppendNum(json, "lumbarMuscleTorque", dog.lumbarMuscleTorque);
            AppendNum(json, "hipMuscleTorque", dog.hipMuscleTorque);
            AppendNum(json, "kneeMuscleTorque", dog.kneeMuscleTorque);
            AppendNum(json, "shoulderMuscleTorque", dog.shoulderMuscleTorque);
            AppendNum(json, "elbowMuscleTorque", dog.elbowMuscleTorque);
            AppendNum(json, "pawExtensorTorque", dog.pawExtensorTorque);
            AppendNum(json, "pawFlexorTorque", dog.pawFlexorTorque);
            AppendNum(json, "neckMuscleTorque", dog.neckMuscleTorque);
            AppendNum(json, "jawMuscleTorque", dog.jawMuscleTorque);
            json.Append(",\"hasJaw\":").Append(dog.jawCollider != null ? "true" : "false");
        }
        json.Append(",\"rigidbodyCount\":").Append(bodyCount.ToString(CultureInfo.InvariantCulture));
        json.Append(",\"continuousBodyCount\":").Append(continuous.ToString(CultureInfo.InvariantCulture));
        json.Append(",\"muscleBehaviourCount\":").Append(muscles.ToString(CultureInfo.InvariantCulture));
        json.Append(",\"frictionBehaviourCount\":").Append(frictions.ToString(CultureInfo.InvariantCulture));
        json.Append(",\"velocityIterations\":").Append(Physics2D.velocityIterations.ToString(CultureInfo.InvariantCulture));
        json.Append(",\"positionIterations\":").Append(Physics2D.positionIterations.ToString(CultureInfo.InvariantCulture));
        if (c != null)
        {
            AppendNum(json, "pawComP", c.pawComP);
            AppendNum(json, "pawComD", c.pawComD);
            AppendNum(json, "pawComDeadzone", c.pawComDeadzone);
            AppendNum(json, "pawComVelDeadzone", c.pawComVelDeadzone);
            AppendNum(json, "pawGroundHoldSeconds", c.pawGroundHoldSeconds);
            AppendNum(json, "chestPGain", c.chestPGain);
            AppendNum(json, "chestDGain", c.chestDGain);
            AppendNum(json, "chestTargetTilt", c.chestTargetTilt);
            AppendNum(json, "pelvisPGain", c.pelvisPGain);
            AppendNum(json, "pelvisDGain", c.pelvisDGain);
            AppendNum(json, "pelvisTargetTilt", c.pelvisTargetTilt);
            AppendNum(json, "lumbarPGain", c.lumbarPGain);
            AppendNum(json, "lumbarDGain", c.lumbarDGain);
            AppendNum(json, "lumbarPelvisP", c.lumbarPelvisP);
            AppendNum(json, "lumbarPelvisD", c.lumbarPelvisD);
            AppendNum(json, "lumbarStopP", c.lumbarStopP);
            AppendNum(json, "lumbarRestP", c.lumbarRestP);
            AppendNum(json, "lumbarDelaySeconds", c.lumbarDelaySeconds);
            AppendNum(json, "lumbarOpenGateDeg", c.lumbarOpenGateDeg);
            json.Append(",\"useLumbarJointRest\":").Append(c.useLumbarJointRest ? "true" : "false");
            AppendNum(json, "lumbarJointRestDeg", c.lumbarJointRestDeg);
            AppendNum(json, "hipPGain", c.hipPGain);
            AppendNum(json, "kneePGain", c.kneePGain);
            AppendNum(json, "startupHipP", c.startupHipP);
            AppendNum(json, "startupHipSeconds", c.startupHipSeconds);
            AppendNum(json, "hipBaseAngle", c.hipBaseAngle);
            AppendNum(json, "shoulderBaseAngle", c.shoulderBaseAngle);
            AppendNum(json, "kneeBaseAngle", c.kneeBaseAngle);
            AppendNum(json, "elbowBaseAngle", c.elbowBaseAngle);
            AppendNum(json, "heightP", c.heightP);
            AppendNum(json, "heightD", c.heightD);
            AppendNum(json, "heightMaxDeg", c.heightMaxDeg);
            AppendNum(json, "heightHipMaxDeg", c.heightHipMaxDeg);
            AppendNum(json, "heightDeadzone", c.heightDeadzone);
            AppendNum(json, "heightVelDeadzone", c.heightVelDeadzone);
            AppendNum(json, "poseHoldDeadzoneDeg", c.poseHoldDeadzoneDeg);
            AppendNum(json, "startupHoldSeconds", c.startupHoldSeconds);
            AppendNum(json, "jawOpen", c.jawOpen);
            AppendNum(json, "jawAngle", c.jawAngle);
            AppendStr(json, "currentState", c.currentState.ToString());
            AppendNum(json, "leapCount", c.leapCount);
            AppendNum(json, "leapCrouchSeconds", c.leapCrouchSeconds);
            AppendNum(json, "leapPushSeconds", c.leapPushSeconds);
            AppendNum(json, "leapAirSeconds", c.leapAirSeconds);
            AppendNum(json, "leapBiteSeconds", c.leapBiteSeconds);
            AppendNum(json, "leapBiteRange", c.leapBiteRange);
            AppendNum(json, "leapPawPlant", c.leapPawPlant);
            AppendNum(json, "leapActivationSpeed", c.leapActivationSpeed);
            AppendNum(json, "leapAimX", c.LeapAim.x);
            AppendNum(json, "leapAimY", c.LeapAim.y);
            json.Append(",\"leapTargetSet\":").Append(c.leapTarget != null ? "true" : "false");
            json.Append(",\"walkWanted\":").Append(c.walkWanted ? "true" : "false");
            AppendNum(json, "walkPeriod", c.walkPeriod);
            AppendNum(json, "walkDuty", c.walkDuty);
            AppendNum(json, "walkRearLag", c.walkRearLag);
            AppendNum(json, "walkHipFlex", c.walkHipFlex);
            AppendNum(json, "walkShoulderFlex", c.walkShoulderFlex);
            AppendNum(json, "walkHipExtend", c.walkHipExtend);
            AppendNum(json, "walkShoulderExtend", c.walkShoulderExtend);
            AppendNum(json, "walkKneeFlex", c.walkKneeFlex);
            AppendNum(json, "walkElbowFlex", c.walkElbowFlex);
            AppendNum(json, "walkPawPlant", c.walkPawPlant);
            AppendNum(json, "walkFrontPlantScale", c.walkFrontPlantScale);
            AppendNum(json, "walkStanceSweepDeg", c.walkStanceSweepDeg);
            AppendNum(json, "walkChestTilt", c.walkChestTilt);
            AppendNum(json, "walkLumbarP", c.walkLumbarP);
            AppendNum(json, "walkHipP", c.walkHipP);
            AppendNum(json, "walkKneeP", c.walkKneeP);
            AppendNum(json, "walkActivationSpeed", c.walkActivationSpeed);
            AppendNum(json, "runSpeed", c.runSpeed);
            AppendNum(json, "runSpeedRef", c.runSpeedRef);
            AppendNum(json, "runStride", c.runStride);
            AppendNum(json, "runHipFlex", c.runHipFlex);
            AppendNum(json, "runShoulderFlex", c.runShoulderFlex);
            AppendNum(json, "runHipExtend", c.runHipExtend);
            AppendNum(json, "runShoulderExtend", c.runShoulderExtend);
            AppendNum(json, "runKneeFlex", c.runKneeFlex);
            AppendNum(json, "runElbowFlex", c.runElbowFlex);
            AppendNum(json, "runChestLean", c.runChestLean);
            AppendNum(json, "runPawPlant", c.runPawPlant);
            json.Append(",\"walkCycleCount\":").Append(c.walkCycleCount.ToString(CultureInfo.InvariantCulture));
            AppendNum(json, "walkPhase", c.walkPhase);
            json.Append(",\"walkCollapsed\":").Append(c.WalkCollapsed ? "true" : "false");
            json.Append(",\"walkPosed\":").Append(c.WalkPosed ? "true" : "false");
            json.Append(",\"senseTick\":").Append(c.senseTick.ToString(CultureInfo.InvariantCulture));
            AppendNum(json, "supportMargin", c.supportMargin);
            json.Append(",\"jawStrikeHits\":").Append((jawStrike != null ? jawStrike.HitCount : 0).ToString(CultureInfo.InvariantCulture));
            json.Append(",\"preyHurtCount\":").Append((preyLife != null ? preyLife.HurtCount : 0).ToString(CultureInfo.InvariantCulture));
            AppendNum(json, "targetPelvisY", c.targetPelvisY);
            AppendNum(json, "neckPGain", c.neckPGain);
            AppendNum(json, "neckDGain", c.neckDGain);
            json.Append(",\"useStablePd\":").Append(c.useStablePd ? "true" : "false");
            AppendNum(json, "muscleActivationSpeed", c.muscleActivationSpeed);
            AppendNum(json, "fallCoMOffset", c.fallCoMOffset);
            if (c.probeCallCount > 0)
            {
                AppendNum(json, "meanProbeHits", c.probeHitSum / (float)c.probeCallCount);
                AppendNum(json, "probeSaturatedFraction", c.probeSaturatedCount / (float)c.probeCallCount);
            }
        }
        json.Append("}");
    }

    private string FormatWalkOnsetLog()
    {
        if (stance == null || stance.WalkOnsetLogCount <= 0)
            return "";
        var log = new StringBuilder();
        for (int i = 0; i < DogStanceController.WalkOnsetLogCapacity; i++)
        {
            float age;
            bool collapsed;
            bool posed;
            float comX;
            float margin;
            float hipDrop;
            float comDrop;
            float columnStanding;
            byte reason;
            if (!stance.TryGetWalkOnsetSample(i, out age, out collapsed,
                    out posed, out comX, out margin, out hipDrop, out comDrop,
                    out columnStanding, out reason))
                continue;
            if (log.Length > 0)
                log.Append('|');
            log.Append(age.ToString("0.00", CultureInfo.InvariantCulture));
            log.Append(",c").Append(collapsed ? "1" : "0");
            log.Append(",p").Append(posed ? "1" : "0");
            log.Append(",x").Append(comX.ToString("0.000", CultureInfo.InvariantCulture));
            log.Append(",m").Append(margin.ToString("0.000", CultureInfo.InvariantCulture));
            log.Append(",hd").Append(hipDrop.ToString("0.000", CultureInfo.InvariantCulture));
            log.Append(",cd").Append(comDrop.ToString("0.000", CultureInfo.InvariantCulture));
            log.Append(",cs").Append(columnStanding.ToString("0.000", CultureInfo.InvariantCulture));
            log.Append(",r").Append(reason.ToString(CultureInfo.InvariantCulture));
        }
        return log.ToString();
    }

    // 5th percentile of the per-frame capture-point margin (m): a sorted copy of
    // the frame list. An empty run has no percentile and returns 0.
    private float CaptureMarginP05()
    {
        if (captureMargins.Count == 0) return 0f;
        List<float> sorted = new List<float>(captureMargins);
        sorted.Sort();
        int idx = Mathf.Clamp(Mathf.FloorToInt(0.05f * sorted.Count), 0, sorted.Count - 1);
        return sorted[idx];
    }

    // Same 5th percentile for the EXTRAPOLATED CoM margin (m), cycle 3: the
    // matching tail of the projection's p05 above, so the two can be compared
    // per run instead of per frame.
    private float CaptureXiMarginP05()
    {
        if (captureXiMargins.Count == 0) return 0f;
        List<float> sorted = new List<float>(captureXiMargins);
        sorted.Sort();
        int idx = Mathf.Clamp(Mathf.FloorToInt(0.05f * sorted.Count), 0, sorted.Count - 1);
        return sorted[idx];
    }

    // Mean per-paw normal impulse over the run, N*s. Paw order FR, FL, RR, RL.
    private float MeanPawImpulse(int paw)
    {
        if (samples <= 0 || paw < 0 || paw >= sumPawImpulse.Length) return 0f;
        return (float)(sumPawImpulse[paw] / samples);
    }

    // ─── CYCLE 21 SENSOR helpers ───
    // The paw hinge joints, in the CSV's paw order FR, FL, RR, RL — the order of
    // the grounded flags (DogStanceController.PawCollider :5560-5570) and of the
    // controller's own public joint fields (frontRightPawJoint .. rearLeftPawJoint,
    // :997-998). Read-only: the controller is not asked to publish anything new.
    private HingeJoint2D PawJoint(int index)
    {
        if (stance == null) return null;
        switch (index)
        {
            case 0: return stance.frontRightPawJoint;
            case 1: return stance.frontLeftPawJoint;
            case 2: return stance.rearRightPawJoint;
            case 3: return stance.rearLeftPawJoint;
            default: return null;
        }
    }

    // The paw's own grounded flag, same order. The flags are the ones the CSV
    // carries (frG, flG, rrG, rlG), so the slip tally and the trace's own
    // contact record cannot disagree about which frames a paw was down.
    private bool PawGroundedNow(int index)
    {
        if (stance == null) return false;
        switch (index)
        {
            case 0: return stance.frontRightGrounded;
            case 1: return stance.frontLeftGrounded;
            case 2: return stance.rearRightGrounded;
            case 3: return stance.rearLeftGrounded;
            default: return false;
        }
    }

    // Nominal stride from the rig's own link geometry — the same sizes
    // DogStanceController caches at Bind — as the chord a stance paw sweeps when
    // its limb rotates by walkStanceSweepDeg about its root joint:
    //   2 * L * sin(sweep / 2),   L = upper + lower + half a paw.
    // A geometric bar, never a bar the controller captured about itself.
    private void NominalStrides(out float front, out float rear)
    {
        front = 0f;
        rear = 0f;
        if (dog == null || stance == null) return;
        float halfSweep = 0.5f * stance.walkStanceSweepDeg * Mathf.Deg2Rad;
        float pawHalf = 0.5f * dog.pawSize.y;
        front = 2f * (dog.frontUpperSize.y + dog.frontLowerSize.y + pawHalf) * Mathf.Sin(halfSweep);
        rear = 2f * (dog.rearThighSize.y + dog.rearShinSize.y + pawHalf) * Mathf.Sin(halfSweep);
    }

    // ─── CYCLE 91 SENSOR helpers ───
    // The slab top, probed once — the mirror of DogStanceController.CacheWalkGround
    // (:2176-2185: y = -2.0 unless a "Ground" object exists), so the per-paw
    // clearances read the same datum as rearClearM / frontClearM.
    private float SlabTopY()
    {
        if (!slabTopProbed)
        {
            slabTopProbed = true;
            slabTopY = -2f;
            GameObject go = GameObject.Find("Ground");
            if (go != null)
            {
                Collider2D col = go.GetComponent<Collider2D>();
                if (col != null) slabTopY = col.bounds.max.y;
            }
        }
        return slabTopY;
    }

    // Each paw's own collider, in the CSV's paw order (FR, FL, RR, RL — the
    // grounded flags' order). The pad face's clearance is measured off the same
    // child the controller's own Col("FrontRightPaw") family binds (:2229-2233).
    private Collider2D PawCol(int index)
    {
        if (pawCols[index] == null)
        {
            HingeJoint2D pawJoint = PawJoint(index);
            pawCols[index] = pawJoint != null ? pawJoint.GetComponent<Collider2D>() : null;
        }
        return pawCols[index];
    }

    // Paw order names for the cycle-91 JSON keys (FR, FL, RR, RL).
    private static string PawKey(string stem, int paw)
    {
        switch (paw)
        {
            case 0: return stem + "FR";
            case 1: return stem + "FL";
            case 2: return stem + "RR";
            default: return stem + "RL";
        }
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private void AppendCsvFloat(float value)
    {
        Span<char> buffer = numberBuffer;
        if (value.TryFormat(buffer, out int written, "G9", CultureInfo.InvariantCulture))
        {
            rows.Append(numberBuffer, 0, written);
            return;
        }

        rows.Append(value.ToString("G9", CultureInfo.InvariantCulture));
    }

    private static void AppendStr(StringBuilder json, string key, string value, bool comma = true)
    {
        if (comma) json.Append(',');
        json.Append('"').Append(key).Append("\":\"");
        if (!string.IsNullOrEmpty(value))
            json.Append(value.Replace("\\", "\\\\").Replace("\"", "\\\""));
        json.Append('"');
    }

    private static void AppendNum(StringBuilder json, string key, float value, bool comma = true)
    {
        if (comma) json.Append(',');
        json.Append('"').Append(key).Append("\":");
        json.Append(value.ToString("G9", CultureInfo.InvariantCulture));
    }

    private static void AppendVec2(StringBuilder json, string key, Vector2 value)
    {
        json.Append(",\"").Append(key).Append("\":{\"x\":");
        json.Append(value.x.ToString("G9", CultureInfo.InvariantCulture));
        json.Append(",\"y\":");
        json.Append(value.y.ToString("G9", CultureInfo.InvariantCulture));
        json.Append('}');
    }

    // Cycle 27: the catch's own coding (0 = RR, 1 = RL, 3 = both, -1 = none —
    // DogStanceController.getUpCatchPaw) as the report's side names.
    private static string CatchSideName(int paw)
    {
        if (paw == 0) return "RR";
        if (paw == 1) return "RL";
        if (paw == 3) return "both";
        return "none";
    }
}
