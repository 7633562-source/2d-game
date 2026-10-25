using UnityEngine;

public partial class StepPhaseDriver
{
    // scrapeLiftAirCatchAirEnterFold already keeps Reach fold through
    // never-scrape Catch→Reach. Catch→Reach still Lerps reachArm→carry
    // on Last: enterArm requires ReachResumedFromCatchScrapeAir, so
    // timeout/abort Catch with the toe still up then bouncing to Reach
    // is ~vault→carry against leftover shin — the Catch→Reach shoulder
    // pair of catchAirEnterFold, a quiet trunk against a folded shin.
    // Keep reachArm (skip the extend Lerp) until the toe meets dirt.
    // extraAge 0 on Catch; the bounce already continues reachArm on Reach
    // PhaseAge (armAge). Heel plant still Lerps. Grounded never-scrape
    // Reach Last-carry is catchAirEnterGroundArm (enterGroundArm is
    // scrape-only). Quiet when catchAirEnterFold is 0 (shin already
    // dropped) or catchAirArm is 0 (Catch already Last-carried). Stance
    // ankle / pelvis / −swingHipFlex / enterArm / catchAirArm /
    // catchAirEnterFold unchanged. 0 = former Last-carry on never-scrape
    // Catch→Reach.
    // CLI -fallSwingCatchScrapeLiftAirCatchAirEnterArm.
    [Tooltip("True airborne Catch, Catch→Reach: 1 = keep reachArm (no Last-carry). 0 = former Lerp(reachArm, carry, extend) on the never-scrape bounce.")]
    public float fallSwingCatchScrapeLiftAirCatchAirEnterArm = 1f;

    // scrapeLiftAirCatchAirEnterArm already keeps reachArm through
    // never-scrape Catch→Reach in air. Grounded never-scrape Reach still
    // Lerps reachArm→carry on Last: enterGroundArm requires
    // ReachResumedFromCatchScrapeAir, so timeout/abort Catch with the toe
    // still up then bouncing to Reach then toe-down is ~vault→carry
    // against leftover shin — the dirt sibling of catchAirEnterArm.
    // Keep reachArm (skip the extend Lerp) on dirt too.
    // ReachResumedFromCatch survives the toe-down (cleared on Reach exit).
    // extraAge 0 on Catch; bounce continues reachArm on Reach PhaseAge.
    // Heel plant still leaves Reach for Catch. Grounded never-scrape
    // Reach fold after that keep is catchAirEnterGroundFold
    // (enterGroundFold is scrape-only). Quiet when catchAirEnterArm is 0
    // (air Reach already Last-carried) or catchAirEnterFold is 0 (shin
    // already dropped) or catchAirArm is 0 (Catch already Last-carried).
    // Stance ankle / pelvis / −swingHipFlex / enterGroundArm /
    // catchAirEnterArm / catchAirEnterFold unchanged. 0 = former
    // Last-carry on grounded never-scrape Reach.
    // CLI -fallSwingCatchScrapeLiftAirCatchAirEnterGroundArm.
    [Tooltip("True airborne Catch, grounded Reach: 1 = keep reachArm (no Last-carry). 0 = former Lerp(reachArm, carry, extend) on toe-down after never-scrape Catch→Reach.")]
    public float fallSwingCatchScrapeLiftAirCatchAirEnterGroundArm = 1f;

    // scrapeLiftAirCatchAirEnterGroundArm already keeps reachArm through
    // grounded never-scrape Reach. Grounded never-scrape Reach still Lerps
    // fold→landDeg on Last: enterGroundFold requires
    // ReachResumedFromCatchScrapeAir, so timeout/abort Catch with the toe
    // still up then bouncing to Reach then toe-down is 140°→~56° against
    // leftover shin — the shin pair of catchAirEnterGroundArm, a keep
    // that catchAirEnterFold still holds only in air. Keep the Reach fold
    // (skip the extend Lerp) on dirt too. ReachResumedFromCatch survives
    // the toe-down (cleared on Reach exit). extraAge 0 on Catch; bounce
    // continues fold on Reach PhaseAge (liftAge). Heel plant still leaves
    // Reach for Catch. Quiet when catchAirEnterFold is 0 (shin already
    // dropped in air) or catchAirEnterGroundArm is 0 (arms already
    // Last-carried on dirt) or catchAirEnterArm is 0 or catchAirArm is 0.
    // Stance ankle / pelvis / −swingHipFlex / enterGroundFold /
    // catchAirEnterFold / catchAirEnterGroundArm unchanged. 0 = former
    // landDeg lerp on grounded never-scrape Reach.
    // CLI -fallSwingCatchScrapeLiftAirCatchAirEnterGroundFold.
    [Tooltip("True airborne Catch, grounded Reach: 1 = keep Reach fold (no 70°→35°). 0 = former Lerp(fold, landDeg, extend) on toe-down after never-scrape Catch→Reach.")]
    public float fallSwingCatchScrapeLiftAirCatchAirEnterGroundFold = 1f;

    // scrapeLiftAirCatchAirEnterGroundFold already keeps Reach fold through
    // grounded never-scrape Reach. Heel plant then leaves Reach for Catch
    // already planted. Recover/Plant still call FallCatchArmCatchEnd, which
    // Lerps FallCatchArmReachStart→carry on Last: RecoveredFromCatchAir
    // requires catchPlantEnterTime after Catch enter (heel came down during
    // Catch, so catchAirArm ran). Planted-at-enter Catch after the bounce
    // has catchPlantEnterTime == phaseEnterTime, so CatchEnd is
    // ~vault→carry against leftover shin — the Recover/Plant shoulder
    // pair of catchAirEnterGroundArm. Skip that Last Lerp and start
    // CatchEnd from reachArm; plant-clock arrive still blends toward carry
    // once the heel has sat catchMin. Recover/Plant decay from that
    // CatchEnd is unchanged. Catch-phase skip Last on that planted Catch
    // is catchAirEnterCatchArm (catchAirGroundArm needs CatchPlantAge <
    // PhaseAge). Quiet when catchAirEnterGroundFold is 0 (shin already dropped on
    // dirt Reach) or catchAirEnterGroundArm is 0 (arms already Last-carried
    // on dirt Reach) or catchAirEndArm is 0 (direct never-scrape Catch
    // already skipped). Stance ankle / pelvis / −swingHipFlex /
    // catchAirEndArm / catchAirGroundFold unchanged. 0 = former Last-carry
    // in Recover/Plant after never-scrape Catch→Reach→Catch.
    // CLI -fallSwingCatchScrapeLiftAirCatchAirEnterEndArm.
    [Tooltip("True airborne Catch, Catch→Reach→Catch Recover/Plant: 1 = CatchEnd from reachArm (no Last-carry). 0 = former Lerp(reachArm, carry, Last) in FallCatchArmCatchEnd after planted Catch enter.")]
    public float fallSwingCatchScrapeLiftAirCatchAirEnterEndArm = 1f;

    // scrapeLiftAirCatchAirEnterCatchFold already skips Last on planted
    // Catch after never-scrape Catch→Reach. Recover/Plant still
    // Max(Last, held+PhaseAge): Last is the Reach freeze at Catch enter
    // (fold was kept, Last still holds Reach extend), so Catch skip
    // starts fold on plant-clock then Recover Max Last yanks 140°→~56°
    // against leftover shin — the Recover/Plant shin pair of
    // catchAirEnterEndArm, continuing catchAirEnterCatchFold. Skip Last
    // and continue plant-clock (LastCatchPlantDuration+PhaseAge /
    // catchMin; Plant adds LastRecoverDuration). Quiet when
    // catchAirEnterCatchFold is 0 (Catch already Max'd Last) or
    // catchAirEnterEndArm is 0 (CatchEnd already Last-carried). Stance
    // ankle / pelvis / −swingHipFlex / catchAirEnterCatchFold /
    // catchAirEnterEndArm unchanged. Scrape Recover/Plant shin still
    // Max Last (RecoveredFromCatchScrapeAir; CatchReachExtendProgress)
    // is catchEndFold. 0 = former Max(Last, held+PhaseAge)
    // in Recover/Plant after never-scrape Catch→Reach→Catch.
    // CLI -fallSwingCatchScrapeLiftAirCatchAirEnterEndFold.
    [Tooltip("True airborne Catch, Catch→Reach→Catch Recover/Plant shin: 1 = skip Last (continue plant-clock fold→landDeg). 0 = former Max(Last, held+PhaseAge) in CatchReachExtendProgress Recover/Plant.")]
    public float fallSwingCatchScrapeLiftAirCatchAirEnterEndFold = 1f;

    // scrapeLiftAirCatchEndArm already skips Last-carry on Recover/Plant
    // after scrape then air. CatchReachExtendProgress Recover/Plant still
    // Max(Last, held+PhaseAge): Last is the Catch-enter freeze (or AirAge
    // resume), not the catchFold / catchGroundFold override, so Recover
    // Max Last yanks 140°→~56° against leftover shin — the Recover/Plant
    // shin pair of catchEndArm. Skip Last and continue plant-clock
    // (LastCatchPlantDuration+PhaseAge / catchMin; Plant adds
    // LastRecoverDuration). Quiet when catchEndArm is 0 (CatchEnd
    // already Last-carried) or catchFold is 0 (shin already dropped) or
    // catchGroundFold is 0 (shin already dropped on dirt). Stance ankle /
    // pelvis / −swingHipFlex / catchAirEnterEndFold / catchEndArm
    // unchanged. Catch planted scrape shin skip Last is
    // catchGroundCatchFold (catchGroundFold needs !planted). Never-scrape
    // air Recover/Plant shin skip Last is catchAirEndFold. 0 =
    // former Max(Last, held+PhaseAge) in Recover/Plant after scrape then
    // air. CLI -fallSwingCatchScrapeLiftAirCatchEndFold.
    [Tooltip("Catch scrape then air, Recover/Plant shin: 1 = skip Last (continue plant-clock fold→landDeg). 0 = former Max(Last, held+PhaseAge) in CatchReachExtendProgress Recover/Plant.")]
    public float fallSwingCatchScrapeLiftAirCatchEndFold = 1f;

    // scrapeLiftAirCatchGroundCatchFold already skips Last on planted scrape
    // Catch. CatchReachExtendProgress Recover/Plant still Max(Last,
    // held+PhaseAge): CatchScrapedThisPhase clears on Catch exit, and
    // RecoveredFromCatchScrapeAir needs lift-off, so never-air Recover
    // has no latch. Last is the Catch-enter freeze, not the
    // catchGroundCatchFold override, so Catch skip starts fold on
    // plant-clock then Recover Max Last yanks 140°→~56° against leftover
    // shin — the Recover/Plant shin pair of catchGroundArm. Skip Last and
    // continue plant-clock (LastCatchPlantDuration+PhaseAge / catchMin;
    // Plant adds LastRecoverDuration). FallCatchArmCatchEnd skips
    // Last-carry on the same latch (RecoveredFromCatchGroundCatch;
    // catchEndArm). Quiet when catchGroundCatchFold is 0 (Catch already
    // Max'd Last) or catchEndArm is 0 (Catch already Last-carried) or
    // catchGroundArm is 0. Stance ankle / pelvis / −swingHipFlex /
    // catchGroundCatchFold / catchEndFold unchanged. Scrape-then-air
    // Recover/Plant is catchEndFold. Plant→Fall leftover skip Last after
    // this Recover skip is catchPlantExitFold. MixHipHold (08:56)
    // already shipped. 0 = former Max(Last, held+PhaseAge) in
    // Recover/Plant after scrape never-air. CLI
    // -fallSwingCatchScrapeLiftAirCatchGroundCatchEndFold.
    [Tooltip("Catch scrape then plant, Recover/Plant shin: 1 = skip Last (continue plant-clock fold→landDeg). 0 = former Max(Last, held+PhaseAge) in CatchReachExtendProgress Recover/Plant after never-air scrape.")]
    public float fallSwingCatchScrapeLiftAirCatchGroundCatchEndFold = 1f;

    // scrapeLiftAirCatchAirEndArm already skips Last-carry on Recover/Plant
    // after never-scrape air Catch. CatchReachExtendProgress Recover/Plant
    // still Max(Last, held+PhaseAge): Last is the Reach freeze at Catch
    // enter (fold was kept by catchAirFold / catchAirGroundFold, Last
    // still holds Reach extend), so Catch skip starts fold on plant-clock
    // then Recover Max Last yanks 140°→~56° against leftover shin — the
    // Recover/Plant shin pair of catchAirEndArm. Skip Last and continue
    // plant-clock (LastCatchPlantDuration+PhaseAge / catchMin; Plant adds
    // LastRecoverDuration). Quiet when catchAirEndArm is 0 (CatchEnd
    // already Last-carried) or catchAirFold is 0 (shin already dropped
    // in air) or catchAirGroundFold is 0 (shin already dropped on dirt).
    // Stance ankle / pelvis / −swingHipFlex / catchAirEnterEndFold /
    // catchEndFold / catchAirEndArm unchanged. Catch planted never-scrape
    // shin skip Last is catchAirGroundCatchFold (catchAirGroundFold needs
    // !planted). Plant→Fall leftover skip Last after this Recover skip is
    // catchPlantExitFold. MixHipHold (08:56) already shipped. 0 = former
    // Max(Last, held+PhaseAge) in Recover/Plant after never-scrape air
    // Catch. CLI -fallSwingCatchScrapeLiftAirCatchAirEndFold.
    [Tooltip("True airborne Catch, Recover/Plant shin: 1 = skip Last (continue plant-clock fold→landDeg). 0 = former Max(Last, held+PhaseAge) in CatchReachExtendProgress Recover/Plant.")]
    public float fallSwingCatchScrapeLiftAirCatchAirEndFold = 1f;

    // plantStanceExtendClock continues leftover through Plant.
    // ComputePlantExitStanceShinDeg still Max(Last, held/catchMin) at
    // Plant→Fall: Last is the Reach freeze (or Catch-enter freeze), so
    // Catch/Recover/Plant skip-Last plant-clock then Fall vault Max Last
    // yanks leftover 140°→~56° / fold→landDeg on the only loaded heel —
    // the Plant-exit pair of catchEndFold / catchAirEndFold /
    // catchAirEnterEndFold. Skip Last and continue plant-clock
    // (LastCatchPlantDuration+LastRecoverDuration+plantAge / catchMin).
    // Quiet when plantStanceExtendClock is 0 (Plant already jumped) or
    // the matching Recover skip is 0. MixHipHold (08:56) already shipped.
    // Scrape never-air Recover/Plant skip Last is catchGroundCatchEndFold
    // (RecoveredFromCatchGroundCatch). 0 = former Max(Last, held) at
    // Plant→Fall. CLI -fallSwingCatchScrapeLiftAirCatchPlantExitFold.
    [Tooltip("Plant→Fall leftover: 1 = skip Last (continue plant-clock fold→landDeg). 0 = former Max(Last, held) at Plant exit.")]
    public float fallSwingCatchScrapeLiftAirCatchPlantExitFold = 1f;

    // catchExtendClock continues Reach leftover through Catch/Recover.
    // Recover then swaps (EnterFallCatchPlant): that shin is now stance,
    // and Plant still commanded landDeg (plantStanceKnee). Recover can
    // leave at RecoverTouchdownMinAge (0.05 s) while leftover still ramps
    // on catchMin (0.22 s) — leftover ~0.23 then jump fold→landDeg on the
    // only loaded heel, the catchLandKnee yank one phase later. Continue
    // leftover on LastCatchPlantDuration+LastRecoverDuration+PhaseAge;
    // once arrived, landDeg holds as before. Happy-path leftover 1 is
    // still landDeg. Fall vault from leftover is fallSwingVaultFromLeftover
    // (plantMin 0.50 s usually finishes the 0.22 s remainder; a short
    // plantMin does not). 0 = former instant landDeg.
    // CLI -fallSwingPlantStanceExtendClock.
    [Tooltip("Plant after a catch swap: 1 = continue Catch leftover shin on the new stance. 0 = former instant landDeg.")]
    public float fallSwingPlantStanceExtendClock = 1f;

    // Vault and lift now both finish at 2×. The 140° fold still shared
    // the 0.15 s ramp: at 0.075 s vault=1 and lift=1 while fold is only
    // 70° of 140°, so the swing chain is still long and the foot stays
    // on dirt. Double that fold (still a ramp, not instant 140°, not
    // sci_a 40°, not 3× vault/lift). Reach / Catch continue that clock
    // on the frozen Fall+Reach age — a Fall-only multiply snaps
    // 140°→70° on Fall→Reach. Quiet when 0: former same-clock fold.
    // CLI -fallSwingFallFoldLead.
    [Tooltip("Fall/Reach fold: 1 = 140° knee at 2× ramp clock (short chain with lift). 0 = former same-clock fold.")]
    public float fallSwingFallFoldLead = 1f;

    // "Caught" is not "CoM behind the ankle". The sole runs from the heel at
    // −0.065 m to the toe at +0.195 m, so a CoM a few centimetres ahead of
    // the ankle is still inside the base of support and the ankle still has
    // a moment arm to roll it. Demanding lead ≤ 0 asked Recover to wait
    // for a plant BEHIND the mass, which a short first step cannot be, and
    // the CoP-only arrest never walks an 8°/6° plant into 5 cm either:
    // torso calms, swap stays 0, then 93° forward. Recover swaps on
    // contact + AheadOfStance. This band is the Plant roll latch and the
    // CoP arrest dead zone, so plantRock does not add forward CoP until
    // the mass is on the sole, and the brake stops where the latch stops
    // asking. 0 restores the former lead ≤ 0 on those two.
    [Tooltip("Signed CoM lead (m) over a landing sole that still counts as caught: the Plant roll latch and the CoP arrest share it. Recover swap does not wait for it. 0 = former CoM-behind-the-ankle latch.")]
    public float plantCaughtLeadMax = 0.05f;

    // Ceiling on the lead-arrest half of the Recover/Plant CoP brake, as a
    // fraction of catchComLead. A full −catchComLead (0.10 m) puts the
    // landing ankle on its +45° stop, where ProtectJointLimit zeroes the
    // command and the brake stops existing. Half of it keeps the ankle off
    // the stop and leaves the trunk term (blendBack − blend) the rest of the
    // range. 0 = no lead arrest (CoP from the trunk term only), 1 = the full
    // brake on a position error.
    [Tooltip("Cap on the CoM-lead arrest in the Recover/Plant CoP brake, as a fraction of catchComLead. 0 = off, 1 = full brake on position alone.")]
    public float plantArrestCopScale = 0.5f;

    // The swap gate is contact + AheadOfStance, so a plant can legitimately
    // land only FootLeadDeadzone (2 cm) ahead of the old sole while the mass
    // is already running. That plant cannot arrest anything: the new stance
    // has no moment arm against a CoM whose capture point is past the toe,
    // and the recovery ladder then hands both legs to the quiet two-foot
    // controller with the CoM outside both soles — the 65° / 90° forward
    // topple one second after the swap. A fall that far along is caught by
    // the NEXT plant, not by a brake, so Plant leaves for Reach instead of
    // standing, and the aborts on the way stop preferring a stand. 0 =
    // former ladder (brake, re-seat, surrender).
    [Tooltip("1 = while the CoM runs past the stance foot, keep stepping (Plant leaves for Reach; aborts do not surrender to two-foot stance). 0 = former recovery ladder.")]
    public float walkRunawayKeepStepping = 1f;

    // Not a cadence: the escape is an event (mass past the support), and this
    // is only the loading window the landing sole needs before the other leg
    // may be thrown. plantMinDuration (0.5 s) is the healthy roll-over wait
    // and is far too long here — the trunk is at 30° inside it.
    [Tooltip("Plant age (s) before the runaway escape may leave for Reach. 0 = escape on the first tick.")]
    public float plantRunawayStepSeconds = 0.10f;

    [Tooltip("Minimum Plant age (s) before Fall may start.")]
    public float plantMinDuration = 0.50f;

    // Deadline on the fallComLeadTrigger hold, so a body that cannot roll
    // forward at all still steps instead of standing for good. The wait is
    // not a stall: the Plant→Fall gate is open, the ladder above is not armed
    // and SinkCollapsed still watches, so this is double support standing on
    // the landing brake while the leading ankle and the trailing push move
    // CoM over the foot — which is what Plant is for. Kept well under
    // plantStallStandSeconds: that rung answers a shut gate, this one an open
    // gate waiting for the roll-over.
    [Tooltip("Extra Plant time (s) Fall may wait for the extrapolated CoM to reach fallComLeadTrigger. 0 = do not wait (former release).")]
    public float plantLeadWaitSeconds = 1.0f;

    // Play/WebGL reads this field as-is. HeadlessTrial CLI default is 1.0 s
    // (-reachTimeout); a shorter Play default (0.35) forced Catch while the
    // working stand walk was still in Reach (torso ~20°). Keep in sync.
    [Tooltip("Max Reach age (s) before forcing Catch.")]
    public float reachTimeout = 1.0f;

    // ActiveReachTimeout is min(reachTimeout, this). The stand writes 1.0 s
    // over reachTimeout; this cap is not a CLI flag on purpose. walk2_blend2
    // spent 0.68 s in Reach and the trunk ran to −28° without it.
    [Tooltip("Hard cap on Reach (s) on top of reachTimeout: the swing must be placed inside the fall it catches.")]
    public float reachMaxDuration = 0.50f;

    [Tooltip("Emergency: leave Reach for Catch when |torso omega| exceeds this (deg/s). 0 = off.")]
    public float reachOmegaAbort = 260f;

    // HeadlessTrial CLI default is 30° (-fallAbortDeg). Play used 18°, which
    // aborted Fall inside the working gait (torso ~20°, WALK-2 ≤20°)
    // and skipped Catch — that is the WebGL fold on W. Keep in sync with the stand.
    [Tooltip("Abort Fall → standing Plant when |torso tilt| exceeds this (deg). 0 = off.")]
    public float fallAbortDeg = 30f;

    [Header("Recovery fallback (any starting pose)")]
    // Every other phase has a deadline; Plant does not. It waits for
    // TiltSettled + signed lead ≤ plantComLeadMax + stance grounded, and
    // nothing in the phase can make that band happen: if the body arrived
    // with CoM already past the foot (cold BeginWalk on a tipped pose, or a
    // failed step that ended split), Plant holds the landing brake forever.
    // The ladder below is that missing deadline. It never fires on the happy
    // path: a post-catch Plant opens the gate inside plantMinDuration, so
    // the stall clock is back at zero before the first rung.
    [Tooltip("Plant stall (s) with the Plant→Fall gate shut before support is re-seated onto the other loaded foot. 0 = off (former: wait forever).")]
    public float plantStallReseatSeconds = 1.5f;

    // A catch plants the new stance AHEAD of CoM, so a healthy Plant sits on
    // a negative lead and this rung has nothing to offer. Only a body that
    // has run past its own support while the other foot is already planted
    // further along gets re-seated, and only if that foot is better by a
    // margin — otherwise contact noise would hand the role back and forth.
    [Tooltip("Signed-lead advantage (m) the other grounded foot must have before Plant re-seats support onto it.")]
    public float plantReseatLeadMargin = 0.05f;

    // Last rung: hand both legs back to the quiet two-foot controller
    // (standLeg 0 → splitLegs false in BalanceController). That is the most
    // verified pose in the project — quiet stance holds RMS 0.0052 with a
    // near-straight knee target, which is also how a folded body stands up.
    // Plant keeps ignoreFalling meanwhile, so the CoM band does not trip
    // RelaxAllMuscles. Released the moment the gate opens; Plant then
    // restarts its clock so the split stance has plantMinDuration to ramp.
    [Tooltip("Plant stall (s) with the gate still shut after a re-seat before both legs drop to two-foot quiet stance until the gate opens. 0 = off.")]
    public float plantStallStandSeconds = 3f;
    // StandingRestore is raised when PlantFallGateReady fails, so a tipped
    // or over-led Plant keeps the Recover brake. The propulsion half of
    // that same Plant — trailing A2 (walkPlantPushScale), trailing hip
    // (walkPlantHipExtend), rock lead, Plant lean — all require
    // !StandingRestore. The flag was never cleared when the gate came
    // BACK, so one 5 ms flicker of TiltSettled (12°) latched the brake
    // on and killed push-off for the rest of the phase. Fall from the
    // plantLeadWait deadline then starts a body at rest: that is the
    // shuffle, not a step. Cycle > 0 still applies ApplyRecoverBrake;
    // this only lets the rock / trailing push add on top once the trunk
    // is back in the band the gate already calls settled. Surrender
    // already "releases the moment the gate opens"; this is that same
    // edge for restore. 0 = former latched-until-Fall.
    [Tooltip("1 = clear StandingRestore when PlantFallGateReady. 0 = former latch until Fall.")]
    public float plantClearRestoreOnGate = 1f;

    // Reach exits on air (plant after air, timeout, abort). With the swing
    // foot never leaving the ground none of the three can fire, so a Reach
    // that fails to lift holds a 140° fold on a loaded foot with no way out.
    // Longer than a whole healthy Plant→Fall→Reach→Catch→Recover cycle.
    [Tooltip("Reach whose swing foot never left the ground: leave for a standing Plant after this age (s). 0 = off (former: no grounded Reach exit).")]
    public float reachGroundStallSeconds = 1.5f;

    // A Catch that times out in the air returns to Reach. With no plant to
    // catch on, that pair can repeat while the trunk keeps going.
    // Catch→Reach is a failed catch, so the happy path never counts one.
    [Tooltip("Consecutive failed catches (Catch→Reach) before the driver returns to a standing Plant. 0 = off.")]
    public float fallCatchBounceMax = 4f;

    [Tooltip("Diagnostic: |CoM − catch foot| after a walking catch (~0.60 m). Not a Plant gate — that offset never enters 0.16 m.")]
    public float recoverComOffsetMax = 0.16f;
}
