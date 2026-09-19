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

        rows.AppendLine("t,chestPitch,pelvisTilt,lumbar,chestY,pelvisY,comX,comY,comOffset,frG,flG,rrG,rlG,sat,state,leapCount,jawAngle,paws,down,fell,upSuccess,hipL,hipR,kneeL,kneeR,shoL,shoR,elbL,elbR,trunkBar,push,trunkLift,capturePointMargin,pawNormalImpulseFR,pawNormalImpulseFL,pawNormalImpulseRR,pawNormalImpulseRL,sumFnOverWeight,controllerTrunkDrop,captureXi,captureHeight,captureMargin");
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
        float barGap = Mathf.Abs(controllerDropNow - trunkDropNow);
        if (barGap > controllerVsRecorderMax)
        {
            controllerVsRecorderMax = barGap;
            controllerVsRecorderMaxFrame = samples;
        }

        // NaN watch on every new column of every frame, so "no NaN" is a number a
        // checker can read and not an eyeball.
        if (!IsFinite(trunkLift)) cycle1NanCount++;
        if (!IsFinite(marginNow)) cycle1NanCount++;
        for (int paw = 0; paw < 4; paw++)
        {
            if (!IsFinite(stance.pawNormalImpulse[paw])) cycle1NanCount++;
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
            walkEndComX = stance.comPosition.x;
            walkEndTime = time;
            samplesWalkActive++;
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
        float walkWindow = walkWindowOpen && walkEndTime > walkStartTime
            ? walkEndTime - walkStartTime
            : 0f;
        float travelX = walkWindowOpen ? walkEndComX - walkStartComX : 0f;
        float speedX = walkWindow > 0.01f ? travelX / walkWindow : 0f;
        AppendNum(json, "comTravelX", travelX);
        AppendNum(json, "comSpeedX", speedX);
        AppendNum(json, "comSpeedKmh", speedX * 3.6f);
        AppendNum(json, "walkWindowSeconds", walkWindow);
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
        AppendApplied(json);
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
}
