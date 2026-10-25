using UnityEngine;

public partial class StepPhaseDriver
{
    // currentStance +1 = stance on the right → swing on the left; −1 the other way.
    // GetContacts-only used to report both soles as air while the solver
    // still held them (toe-roll hang, walkSwap 0). NearContact is the same
    // band IsFirstStepPlanted / PickStance already used on cycle 0.
    private bool IsSwingFootGrounded()
    {
        if (bodyState == null) return true;
        return currentStance > 0.5f
            ? FootDown(true)
            : FootDown(false);
    }

    private bool IsStanceFootGrounded()
    {
        if (bodyState == null) return true;
        return currentStance > 0.5f
            ? FootDown(false)
            : FootDown(true);
    }

    private bool FootDown(bool left)
    {
        if (bodyState == null) return true;
        if (left ? bodyState.leftFootGrounded : bodyState.rightFootGrounded)
            return true;
        float clearance = left
            ? bodyState.leftFootGroundClearance
            : bodyState.rightFootGroundClearance;
        return clearance <= ContactOffsetBand();
    }

    // Plant→Fall and BeginCycle share this band. Forward CoM past
    // plantComLeadMax (0.10 m) blocks on later cycles; CoM behind the
    // foot does not — that is the catch pose. Cycle 0 initiation has no
    // catch plant: a 10 cm third-law shift from the knee lift is still
    // a stand (RecoverPoseIsLost is 0.30 m). Using 0.10 there shut the
    // gate, raised StandingRestore, and turned rock / lean / geo-lift
    // off: Fall never started (walkSwap 0). Stance in the air is not a
    // stand.
    // WalkLift never increments FallCatchCycleCount. SwapCount is that
    // first-plant bit there so a post-swap catch keeps the signed bound
    // instead of the 0.30 m lunge ceiling.
    private bool IsFirstWalkPlant()
    {
        if (useFallCatchWalk != 0)
            return FallCatchCycleCount == 0;
        return SwapCount == 0;
    }

    private bool PlantComReady()
    {
        float lead = StanceComLead();
        float bound = IsFirstWalkPlant()
            ? RecoverComLostMeters
            : Mathf.Max(0.05f, plantComLeadMax);
        return lead <= bound;
    }

    // Cycle 0 commands ~9° of Plant lean (fallPelvisLean × fallForwardLeanScale).
    // TiltSettled is 12°, and initiation+8° was 17°: the overshoot of the
    // command that starts the step shut the gate, raised StandingRestore,
    // and killed geo-lift / lean before Fall. That lean IS the first
    // fall — Fall abort already Catchs it. Cycle 0 Plant does not judge
    // tilt; sink and |lead| > RecoverComLostMeters still fail the gate.
    // Later cycles keep 12° / 40 °/s: a post-catch Plant must actually
    // be standing.
    private bool PlantTiltReady()
    {
        if (IsFirstWalkPlant())
            return true;
        return TiltSettled();
    }

    // Sensors that TiltSettled / StanceComLead / FootDown actually read.
    // Fail closed: bodyState null, vestibular null, or sensorTick 0
    // (ArmWalk in Update before the first estimator step) used to look
    // like a stand. FootDown treats clearance 0 as down, AbsBodyTilt
    // is 0 without a torso, StanceComLead is 0 without CoM — that
    // launched a heap, then RelaxAllMuscles. Quiet BeginWalk after a
    // few idle steps has sensorTick in the thousands.
    private bool PlantSensorsReady()
    {
        if (bodyState == null || vestibular == null || comCalculator == null)
            return false;
        return bodyState.sensorTick > 0;
    }

    // SinkCollapsed is the same band's height: a fold over the feet still
    // reads TiltSettled + lead, and without this BeginCycle would call it
    // a stand (walkRecoverAnyPose; 0 = former tilt/lead/ground).
    // Sensors first: cycle-0 still skips TiltSettled (9° initiation),
    // but it must not open Fall on spawn zeros. Later cycles keep
    // TiltSettled + signed lead ≤ plantComLeadMax.
    private bool PlantFallGateReady()
    {
        if (!PlantSensorsReady())
            return false;
        return PlantTiltReady() && PlantComReady() && IsStanceFootGrounded()
            && !SinkCollapsed();
    }

    // Pose half of the standing Plant: |tilt| < 12°, BOTH soles the
    // solver is holding, signed CoM-to-stance ≤ plantComLeadMax, not
    // a sink. FootDown's clearance-0 band is not a stand: a hovering
    // heap looked planted, Tick cleared restore, DualSupport restarted
    // in the air (p_auto 14:48: first SS 5.17 s, then 87 % air labelled
    // DualSupport, swap 0). Quiet launch is both manifold-grounded.
    // Omega is separate: DualSupport hold must not abort a quiet 2°
    // trunk when the mixer start spikes past RecoverOmegaSettledDeg 40.
    private bool StandingPlantPoseReady()
    {
        if (!PlantSensorsReady())
            return false;
        if (AbsBodyTilt() >= TiltSettledMaxDeg
            || !BothFeetPlanted()
            || SinkCollapsed())
            return false;
        return RecoverComToStanceReady();
    }

    // Standing Plant wait: two loaded soles, not FootDown clearance-0.
    // Cycle-0 PlantFallGateReady still uses IsStanceFootGrounded — that
    // split Plant may open Fall on one plate. Cached refs, no Find.
    private bool BothFeetPlanted()
    {
        if (bodyState == null)
            return false;
        return bodyState.leftFootGrounded && bodyState.rightFootGrounded;
    }

    // CaptureWalk BeginWalk / resume. Pose half of the standing band
    // (tilt 12° + both planted soles + signed CoM-to-stance ≤
    // plantComLeadMax), not omega. DualSupport hold already uses
    // StandingPlantHoldReady; mixer start can spike past 40 °/s on a
    // quiet 2° trunk. A 20° heap still fails TiltSettled. Not the
    // cycle-0 PlantFallGateReady skip.
    private bool CaptureWalkColdStandReady()
    {
        return StandingPlantHoldReady;
    }

    // Standing Plant: signed CoM-to-stance ≤ plantComLeadMax. PlantComReady
    // on cycle 0 is RecoverComLostMeters (0.30 m) so a 9° initiation
    // third-law shift can still open Fall. That 0.30 m must not call a
    // cold BeginWalk with CoM 20 cm past the foot a stand — the walker
    // then launches and dumps. CoM behind the stance foot is a catch, not
    // a heap. Quiet start |lead| ~0 still passes.
    private bool RecoverComToStanceReady()
    {
        return StanceComLead() <= Mathf.Max(0.05f, plantComLeadMax);
    }

    // Heap the 3 s Plant stall cannot catch: abort tilt, a fold the
    // CoM-offset band cannot see, or cycle-0 CoM already past the
    // Falling metre. Not used in Fall/Reach/Catch — running past the
    // foot is the step. walkRecoverAnyPose 0 keeps the former ladder.
    private bool RecoverPoseIsLost()
    {
        if (walkRecoverAnyPose <= 0.5f)
            return false;
        if (SinkCollapsed())
        {
            // Failed-step Plant is a heap. Cycle-0 Plant before Fall is
            // too. After Recover the landing is still the initiation height
            // (lean + 55° already near sinkAbortDrop) — that is the new
            // support's first plantMinDuration, not a two-foot stand.
            if (StandingRestore || plantStandSurrender || FallCatchCycleCount == 0)
                return true;
            return PhaseAge() >= Mathf.Max(0.05f, plantMinDuration);
        }
        // Cycle 0: fallAbortDeg is the first fall (Fall/Reach Catch it).
        // Treating it as a lost Plant pose restored two-foot stance
        // before Fall, walkSwap=0. Later cycles still surrender at 30°.
        // WalkLift later-plant is SwapCount > 0: 30° there is a dump,
        // not initiation.
        // Exception: while the mass is running past the support the trunk
        // passes 30° inside the escape window (the swap arrives at −1.2°
        // and the tilt is 65° a second later), and this surrender fired
        // first — standLeg 0 on a body whose CoM is outside both soles is
        // the topple. A tall body with a loaded stance still has a step
        // left; a collapse (above) does not.
        if (!IsFirstWalkPlant()
            && fallAbortDeg > 0.001f && AbsBodyTilt() >= fallAbortDeg
            && !KeepSteppingOnRunaway())
            return true;
        // Cold BeginWalk past fallAbortDeg is a heap, not the first
        // commanded fall. Cycle-0 Fall still Catchs 30°; this only
        // fires while Plant is already restoring (StandingRestore),
        // which a quiet 9° initiation never is.
        if (IsFirstWalkPlant()
            && StandingRestore
            && fallAbortDeg > 0.001f && AbsBodyTilt() >= fallAbortDeg)
            return true;
        // WalkLift: CoM past the stance toe is the inverted-pendulum
        // step, not a heap, while Unload/Clear/Advance still owns the
        // swing or MaySwap is waiting for the catch plant. Surrender
        // here is standLeg=0 with |offset| already 0.30 —
        // RelaxAllMuscles (burn_walk 5.835). Fall-catch never sets
        // useWalkLift on this path. Sink / 30° still spend above.
        //
        // c125adbd 6.300 (every cadence, walkTime 5 + 1.30 s): first
        // DualSupport both-down, |StanceComLead| crossed
        // RecoverComLostMeters 0.30, this returned true, StandingRestore
        // killed CaptureWalk (walkStance 8 / walkTransfer 8 / walkFirst
        // right → walkSwapCount 0). That lead is the first catch orbit
        // (9° bow → ~0.31 m), not a chair. CaptureWalk / WalkLift first
        // plant keep measuring it; do not restore on it.
        if (IsFirstWalkPlant()
            && Mathf.Abs(StanceComLead()) > RecoverComLostMeters)
        {
            if (balance != null && balance.useCaptureWalker != 0)
                return false;
            if (useFallCatchWalk == 0
                && balance != null
                && balance.useWalkLift != 0
                && (balance.WalkLiftPhaseCode != 0
                    || balance.WalkLiftMaySwap
                    || SwapCount == 0))
                return false;
            return true;
        }
        return false;
    }
}
