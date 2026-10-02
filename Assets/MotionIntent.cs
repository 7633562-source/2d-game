using UnityEngine;

// Intent layer: what the human wants, not how the body gets it.
// The component stores numbers only and has no logic. Anyone may fill them:
// PlayerInputSource from the keyboard, the windowless stand in code,
// later — a creature AI.
//
// Input is deliberately not read by the balancer. First, the windowless
// stand has no keyboard, and the run would need conditional compilation.
// Second, with a hundred creatures only one — the one being played —
// should poll the keyboard, not each in its own FixedUpdate.
//
// This holds the target, not a smoothed value: keyboard crouch is only
// 0 or 1. The control layer does the pose blend; otherwise the stand
// that sets crouch = 1 directly would get an instant squat and behave
// differently from the same key press.
public class MotionIntent : MonoBehaviour
{
    [Header("Move direction: -1 back, +1 forward, 0 stand")]
    [Range(-1f, 1f)]
    public float moveX;

    [Header("Crouch target: 0 standing, 1 squatting")]
    [Range(0f, 1f)]
    public float crouch;

    // SQUAT TASK (cycle 84): how many stand->squat->stand reps the squat task machine
    // should run while crouch > 0. A COUNT, not a switch: 0 is every path that ever
    // existed (the S key, the walk, -squatRise) and the controller's crouch line is
    // then the one it always ran. 1..N hands the level target to SquatStateMachine,
    // which sequences the reps and counts them; the trial still writes a TARGET only.
    [Header("Squat task: reps to run while crouch > 0 (0 = plain hold)")]
    public int squatReps;

    // SQUAT TASK (cycle 94): a take that will run the squat task keeps the stand's
    // flat-sole toe hold OUT of the whole run. Measured: the hold's own posture at
    // t = 0 moves the descent's first second (comX creeps +13 mm against the anchor's
    // +4 mm, the left sole unloads) and the landed squat cannot absorb it - with the
    // hold armed in the pre-stand the take dies (c94h_land: reps 0, Fallen 9.81), with
    // it never armed the same take is the landed anchor bit for bit (52a8c77c, 3/7).
    // 0 = the hold behaves exactly as in cycle 93 (every other path, the editor too).
    [Header("Squat task: 1 = keep the stand's flat-sole toe hold out of this run")]
    public int toeHoldBlock;

    // SQUAT TASK (cycle 85): the two doses the round prices.
    //   squatPace 1 = the level rate is clamped by the load the soles carry (the ceiling
    //   is crouchRatePerSecond); 0 = the c84 clock. Read only while squatReps > 0.
    //   squatBottomHold > 0 overrides the bottom dwell in seconds (BalanceController's
    //   squatBottomHoldSeconds); 0 = the controller's own value.
    // DEFAULT IS THE c84 LAW: cycle 85 measured the load term to work exactly as designed
    // (the measured rate follows the load term, r = 0.72 / 0.95) and to reach the full
    // settled bottom - at the cost of the three-rep take (its neighbours at dwell 0.6 and
    // 0.8 topple too), so the pacing is priced, not landed. A take that wants it asks for
    // it by flag. 0 = off on every path that does not arm a squat, the editor included.
    [Header("Squat task: 1 = load-driven pacing, 0 = the plain clock (default)")]
    public int squatPace;
    [Header("Squat task: bottom dwell override, s (0 = the controller's own)")]
    public float squatBottomHold;

    // SQUAT TASK (cycle 86): the RETURN's own term. Cycle 85 proved the depth finish
    // reachable and named the blocker: after a fully settled bottom the release is a pose
    // spring let go, the body leaves the ground (the load meter reads 0 for whole frames,
    // the landing spikes at 2.2 W, the knees flex back to 24 deg), the stand gate never
    // re-holds its 0.30 s, the machine refuses the next rep and the take topples.
    //   squatReleaseScale > 0 scales the RISE's release rate (0 = the plain clock);
    //   squatReleaseLoad 1 gates that rise by the load the soles carry - the fold law's
    //   own shape mirrored, so the pose unfolds only while it is carrying the body.
    // Both act only while the muscle task is ARMED and only on the rise; every other path
    // (the stand, the walk, the editor) multiplies by exactly 1f.
    [Header("Squat task: rise release-rate scale (0 = the plain clock)")]
    public float squatReleaseScale;
    [Header("Squat task: 1 = the rise follows the load (0 = the plain clock)")]
    public int squatReleaseLoad;
    // CYCLE 85b: the gate's two belts. squatReleaseSink (m/s, 0 = off) takes the rise off
    // the load gate while the pelvis is sinking faster than that - a body falling onto the
    // folded leg must be caught by the pose giving, not held by it. squatReleaseFloor
    // (0..1, 0 = none) is the floor under the gate: whatever the load reads, the rise
    // creeps at least that fast.
    [Header("Squat task: rise sink bypass, m/s (0 = off)")]
    public float squatReleaseSink;
    [Header("Squat task: floor under the rise gate, 0..1 (0 = none)")]
    public float squatReleaseFloor;

    // SQUAT TASK (cycle 87): the BOTTOM's own term. Cycle 85b measured the failure it
    // answers: under the settled bottom the body crept forward for exactly as long as the
    // dwell clock ran (com -0.001 -> -0.073 m in 0.9 s, then -0.26, -0.41, feet off,
    // Fallen). The dwell becomes a FLOOR; with this term armed the exit also reads the
    // body's own "the pose has admitted the load at the bottom" - the descent arrested,
    // the depth held, the soles carrying, both feet down, no fall sequence - held for
    // squatBottomSettle seconds (a transition guard, not a timer).
    //   squatBottomGate 1 arms it; the other two 0 = the controller's own values.
    // DEFAULT IS THE c84 LAW: every path that does not ask by flag keeps the dwell timer.
    [Header("Squat task: 1 = the bottom exits on the body's own settle (0 = the timer)")]
    public int squatBottomGate;
    [Header("Squat task: bottom settle guard, s (0 = the controller's own)")]
    public float squatBottomSettle;
    [Header("Squat task: bottom arrest threshold, m/s (0 = the controller's own)")]
    public float squatBottomMps;

    // CYCLE 90 - THE RISE'S CoM ADMITTANCE (the round's named target: the return after
    // a fully settled bottom). The measured failure: the hold seats the CoM 5-7 cm BEHIND
    // the feet and the extension then feeds the backward run instead of ending it (com
    // -0.06 -> -0.40, ankles on the stop, the body topples backward at 20.4 s). While the
    // CoM is behind squatRiseCom the rise's rate is held at squatRiseComScale (0 = a full
    // hold) for at most squatRiseComMax seconds per incident; a sinking body is never
    // held. squatRiseCom 0 = off: every earlier path keeps the clock.
    [Header("Squat rise: CoM hold band, m (0 = off)")]
    public float squatRiseCom;
    [Header("Squat rise: rate while the CoM is behind (0 = hold)")]
    public float squatRiseComScale;
    [Header("Squat rise: max hold per incident, s (0 = the controller's own)")]
    public float squatRiseComMax;

    // CYCLE 90 - THE ARM RAISE (the owner's own gesture, 28.09): the RIGHT arm lifts in
    // four phases (elbow bend -> straight diagonal up -> hold), the left arm stays down,
    // the feet stay planted, the torso stays quiet - the vestibular core must carry the
    // stance through it. A request only: BalanceController computes the two targets on
    // its own clock (armRaiseTime), ArmDrive - the arms' writer - emits them.
    // armRaise 0 = off on every other path.
    [Header("Arm raise: 1 = the right-arm gesture (0 = off)")]
    public int armRaise;
    [Header("Arm raise: first raise at, s")]
    public float armRaiseTime;
    [Header("Arm raise: shoulder target, deg (negative = up-forward)")]
    public float armRaiseShoulder;
    [Header("Arm raise: elbow target, deg (the straight diagonal)")]
    public float armRaiseElbow;
    [Header("Arm raise: mid-phase elbow bend, deg (forearm up)")]
    public float armRaiseBend;
    [Header("Arm raise: hold at the top, s")]
    public float armRaiseHold;
    [Header("Arm raise: reps")]
    public int armRaiseReps;
    [Header("Arm raise: period between raises, s")]
    public float armRaisePeriod;

    // Which leg carries the weight. 0 — both (former symmetry), −1 — stance
    // on the left (swing on the right), +1 — stance on the right (swing on
    // the left). This is intent, not a smoothed level: the ramp-up is done
    // by BalanceController, otherwise the stand with an instant standLeg
    // would outrun a held key.
    [Header("Support: 0 both feet, -1 left, +1 right")]
    [Range(-1f, 1f)]
    public float standLeg;

    // March in place: the stand writes standLeg left/right on a clock.
    // The balancer reads this so the 90° swing pose and the skipped
    // pelvis tilt do not leak into a quiet one-leg hold. Not a walk.
    [Header("March in place: 0 off, 1 on")]
    [Range(0f, 1f)]
    public float march;

    // Torso lean while on two feet: +1 forward (+X), −1 backward.
    // Not to be confused with moveX (walking): ←/A and →/D write lean.
    // Walk is W / ↑ / Space or the on-screen Walk button.
    [Header("Torso lean: -1 back, +1 forward, 0 vertical")]
    [Range(-1f, 1f)]
    public float lean;

    // Run: 1 while walk is held together with Shift. Shorter cadence than walk.
    [Header("Run: 0 walk/stand, 1 run")]
    [Range(0f, 1f)]
    public float run;

    // Arm strike (the punch). A level, like crouch: the key (F) or the trial
    // writes the request, ArmDrive - the arms' own writer - runs the stroke and
    // owns the striking arm for its length. The strike is a LAYER above the
    // always-on base: this field carries the WISH only, the balance layer keeps
    // the legs, the pelvis and the equilibrium while the arm moves.
    // 0 idle, 1 strike requested (rising edge fires; held re-fires on the
    // interval). The side and the timing are ArmDrive's own knobs.
    [Header("Arm strike: 0 idle, 1 punch")]
    [Range(0f, 1f)]
    public float punch;

    // Leg strike (the kick). Same channel class as the punch above: a level the
    // key (G) or the trial writes, and LegStrikeDrive - the kicking leg's own
    // writer - runs the stroke (lift -> chamber -> extension -> return on hip/
    // knee/ankle) and owns that leg for the stroke's length. The strike is a
    // LAYER above the always-on base: this field carries the WISH only, the
    // support leg, the pelvis law and the equilibrium keep their own writers
    // throughout (that the support leg carries the body is the point).
    // 0 idle, 1 kick requested (rising edge fires; held re-fires on the
    // interval). The side and the timing are LegStrikeDrive's own knobs.
    [Header("Leg strike: 0 idle, 1 kick")]
    [Range(0f, 1f)]
    public float kick;
}
