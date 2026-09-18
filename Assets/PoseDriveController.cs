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
// The balance layer is two loops and no gates:
//   1. pelvis height above the stand frame -> the stance knee extends to keep it,
//   2. the capture point -> where the swing foot lands, and when the legs swap.
//
// Poses: standing (quiet, walk key released) and a four key walk cycle
// (contact, mid stance, push off, swing) keyed to a step clock. The swing leg
// folds under the body instead of trailing: the ankle target passes under the
// pelvis at the knee fold angle, so clearance and the fold come from the same
// geometry the drawing asks for.
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
    private float swingLandShare = 0.8f;
    private float heightGainDegPerMeter = 400f;
    // 25 is the value the 40 s stand was verified on (p_ds 09:44, 15 mm drop).
    // The sign is the one that held: a rising pelvis extends the leg. Do not
    // flip it without a trial - the 09:58 build with this term at 0 and the
    // knee clamp one sided ran 2 m forward and face planted.
    private float heightDampDegPerMeterPerSecond = 25f;
    private float placementMarginMeters = 0.04f;
    private float comLeadSwapMeters = 0.16f;
    private float stepMinMeters = 0.02f;
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

    // ─── recorder-facing counters ──────────────────────────────────────────
    public int poseStepCount;
    public float poseHeightError;

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

    // ─── one physics step ──────────────────────────────────────────────────
    public void Tick(float comOffset)
    {
        if (!bound) return;

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
        }
        DriveTrunk(pelvisTilt, pelvisAngVel, tilt, walkKey && walkEnabled);
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
        // The stand hip carries no angle target: the pelvis upright law below
        // owns both hips, and on planted legs that is also the hip pose.
        DriveOneLeg(true, float.NaN, knee, false, 0f, 0f, 0f);
        DriveOneLeg(false, float.NaN, knee, false, 0f, 0f, 0f);
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
            stepClock -= Mathf.Min(stepClock, halfCycle);
            swingSign = -swingSign;
            poseStepCount++;
            swingFootX0 = swingSign > 0 ? leftFootX : rightFootX;
            stanceFootX = swingSign > 0 ? rightFootX : leftFootX;
            swingFootX = swingSign > 0 ? leftFootX : rightFootX;
        }

        float s = Mathf.Clamp01(stepClock / Mathf.Max(0.05f, swingDuration));
        phaseForLog = s;

        // Capture point: where the CoM will be in one pendulum time constant.
        // That is where the next stance foot belongs; the margin ahead of it is
        // the stability reserve the plan asks for.
        float pendulum = Mathf.Sqrt(Mathf.Max(0.05f, (pelvisY - groundY) / 9.81f));
        float captureX = comX + comVelX * pendulum;
        float placeX = Mathf.Clamp(captureX + placementMarginMeters,
            stanceFootX + stepMinMeters, stanceFootX + stepMaxMeters);

        float standAnkle = standAnkleY;
        // Swing leg: ankle travels from where it left the ground, through the
        // pass under the pelvis, to the placement. The lift at mid swing is
        // what makes the knee fold - a high tucked foot is a folded knee. The
        // foot is down at sLand of the swing and holds the ground after that,
        // so the support is handed over instead of a flight phase.
        float air = Mathf.Clamp01(s / swingLandShare);
        float eased = air * air * (3f - 2f * air);
        float ankleX = Mathf.Lerp(swingFootX0, placeX, eased);
        float ankleY = standAnkle + swingLiftMeters * Mathf.Sin(Mathf.PI * air);
        float swingKnee;
        float swingHip;
        SolveLegIk(ankleX, ankleY, pelvisY, pelvisTilt, out swingHip, out swingKnee);

        float stanceKnee = KneeForHeight(pelvisY, pelvisVelY);
        float stanceHip = float.NaN;   // the pelvis upright law owns the stance hip

        if (swingSign > 0)
        {
            DriveOneLeg(true, swingHip, swingKnee, true, ankleX, ankleY, pelvisY);
            DriveOneLeg(false, stanceHip, stanceKnee, false, 0f, 0f, 0f);
        }
        else
        {
            DriveOneLeg(false, swingHip, swingKnee, true, ankleX, ankleY, pelvisY);
            DriveOneLeg(true, stanceHip, stanceKnee, false, 0f, 0f, 0f);
        }

        // Stance hip stays on the pelvis upright law: the planted thigh follows
        // the body passing over it, which is what carries the weight forward.
        // No hip pose hold here: a step IS hip travel past the band.
        DriveHips(pelvisTilt, pelvisAngVel, 0f, swingSign < 0, swingSign > 0,
            0f, false);
        DriveAnkleBalance(comX, comVelX, leftDown, rightDown);
        DriveToes(swingSign > 0 ? -10f : 0f, swingSign > 0 ? 0f : -10f);

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
        float alpha = Mathf.Acos(Mathf.Clamp(length / (a + b), 0f, 1f));
        float phi = Mathf.Atan2(dx, Mathf.Max(0.01f, -dy));
        float thighWorld = phi + alpha;
        kneeDeg = 2f * alpha * Mathf.Rad2Deg;
        // jointAngle(hip) = pelvis rotation - thigh world rotation: a thigh
        // rotated forward lowers the joint angle below the pelvis upright zero.
        hipDeg = pelvisTilt - thighWorld * Mathf.Rad2Deg;
    }

    private void DriveOneLeg(bool left, float hipDeg, float kneeDeg, bool swing,
                             float ankleX, float ankleY, float pelvisY)
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

        // Airborne ankle keeps a flat foot: the fold, not the ankle, is what
        // lifts the sole. A swing ankle driven to zero would point the toe down
        // on the fold and scrape the ground at lift off.
        if (swing)
        {
            HingeJoint2D ankle = left ? leftAnkleJoint : rightAnkleJoint;
            Muscle ankleFlexor = left ? balance.leftAnkleFlexor : balance.rightAnkleFlexor;
            Muscle ankleExtensor = left ? balance.leftAnkleExtensor : balance.rightAnkleExtensor;
            ref float ankleInertia = ref (left ? ref leftAnkleInertia : ref rightAnkleInertia);
            DriveJoint(ankle, 0f, balance.kneePGain, balance.kneeDGain,
                balance.errorReferenceDegrees, ankleFlexor, ankleExtensor, ref ankleInertia);
        }
        // ankleX / ankleY / pelvisY stay in the signature: the swing placement
        // is solved in TickWalk, and a later cycle needs them for the heel
        // strike angle. They are intentionally unused here.
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
    private void DriveTrunk(float pelvisTilt, float pelvisAngVel, float tilt, bool walking)
    {
        if (vestibular == null) return;
        bool posture = walking || balance.standLegLevel < 0.5f;
        if (posture)
        {
            float lumbarError = Mathf.DeltaAngle(tilt, balance.lumbarTargetTilt)
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
