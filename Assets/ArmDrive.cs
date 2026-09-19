using UnityEngine;

// Arm drive. Yury's rule: the arms work the vestibular apparatus, they are not
// decoration. This component owns the six arm muscle activations (flexor and
// extensor on both shoulders and both elbows) and writes them once per physics
// step: after BalanceController (order 0), before ActuatorDriver applies the
// muscles (order 50). Every other joint, and the wrist, stays with the balance
// controller.
//
// Two jobs:
//
//  1. WALK. A reciprocal swing built from the measured hip angles, not from a
//     clock, so it holds on any gait this rig produces. The left shoulder is
//     driven against the RIGHT thigh and the right shoulder against the LEFT one
//     (the acceptance pairing), plus a smaller same-side term so that the two
//     arms stay in counter phase with each other even in a window where the legs
//     move together. The phase comes from the hips, the amplitude from the
//     balance controller's own swing range: no hardcoded sinusoid.
//
//  2. FALL. Per side: fold the arm down onto the slab, latch the palm's world
//     point, then press. Measured (p_arms20): with the target chasing the
//     shoulder the arm walked its hand along the floor — 191 strokes of 0.18 s,
//     2.7 deg of elbow extension, the palm 0.9 m in the air on the left side.
//     A press needs a FIXED palm: the aim holds the upper arm on the latched
//     point while the elbow extends, the arm becomes a strut, and the only way
//     the chain can give is the shoulder leaving that point — the body rising.
//     Real contact (Collider2D.IsTouchingLayers) starts the stroke and holds it:
//     a stroke is latched for pressMinStrokeSeconds and only a contact loss that
//     lasts pressLostContactSeconds may abandon it.
//
//     Two rules were measured wrong on p_arms39/40 and are now the opposite:
//     (1) the latched palm sits UNDER the shoulder (plantLateralShare 0), not
//     beside it — a side plant spends the stroke on a horizontal shove (the body
//     was pushed 2.0 m along the slab, pelvis never leaving -1.86); (2) the
//     stroke deepens the latched point (pressStrokeMode 1) instead of ramping the
//     elbow angle, because an elbow ramp leaves the hand's position uncontrolled:
//     the hand was measured 0.39 m past its own palm, walking along the floor.
//
//     Two more failed on p_arms50 and are now measured the other way:
//     (3) "under the shoulder" is only half of it — the point must be a CHORD the
//     arm can hold torque at. A lying shoulder 0.11 m over the slab latched a
//     point 0.11 m away against a 0.218 m shortest usable chord: the solve
//     clamped, the hand settled 0.32 m away from its own palm and the elbow
//     "extended" 60 deg into the air (shoulder rise 0.03 m). The offset below is
//     sqrt(reach^2 - drop^2), i.e. the palm is directly under the shoulder
//     whenever that is reachable and at the arm's own minimum chord when it is
//     not; (4) Hold must end when the elbow stops extending while the body is
//     still down, otherwise one plant spends the whole fall (5775 of 6447 fallen
//     frames in Hold, 2 strokes, no rise).
//
// Sign convention of this rig, checked against the recorded columns: jointAngle
// negative = flexion = limb forward. A forward-swinging thigh has a NEGATIVE hip
// jointAngle and a forward-swung arm has a NEGATIVE shoulder angle. A positive
// activation signal increases jointAngle (extensor work).
//
// Interface for the get-up, which lives in BalanceController (not this file):
//
//     armDrive.RequestArmPress(intensity);   // 0..1, call every step while GettingUp
//
// Returns true when both palms are in real contact; the return value is a
// convenience, the caller may ignore it. A request older than
// pressRequestHoldSeconds is dropped, so a get-up that stops asking cannot leave
// the arms latched. Per-side read-back for the get-up and the recorder:
// HandContactL/R, PressPhaseL/R (0 idle 1 lift 2 press 3 hold 4 plant),
// PressStrokeExtensionL/R, PressShoulderLiftL/R, PressLiftMax, PressStrokesL/R.
[DefaultExecutionOrder(10)]
public class ArmDrive : MonoBehaviour
{
    [Header("Walk swing (deg)")]
    // Shoulder travel at |phase| = 1. Human walking swing is 20-30 deg.
    public float walkShoulderSwing = 34f;
    // Elbow: mean flexion plus the swing around it, degrees from elbowBaseAngle.
    public float walkElbowMid = 12f;
    public float walkElbowAmp = 16f;
    // Phase mix. crossWeight > ipsWeight is the point: the shoulder is dominated
    // by the OPPOSITE thigh (counter phase, correlation below -0.5), while the
    // same-side term keeps the two arms in counter phase with each other. The
    // ratio is a real trade-off, not a taste: in a window where the two thights
    // move together (corr(hipL,hipR) +0.9) "shoulder against the opposite thigh"
    // and "the two shoulders against each other" cannot both be strong, and the
    // measured optimum for BOTH being negative is cross/ips = 2 (t = 2).
    public float crossWeight = 1f;
    public float ipsWeight = 0.5f;
    // The right shoulder's own cross weight, i.e. the ratio is per side
    // (crossWeight/ipsWeight = 2 on the left, this one on the right). Rewritten
    // 19.09: the citations this block used to carry (a p_arms50 pair
    // "-0.658 / -0.418" and corr(dL,hipR) +0.36 vs corr(dR,hipL) +0.56) do NOT
    // reproduce from the runs they named, so they are gone rather than patched
    // (the audit is Trials/Runs/programmer-cycle-1909-1900-arm-default-claim.md and
    // -2010-arm-build-identity.md). What does reproduce, re-read this cycle with
    // `Tools/arm-pairing-check.py` on the arms5 build (fingerprint 0fd4f288), the
    // walk's own measured shoulders against the thighs, "measured" = the joint
    // columns, "drive" = the drive's own targets:
    //
    //     window 5.00-6.63 s (accepted, n = 327)   window 5.00-7.30 s
    //     p_arms81  per-side 1.5:  -0.848/-0.749 measured, -0.868/-0.866 drive
    //                                              -0.696/-0.628
    //     p_arms75  one ratio 1.0: -0.842/-0.610 measured, -0.868/-0.732 drive
    //                                              -0.657/-0.413  <- fails here
    //
    // So ONE shared ratio holds BOTH acceptance pairings in the accepted window
    // (the earlier "with ONE ratio the two pairings cannot both go below -0.5" is
    // not in the record), and breaks the right one in the wider window. The
    // default therefore stands only because the accepted evidence was measured on
    // it -- an A/B on the current build (`-armCrossR 1` against the default, two
    // runs) is the measurement this field still owes, and it is Hermes' series.
    public float crossWeightR = 1.5f;
    // +1: left shoulder against the right thigh (the acceptance pattern).
    // -1 flips to the same-side pairing, for a measurement.
    public float swingSign = 1f;
    public float walkRampSeconds = 0.30f;
    // Hip joint speed that counts as "the legs are working a gait".
    public float walkHipSpeedDegPerSec = 25f;
    public float walkHoldSeconds = 0.50f;
    // Running mean of each hip angle (the swing rides the oscillation, not the
    // fold) and the shared scale the deviation is divided by.
    public float hipStatsTau = 0.6f;
    public float minHipVarianceDeg2 = 4f;
    // Tighter tracking while walking: the recorded column must follow the drive.
    public float walkPGainScale = 4f;
    public float walkDriveSlewPerSecond = 60f;

    [Header("Fall press")]
    public float pushEnabled = 1f;
    public float fallTiltDeg = 45f;
    // The press is a closed chain, and it only works if the palm is a FIXED
    // point: the hand target is latched in the world when the palm lands and is
    // not moved again until the stroke ends. Measured p_arms20 with a target
    // that followed the shoulder: 191 strokes per side, 2.7 deg of elbow
    // extension, the arm walked its hand along the slab and the body never rose.
    // The stroke runs Press (elbow extends, shoulder aimed at the latched
    // point) -> Hold -> Lift (palm unloaded, arm folds again) -> Plant (reach
    // the next latched point, the stroke starts on real contact).
    // Plant geometry: the palm lands on the slab at the SMALLEST lateral offset
    // the arm can manage — the closer the palm is to under the shoulder, the more
    // of the elbow extension goes into lifting the body instead of shoving it
    // sideways. The offset is the horizontal leg of the shortest chord the folded
    // arm can make: with the elbow at its flexion stop the arm spans
    // sqrt(L1^2+L2^2-2 L1 L2 cos interior), and anything shorter is unreachable.
    // Measured p_arms21 with a fixed 0.30 m offset: the push left the slab at
    // 57 deg off vertical, the palm slipped and the shoulder rose 70 mm.
    public float plantSideMax = 0.35f;
    // Extra chord over the arm's own shortest usable one that a latched point is
    // kept at, metres. The plant is a CHORD question, not a posture question:
    // measured p_arms50 (plantLateralShare 0), a lying shoulder 0.09-0.12 m over
    // the slab latched the point straight under itself, 0.11 m away against an arm
    // whose shortest usable chord is 0.218 m. The solve clamped, the hand settled
    // 0.32 m away from its own palm, the elbow "extended" 60 deg into the air and
    // the shoulder rose 0.03 m. NEGATIVE values are legal and mean "the chord may
    // be shorter than the arm's own minimum", i.e. the palm goes straight under
    // the shoulder and the solve presses it INTO the slab by the difference --
    // that is the load a press wants. Measured 19.09 on the -165 elbow stop
    // (p_arms70 = the old rig, press on, 0.03): a stroke opened the elbow 40-80
    // deg and moved the shoulder pivot 0.005 m while shoving the body 2.08 m.
    public float plantReachMargin = 0.03f;
    // How much of the latched point's lateral offset is filled by the chord rule.
    // 0 = the palm goes under the shoulder (the press posture); 1 = the former
    // lateral-fill rule, kept so the two can be compared on one build.
    public float plantLateralShare = 0f;
    // Interior angle the plant starts the elbow at: the room the stroke has to
    // extend into, 65 deg = about 115 deg of flexion left to spend, and the
    // elbow target it asks for (-115) sits well inside the -150..0 hinge.
    // Measured p_arms34 with the former "shortest chord the arm can make" rule:
    // a lying shoulder 0.11 m over the slab latched a 0.11 m chord against an
    // arm whose minimum chord is 0.218, so the solve asked for an 18 deg
    // interior angle (the stop is 30), the elbow pinned on the stop and the
    // "stroke" threw the hand 0.66 m into the air while counting 126 deg of
    // "extension" -- extension of a joint that never had the palm loaded.
    // Margin in degrees of elbow interior angle kept above the joint's own stop
    // when a plant point is chosen. The stroke starts AT this fold, so the margin
    // is the elbow torque the stroke has at t=0: too small and the press begins
    // against the stop (p_arms34, 8 deg margin: plantFold -142, 1.6 mm of rise).
    public float plantStopMarginDeg = 25f;
    public float plantFoldMarginDeg = 8f;
    public float plantReachSeconds = 1.2f;
    // Palm centre this far below the slab top: the collider is pressed onto the
    // ground rather than resting on it, which is what makes IsTouchingLayers
    // fire (a geometric slop test does not).
    public float handGroundSink = 0.02f;
    // The stroke drives the latched palm point this far INTO the slab. A palm
    // that is merely asked to stay where it is needs no torque; one that is
    // asked to go through the floor loads the chain, and the block is then the
    // ground, so the only thing that can give is the shoulder: the body rises.
    // Measured p_arms34 with an unloaded latched point: the elbow opened 125 deg
    // in 0.11 s, the hand rose 0.66 m off the slab and the shoulder rose 1.6 mm.
    public float pressDepth = 0.04f;
    // Stroke mechanism. 1 = the latched point itself is driven deeper into the
    // slab and BOTH joints keep solving onto it, so the hand stays on its own
    // palm and the giving link is the shoulder; 0 = the former elbow-angle ramp,
    // which measured p_arms40 let the hand walk 0.39 m past the palm it was
    // supposed to press on.
    public float pressStrokeMode = 1f;
    // Extra depth the latched point gains over one stroke, m, and the rate it
    // gains it at, m/s. The chord is capped by the arm's own length, so this is
    // the room a stroke has to press into.
    public float pressDepthExtra = 0.14f;
    public float pressDepthRate = 0.09f;
    public float pressElbowTarget = -4f;
    // Rate limit on the stroke's elbow goal, deg/s (0 = off). Was off by default
    // on the assumption that "with the palm loaded the slab itself limits the
    // rate" -- measured p_arms34/36, it does not: an arm whose shoulder can rotate
    // freely just throws the HAND, so the elbow snapped 110 deg in 0.1 s (1000
    // deg/s) and the hand left the slab it had been gripping the step before. With
    // the goal ramping at this rate the extension can only be paid for by the
    // shoulder moving, which is what a press is.
    public float pressStrokeDegPerSecond = 60f;
    // The stroke has done its work when the elbow stops extending this long.
    public float pressStallSeconds = 0.5f;
    // A stroke is latched for at least this long, and contact has to be lost for
    // pressLostContactSeconds before it may be abandoned: the churn of p_arms20
    // (0.18 s per "stroke") was a contact flicker, not a lost slab.
    public float pressMinStrokeSeconds = 0.8f;
    public float pressMaxSeconds = 3.0f;
    public float pressLostContactSeconds = 0.15f;
    public float pressReengageDeg = 12f;
    public float pressReengageCooldownSeconds = 0.4f;
    public float liftHoldSeconds = 0.30f;
    // Height the Lift phase carries the hand at, metres above the latched palm.
    // A lift that solves to the plant point itself is a horizontal slide ALONG
    // the slab, which the ground blocks: measured p_arms52, the hand never left
    // the spot under its own shoulder, the elbow folded onto its own stop (-150)
    // and the strokes that followed delivered 1-3 deg of "extension" with the palm
    // 0.20 m out of reach. Carrying the hand clear of the slab is the right motion
    // but the wrong default HERE, and that is measured too: p_arms53/54 with 0.08
    // m kept the palm on the slab 89-92 % of frames yet the chest ended flat on
    // the ground (pivot -1.89, |tilt| 90, pelvis -1.860), against p_arms55/57 with
    // 0 where the chest ended PROPPED (pivot -1.60, |tilt| 58, pelvis -1.73). The
    // hand leaving the slab takes the only support the chest has. 0 = the hand
    // stays planted through the lift.
    public float liftClearanceMeters = 0f;
    // Shoulder bias added to the aim during the stroke, degrees.
    public float pressShoulderBias = 0f;
    // A stroke ends when the elbow is within this of the extension target.
    public float pressDoneMarginDeg = 6f;
    // Aim gain of the shoulder onto the latched palm (1 = point at it each step).
    public float pressAimGain = 1f;
    public float pressTorqueScale = 4f;
    public float pressPGainScale = 2f;
    public float pressDriveSlewPerSecond = 60f;
    // Same rate limit for the ELBOW target, deg/s. The shoulder had one from
    // p_arms38 and the elbow did not, and that asymmetry is measurable: on
    // p_arms60 the down-window elbow TARGET moved at 446-491 deg/s while the
    // shoulder target moved at 58-60 (its own cap) -- the drive was asking the
    // elbow for a step and the muscle spent the stroke catching up.
    public float pressElbowSlewPerSecond = 90f;
    // Real contact for the press, plus a small band so a palm 1-2 mm above the
    // slab still counts as planted.
    public float handNearGroundSlop = 0.04f;
    // Keep the latched palm pressed while the hand's own orientation changes. The
    // hand is 0.16 long, so a palm latched upright (half-height 0.08) that lands
    // FLAT (0.03) hovers 50 mm over the slab and never makes contact -- measured
    // p_arms43, the palm centre sat 0.02 m over the slab and only 46-68 % of the
    // press samples carried a real contact. The correction moves the target in Y
    // only (that axis IS "how hard the palm is pressed"), never in X, so it cannot
    // walk the palm sideways. 0 = the latched world point is frozen as before.
    //
    // MEASURED 19.09 at full size (`_arms-phases2.py` on the arms5 build,
    // fingerprint 0fd4f288, ground window 7.88-40.00 s, 6426 frames, contact read
    // from the recorded `handContactL/R` per phase):
    //
    //     label     phase share L/R      contact inside the phase L/R
    //     p_arms81  plant 38.6/34.5 %    plant  0/0 %   <- the defect
    //               press 35.2/36.4 %    press 64/73 %
    //               hold  15.7/18.6 %    hold  57/66 %
    //               lift  10.4/10.4 %    lift  45/55 %
    //
    // i.e. with the Y frozen at the latch the palm misses the slab ENTIRELY
    // (0 % on both sides) for more than a third of the fall, and that dead phase
    // is why only 2 of 10 (L) and 4 of 11 (R) strokes ever reached the 20 deg the
    // acceptance asks for: a stroke cannot start or extend while the palm is in
    // the air, and `plantReachSeconds` re-latches a fresh point that misses the
    // same way. The latch is taken where the hand STANDs on end (half-height
    // 0.08) and the palm arrives FLAT (0.03), so the frozen target sits about
    // 50 mm high -- the p_arms43 error, not a new one. The palm's own thickness is
    // the only thing this follows; the lateral offset stays latched. Default ON.
    // `-armPalmLive 0` restores the frozen point, `-armPalmLive 0..1` lerps.
    public float pressPalmLiveDepth = 1f;
    public float pressRequestHoldSeconds = 0.25f;

    // Read-only state, for the recorder and the get-up.
    public bool HandContactL { get; private set; }
    public bool HandContactR { get; private set; }
    public bool HandNearGroundL { get; private set; }
    public bool HandNearGroundR { get; private set; }
    public float ArmPhaseL { get; private set; }
    public float ArmPhaseR { get; private set; }
    public float WalkSwingLevel { get; private set; }
    public float PressRequest { get; private set; }
    public int PressPhaseL { get; private set; }
    public int PressPhaseR { get; private set; }
    public float ArmTargetShoulderL { get; private set; }
    public float ArmTargetShoulderR { get; private set; }
    public float ArmTargetElbowL { get; private set; }
    public float ArmTargetElbowR { get; private set; }
    public int PressStrokesL { get; private set; }
    public int PressStrokesR { get; private set; }
    // Largest elbow extension of the last stroke, degrees (the acceptance wants
    // more than 20).
    public float PressStrokeExtensionL { get; private set; }
    public float PressStrokeExtensionR { get; private set; }
    // Largest shoulder extension the last stroke achieved, degrees.
    public float PressStrokeShoulderExtL { get; private set; }
    public float PressStrokeShoulderExtR { get; private set; }
    // Largest pelvis rise measured inside one press stroke, metres.
    public float PressLiftMax { get; private set; }
    // Largest shoulder rise measured inside one press stroke, per side, metres:
    // the palm is latched on the slab, so this is the push itself, not the
    // pelvis (which only follows if the legs hold).
    public float PressShoulderLiftL { get; private set; }
    public float PressShoulderLiftR { get; private set; }
    // Lateral offset of the latched palm from the shoulder pivot, metres.
    public float PressPlantOffsetL { get; private set; }
    public float PressPlantOffsetR { get; private set; }
    // The elbow angle the plant asked for, degrees: how much fold the stroke
    // starts from, i.e. the room the press has to extend into.
    public float PressPlantFoldL { get; private set; }
    public float PressPlantFoldR { get; private set; }
    // Shoulder pivot height and the latched palm height, metres. The gap between
    // them is what the arm has to span, so a press that cannot reach the slab is
    // told from one that reaches it and is not loaded.
    public float PressPivotYL { get { return ShoulderPivotWorld(SideL).y; } }
    public float PressPivotYR { get { return ShoulderPivotWorld(SideR).y; } }
    public float PressPalmYL { get { return PlantTarget(SideL).y; } }
    public float PressPalmYR { get { return PlantTarget(SideR).y; } }
    // World direction the upper arm points, degrees, and the centre of the
    // direction window the shoulder can reach (rest direction) — so an aim that
    // the joint range cannot serve is a measured distance, not an opinion.
    public float PressAimHaveL { get { return UpperArmWorldDeg(SideL); } }
    public float PressAimHaveR { get { return UpperArmWorldDeg(SideR); } }
    public float PressRestL { get { return UpperArmRestDeg(SideL); } }
    public float PressRestR { get { return UpperArmRestDeg(SideR); } }
    public float PressHandContactFractionL { get; private set; }
    public float PressHandContactFractionR { get; private set; }

    private const int SideL = 0;
    private const int SideR = 1;
    private const int PhaseIdle = 0;
    private const int PhaseLift = 1;
    private const int PhasePress = 2;
    private const int PhaseHold = 3;
    private const int PhasePlant = 4;

    private BalanceController balance;
    private VestibularSystem vestibular;

    private readonly HingeJoint2D[] hip = new HingeJoint2D[2];
    private readonly HingeJoint2D[] shoulder = new HingeJoint2D[2];
    private readonly HingeJoint2D[] elbow = new HingeJoint2D[2];
    private readonly Muscle[] shFlex = new Muscle[2];
    private readonly Muscle[] shExt = new Muscle[2];
    private readonly Muscle[] elFlex = new Muscle[2];
    private readonly Muscle[] elExt = new Muscle[2];
    private readonly Transform[] upper = new Transform[2];
    private readonly Transform[] hand = new Transform[2];
    private readonly Collider2D[] handCol = new Collider2D[2];
    private readonly float[] elbowInertia = new float[2];
    private readonly float[] shoulderInertia = new float[2];
    private readonly int[] phase = new int[2];
    private readonly float[] phaseAge = new float[2];
    private readonly float[] strokeStartElbow = new float[2];
    // Last shoulder target emitted while the body is down, and whether it has been
    // set yet: the aim slew limit needs the previous target to work from.
    private readonly float[] aimTargetPrev = new float[2];
    private readonly bool[] aimSlewSeen = new bool[2];
    private readonly float[] elbowSlewPrev = new float[2];
    private readonly float[] strokeStartShoulder = new float[2];
    private readonly float[] strokeStartPelvisY = new float[2];
    private readonly float[] strokeStartShoulderY = new float[2];
    private readonly float[] strokeShoulderLift = new float[2];
    private readonly float[] lostContactTime = new float[2];
    private readonly float[] strokeStall = new float[2];
    private readonly int[] aimSignVote = new int[2];
    // Elbow branch in use per side (0 = upper arm at beta+alpha, 1 = beta-alpha);
    // -1 before the first solve. Kept between steps so the branch cannot flap.
    private readonly int[] aimBranch = new int[2] { -1, -1 };
    private readonly float[] aimHavePrev = new float[2];
    private readonly float[] aimThPrev = new float[2];
    private readonly bool[] aimSignSeen = new bool[2];
    private readonly int[] strokeCount = new int[2];
    private readonly float[] strokeExt = new float[2];
    private readonly float[] strokeShoulderExt = new float[2];
    private readonly float[] contactSamples = new float[2];
    private readonly float[] contactHits = new float[2];
    private readonly float[] outShoulder = new float[2];
    private readonly float[] outElbow = new float[2];
    private readonly float[] outSlew = new float[2];

    private float shTorqueBase = 40f, elTorqueBase = 30f;
    private float hipSpeedHold;
    private float walkBlend;
    // Filled lazily from the human's own walk driver. The swing must follow the
    // walker's decision to walk, not the hip jitter the swing itself creates.
    private StepPhaseDriver stepDriver;
    private readonly float[] hipMean = new float[2];
    private readonly float[] hipDev = new float[2];
    private readonly float[] hipVar = new float[2];
    private readonly bool[] hipStatsReady = new bool[2];
    private float pressRequestTime = -100f;
    private float pelvisStartY;
    private bool down;
    private bool bound;
    private Transform pelvisBody;
    private float groundTopY = -2f;
    private readonly float[] linkL1 = new float[2];
    private readonly float[] linkL2 = new float[2];
    private readonly float[] plantX = new float[2];
    private readonly float[] plantY = new float[2];

    private const string ArgShoulderSwing = "-armSwing";
    private const string ArgElbowAmp = "-armElbowSwing";
    private const string ArgElbowMid = "-armElbowMid";
    private const string ArgSign = "-armSign";
    private const string ArgIps = "-armIps";
    private const string ArgCross = "-armCross";
    private const string ArgCrossR = "-armCrossR";
    private const string ArgWalkGain = "-armWalkGain";
    private const string ArgPush = "-armPush";
    private const string ArgPushScale = "-armPushScale";
    private const string ArgFallTilt = "-armFallTilt";
    private const string ArgPlantSide = "-armPlantSide";
    private const string ArgPlantShare = "-armPlantShare";
    private const string ArgPlantStop = "-armPlantStop";
    private const string ArgPlantReach = "-armPlantReach";
    private const string ArgPressDepth = "-armPressDepth";
    private const string ArgPressMode = "-armPressMode";
    private const string ArgDepthExtra = "-armDepthExtra";
    private const string ArgDepthRate = "-armDepthRate";
    private const string ArgPalmLive = "-armPalmLive";
    private const string ArgHipTau = "-armHipTau";
    private const string ArgStrokeRate = "-armStrokeRate";
    private const string ArgStrokeMin = "-armStrokeMin";
    private const string ArgAimGain = "-armAimGain";
    private const string ArgPressShoulder = "-armPressShoulder";
    private const string ArgPressElbow = "-armPressElbow";
    private const string ArgLiftHold = "-armLiftHold";
    private const string ArgLiftClear = "-armLiftClear";
    private const string ArgElbowSlew = "-armElbowSlew";
    public float armDebug;
    private const string ArgDebug = "-armDebug";

    void Awake()
    {
        ParseArgs();
        Bind();
    }

    // Idempotent: Human.BuildHuman calls it once the body exists.
    public void Bind()
    {
        if (bound) return;
        bound = true;

        balance = GetComponent<BalanceController>();
        vestibular = GetComponent<VestibularSystem>();

        hip[SideL] = JointOn("LeftLegThigh");
        hip[SideR] = JointOn("RightLegThigh");
        shoulder[SideL] = JointOn("LeftArmUpper");
        shoulder[SideR] = JointOn("RightArmUpper");
        elbow[SideL] = JointOn("LeftArmLower");
        elbow[SideR] = JointOn("RightArmLower");

        for (int i = 0; i < 2; i++)
        {
            string side = i == SideL ? "Left" : "Right";
            MuscleFlexorPair(side + "ArmUpper", out shFlex[i], out shExt[i]);
            MuscleFlexorPair(side + "ArmLower", out elFlex[i], out elExt[i]);
            upper[i] = transform.Find(side + "ArmUpper");
            hand[i] = transform.Find(side + "ArmHand");
            handCol[i] = hand[i] != null ? hand[i].GetComponent<Collider2D>() : null;
            phase[i] = PhaseIdle;
            // Link lengths for the two-link solve. The joint anchors live in each
            // segment's own frame, so the pivots are exact, not centre-to-centre.
            if (shoulder[i] != null && shoulder[i].attachedRigidbody != null)
            {
                Transform armRig = shoulder[i].attachedRigidbody.transform;
                Vector2 shoulderPivot = (Vector2)armRig.position + (Vector2)(armRig.rotation * (Vector3)shoulder[i].anchor);
                Vector2 elbowPivot = shoulderPivot + Vector2.down * 0.33f;
                Transform foreRig = elbow[i] != null && elbow[i].attachedRigidbody != null ? elbow[i].attachedRigidbody.transform : null;
                if (foreRig != null)
                {
                    elbowPivot = (Vector2)foreRig.position + (Vector2)(foreRig.rotation * (Vector3)elbow[i].anchor);
                }
                linkL1[i] = Vector2.Distance(shoulderPivot, elbowPivot);
                if (hand[i] != null) linkL2[i] = Vector2.Distance(elbowPivot, (Vector2)hand[i].position);
            }
            if (linkL1[i] <= 0.01f) linkL1[i] = 0.33f;
            if (linkL2[i] <= 0.01f) linkL2[i] = 0.34f;
        }

        if (shFlex[SideL] != null) shTorqueBase = shFlex[SideL].maxTorque;
        if (elFlex[SideL] != null) elTorqueBase = elFlex[SideL].maxTorque;

        pelvisBody = transform.Find("Pelvis");
        if (pelvisBody != null) pelvisStartY = pelvisBody.position.y;
        GameObject ground = GameObject.Find("Ground");
        if (ground != null)
        {
            Collider2D groundCollider = ground.GetComponent<Collider2D>();
            if (groundCollider != null) groundTopY = groundCollider.bounds.max.y;
        }
    }

    private HingeJoint2D JointOn(string name)
    {
        Transform t = transform.Find(name);
        return t != null ? t.GetComponent<HingeJoint2D>() : null;
    }

    private void MuscleFlexorPair(string segmentName, out Muscle flexor, out Muscle extensor)
    {
        flexor = null;
        extensor = null;
        Transform t = transform.Find(segmentName);
        if (t == null) return;
        Muscle[] all = t.GetComponents<Muscle>();
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] == null) continue;
            if (all[i].direction > 0f) flexor = all[i];
            else extensor = all[i];
        }
    }

    // The get-up (BalanceController) calls this every step while it wants the
    // palm press. intensity 0..1. Returns true when the press is running with
    // both palms in real contact.
    public bool RequestArmPress(float intensity)
    {
        float level = Mathf.Clamp01(intensity);
        if (level >= 0.01f)
        {
            PressRequest = level;
            pressRequestTime = Time.fixedTime;
        }
        return HandContactL && HandContactR;
    }

    void FixedUpdate()
    {
        if (!bound) Bind();
        if (shoulder[SideL] == null || elbow[SideL] == null) return;

        float dt = Time.fixedDeltaTime;
        float tilt = vestibular != null ? vestibular.GetBodyTilt() : 0f;
        bool requestLive = PressRequest >= 0.01f
            && Time.fixedTime - pressRequestTime <= pressRequestHoldSeconds;
        down = Mathf.Abs(tilt) >= fallTiltDeg || requestLive;

        SampleHandContact();
        UpdateWalkSwing(dt);
        UpdatePress(dt);
        WriteMuscles();
    }

    // ─── walk swing ───

    private void UpdateWalkSwing(float dt)
    {
        if (stepDriver == null) stepDriver = GetComponent<StepPhaseDriver>();
        bool walkActive = stepDriver != null && stepDriver.walkActive;

        float hipSpeed = 0f;
        for (int i = 0; i < 2; i++)
        {
            if (hip[i] != null)
                hipSpeed = Mathf.Max(hipSpeed, Mathf.Abs(hip[i].jointSpeed));
        }
        if (hipSpeed >= walkHipSpeedDegPerSec) hipSpeedHold = walkHoldSeconds;
        else hipSpeedHold = Mathf.Max(0f, hipSpeedHold - dt);

        // The gate needs a moving hip AND a walk the walker actually asked for.
        // The hip test alone is a trap: the swing shakes the torso, the torso
        // drives the hips, so the hip speed that switched the swing on is made by
        // the swing itself. In a quiet stand that loop latched the swing on for
        // 99.6% of the frames (18.09 21:15: shoulder RMS 181.7 deg/s, elbow 311.6,
        // torsoAngVel 63.0) while the same build with -armSwing 0 stands at
        // 7.4 / 21.5 / 3.9, i.e. exactly the pre-regression stand. The walker's
        // own walkActive reads 0 in all 8001 stand frames and 1 in 7000 of 8001
        // walk frames, so it separates the two states instead of inverting them.
        float target = hipSpeedHold > 0f && walkActive && !down ? 1f : 0f;
        walkBlend = Mathf.MoveTowards(walkBlend, target, dt / Mathf.Max(0.05f, walkRampSeconds));
        WalkSwingLevel = walkBlend;

        // The swing follows the hips' OSCILLATION, not their mean position, and
        // each side is standardised by its own running deviation: measured, the
        // left thigh swings 1.8x the right one, and with a shared scale the
        // left arm's two terms cancel out and the drive emits nothing (p_arms8:
        // left shoulder target +-2 deg against the right one's +-20).
        float alpha = 1f - Mathf.Exp(-dt / Mathf.Max(0.05f, hipStatsTau));
        for (int i = 0; i < 2; i++)
        {
            if (hip[i] == null)
            {
                hipDev[i] = 0f;
                continue;
            }
            float x = hip[i].jointAngle;
            if (!hipStatsReady[i])
            {
                hipMean[i] = x;
                hipVar[i] = 0f;
                hipStatsReady[i] = true;
            }
            float d = x - hipMean[i];
            hipMean[i] += alpha * d;
            hipVar[i] += alpha * (d * d - hipVar[i]);
            float sd = Mathf.Sqrt(Mathf.Max(hipVar[i], minHipVarianceDeg2));
            hipDev[i] = Mathf.Clamp((x - hipMean[i]) / sd, -2.5f, 2.5f);
        }

        float dir = swingSign >= 0f ? 1f : -1f;
        // Positive phase = that arm is BACK. The acceptance pairing: the left
        // shoulder is driven AGAINST the right thigh, so the opposite hip enters
        // with a negative coefficient. The same-side term carries the opposite
        // sign and keeps the two arms in counter phase with each other in a
        // window where the legs move together (corr(hipL,hipR) near +1 otherwise
        // forces both shoulders into one signal — the +0.99 of p_arms2). The two
        // ratios are per side (crossWeightR) because the two hips are not mirror
        // signals in this rig, and each side is standardised by its own deviation.
        float scaleL = Mathf.Max(0.1f, ipsWeight + crossWeight);
        float scaleR = Mathf.Max(0.1f, ipsWeight + crossWeightR);
        float backL = dir * (ipsWeight * hipDev[SideL] - crossWeight * hipDev[SideR]) / scaleL;
        float backR = dir * (ipsWeight * hipDev[SideR] - crossWeightR * hipDev[SideL]) / scaleR;
        ArmPhaseL = Mathf.Clamp(backL, -1f, 1f) * walkBlend;
        ArmPhaseR = Mathf.Clamp(backR, -1f, 1f) * walkBlend;

        float shoulderBase = balance != null ? balance.shoulderBaseAngle : 0f;
        float elbowBase = balance != null ? balance.elbowBaseAngle : -20f;
        // Elbow: flex on the forward swing, release on the back swing. Both
        // sides ride their own phase, so the two elbows counter each other.
        for (int i = 0; i < 2; i++)
        {
            float phaseNow = i == SideL ? ArmPhaseL : ArmPhaseR;
            outShoulder[i] = shoulderBase + walkShoulderSwing * phaseNow;
            outElbow[i] = elbowBase - (walkElbowMid + walkElbowAmp * phaseNow);
            outSlew[i] = walkDriveSlewPerSecond;
        }
    }

    // ─── fall press ───

    // Two-link solve of the arm for a hand target: the shoulder and elbow joint
    // angles that put the palm on the target. The elbow hinge only flexes one
    // way, so the two mirror triangles about the target chord are not both
    // available -- and which one is available is NOT the same on the two sides
    // (measured p_arms34: the left arm's reachable world directions are -18..232
    // deg and the right's 110..360, so +alpha serves one side and -alpha the
    // other). Both branches give the SAME elbow angle, so the branch is chosen by
    // reachability of the shoulder angle -- no sign vote and no mirror
    // assumption, and a wrong branch is impossible.
    //
    // The chord is clamped to the SHORTEST chord the elbow's stop allows: a
    // target nearer than that is pressed INTO the slab (the solve keeps the
    // target's direction and pushes) instead of being folded onto a collided
    // stop, which is what left the former plant with an 18 deg interior angle
    // against a 30 deg stop and no push at all.
    private void SolveArm(int i, Vector2 target, out float shoulderTarget, out float elbowTarget)
    {
        shoulderTarget = shoulder[i] != null ? shoulder[i].jointAngle : 0f;
        elbowTarget = elbow[i] != null ? elbow[i].jointAngle : 0f;
        if (shoulder[i] == null || upper[i] == null) return;

        Vector2 shoulderPivot = ShoulderPivotWorld(i);
        Vector2 toTarget = target - shoulderPivot;
        if (toTarget.sqrMagnitude < 1e-8f) return;

        float minChord = MinChord(i);
        float maxChord = Mathf.Max(minChord + 0.01f, linkL1[i] + linkL2[i] - 0.01f);
        float dist = Mathf.Clamp(toTarget.magnitude, minChord, maxChord);

        float beta = Mathf.Atan2(toTarget.y, toTarget.x) * Mathf.Rad2Deg;
        float cosAlpha = (linkL1[i] * linkL1[i] + dist * dist - linkL2[i] * linkL2[i])
                         / (2f * linkL1[i] * dist);
        float cosElbow = (linkL1[i] * linkL1[i] + linkL2[i] * linkL2[i] - dist * dist)
                         / (2f * linkL1[i] * linkL2[i]);
        float alpha = Mathf.Acos(Mathf.Clamp(cosAlpha, -1f, 1f)) * Mathf.Rad2Deg;
        float interior = Mathf.Acos(Mathf.Clamp(cosElbow, -1f, 1f)) * Mathf.Rad2Deg;

        float candPlus, candMinus;
        bool okPlus = JointAngleForDirection(i, beta + alpha, out candPlus);
        bool okMinus = JointAngleForDirection(i, beta - alpha, out candMinus);
        // Keep the branch this arm is already holding while it stays legal (a
        // press posture is a posture, not a per-step decision: both branches can
        // be inside the shoulder limits at once, and a per-step choice flapped
        // between them -- measured p_arms35, armTargetShoulderL -150, +63, -159,
        // +57 -- so the PD chased a target 200 deg away on alternate steps and
        // the palm never landed).
        //
        // When a fresh choice is needed, take the branch that puts the ELBOW
        // HIGHER: that is the push-up posture, the arm bowing up over its own
        // palm, and it is the one whose extension can only drive the shoulder
        // away from the slab. Measured p_arms36: the "nearest angle" rule took
        // the other branch, whose elbow sat between the palm and the body, and
        // the first elbow extension threw the hand 0.9 m into the air instead of
        // loading it (contact had been 100 % the frame before).
        int pick;
        if (aimBranch[i] == 0 && okPlus) pick = 0;
        else if (aimBranch[i] == 1 && okMinus) pick = 1;
        else if (okPlus && okMinus)
            pick = ElbowHeight(i, beta + alpha) >= ElbowHeight(i, beta - alpha) ? 0 : 1;
        else if (okPlus) pick = 0;
        else if (okMinus) pick = 1;
        else pick = aimBranch[i] == 1 ? 1 : 0;
        aimBranch[i] = pick;
        shoulderTarget = pick == 0 ? candPlus : candMinus;
        elbowTarget = ClampElbow(i, interior - 180f);
    }

    // World height of the elbow for a given upper-arm direction: the elbow is L1
    // from the shoulder pivot along that direction.
    private float ElbowHeight(int i, float upperDirDeg)
    {
        Vector2 pivot = ShoulderPivotWorld(i);
        return pivot.y + linkL1[i] * Mathf.Sin(upperDirDeg * Mathf.Deg2Rad);
    }

    // Chord of the two-link arm at a given interior (elbow) angle, metres.
    private float ChordForInterior(int i, float interiorDeg)
    {
        float c = Mathf.Cos(Mathf.Clamp(interiorDeg, 5f, 175f) * Mathf.Deg2Rad);
        float sq = linkL1[i] * linkL1[i] + linkL2[i] * linkL2[i]
                   - 2f * linkL1[i] * linkL2[i] * c;
        float chord = Mathf.Sqrt(Mathf.Max(0.0001f, sq));
        return Mathf.Clamp(chord, 0.05f, linkL1[i] + linkL2[i] - 0.01f);
    }

    // Shortest chord this arm can make without sitting on the elbow's stop: a
    // chord exactly at the stop leaves the joint no torque to hold the palm down.
    private float MinChord(int i)
    {
        float foldMin = (elbow[i] != null && elbow[i].useLimits ? elbow[i].limits.min : -150f)
                        + plantFoldMarginDeg;
        return ChordForInterior(i, 180f + foldMin);
    }

    // World position of the shoulder hinge: the upper arm's pivot, not its centre.
    private Vector2 ShoulderPivotWorld(int i)
    {
        if (shoulder[i] == null || shoulder[i].attachedRigidbody == null)
            return upper[i] != null ? (Vector2)upper[i].position : Vector2.zero;
        Transform armRig = shoulder[i].attachedRigidbody.transform;
        return (Vector2)armRig.position + (Vector2)(armRig.rotation * (Vector3)shoulder[i].anchor);
    }

    private void UpdatePress(float dt)
    {
        float baseShoulder = balance != null ? balance.shoulderBaseAngle : 0f;
        float baseElbow = balance != null ? balance.elbowBaseAngle : -20f;

        for (int i = 0; i < 2; i++)
        {
            TrackAimSign(i);
            bool contact = i == SideL ? (HandContactL || HandNearGroundL)
                                      : (HandContactR || HandNearGroundR);
            bool realContact = i == SideL ? HandContactL : HandContactR;
            if (down)
            {
                contactSamples[i] += 1f;
                if (contact) contactHits[i] += 1f;
            }

            float shoulderTarget = walkBlend > 0.001f ? outShoulder[i] : baseShoulder;
            float elbowTarget = walkBlend > 0.001f ? outElbow[i] : baseElbow;
            float slew = walkBlend > 0.001f ? walkDriveSlewPerSecond : pressDriveSlewPerSecond;

            if (pushEnabled > 0.5f && down)
            {
                float elbowAngle = elbow[i] != null ? elbow[i].jointAngle : 0f;
                phaseAge[i] += dt;
                slew = pressDriveSlewPerSecond;
                if (realContact) lostContactTime[i] = 0f;
                else lostContactTime[i] += dt;

                if (phase[i] == PhaseIdle)
                {
                    // A fall just latched: latch a plant point from the pose the
                    // body landed in, then reach for it. The latched point is a
                    // WORLD point and is not recomputed while the arm works on
                    // it: the former per-frame PalmCentreHeight update moved the
                    // target with the hand's own orientation, so the arm chased a
                    // point it kept moving.
                    ChoosePlantPoint(i);
                    phase[i] = PhasePlant;
                    phaseAge[i] = 0f;
                }

                // Keep the palm pressed: the target's Y follows the hand's CURRENT
                // thickness (see pressPalmLiveDepth). X is never touched, so this
                // cannot re-create the chasing target the plant latch exists to
                // prevent.
                if (pressPalmLiveDepth > 0f)
                {
                    plantY[i] = Mathf.Lerp(plantY[i], PalmCentreHeight(i),
                        Mathf.Clamp01(pressPalmLiveDepth));
                }

                if (phase[i] == PhaseLift)
                {
                    // Palm unloaded: the arm carries the hand CLEAR of the slab and
                    // translates it to the next plant point (liftClearanceMeters),
                    // then lowers it. Same solve as the plant, but no stroke may
                    // start here.
                    Vector2 liftTarget = PlantTarget(i);
                    liftTarget.y += Mathf.Max(0f, liftClearanceMeters);
                    SolveArm(i, liftTarget, out shoulderTarget, out elbowTarget);
                    if (phaseAge[i] >= liftHoldSeconds)
                    {
                        phase[i] = PhasePlant;
                        phaseAge[i] = 0f;
                    }
                }
                else if (phase[i] == PhasePlant)
                {
                    // Reach for the latched point until the palm is really on the
                    // slab. That contact is the only thing that starts a stroke.
                    SolveArm(i, PlantTarget(i), out shoulderTarget, out elbowTarget);
                    if (realContact)
                    {
                        BeginStroke(i, elbowAngle);
                    }
                    else if (phaseAge[i] >= plantReachSeconds)
                    {
                        // The body rolled and the latched point is out of reach:
                        // take a fresh one from where the shoulder is now.
                        ChoosePlantPoint(i);
                        phaseAge[i] = 0f;
                    }
                }
                else
                {
                    // Press/Hold: the palm sits on the slab at a latched world
                    // point and does not move. Press drives that point pressDepth
                    // INTO the slab, so the arm is always asking for more
                    // extension than the ground allows: the joints load against
                    // the block, and the only thing that can give is the shoulder
                    // -- the body rising. Hold keeps the same point unloaded. The
                    // solve is re-run every step (not frozen) so the arm keeps
                    // pointing at its own palm as the shoulder climbs.
                    Vector2 strokeTarget = PlantTarget(i);
                    bool depthRamp = pressStrokeMode >= 0.5f;
                    if (phase[i] == PhasePress)
                    {
                        strokeTarget.y -= pressDepth;
                        if (depthRamp)
                        {
                            // The latched point is driven deeper into the slab and
                            // BOTH joints keep solving onto it, so the hand stays on
                            // its own palm while the arm is forced to give. This is
                            // the difference from the elbow ramp below: measured
                            // p_arms40 with the ramp, the elbow opened -141 -> -18
                            // while the shoulder aimed at the palm, and the hand
                            // ended 0.39 m PAST the palm it was supposed to press on
                            // (arm lying flat on the ground at x 1.86 against the
                            // palm's 2.25) -- the arm walked its hand instead of
                            // loading it, which is the p_arms20 churn again.
                            strokeTarget.y -= Mathf.Min(pressDepthExtra,
                                pressDepthRate * phaseAge[i]);
                        }
                    }
                    SolveArm(i, strokeTarget, out shoulderTarget, out elbowTarget);
                    shoulderTarget += pressShoulderBias;
                    if (!depthRamp)
                    {
                        // Former stroke: the elbow alone is commanded toward full
                        // extension while the palm is blocked by the slab. The ramp
                        // is direction agnostic on purpose: a folded elbow is
                        // NEGATIVE here (-116 deg at the plant, -4 deg straight), so
                        // the earlier Mathf.Max(pressElbowTarget, start + rate*age)
                        // picked the fully extended target from the very first frame
                        // and the limiter never engaged -- measured p_arms37, the
                        // elbow threw 107 deg in 0.09 s and the hand ended 1.24 m off
                        // its own palm.
                        float strokeGoal = pressElbowTarget;
                        if (pressStrokeDegPerSecond > 0f)
                        {
                            float room = pressElbowTarget - strokeStartElbow[i];
                            float ramp = pressStrokeDegPerSecond * phaseAge[i];
                            strokeGoal = strokeStartElbow[i] + Mathf.Clamp(room, -ramp, ramp);
                        }
                        elbowTarget = ClampElbow(i, strokeGoal);
                    }
                    else
                    {
                        // The depth ramp has given all it has when the target stops
                        // getting deeper; the pose is then held by the solve.
                        elbowTarget = ClampElbow(i, elbowTarget);
                    }

                    if (phase[i] == PhasePress)
                    {
                        TrackStroke(i, elbowAngle, dt);
                        bool strokeDone = phaseAge[i] >= pressMaxSeconds
                                          || (phaseAge[i] >= pressMinStrokeSeconds
                                              && strokeStall[i] >= pressStallSeconds);
                        bool slabLost = lostContactTime[i] >= pressLostContactSeconds
                                        && phaseAge[i] >= pressMinStrokeSeconds;
                        if (strokeDone || slabLost)
                        {
                            phase[i] = PhaseHold;
                            phaseAge[i] = 0f;
                            // The stall that ENDED the stroke must not carry into
                            // the hold, or the hold's own stall test would re-engage
                            // on the first frame after the cooldown and the press
                            // would churn (measured p_arms52 21.5-27 s: 0.5 s strokes
                            // delivering 0-7 deg). The hold measures its own stall.
                            strokeStall[i] = 0f;
                        }
                    }
                    else
                    {
                        // Hold: keep the loaded pose. Another stroke only after a
                        // dwell and only for a reason — the slab is gone, the body
                        // sank and refolded the arm, the elbow ran out of room
                        // (straight = the press has given all it has), or the elbow
                        // has STOPPED extending while the body is still down. That
                        // last one is the climbing loop, and it is measured: p_arms50
                        // with the three former reasons only, the elbow stopped at
                        // -73 deg, none of them ever fired, and the arm sat in Hold
                        // for 5775 of the 6447 fallen frames (2 strokes in 32 s,
                        // 0.03 m of shoulder rise). Measured p_arms20 the opposite
                        // mistake: re-engaging on the elbow angle alone ran 191
                        // strokes of 0.18 s and 2.7 deg each.
                        TrackStroke(i, elbowAngle, dt);
                        bool sank = elbowAngle < strokeStartElbow[i] + pressReengageDeg;
                        bool outOfRoom = elbowAngle >= pressElbowTarget - pressDoneMarginDeg;
                        bool stalled = strokeStall[i] >= pressStallSeconds;
                        if (phaseAge[i] >= pressReengageCooldownSeconds
                            && (lostContactTime[i] >= pressLostContactSeconds || sank
                                || outOfRoom || stalled))
                        {
                            ChoosePlantPoint(i);
                            phase[i] = PhaseLift;
                            phaseAge[i] = 0f;
                        }
                    }
                }
            }
            else if (!down && phase[i] != PhaseIdle)
            {
                // Standing again: hand the arms back to the balance controller.
                phase[i] = PhaseIdle;
                aimBranch[i] = -1;
                strokeStartElbow[i] = 0f;
                lostContactTime[i] = 0f;
            }

            if (pushEnabled > 0.5f && down)
            {
                // The aim itself is rate limited, and this is what keeps the joint
                // calm. The solve is an incremental aim: it reads the arm's own
                // measured world direction and asks the shoulder to move by the
                // error. While the body is still tumbling that reading swings with
                // the arm, the aim chases it and the PD chases the aim -- measured
                // p_arms38, the shoulder target moved 16 deg in one 5 ms step
                // (3200 deg/s) and the arm whirled at 610 deg/s, ten times the
                // calm walking value the brief measured.
                if (!aimSlewSeen[i])
                {
                    aimSlewSeen[i] = true;
                    aimTargetPrev[i] = shoulder[i] != null ? shoulder[i].jointAngle : shoulderTarget;
                    elbowSlewPrev[i] = elbow[i] != null ? elbow[i].jointAngle : elbowTarget;
                }
                if (pressDriveSlewPerSecond > 0f)
                {
                    float maxStep = pressDriveSlewPerSecond * dt;
                    shoulderTarget = aimTargetPrev[i]
                        + Mathf.Clamp(shoulderTarget - aimTargetPrev[i], -maxStep, maxStep);
                }
                aimTargetPrev[i] = shoulderTarget;
                // The elbow gets the same treatment it never had: the stroke is
                // still free to open 90 deg/s, but the drive can no longer ask
                // for a 450 deg/s step that the joint then chases.
                if (pressElbowSlewPerSecond > 0f)
                {
                    float maxElbowStep = pressElbowSlewPerSecond * dt;
                    elbowTarget = elbowSlewPrev[i]
                        + Mathf.Clamp(elbowTarget - elbowSlewPrev[i], -maxElbowStep, maxElbowStep);
                }
                elbowSlewPrev[i] = elbowTarget;
            }
            else
            {
                aimSlewSeen[i] = false;
            }

            outShoulder[i] = shoulderTarget;
            outElbow[i] = elbowTarget;
            outSlew[i] = slew;
            if (i == SideL)
            {
                ArmTargetShoulderL = shoulderTarget;
                ArmTargetElbowL = elbowTarget;
                PressPhaseL = phase[i];
            }
            else
            {
                ArmTargetShoulderR = shoulderTarget;
                ArmTargetElbowR = elbowTarget;
                PressPhaseR = phase[i];
            }
        }

        if (contactSamples[SideL] > 0f)
            PressHandContactFractionL = contactHits[SideL] / contactSamples[SideL];
        if (contactSamples[SideR] > 0f)
            PressHandContactFractionR = contactHits[SideR] / contactSamples[SideR];
        PressStrokesL = strokeCount[SideL];
        PressStrokesR = strokeCount[SideR];
        PressStrokeExtensionL = strokeExt[SideL];
        PressStrokeExtensionR = strokeExt[SideR];
        PressStrokeShoulderExtL = strokeShoulderExt[SideL];
        PressStrokeShoulderExtR = strokeShoulderExt[SideR];

        if (armDebug > 0f) LogArmState();
    }

    // Number dump for the press, off unless -armDebug 1. One line per side every
    // 0.25 s while the arms are working: the geometry a later change has to be
    // argued from (pivot, latched palm, the fold the solve asks for, the joint
    // range it has to live in) next to where the palm actually is.
    private void LogArmState()
    {
        if (Time.frameCount % 50 != 0) return;
        for (int i = 0; i < 2; i++)
        {
            Vector2 pivot = ShoulderPivotWorld(i);
            Vector2 palm = PlantTarget(i);
            float sh = shoulder[i] != null ? shoulder[i].jointAngle : 0f;
            float el = elbow[i] != null ? elbow[i].jointAngle : 0f;
            float shLo = shoulder[i] != null && shoulder[i].useLimits ? shoulder[i].limits.min : 0f;
            float shHi = shoulder[i] != null && shoulder[i].useLimits ? shoulder[i].limits.max : 0f;
            float elLo = elbow[i] != null && elbow[i].useLimits ? elbow[i].limits.min : 0f;
            Vector2 handPos = hand[i] != null ? (Vector2)hand[i].position : Vector2.zero;
            Debug.Log(string.Format(
                "[ARM] t={0:F2} {1} ph={2} age={3:F2} touch={4} lost={5:F2} " +
                "pivot=({6:F2},{7:F2}) palm=({8:F2},{9:F2}) hand=({10:F2},{11:F2}) " +
                "sh={12:F1}[{13:F0},{14:F0}] el={15:F1}[{16:F0}] plantFold={17:F1} " +
                "tgt=({18:F1},{19:F1})", Time.fixedTime, i == SideL ? "L" : "R",
                phase[i], phaseAge[i], (i == SideL ? HandContactL : HandContactR) ? 1 : 0,
                lostContactTime[i], pivot.x, pivot.y, palm.x, palm.y, handPos.x, handPos.y,
                sh, shLo, shHi, el, elLo,
                i == SideL ? PressPlantFoldL : PressPlantFoldR, outShoulder[i], outElbow[i]));
        }
    }

    private void BeginStroke(int i, float elbowAngle)
    {
        phase[i] = PhasePress;
        phaseAge[i] = 0f;
        strokeStartElbow[i] = elbowAngle;
        strokeStartShoulder[i] = shoulder[i] != null ? shoulder[i].jointAngle : 0f;
        strokeStartPelvisY[i] = pelvisBody != null ? pelvisBody.position.y : 0f;
        strokeStartShoulderY[i] = ShoulderPivotWorld(i).y;
        strokeExt[i] = 0f;
        strokeShoulderExt[i] = 0f;
        strokeShoulderLift[i] = 0f;
        strokeStall[i] = 0f;
        // The stroke is NOT steered by a frozen aim any more: the shoulder keeps
        // solving onto the latched palm (Press/Hold in UpdatePress) and the elbow
        // is commanded straight. Measured p_arms25/34 with a frozen aim: the arm
        // was free to rotate about its own elbow, so "extending" it flew the HAND
        // 0.66 m off the slab instead of loading the palm, and the shoulder --
        // the thing a press is supposed to move -- rose 1.6 mm.
        strokeCount[i]++;
    }

    private void TrackStroke(int i, float elbowAngle, float dt)
    {
        // Extension is positive: the elbow angle grows toward 0.
        float ext = elbowAngle - strokeStartElbow[i];
        // One step of expected progress, not a flat 0.5 deg: with the stroke rate
        // limiter on (60 deg/s at 200 Hz = 0.3 deg/step) a fixed threshold trips on
        // every single step, which ended every stroke in p_arms37 at exactly the
        // minimum age and never let the load build.
        float stepDone = pressStrokeDegPerSecond > 0f
            ? pressStrokeDegPerSecond * dt * 0.5f
            : 0.5f;
        if (ext > strokeExt[i] + stepDone)
        {
            strokeExt[i] = ext;
            strokeStall[i] = 0f;
        }
        else
        {
            strokeStall[i] += dt;
        }
        float shoulderAngle = shoulder[i] != null ? shoulder[i].jointAngle : 0f;
        float shoulderExt = shoulderAngle - strokeStartShoulder[i];
        if (shoulderExt > strokeShoulderExt[i]) strokeShoulderExt[i] = shoulderExt;
        float pelvisY = pelvisBody != null ? pelvisBody.position.y : 0f;
        float lift = pelvisY - strokeStartPelvisY[i];
        if (lift > PressLiftMax) PressLiftMax = lift;
        // The palm is latched on the slab through the stroke, so the shoulder
        // pivot rising is the push itself, independent of what the legs do.
        float shoulderLift = ShoulderPivotWorld(i).y - strokeStartShoulderY[i];
        if (shoulderLift > strokeShoulderLift[i])
        {
            strokeShoulderLift[i] = shoulderLift;
            if (i == SideL) PressShoulderLiftL = shoulderLift;
            else PressShoulderLiftR = shoulderLift;
        }
    }

    // ─── plant geometry ───

    private Vector2 PlantTarget(int i)
    {
        return new Vector2(plantX[i], plantY[i]);
    }

    // Where the palm's centre has to sit to press the slab: the hand's own live
    // half-height above the ground top, sunk handGroundSink below it, so the
    // collider is squeezed onto the ground instead of hovering over it.
    // Deliberately NOT a fixed half-height: measured p_arms24 the left hand
    // landed flat (an AABB half-height of 0.03, not the 0.08 the target assumed),
    // stopped 25 mm over the slab and tripped the estimator's slop band with no
    // real contact at all — while the right hand, one centimetre lower and
    // standing on its end, gripped.
    private float PalmCentreHeight(int i)
    {
        float halfY = 0.06f;
        if (hand[i] != null && handCol[i] != null)
            halfY = hand[i].position.y - handCol[i].bounds.min.y;
        return groundTopY - handGroundSink + Mathf.Max(0.01f, halfY);
    }

    // Latch the palm's world point for the coming stroke. The palm goes as close
    // to straight under the shoulder as the elbow's stop allows: the lift a
    // stroke delivers is (chord growth) * (how vertical the chord is), so a palm
    // planted sideways spends the extension on a horizontal shove. The lateral
    // offset is therefore only the length the arm CANNOT fold away -- the chord
    // at the stop plus plantStopMarginDeg of margin, and beyond that straight
    // down. Measured p_arms38: forcing the elbow to start 65 deg from straight
    // planted the palm 0.35 m out, pinned the chord 70 deg off vertical and
    // delivered 0.10 m of shoulder rise, with the arm lying flat on the slab.
    // The earlier rule ("the closest point the folded arm can reach") planted at
    // the stop itself and left the stroke nothing to press with: p_arms34 offset
    // 0.00, plantFold -142, 1.6 mm of rise.
    private void ChoosePlantPoint(int i)
    {
        Vector2 pivot = ShoulderPivotWorld(i);
        aimBranch[i] = -1;   // a new latched point is a fresh posture
        float palmY = PalmCentreHeight(i);
        float drop = Mathf.Max(0.02f, pivot.y - palmY);
        float foldMin = (elbow[i] != null && elbow[i].useLimits ? elbow[i].limits.min : -150f);
        float interiorMin = Mathf.Clamp(180f + foldMin + plantStopMarginDeg, 10f, 170f);
        float chordStart = ChordForInterior(i, interiorMin);
        // The latched point goes UNDER the shoulder (plantLateralShare 0) and, when
        // the shoulder is too low over the slab for that to be reachable, at the
        // shortest chord the arm can still hold torque at. A palm that sits BESIDE
        // the shoulder for its own sake spends the elbow extension on a horizontal
        // shove instead of a lift: measured p_arms39 with the lateral-fill rule, a
        // shoulder 0.11 m over the slab latched a 0.29 m side point, the arm lay
        // flat (sh +45, el -103), and the whole body was shoved 2.0 m along the
        // ground (pivot x 0.65 -> 2.67) with the pelvis never leaving -1.86.
        float reach = MinChord(i) + plantReachMargin;
        float offset;
        if (plantLateralShare > 0.5f)
        {
            // Former fill rule, kept for an A/B on one build.
            float lateralFill = drop < chordStart
                ? Mathf.Sqrt(Mathf.Max(0f, chordStart * chordStart - drop * drop))
                : 0f;
            offset = Mathf.Clamp(lateralFill, 0f, plantSideMax);
        }
        else
        {
            // sqrt(reach^2 - drop^2): 0 when the shoulder is high enough to take
            // the point straight underneath (the press posture), and exactly the
            // arm's minimum chord when it is not -- the nearest point on the slab
            // the arm can push from at all.
            float needed = drop < reach
                ? Mathf.Sqrt(Mathf.Max(0f, reach * reach - drop * drop))
                : 0f;
            offset = Mathf.Clamp(needed, 0f, plantSideMax);
        }

        // The fold the latched point asks for: the stroke starts here, so this is
        // the room the press has. Logged beside the delivered elbow angle, which
        // is how a jammed joint is told from a loaded one. The chord is clamped
        // the way the solve clamps it, so the logged fold is the fold the SOLVE
        // asks for and not the fold of an unreachable point below the slab.
        float chord = Mathf.Clamp(Mathf.Sqrt(offset * offset + drop * drop),
                                  chordStart, linkL1[i] + linkL2[i] - 0.01f);
        float cosInterior = (linkL1[i] * linkL1[i] + linkL2[i] * linkL2[i] - chord * chord)
                            / (2f * linkL1[i] * linkL2[i]);
        float plantFold = Mathf.Acos(Mathf.Clamp(cosInterior, -1f, 1f)) * Mathf.Rad2Deg - 180f;

        if (i == SideL)
        {
            PressPlantOffsetL = offset;
            PressPlantFoldL = plantFold;
        }
        else
        {
            PressPlantOffsetR = offset;
            PressPlantFoldR = plantFold;
        }

        // Which side of the shoulder the palm lands on: the side the folded arm
        // already lies. The rig's arms are not mirror-reachable (measured p_arms34:
        // the left arm's world window is -18..232 deg, the right's 110..360), so a
        // hardcoded side plants half the falls across the body. The fallback is the
        // old fixed side when the hand is level with its own pivot.
        float side = i == SideL ? -1f : 1f;
        if (hand[i] != null && Mathf.Abs(hand[i].position.x - pivot.x) > 0.05f)
            side = hand[i].position.x >= pivot.x ? 1f : -1f;
        plantX[i] = pivot.x + side * offset;
        plantY[i] = palmY;
    }

    // Running vote of how this side's world direction moves with its joint angle.
    // It is a DIAGNOSTIC ONLY and no longer drives the aim. Measured p_arms34:
    // the vote came out -1 on BOTH sides while the recorded columns show the true
    // relation dir = rest + theta on both (left rest 142.4, theta -71.2, have
    // 71.1; right rest 270.8, theta -135.5, have 135.4). The drive trusted the
    // vote, so every aim was inverted: the left palm could not reach its own
    // latched point (0.27 m short, phase "plant" for 33 s) and the right arm only
    // reached the slab by the accident of its hinge range. The vote is corrupted
    // by the torso's own rotation, which changes `have` without moving `theta`;
    // the model below has no vote in it at all.
    private void TrackAimSign(int i)
    {
        if (shoulder[i] == null || shoulder[i].attachedRigidbody == null) return;
        float have = UpperArmWorldDeg(i);
        float th = shoulder[i].jointAngle;
        if (aimSignSeen[i])
        {
            float dHave = Mathf.DeltaAngle(aimHavePrev[i], have);
            float dTh = th - aimThPrev[i];
            if (Mathf.Abs(dTh) > 0.4f && Mathf.Abs(dHave) > 0.4f)
                aimSignVote[i] += dHave * dTh > 0f ? 1 : -1;
        }
        aimSignSeen[i] = true;
        aimHavePrev[i] = have;
        aimThPrev[i] = th;
    }

    // World direction the upper arm points, degrees.
    private float UpperArmWorldDeg(int i)
    {
        if (shoulder[i] == null || shoulder[i].attachedRigidbody == null) return 0f;
        Rigidbody2D arm = shoulder[i].attachedRigidbody;
        Vector2 axis = (Vector2)(arm.transform.rotation * Vector3.down);
        return Mathf.Atan2(axis.y, axis.x) * Mathf.Rad2Deg;
    }

    // Centre of the world directions the shoulder can serve: the rest direction
    // (jointAngle 0) the hinge is built around. The reachable window is this
    // +- the joint's range, so an aim outside it is a joint-range question —
    // measured p_arms34 the left arm's window is -18..232 deg world and the
    // right's 110..360, which is why which side is served by which elbow branch
    // is not a mirror question either.
    private float UpperArmRestDeg(int i)
    {
        if (shoulder[i] == null) return 0f;
        return UpperArmWorldDeg(i) - shoulder[i].jointAngle;
    }

    // Joint angle that points the upper arm along a world direction, when that
    // angle exists inside the hinge limits. The world direction of a HingeJoint2D
    // child moves one for one with its joint angle (the joint rotates the child
    // by exactly jointAngle), so the answer is theta + (want - have) — with NO
    // +/-1 sign to guess. The candidate (and its +/-360 representatives, which
    // the wrap needs) is taken if the limits allow it; otherwise the direction is
    // not servable and the caller must pick the other elbow branch.
    private bool JointAngleForDirection(int i, float wantDeg, out float result)
    {
        result = shoulder[i] != null ? shoulder[i].jointAngle : 0f;
        if (shoulder[i] == null) return false;
        float have = UpperArmWorldDeg(i);
        float raw = shoulder[i].jointAngle
            + Mathf.DeltaAngle(have, wantDeg) * Mathf.Clamp01(pressAimGain);
        if (!shoulder[i].useLimits)
        {
            result = raw;
            return true;
        }
        float lo = shoulder[i].limits.min;
        float hi = shoulder[i].limits.max;
        float now = shoulder[i].jointAngle;
        bool found = false;
        float best = Mathf.Clamp(raw, lo, hi);
        for (int k = -1; k <= 1; k++)
        {
            float cand = raw + 360f * k;
            if (cand < lo || cand > hi) continue;
            if (!found || Mathf.Abs(cand - now) < Mathf.Abs(best - now)) best = cand;
            found = true;
        }
        result = best;
        return found;
    }

    // The sign the aim is solved with. It is +1 by construction, not voted: the
    // hinge rotates the child by the joint angle, so d(world direction)/d(theta)
    // is +1 on both sides, mirrored arms included — their difference lives in the
    // rest direction (+142 left against -90 right), not in the derivative.
    private float AimSign(int i)
    {
        return 1f;
    }

    public float PressAimSignL { get { return AimSign(SideL); } }
    public float PressAimSignR { get { return AimSign(SideR); } }
    public float PressAimVoteL { get { return aimSignVote[SideL] >= 0 ? 1f : -1f; } }
    public float PressAimVoteR { get { return aimSignVote[SideR] >= 0 ? 1f : -1f; } }
    // Elbow branch the press is holding: 0 = upper arm at beta+alpha, 1 = beta-alpha,
    // -1 = not solved yet. A press target that flips between the two every step is
    // a posture the PD cannot hold (measured p_arms35 before the branch memory).
    public float PressAimBranchL { get { return aimBranch[SideL]; } }
    public float PressAimBranchR { get { return aimBranch[SideR]; } }

    private float ClampElbow(int i, float target)
    {
        if (elbow[i] != null && elbow[i].useLimits)
            target = Mathf.Clamp(target, elbow[i].limits.min, elbow[i].limits.max);
        return target;
    }

    // ─── muscle write ───

    private void WriteMuscles()
    {
        bool pressing = pushEnabled > 0.5f && down;
        for (int i = 0; i < 2; i++)
        {
            bool armPress = pressing && phase[i] != PhaseIdle;
            float pScale = armPress ? Mathf.Max(1f, pressPGainScale) : 1f;
            if (walkBlend > 0.001f && !armPress)
                pScale = Mathf.Max(pScale, walkPGainScale);

            float shP = (balance != null ? balance.shoulderPGain : 1.5f) * pScale;
            float shD = balance != null ? balance.shoulderDGain : 0.4f;
            float elP = (balance != null ? balance.elbowPGain : 1.2f) * pScale;
            float elD = balance != null ? balance.elbowDGain : 0.4f;
            float shRef = balance != null ? balance.shoulderErrorReferenceDegrees : 25f;
            float elRef = balance != null ? balance.elbowErrorReferenceDegrees : 25f;

            Drive(shoulder[i], outShoulder[i], shP, shD, shRef,
                shFlex[i], shExt[i], ref shoulderInertia[i], outSlew[i]);
            Drive(elbow[i], outElbow[i], elP, elD, elRef,
                elFlex[i], elExt[i], ref elbowInertia[i], outSlew[i]);
        }

        // A press stroke needs authority over the palm's friction; outside the
        // press the muscles go back to their own ceilings.
        float scale = pressing ? Mathf.Max(1f, pressTorqueScale) : 1f;
        for (int i = 0; i < 2; i++)
        {
            SetPair(shFlex[i], shExt[i], shTorqueBase * scale);
            SetPair(elFlex[i], elExt[i], elTorqueBase * scale);
        }
    }

    private static void SetPair(Muscle flexor, Muscle extensor, float torque)
    {
        if (flexor != null) flexor.maxTorque = torque;
        if (extensor != null) extensor.maxTorque = torque;
    }

    // Stable PD, the same algebra BalanceController uses: the error is taken from
    // the next step and the signal is divided by (1 + K*dt/I), so a strong damper
    // cannot overshoot at the arm's small inertia.
    private void Drive(HingeJoint2D joint, float targetAngle, float pGain, float dGain,
                       float errorRef, Muscle flexor, Muscle extensor,
                       ref float cachedInertia, float slewPerSecond)
    {
        if (joint == null || flexor == null || extensor == null) return;

        if (joint.useLimits)
            targetAngle = Mathf.Clamp(targetAngle, joint.limits.min, joint.limits.max);

        float dt = Time.fixedDeltaTime;
        float speedRef = balance != null ? balance.speedReferenceDegPerSec : 200f;
        float angleForError = joint.jointAngle + joint.jointSpeed * dt;

        float error = Mathf.DeltaAngle(angleForError, targetAngle) / Mathf.Max(1f, errorRef);
        float speed = joint.jointSpeed / Mathf.Max(1f, speedRef);
        float raw = pGain * error - dGain * speed;

        float damping = 0f;
        if (cachedInertia <= 0f)
            cachedInertia = EffectiveInertia(joint);
        if (cachedInertia > 0f)
        {
            float k = dGain * flexor.maxTorque * Mathf.Rad2Deg / Mathf.Max(1f, speedRef);
            damping = k * dt / cachedInertia;
        }

        float signal = Mathf.Clamp(raw / (1f + damping), -1f, 1f);
        float speedPerSecond = Mathf.Max(20f, slewPerSecond);
        if (signal > 0f)
        {
            Slew(extensor, signal, speedPerSecond);
            Slew(flexor, 0f, speedPerSecond);
        }
        else
        {
            Slew(extensor, 0f, speedPerSecond);
            Slew(flexor, -signal, speedPerSecond);
        }
    }

    private static void Slew(Muscle muscle, float target, float speedPerSecond)
    {
        if (muscle == null) return;
        muscle.activation = Mathf.MoveTowards(
            muscle.activation, target, speedPerSecond * Time.fixedDeltaTime);
    }

    private static float EffectiveInertia(HingeJoint2D joint)
    {
        if (joint == null) return 0f;
        Rigidbody2D self = joint.attachedRigidbody;
        Rigidbody2D connected = joint.connectedBody;
        float a = self != null ? self.inertia : 0f;
        float b = connected != null ? connected.inertia : 0f;
        if (a <= 0f) return b;
        if (b <= 0f) return a;
        return 1f / (1f / a + 1f / b);
    }

    // Real contact vs the geometric band, kept apart on purpose: "the palm is at
    // the floor" is not "the palm grips the floor", and that difference was the
    // whole diagnosis of the last run.
    private void SampleHandContact()
    {
        for (int i = 0; i < 2; i++)
        {
            bool real = false;
            bool near = false;
            Collider2D col = handCol[i];
            if (col != null && col.enabled)
            {
                real = col.IsTouchingLayers(GroundLayers.Mask);
                near = col.bounds.min.y <= groundTopY + handNearGroundSlop;
            }
            if (i == SideL)
            {
                HandContactL = real;
                HandNearGroundL = near;
            }
            else
            {
                HandContactR = real;
                HandNearGroundR = near;
            }
        }
    }

    private void ParseArgs()
    {
        string[] args = System.Environment.GetCommandLineArgs();
        walkShoulderSwing = ReadFloat(args, ArgShoulderSwing, walkShoulderSwing);
        walkElbowAmp = ReadFloat(args, ArgElbowAmp, walkElbowAmp);
        walkElbowMid = ReadFloat(args, ArgElbowMid, walkElbowMid);
        swingSign = ReadFloat(args, ArgSign, swingSign);
        ipsWeight = ReadFloat(args, ArgIps, ipsWeight);
        crossWeight = ReadFloat(args, ArgCross, crossWeight);
        crossWeightR = ReadFloat(args, ArgCrossR, crossWeightR);
        walkPGainScale = ReadFloat(args, ArgWalkGain, walkPGainScale);
        pushEnabled = ReadFloat(args, ArgPush, pushEnabled);
        pressTorqueScale = ReadFloat(args, ArgPushScale, pressTorqueScale);
        fallTiltDeg = ReadFloat(args, ArgFallTilt, fallTiltDeg);
        plantSideMax = ReadFloat(args, ArgPlantSide, plantSideMax);
        plantLateralShare = ReadFloat(args, ArgPlantShare, plantLateralShare);
        plantStopMarginDeg = ReadFloat(args, ArgPlantStop, plantStopMarginDeg);
        plantReachMargin = ReadFloat(args, ArgPlantReach, plantReachMargin);
        pressDepth = ReadFloat(args, ArgPressDepth, pressDepth);
        pressStrokeMode = ReadFloat(args, ArgPressMode, pressStrokeMode);
        pressDepthExtra = ReadFloat(args, ArgDepthExtra, pressDepthExtra);
        pressDepthRate = ReadFloat(args, ArgDepthRate, pressDepthRate);
        pressPalmLiveDepth = ReadFloat(args, ArgPalmLive, pressPalmLiveDepth);
        hipStatsTau = ReadFloat(args, ArgHipTau, hipStatsTau);
        pressStrokeDegPerSecond = ReadFloat(args, ArgStrokeRate, pressStrokeDegPerSecond);
        pressMinStrokeSeconds = ReadFloat(args, ArgStrokeMin, pressMinStrokeSeconds);
        pressAimGain = ReadFloat(args, ArgAimGain, pressAimGain);
        pressShoulderBias = ReadFloat(args, ArgPressShoulder, pressShoulderBias);
        pressElbowTarget = ReadFloat(args, ArgPressElbow, pressElbowTarget);
        liftHoldSeconds = ReadFloat(args, ArgLiftHold, liftHoldSeconds);
        liftClearanceMeters = ReadFloat(args, ArgLiftClear, liftClearanceMeters);
        pressElbowSlewPerSecond = ReadFloat(args, ArgElbowSlew, pressElbowSlewPerSecond);
        armDebug = ReadFloat(args, ArgDebug, armDebug);
    }

    private static float ReadFloat(string[] args, string name, float fallback)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] != name) continue;
            float v;
            if (float.TryParse(args[i + 1], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out v))
                return v;
        }
        return fallback;
    }
}
