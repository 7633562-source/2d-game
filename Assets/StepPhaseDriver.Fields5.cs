using UnityEngine;

public partial class StepPhaseDriver
{
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

    // A new fall may only start from a body that still stands over its foot.
    // At cycle-1 Plant CoM was already 0.38 m past support (fallCoMOffset is
    // 0.30) and Fall added forward CoP on top: the stance leg then folded
    // (hipR → +90°, pelvisY −1.02 → −1.84). Standing and braking beats that.
    [Tooltip("Max CoM lead over the stance foot (m) that still allows a new Fall. Past this Plant keeps the landing brake instead. Signed: CoM behind the foot is always allowed — that is where a catch leaves the body.")]
    public float plantComLeadMax = 0.10f;

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

    // Tilt cannot see a fold: the body sinks with CoM still over the feet
    // and the trunk inside the abort band (open defect 8 — fell:false at
    // pelvisDrop 0.84). Height is the quantity that moves. The reference is
    // the measured standing CoM at spawn, then a pose that passed the gate
    // on two feet. The pelvis spawn column is ~0.28 m above live CoM (leg
    // mass hangs under the pelvis) and must not arm this guard. Before a
    // stand has been measured the guard stays inert instead of guessing.
    [Tooltip("CoM drop (m) below the last validated standing height that counts as a collapse: any phase returns to a two-foot standing Plant. 0 = off.")]
    public float sinkAbortDrop = 0.25f;

    [Tooltip("Pelvis sag (m) still counted as standing when judging the CoM bar. Quiet sag is 0.0017; a fold is ~0.84. Not a CoM reference.")]
    public float pelvisStandSlack = 0.12f;

    // Deep crouch (crouchKneeFlex 100) drops the pelvis ~0.42 m — past
    // sinkAbortDrop — with CoM still over the feet and the trunk inside
    // crouchTorsoLean (18°). Height alone then fires the emergency Plant
    // as if the body had folded (defect 8). Collapse through the same
    // height is either still falling or already a heap; a held squat is
    // neither. Uncommanded drop keeps the original height-only abort.
    [Tooltip("Downward CoM speed (m/s) that, during a requested crouch, still counts as collapsing through sinkAbortDrop. A held / slowly lowering squat stays above this.")]
    public float sinkCollapseVy = -1.5f;

    [Tooltip("Torso tilt (deg) past which a low CoM during crouch is a heap, not a squat. Above crouchTorsoLean (18), below fallAbortDeg (30).")]
    public float sinkCollapseTiltDeg = 25f;

    // Cold BeginWalk with both feet off the ground: geometric lead can hand
    // stance to the trailing foot, and the brake then holds a leg that is
    // not going to carry anything. The lower foot lands first.
    [Tooltip("Both feet airborne: pick the lower foot as stance instead of the geometric lead. 0 = former lead-only pick.")]
    public float plantAirLowestFoot = 1f;
}
