using UnityEngine;

// Intent layer: one-leg stance on a timer, transfer — by condition (both feet,
// CoM at center). Do not touch BalanceController.
[DefaultExecutionOrder(-50)]
public partial class StepPhaseDriver : MonoBehaviour
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
}
