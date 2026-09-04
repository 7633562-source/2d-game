using UnityEngine;

// CoM-based balancer.
[DefaultExecutionOrder(0)]
public class BalanceController : MonoBehaviour
{
    public enum BalanceState
    {
        Balancing,
        Recovery,
        Falling
    }

    [Header("System state")]
    public BalanceState currentState = BalanceState.Balancing;

    [Header("State-machine thresholds")]
    public float recoveryCoMOffset = 0.15f;
    public float fallCoMOffset = 0.30f;

    [Header("Balance gains")]
    // Inputs are normalized: 10 cm of offset and 30 °/s of tilt each give unit.
    // Mixing metres with deg/s used to let torso rate swamp the signal.
    public float comOffsetReference = 0.10f;
    public float tiltSpeedReference = 30f;
    public float comVelocityReference = 0.30f;
    public float comProportionalGain = 1.2f;
    public float comDerivativeGain = 0.35f;
    public float maxBalanceSignal = 1.0f;

    [Header("Target angles (degrees)")]
    // For the hip this is no longer a joint angle but a world pelvis-tilt target:
    // negative is forward (clockwise; the human faces right).
    // The knee still holds jointAngle. The −3/6 pair is the working stance:
    // a slight forward pelvis tilt, shin under the pelvis at knee +6°.
    public float hipBaseAngle = -3f;
    public float kneeBaseAngle = 6f;
    public float kneeRecoveryFlex = 12f;
    public float hipBalanceGain = 8f;

    [Header("Crouch")]
    // intent.crouch is a 0-or-1 target, not a smoothed value.
    // Otherwise the stand's instant crouch=1 would outrun a held key.
    public float crouchRatePerSecond = 1.5f;
    public float crouchReleaseRatePerSecond = 0.9f;
    // Knee flexion is positive: range 0…120, stance +6°.
    // Reshot after the knee flip: 40/32 drops 6.8 cm and holds the lumbar
    // at 17.3° of 20. Past that there is a hole (50/40 falls), though 70/55
    // stands again and drops 20.4 cm. Until the pelvis is controlled, do not
    // go past 40/32: stand vs fall there is not about depth.
    public float crouchKneeFlex = 97f;
    // No longer part of the hip target: we do not hold the pelvis–thigh joint.
    // Field kept so the stand and Inspector keep the name.
    public float crouchHipFlex = 32f;
    // Positive is the forward pelvis-tilt target at crouch=1.
    // Forward = clockwise = minus on the world target, same as crouchTorsoLean.
    // Depth comes from the knee; the pelvis only sets how far to lean.
    public float crouchPelvisTilt = 50f;
    // Positive is extra forward chest lean at crouch=1.
    // The human faces right, so forward = clockwise = minus on the target.
    public float crouchTorsoLean = 24f;
    // Level the pose actually uses. For the stand and Inspector.
    public float crouchLevel;
    // Arms forward-down: a hand on the ground widens support (BodyState/CoM).
    [Tooltip("Shoulder forward-down in crouch, deg (minus on both arms).")]
    public float crouchArmShoulder = 18f;
    [Tooltip("Elbow target: almost straight, hand toward the ground (not −130 — that flexes up).")]
    public float crouchArmElbow = -6f;
    [Tooltip("Wrist target in crouch, deg.")]
    public float crouchArmWrist = -8f;
    [Tooltip("Min crouchLevel for the hands to enter support.")]
    public float crouchHandSupportMin = 0.55f;
    [Tooltip("Slop to y=−2.0: hand counts as grounded in crouch, m.")]
    public float crouchHandGroundSlop = 0.24f;
    [Tooltip("Crouch level where hand-support pose starts blending in.")]
    public float crouchHandPoseStart = 0.55f;
    [Tooltip("Deep crouch shoulder target (forward/down), degrees.")]
    public float crouchHandSupportShoulder = 58f;
    [Tooltip("Deep crouch elbow target, degrees.")]
    public float crouchHandSupportElbow = -10f;
    [Tooltip("Deep crouch wrist target, degrees.")]
    public float crouchHandSupportWrist = -15f;
    [Tooltip("Static shoulder split in deep crouch support pose, degrees.")]
    public float crouchHandSupportSpread = 12f;
    [Tooltip("Additional split from balance signal in deep crouch.")]
    public float crouchHandBalanceSpread = 0f;

    [Header("Trunk lean")]
    // intent.lean: +1 forward (− on the torso/pelvis world target), −1 back.
    // Knees stay put — only chest and pelvis; the ankle holds CoM.
    [Tooltip("Slew rate of leanLevel toward intent.lean, 1/s.")]
    public float leanRatePerSecond = 1.5f;
    [Tooltip("Extra chest tilt at lean=+1, deg (forward = minus on the target).")]
    public float leanTorsoAngle = 12f;
    [Tooltip("Extra pelvis tilt at lean=+1, deg (forward = minus on the target).")]
    public float leanPelvisTilt = 8f;
    // Current level −1…+1 for the pose and Inspector.
    public float leanLevel;

    [Header("One-leg stance")]
    // Swing flex: knee plus first (shorten the leg, lift the foot).
    // Hip minus only after lift-off and then by latch — the target must
    // not be weaker than the angle at lift-off (~37°), or the thigh extends
    // and the foot stomps. 40° holds the leg in the air.
    public float swingHipFlex = 40f;
    public float swingKneeFlex = 55f;
    // Grounded swing flex is a fraction of swingKneeFlex: full flex folds the
    // chain, zero never lifts the foot. After latch — full swingKneeFlex.
    public float swingKneeGroundedFraction = 0.25f;
    // Extra swing signal toward the flexor (decreases jointAngle).
    // While the foot is down, both hips stay on the same pelvis PD: extra
    // flexor unloads swing GRF so the knee can lift the foot instead of
    // folding a closed chain. Do not take the swing off the pelvis while grounded.
    public float swingHipUnloadBias = 0.32f;
    // Right leg ahead (+X), left behind: on left support the swing is forward
    // and 50% grounded knee does not lift it — it needs its own gains.
    public float forwardSwingHipUnloadScale = 1.3f;
    public float forwardSwingKneeGroundScale = 1.4f;
    // On right support the swing is the left leg behind (−X). Pelvis tilt
    // does not work there; unload with the same ideas as the forward swing.
    public float backSwingHipUnloadScale = 1.3f;
    public float backSwingKneeGroundScale = 1.4f;
    // Fraction of standLegPelvisTilt backward on right support. 1.0 = −10°
    // plants both feet (swingFoot 1.0); enable as a fraction via CLI.
    public float backPelvisTiltFraction = 0f;
    // Plantarflex on the grounded forward swing. Default 0: +12° drops
    // left support; enable only via CLI after a separate check.
    public float swingAnkleGroundedToeOff = 0f;
    // Pelvis tilt on left support: unloads the forward swing (right leg).
    // Do not apply on right support — it blocks back-swing lift-off.
    public float standLegPelvisTilt = 10f;
    // During walkActive while split, soften swing unload/knee/tilt
    // (not only when both are grounded: the lift-off frame would restore soft=1).
    // Arm counter-reach is off on walkActive (else elbowLimit↑ and fold).
    [Tooltip("Fraction of swing unload/knee/tilt on walk + split.")]
    public float dualSupportSwingScale = 0.32f;
    // Cap standLegLevel on all of walkActive: if you only cut when both
    // are grounded, a brief lift-off sends level→1 and folds after plant.
    // Cold oneg has walkActive=false — no cap.
    [Tooltip("Max standLegLevel while walkActive. 0 = no cap.")]
    public float dualSupportStandCap = 0.40f;
    // Extra swing unload/knee scale on walk-split on top of dualSoft. Tilt
    // stays on dualSoft — otherwise fold after swap. 1 = as now.
    [Tooltip("Extra unload/knee scale on walk-split (not for tilt). 1 = off.")]
    public float walkSwingLiftScale = 1f;
    // Near the end of Stance — walkSingleSupport. Early stance-only drops.
    [Tooltip("Seconds of Stance before lift starts.")]
    public float walkStanceLiftDelay = 6f;
    [Tooltip("Seconds of liftAge ramp after the delay.")]
    public float walkStanceLiftRamp = 2f;
    // Separate from liftRamp: speeding weight (1.5) worsened swing (0.57)
    // and sat (opt5). Default = liftRamp; CLI -walkWeightRamp for sweeps.
    [Tooltip("Seconds of weightBlend (unload/cap) ramp in Stance with CoM on support.")]
    public float walkWeightRamp = 2f;
    // Run: shorter delay/ramp, else lift cannot finish at runStance 4.
    [Tooltip("walkStanceLiftDelay when intent.run=1.")]
    public float runStanceLiftDelay = 2f;
    [Tooltip("walkWeightRamp when intent.run=1.")]
    public float runWeightRamp = 1.5f;
    // Lift only when CoM is over the stance foot, not by timer alone:
    // else delay=4 + lift=2.5 still folds (~22 s, walk_l25_d4).
    [Tooltip("|CoM−stance| ≤ this on walk dual-support to enable lift.")]
    public float walkLiftComMax = 0.10f;
    // Swing toe-off only on walk at high liftBlend; global
    // swingAnkleGroundedToeOff on dual-support drops (~17 s).
    [Tooltip("Max swing toe-off on walk at liftBlend=1, deg. Forward swing only. 0 = off.")]
    public float walkSwingToeOffMax = 0f;
    // Extra unload and knee flex from liftAct — CLI only; default 0.
    [Tooltip("Extra hip-unload at liftAct=1 on walk-split.")]
    public float walkLiftUnloadBias = 0.02f;
    [Tooltip("Fraction of swingKneeFlex on the grounded swing at liftAct=1.")]
    public float walkKneePeelMax = 0.05f;
    // Stance-foot push at full weightBlend: CoM over support alone
    // does not lift the swing (ankleBalance≈0). Not a PD to an angle —
    // only a signal into ankleBalance. 0 = off. CLI -walkStancePush.
    [Tooltip("Extra plantar on the stance ankle at weightBlend≈1. 0 = off.")]
    public float walkStancePush = 0.05f;
    [Tooltip("walkStancePush scale for run. 0 = no push-off in run.")]
    public float runStancePushScale = 0f;
    [Tooltip("Target forward COM speed in walk, m/s.")]
    public float walkTargetSpeed = 1.0f;
    [Tooltip("Additional stance push per (target-current) speed, 1/s.")]
    public float walkSpeedPushGain = 0.35f;
    [Tooltip("Clamp for speed-based extra stance push.")]
    public float walkSpeedPushMax = 0.25f;
    [Tooltip("Multiplier for |stanceComOff| gate when speed-push is active.")]
    public float walkSpeedPushComGateScale = 2f;
    [Tooltip("Use XCoM in stance ankle error (0=CoM, 1=full XCoM).")]
    public float walkXCoMWeight = 0.6f;
    [Tooltip("Effective COM height (m) for XCoM omega0 = sqrt(g/h).")]
    public float walkXCoMHeight = 1.0f;
    [Tooltip("Auto lean gain from walk speed error, deg per (m/s).")]
    public float walkSpeedLeanGain = 8f;
    [Tooltip("Clamp for auto lean generated by speed error, deg.")]
    public float walkSpeedLeanMax = 6f;
    [Tooltip("Torso share of auto lean relative to pelvis lean.")]
    public float walkSpeedLeanTorsoScale = 1.2f;
    [Tooltip("Direct stance-ankle drive gain from walk speed error, 1/s.")]
    public float walkSpeedDriveGain = 0f;
    [Tooltip("Clamp for direct speed drive applied to stance ankle.")]
    public float walkSpeedDriveMax = 0.5f;
    [Tooltip("Minimum standLegLevel before direct speed drive applies.")]
    public float walkSpeedDriveStartLevel = 0.2f;
    [Tooltip("Additional swingHipFlex per 1 m/s speed error, deg.")]
    public float walkSpeedSwingHipGain = 10f;
    [Tooltip("Additional swingKneeFlex per 1 m/s speed error, deg.")]
    public float walkSpeedSwingKneeGain = 14f;
    [Tooltip("Clamp for speed-based swing flex boost, deg.")]
    public float walkSpeedSwingFlexMax = 16f;
    [Tooltip("Base extra swing hip flex on walk, deg.")]
    public float walkSwingHipBoost = 6f;
    [Tooltip("Base extra swing knee flex on walk, deg.")]
    public float walkSwingKneeBoost = 10f;
    [Tooltip("Desired step length from stance to swing foot on walk, m.")]
    public float walkStepLength = 0.22f;
    [Tooltip("Extra desired step length per 1 m/s target speed, m.")]
    public float walkStepLengthSpeedGain = 0f;
    [Tooltip("Hip target correction from step-length error, deg per m.")]
    public float walkStepPlacementGain = 0f;
    [Tooltip("Clamp for step-placement hip correction, deg.")]
    public float walkStepPlacementMax = 0f;
    [Tooltip("Fraction of step-placement correction applied while grounded.")]
    public float walkStepPlacementGroundFraction = 0f;
    [Tooltip("Stance phase progress when placement control becomes active.")]
    public float walkPlacementSyncStart = 0.72f;
    [Tooltip("Ground clearance threshold (m) for touchdown-sync window.")]
    public float walkTouchdownSyncClearance = 0.06f;
    [Tooltip("Extra stance knee bend on walk, deg.")]
    public float walkStanceKneeBend = 10f;
    [Tooltip("Base forward pelvis lean on walk, deg.")]
    public float walkForwardPelvisLean = 2f;
    [Tooltip("Base forward torso lean on walk, deg.")]
    public float walkForwardTorsoLean = 4f;
    // Stance extend at full weightBlend: pelvis up → swing loses GRF.
    // Not swing flex (holdknee/gk25 fold). 0 = off. CLI -walkStanceExtend.
    [Tooltip("Stance-extend fraction (knee/pelvis→0) at weightBlend=1. 0 = off.")]
    public float walkStanceExtend = 0f;
    // Walking is a controlled forward fall, not stepping in place. While the
    // ankle target stays "CoM over support", it pulls the body back, and a
    // support swap travels 0.21 m in 35 s. Here the target is walkComLeadX
    // ahead of the stance foot: gravity supplies the drive, the swing plants.
    // No invisible force. 0 = former behaviour. CLI -walkComLead.
    [Tooltip("Metres ahead of support to hold CoM when moveX≠0. 0 = stepping in place.")]
    public float walkComLeadX = 0f;
    // Swing pose turns on only after lift-off (swingHipLatched), and lift-off
    // needs the thigh already forward — a closed loop: both hips hold the
    // pelvis upright, neither steps out, the foot never leaves the ground
    // (swingFoot ≈ 1.0, travel 0.12 m in 35 s). The scissor gives the swing
    // a forward pose while still grounded, once weight is on support.
    // 0 = off, former behaviour. CLI -walkSwingScissor.
    [Tooltip("standLegLevel from which the swing reaches forward while still grounded. 0 = off.")]
    public float walkSwingScissorLevel = 0.20f;
    // Reaching the thigh forward throws the pelvis back (third law): full
    // −swingHipFlex on the ground does lift off (swingFoot 0.50 vs 0.999),
    // but the human travels backward and folds in 7 s. Grounded reach is
    // a fraction; in the air the pose stays full. CLI -walkSwingScissorFlex.
    [Tooltip("Fraction of swingHipFlex in the grounded scissor pose. 1 = full reach.")]
    public float walkSwingScissorFlex = 0.25f;
    // After a scrape hipair goes upright again and plants the foot. Hold
    // flex for hold more seconds while grounded (not a commit before first air). CLI, default 0.
    [Tooltip("Seconds to hold swing-hip flex after !grounded. 0 = off.")]
    public float walkSwingAirHold = 0f;
    // After latch: keep hip flex until the heel is loaded. The toe does not plant.
    // CLI -walkSwingHeelPlant 1; default 0 = hipair (!grounded).
    [Tooltip("1 = swing pose from !heelLoaded after latch. 0 = hipair.")]
    public float walkSwingHeelPlant = 0f;
    // After a scrape hipair keeps the latch → grounded knee stays off. Clearing
    // the latch on plant restores groundKnee without kneer (that one left latch).
    // CLI -walkSwingUnlatchPlant 1; default 0.
    [Tooltip("1 = clear swingHipLatched when the swing is grounded. 0 = sticky latch.")]
    public float walkSwingUnlatchPlant = 0f;
    // Latch from the first air frame kills groundKnee on plant.
    // Continuous !grounded (s) required before latch. 0 = hipair (immediate).
    [Tooltip("Seconds of continuous air before latch. 0 = from the first frame.")]
    public float walkSwingLatchAir = 0f;
    // In the air hipair holds ankle joint=0 (foot ⊥ shin) → knee flex
    // points the toe down and it scrapes. 1 = world-horizontal foot target.
    [Tooltip("1 = after latch in the air, foot to world horizontal. 0 = joint 0.")]
    public float walkSwingAirLevel = 0f;
    // While liftBlend grows, level catches the cap faster — else unload
    // is weak at cap 0.4→1 and rate 0.5/s.
    [Tooltip("standLegRate scale at liftBlend=1 on walk. 1 = off.")]
    public float walkLiftLevelRateScale = 1f;
    // Debug: actual dual-support scale from the last FixedUpdate.
    [System.NonSerialized] public float lastDualSoft = 1f;
    [System.NonSerialized] public float lastLiftBlend;
    public float standLegRatePerSecond = 1.5f;
    [Tooltip("Release of the one-leg pose at standLeg=0 — faster than lift so the transfer window does not drift.")]
    public float standLegReleaseRatePerSecond = 4f;
    // 0 — dual support, 1 — full swing pose. Smoothed like crouchLevel.
    public float standLegLevel;

    // Error and speed are divided by references, so the PD input is
    // dimensionless and the gains are order-one. Before that, 0.03° of
    // error was enough to slam activation to 1: the loop was a switch
    // and always ran at full power.
    [Header("PD input scaling")]
    public float errorReferenceDegrees = 10f;
    public float speedReferenceDegPerSec = 200f;

    [Header("Joint PD")]
    // hipP/hipD do not feed an angle in dual support: the pelvis is held
    // to world vertical by pelvisPGain/pelvisDGain. In one-leg stance the
    // swing hip uses them, and only after the foot lifts.
    public float hipPGain = 1.5f;
    public float hipDGain = 0.4f;
    public float kneePGain = 1.2f;
    public float kneeDGain = 0.5f;
    public float neckPGain = 1.5f;
    public float neckDGain = 0.4f;
    // Neck and head target world vertical, not the angle to the parent.
    // Their own error reference, like lumbar and pelvis: ComputeSignal used
    // 10°, and that saturated the joint from fractions of a degree.
    public float neckTargetTilt = 0f;
    public float neckErrorReferenceDegrees = 25f;
    // Stable PD only for neck and head. The flag stays so the stand can
    // compare the old behaviour on the same build: without it a fix and a
    // rebuild look the same. Arms are always on SPD — see the block below.
    public bool useStablePd = true;
    [Header("Arms")]
    // Joint pose, not world vertical: otherwise the arm is a pendulum and swings.
    // Elbow flexion is minus (−140…0). Zero is the "straight arm" stop; without
    // a muscle gravity held the joint there ~98% of the time. Target a bit minus.
    // SPD always: elbow K·Δt/Ieff ≈ 11.8, wrist ≈ 5.1, shoulder ≈ 3.4 —
    // explicit PD is unstable. useStablePd does not apply to the arms.
    public float shoulderPGain = 1.5f;
    public float shoulderDGain = 0.4f;
    public float shoulderBaseAngle = 0f;
    public float shoulderErrorReferenceDegrees = 25f;
    public float elbowPGain = 1.2f;
    public float elbowDGain = 0.4f;
    public float elbowBaseAngle = -20f;
    public float elbowErrorReferenceDegrees = 25f;
    public float wristPGain = 1.0f;
    public float wristDGain = 0.4f;
    public float wristBaseAngle = 0f;
    public float wristErrorReferenceDegrees = 25f;
    // Target offset from balanceSignal (CoM + tilt rate): CoM ahead —
    // shoulders plus (hand back), elbow toward zero. Zero — static pose.
    public float armShoulderBalanceGain = 25f;
    public float armElbowBalanceGain = 10f;
    public float armWristBalanceGain = 0f;
    // Arm swing on walk: opposite the legs, sin of Stance phase. Not CoM-balance —
    // that one gave elbowLimit and fold after swap.
    [Tooltip("Shoulder amplitude on walk, deg (sin of Stance phase).")]
    public float walkArmShoulderSwing = 15f;
    [Tooltip("Extra forward elbow flex on walk, deg.")]
    public float walkArmElbowSwing = 5f;
    [Tooltip("Fraction of arm amplitude in Transfer (decay). 0 = base pose.")]
    public float walkArmTransferCarry = 0.35f;
    [Tooltip("Balance-to-shoulder gain during walk arm swing.")]
    public float walkArmBalanceShoulderGain = 4f;
    [Tooltip("Balance-to-elbow gain during walk arm swing.")]
    public float walkArmBalanceElbowGain = 2f;
    [Tooltip("Additional late-stance ankle push near phase end.")]
    public float walkLatePushGain = 0f;
    [Tooltip("Stance progress where late push starts (0..1).")]
    public float walkLatePushStart = 0.72f;
    [Tooltip("Clamp for late-stance ankle push signal.")]
    public float walkLatePushMax = 0f;
    [Tooltip("Require swing airborne for late push-off.")]
    public bool walkLatePushNeedsAir = true;
    [Tooltip("Scale late push by touchdown window (0/1).")]
    public bool walkLatePushNeedsTouchdownWindow = true;
    [Header("Pelvis to world vertical")]
    // Same as lumbar: error and speed come only from VestibularSystem.
    // The hip joint lives on the thigh, the pelvis is connectedBody, so the
    // P/D sign matches ControlJointToAngle, not lumbar: a positive
    // signal turns the pelvis counterclockwise (increases jointAngle).
    // P and D are plus; a negative value inverts the loop.
    public float pelvisPGain = 2.0f;
    public float pelvisDGain = 0.3f;
    public float pelvisErrorReferenceDegrees = 25f;
    [Header("Lumbar")]
    // Reference is the chest world vertical, not the angle to the pelvis.
    // Otherwise when the pelvis topples the lumbar rides 35 kg of chest down with it.
    // Error and speed only from VestibularSystem: mixing world tilt with
    // joint.jointSpeed is forbidden — those are different frames.
    public float lumbarTargetTilt = 0f;
    // P and D are plus. In the formula below they enter as −P and −D because
    // the error is still target − tilt: that keeps the same arithmetic as
    // the working pair P=−2, D=−0.3, without flipping the damper sign.
    public float lumbarPGain = 2.0f;
    public float lumbarDGain = 0.3f;
    public float lumbarErrorReferenceDegrees = 25f;
    [Header("Ankle as pendulum")]
    // Torque against CoM offset, not against the joint angle.
    // Overturning moment is m*g*d ≈ 687*d N·m. On a 1.75 m body the
    // CoM is lower and inertia is smaller: former P=7 and D=4 left torso
    // sway and offset RMS worse than the baseline. P=14 holds CoM tighter,
    // D=8 kills speed after the startup squat.
    public float ankleComP = 14f;
    public float ankleComD = 8f;
    public float ankleLimitMargin = 15f;

    [Header("Muscle activation speed")]
    public float muscleActivationSpeed = 20f;

    [Header("Leg muscles")]
    public Muscle leftHipFlexor;
    public Muscle leftHipExtensor;
    public Muscle rightHipFlexor;
    public Muscle rightHipExtensor;
    public Muscle leftKneeFlexor;
    public Muscle leftKneeExtensor;
    public Muscle rightKneeFlexor;
    public Muscle rightKneeExtensor;
    public Muscle leftAnkleFlexor;
    public Muscle leftAnkleExtensor;
    public Muscle rightAnkleFlexor;
    public Muscle rightAnkleExtensor;

    [Header("Lumbar muscles")]
    public Muscle lumbarFlexor;
    public Muscle lumbarExtensor;

    [Header("Neck and head muscles")]
    public Muscle neckFlexor;
    public Muscle neckExtensor;
    public Muscle headFlexor;
    public Muscle headExtensor;

    [Header("Arm muscles")]
    public Muscle leftShoulderFlexor;
    public Muscle leftShoulderExtensor;
    public Muscle rightShoulderFlexor;
    public Muscle rightShoulderExtensor;
    public Muscle leftElbowFlexor;
    public Muscle leftElbowExtensor;
    public Muscle rightElbowFlexor;
    public Muscle rightElbowExtensor;
    public Muscle leftWristFlexor;
    public Muscle leftWristExtensor;
    public Muscle rightWristFlexor;
    public Muscle rightWristExtensor;

    private HingeJoint2D leftHipJoint;
    private HingeJoint2D rightHipJoint;
    private HingeJoint2D leftKneeJoint;
    private HingeJoint2D rightKneeJoint;
    private HingeJoint2D leftAnkleJoint;
    private HingeJoint2D rightAnkleJoint;
    private HingeJoint2D lumbarJoint;
    private HingeJoint2D neckJoint;
    private HingeJoint2D headJoint;
    private HingeJoint2D leftShoulderJoint;
    private HingeJoint2D rightShoulderJoint;
    private HingeJoint2D leftElbowJoint;
    private HingeJoint2D rightElbowJoint;
    private HingeJoint2D leftWristJoint;
    private HingeJoint2D rightWristJoint;

    // Paired inertia is lazy, on the first FixedUpdate: in Awake
    // Rigidbody2D.inertia has not yet been recomputed from the collider.
    private float neckEffectiveInertia;
    private float headEffectiveInertia;
    private float leftShoulderEffectiveInertia;
    private float rightShoulderEffectiveInertia;
    private float leftElbowEffectiveInertia;
    private float rightElbowEffectiveInertia;
    private float leftWristEffectiveInertia;
    private float rightWristEffectiveInertia;
    private float lastWalkStanceProgress;
    private float leftKneeEffectiveInertia;
    private float rightKneeEffectiveInertia;
    private float leftHipEffectiveInertia;
    private float rightHipEffectiveInertia;
    private float leftAnkleEffectiveInertia;
    private float rightAnkleEffectiveInertia;

    private VestibularSystem vestibularSystem;
    private CenterOfMassCalculator comCalculator;
    private BodyStateEstimator bodyState;
    private MotionIntent intent;
    private StepPhaseDriver stepPhaseDriver;
    // Looked up once: a population has no driver, GetComponent every 200 Hz
    // step will never find one. Late-add — only Bind/Ensure, not FixedUpdate.
    private bool stepPhaseDriverSearched;
    // After the first swing lift-off do not return the hip to the pelvis
    // because of a foot scrape: else the thigh extends and the leg stomps back.
    private bool swingHipLatched;
    // Remaining flex hold after a scrape (walkSwingAirHold).
    private float swingAirHoldRemain;
    // Continuous swing !grounded for walkSwingLatchAir.
    private float swingAirStreak;
    // Last support command: on a sign change reset the level and the latch.
    private float heldStandCmd;
    // Swing angle on the latch frame. After lift-off the target is not weaker
    // than this flex: Min(angleAtLatch, −swingHipFlex) — more negative = more flex.
    private float swingHipAngleAtLatch;

    void Awake()
    {
        vestibularSystem = GetComponent<VestibularSystem>();
        comCalculator = GetComponent<CenterOfMassCalculator>();
        bodyState = GetComponent<BodyStateEstimator>();
        intent = GetComponent<MotionIntent>();
        CacheStepPhaseDriverOnce();

        leftHipJoint = GetJoint("LeftLegThigh");
        rightHipJoint = GetJoint("RightLegThigh");
        leftKneeJoint = GetJoint("LeftLegShin");
        rightKneeJoint = GetJoint("RightLegShin");
        leftAnkleJoint = GetJoint("LeftLegFoot");
        rightAnkleJoint = GetJoint("RightLegFoot");
        lumbarJoint = GetJoint("Torso");
        neckJoint = GetJoint("Neck");
        headJoint = GetJoint("Head");
        leftShoulderJoint = GetJoint("LeftArmUpper");
        rightShoulderJoint = GetJoint("RightArmUpper");
        leftElbowJoint = GetJoint("LeftArmLower");
        rightElbowJoint = GetJoint("RightArmLower");
        leftWristJoint = GetJoint("LeftArmHand");
        rightWristJoint = GetJoint("RightArmHand");
    }

    // One lookup. HeadlessTrial attaches the driver before BuildHuman, so Awake sees it.
    // If AddComponent moves after Awake — explicit Bind/Ensure from outside.
    private void CacheStepPhaseDriverOnce()
    {
        if (stepPhaseDriverSearched) return;
        stepPhaseDriver = GetComponent<StepPhaseDriver>();
        stepPhaseDriverSearched = true;
    }

    public void BindStepPhaseDriver(StepPhaseDriver driver)
    {
        stepPhaseDriver = driver;
        stepPhaseDriverSearched = true;
    }

    // Repeat GetComponent only on an explicit call after a late-add.
    public void EnsureStepPhaseDriver()
    {
        stepPhaseDriverSearched = false;
        CacheStepPhaseDriverOnce();
    }

    private HingeJoint2D GetJoint(string childName)
    {
        Transform t = transform.Find(childName);
        return t != null ? t.GetComponent<HingeJoint2D>() : null;
    }

    void FixedUpdate()
    {
        float comOffset = comCalculator != null ? comCalculator.GetCoMOffsetX() : 0f;
        float angularVelocity = vestibularSystem != null ? vestibularSystem.GetBodyAngularVelocity() : 0f;

        UpdateCrouchLevel();
        UpdateLeanLevel();
        UpdateStandLegLevel();

        bool walkSoft = stepPhaseDriver != null && stepPhaseDriver.walkActive;
        float standCmd = intent != null ? intent.standLeg : 0f;
        if (!walkSoft)
        {
            bool requestSplit = Mathf.Abs(standCmd) > 0.5f;
            if (requestSplit)
            {
                float requestSign = Mathf.Sign(standCmd);
                bool hadSplit = Mathf.Abs(heldStandCmd) > 0.5f;
                if (!hadSplit || Mathf.Sign(heldStandCmd) != requestSign)
                {
                    standLegLevel = 0f;
                    swingHipLatched = false;
                }
                heldStandCmd = requestSign;
            }
            else if (standLegLevel <= 0.001f)
            {
                heldStandCmd = 0f;
            }

            standCmd = heldStandCmd;
        }
        else
        {
            heldStandCmd = 0f;
        }

        bool splitLegs = standLegLevel > 0.001f && Mathf.Abs(standCmd) > 0.5f;
        bool leftIsSwing = splitLegs && standCmd > 0.5f;
        bool rightIsSwing = splitLegs && standCmd < -0.5f;
        bool leftGrounded = bodyState != null && bodyState.leftFootGrounded;
        bool rightGrounded = bodyState != null && bodyState.rightFootGrounded;
        bool walkDualSupport = walkSoft && leftGrounded && rightGrounded;

        float stanceComOff = 0f;
        if (splitLegs && bodyState != null && comCalculator != null)
        {
            Vector2 comSt = comCalculator.GetCenterOfMass();
            float stanceX = leftIsSwing ? bodyState.rightFootPosition.x
                : bodyState.leftFootPosition.x;
            stanceComOff = comSt.x - stanceX;
        }

        float liftAge = 0f;
        bool running = intent != null && intent.run > 0.5f;
        float activeLiftDelay = running ? runStanceLiftDelay : walkStanceLiftDelay;
        float activeWeightRamp = running && runWeightRamp > 0.01f
            ? runWeightRamp
            : (walkWeightRamp > 0.01f ? walkWeightRamp : walkStanceLiftRamp);
        if (walkSoft && splitLegs && stepPhaseDriver != null
            && stepPhaseDriver.PhaseCode == 0)
        {
            float age = stepPhaseDriver.PhaseAge();
            if (!running && stepPhaseDriver.SwapCount == 0)
                age += activeLiftDelay;
            liftAge = Mathf.Clamp01(
                (age - activeLiftDelay) / Mathf.Max(0.05f, walkStanceLiftRamp));
        }

        float comLiftGate = 0f;
        if (walkSoft && splitLegs)
        {
            float absSt = Mathf.Abs(stanceComOff);
            float comMax = Mathf.Max(0.01f, walkLiftComMax);
            if (absSt <= comMax)
                comLiftGate = 1f;
            else if (absSt <= comMax * 2f)
                comLiftGate = 1f - (absSt - comMax) / comMax;
        }

        // Weight already on support — unload like single-support, do not wait delay=6.
        // Else dualSoft=0.35 and cap=0.4 for all of Stance, the foot never lifts
        // (walk1_lift swingFoot 0.9998). Weight ramp is separate from liftRamp.
        // Tilt stays on dualSoft: full tilt on right support plants both feet.
        // Ease-out / soft gate (opt8*) — fold at the same swing ~0.42; keep linear.
        float weightBlend = 0f;
        if (walkSoft && splitLegs && stepPhaseDriver != null
            && stepPhaseDriver.PhaseCode == 0 && comLiftGate >= 1f)
        {
            float age = stepPhaseDriver.PhaseAge();
            weightBlend = Mathf.Clamp01(age / Mathf.Max(0.05f, activeWeightRamp));
        }

        // Smooth lift: cap, unload and ankle grow together; a jump of
        // cap 0.4→1 and liftMult 1→scale at once dropped (~21 s).
        float liftBlend = 0f;
        if (walkSoft && splitLegs && stepPhaseDriver != null
            && stepPhaseDriver.PhaseCode == 0)
            liftBlend = liftAge * comLiftGate;
        lastLiftBlend = liftBlend;
        // Unload/knee is squared: early dual-support does not jerk, end of Stance is sharper.
        float liftAct = liftBlend * liftBlend;

        // Both feet down, CoM over support — the loop looks like single-support.
        bool walkSingleSupport = walkDualSupport && splitLegs && liftBlend >= 0.5f;

        // Cap level by a ramp, not by lifting the clamp: else unload never lifts the foot.
        // capBlend takes both timed lift and weight on support — else cap=0.4
        // keeps unload at zero for almost all of Stance.
        if (walkSoft && dualSupportStandCap > 0.01f)
        {
            float cap = dualSupportStandCap;
            float capBlend = Mathf.Max(liftBlend, weightBlend);
            if (capBlend > 0.001f)
                cap = Mathf.Lerp(dualSupportStandCap, 1f, capBlend);
            if (standLegLevel > cap)
                standLegLevel = cap;
        }

        // Fall from the stance foot — true single-support or liftBlend.
        float fallOffset = comOffset;
        bool singleSupport = splitLegs && (
            (leftIsSwing && !leftGrounded && rightGrounded) ||
            (rightIsSwing && !rightGrounded && leftGrounded));
        if (singleSupport && splitLegs)
            fallOffset = stanceComOff;
        else if (walkDualSupport && splitLegs && liftBlend > 0.001f)
            fallOffset = Mathf.Lerp(comOffset, stanceComOff, liftBlend);
        UpdateState(fallOffset);

        if (currentState == BalanceState.Falling)
        {
            RelaxAllMuscles();
            return;
        }

        float offsetN = comOffset / Mathf.Max(0.01f, comOffsetReference);
        float tiltSpeedN = angularVelocity / Mathf.Max(1f, tiltSpeedReference);
        float balanceSignal = Mathf.Clamp(
            comProportionalGain * offsetN + comDerivativeGain * tiltSpeedN,
            -maxBalanceSignal, maxBalanceSignal);

        float comVelX = bodyState != null ? bodyState.comVelocity.x : 0f;
        float walkMoveDir = 0f;
        float walkSpeedError = 0f;
        if (walkSoft && !running && intent != null && Mathf.Abs(intent.moveX) > 0.01f)
        {
            walkMoveDir = Mathf.Sign(intent.moveX);
            float forwardSpeed = comVelX * walkMoveDir;
            walkSpeedError = Mathf.Max(0f, walkTargetSpeed - forwardSpeed);
        }
        float walkAutoLean = 0f;
        float walkAutoTorsoLean = 0f;
        if (walkMoveDir != 0f)
        {
            float leanMag = Mathf.Clamp(
                walkSpeedError * Mathf.Max(0f, walkSpeedLeanGain),
                0f,
                Mathf.Max(0f, walkSpeedLeanMax));
            walkAutoLean = leanMag * walkMoveDir;
            walkAutoTorsoLean = walkAutoLean * Mathf.Max(0f, walkSpeedLeanTorsoScale);
        }
        float walkBasePelvisLean = 0f;
        float walkBaseTorsoLean = 0f;
        if (walkMoveDir != 0f)
        {
            walkBasePelvisLean = walkMoveDir * Mathf.Max(0f, walkForwardPelvisLean);
            walkBaseTorsoLean = walkMoveDir * Mathf.Max(0f, walkForwardTorsoLean);
        }

        // Pelvis holds world vertical, not the angle to the thigh: else the
        // leg sway copies one-to-one into the pelvis and the lumbar uses the
        // whole ±20° travel. hipBalanceGain still leans the target toward CoM.
        // crouchHipFlex is not in the target — crouch sets pelvis tilt.
        float pelvisTarget = Mathf.Clamp(
            hipBaseAngle - hipBalanceGain * balanceSignal
                - crouchLevel * crouchPelvisTilt - leanLevel * leanPelvisTilt - walkAutoLean - walkBasePelvisLean,
            -60f, 60f);
        // Stance extend: pelvis toward vertical at full weightBlend.
        if (walkSoft && walkStanceExtend > 0.001f && weightBlend > 0.001f)
            pelvisTarget = Mathf.Lerp(pelvisTarget, 0f, Mathf.Clamp01(walkStanceExtend * weightBlend));
        float kneeTarget = kneeBaseAngle + crouchLevel * crouchKneeFlex;
        // Recovery knee flex on dual-support split folds the chain
        // after transfer. On walkSingleSupport — same as one-leg.
        if (currentState == BalanceState.Recovery
            && !(splitLegs && leftGrounded && rightGrounded && !walkSingleSupport))
            kneeTarget += kneeRecoveryFlex;
        kneeTarget = Mathf.Clamp(kneeTarget, 0f, 115f);

        float velN = comVelX / Mathf.Max(0.05f, comVelocityReference);

        // Soft dual-support. Unload/knee move toward one-leg with weightBlend;
        // tilt stays soft (else right support plants both feet).
        float dualSoft = 1f;
        if (walkSoft && splitLegs)
            dualSoft = Mathf.Clamp01(dualSupportSwingScale);
        float tiltSoft = dualSoft;
        float unloadSoft = Mathf.Lerp(dualSoft, 1f, weightBlend);
        float liftMult = Mathf.Lerp(1f, Mathf.Max(1f, walkSwingLiftScale), liftAct);
        float liftSoft = unloadSoft * liftMult;
        lastDualSoft = dualSoft;

        // Unload the swing by pelvis tilt:
        // (swing on the right). On right support tilt plants both feet.
        if (splitLegs && standLegPelvisTilt > 0.01f && standCmd < -0.5f)
        {
            pelvisTarget = Mathf.Clamp(
                pelvisTarget + standLegLevel * standLegPelvisTilt * tiltSoft,
                -60f, 60f);
        }
        else if (splitLegs && standLegPelvisTilt > 0.01f && standCmd > 0.5f
                 && backPelvisTiltFraction > 0.001f)
        {
            pelvisTarget = Mathf.Clamp(
                pelvisTarget - standLegLevel * standLegPelvisTilt * backPelvisTiltFraction * tiltSoft,
                -60f, 60f);
        }

        // CoM ahead turns the body forward.
        // Walk Stance: support from the stance foot — else mid holds CoM
        // between the feet, comLiftGate/liftBlend never open, the swing never lifts.
        // Transfer: mid (lerp by liftBlend), as in walk_mid — else fold on swap.
        bool walkStancePhase = walkSoft && stepPhaseDriver != null
            && stepPhaseDriver.PhaseCode == 0;
        float walkStanceProgress = 0f;
        if (walkStancePhase && stepPhaseDriver != null)
        {
            float phaseDur = Mathf.Max(0.1f, stepPhaseDriver.stanceDuration);
            walkStanceProgress = Mathf.Clamp01(stepPhaseDriver.PhaseAge() / phaseDur);
        }
        lastWalkStanceProgress = walkStanceProgress;
        // Shift the target ahead of support: the ankle stops pulling the body
        // back, and the human travels where moveX asks.
        float comLead = 0f;
        if (walkSoft && intent != null && walkComLeadX > 0.0001f)
            comLead = walkComLeadX * Mathf.Clamp(intent.moveX, -1f, 1f);
        float comRef = Mathf.Max(0.01f, comOffsetReference);
        float ankleOffsetN = (comOffset - comLead) / comRef;
        float xcomShift = 0f;
        float xcomW = Mathf.Clamp01(walkXCoMWeight);
        if (walkSoft && xcomW > 0.0001f)
        {
            float h = Mathf.Max(0.1f, walkXCoMHeight);
            float omega0 = Mathf.Sqrt(9.81f / h);
            xcomShift = comVelX / Mathf.Max(0.1f, omega0);
        }
        float comOffsetXCoM = comOffset + xcomShift;
        if (splitLegs && bodyState != null && comCalculator != null)
        {
            float stanceN = (stanceComOff - comLead) / comRef;
            float stanceXCoMN = ((stanceComOff + xcomShift) - comLead) / comRef;
            if (walkDualSupport)
            {
                float baseN = walkStancePhase
                    ? stanceN
                    : Mathf.Lerp((comOffset - comLead) / comRef, stanceN, liftBlend);
                float baseXCoMN = walkStancePhase
                    ? stanceXCoMN
                    : Mathf.Lerp((comOffsetXCoM - comLead) / comRef, stanceXCoMN, liftBlend);
                ankleOffsetN = Mathf.Lerp(baseN, baseXCoMN, xcomW);
            }
            else
                ankleOffsetN = Mathf.Lerp(stanceN, stanceXCoMN, xcomW);
        }
        else if (xcomW > 0.0001f)
            ankleOffsetN = Mathf.Lerp(ankleOffsetN, (comOffsetXCoM - comLead) / comRef, xcomW);
        float ankleBalance = Mathf.Clamp(
            ankleComP * ankleOffsetN + ankleComD * velN,
            -1f, 1f);

        if (!splitLegs)
        {
            swingHipLatched = false;
            swingAirHoldRemain = 0f;
            swingAirStreak = 0f;
        }
        else if ((leftIsSwing && !leftGrounded) || (rightIsSwing && !rightGrounded))
        {
            swingAirStreak += Time.fixedDeltaTime;
            // 0 = hipair: latch from the first frame. Else — after the streak.
            float needAir = walkSwingLatchAir > 0.001f ? walkSwingLatchAir : 0f;
            if (swingAirStreak >= needAir - 1e-6f)
            {
                if (!swingHipLatched)
                {
                    HingeJoint2D swingJoint = leftIsSwing ? leftHipJoint : rightHipJoint;
                    swingHipAngleAtLatch = swingJoint != null ? swingJoint.jointAngle : -swingHipFlex;
                }
                swingHipLatched = true;
                if (walkSwingAirHold > 0.001f)
                    swingAirHoldRemain = walkSwingAirHold;
            }
        }
        else
        {
            swingAirStreak = 0f;
            if (walkSwingUnlatchPlant > 0.5f && swingHipLatched
                && ((leftIsSwing && leftGrounded) || (rightIsSwing && rightGrounded)))
            {
                swingHipLatched = false;
                swingAirHoldRemain = 0f;
            }
            else if (swingAirHoldRemain > 0f)
                swingAirHoldRemain = Mathf.Max(0f, swingAirHoldRemain - Time.fixedDeltaTime);
        }

        // −swingHipFlex pose: hipair = air only; heelPlant = until
        // the heel plants (the toe does not drop flex). airHold is a timer, rejected.
        bool heelPlant = walkSwingHeelPlant > 0.5f;
        bool leftHeel = bodyState != null && bodyState.leftFootHeelLoaded;
        bool rightHeel = bodyState != null && bodyState.rightFootHeelLoaded;
        bool airHold = swingAirHoldRemain > 0.001f;
        // Scissor breaks the "pose only after lift-off" loop.
        bool scissor = walkSoft && splitLegs && walkSwingScissorLevel > 0.001f
            && standLegLevel >= walkSwingScissorLevel;
        bool leftAir = swingHipLatched && (heelPlant ? !leftHeel : (!leftGrounded || airHold));
        bool rightAir = swingHipLatched && (heelPlant ? !rightHeel : (!rightGrounded || airHold));
        bool leftSwingHip = leftIsSwing && swingHipFlex > 0.5f && (leftAir || scissor);
        bool rightSwingHip = rightIsSwing && swingHipFlex > 0.5f && (rightAir || scissor);
        float swingUnload = 0f;
        if (splitLegs)
        {
            float unloadBase = swingHipUnloadBias * liftSoft;
            // peel/unload used to sit on liftAct (delay=6) — at the end of Stance,
            // when weightBlend is already full from ~2 s. Tie them to weightBlend.
            float earlyAct = Mathf.Max(liftAct, weightBlend);
            if (walkSoft && earlyAct > 0.001f && walkLiftUnloadBias > 0.001f)
                unloadBase += earlyAct * walkLiftUnloadBias;
            swingUnload = standLegLevel * unloadBase;
        }
        float leftSwingUnload = 0f;
        if (leftIsSwing && !leftSwingHip)
        {
            leftSwingUnload = swingUnload;
            if (standCmd > 0.5f)
                leftSwingUnload *= backSwingHipUnloadScale;
        }
        float rightSwingUnload = 0f;
        if (rightIsSwing && !rightSwingHip)
            rightSwingUnload = swingUnload * forwardSwingHipUnloadScale;

        ControlHipsToWorldUpright(
            pelvisTarget,
            !leftSwingHip,
            !rightSwingHip,
            leftSwingUnload,
            rightSwingUnload);

        // After lift-off the target is not weaker than the latch angle: else
        // the thigh extends and the 26 cm foot stomps back.
        float speedSwingBoost = Mathf.Clamp(
            walkSpeedError * Mathf.Max(0f, walkSpeedSwingHipGain),
            0f,
            Mathf.Max(0f, walkSpeedSwingFlexMax));
        float baseSwingHipBoost = walkSoft ? Mathf.Max(0f, walkSwingHipBoost) : 0f;
        float placementHipBoost = 0f;
        float touchdownWindow = 0f;
        if (splitLegs && bodyState != null)
        {
            float clearance = leftIsSwing
                ? bodyState.leftFootGroundClearance
                : bodyState.rightFootGroundClearance;
            float touchH = Mathf.Max(0.005f, walkTouchdownSyncClearance);
            touchdownWindow = Mathf.Clamp01(1f - Mathf.Max(0f, clearance) / touchH);
        }
        if (walkSoft && splitLegs && bodyState != null && walkMoveDir != 0f)
        {
            float placementScale = 0f;
            float placementPhase = Mathf.Clamp01(walkPlacementSyncStart);
            if (walkStancePhase && walkStanceProgress >= placementPhase)
                placementScale = (walkStanceProgress - placementPhase) / Mathf.Max(0.01f, 1f - placementPhase);
            bool swingAirNow = leftIsSwing ? !leftGrounded : !rightGrounded;
            if (swingAirNow)
                placementScale = Mathf.Clamp01(placementScale) * touchdownWindow;
            else
                placementScale = Mathf.Clamp01(placementScale) * Mathf.Clamp01(walkStepPlacementGroundFraction);
            if (placementScale <= 0.0001f)
                placementScale = 0f;

            float desiredStep = Mathf.Max(0.05f, walkStepLength)
                + walkSpeedError * Mathf.Max(0f, walkStepLengthSpeedGain);
            float stanceX = leftIsSwing ? bodyState.rightFootPosition.x : bodyState.leftFootPosition.x;
            float swingX = leftIsSwing ? bodyState.leftFootPosition.x : bodyState.rightFootPosition.x;
            float currentStep = (swingX - stanceX) * walkMoveDir;
            float stepErr = desiredStep - currentStep;
            placementHipBoost = Mathf.Clamp(
                stepErr * Mathf.Max(0f, walkStepPlacementGain),
                -Mathf.Max(0f, walkStepPlacementMax),
                Mathf.Max(0f, walkStepPlacementMax)) * placementScale;
        }
        float swingHipFlexEffective = swingHipFlex + baseSwingHipBoost + speedSwingBoost;
        float latchedHipTarget = swingHipLatched
            ? Mathf.Min(swingHipAngleAtLatch, -swingHipFlexEffective)
            : -swingHipFlexEffective;
        latchedHipTarget -= placementHipBoost;
        // On the ground the reach is dosed: the reaction goes into the pelvis, not the step.
        float groundedHipTarget = -swingHipFlexEffective * Mathf.Clamp01(walkSwingScissorFlex);
        groundedHipTarget -= placementHipBoost * Mathf.Clamp01(walkStepPlacementGroundFraction);
        if (leftSwingHip)
        {
            float target = !leftAir && scissor ? groundedHipTarget : latchedHipTarget;
            ControlJointToAngleStable(leftHipJoint, target,
                hipPGain, hipDGain, errorReferenceDegrees,
                leftHipFlexor, leftHipExtensor, ref leftHipEffectiveInertia);
        }
        if (rightSwingHip)
        {
            float target = !rightAir && scissor ? groundedHipTarget : latchedHipTarget;
            ControlJointToAngleStable(rightHipJoint, target,
                hipPGain, hipDGain, errorReferenceDegrees,
                rightHipFlexor, rightHipExtensor, ref rightHipEffectiveInertia);
        }

        // Bend the swing knee only after lift-off: while the foot is down the
        // chain is closed and swingKneeFlex folds the pelvis with the support.
        // After latch flex like the hip — else the leg never leaves support.
        float leftKnee = kneeTarget;
        float rightKnee = kneeTarget;
        float swingKneeBoost = Mathf.Clamp(
            walkSpeedError * Mathf.Max(0f, walkSpeedSwingKneeGain),
            0f,
            Mathf.Max(0f, walkSpeedSwingFlexMax));
        float baseSwingKneeBoost = walkSoft ? Mathf.Max(0f, walkSwingKneeBoost) : 0f;
        float swingKneeFlexEffective = swingKneeFlex + baseSwingKneeBoost + swingKneeBoost;
        if (splitLegs)
        {
            if (leftIsSwing)
            {
                float groundKnee = swingKneeGroundedFraction * liftSoft;
                if (standCmd > 0.5f)
                    groundKnee *= backSwingKneeGroundScale;
                float earlyActL = Mathf.Max(liftAct, weightBlend);
                if (walkSoft && !swingHipLatched && earlyActL > 0.001f && walkKneePeelMax > 0.001f)
                    groundKnee = Mathf.Max(groundKnee, earlyActL * walkKneePeelMax);
                // Full flex only in the air. On latched&&grounded — kneeTarget
                // (no groundKnee): rearm of the fraction after a scrape gave swing 0.62 / drop 0.05.
                if (swingHipLatched && !leftGrounded)
                    leftKnee = Mathf.Clamp(kneeBaseAngle + standLegLevel * swingKneeFlexEffective, 0f, 115f);
                else if (!swingHipLatched)
                    leftKnee = Mathf.Clamp(
                        kneeTarget + standLegLevel * swingKneeFlexEffective * groundKnee,
                        0f, 115f);
            }
            if (rightIsSwing)
            {
                float groundKnee = swingKneeGroundedFraction * forwardSwingKneeGroundScale * liftSoft;
                float earlyActR = Mathf.Max(liftAct, weightBlend);
                if (walkSoft && !swingHipLatched && earlyActR > 0.001f && walkKneePeelMax > 0.001f)
                    groundKnee = Mathf.Max(groundKnee, earlyActR * walkKneePeelMax);
                if (swingHipLatched && !rightGrounded)
                    rightKnee = Mathf.Clamp(kneeBaseAngle + standLegLevel * swingKneeFlexEffective, 0f, 115f);
                else if (!swingHipLatched)
                    rightKnee = Mathf.Clamp(
                        kneeTarget + standLegLevel * swingKneeFlexEffective * groundKnee,
                        0f, 115f);
            }
            // Stance: straighten the knee toward 0 with weightBlend — lift the pelvis.
            if (walkStanceExtend > 0.001f && weightBlend > 0.001f)
            {
                float ext = Mathf.Clamp01(walkStanceExtend * weightBlend);
                if (!leftIsSwing)
                    leftKnee = Mathf.Lerp(leftKnee, 0f, ext);
                if (!rightIsSwing)
                    rightKnee = Mathf.Lerp(rightKnee, 0f, ext);
            }
            float stanceKneeBend = Mathf.Max(0f, walkStanceKneeBend) * Mathf.Lerp(0.5f, 1f, weightBlend);
            if (stanceKneeBend > 0.001f)
            {
                if (!leftIsSwing)
                    leftKnee = Mathf.Clamp(leftKnee + stanceKneeBend, 0f, 115f);
                if (!rightIsSwing)
                    rightKnee = Mathf.Clamp(rightKnee + stanceKneeBend, 0f, 115f);
            }
        }
        ControlJointToAngleStable(leftKneeJoint, leftKnee, kneePGain, kneeDGain,
            errorReferenceDegrees, leftKneeFlexor, leftKneeExtensor, ref leftKneeEffectiveInertia);
        ControlJointToAngleStable(rightKneeJoint, rightKnee, kneePGain, kneeDGain,
            errorReferenceDegrees, rightKneeFlexor, rightKneeExtensor, ref rightKneeEffectiveInertia);

        // Grounded swing: on walk Stance — stance only (like cold oneg);
        // mid on Transfer dual; on liftBlend fade the swing ankle.
        float leftAnkleBalance;
        float rightAnkleBalance;
        if (walkDualSupport && walkStancePhase)
        {
            leftAnkleBalance = splitLegs && leftIsSwing && leftGrounded ? 0f : ankleBalance;
            rightAnkleBalance = splitLegs && rightIsSwing && rightGrounded ? 0f : ankleBalance;
        }
        else if (walkDualSupport && liftBlend < 0.001f)
        {
            leftAnkleBalance = ankleBalance;
            rightAnkleBalance = ankleBalance;
        }
        else if (walkDualSupport)
        {
            leftAnkleBalance = splitLegs && leftIsSwing && leftGrounded
                ? ankleBalance * (1f - liftAct) : ankleBalance;
            rightAnkleBalance = splitLegs && rightIsSwing && rightGrounded
                ? ankleBalance * (1f - liftAct) : ankleBalance;
        }
        else
        {
            leftAnkleBalance = splitLegs && leftIsSwing && leftGrounded ? 0f : ankleBalance;
            rightAnkleBalance = splitLegs && rightIsSwing && rightGrounded ? 0f : ankleBalance;
        }

        // Stance push-off: at full weightBlend and CoM over the foot add
        // plantar on stance only. Not against the current ankleBalance.
        float runPushScale = running ? Mathf.Max(0f, runStancePushScale) : 1f;
        float speedPush = 0f;
        float speedDriveSignal = 0f;
        if (walkSoft && !running && walkMoveDir != 0f && bodyState != null)
        {
            speedPush = Mathf.Clamp(walkSpeedError * Mathf.Max(0f, walkSpeedPushGain), 0f, Mathf.Max(0f, walkSpeedPushMax));
            if (splitLegs && standLegLevel >= Mathf.Max(0f, walkSpeedDriveStartLevel))
            {
                float drive = Mathf.Clamp(walkSpeedError * Mathf.Max(0f, walkSpeedDriveGain), 0f, Mathf.Max(0f, walkSpeedDriveMax));
                speedDriveSignal = drive * walkMoveDir;
            }
        }
        float totalPush = walkStancePush * runPushScale + speedPush;
        if (walkSoft && splitLegs && stepPhaseDriver != null && stepPhaseDriver.PhaseCode == 0)
        {
            float phaseProgress = walkStanceProgress;
            float lateStart = Mathf.Clamp01(walkLatePushStart);
            bool swingAirNow = leftIsSwing ? !leftGrounded : !rightGrounded;
            if (phaseProgress > lateStart && (!walkLatePushNeedsAir || swingAirNow))
            {
                float lateT = (phaseProgress - lateStart) / Mathf.Max(0.01f, 1f - lateStart);
                float latePush = Mathf.Clamp(
                    lateT * Mathf.Max(0f, walkLatePushGain),
                    0f,
                    Mathf.Max(0f, walkLatePushMax));
                if (walkLatePushNeedsTouchdownWindow)
                    latePush *= touchdownWindow;
                totalPush += latePush;
            }
        }
        float speedComGate = Mathf.Max(0.01f, walkLiftComMax);
        if (speedPush > 0.001f)
            speedComGate *= Mathf.Max(0.1f, walkSpeedPushComGateScale);
        if (walkSoft && splitLegs && totalPush > 0.001f && weightBlend > 0.6f
            && Mathf.Abs(stanceComOff) <= speedComGate)
        {
            float push = totalPush * weightBlend;
            if (leftIsSwing && rightAnkleBalance >= -0.001f)
                rightAnkleBalance = Mathf.Clamp(rightAnkleBalance + push, -1f, 1f);
            else if (rightIsSwing && leftAnkleBalance >= -0.001f)
                leftAnkleBalance = Mathf.Clamp(leftAnkleBalance + push, -1f, 1f);
        }
        if (walkSoft && !running && splitLegs && Mathf.Abs(speedDriveSignal) > 0.001f)
        {
            if (leftIsSwing && rightGrounded)
                rightAnkleBalance = Mathf.Clamp(rightAnkleBalance + speedDriveSignal, -1f, 1f);
            else if (rightIsSwing && leftGrounded)
                leftAnkleBalance = Mathf.Clamp(leftAnkleBalance + speedDriveSignal, -1f, 1f);
        }

        DriveSwingOrStanceAnkle(leftIsSwing, false, leftGrounded, leftAnkleJoint,
            leftAnkleBalance, leftAnkleFlexor, leftAnkleExtensor, ref leftAnkleEffectiveInertia);
        DriveSwingOrStanceAnkle(rightIsSwing, true, rightGrounded, rightAnkleJoint,
            rightAnkleBalance, rightAnkleFlexor, rightAnkleExtensor, ref rightAnkleEffectiveInertia);

        ControlLumbarToWorldUpright(
            lumbarTargetTilt - crouchLevel * crouchTorsoLean - leanLevel * leanTorsoAngle - walkAutoTorsoLean - walkBaseTorsoLean);

        ControlNeckAndHeadToWorldUpright();
        ControlArmsForBalance(balanceSignal);
    }

    private void UpdateState(float comOffset)
    {
        float absOffset = Mathf.Abs(comOffset);
        if (absOffset > fallCoMOffset) currentState = BalanceState.Falling;
        else if (absOffset > recoveryCoMOffset) currentState = BalanceState.Recovery;
        else currentState = BalanceState.Balancing;
    }

    // Crouch target is a step; the regulator sets the descent rate.
    // No MotionIntent — we stand, so the stand and extra humans do not break.
    private void UpdateCrouchLevel()
    {
        float desired = 0f;
        if (intent != null)
            desired = Mathf.Clamp01(intent.crouch);
        float rate = desired < crouchLevel ? crouchReleaseRatePerSecond : crouchRatePerSecond;
        crouchLevel = Mathf.MoveTowards(
            crouchLevel, desired, Mathf.Max(0f, rate) * Time.fixedDeltaTime);
    }

    // Lean is a −1/0/+1 step from the keyboard; the regulator sets the slew.
    private void UpdateLeanLevel()
    {
        float desired = 0f;
        if (intent != null)
            desired = Mathf.Clamp(intent.lean, -1f, 1f);
        leanLevel = Mathf.MoveTowards(
            leanLevel, desired, Mathf.Max(0f, leanRatePerSecond) * Time.fixedDeltaTime);
    }

    // Intent is a −1/0/+1 step; the regulator sets the lift rate.
    private void UpdateStandLegLevel()
    {
        float desired = 0f;
        if (intent != null && Mathf.Abs(intent.standLeg) > 0.5f)
            desired = 1f;
        float rate = standLegRatePerSecond;
        if (desired < 0.5f && standLegLevel > desired)
            rate = standLegReleaseRatePerSecond;
        else if (desired > 0.5f && lastLiftBlend > 0.001f
                 && stepPhaseDriver != null && stepPhaseDriver.walkActive
                 && walkLiftLevelRateScale > 1.01f)
            rate *= 1f + lastLiftBlend * (walkLiftLevelRateScale - 1f);
        standLegLevel = Mathf.MoveTowards(
            standLegLevel, desired, Mathf.Max(0f, rate) * Time.fixedDeltaTime);
    }

    // Dimensionless PD signal in [-1, 1] — goes straight into muscle activation.
    private float ComputeSignal(HingeJoint2D joint, float targetAngle, float pGain, float dGain)
    {
        float error = Mathf.DeltaAngle(joint.jointAngle, targetAngle) / errorReferenceDegrees;
        float speed = joint.jointSpeed / speedReferenceDegPerSec;
        return Mathf.Clamp(pGain * error - dGain * speed, -1f, 1f);
    }

    // One convention for every joint: a positive signal means
    // "increase the joint angle", and that is extensor work. Verified on the knee:
    // counterclockwise torque (flexor) decreases jointAngle.
    private void ApplySignal(float signal, Muscle flexor, Muscle extensor)
    {
        if (signal > 0f)
        {
            UpdateMuscle(extensor, signal);
            UpdateMuscle(flexor, 0f);
        }
        else
        {
            UpdateMuscle(extensor, 0f);
            UpdateMuscle(flexor, -signal);
        }
    }

    // Kept for compatibility; every call has moved to Stable PD.
    private void ControlJointToAngle(HingeJoint2D joint, float targetAngle, float pGain, float dGain, Muscle flexor, Muscle extensor)
    {
        if (joint == null || flexor == null || extensor == null) return;
        ApplySignal(ComputeSignal(joint, targetAngle, pGain, dGain), flexor, extensor);
    }

    // Pelvis to world vertical through the hips. Error and speed are pelvis
    // tilt and ω, not jointAngle/jointSpeed: do not mix world and joint.
    // The sign does not copy lumbar. The lumbar joint sits on the chest, the
    // hip on the thigh, so the pelvis gets torque with a minus. A positive
    // signal (extensor) turns the pelvis counterclockwise and increases jointAngle.
    // While the swing is grounded it stays here: unload shifts its signal to
    // flexor, support gets clean PD. After latch the swing goes to a joint pose.
    private void ControlHipsToWorldUpright(float targetTilt, bool driveLeft, bool driveRight,
                                          float leftUnload, float rightUnload)
    {
        if (vestibularSystem == null) return;

        float tilt = vestibularSystem.GetPelvisTilt();
        float angVel = vestibularSystem.GetPelvisAngularVelocity();
        float error = Mathf.DeltaAngle(tilt, targetTilt) / Mathf.Max(1f, pelvisErrorReferenceDegrees);
        float speed = angVel / Mathf.Max(1f, speedReferenceDegPerSec);
        float signal = Mathf.Clamp(pelvisPGain * error - pelvisDGain * speed, -1f, 1f);

        if (driveLeft)
            ApplySignal(WithHipUnload(signal, leftUnload), leftHipFlexor, leftHipExtensor);
        if (driveRight)
            ApplySignal(WithHipUnload(signal, rightUnload), rightHipFlexor, rightHipExtensor);
    }

    // unload=0 — the same signal, no extra Clamp: the dual-support path is
    // bitwise the former one. Minus unload pulls toward flexor (decreases jointAngle).
    private static float WithHipUnload(float signal, float unload)
    {
        if (unload == 0f) return signal;
        return Mathf.Clamp(signal - unload, -1f, 1f);
    }

    // Swing in the air: foot perpendicular to the shin (ankle 0). Grounded toe-off
    // and stance-only CoM are set above, before this method is called.
    private void DriveSwingOrStanceAnkle(bool isSwing, bool isForwardSwing, bool grounded,
                                         HingeJoint2D joint, float ankleBalance,
                                         Muscle flexor, Muscle extensor, ref float ankleInertia)
    {
        if (isSwing && grounded && !swingHipLatched && isForwardSwing
            && stepPhaseDriver != null && stepPhaseDriver.walkActive
            && lastLiftBlend > 0.35f && walkSwingToeOffMax > 0.1f)
        {
            float toe = lastLiftBlend * lastLiftBlend * walkSwingToeOffMax;
            ControlJointToAngleStable(joint, toe, kneePGain, kneeDGain,
                errorReferenceDegrees, flexor, extensor, ref ankleInertia);
            return;
        }
        if (isSwing && grounded && !swingHipLatched && isForwardSwing
            && swingAnkleGroundedToeOff > 0.1f)
        {
            ControlJointToAngleStable(joint, swingAnkleGroundedToeOff, kneePGain, kneeDGain,
                errorReferenceDegrees, flexor, extensor, ref ankleInertia);
            return;
        }
        if (isSwing && swingHipLatched && !grounded)
        {
            // jointAngle = shin.rot − foot.rot. Foot horizontal (foot≈0) →
            // target = shin angle; else 0 pulls the toe down when the knee flexes.
            float airAnkle = 0f;
            if (walkSwingAirLevel > 0.5f && joint != null && joint.connectedBody != null)
                airAnkle = Mathf.DeltaAngle(0f, joint.connectedBody.rotation);
            ControlJointToAngleStable(joint, airAnkle, hipPGain, hipDGain,
                errorReferenceDegrees, flexor, extensor, ref ankleInertia);
            return;
        }
        ControlAnkleForBalance(joint, ankleBalance, flexor, extensor);
    }

    // Lumbar holds the chest to world vertical. Error and speed are
        // absolute torso tilt and angular velocity, not the joint angle:
        // when the pelvis topples the chest must brace, not ride down with it.
        // Inputs match the working version (error = target − tilt, world speed).
        // Gains are plus, so in P*e − D*ω they enter with a minus:
        // that is exactly the old P=−2 and D=−0.3, not a new damper.
    private void ControlLumbarToWorldUpright(float targetTilt)
    {
        if (lumbarFlexor == null || lumbarExtensor == null) return;
        if (vestibularSystem == null) return;

        float tilt = vestibularSystem.GetBodyTilt();
        float angVel = vestibularSystem.GetBodyAngularVelocity();
        float error = Mathf.DeltaAngle(tilt, targetTilt) / Mathf.Max(1f, lumbarErrorReferenceDegrees);
        float speed = angVel / Mathf.Max(1f, speedReferenceDegPerSec);
        float signal = Mathf.Clamp((-lumbarPGain) * error - (-lumbarDGain) * speed, -1f, 1f);
        ApplySignal(signal, lumbarFlexor, lumbarExtensor);
    }

    // Neck and head to world vertical. They used to hold the angle to the parent
    // (ControlJointToAngle to zero), i.e. they copied every torso topple —
    // the same error already fixed on lumbar and pelvis.
    //
    // What held them was brute force, not the regulator: 15 N·m on paired
    // inertia 0.0015 kg·m² is about 9900 rad/s², and PD reversed the joint
    // every physics step. The head spanned 9.8° at 25 Hz. But that chatter
    // was load-bearing: it welded 5.7 kg of head to the chest, and the
    // forward push sat on that weld. Just lowering torque fails — verified,
    // the threshold drops from 21 N·s below 18 (labels `nm*`). Target first, then torque.
    //
    // Both joints sit on their own segment, like lumbar on the chest, so the
    // sign copies lumbar, not pelvis: plus P and D enter as −P and −D.
    private void ControlNeckAndHeadToWorldUpright()
    {
        if (vestibularSystem == null) return;

        ControlSegmentToWorldUpright(
            vestibularSystem.GetNeckTilt(), vestibularSystem.GetNeckAngularVelocity(),
            neckJoint, neckFlexor, neckExtensor, ref neckEffectiveInertia);
        ControlSegmentToWorldUpright(
            vestibularSystem.GetHeadTilt(), vestibularSystem.GetHeadAngularVelocity(),
            headJoint, headFlexor, headExtensor, ref headEffectiveInertia);
    }

    private void ControlSegmentToWorldUpright(float tilt, float angVel, HingeJoint2D joint,
                                              Muscle flexor, Muscle extensor, ref float cachedInertia)
    {
        if (flexor == null || extensor == null) return;

        // Stable PD (Tan, Liu, Turk, 2011): take the error from the next
        // step, not the current one, and divide the signal by (1 + K·Δt/I).
        // Ordinary explicit PD diverges when K·Δt/I exceeds 2 — the same
        // rule written in balance-actuators for JointFriction. At the neck
        // that ratio is 5.7 at inertia 0.0015 kg·m², hence chatter of
        // 3.3° per physics step. The denominator kills overshoot, and the
        // predicted angle adds phase lead, so the damper can stay strong:
        // it is what keeps the head off the stop on a push.
        float tiltForError = tilt;
        float damping = 0f;

        if (useStablePd)
        {
            float dt = Time.fixedDeltaTime;
            tiltForError = tilt + angVel * dt;

            if (cachedInertia <= 0f)
                cachedInertia = EffectiveInertia(joint);

            if (cachedInertia > 0f)
            {
                // K in N·m·s/rad: normalized dGain × muscle ceiling, converted
                // from degrees to radians. Else the ratio is not dimensionless.
                float k = neckDGain * flexor.maxTorque * Mathf.Rad2Deg
                          / Mathf.Max(1f, speedReferenceDegPerSec);
                damping = k * dt / cachedInertia;
            }
        }

        float error = Mathf.DeltaAngle(tiltForError, neckTargetTilt) / Mathf.Max(1f, neckErrorReferenceDegrees);
        float speed = angVel / Mathf.Max(1f, speedReferenceDegPerSec);
        float raw = (-neckPGain) * error - (-neckDGain) * speed;
        float signal = Mathf.Clamp(raw / (1f + damping), -1f, 1f);
        ApplySignal(signal, flexor, extensor);
    }

    // Base pose plus CoM counter-reach. On walk — opposite-phase swing
    // by Stance phase; CoM-balance is off on walk (fold after swap).
    private void ControlArmsForBalance(float balanceSignal)
    {
        float lSh, rSh, lEl, rEl, lWr, rWr;
        if (stepPhaseDriver != null && stepPhaseDriver.walkActive)
        {
            ComputeWalkArmTargets(out lSh, out rSh, out lEl, out rEl, out lWr, out rWr);
            float walkArmSignal = Mathf.Clamp(balanceSignal, -1f, 1f);
            lSh = Mathf.Clamp(lSh + walkArmSignal * walkArmBalanceShoulderGain, -85f, 85f);
            rSh = Mathf.Clamp(rSh + walkArmSignal * walkArmBalanceShoulderGain, -85f, 85f);
            lEl = Mathf.Clamp(lEl + walkArmSignal * walkArmBalanceElbowGain, -135f, -5f);
            rEl = Mathf.Clamp(rEl + walkArmSignal * walkArmBalanceElbowGain, -135f, -5f);
        }
        else if (crouchLevel > 0.001f && standLegLevel < 0.01f)
            ComputeCrouchArmTargets(out lSh, out rSh, out lEl, out rEl, out lWr, out rWr);
        else
        {
            float armSignal = standLegLevel > 0.01f ? 0f : balanceSignal;
            lSh = rSh = Mathf.Clamp(
                shoulderBaseAngle + armShoulderBalanceGain * armSignal, -85f, 85f);
            lEl = rEl = Mathf.Clamp(
                elbowBaseAngle + armElbowBalanceGain * armSignal, -135f, -5f);
            lWr = rWr = Mathf.Clamp(
                wristBaseAngle + armWristBalanceGain * armSignal, -55f, 55f);
        }

        ApplyArmPose(lSh, rSh, lEl, rEl, lWr, rWr);
    }

    // Arms down toward the ground: minus on the shoulder is forward-down; elbow almost straight.
    // Plus on the left shoulder (as on walk) took the arm back and up.
    private void ComputeCrouchArmTargets(out float leftShoulder, out float rightShoulder,
                                         out float leftElbow, out float rightElbow,
                                         out float leftWrist, out float rightWrist)
    {
        float t = Mathf.Clamp01(crouchLevel);
        float sh = crouchArmShoulder * t;
        float leftBaseShoulder = Mathf.Lerp(shoulderBaseAngle, shoulderBaseAngle - sh * 1.08f, t);
        float rightBaseShoulder = Mathf.Lerp(shoulderBaseAngle, shoulderBaseAngle - sh * 0.92f, t);
        float baseElbow = Mathf.Lerp(elbowBaseAngle, crouchArmElbow, t);
        float baseWrist = Mathf.Lerp(wristBaseAngle, crouchArmWrist, t);

        float supportStart = Mathf.Clamp01(crouchHandPoseStart);
        float supportBlend = Mathf.InverseLerp(supportStart, 1f, t);
        float spread = supportBlend * Mathf.Max(0f, crouchHandSupportSpread);
        float centerShoulder = Mathf.Lerp(
            0.5f * (leftBaseShoulder + rightBaseShoulder),
            -Mathf.Abs(crouchHandSupportShoulder),
            supportBlend);
        leftShoulder = centerShoulder - spread;
        rightShoulder = centerShoulder + spread;

        leftElbow = Mathf.Lerp(baseElbow, crouchHandSupportElbow, supportBlend);
        rightElbow = Mathf.Lerp(baseElbow, crouchHandSupportElbow, supportBlend);
        leftWrist = Mathf.Lerp(baseWrist, crouchHandSupportWrist, supportBlend);
        rightWrist = Mathf.Lerp(baseWrist, crouchHandSupportWrist, supportBlend);

        leftShoulder = Mathf.Clamp(leftShoulder, -85f, 85f);
        rightShoulder = Mathf.Clamp(rightShoulder, -85f, 85f);
        leftElbow = Mathf.Clamp(leftElbow, -135f, -5f);
        rightElbow = Mathf.Clamp(rightElbow, -135f, -5f);
        leftWrist = Mathf.Clamp(leftWrist, -55f, 55f);
        rightWrist = Mathf.Clamp(rightWrist, -55f, 55f);
    }

    // Opposite phase: left swing → right shoulder forward (− shoulder).
    private void ComputeWalkArmTargets(out float leftShoulder, out float rightShoulder,
                                       out float leftElbow, out float rightElbow,
                                       out float leftWrist, out float rightWrist)
    {
        leftShoulder = shoulderBaseAngle;
        rightShoulder = shoulderBaseAngle;
        leftElbow = elbowBaseAngle;
        rightElbow = elbowBaseAngle;
        leftWrist = wristBaseAngle;
        rightWrist = wristBaseAngle;

        if (stepPhaseDriver == null || walkArmShoulderSwing < 0.1f)
            return;

        float standSign;
        float swing;
        int phase = stepPhaseDriver.PhaseCode;
        float standCmd = intent != null ? intent.standLeg : 0f;

        if (phase == 0 && Mathf.Abs(standCmd) > 0.5f)
        {
            float age = stepPhaseDriver.PhaseAge();
            float dur = Mathf.Max(0.1f, stepPhaseDriver.stanceDuration);
            swing = Mathf.Sin(Mathf.Clamp01(age / dur) * Mathf.PI);
            standSign = standCmd;
        }
        else if (phase == 1 && walkArmTransferCarry > 0.001f
                 && Mathf.Abs(stepPhaseDriver.CurrentStanceSign) > 0.5f)
        {
            // Transfer: decaying swing tail, not a cut to the base pose.
            float age = stepPhaseDriver.PhaseAge();
            float decayDur = Mathf.Min(1.5f, stepPhaseDriver.transferMaxDuration);
            float decay = 1f - Mathf.Clamp01(age / Mathf.Max(0.05f, decayDur));
            swing = walkArmTransferCarry * decay;
            standSign = stepPhaseDriver.CurrentStanceSign;
        }
        else
            return;

        ApplyWalkArmSwing(standSign, swing,
            out leftShoulder, out rightShoulder, out leftElbow, out rightElbow,
            out leftWrist, out rightWrist);
    }

    private void ApplyWalkArmSwing(float standSign, float swing,
                                   out float leftShoulder, out float rightShoulder,
                                   out float leftElbow, out float rightElbow,
                                   out float leftWrist, out float rightWrist)
    {
        leftShoulder = shoulderBaseAngle;
        rightShoulder = shoulderBaseAngle;
        leftElbow = elbowBaseAngle;
        rightElbow = elbowBaseAngle;
        leftWrist = wristBaseAngle;
        rightWrist = wristBaseAngle;

        if (swing < 0.001f)
            return;

        float amp = walkArmShoulderSwing * swing;
        float contra = -Mathf.Sign(standSign);
        float rightDelta = contra * amp;
        float leftDelta = -contra * amp;

        leftShoulder = Mathf.Clamp(shoulderBaseAngle + leftDelta, -85f, 85f);
        rightShoulder = Mathf.Clamp(shoulderBaseAngle + rightDelta, -85f, 85f);

        if (walkArmElbowSwing > 0.01f)
        {
            float elAmp = walkArmElbowSwing * swing;
            if (rightDelta < -0.01f)
                rightElbow = Mathf.Clamp(elbowBaseAngle - elAmp, -135f, -5f);
            if (leftDelta < -0.01f)
                leftElbow = Mathf.Clamp(elbowBaseAngle - elAmp, -135f, -5f);
        }

        leftWrist = Mathf.Clamp(wristBaseAngle + leftDelta * 0.15f, -55f, 55f);
        rightWrist = Mathf.Clamp(wristBaseAngle + rightDelta * 0.15f, -55f, 55f);
    }

    private void ApplyArmPose(float leftShoulder, float rightShoulder,
                              float leftElbow, float rightElbow,
                              float leftWrist, float rightWrist)
    {
        ControlJointToAngleStable(
            leftShoulderJoint, leftShoulder, shoulderPGain, shoulderDGain,
            shoulderErrorReferenceDegrees, leftShoulderFlexor, leftShoulderExtensor,
            ref leftShoulderEffectiveInertia);
        ControlJointToAngleStable(
            rightShoulderJoint, rightShoulder, shoulderPGain, shoulderDGain,
            shoulderErrorReferenceDegrees, rightShoulderFlexor, rightShoulderExtensor,
            ref rightShoulderEffectiveInertia);

        ControlJointToAngleStable(
            leftElbowJoint, leftElbow, elbowPGain, elbowDGain,
            elbowErrorReferenceDegrees, leftElbowFlexor, leftElbowExtensor,
            ref leftElbowEffectiveInertia);
        ControlJointToAngleStable(
            rightElbowJoint, rightElbow, elbowPGain, elbowDGain,
            elbowErrorReferenceDegrees, rightElbowFlexor, rightElbowExtensor,
            ref rightElbowEffectiveInertia);

        ControlJointToAngleStable(
            leftWristJoint, leftWrist, wristPGain, wristDGain,
            wristErrorReferenceDegrees, leftWristFlexor, leftWristExtensor,
            ref leftWristEffectiveInertia);
        ControlJointToAngleStable(
            rightWristJoint, rightWrist, wristPGain, wristDGain,
            wristErrorReferenceDegrees, rightWristFlexor, rightWristExtensor,
            ref rightWristEffectiveInertia);
    }

    // Stable PD to a joint angle. Same formula as the neck, sign as
    // ControlJointToAngle: P*error − D*speed. Denominator 1+K·Δt/I always,
    // no useStablePd flag: else elbow and wrist enter a limit cycle.
    private void ControlJointToAngleStable(HingeJoint2D joint, float targetAngle,
                                           float pGain, float dGain, float errorRef,
                                           Muscle flexor, Muscle extensor, ref float cachedInertia)
    {
        if (joint == null || flexor == null || extensor == null) return;

        float dt = Time.fixedDeltaTime;
        float angleForError = joint.jointAngle + joint.jointSpeed * dt;
        float damping = 0f;

        if (cachedInertia <= 0f)
            cachedInertia = EffectiveInertia(joint);

        if (cachedInertia > 0f)
        {
            float k = dGain * flexor.maxTorque * Mathf.Rad2Deg
                      / Mathf.Max(1f, speedReferenceDegPerSec);
            damping = k * dt / cachedInertia;
        }

        float error = Mathf.DeltaAngle(angleForError, targetAngle) / Mathf.Max(1f, errorRef);
        float speed = joint.jointSpeed / speedReferenceDegPerSec;
        float raw = pGain * error - dGain * speed;
        float signal = Mathf.Clamp(raw / (1f + damping), -1f, 1f);
        ApplySignal(signal, flexor, extensor);
    }

    // Paired joint inertia: both bodies rotate toward each other, so the
    // denominator is 1/(1/I₁ + 1/I₂), not one segment's inertia. The head
    // is tied to a light neck, and paired inertia is a third of own — using
    // one body understates stiffness by a factor of three.
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

    // The ankle is not a positional joint but a pendulum drive: it gets
    // only torque against CoM offset. A spring and viscosity to a target
    // angle had to go: the foot is pressed to the ground, so muscle torque
    // turns the whole body, not the joint, and "damping" the angle
    // rocked the human down in 3 seconds. Viscosity comes from JointFriction.
    // A command that drives the joint into the last ankleLimitMargin degrees
    // of the stop is zeroed: else the foot jams at ±45° and there is no "back" torque.
    private void ControlAnkleForBalance(HingeJoint2D joint, float balanceCommand, Muscle flexor, Muscle extensor)
    {
        if (joint == null || flexor == null || extensor == null) return;
        float signal = ProtectJointLimit(joint, Mathf.Clamp(balanceCommand, -1f, 1f));
        ApplySignal(signal, flexor, extensor);
    }

    private float ProtectJointLimit(HingeJoint2D joint, float signal)
    {
        if (!joint.useLimits) return signal;

        float angle = joint.jointAngle;
        float min = joint.limits.min;
        float max = joint.limits.max;
        // A positive signal increases the angle, a negative one decreases it.
        if (signal > 0f && angle >= max - ankleLimitMargin) return 0f;
        if (signal < 0f && angle <= min + ankleLimitMargin) return 0f;
        return signal;
    }

    private void UpdateMuscle(Muscle muscle, float targetActivation)
    {
        if (muscle == null) return;
        // Instant zeroing was bang-bang: extensor→0 in one step, flexor
        // comes on at full torque — a limit cycle on light joints.
        muscle.activation = Mathf.MoveTowards(
            muscle.activation, targetActivation,
            muscleActivationSpeed * Time.fixedDeltaTime);
    }

    private void RelaxAllMuscles()
    {
        UpdateMuscle(leftHipFlexor, 0f); UpdateMuscle(leftHipExtensor, 0f);
        UpdateMuscle(rightHipFlexor, 0f); UpdateMuscle(rightHipExtensor, 0f);
        UpdateMuscle(leftKneeFlexor, 0f); UpdateMuscle(leftKneeExtensor, 0f);
        UpdateMuscle(rightKneeFlexor, 0f); UpdateMuscle(rightKneeExtensor, 0f);
        UpdateMuscle(leftAnkleFlexor, 0f); UpdateMuscle(leftAnkleExtensor, 0f);
        UpdateMuscle(rightAnkleFlexor, 0f); UpdateMuscle(rightAnkleExtensor, 0f);
        UpdateMuscle(lumbarFlexor, 0f); UpdateMuscle(lumbarExtensor, 0f);
        UpdateMuscle(neckFlexor, 0f); UpdateMuscle(neckExtensor, 0f);
        UpdateMuscle(headFlexor, 0f); UpdateMuscle(headExtensor, 0f);
        UpdateMuscle(leftShoulderFlexor, 0f); UpdateMuscle(leftShoulderExtensor, 0f);
        UpdateMuscle(rightShoulderFlexor, 0f); UpdateMuscle(rightShoulderExtensor, 0f);
        UpdateMuscle(leftElbowFlexor, 0f); UpdateMuscle(leftElbowExtensor, 0f);
        UpdateMuscle(rightElbowFlexor, 0f); UpdateMuscle(rightElbowExtensor, 0f);
        UpdateMuscle(leftWristFlexor, 0f); UpdateMuscle(leftWristExtensor, 0f);
        UpdateMuscle(rightWristFlexor, 0f); UpdateMuscle(rightWristExtensor, 0f);
    }
}