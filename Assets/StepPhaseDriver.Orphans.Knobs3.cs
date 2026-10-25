using UnityEngine;

public partial class StepPhaseDriver
{
    // Fall→Reach air now fades at 16° (fallSwingReachHipLiftCap). Bias is
    // still 0.25: at vertical liftSignal = −0.25·16/20 = −0.20, so a
    // 16° Fall thigh that drops a few degrees never walks back into that
    // room and the foot replants (swingFoot stays high). 0.5 is −0.40
    // at vertical and still fades at 16° — not −swingHipFlex / sci_a 40°.
    // Reach only; Catch scrape (ReachResumedFromScrape) may write 0.5
    // (fallSwingCatchReachScrapeHipLift).
    // Quiet when 0: former 0.25. Zero stay zero so -fallSwingHipLift 0
    // still disables. MixHipHold (08:56) already shipped.
    // CLI -fallSwingReachHipLift.
    [Tooltip("Reach hipLift amount: 1 = 0.5 bias (Fall→Reach air). 0 = former 0.25.")]
    public float fallSwingReachHipLift = 1f;

    // Fall→Reach air now writes hipLift 0.5 at 16° (fallSwingReachHipLift).
    // ankleLift is still 0.25: plantarflex * (1−0.25) − 0.25 leaves 0.5 of
    // leftover push on µ=0.4, so the 16°/0.5 hipLift still slides and the
    // foot replants (swingFoot stays high). 0.5 fade cancels a full push
    // to 0 — same as FallAnkleLift, not a 1.0 dorsiflexor. Reach only;
    // Catch air may write the same 0.5 (fallSwingCatchAirAnkleLift);
    // Catch scrape may write the same 0.5 (fallSwingCatchScrapeAnkleLift);
    // Catch→Reach scrape may write the same 0.5
    // (fallSwingCatchReachScrapeAnkleLift). Quiet when 0:
    // former 0.25. Zero stay zero so -fallSwingAnkleLift 0 still
    // disables. MixHipHold (08:56) already shipped.
    // CLI -fallSwingReachAnkleLift.
    [Tooltip("Reach ankleLift: 1 = 0.5 fade (Fall→Reach air). 0 = former 0.25 fade.")]
    public float fallSwingReachAnkleLift = 1f;

    // Fall→Reach air now writes 16° / 0.5 hipLift / 0.5 ankleLift
    // (fallSwingReachHipLiftCap / HipLift / AnkleLift). liftSignal is
    // still P to the cap: at 16° room = 0 so liftSignal = 0, and hold
    // only fires past vertical going back. Gravity then drops the thigh
    // (~6 cm of knee on a 0.43 m segment) and the foot replants
    // (swingFoot stays high). A 0.5 fade floor holds that operating
    // point (liftSignal −0.25 at the cap when hipLift is 0.5) and
    // decays over the 4° deadzone past it — not −swingHipFlex / sci_a
    // 40°. Reach only; Catch scrape (ReachResumedFromScrape) may floor
    // the same way (fallSwingCatchReachScrapeHipLiftCapHold). Quiet when
    // 0: former P=0 at the cap.
    // MixHipHold (08:56) already shipped.
    // CLI -fallSwingReachHipLiftCapHold.
    [Tooltip("Reach hipLift floor: 1 = hold 0.5 fade at the 16° cap (Fall→Reach air). 0 = former fade-to-zero at the cap.")]
    public float fallSwingReachHipLiftCapHold = 1f;

    // Fall lift-off now writes 16° / 0.5 hipLift / 0.5 ankleLift
    // (fallSwingFallHipLiftCap / HipLift / AnkleLift). liftSignal is
    // still P to the cap: at 16° room = 0 so liftSignal = 0, and hold
    // only fires past vertical going back. Gravity then drops the Fall
    // thigh (~6 cm of knee on a 0.43 m segment) and the foot replants
    // before Fall→Reach (air-gated) can start — swingFoot stays high.
    // Reach already floors that fade at 0.5 (fallSwingReachHipLiftCapHold);
    // this is the same floor in Fall, so the 16° operating point holds
    // against gravity (liftSignal −0.25 at the cap when hipLift is 0.5)
    // and decays over the 4° deadzone past it — not −swingHipFlex /
    // sci_a 40°. Fall only; Catch scrape is Reach. Quiet when 0: former
    // P=0 at the cap. MixHipHold (08:56) already shipped.
    // CLI -fallSwingFallHipLiftCapHold.
    [Tooltip("Fall hipLift floor: 1 = hold 0.5 fade at the 16° cap (Fall lift-off). 0 = former fade-to-zero at the cap.")]
    public float fallSwingFallHipLiftCapHold = 1f;

    // Fall hipLift floor now holds the 16° cap against gravity
    // (fallSwingFallHipLiftCapHold). The swing chain still scrapes when
    // leftover→0 shares the 0.15 s fold clock: cycle-0 quiet knee is
    // 6°→0 (~2 mm of height) while 140° fold pulls a closed chain, so
    // the stance is the shorter leg until late in the ramp. Double the
    // vault clock (still a ramp, not vaultRamp 0 first-frame pop) so
    // the strut wins the height fight first. Reach continues that same
    // doubled clock inside ApplyFallCatchVault — a Fall-only multiply
    // would snap leftover back toward plantKnee on Fall→Reach.
    // Not another hipLift fade (sci_a). Quiet when 0: former same-clock
    // vault. CLI -fallSwingFallVaultLead.
    [Tooltip("Fall/Reach vault: 1 = leftover→0 at 2× fold clock (stance height first). 0 = former same-clock vault.")]
    public float fallSwingFallVaultLead = 1f;

    // Vault lead now finishes leftover→0 at 2× fold clock, so the strut
    // is tall first. hipLift / ankleLift still shared that 0.15 s fold
    // ramp: at 0.075 s vault is already 1 while lift is only half of 0.5,
    // so the unweighted foot stays on dirt. Double those two (still a
    // ramp, not instant 0.5; 140° fold stays 1×). Reach / Catch air
    // continue that clock — a Fall-only multiply snaps hipLift down on
    // Fall→Reach. Catch scrape may double the same way
    // (fallSwingCatchScrapeLiftLead).
    // Not sci_a 40° and not another fade. Quiet when 0: former
    // same-clock lift. CLI -fallSwingFallLiftLead.
    [Tooltip("Fall/Reach lift: 1 = hipLift/ankleLift at 2× fold clock (foot up first). 0 = former same-clock lift.")]
    public float fallSwingFallLiftLead = 1f;

    // Vault / lift / fold now all finish at 2×, so Fall/Reach can put the
    // thigh at 16° with a short chain. Timeout Catch still faded hipLift
    // at 8°: a 16° Reach thigh is already past that cap (liftSignal 0,
    // hold only fires past vertical going back). The thigh drops ~8°
    // (~6 cm of knee) and the foot replants — swingFoot stays high.
    // Same 16° fade-out as Fall/Reach, Catch air only; Catch scrape
    // (grounded, heel still up) may use 16°
    // (fallSwingCatchScrapeHipLiftCap). Still far from sci_a 40°.
    // Not 3× clock. Quiet when 0: former 8°. MixHipHold (08:56)
    // already shipped. CLI -fallSwingCatchAirHipLiftCap.
    [Tooltip("Catch air hipLift cap: 1 = 16° past vertical (Fall/Reach air hold). 0 = former 8° cap.")]
    public float fallSwingCatchAirHipLiftCap = 1f;

    // Catch air now fades at 16° (fallSwingCatchAirHipLiftCap). liftSignal
    // is still P to the cap: at 16° room = 0 so liftSignal = 0, and hold
    // only fires past vertical going back. Gravity then drops the Catch
    // thigh (~6 cm of knee on a 0.43 m segment) and the foot replants —
    // swingFoot stays high. Fall already floors that fade at 0.5
    // (fallSwingFallHipLiftCapHold); Reach already floors it
    // (fallSwingReachHipLiftCapHold). This is the same floor in Catch
    // air, so the 16° operating point holds against gravity (liftSignal
    // −0.125 at the cap while hipLift is still 0.25) and decays over
    // the 4° deadzone past it — not −swingHipFlex / sci_a 40°. Catch
    // air only; Catch scrape (grounded) may floor the same way
    // (fallSwingCatchScrapeHipLiftCapHold). Quiet when
    // 0: former P=0 at the cap. MixHipHold (08:56) already shipped.
    // CLI -fallSwingCatchAirHipLiftCapHold.
    [Tooltip("Catch air hipLift floor: 1 = hold 0.5 fade at the 16° cap (timeout Catch air). 0 = former fade-to-zero at the cap.")]
    public float fallSwingCatchAirHipLiftCapHold = 1f;

    // Catch air now fades at 16° and floors that fade at 0.5
    // (fallSwingCatchAirHipLiftCap / CapHold). Bias is still 0.25: at
    // the cap liftSignal = −0.125 vs Fall/Reach −0.25 (those write
    // hipLift 0.5). Gravity then drops the Catch thigh and the foot
    // replants — swingFoot stays high. 0.5 is −0.25 at the cap — same
    // as Fall/Reach, still fades over the 4° deadzone, not
    // −swingHipFlex / sci_a 40°. Catch air only; Catch scrape
    // (grounded) keeps 0.25. Quiet when 0: former 0.25. Zero stay
    // zero so -fallSwingHipLift 0 still disables. MixHipHold (08:56)
    // already shipped. Not 3× clock.
    // CLI -fallSwingCatchAirHipLift.
    [Tooltip("Catch air hipLift amount: 1 = 0.5 bias (timeout Catch air). 0 = former 0.25.")]
    public float fallSwingCatchAirHipLift = 1f;

    // Catch air now writes hipLift 0.5 at 16°
    // (fallSwingCatchAirHipLift). ankleLift is still 0.25: plantarflex
    // * (1−0.25) − 0.25 leaves 0.5 of leftover push on µ=0.4, so the
    // 16°/0.5 hipLift still slides and the foot replants (swingFoot
    // stays high). 0.5 fade cancels a full push to 0 — same as
    // FallAnkleLift / ReachAnkleLift, not a 1.0 dorsiflexor. Catch
    // air only; Catch scrape (grounded, heel still up) may write the
    // same 0.5 (fallSwingCatchScrapeAnkleLift).
    // Quiet when 0: former 0.25. Zero stay zero so -fallSwingAnkleLift
    // 0 still disables. MixHipHold (08:56) already shipped. Not 3×
    // clock. CLI -fallSwingCatchAirAnkleLift.
    [Tooltip("Catch air ankleLift: 1 = 0.5 fade (timeout Catch air). 0 = former 0.25 fade.")]
    public float fallSwingCatchAirAnkleLift = 1f;

    // Catch air now holds 16° / hipLift 0.5 / ankleLift 0.5 / floor 0.5
    // (fallSwingCatchAirHipLiftCap / HipLift / AnkleLift / CapHold).
    // Catch scrape (grounded, heel still up) still faded at 8°: a 16°
    // Fall/Reach/Catch-air thigh is already past that cap (liftSignal 0,
    // hold only fires past vertical going back). The thigh drops ~8°
    // (~6 cm of knee) and the foot replants — swingFoot stays high.
    // Same 16° fade-out as Fall/Reach/Catch air, Catch scrape only;
    // planted Catch (heel loaded) still zeroes thighHold. hipLift 0.5 is
    // fallSwingCatchScrapeHipLift; floor is
    // fallSwingCatchScrapeHipLiftCapHold; ankleLift 0.5 is
    // fallSwingCatchScrapeAnkleLift. Still far
    // from sci_a 40°. Not 3× clock. Quiet when 0: former 8°. MixHipHold
    // (08:56) already shipped. CLI -fallSwingCatchScrapeHipLiftCap.
    [Tooltip("Catch scrape hipLift cap: 1 = 16° past vertical (Fall/Reach/Catch-air hold). 0 = former 8° cap.")]
    public float fallSwingCatchScrapeHipLiftCap = 1f;

    // Catch scrape now fades at 16° (fallSwingCatchScrapeHipLiftCap).
    // liftSignal is still P to the cap: at 16° room = 0 so liftSignal = 0,
    // and hold only fires past vertical going back. Gravity then drops the
    // scrape thigh (~6 cm of knee on a 0.43 m segment) and the foot
    // replants — swingFoot stays high. Fall already floors that fade at 0.5
    // (fallSwingFallHipLiftCapHold); Reach already floors it
    // (fallSwingReachHipLiftCapHold); Catch air already floors it
    // (fallSwingCatchAirHipLiftCapHold). This is the same floor in Catch
    // scrape, so the 16° operating point holds against gravity (liftSignal
    // −0.125 at the cap while hipLift is still 0.25) and decays over
    // the 4° deadzone past it — not −swingHipFlex / sci_a 40°. Catch
    // scrape only (grounded, heel still up); planted Catch still zeroes
    // thighHold. hipLift on scrape may then write 0.5
    // (fallSwingCatchScrapeHipLift); ankleLift may then write 0.5
    // (fallSwingCatchScrapeAnkleLift). Quiet when
    // 0: former fade-to-zero at the cap. MixHipHold (08:56) already shipped.
    // CLI -fallSwingCatchScrapeHipLiftCapHold.
    [Tooltip("Catch scrape hipLift floor: 1 = hold 0.5 fade at the 16° cap (Catch scrape). 0 = former fade-to-zero at the cap.")]
    public float fallSwingCatchScrapeHipLiftCapHold = 1f;

    // Catch scrape now fades at 16° and floors that fade at 0.5
    // (fallSwingCatchScrapeHipLiftCap / CapHold). Bias is still 0.25: at
    // the cap liftSignal = −0.125 vs Fall/Reach/Catch-air −0.25 (those
    // write hipLift 0.5). Gravity then drops the scrape thigh and the foot
    // replants — swingFoot stays high. 0.5 is −0.25 at the cap — same as
    // Fall/Reach/Catch-air, still fades over the 4° deadzone, not
    // −swingHipFlex / sci_a 40°. Catch scrape only (grounded, heel still
    // up); planted Catch still zeroes thighHold. ankleLift on scrape
    // may then write 0.5 (fallSwingCatchScrapeAnkleLift). Quiet when 0:
    // former 0.25. Zero stay zero so
    // -fallSwingHipLift 0 still disables. MixHipHold (08:56) already
    // shipped. Not 3× clock.
    // CLI -fallSwingCatchScrapeHipLift.
    [Tooltip("Catch scrape hipLift amount: 1 = 0.5 bias (Catch scrape). 0 = former 0.25.")]
    public float fallSwingCatchScrapeHipLift = 1f;

    // Catch scrape now writes hipLift 0.5 at 16°
    // (fallSwingCatchScrapeHipLift). ankleLift is still 0.25: plantarflex
    // * (1−0.25) − 0.25 leaves 0.5 of leftover push on µ=0.4, so the
    // 16°/0.5 hipLift still slides and the foot replants (swingFoot
    // stays high). 0.5 fade cancels a full push to 0 — same as
    // FallAnkleLift / ReachAnkleLift / CatchAirAnkleLift, not a 1.0
    // dorsiflexor. Catch scrape only (grounded, heel still up); planted
    // Catch still zeroes thighHold. Quiet when 0: former 0.25. Zero stay
    // zero so -fallSwingAnkleLift 0 still disables. MixHipHold (08:56)
    // already shipped. Not 3× clock.
    // CLI -fallSwingCatchScrapeAnkleLift.
    [Tooltip("Catch scrape ankleLift: 1 = 0.5 fade (Catch scrape). 0 = former 0.25 fade.")]
    public float fallSwingCatchScrapeAnkleLift = 1f;

    // Catch scrape now holds 16° / hipLift 0.5 / ankleLift 0.5 / floor 0.5
    // (fallSwingCatchScrapeHipLiftCap / HipLift / AnkleLift / CapHold).
    // hipLift / ankleLift still share the 1× fold ramp: Fall/Reach/Catch-air
    // already finish those two at 2× (fallSwingFallLiftLead), so at 0.075 s
    // vault=1 and air lift=1 while scrape lift is only half of 0.5 — the
    // unweighted jammed toe stays on dirt (swingFoot stays high). Double
    // those two on Catch scrape (still a ramp, not instant 0.5; not 3×
    // vault/lift/fold; not sci_a 40°). Reach resumed from scrape continues
    // that clock — a Catch-only multiply snaps hipLift down on Catch→Reach.
    // Catch air keeps fallSwingFallLiftLead. Quiet when 0: former 1×
    // scrape lift. MixHipHold (08:56) already shipped.
    // CLI -fallSwingCatchScrapeLiftLead.
    [Tooltip("Catch scrape lift: 1 = hipLift/ankleLift at 2× fold clock (jammed toe up first). 0 = former same-clock scrape lift.")]
    public float fallSwingCatchScrapeLiftLead = 1f;

    // Catch scrape now holds 16° / hipLift 0.5 / ankleLift 0.5 / floor 0.5
    // / lift clock 2× (cap / HipLift / AnkleLift / CapHold / LiftLead).
    // The 16° cap has room=0 at a 16° Fall/Reach/Catch-air thigh: floor
    // 0.5 holds −0.25, but the thigh cannot walk further (~1.7 cm of a
    // 0.43 m segment) and the jammed toe stays on dirt (swingFoot stays
    // high). 24° is still far from sci_a 40°: same fade + 4° deadzone,
    // not −swingHipFlex. At 16° thigh, room is 8° so the scrape can
    // walk into that clearance. Catch scrape only (grounded, heel still
    // up); planted Catch still zeroes thighHold. Fall may then use 24°
    // (fallSwingFallHipLiftCapWide); Reach may then use 24°
    // (fallSwingReachHipLiftCapWide); Catch-air may then use 24°
    // (fallSwingCatchAirHipLiftCapWide). Plant may then use 24°
    // (fallSwingPlantHipLiftCapWide).
    // Catch→Reach scrape already has 24°
    // (fallSwingCatchReachScrapeHipLiftCapWide). Not 3× clock. Quiet when
    // 0: former 16°. MixHipHold (08:56) already shipped.
    // CLI -fallSwingCatchScrapeHipLiftCapWide.
    [Tooltip("Catch scrape hipLift cap: 1 = 24° past vertical (16° hold). 0 = former 16° cap.")]
    public float fallSwingCatchScrapeHipLiftCapWide = 1f;

    // Catch scrape now holds 16° / hipLift 0.5 / ankleLift 0.5 / floor 0.5
    // / lift clock 2× (cap / HipLift / AnkleLift / CapHold / LiftLead);
    // that 16° cap may then use 24° (fallSwingCatchScrapeHipLiftCapWide).
    // Catch→Reach on that scrape
    // (ReachResumedFromScrape) still faded at 8°: a 16° Catch-scrape
    // thigh is already past that cap (liftSignal 0, hold only fires
    // past vertical going back). The thigh drops ~8° (~6 cm of knee
    // on a 0.43 m segment) and the foot replants — swingFoot stays high.
    // Same 16° fade-out as Fall/Reach/Catch-air/Catch-scrape, Catch→Reach
    // scrape only; Fall→Reach air still uses fallSwingReachHipLiftCap.
    // That 16° Catch→Reach scrape still faded to liftSignal 0 at the cap;
    // fallSwingCatchReachScrapeHipLiftCapHold floors it at 0.5. hipLift
    // 0.5 on that scrape resume is fallSwingCatchReachScrapeHipLift;
    // ankleLift 0.5 on that scrape resume is
    // fallSwingCatchReachScrapeAnkleLift. Still far from sci_a 40°. Not 3×
    // clock. Quiet when 0: former 8°. MixHipHold (08:56) already shipped.
    // CLI -fallSwingCatchReachScrapeHipLiftCap.
    [Tooltip("Catch→Reach scrape hipLift cap: 1 = 16° past vertical (Catch scrape hold). 0 = former 8° cap.")]
    public float fallSwingCatchReachScrapeHipLiftCap = 1f;

    // Catch→Reach scrape now fades at 16° (fallSwingCatchReachScrapeHipLiftCap).
    // liftSignal is still P to the cap: at 16° room = 0 so liftSignal = 0,
    // and hold only fires past vertical going back. Gravity then drops the
    // resumed scrape thigh (~6 cm of knee on a 0.43 m segment) and the foot
    // replants — swingFoot stays high. Fall already floors that fade at 0.5
    // (fallSwingFallHipLiftCapHold); Reach already floors it
    // (fallSwingReachHipLiftCapHold); Catch air already floors it
    // (fallSwingCatchAirHipLiftCapHold); Catch scrape already floors it
    // (fallSwingCatchScrapeHipLiftCapHold). This is the same floor on
    // Catch→Reach scrape, so the 16° operating point holds against gravity
    // (liftSignal −0.125 at the cap while hipLift is still 0.25) and
    // decays over the 4° deadzone past it — not −swingHipFlex / sci_a 40°.
    // Catch→Reach scrape only (ReachResumedFromScrape); Fall→Reach air
    // still uses fallSwingReachHipLiftCapHold. hipLift on that resume
    // may then write 0.5 (fallSwingCatchReachScrapeHipLift); ankleLift
    // may then write 0.5 (fallSwingCatchReachScrapeAnkleLift). Quiet when
    // 0: former fade-to-zero at the cap.
    // MixHipHold (08:56) already shipped.
    // CLI -fallSwingCatchReachScrapeHipLiftCapHold.
    [Tooltip("Catch→Reach scrape hipLift floor: 1 = hold 0.5 fade at the 16° cap (Catch scrape bounce). 0 = former fade-to-zero at the cap.")]
    public float fallSwingCatchReachScrapeHipLiftCapHold = 1f;

    // Catch→Reach scrape now fades at 16° and floors that fade at 0.5
    // (fallSwingCatchReachScrapeHipLiftCap / CapHold). Bias is still 0.25:
    // at the cap liftSignal = −0.125 vs Fall/Reach/Catch-air/Catch-scrape
    // −0.25 (those write hipLift 0.5). Gravity then drops the resumed
    // scrape thigh and the foot replants — swingFoot stays high. 0.5 is
    // −0.25 at the cap — same as those, still fades over the 4° deadzone,
    // not −swingHipFlex / sci_a 40°. Catch→Reach scrape only
    // (ReachResumedFromScrape); Fall→Reach air still uses
    // fallSwingReachHipLift. ankleLift on that resume may then write
    // 0.5 (fallSwingCatchReachScrapeAnkleLift). Quiet when 0: former 0.25.
    // Zero stay zero so -fallSwingHipLift 0 still disables. MixHipHold
    // (08:56) already shipped. Not 3× clock.
    // CLI -fallSwingCatchReachScrapeHipLift.
    [Tooltip("Catch→Reach scrape hipLift amount: 1 = 0.5 bias (Catch scrape bounce). 0 = former 0.25.")]
    public float fallSwingCatchReachScrapeHipLift = 1f;

    // Catch→Reach scrape now writes hipLift 0.5 at 16°
    // (fallSwingCatchReachScrapeHipLift). ankleLift is still 0.25:
    // plantarflex * (1−0.25) − 0.25 leaves 0.5 of leftover push on µ=0.4,
    // so the 16°/0.5 hipLift still slides and the foot replants
    // (swingFoot stays high). 0.5 fade cancels a full push to 0 — same as
    // FallAnkleLift / ReachAnkleLift / CatchAirAnkleLift /
    // CatchScrapeAnkleLift, not a 1.0 dorsiflexor. Catch→Reach scrape
    // only (ReachResumedFromScrape); Fall→Reach air still uses
    // fallSwingReachAnkleLift. Quiet when 0: former 0.25. Zero stay
    // zero so -fallSwingAnkleLift 0 still disables. MixHipHold (08:56)
    // already shipped. Not 3× clock.
    // CLI -fallSwingCatchReachScrapeAnkleLift.
    [Tooltip("Catch→Reach scrape ankleLift: 1 = 0.5 fade (Catch scrape bounce). 0 = former 0.25 fade.")]
    public float fallSwingCatchReachScrapeAnkleLift = 1f;

    // Catch→Reach scrape now holds 16° / hipLift 0.5 / ankleLift 0.5 /
    // floor 0.5 / lift clock 2× (cap / HipLift / AnkleLift / CapHold /
    // inherited FallCatchScrapeLiftLeadClock). The 16° cap has room=0 at
    // a 16° Catch-scrape thigh: floor 0.5 holds −0.25, but the thigh
    // cannot walk further (~1.7 cm of a 0.43 m segment) and the jammed
    // toe stays on dirt (swingFoot stays high). 24° is still far from
    // sci_a 40°: same fade + 4° deadzone, not −swingHipFlex. At 16°
    // thigh, room is 8° so the bounce can walk into that clearance.
    // Catch→Reach scrape only (ReachResumedFromScrape); Fall / Reach /
    // Catch-air keep 16°. Catch scrape may then use 24°
    // (fallSwingCatchScrapeHipLiftCapWide). Not 3× clock. Quiet when 0:
    // former 16°. MixHipHold (08:56) already shipped.
    // CLI -fallSwingCatchReachScrapeHipLiftCapWide.
    [Tooltip("Catch→Reach scrape hipLift cap: 1 = 24° past vertical (16° hold). 0 = former 16° cap.")]
    public float fallSwingCatchReachScrapeHipLiftCapWide = 1f;

    [Tooltip("Legacy: no longer used in IsTransferReady (BC lag).")]
    public float transferStandLegLevelMax = 0.05f;
}
