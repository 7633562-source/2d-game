using UnityEngine;

public partial class StepPhaseDriver
{
    // Catch→Reach while airborne kept the first Reach air clock. Balance
    // reads this so hipLift / ankleLift / fold continue the frozen
    // Fall+Reach clock (fallSwingCatchReachLiftClock) instead of
    // restarting from LastFallDuration / PhaseAge 0 or jumping to 1.
    // Stance vault on that same edge is fallSwingCatchReachVaultClock.
    // Catch→Reach vault continuing on the new Reach PhaseAge (still
    // without LastCatch) is fallSwingCatchReachVaultAge.
    // Airborne Catch yield on that same edge is fallSwingCatchAirVaultHold.
    // That hold at clock 0 forced vault=1; keeping the Fall+Reach
    // remainder instead (still without LastCatch) is
    // fallSwingCatchAirVaultRemain.
    // Airborne Catch arms on that same edge are fallSwingCatchAirArmHold.
    // Airborne Catch shin leftover on that same edge is
    // fallSwingCatchAirExtendHold. Catch scrape still Max'd Last with
    // live ReachExtendProgress; freeze Catch shin at Last is
    // fallSwingCatchScrapeExtendHold. That freeze still lost Last at
    // Catch→Reach enter (Max with live Progress); keep Last on the
    // scrape bounce is fallSwingCatchReachScrapeExtendEnterHold. That
    // keep is grounded-only; after Catch scrape then air the timeout
    // bounce still Max'd live Progress is
    // fallSwingCatchScrapeExtendAirEnterHold. Airborne Catch hipLift / ankleLift /
    // thighHold on that same edge is fallSwingCatchAirLiftHold (frozen
    // Fall+Reach clock until the heel plants; Catch→Reach still uses
    // liftAge unless scrapeLiftHold freezes that extraAge on a toe
    // scrape). That hold still lost the hip on Catch scrape then air
    // (forceSwingFlex −swingHipFlex); keep ThighHold through that air is
    // fallSwingCatchScrapeLiftAirHold. That hold still lost the hip on
    // Catch→Reach (CatchScrapedThisPhase cleared); keep ThighHold through
    // the bounce is fallSwingCatchScrapeLiftAirEnterHold. That keep still
    // lost the hip on Reach→Catch (ReachResumedFromCatchScrapeAir cleared);
    // keep ThighHold through the return bounce is
    // fallSwingCatchScrapeLiftAirCatchHold. That keep still commanded
    // CatchReachShinDeg (70°→35°) on the same airborne toe; keep the
    // Reach fold until heel plant is
    // fallSwingCatchScrapeLiftAirCatchFold. That keep still Lerped
    // reachArm→carry on Last in Catch; keep reachArm until heel plant is
    // fallSwingCatchScrapeLiftAirCatchArm. Recover/Plant CatchEnd still
    // Lerped that Last; start CatchEnd from reachArm is
    // fallSwingCatchScrapeLiftAirCatchEndArm. Grounded Catch still Lerped
    // that Last on toe-down; skip Last and arrive on plant-clock is
    // fallSwingCatchScrapeLiftAirCatchGroundArm. True airborne Catch
    // (never scraped) still Lerped that Last in air; keep reachArm is
    // fallSwingCatchScrapeLiftAirCatchAirArm. Recover/Plant CatchEnd
    // still Lerped that Last after Catch flags cleared (scrape latch
    // is scrape-only); start CatchEnd from reachArm is
    // fallSwingCatchScrapeLiftAirCatchAirEndArm. Grounded never-scrape
    // Catch still Lerped that Last on toe-down; skip Last and arrive on
    // plant-clock is fallSwingCatchScrapeLiftAirCatchAirGroundArm.
    // Grounded Reach still Lerped that Last on toe-down after enterArm;
    // keep reachArm is fallSwingCatchScrapeLiftAirEnterGroundArm. That
    // keep still Lerped fold→landDeg on dirt (enterFold is air-only);
    // keep the Reach fold is fallSwingCatchScrapeLiftAirEnterGroundFold.
    // Grounded Catch still Lerped fold→landDeg on CatchReachShinDeg
    // (catchFold is air-only); keep the Reach fold until heel plant is
    // fallSwingCatchScrapeLiftAirCatchGroundFold. Grounded never-scrape
    // Catch still Lerped fold→landDeg on that same toe-down (catchFold /
    // catchGroundFold are scrape-only); keep the Reach fold until heel
    // plant is fallSwingCatchScrapeLiftAirCatchAirGroundFold. True airborne
    // Catch still Lerped fold→landDeg in air (catchFold is scrape-only);
    // keep the Reach fold is fallSwingCatchScrapeLiftAirCatchAirFold.
    // Never-scrape Catch→Reach still Lerped fold→landDeg on Last
    // (enterFold is scrape-only); keep the Reach fold on that bounce is
    // fallSwingCatchScrapeLiftAirCatchAirEnterFold. That keep is the shin.
    // The same Last still Lerped reachArm→carry on that bounce (enterArm
    // is scrape-only); keep reachArm is
    // fallSwingCatchScrapeLiftAirCatchAirEnterArm. Grounded never-scrape
    // Reach still Lerped that Last on toe-down (enterGroundArm is
    // scrape-only); keep reachArm is
    // fallSwingCatchScrapeLiftAirCatchAirEnterGroundArm. That keep still
    // Lerped fold→landDeg on dirt (enterGroundFold is scrape-only); keep
    // the Reach fold is
    // fallSwingCatchScrapeLiftAirCatchAirEnterGroundFold.
    // Recover/Plant CatchEnd still Lerped that Last after Catch flags
    // cleared (RecoveredFromCatchAir needs heel-down during Catch);
    // start CatchEnd from reachArm after planted Catch enter is
    // fallSwingCatchScrapeLiftAirCatchAirEnterEndArm.
    // Catch-phase still Lerped that Last on the planted Catch
    // (catchAirGroundArm needs CatchPlantAge < PhaseAge); skip Last and
    // arrive on plant-clock is
    // fallSwingCatchScrapeLiftAirCatchAirEnterCatchArm.
    // Catch-phase shin still Max(Last, CatchPlantAge) on that planted
    // Catch (catchAirGroundFold needs !planted); skip Last and arrive
    // on plant-clock is
    // fallSwingCatchScrapeLiftAirCatchAirEnterCatchFold.
    // Recover/Plant still Max(Last, held+PhaseAge) after that skip
    // (Last is the Reach freeze); skip Last and continue plant-clock is
    // fallSwingCatchScrapeLiftAirCatchAirEnterEndFold.
    // Scrape Recover/Plant still Max(Last, held+PhaseAge) after catchEndArm
    // (Last is the Catch-enter freeze); skip Last and continue plant-clock
    // is fallSwingCatchScrapeLiftAirCatchEndFold.
    // Catch planted scrape shin still Max(Last, CatchPlantAge) after
    // catchGroundFold (needs !planted); skip Last and arrive on
    // plant-clock is fallSwingCatchScrapeLiftAirCatchGroundCatchFold.
    // Scrape never-air Recover/Plant still Max(Last, held+PhaseAge)
    // after that Catch skip (CatchScrapedThisPhase cleared, scrape latch
    // needs lift-off); skip Last and continue plant-clock is
    // fallSwingCatchScrapeLiftAirCatchGroundCatchEndFold.
    // Never-scrape air Recover/Plant still Max(Last, held+PhaseAge)
    // after catchAirEndArm (Last is the Reach freeze); skip Last and
    // continue plant-clock is
    // fallSwingCatchScrapeLiftAirCatchAirEndFold.
    // Catch planted never-scrape shin still Max(Last, CatchPlantAge)
    // after catchAirGroundFold (needs !planted); skip Last and arrive
    // on plant-clock is
    // fallSwingCatchScrapeLiftAirCatchAirGroundCatchFold.
    // Plant→Fall leftover still Max(Last, held) after those Recover
    // skips; skip Last and continue plant-clock is
    // fallSwingCatchScrapeLiftAirCatchPlantExitFold.
    // Post-swap Plant leftover on the new
    // stance is fallSwingPlantStanceExtendClock. Fall/Reach vault from
    // that leftover (frozen at Plant exit) is fallSwingVaultFromLeftover.
    // Fall swing CoM loop still kept heel-press (Reach already
    // stripped it); Fall→Reach is gated on air, so that pin held
    // swingFoot ~0.96. Propulsive half only — same as Reach — is
    // fallSwingFallAnklePush.
    // Fall hipLift fade-out 16° (Catch scrape may now 16° via
    // fallSwingCatchScrapeHipLiftCap; ankleLift 0.5 is
    // fallSwingCatchScrapeAnkleLift;
    // floor is fallSwingCatchScrapeHipLiftCapHold; hipLift 0.5 is
    // fallSwingCatchScrapeHipLift) is
    // fallSwingFallHipLiftCap. Fall ankleLift 0.5 fade (Reach/Catch
    // scrape may now 0.5 via fallSwingCatchScrapeAnkleLift) is
    // fallSwingFallAnkleLift. Fall hipLift
    // amount 0.5 (Catch scrape may now 0.5 via
    // fallSwingCatchScrapeHipLift) is fallSwingFallHipLift.
    // Fall→Reach first air still took hipair −swingHipFlex (sci_a);
    // keep ThighHold through that air is fallSwingReachAirHold.
    // That air still dropped the 16° Fall thigh (8° cap, liftSignal 0);
    // same 16° fade-out on Reach (Catch scrape keep 8°) is
    // fallSwingReachHipLiftCap. That 16° Reach still wrote 0.25
    // (liftSignal −0.20 at vertical); 0.5 bias on Reach (Catch scrape
    // keep 0.25) is fallSwingReachHipLift. That 16°/0.5 Reach still
    // wrote ankleLift 0.25 (leftover plantarflex slides on µ=0.4);
    // 0.5 fade on Reach (Catch scrape may now 0.5 via
    // fallSwingCatchScrapeAnkleLift) is
    // fallSwingReachAnkleLift. That 16°/0.5/0.5 Reach still had
    // liftSignal 0 at the cap (P to the target); 0.5 fade floor on
    // Reach is
    // fallSwingReachHipLiftCapHold. That same P=0 still drops the
    // Fall thigh at 16° (Reach floor is Reach-only); 0.5 fade floor
    // on Fall is fallSwingFallHipLiftCapHold. That floor still scrapes
    // while leftover→0 shares the fold clock (cycle-0 quiet knee is
    // ~2 mm of height); leftover→0 at 2× fold clock is
    // fallSwingFallVaultLead (Reach continues that clock). That strut
    // still leaves hipLift / ankleLift on the 1× fold ramp (vault 1 at
    // 0.075 s, lift still half); hipLift/ankleLift at 2× fold clock is
    // fallSwingFallLiftLead (Reach / Catch air continue that clock).
    // That lift still shares the 1× fold (70° at lift=1); 140° at 2×
    // ramp clock is fallSwingFallFoldLead (Reach / Catch continue that
    // clock). That fold can now put the thigh at 16° in air; timeout
    // Catch still faded at 8°, so the Reach thigh was immediately past
    // the cap (liftSignal 0) and the foot replanted. Same 16° fade-out
    // on Catch air (Catch scrape keeps 8°) is
    // fallSwingCatchAirHipLiftCap. That 16° Catch air still had
    // liftSignal 0 at the cap (P to the target; Fall/Reach already
    // floor at 0.5); 0.5 fade floor on Catch air is
    // fallSwingCatchAirHipLiftCapHold. That floor
    // still writes hipLift 0.25 (liftSignal −0.125 at the cap vs
    // Fall/Reach −0.25); 0.5 bias on Catch air (Catch scrape keep
    // 0.25) is fallSwingCatchAirHipLift. That 16°/0.5 Catch air
    // still wrote ankleLift 0.25 (leftover plantarflex slides on
    // µ=0.4); 0.5 fade on Catch air (Catch scrape may now 0.5 via
    // fallSwingCatchScrapeAnkleLift) is
    // fallSwingCatchAirAnkleLift. That 16°/0.5/0.5 Catch air still
    // met an 8° scrape cap (liftSignal 0, foot replants); same 16°
    // fade-out on Catch scrape is
    // fallSwingCatchScrapeHipLiftCap. That 16° Catch scrape still had
    // liftSignal 0 at the cap (P to the target; Fall/Reach/Catch air
    // already floor at 0.5); 0.5 fade floor on Catch scrape is
    // fallSwingCatchScrapeHipLiftCapHold. That floor
    // still writes hipLift 0.25 (liftSignal −0.125 at the cap vs
    // Fall/Reach/Catch-air −0.25); 0.5 bias on Catch scrape is
    // fallSwingCatchScrapeHipLift. That 16°/0.5 floor on scrape still
    // wrote ankleLift 0.25 (leftover plantarflex slides on µ=0.4);
    // 0.5 fade on Catch scrape is fallSwingCatchScrapeAnkleLift.
    // That 16°/0.5/0.5 scrape still shares the 1× fold ramp (vault 1 at
    // 0.075 s, lift still half vs Fall/Reach/Catch-air 2×);
    // hipLift/ankleLift at 2× fold clock on Catch scrape is
    // fallSwingCatchScrapeLiftLead (Reach resumed from scrape continues
    // that clock). That 16° scrape still met an 8° Catch→Reach cap
    // (ReachResumedFromScrape, liftSignal 0, foot replants); same 16°
    // fade-out on Catch→Reach scrape is
    // fallSwingCatchReachScrapeHipLiftCap. That 16° Catch→Reach scrape
    // still had liftSignal 0 at the cap (P to the target; Fall/Reach/
    // Catch-air/Catch-scrape already floor at 0.5); 0.5 fade floor on
    // Catch→Reach scrape is fallSwingCatchReachScrapeHipLiftCapHold.
    // That floor still writes hipLift 0.25 (liftSignal −0.125 at the
    // cap vs Fall/Reach/Catch-air/Catch-scrape −0.25); 0.5 bias on
    // Catch→Reach scrape is fallSwingCatchReachScrapeHipLift. That
    // 16°/0.5 floor on Catch→Reach scrape still wrote ankleLift 0.25
    // (leftover plantarflex slides on µ=0.4); 0.5 fade on Catch→Reach
    // scrape is fallSwingCatchReachScrapeAnkleLift.
    // That 16° cap still had room=0 at a 16° Catch-scrape thigh
    // (floor holds −0.25, thigh cannot walk further); 24° fade-out
    // on Catch scrape is fallSwingCatchScrapeHipLiftCapWide.
    // That same room=0 on Catch→Reach scrape is already 24° via
    // fallSwingCatchReachScrapeHipLiftCapWide.
    // That same room=0 still pinned Fall at 16° after 23:47 kept
    // ThighHold on Fall's first air; 24° fade-out on Fall is
    // fallSwingFallHipLiftCapWide. That same room=0 still pinned
    // Reach at 16° after Fall walked into 24°; 24° fade-out on Reach
    // is fallSwingReachHipLiftCapWide. That same room=0 still pinned
    // Catch-air at 16° after Reach walked into 24°; 24° fade-out on
    // Catch-air is fallSwingCatchAirHipLiftCapWide. Plant is
    // fallSwingPlantHipLiftCapWide. That 24° Plant leftover still
    // pinned Fall at 24°; 32° fade-out on Fall is
    // fallSwingFallHipLiftCapCeil. That same room=0 still pinned
    // Reach at 24° after Fall walked into 32°; 32° fade-out on Reach
    // is fallSwingReachHipLiftCapCeil. That same room=0 still pinned
    // Catch-air at 24° after Reach walked into 32°; 32° fade-out on
    // Catch-air is fallSwingCatchAirHipLiftCapCeil. That 24° Plant
    // leftover still pinned Plant at 24°; 32° fade-out on Plant is
    // fallSwingPlantHipLiftCapCeil. That same room=0 still pinned Catch
    // scrape at 24° after Catch-air walked into 32°; 32° fade-out on
    // Catch scrape is fallSwingCatchScrapeHipLiftCapCeil. That same
    // room=0 still pinned Catch→Reach scrape at 24° after Catch scrape
    // walked into 32°; 32° fade-out on Catch→Reach scrape is
    // fallSwingCatchReachScrapeHipLiftCapCeil.
    // Fall→Reach arm ramp on that same PhaseAge reset is
    // fallSwingReachArmClock. Catch arm envelope from that frozen
    // LastFall+LastReach clock (not from 1) is fallSwingCatchArmClock.
    // Catch→Reach arm start from that same frozen envelope (not from 1)
    // is fallSwingCatchReachArmClock. Catch→Reach lift from that same
    // frozen clock (not from 1) is fallSwingCatchReachLiftClock.
    // Catch→Reach lift continuing on the new Reach PhaseAge (not stalled
    // at Catch enter, still without LastCatch) is fallSwingCatchReachLiftAge.
    // Catch→Reach arms continuing on that same new Reach PhaseAge (still
    // without LastCatch) is fallSwingCatchReachArmAge.
    // Catch→Reach resume on a toe scrape (not only free air) is
    // fallSwingCatchReachScrapeResume: Catch timeout already uses
    // !planted, so a grounded heel-up bounce used to reset the air clock.
    // That scrape then still ramped liftAge; freeze the Catch enter
    // lift clock (no new PhaseAge) is fallSwingCatchReachScrapeLiftHold.
    // That freeze then still held extraAge=0 after the toe lifted;
    // resume fold extraAge from lift-off (not PhaseAge) is
    // fallSwingCatchReachScrapeLiftAirAge.
    // That scrape then still ramped vaultAge; freeze the Catch enter
    // vault clock (no new PhaseAge) is fallSwingCatchReachScrapeVaultHold.
    // That freeze then still held extraAge=0 / Catch remain after the
    // toe lifted; resume vault extraAge from lift-off (not PhaseAge) is
    // fallSwingCatchReachScrapeVaultAirAge.
    // That scrape then still ramped armAge; freeze the Catch enter
    // arm clock (no new PhaseAge) is fallSwingCatchReachScrapeArmHold.
    // That freeze then still held extraAge=0 after the toe lifted;
    // resume arm extraAge from lift-off (not PhaseAge) is
    // fallSwingCatchReachScrapeArmAirAge.
    // That scrape then still switched Catch RecoverBrake to Reach lean
    // / 0.25 stance ankle; keep the Catch brake (Reach pose still
    // runs) is fallSwingCatchReachScrapeBrakeHold.
    // That freeze then still held RecoverBrake after the toe lifted;
    // resume Reach lean from lift-off is
    // fallSwingCatchReachScrapeBrakeAirAge.
    // That scrape then still applied leftover stancePlantKnee through
    // ApplyFallCatchVault; keep Catch vault yield (no leftover pose)
    // is fallSwingCatchReachScrapeYieldHold.
    // That freeze then still held Catch yield after the toe lifted;
    // resume ApplyFallCatchVault from lift-off is
    // fallSwingCatchReachScrapeYieldAirAge.
    // That scrape then still ramped ReachExtendProgress (scrapeExtend
    // counts heel-up as air); freeze the Catch-enter extend
    // (shin Lerp and arm carry) is fallSwingCatchReachScrapeExtendHold.
    // That freeze then still held Last after the toe lifted; resume
    // extend from lift-off (not live Progress, not PhaseAge) is
    // fallSwingCatchReachScrapeExtendAirAge.
    // Catch itself still Max'd Last with that live Progress before the
    // bounce; freeze Catch shin at Last on a grounded scrape is
    // fallSwingCatchScrapeExtendHold.
    // That freeze then still Max'd Last with live Progress after the
    // toe lifted (scrapeExtend counted dirt as air); resume from
    // lift-off (not live Progress) is fallSwingCatchScrapeExtendAirAge.
    // That Catch freeze still lost Last at Catch→Reach enter
    // (Max with live Progress); keep Last on that scrape bounce is
    // fallSwingCatchReachScrapeExtendEnterHold.
    // That AirAge resume still lost Last on a timeout bounce after
    // lift-off (ReachResumedFromScrape is grounded-only, so enterHold
    // does not fire and Tick writes live Progress); keep Last on that
    // airborne scrape bounce is fallSwingCatchScrapeExtendAirEnterHold.
    // Catch scrape then air still handed the hip to −swingHipFlex
    // (forceSwingFlex) while airLiftHold's ThighHold had owned the dirt
    // frame. Keep ThighHold through that air (no Reach latch) is
    // fallSwingCatchScrapeLiftAirHold. That hold still lost the hip on
    // Catch→Reach (CatchScrapedThisPhase cleared, forceSwingFlex on):
    // keep ThighHold through the bounce is
    // fallSwingCatchScrapeLiftAirEnterHold. That keep still lost the hip
    // on Reach→Catch (ReachResumedFromCatchScrapeAir cleared, forceSwingFlex
    // on): keep ThighHold through the return bounce is
    // fallSwingCatchScrapeLiftAirCatchHold. That keep still dropped the
    // foot (CatchReachShinDeg 70°→35°); keep the Reach fold until heel
    // plant is fallSwingCatchScrapeLiftAirCatchFold. That keep is Catch
    // only. Catch→Reach still Lerped fold→landDeg on stale Last; keep
    // the Reach fold on that bounce is
    // fallSwingCatchScrapeLiftAirEnterFold. That keep is the shin. The
    // same Last still Lerped reachArm→carry on the bounce; keep
    // reachArm (skip the extend Lerp) is
    // fallSwingCatchScrapeLiftAirEnterArm. That keep is Catch→Reach.
    // Catch-phase still Lerped reachArm→carry on Last; keep reachArm
    // (skip Last and catchAge Lerps) is
    // fallSwingCatchScrapeLiftAirCatchArm. Recover/Plant CatchEnd still
    // Lerped that Last after Catch flags cleared; start CatchEnd from
    // reachArm is fallSwingCatchScrapeLiftAirCatchEndArm. Grounded Catch
    // still Lerped that Last on toe-down; skip Last and arrive on
    // plant-clock is fallSwingCatchScrapeLiftAirCatchGroundArm. True
    // airborne Catch (never scraped) still Lerped that Last in air;
    // keep reachArm is fallSwingCatchScrapeLiftAirCatchAirArm.
    // Recover/Plant CatchEnd still Lerped that Last after Catch flags
    // cleared (scrape latch is scrape-only); start CatchEnd from
    // reachArm is fallSwingCatchScrapeLiftAirCatchAirEndArm. Grounded
    // never-scrape Catch still Lerped that Last on toe-down; skip Last
    // and arrive on plant-clock is
    // fallSwingCatchScrapeLiftAirCatchAirGroundArm. Grounded
    // Reach still Lerped that Last on toe-down after enterArm; keep
    // reachArm (skip the extend Lerp) is
    // fallSwingCatchScrapeLiftAirEnterGroundArm. That keep is the
    // shoulder. Grounded Reach still Lerped fold→landDeg on that same
    // toe-down (enterFold is air-only); keep the Reach fold is
    // fallSwingCatchScrapeLiftAirEnterGroundFold. Grounded Catch still
    // Lerped fold→landDeg on that same toe-down (catchFold is air-only);
    // keep the Reach fold until heel plant is
    // fallSwingCatchScrapeLiftAirCatchGroundFold. Grounded never-scrape
    // Catch still Lerped fold→landDeg on that same toe-down (catchFold /
    // catchGroundFold are scrape-only); keep the Reach fold until heel
    // plant is fallSwingCatchScrapeLiftAirCatchAirGroundFold. True airborne
    // Catch still Lerped fold→landDeg in air (catchFold is scrape-only);
    // keep the Reach fold is fallSwingCatchScrapeLiftAirCatchAirFold.
    // Never-scrape Catch→Reach still Lerped fold→landDeg on Last
    // (enterFold is scrape-only); keep the Reach fold on that bounce is
    // fallSwingCatchScrapeLiftAirCatchAirEnterFold. That keep is the shin.
    // The same Last still Lerped reachArm→carry on that bounce (enterArm
    // is scrape-only); keep reachArm is
    // fallSwingCatchScrapeLiftAirCatchAirEnterArm. Grounded never-scrape
    // Reach still Lerped that Last on toe-down (enterGroundArm is
    // scrape-only); keep reachArm is
    // fallSwingCatchScrapeLiftAirCatchAirEnterGroundArm. That keep still
    // Lerped fold→landDeg on dirt (enterGroundFold is scrape-only); keep
    // the Reach fold is
    // fallSwingCatchScrapeLiftAirCatchAirEnterGroundFold.
    // Recover/Plant CatchEnd still Lerped that Last after Catch flags
    // cleared (RecoveredFromCatchAir needs heel-down during Catch);
    // start CatchEnd from reachArm after planted Catch enter is
    // fallSwingCatchScrapeLiftAirCatchAirEnterEndArm.
    // Catch-phase still Lerped that Last on the planted Catch
    // (catchAirGroundArm needs CatchPlantAge < PhaseAge); skip Last and
    // arrive on plant-clock is
    // fallSwingCatchScrapeLiftAirCatchAirEnterCatchArm.
    // Catch-phase shin still Max(Last, CatchPlantAge) on that planted
    // Catch (catchAirGroundFold needs !planted); skip Last and arrive
    // on plant-clock is
    // fallSwingCatchScrapeLiftAirCatchAirEnterCatchFold.
    // Recover/Plant still Max(Last, held+PhaseAge) after that skip
    // (Last is the Reach freeze); skip Last and continue plant-clock is
    // fallSwingCatchScrapeLiftAirCatchAirEnterEndFold.
    // Scrape Recover/Plant still Max(Last, held+PhaseAge) after catchEndArm
    // (Last is the Catch-enter freeze); skip Last and continue plant-clock
    // is fallSwingCatchScrapeLiftAirCatchEndFold.
    // Catch planted scrape shin still Max(Last, CatchPlantAge) after
    // catchGroundFold (needs !planted); skip Last and arrive on
    // plant-clock is fallSwingCatchScrapeLiftAirCatchGroundCatchFold.
    // Scrape never-air Recover/Plant still Max(Last, held+PhaseAge)
    // after that Catch skip (CatchScrapedThisPhase cleared, scrape latch
    // needs lift-off); skip Last and continue plant-clock is
    // fallSwingCatchScrapeLiftAirCatchGroundCatchEndFold.
    // Never-scrape air Recover/Plant still Max(Last, held+PhaseAge)
    // after catchAirEndArm (Last is the Reach freeze); skip Last and
    // continue plant-clock is
    // fallSwingCatchScrapeLiftAirCatchAirEndFold.
    // Catch planted never-scrape shin still Max(Last, CatchPlantAge)
    // after catchAirGroundFold (needs !planted); skip Last and arrive
    // on plant-clock is
    // fallSwingCatchScrapeLiftAirCatchAirGroundCatchFold.
    // Plant→Fall leftover still Max(Last, held) after those Recover
    // skips; skip Last and continue plant-clock is
    // fallSwingCatchScrapeLiftAirCatchPlantExitFold.
    public bool ReachResumedFromCatch { get; private set; }
    // Catch→Reach while the toe is down (heel still up). Balance reads
    // this so scrapeLiftHold / scrapeVaultHold / scrapeArmHold can freeze
    // fold / hipLift / ankleLift / leftover vault / arm envelope,
    // scrapeLiftAirAge can resume fold extraAge once the toe leaves,
    // scrapeVaultAirAge can resume leftover vault extraAge once the toe
    // leaves (yield remain on dirt, leftover→0 in air),
    // scrapeArmAirAge can resume arm extraAge once the toe leaves,
    // scrapeBrakeHold can keep the Catch landing brake,
    // scrapeBrakeAirAge can resume Reach lean once the toe leaves,
    // scrapeYieldHold can keep Catch vault yield,
    // scrapeYieldAirAge can resume ApplyFallCatchVault once the toe leaves,
    // scrapeExtendHold can freeze shin/arm extend, and
    // scrapeExtendAirAge can resume that Last once the toe leaves,
    // without also stalling a true airborne bounce. scrapeExtendEnterHold
    // then keeps Last at Catch enter (no Max with live Progress) so that
    // freeze is not undone on the first resumed Reach frame.
    // Catch-phase scrape (still in Catch) is fallSwingCatchScrapeExtendAirAge:
    // that freeze Max'd live Progress after lift-off; resume Last from
    // the same lift-off clock, not this Reach latch. That resume still
    // lost Last on Catch→Reach after lift-off (enterHold is grounded-only);
    // keep Last and latch this flag so Tick does not then write live
    // Progress is fallSwingCatchScrapeExtendAirEnterHold.
    public bool ReachResumedFromScrape { get; private set; }
    // Catch scrape then air bounced to Reach. Balance reads this so
    // scrapeLiftAirEnterHold can keep ThighHold after CatchScrapedThisPhase
    // is cleared: forceSwingFlex would otherwise take −swingHipFlex on
    // the first resumed Reach frame (sci_a, the hip pair of
    // scrapeExtendAirEnterHold). Cleared on Reach exit. True airborne
    // Catch never latches. Quiet when scrapeLiftAirHold is 0.
    // scrapeLiftAirEnterFold then keeps the Reach fold (no 70°→35°) on
    // that same bounce: Last is the Catch-enter freeze, not the fold
    // override, so Lerp(fold, landDeg, Last) would drop the foot.
    // scrapeLiftAirEnterArm keeps reachArm on that airborne bounce;
    // scrapeLiftAirEnterGroundArm keeps it after the toe meets dirt;
    // scrapeLiftAirEnterGroundFold keeps the Reach fold on that same
    // dirt (enterFold is air-only).
    public bool ReachResumedFromCatchScrapeAir { get; private set; }
    // Catch scrape then air bounced Reach→Catch. Balance reads this so
    // scrapeLiftAirCatchHold can keep ThighHold after
    // ReachResumedFromCatchScrapeAir is cleared: forceSwingFlex would
    // otherwise take −swingHipFlex on the first resumed Catch frame
    // (sci_a, the return of scrapeLiftAirEnterHold). scrapeLiftAirCatchFold
    // then keeps the Reach fold (no 70°→35°) on that same airborne toe.
    // scrapeLiftAirCatchGroundFold then keeps that fold after the toe
    // meets dirt (catchFold is air-only).
    // Cleared on Catch exit. Heel plant Catch never latches. True airborne
    // Catch never latches. Quiet when scrapeLiftAirEnterHold is 0.
    public bool CatchResumedFromReachScrapeAir { get; private set; }
    // Catch scrape then air then heel plant → Recover. Balance reads this
    // so scrapeLiftAirCatchEndArm can skip Last-carry in FallCatchArmCatchEnd
    // after CatchScrapedThisPhase / CatchResumedFromReachScrapeAir clear on
    // Catch exit: Recover/Plant would otherwise Lerp reachArm→carry on
    // stale Last against leftover shin. Survives Recover→Plant. Cleared on
    // Fall, standing restore, and a Catch that never scraped-then-aired.
    // Heel-only Catch (no lift-off) never latches. Quiet when catchArm is 0.
    // Recover/Plant shin skip Last after that Catch is catchEndFold.
    // Catch planted scrape shin skip Last (catchGroundFold needs
    // !planted) is catchGroundCatchFold. Recover/Plant shin skip Last
    // after never-air scrape (no lift-off, so this latch never fires) is
    // catchGroundCatchEndFold (RecoveredFromCatchGroundCatch). Plant→Fall
    // leftover skip Last after those Recover skips is catchPlantExitFold.
    public bool RecoveredFromCatchScrapeAir { get; private set; }
    // True airborne Catch (never scraped) then heel plant → Recover.
    // Balance reads this so scrapeLiftAirCatchAirEndArm can skip Last-carry
    // in FallCatchArmCatchEnd after Catch flags clear on Catch exit:
    // RecoveredFromCatchScrapeAir is scrape-only, so Recover/Plant would
    // otherwise Lerp reachArm→carry on stale Last against leftover shin.
    // Survives Recover→Plant. Cleared on Fall, standing restore, and a
    // Catch that scraped or entered already planted. Heel-only Catch
    // (planted at Catch enter) never latches. Catch-phase dirt skip after
    // that fly is catchAirGroundArm (CatchPlantAge < PhaseAge). Shin pair
    // of that dirt skip until heel is catchAirGroundFold. Catch planted
    // never-scrape shin skip Last (catchAirGroundFold needs !planted) is
    // catchAirGroundCatchFold. Quiet when catchAirArm is 0.
    // Recover/Plant shin skip Last after that Catch is catchAirEndFold.
    // Plant→Fall leftover skip Last after that Recover skip is
    // catchPlantExitFold. Planted Catch after never-scrape Catch→Reach is
    // RecoveredFromCatchAirEnter (catchAirEnterEndArm).
    public bool RecoveredFromCatchAir { get; private set; }
    // Never-scrape Catch→Reach then heel plant → Catch (already planted).
    // Balance reads this so scrapeLiftAirCatchAirEnterCatchArm can skip
    // Last-carry in Catch (catchAirGroundArm needs CatchPlantAge <
    // PhaseAge) and scrapeLiftAirCatchAirEnterCatchFold can skip Last
    // in CatchReachExtendProgress (catchAirGroundFold needs !planted).
    // EnterFallCatchPhase also snapshots it so
    // RecoveredFromCatchAirEnter can skip Last-carry in FallCatchArmCatchEnd
    // after ReachResumedFromCatch clears on Catch enter, and skip Last in
    // CatchReachExtendProgress Recover/Plant (catchAirEnterEndFold).
    // Cleared on Catch exit. Quiet when catchAirEnterEndArm is 0.
    public bool CatchResumedFromReachAir { get; private set; }
    // Never-scrape Catch→Reach then planted Catch → Recover. Balance reads
    // this so scrapeLiftAirCatchAirEnterEndArm can skip Last-carry in
    // FallCatchArmCatchEnd after Catch flags clear on Catch exit:
    // RecoveredFromCatchAir needs heel-down during Catch, so Recover/Plant
    // would otherwise Lerp reachArm→carry on stale Last against leftover
    // shin. Survives Recover→Plant. Cleared on Fall, standing restore, and
    // a Catch that did not come from that bounce already planted. Direct
    // never-scrape Catch (heel during Catch) is RecoveredFromCatchAir.
    // Catch-phase skip Last on that planted Catch is catchAirEnterCatchArm
    // (CatchResumedFromReachAir; catchAirGroundArm needs CatchPlantAge <
    // PhaseAge). Catch-phase shin skip Last on that planted Catch is
    // catchAirEnterCatchFold (catchAirGroundFold needs !planted).
    // Recover/Plant shin skip Last after that Catch is catchAirEnterEndFold
    // (CatchReachExtendProgress still Max Last). Plant→Fall leftover
    // skip Last after that Recover skip is catchPlantExitFold. Quiet
    // when catchAirEnterGroundFold is 0.
    public bool RecoveredFromCatchAirEnter { get; private set; }
    // Catch scrape then heel plant, never airborne → Recover. Balance
    // reads this so scrapeLiftAirCatchGroundCatchEndFold can skip Last
    // in CatchReachExtendProgress Recover/Plant, and so
    // FallCatchArmCatchEnd can skip Last-carry after
    // CatchScrapedThisPhase clears on Catch exit:
    // RecoveredFromCatchScrapeAir needs scrapeResumeAirEnterTime, so
    // never-air Recover/Plant would otherwise Max Last / Lerp
    // reachArm→carry on stale Last against leftover shin. Survives
    // Recover→Plant. Cleared on Fall, standing restore, and a Catch that
    // lifted off or never scraped. Scrape-then-air Recover is
    // RecoveredFromCatchScrapeAir. Catch planted scrape shin skip Last
    // (catchGroundFold needs !planted) is catchGroundCatchFold. Quiet
    // when catchGroundArm is 0 or catchEndArm is 0.
    public bool RecoveredFromCatchGroundCatch { get; private set; }
}
