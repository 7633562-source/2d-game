using UnityEngine;

public partial class StepPhaseDriver
{
    // Diagnostic: CoM vs the landing foot. walk2_walk Recover floor is
    // 0.60 m — a step, not recoverComOffsetMax 0.16. Kept so the quantity
    // stays named; Plant no longer gates on it.
    private float AbsCatchFootComOffset()
    {
        if (bodyState == null || comCalculator == null)
            return float.PositiveInfinity;
        Vector2 com = comCalculator.GetCenterOfMass();
        float catchX = currentStance > 0.5f
            ? bodyState.leftFootPosition.x
            : bodyState.rightFootPosition.x;
        return Mathf.Abs(com.x - catchX);
    }

    // Live CoM drop below the settled standing bar (world Y). 0 when
    // no bar yet. Onset log / GETUP read this; not a force.
    public float StandingComDropMeters
    {
        get
        {
            if (comCalculator == null || !HasStandingComY)
                return 0f;
            if (comCalculator.TrackedBodyCount <= 0)
                return 0f;
            return StandingComY - comCalculator.GetCenterOfMass().y;
        }
    }
    public bool PelvisDroppedFromStand => !PelvisNearSpawnStand();
    // Failed catches since the last landing. Diagnostic for the ladder.
    public int FallCatchBounceCount => fallCatchBounceCount;

    // Stance/Transfer (or Plant/Fall/Reach vs Catch) without the WalkLift
    // overlay. PhaseCode reports Unload as 1 so CSV leaves 0; that overlay
    // is still this Stance clock. Using PhaseCode==0 as "still Stance"
    // reset the lift on the next 5 ms step (knee act 0.43, walkPhase only
    // −1/0, no single support).
    public bool WalkStanceClock
    {
        get
        {
            if (!walkActive)
                return false;
            if (useFallCatchWalk != 0)
                return fallCatchPhase <= FallCatchPhase.Reach;
            return phase == Phase.Stance;
        }
    }
    public float CurrentStanceSign => currentStance;
    // WALK-2 diagnostics: read-only, for the trial CSV. A phase code alone
    // does not say whether the fall was long enough, whether the body had
    // really passed the stance foot at Reach, and whether the swing ever
    // cleared the ground before the catch.
    public float LastFallDuration => lastFallDuration;
    public float LastCatchDuration => lastCatchDuration;
    public float LastReachDuration => lastReachDuration;
    // Last airborne Reach extend (0–1), frozen on heel plant so Catch does
    // not read holdFold=0. Recover stance sign is the pre-swap Catch identity:
    // Plant decay must not reverse the arms when roles flip.
    public float LastReachExtendProgress => lastReachExtendProgress;
    public bool SwingFootIsPlanted => IsSwingFootPlanted();
    // Toe down, heel still up. CatchReachExtendProgress reads this so
    // scrapeExtendHold (Catch) can freeze Last without also stalling a
    // true airborne extend. scrapeExtendAirAge (Catch) then resumes
    // Last from lift-off once this is false (not live Progress).
    public bool SwingFootIsGrounded => IsSwingFootGrounded();
    // Geometric gap past both contactOffsets. GetContacts can stay true
    // while bounds.min.y is already above the slab; cycle-0 Catch latches
    // the plant on this, not on !GetContacts alone.
    public bool SwingFootSeparated => SwingFootClearance() > ContactOffsetBand();
    // Cycle-0 Catch plant: swing ahead of the stance foot (not of CoM).
    public bool SwingFootAheadOfStance => IsSwingFootAheadOfStance();
    // After the first flight the plant that becomes stance must sit
    // ahead of CoM. AheadOfStance (2 cm) with the mass already past both
    // soles is a support the automaton names Falling.
    public bool SwingFootAheadOfCom => IsSwingFootAheadOfCom();
    public float LastRecoverDuration => lastRecoverDuration;
    public float LastRecoverStanceSign => lastRecoverStanceSign;

    // Catch scrape this phase (toe down, heel up). Balance reads this so
    // scrapeLiftAirHold can keep ThighHold after lift-off: forceSwingFlex
    // would otherwise take −swingHipFlex on the first airborne Catch frame.
    // Cleared on Catch exit. True airborne Catch never latches.
    public bool CatchScrapedThisPhase => catchScrapedThisPhase;

    public float LastCatchPlantDuration => lastCatchPlantDuration;
    // Air age after a scrape resume lift-off. scrapeLiftAirAge /
    // scrapeVaultAirAge / scrapeArmAirAge / scrapeExtendAirAge /
    // Catch scrapeExtendAirAge read this so fold / leftover vault /
    // arm extraAge / shin Last is time since the toe left, not PhaseAge
    // (dirt) and not 0 (stalled). 0 until that first !grounded frame.
    public bool ScrapeResumeAirActive => scrapeResumeAirEnterTime >= 0f;

    // Leftover stance shin (deg) frozen at Plant→Fall. Fall/Reach vault
    // starts from this instead of landDeg when fallSwingVaultFromLeftover
    // is on. Recomputing CatchReachShinDeg during Fall would use this
    // Fall's LastFallDuration after the Reach gate and drop the fold.
    public float LastPlantStanceShinDeg => lastPlantStanceShinDeg;
    public float ReachEntryStanceComOffset => reachEntryStanceComOffset;
    public float CatchEntrySwingClearance => catchEntrySwingClearance;

    public float ActiveStanceDurationNow() => ActiveStanceDuration();

    // Unused by WalkLift cadence after walklift_air: swap is Advance plant,
    // not the Stance clock. Kept for diagnostics.
    public bool WalkLiftClockDue()
    {
        if (phase != Phase.Stance) return false;
        float activeStance = Mathf.Max(0.1f, ActiveStanceDuration());
        if (!IsRunning() && SwapCount == 0)
            activeStance = Mathf.Min(activeStance, Mathf.Max(0.1f, firstWalkStanceDuration));
        return PhaseAge() >= activeStance;
    }

    public float ReachAirAge()
    {
        if (reachAirEnterTime < 0f)
            return 0f;
        return Mathf.Max(0f, lastTickTime - reachAirEnterTime);
    }

    // Stance: swing had at least one airborne frame this phase (BalanceController push gate).
    public bool SwingWasAirborne => swingWasAirborne;

    // Read-only for BalanceController. The catch exemption from the fall
    // automaton must not outlive the height a catch needs: a body already
    // on the slab has no stance left to reach from.
    public bool BodyCollapsed => SinkCollapsed();

    // GETUP-1: drop any walk-orbit peak so GetUpComAtStandHeight can
    // pass once the trunk is upright over the feet. spawnPelvisY stays
    // for PelvisNearSpawnStand. NotifyStoodUp / CaptureStandHeight
    // remeasure from the risen pose.
    public void ClearStandingComBarForGetUp()
    {
        standComHeightValid = false;
        standComHeight = 0f;
        spawnStandHeightValid = false;
        spawnStandHeight = 0f;
    }

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

    // Read-only view of that same band for BalanceController. The
    // double-support push-off may only run on a trunk this gate calls
    // settled: pushing an unsettled Plant is the fold the brake exists for.
    public bool PlantGateReady => PlantFallGateReady();

    // Standing band BeginWalk / restore wait on: TiltSettled (12° /
    // 40 °/s) + signed CoM-to-stance ≤ plantComLeadMax. No column
    // slack — that froze a quiet 6° knee. Happy-path numbers stay.
    public bool StandingPlantReady => RecoverStandReady();

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
}
