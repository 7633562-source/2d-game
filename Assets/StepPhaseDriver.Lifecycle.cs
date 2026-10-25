using UnityEngine;

public partial class StepPhaseDriver
{
    private void EnsureRefs()
    {
        if (intent == null)
            intent = GetComponent<MotionIntent>();
        if (bodyState == null)
            bodyState = GetComponent<BodyStateEstimator>();
        if (comCalculator == null)
            comCalculator = GetComponent<CenterOfMassCalculator>();
        if (balance == null)
            balance = GetComponent<BalanceController>();
        if (vestibular == null)
            vestibular = GetComponent<VestibularSystem>();
        if (humanBody == null)
            humanBody = GetComponent<Human>();
        if (pelvisTf == null)
            pelvisTf = transform.Find("Pelvis");
    }

    // In headless only HeadlessTrial calls Tick (before physics).
    // FixedUpdate is for Play Mode / ArmWalk.
    public bool externalDrive;

    void FixedUpdate()
    {
        if (HeadlessTrial.Active || externalDrive)
        {
            TryCaptureSpawnStandHeight();
            return;
        }
        TryCaptureSpawnStandHeight();
        if (pendingBegin)
        {
            walkStartTime = Time.fixedTime;
            pendingBegin = false;
            lastTickTime = Time.fixedTime;
            BeginCycle(Time.fixedTime);
        }
        if (!walkActive || intent == null) return;
        Tick(Time.fixedTime);
    }

    // Play/WebGL entry. Same pair as HeadlessTrial: BeginWalk then Tick.
    // The old pendingBegin path set walkActive without walkStartTime /
    // currentStance / standLeg until the next FixedUpdate.
    public void ArmWalk()
    {
        if (walkActive)
            return;
        BeginWalk(Time.fixedTime);
        Tick(Time.fixedTime);
    }

    public void BeginWalk(float simTime)
    {
        EnsureRefs();
        // Capture while walkActive is still false. Start() ran before
        // BuildHuman, and once walking the spawn bar must not be rewritten
        // from a heap. Quiet pose: this is the same live CoM FixedUpdate
        // stored during idle stance. Do not floor with the pelvis column.
        TryCaptureSpawnStandHeight();
        walkActive = true;
        pendingBegin = false;
        walkStartTime = simTime;
        lastTickTime = simTime;
        BeginCycle(simTime);
    }

    public void EndWalk()
    {
        walkActive = false;
        pendingBegin = false;
        walkStartTime = -1f;
        StandingRestore = false;
        plantStandSurrender = false;
        plantGateStallSince = -1f;
        plantReseated = false;
        if (intent != null)
            intent.standLeg = 0f;
    }

    private void BeginCycle(float simTime)
    {
        EnsureRefs();
        phase = Phase.Stance;
        fallCatchPhase = FallCatchPhase.Plant;
        phaseEnterTime = simTime;
        // Quiet stand: both feet in the 2 cm deadzone → firstStance, same
        // as the former hard assign. A start already on one loaded foot
        // keeps that foot instead of handing stance to a leg in the air.
        // Same geometric rule as a catch: stance is the leading foot, so
        // the trailer is the swing that steps FORWARD. Standing on the
        // trailer vs the lead did not change the sign of the first-step
        // fall (walkSwap 1, travelX −1.4 m, torso 90° back): DualLoad /
        // 55° / +10° pelvis plus then folded a still-planted swing, and
        // a planted trailing flexor is GRF −X. Comment 48 in
        // BalanceController. EnterFallCatchPlant still records a leading
        // swing as walking backward — do not pick the trailer here.
        currentStance = PickStanceFromFeet(firstStance);
        firstStepSplitLatched = false;
        // Split on this frame so the named stance ankle can roll CoM to
        // the toe. Waiting for Tick (or for ComLeftSupport) left both
        // ankles on mid-pair: 9 mm, then UpdateState 0.30 (burn_stag).
        // The STEP (WalkLift / geo-lift) still waits for ComLeftSupport.
        swingWasAirborne = false;
        swingAirStreak = 0f;
        swingAirQualified = false;
        prevTickTime = -1f;
        SwapCount = 0;
        TransferReadyFrames = 0;
        FallCatchCycleCount = 0;
        PlantRolledFromBehind = true;
        // CaptureWalk onset is DualSupport lean + one swing knee.
        // StandingPlantBandReady is TiltSettled + signed CoM-to-stance,
        // the Plant→Fall standing wait. Cycle-0 PlantFallGateReady
        // skips tilt / 0.30 m — that launched a tipped BeginWalk.
        // Column slack froze a quiet 6° knee (phase 0 / 35 s). Quiet
        // start (tilt 2.1°, |lead| ~0) still opens and walks. A heap
        // surrenders to two-foot Plant; Falling is not named while
        // restore is up. walkActive stays so the walker starts when
        // the gate opens.
        bool captureOn = balance != null && balance.useCaptureWalker != 0;
        bool coldGateReady;
        if (walkRecoverAnyPose > 0.5f)
            // CaptureWalk: pose-only standing band (tilt + lead), not
            // omega. StandingPlantBandReady also asks < 40 °/s; mixer
            // start on a quiet 2.1° trunk spikes past that, BeginWalk
            // raised StandingRestore, CaptureWalkActive stayed false,
            // DualSupport never ran (burn_walk 40 s / swap 0 / 0.0668 m).
            // WalkLift keeps the omega band. Cycle-0 PlantFallGateReady
            // skip still launches a 20° heap — do not use it here.
            coldGateReady = captureOn
                ? CaptureWalkColdStandReady()
                : StandingPlantBandReady();
        else if (captureOn)
            coldGateReady = true;
        else
            coldGateReady = PlantFallGateReady();
        StandingRestore = !coldGateReady
            && (useFallCatchWalk != 0 || walkRecoverAnyPose > 0.5f);
        // Cold start: the ladder clock runs from here, so a BeginWalk on a
        // pose that is not a stand gets the same deadline a failed step gets.
        plantGateStallSince = coldGateReady ? -1f : simTime;
        plantReseated = false;
        plantStandSurrender = false;
        fallCatchBounceCount = 0;
        standComHeightValid = false;
        standComHeight = 0f;
        if (coldGateReady)
            CaptureStandHeight();
        // Any starting pose the standing band refused: two-foot quiet
        // stance now. RecoverPoseIsLost already covered 30° / sink /
        // 0.30 m; the rest of the refused band (tilt 12–30°, |lead|
        // past plantComLeadMax) used to wait plantStallStandSeconds in
        // split Plant — one-leg on a tipped trunk is the fold. Quiet
        // start never raises restore (tilt 2.1°, |lead| ~0).
        // plantMinDuration / TiltSettled / plantComLeadMax stay.
        if (StandingRestore)
            SurrenderWalkToStand(simTime);
        else if (balance != null && balance.useCaptureWalker != 0)
        {
            // Capture walker owns the support foot. Writing standLeg
            // here is a second gait on the same hinges for one Tick.
            if (intent != null)
                intent.standLeg = 0f;
        }
        else
            WriteWalkSplitLeg();
        lastFallDuration = 0f;
        lastCatchDuration = 0f;
        lastCatchPlantDuration = 0f;
        catchPlantEnterTime = -1f;
        lastReachDuration = 0f;
        lastReachExtendProgress = 0f;
        lastRecoverDuration = 0f;
        lastRecoverStanceSign = 0f;
        lastPlantStanceShinDeg = 0f;
        reachEntryStanceComOffset = 0f;
        catchEntrySwingClearance = 0f;
        reachAirEnterTime = -1f;
        scrapeResumeAirEnterTime = -1f;
        scrapeResumeExtendStart = 0f;
        catchScrapedThisPhase = false;
        ReachResumedFromCatch = false;
        ReachResumedFromScrape = false;
        ReachResumedFromCatchScrapeAir = false;
        CatchResumedFromReachScrapeAir = false;
        RecoveredFromCatchScrapeAir = false;
        RecoveredFromCatchAir = false;
        CatchResumedFromReachAir = false;
        RecoveredFromCatchAirEnter = false;
        RecoveredFromCatchGroundCatch = false;
    }
}
