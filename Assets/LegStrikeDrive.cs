using UnityEngine;

// KICK CYCLE k1 — THE LEG STRIKE (удар ногой). Ordered by Yury 25.09:
// «Добавь еще два новых движения. Удар рукой, удар ногой.» The arm's strike is
// ArmDrive's (the punch line, its own stream); THIS file is the leg's.
//
// KICK CYCLE k2 (25.09.2026) — THE SUPPORT-SIDE SHIFT: this component is now
// the strike's SHAPE SOURCE and nothing else. It owns the stroke's clock, its
// phase keys, the base latch and the rate cap, and PUBLISHES the step's three
// angle targets plus the support-side shift's own level into the balance hub
// (BalanceController.kickStrokeActive / kickHipTargetDeg / kickKneeTargetDeg /
// kickAnkleTargetDeg / kickShiftLevel). The POSE PATH (PoseDriveController's
// TickStand, the same path the crouch and the leans are layers of) reads them
// and drives the kicking leg through its own DriveOneLeg - ONE writer per
// joint for the whole body, the kick leg's targets handed to the pose path's
// own driver (the k1 round's cross-component handoff at the stroke's end is
// gone), and the strike becomes a layer of the pose path exactly the way the
// crouch's knee is.
//
// The strike is a LAYER ABOVE THE ALWAYS-ON BASE, and the property that makes
// it one is structural, not a promise: it never touches the trunk, the SUPPORT
// leg's targets, the pelvis law or the pose library - the support leg carrying
// the body is the point of the exercise, not a side effect. This component
// writes NO muscle and NO ceiling at all (k1's own PD, slew and torque-scale
// knobs are retired with the handoff; the pose path's DriveOneLeg delivers the
// strike on the base's own gains, and the k1 rows' default torque scale was a
// no-op anyway).
//
// It is ticked at execution order -10, BEFORE BalanceController's 0, so the
// step the pose path reads is this step's own - the defect that killed the
// crouch/leans (BalanceController's `poseDrive.Tick(comOffset); return;`
// skipping every writer below it) has no branch to hide in on this path.
// With the kick off (no request, no stroke) the published block is exactly
// false / 0f (R2) and the pose path takes its own arithmetic unchanged.
//
// THE STROKE (kick k1's shape, unchanged): lift -> chamber -> extension ->
// return, on the kicking leg's hip/knee/ankle, all as ANGLE targets for the
// pose path's stable PD. Sign conventions of this rig: hip flexion is MINUS
// (thigh forward), knee flexion is PLUS, ankle PLUS is toe down / heel rise.
// [Leg angles: Human.cs hinge limits hip -90..+45, knee 0..120; ankle
// asymmetric 65/22 N.m.]
// The request comes from MotionIntent.kick (the G key in Play, the trial's
// -kick flag, Tools/force-kick.flag): a rising edge fires, a held request
// re-fires every kickIntervalSeconds.
//
// The base angles are LATCHED from the measured joints on the fire step and the
// stroke returns to them, so the leg is handed back to the base exactly where
// it was picked up from. The rate cap limits the emitted target per step (the
// leg may be mid-motion when the request lands), never the phase clock.
//
// THE SUPPORT-SIDE SHIFT'S OWN CLOCK (kick k2): the shift level is 1 while a
// stroke is in flight OR the request is held - the repeat's recovery needs the
// shift in the gaps between strokes, not only inside them - and eases to 0 over
// kickShiftReleaseSeconds once both drop (the lean layer's own release
// discipline: the return is a gesture's risk, so it rides a clock, while the
// way in needs none - the CoM signal it scales is continuous and starts at 0
// on a quiet stand).
// KICK CYCLE k8 (26.09.2026) — THE STROKE'S OWN HEAD: the base is measured
// against the stand's own posture (the quiet stance the request found, frozen
// for the request's window) and `-kickHeadShare` says how much of the head's
// own offset from it rides into the stroke's frame. 1 = the k1..k7 arithmetic
// (the measured stance), 0 = the posture's own angles; the emitted demand stays
// continuous with the leg either way.
[DefaultExecutionOrder(-10)]
public class LegStrikeDrive : MonoBehaviour
{
    // ─── the stroke's own shape (kick k1) ───────────────────────────────────
    public float kickLiftSeconds = 0.24f;
    public float kickChamberSeconds = 0.16f;
    public float kickExtendSeconds = 0.16f;
    public float kickReturnSeconds = 0.45f;
    // Re-fire clock while the request is held (the stroke is 1.01 s, so a held
    // key kicks about once every two seconds).
    public float kickIntervalSeconds = 2.0f;
    // Which leg strikes: +1 the RIGHT, -1 the left. One leg per stroke; the
    // support side keeps whatever the base was already asking it.
    public float kickSide = 1f;
    // Hip (thigh): the lift raises the knee to kickHipLift (minus = forward),
    // the chamber holds a deeper fold, the hit drives the thigh through.
    public float kickHipLiftDeg = -55f;
    public float kickHipChamberDeg = -60f;
    public float kickHipHitDeg = -50f;
    // Knee: folds through the lift and the chamber (heel toward the pelvis),
    // then opens at the hit - the shin is the striking link.
    public float kickKneeLiftDeg = 70f;
    public float kickKneeChamberDeg = 100f;
    public float kickKneeHitDeg = 15f;
    // Ankle: dorsiflexed while the foot is off the floor (toe up), driven
    // through the hit (toe down).
    public float kickAnkleLiftDeg = -14f;
    public float kickAnkleChamberDeg = -20f;
    public float kickAnkleHitDeg = 18f;
    // Rate cap on the emitted targets, degrees per second. 900 leaves the
    // 0.16 s extension (about 530 deg/s on the knee) free.
    public float kickSlewDegPerSecond = 900f;
    // The support-side shift's OUT clock: seconds for the level to ease 1 -> 0
    // after both the stroke and the request are gone. The way in is a step (the
    // law it scales is continuous); the way out is this slow.
    public float kickShiftReleaseSeconds = 1.0f;
    // ─── k4 THE WIND-UP: the load gate at the stroke's head ────────────────
    // The lift may not start while the KICKING foot's measured load is above
    // this share of the two soles' total. The k3 record's own sentence: the
    // settled stance leaves 83/17 of the weight ON the kicking foot, so the
    // next lift jerks a loaded leg off the floor and the transfer is violent
    // (fzL 184 -> 1502 N in 0.2 s, the support ankle straight to its -65 N*m
    // ceiling). The wait is BOUNDED - after kickLoadGateTimeout seconds the
    // stroke fires anyway, so a request can never hang.
    public float kickLoadGateShare = 0.6f;
    public float kickLoadGateTimeout = 1.5f;

    // ─── k8 THE STROKE'S OWN HEAD: the base against the stand's own posture ──
    // THE k7 RECORD'S OWN LEVER (§10, item 1). The read: strokes 1-3 fire from
    // comX 0.014-0.022 m and the 4th from 0.096 m (the window 3 -> 4's own
    // creep), and every stroke after the first is BORN from a stance the stand
    // itself never asked for - the row's heads read hip -8.4..-9.1 deg and
    // ankle +3.8..+6.7 deg against the quiet stance's own +4.1 / -6.6 (the same
    // wall kills the LEFT at its 2nd stroke). The base is latched from the
    // MEASURED joints, so the creep rides into the stroke's own frame: the lift
    // starts from the crept stance and the return hands the leg back to it.
    // kickHeadShare says how much of that offset rides in: 1 = the base is the
    // measured stance (the k1..k7 arithmetic, bit for bit - the flag's default),
    // 0 = the base is the stand's OWN POSTURE (the quiet stance the request
    // found, frozen for the request's window the way the wind-up's own burst
    // frame is), between = that share of the offset. The emitted demand stays
    // continuous with the leg either way (the rate cap carries the first steps),
    // and the posture is a read of the kicking leg's own joints - nothing else
    // in the pose path or the balance layer moves.
    public float kickHeadShare = 1f;

    // ─── k12 THE HEAD'S OWN PHASE (the strike's fire state) ─────────────────
    // THE k11 REPORT'S §9, READ BEFORE ANY KNOB ON THE ROWS ON DISK
    // (Tools c-kickk12-read.py, per stroke and per row). The READ: the FIRST
    // stroke's own numbers - the fire, the flight, the return - are IDENTICAL
    // on every row of the burst family (fire 5.015: comX +0.043, comVelX
    // -0.005, shareR 0.578, supAnk -31.17; return 5.94: comVelX -0.099), so
    // the k3/k4 plant and the k5 one-writer rule are a SHARED initial
    // condition, not the differentiator; the rows part on the W1 window's own
    // rising edge - the first frame after the stroke's end, t 6.035, 48 of 230
    // columns at the 4th decimal - in proportion to the term's level alone
    // (0.01 vs 0.00714 and even 0.0071 vs 0.00714 part on the same frame).
    // What the 2nd stroke's outcome READS is the CoM's own velocity at the
    // fire: comVelX -0.067 (the delivery) holds 40.0049 s, -0.035 survives the
    // transfer and sinks later, -0.026 and up (+0.005 / +0.009 / +0.060) launch
    // the stand inside the window - the CoM is running +0.17..+0.28 m/s within
    // 0.3 s of the fire. The stroke's own swing hands the CoM a fixed forward
    // impulse; a stand that is already moving forward has no braking budget
    // left for it. The load share does NOT separate the rows (0.160 / 0.406 /
    // 0.516 / 0.638 at the fire, the survivor in the middle).
    // kickHeadVelGate is that criterion, the k4 gate's own sibling: while the
    // fire is due, the stroke WAITS - the same bounded clock
    // (kickLoadGateTimeout) and the same counters as the load gate - until the
    // CoM is moving BACKWARD at least this fast (comVelX <= -kickHeadVelGate).
    // 0 = OFF, the k1..k11 arithmetic bit for bit. It reads the stand's own
    // state and writes nothing to any joint.
    public float kickHeadVelGate = 0f;

    // ─── k13 THE FIRST FIRE'S OWN ARM ───────────────────────────────────────
    // THE k12 REPORT'S §6, read before the ladder: the quiet stand the burst is
    // born from drifts BACKWARD at only -0.002..-0.009 m/s (the delivery's own
    // 4.2..5.0 band, and the 1st fire itself reads -0.005), and the moment the
    // request is up the term's window is up with it - the loop's CoM channels
    // scaled to the level - so the stand the gate waits on can only run
    // FORWARD (measured on the k12 0.04 row: 5.0 comX +0.045 / velX +0.033 ->
    // 6.4 +0.586 / +1.597). A rung sized for the LATER fires - the knife edge
    // the 2nd stroke's own state reads (+0.001..+0.06 launches, -0.035 and
    // below hold) - therefore can never open on the burst's FIRST fire: the
    // gate would spend its whole 1.5 s clock and fire the stroke onto a thrown
    // body. kickHeadVelFirst is the rung the FIRST stroke uses (the stand's
    // own quiet phase, before any stroke has fired this run); from the second
    // fire on, kickHeadVelGate decides. -1 (default) = no arm of its own: the
    // k12 arithmetic, character for character. 0 = the first fire is exempt
    // (the gate never holds it). The arm reads the same estimator and writes
    // nothing to any joint.
    public float kickHeadVelFirst = -1f;

    // ─── read-back (the round's own reads; no recorder column) ──────────────
    public float KickRequest { get; private set; }
    public int KickPhase { get; private set; }            // 0 idle 1 lift 2 chamber 3 extension 4 return
    public int KickStrikes { get; private set; }          // strokes started this run
    public float KickAge { get; private set; }            // seconds since the stroke in force started
    public float KickTargetHipDeg { get; private set; }
    public float KickTargetKneeDeg { get; private set; }
    public float KickTargetAnkleDeg { get; private set; }
    public float KickSideIndex { get; private set; }
    // Run-scoped maxima: the deepest hip flexion and the largest knee opening
    // the emitted targets reached (degrees), and the fastest the hip target
    // moved (deg/s) - the strike's own rate, read against the walk swing's
    // 330 deg/s cap so "a kick, not a slow lift" is a number.
    public float KickPeakHipDeg { get; private set; }
    public float KickPeakKneeOpenDeg { get; private set; }
    public float KickPeakTargetRateDegPerSec { get; private set; }
    public float KickLastStrokeSeconds { get; private set; }
    public float KickShiftLevel { get; private set; }
    // k3: the support hip's latched plant angle (degrees, the rig's own hip
    // convention), read back for the round's own reads and published to the
    // balance hub, where the pose path's support hold takes it.
    public float KickSupportHipDeg { get; private set; }
    // k4: the load gate's own read - the longest deferral this run (s), the
    // deferred steps, and the kicking foot's share read on the fire step.
    public float KickGateWaitSeconds { get; private set; }
    public int KickGateDeferrals { get; private set; }
    public float KickHeadLoadShare { get; private set; }
    // k8: the stroke's head against the stand's own posture - the offsets the
    // fire step read (measured minus posture, the rig's own sign) and the base
    // the stroke actually used. Recorded nowhere; published for the round's
    // reads and the editor's own inspection.
    public float KickHeadOffsetHipDeg { get; private set; }
    public float KickHeadOffsetKneeDeg { get; private set; }
    public float KickHeadOffsetAnkleDeg { get; private set; }
    public float KickHeadBaseHipDeg { get; private set; }
    public float KickHeadBaseKneeDeg { get; private set; }
    public float KickHeadBaseAnkleDeg { get; private set; }
    public float KickHeadPostureHipDeg { get; private set; }
    public float KickHeadPostureKneeDeg { get; private set; }
    public float KickHeadPostureAnkleDeg { get; private set; }
    // k12: the CoM's own velocity on the fire step (m/s, the gate's own read).
    public float KickHeadVelMps { get; private set; }

    private BalanceController balance;
    private BodyStateEstimator bodyState;
    private MotionIntent intent;
    private StepPhaseDriver stepDriver;

    private HingeJoint2D hipJoint;
    private HingeJoint2D kneeJoint;
    private HingeJoint2D ankleJoint;
    // k3: the SUPPORT side's hip - the plant's own latch.
    private HingeJoint2D supportHipJoint;

    private bool bound;
    private float kickClock = -1f;
    // The stroke's own clock is closed the moment the stroke ends (the leg is
    // handed back), so the PACE between strokes needs a clock of its own: from
    // the fire, count to max(stroke, interval) - the punch's own cadence rule.
    private float kickSinceFire = -1f;
    private float kickBaseHip;
    private float kickBaseKnee;
    private float kickBaseAnkle;
    // k3: the support hip's angle on the fire step (published; the pose path's
    // hold reads it).
    private float kickBaseSupportHip;
    // k4: the gate's own wait clock (seconds the fire has been held) .
    private float kickGateWait;
    // k14: the due-and-held state of this step - the pace clock has armed a
    // stroke, the request is up, and the gate is still deferring it. Published
    // to the balance hub for the pose path's window level (-kickWindDue); it is
    // false on every step that is not a wait, so the flag's own default leaves
    // every landed row alone (R2).
    private bool kickFireHeld;
    // k8: the stand's own posture (the kicking leg's three angles as the quiet
    // stance held them when the request rose) and the flag that freezes it for
    // the request's own window.
    private float kickHeadPostureHip;
    private float kickHeadPostureKnee;
    private float kickHeadPostureAnkle;
    private bool kickHeadPostureHeld;
    private float kickEmitHip;
    private float kickEmitKnee;
    private float kickEmitAnkle;
    private int kickSideIndex = 1;

    // ─── command line (the same reader style as ArmDrive/PoseDriveController;
    // the trial's own schedule flags -kick / -kickTime / -kickHold live in
    // HeadlessTrial; these tune the stroke itself) ───
    private const string ArgKickLift = "-kickLift";
    private const string ArgKickChamber = "-kickChamber";
    private const string ArgKickExtend = "-kickExtend";
    private const string ArgKickReturn = "-kickReturn";
    private const string ArgKickInterval = "-kickInterval";
    private const string ArgKickSide = "-kickSide";
    private const string ArgKickHipLift = "-kickHipLift";
    private const string ArgKickKneeLift = "-kickKneeLift";
    private const string ArgKickHipChamber = "-kickHipChamber";
    private const string ArgKickKneeChamber = "-kickKneeChamber";
    private const string ArgKickHipHit = "-kickHipHit";
    private const string ArgKickKneeHit = "-kickKneeHit";
    private const string ArgKickAnkleLift = "-kickAnkleLift";
    private const string ArgKickAnkleChamber = "-kickAnkleChamber";
    private const string ArgKickAnkleHit = "-kickAnkleHit";
    private const string ArgKickSlew = "-kickSlew";
    private const string ArgKickShiftRelease = "-kickShiftRelease";
    // k4: the load gate at the stroke's head (share of the two soles' load the
    // KICKING foot may carry at the fire; the wait is bounded by the timeout).
    private const string ArgKickLoadShare = "-kickLoadShare";
    private const string ArgKickLoadTimeout = "-kickLoadTimeout";
    // k8: the stroke's head against the stand's own posture (the share of the
    // measured head's offset from the quiet stance that rides into the stroke's
    // own frame; 1 = the k1..k7 arithmetic).
    private const string ArgKickHeadShare = "-kickHeadShare";
    // k12: the head's own phase - the fire waits (bounded) until the CoM is
    // moving backward at least this fast (m/s; 0 = off).
    private const string ArgKickHeadVel = "-kickHeadVel";
    // k13: the first fire's own arm - the rung the FIRST stroke uses (the
    // quiet stand's phase); -1 = none, the k12 arithmetic.
    private const string ArgKickHeadVelFirst = "-kickHeadVelFirst";

    private void Awake()
    {
        ParseArgs();
    }

    public void Bind()
    {
        if (bound) return;
        bound = true;

        balance = GetComponent<BalanceController>();
        intent = GetComponent<MotionIntent>();

        // The JOINTS of the kicking leg are named here for the stroke's own
        // latch (the measured angles the stroke is born from); the muscles are
        // the pose path's own writers. kickSide resolves the side exactly the
        // way ArmDrive resolves punchSide.
        hipJoint = null;
        kickSideIndex = kickSide >= 0f ? 1 : 0;
        string sideName = kickSideIndex == 1 ? "Right" : "Left";
        Transform hipT = transform.Find(sideName + "LegThigh");
        Transform kneeT = transform.Find(sideName + "LegShin");
        Transform ankleT = transform.Find(sideName + "LegFoot");
        if (hipT != null) hipJoint = hipT.GetComponent<HingeJoint2D>();
        if (kneeT != null) kneeJoint = kneeT.GetComponent<HingeJoint2D>();
        if (ankleT != null) ankleJoint = ankleT.GetComponent<HingeJoint2D>();

        // KICK CYCLE k3 - THE SUPPORT'S PLANT: the OTHER side's hip is read
        // here for the latch alone. The pose path holds it at the angle it had
        // on the fire step through the whole episode (the strike + the release),
        // so the one-leg second has a standing leg of its own instead of a free
        // hip running to its extension stop.
        string supName = kickSideIndex == 1 ? "Left" : "Right";
        Transform supHipT = transform.Find(supName + "LegThigh");
        supportHipJoint = supHipT != null ? supHipT.GetComponent<HingeJoint2D>() : null;
    }

    void FixedUpdate()
    {
        if (!bound) Bind();
        if (hipJoint == null || kneeJoint == null || ankleJoint == null)
        {
            PublishOff();
            return;
        }

        float dt = Time.fixedDeltaTime;
        if (intent == null) intent = GetComponent<MotionIntent>();
        float request = intent != null ? Mathf.Clamp01(intent.kick) : 0f;
        KickRequest = request;
        if (stepDriver == null) stepDriver = GetComponent<StepPhaseDriver>();

        // A body that is not standing does not kick: the fall is the base's own
        // state (Falling/Fallen/GettingUp - the same gate the punch reads
        // through the vestibular tilt), and a kick while the walk clock owns the
        // gait is out of this round's scope. Either way the stroke ends and the
        // base keeps its own writers.
        bool walking = stepDriver != null && stepDriver.walkActive;
        bool upright = balance == null
            || balance.locomotionState == BalanceController.LocomotionState.Standing;
        if (!upright || walking)
        {
            if (kickClock >= 0f) EndKick();
            // k8: a stance that is not the quiet stand has no posture to hold.
            kickHeadPostureHeld = false;
            // k14: a body that is not standing is not waiting on a fire either.
            kickFireHeld = false;
            TickShiftClock(request, dt, false);
            PublishOff();
            return;
        }

        if (kickClock >= 0f) kickClock += dt;
        if (kickSinceFire >= 0f) kickSinceFire += dt;

        // ─── k8 THE STAND'S OWN POSTURE ─────────────────────────────────────
        // Read live while the request is down (the kicking leg's own joints, a
        // read and never a write) and FROZEN on the step the request rises - the
        // stance the burst is born into, held for the request's whole window, so
        // every stroke of a burst is measured against the same stand.
        if (request <= 0.5f)
        {
            kickHeadPostureHeld = false;
            kickHeadPostureHip = hipJoint.jointAngle;
            kickHeadPostureKnee = kneeJoint.jointAngle;
            kickHeadPostureAnkle = ankleJoint.jointAngle;
        }
        else if (!kickHeadPostureHeld && kickClock < 0f)
        {
            kickHeadPostureHeld = true;
        }

        // Fire: the first step the request is up fires (the rising edge), and a
        // request still held re-fires once the stroke and the interval are
        // spent. The pace clock runs from the FIRE, not from the stroke's end
        // (the stroke's own clock closes the moment the stroke ends).
        float strokeSeconds = KickStrokeSeconds();
        bool canFire = kickSinceFire < 0f
            || kickSinceFire >= Mathf.Max(strokeSeconds, kickIntervalSeconds);
        // k4 THE WIND-UP (term 2): the load gate at the head. The fire waits -
        // at most kickLoadGateTimeout seconds - while the KICKING foot still
        // carries more than kickLoadGateShare of the two soles' load. The share
        // is re-read on every deferred step, so the stroke leaves the moment the
        // weight is off the leg it is about to lift; the timeout is what keeps a
        // request from hanging on a stance that never shifts.
        bool gateHeld = false;
        // k14: the state the round's own knob reads (the window's own level while
        // a fire is due and held). Every step starts not-held; only the gate's own
        // deferral below raises it, and the fire itself clears it.
        kickFireHeld = false;
        if (request > 0.5f && canFire && kickClock < 0f)
        {
            float share = KickFootLoadShare();
            bool loadHeld = kickLoadGateShare < 0.999f && share > kickLoadGateShare;
            // k12: the head's own phase - the same bounded wait, one criterion
            // (the CoM must be moving backward). Exactly off at the flag's 0.
            // k13: the rung in force - the first fire's own arm while no stroke
            // has started this run, the main rung after it. With the arm unset
            // (-1) this is kickHeadVelGate, character for character.
            float phaseRung = kickHeadVelGate;
            if (kickHeadVelFirst >= 0f && KickStrikes == 0) phaseRung = kickHeadVelFirst;
            bool phaseHeld = phaseRung > 0f && KickComVelX() > -phaseRung;
            if ((loadHeld || phaseHeld) && kickGateWait < kickLoadGateTimeout)
            {
                kickGateWait += dt;
                if (kickGateWait > KickGateWaitSeconds) KickGateWaitSeconds = kickGateWait;
                KickGateDeferrals++;
                gateHeld = true;
                kickFireHeld = true;
            }
            else
            {
                kickGateWait = 0f;
            }
        }
        if (request > 0.5f && canFire && !gateHeld)
        {
            if (kickClock < 0f)
            {
                KickHeadLoadShare = KickFootLoadShare();
                // k12: the head's own phase, read on the fire step (the round's
                // own read; the gate above wrote nothing).
                KickHeadVelMps = KickComVelX();
                // Latch the BASE pose: the step the stroke is born from, to
                // which the return phase gives the leg back.
                float headHip = hipJoint.jointAngle;
                float headKnee = kneeJoint.jointAngle;
                float headAnkle = ankleJoint.jointAngle;
                // k8: the head, measured against the stand's own posture. The
                // offsets are the read (this fire step minus the frozen quiet
                // stance); the share decides how much of them rides into the
                // stroke's own frame. 1 = the measured stance (the k1..k7
                // arithmetic, character for character), 0 = the posture's own
                // angles, between = the share of the offset.
                float headShare = Mathf.Clamp01(kickHeadShare);
                KickHeadPostureHipDeg = kickHeadPostureHip;
                KickHeadPostureKneeDeg = kickHeadPostureKnee;
                KickHeadPostureAnkleDeg = kickHeadPostureAnkle;
                KickHeadOffsetHipDeg = Mathf.DeltaAngle(kickHeadPostureHip, headHip);
                KickHeadOffsetKneeDeg = Mathf.DeltaAngle(kickHeadPostureKnee, headKnee);
                KickHeadOffsetAnkleDeg = Mathf.DeltaAngle(kickHeadPostureAnkle, headAnkle);
                if (headShare >= 0.999f)
                {
                    kickBaseHip = headHip;
                    kickBaseKnee = headKnee;
                    kickBaseAnkle = headAnkle;
                }
                else if (headShare <= 0.001f)
                {
                    kickBaseHip = kickHeadPostureHip;
                    kickBaseKnee = kickHeadPostureKnee;
                    kickBaseAnkle = kickHeadPostureAnkle;
                }
                else
                {
                    kickBaseHip = Mathf.Lerp(kickHeadPostureHip, headHip, headShare);
                    kickBaseKnee = Mathf.Lerp(kickHeadPostureKnee, headKnee, headShare);
                    kickBaseAnkle = Mathf.Lerp(kickHeadPostureAnkle, headAnkle, headShare);
                }
                KickHeadBaseHipDeg = kickBaseHip;
                KickHeadBaseKneeDeg = kickBaseKnee;
                KickHeadBaseAnkleDeg = kickBaseAnkle;
                // k3: the support's plant is latched on the SAME step - the
                // stance the strike is born from is the stance the support hip
                // is held at while the strike is on.
                kickBaseSupportHip = supportHipJoint != null
                    ? supportHipJoint.jointAngle : 0f;
                KickSupportHipDeg = kickBaseSupportHip;
                // k8: the emitted demand starts where the LEG is, not where the
                // frame is (the head's own continuity; the rate cap carries the
                // first steps toward the frame above).
                kickEmitHip = headHip;
                kickEmitKnee = headKnee;
                kickEmitAnkle = headAnkle;
                KickStrikes++;
                KickLastStrokeSeconds = strokeSeconds;
                KickSideIndex = kickSideIndex;
            }
            kickClock = 0f;
            kickSinceFire = 0f;
        }

        if (kickClock < 0f)
        {
            // Off: the published block is false / 0f (R2) and the pose path
            // takes its own arithmetic for both legs.
            KickPhase = 0;
            KickAge = 0f;
            // k4: the gate's wait belongs to one request - a dropped request is
            // a fresh one when it comes back.
            if (request <= 0.5f) kickGateWait = 0f;
            TickShiftClock(request, dt, request > 0.5f);
            PublishOff();
            PublishShift();
            return;
        }

        KickAge = kickClock;
        float hipDeg, kneeDeg, ankleDeg;
        SampleKick(out hipDeg, out kneeDeg, out ankleDeg);

        // Rate cap on the emitted commands (the leg may be mid-motion when the
        // request lands): the delta is limited per step, the phase clock is not.
        float maxStep = Mathf.Max(0f, kickSlewDegPerSecond) * dt;
        if (maxStep > 0f)
        {
            hipDeg = kickEmitHip + Mathf.Clamp(hipDeg - kickEmitHip, -maxStep, maxStep);
            kneeDeg = kickEmitKnee + Mathf.Clamp(kneeDeg - kickEmitKnee, -maxStep, maxStep);
            ankleDeg = kickEmitAnkle
                + Mathf.Clamp(ankleDeg - kickEmitAnkle, -maxStep, maxStep);
        }
        if (dt > 0f)
        {
            float rate = Mathf.Abs(hipDeg - kickEmitHip) / dt;
            if (rate > KickPeakTargetRateDegPerSec) KickPeakTargetRateDegPerSec = rate;
        }
        kickEmitHip = hipDeg;
        kickEmitKnee = kneeDeg;
        kickEmitAnkle = ankleDeg;
        KickTargetHipDeg = hipDeg;
        KickTargetKneeDeg = kneeDeg;
        KickTargetAnkleDeg = ankleDeg;

        if (-hipDeg > KickPeakHipDeg) KickPeakHipDeg = -hipDeg;
        float kneeOpen = 180f - kneeDeg;
        if (kneeOpen > KickPeakKneeOpenDeg) KickPeakKneeOpenDeg = kneeOpen;

        // Publish the step: the targets the pose path's DriveOneLeg hands to
        // the kicking leg, and the shift's own level (a stroke is in flight, so
        // the level's target is 1).
        TickShiftClock(request, dt, true);
        PublishShift();
        if (balance != null)
        {
            balance.kickStrokeActive = true;
            balance.kickSideRight = kickSideIndex == 1;
            balance.kickHipTargetDeg = hipDeg;
            balance.kickKneeTargetDeg = kneeDeg;
            balance.kickAnkleTargetDeg = ankleDeg;
        }

        if (kickClock >= strokeSeconds) EndKick();
    }

    // lift -> chamber -> extension -> return, each leg joint smoothstepped
    // between its own key angles. The phases are the stroke's own clock; the
    // recursion of the return onto the LATCHED base angles is what hands the leg
    // back.
    private void SampleKick(out float hipDeg, out float kneeDeg, out float ankleDeg)
    {
        float lift = Mathf.Max(0.001f, kickLiftSeconds);
        float chamber = Mathf.Max(0.001f, kickChamberSeconds);
        float extend = Mathf.Max(0.001f, kickExtendSeconds);
        float back = Mathf.Max(0.001f, kickReturnSeconds);
        float t = kickClock;

        if (t < lift)
        {
            float u = SmoothStep(t / lift);
            hipDeg = Mathf.Lerp(kickBaseHip, kickHipLiftDeg, u);
            kneeDeg = Mathf.Lerp(kickBaseKnee, kickKneeLiftDeg, u);
            ankleDeg = Mathf.Lerp(kickBaseAnkle, kickAnkleLiftDeg, u);
            KickPhase = 1;
        }
        else if (t < lift + chamber)
        {
            float u = SmoothStep((t - lift) / chamber);
            hipDeg = Mathf.Lerp(kickHipLiftDeg, kickHipChamberDeg, u);
            kneeDeg = Mathf.Lerp(kickKneeLiftDeg, kickKneeChamberDeg, u);
            ankleDeg = Mathf.Lerp(kickAnkleLiftDeg, kickAnkleChamberDeg, u);
            KickPhase = 2;
        }
        else if (t < lift + chamber + extend)
        {
            float u = SmoothStep((t - lift - chamber) / extend);
            hipDeg = Mathf.Lerp(kickHipChamberDeg, kickHipHitDeg, u);
            kneeDeg = Mathf.Lerp(kickKneeChamberDeg, kickKneeHitDeg, u);
            ankleDeg = Mathf.Lerp(kickAnkleChamberDeg, kickAnkleHitDeg, u);
            KickPhase = 3;
        }
        else
        {
            float u = SmoothStep((t - lift - chamber - extend) / back);
            hipDeg = Mathf.Lerp(kickHipHitDeg, kickBaseHip, u);
            kneeDeg = Mathf.Lerp(kickKneeHitDeg, kickBaseKnee, u);
            ankleDeg = Mathf.Lerp(kickAnkleHitDeg, kickBaseAnkle, u);
            KickPhase = 4;
        }
    }

    private float KickStrokeSeconds()
    {
        return Mathf.Max(0.001f, kickLiftSeconds)
            + Mathf.Max(0.001f, kickChamberSeconds)
            + Mathf.Max(0.001f, kickExtendSeconds)
            + Mathf.Max(0.001f, kickReturnSeconds);
    }

    // Hand the leg back: the clock closes and the published block goes false /
    // 0f on this step, so the pose path writes both legs with its own law again
    // (it never stopped ticking - the kicking leg was only ever a target source
    // for it).
    private void EndKick()
    {
        kickClock = -1f;
        KickPhase = 0;
        KickAge = 0f;
    }

    // k4: the KICKING foot's own share of the two soles' load - the same quantity
    // the recorder publishes as fzL/fzR (BalanceController.KickSoleLoadN), so the
    // gate's number and the row's own columns are one measurement.
    private float KickFootLoadShare()
    {
        if (balance == null) return 0f;
        float dt = Time.fixedDeltaTime;
        bool right = kickSideIndex == 1;
        float kicking = balance.KickSoleLoadN(right, dt);
        float other = balance.KickSoleLoadN(!right, dt);
        float total = kicking + other;
        return total > 1f ? kicking / total : 1f;
    }

    // k12: the CoM's own horizontal velocity, the same estimator the balance
    // layer's own loops read (BodyStateEstimator.comVelocity). A read only.
    private float KickComVelX()
    {
        if (bodyState == null) bodyState = GetComponent<BodyStateEstimator>();
        return bodyState != null ? bodyState.comVelocity.x : 0f;
    }

    // The support-side shift's own level: instant to 1 while the episode is on
    // (in flight or held), eased to 0 over kickShiftReleaseSeconds after.
    private void TickShiftClock(float request, float dt, bool strokeLive)
    {
        bool episode = strokeLive || request > 0.5f;
        if (episode)
        {
            KickShiftLevel = 1f;
        }
        else
        {
            float rate = 1f / Mathf.Max(0.01f, kickShiftReleaseSeconds);
            KickShiftLevel = Mathf.Max(0f, KickShiftLevel - rate * dt);
        }
    }

    private void PublishShift()
    {
        if (balance == null) return;
        balance.kickShiftLevel = KickShiftLevel;
        // k3: the plant travels with the same block (it is 0 / stale only when
        // nothing reads it - the pose path's hold is gated by the level).
        balance.kickSupportHipDeg = KickSupportHipDeg;
        // k14: the wait's own flag travels with the same block (read by the pose
        // path's window only when -kickWindDue is set).
        balance.kickFireHeld = kickFireHeld;
        // k15: the stroke's own head (the lift's unload window) travels with it
        // too - true only inside a stroke, false on every idle frame (read by
        // the pose path's window only when -kickWindHead is set).
        balance.kickHeadActive = kickClock >= 0f && KickPhase == 1;
    }

    private void PublishOff()
    {
        if (balance == null) return;
        balance.kickStrokeActive = false;
        balance.kickHipTargetDeg = 0f;
        balance.kickKneeTargetDeg = 0f;
        balance.kickAnkleTargetDeg = 0f;
    }

    private static float SmoothStep(float u)
    {
        u = Mathf.Clamp01(u);
        return u * u * (3f - 2f * u);
    }

    private void ParseArgs()
    {
        string[] args = System.Environment.GetCommandLineArgs();
        kickLiftSeconds = ReadFloat(args, ArgKickLift, kickLiftSeconds);
        kickChamberSeconds = ReadFloat(args, ArgKickChamber, kickChamberSeconds);
        kickExtendSeconds = ReadFloat(args, ArgKickExtend, kickExtendSeconds);
        kickReturnSeconds = ReadFloat(args, ArgKickReturn, kickReturnSeconds);
        kickIntervalSeconds = ReadFloat(args, ArgKickInterval, kickIntervalSeconds);
        kickSide = ReadFloat(args, ArgKickSide, kickSide);
        kickHipLiftDeg = ReadFloat(args, ArgKickHipLift, kickHipLiftDeg);
        kickKneeLiftDeg = ReadFloat(args, ArgKickKneeLift, kickKneeLiftDeg);
        kickHipChamberDeg = ReadFloat(args, ArgKickHipChamber, kickHipChamberDeg);
        kickKneeChamberDeg = ReadFloat(args, ArgKickKneeChamber, kickKneeChamberDeg);
        kickHipHitDeg = ReadFloat(args, ArgKickHipHit, kickHipHitDeg);
        kickKneeHitDeg = ReadFloat(args, ArgKickKneeHit, kickKneeHitDeg);
        kickAnkleLiftDeg = ReadFloat(args, ArgKickAnkleLift, kickAnkleLiftDeg);
        kickAnkleChamberDeg = ReadFloat(args, ArgKickAnkleChamber, kickAnkleChamberDeg);
        kickAnkleHitDeg = ReadFloat(args, ArgKickAnkleHit, kickAnkleHitDeg);
        kickSlewDegPerSecond = ReadFloat(args, ArgKickSlew, kickSlewDegPerSecond);
        kickShiftReleaseSeconds =
            ReadFloat(args, ArgKickShiftRelease, kickShiftReleaseSeconds);
        // k4: the load gate's two knobs (1 = share 1.0 disarms it: no share can
        // exceed it, so the fire never waits).
        kickLoadGateShare = Mathf.Clamp01(ReadFloat(args, ArgKickLoadShare, kickLoadGateShare));
        kickLoadGateTimeout =
            Mathf.Max(0f, ReadFloat(args, ArgKickLoadTimeout, kickLoadGateTimeout));
        // k8: the head's share (1 = the measured stance's own base, bit for bit;
        // 0 = the stand's own posture).
        kickHeadShare = Mathf.Clamp01(ReadFloat(args, ArgKickHeadShare, kickHeadShare));
        // k12: the head's own phase gate (0 = off, the k1..k11 arithmetic).
        kickHeadVelGate = Mathf.Max(0f, ReadFloat(args, ArgKickHeadVel, kickHeadVelGate));
        // k13: the first fire's own arm (0 = the first fire is exempt; -1 = no
        // arm, the k12 rung governs every fire).
        kickHeadVelFirst = Mathf.Max(-1f,
            ReadFloat(args, ArgKickHeadVelFirst, kickHeadVelFirst));
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
