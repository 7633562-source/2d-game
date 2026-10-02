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
    // WALK CYCLE 64: THE ASK-SIDE TIER. The DRAWN stance leg's knee at the three
    // keys that hold the pelvis through mid-late stance (PoseLibrary: SwingFold 10,
    // FootPlace 12, PlantLoad 10). A genome's g[13..15] or the matching switches
    // move the ASK ITSELF; at these drawn values the blend is the shipped pose bit
    // for bit (c63 section 4's named lever: the drawn keys).
    private float stanceFoldKneeDeg = 10f;
    private float stancePlaceKneeDeg = 12f;
    private float stanceLoadKneeDeg = 10f;
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
    // WALK CYCLE 62: THE STEP'S OWN VAULT — the pelvis/torso vertical motion
    // (the eye items' first defect: "no pelvis/torso vertical motion — the body
    // slides, no weight shift"). The height request of the step in force gains a
    // phase-shaped lift: 0 at the swap that started the step, -poseBobAmp metres
    // at the step's own halfway (mid-stance), 0 again at the next swap. 0 = off:
    // the landed request stands bit for bit.
    private float bobAmpMeters = 0f;
    private float placementMarginMeters = 0.04f;
    private float comLeadSwapMeters = 0.16f;
    // WALK CYCLE 75/76: THE DESCENT ARITHMETIC, READ BY THE BIAS (the c68 §3 fix; see
    // PoseLibrary.Sample). OFF (0, the default) = the landed drawn-knee bias
    // expression bit for bit; ON (1) = the bias aims the ankle with the knee the
    // descent leaves.
    private bool placeKneeLive = false;
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

    // WALK CYCLE 37: THE SWING FOOT'S OWN ANKLE (the manager's named follow-up;
    // ONE writer: `t.swingLeg.ankle` in PoseLibrary.Sample - the plate's own roll,
    // the last joint of the planted sole's own chain). Cycle 36 priced the thigh
    // (spent), cycle 31/32 the knee (re-times or regresses), cycle 33 the key
    // (bound-capped); the ankle is the joint that had not been asked. THE LANDING
    // (the round's own reading): `-poseAnkleCarry -10` with cycle 35's hold 0.010
    // reads the delivered lead -0.1635 (base -0.2238, +0.0603) AND the fall 8.520
    // (base 8.475, +9 frames), the census dead and the package byte-kept.
    // The same tail gate confines it (first open 8.150 on the landed row), the
    // same ramp clocks it, and the rig's own ankle limit clamps it through
    // ClampLeg. 0 = OFF: the emitted ankle is the landed value bit for bit.
    private float swingAnkleDeg = 0f;
    private float swingAnkleRampSeconds = 0.045f;
    private float swingAnkleRampT;   // the gate's own open clock, per step (s)

    // WALK CYCLE 71: THE PUSH-OFF'S OWN DELIVERY (the sole's own roll onto the
    // toe - c67 §8 residue item 1, pushed off / heel-to-toe). The stance role's
    // drawn tail already ASKS the roll (launch +14 / footplace +8 / plantload +16
    // at the ankle keys) and the record's own delivered never answers it: over
    // the step's last 0.30 s the ask means +5.9 and the delivered means -10.8
    // (the deep point: ask +16 against -18..-20 at the step's close; the foot
    // lifts at -5 with its sole still flat). The miss grows with the load, i.e.
    // the DELIVERY is the limit - cycle 28's shape, answered the same way: in
    // the flick on the loaded roll window (cycle 73: a ~0.11 s pulse at
    // cross+0.145..+0.255, the support leg down) the stance ANKLE's P demand is
    // multiplied toward this value. The ASK, the knee, the toes and the hold's own path are
    // untouched. 1 = OFF: the landed expression bit for bit.
    private float pushDeliverP = 1f;
    // The tail gate's own need bound (cycle 34's own: 1 is the lead the swap rule
    // calls a swap; the load bound is cycle 28's own SupportHoldLoadN).
    private const float SwingPlaceNeedMin = 0.9f;
    private float standCaptureSeconds = 4.6f;
    private float standTiltLimitDeg = 8f;
    private float fallenDropMeters = 0.40f;
    private float fallenTiltDeg = 50f;
    private float armSwingDeg = 22f;

    // ═══ WALK CYCLE 93 (rig part 2 - the feet): THE STAND'S FLAT-SOLE TOE HOLD ═══
    // The owner's defect: at rest the soles sit at an angle to the support and jitter
    // ("почему ступни под углом? ... они должны быть параллельно опоре, а они дергаются
    // из-за того что под углом к полу"). Measured on the c92 player: the toes creep UP
    // 21-27 deg off the plate, so the foot rests on the ridge at the MTP - the heel's
    // front corner plus the toe's back corner, ~3 mm apart - and the whole stand rocks
    // on it (the plate's hf span 5.2-7.6 deg, the sole unloading 2.4-2.6 % of frames).
    // Mechanism: the MTP is a LIGHT hinge and the stable-PD denominator (1 + K*dt/I)
    // starves the walk-tuned toe gain (0.8) on it, so the ground's lift under the toe's
    // back corner (moment arm ~1.4 cm) out-torques that hold. The stand's toe therefore
    // gets its own authority - and only the STAND's: the walk's own DriveToes call keeps
    // the base gain (the gait is untouched). Armed only while the soles genuinely rest
    // flat (both feet grounded, both plates within standToeFlatArmDeg of level; the
    // split stance's pitched back foot stays on the base law by construction).
    // scale <= 1 = off: the pre-93 law bit for bit.
    private float standToeHoldScale = 8f;         // toe-gain multiplier at arm (1 = off)
    private float standToeHoldBiasDeg = 2f;       // +deg = toe down (the rig's own sign)
    private float standToeFlatArmDeg = 5f;        // arm at both plates within this of level
    private float standToeFlatDropDeg = 8f;       // disarm above this (hysteresis)
    private float standToeFlatHoldSeconds = 0.2f; // dwell before either switch
    private bool standToeFlatArmed;
    private float standToeFlatAge;
    // WALK CYCLE 94: THE SQUAT GATE. The hold is a STAND device, and the landed
    // squat law regresses under it unguarded (c93g_land_on: reps 2, Fallen 19.02):
    // the fold runs ON the stand path, its plates are flat there too, so the armor
    // fired through the whole squat and the foot's own behavior moved under the
    // machine. Worse measured: the hold's own POSTURE at t = 0 moves the descent
    // itself (comX +13 mm/s against the anchor's +4; c94h_land: reps 0, Fallen 9.81),
    // so a take that squats blocks the hold for its WHOLE run (MotionIntent.toeHoldBlock,
    // written by the trial), and the machine's window (intent.squatReps > 0) is blocked
    // too on any path that still arms the hold there. Disarm is immediate - the fold
    // takes its foot at once, no dwell out. 0 = the c93 behavior bit for bit.
    private float standToeSquatGate = 1f;

    // ═══ WALK CYCLE 40: THE GET-UP AND THE HAND GRIP (Yury's word, 22.09) ═══
    // `-poseGetUp 1` - the get-up family (while the body is Fallen and the walk
    //   key is held, the body drives a rise; see TickGetUp). DEFAULT 0: unset,
    //   the Fallen branch is the landed block verbatim and every pre-existing
    //   command line reproduces its row bit for bit.
    // `-poseHandGrip 1` - the grip family: while a palm presses the slab during
    //   the get-up, that palm's own collider carries the grip material (the
    //   dog's claw pattern, Dog.cs SetClawGrip) with `-poseHandGripMu` as its
    //   friction scale. DEFAULT 0.
    // `-poseHandGripHold <s>` - how long a grip survives after its press ends
    //   (the release clock; the engage itself is the palm's measured load).
    private bool getUpEnabled;
    private bool handGripEnabled;
    private float handGripHoldSeconds = 0.20f;
    // The card's own tilt bar, deg, of the recorder's torsoTilt sign (minus =
    // forward): "torso tilt above -20 deg". Published to BalanceController so
    // the JSON's getUpStandTiltDeg names the bar this law actually used.
    private float getUpStandTiltDeg = -20f;
    // ROUND 14 (the tilt stand's practice): the card's 10 s window is the FLAT
    // slab's budget; on the rocker the palms find the surface a whole attempt
    // late (rise15L: attempt 3 at t 32.2 s), so the ladder's bounds and its
    // progress ramps scale by this. 1.0 = the card's own numbers verbatim
    // (x * 1f is exact), and every row without -poseGetUpPatience keeps its bytes.
    private float getUpPatience = 1f;

    // ═══ WALK CYCLE 45: THE FALL GUARD (the falling state catches itself) ═══
    // Yury's word (22.09): "Если человек падает, это тоже состояние, состояние
    // падения. Он должен падать на руки, а не головой об землю. Он должен
    // выставлять руки и пытаться погасить свое падение... контроль состояния,
    // если ловит состояние падения, принятия рук правильного положения."
    //
    // The FALL is a state of its own and the body must ACT in it. The trigger and
    // the clock are the controller's own geometric ladder
    // (BalanceController.GeometricFallState, BodySupportGeometry 0..3) PLUS the
    // body's own descent rate (walk cycle 51, R1): the pair "the ladder reads
    // Critical or worse" AND "the pelvis is falling at 0.20 m/s or faster" must
    // hold together, and the guard is over when the body is down (IsDown, the
    // landed block's own threshold) or when the pair lets go. No timer, no key,
    // no new signal. The rate term exists because the ladder ALONE blips on the
    // walk's own balance bumps (cycle 45 measured a 0.41 s run two seconds before
    // the fall), while a body that is really going over is a body that is really
    // descending - the walk's own bumps carry +-0.15 m/s and cannot qualify.
    //
    // What it does, and nothing else: it publishes its plant reach to ArmDrive
    // (the ONE writer of the six arm muscles, execution order 10 after this
    // component) and asks it for the press every step. ArmDrive's plant machinery
    // - the same hardware the get-up round exercised - then latches its palm point
    // ON THE SLAB, fallGuardReachMeters ahead of the shoulder on the body's own
    // facing, instead of at the sagged posture the body happened to land in, and
    // HOLDS that point while the body folds over the planted hand
    // (ArmDrive.ChoosePlantPoint -> GuardPlantPoint, with the stroke suppressed
    // while the guard is live, so the elbow GIVES instead of pressing the body
    // back up - the absorb). The walk's own legs, trunk and bars are untouched:
    // the guard writes no joint, and the switch is OFF by default, so every
    // pre-existing command line (the row of record) is the same bytes.
    //
    // THE ONE NUMBER that decides when it is live (walk cycle 51, R1): the PAIR
    // must hold for fallGuardHoldSeconds CONSECUTIVELY - the ladder reads >=
    // Critical AND the pelvis is descending at FallGuardDescendMetersPerSecond or
    // faster. The ladder alone blips (cycle 45: seven runs in the walk's last 3 s,
    // two of them seconds before the fall), so it is not the state saying
    // anything; the pair is (measured on the row of record: the walk's own bumps
    // carry +-0.15 m/s against the -0.20 threshold, and the fall crosses the pair
    // about 8.50 s - the window cycle 45's 0.45-0.60 holds had to buy with the
    // walk's own bytes). The hold is a persistence test ON the pair, not a clock
    // started by it.
    private bool fallGuardEnabled;
    private bool fallGuardLive;
    private float fallGuardStateAge;
    private float fallGuardFacing = 1f;
    // The descent term of the trigger pair, m/s (walk cycle 51, R1): the pelvis'
    // own vertical rate at or below which "the body is going over" is the body
    // DESCENDING. The walk's own bumps sit inside +-0.15 m/s.
    private const float FallGuardDescendMetersPerSecond = -0.20f;
    // How long the PAIR must hold before the guard may arm, s
    // (-poseFallGuardHold).
    private float fallGuardHoldSeconds = 0.10f;
    // The reach drive, m (-poseFallGuardReach): how far AHEAD of the shoulder the
    // palms' latched slab point sits. 0 = straight under the shoulder (the landed
    // press posture); the rig's own arm chord is linkL1 + linkL2 = 0.67 m, so this
    // is the whole difference between "the arms come out in front" and "the arms
    // hold the body up from below".
    private float fallGuardReachMeters = 0.20f;
    // CYCLE 100 (the owner's hand-plant addendum): the get-up's own plant reach,
    // metres, 0 = off. While the get-up asks the press the plant point is taken
    // this far AHEAD of the shoulder (the push-up stance) instead of the landed
    // rule's nearest-reachable fold under it - that fold is what visually plants
    // the hands at the navel. Signed by the fall's facing inside TickGetUp.
    // CYCLE 101 (LANDED re35): the shipped default - the owner's band edge
    // (0.2-0.35 m ahead of the shoulder); `-poseGetUpReach 0` restores the
    // pre-101 rule.
    private float getUpPlantReachMeters = 0.35f;
    // The press request while the guard is live (ArmDrive clamps 0..1): the get-up
    // Rise rung's own level, the strongest the hardware has.
    private const float FallGuardPressLevel = 1f;

    // ─── bound body ────────────────────────────────────────────────────────
    private bool bound;
    private Human human;
    private BalanceController balance;
    private VestibularSystem vestibular;
    private BodyStateEstimator state;
    private CenterOfMassCalculator comCalculator;
    private MotionIntent intent;
    // WALK CYCLE 40: the get-up's two collaborators (see Initialize).
    private ArmDrive armDrive;
    private StepPhaseDriver stepDriver;
    private Rigidbody2D pelvisBody;

    private readonly ActuatorMixer mixer = new ActuatorMixer();

    private HingeJoint2D leftHipJoint, rightHipJoint;
    private HingeJoint2D leftKneeJoint, rightKneeJoint;
    // WALK/HUMAN CYCLE 82: the forward lean's own level (its slow slew; exactly 0f on
    // every frame that never leans - the stand's bit-for-bit door).
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

    // ─── the walk's own neck hold (WALK CYCLE 60) ─────────────────────────
    // The stable-PD denominator's inertia (ActuatorMixer.EffectiveInertia: the
    // harmonic mean of the joint's two bodies' OWN rotations) ignores the chain the
    // joint carries. At the neck that is the head: m 4.86 kg at 0.205 m above the
    // pivot = 0.204 kg.m2 against the 0.09 m segment's own 0.0016, so the divisor
    // lands ~6.4x too large and the walk's hold stiffness ~8 N.m/rad against the
    // head's own gravity 9.8 N.m/rad - the joint rides its +-30 stops (measured on
    // the row of record before this change: neck world tilt span 63.0 deg, 16.2 %
    // of the walk window at a stop, 21 hold segments, worst 0.26 s). The walk's
    // hold divides the signal by the LOAD share instead of the segment's share:
    // walkNeckHoldShare blends the two (0 = the old denominator byte for byte,
    // 1 = the carried chain counted). Walk-gated at the call site, so the stand
    // regime and the stance row are untouched by construction.
    private float walkNeckHoldShare = 0.28f;
    private float neckLoadInertia, headLoadInertia;

    // ─── quiet ankle loop ──────────────────────────────────────────────────
    // The law is the standing controller's, unchanged: command = P*(offset/ref)
    // + D*(speed/ref) with offsets in metres, so a 7 mm CoM offset already asks
    // for the whole ankle. The two factors scale that pair from the CLI.
    private float ankleBalanceP = 1.0f;
    private float ankleBalanceD = 1.0f;

    // ─── the support-side shift (kick cycle k2, 25.09.2026) ────────────────
    // THE MISSING LEVER, NAMED BY THE k1 REPORT: the pose path's stand regulates
    // the CoM with the ankle CoP loop alone and holds the pelvis at world
    // vertical (DriveHips target 0), while the level-1 stand leans the pelvis
    // TARGET toward the CoM with hipBalanceGain - the term the one-leg stand
    // dumps backward without. The strike's CoM ratchet (kickk1_m3: +0.04 m in
    // the first stroke, then +0.09, +0.14) walks the body past what the ankle
    // loop can recover, so while the kick's own shift level is up this stand
    // adds the level-1's term to its pelvis target:
    //     pelvisTargetTilt -= kickShiftGainDeg * kickSignal * balance.kickShiftLevel
    // with kickSignal the level-1's OWN balance signal, verbatim (UpdateState:
    // clamp(comProportionalGain * CoM-offset/comOffsetReference +
    // comDerivativeGain * bodyRate/tiltSpeedReference, +-maxBalanceSignal)), the
    // CoM offset read here the way the ankle loop reads it (comX minus
    // BodyStateEstimator.supportCenterX) and the body rate from the vestibular
    // frame. Both terms are exactly 0f when the level is 0 (the stand fence is
    // the proof), and the gain is signed so a run can read the law in both
    // directions.
    // 0 = the offset is off; the k1 envelope's own row is the control.
    // k4 (26.09.2026): the default is 0 - the term is measured NEGATIVE on both
    // of this line's rows (the single stroke falls at 7.265 s with +-8, and holds
    // 40.0049 s with 0; the k3 record's own repeat is 12.285 s at 0 against
    // 11.855 s at 16). The flag restores the k2 arithmetic, bit for bit.
    private float kickShiftGainDeg;
    // ─── THE WIND-UP (kick cycle k4, 26.09.2026) ────────────────────────────
    // THE STANCE THE STRIKE FIRES FROM. The k3 record's own residue: the CoM
    // travel was never taken by the support channel, and the single stroke's
    // own read (kickk4) shows it as a dive: 0.5 s after the kicking foot comes
    // back down the CoM runs 0.42 m forward with the CoP stuck (comX 0.17 ->
    // 0.59 m, comOffset 0.09 -> 0.42, both hips to their +45 stop, both ankle
    // extensors railing at -65 N*m). The lever is the STANCE, and it is a
    // STATIC offset on the CoM value this stand's ankle loop regulates with:
    // while the strike's level is up the loop reads the CoM kickWindComMeters
    // FURTHER AHEAD than it is, so its own CoP command brakes the body from the
    // stroke's first frame - the CoM eased back over the support foot, which is
    // the margin the transfer needs. The k2 term's shape was signal-scaled and
    // the signal is ~0 exactly in the settled stance that matters; this one is
    // a constant and is therefore alive at the fire, not at the stumble.
    // Sign: the same convention as comXAnkle (a larger value = the loop reads
    // the CoM further forward = a stronger backward command).
    // MEASURED, k4's own ladder: 0 -> 40.0049 s; +0.01 -> 5.830 s, +0.02/+0.04
    // -> 5.820 s, +0.08 -> 5.705 s (a BACKWARD topple: comX -1.16 m, tilt
    // 102 deg); -0.01 -> 7.105 s, -0.02 -> 7.185 s (the k2 shape's own forward
    // dive). The reason is the loop's own scale: ankleComP 14 over
    // comOffsetReference 0.10 m = 140 per metre, so ONE centimetre is a
    // full-scale command and the loop is already railed - the offset's dose is
    // a 0-or-fatal switch, both signs. DEFAULT 0: the knob is kept for the
    // round that re-enters the wind-up as a gain/band, not a constant.
    private float kickWindComMeters;
    // ─── k6 THE WIND-UP'S TERM 1, AS A BOUNDED GAIN (26.09.2026) ───────────
    // THE k5 REPORT'S OWN SECTION 7, and the round that re-enters the wind-up
    // as a gain, not a constant. The k4 record above is why: at 140 per metre a
    // static target shift is a switch, because the loop lives at ~0.7 of full
    // scale (±5 mm of quiet comOffset) and a railed loop is a bang-bang
    // actuator that topples the body.
    // THE RECORD'S OWN MECHANISM, read per second on kickk5q_r5R: the first
    // stroke leaves the stand settled on its KICKING side and it stays there -
    // fzL/fzR 20/677 N with comX 0.115 m flat for six seconds, because the
    // loop's own reference (the support hull's centre) MOVED +X with the
    // kicking foot that landed forward (the k3 record's 6.7 cm the stand never
    // gives back). The next stroke therefore lifts the LOADED leg - the k4
    // load gate defers its whole 1.5 s and fires on the timeout - and the
    // transfer that follows is what rails the support ankle at -65 N*m for
    // ~2 s with the CoM 1.2 m out.
    // THE TERM: while the episode's level is up AND no stroke is in flight (the
    // INTER-STROKE WINDOW - the stretch the stand has to give the weight back
    // in) the ankle loop stops regulating toward the support hull's centre and
    // regulates toward the STANDING foot's own centre - the point it already
    // regulates toward whenever the kicking foot is off the floor - and BOTH of
    // its CoM channels are re-scaled by kickWindGain (0..1). The gain is what
    // makes the term BOUNDED: at the record's own offset (the settled 12 cm,
    // comX 0.115 against the standing foot's own centre) a dose of 0.01 puts
    // the channel at 0.175 of full scale = ~11 N*m of the 65 N*m ceiling, well
    // inside the ~0.3-of-full-scale headroom the k5 report sized, and the
    // target's own linear range is widened by the same 1/0.01 factor (0.7 m of
    // offset would be needed to rail it - no stand reaches that). Target shift
    // and gain-bounding are ONE term: the shift alone is the k4 switch, the
    // gain alone cannot move a reference.
    // DEFAULT 0: the live level stays exactly 0f on every row that does not
    // pass the flag, DriveAnkleBalance takes its pre-k6 expression character
    // for character and the landed rows (the stance and walk fences, the k5
    // take) are reproduced bit for bit (R2).
    private float kickWindGain;
    // The term's own live values, written by the stand path every step: the
    // gain x the shift's level while the inter-stroke window is up (0f
    // otherwise, and then nothing below reads either field), and the standing
    // foot's own plate centre in metres - the loop's reference while it is
    // live.
    private float kickWindLevel;
    private float kickWindRefX;
    // ─── k7 THE TERM'S OWN REFERENCE, LATCHED (26.09.2026) ─────────────────
    // THE k6 REPORT'S OWN SECTION 8, sized by the k6 landing row's own read
    // (kickk6p_w0.01, per step): the term's reference is the standing foot's
    // LIVE plate centre and the burst DRAGS that foot forward - the window
    // 3 -> 4 slides the standing foot's rear edge +0.03 m while the CoM creeps
    // +0.075 m (comX 0.021 -> 0.096, the term's own target with it), and the
    // 4th stroke then fires from that leaning stance and vaults the whole stand
    // +0.58 m (comVelX 0.635 m/s, the extensor railed at -65 N*m, the stand
    // landing split 1.08 m wide with the ankles pinned on the flexor side).
    // The live read is a POSITIVE FEEDBACK: the foot moves with the body and the
    // target moves with the foot. The latch freezes the point the term regulates
    // toward - "the stance the burst fires from", the plant's own
    // kickSupportHipDeg pattern read on the loop's reference instead of the hip.
    //   0 = LIVE, the k6 delivery bit for bit (the flag's default);
    //   1 = re-latched at every inter-stroke window's rising edge (per stroke,
    //       the plant's own pattern - the latch re-arms whenever the episode or
    //       a stroke takes the window down);
    //   2 = latched ONCE at the episode's first rising edge and carried for the
    //       whole burst, let go only when the level falls to 0.
    // The RELEASE is untouched: the window's hand-over to the hull centre still
    // happens when the level reaches 0, and the round reads it ($8's second
    // question) - at the k6 landing row's operating point the loop is already
    // saturated on both sides of it, so the hand-over's own step (|the reference
    // in force - the hull centre|) lands on a railed actuator.
    private float kickWindLatch;
    // The latch's own state: the point latched, whether the window was up on the
    // previous step (the rising edge), and whether the burst's own frame is held.
    private float kickWindRefLatched;
    private bool kickWindWindowUp;
    private bool kickWindFrameHeld;
    // ─── k7 THE RELEASE'S OWN HOLD (26.09.2026) ────────────────────────────
    // $8's SECOND QUESTION: "whether the release (the level easing to 0 at the
    // request's end) should hand over at the hull centre or at the standing
    // foot". The k6 delivery hands over at the moment the level reaches 0: the
    // loop's reference steps from the live standing foot's centre back to the
    // support hull's centre (measured on the k6 landing row: about 0.29 m of
    // step = 0.4 of the loop's own full scale at 140 per metre). With
    // -kickWindHold <s> the term is kept ALIVE for <s> seconds after the level
    // has reached 0 - the hand-over happens after the stand's own settle, not on
    // the level's last frame. The clock is loaded while the request is up and
    // runs only once the level is 0; with 0 (the default) the branch is dead and
    // the release is the k6 delivery bit for bit.
    private float kickWindHold;
    private float kickWindHoldClock;
    // ─── k9 THE TERM'S OWN REFERENCE (26.09.2026) ────────────────────────────
    // THE k8 REPORT'S OWN SECTION 9, item 1: the creep through the inter-stroke
    // windows (the 4th head firing from comX 0.096 m while strokes 1..3 fire from
    // 0.014..0.022, and the window 3 -> 4 alone sliding +0.028 m) is NOT owned
    // yet, and the round that found it refused to guess its writer. Its three
    // candidates all read on the row; THIS knob separates the first from the
    // other two. While the window is up the term's reference is
    //   0 = the STANDING FOOT's plate centre (the k6/k7 delivery, bit for bit);
    //   1 = the SUPPORT HULL's own centre - state.supportCenterX, the loop's own
    //       default reference, i.e. the point this loop regulates toward whenever
    //       the term is not running.
    // With 1 the term keeps its bounded gain but MIGRATES NOTHING: the reference
    // no longer moves with the standing foot, and the release's own hand-over
    // step (|the reference in force - the hull centre|, the k7 section-3 read's
    // 0.30 m) is 0 by construction. With 0 the binding point is the foot.
    // THE ROW'S OWN READ, both candidates, per window (kickk8b_r5Rw1s1): the CoM
    // sits +0.001 -> +0.013 -> +0.084 -> +0.429 m AHEAD of the standing foot's
    // plate centre (a growing positive offset = a growing BACKWARD command) while
    // it sits -0.070 -> -0.069 -> -0.061 -> -0.067 m BEHIND the hull's centre
    // (negative and flat). Same creep, opposite sign: the two candidates are the
    // round's two switch positions, not two spellings of one point.
    // The knob is read wherever the term picks its point: the live read, the k7
    // latches and the k7 hold's own branch. DEFAULT 0 leaves every landed row
    // (the stance and walk fences, the k5..k8 takes) character for character.
    private float kickWindRef;
    // ─── k11 THE TERM'S OWN NORMALIZER OVER TIME (26.09.2026) ────────────────
    // THE k10 REPORT'S OWN SECTION 9, item 1: the dose is a CLIFF (0.01 holds
    // 40.0049 s; 0.03 dies at 12.805 s; 0.10 at 18.23 s) because the term's raw
    // channel is 140 x err and the windows' own carried error spans 0.145 ->
    // 60.05 of full scale (the k10 read): a constant gain cannot serve windows
    // whose raw error spans two orders of magnitude - any dose that ACTS on the
    // creep rails the command in the stroke's own transfer transient, and any
    // dose that stays inside the loop's linear range is invisible where the
    // parking happens (W1/W2 at 0.0015/0.0185, W4 at 0.831 of full scale).
    // THE SHAPE: the term's P at THE ERROR THE WINDOW ITSELF CARRIES - latched
    // at the window's rising edge (the k7 latch-1 pattern's own clock) - is the
    // DOSE, a bounded number, and the level that delivers it is what the window
    // is scaled by:
    //     carry = clamp(|comXAnkle - refX| at the rising edge, FLOOR, CEIL)
    //     level = clamp01(kickWindNorm / (140/m * carry))
    // so P = 140 * err * level = kickWindNorm * (err / carry): the term reads
    // 'how many times ITS OWN window's carried error has grown', never 'how
    // many times full scale' - the rail is unreachable at the carry and the
    // term acts in W3/W4 (where the creep parks the stand) without railing in
    // W2. The k6 shape is untouched: pScale = the level, dScale = its square
    // root, and a bigger level therefore carries proportionally more damper.
    // The carry's floor is the smallest error the stand is ever measured to
    // carry (2 cm = 2.8x full scale); its ceiling is the record's own worst
    // window (+0.43 m).
    // 0 (DEFAULT) = OFF: the level stays the k6 expression character for
    // character and every landed row is reproduced bit for bit (R2).
    private float kickWindNorm;
    // The normalizer's own live state: the carry latched at the window's rising
    // edge and the level that carry buys (both written only while the window is
    // up; read by the level and by the k7 hold's own branch).
    private float kickWindCarry;
    private float kickWindNormLevel;
    private const float KickWindCarryFloor = 0.02f;
    private const float KickWindCarryCeil = 0.50f;
    // ─── k14 THE WINDOW'S OWN LEVEL WHILE A FIRE IS DUE AND HELD (26.09.2026) ─
    // THE k13 REPORT'S OWN SECTION 7: the gate's own rung axis is closed (the arm
    // makes any rung armable and the ladder holds across it) and what remained is
    // WHAT THE GATE DOES WHILE IT WAITS. Today the wait is the SAME state as the
    // inter-stroke stretch - the k6 window's own level, the request up and no
    // stroke in flight - and the round's own read prices the two apart: on the
    // k13 landing row the stretch rails the support ankle (-65 N*m over
    // 15.5..16.2, the transfer doing its work) while the wait sits at the level's
    // own 1 % (the loop's demand ~8..12 N*m, the CoM creeping +0.02..+0.04 m/s) -
    // the stand a waiting gate watches is the nearly free pendulum.
    // THE KNOB: the level in force while a fire is DUE AND HELD
    // (balance.kickFireHeld: the pace clock has armed a stroke and the gate is
    // still deferring it) in place of the window's own level.
    //   -1 (the flag's default) = OFF, the level stays the k6/k11 expression
    //      character for character (every landed row of the lane, bit for bit);
    //   0  = the term's channels are scaled to nothing (DriveAnkleBalance takes its
    //      pre-k6 law: the loop's own full authority toward the hull centre);
    //   1  = the loop's own full authority toward the term's own reference (the
    //      same expression at the level's ceiling).
    // The two ends are the measured switch positions; the middle is the ladder.
    private float kickWindDue = -1f;
    // ─── k15 THE STROKE'S OWN HEAD (26.09.2026) ─────────────────────────────
    // THE k14 REPORT'S OWN SECTION 8, item 1, priced by the k15 round's own read
    // of the two left rows: at a repeat's 2nd fire the STROKE'S LIFT is the
    // hole. The stand the fire finds is a parked split stance whose kicking foot
    // is the FRONT foot, so the CoM sits behind the hull's centre (comOff
    // -0.064) and the loop's steady command is pinned on the flexor side (140 x
    // -0.064, railed) - a forward push - while the limb unloads. The kicking
    // foot leaves the floor 90 ms after the fire, the hull collapses to the
    // standing foot (its front edge 0.067 m, the CoM +0.03), the CoM crosses the
    // edge 0.25 s after the fire at +0.36 m/s, the catch machinery takes over
    // (ctrlFallState 1 at +0.13 s, 2 at +0.25 s) and the stroke rides the
    // support ankle's extensor wall for 154 of 154 swing frames (chamber 32/32,
    // extend 32/32, return 90/90) - the stand dies 1.4..1.6 s later.
    // THE KNOB: the term's own reference AND level in force while the stroke's
    // HEAD is live (balance.kickHeadActive, the lift's unload window), i.e. the
    // k14 knob's sibling on the other half of the episode: the loop reads the
    // CoM against the STANDING foot's plate centre at this level instead of the
    // hull's centre at full scale, so the stand's own action during the unload
    // is the hand-over the stroke needs (the CoM is AHEAD of the standing foot:
    // the error is +0.09 m, the command lands on the extensor side) rather than
    // a railed forward push.
    //   -1 (the flag's default) = OFF, the k6/k11/k14 expression character for
    //      character (every landed row of the lane, bit for bit);
    //   0  = the unload window carries the pre-k6 law (the loop's own full
    //      authority toward the standing foot's centre);
    //   0..1 = the bounded level, the ladder's own rungs.
    private float kickWindHead = -1f;
    // THE SUPPORT'S OWN HOLD (the walk's own discipline, handed to the strike):
    // while a stroke is in flight the SUPPORT leg's knee is driven with
    // PoseLibrary.StanceHoldDeliverP (7x) - "the same error asks 7x the torque on
    // the support's knee while the hold is live" is the walk's own sentence for
    // the same problem, and the strike's one-leg second is that problem. 0 = off
    // (the k1 delivery); 1 = on.
    private float kickSupportHold = 1f;
    // THE MASS LEVER: the trunk (the body's mass carrier, ~40% of it) leans with
    // the same signal and the same level - the crouch/lean layers offset the
    // trunk target beside the pelvis for exactly this reason (the pelvis alone
    // carries little mass). Signed in the rig's own convention: a POSITIVE value
    // leans the chest BACK as the CoM goes forward (forward = minus on the
    // target), a negative one with it. 0 = off.
    private float kickShiftTorsoDeg;
    // The CoM-loop's own target offset, m: the point the ankle loop regulates
    // toward moves with the same signal (the lean layer's leanComShift pattern,
    // its own sign). 0 = off.
    private float kickShiftComMeters;
    // ─── THE SUPPORT'S PLANT (kick cycle k3, 25.09.2026) ───────────────────
    // THE k2 RECORD'S OWN LEVER. The k2 close left the repeat open with one
    // sentence: the CoM travel was never taken by the support channel. The row
    // says why: while a strike is in flight the body is on ONE leg, and that
    // leg's hip is under the free pelvis law alone - it ran to its +45 stop
    // while the support ankle railed at -65 N*m, the pelvis pitched forward and
    // the CoM walked off (kickk2_h16s: fzL 184 -> 1502 N in 0.2 s, comX
    // 0.110 -> 0.522 m in 0.8 s, pelvisTilt -4.6 -> -17.4 deg). The strike
    // LATCHES the support hip's angle on its fire step (LegStrikeDrive ->
    // balance.kickSupportHipDeg); this stand holds the hip inside the ordinary
    // hipHoldBand around that angle for the whole episode (the stroke and the
    // shift's own release), through the very MixHipHold the quiet stance uses -
    // no gain is retuned and no new writer is added. The ankle stays the CoP
    // loop's (the always-on layer keeps its authority) and the KICKING side is
    // untouched: 0 = the k2 delivery, bit for bit.
    private float kickPlantHold = 1f;
    // ─── k19 THE PLANT'S OWN BAND (27.09.2026) ──────────────────────────────
    // THE k18 REPORT'S OWN SECTION 8 (the lever it named for the next round):
    // the line's single open writer is the state each fire finds, and the k19
    // round's own price of the inter-stroke window (c-kickk19-price.py /
    // c-kickk19-roles.py; rows kickk18_r5La001v01 / r5Ll012 / r5Rg0) reads the
    // parked stance's own excess over its latched plant angle: the left rows
    // park the support hip at +12.6 / +8.7 against the latch +4.9 (excess
    // +7.75 / +3.94 deg) while the delivery's parks it on the FLEXOR side
    // (|delta| <= 5.3) - and the ordinary hipHoldBand (20) is silent on all
    // three, so the plant's own hold writes nothing in the window whose state
    // the next fire reads. The hold's law is untouched (k3): this is the BAND
    // it reads, in degrees, while the strike's plant is on. -1 (default) =
    // off, the landed arithmetic bit for bit (balance.hipHoldBand rules); 0 =
    // the hold retired (MixHipHold's own band guard); >= 0.5 = the band itself.
    private float kickPlantBand = -1f;
    // ─── k16 THE PARKED STAND'S OWN MARGIN (26.09.2026) ─────────────────────
    // THE k15 REPORT'S OWN SECTION 7 (the lever it named for the next round):
    // at the repeat's 2nd fire the stand is parked with the CoM only ~4 cm from
    // the standing foot's plate front edge, while the delivery's own parked
    // stand (whose windows HOLD) parks with 7..13 cm of that margin. The k16
    // round's own price of the k14/k15 rows (the two left rows and the delivery,
    // tool c-kickk16-price.py) reads where the difference sits: the term's own
    // error at the fire (comX - the standing foot's plate centre) is +0.0908 m
    // on the left's fatal 2nd fire against +0.0034..+0.0192 on the delivery's
    // holding fires - the parked stand sits 9 cm AHEAD of the point the window
    // aims at, so the window's whisper (its command 0.127 of full scale, the
    // ankle at -7.9 N*m of the -65 wall) is what the row dies under.
    // THE KNOB: the window's own target moves AWAY FROM THE EDGE - the reference
    // (the standing foot's plate centre, or the hull centre under -kickWindRef)
    // shifts BACKWARD along the travel axis by this many metres while the
    // inter-stroke window is up and only there (the head's own branch, the k7
    // release hold and every landed path untouched):
    //     err = comX - (reference - kickWindMargin)
    // so the same bounded gain reads a deeper error and the loop's own brake
    // grows (a POSITIVE error drives the extensor side - see DriveAnkleBalance);
    // sized by the k15 price's own gap (the left's ~4 cm against the delivery's
    // ~8 cm, i.e. ~0.4 x comOffsetReference 0.10 m).
    // BOUNDED: clamped to [0, 0.10] m (0.10 = the loop's own comOffsetReference;
    // the offset is a correction of where the stand parks, never a re-aim of the
    // whole loop). 0 (the default) = OFF - the guard below is false and the k6
    // arithmetic runs character for character (every landed row, bit for bit).
    private float kickWindMargin;

    // ─── THE HEAD'S OWN AUTHORITY (kick cycle k17, 26.09.2026) ──────────────
    // k15 closed the head's REFERENCE (the standing-foot swap kills the landed
    // delivery at every level); the head's own LEVEL with the reference UNCHANGED
    // was never read. The k17 price (tool c-kickk17-price.py, per stroke and per
    // row, before any knob) reads it: the pre-k6 command at the head is RAILED on
    // the FLEXOR side - the two-foot hull's centre sits 6.4..7.9 cm AHEAD of the
    // CoM while the kicking foot is still planted (the k16 section 6 read), so
    // offsetN = (comX - centre)/comOffsetReference reads -0.6..-0.8 and ankleComP
    // 14 puts the command past -1 - and that push is what buys the head's own CoM
    // gain: +0.1793 m/s of the left's fatal head's +0.358 m/s is bought on its
    // 17 flexor-rail frames of 48 (the delivery's own headed strokes: +0.0853..
    // +0.1227 of +0.146..+0.329, 14/48 frames, and they hold).
    // THE KNOB: a bounded LEVEL on the head's own pre-k6 command - ONE-SIDED, the
    // FLEXOR side (the push) only, the extensor side (the brake) untouched, while
    // balance.kickHeadActive is up (the stroke's lift, the unload window: the same
    // flag the k15 read uses). The command is the pre-k17 expression and then
    // bounded: command = Max(command, -kickHeadPush). The value is the flexor's
    // share of full scale (0.5 = 11 N*m of the muscle's own 22 N*m rail; 0 = the
    // brake side only - no push at the head at all).
    //   -1 (the flag's default) = OFF: the guard is false and the command is the
    //      pre-k17 expression character for character (every landed row of the
    //      lane, bit for bit - R2);
    //   0..1 = the bounded level, the ladder's own rungs.
    private float kickHeadPush = -1f;

    // ─── k24 THE STROKE'S OWN FLIGHT (27.09.2026) ───────────────────────────
    // THE k23 REPORT'S OWN SECTION 5 (its named next, taken by this round): the
    // flight - the stroke's one-legged second (its phases 2..4, 0.24..1.01 s
    // after the fire) - is the episode's LAST stretch with no term of its own.
    // The k14 knob covers the WAIT (the gate's deferral), the k15/k17 knobs the
    // HEAD (the lift's unload window, 0.24 s); for the rest of the stroke
    // balance.kickStrokeActive is up, the inter-stroke window is down, and the
    // loop runs the pre-k6 law: full authority toward state.supportCenterX.
    // WHY THAT IS NOT THE SAME LAW, measured by this round's own price
    // (Trials/Runs/kick-k24-price.txt, per stroke and per row): while the
    // kicking foot is AIRBORNE the grounded hull IS the standing foot, so the
    // pre-k6 reference coincides with the term's own - but the flight ENDS with
    // the kicking foot's landing (the return's own ~0.85 s mark) and from that
    // frame the support hull is TWO plates again. The freshly-landed front foot
    // drags the hull's centre far ahead of the CoM (the record row's stroke 2:
    // the loaded hull -0.065..+1.019 against comX +0.434), the loop's error
    // flips NEGATIVE, and the pre-k6 law finishes the throw by pushing the body
    // forward (the +22 flexor wall) while the CoM is still running +0.5 m/s.
    // The standing foot's plate centre does not move: the same comX reads about
    // +0.37 m of error there - an extensor brake.
    // THE KNOB: the term's own reference (the standing foot's plate centre, or
    // the hull centre under -kickWindRef) AND its bounded level in force while
    // the stroke is in flight (balance.kickStrokeActive minus the head - the
    // k14/k15 family's own shape, on the episode's third and last stretch).
    //   -1 (the flag's default) = OFF: the branch below is dead and the flight
    //      carries the level 0f the line above wrote - every landed row of the
    //      lane, bit for bit (R2);
    //   0  = the pre-k6 law (the loop's own full authority toward the hull
    //      centre): the branch writes the level 0f, so the row is the flag's
    //      own absence, character for character;
    //   0..1 = the bounded level, the ladder's own rungs.
    private float kickWindFlight = -1f;

    // ─── stand frame ───────────────────────────────────────────────────────
    private bool standCaptured;
    private float standPelvisY;
    private float standAnkleY;
    private float groundY;
    private float pelvisCenterToSole;

    // ═══ WALK REFLEX (27.09, task "Demo walk: step-up mechanism + tune") ═══
    // The one ground datum this walk holds is taken at the stand frame and is
    // correct only while the floor does not move under it. On the shipped yard
    // tape the floor rises 5 cm at the CurbEntry step (x=8) and the walk dies
    // there: the sole is asked for a plane that no longer exists, the foot stubs
    // the lip and the body topples backwards (fall 20.10 s, the report
    // evo-demo-walk-20260927.md §4b). These two are the whole interface the
    // WalkReflex component writes and this file reads - the reflex is the only
    // source of both, so with no component (or an inert one) every landed
    // expression stands bit for bit:
    //   ReflexFloorOffsetM   - the LOCAL floor's height above that datum, m.
    //                          Added to every height the walk reads off the
    //                          ground (see FloorDatum below) and to the swing
    //                          clearance, so a rise lifts the whole step and
    //                          the foot lands ON the new floor.
    //   ReflexLiftBoostM     - extra sole clearance while a rise is still ahead.
    //   ReflexStumbleVelMps  - 0 = off: the backward-stumble recovery step. With
    //                          it set, a CoM carried BACKWARD past this speed
    //                          for ReflexStumbleHoldS while both soles are down
    //                          fires one swap; the placement law then aims the
    //                          foot where the momentum points (the recovery
    //                          step) instead of toppling. See the swap block.
    public float ReflexFloorOffsetM;
    public float ReflexLiftBoostM;
    public float ReflexStumbleVelMps;
    public float ReflexStumbleHoldS = 0.10f;
    public int ReflexStumbleSteps;      // recorder: recovery steps fired
    // Recorder (27.09): the placement x the law is asking this step (read by
    // WalkReflex's diagnostic line only).
    public float ReflexPlaceX;
    // The datum every height in this file is measured against. Identical to
    // groundY while ReflexFloorOffsetM is 0 (exact float add).
    private float FloorDatumM => groundY + ReflexFloorOffsetM;
    private float reflexStumbleHoldT;
    private bool reflexStumbleLatch;
    // Read by WalkReflex only (its probe needs both while nothing else does).
    public bool Active => active;
    public float GroundYDatum => groundY;
    public float ReflexEyeX => pelvisBody != null ? pelvisBody.position.x : transform.position.x;
    // WALK REFLEX (27.09): the walk's own leg state, mirrored once per TickWalk
    // frame for the reflex's datum gate and its diagnostic line. Written by
    // TickWalk below; nothing in this file reads them, and with no reflex
    // component (or -walkStepDual 0) they are never read at all.
    public bool ReflexLeftDown, ReflexRightDown;
    public float ReflexLeftFootY, ReflexRightFootY;
    public float ReflexLeftFootX, ReflexRightFootX;
    public bool ReflexBothSolesDown => ReflexLeftDown && ReflexRightDown;
    // WALK REFLEX (27.09): the reflex's push-flick hold. While true the stance
    // ankle's push flick (pushDeliverP) is delivered at 1.0 - NOT amplified. The
    // flick is the walk's own roll onto the toe, and a toe standing AT a riser
    // rolls against the wall: the reflex holds the flick for the step that is
    // using the riser as its floor. Written by WalkReflex only; false with no
    // component (or no toe at a riser) = the landed expression bit for bit.
    public bool ReflexHoldPush;
    // WALK REFLEX v2 (28.09): the rise's own step-up delivery, one factor per leg
    // (0 = off). While a leg's sole stands ON the top surface of a rise the factor
    // scales that leg's support delivery in DriveOneLeg: the knee's own drive and,
    // through the shared kneeP, the ankle's plantar push - the climb's own work,
    // delivered. ReflexStepUpHipShare adds the hip's extension channel to the same
    // factor (0 = the hip keeps its landed pair). Written by WalkReflex only;
    // 0 with no component (or no sole on a rise) = the landed expression bit for
    // bit (`* (1 + 0)` and `+ 0` are exact).
    public float ReflexStepUpL, ReflexStepUpR;
    public float ReflexStepUpHipShare;
    // WALK REFLEX v2b (28.09, pace recovery - the vault's own lever): the share
    // of the step-up boost the ANKLE's own stiffness gets (1 = the ship: the
    // boosted kneeP rides into ankleP, so the rise's delivery is the knee's drive
    // AND the plantar roll - the vault of 8.7 that outran the feet; 0 = knee only:
    // the ankle keeps its landed kneeP share, so the same climb is delivered with
    // a compliant toe and cannot launch the body over its own support). Inert by
    // construction: 1 (or stepUpFactor 0) = the landed expression bit for bit.
    public float ReflexStepUpAnkle = 1f;

    // WALK REFLEX v2c (28.09): the cadence law's own scale (1 = the landed walk) -
    // the step clock advances at 1/ReflexCadenceScale. Written by WalkReflex only;
    // no component (or no pace above its trigger) leaves it exactly 1.
    public float ReflexCadenceScale = 1f;
    // WALK REFLEX v2e (28.09, pace recovery): the placement's own two pace seats.
    // ReflexBandScale scales the step band's CEILING (the reach the plan may ask
    // for) while the pace outruns the landed band; ReflexCatchShare is the share of
    // the pendulum constant the catch point uses (0.5 = the landed law: half the
    // constant, the lead the walk can take back; 1.0 = the arrest point = a braking
    // catch that bleeds surplus speed). Both are written by WalkReflex only and are
    // inert at their landed values (1 / 0.5).
    public float ReflexBandScale = 1f;
    public float ReflexCatchShare = 0.5f;

    // ─── CYCLE 115: THE SETTLE STEP ────────────────────────────────────────
    // The owner's stop ([[BRIEF-1002-SETTLESTEP]]): on the walk key's drop the
    // body must not hand over to the stand while it is still moving - today the
    // hand-off IS the fall (the cut lands mid-step at ~0.5-1.0 m/s; c96 wf1:
    // fall ~1.2 s after the stop). The settle keeps the WALK alive past the key:
    // the walk takes one more step whose placement is the ARREST point
    // (ReflexCatchShare 1.0 - "where the CoM comes to rest over its own foot"),
    // and the walk ends on that step's PLANT with both feet down, handing a
    // slow, double-support body to the stand. -poseWalkSettle S arms it (S =
    // the budget seconds; 0 = off, every earlier path bit for bit).
    // Selection: -poseWalkSettleMps V (the moving bar), -poseWalkSettleCatch C
    // (the arrest share), -poseWalkSettleLead L (extra lead in metres).
    private float walkSettleBudget;
    private float walkSettleMps = 0.10f;
    private float walkSettleCatch = 1.0f;
    private float walkSettleLeadM;
    private bool walkSettleLatch;      // the settle owns the walk
    private float walkSettleT;         // seconds under the latch
    private int walkSettleSwaps;       // swaps fired under the latch
    private bool walkSettleArmed;      // a swap fired post-latch: the plan is arrest
    private bool walkSettleWasWalk;    // the walk ran last tick (the drop's own edge)
    private bool walkSettleSpent;      // the one capture swing is spent (c115b)
    private float walkSettleThreatM;   // threat re-arm bar, m (-poseWalkSettleThreat)
    // CYCLE 116 - THE HANDOFF SETTLE. walkSettlePush scales the push-off
    // flick's amplification while the settle latch owns the walk (1 = the
    // landed expression, 0 = no amplification): the arrest step must not
    // deliver the walk's own push, it is the step that has to SHED the speed.
    // walkSettleBendDeg is the arrest step's stance-knee bend floor (0 = off):
    // a latched step cannot extend through full knee lock, so the load phase
    // absorbs instead of vaulting. walkSettleHandOn (with walkSettleBlendS) is
    // the handoff capture: at the release the stand re-anchors its height
    // reference to the body's own walk-end pelvis height and rides it back to
    // the stand frame's own over the window, so the first half-second does not
    // command a wall of correction onto a body that has just stopped moving.
    private float walkSettlePush = 0f;    // CYCLE 123 landed: 0 (the arrest sheds the push; -poseWalkSettlePush 1 restores)
    private float walkSettleBendDeg = 24f;    // CYCLE 123 landed: 24 (the arrest knee floor); -poseWalkSettleBend 0 = off
    // CYCLE 117: THE LOAD-DRIVEN FLOOR (the admittance give). The c116 floor is
    // ONE constant angle for every arrival; but the arrival's own load is what
    // the give should answer (the line's load-admittance doctrine, c87/c88's
    // bottom term): while the settle latch owns the walk the hot sole's load
    // above the carry bar buys knee give at walkSettleBendLoad deg per
    // body-weight, capped - the roll's own spike flexes the arrest step, a
    // quiet carry asks for none. Off (0) the load term is dead and every path
    // is the c116 expression, bit for bit.
    private float walkSettleBendLoad;       // deg per body-weight over the bar (0 = off)
    private float walkSettleBendBar = 0.7f; // the carry bar, body weights
    private float walkSettleBendCap = 45f;  // deg ceiling on the give
    // CYCLE 118 - THE HANDOFF HOLD (c117's named-behind: widen the pocket). The
    // c116/117 floor dies ON the release frame; the pocket's edges are the two
    // ways that hand-over ends badly (a bend too small hands over a body still
    // moving; one too big hands over a lean). Armed, the fold survives the
    // release: while the hold is live the STAND's stance knees may not extend
    // past walkSettleHoldDeg, and the hold ends on the body's own quiet
    // (|comVelX| under the bar for the quiet span), a real key, a down body, or
    // the safety cap - never on a blind timer. 0 = off, the c117 expression bit
    // for bit.
    private float walkSettleHoldDeg;           // deg floor through the handoff (0 = off)
    private float walkSettleHoldMps = 0.15f;   // the quiet bar, m/s
    private float walkSettleHoldQuiet = 0.25f; // quiet span to end the hold, s
    private float walkSettleHoldMax = 2.0f;    // the safety cap, s
    private bool walkSettleHoldLive;           // the hold owns the stance knees
    private float walkSettleHoldT;             // the hold's own clock, s
    private float walkSettleHoldQT;            // the quiet span's own clock, s
    // CYCLE 119 - THE STATE EXIT (c118's named-behind: the release/capture
    // exit). The c115 release needs the plant FRAME to read both feet down
    // while armed; on the shallow bends (20/23) no frame ever met it and the
    // arrest ran to the fall with no conclusion. Armed, the latch may also
    // conclude on the body's own state: both soles grounded, |comVelX| under
    // the bar, support margin at or over its floor, held for the quiet span -
    // a sustained window instead of a single frame. 0 = off, the c118
    // expression bit for bit.
    private float walkSettleExitMps = 0.35f;    // the move bar, m/s (0 = off); CYCLE 122 landed 0.35
    private float walkSettleExitQuiet = 0.15f;  // the held span that concludes, s
    private float walkSettleExitMargin = 0.05f; // the support floor, m
    private float walkSettleExitQT;             // the held span's own clock, s
    // CYCLE 120 - THE WALK-ON GUARD (see FallReflex and
    // BalanceController.settleReleaseGuardT): the release arms the reflex's own
    // deferral window - while it lives the reflex may not start a new swing until
    // the body reads quiet. 0 = off, the pre-120 law bit for bit.
    private float walkSettleGuardWin = 4.0f;    // the guard window, s (0 = off); CYCLE 121 landed 4.0
    private bool walkSettleHandOn;
    private float walkSettleBlendS = 0.35f;
    private float walkSettleHandT;         // the capture window's clock
    private float walkSettleHandPelvisY;   // the walk-end pelvis height
    // WALK REFLEX v2f (28.09, pace recovery): the walk's own uprightness seat. The
    // pose library's trunk tilt target is a fixed value: above the cadence the walk
    // was tuned at, the surplus speed pitches the body forward faster than the
    // library's trunk hold can answer (the trunk = the body's mass carrier, so the
    // pitch moves the CoM ahead of the plan and the plan collapses behind the
    // body). WalkReflex adds this lean (deg, + = the trunk BACK - the recorder's
    // torsoTilt sign has minus = forward) to the walk's torso target while its own
    // pace trigger holds; 0 = the landed target bit for bit.
    public float ReflexTorsoLeanDeg;

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

    // WALK CYCLE 6x (recorder only): the swing foot of the step in force has
    // left the ground since its swap, so the next frame it is planted IS this
    // step's own plant - the frame the newly placed sole first takes the load.
    // Armed at every swap (the leg that becomes the swing leg on that frame is
    // the one that just landed, so its own lift is still ahead of it) and
    // disarmed by the plant itself. Read by nothing but the placement latch
    // below; no law in this file or any other reads it.
    private bool swingAirborneThisStep;

    // WALK CYCLE 28: the support's own sole clearance, held - and DELIVERED at the
    // joint. Cycle 27's account is kept (the recruit in metres, the load gate, the
    // 0.0180 cap) and its finding is answered: the ask is only about a quarter
    // delivered (keep27d/e: a +8.85..10.45 deg ankle ask moved the joint
    // +1.3..3.7 deg and the support's sole to 0.0193..0.0201, right at the
    // estimator's own 0.02 m ground band), so this round drives the SAME ask with a
    // stiffer joint pair on the support leg's ankle and knee while the hold is live
    // (PoseLibrary.StanceHoldDeliverP) - the joint, not the ask.
    private float supportHoldM;

    // WALK CYCLE 35: THE PLANT'S OWN CLOCK - THE FIRE HELD TO THE PLANT'S OWN
    // ARRIVAL. Cycle 34's split (report 037 section 5): the delivered sole is read
    // at the fire-8 frame and the ARRIVAL re-times the fire - the fire's own
    // binding term on the catch is the incoming support's load READ (the 250 N
    // gate), the read sits one frame behind the row's own column, and the plate's
    // load FLICKERS through its arrival (base: 0 through 8.265, 615.0 N at 8.270,
    // 0.0 at 8.275; dose a: 511.2 at 8.250, 198.6 at 8.255, 273.9 at 8.260). The
    // lever: the fire may not read the plant on the flicker's first true frame -
    // it WAITS until the load has READ at or above its gate for this many seconds
    // CONTINUOUSLY, so the read lands with the plant's own arrival. The library,
    // the pose table, the landing key and every ask VALUE stay byte-kept: the ONE
    // writer is the ledOut expression in the swap block. 0 = OFF: the A7 path
    // byte for byte (arg -poseTailHold).
    private float poseTailHoldSeconds = 0f;

    // WALK CYCLE 35: the hold's own clock - seconds the incoming support's load
    // read has stood at or above the swap gate (leadSwapLoadN), continuously.
    // Written on every walk frame, read ONLY by the ledOut expression and only
    // once this step's tail latch below is set; with the knob at 0 it is not read
    // at all (the guard is `>= 0`, always true).
    private float poseTailLoadHeld;

    // WALK CYCLE 35: THE TAIL LATCH - this step's own tail, as cycle 34's gate
    // named it (the support's load has left, the swing leg is airborne with a
    // foot down, the CoM has taken its own lead). Set while the gate is open,
    // cleared at every swap. It is the HOLD's own confine: on the landed row the
    // gate opens ONLY from 8.150 (through 8.270), so the hold can never touch the
    // bar's own fire (6.245), the earlier fires or the 5.60..7.30 price window.
    private bool poseTailSeenThisStep;

    // WALK CYCLE 38: THE NEXT STEP'S OWN TAIL - the tail gate's own extension. The
    // landed gate (cycle 35's latch, cycle 37's carry - the same reads, the same
    // 50 N bound) opens on the landed row ONLY in the catch step's own flight tail
    // (8.160..8.275): the step AFTER it carries the body the whole way (the outgoing
    // support reads 218.7..249.8 N through its own tail, never <= 50), so the SAME
    // gate has no window there as landed - the very condition cycle 26/28's own load
    // bound names (the loaded collapse frames carry 230..380 N). This knob is that
    // bound, READ ON THE STEP THAT FOLLOWS A FLIGHT TAIL AND NOWHERE ELSE: the
    // license is the flight itself (the LANDED bound's own window), handed over at
    // the swap, and a widened window is not a flight - it does not chain past that
    // step. The ONE writer is the bound expression; the reads, the need bound
    // (PlantHoldNeedMin / SwingPlaceNeedMin) and the flags are cycle 34/35/37's own.
    // 50 = the landed bound: the emitted frames are the A7 path byte for byte.
    private float poseNextTailLoadN = 50f;
    // This step's own tail opened at the LANDED bound (the flight); handed to the
    // next step at the swap as the license above.
    private bool poseTailFlightThisStep;
    private bool poseTailFlightPrevStep;

    // WALK CYCLE 28: the support's own clearance as of the last GATED frame. The
    // recruit's rise term (see TickWalk's gate) is measured against it - and it only
    // FEEDS the account inside the excess branch, so no frame whose sole sits below
    // the cap can add to the hold (the round's first build's leak).
    private float supportHoldClearPrevM;

    // WALK CYCLE 28: the afterfall's own crawl (the get up's first stage, driven by
    // the law - see DriveAfterfallCrawl). The latches hold the pose each joint had
    // when the body went down, because the settle pose differs build to build
    // (kneeL 66 deg on the base row, 17 on keep27e) and the tuck must be the same
    // SHAPE on both.
    private bool afterfallCrawlOn;
    private float afterfallStartTime;
    private float afterfallHipL, afterfallHipR;
    private float afterfallKneeL, afterfallKneeR;
    private float afterfallAnkleL, afterfallAnkleR;

    // ═══ WALK CYCLE 40: THE GET-UP'S OWN STATE AND THE GRIP'S OWN STATE ═══
    // The ladder the card asks for: the fall (Falling), the heap with no walk
    // key (Fallen) and the rise (Getting up) are named by TickGetUp on the
    // body's own measured signals - the hands' real contact, the palms' load,
    // the CoM's height and the feet's own places. No stage advances on a blind
    // timer; every one of them has a measured signal AND a bound (the card's
    // 10 s window, divided: see GetUpPlantSeconds .. GetUpRiseSeconds), and the
    // bound is reported as a failure, never as a success.
    private enum GetUpStage { Idle = 0, Plant = 1, Press = 2, Tuck = 3, Rise = 4, Roll = 5 }
    private GetUpStage getUpStage = GetUpStage.Idle;
    private float getUpStageEnterTime;
    private float getUpDownSince = -1f;
    private float getUpFallTime = -1f;
    private float getUpRetryAfter = -1f;
    private bool getUpDone;
    private bool getUpHandsPlanted;    // latched: both palms held real contact
    private bool getUpReachedTuck;     // latched: both feet came under the body
    private bool getUpStandNamed;      // the card's bars named on one frame
    private bool getUpDriveOn;         // the law owned the joints on this step
    // ROUND 18 (the consult: "on-back -> roll to side first"): the roll stage's
    // own switch. While the body is on its back the ladder rotates it off the
    // back hemisphere BEFORE the plant/press may engage; -poseGetUpRoll 0 keeps
    // the pre-round-18 path bit for bit. -poseGetUpRollForce 1 = the probe (one
    // forced roll at the first attempt, for the film and the machinery proof).
    private bool getUpRollEnabled;
    private float getUpRollForce;
    private PoseOrientation getUpOrient;   // the self-orientation core (may be absent)
    private bool getUpOrientTried;
    // ROUND 18 v2 (the ex48/fx150 loop finding): the dwell gate = only a body
    // that has READ back continuously for the dwell is rolled (a mid-toss pass
    // is not a lie); the cap = after this many consecutive bound rolls the
    // divert stands down until the pose leaves the back (no runaway re-entry).
    private float getUpRollDwell;
    private int getUpRollCap;
    // ROUND 29 (the kneel-to-stand push): the rise's own stand mode. The r28
    // columns read the landed-pose unwind as the ceiling: it sweeps the soles
    // ~0.8 m behind the CoM, so the feet-under bar never arms and every attempt
    // ends on its bound. With -poseGetUpStand 1 the rise first HOLDS the fold
    // (the soles ride under the body while it settles) and then converges the
    // legs on the stand pose (-poseGetUpStandHip/Knee/Ankle) instead of the
    // landed pose. Default 0 = the landed expression, bit for bit.
    private bool getUpStandMode;
    private float getUpStandHold;      // share of the rise spent holding the fold
    private float getUpStandHipDeg;    // the stand pose the rise converges on
    private float getUpStandKneeDeg;
    private float getUpStandAnkleDeg;
    private int getUpStandFrom;        // R29 v2: 0 = blend from the fold (draw 1),
                                       //          1 = blend from the LATCHED pose (draw 0)
    // ROUND 30 (the contact-gated rise): the r29 near-miss lifted the soles
    // exactly as the extension accelerated, so the r29 rise could never happen
    // OVER a planted base. With -poseGetUpStandGate 1 the Rise stage's own clock
    // HOLDS while the soles are not both grounded and resumes when they are;
    // the blend, the trunk and the stage's bound all ride that clock.
    // -poseGetUpStandGateMax caps the hold seconds so a hopeless attempt still
    // ends on its bound. Default 0: riseClock == stageAge, byte for byte.
    private bool getUpStandGate;
    private float getUpStandGateMax = 3f;
    private float getUpStandGateHeld;    // seconds held inside this attempt's Rise
    private int getUpStandGateHolds;     // hold episodes inside this attempt's Rise
    private bool getUpStandGateInHold;   // a hold is open right now
    // ROUND 31 (the base gate): the r29 near-miss named the state the rises must
    // start from - both feet UNDER the body AND grounded, palms planted - and
    // measured it during the Press at 59.2, 0.4 s before the stage bounds that
    // carried the body off its base. With -poseGetUpBase 1:
    //   (a) THE FAST-PATH: from the Press or the Tuck, the moment the base reads
    //       (feetBar && palms planted) the Rise starts right then;
    //   (b) THE TUCK WAIT: past its bound the Tuck no longer hands off - it waits
    //       for the base, re-seating the feet at -poseGetUpBaseDraw; the cap
    //       -poseGetUpBaseMax is a FAILURE REPORT (the attempt aborts), never an
    //       escape into an extension off the base;
    //   (c) THE PRESS HOLD: while the Tuck waits or the contact gate holds, the
    //       arm press level drops to -poseGetUpBasePress (default 0) - the stroke
    //       was measured lifting the body off the very feet it must rise over.
    // Default 0: the r29/r30 route runs byte for byte.
    private bool getUpBaseOn;
    private float getUpBaseMax = 3f;      // the Tuck wait's cap, seconds
    private float getUpBasePress;         // the press level while the gate holds
    // ROUND 38: THE WAIT-PRESS LIFT (-poseGetUpWaitPress P + -poseGetUpWaitPressSec S).
    // The nominal base sits on its pelvis and the soles read EXACTLY zero fz, so no
    // leg extension can lift the body (r34/r35). With P >= 0 the wait's press ramps
    // 0 -> P over S seconds per wait episode - the measured arm stroke lifting the
    // body toward its feet - and the GETUP-WAITPRESS log carries the fz response.
    // Default -1: the r31 press rule, byte for byte.
    private float getUpWaitPress = -1f;    // the wait's lift level (-1 = off)
    private float getUpWaitPressSec = 1.2f;// the lift's ramp, seconds
    private float getUpWaitPressT;         // seconds of lift this wait episode
    private float getUpWaitPressLogT;      // the next periodic log, seconds
    private float getUpBaseDraw = 1f;     // the re-seat draw while the Tuck waits
    private float getUpBaseWait;          // seconds waited for the base this attempt
    private int getUpBaseAborts;          // cap aborts, this run (persistent)
    private bool getUpBaseFast;           // the fast-path fired this attempt
    // ROUND 31b: the press hold over the WHOLE fastpath rise. v1 dropped the
    // press only inside the r30 contact-gate holds, but the measured launch
    // lifts the body 0.2-0.3 s AFTER the fastpath fire while both feet still
    // read grounded - the stroke is what walks the body off its base.
    private bool getUpBaseRise;           // the whole-rise press hold, asked
    // ROUND 32: THE SETTLE GATE - the fastpath may not fire off a mid-impact
    // contact. Measured in the r31 traces: every fastpath caught a body FALLING
    // (-2..-3.5 m/s); the plant compressed 6-12 cm in 0.05 s and the drives
    // turned the impact into a bounce (pelvVelY -1.5 -> +1.1 in one frame,
    // then +3.6 m/s and a ~1.4 m flight) - the base was never settled, only
    // touched. With -poseGetUpBaseSettle S (>0) the fire needs the base HELD:
    // feetBar && palms && |pelvVelY| <= -poseGetUpBaseSettleVel, continuously
    // for S seconds. Default 0: the r31 route runs byte for byte.
    private bool getUpBaseSettleOn;
    private float getUpBaseSettleSec;      // the dwell the base must hold, s
    private float getUpBaseSettleVel = 0.6f;
    private float getUpBaseSettleT;        // the dwell so far, this attempt
    private int getUpBaseSettleBlocks;     // would-be fires refused, this run
    private bool getUpBaseSettleWarned;    // one log line per refusal episode
    // ROUND 33: THE ANTI-FLICKER FORMS. The r32 fine trace: the base state holds
    // in runs of 0.065/0.115 s split by 1-4 frame flag dropouts, so the strict
    // continuous dwell can never reach 0.2 s. Two replacements, both OFF by
    // default (the r32 route runs byte for byte):
    //  -poseGetUpBaseSettleDrop D  - a DEBOUNCE: a dropout <= D s does not reset
    //    the settle clock (the clock keeps running through the blink);
    //  -poseGetUpBaseSettleDuty W  - a DUTY CYCLE (with -poseGetUpBaseSettleDutyFrac
    //    F, default 0.6): the quiet fraction over the last W s must reach F, with
    //    the current frame quiet. W > 0 selects this form over the dwell.
    private float getUpBaseSettleDrop;     // the debounce tolerance, s (0 = strict)
    private float getUpBaseSettleDutyWin;  // the duty window, s (0 = not the duty form)
    private float getUpBaseSettleDutyFrac = 0.6f;
    private float getUpBaseSettleGap;      // the dropout so far, s (the debounce's)
    private float getUpBaseSettleDuty;     // the duty readout, 0..1
    private const int GetUpSettleDutyCap = 512;    // 512 x 5 ms = 2.56 s of ring
    private readonly float[] getUpSettleDutyRingT = new float[GetUpSettleDutyCap];
    private readonly float[] getUpSettleDutyRingQ = new float[GetUpSettleDutyCap];
    private int getUpSettleDutyHead;
    private int getUpSettleDutyCount;
    private float getUpSettleDutySum;      // quiet seconds inside the window
    private float getUpSettleDutyAge;      // wall seconds since the attempt began
    // ROUND 35: THE LOADED BASE + HIP-FIRST (the r34 NEXT-1).
    // (a) THE LOAD GATE: the "base" the r30-r33 gates tested is a POSE-FLAG state; the
    //     measured soles under it carried only 0.16-0.28 BW through the extension. With
    //     -poseGetUpBaseLoad F (>0) the fastpath (and the Tuck's own exit) additionally
    //     need BOTH soles to carry >= F x weight, sustained -poseGetUpBaseLoadSec s;
    //     -poseGetUpBaseLoadDrop D tolerates a dropout <= D s (the r33 debounce lesson).
    //     Refusals are counted and named (GETUP-BASE load-block, with the live fz values).
    // (b) THE SIT-BACK (the loaded squat): -poseGetUpBaseSquat B > 0 adds B deg of hip
    //     flexion (and B/2 at the knee) to the Tuck wait's own drive, so the weight
    //     shifts back onto the planted feet while the gate waits.
    // (c) THE HIP-FIRST EXTENSION: -poseGetUpStandHipFirst D delays the knee's (and the
    //     ankle's) blend so the hip leads the extension; -poseGetUpStandKneeClimb C
    //     instead gates the knee release by the pelvis's own climb since the Rise began
    //     (C m = full release). Both 0 = the r29 blend byte for byte.
    private float getUpBaseLoadFrac;       // the per-foot load floor, x weight (0 = off)
    private float getUpBaseLoadSec = 0.1f; // the load must hold this long, s
    private float getUpBaseLoadDrop;       // the dropout tolerance, s (0 = strict)
    private float getUpBaseLoadT;          // the hold so far, this attempt
    private float getUpBaseLoadGap;        // the dropout so far, s
    private int getUpBaseLoadBlocks;       // load-refused fires, this run
    private bool getUpBaseLoadWarned;      // one log line per refusal episode
    // ROUND 39: THE LOADED-WINDOW EXTENSION (the r38 find: the ladder builds a
    // loaded stand-state - the Tuck's fold-unwind with both soles carrying - and
    // re-folds it away; every fire fired off a fz=0 kneel). With
    // -poseGetUpLoadExt 1, during the Tuck: a window (both soles >=
    // -poseGetUpLoadExtKeep x weight) SUPPRESSES the wait's re-fold - the legs
    // hold the unwound draw-0 state - and the Rise fires on the window itself:
    // the soles' SUM >= -poseGetUpLoadExtFire x weight for -poseGetUpLoadExtSec s
    // (a dropout <= -poseGetUpLoadExtDrop s does not reset the clock). The
    // GETUP-LOADEXT enter/exit lines and the fire line carry the fz values.
    // Default 0: every clock below stays 0 - the r38 route, byte for byte.
    // ROUND 40b: the fire-time re-latch (see the fire block). 0 = off.
    private bool getUpLoadExtRelatch;
    private bool getUpLoadExtOn;
    private float getUpLoadExtKeep = 0.04f;  // the per-foot keep floor, x weight
    private float getUpLoadExtFire = 0.10f;  // the sum fire floor, x weight
    private float getUpLoadExtSec = 0.3f;    // the sum must hold this long, s
    private float getUpLoadExtDrop = 0.05f;  // the dropout tolerance, s
    private float getUpLoadExtT;             // the fire clock, s
    private float getUpLoadExtGap;           // the dropout so far, s
    private int getUpLoadExtWindows;         // windows opened, this run
    private int getUpLoadExtFires;           // fires landed, this run
    private bool getUpLoadExtLive;           // the keep window's own state
    // ROUND 40: THE PUSH TRACE + THE HEEL RELEASE (the r39 over-pitch split).
    // The r39 k05 push (fire 68.895) sat INSIDE the deck's -15 -> +15 move window
    // [68.7, 69.6]. -poseGetUpPushTrace 1 logs the blend keys and the leg/trunk
    // targets per frame for -poseGetUpPushTraceSec s from a fire (the csv has the
    // means, not the commands). -poseGetUpStandAnkleFast moves the ankle's blend
    // key off the hip-first delay: 0 = kKnee (verbatim), 1 = kk, 2 = 2*kk.
    private bool getUpPushTraceOn;
    private float getUpPushTraceSec = 2.5f;
    private float getUpPushTraceT0 = -1f;
    private int getUpStandAnkleFast;
    private float gusKeyk, gusKeyKnee, gusKeyAnkle;            // the blend's keys
    private float gusTgtHipL, gusTgtKneeL, gusTgtAnkleL;       // the left leg's targets
    private float getUpBaseSquatDeg;       // the sit-back depth, deg (0 = off)
    private float getUpStandHipFirst;      // the knee's delay, blend units (0 = off)
    private float getUpStandKneeClimb;     // the knee's climb gate, m (0 = off)
    // c99 AUTO RISE (owner's addendum 01.10): the card's own input grows a
    // self-press. A keyless down held -autoGetUp D seconds reads as the key
    // being on (the man tries to rise himself: knees -> arms -> sit -> stand).
    // 0 = off, the historic "no walk key, no rise", byte for byte.
    private float getUpAutoDwell;
    private float getUpAutoAge;
    private bool getUpAutoKey;
    // ROUND 50: THE EXTENSION'S UNFOLD ORDER. The historic sit-ext released
    // the joints off ONE clock (hip first, knee/ankle delayed by hipFirst).
    // The r49 corridor face-plant: the hip walked to the stand pose while the
    // knees were still railed and the body pitched forward BEFORE the unfold.
    // Each joint gets its own release offset (blend units): kJ = (kk-SJ)/(1-SJ).
    // Defaults keep the historic shape byte for byte.
    private float getUpStandHipStart;          // the hip's release offset (0 = kk)
    private float getUpStandKneeStart = -1f;   // the knee's offset (<0 = hipFirst rule)
    private float getUpStandAnkleStart = -1f;  // the ankle's offset (<0 = kKnee)
    // ROUND 42: THE MOVE-GUARD (the rise vs a sweeping deck surface) and THE TRUNK
    // GOVERNOR (bound the torso pitch through the carry). Both default off; the
    // semantics live in the Rise stage. -poseGetUpMoveGuard 1 + MoveGuardMax;
    // -poseGetUpTrunkGov 1|2|3 (1 gate, 2 counter-lean, 3 both) + GovDeg/K/Max.
    private bool getUpMoveGuardOn;
    private float getUpMoveGuardMax = 3f;
    private float getUpMoveGuardHeld;
    private int getUpMoveGuardHolds;
    private bool getUpMoveGuardInHold;
    private float getUpTrunkGovMode;
    private float getUpTrunkGovDeg = 40f;
    private float getUpTrunkGovK = 0.5f;
    private float getUpTrunkGovMax = 2f;
    private float getUpTrunkGovHeld;
    private int getUpTrunkGovHolds;
    private bool getUpTrunkGovInHold;
    // ROUND 43: THE SIT-STAND CONVERSION (the r42 cap-15 sit regime). The sit
    // window is the one state of this line with an upright torso, loaded soles
    // and no motion - and IsDown is blind there (the sitting term needs a drop
    // past 0.50 m; the sit parks above it), so the ladder never runs over it.
    // -poseGetUpSitStand 1 arms: the window (quiet, upright, both soles loaded
    // and parked low) held for Hold s fires a Rise attempt with the pose latched
    // as it sits; while the window holds, the bound's clock is deferred up to
    // Max s. All default off - every pre-existing route keeps its bytes.
    private bool getUpSitStandOn;
    private float getUpSitStandHold = 0.10f;
    private float getUpSitStandMax = 6f;
    private float getUpSitStandKeep = 0.05f;
    private float getUpSitStandVel = 0.50f;
    private float getUpSitStandDropMin = 0.15f;
    // c99 FLAT WINDOW CEILING: the r43 window's drop ceiling was the deck's
    // high-sit geometry (GetUpSittingDropMeters 0.50). A flat floor-sit parks
    // ~0.9 m below stand, so the window never opened there and the seat/step
    // machinery (r45-54) stayed unreachable on flat. With
    // -poseGetUpSitStandDropMax > 0.5 the window opens over the floor-sit too;
    // 0.50 = the historic ceiling, byte for byte. This knob moves the WINDOW
    // only; IsDown's sitting term keeps GetUpSittingDropMeters.
    private float getUpSitStandDropMax = 0.50f;
    private float getUpSitStandT;          // the quiet dwell (Tick-side clock)
    private float getUpSitStandExt;        // the live sit attempt's ext seconds
    private int getUpSitStandFires;        // fires (report)
    private bool getUpSitActive;           // the live attempt is a sit-fired one
    private bool getUpSitWinNow;           // the window, as of this tick (trace)
    // ROUND 44: THE SIT HEEL-PULL (the r43 NEXT-1, knee-flexion-first). The r43
    // read: the stand blend keeps the knee near its latched value (sit knee +6
    // against stand +7) and the hip-first delay freezes it outright, so the legs
    // never FOLD UP over the feet - the pelvis has no path over a base that
    // trails ~0.8 m behind the CoM. With -poseGetUpSitPullKnee K > 0 a sit-fired
    // attempt runs a PULL phase first: the legs drive to latched + (knee +K,
    // hip -H, ankle -A) over -poseGetUpSitPullSec s - the heels fold in under
    // the body (the r39 pre-push state) - and then the extension blends from
    // the PULLED pose to the stand targets. All deltas from the latched sit
    // pose; every default 0 = the r43 routes byte for byte.
    private float getUpSitPullKnee;        // the pull's knee bulge, deg (the enabler)
    private float getUpSitPullHip;         // the thigh's swing from the latch, deg
    private float getUpSitPullAnkle;       // the ankle's release from the latch, deg
    private float getUpSitPullSec = 0.7f;  // the pull phase's own duration, s
    private bool getUpSitPullOn;           // knee > 0 at parse
    private float getUpSitPullKey;         // pull share 0..1, then 1 + ext share (trace)
    private bool getUpSitPullLogged;       // one enter line per attempt
    // ROUND 45: THE SIT RE-SEAT (the r44 NEXT-1). The r44 read: the sit heel-pull
    // re-arranges the legs IN THE AIR - the butt pins the pelvis, the soles read
    // fz == 0 through the whole pull, and the pelvis gains at most +0.24 m. The
    // two orders the r44 consult asked to price:
    //   1 = ARMS-FIRST     a press phase (the palms take the load first, gated on
    //                      the palms' own measured grip load) then a GATED pull
    //                      (gate: soles loaded OR the palms carrying), press held;
    //   2 = FOLD-ONLY      no press phase; the pull advances ONLY while the soles
    //                      carry load (the fold-vs-deck order);
    //   3 = ARMS + HANDOVER  as 1, and the press RAMPS DOWN over the extension's
    //                      first Hand seconds - the load hands over to the soles.
    // The pull clock FREEZES while its gate is closed (frozen seconds counted,
    // deferred out of the attempt's bound), so the re-seat can only run while
    // the body is supported by something other than the butt. Every default 0 =
    // the r44 route byte for byte.
    private float getUpSitSeatMode;        // 0 off, 1 arms-first, 2 fold-only, 3 arms+handover
    private float getUpSitSeatPress = 0.9f;    // the seat press level, 0..1
    private float getUpSitSeatPressMin = 0.10f;// the palms' engaged load, BW each
    private float getUpSitSeatPressMax = 1.2f; // the press phase's cap, s
    private float getUpSitSeatKeep = 0.05f;    // the soles' keep load, fraction of W (summed)
    private float getUpSitSeatGrace = 0.15f;   // a gate dropout up to this is not a close, s
    private float getUpSitSeatHand = 0.8f;     // mode 3: the handover ramp, s
    private float getUpSitSeatFrozenMax = 2.5f;// the freeze budget, s (bound deferral cap)
    private float seatPressUsed;           // the press phase's spent seconds
    private float seatPullT;               // the gated pull clock, s
    private float seatExtT;                // the extension clock, s
    private float seatFrozenT;             // the pull's frozen seconds, s
    private int seatGatePasses;            // gate openings (report)
    private int seatPhase = -1;            // -1 idle, 0 press, 1 pull, 2 ext
    private float seatPressNow;            // the ask level this frame (-1 = historic)
    private float seatGateLastOk = -999f;  // the last frame the gate read open
    private bool seatGateNow;              // the gate as of this frame (trace)
    private bool seatGatePrev;             // the previous frame's gate (edge count)
    // ROUND 46: THE DRAG'S GUARDS (the r45 NEXT-2). The r45 read: the press
    // loads the palms but the pull still tips the body (torso -10 -> -46 by
    // the pull's mid-point) and the ensemble slides; the drag never lands
    // under the CoM. Two signals, both default 0 = the r45 bytes:
    //   -poseGetUpSitDragGap G  the GAP EXIT: both feet within G m of the CoM
    //                           (comX - footX <= G) end the drag where it
    //                           stands; the extension then starts from the
    //                           exit pose (seatPullExitKey), not the fold's.
    //   -poseGetUpSitDragTip T  the TIP FREEZE: while the body tilt deviates
    //                           from the drag's start pitch by more than T
    //                           deg the pull clock freezes (same budget as a
    //                           closed gate), so the fold cannot feed its
    //                           own runaway. Mode 4 = the mode-1 route + both.
    private float getUpSitDragGap;         // the gap exit, m (0 off)
    private float getUpSitDragUnder;       // CYCLE 106: -poseGetUpSitDragUnder: the drag's
                                           // two-sided gap exit, m (0 = the one-sided r46 rule)
    private float getUpSitDragTip;         // the tip freeze band, deg (0 off)
    private float getUpSitDragTilt = 999f; // the drag's start pitch (the tip datum)
    // CYCLE 108: THE SEATED HEEL-DRAW (the c107 named-behind). The draw over a
    // SEATED torso throws the body backward (measured, attempt 0: tilt 10 -> 52
    // in 0.55 s). Armed, the trunk carries a forward counter-lean through the
    // draw, scaled by the pull's own progress. 0 = every historic route.
    private float getUpSitDrawLean;        // -poseGetUpSitDrawLean: the counter-lean, deg
    private float getUpSitDrawSeat = 40f;  // -poseGetUpSitDrawSeat: the draw-start pitch gate, deg
    private bool getUpSitDrawLogged;       // one hold line per attempt
    // CYCLE 109: THE HEEL-PLANT IN THE DRAW'S WAIT (the c108 named-behind). The
    // pull's gate waits for sole load; while it is closed the draw counts
    // toward the dead-man budget (FrozenMax) and the attempt dies. Armed, the
    // wait drives the feet DOWN-and-CLOSE instead of idling: while the gate is
    // closed the legs blend from the frozen pull pose toward the plant deltas,
    // the frozen budget is suspended for the plant's own window (bounded by
    // -poseGetUpSitDrawPlantMax), and the draw releases the moment the sole
    // load returns. Scale 0 = every historic route byte for byte.
    private float getUpSitDrawPlant;       // -poseGetUpSitDrawPlant: master scale (0 = off)
    private float getUpSitDrawPlantKnee = 45f;   // the knee's fold delta (the close axis), deg
    private float getUpSitDrawPlantHip = -25f;   // the hip's delta (signed: - flex, + extend), deg
    private float getUpSitDrawPlantAnkle = -12f; // the ankle's delta (signed), deg
    private float getUpSitDrawPlantSec = 1.0f;   // the plant's ramp, s
    private float getUpSitDrawPlantMax = 2.0f;   // the plant's window before the count resumes, s
    private float seatDrawPlantT;          // the wait's own clock, s (the frozen-budget shield)
    private float seatDrawPlantK;          // the plant's key (0..1; the ramp), trace
    private bool seatDrawPlantLogged;      // one enter line per attempt
    private bool seatDrawPlantReleased;    // one release line per attempt
    // CYCLE 114: THE RE-ARM. The plant is single-shot per attempt (c109): the
    // first release spends the window; the re-unloaded draw then meets the
    // dead-man and the attempt dies. Armed, every re-closed gate starts a
    // FRESH episode - plant -> release -> advance -> freeze -> plant, up to
    // the episode cap - so the draw advances in supported steps. The release
    // stays gated on the sole load (keep). 0 = every c109 route byte for byte.
    private float getUpSitDrawPlantRe;     // -poseGetUpSitDrawPlantRe: per-episode re-arm (0 = off)
    private float getUpSitDrawPlantEps = 3f; // -poseGetUpSitDrawPlantEps: episode cap (incl. the first)
    private float seatDrawPlantEpT;        // the episode's own window clock, s (CYCLE 114)
    private int seatDrawPlantEps;          // episodes entered since the fire (trace)
    private bool seatDrawPlantPrevGate;    // the gate's previous frame (the re-arm edge)
    private float seatGapL, seatGapR;      // comX - footX, this frame (trace)
    private float seatBodyTilt;            // the body tilt, this frame (tip source)
    private float seatTipDev;              // |body tilt - drag start tilt|, deg (trace)
    private float seatPullExitKey = 1f;    // the drag's exit share (the ext's from-pose)
    // ROUND 49: THE CORRIDOR RE-SEAT (the r48 NEXT-1). Between the drag's exit
    // and the extension the legs take turns adding a discrete per-leg budget
    // over the pull's exit pose (Knee folds the heel in, Hip slides the body
    // toward the feet, Ankle releases the toe) so the feet step in under the
    // CoM; the extension releases on the corridor (both feet within ReseatGap
    // m of the CoM AND both soles >= ReseatLoad*W for ReseatLoadSec s), on the
    // shares' exhaustion or on the stage's own budget - a bounded stage, never
    // a deadlock. All defaults 0/off = the r48 bytes.
    private float getUpSitReseat;          // -poseGetUpSitReseat: 1 on, 0 off
    private bool getUpSitReseatOn;
    private float getUpSitReseatGap = 0.18f;
    private float getUpSitReseatKnee = 70f;
    private float getUpSitReseatHip;
    private float getUpSitReseatAnkle;
    private float getUpSitReseatSec = 0.60f;
    private float getUpSitReseatMax = 4.0f;
    private float getUpSitReseatLoad = 0.05f;
    private float getUpSitReseatLoadSec = 0.20f;
    private float seatResL, seatResR;      // per-leg shares 0..1 (trace)
    private float seatReseatT;             // the stage's clock, s (trace)
    private float seatResTurnT;            // the current turn's clock, s
    private float seatResLoadT;            // the sustained-sole-load clock, s
    private int seatResTurn;               // the active leg (0 L, 1 R)
    private bool seatReseatDone;           // the hand-off happened
    private bool seatResLogged;            // one enter line per attempt
    private float seatResHipL, seatResKneeL, seatResAnkleL;   // the hand-off pose
    private float seatResHipR, seatResKneeR, seatResAnkleR;   // (the ext's from)
    // ROUND 51: THE DISCRETE STEP RE-SEAT (the r48-consult order: press-ok ->
    // per-leg discrete steps - ONE foot at a time - -> the corridor -> the
    // ext). The historic pull folds BOTH knees: both soles leave the deck at
    // pull 0.33 and the sitting body dives off its own butt (torso -10 ->
    // -67 in ~0.9 s - the r51 trace). A step folds ONE leg while the other
    // stays planted. Every sharing target is the latch + the leg's own share
    // of the pull-sized budget; the shares ramp in leg cycles (L then R),
    // full rounds are repeatable, and the machine ends on the corridor (both
    // gaps within StepGap AND the summed sole load held StepLoadSec), on the
    // rounds or on the budget - always bounded. The hand-off pose lands in
    // seatRes* for the r49 StandReseat extension. Off = every historic route
    // byte for byte.
    private float getUpSitStep;            // -poseGetUpSitStep: 1 on, 0 off
    private bool getUpSitStepOn;
    private float getUpSitStepHip = 60f;   // per-leg budget: hip delta from the latch
    private float getUpSitStepKnee = 160f; // per-leg budget: knee delta
    private float getUpSitStepAnkle = 50f; // per-leg budget: ankle delta
    private float getUpSitStepSec = 0.55f; // per-leg travel ramp, s
    private float getUpSitStepHold = 0.30f;// per-leg hold at the target, s
    private float getUpSitStepPlant;       // ease back by this share during the hold (0 = keep)
    private float getUpSitStepGap = 0.15f; // the corridor gap, m
    private float getUpSitStepLoad = 0.05f;// the corridor load, fraction of W (summed)
    private float getUpSitStepLoadSec = 0.20f; // the sustained-load clock, s
    private float getUpSitStepMax = 6.0f;  // the whole machine's budget, s
    private float getUpSitStepRounds = 1f; // full L+R rounds (each round adds a budget pass)
    private float getUpSitUnder;           // CYCLE 105: -poseGetUpSitUnder: the two-sided
                                           // under-gate + the full-delta march (0 = off)
    private float stepT;                   // the machine's clock, s
    private float stepFracL, stepFracR;    // the per-leg shares (published to the trace)
    private float stepFromFrac;            // the active leg's share at its phase's start
    private int stepLeg;                   // the active leg (0 L, 1 R)
    private int stepPhase;                 // 0 travel, 1 hold
    private float stepPhaseT;              // the phase's clock, s
    private int stepRound;                 // 0-based round index
    private bool stepDone;                 // the hand-off happened
    private bool stepLogged;               // one enter line per attempt
    private float stepLoadT;               // the sustained-sole-load clock, s
    // ROUND 51c: THE EXT SINK ARREST (the 05:35 consult): while the seat
    // extension runs, its clock FREEZES once the body pitches off its
    // ext-start marks - the torso pitch rate past ArrestRate deg/s or the
    // pelvis dropped ArrestDrop m below its ext-start height. Bounded by
    // ArrestMax (a spent freeze resumes ungated - the r46 lesson), logged on
    // each trip, default off = every historic route byte for byte.
    private float getUpSitExtArrest;          // -poseGetUpSitExtArrest: 1 on, 0 off
    private bool getUpSitExtArrestOn;
    private float getUpSitExtArrestRate = 40f;   // the torso pitch rate bound, deg/s
    private float getUpSitExtArrestDrop = 0.10f; // the pelvis drop bound below ext start, m
    private float getUpSitExtArrestMax = 2.5f;   // the freeze budget, s
    private bool seatExtArrestInit;              // the ext-start datum captured
    private float seatExtPelvY0;                 // the ext-start pelvis Y, m
    private float seatExtPrevTilt;               // the previous frame's body tilt, deg
    private float seatExtArrestT;                // the frozen clock, s
    private int seatExtArrestTrips;              // trips this attempt
    private bool seatExtArrestBad;               // the live state
    // ROUND 52: THE POST-STEP WEIGHT TRANSFER (the r51 NEXT-1; the step
    // exits unloaded). A bounded stage between the step's hand-off and the
    // extension: the legs drive partway back OUT of the fold (hip extension,
    // knee release, a plantar press) seeking a real sole load; exit on the
    // load (sum Fz >= Load*W sustained LoadSec) or the budget. Defaults off
    // = every historic route byte for byte.
    private float getUpSitShift;              // -poseGetUpSitShift: 1 on, 0 off
    private bool getUpSitShiftOn;
    private float getUpSitShiftHip = 25f;     // the hip extension delta from the capture, deg
    private float getUpSitShiftKnee = 45f;    // the knee release delta (toward straight), deg
    private float getUpSitShiftAnkle = 15f;   // the ankle plantar press delta, deg
    private float getUpSitShiftLean;          // the trunk lean over the stage, deg (0 = historic)
    private float getUpSitShiftSec = 2f;      // the stage's budget, s
    private float getUpSitShiftRamp = 0.9f;   // the deltas' travel, s
    private float getUpSitShiftLoad = 0.15f;  // the exit load, fraction of W (Fz sum)
    private float getUpSitShiftLoadSec = 0.25f; // the sustained-load clock, s
    private float getUpSitShiftMax = 4f;      // the rise-bound deferral budget, s
    private float sitShiftT;                  // the stage's clock, s
    private bool sitShiftDone;                // the hand-off happened
    private bool sitShiftEntered;             // one enter line per attempt
    private float sitShiftLoadT;              // the sustained-sole-load clock, s
    private float sitShiftFzMax;              // the stage's peak sole load, N
    private float sitShiftGapMin = 999f;      // the stage's smallest foot-CoM gap, m
    private float sitShiftFrac;               // the deltas' fraction
    // ─── ROUND 54: THE PLANT STAGE ───
    // -poseGetUpSitPlant 1: after the step hands off and BEFORE any transfer,
    // the legs drive a KNEE-EXTENSION REACH from the captured fold (unfold
    // the knee, extend the hip, plantar the ankle) seeking DECK CONTACT. The
    // fz contact gate holds the extension until the soles read the deck (or
    // the reach's budget spends); the ext then starts over the planted base.
    // All default off = the r53 bytes.
    private float getUpSitPlant;              // -poseGetUpSitPlant: 1 on, 0 off
    private bool getUpSitPlantOn;
    private float getUpSitPlantKnee = 110f;   // the knee's unfold delta, deg
    private float getUpSitPlantHip = 25f;     // the hip's extension delta, deg
    private float getUpSitPlantAnkle = 25f;   // the ankle's plantar delta, deg
    private float getUpSitPlantSec = 1.2f;    // the reach's travel ramp, s
    private float getUpSitPlantFz = 0.05f;    // the contact threshold, fraction of W (summed)
    private float getUpSitPlantFzSec = 0.10f; // the contact must hold this long, s
    private float getUpSitPlantMax = 2.0f;    // the reach's budget, s
    private float sitPlantT;                  // the stage's clock, s
    private bool sitPlantDone;                // the hand-off happened
    private bool sitPlantEntered;             // one enter line per attempt
    private float sitPlantFrac;               // the deltas' fraction
    private float sitPlantContactT;           // the contact clock, s
    private float sitPlantFzMax;              // the stage's peak sole load, N
    // ROUND 47: THE CONTINUOUS ARC (the r46 consults' single change). The r46
    // d4 specimen ran the extension from a FLAT exit and still sank 1.0 m -
    // the dump is the extension law itself. The arc replaces the extension's
    // raw time share with its own clock, gated by the torso pitch-rate, and
    // ties the torso ask to the PELVIS HEIGHT. All default off = r46 bytes.
    private float getUpSitArcSec;          // -poseGetUpSitArc: full arc duration, s (0 off)
    private float getUpSitArcTorso = 40f;  // -poseGetUpSitArcTorso: base torso allowance, deg
    private float getUpSitArcRate = 40f;   // -poseGetUpSitArcRate: pitch-rate gate, deg/s (0 off)
    // ROUND 48: THE TIE ARMED EXT-ONLY. The r47 damage: the height-tie was
    // live from the attempt's FIRST frame; its only live diff during the
    // press held the trunk at the base lean, the palms missed the deck
    // (press-cap gL=gR=0.000 -> gate-dead -> the pre-ext collapse). 0 = the
    // r47 route (from the first frame); 1 = armed only once the extension is
    // live (the pull completed); 2 = the tie off (historic trunk asks all
    // the way through - the "drop the tie" dose).
    private float getUpSitArcTieExt;
    private float getUpSitArcClock;        // the arc's own clock, s (rate-gated)
    private float getUpSitArcHeld;         // frozen time, s (the trace)
    private int getUpSitArcHolds;          // hold episodes, this attempt
    private bool getUpSitArcInHold;
    private float getUpSitArcPrevTilt;     // the rate's datum
    private float getUpSitArcTiltRate;     // the EMA rate, deg/s (the trace)
    private float getUpSitArcHShare;       // the height share (the trace)
    private float getUpSitRockSec;         // CYCLE 106: -poseGetUpSitRock: the torso's own
                                           // rock, s (0 = the r47 tie byte for byte)
    private float getUpSitRockGate;        // CYCLE 106: -poseGetUpSitRockGate: hold the ext
                                           // clock until the rock completes (0 = off)
    private float getUpSitRockClock;       // the rock's own clock, s (reset at the fire)
    // CYCLE 107: THE CO-OVER GATE (the c106 named-behind: "hold the lift").
    // Armed, the ext's clocks - the arc's (SitArcKey) and the seat's (seatExtT)
    // - FREEZE while the CoM is still behind the feet (a foot ahead of the
    // CoM's x by more than G): the fold is held (the heels stay tucked, the
    // feet do not shoot back out) while the torso's rock works the body
    // forward over the tuck; the extension unfolds only once the CoM is over
    // the feet (within the band). Never-over is bounded: the attempt ends on
    // its own rise bound. 0 = off, every historic route byte for byte.
    private float getUpSitExtOver;         // -poseGetUpSitExtOver: CoM-over-feet band, m
    private float getUpSitArcBasePelvY = -999f;  // the fire's pelvis datum
    private float getUpTracePelvVel;       // the trace's pelvisVelY witness
    private float getUpTrunkGovBias;
    private float getUpTrunkGovCap;        // ROUND 42b: the counter-lean cap, deg (0 = uncapped)
    private float getUpRiseStartPelvisY = -999f;   // the Rise's own pelvis datum
    private float rigWeightCacheN;         // the human's weight, N (cached)
    // ROUND 36 (probe): the hip-channel burst window, s (To <= From = off).
    private float hipDbgFrom;
    private float hipDbgTo;
    // ROUND 36b: the get-up hip-owner gate (0 = the historic two-writer pair).
    private float getUpHipOwner;
    // ROUND 37: THE SIGN-AWARE HIP ARBITRATION (the r36 NEXT-1b). The r36 probe
    // proved the get-up hip channel is a two-writer cancellation loop: the leg
    // drive (saturated, toward the pose in force) and the pelvis law (saturated,
    // the OPPOSITE sign) write the same muscles every frame with equal
    // 0.1-per-frame MoveTowards steps; the physics step only ever sees the
    // pelvis law's own step (~-8.7 N*m by stage) and the leg drive's demand
    // never lands. The log's own criterion for the division: the two demands
    // are ANTI-PHASE exactly when their signals carry opposite signs. With
    // -poseGetUpHipArb 1 a side's pelvis-law hip write is SKIPPED while THIS
    // frame's leg-drive demand for that side is anti-phase with it (both
    // magnitudes above -poseGetUpHipArbBand), so the leg drive's write stands
    // and its demand lands; in phase (or with no live leg write this frame) the
    // pelvis law applies exactly as before. Mode 2 restricts the arbitration to
    // the Rise stage - the Tuck wait keeps the historic dynamics (the r36
    // lesson: the wait's fire windows depend on them). Default 0 = off.
    private float getUpHipArb;             // 0 off; 1 sign-aware; 2 sign-aware, Rise only
    private float getUpHipArbBand = 0.1f;  // the anti-phase deadband, |signal|
    // ROUND 43d: the sit attempt's own arb band. In the quiet sit the two hip
    // writers cancel exactly as in r36 (the leg demand sits AT the regular
    // band's edge, so the arb never engages mid-window and the blend cannot
    // move the hip); a sit attempt may therefore carry its own band. <0 = the
    // regular band (byte for byte the r43c route).
    private float getUpSitStandArbBand = -1f;
    private float legHipSigL, legHipSigR;  // the leg drive's own hip demand, this frame
    private float legHipSigT = -1f;        // the frame stamp of that capture
    private int getUpArbSuppL, getUpArbSuppR;        // suppressed writes, this attempt
    private int getUpArbSuppLTot, getUpArbSuppRTot;  // suppressed writes, this take
    private bool getUpArbFirstL, getUpArbFirstR;     // one log line per side, per attempt

    // ─── ROUND 36b: THE HIP-OWNER GATE ───
    // The r36 probe (tiltA_r36_p1) proved the get-up hip channel is a two-writer
    // cancellation loop: the stand-blend leg drive (-3.0, saturated, toward the
    // stand pose) and the pelvis law (+1.0, saturated, the opposite sign) write
    // the same muscles every frame; equal 0.1-per-frame MoveTowards steps cancel
    // EXACTLY, the pelvis law (last writer) sets the physics input, and the
    // delivered torque is its own single step (~-8.7 N*m) - neither law's intent
    // ever lands. This gate gives the hip channel ONE owner inside the get-up
    // ladder: the leg drive. Default 0 = the historical pair (byte-identical).
    private void DriveHipsGetUp(float pelvisTilt, float pelvisAngVel)
    {
        if (getUpHipOwner > 0.5f) return;   // the get-up leg drive owns the hips
        if (getUpHipArb > 0.5f)
        {
            DriveHipsArb(pelvisTilt, pelvisAngVel);
            return;
        }
        DriveHips(pelvisTilt, pelvisAngVel, 0f, true, true, 0f, false);
    }

    // ROUND 37: the arbitration's own pelvis-law drive: exactly DriveHips'
    // arithmetic (targetTilt 0, no hold mix - the call site's own arguments),
    // except that a side whose leg-drive demand THIS frame is anti-phase with
    // the pelvis signal (and both clear the band) is not written at all - the
    // leg drive's state stands and its demand reaches the physics step.
    private void DriveHipsArb(float pelvisTilt, float pelvisAngVel)
    {
        bool live = legHipSigT == Time.fixedTime;
        if (getUpHipArb > 1.5f && getUpStage != GetUpStage.Rise) live = false;
        float error = Mathf.DeltaAngle(pelvisTilt, 0f)
            / Mathf.Max(1f, balance.pelvisErrorReferenceDegrees);
        float speed = pelvisAngVel / Mathf.Max(1f, balance.speedReferenceDegPerSec);
        float signal = Mathf.Clamp(
            balance.pelvisPGain * error - balance.pelvisDGain * speed, -1f, 1f);
        HipDbgWrite("pelvL", signal, 0f, leftHipJoint, balance.leftHipFlexor, balance.leftHipExtensor);
        HipDbgWrite("pelvR", signal, 0f, rightHipJoint, balance.rightHipFlexor, balance.rightHipExtensor);
        bool supL = live && HipArbAntiPhase(legHipSigL, signal);
        bool supR = live && HipArbAntiPhase(legHipSigR, signal);
        if (supL)
        {
            getUpArbSuppL++;
            getUpArbSuppLTot++;
            if (!getUpArbFirstL)
            {
                getUpArbFirstL = true;
                Debug.Log("GETUP-HIPARB t=" + N(Time.fixedTime, 3) + " side=L legSig="
                    + N(legHipSigL, 2) + " pelv=" + N(signal, 2) + " stage=" + getUpStage);
            }
        }
        if (supR)
        {
            getUpArbSuppR++;
            getUpArbSuppRTot++;
            if (!getUpArbFirstR)
            {
                getUpArbFirstR = true;
                Debug.Log("GETUP-HIPARB t=" + N(Time.fixedTime, 3) + " side=R legSig="
                    + N(legHipSigR, 2) + " pelv=" + N(signal, 2) + " stage=" + getUpStage);
            }
        }
        if (!supL) ApplyHipSignal(true, signal);
        if (!supR) ApplyHipSignal(false, signal);
    }

    // The log's own criterion (r36): anti-phase = opposite signs, both salient.
    private bool HipArbAntiPhase(float legSig, float pelvSig)
    {
        float band = getUpHipArbBand;
        // ROUND 43d: a live sit attempt reads its OWN band (the sit pose's
        // cancellation sits exactly at the regular edge; the device fires the
        // arb inside the sit's loaded window instead of only at the deck move).
        if (getUpSitStandOn && getUpSitActive && getUpSitStandArbBand >= 0f)
            band = Mathf.Clamp(getUpSitStandArbBand, 0f, 1f);
        if (Mathf.Abs(legSig) < band) return false;
        if (Mathf.Abs(pelvSig) < band) return false;
        return (legSig > 0f) != (pelvSig > 0f);
    }
    private float getUpBackSince = -1f;
    private int getUpRollBounds;
    private bool getUpRollBlocked;
    // The settle pose, latched when the get-up starts (the crawl's own rule):
    // the tuck is a DELTA from the pose the body landed in, so the same shape
    // holds on every build.
    private float getUpHipL, getUpHipR, getUpKneeL, getUpKneeR, getUpAnkleL, getUpAnkleR;
    private float getUpTrunkStartDeg;
    // The grip, per side: the held state, the press clock, the anchored slip.
    private readonly Collider2D[] handCollider = new Collider2D[2];
    private readonly bool[] gripHeld = new bool[2];
    private readonly float[] gripUnloadAge = new float[2];
    private readonly Vector2[] gripAnchor = new Vector2[2];
    private readonly float[] gripSlipM = new float[2];
    private readonly float[] gripLoad = new float[2];      // body weights
    private readonly float[] gripFnN = new float[2];
    private readonly float[] gripTfN = new float[2];
    private readonly float[] gripCapN = new float[2];
    private readonly int[] gripEngages = new int[2];
    private readonly int[] gripTears = new int[2];
    private readonly bool[] gripTornThisStep = new bool[2];
    // CYCLE 110 - THE PRESS LATCH (the owner's 02.10 ask: the palms skate on
    // the ground during the get-up; the grab must bite). The measured-load
    // engage alone never fired on the flat (scene_grip2: the palm's own peak
    // normal load read 0.04 body weights): a live press ask - ArmDrive's
    // press request or a running stroke phase - over a real touch engages
    // too. -poseHandGripPress 0 = the pre-110 law bit for bit.
    private bool handGripPressOn = true;           // -poseHandGripPress (default ON)
    private readonly bool[] gripPressWants = new bool[2];
    private readonly bool[] gripPressLogged = new bool[2];
    private ContactFilter2D handGripFilter;
    private bool handGripFilterReady;
    private readonly ContactPoint2D[] handGripContacts = new ContactPoint2D[12];
    // The get-up's own trace (`<label>-getup.csv`): the witnesses the row has no
    // columns for (the palms' load and the material in force, the grip's
    // engage/hold/release, the stage and the card's three quantities with their
    // frame numbers). A SEPARATE file on purpose - the accepted CSV's column
    // count is part of the identity clause this cycle must keep, so the walk
    // row's own columns are untouched by this cycle.
    private System.IO.StreamWriter getUpTrace;
    private readonly System.Text.StringBuilder getUpTraceRow = new System.Text.StringBuilder(320);
    private float getUpTraceFlushAge;

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
    // WALK CYCLE 6x (recorder only): the pose path's own PLACEMENT, the one
    // quantity 6w could not read. All four are values TickWalk ALREADY computes
    // for the step in force - the same read-only rule as the 6s block above: no
    // gain, limit, threshold, sign, branch or target in this file (or in any
    // other) reads one of them back, and none is a second source of a value
    // another column already carries.
    private float poseAskPlaceXM;         // the placeX clamp's own answer, m
    private float poseAskSwingSoleXM;     // the swing foot's sole x, m
    private float poseAskStanceSoleXM;    // the stance foot's sole x, m
    private float poseAskPlaceLeadM;      // placeX - comX at the PLANT frame, m
    // The latch behind the lead column: written at plant frames only and never
    // cleared inside a step, so the column carries the MOST RECENT plant's lead.
    // The per-frame publication above is cleared to the block's 0 sentinel every
    // step (ClearPoseAskRecord), exactly as the 6w height datum is - the same
    // split the height channel already uses (datum vs published request).
    private float placeLeadLatchM;
    // WALK CYCLE 6y (recorder only): THE REACH GUARD'S OWN TWO NUMBERS - the
    // witness the reach guard above never had. Both are per-frame publications of
    // values TickWalk already computes for the step in force, cleared to this
    // block's 0 sentinel every step by ClearPoseAskRecord; the same read-only rule
    // as the 6x block: no gain, limit, threshold, sign, branch or target in this
    // file (or in any other) reads one of them back.
    private float poseAskReachXM;   // the guard's ceiling, m, -1 when inert
    private float poseAskHipXM;     // the hip ANCHOR x the guard measured from, m
    // WALK CYCLE 6z: THE HORIZONTAL CHANNEL AND THE PIVOT'S OWN RESOLVER. The
    // force and the acceleration are per-STEP measurements of the body itself -
    // they are live on every step this component ticks, whether or not the walk
    // drives - while the resolver belongs to this ask block and follows its 0
    // sentinel. Same read-only discipline as every block above: no gain, limit,
    // threshold, sign, branch or target in this file (or in any other) reads one
    // of them back, and none of them is a second source of a value another
    // column already carries.
    private float poseGroundFxN = PoseGroundFxNoMeasureN;  // measured, N, +x = forward
    private float poseComAccelXMps2;                       // d(comVelX)/dt, m/s^2
    private float prevComVelX;                             // the one-step difference's own state
    private bool comAccelSeeded;                           // false until the second tick
    private int poseAskResolvedTerm;                       // 1..5 driving, 0 not driving
    // The meter's own plates and scratch: the same four sole colliders the
    // recorder's load meter reads (TrialRecorder.ResolveSoleColliders), resolved
    // once, by the same names, on this rig's own transform.
    private readonly Collider2D[] groundFxPlate = new Collider2D[4];  // heelL, toeL, heelR, toeR
    private bool groundFxPlatesResolved;
    private ContactFilter2D groundFxFilter;
    private bool groundFxFilterReady;
    private readonly ContactPoint2D[] groundFxContacts = new ContactPoint2D[24];

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
    // WALK CYCLE 6y: the reach guard's own sentinel (capturePoseReachXM). The
    // guard is INERT on a frame whose hip anchor is at or above the leg's span -
    // the reach circle does not meet the floor - and the column says so with a
    // finite, NON-PHYSICAL value instead of a 0 the "pose gait is not driving the
    // walk" sentinel already owns. No reach a real pose can compute is negative
    // (reachXM is a square root), so -1 cannot collide with a measurement.
    public const float PoseReachInertM = -1f;
    // WALK CYCLE 6z: WHICH TERM RESOLVED placeX on this frame
    // (capturePoseResolvedTerm). The codes are the placement path's own branch
    // order - the step band's clamp first, the leg's reach guard last - and every
    // one of them is set WHERE THE VALUE IS DECIDED, never re-derived by a reader
    // from the published columns. They add no branch and no clamp of their own:
    // the same four numbers, the same Clamp call and the same guard branch run
    // exactly as they ran before this block existed.
    public const int PosePlaceTermNotDriving = 0;    // the pose gait is not driving the WALK
    public const int PosePlaceTermLaw = 1;           // the catching law survived the band
    public const int PosePlaceTermBandFloor = 2;     // the band's floor (stanceFootX + stepMinMeters) bound it
    public const int PosePlaceTermBandCeiling = 3;   // the band's ceiling (stanceFootX + stepMaxMeters) bound it
    public const int PosePlaceTermReachGuard = 4;    // the reach guard's clamp + ramp moved the value
    public const int PosePlaceTermReachInert = 5;    // the guard is INERT (hip anchor at or above the leg's span)
    // WALK CYCLE 6z: the force column's own sentinel (capturePoseGroundFxN). It
    // says "the meter could not run" - no sole plate resolved on this rig, or
    // this component was never ticked (-poseDrive 0) - and is a magnitude no
    // horizontal ground force on this body can reach, so it cannot collide with
    // a reading. The row where neither foot carries load publishes exactly 0
    // instead: a reading, distinct from this sentinel by construction.
    public const float PoseGroundFxNoMeasureN = -1000000f;

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
    // ─── WALK CYCLE 6x: WHERE THE POSE PATH ITSELF PUT THE FOOT ───
    // capturePosePlaceXM - the pivot x the library's placement was resolved for
    //    on this frame, m, world: TickWalk's placeX after BOTH of its clamps -
    //    the step band (stanceFootX + stepMinMeters .. stanceFootX + stepMaxMeters
    //    of the step in force) and the leg's own reach from the live hip ANCHOR,
    //    whose ceiling and centre are the 6y columns capturePoseReachXM /
    //    capturePoseHipXM below, ramped in by the CoM's need. It is the value
    //    handed to the library as
    //    PoseLibrary.Inputs.footPlaceX on this frame, i.e. the ASK, not the
    //    achieved position. LIVE, not a per-step datum: the capture point moves
    //    with the CoM every frame. Read it against capturePoseSwingSoleXM - the
    //    two are the ask and the placed sole of the same frame.
    // capturePoseSwingSoleXM / capturePoseStanceSoleXM - where the swing and the
    //    stance foot's SOLE are on this frame, x, m, world. The pose library
    //    holds angles and names no sole or ankle world x, so this is the foot's
    //    own measured position: BodyStateEstimator.left/rightFootPosition, the
    //    centre of the foot plate (FootPlateCenter: the midpoint of the heel
    //    collider's back end and the toe collider's tip). It is the same point
    //    the step clock reads as swingFootX / stanceFootX and the same one the
    //    placement clamp above is measured from - NOT a contact point invented
    //    here. No geometry is added.
    // capturePosePlaceLeadM - THE BRAKE DISTANCE, the number the next cycle's
    //    single change reads: capturePosePlaceXM minus the CoM x, latched at the
    //    step's PLANT frame (the frame the swing foot's sole comes back onto the
    //    floor after its own lift), signed, + = the pivot is ahead of the CoM.
    //    The CoM is this file's own read (state.comPosition.x, the row's comX
    //    column), so the subtraction needs no second source and no new column.
    //    Held from the plant frame until the next plant, so a driving row carries
    //    the lead of the most recent plant. 0 while the pose gait is not driving
    //    the WALK on this step (this block's own sentinel), which is also the
    //    value before the walk's first plant.
    // ─── WALK CYCLE 6y: THE REACH GUARD'S OWN NUMBERS ───
    // capturePoseReachXM - the ceiling the leg's own reach guard applied on this
    //    frame, m: sqrt(leg^2 - hipToSole^2) with leg = thighSize.y + shinSize.y
    //    and hipToSole measured from the HIP ANCHOR - the pelvis centre minus
    //    pelvisSize.y/2, the point SolveLegIk rotates the leg about - down to the
    //    sole's own floor. It is the half width of the box the pivot may land in,
    //    [hip anchor x - reach, hip anchor x + reach], ramped in by the same need.
    //    WALK CYCLE 6y changed the anchor: measured from the PELVIS CENTRE this
    //    quantity was 0 on every walk frame of 6x (hipToSole 0.9122 m against the
    //    0.86 m of thigh + shin), so the guard was clamp(law, hipX, hipX) - the
    //    placement pinned onto the pelvis, and the pull-back 6x measured (the lead
    //    -0.0056 / -0.0467 / -0.0995 / -0.1206 m at the plants) was its shadow.
    //    PoseReachInertM (-1) = THE GUARD IS INERT on this frame: hipToSoleM >=
    //    leg, the reach circle does not meet the floor, the guard applies no clamp
    //    and no ramp and the step band alone bounds placeX. 0 = the pose gait is
    //    not driving the WALK on this step (this block's own sentinel), never NaN.
    //    The guard's own height is recoverable from this column alone:
    //    (hip anchor y - groundY) = footSize.y/2 + sqrt(leg^2 - reachXM^2).
    // capturePoseHipXM - the hip ANCHOR x the guard measured from, m, world: the
    //    pelvis body's own x (this rig hangs the anchor directly below the pelvis
    //    centre, so the anchor shares the centre's x - Human.CreateLeg places the
    //    thigh at (hipX, -pelvisSize.y/2) in pelvis space, and SolveLegIk reads
    //    dx from pelvisBody.position.x). Published unconditionally on a driving
    //    frame, so the row reads on its own which x the reach box was centred on:
    //    the old input widened that box to nothing about this x on all 353 pulled
    //    rows of c6x_walk, i.e. capturePosePlaceXM WAS this column there.
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
    // WALK CYCLE 6x (recorder only): the pose path's own placement - the ask,
    // the two placed soles and the plant-frame lead. See the block comment above.
    public float capturePosePlaceXM => poseAskPlaceXM;
    public float capturePoseSwingSoleXM => poseAskSwingSoleXM;
    public float capturePoseStanceSoleXM => poseAskStanceSoleXM;
    public float capturePosePlaceLeadM => poseAskPlaceLeadM;
    // WALK CYCLE 6y (recorder only): the reach guard's own ceiling and the hip
    // anchor x it was measured from - the number and the centre the guard above
    // applied. See the block comment above for the inert sentinel PoseReachInertM.
    public float capturePoseReachXM => poseAskReachXM;
    public float capturePoseHipXM => poseAskHipXM;
    // WALK CYCLE 6z (recorder only): the horizontal channel and the resolver.
    // capturePoseGroundFxN - the measured horizontal ground reaction the body's
    //    own feet receive this step, N, +x = the walk's forward direction, from
    //    the same solver impulses the recorder's load meter sums vertically
    //    (0 exactly = both feet carry no load; PoseGroundFxNoMeasureN = the
    //    meter could not run; see SampleGroundReactionX for the full statement).
    // capturePoseComAccelXMps2 - d(comVelX)/dt, m/s^2, a one-step finite
    //    difference of the rig's own measured CoM velocity (5 ms at the trial
    //    step; the rig has no smoothed acceleration estimator).
    // capturePoseResolvedTerm - which term resolved placeX this frame: 1 the law
    //    survived the band, 2 the band's floor, 3 the band's ceiling, 4 the reach
    //    guard moved it, 5 the guard is inert; 0 = not driving the walk. See the
    //    codes above.
    public float capturePoseGroundFxN => poseGroundFxN;
    public float capturePoseComAccelXMps2 => poseComAccelXMps2;
    public int capturePoseResolvedTerm => poseAskResolvedTerm;

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
        // WALK CYCLE 6x: the placement block's per-frame publications. The lead's
        // own LATCH (placeLeadLatchM) is deliberately NOT cleared here - it is a
        // per-step quantity and TickWalk republishes it on every driving step.
        poseAskPlaceXM = 0f;
        poseAskSwingSoleXM = 0f;
        poseAskStanceSoleXM = 0f;
        poseAskPlaceLeadM = 0f;
        // WALK CYCLE 6y: the reach guard's two publications, same per-frame rule.
        // 0 here means "the pose gait is not driving the WALK this step"; the
        // guard's own inert state is a POSITIVE-capable column written -1
        // (PoseReachInertM) by the walk branch that took that branch.
        poseAskReachXM = 0f;
        poseAskHipXM = 0f;
        // WALK CYCLE 6z: the resolver's own 0 - the row is not driving the walk
        // (the walk branch that resolves the pivot writes 1..5). Same per-frame
        // rule as the rest of this block.
        poseAskResolvedTerm = PosePlaceTermNotDriving;
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
        // WALK CYCLE 40: the get-up's own collaborators. ArmDrive is the ONE
        // writer of the arm muscles (execution order 10, after this component),
        // so the rise's press is a REQUEST to it (ArmDrive.RequestArmPress), never
        // an arm angle written from here; StepPhaseDriver carries the settled
        // stand CoM, which is the card's "CoM above the standing threshold"
        // (StepPhaseDriver.StandingComY, the standing bar BalanceController's own
        // GetUpComAtStandHeight reads).
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
        // The palms: index 0 = left, 1 = right, the columns' own order. The hand
        // segment's collider is the palm the press loads, and it is the collider
        // the grip material is swapped onto (Human.SetHandGrip).
        handCollider[0] = HandCollider(root, "LeftArmHand");
        handCollider[1] = HandCollider(root, "RightArmHand");

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
        // WALK CYCLE 64: the ask-side tier's switches - the drawn stance knee keys. The
        // band 4..26 is the PROBE's (the hand's) range; the genome's box is narrower
        // (ArenaGenome: the measured ledge) and the two must stay compatible: the box
        // inside the switches, the shipped default inside both.
        stanceFoldKneeDeg = Mathf.Clamp(GetFloat("-poseStanceFoldKnee", stanceFoldKneeDeg), 4f, 26f);
        stancePlaceKneeDeg = Mathf.Clamp(GetFloat("-poseStancePlaceKnee", stancePlaceKneeDeg), 4f, 26f);
        stanceLoadKneeDeg = Mathf.Clamp(GetFloat("-poseStanceLoadKnee", stanceLoadKneeDeg), 4f, 26f);
        swingLandShare = Mathf.Clamp(GetFloat("-poseLandShare", swingLandShare), 0.3f, 0.98f);
        heightGainDegPerMeter = GetFloat("-poseHeightGain", heightGainDegPerMeter);
        heightDampDegPerMeterPerSecond =
            GetFloat("-poseHeightDamp", heightDampDegPerMeterPerSecond);
        // WALK CYCLE 62: the step's vault amplitude, m (bounded - a dose, not a
        // spring; 0 = the landed request, bit for bit).
        bobAmpMeters = Mathf.Clamp(GetFloat("-poseBobAmp", bobAmpMeters), 0f, 0.06f);
        placementMarginMeters = GetFloat("-posePlaceMargin", placementMarginMeters);
        comLeadSwapMeters = Mathf.Max(0.01f, GetFloat("-poseLeadSwap", comLeadSwapMeters));
        // CYCLE 115: the settle step (see the field block / UpdateWalkSettle).
        // Budget seconds, 0 = OFF - the landed walk-and-stop path, bit for bit.
        walkSettleBudget = Mathf.Max(0f, GetFloat("-poseWalkSettle", 6f));    // CYCLE 123 landed: armed by default (6 s); -poseWalkSettle 0 = off
        walkSettleMps = Mathf.Max(0.01f, GetFloat("-poseWalkSettleMps", walkSettleMps));
        walkSettleCatch = Mathf.Clamp(GetFloat("-poseWalkSettleCatch", walkSettleCatch), 0.5f, 1.6f);
        walkSettleLeadM = Mathf.Clamp(GetFloat("-poseWalkSettleLead", walkSettleLeadM), -0.15f, 0.25f);
        walkSettleThreatM = Mathf.Max(0f, GetFloat("-poseWalkSettleThreat", 0f));
        walkSettlePush = Mathf.Clamp01(GetFloat("-poseWalkSettlePush", walkSettlePush));
        walkSettleBendDeg = Mathf.Clamp(GetFloat("-poseWalkSettleBend", walkSettleBendDeg), 0f, 60f);
        walkSettleBendLoad = Mathf.Max(0f, GetFloat("-poseWalkSettleBendLoad", walkSettleBendLoad));
        walkSettleBendBar = Mathf.Clamp(GetFloat("-poseWalkSettleBendBar", walkSettleBendBar), 0f, 3f);
        walkSettleBendCap = Mathf.Clamp(GetFloat("-poseWalkSettleBendCap", walkSettleBendCap), 0f, 80f);
        walkSettleHoldDeg = Mathf.Clamp(GetFloat("-poseWalkSettleHold", walkSettleHoldDeg), 0f, 60f);
        walkSettleHoldMps = Mathf.Max(0.01f, GetFloat("-poseWalkSettleHoldMps", walkSettleHoldMps));
        walkSettleHoldQuiet = Mathf.Max(0f, GetFloat("-poseWalkSettleHoldQuiet", walkSettleHoldQuiet));
        walkSettleHoldMax = Mathf.Max(0f, GetFloat("-poseWalkSettleHoldMax", walkSettleHoldMax));
        walkSettleExitMps = Mathf.Max(0f, GetFloat("-poseWalkSettleExit", walkSettleExitMps));
        walkSettleExitQuiet = Mathf.Max(0f, GetFloat("-poseWalkSettleExitQuiet", walkSettleExitQuiet));
        walkSettleExitMargin = GetFloat("-poseWalkSettleExitMargin", walkSettleExitMargin);
        walkSettleGuardWin = Mathf.Max(0f, GetFloat("-poseWalkSettleGuard", walkSettleGuardWin));
        walkSettleHandOn = Mathf.RoundToInt(GetFloat("-poseWalkSettleHand", 0f)) != 0;
        walkSettleBlendS = Mathf.Clamp(GetFloat("-poseWalkSettleBlend", walkSettleBlendS), 0f, 3f);
        // WALK CYCLE 75/76: the descent-arithmetic gate (default 0 = the landed path).
        placeKneeLive = Mathf.RoundToInt(GetFloat("-posePlaceKneeLive", 0f)) != 0;
        stepMinMeters = GetFloat("-poseStepMin", stepMinMeters);
        stepMaxMeters = Mathf.Max(stepMinMeters + 0.01f, GetFloat("-poseStepMax", stepMaxMeters));
        standCaptureSeconds = GetFloat("-poseStandCap", standCaptureSeconds);
        armSwingDeg = GetFloat("-poseArmSwing", armSwingDeg);
        // WALK CYCLE 37: the ankle's own carry (default 0 = the A7 path). Signed:
        // the rig's own convention (plus = toe down / heel rise; minus = toe up);
        // bounded, the stance's own band's order, so a dose stays a reading.
        swingAnkleDeg = Mathf.Clamp(GetFloat("-poseAnkleCarry", swingAnkleDeg), -20f, 20f);
        swingAnkleRampSeconds = Mathf.Max(0f, GetFloat("-poseAnkleRamp", swingAnkleRampSeconds));
        // WALK CYCLE 93: the stand's flat-sole toe hold (see the field block above).
        // scale <= 1 keeps the pre-93 law; the three thresholds ride their own family.
        standToeHoldScale = Mathf.Max(0f, GetFloat("-standToeHold", standToeHoldScale));
        standToeHoldBiasDeg = GetFloat("-standToeBias", standToeHoldBiasDeg);
        standToeFlatArmDeg = Mathf.Max(0f, GetFloat("-standToeFlatArm", standToeFlatArmDeg));
        standToeFlatDropDeg = Mathf.Max(standToeFlatArmDeg,
            GetFloat("-standToeFlatDrop", standToeFlatDropDeg));
        standToeFlatHoldSeconds = Mathf.Max(0f,
            GetFloat("-standToeFlatHold", standToeFlatHoldSeconds));
        // WALK CYCLE 94: the squat gate (see the field block) - the hold sleeps for
        // takes that block it (intent.toeHoldBlock) or run the squat machine
        // (intent.squatReps > 0). Default ON.
        standToeSquatGate = GetFloat("-standToeSquatGate", standToeSquatGate);
        // WALK CYCLE 73: the push-off's own delivery, THE FLICK'S OWN GEOMETRY
        // (default 1 = off, the landed path bit for bit). 1..20; the stance
        // ankle's P demand is multiplied toward this value on the loaded roll
        // window [cross+0.145, cross+0.255].
        pushDeliverP = Mathf.Clamp(GetFloat("-posePushDeliverP", pushDeliverP), 1f, 20f);
        // WALK CYCLE 38: the next step's own tail - the gate's own load bound on the
        // step that follows a flight tail. 50 = the landed bound (off, byte-kept);
        // bounded below by the landed bound and above by the row's own collapse band.
        poseNextTailLoadN = Mathf.Clamp(GetFloat("-poseNextTailLoadN", poseNextTailLoadN),
            PoseLibrary.SupportHoldLoadN, 800f);
        // WALK CYCLE 35: the plant's own clock - seconds the incoming support's
        // load read must hold at or above the swap gate before the fire may read
        // the plant (0 = off, the A7 path byte for byte). One-sided and bounded to
        // a fifth of a step: a negative hold is not a reading, and past this the
        // step the fire belongs to is over.
        poseTailHoldSeconds = Mathf.Clamp(GetFloat("-poseTailHold", poseTailHoldSeconds), 0f, 0.10f);
        ankleBalanceP = GetFloat("-poseAnkleP", ankleBalanceP);
        ankleBalanceD = GetFloat("-poseAnkleD", ankleBalanceD);
        // KICK CYCLE k2: the support-side shift's two terms (0 = off, the k1
        // envelope; see the field block for the law). Signed on purpose: a run
        // reads the term in both directions.
        kickShiftGainDeg = GetFloat("-kickShiftGain", kickShiftGainDeg);
        kickSupportHold = Mathf.Clamp01(GetFloat("-kickHoldStiff", kickSupportHold));
        kickShiftTorsoDeg = GetFloat("-kickShiftTorso", kickShiftTorsoDeg);
        kickShiftComMeters = GetFloat("-kickShiftCom", kickShiftComMeters);
        // KICK CYCLE k4: THE WIND-UP's static CoM-loop offset while the strike's
        // level is up (m, the loop's own sign: larger = a stronger backward
        // command). 0 = off.
        kickWindComMeters = GetFloat("-kickWindCom", kickWindComMeters);
        // KICK CYCLE k6: THE WIND-UP'S TERM 1, AS A BOUNDED GAIN - the loop's
        // own channel re-scale while the inter-stroke window is up and its
        // reference migrated to the standing foot's centre (see the field
        // block; 0 = the pre-k6 arithmetic, bit for bit).
        kickWindGain = Mathf.Clamp01(GetFloat("-kickWindGain", kickWindGain));
        // KICK CYCLE k7: THE TERM'S OWN REFERENCE, LATCHED - 0 (default) = the
        // k6 live read of the standing foot's plate centre, 1 = re-latched at
        // every inter-stroke window's rising edge, 2 = the burst's own frame
        // (latched once, carried across the strokes). See the field block.
        kickWindLatch = Mathf.Clamp(GetFloat("-kickWindLatch", kickWindLatch), 0f, 2f);
        // KICK CYCLE k7: THE RELEASE'S OWN HOLD - seconds the term stays alive
        // after the request's level has reached 0 (0 = the k6 release, bit for
        // bit). See the field block.
        kickWindHold = Mathf.Clamp(GetFloat("-kickWindHold", kickWindHold), 0f, 30f);
        // KICK CYCLE k9: THE TERM'S OWN REFERENCE - 0 (default) = the standing
        // foot's plate centre (the k6/k7 delivery, bit for bit), 1 = the support
        // hull's own centre (see the field block; read on the window's own rows).
        kickWindRef = Mathf.Clamp01(GetFloat("-kickWindRef", kickWindRef));
        // KICK CYCLE k11: THE TERM'S OWN NORMALIZER OVER TIME - the bounded DOSE
        // the term's P channel carries AT the error the window itself carries
        // (the per-window normalizer; see the field block). 0 (default) = off,
        // the k6 arithmetic bit for bit.
        kickWindNorm = Mathf.Clamp01(GetFloat("-kickWindNorm", kickWindNorm));
        // KICK CYCLE k14: THE WINDOW'S OWN LEVEL WHILE A FIRE IS DUE AND HELD -
        // -1 (default) = off (the k6/k11 arithmetic bit for bit), 0..1 = the level
        // the window carries while balance.kickFireHeld is up (see the field
        // block: the two ends are the loop's own law and the k6 expression at its
        // ceiling). The sentinel is preserved: Clamp01(-1) is 0 - a MEANINGFUL
        // rung - so the parse clamps only the values above the sentinel.
        float dueLevel = GetFloat("-kickWindDue", kickWindDue);
        kickWindDue = dueLevel < 0f ? -1f : Mathf.Clamp01(dueLevel);
        // KICK CYCLE k15: THE STROKE'S OWN HEAD - -1 (default) = off (the
        // k6/k11/k14 arithmetic bit for bit), 0..1 = the term's level while
        // balance.kickHeadActive is up (the stroke's lift, the unload window:
        // see the field block). The sentinel is preserved exactly as the k14
        // knob's (Clamp01(-1) is 0, a meaningful rung).
        float headLevel = GetFloat("-kickWindHead", kickWindHead);
        kickWindHead = headLevel < 0f ? -1f : Mathf.Clamp01(headLevel);
        // KICK CYCLE k16: THE PARKED STAND'S OWN MARGIN - while the inter-stroke
        // window is up the term's reference moves AWAY FROM THE EDGE (backward
        // along the travel axis) by this many metres, so the same bounded gain
        // reads a deeper error where the stand parks too close to the standing
        // foot's front edge. 0 (default) = off, the k6 arithmetic bit for bit;
        // bounded to [0, 0.10] m = the loop's own comOffsetReference (see the
        // field block; window-only).
        kickWindMargin = Mathf.Clamp(GetFloat("-kickWindMargin", kickWindMargin), 0f, 0.10f);
        // KICK CYCLE k17: THE HEAD'S OWN AUTHORITY - -1 (default) = off (the
        // pre-k17 arithmetic bit for bit), 0..1 = the cap on the head's own
        // flexor-side command while balance.kickHeadActive is up (the stroke's
        // lift, the unload window: see the field block; one-sided - the brake
        // side is untouched). The sentinel is preserved exactly as the k14/k15
        // knobs' (Clamp01(-1) is 0, a meaningful rung - no push at the head).
        float headPushLevel = GetFloat("-kickHeadPush", kickHeadPush);
        kickHeadPush = headPushLevel < 0f ? -1f : Mathf.Clamp01(headPushLevel);
        // KICK CYCLE k24: THE STROKE'S OWN FLIGHT - -1 (default) = off (the
        // pre-k24 arithmetic bit for bit), 0..1 = the term's own reference and
        // level while balance.kickStrokeActive is up and the head is past (the
        // one-legged second and the landing's own tail: see the field block).
        // The sentinel is preserved exactly as the k14/k15/k17 knobs'
        // (Clamp01(-1) is 0, a meaningful rung - the pre-k6 law).
        float flightLevel = GetFloat("-kickWindFlight", kickWindFlight);
        kickWindFlight = flightLevel < 0f ? -1f : Mathf.Clamp01(flightLevel);
        // KICK CYCLE k3: THE SUPPORT'S PLANT - 1 (default) arms the support
        // hip's own hold at the angle latched on the fire step (inside the
        // stand's hipHoldBand, the quiet stance's own mechanism); 0 = the k2
        // delivery, the hip under the free pelvis law.
        kickPlantHold = Mathf.Clamp01(GetFloat("-kickPlantHold", kickPlantHold));
        // KICK CYCLE k19: THE PLANT'S OWN BAND - the band the support hip's hold
        // reads while the strike's plant is on (deg; see the field block). -1
        // (default) = off (balance.hipHoldBand, the landed arithmetic bit for
        // bit). The sentinel is preserved: 0 is a MEANINGFUL rung (MixHipHold's
        // own band guard turns the hold off).
        float plantBandDeg = GetFloat("-kickPlantBand", kickPlantBand);
        kickPlantBand = plantBandDeg < 0f ? -1f : plantBandDeg;
        // WALK CYCLE 60: the walk's neck/head hold inertia share (see the field's note).
        walkNeckHoldShare = Mathf.Clamp(GetFloat("-poseNeckHoldShare", walkNeckHoldShare), 0f, 1f);
        // WALK CYCLE 40: the get-up family and the grip family, both DEFAULT 0 -
        // unset, nothing below this line can run and the Fallen branch is the
        // landed block verbatim (the identity anchor the cycle is judged on).
        getUpEnabled = Mathf.RoundToInt(GetFloat("-poseGetUp", 0f)) != 0;
        getUpPatience = Mathf.Clamp(GetFloat("-poseGetUpPatience", 1f), 0.25f, 8f);
        handGripEnabled = Mathf.RoundToInt(GetFloat("-poseHandGrip", 0f)) != 0;
        handGripHoldSeconds = Mathf.Clamp(GetFloat("-poseHandGripHold", handGripHoldSeconds),
            0f, 2f);
        handGripPressOn = Mathf.RoundToInt(GetFloat("-poseHandGripPress", 1f)) != 0;
        // CYCLE 111: the tear budget (m). 0.03 = the dog's claw default; a dose
        // widens the anchored slip a live grip survives before it tears.
        handGripSlipMeters = Mathf.Clamp(GetFloat("-poseHandGripSlip", handGripSlipMeters),
            0.005f, 0.5f);
        if (handGripEnabled)
            Debug.Log("C111-SLIP budget=" + handGripSlipMeters + " press=" + handGripPressOn);
        getUpStandTiltDeg = Mathf.Clamp(GetFloat("-poseGetUpTilt", getUpStandTiltDeg),
            -45f, -5f);
        // ROUND 18: the roll family, DEFAULT ON inside the get-up family (the
        // consult's "on-back -> roll to side first"). A take without the pose core
        // never reads it; a -poseGetUpRoll 0 take is the old ladder exactly.
        // CYCLE 104: THE FLAT ROLL GATE. The roll/flip re-entry is the DECK's
        // route (the body tossed back onto its back mid-ladder); on flat ground
        // the sit route is the route - c103 measured on the flat squat take:
        // roll on = the flip loop starves the seat machinery (86 enters, 0 SIT
        // events, 1 attempt); roll off = the sit route runs (27 SITSTAND cycles
        // / 28 attempts, 8bceee37). The default now tracks the scene: a
        // -tiltPlatform take keeps the roll ON (its old bytes bit for bit);
        // a flat take defaults OFF. An explicit -poseGetUpRoll 1/0 still wins
        // either way - every old explicit row is one flag away, byte for byte.
        // The orient core is untouched: this is the roll gate alone.
        float rollArg = GetFloat("-poseGetUpRoll", -1f);
        bool rollExplicit = rollArg >= 0f;
        bool onTiltDeck = GetFloat("-tiltPlatform", 0f) > 0.5f;
        getUpRollEnabled = rollExplicit ? Mathf.RoundToInt(rollArg) != 0 : onTiltDeck;
        if (getUpEnabled)
            Debug.Log("C104-ROLLGATE deck=" + onTiltDeck + " set=" + rollExplicit
                + " roll=" + getUpRollEnabled);
        getUpRollForce = Mathf.Clamp01(GetFloat("-poseGetUpRollForce", 0f));
        getUpRollDwell = Mathf.Clamp(GetFloat("-poseGetUpRollDwell", 0.35f), 0f, 3f);
        getUpRollCap = Mathf.Max(1, Mathf.RoundToInt(GetFloat("-poseGetUpRollCap", 2f)));
        // ROUND 29: the stand family, DEFAULT 0 - unset, the rise keeps the
        // landed unwind byte for byte. The pose defaults are the standing
        // record's own settled numbers (the r28 report's -5 / +7 / +7).
        getUpStandMode = Mathf.RoundToInt(GetFloat("-poseGetUpStand", 0f)) != 0;
        getUpStandHold = Mathf.Clamp(GetFloat("-poseGetUpStandHold", 0.5f), 0f, 0.9f);
        getUpStandHipDeg = Mathf.Clamp(GetFloat("-poseGetUpStandHip", -5f), -60f, 60f);
        getUpStandKneeDeg = Mathf.Clamp(GetFloat("-poseGetUpStandKnee", 7f), -20f, 90f);
        getUpStandAnkleDeg = Mathf.Clamp(GetFloat("-poseGetUpStandAnkle", 7f), -45f, 45f);
        getUpStandFrom = Mathf.Clamp(Mathf.RoundToInt(GetFloat("-poseGetUpStandFrom", 0f)), 0, 1);
        // ROUND 30: the contact gate, DEFAULT 0 - unset, the Rise stage reads
        // the r29 route byte for byte (riseClock == stageAge).
        getUpStandGate = Mathf.RoundToInt(GetFloat("-poseGetUpStandGate", 0f)) != 0;
        getUpStandGateMax = Mathf.Clamp(GetFloat("-poseGetUpStandGateMax", 3f), 0f, 30f);
        // ROUND 31: the base gate, DEFAULT 0 - unset, the r29/r30 route runs
        // byte for byte (no fast-path, no wait, no press hold).
        getUpBaseOn = Mathf.RoundToInt(GetFloat("-poseGetUpBase", 0f)) != 0;
        getUpBaseMax = Mathf.Clamp(GetFloat("-poseGetUpBaseMax", 3f), 0f, 30f);
        getUpBasePress = Mathf.Clamp01(GetFloat("-poseGetUpBasePress", 0f));
        getUpWaitPress = Mathf.Clamp(GetFloat("-poseGetUpWaitPress", -1f), -1f, 1f);
        getUpWaitPressSec = Mathf.Clamp(GetFloat("-poseGetUpWaitPressSec", 1.2f), 0.05f, 10f);
        getUpBaseDraw = Mathf.Clamp01(GetFloat("-poseGetUpBaseDraw", 1f));
        getUpBaseRise = Mathf.RoundToInt(GetFloat("-poseGetUpBaseRise", 0f)) != 0;
        // ROUND 32: the settle gate, DEFAULT 0 - unset, the fastpath fires on
        // the bare state, exactly the r31 bytes.
        getUpBaseSettleSec = Mathf.Clamp(GetFloat("-poseGetUpBaseSettle", 0f), 0f, 5f);
        getUpBaseSettleVel = Mathf.Clamp(GetFloat("-poseGetUpBaseSettleVel", 0.6f), 0.02f, 5f);
        // ROUND 33: the anti-flicker forms, DEFAULT 0 - unset, the r32 route runs
        // byte for byte. Drop > 0 = the debounce tolerance; Duty W > 0 = the duty form.
        getUpBaseSettleDrop = Mathf.Clamp(GetFloat("-poseGetUpBaseSettleDrop", 0f), 0f, 1f);
        getUpBaseSettleDutyWin = Mathf.Clamp(GetFloat("-poseGetUpBaseSettleDuty", 0f), 0f, 1f);
        getUpBaseSettleDutyFrac = Mathf.Clamp(GetFloat("-poseGetUpBaseSettleDutyFrac", 0.6f), 0.05f, 1f);
        getUpBaseSettleOn = getUpBaseSettleSec > 0f || getUpBaseSettleDutyWin > 0f;
        // ROUND 35: the load gate / sit-back / hip-first family. All default 0 -
        // unset, the r31-r34 routes run byte for byte.
        getUpBaseLoadFrac = Mathf.Max(0f, GetFloat("-poseGetUpBaseLoad", 0f));
        getUpBaseLoadSec = Mathf.Clamp(GetFloat("-poseGetUpBaseLoadSec", 0.1f), 0f, 5f);
        getUpBaseLoadDrop = Mathf.Clamp(GetFloat("-poseGetUpBaseLoadDrop", 0f), 0f, 1f);
        getUpLoadExtOn = Mathf.RoundToInt(GetFloat("-poseGetUpLoadExt", 0f)) != 0;
        getUpLoadExtKeep = Mathf.Clamp(GetFloat("-poseGetUpLoadExtKeep", 0.04f), 0f, 1f);
        getUpLoadExtFire = Mathf.Clamp(GetFloat("-poseGetUpLoadExtFire", 0.10f), 0f, 2f);
        getUpLoadExtSec = Mathf.Clamp(GetFloat("-poseGetUpLoadExtSec", 0.3f), 0f, 5f);
        getUpLoadExtDrop = Mathf.Clamp(GetFloat("-poseGetUpLoadExtDrop", 0.05f), 0f, 1f);
        getUpLoadExtRelatch = Mathf.RoundToInt(GetFloat("-poseGetUpLoadExtRelatch", 0f)) != 0;
        getUpBaseSquatDeg = Mathf.Clamp(GetFloat("-poseGetUpBaseSquat", 0f), 0f, 60f);
        getUpStandHipFirst = Mathf.Clamp(GetFloat("-poseGetUpStandHipFirst", 0f), 0f, 0.9f);
        getUpStandKneeClimb = Mathf.Clamp(GetFloat("-poseGetUpStandKneeClimb", 0f), 0f, 3f);
        getUpAutoDwell = Mathf.Clamp(GetFloat("-autoGetUp", 0f), 0f, 30f);
        getUpStandHipStart = Mathf.Clamp(GetFloat("-poseGetUpStandHipStart", 0f), 0f, 0.95f);
        getUpStandKneeStart = Mathf.Clamp(GetFloat("-poseGetUpStandKneeStart", -1f), -1f, 0.95f);
        getUpStandAnkleStart = Mathf.Clamp(GetFloat("-poseGetUpStandAnkleStart", -1f), -1f, 0.95f);
        getUpMoveGuardOn = Mathf.RoundToInt(GetFloat("-poseGetUpMoveGuard", 0f)) != 0;
        getUpMoveGuardMax = Mathf.Clamp(GetFloat("-poseGetUpMoveGuardMax", 3f), 0f, 30f);
        getUpTrunkGovMode = Mathf.Clamp(Mathf.RoundToInt(GetFloat("-poseGetUpTrunkGov", 0f)), 0, 3);
        getUpTrunkGovDeg = Mathf.Clamp(GetFloat("-poseGetUpTrunkGovDeg", 40f), 5f, 160f);
        getUpTrunkGovK = Mathf.Clamp(GetFloat("-poseGetUpTrunkGovK", 0.5f), 0f, 3f);
        getUpTrunkGovMax = Mathf.Clamp(GetFloat("-poseGetUpTrunkGovMax", 2f), 0f, 30f);
        getUpTrunkGovCap = Mathf.Clamp(GetFloat("-poseGetUpTrunkGovCap", 0f), 0f, 180f);
        getUpPushTraceOn = Mathf.RoundToInt(GetFloat("-poseGetUpPushTrace", 0f)) != 0;
        getUpPushTraceSec = Mathf.Clamp(GetFloat("-poseGetUpPushTraceSec", 2.5f), 0.1f, 10f);
        getUpStandAnkleFast = Mathf.Clamp(Mathf.RoundToInt(GetFloat("-poseGetUpStandAnkleFast", 0f)), 0, 2);
        // ROUND 43: the sit-stand conversion, DEFAULT 0 - unset, the window
        // helper returns false and every r42 route runs byte for byte.
        getUpSitStandOn = Mathf.RoundToInt(GetFloat("-poseGetUpSitStand", 0f)) != 0;
        getUpSitStandHold = Mathf.Clamp(GetFloat("-poseGetUpSitStandHold", 0.10f), 0f, 5f);
        getUpSitStandMax = Mathf.Clamp(GetFloat("-poseGetUpSitStandMax", 6f), 0f, 60f);
        getUpSitStandKeep = Mathf.Clamp(GetFloat("-poseGetUpSitStandKeep", 0.05f), 0f, 2f);
        getUpSitStandVel = Mathf.Clamp(GetFloat("-poseGetUpSitStandVel", 0.50f), 0f, 10f);
        getUpSitStandDropMin = Mathf.Clamp(GetFloat("-poseGetUpSitStandDrop", 0.15f), 0f, 2f);
        getUpSitStandDropMax = Mathf.Clamp(GetFloat("-poseGetUpSitStandDropMax", 0.50f), 0f, 2f);
        // ROUND 36 (probe): the hip-channel burst logger window, s. Default off
        // (To <= From): every pre-existing take keeps its bytes and its log.
        hipDbgFrom = GetFloat("-poseHipDbgFrom", 0f);
        hipDbgTo = GetFloat("-poseHipDbgTo", 0f);
        // ROUND 36b: the get-up hip-owner gate (1 = the leg drive owns the hips,
        // the pelvis law's hip write is suppressed; default 0 = the historic pair).
        getUpHipOwner = GetFloat("-poseGetUpHipOwner", 0f);
        // ROUND 37: the sign-aware hip arbitration (see the field block): 1 =
        // anti-phase refusals across the ladder, 2 = Rise stage only; the band
        // is the deadband under which a signal counts as absent. 0 = off.
        getUpHipArb = GetFloat("-poseGetUpHipArb", 0f);
        getUpHipArbBand = Mathf.Clamp(GetFloat("-poseGetUpHipArbBand", 0.1f), 0f, 1f);
        getUpSitStandArbBand = Mathf.Clamp(GetFloat("-poseGetUpSitStandArbBand", -1f), -1f, 1f);
        // ROUND 44: the sit heel-pull deltas (all default 0 = the r43 route byte
        // for byte; the knee is the enabler).
        getUpSitPullKnee = Mathf.Clamp(GetFloat("-poseGetUpSitPullKnee", 0f), 0f, 160f);
        getUpSitPullHip = Mathf.Clamp(GetFloat("-poseGetUpSitPullHip", 0f), 0f, 160f);
        getUpSitPullAnkle = Mathf.Clamp(GetFloat("-poseGetUpSitPullAnkle", 0f), 0f, 90f);
        getUpSitPullSec = Mathf.Clamp(GetFloat("-poseGetUpSitPullSec", 0.7f), 0.05f, 3f);
        getUpSitPullOn = getUpSitPullKnee > 0.01f;
        // ROUND 45: the sit re-seat family (all default 0 = the r44 route).
        getUpSitSeatMode = Mathf.Clamp(Mathf.Round(GetFloat("-poseGetUpSitSeat", 0f)), 0f, 4f);
        getUpSitSeatPress = Mathf.Clamp(GetFloat("-poseGetUpSitSeatPress", 0.9f), 0f, 1f);
        getUpSitSeatPressMin = Mathf.Clamp(GetFloat("-poseGetUpSitSeatPressMin", 0.10f), 0.005f, 1f);
        getUpSitSeatPressMax = Mathf.Clamp(GetFloat("-poseGetUpSitSeatPressMax", 1.2f), 0.1f, 5f);
        getUpSitSeatKeep = Mathf.Clamp(GetFloat("-poseGetUpSitSeatKeep", 0.05f), 0.005f, 2f);
        getUpSitSeatGrace = Mathf.Clamp(GetFloat("-poseGetUpSitSeatGrace", 0.15f), 0f, 1f);
        getUpSitSeatHand = Mathf.Clamp(GetFloat("-poseGetUpSitSeatHand", 0.8f), 0f, 3f);
        getUpSitSeatFrozenMax = Mathf.Clamp(GetFloat("-poseGetUpSitSeatFrozenMax", 2.5f), 0.2f, 8f);
        getUpSitDragGap = Mathf.Clamp(GetFloat("-poseGetUpSitDragGap", 0f), 0f, 2f);
        getUpSitDragUnder = Mathf.Clamp(GetFloat("-poseGetUpSitDragUnder", 0f), 0f, 2f);
        getUpSitDragTip = Mathf.Clamp(GetFloat("-poseGetUpSitDragTip", 0f), 0f, 120f);
        getUpSitArcSec = Mathf.Clamp(GetFloat("-poseGetUpSitArc", 0f), 0f, 60f);
        getUpSitArcTorso = Mathf.Clamp(GetFloat("-poseGetUpSitArcTorso", 40f), 0f, 90f);
        getUpSitArcRate = Mathf.Clamp(GetFloat("-poseGetUpSitArcRate", 40f), 0f, 400f);
        getUpSitArcTieExt = Mathf.Clamp(Mathf.Round(GetFloat("-poseGetUpSitArcTieExt", 0f)), 0f, 2f);
        getUpSitRockSec = Mathf.Clamp(GetFloat("-poseGetUpSitRock", 0f), 0f, 30f);
        getUpSitRockGate = Mathf.Clamp(GetFloat("-poseGetUpSitRockGate", 0f), 0f, 1f);
        getUpSitExtOver = Mathf.Clamp(GetFloat("-poseGetUpSitExtOver", 0f), 0f, 2f);
        getUpSitReseat = Mathf.Clamp(Mathf.Round(GetFloat("-poseGetUpSitReseat", 0f)), 0f, 1f);
        getUpSitReseatGap = Mathf.Clamp(GetFloat("-poseGetUpSitReseatGap", 0.18f), 0f, 1.5f);
        getUpSitReseatKnee = Mathf.Clamp(GetFloat("-poseGetUpSitReseatKnee", 70f), 0f, 160f);
        getUpSitReseatHip = Mathf.Clamp(GetFloat("-poseGetUpSitReseatHip", 0f), -90f, 90f);
        getUpSitReseatAnkle = Mathf.Clamp(GetFloat("-poseGetUpSitReseatAnkle", 0f), -90f, 90f);
        getUpSitReseatSec = Mathf.Clamp(GetFloat("-poseGetUpSitReseatSec", 0.60f), 0.05f, 3f);
        getUpSitReseatMax = Mathf.Clamp(GetFloat("-poseGetUpSitReseatMax", 4.0f), 0.2f, 20f);
        getUpSitReseatLoad = Mathf.Clamp(GetFloat("-poseGetUpSitReseatLoad", 0.05f), 0f, 2f);
        getUpSitReseatLoadSec = Mathf.Clamp(GetFloat("-poseGetUpSitReseatLoadSec", 0.20f), 0f, 3f);
        getUpSitReseatOn = getUpSitReseat > 0.5f;
        // ROUND 51: the discrete step re-seat family (default 0 = the r50 route).
        getUpSitStep = Mathf.Clamp(Mathf.Round(GetFloat("-poseGetUpSitStep", 0f)), 0f, 1f);
        getUpSitStepOn = getUpSitStep > 0.5f;
        getUpSitStepHip = Mathf.Clamp(GetFloat("-poseGetUpSitStepHip", 60f), 0f, 160f);
        getUpSitStepKnee = Mathf.Clamp(GetFloat("-poseGetUpSitStepKnee", 160f), 0f, 200f);
        getUpSitStepAnkle = Mathf.Clamp(GetFloat("-poseGetUpSitStepAnkle", 50f), 0f, 90f);
        getUpSitStepSec = Mathf.Clamp(GetFloat("-poseGetUpSitStepSec", 0.55f), 0.05f, 3f);
        getUpSitStepHold = Mathf.Clamp(GetFloat("-poseGetUpSitStepHold", 0.30f), 0f, 3f);
        getUpSitStepPlant = Mathf.Clamp(GetFloat("-poseGetUpSitStepPlant", 0f), 0f, 1f);
        getUpSitStepGap = Mathf.Clamp(GetFloat("-poseGetUpSitStepGap", 0.15f), 0f, 2f);
        getUpSitStepLoad = Mathf.Clamp(GetFloat("-poseGetUpSitStepLoad", 0.05f), 0.005f, 2f);
        getUpSitStepLoadSec = Mathf.Clamp(GetFloat("-poseGetUpSitStepLoadSec", 0.20f), 0f, 3f);
        getUpSitStepMax = Mathf.Clamp(GetFloat("-poseGetUpSitStepMax", 6.0f), 0.5f, 30f);
        getUpSitStepRounds = Mathf.Clamp(GetFloat("-poseGetUpSitStepRounds", 1f), 1f, 6f);
        getUpSitUnder = Mathf.Clamp(GetFloat("-poseGetUpSitUnder", 0f), 0f, 2f);
        // ROUND 51c: the ext sink arrest family (default 0 = the r51 route).
        getUpSitExtArrest = Mathf.Clamp(Mathf.Round(GetFloat("-poseGetUpSitExtArrest", 0f)), 0f, 1f);
        getUpSitExtArrestOn = getUpSitExtArrest > 0.5f;
        getUpSitExtArrestRate = Mathf.Clamp(GetFloat("-poseGetUpSitExtArrestRate", 40f), 1f, 500f);
        getUpSitExtArrestDrop = Mathf.Clamp(GetFloat("-poseGetUpSitExtArrestDrop", 0.10f), 0.005f, 2f);
        getUpSitExtArrestMax = Mathf.Clamp(GetFloat("-poseGetUpSitExtArrestMax", 2.5f), 0f, 10f);
        // ROUND 52: the post-step weight transfer family (default 0 = the
        // r51 route; the doses are take flags).
        getUpSitShift = Mathf.Clamp(Mathf.Round(GetFloat("-poseGetUpSitShift", 0f)), 0f, 1f);
        getUpSitShiftOn = getUpSitShift > 0.5f;
        getUpSitShiftHip = Mathf.Clamp(GetFloat("-poseGetUpSitShiftHip", 25f), 0f, 120f);
        getUpSitShiftKnee = Mathf.Clamp(GetFloat("-poseGetUpSitShiftKnee", 45f), 0f, 200f);
        getUpSitShiftAnkle = Mathf.Clamp(GetFloat("-poseGetUpSitShiftAnkle", 15f), 0f, 90f);
        getUpSitShiftLean = Mathf.Clamp(GetFloat("-poseGetUpSitShiftLean", 0f), -80f, 80f);
        getUpSitDrawLean = Mathf.Clamp(GetFloat("-poseGetUpSitDrawLean", 0f), 0f, 80f);
        getUpSitDrawSeat = Mathf.Clamp(GetFloat("-poseGetUpSitDrawSeat", 40f), 0f, 120f);
        getUpSitDrawPlant = Mathf.Clamp(GetFloat("-poseGetUpSitDrawPlant", 0f), 0f, 3f);
        getUpSitDrawPlantKnee = Mathf.Clamp(GetFloat("-poseGetUpSitDrawPlantKnee", 45f), 0f, 200f);
        getUpSitDrawPlantHip = Mathf.Clamp(GetFloat("-poseGetUpSitDrawPlantHip", -25f), -120f, 120f);
        getUpSitDrawPlantAnkle = Mathf.Clamp(GetFloat("-poseGetUpSitDrawPlantAnkle", -12f), -90f, 90f);
        getUpSitDrawPlantSec = Mathf.Clamp(GetFloat("-poseGetUpSitDrawPlantSec", 1.0f), 0.05f, 8f);
        getUpSitDrawPlantMax = Mathf.Clamp(GetFloat("-poseGetUpSitDrawPlantMax", 2.0f), 0.1f, 8f);
        getUpSitDrawPlantRe = Mathf.Clamp(GetFloat("-poseGetUpSitDrawPlantRe", 0f), 0f, 1f);
        getUpSitDrawPlantEps = Mathf.Clamp(GetFloat("-poseGetUpSitDrawPlantEps", 3f), 1f, 8f);
        getUpSitShiftSec = Mathf.Clamp(GetFloat("-poseGetUpSitShiftSec", 2f), 0.1f, 10f);
        getUpSitShiftRamp = Mathf.Clamp(GetFloat("-poseGetUpSitShiftRamp", 0.9f), 0.05f, 5f);
        getUpSitShiftLoad = Mathf.Clamp(GetFloat("-poseGetUpSitShiftLoad", 0.15f), 0.005f, 2f);
        getUpSitShiftLoadSec = Mathf.Clamp(GetFloat("-poseGetUpSitShiftLoadSec", 0.25f), 0f, 3f);
        getUpSitShiftMax = Mathf.Clamp(GetFloat("-poseGetUpSitShiftMax", 4f), 0.5f, 15f);
        // ROUND 54: the plant stage family (default 0 = the r53 route; the
        // reach doses are take flags).
        getUpSitPlant = Mathf.Clamp(Mathf.Round(GetFloat("-poseGetUpSitPlant", 0f)), 0f, 1f);
        getUpSitPlantOn = getUpSitPlant > 0.5f;
        getUpSitPlantKnee = Mathf.Clamp(GetFloat("-poseGetUpSitPlantKnee", 110f), 0f, 200f);
        getUpSitPlantHip = Mathf.Clamp(GetFloat("-poseGetUpSitPlantHip", 25f), 0f, 120f);
        getUpSitPlantAnkle = Mathf.Clamp(GetFloat("-poseGetUpSitPlantAnkle", 25f), 0f, 90f);
        getUpSitPlantSec = Mathf.Clamp(GetFloat("-poseGetUpSitPlantSec", 1.2f), 0.1f, 8f);
        getUpSitPlantFz = Mathf.Clamp(GetFloat("-poseGetUpSitPlantFz", 0.05f), 0.005f, 2f);
        getUpSitPlantFzSec = Mathf.Clamp(GetFloat("-poseGetUpSitPlantFzSec", 0.10f), 0f, 3f);
        getUpSitPlantMax = Mathf.Clamp(GetFloat("-poseGetUpSitPlantMax", 2.0f), 0.5f, 15f);
        // WALK CYCLE 45: the fall guard's family, DEFAULT 0 - unset, TickFallGuard
        // never runs and ArmDrive's fallGuardReach keeps its own 0, so every
        // pre-existing command line is the row of record's own bytes. The hold is
        // bounded below by the ladder's own step (a one-frame reading is not a
        // state) and above by the flight time this rig's fall can last; the reach
        // is bounded by the arm's own chord.
        fallGuardEnabled = Mathf.RoundToInt(GetFloat("-poseFallGuard", 0f)) != 0;
        fallGuardHoldSeconds = Mathf.Clamp(GetFloat("-poseFallGuardHold", fallGuardHoldSeconds),
            0.05f, 1.20f);
        fallGuardReachMeters = Mathf.Clamp(GetFloat("-poseFallGuardReach", fallGuardReachMeters),
            0f, 0.60f);
        // CYCLE 100: the get-up's own plant reach (see the field). Bounded by the
        // arm's own chord like the guard's.
        getUpPlantReachMeters = Mathf.Clamp(GetFloat("-poseGetUpReach", getUpPlantReachMeters),
            0f, 0.60f);
    }

    // ─── ARENA-1 genome entry ──────────────────────────────────────────────
    // The parameter-sweep arena writes its search genes through this one call
    // instead of reflecting into the private fields. The clamps are the same ones
    // ReadSwitches applies, so a genome can never leave the box the command-line
    // switches accept. `enable` turns the pose drive on the way -poseDrive 1
    // -poseWalk 1 would: active is the drive, walkEnabled is the gait.
    // WALK CYCLE 64: the 3 ask-side genes (g[13..15], the drawn stance knee keys)
    // are appended - the name of every gene keeps its index.
    public const int ArenaGeneCount = 16;

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
        // WALK CYCLE 64: the ask-side tier - the drawn stance knee keys. The clamps are
        // the probe band (4..26) the switches accept, wider than the genome's box (the
        // measured ledge): a genome can never leave the switches' band.
        stanceFoldKneeDeg = Mathf.Clamp(g[13], 4f, 26f);
        stancePlaceKneeDeg = Mathf.Clamp(g[14], 4f, 26f);
        stanceLoadKneeDeg = Mathf.Clamp(g[15], 4f, 26f);
    }

    // ─── THE PLAYED HUMAN'S KICKSTART (25.09) ───────────────────────────────
    // The arena's sibling discipline: the reference walk's own values through ONE
    // call, never a reflection into the private fields. Values = the accepted line
    // (`F:/hermes-data/walk-landed-line.txt`) in this order: swingT, stanceT, lift,
    // landShare, heightGain, heightDamp, bobAmp, placeMargin, leadSwap, stepMin,
    // stepMax, armSwing, ankleP, ankleD, tailHold, ankleCarry, ankleRamp - and,
    // when the caller carries them (v.Length >= 20, the c79 re-base), the three
    // switches: pushDeliverP, placeKneeLive, neckP. Every clamp is ReadSwitches'
    // own - the two paths accept exactly the same box. WHY: an editor/Play human
    // (GameProcess) has no command line to read, so without this call the pose
    // drive stayed OFF and the played man fell on the level-1 path ("человек не идёт").
    public void ApplyReferenceLine(float[] v)
    {
        if (v == null || v.Length < 17) return;
        active = true;
        walkEnabled = true;
        swingDuration = Mathf.Max(0.05f, v[0]);
        stanceDuration = Mathf.Max(0.05f, v[1]);
        swingLiftMeters = Mathf.Max(0f, v[2]);
        swingLandShare = Mathf.Clamp(v[3], 0.3f, 0.98f);
        heightGainDegPerMeter = v[4];
        heightDampDegPerMeterPerSecond = v[5];
        bobAmpMeters = Mathf.Clamp(v[6], 0f, 0.06f);
        placementMarginMeters = v[7];
        comLeadSwapMeters = Mathf.Max(0.01f, v[8]);
        stepMinMeters = v[9];
        stepMaxMeters = Mathf.Max(stepMinMeters + 0.01f, v[10]);
        armSwingDeg = v[11];
        ankleBalanceP = v[12];
        ankleBalanceD = v[13];
        poseTailHoldSeconds = Mathf.Clamp(v[14], 0f, 0.10f);
        swingAnkleDeg = Mathf.Clamp(v[15], -20f, 20f);
        swingAnkleRampSeconds = Mathf.Max(0f, v[16]);
        // THE THREE SWITCHES OF THE ACCEPTED LINE (the c79 re-base), when the
        // caller carries them (v.Length >= 20): the push-off's delivery, the
        // descent-arithmetic gate and the head's own P. The clamps are
        // ReadSwitches' own; a 17-length line (any older caller) leaves the fields
        // untouched - the shipped inert values - so no existing trial row can
        // move. NaN neckP = the neck keeps its own gain.
        if (v.Length >= 20)
        {
            pushDeliverP = Mathf.Clamp(v[17], 1f, 20f);
            placeKneeLive = Mathf.RoundToInt(v[18]) != 0;
            if (balance != null && !float.IsNaN(v[19]))
                balance.neckPGain = v[19];
        }
    }
    // ─── WALK CYCLE 50: the arena's walk configuration entry ────────────────
    // The sibling of ApplyArenaGenome, and the same discipline: the arena's spawn
    // path carries the checkable walk configuration through ONE call instead of
    // reflecting into the private fields. The three values are the accepted trial
    // line's own (-poseTailHold 0.010 -poseAnkleCarry -14 -poseAnkleRamp 0.09 - the
    // only accepted values with no shipped initialiser equal to them, so a genome
    // cannot carry them), and the clamps are the ones ReadSwitches applies, so the
    // arena can never leave the band the command-line switches accept.
    //
    // Nothing on the trial path calls this, and no trial row can move: the trial's
    // own human still takes these three from ReadSwitches (the command line), and
    // this method is reached only from ArenaRunner.SpawnBody.
    public void ApplyArenaWalkConfig(float ankleCarryDeg, float ankleRampSeconds,
                                     float tailHoldSeconds)
    {
        swingAnkleDeg = Mathf.Clamp(ankleCarryDeg, -20f, 20f);
        swingAnkleRampSeconds = Mathf.Max(0f, ankleRampSeconds);
        poseTailHoldSeconds = Mathf.Clamp(tailHoldSeconds, 0f, 0.10f);
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

        // ═══ WALK CYCLE 6z (recorder only): THE HORIZONTAL CHANNEL ═══
        // Two measurements of the body itself, taken on EVERY physics step this
        // component ticks and BEFORE any branch below can return - a filter that
        // skips steps is a filter with an unknown time constant. Neither is part
        // of the ask block, so neither is cleared by ClearPoseAskRecord; both
        // stop being updated the moment the component stops ticking (-poseDrive
        // 0), and the resolver column says which rows the walk itself drove.
        //
        // 1. THE CoM'S OWN HORIZONTAL ACCELERATION, d(comVelX)/dt: a one-step
        //    finite difference (5 ms at the trial step) of the rig's own measured
        //    CoM velocity, BodyStateEstimator.comVelocity.x - the estimator's
        //    one-step difference of the CoM position, the same series the row's
        //    comVelX column is built from. The rig carries no smoothed
        //    acceleration estimator, so this is the honest maximum; differencing
        //    a difference doubles the high-frequency floor, so it is read as a
        //    trend, not frame by frame. 0 on the very first tick of a run (no
        //    previous sample exists yet), which is not a walk row.
        float stepDt = Time.fixedDeltaTime;
        if (stepDt <= 0f) stepDt = 0.005f;
        poseComAccelXMps2 = comAccelSeeded ? (comVelX - prevComVelX) / stepDt : 0f;
        prevComVelX = comVelX;
        comAccelSeeded = true;
        // 2. THE HORIZONTAL GROUND REACTION the body's own feet receive right
        //    now, N, +x = the walk's forward direction. See SampleGroundReactionX
        //    for the source (the solver's own contact impulses on the four sole
        //    plates - the recorder's load meter read on the OTHER axis) and for
        //    the two sentinels.
        poseGroundFxN = SampleGroundReactionX();

        CaptureStandFrame(tilt, pelvisY, leftDown || rightDown,
            (leftFootY + rightFootY) * 0.5f);
        groundY = standPelvisY - pelvisCenterToSole;

        // WALK CYCLE 45/51: the fall guard is asked BEFORE the down test, so it
        // is live exactly while the body is going over - and the landed block
        // below is handed a body whose arms already took the ground. The pelvis'
        // own descent rate is one of the pair's two terms (cycle 51).
        if (fallGuardEnabled) TickFallGuard(tilt, pelvisY, pelvisVelY);

        // ROUND 43: the sit clock. The dwell is counted HERE, where the stand
        // path still drives the body - the ladder only opens once the window has
        // HELD for -poseGetUpSitStandHold s, so nothing below the gate changes
        // for the frames before the fire. Flag off: the clock is pinned 0 and
        // the gate expression is the historic one.
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
            // ═══ WALK CYCLE 40: -poseGetUp 0 IS THE LANDED BLOCK, VERBATIM ═══
            // The switch defaults to 0, so this branch is the only path every
            // pre-existing command line can take: a body on the slab is
            // Falling/Fallen, the joints relax, and while the walk key is held the
            // afterfall crawl (cycle 28) rocks the legs so ArmDrive's presses have
            // purchase. Nothing in this cycle can move a row that does not pass
            // -poseGetUp 1, and the stance run (-poseDrive 1 alone) never reaches
            // this branch at all.
            if (!getUpEnabled)
            {
                // Honest reporting first: the recorder reads BalanceState, and a
                // body on the slab is Falling whatever the old automaton did with
                // the CoM offset. The get up sequence is stage 3.
                balance.currentState = BalanceController.BalanceState.Falling;
                balance.locomotionState = BalanceController.LocomotionState.Fallen;
                RelaxAll();
                // ═══ WALK CYCLE 28: THE AFTERFALL'S OWN RESUMPTION ═══
                // Cycle 27's finding, measured twice on its own rows: a body that
                // settles on the slab FREEZES. The arm machinery's presses are alive
                // in both rows (26/26 strokes on keep27e) yet every joint of the body
                // reads 0.000x deg per frame for thirty seconds and the CoM never
                // advances again - its max is reached at 9.07 s on keep27c and at
                // 24.33 s on keep27e, and the round's travel floor is paid (2.12..2.23
                // against 3.0) on EVERY build that moved the tail's drive. On the base
                // row the same body CRAWLS (2.23 -> 3.46): from 13.3 s its legs start
                // moving - the left hip folds 7 -> -34 deg, the knee 23 -> 42, the
                // ankle -6.5 -> +31, the pelvis rises 12 cm - and ArmDrive's presses
                // gain traction on the moving body. The resumption is therefore made
                // the LAW's own business instead of the settle's luck: while the body
                // is down and the walk key is held, the law drives the legs through
                // the get up's own first stage (the drawing's: tuck the knees under
                // the body, the heels toward the pelvis) as a slow bounded crawl - a
                // ramp into the tuck, then a slow rock around it - so the body is
                // never a statue with nothing for the presses to find purchase in.
                // Bounded and slow by construction (the deltas are the base row's own
                // measured crawl), the arms are NOT touched (ArmDrive owns them), and
                // the drive is the same one writer per joint the walk uses. Nothing
                // here runs before the fall, so the bar's swap frame, the price
                // window, the drive census and the blip are unchanged by this key.
                if (WalkKeyHeld()) DriveAfterfallCrawl();
                return;
            }
            // ═══ WALK CYCLE 40: THE CARD'S OWN BEHAVIOUR ═══
            // -poseGetUp 1: the body owns the joints through TickGetUp, which
            // names the ladder itself (Falling / Fallen / Getting up) and holds the
            // body until the card's three bars are met or the ladder's own bounds
            // are spent. It is entered while the body is down AND, past the first
            // rise, while the law still owns it - so a half-risen body is not
            // handed to the walk the moment the down test clears.
            TickGetUp(tilt, pelvisTilt, pelvisAngVel, pelvisY, pelvisVelY, comX, comVelX,
                leftDown, rightDown, leftFootX, rightFootX, leftFootY, rightFootY);
            return;
        }
        afterfallCrawlOn = false;
        if (getUpEnabled) EndGetUp();

        bool walkKey = WalkKeyHeld();
        // CYCLE 115: the settle step resolves BEFORE the path choice; off it is
        // a read of two fields and nothing below moves by one bit.
        UpdateWalkSettle(walkKey, tilt, pelvisY, comVelX, leftDown, rightDown);
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

        // CYCLE 115: while the settle owns the walk the gait keeps the joints
        // for one arrest step; with the settle off this is the landed
        // `walkKey` condition, bit for bit.
        bool walkRan = (walkKey || walkSettleLatch) && walkEnabled && standCaptured
            && (leftDown || rightDown);
        if (walkRan)
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
        walkSettleWasWalk = walkRan;
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
        float drop = standPelvisY - pelvisY;
        if (drop > fallenDropMeters && Mathf.Abs(tilt) > fallenTiltDeg)
            return true;
        // ROUND 14 (owner addendum 6 - the sitting fall, «отставляет попу»): a
        // body that sits keeps a mild tilt and the gate above misses it
        // (measured on the stand: drop 0.605 m at |tilt| 13 deg, 0 attempts for
        // the whole 60 s take while the palms sat planted). The get-up family
        // alone reads the deep-drop term; every other row keeps its bytes.
        return getUpEnabled && drop > GetUpSittingDropMeters;
    }

    // ─── ROUND 43: THE SIT WINDOW ───
    // The r42 cap-15 finding: after an attempt's bound the body RIGHTS and parks
    // in a quiet upright sit - torso ~-10 deg, both soles loaded, pelvis ~0.3 m
    // below stand, ~1.5-2 s long. It is invisible to the ladder by construction:
    // IsDown's sitting term needs a drop PAST 0.50 m and the sit parks ABOVE it.
    // The window is that state, named: upright torso (the card's own tilt bar),
    // both feet grounded, the soles loaded, the pelvis quiet and parked low.
    // PURE: reads only; every clock lives with its caller.
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
        return intent != null && Mathf.Abs(intent.moveX) > 0.01f;
    }

    // c99 AUTO RISE (owner's addendum 01.10): the card's own input grows a
    // self-press. A keyless down held -autoGetUp D seconds makes the get-up
    // read as keyed, so the man tries to rise himself (knees -> arms -> sit ->
    // stand) instead of lying - the owner: "does not even try". The press
    // latches for the current down; a stand or a real key clears it. With 0
    // (default) this is the historic WalkKeyHeld() exactly.
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

    // ─── quiet pose ────────────────────────────────────────────────────────
    // WALK/HUMAN CYCLE 81: the crouch's own knee stop on the pose path. The old
    // path clamps its crouch knee target at 115 deg (the leg's stop); the crouch
    // layer above uses the same number so a full fold reads the stop on both.
    private const float CrouchKneeStopDeg = 115f;

    private void TickStand(float tilt, float pelvisTilt, float pelvisAngVel,
                           float pelvisY, float pelvisVelY, float comX, float comVelX,
                           bool leftDown, bool rightDown, float leftFootY, float rightFootY)
    {
        float knee = KneeForHeight(pelvisY, pelvisVelY);
        // CYCLE 116: the handoff capture (see the fields). While the window
        // runs the height reference rides from the walk-end pelvis height back
        // to the stand frame's own: the body is taken where it IS. Off or
        // expired = KneeForHeight above, bit for bit.
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
        // The stand path is the law the 40 s stand was verified on and stays as
        // it is: the pelvis-upright law owns both hips, the CoM loop owns the
        // ankles (a torque, not an angle), the trunk target is the controller's
        // own. The pose library drives the WALK; its Stance pose is the pose the
        // walk leaves the ground from, not a second stand controller.
        torsoTargetDeg = balance.lumbarTargetTilt;
        // ═══ THE CROUCH LAYER (walk/human cycle 81): AN ACTION ABOVE THE BASE ═══
        // The owner's order: the crouch is delivered while the pose drive is ON.
        // It is a layer of this path, not a second stand controller. It owns the
        // knee fold TARGET (the depth carrier - the old path's own formula,
        // kneeBaseAngle + crouchLoadLevel * crouchKneeFlex, at the old path's own
        // 115 deg stop) and OFFSETS two base targets: the pelvis-upright loop's
        // world tilt (forward = minus, the old crouch's own sign) and the trunk's
        // (crouchTorsoLean, forward = minus). Every base driver below keeps
        // running - the CoM/ankle loop, the pelvis law, the trunk and the
        // neck/head holds are what keep the crouch from toppling.
        // crouchLoadLevel is the C1 load-shaped level: exactly 0f on any frame
        // that never crouches, so this `if` leaves the stand bit-for-bit the
        // pre-crouch stand (the stand fence is the proof).
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
        // ═══ THE FORWARD LEAN LAYER (walk/human cycle 82): AN ACTION ABOVE THE BASE ═══
        // The owner's order: the lean delivered while the pose drive is ON, reading as a
        // WEIGHT SHIFT, not a torso tilt. It OFFSETS three base targets and writes no
        // muscle signal: A2's pelvis world-tilt target and A3's trunk target take the old
        // path's own lean arithmetic (forward = minus, 6230/9937), and A1's CoM loop gets
        // the point it regulates toward moved FORWARD by leanShiftLevel * leanComShift.
        // A1 stays the ankle's single writer (R1/R3) - only its target moves.
        // THE DOSES (see the constant's comment): D2 held the lean cleanly with the tilt
        // masses on the fast relay and a slow shift; its break was the RELEASE on the fast
        // relay. So the tilt relay is asymmetric - the intent level on the way in, the
        // shift's slow clock on the way out (a lean commits at a person's pace and eases
        // back), and both clocks are exactly 0f on every frame that never leans (the stand
        // fence is the proof, R2). CYCLE 83: BOTH WAYS. Forward: bit-identical to c82 (the c83fwdcheck row re-earns the
        // c82 KEEP). Back: the tilt masses ride the SHIFT's slow clock on the way IN as well
        // (dose 1's fast-in mirror hopped the feet off the floor at the rise - the CoM loop
        // must track the tilt every step; the dorsiflexor side is the weak one).
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
        // ═══ THE SUPPORT-SIDE SHIFT (kick cycle k2): THE STRIKE'S OWN LEVER ON
        // THE STAND ═══
        // The level-1 stand's CoM-fed pelvis term, added to this stand exactly
        // while the strike's own level is up (see the field block): the pelvis
        // TARGET leans toward the CoM with the same balance signal the level-1
        // stand uses - the term the pose path never had, and the one the k1
        // report names as the reason the repeat goes down on the third stroke.
        // The CoM-loop's own target offset rides the same signal (default 0: the
        // point the loop regulates toward moves AWAY from the CoM by
        // kickShiftComMeters at |signal| = 1). Both terms are exactly absent when
        // balance.kickShiftLevel is 0f - the stand fence is the proof (R2).
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
            // k4 THE WIND-UP (term 1, STATIC): the loop's CoM value moves a fixed
            // kickWindComMeters ahead of the measured CoM for as long as the
            // level is up, so the ankle's own CoP command brakes the body from
            // the stroke's first frame (the k3 record's residue: the CoM travel
            // the support channel never took). No signal factor: the k2 shape
            // died exactly where the strain is (~0 signal in a settled stance).
            if (Mathf.Abs(kickWindComMeters) > 0.0001f)
                comXAnkle += kickWindComMeters * kickShift;
            kickPlantOn = kickPlantHold > 0.5f;
        }
        // ═══ k6 - THE WIND-UP'S TERM 1, AS A BOUNDED GAIN ON THE ANKLE LOOP ═══
        // The k5 report's section 7, sized by the k5 record: the k4 constant
        // offset is a 0-or-fatal switch at the loop's own 140 per metre, so the
        // wind-up re-enters here as a gain - the loop's two CoM channels
        // re-scaled by kickWindGain while the INTER-STROKE WINDOW is up (the
        // episode's level up, no stroke in flight: the stretch the stand has to
        // give the weight back onto the standing leg in), with the loop's
        // reference migrated from the support hull's centre to the STANDING
        // foot's own plate centre - the point the same loop already regulates
        // toward whenever the kicking foot is off the floor. The gain is the
        // bound: at the record's 12 cm settled offset a 0.01 dose is 0.175 of
        // full scale (~11 N*m of the 65 N*m ceiling), and the dose would have to
        // be 0.7 m of offset to rail. Both fields are exactly 0f whenever the
        // window is down - every other row of the stand, kick or no kick, is
        // the pre-k6 arithmetic character for character (R2).
        kickWindLevel = 0f;
        kickWindRefX = 0f;
        if (kickShift <= 0.0001f)
        {
            // The episode is off: the per-stroke latch re-arms with the window
            // and the burst's own frame is let go (k7).
            kickWindWindowUp = false;
            kickWindFrameHeld = false;
            // k7 THE RELEASE'S OWN HOLD: the request's level has reached 0, so
            // the k6 release hands the reference back to the hull centre now.
            // With -kickWindHold <s> the term is kept alive for the settle - the
            // hand-over happens <s> seconds later instead of on this frame.
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
            // The request is up: the release's own clock is loaded here, so it
            // starts running the moment the level reaches 0 (k7). With the flag at
            // its default 0 the load is a write of 0f and the branch below never
            // fires - the release stays the k6 one, bit for bit.
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
            // k16: the window's own target away from the edge (see the field
            // block). With the flag at its default the guard is false and this is
            // the k6 expression character for character (R2). The offset travels
            // with the reference through every latch mode below (the latched
            // value IS the shifted point) and through the k11 carry read, which
            // sees the reference this window actually carries.
            if (kickWindMargin > 0f) liveX -= kickWindMargin;
            // k11: the window's own rising edge - the moment the carry the term
            // scales itself by is read (the k7 latch-1 pattern's own clock).
            bool kickWindRising = !kickWindWindowUp;
            if (kickWindLatch > 1.5f)
            {
                // 2: the burst's own frame - latched once, carried across the
                // strokes (a stroke takes the window down, not the frame).
                if (!kickWindFrameHeld)
                {
                    kickWindRefLatched = liveX;
                    kickWindFrameHeld = true;
                }
                kickWindRefX = kickWindRefLatched;
            }
            else if (kickWindLatch > 0.5f)
            {
                // 1: the plant's own pattern - re-latched at every rising edge.
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
                // k11 THE NORMALIZER: the dose is the term's P AT the error this
                // window itself carries, latched on the window's first frame -
                // the level is what that dose costs at this window's own scale.
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
            // k14: THE WAIT'S OWN LEVEL. While the pace clock has armed a stroke
            // and the gate is still deferring it, the window carries the knob's
            // own level in place of the one above - the wait read apart from the
            // inter-stroke stretch, which is what the transfer needs. With the
            // flag unset (-1) this branch is dead: the level is the expression
            // above, character for character (R2).
            if (kickWindDue >= 0f && balance.kickFireHeld) kickWindLevel = kickWindDue;
        }
        else if (kickShift > 0.0001f && kickWindHead >= 0f && balance.kickHeadActive
                 && state != null)
        {
            // k15 THE STROKE'S OWN HEAD: the lift's unload window, the term's
            // reference (the STANDING foot's live plate centre) and the knob's
            // own level in force - the k14 branch's sibling (see the field
            // block). With the flag unset (-1) this branch is dead and the
            // stroke carries exactly the level 0f the line above wrote (R2).
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
            // k24 THE STROKE'S OWN FLIGHT: the one-legged second and the
            // landing's own tail - the loop on the STANDING foot's plate centre
            // at the knob's own level (see the field block). With the flag
            // unset (-1) this branch is dead and the stroke carries exactly the
            // level 0f the line above wrote (R2).
            bool flightSupportIsLeft = balance.kickSideRight;
            Vector2 flightFoot = flightSupportIsLeft
                ? state.leftFootPosition : state.rightFootPosition;
            kickWindRefX = KickWindRefPoint(flightFoot);
            kickWindLevel = Mathf.Clamp01(kickWindFlight);
            kickWindWindowUp = false;
        }
        else
        {
            // A stroke is in flight (or the gain is off): the window is down, the
            // episode is not - the burst's own frame stays latched (mode 2) and
            // the per-stroke latch re-arms for the next rising edge (mode 1).
            kickWindWindowUp = false;
        }
        // The stand hip carries no angle target: the pelvis upright law below
        // owns both hips, and on planted legs that is also the hip pose.
        // ═══ THE LEG STRIKE (kick cycle k2): A LAYER OF THIS PATH ═══
        // The kick's own step (published by LegStrikeDrive at order -10) hands
        // the KICKING leg's three angle targets to this path's own DriveOneLeg -
        // one writer per joint, the same discipline as the crouch's knee target,
        // and the strike never touches the trunk, the pelvis law or the pose
        // library. The SUPPORT leg keeps exactly the base's own call, and the
        // strike's joints are stamped with its own owner code so the row says who
        // held the leg. Off the stroke this is the pre-kick pair, bit for bit.
        if (balance.kickStrokeActive)
        {
            // k2 fix: DriveOneLeg's first parameter is `left`, so the strike's
            // side is passed INVERTED (the k2 first build kicked the support leg
            // - the row's owner census on the kicking side read 0 frames while
            // the support side carried 474).
            bool kickRight = balance.kickSideRight;
            DriveOneLeg(!kickRight, balance.kickHipTargetDeg, balance.kickKneeTargetDeg,
                true, balance.kickAnkleTargetDeg, false, 1f, BalanceController.TapOwnerKick);
            DriveOneLeg(kickRight, float.NaN, knee, false, 0f, kickSupportHold > 0.5f);
        }
        else
        {
            // CYCLE 118: the handoff hold (see the fields): through the settle's
            // release window the stance knees may not extend past the hold floor
            // until the body is quiet; off, `kneeHold` IS `knee`, bit for bit.
            float kneeHold = walkSettleHoldLive && walkSettleHoldDeg > 0.001f
                ? Mathf.Max(knee, walkSettleHoldDeg) : knee;
            DriveOneLeg(true, float.NaN, kneeHold, false, 0f);
            DriveOneLeg(false, float.NaN, kneeHold, false, 0f);
        }
        // ═══ THE SUPPORT'S PLANT (kick cycle k3) ═══
        // The support hip carries the stand's own hold, armed at the angle the
        // strike latched, for as long as the episode is on - and the KICKING
        // side keeps exactly the call it had (the pelvis law for its own side,
        // the strike's own targets above). Every other row of the stand, with
        // the kick off or kickPlantHold 0, is the k2 call verbatim.
        // ═══ k5 - THE KICKING HIP'S OWN WRITER, ON THE STROKE ONLY ═══
        // The k5 read (kickk5_p1R/p1L, reqHip vs hip per step): this block's
        // second call is the LAST writer of the KICKING side's hip while the
        // stroke is in flight, so for 204 frames the strike's own PD demanded
        // one angle and the plain pelvis law overwrote the activation with its
        // own signal - and that signal is side-blind while the two legs are NOT
        // mirrored (Human.CreateLeg puts both thighs at hipX = ±hipHalfSpacing
        // with the SAME hinge limits), so its push lands on the two sides with
        // opposite geometric effect: the LEFT stroke's hip ran past every target
        // to its own -90 stop (kickk5_p1L: hipL -90.08/-90.23 from 6.5 s, the
        // foot stuck at 1.35 m, a fall at 7.720) and the RIGHT stroke's hip was
        // pushed back off its target early (kickk5_p1R: hipR -39.49 at 5.5 s
        // against a -51.10 request; the k4 record's 0.235 m stroke, against the
        // left's 0.696 m). One joint, one writer: while the STROKE is live the
        // kicking hip belongs to the strike alone - the LAW keeps it again the
        // moment the stroke ends (and owns both hips whenever the episode is
        // off), so the stand's own arithmetic is untouched everywhere else.
        if (kickPlantOn)
        {
            bool kickRightSide = balance.kickSideRight;
            // k19: THE HOLD'S OWN DELIVERY. The drive flags below were
            // (kickRightSide, false): correct only while the kick is on the
            // RIGHT (then driveLeft = the left = the support). With the kick on
            // the LEFT the pair resolved to (false, false) - NEITHER hip - so
            // the support hip's hold was never delivered to the left family at
            // all (the k19 ladder: -kickPlantBand 8/6/4 on kickk19_r5La001v01
            // is bit-identical to its control, md5 a8eb7919, while the same
            // band on the delivery sends the support hip to +45.39 deg and the
            // row down at 6.94 s; the k18 report's "the hold is armed at +12.6"
            // was a recomputation from the row, not an engagement). The pair IS
            // the support side's flags (kickRightSide, !kickRightSide): the
            // right-kick family keeps (true, false) bit for bit, and only the
            // left family gains the hold its own stance was latched for.
            DriveHips(pelvisTilt, pelvisAngVel, pelvisTargetTilt,
                kickRightSide, !kickRightSide, balance.kickSupportHipDeg, true, kickPlantBand);
            if (!balance.kickStrokeActive)
                DriveHips(pelvisTilt, pelvisAngVel, pelvisTargetTilt,
                    !kickRightSide, kickRightSide, 0f, false);
        }
        else
        {
            DriveHips(pelvisTilt, pelvisAngVel, pelvisTargetTilt, true, true, 0f, false);
        }
        DriveAnkleBalance(comXAnkle, comVelX, leftDown, rightDown);
        // ═══ WALK CYCLE 93: the stand's flat-sole toe hold (see the field block) ═══
        // Arm the hold only while the man genuinely stands on flat soles; the dwell
        // keeps a plate crossing the band from flapping the gain.
        // WALK CYCLE 94: the gate - the hold sleeps for any take that blocks it (the
        // trial blocks every squat take for its whole run; see MotionIntent.toeHoldBlock)
        // and for the machine's own window on any path that still arms it.
        // WALK CYCLE 94: holdAllowed - the hold MAY arm unless this take blocks it or
        // the squat machine is armed (see the field block / MotionIntent.toeHoldBlock).
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
            // WALK CYCLE 94: the fold takes its foot back at once - no dwell out.
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
        // tilt / leftFootY / rightFootY: the quiet pose reads the world tilt
        // through the trunk driver; the foot heights matter only on the walk.
    }

    // Stance knee target from the pelvis height. A low pelvis needs a longer
    // leg, and on this chain length is cos(knee/2) — so the sign is minus. The
    // rate term extends further while the pelvis is still descending: the
    // damping must push the same way as the error term, or a sinking pelvis
    // commands a deeper knee and the loop feeds itself.
    // WALK CYCLE 47 - THE PER-PLANT ROW (W1/R1's own instrument). Recorder only.
    // Called once per plant, from the pose drive's own latch (posePlantFrame in
    // TickWalk), on the frame the swing foot's sole comes back onto the floor
    // after its own lift. Every number is a read of that frame; see WalkPlantLog
    // for the columns, their members and their signs. No joint, force or target
    // is written here.
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
        // The landing key the library itself clamps to (PoseLibrary.Blend), so the
        // progress below is that blend's own u and not a second reading of it.
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
        // Extension only past the base pose was tried and made it worse, so the
        // band stays the verified [-10, 60] window of the 09:44 stand.
        return Mathf.Clamp(knee, 0f, 60f);
    }

    // ─── CYCLE 115: the settle step's own block (see the field block above) ──
    private void UpdateWalkSettle(bool walkKey, float tilt, float pelvisY, float comVelX,
                                  bool leftDown, bool rightDown)
    {
        if (walkSettleBudget <= 0f) return;
        if (walkKey)
        {
            if (walkSettleLatch) ReleaseWalkSettle("key");
            walkSettleSpent = false;
            walkSettleHoldLive = false;   // CYCLE 118: a real key cancels the hold
            return;
        }
        if (walkSettleLatch)
        {
            walkSettleT += Time.fixedDeltaTime;
            // CYCLE 119 - THE STATE EXIT (see the fields): the conclusion the
            // plant-frame rule kept missing on the shallow bends. While ARMED,
            // a body that reads planted, slow and supported for the quiet span
            // is as good as a release frame - the stand takes it.
            if (walkSettleExitMps > 0.001f && walkSettleArmed)
            {
                bool heldNow = leftDown && rightDown
                    && Mathf.Abs(comVelX) <= walkSettleExitMps
                    && (state == null || state.supportMargin >= walkSettleExitMargin);
                walkSettleExitQT = heldNow ? walkSettleExitQT + Time.fixedDeltaTime : 0f;
                if (walkSettleExitQT >= walkSettleExitQuiet)
                {
                    ReleaseWalkSettle("exit");
                    return;
                }
            }
            if (walkSettleT >= walkSettleBudget + 2f) ReleaseWalkSettle("cap");
            return;
        }
        // CYCLE 118 - the handoff hold's own clock (see the fields): it ends on
        // the body's own quiet (a quiet span), a down body, or the safety cap -
        // never on a blind timer.
        if (walkSettleHoldLive)
        {
            walkSettleHoldT += Time.fixedDeltaTime;
            if (Mathf.Abs(comVelX) <= walkSettleHoldMps) walkSettleHoldQT += Time.fixedDeltaTime;
            else walkSettleHoldQT = 0f;
            bool done = walkSettleHoldQT >= walkSettleHoldQuiet
                || walkSettleHoldT >= walkSettleHoldMax;
            if (done || IsDown(tilt, pelvisY))
            {
                walkSettleHoldLive = false;
                Debug.Log(string.Format(CultureInfo.InvariantCulture,
                    "[walkSettle] hold end {0} t={1:F2} v={2:F3}",
                    walkSettleHoldQT >= walkSettleHoldQuiet ? "quiet"
                        : walkSettleHoldT >= walkSettleHoldMax ? "cap" : "down",
                    walkSettleHoldT, comVelX));
            }
        }
        // C115b: ONE capture swing per walk. `spent` blocks the re-latch that
        // turned the settle into walking forever (c115a: 38 swings, 22 m of
        // travel, no stop). With -poseWalkSettleThreat T armed the settle may
        // fire AGAIN only while the STAND is losing its support (margin < T) -
        // the stutter-step catch, keyed on a real threat, never on speed alone.
        bool threat = walkSettleThreatM > 0f && walkSettleSpent
            && state != null && state.supportMargin < walkSettleThreatM;
        if (!walkSettleWasWalk && !threat) return;
        if (walkSettleSpent && !threat) return;
        if (Mathf.Abs(comVelX) <= walkSettleMps) return;
        if (Mathf.Abs(tilt) > 25f) return;
        if (IsDown(tilt, pelvisY)) return;
        if (!standCaptured) return;
        walkSettleLatch = true;
        walkSettleT = 0f;
        walkSettleSwaps = 0;
        walkSettleArmed = false;
        walkSettleExitQT = 0f;
        Debug.Log(string.Format(CultureInfo.InvariantCulture,
            "[walkSettle] enter v={0:F3} budget={1:F1}", comVelX, walkSettleBudget));
    }

    // The plant of a step while latched: the settle's own release frame. The
    // arrest step has landed with both feet down - the walk's "one capture
    // swing" is complete and the stand takes a carriable body. The FIRST plant
    // after the latch may still carry a pre-latch plan (its swap predates the
    // key's drop), so only an ARMED plant releases - unless the body is already
    // below the moving bar, when the settle had nothing left to do.
    private void SettleOnPlant(bool leftDown, bool rightDown, float comVelX)
    {
        if (!walkSettleLatch) return;
        // CYCLE 119 (diagnostic): every plant frame the latch sees, with its
        // own two flags and the armed state - the c118 forensics could not tell
        // a missed flag from a missed swap; this line can. Logs only, no byte.
        Debug.Log(string.Format(CultureInfo.InvariantCulture,
            "[walkSettle] plant t={0:F2} L={1} R={2} v={3:F3} armed={4} swaps={5} m={6:F3}",
            walkSettleT, leftDown ? 1 : 0, rightDown ? 1 : 0, comVelX,
            walkSettleArmed ? 1 : 0, walkSettleSwaps,
            state != null ? state.supportMargin : 0f));
        if (!leftDown || !rightDown) return;
        if (Mathf.Abs(comVelX) <= walkSettleMps) { ReleaseWalkSettle("quiet plant"); return; }
        if (!walkSettleArmed) return;
        ReleaseWalkSettle("plant");
    }

    private void ReleaseWalkSettle(string why)
    {
        if (!walkSettleLatch) return;
        walkSettleLatch = false;
        walkSettleSpent = true;
        // CYCLE 120 - THE WALK-ON GUARD (see FallReflex): the release hands a
        // still-moving body to the stand; a catch-step fired in the handoff window
        // is what turns the release into a walk-on. Arm the reflex's deferral.
        if (walkSettleGuardWin > 0.001f && why != "key" && balance != null)
        {
            balance.settleReleaseGuardT = walkSettleGuardWin;
            Debug.Log(string.Format(CultureInfo.InvariantCulture,
                "[walkSettle] guard arm {0} w={1:F2} v={2:F3}", why, walkSettleGuardWin,
                state != null ? state.comVelocity.x : 0f));
        }
        // CYCLE 118: the handoff hold arms on the release (see the fields); a
        // key return cancels it, everything else lets the body choose the end.
        if (walkSettleHoldDeg > 0.001f && why != "key")
        {
            walkSettleHoldLive = true;
            walkSettleHoldT = 0f;
            walkSettleHoldQT = 0f;
            Debug.Log(string.Format(CultureInfo.InvariantCulture,
                "[walkSettle] hold arm v={0:F3}", state != null ? state.comVelocity.x : 0f));
        }
        // CYCLE 116: the handoff capture is taken on the release frame - the
        // body's own height, the pose the stand has to carry.
        if (walkSettleHandOn)
        {
            walkSettleHandT = walkSettleBlendS;
            walkSettleHandPelvisY = pelvisBody != null ? pelvisBody.position.y : 0f;
        }
        Debug.Log(string.Format(CultureInfo.InvariantCulture,
            "[walkSettle] release {0} t={1:F2} swaps={2} v={3:F3} margin={4:F3}",
            why, walkSettleT, walkSettleSwaps,
            state != null ? state.comVelocity.x : 0f,
            state != null ? state.supportMargin : 0f));
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
        // WALK REFLEX (27.09): mirror the walk's own leg state for the reflex
        // (its datum gate and its diagnostic line; recorder-only, no reader here).
        ReflexLeftDown = leftDown;
        ReflexRightDown = rightDown;
        ReflexLeftFootY = leftFootY;
        ReflexRightFootY = rightFootY;
        ReflexLeftFootX = leftFootX;
        ReflexRightFootX = rightFootX;

        // WALK CYCLE 6x (recorder only): THE PLANT FRAME of the step in force.
        // The library schedules no plant of its own (it holds angles; its
        // placement is a target, not an event), so the plant is read from the
        // same measured signal the step clock reads for its own swap: the swing
        // foot's ground contact, swingPlanted (the heel or toe contact of the leg
        // the clock has made the swing leg). The frame that matters is the swing's
        // RETURN: a leg comes out of a swap already planted (it is the leg that
        // just landed), so only a contact that follows its own lift counts as this
        // step's plant. Read BEFORE the swap block below, so a plant that fires the
        // swap on the same frame is still attributed to the step it ends.
        if (!swingPlanted) swingAirborneThisStep = true;
        bool posePlantFrame = swingPlanted && swingAirborneThisStep;
        // WALK CYCLE 47 (recorder only): the leg that is planting, read HERE -
        // before the swap block below can flip swingSign. At the plant frame the
        // swing leg that has just come down IS the plant, and the swap may fire on
        // this same frame, so the side cannot be re-read at the row's own frame.
        // Nothing reads this back; swingAirborneThisStep above is untouched.
        bool posePlantLeft = posePlantFrame && swingSign > 0;
        if (posePlantFrame) swingAirborneThisStep = false;

        // WALK REFLEX v2c (28.09, the pace-recovery seat's cadence law): the step
        // clock advances at 1/ReflexCadenceScale - WalkReflex raises the rate while
        // the walk's own pace is above what its cadence can carry (the vault of the
        // curb climb outrunning its support). Everything on the clock scales with
        // it: the phase s = stepClock / swingDuration, clockDone / maySwap, the
        // flick's window and the bob. 1 = the landed cadence bit for bit.
        stepClock += dt / Mathf.Clamp(ReflexCadenceScale, 0.35f, 1f);
        // Rule: the step comes from the CoM leaving the support area. During
        // the double support tail the swap waits for the walk clock. WALK CYCLE
        // 22: the early path that started the next swing once the CoM had passed
        // the stance foot by comLeadSwapMeters is RETIRED here. On c6w21_walk
        // the lead crossed its 0.16 m threshold at 6.175 and stayed past it, and
        // what the step index then named was the walk clock's floor (periods
        // 0.53 / 0.62 / 0.355 s against the cycle's own 0.80 s swing + stance),
        // not a transfer - the body drifting past a foot the feet had already
        // lost. The transfer is the swap's own trigger now; see the load gate
        // below. `lead` itself is not computed any more; the lead COLUMN the
        // recorder carries is the placement's own latch
        // (capturePosePlaceLeadM), which is untouched.
        // A swap while the swing foot is still airborne is a flight phase: the
        // body free falls, lands on folded legs and the 130 N*m knee ceiling
        // cannot straighten it again (p_dsw 5.30, both soles up 0.08 s, pelvis
        // 55 mm down and sinking). So the swap waits for the foot to be planted,
        // with a hard timeout so a swing that never lands cannot stall the gait.
        bool maySwap = swingPlanted || stepClock >= halfCycle + 0.15f;
        bool clockDone = stepClock >= halfCycle;
        // WALK CYCLE 21: THE LEAD SWAP WAITS FOR THE NEW SUPPORT'S LOAD.
        // Cycle 20's carry-forward, measured on its own row: the step index left
        // on the CoM's lead (reason 2) at 6.175 while the foot that had just
        // landed carried 52.7 N, and the same foot carries 1264.9 N a tenth of
        // a second later; the frame clock's own swap carries 590.3 N on this
        // same law. `lead` is measured from the OLD support - it says the body
        // is past the stance foot, not that the new one has taken the weight,
        // and the weight transfer is what the step index is supposed to name.
        // The gate is the load the SWING foot (the next step's support) already
        // carries, read from this component's own sole-plate meter - the
        // vertical axis, poseGroundFzLeftN/RightN, the recorder's fzL/fzR
        // convention on the same frame (SampleGroundReactionX). 250 N is a
        // third of the rig's measured quiet weight (715 N); the recorded catch
        // on the c20 row peaks at 486.5 N, so the gate opens inside the load
        // the foot actually takes. The clock paths are untouched: the planted
        // timeout in `maySwap` and `clockDone` below stay the gait's own
        // fallbacks, so a step whose foot never loads still ends on its clock.
        const float leadSwapLoadN = 250f;
        float swingFootLoadN = swingSign > 0 ? poseGroundFzLeftN : poseGroundFzRightN;
        bool supportLoaded = swingFootLoadN >= leadSwapLoadN;
        // WALK CYCLE 22: THE LOAD IS THE SWAP'S OWN TRIGGER. Cycle 21 put the
        // gate on the lead path; the lead path is retired and the gate is now
        // the whole trigger, so the step index leaves on the WEIGHT the new
        // support carries - the event the swap is named after - and never on the
        // CoM's drift past a foot the feet have already lost. The swing phase's
        // own end (stepClock >= swingDuration) is kept as the trigger's floor:
        // it is what keeps a catch's own impact spike (the c21 row's 856 N at
        // 6.075, the foot still the swing leg's own on that frame) from leaving
        // with the step. `maySwap`'s planted timeout and `clockDone` below are
        // untouched - a foot that never loads still ends its step on the clock.
        // WALK CYCLE 23: THE LOAD MAY ONLY NAME A TRANSFER ON A FOOT THE BODY
        // IS OVER. The c22 gate fired c6w22_walk's k1 at 6.055 while the foot
        // that had just landed (the right, its sole +0.619) stood 0.267 m
        // AHEAD of the CoM (+0.351) and the old support still carried 760 N:
        // the step index left, the pose made the still-loaded leg the swing,
        // the body was left behind its own support and the walk stalled
        // (comTravelX -1.0832, 122 negative comVelX frames). The same gate on
        // c6w21_walk fires at 6.230 - the landing only 0.134 m ahead, the old
        // foot down to 56.6 N - and that row travels (+1.9427). The one term
        // that separates the two rows is that distance, and it is read from
        // the two quantities this block already has: the swing foot's own
        // live x (the local `swingFootX`, the same leg the meter above reads
        // its load from) and comX. The swap therefore waits until the
        // incoming support has come within bodyAtSupportMeters of the body.
        // This is NOT the drift re-admitted: the lead rule and this one are
        // the same line on the emitted rows - the step's own length splits
        // them (lead = length - near) - which is why c6w21_walk's k1 stays at
        // 6.230 (the load is its binding condition on both laws), while the
        // later steps price the difference: c6w21_walk's step 2 fires at the
        // load's own crossing (6.585) here, at the lead's 0.16 under the c21
        // law (6.850). A foot that lands so far ahead that the body never
        // comes to it still ends its step on the clock: `maySwap`'s planted
        // timeout and `clockDone` are untouched.
        const float bodyAtSupportMeters = 0.15f;
        bool bodyAtSupport = swingFootX - comX <= bodyAtSupportMeters;
        // WALK CYCLE 25: THE TRIGGER'S TIME FLOOR IS READ AT THE WALK'S OWN CADENCE.
        // Cycle 24 took the bar, the price and the card's own 3 m of travel, and
        // left the count one short (walkSwapCount 5 against the round's 6). Its
        // walk's steps grew to 0.545 / 0.805 / 0.360 / 0.480 s (0.72 m per swap
        // against the sprint cadence's 0.44), and the sixth swap's own demand -
        // the incoming support loaded to 250 N and inside bodyAtSupportMeters
        // above - opens at 8.09..8.14, INSIDE the swing's own 0.35 s: the floor
        // would hold that transfer to 8.24 while the fall comes at 8.18. Measured
        // on the row's own frames (F:/hermes-data/tmp_w25/probe_w25floor.py and
        // probe_w25mine_floor.py; control: the 0.35 floor reproduces every
        // recorded fire on this row), the 0.35 s floor's whole market there is
        // ONE step - the one begun at 7.050, whose demand reopens at 7.295 and
        // which the floor held to 7.410 (-0.110 s) - plus that sixth window.
        // The floor is therefore read on its own constant, and NOTHING of the
        // pose moves with it: halfCycle, `maySwap`, `clockDone` and the phase
        // s = stepClock / swingDuration keep the swing's 0.35 s.
        // The value is the row's own frames' price, not a taste: at 0.20 s the
        // sixth swap leaves at 8.090 with the incoming support carrying 270 N -
        // inside the window the load itself opens for it (the foot dips below
        // 250 N at 8.04..8.06 and again from 8.14 on) and 0.09 s before the
        // fall. It is a short catch step (0.20 s) by a body already running at
        // 1.15 m/s: a stumble step, named on a foot that has carried load since
        // ~7.95 - no landing impact spike is being named, which is what this
        // floor exists to block (cycle 22). The step it also moves is the one
        // above: its 7.410 becomes 7.300 (the count of fires before the fall on
        // the recorded frames: 6.245 / 7.050 / 7.300 / 7.890 / 8.090 = six). What
        // the replay cannot price is the run after that 7.300: everything later
        // is the gait's own to pay, and the row decides.
        const float swingFloorSeconds = 0.20f;
        // WALK CYCLE 35: THE PLANT'S OWN CLOCK (see the fields above). The clock
        // is written on EVERY frame - seconds the incoming support's load read has
        // been at or above the gate without a break - and read by the ledOut term
        // ONLY once this step's tail latch is set (the catch, on the landed row).
        // At 0 the guard is `held >= 0` = true, so ledOut is the landed expression
        // bit for bit.
        poseTailLoadHeld = supportLoaded ? poseTailLoadHeld + dt : 0f;
        bool ledOut = stepClock >= swingFloorSeconds && supportLoaded && bodyAtSupport
            && (!poseTailSeenThisStep || poseTailLoadHeld >= poseTailHoldSeconds);
        // WALK REFLEX (27.09): THE STUMBLE STEP. The walk's own trigger is a CoM
        // that has come FORWARD (the capture point, the load, the step clock);
        // nothing in it answers a body carried BACKWARD, so a stalled step ends
        // as a backward topple with both soles still planted - the CurbEntry
        // failure in the report and the seam drift under the immortal line both
        // die that way. A body drifting backwards past ReflexStumbleVelMps for
        // ReflexStumbleHoldS while both soles are down (swingPlanted - the same
        // gate `maySwap` reads, so the swap can never fire in flight) releases
        // the step NOW. The foot's destination is not written here: the
        // placement law above takes its capture point off comVelX, so a
        // backward-moving CoM already aims the foot behind the body - one
        // recovery step, then the ordinary walk resumes. The latch clears as
        // soon as the drift is answered, so one stumble is one step.
        bool reflexStep = false;
        if (ReflexStumbleVelMps > 0.001f)
        {
            if (swingPlanted && comVelX <= -ReflexStumbleVelMps && !reflexStumbleLatch)
                reflexStumbleHoldT += dt;
            else if (comVelX > -ReflexStumbleVelMps * 0.5f)
            {
                reflexStumbleHoldT = 0f;
                reflexStumbleLatch = false;
            }
            if (reflexStumbleHoldT >= ReflexStumbleHoldS && !reflexStumbleLatch)
            {
                reflexStumbleLatch = true;
                reflexStumbleHoldT = 0f;
                reflexStep = true;
                ReflexStumbleSteps++;
            }
        }
        if (maySwap && (clockDone || ledOut || reflexStep))
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
            // CYCLE 115: a swap under the settle re-plans the INCOMING swing
            // with the arrest share - that step is the settle step, and its
            // plant is a legal release frame (see SettleOnPlant).
            if (walkSettleLatch)
            {
                walkSettleSwaps++;
                walkSettleArmed = true;
            }
            // WALK CYCLE 6t: the CoM's own position at the swap is the datum the
            // descent's need is read from (see `need` below).
            comXSwap = comX;
            // WALK CYCLE 6w: the height datum of the step that starts on this
            // frame, by the same latch - one write per swap, no second clock.
            hipHeightStepDatumM = pelvisY - FloorDatumM;
            swingFootX0 = swingSign > 0 ? leftFootX : rightFootX;
            stanceFootX = swingSign > 0 ? rightFootX : leftFootX;
            swingFootX = swingSign > 0 ? leftFootX : rightFootX;
            // WALK CYCLE 6x (recorder only): the leg that becomes the swing leg on
            // this frame is the one that just landed, so THIS step's own lift - and
            // therefore its own plant - is still ahead of it. Re-arm the plant
            // detector; the plant that just ended the previous step is already
            // latched.
            swingAirborneThisStep = false;
            // WALK CYCLE 35: the tail latch is PER STEP - the swap that ends the
            // step clears it, so the next step's fire starts unheld.
            // WALK CYCLE 38: the flight tail (the LANDED bound's own window) is
            // handed to the next step as the license for the widened bound.
            poseTailFlightPrevStep = poseTailFlightThisStep;
            poseTailFlightThisStep = false;
            poseTailSeenThisStep = false;
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
        // WALK CYCLE 24: THE DESCENT'S OWN LEAD SHRINKS WITH THE BODY'S SPEED.
        // Cycle 23's price named the producer of its hop: on c6w24pre_walk the
        // step that starts at 6.940 (the sprint's own catch) puts its foot down
        // at 7.175, while the CoM takes the whole 0.16 m lead only at 7.180 -
        // the moment the new support is needed arrives 5 ms AFTER the foot has
        // already landed. The library reads this same quantity as the descent's
        // own authority (`need` drives the clearance ask and the descent's
        // weight together), so the footplace's extension is asked while the leg
        // is already loading, the catch keeps its knee at 55 deg, the sole lands
        // 0.06 m behind the CoM and the pelvis sinks -1.075 m/s: the hop the
        // round is paying for.
        // The 0.16 m is a DISTANCE, and what the delivery needs is how much of
        // the swing is left when the support is needed. At the walk's own band
        // (the c12pre row's 0.27..0.47 m/s) that distance is 0.35..0.59 s - a
        // whole swing - which is the cadence comLeadSwapMeters was measured on,
        // and the term below leaves every step of that row unmoved. At the
        // sprint's 0.7..1.5 m/s the same distance is covered in 0.23..0.11 s.
        // The lead is therefore read against the body's own speed: kept whole
        // through the walk's band, giving up 2.5 of itself per m/s above
        // 0.55 m/s, floored at 0.35 of the lead (measured on the accepted rows,
        // the floor holds the onset inside the swing: no earlier than the 0.17
        // phase of a step, where the swing's own clearance still reads above
        // 50 mm). The onset it moves: c12pre unmoved on every step, c20 left in
        // place but for its first step's 5 ms, c23's catch 7.180 -> 7.090 - 85 ms
        // of unloaded extension before the
        // foot arrives - and c21's 7.080 -> 6.980. The swap block's own terms
        // are untouched; only the physics the swap names moves.
        const float needLeadRefMps = 0.55f;
        const float needLeadShrinkPerMps = 2.5f;
        const float needLeadFloorFrac = 0.35f;
        float needLeadM = comLeadSwapMeters * Mathf.Max(needLeadFloorFrac,
            1f - needLeadShrinkPerMps * Mathf.Max(0f, comVelX - needLeadRefMps));
        float need = Mathf.Clamp01((comX - comXSwap) / Mathf.Max(0.01f, needLeadM));

        // WALK CYCLE 35: THE TAIL GATE - cycle 34's own confine, re-read here as
        // the hold's own. The support's load has left (<= the press's own 50 N
        // bound), the swing leg is airborne with a foot down, and the CoM has
        // taken its own lead (need >= 0.9 - 1 is the lead the swap rule calls a
        // swap). On the landed row the pair opens ONLY from 8.150 (through 8.270),
        // so the latch is set on the CATCH STEP ONLY and the hold's reach is
        // confined there by construction. Read ONLY to set the latch; nothing else
        // sees it. The values are the same reads the support hold's own gate takes
        // later in this method (the load bound is cycle 28's own).
        float plantTailLoadN = swingSign > 0 ? poseGroundFzRightN : poseGroundFzLeftN;
        // WALK CYCLE 38: THE NEXT STEP'S OWN TAIL (see the fields above). The bound
        // this gate reads is the LANDED one except on the step that follows a flight
        // tail; only the bound moves, every other read is cycle 35's own.
        float plantTailBoundN = poseTailFlightPrevStep
            ? Mathf.Max(PoseLibrary.SupportHoldLoadN, poseNextTailLoadN)
            : PoseLibrary.SupportHoldLoadN;
        bool plantTailGate = !swingPlanted && (leftDown || rightDown)
            && plantTailLoadN <= plantTailBoundN
            && need >= PoseLibrary.PlantHoldNeedMin;
        if (plantTailGate)
        {
            poseTailSeenThisStep = true;
            // The license is the LANDED bound's own window - the flight - and a
            // widened window (this step's own tail, if the knob opened it) is NOT
            // one: it cannot license the step after this one.
            if (plantTailBoundN <= PoseLibrary.SupportHoldLoadN) poseTailFlightThisStep = true;
        }

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
        float standHipHeightM = standPelvisY - FloorDatumM;
        float hipHeightLiveM = pelvisY - FloorDatumM;
        // The latch is READ here, never re-taken: latching it on every frame (or
        // re-reading it from the body) is exactly the mirror 6v measured, and it
        // would make the error term identically zero again. The one write is the
        // swap above; only the walk's first step, which no swap precedes, has no
        // datum yet, and its datum is this frame's own live height.
        if (float.IsNaN(hipHeightStepDatumM)) hipHeightStepDatumM = hipHeightLiveM;
        // WALK CYCLE 62: THE STEP'S OWN VAULT. The phase is the step clock's own
        // 0..1 of the step in force, clamped at 1 so a swap that waits for its
        // plant does not run the bump past its own return; the lift is
        // (1 - cos(2*pi*phase))/2 - 0 at both ends of the step, 1 at its halfway.
        // The wave shape (-cos, asking BELOW the datum at the swap) was probed at
        // 0.02/0.03 and both walks collapsed mid-block (fallTime 15.29/13.525,
        // travel 2.29/2.55 m): the ask never goes below the step's own datum.
        // At -poseBobAmp 0 the term is exactly 0 and the landed expression stands
        // bit for bit. The stand frame stays the ceiling.
        float bobPhase01 = Mathf.Clamp01(stepClock / Mathf.Max(0.05f, halfCycle));
        float bobM = bobAmpMeters * (1f - Mathf.Cos(2f * Mathf.PI * bobPhase01)) * 0.5f;
        float hipHeightTargetM = Mathf.Min(standHipHeightM, hipHeightStepDatumM + bobM);
        // WALK CYCLE 6s (recorder only): the height loop's request and the height
        // the body had, published with the rest of the ask block below.
        poseAskHipHeightRequestM = hipHeightTargetM;
        poseAskHipHeightLiveM = hipHeightLiveM;
        // 1. WHERE the next foot lands: the capture point (where the CoM will be
        //    in one pendulum time constant) plus the stability margin the plan
        //    asks for, clamped to the step band. Metres.
        float pendulum = Mathf.Sqrt(Mathf.Max(0.05f, (pelvisY - FloorDatumM) / 9.81f));
        // WALK CYCLE 10: THE CATCH DISTANCE IS HALF THE PENDULUM CONSTANT. The full
        // constant is the ARREST point (where the CoM comes to rest over its own foot)
        // and at c6z_walk's 6.295 plant it read +0.2748 m of lead (capturePosePlaceLeadM,
        // the run's largest) off a transient speed crest (comVelX 0.769 m/s): the leg
        // delivered its sole to 0.8667 m, 0.406 m ahead of the CoM (0.4605 m), the CoM
        // never crossed it, and the two loaded feet held the body back through 6.49-7.0 s
        // (mean capturePoseGroundFxN -80.7 N, -40.4 N*s). Half the constant asks for the
        // lead the walk can take back: 0.157 m at that plant.
        // CYCLE 115: while the settle owns the walk the catch point moves to
        // the ARREST share (plus the optional lead) - the same live placement
        // law reading a different share. Off: the landed expression, bit for bit.
        float settleShareNow = walkSettleLatch ? walkSettleCatch : ReflexCatchShare;
        float captureX = comX + settleShareNow * comVelX * pendulum
            + (walkSettleLatch ? walkSettleLeadM : 0f);
        // WALK CYCLE 6z (recorder only): the law's own value and the band's own
        // two edges are NAMED here so the resolver below can say which of the
        // three the clamp in force used. No number moves: these are the same
        // three expressions the Clamp call carried inline, in the same order,
        // and the Clamp is the same call.
        float placeLawX = captureX + placementMarginMeters;
        float placeBandFloorX = stanceFootX + stepMinMeters;
        // WALK REFLEX v2e (28.09, pace recovery): the step band's own span. The
        // landed band (stepMin -0.2669, stepMax 0.3189 on the ship line) is ~0.59 m
        // per 0.85 s cycle - the walk's own envelope caps it near 0.7 m/s, and above
        // that the plan cannot reach past the body (the v2w pin: placeX frozen at the
        // band ceiling while the body ran from it) and the walk pitches over.
        // WalkReflex raises the CEILING only, and only while the pace outruns the
        // landed band; 1 = the landed band bit for bit.
        float placeBandCeilX = stanceFootX + stepMaxMeters
            * Mathf.Clamp(ReflexBandScale, 0.5f, 2.5f);
        float placeX = Mathf.Clamp(placeLawX, placeBandFloorX, placeBandCeilX);
        // WALK CYCLE 6z (recorder only): THE RESOLVER, taken from the SAME
        // comparison the clamp above performs - below the floor, above the
        // ceiling, or inside the band - so the label names the branch that
        // decided the value and nothing is re-derived by a reader. The reach
        // guard below may then overrule it (4), or be out of the picture
        // entirely (5); see the guard's own branches.
        int placeTerm = placeLawX < placeBandFloorX ? PosePlaceTermBandFloor
            : (placeLawX > placeBandCeilX ? PosePlaceTermBandCeiling : PosePlaceTermLaw);
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
        // WALK CYCLE 6y: THE ANCHOR IS THE HIP JOINT, NOT THE PELVIS CENTRE.
        // 6x measured this guard degenerate: with hipToSoleM read from the pelvis
        // CENTRE it was 0.9122 m at the standing frame against the 0.86 m of thigh
        // + shin, so Mathf.Max(0f, leg^2 - hipToSole^2) was 0 on EVERY walk frame
        // (hipHeightLiveM > 0.905 m everywhere), the clamp became
        // clamp(placeX, hipX, hipX) = the pelvis x, and that pin - ramped in by
        // need - is what the 604 driving rows show: placeX - law = -0.1318 /
        // -0.1451 / -0.1753 / -0.2132 m at the plants, the pivot retreating
        // +0.5196 -> +0.1450 m while the CoM still went forward, the lead negative
        // 1.09 s before the fall. The pelvis centre is not the point the leg
        // rotates about: the hip ANCHOR sits pelvisSize.y * 0.5 below it, exactly
        // as SolveLegIk spells out (`float hipY = pelvisY - human.pelvisSize.y *
        // 0.5f;`), and that is the height the reach must be measured from.
        float hipX = pelvisBody != null ? pelvisBody.position.x : 0f;
        float legLengthM = human.thighSize.y + human.shinSize.y;
        // The anchor shares the pelvis centre's x in this rig - Human.CreateLeg
        // hangs the thigh at (hipX, -pelvisSize.y/2) in pelvis space - so hipX
        // above is the anchor's x and no second read is needed for the clamp.
        float hipAnchorY = pelvisY - human.pelvisSize.y * 0.5f;
        float hipToSoleM = (hipAnchorY - FloorDatumM) - human.footSize.y * 0.5f;
        if (hipToSoleM >= legLengthM)
        {
            // THE INERT BRANCH: the hip anchor is higher than the leg can span,
            // so the reach circle does not meet the floor at all. That is a
            // statement about the POSE (a rising or fully extended body - the
            // stand frame is exactly hipToSoleM == legLengthM), not a licence to
            // pin the placed sole under the pelvis, which is the defect 6x read.
            // This branch therefore applies NO clamp and NO ramp: placeX is left
            // exactly as the capture point + placementMarginMeters clamped by the
            // step band, and the band remains its only bound. The column says so
            // with the documented sentinel instead of a 0 that already means "the
            // pose gait is not driving the walk".
            poseAskReachXM = PoseReachInertM;
            // WALK CYCLE 6z (recorder only): this branch applied no clamp and no
            // ramp, so the guard is out of the picture on this frame and the
            // resolver says so. placeX stands exactly as the band left it.
            placeTerm = PosePlaceTermReachInert;
        }
        else
        {
            float reachXM = Mathf.Sqrt(legLengthM * legLengthM - hipToSoleM * hipToSoleM);
            float placeGuardedX = Mathf.Lerp(placeX,
                Mathf.Clamp(placeX, hipX - reachXM, hipX + reachXM), need);
            // WALK CYCLE 6z (recorder only): did the guard's own clamp + ramp
            // MOVE the value? A label, not a second clamp - placeX is assigned
            // the same Lerp result this branch always assigned (a value already
            // inside the reach box comes back bit-identical, so the comparison
            // below is exactly "the guard changed it").
            if (placeGuardedX != placeX) placeTerm = PosePlaceTermReachGuard;
            placeX = placeGuardedX;
            poseAskReachXM = reachXM;
        }
        // The x the guard measured from, published on every driving frame so the
        // row carries its own centre: read it against capturePosePlaceXM (the
        // pivot) and against the row's comX. On the frames the guard is inert the
        // two still read together - the clamp window is the whole step band.
        poseAskHipXM = hipX;

        // ═══ WALK CYCLE 6x (recorder only): THE POSE PATH'S OWN PLACEMENT ═══
        // 6w located the brake at the swaps but could not name its owner: the
        // level 1 columns (captureSwingAskX / SoleX / PlanTargetX / SolvedSoleX /
        // AppliedSoleX / AskY / HipDemandDeg / planValid / PlanFrameTiltDeg /
        // PostureBiasDeg) are 0 on every walk row - they belong to the level 1
        // capture solver - while captureWalkPath = 1 says the pose library held
        // the joints, and the library's own placement was published nowhere. These
        // four columns are that placement, and every one of them is a value this
        // method already computed for the step in force: nothing below reads one
        // of them back, no law is changed, and the branch that follows is the same
        // one that ran before this block existed.
        // placeX here is the fully resolved pivot - the step band AND the leg's own
        // reach both applied - and it is the exact value handed to the library as
        // Inputs.footPlaceX on this frame, so ask and placed sole read off adjacent
        // columns. The two sole columns are the step clock's own two foot reads
        // (swingFootX is the swing leg's, re-read at a swap; stanceFootX is the
        // field the placement clamp above is measured from).
        poseAskPlaceXM = placeX;
        poseAskSwingSoleXM = swingFootX;
        poseAskStanceSoleXM = stanceFootX;
        // WALK CYCLE 6z (recorder only): the resolver's own answer, published
        // with the ask it belongs to - the label is set in the branches above,
        // never re-derived here.
        poseAskResolvedTerm = placeTerm;
        if (posePlantFrame)
        {
            // WALK CYCLE 47: THE PER-PLANT ROW (see WalkPlantLog). Written on the
            // same frame and from the same reads as the brake distance below, so
            // the instrument and the row's own capturePosePlaceLeadM describe one
            // event. Read-only: no joint, force or target is written by it.
            RecordPlantRow(posePlantLeft, poseAskStepIndex, comX, pelvisY, s,
                stepClock, leftFootX, leftFootY, rightFootX, rightFootY);
            // THE BRAKE DISTANCE: how far ahead of the MEASURED CoM the pivot the
            // library is carrying the leg to sits, on the frame the sole lands.
            // comX is this method's own read (state.comPosition.x, the row's comX
            // column) - the same quantity the capture point above is built from, so
            // the subtraction needs no second source and no extra column. It is
            // paired with the ask of THIS frame by construction: a plant that fires
            // the swap in the same frame is attributed to the step it ends, and the
            // ask it is measured against is then the ask that frame publishes after
            // the swap (the swap has already re-read the stance foot), so the two
            // columns of that row still describe one pivot.
            placeLeadLatchM = placeX - comX;
            // CYCLE 115: the settle step's own release frame (see SettleOnPlant).
            SettleOnPlant(leftDown, rightDown, comVelX);
        }
        poseAskPlaceLeadM = placeLeadLatchM;

        // ═══ WALK CYCLE 28: THE SUPPORT'S OWN SOLE CLEARANCE, HELD (THE JOINT) ═══
        // The object: while the swing leg is airborne the SUPPORT's own sole may not
        // clear the ground, so the ground flag never breaks at the tail
        // (8.230..8.255) and the no-contact window never opens. Cycle 27 priced the
        // object and the mechanism: the sole must sit inside the estimator's own
        // 0.02 m ground band (the flag reads a part grounded at clearance <=
        // 2 x defaultContactOffset = 0.02 m), its five builds delivered 3..10 mm of
        // lowering and 1..2 frames still cleared, and the missing authority was the
        // DELIVERY - the joints followed the asks by about a quarter. This round
        // keeps cycle 27's account (the same cap, the same load gate, the same
        // recruit) and answers the mechanism instead: the stance leg's ankle and
        // knee are driven stiffer while the hold is live (StanceHoldDeliverP, on
        // the same one writer per joint - DriveOneLeg).
        //
        // The quantity is READ, not asked: the estimator's own plate measure (the
        // recorder's soleClearL/R, the ground flag's own contact test), on the leg
        // the step clock has made the SUPPORT (the leg the swing is not). The cap is
        // the value cycle 27 priced on the row (the single-support band's p90 is
        // 0.0173 and no single-support frame exceeds 0.0190 before 8.200, while the
        // flag reads the foot grounded up to 0.0200..0.0215 and off from
        // 0.0205..0.0241). The gate is the flags themselves plus the support's own
        // LOAD: the swing foot is NOT down (swingPlanted) and a foot IS down - i.e.
        // the support is the only foot on the ground - AND the support is not
        // carrying the body (cycle 26: the support unloads at 8.150..8.165 at
        // fzL 40 -> 0, while the loaded collapse frames carry 230..380 N; a foot
        // carrying the body is not clearing the ground and pressing its sole down
        // there is not this object). The account is RECRUITED, not asked once:
        // while the gate is open it is fed by the frame's own excess over the cap
        // AND by the sole's own measured rise since the last gated frame (the
        // recruit has to keep up with the lift, not only close the gap), and once
        // the gate closes it is released at SupportHoldReleasePerFrame. The hold is
        // 0 on every other frame, so the walk's own frames, the 5.60-7.30 price
        // window, the bar's own swap frame and the pre-walk stand are untouched by
        // construction.
        // THE GATE AND THE RECRUIT ARE CYCLE 27'S OWN, verbatim: the account is fed
        // ONLY by a frame whose measured clearance is ABOVE the cap, and released at
        // SupportHoldReleasePerFrame when it is not. That is not a detail - this
        // round's first build paid for it: feeding the account by the sole's own rise
        // on frames whose clearance sat BELOW the cap (0.0150..0.0178 through
        // 5.785..5.995 and every later single-support span) recruited a hold on the
        // WALK's own early steps, the P-multiplied delivery then bit there, and the
        // whole row left c6w27pre_walk at 5.815 s - two and a half seconds before the
        // object's own window - with the bar's own first swap moved (6.245 -> 6.290,
        // gap 0 -> 50 ms, flips 0 -> 2, vY -0.171 -> -0.450) and the tail's crisis
        // gone entirely (the walk kept walking, drive census walk 868 stand 0, the
        // fall moved to 9.94). The cap's own value is why cycle 27 chose 0.0180: on
        // the base row NO single-support frame exceeds 0.0190 before 8.200
        // (probe_w27hold: the > 0.0180 spans are 8.190-8.255, 8.290-8.340,
        // 8.405-8.410 and nothing earlier), so this recruit is zero on every frame
        // before the object's own window by construction.
        float supportClearM = swingSign > 0
            ? (state != null ? state.rightFootGroundClearance : 0f)
            : (state != null ? state.leftFootGroundClearance : 0f);
        float supportFootLoadN = swingSign > 0 ? poseGroundFzRightN : poseGroundFzLeftN;
        bool supportHoldGate = !swingPlanted && (leftDown || rightDown)
            && supportFootLoadN <= PoseLibrary.SupportHoldLoadN;
        float supportHoldExcessM = supportHoldGate
            ? Mathf.Max(0f, supportClearM - PoseLibrary.SupportHoldClearM) : 0f;
        if (supportHoldExcessM > 0f)
        {
            // THE RISE, AND ONLY WHERE THE EXCESS LIVES. Cycle 27's own strongest
            // build fed the account by the frame's excess AND by the sole's own
            // measured rise since the last gated frame - that is what kept the hold
            // big through the lift's last frames (its asks read 8.85..10.45 deg at
            // 8.230..8.255 while this round's excess-only recruit reads 2-3 deg
            // there and lowers the sole only 1.5..2 mm, short of the ~1..1.5 mm the
            // flag's own band still needs). The rise is measured against the last
            // GATED frame, but it only FEEDS here, inside the excess branch: this
            // round's own first build fed the rise on frames whose clearance sat BELOW
            // the cap, recruited the walk's own early single-support spans, and left
            // c6w27pre_walk at 5.815 s. With the feed inside this branch, no frame
            // below the cap can add to the account and no frame before 8.190 can
            // either (the > 0.0180 spans of the base row are 8.190-8.255,
            // 8.290-8.340, 8.405-8.410 and nothing earlier).
            float riseM = Mathf.Max(0f, supportClearM - supportHoldClearPrevM);
            supportHoldClearPrevM = supportClearM;
            supportHoldM = Mathf.Min(PoseLibrary.SupportHoldCeilM,
                supportHoldM + supportHoldExcessM + riseM);
        }
        else
        {
            supportHoldClearPrevM = supportClearM;
            supportHoldM *= PoseLibrary.SupportHoldReleasePerFrame;
            if (supportHoldM < 0.0002f) supportHoldM = 0f;
        }
        // THE STIFF DELIVERY'S OWN GATE: the support's sole must be IN THE FLAG'S
        // BREAK ZONE (>= 0.0190 m - the flag reads a foot grounded up to
        // 0.0200..0.0215 and off from 0.0205..0.0241, and on the base row NO
        // single-support frame exceeds 0.0190 before 8.200). Below that the asks are
        // delivered with the walk's own pair, exactly as cycle 27's five builds did;
        // inside it the pair is multiplied. This is what confines the stiffening to
        // the object's own frames without a clock.
        bool stanceHoldLive = supportHoldM > 0f
            && supportClearM >= PoseLibrary.SupportHoldStiffClearM;

        // WALK CYCLE 37: THE TAIL GATE, re-read as the ankle's own confine - the
        // same reads (and the same bounds) the stance hold's gate takes: the swing
        // leg is airborne with a foot down, the SUPPORT's load read is inside
        // cycle 28's own bound, and the CoM has taken its own lead (need 1 is the
        // lead the swap rule calls a swap). On the landed row the gate opens ONLY
        // from 8.150 (through the plant), so the carry exists on the catch step
        // alone and the bar (6.245) and the price window (5.60..7.30) cannot be
        // touched by construction (the first diff of the landing row is 8.155).
        // WALK CYCLE 38: the SAME widened bound on the next step's own tail - the
        // carry's own confine, the same reads the latch's gate above takes.
        float swingPlaceBoundN = poseTailFlightPrevStep
            ? Mathf.Max(PoseLibrary.SupportHoldLoadN, poseNextTailLoadN)
            : PoseLibrary.SupportHoldLoadN;
        bool swingPlaceGate = !swingPlanted && (leftDown || rightDown)
            && supportFootLoadN <= swingPlaceBoundN
            && need >= SwingPlaceNeedMin;
        if (swingPlaceGate) swingAnkleRampT += dt; else swingAnkleRampT = 0f;
        float swingPlaceRamp = swingAnkleRampSeconds <= 0f
            ? 1f : Mathf.Clamp01(swingAnkleRampT / swingAnkleRampSeconds);

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
        // WALK CYCLE 75/76: the descent-arithmetic gate, handed to the library (off = the
        // landed drawn-knee bias bit for bit).
        poseIn.placeKneeLive = placeKneeLive;
        // WALK CYCLE 37: the ankle's own gated, ramped carry (0 at the default: the
        // library then emits the landed drawn value bit for bit).
        poseIn.swingAnkleDeg = swingPlaceGate ? swingAnkleDeg * swingPlaceRamp : 0f;
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
        // WALK CYCLE 28: the support's own hold - the sole lowering the recruit has
        // accumulated, in metres, which the library turns into the support leg's own
        // ankle press and knee extension (0 when the hold is not in force, or
        // released), plus the lever the ankle press is read against.
        poseIn.stanceSoleHoldM = supportHoldM;
        poseIn.stanceSoleLeverM = human.footSize.x * (1f - human.footHeelShare * 0.5f);
        // WALK REFLEX (27.09): the rise's own extra clearance, above the accepted
        // -poseLift ask. 0 with no reflex component: the landed expression.
        poseIn.swingLiftM = swingLiftMeters + ReflexLiftBoostM;
        // WALK REFLEX (27.09): the placement the law resolved, published for the
        // reflex's own diagnostic line (read only; nothing in this file reads it).
        ReflexPlaceX = placeX;
        // WALK CYCLE 64: the ask-side tier - the drawn stance knee keys (the drawn
        // values by default: the blend below is then the shipped pose bit for bit).
        poseIn.stanceFoldKneeDeg = stanceFoldKneeDeg;
        poseIn.stancePlaceKneeDeg = stancePlaceKneeDeg;
        poseIn.stanceLoadKneeDeg = stanceLoadKneeDeg;
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
        torsoTargetDeg = pose.torsoTiltTargetDeg + ReflexTorsoLeanDeg;   // WALK REFLEX v2f
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
        // WALK CYCLE 73: THE FLICK'S OWN GEOMETRY. Cycle 72's flick (0.035 s
        // half-width at the s = 1 crossing) held the walk but bought nothing:
        // the crossing sits cross = swap + 0.545338 and the planted foot's
        // lift sits cross + 0.400 (median over the record's 44 plants), so the
        // pulse spent its -35 ms half BEFORE the loaded roll window
        // [lift-0.30, lift] = [cross+0.10, cross+0.40] the c71 doses converted
        // in - and left the conversion zone to the record. This edit keeps the
        // lever and moves the pulse onto that window, widened: the boost lives
        // in a 0.11 s pulse centred at swingDuration + 0.20 (span
        // [cross+0.145, cross+0.255]; the x7's observed conversion zone was
        // [cross+0.145, cross+0.28] at fz 420-524), smoothstep up then down,
        // and is exactly 1 outside. The crossing region is the record's again;
        // the tail past the s-clamp swap clamps off with the clock. At
        // pushDeliverP 1 the product is 1.0f, the landed path bit for bit.
        // WALK CYCLE 74 (b) PROBE: THE HEAD'S COUNTER ordered the P=11 /
        // half-width 0.05 bracket probe. The pulse narrowed from 0.055 to
        // 0.05 s so the dose-11 impulse (P-1)*HalfS = 0.50 (mean 0.5 over
        // the 2*HalfS span) lands just above c73's dose10 (9*0.055 = 0.495,
        // fell 31.8 s) with the peak still at 11 - the dispatcher's own
        // shape for the bracket's interior. Keep and span shift unchanged;
        // at pushDeliverP 1 the product is still exactly 1.0f and the
        // landed path bit for bit.
        const float PushFlickHalfS = 0.05f;
        const float PushFlickShiftS = 0.20f;
        float flickU = (stepClock - (swingDuration + PushFlickShiftS - PushFlickHalfS))
            / (2f * PushFlickHalfS);
        float flickScale = 0f;
        if (flickU > 0f && flickU < 1f)
        {
            float flickT = flickU < 0.5f ? flickU * 2f : (1f - flickU) * 2f;
            flickScale = flickT * flickT * (3f - 2f * flickT);
        }
        bool supportDown = swingSign > 0 ? rightDown : leftDown;
        // WALK REFLEX (27.09): the push-flick hold (ReflexHoldPush, WalkReflex):
        // a toe standing at a riser rolls against the wall, so the step that is
        // using the riser as its floor is delivered WITHOUT the amplified flick.
        // False (no component, or no toe at a riser) = the landed expression.
        // CYCLE 116: while the settle latch owns the walk the flick's
        // amplification is scaled by -poseWalkSettlePush (1 = the landed
        // expression): the arrest step must not deliver the walk's own push.
        // The reflex hold path and the no-delivery path are untouched.
        float pushMax = walkSettleLatch && walkSettlePush < 1f
            ? 1f + (pushDeliverP - 1f) * walkSettlePush : pushDeliverP;
        float pushPFactor = pushDeliverP > 1f && supportDown && !ReflexHoldPush
            ? Mathf.Lerp(1f, pushMax, flickScale) : 1f;
        // WALK REFLEX v2 (28.09): the rise's own delivery REPLACES the amplified
        // flick on a leg that carries it - the roll onto the toe and the climb's
        // own push are the same instant on the same ankle, and the amplified roll
        // is the one a rise does not want (i5 in 8.4, the flick hold of 8.3).
        // 0 (no step-up) = the landed expression, both legs.
        float flickL = ReflexStepUpL > 0f ? 1f : pushPFactor;
        float flickR = ReflexStepUpR > 0f ? 1f : pushPFactor;
        // CYCLE 116/117: the arrest step's stance bend floor (see the fields).
        // C116's constant angle is kept as the base floor; on top of it the
        // C117 load term adds give while the latch owns the walk: the hot
        // sole's load above walkSettleBendBar body-weights buys
        // walkSettleBendLoad deg per weight, capped at walkSettleBendCap. Off
        // (both zero) the floor is negative infinity and Mathf.Max is the
        // landed target, bit for bit.
        float kneeFloorDeg = walkSettleLatch && walkSettleBendDeg > 0.001f
            ? walkSettleBendDeg : float.NegativeInfinity;
        if (walkSettleLatch && walkSettleBendLoad > 0.001f)
        {
            float fzHot = Mathf.Max(poseGroundFzLeftN, poseGroundFzRightN);
            float bwN = Mathf.Max(1f, PoseRigWeightN());
            float give = walkSettleBendLoad * Mathf.Max(0f, fzHot / bwN - walkSettleBendBar);
            give = Mathf.Min(give, walkSettleBendCap);
            if (give > 0.001f && give > kneeFloorDeg) kneeFloorDeg = give;
        }
        float kneeLOut = Mathf.Max(leftLeg.knee, kneeFloorDeg);
        float kneeROut = Mathf.Max(rightLeg.knee, kneeFloorDeg);
        DriveOneLeg(true, leftLeg.hip, kneeLOut, true, leftLeg.ankle,
            stanceHoldLive && !leftIsSwing, leftIsSwing ? 1f : flickL,
            stepUpFactor: ReflexStepUpL);
        DriveOneLeg(false, rightLeg.hip, kneeROut, true, rightLeg.ankle,
            stanceHoldLive && leftIsSwing, leftIsSwing ? flickR : 1f,
            stepUpFactor: ReflexStepUpR);
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
                             bool driveAnkle, float ankleDeg, bool holdStiff = false,
                             float anklePFactor = 1f, int owner = BalanceController.TapOwnerNone,
                             float stepUpFactor = 0f)
    {
        HingeJoint2D hip = left ? leftHipJoint : rightHipJoint;
        Muscle hipFlexor = left ? balance.leftHipFlexor : balance.rightHipFlexor;
        Muscle hipExtensor = left ? balance.leftHipExtensor : balance.rightHipExtensor;
        ref float hipInertia = ref (left ? ref leftHipInertia : ref rightHipInertia);
        // WALK REFLEX v2 (28.09): the rise's step-up delivery reaches the hip only
        // through ReflexStepUpHipShare (default 0, i.e. the landed pair). 0 = the
        // landed expression bit for bit.
        float hipP = stepUpFactor > 0f && ReflexStepUpHipShare > 0f
            ? balance.hipPGain * (1f + stepUpFactor * ReflexStepUpHipShare) : balance.hipPGain;
        if (!float.IsNaN(hipDeg))
        {
            DriveJoint(hip, hipDeg, hipP, balance.hipDGain,
                balance.errorReferenceDegrees, hipFlexor, hipExtensor, ref hipInertia);
            // KICK CYCLE k2: the strike hands its targets to THIS driver, so the
            // row's own owner code is stamped here too - the kick's three joints
            // read its owner for exactly the frames this path drove them.
            NoteOwner(hip, hipDeg, owner);
            HipDbgWrite(left ? "legL" : "legR", mixer.lastDemand, hipDeg, hip, hipFlexor, hipExtensor);
            // ROUND 37: the arbitration's own read - the leg drive's hip demand
            // from THIS frame (mixer.lastDemand was just set by the DriveJoint
            // call above; the stamp lets DriveHipsArb refuse a stale capture).
            if (left) legHipSigL = mixer.lastDemand; else legHipSigR = mixer.lastDemand;
            legHipSigT = Time.fixedTime;
        }

        HingeJoint2D knee = left ? leftKneeJoint : rightKneeJoint;
        Muscle kneeFlexor = left ? balance.leftKneeFlexor : balance.rightKneeFlexor;
        Muscle kneeExtensor = left ? balance.leftKneeExtensor : balance.rightKneeExtensor;
        ref float kneeInertia = ref (left ? ref leftKneeInertia : ref rightKneeInertia);
        // WALK CYCLE 28: the support's hold, delivered. JointStable's own algebra is
        // signal = (P*e - D*v) / (1 + damping) with the damping set by the D pair, so
        // a P multiplier IS the demand's own lever - the same error asks StanceHold-
        // DeliverP times the torque on the support's knee and ankle while the hold is
        // live, clipped at the muscle's own ceiling as every other demand is. The hip
        // keeps its own pair: the hip is the trunk-correction channel the library
        // owns, not a sole-height lever (cycle 27's key moved the ankle and the knee).
        float kneeP = holdStiff
            ? balance.kneePGain * PoseLibrary.StanceHoldDeliverP : balance.kneePGain;
        // WALK REFLEX v2 (28.09): the rise's own step-up delivery (ReflexStepUpL/R,
        // WalkReflex): while a sole stands ON the top surface of a rise this factor
        // scales the support's delivery - the knee's drive and, through the shared
        // kneeP below, the ankle's plantar push. 0 = the landed expression bit for
        // bit.
        // WALK REFLEX v2b (28.09): ReflexStepUpAnkle (1 = the ship) is the share of
        // the boost the ankle's own P keeps: the ankle rides Lerp(no-boost, boosted,
        // share). At 1 or with the factor 0 the expression is the landed one.
        float kneePNoBoost = kneeP;
        if (stepUpFactor > 0f) kneeP *= 1f + stepUpFactor;
        float ankleKneeP = stepUpFactor > 0f
            ? Mathf.Lerp(kneePNoBoost, kneeP, Mathf.Clamp01(ReflexStepUpAnkle)) : kneeP;
        DriveJoint(knee, kneeDeg, kneeP, balance.kneeDGain,
            balance.errorReferenceDegrees, kneeFlexor, kneeExtensor, ref kneeInertia);
        NoteOwner(knee, kneeDeg, owner);

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
            // WALK CYCLE 71: the push-off's own delivery factor rides the ankle
            // only (1 by default: the landed expression bit for bit; see the
            // pushPFactor at the walk's own call sites).
            // WALK REFLEX v2b (28.09): ankleKneeP is the same boosted kneeP at the
            // ship's ReflexStepUpAnkle = 1; at 0 the ankle keeps its landed share.
            DriveJoint(ankle, ankleDeg, ankleKneeP * anklePFactor, balance.kneeDGain,
                balance.errorReferenceDegrees, ankleFlexor, ankleExtensor, ref ankleInertia);
            NoteOwner(ankle, ankleDeg, owner);
        }
    }

    // KICK CYCLE k2: the strike's own owner stamp - called only with an owner
    // other than TapOwnerNone (the kick's call site passes it), so every other
    // caller of DriveOneLeg is a no-op here.
    private void NoteOwner(HingeJoint2D joint, float targetDeg, int owner)
    {
        if (owner == BalanceController.TapOwnerNone || balance == null) return;
        int idx = balance.LegJointIndex(joint);
        if (idx >= 0) balance.NoteLegAngle(idx, targetDeg, mixer.lastDemand, owner);
    }

    // The level-1 stand's own balance signal, verbatim (BalanceController
    // .UpdateState: clamp(comProportionalGain * CoM-offset/comOffsetReference +
    // comDerivativeGain * bodyRate/tiltSpeedReference, ±maxBalanceSignal)). The
    // CoM offset is read the way this path's ankle loop reads it (comX minus the
    // estimator's support centre) and the rate is the VESTIBULAR frame's own body
    // angular velocity - the same source the level-1 law reads. Reached only
    // while the kick's shift level is up.
    private float KickShiftSignal(float comX)
    {
        float centre = state != null ? state.supportCenterX : comX;
        float offsetN = (comX - centre) / Mathf.Max(0.01f, balance.comOffsetReference);
        float rate = vestibular != null ? vestibular.GetBodyAngularVelocity() : 0f;
        float rateN = rate / Mathf.Max(1f, balance.tiltSpeedReference);
        return Mathf.Clamp(balance.comProportionalGain * offsetN
            + balance.comDerivativeGain * rateN,
            -balance.maxBalanceSignal, balance.maxBalanceSignal);
    }

    // ─── shared joint drives ───────────────────────────────────────────────
    // k9: the wind-up term's own reference point. kickWindRef 0 (default) = the
    // standing foot's plate centre - the point the k6/k7 delivery regulates
    // toward while the inter-stroke window is up; 1 = the support hull's own
    // centre (state.supportCenterX), the loop's own default reference. The
    // reference is the ONLY thing this knob moves; the term's bounded gain, the
    // k7 latch modes and the release are untouched. Read on the window's own
    // rows either way, so both settings are the same code path.
    private float KickWindRefPoint(Vector2 standFoot)
    {
        if (kickWindRef > 0.5f && state != null) return state.supportCenterX;
        return standFoot.x;
    }

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

    // Same mix as the old quiet stance: inside the band the pelvis law owns the
    // hip, past it the joint pose takes over smoothly. k19: the band may be
    // carried in by the caller (the strike's plant reads its own; see
    // kickPlantBand); -1 keeps the stand's band, bit for bit.
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

    // Quiet ankle loop: torque against the CoM offset, not a spring to an
    // angle. Identical to the standing controller's law, scaled by the two
    // factors above. k6: while the wind-up's own level is up the reference is
    // the STANDING foot's plate centre and both channels carry the level's own
    // scale - the wind-up's bounded gain (see the field block). The level is
    // exactly 0f on every other row and the arithmetic below is then the pre-k6
    // expression character for character (R2: the product by a literal 1f is
    // exact and the reference is the same field read).
    private void DriveAnkleBalance(float comX, float comVelX, bool leftDown, bool rightDown)
    {
        if (state == null) return;
        float refX = state.supportCenterX;
        float pScale = 1f;
        float dScale = 1f;
        if (kickWindLevel > 0.0001f)
        {
            refX = kickWindRefX;
            pScale = kickWindLevel;
            // k6's own shape, measured: the pull is the gain, the DAMPER is its
            // square root. With both channels at the gain the wind-up's own
            // window is 100x under-damped and the load split swings foot to foot
            // (kickk6q_w0.01: share 0.09 <-> 0.88 with comVel +-0.08 m/s), and
            // the stroke that lands on the swing is the one that kills the row;
            // the square root keeps the loop's own brake alive under a bounded
            // pull (0.01 -> 0.1) and still reads 0f exactly when the level does.
            dScale = Mathf.Sqrt(kickWindLevel);
        }
        float offsetN = (comX - refX) / Mathf.Max(0.01f, balance.comOffsetReference);
        float velN = comVelX / Mathf.Max(0.05f, balance.comVelocityReference);
        float command = Mathf.Clamp(
            ankleBalanceP * (balance.ankleComP * (offsetN * pScale))
            + ankleBalanceD * (balance.ankleComD * (velN * dScale)), -1f, 1f);
        // k17: THE HEAD'S OWN AUTHORITY (see the field block). While the stroke's
        // own head is up the loop's command is bounded on the FLEXOR side - the
        // push the pre-k6 law writes while the two-foot hull's centre sits ahead
        // of the CoM - at the knob's own level; the extensor side (the brake) is
        // untouched, one-sided. With the flag at its default (-1) the guard is
        // false and the command is the pre-k17 expression character for character
        // (R2: the landed rows of the lane are the proof).
        if (kickHeadPush >= 0f && balance.kickHeadActive && command < -kickHeadPush)
            command = -kickHeadPush;
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

    // walk cycle 93: gainScale rides the stand's flat-sole hold (the MTP needs real
    // authority there); every other caller passes the default and keeps the base
    // gain bit for bit (x * 1f is exact). The walk's own call is untouched.
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
        // pelvisTilt / pelvisAngVel stay in the signature for the trunk pose
        // that follows the pelvis once the walk keys are tuned.
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
        // WALK CYCLE 60: the walk's own hold inertia (the carried chain counted).
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

    // WALK CYCLE 60: the divisor's inertia with the chain counted - the joint's own body
    // and the carried child joint's body, each at its arm about this joint's pivot, plus
    // each body's own rotation - blended into the base share by walkNeckHoldShare. Cached
    // once per bind like the base (the rig does not change under the joints).
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

    // WALK CYCLE 28: the afterfall's own crawl. The deltas are the base row's own
    // measured crawl (c6w27pre_walk 13.3..15.0 s, the frames ArmDrive's presses
    // first gain traction on), the ramp is slow enough to be the body's own pace
    // (AfterfallRampSeconds) and the rock a slow in-and-out around the tuck
    // (AfterfallRockSeconds), so the legs never punch and the pose is never held
    // as a statue. Each leg's drive is a DELTA from the angle the joint held when
    // the body went down: the settle pose differs build to build (the left knee
    // reads 66 deg on the base row and 17 on keep27e), so a delta is the only way
    // the tuck has the same shape on every build. The arms are not touched here:
    // ArmDrive owns them and its presses ride the body this drive keeps moving.
    private void DriveAfterfallCrawl()
    {
        if (!afterfallCrawlOn)
        {
            afterfallCrawlOn = true;
            afterfallStartTime = Time.fixedTime;
            afterfallHipL = leftHipJoint != null ? leftHipJoint.jointAngle : 0f;
            afterfallHipR = rightHipJoint != null ? rightHipJoint.jointAngle : 0f;
            afterfallKneeL = leftKneeJoint != null ? leftKneeJoint.jointAngle : 0f;
            afterfallKneeR = rightKneeJoint != null ? rightKneeJoint.jointAngle : 0f;
            afterfallAnkleL = leftAnkleJoint != null ? leftAnkleJoint.jointAngle : 0f;
            afterfallAnkleR = rightAnkleJoint != null ? rightAnkleJoint.jointAngle : 0f;
        }
        float u = Time.fixedTime - afterfallStartTime;
        float ramp = Mathf.Clamp01(u / PoseLibrary.AfterfallRampSeconds);
        ramp = ramp * ramp * (3f - 2f * ramp);
        float rock = 0.7f + 0.3f * Mathf.Sin(2f * Mathf.PI * u / PoseLibrary.AfterfallRockSeconds);
        float tuck = ramp * rock;
        DriveOneLeg(true, afterfallHipL - PoseLibrary.AfterfallHipTuckDeg * tuck,
            afterfallKneeL + PoseLibrary.AfterfallKneeTuckDeg * tuck, true,
            afterfallAnkleL + PoseLibrary.AfterfallAnkleTuckDeg * tuck);
        DriveOneLeg(false, afterfallHipR - PoseLibrary.AfterfallHipTuckDeg * tuck,
            afterfallKneeR + PoseLibrary.AfterfallKneeTuckDeg * tuck, true,
            afterfallAnkleR + PoseLibrary.AfterfallAnkleTuckDeg * tuck);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // WALK CYCLE 40 — THE GET-UP AND THE HAND GRIP (Yury's word, 22.09.2026)
    //
    // «человек может увеличивать при необходимости трение кистей рук, имитируя,
    //  как будто бы он цепляется руками за землю либо за любой другой объект.
    //  Это может помочь для его вставания.»
    //
    // THE CARD (.cursor/tasks/GETUP-1-walk.md): while the human is Fallen and the
    // walk key is held he must try to get back on his feet, then walk again; the
    // walk input is not ignored on the ground. The measured state this cycle
    // serves: the walk's row of record topples FORWARD at ~8.5 s and the body
    // then lies face down for the rest of the 40 s run with no attempt at all.
    //
    // THE LAW. TickGetUp is the get-up: five stages, each entered on the body's
    // own MEASURED signal and each bounded by its share of the card's 10 s window
    // (a spent bound is a reported failure, never a success):
    //   Plant  the palms find the slab - ArmDrive.HandContactL && HandContactR
    //          (ArmDrive's own real-contact read, the press's own gate).
    //   Press  the palms' measured load carries the grip and the trunk comes up
    //          over them - the CoM's own height past 0.45 of the stand's.
    //   Tuck   the feet come under the body - both feet grounded and both within
    //          GripFootUnderBandM of the CoM's own x.
    //   Rise   the card's three bars together: torso tilt above -20 deg, the CoM
    //          at the standing bar (StepPhaseDriver.StandingComY, the same bar
    //          BalanceController.GetUpComAtStandHeight reads), both feet under
    //          the body. Then the walk resumes (ResetStepClock) and the walk
    //          key's own frame is Walking again.
    // The arms are NOT driven from here: ArmDrive is the one writer of the six
    // arm muscles (execution order 10, after this component), so the rise asks it
    // through ArmDrive.RequestArmPress - the interface that file publishes for
    // exactly this caller. The trunk's lever is the rig's own pelvis-upright law
    // (DriveHips, the verified stand's) once the feet are under the body, and the
    // leg tuck before that; the knees and ankles stay the pose drive's own.
    //
    // THE GRIP. Yury's instrument, and the dog's own claws are the precedent
    // (DogStanceController cycle 31 / Dog.cs SetClawGrip): while a palm presses
    // the slab during the get-up, THAT palm's collider carries the grip material
    // (Human.SetHandGrip, friction = handFriction * -poseHandGripMu, default
    // scale 3.0 on the palm's own 1.4 = 4.2, the dog's claw material's order of
    // magnitude) and the palm becomes a fixed point for the press to lever on.
    // The engage is a SIGNAL, never a key: the palm's own measured normal load in
    // body weights past GripEngageLoad engages it, the load under GripReleaseLoad
    // for -poseHandGripHold seconds releases it, and an anchored slip past
    // the tear budget TEARS it (the dog's three numbers, cycle 31/33; the budget
    // is a knob since c111 - `-poseHandGripSlip`). It acts
    // ONLY inside the get-up's window: no attempt, no grip, and every grip is
    // released when the attempt ends, the walk key is let go, or the body stands.
    // The witness is the palm collider's own material friction and the pair
    // coefficient the engine combines (PhysicsMaterial2D.GetCombinedValues), with
    // the capacity mu_pair * Fn - the columns of `<label>-getup.csv` below.
    // ═══════════════════════════════════════════════════════════════════════

    // The grip's envelope, per palm - the dog's claw constants (c31: 0.25 body
    // weights to engage on the pad's own measured load, 0.05 to release, 0.03 m
    // of anchored slip to tear) and one band this rig's own feet define.
    private const float GripEngageLoad = 0.25f;     // body weights on that palm
    private const float GripReleaseLoad = 0.05f;    // body weights
    // CYCLE 111: the tear budget became a knob - the dog's 0.03 m stays the
    // default; `-poseHandGripSlip <m>` sets how far the palm may creep before
    // the grip tears out.
    private float handGripSlipMeters = 0.03f;
    private const float GripFootUnderBandM = 0.30f; // "the feet under the body"
    // The ladder's bounds: the card's window ("within 10 s of the fall"), divided
    // over the four stages. 0.25 + 2.0 + 2.5 + 2.0 + 3.0 = 9.75 s.
    private const float GetUpSettleSeconds = 0.25f;
    private const float GetUpPlantSeconds = 2.0f;
    private const float GetUpPressSeconds = 2.5f;
    private const float GetUpTuckSeconds = 2.0f;
    private const float GetUpRiseSeconds = 3.0f;
    // ROUND 18: the roll's own bound. The exit signal is the pose itself (off
    // the back hemisphere); the bound is the failure report it was written as.
    // v2 (the fx150 measurement): a real rolling body needs ~2-4 s to carry
    // 100+ deg; 1.5 s cut the crossing short and re-entered (the loop finding).
    private const float GetUpRollSeconds = 3.0f;
    // A spent attempt leaves the body down; the key is still held, so the law
    // tries again - after this pause (the JSON's getUpAttempts counts them).
    private const float GetUpRetrySeconds = 2.0f;
    // ROUND 14 (the sitting fall): the deep-drop term of IsDown, get-up family
    // only. The sitting body measured a 0.605 m pelvis drop under a 50 deg tilt
    // gate it never clears; a crouch keeps its drop well under half a metre.
    private const float GetUpSittingDropMeters = 0.50f;
    // The tuck's own doses: cycle 28's measured crawl deltas (hip 18 / knee 16 /
    // ankle 12 deg) deepened toward the drawing's knees-under-the-body, in the
    // rig's own conventions (hip minus = flexion, knee plus = flexion, ankle plus
    // = toe down). Bounded by the joints' own limits through ClampLeg/ActuatorMixer.
    private const float GetUpHipDrawDeg = 45f;
    private const float GetUpKneeDrawDeg = 55f;
    private const float GetUpAnkleDrawDeg = 20f;
    // The trunk's own targets through the ladder, deg of world tilt (minus =
    // forward): from the pose the body landed in, over the hands (Press), to
    // nearly upright (Rise). The lumbar law chases these; nothing else does.
    private const float GetUpTrunkPressDeg = -45f;
    private const float GetUpTrunkTuckDeg = -40f;
    private const float GetUpTrunkRiseDeg = -10f;

    private bool GetUpActive()
    {
        return getUpEnabled && !getUpDone
            && (getUpStage != GetUpStage.Idle || getUpDriveOn);
    }

    // The whole ladder, one writer per joint, called from Tick's down branch.
    // ─── the fall guard (walk cycle 45) ────────────────────────────────────
    // Called from Tick before the down test. It owns nothing but ArmDrive's
    // fallGuardReach and the press request, and it never returns early: the walk's
    // own leg drive keeps ticking under it until the landed block takes the body,
    // which is why a guard row's pre-fall frames are the record's own until the
    // very frame the state arms the guard.
    private void TickFallGuard(float tilt, float pelvisY, float pelvisVelY)
    {
        // The same trap the get-up hit in cycle 40 (see TickGetUp): Human.cs adds
        // this component BEFORE it adds ArmDrive, so the Initialize-time lookup
        // found nothing. A guard that trusted it would run its whole state with the
        // arms never asked - measured, the first build of this cycle was inert (the
        // row byte-identical to the record). The lookup is a GetComponent on this
        // same object, so it is the same reference the recorder reads.
        if (armDrive == null) armDrive = GetComponent<ArmDrive>();
        float dt = Time.fixedDeltaTime;
        if (dt <= 0f) dt = 0.005f;
        int state = balance != null ? balance.GeometricFallState : BodySupportGeometry.Supported;
        // WALK CYCLE 51 (R1): the pair. The ladder says the body has left its
        // support; the descent rate says it is really going down rather than
        // wobbling over a bump. Only together do they arm the guard.
        bool goingOver = state >= BodySupportGeometry.Critical
            && pelvisVelY <= FallGuardDescendMetersPerSecond;
        fallGuardStateAge = goingOver ? fallGuardStateAge + dt : 0f;

        if (fallGuardLive && (!goingOver || IsDown(tilt, pelvisY))) EndFallGuard();
        if (!fallGuardLive && goingOver && fallGuardStateAge >= fallGuardHoldSeconds)
        {
            fallGuardLive = true;
            fallGuardFacing = intent != null && Mathf.Abs(intent.moveX) > 0.01f
                ? Mathf.Sign(intent.moveX)
                : 1f;
            if (armDrive != null)
                armDrive.fallGuardReach = fallGuardFacing * fallGuardReachMeters;
        }
        if (fallGuardLive && armDrive != null) armDrive.RequestArmPress(FallGuardPressLevel);
    }

    // 0 IS OFF: ArmDrive's own default, the value every pre-existing line leaves
    // in place, and the value the landed press machinery needs to behave exactly
    // as it did (a latched point whose stroke is allowed to start).
    private void EndFallGuard()
    {
        fallGuardLive = false;
        fallGuardStateAge = 0f;
        if (armDrive != null) armDrive.fallGuardReach = 0f;
    }

    private void TickGetUp(float tilt, float pelvisTilt, float pelvisAngVel, float pelvisY,
                           float pelvisVelY, float comX, float comVelX,
                           bool leftDown, bool rightDown,
                           float leftFootX, float rightFootX, float leftFootY, float rightFootY)
    {
        float dt = Time.fixedDeltaTime;
        if (dt <= 0f) dt = 0.005f;
        float now = Time.fixedTime;
        HipDbgFrame("pre");   // ROUND 36 (probe): the entry state
        // WALK CYCLE 40: ArmDrive and StepPhaseDriver are resolved LAZILY, on the
        // first get-up step, because Human.cs adds this component before it adds
        // ArmDrive (Human.cs: AddComponent<PoseDriveController>() then
        // AddComponent<ArmDrive>()), so the Initialize-time lookup found nothing
        // and the first build of this cycle ran its whole ladder with the arms
        // never asked and the standing CoM bar unread. The lookup is a GetComponent
        // on this same object, so it is the same reference the recorder reads.
        if (armDrive == null) armDrive = GetComponent<ArmDrive>();
        if (stepDriver == null) stepDriver = GetComponent<StepPhaseDriver>();
        // ROUND 18 v2: the back-read tracker (the dwell's clock; a clean
        // off-back moment re-arms the roll cap).
        UpdateBackTracker(now);
        bool down = IsDown(tilt, pelvisY);
        // c99 AUTO RISE (owner's addendum 01.10): the card's own input grows a
        // self-press - a keyless down held -autoGetUp D s reads as the key.
        bool walkKey = GetUpKeyOn(down);
        float comY = comCalculator != null ? comCalculator.GetCenterOfMass().y : 0f;
        float standComY = stepDriver != null && stepDriver.HasStandingComY
            ? stepDriver.StandingComY : float.NaN;
        float pelvisH = pelvisY - groundY;
        float standH = standPelvisY - groundY;

        // ─── THE CARD'S THREE QUANTITIES, on this frame ───
        //   (1) the torso tilt above -20 deg, the recorder's own torsoTilt sign;
        //   (2) the CoM at the standing bar - StepPhaseDriver's settled stand CoM
        //       (the bar BalanceController.GetUpComAtStandHeight uses: com.y +
        //       0.05 >= StandingComY);
        //   (3) both feet under the body: grounded AND within GripFootUnderBandM
        //       of the CoM's own x.
        bool tiltBar = tilt > getUpStandTiltDeg;
        bool comBar = !float.IsNaN(standComY) && comY + 0.05f >= standComY;
        bool leftUnder = Mathf.Abs(leftFootX - comX) <= GripFootUnderBandM;
        bool rightUnder = Mathf.Abs(rightFootX - comX) <= GripFootUnderBandM;
        bool feetBar = leftUnder && rightUnder && leftDown && rightDown;
        bool standing = tiltBar && comBar && feetBar;
        // ROUND 46: the drag's per-frame readings (the seat machine + the trace).
        seatGapL = comX - leftFootX;
        seatGapR = comX - rightFootX;
        seatBodyTilt = tilt;

        if (down && getUpDownSince < 0f)
        {
            getUpDownSince = now;
            if (getUpFallTime < 0f) getUpFallTime = now;
            if (balance != null && balance.getUpFallTime < 0f)
                balance.getUpFallTime = getUpFallTime;
            OpenGetUpTrace();
        }
        float downAge = getUpDownSince >= 0f ? now - getUpDownSince : 0f;

        // ─── THE CARD'S OWN INPUT: no walk key, no rise ───
        if (!walkKey)
        {
            EndAttempt();
            if (balance != null)
            {
                balance.currentState = BalanceController.BalanceState.Falling;
                balance.locomotionState = BalanceController.LocomotionState.Fallen;
            }
            RelaxAll();
            WriteGetUpTrace(tilt, pelvisY, pelvisH, standH, comX, comY, standComY,
                leftFootX, rightFootX, leftFootY, rightFootY, leftDown, rightDown,
                leftUnder, rightUnder, tiltBar, comBar, feetBar, 0f);
            return;
        }

        // ─── the fall's own frames, then the first stage ───
        if (getUpStage == GetUpStage.Idle)
        {
            // ROUND 43: THE SIT-STAND FIRE. The window's clock (Tick) has held
            // -poseGetUpSitStandHold s; the conversion starts here: straight to
            // the Rise with the pose latched as it sits (no settle, no plant, no
            // tuck - the sit IS the base). The retry pause is the DOWN route's
            // guard and does not gate this entry.
            if (getUpSitStandOn && getUpSitStandT >= getUpSitStandHold
                && SitStandWindow(tilt, leftDown, rightDown, pelvisY, pelvisVelY))
            {
                BeginSitAttempt(tilt, now, pelvisY);
            }
            else if (downAge < GetUpSettleSeconds || now < getUpRetryAfter)
            {
                // The settle: the ladder needs Falling to be tellable from Fallen
                // (the card's five states), so the fall's own frames are named
                // Falling, and a body waiting out the retry pause is Fallen.
                if (balance != null)
                {
                    balance.currentState = BalanceController.BalanceState.Falling;
                    balance.locomotionState = downAge < GetUpSettleSeconds
                        ? BalanceController.LocomotionState.Falling
                        : BalanceController.LocomotionState.Fallen;
                }
                RelaxAll();
                // The body is not a statue between attempts either: the measured
                // crawl of cycle 28 keeps the presses something to find purchase
                // in (it moves the legs only; the arms are ArmDrive's).
                DriveAfterfallCrawl();
                WriteGetUpTrace(tilt, pelvisY, pelvisH, standH, comX, comY, standComY,
                    leftFootX, rightFootX, leftFootY, rightFootY, leftDown, rightDown,
                    leftUnder, rightUnder, tiltBar, comBar, feetBar, 0f);
                return;
            }
            else
            {
                BeginAttempt(tilt, now);
            }
        }

        // ─── ROUND 18: the roll re-entry (the body tossed back onto its back
        // mid-ladder). The plant/press may not claim progress while supine: the
        // ladder goes back to the roll and re-earns the plant off the back.
        if (getUpRollEnabled && !getUpRollBlocked && getUpStage != GetUpStage.Roll
            && BodyOnBack() && now - getUpBackSince >= getUpRollDwell)
        {
            GetUpStage wasStage = getUpStage;
            getUpTrunkStartDeg = tilt;      // the ramp starts from where the body is now
            getUpHandsPlanted = false;      // the plant is re-earned after the roll
            SetGetUpStage(GetUpStage.Roll, now);
            Debug.Log("GETUP-ROLL enter t=" + N(now, 3) + " tilt=" + N(tilt, 2)
                + " from=" + wasStage + " heldFor=" + N(now - getUpBackSince, 2)
                + " why=flip");
        }

        float stageAge = now - getUpStageEnterTime;
        bool planted = armDrive != null && armDrive.HandContactL && armDrive.HandContactR;
        if (planted) getUpHandsPlanted = true;
        if (feetBar) getUpReachedTuck = true;

        // ─── ROUND 32: THE SETTLE CLOCK (the base must hold still) ───
        // The r31 fastpath fired the moment the state merely APPEARED - and on
        // this deck the state only appears mid-motion (the feet sweep under
        // while the body falls or is tossed), so every fire caught an impact
        // and the drives turned it into a bounce. The fire now needs the base
        // to HOLD: the state, unbroken, with the pelvis's own vertical speed
        // quiet, for -poseGetUpBaseSettle seconds. Refusals are counted and
        // named (GETUP-BASE settle-block).
        bool wouldFire = planted && feetBar && !getUpBaseFast
            && (getUpStage == GetUpStage.Press || getUpStage == GetUpStage.Tuck);
        bool settleOk = true;
        if (getUpBaseSettleOn && getUpBaseOn)
        {
            bool settleQuiet = wouldFire && Mathf.Abs(pelvisVelY) <= getUpBaseSettleVel;
            if (getUpBaseSettleDutyWin > 0f)
            {
                // ROUND 33: THE DUTY FORM - the quiet fraction over the last W s.
                // The ring keeps (t, quiet s) per frame; evict past the window.
                int cap = GetUpSettleDutyCap;
                if (getUpSettleDutyCount == cap)
                    getUpSettleDutySum -= getUpSettleDutyRingQ[getUpSettleDutyHead];
                else
                    getUpSettleDutyCount++;
                getUpSettleDutyRingT[getUpSettleDutyHead] = now;
                getUpSettleDutyRingQ[getUpSettleDutyHead] = settleQuiet ? dt : 0f;
                getUpSettleDutySum += getUpSettleDutyRingQ[getUpSettleDutyHead];
                getUpSettleDutyHead = (getUpSettleDutyHead + 1) % cap;
                int tail = (getUpSettleDutyHead - getUpSettleDutyCount + cap) % cap;
                while (getUpSettleDutyCount > 1
                    && now - getUpSettleDutyRingT[tail] > getUpBaseSettleDutyWin)
                {
                    getUpSettleDutySum -= getUpSettleDutyRingQ[tail];
                    tail = (tail + 1) % cap;
                    getUpSettleDutyCount--;
                }
                float dutSpan = Mathf.Max(now - getUpSettleDutyRingT[tail], 0.0001f);
                getUpBaseSettleDuty = Mathf.Clamp01(getUpSettleDutySum / dutSpan);
                if (getUpSettleDutyAge < getUpBaseSettleDutyWin) getUpSettleDutyAge += dt;
                settleOk = settleQuiet && getUpSettleDutyAge >= getUpBaseSettleDutyWin
                    && getUpBaseSettleDuty >= getUpBaseSettleDutyFrac;
            }
            else
            {
                // ROUND 33: THE DEBOUNCE - a dropout <= Drop s does not reset the
                // settle clock (the clock keeps running through the blink).
                if (settleQuiet) { getUpBaseSettleGap = 0f; getUpBaseSettleT += dt; }
                else
                {
                    getUpBaseSettleGap += dt;
                    if (getUpBaseSettleDrop > 0f
                        && getUpBaseSettleGap <= getUpBaseSettleDrop)
                        getUpBaseSettleT += dt;
                    else getUpBaseSettleT = 0f;
                }
                settleOk = getUpBaseSettleT >= getUpBaseSettleSec;
            }
            if (wouldFire && !settleOk && !getUpBaseSettleWarned)
            {
                getUpBaseSettleWarned = true;
                getUpBaseSettleBlocks++;
                Debug.Log("GETUP-BASE settle-block t=" + N(now, 3)
                    + " v=" + N(pelvisVelY, 2) + " dwell=" + N(getUpBaseSettleT, 2)
                    + " duty=" + N(getUpBaseSettleDuty, 2));
            }
            if (!wouldFire || settleOk) getUpBaseSettleWarned = false;
        }

        // ─── ROUND 35: THE LOAD GATE (the base must be a LOADED stance) ───
        // The pose-flag base (feet under + grounded + palms) exists while the soles
        // carry almost nothing; the fire that starts the extension must wait until
        // both soles really carry -poseGetUpBaseLoad x weight, held for
        // -poseGetUpBaseLoadSec s (a dropout <= -poseGetUpBaseLoadDrop s does not
        // reset the clock). The refusal logs the live fz values, so a refusal
        // episode is a measurement, not a stall. Default 0: loadOk stays true.
        bool loadOk = true;
        if (getUpBaseOn && getUpBaseLoadFrac > 0f)
        {
            float loadThr = getUpBaseLoadFrac * PoseRigWeightN();
            bool bothLoaded = poseGroundFzLeftN >= loadThr && poseGroundFzRightN >= loadThr;
            if (bothLoaded) { getUpBaseLoadGap = 0f; getUpBaseLoadT += dt; }
            else
            {
                getUpBaseLoadGap += dt;
                if (getUpBaseLoadDrop > 0f && getUpBaseLoadGap <= getUpBaseLoadDrop)
                    getUpBaseLoadT += dt;
                else getUpBaseLoadT = 0f;
            }
            loadOk = getUpBaseLoadT >= getUpBaseLoadSec;
            if (wouldFire && !loadOk && !getUpBaseLoadWarned)
            {
                getUpBaseLoadWarned = true;
                getUpBaseLoadBlocks++;
                Debug.Log("GETUP-BASE load-block t=" + N(now, 3)
                    + " fzL=" + N(poseGroundFzLeftN, 0) + " fzR=" + N(poseGroundFzRightN, 0)
                    + " thr=" + N(loadThr, 0) + " dwell=" + N(getUpBaseLoadT, 2));
            }
            if (!wouldFire || loadOk) getUpBaseLoadWarned = false;
        }

        // ─── ROUND 39: THE LOADED-WINDOW EXTENSION (the r38 find) ───
        // The Tuck's fold-unwind loads both soles for ~1 s; the wait's re-fold
        // then unloads them and every fire fires off a fz=0 kneel. With the
        // device armed the window is KEPT (the Tuck case suppresses the re-fold
        // while it is live), its open/close is logged by name, and the Rise
        // fires on the window itself - the soles' sum at the fire floor for the
        // window so far, whatever the pose flags read. Default off: nothing below
        // runs, every clock stays 0.
        bool loadExtFireReady = false;
        if (getUpBaseOn && getUpLoadExtOn && getUpStage == GetUpStage.Tuck)
        {
            float keepThr = getUpLoadExtKeep * PoseRigWeightN();
            float fireThr = getUpLoadExtFire * PoseRigWeightN();
            bool bothKeep = poseGroundFzLeftN >= keepThr && poseGroundFzRightN >= keepThr;
            bool sumHot = (poseGroundFzLeftN + poseGroundFzRightN) >= fireThr;
            if (bothKeep)
            {
                if (!getUpLoadExtLive)
                {
                    getUpLoadExtWindows++;
                    Debug.Log("GETUP-LOADEXT enter t=" + N(now, 3) + " n=" + getUpLoadExtWindows
                        + " fzL=" + N(poseGroundFzLeftN, 0) + " fzR=" + N(poseGroundFzRightN, 0)
                        + " keep=" + N(keepThr, 0) + " fire=" + N(fireThr, 0));
                }
                getUpLoadExtLive = true;
            }
            else if (getUpLoadExtLive)
            {
                getUpLoadExtLive = false;
                Debug.Log("GETUP-LOADEXT exit t=" + N(now, 3)
                    + " fzL=" + N(poseGroundFzLeftN, 0) + " fzR=" + N(poseGroundFzRightN, 0)
                    + " held=" + N(getUpLoadExtT, 2));
            }
            if (bothKeep && sumHot) { getUpLoadExtGap = 0f; getUpLoadExtT += dt; }
            else if (getUpLoadExtT > 0f)
            {
                getUpLoadExtGap += dt;
                if (getUpLoadExtDrop > 0f && getUpLoadExtGap <= getUpLoadExtDrop)
                    getUpLoadExtT += dt;
                else getUpLoadExtT = 0f;
            }
            loadExtFireReady = getUpLoadExtT >= getUpLoadExtSec;
        }
        else
        {
            getUpLoadExtLive = false;
        }
        // ─── ROUND 39: THE LOADED-WINDOW FIRE ───
        // The Rise fires on the window itself (not the flag combo): a Tuck whose
        // soles have carried the sum floor for the sec seconds. The state at the
        // fire is logged (pl / fb included) so the route is a measurement.
        if (getUpBaseOn && getUpLoadExtOn && !getUpBaseFast && loadExtFireReady
            && getUpStage == GetUpStage.Tuck)
        {
            getUpBaseFast = true;
            getUpLoadExtFires++;
            Debug.Log("GETUP-BASE fastpath t=" + N(now, 3) + " from=Tuck via=loadext held="
                + N(getUpLoadExtT, 2) + " fzL=" + N(poseGroundFzLeftN, 0) + " fzR="
                + N(poseGroundFzRightN, 0) + " pl=" + (getUpHandsPlanted ? "1" : "0")
                + " fb=" + (feetBar ? "1" : "0") + " comX=" + N(comX, 3));
            getUpPushTraceT0 = now;
            // ROUND 40b: the fire-time re-latch. The attempt began at the crash
            // landing, so the stored latch is the near-straight landing pose; the
            // Rise hold would yank the folded loaded Tuck toward it (the r40
            // trace: hip -90 -> -5 in the hold at full torque = the launch).
            // Re-captured here, the hold holds and the blend starts from the fold.
            if (getUpLoadExtRelatch)
            {
                getUpHipL = leftHipJoint != null ? leftHipJoint.jointAngle : getUpHipL;
                getUpHipR = rightHipJoint != null ? rightHipJoint.jointAngle : getUpHipR;
                getUpKneeL = leftKneeJoint != null ? leftKneeJoint.jointAngle : getUpKneeL;
                getUpKneeR = rightKneeJoint != null ? rightKneeJoint.jointAngle : getUpKneeR;
                getUpAnkleL = leftAnkleJoint != null ? leftAnkleJoint.jointAngle : getUpAnkleL;
                getUpAnkleR = rightAnkleJoint != null ? rightAnkleJoint.jointAngle : getUpAnkleR;
                Debug.Log("GETUP-LOADEXT relatch t=" + N(now, 3)
                    + " hipL=" + N(getUpHipL, 1) + " kneeL=" + N(getUpKneeL, 1)
                    + " ankleL=" + N(getUpAnkleL, 1) + " hipR=" + N(getUpHipR, 1));
            }
            SetGetUpStage(GetUpStage.Rise, now);
            stageAge = 0f;
        }

        // ─── ROUND 31: THE FAST-PATH (the base gate's (a)) ───
        // The moment the r29 near-miss state reads - feet under AND grounded and
        // the palms planted - the Rise starts, from the Press or the Tuck, before
        // the stage bounds carry the body off its base.
        if (getUpBaseOn && !getUpBaseFast && getUpHandsPlanted && feetBar && settleOk && loadOk
            && (getUpStage == GetUpStage.Press || getUpStage == GetUpStage.Tuck))
        {
            getUpBaseFast = true;
            Debug.Log("GETUP-BASE fastpath t=" + N(now, 3) + " from=" + getUpStage
                + " pelvH=" + N(pelvisH, 3) + " comX=" + N(comX, 3));
            getUpPushTraceT0 = now;
            SetGetUpStage(GetUpStage.Rise, now);
            stageAge = 0f;
        }
        // ─── the arms: the press is REQUESTED every get-up step ───
        // 1.0 through the plant and the press (the strokes that fix the palms),
        // 0.6 on the rise (the legs and the pelvis law carry it there and the
        // palms must not be spent shoving the body along the slab).
        // ROUND 31: while the base gate WAITS (the Tuck past its bound) or the
        // Rise's contact gate HOLDS, the press drops to -poseGetUpBasePress
        // (default 0): the measured stroke lifts the body off the very feet the
        // rise must rise over.
        // ROUND 45: the sit re-seat machine (publishes seatPhase/seatPressNow
        // and the gated pull key; runs only while a seat attempt is live).
        RunSitSeat(now, dt);
        float pressAsk = getUpStage == GetUpStage.Rise ? 0.6f : 1f;
        bool baseWaiting = getUpBaseOn && getUpStage == GetUpStage.Tuck
            && stageAge >= GetUpTuckSeconds * getUpPatience;
        // ROUND 31b: with -poseGetUpBaseRise 1 the press hold covers the WHOLE
        // Rise (not only the contact-gate holds); flag off = the v1 rule.
        bool gateHolding = getUpBaseOn && getUpStage == GetUpStage.Rise
            && (getUpBaseRise || (getUpStandGate && getUpStandGateInHold));
        if (baseWaiting || gateHolding)
            pressAsk = getUpBasePress;
        // ─── ROUND 38: THE WAIT-PRESS LIFT ───
        // While the wait (or the gate hold) runs and the flag is on, the press
        // ramps 0 -> -poseGetUpWaitPress over the ramp seconds (per wait episode:
        // it restarts whenever the wait re-enters), and the fz/pose response is
        // logged every second so one take prices the lift. The flag absent keeps
        // the r31 rule, byte for byte.
        if (getUpWaitPress >= 0f && (baseWaiting || gateHolding))
        {
            if (getUpWaitPressT <= 0f)
            {
                Debug.Log("GETUP-WAITPRESS enter t=" + N(now, 3) + " level=" + N(getUpWaitPress, 2)
                    + " ramp=" + N(getUpWaitPressSec, 2) + " pelvH=" + N(pelvisH, 3)
                    + " fzL=" + N(poseGroundFzLeftN, 0) + " fzR=" + N(poseGroundFzRightN, 0));
                getUpWaitPressLogT = 1f;
            }
            getUpWaitPressT += dt;
            pressAsk = getUpWaitPress * Mathf.Clamp01(getUpWaitPressT / Mathf.Max(0.05f, getUpWaitPressSec));
            if (getUpWaitPressT >= getUpWaitPressLogT)
            {
                getUpWaitPressLogT = getUpWaitPressT + 1f;
                Debug.Log("GETUP-WAITPRESS t=" + N(now, 3) + " s=" + N(getUpWaitPressT, 2)
                    + " press=" + N(pressAsk, 2) + " pelvH=" + N(pelvisH, 3)
                    + " fzL=" + N(poseGroundFzLeftN, 0) + " fzR=" + N(poseGroundFzRightN, 0)
                    + " dn=" + (leftDown ? "1" : "0") + (rightDown ? "1" : "0"));
            }
        }
        else
        {
            getUpWaitPressT = 0f;
            getUpWaitPressLogT = 0f;
        }
        // ROUND 39: THE PRESS LOCKOUT (r38 item 2) - every priced press dose
        // during the wait killed the base; with the device armed the wait's
        // stroke is locked at 0 as a HARD gate (the soft one is
        // -poseGetUpBasePress 0), and the legs own the lift. Inert when off.
        if (getUpLoadExtOn && (baseWaiting || gateHolding)) pressAsk = 0f;
        // ROUND 45: the seat family owns the ask while its attempt runs (mode 2
        // keeps the historic Rise ask; the machine publishes -1 there).
        if (seatPhase >= 0 && seatPressNow >= 0f)
            pressAsk = seatPressNow;
        // CYCLE 100 - THE PLANT AHEAD OF THE SHOULDER (the owner's hand-plant
        // addendum). Armed for the WHOLE get-up on a prone body with the fall's
        // facing - NOT only while the ask is live: a latch taken during the
        // load-ext lockout (pressAsk forced 0) must land ahead too, or the take
        // mixes two plant rules and the hands creep back to the waist. Cleared by
        // EndGetUp; the crouch and the guard keep the landed geometry. Inert at 0.
        if (armDrive != null)
        {
            float face = intent != null && Mathf.Abs(intent.moveX) > 0.01f
                ? Mathf.Sign(intent.moveX)
                : fallGuardFacing;
            armDrive.getUpPlantReach = getUpPlantReachMeters > 0f
                ? face * getUpPlantReachMeters
                : 0f;
            armDrive.RequestArmPress(pressAsk);
        }

        // ─── the palms' own contacts: engage / hold / release / tear ───
        SampleHandGrip(dt);

        // ─── the stages, each on its measured signal and its own bound ───
        switch (getUpStage)
        {
            case GetUpStage.Roll:
                // ─── ROUND 18 (the consult: "on-back -> roll to side first") ───
                // The body is on its back: rotate it off the back hemisphere
                // toward the press corridor FIRST - the knees draw (something to
                // roll over), the trunk is ramped toward the press's own target,
                // and the palms may not latch a plant while the back reads
                // (ArmDrive holds the round-17 rest). The exit is the POSE, its
                // own measured signal; the bound is the failure report.
                DriveGetUpLegs(Mathf.Clamp01(0.5f * stageAge / (1.0f * getUpPatience)));
                DriveGetUpTrunk(getUpTrunkStartDeg, GetUpTrunkPressDeg,
                    Mathf.Clamp01(stageAge / (1.2f * getUpPatience)));
                if (!(getUpRollForce > 0.5f) && !BodyOnBack())
                {
                    getUpRollBounds = 0;        // a crossing clears the cap
                    Debug.Log("GETUP-ROLL out t=" + N(now, 3) + " reason=cross");
                    SetGetUpStage(GetUpStage.Plant, now);
                }
                else if (stageAge >= GetUpRollSeconds * getUpPatience)
                {
                    getUpRollBounds++;
                    if (getUpRollBounds >= getUpRollCap)
                        getUpRollBlocked = true;
                    Debug.Log("GETUP-ROLL out t=" + N(now, 3) + " reason=bound bounds="
                        + getUpRollBounds + " tilt=" + N(tilt, 2));
                    SetGetUpStage(GetUpStage.Plant, now);
                }
                break;

            case GetUpStage.Plant:
                // The arms plant (ArmDrive's own plant-and-press machinery) while
                // the legs begin the slow tuck - the body must not be a statue
                // under the palms. The signal is the palms' REAL contact.
                DriveGetUpLegs(Mathf.Clamp01(0.35f * stageAge / (1.5f * getUpPatience)));
                DriveGetUpTrunk(getUpTrunkStartDeg, GetUpTrunkPressDeg, Mathf.Clamp01(stageAge / (1.5f * getUpPatience)));
                if (getUpHandsPlanted || stageAge >= GetUpPlantSeconds * getUpPatience)
                    SetGetUpStage(GetUpStage.Press, now);
                break;

            case GetUpStage.Press:
                // The grip is live (the palms' load decides it) and the tuck is
                // paid: the trunk comes up over the planted hands. The measured
                // signal is the CoM's own height against the stand's.
                DriveGetUpLegs(Mathf.Clamp01(0.35f + 0.65f * stageAge / (GetUpPressSeconds * getUpPatience)));
                DriveGetUpTrunk(GetUpTrunkPressDeg, GetUpTrunkTuckDeg, Mathf.Clamp01(stageAge / (GetUpPressSeconds * getUpPatience)));
                if (getUpHandsPlanted && pelvisH >= 0.45f * standH)
                    SetGetUpStage(GetUpStage.Tuck, now);
                else if (stageAge >= GetUpPressSeconds * getUpPatience)
                    SetGetUpStage(GetUpStage.Tuck, now);   // the bound is a failure, reported
                break;

            case GetUpStage.Tuck:
                // The feet come under the body: the hips become the TRUNK's lever
                // (the stand's own pelvis-upright law), so the pelvis is what rotates
                // up over the knees.
                // ═══ WALK CYCLE 42: HUNK R1 — THE TUCK PAYS THE BASE ═══
                // The drive is no longer HELD at 1.0f. The held fold let the body roll
                // over its own head to the apex while the feet hung 0.85-1.36 m up, so
                // the designed exit (the soles under AND grounded) could never fire and
                // the 2.0 s bound handed the rise a body already on the floor (c41 §8).
                // From the depth the press left, the legs un-wind over the tuck's own
                // bound and carry the soles under and down toward the slab — the base
                // the card's third bar names. The trunk's hold, the pelvis law and the
                // exit signal are untouched; the bound is the failure report it was
                // written as. Priced as a probe: KEEP 0 (the get-up family is default-OFF).
                if (getUpBaseOn)
                {
                    // ROUND 31 (the Tuck wait, the base gate's (b)): past its
                    // bound the Tuck no longer hands the Rise a body off its
                    // base. It WAITS for the state, re-seating the feet at
                    // -poseGetUpBaseDraw; the cap is a failure report - the
                    // attempt aborts, never an escape into an extension off the
                    // base (the r30 cap-escape wart, closed here).
                    if (stageAge < GetUpTuckSeconds * getUpPatience)
                        DriveGetUpLegs(Mathf.Clamp01(1f - stageAge / (GetUpTuckSeconds * getUpPatience)));
                    else
                    {
                        getUpBaseWait += dt;
                        // ROUND 39: while a loaded window is live, the wait stops
                        // re-folding - the legs hold the unwound draw-0 state the
                        // window loaded in, so the soles keep the base the
                        // loadext fire needs. Off / no window: the r31 route.
                        bool loadExtHold = getUpLoadExtOn && getUpLoadExtLive;
                        if (loadExtHold)
                            DriveGetUpLegs(0f);
                        else if (getUpBaseSquatDeg > 0f)
                            DriveGetUpLegsSquat(getUpBaseDraw);
                        else
                            DriveGetUpLegs(getUpBaseDraw);
                        if (getUpBaseWait >= getUpBaseMax)
                        {
                            getUpBaseAborts++;
                            Debug.Log("GETUP-BASE abort t=" + N(now, 3)
                                + " wait=" + N(getUpBaseWait, 2) + " pelvH=" + N(pelvisH, 3));
                            FinishAttempt(false, now);
                            WriteGetUpTrace(tilt, pelvisY, pelvisH, standH, comX, comY, standComY,
                                leftFootX, rightFootX, leftFootY, rightFootY, leftDown, rightDown,
                                leftUnder, rightUnder, tiltBar, comBar, feetBar, stageAge);
                            return;
                        }
                    }
                    DriveGetUpTrunk(GetUpTrunkTuckDeg, GetUpTrunkTuckDeg, 1f);
                    DriveHipsGetUp(pelvisTilt, pelvisAngVel);
                    if (feetBar && getUpHandsPlanted && settleOk && loadOk)
                        SetGetUpStage(GetUpStage.Rise, now);
                }
                else
                {
                    DriveGetUpLegs(Mathf.Clamp01(1f - stageAge / (GetUpTuckSeconds * getUpPatience)));
                    DriveGetUpTrunk(GetUpTrunkTuckDeg, GetUpTrunkTuckDeg, 1f);
                    DriveHipsGetUp(pelvisTilt, pelvisAngVel);
                    if ((feetBar && getUpHandsPlanted) || stageAge >= GetUpTuckSeconds * getUpPatience)
                        SetGetUpStage(GetUpStage.Rise, now);
                }
                break;

            case GetUpStage.Rise:
                // The legs extend under the body (the tuck unwinds toward the pose
                // the body landed in, a straighter chain under a climbing pelvis),
                // the trunk is asked to be nearly upright, and the pelvis law keeps
                // the hips working. The card's three bars end the ladder.
                float riseClock = stageAge;           // ROUND 30: the bound's own clock
                float riseShare = Mathf.Clamp01(stageAge / (GetUpRiseSeconds * getUpPatience));
                float trunkGovBiasNow = 0f;           // ROUND 42: the trunk governor's bias
                // ─── ROUND 43: THE SIT-STAND EXTENSION ───
                // While a sit-fired attempt runs and the window holds, the bound's
                // clock is deferred - the extension keeps working over the state
                // the fire was earned from - up to -poseGetUpSitStandMax s. The
                // window ends (or the cap lands): the historic bound resumes.
                if (getUpSitStandOn && getUpSitActive)
                {
                    // ROUND 43c: the clock stays RAW - the blend RUNS over the
                    // live window (wave 2 pinned riseShare with the bound and the
                    // extension only started after the window broke). Only the
                    // BOUND defers; see the bound test below.
                    bool sitWinLive = SitStandWindow(tilt, leftDown, rightDown, pelvisY, pelvisVelY);
                    getUpSitWinNow = sitWinLive;   // the trace reads the TRUE window
                    if (sitWinLive && getUpSitStandExt < getUpSitStandMax)
                        getUpSitStandExt += dt;
                    if (balance != null && getUpSitStandExt > balance.getUpSitExtMax)
                        balance.getUpSitExtMax = getUpSitStandExt;
                }
                // ROUND 47: THE ARC'S CLOCK. It only counts while the extension
                // is LIVE (the fold is done) and the torso pitch-rate is inside
                // the gate; outside the gate it freezes (the leg blend holds its
                // pose - the ask cannot feed its own runaway).
                getUpTracePelvVel = pelvisVelY;
                if (getUpSitActive && getUpSitArcSec > 0.001f)
                {
                    float arcInst = (tilt - getUpSitArcPrevTilt) / Mathf.Max(1e-5f, dt);
                    getUpSitArcPrevTilt = tilt;
                    getUpSitArcTiltRate += (arcInst - getUpSitArcTiltRate) * Mathf.Clamp01(20f * dt);
                    bool arcLive = getUpSitPullKey >= 0.999f;
                    bool arcCalm = getUpSitArcRate <= 0f
                        || Mathf.Abs(getUpSitArcTiltRate) <= getUpSitArcRate;
                    // CYCLE 106: THE ROCK GATE. Armed, the ext clock waits for
                    // the torso's rock to complete - the legs stay folded while
                    // the torso comes up (the sit-up first, then the extension).
                    // Off (or no rock): the historic condition, byte for byte.
                    bool rockReady = getUpSitRockGate < 0.5f
                        || getUpSitRockSec <= 0.001f
                        || getUpSitRockClock >= getUpSitRockSec;
                    // CYCLE 107: THE CO-OVER GATE. Armed, the ext clock also
                    // waits for the CoM to be over the feet (no foot ahead of
                    // it by more than the band) - the fold holds while the
                    // body is still behind its support. Off: byte for byte.
                    bool extOverReady = true;
                    if (getUpSitExtOver > 0.001f)
                        extOverReady = Mathf.Min(seatGapL, seatGapR) >= -getUpSitExtOver;
                    if (arcLive && arcCalm && rockReady && extOverReady)
                    {
                        if (getUpSitArcClock < 30f) getUpSitArcClock += dt;
                        getUpSitArcInHold = false;
                    }
                    else if (arcLive)
                    {
                        getUpSitArcHeld += dt;
                        if (!getUpSitArcInHold)
                        {
                            getUpSitArcInHold = true;
                            getUpSitArcHolds++;
                        }
                    }
                }
                if (getUpStandMode)
                {
                    // ROUND 29: hold the fold first (the soles ride under the
                    // body), then converge the legs on the stand pose. v2: the
                    // hold and the blend may start from the LATCHED pose (draw 0,
                    // the feet-under state the crawl leaves) instead of the fold.
                    //
                    // ROUND 30: THE CONTACT-GATED RISE - the clock holds while the
                    // soles are not both grounded, so the extension can only
                    // advance OVER the planted feet (the r29 near-miss lifted them
                    // exactly as the rise accelerated). The blend, the trunk and
                    // the bound all ride the gated clock; -poseGetUpStandGateMax
                    // caps the hold. Default off: riseClock == stageAge.
                    if (getUpStandGate)
                    {
                        if (!(leftDown && rightDown) && getUpStandGateHeld < getUpStandGateMax)
                        {
                            getUpStandGateHeld = Mathf.Min(getUpStandGateMax, getUpStandGateHeld + dt);
                            if (!getUpStandGateInHold)
                            {
                                getUpStandGateInHold = true;
                                getUpStandGateHolds++;
                            }
                        }
                        else if (leftDown && rightDown)
                        {
                            getUpStandGateInHold = false;
                        }
                        riseClock = Mathf.Max(0f, stageAge - getUpStandGateHeld);
                        riseShare = Mathf.Clamp01(riseClock / (GetUpRiseSeconds * getUpPatience));
                    }
                    // ─── ROUND 42: THE MOVE-GUARD ───
                    // The r41 finding: the practice's only conversion sat inside a
                    // deck HOLD while the sweeps excited the body onto its feet -
                    // and the same sweeps throw a half-risen body. With the flag on
                    // the rise extension's clock does not advance while a deck's
                    // surface is MOVING (TiltPlatform.AnyMoving), so the blend runs
                    // over the still windows; the hold is capped by MoveGuardMax.
                    if (getUpMoveGuardOn)
                    {
                        bool deckMoving = TiltPlatform.AnyMoving();
                        if (deckMoving && getUpMoveGuardHeld < getUpMoveGuardMax)
                        {
                            getUpMoveGuardHeld = Mathf.Min(getUpMoveGuardMax, getUpMoveGuardHeld + dt);
                            if (!getUpMoveGuardInHold)
                            {
                                getUpMoveGuardInHold = true;
                                getUpMoveGuardHolds++;
                            }
                        }
                        else if (!deckMoving)
                        {
                            getUpMoveGuardInHold = false;
                        }
                        riseClock = Mathf.Max(0f, riseClock - getUpMoveGuardHeld);
                        riseShare = Mathf.Clamp01(riseClock / (GetUpRiseSeconds * getUpPatience));
                    }
                    // ─── ROUND 42: THE TRUNK GOVERNOR ───
                    // The r39 over-pitch fired into a trunk with no counter-authority
                    // (the four chats' consensus increment). Mode bit 1 (the pitch
                    // gate): hold the extension clock while the torso is pitched
                    // beyond GovDeg - either side of vertical. Mode bit 2 (the
                    // counter-lean): bias the trunk target AWAY from the deviation
                    // (torsoTargetDeg has minus = forward, the same axis as tilt),
                    // proportionally to the degrees past the band.
                    if (getUpTrunkGovMode == 1f || getUpTrunkGovMode == 3f)
                    {
                        float govDev = Mathf.Abs(tilt);
                        if (govDev > getUpTrunkGovDeg && getUpTrunkGovHeld < getUpTrunkGovMax)
                        {
                            getUpTrunkGovHeld = Mathf.Min(getUpTrunkGovMax, getUpTrunkGovHeld + dt);
                            if (!getUpTrunkGovInHold)
                            {
                                getUpTrunkGovInHold = true;
                                getUpTrunkGovHolds++;
                            }
                        }
                        else if (govDev <= getUpTrunkGovDeg)
                        {
                            getUpTrunkGovInHold = false;
                        }
                        riseClock = Mathf.Max(0f, riseClock - getUpTrunkGovHeld);
                        riseShare = Mathf.Clamp01(riseClock / (GetUpRiseSeconds * getUpPatience));
                    }
                    if (getUpTrunkGovMode == 2f || getUpTrunkGovMode == 3f)
                    {
                        float govDev = Mathf.Abs(tilt);
                        float govExcess = govDev - getUpTrunkGovDeg;
                        if (govExcess > 0f)
                        {
                            trunkGovBiasNow = -Mathf.Sign(tilt) * getUpTrunkGovK * govExcess;
                            // ROUND 42b: the cap. The r42 wave-1 doses ran uncapped
                            // and slammed the trunk target by +-70..140 deg during
                            // tumbles (|tilt| can pass 90); the cap bounds the
                            // counter-lean to a sane task. 0 = uncapped (the r42
                            // bytes).
                            if (getUpTrunkGovCap > 0f)
                                trunkGovBiasNow = Mathf.Clamp(trunkGovBiasNow,
                                    -getUpTrunkGovCap, getUpTrunkGovCap);
                            float govMag = Mathf.Abs(trunkGovBiasNow);
                            if (govMag > getUpTrunkGovBias) getUpTrunkGovBias = govMag;
                        }
                    }
                    if (getUpStandKneeClimb > 0f && getUpRiseStartPelvisY < -999f)
                        getUpRiseStartPelvisY = pelvisY;   // ROUND 35: the climb datum
                    if (seatPhase >= 0)
                    {
                        // ROUND 45: the seat route's drives (the machine above
                        // owns the phase; see DriveGetUpLegsSeat).
                        DriveGetUpLegsSeat();
                    }
                    else if (getUpSitPullOn && getUpSitActive)
                    {
                        // ROUND 44: the sit heel-pull route - the pull phase folds
                        // the knees under the body, then the extension blends from
                        // the pulled pose (see the drive pair below). The key rides
                        // raw stageAge so either phase is visible per frame (the
                        // trace column: 0..1 pull, 1..2 extension).
                        float pullSecP = Mathf.Max(0.05f, getUpSitPullSec);
                        if (stageAge < pullSecP)
                        {
                            float pullP = Mathf.Clamp01(stageAge / pullSecP);
                            getUpSitPullKey = pullP;
                            if (!getUpSitPullLogged)
                            {
                                getUpSitPullLogged = true;
                                Debug.Log("GETUP-SITPULL enter t=" + N(now, 3)
                                    + " knee=" + N(getUpKneeL + getUpSitPullKnee, 1)
                                    + " hip=" + N(getUpHipL - getUpSitPullHip, 1)
                                    + " ankle=" + N(getUpAnkleL - getUpSitPullAnkle, 1)
                                    + " sec=" + N(pullSecP, 2));
                            }
                            DriveGetUpLegsSitPull(pullP);
                        }
                        else
                        {
                            float pullExt = Mathf.Clamp01((stageAge - pullSecP)
                                / Mathf.Max(0.1f, GetUpRiseSeconds * getUpPatience - pullSecP));
                            // ROUND 47: the arc's own key (rate-gated clock).
                            if (getUpSitArcSec > 0.001f) pullExt = SitArcKey();
                            getUpSitPullKey = 1f + pullExt;
                            DriveGetUpLegsStandPulled(pullExt);
                        }
                    }
                    else if (riseShare < getUpStandHold)
                        DriveGetUpLegs(getUpStandFrom > 0 ? 0f : 1f);
                    else
                        DriveGetUpLegsStand(Mathf.InverseLerp(getUpStandHold, 1f, riseShare), pelvisY);
                }
                else
                {
                    DriveGetUpLegs(Mathf.Clamp01(1f - stageAge / (GetUpRiseSeconds * getUpPatience)));
                }
                // ROUND 43b: the sit attempt's trunk SOURCE. The historic Rise
                // blends the trunk from the TUCK deg (-40, forward); a sit-fired
                // attempt starts with the torso already upright, and the -40 pull
                // yanked it forward at the fire - the window broke at -21 deg in
                // ~0.7 s and the man dove face-first (s10, fires t=79.46/85.11).
                // A sit attempt blends from ITS OWN latched tilt instead.
                float trunkFromDeg = getUpSitActive ? getUpTrunkStartDeg : GetUpTrunkTuckDeg;
                float trunkShare = riseShare;
                // ROUND 48: the tie's ARMING. 0 = live from the attempt's first
                // frame (the r47 route); 1 = armed only while the extension is
                // live (the pull completed - the press and the pull keep the
                // historic asks, so the palms may load again); 2 = the tie off.
                // Flag absent: the r47 condition, byte for byte.
                bool arcTieLive = getUpSitActive && getUpSitArcSec > 0.001f
                    && (getUpSitArcTieExt < 0.5f
                        || (getUpSitArcTieExt < 1.5f && getUpSitPullKey >= 0.999f));
                if (arcTieLive)
                {
                    // ROUND 47: the torso tied to HEIGHT - the base allowance
                    // blends out as the pelvis climbs toward stand height; the
                    // base lean is clamped to +-ArcTorso.
                    getUpSitArcHShare = Mathf.Clamp01((pelvisY - getUpSitArcBasePelvY)
                        / Mathf.Max(0.05f, standH - getUpSitArcBasePelvY));
                    trunkFromDeg = Mathf.Clamp(getUpTrunkStartDeg,
                        -getUpSitArcTorso, getUpSitArcTorso);
                    // CYCLE 106: THE SIT-UP ROCK. The height tie alone deadlocks
                    // on the flat squat take: the pelvis never climbs, so the
                    // trunk never lifts off its latched lean and the CoM never
                    // comes over the parked feet. The rock clock is the torso's
                    // OWN datum - it runs from the fire - and the tie takes the
                    // larger share: the torso curls up first (the owner: "bends
                    // his knees and tries to sit up"), then the extension works.
                    // Rock 0 = the r47 tie byte for byte.
                    if (getUpSitRockSec > 0.001f)
                    {
                        if (getUpSitRockClock < 30f) getUpSitRockClock += dt;
                        trunkShare = Mathf.Max(getUpSitArcHShare,
                            Mathf.Clamp01(getUpSitRockClock / getUpSitRockSec));
                    }
                    else
                    {
                        trunkShare = getUpSitArcHShare;
                    }
                }
                else if (getUpSitArcTieExt > 0f)
                {
                    getUpSitArcHShare = 0f;   // the tie is not armed (trace)
                }
                float trunkToDeg = GetUpTrunkRiseDeg;
                // ROUND 52: the shift's lean dose - an optional trunk lean
                // over the weight-transfer stage (lean 0 = historic).
                if (Mathf.Abs(getUpSitShiftLean) > 0.001f && getUpSitShiftOn
                    && getUpSitActive && seatPhase == 1 && stepDone && !sitShiftDone)
                {
                    trunkFromDeg = getUpTrunkStartDeg;
                    trunkToDeg = getUpTrunkStartDeg + getUpSitShiftLean * sitShiftFrac;
                    trunkShare = 1f;
                }
                // CYCLE 108: THE SEATED HEEL-DRAW. The draw over a seated torso
                // (its start pitch <= DrawSeat) throws the body backward as it
                // runs - the heel-draw's reaction runs through a trunk that
                // pushes nothing back (measured, c107_ctl_du08 attempt 0: tilt
                // 10 -> 52 within 0.55 s of the pull's start, the feet dragged
                // back with it, the attempt dead on its back). Armed: a FORWARD
                // counter-lean on the trunk, scaled by the pull's own progress
                // - the heels come under a body that stays up. The gate reads
                // the drag's START pitch (latched at the pull's entry), so the
                // sprawl attempts keep every byte. 0 = off, byte for byte.
                if (getUpSitDrawLean > 0.001f && getUpSitActive && seatPhase == 1
                    && getUpSitDragTilt <= getUpSitDrawSeat)
                {
                    float drawP = Mathf.Clamp01(seatPullT / Mathf.Max(0.05f, getUpSitPullSec));
                    trunkGovBiasNow -= getUpSitDrawLean * drawP;
                    if (!getUpSitDrawLogged)
                    {
                        getUpSitDrawLogged = true;
                        Debug.Log("GETUP-SITDRAW hold t=" + N(now, 3)
                            + " H=" + N(getUpSitDrawLean, 1)
                            + " seat=" + N(getUpSitDragTilt, 1)
                            + " thr=" + N(getUpSitDrawSeat, 1));
                    }
                }
                DriveGetUpTrunk(trunkFromDeg, trunkToDeg, trunkShare, trunkGovBiasNow);
                DriveHipsGetUp(pelvisTilt, pelvisAngVel);
                if (standing)
                {
                    getUpStandNamed = true;
                    getUpDone = true;
                    if (balance != null)
                    {
                        balance.getUpSuccess = true;
                        balance.getUpSeconds = getUpFallTime >= 0f ? now - getUpFallTime : -1f;
                    }
                    FinishAttempt(true, now);
                    WriteGetUpTrace(tilt, pelvisY, pelvisH, standH, comX, comY, standComY,
                        leftFootX, rightFootX, leftFootY, rightFootY, leftDown, rightDown,
                        leftUnder, rightUnder, tiltBar, comBar, feetBar, stageAge);
                    return;
                }
                // ROUND 43c: a sit-fired attempt's bound defers by its own ext
                // seconds (the extension keeps working over the held window);
                // every other route reads the historic bound exactly.
                float riseBound = GetUpRiseSeconds * getUpPatience
                    + (getUpSitStandOn && getUpSitActive ? getUpSitStandExt : 0f)
                    + (seatPhase >= 0 ? Mathf.Min(seatFrozenT, getUpSitSeatFrozenMax) : 0f)
                    + (getUpSitDrawPlant > 0.001f
                        ? Mathf.Min(seatDrawPlantT, getUpSitDrawPlantMax
                            * (getUpSitDrawPlantRe > 0.001f ? getUpSitDrawPlantEps : 1f))
                        : 0f)
                    + (getUpSitStepOn ? Mathf.Min(stepT, getUpSitStepMax) : 0f)
                    + (getUpSitShiftOn ? Mathf.Min(sitShiftT, getUpSitShiftMax) : 0f)
                    + (getUpSitPlantOn ? Mathf.Min(sitPlantT, getUpSitPlantMax) : 0f)
                    + (getUpSitExtArrestOn ? Mathf.Min(seatExtArrestT, getUpSitExtArrestMax) : 0f);
                if (riseClock >= riseBound)
                {
                    FinishAttempt(false, now);
                    WriteGetUpTrace(tilt, pelvisY, pelvisH, standH, comX, comY, standComY,
                        leftFootX, rightFootX, leftFootY, rightFootY, leftDown, rightDown,
                        leftUnder, rightUnder, tiltBar, comBar, feetBar, stageAge);
                    return;
                }
                break;
        }

        // ─── the ladder, named where the recorder already reads it ───
        if (balance != null)
        {
            balance.locomotionState = BalanceController.LocomotionState.GettingUp;
            balance.currentState = BalanceController.BalanceState.Recovery;
            // The recorder's own get-up ladder: 1 lying, 2 sitting, 3 knees
            // tucked, 4 standing - the stage in force, not a claim about success.
            balance.getUpPose = (int)getUpStage;
            if (balance.getUpPoseMax < balance.getUpPose)
                balance.getUpPoseMax = balance.getUpPose;
            balance.getUpGateHeld = getUpStandGateHeld;    // ROUND 30: the contact gate (log mirror)
            balance.getUpGateHolds = getUpStandGateHolds;
            balance.getUpBaseWait = getUpBaseWait;         // ROUND 31: the base gate (log mirror)
            balance.getUpBaseAborts = getUpBaseAborts;
            balance.getUpBaseSettle = getUpBaseSettleT;    // ROUND 32: the settle gate (log mirror)
            balance.getUpBaseSettleBlocks = getUpBaseSettleBlocks;
            balance.getUpBaseLoad = getUpBaseLoadT;        // ROUND 35: the load gate (log mirror)
            balance.getUpBaseLoadBlocks = getUpBaseLoadBlocks;
            balance.getUpArbSuppL = getUpArbSuppLTot;      // ROUND 37: the hip arbitration (log mirror)
            balance.getUpArbSuppR = getUpArbSuppRTot;
            balance.getUpMoveGuardHeld = getUpMoveGuardHeld;   // ROUND 42 (log mirror)
            balance.getUpMoveGuardHolds = getUpMoveGuardHolds;
            balance.getUpTrunkGovHeld = getUpTrunkGovHeld;
            balance.getUpTrunkGovHolds = getUpTrunkGovHolds;
            balance.getUpTrunkGovBias = getUpTrunkGovBias;
            balance.getUpSitExt = getUpSitStandExt;        // ROUND 43 (log mirror)
            balance.getUpSitFires = getUpSitStandFires;
            balance.getUpSitSeatFrozen = Mathf.Max(balance.getUpSitSeatFrozen, seatFrozenT);
            balance.getUpSitSeatPasses = Mathf.Max(balance.getUpSitSeatPasses, seatGatePasses);
        }
        getUpDriveOn = true;
        HipDbgFrame("post");  // ROUND 36 (probe): the ladder's exit state

        WriteGetUpTrace(tilt, pelvisY, pelvisH, standH, comX, comY, standComY,
            leftFootX, rightFootX, leftFootY, rightFootY, leftDown, rightDown,
            leftUnder, rightUnder, tiltBar, comBar, feetBar, stageAge);
        PushTraceFrame(now, tilt);
    }

    // One writer per leg joint, the pose drive's own (DriveOneLeg). `draw` is the
    // tuck's depth: 0 = the pose the body landed in, 1 = the full knees-under
    // tuck. The angles are DELTAS from the settle pose (the crawl's own rule,
    // cycle 28), so the same shape holds on every build.
    private void DriveGetUpLegs(float draw)
    {
        float d = Mathf.Clamp(draw, 0f, 1f);
        DriveOneLeg(true, getUpHipL - GetUpHipDrawDeg * d,
            getUpKneeL + GetUpKneeDrawDeg * d, true, getUpAnkleL + GetUpAnkleDrawDeg * d);
        DriveOneLeg(false, getUpHipR - GetUpHipDrawDeg * d,
            getUpKneeR + GetUpKneeDrawDeg * d, true, getUpAnkleR + GetUpAnkleDrawDeg * d);
    }

    // ROUND 35: the sit-back variant of the Tuck wait - the same fold, B deg of hip
    // flexion deeper (and half that at the knee), so the hips travel back over the
    // planted soles while the load gate waits. B = 0 returns DriveGetUpLegs exactly.
    private void DriveGetUpLegsSquat(float draw)
    {
        float d = Mathf.Clamp(draw, 0f, 1f);
        float b = getUpBaseSquatDeg;
        DriveOneLeg(true, getUpHipL - GetUpHipDrawDeg * d - b,
            getUpKneeL + GetUpKneeDrawDeg * d + 0.5f * b, true, getUpAnkleL + GetUpAnkleDrawDeg * d);
        DriveOneLeg(false, getUpHipR - GetUpHipDrawDeg * d - b,
            getUpKneeR + GetUpKneeDrawDeg * d + 0.5f * b, true, getUpAnkleR + GetUpAnkleDrawDeg * d);
    }

    // ROUND 35: the human's weight, N - the same sum the balance side's RigWeightN
    // runs (every body of the rig, x gravity), cached once. The load gate's floor.
    private float PoseRigWeightN()
    {
        if (rigWeightCacheN > 0.01f) return rigWeightCacheN;
        Rigidbody2D[] bodies = GetComponentsInChildren<Rigidbody2D>();
        float mass = 0f;
        for (int i = 0; i < bodies.Length; i++)
            if (bodies[i] != null) mass += bodies[i].mass;
        rigWeightCacheN = mass * Physics2D.gravity.magnitude;
        return rigWeightCacheN;
    }

    // ROUND 29: the stand convergence - the pose the rise holds (the fold at
    // draw 1, or v2's latched pose at draw 0) blended to the stand-pose knobs
    // (both sides, the joint conventions: hip minus = flexion, knee plus =
    // flexion, ankle plus = toe down).
    private void DriveGetUpLegsStand(float k, float pelvisYNow)
    {
        float kk = Mathf.Clamp01(k);
        // ROUND 35: the fold release may lag the hip's own blend. THE DELAY form
        // (-poseGetUpStandHipFirst D): the knee + ankle hold the fold until the hip
        // progress passes D, then release over the rest. THE CLIMB form
        // (-poseGetUpStandKneeClimb C): the release is gated by the pelvis's own
        // rise since the Rise began - no climb, no release (the closed chain).
        // Both 0: kKnee == kk and every lerp is the r29 one, bit for bit.
        float kKnee = kk;
        if (getUpStandHipFirst > 0f)
        {
            kKnee = Mathf.Clamp01((kk - getUpStandHipFirst) / (1f - getUpStandHipFirst));
        }
        else if (getUpStandKneeClimb > 0f)
        {
            float climbed = pelvisYNow - getUpRiseStartPelvisY;
            kKnee = Mathf.Clamp01(climbed / getUpStandKneeClimb);
        }
        float d0 = getUpStandFrom > 0 ? 0f : 1f;
        float hL = getUpHipL - GetUpHipDrawDeg * d0;
        float kL = getUpKneeL + GetUpKneeDrawDeg * d0;
        float aL = getUpAnkleL + GetUpAnkleDrawDeg * d0;
        float hR = getUpHipR - GetUpHipDrawDeg * d0;
        float kR = getUpKneeR + GetUpKneeDrawDeg * d0;
        float aR = getUpAnkleR + GetUpAnkleDrawDeg * d0;
        // ROUND 40: the ankle's own blend key (the heel release). 0 = kKnee.
        float kAnkle = kKnee;
        if (getUpStandAnkleFast == 1) kAnkle = kk;
        else if (getUpStandAnkleFast >= 2) kAnkle = Mathf.Clamp01(2f * kk);
        float hLT = Mathf.Lerp(hL, getUpStandHipDeg, kk);
        float kLT = Mathf.Lerp(kL, getUpStandKneeDeg, kKnee);
        float aLT = Mathf.Lerp(aL, getUpStandAnkleDeg, kAnkle);
        float hRT = Mathf.Lerp(hR, getUpStandHipDeg, kk);
        float kRT = Mathf.Lerp(kR, getUpStandKneeDeg, kKnee);
        float aRT = Mathf.Lerp(aR, getUpStandAnkleDeg, kAnkle);
        gusKeyk = kk; gusKeyKnee = kKnee; gusKeyAnkle = kAnkle;
        gusTgtHipL = hLT; gusTgtKneeL = kLT; gusTgtAnkleL = aLT;
        DriveOneLeg(true, hLT, kLT, true, aLT);
        DriveOneLeg(false, hRT, kRT, true, aRT);
    }

    // ROUND 44: THE SIT HEEL-PULL - the knees fold first so the heels travel
    // under the body (the r39 pre-push state: the hip near -90, the shins
    // folded). The targets are DELTAS from the latched sit pose (the house
    // rule), ramped by the pull key p: p = 0 is the latch, p = 1 the fold.
    private void DriveGetUpLegsSitPull(float p)
    {
        float pp = Mathf.Clamp01(p);
        DriveOneLeg(true, getUpHipL - getUpSitPullHip * pp,
            getUpKneeL + getUpSitPullKnee * pp, true, getUpAnkleL - getUpSitPullAnkle * pp);
        DriveOneLeg(false, getUpHipR - getUpSitPullHip * pp,
            getUpKneeR + getUpSitPullKnee * pp, true, getUpAnkleR - getUpSitPullAnkle * pp);
    }

    // CYCLE 109: the wait's plant drive - the frozen pull pose plus the plant
    // deltas at the key (down-and-close), per leg; the pull's own shape.
    private void DriveGetUpLegsSitPullPlant(float p)
    {
        float pp = Mathf.Clamp01(p);
        float kk = seatDrawPlantK * getUpSitDrawPlant;
        DriveOneLeg(true, getUpHipL - getUpSitPullHip * pp + getUpSitDrawPlantHip * kk,
            getUpKneeL + getUpSitPullKnee * pp + getUpSitDrawPlantKnee * kk, true,
            getUpAnkleL - getUpSitPullAnkle * pp + getUpSitDrawPlantAnkle * kk);
        DriveOneLeg(false, getUpHipR - getUpSitPullHip * pp + getUpSitDrawPlantHip * kk,
            getUpKneeR + getUpSitPullKnee * pp + getUpSitDrawPlantKnee * kk, true,
            getUpAnkleR - getUpSitPullAnkle * pp + getUpSitDrawPlantAnkle * kk);
    }

    // ROUND 50: a joint's unfold key. start = 0 returns kk (the historic
    // full-clock release); start > 0 delays the joint's release to the tail.
    private float UnfoldKey(float kk, float start)
    {
        if (start <= 0f) return kk;
        return Mathf.Clamp01((kk - start) / (1f - start));
    }

    // ROUND 44: the extension after the pull - blend from the FULLY-PULLED pose
    // to the stand targets (the r29 keys, the hip-first rule kept; k = 1 lands
    // where DriveGetUpLegsStand lands). ROUND 46: pullKey scales how far the
    // drag actually got (the gap-out / guarded gate-dead exit pose); the
    // default 1f keeps every unarmed route byte for byte. ROUND 50: the
    // per-joint unfold order (see the fields' block).
    private void DriveGetUpLegsStandPulled(float k, float pullKey = 1f)
    {
        float kk = Mathf.Clamp01(k);
        float pk = Mathf.Clamp01(pullKey);
        float kHip = UnfoldKey(kk, getUpStandHipStart);
        float kKnee = kk;
        if (getUpStandKneeStart >= 0f)
            kKnee = UnfoldKey(kk, getUpStandKneeStart);
        else if (getUpStandHipFirst > 0f)
            kKnee = Mathf.Clamp01((kk - getUpStandHipFirst) / (1f - getUpStandHipFirst));
        float kAnkle = getUpStandAnkleStart >= 0f ? UnfoldKey(kk, getUpStandAnkleStart) : kKnee;
        float hLT = Mathf.Lerp(getUpHipL - getUpSitPullHip * pk, getUpStandHipDeg, kHip);
        float kLT = Mathf.Lerp(getUpKneeL + getUpSitPullKnee * pk, getUpStandKneeDeg, kKnee);
        float aLT = Mathf.Lerp(getUpAnkleL - getUpSitPullAnkle * pk, getUpStandAnkleDeg, kAnkle);
        float hRT = Mathf.Lerp(getUpHipR - getUpSitPullHip * pk, getUpStandHipDeg, kHip);
        float kRT = Mathf.Lerp(getUpKneeR + getUpSitPullKnee * pk, getUpStandKneeDeg, kKnee);
        float aRT = Mathf.Lerp(getUpAnkleR - getUpSitPullAnkle * pk, getUpStandAnkleDeg, kAnkle);
        gusKeyk = kk; gusKeyKnee = kKnee; gusKeyAnkle = kAnkle;
        gusTgtHipL = hLT; gusTgtKneeL = kLT; gusTgtAnkleL = aLT;
        DriveOneLeg(true, hLT, kLT, true, aLT);
        DriveOneLeg(false, hRT, kRT, true, aRT);
    }

    // ROUND 49: THE CORRIDOR RE-SEAT. The targets = the drag's exit pose per
    // leg plus the leg's own share of the re-seat budget (the house deltas).
    private void ReseatTargets(float sL, float sR,
        out float hipL, out float kneeL, out float ankleL,
        out float hipR, out float kneeR, out float ankleR)
    {
        float pk = Mathf.Clamp01(seatPullExitKey);
        hipL = getUpHipL - getUpSitPullHip * pk + getUpSitReseatHip * sL;
        kneeL = getUpKneeL + getUpSitPullKnee * pk + getUpSitReseatKnee * sL;
        ankleL = getUpAnkleL - getUpSitPullAnkle * pk + getUpSitReseatAnkle * sL;
        hipR = getUpHipR - getUpSitPullHip * pk + getUpSitReseatHip * sR;
        kneeR = getUpKneeR + getUpSitPullKnee * pk + getUpSitReseatKnee * sR;
        ankleR = getUpAnkleR - getUpSitPullAnkle * pk + getUpSitReseatAnkle * sR;
    }

    // ROUND 49: the re-seat's drive (the shares as they stand this frame).
    private void DriveGetUpLegsReseat()
    {
        float hL, kL, aL, hR, kR, aR;
        ReseatTargets(seatResL, seatResR, out hL, out kL, out aL, out hR, out kR, out aR);
        DriveOneLeg(true, hL, kL, true, aL);
        DriveOneLeg(false, hR, kR, true, aR);
    }

    // ROUND 49: the extension after a re-seat - blends from the captured
    // hand-off pose (per leg) to the stand targets (the StandPulled shape).
    // ROUND 50: the per-joint unfold order (see the fields' block).
    private void DriveGetUpLegsStandReseat(float k)
    {
        float kk = Mathf.Clamp01(k);
        float kHip = UnfoldKey(kk, getUpStandHipStart);
        float kKnee = kk;
        if (getUpStandKneeStart >= 0f)
            kKnee = UnfoldKey(kk, getUpStandKneeStart);
        else if (getUpStandHipFirst > 0f)
            kKnee = Mathf.Clamp01((kk - getUpStandHipFirst) / (1f - getUpStandHipFirst));
        float kAnkle = getUpStandAnkleStart >= 0f ? UnfoldKey(kk, getUpStandAnkleStart) : kKnee;
        float hLT = Mathf.Lerp(seatResHipL, getUpStandHipDeg, kHip);
        float kLT = Mathf.Lerp(seatResKneeL, getUpStandKneeDeg, kKnee);
        float aLT = Mathf.Lerp(seatResAnkleL, getUpStandAnkleDeg, kAnkle);
        float hRT = Mathf.Lerp(seatResHipR, getUpStandHipDeg, kHip);
        float kRT = Mathf.Lerp(seatResKneeR, getUpStandKneeDeg, kKnee);
        float aRT = Mathf.Lerp(seatResAnkleR, getUpStandAnkleDeg, kAnkle);
        gusKeyk = kk; gusKeyKnee = kKnee; gusKeyAnkle = kAnkle;
        gusTgtHipL = hLT; gusTgtKneeL = kLT; gusTgtAnkleL = aLT;
        DriveOneLeg(true, hLT, kLT, true, aLT);
        DriveOneLeg(false, hRT, kRT, true, aRT);
    }

    // ROUND 49: the corridor re-seat runner (see the fields' block). The legs
    // take turns; the active leg's share ramps over ReseatSec s slices (a
    // discrete per-leg budget: a knee fold slides one heel in at a time).
    // Exits: the corridor (the gaps AND the sustained sole load), the shares
    // spent, or the stage's own budget - always bounded.
    private void RunSitReseat(float now, float dt)
    {
        if (!seatResLogged)
        {
            seatResLogged = true;
            Debug.Log("GETUP-RESEAT enter t=" + N(now, 3) + " gapL=" + N(seatGapL, 3)
                + " gapR=" + N(seatGapR, 3) + " pk=" + N(seatPullExitKey, 3)
                + " knee=" + N(getUpSitReseatKnee, 0) + " hip=" + N(getUpSitReseatHip, 0)
                + " gapTgt=" + N(getUpSitReseatGap, 3) + " fz=" + N(poseGroundFzLeftN + poseGroundFzRightN, 0));
        }
        seatReseatT += dt;
        float w = PoseRigWeightN();
        float fzSum = poseGroundFzLeftN + poseGroundFzRightN;
        if (w > 1f && fzSum >= getUpSitReseatLoad * w) seatResLoadT += dt;
        else seatResLoadT = 0f;
        bool roomL = seatResL < 0.999f && seatGapL > getUpSitReseatGap;
        bool roomR = seatResR < 0.999f && seatGapR > getUpSitReseatGap;
        bool activeRoom = seatResTurn == 0 ? roomL : roomR;
        bool otherRoom = seatResTurn == 0 ? roomR : roomL;
        if (!activeRoom && otherRoom)
        {
            seatResTurn = 1 - seatResTurn;
            seatResTurnT = 0f;
        }
        else if (activeRoom)
        {
            seatResTurnT += dt;
            float rate = dt / Mathf.Max(0.05f, getUpSitReseatSec);
            if (seatResTurn == 0) seatResL = Mathf.Clamp01(seatResL + rate);
            else seatResR = Mathf.Clamp01(seatResR + rate);
            if (seatResTurnT >= Mathf.Max(0.05f, getUpSitReseatSec) && otherRoom)
            {
                seatResTurn = 1 - seatResTurn;
                seatResTurnT = 0f;
            }
        }
        bool gapsOk = seatGapL <= getUpSitReseatGap && seatGapR <= getUpSitReseatGap;
        bool corridor = gapsOk && seatResLoadT >= getUpSitReseatLoadSec;
        bool spent = seatResL >= 0.999f && seatResR >= 0.999f;
        if (corridor || spent || seatReseatT >= getUpSitReseatMax)
        {
            seatReseatDone = true;
            ReseatTargets(seatResL, seatResR,
                out seatResHipL, out seatResKneeL, out seatResAnkleL,
                out seatResHipR, out seatResKneeR, out seatResAnkleR);
            Debug.Log("GETUP-RESEAT done t=" + N(now, 3) + " why="
                + (corridor ? "corridor" : (spent ? "spent" : "max"))
                + " gapL=" + N(seatGapL, 3) + " gapR=" + N(seatGapR, 3)
                + " loadT=" + N(seatResLoadT, 2) + " sL=" + N(seatResL, 3)
                + " sR=" + N(seatResR, 3) + " fz=" + N(fzSum, 0));
        }
    }

    // ROUND 49: the re-seat state, fresh per attempt (the r45 re-enter rule).
    private void ResetSitReseat()
    {
        seatResL = 0f; seatResR = 0f; seatReseatT = 0f; seatResTurnT = 0f;
        seatResLoadT = 0f; seatResTurn = 0; seatReseatDone = false; seatResLogged = false;
    }

    // ROUND 51: the per-leg step targets - the latch plus the leg's own share.
    private void StepTargets(out float hipL, out float kneeL, out float ankleL,
        out float hipR, out float kneeR, out float ankleR)
    {
        hipL = getUpHipL - getUpSitStepHip * stepFracL;
        kneeL = getUpKneeL + getUpSitStepKnee * stepFracL;
        ankleL = getUpAnkleL - getUpSitStepAnkle * stepFracL;
        hipR = getUpHipR - getUpSitStepHip * stepFracR;
        kneeR = getUpKneeR + getUpSitStepKnee * stepFracR;
        ankleR = getUpAnkleR - getUpSitStepAnkle * stepFracR;
    }

    // ROUND 51: the step machine's drive (the shares as they stand this frame).
    private void DriveGetUpLegsStep()
    {
        float hL, kL, aL, hR, kR, aR;
        StepTargets(out hL, out kL, out aL, out hR, out kR, out aR);
        DriveOneLeg(true, hL, kL, true, aL);
        DriveOneLeg(false, hR, kR, true, aR);
    }

    // ROUND 51: the step state, fresh per attempt.
    private void ResetSitStep()
    {
        stepT = 0f; stepFracL = 0f; stepFracR = 0f; stepFromFrac = 0f; stepLeg = 0;
        stepPhase = 0; stepPhaseT = 0f; stepRound = 0; stepDone = false;
        stepLogged = false; stepLoadT = 0f;
    }

    // ROUND 52: THE POST-STEP WEIGHT TRANSFER. The step lands the feet near
    // the CoM but every step exit is "spent" - the folds unloaded the soles
    // (the r51 wall). This stage drives the legs partway BACK out of the
    // fold (hip extension, knee release, a plantar press) seeking a real
    // sole load before the extension; the exits are the load (sum Fz over
    // Load*W sustained LoadSec) or the stage's budget. The extension then
    // starts from THIS pose (the same seatRes* hand-off the step uses).
    private void RunSitShift(float now, float dt)
    {
        if (!sitShiftEntered)
        {
            sitShiftEntered = true;
            Debug.Log("GETUP-SITSHIFT enter t=" + N(now, 3)
                + " gapL=" + N(seatGapL, 3) + " gapR=" + N(seatGapR, 3)
                + " hip=" + N(getUpSitShiftHip, 0) + " knee=" + N(getUpSitShiftKnee, 0)
                + " ankle=" + N(getUpSitShiftAnkle, 0) + " lean=" + N(getUpSitShiftLean, 0)
                + " load=" + N(getUpSitShiftLoad, 3) + " sec=" + N(getUpSitShiftSec, 2)
                + " fz=" + N(poseGroundFzLeftN + poseGroundFzRightN, 0));
        }
        sitShiftT += dt;
        float w = PoseRigWeightN();
        float fzSum = poseGroundFzLeftN + poseGroundFzRightN;
        if (fzSum > sitShiftFzMax) sitShiftFzMax = fzSum;
        float gMin = Mathf.Min(seatGapL, seatGapR);
        if (gMin < sitShiftGapMin) sitShiftGapMin = gMin;
        if (w > 1f && fzSum >= getUpSitShiftLoad * w) sitShiftLoadT += dt;
        else sitShiftLoadT = 0f;
        bool loaded = sitShiftLoadT >= getUpSitShiftLoadSec;
        bool spent = sitShiftT >= getUpSitShiftSec;
        if (loaded || spent)
        {
            float f = sitShiftFrac;
            seatResHipL = getUpHipL - getUpSitStepHip * seatResL + getUpSitShiftHip * f;
            seatResKneeL = getUpKneeL + getUpSitStepKnee * seatResL - getUpSitShiftKnee * f;
            seatResAnkleL = getUpAnkleL - getUpSitStepAnkle * seatResL + getUpSitShiftAnkle * f;
            seatResHipR = getUpHipR - getUpSitStepHip * seatResR + getUpSitShiftHip * f;
            seatResKneeR = getUpKneeR + getUpSitStepKnee * seatResR - getUpSitShiftKnee * f;
            seatResAnkleR = getUpAnkleR - getUpSitStepAnkle * seatResR + getUpSitShiftAnkle * f;
            sitShiftDone = true;
            Debug.Log("GETUP-SITSHIFT done t=" + N(now, 3)
                + " why=" + (loaded ? "load" : "spent")
                + " fz=" + N(fzSum, 0) + " fzMax=" + N(sitShiftFzMax, 0)
                + " gapL=" + N(seatGapL, 3) + " gapR=" + N(seatGapR, 3)
                + " frac=" + N(f, 3) + " loadT=" + N(sitShiftLoadT, 2));
            return;
        }
        sitShiftFrac = Mathf.Clamp01(sitShiftT / Mathf.Max(0.05f, getUpSitShiftRamp));
    }

    // ROUND 52: the shift's drive (the step capture plus the shift's own
    // deltas at the stage's fraction; per leg, like StepTargets).
    private void DriveGetUpLegsShift()
    {
        float f = sitShiftFrac;
        float hL = getUpHipL - getUpSitStepHip * seatResL + getUpSitShiftHip * f;
        float kL = getUpKneeL + getUpSitStepKnee * seatResL - getUpSitShiftKnee * f;
        float aL = getUpAnkleL - getUpSitStepAnkle * seatResL + getUpSitShiftAnkle * f;
        float hR = getUpHipR - getUpSitStepHip * seatResR + getUpSitShiftHip * f;
        float kR = getUpKneeR + getUpSitStepKnee * seatResR - getUpSitShiftKnee * f;
        float aR = getUpAnkleR - getUpSitStepAnkle * seatResR + getUpSitShiftAnkle * f;
        DriveOneLeg(true, hL, kL, true, aL);
        DriveOneLeg(false, hR, kR, true, aR);
    }

    // ROUND 52: the shift state, fresh per attempt.
    private void ResetSitShift()
    {
        sitShiftT = 0f; sitShiftDone = false; sitShiftEntered = false;
        sitShiftLoadT = 0f; sitShiftFzMax = 0f; sitShiftGapMin = 999f;
        sitShiftFrac = 0f;
    }

    // ROUND 54: THE PLANT STAGE (see the fields' block). The reach ramps the
    // step's fold BACK OUT - knee unfold (Knee), hip extension (Hip), plantar
    // press (Ankle) - while the soles are watched; the hand-off fires on the
    // CONTACT gate (sum fz >= Fz*W held FzSec) or the budget (Max). The ext
    // then starts from the reached pose (the usual seatRes* capture).
    private void RunSitPlant(float now, float dt)
    {
        if (!sitPlantEntered)
        {
            sitPlantEntered = true;
            Debug.Log("GETUP-SITPLANT enter t=" + N(now, 3)
                + " knee=" + N(getUpSitPlantKnee, 0) + " hip=" + N(getUpSitPlantHip, 0)
                + " ankle=" + N(getUpSitPlantAnkle, 0) + " sec=" + N(getUpSitPlantSec, 2)
                + " fz=" + N(poseGroundFzLeftN + poseGroundFzRightN, 0));
        }
        sitPlantT += dt;
        float w = PoseRigWeightN();
        float fzSum = poseGroundFzLeftN + poseGroundFzRightN;
        if (fzSum > sitPlantFzMax) sitPlantFzMax = fzSum;
        if (w > 1f && fzSum >= getUpSitPlantFz * w) sitPlantContactT += dt;
        else sitPlantContactT = 0f;
        bool contact = sitPlantContactT >= getUpSitPlantFzSec;
        bool spent = sitPlantT >= getUpSitPlantMax;
        if (contact || spent)
        {
            float f = sitPlantFrac;
            seatResHipL = getUpHipL - getUpSitStepHip * seatResL + getUpSitPlantHip * f;
            seatResKneeL = getUpKneeL + getUpSitStepKnee * seatResL - getUpSitPlantKnee * f;
            seatResAnkleL = getUpAnkleL - getUpSitStepAnkle * seatResL + getUpSitPlantAnkle * f;
            seatResHipR = getUpHipR - getUpSitStepHip * seatResR + getUpSitPlantHip * f;
            seatResKneeR = getUpKneeR + getUpSitStepKnee * seatResR - getUpSitPlantKnee * f;
            seatResAnkleR = getUpAnkleR - getUpSitStepAnkle * seatResR + getUpSitPlantAnkle * f;
            sitPlantDone = true;
            Debug.Log("GETUP-SITPLANT done t=" + N(now, 3)
                + " why=" + (contact ? "contact" : "spent")
                + " fz=" + N(fzSum, 0) + " fzMax=" + N(sitPlantFzMax, 0)
                + " frac=" + N(f, 3) + " plant=" + N(sitPlantT, 2));
            return;
        }
        sitPlantFrac = Mathf.Clamp01(sitPlantT / Mathf.Max(0.05f, getUpSitPlantSec));
    }

    // ROUND 54: the plant's drive (the step capture plus the reach's deltas
    // at the stage's fraction; per leg, like StepTargets).
    private void DriveGetUpLegsPlant()
    {
        float f = sitPlantFrac;
        float hL = getUpHipL - getUpSitStepHip * seatResL + getUpSitPlantHip * f;
        float kL = getUpKneeL + getUpSitStepKnee * seatResL - getUpSitPlantKnee * f;
        float aL = getUpAnkleL - getUpSitStepAnkle * seatResL + getUpSitPlantAnkle * f;
        float hR = getUpHipR - getUpSitStepHip * seatResR + getUpSitPlantHip * f;
        float kR = getUpKneeR + getUpSitStepKnee * seatResR - getUpSitPlantKnee * f;
        float aR = getUpAnkleR - getUpSitStepAnkle * seatResR + getUpSitPlantAnkle * f;
        DriveOneLeg(true, hL, kL, true, aL);
        DriveOneLeg(false, hR, kR, true, aR);
    }

    // ROUND 54: the plant state, fresh per attempt.
    private void ResetSitPlant()
    {
        sitPlantT = 0f; sitPlantDone = false; sitPlantEntered = false;
        sitPlantFrac = 0f; sitPlantContactT = 0f; sitPlantFzMax = 0f;
    }

    // ROUND 51: THE DISCRETE STEP runner (see the fields' block). One leg's
    // share ramps to its round target over StepSec, holds StepHold (with the
    // optional -StepPlant ease-back), then the other leg; rounds repeat the
    // pass. The shares publish through seatResL/R/T so the trace reads them.
    private void RunSitStep(float now, float dt)
    {
        if (!stepLogged)
        {
            stepLogged = true;
            Debug.Log("GETUP-SITSTEP enter t=" + N(now, 3) + " gapL=" + N(seatGapL, 3)
                + " gapR=" + N(seatGapR, 3) + " hip=" + N(getUpSitStepHip, 0)
                + " knee=" + N(getUpSitStepKnee, 0) + " rounds=" + N(getUpSitStepRounds, 0)
                + " under=" + N(getUpSitUnder, 2)
                + " fz=" + N(poseGroundFzLeftN + poseGroundFzRightN, 0));
        }
        stepT += dt;
        float w = PoseRigWeightN();
        float fzSum = poseGroundFzLeftN + poseGroundFzRightN;
        if (w > 1f && fzSum >= getUpSitStepLoad * w) stepLoadT += dt;
        else stepLoadT = 0f;
        // CYCLE 105: THE UNDER-GATE. The legacy corridor is one-sided (gap is
        // comX - footX; a foot AHEAD of the CoM reads negative and passed the
        // check trivially - measured on the flat squat take: every SITSTEP
        // exited "why=corridor" at gapL=-0.85 within 0.2 s, frozen at fL=0.36).
        // Armed, the corridor is TWO-SIDED: both feet within G of the CoM's
        // own x - the same band the stand gate calls "feet under" (0.30 m).
        bool gapsOk = getUpSitUnder > 0.001f
            ? Mathf.Abs(seatGapL) <= getUpSitUnder && Mathf.Abs(seatGapR) <= getUpSitUnder
            : seatGapL <= getUpSitStepGap && seatGapR <= getUpSitStepGap;
        bool corridor = gapsOk && stepLoadT >= getUpSitStepLoadSec;
        bool spent = stepRound >= Mathf.RoundToInt(getUpSitStepRounds);
        if (corridor || spent || stepT >= getUpSitStepMax)
        {
            stepDone = true;
            StepTargets(out seatResHipL, out seatResKneeL, out seatResAnkleL,
                out seatResHipR, out seatResKneeR, out seatResAnkleR);
            Debug.Log("GETUP-SITSTEP done t=" + N(now, 3) + " why="
                + (corridor ? "corridor" : (spent ? "spent" : "max"))
                + " gapL=" + N(seatGapL, 3) + " gapR=" + N(seatGapR, 3)
                + " loadT=" + N(stepLoadT, 2) + " fL=" + N(stepFracL, 3)
                + " fR=" + N(stepFracR, 3) + " fz=" + N(fzSum, 0));
            return;
        }
        float travelSec = Mathf.Max(0.05f, getUpSitStepSec);
        float holdSec = Mathf.Max(0f, getUpSitStepHold);
        // CYCLE 105: THE MARCH. Legacy tgt divides ONE delta across the rounds
        // (max closure ~0.1 m); armed, every completed round re-applies the
        // FULL delta - the flat base is ~0.5 m out. The round budget stays
        // -poseGetUpSitStepRounds, the time cap -poseGetUpSitStepMax.
        float tgt = getUpSitUnder > 0.001f
            ? stepRound + 1f
            : (stepRound + 1f) / getUpSitStepRounds;
        stepPhaseT += dt;
        float ramp = stepPhase == 0 ? Mathf.Clamp01(stepPhaseT / travelSec) : 1f;
        float frac = Mathf.Lerp(stepFromFrac, tgt, ramp);
        if (stepPhase == 1 && getUpSitStepPlant > 0.001f)
        {
            float back = Mathf.Clamp01(stepPhaseT / Mathf.Max(0.05f, holdSec));
            frac = Mathf.Lerp(tgt, tgt * (1f - getUpSitStepPlant), back);
        }
        if (stepLeg == 0) stepFracL = frac; else stepFracR = frac;
        if (stepPhase == 0 && stepPhaseT >= travelSec)
        {
            stepPhase = 1;
            stepPhaseT = 0f;
        }
        else if (stepPhase == 1 && stepPhaseT >= holdSec)
        {
            stepPhase = 0;
            stepPhaseT = 0f;
            if (stepLeg == 0) stepLeg = 1;
            else { stepLeg = 0; stepRound++; }
            stepFromFrac = stepLeg == 0 ? stepFracL : stepFracR;
        }
        seatResL = stepFracL; seatResR = stepFracR; seatReseatT = stepT;
    }

    // ROUND 47: the arc's key - the rate-gated clock over the arc seconds.
    private float SitArcKey()
    {
        return Mathf.Clamp01(getUpSitArcClock / Mathf.Max(0.05f, getUpSitArcSec));
    }

    // ROUND 45: the seat route's drive. Phase 0: hold the latch (the press takes
    // the load). Phase 1: the GATED pull (the r44 deltas; the machine's clock).
    // Phase 2: the r44 extension from the pulled pose.
    private void DriveGetUpLegsSeat()
    {
        float pullSecP = Mathf.Max(0.05f, getUpSitPullSec);
        if (seatPhase == 0)
        {
            getUpSitPullKey = 0f;
            DriveGetUpLegsSitPull(0f);
        }
        else if (getUpSitStepOn && seatPhase == 1)
        {
            // ROUND 51: the step machine's own drive (the historic pull is
            // not run on this route).
            // ROUND 52: once the step has handed off, the weight-transfer
            // stage's own drive runs until its exit.
            // ROUND 54: the plant stage's reach runs first (it owns the
            // frames between the hand-off and its contact gate).
            if (stepDone && getUpSitPlantOn && !sitPlantDone) DriveGetUpLegsPlant();
            else if (stepDone && getUpSitShiftOn && !sitShiftDone) DriveGetUpLegsShift();
            else DriveGetUpLegsStep();
        }
        else if (seatPhase == 1)
        {
            float p = Mathf.Clamp01(seatPullT / pullSecP);
            getUpSitPullKey = p;
            // CYCLE 109: the wait's own drive - with the plant armed the legs
            // blend from the frozen pull pose toward the plant deltas at the
            // key (it ramps down on the release; 0 = the historic pull drive).
            if (getUpSitDrawPlant > 0.001f) DriveGetUpLegsSitPullPlant(p);
            else DriveGetUpLegsSitPull(p);
        }
        else if (getUpSitReseatOn && !seatReseatDone)
        {
            // ROUND 49: the re-seat's own drive - the per-leg shares over the
            // drag's exit pose (no ext clock runs until the hand-off).
            DriveGetUpLegsReseat();
        }
        else
        {
            float tailSec = Mathf.Max(0.1f, GetUpRiseSeconds * getUpPatience
                - seatPressUsed - pullSecP);
            float k = Mathf.Clamp01(seatExtT / tailSec);
            // ROUND 47: the arc's own key (rate-gated clock).
            if (getUpSitArcSec > 0.001f) k = SitArcKey();
            getUpSitPullKey = 1f + k;
            // ROUND 46: the exit pose - a drag that stopped early (gap-out or a
            // guarded gate-dead) extends from the share it actually reached;
            // seatPullExitKey = 1f (every unarmed route) is the r45 bytes.
            // ROUND 49: a re-seat hand-off extends from its captured pose.
            if ((getUpSitReseatOn && seatReseatDone) || stepDone)
                DriveGetUpLegsStandReseat(k);
            else
                DriveGetUpLegsStandPulled(k, seatPullExitKey);
        }
    }

    // ROUND 45: the sit re-seat machine. Runs once per frame while a seat
    // attempt is live; publishes seatPhase/seatPressNow and the trace key.
    // The pull's gate: soles carrying >= Keep*W, or (modes 1/3) the palms
    // carrying >= PressMin together; a dropout up to Grace s is not a close.
    // The pull clock freezes while the gate is closed; the freeze budget
    // (FrozenMax) force-advances to the extension with a gate-dead line.
    private void RunSitSeat(float now, float dt)
    {
        if (!(getUpSitSeatMode > 0f) || !getUpSitActive || getUpStage != GetUpStage.Rise)
        {
            seatPhase = -1;
            return;
        }
        if (seatPhase < 0)
        {
            seatPhase = getUpSitSeatMode == 2f ? 1 : 0;
            seatPressUsed = 0f; seatPullT = 0f; seatExtT = 0f; seatFrozenT = 0f;
            seatGatePasses = 0; seatGatePrev = false; seatGateNow = false;
            seatGateLastOk = -999f;
            seatPullExitKey = 1f;   // ROUND 46: the drag's exit share
            ResetSitReseat();       // ROUND 49: the re-seat state, fresh
            ResetSitStep();         // ROUND 51: the step state, fresh
            seatExtArrestInit = false;   // ROUND 51c: the arrest datum is per attempt
            seatExtArrestT = 0f; seatExtArrestTrips = 0; seatExtArrestBad = false;
            ResetSitShift();        // ROUND 52: the shift state, fresh
            ResetSitPlant();        // ROUND 54: the plant state, fresh
            Debug.Log("GETUP-SITSEAT enter t=" + N(now, 3) + " mode=" + N(getUpSitSeatMode, 0)
                + " press=" + N(getUpSitSeatPress, 2) + " keep=" + N(getUpSitSeatKeep, 3)
                + " pressmin=" + N(getUpSitSeatPressMin, 3));
        }
        float weightN = PoseRigWeightN();
        float fzSum = poseGroundFzLeftN + poseGroundFzRightN;
        float palmSum = gripLoad[0] + gripLoad[1];
        bool soleOk = weightN > 1f && fzSum >= getUpSitSeatKeep * weightN;
        bool palmOk = getUpSitSeatMode != 2f && palmSum >= getUpSitSeatPressMin;
        if (soleOk || palmOk) seatGateLastOk = now;
        bool gate = (soleOk || palmOk) || (now - seatGateLastOk) <= getUpSitSeatGrace;
        if (gate && !seatGatePrev && seatPhase == 1)
        {
            seatGatePasses++;
            if (seatGatePasses == 1)
                Debug.Log("GETUP-SITSEAT gate t=" + N(now, 3) + " frozen=" + N(seatFrozenT, 2)
                    + " fz=" + N(fzSum, 0) + " palms=" + N(palmSum, 3));
        }
        seatGatePrev = gate;
        seatGateNow = gate;

        if (seatPhase == 0)
        {
            seatPressUsed += dt;
            bool bothPalms = gripLoad[0] >= getUpSitSeatPressMin && gripLoad[1] >= getUpSitSeatPressMin;
            if (bothPalms)
            {
                Debug.Log("GETUP-SITSEAT press-ok t=" + N(now, 3) + " used=" + N(seatPressUsed, 2)
                    + " gL=" + N(gripLoad[0], 3) + " gR=" + N(gripLoad[1], 3));
                getUpSitDragTilt = seatBodyTilt;   // ROUND 46: the tip datum
                seatPhase = 1;
            }
            else if (seatPressUsed >= getUpSitSeatPressMax)
            {
                Debug.Log("GETUP-SITSEAT press-cap t=" + N(now, 3) + " used=" + N(seatPressUsed, 2)
                    + " gL=" + N(gripLoad[0], 3) + " gR=" + N(gripLoad[1], 3));
                getUpSitDragTilt = seatBodyTilt;   // ROUND 46: the tip datum
                seatPhase = 1;
            }
        }
        else if (seatPhase == 1 && getUpSitStepOn)
        {
            // ROUND 51: the discrete step re-seat replaces the pull AND the
            // r49 reseat here (one foot at a time; see the fields' block).
            // ROUND 52: THE POST-STEP WEIGHT TRANSFER - once the step
            // hands off, this stage runs before the extension (the soles
            // load; see the fields' block).
            if (!stepDone)
            {
                RunSitStep(now, dt);
                if (stepDone && !(getUpSitPlantOn && !sitPlantDone)
                    && !(getUpSitShiftOn && !sitShiftDone))
                    seatPhase = 2;
            }
            else if (getUpSitPlantOn && !sitPlantDone)
            {
                // ROUND 54: the plant stage - the reach's fz contact gate
                // holds the extension until the soles reach the deck.
                RunSitPlant(now, dt);
            }
            else if (getUpSitShiftOn && !sitShiftDone)
            {
                RunSitShift(now, dt);
                if (sitShiftDone) seatPhase = 2;
            }
            else
            {
                seatPhase = 2;
            }
        }
        else if (seatPhase == 1)
        {
            // ROUND 46: the tip freeze - the fold's reaction pitches the body
            // (torso -10 -> -46 by the pull's mid-point in seat1_pB); while the
            // deviation from the drag's start pitch exceeds Tip deg the pull
            // clock freezes exactly as a closed gate would (same budget).
            bool armed46 = getUpSitDragGap > 0.001f || getUpSitDragTip > 0.001f;
            bool tipBad = false;
            if (getUpSitDragTilt < 900f)
                seatTipDev = Mathf.Abs(seatBodyTilt - getUpSitDragTilt);
            if (getUpSitDragTip > 0.001f && getUpSitDragTilt < 900f)
                tipBad = seatTipDev > getUpSitDragTip;
            // CYCLE 109: THE HEEL-PLANT IN THE DRAW'S WAIT. While the gate is
            // closed (soles unloaded) the plant window replaces the dead-man
            // count - the legs are driven down-and-close (the drive side) and
            // the frozen budget starts only once the plant's window expires.
            // The moment the gate reopens the draw advances (the release).
            // CYCLE 114: THE RE-ARM. Single-shot per attempt (c109): the first
            // release spends the window and the re-unloaded draw meets the
            // dead-man. Armed, every re-closed gate starts a FRESH episode -
            // plant -> release -> advance -> freeze -> plant, up to the
            // episode cap - so the draw advances in supported steps.
            bool plantArmed = getUpSitDrawPlant > 0.001f;
            bool reArm = plantArmed && getUpSitDrawPlantRe > 0.001f;
            if (reArm && seatDrawPlantReleased && !gate && seatDrawPlantPrevGate
                && seatDrawPlantEps + 1 < (int)getUpSitDrawPlantEps)
            {
                seatDrawPlantEps++;
                seatDrawPlantEpT = 0f;
                seatDrawPlantLogged = false; seatDrawPlantReleased = false;
                Debug.Log("GETUP-DRAWPLANT reenter t=" + N(now, 3)
                    + " eps=" + seatDrawPlantEps + " fz=" + N(fzSum, 0)
                    + " pull=" + N(seatPullT, 3) + " gapL=" + N(seatGapL, 3));
            }
            seatDrawPlantPrevGate = gate;
            bool plantWait = plantArmed && !gate
                && (reArm ? seatDrawPlantEpT : seatDrawPlantT) < getUpSitDrawPlantMax;
            if (plantWait)
            {
                seatDrawPlantEpT += dt;
                seatDrawPlantT += dt;
                if (!seatDrawPlantLogged)
                {
                    seatDrawPlantLogged = true;
                    Debug.Log("GETUP-DRAWPLANT enter t=" + N(now, 3)
                        + " K=" + N(getUpSitDrawPlant, 2) + " eps=" + seatDrawPlantEps
                        + " fz=" + N(fzSum, 0)
                        + " gapL=" + N(seatGapL, 3) + " gapR=" + N(seatGapR, 3));
                }
            }
            else if (plantArmed && gate && seatDrawPlantT > 0.001f && !seatDrawPlantReleased)
            {
                seatDrawPlantReleased = true;
                Debug.Log("GETUP-DRAWPLANT release t=" + N(now, 3)
                    + " eps=" + seatDrawPlantEps
                    + " fz=" + N(fzSum, 0) + " hold=" + N(seatDrawPlantT, 2));
            }
            float plantKTgt = plantArmed && !gate ? 1f : 0f;
            seatDrawPlantK = Mathf.MoveTowards(seatDrawPlantK, plantKTgt,
                dt / Mathf.Max(0.05f, getUpSitDrawPlantSec));
            if (gate && !tipBad) seatPullT += dt;
            else if (!plantWait) seatFrozenT += dt;
            // ROUND 46: the gap exit - both feet within Gap m of the CoM end
            // the drag where it stands; the extension starts from the exit
            // pose (seatPullExitKey), not the full fold's.
            // CYCLE 106: THE DRAG'S UNDER-GATE. The r46 rule is one-sided
            // (a foot AHEAD reads negative and passed at once - measured:
            // seatGapL -0.667 <= 0.50, so the pull fold never ran on the flat
            // sprawl). Armed, both feet must be within G of the CoM's x - the
            // fold works until the pull's own budget ends it. 0 = r46 bytes.
            bool gapOut = getUpSitDragGap > 0.001f
                && (getUpSitDragUnder > 0.001f
                    ? Mathf.Abs(seatGapL) <= getUpSitDragUnder
                        && Mathf.Abs(seatGapR) <= getUpSitDragUnder
                    : seatGapL <= getUpSitDragGap && seatGapR <= getUpSitDragGap);
            if (gapOut)
            {
                seatPullExitKey = Mathf.Clamp01(seatPullT / Mathf.Max(0.05f, getUpSitPullSec));
                Debug.Log("GETUP-SITSEAT gap-out t=" + N(now, 3) + " gapL=" + N(seatGapL, 3)
                    + " gapR=" + N(seatGapR, 3) + " pull=" + N(seatPullExitKey, 3)
                    + " tilt=" + N(seatBodyTilt, 1));
                seatPhase = 2;
            }
            else if (seatPullT >= Mathf.Max(0.05f, getUpSitPullSec))
            {
                seatPullExitKey = 1f;
                seatPhase = 2;
            }
            else if (seatFrozenT >= getUpSitSeatFrozenMax)
            {
                Debug.Log("GETUP-SITSEAT gate-dead t=" + N(now, 3) + " frozen=" + N(seatFrozenT, 2)
                    + " pull=" + N(seatPullT, 3) + " fz=" + N(fzSum, 0) + " palms=" + N(palmSum, 3));
                seatPullExitKey = armed46
                    ? Mathf.Clamp01(seatPullT / Mathf.Max(0.05f, getUpSitPullSec)) : 1f;
                seatPhase = 2;
            }
        }
        else if (!getUpSitStepOn && getUpSitReseatOn && !seatReseatDone)
        {
            // ROUND 49: the corridor re-seat runs here (the ext clock waits).
            RunSitReseat(now, dt);
        }
        else
        {
            // ROUND 51c: THE EXT SINK ARREST - the ext's clock freezes while
            // the body pitches off its ext-start marks (bounded; logged).
            float extDt = dt;
            if (getUpSitExtArrestOn && seatPhase == 2)
            {
                float pelvYNow = pelvisBody != null ? pelvisBody.position.y : 0f;
                if (!seatExtArrestInit)
                {
                    seatExtArrestInit = true;
                    seatExtPelvY0 = pelvYNow;
                    seatExtPrevTilt = seatBodyTilt;
                }
                float xRate = Mathf.Abs(seatBodyTilt - seatExtPrevTilt) / Mathf.Max(dt, 1e-4f);
                seatExtPrevTilt = seatBodyTilt;
                bool bad = xRate > getUpSitExtArrestRate
                    || pelvYNow < seatExtPelvY0 - getUpSitExtArrestDrop;
                if (bad && !seatExtArrestBad)
                {
                    seatExtArrestTrips++;
                    Debug.Log("GETUP-EXTARREST trip t=" + N(now, 3) + " rate=" + N(xRate, 1)
                        + " pelvY=" + N(pelvYNow, 3) + " y0=" + N(seatExtPelvY0, 3)
                        + " frozen=" + N(seatExtArrestT, 2) + " ext=" + N(seatExtT, 2));
                }
                seatExtArrestBad = bad;
                if (bad && seatExtArrestT < getUpSitExtArrestMax)
                {
                    seatExtArrestT += dt;
                    extDt = 0f;
                }
            }
            // CYCLE 107: THE CO-OVER GATE (see the field's block) - the plain
            // ext clock freezes while the CoM is still behind the feet.
            if (getUpSitExtOver > 0.001f && seatPhase == 2
                && Mathf.Min(seatGapL, seatGapR) < -getUpSitExtOver)
                extDt = 0f;
            seatExtT += extDt;
        }
        seatPressNow = getUpSitSeatPress;
        if (getUpSitSeatMode == 2f) seatPressNow = -1f;
        else if (getUpSitSeatMode == 3f && seatPhase >= 2 && getUpSitSeatHand > 0.001f)
            seatPressNow = getUpSitSeatPress * Mathf.Clamp01(1f - seatExtT / getUpSitSeatHand);
    }

    // ROUND 40: the push trace - the commands through a fire's first seconds.
    private void PushTraceFrame(float now, float tilt)
    {
        if (!getUpPushTraceOn || getUpPushTraceT0 < 0f) return;
        float age = now - getUpPushTraceT0;
        if (age < 0f) return;
        if (age > getUpPushTraceSec)
        {
            getUpPushTraceT0 = -1f;
            return;
        }
        float lum = lumbarJoint != null ? lumbarJoint.jointAngle : 0f;
        Debug.Log("PUSHTRACE t=" + N(now, 3) + " a=" + N(age, 3) + " st=" + getUpStage
            + " kk=" + N(gusKeyk, 3) + " kK=" + N(gusKeyKnee, 3) + " kA=" + N(gusKeyAnkle, 3)
            + " hT=" + N(gusTgtHipL, 1) + " kT=" + N(gusTgtKneeL, 1) + " aT=" + N(gusTgtAnkleL, 1)
            + " hM=" + N(leftHipJoint != null ? leftHipJoint.jointAngle : 0f, 1)
            + " kM=" + N(leftKneeJoint != null ? leftKneeJoint.jointAngle : 0f, 1)
            + " aM=" + N(leftAnkleJoint != null ? leftAnkleJoint.jointAngle : 0f, 1)
            + " tTgt=" + N(torsoTargetDeg, 1) + " tMe=" + N(tilt, 1) + " lum=" + N(lum, 1)
            + " fzL=" + N(poseGroundFzLeftN, 0) + " fzR=" + N(poseGroundFzRightN, 0));
    }

    // The trunk's own blend over the ladder: the lumbar writes the target the
    // stage in force owns (the walk/stand paths' own driver, DriveTrunk).
    private void DriveGetUpTrunk(float fromDeg, float toDeg, float share, float biasDeg = 0f)
    {
        torsoTargetDeg = Mathf.Lerp(fromDeg, toDeg, Mathf.Clamp01(share));
        // ROUND 42: the trunk governor's counter-lean (0 = byte for byte the r29
        // blend: the add is skipped, not a +0.0).
        if (biasDeg != 0f) torsoTargetDeg += biasDeg;
        DriveTrunk(0f, 0f, vestibular != null ? vestibular.GetBodyTilt() : 0f, true,
            torsoTargetDeg);
    }

    // ROUND 18: the roll entry test - the body must be on its back (the pose
    // core's own LyingBack) or the forced probe. An absent core or the flag off
    // keeps the pre-round-18 entry, bit for bit.
    private bool BodyOnBack()
    {
        if (!getUpOrientTried)
        {
            getUpOrientTried = true;
            getUpOrient = GetComponent<PoseOrientation>();
        }
        return getUpOrient != null && getUpOrient.Valid && getUpOrient.LyingBack;
    }

    // ROUND 18 v2: the back-read tracker - the dwell's own clock, and the
    // moment the pose leaves the back (which re-arms the roll cap: a later
    // back episode gets its own rolls).
    private void UpdateBackTracker(float now)
    {
        if (BodyOnBack())
        {
            if (getUpBackSince < 0f) getUpBackSince = now;
        }
        else
        {
            getUpBackSince = -1f;
            getUpRollBlocked = false;
            getUpRollBounds = 0;
        }
    }

    private void BeginAttempt(float tilt, float now)
    {
        getUpRollBounds = 0;                    // v2: the counters are per attempt
        getUpRollBlocked = false;
        getUpArbSuppL = 0;                      // ROUND 37: the arbitration counters
        getUpArbSuppR = 0;
        getUpArbFirstL = false;
        getUpArbFirstR = false;
        bool rollStart = getUpRollEnabled
            && (getUpRollForce > 0.5f
                || (BodyOnBack() && now - getUpBackSince >= getUpRollDwell));
        getUpStage = rollStart ? GetUpStage.Roll : GetUpStage.Plant;
        getUpStageEnterTime = now;
        getUpDone = false;
        getUpHandsPlanted = false;
        getUpReachedTuck = false;
        getUpStandNamed = false;
        getUpStandGateHeld = 0f;             // ROUND 30: the gate's counters are per attempt
        getUpStandGateHolds = 0;
        getUpStandGateInHold = false;
        getUpBaseWait = 0f;                  // ROUND 31: the base gate's attempt clocks
        getUpBaseFast = false;
        getUpBaseSettleT = 0f;               // ROUND 32: the settle dwell is per attempt
        getUpBaseSettleWarned = false;
        getUpBaseSettleGap = 0f;             // ROUND 33: the anti-flicker clocks
        getUpBaseLoadT = 0f;                 // ROUND 35: the load gate's clocks
        getUpBaseLoadGap = 0f;
        getUpBaseLoadWarned = false;
        getUpLoadExtT = 0f;                  // ROUND 39: the loaded-window clocks
        getUpLoadExtGap = 0f;
        getUpLoadExtLive = false;
        getUpMoveGuardHeld = 0f;             // ROUND 42: the move-guard's attempt clocks
        getUpMoveGuardHolds = 0;
        getUpMoveGuardInHold = false;
        getUpTrunkGovHeld = 0f;              // ROUND 42: the trunk governor's attempt clocks
        getUpTrunkGovHolds = 0;
        getUpTrunkGovInHold = false;
        getUpTrunkGovBias = 0f;
        getUpRiseStartPelvisY = -999f;       // ROUND 35: the climb datum is per attempt
        getUpBaseSettleDuty = 0f;
        getUpSettleDutyHead = 0;
        getUpSettleDutyCount = 0;
        getUpSettleDutySum = 0f;
        getUpSettleDutyAge = 0f;
        getUpDriveOn = true;
        getUpSitActive = false;      // ROUND 43b: a down-route attempt is never a sit one
        // The settle pose, latched the way the crawl latches its own.
        getUpHipL = leftHipJoint != null ? leftHipJoint.jointAngle : 0f;
        getUpHipR = rightHipJoint != null ? rightHipJoint.jointAngle : 0f;
        getUpKneeL = leftKneeJoint != null ? leftKneeJoint.jointAngle : 0f;
        getUpKneeR = rightKneeJoint != null ? rightKneeJoint.jointAngle : 0f;
        getUpAnkleL = leftAnkleJoint != null ? leftAnkleJoint.jointAngle : 0f;
        getUpAnkleR = rightAnkleJoint != null ? rightAnkleJoint.jointAngle : 0f;
        getUpTrunkStartDeg = tilt;
        afterfallCrawlOn = false;
        if (rollStart)
            Debug.Log("GETUP-ROLL enter t=" + N(now, 3) + " tilt=" + N(tilt, 2)
                + " why=" + (getUpRollForce > 0.5f ? "force" : "start"));
        if (balance != null)
        {
            balance.getUpAttempts++;
            balance.getUpPose = 1;
            if (balance.getUpPoseMax < 1) balance.getUpPoseMax = 1;
            balance.locomotionState = BalanceController.LocomotionState.GettingUp;
            balance.currentState = BalanceController.BalanceState.Recovery;
        }
    }

    // ROUND 43: the sit-fired attempt. The same per-attempt resets BeginAttempt
    // runs (minus the roll / the Plant entry: the sit IS the base, the ladder
    // jumps straight to the Rise), with the pose latch taken HERE, from the body
    // as it sits - the r40 lesson: the hold phase drives the legs to the LATCHED
    // pose, so a stale latch (an old fall's landing) would snap them away.
    private void BeginSitAttempt(float tilt, float now, float pelvisY)
    {
        getUpRollBounds = 0;
        getUpRollBlocked = false;
        getUpArbSuppL = 0;
        getUpArbSuppR = 0;
        getUpArbFirstL = false;
        getUpArbFirstR = false;
        getUpStage = GetUpStage.Rise;
        getUpStageEnterTime = now;
        getUpDone = false;
        getUpHandsPlanted = false;
        getUpReachedTuck = false;
        getUpStandNamed = false;
        getUpStandGateHeld = 0f;
        getUpStandGateHolds = 0;
        getUpStandGateInHold = false;
        getUpBaseWait = 0f;
        getUpBaseFast = false;
        getUpBaseSettleT = 0f;
        getUpBaseSettleWarned = false;
        getUpBaseSettleGap = 0f;
        getUpBaseLoadT = 0f;
        getUpBaseLoadGap = 0f;
        getUpBaseLoadWarned = false;
        getUpLoadExtT = 0f;
        getUpLoadExtGap = 0f;
        getUpLoadExtLive = false;
        getUpMoveGuardHeld = 0f;
        getUpMoveGuardHolds = 0;
        getUpMoveGuardInHold = false;
        getUpTrunkGovHeld = 0f;
        getUpTrunkGovHolds = 0;
        getUpTrunkGovInHold = false;
        getUpTrunkGovBias = 0f;
        getUpRiseStartPelvisY = -999f;
        getUpBaseSettleDuty = 0f;
        getUpSettleDutyHead = 0;
        getUpSettleDutyCount = 0;
        getUpSettleDutySum = 0f;
        getUpSettleDutyAge = 0f;
        getUpDriveOn = true;
        getUpHipL = leftHipJoint != null ? leftHipJoint.jointAngle : 0f;
        getUpHipR = rightHipJoint != null ? rightHipJoint.jointAngle : 0f;
        getUpKneeL = leftKneeJoint != null ? leftKneeJoint.jointAngle : 0f;
        getUpKneeR = rightKneeJoint != null ? rightKneeJoint.jointAngle : 0f;
        getUpAnkleL = leftAnkleJoint != null ? leftAnkleJoint.jointAngle : 0f;
        getUpAnkleR = rightAnkleJoint != null ? rightAnkleJoint.jointAngle : 0f;
        getUpTrunkStartDeg = tilt;
        afterfallCrawlOn = false;
        getUpSitActive = true;
        getUpSitStandExt = 0f;
        getUpSitStandT = 0f;
        getUpSitStandFires++;
        getUpSitPullKey = 0f;      // ROUND 44: the pull route's own clocks
        getUpSitPullLogged = false;
        getUpSitDrawLogged = false;   // CYCLE 108: the draw's hold line is per attempt
        seatDrawPlantT = 0f; seatDrawPlantK = 0f;
        seatDrawPlantLogged = false; seatDrawPlantReleased = false;   // CYCLE 109: per attempt
        seatDrawPlantEpT = 0f; seatDrawPlantEps = 0; seatDrawPlantPrevGate = false;   // CYCLE 114
        // ROUND 47: the arc's own state (the fire is the base datum).
        getUpSitArcClock = 0f;
        getUpSitArcHeld = 0f;
        getUpSitArcHolds = 0;
        getUpSitArcInHold = false;
        getUpSitArcTiltRate = 0f;
        getUpSitArcHShare = 0f;
        getUpSitRockClock = 0f;    // CYCLE 106: the rock's clock, fresh at the fire
        getUpSitArcBasePelvY = pelvisY;
        getUpSitArcPrevTilt = tilt;
        if (getUpSitArcSec > 0.001f)
            Debug.Log("GETUP-SITARC enter t=" + N(now, 3) + " sec=" + N(getUpSitArcSec, 2)
                + " torso=" + N(getUpSitArcTorso, 1) + " rate=" + N(getUpSitArcRate, 1)
                + " tieext=" + N(getUpSitArcTieExt, 0)
                + " rock=" + N(getUpSitRockSec, 2) + " gate=" + N(getUpSitRockGate, 0)
                + " over=" + N(getUpSitExtOver, 2));
        seatPhase = -1;            // ROUND 45: the seat machine re-enters fresh
        ResetSitReseat();          // ROUND 49
        seatPressNow = 0f;
        seatGateNow = false;
        if (balance != null)
        {
            balance.getUpAttempts++;
            balance.getUpPose = 4;
            if (balance.getUpPoseMax < 4) balance.getUpPoseMax = 4;
            balance.getUpSitFires = getUpSitStandFires;
            balance.locomotionState = BalanceController.LocomotionState.GettingUp;
            balance.currentState = BalanceController.BalanceState.Recovery;
        }
        Debug.Log("GETUP-SITSTAND enter t=" + N(now, 3) + " drop=" + N(standPelvisY - pelvisY, 3)
            + " fzL=" + N(poseGroundFzLeftN, 0) + " fzR=" + N(poseGroundFzRightN, 0)
            + " tilt=" + N(tilt, 1));
    }

    private void SetGetUpStage(GetUpStage stage, float now)
    {
        getUpStage = stage;
        getUpStageEnterTime = now;
    }

    // The attempt ends: the grips are released (nothing of this cycle may act
    // outside the get-up's window) and the ladder returns to Idle, which is what
    // hands the body back to the walk path on the next step.
    private void FinishAttempt(bool stood, float now)
    {
        EndAttempt();
        getUpDriveOn = false;
        getUpRetryAfter = stood ? -1f : now + GetUpRetrySeconds * getUpPatience;
        // The walk resumes from a fresh step: the step clock stopped when the
        // body went down, so a stale phase would start the gait mid-swing.
        ResetStepClock();
        if (balance != null)
            balance.locomotionState = stood
                ? BalanceController.LocomotionState.Walking
                : BalanceController.LocomotionState.Fallen;
        if (getUpSitActive)
            Debug.Log("GETUP-SITSTAND end t=" + N(now, 3) + " ext=" + N(getUpSitStandExt, 2)
                + " stood=" + (stood ? 1 : 0));
        getUpSitActive = false;
        // ROUND 45: the r44 wart - the trace's sitPull key staled at 2.0 after an
        // attempt (it reset only at BeginSitAttempt). Every exit passes here.
        getUpSitPullKey = 0f;
        seatPhase = -1;
        ResetSitReseat();          // ROUND 49: no stale re-seat state
        // ROUND 37: the arbitration's own report - one line per attempt that
        // actually refused pelvis-law writes (the raw counts; the running log
        // mirror goes to the stand log).
        if (getUpHipArb > 0.5f && (getUpArbSuppL + getUpArbSuppR) > 0)
            Debug.Log("GETUP-HIPARB attempt t=" + N(now, 3) + " suppL=" + getUpArbSuppL
                + " suppR=" + getUpArbSuppR + " totL=" + getUpArbSuppLTot
                + " totR=" + getUpArbSuppRTot + (stood ? " stood" : " notstood"));
        // The trace is NOT closed here: one run's trace is one file from the
        // first down frame to the run's end, so a retry after a spent attempt
        // cannot truncate the attempts that came before it (the first build of
        // this cycle lost attempts 1 and 2 to exactly that).
    }

    // Every way out of the get-up passes through here: the grips let go and the
    // ladder resets. Idempotent - it is called on every frame the body is not
    // down, whether or not an attempt ever ran.
    private void EndAttempt()
    {
        if (!gripHeld[0] && !gripHeld[1] && getUpStage == GetUpStage.Idle && !getUpDriveOn)
            return;
        ReleaseGrip(0, false);
        ReleaseGrip(1, false);
        getUpStage = GetUpStage.Idle;
        getUpDownSince = -1f;
    }

    // Called from the walk/stand path once the body is no longer down: the get-up
    // is over (whether it stood or ran out of bounds), the grips are off and the
    // step clock starts fresh so the walk keys from zero.
    private void EndGetUp()
    {
        bool wasActive = getUpStage != GetUpStage.Idle || getUpDriveOn;
        EndAttempt();
        getUpDriveOn = false;
        // CYCLE 100: the get-up's plant reach must never outlive the get-up.
        if (armDrive != null) armDrive.getUpPlantReach = 0f;
        if (wasActive)
        {
            getUpRetryAfter = -1f;
            ResetStepClock();
        }
        // The writer stays open for the whole run (see FinishAttempt): a second
        // fall later in the same run appends to the same trace instead of
        // truncating it. It is closed by OnDestroy / OnApplicationQuit.
    }

    private void ResetStepClock()
    {
        stepClock = 0f;
        poseTailLoadHeld = 0f;
    }

    // ─── the grip: the palms' own measured contact, the dog's three numbers ───
    // The palm's normal load is read off the SAME contact impulses the sole meter
    // reads (BodyStateEstimator's own source of truth: the solver's accumulated
    // impulses), on the hand's own collider, in body weights - so the engage is a
    // measurement of the press, not of the press's intent.
    private void SampleHandGrip(float dt)
    {
        float weightN = human != null ? Mathf.Abs(Physics2D.gravity.y) * human.totalMass : 0f;
        for (int side = 0; side < 2; side++)
        {
            gripTornThisStep[side] = false;
            Collider2D col = handCollider[side];
            if (col == null) continue;
            if (!handGripFilterReady)
            {
                handGripFilter = new ContactFilter2D();
                handGripFilter.SetLayerMask(GroundLayers.Mask);
                handGripFilter.useTriggers = false;
                handGripFilterReady = true;
            }
            int n = col.GetContacts(handGripFilter, handGripContacts);
            int count = Mathf.Min(n, handGripContacts.Length);
            float fn = 0f, tf = 0f;
            for (int k = 0; k < count; k++)
            {
                ContactPoint2D cp = handGripContacts[k];
                if (cp.normalImpulse * cp.normal.y <= 0f) continue;
                fn += cp.normalImpulse * cp.normal.y;
                tf += Mathf.Abs(cp.tangentImpulse);
            }
            gripFnN[side] = fn / dt;
            gripTfN[side] = tf / dt;
            gripLoad[side] = weightN > 1f ? gripFnN[side] / weightN : 0f;
            gripCapN[side] = PairMu() * gripFnN[side];
            if (!handGripEnabled || getUpStage == GetUpStage.Idle)
            {
                // No grip family, or no attempt running: the palm carries its own
                // material and the state below is a reading of a free palm.
                gripPressWants[side] = false;
                ReleaseGrip(side, false);
                continue;
            }
            // CYCLE 110: THE PRESS LATCH. The measured-load engage alone never
            // fired on the flat get-up (scene_grip2: the palm's own peak load
            // 0.04 body weights): a live press ask - ArmDrive's press request
            // or a running stroke phase - over a real touch engages instead.
            // -poseHandGripPress 0 = the pre-110 law bit for bit.
            bool pressLive = handGripPressOn && PressWants(side);
            gripPressWants[side] = pressLive;
            if (!gripHeld[side])
            {
                if (gripLoad[side] >= GripEngageLoad || (pressLive && count > 0))
                {
                    gripHeld[side] = true;
                    gripUnloadAge[side] = 0f;
                    gripSlipM[side] = 0f;
                    gripAnchor[side] = col.bounds.center;
                    gripEngages[side]++;
                    if (pressLive && !gripPressLogged[side])
                    {
                        gripPressLogged[side] = true;
                        Debug.Log("GRIP-PRESS engage side=" + side
                            + " t=" + N(Time.fixedTime, 3)
                            + " fn=" + N(fn, 1) + " load=" + N(gripLoad[side], 4));
                    }
                    if (human != null) human.SetHandGrip(col, true);
                }
            }
            else
            {
                // The anchored slip: the palm's own point's drift since the
                // engage. Past the budget the grip is torn out and latched until
                // the palm unloads and presses again.
                gripSlipM[side] = ((Vector2)col.bounds.center - gripAnchor[side]).magnitude;
                if (gripSlipM[side] > handGripSlipMeters)
                {
                    gripTears[side]++;
                    gripTornThisStep[side] = true;
                    ReleaseGrip(side, true);
                }
                else if (gripLoad[side] < GripReleaseLoad && !pressLive)
                {
                    gripUnloadAge[side] += dt;
                    if (gripUnloadAge[side] >= handGripHoldSeconds) ReleaseGrip(side, false);
                }
                else
                {
                    gripUnloadAge[side] = 0f;
                }
            }
        }
    }

    // CYCLE 110: the press the grip asks authority from, per side - ArmDrive's
    // own press request (the get-up's ask) or a running stroke phase (1..4).
    // Off = the pre-110 load-only engage.
    private bool PressWants(int side)
    {
        if (armDrive == null) return false;
        if (armDrive.PressRequest >= 0.01f) return true;
        return (side == 0 ? armDrive.PressPhaseL : armDrive.PressPhaseR) >= 1;
    }

    // ---- CYCLE 110: the grip row (the take's own witness; nothing reads it back) ----
    public int CaptureGripEnabled => handGripEnabled ? 1 : 0;
    public int CaptureGripStage => (int)getUpStage;
    public int CaptureGripWants(int side) => gripPressWants[side] ? 1 : 0;
    public int CaptureGripHeld(int side) => gripHeld[side] ? 1 : 0;
    public float CaptureGripFn(int side) => gripFnN[side];
    public float CaptureGripLoad(int side) => gripLoad[side];
    public float CaptureGripSlip(int side) => gripSlipM[side];
    public int CaptureGripEngages(int side) => gripEngages[side];
    public int CaptureGripTears(int side) => gripTears[side];
    public float CaptureGripMuMat => HandMaterialFriction(0);
    public float CaptureGripMuPair => PairMu();
    public float CaptureGripPalmX(int side) =>
        handCollider[side] != null ? handCollider[side].bounds.center.x : 0f;
    public float CaptureGripPalmY(int side) =>
        handCollider[side] != null ? handCollider[side].bounds.center.y : 0f;

    private void ReleaseGrip(int side, bool tore)
    {
        if (!gripHeld[side]) return;
        gripHeld[side] = false;
        gripUnloadAge[side] = 0f;
        if (human != null && handCollider[side] != null)
            human.SetHandGrip(handCollider[side], false);
    }

    // The pair coefficient the 2D contact response itself combines, from the two
    // materials in force (PhysicsMaterial2D.GetCombinedValues - the function the
    // engine's own friction uses, not a rule assumed here). Cached: the ground's
    // material never changes, and the palm's is read live because the grip swaps
    // it.
    private float handGripMuCache = -1f;
    private float handGripMuGround = -1f;

    private float PairMu()
    {
        if (handGripMuCache >= 0f) return handGripMuCache;
        if (handGripMuGround < 0f)
        {
            GameObject ground = GameObject.Find("Ground");
            Collider2D groundCol = ground != null ? ground.GetComponent<Collider2D>() : null;
            handGripMuGround = groundCol != null && groundCol.sharedMaterial != null
                ? Mathf.Max(0f, groundCol.sharedMaterial.friction) : 0.4f;
        }
        float palmMu = 1.4f;
        PhysicsMaterialCombine2D palmMode = PhysicsMaterialCombine2D.Average;
        Collider2D palm = handCollider[0] != null ? handCollider[0] : handCollider[1];
        if (palm != null && palm.sharedMaterial != null)
        {
            palmMu = Mathf.Max(0f, palm.sharedMaterial.friction);
            palmMode = palm.sharedMaterial.frictionCombine;
        }
        handGripMuCache = Mathf.Max(0f,
            PhysicsMaterial2D.GetCombinedValues(palmMu, handGripMuGround, palmMode,
                PhysicsMaterialCombine2D.Average));
        return handGripMuCache;
    }

    // The palm's own material friction in force - the number A2 of the cycle asks
    // for: above the palm's free 1.4 while a grip holds.
    private float HandMaterialFriction(int side)
    {
        Collider2D col = handCollider[side];
        return col != null && col.sharedMaterial != null
            ? col.sharedMaterial.friction : 0f;
    }

    // ─── the get-up's own trace (witness only; nothing reads it back) ───
    private void OpenGetUpTrace()
    {
        if (!getUpEnabled || getUpTrace != null) return;
        string label = GetString("-label", "trial");
        string folder = GetString("-out", ".");
        try
        {
            getUpTrace = new System.IO.StreamWriter(
                System.IO.Path.Combine(folder, label + "-getup.csv"), false);
            getUpTrace.WriteLine(
                "frame;t;stage;loco;tilt;pelvisY;pelvisH;standH;comX;comY;comStand;"
                + "footLx;footRx;footLy;footRy;footDownL;footDownR;underL;underR;"
                + "tiltBar;comBar;feetBar;handContactL;handContactR;pressPhaseL;pressPhaseR;"
                + "ask;gripL;gripR;gripLoadL;gripLoadR;fnL;fnR;tfL;tfR;capL;capR;muMat;muPair;"
                + "slipL;slipR;engagesL;engagesR;tearsL;tearsR;stageAge;gateHeld;gateHolds;baseWait;baseAborts;settleT;settleBlocks;settleDuty;settleGap"
                + ";sitWin;sitExt;sitPull;seatGate;seatFr;dragGapL;dragGapR;dragTip"
                + ";pvV;arcK;arcHld;hShare;tRate;tgtHipL"
                + ";rsL;rsR;rsT;tgtKneeL;tgtAnkleL;shiftT;shiftFzMax;shiftGap"
                + ";plantT;plantFrac;plantFzMax;drawPlantK;drawPlantT;drawPlantEps;drawPlantEpT");
        }
        catch (System.Exception)
        {
            getUpTrace = null;
        }
    }

    private void WriteGetUpTrace(float tilt, float pelvisY, float pelvisH, float standH,
                                 float comX, float comY, float standComY,
                                 float leftFootX, float rightFootX,
                                 float leftFootY, float rightFootY,
                                 bool leftDown, bool rightDown,
                                 bool leftUnder, bool rightUnder,
                                 bool tiltBar, bool comBar, bool feetBar, float stageAge)
    {
        if (!getUpEnabled || getUpTrace == null) return;
        getUpTraceRow.Clear();
        getUpTraceRow.Append(Time.frameCount).Append(';')
            .Append(N(Time.fixedTime, 3)).Append(';')
            .Append((int)getUpStage).Append(';')
            .Append((int)(balance != null ? balance.locomotionState
                : BalanceController.LocomotionState.Standing)).Append(';')
            .Append(N(tilt, 2)).Append(';').Append(N(pelvisY, 4)).Append(';')
            .Append(N(pelvisH, 4)).Append(';').Append(N(standH, 4)).Append(';')
            .Append(N(comX, 4)).Append(';').Append(N(comY, 4)).Append(';')
            .Append(float.IsNaN(standComY) ? "-1" : N(standComY, 4)).Append(';')
            .Append(N(leftFootX, 4)).Append(';').Append(N(rightFootX, 4)).Append(';')
            .Append(N(leftFootY, 4)).Append(';').Append(N(rightFootY, 4)).Append(';')
            .Append(leftDown ? 1 : 0).Append(';').Append(rightDown ? 1 : 0).Append(';')
            .Append(leftUnder ? 1 : 0).Append(';').Append(rightUnder ? 1 : 0).Append(';')
            .Append(tiltBar ? 1 : 0).Append(';').Append(comBar ? 1 : 0).Append(';')
            .Append(feetBar ? 1 : 0).Append(';')
            .Append(armDrive != null && armDrive.HandContactL ? 1 : 0).Append(';')
            .Append(armDrive != null && armDrive.HandContactR ? 1 : 0).Append(';')
            .Append(armDrive != null ? armDrive.PressPhaseL : -1).Append(';')
            .Append(armDrive != null ? armDrive.PressPhaseR : -1).Append(';')
            .Append(N(armDrive != null ? armDrive.PressRequest : 0f, 3)).Append(';')
            .Append(gripHeld[0] ? 1 : 0).Append(';').Append(gripHeld[1] ? 1 : 0).Append(';')
            .Append(N(gripLoad[0], 4)).Append(';').Append(N(gripLoad[1], 4)).Append(';')
            .Append(N(gripFnN[0], 2)).Append(';').Append(N(gripFnN[1], 2)).Append(';')
            .Append(N(gripTfN[0], 2)).Append(';').Append(N(gripTfN[1], 2)).Append(';')
            .Append(N(gripCapN[0], 2)).Append(';').Append(N(gripCapN[1], 2)).Append(';')
            .Append(N(HandMaterialFriction(0), 3)).Append(';').Append(N(PairMu(), 3)).Append(';')
            .Append(N(gripSlipM[0], 4)).Append(';').Append(N(gripSlipM[1], 4)).Append(';')
            .Append(gripEngages[0]).Append(';').Append(gripEngages[1]).Append(';')
            .Append(gripTears[0]).Append(';').Append(gripTears[1]).Append(';')
            .Append(N(stageAge, 3))
            .Append(';').Append(N(getUpStandGateHeld, 3)).Append(';').Append(getUpStandGateHolds)
            .Append(';').Append(N(getUpBaseWait, 3)).Append(';').Append(getUpBaseAborts)
            .Append(';').Append(N(getUpBaseSettleT, 3)).Append(';').Append(getUpBaseSettleBlocks)
            .Append(';').Append(N(getUpBaseSettleDuty, 3)).Append(';').Append(N(getUpBaseSettleGap, 3))
            .Append(';').Append(getUpSitWinNow ? 1 : 0).Append(';').Append(N(getUpSitStandExt, 3))
            .Append(';').Append(N(getUpSitPullKey, 3))
            .Append(';').Append(seatGateNow ? 1 : 0).Append(';').Append(N(seatFrozenT, 2))
            .Append(';').Append(N(seatGapL, 3)).Append(';').Append(N(seatGapR, 3))
            .Append(';').Append(N(seatTipDev, 1))
            .Append(';').Append(N(getUpTracePelvVel, 3)).Append(';').Append(N(getUpSitArcClock, 3))
            .Append(';').Append(N(getUpSitArcHeld, 3)).Append(';').Append(N(getUpSitArcHShare, 3))
            .Append(';').Append(N(getUpSitArcTiltRate, 1)).Append(';').Append(N(gusTgtHipL, 1))
            .Append(';').Append(N(seatResL, 3)).Append(';').Append(N(seatResR, 3))
            .Append(';').Append(N(seatReseatT, 3))
            .Append(';').Append(N(gusTgtKneeL, 1)).Append(';').Append(N(gusTgtAnkleL, 1))
            .Append(';').Append(N(sitShiftT, 3)).Append(';').Append(N(sitShiftFzMax, 0))
            .Append(';').Append(N(sitShiftGapMin, 3))
            .Append(';').Append(N(sitPlantT, 3)).Append(';').Append(N(sitPlantFrac, 3))
            .Append(';').Append(N(sitPlantFzMax, 0))
            .Append(';').Append(N(seatDrawPlantK, 3)).Append(';').Append(N(seatDrawPlantT, 3))
            .Append(';').Append(N(seatDrawPlantEps, 0)).Append(';').Append(N(seatDrawPlantEpT, 3));
        getUpTrace.WriteLine(getUpTraceRow.ToString());
        getUpTraceFlushAge += Time.fixedDeltaTime;
        if (getUpTraceFlushAge >= 0.2f)
        {
            getUpTrace.Flush();
            getUpTraceFlushAge = 0f;
        }
    }

    private void CloseGetUpTrace()
    {
        if (getUpTrace == null) return;
        try
        {
            getUpTrace.Flush();
            getUpTrace.Dispose();
        }
        catch (System.Exception)
        {
            // a closed run is not a crash: the rows already written stand.
        }
        getUpTrace = null;
    }

    // Invariant-culture number for the trace (the CSV's own convention).
    private static string N(float value, int digits)
    {
        return value.ToString("F" + digits, CultureInfo.InvariantCulture);
    }

    private static string GetString(string name, string fallback)
    {
        string[] args = System.Environment.GetCommandLineArgs();
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

    // ─── ROUND 36 (probe): THE HIP-CHANNEL BURST LOGGER ───
    // Pure instrumentation: every call is a Debug.Log gated by the [From, To]
    // window (To <= From = off; a take that does not pass -poseHipDbgFrom /
    // -poseHipDbgTo never reaches a line). It writes no state and is removed
    // after the round's probe build. HIPW = one hip write of a driving path
    // (the tag names the writer); HIPF = the frame's state (all four hip
    // muscles), logged at TickGetUp's entry and exit so a writer between the
    // log points shows up as a delta with no HIPW line.
    private bool HipDbgOn()
    {
        float t = Time.fixedTime;
        return hipDbgTo > hipDbgFrom && t >= hipDbgFrom && t <= hipDbgTo;
    }

    private void HipDbgWrite(string tag, float sig, float tgt, HingeJoint2D hip, Muscle fx, Muscle ex)
    {
        if (!HipDbgOn()) return;
        Debug.Log("HIPW t=" + N(Time.fixedTime, 3) + " " + tag
            + " sig=" + N(sig, 4) + " tgt=" + N(tgt, 2)
            + " ang=" + N(hip != null ? hip.jointAngle : 0f, 3)
            + " flex=" + N(fx != null ? fx.activation : 0f, 5)
            + " ext=" + N(ex != null ? ex.activation : 0f, 5));
    }

    private void HipDbgFrame(string tag)
    {
        if (!HipDbgOn()) return;
        Debug.Log("HIPF t=" + N(Time.fixedTime, 3) + " " + tag
            + " Lf=" + N(balance != null && balance.leftHipFlexor != null ? balance.leftHipFlexor.activation : -1f, 5)
            + " Le=" + N(balance != null && balance.leftHipExtensor != null ? balance.leftHipExtensor.activation : -1f, 5)
            + " Rf=" + N(balance != null && balance.rightHipFlexor != null ? balance.rightHipFlexor.activation : -1f, 5)
            + " Re=" + N(balance != null && balance.rightHipExtensor != null ? balance.rightHipExtensor.activation : -1f, 5)
            + " stage=" + getUpStage);
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

    // WALK CYCLE 21: THE SOLE-PLATE METER'S VERTICAL AXIS, PER FOOT, N.
    // Written on every tick by SampleGroundReactionX (the same four plates,
    // the same support filter, the same frame as the horizontal column), in
    // the recorder's fzL/fzR convention: Fn = sum(normalImpulse * n.y) / dt.
    // The lead swap's load gate reads them; nothing else does. Their default
    // 0 is also their "the meter is not running" reading, so a meter that
    // never resolves leaves the lead path closed and the step on its clock.
    private float poseGroundFzLeftN;
    private float poseGroundFzRightN;

    // ═══ WALK CYCLE 6z (recorder only): THE HORIZONTAL GROUND REACTION, MEASURED ═══
    // 6y located the walk's death in the CoM itself - comVelX turned negative
    // 1.02 s into the drive and never came back - and the record could not say
    // why: there was no horizontal force column anywhere, so every "the body is
    // braked" statement was an inference, not a read. This is that column's
    // source, read from the SAME meter the recorder's fzL/fzR come from: the
    // solver's own contact impulses on the four sole plates, not a geometric
    // probe, not a hardcoded mass and not a re-derivation from joint torques.
    //
    // The vector: a contact's accumulated impulse ON THE PLATE is
    //     P = normalImpulse * n + tangentImpulse * t
    // with n the contact normal Unity publishes and t the tangent Box2D solves
    // friction along. Box2D's own definition (b2_contact_solver.cpp v2.4.1):
    //     b2Vec2 tangent = b2Cross(normal, 1.0f);        // (n.y, -n.x)
    //     b2Vec2 P = normalImpulse * normal + tangentImpulse * tangent;
    // with P applied to one body of the pair and -P to the other, so the two
    // published scalars are the components of ONE impulse vector along (n, t).
    // The x component is therefore
    //     P.x = normalImpulse * n.x + tangentImpulse * n.y
    // and the filter below is the load meter's own support test: only a contact
    // that pushes the plate along +n (normalImpulse * n.y > 0 - the plate on the
    // far side of the normal from what it presses) counts, exactly as
    // TrialRecorder.SampleSoleLoad counts weight. The two columns therefore
    // describe the SAME contact set, on the SAME frame, as two components of one
    // impulse sum: Fx = sum(P.x) / dt, N, +x = the ground pushes the body forward.
    //
    // The sign is checkable from the same CSV without a second build:
    // sum(Fx) is the ONLY horizontal external force on the body (gravity has no
    // x component), so it must satisfy sum(Fx) = mass * d(comVelX)/dt - a
    // positive slope against the row's own acceleration column, near the rig's
    // mass, is the convention confirmed; a NEGATIVE slope means the tangent term
    // is inverted and the column must be read backwards.
    // Sentinels: 0 exactly = the meter ran and neither foot carried load on this
    // step (a reading, not a gap); PoseGroundFxNoMeasureN = the meter could not
    // run at all (no sole plate resolved, or this component never ticked).
    private float SampleGroundReactionX()
    {
        if (!groundFxPlatesResolved) ResolveGroundFxPlates();
        bool anyPlate = false;
        for (int i = 0; i < 4; i++)
            if (groundFxPlate[i] != null) anyPlate = true;
        if (!anyPlate) return PoseGroundFxNoMeasureN;
        if (!groundFxFilterReady)
        {
            groundFxFilter = new ContactFilter2D();
            groundFxFilter.SetLayerMask(GroundLayers.Mask);
            groundFxFilter.useTriggers = false;
            groundFxFilterReady = true;
        }
        float dt = Time.fixedDeltaTime;
        if (dt <= 0f) dt = 0.005f;
        float impulseX = 0f;
        float impulseYLeft = 0f;
        float impulseYRight = 0f;
        // Heel and toe are two plates of one support; the sum runs over all four,
        // so both feet are read and the two sides are already summed.
        for (int i = 0; i < 4; i++)
        {
            Collider2D col = groundFxPlate[i];
            if (col == null) continue;
            int n = col.GetContacts(groundFxFilter, groundFxContacts);
            int count = Mathf.Min(n, groundFxContacts.Length);
            for (int k = 0; k < count; k++)
            {
                ContactPoint2D cp = groundFxContacts[k];
                // The load meter's own support filter, verbatim: only an impulse
                // that pushes the plate up the normal carries weight.
                if (cp.normalImpulse * cp.normal.y <= 0f) continue;
                impulseX += cp.normalImpulse * cp.normal.x
                    + cp.tangentImpulse * cp.normal.y;
                // WALK CYCLE 21: the same impulse's vertical component, added to
                // the support the plate belongs to (0/1 = left heel/toe, 2/3 =
                // right heel/toe - ResolveGroundFxPlates). This is the fz reading
                // the row's fzL/fzR column carries, taken on this frame.
                float fn = cp.normalImpulse * cp.normal.y;
                if (i < 2) impulseYLeft += fn;
                else impulseYRight += fn;
            }
        }
        poseGroundFzLeftN = impulseYLeft / dt;
        poseGroundFzRightN = impulseYRight / dt;
        return impulseX / dt;
    }

    private void ResolveGroundFxPlates()
    {
        groundFxPlate[0] = GroundFxPlate("LeftLegFoot");
        groundFxPlate[1] = GroundFxPlate("LeftLegToe");
        groundFxPlate[2] = GroundFxPlate("RightLegFoot");
        groundFxPlate[3] = GroundFxPlate("RightLegToe");
        bool any = groundFxPlate[0] != null || groundFxPlate[1] != null
            || groundFxPlate[2] != null || groundFxPlate[3] != null;
        // Only latch when something was found - the same rule the recorder's own
        // resolver follows: an early call must not freeze four nulls forever.
        if (any) groundFxPlatesResolved = true;
    }

    private Collider2D GroundFxPlate(string name)
    {
        Transform t = transform.Find(name);
        return t != null ? t.GetComponent<Collider2D>() : null;
    }

    // WALK CYCLE 40: the palm's own collider, by the segment's own name (the
    // same lookup the wrist joint pair uses in Initialize).
    private static Collider2D HandCollider(Transform root, string name)
    {
        Transform t = root.Find(name);
        return t != null ? t.GetComponent<Collider2D>() : null;
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
        public bool placeKneeLive;       // WALK CYCLE 75/76: the placement bias reads the
                                         //   knee the descent leaves (the c68 §3 fix);
                                         //   false = the landed drawn-knee expression
        public float swingAnkleDeg;      // WALK CYCLE 37: the swing ankle's own carry
                                         //   (deg, the rig's own sign; plus = toe
                                         //   down / heel rise), gated and ramped by
                                         //   the driver's tail gate; 0 = off
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
        public float stanceFoldKneeDeg;  // WALK CYCLE 64: the drawn stance knee at the
        public float stancePlaceKneeDeg; //   SwingFold / FootPlace / PlantLoad keys
        public float stanceLoadKneeDeg;  //   (the ask itself; the drawn values default)
        public float heightGainDegPerM;  // the stand's verified height pair, as it is
        public float heightDampDegPerMps;
        public float hipMin, hipMax;     // Human.cs limits (pose clamp + the table's
        public float kneeMin, kneeMax;   // own audit), degrees
        public float ankleLimit;         // symmetric ankle pair, Human.cs:555
        public float toeMin, toeMax;
        public float stanceSoleHoldM;    // WALK CYCLE 28, from BALANCE LAYER: the sole
                                         //   lowering the recruit holds for the
                                         //   SUPPORT leg, metres (0 = not in force)
        public float stanceSoleLeverM;   // the sole's front-bottom corner about the ankle
                                         //   hinge, m - the arm the hold's ankle press
                                         //   is read against
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

    // WALK CYCLE 28: cycle 27's hold account, kept (the cap the row priced, the
    // gate's own load bound, the band, the ceiling, the release) - and this
    // round's own key: the delivered stiffness. Cycle 27 measured the ask->joint
    // delivery at about a quarter (a +8.85..10.45 deg ankle ask moved the joint
    // +1.3..3.7 deg, a -13 deg knee ask moved -5.4..7.0), which is why every
    // build that DID move the sole still cleared the flag at 1..2 frames: the
    // band and the ceiling were never the limit, the DELIVERY was. StanceHold-
    // DeliverP multiplies the support leg's knee/ankle P demand while the hold is
    // live (the D pair is untouched, so the pair's damping ratio is unchanged).
    public const float SupportHoldClearM = 0.0180f;
    public const float SupportHoldLoadN = 50f;

    // WALK CYCLE 35: the tail gate's own need bound (cycle 34's own: 1 is the lead
    // the swap rule calls a swap; the load bound above is cycle 28's own). It is
    // the hold's confine - on the landed row the pair confines the latch's first
    // open frame to 8.150.
    public const float PlantHoldNeedMin = 0.9f;
    // The stiff delivery's own gate: the support's sole inside the flag's break
    // zone (the flag grounds up to 0.0200..0.0215 and clears from 0.0205..0.0241;
    // no single-support frame of the base row exceeds 0.0190 before 8.200).
    // Build 4 widened the zone to the account's own cap (0.0180, where the hold
    // can be fed at all) AND raised the multiplier to 5 in one move: the flag's
    // break then came EARLIER (8.235 against build 3's 8.245) and the afterfall
    // froze - the stiff pair acting from 8.190 kicks the body's rotation before
    // the object's own frames. Build 5 keeps this zone and the multiplier of 5,
    // so the two moves are priced apart.
    public const float SupportHoldStiffClearM = 0.0190f;
    public const float SupportHoldBandDeg = 24f;
    public const float SupportHoldCeilM = 0.050f;
    public const float SupportHoldReleasePerFrame = 0.94f;
    public const float StanceHoldDeliverP = 7f;

    // WALK CYCLE 28: the afterfall's own crawl (DriveAfterfallCrawl). The deltas
    // are ABOUT HALF the base row's own measured crawl between 13.3 and 15.0 s
    // (the left hip folds 7 -> -34 deg, the knee 23 -> 42, the ankle -6.5 -> +31
    // while the pelvis rises 12 cm and the CoM starts to advance); the ramp and
    // the rock are slow enough to be the body's own pace, so the legs never
    // punch. The round's own first build is the reason for the half: with the
    // full deltas the law drove the body OFF the slab's own down condition (the
    // pelvis rose 0.85 m, the palms unloaded - contact fraction 0.005 against the
    // base row's 0.80 - and ArmDrive's presses never fired: 0 strokes), and the
    // CoM paid for it (the row's own max reached at 9.00 s, travel -0.24 m). The
    // crawl's object is only to keep the body ALIVE for the presses to find
    // purchase in - never to lift its weight off them.
    public const float AfterfallRampSeconds = 3f;
    public const float AfterfallRockSeconds = 6f;
    public const float AfterfallHipTuckDeg = 18f;
    public const float AfterfallKneeTuckDeg = 16f;
    public const float AfterfallAnkleTuckDeg = 12f;

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
        return Blend(phase, landingShare,
                     SwingFold.stance.knee, FootPlace.stance.knee, PlantLoad.stance.knee);
    }

    // WALK CYCLE 64: THE ASK-SIDE TIER - the same clock with the drawn stance
    // leg's knee keys SUBSTITUTED (SwingFold / FootPlace / PlantLoad; the three
    // values a genome's g[13..15] or the matching switches move). At the drawn
    // values the two calls run the same floats through the same Mix chain, so
    // every pre-c64 row stays bit-identical by construction.
    public static Pose Blend(float phase, float landingShare,
                             float foldKneeDeg, float placeKneeDeg, float loadKneeDeg)
    {
        float land = Mathf.Clamp(landingShare, FoldKey + 0.05f, 0.98f);
        Pose fold = SwingFold;   fold.stance.knee = foldKneeDeg;
        Pose place = FootPlace;  place.stance.knee = placeKneeDeg;
        Pose load = PlantLoad;   load.stance.knee = loadKneeDeg;
        if (phase <= LaunchKey)
            return Mix(Stance, Launch, phase / LaunchKey);
        if (phase <= FoldKey)
            return Mix(Launch, fold, (phase - LaunchKey) / (FoldKey - LaunchKey));
        if (phase <= land)
            return Mix(fold, place, (phase - FoldKey) / (land - FoldKey));
        return Mix(place, load, Mathf.Clamp01((phase - land) / (1f - land)));
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
        Pose p = Blend(x.swingPhase, x.landingShare,
                       x.stanceFoldKneeDeg, x.stancePlaceKneeDeg, x.stanceLoadKneeDeg);
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
            // WALK CYCLE 20: the fold hold runs to the sole-putting ask's own
            // onset instead of expiring at the landing share (the decay, :1886).
            // The hold is floored on its hold side, so the swing knee stays in
            // the fold band until x.supportNeed takes the descent over (:1817)
            // instead of extending the sole off the floor while still unloaded.
            // 0.28 carries the frame clock's own emitted knee at its touch
            // (35.1 deg) onto the step clock's arrival (measured +7.9 deg here).
            float weight = Mathf.Max(FoldWeight(x.swingPhase, land),
                x.swingPhase > FoldKey ? 0.28f : 0f);
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
        // WALK CYCLE 75/76: THE DESCENT ARITHMETIC (the c68 §3 fix), gated. The bias
        // aims the ankle at the pivot; section 1 above may have rewritten the swing
        // knee (the descent) between the drawn pose and the touch, and the DRAWN
        // knee's ankle offset (t.nominalAnkleXM) understates the reach the
        // straightened leg leaves - the composed sole then lands past the ask by the
        // throw. ON: the same two-link arithmetic with the knee the descent leaves.
        // OFF (the default): the landed expression bit for bit.
        float nominalAnkleX = t.nominalAnkleXM;
        if (x.placeKneeLive)
        {
            float shinWorldLive = thighWorld - swingKnee;
            nominalAnkleX = thigh * Mathf.Sin(thighRad)
                + shin * Mathf.Sin(shinWorldLive * Mathf.Deg2Rad);
        }
        t.placementErrorM = x.footPlaceX - (x.pelvisX + nominalAnkleX);
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
        // WALK CYCLE 37: THE SWING FOOT'S OWN ANKLE (the named follow-up, landed).
        // The landed line emitted the pose's own drawn ankle; the ask now gains the
        // gated, ramped carry. At 0 the test is false and the landed value stands
        // bit for bit; ClampLeg below still holds the rig's own stops.
        if (x.swingAnkleDeg != 0f)
            t.swingLeg.ankle += x.swingAnkleDeg;
        t.stanceLeg = p.stance;
        t.stanceLeg.hip += t.trunkCorrDeg;
        t.stanceLeg.knee += t.heightTrimDeg;
        // WALK CYCLE 28: the support's own sole clearance, held. Exactly what cycle 27
        // priced on this row (its hunk, re-applied - the round reverted it, the key
        // itself was never the fault: the delivery was). The hold is a lowering of
        // the support's sole, read about the support's own joints: the ankle press
        // moves the sole by about the foot's own front lever, and the drawn stance
        // pose folds the knee ~22 deg, which is what lifts the sole - so the same
        // lowering is asked of the knee about its own lever. Both are bounded by the
        // band and clamped by the rig's stops below.
        if (x.stanceSoleHoldM > 0f)
        {
            float ankleLeverM = Mathf.Max(0.02f, x.stanceSoleLeverM);
            t.stanceLeg.ankle += Mathf.Clamp(
                x.stanceSoleHoldM / ankleLeverM * Mathf.Rad2Deg, 0f, SupportHoldBandDeg);
            float stanceThighRad = -p.stance.hip * Mathf.Deg2Rad;
            float stanceKneeRad = p.stance.knee * Mathf.Deg2Rad;
            float kneeLeverM = Mathf.Max(0.02f,
                x.shinM * Mathf.Abs(Mathf.Sin(stanceThighRad - stanceKneeRad)));
            t.stanceLeg.knee -= Mathf.Clamp(
                x.stanceSoleHoldM / kneeLeverM * Mathf.Rad2Deg, 0f, SupportHoldBandDeg);
        }
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
