using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

// ─── TILT-PLATFORM STAND: THE PER-STEP LOG (lane wiring, 29.09) ───
//
// The owner's stand (refs/tilt-platform-20260928.jpg; TiltPlatform.cs drives the deck):
// the platform rocks about its central axis, +1 deg, -1 deg, +2, -2, ... and the man
// must stay standing. This is the harness half of the stand: ONE row per step (the
// +n and -n side together), carrying what the shared fall criterion saw and which of
// the six balance channels moved while that step was held:
//
//   ankle  - the CoM offset the ankles work against (the CoP proxy of the stand)
//   hip    - the hip joints' own travel (the fore/aft lean)
//   knee   - the knees' travel (the knee strategy: lower the CoM, buy time)
//   arms   - the shoulders' travel (the fast balance organ)
//   stance - the base the take was GIVEN (-stanceWidth; a geometry, reported per row)
//   step   - a foot leaving the deck (the catch-step; the last resort)
//
// Read-only instrumentation: it samples quantities the rig already publishes
// (BodyStateEstimator, CenterOfMassCalculator, VestibularSystem, the arm joints) and
// never writes a force. It exists only when the take passed -tiltPlatform 1, so every
// other row keeps its bytes. The fall is the criterion's own (TrialRecorder reads
// FallCriterion.cs; this log reads the recorder's fall time and never re-decides it).
//
// The step schedule mirrors TiltPlatform.FixedUpdate (settle, then move+hold per side).
// The mirror is checked against the drive every frame: when a row opens, the deck's own
// CurrentStep must agree, or a warning is logged - a silent drift between the log and
// the deck would mislabel every number in the report.
public sealed class TiltStandLog
{
    // A channel counts as "moved" past these travels inside one step. Modest on
    // purpose: the mask is a pointer for the report, not a measurement.
    public const float KneeChannelDeg = 3f;
    public const float HipChannelDeg = 2f;
    public const float AnkleChannelDeg = 2f;
    public const float ArmChannelDeg = 5f;
    public const float AnkleComOffsetM = 0.02f;
    public const int AirChannelFrames = 3;
    // The take ends this long after the fall onset, so the film keeps the fall
    // itself; the ramp's own end is the other end of the take.
    public const float EndAfterFallSeconds = 2.5f;

    private struct StepRow
    {
        public int step;
        public float angle;
        public float tStart;
        public float tEnd;
        public bool complete;
        public bool fell;
        public bool held;
        public float fellAt;
        public float knee;
        public float hip;
        public float ankle;
        public float arm;
        public float comOffset;
        public float margin;
        public float tiltEnd;
        public int air;
        public float sepStart;
        public float sepEnd;
        public int stanceSteps;
        public int braceFires;
        public int braceContactFrames;
        public float braceFirstContactT;
        public int braceSuppressed;
        public float braceFirstFireT;
        public float reflexUnwindT;
        public float reflexUnwindDoneT;
        public float reflexUnstepT;
        public float reflexUnstepDoneT;
        public float reflexHandT;              // round 11: the plant hand-over flip
        public int riseAttempts;               // round 13: get-up attempts opened inside the row
        public string poseEnd;                 // round 17: the pose at the step's close
        public string poseFall;                // the pose the fall criterion fired from
        public bool fellBack;                  // the fall fired from a back-lying pose
        public int risePoseMax;
        public float riseGateHeld;             // round 30: the contact gate held seconds (row max)
        public int riseGateHolds;
        public float riseBaseWait;             // round 31: the base gate wait seconds (row max)
        public int riseBaseAborts;             // round 31: the cap aborts, this run              // round 30: the gate's hold episodes (row max)                // round 13: the deepest get-up stage in the row (1..4)              // round 11: the plant hand-over flip
        public float riseBaseSettle;           // round 32: the settle dwell (row max)
        public int riseBaseSettleBlocks;       // round 32: the refused fires, this run
        public float riseBaseLoad;             // round 35: the load gate (row max)
        public int riseBaseLoadBlocks;         // round 35: the refused fires, this run
        public int riseArbSuppL;               // round 37: the hip arbitration (row snapshot)
        public int riseArbSuppR;
        public float riseMoveGuardHeld;        // round 42: the move guard (row max)
        public int riseMoveGuardHolds;
        public float riseGovHeld;              // round 42: the trunk governor (row max)
        public int riseGovHolds;
        public float riseGovBias;              // round 42: the largest counter-lean applied
    }

    private readonly TiltPlatform platform;
    private readonly TrialRecorder recorder;
    private readonly BodyStateEstimator state;
    private readonly CenterOfMassCalculator com;
    private readonly VestibularSystem vestibular;
    private readonly IntuitiveStance stance;   // the man's own stance layer (may be absent)
    private readonly BraceReflex brace;        // the owner's brace reflex (may be absent)
    private readonly FallReflex reflex;        // the fall reflex + its unwind (may be absent)
    private readonly PoseOrientation pose;     // round 17: the self-orientation core (may be absent)
    private readonly BalanceController balance; // round 13: the get-up practice (rise columns)
    private readonly bool getUpMode;            // round 13: the take does not end at the fall
    private readonly HingeJoint2D leftArmUpper;
    private readonly HingeJoint2D rightArmUpper;
    private readonly ImpactCaption caption;
    private readonly float baseMeters;
    // ROUND 15 (the plant-phase ejection instrument, -tiltPen 1): the planted palm's
    // own bottom face against the deck's top face under it, per step, for each hand.
    private readonly ArmDrive arm;
    private readonly Transform handL;
    private readonly Transform handR;
    private readonly Collider2D handColL;
    private readonly Collider2D handColR;
    public bool penDebug;
    private float penNext;

    private readonly List<StepRow> rows = new List<StepRow>(16);

    // The open row's accumulators.
    private int openStep;
    private float rowStart;
    private float rowKnee0L, rowKnee0R, rowHip0L, rowHip0R;
    private float rowAnkle0L, rowAnkle0R, rowArm0L, rowArm0R;
    private float rowKnee, rowHip, rowAnkle, rowArm, rowComOffset, rowMargin, rowTiltEnd;
    private int rowAir;
    private float rowSepStart;
    private int rowStance0;
    private int rowBraceFire0;
    private int rowBraceContact0;
    private int rowBraceSupp0;
    private bool rowFell;
    private float rowFellAt = -1f;
    private int rowRise0;
    private string rowPoseFall = "-";
    private bool rowFellBack;
    private string poseNow = "-";
    private int rowRisePoseMax;
    private float rowRiseGateHeld;      // round 30: the contact gate (row max)
    private int rowRiseGateHolds;
    private float takeGateHeldMax;      // round 30: the contact gate (take max)
    private int takeGateHoldsMax;
    private float rowRiseBaseWait;      // round 31: the base gate (row max)
    private int rowRiseBaseAborts;
    private float takeBaseWaitMax;      // round 31: the base gate (take max)
    private int takeBaseAbortsMax;
    private float rowRiseBaseSettle;    // round 32: the settle gate (row max)
    private int rowRiseBaseSettleBlocks;
    private float takeBaseSettleMax;    // round 32: the settle gate (take max)
    private int takeBaseSettleBlocksMax;
    private float rowRiseBaseLoad;      // round 35: the load gate (row max)
    private int rowRiseBaseLoadBlocks;
    private float takeBaseLoadMax;      // round 35: the load gate (take max)
    private int takeBaseLoadBlocksMax;
    private int rowRiseArbSuppL;        // round 37: the hip arbitration (row snapshot)
    private int rowRiseArbSuppR;
    private int takeArbSuppLMax;        // round 37: the hip arbitration (take max)
    private int takeArbSuppRMax;
    private float rowRiseMoveGuardHeld; // round 42: the move guard (row max)
    private int rowRiseMoveGuardHolds;
    private float rowRiseGovHeld;       // round 42: the trunk governor (row max)
    private int rowRiseGovHolds;
    private float rowRiseGovBias;
    private float takeMoveGuardHeldMax; // round 42 (take max)
    private int takeMoveGuardHoldsMax;
    private float takeGovHeldMax;
    private int takeGovHoldsMax;
    private float takeGovBiasMax;
    private Transform humanT;           // round 35: the void end's own body
    private Transform pelvisT;          // round 36: the void end reads the PELVIS body
    private float voidSince = -1f;      // round 35: seconds below the deck
    // ROUND 35: -tiltVoidEnd 1 - a practice take ends once the body has been below
    // the decks for 0.5 s (it left them; the void is not a practice).
    public bool voidEndOn;
    public float VoidEndTime { get; private set; } = -1f;

    private int scheduledStep;
    private float fallTime = -1f;
    private bool endRequested;

    public bool WantsEnd
    {
        get { return endRequested; }
    }

    public float HeldDeg { get; private set; }
    public int FellStep { get; private set; }
    public float FellAt { get; private set; }
    public string FellPose { get; private set; } = "-";   // round 17: the pose at the fall
    public bool FellBack { get; private set; }
    public int StepCount
    {
        get { return rows.Count; }
    }

    public TiltStandLog(TiltPlatform platform, Human human, TrialRecorder recorder, bool visual,
        bool getUpMode)
    {
        this.platform = platform;
        this.recorder = recorder;
        if (human != null)
        {
            state = human.GetComponent<BodyStateEstimator>();
            com = human.GetComponent<CenterOfMassCalculator>();
            vestibular = human.GetComponent<VestibularSystem>();
            stance = human.GetComponent<IntuitiveStance>();
            brace = human.GetComponent<BraceReflex>();
            reflex = human.GetComponent<FallReflex>();
            pose = human.GetComponent<PoseOrientation>();
            balance = human.GetComponent<BalanceController>();   // round 13
            this.getUpMode = getUpMode;                          // round 13
            humanT = human.transform;                            // round 35: the void end
            pelvisT = human.transform.Find("Pelvis");            // round 36: the void end body
            baseMeters = human.hipHalfSpacing * 2f;
            arm = human.GetComponent<ArmDrive>();
            handL = human.transform.Find("LeftArmHand");
            handR = human.transform.Find("RightArmHand");
            handColL = handL != null ? handL.GetComponent<Collider2D>() : null;
            handColR = handR != null ? handR.GetComponent<Collider2D>() : null;
            Transform armL = human.transform.Find("LeftArmUpper");
            Transform armR = human.transform.Find("RightArmUpper");
            leftArmUpper = armL != null ? armL.GetComponent<HingeJoint2D>() : null;
            rightArmUpper = armR != null ? armR.GetComponent<HingeJoint2D>() : null;
        }

        // Picture only (ImpactCaption: TextMesh + SpriteRenderer, no collider, no
        // Rigidbody2D), and only on the visual take - the -nographics runs have no
        // camera and the caption's own Create warns and skips without one.
        if (visual)
        {
            caption = ImpactCaption.Create(
                "TILT STAND (refs/tilt-platform-20260928.jpg)  deck "
                    + F(platform.deckLength) + " m central pivot  base " + F(baseMeters) + " m",
                "tilt-stand");
        }
    }

    // Once per executed physics step of the human trial, right after recorder.Sample.
    public void Sample(float time)
    {
        float move = Mathf.Max(0.2f, platform.stepMoveSeconds);
        float hold = Mathf.Max(0.2f, platform.stepHoldSeconds);
        float leg = move + hold;
        int maxSteps = Mathf.Max(1, Mathf.RoundToInt((platform.maxStepDeg - platform.firstStepDeg)
            / Mathf.Max(0.5f, platform.stepIncrementDeg)) + 1);
        float tRamp = platform.RampTime - platform.settleSeconds;
        int stepNow = tRamp < 0f ? 0 : 1 + Mathf.FloorToInt(tRamp / leg) / 2;
        if (stepNow > maxSteps)
            stepNow = maxSteps;
        scheduledStep = stepNow;

        if (stepNow != openStep)
        {
            CloseRow(time, stepNow > openStep);
            OpenRow(stepNow, time);
        }

        if (openStep >= 1)
        {
            float kneeL = state != null ? state.leftKneeAngle : 0f;
            float kneeR = state != null ? state.rightKneeAngle : 0f;
            float hipL = state != null ? state.leftHipAngle : 0f;
            float hipR = state != null ? state.rightHipAngle : 0f;
            float ankleL = state != null ? state.leftAnkleAngle : 0f;
            float ankleR = state != null ? state.rightAnkleAngle : 0f;
            float armL = leftArmUpper != null ? leftArmUpper.jointAngle : 0f;
            float armR = rightArmUpper != null ? rightArmUpper.jointAngle : 0f;
            rowKnee = Mathf.Max(rowKnee, Mathf.Max(Mathf.Abs(kneeL - rowKnee0L), Mathf.Abs(kneeR - rowKnee0R)));
            rowHip = Mathf.Max(rowHip, Mathf.Max(Mathf.Abs(hipL - rowHip0L), Mathf.Abs(hipR - rowHip0R)));
            rowAnkle = Mathf.Max(rowAnkle, Mathf.Max(Mathf.Abs(ankleL - rowAnkle0L), Mathf.Abs(ankleR - rowAnkle0R)));
            rowArm = Mathf.Max(rowArm, Mathf.Max(Mathf.Abs(armL - rowArm0L), Mathf.Abs(armR - rowArm0R)));
            rowComOffset = Mathf.Max(rowComOffset, Mathf.Abs(com != null ? com.GetCoMOffsetX() : 0f));
            if (state != null)
            {
                rowMargin = Mathf.Min(rowMargin, state.supportMargin);
                if (!state.leftFootGrounded || !state.rightFootGrounded)
                    rowAir++;
            }
            rowTiltEnd = vestibular != null ? Mathf.Abs(vestibular.GetBodyTilt()) : 0f;
            if (balance != null)
            {
                rowRisePoseMax = Mathf.Max(rowRisePoseMax, balance.getUpPose);
                if (balance.getUpGateHeld > rowRiseGateHeld) rowRiseGateHeld = balance.getUpGateHeld;
                if (balance.getUpGateHolds > rowRiseGateHolds) rowRiseGateHolds = balance.getUpGateHolds;
                if (balance.getUpGateHeld > takeGateHeldMax) takeGateHeldMax = balance.getUpGateHeld;
                if (balance.getUpGateHolds > takeGateHoldsMax) takeGateHoldsMax = balance.getUpGateHolds;
                if (balance.getUpBaseWait > rowRiseBaseWait) rowRiseBaseWait = balance.getUpBaseWait;
                if (balance.getUpBaseAborts > rowRiseBaseAborts) rowRiseBaseAborts = balance.getUpBaseAborts;
                if (balance.getUpBaseWait > takeBaseWaitMax) takeBaseWaitMax = balance.getUpBaseWait;
                if (balance.getUpBaseAborts > takeBaseAbortsMax) takeBaseAbortsMax = balance.getUpBaseAborts;
                if (balance.getUpBaseSettle > rowRiseBaseSettle) rowRiseBaseSettle = balance.getUpBaseSettle;
                if (balance.getUpBaseSettleBlocks > rowRiseBaseSettleBlocks) rowRiseBaseSettleBlocks = balance.getUpBaseSettleBlocks;
                if (balance.getUpBaseSettle > takeBaseSettleMax) takeBaseSettleMax = balance.getUpBaseSettle;
                if (balance.getUpBaseSettleBlocks > takeBaseSettleBlocksMax) takeBaseSettleBlocksMax = balance.getUpBaseSettleBlocks;
                if (balance.getUpBaseLoad > rowRiseBaseLoad) rowRiseBaseLoad = balance.getUpBaseLoad;
                if (balance.getUpBaseLoadBlocks > rowRiseBaseLoadBlocks) rowRiseBaseLoadBlocks = balance.getUpBaseLoadBlocks;
                if (balance.getUpBaseLoad > takeBaseLoadMax) takeBaseLoadMax = balance.getUpBaseLoad;
                if (balance.getUpBaseLoadBlocks > takeBaseLoadBlocksMax) takeBaseLoadBlocksMax = balance.getUpBaseLoadBlocks;
                if (balance.getUpArbSuppL > rowRiseArbSuppL) rowRiseArbSuppL = balance.getUpArbSuppL;
                if (balance.getUpArbSuppR > rowRiseArbSuppR) rowRiseArbSuppR = balance.getUpArbSuppR;
                if (balance.getUpArbSuppL > takeArbSuppLMax) takeArbSuppLMax = balance.getUpArbSuppL;
                if (balance.getUpArbSuppR > takeArbSuppRMax) takeArbSuppRMax = balance.getUpArbSuppR;
                if (balance.getUpMoveGuardHeld > rowRiseMoveGuardHeld) rowRiseMoveGuardHeld = balance.getUpMoveGuardHeld;
                if (balance.getUpMoveGuardHolds > rowRiseMoveGuardHolds) rowRiseMoveGuardHolds = balance.getUpMoveGuardHolds;
                if (balance.getUpMoveGuardHeld > takeMoveGuardHeldMax) takeMoveGuardHeldMax = balance.getUpMoveGuardHeld;
                if (balance.getUpMoveGuardHolds > takeMoveGuardHoldsMax) takeMoveGuardHoldsMax = balance.getUpMoveGuardHolds;
                if (balance.getUpTrunkGovHeld > rowRiseGovHeld) rowRiseGovHeld = balance.getUpTrunkGovHeld;
                if (balance.getUpTrunkGovHolds > rowRiseGovHolds) rowRiseGovHolds = balance.getUpTrunkGovHolds;
                if (balance.getUpTrunkGovHeld > takeGovHeldMax) takeGovHeldMax = balance.getUpTrunkGovHeld;
                if (balance.getUpTrunkGovHolds > takeGovHoldsMax) takeGovHoldsMax = balance.getUpTrunkGovHolds;
                if (balance.getUpTrunkGovBias > rowRiseGovBias) rowRiseGovBias = balance.getUpTrunkGovBias;
                if (balance.getUpTrunkGovBias > takeGovBiasMax) takeGovBiasMax = balance.getUpTrunkGovBias;
            }
        }

        // Round 17: the live pose (the self-orientation core), sampled for the rows.
        poseNow = pose != null && pose.Valid ? pose.State.ToString() : "-";
        if (recorder != null)
            fallTime = recorder.PhysicalFallTime;
        if (fallTime >= 0f && openStep >= 1 && !rowFell)
        {
            rowFell = true;
            rowFellAt = fallTime;
            // Round 17: the pose the fall criterion fired from (the orientation core).
            rowPoseFall = poseNow;
            rowFellBack = pose != null && pose.Valid && pose.LyingBack;
        }
        // Round 13: with the get-up practice armed the take does NOT end at the
        // fall - the man rises, the deck keeps testing him and the duration owns
        // the end (the rise count needs the whole story).
        if (!getUpMode && !endRequested && fallTime >= 0f && time >= fallTime + EndAfterFallSeconds)
            endRequested = true;

        // ROUND 35: the practice's VOID END - once every support has left the decks the
        // body free-falls (r13's no-floor finding); the take ends 0.5 s into that fall
        // instead of grinding the void to the duration. -tiltVoidEnd 1, default off.
        if (getUpMode && voidEndOn && !endRequested && platform != null)
        {
            // ROUND 36: the root is a static hierarchy node (r13's tear leaves it
            // standing while the bodies fall); the void end reads the PELVIS body.
            Transform voidBody = pelvisT != null ? pelvisT : humanT;
            if (voidBody != null && voidBody.position.y < platform.transform.position.y - 3f)
            {
                if (voidSince < 0f) voidSince = time;
                if (time - voidSince >= 0.5f)
                {
                    endRequested = true;
                    VoidEndTime = time;
                    Debug.Log("TILT STAND void-end t=" + F(time)
                        + " pelvY=" + F(voidBody.position.y));
                }
            }
            else voidSince = -1f;
        }

        // ROUND 15: the palm-penetration trace (-tiltPen 1). One line per 0.02 s of
        // trial time: both hands against the deck's top face under them, so the
        // plant-phase ejection can be read frame by frame instead of guessed.
        if (penDebug && platform != null && time >= penNext && (handL != null || handR != null))
        {
            penNext = time + 0.02f;
            float rotZ = platform.transform.eulerAngles.z;
            if (rotZ > 180f)
                rotZ -= 360f;
            Debug.Log("TILTPEN t=" + F(time)
                + " rt=" + F(platform.RampTime)
                + " ang=" + F(platform.CurrentAngleDeg)
                + " rot=" + F(rotZ)
                + " | L " + Pen(handL, handColL)
                + " | R " + Pen(handR, handColR));
        }

        if (caption != null)
            caption.Update(BannerText(time), -1f, Vector2.zero);
    }

    // Called once, after the trial loop ended: closes whatever step was open.
    public void Finish(float time)
    {
        CloseRow(time, false);
    }

    private void OpenRow(int step, float time)
    {
        openStep = step;
        rowStart = time;
        rowKnee = 0f;
        rowHip = 0f;
        rowAnkle = 0f;
        rowArm = 0f;
        rowComOffset = 0f;
        rowMargin = 0f;
        rowTiltEnd = 0f;
        rowAir = 0;
        rowSepStart = Sep();
        rowStance0 = stance != null ? stance.StanceSteps : 0;
        rowBraceFire0 = brace != null ? brace.FireCount : 0;
        rowBraceContact0 = brace != null ? brace.ContactFrames : 0;
        rowBraceSupp0 = brace != null ? brace.SuppressedCount : 0;
        rowFell = false; rowFellAt = -1f;
        rowPoseFall = "-"; rowFellBack = false;
        rowRise0 = balance != null ? balance.getUpAttempts : 0;
        rowRisePoseMax = 0;
        rowRiseGateHeld = 0f;
        rowRiseGateHolds = 0;
        rowRiseBaseWait = 0f;
        rowRiseBaseAborts = 0;
        rowRiseBaseSettle = 0f;
        rowRiseBaseSettleBlocks = 0;
        rowRiseBaseLoad = 0f;
        rowRiseBaseLoadBlocks = 0;
        rowRiseArbSuppL = 0;
        rowRiseArbSuppR = 0;
        rowRiseMoveGuardHeld = 0f;
        rowRiseMoveGuardHolds = 0;
        rowRiseGovHeld = 0f;
        rowRiseGovHolds = 0;
        rowRiseGovBias = 0f;
        rowKnee0L = state != null ? state.leftKneeAngle : 0f;
        rowKnee0R = state != null ? state.rightKneeAngle : 0f;
        rowHip0L = state != null ? state.leftHipAngle : 0f;
        rowHip0R = state != null ? state.rightHipAngle : 0f;
        rowAnkle0L = state != null ? state.leftAnkleAngle : 0f;
        rowAnkle0R = state != null ? state.rightAnkleAngle : 0f;
        rowArm0L = leftArmUpper != null ? leftArmUpper.jointAngle : 0f;
        rowArm0R = rightArmUpper != null ? rightArmUpper.jointAngle : 0f;
        rowMargin = float.MaxValue;

        // The mirror check: the deck's own step counter must agree with the
        // schedule this log derived from the same public knobs.
        if (step >= 1 && platform.CurrentStep != step)
        {
            Debug.LogWarning("TILT-LOG schedule drift: log step " + step
                + " vs deck CurrentStep " + platform.CurrentStep);
        }
    }

    private void CloseRow(float time, bool closedByNext)
    {
        if (openStep < 1)
        {
            openStep = 0;
            return;
        }

        float nominal = 2f * (Mathf.Max(0.2f, platform.stepMoveSeconds)
            + Mathf.Max(0.2f, platform.stepHoldSeconds));
        StepRow row = new StepRow();
        row.step = openStep;
        row.angle = Mathf.Min(platform.firstStepDeg + (openStep - 1) * platform.stepIncrementDeg, platform.maxStepDeg);
        row.tStart = rowStart;
        row.tEnd = time;
        row.complete = closedByNext || (time - rowStart) >= nominal - 0.05f;
        row.fell = rowFell;
        row.held = row.complete && !rowFell;
        row.fellAt = rowFellAt;
        row.knee = rowKnee;
        row.hip = rowHip;
        row.ankle = rowAnkle;
        row.arm = rowArm;
        row.comOffset = rowComOffset;
        row.margin = rowMargin == float.MaxValue ? 0f : rowMargin;
        row.tiltEnd = rowTiltEnd;
        row.air = rowAir;
        row.sepStart = rowSepStart;
        row.sepEnd = Sep();
        row.stanceSteps = stance != null ? stance.StanceSteps - rowStance0 : 0;
        row.braceFires = brace != null ? brace.FireCount - rowBraceFire0 : 0;
        row.braceContactFrames = brace != null ? brace.ContactFrames - rowBraceContact0 : 0;
        float contactT = brace != null ? brace.FirstContactTime : -1f;
        row.braceFirstContactT = (contactT >= 0f && contactT >= rowStart && contactT <= time)
            ? contactT : -1f;
        row.braceSuppressed = brace != null ? brace.SuppressedCount - rowBraceSupp0 : 0;
        float fireT = brace != null ? brace.FirstFireTime : -1f;
        row.braceFirstFireT = (fireT >= 0f && fireT >= rowStart && fireT <= time)
            ? fireT : -1f;
        float uwT = reflex != null ? reflex.UnwindStartTime : -1f;
        row.reflexUnwindT = (uwT >= 0f && uwT >= rowStart && uwT <= time) ? uwT : -1f;
        float uwD = reflex != null ? reflex.UnwindDoneTime : -1f;
        row.reflexUnwindDoneT = (uwD >= 0f && uwD >= rowStart && uwD <= time) ? uwD : -1f;
        float usT = reflex != null ? reflex.UnstepStartTime : -1f;
        row.reflexUnstepT = (usT >= 0f && usT >= rowStart && usT <= time) ? usT : -1f;
        float usD = reflex != null ? reflex.UnstepDoneTime : -1f;
        row.reflexUnstepDoneT = (usD >= 0f && usD >= rowStart && usD <= time) ? usD : -1f;
        float uhT = reflex != null ? reflex.UnstepHandoverTime : -1f;
        row.reflexHandT = (uhT >= 0f && uhT >= rowStart && uhT <= time) ? uhT : -1f;
        row.riseAttempts = balance != null ? balance.getUpAttempts - rowRise0 : 0;
        row.risePoseMax = rowRisePoseMax;
        row.riseGateHeld = rowRiseGateHeld;
        row.riseGateHolds = rowRiseGateHolds;
        row.riseBaseWait = rowRiseBaseWait;
        row.riseBaseAborts = rowRiseBaseAborts;
        row.riseBaseSettle = rowRiseBaseSettle;
        row.riseBaseSettleBlocks = rowRiseBaseSettleBlocks;
        row.riseBaseLoad = rowRiseBaseLoad;
        row.riseBaseLoadBlocks = rowRiseBaseLoadBlocks;
        row.riseArbSuppL = rowRiseArbSuppL;
        row.riseArbSuppR = rowRiseArbSuppR;
        row.riseMoveGuardHeld = rowRiseMoveGuardHeld;
        row.riseMoveGuardHolds = rowRiseMoveGuardHolds;
        row.riseGovHeld = rowRiseGovHeld;
        row.riseGovHolds = rowRiseGovHolds;
        row.riseGovBias = rowRiseGovBias;
        row.poseEnd = poseNow;
        row.poseFall = rowPoseFall;
        row.fellBack = rowFellBack;
        rows.Add(row);
        openStep = 0;
    }

    private float Sep()
    {
        return state != null ? state.rightFootPosition.x - state.leftFootPosition.x : 0f;
    }

    // The channels a step's travels say moved (see the thresholds at the top).
    private static string ChannelMask(StepRow row)
    {
        StringBuilder mask = new StringBuilder(24);
        if (row.comOffset >= AnkleComOffsetM || row.ankle >= AnkleChannelDeg)
            mask.Append("ankle|");
        if (row.knee >= KneeChannelDeg)
            mask.Append("knee|");
        if (row.hip >= HipChannelDeg)
            mask.Append("hip|");
        if (row.arm >= ArmChannelDeg)
            mask.Append("arm|");
        if (row.air >= AirChannelFrames)
            mask.Append("step|");
        // The man's own stance: his feet moved the base inside this step (a real step
        // of the IntuitiveStance layer - the width the owner asked to see).
        if (Mathf.Abs(row.sepEnd - row.sepStart) >= 0.012f)
            mask.Append("stance|");
        // The brace reflex (owner's addendum 5): it fired or a palm touched a surface here.
        if (row.braceFires > 0 || row.braceContactFrames > 0)
            mask.Append("brace|");
        // The fall reflex's unwind (round 9): the wind-back ran inside this step.
        if (row.reflexUnwindT >= 0f || row.reflexUnwindDoneT >= 0f)
            mask.Append("wind|");
        // The un-step (round 10): the unwind relocated a foot inside this step.
        if (row.reflexUnstepT >= 0f || row.reflexUnstepDoneT >= 0f)
            mask.Append("place|");
        // The plant hand-over (round 11): the drive rode a locoState flip out (the plant completed).
        if (row.reflexHandT >= 0f)
            mask.Append("hand|");
        // The self-orientation (round 17): the fall fired from a back-lying pose.
        if (row.fellBack)
            mask.Append("back|");
        // The get-up (round 13): an attempt opened or advanced inside this step.
        if (row.riseAttempts > 0 || row.risePoseMax >= 2)
            mask.Append("rise|");
        if (mask.Length == 0)
            return "none";
        mask.Length = mask.Length - 1;
        return mask.ToString();
    }

    public string BuildCsv()
    {
        StringBuilder csv = new StringBuilder(2048);
        csv.Append("step;angleDeg;tStart;tEnd;complete;held;fell;fellAt;kneeDeg;hipDeg;ankleDeg;armDeg;"
            + "comOffsetMaxM;marginMinM;airFrames;tiltEndDeg;baseM;channels;sepStartM;sepEndM;stanceSteps"
            + ";braceFires;braceContactFrames;braceFirstContactT;braceSuppressed;braceFirstFireT"
            + ";reflexUnwindT;reflexUnwindDoneT;reflexUnstepT;reflexUnstepDoneT;reflexHandT"
            + ";riseAttempts;risePoseMax;riseGateHeld;riseGateHolds;riseBaseWait;riseBaseAborts;riseBaseSettle;riseBaseSettleBlocks;riseBaseLoad;riseBaseLoadBlocks;riseMoveGuardHeld;riseMoveGuardHolds;riseGovHeld;riseGovHolds;riseGovBias;poseEnd;poseFall;fellBack;arbSuppL;arbSuppR\n");
        for (int i = 0; i < rows.Count; i++)
        {
            StepRow row = rows[i];
            csv.Append(row.step).Append(';').Append(F(row.angle)).Append(';')
                .Append(F(row.tStart)).Append(';').Append(F(row.tEnd)).Append(';')
                .Append(row.complete ? '1' : '0').Append(';')
                .Append(row.held ? '1' : '0').Append(';')
                .Append(row.fell ? '1' : '0').Append(';').Append(F(row.fellAt)).Append(';')
                .Append(F(row.knee)).Append(';').Append(F(row.hip)).Append(';')
                .Append(F(row.ankle)).Append(';').Append(F(row.arm)).Append(';')
                .Append(F(row.comOffset)).Append(';').Append(F(row.margin)).Append(';')
                .Append(row.air).Append(';').Append(F(row.tiltEnd)).Append(';')
                .Append(F(baseMeters)).Append(';').Append(ChannelMask(row)).Append(';')
                .Append(F(row.sepStart)).Append(';').Append(F(row.sepEnd)).Append(';')
                .Append(row.stanceSteps).Append(';')
                .Append(row.braceFires).Append(';').Append(row.braceContactFrames).Append(';')
                .Append(F(row.braceFirstContactT)).Append(';').Append(row.braceSuppressed).Append(';')
                .Append(F(row.braceFirstFireT)).Append(';')
                .Append(F(row.reflexUnwindT)).Append(';').Append(F(row.reflexUnwindDoneT)).Append(';')
                .Append(F(row.reflexUnstepT)).Append(';').Append(F(row.reflexUnstepDoneT)).Append(';')
                .Append(F(row.reflexHandT)).Append(';')
                .Append(row.riseAttempts).Append(';').Append(row.risePoseMax).Append(';')
                .Append(F(row.riseGateHeld)).Append(';').Append(row.riseGateHolds).Append(';')
                .Append(F(row.riseBaseWait)).Append(';').Append(row.riseBaseAborts).Append(';')
                .Append(F(row.riseBaseSettle)).Append(';').Append(row.riseBaseSettleBlocks).Append(';')
                .Append(F(row.riseBaseLoad)).Append(';').Append(row.riseBaseLoadBlocks).Append(';')
                .Append(F(row.riseMoveGuardHeld)).Append(';').Append(row.riseMoveGuardHolds).Append(';')
                .Append(F(row.riseGovHeld)).Append(';').Append(row.riseGovHolds).Append(';')
                .Append(F(row.riseGovBias)).Append(';')
                .Append(row.poseEnd).Append(';').Append(row.poseFall).Append(';')
                .Append(row.fellBack ? '1' : '0').Append(';')
                .Append(row.riseArbSuppL).Append(';').Append(row.riseArbSuppR).Append('\n');
        }
        return csv.ToString();
    }

    // The take's own summary: the largest step held, the step of the fall.
    public void Summarize()
    {
        HeldDeg = 0f;
        FellStep = 0;
        FellAt = -1f;
        FellPose = "-";
        FellBack = false;
        for (int i = 0; i < rows.Count; i++)
        {
            StepRow row = rows[i];
            if (row.fell && FellStep == 0)
            {
                FellStep = row.step;
                FellAt = row.fellAt;
                FellPose = row.poseFall;
                FellBack = row.fellBack;
            }
        }

        // Contiguous from step 1: the ramp holds a step only if every earlier step held.
        for (int i = 0; i < rows.Count; i++)
        {
            StepRow row = rows[i];
            if (row.step == i + 1 && row.held)
                HeldDeg = row.angle;
            else
                break;
        }
    }

    public string ReportLine()
    {
        Summarize();
        string fell = FellStep > 0
            ? "fell at step " + FellStep + " (" + F(FellAt) + " s)"
            : "no fall";
        string line = "tilt stand: held " + F(HeldDeg) + " deg, " + fell + "; channels: " + UsedChannels();
        if (pose != null && FellStep > 0)
            line += "; pose=" + FellPose + (FellBack ? " back-lying" : "");
        if (stance != null && stance.StanceSteps > 0)
        {
            line += "; stance: " + stance.StanceSteps + " steps, sep "
                + F(rows.Count > 0 ? rows[0].sepStart : 0f) + " -> "
                + F(rows.Count > 0 ? rows[rows.Count - 1].sepEnd : 0f) + " m";
        }
        if (brace != null && (brace.FireCount > 0 || brace.SuppressedCount > 0))
        {
            line += "; brace: " + brace.FireCount + (brace.FireCount == 1 ? " fire" : " fires");
            if (brace.FirstFireTime >= 0f)
                line += " t=" + F(brace.FirstFireTime);
            if (brace.FirstContactTime >= 0f)
                line += ", hand contact t=" + F(brace.FirstContactTime);
            if (brace.SuppressedCount > 0)
                line += ", " + brace.SuppressedCount + " suppressed";
        }
        if (reflex != null && reflex.UnwindCount > 0)
        {
            line += "; reflex unwind: t=" + F(reflex.UnwindStartTime);
            if (reflex.UnwindDoneTime >= 0f)
                line += " -> " + F(reflex.UnwindDoneTime);
        }
        if (balance != null && (getUpMode || balance.getUpAttempts > 0))
        {
            line += "; getUp: " + balance.getUpAttempts
                + (balance.getUpAttempts == 1 ? " attempt" : " attempts")
                + ", poseMax " + balance.getUpPoseMax
                + (balance.getUpSuccess ? ", stood" : ", not stood");
            if (balance.getUpSuccess && balance.getUpSeconds >= 0f)
                line += " in " + F(balance.getUpSeconds) + " s";
            if (takeGateHeldMax > 0f)
                line += ", gate held " + F(takeGateHeldMax) + " s (" + takeGateHoldsMax
                    + (takeGateHoldsMax == 1 ? " hold" : " holds") + ")";
            if (takeBaseWaitMax > 0f || takeBaseAbortsMax > 0)
                line += ", base wait " + F(takeBaseWaitMax) + " s (" + takeBaseAbortsMax
                    + (takeBaseAbortsMax == 1 ? " abort" : " aborts") + ")";
            if (takeBaseSettleMax > 0f || takeBaseSettleBlocksMax > 0)
                line += ", settle " + F(takeBaseSettleMax) + " s (" + takeBaseSettleBlocksMax
                    + (takeBaseSettleBlocksMax == 1 ? " block" : " blocks") + ")";
            if (takeArbSuppLMax > 0 || takeArbSuppRMax > 0)
                line += ", hipArb supp " + takeArbSuppLMax + "/" + takeArbSuppRMax;
            if (takeMoveGuardHeldMax > 0f)
                line += ", move guard " + F(takeMoveGuardHeldMax) + " s (" + takeMoveGuardHoldsMax
                    + (takeMoveGuardHoldsMax == 1 ? " hold" : " holds") + ")";
            if (takeGovHeldMax > 0f || takeGovBiasMax > 0f)
                line += ", gov held " + F(takeGovHeldMax) + " s, bias max " + F(takeGovBiasMax) + " deg";
            if (balance.getUpSitFires > 0 || balance.getUpSitExtMax > 0f)
                line += ", sit-stand " + balance.getUpSitFires
                    + (balance.getUpSitFires == 1 ? " fire" : " fires")
                    + ", ext max " + F(balance.getUpSitExtMax) + " s"
                    + (balance.getUpSitSeatFrozen > 0f
                        ? ", seat frozen " + F(balance.getUpSitSeatFrozen) + " s ("
                            + balance.getUpSitSeatPasses + " pass)"
                        : "");
        }
        return line;
    }

    // The channels any step that actually stood used - the union over held steps,
    // plus the failing step's own line when the take fell.
    public string UsedChannels()
    {
        StringBuilder bits = new StringBuilder(48);
        for (int i = 0; i < rows.Count; i++)
        {
            if (!rows[i].held && !rows[i].fell)
                continue;
            string mask = ChannelMask(rows[i]);
            if (mask == "none" || bits.ToString().Contains(mask))
                continue;
            if (bits.Length > 0)
                bits.Append("; ");
            bits.Append("step ").Append(rows[i].step).Append(':').Append(mask);
        }
        return bits.Length > 0 ? bits.ToString() : "none";
    }

    public string SummaryFragment()
    {
        Summarize();
        StringBuilder json = new StringBuilder(256);
        json.Append(",\"tilt\":{\"deckLength\":").Append(F(platform.deckLength));
        json.Append(",\"settleSeconds\":").Append(F(platform.settleSeconds));
        json.Append(",\"stepMoveSeconds\":").Append(F(platform.stepMoveSeconds));
        json.Append(",\"stepHoldSeconds\":").Append(F(platform.stepHoldSeconds));
        json.Append(",\"firstStepDeg\":").Append(F(platform.firstStepDeg));
        json.Append(",\"maxStepDeg\":").Append(F(platform.maxStepDeg));
        json.Append(",\"baseMeters\":").Append(F(baseMeters));
        if (stance != null)
        {
            json.Append(",\"stanceArmed\":1");
            json.Append(",\"stanceSteps\":").Append(stance.StanceSteps);
            json.Append(",\"sepStartM\":").Append(F(rows.Count > 0 ? rows[0].sepStart : 0f));
            json.Append(",\"sepEndM\":").Append(F(rows.Count > 0 ? rows[rows.Count - 1].sepEnd : 0f));
        }
        if (brace != null)
        {
            json.Append(",\"braceFires\":").Append(brace.FireCount);
            json.Append(",\"braceContactFrames\":").Append(brace.ContactFrames);
            if (brace.FirstFireTime >= 0f)
                json.Append(",\"braceFireT\":").Append(F(brace.FirstFireTime));
            if (brace.FirstContactTime >= 0f)
                json.Append(",\"braceContactT\":").Append(F(brace.FirstContactTime));
            if (brace.SuppressedCount > 0)
                json.Append(",\"braceSuppressed\":").Append(brace.SuppressedCount);
        }
        if (reflex != null)
        {
            if (reflex.FireTime >= 0f)
                json.Append(",\"reflexFireT\":").Append(F(reflex.FireTime));
            if (reflex.UnwindStartTime >= 0f)
                json.Append(",\"reflexUnwindT\":").Append(F(reflex.UnwindStartTime));
            if (reflex.UnwindDoneTime >= 0f)
                json.Append(",\"reflexUnwindDoneT\":").Append(F(reflex.UnwindDoneTime));
            if (reflex.UnstepStartTime >= 0f)
                json.Append(",\"reflexUnstepT\":").Append(F(reflex.UnstepStartTime));
            if (reflex.UnstepDoneTime >= 0f)
                json.Append(",\"reflexUnstepDoneT\":").Append(F(reflex.UnstepDoneTime));
            if (reflex.UnstepHandoverTime >= 0f)
                json.Append(",\"reflexHandT\":").Append(F(reflex.UnstepHandoverTime));
        }
        if (pose != null)
        {
            json.Append(",\"poseArmed\":1");
            if (FellStep > 0)
            {
                json.Append(",\"poseAtFall\":\"").Append(FellPose).Append('"');
                json.Append(",\"fellBack\":").Append(FellBack ? "true" : "false");
            }
            if (reflex != null)
                json.Append(",\"reflexRestCount\":").Append(reflex.OrientRestCount);
            if (arm != null)
                json.Append(",\"armRestEntries\":").Append(arm.OrientRestEntries);
        }
        if (getUpMode && balance != null)
        {
            json.Append(",\"riseAttempts\":").Append(balance.getUpAttempts);
            json.Append(",\"riseStood\":").Append(balance.getUpSuccess ? "true" : "false");
            json.Append(",\"risePoseMax\":").Append(balance.getUpPoseMax);
            if (takeGateHeldMax > 0f)
            {
                json.Append(",\"riseGateHeld\":").Append(F(takeGateHeldMax));
                json.Append(",\"riseGateHolds\":").Append(takeGateHoldsMax);
            }
            if (takeBaseWaitMax > 0f || takeBaseAbortsMax > 0)
            {
                json.Append(",\"riseBaseWait\":").Append(F(takeBaseWaitMax));
                json.Append(",\"riseBaseAborts\":").Append(takeBaseAbortsMax);
            }
            if (takeBaseSettleMax > 0f || takeBaseSettleBlocksMax > 0)
            {
                json.Append(",\"riseBaseSettle\":").Append(F(takeBaseSettleMax));
                json.Append(",\"riseBaseSettleBlocks\":").Append(takeBaseSettleBlocksMax);
            }
            if (takeBaseLoadMax > 0f || takeBaseLoadBlocksMax > 0)
            {
                json.Append(",\"riseBaseLoad\":").Append(F(takeBaseLoadMax));
                json.Append(",\"riseBaseLoadBlocks\":").Append(takeBaseLoadBlocksMax);
            }
            if (takeArbSuppLMax > 0 || takeArbSuppRMax > 0)
            {
                json.Append(",\"riseArbSuppL\":").Append(takeArbSuppLMax);
                json.Append(",\"riseArbSuppR\":").Append(takeArbSuppRMax);
            }
            if (takeMoveGuardHeldMax > 0f || takeMoveGuardHoldsMax > 0)
            {
                json.Append(",\"riseMoveGuardHeld\":").Append(F(takeMoveGuardHeldMax));
                json.Append(",\"riseMoveGuardHolds\":").Append(takeMoveGuardHoldsMax);
            }
            if (takeGovHeldMax > 0f || takeGovHoldsMax > 0 || takeGovBiasMax > 0f)
            {
                json.Append(",\"riseGovHeld\":").Append(F(takeGovHeldMax));
                json.Append(",\"riseGovHolds\":").Append(takeGovHoldsMax);
                json.Append(",\"riseGovBias\":").Append(F(takeGovBiasMax));
            }
            if (balance.getUpSitFires > 0 || balance.getUpSitExtMax > 0f)
            {
                json.Append(",\"riseSitFires\":").Append(balance.getUpSitFires);
                json.Append(",\"riseSitExt\":").Append(F(balance.getUpSitExtMax));
                if (balance.getUpSitSeatFrozen > 0f)
                    json.Append(",\"riseSeatFrozen\":").Append(F(balance.getUpSitSeatFrozen))
                        .Append(",\"riseSeatPasses\":").Append(balance.getUpSitSeatPasses);
            }
            if (VoidEndTime >= 0f)
                json.Append(",\"voidEnd\":").Append(F(VoidEndTime));
            if (balance.getUpSeconds >= 0f)
                json.Append(",\"riseSeconds\":").Append(F(balance.getUpSeconds));
        }
        json.Append(",\"heldDeg\":").Append(F(HeldDeg));
        json.Append(",\"fellStep\":").Append(FellStep);
        json.Append(",\"fellAt\":").Append(F(FellAt));
        json.Append(",\"steps\":").Append(rows.Count);
        json.Append('}');
        return json.ToString();
    }

    private string BannerText(float time)
    {
        string state = openStep < 1
            ? "settling"
            : (rowFell ? "FALLEN" : (WantsEnd ? "down" : "STANDING"));
        string step = openStep < 1
            ? "pre-ramp"
            : "step " + openStep + " = " + F(Mathf.Min(platform.firstStepDeg + (openStep - 1) * platform.stepIncrementDeg, platform.maxStepDeg))
                + " deg";
        return step + "  " + state + "  t=" + F(time) + " s"
            + "  base " + F(Sep()) + " m"
            + (pose != null ? "  pose=" + poseNow : "")
            + (rowFell && rowFellAt >= 0f ? "  (fall at " + F(rowFellAt) + " s)" : "");
    }

    // ROUND 15: one hand against the deck - its centre, the palm's bottom face, the
    // deck top under it, the penetration (positive = the palm is inside the deck)
    // and the rig's own palm-contact flag.
    private string Pen(Transform hand, Collider2D col)
    {
        if (hand == null || platform == null)
            return "-";
        float x = hand.position.x;
        float bottom = col != null ? col.bounds.min.y : hand.position.y;
        float top = platform.DeckTopWorldY(x);
        int contact = 0;
        if (arm != null)
            contact = (hand == handL ? arm.HandContactL : arm.HandContactR) ? 1 : 0;
        return "x=" + F(x) + " bot=" + F(bottom) + " top=" + F(top)
            + " pen=" + F(top - bottom) + " c=" + contact;
    }

    private static string F(float value)
    {
        return value.ToString("0.####", CultureInfo.InvariantCulture);
    }
}
