using UnityEngine;

// SQUAT TASK (walk/human cycle 84). The stand -> descend -> bottom -> ascend -> stand
// cycle as a first-class task, repeated: "three full squats in a row" stops being a
// single timed window and becomes a state the controller can see, count and judge.
//
// Mirrors GaitStateMachine: a sealed class, named states, a Tuning/Sensed struct in and
// a Decision struct out, per-frame gate bits and entry reasons as evidence, and a rep
// counter that only counts a full cycle that passed its own guards. Nothing here touches
// a Rigidbody2D, a collider or a Muscle: the parent reads the sensed quantities (the
// measured knee angle, the load channel it already filters, comOffset, the vestibular
// tilt, the grounded flags, the level its own ramp has reached) and applies levelTarget
// to the crouch channel it already ramps. While the machine is idle the parent's
// arithmetic is untouched, so every path that does not arm a squat stays bit for bit.
//
// States:
//   Idle       - not armed: no arithmetic, no counter moves.
//   Stand      - level 0. The stand-confirm gates must hold standConfirmSeconds.
//   Descend    - level 1. Down until level arrived AND depth AND load AND settle AND
//                tilt all pass (the bottom is sensed, never scheduled).
//   BottomHold - level 1. Dwell at the bottom; the rep is not counted here.
//   Ascend     - level 0. Up; the rep counts the moment the stand gates fire again.
//   Done       - the requested rep count is met: level 0 for good.
//
// A rep is registered clean only if |comOffset| <= comRepBoundM and |tilt| <= tiltBoundDeg
// held on every frame of the descent+ascent and the soles never unloaded past
// loadDipFloor for longer than loadDipMaxSeconds. The flag is evidence: it never gates
// the motion.
//
// CYCLE 85 adds the load term: the parent's level rate constant is a CEILING and the
// load the soles carry clamps it (Decision.paceScale), so the fold is paced by the
// body, not by the clock - the review's "the fold ran on the clock, not on the load".
// A stall guard keeps a stuck load reading from freezing the task, and the parent can
// switch the whole term off (-squatPace 0), which is this file's own control arm.
// MEASURED, KEPT OFF: the term works as designed (the delivered rate tracks the load
// term at r = 0.72 descent / 0.95 rise) and it reaches the full settled bottom, but the
// take then loses its three reps - the stand gates never re-hold their 0.30 s after a
// fully settled bottom, so the machine refuses the next rep and the body topples. The
// same is true of the longer dwell alone (0.6 and 0.8 s). Both stay doses; the shipped
// default is 0 (this file is then not called at all beyond its gates).
//
// CYCLE 86 adds the rise's own term (TickReleasePace): the return is where a settled
// bottom breaks the take, so the release rate follows the load the soles carry - the
// fold law's shape mirrored - or a flat scale beside it.
//
// CYCLE 88 LANDS the composition as the shipped default: the bottom's own term (a floor
// under the dwell plus the body-settled exit) with the rise's load gate at floor 0.35 -
// c87's priced candidate, re-earned as the default take (c88land_a = c87bg40relf, byte
// for byte). The control arm is one line away: bottomGate 0, releaseLoadFloor 0 and the
// floor 0 give the c84 clock row again (c88ctl_law = 013338bd), and the stand / walk /
// crouch pose fences are untouched - all three proven on the landed build.
public enum SquatState
{
    Idle = 0,
    Stand = 1,
    Descend = 2,
    BottomHold = 3,
    Ascend = 4,
    Done = 5
}

public sealed class SquatStateMachine
{
    // The parent's own constants, handed in every step: the machine owns no tuning of
    // its own, so a dose changes one place in BalanceController.
    public struct Tuning
    {
        public int reps;                    // requested reps; 0 = do not arm
        public float dt;
        public float kneeBaseDeg;           // the pose's stand knee target
        public float depthFlexDeg;          // knee flexion (measured) that counts as the bottom
        public float returnBandDeg;         // |knee - base| that counts as returned
        public float comSettleM;            // |comOffset| for the stand-confirm gate
        public float comRepBoundM;          // |comOffset| bound inside a rep
        public float tiltBoundDeg;          // |body tilt| bound inside a rep
        public float loadFloor;             // measured load / weight, lower edge of the bottom gate
        public float loadCeiling;           // ... upper edge
        public float loadDipFloor;          // a dip below this is a rep defect when it lasts
        public float loadDipMaxSeconds;
        public float standConfirmSeconds;
        public float bottomHoldSeconds;
        public float phaseWatchdogSeconds;  // a phase longer than this aborts the task
        // Cycle 85: the load term. The parent's rate constant is the ceiling; these
        // three describe how the load clamps it and what happens when the load stops
        // answering at all.
        public float paceFloorScale;        // creep the stall guard holds the scale at
        public float paceStallSeconds;      // no-drive time before that creep engages
        public float paceDriveFloor;        // a drive at/below this counts as no drive
        // Cycle 86: the RISE's own term. releaseScale = 1 is the untouched clock (the
        // shipped law); releaseLoadFloor > 0 mirrors the fold law on the way up, so the
        // pose unfolds only while the soles are carrying the body.
        public float releaseScale;          // flat rate scale on the rise (1 = untouched)
        public float releaseLoadFloor;      // load at/below which the rise stops; 0 = off
        // Cycle 85b: the gate must never hold a body that is falling onto the leg. A body
        // sinking faster than releaseSinkGuard m/s takes the plain clock instead of the
        // load gate (a folded leg cannot catch a falling body - the pose has to give).
        // releaseFloor is the belt beside that braces: whatever the gate reads, the rise
        // creeps at least this fast (0 = the gate may freeze, the cycle 85 behaviour).
        public float releaseSinkGuard;      // m/s; 0 = off (the load gate is the only rule)
        public float releaseFloor;          // 0..1 floor under the gate; 0 = none

        // Cycle 90: THE RISE'S CoM ADMITTANCE - the named target's own term. The rise
        // runs on the load and on the clock; neither reads WHERE the mass actually is.
        // After a fully settled bottom the hold seats the CoM 5-7 cm BEHIND the feet
        // (c90 trace: comOffset -0.06 at the exit vs -0.033..-0.055 on the takes that
        // recover) and on some takes the extension feeds that backward run instead of
        // ending it: comOffset -0.06 -> -0.40 through the rise, both ankles on their
        // stop, the body toppling BACKWARD at 20.4 s, while the passing branch brings
        // the CoM back to +0.00. So while the CoM is behind `comBand` the rise's rate
        // is held at `comScale` (0 = a full hold) for at most `comHoldMax` seconds -
        // one incident can never strand the squat - and a body already coming DOWN
        // (the sink belt, the same 0.20 m/s the release term respects) is never held
        // at all. comBand 0 = off: the untouched clock on every earlier path.
        public float comBand;           // m; 0 = off
        public float comScale;          // 0..1 rate scale while the CoM is behind
        public float comHoldMax;        // s; the longest a single hold may last

        // CYCLE 87: THE BOTTOM'S OWN TERM. The dwell (bottomHoldSeconds) becomes a floor;
        // with bottomGate > 0 armed the exit must also read the body's own "the pose has
        // admitted the load at the bottom": the descent ARRESTED (the pelvis has stopped
        // falling), the depth the fold reached still held, the soles carrying the body,
        // both feet down - and that state must HOLD for bottomSettleSeconds. A transition
        // guard, not a timer: cycle 85b measured the failure this answers - under a fixed
        // dwell the body crept forward for exactly as long as the clock ran.
        public float bottomGate;            // > 0 = the bottom exits on the body's own settle
        public float bottomSettleSeconds;   // how long the bottom gates must hold
        public float bottomSettleMps;       // |pelvis vY| at/below which the descent is arrested
    }

    public struct Sensed
    {
        public float kneeDeg;               // measured knee angle, max of both legs
        public float loadRatio;             // measured sole load / weight; < 0 = no sample
        public float comOffsetM;
        public float tiltDeg;
        public bool bothFeetDown;
        public bool locomotionQuiet;        // the parent's own "no fall sequence" read
        public float level;                 // the crouch level the parent's ramp has reached
        public float pelvisVelY;            // pelvis vertical velocity, m/s (< 0 = sinking)
    }

    public struct Decision
    {
        public SquatState next;
        public bool enter;
        public float levelTarget;           // 0 or 1
        public float paceScale;             // 0..1: the load term the parent's rate is clamped by
        public float releaseScale;          // 0..1 on the rise only; 1 = the clock untouched
        public bool repComplete;
        public bool abort;
    }

    // ─── evidence: gate bits, one per guard the frame tested (never read back) ───
    public const int GateLevel = 1;
    public const int GateDepth = 2;
    public const int GateKneeBack = 4;
    public const int GateLoad = 8;
    public const int GateCom = 16;
    public const int GateTilt = 32;
    public const int GateFeet = 64;
    public const int GateQuiet = 128;
    public const int GateHold = 256;
    public const int GateSettle = 512;   // cycle 87: the descent has ARRESTED (the bottom's own term)

    public const int ReasonNone = 0;
    public const int ReasonStandConfirmed = 1;
    public const int ReasonBottomReached = 2;
    public const int ReasonBottomHeld = 3;
    public const int ReasonReturned = 4;
    public const int ReasonRepsDone = 5;
    public const int ReasonWatchdog = 6;

    public SquatState State { get; private set; }
    public float StateAge { get; private set; }
    public int RepsRequested { get; private set; }
    public int RepsDone { get; private set; }
    public int RepOkMask { get; private set; }
    public bool Aborted { get; private set; }
    public int LastGateMask { get; private set; }
    public int LastReason { get; private set; }
    public float LastRepSeconds { get; private set; }
    public float LastBottomKneeDeg { get; private set; }
    public float LastBottomComM { get; private set; }
    public float LastBottomTiltDeg { get; private set; }
    public float LastBottomLoad { get; private set; }
    // Cycle 85 evidence: the load term as it was applied last, and whether the stall
    // guard ever had to take over. The latched pair stays readable after Done.
    public float PaceScale { get; private set; }
    public bool PaceStalled { get; private set; }
    public bool PaceFallbackFired { get; private set; }
    public float PaceStallTotalSeconds { get; private set; }
    public int PaceNoDriveFrames { get; private set; }
    // Cycle 86 evidence: the rise's own term as applied (the smallest scale the take saw),
    // the time the load gate held the rise frozen, and whether the stall guard took over.
    public float ReleaseScaleMin { get; private set; }
    public float ReleaseFrozenSeconds { get; private set; }
    public bool ReleaseFallbackFired { get; private set; }
    public bool ReleaseStalled { get; private set; }
    // Cycle 85b evidence: the frames the sink bypass took the rise off the load gate, and
    // whether the last step was bypassed.
    public float ReleaseSinkSeconds { get; private set; }
    public bool ReleaseSinkBypass { get; private set; }

    // Cycle 87: the bottom's own term, evidence only (never gates anything else). The
    // guard's held time as of the last tick and the seconds the gate held PAST the dwell's
    // floor on this take - the quantity the round prices (cycle 85b's creep window).
    public float BottomSettleAge { get; private set; }
    public float BottomSettleExtraSeconds { get; private set; }
    // Cycle 90: the rise's CoM term as the take ran it - how many times the CoM
    // crossed the band, the total seconds held, and the seconds the sink belt kept a
    // hold off a body that was already coming down. All 0 while unarmed.
    public float ComHoldSeconds { get; private set; }
    public int ComHoldFires { get; private set; }
    public float ComHoldSinkSeconds { get; private set; }
    private float comHoldAge;
    private bool comHoldEngaged;
    private bool comHoldMaxed;
    // The belt under the hold: a body falling faster than this is never held (the same
    // number the release term was taught on c85b's sink failure).
    private const float ComHoldSinkGuard = 0.20f;

    private float quietAge;      // how long the stand-confirm gates have held
    private float repAge;        // seconds inside the current rep
    private float loadDipAge;    // how long the soles have been under loadDipFloor
    private float paceStallAge;  // how long the load term has been giving no drive
    private float releaseStallAge;   // cycle 86: the same, for the rise's load gate
    private float releaseGate = 1f;  // cycle 86: the scale the rise is running at
    private float bottomQuietAge;    // cycle 87: how long the bottom's own gates have held
    private bool repClean;

    // Arm on the first step the parent asks for a count. Counters latch while the
    // machine is idle, so the parent can publish a finished take.
    public void Arm(int reps)
    {
        if (reps <= 0) return;
        RepsRequested = reps;
        RepsDone = 0;
        RepOkMask = 0;
        Aborted = false;
        LastRepSeconds = 0f;
        LastBottomKneeDeg = 0f;
        LastBottomComM = 0f;
        LastBottomTiltDeg = 0f;
        LastBottomLoad = 0f;
        PaceScale = 0f;
        PaceStalled = false;
        PaceFallbackFired = false;
        PaceStallTotalSeconds = 0f;
        PaceNoDriveFrames = 0;
        ReleaseScaleMin = 1f;
        ReleaseFrozenSeconds = 0f;
        ReleaseFallbackFired = false;
        ReleaseSinkSeconds = 0f;
        ReleaseSinkBypass = false;
        releaseStallAge = 0f;
        releaseGate = 1f;
        bottomQuietAge = 0f;
        BottomSettleAge = 0f;
        BottomSettleExtraSeconds = 0f;
        ComHoldSeconds = 0f;
        ComHoldFires = 0;
        ComHoldSinkSeconds = 0f;
        comHoldAge = 0f;
        comHoldEngaged = false;
        comHoldMaxed = false;
        quietAge = 0f;
        repAge = 0f;
        loadDipAge = 0f;
        paceStallAge = 0f;
        repClean = true;
        LastGateMask = 0;
        LastReason = ReasonNone;
        Enter(SquatState.Stand, ReasonNone);
    }

    // Called on every step the parent does NOT arm a squat. The latched counters stay.
    public void Disarm()
    {
        if (State == SquatState.Idle) return;
        State = SquatState.Idle;
        StateAge = 0f;
        quietAge = 0f;
        repAge = 0f;
        loadDipAge = 0f;
        paceStallAge = 0f;
        PaceScale = 0f;      // evidence only: an unarmed take reads 0, like the rest of the block
        PaceStalled = false;
        ReleaseScaleMin = 0f;    // cycle 86: an unarmed take reads 0 for the rise's term too
        ReleaseStalled = false;
        ReleaseSinkBypass = false;
        releaseStallAge = 0f;
        releaseGate = 1f;
        bottomQuietAge = 0f;      // cycle 87: an unarmed take reads the block as 0
        BottomSettleAge = 0f;
        BottomSettleExtraSeconds = 0f;
        comHoldAge = 0f;          // cycle 90: the same rule for the rise's CoM block
        comHoldEngaged = false;
        comHoldMaxed = false;
    }

    public Decision Decide(Tuning t, Sensed s)
    {
        Decision d = default;
        d.paceScale = 1f;      // the untouched clock; the load term may only lower it
        LastGateMask = 0;
        LastReason = ReasonNone;
        float dt = Mathf.Max(0f, t.dt);
        if (t.reps <= 0)
        {
            Disarm();
            return d;
        }
        if (State == SquatState.Idle) Arm(t.reps);
        StateAge += dt;

        // Inside a rep the two quiet bounds and the load dip are watched frame by frame;
        // the flag is evidence only.
        if (State == SquatState.Descend || State == SquatState.BottomHold || State == SquatState.Ascend)
        {
            repAge += dt;
            if (Mathf.Abs(s.comOffsetM) > t.comRepBoundM) repClean = false;
            if (Mathf.Abs(s.tiltDeg) > t.tiltBoundDeg) repClean = false;
            if (s.loadRatio >= 0f && s.loadRatio < t.loadDipFloor)
            {
                loadDipAge += dt;
                if (loadDipAge > t.loadDipMaxSeconds) repClean = false;
            }
            else
            {
                loadDipAge = 0f;
            }
        }

        switch (State)
        {
            case SquatState.Stand: DecideStand(t, s, ref d); break;
            case SquatState.Descend: DecideDescend(t, s, ref d); break;
            case SquatState.BottomHold: DecideBottomHold(t, s, ref d); break;
            case SquatState.Ascend: DecideAscend(t, s, ref d); break;
            case SquatState.Done: d.levelTarget = 0f; break;
        }
        // The load term (cycle 85). Read on the state the frame is MOVING in - the one
        // the decision enters, not the one it leaves - so a step that opens a descent
        // is already paced by the fold shape.
        TickLoadPace(t, s, d.enter ? d.next : State, ref d);
        TickReleasePace(t, s, d.enter ? d.next : State, ref d);
        TickRiseCom(t, s, d.enter ? d.next : State, ref d);
        TickWatchdog(t, ref d);
        return d;
    }

    public void Apply(Decision decision)
    {
        if (!decision.enter) return;
        if (decision.next == SquatState.Descend)
        {
            repClean = true;
            repAge = 0f;
            loadDipAge = 0f;
        }
        // Cycle 87: the bottom's guard clock starts the moment the bottom is entered -
        // the descent's own arrival frame must never count toward it.
        if (decision.next == SquatState.BottomHold) bottomQuietAge = 0f;
        Enter(decision.next, LastReason);
    }

    // Stand: level 0. The gates must hold (knee back within band, comOffset settled,
    // both feet down, no fall sequence) before the next descent may start.
    private void DecideStand(Tuning t, Sensed s, ref Decision d)
    {
        d.levelTarget = 0f;
        bool kneeBack = Mathf.Abs(s.kneeDeg - t.kneeBaseDeg) <= t.returnBandDeg;
        bool comSettled = Mathf.Abs(s.comOffsetM) <= t.comSettleM;
        if (kneeBack) LastGateMask |= GateKneeBack;
        if (comSettled) LastGateMask |= GateCom;
        if (s.bothFeetDown) LastGateMask |= GateFeet;
        if (s.locomotionQuiet) LastGateMask |= GateQuiet;
        bool gates = kneeBack && comSettled && s.bothFeetDown && s.locomotionQuiet;
        quietAge = gates ? quietAge + Mathf.Max(0f, t.dt) : 0f;
        if (quietAge >= t.standConfirmSeconds) LastGateMask |= GateHold;
        if (!gates || quietAge < t.standConfirmSeconds) return;
        if (RepsDone < t.reps)
        {
            LastReason = ReasonStandConfirmed;
            d.next = SquatState.Descend;
            d.enter = true;
        }
        else
        {
            LastReason = ReasonRepsDone;
            d.next = SquatState.Done;
            d.enter = true;
        }
    }

    // Descend: level 1. The bottom is SENSED: the pose has arrived, the measured knee is
    // at the round's depth, the soles carry the body and the body is quiet.
    private void DecideDescend(Tuning t, Sensed s, ref Decision d)
    {
        d.levelTarget = 1f;
        bool level = s.level >= 0.999f;
        bool depth = s.kneeDeg >= t.kneeBaseDeg + t.depthFlexDeg;
        bool load = s.loadRatio >= t.loadFloor && s.loadRatio <= t.loadCeiling;
        bool comSettled = Mathf.Abs(s.comOffsetM) <= t.comRepBoundM;
        bool tiltOk = Mathf.Abs(s.tiltDeg) <= t.tiltBoundDeg;
        if (level) LastGateMask |= GateLevel;
        if (depth) LastGateMask |= GateDepth;
        if (load) LastGateMask |= GateLoad;
        if (comSettled) LastGateMask |= GateCom;
        if (tiltOk) LastGateMask |= GateTilt;
        if (s.bothFeetDown) LastGateMask |= GateFeet;
        if (!(level && depth && load && comSettled && tiltOk && s.bothFeetDown)) return;
        LastBottomKneeDeg = s.kneeDeg;
        LastBottomComM = s.comOffsetM;
        LastBottomTiltDeg = s.tiltDeg;
        LastBottomLoad = s.loadRatio;
        LastReason = ReasonBottomReached;
        d.next = SquatState.BottomHold;
        d.enter = true;
    }

    // BottomHold: level 1, a dwell - and, cycle 87, the bottom's OWN term. A squat that
    // never pauses at the bottom is a bounce, so the dwell stays a FLOOR; with the gate
    // armed the exit must also read the body's own "the pose has admitted the load": the
    // descent is ARRESTED (the pelvis has stopped falling), the depth the fold reached is
    // still held, the soles carry the body, both feet are down and there is no fall
    // sequence - and that state must HOLD for bottomSettleSeconds. The measured failure
    // this answers (c85b): under a fixed dwell the body crept forward under the settled
    // bottom for exactly as long as the clock ran (com -0.001 -> -0.073 m in 0.9 s, then
    // -0.26, -0.41, feet off, Fallen). The dwell now ends when the body says the bottom
    // is done, not when the clock says so.
    private void DecideBottomHold(Tuning t, Sensed s, ref Decision d)
    {
        d.levelTarget = 1f;
        bool timerDone = StateAge >= t.bottomHoldSeconds;
        if (t.bottomGate <= 0f)
        {
            bottomQuietAge = 0f;
            BottomSettleAge = 0f;
            if (!timerDone) return;
            LastReason = ReasonBottomHeld;
            d.next = SquatState.Ascend;
            d.enter = true;
            return;
        }
        bool depthHeld = s.kneeDeg >= t.kneeBaseDeg + t.depthFlexDeg - t.returnBandDeg;
        bool arrested = Mathf.Abs(s.pelvisVelY) <= t.bottomSettleMps;
        // An unreadable meter (ratio < 0, the crouch channel's own sentinel) leaves the
        // load gate open, exactly like the fold's own term does.
        bool loadAdmitted = s.loadRatio < 0f || s.loadRatio >= t.loadFloor;
        if (depthHeld) LastGateMask |= GateDepth;
        if (arrested) LastGateMask |= GateSettle;
        if (loadAdmitted) LastGateMask |= GateLoad;
        if (s.bothFeetDown) LastGateMask |= GateFeet;
        if (s.locomotionQuiet) LastGateMask |= GateQuiet;
        bool gates = depthHeld && arrested && loadAdmitted && s.bothFeetDown && s.locomotionQuiet;
        bottomQuietAge = gates ? bottomQuietAge + Mathf.Max(0f, t.dt) : 0f;
        if (bottomQuietAge >= t.bottomSettleSeconds) LastGateMask |= GateHold;
        BottomSettleAge = bottomQuietAge;
        // The seconds the gate held PAST the dwell's floor: cycle 85b's creep window, on
        // the record as the dose's own price.
        if (timerDone) BottomSettleExtraSeconds += Mathf.Max(0f, t.dt);
        if (!(timerDone && gates && bottomQuietAge >= t.bottomSettleSeconds)) return;
        LastReason = ReasonBottomHeld;
        d.next = SquatState.Ascend;
        d.enter = true;
    }

    // Ascend: level 0. The rep counts the moment the stand gates fire: the return is the
    // object, so a rep that never comes back up is not a rep.
    private void DecideAscend(Tuning t, Sensed s, ref Decision d)
    {
        d.levelTarget = 0f;
        bool kneeBack = Mathf.Abs(s.kneeDeg - t.kneeBaseDeg) <= t.returnBandDeg;
        bool comSettled = Mathf.Abs(s.comOffsetM) <= t.comSettleM;
        if (kneeBack) LastGateMask |= GateKneeBack;
        if (comSettled) LastGateMask |= GateCom;
        if (s.bothFeetDown) LastGateMask |= GateFeet;
        if (s.locomotionQuiet) LastGateMask |= GateQuiet;
        if (!(kneeBack && comSettled && s.bothFeetDown && s.locomotionQuiet)) return;
        RepsDone++;
        LastRepSeconds = repAge;
        if (repClean) RepOkMask |= 1 << (RepsDone - 1);
        d.repComplete = true;
        LastReason = ReasonReturned;
        d.next = SquatState.Stand;
        d.enter = true;
    }

    // ─── LOAD-DRIVEN PACING (cycle 85) ───
    // The review's point, verbatim: "the fold ran on the clock, not on the load". The
    // parent's two rate constants stay what they are - this scale (0..1) is the load
    // term that clamps the one in use, and it is the same pair of shapes the crouch's
    // own C1 leg channel already throttles with:
    //   Descend - 1 while the soles carry body weight, 0 at the C1 band floor (the pose
    //             stops folding while the legs unload, which is the squat_rise1 failure);
    //   Ascend  - 1 at body weight, 0 at the C1 band ceiling (the push stops before the
    //             reaction can turn into a bang).
    // A load the meter cannot read (ratio < 0, the crouch channel's own sentinel) leaves
    // the scale at 1: the plain clock, exactly as before this cycle.
    //
    // The stall guard keeps a stuck reading from freezing a squat: after paceStallSeconds
    // with the drive at or below paceDriveFloor the scale is held at paceFloorScale, so a
    // body whose load never comes back still finishes the rep. The phase watchdog stays
    // the last resort behind it.
    private void TickLoadPace(Tuning t, Sensed s, SquatState moving, ref Decision d)
    {
        d.paceScale = 1f;
        bool pacing = moving == SquatState.Descend || moving == SquatState.Ascend;
        if (!pacing || s.loadRatio < 0f)
        {
            paceStallAge = 0f;
            PaceStalled = false;
            PaceScale = 1f;
            return;
        }
        float drive;
        if (moving == SquatState.Descend)
            drive = Mathf.Clamp01((s.loadRatio - t.loadFloor) / Mathf.Max(0.01f, 1f - t.loadFloor));
        else
            drive = Mathf.Clamp01((t.loadCeiling - s.loadRatio) / Mathf.Max(0.01f, t.loadCeiling - 1f));
        if (drive <= t.paceDriveFloor)
        {
            paceStallAge += Mathf.Max(0f, t.dt);
            PaceNoDriveFrames++;
            PaceStallTotalSeconds += Mathf.Max(0f, t.dt);
            if (paceStallAge >= t.paceStallSeconds)
            {
                PaceStalled = true;
                PaceFallbackFired = true;
            }
        }
        else
        {
            paceStallAge = 0f;
            PaceStalled = false;
        }
        if (PaceStalled) drive = Mathf.Max(drive, t.paceFloorScale);
        PaceScale = drive;
        d.paceScale = drive;
    }

    // ─── THE RISE'S OWN TERM (cycle 86) ───
    // Cycle 85 measured the depth finish and named what blocks it. After a settled bottom
    // the release is a pose spring let go: the body leaves the ground (the load meter reads
    // 0 for whole frames, the landing spikes to 2.2 W, the knees flex back to 24 deg), the
    // stand gate never re-holds its 0.30 s, the machine refuses the next rep and the take
    // topples 1.5-2.5 s later. The same take that only skims the bottom (c84's 2.5-6.4 mm)
    // releases while still sinking and walks away - so the depth is not what breaks it,
    // the RETURN is.
    //
    // The rise is priced the way the fold already was: the load the soles carry gates the
    // rate, with the fold law's own shape mirrored -
    //   gate = 1 while the body is on its feet (ratio >= 1), 0 at the band floor (0.90),
    // so the pose unfolds only while it is carrying the body: a body that has floated has
    // no further extension to float on, and lands back onto a leg that is still folded.
    // `releaseScale` is the flat dose beside it (a slower clock, no load term at all), and
    // the same stall guard the load term has keeps a meter that never reads again from
    // freezing the rise - the phase watchdog stays the last resort behind it.
    private void TickReleasePace(Tuning t, Sensed s, SquatState moving, ref Decision d)
    {
        d.releaseScale = 1f;
        float scale = t.releaseScale > 0f ? t.releaseScale : 1f;
        if (moving != SquatState.Ascend)
        {
            // The rise only. The fold, the bottom, the stand's own residual release and
            // every path that never arms the task keep the untouched clock, which is also
            // why a stuck meter can never leave a take folded at its end.
            releaseStallAge = 0f;
            ReleaseStalled = false;
            releaseGate = 1f;
            ReleaseSinkBypass = false;
            return;
        }
        ReleaseSinkBypass = t.releaseSinkGuard > 0f && s.pelvisVelY < -t.releaseSinkGuard;
        if (ReleaseSinkBypass) ReleaseSinkSeconds += Mathf.Max(0f, t.dt);
        float gate = 1f;
        if (t.releaseLoadFloor > 0f && s.loadRatio >= 0f && !ReleaseSinkBypass)
        {
            gate = Mathf.Clamp01((s.loadRatio - t.releaseLoadFloor)
                / Mathf.Max(0.01f, 1f - t.releaseLoadFloor));
            if (gate <= t.paceDriveFloor)
            {
                releaseStallAge += Mathf.Max(0f, t.dt);
                ReleaseFrozenSeconds += Mathf.Max(0f, t.dt);
                if (releaseStallAge >= t.paceStallSeconds)
                {
                    ReleaseStalled = true;
                    ReleaseFallbackFired = true;
                }
            }
            else
            {
                releaseStallAge = 0f;
                ReleaseStalled = false;
            }
            if (ReleaseStalled) gate = Mathf.Max(gate, t.paceFloorScale);
        }
        // Belt and braces (cycle 85b): the floor under the gate, and the sink bypass above
        // it. A freeze that starts while the body is FALLING is the one failure the round
        // measured (dwell 0.8 + scale 0.4: the level sat at 0.33 while the pelvis went
        // -0.09 -> -1.8 m/s and the take collapsed) - the load gate must hold a body that
        // floats, never one that is coming down onto the leg.
        if (t.releaseFloor > 0f) gate = Mathf.Max(gate, t.releaseFloor);
        releaseGate = scale * gate;
        if (releaseGate < ReleaseScaleMin) ReleaseScaleMin = releaseGate;
        d.releaseScale = releaseGate;
    }

    // ─── THE RISE'S CoM ADMITTANCE (cycle 90) ───
    // The named target of the round: the return after a fully settled bottom. The
    // measured mechanism (c90 trace, c89's settle 0.20 on the current player): a fully
    // settled bottom leaves the CoM 5.5-6.6 cm BEHIND the feet, and on the takes that
    // die the extension never brings it back - comOffset runs 0.06 -> 0.40 backward
    // through the rise, both ankles saturate on their stop, and the body topples
    // BACKWARD. The returning takes start the rise 0.5-2 cm less far back and the
    // extension ends them at comOffset +0.00..+0.003. The fork is decided while the
    // CoM is still inside the foot, so this term spends its authority there: while the
    // CoM sits behind `comBand` the rise holds at `comScale` (0 = full hold), one
    // incident for at most `comHoldMax`; the moment the CoM crosses back the extension
    // resumes at the paced rate, and a body already sinking past the belt is never
    // held (the same 0.20 m/s the release term was taught on c85b's sink failure).
    private void TickRiseCom(Tuning t, Sensed s, SquatState moving, ref Decision d)
    {
        if (t.comBand <= 0f || moving != SquatState.Ascend)
        {
            comHoldAge = 0f;
            comHoldEngaged = false;
            comHoldMaxed = false;
            return;
        }
        bool sink = s.pelvisVelY < -ComHoldSinkGuard;
        if (sink) ComHoldSinkSeconds += Mathf.Max(0f, t.dt);
        bool behind = s.comOffsetM <= -t.comBand;
        bool holdNow = behind && !sink;
        if (holdNow)
        {
            if (!comHoldEngaged)
            {
                comHoldEngaged = true;
                comHoldAge = 0f;
                comHoldMaxed = false;
                ComHoldFires++;
            }
            comHoldAge += Mathf.Max(0f, t.dt);
            ComHoldSeconds += Mathf.Max(0f, t.dt);
            if (!comHoldMaxed && comHoldAge >= t.comHoldMax) comHoldMaxed = true;
        }
        else
        {
            comHoldEngaged = false;
            comHoldAge = 0f;
            comHoldMaxed = false;
        }
        float eff = (holdNow && !comHoldMaxed) ? Mathf.Clamp01(t.comScale) : 1f;
        if (eff < 1f)
        {
            d.releaseScale *= eff;
            if (d.releaseScale < ReleaseScaleMin) ReleaseScaleMin = d.releaseScale;
        }
    }

    // The watchdog: a phase that cannot finish is a stall, not a squat. Aborting puts the
    // body back to stand and stops the task instead of holding the fold. The abort's own
    // return to stand is NOT load-paced: getting upright is the whole point of aborting.
    private void TickWatchdog(Tuning t, ref Decision d)
    {
        if (State != SquatState.Descend && State != SquatState.BottomHold && State != SquatState.Ascend) return;
        if (t.phaseWatchdogSeconds <= 0f || StateAge <= t.phaseWatchdogSeconds) return;
        Aborted = true;
        LastReason = ReasonWatchdog;
        d.levelTarget = 0f;
        d.paceScale = 1f;
        d.next = SquatState.Done;
        d.enter = true;
        d.abort = true;
    }

    private void Enter(SquatState next, int reason)
    {
        State = next;
        StateAge = 0f;
        if (next == SquatState.Stand) quietAge = 0f;
        if (reason != ReasonNone) LastReason = reason;
    }
}
