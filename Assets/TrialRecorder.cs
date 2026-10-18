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

    // ─── PHYSICAL FALL DETECTION (C1, 18.09) ───
    // ONE criterion for the whole project: Assets/FallCriterion.cs. The private
    // tilt/drop/settle constants and both drop arms that stood here are gone.
    // The automaton is still not ground truth (trial p_sw1 lay on the dirt from
    // 8 s while BalanceState stayed at Recovery) - but a second private rule is
    // not ground truth either: this one reported 8.415 s on a body that had
    // already collapsed at 6.035 s, and the arena's copy latched 2112 of 2112
    // rows on 0.064 m of ordinary walk-onset sink because it measured the drop
    // against the spawn pose. The shared rule: down when the root is 0.25 m
    // below its own SETTLED stand height, or when |tilt| > 60 deg, held 0.15 s;
    // the reported time is the FIRST frame of that hold.
    private FallCriterion.Latch fallLatch;
    private FallCriterion.Baseline standBaseline;
    // The settled stand height is measured over the same quiet stance window the
    // load meter's Wsup uses, and frozen at its end: never the spawn pose, never
    // a running maximum, so a body that has started to sink cannot lower its own
    // bar. Only frames leaning less than 45 deg are admitted.
    //
    // The window FOLLOWS THE WALK: always the one quiet second immediately before
    // the walk window opens (walkTime - 1.0 .. walkTime). With a short -walkTime
    // a hardcoded 4.0-5.0 s window would be recorded while the body is already
    // walking - already falling - and every load / residual baseline would be
    // measured off the wrong second. Derived once in the constructor from
    // info.RequestedWalkTime; for trials that do not walk RequestedWalkTime keeps
    // the plain 5 s default, so their window is still exactly 4.0-5.0 s and their
    // records stay byte-identical to previous labels. Clamped so an early (or
    // absent) walk still leaves a real window.
    private readonly float baselineWindowStart;
    private readonly float baselineWindowEnd;

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
    // HUMAN STEP CLOCK (harness only, 21.09): the executed physics steps of the
    // run, set by HeadlessTrial.RunTrial just before the summary is written - the
    // same audit field the dog recorder carries (DogTrialRecorder.cs). A healthy
    // step-driven run reads `samples == executedSteps` and
    // `simulatedSeconds == executedSteps * fixedDeltaTime`; a run that sampled a
    // cadence the physics never ran is visible in the JSON instead of being
    // averaged away.
    private int executedSteps = -1;
    private double sumComOffsetSquared;
    private double sumAbsComOffset;
    private double sumTorsoAngVelSquared;
    private float maxAbsTilt;
    private float fallTime = -1f;
    // First frame the body was actually down (physical); -1 while he is up.
    // `fell` and `survivedSeconds` read this one, not the automaton.
    private float physicalFallTime = -1f;
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
    // WALK CYCLE 51 meter: the palm's own pitch. The hand's world angle (deg) at
    // the first frame each palm grounds, the wrist's delivered joint angle at that
    // same frame, and the closest either hand came to the slab's parallel (90 deg
    // = the long axis horizontal) anywhere in the run. JSON only - no CSV column.
    private float handWorldAtGroundL, handWorldAtGroundR;
    private float wristAngleAtGroundL, wristAngleAtGroundR;
    private float handFlatErrorMinL = float.MaxValue, handFlatErrorMinR = float.MaxValue;
    private bool handGroundLatchedL, handGroundLatchedR;
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
    // The pose-library controller (the default walk path and every arena human):
    // read for the IK saturation bit only - the reporter never drives it.
    private PoseDriveController pose;
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

    // ─── C1 LOAD METER (append-only, recorder only) ───
    // Read by nothing but the CSV and the JSON: no controller sees these. The
    // vertical contact force is the solver's OWN impulse on the four sole plates
    // (ContactPoint2D.normalImpulse / dt), not a geometric probe and not a
    // hardcoded mass. Wsup is the measured total vertical force over the quiet
    // stance window (the second before the walk opens; 4.0-5.0 s for trials that
    // do not walk); the drop test in FallCriterion uses the pelvis height, so the
    // two are independent measurements of the same stance.
    private readonly Collider2D[] soleCollider = new Collider2D[4];   // heelL, toeL, heelR, toeR
    private readonly float[] soleFz = new float[2];                   // per side, N
    private readonly float[] solePlateMinX = new float[2];
    private readonly float[] solePlateMaxX = new float[2];
    private readonly float[] solePlateMinY = new float[2];
    // Contact points of one frame, kept so the CoP can be built only from the
    // points that actually lie on a sole plate (see SampleSoleLoad).
    private const int CopPointCapacity = 8;
    private readonly float[] copPointX = new float[2 * CopPointCapacity];
    private readonly float[] copPointW = new float[2 * CopPointCapacity];
    private readonly int[] copPointCount = new int[2];
    private readonly ContactPoint2D[] contactBuffer = new ContactPoint2D[24];
    private ContactFilter2D groundFilter;
    private bool groundFilterReady;
    private bool soleCollidersResolved;
    private float fzL;
    private float fzR;
    // C2a: footLoadedL/R are MEASURED load flags now, not the capture walker's
    // tap. The tap is written only while the walker ticks (so a stand window or
    // any stand-only run read 0.00 in every frame) and it FREEZES at the last
    // tick after the walker stops, which is worse than being empty. The flag
    // below is derived from the same solver impulses as fzL/fzR on EVERY step.
    // Stated threshold: a sole the solver pushes more than this many newtons
    // through is loaded. 20 N is 2.9 % of the 682 N measured baseline wSup, so
    // it means "the solver is carrying weight here", not "the plate is inside
    // the contactOffset band" (that band is 20 mm wide, see the C2a report);
    // the walk guard's 0.2 * wSup (~136 N) is the same test at a higher bar.
    public float footLoadFlagNewtons = 20f;
    private int footLoadFlagL;
    private int footLoadFlagR;
    private float fzOverWsup;
    private float xCopMeas;
    private int copValid;
    private int copRejected;
    private float hullMinX;
    private float hullMaxX;
    private float lambdaMeas;
    private float dcmNow;
    private float supportHeight;
    private float comVelX;
    // ─── PUNCH p12 — THE SUPPORT POLYGON'S OWN EDGE (recorder only) ───
    // The estimator's own polygon (the union of the GROUNDED colliders' bounds) is
    // published directly, with each edge attributed to the sole plate that carries
    // it, so the edge loss can be read instead of inferred from supportMargin.
    // supportGroundedMask bit i = the plate i grounded (1 footL, 2 toeL, 4 footR,
    // 8 toeR, the estimator's own four flags); *Id = that plate's index, or -1 when
    // no sole plate explains the edge (both feet airborne, or the squat's hands).
    // No physics column reads any of it.
    private float supportPolyMinX;
    private float supportPolyMaxX;
    private int supportMinId;
    private int supportMaxId;
    private int supportGroundedMask;
    // ─── PUNCH p16 — THE TOE/MTP JOINT'S OWN CHANNELS (recorder only) ───
    // The fourth hinge pair the record never carried: the MTP (the toe plate's own
    // hinge to the foot; LeftLegToe/RightLegToe, limits -35..+50 deg, Human.cs).
    // p14's own recorded debt - the pair's divergence at dt +0.90 could not be read
    // through it. Published per frame, the same three quantities the ankle carries:
    // the MEASURED joint angle (deg, the estimator's own toe angle; jointAngle =
    // heel - toe, plus = toe down / heel rise), the toe muscle pair's activation
    // (the demand's clamped witness, 0..1 of the 12 N*m ceiling) and the torque the
    // pair actually applies (N*m, the ankle's own AppliedTorque rule). Recorder
    // only: no controller reads any of these six fields, so no physics moves.
    private float toeAngleL;
    private float toeAngleR;
    private float actToeL;
    private float actToeR;
    private float appTorqueToeL;
    private float appTorqueToeR;
    private int ikSaturated;
    private int samplesIkSaturated;
    private double quietFzSum;
    private int quietFzCount;
    private float wSup;
    private float lastComXSample;
    private float lastComYSample;
    private float lastSoleBottomY;
    private bool hasComSample;

    // ─── A2/A3/A4 columns (append-only, 19.09.2026) ───
    // A2: the geometric support state, published twice - once as the controller
    // itself computes it (BalanceController.GeometricFallState) and once from
    // this recorder's own measured loaded-plate hull. A4: the pelvis vertical
    // velocity and its one-step difference (the squat test's output channel).
    private Rigidbody2D pelvisBody;
    private float pelvisVelY;
    private float pelvisAccY;
    private float lastPelvisVelYSample;
    private bool hasPelvisVelSample;
    private int ctrlFallState;
    private int hullFallState;
    private float ctrlFallFirstTime = -1f;
    private int ctrlFallFirstCode;
    private float ctrlFallenFirstTime = -1f;
    private float hullFallFirstTime = -1f;
    private float hullFallenFirstTime = -1f;
    private int samplesCtrlCritical;
    private int samplesCtrlFallen;
    private int samplesHullCritical;
    private int samplesHullFallen;
    private float maxAbsPelvisAccY;
    // A3: the IK workspace residual per leg, from the ACTUAL pelvis->ankle
    // vector (see SampleGeometricState). Index 0 = left, 1 = right.
    private readonly float[] ikReach = new float[2];
    private readonly float[] ikNear = new float[2];
    private readonly float[] ikResidKnee = new float[2];
    private readonly int[] ikGuardDy = new int[2];
    private readonly int[] ikGuardLen = new int[2];
    private readonly double[] sumIkResidAbs = new double[2];
    private readonly float[] maxIkResidAbs = new float[2];
    private readonly int[] samplesIkGuardDy = new int[2];
    private readonly int[] samplesIkGuardLen = new int[2];
    // WALK CYCLE 67 — THE REACH > SPAN PAIR (the placement owner's instrument).
    // A3 above measures the leg's ACTUAL chord (ikReach, pelvis-centre convention:
    // the point (pelvis x, pelvis centre y - pelvisSize.y/2) to the foot segment
    // centre the estimator publishes) and compares it to the SOLVER's own bars
    // (0.45..0.995 of thigh+shin). That comparison is blind to the one arithmetic
    // that actually decides whether the swing can catch: the leg's own SPAN - the
    // longest chord the built leg can put the foot centre on, thigh + shin +
    // footSize.y/2 (the ankle joint's own drop to the foot segment centre), scaled
    // live by the same thighSize the builder and the IK read. legSpan is that
    // length, per side, finite on every frame (0 with no body/estimator = the
    // block's sentinel, never NaN); ikOverSpan is 1 while EITHER leg's reach
    // exceeds its span, i.e. while the line asks the leg for a pose its own
    // geometry cannot reach (c66 measured exactly this at v>=0.15 - and
    // ikSaturated read 0/2785 through the fall).
    private readonly float[] legSpan = new float[2];
    private readonly int[] ikOverSpan = new int[2];
    private readonly int[] samplesIkOverSpan = new int[2];
    private readonly double[] sumIkReachOverSpan = new double[2];
    private float maxIkReachOverSpan = float.NegativeInfinity;
    // Same residual restricted to the quiet window (baselineWindowStart/End,
    // window wSup is measured in): the upright baseline the CSV reader must
    // subtract before calling a residual a workspace violation.
    private readonly double[] sumIkResidQuietAbs = new double[2];
    private readonly int[] samplesIkResidQuiet = new int[2];

    // ─── WALK CYCLE 68 — THE SOLE'S OWN SLIP AT STANCE ───
    // The eyes' "foot slide" / "no push-off" in numbers, per stance phase.
    // While a sole carries load the foot should hold its ground: this measures how
    // fast the LOADED foot's own rigid centre is dragged horizontally (m/s). Per
    // side the centre is the plate pair's combined collider-bounds midpoint (the
    // same two colliders the load meter sums); the gate is the lane's own load
    // flag (soleFz > footLoadFlagNewtons). A side with no previous sample or no
    // load reads 0 exactly; the column carries the FASTER of the two sides.
    // Recorder only - no controller reads any of it, and nothing in this block
    // writes a physics column (the fence must stay byte-identical).
    // Summary: soleSlipAtStanceMeanMps (over the frames any foot carried load),
    // soleSlipAtStanceMaxMps, soleSlipAtStanceLoadedFrames, and
    // soleSlipAtStanceStepMeansMps - one entry per walkSwap bucket (the walk's own
    // step counter; bucket k = the frames the run carried walkSwap == k).
    private readonly float[] soleSlipPrevX = new float[2];
    private readonly bool[] soleSlipPrevValid = new bool[2];
    private float soleSlipAtStance;
    private float soleSlipLoadedSum;
    private int soleSlipLoadedFrames;
    private float soleSlipMax;
    private int soleSlipStepKey = int.MinValue;
    private float soleSlipStepSum;
    private int soleSlipStepFrames;
    private readonly float[] soleSlipStepMeans = new float[256];
    private int soleSlipStepMeanCount;

    public TrialRecorder(Human human, string label)
        : this(human, new TrialRunInfo { Label = label })
    {
    }

    public TrialRecorder(Human human, TrialRunInfo info)
    {
        this.human = human;
        this.info = info ?? new TrialRunInfo();
        this.label = this.info.Label ?? "trial";

        // Quiet window = the one second before the walk opens (see the field
        // note): computed once, the walk time never moves during a run.
        baselineWindowStart = Mathf.Max(0.25f, this.info.RequestedWalkTime - 1.0f);
        baselineWindowEnd = Mathf.Max(baselineWindowStart + 0.25f, this.info.RequestedWalkTime);

        balance = human.GetComponent<BalanceController>();
        state = human.GetComponent<BodyStateEstimator>();
        com = human.GetComponent<CenterOfMassCalculator>();
        vestibular = human.GetComponent<VestibularSystem>();
        head = human.headSegment != null ? human.headSegment.transform : null;
        Transform pelvisTransform = human.transform.Find("Pelvis");
        pelvis = pelvisTransform;
        // A4: the pelvis rigid body's own velocity, so the vertical acceleration
        // column below is the solver's velocity differenced once, not a second
        // difference of positions (which doubles the quantisation noise).
        pelvisBody = pelvisTransform != null
            ? pelvisTransform.GetComponent<Rigidbody2D>()
            : null;
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
        pose = human.GetComponent<PoseDriveController>();

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
            // CYCLE 100 (the hand-plant read): the latched palm's world point
            // FORE-AFT of its shoulder pivot (metres, +x = ahead of the shoulder;
            // 0 = no live plant on that side), plus the armed get-up plant reach
            // command. The navel-vs-shoulder-zone acceptance reads this pair.
            .Append("armPlantForeL").Append(';')
            .Append("armPlantForeR").Append(';')
            .Append("armGetUpReach").Append(';')
            // ─── CYCLE 110 — THE GRIP ROW (append-only; the owner's 02.10 ask:
            // the palms skate during the get-up; the grab must bite). gripEnabled
            // = -poseHandGrip armed; getUpStage = the pose ladder's stage
            // (Idle=0); gripWantsL/R = the press latch's ask; gripHeldL/R = the
            // grip material on that palm; gripFnL/R = the palm's own measured
            // normal load, N; gripLoadL/R in body weights; gripSlipL/R = the
            // anchored slip, m; gripEngagesL/R / gripTearsL/R = cumulative
            // counters; gripMuMat = the palm material's live friction;
            // gripMuPair = the pair coefficient the engine combines;
            // gripPalmXL/YL/XR/YR = the palm's world point (the skate witness).
            .Append("gripEnabled").Append(';')
            .Append("getUpStage").Append(';')
            .Append("gripWantsL").Append(';')
            .Append("gripWantsR").Append(';')
            .Append("gripHeldL").Append(';')
            .Append("gripHeldR").Append(';')
            .Append("gripFnL").Append(';')
            .Append("gripFnR").Append(';')
            .Append("gripLoadL").Append(';')
            .Append("gripLoadR").Append(';')
            .Append("gripSlipL").Append(';')
            .Append("gripSlipR").Append(';')
            .Append("gripEngagesL").Append(';')
            .Append("gripEngagesR").Append(';')
            .Append("gripTearsL").Append(';')
            .Append("gripTearsR").Append(';')
            .Append("gripMuMat").Append(';')
            .Append("gripMuPair").Append(';')
            .Append("gripPalmXL").Append(';')
            .Append("gripPalmYL").Append(';')
            .Append("gripPalmXR").Append(';')
            .Append("gripPalmYR").Append(';')
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
            // armAimVoteL/R — APPEND-ONLY. The +/-1 that the old sign VOTE
            // produced for the press aim, next to armAimSignL/R which is the sign
            // the drive actually used (now +1 by construction: a HingeJoint2D
            // rotates its child by exactly its joint angle, so the upper arm's
            // world direction moves one for one with it on BOTH sides — the
            // mirrored arms differ in their rest direction, not in the
            // derivative). p_arms34 recorded vote -1 on both sides while the
            // columns show the +1 relation, and the drive followed the vote: the
            // left palm never reached its own latched point.
            .Append("armAimVoteL").Append(';')
            .Append("armAimVoteR").Append(';')
            // armAimBranchL/R — the elbow branch the press is holding (0 = upper
            // arm at beta+alpha, 1 = beta-alpha, -1 = unsolved). Both branches can
            // be legal at once; if this column flips every 1-3 frames the PD is
            // chasing a posture it cannot hold and the palm never lands.
            .Append("armAimBranchL").Append(';')
            .Append("armAimBranchR").Append(';')
            // ─── armWindow* — APPEND-ONLY (arms cycle 8) ───
            // The matched press window: latched on the frame the body goes DOWN
            // and sampled while it is down, so these columns exist on the
            // press-OFF control too. armWindowSeconds is the window's own length,
            // armWindowRiseL/R the shoulder-pivot rise inside it, armWindowTravelX
            // the body's own travel along the slab: a shove and a lift are told
            // apart only when both are read in ONE window. The stroke-scoped
            // armShoulderLiftL/R above is 0 on the control by construction (no
            // stroke ever starts there), which is exactly why the press claim
            // needed this pair.
            .Append("armWindowSeconds").Append(';')
            .Append("armWindowRiseL").Append(';')
            .Append("armWindowRiseR").Append(';')
            .Append("armWindowPelvisRise").Append(';')
            .Append("armWindowTravelX").Append(';')
            .Append("armWindowContactL").Append(';')
            .Append("armWindowContactR").Append(';')
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
            // and the measured load flags (see SampleSoleLoad: fz* above a
            // stated threshold, every frame, walker or not).
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
            .Append("demandAnkleR").Append(';')
            // ─── C1 LOAD METER (append-only, indices stable) ───
            // fzL/fzR/fzTotal — vertical contact force per foot and in total, N,
            //    from the solver's own contact impulses on the heel+toe plates
            //    (ContactPoint2D.normalImpulse / dt).
            // wSup — the MEASURED total vertical force in quiet stance
            //    (the second before the walk opens; 4.0-5.0 s when the trial
            //    does not walk), N: the denominator, never a hardcoded mass.
            // fzOverWsup — fzTotal / wSup: 1.0 means the body is carried.
            // xCopMeas — centre of pressure x from the same impulses, m.
            // hullMin/hullMax — support hull of the LOADED plates, m (zero width
            //    at the CoM when nothing is loaded).
            // lambdaMeas — right foot's share of the total load, 0..1.
            // dcm — capture point comX + comVelX*sqrt(h/g), m.
            // supportH — CoM height above the loaded sole bottom, m.
            // comVelX — one-step finite difference of comX, m/s.
            // ikSaturated — 1 when the IK that last drove a leg had to clamp
            //    (the capture walker's chord clamp or the pose controller's
            //    dy/length clamp); 0 otherwise.
            .Append("fzL").Append(';')
            .Append("fzR").Append(';')
            .Append("fzTotal").Append(';')
            .Append("wSup").Append(';')
            .Append("fzOverWsup").Append(';')
            .Append("xCopMeas").Append(';')
            .Append("hullMin").Append(';')
            .Append("hullMax").Append(';')
            .Append("lambdaMeas").Append(';')
            .Append("dcm").Append(';')
            .Append("supportH").Append(';')
            .Append("comVelX").Append(';')
            .Append("ikSaturated").Append(';')
            // copValid — 1 when the CoP above came from contact points lying on
            //    a sole plate; 0 when no such point existed and the cell is the
            //    CoM fallback (never to be read as a measurement).
            // copRejected — manifold points of this frame that were NOT on a
            //    plate (Box2D vertex-face points from the ground's corner).
            .Append("copValid").Append(';')
            .Append("copRejected").Append(';')
            // ─── A2/A3/A4 columns (append-only, 19.09.2026) ───
            // pelvisVelY/pelvisAccY — the pelvis rigid body's vertical velocity
            //    and its one-step difference, m/s and m/s^2 (A4: the scripted
            //    squat-and-rise -squatRise 1 is measured on these two beside
            //    fzTotal).
            // ctrlFallState — the CONTROLLER's own geometric state, from
            //    BalanceController.GeometricFallState: 0 supported, 1 critical
            //    (the capture point is outside the support polygon, so no CoP
            //    inside it can stop the mass), 2 fallen (the CoM itself is
            //    outside), 3 no support (no foot grounded). Geometry only, no
            //    timer and no latch - see Assets/BodySupportGeometry.cs.
            // hullFallState — the same ladder computed by this recorder against
            //    the measured loaded-plate hull already in this row
            //    (hullMin/hullMax + comX + comVelX + supportH): "no support"
            //    means fzTotal <= 0.2 * wSup. It is here so the state is checkable
            //    from the columns beside it; the controller's version uses its own
            //    ankle-referenced CoM height, ~4 % different omega.
            // ikReachL/R — the ACTUAL pelvis->ankle chord of that leg, m, from
            //    the pelvis centre (y - pelvisSize.y/2, the pose drive's own hip
            //    anchor) to the foot centre the estimator publishes.
            // ikNearL/R — the nearest reachable chord to it, m: the same vector
            //    clamped into the pose drive's workspace [0.45*(a+b), 0.995*(a+b)]
            //    with a, b = thighSize.y, shinSize.y (PoseDriveController.SolveLegIk).
            // ikResidKneeL/R — the angular workspace residual, degrees:
            //    reqKnee - the knee flexion that nearest reachable chord implies
            //    (2*acos(near/(a+b))). 0 = the command is inside the workspace the
            //    real leg occupies. A quiet stand does NOT floor at 0: the
            //    measured upright chord is 0.999 of the bars (a 4.5-5.1 deg knee)
            //    while the solver caps at 0.995 (11.46 deg), so a healthy stand
            //    reads about -7 deg. The JSON carries the measured baseline
            //    (ikResidKneeQuietMeanAbs*), so the floor is never a violation.
            // ikGuardDyL/R — 1 when the pose drive's dy >= -0.12 m floor fires on
            //    the ACTUAL vector (the ankle sits above the hip's 0.12 m floor).
            // ikGuardLenL/R — 1 when its length clamp fires on the ACTUAL vector
            //    (chord below 0.45*(a+b) or above 0.995*(a+b)). Those are the
            //    solver's two guards, evaluated on both legs every frame; the
            //    solver itself evaluates them only on the leg it is solving.
            //    READ THE STAND BASELINE BEFORE USING THEM: the solver's upper
            //    bar is 0.995*(a+b) = 0.8557 m on 0.86 m bars while the measured
            //    quiet-stance chord is 0.999 of the bars (0.859 m), so a healthy
            //    stand sits just ABOVE the bar and ikGuardLen reads 1 there by
            //    construction - both fractions are in the JSON
            //    (ikGuardLenFractionL/R) so the stand's own duty is always known.
            //    The walker's own IK (SolveCaptureLegIk) uses even tighter bars
            //    (l1+l2-0.01 = 0.85 m); both solvers are named here because the
            //    walk path is solved by the walker, not by the pose drive.
            .Append("pelvisVelY").Append(';')
            .Append("pelvisAccY").Append(';')
            .Append("ctrlFallState").Append(';')
            .Append("hullFallState").Append(';')
            .Append("ikReachL").Append(';')
            .Append("ikReachR").Append(';')
            .Append("ikNearL").Append(';')
            .Append("ikNearR").Append(';')
            .Append("ikResidKneeL").Append(';')
            .Append("ikResidKneeR").Append(';')
            .Append("ikGuardDyL").Append(';')
            .Append("ikGuardDyR").Append(';')
            .Append("ikGuardLenL").Append(';')
            .Append("ikGuardLenR").Append(';')
            // ─── WALK CYCLE 6j (append-only, indices stable) ───
            // The DELIVERED plant geometry, published per frame by
            // BalanceControllerCaptureWalk.DriveCaptureSwingLeg. Before these
            // four columns the 0.43-0.75 m live ask and the ~0.18 m sole the
            // third plant actually delivered could only be INFERRED from
            // hullMax/soleClear/comX to ~0.03-0.04 m; with the point handed to
            // the solver, the plan target in force, the age the swing curve was
            // evaluated at and the swing ankle's own measured world X recorded,
            // the clamp that owns the loss is named by measurement.
            // captureSwingAskX — X handed to SolveCaptureLegIk this frame, m
            //    (post reach disc, pre chord clamp).
            // captureSwingSoleX — the swing ankle's measured world X in that
            //    same frame, m. ask - sole is the error the leg still has to
            //    travel; both 0 = the walker is not running.
            // captureSwingTrackAge — the age the swing curve was evaluated at,
            //    s, after the WALK CYCLE 6i airborne clock.
            // capturePlanTargetX — the plan target in force that frame, m,
            //    after the reach-disc X clamp and the live-ask floor.
            .Append("captureSwingAskX").Append(';')
            .Append("captureSwingSoleX").Append(';')
            .Append("captureSwingTrackAge").Append(';')
            .Append("capturePlanTargetX").Append(';')
            // ─── WALK CYCLE 6k (append-only, indices stable) ───
            // The solver's OWN answer, next to the point it was handed (6j).
            // 6j left the 0.457 m at the plant between two owners — the
            // solver's input-to-solution kinematics, or the drive receiving
            // something other than the ask — because nothing recorded what the
            // solver believes it achieved. Forward kinematics of the returned
            // angles is that belief.
            // captureSwingSolvedSoleX — world X of the ankle the returned
            //    (thighWorld, kneeFlex) put the ankle at, m.
            // captureSwingSolvedResidualX — captureSwingAskX minus
            //    captureSwingSolvedSoleX, m; ~0 = the solver gave back the
            //    point it was handed, so the loss is downstream of the solver.
            // captureSwingSolveFrameX — world X of the hip hinge the chord was
            //    measured from, m (the origin of the solve).
            // captureSwingSolveFrameAngleDeg — the parent tilt the returned
            //    world thigh angle is converted into a joint command against
            //    (the plan's pelvis tilt), deg: the one body frame in a
            //    world-space solve, and the number that lets the plant's
            //    -22.4 deg trunk tilt be tested as the owner.
            // All four are 0 while the walker is not running, never NaN.
            .Append("captureSwingSolvedSoleX").Append(';')
            .Append("captureSwingSolvedResidualX").Append(';')
            .Append("captureSwingSolveFrameX").Append(';')
            .Append("captureSwingSolveFrameAngleDeg").Append(';')
            // ─── WALK CYCLE 6l (append-only, indices stable) ───
            // The pose that was APPLIED, next to the one the solver returned
            // (6k) and the point it was handed (6j). 6k proved the solver's own
            // answer lands on the ask to the digit and left the 0.457 m at the
            // plant in the DELIVERY, between two owners nothing separated: the
            // frame the returned world angles are written into a joint command
            // against, and the clamps/tracking that stand between those angles
            // and the targets the joints are given. These four columns are 6k's
            // own forward kinematics rebuilt on the MEASURED parent and on the
            // post-clamp targets, so both halves are subtracted in the CSV:
            //     solvedSole - appliedSole   = the clamps / tracking
            //     appliedSole - measuredSole = the parent frame
            // captureSwingAppliedSoleX — world X of the ankle the DRIVEN pose
            //    puts the ankle at this frame, m (measured hinge + measured
            //    parent tilt + post-clamp hip/knee targets). Commanded pose,
            //    not the measured leg.
            // captureSwingAppliedResidualX — captureSwingAskX minus
            //    captureSwingAppliedSoleX, m; ~0 = the applied chain lands on
            //    the ask and the parent frame is not the owner.
            // captureSwingAppliedFrameX — world X of the measured hip hinge
            //    the applied chain was built on, m; coincides with
            //    captureSwingSolveFrameX by construction.
            // captureSwingAppliedFrameAngleDeg — the MEASURED parent tilt the
            //    applied joint command is written in, deg; subtract
            //    captureSwingSolveFrameAngleDeg to read the frame mismatch.
            // All four are 0 while the walker is not running, never NaN.
            .Append("captureSwingAppliedSoleX").Append(';')
            .Append("captureSwingAppliedResidualX").Append(';')
            .Append("captureSwingAppliedFrameX").Append(';')
            .Append("captureSwingAppliedFrameAngleDeg").Append(';')
            // ─── WALK CYCLE 6n (append-only, indices stable) ───
            // The ask's own HEIGHT, published before any number moved, and the
            // flexion demand that ask carries in the frame it is delivered in.
            // 6m closed the frame defect and left the plant's ask unmeasurable in
            // one coordinate: the flexion an ask needs is a function of the
            // sole's height as much as its X (the chord angle grows with X, the
            // thigh-to-chord angle grows as the chord shortens, i.e. as the
            // ankle is asked higher), so the 65 deg allowance could not be told
            // from a Y that asks for too short a chord. The second column is the
            // pre-bound demand, so the 6n allowance spending itself on the ask
            // never hides the demand it refused.
            // captureSwingAskY — world Y of the point handed to
            //    SolveCaptureLegIk this frame, m (up is plus), the same point
            //    captureSwingAskX is the X of: post reach disc, post
            //    CaptureSwingAnkleYForChord, post the 6n flexion allowance.
            // captureSwingHipDemandDeg — the hip flexion that point carries in
            //    the frame the command is delivered in, deg, BEFORE the 6n
            //    allowance bounds it (more negative = more flexion asked for);
            //    frame minus the world thigh the solve returns for the ask.
            // Both are 0 while the walker is not running, never NaN.
            .Append("captureSwingAskY").Append(';')
            .Append("captureSwingHipDemandDeg").Append(';')
            // ─── WALK CYCLE 6o (append-only, indices stable) ───
            // The frame the plan TARGET is built in. 6j..6n publish what the
            // target is worth and what the body is doing on the frame the ask is
            // solved, but nothing says what frame the target was COMPUTED in:
            // the target is frozen once and delivered over a whole swing, so the
            // measured tilt on the row it lands on is not the tilt it was built
            // against and the reader cannot tell the plan moving the target from
            // the body leaving the plan's frame (c6n_walk: target 0.5303 -> 0.7839 m
            // while the measured tilt runs to −37.6 deg). Appended at the END so
            // every column before it keeps its index.
            // capturePlanFrameTiltDeg — the measured pelvis tilt at the freeze
            //    that built the plan in force, deg, world frame, minus is forward
            //    (the recorder's pelvisTilt convention); held while that plan is
            //    in force. The reader's split is
            //      pelvisTilt - capturePlanFrameTiltDeg
            //    = the pitch the delivered target was not built in. 0 while the
            //    walker is not running and before the first freeze, never NaN.
            .Append("capturePlanFrameTiltDeg").Append(';')
            // ─── WALK CYCLE 6p (append-only, indices stable) ───
            // The POSTURE ANSWER the delivered command carries. 6j..6o publish what
            // the target is worth, what the body is doing and what frame the plan
            // was built in, and 6o pays the split out of the swing hip's flexion
            // allowance — but nothing said what the delivered hip command does with
            // the posture itself, so "the pitch is charged to the leg" and "the
            // pitch is answered by the joint" could not be told apart in the row.
            // Appended at the END so every column before it keeps its index.
            // captureSwingPostureBiasDeg — the bounded posture error the swing hip's
            //    delivered command carried this frame, deg of hip joint angle,
            //    positive = the ask sits on the EXTENSION side of the IK pose (the
            //    direction that pitches the pelvis back). Bounded by the walker's
            //    own 45 N*m loaded-hip band in degrees (3.0). Held like the 6j-6o
            //    set; 0 while the walker is not running or with no positive
            //    plan-frame drift, never NaN.
            .Append("captureSwingPostureBiasDeg").Append(';')
            // ─── WALK CYCLE 6q (append-only, indices stable) ───
            // 6j..6p published what the LEVEL 1 walker delivered. This cycle put a
            // SECOND path behind -poseDrive 1 (default off): the level 2 pose
            // library, which writes the same six leg joints from a named pose
            // table. A row therefore has to say which path produced it, or a
            // level 2 run and a level 1 run cannot be told apart by their rows.
            // Appended at the END so every column before it keeps its index.
            // captureWalkPath — code, no units, no frame: 0 = the level 1 path
            //    (the capture walker and every layer before it; also the sentinel
            //    written by ResetCaptureWalk), 1 = the level 2 pose library held
            //    the joints on this row. Recorder only; no NaN.
            .Append("captureWalkPath").Append(';')
            // ─── WALK CYCLE 6s (append-only, indices stable) ───
            // 6q said WHICH path wrote the leg joints on a row; nothing said what
            // the LEVEL 2 path ASKED FOR. The reversal that costs the walk its
            // row (c6r2_walk: comX 0.508 at 6.4 s, 0.497 at 7.0, comVelX -0.599,
            // fell 7.255 s) was therefore readable only through the library's
            // numbers AFTER the PD had them (hip*/knee*/ankle*) and the torques
            // it applied (appTorque*) - and those two cannot be told apart in the
            // reversal. These 15 columns are the request itself: the six joint
            // angles the library handed the PD on this step, the hip height it
            // was asked to hold against the height it had, the pelvis pitch it
            // targets, the step clock's swap decision, and whether the pose gait
            // is driving at all. Appended at the END so every column before it
            // keeps its index. 0 is the sentinel for "the pose gait is not
            // driving the WALK on this step" (the -poseDrive switch is off, the
            // gait is standing, or its own down test relaxed the body), never NaN
            // — see the publication block in PoseDriveController.cs.
            // capturePoseTargetHipLDeg / KneeL / AnkleL / HipR / KneeR / AnkleR —
            //    the pose library's own joint angle requests for this step, deg,
            //    per RIG side, rig convention (hip minus = flexion, knee plus =
            //    flexion, ankle plus = toe down). The swing leg is the one whose
            //    knee target is the folded one; the stance leg's hip target is the
            //    one the trunk hold is spent on.
            .Append("capturePoseTargetHipLDeg").Append(';')
            .Append("capturePoseTargetKneeLDeg").Append(';')
            .Append("capturePoseTargetAnkleLDeg").Append(';')
            .Append("capturePoseTargetHipRDeg").Append(';')
            .Append("capturePoseTargetKneeRDeg").Append(';')
            .Append("capturePoseTargetAnkleRDeg").Append(';')
            // capturePoseHipHeightRequestM — the hip height the balance layer
            //    asked the gait to hold, m (the stand frame's height).
            // capturePoseHipHeightLiveM — the hip height the body had, m.
            //    Request minus live is the height loop's own error, in metres.
            // capturePosePelvisTiltTargetDeg — the pelvis world tilt the pose in
            //    force owns, deg, minus = forward (the row's pelvisTilt sign);
            //    subtract it from the row's pelvisTilt for the pose's pitch error,
            //    whose bounded answer is the trunk correction on the stance hip.
            .Append("capturePoseHipHeightRequestM").Append(';')
            .Append("capturePoseHipHeightLiveM").Append(';')
            .Append("capturePosePelvisTiltTargetDeg").Append(';')
            // capturePoseStepIndex — the index of the step in force: the number of
            //    swaps the pose gait has fired (the same number walkSwap carries,
            //    published inside the pose block so the block reads alone).
            // capturePoseMaySwap / ClockDone / LedOut — the three inputs of the
            //    swap decision as the pose gait computed them on this step, 0/1.
            // capturePoseSwapReason — which of them fired the swap on the step a
            //    swap happened: 0 none, 1 the half cycle ran out, 2 the CoM led
            //    the stance foot, 3 the planted timeout (a swing that never
            //    landed). Frozen codes.
            .Append("capturePoseStepIndex").Append(';')
            .Append("capturePoseMaySwap").Append(';')
            .Append("capturePoseClockDone").Append(';')
            .Append("capturePoseLedOut").Append(';')
            .Append("capturePoseSwapReason").Append(';')
            // capturePoseDriveState — 0 the pose gait is stopped (the switch is
            //    off, or its own down test relaxed the body), 1 it is driving the
            //    STAND pose, 2 it is driving the WALK. The column that answers the
            //    post-fall runaway: `down` = 1 with this at 2 is a pose gait still
            //    driving a body already on the slab.
            .Append("capturePoseDriveState").Append(';')
            // ─── WALK CYCLE 6x (append-only, indices stable) ───
            // 6s published what the pose gait ASKED the joints for; nothing
            // published where the POSE PATH ITSELF placed the foot. c6w_walk
            // failed backwards (fell 7.625 s, swaps 3, comTravelX -1.4659 m) with
            // its brake at the swaps - CoM +0.204 m in step 1 and only +0.034 m in
            // step 2, comVelX -0.448 at the third swap, the CoM 6 cm behind the
            // support edge while still moving - and the owner could not be read:
            // the level 1 columns (captureSwingAskX / SoleX / PlanTargetX /
            // SolvedSoleX / AppliedSoleX / AskY / HipDemandDeg / planValid /
            // PlanFrameTiltDeg / PostureBiasDeg) are 0 on all 524 walk rows while
            // captureWalkPath = 1, i.e. the pose library drove and its own
            // placement was published nowhere. "The pivot landed N m ahead of the
            // CoM and the body could not follow" was therefore a hypothesis.
            // These four columns are that placement. Appended at the END so every
            // column before them keeps its index. 0 is the block's sentinel for
            // "the pose gait is not driving the WALK on this step" (the same
            // convention as the 6s block above), never NaN - see the publication
            // block in PoseDriveController.cs.
            // capturePosePlaceXM — the ASK: the pivot x the library's placement was
            //    resolved for on this frame, m, world, i.e. placeX after BOTH of the
            //    walk's clamps (the step band of the step in force and the leg's own
            //    reach from the live hip ANCHOR, whose ceiling and centre are the
            //    6y columns capturePoseReachXM / capturePoseHipXM at the end of
            //    this row) - the exact value handed to
            //    PoseLibrary.Inputs.footPlaceX this frame. Live, not a per-step
            //    datum. Read it against capturePoseSwingSoleXM (ask vs placed).
            // capturePoseSwingSoleXM / capturePoseStanceSoleXM — the APPLIED: where
            //    the swing and the stance foot's sole are on this frame, x, m,
            //    world. The library names no sole or ankle world x, so this is the
            //    foot's own measured position (BodyStateEstimator's foot position,
            //    the centre of the foot plate) — the same point the step clock reads
            //    as swingFootX / stanceFootX and the placement clamp is measured
            //    from. No geometry is invented.
            // capturePosePlaceLeadM — THE BRAKE DISTANCE: capturePosePlaceXM minus
            //    the row's own comX, latched at the step's PLANT frame (the frame
            //    the swing foot's sole comes back onto the floor after its own
            //    lift; the signal is the same measured foot contact the step clock's
            //    swap gate reads), signed, + = the pivot is AHEAD of the CoM. Held
            //    from that plant until the next one, so a driving row carries the
            //    most recent plant's lead. This is the number the next cycle's
            //    single change reads.
            .Append("capturePosePlaceXM").Append(';')
            .Append("capturePoseSwingSoleXM").Append(';')
            .Append("capturePoseStanceSoleXM").Append(';')
            .Append("capturePosePlaceLeadM").Append(';')
            // ─── WALK CYCLE 6y (append-only): THE REACH GUARD'S OWN NUMBERS ───
            // The guard that resolved capturePosePlaceXM published nothing of its
            // own in 6x, so its degeneracy had to be recomputed from the other
            // columns; these two ARE that guard. Same block sentinel rule as
            // above - 0 = the pose gait is not driving the WALK on this step,
            // never NaN - plus one sentinel of the guard's own (-1, see below).
            // capturePoseReachXM — the CEILING the reach guard applied to the
            //    placement on this frame, m: sqrt(leg^2 - hipToSole^2), leg =
            //    thighSize.y + shinSize.y, hipToSole measured from the HIP JOINT
            //    ANCHOR (the pelvis centre minus pelvisSize.y/2, the point the
            //    leg rotates about - PoseDriveController.SolveLegIk's own
            //    `hipY = pelvisY - pelvisSize.y * 0.5f`) down to the sole's floor
            //    (footSize.y/2 below the ground datum). It is the HALF WIDTH of
            //    the box the pivot was allowed to land in,
            //    [capturePoseHipXM - reach, capturePoseHipXM + reach], ramped in
            //    by the CoM's need. WALK CYCLE 6y moved the anchor from the pelvis
            //    CENTRE (6x: this number was 0 on all 353 rows where the band did
            //    not bind, hipToSole 0.9122 m against the 0.86 m of thigh + shin,
            //    so capturePosePlaceXM was clamped onto the pelvis x) to the hip
            //    anchor, where it is a positive physical number.
            //    -1 = THE GUARD WAS INERT on this frame
            //    (PoseDriveController.PoseReachInertM): hipToSoleM >= leg, the
            //    reach circle does not meet the floor, so no clamp and no ramp were
            //    applied and the step band alone bounded placeX.
            //    The guard's own height is recoverable from this column:
            //    (hip anchor y - groundY) = footSize.y/2 + sqrt(leg^2 - reach^2).
            // capturePoseHipXM — the hip ANCHOR x the guard measured from, m,
            //    world (the pelvis body's own x: the anchor hangs directly below
            //    the pelvis centre in this rig). Read against the row's comX and
            //    capturePosePlaceXM: with the 6x input the box was zero wide about
            //    this x, i.e. the pivot WAS this column on every pulled row.
            .Append("capturePoseReachXM").Append(';')
            .Append("capturePoseHipXM").Append(';')
            // WALK CYCLE 6z — THE HORIZONTAL CHANNEL AND THE PIVOT'S OWN
            // RESOLVER, appended LAST (the walk CSV goes 223 -> 226 columns) in
            // the same order as the row below, so every column before them keeps
            // its index. All three are recorder-only: no controller reads one.
            // capturePoseGroundFxN — the MEASURED horizontal ground reaction the
            //    body's own feet receive on this step, N, +x = the walk's forward
            //    direction: the x component of the same solver impulses
            //    (ContactPoint2D.normalImpulse / tangentImpulse on the four sole
            //    plates) the fzL/fzR load meter sums on the vertical, read from
            //    the pose drive's own copy of that meter (PoseDriveController.
            //    SampleGroundReactionX) and NOT re-derived from joint torques.
            //    A contact's impulse vector is normalImpulse * n +
            //    tangentImpulse * t with t = (n.y, -n.x) (Box2D's own tangent,
            //    b2_contact_solver.cpp), and only contacts that push the plate
            //    along +n (the load meter's own support test) are summed, so this
            //    column and fzL/fzR describe the same contact set. The sign is
            //    checkable against the row's own next column: sum(Fx) is the only
            //    horizontal external force on the body, so a positive regression
            //    slope of this column against mass * capturePoseComAccelXMps2
            //    confirms it (a negative slope = read the column backwards).
            //    Sentinels: 0 exactly = the meter ran and neither foot carried
            //    load on this step (a reading, not a gap); -1000000
            //    (PoseDriveController.PoseGroundFxNoMeasureN) = the meter could
            //    not run (no sole plate resolved, or -poseDrive 0 was never
            //    ticked, in which case the whole run carries it).
            // capturePoseComAccelXMps2 — d(comVelX)/dt, m/s^2, a one-step finite
            //    difference (5 ms at the trial step) of the rig's own measured
            //    CoM velocity (BodyStateEstimator.comVelocity.x, the same
            //    position series the comVelX column is built from). The rig
            //    carries no smoothed acceleration estimator, so this is a finite
            //    difference and is read as a trend, not frame by frame. 0 on the
            //    first tick of a run.
            // capturePoseResolvedTerm — WHICH TERM RESOLVED THE PIVOT placeX on
            //    this row, one integer, decided in PoseDriveController.TickWalk's
            //    placement block where the value is decided (never re-derived
            //    here): 1 = the catching law (capture point + placementMarginMeters)
            //    survived the step band; 2 = the band's FLOOR bound it
            //    (placeX == capturePoseStanceSoleXM + stepMinMeters exactly);
            //    3 = the band's CEILING; 4 = the leg's reach guard moved the value
            //    (its clamp + the CoM-need ramp); 5 = the guard was INERT on this
            //    frame (the hip anchor at or above the leg's span - the reach
            //    circle does not meet the floor), so only the law and the band
            //    could act, and the row's capturePoseReachXM reads the -1
            //    sentinel; 0 = this row is not driving the walk (the same rows
            //    capturePoseDriveState reports as not 2). Codes 1..4 can only
            //    appear with capturePoseReachXM != -1 and code 5 only with
            //    capturePoseReachXM == -1.
            .Append("capturePoseGroundFxN").Append(';')
            .Append("capturePoseComAccelXMps2").Append(';')
            .Append("capturePoseResolvedTerm").Append(';')
            // ─── WALK CYCLE 67 — THE REACH > SPAN PAIR (append-only, indices stable) ───
            // The two instruments the c66 finding asked for, appended LAST (the walk
            // CSV goes 226 -> 229 columns) in the same order as the row below, so
            // every column before them keeps its index. Recorder-only: no controller
            // reads either one.
            // legSpanL / legSpanR — the LIVE built span of the leg, m:
            //    thighSize.y + shinSize.y + footSize.y/2. That sum is the longest
            //    chord the leg can put the foot segment's centre on measured from the
            //    hip joint (the ankle hinge's own drop to the foot centre is
            //    footSize.y/2), read per frame from the same sizes the builder and
            //    the IK read - so a foreshortened thigh moves it with the body (the
            //    c66 ON rows: 0.9050 -> 0.8405 at v=0.15 -> 0.7975 at 0.25 ->
            //    0.7330 at 0.40, exactly -0.43*v). Read it against the row's own
            //    ikReachL/R: reach > span = the walk asks the leg for a pose its own
            //    geometry cannot reach. 0 = not measured (no body or no estimator
            //    yet) - the 6x block's sentinel, never NaN.
            // ikOverSpan — 1 while EITHER leg's ikReach exceeds its legSpan on this
            //    row, else 0. The column ResolveIkSaturated now also fires on (WALK
            //    CYCLE 67), i.e. `ikSaturated` = 1 contains this witness beside the
            //    solvers' own clamp witness; published separately so a reader can
            //    tell WHICH fired.
            .Append("legSpanL").Append(';')
            .Append("legSpanR").Append(';')
            .Append("ikOverSpan").Append(';')
            // soleSlipAtStance — WALK CYCLE 68: the fastest horizontal drift of a
            // LOADED sole (plate-pair centre, m/s) on this row; 0 exactly when no
            // sole carries load (fz > footLoadFlagNewtons) or the foot holds its
            // ground. Finite by construction, never NaN.
            .Append("soleSlipAtStance").Append(';')
            // supportMinX/supportMaxX — PUNCH p12: the estimator's own support polygon's
            // two edges (the union of the GROUNDED colliders' bounds, m), published
            // directly so the edge can be read instead of inferred from supportMargin.
            // supportMinId/supportMaxId = the sole plate carrying that edge (0 LeftLegFoot,
            // 1 LeftLegToe, 2 RightLegFoot, 3 RightLegToe), -1 when no plate explains it.
            // supportGroundedMask = the estimator's own four grounded flags as bits
            // (1 footL, 2 toeL, 4 footR, 8 toeR). Recorder only; finite, never NaN.
            .Append("supportMinX").Append(';')
            .Append("supportMaxX").Append(';')
            .Append("supportMinId").Append(';')
            .Append("supportMaxId").Append(';')
            .Append("supportGroundedMask").Append(';')
            // PUNCH p16 — the toe/MTP joint's own channels, appended LAST (the walk
            // CSV goes 235 -> 241 columns): toeL / toeR = the measured MTP angle
            // (deg, the estimator's own toe jointAngle; plus = toe down / heel rise,
            // minus = toe up / swing clearance, the rig's own limits -35..+50 deg);
            // actToeL / actToeR = the toe muscle pair's activation (0..1 of the
            // 12 N*m ceiling - the demand's clamped witness); appTorqueToeL /
            // appTorqueToeR = the torque that pair actually applies (N*m, signed in
            // the ankle column's own convention). Recorder only; finite, never NaN.
            .Append("toeL").Append(';')
            .Append("toeR").Append(';')
            .Append("actToeL").Append(';')
            .Append("actToeR").Append(';')
            .Append("appTorqueToeL").Append(';')
            .Append("appTorqueToeR").Append(';')
            // WALK CYCLE 126 — THE ANKLE LOOP'S OWN NUMBERS, appended LAST in the
            // same order as their read block below (the walk CSV goes 241 -> 253
            // columns): the loop's own comX and reference (m, world x), its two
            // normalized channels (offsetN, velN), its two TERMS (P and D) and the
            // pre-clamp raw sum, the final command (post the clamp and the k17
            // head guard), the signal each ankle ACTUALLY received (post the
            // stop-margin protect; 0 while a side is not down), and the two
            // ground flags the loop saw (0/1). All finite by construction; the
            // pose drive's last-computed values persist on steps its path is not
            // driving (the pose path runs the loop every frame of a stand).
            .Append("ankLoopComX").Append(';')
            .Append("ankLoopRefX").Append(';')
            .Append("ankLoopOffN").Append(';')
            .Append("ankLoopVelN").Append(';')
            .Append("ankLoopP").Append(';')
            .Append("ankLoopD").Append(';')
            .Append("ankLoopRaw").Append(';')
            .Append("ankLoopCmd").Append(';')
            .Append("ankLoopSigL").Append(';')
            .Append("ankLoopSigR").Append(';')
            .Append("ankLoopLd").Append(';')
            .Append("ankLoopRd").Append(';')
            // WALK CYCLE 127: the crouch gate's own input - the crouch's
            // load-shaped level the gate scales by (0 on any frame that never
            // crouches). Appended after the c126 set; order matches the rows.
            .Append("ankLoopCrouchLoad").AppendLine();
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

    // Called by HeadlessTrial.RunTrial before the summary is written: how many
    // physics steps the run really executed (the human `humanStepping` block,
    // the dog recorder's own MarkExecutedSteps).
    public void MarkExecutedSteps(int steps)
    {
        executedSteps = steps;
    }

    // TILT STAND (29.09, the lane's per-step log): the fall the shared criterion
    // observed on this run, -1 while the body stayed up. Read-only - the criterion
    // itself is untouched (FallCriterion.cs stays the one owner of the rule).
    public float PhysicalFallTime
    {
        get { return physicalFallTime; }
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
        // WALK CYCLE 51 meter (see the fields): the hand's world pitch this frame.
        if (leftHandCollider != null)
        {
            float w = Mathf.DeltaAngle(0f, leftHandCollider.transform.eulerAngles.z);
            handFlatErrorMinL = Mathf.Min(handFlatErrorMinL, FlatErrorDeg(w));
            if (!handGroundLatchedL && leftHandGrounded)
            {
                handGroundLatchedL = true;
                handWorldAtGroundL = w;
                wristAngleAtGroundL = armDrive != null ? armDrive.PressWristAngleL : 0f;
            }
        }
        if (rightHandCollider != null)
        {
            float w = Mathf.DeltaAngle(0f, rightHandCollider.transform.eulerAngles.z);
            handFlatErrorMinR = Mathf.Min(handFlatErrorMinR, FlatErrorDeg(w));
            if (!handGroundLatchedR && rightHandGrounded)
            {
                handGroundLatchedR = true;
                handWorldAtGroundR = w;
                wristAngleAtGroundR = armDrive != null ? armDrive.PressWristAngleR : 0f;
            }
        }

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

        // ─── PHYSICAL FALL (C1: the one project-wide criterion) ───
        // Tilt / root height only, never the controller state: the automaton can
        // park at Recovery while the body lies on the dirt (p_sw1). The settled
        // stand height is measured over the quiet window and then frozen.
        if (time >= baselineWindowStart && time < baselineWindowEnd)
            standBaseline.Feed(pelvisY, tilt);
        else if (time >= baselineWindowEnd)
            standBaseline.Freeze();
        float fallBaselineHeight = standBaseline.valid
            ? standBaseline.height
            : float.NegativeInfinity;
        // No measured stand height (a trial shorter than the window, or a body
        // already down in it) disables the DROP arm - the criterion is then the
        // tilt alone. Feeding it the spawn pose is exactly what made the arena's
        // copy latch 2112 of 2112 rows, so it is not used as a bar.
        downNow = FallCriterion.Step(ref fallLatch, fallBaselineHeight, pelvisY, tilt,
            Time.fixedDeltaTime, time);
        if (downNow)
        {
            samplesDown++;
            // Real palm contact while the body is down: the press needs it, and
            // "the palm is at the slab" was already being logged as grounded.
            samplesDownArm++;
            if (handContactL) samplesDownHandContactL++;
            if (handContactR) samplesDownHandContactR++;
            // The run's FIRST frame of the sustained run, not the end of the
            // settle: a single-frame crossing mid-dive (p_arms4: 6.115 s, then
            // |tilt| 45.2 -> 26.6 deg with the pelvis still dropping) never
            // reaches the sustain and the latch lands on the first real run.
            if (physicalFallTime < 0f && fallLatch.fired)
                physicalFallTime = fallLatch.fireTime;
        }

        // ─── LOAD METER (C1: per foot, per frame) ───
        SampleSoleLoad(time, comPosition, pelvisY, walkSwap);

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
        // ─── A2/A3/A4 (append-only, 19.09.2026) ───
        // Runs here, not earlier: by now the load meter (fzL/fzR/hullMin/hullMax/
        // wSup/comVelX/supportH) and the per-joint command record (legReqOut,
        // i.e. the req* columns) both hold THIS frame's values, so the workspace
        // residual compares against the command that actually reached the PD on
        // this step rather than the previous one.
        SampleGeometricState(time, comPosition, pelvisY);

        // ─── WALK CYCLE 6j (append-only): the DELIVERED plant geometry ───
        // Read once per row from the walker's own instruments. 0 is its
        // walker-off sentinel; a non-finite value is written as 0 so no row
        // can carry a NaN into a consumer.
        float j6SwingAskX = FiniteOrZero(balance != null ? balance.captureSwingAskX : 0f);
        float j6SwingSoleX = FiniteOrZero(balance != null ? balance.captureSwingSoleX : 0f);
        float j6SwingTrackAge = FiniteOrZero(balance != null ? balance.captureSwingTrackAge : 0f);
        float j6PlanTargetX = FiniteOrZero(balance != null ? balance.capturePlanTargetX : 0f);
        // ─── WALK CYCLE 6k (append-only): the solver's OWN answer ───
        // Same read-once, FiniteOrZero discipline as the 6j block above; the
        // walker's own 0 sentinel passes through unchanged.
        float j6kSolvedSoleX = FiniteOrZero(balance != null ? balance.captureSwingSolvedSoleX : 0f);
        float j6kSolvedResidualX =
            FiniteOrZero(balance != null ? balance.captureSwingSolvedResidualX : 0f);
        float j6kSolveFrameX = FiniteOrZero(balance != null ? balance.captureSwingSolveFrameX : 0f);
        float j6kSolveFrameAngleDeg =
            FiniteOrZero(balance != null ? balance.captureSwingSolveFrameAngleDeg : 0f);
        // ─── WALK CYCLE 6l (append-only): the pose that was APPLIED ───
        // Same read-once, FiniteOrZero discipline as the 6j and 6k blocks
        // above; the walker's own 0 sentinel passes through unchanged.
        float j6lAppliedSoleX =
            FiniteOrZero(balance != null ? balance.captureSwingAppliedSoleX : 0f);
        float j6lAppliedResidualX =
            FiniteOrZero(balance != null ? balance.captureSwingAppliedResidualX : 0f);
        float j6lAppliedFrameX =
            FiniteOrZero(balance != null ? balance.captureSwingAppliedFrameX : 0f);
        float j6lAppliedFrameAngleDeg =
            FiniteOrZero(balance != null ? balance.captureSwingAppliedFrameAngleDeg : 0f);
        // ─── WALK CYCLE 6n (append-only): the ask's own Y and its pre-bound
        // flexion demand. Same read-once, FiniteOrZero discipline as the 6j/6k/
        // 6l blocks above; the walker's own 0 sentinel passes through unchanged.
        float j6nSwingAskY = FiniteOrZero(balance != null ? balance.captureSwingAskY : 0f);
        float j6nHipDemandDeg =
            FiniteOrZero(balance != null ? balance.captureSwingHipDemandDeg : 0f);
        // ─── WALK CYCLE 6o (append-only): the frame the plan target is built
        // in. Same read-once, FiniteOrZero discipline as the 6j/6k/6l/6n blocks
        // above; the walker's own 0 sentinel passes through unchanged.
        float j6oPlanFrameTiltDeg =
            FiniteOrZero(balance != null ? balance.capturePlanFrameTiltDeg : 0f);
        // ─── WALK CYCLE 6p (append-only): the bounded posture answer the
        // delivered swing-hip command carried. Same read-once, FiniteOrZero
        // discipline as the 6j..6o blocks above; the walker's own 0 sentinel
        // passes through unchanged.
        float j6pPostureBiasDeg =
            FiniteOrZero(balance != null ? balance.captureSwingPostureBiasDeg : 0f);
        // ─── WALK CYCLE 6q (append-only): WHICH PATH RAN. Read once per row like
        // the 6j..6p blocks above; the 0 the level 1 path publishes passes
        // through unchanged, so the sentinel never becomes a NaN and never a
        // second meaning.
        float j6qWalkPath = FiniteOrZero(balance != null ? balance.captureWalkPath : 0f);
        // ─── WALK CYCLE 6s (append-only): THE POSE GAIT'S OWN ASKS ───
        // 6q published WHICH path wrote the leg joints; nothing published what
        // the LEVEL 2 path ASKED FOR, so the 6.4-7.0 s reversal of c6r2_walk
        // (comX 0.508 at 6.4 s, 0.497 at 7.0, comVelX -0.599, fell 7.255 s) could
        // only be read through the library's numbers AFTER the PD had them
        // (hip*/knee*/ankle*) and the torques it applied (appTorque*). Read once
        // per row like the 6j..6q blocks above, same FiniteOrZero discipline; the
        // pose drive's own 0 sentinel (the pose gait is not driving the WALK on
        // this step: the switch is off, the gait is standing, or its own down test
        // relaxed the body) passes through unchanged, never NaN.
        float j6sPoseHipLDeg = FiniteOrZero(pose != null ? pose.capturePoseTargetHipLDeg : 0f);
        float j6sPoseKneeLDeg = FiniteOrZero(pose != null ? pose.capturePoseTargetKneeLDeg : 0f);
        float j6sPoseAnkleLDeg = FiniteOrZero(pose != null ? pose.capturePoseTargetAnkleLDeg : 0f);
        float j6sPoseHipRDeg = FiniteOrZero(pose != null ? pose.capturePoseTargetHipRDeg : 0f);
        float j6sPoseKneeRDeg = FiniteOrZero(pose != null ? pose.capturePoseTargetKneeRDeg : 0f);
        float j6sPoseAnkleRDeg = FiniteOrZero(pose != null ? pose.capturePoseTargetAnkleRDeg : 0f);
        float j6sPoseHipHeightRequestM =
            FiniteOrZero(pose != null ? pose.capturePoseHipHeightRequestM : 0f);
        float j6sPoseHipHeightLiveM =
            FiniteOrZero(pose != null ? pose.capturePoseHipHeightLiveM : 0f);
        float j6sPosePelvisTiltTargetDeg =
            FiniteOrZero(pose != null ? pose.capturePosePelvisTiltTargetDeg : 0f);
        float j6sPoseStepIndex = FiniteOrZero(pose != null ? pose.capturePoseStepIndex : 0f);
        float j6sPoseMaySwap = FiniteOrZero(pose != null ? pose.capturePoseMaySwap : 0f);
        float j6sPoseClockDone = FiniteOrZero(pose != null ? pose.capturePoseClockDone : 0f);
        float j6sPoseLedOut = FiniteOrZero(pose != null ? pose.capturePoseLedOut : 0f);
        float j6sPoseSwapReason = FiniteOrZero(pose != null ? pose.capturePoseSwapReason : 0f);
        float j6sPoseDriveState = FiniteOrZero(pose != null ? pose.capturePoseDriveState : 0f);
        // ─── WALK CYCLE 6x (append-only): THE POSE PATH'S OWN PLACEMENT ───
        // 6w's row failed with its brake at the swaps and no column that named the
        // owner of the placement (the level 1 capture-solver columns are 0 on every
        // walk row while captureWalkPath = 1). Read once per row like the 6j..6s
        // blocks above, same FiniteOrZero discipline; the pose drive's own 0
        // sentinel (the pose gait is not driving the WALK on this step) passes
        // through unchanged, never NaN. See the header block above for the units,
        // the plant signal and the sign of the lead.
        float j6xPosePlaceXM = FiniteOrZero(pose != null ? pose.capturePosePlaceXM : 0f);
        float j6xPoseSwingSoleXM = FiniteOrZero(pose != null ? pose.capturePoseSwingSoleXM : 0f);
        float j6xPoseStanceSoleXM = FiniteOrZero(pose != null ? pose.capturePoseStanceSoleXM : 0f);
        float j6xPosePlaceLeadM = FiniteOrZero(pose != null ? pose.capturePosePlaceLeadM : 0f);
        // ─── WALK CYCLE 6y (append-only): THE REACH GUARD'S OWN NUMBERS ───
        // The ceiling the guard applied and the hip anchor x it measured from, read
        // once per row like the blocks above, same FiniteOrZero discipline: the pose
        // drive's 0 sentinel (not driving the WALK on this step) passes through
        // unchanged and the guard's own inert sentinel (-1) passes through as the
        // finite number it is. See the header block above for the units and the
        // recoverable anchor height.
        float j6yPoseReachXM = FiniteOrZero(pose != null ? pose.capturePoseReachXM : 0f);
        float j6yPoseHipXM = FiniteOrZero(pose != null ? pose.capturePoseHipXM : 0f);
        // ─── WALK CYCLE 6z (append-only): THE HORIZONTAL CHANNEL AND THE
        // RESOLVER ───
        // Read once per row like the blocks above, same FiniteOrZero discipline.
        // The force carries the pose drive's own no-measurement sentinel when the
        // component is absent from this rig (-1000000, a value no physical
        // horizontal force reaches), so a run that never measured the channel is
        // distinguishable from a run that measured zero load; the resolver's 0 is
        // the pose drive's own "this row is not driving the walk" sentinel; the
        // acceleration is a measurement and has no sentinel. See the header block
        // above for the source, the units and the sign check.
        float j6zPoseGroundFxN = FiniteOrZero(pose != null
            ? pose.capturePoseGroundFxN
            : PoseDriveController.PoseGroundFxNoMeasureN);
        float j6zPoseComAccelX = FiniteOrZero(pose != null ? pose.capturePoseComAccelXMps2 : 0f);
        float j6zPoseResolvedTerm = FiniteOrZero(pose != null
            ? pose.capturePoseResolvedTerm
            : PoseDriveController.PosePlaceTermNotDriving);
        // ─── WALK CYCLE 126 (append-only): THE ANKLE LOOP'S OWN NUMBERS ───
        // Read once per row like the blocks above, same FiniteOrZero discipline;
        // the fields persist the loop's last-computed values on steps its path
        // is not driving (see the header block for the units).
        float j126AnkComX = FiniteOrZero(pose != null ? pose.ankLoopComX : 0f);
        float j126AnkRefX = FiniteOrZero(pose != null ? pose.ankLoopRefX : 0f);
        float j126AnkOffN = FiniteOrZero(pose != null ? pose.ankLoopOffN : 0f);
        float j126AnkVelN = FiniteOrZero(pose != null ? pose.ankLoopVelN : 0f);
        float j126AnkP = FiniteOrZero(pose != null ? pose.ankLoopP : 0f);
        float j126AnkD = FiniteOrZero(pose != null ? pose.ankLoopD : 0f);
        float j126AnkRaw = FiniteOrZero(pose != null ? pose.ankLoopRaw : 0f);
        float j126AnkCmd = FiniteOrZero(pose != null ? pose.ankLoopCmd : 0f);
        float j126AnkSigL = FiniteOrZero(pose != null ? pose.ankLoopSigL : 0f);
        float j126AnkSigR = FiniteOrZero(pose != null ? pose.ankLoopSigR : 0f);
        float j126AnkLd = FiniteOrZero(pose != null ? pose.ankLoopLd : 0f);
        float j126AnkRd = FiniteOrZero(pose != null ? pose.ankLoopRd : 0f);
        // WALK CYCLE 127 (append-only): the crouch's load-shaped level (the
        // crouch gate's own input; 0 on any frame that never crouches).
        float j127AnkCLL = FiniteOrZero(pose != null ? pose.ankLoopCrouchLoad : 0f);

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
            .Append(F(armDrive != null && armDrive.PressPhaseL != 0
                ? armDrive.PressPlantForeL : 0f)).Append(';')
            .Append(F(armDrive != null && armDrive.PressPhaseR != 0
                ? armDrive.PressPlantForeR : 0f)).Append(';')
            .Append(F(armDrive != null ? armDrive.GetUpReachCmd : 0f)).Append(';')
            // CYCLE 110 — the grip row (same order as the header block above).
            .Append(pose != null ? pose.CaptureGripEnabled : 0).Append(';')
            .Append(pose != null ? pose.CaptureGripStage : -1).Append(';')
            .Append(pose != null ? pose.CaptureGripWants(0) : 0).Append(';')
            .Append(pose != null ? pose.CaptureGripWants(1) : 0).Append(';')
            .Append(pose != null ? pose.CaptureGripHeld(0) : 0).Append(';')
            .Append(pose != null ? pose.CaptureGripHeld(1) : 0).Append(';')
            .Append(F(pose != null ? pose.CaptureGripFn(0) : 0f)).Append(';')
            .Append(F(pose != null ? pose.CaptureGripFn(1) : 0f)).Append(';')
            .Append(F(pose != null ? pose.CaptureGripLoad(0) : 0f)).Append(';')
            .Append(F(pose != null ? pose.CaptureGripLoad(1) : 0f)).Append(';')
            .Append(F(pose != null ? pose.CaptureGripSlip(0) : 0f)).Append(';')
            .Append(F(pose != null ? pose.CaptureGripSlip(1) : 0f)).Append(';')
            .Append(pose != null ? pose.CaptureGripEngages(0) : 0).Append(';')
            .Append(pose != null ? pose.CaptureGripEngages(1) : 0).Append(';')
            .Append(pose != null ? pose.CaptureGripTears(0) : 0).Append(';')
            .Append(pose != null ? pose.CaptureGripTears(1) : 0).Append(';')
            .Append(F(pose != null ? pose.CaptureGripMuMat : 0f)).Append(';')
            .Append(F(pose != null ? pose.CaptureGripMuPair : 0f)).Append(';')
            .Append(F(pose != null ? pose.CaptureGripPalmX(0) : 0f)).Append(';')
            .Append(F(pose != null ? pose.CaptureGripPalmY(0) : 0f)).Append(';')
            .Append(F(pose != null ? pose.CaptureGripPalmX(1) : 0f)).Append(';')
            .Append(F(pose != null ? pose.CaptureGripPalmY(1) : 0f)).Append(';')
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
            .Append(F(armDrive != null ? armDrive.PressAimVoteL : 0f)).Append(';')
            .Append(F(armDrive != null ? armDrive.PressAimVoteR : 0f)).Append(';')
            .Append(F(armDrive != null ? armDrive.PressAimBranchL : 0f)).Append(';')
            .Append(F(armDrive != null ? armDrive.PressAimBranchR : 0f)).Append(';')
            // Matched press window (arms cycle 8). Per frame, on every run, with
            // the press on and off.
            .Append(F(armDrive != null ? armDrive.PressWindowSeconds : 0f)).Append(';')
            .Append(F(armDrive != null ? armDrive.PressWindowShoulderRiseL : 0f)).Append(';')
            .Append(F(armDrive != null ? armDrive.PressWindowShoulderRiseR : 0f)).Append(';')
            .Append(F(armDrive != null ? armDrive.PressWindowPelvisRise : 0f)).Append(';')
            .Append(F(armDrive != null ? armDrive.PressWindowTravelX : 0f)).Append(';')
            .Append(F(armDrive != null ? armDrive.PressWindowContactFractionL : 0f)).Append(';')
            .Append(F(armDrive != null ? armDrive.PressWindowContactFractionR : 0f)).Append(';')
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
            // C2a: footLoaded* is the MEASURED load flag (fz* > footLoadFlagNewtons,
            // see SampleSoleLoad), live on every row. It used to carry the walker
            // tap's loaded bit, which read 0.00 through every stand window and
            // then froze at the last tick. footTouch*/footSupport* below are still
            // the walker's own tap (tapFresh says whether they are fresh).
            .Append(footLoadFlagL).Append(';')
            .Append(footLoadFlagR).Append(';')
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
            .Append(F(legDemandOut[5])).Append(';')
            // C1 load meter - same order as the header block above.
            .Append(F(fzL)).Append(';')
            .Append(F(fzR)).Append(';')
            .Append(F(fzL + fzR)).Append(';')
            .Append(F(wSup)).Append(';')
            .Append(F(fzOverWsup)).Append(';')
            .Append(F(xCopMeas)).Append(';')
            .Append(F(hullMinX)).Append(';')
            .Append(F(hullMaxX)).Append(';')
            .Append(F(lambdaMeas)).Append(';')
            .Append(F(dcmNow)).Append(';')
            .Append(F(supportHeight)).Append(';')
            .Append(F(comVelX)).Append(';')
            .Append(ikSaturated).Append(';')
            .Append(copValid).Append(';')
            .Append(copRejected).Append(';')
            // A2/A3/A4 - same order as the header block above.
            .Append(F(pelvisVelY)).Append(';')
            .Append(F(pelvisAccY)).Append(';')
            .Append(ctrlFallState).Append(';')
            .Append(hullFallState).Append(';')
            .Append(F(ikReach[0])).Append(';')
            .Append(F(ikReach[1])).Append(';')
            .Append(F(ikNear[0])).Append(';')
            .Append(F(ikNear[1])).Append(';')
            .Append(F(ikResidKnee[0])).Append(';')
            .Append(F(ikResidKnee[1])).Append(';')
            .Append(ikGuardDy[0]).Append(';')
            .Append(ikGuardDy[1]).Append(';')
            .Append(ikGuardLen[0]).Append(';')
            .Append(ikGuardLen[1]).Append(';')
            // WALK CYCLE 6j — the delivered plant geometry, same order as the
            // header block above (ask X, sole X, track age, plan target X).
            .Append(F(j6SwingAskX)).Append(';')
            .Append(F(j6SwingSoleX)).Append(';')
            .Append(F(j6SwingTrackAge)).Append(';')
            .Append(F(j6PlanTargetX)).Append(';')
            // WALK CYCLE 6k — the solver's own answer, same order as the header
            // block above (solved sole X, residual, solve frame X, frame angle).
            .Append(F(j6kSolvedSoleX)).Append(';')
            .Append(F(j6kSolvedResidualX)).Append(';')
            .Append(F(j6kSolveFrameX)).Append(';')
            .Append(F(j6kSolveFrameAngleDeg)).Append(';')
            // WALK CYCLE 6l — the pose that was APPLIED, same order as the
            // header block above (applied sole X, applied residual, applied
            // frame X, applied frame angle).
            .Append(F(j6lAppliedSoleX)).Append(';')
            .Append(F(j6lAppliedResidualX)).Append(';')
            .Append(F(j6lAppliedFrameX)).Append(';')
            .Append(F(j6lAppliedFrameAngleDeg)).Append(';')
            // WALK CYCLE 6n — the ask's own Y and its pre-bound flexion demand,
            // same order as the header block above.
            .Append(F(j6nSwingAskY)).Append(';')
            .Append(F(j6nHipDemandDeg)).Append(';')
            // WALK CYCLE 6o — the frame the plan target is built in, appended at
            // the end so every column before it keeps its index.
            .Append(F(j6oPlanFrameTiltDeg)).Append(';')
            // WALK CYCLE 6p — the bounded posture answer on the delivered command,
            // appended LAST so every column before it keeps its index.
            .Append(F(j6pPostureBiasDeg)).Append(';')
            // WALK CYCLE 6q — which path held the joints, appended LAST so every
            // column before it keeps its index (0 = level 1, 1 = the pose library).
            .Append(F(j6qWalkPath)).Append(';')
            // WALK CYCLE 6s — the pose gait's own asks, appended LAST in the same
            // order as the header block above so every column before them keeps
            // its index: the six joint requests, the hip-height request and live
            // height, the pelvis pitch target, the step clock's swap decision and
            // whether the pose gait is driving at all.
            .Append(F(j6sPoseHipLDeg)).Append(';')
            .Append(F(j6sPoseKneeLDeg)).Append(';')
            .Append(F(j6sPoseAnkleLDeg)).Append(';')
            .Append(F(j6sPoseHipRDeg)).Append(';')
            .Append(F(j6sPoseKneeRDeg)).Append(';')
            .Append(F(j6sPoseAnkleRDeg)).Append(';')
            .Append(F(j6sPoseHipHeightRequestM)).Append(';')
            .Append(F(j6sPoseHipHeightLiveM)).Append(';')
            .Append(F(j6sPosePelvisTiltTargetDeg)).Append(';')
            .Append(F(j6sPoseStepIndex)).Append(';')
            .Append(F(j6sPoseMaySwap)).Append(';')
            .Append(F(j6sPoseClockDone)).Append(';')
            .Append(F(j6sPoseLedOut)).Append(';')
            .Append(F(j6sPoseSwapReason)).Append(';')
            .Append(F(j6sPoseDriveState)).Append(';')
            // WALK CYCLE 6x — the pose path's own placement, appended LAST in the
            // same order as the header block above so every column before them keeps
            // its index: the ask (the resolved pivot), the swing and the stance
            // sole, and the plant-frame lead (the brake distance).
            .Append(F(j6xPosePlaceXM)).Append(';')
            .Append(F(j6xPoseSwingSoleXM)).Append(';')
            .Append(F(j6xPoseStanceSoleXM)).Append(';')
            .Append(F(j6xPosePlaceLeadM)).Append(';')
            // WALK CYCLE 6y — the reach guard's own numbers, appended LAST again in
            // the same order as their header block, so nothing before them moves
            // (the walk CSV goes 221 -> 223 columns): the ceiling the guard applied
            // (positive, or -1 when the guard was inert) and the hip anchor x it
            // measured from.
            .Append(F(j6yPoseReachXM)).Append(';')
            .Append(F(j6yPoseHipXM)).Append(';')
            // WALK CYCLE 6z — the horizontal channel and the pivot's resolver,
            // appended LAST again in the same order as their header block, so
            // nothing before them moves (the walk CSV goes 223 -> 226 columns):
            // the measured horizontal ground force (N), the CoM's own horizontal
            // acceleration (m/s^2) and the integer code of the term that resolved
            // the pivot on this row.
            .Append(F(j6zPoseGroundFxN)).Append(';')
            .Append(F(j6zPoseComAccelX)).Append(';')
            .Append(F(j6zPoseResolvedTerm)).Append(';')
            // WALK CYCLE 67 — the reach > span pair, appended LAST again in the same
            // order as their header block (the walk CSV goes 226 -> 229 columns): the
            // live built leg span per side (m) and the 0/1 witness that either leg's
            // actual chord exceeds its own span on this row. Both finite by
            // construction - legSpan carries the block's 0 sentinel when no body or
            // no estimator is available, never NaN.
            .Append(F(legSpan[0])).Append(';')
            .Append(F(legSpan[1])).Append(';')
            .Append(ikOverSpan[0] == 1 || ikOverSpan[1] == 1 ? 1 : 0).Append(';')
            // WALK CYCLE 68 — the sole's own slip at stance, appended LAST (the
            // walk CSV goes 229 -> 230 columns): the loaded foot's own centre
            // drift, m/s (see the field block's note). Finite by construction.
            .Append(F(soleSlipAtStance)).Append(';')
            // PUNCH p12 — the support polygon's own edge and its attribution, appended
            // LAST (the walk CSV goes 230 -> 235 columns): the estimator's polygon edges
            // (m), the sole plate carrying each edge (or -1) and the grounded mask.
            .Append(F(supportPolyMinX)).Append(';')
            .Append(F(supportPolyMaxX)).Append(';')
            .Append(supportMinId).Append(';')
            .Append(supportMaxId).Append(';')
            .Append(supportGroundedMask).Append(';')
            // PUNCH p16 — the toe/MTP joint's own channels, appended LAST in the same
            // order as their header block (the walk CSV goes 235 -> 241 columns).
            .Append(F(toeAngleL)).Append(';')
            .Append(F(toeAngleR)).Append(';')
            .Append(F(actToeL)).Append(';')
            .Append(F(actToeR)).Append(';')
            .Append(F(appTorqueToeL)).Append(';')
            .Append(F(appTorqueToeR)).Append(';')
            // WALK CYCLE 126 — the ankle loop's own numbers, appended LAST in the
            // same order as their header block so every column before them keeps
            // its index: comX, refX, offsetN, velN, the P and D terms and the raw
            // sum, the clamped command, the applied signal per side and the two
            // ground flags the loop saw.
            .Append(F(j126AnkComX)).Append(';')
            .Append(F(j126AnkRefX)).Append(';')
            .Append(F(j126AnkOffN)).Append(';')
            .Append(F(j126AnkVelN)).Append(';')
            .Append(F(j126AnkP)).Append(';')
            .Append(F(j126AnkD)).Append(';')
            .Append(F(j126AnkRaw)).Append(';')
            .Append(F(j126AnkCmd)).Append(';')
            .Append(F(j126AnkSigL)).Append(';')
            .Append(F(j126AnkSigR)).Append(';')
            .Append(F(j126AnkLd)).Append(';')
            .Append(F(j126AnkRd)).Append(';')
            .Append(F(j127AnkCLL)).AppendLine();
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
        json.Append(",\"executedSteps\":").Append(executedSteps.ToString(CultureInfo.InvariantCulture));
        AppendNum(json, "fixedDeltaTime", Time.fixedDeltaTime);
        AppendNum(json, "muscleActivationSpeed", balance != null ? balance.muscleActivationSpeed : 0f);
        AppendNum(json, "survivedSeconds", survived);
        json.Append(",\"fell\":").Append(physicalFallTime >= 0f ? "true" : "false");
        // Where the flag came from. fallTime is the old automaton latch, kept
        // for the cross-check: it disagreed with the body in 19 runs on disk.
        AppendNum(json, "fallTime", physicalFallTime);
        AppendNum(json, "fallAutomatonTime", fallTime);
        AppendNum(json, "downSeconds", samplesDown * Time.fixedDeltaTime);
        // C1: the criterion's own inputs, so the fall time is never quoted
        // without the baseline it was measured against. The rule itself lives in
        // Assets/FallCriterion.cs (0.25 m below the settled stand, or 60 deg
        // tilt, held 0.15 s).
        AppendNum(json, "fallBaselineHeight", standBaseline.Value(startPelvisY));
        json.Append(",\"fallBaselineValid\":").Append(standBaseline.valid ? "true" : "false");
        // fallBaselineValid false means the drop arm never fired: the spawn pose
        // reported above is for scale only, never used as a bar.
        json.Append(",\"fallBaselineFrozen\":").Append(standBaseline.frozen ? "true" : "false");
        AppendNum(json, "fallCriterionDropMeters", FallCriterion.DropMeters);
        AppendNum(json, "fallCriterionTiltDegrees", FallCriterion.TiltDegrees);
        AppendNum(json, "fallCriterionSustainSeconds", FallCriterion.SustainSeconds);
        AppendNum(json, "wSupNewtons", wSup);
        // How many frames the measured weight was averaged over, so the window is
        // never a mystery. (The last frame of the window prints rounded to the
        // window end - 5.0000 s for a 4.0-5.0 s window - because the player's
        // time there is 4.99995 s, and it is inside the window.)
        AppendNum(json, "wSupQuietFrames", quietFzCount);
        AppendNum(json, "ikSaturatedFraction", FractionSaturatedIk());
        // ─── A2 geometric fall state + A3 IK workspace residual (19.09.2026) ───
        // The state the controller never had: BalanceState stayed 0/1 in every
        // measured run. ctrlFallStateFirstTime is the acceptance number - on
        // walk1_load4 it must be at or before the FallCriterion time (5.865 s).
        // hull* is the same ladder against the recorder's measured loaded-plate
        // hull, so the switch is checkable from the CSV columns themselves.
        AppendNum(json, "ctrlFallStateFirstTime", ctrlFallFirstTime);
        AppendNum(json, "ctrlFallStateFirstCode", ctrlFallFirstCode);
        AppendNum(json, "ctrlFallenFirstTime", ctrlFallenFirstTime);
        AppendNum(json, "ctrlCriticalFraction", Fraction(samplesCtrlCritical));
        AppendNum(json, "ctrlFallenFraction", Fraction(samplesCtrlFallen));
        AppendNum(json, "hullFallStateFirstTime", hullFallFirstTime);
        AppendNum(json, "hullFallenFirstTime", hullFallenFirstTime);
        AppendNum(json, "hullCriticalFraction", Fraction(samplesHullCritical));
        AppendNum(json, "hullFallenFraction", Fraction(samplesHullFallen));
        AppendNum(json, "ikReachFinalL", FiniteOrZero(ikReach[0]));
        AppendNum(json, "ikReachFinalR", FiniteOrZero(ikReach[1]));
        // WALK CYCLE 67 — the reach > span pair (see the CSV block). legSpanFinal*
        // is the live built span at the last sample; ikReachOverSpanMaxM is the
        // row's largest signed exceedance of ikReach over legSpan (positive = the
        // leg was asked past its own geometry by that many metres, negative = the
        // largest headroom; -1 = never measured, the one sentinel); the two
        // fractions are the watch's own shares, per side, of frames over span.
        AppendNum(json, "legSpanFinalL", legSpan[0]);
        AppendNum(json, "legSpanFinalR", legSpan[1]);
        AppendNum(json, "ikReachOverSpanMaxM",
            IsFinite(maxIkReachOverSpan) ? maxIkReachOverSpan : -1f);
        AppendNum(json, "ikOverSpanFractionL",
            samples > 0 ? (float)samplesIkOverSpan[0] / samples : 0f);
        AppendNum(json, "ikOverSpanFractionR",
            samples > 0 ? (float)samplesIkOverSpan[1] / samples : 0f);
        AppendNum(json, "ikResidKneeMeanAbsL",
            samples > 0 ? (float)(sumIkResidAbs[0] / samples) : 0f);
        AppendNum(json, "ikResidKneeMeanAbsR",
            samples > 0 ? (float)(sumIkResidAbs[1] / samples) : 0f);
        AppendNum(json, "ikResidKneeMaxAbsL", FiniteOrZero(maxIkResidAbs[0]));
        AppendNum(json, "ikResidKneeMaxAbsR", FiniteOrZero(maxIkResidAbs[1]));
        // The upright floor: mean |residual| in the same quiet window wSup is
        // taken from (the second before the walk opens; 4.0-5.0 s when the trial
        // does not walk). Quote this beside any residual so the floor is never
        // read as a violation.
        AppendNum(json, "ikResidKneeQuietMeanAbsL",
            samplesIkResidQuiet[0] > 0
                ? (float)(sumIkResidQuietAbs[0] / samplesIkResidQuiet[0]) : 0f);
        AppendNum(json, "ikResidKneeQuietMeanAbsR",
            samplesIkResidQuiet[1] > 0
                ? (float)(sumIkResidQuietAbs[1] / samplesIkResidQuiet[1]) : 0f);
        AppendNum(json, "ikGuardDyFractionL", Fraction(samplesIkGuardDy[0]));
        AppendNum(json, "ikGuardDyFractionR", Fraction(samplesIkGuardDy[1]));
        AppendNum(json, "ikGuardLenFractionL", Fraction(samplesIkGuardLen[0]));
        AppendNum(json, "ikGuardLenFractionR", Fraction(samplesIkGuardLen[1]));
        // A4: the squat run's own channel - fzTotal sits beside these in the CSV.
        AppendNum(json, "maxAbsPelvisAccY", FiniteOrZero(maxAbsPelvisAccY));
        AppendNum(json, "finalPelvisVelY", FiniteOrZero(pelvisVelY));
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
        // SQUAT TASK (cycle 84): the task machine's own counters, straight off the
        // controller. All 0 on every path that never arms a squat, so a row's summary
        // says by itself whether a task ran and how many reps came back clean
        // (squatRepOkMask bit i = rep i+1 held every bound on every frame).
        AppendNum(json, "squatState", balance != null ? balance.SquatStateCode : 0);
        AppendNum(json, "squatRepsRequested", balance != null ? balance.SquatRepsRequested : 0);
        AppendNum(json, "squatRepsDone", balance != null ? balance.SquatRepsDone : 0);
        AppendNum(json, "squatRepOkMask", balance != null ? balance.SquatRepOkMask : 0);
        AppendNum(json, "squatAborted", balance != null && balance.SquatAborted ? 1f : 0f);
        AppendNum(json, "squatGateMask", balance != null ? balance.SquatGateMask : 0);
        AppendNum(json, "squatReason", balance != null ? balance.SquatReason : 0);
        AppendNum(json, "squatLastRepSeconds", balance != null ? balance.SquatLastRepSeconds : 0f);
        AppendNum(json, "squatBottomKneeDeg", balance != null ? balance.SquatBottomKneeDeg : 0f);
        AppendNum(json, "squatBottomComM", balance != null ? balance.SquatBottomComM : 0f);
        AppendNum(json, "squatBottomTiltDeg", balance != null ? balance.SquatBottomTiltDeg : 0f);
        AppendNum(json, "squatBottomLoad", balance != null ? balance.SquatBottomLoad : 0f);
        // Cycle 85: the load term, the dwell this take ran and the stall guard's own
        // counters. Evidence only - the flag never gates motion, and a take that arms
        // nothing still reads the whole block as 0.
        AppendNum(json, "squatBottomHoldUsed", balance != null ? balance.SquatBottomHoldSeconds : 0f);
        AppendNum(json, "squatPaceScale", balance != null ? balance.SquatPaceScale : 0f);
        AppendNum(json, "squatPaceFallback", balance != null && balance.SquatPaceFallbackFired ? 1f : 0f);
        AppendNum(json, "squatPaceStallSeconds", balance != null ? balance.SquatPaceStallSeconds : 0f);
        AppendNum(json, "squatPaceNoDriveFrames", balance != null ? balance.SquatPaceNoDriveFrames : 0f);
        // Cycle 86: the rise's own term (the return). The knob the take asked for, the
        // smallest scale the rise ran at, the seconds the load gate held it and whether
        // the stall guard had to take over.
        AppendNum(json, "squatReleaseKnob", balance != null ? balance.SquatReleaseKnob : 0f);
        AppendNum(json, "squatReleaseScaleMin", balance != null ? balance.SquatReleaseScaleMin : 0f);
        AppendNum(json, "squatReleaseFrozenSeconds", balance != null ? balance.SquatReleaseFrozenSeconds : 0f);
        AppendNum(json, "squatReleaseFallback", balance != null && balance.SquatReleaseFallbackFired ? 1f : 0f);
        // Cycle 85b: the sink bypass as the take ran it (seconds off the load gate).
        AppendNum(json, "squatReleaseSinkSeconds", balance != null ? balance.SquatReleaseSinkSeconds : 0f);
        // Cycle 87: the bottom's own term. The guard's held time as the bottom was left and
        // the seconds the gate held PAST the dwell's floor (cycle 85b's creep window).
        AppendNum(json, "squatBottomSettleAge", balance != null ? balance.SquatBottomSettleAge : 0f);
        AppendNum(json, "squatBottomSettleExtraSeconds",
            balance != null ? balance.SquatBottomSettleExtraSeconds : 0f);
        // Cycle 90: the rise's CoM admittance (fires, seconds held, sink-belt seconds) and
        // the arm raise (reps whose hold completed, the deepest CoM excursion while a raise
        // was live, the offset at the end of the last hold). All 0 on takes that arm none.
        AppendNum(json, "squatComHoldFires", balance != null ? balance.SquatComHoldFires : 0);
        AppendNum(json, "squatComHoldSeconds", balance != null ? balance.SquatComHoldSeconds : 0f);
        AppendNum(json, "squatComHoldSinkSeconds", balance != null ? balance.SquatComHoldSinkSeconds : 0f);
        AppendNum(json, "armRaiseRepsDone", balance != null ? balance.ArmRaiseRepsDone : 0);
        AppendNum(json, "armRaiseComMinM", balance != null ? balance.ArmRaiseComMinM : 0f);
        AppendNum(json, "armRaiseComHoldEndM", balance != null ? balance.ArmRaiseComHoldEndM : 0f);
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
        // WALK CYCLE 68 — the sole's own slip at stance (m/s), the eyes' "foot
        // slide" in numbers: mean over the frames any foot carried load, max over
        // the run, one bucket per walk step (the walkSwap series), and the frames
        // the mean was taken over.
        AppendNum(json, "soleSlipAtStanceMeanMps",
            soleSlipLoadedFrames > 0 ? soleSlipLoadedSum / soleSlipLoadedFrames : 0f);
        AppendNum(json, "soleSlipAtStanceMaxMps", soleSlipMax);
        AppendNum(json, "soleSlipAtStanceLoadedFrames", soleSlipLoadedFrames);
        json.Append(",\"soleSlipAtStanceStepMeansMps\":[");
        {
            int slipSteps = soleSlipStepMeanCount;
            for (int i = 0; i < slipSteps; i++)
            {
                if (i > 0) json.Append(',');
                json.Append(F(soleSlipStepMeans[i]));
            }
            if (soleSlipStepFrames > 0)
            {
                if (slipSteps > 0) json.Append(',');
                json.Append(F(soleSlipStepSum / soleSlipStepFrames));
            }
        }
        json.Append(']');
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
        // armPressStroke*: the fall press's own maxima over the WHOLE RUN since
        // arms cycle 10 (they used to be the last stroke's, so a trial ending
        // mid-stroke published 0.000 for a press that really extended). The
        // stroke in flight is not published; ArmDrive keeps it as
        // PressLastStroke* for a reader inside the engine.
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
        // WALK CYCLE 51 meter: the palm's own pitch at its first ground frame (90 =
        // parallel to the slab), the wrist's delivered angle there, and the run's
        // closest approach to flat. The three are read together: the first says
        // what arrived, the second what the joint was asked to hold, the third
        // whether the guard ever had the palm flat at all.
        AppendNum(json, "handWorldAtGroundL", handGroundLatchedL ? handWorldAtGroundL : 0f);
        AppendNum(json, "handWorldAtGroundR", handGroundLatchedR ? handWorldAtGroundR : 0f);
        AppendNum(json, "wristAngleAtGroundL", wristAngleAtGroundL);
        AppendNum(json, "wristAngleAtGroundR", wristAngleAtGroundR);
        AppendNum(json, "handFlatErrorMinL", handFlatErrorMinL > 1000f ? 0f : handFlatErrorMinL);
        AppendNum(json, "handFlatErrorMinR", handFlatErrorMinR > 1000f ? 0f : handFlatErrorMinR);
        // Shoulder rise inside a stroke, per side: with the palm latched on the
        // slab this is the push itself. The pelvis number above only follows it
        // if the legs hold, so on its own it cannot say whether the arms worked.
        AppendNum(json, "armPressShoulderLiftL", armDrive != null ? armDrive.PressShoulderLiftL : 0f);
        AppendNum(json, "armPressShoulderLiftR", armDrive != null ? armDrive.PressShoulderLiftR : 0f);
        // ─── The MATCHED press window (arms cycle 8) ───
        // The numbers above are stroke scoped and zero on the press-OFF control,
        // where no stroke ever starts, so that arm of any A/B is empty. These
        // open on the DOWN EDGE instead
        // and are sampled while the body is down, so the press-ON run and the
        // press-OFF run of one build report the same columns over the same
        // simulated seconds. armPressWindowRiseL/R is the shoulder-pivot rise,
        // armPressWindowTravelX the body's travel along the slab, and the two
        // fractions say how much of the window the palm was really on the
        // ground. Read them as a PAIR with armPressShoulderLift*, never alone.
        AppendNum(json, "armPressWindowSeconds", armDrive != null ? armDrive.PressWindowSeconds : 0f);
        AppendNum(json, "armPressWindowRiseL", armDrive != null ? armDrive.PressWindowShoulderRiseL : 0f);
        AppendNum(json, "armPressWindowRiseR", armDrive != null ? armDrive.PressWindowShoulderRiseR : 0f);
        AppendNum(json, "armPressWindowPelvisRise", armDrive != null ? armDrive.PressWindowPelvisRise : 0f);
        AppendNum(json, "armPressWindowTravelX", armDrive != null ? armDrive.PressWindowTravelX : 0f);
        AppendNum(json, "armPressWindowContactL", armDrive != null ? armDrive.PressWindowContactFractionL : 0f);
        AppendNum(json, "armPressWindowContactR", armDrive != null ? armDrive.PressWindowContactFractionR : 0f);
        AppendNum(json, "armPressPlantOffsetL", armDrive != null ? armDrive.PressPlantOffsetL : 0f);
        AppendNum(json, "armPressPlantOffsetR", armDrive != null ? armDrive.PressPlantOffsetR : 0f);
        // The elbow angle the plant asked for, degrees: the fold the stroke starts
        // from, so the extension it had available. A stroke that starts near 0 had
        // no room and could not be a press whatever the body did.
        AppendNum(json, "armPressPlantFoldL", armDrive != null ? armDrive.PressPlantFoldL : 0f);
        AppendNum(json, "armPressPlantFoldR", armDrive != null ? armDrive.PressPlantFoldR : 0f);
        // The press aim sign the drive used (+1, exact) against the old vote.
        // They disagreed on both sides in p_arms34 and the drive followed the
        // vote, so both are recorded: a regression shows up as a divergence.
        AppendNum(json, "armPressAimSignL", armDrive != null ? armDrive.PressAimSignL : 0f);
        AppendNum(json, "armPressAimSignR", armDrive != null ? armDrive.PressAimSignR : 0f);
        AppendNum(json, "armPressAimVoteL", armDrive != null ? armDrive.PressAimVoteL : 0f);
        AppendNum(json, "armPressAimVoteR", armDrive != null ? armDrive.PressAimVoteR : 0f);
        AppendNum(json, "armPressAimBranchL", armDrive != null ? armDrive.PressAimBranchL : -1f);
        AppendNum(json, "armPressAimBranchR", armDrive != null ? armDrive.PressAimBranchR : -1f);
        AppendNum(json, "armPressRequest", armDrive != null ? armDrive.PressRequest : 0f);
        AppendNum(json, "armGetUpSlapFires", armDrive != null ? armDrive.SlapFires : 0f);
        AppendNum(json, "armGetUpSlapEnds", armDrive != null ? armDrive.SlapEnds : 0f);
        AppendNum(json, "armGetUpSlapPeakDownMps", armDrive != null ? armDrive.SlapPeakDownMps : 0f);
        AppendNum(json, "armGetUpSlapBlocks", armDrive != null ? armDrive.SlapBlocks : 0f);
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
        // CYCLE 91: the stride stance actually applied (m, pelvis-referred ankles);
        // 0/0 = the rig's own geometry. The pair is the geometry evidence of the
        // -stanceStride sweep.
        AppendNum(json, "stanceStrideFront", h.stanceStrideFront);
        AppendNum(json, "stanceStrideBack", h.stanceStrideBack);
        // b7: the per-side standing shoulder bases APPLIED (deg; NaN follows
        // -shoulderBase, so the resolved value is written - geometry evidence of
        // the asymmetric arm pose takes).
        AppendNum(json, "shoulderBaseL", balance != null ? balance.ShoulderBaseFor(0) : 0f);
        AppendNum(json, "shoulderBaseR", balance != null ? balance.ShoulderBaseFor(1) : 0f);
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
        // WALK CYCLE 67 THE GUARD (HeadlessTrial): the runtime-ramp state this run
        // actually took. foreshortenRampAt = -1 with -fsRampAt passed means the
        // guard REFUSED the runtime channel (the value applied at build instead);
        // >= 0 means the channel is armed (only reachable with -fsRampUnsafe 1).
        // foreshortenRampUnsafe pins which of the two the caller asked for.
        AppendNum(json, "foreshortenRampAt", HeadlessTrial.ForeshortenRampAt);
        AppendNum(json, "foreshortenRampUnsafe", HeadlessTrial.ForeshortenRampUnsafe ? 1f : 0f);
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

    // WALK CYCLE 51 meter: how far the hand's world angle is from the slab's own
    // parallel (its long axis horizontal - the nearest of +90/-90 deg).
    private static float FlatErrorDeg(float world)
    {
        return Mathf.Min(Mathf.Abs(Mathf.DeltaAngle(world, 90f)),
                         Mathf.Abs(Mathf.DeltaAngle(world, -90f)));
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
    // ─── C1 LOAD METER: per foot, per frame ───
    // One fixed step of measured load. Every number comes from the solver's own
    // contact impulses on the four sole plates or from the CoM the rig already
    // publishes; there is no hardcoded mass and no geometric proxy. Recorder
    // only - no controller reads any of it.
    private void SampleSoleLoad(float time, Vector2 comPosition, float pelvisY, int walkSwap)
    {
        if (!soleCollidersResolved) ResolveSoleColliders();
        if (!groundFilterReady)
        {
            groundFilter = new ContactFilter2D();
            groundFilter.SetLayerMask(GroundLayers.Mask);
            groundFilter.useTriggers = false;
            groundFilterReady = true;
        }
        float dt = Time.fixedDeltaTime;
        if (dt <= 0f) dt = 0.005f;

        for (int side = 0; side < 2; side++)
        {
            soleFz[side] = 0f;
            solePlateMinX[side] = float.MaxValue;
            solePlateMaxX[side] = float.MinValue;
            solePlateMinY[side] = float.MaxValue;
            copPointCount[side] = 0;
        }

        // Heel and toe are two plates of one support: the load of the foot is
        // what the solver pushes through both of them.
        for (int i = 0; i < 4; i++)
        {
            Collider2D col = soleCollider[i];
            if (col == null) continue;
            int side = i < 2 ? 0 : 1;
            Bounds b = col.bounds;
            if (b.min.x < solePlateMinX[side]) solePlateMinX[side] = b.min.x;
            if (b.max.x > solePlateMaxX[side]) solePlateMaxX[side] = b.max.x;
            if (b.min.y < solePlateMinY[side]) solePlateMinY[side] = b.min.y;
            int n = col.GetContacts(groundFilter, contactBuffer);
            int count = Mathf.Min(n, contactBuffer.Length);
            for (int k = 0; k < count; k++)
            {
                ContactPoint2D cp = contactBuffer[k];
                // normalImpulse is the solver's impulse along the contact
                // normal. Only its UPWARD part is weight: a contact pressing
                // sideways against a lip is not support.
                float vertical = cp.normalImpulse * cp.normal.y;
                if (vertical <= 0f) continue;
                soleFz[side] += vertical / dt;
                int slot = side * CopPointCapacity + copPointCount[side];
                if (copPointCount[side] < CopPointCapacity)
                {
                    copPointX[slot] = cp.point.x;
                    copPointW[slot] = vertical;
                    copPointCount[side]++;
                }
            }
        }

        fzL = soleFz[0];
        fzR = soleFz[1];
        float total = fzL + fzR;

        // C2a load flag: live on every step, standing included, and never
        // frozen with the walker tap. Both feet are reported independently -
        // a sole that the solver carries no weight through is 0 N, which is a
        // reading, not a missing sample (measured: quiet stance rocks the load
        // between the soles at ~3 Hz, so neither flag is true in every frame).
        footLoadFlagL = fzL > footLoadFlagNewtons ? 1 : 0;
        footLoadFlagR = fzR > footLoadFlagNewtons ? 1 : 0;

        // ─── WALK CYCLE 68 — THE SOLE'S OWN SLIP AT STANCE (recorder only) ───
        // See the field block's note: the loaded plate pair's combined centre,
        // frame to frame, over dt. Nothing here feeds a physics column.
        float slipNow = 0f;
        for (int slipSide = 0; slipSide < 2; slipSide++)
        {
            bool plateValid = solePlateMinX[slipSide] <= solePlateMaxX[slipSide];
            float plateCx = plateValid
                ? (solePlateMinX[slipSide] + solePlateMaxX[slipSide]) * 0.5f : 0f;
            if (plateValid && soleSlipPrevValid[slipSide]
                && soleFz[slipSide] > footLoadFlagNewtons)
            {
                float slip = Mathf.Abs(plateCx - soleSlipPrevX[slipSide]) / dt;
                if (slip > slipNow) slipNow = slip;
            }
            soleSlipPrevValid[slipSide] = plateValid;
            if (plateValid) soleSlipPrevX[slipSide] = plateCx;
        }
        soleSlipAtStance = slipNow;

        bool slipLoaded = footLoadFlagL == 1 || footLoadFlagR == 1;
        if (slipLoaded)
        {
            soleSlipLoadedSum += slipNow;
            soleSlipLoadedFrames++;
        }
        if (slipNow > soleSlipMax) soleSlipMax = slipNow;

        // The walk's own step counter buckets the series: one bucket per step.
        if (walkSwap != soleSlipStepKey)
        {
            if (soleSlipStepKey != int.MinValue
                && soleSlipStepMeanCount < soleSlipStepMeans.Length)
            {
                soleSlipStepMeans[soleSlipStepMeanCount++] = soleSlipStepFrames > 0
                    ? soleSlipStepSum / soleSlipStepFrames : 0f;
            }
            soleSlipStepKey = walkSwap;
            soleSlipStepSum = 0f;
            soleSlipStepFrames = 0;
        }
        if (slipLoaded)
        {
            soleSlipStepSum += slipNow;
            soleSlipStepFrames++;
        }

        // Support hull = the plates that carry contact this frame. The CoP is the
        // impulse-weighted mean of the contact points on exactly these plates, so
        // "CoP inside the hull" holds by construction rather than almost always: a
        // plate in contact counts even at 1 N - it is still support - and the
        // earlier 5 %-of-total floor let a lightly loaded plate shift the CoP
        // outside a hull built without it (1 frame in 7045, 4 mm). In flight no
        // hull exists and it is reported as zero width at the CoM.
        bool useLeft = soleFz[0] > 0f;
        bool useRight = soleFz[1] > 0f;
        if (!useLeft && !useRight)
        {
            useLeft = true;
            useRight = true;
        }
        hullMinX = float.MaxValue;
        hullMaxX = float.MinValue;
        if (useLeft && solePlateMinX[0] < float.MaxValue)
        {
            hullMinX = solePlateMinX[0];
            hullMaxX = solePlateMaxX[0];
        }
        if (useRight && solePlateMinX[1] < float.MaxValue)
        {
            hullMinX = Mathf.Min(hullMinX, solePlateMinX[1]);
            hullMaxX = Mathf.Max(hullMaxX, solePlateMaxX[1]);
        }
        if (hullMinX > hullMaxX)
        {
            hullMinX = comPosition.x;
            hullMaxX = comPosition.x;
        }

        // ─── PUNCH p12 — THE SUPPORT POLYGON'S OWN EDGE, ATTRIBUTED ───
        // Same frame as the hull above, the estimator's own grounded-collider polygon:
        // its two edges, the mask of the plates the estimator calls grounded, and which
        // plate carries each edge. A read of the sensor's own rectangle, not of any
        // controller: nothing below (or anywhere) consumes these five fields.
        supportPolyMinX = state != null ? state.supportMinX : 0f;
        supportPolyMaxX = state != null ? state.supportMaxX : 0f;
        supportMinId = -1;
        supportMaxId = -1;
        supportGroundedMask = 0;
        if (state != null)
        {
            if (state.leftHeelGrounded) supportGroundedMask |= 1;
            if (state.leftToeGrounded) supportGroundedMask |= 2;
            if (state.rightHeelGrounded) supportGroundedMask |= 4;
            if (state.rightToeGrounded) supportGroundedMask |= 8;
            for (int i = 0; i < 4; i++)
            {
                if ((supportGroundedMask & (1 << i)) == 0) continue;
                Collider2D col = soleCollider[i];
                if (col == null) continue;
                Bounds cb = col.bounds;
                if (supportMinId < 0 && Mathf.Abs(cb.min.x - supportPolyMinX) <= 1e-5f) supportMinId = i;
                if (supportMaxId < 0 && Mathf.Abs(cb.max.x - supportPolyMaxX) <= 1e-5f) supportMaxId = i;
            }
        }

        // ─── PUNCH p16 — THE TOE/MTP JOINT'S OWN CHANNELS ───
        // Same frame as the support block above, recorder-only: the MTP angle the
        // estimator measured per side, the toe muscle pair's own activation and the
        // torque it applies - the demand's clamped witness and the torque actually
        // delivered, read from the same objects the ankle's own columns use (the
        // toe notes never reach the six-joint leg record: LegJointIndex returns -1
        // for the MTP, which is why p14 could not read the joint's demand). Nothing
        // consumes these six fields; the sentinel is 0 with no body/estimator.
        toeAngleL = state != null ? state.leftToeAngle : 0f;
        toeAngleR = state != null ? state.rightToeAngle : 0f;
        actToeL = MaxActivation(balance?.leftToeFlexor, balance?.leftToeExtensor);
        actToeR = MaxActivation(balance?.rightToeFlexor, balance?.rightToeExtensor);
        appTorqueToeL = AppliedTorque(balance?.leftToeFlexor, balance?.leftToeExtensor);
        appTorqueToeR = AppliedTorque(balance?.rightToeFlexor, balance?.rightToeExtensor);

        xCopMeas = CopFromPlateContacts(comPosition.x, useLeft, useRight);
        lambdaMeas = total > 1e-3f ? fzR / total : 0f;

        float soleBottom = float.MaxValue;
        if (useLeft && solePlateMinY[0] < float.MaxValue)
            soleBottom = Mathf.Min(soleBottom, solePlateMinY[0]);
        if (useRight && solePlateMinY[1] < float.MaxValue)
            soleBottom = Mathf.Min(soleBottom, solePlateMinY[1]);
        if (soleBottom < float.MaxValue) lastSoleBottomY = soleBottom;
        supportHeight = Mathf.Max(0.05f, comPosition.y - lastSoleBottomY);

        // The rig publishes no CoM velocity, so this is a one-step finite
        // difference (5 ms at the trial step). Stated as such, not as a sensor.
        comVelX = hasComSample ? (comPosition.x - lastComXSample) / dt : 0f;
        lastComXSample = comPosition.x;
        lastComYSample = comPosition.y;
        hasComSample = true;
        dcmNow = comPosition.x + comVelX * Mathf.Sqrt(supportHeight / 9.81f);

        // Wsup: the MEASURED total vertical force in quiet stance, never a
        // hardcoded mass. It is the mean over the whole quiet window (the second
        // before the walk opens; 4.0-5.0 s for trials that do not walk), so the
        // value stops moving when the window closes.
        if (time >= baselineWindowStart && time < baselineWindowEnd)
        {
            quietFzSum += total;
            quietFzCount++;
        }
        if (quietFzCount > 0) wSup = (float)(quietFzSum / quietFzCount);
        fzOverWsup = wSup > 1f ? total / wSup : 0f;

        ikSaturated = ResolveIkSaturated();
        if (ikSaturated != 0) samplesIkSaturated++;
    }

    // ─── A2 GEOMETRIC SUPPORT STATE + A3 IK WORKSPACE RESIDUAL (19.09.2026) ───
    //
    // A2. The controller's own geometric state (BalanceController.GeometricFallState,
    //     codes in Assets/BodySupportGeometry.cs) is published beside the same
    //     ladder computed HERE against the measured loaded-plate hull that is
    //     already in this CSV (hullMin/hullMax + comX + comVelX + supportH) - the
    //     one a reader can re-derive from the columns next to it. The recorder's
    //     capture point uses supportH; the controller's uses its own
    //     ankle-referenced height (~0.065 m lower h, ~4 % higher omega), so the
    //     two columns can differ on the critical frame and both are named.
    //     "No support" = no foot carries more than 0.2 * wSup this frame, the
    //     same bar the walk brief uses for "this foot is loaded". Nothing here
    //     latches: every frame is classified from scratch, the first-time fields
    //     only remember WHEN a code first appeared for the JSON summary.
    //
    // A3. Per leg, from the ACTUAL pelvis->ankle vector: the leg's real chord,
    //     the nearest reachable chord to it (the pose drive's own bars,
    //     PoseDriveController.SolveLegIk: [0.45*(a+b), 0.995*(a+b)] with a, b the
    //     thigh and shin lengths, and a dy floor of -0.12 m), the angular
    //     residual kneeCmd - kneeNearest in degrees, and the two solver guards
    //     evaluated on that actual vector. The solver only evaluates its guards
    //     on the leg it happens to be solving; here both legs are measured every
    //     frame. The hip point is the pose drive's own anchor (pelvis centre x,
    //     pelvis centre y - pelvisSize.y/2) and the ankle point is the foot
    //     segment centre the estimator publishes - the same point the walker
    //     aims at. A healthy quiet stand is NOT zero: the measured quiet chord
    //     is 0.999 of the bars (a 4.5-5.1 deg knee) while the solver caps at
    //     0.995 (11.46 deg of knee at those bars), so the residual floors at
    //     about -7 deg upright, and the JSON carries that measured baseline
    //     (ikResidKneeQuietMeanAbs*) so the floor is never read as a workspace
    //     violation.
    //
    // A4. pelvisVelY / pelvisAccY: the pelvis rigid body's vertical velocity and
    //     its one-step difference - the channel the scripted squat-and-rise
    //     (-squatRise 1, HeadlessTrial) is judged on. fzTotal is already beside
    //     it in the CSV.
    private void SampleGeometricState(float time, Vector2 comPosition, float pelvisY)
    {
        float dt = Time.fixedDeltaTime;

        // ─── A4: pelvis vertical velocity and acceleration ───
        if (pelvisBody != null)
        {
            pelvisVelY = pelvisBody.linearVelocity.y;
            pelvisAccY = hasPelvisVelSample ? (pelvisVelY - lastPelvisVelYSample) / dt : 0f;
            lastPelvisVelYSample = pelvisVelY;
            hasPelvisVelSample = true;
            maxAbsPelvisAccY = Mathf.Max(maxAbsPelvisAccY, Mathf.Abs(pelvisAccY));
        }
        else
        {
            pelvisVelY = float.NaN;
            pelvisAccY = float.NaN;
        }

        // ─── A2: the controller's state and this recorder's hull state ───
        ctrlFallState = balance != null
            ? balance.GeometricFallState
            : BodySupportGeometry.NoSupport;
        bool hullHasSupport = fzL + fzR > 0.2f * wSup;
        hullFallState = BodySupportGeometry.Level(hullHasSupport, comPosition.x, comVelX,
            supportHeight, hullMinX, hullMaxX);
        if (ctrlFallState >= BodySupportGeometry.Critical)
        {
            if (ctrlFallFirstTime < 0f)
            {
                ctrlFallFirstTime = time;
                ctrlFallFirstCode = ctrlFallState;
            }
            if (ctrlFallState == BodySupportGeometry.Critical) samplesCtrlCritical++;
            else samplesCtrlFallen++;
            if (ctrlFallState == BodySupportGeometry.Fallen && ctrlFallenFirstTime < 0f)
                ctrlFallenFirstTime = time;
        }
        if (hullFallState >= BodySupportGeometry.Critical)
        {
            if (hullFallFirstTime < 0f) hullFallFirstTime = time;
            if (hullFallState == BodySupportGeometry.Critical) samplesHullCritical++;
            else samplesHullFallen++;
            if (hullFallState == BodySupportGeometry.Fallen && hullFallenFirstTime < 0f)
                hullFallenFirstTime = time;
        }

        // ─── A3: the IK workspace residual, per leg, from the actual vector ───
        if (state == null || human == null)
        {
            for (int side = 0; side < 2; side++)
            {
                ikReach[side] = float.NaN;
                ikNear[side] = float.NaN;
                ikResidKnee[side] = float.NaN;
                ikGuardDy[side] = 0;
                ikGuardLen[side] = 0;
                legSpan[side] = 0f;      // the 6x block's sentinel: not measured
                ikOverSpan[side] = 0;
            }
            return;
        }
        float a = human.thighSize.y;
        float b = human.shinSize.y;
        float bars = a + b;
        if (bars < 0.001f) return;
        float rMin = bars * 0.45f;
        float rMax = bars * 0.995f;
        float hipY = pelvisY - human.pelvisSize.y * 0.5f;
        float pelvisX = pelvis != null ? pelvis.position.x : 0f;
        for (int side = 0; side < 2; side++)
        {
            Vector2 foot = side == 0 ? state.leftFootPosition : state.rightFootPosition;
            float dx = foot.x - pelvisX;
            float dy = foot.y - hipY;
            float reach = Mathf.Sqrt(dx * dx + dy * dy);
            float near = Mathf.Clamp(reach, rMin, rMax);
            float kneeNear = 2f * Mathf.Acos(Mathf.Clamp(near / bars, -1f, 1f))
                * Mathf.Rad2Deg;
            float resid = legReqOut[side == 0 ? 2 : 3] - kneeNear;
            ikReach[side] = reach;
            ikNear[side] = near;
            ikResidKnee[side] = resid;
            ikGuardDy[side] = dy > -0.12f ? 1 : 0;
            ikGuardLen[side] = reach < rMin || reach > rMax ? 1 : 0;
            // WALK CYCLE 67: the leg's own SPAN - thigh + shin + footSize.y/2, the
            // live built lengths (a, b above are read per frame, so a foreshortened
            // thigh moves this number with the body) - and the reach > span witness
            // the solver's own clamp cannot see (see the field block). over is
            // signed: its running max is the row's largest exceedance, negative =
            // the largest headroom.
            legSpan[side] = a + b + human.footSize.y * 0.5f;
            float over = reach - legSpan[side];
            ikOverSpan[side] = over > 0f ? 1 : 0;
            samplesIkOverSpan[side] += ikOverSpan[side];
            sumIkReachOverSpan[side] += over;
            if (over > maxIkReachOverSpan) maxIkReachOverSpan = over;
            sumIkResidAbs[side] += Mathf.Abs(resid);
            maxIkResidAbs[side] = Mathf.Max(maxIkResidAbs[side], Mathf.Abs(resid));
            samplesIkGuardDy[side] += ikGuardDy[side];
            samplesIkGuardLen[side] += ikGuardLen[side];
            if (time >= baselineWindowStart && time < baselineWindowEnd)
            {
                sumIkResidQuietAbs[side] += Mathf.Abs(resid);
                samplesIkResidQuiet[side]++;
            }
        }
    }

    private float FractionSaturatedIk()
    {
        return samples > 0 ? (float)samplesIkSaturated / samples : 0f;
    }

    // The CoP is the impulse-weighted mean of the contact points that lie ON the
    // sole plates. Box2D can report a manifold point outside the plate - a
    // vertex-face point from the ground's corner: measured once at 6.700 s,
    // 22.7 N (3.3 % of body weight) on a foot the estimator logged 19 mm clear,
    // point 3.8 mm outside the plate AABB. Such a point is not a pressure centre,
    // so it is counted in copRejected instead of being averaged in. When no point
    // lies on a plate the CoP is undefined: the cell then carries the CoM and
    // copValid = 0, so a reader can never mistake a fallback for a measurement.
    private float CopFromPlateContacts(float fallbackX, bool useLeft, bool useRight)
    {
        float weight = 0f;
        float moment = 0f;
        copRejected = 0;
        for (int side = 0; side < 2; side++)
        {
            for (int k = 0; k < copPointCount[side]; k++)
            {
                float x = copPointX[side * CopPointCapacity + k];
                float w = copPointW[side * CopPointCapacity + k];
                bool onPlate = (useLeft && x >= solePlateMinX[0] && x <= solePlateMaxX[0])
                    || (useRight && x >= solePlateMinX[1] && x <= solePlateMaxX[1]);
                if (onPlate)
                {
                    moment += x * w;
                    weight += w;
                }
                else
                {
                    copRejected++;
                }
            }
        }
        copValid = weight > 1e-6f ? 1 : 0;
        return copValid == 1 ? moment / weight : fallbackX;
    }

    // True when the IK that most recently drove a leg had to clamp its reach.
    // Two implementations exist (the capture walker's chord clamp in
    // SolveCaptureLegIk and the pose controller's dy/length clamp in
    // SolveLegIk); the recorder reports whichever solved most recently instead
    // of guessing which one is "the" walker. Instrumentation only.
    private int ResolveIkSaturated()
    {
        int frame = Time.frameCount;
        int captureTick = balance != null ? balance.CaptureIkSolveTick : int.MinValue;
        int poseTick = pose != null ? pose.IkSolveTick : int.MinValue;
        int sat;
        if (captureTick != int.MinValue && captureTick >= poseTick
            && frame - captureTick <= 2)
            sat = balance.CaptureIkSaturated ? 1 : 0;
        else if (poseTick != int.MinValue && frame - poseTick <= 2)
            sat = pose.IkSaturated ? 1 : 0;
        else
            sat = 0;
        // WALK CYCLE 67 (the fix the c66 finding asked for): the solvers' own clamp
        // witness above is blind to the arithmetic that ends the catch - a demand
        // that exceeds the leg's own SPAN. c66 measured it: at v >= 0.15 the walk's
        // reach demand stayed 0.872-0.888 m while the span shrank by 0.43*v, the
        // catch landed short, the body tipped backward - and this column read
        // 0/2785 through every frame of it. So the column now also fires on the
        // MEASURED pair from A3/WALK CYCLE 67 (ikReach vs legSpan, both live): either
        // leg's actual chord past its own built span = the ask is not a pose the leg
        // has. No change to the clamp path: when no leg is over span this returns
        // exactly what it returned before, so every pre-c67 row keeps its value.
        if (sat == 0 && (ikOverSpan[0] == 1 || ikOverSpan[1] == 1))
            sat = 1;
        return sat;
    }

    // The four sole plates are the heel and toe colliders Human.CreateLeg builds
    // ("LeftLegFoot"/"LeftLegToe", ...), the same boxes BalanceController
    // caches. Resolved lazily: the rig may still be constructing at Bind time.
    private void ResolveSoleColliders()
    {
        if (human == null) return;
        soleCollider[0] = SegmentCollider("LeftLegFoot");
        soleCollider[1] = SegmentCollider("LeftLegToe");
        soleCollider[2] = SegmentCollider("RightLegFoot");
        soleCollider[3] = SegmentCollider("RightLegToe");
        bool any = soleCollider[0] != null || soleCollider[1] != null
            || soleCollider[2] != null || soleCollider[3] != null;
        // Only latch when something was found: a first Sample before the body is
        // built must not freeze four nulls forever.
        if (any) soleCollidersResolved = true;
    }

    private Collider2D SegmentCollider(string name)
    {
        Transform t = human.transform.Find(name);
        return t != null ? t.GetComponent<Collider2D>() : null;
    }

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
