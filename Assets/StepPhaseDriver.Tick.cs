using UnityEngine;

public partial class StepPhaseDriver
{
    public void Tick(float simTime)
    {
        EnsureRefs();
        if (!walkActive || intent == null || walkStartTime < 0f)
            return;

        lastTickTime = simTime;

        if (simTime < walkStartTime)
        {
            intent.standLeg = 0f;
            return;
        }

        // WALK-3: the capture-point walker (BalanceControllerCaptureWalk.cs)
        // is the gait now. Neither automaton below may also write phases or
        // the split-leg intent — that channel is not how the new walker picks
        // a support foot, and two writers on one gait is exactly the hybrid
        // this walker replaces. walkActive stays true: it is still the bit
        // that says "the human is walking".
        if (balance != null && balance.useCaptureWalker != 0)
        {
            // Resume after a heap / tipped BeginWalk. Same standing
            // band BeginWalk used (TiltSettled + CoM-to-stance). Not
            // PlantFallGateReady (cycle 0 skips tilt / 0.30 m) and not
            // the column test (quiet 6° knee, phase 0 / 35 s). A pose
            // outside it keeps two-foot quiet stance until the gate
            // opens, then the walker starts. Play Tick is −50; headless
            // Tick is after FixedUpdate. TiltSettled / plantComLeadMax stay.
            if (walkRecoverAnyPose > 0.5f && (StandingRestore || plantStandSurrender))
            {
                // Same pose-only band BeginWalk used (not omega: mixer
                // start on a quiet trunk is not a heap). TiltSettled
                // 12° + signed CoM-to-stance ≤ plantComLeadMax, then
                // the walker starts. GETUP-1 owns the joints until
                // FinishGetUp: releasing here at 12° launched DualSupport
                // on a still-tipped trunk. Fallen keeps the hold until
                // the walk key starts TickGetUp.
                bool getUpOwns = balance.locomotionState
                    == BalanceController.LocomotionState.GettingUp
                    || balance.locomotionState
                    == BalanceController.LocomotionState.Fallen;
                if (getUpOwns)
                    SurrenderWalkToStand(simTime);
                else if (CaptureWalkColdStandReady())
                    ReleaseStandingPlantGate(simTime);
                else
                    SurrenderWalkToStand(simTime);
            }
            intent.standLeg = 0f;
            return;
        }

        if (useFallCatchWalk != 0)
        {
            TickFallCatchWalk(simTime);
            return;
        }

        float phaseAge = simTime - phaseEnterTime;

        if (phase == Phase.Stance)
        {
            // Capture step: split so the stance ankle can roll; lift when
            // ComLeftSupport. RecoverStandReady is the quiet band — it
            // passes on tick 1. The dump was idle-clock lift on that
            // split (burn_stag +1.30 s), not the split itself.
            // WalkLift has no Plant phase. The same TiltSettled +
            // CoM-to-stance band still holds two-foot quiet stance until
            // the gate opens, then the split / Unload run as before.
            // Do not re-judge TiltSettled while WalkLift owns the swing:
            // initiation lean is the step. Happy path never raises restore.
            if (walkRecoverAnyPose > 0.5f && useFallCatchWalk == 0)
            {
                bool liftBusy = balance != null && balance.useWalkLift != 0
                    && (balance.WalkLiftPhaseCode != 0
                        || balance.WalkLiftMaySwap
                        || SwapCount == 0);
                if (StandingRestore || plantStandSurrender)
                {
                    if (RecoverStandReady() && !SinkCollapsed())
                    {
                        ReleaseStandingPlantGate(simTime);
                    }
                    else
                    {
                        SurrenderWalkToStand(simTime);
                        prevTickTime = simTime;
                        return;
                    }
                }
                else if (SinkCollapsed()
                    || (!liftBusy && RecoverPoseIsLost()))
                {
                    SurrenderWalkToStand(simTime);
                    prevTickTime = simTime;
                    return;
                }
            }
            WriteWalkSplitLeg();
            float dt = prevTickTime < 0f ? 0f : Mathf.Max(0f, simTime - prevTickTime);
            prevTickTime = simTime;

            bool swingGrounded = IsSwingFootGrounded();
            bool walkLiftMode = balance != null && balance.useWalkLift != 0;
            // Idle is still the step: Unload starts on this Stance. Treating
            // PhaseCode==0 as "not busy" let RecoverPoseIsLost zero standLeg
            // before the swing knee ran (p_now: maxStandLegLevel 0, walkPhase
            // only −1/0).
            bool walkLiftBusy = walkLiftMode
                && (balance.WalkLiftPhaseCode != 0
                    || balance.WalkLiftMaySwap
                    || SwapCount == 0);
            if (walkTiltAbortDeg > 0.001f && phaseAge >= 0.1f && !walkLiftMode)
            {
                float absTilt = AbsBodyTilt();
                if (absTilt >= walkTiltAbortDeg)
                {
                    EnterTransfer(simTime);
                    intent.standLeg = 0f;
                    return;
                }
            }
            if (!swingGrounded)
            {
                swingWasAirborne = true;
                swingAirStreak += dt;
                if (stanceHeelAirMin > 0.001f && swingAirStreak >= stanceHeelAirMin)
                    swingAirQualified = true;
            }
            else
            {
                // WalkLift plants on the Stance split. standLeg=0 here is
                // both hips DrivePelvis and both ankles CoM on a dumped
                // trunk (walklift_unload t=6.01, first air + CoM trigger).
                if (balance == null || balance.useWalkLift == 0)
                {
                    if (balance != null && balance.WalkLiftMayTransfer
                        && phaseAge >= 0.15f)
                    {
                        EnterTransfer(simTime);
                        intent.standLeg = 0f;
                        return;
                    }
                    bool useHeel = stanceHeelAirMin > 0.001f && stanceHeelStrikeMinAge > 0.001f;
                    if (useHeel && swingAirQualified && phaseAge >= stanceHeelStrikeMinAge)
                    {
                        EnterTransfer(simTime);
                        intent.standLeg = 0f;
                        return;
                    }
                }
                swingAirStreak = 0f;
            }

            // Body is already ahead of the support — the step is overdue, cannot wait for the timer.
            float activeComTrigger = stanceComTrigger + ActiveWalkTargetSpeed() * stanceComTriggerPerSpeed;
            float triggerMinAge = Mathf.Max(
                Mathf.Max(0.05f, stanceComMinAge),
                ActiveStanceDuration() * Mathf.Clamp01(walkComTriggerMinStanceFraction));
            // WalkLift: first airborne frame sets swingWasAirborne, then
            // this gate zeros standLeg at −40° (Headless −walkComTrigger
            // 0.01). The overlay confirms the plant; DualSupport is the
            // dump (walklift_swap 7.93 s).
            if (!walkLiftMode
                && activeComTrigger > 0.0001f
                && phaseAge >= triggerMinAge
                && swingWasAirborne
                && StanceComLead() >= activeComTrigger)
            {
                EnterTransfer(simTime);
                intent.standLeg = 0f;
                return;
            }

            float activeStance = Mathf.Max(0.1f, ActiveStanceDuration());
            if (!IsRunning() && SwapCount == 0)
                activeStance = Mathf.Min(activeStance, Mathf.Max(0.1f, firstWalkStanceDuration));
            if (walkLiftMode)
            {
                // Planted clock EnterStance is the wall (keep35/spent0).
                // Swap only after WalkLift Advance plants an airborne
                // swing AHEAD of CoM. Idle + ComLeftSupport +
                // SwingFootIsAhead relabelled the 3 cm viewpoint lunge
                // (right already leading) as a step: first MaySwap /
                // scrape, then the new swing is the old lead, then
                // EnterStance onto a sole behind CoM — burn_walk 5.835,
                // swap 2, RelaxAllMuscles. Unload on PlantStandReady
                // is still the lift. Direct EnterStance, not Transfer.
                if (balance.WalkLiftMaySwap && WalkLiftSwapSettled())
                    EnterStance(simTime, -currentStance);
                return;
            }
            if (!walkLiftBusy && phaseAge >= activeStance)
            {
                EnterTransfer(simTime);
                intent.standLeg = 0f;
            }
            return;
        }

        if (balance != null && balance.useWalkLift != 0)
        {
            // WalkLift never asked for DualSupport. Restore the split
            // without counting a swap.
            phase = Phase.Stance;
            phaseEnterTime = simTime;
            if (intent != null)
                intent.standLeg = currentStance;
            return;
        }

        intent.standLeg = 0f;
        bool ready = IsTransferReady();
        if (ready)
            TransferReadyFrames++;
        bool swingGroundedNow = IsSwingFootGrounded();
        if (phaseAge >= Mathf.Max(transferMinDuration, transferTouchdownMaxAge) && swingGroundedNow)
        {
            EnterStance(simTime, -currentStance);
            return;
        }
        if (phaseAge >= transferMinDuration && ready)
        {
            EnterStance(simTime, -currentStance);
            return;
        }
        if (phaseAge >= ActiveTransferMaxDuration() && IsTransferFallbackReady())
        {
            EnterStance(simTime, -currentStance);
            return;
        }
        // If the swing stays airborne, BothFeet may never become true and
        // Transfer can stall visually as a frozen half-step. Force a phase
        // switch after a bounded timeout in both walk and run.
        float hardTimeout = IsRunning() ? 1.75f : 1.25f;
        if (phaseAge >= ActiveTransferMaxDuration() * hardTimeout)
            EnterStance(simTime, -currentStance);
    }

    private bool IsRunning()
    {
        return intent != null && intent.run > 0.5f;
    }

    private float ActiveStanceDuration()
    {
        if (IsRunning())
            return Mathf.Max(0.5f, runStanceDuration);

        float duration = stanceDuration;
        float targetSpeed = ActiveWalkTargetSpeed();
        if (walkCadenceStanceGain > 0.0001f && targetSpeed > 0.0001f)
            duration = Mathf.Max(walkCadenceMinStance, duration - targetSpeed * walkCadenceStanceGain);
        return Mathf.Max(0.1f, duration);
    }

    private float ActiveTransferMaxDuration()
    {
        if (IsRunning())
            return Mathf.Max(0.5f, runTransferMaxDuration);

        float duration = transferMaxDuration;
        float targetSpeed = ActiveWalkTargetSpeed();
        if (walkCadenceTransferGain > 0.0001f && targetSpeed > 0.0001f)
            duration = Mathf.Max(walkCadenceMinTransfer, duration - targetSpeed * walkCadenceTransferGain);
        return Mathf.Max(0.1f, duration);
    }

    private float ActiveWalkTargetSpeed()
    {
        return balance != null ? Mathf.Max(0f, balance.walkTargetSpeed) : 0f;
    }

    private void EnterTransfer(float simTime)
    {
        phase = Phase.Transfer;
        phaseEnterTime = simTime;
        swingWasAirborne = false;
        swingAirStreak = 0f;
        swingAirQualified = false;
        prevTickTime = -1f;
    }
}
