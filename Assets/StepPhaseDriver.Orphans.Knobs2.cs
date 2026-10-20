using UnityEngine;

public partial class StepPhaseDriver
{
    // scrapeLiftAirEnterGroundArm already keeps reachArm through grounded
    // Reach after scrape-air. Grounded Reach still Lerps fold→landDeg on
    // Last: enterFold is air-only, so the first toe-down Reach frame is
    // 140°→~56° against leftover shin — the shin pair of enterGroundArm,
    // a keep that enterFold still holds only in air. Keep the Reach fold
    // (skip the extend Lerp) on dirt too. ReachResumedFromCatchScrapeAir
    // survives the toe-down (cleared on Reach exit). Heel plant still
    // leaves Reach for Catch. Grounded Catch fold is catchGroundFold.
    // Quiet when enterFold is 0 (shin already dropped
    // in air) or enterGroundArm is 0 (arms already Last-carried on dirt).
    // Stance ankle / pelvis / −swingHipFlex unchanged. 0 = former landDeg
    // lerp on grounded Reach.
    // CLI -fallSwingCatchScrapeLiftAirEnterGroundFold.
    [Tooltip("Catch scrape then air, grounded Reach: 1 = keep Reach fold (no 70°→35°). 0 = former Lerp(fold, landDeg, extend) on toe-down.")]
    public float fallSwingCatchScrapeLiftAirEnterGroundFold = 1f;

    // scrapeLiftAirCatchAirGroundArm already skips Last on grounded
    // never-scrape Catch. Grounded never-scrape Catch still Lerps
    // fold→landDeg on CatchReachShinDeg: catchFold / catchGroundFold are
    // scrape-only, so the first toe-down after a never-scrape fly is
    // 140°→~56° against leftover shin — the shin pair of catchAirGroundArm,
    // a keep that catchAirArm still holds only on the shoulders. Keep
    // the Reach fold (skip the landDeg Lerp) on dirt too until heel plant.
    // Heel-only Catch (CatchPlantAge tracks PhaseAge) does not skip
    // (catchAirArm never ran). extraAge stays 0: never-scrape air is
    // not scrape lift-off. Heel plant still takes landDeg. Quiet when
    // catchAirArm is 0 (air Catch already Last-carried) or catchAirGroundArm
    // is 0 (arms already Last-carried on dirt). Stance ankle / pelvis /
    // −swingHipFlex unchanged. 0 = former landDeg lerp on grounded
    // never-scrape Catch.
    // CLI -fallSwingCatchScrapeLiftAirCatchAirGroundFold.
    [Tooltip("True airborne Catch, grounded: 1 = keep Reach fold until heel plant (no 70°→35°). 0 = former CatchReachShinDeg plant-extend on toe-down after never-scrape air.")]
    public float fallSwingCatchScrapeLiftAirCatchAirGroundFold = 1f;

    // scrapeLiftAirCatchAirEnterEndArm already starts Recover/Plant CatchEnd
    // from reachArm after planted Catch enter. Catch-phase still Lerps
    // FallCatchArmReachStart→carry on Last, then toward carry on
    // CatchPlantAge: catchAirGroundArm needs CatchPlantAge < PhaseAge
    // (heel came down during Catch), and planted-at-enter has
    // catchPlantEnterTime == phaseEnterTime, so the first Catch frame
    // after the bounce is ~vault→carry against leftover shin — the
    // Catch-phase shoulder pair of catchAirEnterEndArm. Skip that Last
    // Lerp and arrive on plant-clock from reachArm (CatchPlantAge tracks
    // PhaseAge here, so the ramp is catchMin from enter). Recover
    // CatchEnd skipLast then continues the same envelope. Quiet when
    // catchAirEnterEndArm is 0 (Recover already Last-carried) or
    // catchAirEnterGroundFold is 0 (shin already dropped on dirt Reach)
    // or catchAirEnterGroundArm is 0 (arms already Last-carried on dirt
    // Reach) or plantClock is off. Stance ankle / pelvis /
    // −swingHipFlex / catchAirGroundArm / catchAirEnterEndArm unchanged.
    // Catch-phase shin on that planted Catch is catchAirEnterCatchFold
    // (catchAirGroundFold needs !planted). 0 = former Last-carry on
    // planted Catch after never-scrape Catch→Reach.
    // CLI -fallSwingCatchScrapeLiftAirCatchAirEnterCatchArm.
    [Tooltip("True airborne Catch, Catch→Reach→Catch planted Catch: 1 = skip Last-carry (plant-clock arrive from reachArm). 0 = former Lerp(reachArm, carry, Last) on Catch after planted enter.")]
    public float fallSwingCatchScrapeLiftAirCatchAirEnterCatchArm = 1f;

    // scrapeLiftAirCatchAirEnterCatchArm already skips Last-carry on
    // planted Catch after never-scrape Catch→Reach. CatchReachShinDeg
    // still Max(Last, CatchPlantAge): catchAirGroundFold needs !planted,
    // and planted-at-enter has catchPlantEnterTime == phaseEnterTime so
    // CatchPlantAge tracks PhaseAge. Last is the Reach freeze (fold was
    // kept by enterGroundFold, but Last still holds Reach extend), so
    // the first Catch frame is 140°→~56° against leftover shin — the
    // shin pair of catchAirEnterCatchArm. Skip Last and arrive on
    // plant-clock (CatchPlantAge / catchMin; tracks PhaseAge here). Quiet
    // when catchAirEnterCatchArm is 0 (Catch already Last-carried) or
    // catchAirEnterGroundFold is 0 (shin already dropped on dirt Reach).
    // Stance ankle / pelvis / −swingHipFlex / catchAirGroundFold /
    // catchAirEnterCatchArm unchanged. Recover/Plant still Max Last
    // (CatchReachExtendProgress Recover/Plant) — catchAirEnterEndFold.
    // 0 = former Max(Last, CatchPlantAge) on planted Catch after
    // never-scrape Catch→Reach. CLI -fallSwingCatchScrapeLiftAirCatchAirEnterCatchFold.
    [Tooltip("True airborne Catch, Catch→Reach→Catch planted Catch shin: 1 = skip Last (plant-clock fold→landDeg). 0 = former Max(Last, CatchPlantAge) on Catch after planted enter.")]
    public float fallSwingCatchScrapeLiftAirCatchAirEnterCatchFold = 1f;

    // scrapeLiftAirCatchGroundFold already keeps Reach fold through
    // grounded scrape Catch until heel plant. CatchReachShinDeg still
    // Max(Last, CatchPlantAge) once planted: catchGroundFold needs
    // !planted, and CatchPlantAge is 0 at heel-down (or tracks PhaseAge
    // if planted at enter). Last is the Catch-enter freeze (or AirAge
    // resume), not the catchGroundFold override, so the first planted
    // Catch frame is 140°→~56° against leftover shin — the planted shin
    // pair of catchGroundArm. Skip Last and arrive on plant-clock
    // (CatchPlantAge / catchMin). Quiet when catchGroundFold is 0 (shin
    // already dropped on dirt) or catchGroundArm is 0 (Catch already
    // Last-carried) or catchEndFold is 0 (Recover already Max'd Last).
    // Stance ankle / pelvis / −swingHipFlex / catchGroundFold /
    // catchEndFold unchanged. Never-scrape planted Catch after
    // Catch→Reach is catchAirEnterCatchFold. Never-scrape air planted
    // Catch (heel during Catch) is catchAirGroundCatchFold. Never-scrape
    // air Recover/Plant shin skip Last is catchAirEndFold. 0 = former
    // Max(Last, CatchPlantAge) on planted scrape Catch.
    // CLI -fallSwingCatchScrapeLiftAirCatchGroundCatchFold.
    [Tooltip("Catch scrape then plant, Catch shin: 1 = skip Last (plant-clock fold→landDeg). 0 = former Max(Last, CatchPlantAge) on planted Catch after scrape.")]
    public float fallSwingCatchScrapeLiftAirCatchGroundCatchFold = 1f;

    // scrapeLiftAirCatchAirGroundFold already keeps Reach fold through
    // grounded never-scrape Catch until heel plant. CatchReachShinDeg still
    // Max(Last, CatchPlantAge) once planted: catchAirGroundFold needs
    // !planted, and CatchPlantAge is 0 at heel-down (or tracks PhaseAge
    // if planted at enter). Last is the Reach freeze at Catch enter
    // (fold was kept by catchAirFold / catchAirGroundFold, Last still
    // holds Reach extend), so the first planted Catch frame is 140°→~56°
    // against leftover shin — the planted shin pair of catchAirGroundArm.
    // Skip Last and arrive on plant-clock (CatchPlantAge / catchMin).
    // Quiet when catchAirGroundFold is 0 (shin already dropped on dirt)
    // or catchAirGroundArm is 0 (Catch already Last-carried) or
    // catchAirEndFold is 0 (Recover already Max'd Last). Heel-only Catch
    // (CatchPlantAge tracks PhaseAge) does not skip — planted Catch after
    // never-scrape Catch→Reach is catchAirEnterCatchFold. Stance ankle /
    // pelvis / −swingHipFlex / catchAirGroundFold / catchAirEndFold
    // unchanged. Plant→Fall leftover skip Last after Recover skip is
    // catchPlantExitFold. MixHipHold (08:56) already shipped. 0 = former
    // Max(Last, CatchPlantAge) on planted never-scrape Catch.
    // CLI -fallSwingCatchScrapeLiftAirCatchAirGroundCatchFold.
    [Tooltip("True airborne Catch then plant, Catch shin: 1 = skip Last (plant-clock fold→landDeg). 0 = former Max(Last, CatchPlantAge) on planted Catch after never-scrape air.")]
    public float fallSwingCatchScrapeLiftAirCatchAirGroundCatchFold = 1f;

    // plantStanceExtendClock holds leftover through Plant. Fall then set
    // stancePlantKnee = landDeg and vaulted from that: leftover still on
    // the catchMin clock (Recover 0.05 s + a short plantMin) jumped
    // fold→landDeg on the only loaded heel, then Lerp to 0 — the same
    // extensor yank, one phase later. Freeze leftover at Plant→Fall and
    // vault from that shin (happy-path leftover 1 is still landDeg).
    // Do not keep targeting landDeg during Fall: that fights the vault.
    // 0 = former vault from landDeg. CLI -fallSwingVaultFromLeftover.
    [Tooltip("Fall/Reach: 1 = vault stance from leftover shin frozen at Plant exit. 0 = former vault from landDeg.")]
    public float fallSwingVaultFromLeftover = 1f;

    // Reach already strips the heel-press half of the swing CoM loop
    // (propulsive plantarflex only). Fall still kept the full loop:
    // Fall→Reach is gated on air, so a heel-press on the folding swing
    // pinned the foot (swingFoot ~0.96) and Reach never started — the
    // lift window never ran. Same half as Reach; stance ankle / comLead
    // still drive. ankleLift still fades the remaining toe grip. Quiet
    // when 0: former full Fall loop (heel-press on a body going back).
    // MixHipHold (08:56) already shipped. Last-carry Recover/Plant shin
    // family is closed (catchPlantExitFold). CLI -fallSwingFallAnklePush.
    [Tooltip("Fall swing ankle: 1 = propulsive half only (Reach lift, no heel-press pin). 0 = former full Fall CoM loop.")]
    public float fallSwingFallAnklePush = 1f;

    // hipLift still faded at 8° past vertical. 8° of a 0.43 m thigh is
    // ~6 cm forward and ~0.4 cm up — the 140° fold still slides along µ=0.4
    // instead of raising the knee, so Fall→Reach (air-gated) never starts.
    // FallAnklePush already dropped the heel-press pin; this raises the
    // Fall-only fade-out to 16° (still far from sci_a 40°). Reach may
    // use the same 16° (fallSwingReachHipLiftCap); Catch scrape may
    // use the same 16° (fallSwingCatchScrapeHipLiftCap).
    // Quiet when 0: former 8° cap. Fall ankleLift may
    // now be 0.5 (fallSwingFallAnkleLift); Reach may write the
    // same 0.5 (fallSwingReachAnkleLift); stance / comLead /
    // −swingHipFlex / PD are untouched.
    // MixHipHold (08:56) already shipped. Last-carry Recover/Plant shin
    // family is closed (catchPlantExitFold). CLI -fallSwingFallHipLiftCap.
    [Tooltip("Fall hipLift cap: 1 = 16° past vertical (Fall lift-off). 0 = former 8° cap.")]
    public float fallSwingFallHipLiftCap = 1f;

    // 16° hipLift still lost to the remaining toe. After FallAnklePush,
    // plantarflex * (1−0.25) − 0.25 leaves 0.5 of a full push on µ=0.4,
    // so ~1.7 cm of knee rise still slides and Fall→Reach (air-gated)
    // never starts. 0.5 fade cancels a full push to 0 — unweights
    // without restoring the heel-press FallAnklePush just stripped
    // (1.0 would be a full dorsiflexor). Fall only; Reach may write
    // 0.5 (fallSwingReachAnkleLift); Catch air may write the same
    // 0.5 (fallSwingCatchAirAnkleLift); Catch scrape may write the same
    // 0.5 (fallSwingCatchScrapeAnkleLift); Catch→Reach scrape may write
    // the same 0.5 (fallSwingCatchReachScrapeAnkleLift). Quiet
    // when 0: former 0.25. Stance / comLead / hipLift cap /
    // −swingHipFlex / PD untouched. MixHipHold (08:56) already shipped.
    // Last-carry Recover/Plant shin family is closed
    // (catchPlantExitFold). CLI -fallSwingFallAnkleLift.
    [Tooltip("Fall ankleLift: 1 = 0.5 fade (Fall lift-off). 0 = former 0.25 fade.")]
    public float fallSwingFallAnkleLift = 1f;

    // 16° cap + 0.25 bias never walks the thigh into that room: at
    // vertical liftSignal = −0.25·16/20 = −0.20, so the 140° fold still
    // slides an unweighted foot (FallAnkleLift already cancelled leftover
    // push) and Fall→Reach (air-gated) never starts. 0.5 bias is −0.40
    // at vertical and still fades at 16° — not −swingHipFlex / sci_a 40°.
    // Fall only; Reach may write 0.5 (fallSwingReachHipLift); Catch
    // scrape keeps 0.25. Quiet when 0: former 0.25.
    // Zero stay zero so -fallSwingHipLift 0 still disables. Stance /
    // comLead / hipLift cap / ankleLift / pelvis / PD untouched.
    // MixHipHold (08:56) already shipped. Last-carry Recover/Plant shin
    // family is closed (catchPlantExitFold). CLI -fallSwingFallHipLift.
    [Tooltip("Fall hipLift amount: 1 = 0.5 bias (Fall lift-off). 0 = former 0.25.")]
    public float fallSwingFallHipLift = 1f;

    // Fall hipLift 0.5 + 16° cap + ankleLift 0.5 can now make Fall→Reach
    // air. The first airborne Reach frame still takes hipair −swingHipFlex
    // (latch is immediate, walkSwingLatchAir 0): Min(angleAtLatch, −40°)
    // yanks a 16° thigh to sci_a and throws the pelvis back. scrapeLiftAirHold
    // only skips the forceSwingFlex add — hipair already set the pose.
    // Clear that pose and keep ThighHold through Fall→Reach first air.
    // Catch→Reach (ReachResumedFromCatch) still reaches. Quiet when 0:
    // former hipair jump. MixHipHold (08:56) already shipped.
    // CLI -fallSwingReachAirHold.
    [Tooltip("Fall→Reach first air: 1 = keep ThighHold (no hipair −swingHipFlex). 0 = former latch jump on the first airborne frame.")]
    public float fallSwingReachAirHold = 1f;

    // 23:39 snap-hold can now make Fall's OWN first millimetre of air.
    // Reach's hold never ran there: Fall→Reach is gated on air, and hipair
    // latches on that same Fall frame (walkSwingLatchAir 0) and takes
    // −swingHipFlex (sci_a). The fold that just freed the chain yanks to
    // 40°, the pelvis eats the reaction, the sole replants, Reach waits.
    // Same keep as Reach: clear the latch pose and continue ThighHold.
    // Fall only; Reach / Catch still use their own holds. Quiet when 0:
    // former latch jump. MixHipHold (08:56) already shipped.
    // CLI -fallSwingFallAirHold.
    [Tooltip("Fall first air: 1 = keep ThighHold (no hipair −swingHipFlex). 0 = former latch jump on the first airborne Fall frame.")]
    public float fallSwingFallAirHold = 1f;

    // 23:47 can now keep ThighHold on Fall's own first millimetre of
    // air (no hipair −swingHipFlex). Fall still fades at 16°: Plant
    // leftover + hipLift 0.5 already walks a 16° thigh, so room=0,
    // floor holds −0.25, but the thigh cannot walk further (~1.7 cm
    // of a 0.43 m segment). The fold that just freed the chain still
    // scrapes, the sole replants, Fall→Reach waits on air. 24° is
    // the same clearance Catch scrape already uses
    // (fallSwingCatchScrapeHipLiftCapWide): at 16° thigh, room is 8°
    // so Fall can walk into that gap. Same fade + 4° deadzone, not
    // −swingHipFlex / sci_a 40°. Fall only; Reach may then use 24°
    // (fallSwingReachHipLiftCapWide); Catch-air may then use 24°
    // (fallSwingCatchAirHipLiftCapWide); Catch scrape already 24°;
    // Plant may then use 24° (fallSwingPlantHipLiftCapWide).
    // Not 3× clock. Quiet when 0: former 16°. MixHipHold
    // (08:56) already shipped. CLI -fallSwingFallHipLiftCapWide.
    [Tooltip("Fall hipLift cap: 1 = 24° past vertical (16° hold). 0 = former 16° cap.")]
    public float fallSwingFallHipLiftCapWide = 1f;

    // 23:53 can now keep 24° in Fall (room=8° at a 16° leftover thigh).
    // Fall→Reach is gated on that air. Reach still fades at 16°
    // (fallSwingReachHipLiftCap): a Fall thigh that walked into the
    // 24° gap is already past the Reach cap, so room≤0, floor holds
    // −0.25, the thigh drops ~8° (~6 cm of knee on a 0.43 m segment)
    // and the sole replants — swingFoot stays high. 24° is the same
    // clearance Fall / Catch scrape already use: at 16° thigh, room
    // is 8° so Reach can hold the lift it just earned. Same fade + 4°
    // deadzone, not −swingHipFlex / sci_a 40°. Reach only
    // (!ReachResumedFromScrape — that bounce already has 24° via
    // fallSwingCatchReachScrapeHipLiftCapWide). Catch-air may then
    // use 24° (fallSwingCatchAirHipLiftCapWide). Plant may then use
    // 24° (fallSwingPlantHipLiftCapWide).
    // Not 3× clock. Quiet when 0: former 16°. MixHipHold (08:56)
    // already shipped. CLI -fallSwingReachHipLiftCapWide.
    [Tooltip("Reach hipLift cap: 1 = 24° past vertical (16° hold). 0 = former 16° cap.")]
    public float fallSwingReachHipLiftCapWide = 1f;

    // 23:58 can now keep 24° in Reach (room=8° at a 16° leftover thigh).
    // Reach→Catch timeout air sits on that lift. Catch-air still fades
    // at 16° (fallSwingCatchAirHipLiftCap): a Reach thigh that walked
    // into the 24° gap is already past the Catch-air cap, so room≤0,
    // floor holds −0.25, the thigh drops ~8° (~6 cm of knee on a
    // 0.43 m segment) and the sole replants — swingFoot stays high.
    // 24° is the same clearance Fall / Reach / Catch scrape already
    // use: at 16° thigh, room is 8° so Catch-air can hold the lift it
    // just earned. Same fade + 4° deadzone, not −swingHipFlex /
    // sci_a 40°. Catch-air only (!SwingFootIsGrounded — Catch scrape
    // already 24° via fallSwingCatchScrapeHipLiftCapWide). Plant may
    // then use 24° (fallSwingPlantHipLiftCapWide). Not 3× clock.
    // Quiet when 0: former 16°. MixHipHold (08:56) already shipped.
    // CLI -fallSwingCatchAirHipLiftCapWide.
    [Tooltip("Catch air hipLift cap: 1 = 24° past vertical (16° hold). 0 = former 16° cap.")]
    public float fallSwingCatchAirHipLiftCapWide = 1f;

    // 00:03 can now keep 24° in Catch-air (room=8° at a 16° leftover thigh).
    // Reach→Catch timeout air sits on that lift. Plant still fades at 16°:
    // leftover Plant + hipLift 0.5 already walks a 16° thigh, so room=0,
    // floor holds −0.25, but the thigh cannot walk further (~1.7 cm of a
    // 0.43 m segment). The fold that should free the chain still scrapes,
    // the sole stays on dirt, Fall→Reach waits on air. 24° is the same
    // clearance Fall / Reach / Catch-air / Catch scrape already use: at
    // 16° thigh, room is 8° so Plant can walk into that gap. Same fade +
    // 4° deadzone, not −swingHipFlex / sci_a 40°. Plant only. Not 3× clock.
    // Quiet when 0: former 16°. MixHipHold (08:56) already shipped.
    // Fall may then use 32° (fallSwingFallHipLiftCapCeil).
    // CLI -fallSwingPlantHipLiftCapWide.
    [Tooltip("Plant hipLift cap: 1 = 24° past vertical (16° hold). 0 = former 16° cap.")]
    public float fallSwingPlantHipLiftCapWide = 1f;

    // 00:09 can now keep 24° in Plant (room=8° at a 16° leftover thigh).
    // hipLift 0.5 + floor −0.25 walks that gap during plantMinDuration,
    // so EnterFall arrives at a 24° thigh. Fall still fades at 24°
    // (fallSwingFallHipLiftCapWide): leftover 24° is already at the cap,
    // room=0, floor holds −0.25, but the thigh cannot walk further (~1.7 cm
    // of a 0.43 m segment). The fold that should free the chain still
    // scrapes, the sole stays on dirt, Fall→Reach waits on air. 32° is
    // the same ceiling walkSwingLiftCouple already uses while the stance
    // hip eats the shove (coupleCapCeilingDeg): at 24° thigh, room is 8°
    // so Fall can walk into that gap. Same fade + 4° deadzone, not
    // −swingHipFlex / sci_a 40°. Fall only; Reach may then use 32°
    // (fallSwingReachHipLiftCapCeil); Catch-air may then use 32°
    // (fallSwingCatchAirHipLiftCapCeil); Plant may then use 32°
    // (fallSwingPlantHipLiftCapCeil).
    // Not 3× clock. Quiet when 0: former 24°. MixHipHold (08:56)
    // already shipped. CLI -fallSwingFallHipLiftCapCeil.
    [Tooltip("Fall hipLift cap: 1 = 32° past vertical (24° hold). 0 = former 24° cap.")]
    public float fallSwingFallHipLiftCapCeil = 1f;

    // 00:16 can now keep 32° in Fall (room=8° at a 24° leftover thigh).
    // Fall→Reach is gated on that air. Reach still fades at 24°
    // (fallSwingReachHipLiftCapWide): a Fall thigh that walked into the
    // 32° gap is already past the Reach cap, so room≤0, floor holds
    // −0.25, the thigh drops ~8° (~6 cm of knee on a 0.43 m segment)
    // and the sole replants — swingFoot stays high. 32° is the same
    // ceiling walkSwingLiftCouple already uses (coupleCapCeilingDeg):
    // at 24° thigh, room is 8° so Reach can hold the lift it just
    // earned. Same fade + 4° deadzone, not −swingHipFlex / sci_a 40°.
    // Reach only (!ReachResumedFromScrape — that bounce already has 24°
    // via fallSwingCatchReachScrapeHipLiftCapWide). Catch-air may then
    // use 32° (fallSwingCatchAirHipLiftCapCeil); Plant may then use
    // 32° (fallSwingPlantHipLiftCapCeil).
    // Not 3× clock. Quiet when 0: former 24°. MixHipHold
    // (08:56) already shipped. CLI -fallSwingReachHipLiftCapCeil.
    [Tooltip("Reach hipLift cap: 1 = 32° past vertical (24° hold). 0 = former 24° cap.")]
    public float fallSwingReachHipLiftCapCeil = 1f;

    // 00:25 can now keep 32° in Reach (room=8° at a 24° leftover thigh).
    // Fall→Reach is gated on that air. Reach→Catch timeout air sits on
    // that lift. Catch-air still fades at 24°
    // (fallSwingCatchAirHipLiftCapWide): a Reach thigh that walked into
    // the 32° gap is already past the Catch-air cap, so room≤0, floor
    // holds −0.25, the thigh drops ~8° (~6 cm of knee on a 0.43 m
    // segment) and the sole replants — swingFoot stays high. 32° is
    // the same ceiling walkSwingLiftCouple already uses
    // (coupleCapCeilingDeg): at 24° thigh, room is 8° so Catch-air can
    // hold the lift it just earned. Same fade + 4° deadzone, not
    // −swingHipFlex / sci_a 40°. Catch-air only (!SwingFootIsGrounded —
    // Catch scrape may then use 32° via fallSwingCatchScrapeHipLiftCapCeil).
    // Plant may then use 32° (fallSwingPlantHipLiftCapCeil). Not 3× clock.
    // Quiet when 0: former 24°. MixHipHold
    // (08:56) already shipped. CLI -fallSwingCatchAirHipLiftCapCeil.
    [Tooltip("Catch air hipLift cap: 1 = 32° past vertical (24° hold). 0 = former 24° cap.")]
    public float fallSwingCatchAirHipLiftCapCeil = 1f;

    // 00:28 can now keep 32° in Catch-air (room=8° at a 24° leftover thigh).
    // Reach→Catch timeout air sits on that lift. Plant still fades at 24°
    // (fallSwingPlantHipLiftCapWide): leftover Plant + hipLift 0.5 already
    // walks a 24° thigh, so room=0, floor holds −0.25, but the thigh cannot
    // walk further (~1.7 cm of a 0.43 m segment). The fold that should
    // free the chain still scrapes, the sole stays on dirt, Fall→Reach
    // waits on air. 32° is the same ceiling walkSwingLiftCouple already
    // uses (coupleCapCeilingDeg): at 24° thigh, room is 8° so Plant can
    // walk into that gap. Same fade + 4° deadzone, not −swingHipFlex /
    // sci_a 40°. Plant only (Fall / Reach / Catch-air already 32°; Catch
    // scrape may then use 32° via fallSwingCatchScrapeHipLiftCapCeil).
    // Not 3× clock. Quiet when 0: former 24°. MixHipHold (08:56) already
    // shipped. CLI -fallSwingPlantHipLiftCapCeil.
    [Tooltip("Plant hipLift cap: 1 = 32° past vertical (24° hold). 0 = former 24° cap.")]
    public float fallSwingPlantHipLiftCapCeil = 1f;

    // 00:30 can now keep 32° in Plant (room=8° at a 24° leftover thigh).
    // Reach→Catch timeout air sits on Catch-air 32°. Catch scrape still
    // fades at 24° (fallSwingCatchScrapeHipLiftCapWide): a Catch-air
    // thigh that walked into the 32° gap is already past the scrape cap,
    // so room≤0, floor holds −0.25, the thigh drops ~8° (~6 cm of knee
    // on a 0.43 m segment) and the sole replants — swingFoot stays high.
    // 32° is the same ceiling walkSwingLiftCouple already uses
    // (coupleCapCeilingDeg): at 24° thigh, room is 8° so Catch scrape
    // can hold the lift it just earned. Same fade + 4° deadzone, not
    // −swingHipFlex / sci_a 40°. Catch scrape only (SwingFootIsGrounded —
    // Catch-air already 32° via fallSwingCatchAirHipLiftCapCeil).
    // Catch→Reach scrape may then use 32° via
    // fallSwingCatchReachScrapeHipLiftCapCeil. Not 3× clock. Quiet when
    // 0: former 24°. MixHipHold (08:56) already shipped.
    // CLI -fallSwingCatchScrapeHipLiftCapCeil.
    [Tooltip("Catch scrape hipLift cap: 1 = 32° past vertical (24° hold). 0 = former 24° cap.")]
    public float fallSwingCatchScrapeHipLiftCapCeil = 1f;

    // 00:36 can now keep 32° in Catch scrape (room=8° at a 24° leftover
    // thigh). Catch-air already 32°. Catch timeout on a grounded bounce
    // (ReachResumedFromScrape) still fades at 24°
    // (fallSwingCatchReachScrapeHipLiftCapWide): a Catch-scrape thigh that
    // walked into the 32° gap is already past the Catch→Reach-scrape cap,
    // so room≤0, floor holds −0.25, the thigh drops ~8° (~6 cm of knee
    // on a 0.43 m segment) and the sole replants — swingFoot stays high.
    // 32° is the same ceiling walkSwingLiftCouple already uses
    // (coupleCapCeilingDeg): at 24° thigh, room is 8° so Catch→Reach
    // scrape can hold the lift it just earned. Same fade + 4° deadzone,
    // not −swingHipFlex / sci_a 40°. Catch→Reach scrape only
    // (ReachResumedFromScrape — Catch scrape already 32° via
    // fallSwingCatchScrapeHipLiftCapCeil). Not 3× clock. Quiet when 0:
    // former 24°. MixHipHold (08:56) already shipped.
    // CLI -fallSwingCatchReachScrapeHipLiftCapCeil.
    [Tooltip("Catch→Reach scrape hipLift cap: 1 = 32° past vertical (24° hold). 0 = former 24° cap.")]
    public float fallSwingCatchReachScrapeHipLiftCapCeil = 1f;

    // 00:41 can now keep 32° on Catch→Reach scrape. Reach→Catch timeout
    // air sits on Catch-air 32°. That air still takes hipair −swingHipFlex:
    // fallSwingCatchAirLiftHold writes the ThighHold terms, but
    // keepThighHoldAir is scrape-only (fallSwingCatchScrapeLiftAirHold),
    // and ThighHold in air needs that keep. forceSwingFlex then latches
    // Min(angleAtLatch, −40°) on the first never-scraped Catch frame —
    // the same yank Reach / Fall already closed (fallSwingReachAirHold /
    // fallSwingFallAirHold). The 32° Catch-air cap never runs on that
    // happy-path timeout; the fold that just freed the chain yanks to
    // 40°, the pelvis eats the reaction, the sole replants. Same keep:
    // clear the latch pose and continue ThighHold. Catch-air only
    // (!SwingFootIsGrounded — planted Catch still takes the trunk;
    // scrape-then-air already has scrapeLiftAirHold). Not sci_a 40°.
    // Quiet when 0: former hipair jump. MixHipHold (08:56) already
    // shipped. CLI -fallSwingCatchAirHold.
    [Tooltip("Catch timeout air: 1 = keep ThighHold (no hipair −swingHipFlex). 0 = former latch jump on never-scraped Catch air.")]
    public float fallSwingCatchAirHold = 1f;

    // 00:42 can now keep ThighHold on Reach→Catch timeout air (no hipair
    // −swingHipFlex). Catch timeout without a plant still bounces to Reach
    // (catchMax, !swingDown). reachAirHold excludes ReachResumedFromCatch,
    // so that bounce takes hipair: Catch-air just held a 32° thigh, then
    // Min(angleAtLatch, −40°) yanks it, the pelvis eats the reaction, the
    // sole replants — swingFoot stays high. scrape Catch→Reach already
    // has scrapeLiftAirEnterHold. Same keep as Catch-air: clear the latch
    // pose and continue ThighHold. Catch→Reach air only
    // (ReachResumedFromCatch, never-scrape — scrape already kept; planted
    // Reach still takes the trunk). Quiet when catchAirHold is 0 (Catch
    // already jumped). MixHipHold (08:56) already shipped.
    // CLI -fallSwingCatchAirEnterHold.
    [Tooltip("Catch→Reach timeout air: 1 = keep ThighHold (no hipair −swingHipFlex). 0 = former latch jump on never-scraped Catch→Reach.")]
    public float fallSwingCatchAirEnterHold = 1f;

    // 00:53 can now keep ThighHold on Catch→Reach timeout air (no hipair
    // −swingHipFlex). Plant 32° fold (walkPlantSwingKnee + hipLift 0.5)
    // can now make Plant's OWN first millimetre of air. That fold writes
    // only while swingGroundedNow, and lastPlantSwing used to clear every
    // Plant frame, so the airborne frame drops reachKnee / thighHold to 0.
    // hipair latches the same frame (walkSwingLatchAir 0) and takes
    // −swingHipFlex: the fold that just freed the chain yanks to 40°,
    // the pelvis eats the reaction, the sole replants, Plant→Fall waits
    // on air. Fall's hold never ran — it is Fall-only. Same keep: hold
    // the last grounded Plant pose, clear the latch, continue ThighHold.
    // Plant air only (enter frame still clears leftover so a new trailing
    // foot is bitwise the former Fall start at 0). Quiet when 0: former
    // hipair jump. MixHipHold (08:56) already shipped.
    // CLI -fallSwingPlantAirHold.
    [Tooltip("Plant first air: 1 = keep ThighHold (no hipair −swingHipFlex). 0 = former latch jump on the first airborne Plant frame.")]
    public float fallSwingPlantAirHold = 1f;

    // 01:05 can now keep ThighHold on Plant's own first millimetre of air.
    // Tick is −50, so an airborne Plant→Fall is Reach on the next tick
    // (Fall's air gate). reachAirHold then clears the latch pose, but
    // reachAirSwing still ramps latchedHipTarget = Min(angleAtLatch,
    // −swingHipFlex). A 32° Plant leftover is already at
    // coupleCapCeilingDeg, so Min(−32, −40) = −40°: the 0.20 s ramp
    // walks 8° past the ceiling the stance hip already pays. Same 32°,
    // not sci_a 40°. Reach air-swing only (ThighHold keeps unchanged).
    // Quiet when 0: former −swingHipFlex. MixHipHold (08:56) already
    // shipped. CLI -fallSwingReachAirSwingCeil.
    [Tooltip("Reach air-swing hip target: 1 = clamp to 32° couple ceiling. 0 = former −swingHipFlex.")]
    public float fallSwingReachAirSwingCeil = 1f;

    // 01:15 can now clear hipair on Catch→Reach scrape-air, so ThighHold
    // at coupleCapCeilingDeg actually runs. fallSwingReachAirSwingCeil
    // only clamps the Reach placement ramp (reachAirHold). Recover can
    // still lose the catch toe for a millimetre (sticky latch,
    // walkSwingUnlatchPlant 0): leftAir is already true, so leftSwingHip
    // starts on and takes Min(angleAtLatch, −swingHipFlex). A 32° leftover
    // yanks to 40° (sci_a), the pelvis eats the reaction, the sole replants,
    // Recover's 0.05 s plant window misses. Same 32°, not sci_a 40°.
    // Hipair pose only (ThighHold / reachAirSwing unchanged). Quiet when
    // 0: former −swingHipFlex. MixHipHold (08:56) already shipped.
    // CLI -fallSwingHipairCeil.
    [Tooltip("Hipair latched target: 1 = clamp to 32° couple ceiling. 0 = former −swingHipFlex.")]
    public float fallSwingHipairCeil = 1f;

    // 01:23 can now clamp Recover lost-toe hipair to 32°. Recover still
    // takes that pose: no keep writes ThighHold there (landing hip must
    // take the trunk), sticky latch (walkSwingUnlatchPlant 0) starts
    // leftSwingHip on, and leftDrivePelvis stays off. The 0.05 s plant
    // window then commands a 32° flexor instead of the pelvis loop that
    // planted Catch already uses. Clear hipair so the landing hip runs
    // when the sole is down. Airborne Recover (the millimetre of lost
    // toe) also silences pelvis-upright — an open-chain SPD is the
    // original airLiftHold shove, not a plant. Not a ThighHold keep,
    // not sci_a 40°, coupleCapCeilingDeg stays 32°. Recover only.
    // Quiet when 0: former 01:23 clamp. MixHipHold (08:56) already
    // shipped. CLI -fallSwingRecoverHipairClear.
    [Tooltip("Recover: 1 = clear hipair so the landing hip takes the trunk. 0 = former latched −32° pose.")]
    public float fallSwingRecoverHipairClear = 1f;

    // 01:28 can now clear Recover hipair and silence pelvis/unload on
    // lost-toe air. Recover still writes airAnkle/airHeel 0 so a heel
    // flicker cannot SPD a standing ankle to world-flat, but DriveSwing
    // still treats that millimetre as airborne swing with sticky latch:
    // joint=0 (foot ⊥ shin). Knee flex then points the toe down and it
    // scrapes — the sole replants, Recover's 0.05 s plant window misses.
    // The hip is already free, so this is the original airLiftHold shove
    // one joint down, not a plant. Quiet the latched air-ankle; planted
    // Recover already took catchAnkleOnSwing. Not a CoM ankle on an open
    // chain, not world-flat, coupleCapCeilingDeg stays 32°. Recover-air
    // only (needs recoverHipairClear). Quiet when 0: former joint=0.
    // MixHipHold (08:56) already shipped. CLI -fallSwingRecoverAirAnkleClear.
    [Tooltip("Recover-air: 1 = skip latched joint=0 ankle (quiet muscles). 0 = former foot ⊥ shin.")]
    public float fallSwingRecoverAirAnkleClear = 1f;

    // 01:36 can now skip latched joint=0 on Recover-air. Plant also
    // writes airAnkle/airHeel 0 (a heel flicker must not SPD a standing
    // Plant ankle to world-flat), but Plant's own 32° fold can now make
    // the trailing foot's first millimetre of air. hipair latches that
    // same frame (walkSwingLatchAir 0). plantAirHold already clears the
    // hip pose and keeps ThighHold, so DriveSwing still SPD ankle joint=0
    // (foot ⊥ shin). Knee flex then points the toe down and it scrapes —
    // the sole replants, Plant→Fall waits on air, swingFoot stays high.
    // Fall/Reach/Catch already write airAnkle (world-flat). Quiet the
    // latched Plant-air ankle; grounded Plant already took CoM/scrape.
    // Not a CoM ankle on an open chain, not world-flat,
    // coupleCapCeilingDeg stays 32°. Plant-air only (needs plantAirHold).
    // Quiet when 0: former joint=0. MixHipHold (08:56) already shipped.
    // CLI -fallSwingPlantAirAnkleClear.
    [Tooltip("Plant-air: 1 = skip latched joint=0 ankle (quiet muscles). 0 = former foot ⊥ shin.")]
    public float fallSwingPlantAirAnkleClear = 1f;

    // 01:45 quieted that Plant-air ankle (ApplySignal 0). The fold had
    // already shortened the chain; a silent foot then falls toe-down on
    // an open shin and the sole replants — swingFoot stays the shuffle
    // (~0.92). Joint=0 is the other paddle. Fall/Reach already drive
    // world-flat (airAnkle) on this millimetre: shin–foot SPD, not a
    // hip pair, so the pelvis loop is untouched. Write that same pose
    // here instead of silence. Gated on the Plant fold leftover (not a
    // standing heel flicker). When this is on, the quiet skip above
    // does not run — drive, not zero. Quiet when 0: former 01:45 skip
    // (or joint=0 if that skip is also 0). MixHipHold already shipped.
    // CLI -fallSwingPlantAirAnkleLevel.
    [Tooltip("Plant-air: 1 = drive world-flat (same airAnkle as Fall/Reach). 0 = former quiet / joint=0.")]
    public float fallSwingPlantAirAnkleLevel = 1f;

    // 01:45 can now skip latched joint=0 on Plant-air. Recover-air already
    // cleared hipair (01:28) and quieted the ankle (01:36). Recover still
    // writes catchKnee / reachKnee = CatchReachShinDeg (landDeg). Drive
    // then SPD that extend on the open chain: hip silent, ankle silent,
    // knee yank fold→landDeg. Extensor on an airborne shin slaps the
    // quiet foot into the dirt, Recover's 0.05 s plant window misses.
    // The hip is already free, so this is the original airLiftHold shove
    // one joint up, not a plant. Quiet the Recover-air knee; planted
    // Recover already took catchKnee plant. Not a ThighHold keep, not
    // world-flat, coupleCapCeilingDeg stays 32°. Recover-air only
    // (needs recoverHipairClear). Quiet when 0: former landDeg SPD.
    // MixHipHold (08:56) already shipped. CLI -fallSwingRecoverAirKneeClear.
    [Tooltip("Recover-air: 1 = skip swing-knee SPD (quiet muscles). 0 = former landDeg extend.")]
    public float fallSwingRecoverAirKneeClear = 1f;

    // Fall→Reach first air now keeps ThighHold (fallSwingReachAirHold).
    // hipLift still faded at 8°: a 16° Fall thigh is already past that
    // cap, so liftSignal = 0 and holdSignal = 0 (hold only fires past
    // vertical going back). The thigh drops ~8° (~6 cm of knee on a
    // 0.43 m segment) and the foot replants — swingFoot stays high.
    // Same 16° fade-out as Fall, Reach only; Catch scrape
    // (ReachResumedFromScrape) may use 16°
    // (fallSwingCatchReachScrapeHipLiftCap). Still far from sci_a 40°.
    // Quiet when 0: former 8°. MixHipHold (08:56) already shipped.
    // CLI -fallSwingReachHipLiftCap.
    [Tooltip("Reach hipLift cap: 1 = 16° past vertical (Fall→Reach air). 0 = former 8° cap.")]
    public float fallSwingReachHipLiftCap = 1f;
}
