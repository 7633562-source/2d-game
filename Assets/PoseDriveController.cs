using System.Globalization;
using UnityEngine;

// Level 2 pose drive (-poseDrive 1): replaces only the decision layer; off = the old path runs.
// Balance reduces to two metre requests (foot landing X, hip height held from the step's start);
// PoseLibrary.Sample turns them into every leg joint angle (deg), driven via ActuatorMixer's PD.
public partial class PoseDriveController : MonoBehaviour
{
    // ─── switches ───
    // Parsed from the command line here so the frozen -walk switch surface stays untouched.
    public bool active;
    private bool walkEnabled;
    private float swingDuration = 0.35f;
    private float stanceDuration = 0.45f;
    private float swingLiftMeters = 0.22f;
    // Drawn stance-leg knee (deg) at the keys holding the pelvis through mid-late stance (SwingFold,
    // FootPlace, PlantLoad); genome g[13..15] or the matching switches move the ask itself.
    private float stanceFoldKneeDeg = 10f;
    private float stancePlaceKneeDeg = 12f;
    private float stanceLoadKneeDeg = 10f;
    // ─── IK instrumentation ───
    // Recorder only, never read by control: set on every SolveLegIk with whether it clamped, and when.
    public bool IkSaturated;
    public int IkSolveTick = -1;
    private float swingLandShare = 0.8f;
    private float heightGainDegPerMeter = 400f;
    // Pelvis-height damping, deg per m/s. Sign: a rising pelvis extends the leg. Do not flip or zero
    // it without a trial: without it the body ran forward and face-planted.
    private float heightDampDegPerMeterPerSecond = 25f;
    // Step vertical bob (m): the step's height request gains a phase-shaped lift, 0 at the swap that
    // started the step, the full amplitude at mid-stance, 0 again at the next swap. 0 = off.
    private float bobAmpMeters = 0f;
    private float placementMarginMeters = 0.04f;
    private float comLeadSwapMeters = 0.16f;
    // Descent-aware bias (see PoseLibrary.Sample). Off (default): the bias uses the drawn knee;
    // on: the bias aims the ankle with the knee the descent actually leaves.
    private bool placeKneeLive = false;
    // Foot-placement band around the stance foot (m): one step either side, so the law can put the
    // pivot behind a CoM falling backwards. Read only by TickWalk's placeX clamp.
    private float stepMinMeters = -0.45f;
    private float stepMaxMeters = 0.45f;
    // Step-length symmetry trim (req-201; all 0 = off, the walk path is then bit-identical). The
    // commanded placement pivot is nudged +trim/2 on a left-lead step, -trim/2 on a right-lead one;
    // the trim is an event integrator over the measured step-length difference (see TickWalk). Metres.
    private float poseWalkSymTrimGain;          // -poseWalkSymTrim: per-step gain (0 = off)
    private float poseWalkSymTrimMax = 0.06f;   // -poseWalkSymTrimMax: clamp on the trim
    private float poseWalkSymTrim0;             // -poseWalkSymTrim0: task value at each walk start
    private float symTrim;                      // the live trim, m
    private float symLeadLM;                    // last completed LEFT-lead step length, m
    private float symLeadRM;                    // last completed RIGHT-lead step length, m
    private bool symLeadLSeen;                  // a left-lead length exists
    private bool symLeadRSeen;                  // a right-lead length exists
    // req-202 SYM: the sane band of a measured step lead, m; outside it the read is a
    // misread (a flutter touch or a bump) and the previous value is kept.
    private const float symLeadMinM = 0.15f;
    private const float symLeadMaxM = 0.80f;
    // req-203 SYM: the read-source switch (0 = the plant frame, the req-201 source and the
    // default; 1 = the load-taking swap, the req-202 source). Inert at either value while the
    // trim gain is 0.
    private float poseWalkSymRead;
    // Recorder mirrors for the _assist.csv log; the main csv is untouched.
    public float SymTrimM { get { return symTrim; } }
    public float SymLeadLM { get { return symLeadLM; } }
    public float SymLeadRM { get { return symLeadRM; } }

    // req-226 DYNHIP: the swing hip's pelvis-side anchor x, per stride. Mode 0 (absent) = no read, no
    // write. Mode 1 JOINT: while the walk gate is up the swing side's connectedAnchor.x converges
    // toward the pelvis centre line by hipDynAmp, the stance side returns to built at hipDynOut
    // (hold at 0); gate down = both back at hipDynIn. Mode 2 AIM: no joint write, the placement aims
    // from the swing hip's own anchor (Walk.cs poseIn.pelvisX).
    private int hipDynMode;                  // -hipDynMode: 0 off | 1 joint | 2 aim
    private float hipDynAmp = 0.0135f;       // -hipDynAmp: converge amplitude, m (<= built +-0.01476)
    private float hipDynIn = 0.10f;          // -hipDynIn: rate toward the converged value, m/s
    private float hipDynOut = 0.03f;         // -hipDynOut: stance return rate, m/s (0 = hold converged)
    private float hipDynAim = 1f;            // -hipDynAim: mode 2 share k of the swing anchor x
    private Vector2 hipDynBuiltL;            // built anchors, captured once in Initialize
    private Vector2 hipDynBuiltR;
    private float hipDynLiveL;               // last written values (a write only on a change)
    private float hipDynLiveR;
    private bool hipDynGate;                 // armed by the walk branch, cleared every tick
    private float hipDynAimLive;             // mode 2: the aim term actually fed to pelvisX (m)
    private float hipDynWorkJ;               // ledger: |reactionForce.x * dx| per write (J)
    private int hipDynWrites;                // number of write ticks
    private float hipDynMaxDHipDeg;          // max |d jointAngle| across a write tick (gate 0 read)
    private float hipDynPendAL;              // req-229: settled gate-0 read - the hip angle at
    private float hipDynPendAR;              // the write; read back one tick later (solver-run)
    private bool hipDynPendL;                // a settled read is due on the next tick
    private bool hipDynPendR;

    // Recorder mirrors for the _assist.csv log (req-226); the joints are read, never written here.
    public int HipDynMode { get { return hipDynMode; } }
    public float HipDynAmp { get { return hipDynAmp; } }
    public float HipDynIn { get { return hipDynIn; } }
    public float HipDynOut { get { return hipDynOut; } }
    public float HipDynAim { get { return hipDynAim; } }
    public float HipDynWorkJ { get { return hipDynWorkJ; } }
    public int HipDynWrites { get { return hipDynWrites; } }
    public float HipDynMaxDHipDeg { get { return hipDynMaxDHipDeg; } }
    public float HipAnchorXL { get { return leftHipJoint != null ? leftHipJoint.connectedAnchor.x : 0f; } }
    public float HipAnchorXR { get { return rightHipJoint != null ? rightHipJoint.connectedAnchor.x : 0f; } }
    public float HipDynAimM { get { return hipDynAimLive; } }

    // Swing foot's own ankle offset (deg); single writer: t.swingLeg.ankle in PoseLibrary.Sample.
    // Opened by the tail gate, ramped in over swingAnkleRampSeconds, clamped by ClampLeg. 0 = off.
    private float swingAnkleDeg = 0f;
    private float swingAnkleRampSeconds = 0.045f;
    private float swingAnkleRampT;   // the gate's own open clock, per step (s)

    // Push-off delivery: the drawn toe roll is asked but not delivered under load, so in the loaded
    // roll flick (~0.11 s, support leg down) the stance ankle's P is multiplied toward this. 1 = off.
    private float pushDeliverP = 1f;
    // Tail-gate need bound: 1 is the lead the swap rule calls a swap (load bound: SupportHoldLoadN).
    private const float SwingPlaceNeedMin = 0.9f;
    private float standCaptureSeconds = 4.6f;
    private float standTiltLimitDeg = 8f;
    private float fallenDropMeters = 0.40f;
    private float fallenTiltDeg = 50f;
    private float armSwingDeg = 22f;

    // Stand flat-sole toe hold: the light MTP hinge starves the toe gain under the stable-PD divisor,
    // so ground lift curls the toes and the foot rocks on its MTP ridge. Armed only while both soles
    // rest flat, it multiplies the stand's toe gain (walk DriveToes keeps the base). scale <= 1 = off.
    private float standToeHoldScale = 8f;         // toe-gain multiplier at arm (1 = off)
    private float standToeHoldBiasDeg = 2f;       // +deg = toe down (the rig's own sign)
    private float standToeFlatArmDeg = 5f;        // arm at both plates within this of level
    private float standToeFlatDropDeg = 8f;       // disarm above this (hysteresis)
    private float standToeFlatHoldSeconds = 0.2f; // dwell before either switch
    private bool standToeFlatArmed;
    private float standToeFlatAge;
    // Squat gate: the toe hold's posture shifts the squat descent, so it is blocked for a whole take
    // that squats (MotionIntent.toeHoldBlock) and while squat reps are armed; disarms at once. 0 = off.
    private float standToeSquatGate = 1f;

    // ─── bound body ───
    private bool bound;
    private Human human;
    private BalanceController balance;
    private VestibularSystem vestibular;
    private BodyStateEstimator state;
    private CenterOfMassCalculator comCalculator;
    private MotionIntent intent;
    // The get-up's two collaborators (see Initialize).
    private ArmDrive armDrive;
    private StepPhaseDriver stepDriver;
    private Rigidbody2D pelvisBody;

    private readonly ActuatorMixer mixer = new ActuatorMixer();

    private HingeJoint2D leftHipJoint, rightHipJoint;
    private HingeJoint2D leftKneeJoint, rightKneeJoint;
    // Forward lean level (slow slew); exactly 0 on every frame that never leans.
    private float leanShiftLevel;
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

    // ─── walk neck hold ───
    // The stable-PD inertia ignores the carried head, so the divisor is ~6x too large and the neck
    // rides its stops; walkNeckHoldShare blends in the chain load (0 = segment, 1 = chain). Walk only.
    private float walkNeckHoldShare = 0.28f;
    private float neckLoadInertia, headLoadInertia;

    // ─── quiet ankle loop ───
    // The standing law: command = P*(offset/ref) + D*(speed/ref), offsets in metres (a 7 mm CoM offset
    // already asks for the whole ankle). These two CLI factors scale P and D.
    private float ankleBalanceP = 1.0f;
    private float ankleBalanceD = 1.0f;
    // Foot-capacity bound on the ankle loop while crouched: each foot's signal is capped at what its
    // measured load can hold about the MTP (plantar) or the heel edge (dorsal), times eta, so the
    // surplus cannot lift the heel. -clAnkleCap <eta>; 0 (default) = off.
    private float clAnkleCapEta;
    // The same bound while standing upright (-clAnkleCapStand <eta>; 0 = off): at rest both ankles
    // railed plantar (2 x 65 N*m) ask for a pressure point beyond the toes, the heels rise and the
    // body hops - seen after a step lands with a few cm/s left in the CoM.
    private float clAnkleCapStandEta;
    private float clCapFzLpL, clCapFzLpR;
    // Quiet stand (-clStandLp <ms>; 0 = off). On the stand the raw angular speeds flip sign every
    // physics frame, and the D terms of the pelvis law, the knee PD and the lumbar law turn that into
    // a 100 Hz torque buzz (hip +10 / -5 N*m frame to frame). The soles creep under it, the stand
    // thumps every ~1.5 s and now and then a thump grows into a hop. With the switch those D terms
    // read the speed through a first-order low-pass of <ms>. -clStandLpMask: 1 pelvis law, 2 knees,
    // 4 lumbar (default 7). -clStandLpScope 1 (default): the upright stand only; 2: the crouch too.
    // -clStandD <x> (default 1): multiplier on the D of the pelvis and lumbar laws in the same scope.
    private float clStandLpS;
    private int clStandLpMask = 7;
    private int clStandLpScope = 1;
    private float clStandDMul = 1f;
    private float clLpPelvisW, clLpKneeWL, clLpKneeWR, clLpBodyW;
    private float clLpTickTime = -1f;        // fixedTime of the last stand tick that ran the filters
    private bool clLpNow;                    // this tick's drivers read the filtered speeds
    private float clDNow = 1f;               // this tick's D multiplier
    // -clStandKneeMin <deg> (0 = off): floor of the upright stand's knee target, so a ringing leg
    // cannot reach the knee's 0 deg stop (the hop's bounce).
    private float clStandKneeMinDeg;
    // The settle's arrest step is running (the assist reads it under -assistSettleWalk).
    public bool ClSettleStepping => walkSettleLatch
        && balance != null && balance.locomotionState == BalanceController.LocomotionState.Standing;
    // Walk entry from the stand: the step state (clock, swing side, height and CoM datums) is whatever
    // the previous walk left. -clWalkEntry 1: an entry on one foot starts a fresh step with the free
    // foot as the swing; 2: every entry starts fresh, the rear foot swinging first. 0 (default) = off.
    private int clWalkEntryMode;
    // -clEntryRear <m> (0.10): stagger above which the rear foot swings first on a fresh entry (below
    // it the swing side is whatever the last walk left). -clEntryLoad 1: below it the foot that
    // carries less load swings first.
    private float clEntryRearM = 0.10f;
    private bool clEntryLoadOn;
    // Stance close (-clStanceClose 1; 0 = off): a quiet stand on staggered feet (what the stop's last
    // step leaves) first takes the CoM onto the front foot - the ankle loop's reference and the
    // assist's home move to that sole's middle - then the rear foot steps up beside it, so the squat
    // and the next walk start from level feet. The squat waits while it runs (ClStanceCloseBusy).
    // Phases: 0 idle, 1 weight shift, 2 step. The body is taken to face +x, as the walk does.
    private bool clStanceCloseOn;
    private float clCloseMinM = 0.15f;       // -clStanceCloseMin: ankle-to-ankle stagger that arms it, m
    private float clCloseFrontUpT;           // seconds the front sole has been off the ground in the shift
    private float clCloseStepS = 0.45f;      // -clStanceCloseT: the step's duration, s
    // -clStanceCloseSpeed <m/s> (0 = off): the step takes at least distance / speed seconds. A long
    // step done in the fixed 0.45 s swings the leg so fast that the pelvis pitches 8 deg under it and
    // the foot lands 0.09 m past its target; a short one (0.28 m) lands within 0.01 m.
    private float clCloseSpeedMps;
    private float clCloseStepNowS = 0.45f;   // this step's duration, s
    // -clStanceCloseLoop 1 (0 = off): the landing is closed on the stepping ankle's measured place.
    // On the open path the hip returns later than the knee extends, so a 0.2 m step sets the foot
    // down 0.07 m past its target with the thigh still forward. With the switch the lift is held to
    // the path's end; the foot then comes down at -clStanceCloseLand m/s, slowed in proportion to
    // the ankle's x error (slowest at -clStanceCloseTol m), the asked x is pushed against that error
    // by -clStanceCloseKx, the target follows the front ankle, and the leg may straighten fully (the
    // solve's 0.995 reach leaves the sole ~3 mm over the ground under the hip).
    private bool clCloseLoopOn;
    private float clCloseLandMps = 0.25f;
    private float clCloseTolM = 0.03f;
    private float clCloseKx = 0.5f;
    private float clCloseLiftNow;            // the asked height over the landing line, m
    private float clCloseMeasX;              // the stepping ankle, world x
    private float clCloseErrX;               // measured - target, m (plus = past the target)
    private float clCloseSeatM;              // -clStanceCloseSeat: the step ends only with the asked lift at or under this, m (0 = off)
    private bool clClosePullBackOn;          // -clClosePullBack: a shift that times out short of the front foot steps the FRONT foot back instead of aborting
    private bool clClosePullRun;             // the pull-to-Com step is running (its target is frozen)
    private float clClosePullX1;             // the pull-to-Com step's frozen x target, m
    private float clClosePullComM;           // -clClosePullCom: the pull-back lands this far behind the CoM, m (0 = off)
    private float clClosePullLiftK = 1f;     // -clClosePullLift: arc fraction of the pull step, 1 = flight, 0 = ground drag
    private float clCloseDoneLoad;           // -clCloseDoneLoad: the step ends only once the stepping sole carries this load, N (0 = off)
    private float clCloseLoadHoldT;          // seconds the load gate has held the done back in this step, 0 = not holding
    private float clCloseDoneKnee;           // -clCloseDoneKnee: the step ends only with the stepping knee within this of the stand's law, deg (0 = off)
    private float clCloseKneeHoldT;          // seconds the knee gate has held the done back in this step, 0 = not holding
    private float clCloseSwingKneeDeg;       // the stepping knee's flexion (IK solve), deg - the knee gate's read
    private float clCloseStandKneeDeg;       // the stand's knee law, deg - the knee gate's reference
    private float clClosePullHomeM;          // -clClosePullHome: through the pull the loop's reference stays this ahead of the pull target, m (0 = off)
    private float clCloseShiftHoldS;         // -clCloseShiftHold: a front-sole lift mid-shift pins the reference for up to this long, s (0 = off)
    private float clCloseHoldUsed;           // the pin's budget spent on the current lift, s
    private float clCloseStallS = 2.5f;      // -clCloseStall: a shift stalled this long gives up to the pull paths (or aborts)
    private float clCloseHoldM;              // -clStanceCloseHold: hold the landing descent while |x err| > m (0 = off)
    private float clCloseRetryM;             // -clStanceCloseRetry: re-hop the step while the landing error is over m (0 = off)
    private float clCloseRetryT;             // -clStanceCloseRetryT: minimum duration of a retry sub-step, s (0 = 0.3)
    private int clCloseRetryExtra;           // -clStanceCloseRetries: retry cap per step (0 = 2)
    private int clCloseRetryN;               // retries used in this step
    private float clCloseRetryGain;          // -clStanceCloseRetryGain: the re-hop aims at this fraction of the miss (0 = off, aims at the target)
    private float clCloseRetryX1;            // frozen x target of the running gain re-hop, m
    private float clCloseHoldHop;            // -clStanceCloseHoldHop: a hold stalled above this err gets one quiet re-hop (0 = off)
    private float clCloseNudgeM;             // -clStanceCloseNudge: a finished landing off by more than this (aim-relative) gets one micro re-step (0 = off)
    private int clCloseNudgeN;               // micro re-steps used in this step
    private const float ClCloseLiftM = 0.05f;
    private int clClosePhase;
    private float clCloseT;                  // seconds in the current phase
    private float clCloseQuietT;             // the arming quiet span's clock, s
    private float clCloseTickTime = -1f;     // fixedTime of the last stand tick that ran the law
    private float clCloseHomeX;              // the front sole's middle, world x
    private bool clCloseSwingLeft;
    private int clCloseTries;                // attempts since the stand began
    // -clStanceCloseRearm <s> (0 = off): a close that lands level gives its attempts back, so a
    // stand of any length keeps closing the stagger an unloaded foot creeps into; <s> = the least
    // pause before the next close.
    private float clCloseRearmS;
    private float clCloseAimM;               // -clStanceCloseAim: step target offset from the front ankle, m
    private float clCloseDropMps;            // -clStanceCloseDrop: descent of the step's target past its end, m/s
    // -clStopLand <phase> (0 = off): under the settle latch the swing descent, CoM-paced on the walk,
    // is also paced by the step clock - fully asked at this swing phase - so the arrest step lands on
    // time whatever the slowing body does (the one piece of v16 that helped: 10/13 against 9/13).
    private float clStopLandPhase;
    // Entry ready gate (-clEntryReady <s>; 0 = off). The walk launches reliably only from a level,
    // quiet stand: a walk key pressed while the stop is still closing the feet falls 1-2 s later.
    // With the switch such a key press is honoured once the stand is ready - both soles down, no
    // close phase running, ankle stagger < 0.05 m, |comVelX| <= 0.10 m/s, for 0.15 s - or after <s>
    // seconds. A key that returns while the settle latch still walks is not held.
    private float clEntryReadyS;
    private float clEntryReadyStaggerM = 0.05f;   // -clEntryReadyStagger: the level bar, m
    private bool clEntryReadyLatchOn = true;      // -clEntryReadyLatch
    private float clEntryReadyQuietS = 0.15f;     // -clEntryReadyQuiet: the ready span, s
    private float clEntryReadyVel = 0.10f;        // -clEntryReadyVel: the quiet bar, m/s
    private float clEntryReadyT;             // seconds this key press has been held back
    private float clEntryReadyQT;            // the ready span's clock
    private bool clEntryReadyDone;           // this key press has been honoured
    // -clEntryMask 1 (0 = off): while the gate holds a key back, MotionIntent.holdWalk hides it from
    // every other reader of moveX too (capture walk, assist, recorder) and the input sources keep
    // the step driver off it. Armed between key presses as well, so the layers that tick before
    // this one never see the first frame of a held key.
    private bool clEntryMaskOn;
    // -clEntryStraight <deg> (0 = off): the entry gate's ready clock runs only while the legs
    // read straight - |hip| and knee (both sides, joint angles) at or under this bar. A squat
    // cut mid-descent and its rise pass the old terms (level, quiet, feet down) with the body
    // still folded, so a key pressed on a rising body was honoured at once; switch off = old terms.
    private float clEntryStraightDeg;

    private bool ClEntryHold(bool walkKey, bool leftDown, bool rightDown, float comVelX)
    {
        if (clEntryReadyS <= 0f) return false;
        float dt = Time.fixedDeltaTime;
        // The ready clock runs all the time, key or no key: a stand that has already been level and
        // quiet for the span answers the key at once.
        bool walkingNow = walkSettleWasWalk || walkSettleLatch;
        float stagger = 0f;
        if (leftAnkleJoint != null && rightAnkleJoint != null)
            stagger = Mathf.Abs(leftAnkleJoint.transform.TransformPoint(leftAnkleJoint.anchor).x
                - rightAnkleJoint.transform.TransformPoint(rightAnkleJoint.anchor).x);
        // -clEntryStraight <deg>: the legs must read straight (see the field block) for the clock.
        bool legsStraight = true;
        if (clEntryStraightDeg > 0f)
        {
            float hipL = leftHipJoint != null ? Mathf.Abs(leftHipJoint.jointAngle) : 0f;
            float hipR = rightHipJoint != null ? Mathf.Abs(rightHipJoint.jointAngle) : 0f;
            float kneeL = leftKneeJoint != null ? Mathf.Abs(leftKneeJoint.jointAngle) : 0f;
            float kneeR = rightKneeJoint != null ? Mathf.Abs(rightKneeJoint.jointAngle) : 0f;
            legsStraight = Mathf.Max(Mathf.Max(hipL, hipR), Mathf.Max(kneeL, kneeR)) <= clEntryStraightDeg;
        }
        bool ready = !walkingNow && leftDown && rightDown && !(clClosePhase > 0 && ClCloseFresh)
            && stagger < clEntryReadyStaggerM && Mathf.Abs(comVelX) <= clEntryReadyVel && legsStraight;
        clEntryReadyQT = ready ? clEntryReadyQT + dt : 0f;
        if (!walkKey)
        {
            clEntryReadyT = 0f;
            clEntryReadyDone = false;
            return false;
        }
        if (clEntryReadyDone) return false;
        // -clEntryReadyLatch 0: a key that returns while the settle latch still walks is not held
        // (the walk goes on); 1 (default): the stop, once begun, is finished first.
        if (!clEntryReadyLatchOn && walkingNow)
        {
            clEntryReadyDone = true;
            return false;
        }
        clEntryReadyT += dt;
        if (clEntryReadyQT < clEntryReadyQuietS && clEntryReadyT < clEntryReadyS) return true;
        clEntryReadyDone = true;
        Debug.Log(string.Format(CultureInfo.InvariantCulture,
            "[walkEntry] ready t={0:F3} waited={1:F2} stagger={2:F3} v={3:F3} {4}",
            Time.fixedTime, clEntryReadyT, stagger, comVelX,
            clEntryReadyQT >= clEntryReadyQuietS ? "ready" : "timeout"));
        return false;
    }
    private float clCloseDoneTime = -1000f;  // fixedTime of the last close that landed level
    private float clCloseAnkleX0, clCloseAnkleY0, clCloseAnkleX1, clCloseAnkleY1;
    private bool ClCloseFresh => Time.fixedTime - clCloseTickTime <= 2.5f * Time.fixedDeltaTime;
    // The stand right after a coast (see -clReleaseRampClose): its feet close at the coast's bar, so a
    // stagger between the gate's ready bar (0.10) and the plain close bar (clCloseMinM) is still levelled.
    private float ClCloseMinNow => clReleaseRampPost && clReleaseRampClose > 0f
        ? Mathf.Min(clCloseMinM, clReleaseRampClose) : clCloseMinM;
    public bool ClStanceCloseBusy => clClosePhase > 0 && ClCloseFresh;
    // NaN = no home: the assist keeps its own (the middle of the soles).
    public float ClStanceHomeX => clClosePhase > 0 && ClCloseFresh ? clCloseHomeX : float.NaN;

    // ─── stand frame ───
    private bool standCaptured;
    private float standPelvisY;
    private float standAnkleY;
    private float groundY;
    private float pelvisCenterToSole;

    // ─── recorder-facing counters ───
    public int poseStepCount;
    public float poseHeightError;

    // Torso (lumbar) world tilt target of the pose in force, written each step by TickStand/TickWalk
    // and read by the trunk driver: the walk writes the library's target, the stand lumbarTargetTilt.
    private float torsoTargetDeg;
    // Human.cs sets the ankle hinge limits as literals at the heel's ConnectTo call (no field); the
    // pose library clamps every ankle target to this symmetric pair (deg).
    private const float AnkleLimitDeg = 45f;

    public void Initialize(Human owner, BalanceController controller)
    {
        human = owner;
        balance = controller;
        if (human == null || balance == null) return;

        vestibular = human.vestibularSystem;
        state = human.bodyStateEstimator;
        comCalculator = human.comCalculator;
        intent = GetComponent<MotionIntent>();
        // ArmDrive is the single writer of the arm muscles (runs after this), so the rise's press is a
        // request (ArmDrive.RequestArmPress); StepPhaseDriver.StandingComY is the standing CoM threshold.
        armDrive = GetComponent<ArmDrive>();
        stepDriver = GetComponent<StepPhaseDriver>();
        if (balance != null) balance.getUpStandTiltDeg = getUpStandTiltDeg;

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
        // Palms: index 0 = left, 1 = right. The hand collider is what the press loads and what the grip
        // material is swapped onto (Human.SetHandGrip).
        handCollider[0] = HandCollider(root, "LeftArmHand");
        handCollider[1] = HandCollider(root, "RightArmHand");

        // req-226: capture the built hip anchors once; the live mirrors record what is written.
        if (leftHipJoint != null) hipDynBuiltL = leftHipJoint.connectedAnchor;
        if (rightHipJoint != null) hipDynBuiltR = rightHipJoint.connectedAnchor;
        hipDynLiveL = hipDynBuiltL.x;
        hipDynLiveR = hipDynBuiltR.x;

        mixer.activationSpeed = balance.muscleActivationSpeed;
        mixer.speedReference = balance.speedReferenceDegPerSec;

        // The pelvis centre sits pelvisSize.y/2 above the hip anchor, the sole footSize.y/2 below the
        // ankle: the stand frame spans the whole chain.
        pelvisCenterToSole = human.pelvisSize.y * 0.5f
            + human.thighSize.y + human.shinSize.y + human.footSize.y * 0.5f;

        ReadSwitches();
        bound = true;
    }

    // ─── one physics step ───
    public void Tick(float comOffset)
    {
        if (!bound) return;
        // req-121 (a): R is the walk RUNNING's last right plant - cleared on a tick the walk did not run.
        if (clReleasePhaseOn && !walkSettleWasWalk) clReleasePhaseR = -1f;

        // Recorder only: reset the pose-ask block to its 0 sentinel before any branch can return, so
        // relaxed and stand steps report a zero ask and capturePoseDriveState says whether the gait drives.
        ClearPoseAskRecord();
        // req-226: one anchor step per tick, before any early return below; the walk branch arms the
        // gate on this tick and StepHipDyn reads it on the next, so a non-walk tick ramps to built.
        StepHipDyn();
        hipDynGate = false;
        hipDynAimLive = 0f;
        // Quiet stand: only a stand tick turns the filtered speeds on.
        clLpNow = false;
        clDNow = 1f;

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

        // ─── horizontal channel (recorder only) ───
        // Sampled every tick before any return. 1: CoM x-acceleration, a one-step difference of
        // comVelocity.x (doubly differenced, so read as a trend); 0 on the first tick.
        float stepDt = Time.fixedDeltaTime;
        if (stepDt <= 0f) stepDt = 0.005f;
        poseComAccelXMps2 = comAccelSeeded ? (comVelX - prevComVelX) / stepDt : 0f;
        prevComVelX = comVelX;
        comAccelSeeded = true;
        // 2: horizontal ground reaction on the feet, N, +x = walk forward (see SampleGroundReactionX).
        poseGroundFxN = SampleGroundReactionX();

        CaptureStandFrame(tilt, pelvisY, leftDown || rightDown,
            (leftFootY + rightFootY) * 0.5f);
        groundY = standPelvisY - pelvisCenterToSole;

        // The fall guard runs before the down test so it acts while the body is going over and the arms
        // take the ground first; the pelvis descent rate is one of its two terms.
        if (fallGuardEnabled) TickFallGuard(tilt, pelvisY, pelvisVelY);

        // Sit-stand dwell clock, counted while the stand path still drives: the get-up opens only after
        // the sit window has held for getUpSitStandHold s. Flag off: the clock stays 0.
        bool sitStandWindow = getUpStage == GetUpStage.Idle && !getUpDriveOn
            && SitStandWindow(tilt, leftDown, rightDown, pelvisY, pelvisVelY);
        getUpSitWinNow = sitStandWindow;
        float sitDt = Time.fixedDeltaTime > 0f ? Time.fixedDeltaTime : 0.005f;
        if (getUpSitStandOn && sitStandWindow)
            getUpSitStandT += sitDt;
        else
            getUpSitStandT = 0f;
        bool sitStandFire = getUpSitStandOn && sitStandWindow
            && getUpSitStandT >= getUpSitStandHold;

        if (IsDown(tilt, pelvisY) || getUpDriveOn || GetUpActive() || sitStandFire)
        {
            // ─── down, get-up off ───
            // -poseGetUp 0 (default): the body reports Falling/Fallen and relaxes; while the walk key is held
            // the afterfall crawl rocks the legs so ArmDrive's presses find purchase.
            if (!getUpEnabled)
            {
                // The recorder reads BalanceState: a body on the slab reports Falling whatever its CoM offset.
                balance.currentState = BalanceController.BalanceState.Falling;
                balance.locomotionState = BalanceController.LocomotionState.Fallen;
                if (intent != null) intent.holdWalk = false;
                RelaxAll();
                // ─── afterfall crawl ───
                // A body settled on the slab freezes and the arm presses find no purchase, so while down with the
                // walk key held the legs ramp slowly into the get-up tuck and rock around it. Arms untouched.
                if (WalkKeyHeld()) DriveAfterfallCrawl();
                return;
            }
            // ─── get-up on ───
            // -poseGetUp 1: TickGetUp owns the joints and the ladder state until the card's three bars are met
            // or its bounds are spent; it keeps a half-risen body instead of handing it to the walk early.
            if (intent != null) intent.holdWalk = false;
            TickGetUp(tilt, pelvisTilt, pelvisAngVel, pelvisY, pelvisVelY, comX, comVelX,
                leftDown, rightDown, leftFootX, rightFootX, leftFootY, rightFootY);
            return;
        }
        afterfallCrawlOn = false;
        if (getUpEnabled) EndGetUp();

        // The stop's coast-down (-clReleaseRamp; inert at 0): the key-up edge is caught before any
        // reader of moveX below, so the gait, the settle and BalanceAssist later in the step all see
        // the same ramped command.
        UpdateReleaseRamp();
        bool walkKey = WalkKeyHeld();
        // Entry ready gate (see clEntryReadyS): a key pressed on a stand that is not ready counts as
        // not pressed until the stand is ready - the stand, its close and the assist keep their
        // standing rules meanwhile.
        if (ClEntryHold(walkKey, leftDown, rightDown, comVelX)) walkKey = false;
        // The mask (see clEntryMaskOn): set until this key press is honoured, clear from then on.
        if (intent != null) intent.holdWalk = clEntryMaskOn && clEntryReadyS > 0f && !clEntryReadyDone;
        // The settle step resolves before the path choice; when off it only reads two fields.
        UpdateWalkSettle(walkKey, tilt, pelvisY, comVelX, leftDown, rightDown);
        balance.locomotionState = walkKey
            ? BalanceController.LocomotionState.Walking
            : BalanceController.LocomotionState.Standing;
        balance.currentState = Mathf.Abs(tilt) > 25f
            ? BalanceController.BalanceState.Recovery
            : BalanceController.BalanceState.Balancing;

        float pelvisHeight = pelvisY - groundY;   // kept for the walk log
        poseHeightError = standPelvisY - pelvisY;
        // The recorder reads CaptureWalkStepCount for walkSwap; the pose gait publishes its count there.
        balance.poseDriveStepCount = poseStepCount;

        // While the settle owns the walk, the gait keeps the joints for one arrest step.
        bool walkRan = (walkKey || walkSettleLatch) && walkEnabled && standCaptured
            && (leftDown || rightDown);
        // req-226: the swing-locked anchor gate. It drops at every stop edge (key-up, release pend,
        // coast, post-coast stand, settle latch), so the release and arrest plants land on the built
        // anchors; the walk branch is the only writer and 0 keeps it off in every other state.
        if (hipDynMode > 0)
            hipDynGate = walkRan && walkKey && !walkSettleLatch && !clReleasePhasePend
                && !ClReleaseCoastLive() && !clReleaseRampPost
                && balance != null && balance.currentState == BalanceController.BalanceState.Balancing;
        if (walkRan)
        {
            if (clWalkEntryMode > 0 && !walkSettleWasWalk)
                EnterWalkFresh(leftDown, rightDown, leftFootX, rightFootX);
            TickWalk(tilt, pelvisTilt, pelvisAngVel, pelvisY, pelvisVelY,
                comX, comVelX, leftDown, rightDown, leftFootX, rightFootX,
                leftFootY, rightFootY);
        }
        else
        {
            TickStand(tilt, pelvisTilt, pelvisAngVel, pelvisY, pelvisVelY,
                comX, comVelX, leftDown, rightDown, leftFootY, rightFootY);
            // Recorder only: the pose gait drives the joints with the stand pose, no walk ask this step.
            poseAskDriveState = PoseDriveStateStand;
        }
        if (walkRan && !walkSettleWasWalk)
        {
            clTap2WalkT = Time.fixedTime;  // req-144 r5: the walk's start
            // req-201: the symmetry trim restarts from its calibration at every walk start; the
            // lead memory is cleared so the first update of a walk uses this walk's own steps.
            symTrim = Mathf.Clamp(poseWalkSymTrim0, -poseWalkSymTrimMax, poseWalkSymTrimMax);
            symLeadLSeen = false;
            symLeadRSeen = false;
        }
        walkSettleWasWalk = walkRan;
        // The coast's stand flag (see -clReleaseRampClose): any walk clears it - it marks only the stand
        // the coast itself left.
        if (walkRan) clReleaseRampPost = false;
        DriveTrunk(pelvisTilt, pelvisAngVel, tilt, walkKey && walkEnabled, torsoTargetDeg);
    }

    // req-226: step the thigh joints' pelvis-side anchors: while the walk gate is up the swing side
    // (swingSign) converges toward the pelvis centre line by hipDynAmp, the stance side returns to
    // its built x at hipDynOut (hold when 0); gate down = both back at hipDynIn. One write per side
    // per tick, only where the value changes; the built Vector2 y is never touched.
    private void StepHipDyn()
    {
        if (hipDynMode != 1 || leftHipJoint == null || rightHipJoint == null) return;
        // req-229: the settled gate-0 read - the hip angle one tick after the write.
        if (hipDynPendL) { hipDynPendL = false; float dL = Mathf.Abs(leftHipJoint.jointAngle - hipDynPendAL); if (dL > hipDynMaxDHipDeg) hipDynMaxDHipDeg = dL; }
        if (hipDynPendR) { hipDynPendR = false; float dR = Mathf.Abs(rightHipJoint.jointAngle - hipDynPendAR); if (dR > hipDynMaxDHipDeg) hipDynMaxDHipDeg = dR; }
        float dt = Time.fixedDeltaTime;
        float goalL = hipDynBuiltL.x;
        float goalR = hipDynBuiltR.x;
        float rateL = hipDynIn;
        float rateR = hipDynIn;
        if (hipDynGate)
        {
            // swingSign > 0 = the left leg is the swing leg: it converges, the right holds at Out.
            if (swingSign > 0)
            {
                goalL = hipDynBuiltL.x + hipDynAmp;
                rateR = hipDynOut;
            }
            else
            {
                goalR = hipDynBuiltR.x - hipDynAmp;
                rateL = hipDynOut;
            }
        }
        hipDynLiveL = Mathf.MoveTowards(hipDynLiveL, goalL, rateL * dt);
        hipDynLiveR = Mathf.MoveTowards(hipDynLiveR, goalR, rateR * dt);
        if (hipDynLiveL != leftHipJoint.connectedAnchor.x) WriteHipAnchor(leftHipJoint, hipDynLiveL);
        if (hipDynLiveR != rightHipJoint.connectedAnchor.x) WriteHipAnchor(rightHipJoint, hipDynLiveR);
    }

    // req-226: one anchor write; the ledger reads the joint's own reaction force (the stance side
    // carries the weight, so the work number is honest) and logs the angle for the settled read.
    private void WriteHipAnchor(HingeJoint2D joint, float x)
    {
        float a0 = joint.jointAngle;
        float dx = x - joint.connectedAnchor.x;
        Vector2 a = joint.connectedAnchor;
        a.x = x;
        joint.connectedAnchor = a;
        hipDynWrites++;
        hipDynWorkJ += Mathf.Abs(joint.reactionForce.x * dx);
        // req-229: the settled read runs on the next step; a0 is the angle at the write.
        if (joint == leftHipJoint) { hipDynPendAL = a0; hipDynPendL = true; }
        else { hipDynPendAR = a0; hipDynPendR = true; }
    }

    // ─── stand frame ───
    // The quiet stance height, tracked while upright (not the spawn frame), so holding it holds the
    // walk start too. The sole stays at groundY for the whole trial, so one capture suffices.
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
        float drop = standPelvisY - pelvisY;
        if (drop > fallenDropMeters && Mathf.Abs(tilt) > fallenTiltDeg)
            return true;
        // A sitting fall keeps a mild tilt that the gate above misses; only the get-up path reads this
        // deep-drop term.
        return getUpEnabled && drop > GetUpSittingDropMeters;
    }

    // ─── sit window ───
    // After an attempt the body can park in a quiet upright sit above IsDown's sitting drop: torso
    // upright, both soles grounded and loaded, pelvis quiet and low. Pure: clocks live with callers.
    private bool SitStandWindow(float tilt, bool leftDown, bool rightDown,
                                float pelvisY, float pelvisVelY)
    {
        if (!getUpSitStandOn || !getUpEnabled || getUpDone) return false;
        if (!standCaptured || !WalkKeyHeld()) return false;
        float drop = standPelvisY - pelvisY;
        if (drop <= getUpSitStandDropMin) return false;
        if (drop > getUpSitStandDropMax) return false;
        if (tilt <= getUpStandTiltDeg) return false;
        if (!leftDown || !rightDown) return false;
        if (pelvisVelY < -getUpSitStandVel || pelvisVelY > getUpSitStandVel) return false;
        if (poseGroundFzLeftN + poseGroundFzRightN < getUpSitStandKeep * PoseRigWeightN())
            return false;
        return true;
    }

    private bool WalkKeyHeld()
    {
        // The real key: the entry gate decides on it and hides it from the others (clEntryMaskOn).
        return intent != null && Mathf.Abs(intent.moveXRaw) > 0.01f;
    }

    // Auto rise: a keyless down held for -autoGetUp seconds reads as keyed, so the body tries to rise
    // on its own. Latches for the current down; standing or a real key clears it. 0 (default) = off.
    private bool GetUpKeyOn(bool down)
    {
        if (WalkKeyHeld())
        {
            getUpAutoAge = 0f;
            getUpAutoKey = false;
            return true;
        }
        if (getUpAutoDwell <= 0f)
        {
            getUpAutoAge = 0f;
            getUpAutoKey = false;
            return false;
        }
        if (down)
        {
            getUpAutoAge += Time.fixedDeltaTime;
            if (getUpAutoAge >= getUpAutoDwell)
                getUpAutoKey = true;
        }
        else
        {
            getUpAutoAge = 0f;
            getUpAutoKey = false;
        }
        return getUpAutoKey;
    }

    // ─── quiet pose ───
    // Crouch knee stop (deg): the leg's stop, the same clamp the old path uses for its crouch knee.
    private const float CrouchKneeStopDeg = 115f;

    private void TickStand(float tilt, float pelvisTilt, float pelvisAngVel,
                           float pelvisY, float pelvisVelY, float comX, float comVelX,
                           bool leftDown, bool rightDown, float leftFootY, float rightFootY)
    {
        float knee = KneeForHeight(pelvisY, pelvisVelY);
        // Walk-to-stand handoff: while the window runs, the height reference blends from the walk-end
        // pelvis height back to the stand frame, so the body is taken where it is. Off/expired: as above.
        if (walkSettleHandOn && walkSettleHandT > 0f)
        {
            float u = Mathf.Clamp01(walkSettleHandT / Mathf.Max(0.01f, walkSettleBlendS));
            float hRef = Mathf.Lerp(standPelvisY, walkSettleHandPelvisY, u);
            float handKnee = balance.kneeBaseAngle
                - heightGainDegPerMeter * (hRef - pelvisY)
                - heightDampDegPerMeterPerSecond * pelvisVelY;
            knee = Mathf.Lerp(Mathf.Clamp(handKnee, 0f, 60f), knee, 1f - u);
            walkSettleHandT -= Time.fixedDeltaTime;
        }
        // Stand law: the pelvis-upright law owns both hips, the CoM loop owns the ankles (a torque), the
        // trunk target is the controller's. The pose library drives only the walk; Stance is its start.
        torsoTargetDeg = balance.lumbarTargetTilt;
        // ─── crouch layer ───
        // Owns the knee fold target (kneeBaseAngle + level*crouchKneeFlex, clamped at the stop) and offsets
        // pelvis and trunk tilt targets (forward = minus); base drivers keep running. Level is 0 when idle.
        float crouch = balance.crouchLoadLevel;
        float pelvisTargetTilt = 0f;
        if (crouch > 0.0001f)
        {
            float c = Mathf.Clamp01(crouch);
            knee = Mathf.Clamp(
                balance.kneeBaseAngle + c * balance.crouchKneeFlex, 0f, CrouchKneeStopDeg);
            pelvisTargetTilt = -c * balance.crouchPelvisTilt;
            torsoTargetDeg = balance.lumbarTargetTilt - c * balance.crouchTorsoLean;
        }
        // ─── forward lean layer ───
        // A weight shift: offsets pelvis/trunk tilt targets (forward = minus) and moves the CoM loop target
        // by leanShiftLevel*leanComShift. Forward tilt enters at intent pace, exits slow; back is slow.
        float comXAnkle = comX;
        float lean = balance.leanLevel;
        leanShiftLevel = Mathf.MoveTowards(leanShiftLevel,
            Mathf.Abs(lean) > 0.0001f ? Mathf.Clamp(lean, -1f, 1f) : 0f,
            Mathf.Max(0f, balance.leanShiftRatePerSecond) * Time.fixedDeltaTime);
        float leanTiltLevel;
        if (lean > 0f)
            leanTiltLevel = Mathf.Max(Mathf.Clamp01(lean), leanShiftLevel);
        else
            leanTiltLevel = leanShiftLevel;
        if (Mathf.Abs(leanTiltLevel) > 0.0001f)
        {
            pelvisTargetTilt -= leanTiltLevel * balance.leanPelvisTilt;
            torsoTargetDeg -= leanTiltLevel * balance.leanTorsoAngle;
        }
        if (Mathf.Abs(leanShiftLevel) > 0.0001f)
        {
            comXAnkle = comX - leanShiftLevel * balance.leanComShift;
        }
        // ─── kick support-side shift ───
        // While the strike's level is up the pelvis target leans toward the CoM by the level-1 stand
        // signal; the CoM loop target can move away from the CoM by kickShiftComMeters at |signal| = 1.
        float kickShift = balance.kickShiftLevel;
        bool kickPlantOn = false;
        if (kickShift > 0.0001f)
        {
            float kickSignal = KickShiftSignal(comX);
            pelvisTargetTilt -= kickShiftGainDeg * kickSignal * kickShift;
            if (Mathf.Abs(kickShiftTorsoDeg) > 0.0001f)
                torsoTargetDeg += kickShiftTorsoDeg * kickSignal * kickShift;
            if (Mathf.Abs(kickShiftComMeters) > 0.0001f)
                comXAnkle += kickShiftComMeters * kickSignal * kickShift;
            // Wind-up: while the level is up the loop's CoM value sits kickWindComMeters ahead of the measured
            // CoM, so the ankle's CoP command brakes from the stroke's first frame. No signal factor: the
            // signal is ~0 in a settled stance, exactly where the strain is.
            if (Mathf.Abs(kickWindComMeters) > 0.0001f)
                comXAnkle += kickWindComMeters * kickShift;
            kickPlantOn = kickPlantHold > 0.5f;
        }
        // ─── kick wind-up gain ───
        // Inter-stroke window (level up, no stroke): the ankle loop's CoM channels scale by kickWindGain
        // (bounded; a fixed offset is all-or-fatal) and its reference moves to the standing foot's plate.
        kickWindLevel = 0f;
        kickWindRefX = 0f;
        if (kickShift <= 0.0001f)
        {
            // Episode off: the per-stroke latch re-arms and the burst's held frame is released.
            kickWindWindowUp = false;
            kickWindFrameHeld = false;
            // Level reached 0: the reference returns to the hull centre; with -kickWindHold s the term stays
            // alive for the settle and the hand-over happens s seconds later.
            if (kickWindHoldClock > 0f)
            {
                kickWindHoldClock -= Time.fixedDeltaTime;
                if (kickWindHoldClock > 0f
                    && (kickWindGain > 0.0001f || kickWindNorm > 0.0001f)
                    && !balance.kickStrokeActive && state != null)
                {
                    bool holdIsLeft = balance.kickSideRight;
                    Vector2 holdFoot = holdIsLeft
                        ? state.leftFootPosition : state.rightFootPosition;
                    kickWindRefX = KickWindRefPoint(holdFoot);
                    kickWindLevel = kickWindNorm > 0.0001f
                        ? kickWindNormLevel : Mathf.Clamp01(kickWindGain);
                }
            }
        }
        else if (kickShift > 0.0001f)
        {
            // Request up: load the release clock so it runs from the moment the level reaches 0 (0 = no hold).
            kickWindHoldClock = kickWindHold;
        }
        if (kickShift > 0.0001f
            && (kickWindGain > 0.0001f || kickWindNorm > 0.0001f)
            && !balance.kickStrokeActive && state != null)
        {
            bool supportIsLeft = balance.kickSideRight;
            Vector2 standFoot = supportIsLeft
                ? state.leftFootPosition : state.rightFootPosition;
            float liveX = KickWindRefPoint(standFoot);
            // Optional margin moves the window's target away from the edge; the shifted point is what every
            // latch mode and the carry read below see.
            if (kickWindMargin > 0f) liveX -= kickWindMargin;
            // The window's rising edge: when the carry the term scales by is read.
            bool kickWindRising = !kickWindWindowUp;
            if (kickWindLatch > 1.5f)
            {
                // Mode 2: the burst's frame, latched once and kept across strokes (a stroke drops the window only).
                if (!kickWindFrameHeld)
                {
                    kickWindRefLatched = liveX;
                    kickWindFrameHeld = true;
                }
                kickWindRefX = kickWindRefLatched;
            }
            else if (kickWindLatch > 0.5f)
            {
                // Mode 1: re-latched at every rising edge.
                if (kickWindRising) kickWindRefLatched = liveX;
                kickWindRefX = kickWindRefLatched;
            }
            else
            {
                kickWindRefX = liveX;
            }
            kickWindWindowUp = true;
            if (kickWindNorm > 0.0001f)
            {
                // Normalizer: the dose is the term's P at the error this window carries, latched on its first
                // frame; the level is what that dose costs at the window's scale.
                if (kickWindRising)
                {
                    kickWindCarry = Mathf.Clamp(
                        Mathf.Abs(comXAnkle - kickWindRefX),
                        KickWindCarryFloor, KickWindCarryCeil);
                    float perMetre = ankleBalanceP * balance.ankleComP
                        / Mathf.Max(0.01f, balance.comOffsetReference);
                    kickWindNormLevel = Mathf.Clamp01(kickWindNorm / (perMetre * kickWindCarry));
                }
                kickWindLevel = kickWindNormLevel;
            }
            else
            {
                kickWindLevel = Mathf.Clamp01(kickWindGain * kickShift);
            }
            // While the pace clock has armed a stroke and the gate still defers it, the window carries
            // kickWindDue's level instead. -1 (unset) = off.
            if (kickWindDue >= 0f && balance.kickFireHeld) kickWindLevel = kickWindDue;
        }
        else if (kickShift > 0.0001f && kickWindHead >= 0f && balance.kickHeadActive
                 && state != null)
        {
            // Stroke head (the lift's unload window): reference on the standing foot's live plate centre at
            // kickWindHead's level. -1 (unset) = off: the stroke carries level 0.
            bool headSupportIsLeft = balance.kickSideRight;
            Vector2 headFoot = headSupportIsLeft
                ? state.leftFootPosition : state.rightFootPosition;
            kickWindRefX = KickWindRefPoint(headFoot);
            kickWindLevel = Mathf.Clamp01(kickWindHead);
            kickWindWindowUp = false;
        }
        else if (kickShift > 0.0001f && kickWindFlight >= 0f && balance.kickStrokeActive
                 && !balance.kickHeadActive && state != null)
        {
            // Stroke flight (one-legged phase and landing tail): reference on the standing foot's plate centre
            // at kickWindFlight's level. -1 (unset) = off: the stroke carries level 0.
            bool flightSupportIsLeft = balance.kickSideRight;
            Vector2 flightFoot = flightSupportIsLeft
                ? state.leftFootPosition : state.rightFootPosition;
            kickWindRefX = KickWindRefPoint(flightFoot);
            kickWindLevel = Mathf.Clamp01(kickWindFlight);
            kickWindWindowUp = false;
        }
        else
        {
            // A stroke in flight (or gain off): the window is down but the episode is not; mode 2 keeps its
            // frame, mode 1 re-arms for the next rising edge.
            kickWindWindowUp = false;
        }
        // ─── quiet stand ───
        // The D terms below read low-passed speeds (see clStandLpS). The filters run on every stand
        // tick; a tick after a gap (a walk, a fall) starts them from the live values.
        bool clUpright = crouch <= 0.0001f && kickShift <= 0.0001f;
        bool clScope = kickShift <= 0.0001f && (clUpright || clStandLpScope >= 2);
        if (clStandLpS > 0f)
        {
            float lpDt = Time.fixedDeltaTime;
            float kneeWL = leftKneeJoint != null ? leftKneeJoint.jointSpeed : 0f;
            float kneeWR = rightKneeJoint != null ? rightKneeJoint.jointSpeed : 0f;
            float bodyW = vestibular != null ? vestibular.GetBodyAngularVelocity() : 0f;
            if (Time.fixedTime - clLpTickTime > 1.5f * lpDt)
            {
                clLpPelvisW = pelvisAngVel;
                clLpKneeWL = kneeWL;
                clLpKneeWR = kneeWR;
                clLpBodyW = bodyW;
            }
            clLpTickTime = Time.fixedTime;
            float lpA = lpDt / (clStandLpS + lpDt);
            clLpPelvisW += (pelvisAngVel - clLpPelvisW) * lpA;
            clLpKneeWL += (kneeWL - clLpKneeWL) * lpA;
            clLpKneeWR += (kneeWR - clLpKneeWR) * lpA;
            clLpBodyW += (bodyW - clLpBodyW) * lpA;
            clLpNow = clScope;
        }
        if (clScope) clDNow = clStandDMul;
        if (clLpNow && (clStandLpMask & 1) != 0) pelvisAngVel = clLpPelvisW;
        // From here pelvisAngVel only feeds the pelvis law's D term (DriveHips).
        pelvisAngVel *= clDNow;
        if (clStandKneeMinDeg > 0f && clUpright) knee = Mathf.Max(knee, clStandKneeMinDeg);
        bool closeStep = clStanceCloseOn && kickShift <= 0.0001f
            && UpdateStanceClose(crouch, lean, comX, comVelX, leftDown, rightDown);
        // ─── leg strike ───
        // The kick hands the kicking leg's three targets to DriveOneLeg (one writer, kick owner stamped);
        // the support leg keeps the base call. Stand hips get no angle target: the pelvis law owns them.
        if (balance.kickStrokeActive)
        {
            // DriveOneLeg's first parameter is `left`, so the strike's side is passed inverted.
            bool kickRight = balance.kickSideRight;
            DriveOneLeg(!kickRight, balance.kickHipTargetDeg, balance.kickKneeTargetDeg,
                true, balance.kickAnkleTargetDeg, false, 1f, BalanceController.TapOwnerKick);
            DriveOneLeg(kickRight, float.NaN, knee, false, 0f, kickSupportHold > 0.5f);
        }
        else if (closeStep)
        {
            DriveStanceCloseLegs(knee, pelvisY, pelvisTilt);
        }
        else
        {
            // Handoff hold: through the settle's release window the stance knees may not extend past the hold
            // floor until the body is quiet. Off: kneeHold equals knee.
            float kneeHold = walkSettleHoldLive && walkSettleHoldDeg > 0.001f
                ? Mathf.Max(knee, walkSettleHoldDeg) : knee;
            bool kneeLp = clLpNow && (clStandLpMask & 2) != 0;
            DriveOneLeg(true, float.NaN, kneeHold, false, 0f, kneeLp: kneeLp);
            DriveOneLeg(false, float.NaN, kneeHold, false, 0f, kneeLp: kneeLp);
        }
        // ─── kick support plant ───
        // The support hip holds the angle the strike latched while the episode is on. During a stroke the
        // kicking hip is the strike's alone: the pelvis law is side-blind and the legs are not mirrored.
        if (kickPlantOn)
        {
            bool kickRightSide = balance.kickSideRight;
            // The drive flags are the support side's (kickRightSide, !kickRightSide), so the hold reaches the
            // support hip whichever side kicks.
            DriveHips(pelvisTilt, pelvisAngVel, pelvisTargetTilt,
                kickRightSide, !kickRightSide, balance.kickSupportHipDeg, true, kickPlantBand);
            if (!balance.kickStrokeActive)
                DriveHips(pelvisTilt, pelvisAngVel, pelvisTargetTilt,
                    !kickRightSide, kickRightSide, 0f, false);
        }
        else if (closeStep)
        {
            // The stepping hip is the step's alone; the pelvis law keeps the support hip.
            DriveHips(pelvisTilt, pelvisAngVel, pelvisTargetTilt,
                !clCloseSwingLeft, clCloseSwingLeft, 0f, false);
        }
        else
        {
            DriveHips(pelvisTilt, pelvisAngVel, pelvisTargetTilt, true, true, 0f, false);
        }
        // The stepping foot's ankle is a pose joint for the step (DriveStanceCloseLegs).
        DriveAnkleBalance(comXAnkle, comVelX,
            leftDown && !(closeStep && clCloseSwingLeft), rightDown && !(closeStep && !clCloseSwingLeft));
        // ─── stand toe hold ───
        // Arms only while both soles genuinely rest flat; the dwell keeps a plate near the band from
        // flapping the gain. Blocked for squat takes (MotionIntent.toeHoldBlock) and armed squat reps.
        bool holdAllowed = standToeSquatGate <= 0.5f
            || !(intent != null && (intent.toeHoldBlock > 0 || intent.squatReps > 0));
        bool toeFlatNow = false;
        if (standToeHoldScale > 1.0001f && state != null && holdAllowed
            && state.leftFootGrounded && state.rightFootGrounded)
        {
            float footWorldL = state.pelvisTilt - state.leftHipAngle
                - state.leftKneeAngle - state.leftAnkleAngle;
            float footWorldR = state.pelvisTilt - state.rightHipAngle
                - state.rightKneeAngle - state.rightAnkleAngle;
            float armDeg = standToeFlatArmed ? standToeFlatDropDeg : standToeFlatArmDeg;
            toeFlatNow = Mathf.Abs(footWorldL) <= armDeg && Mathf.Abs(footWorldR) <= armDeg;
        }
        if (!holdAllowed)
        {
            // Blocked: disarm at once, no dwell.
            standToeFlatArmed = false;
            standToeFlatAge = 0f;
        }
        else if (toeFlatNow != standToeFlatArmed)
        {
            standToeFlatAge += Time.fixedDeltaTime;
            if (standToeFlatAge >= standToeFlatHoldSeconds)
            {
                standToeFlatArmed = toeFlatNow;
                standToeFlatAge = 0f;
            }
        }
        else
        {
            standToeFlatAge = 0f;
        }
        if (standToeFlatArmed)
            DriveToes(standToeHoldBiasDeg, standToeHoldBiasDeg, standToeHoldScale);
        else
            DriveToes(0f, 0f, 1f);
        DriveArms(0f, 0f, balance.elbowBaseAngle, balance.elbowBaseAngle);
        // tilt and foot heights are unused: the trunk driver reads tilt; foot heights matter on the walk.
    }

    // ─── stance close ───
    // Runs the phases (see clStanceCloseOn); true while the step owns the rear leg. A stand tick
    // after a gap (a walk, a fall) starts over. At most two attempts per stand.
    private bool UpdateStanceClose(float crouch, float lean, float comX, float comVelX,
                                   bool leftDown, bool rightDown)
    {
        float dt = Time.fixedDeltaTime;
        if (!ClCloseFresh)
        {
            clClosePhase = 0;
            clCloseQuietT = 0f;
            clCloseTries = 0;
            clClosePullRun = false;
        }
        clCloseTickTime = Time.fixedTime;
        if (leftAnkleJoint == null || rightAnkleJoint == null || human == null) return false;
        Vector2 ankleL = leftAnkleJoint.transform.TransformPoint(leftAnkleJoint.anchor);
        Vector2 ankleR = rightAnkleJoint.transform.TransformPoint(rightAnkleJoint.anchor);
        bool otherLayer = crouch > 0.0001f || balance.crouchLevel > 0.0001f
            || Mathf.Abs(lean) > 0.0001f || balance.kickStrokeActive;
        if (clClosePhase == 0)
        {
            bool arm = leftDown && rightDown && !otherLayer && clCloseTries < 2
                && (clCloseRearmS <= 0f || Time.fixedTime - clCloseDoneTime >= clCloseRearmS)
                && Mathf.Abs(ankleL.x - ankleR.x) >= ClCloseMinNow && Mathf.Abs(comVelX) <= 0.30f;
            clCloseQuietT = arm ? clCloseQuietT + dt : 0f;
            if (clCloseQuietT < 0.10f) return false;
            clClosePhase = 1;
            clCloseT = 0f;
            clCloseQuietT = 0f;
            clCloseFrontUpT = 0f;
            clCloseHoldUsed = 0f;
            clCloseTries++;
            clCloseSwingLeft = ankleL.x < ankleR.x;
            Debug.Log(string.Format(CultureInfo.InvariantCulture,
                "[stanceClose] shift t={0:F3} swing={1} stagger={2:F3} comX={3:F3}",
                Time.fixedTime, clCloseSwingLeft ? "L" : "R", Mathf.Abs(ankleL.x - ankleR.x), comX));
        }
        Vector2 front = clCloseSwingLeft ? ankleR : ankleL;
        Vector2 rear = clCloseSwingLeft ? ankleL : ankleR;
        clCloseHomeX = front.x - human.ankleHeelOffset + human.footSize.x * 0.5f;
        // -clClosePullHome <m> (0 = off): while the pull runs the loop's reference stays at the pull's
        // landing line (the target + m) instead of stepping to the old rear sole: the flip would move
        // it by the whole stagger (1.4 m in the worst takes) and the loop's command reversal mid-pull
        // is what topples an already quiet body. The foot lands beside a held CoM; the close that
        // follows (the pull makes it satisfiable) does the rest.
        // The pin: m > 0 holds the CoM where it is; m < 0 lets it ease back so the loaded foot can
        // drag at all (a midway split keeps both feet loaded and the pose leg's drag stalls).
        if (clClosePullRun && clClosePullHomeM != 0f) clCloseHomeX = clClosePullX1 + clClosePullHomeM;
        clCloseT += dt;
        if (clClosePhase == 1)
        {
            // A front sole flickering off the ground does not end the shift; 0.15 s off does.
            bool frontDown = clCloseSwingLeft ? rightDown : leftDown;
            clCloseFrontUpT = frontDown ? 0f : clCloseFrontUpT + dt;
            // -clCloseShiftHold <s> (0 = off): a front sole that lifts mid-shift no longer ends the
            // shift at once. The loop's reference is pinned at the current CoM while the sole is off
            // the ground, for up to <s> s per lift, so the sole can re-plant and the shift can
            // finish; without the pin the reference keeps driving the CoM onto a foot in the air.
            // The usual abort stands once the pin's budget is spent (or the stall hits).
            if (clCloseShiftHoldS > 0f)
            {
                if (frontDown) clCloseHoldUsed = 0f;
                else if (clCloseFrontUpT > 0.15f && clCloseHoldUsed < clCloseShiftHoldS)
                {
                    if (clCloseHoldUsed == 0f)
                        Debug.Log(string.Format(CultureInfo.InvariantCulture,
                            "[stanceClose] shifthold t={0:F3} comX={1:F3} home={2:F3}",
                            Time.fixedTime, comX, clCloseHomeX));
                    clCloseHomeX = comX;
                    clCloseHoldUsed += dt;
                }
            }
            bool holdPin = clCloseShiftHoldS > 0f && clCloseFrontUpT > 0.15f
                && clCloseHoldUsed < clCloseShiftHoldS;
            if (otherLayer || (clCloseFrontUpT > 0.15f && !holdPin) || clCloseT > clCloseStallS)
            {
                // -clClosePullBack (0 = off): the shift timed out with the CoM still short of the front
                // foot (the abort below). Instead of giving up, close from the other side: flip the
                // swing so the step runs BACKWARD - the front foot comes back toward the CoM, its aim
                // the usual level-feet gap off the old rear ankle. Same step machinery, same logs.
                if (clClosePullComM > 0f && clCloseT > clCloseStallS && !otherLayer && clCloseFrontUpT <= 0.15f
                    && comX < clCloseHomeX - 0.05f)
                {
                    // -clClosePullCom <m> (0 = off): the shift timed out short of the front foot. Step
                    // the FRONT foot back to just behind the CoM (the target is FROZEN - the loop's
                    // per-tick re-aim would otherwise drive it past the old rear ankle). The CoM never
                    // leaves the hull, and the next attempt (the usual close, now satisfiable) finishes
                    // level feet.
                    clCloseSwingLeft = !clCloseSwingLeft;
                    Vector2 fc = clCloseSwingLeft ? ankleR : ankleL;
                    Vector2 rc = clCloseSwingLeft ? ankleL : ankleR;
                    clClosePullRun = true;
                    clClosePullX1 = comX - clClosePullComM;
                    Debug.Log(string.Format(CultureInfo.InvariantCulture,
                        "[stanceClose] pullcom t={0:F3} comX={1:F3} from={2:F3} to={3:F3}",
                        Time.fixedTime, comX, rc.x, clClosePullX1));
                    clClosePhase = 2;
                    clCloseT = 0f;
                    clCloseAnkleX0 = rc.x;
                    clCloseAnkleY0 = rc.y;
                    clCloseAnkleX1 = clClosePullX1;
                    clCloseStepNowS = clCloseSpeedMps > 0f
                        ? Mathf.Max(clCloseStepS, Mathf.Abs(clCloseAnkleX1 - rc.x) / clCloseSpeedMps)
                        : clCloseStepS;
                    clCloseAnkleY1 = fc.y;
                    clCloseLiftNow = 0f;
                    clCloseMeasX = rc.x;
                    clCloseErrX = 0f;
                    clCloseRetryN = 0;
                    clCloseNudgeN = 0;
                    clCloseLoadHoldT = 0f;
                    clCloseKneeHoldT = 0f;
                    return true;
                }
                if (clClosePullBackOn && clCloseT > clCloseStallS && !otherLayer && clCloseFrontUpT <= 0.15f
                    && comX < clCloseHomeX - 0.05f)
                {
                    clCloseSwingLeft = !clCloseSwingLeft;
                    Vector2 f2 = clCloseSwingLeft ? ankleR : ankleL;
                    Vector2 r2 = clCloseSwingLeft ? ankleL : ankleR;
                    Debug.Log(string.Format(CultureInfo.InvariantCulture,
                        "[stanceClose] pullback t={0:F3} comX={1:F3} from={2:F3} to={3:F3}",
                        Time.fixedTime, comX, r2.x, f2.x + clCloseAimM));
                    clClosePhase = 2;
                    clCloseT = 0f;
                    clCloseAnkleX0 = r2.x;
                    clCloseAnkleY0 = r2.y;
                    clCloseAnkleX1 = f2.x + clCloseAimM;
                    clCloseStepNowS = clCloseSpeedMps > 0f
                        ? Mathf.Max(clCloseStepS, Mathf.Abs(clCloseAnkleX1 - r2.x) / clCloseSpeedMps)
                        : clCloseStepS;
                    clCloseAnkleY1 = f2.y;
                    clCloseLiftNow = 0f;
                    clCloseMeasX = r2.x;
                    clCloseErrX = 0f;
                    clCloseRetryN = 0;
                    clCloseNudgeN = 0;
                    clCloseLoadHoldT = 0f;
                    clCloseKneeHoldT = 0f;
                    return true;
                }
                Debug.Log(string.Format(CultureInfo.InvariantCulture,
                    "[stanceClose] abort t={0:F3} age={1:F2} comX={2:F3} home={3:F3}",
                    Time.fixedTime, clCloseT, comX, clCloseHomeX));
                clClosePullRun = false;
                clClosePhase = 0;
                return false;
            }
            // The step may start once the CoM is 0.05 m inside the front sole and not running.
            float heelX = front.x - human.ankleHeelOffset;
            if (comX < heelX + 0.05f || Mathf.Abs(comVelX) > 0.40f) return false;
            clClosePhase = 2;
            clCloseT = 0f;
            clCloseAnkleX0 = rear.x;
            clCloseAnkleY0 = rear.y;
            // -clStanceCloseAim: the step lands ~0.09 m past the front ankle (the body still moves
            // while it steps), and the stand that follows rests on that one leg; the aim offset (m,
            // minus = short of the front ankle) lets the close end with level feet.
            clCloseAnkleX1 = front.x + clCloseAimM;
            clCloseStepNowS = clCloseSpeedMps > 0f
                ? Mathf.Max(clCloseStepS, Mathf.Abs(clCloseAnkleX1 - rear.x) / clCloseSpeedMps)
                : clCloseStepS;
            clCloseAnkleY1 = front.y;
            clCloseLiftNow = 0f;
            clCloseMeasX = rear.x;
            clCloseErrX = 0f;
            clCloseRetryN = 0;
            clCloseNudgeN = 0;
            clCloseLoadHoldT = 0f;
            clCloseKneeHoldT = 0f;
            Debug.Log(string.Format(CultureInfo.InvariantCulture,
                "[stanceClose] step t={0:F3} comX={1:F3} v={2:F3} from={3:F3} to={4:F3}",
                Time.fixedTime, comX, comVelX, rear.x, front.x));
            return true;
        }
        // Step: ends on the stepping sole's contact after the full path, or 0.6 of a path later.
        float u = clCloseT / Mathf.Max(0.05f, clCloseStepNowS);
        bool swingDown = clCloseSwingLeft ? leftDown : rightDown;
        bool late = u >= 1.6f;
        bool landed = u >= 1f && swingDown;
        if (clCloseLoopOn)
        {
            clCloseAnkleX1 = clClosePullRun ? clClosePullX1
                : (clCloseRetryGain > 0f && clCloseRetryN > 0 ? clCloseRetryX1 : front.x + clCloseAimM);
            clCloseAnkleY1 = front.y;
            clCloseMeasX = rear.x;
            clCloseErrX = rear.x - clCloseAnkleX1;
            // -clStanceCloseHoldHop <m> (0 = off): with Hold on, an x error stuck past m for 0.4 s
            // while the descent waits gets ONE quiet re-hop toward the target - the stalled foot is
            // out of the converged servo's reach, so the hold alone ends far short.
            if (clCloseHoldHop > 0f && clCloseRetryN == 0 && u >= 1f
                && Mathf.Abs(clCloseErrX) > clCloseHoldHop && clCloseT >= clCloseStepNowS + 0.4f)
            {
                clCloseRetryN++;
                clCloseT = 0f;
                clCloseLiftNow = ClCloseLiftM;
                clCloseAnkleX0 = rear.x;
                clCloseAnkleX1 = clCloseRetryGain > 0f
                    ? rear.x + Mathf.Clamp01(clCloseRetryGain) * (front.x + clCloseAimM - rear.x)
                    : front.x + clCloseAimM;
                // The per-tick aim line below re-reads clCloseRetryX1 once RetryN > 0; freeze the
                // hop's target here, else the gain aim is lost and the path drives toward x = 0.
                clCloseRetryX1 = clCloseAnkleX1;
                float hopMin = clCloseRetryT > 0f ? clCloseRetryT : 0.3f;
                clCloseStepNowS = Mathf.Max(hopMin, clCloseSpeedMps > 0f
                    ? Mathf.Abs(clCloseAnkleX1 - rear.x) / clCloseSpeedMps : hopMin);
                Debug.Log(string.Format(CultureInfo.InvariantCulture,
                    "[stanceClose] hop t={0:F3} err={1:F3}", Time.fixedTime, clCloseErrX));
                return true;
            }
            // -clStanceCloseRetry <m> (0 = off): a landing further than m from the target lifts the
            // stepping foot again and re-runs a short approach to the measured target, up to two tries.
            // The done still reports the first seated landing; the retry only buys a better one.
            // -clStanceCloseRetries <n> raises the try cap (0 = 2).
            int retryMax = clCloseRetryExtra > 0 ? clCloseRetryExtra : 2;
            if (clCloseRetryM > 0f && swingDown && u >= 1f
                && Mathf.Abs(clCloseErrX) > clCloseRetryM && clCloseRetryN < retryMax)
            {
                clCloseRetryN++;
                clCloseT = 0f;
                clCloseLiftNow = ClCloseLiftM;
                clCloseAnkleX0 = rear.x;
                // -clStanceCloseRetryGain <g> (0 = off): the re-hop aims at g of the current miss,
                // not at the target - the sub-step overshoots its aim (measured +0.03 on a 0.02 aim).
                // The target is FROZEN for the re-hop (the per-tick aim line below keeps it) - the
                // re-aimed path itself is what pulled the foot past the target before.
                clCloseRetryX1 = clCloseRetryGain > 0f
                    ? rear.x + Mathf.Clamp01(clCloseRetryGain) * (front.x + clCloseAimM - rear.x)
                    : front.x + clCloseAimM;
                clCloseAnkleX1 = clCloseRetryX1;
                float retryMin = clCloseRetryT > 0f ? clCloseRetryT : 0.3f;
                clCloseStepNowS = Mathf.Max(retryMin, clCloseSpeedMps > 0f
                    ? Mathf.Abs(clCloseAnkleX1 - rear.x) / clCloseSpeedMps : retryMin);
                Debug.Log(string.Format(CultureInfo.InvariantCulture,
                    "[stanceClose] retry t={0:F3} n={1} err={2:F3}", Time.fixedTime, clCloseRetryN, clCloseErrX));
                return true;
            }
            // -clClosePullLift <k> (1 = the usual flight): k scales the pull step's lift arc - k = 0
            // drags the sole along the ground, so the CoM never leaves the two-foot hull mid-pull.
            float arcK = clClosePullRun ? clClosePullLiftK : 1f;
            if (u < 1f)
                clCloseLiftNow = ClCloseLiftM * arcK * Mathf.Sin(0.5f * Mathf.PI * Mathf.Clamp01(u / 0.3f));
            else
            {
                // -clStanceCloseHold <m> (0 = off): the descent waits while the x error is still large,
                // so the sole lands once the servo has converged; after stepNow+1 s the usual pacing
                // resumes and the late timeout still ends the step.
                float holdGate = clCloseHoldM > 0f && clCloseT < clCloseStepNowS + 1f ? clCloseHoldM : -1f;
                float pace = holdGate >= 0f && Mathf.Abs(clCloseErrX) > holdGate
                    ? 0f : Mathf.Clamp(1f - Mathf.Abs(clCloseErrX) / clCloseTolM, 0.2f, 1f);
                clCloseLiftNow = Mathf.Max(-0.03f, clCloseLiftNow - clCloseLandMps * dt * pace);
            }
            late = clCloseT >= clCloseStepNowS + (clCloseDoneLoad > 0f || clCloseDoneKnee > 0f ? 3f : 1.5f);
            // -clStanceCloseSeat: the sole touches with the lift still asked (knee ~30 deg); the step
            // ends once the descent has seated the foot, so the stand does not straighten the knee at once.
            if (clCloseSeatM > 0f) landed = landed && clCloseLiftNow <= clCloseSeatM;
            // -clCloseDoneLoad <N> (0 = off): the step ends only once the stepping sole carries this
            // much ground load - a done logged at first touch hands the stand a leg it then straightens
            // and slides. While the gate holds, the late timeout runs at stepNow + 3 s (see above).
            if (clCloseDoneLoad > 0f && landed && !late)
            {
                float fzSwing = clCloseSwingLeft ? poseGroundFzLeftN : poseGroundFzRightN;
                if (fzSwing < clCloseDoneLoad)
                {
                    if (clCloseLoadHoldT == 0f)
                        Debug.Log(string.Format(CultureInfo.InvariantCulture,
                            "[stanceClose] loadwait t={0:F3} fz={1:F0} need={2:F0}",
                            Time.fixedTime, fzSwing, clCloseDoneLoad));
                    landed = false;
                    clCloseLoadHoldT += dt;
                }
                else if (clCloseLoadHoldT > 0f)
                {
                    Debug.Log(string.Format(CultureInfo.InvariantCulture,
                        "[stanceClose] loadok t={0:F3} fz={1:F0} held={2:F2}",
                        Time.fixedTime, fzSwing, clCloseLoadHoldT));
                    clCloseLoadHoldT = 0f;
                }
            }
            // -clCloseDoneKnee <deg> (0 = off): the done also waits for the stepping knee: the stand
            // that takes the leg over holds its own knee law (KneeForHeight), and a done handed over
            // with the step's knee still ~20 deg bent lets that straightening slide the light foot.
            // The step ends only once the knee is within <deg> of the stand's law.
            if (clCloseDoneKnee > 0f && landed && !late)
            {
                float kneeGap = Mathf.Abs(clCloseSwingKneeDeg - clCloseStandKneeDeg);
                if (kneeGap > clCloseDoneKnee)
                {
                    if (clCloseKneeHoldT == 0f)
                        Debug.Log(string.Format(CultureInfo.InvariantCulture,
                            "[stanceClose] kneewait t={0:F3} knee={1:F1} stand={2:F1} gap={3:F1}",
                            Time.fixedTime, clCloseSwingKneeDeg, clCloseStandKneeDeg, kneeGap));
                    landed = false;
                    clCloseKneeHoldT += dt;
                }
                else if (clCloseKneeHoldT > 0f)
                {
                    Debug.Log(string.Format(CultureInfo.InvariantCulture,
                        "[stanceClose] kneeok t={0:F3} knee={1:F1} gap={2:F1} held={3:F2}",
                        Time.fixedTime, clCloseSwingKneeDeg, kneeGap, clCloseKneeHoldT));
                    clCloseKneeHoldT = 0f;
                }
            }
            // -clStanceCloseNudge <m> (0 = off): a finished landing further than m from the aim (the
            // metric's own bar) gets ONE immediate micro re-step - the landing scatter, not a stall,
            // is what the aim cannot hold; a landing that would pass is left untouched.
            if (clCloseNudgeM > 0f && clCloseNudgeN == 0 && landed && !late
                && Mathf.Abs(clCloseErrX + clCloseAimM) > clCloseNudgeM)
            {
                clCloseNudgeN++;
                clCloseT = 0f;
                clCloseLiftNow = ClCloseLiftM;
                clCloseAnkleX0 = rear.x;
                clCloseAnkleX1 = front.x + clCloseAimM;
                float nudMin = clCloseRetryT > 0f ? clCloseRetryT : 0.3f;
                clCloseStepNowS = Mathf.Max(nudMin, clCloseSpeedMps > 0f
                    ? Mathf.Abs(clCloseAnkleX1 - rear.x) / clCloseSpeedMps : nudMin);
                Debug.Log(string.Format(CultureInfo.InvariantCulture,
                    "[stanceClose] nudge t={0:F3} err={1:F3}", Time.fixedTime, clCloseErrX));
                return true;
            }
            if (landed || late)
                Debug.Log(string.Format(CultureInfo.InvariantCulture,
                    "[stanceClose] land t={0:F3} err={1:F3} lift={2:F3} age={3:F2} v={4:F3}",
                    Time.fixedTime, clCloseErrX, clCloseLiftNow, clCloseT, comVelX));
        }
        if (landed || late)
        {
            Debug.Log(string.Format(CultureInfo.InvariantCulture,
                "[stanceClose] done t={0:F3} down={1} stagger={2:F3} comX={3:F3}",
                Time.fixedTime, swingDown ? 1 : 0, Mathf.Abs(ankleL.x - ankleR.x), comX));
            if (clCloseRearmS > 0f && swingDown && Mathf.Abs(ankleL.x - ankleR.x) < clCloseMinM)
            {
                clCloseTries = 0;
                clCloseDoneTime = Time.fixedTime;
            }
            clClosePullRun = false;
            clClosePhase = 0;
            return false;
        }
        return true;
    }

    // The stepping leg follows an ankle path (smoothstep along x, a sine lift) through the two-link
    // solve with the sole kept level; the support leg keeps the stand's knee law.
    private void DriveStanceCloseLegs(float knee, float pelvisY, float pelvisTilt)
    {
        float u = Mathf.Clamp01(clCloseT / Mathf.Max(0.05f, clCloseStepNowS));
        float e = u * u * (3f - 2f * u);
        float ankleX = Mathf.Lerp(clCloseAnkleX0, clCloseAnkleX1, e);
        float ankleY = Mathf.Lerp(clCloseAnkleY0, clCloseAnkleY1, e)
            + ClCloseLiftM * (clClosePullRun ? clClosePullLiftK : 1f) * Mathf.Sin(Mathf.PI * u);
        // -clStanceCloseDrop <m/s> (0 = off): past the path's end the ankle target keeps going down
        // until the sole lands. Without it a step whose sole hovers at the path's end is closed on the
        // timeout (`done down=0`) and the stand inherits a leg that is not on the ground.
        if (clCloseDropMps > 0f)
            ankleY -= clCloseDropMps * Mathf.Max(0f, clCloseT - clCloseStepNowS);
        if (clCloseLoopOn)
        {
            // x: the path plus the push against the measured error; y: the landing line plus the lift.
            ankleX += Mathf.Clamp(clCloseKx * (ankleX - clCloseMeasX), -0.08f, 0.08f);
            ankleY = Mathf.Lerp(clCloseAnkleY0, clCloseAnkleY1, e) + clCloseLiftNow;
        }
        SolveLegIk(ankleX, ankleY, pelvisY, pelvisTilt, out float hipDeg, out float kneeDeg,
            clCloseLoopOn ? 0.9995f : 0.995f);
        // The knee gate's reads (see clCloseDoneKnee): the stepping knee against the stand's law.
        clCloseSwingKneeDeg = kneeDeg;
        clCloseStandKneeDeg = knee;
        // Foot world angle = pelvisTilt - hip - knee - ankle; 0 = a level sole.
        float ankleDeg = pelvisTilt - hipDeg - kneeDeg;
        DriveOneLeg(clCloseSwingLeft, hipDeg, kneeDeg, true, ankleDeg);
        DriveOneLeg(!clCloseSwingLeft, float.NaN, knee, false, 0f);
    }

    // The release hook's own lead hand-over (req-136 C1): the plant frame's contactX - comX, latched in
    // TickWalk just before ReleasePhaseOnPlant; the hook's lead >= 0.20 term reads it.
    private float releasePhasePlantLeadM;

    // Recorder only: one row per plant, on the frame the swing sole lands after its lift (latched in
    // TickWalk via posePlantFrame). See WalkPlantLog for columns and signs; nothing is driven here.
    private void RecordPlantRow(bool plantLeft, int stepIndex, float comX,
        float pelvisY, float swingPhase, float stepClockT, float leftFootX,
        float leftFootY, float rightFootX, float rightFootY)
    {
        float contactX = plantLeft ? leftFootX : rightFootX;
        float contactY = plantLeft ? leftFootY : rightFootY;
        HingeJoint2D hip = plantLeft ? leftHipJoint : rightHipJoint;
        Vector2 hipPoint;
        if (hip != null)
            hipPoint = hip.transform.TransformPoint(hip.anchor);
        else if (pelvisBody != null)
            hipPoint = pelvisBody.position;
        else
            hipPoint = new Vector2(contactX, pelvisY);
        // The landing key clamped exactly as PoseLibrary.Blend does, so blendU is that blend's own u.
        float land = Mathf.Clamp(swingLandShare, PoseLibrary.FoldKey + 0.05f, 0.98f);
        float blendU = Mathf.Clamp01((swingPhase - PoseLibrary.FoldKey)
            / Mathf.Max(0.05f, land - PoseLibrary.FoldKey));
        Vector2 contact = new Vector2(contactX, contactY);
        WalkPlantLog.Record(new WalkPlantLog.Row
        {
            k = WalkPlantLog.Count + 1,
            time = Time.fixedTime,
            plantLeft = plantLeft ? 1f : 0f,
            stepIndex = stepIndex,
            contactX = contactX,
            comX = comX,
            lead = contactX - comX,
            chord = Vector2.Distance(hipPoint, contact),
            hipY = hipPoint.y,
            pelvisY = pelvisY,
            planStepT = swingDuration,
            plantClockT = stepClockT,
            landBlendU = blendU
        });
    }

    private float KneeForHeight(float pelvisY, float pelvisVelY)
    {
        float error = standPelvisY - pelvisY;
        float knee = balance.kneeBaseAngle
            - heightGainDegPerMeter * error
            - heightDampDegPerMeterPerSecond * pelvisVelY;
        // Error term minus: a low pelvis needs a longer leg (length ~ cos(knee/2)). Rate term: a rising
        // pelvis extends the leg (the sign that held in trials).
        return Mathf.Clamp(knee, 0f, 60f);
    }

    // One leg, one writer. hipDeg NaN = no hip angle target (on the stand the pelvis-upright law owns
    // it); driveAnkle false = the balance loop drives the ankle as a torque instead of an angle.
    private void DriveOneLeg(bool left, float hipDeg, float kneeDeg,
                             bool driveAnkle, float ankleDeg, bool holdStiff = false,
                             float anklePFactor = 1f, int owner = BalanceController.TapOwnerNone,
                             float stepUpFactor = 0f, bool kneeLp = false)
    {
        HingeJoint2D hip = left ? leftHipJoint : rightHipJoint;
        Muscle hipFlexor = left ? balance.leftHipFlexor : balance.rightHipFlexor;
        Muscle hipExtensor = left ? balance.leftHipExtensor : balance.rightHipExtensor;
        ref float hipInertia = ref (left ? ref leftHipInertia : ref rightHipInertia);
        // Step-up delivery reaches the hip only via ReflexStepUpHipShare (default 0 = no hip boost).
        float hipP = stepUpFactor > 0f && ReflexStepUpHipShare > 0f
            ? balance.hipPGain * (1f + stepUpFactor * ReflexStepUpHipShare) : balance.hipPGain;
        if (!float.IsNaN(hipDeg))
        {
            DriveJoint(hip, hipDeg, hipP, balance.hipDGain,
                balance.errorReferenceDegrees, hipFlexor, hipExtensor, ref hipInertia);
            // The strike's targets come through this driver, so its owner code is stamped here too.
            NoteOwner(hip, hipDeg, owner);
            HipDbgWrite(left ? "legL" : "legR", mixer.lastDemand, hipDeg, hip, hipFlexor, hipExtensor);
            // This frame's hip demand for the arbitration; the timestamp lets DriveHipsArb reject a stale one.
            if (left) legHipSigL = mixer.lastDemand; else legHipSigR = mixer.lastDemand;
            legHipSigT = Time.fixedTime;
        }

        HingeJoint2D knee = left ? leftKneeJoint : rightKneeJoint;
        Muscle kneeFlexor = left ? balance.leftKneeFlexor : balance.rightKneeFlexor;
        Muscle kneeExtensor = left ? balance.leftKneeExtensor : balance.rightKneeExtensor;
        ref float kneeInertia = ref (left ? ref leftKneeInertia : ref rightKneeInertia);
        // JointStable: signal = (P*e - D*v) / (1 + damping), so a P multiplier scales the demand: the hold
        // asks StanceHoldDeliverP times the torque on support knee and ankle, clipped at muscle ceiling.
        // The hip keeps its gains: it is the library's trunk-correction channel, not a sole-height lever.
        float kneeP = holdStiff
            ? balance.kneePGain * PoseLibrary.StanceHoldDeliverP : balance.kneePGain;
        // Step-up on a rise (ReflexStepUpL/R) while the sole stands on its top: the knee's P scales by
        // (1 + factor); the ankle's plantar push gets Lerp(unboosted, boosted, ReflexStepUpAnkle). 0 = off.
        float kneePNoBoost = kneeP;
        if (stepUpFactor > 0f) kneeP *= 1f + stepUpFactor;
        float ankleKneeP = stepUpFactor > 0f
            ? Mathf.Lerp(kneePNoBoost, kneeP, Mathf.Clamp01(ReflexStepUpAnkle)) : kneeP;
        // kneeLp (the quiet stand): the same PD on the low-passed knee speed.
        if (kneeLp)
            DriveJointLp(knee, kneeDeg, kneeP, balance.kneeDGain,
                balance.errorReferenceDegrees, kneeFlexor, kneeExtensor, ref kneeInertia,
                left ? clLpKneeWL : clLpKneeWR);
        else
            DriveJoint(knee, kneeDeg, kneeP, balance.kneeDGain,
                balance.errorReferenceDegrees, kneeFlexor, kneeExtensor, ref kneeInertia);
        NoteOwner(knee, kneeDeg, owner);

        // On the walk the ankle is a pose joint (+ = toe down / heel rise, - = toe up for clearance), held
        // with the swing ankle's PD pair. On the stand it stays a torque loop and skips this branch.
        if (driveAnkle)
        {
            HingeJoint2D ankle = left ? leftAnkleJoint : rightAnkleJoint;
            Muscle ankleFlexor = left ? balance.leftAnkleFlexor : balance.rightAnkleFlexor;
            Muscle ankleExtensor = left ? balance.leftAnkleExtensor : balance.rightAnkleExtensor;
            ref float ankleInertia = ref (left ? ref leftAnkleInertia : ref rightAnkleInertia);
            // anklePFactor (push-off delivery, 1 = off) acts on the ankle only; ankleKneeP carries the step-up
            // boost scaled by ReflexStepUpAnkle (1 = full boost, 0 = unboosted).
            DriveJoint(ankle, ankleDeg, ankleKneeP * anklePFactor, balance.kneeDGain,
                balance.errorReferenceDegrees, ankleFlexor, ankleExtensor, ref ankleInertia);
            NoteOwner(ankle, ankleDeg, owner);
        }
    }

    // Stamps the kick's ownership; a no-op for TapOwnerNone, i.e. every other DriveOneLeg caller.
    private void NoteOwner(HingeJoint2D joint, float targetDeg, int owner)
    {
        if (owner == BalanceController.TapOwnerNone || balance == null) return;
        int idx = balance.LegJointIndex(joint);
        if (idx >= 0) balance.NoteLegAngle(idx, targetDeg, mixer.lastDemand, owner);
    }

    private void DriveJoint(HingeJoint2D joint, float targetDeg, float pGain, float dGain,
                            float errorRef, Muscle flexor, Muscle extensor, ref float inertia)
    {
        if (joint == null || flexor == null || extensor == null) return;
        mixer.JointStable(joint, targetDeg, pGain, dGain, errorRef,
            flexor, extensor, ref inertia);
    }

    // ActuatorMixer.JointStable's arithmetic with the joint speed supplied by the caller (the quiet
    // stand's low-passed speed) in both the look-ahead and the D term.
    private void DriveJointLp(HingeJoint2D joint, float targetDeg, float pGain, float dGain,
                              float errorRef, Muscle flexor, Muscle extensor, ref float inertia,
                              float speedDegPerSec)
    {
        if (joint == null || flexor == null || extensor == null) return;
        if (joint.useLimits)
            targetDeg = Mathf.Clamp(targetDeg, joint.limits.min, joint.limits.max);
        float dt = Time.fixedDeltaTime;
        float error = Mathf.DeltaAngle(joint.jointAngle + speedDegPerSec * dt, targetDeg)
            / Mathf.Max(1f, errorRef);
        float raw = pGain * error - dGain * (speedDegPerSec / mixer.speedReference);
        float damping = 0f;
        if (inertia <= 0f) inertia = ActuatorMixer.EffectiveInertia(joint);
        if (inertia > 0f)
        {
            float k = dGain * flexor.maxTorque * Mathf.Rad2Deg / Mathf.Max(1f, mixer.speedReference);
            damping = k * dt / inertia;
        }
        float demand = raw / (1f + damping);
        mixer.lastDemand = demand;
        mixer.ApplySignal(Mathf.Clamp(demand, -1f, 1f), flexor, extensor);
    }

    // Pelvis to world vertical through the hips, plus the quiet-stance hip hold: the pelvis law alone
    // lets the hips splay, so past hipHoldBand the same signal gets a joint PD toward the pose angle
    // and a stand cannot drift into a lunge.
    private void DriveHips(float pelvisTilt, float pelvisAngVel, float targetTilt,
                           bool driveLeft, bool driveRight,
                           float holdAngle, bool allowHold, float holdBandDeg = -1f)
    {
        float error = Mathf.DeltaAngle(pelvisTilt, targetTilt)
            / Mathf.Max(1f, balance.pelvisErrorReferenceDegrees);
        float speed = pelvisAngVel / Mathf.Max(1f, balance.speedReferenceDegPerSec);
        float signal = Mathf.Clamp(
            balance.pelvisPGain * error - balance.pelvisDGain * speed, -1f, 1f);
        HipDbgWrite("pelvL", signal, holdAngle, leftHipJoint, balance.leftHipFlexor, balance.leftHipExtensor);
        HipDbgWrite("pelvR", signal, holdAngle, rightHipJoint, balance.rightHipFlexor, balance.rightHipExtensor);
        if (driveLeft) ApplyHipSignal(true, MixHipHold(signal, leftHipJoint, holdAngle, allowHold, holdBandDeg));
        if (driveRight) ApplyHipSignal(false, MixHipHold(signal, rightHipJoint, holdAngle, allowHold, holdBandDeg));
    }

    // Inside the band the pelvis law owns the hip; past it the joint pose takes over smoothly. A caller
    // may pass its own band (the kick plant's kickPlantBand); -1 = balance.hipHoldBand.
    private float MixHipHold(float pelvisSignal, HingeJoint2D hip, float holdAngleDeg, bool allowHold,
                             float bandDeg = -1f)
    {
        if (!allowHold || hip == null) return pelvisSignal;
        float band = bandDeg >= 0f ? bandDeg : balance.hipHoldBand;
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

    // Quiet ankle loop: torque against the CoM offset (not a spring to an angle), the standing law
    // scaled by the factors above; with the kick wind-up level up it regulates to the standing foot.
    // ankDbg*/ankLoop*: recorder-only copies of the loop's inputs and terms; no control reads them.
    private float ankDbgComX, ankDbgRefX, ankDbgOffN, ankDbgVelN;
    private float ankDbgP, ankDbgD, ankDbgRaw, ankDbgCmd;
    private float ankDbgSigL, ankDbgSigR, ankDbgLd, ankDbgRd;
    private float ankDbgCLL; // walk c127: the crouch gate's input (crouchLoadLevel)
    public float ankLoopComX => ankDbgComX;
    public float ankLoopRefX => ankDbgRefX;
    public float ankLoopOffN => ankDbgOffN;
    public float ankLoopVelN => ankDbgVelN;
    public float ankLoopP => ankDbgP;
    public float ankLoopD => ankDbgD;
    public float ankLoopRaw => ankDbgRaw;
    public float ankLoopCmd => ankDbgCmd;
    public float ankLoopSigL => ankDbgSigL;
    public float ankLoopSigR => ankDbgSigR;
    public float ankLoopLd => ankDbgLd;
    public float ankLoopRd => ankDbgRd;
    public float ankLoopCrouchLoad => ankDbgCLL;
    private void DriveAnkleBalance(float comX, float comVelX, bool leftDown, bool rightDown)
    {
        if (state == null) return;
        float refX = state.supportCenterX;
        // Stance close: the loop regulates to the front sole's middle.
        if (clStanceCloseOn && clClosePhase > 0 && clCloseTickTime == Time.fixedTime) refX = clCloseHomeX;
        float pScale = 1f;
        float dScale = 1f;
        if (kickWindLevel > 0.0001f)
        {
            refX = kickWindRefX;
            pScale = kickWindLevel;
            // Wind-up shape: P scales by the level, D by its square root. Equal scaling leaves the window badly
            // under-damped (load swings foot to foot); sqrt keeps the brake alive and is still 0 at level 0.
            dScale = Mathf.Sqrt(kickWindLevel);
        }
        float offsetN = (comX - refX) / Mathf.Max(0.01f, balance.comOffsetReference);
        float velN = comVelX / Mathf.Max(0.05f, balance.comVelocityReference);
        float command = Mathf.Clamp(
            ankleBalanceP * (balance.ankleComP * (offsetN * pScale))
            + ankleBalanceD * (balance.ankleComD * (velN * dScale)), -1f, 1f);
        // Stroke head: while the head is up the command is bounded on the flexor (push) side at
        // kickHeadPush; the extensor side (the brake) is untouched. -1 (default) = off.
        if (kickHeadPush >= 0f && balance.kickHeadActive && command < -kickHeadPush)
            command = -kickHeadPush;
        // Recorder only: the loop's inputs, terms, clamped command and contacts; the two signal fields get
        // what each ankle actually received in DriveAnkle (0 when that side is not down).
        ankDbgComX = comX;
        ankDbgRefX = refX;
        ankDbgOffN = offsetN;
        ankDbgVelN = velN;
        ankDbgP = ankleBalanceP * (balance.ankleComP * (offsetN * pScale));
        ankDbgD = ankleBalanceD * (balance.ankleComD * (velN * dScale));
        ankDbgRaw = ankDbgP + ankDbgD;
        ankDbgCmd = command;
        ankDbgLd = leftDown ? 1f : 0f;
        ankDbgRd = rightDown ? 1f : 0f;
        ankDbgCLL = balance.crouchLoadLevel;
        ankDbgSigL = 0f;
        ankDbgSigR = 0f;
        if (leftDown) DriveAnkle(true, command);
        if (rightDown) DriveAnkle(false, command);
    }

    private void DriveAnkle(bool left, float command)
    {
        HingeJoint2D ankle = left ? leftAnkleJoint : rightAnkleJoint;
        Muscle flexor = left ? balance.leftAnkleFlexor : balance.rightAnkleFlexor;
        Muscle extensor = left ? balance.leftAnkleExtensor : balance.rightAnkleExtensor;
        if (ankle == null || flexor == null || extensor == null) return;
        float capEta = balance.crouchLevel > 0.0001f ? clAnkleCapEta : clAnkleCapStandEta;
        if (capEta > 0f)
            command = CapAnkleToFoot(left, command, flexor, extensor, capEta);
        // Record the signal the joint actually received (after the stop-margin protect).
        float applied = mixer.ApplyAnkle(ankle, command, flexor, extensor,
            balance.ankleLimitMargin);
        if (left) ankDbgSigL = applied;
        else ankDbgSigR = applied;
    }

    // Largest signal this foot can carry without rotating: load x ankle-to-MTP arm (plantar, +) or
    // load x ankle-to-heel arm (dorsal, -), times eta, over the muscle ceiling. Unloaded foot -> 0.
    private float CapAnkleToFoot(bool left, float command, Muscle flexor, Muscle extensor, float eta)
    {
        float fz = Mathf.Max(0f, left ? poseGroundFzLeftN : poseGroundFzRightN);
        // Upright stand: the bound reads the load low-passed (0.2 s). On the raw load it closes a loop -
        // torque lifts the body, the load drops, the bound shuts, the body lands, the load spikes -
        // and the stand bounces on the spot. The crouch keeps the raw load.
        float capDt = Time.fixedDeltaTime;
        ref float fzLp = ref (left ? ref clCapFzLpL : ref clCapFzLpR);
        // A sole that was unloaded starts from the live load, not from 0.
        if (fzLp <= 1f) fzLp = fz;
        fzLp += (fz - fzLp) * capDt / (0.2f + capDt);
        if (balance.crouchLevel <= 0.0001f) fz = fzLp;
        float toeArm = 0.117f;
        float heelArm = 0.065f;
        if (human != null)
        {
            float heelShare = Mathf.Clamp(human.footHeelShare, 0.4f, 0.85f);
            toeArm = Mathf.Max(0f, human.footSize.x * heelShare - human.ankleHeelOffset);
            heelArm = Mathf.Max(0f, human.ankleHeelOffset);
        }
        if (command > 0f)
            return Mathf.Min(command, eta * fz * toeArm / Mathf.Max(1f, extensor.maxTorque));
        if (command < 0f)
            return Mathf.Max(command, -eta * fz * heelArm / Mathf.Max(1f, flexor.maxTorque));
        return command;
    }

    // gainScale > 1 only for the stand's flat-sole toe hold (the light MTP needs authority there); all
    // other callers keep the base gain.
    private void DriveToes(float leftDeg, float rightDeg, float gainScale = 1f)
    {
        DriveJoint(leftToeJoint, leftDeg, balance.toePGain * gainScale, balance.toeDGain,
            balance.toeErrorReferenceDegrees, balance.leftToeFlexor,
            balance.leftToeExtensor, ref leftToeInertia);
        DriveJoint(rightToeJoint, rightDeg, balance.toePGain * gainScale, balance.toeDGain,
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

    // Chest, neck and head to world vertical, as the old path: the joint sits on the segment, so +P and
    // +D enter as -P and -D. torsoTargetDeg: walk = pose library's, stand = lumbarTargetTilt.
    private void DriveTrunk(float pelvisTilt, float pelvisAngVel, float tilt,
                            bool walking, float torsoTargetDeg)
    {
        if (vestibular == null) return;
        bool posture = walking || balance.standLegLevel < 0.5f;
        if (posture)
        {
            float lumbarError = Mathf.DeltaAngle(tilt, torsoTargetDeg)
                / Mathf.Max(1f, balance.lumbarErrorReferenceDegrees);
            // Quiet stand: the D term reads the low-passed trunk speed, times the stand's D multiplier.
            float lumbarW = clLpNow && (clStandLpMask & 4) != 0
                ? clLpBodyW : vestibular.GetBodyAngularVelocity();
            float lumbarSpeed = lumbarW * clDNow
                / Mathf.Max(1f, balance.speedReferenceDegPerSec);
            float lumbarSignal = Mathf.Clamp(
                (-balance.lumbarPGain) * lumbarError - (-balance.lumbarDGain) * lumbarSpeed,
                -1f, 1f);
            mixer.ApplySignal(lumbarSignal, balance.lumbarFlexor, balance.lumbarExtensor);

            ApplyWorldUpright(vestibular.GetNeckTilt(), vestibular.GetNeckAngularVelocity(),
                neckJoint, balance.neckFlexor, balance.neckExtensor, ref neckInertia,
                walking, headJoint, ref neckLoadInertia);
            ApplyWorldUpright(vestibular.GetHeadTilt(), vestibular.GetHeadAngularVelocity(),
                headJoint, balance.headFlexor, balance.headExtensor, ref headInertia,
                walking, null, ref headLoadInertia);
        }
        else
        {
            DriveJoint(lumbarJoint, balance.hipBaseAngle, balance.lumbarPGain, balance.lumbarDGain,
                balance.lumbarErrorReferenceDegrees, balance.lumbarFlexor,
                balance.lumbarExtensor, ref lumbarInertia);
        }
        // pelvisTilt / pelvisAngVel are unused, kept for a trunk pose that follows the pelvis.
    }

    private void ApplyWorldUpright(float tilt, float angVel, HingeJoint2D joint,
                                   Muscle flexor, Muscle extensor, ref float inertia,
                                   bool walkHold, HingeJoint2D carriedJoint, ref float loadCache)
    {
        if (joint == null || flexor == null || extensor == null) return;
        float dt = Time.fixedDeltaTime;
        float tiltForError = tilt + angVel * dt;
        float damping = 0f;
        if (inertia <= 0f) inertia = ActuatorMixer.EffectiveInertia(joint);
        // Walk hold: the divisor counts the carried chain's inertia.
        float divisorInertia = walkHold && walkNeckHoldShare > 0f
            ? WalkHoldInertia(joint, inertia, carriedJoint, ref loadCache)
            : inertia;
        if (divisorInertia > 0f)
        {
            float k = balance.neckDGain * flexor.maxTorque * Mathf.Rad2Deg
                      / Mathf.Max(1f, balance.speedReferenceDegPerSec);
            damping = k * dt / divisorInertia;
        }
        float error = Mathf.DeltaAngle(tiltForError, balance.neckTargetTilt)
            / Mathf.Max(1f, balance.neckErrorReferenceDegrees);
        float speed = angVel / Mathf.Max(1f, balance.speedReferenceDegPerSec);
        float raw = (-balance.neckPGain) * error - (-balance.neckDGain) * speed;
        float signal = Mathf.Clamp(raw / (1f + damping), -1f, 1f);
        mixer.ApplySignal(signal, flexor, extensor);
    }

    // Divisor inertia with the chain counted: the joint's body and the carried child's, each at its arm
    // about this pivot plus its own rotation; blended by walkNeckHoldShare, cached once per bind.
    private float WalkHoldInertia(HingeJoint2D joint, float baseInertia,
                                  HingeJoint2D carriedJoint, ref float loadCache)
    {
        if (loadCache <= 0f)
        {
            loadCache = baseInertia;
            Vector2 pivot = joint.transform.TransformPoint(joint.anchor);
            Rigidbody2D self = joint.attachedRigidbody;
            if (self != null)
                loadCache = SelfHoldInertia(loadCache, self, pivot);
            Rigidbody2D carried = carriedJoint != null ? carriedJoint.attachedRigidbody : null;
            if (carried != null && carried != self)
                loadCache = SelfHoldInertia(loadCache, carried, pivot);
        }
        return Mathf.Lerp(baseInertia, loadCache, walkNeckHoldShare);
    }

    private static float SelfHoldInertia(float load, Rigidbody2D body, Vector2 pivot)
    {
        float arm = ((Vector2)body.worldCenterOfMass - pivot).magnitude;
        return load + body.mass * arm * arm + body.inertia;
    }

    // Invariant-culture number for the trace (the CSV's convention).
    private static string N(float value, int digits)
    {
        return value.ToString("F" + digits, CultureInfo.InvariantCulture);
    }

    private static string GetString(string name, string fallback)
    {
        string[] args = LauncherArgs.Args();
        int index = System.Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : fallback;
    }

    private void OnDestroy()
    {
        CloseGetUpTrace();
    }

    private void OnApplicationQuit()
    {
        CloseGetUpTrace();
    }

    private void RelaxAll()
    {
        HipDbgWrite("relax", 0f, 0f, null, balance.leftHipFlexor, balance.leftHipExtensor);
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

    // The palm's collider, looked up by segment name like the wrist joints in Initialize.
    private static Collider2D HandCollider(Transform root, string name)
    {
        Transform t = root.Find(name);
        return t != null ? t.GetComponent<Collider2D>() : null;
    }

    // ─── lookups and command line ───
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

    // This file reads its own switches, so neither the frozen -walk surface nor the old path is edited.
    private static float GetFloat(string name, float fallback)
    {
        string[] args = LauncherArgs.Args();
        int index = System.Array.IndexOf(args, name);
        if (index < 0 || index + 1 >= args.Length) return fallback;
        return float.TryParse(args[index + 1], NumberStyles.Float,
            CultureInfo.InvariantCulture, out float value) ? value : fallback;
    }
}
