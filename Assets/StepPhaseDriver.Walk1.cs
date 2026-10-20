using UnityEngine;

public partial class StepPhaseDriver
{
    [Header("Stance")]
    // Keep in sync with HeadlessTrial -walkStance default.
    [Tooltip("Minimum seconds on one leg before requesting transfer.")]
    public float stanceDuration = 6f;
    [Tooltip("Seconds of the first walk stance after input to avoid a frozen start.")]
    public float firstWalkStanceDuration = 2.5f;

    // Run (intent.run): shorter stance — more frequent swaps. Not SIMBICON.
    [Tooltip("Seconds of Stance when intent.run=1.")]
    public float runStanceDuration = 4f;
    [Tooltip("Seconds reduction of walk stance per 1 m/s target speed.")]
    public float walkCadenceStanceGain = 2.0f;
    [Tooltip("Minimum walk stance duration under cadence scaling.")]
    public float walkCadenceMinStance = 2.0f;

    // After the first lift-off: grounded again → Transfer, do not wait for the timer.
    // One air frame is a bounce (heel_w1 fold). Need an air streak.
    // 0 = off (timer stanceDuration only). CLI -walkHeelMin / -walkHeelAir.
    [Tooltip("Min Stance age (s) after qualified air → Transfer.")]
    public float stanceHeelStrikeMinAge = 0f;
    [Tooltip("Min continuous !swingGrounded (s) before heel-strike qualifies. 0 = off.")]
    public float stanceHeelAirMin = 0f;

    // Walk is a forward fall with a planted foot, not a timer-driven step.
    // While Stance holds the timer, walkComLeadX lets the body pass the
    // stance foot and fall face-first (lead_003…lead_015: torso ~92°,
    // pelvisDrop 0.86). Support swap must follow the body: CoM has gone
    // ahead of the stance foot by stanceComTrigger — time to plant the other.
    // This stays a condition inside the phase driver, not a separate finite
    // automaton: do not open SIMBICON. 0 = timer only, former behavior.
    [Tooltip("CoM ahead of the stance foot (m) → Transfer. 0 = timer only.")]
    public float stanceComTrigger = 0.01f;

    [Tooltip("Min Stance age (s) before the CoM trigger fires.")]
    public float stanceComMinAge = 0.25f;
    [Tooltip("Additional stanceComTrigger per 1 m/s target speed.")]
    public float stanceComTriggerPerSpeed = -0.004f;
    [Tooltip("Earliest stance fraction where CoM-trigger may switch to Transfer. 0 = off.")]
    public float walkComTriggerMinStanceFraction = 0f;
    [Tooltip("If |torso tilt| exceeds this in stance, force Transfer. 0 = off.")]
    public float walkTiltAbortDeg = 0f;
    [Tooltip("Allow Transfer->Stance only when |torso tilt| <= this. 0 = off.")]
    public float walkTiltRecoverDeg = 0f;

    [Header("Transfer (event-gated)")]
    [Tooltip("Max seconds in dual support — then a soft fallback.")]
    public float transferMaxDuration = 6f;

    [Tooltip("Max Transfer when intent.run=1.")]
    public float runTransferMaxDuration = 4f;
    [Tooltip("Seconds reduction of walk transfer per 1 m/s target speed.")]
    public float walkCadenceTransferGain = 1.5f;
    [Tooltip("Minimum walk transfer duration under cadence scaling.")]
    public float walkCadenceMinTransfer = 1.0f;

    [Tooltip("Do not check ready earlier — intent=0 must reach BC.")]
    public float transferMinDuration = 0.7f;
    [Tooltip("If swing foot has landed, force leaving Transfer after this age, s.")]
    public float transferTouchdownMaxAge = 1.2f;

    [Tooltip("Both feet grounded and |CoM offset| ≤ this — hard ready.")]
    public float transferComOffsetMax = 0.10f;

    [Tooltip("Soft fallback after transferMaxDuration.")]
    public float transferFallbackComMax = 0.12f;

    // First split latches on BeginWalk so a later dip back inside the
    // plate does not drop standLeg and unwind the stance-ankle roll.
    private bool firstStepSplitLatched;

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

    private void EnterStance(float simTime, float stance)
    {
        currentStance = stance;
        phase = Phase.Stance;
        phaseEnterTime = simTime;
        swingWasAirborne = false;
        swingAirStreak = 0f;
        swingAirQualified = false;
        prevTickTime = -1f;
        SwapCount++;
        // Post-swap plant: TiltSettled + signed CoM-to-stance before the
        // next Unload. A plant outside that band used to split immediately
        // (burn_walk: 3 swaps / 0.835 s / 90°). First plant still uses
        // |lead| ≤ plantComLeadMax; later plants keep signed lead so a
        // catch behind the new sole is a stand. Quiet swap already in
        // the band never surrenders.
        if (walkRecoverAnyPose > 0.5f && useFallCatchWalk == 0
            && !RecoverStandReady())
        {
            SurrenderWalkToStand(simTime);
            return;
        }
        if (intent != null)
            intent.standLeg = currentStance;
    }

    // Swing sole sits at least FootLeadDeadzone ahead of the stance sole
    // along walk. Stacked feet (the 0.2 m shuffle) return false.
    private bool SwingFootIsAhead()
    {
        if (bodyState == null) return false;
        float stanceX = currentStance > 0.5f
            ? bodyState.rightFootPosition.x
            : bodyState.leftFootPosition.x;
        float swingX = currentStance > 0.5f
            ? bodyState.leftFootPosition.x
            : bodyState.rightFootPosition.x;
        return (swingX - stanceX) * WalkDir() >= FootLeadDeadzone;
    }

    private float AbsComOffset()
    {
        return comCalculator != null ? Mathf.Abs(comCalculator.GetCoMOffsetX()) : float.PositiveInfinity;
    }

    // WalkLift clock swap: the new sole must be a catch plate (ahead
    // of CoM and of the old stance). Mid-pair offset ≤ 0.05 / margin
    // 0.02 passed the 3 cm viewpoint lunge and dumped (burn_walk
    // swap 2). Do not use TiltSettled (12°) — plant's operating tilt
    // is already past that.
    private bool WalkLiftSwapSettled()
    {
        if (!BothFeetGrounded()) return false;
        if (!swingWasAirborne) return false;
        if (!SwingFootIsAhead()) return false;
        if (bodyState == null || comCalculator == null) return false;
        Vector2 com = comCalculator.GetCenterOfMass();
        Vector2 swing = currentStance > 0.5f
            ? bodyState.leftFootPosition
            : bodyState.rightFootPosition;
        if ((swing.x - com.x) * WalkDir() < FootLeadDeadzone)
            return false;
        return AbsBodyTilt() <= 20f;
    }

    private bool IsTransferReady()
    {
        if (bodyState == null || comCalculator == null)
            return false;
        if (!BothFeetGrounded())
            return false;
        if (walkTiltRecoverDeg > 0.001f && AbsBodyTilt() > walkTiltRecoverDeg)
            return false;
        // Mid-offset only: the "CoM over the next foot" threshold on a cold
        // first→left did not fire (walk_swap_soft2 swap=0). Soft entry
        // after EnterStance — dualSupportSwingScale in BalanceController.
        return AbsComOffset() <= transferComOffsetMax;
    }

    private bool IsTransferFallbackReady()
    {
        if (bodyState == null || comCalculator == null)
            return false;
        if (!BothFeetGrounded())
            return false;
        if (walkTiltRecoverDeg > 0.001f && AbsBodyTilt() > walkTiltRecoverDeg)
            return false;
        return AbsComOffset() <= transferFallbackComMax;
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

    // Standing Plant BeginWalk / restore / Plant→Fall wait share.
    // TiltSettled (12° / 40 °/s) + signed CoM-to-stance ≤ plantComLeadMax
    // (0.10 m) + the named stance sole down + not a sink. Cycle-0
    // PlantFallGateReady skips tilt and allows RecoverComLostMeters
    // 0.30 m so a 9° initiation can open Fall — that is live gait, not
    // a stand. CaptureColumnIsStanding froze a quiet 6° knee (phase 0 /
    // 35 s). CoM behind the stance foot is a catch, not a heap.
    // Happy-path numbers stay.
    private bool StandingPlantBandReady()
    {
        return StandingPlantPoseReady()
            && AbsBodyOmega() < RecoverOmegaSettledDeg;
    }

    // Standing band the recovery ladder waits on. PlantFallGateReady on
    // cycle 0 skips TiltSettled so a 9° initiation lean can open Fall;
    // that skip must not call a tipped BeginWalk a stand. Numbers are
    // the happy-path ones: 12° / 40 °/s and plantComLeadMax (0.10 m).
    // walkRecoverAnyPose 0 keeps the former tilt-skipped gate.
    // WalkLift first plant is SwapCount == 0 (FallCatchCycleCount stays 0).
    private bool RecoverStandReady()
    {
        if (walkRecoverAnyPose <= 0.5f)
            return PlantFallGateReady();
        return StandingPlantBandReady();
    }
}
