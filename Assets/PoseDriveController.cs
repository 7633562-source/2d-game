using System.Globalization;
using UnityEngine;

// LEVEL 2 pose drive for the human. Switch: -poseDrive 1.
//
// Only the decision layer is replaced, the physics is not. A pose library holds
// target joint angles per side plus a pelvis height; every joint is driven by
// the same stable PD the old path uses (error from the next step, divided by
// 1 + K*dt/I) and written through ActuatorMixer. Segments, masses, the muscle
// torque ceilings, JointFriction, the joint limits and the trial recorder stay
// untouched. BalanceController keeps the whole old path and runs it whenever
// the switch is off, so rollback is one command-line argument.
//
// WALK CYCLE 6q: the balance layer is reduced to TWO metre-valued requests and
// to nothing else -
//   1. WHERE the next foot lands (the capture point plus the margin, clamped to
//      the step band), and
//   2. HOW HIGH the hip is held: WALK CYCLE 6w, the hip height the step in force
//      STARTED from - latched at the swap that started it, capped by the verified
//      stand frame. It was the stand frame's own constant (6u), which froze while
//      the body pitched and sank, and then the live height (6v), which is the very
//      quantity the loop's error is measured against and so made that error
//      identically zero -
// and the POSE LIBRARY (the class at the foot of this file) turns those two
// requests, the step clock and the pelvis state into EVERY joint angle on both
// legs. The library is the single writer of the six leg joints; it writes angles
// through ActuatorMixer and never a torque of its own. THE HANDOVER is written
// at the call marked `WALK CYCLE 6q HANDOVER` in TickWalk: everything above that
// line is metres, everything below it is the library's degrees.
//
// Poses are DATA, in degrees, inside the joint limits of Human.cs: stance (the
// pose the walk leaves the ground from), launch (the first push), swing fold
// (the leg folded under the body, the knee in the drawing's 60..80 deg band),
// foot place (the knee back under 20 deg before the plant, the foot carried to
// the placement), plant / load (the foot down and taking the weight). Five
// poses, blended over the step clock by PoseLibrary.Sample.
//
// The path is chosen once per physics step by BalanceController.FixedUpdate and
// published as `captureWalkPath` (0 = the level 1 path, the capture walker;
// 1 = this pose library holds the joints), so a row says which path ran.
public class PoseDriveController : MonoBehaviour
{
    // ─── switches ──────────────────────────────────────────────────────────
    // Read from the command line by this file, so the frozen -walk switch
    // surface is not touched. Every one of them only tunes this file.
    public bool active;
    private bool walkEnabled;
    private float swingDuration = 0.35f;
    private float stanceDuration = 0.45f;
    private float swingLiftMeters = 0.22f;
    // ─── C1 instrumentation (recorder only; the control math never reads it) ───
    // Set on every SolveLegIk with whether the solve had to clamp, and when.
    public bool IkSaturated;
    public int IkSolveTick = -1;
    private float swingLandShare = 0.8f;
    private float heightGainDegPerMeter = 400f;
    // 25 is the value the 40 s stand was verified on (p_ds 09:44, 15 mm drop).
    // The sign is the one that held: a rising pelvis extends the leg. Do not
    // flip it without a trial - the 09:58 build with this term at 0 and the
    // knee clamp one sided ran 2 m forward and face planted.
    private float heightDampDegPerMeterPerSecond = 25f;
    private float placementMarginMeters = 0.04f;
    private float comLeadSwapMeters = 0.16f;
    // WALK CYCLE 6r: the floor was +0.02 m, so the law could only ever ask for a
    // step IN FRONT of the stance foot. c6r_posewalk1 measured the request going
    // behind it for 143 of the 172 single-support frames of the third cycle -
    // captureX + margin fell from +0.7480 m at 6.45 s to -0.0532 m at the 7.275 s
    // swap against a recorded stance hull of 0.7040..0.7066 m (ankle 0.771 m) -
    // so the clamp answered "forward 0.02 m" (0.791 m, i.e. 0.54 m ahead of a CoM
    // that had already returned to 0.2525 m) and held that answer for 0.855 s.
    // The band is now one step either side of the stance foot; the law can put
    // the pivot behind a CoM that is falling backwards. Read only inside
    // TickWalk's placeX clamp, so the quiet stand, the -poseWalk 0 stand and the
    // level 1 capture walker cannot see it.
    private float stepMinMeters = -0.45f;
    private float stepMaxMeters = 0.45f;
    private float standCaptureSeconds = 4.6f;
    private float standTiltLimitDeg = 8f;
    private float fallenDropMeters = 0.40f;
    private float fallenTiltDeg = 50f;
    private float armSwingDeg = 22f;

    // ─── bound body ────────────────────────────────────────────────────────
    private bool bound;
    private Human human;
    private BalanceController balance;
    private VestibularSystem vestibular;
    private BodyStateEstimator state;
    private CenterOfMassCalculator comCalculator;
    private MotionIntent intent;
    private Rigidbody2D pelvisBody;

    private readonly ActuatorMixer mixer = new ActuatorMixer();

    private HingeJoint2D leftHipJoint, rightHipJoint;
    private HingeJoint2D leftKneeJoint, rightKneeJoint;
    private HingeJoint2D leftAnkleJoint, rightAnkleJoint;
    private HingeJoint2D leftToeJoint, rightToeJoint;
    private HingeJoint2D lumbarJoint, neckJoint, headJoint;
    private HingeJoint2D leftShoulderJoint, rightShoulderJoint;
    private HingeJoint2D leftElbowJoint, rightElbowJoint;
    private HingeJoint2D leftWristJoint, rightWristJoint;

    private float leftHipInertia, rightHipInertia;
    private float leftKneeInertia, rightKneeInertia;
    private float leftAnkleInertia, rightAnkleInertia;
    private float leftToeInertia, rightToeInertia;
    private float lumbarInertia, neckInertia, headInertia;
    private float leftShoulderInertia, rightShoulderInertia;
    private float leftElbowInertia, rightElbowInertia;
    private float leftWristInertia, rightWristInertia;

    // ─── quiet ankle loop ──────────────────────────────────────────────────
    // The law is the standing controller's, unchanged: command = P*(offset/ref)
    // + D*(speed/ref) with offsets in metres, so a 7 mm CoM offset already asks
    // for the whole ankle. The two factors scale that pair from the CLI.
    private float ankleBalanceP = 1.0f;
    private float ankleBalanceD = 1.0f;

    // ─── stand frame ───────────────────────────────────────────────────────
    private bool standCaptured;
    private float standPelvisY;
    private float standAnkleY;
    private float groundY;
    private float pelvisCenterToSole;

    // ─── step clock ────────────────────────────────────────────────────────
    private int swingSign = 1;          // +1 = the left leg is the swing leg
    private float stepClock;
    private float stanceFootX;
    private float swingFootX0;
    private float phaseForLog;
    // WALK CYCLE 6t: the CoM's own X at the last swap (NaN until the first
    // walk step). The lead the swap rule reads is measured against the stance
    // foot, and after a placement that foot sits most of a step ahead of the
    // CoM, so that lead is structurally unreachable until the CoM has walked
    // the step back (6s: the third swap could only fire on the step clock, with
    // comVelX already -0.888). The same quantity measured from the CoM's own
    // position at the swap does close, and it is what the swing's descent is
    // scheduled by - see `need` in TickWalk.
    private float comXSwap = float.NaN;

    // WALK CYCLE 6w: the hip height at the swap that STARTED the step in force
    // (NaN until the walk's own first frame) - the same one-read latch as
    // comXSwap, written in the same block on the same frame, and NOT a second
    // clock. 6v asked for the LIVE height of the step in force, i.e. for the very
    // quantity the height loop's error is then measured against: while the step
    // was at or under the stand frame the request WAS the live height, frame for
    // frame, so `request - live` was identically 0 on every driving row and the
    // channel carried -damp * pelvisVelY and nothing else, whatever the body did.
    // A datum taken once per step cannot do that: it is a constant for the whole
    // step, so the error is the step's own sink from where it started and the
    // ask is a function of the body moving under the step instead of a mirror of
    // it. Read-only for every other law in this file (the walk's balance layer
    // request and its two publication columns).
    private float hipHeightStepDatumM = float.NaN;

    // ─── recorder-facing counters ──────────────────────────────────────────
    public int poseStepCount;
    public float poseHeightError;

    // The torso (lumbar) world tilt target the pose in force owns: written by
    // TickStand / TickWalk on every physics step, read by the trunk driver. The
    // walk writes the pose library's own torso target; the stand writes the
    // verified lumbarTargetTilt, i.e. the stand path is unchanged.
    private float torsoTargetDeg;
    // Human.cs writes the ankle's hinge limits as literals at the heel's
    // ConnectTo call (Human.cs:555) and names no field for them; the pose
    // library clamps every ankle target to this symmetric pair.
    private const float AnkleLimitDeg = 45f;

    // ═══ WALK CYCLE 6s RECORDER-FACING PUBLICATIONS: THE POSE GAIT'S OWN ASKS ═══
    // c6r2_walk lost its row with the REQUEST unpublished. The row carried what
    // the library's angles became (hipL/R, kneeL/R, ankleL/R), what the PD did
    // with them (appTorque*) and which path wrote them (captureWalkPath) - but
    // not what the library ASKED FOR on the step: its six joint targets, the hip
    // height and pelvis pitch it held, and the step clock's own swap decision.
    // So in the 6.4-7.0 s reversal "the gait asked the stance hip for this" and
    // "the PD did this with the stance hip" could not be told apart. These are
    // recorder-only publications of values TickWalk ALREADY computes: no gain,
    // limit, threshold, sign, branch or target in this file (or in any other)
    // reads one of them back, and none of them is a second source of a value
    // another column already carries.
    // Sentinels: 0 = the pose gait is not driving the WALK on this step - the
    // -poseDrive switch is off (this component is then never ticked at all and
    // the whole block stays 0), the gait is standing (TickStand writes no walk
    // ask), or the pose's own down test relaxed the body (Tick returns through
    // RelaxAll). Never NaN: every writer writes a finite constant or a value the
    // library computed from finite inputs.
    private float poseAskHipLDeg, poseAskKneeLDeg, poseAskAnkleLDeg;
    private float poseAskHipRDeg, poseAskKneeRDeg, poseAskAnkleRDeg;
    private float poseAskHipHeightRequestM, poseAskHipHeightLiveM;
    private float poseAskPelvisTiltTargetDeg;
    private int poseAskStepIndex;
    private int poseAskMaySwap, poseAskClockDone, poseAskLedOut;
    private int poseAskSwapReason;
    private int poseAskDriveState;

    // Why the swap fired this step (capturePoseSwapReason). Frozen codes: an old
    // CSV stays readable.
    public const int PoseSwapReasonNone = 0;            // no swap on this step
    public const int PoseSwapReasonClock = 1;           // the half cycle ran out
    public const int PoseSwapReasonLead = 2;            // the CoM led the stance foot
    public const int PoseSwapReasonPlantedTimeout = 3;  // the swing never landed
    // Whether the pose gait wrote the joints this step (capturePoseDriveState).
    public const int PoseDriveStateStopped = 0;         // not driving (off/relaxed)
    public const int PoseDriveStateStand = 1;           // driving the stand pose
    public const int PoseDriveStateWalk = 2;            // driving the walk gait

    // capturePoseTargetHipLDeg .. capturePoseTargetAnkleRDeg - the six leg joint
    //    ANGLE requests the library handed the PD on this step, degrees, rig
    //    convention (hip minus = flexion, knee plus = flexion, ankle plus = toe
    //    down / heel rise). The side is the RIG's side, not the swing/stance
    //    role: the swing leg is the one whose knee target is the folded one.
    // capturePoseHipHeightRequestM - the hip height the balance layer asked the
    //    gait to hold, m: the height loop's request. WALK CYCLE 6w: it is the hip
    //    height the step in force STARTED from, latched at the swap that started
    //    it (the walk's own first frame for the first step) and capped by the
    //    verified stand frame, so the column HOLDS one value for the whole step
    //    while capturePoseHipHeightLiveM moves - the two separate inside a step
    //    and `request - live` at its end IS that step's own sink. 6u made this
    //    column the stand constant 0.9850 (1 distinct value on every driving row)
    //    and 6v the live height (329 distinct values, |request - live| = 0.000000
    //    on all 417 driving rows of c6v_walk: the loop's error dead by
    //    construction, the channel carrying the rate term alone).
    // capturePoseHipHeightLiveM - the hip height the body had this step, m
    //    (pelvisY - groundY). Request minus live is that loop's own error.
    // capturePosePelvisTiltTargetDeg - the pelvis world tilt the pose in force
    //    owns, deg, minus = forward (the recorder's pelvisTilt sign). Read
    //    against the row's pelvisTilt: it is the pose's own pitch error, and its
    //    answer is the trunk correction the library spends on the stance hip.
    //    WALK CYCLE 6w: this column is NOT the balance layer's ask and it carries
    //    no height error. The library writes the drawn pose's own pelvis tilt
    //    (PoseLibrary.Sample: `t.pelvisTiltTargetDeg = p.pelvisTiltDeg`; min
    //    -1.9919 / max 0.0000, 50 distinct values on 6u and 45 on 6v, while the
    //    measured pelvisTilt reaches -12.79 deg before the fall) and the error
    //    into the stance hip is the library's separate trunk hold - TrunkGainDegPerDeg
    //    x DeltaAngle(measured, that target) - TrunkDampDegPerDegPerSec x rate,
    //    bounded TrunkBandDeg - which reads no height request at all. So the
    //    height channel's only witnesses are capturePoseHipHeightRequestM and
    //    capturePoseHipHeightLiveM, and the A/B is read off those two.
    // capturePoseStepIndex - the index of the step in force: the number of swaps
    //    the pose gait has fired, the same number walkSwap carries, published
    //    inside the pose block so the block reads alone (a second read, not a
    //    second source).
    // capturePoseMaySwap / ClockDone / LedOut - the three inputs of the swap
    //    decision exactly as TickWalk computed them this step, 0/1.
    // capturePoseSwapReason - which of them fired the swap on the step a swap
    //    happened: 1 clock, 2 lead, 3 the planted timeout; 0 = no swap.
    // capturePoseDriveState - 0 the pose gait is stopped (the switch is off, or
    //    its own down test relaxed the body), 1 it is driving the STAND pose, 2
    //    it is driving the WALK. This is the column that answers the post-fall
    //    runaway: a row with the recorder's `down` = 1 and this column at 2 is a
    //    pose gait still driving a body that is already on the slab.
    public float capturePoseTargetHipLDeg => poseAskHipLDeg;
    public float capturePoseTargetKneeLDeg => poseAskKneeLDeg;
    public float capturePoseTargetAnkleLDeg => poseAskAnkleLDeg;
    public float capturePoseTargetHipRDeg => poseAskHipRDeg;
    public float capturePoseTargetKneeRDeg => poseAskKneeRDeg;
    public float capturePoseTargetAnkleRDeg => poseAskAnkleRDeg;
    public float capturePoseHipHeightRequestM => poseAskHipHeightRequestM;
    public float capturePoseHipHeightLiveM => poseAskHipHeightLiveM;
    public float capturePosePelvisTiltTargetDeg => poseAskPelvisTiltTargetDeg;
    public int capturePoseStepIndex => poseAskStepIndex;
    public int capturePoseMaySwap => poseAskMaySwap;
    public int capturePoseClockDone => poseAskClockDone;
    public int capturePoseLedOut => poseAskLedOut;
    public int capturePoseSwapReason => poseAskSwapReason;
    public int capturePoseDriveState => poseAskDriveState;

    // Single writer of the 0 sentinel for the ask block: called once per physics
    // step BEFORE either gait branch, so a row never carries the previous step's
    // ask, and a step that relaxes the body (or one that never ticks this
    // component at all) carries the documented zero.
    private void ClearPoseAskRecord()
    {
        poseAskHipLDeg = 0f;
        poseAskKneeLDeg = 0f;
        poseAskAnkleLDeg = 0f;
        poseAskHipRDeg = 0f;
        poseAskKneeRDeg = 0f;
        poseAskAnkleRDeg = 0f;
        poseAskHipHeightRequestM = 0f;
        poseAskHipHeightLiveM = 0f;
        poseAskPelvisTiltTargetDeg = 0f;
        poseAskStepIndex = 0;
        poseAskMaySwap = 0;
        poseAskClockDone = 0;
        poseAskLedOut = 0;
        poseAskSwapReason = PoseSwapReasonNone;
        poseAskDriveState = PoseDriveStateStopped;
    }

    public void Initialize(Human owner, BalanceController controller)
    {
        human = owner;
        balance = controller;
        if (human == null || balance == null) return;

        vestibular = human.vestibularSystem;
        state = human.bodyStateEstimator;
        comCalculator = human.comCalculator;
        intent = GetComponent<MotionIntent>();

        Transform root = transform;
        pelvisBody = FindBody(root, "Pelvis");
        leftHipJoint = FindJoint(root, "LeftLegThigh");
        rightHipJoint = FindJoint(root, "RightLegThigh");
        leftKneeJoint = FindJoint(root, "LeftLegShin");
        rightKneeJoint = FindJoint(root, "RightLegShin");
        leftAnkleJoint = FindJoint(root, "LeftLegFoot");
        rightAnkleJoint = FindJoint(root, "RightLegFoot");
        leftToeJoint = FindJoint(root, "LeftLegToe");
        rightToeJoint = FindJoint(root, "RightLegToe");
        lumbarJoint = FindJoint(root, "Torso");
        neckJoint = FindJoint(root, "Neck");
        headJoint = FindJoint(root, "Head");
        leftShoulderJoint = FindJoint(root, "LeftArmUpper");
        rightShoulderJoint = FindJoint(root, "RightArmUpper");
        leftElbowJoint = FindJoint(root, "LeftArmLower");
        rightElbowJoint = FindJoint(root, "RightArmLower");
        leftWristJoint = FindJoint(root, "LeftArmHand");
        rightWristJoint = FindJoint(root, "RightArmHand");

        mixer.activationSpeed = balance.muscleActivationSpeed;
        mixer.speedReference = balance.speedReferenceDegPerSec;

        // The pelvis centre sits pelvisSize.y/2 above the hip anchor, the sole
        // footSize.y/2 below the ankle: the stand frame is the whole chain.
        pelvisCenterToSole = human.pelvisSize.y * 0.5f
            + human.thighSize.y + human.shinSize.y + human.footSize.y * 0.5f;

        ReadSwitches();
        bound = true;
    }

    private void ReadSwitches()
    {
        active = Mathf.RoundToInt(GetFloat("-poseDrive", 0f)) != 0;
        walkEnabled = Mathf.RoundToInt(GetFloat("-poseWalk", 0f)) != 0;
        swingDuration = Mathf.Max(0.05f, GetFloat("-poseSwingT", swingDuration));
        stanceDuration = Mathf.Max(0.05f, GetFloat("-poseStanceT", stanceDuration));
        swingLiftMeters = Mathf.Max(0f, GetFloat("-poseLift", swingLiftMeters));
        swingLandShare = Mathf.Clamp(GetFloat("-poseLandShare", swingLandShare), 0.3f, 0.98f);
        heightGainDegPerMeter = GetFloat("-poseHeightGain", heightGainDegPerMeter);
        heightDampDegPerMeterPerSecond =
            GetFloat("-poseHeightDamp", heightDampDegPerMeterPerSecond);
        placementMarginMeters = GetFloat("-posePlaceMargin", placementMarginMeters);
        comLeadSwapMeters = Mathf.Max(0.01f, GetFloat("-poseLeadSwap", comLeadSwapMeters));
        stepMinMeters = GetFloat("-poseStepMin", stepMinMeters);
        stepMaxMeters = Mathf.Max(stepMinMeters + 0.01f, GetFloat("-poseStepMax", stepMaxMeters));
        standCaptureSeconds = GetFloat("-poseStandCap", standCaptureSeconds);
        armSwingDeg = GetFloat("-poseArmSwing", armSwingDeg);
        ankleBalanceP = GetFloat("-poseAnkleP", ankleBalanceP);
        ankleBalanceD = GetFloat("-poseAnkleD", ankleBalanceD);
    }

    // ─── ARENA-1 genome entry ──────────────────────────────────────────────
    // The parameter-sweep arena writes its 13 search genes through this one call
    // instead of reflecting into the private fields. The clamps are the same ones
    // ReadSwitches applies, so a genome can never leave the box the command-line
    // switches accept. `enable` turns the pose drive on the way -poseDrive 1
    // -poseWalk 1 would: active is the drive, walkEnabled is the gait.
    public const int ArenaGeneCount = 13;

    public void ApplyArenaGenome(float[] g, bool enable)
    {
        if (g == null || g.Length < ArenaGeneCount) return;
        active = enable;
        walkEnabled = enable;
        swingDuration = Mathf.Max(0.05f, g[0]);
        stanceDuration = Mathf.Max(0.05f, g[1]);
        swingLiftMeters = Mathf.Max(0f, g[2]);
        swingLandShare = Mathf.Clamp(g[3], 0.3f, 0.98f);
        heightGainDegPerMeter = g[4];
        heightDampDegPerMeterPerSecond = g[5];
        placementMarginMeters = g[6];
        comLeadSwapMeters = Mathf.Max(0.01f, g[7]);
        // WALK CYCLE 6r: the floor at 0 was the ONE clamp ApplyArenaGenome added
        // that ReadSwitches does not (-poseStepMin passes its switch value through
        // bare), so it contradicted this path's own header promise that a genome
        // can never leave the box the command-line switches accept. With the
        // shipped band now -0.45 .. +0.45 the floor also walled off half of it:
        // every negative sample landed on 0, the gene read as a constant, and no
        // row of the influence log can show it - the log stores the gene it
        // SAMPLED, not the value the body received. The band is clamped by
        // ArenaGenome.Clamp (the box); this line only assigns.
        stepMinMeters = g[8];
        stepMaxMeters = Mathf.Max(stepMinMeters + 0.01f, g[9]);
        armSwingDeg = g[10];
        ankleBalanceP = g[11];
        ankleBalanceD = g[12];
    }

    // ─── one physics step ──────────────────────────────────────────────────
    public void Tick(float comOffset)
    {
        if (!bound) return;

        // WALK CYCLE 6s (recorder only): the pose-ask block starts every step at
        // its documented 0 sentinel, BEFORE any branch can return. A step that
        // relaxes the body (the pose's own down test) or that drives the stand
        // pose reports a zero ask and capturePoseDriveState = 0/1, so the row
        // says itself whether the pose gait is driving.
        ClearPoseAskRecord();

        float tilt = vestibular != null ? vestibular.GetBodyTilt() : 0f;
        float pelvisTilt = vestibular != null ? vestibular.GetPelvisTilt() : 0f;
        float pelvisAngVel = vestibular != null ? vestibular.GetPelvisAngularVelocity() : 0f;
        float pelvisY = pelvisBody != null ? pelvisBody.position.y : 0f;
        float pelvisVelY = pelvisBody != null ? pelvisBody.linearVelocity.y : 0f;
        float comX = state != null ? state.comPosition.x : 0f;
        float comVelX = state != null ? state.comVelocity.x : 0f;
        bool leftDown = state != null && state.leftFootGrounded;
        bool rightDown = state != null && state.rightFootGrounded;
        float leftFootX = state != null ? state.leftFootPosition.x : 0f;
        float rightFootX = state != null ? state.rightFootPosition.x : 0f;
        float leftFootY = state != null ? state.leftFootPosition.y : 0f;
        float rightFootY = state != null ? state.rightFootPosition.y : 0f;

        CaptureStandFrame(tilt, pelvisY, leftDown || rightDown,
            (leftFootY + rightFootY) * 0.5f);
        groundY = standPelvisY - pelvisCenterToSole;

        if (IsDown(tilt, pelvisY))
        {
            // Honest reporting first: the recorder reads BalanceState, and a
            // body on the slab is Falling whatever the old automaton did with
            // the CoM offset. The get up sequence is stage 3.
            balance.currentState = BalanceController.BalanceState.Falling;
            balance.locomotionState = BalanceController.LocomotionState.Fallen;
            RelaxAll();
            return;
        }

        bool walkKey = WalkKeyHeld();
        balance.locomotionState = walkKey
            ? BalanceController.LocomotionState.Walking
            : BalanceController.LocomotionState.Standing;
        balance.currentState = Mathf.Abs(tilt) > 25f
            ? BalanceController.BalanceState.Recovery
            : BalanceController.BalanceState.Balancing;

        float pelvisHeight = pelvisY - groundY;   // kept for the walk log
        poseHeightError = standPelvisY - pelvisY;
        // The recorder reads CaptureWalkStepCount for walkSwap. The pose gait
        // owns the steps while the switch is on, so it publishes its own count
        // there instead of the recorder learning a second source.
        balance.poseDriveStepCount = poseStepCount;

        if (walkKey && walkEnabled && standCaptured && (leftDown || rightDown))
        {
            TickWalk(tilt, pelvisTilt, pelvisAngVel, pelvisY, pelvisVelY,
                comX, comVelX, leftDown, rightDown, leftFootX, rightFootX,
                leftFootY, rightFootY);
        }
        else
        {
            TickStand(tilt, pelvisTilt, pelvisAngVel, pelvisY, pelvisVelY,
                comX, comVelX, leftDown, rightDown, leftFootY, rightFootY);
            // WALK CYCLE 6s (recorder only): the pose gait IS writing the joints,
            // but with the stand pose - no walk ask this step.
            poseAskDriveState = PoseDriveStateStand;
        }
        DriveTrunk(pelvisTilt, pelvisAngVel, tilt, walkKey && walkEnabled, torsoTargetDeg);
    }

    // ─── stand frame ───────────────────────────────────────────────────────
    // The stand frame is the quiet stance height, tracked while the body is
    // still upright, not the spawn frame: a pose that holds it holds the walk
    // start too. The sole stays at groundY for the whole trial, so one capture
    // is enough.
    private void CaptureStandFrame(float tilt, float pelvisY, bool anyDown, float footY)
    {
        if (standCaptured && Time.fixedTime > standCaptureSeconds) return;
        if (anyDown && Mathf.Abs(tilt) < standTiltLimitDeg)
        {
            if (!standCaptured || pelvisY > standPelvisY)
            {
                standPelvisY = pelvisY;
                standAnkleY = footY;
            }
            standCaptured = true;
        }
    }

    private bool IsDown(float tilt, float pelvisY)
    {
        if (!standCaptured) return false;
        return pelvisY < standPelvisY - fallenDropMeters
            && Mathf.Abs(tilt) > fallenTiltDeg;
    }

    private bool WalkKeyHeld()
    {
        return intent != null && Mathf.Abs(intent.moveX) > 0.01f;
    }

    // ─── quiet pose ────────────────────────────────────────────────────────
    private void TickStand(float tilt, float pelvisTilt, float pelvisAngVel,
                           float pelvisY, float pelvisVelY, float comX, float comVelX,
                           bool leftDown, bool rightDown, float leftFootY, float rightFootY)
    {
        float knee = KneeForHeight(pelvisY, pelvisVelY);
        // The stand path is the law the 40 s stand was verified on and stays as
        // it is: the pelvis-upright law owns both hips, the CoM loop owns the
        // ankles (a torque, not an angle), the trunk target is the controller's
        // own. The pose library drives the WALK; its Stance pose is the pose the
        // walk leaves the ground from, not a second stand controller.
        torsoTargetDeg = balance.lumbarTargetTilt;
        // The stand hip carries no angle target: the pelvis upright law below
        // owns both hips, and on planted legs that is also the hip pose.
        DriveOneLeg(true, float.NaN, knee, false, 0f);
        DriveOneLeg(false, float.NaN, knee, false, 0f);
        DriveHips(pelvisTilt, pelvisAngVel, 0f, true, true, 0f, false);
        DriveAnkleBalance(comX, comVelX, leftDown, rightDown);
        DriveToes(0f, 0f);
        DriveArms(0f, 0f, balance.elbowBaseAngle, balance.elbowBaseAngle);
        // tilt / leftFootY / rightFootY: the quiet pose reads the world tilt
        // through the trunk driver; the foot heights matter only on the walk.
    }

    // Stance knee target from the pelvis height. A low pelvis needs a longer
    // leg, and on this chain length is cos(knee/2) — so the sign is minus. The
    // rate term extends further while the pelvis is still descending: the
    // damping must push the same way as the error term, or a sinking pelvis
    // commands a deeper knee and the loop feeds itself.
    private float KneeForHeight(float pelvisY, float pelvisVelY)
    {
        float error = standPelvisY - pelvisY;
        float knee = balance.kneeBaseAngle
            - heightGainDegPerMeter * error
            - heightDampDegPerMeterPerSecond * pelvisVelY;
        // Extension only past the base pose was tried and made it worse, so the
        // band stays the verified [-10, 60] window of the 09:44 stand.
        return Mathf.Clamp(knee, 0f, 60f);
    }

    // ─── walk pose ─────────────────────────────────────────────────────────
    private void TickWalk(float tilt, float pelvisTilt, float pelvisAngVel,
                          float pelvisY, float pelvisVelY, float comX, float comVelX,
                          bool leftDown, bool rightDown, float leftFootX, float rightFootX,
                          float leftFootY, float rightFootY)
    {
        float dt = Time.fixedDeltaTime;
        float halfCycle = swingDuration + stanceDuration;
        float swingFootX = swingSign > 0 ? leftFootX : rightFootX;
        float stanceX = swingSign > 0 ? rightFootX : leftFootX;
        bool swingPlanted = swingSign > 0 ? leftDown : rightDown;

        stepClock += dt;
        // Rule: the step comes from the CoM leaving the support area. During
        // the double support tail the swap waits for the walk clock, but a CoM
        // that has already passed the stance foot by comLeadSwapMeters starts
        // the next swing early instead of letting the body topple over it.
        float lead = comX - stanceFootX;
        // A swap while the swing foot is still airborne is a flight phase: the
        // body free falls, lands on folded legs and the 130 N*m knee ceiling
        // cannot straighten it again (p_dsw 5.30, both soles up 0.08 s, pelvis
        // 55 mm down and sinking). So the swap waits for the foot to be planted,
        // with a hard timeout so a swing that never lands cannot stall the gait.
        bool maySwap = swingPlanted || stepClock >= halfCycle + 0.15f;
        bool clockDone = stepClock >= halfCycle;
        bool ledOut = stepClock >= swingDuration && lead > comLeadSwapMeters;
        if (maySwap && (clockDone || ledOut))
        {
            // WALK CYCLE 6s (recorder only): why THIS swap fired. The planted
            // timeout outranks the other two - it is the gait's own defect flag,
            // a swing that never landed inside the half cycle plus the slack.
            poseAskSwapReason = stepClock >= halfCycle + 0.15f
                ? PoseSwapReasonPlantedTimeout
                : (ledOut && !clockDone ? PoseSwapReasonLead : PoseSwapReasonClock);
            stepClock -= Mathf.Min(stepClock, halfCycle);
            swingSign = -swingSign;
            poseStepCount++;
            // WALK CYCLE 6t: the CoM's own position at the swap is the datum the
            // descent's need is read from (see `need` below).
            comXSwap = comX;
            // WALK CYCLE 6w: the height datum of the step that starts on this
            // frame, by the same latch - one write per swap, no second clock.
            hipHeightStepDatumM = pelvisY - groundY;
            swingFootX0 = swingSign > 0 ? leftFootX : rightFootX;
            stanceFootX = swingSign > 0 ? rightFootX : leftFootX;
            swingFootX = swingSign > 0 ? leftFootX : rightFootX;
        }

        // WALK CYCLE 6s (recorder only): the step clock's decision as it was
        // taken this step, and the index of the step now in force (written after
        // the swap above, so it is the same number walkSwap carries on this row).
        poseAskMaySwap = maySwap ? 1 : 0;
        poseAskClockDone = clockDone ? 1 : 0;
        poseAskLedOut = ledOut ? 1 : 0;
        poseAskStepIndex = poseStepCount;
        poseAskDriveState = PoseDriveStateWalk;

        float s = Mathf.Clamp01(stepClock / Mathf.Max(0.05f, swingDuration));
        phaseForLog = s;

        // WALK CYCLE 6t — THE SWING'S DESCENT IS SCHEDULED BY THE CoM.
        // `lead` above is the swap rule's own quantity, but it is read against
        // the STANCE foot, and after a placement that foot sits most of a step
        // ahead of the CoM: the 0.16 m is never taken back, the lead rule goes
        // structurally dead and the next swap falls through to the step clock
        // (6s: swap 3 at 7.125 s on reason 1 = clock, comVelX already -0.888).
        // The same quantity measured from the CoM's own position at the swap
        // does close, and it is the one the delivery follows: how far the CoM
        // itself has advanced, in the units the swap decision uses. 0 = the CoM
        // has not moved since the swap, 1 = it has taken the lead the lead rule
        // calls a swap, i.e. the moment the new support is needed.
        if (float.IsNaN(comXSwap)) comXSwap = comX;
        float need = Mathf.Clamp01((comX - comXSwap) / Mathf.Max(0.01f, comLeadSwapMeters));

        // ═══ THE BALANCE LAYER, REDUCED TO TWO METRE-VALUED REQUESTS ═══
        // 2. HOW HIGH the hip is held: the datum of the STEP IN FORCE. Metres.
        // WALK CYCLE 6u-6v: this was the stand frame's own height (standPelvisY -
        // groundY), a constant 0.9850 m for the whole walk - a datum the walk
        // left behind at its first step and never took back. It turned the
        // height loop into an extension ask that could not be answered: over the
        // last step's 0.7281 m pelvis the request still read 0.9850 m, so the
        // loop's error was the 0.2569 m the body had already given up and it
        // asked 400 * 0.2569 = 103 deg of knee extension against the 12 deg
        // HeightTrimBandDeg, with the stance knee already on its own 0 deg floor
        // (c6u_walk at 10.700 s: capturePoseHipHeightRequestM 0.9850 against
        // capturePoseHipHeightLiveM 0.7281, capturePoseTargetKneeLDeg 0.0,
        // capturePoseTargetHipLDeg -2.0, pelvisTilt +34.4 deg). 6v answered that
        // with the LIVE height, which is the quantity the error is then measured
        // against: `request - live` came out exactly 0.000000 m on all 417
        // driving rows of c6v_walk (329 distinct requests, every one of them equal
        // to live), and the channel was left carrying the -damp * pelvisVelY rate
        // term alone - the error term dead by construction - while the body's hip
        // ran 0.1335 .. 0.9778 m and the row fell at 6.840 s.
        // WALK CYCLE 6w: the request is the height the step STARTED from - the
        // latch taken at the swap that started it (hipHeightStepDatumM above), and
        // the walk's own first frame for the first step, which no swap precedes.
        // It is then CONSTANT for the whole step, so the request and live separate
        // as the step sinks: at the end of a step `request - live` IS that step's
        // own sink - 0.0048 / 0.0851 m on 6v's first three steps and 0.5300 m on
        // the step that was already collapsing at t 6.67 - the loop's error term
        // is 400 * that sink, and the ask saturates the 12 deg band in the
        // direction that lifts the body instead of asking nothing at all. The
        // verified stand frame stays the request's CEILING: a step is never asked
        // for a hip taller than the quiet stance the stand loop is verified on.
        float standHipHeightM = standPelvisY - groundY;
        float hipHeightLiveM = pelvisY - groundY;
        // The latch is READ here, never re-taken: latching it on every frame (or
        // re-reading it from the body) is exactly the mirror 6v measured, and it
        // would make the error term identically zero again. The one write is the
        // swap above; only the walk's first step, which no swap precedes, has no
        // datum yet, and its datum is this frame's own live height.
        if (float.IsNaN(hipHeightStepDatumM)) hipHeightStepDatumM = hipHeightLiveM;
        float hipHeightTargetM = Mathf.Min(standHipHeightM, hipHeightStepDatumM);
        // WALK CYCLE 6s (recorder only): the height loop's request and the height
        // the body had, published with the rest of the ask block below.
        poseAskHipHeightRequestM = hipHeightTargetM;
        poseAskHipHeightLiveM = hipHeightLiveM;
        // 1. WHERE the next foot lands: the capture point (where the CoM will be
        //    in one pendulum time constant) plus the stability margin the plan
        //    asks for, clamped to the step band. Metres.
        float pendulum = Mathf.Sqrt(Mathf.Max(0.05f, (pelvisY - groundY) / 9.81f));
        float captureX = comX + comVelX * pendulum;
        float placeX = Mathf.Clamp(captureX + placementMarginMeters,
            stanceFootX + stepMinMeters, stanceFootX + stepMaxMeters);
        // ... and never further than the leg can bring the SOLE to the floor
        // from where the hip is right now. The chain is thigh + shin and the sole
        // hangs soleBelowAnkleM below the ankle, so from a hip hipToSoleM above
        // the sole the widest the ankle may stand is sqrt(leg^2 - hipToSole^2).
        // Past it the placement is a foot the leg cannot put down: the drawn
        // pose holds its angles, the sole hangs and the landing waits for the
        // sinking pelvis instead of for the CoM (6s: 0.52 s of single support
        // with fz exactly 0 and the second swap's foot landing 0.63 s after its
        // own swap). The limit is ramped in by the same need, so while the CoM
        // does not need the foot the placement is the capture point's, unchanged.
        float hipX = pelvisBody != null ? pelvisBody.position.x : 0f;
        float legLengthM = human.thighSize.y + human.shinSize.y;
        float hipToSoleM = hipHeightLiveM - human.footSize.y * 0.5f;
        float reachXM = Mathf.Sqrt(Mathf.Max(0f,
            legLengthM * legLengthM - hipToSoleM * hipToSoleM));
        placeX = Mathf.Lerp(placeX,
            Mathf.Clamp(placeX, hipX - reachXM, hipX + reachXM), need);

        // ═══════════════════ WALK CYCLE 6q HANDOVER ═══════════════════════
        // Above this line: metres, and the two requests are the balance layer's
        // own decisions. Below it: degrees, and every one of them is the pose
        // library's. The library is the single writer of the six leg joints on
        // this step - nothing after this call writes a raw joint angle, and the
        // pelvis-upright law (DriveHips) is NOT called on the walk: the trunk is
        // held by the pose's own pelvis target plus the library's bounded hip
        // correction on the stance leg.
        PoseLibrary.Inputs poseIn = new PoseLibrary.Inputs();
        poseIn.swingPhase = s;
        poseIn.landingShare = swingLandShare;
        poseIn.footPlaceX = placeX;
        poseIn.hipHeightM = hipHeightLiveM;
        poseIn.hipHeightTargetM = hipHeightTargetM;
        poseIn.pelvisX = hipX;
        // WALK CYCLE 6t: the same need the placement above is ramped by, handed
        // to the library so the descent's own ask closes on it too.
        poseIn.supportNeed = need;
        poseIn.pelvisTiltDeg = pelvisTilt;
        poseIn.pelvisAngVelDeg = pelvisAngVel;
        poseIn.pelvisVelY = pelvisVelY;
        poseIn.thighM = human.thighSize.y;
        poseIn.shinM = human.shinSize.y;
        // The ankle sits at the centre of the heel plate, so the sole hangs half
        // a foot below the hinge (Human.cs CreateLeg / pelvisCenterToSole above).
        poseIn.soleBelowAnkleM = human.footSize.y * 0.5f;
        poseIn.swingLiftM = swingLiftMeters;
        poseIn.heightGainDegPerM = heightGainDegPerMeter;
        poseIn.heightDampDegPerMps = heightDampDegPerMeterPerSecond;
        poseIn.hipMin = human.hipLimitMin;
        poseIn.hipMax = human.hipLimitMax;
        poseIn.kneeMin = human.kneeLimitMin;
        poseIn.kneeMax = human.kneeLimitMax;
        poseIn.ankleLimit = AnkleLimitDeg;
        poseIn.toeMin = human.toeLimitMin;
        poseIn.toeMax = human.toeLimitMax;
        PoseLibrary.Targets pose = PoseLibrary.Sample(poseIn);

        // The trunk target this step's pose owns (DriveTrunk reads it).
        torsoTargetDeg = pose.torsoTiltTargetDeg;
        // WALK CYCLE 6s (recorder only): the pelvis pitch the pose in force owns.
        poseAskPelvisTiltTargetDeg = pose.pelvisTiltTargetDeg;
        // The pose path solves no inverse kinematics - the library holds angles
        // - so the walker's own IK-saturation witness is false by construction
        // here; PoseLibrary.ClampLeg bounds every angle to the rig's limits
        // instead, which is the analogue of the same event.
        IkSaturated = false;
        IkSolveTick = Time.frameCount;

        // One writer per joint. The library's pair is the SWING leg (the leg in
        // the air at this phase) and the STANCE leg (the one carrying the
        // weight); which side that is on the rig is the step clock's business.
        bool leftIsSwing = swingSign > 0;
        PoseLibrary.LegPose leftLeg = leftIsSwing ? pose.swingLeg : pose.stanceLeg;
        PoseLibrary.LegPose rightLeg = leftIsSwing ? pose.stanceLeg : pose.swingLeg;
        // WALK CYCLE 6s (recorder only): the six requests this step hands the PD,
        // per RIG side, before ActuatorMixer's own live-limit clamp.
        poseAskHipLDeg = leftLeg.hip;
        poseAskKneeLDeg = leftLeg.knee;
        poseAskAnkleLDeg = leftLeg.ankle;
        poseAskHipRDeg = rightLeg.hip;
        poseAskKneeRDeg = rightLeg.knee;
        poseAskAnkleRDeg = rightLeg.ankle;
        DriveOneLeg(true, leftLeg.hip, leftLeg.knee, true, leftLeg.ankle);
        DriveOneLeg(false, rightLeg.hip, rightLeg.knee, true, rightLeg.ankle);
        DriveToes(leftLeg.toe, rightLeg.toe);

        // Arms counter the opposite leg: the left arm swings with the right
        // leg, so the shoulder correlation with the same side leg stays below
        // zero by construction when the arm amplitude is non zero.
        float armPhase = Mathf.Cos(Mathf.PI * s) * Mathf.Sign(swingSign);
        float shoulderL = armSwingDeg * armPhase;
        float shoulderR = -shoulderL;
        float elbowL = -20f - 12f * Mathf.Abs(armPhase) * (armPhase > 0f ? 1f : 0f);
        float elbowR = -20f - 12f * Mathf.Abs(armPhase) * (armPhase < 0f ? 1f : 0f);
        DriveArms(shoulderL, shoulderR, elbowL, elbowR);
    }

    // Planar two link IK for the swing leg. a = thigh, b = shin, both from the
    // segment sizes, so the anatomy is not hardcoded here. The knee is the
    // vertex and it points forward: theta = phi + alpha, knee flexion = 2*alpha.
    //
    // WALK CYCLE 6q: NOT called any more - the pose library holds the joint
    // angles and solves the placement as an angle offset from a drawn pose, so
    // the walk no longer runs an inverse kinematics. Kept as the reference the
    // pose path's angles can be read against (and the rollback's own solver).
    private void SolveLegIk(float ankleX, float ankleY, float pelvisY, float pelvisTilt,
                            out float hipDeg, out float kneeDeg)
    {
        float a = human.thighSize.y;
        float b = human.shinSize.y;
        float hipY = pelvisY - human.pelvisSize.y * 0.5f;
        float dx = ankleX - pelvisBody.position.x;
        // The ankle is always below the hip: the planar solve has no branch for
        // a target above it, and a pelvis that has sunk under its own feet must
        // not hand the IK a degenerate direction.
        float dy = Mathf.Min(ankleY - hipY, -0.12f);
        float reach = Mathf.Sqrt(dx * dx + dy * dy);
        float maxReach = (a + b) * 0.995f;
        float length = Mathf.Clamp(reach, (a + b) * 0.45f, maxReach);
        // C1 witness (recorder only, read by nothing in this file): the solve had
        // to clamp. `dy` clamped means the ankle target sat above the hip's
        // 0.12 m floor - once the pelvis sinks, that is the frozen near-127 deg
        // knee target and the degenerating hip angle the walk report measured.
        // `length` clamped means the target was out of reach altogether.
        IkSaturated = ((ankleY - hipY) > -0.12f) || Mathf.Abs(length - reach) > 1e-4f;
        IkSolveTick = Time.frameCount;
        float alpha = Mathf.Acos(Mathf.Clamp(length / (a + b), 0f, 1f));
        float phi = Mathf.Atan2(dx, Mathf.Max(0.01f, -dy));
        float thighWorld = phi + alpha;
        kneeDeg = 2f * alpha * Mathf.Rad2Deg;
        // jointAngle(hip) = pelvis rotation - thigh world rotation: a thigh
        // rotated forward lowers the joint angle below the pelvis upright zero.
        hipDeg = pelvisTilt - thighWorld * Mathf.Rad2Deg;
    }

    // One leg, one writer. hipDeg NaN means "no hip angle target" (the quiet
    // stand: the pelvis-upright law owns the hip there). driveAnkle false means
    // the ankle is driven as a torque by the balance loop instead of being held
    // at an angle.
    private void DriveOneLeg(bool left, float hipDeg, float kneeDeg,
                             bool driveAnkle, float ankleDeg)
    {
        HingeJoint2D hip = left ? leftHipJoint : rightHipJoint;
        Muscle hipFlexor = left ? balance.leftHipFlexor : balance.rightHipFlexor;
        Muscle hipExtensor = left ? balance.leftHipExtensor : balance.rightHipExtensor;
        ref float hipInertia = ref (left ? ref leftHipInertia : ref rightHipInertia);
        if (!float.IsNaN(hipDeg))
            DriveJoint(hip, hipDeg, balance.hipPGain, balance.hipDGain,
                balance.errorReferenceDegrees, hipFlexor, hipExtensor, ref hipInertia);

        HingeJoint2D knee = left ? leftKneeJoint : rightKneeJoint;
        Muscle kneeFlexor = left ? balance.leftKneeFlexor : balance.rightKneeFlexor;
        Muscle kneeExtensor = left ? balance.leftKneeExtensor : balance.rightKneeExtensor;
        ref float kneeInertia = ref (left ? ref leftKneeInertia : ref rightKneeInertia);
        DriveJoint(knee, kneeDeg, balance.kneePGain, balance.kneeDGain,
            balance.errorReferenceDegrees, kneeFlexor, kneeExtensor, ref kneeInertia);

        // The ankle is a pose joint on the walk: the library's own number (plus
        // = toe down / heel rise, minus = toe up for clearance). The PD is the
        // same pair the airborne swing ankle was always held with, so no gain is
        // retuned and none is added. On the stand the ankle stays a torque loop
        // and this branch is not entered.
        if (driveAnkle)
        {
            HingeJoint2D ankle = left ? leftAnkleJoint : rightAnkleJoint;
            Muscle ankleFlexor = left ? balance.leftAnkleFlexor : balance.rightAnkleFlexor;
            Muscle ankleExtensor = left ? balance.leftAnkleExtensor : balance.rightAnkleExtensor;
            ref float ankleInertia = ref (left ? ref leftAnkleInertia : ref rightAnkleInertia);
            DriveJoint(ankle, ankleDeg, balance.kneePGain, balance.kneeDGain,
                balance.errorReferenceDegrees, ankleFlexor, ankleExtensor, ref ankleInertia);
        }
    }

    // ─── shared joint drives ───────────────────────────────────────────────
    private void DriveJoint(HingeJoint2D joint, float targetDeg, float pGain, float dGain,
                            float errorRef, Muscle flexor, Muscle extensor, ref float inertia)
    {
        if (joint == null || flexor == null || extensor == null) return;
        mixer.JointStable(joint, targetDeg, pGain, dGain, errorRef,
            flexor, extensor, ref inertia);
    }

    // Pelvis to world vertical through the hips, plus the quiet-stance hip
    // hold. The pelvis law alone leaves the hips free to splay: past
    // hipHoldBand the same signal gets the joint PD toward the pose angle, so
    // a stand cannot drift into a 44 deg lunge (p_ds 5.0 hipL +14 hipR -8,
    // CoM 0.30 m ahead of the stand frame).
    private void DriveHips(float pelvisTilt, float pelvisAngVel, float targetTilt,
                           bool driveLeft, bool driveRight,
                           float holdAngle, bool allowHold)
    {
        float error = Mathf.DeltaAngle(pelvisTilt, targetTilt)
            / Mathf.Max(1f, balance.pelvisErrorReferenceDegrees);
        float speed = pelvisAngVel / Mathf.Max(1f, balance.speedReferenceDegPerSec);
        float signal = Mathf.Clamp(
            balance.pelvisPGain * error - balance.pelvisDGain * speed, -1f, 1f);
        if (driveLeft) ApplyHipSignal(true, MixHipHold(signal, leftHipJoint, holdAngle, allowHold));
        if (driveRight) ApplyHipSignal(false, MixHipHold(signal, rightHipJoint, holdAngle, allowHold));
    }

    // Same mix as the old quiet stance: inside the band the pelvis law owns the
    // hip, past it the joint pose takes over smoothly.
    private float MixHipHold(float pelvisSignal, HingeJoint2D hip, float holdAngleDeg, bool allowHold)
    {
        if (!allowHold || hip == null) return pelvisSignal;
        float band = balance.hipHoldBand;
        if (band < 0.5f) return pelvisSignal;
        float excess = Mathf.Abs(hip.jointAngle - holdAngleDeg) - band;
        if (excess <= 0f) return pelvisSignal;
        float poseError = Mathf.DeltaAngle(hip.jointAngle, holdAngleDeg)
            / Mathf.Max(1f, balance.errorReferenceDegrees);
        float poseSpeed = hip.jointSpeed / Mathf.Max(1f, balance.speedReferenceDegPerSec);
        float pose = Mathf.Clamp(balance.hipPGain * poseError - balance.hipDGain * poseSpeed, -1f, 1f);
        float mix = Mathf.Clamp01(excess / Mathf.Max(1f, band));
        return Mathf.Clamp(pelvisSignal + pose * mix, -1f, 1f);
    }

    private void ApplyHipSignal(bool left, float signal)
    {
        Muscle flexor = left ? balance.leftHipFlexor : balance.rightHipFlexor;
        Muscle extensor = left ? balance.leftHipExtensor : balance.rightHipExtensor;
        if (flexor == null || extensor == null) return;
        mixer.ApplySignal(signal, flexor, extensor);
    }

    // Quiet ankle loop: torque against the CoM offset, not a spring to an
    // angle. Identical to the standing controller's law, scaled by the two
    // factors above.
    private void DriveAnkleBalance(float comX, float comVelX, bool leftDown, bool rightDown)
    {
        if (state == null) return;
        float offset = comX - state.supportCenterX;
        float offsetN = offset / Mathf.Max(0.01f, balance.comOffsetReference);
        float velN = comVelX / Mathf.Max(0.05f, balance.comVelocityReference);
        float command = Mathf.Clamp(
            ankleBalanceP * (balance.ankleComP * offsetN)
            + ankleBalanceD * (balance.ankleComD * velN), -1f, 1f);
        if (leftDown) DriveAnkle(true, command);
        if (rightDown) DriveAnkle(false, command);
    }

    private void DriveAnkle(bool left, float command)
    {
        HingeJoint2D ankle = left ? leftAnkleJoint : rightAnkleJoint;
        Muscle flexor = left ? balance.leftAnkleFlexor : balance.rightAnkleFlexor;
        Muscle extensor = left ? balance.leftAnkleExtensor : balance.rightAnkleExtensor;
        if (ankle == null || flexor == null || extensor == null) return;
        mixer.ApplyAnkle(ankle, command, flexor, extensor, balance.ankleLimitMargin);
    }

    private void DriveToes(float leftDeg, float rightDeg)
    {
        DriveJoint(leftToeJoint, leftDeg, balance.toePGain, balance.toeDGain,
            balance.toeErrorReferenceDegrees, balance.leftToeFlexor,
            balance.leftToeExtensor, ref leftToeInertia);
        DriveJoint(rightToeJoint, rightDeg, balance.toePGain, balance.toeDGain,
            balance.toeErrorReferenceDegrees, balance.rightToeFlexor,
            balance.rightToeExtensor, ref rightToeInertia);
    }

    private void DriveArms(float shoulderL, float shoulderR, float elbowL, float elbowR)
    {
        DriveJoint(leftShoulderJoint, shoulderL, balance.shoulderPGain, balance.shoulderDGain,
            balance.shoulderErrorReferenceDegrees, balance.leftShoulderFlexor,
            balance.leftShoulderExtensor, ref leftShoulderInertia);
        DriveJoint(rightShoulderJoint, shoulderR, balance.shoulderPGain, balance.shoulderDGain,
            balance.shoulderErrorReferenceDegrees, balance.rightShoulderFlexor,
            balance.rightShoulderExtensor, ref rightShoulderInertia);
        DriveJoint(leftElbowJoint, elbowL, balance.elbowPGain, balance.elbowDGain,
            balance.elbowErrorReferenceDegrees, balance.leftElbowFlexor,
            balance.leftElbowExtensor, ref leftElbowInertia);
        DriveJoint(rightElbowJoint, elbowR, balance.elbowPGain, balance.elbowDGain,
            balance.elbowErrorReferenceDegrees, balance.rightElbowFlexor,
            balance.rightElbowExtensor, ref rightElbowInertia);
        DriveJoint(leftWristJoint, balance.wristBaseAngle, balance.wristPGain, balance.wristDGain,
            balance.wristErrorReferenceDegrees, balance.leftWristFlexor,
            balance.leftWristExtensor, ref leftWristInertia);
        DriveJoint(rightWristJoint, balance.wristBaseAngle, balance.wristPGain, balance.wristDGain,
            balance.wristErrorReferenceDegrees, balance.rightWristFlexor,
            balance.rightWristExtensor, ref rightWristInertia);
    }

    // Chest, neck and head to world vertical. Same law as the old path: the
    // joint sits on the segment, so plus P and D enter as minus P and minus D.
    // torsoTargetDeg is the world tilt target the pose in force owns (the walk:
    // the pose library's own; the stand: lumbarTargetTilt, as before).
    private void DriveTrunk(float pelvisTilt, float pelvisAngVel, float tilt,
                            bool walking, float torsoTargetDeg)
    {
        if (vestibular == null) return;
        bool posture = walking || balance.standLegLevel < 0.5f;
        if (posture)
        {
            float lumbarError = Mathf.DeltaAngle(tilt, torsoTargetDeg)
                / Mathf.Max(1f, balance.lumbarErrorReferenceDegrees);
            float lumbarSpeed = vestibular.GetBodyAngularVelocity()
                / Mathf.Max(1f, balance.speedReferenceDegPerSec);
            float lumbarSignal = Mathf.Clamp(
                (-balance.lumbarPGain) * lumbarError - (-balance.lumbarDGain) * lumbarSpeed,
                -1f, 1f);
            mixer.ApplySignal(lumbarSignal, balance.lumbarFlexor, balance.lumbarExtensor);

            ApplyWorldUpright(vestibular.GetNeckTilt(), vestibular.GetNeckAngularVelocity(),
                neckJoint, balance.neckFlexor, balance.neckExtensor, ref neckInertia);
            ApplyWorldUpright(vestibular.GetHeadTilt(), vestibular.GetHeadAngularVelocity(),
                headJoint, balance.headFlexor, balance.headExtensor, ref headInertia);
        }
        else
        {
            DriveJoint(lumbarJoint, balance.hipBaseAngle, balance.lumbarPGain, balance.lumbarDGain,
                balance.lumbarErrorReferenceDegrees, balance.lumbarFlexor,
                balance.lumbarExtensor, ref lumbarInertia);
        }
        // pelvisTilt / pelvisAngVel stay in the signature for the trunk pose
        // that follows the pelvis once the walk keys are tuned.
    }

    private void ApplyWorldUpright(float tilt, float angVel, HingeJoint2D joint,
                                   Muscle flexor, Muscle extensor, ref float inertia)
    {
        if (joint == null || flexor == null || extensor == null) return;
        float dt = Time.fixedDeltaTime;
        float tiltForError = tilt + angVel * dt;
        float damping = 0f;
        if (inertia <= 0f) inertia = ActuatorMixer.EffectiveInertia(joint);
        if (inertia > 0f)
        {
            float k = balance.neckDGain * flexor.maxTorque * Mathf.Rad2Deg
                      / Mathf.Max(1f, balance.speedReferenceDegPerSec);
            damping = k * dt / inertia;
        }
        float error = Mathf.DeltaAngle(tiltForError, balance.neckTargetTilt)
            / Mathf.Max(1f, balance.neckErrorReferenceDegrees);
        float speed = angVel / Mathf.Max(1f, balance.speedReferenceDegPerSec);
        float raw = (-balance.neckPGain) * error - (-balance.neckDGain) * speed;
        float signal = Mathf.Clamp(raw / (1f + damping), -1f, 1f);
        mixer.ApplySignal(signal, flexor, extensor);
    }

    private void RelaxAll()
    {
        ActuatorMixer.Zero(balance.leftHipFlexor, balance.leftHipExtensor);
        ActuatorMixer.Zero(balance.rightHipFlexor, balance.rightHipExtensor);
        ActuatorMixer.Zero(balance.leftKneeFlexor, balance.leftKneeExtensor);
        ActuatorMixer.Zero(balance.rightKneeFlexor, balance.rightKneeExtensor);
        ActuatorMixer.Zero(balance.leftAnkleFlexor, balance.leftAnkleExtensor);
        ActuatorMixer.Zero(balance.rightAnkleFlexor, balance.rightAnkleExtensor);
        ActuatorMixer.Zero(balance.lumbarFlexor, balance.lumbarExtensor);
        ActuatorMixer.Zero(balance.leftShoulderFlexor, balance.leftShoulderExtensor);
        ActuatorMixer.Zero(balance.rightShoulderFlexor, balance.rightShoulderExtensor);
    }

    // ─── lookups and command line ──────────────────────────────────────────
    private static HingeJoint2D FindJoint(Transform root, string name)
    {
        Transform found = root.Find(name);
        return found != null ? found.GetComponent<HingeJoint2D>() : null;
    }

    private static Rigidbody2D FindBody(Transform root, string name)
    {
        Transform found = root.Find(name);
        return found != null ? found.GetComponent<Rigidbody2D>() : null;
    }

    // This file reads its own switches: the frozen -walk surface is not touched
    // and the old path needs no edit to stay switchable.
    private static float GetFloat(string name, float fallback)
    {
        string[] args = System.Environment.GetCommandLineArgs();
        int index = System.Array.IndexOf(args, name);
        if (index < 0 || index + 1 >= args.Length) return fallback;
        return float.TryParse(args[index + 1], NumberStyles.Float,
            CultureInfo.InvariantCulture, out float value) ? value : fallback;
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// LEVEL 2 POSE LIBRARY (walk cycle 6q) — the walk's shapes as DATA, in degrees.
//
// Conventions are the rig's own (Assets/Human.cs, CreateLeg):
//   hip    minus = flexion, the thigh forward                 limits -90 .. +45
//   knee   0 = straight, plus = flexion                        limits   0 .. +120
//   ankle  jointAngle = heel - toe: plus = toe down / heel rise
//          (plantarflexion), minus = toe up (dorsiflexion)          -45 .. +45
//   toe    MTP: minus = toe up (swing clearance), plus = toe down     -35 .. +50
//   trunk  pelvisTiltDeg / torsoTiltDeg = world tilt targets, minus = forward
//          (the recorder's pelvisTilt sign, VestibularSystem.GetPelvisTilt)
// The ankle pair is the literal pair of the heel.ConnectTo call (Human.cs:555);
// Human.cs names no field for it, so the limit travels in Inputs.ankleLimit.
//
// Every number in the table is inside those limits - the widest is the swing
// fold's hip -30 against the -90 stop - and ActuatorMixer.JointStable clamps
// every target to the LIVE joint limits a second time, so a mistyped pose can
// never drive a joint onto a stop. ClampLeg below re-clamps in the library for
// the same reason.
//
// A POSE owns both legs' hip / knee / ankle / toe in degrees, the pelvis tilt
// target and the torso (lumbar) tilt target. The step clock picks the pose pair
// and blends between the keys (Blend):
//   stance      0.00  the pose the walk leaves the ground from (hipBaseAngle /
//                     kneeBaseAngle: the pose the verified quiet stand holds)
//   launch      0.12  the first push: the stance leg extends and rolls onto the
//                     toe, the swing knee starts to fold
//   swing fold  0.35  the leg folded under the body (Yury 18.09 "Swing leg must
//                     fold": knee 60..80 deg, the foot passes under the pelvis)
//   foot place  the swing's landing share (0.80): the knee back under 20 deg
//                     before the plant, the foot carried to the placement
//   plant/load  1.00  the foot is down and takes the load, the rear leg pushes
//
// THE LAW (Sample): the balance layer hands over two requests it owns, both in
// METRES - where the foot lands and how high the hip is held - plus the pelvis
// state. This class returns every joint angle, with three terms on top of the
// drawn pose:
//   1. the clearance request (-poseLift, m, measured at the SOLE) may only RAISE
//      the drawn fold, and the drawing's 60..80 deg band caps the knee it may
//      ask for;
//   2. the placement: the error between where the drawn pose would put the foot
//      and where the balance layer says it belongs becomes a swing HIP angle -
//      1 deg of hip moves the ankle by the pose's own hip-to-ankle drop, so the
//      gain is a property of the pose, not a tuned number;
//   3. the hip height: the same loop the quiet stand is verified on (gain, damp
//      and the pelvis vertical rate - the constants come from the controller and
//      are NOT retuned here), trimmed onto the knee and bounded to
//      HeightTrimBandDeg, so the request can never rewrite the pose. WALK CYCLE
//      6w: the request handed in is the DATUM of the step in force (the height it
//      started from, latched at its swap, the stand frame as ceiling), so the
//      error this loop trims is the step's own sink - not a stand constant the
//      walk has already left behind (6u) and not the live height the error is
//      measured against (6v, which made the error identically zero);
//   4. the trunk hold: the pelvis tilt error and rate against the pose's own
//      pelvis target become a HIP ANGLE on the stance leg - the leg that can
//      move the trunk - with the signs of the verified pelvis law (a positive
//      hip angle is extensor work, the correction for a back-tipped pelvis),
//      bounded to TrunkBandDeg.
// Nothing here reads a torque, a gain or an actuator: it is a function from
// metres and degrees to degrees.
public static class PoseLibrary
{
    // One leg's pose. All four in degrees, joint conventions above.
    public struct LegPose
    {
        public float hip;
        public float knee;
        public float ankle;
        public float toe;
    }

    // One named pose: the leg in the air, the leg carrying the weight, and the
    // trunk targets the same pose owns.
    public struct Pose
    {
        public string name;
        public LegPose swing;
        public LegPose stance;
        public float pelvisTiltDeg;
        public float torsoTiltDeg;
    }

    // Everything the library is given. The two metre-valued entries are the
    // balance layer's own decisions; everything else is a measurement or a rig
    // constant, and nothing here is a gain the library tunes.
    public struct Inputs
    {
        public float swingPhase;         // 0..1 of the swing: stepClock/swingDuration
        public float supportNeed;        // BALANCE LAYER, WALK CYCLE 6t: the CoM's own
                                         //   lead since the swap as a share of
                                         //   comLeadSwapMeters. 0 = the CoM has not
                                         //   moved (the clearance ask and the drawn
                                         //   fold own the descent), 1 = the CoM has
                                         //   taken the lead the swap rule calls a
                                         //   swap and the sole is asked for the floor
        public float landingShare;       // the swing share at which the foot is down
        public float footPlaceX;         // BALANCE LAYER: where the next foot lands, m
        public float hipHeightM;         // live pelvis height above the ground, m
        public float hipHeightTargetM;   // BALANCE LAYER: the height it holds, m
                                         //   (6w: the datum of the step in force -
                                         //   latched at the swap that started it,
                                         //   stand frame as ceiling - so it is
                                         //   CONSTANT inside the step and
                                         //   x.hipHeightM minus it is the step's
                                         //   own sink; 6v handed in the live
                                         //   height, which zeroed that error)
        public float pelvisX;            // pelvis X (the pose is hip-relative), m
        public float pelvisTiltDeg;      // measured pelvis world tilt, minus = forward
        public float pelvisAngVelDeg;    // pelvis rate, same sign as the tilt, deg/s
        public float pelvisVelY;         // pelvis vertical rate, plus = rising, m/s
        public float thighM;             // segment lengths, m
        public float shinM;
        public float soleBelowAnkleM;    // ankle hinge height above the sole, m
                                         //   (Human.cs: the ankle sits at the
                                         //   centre of the heel plate, so the
                                         //   sole is half a foot below it)
        public float swingLiftM;         // the SOLE clearance the swing foot is
                                         //   asked for, m (-poseLift)
        public float heightGainDegPerM;  // the stand's verified height pair, as it is
        public float heightDampDegPerMps;
        public float hipMin, hipMax;     // Human.cs limits (pose clamp + the table's
        public float kneeMin, kneeMax;   // own audit), degrees
        public float ankleLimit;         // symmetric ankle pair, Human.cs:555
        public float toeMin, toeMax;
    }

    // Every joint angle of the step, plus the trunk targets the same pose owns.
    // The witnesses (poseIndex .. trunkCorrDeg) are recorded, never read back.
    public struct Targets
    {
        public LegPose swingLeg;
        public LegPose stanceLeg;
        public float pelvisTiltTargetDeg;
        public float torsoTiltTargetDeg;
        public int poseIndex;
        public string poseName;
        public float nominalAnkleXM;
        public float clearanceM;
        public float placementErrorM;
        public float placementDeg;
        public float heightTrimDeg;
        public float trunkCorrDeg;
    }

    // ─── step-clock keys (landing key = Inputs.landingShare) ───────────────
    public const float LaunchKey = 0.12f;
    public const float FoldKey = 0.35f;

    // ─── the law's own constants ───────────────────────────────────────────
    // The swing fold's band, from the drawing (Yury 18.09, "Swing leg must
    // fold": the knee bends 60 to 80 deg while the foot is off the ground).
    public const float FoldKneeMinDeg = 60f;
    public const float FoldKneeMaxDeg = 80f;
    // WALK CYCLE 6t: the knee the descent may never straighten past - the drawn
    // footplace pose's own 20 deg. The descent's ask carries the sole down onto
    // the floor, and this ceiling is what keeps the ordering Yury's swing rule
    // asks for: the leg is under 20 deg before the sole lands, and the fold band
    // above still owns every pose the foot has to clear in.
    public const float PlaceKneeCeilDeg = 20f;
    // The trunk hold: how much hip angle answers one degree of pelvis tilt
    // error, and how much answers one deg/s of pelvis rate. First-pass numbers
    // of the pose path (this cycle measures the off state only); they are the
    // angle form of pelvisPGain 2.0 / pelvisDGain 0.3, bounded to TrunkBandDeg
    // so a tipped body cannot rewrite the pose.
    public const float TrunkGainDegPerDeg = 0.6f;
    public const float TrunkDampDegPerDegPerSec = 0.02f;
    public const float TrunkBandDeg = 12f;
    // The hip-height request's own bound (the height pair itself - gain, damp -
    // is the controller's verified stand pair and is passed in, not retuned).
    public const float HeightTrimBandDeg = 12f;

    // ─── the five poses ────────────────────────────────────────────────────
    // stance: the quiet stand pose the walk starts from - hipBaseAngle -3 /
    //   kneeBaseAngle 6 of BalanceController, i.e. the pose the verified quiet
    //   stance holds, both legs equal.
    public static readonly Pose Stance = Make("stance",
        Leg(-3f, 6f, 0f, 0f), Leg(-3f, 6f, 0f, 0f), 0f, 0f);

    // launch (the first push): the stance leg extends (-3 -> +6 hip) and rolls
    //   onto the toe (+14 ankle, +10 toe), the swing knee folds from 6 to 28 as
    //   the foot leaves, the toe lifts for clearance (toe -12).
    public static readonly Pose Launch = Make("launch",
        Leg(-16f, 28f, -6f, -12f), Leg(6f, 8f, 14f, 10f), -2f, -1f);

    // swing fold: the leg folded under the body - the drawing's band, knee 68
    //   (60..80), the thigh 30 deg forward of the pelvis, the toe up. The stance
    //   leg is nearly straight (knee 10) with the body passing over it.
    public static readonly Pose SwingFold = Make("swingfold",
        Leg(-30f, 68f, -10f, -14f), Leg(-6f, 10f, 4f, 2f), 0f, 0f);

    // foot place: the knee is back under 20 deg before the plant and the thigh
    //   carries the leg forward to the placement; the stance leg has begun to
    //   roll onto the toe for the push that follows.
    public static readonly Pose FootPlace = Make("footplace",
        Leg(-22f, 20f, -8f, -6f), Leg(2f, 12f, 8f, 6f), 0f, 0f);

    // plant / load: the just-landed leg takes the weight - the knee softens to
    //   24 (the load is absorbed, then the leg stands him up) - and the rear
    //   leg is at its push-off pose. The `swing` leg is the FRONT leg here: it
    //   landed at the previous key and the roles swap at the next step clock.
    public static readonly Pose PlantLoad = Make("plantload",
        Leg(-16f, 24f, -2f, 0f), Leg(10f, 10f, 16f, 12f), -1f, 0f);

    // Keyed in clock order: Stance 0.00, Launch 0.12, SwingFold 0.35, FootPlace
    // at the landing share, PlantLoad 1.00.
    public static readonly Pose[] WalkCycle = { Stance, Launch, SwingFold, FootPlace, PlantLoad };
    public static int PoseCount { get { return WalkCycle.Length; } }
    public static string PoseName(int index)
    {
        return index >= 0 && index < WalkCycle.Length ? WalkCycle[index].name : "";
    }

    private static LegPose Leg(float hip, float knee, float ankle, float toe)
    {
        LegPose l;
        l.hip = hip;
        l.knee = knee;
        l.ankle = ankle;
        l.toe = toe;
        return l;
    }

    private static Pose Make(string name, LegPose swing, LegPose stance,
                             float pelvisDeg, float torsoDeg)
    {
        Pose p;
        p.name = name;
        p.swing = swing;
        p.stance = stance;
        p.pelvisTiltDeg = pelvisDeg;
        p.torsoTiltDeg = torsoDeg;
        return p;
    }

    // ─── the clock: which pose, and how much of it ─────────────────────────
    public static Pose Blend(float phase, float landingShare)
    {
        float land = Mathf.Clamp(landingShare, FoldKey + 0.05f, 0.98f);
        if (phase <= LaunchKey)
            return Mix(Stance, Launch, phase / LaunchKey);
        if (phase <= FoldKey)
            return Mix(Launch, SwingFold, (phase - LaunchKey) / (FoldKey - LaunchKey));
        if (phase <= land)
            return Mix(SwingFold, FootPlace, (phase - FoldKey) / (land - FoldKey));
        return Mix(FootPlace, PlantLoad, Mathf.Clamp01((phase - land) / (1f - land)));
    }

    private static Pose Mix(Pose a, Pose b, float t)
    {
        t = Mathf.Clamp01(t);
        t = t * t * (3f - 2f * t);           // smoothstep: no step change at a key
        Pose p;
        p.name = t < 0.5f ? a.name : b.name;
        p.swing = MixLeg(a.swing, b.swing, t);
        p.stance = MixLeg(a.stance, b.stance, t);
        p.pelvisTiltDeg = Mathf.Lerp(a.pelvisTiltDeg, b.pelvisTiltDeg, t);
        p.torsoTiltDeg = Mathf.Lerp(a.torsoTiltDeg, b.torsoTiltDeg, t);
        return p;
    }

    private static LegPose MixLeg(LegPose a, LegPose b, float t)
    {
        LegPose l;
        l.hip = Mathf.Lerp(a.hip, b.hip, t);
        l.knee = Mathf.Lerp(a.knee, b.knee, t);
        l.ankle = Mathf.Lerp(a.ankle, b.ankle, t);
        l.toe = Mathf.Lerp(a.toe, b.toe, t);
        return l;
    }

    private static int NearestKey(float phase, float land)
    {
        // No array and no allocation: this runs once per physics step of a pose
        // walk, and the project's perf rule is no per-step garbage.
        int best = 0;
        float bestGap = Mathf.Abs(phase);
        float gap = Mathf.Abs(phase - LaunchKey);
        if (gap < bestGap) { bestGap = gap; best = 1; }
        gap = Mathf.Abs(phase - FoldKey);
        if (gap < bestGap) { bestGap = gap; best = 2; }
        gap = Mathf.Abs(phase - land);
        if (gap < bestGap) { bestGap = gap; best = 3; }
        gap = Mathf.Abs(phase - 1f);
        if (gap < bestGap) { best = 4; }
        return best;
    }

    // ─── the law ───────────────────────────────────────────────────────────
    public static Targets Sample(Inputs x)
    {
        Targets t = new Targets();
        Pose p = Blend(x.swingPhase, x.landingShare);
        float thigh = Mathf.Max(0.05f, x.thighM);
        float shin = Mathf.Max(0.05f, x.shinM);
        float land = Mathf.Clamp(x.landingShare, FoldKey + 0.05f, 0.98f);

        // The drawn pose, hip-relative. thighWorld / shinWorld are world angles
        // from vertical with forward plus; the hip's own joint angle is minus
        // flexion and the knee folds the shin back off the thigh, so
        // shinWorld = thighWorld - knee.
        float thighWorld = -p.swing.hip;
        float shinWorld = thighWorld - p.swing.knee;
        float thighRad = thighWorld * Mathf.Deg2Rad;
        float shinRad = shinWorld * Mathf.Deg2Rad;
        t.nominalAnkleXM = thigh * Mathf.Sin(thighRad) + shin * Mathf.Sin(shinRad);
        float drop = thigh * Mathf.Cos(thighRad) + shin * Mathf.Cos(shinRad);
        // The clearance the drawn swing pose gives: the SOLE's height above the
        // ground while the hip is held at the height the balance layer asks for
        // (the ankle hinge sits `soleBelowAnkleM` above the sole).
        t.clearanceM = x.hipHeightM - drop - x.soleBelowAnkleM;

        // 1. the sole's own height. Two asks through the same solve, and the CoM
        //    decides which is in force:
        //    - the clearance the foot is asked to keep while the CoM does NOT
        //      need the support (-poseLift, m). Unchanged: it may only RAISE the
        //      drawn fold, and the drawing's band caps the knee it may ask for
        //      (Yury 18.09: 60-80 deg while the foot is off the ground).
        //    - WALK CYCLE 6t: the DESCENT onto the floor as the CoM does need it
        //      (x.supportNeed = the CoM's own lead since the swap, the quantity
        //      the swap decision reads). The rate is the CoM's, not the step
        //      clock's: the sole is placed when the new support is needed instead
        //      of waiting for the sinking pelvis to meet the drawn angles. This
        //      ask may only STRAIGHTEN the leg and never past the placement
        //      pose's own knee, so it stays under 20 deg before the sole lands.
        float swingKnee = p.swing.knee;
        float clearanceAsk = x.swingLiftM * (1f - x.supportNeed);
        if (clearanceAsk > 0.001f)
        {
            float asked = KneeForDrop(
                x.hipHeightM - x.soleBelowAnkleM - clearanceAsk, thighWorld,
                thigh, shin, p.swing.knee);
            asked = Mathf.Clamp(asked, FoldKneeMinDeg, FoldKneeMaxDeg);
            float weight = FoldWeight(x.swingPhase, land);
            if (asked > swingKnee)
                swingKnee += (asked - swingKnee) * weight;
        }
        else
        {
            float asked = KneeForDrop(
                x.hipHeightM - x.soleBelowAnkleM, thighWorld,
                thigh, shin, p.swing.knee);
            asked = Mathf.Clamp(asked, x.kneeMin, PlaceKneeCeilDeg);
            float weight = SmoothWindow(x.swingPhase, FoldKey, land) * x.supportNeed;
            if (asked < swingKnee)
                swingKnee += (asked - swingKnee) * weight;
        }

        // 2. the placement: the balance layer's foot position against the pose's
        //    own. 1 deg of hip moves the ankle by the pose's hip-to-ankle drop,
        //    so the gain is the pose's own geometry.
        t.placementErrorM = x.footPlaceX - (x.pelvisX + t.nominalAnkleXM);
        float placeWeight = SmoothWindow(x.swingPhase, FoldKey, land);
        float placeGain = Mathf.Rad2Deg / Mathf.Max(0.05f, drop);
        t.placementDeg = -placeGain * t.placementErrorM * placeWeight;

        // 3. the hip height the balance layer holds: the verified stand loop's
        //    own pair, trimmed onto the knee and bounded. The stance leg holds
        //    the height outright; the swing leg is trimmed by the same request as
        //    it comes down, so the foot meets the ground instead of hanging.
        float heightErrorM = x.hipHeightTargetM - x.hipHeightM;   // plus = the hip is low
        float trim = -(x.heightGainDegPerM * heightErrorM
                       + x.heightDampDegPerMps * x.pelvisVelY);
        t.heightTrimDeg = Mathf.Clamp(trim, -HeightTrimBandDeg, HeightTrimBandDeg);
        float swingHeightWeight = SmoothWindow(x.swingPhase, FoldKey, land);

        // 4. the trunk hold against the pose's own pelvis target. Same signs as
        //    the verified pelvis law: a positive hip angle is extensor work, and
        //    a pelvis that has gone forward of its target (error positive) is
        //    answered by extension.
        float pelvisErrorDeg = Mathf.DeltaAngle(x.pelvisTiltDeg, p.pelvisTiltDeg);
        t.trunkCorrDeg = Mathf.Clamp(
            TrunkGainDegPerDeg * pelvisErrorDeg
            - TrunkDampDegPerDegPerSec * x.pelvisAngVelDeg,
            -TrunkBandDeg, TrunkBandDeg);

        // The six joints. One owner: the library.
        t.swingLeg = p.swing;
        t.swingLeg.hip += t.placementDeg;
        t.swingLeg.knee = swingKnee + t.heightTrimDeg * swingHeightWeight;
        t.stanceLeg = p.stance;
        t.stanceLeg.hip += t.trunkCorrDeg;
        t.stanceLeg.knee += t.heightTrimDeg;
        t.pelvisTiltTargetDeg = p.pelvisTiltDeg;
        t.torsoTiltTargetDeg = p.torsoTiltDeg;
        t.poseIndex = NearestKey(x.swingPhase, land);
        t.poseName = PoseName(t.poseIndex);
        t.swingLeg = ClampLeg(t.swingLeg, x);
        t.stanceLeg = ClampLeg(t.stanceLeg, x);
        return t;
    }

    // The knee flexion at which the hip-to-ankle drop is `dropM` with the thigh
    // held at `thighWorldDeg` - the chain's own law
    //   drop = thigh*cos(tw) + shin*cos(tw - knee)
    // inverted. Cosine is even, so a drop has two knees: a nearly straight leg
    // and a folded one. `branchKneeDeg` (the drawn pose's own knee) picks the
    // branch, so a fold stays a fold and an extended leg stays extended.
    private static float KneeForDrop(float dropM, float thighWorldDeg, float thigh,
                                     float shin, float branchKneeDeg)
    {
        float tw = thighWorldDeg * Mathf.Deg2Rad;
        float c = Mathf.Clamp((dropM - thigh * Mathf.Cos(tw)) / Mathf.Max(0.01f, shin),
            -1f, 1f);
        float shinWorld = Mathf.Acos(c) * Mathf.Rad2Deg;
        float folded = thighWorldDeg + shinWorld;
        float straight = thighWorldDeg - shinWorld;
        return branchKneeDeg > (folded + straight) * 0.5f ? folded : straight;
    }

    // How much of the fold the clock is at: 0 at the launch key, 1 at the fold
    // key, back to 0 at the landing.
    private static float FoldWeight(float phase, float land)
    {
        if (phase <= FoldKey)
        {
            float u = Mathf.Clamp01((phase - LaunchKey) / (FoldKey - LaunchKey));
            return u * u * (3f - 2f * u);
        }
        float v = Mathf.Clamp01((phase - FoldKey) / Mathf.Max(0.05f, land - FoldKey));
        return 1f - v * v * (3f - 2f * v);
    }

    private static float SmoothWindow(float phase, float from, float to)
    {
        float u = Mathf.Clamp01((phase - from) / Mathf.Max(0.05f, to - from));
        return u * u * (3f - 2f * u);
    }

    // The library's own last word on a pose: every angle inside the rig's own
    // limits, so a pose is reachable by construction even before
    // ActuatorMixer.JointStable clamps the target to the live joint limits.
    private static LegPose ClampLeg(LegPose l, Inputs x)
    {
        l.hip = Mathf.Clamp(l.hip, x.hipMin, x.hipMax);
        l.knee = Mathf.Clamp(l.knee, x.kneeMin, x.kneeMax);
        l.ankle = Mathf.Clamp(l.ankle, -x.ankleLimit, x.ankleLimit);
        l.toe = Mathf.Clamp(l.toe, x.toeMin, x.toeMax);
        return l;
    }
}
