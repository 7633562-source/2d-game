using System;
using System.Globalization;
using System.Text;
using UnityEngine;

// Context of one run: what was requested and which build was checked.
// Fingerprint comes from build-manifest.json; it is not recomputed from the player sources.
public sealed class TrialRunInfo
{
    public string Label = "trial";
    public float Duration;
    public float StartY = -0.82f;
    public float RequestedPushImpulse;
    public float RequestedPushTime = 5f;
    public float RequestedCrouch;
    public float RequestedCrouchTime = 5f;
    public float RequestedCrouchHold = 10f;
    public float RequestedLean;
    public float RequestedLeanTime = 5f;
    public float RequestedLeanHold = 10f;
    public float RequestedStandLeg;
    public float RequestedStandLegTime = 5f;
    public float RequestedStandLegHold = 10f;
    public float RequestedMarch;
    public float RequestedMarchTime = 5f;
    public float RequestedMarchDuration = 24f;
    public float RequestedMarchHold = 2.5f;
    public float RequestedMarchPlant = 1.5f;
    public float RequestedMarchFirst = -1f;
    public float RequestedWalk;
    public float RequestedWalkTime = 5f;
    public float RequestedWalkDuration = 30f;
    public float RequestedWalkStance = 8f;
    public float RequestedRun;
    public float RequestedRunTime = 5f;
    public float RequestedRunHold = 20f;
    public float RequestedWalkTransfer = 8f;
    public float RequestedWalkTransferCom = 0.10f;
    public float RequestedWalkTransferFallbackCom = 0.12f;
    public float RequestedWalkTransferLevel = 0.05f;
    public float RequestedWalkFirst = 1f;
    public float MuscleMultiplier = 1f;
    public float FrictionMultiplier = 1f;
    public string SourceFingerprint = "";
    public string ManifestUnityVersion = "";
    public string ManifestBuildGuid = "";
    public string ManifestBuildTimeUtc = "";
    public string ManifestPath = "";
    public string[] CommandArgs = Array.Empty<string>();
}

// Samples the human on every physics step of the run and computes
// summary metrics. Applies nothing to the body — read-only,
// same as the rest of the sensor layer.
public class TrialRecorder
{
    // Activation is saturated just below one: the PD controller
    // hits the ceiling and then loses authority.
    private const float SATURATION_THRESHOLD = 0.999f;

    // The human is settled when the CoM offset
    // fits back into a centimetre: in quiet stance it is about 7 mm.
    private const float SETTLED_COM_OFFSET = 0.01f;

    // After crouch release the pelvis should rise almost to the start
    // height. 5 cm is more than stance sag and less than a visible squat.
    private const float PELVIS_RECOVER_TOLERANCE = 0.05f;

    // A joint is at the stop if the angle is within 1° of min/max. This is not force, it is
    // geometry: arms and head stick at the limit when K is too small.
    private const float LIMIT_MARGIN_DEGREES = 1f;

    // ─── PHYSICAL FALL DETECTION ───
    // The automaton is not ground truth. Trial p_sw1 (18.09, 07:55 build) lay on
    // the dirt from 8 s to 39 s with torsoTilt -51 deg, headY -1.52 and
    // supportMargin -0.84, while BalanceState stayed at Recovery and `fell`
    // stayed false — the Falling state is never entered once the get-up
    // automaton owns the body. The body is down when the torso is past 45 deg
    // AND the pelvis has dropped 30 cm, or when the pelvis is more than half a
    // metre low AND the head has fallen below the standing pelvis height.
    // Measured over the 1095 fallen=false runs on disk: a quiet stance reaches
    // neither, the deepest crouch stops at 0.46 m of pelvis drop with the head
    // 0.10 m above the standing pelvis height, and both routes together catch
    // 19 runs the automaton missed.
    private const float DOWN_TILT_DEGREES = 45f;
    private const float DOWN_PELVIS_DROP = 0.30f;
    private const float DOWN_DEEP_DROP = 0.50f;
    // The signal must hold a few frames: one noisy frame is not a fall. The
    // reported time is the FIRST frame of the run, not the end of the settle.
    private const float DOWN_SETTLE_SECONDS = 0.03f;

    private readonly Human human;
    private readonly string label;
    private readonly TrialRunInfo info;

    private readonly BalanceController balance;
    private readonly BodyStateEstimator state;
    private readonly CenterOfMassCalculator com;
    private readonly VestibularSystem vestibular;
    private readonly Transform head;
    private readonly Transform pelvis;
    private readonly Collider2D leftHandCollider;
    private readonly Collider2D rightHandCollider;

    private readonly StringBuilder rows = new StringBuilder();

    private int samples;
    private double sumComOffsetSquared;
    private double sumAbsComOffset;
    private double sumTorsoAngVelSquared;
    private float maxAbsTilt;
    private float fallTime = -1f;
    // First frame the body was actually down (physical); -1 while he is up.
    // `fell` and `survivedSeconds` read this one, not the automaton.
    private float physicalFallTime = -1f;
    private float downAge;
    private int samplesDown;
    // True on this frame whenever the body is on the ground (not latched).
    private bool downNow;
    private int samplesBalancing;
    private int samplesRecovery;
    private int samplesFalling;
    private int samplesGettingUp;
    private int samplesFallenLoco;
    private int samplesGetUpWalk;
    private int samplesSaturated;
    private int samplesBothFeet;
    private float startHeadY;
    private float minHeadY = float.MaxValue;
    private float minHandY = float.MaxValue;
    private float lastHeadY;
    private float startPelvisY;
    private float minPelvisY = float.MaxValue;
    private float lastPelvisY;
    private float minHipAngle = float.MaxValue;
    private float maxHipAngle = float.MinValue;
    private float minKneeAngle = float.MaxValue;
    private float maxKneeAngle = float.MinValue;
    private float maxAbsHipAngle;
    private float maxAbsKneeAngle;
    private float maxCrouchLevel;
    private float maxStandLegLevel;

    private float crouchStartTime = -1f;
    private float crouchReleaseTime = -1f;
    private float requestedCrouchLevel;
    private int samplesCrouchWindow;
    private int samplesBothFeetCrouch;
    private int samplesBothHandsCrouch;
    private int samplesLeftHandCrouch;
    private int samplesRightHandCrouch;

    private float standLegStartTime = -1f;
    private float standLegReleaseTime = -1f;
    private float requestedStandLeg;
    private int samplesStandLegWindow;
    private int samplesBothFeetStandLeg;
    private int samplesSwingGrounded;
    private int samplesStanceGrounded;
    private int samplesLeftGrounded;
    private int samplesRightGrounded;
    // Cold one-leg hold quality (after the swing is airborne + 2 s settle).
    // maxAbsPelvisTilt is the whole run, including lift; acceptance is
    // the settled band and CoM drift during the hold.
    private const float OneLegHoldSettleSeconds = 2f;
    private float oneLegAirStartTime = -1f;
    private float oneLegHoldStartTime = -1f;
    private int samplesOneLegHold;
    private int marchSwapCount;
    private float lastMarchStandCmd;
    private int samplesMarchAir;
    private float sumMarchSwingKnee;
    private float maxMarchSwingKnee = float.MinValue;
    private float sumMarchSwingHip;
    private float minMarchSwingHip = float.MaxValue;
    private float marchPelvisMin = float.MaxValue;
    private float marchPelvisMax = float.MinValue;
    private float marchAirAge;
    private float oneLegPelvisMin = float.MaxValue;
    private float oneLegPelvisMax = float.MinValue;
    private float oneLegComXMin = float.MaxValue;
    private float oneLegComXMax = float.MinValue;
    private float oneLegLastPelvisDelta;
    private float oneLegLastPelvis;
    private bool oneLegHasPrevPelvis;
    private int oneLegPelvisReversals;
    private float lastTilt;
    private float lastComOffset;
    // Distance travelled by the CoM along X. Without it a support swap counted
    // as walking while the body stayed put: 0.21 m in 35 s is marching in place.
    private float startComX;
    private float lastComX;
    private bool hasStartComX;
    private float lastPelvisTilt;
    private float lastLumbarAngle;
    private float maxAbsLumbarAngle;
    private float maxAbsPelvisTilt;
    private int samplesWalkLiftOwns;
    private int samplesWalkLiftAdvance;
    private int samplesWalkLiftPlantOverFloor;
    private float sumWalkLiftComLead;
    private float maxAbsWalkLiftComLead;
    private float maxWalkLiftCaptureStep;
    private float maxWalkLiftXiLead;
    private int samplesWalkAnkleFwdClip;
    private int samplesWalkAnkleDorsiFade;

    private float pushTime = -1f;
    private float pushImpulse;
    private float maxAbsComOffsetAfterPush;
    private float maxAbsTiltAfterPush;
    private float lastUnsettledTime = -1f;

    // Passive viscosity of neck/head/arms: snapshot of Ieff and RMS relative
    // speed. Does not affect torques — only reads the already built body.
    private readonly JointDampingWatch neckWatch;
    private readonly JointDampingWatch headWatch;
    private readonly JointDampingWatch shoulderWatch;
    private readonly JointDampingWatch elbowWatch;
    private readonly JointDampingWatch wristWatch;

    private StepPhaseDriver stepDriver;
    private MotionIntent motionIntent;
    private ArmDrive armDrive;
    // Arm drive evidence, tracked here so the number lands in the JSON as well
    // as in the per-side CSV columns: the counter phase is only measurable if
    // the commanded phase and the delivered joint angle are both on record.
    private int samplesArmSwingOn;
    private int samplesDownArm;
    private int samplesDownHandContactL;
    private int samplesDownHandContactR;
    private float maxArmHandY;
    private int maxWalkSwapCount;
    private int lastWalkPhase;
    private int lastFallCatchPhase = -1;
    private int maxFallCatchCycleCount;
    private float lastStandLegCmd;
    private int lastSensorTick;
    private int comBodyCount;
    private float sensorComXMin = float.MaxValue;
    private float sensorComXMax = float.MinValue;
    private float sensorMarginMin = float.MaxValue;
    private float sensorMarginMax = float.MinValue;
    private float sensorPelvisYMin = float.MaxValue;
    private float sensorPelvisYMax = float.MinValue;
    private int samplesOnsetWindow;
    private int samplesOnsetCollapsed;
    private int samplesOnsetPosed;
    // WALK-1: the walker's tick counter at the previous row, so tapFresh can
    // tell a live command from a frozen last one.
    private long lastTapTicks = -1;
    // WALK-1 recorder completeness: one owner, one finite request and one
    // pre-clamp demand per leg joint per row (hipL, hipR, kneeL, kneeR,
    // ankleL, ankleR). Preallocated: this runs at 200 Hz.
    private readonly int[] legOwnerOut = new int[BalanceController.LegJointCount];
    private readonly float[] legReqOut = new float[BalanceController.LegJointCount];
    private readonly float[] legDemandOut = new float[BalanceController.LegJointCount];
    private int legFallbackMask;
    private int legLayerOut;

    public TrialRecorder(Human human, string label)
        : this(human, new TrialRunInfo { Label = label })
    {
    }

    public TrialRecorder(Human human, TrialRunInfo info)
    {
        this.human = human;
        this.info = info ?? new TrialRunInfo();
        this.label = this.info.Label ?? "trial";

        balance = human.GetComponent<BalanceController>();
        state = human.GetComponent<BodyStateEstimator>();
        com = human.GetComponent<CenterOfMassCalculator>();
        vestibular = human.GetComponent<VestibularSystem>();
        head = human.headSegment != null ? human.headSegment.transform : null;
        Transform pelvisTransform = human.transform.Find("Pelvis");
        pelvis = pelvisTransform;
        Transform leftHandT = human.transform.Find("LeftArmHand");
        Transform rightHandT = human.transform.Find("RightArmHand");
        leftHandCollider = leftHandT != null ? leftHandT.GetComponent<Collider2D>() : null;
        rightHandCollider = rightHandT != null ? rightHandT.GetComponent<Collider2D>() : null;

        startHeadY = head != null ? head.position.y : 0f;
        lastHeadY = startHeadY;
        startPelvisY = pelvis != null ? pelvis.position.y : 0f;
        lastPelvisY = startPelvisY;
        minPelvisY = startPelvisY;

        neckWatch = JointDampingWatch.Single("neck", FindJoint(human, "Neck"));
        headWatch = JointDampingWatch.Single("head", FindJoint(human, "Head"));
        shoulderWatch = JointDampingWatch.Pair(
            "shoulder",
            FindJoint(human, "LeftArmUpper"),
            FindJoint(human, "RightArmUpper"));
        elbowWatch = JointDampingWatch.Pair(
            "elbow",
            FindJoint(human, "LeftArmLower"),
            FindJoint(human, "RightArmLower"));
        wristWatch = JointDampingWatch.Pair(
            "wrist",
            FindJoint(human, "LeftArmHand"),
            FindJoint(human, "RightArmHand"));

        stepDriver = human.GetComponent<StepPhaseDriver>();
        motionIntent = human.GetComponent<MotionIntent>();
        armDrive = human.GetComponent<ArmDrive>();

        // Header into the same rows: Join+new[] would allocate an array and an intermediate string for nothing.
        rows.Append("time").Append(';')
            .Append("state").Append(';')
            .Append("torsoTilt").Append(';')
            .Append("torsoAngVel").Append(';')
            .Append("pelvisTilt").Append(';')
            .Append("comX").Append(';')
            .Append("comOffset").Append(';')
            .Append("supportMargin").Append(';')
            .Append("leftGrounded").Append(';')
            .Append("rightGrounded").Append(';')
            .Append("headY").Append(';')
            .Append("hipL").Append(';')
            .Append("hipR").Append(';')
            .Append("kneeL").Append(';')
            .Append("kneeR").Append(';')
            .Append("ankleL").Append(';')
            .Append("ankleR").Append(';')
            .Append("lumbar").Append(';')
            .Append("actHipL").Append(';')
            .Append("actHipR").Append(';')
            .Append("actKneeL").Append(';')
            .Append("actKneeR").Append(';')
            .Append("actAnkleL").Append(';')
            .Append("actAnkleR").Append(';')
            .Append("pelvisY").Append(';')
            .Append("crouchLevel").Append(';')
            .Append("standLegLevel").Append(';')
            .Append("standLegCmd").Append(';')
            .Append("walkPhase").Append(';')
            .Append("walkSwap").Append(';')
            .Append("fallCatchPhase").Append(';')
            .Append("fallCatchCycle").Append(';')
            .Append("dualSoft").Append(';')
            .Append("neckAngle").Append(';')
            .Append("headAngle").Append(';')
            .Append("shoulderAngle").Append(';')
            .Append("elbowAngle").Append(';')
            .Append("walkCollapsed").Append(';')
            .Append("walkPosed").Append(';')
            // Per side, so the arm phase is measurable. One averaged
            // shoulderAngle already hid the counter swing: it logged the LEFT
            // hinge only, and the left/right relation is the whole point.
            .Append("shoulderAngleL").Append(';')
            .Append("shoulderAngleR").Append(';')
            .Append("elbowAngleL").Append(';')
            .Append("elbowAngleR").Append(';')
            .Append("actShoulderL").Append(';')
            .Append("actShoulderR").Append(';')
            .Append("actElbowL").Append(';')
            .Append("actElbowR").Append(';')
            .Append("handGroundedL").Append(';')
            .Append("handGroundedR").Append(';')
            // Arm drive evidence, per side. handGrounded* above is the estimator's
            // slop test; handContactL/R below is a real Box2D contact with the
            // ground layer, and handYL/R is where the palm actually is. armPhaseL/R
            // is the drive's own phase, so a flat correlation can be told apart
            // from a drive that never asked for a counter phase.
            .Append("handContactL").Append(';')
            .Append("handContactR").Append(';')
            .Append("handYL").Append(';')
            .Append("handYR").Append(';')
            .Append("armSwingLevel").Append(';')
            .Append("armPhaseL").Append(';')
            .Append("armPhaseR").Append(';')
            .Append("armTargetShoulderL").Append(';')
            .Append("armTargetShoulderR").Append(';')
            .Append("armTargetElbowL").Append(';')
            .Append("armTargetElbowR").Append(';')
            .Append("armPressPhaseL").Append(';')
            .Append("armPressPhaseR").Append(';')
            .Append("armPressRequest").Append(';')
            // Press evidence per side, per frame: the running stroke extension,
            // the latch of the latched palm from the shoulder pivot, the shoulder
            // rise inside the stroke (the push itself, with the palm fixed) and
            // how far past its stop the plant asked the elbow to fold. One
            // averaged shoulderAngle hid the counter phase; one arm-press number
            // hides which palm is planted and which arm is doing the pushing.
            .Append("armStrokeExtL").Append(';')
            .Append("armStrokeExtR").Append(';')
            .Append("armStrokesL").Append(';')
            .Append("armStrokesR").Append(';')
            .Append("armShoulderLiftL").Append(';')
            .Append("armShoulderLiftR").Append(';')
            .Append("armPlantOffsetL").Append(';')
            .Append("armPlantOffsetR").Append(';')
            .Append("armPlantFoldL").Append(';')
            .Append("armPlantFoldR").Append(';')
            .Append("armAimSignL").Append(';')
            .Append("armAimSignR").Append(';')
            .Append("armPivotYL").Append(';')
            .Append("armPivotYR").Append(';')
            .Append("armPalmYL").Append(';')
            .Append("armPalmYR").Append(';')
            .Append("armHaveL").Append(';')
            .Append("armHaveR").Append(';')
            .Append("armRestL").Append(';')
            .Append("armRestR").Append(';')
            // down = on the ground this frame, fell = latched at the first
            // down frame, so the frame the flag turns true is in the CSV.
            .Append("down").Append(';')
            .Append("fell").Append(';')
            // ─── WALK-1 command tap: APPEND-ONLY ───
            // Every column above keeps its name: probes parse them. These are
            // added at the end, one row per frame, on the walk trial only.
            // ownerHip*/Knee*/Ankle* — which drive method wrote that joint
            // this frame (0 = nobody, see BalanceController.TapOwner*).
            // reqHip*/Knee* — the target ANGLE it handed to the PD, degrees,
            // after the loaded caps. reqAnkle* is NaN when the CoP loop owned
            // the ankle, because that loop commands torque (copCmd*).
            // appTorque* — activation * maxTorque * direction of the pair,
            // i.e. the torque the actuators really applied.
            // swapReason/swapMode/swapAge — why the last role swap fired
            // (1 contact, 2 clock, 3 lead, 4 watchdog, 5 emergency, 6 late;
            // 1 paired / 2 catchOwns) and the gait age when it did.
            // gateMask/rejectCode — the first-step gates that passed, and the
            // first one that did not. planValid — the frozen plan accepted.
            // soleClear*/footLoaded* — measured sole clearance above the slab
            // and the contact sensor's own load flags.
            .Append("tapActive").Append(';')
            .Append("tapAge").Append(';')
            .Append("tapFresh").Append(';')
            .Append("tapTicks").Append(';')
            .Append("walkTapAbort").Append(';')
            .Append("ownerHipL").Append(';')
            .Append("ownerHipR").Append(';')
            .Append("ownerKneeL").Append(';')
            .Append("ownerKneeR").Append(';')
            .Append("ownerAnkleL").Append(';')
            .Append("ownerAnkleR").Append(';')
            .Append("reqHipL").Append(';')
            .Append("reqHipR").Append(';')
            .Append("reqKneeL").Append(';')
            .Append("reqKneeR").Append(';')
            .Append("reqAnkleL").Append(';')
            .Append("reqAnkleR").Append(';')
            .Append("copCmdL").Append(';')
            .Append("copCmdR").Append(';')
            .Append("appTorqueHipL").Append(';')
            .Append("appTorqueHipR").Append(';')
            .Append("appTorqueKneeL").Append(';')
            .Append("appTorqueKneeR").Append(';')
            .Append("appTorqueAnkleL").Append(';')
            .Append("appTorqueAnkleR").Append(';')
            .Append("roleL").Append(';')
            .Append("roleR").Append(';')
            .Append("gaitState").Append(';')
            .Append("gaitStateAge").Append(';')
            .Append("stepWalkActive").Append(';')
            .Append("stepPhaseCode").Append(';')
            .Append("stepSwapCount").Append(';')
            .Append("dualSupport").Append(';')
            .Append("swapReason").Append(';')
            .Append("swapMode").Append(';')
            .Append("swapAge").Append(';')
            .Append("gaitSteps").Append(';')
            .Append("entryReason").Append(';')
            .Append("gateMask").Append(';')
            .Append("rejectCode").Append(';')
            .Append("planValid").Append(';')
            .Append("footTouchL").Append(';')
            .Append("footTouchR").Append(';')
            .Append("footLoadedL").Append(';')
            .Append("footLoadedR").Append(';')
            .Append("footSupportL").Append(';')
            .Append("footSupportR").Append(';')
            .Append("soleClearL").Append(';')
            .Append("soleClearR").Append(';')
            // Who owned the BODY after the walker stopped. The walker is
            // dropped when StepPhaseDriver.StandingRestore/Surrender is armed
            // or the locomotion state leaves Standing/Walking, and
            // BalanceController then falls through to the standing Plant that
            // holds the last leg command. locoState = (int)balance.locomotionState.
            .Append("stepStandingRestore").Append(';')
            .Append("stepStandingSurrender").Append(';')
            .Append("locoState").Append(';')
            // ─── WALK-1 recorder completeness (append-only) ───
            // ownerHipL..copCmdR above are now filled on EVERY row: the
            // capture walker's own drive code (1..14) while it wrote that joint
            // this frame, else the layer that held it (15 idle, 16 quiet
            // stance / stance clock, 17 standing Plant, 18 get-up, 19 fallen /
            // relaxed, 20 pose library, 21 the capture walker as a layer).
            // req* is the angle that owner handed the PD, degrees. When the
            // owner commands TORQUE instead of an angle (CoP / CoM ankle, a
            // world-upright hip, a zero snap) or nobody asked, req* is the
            // MEASURED joint angle - a held value, not an ask - and the
            // matching bit of reqFallbackMask is 1 (bit0 hipL .. bit5 ankleR).
            // demand* is that owner's demand BEFORE its own clamp, in units of
            // the acting muscle's ceiling (hip 100, knee 130, ankle 65 ext /
            // 22 flex N*m), so |demand| > 1 = it asked for more torque than the
            // muscle can deliver. 0 with an idle owner = no demand recorded.
            // legLayer = the layer code that drove the legs on that step.
            .Append("legLayer").Append(';')
            .Append("reqFallbackMask").Append(';')
            .Append("demandHipL").Append(';')
            .Append("demandHipR").Append(';')
            .Append("demandKneeL").Append(';')
            .Append("demandKneeR").Append(';')
            .Append("demandAnkleL").Append(';')
            .Append("demandAnkleR").AppendLine();
    }

    public void BindWalkDriver(StepPhaseDriver driver)
    {
        stepDriver = driver;
        if (human != null)
            motionIntent = human.GetComponent<MotionIntent>();
    }

    // Crouch window: from press to release. Needed to separate
    // pelvis drop and support from the startup settle.
    public void MarkCrouch(float time, float level)
    {
        crouchStartTime = time;
        requestedCrouchLevel = level;
    }

    public void MarkCrouchRelease(float time)
    {
        crouchReleaseTime = time;
    }

    public void MarkStandLeg(float time, float side)
    {
        standLegStartTime = time;
        requestedStandLeg = side;
    }

    public void MarkStandLegRelease(float time)
    {
        standLegReleaseTime = time;
    }

    // Called by the stand at the push so recovery metrics
    // are separated from what happened before the disturbance.
    public void MarkPush(float time, float impulse)
    {
        pushTime = time;
        pushImpulse = impulse;
    }

    // Called after every physics step.
    public void Sample(float time)
    {
        samples++;

        float tilt = vestibular != null ? vestibular.GetBodyTilt() : 0f;
        float angVel = vestibular != null ? vestibular.GetBodyAngularVelocity() : 0f;
        float comOffset = com != null ? com.GetCoMOffsetX() : 0f;
        Vector2 comPosition = com != null ? com.GetCenterOfMass() : Vector2.zero;
        if (!hasStartComX)
        {
            startComX = comPosition.x;
            hasStartComX = true;
        }
        lastComX = comPosition.x;
        float headY = head != null ? head.position.y : 0f;
        float pelvisY = pelvis != null ? pelvis.position.y : 0f;
        float crouchLevel = balance != null ? balance.crouchLevel : 0f;
        float standLegLevel = balance != null ? balance.standLegLevel : 0f;
        float dualSoft = balance != null ? balance.lastDualSoft : 1f;
        float standLegCmd = motionIntent != null ? motionIntent.standLeg : 0f;
        if (stepDriver == null && human != null)
            stepDriver = human.GetComponent<StepPhaseDriver>();
        if (motionIntent == null && human != null)
            motionIntent = human.GetComponent<MotionIntent>();
        standLegCmd = motionIntent != null ? motionIntent.standLeg : standLegCmd;
        int walkPhase = stepDriver != null && stepDriver.walkActive ? stepDriver.PhaseCode : -1;
        int walkSwap = stepDriver != null ? stepDriver.SwapCount : 0;
        if (balance != null && balance.useCaptureWalker != 0 && stepDriver != null
            && stepDriver.walkActive)
        {
            walkSwap = balance.CaptureWalkStepCount;
            walkPhase = balance.CaptureWalkStateCode;
        }
        int fallCatchPhase = stepDriver != null ? stepDriver.FallCatchPhaseCode : -1;
        int fallCatchCycle = stepDriver != null ? stepDriver.FallCatchCycleCount : 0;
        maxWalkSwapCount = Mathf.Max(maxWalkSwapCount, walkSwap);
        maxFallCatchCycleCount = Mathf.Max(maxFallCatchCycleCount, fallCatchCycle);
        lastWalkPhase = walkPhase;
        lastFallCatchPhase = fallCatchPhase;
        lastStandLegCmd = standLegCmd;

        float march = motionIntent != null ? motionIntent.march : 0f;
        if (march > 0.5f)
        {
            if (Mathf.Abs(standLegCmd) > 0.5f
                && (Mathf.Abs(lastMarchStandCmd) < 0.5f
                    || standLegCmd * lastMarchStandCmd < 0f))
                marchSwapCount++;
            lastMarchStandCmd = standLegCmd;
        }

        float supportMargin = state != null ? state.supportMargin : 0f;
        bool leftGrounded = state != null && state.leftFootGrounded;
        bool rightGrounded = state != null && state.rightFootGrounded;
        bool leftHandGrounded = state != null && state.leftHandGrounded;
        bool rightHandGrounded = state != null && state.rightHandGrounded;

        // Arm drive: the commanded phase next to the delivered angle is what makes
        // the counter phase measurable, and a real hand contact is what the press
        // claims. Both are per side.
        bool handContactL = armDrive != null && armDrive.HandContactL;
        bool handContactR = armDrive != null && armDrive.HandContactR;
        float handYL = leftHandCollider != null ? leftHandCollider.transform.position.y : 0f;
        float handYR = rightHandCollider != null ? rightHandCollider.transform.position.y : 0f;
        if (armDrive != null && armDrive.WalkSwingLevel > 0.5f) samplesArmSwingOn++;
        if (handYL > maxArmHandY) maxArmHandY = handYL;

        float actHipL = MaxActivation(balance?.leftHipFlexor, balance?.leftHipExtensor);
        float actHipR = MaxActivation(balance?.rightHipFlexor, balance?.rightHipExtensor);
        float actKneeL = MaxActivation(balance?.leftKneeFlexor, balance?.leftKneeExtensor);
        float actKneeR = MaxActivation(balance?.rightKneeFlexor, balance?.rightKneeExtensor);
        float actAnkleL = MaxActivation(balance?.leftAnkleFlexor, balance?.leftAnkleExtensor);
        float actAnkleR = MaxActivation(balance?.rightAnkleFlexor, balance?.rightAnkleExtensor);

        BalanceController.BalanceState currentState = balance != null
            ? balance.currentState
            : BalanceController.BalanceState.Balancing;

        // ─── METRIC ACCUMULATION ───
        sumComOffsetSquared += (double)comOffset * comOffset;
        sumAbsComOffset += Mathf.Abs(comOffset);
        sumTorsoAngVelSquared += (double)angVel * angVel;
        maxAbsTilt = Mathf.Max(maxAbsTilt, Mathf.Abs(tilt));
        minHeadY = Mathf.Min(minHeadY, headY);
        if (leftHandCollider != null)
            minHandY = Mathf.Min(minHandY, leftHandCollider.bounds.min.y);
        if (rightHandCollider != null)
            minHandY = Mathf.Min(minHandY, rightHandCollider.bounds.min.y);
        lastHeadY = headY;
        minPelvisY = Mathf.Min(minPelvisY, pelvisY);
        lastPelvisY = pelvisY;
        maxCrouchLevel = Mathf.Max(maxCrouchLevel, crouchLevel);
        maxStandLegLevel = Mathf.Max(maxStandLegLevel, standLegLevel);
        lastTilt = tilt;
        lastComOffset = comOffset;

        // ─── PHYSICAL FALL ───
        // Tilt / head / pelvis only, never the controller state: the automaton
        // can park at Recovery while the body lies on the dirt (p_sw1).
        float pelvisDropNow = startPelvisY - pelvisY;
        float headRelToStandingPelvis = headY - startPelvisY;
        downNow = (Mathf.Abs(tilt) >= DOWN_TILT_DEGREES && pelvisDropNow >= DOWN_PELVIS_DROP)
            || (pelvisDropNow >= DOWN_DEEP_DROP && headRelToStandingPelvis <= 0f);
        if (downNow)
        {
            downAge += Time.fixedDeltaTime;
            samplesDown++;
            // Real palm contact while the body is down: the press needs it, and
            // "the palm is at the slab" was already being logged as grounded.
            samplesDownArm++;
            if (handContactL) samplesDownHandContactL++;
            if (handContactR) samplesDownHandContactR++;
            // The run's FIRST frame, not the end of the settle. A single-frame
            // crossing mid-dive (p_arms4: 6.115, then |tilt| 45.2 -> 26.6 deg
            // with the pelvis still dropping) is rejected by the settle on
            // purpose and the latch lands on the first sustained run.
            if (physicalFallTime < 0f && downAge >= DOWN_SETTLE_SECONDS)
                physicalFallTime = time - (downAge - Time.fixedDeltaTime);
        }
        else
        {
            downAge = 0f;
        }

        if (state != null)
        {
            lastSensorTick = state.sensorTick;
            float sensorX = state.comPosition.x;
            if (sensorX < sensorComXMin) sensorComXMin = sensorX;
            if (sensorX > sensorComXMax) sensorComXMax = sensorX;
            if (supportMargin < sensorMarginMin) sensorMarginMin = supportMargin;
            if (supportMargin > sensorMarginMax) sensorMarginMax = supportMargin;
        }
        if (com != null)
            comBodyCount = com.TrackedBodyCount;
        if (pelvisY < sensorPelvisYMin) sensorPelvisYMin = pelvisY;
        if (pelvisY > sensorPelvisYMax) sensorPelvisYMax = pelvisY;

        float hipL = state != null ? state.leftHipAngle : 0f;
        float hipR = state != null ? state.rightHipAngle : 0f;
        float kneeL = state != null ? state.leftKneeAngle : 0f;
        float kneeR = state != null ? state.rightKneeAngle : 0f;
        minHipAngle = Mathf.Min(minHipAngle, Mathf.Min(hipL, hipR));
        maxHipAngle = Mathf.Max(maxHipAngle, Mathf.Max(hipL, hipR));
        minKneeAngle = Mathf.Min(minKneeAngle, Mathf.Min(kneeL, kneeR));
        maxKneeAngle = Mathf.Max(maxKneeAngle, Mathf.Max(kneeL, kneeR));
        maxAbsHipAngle = Mathf.Max(maxAbsHipAngle, Mathf.Max(Mathf.Abs(hipL), Mathf.Abs(hipR)));
        maxAbsKneeAngle = Mathf.Max(maxAbsKneeAngle, Mathf.Max(Mathf.Abs(kneeL), Mathf.Abs(kneeR)));

        bool inCrouchWindow = crouchStartTime >= 0f && time >= crouchStartTime
            && (crouchReleaseTime < 0f || time <= crouchReleaseTime);
        if (inCrouchWindow)
        {
            samplesCrouchWindow++;
            if (leftGrounded && rightGrounded) samplesBothFeetCrouch++;
            if (leftHandGrounded && rightHandGrounded) samplesBothHandsCrouch++;
            if (leftHandGrounded) samplesLeftHandCrouch++;
            if (rightHandGrounded) samplesRightHandCrouch++;
        }

        bool inStandLegWindow = standLegStartTime >= 0f && time >= standLegStartTime
            && (standLegReleaseTime < 0f || time <= standLegReleaseTime);
        // Walk: support window — Stance phases (not transfer), sign from standLegCmd.
        bool inWalkStanceWindow = stepDriver != null && stepDriver.walkActive
            && walkPhase == 0 && Mathf.Abs(standLegCmd) > 0.5f;
        bool inCaptureWalkWindow = balance != null && balance.useCaptureWalker != 0
            && stepDriver != null && stepDriver.walkActive;
        if (inStandLegWindow || inWalkStanceWindow || inCaptureWalkWindow)
        {
            samplesStandLegWindow++;
            if (leftGrounded && rightGrounded) samplesBothFeetStandLeg++;
            if (inCaptureWalkWindow)
            {
                bool leftIsStance = balance.CaptureStanceFootSign < 0;
                if (leftIsStance)
                {
                    if (leftGrounded) samplesStanceGrounded++;
                    if (rightGrounded) samplesSwingGrounded++;
                }
                else
                {
                    if (rightGrounded) samplesStanceGrounded++;
                    if (leftGrounded) samplesSwingGrounded++;
                }
            }
            else
            {
                float stanceSign = inStandLegWindow ? requestedStandLeg : standLegCmd;
                bool leftIsStance = stanceSign < -0.5f;
                bool rightIsStance = stanceSign > 0.5f;
                if (leftIsStance)
                {
                    if (leftGrounded) samplesStanceGrounded++;
                    if (rightGrounded) samplesSwingGrounded++;
                }
                else if (rightIsStance)
                {
                    if (rightGrounded) samplesStanceGrounded++;
                    if (leftGrounded) samplesSwingGrounded++;
                }
            }
        }

        if (inCaptureWalkWindow && stepDriver.WalkStartTime >= 0f
            && time >= stepDriver.WalkStartTime
            && time <= stepDriver.WalkStartTime + 3f)
        {
            samplesOnsetWindow++;
            if (balance.CaptureWalkCollapsed) samplesOnsetCollapsed++;
            if (balance.CaptureWalkPosed) samplesOnsetPosed++;
        }

        if (leftGrounded) samplesLeftGrounded++;
        if (rightGrounded) samplesRightGrounded++;
        float lumbar = state != null ? state.lumbarAngle : 0f;
        float pelvisTilt = state != null ? state.pelvisTilt : 0f;
        lastLumbarAngle = lumbar;
        lastPelvisTilt = pelvisTilt;
        maxAbsLumbarAngle = Mathf.Max(maxAbsLumbarAngle, Mathf.Abs(lumbar));
        maxAbsPelvisTilt = Mathf.Max(maxAbsPelvisTilt, Mathf.Abs(pelvisTilt));

        if (march > 0.5f && Mathf.Abs(standLegCmd) > 0.5f)
        {
            bool leftIsSwingNow = standLegCmd > 0.5f;
            bool swingAirNow = leftIsSwingNow ? !leftGrounded : !rightGrounded;
            if (swingAirNow)
            {
                marchAirAge += Time.fixedDeltaTime;
                if (marchAirAge >= 0.4f)
                {
                    samplesMarchAir++;
                    float swingKnee = leftIsSwingNow ? kneeL : kneeR;
                    float swingHip = leftIsSwingNow ? hipL : hipR;
                    sumMarchSwingKnee += swingKnee;
                    maxMarchSwingKnee = Mathf.Max(maxMarchSwingKnee, swingKnee);
                    sumMarchSwingHip += swingHip;
                    minMarchSwingHip = Mathf.Min(minMarchSwingHip, swingHip);
                    marchPelvisMin = Mathf.Min(marchPelvisMin, pelvisTilt);
                    marchPelvisMax = Mathf.Max(marchPelvisMax, pelvisTilt);
                }
            }
            else
                marchAirAge = 0f;
        }
        else
            marchAirAge = 0f;

        bool walkActiveNow = stepDriver != null && stepDriver.walkActive;
        if (inStandLegWindow && !walkActiveNow && Mathf.Abs(requestedStandLeg) > 0.5f)
        {
            bool swingAir = requestedStandLeg > 0.5f ? !leftGrounded : !rightGrounded;
            if (swingAir)
            {
                if (oneLegAirStartTime < 0f)
                    oneLegAirStartTime = time;
                if (time >= oneLegAirStartTime + OneLegHoldSettleSeconds)
                {
                    if (oneLegHoldStartTime < 0f)
                        oneLegHoldStartTime = time;
                    samplesOneLegHold++;
                    oneLegPelvisMin = Mathf.Min(oneLegPelvisMin, pelvisTilt);
                    oneLegPelvisMax = Mathf.Max(oneLegPelvisMax, pelvisTilt);
                    oneLegComXMin = Mathf.Min(oneLegComXMin, comPosition.x);
                    oneLegComXMax = Mathf.Max(oneLegComXMax, comPosition.x);
                    if (oneLegHasPrevPelvis)
                    {
                        float d = pelvisTilt - oneLegLastPelvis;
                        if (oneLegLastPelvisDelta * d < 0f && Mathf.Abs(d) > 0.0001f)
                            oneLegPelvisReversals++;
                        oneLegLastPelvisDelta = d;
                    }
                    oneLegLastPelvis = pelvisTilt;
                    oneLegHasPrevPelvis = true;
                }
            }
        }

        if (balance != null)
        {
            if (balance.WalkLiftPhaseCode > 0)
                samplesWalkLiftOwns++;
            float lead = balance.lastWalkLiftComLeadApplied;
            sumWalkLiftComLead += lead;
            maxAbsWalkLiftComLead = Mathf.Max(maxAbsWalkLiftComLead, Mathf.Abs(lead));
            if (balance.WalkLiftPhaseCode == 3)
            {
                samplesWalkLiftAdvance++;
                float plant = balance.lastWalkLiftCaptureStep;
                if (plant > maxWalkLiftCaptureStep)
                    maxWalkLiftCaptureStep = plant;
                float vel = balance.lastWalkLiftComVelX;
                float h = Mathf.Max(0.20f, balance.walkXCoMHeight);
                float omega = Mathf.Sqrt(9.81f / h);
                float xiNowLead = Mathf.Abs(vel) / Mathf.Max(0.1f, omega);
                if (xiNowLead > maxWalkLiftXiLead)
                    maxWalkLiftXiLead = xiNowLead;
                if (plant > balance.walkLiftStepLength + 0.001f)
                    samplesWalkLiftPlantOverFloor++;
            }
            if (Mathf.Abs(balance.lastWalkAnkleFwdRaw - balance.lastWalkAnkleFwdDrive) > 0.0001f)
                samplesWalkAnkleFwdClip++;
            if (balance.lastWalkAnkleDorsiFade < 0.999f)
                samplesWalkAnkleDorsiFade++;
        }

        if (pushTime >= 0f && time >= pushTime)
        {
            maxAbsComOffsetAfterPush = Mathf.Max(maxAbsComOffsetAfterPush, Mathf.Abs(comOffset));
            maxAbsTiltAfterPush = Mathf.Max(maxAbsTiltAfterPush, Mathf.Abs(tilt));
            if (Mathf.Abs(comOffset) > SETTLED_COM_OFFSET) lastUnsettledTime = time;
        }

        switch (currentState)
        {
            case BalanceController.BalanceState.Balancing: samplesBalancing++; break;
            case BalanceController.BalanceState.Recovery: samplesRecovery++; break;
            case BalanceController.BalanceState.Falling: samplesFalling++; break;
        }

        if (balance != null)
        {
            switch (balance.locomotionState)
            {
                case BalanceController.LocomotionState.GettingUp:
                    samplesGettingUp++;
                    break;
                case BalanceController.LocomotionState.Fallen:
                    samplesFallenLoco++;
                    break;
                case BalanceController.LocomotionState.Walking:
                    if (balance.getUpSuccess)
                        samplesGetUpWalk++;
                    break;
            }
        }

        if (currentState == BalanceController.BalanceState.Falling && fallTime < 0f)
            fallTime = time;

        if (Mathf.Max(Mathf.Max(actHipL, actHipR), Mathf.Max(actKneeL, actKneeR)) >= SATURATION_THRESHOLD)
            samplesSaturated++;

        if (leftGrounded && rightGrounded) samplesBothFeet++;

        SampleDampingWatch(neckWatch);
        SampleDampingWatch(headWatch);
        SampleDampingWatch(shoulderWatch);
        SampleDampingWatch(elbowWatch);
        SampleDampingWatch(wristWatch);

        // ─── WALK-1 command tap ───
        // The walker's own per-frame record. tapAge is the frame gap between
        // the publish and this sample: 0-1 is the same physics step, a larger
        // number means the walker did NOT tick (its rows are then stale).
        BalanceController.CaptureWalkTap tap = balance != null
            ? balance.walkTap
            : default(BalanceController.CaptureWalkTap);
        int tapAge = tap.active == 1 ? Time.frameCount - tap.tick : -1;
        int tapActive = tap.active == 1 && tapAge >= 0 && tapAge <= 1 ? 1 : 0;
        // Time.frameCount does not advance one-for-one with the physics in the
        // headless loop, so freshness is judged on the walker's OWN counter:
        // tapFresh 1 = the walker ticked since the previous row. A run of
        // tapFresh 0 means every tap value below is a frozen last command.
        int tapFresh = lastTapTicks < 0 || tap.walkTicks != lastTapTicks ? 1 : 0;
        lastTapTicks = tap.walkTicks;
        // WALK-1 completeness: one owner + one finite request per leg joint,
        // whatever layer held it (see CompleteLegRequestRecord).
        CompleteLegRequestRecord(tap, tapActive);
        float soleClearL = state != null ? state.leftFootGroundClearance : float.NaN;
        float soleClearR = state != null ? state.rightFootGroundClearance : float.NaN;

        // The same column set, without new[] and Join on every 200 Hz step.
        rows.Append(F(time)).Append(';')
            .Append(((int)currentState).ToString(CultureInfo.InvariantCulture)).Append(';')
            .Append(F(tilt)).Append(';')
            .Append(F(angVel)).Append(';')
            .Append(F(state != null ? state.pelvisTilt : 0f)).Append(';')
            .Append(F(comPosition.x)).Append(';')
            .Append(F(comOffset)).Append(';')
            .Append(F(supportMargin)).Append(';')
            .Append(leftGrounded ? "1" : "0").Append(';')
            .Append(rightGrounded ? "1" : "0").Append(';')
            .Append(F(headY)).Append(';')
            .Append(F(state != null ? state.leftHipAngle : 0f)).Append(';')
            .Append(F(state != null ? state.rightHipAngle : 0f)).Append(';')
            .Append(F(state != null ? state.leftKneeAngle : 0f)).Append(';')
            .Append(F(state != null ? state.rightKneeAngle : 0f)).Append(';')
            .Append(F(state != null ? state.leftAnkleAngle : 0f)).Append(';')
            .Append(F(state != null ? state.rightAnkleAngle : 0f)).Append(';')
            .Append(F(state != null ? state.lumbarAngle : 0f)).Append(';')
            .Append(F(actHipL)).Append(';')
            .Append(F(actHipR)).Append(';')
            .Append(F(actKneeL)).Append(';')
            .Append(F(actKneeR)).Append(';')
            .Append(F(actAnkleL)).Append(';')
            .Append(F(actAnkleR)).Append(';')
            .Append(F(pelvisY)).Append(';')
            .Append(F(crouchLevel)).Append(';')
            .Append(F(standLegLevel)).Append(';')
            .Append(F(standLegCmd)).Append(';')
            .Append(walkPhase.ToString(CultureInfo.InvariantCulture)).Append(';')
            .Append(walkSwap.ToString(CultureInfo.InvariantCulture)).Append(';')
            .Append(fallCatchPhase.ToString(CultureInfo.InvariantCulture)).Append(';')
            .Append(fallCatchCycle.ToString(CultureInfo.InvariantCulture)).Append(';')
            .Append(F(dualSoft)).Append(';')
            .Append(F(WatchAngle(neckWatch))).Append(';')
            .Append(F(WatchAngle(headWatch))).Append(';')
            .Append(F(WatchAngle(shoulderWatch))).Append(';')
            .Append(F(WatchAngle(elbowWatch))).Append(';')
            .Append(inCaptureWalkWindow && balance.CaptureWalkCollapsed ? "1" : "0").Append(';')
            .Append(inCaptureWalkWindow && balance.CaptureWalkPosed ? "1" : "0").Append(';')
            .Append(F(WatchAngle(shoulderWatch, 0))).Append(';')
            .Append(F(WatchAngle(shoulderWatch, 1))).Append(';')
            .Append(F(WatchAngle(elbowWatch, 0))).Append(';')
            .Append(F(WatchAngle(elbowWatch, 1))).Append(';')
            .Append(F(MaxActivation(balance?.leftShoulderFlexor, balance?.leftShoulderExtensor))).Append(';')
            .Append(F(MaxActivation(balance?.rightShoulderFlexor, balance?.rightShoulderExtensor))).Append(';')
            .Append(F(MaxActivation(balance?.leftElbowFlexor, balance?.leftElbowExtensor))).Append(';')
            .Append(F(MaxActivation(balance?.rightElbowFlexor, balance?.rightElbowExtensor))).Append(';')
            .Append(leftHandGrounded ? "1" : "0").Append(';')
            .Append(rightHandGrounded ? "1" : "0").Append(';')
            .Append(handContactL ? "1" : "0").Append(';')
            .Append(handContactR ? "1" : "0").Append(';')
            .Append(F(handYL)).Append(';')
            .Append(F(handYR)).Append(';')
            .Append(F(armDrive != null ? armDrive.WalkSwingLevel : 0f)).Append(';')
            .Append(F(armDrive != null ? armDrive.ArmPhaseL : 0f)).Append(';')
            .Append(F(armDrive != null ? armDrive.ArmPhaseR : 0f)).Append(';')
            .Append(F(armDrive != null ? armDrive.ArmTargetShoulderL : 0f)).Append(';')
            .Append(F(armDrive != null ? armDrive.ArmTargetShoulderR : 0f)).Append(';')
            .Append(F(armDrive != null ? armDrive.ArmTargetElbowL : 0f)).Append(';')
            .Append(F(armDrive != null ? armDrive.ArmTargetElbowR : 0f)).Append(';')
            .Append((armDrive != null ? armDrive.PressPhaseL : 0).ToString(CultureInfo.InvariantCulture)).Append(';')
            .Append((armDrive != null ? armDrive.PressPhaseR : 0).ToString(CultureInfo.InvariantCulture)).Append(';')
            .Append(F(armDrive != null ? armDrive.PressRequest : 0f)).Append(';')
            .Append(F(armDrive != null ? armDrive.PressStrokeExtensionL : 0f)).Append(';')
            .Append(F(armDrive != null ? armDrive.PressStrokeExtensionR : 0f)).Append(';')
            .Append((armDrive != null ? armDrive.PressStrokesL : 0).ToString(CultureInfo.InvariantCulture)).Append(';')
            .Append((armDrive != null ? armDrive.PressStrokesR : 0).ToString(CultureInfo.InvariantCulture)).Append(';')
            .Append(F(armDrive != null ? armDrive.PressShoulderLiftL : 0f)).Append(';')
            .Append(F(armDrive != null ? armDrive.PressShoulderLiftR : 0f)).Append(';')
            .Append(F(armDrive != null ? armDrive.PressPlantOffsetL : 0f)).Append(';')
            .Append(F(armDrive != null ? armDrive.PressPlantOffsetR : 0f)).Append(';')
            .Append(F(armDrive != null ? armDrive.PressPlantFoldL : 0f)).Append(';')
            .Append(F(armDrive != null ? armDrive.PressPlantFoldR : 0f)).Append(';')
            .Append(F(armDrive != null ? armDrive.PressAimSignL : 0f)).Append(';')
            .Append(F(armDrive != null ? armDrive.PressAimSignR : 0f)).Append(';')
            .Append(F(armDrive != null ? armDrive.PressPivotYL : 0f)).Append(';')
            .Append(F(armDrive != null ? armDrive.PressPivotYR : 0f)).Append(';')
            .Append(F(armDrive != null ? armDrive.PressPalmYL : 0f)).Append(';')
            .Append(F(armDrive != null ? armDrive.PressPalmYR : 0f)).Append(';')
            .Append(F(armDrive != null ? armDrive.PressAimHaveL : 0f)).Append(';')
            .Append(F(armDrive != null ? armDrive.PressAimHaveR : 0f)).Append(';')
            .Append(F(armDrive != null ? armDrive.PressRestL : 0f)).Append(';')
            .Append(F(armDrive != null ? armDrive.PressRestR : 0f)).Append(';')
            .Append(downNow ? "1" : "0").Append(';')
            .Append(physicalFallTime >= 0f ? "1" : "0").Append(';')
            // ─── WALK-1 command tap. Appended last, never reordered: the
            // numbers are only meaningful next to the artifact that produced
            // them (label + sourceFingerprint), and tapAge 0-1 means the
            // walker ticked on this very frame.
            .Append(tapActive).Append(';')
            .Append(tapAge).Append(';')
            .Append(tapFresh).Append(';')
            .Append(tap.walkTicks).Append(';')
            .Append(tap.abortCode).Append(';')
            // WALK-1 completeness: these twelve columns are filled for EVERY
            // row (walker code while it wrote the joint, else the layer that
            // held it - see CompleteLegRequestRecord). req* is an angle
            // request in degrees; when the owner commands torque instead, it
            // is the measured joint angle and the row's reqFallbackMask bit
            // is set. copCmd* is finite on every row: 0 = the CoP loop did
            // not command that ankle this frame (ownerAnkle* names who did).
            .Append(legOwnerOut[0]).Append(';')
            .Append(legOwnerOut[1]).Append(';')
            .Append(legOwnerOut[2]).Append(';')
            .Append(legOwnerOut[3]).Append(';')
            .Append(legOwnerOut[4]).Append(';')
            .Append(legOwnerOut[5]).Append(';')
            .Append(F(legReqOut[0])).Append(';')
            .Append(F(legReqOut[1])).Append(';')
            .Append(F(legReqOut[2])).Append(';')
            .Append(F(legReqOut[3])).Append(';')
            .Append(F(legReqOut[4])).Append(';')
            .Append(F(legReqOut[5])).Append(';')
            .Append(F(IsFinite(tap.copCmdL) ? tap.copCmdL : 0f)).Append(';')
            .Append(F(IsFinite(tap.copCmdR) ? tap.copCmdR : 0f)).Append(';')
            .Append(F(AppliedTorque(balance?.leftHipFlexor, balance?.leftHipExtensor))).Append(';')
            .Append(F(AppliedTorque(balance?.rightHipFlexor, balance?.rightHipExtensor))).Append(';')
            .Append(F(AppliedTorque(balance?.leftKneeFlexor, balance?.leftKneeExtensor))).Append(';')
            .Append(F(AppliedTorque(balance?.rightKneeFlexor, balance?.rightKneeExtensor))).Append(';')
            .Append(F(AppliedTorque(balance?.leftAnkleFlexor, balance?.leftAnkleExtensor))).Append(';')
            .Append(F(AppliedTorque(balance?.rightAnkleFlexor, balance?.rightAnkleExtensor))).Append(';')
            .Append(tap.roleL).Append(';')
            .Append(tap.roleR).Append(';')
            // gaitState* is the capture walker's own state; step* is the
            // StepPhaseDriver the walkPhase/walkSwap columns fall back to
            // when its walkActive goes false. Two counters, one row.
            .Append(tap.gaitState).Append(';')
            .Append(F(tap.gaitStateAge)).Append(';')
            .Append(stepDriver != null && stepDriver.walkActive ? 1 : 0).Append(';')
            .Append(stepDriver != null ? stepDriver.PhaseCode : -99).Append(';')
            .Append(stepDriver != null ? stepDriver.SwapCount : -99).Append(';')
            .Append(tap.dualSupport).Append(';')
            .Append(tap.swapReason).Append(';')
            .Append(tap.swapMode).Append(';')
            .Append(F(tap.swapAge)).Append(';')
            .Append(tap.gaitSteps).Append(';')
            .Append(tap.entryReason).Append(';')
            .Append(tap.gateMask).Append(';')
            .Append(tap.rejectCode).Append(';')
            .Append(tap.planValid).Append(';')
            .Append(tap.touchL).Append(';')
            .Append(tap.touchR).Append(';')
            .Append(tap.loadedL).Append(';')
            .Append(tap.loadedR).Append(';')
            .Append(tap.supportL).Append(';')
            .Append(tap.supportR).Append(';')
            .Append(F(soleClearL)).Append(';')
            .Append(F(soleClearR)).Append(';')
            .Append(stepDriver != null && stepDriver.StandingRestore ? 1 : 0).Append(';')
            .Append(stepDriver != null && stepDriver.StandingSurrender ? 1 : 0).Append(';')
            .Append(balance != null ? (int)balance.locomotionState : -1).Append(';')
            // WALK-1 completeness: the layer that held the legs this step, the
            // per-joint "req* is a held angle" mask, and the per-joint demand
            // before the owner's own clamp (see the header comment).
            .Append(legLayerOut).Append(';')
            .Append(legFallbackMask).Append(';')
            .Append(F(legDemandOut[0])).Append(';')
            .Append(F(legDemandOut[1])).Append(';')
            .Append(F(legDemandOut[2])).Append(';')
            .Append(F(legDemandOut[3])).Append(';')
            .Append(F(legDemandOut[4])).Append(';')
            .Append(F(legDemandOut[5])).AppendLine();
    }

    private static float WatchAngle(JointDampingWatch watch, int index)
    {
        return watch != null ? watch.AngleAt(index) : 0f;
    }

    private static float WatchAngle(JointDampingWatch watch)
    {
        return watch != null ? watch.FirstAngle() : 0f;
    }

    public string BuildCsv()
    {
        return rows.ToString();
    }

    public string BuildSummaryJson(float simulatedSeconds, float wallSeconds = -1f)
    {
        float rmsComOffset = samples > 0 ? Mathf.Sqrt((float)(sumComOffsetSquared / samples)) : 0f;
        float meanAbsComOffset = samples > 0 ? (float)(sumAbsComOffset / samples) : 0f;
        float rmsTorsoAngVel = samples > 0 ? Mathf.Sqrt((float)(sumTorsoAngVelSquared / samples)) : 0f;
        // Physical fall latch, not the BalanceState automaton: the automaton
        // never enters Falling once the get-up path owns the body (p_sw1).
        float survived = physicalFallTime >= 0f ? physicalFallTime : simulatedSeconds;
        float headDrop = startHeadY - lastHeadY;
        float pelvisDrop = startPelvisY - minPelvisY;
        float pelvisRecoverError = startPelvisY - lastPelvisY;
        bool returnedToStand = crouchReleaseTime >= 0f
            && physicalFallTime < 0f
            && pelvisRecoverError <= PELVIS_RECOVER_TOLERANCE;
        float crouchBothFeet = samplesCrouchWindow > 0
            ? samplesBothFeetCrouch / (float)samplesCrouchWindow
            : 0f;
        float crouchBothHands = samplesCrouchWindow > 0
            ? samplesBothHandsCrouch / (float)samplesCrouchWindow
            : 0f;
        float crouchLeftHand = samplesCrouchWindow > 0
            ? samplesLeftHandCrouch / (float)samplesCrouchWindow
            : 0f;
        float crouchRightHand = samplesCrouchWindow > 0
            ? samplesRightHandCrouch / (float)samplesCrouchWindow
            : 0f;
        float standLegBothFeet = samplesStandLegWindow > 0
            ? samplesBothFeetStandLeg / (float)samplesStandLegWindow
            : 0f;
        float swingFootGrounded = samplesStandLegWindow > 0
            ? samplesSwingGrounded / (float)samplesStandLegWindow
            : 0f;
        float stanceFootGrounded = samplesStandLegWindow > 0
            ? samplesStanceGrounded / (float)samplesStandLegWindow
            : 0f;
        // How many seconds after the push the CoM offset stayed outside the settled band.
        float recoverySeconds = pushTime >= 0f && lastUnsettledTime >= 0f
            ? lastUnsettledTime - pushTime
            : 0f;

        StringBuilder json = new StringBuilder();
        json.Append("{");
        AppendStr(json, "label", label, false);
        AppendNum(json, "simulatedSeconds", simulatedSeconds);
        if (wallSeconds >= 0f)
        {
            AppendNum(json, "wallSeconds", wallSeconds);
            if (simulatedSeconds > 0f)
                AppendNum(json, "wallPerSim", wallSeconds / simulatedSeconds);
        }
        json.Append(",\"samples\":").Append(samples.ToString(CultureInfo.InvariantCulture));
        AppendNum(json, "fixedDeltaTime", Time.fixedDeltaTime);
        AppendNum(json, "muscleActivationSpeed", balance != null ? balance.muscleActivationSpeed : 0f);
        AppendNum(json, "survivedSeconds", survived);
        json.Append(",\"fell\":").Append(physicalFallTime >= 0f ? "true" : "false");
        // Where the flag came from. fallTime is the old automaton latch, kept
        // for the cross-check: it disagreed with the body in 19 runs on disk.
        AppendNum(json, "fallTime", physicalFallTime);
        AppendNum(json, "fallAutomatonTime", fallTime);
        AppendNum(json, "downSeconds", samplesDown * Time.fixedDeltaTime);
        AppendNum(json, "downFraction", Fraction(samplesDown));
        AppendNum(json, "maxAbsTorsoTilt", maxAbsTilt);
        AppendNum(json, "maxAbsLumbarAngle", maxAbsLumbarAngle);
        AppendNum(json, "maxAbsPelvisTilt", maxAbsPelvisTilt);
        AppendNum(json, "finalPelvisTilt", lastPelvisTilt);
        AppendNum(json, "finalLumbarAngle", lastLumbarAngle);
        AppendNum(json, "finalTorsoTilt", lastTilt);
        AppendNum(json, "rmsComOffset", rmsComOffset);
        AppendNum(json, "rmsTorsoAngVel", rmsTorsoAngVel);
        AppendNum(json, "meanAbsComOffset", meanAbsComOffset);
        AppendNum(json, "finalComOffset", lastComOffset);
        AppendNum(json, "comTravelX", lastComX - startComX);
        AppendNum(json, "comSpeedX",
            simulatedSeconds > 0f ? (lastComX - startComX) / simulatedSeconds : 0f);
        AppendNum(json, "headDrop", headDrop);
        AppendNum(json, "minHeadY", minHeadY);
        AppendNum(json, "pushImpulse", pushImpulse);
        AppendNum(json, "pushTime", pushTime);
        AppendNum(json, "maxComOffsetAfterPush", maxAbsComOffsetAfterPush);
        AppendNum(json, "maxTiltAfterPush", maxAbsTiltAfterPush);
        AppendNum(json, "recoverySeconds", recoverySeconds);
        AppendNum(json, "muscleSaturationFraction", Fraction(samplesSaturated));
        AppendNum(json, "bothFeetGroundedFraction", Fraction(samplesBothFeet));
        AppendNum(json, "leftFootGroundedFraction", Fraction(samplesLeftGrounded));
        AppendNum(json, "rightFootGroundedFraction", Fraction(samplesRightGrounded));
        AppendNum(json, "balancingFraction", Fraction(samplesBalancing));
        AppendNum(json, "recoveryFraction", Fraction(samplesRecovery));
        AppendNum(json, "fallingFraction", Fraction(samplesFalling));
        AppendNum(json, "startPelvisY", startPelvisY);
        AppendNum(json, "minPelvisY", minPelvisY);
        AppendNum(json, "finalPelvisY", lastPelvisY);
        AppendNum(json, "pelvisDrop", pelvisDrop);
        AppendNum(json, "pelvisRecoverError", pelvisRecoverError);
        json.Append(",\"returnedToStand\":").Append(returnedToStand ? "true" : "false");
        AppendNum(json, "minHipAngle", FiniteOrZero(minHipAngle));
        AppendNum(json, "maxHipAngle", FiniteOrZero(maxHipAngle));
        AppendNum(json, "minKneeAngle", FiniteOrZero(minKneeAngle));
        AppendNum(json, "maxKneeAngle", FiniteOrZero(maxKneeAngle));
        AppendNum(json, "maxAbsHipAngle", maxAbsHipAngle);
        AppendNum(json, "maxAbsKneeAngle", maxAbsKneeAngle);
        AppendNum(json, "maxCrouchLevel", maxCrouchLevel);
        AppendNum(json, "crouchBothFeetGroundedFraction", crouchBothFeet);
        AppendNum(json, "minHandY", minHandY > 100f ? 0f : minHandY);
        AppendNum(json, "crouchBothHandsGroundedFraction", crouchBothHands);
        AppendNum(json, "crouchLeftHandGroundedFraction", crouchLeftHand);
        AppendNum(json, "crouchRightHandGroundedFraction", crouchRightHand);
        AppendNum(json, "crouchTime", crouchStartTime);
        AppendNum(json, "crouchReleaseTime", crouchReleaseTime);
        AppendNum(json, "requestedCrouch", requestedCrouchLevel);
        AppendNum(json, "maxStandLegLevel", maxStandLegLevel);
        AppendNum(json, "standLegBothFeetGroundedFraction", standLegBothFeet);
        AppendNum(json, "swingFootGroundedFraction", swingFootGrounded);
        AppendNum(json, "stanceFootGroundedFraction", stanceFootGrounded);
        AppendNum(json, "standLegTime", standLegStartTime);
        AppendNum(json, "standLegReleaseTime", standLegReleaseTime);
        AppendNum(json, "requestedStandLeg", requestedStandLeg);
        float oneLegHoldSeconds = samplesOneLegHold * Time.fixedDeltaTime;
        float oneLegPelvisSpan = samplesOneLegHold > 0
            ? oneLegPelvisMax - oneLegPelvisMin
            : 0f;
        float oneLegComDriftX = samplesOneLegHold > 0
            ? oneLegComXMax - oneLegComXMin
            : 0f;
        float oneLegPelvisReversalHz = oneLegHoldSeconds > 0.001f
            ? oneLegPelvisReversals / oneLegHoldSeconds
            : 0f;
        AppendNum(json, "oneLegHoldSeconds", oneLegHoldSeconds);
        AppendNum(json, "oneLegHoldStartTime", oneLegHoldStartTime);
        AppendNum(json, "oneLegPelvisMin", samplesOneLegHold > 0 ? oneLegPelvisMin : 0f);
        AppendNum(json, "oneLegPelvisMax", samplesOneLegHold > 0 ? oneLegPelvisMax : 0f);
        AppendNum(json, "oneLegPelvisSpan", oneLegPelvisSpan);
        AppendNum(json, "oneLegComDriftX", oneLegComDriftX);
        AppendNum(json, "oneLegPelvisReversalHz", oneLegPelvisReversalHz);
        AppendNum(json, "marchSwapCount", marchSwapCount);
        AppendNum(json, "marchAirSeconds", samplesMarchAir * Time.fixedDeltaTime);
        AppendNum(json, "marchSwingKneeMean",
            samplesMarchAir > 0 ? sumMarchSwingKnee / samplesMarchAir : 0f);
        AppendNum(json, "marchSwingKneeMax",
            samplesMarchAir > 0 ? maxMarchSwingKnee : 0f);
        AppendNum(json, "marchSwingHipMean",
            samplesMarchAir > 0 ? sumMarchSwingHip / samplesMarchAir : 0f);
        AppendNum(json, "marchSwingHipMin",
            samplesMarchAir > 0 ? minMarchSwingHip : 0f);
        AppendNum(json, "marchPelvisSpan",
            samplesMarchAir > 0 ? marchPelvisMax - marchPelvisMin : 0f);
        AppendNum(json, "walkSwapCount", maxWalkSwapCount);
        AppendNum(json, "captureWalkStepCount",
            balance != null ? balance.CaptureWalkStepCount : 0f);
        AppendNum(json, "sensorTick", lastSensorTick);
        AppendNum(json, "comBodyCount", comBodyCount);
        AppendNum(json, "sensorComXMin",
            samples > 0 && sensorComXMin < float.MaxValue * 0.5f ? sensorComXMin : 0f);
        AppendNum(json, "sensorComXMax",
            samples > 0 && sensorComXMax > float.MinValue * 0.5f ? sensorComXMax : 0f);
        AppendNum(json, "sensorSupportMarginMin",
            samples > 0 && sensorMarginMin < float.MaxValue * 0.5f ? sensorMarginMin : 0f);
        AppendNum(json, "sensorSupportMarginMax",
            samples > 0 && sensorMarginMax > float.MinValue * 0.5f ? sensorMarginMax : 0f);
        AppendNum(json, "sensorPelvisYMin",
            samples > 0 && sensorPelvisYMin < float.MaxValue * 0.5f ? sensorPelvisYMin : 0f);
        AppendNum(json, "sensorPelvisYMax",
            samples > 0 && sensorPelvisYMax > float.MinValue * 0.5f ? sensorPelvisYMax : 0f);
        AppendNum(json, "walkOnsetSeconds", samplesOnsetWindow * Time.fixedDeltaTime);
        AppendNum(json, "walkOnsetCollapsedFraction",
            samplesOnsetWindow > 0 ? samplesOnsetCollapsed / (float)samplesOnsetWindow : 0f);
        AppendNum(json, "walkOnsetPosedFraction",
            samplesOnsetWindow > 0 ? samplesOnsetPosed / (float)samplesOnsetWindow : 0f);
        json.Append(",\"walkOnsetCollapsed\":")
            .Append(samplesOnsetCollapsed > 0 ? "true" : "false");
        json.Append(",\"walkOnsetPosed\":")
            .Append(samplesOnsetPosed > 0 ? "true" : "false");
        AppendNum(json, "locomotionState",
            balance != null ? (float)(int)balance.locomotionState : 0f);
        AppendNum(json, "getUpAttempts",
            balance != null ? balance.getUpAttempts : 0f);
        json.Append(",\"getUpSuccess\":")
            .Append(balance != null && balance.getUpSuccess ? "true" : "false");
        AppendNum(json, "getUpSeconds",
            balance != null ? balance.getUpSeconds : -1f);
        AppendNum(json, "getUpFallTime",
            balance != null ? balance.getUpFallTime : -1f);
        AppendNum(json, "getUpPose",
            balance != null ? balance.getUpPose : 0f);
        AppendNum(json, "getUpPoseMax",
            balance != null ? balance.getUpPoseMax : 0f);
        json.Append(",\"getUpReachedLying\":")
            .Append(balance != null && balance.getUpPoseMax >= 1 ? "true" : "false");
        json.Append(",\"getUpReachedSitting\":")
            .Append(balance != null && balance.getUpPoseMax >= 2 ? "true" : "false");
        json.Append(",\"getUpReachedKneesTucked\":")
            .Append(balance != null && balance.getUpPoseMax >= 3 ? "true" : "false");
        json.Append(",\"getUpReachedStanding\":")
            .Append(balance != null && balance.getUpPoseMax >= 4 ? "true" : "false");
        AppendNum(json, "gettingUpSeconds", samplesGettingUp * Time.fixedDeltaTime);
        AppendNum(json, "fallenLocoSeconds", samplesFallenLoco * Time.fixedDeltaTime);
        AppendNum(json, "getUpWalkSeconds", samplesGetUpWalk * Time.fixedDeltaTime);
        AppendStr(json, "walkOnsetLog", FormatCaptureOnsetLog());
        AppendNum(json, "walkLiftOwnsFraction", Fraction(samplesWalkLiftOwns));
        AppendNum(json, "walkLiftAdvanceFraction", Fraction(samplesWalkLiftAdvance));
        AppendNum(json, "walkLiftPlantOverFloorFraction",
            samplesWalkLiftAdvance > 0
                ? samplesWalkLiftPlantOverFloor / (float)samplesWalkLiftAdvance
                : 0f);
        AppendNum(json, "maxWalkLiftCaptureStep", maxWalkLiftCaptureStep);
        AppendNum(json, "maxWalkLiftXiLead", maxWalkLiftXiLead);
        AppendNum(json, "walkAnkleFwdClipFraction", Fraction(samplesWalkAnkleFwdClip));
        AppendNum(json, "walkAnkleDorsiFadeFraction", Fraction(samplesWalkAnkleDorsiFade));
        AppendNum(json, "meanWalkLiftComLead",
            samples > 0 ? sumWalkLiftComLead / samples : 0f);
        AppendNum(json, "maxAbsWalkLiftComLead", maxAbsWalkLiftComLead);
        AppendNum(json, "fallCatchCycleCount", maxFallCatchCycleCount);
        AppendNum(json, "finalFallCatchPhase", lastFallCatchPhase);
        AppendNum(json, "walkTransferReadyFrames",
            stepDriver != null ? stepDriver.TransferReadyFrames : 0f);
        AppendNum(json, "finalWalkPhase", lastWalkPhase);
        AppendNum(json, "finalStandLegCmd", lastStandLegCmd);
        AppendNum(json, "rmsNeckJointSpeed", RmsSpeed(neckWatch));
        AppendNum(json, "rmsHeadJointSpeed", RmsSpeed(headWatch));
        AppendNum(json, "rmsShoulderJointSpeed", RmsSpeed(shoulderWatch));
        AppendNum(json, "rmsElbowJointSpeed", RmsSpeed(elbowWatch));
        AppendNum(json, "rmsWristJointSpeed", RmsSpeed(wristWatch));
        AppendNum(json, "neckLimitFraction", LimitFraction(neckWatch));
        AppendNum(json, "headLimitFraction", LimitFraction(headWatch));
        AppendNum(json, "shoulderLimitFraction", LimitFraction(shoulderWatch));
        AppendNum(json, "elbowLimitFraction", LimitFraction(elbowWatch));
        AppendNum(json, "wristLimitFraction", LimitFraction(wristWatch));
        // Arm drive, per side. armSwingFraction: how much of the run the walk
        // counter swing was engaged. armHandContactFraction*: real palm-on-ground
        // contacts while the body is down (handGrounded* is only the slop test).
        // armPressStroke*: the last stroke of the fall press.
        AppendNum(json, "armSwingFraction",
            samples > 0 ? samplesArmSwingOn / (float)samples : 0f);
        AppendNum(json, "armHandContactFractionL",
            samplesDownArm > 0 ? samplesDownHandContactL / (float)samplesDownArm : 0f);
        AppendNum(json, "armHandContactFractionR",
            samplesDownArm > 0 ? samplesDownHandContactR / (float)samplesDownArm : 0f);
        AppendNum(json, "armPressStrokesL", armDrive != null ? armDrive.PressStrokesL : 0f);
        AppendNum(json, "armPressStrokesR", armDrive != null ? armDrive.PressStrokesR : 0f);
        AppendNum(json, "armPressStrokeMaxDegL", armDrive != null ? armDrive.PressStrokeExtensionL : 0f);
        AppendNum(json, "armPressStrokeMaxDegR", armDrive != null ? armDrive.PressStrokeExtensionR : 0f);
        AppendNum(json, "armPressShoulderMaxDegL", armDrive != null ? armDrive.PressStrokeShoulderExtL : 0f);
        AppendNum(json, "armPressShoulderMaxDegR", armDrive != null ? armDrive.PressStrokeShoulderExtR : 0f);
        AppendNum(json, "armPressLiftMaxMeters", armDrive != null ? armDrive.PressLiftMax : 0f);
        // Shoulder rise inside a stroke, per side: with the palm latched on the
        // slab this is the push itself. The pelvis number above only follows it
        // if the legs hold, so on its own it cannot say whether the arms worked.
        AppendNum(json, "armPressShoulderLiftL", armDrive != null ? armDrive.PressShoulderLiftL : 0f);
        AppendNum(json, "armPressShoulderLiftR", armDrive != null ? armDrive.PressShoulderLiftR : 0f);
        AppendNum(json, "armPressPlantOffsetL", armDrive != null ? armDrive.PressPlantOffsetL : 0f);
        AppendNum(json, "armPressPlantOffsetR", armDrive != null ? armDrive.PressPlantOffsetR : 0f);
        // The elbow angle the plant asked for, degrees: the fold the stroke starts
        // from, so the extension it had available. A stroke that starts near 0 had
        // no room and could not be a press whatever the body did.
        AppendNum(json, "armPressPlantFoldL", armDrive != null ? armDrive.PressPlantFoldL : 0f);
        AppendNum(json, "armPressPlantFoldR", armDrive != null ? armDrive.PressPlantFoldR : 0f);
        AppendNum(json, "armPressRequest", armDrive != null ? armDrive.PressRequest : 0f);
        AppendNum(json, "armMaxHandY", maxArmHandY);
        AppendNum(json, "muscleMultiplier", human.muscleMultiplier);
        AppendNum(json, "frictionMultiplier", human.frictionMultiplier);
        AppendNum(json, "lumbarFrictionMaxTorque", human.lumbarFrictionMaxTorque);
        AppendNum(json, "hipPGain", balance != null ? balance.hipPGain : 0f);
        AppendNum(json, "kneePGain", balance != null ? balance.kneePGain : 0f);
        AppendNum(json, "ankleComD", balance != null ? balance.ankleComD : 0f);
        AppendNum(json, "kneeBaseAngle", balance != null ? balance.kneeBaseAngle : 0f);
        AppendNum(json, "ankleComP", balance != null ? balance.ankleComP : 0f);
        AppendNum(json, "comProportionalGain", balance != null ? balance.comProportionalGain : 0f);

        AppendNum(json, "duration", info.Duration);
        AppendNum(json, "startY", info.StartY);
        AppendNum(json, "hipBaseAngle", balance != null ? balance.hipBaseAngle : 0f);
        AppendNum(json, "hipDGain", balance != null ? balance.hipDGain : 0f);
        AppendNum(json, "kneeDGain", balance != null ? balance.kneeDGain : 0f);
        AppendNum(json, "comDerivativeGain", balance != null ? balance.comDerivativeGain : 0f);
        AppendNum(json, "hipBalanceGain", balance != null ? balance.hipBalanceGain : 0f);
        AppendNum(json, "lumbarPGain", balance != null ? balance.lumbarPGain : 0f);
        AppendNum(json, "lumbarDGain", balance != null ? balance.lumbarDGain : 0f);
        AppendNum(json, "lumbarTargetTilt", balance != null ? balance.lumbarTargetTilt : 0f);
        AppendNum(json, "lumbarErrorReferenceDegrees", balance != null ? balance.lumbarErrorReferenceDegrees : 0f);
        AppendNum(json, "lumbarFriction", human.lumbarFriction);
        AppendNum(json, "recoveryCoMOffset", balance != null ? balance.recoveryCoMOffset : 0f);
        AppendNum(json, "fallCoMOffset", balance != null ? balance.fallCoMOffset : 0f);
        AppendNum(json, "crouchRatePerSecond", balance != null ? balance.crouchRatePerSecond : 0f);
        AppendNum(json, "crouchReleaseRatePerSecond", balance != null ? balance.crouchReleaseRatePerSecond : 0f);
            AppendNum(json, "crouchKneeFlex", balance != null ? balance.crouchKneeFlex : 0f);
            AppendNum(json, "crouchHipFlex", balance != null ? balance.crouchHipFlex : 0f);
            AppendNum(json, "crouchPelvisTilt", balance != null ? balance.crouchPelvisTilt : 0f);
            AppendNum(json, "crouchTorsoLean", balance != null ? balance.crouchTorsoLean : 0f);
            AppendNum(json, "pelvisPGain", balance != null ? balance.pelvisPGain : 0f);
            AppendNum(json, "pelvisDGain", balance != null ? balance.pelvisDGain : 0f);
            AppendNum(json, "pelvisErrorReferenceDegrees", balance != null ? balance.pelvisErrorReferenceDegrees : 0f);
        AppendStr(json, "unityVersion", Application.unityVersion);
        AppendStr(json, "buildGuid", Application.buildGUID);
        AppendStr(json, "sourceFingerprint", info.SourceFingerprint ?? "");
        AppendStr(json, "muscleSaturationScope", "hips,knees");

        json.Append(",\"inputs\":{");
        AppendStr(json, "label", info.Label ?? "", false);
        AppendNum(json, "duration", info.Duration);
        AppendNum(json, "startY", info.StartY);
        AppendNum(json, "pushImpulse", info.RequestedPushImpulse);
        AppendNum(json, "pushTime", info.RequestedPushTime);
        AppendNum(json, "crouch", info.RequestedCrouch);
        AppendNum(json, "crouchTime", info.RequestedCrouchTime);
        AppendNum(json, "crouchHold", info.RequestedCrouchHold);
        AppendNum(json, "lean", info.RequestedLean);
        AppendNum(json, "leanTime", info.RequestedLeanTime);
        AppendNum(json, "leanHold", info.RequestedLeanHold);
        AppendNum(json, "standLeg", info.RequestedStandLeg);
        AppendNum(json, "standLegTime", info.RequestedStandLegTime);
        AppendNum(json, "standLegHold", info.RequestedStandLegHold);
        AppendNum(json, "march", info.RequestedMarch);
        AppendNum(json, "marchTime", info.RequestedMarchTime);
        AppendNum(json, "marchDuration", info.RequestedMarchDuration);
        AppendNum(json, "marchHold", info.RequestedMarchHold);
        AppendNum(json, "marchPlant", info.RequestedMarchPlant);
        AppendNum(json, "marchFirst", info.RequestedMarchFirst);
        AppendNum(json, "walk", info.RequestedWalk);
        AppendNum(json, "walkTime", info.RequestedWalkTime);
        AppendNum(json, "walkDuration", info.RequestedWalkDuration);
        AppendNum(json, "run", info.RequestedRun);
        AppendNum(json, "runTime", info.RequestedRunTime);
        AppendNum(json, "runHold", info.RequestedRunHold);
        AppendNum(json, "walkStance", info.RequestedWalkStance);
        AppendNum(json, "walkTransfer", info.RequestedWalkTransfer);
        AppendNum(json, "walkTransferCom", info.RequestedWalkTransferCom);
        AppendNum(json, "walkTransferFallbackCom", info.RequestedWalkTransferFallbackCom);
        AppendNum(json, "walkTransferLevel", info.RequestedWalkTransferLevel);
        AppendNum(json, "walkFirst", info.RequestedWalkFirst);
        AppendNum(json, "fixedDelta", Time.fixedDeltaTime);
        AppendNum(json, "muscle", info.MuscleMultiplier);
        AppendNum(json, "friction", info.FrictionMultiplier);
        json.Append("}");

        json.Append(",\"meta\":{");
        AppendStr(json, "unityVersion", Application.unityVersion, false);
        AppendStr(json, "buildGuid", Application.buildGUID);
        AppendStr(json, "sourceFingerprint", info.SourceFingerprint ?? "");
        AppendStr(json, "manifestUnityVersion", info.ManifestUnityVersion ?? "");
        AppendStr(json, "manifestBuildGuid", info.ManifestBuildGuid ?? "");
        AppendStr(json, "manifestBuildTimeUtc", info.ManifestBuildTimeUtc ?? "");
        AppendStr(json, "buildManifestPath", info.ManifestPath ?? "");
        json.Append("}");

        AppendCommandArgs(json, info.CommandArgs);
        AppendApplied(json, human, balance);

        json.Append("}");
        return json.ToString();
    }

    private void AppendApplied(StringBuilder json, Human h, BalanceController b)
    {
        float actualMass;
        int bodyCount;
        SumRigidbodies(h, out actualMass, out bodyCount);

        json.Append(",\"applied\":{");
        AppendNum(json, "totalMass", h.totalMass, false);
        AppendNum(json, "actualRigidbodyMass", actualMass);
        json.Append(",\"rigidbodyCount\":").Append(bodyCount.ToString(CultureInfo.InvariantCulture));
        json.Append(",\"continuousBodyCount\":").Append(CountContinuous(h).ToString(CultureInfo.InvariantCulture));
        json.Append(",\"muscleBehaviourCount\":").Append(CountBehaviours<Muscle>(h).ToString(CultureInfo.InvariantCulture));
        json.Append(",\"frictionBehaviourCount\":").Append(CountBehaviours<JointFriction>(h).ToString(CultureInfo.InvariantCulture));
        json.Append(",\"velocityIterations\":").Append(Physics2D.velocityIterations.ToString(CultureInfo.InvariantCulture));
        json.Append(",\"positionIterations\":").Append(Physics2D.positionIterations.ToString(CultureInfo.InvariantCulture));
        if (state != null && state.probeCallCount > 0)
        {
            AppendNum(json, "meanProbeHits", state.probeHitSum / (float)state.probeCallCount);
            AppendNum(json, "probeSaturatedFraction", state.probeSaturatedCount / (float)state.probeCallCount);
        }
        AppendNum(json, "muscleMultiplier", h.muscleMultiplier);
        AppendNum(json, "frictionMultiplier", h.frictionMultiplier);
        AppendNum(json, "viewAngleDegrees", h.viewAngleDegrees);
        AppendNum(json, "hipHalfSpacing", h.hipHalfSpacing);
        AppendNum(json, "shoulderHalfSpacing", h.shoulderHalfSpacing);
        AppendNum(json, "ankleHeelOffset", h.ankleHeelOffset);
        AppendNum(json, "shoulderDropFromNeck", h.shoulderDropFromNeck);
        AppendVec2(json, "pelvisSize", h.pelvisSize);
        AppendVec2(json, "torsoSize", h.torsoSize);
        AppendVec2(json, "headSize", h.headSize);
        AppendVec2(json, "neckSize", h.neckSize);
        AppendVec2(json, "upperArmSize", h.upperArmSize);
        AppendVec2(json, "lowerArmSize", h.lowerArmSize);
        AppendVec2(json, "handSize", h.handSize);
        AppendVec2(json, "thighSize", h.thighSize);
        AppendVec2(json, "shinSize", h.shinSize);
        AppendVec2(json, "footSize", h.footSize);
        AppendNum(json, "footHeelShare", h.footHeelShare);
        AppendNum(json, "toeFriction", h.toeFriction);
        AppendNum(json, "toeMuscleTorque", h.toeMuscleTorque);
        // Check contact, not intent: the pair is
        // sqrt(µ_foot · µ_ground); one foot does not set grip by itself.
        AppendNum(json, "footFriction", h.footFriction);
        float groundMu = ResolveGroundFriction();
        AppendNum(json, "groundFriction", groundMu);
        AppendNum(json, "contactFriction", Mathf.Sqrt(Mathf.Max(0f, h.footFriction) * Mathf.Max(0f, groundMu)));
        AppendNum(json, "lumbarFriction", h.lumbarFriction);
        AppendNum(json, "lumbarFrictionMaxTorque", h.lumbarFrictionMaxTorque);
        AppendNum(json, "hipFriction", h.hipFriction);
        AppendNum(json, "kneeFriction", h.kneeFriction);
        AppendNum(json, "ankleFriction", h.ankleFriction);
        AppendNum(json, "neckFriction", h.neckFriction);
        AppendNum(json, "shoulderFriction", h.shoulderFriction);
        AppendNum(json, "elbowFriction", h.elbowFriction);
        AppendNum(json, "wristFriction", h.wristFriction);
        AppendNum(json, "lumbarMuscleTorque", h.lumbarMuscleTorque);
        AppendNum(json, "hipMuscleTorque", h.hipMuscleTorque);
        AppendNum(json, "kneeMuscleTorque", h.kneeMuscleTorque);
        AppendNum(json, "ankleExtensorTorque", h.ankleExtensorTorque);
        AppendNum(json, "ankleFlexorTorque", h.ankleFlexorTorque);
        AppendNum(json, "neckMuscleTorque", h.neckMuscleTorque);
        AppendNum(json, "shoulderMuscleTorque", h.shoulderMuscleTorque);
        AppendNum(json, "elbowMuscleTorque", h.elbowMuscleTorque);
        AppendNum(json, "wristMuscleTorque", h.wristMuscleTorque);
        if (b != null)
        {
            AppendNum(json, "hipBaseAngle", b.hipBaseAngle);
            AppendNum(json, "kneeBaseAngle", b.kneeBaseAngle);
            AppendNum(json, "kneeRecoveryFlex", b.kneeRecoveryFlex);
            AppendNum(json, "crouchRatePerSecond", b.crouchRatePerSecond);
            AppendNum(json, "crouchReleaseRatePerSecond", b.crouchReleaseRatePerSecond);
            AppendNum(json, "crouchKneeFlex", b.crouchKneeFlex);
            AppendNum(json, "crouchHipFlex", b.crouchHipFlex);
            AppendNum(json, "crouchPelvisTilt", b.crouchPelvisTilt);
            AppendNum(json, "crouchTorsoLean", b.crouchTorsoLean);
            AppendNum(json, "leanRatePerSecond", b.leanRatePerSecond);
            AppendNum(json, "leanTorsoAngle", b.leanTorsoAngle);
            AppendNum(json, "leanPelvisTilt", b.leanPelvisTilt);
            AppendNum(json, "crouchArmShoulder", b.crouchArmShoulder);
            AppendNum(json, "crouchArmElbow", b.crouchArmElbow);
            AppendNum(json, "crouchArmWrist", b.crouchArmWrist);
            AppendNum(json, "crouchHandSupportMin", b.crouchHandSupportMin);
            AppendNum(json, "crouchHandGroundSlop", b.crouchHandGroundSlop);
            AppendNum(json, "crouchHandPoseStart", b.crouchHandPoseStart);
            AppendNum(json, "crouchHandSupportShoulder", b.crouchHandSupportShoulder);
            AppendNum(json, "crouchHandSupportElbow", b.crouchHandSupportElbow);
            AppendNum(json, "crouchHandSupportWrist", b.crouchHandSupportWrist);
            AppendNum(json, "crouchHandSupportSpread", b.crouchHandSupportSpread);
            AppendNum(json, "crouchHandBalanceSpread", b.crouchHandBalanceSpread);
            AppendNum(json, "swingHipFlex", b.swingHipFlex);
            AppendNum(json, "swingKneeFlex", b.swingKneeFlex);
            AppendNum(json, "marchHipFlex", b.marchHipFlex);
            AppendNum(json, "marchKneeFlex", b.marchKneeFlex);
            AppendNum(json, "marchPrepareTilt", b.marchPrepareTilt);
            AppendNum(json, "marchLiftTilt", b.marchLiftTilt);
            AppendNum(json, "marchTiltRate", b.marchTiltRate);
            AppendNum(json, "marchPelvisHold", b.marchPelvisHold);
            AppendNum(json, "marchComPelvisGain", b.marchComPelvisGain);
            AppendNum(json, "marchPelvisComClamp", b.marchPelvisComClamp);
            AppendNum(json, "marchAnkleRefScale", b.marchAnkleRefScale);
            AppendNum(json, "marchStancePelvisP", b.marchStancePelvisP);
            AppendNum(json, "marchAnkleCap", b.marchAnkleCap);
            AppendNum(json, "marchAnkleToeGate", b.marchAnkleToeGate);
            AppendNum(json, "marchAnkleHeelGate", b.marchAnkleHeelGate);
            AppendNum(json, "marchComDelay", b.marchComDelay);
            AppendNum(json, "marchPoseHip", b.marchPoseHip);
            AppendNum(json, "marchPoseKnee", b.marchPoseKnee);
            AppendNum(json, "swingKneeGroundedFraction", b.swingKneeGroundedFraction);
            AppendNum(json, "swingHipUnloadBias", b.swingHipUnloadBias);
            AppendNum(json, "forwardSwingHipUnloadScale", b.forwardSwingHipUnloadScale);
            AppendNum(json, "forwardSwingKneeGroundScale", b.forwardSwingKneeGroundScale);
            AppendNum(json, "backSwingHipUnloadScale", b.backSwingHipUnloadScale);
            AppendNum(json, "backSwingKneeGroundScale", b.backSwingKneeGroundScale);
            AppendNum(json, "backPelvisTiltFraction", b.backPelvisTiltFraction);
            AppendNum(json, "swingAnkleGroundedToeOff", b.swingAnkleGroundedToeOff);
            AppendNum(json, "standLegPelvisTilt", b.standLegPelvisTilt);
            AppendNum(json, "oneLegPelvisPBoost", b.oneLegPelvisPBoost);
            AppendNum(json, "oneLegPelvisDBoost", b.oneLegPelvisDBoost);
            AppendNum(json, "dualSupportSwingScale", b.dualSupportSwingScale);
            AppendNum(json, "dualSupportStandCap", b.dualSupportStandCap);
            AppendNum(json, "walkAirReleaseAuthority", b.walkAirReleaseAuthority);
            AppendNum(json, "walkReachAirSwingSeconds", b.walkReachAirSwingSeconds);
            AppendNum(json, "walkSwingLiftScale", b.walkSwingLiftScale);
            AppendNum(json, "walkStanceLiftDelay", b.walkStanceLiftDelay);
            AppendNum(json, "walkStanceLiftRamp", b.walkStanceLiftRamp);
            AppendNum(json, "walkWeightRamp", b.walkWeightRamp);
            AppendNum(json, "runStanceLiftDelay", b.runStanceLiftDelay);
            AppendNum(json, "runWeightRamp", b.runWeightRamp);
            AppendNum(json, "walkLiftComMax", b.walkLiftComMax);
            AppendNum(json, "walkSwingToeOffMax", b.walkSwingToeOffMax);
            AppendNum(json, "walkLiftLevelRateScale", b.walkLiftLevelRateScale);
            AppendNum(json, "walkLiftUnloadBias", b.walkLiftUnloadBias);
            AppendNum(json, "walkKneePeelMax", b.walkKneePeelMax);
            AppendNum(json, "walkStancePush", b.walkStancePush);
            AppendNum(json, "walkPlantStancePushFirst", b.walkPlantStancePushFirst);
            AppendNum(json, "runStancePushScale", b.runStancePushScale);
            AppendNum(json, "walkStanceHipExtend", b.walkStanceHipExtend);
            AppendNum(json, "walkStanceHipExtendMax", b.walkStanceHipExtendMax);
            AppendNum(json, "walkStancePushOnLead", b.walkStancePushOnLead);
            AppendNum(json, "walkPlantPushScale", b.walkPlantPushScale);
            AppendNum(json, "walkPlantPushLeadWindow", b.walkPlantPushLeadWindow);
            AppendNum(json, "walkPlantRockLead", b.walkPlantRockLead);
            AppendNum(json, "walkScrapeAnkleDrive", b.walkScrapeAnkleDrive);
            AppendNum(json, "walkPushToeFade", b.walkPushToeFade);
            AppendNum(json, "walkFallComRollToe", b.walkFallComRollToe);
            AppendNum(json, "walkTargetSpeed", b.walkTargetSpeed);
            AppendNum(json, "walkSpeedPushGain", b.walkSpeedPushGain);
            AppendNum(json, "walkSpeedPushMax", b.walkSpeedPushMax);
            AppendNum(json, "walkPushCounterAllowance", b.walkPushCounterAllowance);
            AppendNum(json, "walkPropulsionGain", b.walkPropulsionGain);
            AppendNum(json, "walkPropulsionMax", b.walkPropulsionMax);
            AppendNum(json, "walkPropulsionStart", b.walkPropulsionStart);
            AppendNum(json, "walkPropulsionPeak", b.walkPropulsionPeak);
            AppendNum(json, "walkPropulsionEnd", b.walkPropulsionEnd);
            AppendNum(json, "walkPropulsionTiltGuardDeg", b.walkPropulsionTiltGuardDeg);
            AppendNum(json, "walkPropulsionComWindow", b.walkPropulsionComWindow);
            AppendNum(json, "walkPropulsionMinWeight", b.walkPropulsionMinWeight);
            AppendNum(json, "walkPropulsionCounterAllowance", b.walkPropulsionCounterAllowance);
            AppendNum(json, "walkSpeedPushComGateScale", b.walkSpeedPushComGateScale);
            AppendNum(json, "walkXCoMWeight", b.walkXCoMWeight);
            AppendNum(json, "walkXCoMHeight", b.walkXCoMHeight);
            AppendNum(json, "walkSpeedLeanGain", b.walkSpeedLeanGain);
            AppendNum(json, "walkSpeedLeanMax", b.walkSpeedLeanMax);
            AppendNum(json, "walkSpeedLeanTorsoScale", b.walkSpeedLeanTorsoScale);
            AppendNum(json, "walkSpeedDriveGain", b.walkSpeedDriveGain);
            AppendNum(json, "walkSpeedDriveMax", b.walkSpeedDriveMax);
            AppendNum(json, "walkSpeedDriveStartLevel", b.walkSpeedDriveStartLevel);
            AppendNum(json, "walkSpeedSwingHipGain", b.walkSpeedSwingHipGain);
            AppendNum(json, "walkSpeedSwingKneeGain", b.walkSpeedSwingKneeGain);
            AppendNum(json, "walkSpeedSwingFlexMax", b.walkSpeedSwingFlexMax);
            AppendNum(json, "walkSwingHipBoost", b.walkSwingHipBoost);
            AppendNum(json, "walkSwingKneeBoost", b.walkSwingKneeBoost);
            AppendNum(json, "walkStepLength", b.walkStepLength);
            AppendNum(json, "walkStepLengthSpeedGain", b.walkStepLengthSpeedGain);
            AppendNum(json, "walkStepPlacementGain", b.walkStepPlacementGain);
            AppendNum(json, "walkStepPlacementMax", b.walkStepPlacementMax);
            AppendNum(json, "walkStepPlacementGroundFraction", b.walkStepPlacementGroundFraction);
            AppendNum(json, "walkStepPlaceOnFold", b.walkStepPlaceOnFold);
            AppendNum(json, "walkStepComMargin", b.walkStepComMargin);
            AppendNum(json, "walkStepCaptureTarget", b.walkStepCaptureTarget);
            AppendNum(json, "walkStepMinLength", b.walkStepMinLength);
            AppendNum(json, "walkStepCatchLeadSync", b.walkStepCatchLeadSync);
            AppendNum(json, "walkStepCatchLeadPad", b.walkStepCatchLeadPad);
            AppendNum(json, "walkPlacementSyncStart", b.walkPlacementSyncStart);
            AppendNum(json, "walkTouchdownSyncClearance", b.walkTouchdownSyncClearance);
            AppendNum(json, "walkStanceKneeBend", b.walkStanceKneeBend);
            AppendNum(json, "walkStanceKneeRecoveryFlex", b.walkStanceKneeRecoveryFlex);
            AppendNum(json, "walkForwardPelvisLean", b.walkForwardPelvisLean);
            AppendNum(json, "walkForwardTorsoLean", b.walkForwardTorsoLean);
            AppendNum(json, "walkStanceExtend", b.walkStanceExtend);
            AppendNum(json, "walkComLeadX", b.walkComLeadX);
            AppendNum(json, "walkFirstLiftLead", b.walkFirstLiftLead);
            AppendNum(json, "walkFirstLeadSeconds", b.walkFirstLeadSeconds);
            AppendNum(json, "useWalkLift", b.useWalkLift);
            AppendNum(json, "walkLiftUnloadFromDirt", b.walkLiftUnloadFromDirt);
            AppendNum(json, "useCaptureWalker", b.useCaptureWalker);
            AppendNum(json, "getUpFallenAbsTilt", b.getUpFallenAbsTilt);
            AppendNum(json, "getUpStandTiltDeg", b.getUpStandTiltDeg);
            AppendNum(json, "getUpHipFlex", b.getUpHipFlex);
            AppendNum(json, "getUpKneeFlex", b.getUpKneeFlex);
            AppendNum(json, "getUpTuckMinSeconds", b.getUpTuckMinSeconds);
            AppendNum(json, "getUpSquatAbsTilt", b.getUpSquatAbsTilt);
            AppendNum(json, "getUpTuckMaxSeconds", b.getUpTuckMaxSeconds);
            AppendNum(json, "getUpRiseSeconds", b.getUpRiseSeconds);
            AppendNum(json, "captureStepDuration", b.captureStepDuration);
            AppendNum(json, "captureSwingXShare", b.captureSwingXShare);
            AppendNum(json, "captureTargetSpeed", b.captureTargetSpeed);
            AppendNum(json, "captureLandLegLength", b.captureLandLegLength);
            AppendNum(json, "captureStanceLegLength", b.captureStanceLegLength);
            AppendNum(json, "captureTrunkLeanDeg", b.captureTrunkLeanDeg);
            AppendNum(json, "capturePelvisLeanDeg", b.capturePelvisLeanDeg);
            AppendNum(json, "captureLeanPlantFraction", b.captureLeanPlantFraction);
            AppendNum(json, "captureDoubleSupportMin", b.captureDoubleSupportMin);
            AppendNum(json, "captureCopTravelBias", b.captureCopTravelBias);
            AppendNum(json, "captureLaunchSeconds", b.captureLaunchSeconds);
            AppendNum(json, "captureLaunchAnkleCap", b.captureLaunchAnkleCap);
            AppendNum(json, "captureLaunchSpeed", b.captureLaunchSpeed);
            AppendNum(json, "captureLaunchLead", b.captureLaunchLead);
            AppendNum(json, "captureCatchPast", b.captureCatchPast);
            AppendNum(json, "walkLiftKneeFlex", b.walkLiftKneeFlex);
            AppendNum(json, "walkLiftStepLength", b.walkLiftStepLength);
            AppendNum(json, "walkLiftStepMax", b.walkLiftStepMax);
            AppendNum(json, "walkLiftAnkleFwdDriveMax", b.walkLiftAnkleFwdDriveMax);
            AppendNum(json, "walkAnkleDorsiHold", b.walkAnkleDorsiHold);
            AppendNum(json, "walkAnkleDorsiFade", b.walkAnkleDorsiFade);
            AppendNum(json, "walkLiftCaptureLookahead", b.walkLiftCaptureLookahead);
            AppendNum(json, "walkLiftLandX", b.lastWalkLiftLandX);
            AppendNum(json, "walkLiftCaptureX", b.lastWalkLiftCaptureX);
            AppendNum(json, "walkLiftCaptureStep", b.lastWalkLiftCaptureStep);
            AppendNum(json, "walkLiftXiNow", b.lastWalkLiftXiNow);
            AppendNum(json, "walkLiftComVelX", b.lastWalkLiftComVelX);
            AppendNum(json, "walkAnkleFwdRaw", b.lastWalkAnkleFwdRaw);
            AppendNum(json, "walkAnkleFwdDrive", b.lastWalkAnkleFwdDrive);
            AppendNum(json, "walkAnkleDorsiFadeNow", b.lastWalkAnkleDorsiFade);
            AppendNum(json, "walkLiftPhase", b.WalkLiftPhaseCode);
            AppendNum(json, "walkLiftCoupleHipShare", b.walkLiftCoupleHipShare);
            AppendNum(json, "walkLiftReactionCap", b.walkLiftReactionCap);
            AppendNum(json, "walkLiftAdvanceSeconds", b.walkLiftAdvanceSeconds);
            AppendNum(json, "walkLiftAdvanceHipRamp", b.walkLiftAdvanceHipRamp);
            AppendNum(json, "walkLiftTauSw", b.lastWalkLiftTauSw);
            AppendNum(json, "walkLiftCoupleRamp", b.lastWalkLiftCoupleRamp);
            AppendNum(json, "walkLiftAdvanceScale", b.lastWalkLiftAdvanceScale);
            AppendNum(json, "walkLiftComLead", b.lastWalkLiftComLead);
            AppendNum(json, "walkLiftComLeadApplied", b.lastWalkLiftComLeadApplied);
            AppendNum(json, "walkSwingScissorLevel", b.walkSwingScissorLevel);
            AppendNum(json, "walkSwingScissorFlex", b.walkSwingScissorFlex);
            AppendNum(json, "walkSwingLiftCouple", b.walkSwingLiftCouple);
            AppendNum(json, "walkSwingLiftCoupleMax", b.walkSwingLiftCoupleMax);
            AppendNum(json, "walkSwingLiftCoupleCapScale", b.walkSwingLiftCoupleCapScale);
            if (stepDriver != null)
            {
                AppendNum(json, "useFallCatchWalk", stepDriver.useFallCatchWalk);
                AppendNum(json, "fallComLeadTrigger", stepDriver.fallComLeadTrigger);
                AppendNum(json, "fallPelvisLean", stepDriver.fallPelvisLean);
                AppendNum(json, "fallForwardLeanScale", stepDriver.fallForwardLeanScale);
                AppendNum(json, "catchKneeFlex", stepDriver.catchKneeFlex);
                AppendNum(json, "catchLeadCapture", stepDriver.catchLeadCapture);
                AppendNum(json, "catchFootLeadMin", stepDriver.catchFootLeadMin);
                AppendNum(json, "catchLeadCaptureMargin", stepDriver.catchLeadCaptureMargin);
                AppendNum(json, "plantMinDuration", stepDriver.plantMinDuration);
                AppendNum(json, "plantLeadWaitSeconds", stepDriver.plantLeadWaitSeconds);
                AppendNum(json, "plantCaughtLeadMax", stepDriver.plantCaughtLeadMax);
                AppendNum(json, "plantArrestCopScale", stepDriver.plantArrestCopScale);
                AppendNum(json, "walkRunawayKeepStepping", stepDriver.walkRunawayKeepStepping);
                AppendNum(json, "plantRunawayStepSeconds", stepDriver.plantRunawayStepSeconds);
                AppendNum(json, "reachTimeout", stepDriver.reachTimeout);
                AppendNum(json, "fallAbortDeg", stepDriver.fallAbortDeg);
                AppendNum(json, "plantStallReseatSeconds", stepDriver.plantStallReseatSeconds);
                AppendNum(json, "plantReseatLeadMargin", stepDriver.plantReseatLeadMargin);
                AppendNum(json, "plantStallStandSeconds", stepDriver.plantStallStandSeconds);
                AppendNum(json, "plantClearRestoreOnGate", stepDriver.plantClearRestoreOnGate);
                AppendNum(json, "reachGroundStallSeconds", stepDriver.reachGroundStallSeconds);
                AppendNum(json, "fallCatchBounceMax", stepDriver.fallCatchBounceMax);
                AppendNum(json, "sinkAbortDrop", stepDriver.sinkAbortDrop);
                json.Append(",\"spawnStandHeightValid\":")
                    .Append(stepDriver.SpawnStandHeightValid ? "true" : "false");
                AppendNum(json, "spawnStandHeight", stepDriver.SpawnStandHeight);
                json.Append(",\"standComHeightValid\":")
                    .Append(stepDriver.StandComHeightValid ? "true" : "false");
                AppendNum(json, "standComHeight", stepDriver.StandComHeight);
                AppendNum(json, "pelvisStandSlack", stepDriver.pelvisStandSlack);
                AppendNum(json, "plantAirLowestFoot", stepDriver.plantAirLowestFoot);
                AppendNum(json, "walkRecoverAnyPose", stepDriver.walkRecoverAnyPose);
                AppendNum(json, "stanceComTrigger", stepDriver.stanceComTrigger);
                AppendNum(json, "stanceComMinAge", stepDriver.stanceComMinAge);
                AppendNum(json, "stanceComTriggerPerSpeed", stepDriver.stanceComTriggerPerSpeed);
                AppendNum(json, "walkComTriggerMinStanceFraction", stepDriver.walkComTriggerMinStanceFraction);
                AppendNum(json, "walkTiltAbortDeg", stepDriver.walkTiltAbortDeg);
                AppendNum(json, "walkTiltRecoverDeg", stepDriver.walkTiltRecoverDeg);
                AppendNum(json, "walkCadenceStanceGain", stepDriver.walkCadenceStanceGain);
                AppendNum(json, "walkCadenceMinStance", stepDriver.walkCadenceMinStance);
                AppendNum(json, "transferMinDuration", stepDriver.transferMinDuration);
                AppendNum(json, "transferTouchdownMaxAge", stepDriver.transferTouchdownMaxAge);
                AppendNum(json, "walkCadenceTransferGain", stepDriver.walkCadenceTransferGain);
                AppendNum(json, "walkCadenceMinTransfer", stepDriver.walkCadenceMinTransfer);
            }
            AppendNum(json, "walkSwingRetractGain", b.walkSwingRetractGain);
            AppendNum(json, "walkSwingRetractMax", b.walkSwingRetractMax);
            AppendNum(json, "walkSwingPlaceGeometry", b.walkSwingPlaceGeometry);
            AppendNum(json, "walkSwingPlaceGeoOnly", b.walkSwingPlaceGeoOnly);
            AppendNum(json, "walkSwingLandKneeGeometry", b.walkSwingLandKneeGeometry);
            AppendNum(json, "walkLandKneeMinDeg", b.walkLandKneeMinDeg);
            AppendNum(json, "walkGeoSwingLift", b.walkGeoSwingLift);
            AppendNum(json, "walkGeoSwingLiftClearance", b.walkGeoSwingLiftClearance);
            AppendNum(json, "walkStanceGeoFriction", b.walkStanceGeoFriction);
            AppendNum(json, "walkSwingLegFrictionTau", b.walkSwingLegFrictionTau);
            AppendNum(json, "walkSwingAirHold", b.walkSwingAirHold);
            AppendNum(json, "walkSwingHeelPlant", b.walkSwingHeelPlant);
            AppendNum(json, "walkSwingUnlatchPlant", b.walkSwingUnlatchPlant);
            AppendNum(json, "walkSwingLatchAir", b.walkSwingLatchAir);
            AppendNum(json, "walkSwingAirLevel", b.walkSwingAirLevel);
            AppendNum(json, "walkArmShoulderSwing", b.walkArmShoulderSwing);
            AppendNum(json, "walkArmElbowSwing", b.walkArmElbowSwing);
            AppendNum(json, "walkArmTransferCarry", b.walkArmTransferCarry);
            AppendNum(json, "walkArmBalanceShoulderGain", b.walkArmBalanceShoulderGain);
            AppendNum(json, "walkArmBalanceElbowGain", b.walkArmBalanceElbowGain);
            AppendNum(json, "walkLatePushGain", b.walkLatePushGain);
            AppendNum(json, "walkLatePushStart", b.walkLatePushStart);
            AppendNum(json, "walkLatePushMax", b.walkLatePushMax);
            AppendNum(json, "walkLatePushNeedsAir", b.walkLatePushNeedsAir ? 1f : 0f);
            AppendNum(json, "walkLatePushNeedsTouchdownWindow", b.walkLatePushNeedsTouchdownWindow ? 1f : 0f);
            AppendNum(json, "standLegRatePerSecond", b.standLegRatePerSecond);
            AppendNum(json, "hipPGain", b.hipPGain);
            AppendNum(json, "hipDGain", b.hipDGain);
            AppendNum(json, "pelvisPGain", b.pelvisPGain);
            AppendNum(json, "pelvisDGain", b.pelvisDGain);
            AppendNum(json, "pelvisErrorReferenceDegrees", b.pelvisErrorReferenceDegrees);
            AppendNum(json, "kneePGain", b.kneePGain);
            AppendNum(json, "kneeDGain", b.kneeDGain);
            AppendNum(json, "hipBalanceGain", b.hipBalanceGain);
            AppendNum(json, "lumbarPGain", b.lumbarPGain);
            AppendNum(json, "lumbarDGain", b.lumbarDGain);
            AppendNum(json, "lumbarTargetTilt", b.lumbarTargetTilt);
            AppendNum(json, "lumbarErrorReferenceDegrees", b.lumbarErrorReferenceDegrees);
            AppendNum(json, "ankleComP", b.ankleComP);
            AppendNum(json, "ankleComD", b.ankleComD);
            AppendNum(json, "ankleLimitMargin", b.ankleLimitMargin);
            AppendNum(json, "comOffsetReference", b.comOffsetReference);
            AppendNum(json, "tiltSpeedReference", b.tiltSpeedReference);
            AppendNum(json, "comVelocityReference", b.comVelocityReference);
            AppendNum(json, "comProportionalGain", b.comProportionalGain);
            AppendNum(json, "comDerivativeGain", b.comDerivativeGain);
            AppendNum(json, "maxBalanceSignal", b.maxBalanceSignal);
            AppendNum(json, "errorReferenceDegrees", b.errorReferenceDegrees);
            AppendNum(json, "speedReferenceDegPerSec", b.speedReferenceDegPerSec);
            AppendNum(json, "recoveryCoMOffset", b.recoveryCoMOffset);
            AppendNum(json, "fallCoMOffset", b.fallCoMOffset);
            AppendNum(json, "muscleActivationSpeed", b.muscleActivationSpeed);
            AppendNum(json, "neckPGain", b.neckPGain);
            AppendNum(json, "neckDGain", b.neckDGain);
            AppendNum(json, "neckTargetTilt", b.neckTargetTilt);
            AppendNum(json, "neckErrorReferenceDegrees", b.neckErrorReferenceDegrees);
            AppendNum(json, "useStablePd", b.useStablePd ? 1f : 0f);
            AppendNum(json, "shoulderPGain", b.shoulderPGain);
            AppendNum(json, "shoulderDGain", b.shoulderDGain);
            AppendNum(json, "shoulderBaseAngle", b.shoulderBaseAngle);
            AppendNum(json, "shoulderErrorReferenceDegrees", b.shoulderErrorReferenceDegrees);
            AppendNum(json, "elbowPGain", b.elbowPGain);
            AppendNum(json, "elbowDGain", b.elbowDGain);
            AppendNum(json, "elbowBaseAngle", b.elbowBaseAngle);
            AppendNum(json, "elbowErrorReferenceDegrees", b.elbowErrorReferenceDegrees);
            AppendNum(json, "wristPGain", b.wristPGain);
            AppendNum(json, "wristDGain", b.wristDGain);
            AppendNum(json, "wristBaseAngle", b.wristBaseAngle);
            AppendNum(json, "wristErrorReferenceDegrees", b.wristErrorReferenceDegrees);
            AppendNum(json, "toePGain", b.toePGain);
            AppendNum(json, "toeDGain", b.toeDGain);
            AppendNum(json, "toeClearanceDeg", b.toeClearanceDeg);
            AppendNum(json, "captureToePushDeg", b.captureToePushDeg);
            AppendNum(json, "captureToeLeanGain", b.captureToeLeanGain);
            AppendNum(json, "armShoulderBalanceGain", b.armShoulderBalanceGain);
            AppendNum(json, "armElbowBalanceGain", b.armElbowBalanceGain);
            AppendNum(json, "armWristBalanceGain", b.armWristBalanceGain);
        }

        AppendJoints(json, h);
        AppendJointDamping(json);
        AppendPhysicsJobOptions(json);
        json.Append("}");
    }

    // Actual jobOptions on the first step — without them you cannot see whether determinism reset.
    private static void AppendPhysicsJobOptions(StringBuilder json)
    {
        PhysicsJobOptions2D options = Physics2D.jobOptions;
        json.Append(",\"physicsJobOptions\":{");
        json.Append("\"useMultithreading\":").Append(options.useMultithreading ? "1" : "0");
        json.Append(",\"useConsistencySorting\":").Append(options.useConsistencySorting ? "1" : "0");
        json.Append('}');
    }

    // Snapshot of Ieff and the applied K. Ieff = 1/(1/Iself+1/Iconnected):
    // this effective inertia is what gives the pair its relative acceleration,
    // not I of one segment. For the head, connected is the thin neck.
    private void AppendJointDamping(StringBuilder json)
    {
        json.Append(",\"jointDamping\":{");
        bool first = true;
        first = AppendDampingSnapshot(json, neckWatch, first);
        first = AppendDampingSnapshot(json, headWatch, first);
        first = AppendDampingSnapshot(json, shoulderWatch, first);
        first = AppendDampingSnapshot(json, elbowWatch, first);
        AppendDampingSnapshot(json, wristWatch, first);
        json.Append("}");
    }

    private static bool AppendDampingSnapshot(StringBuilder json, JointDampingWatch watch, bool first)
    {
        if (watch == null) return first;
        watch.CaptureIfNeeded();
        if (!first) json.Append(',');
        json.Append('"').Append(watch.Name).Append("\":{");
        AppendPrecise(json, "selfInertia", watch.SelfInertia, false);
        AppendPrecise(json, "connectedInertia", watch.ConnectedInertia);
        AppendPrecise(json, "ieff", watch.Ieff);
        AppendPrecise(json, "damping", watch.Damping);
        AppendPrecise(json, "maxTorque", watch.MaxTorque);
        AppendPrecise(json, "tauSeconds", watch.TauSeconds);
        AppendPrecise(json, "stepRatio", watch.StepRatio);
        json.Append('}');
        return false;
    }

    private static void SampleDampingWatch(JointDampingWatch watch)
    {
        if (watch == null) return;
        watch.CaptureIfNeeded();
        watch.Sample();
    }

    private static float RmsSpeed(JointDampingWatch watch)
    {
        if (watch == null || watch.SpeedSamples <= 0) return 0f;
        return Mathf.Sqrt((float)(watch.SumSpeedSquared / watch.SpeedSamples));
    }

    private static float LimitFraction(JointDampingWatch watch)
    {
        if (watch == null || watch.LimitSamples <= 0) return 0f;
        return watch.LimitHits / (float)watch.LimitSamples;
    }

    private static void SumRigidbodies(Human h, out float mass, out int count)
    {
        mass = 0f;
        count = 0;
        if (h == null) return;
        Rigidbody2D[] bodies = h.GetComponentsInChildren<Rigidbody2D>();
        for (int i = 0; i < bodies.Length; i++)
        {
            if (bodies[i] == null) continue;
            mass += bodies[i].mass;
            count++;
        }
    }

    private static int CountContinuous(Human h)
    {
        if (h == null) return 0;
        int n = 0;
        Rigidbody2D[] bodies = h.GetComponentsInChildren<Rigidbody2D>();
        for (int i = 0; i < bodies.Length; i++)
        {
            if (bodies[i] != null
                && bodies[i].collisionDetectionMode == CollisionDetectionMode2D.Continuous)
                n++;
        }
        return n;
    }

    private static int CountBehaviours<T>(Human h) where T : MonoBehaviour
    {
        return h == null ? 0 : h.GetComponentsInChildren<T>().Length;
    }

    private static void AppendJoints(StringBuilder json, Human h)
    {
        json.Append(",\"joints\":{");
        bool first = true;
        first = AppendNamedJoint(json, "lumbar", FindJoint(h, "Torso"), first);
        first = AppendNamedJoint(json, "neck", FindJoint(h, "Neck"), first);
        first = AppendNamedJoint(json, "head", FindJoint(h, "Head"), first);
        first = AppendSymmetricJoint(json, "shoulder", FindJoint(h, "LeftArmUpper"), FindJoint(h, "RightArmUpper"), first);
        first = AppendSymmetricJoint(json, "elbow", FindJoint(h, "LeftArmLower"), FindJoint(h, "RightArmLower"), first);
        first = AppendSymmetricJoint(json, "wrist", FindJoint(h, "LeftArmHand"), FindJoint(h, "RightArmHand"), first);
        first = AppendSymmetricJoint(json, "hip", FindJoint(h, "LeftLegThigh"), FindJoint(h, "RightLegThigh"), first);
        first = AppendSymmetricJoint(json, "knee", FindJoint(h, "LeftLegShin"), FindJoint(h, "RightLegShin"), first);
        first = AppendSymmetricJoint(json, "ankle", FindJoint(h, "LeftLegFoot"), FindJoint(h, "RightLegFoot"), first);
        AppendSymmetricJoint(json, "toe", FindJoint(h, "LeftLegToe"), FindJoint(h, "RightLegToe"), first);
        json.Append("}");
    }

    private static HingeJoint2D FindJoint(Human h, string childName)
    {
        if (h == null) return null;
        Transform t = h.transform.Find(childName);
        return t != null ? t.GetComponent<HingeJoint2D>() : null;
    }

    private static bool SameLimits(HingeJoint2D a, HingeJoint2D b)
    {
        if (a == null || b == null) return false;
        return a.useLimits == b.useLimits
            && Mathf.Approximately(a.limits.min, b.limits.min)
            && Mathf.Approximately(a.limits.max, b.limits.max);
    }

    private static bool AppendSymmetricJoint(StringBuilder json, string name, HingeJoint2D left, HingeJoint2D right, bool first)
    {
        if (SameLimits(left, right))
            return AppendNamedJoint(json, name, left, first);

        first = AppendNamedJoint(json, name + "Left", left, first);
        return AppendNamedJoint(json, name + "Right", right, first);
    }

    private static bool AppendNamedJoint(StringBuilder json, string name, HingeJoint2D joint, bool first)
    {
        if (joint == null) return first;
        if (!first) json.Append(',');
        json.Append('"').Append(name).Append("\":{");
        json.Append("\"useLimits\":").Append(joint.useLimits ? "true" : "false");
        json.Append(",\"min\":").Append(F(joint.limits.min));
        json.Append(",\"max\":").Append(F(joint.limits.max));
        json.Append('}');
        return false;
    }

    // Ground is built by GroundBuilder, not by the body: without its material
    // the sole friction figure says nothing about grip.
    private static float ResolveGroundFriction()
    {
        GameObject ground = GameObject.Find("Ground");
        if (ground == null)
            return 0.4f;
        Collider2D col = ground.GetComponent<Collider2D>();
        if (col == null)
            return 0.4f;
        return col.sharedMaterial != null ? col.sharedMaterial.friction : 0.4f;
    }

    private static void AppendVec2(StringBuilder json, string key, Vector2 value, bool comma = true)
    {
        if (comma) json.Append(',');
        json.Append('"').Append(key).Append("\":{\"x\":").Append(F(value.x)).Append(",\"y\":").Append(F(value.y)).Append('}');
    }

    private static void AppendCommandArgs(StringBuilder json, string[] args)
    {
        json.Append(",\"commandArgs\":[");
        if (args != null)
        {
            for (int i = 0; i < args.Length; i++)
            {
                if (i > 0) json.Append(',');
                json.Append('"').Append(Escape(args[i])).Append('"');
            }
        }
        json.Append(']');
    }

    private float Fraction(int count)
    {
        return samples > 0 ? count / (float)samples : 0f;
    }

    private static float FiniteOrZero(float value)
    {
        return float.IsInfinity(value) || float.IsNaN(value) ? 0f : value;
    }

    private static float MaxActivation(Muscle flexor, Muscle extensor)
    {
        float a = flexor != null ? flexor.activation : 0f;
        float b = extensor != null ? extensor.activation : 0f;
        return Mathf.Max(a, b);
    }

    // WALK-1: the torque a flexor/extensor pair is really applying. Muscle.Apply
    // adds activation * maxTorque * direction to the body, so this sum is the
    // APPLIED torque, next to the requested target angle in the same row.
    private static float AppliedTorque(Muscle flexor, Muscle extensor)
    {
        float torque = 0f;
        if (flexor != null)
            torque += flexor.activation * flexor.maxTorque * flexor.direction;
        if (extensor != null)
            torque += extensor.activation * extensor.maxTorque * extensor.direction;
        return torque;
    }

    private string FormatCaptureOnsetLog()
    {
        if (balance == null || balance.CaptureOnsetLogCount <= 0)
            return "";
        var log = new StringBuilder();
        for (int i = 0; i < BalanceController.CaptureOnsetLogCapacity; i++)
        {
            float age;
            bool collapsed;
            bool posed;
            float comX;
            float margin;
            float hipDrop;
            float comDrop;
            bool columnStanding;
            byte reason;
            if (!balance.TryGetCaptureOnsetSample(i, out age, out collapsed,
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
            log.Append(",col").Append(columnStanding ? "1" : "0");
            log.Append(",r").Append(reason.ToString(CultureInfo.InvariantCulture));
        }
        return log.ToString();
    }

    private static void AppendNum(StringBuilder json, string key, float value, bool comma = true)
    {
        if (comma) json.Append(',');
        json.Append('"').Append(key).Append("\":").Append(F(value));
    }

    // Inertias and tau are small (10⁻³…10⁻⁴): four F4 digits are not enough.
    private static void AppendPrecise(StringBuilder json, string key, float value, bool comma = true)
    {
        if (comma) json.Append(',');
        json.Append('"').Append(key).Append("\":").Append(value.ToString("G6", CultureInfo.InvariantCulture));
    }

    private static void AppendStr(StringBuilder json, string key, string value, bool comma = true)
    {
        if (comma) json.Append(',');
        json.Append('"').Append(key).Append("\":\"").Append(Escape(value ?? "")).Append('"');
    }

    private static string Escape(string value)
    {
        if (string.IsNullOrEmpty(value)) return "";

        StringBuilder sb = new StringBuilder(value.Length + 8);
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            switch (c)
            {
                case '\\': sb.Append("\\\\"); break;
                case '"': sb.Append("\\\""); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (c < ' ')
                        sb.AppendFormat(CultureInfo.InvariantCulture, "\\u{0:x4}", (int)c);
                    else
                        sb.Append(c);
                    break;
            }
        }

        return sb.ToString();
    }

    // ─── WALK-1 recorder completeness ───
    // One owner and one finite request per leg joint on EVERY row. The rule:
    //   1. the capture walker's own drive code for that joint, while it wrote
    //      the joint this very frame (tapActive 1 and a non-zero tap owner);
    //   2. else the layer that held the joint this step, from the controller's
    //      own per-step registry (BalanceController.LegRequest*);
    //   3. else a documented IDLE (15) - a joint no layer wrote.
    // A joint whose owner commands TORQUE rather than an angle (the CoP / CoM
    // ankle, a world-upright hip, a zero snap) or that nobody asked for is
    // recorded with the MEASURED joint angle and its reqFallbackMask bit set:
    // the column is then a held angle, not an ask, and it is never NaN.
    private void CompleteLegRequestRecord(BalanceController.CaptureWalkTap tap,
                                          int tapActive)
    {
        legFallbackMask = 0;
        legLayerOut = balance != null
            ? balance.LegRequestLayer
            : BalanceController.TapOwnerIdle;
        for (int i = 0; i < BalanceController.LegJointCount; i++)
        {
            int owner = BalanceController.TapOwnerNone;
            float req = float.NaN;
            if (tapActive == 1)
            {
                switch (i)
                {
                    case 0: owner = tap.ownerHipL; req = tap.reqHipL; break;
                    case 1: owner = tap.ownerHipR; req = tap.reqHipR; break;
                    case 2: owner = tap.ownerKneeL; req = tap.reqKneeL; break;
                    case 3: owner = tap.ownerKneeR; req = tap.reqKneeR; break;
                    case 4: owner = tap.ownerAnkleL; req = tap.reqAnkleL; break;
                    default: owner = tap.ownerAnkleR; req = tap.reqAnkleR; break;
                }
            }
            if (owner == BalanceController.TapOwnerNone && balance != null
                && balance.LegRequestOwner(i) != BalanceController.TapOwnerNone)
            {
                owner = balance.LegRequestOwner(i);
                req = balance.LegRequestDeg(i);
            }
            float demand = balance != null ? balance.LegRequestDemand(i) : float.NaN;
            if (owner == BalanceController.TapOwnerNone)
                owner = BalanceController.TapOwnerIdle;
            if (!IsFinite(req) || OwnerCommandsTorque(owner))
            {
                req = MeasuredLegAngle(i);
                legFallbackMask |= 1 << i;
            }
            if (!IsFinite(demand))
                demand = 0f;
            legOwnerOut[i] = owner;
            legReqOut[i] = req;
            legDemandOut[i] = demand;
        }
    }

    // ownerHipL..ownerAnkleR order: hipL, hipR, kneeL, kneeR, ankleL, ankleR.
    private float MeasuredLegAngle(int i)
    {
        if (state == null) return 0f;
        switch (i)
        {
            case 0: return state.leftHipAngle;
            case 1: return state.rightHipAngle;
            case 2: return state.leftKneeAngle;
            case 3: return state.rightKneeAngle;
            case 4: return state.leftAnkleAngle;
            default: return state.rightAnkleAngle;
        }
    }

    // Owners whose "request" is a torque, not an angle: their req* column is a
    // held (measured) angle, and the row's reqFallbackMask bit says so.
    private static bool OwnerCommandsTorque(int owner)
    {
        return owner == BalanceController.TapOwnerCopAnkle
               || owner == BalanceController.TapOwnerZero
               || owner == BalanceController.TapOwnerIdle
               || owner == BalanceController.TapOwnerFalling;
    }

    private static bool IsFinite(float v)
    {
        return !float.IsNaN(v) && !float.IsInfinity(v);
    }

    private static string F(float value)
    {
        return value.ToString("F4", CultureInfo.InvariantCulture);
    }

    // One joint kind: a singleton (neck, head) or a left/right pair.
    // Ieff and K are taken from the first live hinge; RMS and stops — from all.
    private sealed class JointDampingWatch
    {
        public readonly string Name;
        public readonly HingeJoint2D[] Joints;

        public float SelfInertia;
        public float ConnectedInertia;
        public float Ieff;
        public float Damping;
        public float MaxTorque;
        public float TauSeconds;
        public float StepRatio;
        public double SumSpeedSquared;
        public int SpeedSamples;
        public int LimitHits;
        public int LimitSamples;

        private bool captured;

        private JointDampingWatch(string name, HingeJoint2D[] joints)
        {
            Name = name;
            Joints = joints ?? Array.Empty<HingeJoint2D>();
        }

        public static JointDampingWatch Single(string name, HingeJoint2D joint)
        {
            return new JointDampingWatch(name, joint != null ? new[] { joint } : Array.Empty<HingeJoint2D>());
        }

        public static JointDampingWatch Pair(string name, HingeJoint2D left, HingeJoint2D right)
        {
            int count = (left != null ? 1 : 0) + (right != null ? 1 : 0);
            HingeJoint2D[] joints = new HingeJoint2D[count];
            int i = 0;
            if (left != null) joints[i++] = left;
            if (right != null) joints[i] = right;
            return new JointDampingWatch(name, joints);
        }

        public void CaptureIfNeeded()
        {
            if (captured) return;
            captured = true;

            HingeJoint2D joint = FirstAlive();
            if (joint == null) return;

            Rigidbody2D self = joint.attachedRigidbody;
            Rigidbody2D connected = joint.connectedBody;
            SelfInertia = self != null ? self.inertia : 0f;
            ConnectedInertia = connected != null ? connected.inertia : 0f;
            Ieff = EffectiveInertia(SelfInertia, ConnectedInertia);

            JointFriction friction = joint.GetComponent<JointFriction>();
            Damping = friction != null ? friction.damping : 0f;
            MaxTorque = friction != null ? friction.maxTorque : 0f;

            if (Damping > 0f && Ieff > 0f)
            {
                TauSeconds = Ieff / Damping;
                StepRatio = Damping * Time.fixedDeltaTime / Ieff;
            }
        }

        public void Sample()
        {
            for (int i = 0; i < Joints.Length; i++)
            {
                HingeJoint2D joint = Joints[i];
                if (joint == null) continue;

                float speed = joint.jointSpeed;
                SumSpeedSquared += (double)speed * speed;
                SpeedSamples++;

                LimitSamples++;
                if (IsNearLimit(joint)) LimitHits++;
            }
        }

        // Angle of the group's first live joint. RMS speed alone is not enough: 360 °/s
        // can be a fine 100 Hz tremor or a large 12 Hz sway, and on screen
        // those are completely different. Amplitude is visible only from the angle.
        public float FirstAngle()
        {
            HingeJoint2D joint = FirstAlive();
            return joint != null ? joint.jointAngle : 0f;
        }

        // The same hinge, addressed by side: Pair() stores left first and
        // right second, so index 1 is the right arm. The per-side columns in
        // the CSV need both, not the first one only.
        public float AngleAt(int index)
        {
            if (Joints == null || index < 0 || index >= Joints.Length) return 0f;
            HingeJoint2D joint = Joints[index];
            return joint != null ? joint.jointAngle : 0f;
        }

        private HingeJoint2D FirstAlive()
        {
            for (int i = 0; i < Joints.Length; i++)
            {
                if (Joints[i] != null) return Joints[i];
            }
            return null;
        }

        private static bool IsNearLimit(HingeJoint2D joint)
        {
            if (joint == null || !joint.useLimits) return false;
            float angle = joint.jointAngle;
            float min = joint.limits.min;
            float max = joint.limits.max;
            return angle <= min + LIMIT_MARGIN_DEGREES || angle >= max - LIMIT_MARGIN_DEGREES;
        }

        // The same sum of inverse inertias as in JointFriction: the pair τ, −τ
        // gives relative acceleration τ·(1/I₁ + 1/I₂).
        private static float EffectiveInertia(float iSelf, float iConnected)
        {
            float inv = 0f;
            if (iSelf > 0f) inv += 1f / iSelf;
            if (iConnected > 0f) inv += 1f / iConnected;
            return inv > 0f ? 1f / inv : 0f;
        }
    }
}
