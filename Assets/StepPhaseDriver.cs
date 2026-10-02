using UnityEngine;

// Intent layer: one-leg stance on a timer, transfer — by condition (both feet,
// CoM at center). Do not touch BalanceController.
[DefaultExecutionOrder(-50)]
public class StepPhaseDriver : MonoBehaviour
{
    // WALK-2 limit-cycle walker. Off = legacy Stance/Transfer only.
    public enum FallCatchPhase
    {
        Plant = 0,
        Fall = 1,
        Reach = 2,
        Catch = 3,
        Recover = 4
    }

    private enum Phase
    {
        Stance,
        Transfer
    }

    [Header("Stance")]
    // Keep in sync with HeadlessTrial -walkStance default.
    [Tooltip("Minimum seconds on one leg before requesting transfer.")]
    public float stanceDuration = 6f;
    [Tooltip("Seconds of the first walk stance after input to avoid a frozen start.")]
    public float firstWalkStanceDuration = 2.5f;

    // Run (intent.run): shorter stance — more frequent swaps. Not SIMBICON.
    [Tooltip("Seconds of Stance when intent.run=1.")]
    public float runStanceDuration = 4f;
    [Tooltip("Seconds reduction of walk stance per 1 m/s target speed.")]
    public float walkCadenceStanceGain = 2.0f;
    [Tooltip("Minimum walk stance duration under cadence scaling.")]
    public float walkCadenceMinStance = 2.0f;

    // After the first lift-off: grounded again → Transfer, do not wait for the timer.
    // One air frame is a bounce (heel_w1 fold). Need an air streak.
    // 0 = off (timer stanceDuration only). CLI -walkHeelMin / -walkHeelAir.
    [Tooltip("Min Stance age (s) after qualified air → Transfer.")]
    public float stanceHeelStrikeMinAge = 0f;
    [Tooltip("Min continuous !swingGrounded (s) before heel-strike qualifies. 0 = off.")]
    public float stanceHeelAirMin = 0f;

    // Walk is a forward fall with a planted foot, not a timer-driven step.
    // While Stance holds the timer, walkComLeadX lets the body pass the
    // stance foot and fall face-first (lead_003…lead_015: torso ~92°,
    // pelvisDrop 0.86). Support swap must follow the body: CoM has gone
    // ahead of the stance foot by stanceComTrigger — time to plant the other.
    // This stays a condition inside the phase driver, not a separate finite
    // automaton: do not open SIMBICON. 0 = timer only, former behavior.
    [Tooltip("CoM ahead of the stance foot (m) → Transfer. 0 = timer only.")]
    public float stanceComTrigger = 0.01f;

    [Tooltip("Min Stance age (s) before the CoM trigger fires.")]
    public float stanceComMinAge = 0.25f;
    [Tooltip("Additional stanceComTrigger per 1 m/s target speed.")]
    public float stanceComTriggerPerSpeed = -0.004f;
    [Tooltip("Earliest stance fraction where CoM-trigger may switch to Transfer. 0 = off.")]
    public float walkComTriggerMinStanceFraction = 0f;
    [Tooltip("If |torso tilt| exceeds this in stance, force Transfer. 0 = off.")]
    public float walkTiltAbortDeg = 0f;
    [Tooltip("Allow Transfer->Stance only when |torso tilt| <= this. 0 = off.")]
    public float walkTiltRecoverDeg = 0f;

    [Tooltip("First phase: +1 right stance, −1 left.")]
    public float firstStance = 1f;

    [Header("Fall–catch walk (WALK-2)")]
    // Play/WebGL default 1: W starts this gait. HeadlessTrial CLI default is
    // 0 so a quiet -walk 1 stays the legacy Stance/Transfer path.
    [Tooltip("1 = Plant→Fall→Reach→Catch→Recover FSM. 0 = legacy Stance/Transfer only.")]
    public int useFallCatchWalk = 1;

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

    [Tooltip("Catch/Recover: metres the ankle CoP target sits behind CoM. Saturated plantarflexor brake; larger than fallComLead.")]
    public float catchComLead = 0.10f;

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

    // The ladder above still cannot see two poses. (1) A fold that already
    // sits inside TiltSettled + signed lead ≤ plantComLeadMax — open defect 8:
    // CoM over the feet, trunk calm, pelvis on the ground. BeginCycle then
    // calls that a stand, CaptureStandHeight records the folded CoM as the
    // bar, and Fall starts from a body that has already collapsed. Arm the
    // sink guard from the measured spawn CoM (idle FixedUpdate after
    // BuildHuman, and BeginWalk before walkActive — Start runs before
    // BuildHuman). Do not floor with the pelvis spawn column: that sits
    // ~0.28 m above live CoM and reads quiet stance as collapsed.
    // Keep the measured floor under every later CaptureStandHeight, fail the gate,
    // and surrender cycle-0 Plant to two-foot quiet stance. (2) The last
    // rung required both feet down, so a single-support stall (air start,
    // one foot landed past CoM, the other still up) waited forever. Any
    // contact is enough to hand the quiet two-foot controller the airborne
    // leg; still no surrender in the air. Happy path never reaches either
    // rung: a quiet BeginWalk is not sunk (pelvisDrop 0.0017 vs
    // sinkAbortDrop 0.25) and opens the gate inside plantMinDuration.
    // (3) Fall abort / Reach stall / Catch bounce called EnterStandingPlant
    // with plantStandSurrender false, so a body already at fallAbortDeg
    // (30°) spent plantStallStandSeconds (3 s) in split Plant — that is
    // the fold, not a restore. Immediate two-foot surrender when the
    // pose is already a heap: abort tilt, sink, or cycle-0 |CoM−stance|
    // past RecoverComLostMeters (0.30, the same Fall band). (4) Cycle-0
    // PlantComReady is signed, and the forward bound is RecoverComLostMeters
    // not plantComLeadMax. 0.10 m still shut the gate on the ~10 cm
    // third-law shift from initiation, raised StandingRestore, and killed
    // rock / lean / geo-lift — walkSwap stayed 0. A cold lunge still
    // surrenders at 0.30 m. Cycle-0 tilt allows the commanded Plant lean
    // so 9° initiation cannot fail the 12° TiltSettled band; omega is not
    // in that gate (a 9° lean over plantMinDuration already makes ~18 °/s
    // and a flicker of 40 °/s latched the brake). (5) Cycle-0 FallLeadReady
    // does not wait for 1.5 cm of capture-point lead: stacked feet at rest
    // never grow that number, so Plant sat 1.5 s split and folded. Age is
    // plantMinDuration. Later cycles keep the trigger / plantLeadWait.
    // (6) Cycle-0 Reach/Catch plant is swing-ahead-of-stance, not
    // CoM-ahead: stacked feet fail catchFootLeadMin vs CoM. (7) Cycle-0
    // Fall/Reach abort at fallAbortDeg used to EnterStandingPlant: a 30°
    // lean IS the first fall, so restoring Plant cancelled Catch, Recover
    // never ran, walkSwap stayed 0, travelX 0, fold ~4 s. Cycle 0 Catchs
    // that lean. (8) Catch/Recover judged IsSwingFootPlanted (heel): geo
    // lift pitches the stacked sole onto the toe, so swingDown stayed
    // false and even stall-Catch had no Recover exit. Cycle 0 uses
    // grounded. Later cycles keep heel-first and abort-to-Plant.
    // (9) Those Catchs still left walkSwap=0. PlantTiltReady 17° and
    // RecoverPoseIsLost at fallAbortDeg shut Plant→Fall on the same
    // 30° lean Fall now Catchs — StandingRestore killed geo-lift
    // before the first Fall. Airborne Catch (geo clearance past the
    // 0.06 m probe) bounced Reach at catchMax because every Recover
    // exit needs swingDown; bounceMax then Planted. Grounded Catch
    // ran ApplyRecoverBrake + 0.15×catchKnee on stacked soles, which
    // is a landing snap under the hip, not a 4 cm plant. Cycle 0:
    // Plant tilt does not block Fall; abort tilt is not a lost pose;
    // Fall goes Catch (skip the 1.5 s Reach stall); airborne Catch
    // waits for the sole; Recover on span ≥ FootLeadDeadzone.
    // (10) firstStepPlace Catch then wrote reachKneeFlex 140° after
    // the geo lift. Geo assign needs both soles down, so the first
    // airborne frame of a successful lift took a 140° tuck, vault
    // yield dropped the stance strut, and sinkAbort Planted at ~4 s
    // with walkSwap still 0. Cycle 0 Catch keeps the Fall/geo knee,
    // holds the vault, and ThighHold so the 4 cm placement can land.
    // (11) That still left walkSwap=0. walkStepPlaceOnFold last-wrote
    // Asin(0.04/0.78)≈3° over ThighHold's 16–32° lift hip the moment
    // the knee was ≥20°, and grounded Catch (IsFirstStepPlanted) skipped
    // the air hipLift write, so the thigh dropped on Fall→Catch. µ=1.2
    // never grew FootLeadDeadzone. Cycle 0 Plant/Fall/Catch keeps
    // ThighHold (no 3° place) and Catch keeps hipLift 0.5 until Recover.
    // (12) That still left walkSwap=0. Geo ~50° for 8 cm is the lift;
    // Catch's 40° cap is 3 cm longer — the 0.06 m probe — so the first
    // airborne frame replanted and Recover never saw FootLeadDeadzone.
    // Cycle 0 Catch holds the last grounded geo fold (not 40° / 140°)
    // and skips landing-knee straighten until Recover.
    // (13) That still left walkSwap=0. 11:19 read lastFirstStepGeoKnee
    // from static ComputeFallCatchBalance (does not compile) and only
    // CAPPED 140° — landDeg 35° is below the cap, so the first air
    // frame without catchAirFold replanted; if the cap did hold 8 cm
    // the hovering sole never made swingDown and Catch had no exit.
    // Cycle 0 Catch ASSIGNS the latched standing geo until span ≥
    // FootLeadDeadzone, then landDeg so the 0.06 m probe reads
    // grounded and Recover can swap.
    // (14) That still left walkSwap=0. landDeg 35° with hipLift 0.5
    // still hovered ~8 cm (thigh 16–32° holds the ankle up);
    // Catch requires swingDown, so Recover never ran and sinkAbort
    // Planted at ~4 s. Cycle 0 Catch-ahead drops hipLift / ThighHold
    // / forceSwingFlex (same hipair clear as Recover) so gravity
    // plants the 2 cm span; vault stays. Until ahead, geo + lift hold.
    // (15) That still left walkSwap=0. ThighHold's hold term is 0 at
    // 16–32° (only hipLift floors the cap). Zeroing lift made the hip
    // limp: the sole retracted below FootLeadDeadzone before the 0.06 m
    // probe, and ending firstStepHoldLift on ahead re-armed the 3°
    // place unwind on the same frame. landDeg 35° at a 32° thigh is
    // still ~8 cm short. Cycle 0 Catch-ahead KEEPS hipLift / ThighHold
    // (span) and last-writes LandingKneeFlexDeg so the shin covers the
    // remaining clearance on that hip. firstStepHoldLift stays for the
    // whole Catch (no 3° place). Vault stays. Until ahead, geo + lift.
    // (16) That still left walkSwap=0. Geometry: at standing hip height
    // a 32° thigh cannot reach the ground (0.86·cos32° = 0.73 m, ~13 cm
    // hover). LandingKneeFlexDeg at that leftover asks for ~6° and
    // shortens vertical reach further. Catch has no cycle-0 air exit,
    // sinkAbort ~4 s. Cycle-0 Catch-air plants a matched 15°/15° pose
    // (hip flexion minus, knee plus): inside the 0.06 m probe with
    // ~7 cm span. firstStepHoldLift still blocks the 3° place. hipLift
    // drops only in Catch-air so ThighHold does not hold 32°; thighHold
    // stays so forceSwingFlex does not take −swingHipFlex. Grounded
    // Catch keeps geo + lift until the probe clears. Vault stays.
    // (17) That still left walkSwap=0. 15°/15° plants for one 5 ms
    // frame, then grounded Catch restored hipLift 0.5 (air-lift-hold
    // / Fall lift) and geo ~50°. The sole left the 0.06 m probe again;
    // Recover needs swingDown ∧ span ≥ FootLeadDeadzone for catchMin
    // (0.22 s); sinkAbort ~4 s. Cycle-0 Catch latches the plant after
    // the first airborne frame and holds hipLift 0 through contact.
    // (18) That still left walkSwap=0. 15°/15° matched is 1.5 cm short
    // of GetContacts at standing hip height (useGetContactsProbe, not
    // the 0.06 m box). Combined contactOffset 2 cm only fires on the
    // SPD overshoot through a longer chain. Recover then wrote
    // catchKnee / ApplyRecoverBrake and lifted that sole before
    // RecoverTouchdownMinAge. Cycle-0 plants 8°/6° (shortfall ~4 mm,
    // span ~7.5 cm), latches through Recover, skips catchMin (the
    // 0.05 s Recover hold is the plant). Until air, geo + lift. Vault
    // stays.
    // (19) That still left walkSwap=0. 8°/6° at standing hip height
    // sits ~4 mm above geometric overlap. GetContacts (not the 0.06 m
    // box) then never reports the sole, Catch never sees swingDown,
    // Recover never runs, sinkAbort ~4 s. Cycle 0 treats clearance
    // inside defaultContactOffset as planted — the 4 mm hover IS the
    // contact the offset exists for. Hip SPD to 8° wins over hipair
    // on that same Catch-air frame.
    // (20) That still left walkSwap=0. Geo lift aimed at 8 cm (the old
    // OverlapBox) is a ~50° squat; DualLoad never opens, the stance
    // sags with the fold, GetContacts stays true, Catch never latches
    // 8°/6°, Recover never sees ahead. Cycle 0 geo-lifts 2.5 cm (~28°,
    // past both contactOffsets), plants 10°/10° on a 10° stance-knee
    // hip drop (reach matches H, span ~4.5 cm), latches on geometric
    // separation not GetContacts-only, and PickStanceFromFeet counts
    // NearContact so the swap hands the front foot. Hip SPD to 10°
    // still wins over hipair. Vault / firstStepHoldLift stay.
    // (21) That still left walkSwap=0. Geo 2.5 cm on a loaded stacked
    // sole is still a squat: DualLoad never opens, the hip drops with
    // the fold, GetContacts stays true, Separated never latches, the
    // 10°/10° SPD never runs, sinkAbort ~4 s. Cycle 0 skips geo and
    // the 140° Fall pin. Fall commands the 10°/10° plant (hip SPD,
    // matching stance knee), Catch latches on entry, liftGateBlend = 1
    // so the assigned swing is trailing (unload / friction / cap).
    // Recover still needs span ≥ FootLeadDeadzone. Vault /
    // firstStepHoldLift stay. footFriction stays 1.2.
    // (22) That still left walkSwap=0. 10°/10° on dirt cannot make
    // FootLeadDeadzone: both soles share the load, µ=1.2 friction
    // exceeds hip horizontal force, stance 10° keeps both chains the
    // same length. Cycle 0 Plant/Fall/Catch grounded: 55° swing fold
    // on a knee-0 stance strut (DrivePelvis stays). Latch 8°/6° on
    // the first airborne frame, not Catch entry. Recover still needs
    // span ≥ FootLeadDeadzone. Vault / firstStepHoldLift stay.
    // footFriction stays 1.2.
    // (23) That still left walkSwap=0. Comment 22 wrote DrivePelvis
    // stays, but Fall reachKneeFolding drops it the moment reachKnee
    // > 0.5°, and ApplyFirstStepCycle0Pose also wrote hipLift /
    // ThighHold / forceSwingFlex. Hip flexor on a loaded µ=1.2 sole
    // is sci_a: pelvis drops with the 55° fold, GetContacts never
    // clears, 8°/6° never latches, sinkAbort ~4 s. Cycle 0 grounded:
    // DrivePelvis stays (reachKneeFolding off, plantHipRelease 0),
    // knee-only 55°, stance vault, ankleLift unweight. Latch 8°/6°
    // on the first airborne frame. Recover still needs span ≥
    // FootLeadDeadzone. Vault / firstStepHoldLift stay. footFriction
    // stays 1.2.
    // (24) That still left walkSwap=0. Knee-only 55° on dirt is still
    // a squat: DrivePelvis holds thigh orientation, not hip height.
    // Quiet vault 6°→0 is ~1 mm; 55° shortens hip-to-ankle ~10 cm,
    // both soles stay loaded, µ=1.2 cannot slide, GetContacts never
    // clears. Plant walkPlantSwingKneeFirst also seeded lastPlant
    // 55°, so fold-hold restored the squat after ApplyFirstStep
    // zeroed it. Cycle 0 grounded: no 55° last-write (oneg
    // grounded-knee / unload / cap via liftGateBlend=1), DrivePelvis
    // stays, vault, ankleLift. Latch 8°/6° on the first airborne
    // frame. Recover still needs span ≥ FootLeadDeadzone. Vault /
    // firstStepHoldLift stay. footFriction stays 1.2.
    // (25) That still left walkSwap=0. liftGateBlend=1 was a TIMER,
    // not a load: DualLoadMinSpan 0.04 sat above the 3 cm viewpoint
    // lunge (hipHalfSpacing·cos80°·2), FallLeadReady was auto-true,
    // so oneg 16° / unload 0.32 ran at 50/50. µN at half weight still
    // sticks. DualLoadMinSpan = FootLeadDeadzone (0.02). Fall waits
    // for CoM 1.5 cm past the already-leading right foot. Blend is
    // measured frontShare. 55° / unload only after frontShare ≥ 0.5
    // (N≈0, µ does not stick). Air latches 8°/6°. Recover still
    // needs span ≥ FootLeadDeadzone. Vault / firstStepHoldLift stay.
    // footFriction stays 1.2.
    // (26) That still left walkSwap=0. frontShare ≥ 0.5 on a 3 cm
    // lunge IS rest: (x_com − x_trail)/span = 0.5 at mid-stance, and
    // RMS CoM 5 mm flickers across that gate. Unload 0.32 / 55° ran
    // at 50/50; plantLeadWait then Fall'd the same squat; ApplyFirstStep
    // last-wrote reachKnee 0 on dirt so leftover never survived.
    // DualLoad cycle 0 ramps from StanceLead onto the leading foot
    // (N≈0), not the 3 cm share. FallLeadReady cycle 0 has no
    // deadline. ApplyFirstStep keeps leftover once CoM is on that
    // foot. Air latches 8°/6°. Recover still needs span ≥
    // FootLeadDeadzone. Vault / firstStepHoldLift stay. footFriction
    // stays 1.2.
    // (27) That still left walkSwap=0. FallLeadReady waited for
    // Capture ≥ 1.5 cm PAST a foot that already sits 1.5 cm ahead of
    // CoM. The ankle CoM loop holds RMS 5 mm at mid-stance and
    // cancels the 9° Plant lean, so that 3 cm roll never happens:
    // Plant never EnterFall, DualLoad/55° stay shut (StanceLead < 0),
    // sinkAbort ~4 s, walkSwap=0, travelX=0. Cycle 0: fade the ankle
    // hold until CoM is ON that foot; FallLeadReady is StanceLead ≥ 0
    // (not Capture ≥ trigger, not a deadline). DualLoad / 55° still
    // wait for that same event (trail N≈0). Air latches 8°/6°.
    // Recover still needs span ≥ FootLeadDeadzone. Vault /
    // firstStepHoldLift stay. footFriction stays 1.2.
    // (28) That still left walkSwap=0. Comment 27's ankleScale fade
    // is the actuator of plantRockLead: at rest StanceLead ≈ −1.5 cm
    // so scale went to 0 on frame 1, the leading sole could not
    // dorsiflex, and the 9° trunk lean's third-law shoved the pelvis
    // back. DualLoad then used StanceLead / trigger, which is also 0
    // at the gate, so even a late arrival kept cap/unload shut.
    // Cycle 0: ankleScale stays 1 (rock + lean roll CoM onto the
    // leading foot). FallLeadReady is still StanceLead ≥ 0. DualLoad
    // is 1 at that same event, not StanceLead / trigger. Air latches
    // 8°/6°. Recover still needs span ≥ FootLeadDeadzone. Vault /
    // firstStepHoldLift stay. footFriction stays 1.2.
    // (29) That still left walkSwap=0. Rock + 9° lean are multiplied
    // by calm² (~0.3 at 9°). Cycle 0 skipped trailing A2 / hip
    // ("no rear arm") while walkPlantStanceDriveFirst extended the
    // LEADING hip at StanceLead ≈ −1.5 cm — that hip is a brake
    // until CoM has arrived. The 3 cm lunge IS the rear arm. Cycle 0:
    // trailing A2 + hip on, leading-hip drive only once StanceLead ≥ 0,
    // trailWindow doses by the 3 cm span not 8 cm. FallLeadReady /
    // DualLoad still StanceLead ≥ 0. Air latches 8°/6°. Recover still
    // needs span ≥ FootLeadDeadzone. Vault / firstStepHoldLift stay.
    // footFriction stays 1.2.
    // (30) That still left walkSwap=0. RecoverBlend is raw² with
    // tiltN = tilt/(0.7·abort): at the 9° initiation, calm ≈ 0.33, so
    // plantRockLead 3 cm arrives as 1 cm and the mid-stance ankle loop
    // wins. Comment 29's trailing A2 on a 3 cm lunge / 26 cm foot puts
    // CoP at the toe, ahead of CoM — a brake, not a rear arm. Cycle 0:
    // rock not × calm (Plant and the Fall hold), trailing A2 / hip
    // later-cycle only, leading-hip drive still only once StanceLead ≥ 0.
    // FallLeadReady / DualLoad still StanceLead ≥ 0. Air latches 8°/6°.
    // Recover still needs span ≥ FootLeadDeadzone. Vault /
    // firstStepHoldLift stay. footFriction stays 1.2.
    // (31) That still left walkSwap=0. Rock without calm is a couple on
    // a µ=1.2 stacked pair: the leading heel presses, the trailing sole
    // pins X, StanceLead stays −1.5 cm. FallLeadReady never fires. The
    // CoM translation that can close that gap is the geometric trunk-
    // mass shift from the 9° Plant lean (~2.7 cm at half-body / 0.35 m).
    // That lean was still × RecoverBlend (~3°, ~0.9 cm) and faded on
    // Capture, so any forward velocity from the couple killed it before
    // CoM reached the leading foot. Cycle 0: lean not × calm, leanRoom
    // 1 until EnterFall, Fall hold keeps that 9°. Rock stays not × calm.
    // Trailing A2 / hip later-cycle only. Leading-hip drive still only
    // once StanceLead ≥ 0. FallLeadReady / DualLoad still StanceLead ≥ 0.
    // Air latches 8°/6°. Recover still needs span ≥ FootLeadDeadzone.
    // Vault / firstStepHoldLift stay. footFriction stays 1.2.
    // (32) That still left walkSwap=0. The 9° lean about a free hip
    // assumes the hip is a fixed pivot. Two pinned soles (µ=1.2) make
    // it a bow: hips go back, trunk goes forward, whole-body CoM stays
    // at mid, StanceLead stays −1.5 cm. The translator is the trailing
    // CoM loop — both ankles share the leading-foot error, so the trail
    // dorsiflexes (heel, CoP behind CoM). walkPlantSwingAnkleReleaseFirst
    // faded that loop on the same plantMinDuration clock as rockClock,
    // product ≤ 0.25, then the pin returned. Cycle 0: keep the trail CoM
    // loop until StanceLead ≥ 0; clock fade is the unload at DualLoad,
    // not the initiation. Lean not × calm. Rock not × calm. Trailing A2
    // / hip later-cycle only. Leading-hip drive still only once
    // StanceLead ≥ 0. FallLeadReady / DualLoad still StanceLead ≥ 0.
    // Air latches 8°/6°. Recover still needs span ≥ FootLeadDeadzone.
    // Vault / firstStepHoldLift stay. footFriction stays 1.2.
    // (33) That still left walkSwap=0. Comment 32 kept the trail CoM
    // loop until StanceLead ≥ 0. DrivePelvis still holds the pelvis
    // world-upright on the stacked pair (plantHipRelease 0), so a CoP
    // behind CoM cannot rotate the inverted pendulum, and the same 9°
    // written into pelvisLean is a bow: hips back, CoM stays,
    // StanceLead stays −1.5 cm. walkPlantTiltFirst's 10° is a second
    // copy of that bow on the plantMinDuration clock. Plant never
    // EnterFall, 55° never legal, sinkAbort ~4 s, walkSwap=0,
    // travelX=0. Cycle 0 Plant while the trail is still loaded: 9° is
    // lumbar only (torsoLean); pelvisLean stays 0; walkPlantTiltFirst
    // waits for StanceLead ≥ 0. DrivePelvis is then a pivot, trunk mass
    // shifts forward, FallLeadReady can fire. Trail CoM keep / lean
    // not × calm / rock not × calm stay. Trailing A2 / hip later-cycle
    // only. Leading-hip drive still only once StanceLead ≥ 0.
    // FallLeadReady / DualLoad still StanceLead ≥ 0. Air latches 8°/6°.
    // Recover still needs span ≥ FootLeadDeadzone. Vault /
    // firstStepHoldLift stay. footFriction stays 1.2.
    // (34) That still left walkSwap=0. Comment 33 made the 9° lumbar
    // only so DrivePelvis could be a pivot. The pivot still has a
    // P-to-vertical spring: lumbar third-law is absorbed (~0.9 cm of
    // chest mass), StanceLead stays −1.5 cm, Plant never EnterFall.
    // Cycle 0 Plant while the trail is loaded: hips lock to the stance
    // snapshot (not hipHoldAngle 0 — that brake is why hipHold is off
    // on a split) and pelvis P tracks current tilt, so the 9° is an
    // inverted-pendulum moment about the ankles. FallLeadReady can
    // fire when StanceLead ≥ 0. Trail CoM keep / lean not × calm /
    // rock not × calm stay. pelvisLean stays 0; walkPlantTiltFirst
    // waits for StanceLead ≥ 0. Trailing A2 / hip later-cycle only.
    // Leading-hip drive still only once StanceLead ≥ 0. FallLeadReady
    // / DualLoad still StanceLead ≥ 0. Air latches 8°/6°. Recover
    // still needs span ≥ FootLeadDeadzone. Vault / firstStepHoldLift
    // stay. footFriction stays 1.2.
    // (35) That still left walkSwap=0. Hip-lock + two planted µ=1.2
    // soles is overconstrained: the stack cannot rotate about the
    // ankles, MixHipHold D brakes pelvis ω, and lumbar 9° is only
    // ~0.9 cm of chest (need 1.5 cm). Revert the lock. Cycle 0 Plant
    // while the trail is loaded: trailing HIP extend (not A2 — toe
    // CoP on a 26 cm sole is a brake). Plus = thigh lower end back;
    // foot 1.5 cm behind CoM carries the pelvis onto the already-
    // leading foot. DrivePelvis stays world-vertical (hipHold off on
    // a split). trailWindow is the plantMinDuration clock, not 8 cm
    // (that arm never grows). FallLeadReady can fire when StanceLead
    // ≥ 0. Trail CoM keep / lean not × calm / rock not × calm stay.
    // pelvisLean stays 0; walkPlantTiltFirst waits for StanceLead ≥ 0.
    // Trailing A2 later-cycle only. Leading-hip drive still only once
    // StanceLead ≥ 0. FallLeadReady / DualLoad still StanceLead ≥ 0.
    // Air latches 8°/6°. Recover still needs span ≥ FootLeadDeadzone.
    // Vault / firstStepHoldLift stay. footFriction stays 1.2.
    // (36) That still left walkSwap=0. Comment 35's extend was a 0.15
    // DrivePelvis counter. Same-hip P-to-vertical cancels it at ~2°
    // of pelvis tilt; only the leading hip flexes (a sit). Cycle 0
    // Plant while the trail is loaded: trailing hip LEAVES DrivePelvis
    // and SPD-extends (plus pose, plantMinDuration clock, 8° ceiling).
    // Leading hip stays the world-vertical pivot: rear open + front
    // close is a lunge onto the already-leading foot. FallLeadReady
    // can fire when StanceLead ≥ 0. Trail CoM keep / lean not × calm /
    // rock not × calm stay. pelvisLean stays 0; walkPlantTiltFirst
    // waits for StanceLead ≥ 0. Trailing A2 later-cycle only.
    // Leading-hip drive still only once StanceLead ≥ 0. FallLeadReady
    // / DualLoad still StanceLead ≥ 0. Air latches 8°/6°. Recover
    // still needs span ≥ FootLeadDeadzone. Vault / firstStepHoldLift
    // stay. footFriction stays 1.2.
    // (37) That still left walkSwap=0. Comment 36's leading "pivot" is
    // DrivePelvis at signal 0 plus hipFriction 12 (a weld). Two equal
    // planted chains with a horizontal pelvis are a parallelogram:
    // CoM cannot walk onto the already-leading foot unless BOTH thighs
    // take the same plus angle. A welded lead is 0 DOF, trail SPD is
    // internal force, then the lead flexes (a sit). Cycle 0 Plant
    // while the trail is loaded: BOTH hips leave DrivePelvis and
    // SPD-extend to the same plus pose; leading-hip viscosity fades
    // with the trail. Stance knee / ankle keep the built damper.
    // FallLeadReady can fire when StanceLead ≥ 0. Trail CoM keep /
    // lean not × calm / rock not × calm stay. pelvisLean stays 0;
    // walkPlantTiltFirst waits for StanceLead ≥ 0. Trailing A2
    // later-cycle only. Leading-hip DrivePelvis only once StanceLead
    // ≥ 0. FallLeadReady / DualLoad still StanceLead ≥ 0. Air latches
    // 8°/6°. Recover still needs span ≥ FootLeadDeadzone. Vault /
    // firstStepHoldLift stay. footFriction stays 1.2.
    // (38) That still left walkSwap=0. Comment 37 freed DrivePelvis
    // on both hips and faded only the leading hip. Remaining 0 DOF:
    // swingFade is clearance, so the trailing hip stays hipFriction 12
    // on dirt, and ankle K=3 on I/10 is a tighter weld than the hip —
    // two rigid struts, the parallelogram cannot change angle. torsoLean
    // 9° with DrivePelvis off is the comment-33 bow. Cycle 0 Plant
    // while the trail is loaded: both hip and ankle dampers snap to
    // tau K (knees keep the strut); torsoLean stays 0 with pelvisLean
    // until StanceLead ≥ 0. FallLeadReady can fire when StanceLead ≥ 0.
    // Trail CoM keep / rock not × calm stay. Trailing A2 later-cycle
    // only. Leading-hip DrivePelvis only once StanceLead ≥ 0.
    // FallLeadReady / DualLoad still StanceLead ≥ 0. Air latches
    // 8°/6°. Recover still needs span ≥ FootLeadDeadzone. Vault /
    // firstStepHoldLift stay. footFriction stays 1.2.
    // (39) That still left walkSwap=0. Comment 38 snapped the dampers
    // so the struts could rotate, but both hips still targeted the
    // same +8° joint angle with DrivePelvis off. Two equilibria: shear
    // (thighs rotate about the ankles, CoM translates) vs sit (thighs
    // stay vertical, pelvis nods +8°, joint errors go to 0). Sit is
    // free; SPD takes it; StanceLead stays −1.5 cm; Plant never
    // EnterFall; 55° never legal; sinkAbort ~4 s; walkSwap=0;
    // travelX=0. Cycle 0 Plant while the trail is loaded: trail hip
    // leaves DrivePelvis and SPD-extends; leading hip STAYS the
    // world-vertical pivot (selects shear). Hip+ankle dampers still
    // snap to tau K. torsoLean 9° is legal again; pelvisLean stays 0
    // until StanceLead ≥ 0. Trail CoM keep / rock not × calm stay.
    // Trailing A2 later-cycle only. Leading DrivePelvis stays through
    // the trail-loaded window. FallLeadReady / DualLoad still
    // StanceLead ≥ 0. Air latches 8°/6°. Recover still needs span ≥
    // FootLeadDeadzone. Vault / firstStepHoldLift stay. footFriction
    // stays 1.2.
    // (40) That still left walkSwap=0. Comment 39's trail +8° is a
    // JOINT angle. jointAngle = pelvis − thigh, so sit (thighs stay
    // vertical, pelvis nods +8°) zeroes the error with no CoM travel;
    // DrivePelvis on the already-snapped leading hip then rotates the
    // lead thigh instead of un-nodding the pelvis. StanceLead stays
    // −1.5 cm; Plant never EnterFall; 55° never legal; sinkAbort ~4 s;
    // walkSwap=0; travelX=0. Cycle 0 Plant while the trail is loaded:
    // trail hip leaves DrivePelvis and SPD-holds WORLD thigh tilt
    // (knee back). Sit leaves the thigh at 0. Leading hip STAYS the
    // world-vertical pivot. Hip+ankle dampers still snap to tau K.
    // torsoLean 9° is legal; pelvisLean stays 0 until StanceLead ≥ 0.
    // Trail CoM keep / rock not × calm stay. Trailing A2 later-cycle
    // only. Leading DrivePelvis stays through the trail-loaded window.
    // FallLeadReady / DualLoad still StanceLead ≥ 0. Air latches
    // 8°/6°. Recover still needs span ≥ FootLeadDeadzone. Vault /
    // firstStepHoldLift stay. footFriction stays 1.2.
    // (41) That still left walkSwap=0. Comment 40's trail WORLD tilt
    // is the right error (sit cannot zero it), but DrivePelvis on the
    // leading hip is the wrong pivot. The trail extensor is a couple:
    // clockwise trail thigh, CCW pelvis. DrivePelvis sees that nod
    // and flexes the lead thigh — a sit, not the equal-angle shear
    // comment 37 named. StanceLead stays −1.5 cm; Plant never
    // EnterFall; 55° never legal; sinkAbort ~4 s; walkSwap=0;
    // travelX=0. Cycle 0 Plant while the trail is loaded: BOTH hips
    // leave DrivePelvis and SPD-hold the SAME WORLD thigh tilt (knee
    // back). Sit leaves both thighs at 0. Equal world angles are the
    // parallelogram shear. Hip+ankle dampers still snap to tau K.
    // torsoLean stays 0 with pelvisLean until StanceLead ≥ 0.
    // Trail CoM keep / rock not × calm stay. Trailing A2 later-cycle
    // only. Leading DrivePelvis only once StanceLead ≥ 0.
    // FallLeadReady / DualLoad still StanceLead ≥ 0. Air latches
    // 8°/6°. Recover still needs span ≥ FootLeadDeadzone. Vault /
    // firstStepHoldLift stay. footFriction stays 1.2.
    // (42) That still left walkSwap=0. Comment 41's equal WORLD thigh
    // tilt is the shear, but plantRockLead + dual CoM ankle still
    // rotate the shins (locked knees → thighs) in world frame. That
    // µ=1.2 couple wins at the sole; the hip pose is an internal
    // force; pelvis nods (sit); StanceLead stays −1.5 cm; Plant never
    // EnterFall; 55° never legal; sinkAbort ~4 s; walkSwap=0;
    // travelX=0. Cycle 0 Plant: BOTH hips leave DrivePelvis and
    // SPD-hold the SAME WORLD thigh tilt, not gated on trailLead /
    // plantHipExtend. Both ankle CoM loops (including plantRockLead)
    // stay at 0 so the snapped ankles are hinges. Ramp while the trail
    // is loaded, freeze that angle until EnterFall — otherwise
    // DrivePelvis returns at StanceLead ≥ 0 and unwinds the 1.5 cm
    // during plantMinDuration. Hip+ankle dampers still snap to tau K.
    // torsoLean stays 0 with pelvisLean until StanceLead ≥ 0.
    // Trailing A2 later-cycle only. Leading DrivePelvis only once
    // EnterFall. FallLeadReady / DualLoad still StanceLead ≥ 0.
    // Air latches 8°/6°. Recover still needs span ≥ FootLeadDeadzone.
    // Vault / firstStepHoldLift stay. footFriction stays 1.2.
    // (43) That still left walkSwap=0. Comments 37–42 drove shear with
    // hip WORLD/joint SPD and zeroed both ankle CoM loops. Hip muscle
    // is an internal couple (extensor clockwise on the thigh, CCW on
    // the pelvis) so the cheap equilibrium is a sit; StanceLead stays
    // −1.5 cm. Separately, plantRockLead never wrote on cycle 0
    // (`FallCatchCycleCount != 0` contradicted the comment that the
    // leading ankle already fired). No external CoP → Plant never
    // EnterFall; 55° never legal; sinkAbort ~4 s; walkSwap=0;
    // travelX=0. Cycle 0 Plant restores the ankle strategy:
    // plantRockLead writes (CoP behind CoM). DrivePelvis stays the
    // lumbar pivot. torsoLean 9° is back. Comment 44: pelvisLean is
    // the inverted-pendulum WORLD target DrivePelvis holds (minus =
    // forward), not a free-hip bow. Lumbar-only left ~1.35 cm of CoM,
    // short of StanceLead ≥ 0, and ankle K=3 froze the shin. Cycle 0
    // Plant writes the 9° pelvisLean and snaps only the ankle dampers
    // (hip K stays). The fulcrum is the ground, not the hinge. Comment
    // 45: that WORLD target is still a bow (SPD sees pelvis inertia).
    // Cycle 0 Plant SPD-holds the quiet hip snapshot (DrivePelvis off)
    // so the snapped ankles are the hinge; plantRockLead is the CoP.
    // Trunk lean waits (lumbar third-law on a locked stack is backward
    // about those ankles). Trail CoM keep / rock not × calm stay.
    // Trailing A2 / plantHipExtend later-cycle only. walkPlantTiltFirst
    // still waits (that +10 is the backward clearance tilt).
    // FallLeadReady / DualLoad still StanceLead ≥ 0. Air latches
    // 8°/6°. Recover still needs span ≥ FootLeadDeadzone. Vault /
    // firstStepHoldLift stay. footFriction stays 1.2.
    // Comment 46: quiet-joint lock + two soles is a table, not a
    // rocker. Cycle 0 Plant WORLD-holds both thighs at the same
    // clockwise tilt (shear). Snap hip+ankle dampers. Comment 47:
    // keep the shear, restore plantRockLead on the snapped ankles
    // (zeroing them left only an internal hip couple). Freeze that
    // world angle once StanceLead ≥ 0; DrivePelvis stays off until
    // EnterFall. Trunk lean waits. DualLoad / 55° still that
    // event, but only in Fall — Plant dirt must not fold 55°
    // in the freeze wait.
    // abort Plant waits the 3 s ladder).
    [Tooltip("1 = recover any starting pose: measured spawn CoM, TiltSettled + signed CoM-to-stance ≤ plantComLeadMax before Plant→Fall, one-foot surrender, abort Plant stands now. Cycle-0 Fall still uses RecoverComLostMeters 0.30 m (PlantComReady), not this standing band. 0 = former ladder.")]
    public float walkRecoverAnyPose = 1f;

    [Header("Transfer (event-gated)")]
    [Tooltip("Max seconds in dual support — then a soft fallback.")]
    public float transferMaxDuration = 6f;

    [Tooltip("Max Transfer when intent.run=1.")]
    public float runTransferMaxDuration = 4f;
    [Tooltip("Seconds reduction of walk transfer per 1 m/s target speed.")]
    public float walkCadenceTransferGain = 1.5f;
    [Tooltip("Minimum walk transfer duration under cadence scaling.")]
    public float walkCadenceMinTransfer = 1.0f;

    [Tooltip("Do not check ready earlier — intent=0 must reach BC.")]
    public float transferMinDuration = 0.7f;
    [Tooltip("If swing foot has landed, force leaving Transfer after this age, s.")]
    public float transferTouchdownMaxAge = 1.2f;

    [Tooltip("Both feet grounded and |CoM offset| ≤ this — hard ready.")]
    public float transferComOffsetMax = 0.10f;

    [Tooltip("Diagnostic: |CoM − catch foot| after a walking catch (~0.60 m). Not a Plant gate — that offset never enters 0.16 m.")]
    public float recoverComOffsetMax = 0.16f;

    [Tooltip("Soft fallback after transferMaxDuration.")]
    public float transferFallbackComMax = 0.12f;

    [Tooltip("Legacy: no longer used in IsTransferReady (BC lag).")]
    public float transferStandLegLevelMax = 0.05f;

    public bool walkActive;
    // Headless BeginWalk writes the simulated clock. Capture-walk
    // onset logging (first 3 s) measures age from this, not from a
    // physics-step counter that ResetCaptureWalk zeroes.
    public float WalkStartTime => walkStartTime;

    // Counters for the summary: without them the CSV looks "ready" but the swap is invisible.
    public int SwapCount { get; private set; }
    public int TransferReadyFrames { get; private set; }
    public int FallCatchCycleCount { get; private set; }
    // Failed Fall/Reach abort: Plant holds the same stance and the Recover
    // brake until TiltSettled + signed CoM lead, then the next Fall may start.
    // Not a completed cycle — FallCatchCycleCount stays put.
    public bool StandingRestore { get; private set; }
    // After Recover the new stance may still have CoM ahead of it
    // (AheadOfStance is a foot span, not a catch). Stay false until a
    // tick sees lead inside plantCaughtLeadMax: plantRock / lean must
    // not add forward CoP on that uncaught plant (that was the 90°
    // topple). FallLeadReady may still fire on the lead-wait deadline
    // so the latch cannot stall a completed swap until 93°.
    public bool PlantRolledFromBehind { get; private set; }
    // Recovery ladder is holding two-foot quiet stance instead of a split
    // Plant. Not a phase and not a cycle — the walk resumes from the same
    // Plant once the gate opens.
    public bool StandingSurrender => plantStandSurrender;
    public bool SpawnStandHeightValid => spawnStandHeightValid;
    public float SpawnStandHeight => spawnStandHeight;
    public bool StandComHeightValid => standComHeightValid;
    public float StandComHeight => standComHeight;
    public bool HasStandingComY => standComHeightValid || spawnStandHeightValid;
    public float StandingComY => standComHeightValid ? standComHeight : spawnStandHeight;
    // Live CoM drop below the settled standing bar (world Y). 0 when
    // no bar yet. Onset log / GETUP read this; not a force.
    public float StandingComDropMeters
    {
        get
        {
            if (comCalculator == null || !HasStandingComY)
                return 0f;
            if (comCalculator.TrackedBodyCount <= 0)
                return 0f;
            return StandingComY - comCalculator.GetCenterOfMass().y;
        }
    }
    public bool PelvisDroppedFromStand => !PelvisNearSpawnStand();
    // Failed catches since the last landing. Diagnostic for the ladder.
    public int FallCatchBounceCount => fallCatchBounceCount;
    public int FallCatchPhaseCode =>
        useFallCatchWalk != 0 && walkActive ? (int)fallCatchPhase : -1;
    public int PhaseCode
    {
        get
        {
            // Capture walker owns the gait: Tick returns without writing
            // fallCatchPhase or the legacy Stance/Transfer clock, so those
            // stay at Plant / Stance = 0 for the whole run. Report the
            // walker's own state so CSV walkPhase and the overlay move.
            if (balance == null)
                EnsureRefs();
            if (balance != null && balance.useCaptureWalker != 0 && walkActive)
                return balance.CaptureWalkStateCode;
            // WalkLift never EnterTransfer (that DualSupport is the dump).
            // Overlay Unload/Clear/Advance is the step; report it so
            // CSV walkPhase leaves Stance=0 once the swing knee lifts.
            if (balance != null && balance.useWalkLift != 0 && walkActive
                && balance.WalkLiftPhaseCode != 0)
                return balance.WalkLiftPhaseCode;
            return useFallCatchWalk != 0
                ? (fallCatchPhase <= FallCatchPhase.Reach ? 0 : 1)
                : (phase == Phase.Transfer ? 1 : 0);
        }
    }
    // Stance/Transfer (or Plant/Fall/Reach vs Catch) without the WalkLift
    // overlay. PhaseCode reports Unload as 1 so CSV leaves 0; that overlay
    // is still this Stance clock. Using PhaseCode==0 as "still Stance"
    // reset the lift on the next 5 ms step (knee act 0.43, walkPhase only
    // −1/0, no single support).
    public bool WalkStanceClock
    {
        get
        {
            if (!walkActive)
                return false;
            if (useFallCatchWalk != 0)
                return fallCatchPhase <= FallCatchPhase.Reach;
            return phase == Phase.Stance;
        }
    }
    public float CurrentStanceSign => currentStance;
    // WALK-2 diagnostics: read-only, for the trial CSV. A phase code alone
    // does not say whether the fall was long enough, whether the body had
    // really passed the stance foot at Reach, and whether the swing ever
    // cleared the ground before the catch.
    public float LastFallDuration => lastFallDuration;
    public float LastCatchDuration => lastCatchDuration;
    public float LastReachDuration => lastReachDuration;
    // Last airborne Reach extend (0–1), frozen on heel plant so Catch does
    // not read holdFold=0. Recover stance sign is the pre-swap Catch identity:
    // Plant decay must not reverse the arms when roles flip.
    public float LastReachExtendProgress => lastReachExtendProgress;
    public bool SwingFootIsPlanted => IsSwingFootPlanted();
    // Toe down, heel still up. CatchReachExtendProgress reads this so
    // scrapeExtendHold (Catch) can freeze Last without also stalling a
    // true airborne extend. scrapeExtendAirAge (Catch) then resumes
    // Last from lift-off once this is false (not live Progress).
    public bool SwingFootIsGrounded => IsSwingFootGrounded();
    // Geometric gap past both contactOffsets. GetContacts can stay true
    // while bounds.min.y is already above the slab; cycle-0 Catch latches
    // the plant on this, not on !GetContacts alone.
    public bool SwingFootSeparated => SwingFootClearance() > ContactOffsetBand();
    // Cycle-0 Catch plant: swing ahead of the stance foot (not of CoM).
    public bool SwingFootAheadOfStance => IsSwingFootAheadOfStance();
    // After the first flight the plant that becomes stance must sit
    // ahead of CoM. AheadOfStance (2 cm) with the mass already past both
    // soles is a support the automaton names Falling.
    public bool SwingFootAheadOfCom => IsSwingFootAheadOfCom();
    public float LastRecoverDuration => lastRecoverDuration;
    public float LastRecoverStanceSign => lastRecoverStanceSign;
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
    // Catch scrape this phase (toe down, heel up). Balance reads this so
    // scrapeLiftAirHold can keep ThighHold after lift-off: forceSwingFlex
    // would otherwise take −swingHipFlex on the first airborne Catch frame.
    // Cleared on Catch exit. True airborne Catch never latches.
    public bool CatchScrapedThisPhase => catchScrapedThisPhase;
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
    // Plant-gated Catch age: 0 while the heel is up. Yield, arm decay,
    // and leftover shin extend read this so the trailing vault, the
    // Reach arm envelope, and the landing fold do not collapse in the air.
    public float CatchPlantAge()
    {
        if (catchPlantEnterTime < 0f)
            return 0f;
        return Mathf.Max(0f, lastTickTime - catchPlantEnterTime);
    }
    public float LastCatchPlantDuration => lastCatchPlantDuration;
    // Air age after a scrape resume lift-off. scrapeLiftAirAge /
    // scrapeVaultAirAge / scrapeArmAirAge / scrapeExtendAirAge /
    // Catch scrapeExtendAirAge read this so fold / leftover vault /
    // arm extraAge / shin Last is time since the toe left, not PhaseAge
    // (dirt) and not 0 (stalled). 0 until that first !grounded frame.
    public bool ScrapeResumeAirActive => scrapeResumeAirEnterTime >= 0f;
    public float ScrapeResumeAirAge()
    {
        if (scrapeResumeAirEnterTime < 0f)
            return 0f;
        return Mathf.Max(0f, lastTickTime - scrapeResumeAirEnterTime);
    }

    // Last + lift-off age on the ReachExtendProgress slope. scrapeExtend
    // already counted dirt as air, so live Progress would jump on the
    // first air frame. scrapeResumeExtendStart is Catch-enter Last frozen
    // at lift-off; this does not compound if Tick writes Last each step.
    public float ScrapeResumeExtendProgress()
    {
        if (scrapeResumeAirEnterTime < 0f)
            return Mathf.Clamp01(lastReachExtendProgress);
        float window = ActiveReachTimeout();
        float start = Mathf.Clamp01(reachExtendStart);
        float denom = Mathf.Max(0.05f, 1f - start) * window;
        return Mathf.Clamp01(
            scrapeResumeExtendStart
            + ScrapeResumeAirAge() / Mathf.Max(0.02f, denom));
    }
    // Leftover stance shin (deg) frozen at Plant→Fall. Fall/Reach vault
    // starts from this instead of landDeg when fallSwingVaultFromLeftover
    // is on. Recomputing CatchReachShinDeg during Fall would use this
    // Fall's LastFallDuration after the Reach gate and drop the fold.
    public float LastPlantStanceShinDeg => lastPlantStanceShinDeg;
    public float ReachEntryStanceComOffset => reachEntryStanceComOffset;
    public float CatchEntrySwingClearance => catchEntrySwingClearance;
    // Age on the clock of the last Tick (stand — simulated, Play — fixedTime).
    // Balance used to take Time.fixedTime on Tick(sim) — different axes.
    public float PhaseAge() => lastTickTime - phaseEnterTime;
    public float ActiveStanceDurationNow() => ActiveStanceDuration();

    // Unused by WalkLift cadence after walklift_air: swap is Advance plant,
    // not the Stance clock. Kept for diagnostics.
    public bool WalkLiftClockDue()
    {
        if (phase != Phase.Stance) return false;
        float activeStance = Mathf.Max(0.1f, ActiveStanceDuration());
        if (!IsRunning() && SwapCount == 0)
            activeStance = Mathf.Min(activeStance, Mathf.Max(0.1f, firstWalkStanceDuration));
        return PhaseAge() >= activeStance;
    }
    // Reach window actually in force: the stand overrides reachTimeout, the
    // cap does not come from the stand.
    public float ActiveReachTimeout() => Mathf.Min(
        Mathf.Max(0.05f, reachTimeout),
        Mathf.Max(0.05f, reachMaxDuration));
    public float ReachAirAge()
    {
        if (reachAirEnterTime < 0f)
            return 0f;
        return Mathf.Max(0f, lastTickTime - reachAirEnterTime);
    }
    // 0 = shin still folded, 1 = shin at reachKneeLandDeg. BalanceController
    // reads this to extend the swing knee before touchdown.
    public float ReachExtendProgress()
    {
        if (reachAirEnterTime < 0f)
            return 0f;
        // A contact flicker starts the air clock. Extending toward
        // reachKneeLandDeg while the WHOLE sole is down pins it (140° → 35°
        // on the ground, swingFoot 0.96). Hold that fold on a real plant.
        // A toe scrape is not a plant (fallSwingHeelPlant): resetting to 0
        // there put the ankle too high for airHeel to load the heel, so
        // Catch never saw plantedAfterAir. scrapeExtend keeps the clock.
        bool holdFold = IsSwingFootGrounded();
        if (fallSwingScrapeExtend > 0.5f && HeelPlantOn())
            holdFold = IsSwingHeelLoaded();
        if (holdFold)
            return 0f;
        float window = ActiveReachTimeout();
        float start = Mathf.Clamp01(reachExtendStart);
        float age = Mathf.Max(0f, lastTickTime - reachAirEnterTime) / window;
        // Airborne: start the shin extend immediately. reachExtendStart
        // is a grounded-Reach guard (do not unfold a still-loaded trail
        // toward landDeg). Once the foot is up, waiting 0.25 of timeout
        // holds reachKneeFlex and the sole stays behind CoM while the
        // trunk falls. Cycle-0 last-write (ApplyFirstStepCycle0Pose)
        // still Lerps 55° → landDeg; this only removes the wait.
        if (!IsSwingFootGrounded())
            start = 0f;
        return Mathf.Clamp01((age - start) / Mathf.Max(0.05f, 1f - start));
    }
    // Stance: swing had at least one airborne frame this phase (BalanceController push gate).
    public bool SwingWasAirborne => swingWasAirborne;

    private MotionIntent intent;
    private BodyStateEstimator bodyState;
    private CenterOfMassCalculator comCalculator;
    private BalanceController balance;
    private VestibularSystem vestibular;

    private float walkStartTime = -1f;
    private bool pendingBegin;
    private Phase phase;
    private FallCatchPhase fallCatchPhase;
    private float phaseEnterTime;
    private float lastTickTime;
    private float currentStance;
    // Stance: whether the swing had at least one !grounded frame (for heel-strike).
    private bool swingWasAirborne;
    // First split latches on BeginWalk so a later dip back inside the
    // plate does not drop standLeg and unwind the stance-ankle roll.
    private bool firstStepSplitLatched;
    private float swingAirStreak;
    private bool swingAirQualified;
    private float prevTickTime = -1f;
    private float lastFallDuration;
    private float lastCatchDuration;
    private float lastCatchPlantDuration;
    private float catchPlantEnterTime = -1f;
    private float lastReachDuration;
    private float lastReachExtendProgress;
    private float lastRecoverDuration;
    private float lastRecoverStanceSign;
    private float lastPlantStanceShinDeg;
    private float reachEntryStanceComOffset;
    private float catchEntrySwingClearance;
    // Clock for shin extend. PhaseAge includes grounded Fall leftover, and
    // a cadence pass started ReachExtendProgress on that clock: the knee
    // ran toward reachKneeLandDeg while the foot was still down, so 140°
    // of flex never lifted (swingFoot 0.96). Start the window on first air.
    private float reachAirEnterTime = -1f;
    // First !grounded after a scrape resume (Catch→Reach or Catch
    // scrapeExtendAirAge). scrapeLiftAirAge / scrapeVaultAirAge /
    // scrapeArmAirAge extraAge is lastTickTime minus this, not PhaseAge
    // (dirt) and not 0 (stall).
    private float scrapeResumeAirEnterTime = -1f;
    // simTime of the first Plant tick with the Plant→Fall gate shut, −1 while
    // the gate is open. The stall age is a stamp difference on purpose:
    // TickFallCatchWalk has no dt (prevTickTime is only stamped there).
    private float plantGateStallSince = -1f;
    // One re-seat per stall, so a shut gate cannot ping-pong support.
    private bool plantReseated;
    // Last rung of the ladder: Plant writes standLeg 0 (two-foot quiet
    // stance) until the gate opens again.
    private bool plantStandSurrender;
    // Failed catches (Catch→Reach) since the last landing or standing Plant.
    private int fallCatchBounceCount;
    // CoM height of the last pose that passed the gate on two feet, and
    // whether such a pose has been seen at all this walk.
    private float standComHeight;
    private bool standComHeightValid;
    // Standing CoM y, once, after BuildHuman while idle. Floor for the
    // sink guard so a folded BeginWalk cannot write its own collapsed
    // height as "this is how tall standing is". Not the pelvis spawn
    // column: that is ~0.28 m above live CoM (leg mass under the pelvis)
    // and exceeds sinkAbortDrop 0.25 on a quiet stance.
    private float spawnStandHeight;
    private bool spawnStandHeightValid;
    private float spawnPelvisY;
    private bool spawnPelvisYValid;
    private Transform pelvisTf;
    private Human humanBody;

    // Catch scrape this phase (toe down, heel up). scrapeExtendAirAge
    // starts the air clock only after this, so a true airborne Catch
    // still Max's Last with live Progress.
    private bool catchScrapedThisPhase;
    // Catch-enter Last at that first air frame. scrapeExtendAirAge
    // continues from this, not live Last (Tick would compound).
    private float scrapeResumeExtendStart;

    // Feet closer than this along the walk direction are "level": keep the
    // alternating order instead of flipping roles on contact noise.
    private const float FootLeadDeadzone = 0.02f;
    // Touchdown is an event; one physics step of contact is enough, but not
    // the same frame we entered Recover in.
    private const float RecoverTouchdownMinAge = 0.05f;
    // walk2_blend: TiltSettled was tilt-only (|tilt| < 45°). Recover planted
    // at 7° / 55 deg/s; 0.05 s later Fall at +49 deg/s and Catch cycle 1
    // went to 101°. Spin must settle too. 40 deg/s sits inside the recover
    // blend omegaRef (50) so Fall starts after the brake has begun to ease,
    // not while hard is still 1.
    private const float RecoverOmegaSettledDeg = 40f;
    // Trunk band that counts as "standing again" before the next fall.
    private const float TiltSettledMaxDeg = 12f;
    // Cycle-0 |CoM − stance| past this is already BalanceController Falling
    // (fallCoMOffset 0.30). The 3 s Plant stall is a collapse, not a restore.
    // Later cycles keep signed lead: a catch plants ~0.13 m ahead of CoM.
    private const float RecoverComLostMeters = 0.30f;

    void Awake()
    {
        // BodyState / CoM appear in BuildHuman after AddComponent —
        // they are not here yet. Pick them up lazily in EnsureRefs.
        intent = GetComponent<MotionIntent>();
    }

    void Start()
    {
        // AddComponent runs Start before Human.BuildHuman, so Pelvis is
        // missing here. Capture is retried from FixedUpdate while idle and
        // from BeginWalk before walkActive, never from a folded Tick.
        EnsureRefs();
        TryCaptureSpawnStandHeight();
    }

    private void EnsureRefs()
    {
        if (intent == null)
            intent = GetComponent<MotionIntent>();
        if (bodyState == null)
            bodyState = GetComponent<BodyStateEstimator>();
        if (comCalculator == null)
            comCalculator = GetComponent<CenterOfMassCalculator>();
        if (balance == null)
            balance = GetComponent<BalanceController>();
        if (vestibular == null)
            vestibular = GetComponent<VestibularSystem>();
        if (humanBody == null)
            humanBody = GetComponent<Human>();
        if (pelvisTf == null)
            pelvisTf = transform.Find("Pelvis");
    }

    // In headless only HeadlessTrial calls Tick (before physics).
    // FixedUpdate is for Play Mode / ArmWalk.
    public bool externalDrive;

    void FixedUpdate()
    {
        if (HeadlessTrial.Active || externalDrive)
        {
            TryCaptureSpawnStandHeight();
            return;
        }
        TryCaptureSpawnStandHeight();
        if (pendingBegin)
        {
            walkStartTime = Time.fixedTime;
            pendingBegin = false;
            lastTickTime = Time.fixedTime;
            BeginCycle(Time.fixedTime);
        }
        if (!walkActive || intent == null) return;
        Tick(Time.fixedTime);
    }

    // Play/WebGL entry. Same pair as HeadlessTrial: BeginWalk then Tick.
    // The old pendingBegin path set walkActive without walkStartTime /
    // currentStance / standLeg until the next FixedUpdate.
    public void ArmWalk()
    {
        if (walkActive)
            return;
        BeginWalk(Time.fixedTime);
        Tick(Time.fixedTime);
    }

    public void BeginWalk(float simTime)
    {
        EnsureRefs();
        // Capture while walkActive is still false. Start() ran before
        // BuildHuman, and once walking the spawn bar must not be rewritten
        // from a heap. Quiet pose: this is the same live CoM FixedUpdate
        // stored during idle stance. Do not floor with the pelvis column.
        TryCaptureSpawnStandHeight();
        walkActive = true;
        pendingBegin = false;
        walkStartTime = simTime;
        lastTickTime = simTime;
        BeginCycle(simTime);
    }

    public void EndWalk()
    {
        walkActive = false;
        pendingBegin = false;
        walkStartTime = -1f;
        StandingRestore = false;
        plantStandSurrender = false;
        plantGateStallSince = -1f;
        plantReseated = false;
        if (intent != null)
            intent.standLeg = 0f;
    }

    private void BeginCycle(float simTime)
    {
        EnsureRefs();
        phase = Phase.Stance;
        fallCatchPhase = FallCatchPhase.Plant;
        phaseEnterTime = simTime;
        // Quiet stand: both feet in the 2 cm deadzone → firstStance, same
        // as the former hard assign. A start already on one loaded foot
        // keeps that foot instead of handing stance to a leg in the air.
        // Same geometric rule as a catch: stance is the leading foot, so
        // the trailer is the swing that steps FORWARD. Standing on the
        // trailer vs the lead did not change the sign of the first-step
        // fall (walkSwap 1, travelX −1.4 m, torso 90° back): DualLoad /
        // 55° / +10° pelvis plus then folded a still-planted swing, and
        // a planted trailing flexor is GRF −X. Comment 48 in
        // BalanceController. EnterFallCatchPlant still records a leading
        // swing as walking backward — do not pick the trailer here.
        currentStance = PickStanceFromFeet(firstStance);
        firstStepSplitLatched = false;
        // Split on this frame so the named stance ankle can roll CoM to
        // the toe. Waiting for Tick (or for ComLeftSupport) left both
        // ankles on mid-pair: 9 mm, then UpdateState 0.30 (burn_stag).
        // The STEP (WalkLift / geo-lift) still waits for ComLeftSupport.
        swingWasAirborne = false;
        swingAirStreak = 0f;
        swingAirQualified = false;
        prevTickTime = -1f;
        SwapCount = 0;
        TransferReadyFrames = 0;
        FallCatchCycleCount = 0;
        PlantRolledFromBehind = true;
        // CaptureWalk onset is DualSupport lean + one swing knee.
        // StandingPlantBandReady is TiltSettled + signed CoM-to-stance,
        // the Plant→Fall standing wait. Cycle-0 PlantFallGateReady
        // skips tilt / 0.30 m — that launched a tipped BeginWalk.
        // Column slack froze a quiet 6° knee (phase 0 / 35 s). Quiet
        // start (tilt 2.1°, |lead| ~0) still opens and walks. A heap
        // surrenders to two-foot Plant; Falling is not named while
        // restore is up. walkActive stays so the walker starts when
        // the gate opens.
        bool captureOn = balance != null && balance.useCaptureWalker != 0;
        bool coldGateReady;
        if (walkRecoverAnyPose > 0.5f)
            // CaptureWalk: pose-only standing band (tilt + lead), not
            // omega. StandingPlantBandReady also asks < 40 °/s; mixer
            // start on a quiet 2.1° trunk spikes past that, BeginWalk
            // raised StandingRestore, CaptureWalkActive stayed false,
            // DualSupport never ran (burn_walk 40 s / swap 0 / 0.0668 m).
            // WalkLift keeps the omega band. Cycle-0 PlantFallGateReady
            // skip still launches a 20° heap — do not use it here.
            coldGateReady = captureOn
                ? CaptureWalkColdStandReady()
                : StandingPlantBandReady();
        else if (captureOn)
            coldGateReady = true;
        else
            coldGateReady = PlantFallGateReady();
        StandingRestore = !coldGateReady
            && (useFallCatchWalk != 0 || walkRecoverAnyPose > 0.5f);
        // Cold start: the ladder clock runs from here, so a BeginWalk on a
        // pose that is not a stand gets the same deadline a failed step gets.
        plantGateStallSince = coldGateReady ? -1f : simTime;
        plantReseated = false;
        plantStandSurrender = false;
        fallCatchBounceCount = 0;
        standComHeightValid = false;
        standComHeight = 0f;
        if (coldGateReady)
            CaptureStandHeight();
        // Any starting pose the standing band refused: two-foot quiet
        // stance now. RecoverPoseIsLost already covered 30° / sink /
        // 0.30 m; the rest of the refused band (tilt 12–30°, |lead|
        // past plantComLeadMax) used to wait plantStallStandSeconds in
        // split Plant — one-leg on a tipped trunk is the fold. Quiet
        // start never raises restore (tilt 2.1°, |lead| ~0).
        // plantMinDuration / TiltSettled / plantComLeadMax stay.
        if (StandingRestore)
            SurrenderWalkToStand(simTime);
        else if (balance != null && balance.useCaptureWalker != 0)
        {
            // Capture walker owns the support foot. Writing standLeg
            // here is a second gait on the same hinges for one Tick.
            if (intent != null)
                intent.standLeg = 0f;
        }
        else
            WriteWalkSplitLeg();
        lastFallDuration = 0f;
        lastCatchDuration = 0f;
        lastCatchPlantDuration = 0f;
        catchPlantEnterTime = -1f;
        lastReachDuration = 0f;
        lastReachExtendProgress = 0f;
        lastRecoverDuration = 0f;
        lastRecoverStanceSign = 0f;
        lastPlantStanceShinDeg = 0f;
        reachEntryStanceComOffset = 0f;
        catchEntrySwingClearance = 0f;
        reachAirEnterTime = -1f;
        scrapeResumeAirEnterTime = -1f;
        scrapeResumeExtendStart = 0f;
        catchScrapedThisPhase = false;
        ReachResumedFromCatch = false;
        ReachResumedFromScrape = false;
        ReachResumedFromCatchScrapeAir = false;
        CatchResumedFromReachScrapeAir = false;
        RecoveredFromCatchScrapeAir = false;
        RecoveredFromCatchAir = false;
        CatchResumedFromReachAir = false;
        RecoveredFromCatchAirEnter = false;
        RecoveredFromCatchGroundCatch = false;
    }

    public void Tick(float simTime)
    {
        EnsureRefs();
        if (!walkActive || intent == null || walkStartTime < 0f)
            return;

        lastTickTime = simTime;

        if (simTime < walkStartTime)
        {
            intent.standLeg = 0f;
            return;
        }

        // WALK-3: the capture-point walker (BalanceControllerCaptureWalk.cs)
        // is the gait now. Neither automaton below may also write phases or
        // the split-leg intent — that channel is not how the new walker picks
        // a support foot, and two writers on one gait is exactly the hybrid
        // this walker replaces. walkActive stays true: it is still the bit
        // that says "the human is walking".
        if (balance != null && balance.useCaptureWalker != 0)
        {
            // Resume after a heap / tipped BeginWalk. Same standing
            // band BeginWalk used (TiltSettled + CoM-to-stance). Not
            // PlantFallGateReady (cycle 0 skips tilt / 0.30 m) and not
            // the column test (quiet 6° knee, phase 0 / 35 s). A pose
            // outside it keeps two-foot quiet stance until the gate
            // opens, then the walker starts. Play Tick is −50; headless
            // Tick is after FixedUpdate. TiltSettled / plantComLeadMax stay.
            if (walkRecoverAnyPose > 0.5f && (StandingRestore || plantStandSurrender))
            {
                // Same pose-only band BeginWalk used (not omega: mixer
                // start on a quiet trunk is not a heap). TiltSettled
                // 12° + signed CoM-to-stance ≤ plantComLeadMax, then
                // the walker starts. GETUP-1 owns the joints until
                // FinishGetUp: releasing here at 12° launched DualSupport
                // on a still-tipped trunk. Fallen keeps the hold until
                // the walk key starts TickGetUp.
                bool getUpOwns = balance.locomotionState
                    == BalanceController.LocomotionState.GettingUp
                    || balance.locomotionState
                    == BalanceController.LocomotionState.Fallen;
                if (getUpOwns)
                    SurrenderWalkToStand(simTime);
                else if (CaptureWalkColdStandReady())
                    ReleaseStandingPlantGate(simTime);
                else
                    SurrenderWalkToStand(simTime);
            }
            intent.standLeg = 0f;
            return;
        }

        if (useFallCatchWalk != 0)
        {
            TickFallCatchWalk(simTime);
            return;
        }

        float phaseAge = simTime - phaseEnterTime;

        if (phase == Phase.Stance)
        {
            // Capture step: split so the stance ankle can roll; lift when
            // ComLeftSupport. RecoverStandReady is the quiet band — it
            // passes on tick 1. The dump was idle-clock lift on that
            // split (burn_stag +1.30 s), not the split itself.
            // WalkLift has no Plant phase. The same TiltSettled +
            // CoM-to-stance band still holds two-foot quiet stance until
            // the gate opens, then the split / Unload run as before.
            // Do not re-judge TiltSettled while WalkLift owns the swing:
            // initiation lean is the step. Happy path never raises restore.
            if (walkRecoverAnyPose > 0.5f && useFallCatchWalk == 0)
            {
                bool liftBusy = balance != null && balance.useWalkLift != 0
                    && (balance.WalkLiftPhaseCode != 0
                        || balance.WalkLiftMaySwap
                        || SwapCount == 0);
                if (StandingRestore || plantStandSurrender)
                {
                    if (RecoverStandReady() && !SinkCollapsed())
                    {
                        ReleaseStandingPlantGate(simTime);
                    }
                    else
                    {
                        SurrenderWalkToStand(simTime);
                        prevTickTime = simTime;
                        return;
                    }
                }
                else if (SinkCollapsed()
                    || (!liftBusy && RecoverPoseIsLost()))
                {
                    SurrenderWalkToStand(simTime);
                    prevTickTime = simTime;
                    return;
                }
            }
            WriteWalkSplitLeg();
            float dt = prevTickTime < 0f ? 0f : Mathf.Max(0f, simTime - prevTickTime);
            prevTickTime = simTime;

            bool swingGrounded = IsSwingFootGrounded();
            bool walkLiftMode = balance != null && balance.useWalkLift != 0;
            // Idle is still the step: Unload starts on this Stance. Treating
            // PhaseCode==0 as "not busy" let RecoverPoseIsLost zero standLeg
            // before the swing knee ran (p_now: maxStandLegLevel 0, walkPhase
            // only −1/0).
            bool walkLiftBusy = walkLiftMode
                && (balance.WalkLiftPhaseCode != 0
                    || balance.WalkLiftMaySwap
                    || SwapCount == 0);
            if (walkTiltAbortDeg > 0.001f && phaseAge >= 0.1f && !walkLiftMode)
            {
                float absTilt = AbsBodyTilt();
                if (absTilt >= walkTiltAbortDeg)
                {
                    EnterTransfer(simTime);
                    intent.standLeg = 0f;
                    return;
                }
            }
            if (!swingGrounded)
            {
                swingWasAirborne = true;
                swingAirStreak += dt;
                if (stanceHeelAirMin > 0.001f && swingAirStreak >= stanceHeelAirMin)
                    swingAirQualified = true;
            }
            else
            {
                // WalkLift plants on the Stance split. standLeg=0 here is
                // both hips DrivePelvis and both ankles CoM on a dumped
                // trunk (walklift_unload t=6.01, first air + CoM trigger).
                if (balance == null || balance.useWalkLift == 0)
                {
                    if (balance != null && balance.WalkLiftMayTransfer
                        && phaseAge >= 0.15f)
                    {
                        EnterTransfer(simTime);
                        intent.standLeg = 0f;
                        return;
                    }
                    bool useHeel = stanceHeelAirMin > 0.001f && stanceHeelStrikeMinAge > 0.001f;
                    if (useHeel && swingAirQualified && phaseAge >= stanceHeelStrikeMinAge)
                    {
                        EnterTransfer(simTime);
                        intent.standLeg = 0f;
                        return;
                    }
                }
                swingAirStreak = 0f;
            }

            // Body is already ahead of the support — the step is overdue, cannot wait for the timer.
            float activeComTrigger = stanceComTrigger + ActiveWalkTargetSpeed() * stanceComTriggerPerSpeed;
            float triggerMinAge = Mathf.Max(
                Mathf.Max(0.05f, stanceComMinAge),
                ActiveStanceDuration() * Mathf.Clamp01(walkComTriggerMinStanceFraction));
            // WalkLift: first airborne frame sets swingWasAirborne, then
            // this gate zeros standLeg at −40° (Headless −walkComTrigger
            // 0.01). The overlay confirms the plant; DualSupport is the
            // dump (walklift_swap 7.93 s).
            if (!walkLiftMode
                && activeComTrigger > 0.0001f
                && phaseAge >= triggerMinAge
                && swingWasAirborne
                && StanceComLead() >= activeComTrigger)
            {
                EnterTransfer(simTime);
                intent.standLeg = 0f;
                return;
            }

            float activeStance = Mathf.Max(0.1f, ActiveStanceDuration());
            if (!IsRunning() && SwapCount == 0)
                activeStance = Mathf.Min(activeStance, Mathf.Max(0.1f, firstWalkStanceDuration));
            if (walkLiftMode)
            {
                // Planted clock EnterStance is the wall (keep35/spent0).
                // Swap only after WalkLift Advance plants an airborne
                // swing AHEAD of CoM. Idle + ComLeftSupport +
                // SwingFootIsAhead relabelled the 3 cm viewpoint lunge
                // (right already leading) as a step: first MaySwap /
                // scrape, then the new swing is the old lead, then
                // EnterStance onto a sole behind CoM — burn_walk 5.835,
                // swap 2, RelaxAllMuscles. Unload on PlantStandReady
                // is still the lift. Direct EnterStance, not Transfer.
                if (balance.WalkLiftMaySwap && WalkLiftSwapSettled())
                    EnterStance(simTime, -currentStance);
                return;
            }
            if (!walkLiftBusy && phaseAge >= activeStance)
            {
                EnterTransfer(simTime);
                intent.standLeg = 0f;
            }
            return;
        }

        if (balance != null && balance.useWalkLift != 0)
        {
            // WalkLift never asked for DualSupport. Restore the split
            // without counting a swap.
            phase = Phase.Stance;
            phaseEnterTime = simTime;
            if (intent != null)
                intent.standLeg = currentStance;
            return;
        }

        intent.standLeg = 0f;
        bool ready = IsTransferReady();
        if (ready)
            TransferReadyFrames++;
        bool swingGroundedNow = IsSwingFootGrounded();
        if (phaseAge >= Mathf.Max(transferMinDuration, transferTouchdownMaxAge) && swingGroundedNow)
        {
            EnterStance(simTime, -currentStance);
            return;
        }
        if (phaseAge >= transferMinDuration && ready)
        {
            EnterStance(simTime, -currentStance);
            return;
        }
        if (phaseAge >= ActiveTransferMaxDuration() && IsTransferFallbackReady())
        {
            EnterStance(simTime, -currentStance);
            return;
        }
        // If the swing stays airborne, BothFeet may never become true and
        // Transfer can stall visually as a frozen half-step. Force a phase
        // switch after a bounded timeout in both walk and run.
        float hardTimeout = IsRunning() ? 1.75f : 1.25f;
        if (phaseAge >= ActiveTransferMaxDuration() * hardTimeout)
            EnterStance(simTime, -currentStance);
    }

    private bool IsRunning()
    {
        return intent != null && intent.run > 0.5f;
    }

    private float ActiveStanceDuration()
    {
        if (IsRunning())
            return Mathf.Max(0.5f, runStanceDuration);

        float duration = stanceDuration;
        float targetSpeed = ActiveWalkTargetSpeed();
        if (walkCadenceStanceGain > 0.0001f && targetSpeed > 0.0001f)
            duration = Mathf.Max(walkCadenceMinStance, duration - targetSpeed * walkCadenceStanceGain);
        return Mathf.Max(0.1f, duration);
    }

    private float ActiveTransferMaxDuration()
    {
        if (IsRunning())
            return Mathf.Max(0.5f, runTransferMaxDuration);

        float duration = transferMaxDuration;
        float targetSpeed = ActiveWalkTargetSpeed();
        if (walkCadenceTransferGain > 0.0001f && targetSpeed > 0.0001f)
            duration = Mathf.Max(walkCadenceMinTransfer, duration - targetSpeed * walkCadenceTransferGain);
        return Mathf.Max(0.1f, duration);
    }

    private float ActiveWalkTargetSpeed()
    {
        return balance != null ? Mathf.Max(0f, balance.walkTargetSpeed) : 0f;
    }

    private void EnterTransfer(float simTime)
    {
        phase = Phase.Transfer;
        phaseEnterTime = simTime;
        swingWasAirborne = false;
        swingAirStreak = 0f;
        swingAirQualified = false;
        prevTickTime = -1f;
    }

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

    // Same leftover shin BalanceController commands in Plant (CatchReachShinDeg
    // at this Plant age). Capture here so Fall/Reach vault can start from it
    // after lastReachExtendProgress is zeroed and lastFallDuration belongs
    // to this Fall. catchPlantExitFold skips Max(Last) when Recover already
    // skipped Last: otherwise Plant→Fall yanks leftover against the plant-
    // clock shin Plant was still showing. Flag 0 / no Recover skip latch
    // is the former Max(Last, held).
    private float ComputePlantExitStanceShinDeg(float plantAge)
    {
        float flex = Mathf.Max(0f, reachKneeFlex);
        float fold = flex;
        if (fallSwingReachLiftClock > 0.5f)
        {
            float foldRamp = Mathf.Max(0.02f, fallKneeRampSeconds);
            float foldClock = Mathf.Clamp01(
                (lastFallDuration + lastReachDuration) / foldRamp);
            if (fallSwingFallFoldLead > 0.5f)
                foldClock = Mathf.Clamp01(foldClock * 2f);
            fold = flex * foldClock;
        }
        float start = Mathf.Clamp01(lastReachExtendProgress);
        float catchRamp = Mathf.Max(0.05f, catchMinDuration);
        bool plantClock = fallSwingCatchAirExtendHold > 0.5f;
        float held = plantClock
            ? lastCatchPlantDuration + lastRecoverDuration + plantAge
            : lastCatchDuration + lastRecoverDuration + plantAge;
        float clock = Mathf.Clamp01(held / catchRamp);
        bool skipLast = fallSwingCatchScrapeLiftAirCatchPlantExitFold > 0.5f
            && fallSwingPlantStanceExtendClock > 0.5f
            && ((fallSwingCatchScrapeLiftAirCatchEndFold > 0.5f
                    && RecoveredFromCatchScrapeAir)
                || (fallSwingCatchScrapeLiftAirCatchAirEndFold > 0.5f
                    && RecoveredFromCatchAir)
                || (fallSwingCatchScrapeLiftAirCatchAirEnterEndFold > 0.5f
                    && RecoveredFromCatchAirEnter)
                || (fallSwingCatchScrapeLiftAirCatchGroundCatchEndFold > 0.5f
                    && RecoveredFromCatchGroundCatch));
        float progress = skipLast ? clock : Mathf.Max(start, clock);
        float shin = Mathf.Lerp(fold, Mathf.Max(0f, reachKneeLandDeg), progress);
        return ApplyPlantMidStanceExtend(shin, plantAge);
    }

    // fallSwingPlantMidStanceExtend above. Roll-over fraction of the same
    // band Plant→Fall waits on, so the extension is a measured quantity and
    // not a clock: CoM still behind the plant reads 0, CoM at
    // plantComLeadMax reads 1. The age factor is the soft start — the same
    // shape walkPlantRockLead uses, and it is what keeps the command
    // continuous at Plant enter on a plant that landed with the CoM already
    // ahead of the foot (a step in a knee setpoint is a step in the knee
    // torque, which is the yank every ramp in this file exists to remove).
    public float PlantMidStanceExtendFraction(float plantAge)
    {
        if (fallSwingPlantMidStanceExtend <= 0.001f)
            return 0f;
        float leadBound = Mathf.Max(0.02f, plantComLeadMax);
        float roll = Mathf.Clamp01(StanceComLead() / leadBound);
        float ageClock = Mathf.Clamp01(
            plantAge / Mathf.Max(0.05f, plantMinDuration));
        return Mathf.Clamp01(fallSwingPlantMidStanceExtend) * roll * ageClock;
    }

    // BalanceController commands this shin in Plant and freezes the same
    // number at Plant→Fall, so both must read one schedule or the exit jumps
    // the knee back to landDeg. The 0.51° floor is not a fudge: the consumer
    // arms the whole plant pose on `stancePlantKnee > 0.5f`, and dropping
    // through it hands the strut to walkStanceKneeBend, which would ADD its
    // bend — a step up on the leg we just straightened. A leg configured
    // without a landing flex at all (reachKneeLandDeg 0) keeps the old
    // closed gate.
    public float ApplyPlantMidStanceExtend(float shinDeg, float plantAge)
    {
        if (shinDeg <= 0.5f)
            return shinDeg;
        float ext = PlantMidStanceExtendFraction(plantAge);
        if (ext <= 0.001f)
            return shinDeg;
        return Mathf.Max(0.51f, Mathf.Lerp(shinDeg, 0f, ext));
    }

    private void EnterFallCatchPhase(FallCatchPhase next, float simTime)
    {
        // Catch→Reach is a catch that found no plant. Reach→Catch is the
        // healthy exit and is not counted.
        if (fallCatchPhase == FallCatchPhase.Catch && next == FallCatchPhase.Reach)
            fallCatchBounceCount++;
        if (next == FallCatchPhase.Fall)
            fallCatchBounceCount = 0;
        if (fallCatchPhase == FallCatchPhase.Fall && next != FallCatchPhase.Fall)
            lastFallDuration = Mathf.Max(0f, simTime - phaseEnterTime);
        if (fallCatchPhase == FallCatchPhase.Reach && next != FallCatchPhase.Reach)
            lastReachDuration = Mathf.Max(0f, simTime - phaseEnterTime);
        if (fallCatchPhase == FallCatchPhase.Catch && next != FallCatchPhase.Catch)
        {
            lastCatchDuration = Mathf.Max(0f, simTime - phaseEnterTime);
            lastCatchPlantDuration = CatchPlantAge();
        }
        // Plant→Reach is the runaway escape: a catch step with no initiation
        // Fall. Everything Plant→Fall hands over belongs to the new swing
        // here too — and StandingRestore above all, because the placement
        // commands in BalanceController are gated on !StandingRestore and
        // the escape leaves a Plant whose gate had just raised it.
        bool plantEscapeReach = fallCatchPhase == FallCatchPhase.Plant
            && next == FallCatchPhase.Reach;
        // Freeze leftover before Fall zeros lastReachExtendProgress and
        // before this Fall overwrites lastFallDuration at the Reach gate.
        // The escape needs the same freeze: the stance shin is the leg the
        // body is standing on, and Reach reads that number as the vault
        // start (LastPlantStanceShinDeg), so losing it steps the knee.
        if (fallCatchPhase == FallCatchPhase.Plant
            && (next == FallCatchPhase.Fall || plantEscapeReach))
            lastPlantStanceShinDeg = ComputePlantExitStanceShinDeg(
                Mathf.Max(0f, simTime - phaseEnterTime));
        // Read the old phase before the assign: Catch→Reach must keep the
        // air clock that Catch was still extending. Fall→Reach still starts
        // a new window (early lift, or timeout with the foot down).
        // Catch timeout bounces on !planted (air or toe scrape). Air-only
        // resume let a scrape reset the clock and refold the shin on dirt.
        bool scrapeOk = fallSwingCatchReachScrapeResume > 0.5f
            ? !IsSwingFootPlanted()
            : !IsSwingFootGrounded();
        bool resumeReach = next == FallCatchPhase.Reach
            && fallSwingCatchReachResume > 0.5f
            && fallCatchPhase == FallCatchPhase.Catch
            && scrapeOk
            && reachAirEnterTime >= 0f;
        // Latch before the clear: Catch scrapeExtendAirAge already resumed
        // Last from lift-off. ReachResumedFromScrape is grounded-only, so
        // a timeout bounce after that lift-off Max'd Last with live
        // Progress (dirt counted as air) and Tick then wrote that jump.
        bool catchScraped = catchScrapedThisPhase;
        bool scrapeThenAirEnter = resumeReach
            && catchScraped
            && !IsSwingFootGrounded()
            && fallSwingCatchScrapeExtendHold > 0.5f
            && fallSwingCatchScrapeExtendAirAge > 0.5f
            && fallSwingCatchScrapeExtendAirEnterHold > 0.5f;
        // scrapeLiftAirHold already kept ThighHold through Catch air.
        // CatchScrapedThisPhase is cleared below, so the bounce would
        // take forceSwingFlex −swingHipFlex (sci_a). Latch that scrape
        // then air independently of the shin enterHold so lift can keep
        // ThighHold when extend flags are off. Quiet when Catch lift
        // already jumped (airLiftHold / scrapeLiftAirHold 0).
        // scrapeLiftAirCatchHold: that keep is Catch→Reach only.
        // ReachResumedFromCatchScrapeAir is cleared below, so the return
        // bounce would take −swingHipFlex. Latch independently of shin
        // so lift can keep ThighHold when extend flags are off. Quiet
        // when enterHold is 0 (Reach already jumped). Heel plant Catch
        // does not latch (landing hip). CatchResumedFromReachScrapeAir
        // also re-arms scrapeThenAirLiftEnter so a later Catch→Reach
        // does not lose the hip after CatchScrapedThisPhase stayed off.
        bool priorCatchFromReachScrapeAir = CatchResumedFromReachScrapeAir;
        bool scrapeThenAirLiftEnter = resumeReach
            && (catchScraped || priorCatchFromReachScrapeAir)
            && !IsSwingFootGrounded()
            && fallSwingCatchAirLiftHold > 0.5f
            && fallSwingCatchScrapeLiftAirHold > 0.5f
            && fallSwingCatchScrapeLiftAirEnterHold > 0.5f;
        bool scrapeThenAirCatchEnter = next == FallCatchPhase.Catch
            && fallCatchPhase == FallCatchPhase.Reach
            && ReachResumedFromCatchScrapeAir
            && !IsSwingFootPlanted()
            && fallSwingCatchAirLiftHold > 0.5f
            && fallSwingCatchScrapeLiftAirHold > 0.5f
            && fallSwingCatchScrapeLiftAirEnterHold > 0.5f
            && fallSwingCatchScrapeLiftAirCatchHold > 0.5f;
        // scrapeLiftAirCatchArm already kept reachArm through airborne
        // Catch. CatchScrapedThisPhase / CatchResumedFromReachScrapeAir
        // clear below, so Recover/Plant FallCatchArmCatchEnd would Lerp
        // reachArm→carry on stale Last against leftover shin. Latch
        // scrape then air (toe scrape + lift-off, or the return bounce)
        // through Recover→Plant. Heel-only Catch does not latch (no
        // catchArm keep). Quiet when catchArm is 0 (Catch already
        // Last-carried) or catchEndArm is 0. Recover/Plant shin skip
        // Last after this latch is catchEndFold. Catch planted scrape
        // shin skip Last is catchGroundCatchFold (catchGroundFold needs
        // !planted).
        bool scrapeThenAirCatchEnd = next == FallCatchPhase.Recover
            && fallCatchPhase == FallCatchPhase.Catch
            && fallSwingCatchScrapeLiftAirCatchEndArm > 0.5f
            && fallSwingCatchScrapeLiftAirCatchArm > 0.5f
            && fallSwingCatchScrapeLiftAirCatchFold > 0.5f
            && fallSwingCatchAirLiftHold > 0.5f
            && fallSwingCatchScrapeLiftAirHold > 0.5f
            && (priorCatchFromReachScrapeAir
                || (catchScraped && scrapeResumeAirEnterTime >= 0f));
        // scrapeLiftAirCatchAirArm already kept reachArm through true
        // airborne Catch. RecoveredFromCatchScrapeAir is scrape-only, so
        // Recover/Plant FallCatchArmCatchEnd would Lerp reachArm→carry
        // on stale Last against leftover shin. Latch never-scrape Catch
        // that actually flew (catchPlantEnterTime after Catch enter — heel
        // came down during Catch, so catchAirArm ran). Heel-only Catch
        // (planted at Catch enter) does not latch — planted Catch after
        // never-scrape Catch→Reach is RecoveredFromCatchAirEnter.
        // Recover/Plant shin skip Last after this latch is catchAirEndFold.
        // Quiet when catchAirArm is 0 (Catch already Last-carried) or catchArm is 0.
        bool neverScrapeAirCatchEnd = next == FallCatchPhase.Recover
            && fallCatchPhase == FallCatchPhase.Catch
            && fallSwingCatchScrapeLiftAirCatchAirEndArm > 0.5f
            && fallSwingCatchScrapeLiftAirCatchAirArm > 0.5f
            && fallSwingCatchScrapeLiftAirCatchArm > 0.5f
            && !catchScraped
            && !priorCatchFromReachScrapeAir
            && catchPlantEnterTime >= 0f
            && catchPlantEnterTime > phaseEnterTime + 0.0001f;
        // scrapeLiftAirCatchAirEnterGroundFold already kept Reach fold
        // through grounded never-scrape Reach. Heel plant then leaves
        // Reach for Catch already planted. RecoveredFromCatchAir needs
        // catchPlantEnterTime after Catch enter, so Recover/Plant
        // FallCatchArmCatchEnd would Lerp reachArm→carry on stale Last
        // against leftover shin. Latch that planted Catch enter
        // independently of catchAirEndArm so CatchEnd can skip Last
        // when the bounce already kept fold/arm. Catch-phase skip Last
        // on that planted Catch is catchAirEnterCatchArm (reads
        // CatchResumedFromReachAir; catchAirGroundArm needs CatchPlantAge
        // < PhaseAge). Catch-phase shin skip Last on that planted Catch
        // is catchAirEnterCatchFold (catchAirGroundFold needs !planted).
        // Recover/Plant shin skip Last after that Catch is
        // catchAirEnterEndFold (CatchReachExtendProgress still Max Last).
        // Quiet when
        // catchAirEnterGroundFold is 0 (shin already dropped) or
        // catchAirEnterGroundArm is 0 (arms already Last-carried) or
        // catchAirEnterEndArm is 0. Airborne Reach→Catch is
        // neverScrapeAirCatchEnd (catchAirArm still ran).
        bool priorCatchFromReachAir = CatchResumedFromReachAir;
        bool neverScrapeReachCatchEnter = next == FallCatchPhase.Catch
            && fallCatchPhase == FallCatchPhase.Reach
            && ReachResumedFromCatch
            && !ReachResumedFromCatchScrapeAir
            && IsSwingFootPlanted()
            && fallSwingCatchScrapeLiftAirCatchAirEnterEndArm > 0.5f
            && fallSwingCatchScrapeLiftAirCatchAirEnterGroundFold > 0.5f
            && fallSwingCatchScrapeLiftAirCatchAirEnterGroundArm > 0.5f
            && fallSwingCatchScrapeLiftAirCatchAirEnterArm > 0.5f
            && fallSwingCatchScrapeLiftAirCatchAirEnterFold > 0.5f
            && fallSwingCatchScrapeLiftAirCatchAirFold > 0.5f
            && fallSwingCatchScrapeLiftAirCatchAirArm > 0.5f
            && fallSwingCatchScrapeLiftAirCatchArm > 0.5f;
        bool neverScrapeEnterCatchEnd = next == FallCatchPhase.Recover
            && fallCatchPhase == FallCatchPhase.Catch
            && priorCatchFromReachAir
            && fallSwingCatchScrapeLiftAirCatchAirEnterEndArm > 0.5f
            && fallSwingCatchScrapeLiftAirCatchAirEnterGroundFold > 0.5f
            && fallSwingCatchScrapeLiftAirCatchAirEnterGroundArm > 0.5f
            && fallSwingCatchScrapeLiftAirCatchAirEnterArm > 0.5f
            && fallSwingCatchScrapeLiftAirCatchAirEnterFold > 0.5f
            && fallSwingCatchScrapeLiftAirCatchAirFold > 0.5f
            && fallSwingCatchScrapeLiftAirCatchAirArm > 0.5f
            && fallSwingCatchScrapeLiftAirCatchArm > 0.5f;
        // scrapeLiftAirCatchGroundArm already skipped Last-carry on
        // grounded scrape Catch, and catchGroundCatchFold skipped Last on
        // planted scrape shin. CatchScrapedThisPhase clears below, and
        // RecoveredFromCatchScrapeAir needs scrapeResumeAirEnterTime, so
        // never-air Recover/Plant FallCatchArmCatchEnd would Lerp
        // reachArm→carry on stale Last against leftover shin, and
        // CatchReachExtendProgress would Max Last (140°→~56°). Latch
        // scrape that planted without lift-off. Scrape-then-air (this
        // Catch or the return bounce) is scrapeThenAirCatchEnd. Quiet
        // when catchGroundArm is 0 (Catch already Last-carried) or
        // catchEndArm is 0. Recover/Plant shin skip Last after this latch
        // is catchGroundCatchEndFold.
        bool scrapeNeverAirCatchEnd = next == FallCatchPhase.Recover
            && fallCatchPhase == FallCatchPhase.Catch
            && fallSwingCatchScrapeLiftAirCatchEndArm > 0.5f
            && fallSwingCatchScrapeLiftAirCatchGroundArm > 0.5f
            && fallSwingCatchScrapeLiftAirCatchGroundFold > 0.5f
            && fallSwingCatchScrapeLiftAirCatchFold > 0.5f
            && fallSwingCatchAirLiftHold > 0.5f
            && fallSwingCatchScrapeLiftAirHold > 0.5f
            && catchScraped
            && !priorCatchFromReachScrapeAir
            && scrapeResumeAirEnterTime < 0f
            && IsSwingFootPlanted();
        fallCatchPhase = next;
        phaseEnterTime = simTime;
        ReachResumedFromCatch = resumeReach;
        ReachResumedFromScrape = resumeReach && (IsSwingFootGrounded() || scrapeThenAirEnter);
        ReachResumedFromCatchScrapeAir = scrapeThenAirLiftEnter;
        CatchResumedFromReachScrapeAir = scrapeThenAirCatchEnter;
        RecoveredFromCatchScrapeAir = scrapeThenAirCatchEnd;
        RecoveredFromCatchAir = neverScrapeAirCatchEnd;
        CatchResumedFromReachAir = neverScrapeReachCatchEnter;
        RecoveredFromCatchAirEnter = neverScrapeEnterCatchEnd;
        RecoveredFromCatchGroundCatch = scrapeNeverAirCatchEnd;
        catchScrapedThisPhase = false;
        if (next == FallCatchPhase.Reach)
        {
            reachEntryStanceComOffset = StanceComLead();
            scrapeResumeAirEnterTime = -1f;
            scrapeResumeExtendStart = 0f;
            if (resumeReach)
            {
                swingWasAirborne = true;
                // scrapeExtendEnterHold: Catch scrapeExtendHold already
                // froze shin/arm at Last. Max with live Progress here
                // writes the 70°→35° / carry that Catch just avoided.
                // Airborne bounce still Max's. Catch freeze off still
                // Max's so Last tracks the live shin Catch was showing.
                // scrapeExtendAirEnterHold: that keep is grounded-only.
                // After Catch scrape then lift-off, AirAge already resumed
                // Last; Max / the first Reach tick would still write live
                // Progress. Keep Last; latch ReachResumedFromScrape so Tick
                // continues from the bounce (not Catch air age). Flag 0 is
                // the former Max even after Catch scrape then air.
                bool keepLast = ReachResumedFromScrape
                    && fallSwingCatchReachScrapeExtendEnterHold > 0.5f
                    && fallSwingCatchScrapeExtendHold > 0.5f;
                if (scrapeThenAirEnter)
                    keepLast = true;
                if (!keepLast)
                    lastReachExtendProgress = Mathf.Max(
                        lastReachExtendProgress, ReachExtendProgress());
            }
            else
            {
                reachAirEnterTime = IsSwingFootGrounded() ? -1f : simTime;
                lastReachExtendProgress = 0f;
                if (reachAirEnterTime >= 0f)
                {
                    swingWasAirborne = true;
                    lastReachExtendProgress = ReachExtendProgress();
                }
            }
            // scrapeLiftAirCatchFold starts this clock on Catch enter so
            // fold extraAge is lift-off, not 0. Catch→Reach resets it
            // above; start it here after keepLast so enterFold extraAge
            // does not sit at 0 until the first Reach Tick (and so a
            // lift-only bounce, where ReachResumedFromScrape is off,
            // still ages). scrapeResumeExtendStart follows the Last
            // this bounce actually kept.
            if (scrapeThenAirLiftEnter
                && !IsSwingFootGrounded()
                && fallSwingCatchScrapeLiftAirEnterFold > 0.5f
                && scrapeResumeAirEnterTime < 0f)
            {
                scrapeResumeAirEnterTime = simTime;
                scrapeResumeExtendStart = lastReachExtendProgress;
            }
        }
        if (next == FallCatchPhase.Catch)
        {
            catchEntrySwingClearance = SwingFootClearance();
            catchPlantEnterTime = IsSwingFootPlanted() ? simTime : -1f;
            // Resume Catch may arrive airborne with the scrape-air clock
            // still -1 (Reach Tick starts it only when ReachResumedFromScrape).
            // Start it here so fold extraAge is lift-off age, not 0.
            // Heel plant does not.
            if (scrapeThenAirCatchEnter
                && !IsSwingFootGrounded()
                && fallSwingCatchScrapeLiftAirCatchFold > 0.5f
                && scrapeResumeAirEnterTime < 0f)
            {
                scrapeResumeAirEnterTime = simTime;
                scrapeResumeExtendStart = lastReachExtendProgress;
            }
        }
        else
            catchPlantEnterTime = -1f;
        if (next == FallCatchPhase.Fall || plantEscapeReach)
        {
            StandingRestore = false;
            lastReachExtendProgress = 0f;
            ReachResumedFromCatch = false;
            ReachResumedFromScrape = false;
            ReachResumedFromCatchScrapeAir = false;
            CatchResumedFromReachScrapeAir = false;
            RecoveredFromCatchScrapeAir = false;
            RecoveredFromCatchAir = false;
            CatchResumedFromReachAir = false;
            RecoveredFromCatchAirEnter = false;
            RecoveredFromCatchGroundCatch = false;
            scrapeResumeAirEnterTime = -1f;
            scrapeResumeExtendStart = 0f;
        }
        // The escape has no Fall behind it, so the swing lift and the stance
        // vault must clock off this Reach's own age. Inheriting the previous
        // cycle's Fall duration finishes the 0.15 s ramp before the first
        // tick — a step in the hip and knee commands on exactly the edge
        // this escape has to cross smoothly. The stance shin setpoint is
        // carried by lastPlantStanceShinDeg above, captured before this.
        if (plantEscapeReach)
            lastFallDuration = 0f;
        if (next == FallCatchPhase.Plant || next == FallCatchPhase.Fall)
        {
            // New support: the previous swing's air latch must not open
            // stance push / heel-strike on a foot that has not left the
            // ground this Plant. EnterStandingPlant / EnterFallCatchPlant
            // already clear the three; this is the EnterFallCatchPhase
            // path (and Plant→Fall, whose swing has not flown yet).
            swingWasAirborne = false;
            swingAirStreak = 0f;
            swingAirQualified = false;
        }
    }

    // Emergency: the step failed, stand on the same foot. No role swap and
    // no cycle count — those belong to a completed Recover plant. Plant→Fall
    // still uses TiltSettled + signed lead ≤ plantComLeadMax after this.
    private void EnterStandingPlant(float simTime)
    {
        // Emergency stand, including SinkCollapsed: leftover Fall/Reach/
        // Catch clocks belong to the failed step. Keeping lastFallDuration
        // (or catchPlantEnterTime) would feed ComputePlantExitStanceShinDeg
        // a fold ramp that this Plant never ran. Capture is for a completed
        // Recover→Plant (EnterFallCatchPlant), not this abort.
        lastFallDuration = 0f;
        lastReachDuration = 0f;
        lastCatchDuration = 0f;
        lastCatchPlantDuration = 0f;
        lastReachExtendProgress = 0f;
        lastRecoverDuration = 0f;
        lastRecoverStanceSign = 0f;
        lastPlantStanceShinDeg = 0f;
        reachEntryStanceComOffset = 0f;
        catchEntrySwingClearance = 0f;
        catchPlantEnterTime = -1f;
        fallCatchPhase = FallCatchPhase.Plant;
        phaseEnterTime = simTime;
        swingWasAirborne = false;
        swingAirStreak = 0f;
        swingAirQualified = false;
        reachAirEnterTime = -1f;
        scrapeResumeAirEnterTime = -1f;
        scrapeResumeExtendStart = 0f;
        prevTickTime = -1f;
        StandingRestore = true;
        PlantRolledFromBehind = IsComCaughtByStanceFoot();
        // Fresh Plant: the ladder gets a fresh deadline and its re-seat back.
        // The stall clock starts here — the failed step is exactly the pose
        // the gate is going to refuse.
        plantGateStallSince = simTime;
        plantReseated = false;
        plantStandSurrender = false;
        fallCatchBounceCount = 0;
        ReachResumedFromCatch = false;
        ReachResumedFromScrape = false;
        ReachResumedFromCatchScrapeAir = false;
        CatchResumedFromReachScrapeAir = false;
        RecoveredFromCatchScrapeAir = false;
        RecoveredFromCatchAir = false;
        CatchResumedFromReachAir = false;
        RecoveredFromCatchAirEnter = false;
        RecoveredFromCatchGroundCatch = false;
        catchScrapedThisPhase = false;
        // Failed step: if the old stance left the ground, stand on the
        // remaining loaded foot. Do not PickStanceFromFeet — a split
        // pose would hand stance to the front foot and look like a swap.
        HoldLoadedStance();
        if (intent != null)
            intent.standLeg = currentStance;
        // Failed Fall/Reach/Catch already past the abort band: two-foot
        // quiet stance now, not a 3 s split Plant. Mild stalls (Reach
        // grounded at 18°) still take the reseat / surrender ladder.
        if (RecoverPoseIsLost())
            SurrenderWalkToStand(simTime);
    }

    private void EnterFallCatchPlant(float simTime)
    {
        // Roles come from where the feet are, not from whose turn it is.
        // After a catch the leading foot carries the weight, so the trailing
        // one is the only leg free to reach. Alternating blindly hands the
        // swing role to the front foot and the body walks itself backwards.
        if (fallCatchPhase == FallCatchPhase.Recover)
            lastRecoverDuration = Mathf.Max(0f, simTime - phaseEnterTime);
        lastRecoverStanceSign = currentStance;
        currentStance = PickStanceFromFeet(-currentStance);
        PlantRolledFromBehind = IsComCaughtByStanceFoot();
        fallCatchPhase = FallCatchPhase.Plant;
        phaseEnterTime = simTime;
        swingWasAirborne = false;
        swingAirStreak = 0f;
        swingAirQualified = false;
        reachAirEnterTime = -1f;
        scrapeResumeAirEnterTime = -1f;
        scrapeResumeExtendStart = 0f;
        catchPlantEnterTime = -1f;
        prevTickTime = -1f;
        SwapCount++;
        FallCatchCycleCount++;
        StandingRestore = false;
        // A completed cycle: the ladder starts over on the new support.
        plantGateStallSince = -1f;
        plantReseated = false;
        plantStandSurrender = false;
        fallCatchBounceCount = 0;
        ReachResumedFromCatch = false;
        ReachResumedFromScrape = false;
        ReachResumedFromCatchScrapeAir = false;
        CatchResumedFromReachScrapeAir = false;
        CatchResumedFromReachAir = false;
        // RecoveredFromCatchScrapeAir / RecoveredFromCatchAir /
        // RecoveredFromCatchAirEnter / RecoveredFromCatchGroundCatch stay:
        // Plant still reads CatchEnd.
        catchScrapedThisPhase = false;
        if (intent != null)
            intent.standLeg = currentStance;
    }

    private void EnterStance(float simTime, float stance)
    {
        currentStance = stance;
        phase = Phase.Stance;
        phaseEnterTime = simTime;
        swingWasAirborne = false;
        swingAirStreak = 0f;
        swingAirQualified = false;
        prevTickTime = -1f;
        SwapCount++;
        // Post-swap plant: TiltSettled + signed CoM-to-stance before the
        // next Unload. A plant outside that band used to split immediately
        // (burn_walk: 3 swaps / 0.835 s / 90°). First plant still uses
        // |lead| ≤ plantComLeadMax; later plants keep signed lead so a
        // catch behind the new sole is a stand. Quiet swap already in
        // the band never surrenders.
        if (walkRecoverAnyPose > 0.5f && useFallCatchWalk == 0
            && !RecoverStandReady())
        {
            SurrenderWalkToStand(simTime);
            return;
        }
        if (intent != null)
            intent.standLeg = currentStance;
    }

    // currentStance +1 = stance on the right → swing on the left; −1 the other way.
    // GetContacts-only used to report both soles as air while the solver
    // still held them (toe-roll hang, walkSwap 0). NearContact is the same
    // band IsFirstStepPlanted / PickStance already used on cycle 0.
    private bool IsSwingFootGrounded()
    {
        if (bodyState == null) return true;
        return currentStance > 0.5f
            ? FootDown(true)
            : FootDown(false);
    }

    private bool IsStanceFootGrounded()
    {
        if (bodyState == null) return true;
        return currentStance > 0.5f
            ? FootDown(false)
            : FootDown(true);
    }

    private bool FootDown(bool left)
    {
        if (bodyState == null) return true;
        if (left ? bodyState.leftFootGrounded : bodyState.rightFootGrounded)
            return true;
        float clearance = left
            ? bodyState.leftFootGroundClearance
            : bodyState.rightFootGroundClearance;
        return clearance <= ContactOffsetBand();
    }

    // Plant→Fall and BeginCycle share this band. Forward CoM past
    // plantComLeadMax (0.10 m) blocks on later cycles; CoM behind the
    // foot does not — that is the catch pose. Cycle 0 initiation has no
    // catch plant: a 10 cm third-law shift from the knee lift is still
    // a stand (RecoverPoseIsLost is 0.30 m). Using 0.10 there shut the
    // gate, raised StandingRestore, and turned rock / lean / geo-lift
    // off: Fall never started (walkSwap 0). Stance in the air is not a
    // stand.
    // WalkLift never increments FallCatchCycleCount. SwapCount is that
    // first-plant bit there so a post-swap catch keeps the signed bound
    // instead of the 0.30 m lunge ceiling.
    private bool IsFirstWalkPlant()
    {
        if (useFallCatchWalk != 0)
            return FallCatchCycleCount == 0;
        return SwapCount == 0;
    }

    private bool PlantComReady()
    {
        float lead = StanceComLead();
        float bound = IsFirstWalkPlant()
            ? RecoverComLostMeters
            : Mathf.Max(0.05f, plantComLeadMax);
        return lead <= bound;
    }

    // Cycle 0 commands ~9° of Plant lean (fallPelvisLean × fallForwardLeanScale).
    // TiltSettled is 12°, and initiation+8° was 17°: the overshoot of the
    // command that starts the step shut the gate, raised StandingRestore,
    // and killed geo-lift / lean before Fall. That lean IS the first
    // fall — Fall abort already Catchs it. Cycle 0 Plant does not judge
    // tilt; sink and |lead| > RecoverComLostMeters still fail the gate.
    // Later cycles keep 12° / 40 °/s: a post-catch Plant must actually
    // be standing.
    private bool PlantTiltReady()
    {
        if (IsFirstWalkPlant())
            return true;
        return TiltSettled();
    }

    // Sensors that TiltSettled / StanceComLead / FootDown actually read.
    // Fail closed: bodyState null, vestibular null, or sensorTick 0
    // (ArmWalk in Update before the first estimator step) used to look
    // like a stand. FootDown treats clearance 0 as down, AbsBodyTilt
    // is 0 without a torso, StanceComLead is 0 without CoM — that
    // launched a heap, then RelaxAllMuscles. Quiet BeginWalk after a
    // few idle steps has sensorTick in the thousands.
    private bool PlantSensorsReady()
    {
        if (bodyState == null || vestibular == null || comCalculator == null)
            return false;
        return bodyState.sensorTick > 0;
    }

    // SinkCollapsed is the same band's height: a fold over the feet still
    // reads TiltSettled + lead, and without this BeginCycle would call it
    // a stand (walkRecoverAnyPose; 0 = former tilt/lead/ground).
    // Sensors first: cycle-0 still skips TiltSettled (9° initiation),
    // but it must not open Fall on spawn zeros. Later cycles keep
    // TiltSettled + signed lead ≤ plantComLeadMax.
    private bool PlantFallGateReady()
    {
        if (!PlantSensorsReady())
            return false;
        return PlantTiltReady() && PlantComReady() && IsStanceFootGrounded()
            && !SinkCollapsed();
    }

    // Pose half of the standing Plant: |tilt| < 12°, BOTH soles the
    // solver is holding, signed CoM-to-stance ≤ plantComLeadMax, not
    // a sink. FootDown's clearance-0 band is not a stand: a hovering
    // heap looked planted, Tick cleared restore, DualSupport restarted
    // in the air (p_auto 14:48: first SS 5.17 s, then 87 % air labelled
    // DualSupport, swap 0). Quiet launch is both manifold-grounded.
    // Omega is separate: DualSupport hold must not abort a quiet 2°
    // trunk when the mixer start spikes past RecoverOmegaSettledDeg 40.
    private bool StandingPlantPoseReady()
    {
        if (!PlantSensorsReady())
            return false;
        if (AbsBodyTilt() >= TiltSettledMaxDeg
            || !BothFeetPlanted()
            || SinkCollapsed())
            return false;
        return RecoverComToStanceReady();
    }

    // Standing Plant wait: two loaded soles, not FootDown clearance-0.
    // Cycle-0 PlantFallGateReady still uses IsStanceFootGrounded — that
    // split Plant may open Fall on one plate. Cached refs, no Find.
    private bool BothFeetPlanted()
    {
        if (bodyState == null)
            return false;
        return bodyState.leftFootGrounded && bodyState.rightFootGrounded;
    }

    // Standing Plant BeginWalk / restore / Plant→Fall wait share.
    // TiltSettled (12° / 40 °/s) + signed CoM-to-stance ≤ plantComLeadMax
    // (0.10 m) + the named stance sole down + not a sink. Cycle-0
    // PlantFallGateReady skips tilt and allows RecoverComLostMeters
    // 0.30 m so a 9° initiation can open Fall — that is live gait, not
    // a stand. CaptureColumnIsStanding froze a quiet 6° knee (phase 0 /
    // 35 s). CoM behind the stance foot is a catch, not a heap.
    // Happy-path numbers stay.
    private bool StandingPlantBandReady()
    {
        return StandingPlantPoseReady()
            && AbsBodyOmega() < RecoverOmegaSettledDeg;
    }

    // CaptureWalk BeginWalk / resume. Pose half of the standing band
    // (tilt 12° + both planted soles + signed CoM-to-stance ≤
    // plantComLeadMax), not omega. DualSupport hold already uses
    // StandingPlantHoldReady; mixer start can spike past 40 °/s on a
    // quiet 2° trunk. A 20° heap still fails TiltSettled. Not the
    // cycle-0 PlantFallGateReady skip.
    private bool CaptureWalkColdStandReady()
    {
        return StandingPlantHoldReady;
    }

    // Standing band the recovery ladder waits on. PlantFallGateReady on
    // cycle 0 skips TiltSettled so a 9° initiation lean can open Fall;
    // that skip must not call a tipped BeginWalk a stand. Numbers are
    // the happy-path ones: 12° / 40 °/s and plantComLeadMax (0.10 m).
    // walkRecoverAnyPose 0 keeps the former tilt-skipped gate.
    // WalkLift first plant is SwapCount == 0 (FallCatchCycleCount stays 0).
    private bool RecoverStandReady()
    {
        if (walkRecoverAnyPose <= 0.5f)
            return PlantFallGateReady();
        return StandingPlantBandReady();
    }

    // Standing Plant: signed CoM-to-stance ≤ plantComLeadMax. PlantComReady
    // on cycle 0 is RecoverComLostMeters (0.30 m) so a 9° initiation
    // third-law shift can still open Fall. That 0.30 m must not call a
    // cold BeginWalk with CoM 20 cm past the foot a stand — the walker
    // then launches and dumps. CoM behind the stance foot is a catch, not
    // a heap. Quiet start |lead| ~0 still passes.
    private bool RecoverComToStanceReady()
    {
        return StanceComLead() <= Mathf.Max(0.05f, plantComLeadMax);
    }

    // Heap the 3 s Plant stall cannot catch: abort tilt, a fold the
    // CoM-offset band cannot see, or cycle-0 CoM already past the
    // Falling metre. Not used in Fall/Reach/Catch — running past the
    // foot is the step. walkRecoverAnyPose 0 keeps the former ladder.
    private bool RecoverPoseIsLost()
    {
        if (walkRecoverAnyPose <= 0.5f)
            return false;
        if (SinkCollapsed())
        {
            // Failed-step Plant is a heap. Cycle-0 Plant before Fall is
            // too. After Recover the landing is still the initiation height
            // (lean + 55° already near sinkAbortDrop) — that is the new
            // support's first plantMinDuration, not a two-foot stand.
            if (StandingRestore || plantStandSurrender || FallCatchCycleCount == 0)
                return true;
            return PhaseAge() >= Mathf.Max(0.05f, plantMinDuration);
        }
        // Cycle 0: fallAbortDeg is the first fall (Fall/Reach Catch it).
        // Treating it as a lost Plant pose restored two-foot stance
        // before Fall, walkSwap=0. Later cycles still surrender at 30°.
        // WalkLift later-plant is SwapCount > 0: 30° there is a dump,
        // not initiation.
        // Exception: while the mass is running past the support the trunk
        // passes 30° inside the escape window (the swap arrives at −1.2°
        // and the tilt is 65° a second later), and this surrender fired
        // first — standLeg 0 on a body whose CoM is outside both soles is
        // the topple. A tall body with a loaded stance still has a step
        // left; a collapse (above) does not.
        if (!IsFirstWalkPlant()
            && fallAbortDeg > 0.001f && AbsBodyTilt() >= fallAbortDeg
            && !KeepSteppingOnRunaway())
            return true;
        // Cold BeginWalk past fallAbortDeg is a heap, not the first
        // commanded fall. Cycle-0 Fall still Catchs 30°; this only
        // fires while Plant is already restoring (StandingRestore),
        // which a quiet 9° initiation never is.
        if (IsFirstWalkPlant()
            && StandingRestore
            && fallAbortDeg > 0.001f && AbsBodyTilt() >= fallAbortDeg)
            return true;
        // WalkLift: CoM past the stance toe is the inverted-pendulum
        // step, not a heap, while Unload/Clear/Advance still owns the
        // swing or MaySwap is waiting for the catch plant. Surrender
        // here is standLeg=0 with |offset| already 0.30 —
        // RelaxAllMuscles (burn_walk 5.835). Fall-catch never sets
        // useWalkLift on this path. Sink / 30° still spend above.
        //
        // c125adbd 6.300 (every cadence, walkTime 5 + 1.30 s): first
        // DualSupport both-down, |StanceComLead| crossed
        // RecoverComLostMeters 0.30, this returned true, StandingRestore
        // killed CaptureWalk (walkStance 8 / walkTransfer 8 / walkFirst
        // right → walkSwapCount 0). That lead is the first catch orbit
        // (9° bow → ~0.31 m), not a chair. CaptureWalk / WalkLift first
        // plant keep measuring it; do not restore on it.
        if (IsFirstWalkPlant()
            && Mathf.Abs(StanceComLead()) > RecoverComLostMeters)
        {
            if (balance != null && balance.useCaptureWalker != 0)
                return false;
            if (useFallCatchWalk == 0
                && balance != null
                && balance.useWalkLift != 0
                && (balance.WalkLiftPhaseCode != 0
                    || balance.WalkLiftMaySwap
                    || SwapCount == 0))
                return false;
            return true;
        }
        return false;
    }

    // WalkLift abort / BalanceController: same two-foot Plant the
    // BeginWalk ladder already uses. Fall-catch has EnterStandingPlant.
    public void RequestStandingPlant()
    {
        if (!walkActive || walkRecoverAnyPose <= 0.5f)
            return;
        // Fall-catch FSM has EnterStandingPlant. Capture walker still
        // uses this two-foot Plant: useFallCatchWalk defaults to 1 even
        // when useCaptureWalker owns Tick, so the old gate was a no-op
        // and a launch squat never returned to stand.
        bool captureOn = balance != null && balance.useCaptureWalker != 0;
        if (useFallCatchWalk != 0 && !captureOn)
            return;
        float t = lastTickTime >= 0f ? lastTickTime : Time.fixedTime;
        SurrenderWalkToStand(t);
    }

    // GETUP-1 / any-pose restore: keep the capture walker off. Tick
    // clears this only on TiltSettled + signed CoM-to-stance (the same
    // BeginWalk Plant→Fall band). A −70° trunk is not that stand.
    public void HoldGetUpPlant()
    {
        StandingRestore = true;
        plantStandSurrender = true;
        if (intent != null)
            intent.standLeg = 0f;
        if (plantGateStallSince < 0f)
            plantGateStallSince = lastTickTime >= 0f ? lastTickTime : Time.fixedTime;
    }

    public void NotifyStoodUp()
    {
        StandingRestore = false;
        plantStandSurrender = false;
        plantGateStallSince = -1f;
        plantReseated = false;
        CaptureStandHeight();
        if (intent != null)
            intent.standLeg = 0f;
        if (balance != null)
            balance.NotifyStoodUpFromPlant();
    }

    // TiltSettled + CoM-to-stance opened. Same bits NotifyStoodUp
    // clears, plus GETUP success if a heap had armed attempts.
    // Happy path never calls this (StandingRestore was false).
    private void ReleaseStandingPlantGate(float simTime)
    {
        plantGateStallSince = -1f;
        plantReseated = false;
        StandingRestore = false;
        plantStandSurrender = false;
        CaptureStandHeight();
        phaseEnterTime = simTime;
        if (intent != null)
            intent.standLeg = 0f;
        if (balance != null)
            balance.NotifyStoodUpFromPlant();
    }

    private void SurrenderWalkToStand(float simTime)
    {
        plantStandSurrender = true;
        StandingRestore = true;
        if (plantGateStallSince < 0f)
            plantGateStallSince = simTime;
        if (intent != null)
            intent.standLeg = 0f;
    }

    // Cycle-0 Fall starts from the standing band already applied at
    // BeginCycle (TiltSettled + |CoM−stance| ≤ plantComLeadMax, else
    // two-foot surrender). StanceLead ≥ 0 never opens on stacked soles:
    // the assigned lead foot sits ~1.5 cm ahead of CoM and the walk
    // window moves ~9 mm, so Plant waited plantMinDuration then dumped
    // (burn_stag +1.30 s, walkSwap 0, knees 3–7°). plantAged still
    // gates the call site. Later cycles keep the capture trigger.
    //
    // Lower bound of the same band, kept OUT of PlantFallGateReady on
    // purpose: a shut gate arms the recovery ladder (re-seat, then surrender
    // to two-foot stance) and raises StandingRestore, and waiting for the
    // roll-over is neither a stall nor a failed step.
    //
    // Later cycles use the extrapolated CoM (capture point), not position:
    // xcom = lead + v/omega0. Position alone would hold a brisk walk far too
    // long — momentum carries the body over a foot it has not reached yet —
    // and would release a body drifting backward with CoM nominally ahead.
    // omega0 comes from the same effective CoM height the stance ankle and
    // the step placement already use, so "where the foot lands" and "when the
    // leg is released" read one number. Full weight here, not walkXCoMWeight:
    // that weight blends a prediction into a control error, this is the
    // prediction itself.
    //
    // Cycle 0 is not that walk: the assigned stance already sits ~1.5 cm
    // ahead of CoM (viewpoint lunge), so Capture >= fallComLeadTrigger
    // asks for 1.5 cm PAST that foot — outside the 3 cm polygon unless
    // the body is already falling. The mid-stance ankle hold (target 0)
    // cancels a 9° lean, but plantRockLead is that same loop's CoP
    // (ankleScale 1). Fading scale to 0 (comment 27) killed the CoP
    // (comment 28). RecoverBlend at 9° then cut that CoP to ~1 cm
    // (comment 30); trailing A2 on the stacked 26 cm sole put CoP at
    // the toe (a brake). Comment 31: that CoP on µ=1.2 stacked soles is
    // tilt, not translation, and the 9° lean must not be × RecoverBlend.
    // Comment 32: the lean on a pinned trail is a bow, not that 2.7 cm.
    // The trail CoM loop (heel) stays until StanceLead >= 0. Comment 33:
    // pelvisLean is a bow; 9° is lumbar only. Comment 34: hip-lock +
    // pelvis-track left StanceLead at -1.5 cm (overconstrained stack).
    // Comment 35: trailing-hip extend as a DrivePelvis counter is
    // cancelled by same-hip P. Comment 36: trail leaves DrivePelvis.
    // Comment 37: both hips SPD-extend to the same plus pose (the
    // leading weld blocked the parallelogram shear). Comment 38:
    // trail hip K=12 and ankle K=3 were the remaining 0 DOF; torsoLean
    // 9° with DrivePelvis off is the bow. Comment 39: that plus pose
    // on a free pelvis is a sit, not shear. Comment 40: trail WORLD
    // tilt + leading DrivePelvis still sat (DrivePelvis flexed the
    // lead). Comment 41: BOTH hips hold the same WORLD thigh tilt;
    // DrivePelvis off. Comment 42: plantRockLead + dual CoM ankle
    // stay at 0 so the WORLD pose is not a shin couple; pose is not
    // gated on trailLead / plantHipExtend. Hip+ankle dampers still
    // snap to tau K; torsoLean waits with pelvisLean for StanceLead >= 0.
    // Comment 43: those hip poses sit (internal couple) and
    // plantRockLead was gated off cycle 0. Restore the ankle CoP +
    // lumbar lean; DrivePelvis stays the pivot. Comment 44: pelvisLean
    // is the WORLD target that pivot holds. Snap only the ankle
    // dampers so the shin can rotate over the sole. Comment 45: that
    // target is still a bow. SPD-hold the quiet hip snapshot so the
    // snapped ankles are the hinge; plantRockLead is the CoP.
    // Comment 46: that lock has no DoF (two contacts). Cycle 0
    // Plant WORLD-holds both thighs at the same clockwise tilt
    // (sit cannot zero a world error). Snap hip+ankle dampers.
    // Comment 47: keep that shear, but do not zero the ankle CoM
    // loops — without plantRockLead the hip couple cannot move
    // CoM and StanceLead stays -1.5 cm. Freeze once StanceLead
    // >= 0; DrivePelvis stays off until EnterFall. Fire
    // when StanceLead >= 0 (trail N≈0). That is the same event
    // FirstStepTrailStillLoaded uses.
    // plantAged is not the cycle-0 trigger (ComLeftSupport at the call
    // site). Later cycles keep the capture trigger / deadline.
    private bool FallLeadReady(float phaseAge)
    {
        if (fallComLeadTrigger <= 0.0001f)
            return true;
        // Cycle 0: CoM leaving the support polygon, not plantAged / the
        // standing band. Stacked soles never grow StanceLead ≥ 0 (~9 mm),
        // and returning true here opened Fall on the timer — burn_stag
        // then hit fallCoMOffset 0.30 at +1.30 s with walkSwap 0.
        if (FallCatchCycleCount == 0)
            return ComLeftSupport;
        // Uncaught Recover swap: CoM entered Plant already past the new
        // stance. Opening Fall on the first aged tick is the 90° topple
        // (capture ≥ 1.5 cm is then true immediately). Wait for a tick
        // inside plantCaughtLeadMax so plantRock may roll; if that tick
        // never comes, the deadline still releases. A hard latch here
        // after Recover stopped waiting for the same band sat Plant
        // until the body fell 93° with no second cycle.
        float deadline = Mathf.Max(0.05f, plantMinDuration) + plantLeadWaitSeconds;
        bool timedOut = plantLeadWaitSeconds <= 0.0001f || phaseAge >= deadline;
        if (PlantRolledFromBehind && StanceLeadCapture >= fallComLeadTrigger)
            return true;
        return timedOut;
    }

    // Plant is standing on two feet on purpose, waiting for the CoM to roll
    // over the stance foot. Read-only, for the Play overlay.
    public bool PlantWaitingForLead { get; private set; }

    // Read-only view of that same band for BalanceController. The
    // double-support push-off may only run on a trunk this gate calls
    // settled: pushing an unsettled Plant is the fold the brake exists for.
    public bool PlantGateReady => PlantFallGateReady();

    // Standing band BeginWalk / restore wait on: TiltSettled (12° /
    // 40 °/s) + signed CoM-to-stance ≤ plantComLeadMax. No column
    // slack — that froze a quiet 6° knee. Happy-path numbers stay.
    public bool StandingPlantReady => RecoverStandReady();

    // DualSupport hold / CaptureWalk BeginWalk / Tick resume: same pose
    // as StandingPlantReady without the 40 °/s omega (mixer start on a
    // quiet 2° trunk). Two manifold-grounded soles, tilt 12°, signed
    // CoM-to-stance ≤ plantComLeadMax. Clearance-0 FootDown is not this
    // band. A 20° heap still fails tilt. Quiet both-down still opens.
    public bool StandingPlantHoldReady => StandingPlantPoseReady();

    // Happy-path TiltSettled number (12°). Plant restore / Plant→Fall
    // read this; do not retune. Omega stays on TiltSettled itself.
    public float StandingPlantTiltMaxDeg => TiltSettledMaxDeg;

    // Live CaptureWalk DualSupport after that hold: TiltSettled 12° and
    // plantComLeadMax 0.10 already launched the walker (BeginWalk / the
    // 30 ms hold). Re-testing them on the 9° command aborted the peel
    // (torso −14° at comX 0.069). Cycle-0 PlantComReady is
    // RecoverComLostMeters 0.30; fallAbortDeg 30 is no longer that 9°.
    // RecoverPoseIsLost skips both on first plant when WalkLift is still
    // on the object (useFallCatchWalk 0, SwapCount 0) — p_auto then
    // slid with margin −0.385 at 1.00 s, c0 the whole first second,
    // pelvisDrop 0.87 / torso 90°.
    //
    // The 6.300 s event (c125adbd, every cadence): DualSupport still
    // both-down, trail still touching, then
    // BalanceController.UpdateState names Falling when |offset| >
    // fallCoMOffset 0.30 at walkTime 5 + 1.30 s. CaptureWalk's copy of
    // that same 0.30 was this method: signedStanceLead (com.x minus
    // the stance sole) > RecoverComLostMeters returned true, then
    // ReturnCaptureWalkToStandingPlant ended the walk (walkStance 8 /
    // walkTransfer 8 / walkFirst right → walkSwapCount 0). The 9°
    // onset bow already stores ~0.13 m; e^{ωT}≈2.4 at T=0.26 s puts
    // lead ~0.31 m — that orbit is the first catch, not a chair.
    // Caller is first DualSupport skate only. SinkCollapsed /
    // fallAbortDeg 30 still restore a heap. signedStanceLead stays
    // on the signature so the caller still measures the orbit.
    public bool StandingPlantHeap(float signedStanceLead)
    {
        if (walkRecoverAnyPose <= 0.5f)
            return false;
        if (SinkCollapsed())
            return true;
        if (fallAbortDeg > 0.001f && AbsBodyTilt() >= fallAbortDeg)
            return true;
        // signedStanceLead > RecoverComLostMeters 0.30 is the 6.300 s
        // dump (c125adbd). Keep measuring it; do not restore on it.
        _ = signedStanceLead > RecoverComLostMeters;
        return false;
    }

    // Tilt half of that band, without the column / lead tests. DualSupport
    // dirt-lift shortens the swing chord on purpose; using StandingPlantReady
    // there would abort a live peel.
    public bool StandingTiltSettled => TiltSettled();

    // CoM has left the STANCE plate along walk — capture point, not
    // position, and not mid-support of both soles. While both are down,
    // GetSupportWidth is the pair: pulling CoM onto the named stance
    // sits deeper inside that polygon, so the event never fires and
    // the swing stays DrivePelvis into the slab (burn_stag 9 mm).
    // StanceLeadCapture is StanceComLead + v/omega0; the toe is
    // footSize.x/2 from the foot centre StanceComLead measures from.
    // plantMinDuration / Idle 0.10 s are not this gate.
    public bool ComLeftSupport
    {
        get
        {
            float half = 0.13f;
            if (humanBody != null && humanBody.footSize.x > 0.05f)
                half = humanBody.footSize.x * 0.5f;
            return StanceLeadCapture >= half;
        }
    }

    // First split is the stance-ankle weight transfer. It must not wait
    // for ComLeftSupport (that needs the named stance ankle). The STEP
    // waits on PlantStandReady. Latch so a later dip inside the plate
    // does not drop standLeg and unwind the roll.
    private bool FirstStepMaySplit()
    {
        if (SwapCount != 0 || FallCatchCycleCount != 0) return true;
        if (firstStepSplitLatched) return true;
        firstStepSplitLatched = true;
        return true;
    }

    private void WriteWalkSplitLeg()
    {
        if (intent == null) return;
        if (plantStandSurrender || !FirstStepMaySplit())
            intent.standLeg = 0f;
        else
            intent.standLeg = currentStance;
    }

    // WalkLift / geo-lift: the first step fires once CoM has left the
    // stance plate. Split may already be on (weight transfer). Cached refs,
    // no Find.
    public bool PlantStandReady => ComLeftSupport;

    // Read-only view of the quantity the Plant→Fall gate itself measures
    // (signed CoM lead over the stance foot, metres). BalanceController needs
    // it so the Plant roll-over setpoint fades as the CoM approaches
    // plantComLeadMax instead of rolling past the gate's upper bound.
    public float StanceLead => StanceComLead();

    // Signed CoM lead over the current swing foot. Plus = CoM ahead of
    // that sole (the catch has not arrested). ApplyRecoverBrake reads it so
    // a calm trunk cannot mirror CoP forward while the mass is still past
    // the plant — but only as CoP, never as a trunk lean. Recover itself
    // no longer waits for this band; that wait was swap 0.
    public float SwingLead => SwingComLead();

    // The extrapolated form of that same lead — the capture point,
    // StanceComLead() + v/omega0 — which is what the Plant→Fall lead gate
    // actually judges (FallLeadReady reads this property, so there is one
    // copy of the formula). StanceLead above is position alone. The Plant
    // trunk lean that starts the roll-over (walkPlantLeanRoll) fades out on
    // this number rather than on a second criterion, so the phase cannot end
    // while the lean that ended it is still commanded.
    // Forward CoM velocity for both capture-point readers here (this property
    // and ActiveCatchFootLead). It comes from BalanceController
    // (walkComVelFilterTau) rather than from the raw estimator sample, because
    // a gate that decides WHEN the leg is released and a target that decides
    // WHERE it lands must be extrapolating one body. This driver runs at
    // order −50, ahead of the controller, so the value is the previous 5 ms
    // step of a filter whose own constant is a dozen — which is also why
    // there is no second copy of the filter on this side. Raw sample when
    // there is no controller at all.
    private float ComVelForwardX()
    {
        if (balance != null)
            return balance.WalkComVelocityX * WalkDir();
        return bodyState != null ? bodyState.comVelocity.x * WalkDir() : 0f;
    }

    public float StanceLeadCapture
    {
        get
        {
            float h = balance != null ? Mathf.Max(0.1f, balance.walkXCoMHeight) : 1f;
            float omega0 = Mathf.Sqrt(9.81f / h);
            return StanceComLead() + ComVelForwardX() / omega0;
        }
    }

    // Commanded stance in the air, the other foot down: stand on the
    // loaded one. Both down or both up: keep the identity (geometric
    // lead would swap after a missed catch).
    private void HoldLoadedStance()
    {
        if (bodyState == null || IsStanceFootGrounded())
            return;
        bool leftDown = FootDown(true);
        bool rightDown = FootDown(false);
        if (leftDown == rightDown)
            return;
        currentStance = leftDown ? -1f : 1f;
    }

    // Plant ladder rung 1. CoM has run past the commanded support while the
    // other foot is already planted further along the walk: standing on that
    // foot puts the lead back inside the band at once. Not a step — no
    // SwapCount, no FallCatchCycleCount, and the margin keeps a healthy
    // negative-lead Plant (CoM behind its own support) untouched.
    private bool TryReseatStanceToLoadedFoot()
    {
        if (bodyState == null || comCalculator == null)
            return false;
        if (!BothFeetGrounded())
            return false;
        float otherLead = OtherFootComLead();
        if (otherLead >= StanceComLead() - Mathf.Max(0f, plantReseatLeadMargin))
            return false;
        currentStance = -currentStance;
        return true;
    }

    // Signed lead of the current swing foot, same convention as StanceComLead.
    private float OtherFootComLead()
    {
        return SwingComLead();
    }

    // Reference for the sink guard. Only a settled two-foot pose that
    // passed the Plant→Fall gate counts as "this is how tall standing
    // is here". World CoM from the sensors after settle — never the
    // pelvis spawn column (~0.28 m too high → sinkAbortDrop on quiet
    // stance) and never Max of a walk-orbit peak (that held the bar
    // 0.25 m above live CoM and locked DualSupport as collapsed).
    private void CaptureStandHeight()
    {
        if (comCalculator == null || !BothFeetGrounded())
            return;
        if (comCalculator.TrackedBodyCount <= 0)
            return;
        if (!PlantSensorsReady())
            return;
        if (AbsBodyTilt() >= TiltSettledMaxDeg)
            return;
        if (!BothFeetPlanted())
            return;
        float comY = comCalculator.GetCenterOfMass().y;
        // Settled world sensors only. Never Max of a walk-orbit peak
        // (that held the bar ~0.25 m above live CoM → DualSupport
        // collapsed ~99 % of onset and GETUP BodyCollapsed forever).
        // Geometric pelvis column is RetargetStandComIfColumn only.
        RetargetStandComIfColumn(comY);
    }

    // Folding is not tipping: tilt and CoM offset both stay calm while the
    // body sinks (open defect 8). Inert until a stand has been measured, or
    // until walkRecoverAnyPose has the spawn CoM to compare against.
    // A requested crouch is the same height drop on purpose: only a heap
    // (tilt past the crouch lean) or a crash (CoM still falling fast)
    // is an abort then. Uncommanded drop is still a fold.

    // Read-only for BalanceController. The catch exemption from the fall
    // automaton must not outlive the height a catch needs: a body already
    // on the slab has no stance left to reach from.
    public bool BodyCollapsed => SinkCollapsed();

    private bool SinkCollapsed()
    {
        if (sinkAbortDrop <= 0.001f || comCalculator == null)
            return false;
        // GETUP-1 / Fallen: the heap is below any standing bar by
        // definition. Dog already clears WalkCollapsed here; human
        // BodyCollapsed must not freeze TickGetUp for 32 s.
        if (balance == null)
            EnsureRefs();
        if (balance != null
            && (balance.locomotionState
                    == BalanceController.LocomotionState.GettingUp
                || balance.locomotionState
                    == BalanceController.LocomotionState.Fallen))
            return false;
        float comY = comCalculator.GetCenterOfMass().y;
        float reference;
        if (standComHeightValid)
            reference = standComHeight;
        else if (spawnStandHeightValid && walkRecoverAnyPose > 0.5f)
            reference = spawnStandHeight;
        else
            return false;

        // Quiet stance is not a 0.28 m fold. The pelvis column / root
        // fallback sits ~0.28 m above live CoM, so drop > sinkAbortDrop
        // 0.25 while pelvisDrop is 0.0017 and StandingRestore kills the
        // capture walker. A real heap drops the pelvis ~0.8 m (defect 8).
        // Geometric column is only this pelvis test, never the CoM bar.
        if (PelvisNearSpawnStand()
            && PlantSensorsReady()
            && AbsBodyTilt() < TiltSettledMaxDeg
            && BothFeetPlanted())
        {
            if (reference - comY >= sinkAbortDrop)
                RetargetStandComIfColumn(comY);
            return false;
        }

        if (reference - comY < sinkAbortDrop)
            return false;
        if (intent != null && intent.crouch > 0.5f)
        {
            float tilt = AbsBodyTilt();
            float vy = bodyState != null ? bodyState.comVelocity.y : 0f;
            return tilt >= sinkCollapseTiltDeg || vy <= sinkCollapseVy;
        }
        return true;
    }

    private void RetargetStandComIfColumn(float comY)
    {
        spawnStandHeight = comY;
        spawnStandHeightValid = true;
        standComHeight = comY;
        standComHeightValid = true;
    }

    private bool PelvisNearSpawnStand()
    {
        if (pelvisTf == null)
            return false;
        float spawnY = spawnPelvisYValid ? spawnPelvisY : GeometricPelvisStandY();
        return pelvisTf.position.y > spawnY - Mathf.Max(0.05f, pelvisStandSlack);
    }

    // Root spawn y, same column as GameProcess. Not a CoM bar.
    private float GeometricPelvisStandY()
    {
        if (humanBody == null)
            return -0.82f;
        float column = (humanBody.pelvisSize.y + humanBody.torsoSize.y) * 0.5f
            + humanBody.thighSize.y + humanBody.shinSize.y + humanBody.footSize.y;
        return -2f + column;
    }

    // Spawn CoM, once, after the body settles. Called from Start, from
    // FixedUpdate while idle, and from BeginWalk before walkActive.
    // World Y from CenterOfMassCalculator (sensors), not the pelvis
    // column and not a startup bounce. TrackedBodyCount > 0 means
    // BuildHuman filled the list — otherwise GetCenterOfMass returns
    // the root. Find is cached.
    private void TryCaptureSpawnStandHeight()
    {
        if (spawnStandHeightValid || walkRecoverAnyPose <= 0.5f)
            return;
        if (walkActive)
            return;
        EnsureRefs();
        if (pelvisTf == null || comCalculator == null)
            return;
        if (comCalculator.TrackedBodyCount <= 0)
            return;
        // sensorTick 0: FootDown still treats clearance 0 as down, so a
        // BeginWalk in Update would stamp the spawn bar from zeros.
        // Idle FixedUpdate is after BodyStateEstimator (−100).
        if (bodyState == null || bodyState.sensorTick <= 0)
            return;
        if (!BothFeetGrounded() || !BothFeetPlanted())
            return;
        if (!PlantSensorsReady())
            return;
        // Settled trunk only. A mixer spike or spawn drop must not
        // write the standing bar.
        if (AbsBodyTilt() >= TiltSettledMaxDeg)
            return;
        // World-space settle: wait out the spawn bounce. Capturing mid-
        // slam arms a column ~0.25 m above quiet CoM → DualSupport
        // collapsed ~99 % of onset (reason 2) and GETUP never clears
        // GetUpComAtStandHeight. Same band as dog settle (0.12 / 0.20).
        if (Mathf.Abs(bodyState.comVelocity.y) > 0.12f)
            return;
        if (Mathf.Abs(bodyState.comVelocity.x) > 0.20f)
            return;
        spawnStandHeight = comCalculator.GetCenterOfMass().y;
        spawnStandHeightValid = true;
        spawnPelvisY = pelvisTf.position.y;
        spawnPelvisYValid = true;
        RetargetStandComIfColumn(spawnStandHeight);
    }

    // GETUP-1: drop any walk-orbit peak so GetUpComAtStandHeight can
    // pass once the trunk is upright over the feet. spawnPelvisY stays
    // for PelvisNearSpawnStand. NotifyStoodUp / CaptureStandHeight
    // remeasure from the risen pose.
    public void ClearStandingComBarForGetUp()
    {
        standComHeightValid = false;
        standComHeight = 0f;
        spawnStandHeightValid = false;
        spawnStandHeight = 0f;
    }

    private bool HeelPlantOn()
    {
        return fallSwingHeelPlant > 0.5f;
    }

    private bool IsSwingHeelLoaded()
    {
        if (bodyState == null) return true;
        return currentStance > 0.5f
            ? bodyState.leftFootHeelLoaded
            : bodyState.rightFootHeelLoaded;
    }

    // Catch/Recover landing. Toe scrape is not a plant: the heel must be
    // the loaded end (airHeel already points the toe up). 0 = any contact.
    private bool IsSwingFootPlanted()
    {
        if (!HeelPlantOn())
            return IsSwingFootGrounded();
        return IsSwingHeelLoaded();
    }

    // Cycle-0 initiation never takes an airborne heel-first plant: geo
    // lift pitches the still-stacked sole onto the toe, so heel-loaded
    // stays false and Catch/Recover have no exit (walkSwap 0). Later
    // cycles keep heel-first — a toe scrape after a real flight is not
    // a catch.
    // Cycle 0 also plants when the sole is inside the combined contactOffset
    // of the slab (comment 20): a reaching 10°/10° pose still sits inside
    // that band before overlap, and waiting for GetContacts-only is
    // sinkAbort / walkSwap=0. Later cycles keep heel-first.
    private bool IsFirstStepPlanted()
    {
        if (FallCatchCycleCount == 0)
            return IsSwingFootGrounded() || SwingFootNearContact();
        return IsSwingFootPlanted();
    }

    private static float ContactOffsetBand()
    {
        return Physics2D.defaultContactOffset * 2f;
    }

    private bool SwingFootNearContact()
    {
        return SwingFootClearance() <= ContactOffsetBand();
    }

    // Ground clearance of the foot that is currently the swing.
    private float SwingFootClearance()
    {
        if (bodyState == null) return 0f;
        return currentStance > 0.5f
            ? bodyState.leftFootGroundClearance
            : bodyState.rightFootGroundClearance;
    }

    // How far ahead of the CoM the catch plant must land, in metres
    // (catchLeadCapture above). Public because BalanceController commands the
    // step that this very method judges: the two must read one number, not a
    // field and a live value that drift apart.
    public float ActiveCatchFootLead()
    {
        float tuned = Mathf.Max(0f, catchFootLead);
        if (catchLeadCapture <= 0.5f)
            return tuned;
        float h = balance != null ? Mathf.Max(0.1f, balance.walkXCoMHeight) : 1f;
        float omega0 = Mathf.Sqrt(9.81f / h);
        float capture = ComVelForwardX() / omega0 + Mathf.Max(0f, catchLeadCaptureMargin);
        // Min() on the floor keeps the clamp well ordered if catchFootLead is
        // ever set below catchFootLeadMin from the Inspector or CLI.
        float floor = Mathf.Min(tuned, Mathf.Max(0f, catchFootLeadMin));
        return Mathf.Clamp(capture, floor, tuned);
    }

    // True when the swing foot centre is ahead of CoM along the walk
    // direction by ActiveCatchFootLead. That is the catch plant: a foot under
    // the hip cannot produce a restoring moment about the CoM.
    private bool IsSwingFootAheadOfCom()
    {
        if (bodyState == null || comCalculator == null) return false;
        Vector2 com = comCalculator.GetCenterOfMass();
        Vector2 swing = currentStance > 0.5f
            ? bodyState.leftFootPosition
            : bodyState.rightFootPosition;
        return (swing.x - com.x) * WalkDir() >= ActiveCatchFootLead();
    }

    // The first flight is what makes a capture plant possible. Stacked
    // dirt never grows AheadOfCom (initiation already walked CoM past both
    // soles). After that flight the plant that becomes stance must be
    // AheadOfCom, or PickStanceFromFeet hands a sole the mass has passed
    // and UpdateState names Falling.
    private bool FirstStepHasFlown()
    {
        return swingWasAirborne || reachAirEnterTime >= 0f;
    }

    private bool CatchFootIsReady()
    {
        if (FallCatchCycleCount == 0 && !FirstStepHasFlown())
            return IsSwingFootAheadOfStance();
        return IsSwingFootAheadOfCom();
    }

    // Cycle-0 Catch plant: swing ahead of the STANCE foot, not of CoM.
    // Initiation lean walks CoM past both stacked soles (PlantComReady
    // allows 0.30 m), so IsSwingFootAheadOfCom then asks for stance-span
    // plus that lead — a slide µ=1.2 cannot make. Span ≥ FootLeadDeadzone
    // is what EnterFallCatchPlant needs to hand the next stance to the
    // front foot instead of flipping two stacked soles.
    private bool IsSwingFootAheadOfStance()
    {
        if (bodyState == null) return false;
        Vector2 swing = currentStance > 0.5f
            ? bodyState.leftFootPosition
            : bodyState.rightFootPosition;
        Vector2 stance = currentStance > 0.5f
            ? bodyState.rightFootPosition
            : bodyState.leftFootPosition;
        // FootLeadDeadzone is the span PickStanceFromFeet needs to hand
        // the next support to the front foot. catchFootLeadMin (3.5 cm)
        // is a CoM-ahead floor; stacked initiation never grows it.
        return (swing.x - stance.x) * WalkDir() >= FootLeadDeadzone;
    }

    // Same band over the commanded stance foot: Plant asks it to decide
    // whether this plant was caught (PlantRolledFromBehind) or whether the
    // mass is still running away from the support. Recover swap does not
    // read this — AheadOfStance can be true with CoM still past the
    // plant, and waiting for the band there was walkSwap 0.
    private bool IsComCaughtByStanceFoot()
    {
        return StanceComLead() <= CaughtLeadBand();
    }

    private float CaughtLeadBand()
    {
        return Mathf.Max(0f, plantCaughtLeadMax);
    }

    // The mass is running past the support. Judged on the capture point as
    // well as the position: the ankle can only hold a CoM whose extrapolated
    // landing point stays inside the sole, and that sole's ceiling is
    // m·g·arm about the toe (balance-actuators.mdc) — the same ceiling a
    // two-foot stand has on a shorter arm. Past this band no stance torque
    // arrests the fall; only a plant ahead of the capture point does.
    // Cycle 0 is excluded: its lean is commanded, and PlantComReady already
    // gives initiation the 0.30 m band.
    private bool ComRunningPastStance()
    {
        if (FallCatchCycleCount == 0)
            return false;
        float bound = Mathf.Max(0.05f, plantComLeadMax);
        return StanceComLead() > bound || StanceLeadCapture > bound;
    }

    // ...and there is a leg free to do it with: the commanded support is
    // loaded, so the other one can be thrown forward, and the body is not
    // already a heap. A stand is the right answer to a collapse; it is the
    // wrong answer to a body that is still tall and moving.
    private bool KeepSteppingOnRunaway()
    {
        return walkRunawayKeepStepping > 0.5f
            && !plantStandSurrender
            && ComRunningPastStance()
            && IsStanceFootGrounded()
            && !SinkCollapsed();
    }

    // +1 = stance on the right (swing left), −1 the other way. Geometric
    // rule for a landing AND for cold BeginWalk: the leading foot carries
    // the weight, so the trailing leg is the one that steps forward.
    // Standing on the trailer hands swing to the already-leading foot and
    // the body walks itself backward (EnterFallCatchPlant).
    private float PickStanceFromFeet(float fallback)
    {
        if (bodyState == null) return fallback;
        // Only a loaded foot can be stance. One foot down decides on its
        // own — otherwise the geometry can hand the role to a leg that is
        // in the air and the next phase has nothing under it.
        bool leftDown = FootDown(true);
        bool rightDown = FootDown(false);
        if (leftDown && !rightDown) return -1f;
        if (rightDown && !leftDown) return 1f;
        // Neither foot is down (cold BeginWalk in the air, or mid-fall). The
        // geometric lead can hand stance to the trailing foot and the brake
        // then holds a leg that will carry nothing. The lower foot lands
        // first, so that is the support the next Plant can actually use.
        // Every landing call site has a foot down, so this is BeginCycle only.
        if (!leftDown && !rightDown && plantAirLowestFoot > 0.5f)
        {
            float leftClear = bodyState.leftFootGroundClearance;
            float rightClear = bodyState.rightFootGroundClearance;
            if (Mathf.Abs(leftClear - rightClear) > 0.01f)
                return leftClear < rightClear ? -1f : 1f;
        }
        float lead = (bodyState.rightFootPosition.x - bodyState.leftFootPosition.x) * WalkDir();
        if (Mathf.Abs(lead) < FootLeadDeadzone) return fallback;
        return lead > 0f ? 1f : -1f;
    }

    // Where "forward" is. No lateral axis in the sagittal model: +1 = +X.
    private float WalkDir()
    {
        return intent != null && Mathf.Abs(intent.moveX) > 0.01f
            ? Mathf.Sign(intent.moveX)
            : 1f;
    }

    // How far CoM has gone ahead of the stance foot along the walk direction.
    private float StanceComLead()
    {
        if (bodyState == null || comCalculator == null) return 0f;
        Vector2 com = comCalculator.GetCenterOfMass();
        Vector2 stanceFoot = currentStance > 0.5f
            ? bodyState.rightFootPosition
            : bodyState.leftFootPosition;
        return (com.x - stanceFoot.x) * WalkDir();
    }

    private float SwingComLead()
    {
        if (bodyState == null || comCalculator == null) return 0f;
        Vector2 com = comCalculator.GetCenterOfMass();
        Vector2 swingFoot = currentStance > 0.5f
            ? bodyState.leftFootPosition
            : bodyState.rightFootPosition;
        return (com.x - swingFoot.x) * WalkDir();
    }

    // Swing sole sits at least FootLeadDeadzone ahead of the stance sole
    // along walk. Stacked feet (the 0.2 m shuffle) return false.
    private bool SwingFootIsAhead()
    {
        if (bodyState == null) return false;
        float stanceX = currentStance > 0.5f
            ? bodyState.rightFootPosition.x
            : bodyState.leftFootPosition.x;
        float swingX = currentStance > 0.5f
            ? bodyState.leftFootPosition.x
            : bodyState.rightFootPosition.x;
        return (swingX - stanceX) * WalkDir() >= FootLeadDeadzone;
    }

    private bool BothFeetGrounded()
    {
        return bodyState != null && FootDown(true) && FootDown(false);
    }

    // Last rung of the recovery ladder: at least one foot on the dirt, so
    // the quiet two-foot controller has something to stand on. Airborne
    // stay on the landing brake until a foot arrives.
    private bool AnyFootGrounded()
    {
        if (walkRecoverAnyPose <= 0.5f)
            return BothFeetGrounded();
        return bodyState != null && (FootDown(true) || FootDown(false));
    }

    private float AbsComOffset()
    {
        return comCalculator != null ? Mathf.Abs(comCalculator.GetCoMOffsetX()) : float.PositiveInfinity;
    }

    // WalkLift clock swap: the new sole must be a catch plate (ahead
    // of CoM and of the old stance). Mid-pair offset ≤ 0.05 / margin
    // 0.02 passed the 3 cm viewpoint lunge and dumped (burn_walk
    // swap 2). Do not use TiltSettled (12°) — plant's operating tilt
    // is already past that.
    private bool WalkLiftSwapSettled()
    {
        if (!BothFeetGrounded()) return false;
        if (!swingWasAirborne) return false;
        if (!SwingFootIsAhead()) return false;
        if (bodyState == null || comCalculator == null) return false;
        Vector2 com = comCalculator.GetCenterOfMass();
        Vector2 swing = currentStance > 0.5f
            ? bodyState.leftFootPosition
            : bodyState.rightFootPosition;
        if ((swing.x - com.x) * WalkDir() < FootLeadDeadzone)
            return false;
        return AbsBodyTilt() <= 20f;
    }

    // Diagnostic: CoM vs the landing foot. walk2_walk Recover floor is
    // 0.60 m — a step, not recoverComOffsetMax 0.16. Kept so the quantity
    // stays named; Plant no longer gates on it.
    private float AbsCatchFootComOffset()
    {
        if (bodyState == null || comCalculator == null)
            return float.PositiveInfinity;
        Vector2 com = comCalculator.GetCenterOfMass();
        float catchX = currentStance > 0.5f
            ? bodyState.leftFootPosition.x
            : bodyState.rightFootPosition.x;
        return Mathf.Abs(com.x - catchX);
    }

    private bool IsTransferReady()
    {
        if (bodyState == null || comCalculator == null)
            return false;
        if (!BothFeetGrounded())
            return false;
        if (walkTiltRecoverDeg > 0.001f && AbsBodyTilt() > walkTiltRecoverDeg)
            return false;
        // Mid-offset only: the "CoM over the next foot" threshold on a cold
        // first→left did not fire (walk_swap_soft2 swap=0). Soft entry
        // after EnterStance — dualSupportSwingScale in BalanceController.
        return AbsComOffset() <= transferComOffsetMax;
    }

    private bool IsTransferFallbackReady()
    {
        if (bodyState == null || comCalculator == null)
            return false;
        if (!BothFeetGrounded())
            return false;
        if (walkTiltRecoverDeg > 0.001f && AbsBodyTilt() > walkTiltRecoverDeg)
            return false;
        return AbsComOffset() <= transferFallbackComMax;
    }

    private float AbsBodyTilt()
    {
        return vestibular != null ? Mathf.Abs(vestibular.GetBodyTilt()) : 0f;
    }

    // Recover / Catch / post-catch Plant: absolute 12° / 40 °/s band.
    // Plant→Fall uses this; abort is the Fall emergency, not this gate.
    private bool TiltSettled()
    {
        // Absolute band, not a multiple of the abort angle. The stand runs
        // fallAbortDeg = 30, so abort × 1.5 was 45° — a gate that let Plant
        // start the next fall from a 15° tip with 25 deg/s still on the
        // trunk (walk2_blend2, 6.605 s), and that fall never came back.
        return AbsBodyTilt() < TiltSettledMaxDeg
            && AbsBodyOmega() < RecoverOmegaSettledDeg;
    }

    private float AbsBodyOmega()
    {
        return vestibular != null ? Mathf.Abs(vestibular.GetBodyAngularVelocity()) : 0f;
    }
}
