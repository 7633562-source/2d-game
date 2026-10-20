using UnityEngine;

public partial class StepPhaseDriver
{
    // scrapeLiftHold latches extraAge=0 for the whole resumed Reach, so a
    // toe that later lifts never finishes the leftover fold (hipLift /
    // ankleLift / 70°→140°). That is the 23:18 stall again, one contact
    // later: dirt freeze was right, air freeze is not. Continue extraAge
    // from lift-off only — not PhaseAge (scrape time saturates the 0.15 s
    // ramp on the first air frame). Vault extraAge resume is
    // scrapeVaultAirAge. Arm extraAge resume is scrapeArmAirAge.
    // Timeout-to-Reach from Fall / scrape freeze on dirt as they were.
    // Quiet when scrapeLiftHold is 0 (extraAge is already PhaseAge).
    // 0 = former extraAge=0 even after lift-off.
    // CLI -fallSwingCatchReachScrapeLiftAirAge.
    [Tooltip("Catch→Reach scrape then air: 1 = resume fold extraAge from lift-off. 0 = former extraAge=0 for the whole resumed Reach.")]
    public float fallSwingCatchReachScrapeLiftAirAge = 1f;

    // scrapeLiftHold already freezes hipLift / ankleLift / fold extraAge
    // on Catch→Reach with the toe down. Vault extraAge still added the
    // new Reach PhaseAge onto FallCatchFallReachClock: Catch airVaultRemain
    // had held leftover at the Fall+Reach remainder, the bounce then
    // finished that remainder toward 1 (Lerp leftover→0) while the toe
    // is on dirt. That is leftover→0 on the only planted heel — the
    // vault pair of scrapeLiftHold's 70°→140° fold. Freeze extraAge
    // (same Catch enter clock as airVaultRemain). Air Catch→Reach is
    // unchanged (that bounce must still finish the remainder). scrapeArmHold
    // then freezes the shoulder extraAge on this scrape. Timeout-to-Reach
    // from Fall / lift as they were. 0 = former scrape resume that still
    // ramps vaultAge. CLI -fallSwingCatchReachScrapeVaultHold.
    [Tooltip("Catch→Reach scrape: 1 = freeze the trailing vault at the Catch enter clock. 0 = former vaultAge ramp (leftover→0 on dirt).")]
    public float fallSwingCatchReachScrapeVaultHold = 1f;

    // scrapeVaultHold latches extraAge=0 for the whole resumed Reach, so a
    // toe that later lifts never finishes leftover→0 (stanceVault stays
    // at the Catch enter remainder). scrapeYieldHold is that same freeze
    // on the default path: Catch remain, no leftover plantKnee. That is
    // the 00:29 stall again, the strut: dirt freeze was right, air freeze
    // is not. Continue extraAge from lift-off only — not PhaseAge (scrape
    // time saturates the 0.15 s ramp) and not leftover plantKnee (yield
    // still holds on dirt). Arm extraAge resume is scrapeArmAirAge.
    // Timeout-to-Reach from Fall / scrape freeze on dirt as they were.
    // Quiet when scrapeVaultHold is 0 and scrapeYieldHold is 0 (extraAge
    // is already PhaseAge / ApplyFallCatchVault). 0 = former extraAge=0
    // / frozen remain even after lift-off.
    // CLI -fallSwingCatchReachScrapeVaultAirAge.
    [Tooltip("Catch→Reach scrape then air: 1 = resume vault extraAge from lift-off. 0 = former extraAge=0 / frozen remain for the whole resumed Reach.")]
    public float fallSwingCatchReachScrapeVaultAirAge = 1f;

    // scrapeLiftHold / scrapeVaultHold already freeze fold and leftover
    // vault extraAge on Catch→Reach with the toe down. Arm extraAge still
    // added the new Reach PhaseAge onto FallCatchArmReachStart: Catch
    // airArmHold had frozen the envelope at ~0.5, the bounce then finished
    // that remainder toward 1 (Lerp reachArm→carry via extend, and the
    // envelope itself ~0.5→1) while the toe is on dirt. That is the
    // shoulder pair of scrapeLiftHold's 70°→140° fold — third law yanks
    // the trunk while airHeel still cannot drop the heel. Freeze extraAge
    // (same Catch enter clock as airArmHold). Air Catch→Reach is unchanged
    // (that bounce must still finish the envelope). scrapeArmAirAge then
    // resumes extraAge once the toe leaves (this freeze otherwise stalls
    // ~0.5→1 in air). scrapeBrakeHold then
    // keeps the Catch landing brake on this scrape so Reach lean does
    // not cut the stance CoP. Timeout-to-Reach from Fall / lift / vault
    // as they were. 0 = former scrape resume that still ramps armAge.
    // CLI -fallSwingCatchReachScrapeArmHold.
    [Tooltip("Catch→Reach scrape: 1 = freeze the arm envelope at the Catch enter clock. 0 = former armAge ramp (~0.5→1 on dirt).")]
    public float fallSwingCatchReachScrapeArmHold = 1f;

    // scrapeArmHold latches extraAge=0 for the whole resumed Reach, so a
    // toe that later lifts never finishes ~0.5→1 (reachArm stays at
    // FallCatchArmReachStart). That is the 00:37 stall again, the
    // shoulders: dirt freeze was right, air freeze is not. Continue extraAge
    // from lift-off only — not PhaseAge (scrape time saturates the 0.15 s
    // ramp). Timeout-to-Reach from Fall / scrape freeze on dirt as they were.
    // Quiet when scrapeArmHold is 0 (extraAge is already PhaseAge).
    // 0 = former extraAge=0 even after lift-off.
    // CLI -fallSwingCatchReachScrapeArmAirAge.
    [Tooltip("Catch→Reach scrape then air: 1 = resume arm extraAge from lift-off. 0 = former extraAge=0 for the whole resumed Reach.")]
    public float fallSwingCatchReachScrapeArmAirAge = 1f;

    // scrapeLiftHold / scrapeVaultHold / scrapeArmHold already freeze
    // fold / leftover vault / arm extraAge on Catch→Reach with the toe
    // down. The trunk envelope still switched: Catch RecoverBrake
    // (ankleScale 1, signed CoP, signed lean) dropped to Reach's
    // calm-reduced stance ankle (Lerp 1→0.25) plus forward pelvis/torso
    // lean while the swing toe is jammed and the stance heel is the
    // only real plant. That is the trunk pair of scrapeArmHold's
    // ~0.5→1 shoulder yank — third law on the stance, not the
    // shoulders, while airHeel still cannot drop the heel. Keep the
    // Catch brake on this scrape; Reach swing pose (frozen clocks)
    // still runs. Air Catch→Reach is unchanged (the foot is up; the
    // body must keep falling). Timeout-to-Reach from Fall / lift /
    // vault / arm as they were. scrapeBrakeAirAge then resumes Reach
    // lean once the toe leaves (this hold otherwise stalls Catch brake
    // in air). scrapeYieldHold then keeps Catch vault yield (no leftover
    // stancePlantKnee) on that same scrape. scrapeYieldAirAge then
    // takes ApplyFallCatchVault once the toe leaves.
    // 0 = former scrape resume that still
    // took the Reach lean. CLI -fallSwingCatchReachScrapeBrakeHold.
    [Tooltip("Catch→Reach scrape: 1 = keep the Catch landing brake on the stance. 0 = former Reach lean / 0.25 ankle on dirt.")]
    public float fallSwingCatchReachScrapeBrakeHold = 1f;

    // scrapeBrakeHold latches Catch RecoverBrake for the whole resumed
    // Reach, so a toe that later lifts never takes Reach lean (stance
    // ankle stays at 1, signed CoP, no forward pelvis/torso). That is
    // the 00:42 stall again, the trunk: dirt freeze was right, air freeze
    // is not. Resume the Reach envelope from lift-off only — the foot is
    // up; the body must keep falling. Not PhaseAge (this is not a ramp)
    // and not extraAge (RecoverBrake vs Reach lean is a switch).
    // Timeout-to-Reach from Fall / scrape freeze on dirt as they were.
    // Quiet when scrapeBrakeHold is 0 (envelope is already Reach lean).
    // 0 = former Catch brake even after lift-off.
    // CLI -fallSwingCatchReachScrapeBrakeAirAge.
    [Tooltip("Catch→Reach scrape then air: 1 = resume Reach lean from lift-off. 0 = former Catch brake for the whole resumed Reach.")]
    public float fallSwingCatchReachScrapeBrakeAirAge = 1f;

    // scrapeVaultHold already freezes vault extraAge on Catch→Reach with
    // the toe down, so leftover→0 does not finish on dirt. ApplyFallCatchVault
    // still set stancePlantKnee to leftover and Lerp(that, 0, frozenClock).
    // Catch air never does that: yield is the vault fraction from the
    // current strut, and "Do not set stancePlantKnee here — that pose is
    // the NEW stance after swap, not the trailing strut." Early-lift
    // Catch (clock ~0.5) held ~Lerp(quiet, 0, 0.5); scrape Reach then
    // commanded leftover (~35°) and Lerp(35, 0, 0.5) ≈ 17° — a flexor
    // yank on the only planted heel, the stance-knee pair of scrapeBrakeHold
    // cutting the CoP. Keep Catch yield on this scrape (Catch clock:
    // !planted → remain). scrapeVaultAirAge continues that remain on dirt
    // (and in air when this hold stays latched). scrapeYieldAirAge then
    // takes ApplyFallCatchVault once the toe leaves — leftover plantKnee
    // in air is the Reach vault an airborne bounce already uses.
    // Air Catch→Reach still vaults. Timeout-to-Reach from Fall / lift /
    // vault extraAge / arm / brake as they were.
    // 0 = former scrape resume that still applied leftover plantKnee.
    // CLI -fallSwingCatchReachScrapeYieldHold.
    [Tooltip("Catch→Reach scrape: 1 = keep Catch vault yield (no leftover stancePlantKnee). 0 = former ApplyFallCatchVault leftover pose on dirt.")]
    public float fallSwingCatchReachScrapeYieldHold = 1f;

    // scrapeYieldHold latches Catch vault yield for the whole resumed
    // Reach, so a toe that later lifts never takes ApplyFallCatchVault
    // (stancePlantKnee stays unset; remain may still tick via
    // scrapeVaultAirAge). That is the 00:54 stall again, the strut pose:
    // dirt freeze was right, air freeze is not. Resume the Reach vault
    // from lift-off only — leftover plantKnee in air, not on dirt.
    // Not PhaseAge (this is an envelope switch, like scrapeBrakeAirAge)
    // and not remain-only (that still skips leftover plantKnee).
    // Timeout-to-Reach from Fall / scrape freeze on dirt as they were.
    // Quiet when scrapeYieldHold is 0 (envelope is already ApplyFallCatchVault).
    // 0 = former Catch yield even after lift-off.
    // CLI -fallSwingCatchReachScrapeYieldAirAge.
    [Tooltip("Catch→Reach scrape then air: 1 = resume ApplyFallCatchVault from lift-off. 0 = former Catch yield for the whole resumed Reach.")]
    public float fallSwingCatchReachScrapeYieldAirAge = 1f;

    // scrapeLiftHold already freezes fold extraAge on Catch→Reach with the
    // toe down, so 70°→140° does not finish on dirt. scrapeExtend still
    // treats a heel-up scrape as air, so ReachExtendProgress keeps
    // ticking: Lerp(frozenFold, landDeg, extend) then finishes 70°→35°
    // on the jammed toe — the shin pair of scrapeLiftHold, and the
    // shoulder lerp(reachArm, carry, extend) pair of scrapeArmHold
    // (LastReachExtendProgress updates every Reach tick when !holdFold).
    // Catch air still extends (drop the foot). Keep the Catch-enter
    // extend on this scrape; airborne Catch→Reach still ramps.
    // scrapeExtendAirAge then resumes Last from lift-off (this freeze
    // otherwise stalls 70°→35° / carry in air). Timeout-to-Reach from
    // Fall / lift / vault / arm / brake / yield as they were.
    // 0 = former scrape resume that still ramped extend.
    // CLI -fallSwingCatchReachScrapeExtendHold.
    [Tooltip("Catch→Reach scrape: 1 = freeze shin/arm extend at the Catch enter clock. 0 = former extend ramp (70°→35° / carry on dirt).")]
    public float fallSwingCatchReachScrapeExtendHold = 1f;

    // scrapeExtendHold latches Last for the whole resumed Reach, so a
    // toe that later lifts never finishes 70°→35° / reachArm→carry
    // (Tick skips Last, Balance forces Last). That is the 00:46 stall
    // again, the shin: dirt freeze was right, air freeze is not.
    // Continue from Catch-enter Last using only lift-off age — not
    // live ReachExtendProgress (scrapeExtend already counted dirt as
    // air, first air frame would jump) and not PhaseAge. Tick writes
    // Last so Catch inherits. Quiet when scrapeExtendHold is 0 (Last
    // already tracks live Progress). 0 = former frozen Last even after
    // lift-off. CLI -fallSwingCatchReachScrapeExtendAirAge.
    [Tooltip("Catch→Reach scrape then air: 1 = resume shin/arm extend from lift-off. 0 = former frozen Last for the whole resumed Reach.")]
    public float fallSwingCatchReachScrapeExtendAirAge = 1f;

    // Same Catch→Reach PhaseAge reset, the stance strut. catchReachResume
    // already holds hipLift / ankleLift / shin at the completed Fall+Reach
    // fold. Vault did not: FallCatchVaultClock on Reach is
    // LastFallDuration+PhaseAge, so a short Fall (the early-lift Reach gate)
    // re-enters at LastFall/ramp and Lerp(landDeg, 0, that) bends a 0°
    // strut back toward 35°. Keep LastReach on that clock. LastCatch on
    // the 0.15 s ramp is the saturate: air Catch is not vault progress
    // (catchAirVaultRemain already held the Fall+Reach remainder; 0.55 s
    // would snap leftover→0 on the only strut). Finishing that remainder
    // on the new Reach PhaseAge is fallSwingCatchReachVaultAge, not this
    // keep-clock flag. Fall→Reach is unchanged (lastReachDuration is the
    // previous cycle). 0 = former LastFallDuration + 0.
    // CLI -fallSwingCatchReachVaultClock.
    [Tooltip("Catch→Reach in air: 1 = keep the Fall+Reach vault clock. 0 = former restart from LastFallDuration.")]
    public float fallSwingCatchReachVaultClock = 1f;

    // catchReachVaultClock kept LastReach+LastCatch so Catch→Reach does
    // not restart at LastFall/PhaseAge 0. LastCatch on the 0.15 s ramp
    // saturates after catchMax: air Catch is not vault progress (same as
    // liftAge / armAge / airVaultRemain). Continue the Fall+Reach
    // remainder on the new Reach PhaseAge — do not add LastCatch.
    // scrapeVaultHold then freezes that extraAge on a toe scrape so the
    // bounce does not finish leftover→0 on dirt. scrapeVaultAirAge then
    // resumes extraAge once the toe leaves (this freeze otherwise stalls
    // leftover→0 in air). scrapeArmHold then
    // freezes the shoulder extraAge on that same scrape. scrapeArmAirAge
    // then resumes that arm extraAge once the toe leaves (this freeze
    // otherwise stalls ~0.5→1 in air). Timeout-to-Reach
    // from Fall / lift / arm as they were. Happy-path
    // (clock already 1) stays 1. Flag 0 is the former LastReach+LastCatch
    // saturate. CLI -fallSwingCatchReachVaultAge.
    [Tooltip("Catch→Reach in air: 1 = keep ramping the Fall+Reach vault remainder on the new Reach PhaseAge (no LastCatch). 0 = former LastReach+LastCatch saturate.")]
    public float fallSwingCatchReachVaultAge = 1f;

    // Same Catch→Reach PhaseAge reset, the shoulders. reachArmClock
    // continues Fall→Reach, but Catch→Reach still started reachArm at 1
    // (!ReachResumedFromCatch). LastReachExtendProgress holds the envelope
    // only when extend is already high; an early Reach gate then trunkGone
    // Catch (extend 0) holds FallCatchArmReachStart (~0.5) for catchMax,
    // then the bounce lerps from 1. Air Catch is not arm progress (same
    // as catchAirArmHold): do not add LastCatch onto the 0.15 s vault
    // clock — that saturates to 1 after 0.55 s. Continue the frozen Catch
    // envelope. Timeout-to-Reach from Fall is unchanged. 0 = former
    // Catch→Reach start at 1. CLI -fallSwingCatchReachArmClock.
    [Tooltip("Catch→Reach in air: 1 = keep the Catch arm envelope (Fall+Reach clock). 0 = former Reach start at 1.")]
    public float fallSwingCatchReachArmClock = 1f;

    // Same Catch→Reach PhaseAge reset, the swing lift. catchReachResume
    // already keeps the air extend clock so ReachExtendProgress does not
    // jump to 0. The fold / hipLift / ankleLift then still forced
    // reachLiftClock = 1, so an early Reach gate (clock ~0.5) then
    // trunkGone Catch (extend 0) jumped CatchReachFoldDeg (~70°) to
    // full 140° on the bounce — the shin pair of catchReachArmClock.
    // LastCatch must not join the 0.15 s vault clock (air Catch is not
    // lift progress; 0.55 s would saturate to 1, same as the arms).
    // Continue the frozen Fall+Reach clock. Timeout-to-Reach from Fall
    // is unchanged. Happy-path (clock already 1) is still full lift.
    // 0 = former Catch→Reach lift at 1. CLI -fallSwingCatchReachLiftClock.
    [Tooltip("Catch→Reach in air: 1 = keep the Fall+Reach lift clock. 0 = former resume at full fold.")]
    public float fallSwingCatchReachLiftClock = 1f;

    // catchReachLiftClock froze LastFall+LastReach so the bounce does not
    // jump 70°→140°. That freeze also stalled the remaining fold: the new
    // Reach PhaseAge is 0, so hipLift / ankleLift / fold stay at ~0.5 for
    // the whole second Reach. Air Catch must still not join the 0.15 s
    // ramp (0.55 s would saturate to 1). Add only the resumed Reach age.
    // Timeout-to-Reach from Fall is unchanged. Happy-path (clock already
    // 1) stays 1. 0 = former freeze with no new PhaseAge.
    // CLI -fallSwingCatchReachLiftAge.
    [Tooltip("Catch→Reach in air: 1 = keep ramping the frozen lift clock on the new Reach PhaseAge. 0 = former freeze at Catch enter.")]
    public float fallSwingCatchReachLiftAge = 1f;

    // catchReachArmClock froze LastFall+LastReach so the bounce does not
    // jump ~0.5→1. That freeze also stalled the remaining envelope: the
    // new Reach PhaseAge is 0, so reachArm stays at FallCatchArmReachStart
    // (~0.5) for the whole second Reach — the shoulder pair of the 23:26
    // lift stall. Air Catch must still not join the 0.15 s ramp (0.55 s
    // would saturate to 1, same as lift). Add only the resumed Reach age.
    // scrapeArmHold then freezes that extraAge on a toe scrape so the
    // bounce does not finish ~0.5→1 on dirt. scrapeArmAirAge then
    // resumes extraAge once the toe leaves (this freeze otherwise
    // stalls ~0.5→1 in air). Timeout-to-Reach from Fall
    // is unchanged. Happy-path (clock already 1) stays 1. 0 = former
    // freeze with no new PhaseAge.
    // CLI -fallSwingCatchReachArmAge.
    [Tooltip("Catch→Reach in air: 1 = keep ramping the frozen arm envelope on the new Reach PhaseAge. 0 = former freeze at Catch enter.")]
    public float fallSwingCatchReachArmAge = 1f;

    // Same airborne Catch window, the trailing strut. Yield still ran on
    // Catch PhaseAge, so a timeout Catch (foot still up) ramped stanceVault
    // 1→0 while that knee was the only support. Catch→Reach then restored
    // vault clock to 1 and snapped ~16° back to 0°. Hold yield at 0 until
    // the heel plants; leftover ramps on catchMin from plant age, not Catch
    // age (air time is not load transfer). Recover continues
    // LastCatchPlantDuration. 0 = former Catch PhaseAge (yield in air).
    // CLI -fallSwingCatchAirVaultHold.
    [Tooltip("Catch: 1 = yield the trailing vault only after heel plant. 0 = former yield on Catch PhaseAge (including air).")]
    public float fallSwingCatchAirVaultHold = 1f;

    // catchAirVaultHold froze yield at clock 0 so air Catch does not
    // collapse 1→0. ApplyCatchVaultYield then set stanceVault=1.
    // Early Reach gate (Fall clock ~0.5) then trunkGone Catch jumped
    // leftover→0 on the only strut — the instant-vault yank, in the air.
    // Hold the Fall+Reach vault remainder (yield = 1 − that clock).
    // LastCatch stays off: air Catch is not vault progress (0.55 s would
    // saturate the 0.15 s ramp to 1, same as lift/arm). Planted Catch
    // continues from that start so the first heel frame does not snap
    // back to yield 0. Happy-path (clock already 1) remain is 0, same
    // as the former hold-at-1. Timeout-to-Reach from Fall / lift / arm
    // as they were. 0 = former hold-at-1.
    // CLI -fallSwingCatchAirVaultRemain.
    [Tooltip("Catch: 1 = air vault stays at the Fall+Reach remainder, then yield from that. 0 = former hold at full vault.")]
    public float fallSwingCatchAirVaultRemain = 1f;

    // Same airborne Catch window, the arms. TryFallCatchArmSwing Catch
    // still lerped reachSwing→carry on Catch PhaseAge, so a timeout Catch
    // (foot still up) dropped the swing to transferCarry while the trunk
    // was still falling. Catch→Reach then restored LastReachExtendProgress
    // from reachArm=1 when extend was still 0 — the third-law pair of the
    // vault 0°→16°→0° on that same bounce (now fallSwingCatchReachArmClock).
    // Hold the Reach envelope until the heel plants; leftover lerps on
    // catchMin from plant age, not Catch age (air time is not load transfer). Recover/Plant Catch-end uses
    // LastCatchPlantDuration so a long air Catch does not count as
    // arrived-at-carry. 0 = former Catch PhaseAge (decay in air).
    // CLI -fallSwingCatchAirArmHold.
    [Tooltip("Catch: 1 = decay arm swing only after heel plant. 0 = former decay on Catch PhaseAge (including air).")]
    public float fallSwingCatchAirArmHold = 1f;

    // Same airborne Catch window, the landing shin leftover.
    // catchExtendClock already continues Reach→Catch, but the planted
    // remainder still ran on Catch PhaseAge. Timeout Catch (foot still
    // up) then counted air time as load transfer: the first heel-loaded
    // frame jumped leftover to 1 (fold→landDeg) on a 0.065 m heel —
    // the catchLandKnee yank, delayed until plant. Recover used
    // LastCatchDuration (air included) and snapped the same way.
    // Plant age is 0 until the heel loads; Recover continues
    // LastCatchPlantDuration. Air Catch still uses ReachExtendProgress.
    // A toe scrape is not that air: scrapeExtend ticks on heel-up, so
    // Catch finished 70°→35° on dirt (fallSwingCatchScrapeExtendHold).
    // That freeze then still Max'd live Progress after lift-off
    // (fallSwingCatchScrapeExtendAirAge).
    // 0 = former Catch PhaseAge / LastCatchDuration.
    // CLI -fallSwingCatchAirExtendHold.
    [Tooltip("Catch: 1 = leftover shin extend only after heel plant. 0 = former leftover on Catch PhaseAge (including air).")]
    public float fallSwingCatchAirExtendHold = 1f;

    // airExtendHold waits for the heel before leftover. Air Catch still
    // Max(Last, ReachExtendProgress) so the foot can drop. scrapeExtend
    // treats a heel-up scrape as air, so that live clock finishes
    // 70°→35° on the jammed toe during Catch — then Catch→Reach
    // scrapeExtendHold freezes Last and snaps the shin back. Keep Last
    // on this scrape; true air still ramps. Timeout-to-Reach from Fall /
    // Catch→Reach scrape clocks as they were. Catch→Reach enter still
    // Max'd Last with that live Progress (fallSwingCatchReachScrapeExtendEnterHold).
    // After Catch scrape then air that keep is grounded-only
    // (fallSwingCatchScrapeExtendAirEnterHold).
    // 0 = former live Progress on a Catch scrape.
    // scrapeExtendAirAge then resumes Last from lift-off (this freeze
    // otherwise Max's live Progress after the toe leaves: scrapeExtend
    // already counted dirt as air).
    // CLI -fallSwingCatchScrapeExtendHold.
    [Tooltip("Catch scrape: 1 = freeze shin extend at Catch enter (Last). 0 = former live ReachExtendProgress (70°→35° on dirt).")]
    public float fallSwingCatchScrapeExtendHold = 1f;

    // scrapeExtendHold (Catch) latches Last while the toe is down, so a
    // later lift-off still Max'd Last with live ReachExtendProgress.
    // scrapeExtend already counted that dirt as air, so the first air
    // frame jumped 70°→35° — the Catch-phase pair of scrapeExtendAirAge
    // on Catch→Reach. Continue from Catch-enter Last using only lift-off
    // age (ScrapeResumeExtendProgress), not live Progress and not
    // PhaseAge. Tick writes Last so Catch→Reach inherits. Quiet when
    // scrapeExtendHold is 0 (Catch already shows live Progress).
    // 0 = former live Progress even after lift-off.
    // CLI -fallSwingCatchScrapeExtendAirAge.
    [Tooltip("Catch scrape then air: 1 = resume shin extend from lift-off. 0 = former live ReachExtendProgress (jump 70°→35°) after lift-off.")]
    public float fallSwingCatchScrapeExtendAirAge = 1f;

    // scrapeExtendAirAge (Catch) already resumed Last from lift-off.
    // Catch→Reach enter still Max(Last, live Progress) because
    // ReachResumedFromScrape is grounded-only: a timeout bounce after that
    // lift-off is an airborne resume, so enterHold does not fire, and the
    // first Reach tick writes live Progress (scrapeExtend counted dirt as
    // air). That is the bounce-frame jump 70°→35° / carry that AirAge just
    // avoided. Keep Last and latch ReachResumedFromScrape so Tick continues
    // from that Last (Reach scrapeExtendAirAge from the bounce, not Catch
    // air age — that would saturate the 0.15 s fold ramp). Airborne
    // Catch→Reach with no scrape is unchanged (catchScrapedThisPhase stays
    // false). Quiet when scrapeExtendAirAge is 0: Catch already showed live
    // Progress; Max copies that into Last so the bounce does not snap
    // 35°→70°. Quiet when Hold is 0: Catch never latches catchScrapedThisPhase.
    // 0 = former enter Max / live Progress after Catch scrape then air.
    // CLI -fallSwingCatchScrapeExtendAirEnterHold.
    [Tooltip("Catch scrape then air Catch→Reach: 1 = keep Catch Last (no Max with live Progress). 0 = former Max (70°→35° / carry on the bounce frame).")]
    public float fallSwingCatchScrapeExtendAirEnterHold = 1f;

    // scrapeExtendHold (Catch) already froze CatchReachShinDeg at Last.
    // Catch→Reach enter still Max(Last, ReachExtendProgress): scrapeExtend
    // ticks on a heel-up toe, so live Progress has already finished
    // 70°→35° / arm carry. That write is the bounce frame of the Catch
    // freeze — scrapeExtendHold (Tick) then holds the NEW Last. Keep Last;
    // airborne Catch→Reach still Max's (the foot is up). Quiet when Catch
    // scrapeExtendHold is 0: Catch was already showing live Progress, and
    // Max copies that into Last so the bounce does not snap 35°→70°.
    // scrapeExtendAirEnterHold is that same keep after Catch scrape then
    // air (enterHold is grounded-only). Timeout-to-Reach from Fall /
    // Catch scrape clocks as they were.
    // 0 = former enter Max. CLI -fallSwingCatchReachScrapeExtendEnterHold.
    [Tooltip("Catch→Reach scrape enter: 1 = keep Catch-enter Last (no Max with live Progress). 0 = former Max (70°→35° / carry on the bounce frame).")]
    public float fallSwingCatchReachScrapeExtendEnterHold = 1f;

    // Same airborne Catch window, the swing lift. Vault / arms / shin leftover
    // already wait for the heel (airVaultHold / airArmHold / airExtendHold).
    // hipLift / ankleLift / thighHold did not: timeout Catch zeroes them, so
    // a toe scrape before heelLoaded hands the swing hip back to the pelvis
    // loop — extensor against the fold, the 0.96 grounded foot, one phase
    // after scrapeExtend. Hold the frozen Fall+Reach lift until the heel
    // plants; Catch must not keep ramping (same as CatchReachFoldDeg extraAge
    // 0). Planted Catch / Recover stay silent so the landing hip takes the
    // trunk. Catch→Reach already resumes that clock (liftAge). LastCatch
    // stays off: air is not lift progress. 0 = former Catch lift at 0.
    // CLI -fallSwingCatchAirLiftHold.
    [Tooltip("Catch: 1 = keep Fall+Reach hip/ankle/thigh lift until heel plant. 0 = former Catch lift at 0 (pelvis loop on a scrape).")]
    public float fallSwingCatchAirLiftHold = 1f;

    // airLiftHold already writes thighHold / hipLift until the heel plants.
    // Catch still sets forceSwingFlex: on dirt ThighHold owns the hip
    // (grounded reach is off), but the first airborne frame after a scrape
    // takes −swingHipFlex — the hip pair of scrapeExtendAirAge's 70°→35°,
    // sci_a on a just-unweighted toe. Keep ThighHold through Catch air;
    // Catch→Reach keep on that same edge is scrapeLiftAirEnterHold.
    // True airborne Catch (never scraped) still forceSwingFlex. Quiet
    // when airLiftHold is 0 (Catch lift already 0, ThighHold does not
    // run). 0 = former −swingHipFlex on Catch scrape then air.
    // CLI -fallSwingCatchScrapeLiftAirHold.
    [Tooltip("Catch scrape then air: 1 = keep ThighHold (no −swingHipFlex). 0 = former forceSwingFlex jump on the first airborne frame.")]
    public float fallSwingCatchScrapeLiftAirHold = 1f;

    // scrapeLiftAirHold already keeps ThighHold through Catch air.
    // Catch→Reach still sets forceSwingFlex: CatchScrapedThisPhase is
    // cleared on exit, so the bounce takes −swingHipFlex — the hip pair
    // of scrapeExtendAirEnterHold's 70°→35° / live Progress. Keep
    // ThighHold through that resumed Reach. True airborne Catch (never
    // scraped) still reaches. Quiet when scrapeLiftAirHold is 0 (Catch
    // already jumped). 0 = former −swingHipFlex on Catch scrape then
    // air then Reach. CLI -fallSwingCatchScrapeLiftAirEnterHold.
    // 01:12 can now clamp Reach air-swing to 32°. That ramp is
    // reachAirHold only (!ReachResumedFromCatch). Catch→Reach scrape-air
    // still skip-added forceSwingFlex (catchScrapeLift) and left hipair
    // on: leftAir is already true, so leftSwingHip starts on and takes
    // Min(angleAtLatch, −swingHipFlex). A 32° leftover yanks to 40°
    // (sci_a). The keep now clears that pose, same as later air holds.
    // Flag 0 is the former skip-add (hipair on the bounce).
    [Tooltip("Catch scrape then air, Catch→Reach: 1 = keep ThighHold (no −swingHipFlex). 0 = former forceSwingFlex jump on the bounce.")]
    public float fallSwingCatchScrapeLiftAirEnterHold = 1f;

    // scrapeLiftAirEnterHold already keeps ThighHold through Catch→Reach.
    // Reach→Catch still sets forceSwingFlex: ReachResumedFromCatchScrapeAir
    // is cleared on exit, so the return bounce takes −swingHipFlex — the
    // hip pair of that Catch→Reach keep, sci_a on the same airborne toe.
    // Keep ThighHold through that resumed Catch. Heel plant Catch stays
    // off (landing hip). True airborne Catch (never scraped) still
    // reaches. Quiet when scrapeLiftAirEnterHold is 0 (Reach already
    // jumped). 0 = former −swingHipFlex on Catch scrape then air then
    // Reach then Catch. CLI -fallSwingCatchScrapeLiftAirCatchHold.
    [Tooltip("Catch scrape then air, Reach→Catch: 1 = keep ThighHold (no −swingHipFlex). 0 = former forceSwingFlex jump on the return bounce.")]
    public float fallSwingCatchScrapeLiftAirCatchHold = 1f;

    // scrapeLiftAirCatchHold already keeps ThighHold through Reach→Catch.
    // Catch still commands CatchReachShinDeg (Lerp fold→landDeg). After a
    // scrape then air that extend is 70°→35° on a foot that just left —
    // the shin pair of the hip keep, the shuffle (swingFoot 0.92). Keep
    // the Reach fold and continue extraAge from lift-off (not PhaseAge:
    // scrape time would saturate the 0.15 s ramp on dirt). Heel plant
    // still takes landDeg. Grounded Catch fold is catchGroundFold.
    // Stance ankle / pelvis loop unchanged. True
    // airborne Catch fold is catchAirFold (shin pair of catchAirArm). Quiet when
    // scrapeLiftAirHold is 0 (Catch hip already jumped) or catchHold is 0
    // on the return bounce. 0 = former landDeg lerp (foot drops).
    // CLI -fallSwingCatchScrapeLiftAirCatchFold.
    [Tooltip("Catch scrape then air: 1 = keep Reach fold until heel plant (no 70°→35°). 0 = former CatchReachShinDeg plant-extend.")]
    public float fallSwingCatchScrapeLiftAirCatchFold = 1f;

    // scrapeLiftAirCatchFold already keeps Reach fold through Catch.
    // Catch→Reach still Lerps fold→landDeg on ReachExtendProgress: Last
    // was the Catch-enter freeze (or AirAge resume), not the fold override,
    // so the bounce is 140°→~56° on the same airborne toe — the shin pair
    // of scrapeLiftAirEnterHold, the shuffle scrapeLiftAirCatchFold just
    // closed. Keep the Reach fold (skip the extend Lerp) until the toe
    // meets dirt; extraAge from lift-off so scrape PhaseAge does not
    // saturate the 0.15 s ramp. Heel plant still Lerps. Grounded Reach
    // fold is enterGroundFold. Never-scrape Catch→Reach fold is
    // catchAirEnterFold (this latch is scrape-only). Quiet
    // when enterHold is 0 (Reach hip already jumped) or catchFold is 0
    // (Catch already dropped the foot). 0 = former landDeg lerp.
    // CLI -fallSwingCatchScrapeLiftAirEnterFold.
    [Tooltip("Catch scrape then air, Catch→Reach: 1 = keep Reach fold (no 70°→35°). 0 = former Lerp(fold, landDeg, extend) on the bounce.")]
    public float fallSwingCatchScrapeLiftAirEnterFold = 1f;

    // scrapeLiftAirEnterFold already keeps Reach fold through Catch→Reach.
    // Catch→Reach still Lerps reachArm→carry on ReachExtendProgress: Last
    // was the Catch-enter freeze (or AirAge resume), not the fold override,
    // so the bounce is ~0.5→carry on the same airborne toe — the shoulder
    // pair of enterFold's 140° keep, a quiet trunk against a folded shin.
    // Keep reachArm (skip the extend Lerp) until the toe meets dirt.
    // Grounded Reach Last-carry is enterGroundArm. Grounded Reach fold
    // after that keep is enterGroundFold. True airborne Catch Last-carry is
    // catchAirArm. Quiet when enterFold is 0 (shin already dropped) or
    // armClock is 0 (legacy restart). 0 = former carry blend.
    // CLI -fallSwingCatchScrapeLiftAirEnterArm.
    [Tooltip("Catch scrape then air, Catch→Reach: 1 = keep reachArm (no Last-carry). 0 = former Lerp(reachArm, carry, extend) on the bounce.")]
    public float fallSwingCatchScrapeLiftAirEnterArm = 1f;

    // scrapeLiftAirEnterArm already keeps reachArm through Catch→Reach.
    // Catch-phase still Lerps FallCatchArmReachStart→carry on Last, then
    // toward carry on catchAge: Last is the Catch-enter freeze (or AirAge
    // resume), not the fold override, so the same airborne toe is
    // ~0.5→carry against catchFold's 140° — the Catch-phase shoulder pair
    // of catchFold, a quiet trunk against a folded shin. Keep reachArm
    // (skip both Last and catchAge Lerps) until the toe meets dirt;
    // extraAge from lift-off so scrape PhaseAge does not saturate the
    // 0.15 s ramp. Grounded Catch Last-carry is catchGroundArm. True
    // airborne Catch (never scraped) Last-carry is catchAirArm. Quiet when catchFold
    // is 0 (shin already dropped) or catchHold is 0 on the return bounce.
    // 0 = former Last-carry. CLI -fallSwingCatchScrapeLiftAirCatchArm.
    [Tooltip("Catch scrape then air: 1 = keep reachArm until heel plant (no Last-carry). 0 = former Lerp(reachArm, carry, Last) in Catch.")]
    public float fallSwingCatchScrapeLiftAirCatchArm = 1f;
}
