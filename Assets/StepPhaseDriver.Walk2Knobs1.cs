using UnityEngine;

public partial class StepPhaseDriver
{
    // Lower bound of the Plant→Fall gate, read together with
    // plantLeadWaitSeconds. plantComLeadMax is the upper bound and stops a
    // fall that starts too LATE; nothing stopped one that starts too early,
    // and early is the normal case — a catch plants the new stance foot ahead
    // of CoM, so Plant begins on a negative lead and Fall used to be released
    // the instant plantMinDuration expired.
    //
    // A body over one foot is an inverted pendulum about the CoP, and it
    // topples the way we walk only while the CoP sits BEHIND the CoM. The
    // sole is finite: the CoP cannot go further back than the heel, which is
    // footSize.x/2 = 0.13 m behind the foot centre that StanceComLead
    // measures from. With CoM still behind the foot the whole gravitational
    // moment arm left for Fall is (lead + 0.13) — a couple of centimetres —
    // so Fall spends fallKneeRampSeconds barely moving and Reach then lifts
    // the leg anyway. That is the shuffle: the swing is released before the
    // body has anything to fall with, and single support starts with CoM
    // behind its own support, which is a fall backward, not a step.
    [Tooltip("Signed CoM lead (m) over the stance foot the extrapolated CoM must reach before a new Fall starts. 0 = former release on plantMinDuration alone.")]
    public float fallComLeadTrigger = 0.015f;

    [Tooltip("Forward pelvis lean target in Fall (deg). Stage 2 reads this in BalanceController.")]
    public float fallPelvisLean = 12f;

    // The field above is named for Fall, but Fall was the one phase that
    // zeroed it: BalanceController wrote pelvisLean/torsoLean = 0 there and
    // applied lean × 0.75 in Reach. Fall is the toppling phase and the only
    // one where both feet are still down, so the third-law reaction of a
    // trunk lean is shared by two grounded legs and the stance ankle keeps
    // full authority (ankleScale 1 in Fall, 0.25 in Reach). Leaving it at 0
    // also put a 9° step into the pelvis and lumbar targets on the Fall→Reach
    // edge. Same 0.75 as Reach so the two phases join continuously; 0 = the
    // former upright Fall.
    [Tooltip("Fraction of fallPelvisLean applied forward during Fall. 0 = former upright Fall.")]
    public float fallForwardLeanScale = 0.75f;

    [Tooltip("Swing knee flex in Catch (deg). Stage 2 reads this in BalanceController.")]
    public float catchKneeFlex = 55f;

    [Tooltip("Min Catch age (s) before Recover. The landing ankle needs a few steps to bite.")]
    public float catchMinDuration = 0.22f;

    [Tooltip("Max Catch age (s). Hold the plant while the ankle/hip brake the trunk; do not leave while the swing is still in the air.")]
    public float catchMaxDuration = 0.55f;

    [Tooltip("Swing foot centre must be this far ahead of CoM (m) to finish Catch. Under ~0.08 m the plant sits under the hip and cannot restore.")]
    public float catchFootLead = 0.08f;

    // Where the foot must land is a constant here, and BalanceController's
    // walkStepCatchLeadSync raises the commanded step to exactly this number —
    // so 0.08 m, not the balance law, decides every landing point. Foot
    // placement is what sets how much forward speed survives a step: land ON
    // the capture point (CoM + v/omega0) and the body arrives at the top of
    // the new arc with zero speed, land short of it and the roll carries on,
    // land BEYOND it and the body stops and rocks back. At the ~0.05 m/s this
    // gait actually carries, the capture point is about 0.02 m ahead of CoM,
    // so a fixed 0.08 m plants the sole a capture point too far on every
    // cycle. That is a stop commanded once per step — the same defect
    // walkStepCaptureTarget removed from the stride floor, still alive in the
    // criterion the stride is now synced to, and the shape of the flat
    // comTravelX 0.208 m.
    // Two more things come off the same constant: the CoM then has to travel
    // 0.08 m before it is over the new stance foot at all, which is the
    // Plant roll-over wait (FallLeadReady) spent standing, and the heel edge
    // is only footSize.x/2 = 0.13 m from the foot centre, so a 0.10 m
    // commanded lead leaves the next Plant almost no sole behind the CoM to
    // brake on.
    // One-sided by the Clamp: the requirement may only come DOWN from the
    // tuned catchFootLead, never up, so a fast body (v/omega0 already past
    // it) keeps today's number and today's trajectory. omega0 comes from
    // balance.walkXCoMHeight — the same height the stance ankle, the
    // placement solve and FallLeadReady already read, so the judge, the
    // command and the release all measure one quantity.
    // 0 = the former constant.
    [Tooltip("1 = Catch lead requirement is the capture point (catchFootLead caps it). 0 = former constant 0.08 m.")]
    public float catchLeadCapture = 1f;

    // Floor of that requirement. A body at rest has a capture point on top of
    // its own CoM, and a foot planted under the hip has no moment arm about
    // the CoM at all (that is what catchFootLead was guarding). The foot
    // centre still keeps its whole sole: at 0.035 m ahead of CoM the toe edge
    // is 0.165 m ahead of it, so the plant has arrest authority to spare.
    [Tooltip("Shortest Catch lead requirement (m) when the capture point is smaller.")]
    public float catchFootLeadMin = 0.035f;

    // The command is issued in flight and the criterion is read at touchdown,
    // so the CoM advances a little in between: same role as
    // BalanceController.walkStepCatchLeadPad, on the criterion side.
    [Tooltip("Metres added to the capture point in the Catch lead requirement.")]
    public float catchLeadCaptureMargin = 0.015f;

    [Tooltip("Swing knee flex in Fall and Reach (deg), including while the foot is still down.")]
    public float reachKneeFlex = 140f;

    // Longer moment arm on the plant, same touchdown height. walk2_blend2
    // landed with hip 46° / knee 61°, which puts the foot 0.20 m ahead of
    // the hip; extending the shin to 35° moves it to 0.39 m while the foot
    // stays 0.72 m below the hip, so it lands at the same moment. A fully
    // straight leg is not an option: hip height in stance is already the
    // leg length, so the swing can only touch down while the body descends.
    [Tooltip("Swing knee flex at touchdown (deg). Reach extends the shin from reachKneeFlex to this: same landing height, longer forward moment arm.")]
    public float reachKneeLandDeg = 35f;

    [Tooltip("Fraction of the reach spent folding before the shin starts to extend.")]
    public float reachExtendStart = 0.25f;

    // walk2_blend2 Fall: the grounded swing knee went 10°→57° in 0.08 s and
    // the trunk picked up −174 deg/s before the foot even left the ground.
    // That is a knee buckle, not a forward fall. Same end flex, slower target.
    [Tooltip("Seconds over which the grounded swing knee reaches reachKneeFlex in Fall.")]
    public float fallKneeRampSeconds = 0.15f;

    // airHeel (21:42) cocks the toe up, but Catch/Recover still treated any
    // contact as a plant. The toe is 0.195 m past the ankle, the heel 0.065 m
    // behind, so a scrape counted as landing, straightened the knee, and
    // parked the foot under the hip — catchFootLead never accumulated.
    // Plant = heelLoaded (BodyStateEstimator, already cached). A toe scrape
    // stays Reach: the fold and the airborne ankle pose keep working.
    // Hip pose stays grounded-gated (−swingHipFlex on a loaded toe is sci_a).
    // 0 = former any-contact plant. CLI -fallSwingHeelPlant.
    [Tooltip("Fall/Reach/Catch: 1 = Catch plant is heelLoaded, not any contact. 0 = grounded.")]
    public float fallSwingHeelPlant = 1f;

    // Heel plant (21:31) kept a scrape in Reach, but ReachExtendProgress still
    // treated any contact as "foot down" and returned 0: the shin jumped back
    // to 140°. At that flex the ankle sits near the hip, so airHeel cannot
    // put the heel on the dirt (heel is 0.065 m from the ankle). Continue
    // the air-clock extend toward reachKneeLandDeg while only the toe is
    // loaded — that drops the ankle, the heel can plant, Catch can start.
    // Full heelLoaded still holds the fold here (Catch takes landDeg).
    // Hip pose stays grounded-gated. 0 = former reset on any contact.
    // CLI -fallSwingScrapeExtend.
    [Tooltip("Reach: 1 = keep shin extend on a toe scrape after air. 0 = reset extend on any contact.")]
    public float fallSwingScrapeExtend = 1f;

    // plantStanceKnee holds landDeg (35°) for the WHOLE Plant, so the new
    // stance carries the body at kneeBaseAngle + 35 = ~41°. A two-link leg
    // is 2*L*cos(knee/2) hip-to-ankle: at L 0.43 that is 0.8056 m against
    // 0.8600 m straight — the pelvis rides 5.4 cm low through all of double
    // support, and the only extension in the cycle is the Fall vault, which
    // arrives after the phase gate opens and finishes in 0.075…0.15 s.
    // That is a shuffle, not a walk: the vaulting inverted pendulum of human
    // gait rises over the support DURING stance, and that rise is the knee
    // extensor doing the propulsive work in double support (the trailing
    // ankle push, walkPlantPushScale, is the other half and it currently
    // pushes a sunk pelvis). It also costs the next swing 5.4 cm of the
    // clearance every lift lever is fighting for.
    // Extend the same pose from landDeg toward straight on the roll-over the
    // phase is already waiting for: PlantComReady measures the signed CoM
    // lead over that foot, so the fraction is 0 while the CoM is still behind
    // the plant (early loading — extending there would push the body BACK,
    // which is why the leading knee must not extend before mid-stance) and
    // full as the lead reaches plantComLeadMax, the instant Plant→Fall
    // releases. The exact complement of walkPlantRockLead, which fades out on
    // the same measure: the rock setpoint drives the roll, this takes over as
    // the roll arrives. Continuous both ways, and one-sided — the strut can
    // only get LONGER, the direction the Fall vault already goes, so the
    // pelvis cannot sink from this path. 0 = former frozen landDeg.
    // CLI -walkPlantMidExtend.
    [Tooltip("Plant: extend the new stance knee from reachKneeLandDeg toward straight as the CoM rolls over that foot (mid-stance extension). 0 = former frozen landDeg for the whole phase.")]
    public float fallSwingPlantMidStanceExtend = 1f;

    // vaultRamp already continues Fall→Reach on the stance strut. The swing
    // lift did not: hipLift, ankleLift and reachKnee sat on Fall's PhaseAge,
    // then Reach commanded the full fold / bias on the first airborne frame
    // because leaving Fall resets that age. Early lift is the intended
    // Fall→Reach gate (!grounded before fallKneeRampSeconds). A 70°→140°
    // knee plus a step-1 hipLift is the same scissor yank the Fall ramp
    // was written to avoid, one phase later. Continue the vault clock onto
    // those three; timeout-to-Reach (clock already 1) is unchanged.
    // 0 = former instant full Reach lift. CLI -fallSwingReachLiftClock.
    [Tooltip("Reach: 1 = continue Fall hip/ankle/knee lift on the knee-ramp clock. 0 = former instant full lift on Reach enter.")]
    public float fallSwingReachLiftClock = 1f;

    // Catch airborne past catchMax used to bounce Reach↔Catch (walk2_blend2
    // freeze). Re-entering Reach reset reachAirEnterTime to now, so
    // ReachExtendProgress jumped to 0 and the shin snapped back to the
    // Fall fold (~140°) while the foot was still in the air — the same
    // PhaseAge-reset yank as catchExtendClock, the other edge. Keep the
    // air clock; the fold / hipLift / ankleLift resume-at-1 is
    // fallSwingCatchReachLiftClock, and finishing that ramp on the new
    // Reach PhaseAge (still without LastCatch) is fallSwingCatchReachLiftAge,
    // not this extend-clock flag.
    // Timeout-to-Reach from Fall is unchanged (that enter still starts
    // the air window). 0 = former reset.
    // CLI -fallSwingCatchReachResume.
    [Tooltip("Catch→Reach in air: 1 = keep the Reach extend clock. 0 = former reset (shin refolds to 140°).")]
    public float fallSwingCatchReachResume = 1f;

    // catchReachResume already keeps the air clock on Catch→Reach in free
    // air. Catch timeout also fires on a toe scrape (!heelLoaded, still
    // grounded): resume then required !grounded, so a scrape reset
    // reachAirEnterTime to -1 and lastReachExtendProgress to 0 — shin
    // 35°→140° with the toe on dirt. That is the scrapeExtend yank on the
    // bounce. At 140° the ankle sits near the hip, airHeel cannot drop
    // the heel, catchFootLead never accumulates. Resume on !planted (the
    // same gate as the Catch timeout), not only free air. scrapeExtend
    // already treats a toe as air for the extend clock; this is that
    // same contact on the phase edge. Lift / arm / vault clocks already
    // follow ReachResumedFromCatch. scrapeLiftHold then freezes that lift
    // clock on this scrape so liftAge does not finish 70°→140° on dirt.
    // scrapeVaultHold then freezes that vault extraAge so leftover does
    // not finish →0 on the planted heel. scrapeArmHold then freezes that
    // arm extraAge so the leftover envelope does not finish ~0.5→1 on
    // dirt (third-law pair of the fold / leftover yank). Timeout-to-Reach
    // from Fall is unchanged (that enter is not Catch→Reach). 0 = former
    // air-only resume. CLI -fallSwingCatchReachScrapeResume.
    [Tooltip("Catch→Reach: 1 = also keep the extend clock on a toe scrape. 0 = former air-only resume (scrape refolds to 140°).")]
    public float fallSwingCatchReachScrapeResume = 1f;

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
}
