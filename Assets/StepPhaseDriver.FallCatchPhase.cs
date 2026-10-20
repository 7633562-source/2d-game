using UnityEngine;

public partial class StepPhaseDriver
{
    // Same leftover shin BalanceController commands in Plant (CatchReachShinDeg
    // at this Plant age). Capture here so Fall/Reach vault can start from it
    // after lastReachExtendProgress is zeroed and lastFallDuration belongs
    // to this Fall. catchPlantExitFold skips Max(Last) when Recover already
    // skipped Last: otherwise Plant→Fall yanks leftover against the plant-
    // clock shin Plant was still showing. Flag 0 / no Recover skip latch
    // is the former Max(Last, held).
    private float ComputePlantExitStanceShinDeg(float plantAge)
    {
        float flex = Mathf.Max(0f, reachKneeFlex);
        float fold = flex;
        if (fallSwingReachLiftClock > 0.5f)
        {
            float foldRamp = Mathf.Max(0.02f, fallKneeRampSeconds);
            float foldClock = Mathf.Clamp01(
                (lastFallDuration + lastReachDuration) / foldRamp);
            if (fallSwingFallFoldLead > 0.5f)
                foldClock = Mathf.Clamp01(foldClock * 2f);
            fold = flex * foldClock;
        }
        float start = Mathf.Clamp01(lastReachExtendProgress);
        float catchRamp = Mathf.Max(0.05f, catchMinDuration);
        bool plantClock = fallSwingCatchAirExtendHold > 0.5f;
        float held = plantClock
            ? lastCatchPlantDuration + lastRecoverDuration + plantAge
            : lastCatchDuration + lastRecoverDuration + plantAge;
        float clock = Mathf.Clamp01(held / catchRamp);
        bool skipLast = fallSwingCatchScrapeLiftAirCatchPlantExitFold > 0.5f
            && fallSwingPlantStanceExtendClock > 0.5f
            && ((fallSwingCatchScrapeLiftAirCatchEndFold > 0.5f
                    && RecoveredFromCatchScrapeAir)
                || (fallSwingCatchScrapeLiftAirCatchAirEndFold > 0.5f
                    && RecoveredFromCatchAir)
                || (fallSwingCatchScrapeLiftAirCatchAirEnterEndFold > 0.5f
                    && RecoveredFromCatchAirEnter)
                || (fallSwingCatchScrapeLiftAirCatchGroundCatchEndFold > 0.5f
                    && RecoveredFromCatchGroundCatch));
        float progress = skipLast ? clock : Mathf.Max(start, clock);
        float shin = Mathf.Lerp(fold, Mathf.Max(0f, reachKneeLandDeg), progress);
        return ApplyPlantMidStanceExtend(shin, plantAge);
    }

    // fallSwingPlantMidStanceExtend above. Roll-over fraction of the same
    // band Plant→Fall waits on, so the extension is a measured quantity and
    // not a clock: CoM still behind the plant reads 0, CoM at
    // plantComLeadMax reads 1. The age factor is the soft start — the same
    // shape walkPlantRockLead uses, and it is what keeps the command
    // continuous at Plant enter on a plant that landed with the CoM already
    // ahead of the foot (a step in a knee setpoint is a step in the knee
    // torque, which is the yank every ramp in this file exists to remove).
    public float PlantMidStanceExtendFraction(float plantAge)
    {
        if (fallSwingPlantMidStanceExtend <= 0.001f)
            return 0f;
        float leadBound = Mathf.Max(0.02f, plantComLeadMax);
        float roll = Mathf.Clamp01(StanceComLead() / leadBound);
        float ageClock = Mathf.Clamp01(
            plantAge / Mathf.Max(0.05f, plantMinDuration));
        return Mathf.Clamp01(fallSwingPlantMidStanceExtend) * roll * ageClock;
    }

    // BalanceController commands this shin in Plant and freezes the same
    // number at Plant→Fall, so both must read one schedule or the exit jumps
    // the knee back to landDeg. The 0.51° floor is not a fudge: the consumer
    // arms the whole plant pose on `stancePlantKnee > 0.5f`, and dropping
    // through it hands the strut to walkStanceKneeBend, which would ADD its
    // bend — a step up on the leg we just straightened. A leg configured
    // without a landing flex at all (reachKneeLandDeg 0) keeps the old
    // closed gate.
    public float ApplyPlantMidStanceExtend(float shinDeg, float plantAge)
    {
        if (shinDeg <= 0.5f)
            return shinDeg;
        float ext = PlantMidStanceExtendFraction(plantAge);
        if (ext <= 0.001f)
            return shinDeg;
        return Mathf.Max(0.51f, Mathf.Lerp(shinDeg, 0f, ext));
    }

    private void EnterFallCatchPhase(FallCatchPhase next, float simTime)
    {
        // Catch→Reach is a catch that found no plant. Reach→Catch is the
        // healthy exit and is not counted.
        if (fallCatchPhase == FallCatchPhase.Catch && next == FallCatchPhase.Reach)
            fallCatchBounceCount++;
        if (next == FallCatchPhase.Fall)
            fallCatchBounceCount = 0;
        if (fallCatchPhase == FallCatchPhase.Fall && next != FallCatchPhase.Fall)
            lastFallDuration = Mathf.Max(0f, simTime - phaseEnterTime);
        if (fallCatchPhase == FallCatchPhase.Reach && next != FallCatchPhase.Reach)
            lastReachDuration = Mathf.Max(0f, simTime - phaseEnterTime);
        if (fallCatchPhase == FallCatchPhase.Catch && next != FallCatchPhase.Catch)
        {
            lastCatchDuration = Mathf.Max(0f, simTime - phaseEnterTime);
            lastCatchPlantDuration = CatchPlantAge();
        }
        // Plant→Reach is the runaway escape: a catch step with no initiation
        // Fall. Everything Plant→Fall hands over belongs to the new swing
        // here too — and StandingRestore above all, because the placement
        // commands in BalanceController are gated on !StandingRestore and
        // the escape leaves a Plant whose gate had just raised it.
        bool plantEscapeReach = fallCatchPhase == FallCatchPhase.Plant
            && next == FallCatchPhase.Reach;
        // Freeze leftover before Fall zeros lastReachExtendProgress and
        // before this Fall overwrites lastFallDuration at the Reach gate.
        // The escape needs the same freeze: the stance shin is the leg the
        // body is standing on, and Reach reads that number as the vault
        // start (LastPlantStanceShinDeg), so losing it steps the knee.
        if (fallCatchPhase == FallCatchPhase.Plant
            && (next == FallCatchPhase.Fall || plantEscapeReach))
            lastPlantStanceShinDeg = ComputePlantExitStanceShinDeg(
                Mathf.Max(0f, simTime - phaseEnterTime));
        // Read the old phase before the assign: Catch→Reach must keep the
        // air clock that Catch was still extending. Fall→Reach still starts
        // a new window (early lift, or timeout with the foot down).
        // Catch timeout bounces on !planted (air or toe scrape). Air-only
        // resume let a scrape reset the clock and refold the shin on dirt.
        bool scrapeOk = fallSwingCatchReachScrapeResume > 0.5f
            ? !IsSwingFootPlanted()
            : !IsSwingFootGrounded();
        bool resumeReach = next == FallCatchPhase.Reach
            && fallSwingCatchReachResume > 0.5f
            && fallCatchPhase == FallCatchPhase.Catch
            && scrapeOk
            && reachAirEnterTime >= 0f;
        // Latch before the clear: Catch scrapeExtendAirAge already resumed
        // Last from lift-off. ReachResumedFromScrape is grounded-only, so
        // a timeout bounce after that lift-off Max'd Last with live
        // Progress (dirt counted as air) and Tick then wrote that jump.
        bool catchScraped = catchScrapedThisPhase;
        bool scrapeThenAirEnter = resumeReach
            && catchScraped
            && !IsSwingFootGrounded()
            && fallSwingCatchScrapeExtendHold > 0.5f
            && fallSwingCatchScrapeExtendAirAge > 0.5f
            && fallSwingCatchScrapeExtendAirEnterHold > 0.5f;
        // scrapeLiftAirHold already kept ThighHold through Catch air.
        // CatchScrapedThisPhase is cleared below, so the bounce would
        // take forceSwingFlex −swingHipFlex (sci_a). Latch that scrape
        // then air independently of the shin enterHold so lift can keep
        // ThighHold when extend flags are off. Quiet when Catch lift
        // already jumped (airLiftHold / scrapeLiftAirHold 0).
        // scrapeLiftAirCatchHold: that keep is Catch→Reach only.
        // ReachResumedFromCatchScrapeAir is cleared below, so the return
        // bounce would take −swingHipFlex. Latch independently of shin
        // so lift can keep ThighHold when extend flags are off. Quiet
        // when enterHold is 0 (Reach already jumped). Heel plant Catch
        // does not latch (landing hip). CatchResumedFromReachScrapeAir
        // also re-arms scrapeThenAirLiftEnter so a later Catch→Reach
        // does not lose the hip after CatchScrapedThisPhase stayed off.
        bool priorCatchFromReachScrapeAir = CatchResumedFromReachScrapeAir;
        bool scrapeThenAirLiftEnter = resumeReach
            && (catchScraped || priorCatchFromReachScrapeAir)
            && !IsSwingFootGrounded()
            && fallSwingCatchAirLiftHold > 0.5f
            && fallSwingCatchScrapeLiftAirHold > 0.5f
            && fallSwingCatchScrapeLiftAirEnterHold > 0.5f;
        bool scrapeThenAirCatchEnter = next == FallCatchPhase.Catch
            && fallCatchPhase == FallCatchPhase.Reach
            && ReachResumedFromCatchScrapeAir
            && !IsSwingFootPlanted()
            && fallSwingCatchAirLiftHold > 0.5f
            && fallSwingCatchScrapeLiftAirHold > 0.5f
            && fallSwingCatchScrapeLiftAirEnterHold > 0.5f
            && fallSwingCatchScrapeLiftAirCatchHold > 0.5f;
        // scrapeLiftAirCatchArm already kept reachArm through airborne
        // Catch. CatchScrapedThisPhase / CatchResumedFromReachScrapeAir
        // clear below, so Recover/Plant FallCatchArmCatchEnd would Lerp
        // reachArm→carry on stale Last against leftover shin. Latch
        // scrape then air (toe scrape + lift-off, or the return bounce)
        // through Recover→Plant. Heel-only Catch does not latch (no
        // catchArm keep). Quiet when catchArm is 0 (Catch already
        // Last-carried) or catchEndArm is 0. Recover/Plant shin skip
        // Last after this latch is catchEndFold. Catch planted scrape
        // shin skip Last is catchGroundCatchFold (catchGroundFold needs
        // !planted).
        bool scrapeThenAirCatchEnd = next == FallCatchPhase.Recover
            && fallCatchPhase == FallCatchPhase.Catch
            && fallSwingCatchScrapeLiftAirCatchEndArm > 0.5f
            && fallSwingCatchScrapeLiftAirCatchArm > 0.5f
            && fallSwingCatchScrapeLiftAirCatchFold > 0.5f
            && fallSwingCatchAirLiftHold > 0.5f
            && fallSwingCatchScrapeLiftAirHold > 0.5f
            && (priorCatchFromReachScrapeAir
                || (catchScraped && scrapeResumeAirEnterTime >= 0f));
        // scrapeLiftAirCatchAirArm already kept reachArm through true
        // airborne Catch. RecoveredFromCatchScrapeAir is scrape-only, so
        // Recover/Plant FallCatchArmCatchEnd would Lerp reachArm→carry
        // on stale Last against leftover shin. Latch never-scrape Catch
        // that actually flew (catchPlantEnterTime after Catch enter — heel
        // came down during Catch, so catchAirArm ran). Heel-only Catch
        // (planted at Catch enter) does not latch — planted Catch after
        // never-scrape Catch→Reach is RecoveredFromCatchAirEnter.
        // Recover/Plant shin skip Last after this latch is catchAirEndFold.
        // Quiet when catchAirArm is 0 (Catch already Last-carried) or catchArm is 0.
        bool neverScrapeAirCatchEnd = next == FallCatchPhase.Recover
            && fallCatchPhase == FallCatchPhase.Catch
            && fallSwingCatchScrapeLiftAirCatchAirEndArm > 0.5f
            && fallSwingCatchScrapeLiftAirCatchAirArm > 0.5f
            && fallSwingCatchScrapeLiftAirCatchArm > 0.5f
            && !catchScraped
            && !priorCatchFromReachScrapeAir
            && catchPlantEnterTime >= 0f
            && catchPlantEnterTime > phaseEnterTime + 0.0001f;
        // scrapeLiftAirCatchAirEnterGroundFold already kept Reach fold
        // through grounded never-scrape Reach. Heel plant then leaves
        // Reach for Catch already planted. RecoveredFromCatchAir needs
        // catchPlantEnterTime after Catch enter, so Recover/Plant
        // FallCatchArmCatchEnd would Lerp reachArm→carry on stale Last
        // against leftover shin. Latch that planted Catch enter
        // independently of catchAirEndArm so CatchEnd can skip Last
        // when the bounce already kept fold/arm. Catch-phase skip Last
        // on that planted Catch is catchAirEnterCatchArm (reads
        // CatchResumedFromReachAir; catchAirGroundArm needs CatchPlantAge
        // < PhaseAge). Catch-phase shin skip Last on that planted Catch
        // is catchAirEnterCatchFold (catchAirGroundFold needs !planted).
        // Recover/Plant shin skip Last after that Catch is
        // catchAirEnterEndFold (CatchReachExtendProgress still Max Last).
        // Quiet when
        // catchAirEnterGroundFold is 0 (shin already dropped) or
        // catchAirEnterGroundArm is 0 (arms already Last-carried) or
        // catchAirEnterEndArm is 0. Airborne Reach→Catch is
        // neverScrapeAirCatchEnd (catchAirArm still ran).
        bool priorCatchFromReachAir = CatchResumedFromReachAir;
        bool neverScrapeReachCatchEnter = next == FallCatchPhase.Catch
            && fallCatchPhase == FallCatchPhase.Reach
            && ReachResumedFromCatch
            && !ReachResumedFromCatchScrapeAir
            && IsSwingFootPlanted()
            && fallSwingCatchScrapeLiftAirCatchAirEnterEndArm > 0.5f
            && fallSwingCatchScrapeLiftAirCatchAirEnterGroundFold > 0.5f
            && fallSwingCatchScrapeLiftAirCatchAirEnterGroundArm > 0.5f
            && fallSwingCatchScrapeLiftAirCatchAirEnterArm > 0.5f
            && fallSwingCatchScrapeLiftAirCatchAirEnterFold > 0.5f
            && fallSwingCatchScrapeLiftAirCatchAirFold > 0.5f
            && fallSwingCatchScrapeLiftAirCatchAirArm > 0.5f
            && fallSwingCatchScrapeLiftAirCatchArm > 0.5f;
        bool neverScrapeEnterCatchEnd = next == FallCatchPhase.Recover
            && fallCatchPhase == FallCatchPhase.Catch
            && priorCatchFromReachAir
            && fallSwingCatchScrapeLiftAirCatchAirEnterEndArm > 0.5f
            && fallSwingCatchScrapeLiftAirCatchAirEnterGroundFold > 0.5f
            && fallSwingCatchScrapeLiftAirCatchAirEnterGroundArm > 0.5f
            && fallSwingCatchScrapeLiftAirCatchAirEnterArm > 0.5f
            && fallSwingCatchScrapeLiftAirCatchAirEnterFold > 0.5f
            && fallSwingCatchScrapeLiftAirCatchAirFold > 0.5f
            && fallSwingCatchScrapeLiftAirCatchAirArm > 0.5f
            && fallSwingCatchScrapeLiftAirCatchArm > 0.5f;
        // scrapeLiftAirCatchGroundArm already skipped Last-carry on
        // grounded scrape Catch, and catchGroundCatchFold skipped Last on
        // planted scrape shin. CatchScrapedThisPhase clears below, and
        // RecoveredFromCatchScrapeAir needs scrapeResumeAirEnterTime, so
        // never-air Recover/Plant FallCatchArmCatchEnd would Lerp
        // reachArm→carry on stale Last against leftover shin, and
        // CatchReachExtendProgress would Max Last (140°→~56°). Latch
        // scrape that planted without lift-off. Scrape-then-air (this
        // Catch or the return bounce) is scrapeThenAirCatchEnd. Quiet
        // when catchGroundArm is 0 (Catch already Last-carried) or
        // catchEndArm is 0. Recover/Plant shin skip Last after this latch
        // is catchGroundCatchEndFold.
        bool scrapeNeverAirCatchEnd = next == FallCatchPhase.Recover
            && fallCatchPhase == FallCatchPhase.Catch
            && fallSwingCatchScrapeLiftAirCatchEndArm > 0.5f
            && fallSwingCatchScrapeLiftAirCatchGroundArm > 0.5f
            && fallSwingCatchScrapeLiftAirCatchGroundFold > 0.5f
            && fallSwingCatchScrapeLiftAirCatchFold > 0.5f
            && fallSwingCatchAirLiftHold > 0.5f
            && fallSwingCatchScrapeLiftAirHold > 0.5f
            && catchScraped
            && !priorCatchFromReachScrapeAir
            && scrapeResumeAirEnterTime < 0f
            && IsSwingFootPlanted();
        fallCatchPhase = next;
        phaseEnterTime = simTime;
        ReachResumedFromCatch = resumeReach;
        ReachResumedFromScrape = resumeReach && (IsSwingFootGrounded() || scrapeThenAirEnter);
        ReachResumedFromCatchScrapeAir = scrapeThenAirLiftEnter;
        CatchResumedFromReachScrapeAir = scrapeThenAirCatchEnter;
        RecoveredFromCatchScrapeAir = scrapeThenAirCatchEnd;
        RecoveredFromCatchAir = neverScrapeAirCatchEnd;
        CatchResumedFromReachAir = neverScrapeReachCatchEnter;
        RecoveredFromCatchAirEnter = neverScrapeEnterCatchEnd;
        RecoveredFromCatchGroundCatch = scrapeNeverAirCatchEnd;
        catchScrapedThisPhase = false;
        if (next == FallCatchPhase.Reach)
        {
            reachEntryStanceComOffset = StanceComLead();
            scrapeResumeAirEnterTime = -1f;
            scrapeResumeExtendStart = 0f;
            if (resumeReach)
            {
                swingWasAirborne = true;
                // scrapeExtendEnterHold: Catch scrapeExtendHold already
                // froze shin/arm at Last. Max with live Progress here
                // writes the 70°→35° / carry that Catch just avoided.
                // Airborne bounce still Max's. Catch freeze off still
                // Max's so Last tracks the live shin Catch was showing.
                // scrapeExtendAirEnterHold: that keep is grounded-only.
                // After Catch scrape then lift-off, AirAge already resumed
                // Last; Max / the first Reach tick would still write live
                // Progress. Keep Last; latch ReachResumedFromScrape so Tick
                // continues from the bounce (not Catch air age). Flag 0 is
                // the former Max even after Catch scrape then air.
                bool keepLast = ReachResumedFromScrape
                    && fallSwingCatchReachScrapeExtendEnterHold > 0.5f
                    && fallSwingCatchScrapeExtendHold > 0.5f;
                if (scrapeThenAirEnter)
                    keepLast = true;
                if (!keepLast)
                    lastReachExtendProgress = Mathf.Max(
                        lastReachExtendProgress, ReachExtendProgress());
            }
            else
            {
                reachAirEnterTime = IsSwingFootGrounded() ? -1f : simTime;
                lastReachExtendProgress = 0f;
                if (reachAirEnterTime >= 0f)
                {
                    swingWasAirborne = true;
                    lastReachExtendProgress = ReachExtendProgress();
                }
            }
            // scrapeLiftAirCatchFold starts this clock on Catch enter so
            // fold extraAge is lift-off, not 0. Catch→Reach resets it
            // above; start it here after keepLast so enterFold extraAge
            // does not sit at 0 until the first Reach Tick (and so a
            // lift-only bounce, where ReachResumedFromScrape is off,
            // still ages). scrapeResumeExtendStart follows the Last
            // this bounce actually kept.
            if (scrapeThenAirLiftEnter
                && !IsSwingFootGrounded()
                && fallSwingCatchScrapeLiftAirEnterFold > 0.5f
                && scrapeResumeAirEnterTime < 0f)
            {
                scrapeResumeAirEnterTime = simTime;
                scrapeResumeExtendStart = lastReachExtendProgress;
            }
        }
        if (next == FallCatchPhase.Catch)
        {
            catchEntrySwingClearance = SwingFootClearance();
            catchPlantEnterTime = IsSwingFootPlanted() ? simTime : -1f;
            // Resume Catch may arrive airborne with the scrape-air clock
            // still -1 (Reach Tick starts it only when ReachResumedFromScrape).
            // Start it here so fold extraAge is lift-off age, not 0.
            // Heel plant does not.
            if (scrapeThenAirCatchEnter
                && !IsSwingFootGrounded()
                && fallSwingCatchScrapeLiftAirCatchFold > 0.5f
                && scrapeResumeAirEnterTime < 0f)
            {
                scrapeResumeAirEnterTime = simTime;
                scrapeResumeExtendStart = lastReachExtendProgress;
            }
        }
        else
            catchPlantEnterTime = -1f;
        if (next == FallCatchPhase.Fall || plantEscapeReach)
        {
            StandingRestore = false;
            lastReachExtendProgress = 0f;
            ReachResumedFromCatch = false;
            ReachResumedFromScrape = false;
            ReachResumedFromCatchScrapeAir = false;
            CatchResumedFromReachScrapeAir = false;
            RecoveredFromCatchScrapeAir = false;
            RecoveredFromCatchAir = false;
            CatchResumedFromReachAir = false;
            RecoveredFromCatchAirEnter = false;
            RecoveredFromCatchGroundCatch = false;
            scrapeResumeAirEnterTime = -1f;
            scrapeResumeExtendStart = 0f;
        }
        // The escape has no Fall behind it, so the swing lift and the stance
        // vault must clock off this Reach's own age. Inheriting the previous
        // cycle's Fall duration finishes the 0.15 s ramp before the first
        // tick — a step in the hip and knee commands on exactly the edge
        // this escape has to cross smoothly. The stance shin setpoint is
        // carried by lastPlantStanceShinDeg above, captured before this.
        if (plantEscapeReach)
            lastFallDuration = 0f;
        if (next == FallCatchPhase.Plant || next == FallCatchPhase.Fall)
        {
            // New support: the previous swing's air latch must not open
            // stance push / heel-strike on a foot that has not left the
            // ground this Plant. EnterStandingPlant / EnterFallCatchPlant
            // already clear the three; this is the EnterFallCatchPhase
            // path (and Plant→Fall, whose swing has not flown yet).
            swingWasAirborne = false;
            swingAirStreak = 0f;
            swingAirQualified = false;
        }
    }

    // Emergency: the step failed, stand on the same foot. No role swap and
    // no cycle count — those belong to a completed Recover plant. Plant→Fall
    // still uses TiltSettled + signed lead ≤ plantComLeadMax after this.
    private void EnterStandingPlant(float simTime)
    {
        // Emergency stand, including SinkCollapsed: leftover Fall/Reach/
        // Catch clocks belong to the failed step. Keeping lastFallDuration
        // (or catchPlantEnterTime) would feed ComputePlantExitStanceShinDeg
        // a fold ramp that this Plant never ran. Capture is for a completed
        // Recover→Plant (EnterFallCatchPlant), not this abort.
        lastFallDuration = 0f;
        lastReachDuration = 0f;
        lastCatchDuration = 0f;
        lastCatchPlantDuration = 0f;
        lastReachExtendProgress = 0f;
        lastRecoverDuration = 0f;
        lastRecoverStanceSign = 0f;
        lastPlantStanceShinDeg = 0f;
        reachEntryStanceComOffset = 0f;
        catchEntrySwingClearance = 0f;
        catchPlantEnterTime = -1f;
        fallCatchPhase = FallCatchPhase.Plant;
        phaseEnterTime = simTime;
        swingWasAirborne = false;
        swingAirStreak = 0f;
        swingAirQualified = false;
        reachAirEnterTime = -1f;
        scrapeResumeAirEnterTime = -1f;
        scrapeResumeExtendStart = 0f;
        prevTickTime = -1f;
        StandingRestore = true;
        PlantRolledFromBehind = IsComCaughtByStanceFoot();
        // Fresh Plant: the ladder gets a fresh deadline and its re-seat back.
        // The stall clock starts here — the failed step is exactly the pose
        // the gate is going to refuse.
        plantGateStallSince = simTime;
        plantReseated = false;
        plantStandSurrender = false;
        fallCatchBounceCount = 0;
        ReachResumedFromCatch = false;
        ReachResumedFromScrape = false;
        ReachResumedFromCatchScrapeAir = false;
        CatchResumedFromReachScrapeAir = false;
        RecoveredFromCatchScrapeAir = false;
        RecoveredFromCatchAir = false;
        CatchResumedFromReachAir = false;
        RecoveredFromCatchAirEnter = false;
        RecoveredFromCatchGroundCatch = false;
        catchScrapedThisPhase = false;
        // Failed step: if the old stance left the ground, stand on the
        // remaining loaded foot. Do not PickStanceFromFeet — a split
        // pose would hand stance to the front foot and look like a swap.
        HoldLoadedStance();
        if (intent != null)
            intent.standLeg = currentStance;
        // Failed Fall/Reach/Catch already past the abort band: two-foot
        // quiet stance now, not a 3 s split Plant. Mild stalls (Reach
        // grounded at 18°) still take the reseat / surrender ladder.
        if (RecoverPoseIsLost())
            SurrenderWalkToStand(simTime);
    }

    private void EnterFallCatchPlant(float simTime)
    {
        // Roles come from where the feet are, not from whose turn it is.
        // After a catch the leading foot carries the weight, so the trailing
        // one is the only leg free to reach. Alternating blindly hands the
        // swing role to the front foot and the body walks itself backwards.
        if (fallCatchPhase == FallCatchPhase.Recover)
            lastRecoverDuration = Mathf.Max(0f, simTime - phaseEnterTime);
        lastRecoverStanceSign = currentStance;
        currentStance = PickStanceFromFeet(-currentStance);
        PlantRolledFromBehind = IsComCaughtByStanceFoot();
        fallCatchPhase = FallCatchPhase.Plant;
        phaseEnterTime = simTime;
        swingWasAirborne = false;
        swingAirStreak = 0f;
        swingAirQualified = false;
        reachAirEnterTime = -1f;
        scrapeResumeAirEnterTime = -1f;
        scrapeResumeExtendStart = 0f;
        catchPlantEnterTime = -1f;
        prevTickTime = -1f;
        SwapCount++;
        FallCatchCycleCount++;
        StandingRestore = false;
        // A completed cycle: the ladder starts over on the new support.
        plantGateStallSince = -1f;
        plantReseated = false;
        plantStandSurrender = false;
        fallCatchBounceCount = 0;
        ReachResumedFromCatch = false;
        ReachResumedFromScrape = false;
        ReachResumedFromCatchScrapeAir = false;
        CatchResumedFromReachScrapeAir = false;
        CatchResumedFromReachAir = false;
        // RecoveredFromCatchScrapeAir / RecoveredFromCatchAir /
        // RecoveredFromCatchAirEnter / RecoveredFromCatchGroundCatch stay:
        // Plant still reads CatchEnd.
        catchScrapedThisPhase = false;
        if (intent != null)
            intent.standLeg = currentStance;
    }
}
