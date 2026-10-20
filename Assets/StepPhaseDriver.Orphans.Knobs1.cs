using UnityEngine;

public partial class StepPhaseDriver
{
    [Tooltip("Catch/Recover: metres the ankle CoP target sits behind CoM. Saturated plantarflexor brake; larger than fallComLead.")]
    public float catchComLead = 0.10f;

    // Two legs, one pelvis: 120° of swing knee wants the hip 0.43 m from that
    // ankle, a nearly straight stance wants 0.86 m. If the stance knee is
    // allowed to yield, the pelvis drops and the swing foot never unloads
    // (shuffle, swingFoot ~0.92). 1 = command stance knee to 0 in Fall/Reach
    // so the strut wins the height fight. Not WALK-1 walkStanceExtend (that
    // path stays off while useFallCatchWalk≠0). CLI -fallStanceVault.
    [Tooltip("Fall/Reach: fraction of stance-knee extension toward 0. 1 = full strut.")]
    public float fallStanceVault = 1f;

    // Free swing hip + 140° knee lets the thigh collapse backward (extension)
    // while the foot is still down: that is the "backward first" step, not a
    // heel-to-butt lift. Two-sided world-vertical SPD on this joint torqued
    // the pelvis against the stance loop (20:25, reverted 21:05). This is
    // flexor-only, and only past vertical — a stop, not a pose. 0 = silence.
    // CLI -fallSwingThighHold.
    [Tooltip("Fall/Reach: 0–1, resist swing-thigh extension past world vertical. 0 = silenced hip.")]
    public float fallSwingThighHold = 1f;

    // Stop is not lift. Vertical thigh + 140° knee still slides the foot
    // backward along the dirt (µ=0.4) instead of raising the knee. A constant
    // flexor bias prefers hip flexion so the knee comes up; it is not a
    // −swingHipFlex pose (sci_a, travel −1.11 m). Caps at 8° past vertical
    // in BalanceController; Fall lift-off may use 16°
    // (fallSwingFallHipLiftCap) now that FallAnklePush un-pinned the heel.
    // Reach may use the same 16° (fallSwingReachHipLiftCap); Catch scrape
    // may use the same 16° (fallSwingCatchScrapeHipLiftCap); Catch→Reach
    // scrape may use the same 16° (fallSwingCatchReachScrapeHipLiftCap);
    // Catch scrape may then use 24°
    // (fallSwingCatchScrapeHipLiftCapWide);
    // Catch→Reach scrape may then use 24°
    // (fallSwingCatchReachScrapeHipLiftCapWide);
    // Fall may then use 24° (fallSwingFallHipLiftCapWide);
    // Reach may then use 24° (fallSwingReachHipLiftCapWide);
    // Catch-air may then use 24° (fallSwingCatchAirHipLiftCapWide);
    // Plant may then use 24° (fallSwingPlantHipLiftCapWide);
    // Fall may then use 32° (fallSwingFallHipLiftCapCeil);
    // Reach may then use 32° (fallSwingReachHipLiftCapCeil);
    // Catch-air may then use 32° (fallSwingCatchAirHipLiftCapCeil);
    // Plant may then use 32° (fallSwingPlantHipLiftCapCeil);
    // Catch scrape may then use 32° (fallSwingCatchScrapeHipLiftCapCeil);
    // Catch→Reach scrape may then use 32°
    // (fallSwingCatchReachScrapeHipLiftCapCeil);
    // hipLift /
    // ankleLift on scrape may write 0.5 (fallSwingCatchScrapeHipLift /
    // fallSwingCatchScrapeAnkleLift). Fall may also write 0.5 bias (fallSwingFallHipLift);
    // Reach may write the same 0.5 (fallSwingReachHipLift); Catch scrape
    // may write the same 0.5 (fallSwingCatchScrapeHipLift). Reach may then floor the fade at 0.5
    // (fallSwingReachHipLiftCapHold) so the 16° cap holds against
    // gravity. Fall may floor the same
    // way (fallSwingFallHipLiftCapHold); Reach floor is not this lever.
    // Catch air may floor the same way (fallSwingCatchAirHipLiftCapHold);
    // Catch scrape may floor the same way (fallSwingCatchScrapeHipLiftCapHold);
    // Catch→Reach scrape may floor the same way
    // (fallSwingCatchReachScrapeHipLiftCapHold);
    // Catch→Reach scrape may then write 0.5
    // (fallSwingCatchReachScrapeHipLift).
    // Catch air may then write 0.5
    // (fallSwingCatchAirHipLift) so that floor holds −0.25 at the cap
    // like Fall/Reach; Catch scrape may write the same 0.5
    // (fallSwingCatchScrapeHipLift). Catch air may then
    // write ankleLift 0.5 (fallSwingCatchAirAnkleLift) so leftover
    // plantarflex after FallAnklePush cancels to 0 like Fall/Reach;
    // Catch scrape may write the same 0.5
    // (fallSwingCatchScrapeAnkleLift); Catch→Reach scrape may write
    // the same 0.5 (fallSwingCatchReachScrapeAnkleLift).
    // Fall vault may then finish leftover→0 at 2× the fold clock
    // (fallSwingFallVaultLead) so the strut is tall before the 140°
    // chain shortens; Reach continues that clock. 0 = only the stop.
    // CLI -fallSwingHipLift.
    [Tooltip("Fall/Reach: 0–1, flexor bias on the grounded swing hip so the knee rises. 0 = stop only.")]
    public float fallSwingHipLift = 0.25f;

    // Hip lift raises the knee; a plantarflexing swing ankle still jams the
    // toe into µ=0.4 dirt and the shortening chain slides the foot back
    // (21:12). This fades that toe grip and, if the loop was pushing, adds
    // a small dorsiflexor (toe-up) so the foot can leave with the knee —
    // not an ankle-angle PD (that drops the body) and not a push-off.
    // Forward drive stays on the stance ankle / comLead (09:52: zeroing
    // this ankle without a stance drive ratcheted backward). Fall lift-off
    // may write 0.5 (fallSwingFallAnkleLift); Reach may write the same
    // 0.5 (fallSwingReachAnkleLift); Catch air may write the same 0.5
    // (fallSwingCatchAirAnkleLift); Catch scrape may write the same 0.5
    // (fallSwingCatchScrapeAnkleLift); Catch→Reach scrape may write
    // the same 0.5 (fallSwingCatchReachScrapeAnkleLift). 0 = own-error
    // loop only. CLI -fallSwingAnkleLift.
    [Tooltip("Fall/Reach: 0–1, unweight the grounded swing ankle (fade toe grip, slight toe-up). 0 = own-error loop.")]
    public float fallSwingAnkleLift = 0.25f;

    // Grounded ankleLift can unweight the toe; once the foot is up, the
    // latched air target is joint 0 (foot along the shin). At reachKneeFlex
    // 140° that shin is almost horizontal, so 0 points the toe into the
    // dirt and the first airborne frame replants (swingFoot stays ~1).
    // World-horizontal is the same formula as walkSwingAirLevel, scoped to
    // Fall/Reach/Catch and only while !grounded — not an ankle-angle PD on
    // a planted foot, and not the rejected global airLevel on legacy walk.
    // 0 = joint 0 (former). CLI -fallSwingAirAnkle.
    [Tooltip("Fall/Reach/Catch: 1 = hold the airborne swing foot world-flat. 0 = joint 0 (toe along shin).")]
    public float fallSwingAirAnkle = 1f;

    // World-flat (21:30) keeps the paddle off the dirt at 140° knee, but a
    // level sole still meets the ground toe-and-heel together. The toe is
    // 0.195 m past the ankle, the heel 0.065 m behind, so a flat plant
    // stubs the toe first, replants under the hip, and Catch never makes
    // catchFootLead. Minus degrees from flat = dorsiflex (toe-up): plus
    // ankle is plantarflex (swingAnkleGroundedToeOff +12°). Only while
    // airborne and only in the last ~8 cm of clearance — not an
    // ankle-angle PD on a planted foot, not global walkSwingToeOff.
    // 0 = world-flat only. CLI -fallSwingAirHeel.
    [Tooltip("Fall/Reach/Catch: airborne heel-first, deg of toe-up as clearance drops. 0 = world-flat only.")]
    public float fallSwingAirHeel = 10f;

    // scrapeExtend drops the heel at reachKneeLandDeg. Catch then used to
    // snap the planted shin to 0.15×catchKnee (~8°). That extensor yank on
    // a just-loaded heel (0.065 m behind the ankle) pops the heel off or
    // shortens the moment arm under the hip — the hold-fold after a good
    // plant. Hold landDeg on Catch/Recover while the heel is the plant.
    // 0 = former 0.15 straighten. CLI -fallSwingCatchLandKnee.
    [Tooltip("Catch/Recover: 1 = planted swing knee stays at reachKneeLandDeg. 0 = former 0.15×catchKnee.")]
    public float fallSwingCatchLandKnee = 1f;

    // catchLandKnee holds the landing shin through Catch/Recover. Recover then
    // swaps roles (EnterFallCatchPlant): that shin is now stance, and Plant
    // used to command kneeBaseAngle (~6°) plus walkStanceKneeBend. Same
    // extensor yank, one phase later, on the only loaded foot. Hold landDeg
    // on the new stance. Fall vaults that pose toward 0 on the knee-ramp
    // clock when fallSwingVaultRamp is on — not on the first Fall frame.
    // Quiet first Plant and StandingRestore (same foot, no swap) keep
    // the stance knee. 0 = former quiet stance. CLI -fallSwingPlantStanceKnee.
    [Tooltip("Plant after a catch swap: 1 = new stance knee stays at reachKneeLandDeg. 0 = former quiet stance knee.")]
    public float fallSwingPlantStanceKnee = 1f;

    // plantStanceKnee holds landDeg through Plant. Fall then set stanceVault=1
    // on the first frame: Lerp(knee, 0, 1) is 35°→0 on the only loaded heel —
    // the same extensor yank, one phase later. Ramp that vault on the swing-knee
    // clock so the strut lengthens as the swing shortens, and keep landDeg as
    // the Lerp start (not quiet kneeBaseAngle). Reach continues the clock
    // because leaving Fall resets PhaseAge. 0 = former instant vault.
    // CLI -fallSwingVaultRamp.
    [Tooltip("Fall/Reach: 1 = vault stance knee from landDeg on the knee-ramp clock. 0 = former instant vault to 0.")]
    public float fallSwingVaultRamp = 1f;

    // vaultRamp lengthens the stance to 0 by Catch. Catch/Recover then left
    // stanceVault at 0, so the trailing (old) stance jumped 0° → kneeTarget
    // plus walkStanceKneeBend (~16°) on the first Catch frame — a flexor
    // collapse of the only long strut, pelvis onto the new plant before the
    // catch ankle has bitten. Same family as the 35°→0 Fall yank, other
    // leg. Yield that vault on the catchMin clock; Recover continues
    // because leaving Catch resets PhaseAge. 0 = former instant drop.
    // CLI -fallSwingCatchVaultYield.
    [Tooltip("Catch/Recover: 1 = yield the trailing stance vault on the catchMin clock. 0 = former instant drop to quiet knee.")]
    public float fallSwingCatchVaultYield = 1f;

    // Same PhaseAge reset as the vault yield. TryFallCatchArmSwing used to
    // restart Catch at swing=1 (Reach had already decayed to transferCarry)
    // and Plant returned false, so Recover's 0.05 s tail snapped to quiet.
    // That is a shoulder yank on the landing, the third-law pair of the
    // 0°→16° knee. Continue Reach→Catch→Recover→Plant; first Plant and
    // StandingRestore stay quiet. 0 = former restart / snap.
    // CLI -fallSwingArmClock.
    [Tooltip("Catch/Recover/Plant: 1 = continue Reach arm swing across phase resets. 0 = former Catch restart at 1 and Plant snap to quiet.")]
    public float fallSwingArmClock = 1f;

    // Same PhaseAge reset as reachLiftClock, the shoulders. Fall ramps
    // arm swing 0→1 on PhaseAge; Reach then started at 1, so an early
    // lift (the Reach gate) jumped ~0.5→1 on the first air frame — the
    // third-law pair of the 70°→140° knee. Continue the vault clock;
    // timeout-to-Reach (clock already 1) is unchanged. Catch→Reach used
    // to skip this ramp (reachArm=1); that bounce is
    // fallSwingCatchReachArmClock, not this Fall→Reach flag. 0 = former
    // Reach start at 1.
    // CLI -fallSwingReachArmClock.
    [Tooltip("Reach: 1 = continue Fall arm swing on the knee-ramp clock. 0 = former instant full swing on Reach enter.")]
    public float fallSwingReachArmClock = 1f;

    // Same PhaseAge reset as reachArmClock, the Catch edge. Reach now
    // continues Fall's vault clock, but Catch still built reachSwing from
    // 1. Early lift (the Reach gate) then a heel before the 0.15 s ramp
    // finishes jumped ~0.5→1 on the first Catch frame — the shoulder pair
    // of catchExtendClock (shin still on the Fall ramp). Freeze the same
    // LastFall+LastReach clock as CatchReachFoldDeg; timeout Catch (clock
    // already 1) matches the old start at 1. Recover/Plant Catch-end uses
    // the same start so the 0.05 s Recover tail does not snap. Flag 0 is
    // the former Catch envelope from 1. CLI -fallSwingCatchArmClock.
    [Tooltip("Catch/Recover/Plant: 1 = continue Fall+Reach arm envelope (vault clock). 0 = former Catch start at 1.")]
    public float fallSwingCatchArmClock = 1f;

    // Same PhaseAge reset as the Reach lift clock, other edge: Catch used
    // to command reachKneeLandDeg on the first frame. Early lift is the
    // Reach gate, so that shin is still on the Fall ramp (70°…140°) and
    // ReachExtendProgress has not reached 1 — 70°→35° on a just-loaded
    // heel is the catchLandKnee yank, one phase later. Continue the Reach
    // lerp; freeze the fold clock at Catch enter (do not keep folding);
    // leftover extend ramps on catchMin once the heel is down. Timeout
    // Catch (progress already 1) matches the old landDeg start.
    // 0 = former instant landDeg. CLI -fallSwingCatchExtendClock.
    [Tooltip("Catch/Recover: 1 = continue Reach shin extend across phase reset. 0 = former instant landDeg.")]
    public float fallSwingCatchExtendClock = 1f;

    // scrapeResume already keeps the extend clock on Catch→Reach with the
    // toe down. liftAge then still adds the new Reach PhaseAge onto the
    // frozen Fall+Reach fold: Catch airLiftHold had stopped at ~70°, the
    // bounce resumes toward 140° while the toe is on dirt. At 140° the
    // ankle sits near the hip, airHeel cannot drop the heel — the same
    // plant that scrapeResume was written to save, undone by the shin
    // pair of liftAge. Freeze extraAge (same Catch enter clock as
    // airLiftHold). Air Catch→Reach is unchanged (that bounce must still
    // finish shortening). scrapeVaultHold then freezes that same extraAge
    // on the trailing strut. scrapeVaultAirAge then resumes that vault
    // extraAge once the toe leaves. scrapeArmHold then freezes the shoulder
    // extraAge. scrapeArmAirAge then resumes that arm extraAge once the
    // toe leaves. Timeout-to-Reach from Fall is unchanged.
    // scrapeLiftAirAge then resumes extraAge once the toe leaves
    // (this freeze otherwise stalls 70°→140° in air).
    // 0 = former scrape resume that still ramps liftAge.
    // CLI -fallSwingCatchReachScrapeLiftHold.
    [Tooltip("Catch→Reach scrape: 1 = freeze hip/ankle/fold at the Catch enter clock. 0 = former liftAge ramp (refolds to 140° on dirt).")]
    public float fallSwingCatchReachScrapeLiftHold = 1f;

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
}
