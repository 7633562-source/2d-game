using UnityEngine;

public partial class StepPhaseDriver
{
    // WALK-2 fall–catch cycle. This writes intent and phase only; the pose
    // per phase lives in BalanceController.ComputeFallCatchBalance.
    private void TickFallCatchWalk(float simTime)
    {
        float phaseAge = simTime - phaseEnterTime;
        prevTickTime = simTime;
        // Only the Plant branch below raises it; every other phase clears it,
        // so the Play overlay can name a roll-over hold instead of showing the
        // same silent pause a missed key press looks like.
        PlantWaitingForLead = false;

        // Collapse the tilt aborts cannot see: the body folds downward with
        // CoM still over the feet, so every gate in the machine reads calm
        // while the pelvis goes to the ground. Height says otherwise. Any
        // phase drops straight to a two-foot standing Plant — the swing
        // fold is exactly what is pulling the body down, so keeping the
        // split pose would be the collapse, not the cure.
        // Cycle 0 Fall/Reach/Catch after lift-off is the first step, not a
        // heap: the 55° trail fold plus 24° lean already sit near
        // sinkAbortDrop 0.25 m. Skipping that abort only while the swing
        // is still in the air was the Reach→Plant reset: the plant frame
        // lifted the exemption, EnterStandingPlant wrote standLeg 0, both
        // hips took the quiet pelvis-upright loop, and the pelvis dropped
        // ~0.65 m in 0.4 s (walkSwap 0). Keep the exemption through Catch /
        // Recover after that first flight. A grounded squat that never
        // left still aborts. After a completed Recover the landing Plant is
        // the same height — do not surrender it for plantMinDuration.
        bool firstStepAfterAir = FallCatchCycleCount == 0
            && (swingWasAirborne || reachAirEnterTime >= 0f || !IsSwingFootGrounded())
            && (fallCatchPhase == FallCatchPhase.Fall
                || fallCatchPhase == FallCatchPhase.Reach
                || fallCatchPhase == FallCatchPhase.Catch
                || fallCatchPhase == FallCatchPhase.Recover);
        bool landingPlantHold = fallCatchPhase == FallCatchPhase.Plant
            && FallCatchCycleCount > 0
            && !StandingRestore
            && !plantStandSurrender
            && phaseAge < Mathf.Max(0.05f, plantMinDuration);
        if (SinkCollapsed()
            && !firstStepAfterAir
            && !landingPlantHold
            && (fallCatchPhase != FallCatchPhase.Plant || !plantStandSurrender))
        {
            EnterStandingPlant(simTime);
            plantStandSurrender = true;
            intent.standLeg = 0f;
            return;
        }

        switch (fallCatchPhase)
        {
            case FallCatchPhase.Plant:
                // Stand with the landing brake until CoM is back over the
                // stance foot. A later cadence pass swapped TiltSettled
                // for tilt-below-abort and Plant fell while CoM was still
                // 0.38 m past support (walk2_blend2 folded, torso 102°).
                // Fall is allowed only when the forward lead is under
                // plantComLeadMax (0.10 m) and the trunk is inside the
                // 12° / 40 °/s band; otherwise ApplyRecoverBrake keeps
                // holding.
                // The lead is signed on purpose. A catch plants the new
                // stance foot half a step AHEAD of CoM, so right after the
                // role swap the lead is negative — the body stands behind
                // its own support, which is the safest place a fall can
                // start from. Taking |lead| rejected exactly that pose:
                // in walk2_restore3 the feet split to 0.27 m apart, the
                // lead sat at −0.133 m, and Plant never reopened — one
                // cycle, swing foot down 0.96 of the run, travel −0.065 m.
                // Only CoM running past the foot may block the next fall.
                // Happy-path numbers stay: plantMinDuration, plantComLeadMax,
                // TiltSettled. plantMinDuration alone is not "unsettled" —
                // the first quiet Plant must keep identity ankle for 0.5 s.
                // The standing band (TiltSettled + signed lead + stance
                // grounded) is: if it fails, raise StandingRestore so
                // cycle-0 Plant still brakes (tilt 12–30° with CoM 0.38 m
                // past used to sit here with no CoP restore and fold).
                // BeginCycle applies the same gate so a cold BeginWalk
                // does not wait a Tick to raise the brake.
                float stanceBefore = currentStance;
                HoldLoadedStance();
                if (IsComCaughtByStanceFoot())
                    PlantRolledFromBehind = true;
                if (currentStance != stanceBefore)
                {
                    phaseEnterTime = simTime;
                    StandingRestore = true;
                    // A different foot is a different lead: the ladder gets
                    // its re-seat back on the new support.
                    plantReseated = false;
                    WriteWalkSplitLeg();
                    break;
                }
                bool plantGateReady = PlantFallGateReady();
                bool recoverReady = RecoverStandReady();
                if (StandingRestore)
                {
                    // Restore Plant: do not clear the stall on the loose
                    // cycle-0 PlantFallGateReady (tilt skipped). That is
                    // the hole that EnterFall'd a tipped BeginWalk on the
                    // first Tick. Wait for TiltSettled + CoM-to-stance.
                    if (recoverReady)
                    {
                        if (plantClearRestoreOnGate > 0.5f)
                        {
                            // Restart Plant so initiation lean /
                            // plantMinDuration start from a stand, not
                            // from the restore age. Happy path never
                            // enters here (StandingRestore was false).
                            ReleaseStandingPlantGate(simTime);
                            WriteWalkSplitLeg();
                            break;
                        }
                        plantGateStallSince = -1f;
                        plantReseated = false;
                        CaptureStandHeight();
                    }
                    else if (plantGateStallSince < 0f)
                        plantGateStallSince = simTime;
                }
                else if (plantGateReady)
                {
                    plantGateStallSince = -1f;
                    plantReseated = false;
                    CaptureStandHeight();
                    // plantClearRestoreOnGate above. Cycle-0 still has no
                    // trailing push (FallCatchCycleCount > 0); this only
                    // unblocks rock / A2 / plantHipExtend after a flicker.
                    if (plantClearRestoreOnGate > 0.5f)
                        StandingRestore = false;
                }
                else
                {
                    if (plantGateStallSince < 0f)
                        plantGateStallSince = simTime;
                    StandingRestore = true;
                }
                // Uncaught plant (walkRunawayKeepStepping): the mass entered
                // this Plant already past the new sole, so the phase has
                // nothing to catch with. Everything below is a stand — the
                // CoP brake inside a 0.26 m sole, the re-seat, then the
                // two-foot surrender — and with the CoM outside both soles
                // a stand is the topple, not the cure (swap at ~7 s, then
                // 65° and 90° forward). Throw the trailing leg instead:
                // Reach places it ahead of the capture point (it already
                // aims at ActiveCatchFootLead on later cycles), Catch and
                // Recover swap onto it, and the walk continues into the next
                // cycle. Reach, not Fall: Fall is initiation — swing fold
                // plus forward CoP on the stance — which is the fold this
                // phase exists to prevent, and its gate is shut here anyway.
                if (!plantStandSurrender
                    && phaseAge >= Mathf.Max(0f, plantRunawayStepSeconds)
                    && KeepSteppingOnRunaway())
                {
                    EnterFallCatchPhase(FallCatchPhase.Reach, simTime);
                    break;
                }
                // Standing again: give the split stance plantMinDuration to
                // ramp standLegLevel back up before the next Fall, instead
                // of falling on the frame the surrender ends. Height must
                // have come back too — the gate reads calm on a folded body,
                // so releasing on the gate alone would hand the split pose
                // back and the sink guard would re-arm it the next tick
                // (standLeg flapping at 200 Hz).
                if (plantStandSurrender && recoverReady && !SinkCollapsed())
                {
                    plantStandSurrender = false;
                    phaseEnterTime = simTime;
                    WriteWalkSplitLeg();
                    break;
                }
                // Abort-level Plant: do not wait plantStallStandSeconds.
                // Fall abort used to land here at 30° with the split
                // brake for 3 s; that is the fold. Quiet Plant never
                // sees RecoverPoseIsLost (tilt 1.4°, |lead| ~0).
                // Cycle-0 restore that is not yet a stand (12–30°,
                // |lead| past plantComLeadMax): same surrender. Split
                // Plant for 3 s on a 20° BeginWalk is the fold. Happy
                // path never raises restore. Later cycles keep the
                // reseat ladder / KeepSteppingOnRunaway.
                if (!plantStandSurrender && (RecoverPoseIsLost()
                    || (walkRecoverAnyPose > 0.5f
                        && FallCatchCycleCount == 0
                        && StandingRestore
                        && !recoverReady)))
                {
                    SurrenderWalkToStand(simTime);
                    break;
                }
                float plantStallAge = plantGateStallSince < 0f
                    ? 0f
                    : simTime - plantGateStallSince;
                // Restore Plant is a shut gate even when cycle-0
                // PlantFallGateReady skips TiltSettled. Otherwise a
                // 25° BeginWalk never arms reseat / two-foot surrender.
                bool ladderOpen = StandingRestore ? recoverReady : plantGateReady;
                if (!ladderOpen
                    && !plantReseated
                    && plantStallReseatSeconds > 0.001f
                    && plantStallAge >= plantStallReseatSeconds
                    && TryReseatStanceToLoadedFoot())
                {
                    phaseEnterTime = simTime;
                    StandingRestore = true;
                    plantReseated = true;
                    WriteWalkSplitLeg();
                    break;
                }
                if (!ladderOpen
                    && plantStallStandSeconds > 0.001f
                    && plantStallAge >= plantStallStandSeconds
                    && AnyFootGrounded())
                    plantStandSurrender = true;
                WriteWalkSplitLeg();
                // plantMinDuration is the earliest a fall may start, not the
                // moment it should. Later cycles wait for the extrapolated
                // CoM past fallComLeadTrigger (FallLeadReady). Cycle 0
                // fires when CoM is ON the leading stance foot (StanceLead
                // ≥ 0) — Capture ≥ trigger is 1.5 cm past that foot and
                // never grows (comment 27). Standing on the trailer made
                // this true on tick 1 and lifted the lead foot backward.
                // Comment 28: do not fade
                // ankleScale to get there; plantRockLead is that loop.
                // Comment 30: that rock is not × RecoverBlend. Comment
                // 31: the 9° Plant lean is not × RecoverBlend either and
                // is not faded on Capture. Comment 32: that lean on a
                // pinned trailing sole is a bow (hips back, CoM at mid).
                // The trail CoM loop (heel) stays until StanceLead ≥ 0.
                // Comment 33: pelvisLean is a bow; 9° is lumbar only.
                // Comment 34: hip-lock + pelvis-track left StanceLead at
                // −1.5 cm (two contacts, overconstrained). Comment 35:
                // trailing-hip extend as a DrivePelvis counter is cancelled
                // by same-hip P. Comment 36: trail leaves DrivePelvis;
                // the leading "pivot" was a hipFriction 12 weld (0 DOF).
                // Comment 37: both hips SPD-extend to the same plus pose
                // so the parallelogram can shear. Comment 38: that pose
                // still sat in a 0-DOF chain (trail hip K=12, ankle K=3);
                // torsoLean 9° with DrivePelvis off is the bow. Comment
                // 39: same +8° joint target on a free pelvis is a sit
                // (thighs stay vertical, pelvis nods, StanceLead stays
                // −1.5 cm). Comment 40: trail holds WORLD thigh tilt
                // (sit leaves the thigh at 0). Leading DrivePelvis
                // flexed the lead to un-nod the pelvis (a sit).
                // Comment 41: BOTH hips leave DrivePelvis and hold the
                // SAME WORLD thigh tilt. Hip+ankle dampers still snap
                // to tau K; torsoLean waits with pelvisLean. Comment
                // 42: plantRockLead + dual CoM ankle stay at 0 (shin
                // couple); WORLD pose is not gated on trailLead. Comment
                // 43: those hip poses sit (internal couple) and
                // plantRockLead was gated off cycle 0. Restore the ankle
                // CoP + lumbar lean; DrivePelvis stays the pivot. Comment
                // 44: pelvisLean is the WORLD target that pivot holds
                // (inverted pendulum). Lumbar-only left ~1.35 cm; ankle
                // K=3 froze the shin. Cycle 0 writes pelvisLean and snaps
                // only the ankle dampers. Comment 45: that target is still
                // a bow. Cycle 0 Plant SPD-holds the quiet hip snapshot
                // (DrivePelvis off) so the snapped ankles are the hinge;
                // plantRockLead is the CoP. Trunk lean waits. Trailing
                // A2 stays later-cycle. Comment 46: that lock froze the
                // standing parallelogram. Cycle 0 Plant WORLD-holds both
                // thighs at the same clockwise tilt and snaps hip+ankle
                // dampers. Comment 47: keep plantRockLead on those
                // snapped ankles (zeroing them left only the hip couple).
                // Freeze the world angle once StanceLead ≥ 0 so
                // DrivePelvis cannot unwind the 1.5 cm before plantAged
                // EnterFall.
                bool plantAged = phaseAge >= Mathf.Max(0.05f, plantMinDuration);
                bool plantLeadReady = FallLeadReady(phaseAge);
                // Cycle 0: ComLeftSupport is the trigger, not plantMinDuration
                // (that wait is the +1.30 s dump). Later cycles keep the
                // aged capture / plantLeadWait deadline.
                bool plantGo = FallCatchCycleCount == 0
                    ? plantLeadReady
                    : (plantAged && plantLeadReady);
                PlantWaitingForLead = plantGateReady && !plantGo
                    && !StandingRestore && !plantStandSurrender;
                // StandingRestore must actually be standing (TiltSettled
                // + CoM-to-stance) before Fall. plantClearRestoreOnGate 0
                // latches restore until this edge; recoverReady is that
                // latch's release. Happy-path cycle 0 never raised restore.
                if (plantGateReady && plantGo
                    && !plantStandSurrender
                    && (!StandingRestore || recoverReady))
                    EnterFallCatchPhase(FallCatchPhase.Fall, simTime);
                break;

            case FallCatchPhase.Fall:
                intent.standLeg = currentStance;
                if (fallAbortDeg > 0.001f
                    && phaseAge >= 0.05f
                    && AbsBodyTilt() >= fallAbortDeg)
                {
                    // Later cycles: same stance, Recover brake. Recover here
                    // used to catchAnkleOnSwing on the trailing foot and then
                    // swap roles — a missed catch, not a landing.
                    // Cycle 0: 30° is the initiation fall, not a failed
                    // step. Plant restore here is walkSwap=0 / travelX=0.
                    // Air: Reach (placement), not Catch with nothing ahead.
                    // Later cycles used to StandingPlant at 30°: after an
                    // uncaught swap that is RecoverPoseIsLost → surrender
                    // while CoM is already past the new toe (90° in 1 s).
                    // Air still Reach; a plant already ahead of CoM is Catch.
                    if (FallCatchCycleCount == 0)
                        EnterFallCatchPhase(
                            IsSwingFootGrounded()
                                ? FallCatchPhase.Catch
                                : FallCatchPhase.Reach,
                            simTime);
                    else if (!IsSwingFootGrounded())
                        EnterFallCatchPhase(FallCatchPhase.Reach, simTime);
                    else if (IsSwingFootAheadOfCom() && IsFirstStepPlanted())
                        EnterFallCatchPhase(FallCatchPhase.Catch, simTime);
                    else
                        EnterStandingPlant(simTime);
                    break;
                }
                if (!IsSwingFootGrounded())
                {
                    swingWasAirborne = true;
                    // Air is the Reach window: extend the shin forward and
                    // put the foot ahead of stance. Cycle 0 used to skip
                    // that and Catch immediately — Catch then had nothing
                    // ahead to plant, SinkCollapsed Planted, walkSwap=0.
                    EnterFallCatchPhase(FallCatchPhase.Reach, simTime);
                    break;
                }
                // Knee ramp finished and the foot is still down: Reach so
                // the fold can keep unloading. Cycle 0 used to Catch here
                // (a 1.5 s grounded Reach stall); after the foot actually
                // leaves, that skip is the same missed placement.
                if (phaseAge >= Mathf.Max(0.05f, fallKneeRampSeconds))
                    EnterFallCatchPhase(FallCatchPhase.Reach, simTime);
                break;

            case FallCatchPhase.Reach:
                intent.standLeg = currentStance;
                if (!IsSwingFootGrounded())
                {
                    swingWasAirborne = true;
                    if (reachAirEnterTime < 0f)
                        reachAirEnterTime = simTime;
                    // scrapeLiftHold / scrapeVaultHold / scrapeArmHold
                    // zero extraAge while the toe is down. Start the air
                    // clock on the first lift-off so scrapeLiftAirAge /
                    // scrapeVaultAirAge / scrapeArmAirAge / scrapeExtendAirAge
                    // can finish the leftover fold / leftover→0 / ~0.5→1 /
                    // 70°→35° without counting scrape PhaseAge.
                    // scrapeLiftAirEnterFold reads the same clock: ReachResumedFromScrape
                    // is extend-flag-gated, so a lift-only bounce would
                    // leave extraAge at 0 for the whole Reach.
                    if ((ReachResumedFromScrape
                            || (ReachResumedFromCatchScrapeAir
                                && fallSwingCatchScrapeLiftAirEnterFold > 0.5f))
                        && scrapeResumeAirEnterTime < 0f)
                    {
                        scrapeResumeAirEnterTime = simTime;
                        scrapeResumeExtendStart = lastReachExtendProgress;
                    }
                }
                // Freeze extend on a heel plant (ReachExtendProgress returns
                // 0 there). Catch must start from the last airborne value,
                // not from holdFold, or the arm clock restarts at 1.
                bool holdFold = IsSwingFootGrounded();
                if (fallSwingScrapeExtend > 0.5f && HeelPlantOn())
                    holdFold = IsSwingHeelLoaded();
                // scrapeExtendHold: Catch→Reach on a toe scrape already
                // froze fold extraAge. Updating Last here from the still-
                // ticking air clock would finish 70°→35° and lerp arms
                // toward carry on dirt. Airborne resume still records.
                // scrapeExtendAirAge: that freeze then held Last after
                // the toe left. Write Last from lift-off (not live
                // Progress: scrapeExtend counted dirt as air).
                bool scrapeHold = ReachResumedFromScrape
                    && fallSwingCatchReachScrapeExtendHold > 0.5f;
                if (!holdFold)
                {
                    if (scrapeHold
                        && fallSwingCatchReachScrapeExtendAirAge > 0.5f
                        && !IsSwingFootGrounded())
                        lastReachExtendProgress = ScrapeResumeExtendProgress();
                    else if (!scrapeHold)
                        lastReachExtendProgress = ReachExtendProgress();
                }
                // Catch on a still-grounded swing UNDER the hip runs
                // catchKnee × 0.15 and straightens the 140° fold
                // (swingFoot 0.96). That is why a scrape with no air must
                // not Catch. A foot already AHEAD of CoM is the plant
                // Catch itself judges (IsSwingFootAheadOfCom). Cycle 0
                // never has that number: DualLoadMinSpan is 4 cm of X,
                // stacked feet start at 0, and µ=1.2 will not slide the
                // sole under the hip. The first step's plant is the swing
                // AHEAD OF STANCE (the span PickStanceFromFeet needs to
                // hand the next support to the front foot), even if lean
                // already walked CoM past both soles.
                // Air-ahead used to Catch at 0.05 s (`!airborne || firstStep`).
                // Catch then cannot Recover (needs swingDown) and at catchMax
                // bounced Reach: Reach Catchs again, torso −71°, walkSwap 0.
                // Catch / Recover swap on a planted forward foot. Stay in
                // Reach until that plant; timeout / trunk abort Catch only
                // later cycles (a missed air catch must not freeze).
                bool trunkGone = (fallAbortDeg > 0.001f && AbsBodyTilt() >= fallAbortDeg)
                    || (reachOmegaAbort > 0.001f && AbsBodyOmega() >= reachOmegaAbort);
                bool firstStep = FallCatchCycleCount == 0;
                // Plant is heel-first (fallSwingHeelPlant): a toe scrape after
                // air is not Catch on later cycles. Cycle 0 geo-lift lands
                // on the toe, so grounded-after-air is the plant.
                bool plantedAfterAir = IsFirstStepPlanted() && reachAirEnterTime >= 0f;
                bool airborne = !IsSwingFootGrounded();
                // After air the capture hip can put the sole in front of
                // CoM. Catching on mere contact (`firstStep && plantedAfterAir`)
                // swapped onto a 2 cm AheadOfStance plant while the mass
                // was already past that sole — EnterFallCatchPlant then
                // named Falling (offset > fallCoMOffset) and the trunk
                // went 90° in two seconds. Stacked dirt never flew: keep
                // AheadOfStance there. After air wait for AheadOfCom.
                bool placedAhead = CatchFootIsReady() && IsFirstStepPlanted();
                if ((placedAhead && (plantedAfterAir || phaseAge >= 0.05f))
                    || (airborne && phaseAge >= ActiveReachTimeout() && !firstStep)
                    || (trunkGone && phaseAge >= 0.05f && IsFirstStepPlanted() && !firstStep))
                    EnterFallCatchPhase(FallCatchPhase.Catch, simTime);
                // Cycle 0: the first sole-down after Reach air is Catch, not
                // a standing Plant. trunkGone here was Reach→Plant on the
                // plant frame (lean already near fallAbortDeg), standLeg 0,
                // both hips DrivePelvis, walkSwap 0. Later cycles still abort.
                // Runaway: a scraping sole with the trunk past the abort band
                // is the middle of the catch, not a failed step. Hand it to
                // Catch (which judges the heel) instead of standing on a
                // foot the CoM has already passed.
                else if (!airborne && phaseAge >= 0.05f && trunkGone && !firstStep)
                {
                    if (KeepSteppingOnRunaway())
                        EnterFallCatchPhase(FallCatchPhase.Catch, simTime);
                    else
                        EnterStandingPlant(simTime);
                }
                // Cycle 0: a grounded Reach that never made the span still
                // Catchs. EnterStandingPlant here was the walkSwap=0 path —
                // Plant restore cancelled the Fall push-off and the 35°
                // geo fold sat in a split squat until the body dropped.
                // Later cycles keep the stall: a missed catch on a body
                // that already travelled must not swap onto the trail foot.
                else if (!airborne
                    && reachAirEnterTime < 0f
                    && reachGroundStallSeconds > 0.001f
                    && phaseAge >= reachGroundStallSeconds)
                {
                    if (FallCatchCycleCount == 0)
                        EnterFallCatchPhase(FallCatchPhase.Catch, simTime);
                    else
                        EnterStandingPlant(simTime);
                }
                break;

            case FallCatchPhase.Catch:
                // Keep the swing identity. standLeg=0 dropped splitLegs, so
                // catchKnee, forceSwingFlex and the landing ankle never ran,
                // and the trunk went to 90° with both hips holding quiet stance.
                intent.standLeg = currentStance;
                bool swingDown = IsFirstStepPlanted();
                if (swingDown && catchPlantEnterTime < 0f)
                    catchPlantEnterTime = simTime;
                // scrapeExtendHold freezes Catch shin at Last on a toe
                // scrape. scrapeExtend still ticks (heel-up is air), so a
                // later lift-off Max'd Last with live Progress and jumped
                // 70°→35°. Latch the scrape; start the air clock on first
                // lift-off; write Last so Catch→Reach inherits. Flag 0 is
                // the former live Progress even in air. scrapeLiftAirHold
                // latches the same scrape without extendHold so ThighHold
                // can keep the hip after lift-off. scrapeLiftAirEnterHold
                // then latches ReachResumedFromCatchScrapeAir on timeout
                // bounce so Catch→Reach does not take −swingHipFlex.
                // scrapeLiftAirCatchHold then latches
                // CatchResumedFromReachScrapeAir on the return bounce so
                // Reach→Catch does not take −swingHipFlex.
                // scrapeLiftAirCatchFold then keeps the Reach fold on that
                // same airborne Catch (no 70°→35°). scrapeLiftAirCatchArm
                // then keeps reachArm on that same airborne Catch (no
                // Last-carry). scrapeLiftAirCatchEndArm then starts
                // Recover/Plant CatchEnd from that reachArm (Last is stale
                // after Catch flags clear). scrapeLiftAirCatchGroundArm
                // then skips Last on grounded scrape Catch. scrapeLiftAirCatchAirArm
                // then keeps reachArm on true airborne Catch (never scraped).
                // scrapeLiftAirCatchAirEndArm then starts Recover/Plant CatchEnd
                // from that reachArm (scrape latch is scrape-only).
                // scrapeLiftAirCatchAirGroundArm then skips Last on grounded
                // never-scrape Catch (catchAirArm is air-only).
                // scrapeLiftAirCatchAirFold then keeps that fold on true
                // airborne Catch (catchFold is scrape-only).
                // scrapeLiftAirCatchAirEnterFold then keeps that fold on
                // never-scrape Catch→Reach (enterFold is scrape-only).
                // scrapeLiftAirCatchAirEnterArm then keeps reachArm on
                // that same bounce (enterArm is scrape-only).
                // scrapeLiftAirCatchAirEnterGroundArm then keeps reachArm
                // after the toe meets dirt (enterGroundArm is scrape-only).
                // scrapeLiftAirCatchAirEnterGroundFold then keeps that fold
                // on dirt (enterGroundFold is scrape-only).
                // scrapeLiftAirCatchAirEnterEndArm then starts Recover/Plant
                // CatchEnd from reachArm after that bounce's planted Catch
                // (RecoveredFromCatchAir needs heel-down during Catch).
                // scrapeLiftAirCatchAirEnterCatchArm then skips Last on
                // that planted Catch (catchAirGroundArm needs CatchPlantAge
                // < PhaseAge).
                // scrapeLiftAirEnterFold then
                // keeps that fold on Catch→Reach (Last is stale).
                // scrapeLiftAirEnterGroundFold then keeps that fold after
                // the toe meets dirt (enterFold is air-only).
                // scrapeLiftAirCatchGroundFold then keeps that fold after
                // the Catch toe meets dirt (catchFold is air-only).
                // Start the air clock even when extendAirAge is off so
                // fold extraAge does not count scrape PhaseAge.
                // Quiet when both Holds are 0 (no latch). True airborne
                // Catch never latches.
                if (IsSwingFootGrounded() && !swingDown
                    && (fallSwingCatchScrapeExtendHold > 0.5f
                        || (fallSwingCatchAirLiftHold > 0.5f
                            && fallSwingCatchScrapeLiftAirHold > 0.5f)))
                    catchScrapedThisPhase = true;
                bool scrapeAirClock = catchScrapedThisPhase
                    || CatchResumedFromReachScrapeAir;
                bool foldAge = scrapeAirClock
                    && fallSwingCatchScrapeLiftAirCatchFold > 0.5f
                    && fallSwingCatchAirLiftHold > 0.5f
                    && fallSwingCatchScrapeLiftAirHold > 0.5f;
                bool extendAirAge = catchScrapedThisPhase
                    && fallSwingCatchScrapeExtendAirAge > 0.5f;
                if (scrapeAirClock
                    && !IsSwingFootGrounded()
                    && (extendAirAge || foldAge))
                {
                    if (scrapeResumeAirEnterTime < 0f)
                    {
                        scrapeResumeAirEnterTime = simTime;
                        scrapeResumeExtendStart = lastReachExtendProgress;
                    }
                    if (extendAirAge)
                        lastReachExtendProgress = ScrapeResumeExtendProgress();
                }
                bool ahead = CatchFootIsReady();
                float catchMin = Mathf.Max(0.05f, catchMinDuration);
                float catchMax = Mathf.Max(catchMin, catchMaxDuration);
                // Recover only after a forward plant (moment arm ≥ catchFootLead)
                // and a hold so the ankle can saturate. Timeout still needs the
                // foot down: leaving Catch in the air hands a rotating trunk
                // to Recover with no plant. A scrape behind CoM used to
                // unstick into Recover (role swap) and fold; it now returns
                // to standing Plant on the same foot.
                // All three Catch exits below need the foot down. When the
                // body goes prone both feet leave the ground and the FSM had
                // no exit at all: walk2_blend2 sat in Catch from 6.79 s to
                // 35 s and the 104° torso happened inside that frozen phase.
                // No plant to catch on → reach again, do not freeze.
                // Cycle 0 already ahead in the air: bounce Reach is the
                // hang. Reach Catchs on the same ahead flag, Catch times
                // out, torso −71°, Recover never runs, walkSwap 0. Stay
                // until the sole plants; plantedAhead below then Recovers.
                if (!swingDown && phaseAge >= catchMax
                    && !(FallCatchCycleCount == 0 && ahead))
                {
                    // Airborne Catch past the window used to bounce Reach↔Catch
                    // while the trunk was already gone (walk2_blend2 freeze).
                    // Later cycles: torso past abort → standing Plant.
                    // Cycle 0 used to stay in Catch with no plant (the first
                    // swing never got a Reach window). That hang is walkSwap=0
                    // then SinkCollapsed Plant. Air still BEHIND → Reach.
                    // Runaway: both stands below are the topple (the mass is
                    // already outside the soles), so bounce Reach and keep
                    // hunting for a plant ahead of the capture point.
                    bool catchKeepStepping = KeepSteppingOnRunaway();
                    if (FallCatchCycleCount != 0
                        && !catchKeepStepping
                        && fallAbortDeg > 0.001f && AbsBodyTilt() >= fallAbortDeg)
                        EnterStandingPlant(simTime);
                    else if (FallCatchCycleCount != 0
                        && !catchKeepStepping
                        && fallCatchBounceMax > 0.5f
                        && fallCatchBounceCount >= fallCatchBounceMax)
                        EnterStandingPlant(simTime);
                    else
                        EnterFallCatchPhase(FallCatchPhase.Reach, simTime);
                    break;
                }
                bool plantedAhead = swingDown && ahead;
                bool catchTimeout = phaseAge >= catchMax && swingDown;
                // Trunk already arrested (tilt back under the abort band)
                // and the swing is down: do not wait out catchMax while CoM
                // vaults over a jammed plantarflexor (walk2_walk: 0.22→0.55 s
                // of Catch grew |catchOff| 0.19→0.60 m and dropped the pelvis
                // 0.5 m). Recover then has no 0.16 m band left to wait for.
                bool trunkCaught = phaseAge >= catchMin && swingDown && TiltSettled();
                // Cycle 0: the latched 10°/10° plant IS the step. catchMin
                // 0.22 s was sized for a real fall-catch ankle saturate;
                // waiting it on a µ=1.2 sole lets initiation lean run past
                // sinkAbort. RecoverTouchdownMinAge (0.05 s) is the hold.
                // Later cycles keep catchMin so a scrape does not swap.
                // Runaway: `ahead` here is IsSwingFootAheadOfCom, i.e. the
                // foot is planted past the capture point — that is the real
                // catch, not a scrape, and holding it out for 0.22 s spends
                // the arrest on falling. Recover's own 0.05 s touchdown hold
                // stays the debounce.
                bool catchReady = plantedAhead
                    && (FallCatchCycleCount == 0
                        || phaseAge >= catchMin
                        || KeepSteppingOnRunaway());
                if (catchReady || trunkCaught)
                    EnterFallCatchPhase(FallCatchPhase.Recover, simTime);
                else if (catchTimeout)
                {
                    // Cycle 0: swap only on a forward plant. Timeout Recover
                    // without span flipped two stacked soles (walkSwap with
                    // travelX=0). Stay in Catch so the 4 cm placement can
                    // finish; sinkAbort is the heap exit. Later cycles keep
                    // the scrape-to-Plant path (a trail-foot swap is the fold).
                    if (FallCatchCycleCount == 0 && swingDown && ahead)
                        EnterFallCatchPhase(FallCatchPhase.Recover, simTime);
                    // Runaway: the sole is down but behind the mass, so this
                    // is a missed catch, not a landing — reach again instead
                    // of standing on a foot the CoM has already left.
                    else if (FallCatchCycleCount != 0 && KeepSteppingOnRunaway())
                        EnterFallCatchPhase(FallCatchPhase.Reach, simTime);
                    else if (FallCatchCycleCount != 0)
                        EnterStandingPlant(simTime);
                }
                break;

            case FallCatchPhase.Recover:
                // Keep splitLegs. standLeg=0 ended the landing-foot ankle
                // loop on the first Recover frame, so the 0.10 m CoP brake
                // never ran while the trunk was still past 20°.
                intent.standLeg = currentStance;
                if (IsTransferReady())
                    TransferReadyFrames++;
                // Plant on contact + trunk arrest, not on CoM-over-catch.
                // comOnCatch (|CoM − catch foot| ≤ recoverComOffsetMax 0.16)
                // never fires: walk2_walk Recover sat 28 s at |catchOff| ≥
                // 0.60 m. The catch ankle is already on the +45° stop, so
                // ProtectJointLimit zeroes plantarflex and the offset cannot
                // shrink into 0.16 m. The soft brake is in
                // BalanceController Recover/Plant: fade CoP as tilt/omega drop
                // so the ankle leaves the stop, hips take the trunk, then
                // the next Fall does not add forward CoP while tipped.
                // TiltSettled also waits for |omega| < RecoverOmegaSettledDeg
                // (walk2_blend planted on tilt alone at 55 deg/s).
                // recoverHold stays RecoverTouchdownMinAge (0.05 s): the
                // contact window is ~0.06 s (walk2_walk). A longer gate
                // misses the plant and returns the 0-cycle stall.
                // Plant on a sole that can be the next support. Stacked
                // dirt (never flew) still swaps on AheadOfStance — µ=1.2
                // cannot grow catchFootLead vs a CoM that initiation
                // already walked past both soles. After air the capture
                // hip owns the landing: swap on AheadOfCom so the new
                // stance sits in front of the mass and GetCoMOffsetX
                // stays inside fallCoMOffset. Waiting for the 5 cm
                // caught band (lead ≤ plantCaughtLeadMax) was swap=0 on
                // an 8°/6° retract; that snap is no longer the air plant.
                bool plantReady = IsFirstStepPlanted() && CatchFootIsReady();
                if (phaseAge >= RecoverTouchdownMinAge && plantReady)
                {
                    EnterFallCatchPlant(simTime);
                    break;
                }
                // Airborne Recover used to wait transferMax (~6 s) while the
                // trunk went prone. Abort as soon as Fall would: same stance,
                // no swap. Happy-path Recover has the catch foot down in
                // ~0.05 s and never hits this.
                // Cycle 0: 30° is still the initiation fall. Abort-to-Plant
                // here cancelled the swap the moment Catch delivered the
                // plant (lean is already ~24°, abort 30°), torso then −71°.
                // Stay until plantReady or transferMax; later cycles abort.
                // Runaway: the abort band is crossed inside every catch that
                // starts from an uncaught plant, and standing there ends the
                // walk (standLeg 0 with the CoM past both soles). Let Recover
                // wait for the plant it is here for; the deadline below is
                // still the exit.
                if (FallCatchCycleCount != 0
                    && fallAbortDeg > 0.001f && phaseAge >= 0.05f
                    && AbsBodyTilt() >= fallAbortDeg && !plantReady
                    && !KeepSteppingOnRunaway())
                {
                    EnterStandingPlant(simTime);
                    break;
                }
                if (phaseAge >= ActiveTransferMaxDuration() && plantReady)
                    EnterFallCatchPlant(simTime);
                else if (phaseAge >= ActiveTransferMaxDuration() && !plantReady)
                {
                    // Same reason: a deadline with no plant is another reach,
                    // not a stand, while the mass is still running.
                    if (KeepSteppingOnRunaway())
                        EnterFallCatchPhase(FallCatchPhase.Reach, simTime);
                    else
                        EnterStandingPlant(simTime);
                }
                break;
        }
    }
}
