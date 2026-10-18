using UnityEngine;

public partial class StepPhaseDriver
{
    // scrapeLiftAirCatchArm already keeps reachArm through airborne Catch.
    // Recover/Plant still call FallCatchArmCatchEnd, which Lerps
    // FallCatchArmReachStart→carry on Last, then toward carry on
    // LastCatchPlantDuration: Last is the Catch-enter freeze (or AirAge
    // resume), not the catchArm override, so the first Recover frame is
    // ~0.5→carry against plantStanceExtendClock leftover shin — the
    // Recover/Plant shoulder pair of catchArm. Skip that Last Lerp and
    // start CatchEnd from reachArm; plant-clock arrive still blends
    // toward carry once the heel has sat catchMin (a long plant already
    // arrived). Recover/Plant decay from that CatchEnd is unchanged.
    // Grounded Catch Last-carry is catchGroundArm. True airborne Catch
    // Last-carry is catchAirArm. Quiet when catchArm is 0 (Catch already Last-carried)
    // or catchFold is 0 (shin already dropped). Shin pair of this skip
    // (CatchReachExtendProgress Recover/Plant still Max Last) is
    // catchEndFold. 0 = former Last-carry in
    // Recover/Plant. CLI -fallSwingCatchScrapeLiftAirCatchEndArm.
    [Tooltip("Catch scrape then air, Recover/Plant: 1 = CatchEnd from reachArm (no Last-carry). 0 = former Lerp(reachArm, carry, Last) in FallCatchArmCatchEnd.")]
    public float fallSwingCatchScrapeLiftAirCatchEndArm = 1f;

    // scrapeLiftAirCatchEndArm already starts Recover/Plant CatchEnd from
    // reachArm. Grounded Catch still Lerps FallCatchArmReachStart→carry on
    // Last, then toward carry on CatchPlantAge: Last is the Catch-enter
    // freeze (or AirAge resume), not the catchArm override, so the first
    // toe-down Catch frame is ~0.5→carry against leftover shin — the
    // grounded Catch shoulder pair of catchArm, a quiet trunk against a
    // folded shin that scrapeExtendHold still holds. Skip that Last Lerp
    // and arrive on plant-clock (0 until heel); Recover CatchEnd skipLast
    // then continues the same envelope. Airborne Catch keep is catchArm.
    // True airborne Catch (never scraped) Last-carry is catchAirArm. Quiet when
    // catchArm is 0 (air Catch already Last-carried), catchEndArm is 0
    // (Recover already Last-carried), catchFold is 0 (shin already
    // dropped), or plantClock is off (former PhaseAge). 0 = former
    // Last-carry on grounded Catch.
    // CLI -fallSwingCatchScrapeLiftAirCatchGroundArm.
    [Tooltip("Catch scrape then air, grounded Catch: 1 = skip Last-carry (plant-clock arrive from reachArm). 0 = former Lerp(reachArm, carry, Last) on toe-down.")]
    public float fallSwingCatchScrapeLiftAirCatchGroundArm = 1f;

    // scrapeLiftAirCatchGroundArm already skips Last on grounded scrape Catch.
    // A true airborne Catch (never scraped: timeout/abort from Reach with
    // the toe still up) still Lerps FallCatchArmReachStart→carry on Last:
    // Last is the Reach freeze at Catch enter, not a scrape, so the catching
    // shin plants against ~vault→carry shoulders before the heel meets dirt.
    // Keep reachArm (skip Last) until the toe meets dirt. extraAge stays 0:
    // air Catch is not arm progress (LastCatch must not join the 0.15 s
    // vault clock). Recover/Plant CatchEnd after that keep is catchAirEndArm.
    // Grounded never-scrape Catch Last-carry is catchAirGroundArm.
    // Grounded Reach Last-carry after enterArm is enterGroundArm.
    // Grounded Reach fold after that keep is enterGroundFold.
    // Quiet when catchArm is 0 (Catch already Last-carried). Shin pair of
    // this keep is catchAirFold. Stance ankle / pelvis / −swingHipFlex /
    // fold unchanged. 0 = former Last-carry on never-scrape air.
    // CLI -fallSwingCatchScrapeLiftAirCatchAirArm.
    [Tooltip("True airborne Catch (never scraped): 1 = keep reachArm (no Last-carry). 0 = former Lerp(reachArm, carry, Last) in air.")]
    public float fallSwingCatchScrapeLiftAirCatchAirArm = 1f;

    // scrapeLiftAirCatchAirArm already keeps reachArm through true airborne
    // Catch. Recover/Plant still call FallCatchArmCatchEnd, which Lerps
    // FallCatchArmReachStart→carry on Last: RecoveredFromCatchScrapeAir
    // is scrape-only, so the first Recover frame after a never-scrape
    // Catch is ~vault→carry against leftover shin — the Recover/Plant
    // shoulder pair of catchAirArm. Skip that Last Lerp and start CatchEnd
    // from reachArm; plant-clock arrive still blends toward carry once
    // the heel has sat catchMin. Recover/Plant decay from that CatchEnd
    // is unchanged. Heel-only Catch (planted at Catch enter) does not
    // latch (catchAirArm never ran). Grounded never-scrape Catch
    // Last-carry is catchAirGroundArm. Grounded Reach Last-carry after
    // enterArm is enterGroundArm. Grounded Reach fold after that keep
    // is enterGroundFold.
    // Quiet when catchAirArm is 0
    // (Catch already Last-carried) or catchArm is 0. Stance ankle /
    // pelvis / −swingHipFlex / fold unchanged. Recover/Plant shin skip
    // Last after that Catch is catchAirEndFold. 0 = former Last-carry in
    // Recover/Plant after never-scrape Catch.
    // CLI -fallSwingCatchScrapeLiftAirCatchAirEndArm.
    [Tooltip("True airborne Catch, Recover/Plant: 1 = CatchEnd from reachArm (no Last-carry). 0 = former Lerp(reachArm, carry, Last) in FallCatchArmCatchEnd.")]
    public float fallSwingCatchScrapeLiftAirCatchAirEndArm = 1f;

    // scrapeLiftAirCatchAirArm already keeps reachArm through true airborne
    // Catch. Grounded never-scrape Catch still Lerps FallCatchArmReachStart
    // →carry on Last: catchAirArm is air-only, so the first toe-down after
    // a never-scrape fly is ~vault→carry against leftover shin — the dirt
    // sibling of catchAirArm. Skip that Last Lerp and arrive on plant-clock
    // (0 until heel); Recover catchAirEndArm then continues the same
    // envelope. Heel-only Catch (planted at Catch enter, CatchPlantAge
    // tracks PhaseAge) does not skip (catchAirArm never ran). Grounded
    // Reach fold after enterGroundArm is enterGroundFold. Quiet when
    // catchAirArm is 0 (air Catch already Last-carried), catchAirEndArm
    // is 0 (Recover already Last-carried), catchArm is 0, or plantClock
    // is off. Stance ankle / pelvis / −swingHipFlex / fold unchanged.
    // 0 = former Last-carry on grounded never-scrape Catch.
    // CLI -fallSwingCatchScrapeLiftAirCatchAirGroundArm.
    [Tooltip("True airborne Catch, grounded: 1 = skip Last-carry (plant-clock arrive from reachArm). 0 = former Lerp(reachArm, carry, Last) on toe-down after never-scrape air.")]
    public float fallSwingCatchScrapeLiftAirCatchAirGroundArm = 1f;

    // scrapeLiftAirEnterArm already keeps reachArm through Catch→Reach
    // scrape-air. Grounded Reach still Lerps reachArm→carry on Last:
    // Last is the Catch-enter freeze (or AirAge resume), not the enterArm
    // override, so the first toe-down Reach frame is ~0.5→carry against
    // leftover shin — the dirt sibling of enterArm, a quiet trunk against
    // a folded shin that enterFold still holds only in air. Keep reachArm
    // (skip the extend Lerp) on dirt too. ReachResumedFromCatchScrapeAir
    // survives the toe-down (cleared on Reach exit). Grounded Reach fold
    // after that keep is enterGroundFold. Grounded never-scrape
    // Catch Last-carry is catchAirGroundArm. Quiet when enterArm is 0 (air Reach already
    // Last-carried) or enterFold is 0 (shin already dropped). Stance
    // ankle / pelvis / −swingHipFlex / fold unchanged. 0 = former
    // Last-carry on grounded Reach.
    // CLI -fallSwingCatchScrapeLiftAirEnterGroundArm.
    [Tooltip("Catch scrape then air, grounded Reach: 1 = keep reachArm (no Last-carry). 0 = former Lerp(reachArm, carry, extend) on toe-down.")]
    public float fallSwingCatchScrapeLiftAirEnterGroundArm = 1f;

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

    // scrapeLiftAirCatchFold already keeps Reach fold through airborne Catch.
    // Grounded Catch still Lerps fold→landDeg on CatchReachShinDeg:
    // catchFold is air-only (!SwingFootIsGrounded), so the first toe-down
    // Catch frame is 140°→~56° against leftover shin — the shin pair of
    // catchGroundArm, a keep that catchFold still holds only in air. Keep
    // the Reach fold (skip the landDeg Lerp) on dirt too until heel plant.
    // CatchScrapedThisPhase / CatchResumedFromReachScrapeAir survive the
    // toe-down. Heel plant skip Last is catchGroundCatchFold (plant-clock
    // fold→landDeg). Grounded never-scrape Catch
    // fold is catchAirGroundFold (shin pair of catchAirGroundArm). Quiet when
    // catchFold is 0 (shin already dropped in air) or catchGroundArm is 0
    // (arms already Last-carried on dirt). Stance ankle / pelvis /
    // −swingHipFlex unchanged. 0 = former landDeg lerp on grounded Catch.
    // CLI -fallSwingCatchScrapeLiftAirCatchGroundFold.
    [Tooltip("Catch scrape then air, grounded Catch: 1 = keep Reach fold until heel plant (no 70°→35°). 0 = former CatchReachShinDeg plant-extend on toe-down.")]
    public float fallSwingCatchScrapeLiftAirCatchGroundFold = 1f;

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

    // scrapeLiftAirCatchAirArm already keeps reachArm through true airborne
    // Catch. Never-scrape air Catch still Lerps fold→landDeg on
    // CatchReachShinDeg: catchFold is scrape-only, so a timeout/abort Catch
    // with the toe still up is 140°→~56° against leftover shin — the shin
    // pair of catchAirArm, a keep that catchAirArm still holds only on the
    // shoulders. Keep the Reach fold (skip the landDeg Lerp) until the
    // toe meets dirt. extraAge stays 0: never-scrape air is not scrape
    // lift-off. Heel plant still takes landDeg. Grounded never-scrape
    // Catch fold is catchAirGroundFold. Quiet when catchAirArm is 0
    // (shoulders already Last-carried). Stance ankle / pelvis /
    // −swingHipFlex / catchFold / catchGroundFold / catchAirArm /
    // catchAirGroundArm / catchAirGroundFold unchanged. 0 = former landDeg
    // lerp on never-scrape air Catch.
    // CLI -fallSwingCatchScrapeLiftAirCatchAirFold.
    [Tooltip("True airborne Catch (never scraped): 1 = keep Reach fold until heel plant (no 70°→35°). 0 = former CatchReachShinDeg plant-extend in air.")]
    public float fallSwingCatchScrapeLiftAirCatchAirFold = 1f;

    // scrapeLiftAirCatchAirFold already keeps Reach fold through never-scrape
    // air Catch. Catch→Reach still Lerps fold→landDeg on Last: enterFold
    // requires ReachResumedFromCatchScrapeAir, so timeout/abort Catch with
    // the toe still up then bouncing to Reach is 140°→~56° against leftover
    // shin — the Catch→Reach shin pair of catchAirFold. Keep the Reach fold
    // (skip the extend Lerp) until the toe meets dirt. extraAge stays 0 on
    // Catch; the bounce already continues fold on Reach PhaseAge (liftAge).
    // Heel plant still Lerps. Grounded never-scrape Reach fold is
    // catchAirEnterGroundFold (enterGroundFold is scrape-only). Shoulder
    // pair of this skip is catchAirEnterArm. Quiet when catchAirFold is 0
    // (Catch already dropped the shin). Stance ankle / pelvis /
    // −swingHipFlex / enterFold / catchFold / catchAirFold / catchAirArm
    // unchanged. 0 = former landDeg lerp on never-scrape Catch→Reach.
    // CLI -fallSwingCatchScrapeLiftAirCatchAirEnterFold.
    [Tooltip("True airborne Catch, Catch→Reach: 1 = keep Reach fold (no 70°→35°). 0 = former Lerp(fold, landDeg, extend) on the never-scrape bounce.")]
    public float fallSwingCatchScrapeLiftAirCatchAirEnterFold = 1f;

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
}
