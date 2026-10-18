using UnityEngine;

public partial class StepPhaseDriver
{
    // WalkLift abort / BalanceController: same two-foot Plant the
    // BeginWalk ladder already uses. Fall-catch has EnterStandingPlant.
    public void RequestStandingPlant()
    {
        if (!walkActive || walkRecoverAnyPose <= 0.5f)
            return;
        // Fall-catch FSM has EnterStandingPlant. Capture walker still
        // uses this two-foot Plant: useFallCatchWalk defaults to 1 even
        // when useCaptureWalker owns Tick, so the old gate was a no-op
        // and a launch squat never returned to stand.
        bool captureOn = balance != null && balance.useCaptureWalker != 0;
        if (useFallCatchWalk != 0 && !captureOn)
            return;
        float t = lastTickTime >= 0f ? lastTickTime : Time.fixedTime;
        SurrenderWalkToStand(t);
    }

    // GETUP-1 / any-pose restore: keep the capture walker off. Tick
    // clears this only on TiltSettled + signed CoM-to-stance (the same
    // BeginWalk Plant→Fall band). A −70° trunk is not that stand.
    public void HoldGetUpPlant()
    {
        StandingRestore = true;
        plantStandSurrender = true;
        if (intent != null)
            intent.standLeg = 0f;
        if (plantGateStallSince < 0f)
            plantGateStallSince = lastTickTime >= 0f ? lastTickTime : Time.fixedTime;
    }

    public void NotifyStoodUp()
    {
        StandingRestore = false;
        plantStandSurrender = false;
        plantGateStallSince = -1f;
        plantReseated = false;
        CaptureStandHeight();
        if (intent != null)
            intent.standLeg = 0f;
        if (balance != null)
            balance.NotifyStoodUpFromPlant();
    }

    // TiltSettled + CoM-to-stance opened. Same bits NotifyStoodUp
    // clears, plus GETUP success if a heap had armed attempts.
    // Happy path never calls this (StandingRestore was false).
    private void ReleaseStandingPlantGate(float simTime)
    {
        plantGateStallSince = -1f;
        plantReseated = false;
        StandingRestore = false;
        plantStandSurrender = false;
        CaptureStandHeight();
        phaseEnterTime = simTime;
        if (intent != null)
            intent.standLeg = 0f;
        if (balance != null)
            balance.NotifyStoodUpFromPlant();
    }

    private void SurrenderWalkToStand(float simTime)
    {
        plantStandSurrender = true;
        StandingRestore = true;
        if (plantGateStallSince < 0f)
            plantGateStallSince = simTime;
        if (intent != null)
            intent.standLeg = 0f;
    }

    // Cycle-0 Fall starts from the standing band already applied at
    // BeginCycle (TiltSettled + |CoM−stance| ≤ plantComLeadMax, else
    // two-foot surrender). StanceLead ≥ 0 never opens on stacked soles:
    // the assigned lead foot sits ~1.5 cm ahead of CoM and the walk
    // window moves ~9 mm, so Plant waited plantMinDuration then dumped
    // (burn_stag +1.30 s, walkSwap 0, knees 3–7°). plantAged still
    // gates the call site. Later cycles keep the capture trigger.
    //
    // Lower bound of the same band, kept OUT of PlantFallGateReady on
    // purpose: a shut gate arms the recovery ladder (re-seat, then surrender
    // to two-foot stance) and raises StandingRestore, and waiting for the
    // roll-over is neither a stall nor a failed step.
    //
    // Later cycles use the extrapolated CoM (capture point), not position:
    // xcom = lead + v/omega0. Position alone would hold a brisk walk far too
    // long — momentum carries the body over a foot it has not reached yet —
    // and would release a body drifting backward with CoM nominally ahead.
    // omega0 comes from the same effective CoM height the stance ankle and
    // the step placement already use, so "where the foot lands" and "when the
    // leg is released" read one number. Full weight here, not walkXCoMWeight:
    // that weight blends a prediction into a control error, this is the
    // prediction itself.
    //
    // Cycle 0 is not that walk: the assigned stance already sits ~1.5 cm
    // ahead of CoM (viewpoint lunge), so Capture >= fallComLeadTrigger
    // asks for 1.5 cm PAST that foot — outside the 3 cm polygon unless
    // the body is already falling. The mid-stance ankle hold (target 0)
    // cancels a 9° lean, but plantRockLead is that same loop's CoP
    // (ankleScale 1). Fading scale to 0 (comment 27) killed the CoP
    // (comment 28). RecoverBlend at 9° then cut that CoP to ~1 cm
    // (comment 30); trailing A2 on the stacked 26 cm sole put CoP at
    // the toe (a brake). Comment 31: that CoP on µ=1.2 stacked soles is
    // tilt, not translation, and the 9° lean must not be × RecoverBlend.
    // Comment 32: the lean on a pinned trail is a bow, not that 2.7 cm.
    // The trail CoM loop (heel) stays until StanceLead >= 0. Comment 33:
    // pelvisLean is a bow; 9° is lumbar only. Comment 34: hip-lock +
    // pelvis-track left StanceLead at -1.5 cm (overconstrained stack).
    // Comment 35: trailing-hip extend as a DrivePelvis counter is
    // cancelled by same-hip P. Comment 36: trail leaves DrivePelvis.
    // Comment 37: both hips SPD-extend to the same plus pose (the
    // leading weld blocked the parallelogram shear). Comment 38:
    // trail hip K=12 and ankle K=3 were the remaining 0 DOF; torsoLean
    // 9° with DrivePelvis off is the bow. Comment 39: that plus pose
    // on a free pelvis is a sit, not shear. Comment 40: trail WORLD
    // tilt + leading DrivePelvis still sat (DrivePelvis flexed the
    // lead). Comment 41: BOTH hips hold the same WORLD thigh tilt;
    // DrivePelvis off. Comment 42: plantRockLead + dual CoM ankle
    // stay at 0 so the WORLD pose is not a shin couple; pose is not
    // gated on trailLead / plantHipExtend. Hip+ankle dampers still
    // snap to tau K; torsoLean waits with pelvisLean for StanceLead >= 0.
    // Comment 43: those hip poses sit (internal couple) and
    // plantRockLead was gated off cycle 0. Restore the ankle CoP +
    // lumbar lean; DrivePelvis stays the pivot. Comment 44: pelvisLean
    // is the WORLD target that pivot holds. Snap only the ankle
    // dampers so the shin can rotate over the sole. Comment 45: that
    // target is still a bow. SPD-hold the quiet hip snapshot so the
    // snapped ankles are the hinge; plantRockLead is the CoP.
    // Comment 46: that lock has no DoF (two contacts). Cycle 0
    // Plant WORLD-holds both thighs at the same clockwise tilt
    // (sit cannot zero a world error). Snap hip+ankle dampers.
    // Comment 47: keep that shear, but do not zero the ankle CoM
    // loops — without plantRockLead the hip couple cannot move
    // CoM and StanceLead stays -1.5 cm. Freeze once StanceLead
    // >= 0; DrivePelvis stays off until EnterFall. Fire
    // when StanceLead >= 0 (trail N≈0). That is the same event
    // FirstStepTrailStillLoaded uses.
    // plantAged is not the cycle-0 trigger (ComLeftSupport at the call
    // site). Later cycles keep the capture trigger / deadline.
    private bool FallLeadReady(float phaseAge)
    {
        if (fallComLeadTrigger <= 0.0001f)
            return true;
        // Cycle 0: CoM leaving the support polygon, not plantAged / the
        // standing band. Stacked soles never grow StanceLead ≥ 0 (~9 mm),
        // and returning true here opened Fall on the timer — burn_stag
        // then hit fallCoMOffset 0.30 at +1.30 s with walkSwap 0.
        if (FallCatchCycleCount == 0)
            return ComLeftSupport;
        // Uncaught Recover swap: CoM entered Plant already past the new
        // stance. Opening Fall on the first aged tick is the 90° topple
        // (capture ≥ 1.5 cm is then true immediately). Wait for a tick
        // inside plantCaughtLeadMax so plantRock may roll; if that tick
        // never comes, the deadline still releases. A hard latch here
        // after Recover stopped waiting for the same band sat Plant
        // until the body fell 93° with no second cycle.
        float deadline = Mathf.Max(0.05f, plantMinDuration) + plantLeadWaitSeconds;
        bool timedOut = plantLeadWaitSeconds <= 0.0001f || phaseAge >= deadline;
        if (PlantRolledFromBehind && StanceLeadCapture >= fallComLeadTrigger)
            return true;
        return timedOut;
    }

    // Plant is standing on two feet on purpose, waiting for the CoM to roll
    // over the stance foot. Read-only, for the Play overlay.
    public bool PlantWaitingForLead { get; private set; }

    // Read-only view of that same band for BalanceController. The
    // double-support push-off may only run on a trunk this gate calls
    // settled: pushing an unsettled Plant is the fold the brake exists for.
    public bool PlantGateReady => PlantFallGateReady();

    // Standing band BeginWalk / restore wait on: TiltSettled (12° /
    // 40 °/s) + signed CoM-to-stance ≤ plantComLeadMax. No column
    // slack — that froze a quiet 6° knee. Happy-path numbers stay.
    public bool StandingPlantReady => RecoverStandReady();

    // DualSupport hold / CaptureWalk BeginWalk / Tick resume: same pose
    // as StandingPlantReady without the 40 °/s omega (mixer start on a
    // quiet 2° trunk). Two manifold-grounded soles, tilt 12°, signed
    // CoM-to-stance ≤ plantComLeadMax. Clearance-0 FootDown is not this
    // band. A 20° heap still fails tilt. Quiet both-down still opens.
    public bool StandingPlantHoldReady => StandingPlantPoseReady();

    // Happy-path TiltSettled number (12°). Plant restore / Plant→Fall
    // read this; do not retune. Omega stays on TiltSettled itself.
    public float StandingPlantTiltMaxDeg => TiltSettledMaxDeg;

    // Live CaptureWalk DualSupport after that hold: TiltSettled 12° and
    // plantComLeadMax 0.10 already launched the walker (BeginWalk / the
    // 30 ms hold). Re-testing them on the 9° command aborted the peel
    // (torso −14° at comX 0.069). Cycle-0 PlantComReady is
    // RecoverComLostMeters 0.30; fallAbortDeg 30 is no longer that 9°.
    // RecoverPoseIsLost skips both on first plant when WalkLift is still
    // on the object (useFallCatchWalk 0, SwapCount 0) — p_auto then
    // slid with margin −0.385 at 1.00 s, c0 the whole first second,
    // pelvisDrop 0.87 / torso 90°.
    //
    // The 6.300 s event (c125adbd, every cadence): DualSupport still
    // both-down, trail still touching, then
    // BalanceController.UpdateState names Falling when |offset| >
    // fallCoMOffset 0.30 at walkTime 5 + 1.30 s. CaptureWalk's copy of
    // that same 0.30 was this method: signedStanceLead (com.x minus
    // the stance sole) > RecoverComLostMeters returned true, then
    // ReturnCaptureWalkToStandingPlant ended the walk (walkStance 8 /
    // walkTransfer 8 / walkFirst right → walkSwapCount 0). The 9°
    // onset bow already stores ~0.13 m; e^{ωT}≈2.4 at T=0.26 s puts
    // lead ~0.31 m — that orbit is the first catch, not a chair.
    // Caller is first DualSupport skate only. SinkCollapsed /
    // fallAbortDeg 30 still restore a heap. signedStanceLead stays
    // on the signature so the caller still measures the orbit.
    public bool StandingPlantHeap(float signedStanceLead)
    {
        if (walkRecoverAnyPose <= 0.5f)
            return false;
        if (SinkCollapsed())
            return true;
        if (fallAbortDeg > 0.001f && AbsBodyTilt() >= fallAbortDeg)
            return true;
        // signedStanceLead > RecoverComLostMeters 0.30 is the 6.300 s
        // dump (c125adbd). Keep measuring it; do not restore on it.
        _ = signedStanceLead > RecoverComLostMeters;
        return false;
    }

    // Tilt half of that band, without the column / lead tests. DualSupport
    // dirt-lift shortens the swing chord on purpose; using StandingPlantReady
    // there would abort a live peel.
    public bool StandingTiltSettled => TiltSettled();

    // CoM has left the STANCE plate along walk — capture point, not
    // position, and not mid-support of both soles. While both are down,
    // GetSupportWidth is the pair: pulling CoM onto the named stance
    // sits deeper inside that polygon, so the event never fires and
    // the swing stays DrivePelvis into the slab (burn_stag 9 mm).
    // StanceLeadCapture is StanceComLead + v/omega0; the toe is
    // footSize.x/2 from the foot centre StanceComLead measures from.
    // plantMinDuration / Idle 0.10 s are not this gate.
    public bool ComLeftSupport
    {
        get
        {
            float half = 0.13f;
            if (humanBody != null && humanBody.footSize.x > 0.05f)
                half = humanBody.footSize.x * 0.5f;
            return StanceLeadCapture >= half;
        }
    }

    // First split is the stance-ankle weight transfer. It must not wait
    // for ComLeftSupport (that needs the named stance ankle). The STEP
    // waits on PlantStandReady. Latch so a later dip inside the plate
    // does not drop standLeg and unwind the roll.
    private bool FirstStepMaySplit()
    {
        if (SwapCount != 0 || FallCatchCycleCount != 0) return true;
        if (firstStepSplitLatched) return true;
        firstStepSplitLatched = true;
        return true;
    }

    private void WriteWalkSplitLeg()
    {
        if (intent == null) return;
        if (plantStandSurrender || !FirstStepMaySplit())
            intent.standLeg = 0f;
        else
            intent.standLeg = currentStance;
    }

    // WalkLift / geo-lift: the first step fires once CoM has left the
    // stance plate. Split may already be on (weight transfer). Cached refs,
    // no Find.
    public bool PlantStandReady => ComLeftSupport;

    // Read-only view of the quantity the Plant→Fall gate itself measures
    // (signed CoM lead over the stance foot, metres). BalanceController needs
    // it so the Plant roll-over setpoint fades as the CoM approaches
    // plantComLeadMax instead of rolling past the gate's upper bound.
    public float StanceLead => StanceComLead();

    // Signed CoM lead over the current swing foot. Plus = CoM ahead of
    // that sole (the catch has not arrested). ApplyRecoverBrake reads it so
    // a calm trunk cannot mirror CoP forward while the mass is still past
    // the plant — but only as CoP, never as a trunk lean. Recover itself
    // no longer waits for this band; that wait was swap 0.
    public float SwingLead => SwingComLead();

    // The extrapolated form of that same lead — the capture point,
    // StanceComLead() + v/omega0 — which is what the Plant→Fall lead gate
    // actually judges (FallLeadReady reads this property, so there is one
    // copy of the formula). StanceLead above is position alone. The Plant
    // trunk lean that starts the roll-over (walkPlantLeanRoll) fades out on
    // this number rather than on a second criterion, so the phase cannot end
    // while the lean that ended it is still commanded.
    // Forward CoM velocity for both capture-point readers here (this property
    // and ActiveCatchFootLead). It comes from BalanceController
    // (walkComVelFilterTau) rather than from the raw estimator sample, because
    // a gate that decides WHEN the leg is released and a target that decides
    // WHERE it lands must be extrapolating one body. This driver runs at
    // order −50, ahead of the controller, so the value is the previous 5 ms
    // step of a filter whose own constant is a dozen — which is also why
    // there is no second copy of the filter on this side. Raw sample when
    // there is no controller at all.
    private float ComVelForwardX()
    {
        if (balance != null)
            return balance.WalkComVelocityX * WalkDir();
        return bodyState != null ? bodyState.comVelocity.x * WalkDir() : 0f;
    }

    public float StanceLeadCapture
    {
        get
        {
            float h = balance != null ? Mathf.Max(0.1f, balance.walkXCoMHeight) : 1f;
            float omega0 = Mathf.Sqrt(9.81f / h);
            return StanceComLead() + ComVelForwardX() / omega0;
        }
    }
}
