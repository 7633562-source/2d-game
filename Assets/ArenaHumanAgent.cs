using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

// ARENA-1: one arena human. Owns nothing but the measurement - the body itself is
// the standard rig (Human.BuildHuman), so the numbers stay comparable with the
// walk trials.
//
// Upright / fallen come from the project-wide criterion, Assets/FallCriterion.cs
// (C1, 18.09 - the trial `fell` flag is never read here, and this file no longer
// carries a second copy of the rule):
//   down  = trunk tilt past 60 deg, OR pelvis 0.25 m below THIS human's own
//           SETTLED stand height, sustained 0.15 s
// The stand height is measured over the first second of the human's life, after
// the drop-in has settled (spawn y -0.82 settles to -1.00 within 0.25 s), while
// the body is upright. It used to be the running MAXIMUM, which in the spawn
// pose is 0.186 m above the settled stance - so 0.064 m of ordinary walk-onset
// sink latched `fallen` in 2112 of 2112 rows and the column carried no
// information at all (Trials/Runs/arena1_latch-findings.md).
//
// The trajectory ring (every 0.25 s, first 15 s) exists for one reason: the elite
// genome is re-spawned unchanged, and the two copies must walk the same distance
// and the same early path. If a younger copy is deflected, a human-human collision
// is still live.
public class ArenaHumanAgent : MonoBehaviour
{
    // Kept for the elite-copy / cull reporting: the same physical criterion the
    // shared file applies, named here only so a reader can see the numbers.
    public const float FallTiltDegrees = FallCriterion.TiltDegrees;
    public const float FallDropMeters = FallCriterion.DropMeters;
    public const float FallSustainSeconds = FallCriterion.SustainSeconds;
    // WALK CYCLE 58: 64 -> 84 slots. The fitness window now starts at the 5 s
    // walk onset and runs a full 15 s (the measure mark at 20 s), so the ring
    // must cover 20 s of the body's life; at 0.25 s a slot that is 80 samples,
    // and the four extra slots are margin for the frame grid.
    private const int TrajectorySlots = 84;
    private const float TrajectoryPeriod = 0.25f;

    // ─── the number on the head (Yury's 18.09 correction) ────────────────────
    // The digit belongs INSIDE the head: centred on the head's own box and sized to
    // a measured fraction of the head's height. `ArenaRunner.numberHeight` is the
    // REQUESTED size, so it acts as a ceiling and never as the target - its
    // Inspector default 0.34 m is larger than a whole head, which is exactly what
    // used to be drawn ABOVE the skull and then merged into the pile in the frame.
    // The correction's ceiling is 60 % of the head height; 0.55 is used so the
    // glyph keeps a margin inside the box.
    public const float NumberHeadHeightFraction = 0.55f;
    public const float NumberMaxFractionOfHead = 0.60f;
    private const int NumberLogLimit = 24;          // log the first humans only
    public static int NumberSpawned;                // numbers attached this run
    public static int NumberLogged;
    public static float NumberLastHeadHeightMeters;
    public static float NumberLastTargetHeightMeters;
    public static float NumberLastDrawnHeightMeters;
    public static bool NumberLastInsideHead;

    private TextMesh numberMesh;
    private float numberTargetHeight;
    private int numberCheckStage;                   // 0 measure+correct, 1 verify
    private float numberHeadHeight;

    // identity / inheritance (provable from the log)
    public int id;
    public int parentId = -1;
    public int childKind;              // 0 fresh random, 1 child of a parent, 2 elite copy,
                                       // 3 the run's reference genome (ArenaGenome.Defaults, id 1)
    public int mutationSeed;
    public string mutationFamily = "";   // WALK CYCLE 53: which family's genes moved ("" = none/fresh)
    public int selectionPoolSize;        // WALK CYCLE 53: the window's best third the parent was drawn from
    public bool parentInBestQuartile;
    public int parentRank = -1;
    public int poolSize;
    public float parentFitness = float.NaN;

    public float spawnTime;
    public float spawnX;
    public float[] genome = new float[ArenaGenome.Count];

    // measurement
    public bool fallen;
    public float fallTime = -1f;
    public float tiltAtFall;
    public float fallDistance = float.NaN;   // WALK CYCLE 53: the distance at the fall's own frame
    public float distanceAt15 = float.NaN;
    public bool uprightAt15;
    public float monotoneShare = float.NaN;  // WALK CYCLE 53: share of ring steps with x rising
    public float fitnessWindowSeconds = 15f; // set by the runner's measureSeconds in Bind
    public bool eligibleAtMeasure;           // WALK CYCLE 53: the pre-filter verdict, latched at measure
    public float distanceAtCull = float.NaN;
    public bool uprightAtCull;
    public bool aliveAtCull = true;
    public float destroyedTime = -1f;
    public float measuredTime = -1f;

    private Human human;
    private Rigidbody2D pelvis;
    private VestibularSystem vestibular;
    // WALK CYCLE 50: the body's own walk key (see OpenWalkKey below).
    private MotionIntent intent;
    private bool walkKeyOpen;
    private StepPhaseDriver stepDriver;   // the walk family the trial arms at its onset
    // WALK CYCLE 58: the window's own sources.
    private BalanceController balance;            // the plant clock (CaptureWalkStepCount)
    private BodyStateEstimator stateEstimator;    // the sole clearances (the lift clause)
    private bool windowFrozen;                    // the window's terms latched at fall/measure
    private int lastStepCounter = -1;
    private int walkStepsAccumulator;
    // C1: the shared criterion's latch and the SETTLED stand height it is measured
    // against. The running maximum that stood here climbed to the spawn pose.
    private FallCriterion.Latch fallLatch;
    private FallCriterion.Baseline standBaseline;
    // The stand height is read from this window of the human's own life: the
    // drop-in has settled by 0.25 s, and at 1.2 s the body is still standing.
    private const float BaselineWindowStart = 0.4f;
    private const float BaselineWindowEnd = 1.2f;
    private float lastTilt;
    private float lastPelvisY;
    private float nextTrajectoryTime;
    private int trajectoryCount;
    private readonly float[] trajectory = new float[TrajectorySlots * 3];
    private float simDelta = 0.005f;

    public float Distance => pelvis != null ? pelvis.position.x - spawnX : 0f;
    public float Tilt => lastTilt;
    public float PelvisY => lastPelvisY;
    public float Age(float simTime) => simTime - spawnTime;
    public Human Body => human;
    public Rigidbody2D PelvisBody => pelvis;

    // ─── WALK CYCLE 53: THE FITNESS THE LINE ITSELF JUDGES ────────────────
    // The old law measured a different episode: distance at 15 s minus a binary
    // fall penalty. It let a body that stood still beat a body that walked, and it
    // said nothing about WHEN the fall happened. The line's own event is
    //    fitness = distance_at_min(15, fallTime) + 0.4 * min(fallTime, 15)
    //              - 3.0 * [fallTime < 15]
    // i.e. metres actually covered inside the body's own life, a mild reward for
    // the seconds it stayed up, and a flat price for falling inside the window.
    //
    // WALK CYCLE 54: THE TWO NUMBERS MOVE (the c53 run's own census says why).
    // The 0.3 m floor admitted "standers with a nudge": the eligible window of
    // arena_fix1 (73 of the last 120 measured) held 3 bodies under 1.0 m and 4
    // under 1.5 m, and 0.3 m is no floor at all for a body whose fitness can be
    // bought by standing. The floor becomes 1.5 m: nothing that walked less than
    // 1.5 m in its 15 s window can be a parent. The 0.4 m/s stand reward paid
    // 0.4 * 15 = 6.0 m for standing the whole window - more than the walk's own
    // best row (5.3 m) - so a no-fall nudge (6.0-6.5) outranked every walker that
    // fell after ~12 s (3.0 + 4.8 - 3.0 = 4.8). At 0.1 m/s standing is worth 1.5 m
    // at most: the ranking is a distance again, the survival bonus is small.
    // The pre-filter stays a GATE (Eligible), never a weight.
    public const float FallPriceMeters = 3.0f;
    public const float StandRewardMetersPerSecond = 0.1f;
    // The pre-filter's two bars (the #675 lesson: the arena's own #1 walked
    // BACKWARDS -0.369 m on the line).
    public const float MinDistance15Meters = 1.5f;
    public const float MonotoneShareMin = 0.70f;
    public const int MonotoneMinSamples = 4;

    // ─── WALK CYCLE 58: THE LINE'S OWN BARS ────────────────────────────────
    // Yury's word (23.09): "почему их ходьба намного хуже чем у нашего эталонного
    // человека?" and «На видео люди просто стоят» - the field the arena ranked
    // top was a field of STANDERS (median 2.15 m per window, ~0.14 m/s of drift):
    // the c53/c54 law paid a body for the DISTANCE it covered and never asked
    // whether a walk happened at all. The rank becomes the line's own order:
    //
    //   THE WINDOW (Yury's onset rule, binding downstream): the quiet stand the
    //   protocol opens with (ArenaRunner.WalkOnsetSeconds = 5 s) is not gait and
    //   is not counted. Every term below is measured over
    //       W = [onset, min(fallOnset, onset + PostOnsetWindowSeconds)]
    //   with onset = spawnTime + WalkOnsetSeconds; the runner's measureSeconds
    //   was extended 15 -> 20 s so the POST-onset window is a full 15 s.
    //
    //   THE WALK CLASS (the line's census, a GATE - a discrete clause cannot be
    //   priced): steps >= WalkStepBar (the line's own `swaps >= 6`, counted by
    //   the same plant clock the trial records as captureWalkStepCount) AND
    //   swingLift >= SwingLiftBarMeters (the no-lift guard: the shuffle class
    //   re-plants its feet 45 times without ever clearing the floor) AND the
    //   anti-back filter (the window's monotone share >= MonotoneShareMin) AND
    //   window distance >= MinDistance15Meters (the c54 floor kept as the
    //   class's own minimum).
    //
    //   THE RANK inside the class (TIME-TO-FALL first, distance last):
    //       fitness = 1000 * windowSeconds + 1 * windowDistance
    //                 - 3.0 * [fell inside W]
    //   windowSeconds = the seconds of walking life after the onset (primary;
    //   0.001 s of it outranks every metre of the distance tiebreak), the c53
    //   fall price kept as the secondary at the distance scale. A body that
    //   never walks gets no distance credit at all (Yury: "a body whose motion
    //   never starts after the onset receives no distance credit"): its score
    //   is the registration residue StandResidueWeight * windowDistance, below
    //   every body of the class.
    //
    //   LawControl = 1 keeps the c54 shape over the SAME window for the round's
    //   own control run (the before/after bars share one measurement); the
    //   default is LawLine = 2.
    public const int WalkStepBar = 6;
    public const float SwingLiftBarMeters = 0.05f;   // the c58 no-lift guard (LawLine's own, kept)
    public const float PostOnsetWindowSeconds = 15f;
    public const float SurvivalWeight = 1000f;
    public const float DistanceTiebreakWeight = 1f;
    public const float StandResidueWeight = 0.001f;
    public const int LawControl = 1;
    public const int LawLine = 2;
    // WALK CYCLE 61: THE LIFT'S QUALITY - the census lift clause becomes a QUALITY BAND.
    // The c58 clause was a plain FLOOR (0.05 m, a no-lift detector): a body could clear it
    // with the shuffle-step the eye reads as standing, and the filed field's own best sits
    // at 0.099 m while the stand-shufflers' soles never leave ~0.02 m. The band has two
    // parts, both MEASURED from the recorded rows (no genome value is read):
    //   the floor   swingLift >= 0.08 m - the accepted line's own weaker leg (c6w57's row:
    //               0.0819 m over [5,20) s; c6w60's row 0.0921; c6w56's 0.1162), i.e. the
    //               amplitude the line itself walks on, asked of every body;
    //   the shape   swingClearShare >= 0.15 - of the window's own step samples, each foot
    //               must spend at least 15 % CLEAR of the shuffle ceiling (0.02 m) - the
    //               time the foot is off the floor, not a one-frame spike. The accepted
    //               line reads 0.25/0.29 (c6w57), 0.27/0.29 (c6w56), 0.34/0.34 (c6w60);
    //               a stand-shuffler that never lifts reads ~0.00-0.02.
    // Both numbers come from the accepted line's own lift signature, measured at this
    // round's open over the post-onset window; the knobs (-arenaLiftFloor, -arenaClearShare)
    // exist for the round's own sensitivity reads, the defaults above are the landed band.
    public const int LawQuality = 3;
    public const float SweepLiftFloorMeters = 0.08f;
    public const float SweepClearCeilingMeters = 0.02f;
    public const float SweepClearShareBar = 0.15f;
    public float liftFloorMeters = SweepLiftFloorMeters;   // -arenaLiftFloor (the round's knob)
    public float clearShareBar = SweepClearShareBar;       // -arenaClearShare (the round's knob)
    public int lawMode = LawQuality;

    // WALK CYCLE 58: the window's own measured quantities (all quotable from
    // the run's columns; the append-only influence log's schema is frozen, so
    // these live in the per-run CSV and the summary JSON).
    public float onsetTime = -1f;         // spawnTime + WalkOnsetSeconds, latched by OpenWalkKey
    public float onsetDistance;           // Distance at the onset - the window's own zero
    public int onsetStepCount;            // the plant clock at the onset
    public int walkSteps;                 // steps that fired inside W (reset-tolerant)
    public float swingLiftL;              // the L foot's peak sole clearance inside W
    public float swingLiftR;              // the R foot's peak sole clearance inside W
    public float swingLift;               // min(L, R): BOTH feet had to clear the floor
    public float swingClearShareL;        // WALK CYCLE 61: of W's samples, the share the L foot
    public float swingClearShareR;        //   spent CLEAR of the shuffle band (0.02 m)
    public float swingClearShare;         // min(L, R): the weaker foot's own share
    private int clearSamples;             // the window's own sample clock (both feet share it)
    private int clearAccumL, clearAccumR;
    public float windowDistance;          // metres covered inside W
    public float windowSeconds;           // the walking seconds inside W, capped at 15
    public float monotoneShareWindow = float.NaN;  // the anti-back filter's own share (inside W)
    public bool onWalkCensus;             // steps AND lift clauses passed
    public bool forwardWalk;              // the anti-back filter's verdict

    public float Fitness
    {
        get
        {
            bool fellInside = fallTime >= 0f && onsetTime >= 0f
                && fallTime < onsetTime + PostOnsetWindowSeconds - 1e-6f;
            if (lawMode == LawControl)
            {
                // The c54 shape, on this window (the round's control run only).
                return windowDistance + StandRewardMetersPerSecond * windowSeconds
                    - (fellInside ? FallPriceMeters : 0f);
            }
            if (!onWalkCensus || !forwardWalk)
                return StandResidueWeight * windowDistance;
            return SurvivalWeight * windowSeconds + DistanceTiebreakWeight * windowDistance
                - (fellInside ? FallPriceMeters : 0f);
        }
    }

    // The pre-filter's verdict, taken at measure time. The walk class's own
    // gate: ineligible bodies never enter the selection pool (the c58 census
    // replaced the c53/c54 bars; LawControl restores the old pair for the
    // control run).
    public bool Eligible
    {
        get
        {
            if (lawMode == LawControl)
            {
                if (float.IsNaN(distanceAt15) || distanceAt15 < MinDistance15Meters) return false;
                if (trajectoryCount >= MonotoneMinSamples
                    && (float.IsNaN(monotoneShare) || monotoneShare < MonotoneShareMin)) return false;
                return true;
            }
            if (!onWalkCensus || !forwardWalk) return false;
            if (windowDistance < MinDistance15Meters) return false;
            return true;
        }
    }

    public void Bind(Human owner, float spawnTimeSeconds, float spawnXMeters, float delta,
        float measureWindowSeconds = 15f)
    {
        human = owner;
        spawnTime = spawnTimeSeconds;
        spawnX = spawnXMeters;
        simDelta = delta > 0f ? delta : 0.005f;
        fitnessWindowSeconds = measureWindowSeconds > 0f ? measureWindowSeconds : 15f;
        vestibular = owner != null ? owner.vestibularSystem : null;
        intent = owner != null ? owner.GetComponent<MotionIntent>() : null;
        stepDriver = owner != null ? owner.GetComponent<StepPhaseDriver>() : null;
        // WALK CYCLE 58: the window's own sources - the plant clock and the sole
        // clearances. Both are the standard rig's own components (the same numbers
        // the trial rows carry as captureWalkStepCount / soleClear*).
        balance = owner != null ? owner.GetComponent<BalanceController>() : null;
        stateEstimator = owner != null ? owner.GetComponent<BodyStateEstimator>() : null;
        if (owner != null)
        {
            Transform body = owner.transform.Find("Pelvis");
            if (body != null) pelvis = body.GetComponent<Rigidbody2D>();
        }
        nextTrajectoryTime = spawnTime;
    }

    // ─── WALK CYCLE 50: THE WALK KEY ───────────────────────────────────────
    // The arena spawns a body and, until this cycle, commanded it to walk on that
    // same frame. The accepted walk is verified from a settled quiet stand - the
    // trial opens the key at -walkTime 5 s - and a key opened earlier never reaches
    // the walk at all: with no foot down the pose drive only runs its stand pose,
    // so the body topples and every walk switch of the accepted line stays inert
    // (measured: the accepted line with -walkTime 0 collapses in 1.47 s, the same
    // line and genome with -walkTime 5 walks 3.9 m). The key is opened ONCE per
    // body, by ArenaRunner, when the body's own stand window is over, and it stays
    // open for the rest of that body's life - the arena's bodies have no
    // walkDuration: their whole measured life is the walk.
    //
    // Arming the step driver is part of the same act, because that is what the
    // trial does at its own onset: HeadlessTrial.UpdateWalkIntent calls
    // BeginWalk(walkTime) when the window opens, then Tick(time) on every step of
    // it. StepPhaseDriver.walkActive is the bit a LIVE walk term reads - ArmDrive's
    // hipSpeedHold target is gated on it (ArmDrive.cs), as are the capture walker's
    // own guards - so a body that never arms the driver walks with that term inert
    // and cannot behave like the trial's. The driver writes no joint of its own
    // (Tick publishes gait phase and standLeg only), so the pose drive stays the
    // single joint writer.
    public bool WalkKeyOpen => walkKeyOpen;

    public void OpenWalkKey(float walkStartTime)
    {
        if (walkKeyOpen) return;
        walkKeyOpen = true;
        // WALK CYCLE 58: the onset is the window's own zero (Yury's onset rule).
        onsetTime = walkStartTime;
        onsetDistance = Distance;
        onsetStepCount = StepCounter;
        lastStepCounter = onsetStepCount;
        if (intent != null) intent.moveX = 1f;
        if (stepDriver != null) stepDriver.BeginWalk(walkStartTime);
    }

    // WALK CYCLE 58: the plant clock, the SAME rule TrialRecorder uses for its
    // walkSwapCount column - the capture walker's own count while it is the walk,
    // the step driver's swaps otherwise.
    private int StepCounter
    {
        get
        {
            if (balance != null && balance.useCaptureWalker != 0 && stepDriver != null
                && stepDriver.walkActive)
                return balance.CaptureWalkStepCount;
            return stepDriver != null ? stepDriver.SwapCount : 0;
        }
    }

    // WALK CYCLE 58: the window's terms are latched ONCE, at the earlier of the
    // fall and the measure mark - after that the body is out of its own episode
    // and nothing it does may move its score.
    private void FreezeWindow(float endTime)
    {
        if (windowFrozen) return;
        windowFrozen = true;
        float seconds = onsetTime >= 0f ? endTime - onsetTime : 0f;
        windowSeconds = Mathf.Clamp(seconds, 0f, PostOnsetWindowSeconds);
        windowDistance = onsetTime >= 0f ? Distance - onsetDistance : 0f;
        walkSteps = walkStepsAccumulator;
        swingLift = Mathf.Min(swingLiftL, swingLiftR);
        // WALK CYCLE 61: the shape half of the band, latched with the peaks.
        swingClearShareL = clearSamples > 0 ? clearAccumL / (float)clearSamples : float.NaN;
        swingClearShareR = clearSamples > 0 ? clearAccumR / (float)clearSamples : float.NaN;
        swingClearShare = clearSamples > 0
            ? Mathf.Min(swingClearShareL, swingClearShareR)
            : float.NaN;
        bool liftClause;
        if (lawMode == LawQuality)
        {
            // WALK CYCLE 61: the band - the floor AND the shape, per foot, both MEASURED.
            liftClause = swingLift >= liftFloorMeters
                && swingClearShareL >= clearShareBar && swingClearShareR >= clearShareBar;
        }
        else
        {
            liftClause = swingLift >= SwingLiftBarMeters;   // WALK CYCLE 58's no-lift guard
        }
        onWalkCensus = walkSteps >= WalkStepBar && liftClause;
        monotoneShareWindow = ComputeMonotoneShareWindow(endTime);
        forwardWalk = !(WindowSamples(endTime) >= MonotoneMinSamples
            && (float.IsNaN(monotoneShareWindow) || monotoneShareWindow < MonotoneShareMin));
    }

    private int WindowSamples(float endTime)
    {
        if (onsetTime < 0f) return 0;
        int n = 0;
        for (int i = 0; i < trajectoryCount; i++)
        {
            float age = trajectory[i * 3];
            if (age + 1e-6f < onsetTime - spawnTime) continue;
            if (age > endTime - spawnTime + 1e-6f) break;
            n++;
        }
        return n;
    }

    // The anti-back filter's own share: of the ring's consecutive steps INSIDE the
    // window, the fraction whose x rose. The c53 whole-ring share is kept beside it
    // for the older readers.
    private float ComputeMonotoneShareWindow(float endTime)
    {
        if (trajectoryCount < 2 || onsetTime < 0f) return float.NaN;
        int rises = 0, steps = 0;
        for (int i = 1; i < trajectoryCount; i++)
        {
            float age = trajectory[i * 3];
            float prevAge = trajectory[(i - 1) * 3];
            if (age > endTime - spawnTime + 1e-6f) break;
            if (prevAge + 1e-6f < onsetTime - spawnTime) continue;
            if (trajectory[i * 3 + 1] > trajectory[(i - 1) * 3 + 1]) rises++;
            steps++;
        }
        if (steps == 0) return float.NaN;
        return rises / (float)steps;
    }

    // One step of the armed driver, the trial's own Tick(time) while the window is
    // open. Inert before OpenWalkKey (the driver's walkActive is false).
    public void TickWalkDrive(float simTime)
    {
        if (!walkKeyOpen || stepDriver == null) return;
        stepDriver.Tick(simTime);
    }

    // One fixed step of measurement. Called by ArenaRunner, never by physics.
    public void Step(float simTime)
    {
        if (pelvis == null) return;

        CalibrateNumber(simTime);

        lastTilt = Mathf.Abs(vestibular != null ? vestibular.GetBodyTilt() : 0f);
        lastPelvisY = pelvis.position.y;

        // WALK CYCLE 58: the window's live terms, sampled every step from the onset
        // (the quiet stand is not gait): the sole clearances (the lift clause) and
        // the plant clock (the steps clause). Frozen at the earlier of the fall and
        // the measure mark.
        if (walkKeyOpen && !windowFrozen)
        {
            if (stateEstimator != null)
            {
                float clearL = stateEstimator.leftFootGroundClearance;
                float clearR = stateEstimator.rightFootGroundClearance;
                if (clearL > swingLiftL) swingLiftL = clearL;
                if (clearR > swingLiftR) swingLiftR = clearR;
                // WALK CYCLE 61: the shape half of the band - the time each foot spends
                // clear of the shuffle ceiling, on the same sample clock as the peaks.
                clearSamples++;
                if (clearL > SweepClearCeilingMeters) clearAccumL++;
                if (clearR > SweepClearCeilingMeters) clearAccumR++;
            }
            int stepNow = StepCounter;
            // Count only plausible increments (a swap takes tens of frames, so a
            // single step can move the counter by at most 1); a counter that RESET
            // or a source switch just re-bases - the census must not be bought by a
            // jump in the clock.
            if (lastStepCounter >= 0 && stepNow > lastStepCounter && stepNow - lastStepCounter <= 2)
                walkStepsAccumulator += stepNow - lastStepCounter;
            lastStepCounter = stepNow;
        }

        // The settled stand height: measured over the first second of this
        // human's life once the drop-in has settled, then frozen. The running
        // maximum that stood here was the spawn pose - 0.186 m above the settled
        // stance - and 0.064 m of ordinary sink then latched the flag.
        float age = simTime - spawnTime;
        if (age >= BaselineWindowStart && age < BaselineWindowEnd)
            standBaseline.Feed(lastPelvisY, lastTilt);
        else if (age >= BaselineWindowEnd)
            standBaseline.Freeze();
        float standHeight = standBaseline.valid
            ? standBaseline.height
            : float.NegativeInfinity;
        // No measured stand height disables the drop arm; the tilt arm still
        // applies. The spawn pose is never used as a bar.
        bool downNow = FallCriterion.Step(ref fallLatch, standHeight, lastPelvisY,
            lastTilt, simDelta, simTime);

        if (!fallen && fallLatch.fired)
        {
            fallen = true;
            fallTime = fallLatch.fireTime;
            tiltAtFall = lastTilt;
            // WALK CYCLE 53: the fitness needs the distance AT the fall, not at the
            // measure deadline - the fall ends the episode the line judges.
            fallDistance = Distance;
            // WALK CYCLE 58: the fall ends the WINDOW too - freeze its terms here so
            // nothing after the fall can move the score.
            FreezeWindow(fallTime);
        }

        if (trajectoryCount < TrajectorySlots && simTime >= nextTrajectoryTime)
        {
            nextTrajectoryTime += TrajectoryPeriod;
            trajectory[trajectoryCount * 3] = simTime - spawnTime;
            trajectory[trajectoryCount * 3 + 1] = pelvis.position.x - spawnX;
            trajectory[trajectoryCount * 3 + 2] = pelvis.position.y;
            trajectoryCount++;
        }
    }

    // The digit is measured, not assumed: whatever the first frame drew is read
    // back from the rendered mesh, corrected once against the head-relative
    // target, and verified on the following step. The log carries head height,
    // target, drawn height and whether the glyph sits inside the head's own box,
    // so a frame can be checked without an eye.
    private void CalibrateNumber(float simTime)
    {
        if (numberCheckStage >= 2) return;
        if (numberMesh == null)
        {
            ArenaNumberTarget intent = GetComponentInChildren<ArenaNumberTarget>();
            if (intent == null)
            {
                // No number on this human (older path) - nothing to measure.
                if (simTime - spawnTime > 0.5f) numberCheckStage = 2;
                return;
            }
            numberMesh = intent.GetComponent<TextMesh>();
            numberTargetHeight = intent.targetHeightMeters;
            numberHeadHeight = intent.headHeightMeters;
            if (numberMesh == null) { numberCheckStage = 2; return; }
        }

        MeshRenderer rendered = numberMesh.GetComponent<MeshRenderer>();
        float drawn = rendered != null ? rendered.bounds.size.y : 0f;
        if (drawn <= 1e-5f)
        {
            // -nographics has no renderer to read: the analytic value stands, and
            // the log says so instead of pretending it was measured.
            if (simTime - spawnTime > 0.5f && numberCheckStage == 0)
            {
                numberCheckStage = 2;
                ReportNumber(numberTargetHeight, false, false);
            }
            return;
        }

        if (numberCheckStage == 0)
        {
            numberCheckStage = 1;
            if (Mathf.Abs(drawn - numberTargetHeight) > 0.002f)
                numberMesh.characterSize *= numberTargetHeight / drawn;
            return;
        }

        numberCheckStage = 2;
        ReportNumber(drawn, true, NumberInsideHead(numberMesh));
    }

    private void ReportNumber(float drawn, bool measured, bool inside)
    {
        NumberLastHeadHeightMeters = numberHeadHeight;
        NumberLastTargetHeightMeters = numberTargetHeight;
        NumberLastDrawnHeightMeters = drawn;
        NumberLastInsideHead = inside;
        if (NumberLogged >= NumberLogLimit) return;
        NumberLogged++;
        Debug.Log(string.Format(CultureInfo.InvariantCulture,
            "ARENA-NUMBER id={0} head={1:0.000}m target={2:0.000}m drawn={3:0.000}m "
            + "ratio={4:0.00} measured={5} insideHead={6} ceiling={7:0.00}x head",
            id, numberHeadHeight, numberTargetHeight, drawn,
            numberHeadHeight > 1e-4f ? drawn / numberHeadHeight : 0f, measured, inside,
            NumberMaxFractionOfHead));
    }

    private static bool NumberInsideHead(TextMesh mesh)
    {
        if (mesh == null) return false;
        MeshRenderer rendered = mesh.GetComponent<MeshRenderer>();
        Collider2D headBox = mesh.transform.parent != null
            ? mesh.transform.parent.GetComponent<Collider2D>()
            : null;
        if (rendered == null || headBox == null) return false;
        Bounds head = headBox.bounds;
        Bounds digit = rendered.bounds;
        return digit.min.x >= head.min.x - 1e-4f && digit.max.x <= head.max.x + 1e-4f
            && digit.min.y >= head.min.y - 1e-4f && digit.max.y <= head.max.y + 1e-4f;
    }

    public bool UprightNow()
    {
        return !fallen
            && lastTilt <= FallTiltDegrees
            && (!standBaseline.valid
                || lastPelvisY >= standBaseline.height - FallDropMeters);
    }

    public void MarkMeasured(float simTime)
    {
        measuredTime = simTime;
        distanceAt15 = Distance;
        uprightAt15 = UprightNow();
        monotoneShare = ComputeMonotoneShare();
        // WALK CYCLE 58: the measure mark ends the window for a body that never
        // fell; the terms latch here exactly as they do at a fall.
        FreezeWindow(simTime);
        eligibleAtMeasure = Eligible;
    }

    // WALK CYCLE 53: the share of the ring's consecutive steps whose x ROSE. The
    // arena's own #1 genome (#675) walked backwards -0.369 m on the line, and this
    // ring was already on disk when it did: the pre-filter only had to read it.
    private float ComputeMonotoneShare()
    {
        if (trajectoryCount < 2) return float.NaN;
        int rises = 0;
        for (int i = 1; i < trajectoryCount; i++)
            if (trajectory[i * 3 + 1] > trajectory[(i - 1) * 3 + 1]) rises++;
        return rises / (float)(trajectoryCount - 1);
    }

    public void MarkCulled(float simTime, bool upright)
    {
        destroyedTime = simTime;
        distanceAtCull = Distance;
        uprightAtCull = upright;
        aliveAtCull = false;
    }

    public string TrajectoryJson()
    {
        StringBuilder sb = new StringBuilder(256);
        sb.Append('[');
        for (int i = 0; i < trajectoryCount; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append("{\"t\":");
            sb.Append(trajectory[i * 3].ToString("0.###", CultureInfo.InvariantCulture));
            sb.Append(",\"x\":");
            sb.Append(trajectory[i * 3 + 1].ToString("0.#####", CultureInfo.InvariantCulture));
            sb.Append(",\"y\":");
            sb.Append(trajectory[i * 3 + 2].ToString("0.#####", CultureInfo.InvariantCulture));
            sb.Append('}');
        }
        sb.Append(']');
        return sb.ToString();
    }

    // Distance along the path at a requested age (used to compare the two elite
    // copies at the same age, not at the same wall time).
    public bool TrajectoryAt(float age, out float x, out float y)
    {
        x = 0f;
        y = 0f;
        if (trajectoryCount == 0) return false;
        int best = 0;
        float bestError = float.MaxValue;
        for (int i = 0; i < trajectoryCount; i++)
        {
            float error = Mathf.Abs(trajectory[i * 3] - age);
            if (error < bestError)
            {
                bestError = error;
                best = i;
            }
        }
        x = trajectory[best * 3 + 1];
        y = trajectory[best * 3 + 2];
        return bestError <= 0.13f;
    }

    public int TrajectorySamples => trajectoryCount;

    // ─── the number on the head ────────────────────────────────────────────
    // Digits above the head, one TextMesh per human: no collider, no rigidbody, no
    // effect on physics. Readable at 1x - verified by looking at a frame, not by
    // reading this code.
    private static Font labelFont;
    private static bool labelFontResolved;

    public static Font LabelFont()
    {
        if (!labelFontResolved)
        {
            labelFontResolved = true;
            labelFont = LoadBuiltinFont("LegacyRuntime.ttf");
            if (labelFont == null)
            {
                labelFont = LoadBuiltinFont("Arial.ttf");
                if (labelFont != null)
                    Debug.LogWarning("ARENA_LABEL_FONT fallback Arial.ttf");
            }
            else
            {
                Debug.Log("ARENA_LABEL_FONT LegacyRuntime.ttf");
            }
            if (labelFont == null)
                Debug.LogError("ARENA_LABEL_FONT none - head numbers will not render");
        }
        return labelFont;
    }

    private static Font LoadBuiltinFont(string name)
    {
        try
        {
            return Resources.GetBuiltinResource<Font>(name);
        }
        catch (System.Exception)
        {
            return null;
        }
    }

    // heightMeters is the height of the digit line in world metres. TextMesh maps
    // characterSize * fontSize / 10 to that height for the built-in font, so the
    // character size follows from the requested height.
    public static TextMesh AttachNumber(GameObject humanObject, int number, float heightMeters)
    {
        if (humanObject == null) return null;
        Transform head = humanObject.transform.Find("Head");
        if (head == null) return null;

        // The digit is measured against the head's OWN box, not against the body
        // height, and the requested size is a ceiling: the head is about 0.22 m on
        // this rig, so the requested 0.34 m has to be clamped or the number spills
        // over the skull (the 18.08 defect, found in the frame).
        float headHeight = HeadHeightMeters(head, heightMeters);
        float requested = Mathf.Max(0.005f, heightMeters);
        float target = Mathf.Min(requested, headHeight * NumberHeadHeightFraction);

        GameObject label = new GameObject("ArenaNumber");
        label.layer = humanObject.layer;
        label.transform.SetParent(head, false);
        label.transform.localPosition = new Vector3(0f, 0f, -0.1f);   // centred in the head
        label.transform.localRotation = Quaternion.identity;

        TextMesh textMesh = label.AddComponent<TextMesh>();
        Font font = LabelFont();
        if (font != null) textMesh.font = font;
        textMesh.text = number.ToString(CultureInfo.InvariantCulture);
        textMesh.fontSize = 72;
        // First guess from the identity the earlier cycle used (world height =
        // characterSize * fontSize / 10); the agent measures the drawn mesh and
        // corrects it on the next step, so the guess is never the claim.
        textMesh.characterSize = Mathf.Max(0.005f, target * 10f / textMesh.fontSize);
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.alignment = TextAlignment.Center;
        textMesh.color = new Color(0f, 0f, 0f, 1f);

        MeshRenderer renderer = label.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            renderer.sortingOrder = 200;
            if (font != null && font.material != null)
                renderer.sharedMaterial = font.material;
        }

        ArenaNumberTarget intent = label.AddComponent<ArenaNumberTarget>();
        intent.id = number;
        intent.headHeightMeters = headHeight;
        intent.requestedHeightMeters = requested;
        intent.targetHeightMeters = target;
        NumberSpawned++;
        return textMesh;
    }

    // The head's own collider box (world AABB). Fallback: a human head is about an
    // eighth of the body - used only when the rig has no collider on the head.
    public static float HeadHeightMeters(Transform head, float bodyHeightMeters)
    {
        Collider2D box = head != null ? head.GetComponent<Collider2D>() : null;
        if (box != null && box.bounds.size.y > 1e-4f)
            return box.bounds.size.y;
        return Mathf.Max(0.05f, bodyHeightMeters * 0.125f);
    }

    // Carries the digit's intent from AttachNumber (called by ArenaRunner before
    // the agent exists) to the agent's own first measurement - its own top-level
    // file, because Unity will not add a nested class as a component.
}
