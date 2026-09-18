using UnityEngine;

// CoM-based balancer.
// Partial: the WALK-3 capture-point walker lives in
// BalanceControllerCaptureWalk.cs. It is a second walker beside the legacy
// fall-catch code in this file, gated by useCaptureWalker, and it shares the
// joint caches and the SPD helpers below so no hinge ever gets two owners.
[DefaultExecutionOrder(0)]
public partial class BalanceController : MonoBehaviour
{
    public enum BalanceState
    {
        Balancing,
        Recovery,
        Falling
    }

    // GETUP-1. The CoM-offset automaton above stays for the stand CSV
    // (Balancing / Recovery / Falling). These five are the body on the
    // ground: quiet, walking, still going down, heap, and the walk-key
    // stand-up. Do not reuse BalanceState=1 (Recovery) as "getting up" —
    // that is the freeze at hip +45° from world-upright on a −70° trunk.
    public enum LocomotionState
    {
        Standing = 0,
        Walking = 1,
        Falling = 2,
        Fallen = 3,
        GettingUp = 4
    }

    [Header("System state")]
    public BalanceState currentState = BalanceState.Balancing;
    [Tooltip("Standing / Walking / Falling / Fallen / GettingUp. Not BalanceState.")]
    public LocomotionState locomotionState = LocomotionState.Standing;

    [Header("Get up from a fall")]
    // GETUP-1 pose order (fixed): lying → sit up → tuck knees (≥70°)
    // with heels under the pelvis / CoM → arm push → stand. A path that
    // starts with knee tuck skips the sit and is wrong (D: knee span 35°
    // while hip 48° on a −70° plank). Walk key on a Fallen heap drives
    // GettingUp; CaptureWalk stays off until NotifyStoodUp.
    [Tooltip("|torso tilt| (deg) that counts as a heap, not a step. Quiet stance is ~1°.")]
    public float getUpFallenAbsTilt = 45f;
    [Tooltip("Success: torso tilt must rise above this (deg). GETUP-1: −20 from a −70° sprawl.")]
    public float getUpStandTiltDeg = -20f;
    [Tooltip("Hip flexion (deg, minus) while tucking after the sit. Stop is −90.")]
    public float getUpHipFlex = 70f;
    [Tooltip("Knee flexion (deg, plus) while tucking. Must reach ≥70. Stop is +120.")]
    public float getUpKneeFlex = 90f;
    [Tooltip("Seconds a sole must stay planted before the tuck phase may latch (after sit).")]
    public float getUpTuckMinSeconds = 0.15f;
    [Tooltip("|torso tilt| (deg) at or below which the sit-up is done and the knee tuck may start.")]
    public float getUpSquatAbsTilt = 35f;
    [Tooltip("Seconds the knee curl may run before the rise starts (the tuck budget). The rise also waits for the knee to fold onto a sole that is back down.")]
    public float getUpTuckMaxSeconds = 4f;
    [Tooltip("Seconds to blend a planted recovery toward quiet stance after the tuck.")]
    public float getUpRiseSeconds = 2.5f;
    // Arms push the slab while GettingUp (CURRENT: elbow travel > 20°,
    // non-zero command). crouchHandSupportElbow −10 is almost straight —
    // burn_walk kept shoulder ~−5 / elbow ~+2 for 33 s.
    [Tooltip("Shoulder forward-down (deg, minus) while GettingUp on a prone heap.")]
    public float getUpArmShoulder = 75f;
    [Tooltip("Elbow flexion (deg, applied minus) while planting / sitting / tucking. Rise extends toward crouchHandSupportElbow.")]
    public float getUpArmElbowFlex = 85f;
    [Tooltip("Get-up attempts this body (walk key on a heap).")]
    public int getUpAttempts;
    [Tooltip("True once tilt / CoM height / feet have passed GETUP-1.")]
    public bool getUpSuccess;
    [Tooltip("Seconds from heap to stand. −1 if not yet.")]
    public float getUpSeconds = -1f;
    [Tooltip("Sim time the heap was first seen. −1 if never.")]
    public float getUpFallTime = -1f;
    // Pose the body has reached on the current attempt (video gate).
    // 0 none, 1 lying, 2 sitting, 3 knees tucked, 4 standing.
    [Tooltip("Highest GETUP pose reached this attempt (lying/sitting/tucked/standing).")]
    public int getUpPose;
    [Tooltip("Highest GETUP pose reached this run (latched).")]
    public int getUpPoseMax;

    [Header("State-machine thresholds")]
    public float recoveryCoMOffset = 0.15f;
    public float fallCoMOffset = 0.30f;
    // A flight plus its plant is well under a second. Anything that holds the
    // walk exemption longer is not catching a step, it is a heap that keeps
    // commanding pelvis-to-vertical at saturation.
    [Tooltip("Seconds the fall-catch walk may keep the automaton in Recovery while |CoM offset| is past fallCoMOffset. 0 = unlimited (former behaviour).")]
    public float fallCatchFallGraceSeconds = 1.5f;
    // Seconds the exemption has been running. Cleared only when the body is
    // back inside fallCoMOffset, so a spent exemption cannot re-arm itself.
    private float fallCatchIgnoreFallAge;

    [Header("Balance gains")]
    // Inputs are normalized: 10 cm of offset and 30 °/s of tilt each give unit.
    // Mixing metres with deg/s used to let torso rate swamp the signal.
    public float comOffsetReference = 0.10f;
    public float tiltSpeedReference = 30f;
    public float comVelocityReference = 0.30f;
    public float comProportionalGain = 1.2f;
    public float comDerivativeGain = 0.35f;
    public float maxBalanceSignal = 1.0f;

    [Header("Target angles (degrees)")]
    // For the hip this is no longer a joint angle but a world pelvis-tilt target:
    // negative is forward (clockwise; the human faces right).
    // The knee still holds jointAngle. The −3/6 pair is the working stance:
    // a slight forward pelvis tilt, shin under the pelvis at knee +6°.
    public float hipBaseAngle = -3f;
    public float kneeBaseAngle = 6f;
    public float kneeRecoveryFlex = 12f;
    public float hipBalanceGain = 8f;

    [Header("Crouch")]
    // intent.crouch is a 0-or-1 target, not a smoothed value.
    // Otherwise the stand's instant crouch=1 would outrun a held key.
    public float crouchRatePerSecond = 1.5f;
    public float crouchReleaseRatePerSecond = 0.9f;
    // Knee flexion is positive: range 0…120, stance +6°.
    // Reshot after the knee flip: 40/32 drops 6.8 cm and holds the lumbar
    // at 17.3° of 20. Past that there is a hole (50/40 falls), though 70/55
    // stands again and drops 20.4 cm. Until the pelvis is controlled, do not
    // go past 40/32: stand vs fall there is not about depth.
    public float crouchKneeFlex = 97f;
    // No longer part of the hip target: we do not hold the pelvis–thigh joint.
    // Field kept so the stand and Inspector keep the name.
    public float crouchHipFlex = 32f;
    // Positive is the forward pelvis-tilt target at crouch=1.
    // Forward = clockwise = minus on the world target, same as crouchTorsoLean.
    // Depth comes from the knee; the pelvis only sets how far to lean.
    public float crouchPelvisTilt = 50f;
    // Positive is extra forward chest lean at crouch=1.
    // The human faces right, so forward = clockwise = minus on the target.
    public float crouchTorsoLean = 24f;
    // Level the pose actually uses. For the stand and Inspector.
    public float crouchLevel;
    // Arms forward-down: a hand on the ground widens support (BodyState/CoM).
    [Tooltip("Shoulder forward-down in crouch, deg (minus on both arms).")]
    public float crouchArmShoulder = 18f;
    [Tooltip("Elbow target: almost straight, hand toward the ground (not −130 — that flexes up).")]
    public float crouchArmElbow = -6f;
    [Tooltip("Wrist target in crouch, deg.")]
    public float crouchArmWrist = -8f;
    [Tooltip("Min crouchLevel for the hands to enter support.")]
    public float crouchHandSupportMin = 0.55f;
    [Tooltip("Slop to y=−2.0: hand counts as grounded in crouch, m.")]
    public float crouchHandGroundSlop = 0.24f;
    [Tooltip("Crouch level where hand-support pose starts blending in.")]
    public float crouchHandPoseStart = 0.55f;
    [Tooltip("Deep crouch shoulder target (forward/down), degrees.")]
    public float crouchHandSupportShoulder = 58f;
    [Tooltip("Deep crouch elbow target, degrees.")]
    public float crouchHandSupportElbow = -10f;
    [Tooltip("Deep crouch wrist target, degrees.")]
    public float crouchHandSupportWrist = -15f;
    [Tooltip("Static shoulder split in deep crouch support pose, degrees.")]
    public float crouchHandSupportSpread = 12f;
    [Tooltip("Additional split from balance signal in deep crouch.")]
    public float crouchHandBalanceSpread = 0f;

    [Header("Trunk lean")]
    // intent.lean: +1 forward (− on the torso/pelvis world target), −1 back.
    // Knees stay put — only chest and pelvis; the ankle holds CoM.
    [Tooltip("Slew rate of leanLevel toward intent.lean, 1/s.")]
    public float leanRatePerSecond = 1.5f;
    [Tooltip("Extra chest tilt at lean=+1, deg (forward = minus on the target).")]
    public float leanTorsoAngle = 12f;
    [Tooltip("Extra pelvis tilt at lean=+1, deg (forward = minus on the target).")]
    public float leanPelvisTilt = 8f;
    // Current level −1…+1 for the pose and Inspector.
    public float leanLevel;

    [Header("One-leg stance")]
    // Swing flex: knee plus first (shorten the leg, lift the foot).
    // Hip minus only after lift-off and then by latch — the target must
    // not be weaker than the angle at lift-off (~37°), or the thigh extends
    // and the foot stomps. 40° holds the leg in the air.
    public float swingHipFlex = 40f;
    public float swingKneeFlex = 55f;
    // March in place (intent.march, not walk / WalkLift). Raised-leg pose:
    // hip flexion minus (thigh toward horizontal), knee plus (shank down).
    // Quiet one-leg keep swingHipFlex 40 / swingKneeFlex 55. Hip joint
    // stop is −90, so 90 sits on the limit; that is the requested picture.
    [Header("March in place (not walk)")]
    [Tooltip("Air hip flexion (deg, minus) while marching. One-leg hold ignores this.")]
    public float marchHipFlex = 90f;
    [Tooltip("Air knee angle (deg, plus) while marching. Joint target, not added to kneeBase.")]
    public float marchKneeFlex = 90f;
    // Pelvis tilt as CoM compensation is bistable (same well that dumped
    // CaptureWalk): +1.0 hold / −0.9 mid / +0.885 soft / −0.75 ph. Target
    // stays world vertical. Extra stance-hip P is 0: it walked +X
    // (march_air CSV). CoM is a delayed ankle cap, then toe/heel after
    // pose-up. Retired tilt fields stay 0; do not retune them.
    // Wall (march_pin): 90° pose holds; the ankle CoM loop is the same
    // two wells (air +0.57 toe / pin −0.62 heel). Do not add another
    // cap, delay, or tilt. Card ONELEG-2.
    [Tooltip("Retired. March pelvis stays vertical. Keep 0.")]
    public float marchPrepareTilt = 0f;
    [Tooltip("Retired. March pelvis stays vertical. Keep 0.")]
    public float marchLiftTilt = 0f;
    [Tooltip("Slew leftover marchPelvisBias to 0 (deg/s).")]
    public float marchTiltRate = 30f;
    [Tooltip("Retired. March pelvis stays vertical. Keep 0.")]
    public float marchPelvisHold = 0f;
    [Tooltip("Retired. Do not chase CoM with pelvis tilt. Keep 0.")]
    public float marchComPelvisGain = 0f;
    [Tooltip("Retired clamp (deg). Unused while marchComPelvisGain is 0.")]
    public float marchPelvisComClamp = 8f;
    [Tooltip("March single-support ankle: fraction of comOffsetReference. 1 = quiet-stance ref.")]
    [Range(0.2f, 1f)]
    public float marchAnkleRefScale = 1f;
    [Tooltip("Extra pelvis P after pose-up air. 0: planted extensor walks +X (march_air CSV).")]
    public float marchStancePelvisP = 0f;
    [Tooltip("March |ankle| cap after a short true-air streak. Soft so P=14 does not vault.")]
    [Range(0.05f, 1f)]
    public float marchAnkleCap = 0.45f;
    [Tooltip("CoM-ahead of the stance foot (m) past which plantarflex is forbidden. Pose-up only.")]
    public float marchAnkleToeGate = 0.08f;
    [Tooltip("CoM-behind of the stance foot (m) past which dorsiflex is forbidden. Pose-up only.")]
    public float marchAnkleHeelGate = 0.05f;
    [Tooltip("Seconds of true swing air before the ankle cap. Skips a scrape; 0.25 vaulted (march_air).")]
    public float marchComDelay = 0.08f;
    [Tooltip("Swing hip (deg, minus) that counts as pose-up for the toe/heel gates.")]
    public float marchPoseHip = 40f;
    [Tooltip("Swing knee (deg, plus) that counts as pose-up for the toe/heel gates.")]
    public float marchPoseKnee = 50f;
    // Grounded swing flex is a fraction of swingKneeFlex: full flex folds the
    // chain, zero never lifts the foot. After latch — full swingKneeFlex.
    public float swingKneeGroundedFraction = 0.25f;
    // Extra swing signal toward the flexor (decreases jointAngle).
    // While the foot is down, both hips stay on the same pelvis PD: extra
    // flexor unloads swing GRF so the knee can lift the foot instead of
    // folding a closed chain. Do not take the swing off the pelvis while grounded.
    public float swingHipUnloadBias = 0.32f;
    // Right leg ahead (+X), left behind: on left support the swing is forward
    // and 50% grounded knee does not lift it — it needs its own gains.
    public float forwardSwingHipUnloadScale = 1.3f;
    public float forwardSwingKneeGroundScale = 1.4f;
    // On right support the swing is the left leg behind (−X). Pelvis tilt
    // does not work there; unload with the same ideas as the forward swing.
    public float backSwingHipUnloadScale = 1.3f;
    public float backSwingKneeGroundScale = 1.4f;
    // Fraction of standLegPelvisTilt backward on right support. 1.0 = −10°
    // plants both feet (swingFoot 1.0); enable as a fraction via CLI.
    public float backPelvisTiltFraction = 0f;
    // Plantarflex on the grounded forward swing. Default 0: +12° drops
    // left support; enable only via CLI after a separate check.
    public float swingAnkleGroundedToeOff = 0f;
    // Pelvis tilt on left support: unloads the forward swing (right leg).
    // Do not apply on right support — it blocks back-swing lift-off.
    public float standLegPelvisTilt = 10f;
    // Same tilt, same sign, on RIGHT support — but only on the fall-catch
    // walk. The "do not apply on right support" note above is a property of
    // the cold one-leg probe, where right support means the left leg swings
    // BACKWARD (backSwingHipUnloadScale, backPelvisTiltFraction: the mirror
    // there is the negative sign). On the walk path the swing always reaches
    // FORWARD, toward the capture point, whichever leg it is — the placement
    // solver, the unload scale (1.3 on both) and the grounded-knee scale
    // (1.4 on both) are already side-independent there. Leaving the tilt out
    // of right support therefore hands the assist to every second step only,
    // which is an asymmetric gait by construction. Fall-catch walk with a
    // walk direction only, so cold one-leg (oneg_l / oneg_r), quiet stance,
    // the pushes, crouch and legacy useFallCatchWalk = 0 walk stay bitwise.
    [Tooltip("Fraction of standLegPelvisTilt applied on RIGHT support during fall-catch walk, same sign as left support. 0 = former left-only tilt.")]
    public float walkStandLegPelvisTiltMirror = 1f;
    // During walkActive while split, soften swing unload/knee/tilt
    // (not only when both are grounded: the lift-off frame would restore soft=1).
    // Arm counter-reach is off on walkActive (else elbowLimit↑ and fold).
    [Tooltip("Fraction of swing unload/knee/tilt on walk + split.")]
    public float dualSupportSwingScale = 0.32f;
    // Cap standLegLevel on all of walkActive: if you only cut when both
    // are grounded, a brief lift-off sends level→1 and folds after plant.
    // Cold oneg has walkActive=false — no cap.
    [Tooltip("Max standLegLevel while walkActive. 0 = no cap.")]
    public float dualSupportStandCap = 0.40f;
    // The cap above and the dualSoft softener were both written as the FLOOR
    // of a ramp that lifts as weight leaves the swing leg. On the fall-catch
    // gait that ramp (liftBlend / liftGateBlend / weightBlend) is computed
    // only when !fallCatchWalk, so the floor is all that ever applies and the
    // swing leg spends the whole cycle on 0.40 × 0.32 ≈ 13% of its authority.
    // This restores the ramp from measured swing-foot clearance instead of the
    // legacy Stance clock. 0 = the former frozen floor.
    [Tooltip("Fall-catch: swing-foot air release lifts the cap/unload ramp. 0 = frozen floor.")]
    public float walkAirReleaseAuthority = 1f;
    // The same ramp, measured from the quantity it stands for. The release
    // above is the swing foot's own CLEARANCE, and clearance is a bootstrap:
    // the leg needs authority to leave the dirt and only earns authority once
    // it already has. So through double support — the longest part of this
    // cycle, and the only part where the lift can be prepared — the ramp reads
    // zero and the swing leg sits back on the 0.40 x 0.32 floor, however
    // unloaded it really is.
    // Which foot carries the weight in double support is not a clock and not a
    // guess, it is statics: two supports with one CoM between them, so the
    // trailing foot carries (x_lead - x_com) / (x_lead - x_trail) and the
    // leading foot the rest. Every term is already measured in this method —
    // both foot positions and the same CoM the stance ankle and the
    // Plant->Fall gate read. The trailing foot is fully unloaded exactly when
    // the CoM has rolled onto the leading one, which is the event the FSM is
    // waiting for before it releases the leg (fallComLeadTrigger).
    // One-sided by construction: it can only RAISE the ramp, and it reads 0
    // while the CoM still stands over the trailing foot, so a leg that carries
    // weight is softened exactly as before and the recorded 0.55 / 0.70 cap
    // folds (which raised that floor everywhere) are untouched. The pose it
    // unlocks is not a new one: the accepted cold one-leg probe runs with no
    // cap and no dualSoft at all, at full authority, on a leg unloaded the
    // same way. 0 = the former clearance-only ramp.
    [Tooltip("Fall-catch double support: measured weight share off the swing foot lifts the cap/unload ramp. 0 = clearance-only ramp.")]
    public float walkDualLoadRelease = 1f;
    // Feet closer than this along the walk are one support, not two, and the
    // share degenerates. Quiet stance is not span 0: hipHalfSpacing 0.085 m
    // at viewAngle 80° projects ±1.5 cm, so the soles sit ~3 cm apart
    // (right already leading). 0.04 m sat just above that lunge and DualLoad
    // never opened; 0.02 m is the same FootLeadDeadzone PickStanceFromFeet
    // already trusts as two supports.
    private const float DualLoadMinSpan = 0.02f;
    // Cycle-0 first step. Hip flexion is minus.
    //
    // Direction: cold BeginCycle uses PickStanceFromFeet (stance = the
    // leading foot, swing = the trailer). WalkDir is Sign(moveX)=+1.
    // Standing on the trailer vs the lead did not change the sign of
    // the first-step fall: walkSwap 1, comTravelX −1.4 m, torso 90°
    // back either way. The backward CoM is not the stance label.
    //
    // After FallLeadReady (StanceLead ≥ 0) the trailer is still planted.
    // DualLoad / 55° / walkPlantTiltFirst (+10° to pelvisTarget) then
    // fire on that closed chain. Hip flexion is minus: a planted
    // trailing flexor wants the foot +X, µ=1.2, so GRF on the body is
    // −X. 55° shortens the rear strut (hip drops, backward pitch).
    // Plus pelvis is backward world tilt (hipBaseAngle: minus = forward).
    // Those three are the −X moment; stance-foot choice only picks
    // which leg they fold. Comment 48: DualLoad / hipLift / +10° back
    // stay off while the swing sole is down. Knee-only fold is not that
    // triad: plus flex shortens the trail while DrivePelvis holds the
    // pelvis and stance vault is the tall strut. Last-write 0 only while
    // CoM is still BEHIND the lead (StanceLead < 0) — that 55° is a sit.
    // Once StanceLead ≥ 0 the helper writes FirstStepLiftKneeDeg so the
    // sole can leave. Comment 48 blamed reachKnee 0 for the 23.5° stand;
    // that fold is the lift, not the fall. The hold is the stance ankle
    // CoM loop: RecoverBlend hits 0 at ~21° of torso (0.7×fallAbortDeg)
    // while CoM is still ~6.5 cm inside the sole (toe ≈ 0.13 m). Fall
    // then writes comLead × blend = 0, Catch cycle 0 writes comLead 0
    // at ankleScale 1, and the loop plantarflexes CoP under/ahead of
    // CoM — a lunge, not a topple, walkSwap 0. Hip/pelvis PD only hold
    // the commanded lean spring; they are not the BoS. Do not zero
    // ankleScale (comment 28 / walk_fix2: lean third-law then walks
    // BACKWARD). Keep the CoP target AHEAD of CoM until the toe.
    // After that roll the body travels on the stance ankle (inverted
    // pendulum through the toe, walkSwap 0, torso −71° at ~26 s).
    // Catch/Recover already judge a forward plant
    // (IsSwingFootAheadOfStance / AheadOfCom). That condition stays
    // false because the trailer is BEHIND the lead, DualLoadMinSpan
    // 4 cm is that stacked lunge, and firstStepHoldLift / DrivePelvis
    // never apply the capture-point hip. FirstStepToeCatchReady opens
    // that existing placement once CoM is on the lead and rolling
    // toward the toe (trail N≈0). hipLift / DualLoad / +10° stay off.
    // FallLeadReady still opens Fall. Translator on dirt is the
    // trailing heel-press (plantRockLead dorsiflex, CoP behind CoM,
    // forward GRF) with the leading ankle released only while
    // StanceLead < 0 (that sole is a brake), ankle K snapped in that
    // same window, DrivePelvis on, walkPlantLeanFirst 9° (minus =
    // forward), stance-hip PLUS extend once StanceLead ≥ 0.
    // Comments 27–47 are the closed-chain dead end that tried to walk
    // the 1.5 cm by shearing the hips.
    //
    // Intent / PlantFallGateReady already open on cycle 0. FallLeadReady
    // is StanceLead ≥ 0 (CoM ON the leading foot), not Capture ≥
    // trigger (1.5 cm PAST that foot). The stance ankle stays at
    // ankleScale 1: plantRockLead is that loop's CoP, and a fade to 0
    // left only the 9° trunk lean (third-law pelvis back, comment 28).
    // DualLoad / 55° still wait for StanceLead ≥ 0 (trail N≈0). Comment
    // 30 took RecoverBlend off the 3 cm rock. Comment 31 took it off
    // the 9° lean too (leanRoom 1 until EnterFall). Comment 32 kept the
    // trail CoM loop until that same event. Comment 33 made the 9°
    // lumbar-only (pelvisLean is a bow on a free hip). Comment 34 locked
    // both hips and tracked pelvis tilt so lumbar 9° could rotate the
    // stack about the ankles. Two planted soles + locked hips are a
    // structure, not an inverted pendulum; MixHipHold D still brakes ω;
    // lumbar 9° is ~0.9 cm of chest, short of the 1.5 cm to StanceLead
    // ≥ 0. Comment 35 put trailing-hip extend in the DrivePelvis counter
    // (0.15). Same-hip P-to-vertical cancels that bias at ~2° of pelvis
    // tilt; only the leading hip flexes — a sit, not a lunge. Comment
    // 36 took the trail off DrivePelvis and SPD-extended it; the
    // leading hip stayed the world-vertical "pivot". When pelvis error
    // is 0 that pivot applies signal 0, so the leading hip is only
    // hipFriction 12 — a weld. Equal-length planted legs with a
    // horizontal pelvis are a parallelogram: shear (the 1.5 cm of
    // StanceLead) needs BOTH thighs at the same plus world angle. A
    // welded leading hip is 0 DOF, trail SPD is an internal force,
    // DrivePelvis then flexes the lead (a sit). Comments 37–42 drove
    // that parallelogram with hip WORLD/joint SPD and even zeroed both
    // ankle CoM loops. Hip muscle is an internal couple: extensor on
    // the thigh is clockwise, the pelvis reaction is CCW, so the cheap
    // equilibrium is a sit (thighs stay world-vertical, pelvis nods,
    // StanceLead stays −1.5 cm). Comment 42 then blamed the ankle
    // couple for fighting that pose — but plantRockLead never wrote on
    // cycle 0 (`FallCatchCycleCount != 0` contradicted the comment that
    // said the leading ankle already fired). No external CoP, no lean
    // pivot, snapped ankles: Plant never EnterFall; 55° never legal;
    // sinkAbort ~4 s; walkSwap=0; travelX=0. Comment 43: cycle-0 Plant
    // restores the ankle strategy. plantRockLead writes on cycle 0
    // (CoP behind CoM, GRF moment is external). DrivePelvis stays the
    // lumbar pivot. torsoLean 9° is back (~2.7 cm of chest). Comment
    // 44: pelvisLean is the inverted-pendulum WORLD target (minus =
    // forward), not a free-hip bow — skipping it left only ~1.35 cm
    // of chest CoM, short of the 1.5 cm to StanceLead ≥ 0, and ankle
    // K=3 froze the shin to the flat foot. Cycle 0 Plant writes the 9°
    // pelvisLean and snaps only the ankle dampers (hip K stays). The
    // fulcrum is the ground, not the hinge. Comment 45: that WORLD
    // target is still a bow. DrivePelvis SPD sees pelvis inertia, not
    // the 70 kg stack, so it nods the pelvis on free hips and StanceLead
    // stays −1.5 cm. Comment 45 then SPD-held the quiet hip snapshot
    // (DrivePelvis off, ankles snapped, plantRockLead on). Two planted
    // µ=1.2 soles plus locked hip joints are a table, not an inverted
    // pendulum: the quiet snapshot is the standing parallelogram, so
    // the 1.5 cm of StanceLead has no DoF, rock torques stay internal,
    // Plant never EnterFall. Comment 46: cycle-0 Plant WORLD-holds
    // both thighs at the same clockwise tilt (lower end back = hip
    // forward). Sit leaves the thigh at world 0 and cannot zero that
    // error; equal world angles with feet planted are the shear that
    // carries CoM onto the already-leading foot. Snap hip+ankle
    // dampers (K=12 / K=3 were the weld). Comment 46 then zeroed both
    // ankle CoM loops — that was the remaining WHY. Hip muscle is an
    // internal couple; without the ankle CoP the 70 kg CoM cannot
    // accelerate, StanceLead stays −1.5 cm, Plant never EnterFall.
    // Comment 47: keep the WORLD shear and the damper snap, but put
    // plantRockLead back on both soles. Snapped ankles are hinges, so
    // dorsiflex (CoP behind CoM) is the same clockwise shin/thigh
    // rotation the WORLD pose asks for, not comment 41's fight (that
    // fight was a welded K=3 shin). Freeze the commanded world angle
    // once StanceLead ≥ 0, and keep DrivePelvis off until EnterFall,
    // or the pelvis loop unwinds the 1.5 cm during plantMinDuration.
    // Trunk lean waits (lumbar third-law on this stack is a backward
    // couple). Trailing A2 / plantHipExtend later-cycle only.
    // walkPlantTiltFirst still waits. DualLoad / 55° still StanceLead
    // ≥ 0, but only once Fall starts — Plant dirt must not fold 55°
    // in the freeze wait. Air latches 8°/6°. firstStepHoldLift
    // blocks the 3° place unwind.
    private const float FirstStepPlantHipDeg = 8f;
    private const float FirstStepPlantKneeDeg = 6f;
    // Cycle-0 dirt lift once CoM is on the lead. Matches swingKneeFlex; not
    // reachKneeFlex 140° (that pin on a stacked chain is the squat).
    private const float FirstStepLiftKneeDeg = 55f;
    // The same statics, on the OTHER consumer. liftGateBlend already reads the
    // measured share (walkDualLoadRelease). stanceLoad still does not: it is
    // Lerp(0.5, 1, airRelease), so the whole of double support sits on a 0.5
    // floor. That number is then used as if it were the LEADING foot's load
    // (walkStancePush, walkStanceHipExtend, the stanceLoad >= 0.5 gate) AND
    // as if it were the TRAILING foot's load (plantHipExtend × (1 − clearance)).
    // One number cannot be both. The 0.5 floor therefore under-pushes the
    // stance ankle once CoM has rolled onto it (true share ≈ 1, push at half)
    // and over-pushes the rear hip after the front has taken the weight (true
    // rear share is already ~0, the hip still extends at 0.5 until the foot
    // actually leaves). That is the leftover clearance-proxy this field
    // closes: leading share into stanceLoad, trailing share into the Plant
    // hip, same span/CoM already measured for the ramp. Mix, not a new gain:
    // 0 keeps the 0.5 floor bit for bit, including the plantHipExtend proxy.
    //
    // The trailing ANKLE (walkPlantPushScale) was the third consumer and
    // still is not: it never read a load at all, so its implicit scale is
    // 1. trailWindow GROWS as CoM rolls past the rear foot, which is the
    // right A2 geometry up to the peak and the pin after it — once the
    // front has the weight the rear sole is still plantarflexing at full
    // A2 (swingFoot 0.96). ankleLift only cliffs that at frontShare ≥ 0.5.
    // Trailing share is the continuous fade the hip already uses. The 18:58
    // fear (honest statics kill the 15:05 burst) is FRONT share on this
    // ankle; rear share at the start of the window is > 0.5, and the
    // fallback when the share was not measured stays 1, so flag 0 is
    // bitwise the former burst. Mix is the same field: one statics, both
    // rear joints.
    [Tooltip("Fall-catch double support: measured leading/trailing weight share for stanceLoad and the Plant rear hip/ankle. 0 = 0.5 floor / former Plant A2.")]
    public float walkStanceLoadShare = 1f;
    // Plant never started the Fall knee-lift, so the trailing foot spent the
    // longest phase of the cycle on pelvis-upright (an extensor against any
    // fold) and on a 0.25 grounded-knee fraction. That is the shuffle:
    // swingFoot stays near 0.92 even when Fall later folds, because Plant
    // is 0.5–3 s of dual support. Knee torque is thigh–shin, so it does
    // not fight the pelvis loop; the stance hip still owns world vertical.
    // The stance ankle stays at ankleScale 1 — this is not the ankleScale-0
    // Fall that left the trunk without a restoring moment. The trailing
    // ankle only fades its own plantarflex (same ankleLift as Fall) so µ=0.4
    // does not turn the shortening into a slide. Gated on the measured
    // dual-support unload (liftGateBlend): a trailing foot that still
    // carries weight is not shortened. 0 = former Plant with no swing fold.
    [Tooltip("Plant: fold the unloaded trailing knee (swingKneeFlex × load-share). 0 = former Plant, no swing fold.")]
    public float walkPlantSwingKnee = 1f;
    // 22:00 opened that fold at mid-span (plantKneeClock >= 0.5) so the first
    // centimetre of CoM roll did not yank the trailing hip off the pelvis
    // loop. The pose itself was still swingKneeFlex × clock: at the gate
    // that is already 27.5°, which is a step in the knee command — and in
    // hipLift / ankleLift — on the same 5 ms frame DrivePelvis drops.
    // A step in a target is a step in torque (sci_a). This spends that
    // 27.5° across the remaining half-span: fold is 0 at the gate (rear
    // hip push and A2 still own the joint) and full only when the rear
    // carries nothing. Mix, not a new gain: 0 is the 22:00 multiply.
    //
    // 22:36 remapped the pose but left two jumps on that same gate frame:
    // thighHold wrote full Fall hold (not × plantFold), and reachKneeFolding
    // is reachKnee > 0.5°, which at swingKneeFlex 55° is plantFold > 0.009
    // — clock ≈ 0.504, rear still carrying ~half. DrivePelvis dropping
    // there is a closed-chain flexor takeover (ThighHold) on a loaded
    // trailing hip, the sci_a shape 21:46 took the hip off the pelvis
    // loop to avoid, only now at mid-span instead of at the first
    // centimetre. Same mix finishes it: thighHold follows plantFold,
    // and Plant DrivePelvis stays until plantFold >= 0.5 (the hipLiftFloor
    // already used as "ThighHold has authority"). Knee / ankleLift still
    // ramp from the 22:00 gate so the fold is not a 27.5° step when the
    // hip finally hands off. 0 = 22:00 multiply, full hold, immediate drop.
    [Tooltip("Plant swing fold: remap the 0.5…1 load-share onto 0…1 pose (no 27.5° step at mid-span). 0 = former swingKneeFlex × clock.")]
    public float walkPlantKneeRamp = 1f;
    // walkPlantSwingKnee still skipped cycle 0. That was the DualLoadMinSpan
    // gate: both feet sit under the hip, load-share never opens, liftGateBlend
    // stays 0, and the first Fall inherited a straight trailing shin. WALK-2
    // initiation is knee lift + stance drive + forward lean. Lean is already
    // on cycle 0 (walkPlantLeanFirst); trailing A2 / hip stay later-cycle
    // (no rear moment arm). Knee torque is thigh–shin, so it does not fight
    // the pelvis loop — the stance hip still owns world vertical. The clock is
    // the same plantMinDuration ramp the trunk lean already uses, remapped by
    // walkPlantKneeRamp so the gate is not a 27.5° step. DrivePelvis stays
    // (plantHipRelease stays below 0.5) while that stacked foot is still down;
    // thighHold / hipLift / ankleLift are stored so Plant-air keep and Fall
    // leftover have terms if the fold actually clears. 0 = former later-cycle
    // only.
    [Tooltip("Plant swing-knee fold also on cycle 0 (gait initiation). 0 = former later-cycle only.")]
    public float walkPlantSwingKneeFirst = 1f;
    // WALK-2 initiation is knee lift + stance drive + forward lean. Lean and
    // knee already share the plantMinDuration clock on cycle 0; the stance
    // HIP does not. walkStanceHipExtend is the right joint (plus = thigh
    // lower end back, planted foot carries the pelvis forward) but its gate
    // is pushAfterAir × pushWindow: Plant reports PhaseCode 0, the swing
    // has never left, and CoM still sits on the stacked feet, so both
    // terms are 0. Waiting for lead is circular — the hip is what would
    // make the lead. This is not trailing A2 / plantHipExtend (no rear
    // moment arm while the feet are together, and WALK-2 forbids
    // push-off as the start). Same gain / clamp / counter as the later-
    // cycle lever, same PlantGateReady / restore / surrender band as lean
    // and knee, faded by plantComLeadMax so the hip does not stall the
    // Plant→Fall gate. DrivePelvis stays (plantHipRelease is 0 on cycle 0)
    // so this bias stays inside the pelvis loop. 0 = former wait-for-lead.
    [Tooltip("Plant stance-hip drive on cycle 0 (gait initiation). 0 = former wait for CoM lead / airborne swing.")]
    public float walkPlantStanceDriveFirst = 1f;
    // walkPlantSwingKneeFirst wrote the 55° Plant fold on cycle 0, then the
    // damper ate it. walkSwingFrictionLoadShare fades that weld from
    // liftGateBlend, and DualLoadMinSpan never opens while both feet sit
    // under the hip, so fade stays 0, snap never arms, and stopTorque 15 N·m
    // still holds ~14 kg on the shin the SPD is trying to shorten. Same
    // plantMinDuration clock the fold already uses, same 0.5 snap gate the
    // later-cycle dirt path already uses, same PlantGateReady / restore /
    // surrender band. DrivePelvis stays (plantHipRelease is 0 on cycle 0).
    // 0 = former cycle-0 weld until a millimetre of air.
    [Tooltip("Plant swing-leg viscosity fade on cycle 0 (gait initiation). 0 = former DualLoad-only weld until air.")]
    public float walkPlantSwingFrictionFirst = 1f;
    // WALK-2 initiation is knee lift + stance drive + forward lean. Lean,
    // swing knee, stance hip and swing friction already share the
    // plantMinDuration clock on cycle 0. The STANCE knee does not.
    // walkStanceKneeBendLiftFade already wants to fade that extra 5° as
    // the swing unloads, but it reads liftGateBlend, and DualLoadMinSpan
    // never opens while both feet sit under the hip, so the schedule stays
    // at the 0.5 end — 5° of stance flex on the only strut. The vault
    // comment already names that as the shuffle (pelvis settles on the
    // shorter leg). Later-cycle Plant skips the bend (stancePlantKnee from
    // leftover). Cycle 0 is the quiet start: no leftover, so it is the
    // only Plant still shortening the stance while the swing tries to
    // fold. Same plantMinDuration clock, same PlantGateReady / restore /
    // surrender band. DrivePelvis stays (plantHipRelease is 0 on cycle 0).
    // Not a Fall vault to 0° and not trailing A2. 0 = former extra 5°
    // until DualLoad or air.
    [Tooltip("Plant stance-knee extra bend fades on cycle 0 (gait initiation). 0 = former DualLoad-only 5° shuffle flex.")]
    public float walkPlantStanceStraightFirst = 1f;
    // WALK-2 initiation is knee lift + stance drive + forward lean. Lean,
    // swing knee, stance hip, swing friction and stance-knee straighten
    // already share the plantMinDuration clock on cycle 0. The SWING HIP
    // flexor bias does not. swingHipUnloadBias is the right term (minus =
    // thigh lower end forward, the knee rises instead of the foot sliding
    // back) but it is scaled by liftSoft × standLegLevel. DualLoadMinSpan
    // never opens while both feet sit under the hip, so liftGateBlend stays
    // 0, unloadSoft stays dualSupportSwingScale 0.32, the cap freezes
    // standLegLevel at 0.40, and the product arrives as 0.041 of the tuned
    // 0.32. That is why a 55° Plant fold still leaves the foot on dirt: the
    // pelvis loop is an extensor against any thigh rise, and the bias that
    // would cancel it never reached the joint. DrivePelvis stays
    // (plantHipRelease is 0 on cycle 0) — this is a bias inside that loop,
    // not ThighHold and not −swingHipFlex (sci_a). Same PlantGateReady /
    // restore / surrender band. 0 = former DualLoad-only 0.041 until air.
    [Tooltip("Plant swing-hip flexor unload on cycle 0 (gait initiation). 0 = former DualLoad-only 0.041 until air.")]
    public float walkPlantSwingUnloadFirst = 1f;
    // WALK-2 initiation is knee lift + stance drive + forward lean. Lean,
    // swing knee, stance hip, swing friction, stance-knee straighten and
    // swing-hip unload already share the plantMinDuration clock on cycle 0.
    // The PELVIS TARGET does not. standLegPelvisTilt is the term that
    // lifted oneg_l (swingFoot 0.30): hip flexion alone raises the thigh
    // relative to the pelvis, while tilting the pelvis raises the whole
    // swing hip — ground clearance without asking the knee for more flex
    // (walkStandTiltAirRelease). walkPlantSwingUnloadFirst is a 0.32
    // flexor bias inside DrivePelvis; the loop's target is still near
    // vertical, so the same loop stays an extensor against any thigh
    // rise and the 55° fold still slides. DualLoadMinSpan never opens
    // while both feet sit under the hip, so liftGateBlend stays 0,
    // tiltSoft stays dualSupportSwingScale 0.32, standLegLevel stays
    // dualSupportStandCap 0.40, and the product is 1.28° of that 10°.
    // Same plantMinDuration clock, same plus sign the walk mirror already
    // uses on both supports, same PlantGateReady / restore / surrender
    // band. DrivePelvis stays — this is that loop's TARGET, not
    // ThighHold and not −swingHipFlex (sci_a). 0 = former 1.28° until air.
    [Tooltip("Plant pelvis-tilt unload on cycle 0 (gait initiation). 0 = former DualLoad-only 1.28° until air.")]
    public float walkPlantTiltFirst = 1f;
    // WALK-2 initiation is knee lift + stance drive + forward lean. Lean,
    // swing knee, stance hip, swing friction, stance-knee straighten,
    // swing-hip unload and pelvis tilt already share the plantMinDuration
    // clock on cycle 0. The SWING HIP LIFT does not. walkPlantSwingKnee
    // already writes hipLift / thighHold on that clock (knee rises instead
    // of the foot sliding back), and later-cycle Plant hands the trailing
    // hip to ThighHold once plantFold >= 0.5. Cycle 0 forced plantHipRelease
    // to 0 so DrivePelvis stayed: DualLoadMinSpan never opens while both
    // feet sit under the hip, and taking a stacked hip off the pelvis loop
    // was the closed-chain flexor sci_a. That lock now discards the lift
    // those other clocks already paid for: reachKneeFolding stays false,
    // ApplyFallCatchSwingThighHold never runs, the 55° fold still slides
    // because the same loop is still an extensor against the thigh. This
    // is that later-cycle hand-off on the same plantFold (cap 16–32°, not
    // −swingHipFlex). Restore / surrender keep DrivePelvis (brake).
    // 0 = former cycle-0 DrivePelvis until DualLoad or air.
    [Tooltip("Plant swing-hip ThighHold on cycle 0 once plantFold >= 0.5. 0 = former DrivePelvis until DualLoad or air.")]
    public float walkPlantHipReleaseFirst = 1f;
    // WALK-2 initiation is knee lift + stance drive + forward lean. Lean,
    // swing knee, stance hip, swing friction, stance-knee straighten,
    // swing-hip unload, pelvis tilt and ThighHold already share the
    // plantMinDuration clock on cycle 0. The SWING ANKLE does not.
    // Fall-catch dual support always takes liftBlend < 0.001 (that clock
    // is legacy Stance), so BOTH ankles get the CoM loop. Quiet stance /
    // one-leg already zero a grounded swing ankle so it does not fight
    // the support. At comLead 0 that hold cancelled lean (WALK-2 root).
    // plantRockLead makes the shared leading-foot error a heel press:
    // CoP behind CoM on the trailing sole, which is the only translation
    // a µ=1.2 stacked pair can make. Fading that loop on the rock's own
    // clock left clock·(1−clock) ≤ 0.25, then the pin, then a 9° bow.
    // Keep the trail CoM loop until StanceLead ≥ 0 (FallLeadReady /
    // DualLoad); the clock fade is the unload at that event, not the
    // initiation. Not trailing A2 (plantarflex on a 26 cm sole whose
    // toe is already ahead of CoM — comment 30) and not an ankle-angle
    // PD. walkPlantSwingAnkleKeepFirst keeps the released signal through
    // the Plant ankleLift overwrite (plantFold). Restore / surrender
    // keep the hold (brake).
    // 0 = former dual-support CoM on both soles until DualLoad or air.
    [Tooltip("Plant swing-ankle CoM release on cycle 0 (gait initiation). 0 = former dual-support CoM hold on both soles.")]
    public float walkPlantSwingAnkleReleaseFirst = 1f;
    // WALK-2 initiation is knee lift + stance drive + forward lean. Lean,
    // swing knee, stance hip, swing friction, stance-knee straighten,
    // swing-hip unload, pelvis tilt, ThighHold and swing-ankle CoM
    // release already share the plantMinDuration clock on cycle 0.
    // The Plant ankleLift overwrite does not. walkPlantSwingAnkleReleaseFirst
    // fades the dual-support CoM hold, then this block replaces that
    // zero with the swing foot's own CoM loop the moment plantFold writes
    // ankleLift (clock >= 0.5). On stacked feet that plantarflex is the
    // same WALK-2 root: CoM ahead of the sole asks the toe to push, which
    // cancels rock lead / stance-hip / trunk lean. Keep the released
    // signal and apply only the dorsiflex unweight (toe-up so µ=0.4
    // does not slide the shortening shin). Not trailing A2 and not an
    // ankle-angle PD. Restore / surrender keep the overwrite (brake).
    // 0 = former CoM overwrite once plantFold writes ankleLift.
    [Tooltip("Plant cycle-0: keep released swing-ankle CoM; ankleLift is dorsiflex only. 0 = former CoM overwrite at plantFold.")]
    public float walkPlantSwingAnkleKeepFirst = 1f;
    // WALK-2 initiation is knee lift + stance drive + forward lean. Lean,
    // swing knee, stance hip, swing friction, stance-knee straighten,
    // swing-hip unload, pelvis tilt, ThighHold, swing-ankle CoM release
    // and walkPlantSwingAnkleKeepFirst already share the plantMinDuration
    // clock on cycle 0. Plant→Fall does not keep that ankle *signal*.
    // walkPlantFallFoldHold already Maxes the pose clocks (reachKnee /
    // hipLift / thighHold / ankleLift) across the PhaseAge reset; this
    // block then overwrites the swing sole with its own CoM loop and the
    // propulsive-half clamp Max(0, signal·dir). Cycle 0 has no trailing
    // arm: CoM sits on the stacked soles, the loop is ~0 or dorsiflex
    // from rock lead, and that clamp deletes dorsiflex. ankleLift's mix
    // only adds toe-up against plantarflex, so a zero loop stays zero.
    // The Plant keep dies on the first 5 ms Fall frame — the phase whose
    // job is the lift. Same keep formula (released CoM + dorsiflex
    // unweight, not trailing A2 and not an ankle-angle PD). Grounded
    // Reach is the same hole one timeout later (Fall→Reach while the
    // sole is still down). Catch keeps the overwrite (landing ankle).
    // Later cycles keep the overwrite (trailing A2). Restore / surrender
    // never reach this Fall (EnterStandingPlant).
    // 0 = former CoM overwrite + propulsive-half on first Fall/Reach.
    [Tooltip("Plant→Fall cycle-0: keep released swing-ankle CoM + dorsiflex; skip propulsive-half. 0 = former Fall overwrite.")]
    public float walkPlantFallAnkleKeepFirst = 1f;
    // walkPlantFallAnkleKeepFirst skips the swing-foot CoM overwrite, then
    // reads leftAnkleBalance as "current". That is the released zero only
    // in Plant: walkPlantSwingAnkleReleaseFirst is Plant-only, and EnterFall
    // resets PhaseAge, so Fall dual-support (liftBlend < 0.001) writes the
    // midpoint CoM loop back onto BOTH soles. Keeping that current is the
    // WALK-2 root again — plantarflex when CoM is ahead of the stacked
    // feet, which cancels rock lead / stance-hip / trunk lean. Hold the
    // Plant release endpoint (zero) through grounded Fall/Reach; ankleLift
    // is still only dorsiflex unweight. Catch / later cycles keep the
    // overwrite (landing A2). Restore / surrender never reach this Fall.
    // 0 = former keep of dual-support current (04:35 keep as written).
    [Tooltip("Plant→Fall cycle-0: Fall/Reach keep uses the released zero, not dual-support CoM. 0 = former keep of current.")]
    public float walkPlantFallAnkleReleaseHoldFirst = 1f;
    // WALK-2 initiation is knee lift + stance drive + forward lean. Lean,
    // swing knee, stance hip, swing friction, stance-knee straighten,
    // swing-hip unload, pelvis tilt, ThighHold and the swing-ankle keep
    // already share the plantMinDuration clock on cycle 0. Plant→Fall
    // already Maxes the pose clocks (walkPlantFallFoldHold) and holds
    // the released ankle zero (walkPlantFallAnkleReleaseHoldFirst).
    // The viscosity fade does not. walkPlantSwingFrictionFirst is
    // Plant-only; EnterFall resets PhaseAge, DualLoadMinSpan still
    // never opens, liftGateBlend stays 0, airRelease is 0 while the
    // sole is down, so loadFade is 0. ApplySwingLegFriction(0) then
    // restores the built weld (the restore path on a phase change):
    // stopTorque 15 N·m on the shin walkPlantFallFoldHold is trying to
    // keep at 55°. That is the 03:58 no-op one phase later. Hold the
    // Plant fade endpoint (1 — EnterFall already required plantAged)
    // through grounded Fall/Reach; snap stays at the same 0.5 dirt
    // gate. Catch / later cycles keep DualLoad / airRelease. Restore /
    // surrender never reach this Fall (EnterStandingPlant).
    // 0 = former weld restore on the first Fall/Reach frame.
    [Tooltip("Plant→Fall cycle-0: keep the Plant swing-leg viscosity fade through grounded Fall/Reach. 0 = former weld restore on PhaseAge reset.")]
    public float walkPlantFallFrictionHoldFirst = 1f;
    // WALK-2 initiation is knee lift + stance drive + forward lean. Lean,
    // swing knee and stance-hip drive already share the plantMinDuration
    // clock on cycle 0; Plant→Fall already keeps the fold, the released
    // ankle and the viscosity fade. The STANCE hip drive does not.
    // walkPlantStanceDriveFirst is Plant-only; EnterFall resets PhaseAge,
    // DualLoadMinSpan still never opens, the swing has never left so
    // pushAfterAir is 0, CoM still sits on the stacked feet so
    // pushWindow is 0, and the later-cycle extend is 0. The stance hip
    // goes back to world-upright only — the third-law cancel of the
    // pelvis carry the 55° fold needs if the swing sole is to unload.
    // This is not trailing A2 / plantHipExtend (no rear moment arm, and
    // WALK-2 forbids push-off as the start). Hold the Plant endpoint
    // (clock 1 — EnterFall already required plantAged) through Fall/
    // Reach; firstRoom still fades at plantComLeadMax so the hip does
    // not stall the gate. Catch / later cycles keep pushAfterAir /
    // pushWindow. Restore / surrender never reach this Fall.
    // 0 = former world-upright stance hip on the first Fall/Reach frame.
    [Tooltip("Plant→Fall cycle-0: keep the Plant stance-hip drive through Fall/Reach. 0 = former world-upright stance hip on PhaseAge reset.")]
    public float walkPlantFallStanceDriveHoldFirst = 1f;
    // WALK-2 initiation is knee lift + stance drive + forward lean. Knee
    // fold (walkPlantFallFoldHold) and stance-hip drive
    // (walkPlantFallStanceDriveHoldFirst) already survive Plant→Fall.
    // The trunk lean does not. walkPlantLeanFirst writes the Plant 9°
    // on cycle 0, then fades to 0 as CoM reaches fallComLeadTrigger so
    // Fall's own ramp can start without a step. Cycle 0 never opens
    // DualLoadMinSpan, CoM stays on the stacked feet, leanRoom stays
    // ~1, and Plant still has the full 9° at the plantLeadWait
    // deadline — then Fall assigns fallLean = scale * calm *
    // fallLiftClock with PhaseAge 0. That is a 9°→0 step on the first
    // 5 ms Fall frame, the third leg of the triangle gone. Hold the
    // Plant endpoint (clock 1, current leanRoom * calm) through Fall/
    // Reach; Fall's own clock Maxes when it overtakes. Catch / later
    // cycles keep Fall/Reach envelopes. Restore / surrender never
    // reach this Fall. Happy-path leanRoom→0 at the gate is a no-op.
    // 0 = former 9°→0 on the first Fall/Reach frame.
    [Tooltip("Plant→Fall cycle-0: keep the Plant trunk lean through Fall/Reach. 0 = former PhaseAge-reset 9° drop.")]
    public float walkPlantFallLeanHoldFirst = 1f;
    // WALK-2 initiation is knee lift + stance drive + forward lean. The
    // triangle (fold / stance-hip / trunk lean) already survives
    // Plant→Fall. The pelvis TARGET does not. walkPlantTiltFirst writes
    // the oneg_l 10° on cycle 0 so the stance hip raises the swing hip
    // instead of the 55° fold sliding. EnterFall resets PhaseAge,
    // DualLoadMinSpan still never opens, liftGateBlend stays 0, so
    // tiltSoft stays dualSupportSwingScale 0.32 and standLegLevel stays
    // dualSupportStandCap 0.40: the product is 1.28° again. DrivePelvis
    // on the stance hip then yanks the pelvis toward vertical — that
    // drops the swing hip the 10° bought and fights the 9° lean
    // walkPlantFallLeanHoldFirst just held. Hold the Plant endpoint
    // (clock 1 — EnterFall already required plantAged) through Fall/
    // Reach. Catch / later cycles keep DualLoad tiltSoft. Restore /
    // surrender never reach this Fall. This is that loop's TARGET, not
    // ThighHold and not −swingHipFlex (sci_a).
    // 0 = former 10°→1.28° on the first Fall/Reach frame.
    [Tooltip("Plant→Fall cycle-0: keep the Plant pelvis-tilt unload through Fall/Reach. 0 = former DualLoad-only 1.28° on PhaseAge reset.")]
    public float walkPlantFallTiltHoldFirst = 1f;
    // WALK-2 initiation is knee lift + stance drive + forward lean. The
    // triangle, friction fade, ThighHold and pelvis tilt already survive
    // Plant→Fall. The STANCE knee straighten does not.
    // walkPlantStanceStraightFirst faded the extra 5° (walkStanceKneeBend
    // × 0.5) on the plantMinDuration clock so the only strut is tall
    // while the swing folds. EnterFall resets PhaseAge, DualLoadMinSpan
    // still never opens, liftGateBlend stays 0, so the fade schedule
    // returns to its 0.5 end — 5° of stance flex on the lift phase.
    // Fall vault is the same clock: vaultClock is 0 on that first 5 ms
    // frame, so stanceVault is 0 and the extra bend is not skipped.
    // Hip-to-ankle on a two-bar leg is 2·L·cos(knee/2): those 5° sit
    // the stance hip ~4 mm lower, which is swing-foot clearance spent
    // on the only loaded shin (the shuffle the vault comment names).
    // Hip-unload hold would be a no-op here (ThighHold already owns
    // the swing hip on Fall via fold hold). Hold the Plant endpoint
    // (clock 1 — EnterFall already required plantAged) through Fall/
    // Reach so the fade stays at 0 extra. Catch / later cycles keep
    // DualLoad / vault. Restore / surrender never reach this Fall.
    // Not trailing A2 and not a Fall vault to 0°.
    // 0 = former extra 5° on the first Fall/Reach frame.
    [Tooltip("Plant→Fall cycle-0: keep the Plant stance-knee straighten through Fall/Reach. 0 = former extra 5° on PhaseAge reset.")]
    public float walkPlantFallStanceStraightFirst = 1f;
    // WALK-2 initiation is knee lift + stance drive + forward lean. The
    // triangle, friction fade, ThighHold, pelvis tilt and stance-knee
    // straighten already survive Plant→Fall. The ANKLE half of the
    // roll-over does not. walkPlantRockLead is the CoP setpoint the
    // Plant→Fall gate waits for (0.03 m, clock 1 at plantAged); lean
    // is the trunk half and already holds. EnterFall replaces
    // fc.comLead with fallComLead × blend (0.025 m on a calm trunk)
    // and walkFallComRollToe starts at PhaseAge 0, so rollT is 0:
    // the first Fall frames pin CoM on a stationary 0.025 m — the
    // shuffle that comment named. Grounded Reach is worse: Reach
    // writes comLead 0, so the stance ankle returns to holding CoM
    // over the stacked feet (the WALK-2 root) while Fall→Reach
    // still waits on air. Hold the Plant endpoint (clock 1, current
    // rockRoom × calm). Fall's own roll Maxes when it overtakes.
    // Catch / later cycles keep fallComLead / RecoverBrake. Not
    // trailing A2 and not an ankle-angle PD.
    // 0 = former 0.03→0.025 (Fall) / 0.03→0 (Reach) on PhaseAge reset.
    [Tooltip("Plant→Fall cycle-0: keep the Plant rock-lead CoP through Fall/Reach. 0 = former fallComLead 0.025 / Reach 0 on PhaseAge reset.")]
    public float walkPlantFallRockHoldFirst = 1f;
    // WALK-2 initiation is knee lift + stance drive + forward lean. The
    // triangle, friction fade, ThighHold, pelvis tilt, stance-knee
    // straighten and rock-lead CoP already survive Plant→Fall.
    // The ACTUATOR of that CoP does not. Fall writes ankleScale 1, then
    // after fallKneeRampSeconds (0.15 s) StepPhaseDriver enters Reach
    // even with the swing still on dirt — DualLoadMinSpan never opens,
    // so that is the cycle-0 lift phase. Reach then Lerp(1, 0.25, calm)
    // on a stacked calm trunk: 0.25. walkPlantFallRockHoldFirst kept
    // the 0.03 m setpoint; ankleBalance *= 0.25 throws away 75% of the
    // torque that setpoint is supposed to make, so the stance sole
    // cannot finish the roll-over Fall→Reach is waiting on (air).
    // Hold Plant/Fall's ankleScale 1 while that sole is down and the
    // held comLead is live. Air Reach keeps 0.25 (do not fight a real
    // fall). Catch / later cycles keep the Reach envelope. Hip-unload
    // hold is still a no-op (ThighHold already owns the swing hip on
    // Fall via fold hold). Not trailing A2 and not an ankle-angle PD.
    // 0 = former Reach 0.25 on the first grounded Fall→Reach frame.
    [Tooltip("Plant→Fall cycle-0: keep Fall ankleScale 1 through grounded Reach while rock-lead is live. 0 = former Reach 0.25 on PhaseAge reset.")]
    public float walkPlantFallAnkleScaleHoldFirst = 1f;
    // WALK-2 initiation is knee lift + stance drive + forward lean. The
    // triangle, friction fade, ThighHold, pelvis tilt, stance-knee
    // straighten, rock-lead CoP and ankleScale already survive Plant→Fall.
    // The FOLD MAGNITUDE does not. walkPlantFallFoldHold Maxes Plant's
    // 55° across the PhaseAge reset, then Fall's 2× fold clock overtakes
    // to reachKneeFlex 140° in fallKneeRampSeconds (0.15 s). DualLoadMinSpan
    // still never opens, so that 140° is a squat on a still-shared plant:
    // hip-to-ankle is 2·L·cos(knee/2), 55° vs 140° is ~0.78 L vs ~0.34 L,
    // and the pelvis drops onto both soles. liftGateBlend stays 0,
    // Fall→Reach still waits on air. Plant's own walkPlantSwingKnee
    // comment already named that squat (fold is swingKneeFlex, not 140°).
    // Cap at lastPlantSwingReachKnee while the swing sole is down. Air
    // Fall/Reach keeps 140° (short chain once the foot is free). Catch /
    // later cycles keep the envelope. Hip-unload hold is still a no-op
    // (ThighHold already owns the swing hip on Fall via fold hold). Not
    // trailing A2 and not −swingHipFlex (sci_a).
    // 0 = former grounded Fall/Reach ramp to 140° on cycle 0.
    [Tooltip("Plant→Fall cycle-0: cap grounded Fall/Reach swing fold at the Plant 55°. 0 = former ramp to reachKneeFlex 140° on dirt.")]
    public float walkPlantFallFoldCapFirst = 1f;
    // WALK-2 initiation is knee lift + stance drive + forward lean. The
    // triangle, friction fade, ThighHold, pelvis tilt, stance-knee
    // straighten, rock-lead CoP, ankleScale and the 55° fold cap already
    // survive Plant→Fall. The STANCE HIP LOOP SCALE does not. Plant writes
    // hipBalanceScale 1. Fall then Lerp(1, 0.15, calm) and Reach
    // Lerp(1, 0.25, blendBack). DualLoadMinSpan still never opens, so
    // cycle 0 Fall/Reach is a calm stacked trunk: calm = 1, blendBack = 1,
    // and the loop that holds the pelvis up against the 55° shortening
    // drops to 0.15 / 0.25. The 0.15 was sized for a committed topple
    // (do not cancel the fall); on a still-shared plant it is the squat
    // the fold cap just stopped from the knee side — pelvis sags onto
    // both soles, liftGateBlend stays 0, Fall→Reach still waits on air.
    // Hold 1 while the swing sole is down. Air Fall/Reach keeps 0.15 /
    // 0.25 (do not fight a real fall). Catch / later cycles keep the
    // envelope. Hip-unload hold is still a no-op (ThighHold already
    // owns the swing hip on Fall via fold hold). Not trailing A2 and
    // not a PD retune.
    // 0 = former Fall 0.15 / Reach 0.25 on the first grounded Fall frame.
    [Tooltip("Plant→Fall cycle-0: keep Plant hipBalanceScale 1 through grounded Fall/Reach. 0 = former Fall 0.15 / Reach 0.25 on a calm stacked trunk.")]
    public float walkPlantFallHipBalanceHoldFirst = 1f;
    // WALK-2 initiation is knee lift + stance drive + forward lean. The
    // triangle, friction fade, ThighHold, pelvis tilt, stance-knee
    // straighten, rock-lead CoP, ankleScale, the 55° fold cap and
    // hipBalanceScale 1 already survive Plant→Fall. The WALK SPEED /
    // BASE LEAN GATE does not. Plant sets suppressWalkLean, which zeros
    // walkAutoLean (speed error × 8, cap 6°) and walkBasePelvisLean (2°)
    // plus the torso pair (7.2° + 4°). Fall and Reach never set that
    // flag. DualLoadMinSpan still never opens, so cycle 0 Fall/Reach is
    // a stacked trunk at rest: walkSpeedError = walkTargetSpeed = 1, and
    // the first 5 ms Fall frame adds 8° of pelvis and 11.2° of chest on
    // top of the held 9° fc lean. Lumbar travel is ±20°; 9 + 11.2 sits
    // on the stop. A step in a pelvis/chest target is the sci_a shove
    // every other Fall ramp exists to avoid — it drives the pelvis back
    // through the joint pair and cancels the roll-over Fall→Reach is
    // waiting on (air). Hold Plant's suppress while the swing sole is
    // down. Air Fall/Reach keeps speed/base lean (do not starve a real
    // fall). Catch / later cycles keep their own suppress (RecoverBrake
    // / Plant). Hip-unload hold is still a no-op (ThighHold already
    // owns the swing hip on Fall via fold hold). Not trailing A2, not
    // a PD retune, and not a walkTargetSpeed change (board slot).
    // 0 = former speed/base lean step on the first grounded Fall frame.
    [Tooltip("Plant→Fall cycle-0: keep Plant suppressWalkLean through grounded Fall/Reach. 0 = former 8°/11° speed+base lean step on dirt.")]
    public float walkPlantFallSuppressWalkLeanFirst = 1f;
    // WALK-2 initiation is knee lift + stance drive + forward lean. The
    // triangle, friction fade, ThighHold, pelvis tilt, stance-knee
    // straighten, rock-lead CoP, ankleScale, the 55° fold cap,
    // hipBalanceScale 1 and suppressWalkLean already survive Plant→Fall.
    // The GROUNDED FOLD CEILING does not. walkPlantFallFoldCapFirst
    // pinned the shin at lastPlant 55° so Fall's 2× clock could not
    // squat a sagging pelvis (140° → 2·L·cos(70°) ≈ 0.34 L). That pin
    // was sized for hipBalanceScale 0.15 and the 8°/11° lumbar slam;
    // both are held now, so the strut can carry more shortening than
    // Plant paid. DualLoadMinSpan still never opens, liftGateBlend = 0,
    // Fall→Reach still waits on air, and 55° on a still-shared plant
    // is the same 0.78 L the initiation fold already commanded — if
    // that pose did not clear, Fall cannot add a millimetre. Raise the
    // Min to a quarter of the remaining Fall fold (Lerp 55→140 at
    // 0.25 = 76°, 2·L·cos(38°) ≈ 0.79 L) while the sole is down. Fall's
    // clock Maxes from 55; this only raises the ceiling. Air keeps
    // 140°. Catch / later cycles keep the envelope. Hip-unload hold is
    // still a no-op (ThighHold already owns the swing hip on Fall via
    // fold hold). Not trailing A2, not −swingHipFlex (sci_a), and not
    // a PD retune.
    // 0 = former grounded pin at Plant 55°.
    [Tooltip("Plant→Fall cycle-0: let grounded Fall/Reach fold grow from Plant 55° a quarter of the way to reachKneeFlex. 0 = former 55° pin on dirt.")]
    public float walkPlantFallFoldGrowFirst = 1f;
    // WALK-2 initiation is knee lift + stance drive + forward lean. The
    // triangle, friction fade, ThighHold, pelvis tilt, stance-knee
    // straighten, rock-lead CoP, ankleScale, the 55° fold cap,
    // hipBalanceScale 1, suppressWalkLean and the 76° grow ceiling
    // already survive Plant→Fall. The FOLD HOLD FLOOR does not.
    // walkPlantFallFoldGrowFirst raised Min to Lerp(55, 140, 0.25) =
    // 76° (2·L·cos(38°) ≈ 0.79 of the straight chain vs 55° at
    // 2·L·cos(27.5°) ≈ 0.89). Fold hold still Maxes lastPlant 55°
    // across PhaseAge 0, and Fall's 2× clock starts at 0: first Fall
    // frames stay at the 55° that already failed to clear. DualLoadMinSpan
    // still never opens, liftGateBlend = 0, Fall→Reach still waits on
    // air. Time to overtake 55° is ~30 ms, to hit 76° ~41 ms of the
    // 150 ms Fall — a quarter of the lift phase sitting on the Plant
    // pose. Max to the grow cap on the first grounded Fall/Reach frame
    // so the extra shortening is commanded immediately. The 2× clock
    // still Maxes if it would exceed 76; Min still caps. Air keeps
    // 140°. Catch / later cycles keep the envelope. Hip-unload hold
    // is still a no-op (ThighHold already owns the swing hip on Fall
    // via fold hold). Not trailing A2, not −swingHipFlex (sci_a), and
    // not a PD retune.
    // 0 = former wait for the 2× clock to climb 55°→76°.
    [Tooltip("Plant→Fall cycle-0: seed grounded Fall/Reach fold at the 76° grow cap instead of waiting for the 2× clock. 0 = former climb from Plant 55°.")]
    public float walkPlantFallFoldSeedFirst = 1f;
    // WALK-2 initiation is knee lift + stance drive + forward lean. The
    // triangle, friction fade, ThighHold, pelvis tilt, stance-knee
    // straighten, rock-lead CoP, ankleScale, the 55° fold cap,
    // hipBalanceScale 1, suppressWalkLean, the 76° grow ceiling and
    // the fold-hold seed already survive Plant→Fall. The LIFT PAIR
    // that must match that extra fold does not. Fold seed Maxes
    // reachKnee to 76° on the first grounded Fall frame; hipLift /
    // ankleLift stay at the Plant leftover sized for 55° (Fall's own
    // 0.5 cap × plantFold, then fold hold Maxes that leftover while
    // the 2× lift clock starts at 0). hipLift is the flexor bias that
    // raises the knee instead of sliding the foot back; ankleLift
    // unweights that same sole so µ=0.4 does not turn the extra
    // shortening into a backward swing — the start WALK-2 forbids.
    // DualLoadMinSpan still never opens, liftGateBlend = 0, Fall→Reach
    // still waits on air. Same quarter as fold grow: Lerp(Plant, 1,
    // 0.25) so 0.5→0.625, still a Clamp01 bias, still fades at the
    // 16–32° ThighHold cap, not −swingHipFlex (sci_a) and not a 1.0
    // dorsiflexor heel-press. Air Fall/Reach keeps Plant/Fall 0.5.
    // Catch / later cycles keep the envelope. Hip-unload hold is
    // still a no-op (ThighHold already owns the swing hip on Fall
    // via fold hold). Not trailing A2 and not a PD retune.
    // 0 = former Plant 0.5 hipLift/ankleLift under a 76° shin.
    [Tooltip("Plant→Fall cycle-0: seed hipLift/ankleLift a quarter of the way from the Plant leftover toward 1 so the 76° fold lifts instead of sliding. 0 = former Plant 0.5 bias under the seeded fold.")]
    public float walkPlantFallLiftSeedFirst = 1f;
    // WALK-2 initiation is knee lift + stance drive + forward lean. The
    // triangle, friction fade, ThighHold, pelvis tilt, stance-knee
    // straighten, rock-lead CoP, ankleScale, the 55° fold cap,
    // hipBalanceScale 1, suppressWalkLean, the 76° grow ceiling, the
    // fold-hold seed and the 0.625 lift pair already survive Plant→Fall.
    // The CAP FLOOR that holds that pair at the 16–32° leftover does not.
    // Lift seed raised hipLift 0.5→0.625; ApplyFallCatchSwingThighHold
    // still floors the fade at 0.5, so at the cap leftover already sits
    // on (room ≤ 0) the extra bias is halved: liftSignal = −0.625·0.5 =
    // −0.3125 instead of −0.625·0.625. Plant leftover + hipLift 0.5
    // walked into fallSwingFallHipLiftCapCeil 32° — that is the
    // operating point, not a far target. DualLoadMinSpan still never
    // opens, liftGateBlend = 0, Fall→Reach still waits on air. Same
    // quarter as fold grow / lift seed: Lerp(0.5, 1, 0.25) = 0.625.
    // Still a Clamp01 hold against gravity at the cap, still not
    // −swingHipFlex (sci_a), still not a 1.0 floor that would keep
    // lifting into 40°. Air Fall/Reach keeps the 0.5 floor. Catch /
    // later cycles keep the envelope. Hip-unload hold is still a
    // no-op (ThighHold already owns the swing hip on Fall via fold
    // hold). Not trailing A2 and not a PD retune.
    // 0 = former 0.5 floor under the seeded 0.625 hipLift.
    [Tooltip("Plant→Fall cycle-0: seed the ThighHold cap floor a quarter of the way from 0.5 toward 1 so the 0.625 hipLift actually holds at 32°. 0 = former 0.5 floor that halves the seeded bias.")]
    public float walkPlantFallLiftFloorSeedFirst = 1f;
    // WALK-2 initiation is knee lift + stance drive + forward lean. The
    // triangle, friction fade, ThighHold, pelvis tilt, stance-knee
    // straighten, rock-lead CoP, ankleScale, the 55° fold cap,
    // hipBalanceScale 1, suppressWalkLean, the 76° grow ceiling, the
    // fold-hold seed, the 0.625 lift pair and the matching 0.625 cap
    // floor already survive Plant→Fall. The REMAINING BIAS toward 1
    // does not. Floor seed matched hipLift so leftover 32° (CapCeil,
    // room ≤ 0) holds −0.391 instead of −0.3125. That is still a Clamp01
    // hold at the cap, still not −swingHipFlex (sci_a) and not a 1.0
    // floor into 40°. DualLoadMinSpan still never opens, liftGateBlend
    // = 0, Fall→Reach still waits on air. Same quarter as fold grow /
    // lift seed / floor seed: Lerp(0.625, 1, 0.25) = 0.719 on hipLift,
    // ankleLift and the floor together so they stay matched (a floor-only
    // grow would over-hold a 0.625 bias; a pair grow without the floor
    // would re-halve at the cap). liftSignal −0.719·0.719 = −0.517.
    // Still Clamp01, still fades over the 4° deadzone past 32°, not
    // sci_a. Air Fall/Reach keeps 0.625. Catch / later cycles keep the
    // envelope. Hip-unload hold is still a no-op (ThighHold already
    // owns the swing hip on Fall via fold hold). Not trailing A2 and
    // not a PD retune.
    // 0 = former matched 0.625 trio under the 76° shin.
    [Tooltip("Plant→Fall cycle-0: grow the matched 0.625 hipLift/ankleLift/floor trio a quarter toward 1 so the 32° leftover holds −0.517 instead of −0.391. 0 = former 0.625 trio.")]
    public float walkPlantFallLiftGrowFirst = 1f;
    // WALK-2 initiation is knee lift + stance drive + forward lean. The
    // triangle, friction fade, ThighHold, pelvis tilt, stance-knee
    // straighten, rock-lead CoP, ankleScale, the 55° fold cap,
    // hipBalanceScale 1, suppressWalkLean, the 76° grow ceiling, the
    // fold-hold seed, the 0.625 lift pair, the matching 0.625 cap
    // floor and the 0.719 trio already survive Plant→Fall. The
    // REMAINING BIAS toward 1 does not. GrowFirst matched leftover 32°
    // (CapCeil, room ≤ 0) to −0.517. Remaining gap to 1.0 is 0.281 of
    // Clamp01 bias; a floor-only second grow would over-hold a 0.719
    // pair, a pair grow without the floor would re-halve at the cap.
    // DualLoadMinSpan still never opens, liftGateBlend = 0, Fall→Reach
    // still waits on air. Same quarter as fold grow / lift seed /
    // floor seed / GrowFirst: Lerp(0.719, 1, 0.25) = 0.789 on hipLift,
    // ankleLift and the floor together. liftSignal −0.789·0.789 =
    // −0.623. Still Clamp01, still fades over the 4° deadzone past
    // 32°, not sci_a. Air Fall/Reach keeps 0.719. Catch / later cycles
    // keep the envelope. Hip-unload hold is still a no-op (ThighHold
    // already owns the swing hip on Fall via fold hold). Not trailing
    // A2 and not a PD retune.
    // 0 = former matched 0.719 trio under the 76° shin.
    [Tooltip("Plant→Fall cycle-0: grow the matched 0.719 hipLift/ankleLift/floor trio a quarter toward 1 so the 32° leftover holds −0.623 instead of −0.517. 0 = former 0.719 trio.")]
    public float walkPlantFallLiftGrowMoreFirst = 1f;
    // WALK-2 initiation is knee lift + stance drive + forward lean. The
    // triangle, friction fade, ThighHold, pelvis tilt, stance-knee
    // straighten, rock-lead CoP, ankleScale, the 55° fold cap,
    // hipBalanceScale 1, suppressWalkLean, the 76° grow ceiling, the
    // fold-hold seed, the 0.625 lift pair, the matching 0.625 cap
    // floor, the 0.719 trio and the 0.789 trio already survive
    // Plant→Fall. The REMAINING BIAS toward 1 does not. GrowMoreFirst
    // matched leftover 32° (CapCeil, room ≤ 0) to −0.623. Remaining
    // gap to 1.0 is 0.211 of Clamp01 bias; a floor-only third grow
    // would over-hold a 0.789 pair, a pair grow without the floor
    // would re-halve at the cap. DualLoadMinSpan still never opens,
    // liftGateBlend = 0, Fall→Reach still waits on air. Same quarter
    // as fold grow / lift seed / floor seed / GrowFirst / GrowMore:
    // Lerp(0.789, 1, 0.25) = 0.842 on hipLift, ankleLift and the
    // floor together. liftSignal −0.842·0.842 = −0.709. Still
    // Clamp01, still fades over the 4° deadzone past 32°, not sci_a.
    // Air Fall/Reach keeps 0.789. Catch / later cycles keep the
    // envelope. Hip-unload hold is still a no-op (ThighHold already
    // owns the swing hip on Fall via fold hold). Not trailing A2 and
    // not a PD retune.
    // 0 = former matched 0.789 trio under the 76° shin.
    [Tooltip("Plant→Fall cycle-0: grow the matched 0.789 hipLift/ankleLift/floor trio a quarter toward 1 so the 32° leftover holds −0.709 instead of −0.623. 0 = former 0.789 trio.")]
    public float walkPlantFallLiftGrowYetFirst = 1f;
    // WALK-2 initiation is knee lift + stance drive + forward lean. The
    // triangle, friction fade, ThighHold, pelvis tilt, stance-knee
    // straighten, rock-lead CoP, ankleScale, the 55° fold cap,
    // hipBalanceScale 1, suppressWalkLean, the 76° grow ceiling, the
    // fold-hold seed, the 0.625 lift pair, the matching 0.625 cap
    // floor, the 0.719 trio, the 0.789 trio and the 0.842 trio already
    // survive Plant→Fall. The REMAINING BIAS toward 1 does not.
    // GrowYetFirst matched leftover 32° (CapCeil, room ≤ 0) to
    // −0.709. Remaining gap to 1.0 is 0.158 of Clamp01 bias; a
    // floor-only fourth grow would over-hold a 0.842 pair, a pair
    // grow without the floor would re-halve at the cap.
    // DualLoadMinSpan still never opens, liftGateBlend = 0, Fall→Reach
    // still waits on air. Same quarter as fold grow / lift seed /
    // floor seed / GrowFirst / GrowMore / GrowYet: Lerp(0.842, 1,
    // 0.25) = 0.882 on hipLift, ankleLift and the floor together.
    // liftSignal −0.882·0.882 = −0.778. Still Clamp01, still fades
    // over the 4° deadzone past 32°, not sci_a. Air Fall/Reach keeps
    // 0.842. Catch / later cycles keep the envelope. Hip-unload hold
    // is still a no-op (ThighHold already owns the swing hip on Fall
    // via fold hold). Not trailing A2 and not a PD retune.
    // 0 = former matched 0.842 trio under the 76° shin.
    [Tooltip("Plant→Fall cycle-0: grow the matched 0.842 hipLift/ankleLift/floor trio a quarter toward 1 so the 32° leftover holds −0.778 instead of −0.709. 0 = former 0.842 trio.")]
    public float walkPlantFallLiftGrowEvenFirst = 1f;
    // WALK-2 initiation is knee lift + stance drive + forward lean. The
    // triangle, friction fade, ThighHold, pelvis tilt, stance-knee
    // straighten, rock-lead CoP, ankleScale, the 55° fold cap,
    // hipBalanceScale 1, suppressWalkLean, the 76° grow ceiling, the
    // fold-hold seed, the 0.625 lift pair, the matching 0.625 cap
    // floor, the 0.719 trio, the 0.789 trio, the 0.842 trio and the
    // 0.882 trio already survive Plant→Fall. The REMAINING BIAS
    // toward 1 does not. GrowEvenFirst matched leftover 32°
    // (CapCeil, room ≤ 0) to −0.778. Remaining gap to 1.0 is 0.118
    // of Clamp01 bias; a floor-only fifth grow would over-hold a
    // 0.882 pair, a pair grow without the floor would re-halve at
    // the cap. DualLoadMinSpan still never opens, liftGateBlend =
    // 0, Fall→Reach still waits on air. Same quarter as fold grow /
    // lift seed / floor seed / GrowFirst / GrowMore / GrowYet /
    // GrowEven: Lerp(0.882, 1, 0.25) = 0.912 on hipLift, ankleLift
    // and the floor together. liftSignal −0.912·0.912 = −0.831.
    // Still Clamp01, still fades over the 4° deadzone past 32°, not
    // sci_a. Air Fall/Reach keeps 0.882. Catch / later cycles keep
    // the envelope. Hip-unload hold is still a no-op (ThighHold
    // already owns the swing hip on Fall via fold hold). Not
    // trailing A2 and not a PD retune.
    // 0 = former matched 0.882 trio under the 76° shin.
    [Tooltip("Plant→Fall cycle-0: grow the matched 0.882 hipLift/ankleLift/floor trio a quarter toward 1 so the 32° leftover holds −0.831 instead of −0.778. 0 = former 0.882 trio.")]
    public float walkPlantFallLiftGrowStillFirst = 1f;
    // WALK-2 initiation is knee lift + stance drive + forward lean. The
    // triangle, friction fade, ThighHold, pelvis tilt, stance-knee
    // straighten, rock-lead CoP, ankleScale, the 55° fold cap,
    // hipBalanceScale 1, suppressWalkLean, the 76° grow ceiling, the
    // fold-hold seed, the 0.625 lift pair, the matching 0.625 cap
    // floor, the 0.719 trio, the 0.789 trio, the 0.842 trio, the
    // 0.882 trio and the 0.912 trio already survive Plant→Fall. The
    // REMAINING BIAS toward 1 does not. GrowStillFirst matched leftover
    // 32° (CapCeil, room ≤ 0) to −0.831. Remaining gap to 1.0 is 0.088
    // of Clamp01 bias; a floor-only sixth grow would over-hold a 0.912
    // pair, a pair grow without the floor would re-halve at the cap.
    // DualLoadMinSpan still never opens, liftGateBlend = 0, Fall→Reach
    // still waits on air. Same quarter as fold grow / lift seed /
    // floor seed / GrowFirst / GrowMore / GrowYet / GrowEven /
    // GrowStill: Lerp(0.912, 1, 0.25) = 0.934 on hipLift, ankleLift
    // and the floor together. liftSignal −0.934·0.934 = −0.872.
    // Still Clamp01, still fades over the 4° deadzone past 32°, not
    // sci_a. Air Fall/Reach keeps 0.912. Catch / later cycles keep
    // the envelope. Hip-unload hold is still a no-op (ThighHold
    // already owns the swing hip on Fall via fold hold). Not
    // trailing A2 and not a PD retune.
    // 0 = former matched 0.912 trio under the 76° shin.
    [Tooltip("Plant→Fall cycle-0: grow the matched 0.912 hipLift/ankleLift/floor trio a quarter toward 1 so the 32° leftover holds −0.872 instead of −0.831. 0 = former 0.912 trio.")]
    public float walkPlantFallLiftGrowAgainFirst = 1f;
    // WALK-2 initiation is knee lift + stance drive + forward lean. The
    // triangle, friction fade, ThighHold, pelvis tilt, stance-knee
    // straighten, rock-lead CoP, ankleScale, the 55° fold cap,
    // hipBalanceScale 1, suppressWalkLean, the 76° grow ceiling, the
    // fold-hold seed, the 0.625 lift pair, the matching 0.625 cap
    // floor, the 0.719 trio, the 0.789 trio, the 0.842 trio, the
    // 0.882 trio, the 0.912 trio and the 0.934 trio already survive
    // Plant→Fall. The REMAINING BIAS toward 1 does not. GrowAgainFirst
    // matched leftover 32° (CapCeil, room ≤ 0) to −0.872. Remaining
    // gap to 1.0 is 0.066 of Clamp01 bias; a floor-only seventh grow
    // would over-hold a 0.934 pair, a pair grow without the floor
    // would re-halve at the cap. DualLoadMinSpan still never opens,
    // liftGateBlend = 0, Fall→Reach still waits on air. Same quarter
    // as fold grow / lift seed / floor seed / GrowFirst / GrowMore /
    // GrowYet / GrowEven / GrowStill / GrowAgain: Lerp(0.934, 1,
    // 0.25) = 0.951 on hipLift, ankleLift and the floor together.
    // liftSignal −0.951·0.951 = −0.904. Still Clamp01, still fades
    // over the 4° deadzone past 32°, not sci_a. Air Fall/Reach keeps
    // 0.934. Catch / later cycles keep the envelope. Hip-unload hold
    // is still a no-op (ThighHold already owns the swing hip on Fall
    // via fold hold). Not trailing A2 and not a PD retune.
    // 0 = former matched 0.934 trio under the 76° shin.
    [Tooltip("Plant→Fall cycle-0: grow the matched 0.934 hipLift/ankleLift/floor trio a quarter toward 1 so the 32° leftover holds −0.904 instead of −0.872. 0 = former 0.934 trio.")]
    public float walkPlantFallLiftGrowAlsoFirst = 1f;
    // WALK-2 initiation is knee lift + stance drive + forward lean. The
    // triangle, friction fade, ThighHold, pelvis tilt, stance-knee
    // straighten, rock-lead CoP, ankleScale, the 55° fold cap,
    // hipBalanceScale 1, suppressWalkLean, the 76° grow ceiling, the
    // fold-hold seed, the 0.625 lift pair, the matching 0.625 cap
    // floor, the 0.719 trio, the 0.789 trio, the 0.842 trio, the
    // 0.882 trio, the 0.912 trio, the 0.934 trio and the 0.951 trio
    // already survive Plant→Fall. The REMAINING BIAS toward 1 does
    // not. GrowAlsoFirst matched leftover 32° (CapCeil, room ≤ 0) to
    // −0.904. Remaining gap to 1.0 is 0.049 of Clamp01 bias; a
    // floor-only eighth grow would over-hold a 0.951 pair, a pair
    // grow without the floor would re-halve at the cap.
    // DualLoadMinSpan still never opens, liftGateBlend = 0, Fall→Reach
    // still waits on air. Same quarter as fold grow / lift seed /
    // floor seed / GrowFirst / GrowMore / GrowYet / GrowEven /
    // GrowStill / GrowAgain / GrowAlso: Lerp(0.951, 1, 0.25) = 0.963
    // on hipLift, ankleLift and the floor together. liftSignal
    // −0.963·0.963 = −0.927. Still Clamp01, still fades over the 4°
    // deadzone past 32°, not sci_a. Air Fall/Reach keeps 0.951.
    // Catch / later cycles keep the envelope. Hip-unload hold is
    // still a no-op (ThighHold already owns the swing hip on Fall
    // via fold hold). Not trailing A2 and not a PD retune.
    // 0 = former matched 0.951 trio under the 76° shin.
    [Tooltip("Plant→Fall cycle-0: grow the matched 0.951 hipLift/ankleLift/floor trio a quarter toward 1 so the 32° leftover holds −0.927 instead of −0.904. 0 = former 0.951 trio.")]
    public float walkPlantFallLiftGrowExtraFirst = 1f;
    // WALK-2 initiation is knee lift + stance drive + forward lean. The
    // triangle, friction fade, ThighHold, pelvis tilt, stance-knee
    // straighten, rock-lead CoP, ankleScale, the 55° fold cap,
    // hipBalanceScale 1, suppressWalkLean, the 76° grow ceiling, the
    // fold-hold seed, the 0.625 lift pair, the matching 0.625 cap
    // floor, the 0.719 trio, the 0.789 trio, the 0.842 trio, the
    // 0.882 trio, the 0.912 trio, the 0.934 trio, the 0.951 trio
    // and the 0.963 trio already survive Plant→Fall. The REMAINING
    // BIAS toward 1 does not. GrowExtraFirst matched leftover 32°
    // (CapCeil, room ≤ 0) to −0.927. Remaining gap to 1.0 is 0.037
    // of Clamp01 bias; a floor-only ninth grow would over-hold a
    // 0.963 pair, a pair grow without the floor would re-halve at
    // the cap. DualLoadMinSpan still never opens, liftGateBlend = 0,
    // Fall→Reach still waits on air. Same quarter as fold grow /
    // lift seed / floor seed / GrowFirst / GrowMore / GrowYet /
    // GrowEven / GrowStill / GrowAgain / GrowAlso / GrowExtra:
    // Lerp(0.963, 1, 0.25) = 0.972 on hipLift, ankleLift and the floor
    // together. liftSignal −0.972·0.972 = −0.945. Still Clamp01,
    // still fades over the 4° deadzone past 32°, not sci_a. Air
    // Fall/Reach keeps 0.963. Catch / later cycles keep the envelope.
    // Hip-unload hold is still a no-op (ThighHold already owns the
    // swing hip on Fall via fold hold). Not trailing A2 and not a
    // PD retune.
    // 0 = former matched 0.963 trio under the 76° shin.
    [Tooltip("Plant→Fall cycle-0: grow the matched 0.963 hipLift/ankleLift/floor trio a quarter toward 1 so the 32° leftover holds −0.945 instead of −0.927. 0 = former 0.963 trio.")]
    public float walkPlantFallLiftGrowPlusFirst = 1f;
    // WALK-2 initiation is knee lift + stance drive + forward lean. The
    // triangle, friction fade, ThighHold, pelvis tilt, stance-knee
    // straighten, rock-lead CoP, ankleScale, the 55° fold cap,
    // hipBalanceScale 1, suppressWalkLean, the 76° grow ceiling, the
    // fold-hold seed, the 0.625 lift pair, the matching 0.625 cap
    // floor, the 0.719…0.972 trio and PlusFirst already survive
    // Plant→Fall. The CAP leftover does not. PlusFirst matched leftover
    // 32° (CapCeil, room ≤ 0) to −0.945. Extra hipLift at room ≤ 0
    // does not raise past CapCeil — it only holds 32° harder. The
    // couple clamp then Min's hipLiftCapDeg back to
    // coupleCapCeilingDeg 32°, so a Fall CapCeil write of 34° before
    // that Min is a no-op. DualLoadMinSpan still never opens,
    // liftGateBlend = 0, Fall→Reach still waits on air. Same quarter
    // as fold grow: Lerp(32, 40, 0.25) = 34° after the couple clamp.
    // At a 32° thigh, room = 2° (~1.5 cm of a 0.43 m thigh). fade
    // stays Max(2/38, 0.972) = 0.972 so liftSignal stays −0.945 until
    // the thigh walks; still 6° short of sci_a 40°, still not
    // −swingHipFlex. Deadzone still 4° past the new cap. Air Fall/
    // Reach keeps 32°. Catch / later cycles keep the envelope.
    // Hip-unload hold is still a no-op (ThighHold already owns the
    // swing hip on Fall via fold hold). Not trailing A2 and not a
    // PD retune.
    // 0 = former 32° CapCeil leftover under the 0.972 trio.
    [Tooltip("Plant→Fall cycle-0: grow the 32° hip-lift cap a quarter toward 40° so leftover at CapCeil has 2° of room. 0 = former 32° leftover.")]
    public float walkPlantFallLiftCapGrowFirst = 1f;
    // WALK-2 initiation is knee lift + stance drive + forward lean. The
    // triangle, friction fade, ThighHold, pelvis tilt, stance-knee
    // straighten, rock-lead CoP, ankleScale, the 55° fold cap,
    // hipBalanceScale 1, suppressWalkLean, the 76° grow ceiling, the
    // fold-hold seed, the 0.625 lift pair, the matching 0.625 cap
    // floor, the 0.719…0.972 trio, PlusFirst and CapGrowFirst already
    // survive Plant→Fall. The remaining 6° to sci_a does not.
    // CapGrowFirst wrote leftover 34° (Lerp 32→40 at 0.25) after the
    // couple Min, so a 32° thigh has 2° of room (~1.5 cm of a 0.43 m
    // thigh). fade stays Max(2/38, 0.972) = 0.972; liftSignal stays
    // −0.945 until the thigh walks onto 34°, then room ≤ 0 again.
    // Extra hipLift at that leftover still cannot raise past the cap.
    // DualLoadMinSpan still never opens, liftGateBlend = 0, Fall→Reach
    // still waits on air. Same quarter as fold grow: Lerp(34, 40,
    // 0.25) = 35.5°. At a 34° thigh, room = 1.5° (~1.1 cm). fade stays
    // Max(1.5/39.5, 0.972) so liftSignal stays −0.945 until the thigh
    // walks; still 4.5° short of sci_a 40°, still not −swingHipFlex.
    // Deadzone still 4° past the new cap. Air Fall/Reach keeps 34°.
    // Catch / later cycles keep the envelope. Hip-unload hold is still
    // a no-op (ThighHold already owns the swing hip on Fall via fold
    // hold). Not trailing A2 and not a PD retune.
    // 0 = former 34° CapGrowFirst leftover under the 0.972 trio.
    [Tooltip("Plant→Fall cycle-0: grow the 34° hip-lift cap a quarter toward 40° so leftover at CapGrowFirst has 1.5° of room. 0 = former 34° leftover.")]
    public float walkPlantFallLiftCapGrowMoreFirst = 1f;
    // WALK-2 initiation is knee lift + stance drive + forward lean. The
    // triangle, friction fade, ThighHold, pelvis tilt, stance-knee
    // straighten, rock-lead CoP, ankleScale, the 55° fold cap,
    // hipBalanceScale 1, suppressWalkLean, the 76° grow ceiling, the
    // fold-hold seed, the 0.625 lift pair, the matching 0.625 cap
    // floor, the 0.719…0.972 trio, PlusFirst, CapGrowFirst and
    // CapGrowMoreFirst already survive Plant→Fall. The remaining 4.5°
    // to sci_a does not. CapGrowMoreFirst wrote leftover 35.5° (Lerp
    // 34→40 at 0.25) after the couple Min, so a 34° thigh has 1.5° of
    // room (~1.1 cm of a 0.43 m thigh). fade stays Max(1.5/39.5,
    // 0.972) = 0.972; liftSignal stays −0.945 until the thigh walks
    // onto 35.5°, then room ≤ 0 again. Extra hipLift at that leftover
    // still cannot raise past the cap. DualLoadMinSpan still never
    // opens, liftGateBlend = 0, Fall→Reach still waits on air. Same
    // quarter as fold grow: Lerp(35.5, 40, 0.25) = 36.625°. At a 35.5°
    // thigh, room = 1.125° (~0.84 cm). fade stays Max(1.125/40.625,
    // 0.972) so liftSignal stays −0.945 until the thigh walks; still
    // 3.375° short of sci_a 40°, still not −swingHipFlex. Deadzone
    // still 4° past the new cap. Air Fall/Reach keeps 35.5°. Catch /
    // later cycles keep the envelope. Hip-unload hold is still a
    // no-op (ThighHold already owns the swing hip on Fall via fold
    // hold). Not trailing A2 and not a PD retune.
    // 0 = former 35.5° CapGrowMoreFirst leftover under the 0.972 trio.
    [Tooltip("Plant→Fall cycle-0: grow the 35.5° hip-lift cap a quarter toward 40° so leftover at CapGrowMoreFirst has 1.125° of room. 0 = former 35.5° leftover.")]
    public float walkPlantFallLiftCapGrowYetFirst = 1f;
    // WALK-2 initiation is knee lift + stance drive + forward lean. The
    // triangle, friction fade, ThighHold, pelvis tilt, stance-knee
    // straighten, rock-lead CoP, ankleScale, the 55° fold cap,
    // hipBalanceScale 1, suppressWalkLean, the 76° grow ceiling, the
    // fold-hold seed, the 0.625 lift pair, the matching 0.625 cap
    // floor, the 0.719…0.972 trio, PlusFirst, CapGrowFirst,
    // CapGrowMoreFirst and CapGrowYetFirst already survive Plant→Fall.
    // The remaining 3.375° to sci_a does not. CapGrowYetFirst wrote
    // leftover 36.625° (Lerp 35.5→40 at 0.25) after the couple Min, so
    // a 35.5° thigh has 1.125° of room (~0.84 cm of a 0.43 m thigh).
    // fade stays Max(1.125/40.625, 0.972) = 0.972; liftSignal stays
    // −0.945 until the thigh walks onto 36.625°, then room ≤ 0 again.
    // Extra hipLift at that leftover still cannot raise past the cap.
    // DualLoadMinSpan still never opens, liftGateBlend = 0, Fall→Reach
    // still waits on air. Same quarter as fold grow: Lerp(36.625, 40,
    // 0.25) = 37.46875°. At a 36.625° thigh, room = 0.84375° (~0.63 cm).
    // fade stays Max(0.84375/41.46875, 0.972) so liftSignal stays
    // −0.945 until the thigh walks; still 2.53125° short of sci_a 40°,
    // still not −swingHipFlex. Deadzone still 4° past the new cap. Air
    // Fall/Reach keeps 36.625°. Catch / later cycles keep the envelope.
    // Hip-unload hold is still a no-op (ThighHold already owns the
    // swing hip on Fall via fold hold). Not trailing A2 and not a
    // PD retune.
    // 0 = former 36.625° CapGrowYetFirst leftover under the 0.972 trio.
    [Tooltip("Plant→Fall cycle-0: grow the 36.625° hip-lift cap a quarter toward 40° so leftover at CapGrowYetFirst has 0.84375° of room. 0 = former 36.625° leftover.")]
    public float walkPlantFallLiftCapGrowEvenFirst = 1f;
    // WALK-2 initiation is knee lift + stance drive + forward lean. The
    // triangle, friction fade, ThighHold, pelvis tilt, stance-knee
    // straighten, rock-lead CoP, ankleScale, the 55° fold cap,
    // hipBalanceScale 1, suppressWalkLean, the 76° grow ceiling, the
    // fold-hold seed, the 0.625 lift pair, the matching 0.625 cap
    // floor, the 0.719…0.972 trio, PlusFirst, CapGrowFirst,
    // CapGrowMoreFirst, CapGrowYetFirst and CapGrowEvenFirst already
    // survive Plant→Fall. The remaining 2.53125° to sci_a does not.
    // CapGrowEvenFirst wrote leftover 37.46875° (Lerp 36.625→40 at
    // 0.25) after the couple Min, so a 36.625° thigh has 0.84375° of
    // room (~0.63 cm of a 0.43 m thigh). fade stays Max(0.84375/
    // 41.46875, 0.972) = 0.972; liftSignal stays −0.945 until the
    // thigh walks onto 37.46875°, then room ≤ 0 again. Extra hipLift
    // at that leftover still cannot raise past the cap. DualLoadMinSpan
    // still never opens, liftGateBlend = 0, Fall→Reach still waits on
    // air. Same quarter as fold grow: Lerp(37.46875, 40, 0.25) =
    // 38.1015625°. At a 37.46875° thigh, room = 0.6328125° (~0.47 cm).
    // fade stays Max(0.6328125/42.1015625, 0.972) so liftSignal stays
    // −0.945 until the thigh walks; still 1.8984375° short of sci_a
    // 40°, still not −swingHipFlex. Deadzone still 4° past the new
    // cap. Air Fall/Reach keeps 37.46875°. Catch / later cycles keep
    // the envelope. Hip-unload hold is still a no-op (ThighHold
    // already owns the swing hip on Fall via fold hold). Not trailing
    // A2 and not a PD retune.
    // 0 = former 37.46875° CapGrowEvenFirst leftover under the 0.972
    // trio.
    [Tooltip("Plant→Fall cycle-0: grow the 37.46875° hip-lift cap a quarter toward 40° so leftover at CapGrowEvenFirst has 0.6328125° of room. 0 = former 37.46875° leftover.")]
    public float walkPlantFallLiftCapGrowStillFirst = 1f;
    // WALK-2 initiation is knee lift + stance drive + forward lean. The
    // triangle, friction fade, ThighHold, pelvis tilt, stance-knee
    // straighten, rock-lead CoP, ankleScale, the 55° fold cap,
    // hipBalanceScale 1, suppressWalkLean, the 76° grow ceiling, the
    // fold-hold seed, the 0.625 lift pair, the matching 0.625 cap
    // floor, the 0.719…0.972 trio, PlusFirst, CapGrowFirst,
    // CapGrowMoreFirst, CapGrowYetFirst, CapGrowEvenFirst and
    // CapGrowStillFirst already survive Plant→Fall. The remaining
    // 1.8984375° to sci_a does not. CapGrowStillFirst wrote leftover
    // 38.1015625° (Lerp 37.46875→40 at 0.25) after the couple Min, so
    // a 37.46875° thigh has 0.6328125° of room (~0.47 cm of a 0.43 m
    // thigh). fade stays Max(0.6328125/42.1015625, 0.972) = 0.972;
    // liftSignal stays −0.945 until the thigh walks onto 38.1015625°,
    // then room ≤ 0 again. Extra hipLift at that leftover still cannot
    // raise past the cap. DualLoadMinSpan still never opens,
    // liftGateBlend = 0, Fall→Reach still waits on air. Same quarter
    // as fold grow: Lerp(38.1015625, 40, 0.25) = 38.576171875°. At a
    // 38.1015625° thigh, room = 0.474609375° (~0.36 cm). fade stays
    // Max(0.474609375/42.576171875, 0.972) so liftSignal stays −0.945
    // until the thigh walks; still 1.423828125° short of sci_a 40°,
    // still not −swingHipFlex. Deadzone still 4° past the new cap. Air
    // Fall/Reach keeps 38.1015625°. Catch / later cycles keep the
    // envelope. Hip-unload hold is still a no-op (ThighHold already
    // owns the swing hip on Fall via fold hold). Not trailing A2 and
    // not a PD retune.
    // 0 = former 38.1015625° CapGrowStillFirst leftover under the
    // 0.972 trio.
    [Tooltip("Plant→Fall cycle-0: grow the 38.1015625° hip-lift cap a quarter toward 40° so leftover at CapGrowStillFirst has 0.474609375° of room. 0 = former 38.1015625° leftover.")]
    public float walkPlantFallLiftCapGrowAgainFirst = 1f;
    // WALK-2 initiation is knee lift + stance drive + forward lean. The
    // triangle, friction fade, ThighHold, pelvis tilt, stance-knee
    // straighten, rock-lead CoP, ankleScale, the 55° fold cap,
    // hipBalanceScale 1, suppressWalkLean, the 76° grow ceiling, the
    // fold-hold seed, the 0.625 lift pair, the matching 0.625 cap
    // floor, the 0.719…0.972 trio, PlusFirst, CapGrowFirst,
    // CapGrowMoreFirst, CapGrowYetFirst, CapGrowEvenFirst,
    // CapGrowStillFirst and CapGrowAgainFirst already survive
    // Plant→Fall. The remaining 1.423828125° to sci_a does not.
    // CapGrowAgainFirst wrote leftover 38.576171875° (Lerp
    // 38.1015625→40 at 0.25) after the couple Min, so a 38.1015625°
    // thigh has 0.474609375° of room (~0.36 cm of a 0.43 m thigh).
    // fade stays Max(0.474609375/42.576171875, 0.972) = 0.972;
    // liftSignal stays −0.945 until the thigh walks onto
    // 38.576171875°, then room ≤ 0 again. Extra hipLift at that
    // leftover still cannot raise past the cap. DualLoadMinSpan
    // still never opens, liftGateBlend = 0, Fall→Reach still waits
    // on air. Same quarter as fold grow: Lerp(38.576171875, 40,
    // 0.25) = 38.93212890625°. At a 38.576171875° thigh, room =
    // 0.35595703125° (~0.27 cm). fade stays Max(0.35595703125/
    // 42.93212890625, 0.972) so liftSignal stays −0.945 until the
    // thigh walks; still 1.06787109375° short of sci_a 40°, still
    // not −swingHipFlex. Deadzone still 4° past the new cap. Air
    // Fall/Reach keeps 38.576171875°. Catch / later cycles keep
    // the envelope. Hip-unload hold is still a no-op (ThighHold
    // already owns the swing hip on Fall via fold hold). Not
    // trailing A2 and not a PD retune.
    // 0 = former 38.576171875° CapGrowAgainFirst leftover under the
    // 0.972 trio.
    [Tooltip("Plant→Fall cycle-0: grow the 38.576171875° hip-lift cap a quarter toward 40° so leftover at CapGrowAgainFirst has 0.35595703125° of room. 0 = former 38.576171875° leftover.")]
    public float walkPlantFallLiftCapGrowAlsoFirst = 1f;
    // WALK-2 initiation is knee lift + stance drive + forward lean. The
    // triangle, friction fade, ThighHold, pelvis tilt, stance-knee
    // straighten, rock-lead CoP, ankleScale, the 55° fold cap,
    // hipBalanceScale 1, suppressWalkLean, the 76° grow ceiling, the
    // fold-hold seed, the 0.625 lift pair, the matching 0.625 cap
    // floor, the 0.719…0.972 trio, PlusFirst, CapGrowFirst,
    // CapGrowMoreFirst, CapGrowYetFirst, CapGrowEvenFirst,
    // CapGrowStillFirst, CapGrowAgainFirst and CapGrowAlsoFirst already
    // survive Plant→Fall. The remaining 1.06787109375° to sci_a does
    // not.
    // CapGrowAlsoFirst wrote leftover 38.93212890625° (Lerp
    // 38.576171875→40 at 0.25) after the couple Min, so a 38.576171875°
    // thigh has 0.35595703125° of room (~0.27 cm of a 0.43 m thigh).
    // fade stays Max(0.35595703125/42.93212890625, 0.972) = 0.972;
    // liftSignal stays −0.945 until the thigh walks onto
    // 38.93212890625°, then room ≤ 0 again. Extra hipLift at that
    // leftover still cannot raise past the cap. DualLoadMinSpan
    // still never opens, liftGateBlend = 0, Fall→Reach still waits
    // on air. Same quarter as fold grow: Lerp(38.93212890625, 40,
    // 0.25) = 39.1990966796875°. At a 38.93212890625° thigh, room =
    // 0.2669677734375° (~0.20 cm). fade stays Max(0.2669677734375/
    // 43.1990966796875, 0.972) so liftSignal stays −0.945 until the
    // thigh walks; still 0.8009033203125° short of sci_a 40°, still
    // not −swingHipFlex. Deadzone still 4° past the new cap. Air
    // Fall/Reach keeps 38.93212890625°. Catch / later cycles keep
    // the envelope. Hip-unload hold is still a no-op (ThighHold
    // already owns the swing hip on Fall via fold hold). Not
    // trailing A2 and not a PD retune.
    // 0 = former 38.93212890625° CapGrowAlsoFirst leftover under the
    // 0.972 trio.
    [Tooltip("Plant→Fall cycle-0: grow the 38.93212890625° hip-lift cap a quarter toward 40° so leftover at CapGrowAlsoFirst has 0.2669677734375° of room. 0 = former 38.93212890625° leftover.")]
    public float walkPlantFallLiftCapGrowExtraFirst = 1f;
    // WALK-2 initiation is knee lift + stance drive + forward lean. The
    // triangle, friction fade, ThighHold, pelvis tilt, stance-knee
    // straighten, rock-lead CoP, ankleScale, the 55° fold cap,
    // hipBalanceScale 1, suppressWalkLean, the 76° grow ceiling, the
    // fold-hold seed, the 0.625 lift pair, the matching 0.625 cap
    // floor, the 0.719…0.972 trio, PlusFirst, CapGrowFirst,
    // CapGrowMoreFirst, CapGrowYetFirst, CapGrowEvenFirst,
    // CapGrowStillFirst, CapGrowAgainFirst, CapGrowAlsoFirst and
    // CapGrowExtraFirst already survive Plant→Fall. The remaining
    // 0.8009033203125° to sci_a does not.
    // CapGrowExtraFirst wrote leftover 39.1990966796875° (Lerp
    // 38.93212890625→40 at 0.25) after the couple Min, so a
    // 38.93212890625° thigh has 0.2669677734375° of room (~0.20 cm
    // of a 0.43 m thigh). fade stays Max(0.2669677734375/
    // 43.1990966796875, 0.972) = 0.972; liftSignal stays −0.945
    // until the thigh walks onto 39.1990966796875°, then room ≤ 0
    // again. Extra hipLift at that leftover still cannot raise past
    // the cap. DualLoadMinSpan still never opens, liftGateBlend = 0,
    // Fall→Reach still waits on air. Same quarter as fold grow:
    // Lerp(39.1990966796875, 40, 0.25) = 39.399322509765625°. At a
    // 39.1990966796875° thigh, room = 0.200225830078125° (~0.15 cm).
    // fade stays Max(0.200225830078125/43.399322509765625, 0.972) so
    // liftSignal stays −0.945 until the thigh walks; still
    // 0.600677490234375° short of sci_a 40°, still not
    // −swingHipFlex. Deadzone still 4° past the new cap. Air
    // Fall/Reach keeps 39.1990966796875°. Catch / later cycles keep
    // the envelope. Hip-unload hold is still a no-op (ThighHold
    // already owns the swing hip on Fall via fold hold). Not
    // trailing A2 and not a PD retune.
    // 0 = former 39.1990966796875° CapGrowExtraFirst leftover under
    // the 0.972 trio.
    [Tooltip("Plant→Fall cycle-0: grow the 39.1990966796875° hip-lift cap a quarter toward 40° so leftover at CapGrowExtraFirst has 0.200225830078125° of room. 0 = former 39.1990966796875° leftover.")]
    public float walkPlantFallLiftCapGrowPlusFirst = 1f;
    // WALK-2 initiation is knee lift + stance drive + forward lean. The
    // triangle, friction fade, ThighHold, pelvis tilt, stance-knee
    // straighten, rock-lead CoP, ankleScale, the 55° fold cap,
    // hipBalanceScale 1, suppressWalkLean, the 76° grow ceiling, the
    // fold-hold seed, the 0.625 lift pair, the matching 0.625 cap
    // floor, the 0.719…0.972 trio, PlusFirst, CapGrowFirst,
    // CapGrowMoreFirst, CapGrowYetFirst, CapGrowEvenFirst,
    // CapGrowStillFirst, CapGrowAgainFirst, CapGrowAlsoFirst,
    // CapGrowExtraFirst and CapGrowPlusFirst already survive
    // Plant→Fall. The remaining 0.600677490234375° to sci_a does not.
    // CapGrowPlusFirst wrote leftover 39.399322509765625° (Lerp
    // 39.1990966796875→40 at 0.25) after the couple Min, so a
    // 39.1990966796875° thigh has 0.200225830078125° of room (~0.15 cm
    // of a 0.43 m thigh). fade stays Max(0.200225830078125/
    // 43.399322509765625, 0.972) = 0.972; liftSignal stays −0.945
    // until the thigh walks onto 39.399322509765625°, then room ≤ 0
    // again. Extra hipLift at that leftover still cannot raise past
    // the cap. DualLoadMinSpan still never opens, liftGateBlend = 0,
    // Fall→Reach still waits on air. Same quarter as fold grow:
    // Lerp(39.399322509765625, 40, 0.25) = 39.54949188232421875°. At a
    // 39.399322509765625° thigh, room = 0.15016937255859375° (~0.11 cm).
    // fade stays Max(0.15016937255859375/43.54949188232421875, 0.972)
    // so liftSignal stays −0.945 until the thigh walks; still
    // 0.45050811767578125° short of sci_a 40°, still not
    // −swingHipFlex. Deadzone still 4° past the new cap. Air
    // Fall/Reach keeps 39.399322509765625°. Catch / later cycles keep
    // the envelope. Hip-unload hold is still a no-op (ThighHold
    // already owns the swing hip on Fall via fold hold). Not
    // trailing A2 and not a PD retune.
    // 0 = former 39.399322509765625° CapGrowPlusFirst leftover under
    // the 0.972 trio.
    [Tooltip("Plant→Fall cycle-0: grow the 39.399322509765625° hip-lift cap a quarter toward 40° so leftover at CapGrowPlusFirst has 0.15016937255859375° of room. 0 = former 39.399322509765625° leftover.")]
    public float walkPlantFallLiftCapGrowFurtherFirst = 1f;
    // WALK-2 initiation is knee lift + stance drive + forward lean. The
    // triangle, friction fade, ThighHold, pelvis tilt, stance-knee
    // straighten, rock-lead CoP, ankleScale, the 55° fold cap,
    // hipBalanceScale 1, suppressWalkLean, the 76° grow ceiling, the
    // fold-hold seed, the 0.625 lift pair, the matching 0.625 cap
    // floor, the 0.719…0.972 trio, PlusFirst, CapGrowFirst,
    // CapGrowMoreFirst, CapGrowYetFirst, CapGrowEvenFirst,
    // CapGrowStillFirst, CapGrowAgainFirst, CapGrowAlsoFirst,
    // CapGrowExtraFirst, CapGrowPlusFirst and CapGrowFurtherFirst
    // already survive Plant→Fall. The remaining 0.45050811767578125°
    // to sci_a does not.
    // CapGrowFurtherFirst wrote leftover 39.54949188232421875° (Lerp
    // 39.399322509765625→40 at 0.25) after the couple Min, so a
    // 39.399322509765625° thigh has 0.15016937255859375° of room
    // (~0.11 cm of a 0.43 m thigh). fade stays Max(0.15016937255859375/
    // 43.54949188232421875, 0.972) = 0.972; liftSignal stays −0.945
    // until the thigh walks onto 39.54949188232421875°, then room ≤ 0
    // again. Extra hipLift at that leftover still cannot raise past
    // the cap. DualLoadMinSpan still never opens, liftGateBlend = 0,
    // Fall→Reach still waits on air. Same quarter as fold grow:
    // Lerp(39.54949188232421875, 40, 0.25) = 39.6621189117431640625°.
    // At a 39.54949188232421875° thigh, room = 0.1126270294189453125°
    // (~0.084 cm). fade stays Max(0.1126270294189453125/
    // 43.6621189117431640625, 0.972) so liftSignal stays −0.945 until
    // the thigh walks; still 0.3378810882568359375° short of sci_a
    // 40°, still not −swingHipFlex. Deadzone still 4° past the new
    // cap. Air Fall/Reach keeps 39.54949188232421875°. Catch / later
    // cycles keep the envelope. Hip-unload hold is still a no-op
    // (ThighHold already owns the swing hip on Fall via fold hold).
    // Not trailing A2 and not a PD retune.
    // 0 = former 39.54949188232421875° CapGrowFurtherFirst leftover
    // under the 0.972 trio.
    [Tooltip("Plant→Fall cycle-0: grow the 39.54949188232421875° hip-lift cap a quarter toward 40° so leftover at CapGrowFurtherFirst has 0.1126270294189453125° of room. 0 = former 39.54949188232421875° leftover.")]
    public float walkPlantFallLiftCapGrowOnwardFirst = 1f;
    // WALK-2 initiation is knee lift + stance drive + forward lean. The
    // triangle, friction fade, ThighHold, pelvis tilt, stance-knee
    // straighten, rock-lead CoP, ankleScale, the 55° fold cap,
    // hipBalanceScale 1, suppressWalkLean, the 76° grow ceiling, the
    // fold-hold seed, the 0.625 lift pair, the matching 0.625 cap
    // floor, the 0.719…0.972 trio, PlusFirst, CapGrowFirst,
    // CapGrowMoreFirst, CapGrowYetFirst, CapGrowEvenFirst,
    // CapGrowStillFirst, CapGrowAgainFirst, CapGrowAlsoFirst,
    // CapGrowExtraFirst, CapGrowPlusFirst, CapGrowFurtherFirst and
    // CapGrowOnwardFirst already survive Plant→Fall. The remaining
    // 0.3378810882568359375° to sci_a does not.
    // CapGrowOnwardFirst wrote leftover 39.6621189117431640625° (Lerp
    // 39.54949188232421875→40 at 0.25) after the couple Min, so a
    // 39.54949188232421875° thigh has 0.1126270294189453125° of room
    // (~0.084 cm of a 0.43 m thigh). fade stays Max(0.1126270294189453125/
    // 43.6621189117431640625, 0.972) = 0.972; liftSignal stays −0.945
    // until the thigh walks onto 39.6621189117431640625°, then room ≤ 0
    // again. Extra hipLift at that leftover still cannot raise past
    // the cap. DualLoadMinSpan still never opens, liftGateBlend = 0,
    // Fall→Reach still waits on air. Same quarter as fold grow:
    // Lerp(39.6621189117431640625, 40, 0.25) = 39.746589183807373046875°.
    // At a 39.6621189117431640625° thigh, room = 0.084470272064208984375°
    // (~0.063 cm). fade stays Max(0.084470272064208984375/
    // 43.746589183807373046875, 0.972) so liftSignal stays −0.945 until
    // the thigh walks; still 0.253410816192626953125° short of sci_a
    // 40°, still not −swingHipFlex. Deadzone still 4° past the new
    // cap. Air Fall/Reach keeps 39.6621189117431640625°. Catch / later
    // cycles keep the envelope. Hip-unload hold is still a no-op
    // (ThighHold already owns the swing hip on Fall via fold hold).
    // Not trailing A2 and not a PD retune.
    // 0 = former 39.6621189117431640625° CapGrowOnwardFirst leftover
    // under the 0.972 trio.
    [Tooltip("Plant→Fall cycle-0: grow the 39.6621189117431640625° hip-lift cap a quarter toward 40° so leftover at CapGrowOnwardFirst has 0.084470272064208984375° of room. 0 = former 39.6621189117431640625° leftover.")]
    public float walkPlantFallLiftCapGrowAheadFirst = 1f;
    // WALK-2 initiation is knee lift + stance drive + forward lean. The
    // triangle, friction fade, ThighHold, pelvis tilt, stance-knee
    // straighten, rock-lead CoP, ankleScale, the 55° fold cap,
    // hipBalanceScale 1, suppressWalkLean, the 76° grow ceiling, the
    // fold-hold seed, the 0.625 lift pair, the matching 0.625 cap
    // floor, the 0.719…0.972 trio, PlusFirst, CapGrowFirst,
    // CapGrowMoreFirst, CapGrowYetFirst, CapGrowEvenFirst,
    // CapGrowStillFirst, CapGrowAgainFirst, CapGrowAlsoFirst,
    // CapGrowExtraFirst, CapGrowPlusFirst, CapGrowFurtherFirst,
    // CapGrowOnwardFirst and CapGrowAheadFirst already survive
    // Plant→Fall. The remaining 0.253410816192626953125° to sci_a
    // does not.
    // CapGrowAheadFirst wrote leftover 39.746589183807373046875° (Lerp
    // 39.6621189117431640625→40 at 0.25) after the couple Min, so a
    // 39.6621189117431640625° thigh has 0.084470272064208984375° of room
    // (~0.063 cm of a 0.43 m thigh). fade stays Max(0.084470272064208984375/
    // 43.746589183807373046875, 0.972) = 0.972; liftSignal stays −0.945
    // until the thigh walks onto 39.746589183807373046875°, then room ≤ 0
    // again. Extra hipLift at that leftover still cannot raise past
    // the cap. DualLoadMinSpan still never opens, liftGateBlend = 0,
    // Fall→Reach still waits on air. Same quarter as fold grow:
    // Lerp(39.746589183807373046875, 40, 0.25) = 39.80994188785552978515625°.
    // At a 39.746589183807373046875° thigh, room = 0.06335270404815673828125°
    // (~0.048 cm). fade stays Max(0.06335270404815673828125/
    // 43.80994188785552978515625, 0.972) so liftSignal stays −0.945 until
    // the thigh walks; still 0.19005811214447021484375° short of sci_a
    // 40°, still not −swingHipFlex. Deadzone still 4° past the new
    // cap. Air Fall/Reach keeps 39.746589183807373046875°. Catch / later
    // cycles keep the envelope. Hip-unload hold is still a no-op
    // (ThighHold already owns the swing hip on Fall via fold hold).
    // Not trailing A2 and not a PD retune.
    // 0 = former 39.746589183807373046875° CapGrowAheadFirst leftover
    // under the 0.972 trio.
    [Tooltip("Plant→Fall cycle-0: grow the 39.746589183807373046875° hip-lift cap a quarter toward 40° so leftover at CapGrowAheadFirst has 0.06335270404815673828125° of room. 0 = former 39.746589183807373046875° leftover.")]
    public float walkPlantFallLiftCapGrowAlongFirst = 1f;
    // WALK-2 initiation is knee lift + stance drive + forward lean. The
    // triangle, friction fade, ThighHold, pelvis tilt, stance-knee
    // straighten, rock-lead CoP, ankleScale, the 55° fold cap,
    // hipBalanceScale 1, suppressWalkLean, the 76° grow ceiling, the
    // fold-hold seed, the 0.625 lift pair, the matching 0.625 cap
    // floor, the 0.719…0.972 trio, PlusFirst, CapGrowFirst,
    // CapGrowMoreFirst, CapGrowYetFirst, CapGrowEvenFirst,
    // CapGrowStillFirst, CapGrowAgainFirst, CapGrowAlsoFirst,
    // CapGrowExtraFirst, CapGrowPlusFirst, CapGrowFurtherFirst,
    // CapGrowOnwardFirst, CapGrowAheadFirst and CapGrowAlongFirst
    // already survive Plant→Fall. The remaining
    // 0.19005811214447021484375° to sci_a does not.
    // CapGrowAlongFirst wrote leftover 39.80994188785552978515625° (Lerp
    // 39.746589183807373046875→40 at 0.25) after the couple Min, so a
    // 39.746589183807373046875° thigh has 0.06335270404815673828125° of room
    // (~0.048 cm of a 0.43 m thigh). fade stays Max(0.06335270404815673828125/
    // 43.80994188785552978515625, 0.972) = 0.972; liftSignal stays −0.945
    // until the thigh walks onto 39.80994188785552978515625°, then room ≤ 0
    // again. Extra hipLift at that leftover still cannot raise past
    // the cap. DualLoadMinSpan still never opens, liftGateBlend = 0,
    // Fall→Reach still waits on air. Same quarter as fold grow:
    // Lerp(39.80994188785552978515625, 40, 0.25) = 39.8574564158916473388671875°.
    // At a 39.80994188785552978515625° thigh, room = 0.0475145280361175537109375°
    // (~0.036 cm). fade stays Max(0.0475145280361175537109375/
    // 43.8574564158916473388671875, 0.972) so liftSignal stays −0.945 until
    // the thigh walks; still 0.1425435841083526611328125° short of sci_a
    // 40°, still not −swingHipFlex. Deadzone still 4° past the new
    // cap. Air Fall/Reach keeps 39.80994188785552978515625°. Catch / later
    // cycles keep the envelope. Hip-unload hold is still a no-op
    // (ThighHold already owns the swing hip on Fall via fold hold).
    // Not trailing A2 and not a PD retune.
    // 0 = former 39.80994188785552978515625° CapGrowAlongFirst leftover
    // under the 0.972 trio.
    [Tooltip("Plant→Fall cycle-0: grow the 39.80994188785552978515625° hip-lift cap a quarter toward 40° so leftover at CapGrowAlongFirst has 0.0475145280361175537109375° of room. 0 = former 39.80994188785552978515625° leftover.")]
    public float walkPlantFallLiftCapGrowForthFirst = 1f;
    // WALK-2 initiation is knee lift + stance drive + forward lean. The
    // triangle, friction fade, ThighHold, pelvis tilt, stance-knee
    // straighten, rock-lead CoP, ankleScale, the 55° fold cap,
    // hipBalanceScale 1, suppressWalkLean, the 76° grow ceiling, the
    // fold-hold seed, the 0.625 lift pair, the matching 0.625 cap
    // floor, the 0.719…0.972 trio, PlusFirst, CapGrowFirst,
    // CapGrowMoreFirst, CapGrowYetFirst, CapGrowEvenFirst,
    // CapGrowStillFirst, CapGrowAgainFirst, CapGrowAlsoFirst,
    // CapGrowExtraFirst, CapGrowPlusFirst, CapGrowFurtherFirst,
    // CapGrowOnwardFirst, CapGrowAheadFirst, CapGrowAlongFirst
    // and CapGrowForthFirst already survive Plant→Fall. The remaining
    // 0.1425435841083526611328125° to sci_a does not.
    // CapGrowForthFirst wrote leftover 39.8574564158916473388671875° (Lerp
    // 39.80994188785552978515625→40 at 0.25) after the couple Min, so a
    // 39.80994188785552978515625° thigh has 0.0475145280361175537109375° of room
    // (~0.036 cm of a 0.43 m thigh). fade stays Max(0.0475145280361175537109375/
    // 43.8574564158916473388671875, 0.972) = 0.972; liftSignal stays −0.945
    // until the thigh walks onto 39.8574564158916473388671875°, then room ≤ 0
    // again. Extra hipLift at that leftover still cannot raise past
    // the cap. DualLoadMinSpan still never opens, liftGateBlend = 0,
    // Fall→Reach still waits on air. Same quarter as fold grow:
    // Lerp(39.8574564158916473388671875, 40, 0.25) = 39.893092311918735504150390625°.
    // At a 39.8574564158916473388671875° thigh, room = 0.035635896027088165283203125°
    // (~0.027 cm). fade stays Max(0.035635896027088165283203125/
    // 43.893092311918735504150390625, 0.972) so liftSignal stays −0.945 until
    // the thigh walks; still 0.106907688081264495849609375° short of sci_a
    // 40°, still not −swingHipFlex. Deadzone still 4° past the new
    // cap. Air Fall/Reach keeps 39.8574564158916473388671875°. Catch / later
    // cycles keep the envelope. Hip-unload hold is still a no-op
    // (ThighHold already owns the swing hip on Fall via fold hold).
    // Not trailing A2 and not a PD retune.
    // 0 = former 39.8574564158916473388671875° CapGrowForthFirst leftover
    // under the 0.972 trio.
    [Tooltip("Plant→Fall cycle-0: grow the 39.8574564158916473388671875° hip-lift cap a quarter toward 40° so leftover at CapGrowForthFirst has 0.035635896027088165283203125° of room. 0 = former 39.8574564158916473388671875° leftover.")]
    public float walkPlantFallLiftCapGrowBeyondFirst = 1f;
    // WALK-2 initiation is knee lift + stance drive + forward lean. The
    // triangle, friction fade, ThighHold, pelvis tilt, stance-knee
    // straighten, rock-lead CoP, ankleScale, the 55° fold cap,
    // hipBalanceScale 1, suppressWalkLean, the 76° grow ceiling, the
    // fold-hold seed, the 0.625 lift pair, the matching 0.625 cap
    // floor, the 0.719…0.972 trio, PlusFirst, CapGrowFirst,
    // CapGrowMoreFirst, CapGrowYetFirst, CapGrowEvenFirst,
    // CapGrowStillFirst, CapGrowAgainFirst, CapGrowAlsoFirst,
    // CapGrowExtraFirst, CapGrowPlusFirst, CapGrowFurtherFirst,
    // CapGrowOnwardFirst, CapGrowAheadFirst, CapGrowAlongFirst,
    // CapGrowForthFirst and CapGrowBeyondFirst already survive
    // Plant→Fall. The remaining 0.106907688081264495849609375° to
    // sci_a does not.
    // CapGrowBeyondFirst wrote leftover 39.893092311918735504150390625° (Lerp
    // 39.8574564158916473388671875→40 at 0.25) after the couple Min, so a
    // 39.8574564158916473388671875° thigh has 0.035635896027088165283203125° of room
    // (~0.027 cm of a 0.43 m thigh). fade stays Max(0.035635896027088165283203125/
    // 43.893092311918735504150390625, 0.972) = 0.972; liftSignal stays −0.945
    // until the thigh walks onto 39.893092311918735504150390625°, then room ≤ 0
    // again. Extra hipLift at that leftover still cannot raise past
    // the cap. DualLoadMinSpan still never opens, liftGateBlend = 0,
    // Fall→Reach still waits on air. Same quarter as fold grow:
    // Lerp(39.893092311918735504150390625, 40, 0.25) = 39.91981923393905162811279296875°.
    // At a 39.893092311918735504150390625° thigh, room = 0.02672692202031612396240234375°
    // (~0.020 cm). fade stays Max(0.02672692202031612396240234375/
    // 43.91981923393905162811279296875, 0.972) so liftSignal stays −0.945 until
    // the thigh walks; still 0.08018076606094837188720703125° short of sci_a
    // 40°, still not −swingHipFlex. Deadzone still 4° past the new
    // cap. Air Fall/Reach keeps 39.893092311918735504150390625°. Catch / later
    // cycles keep the envelope. Hip-unload hold is still a no-op
    // (ThighHold already owns the swing hip on Fall via fold hold).
    // Not trailing A2 and not a PD retune.
    // 0 = former 39.893092311918735504150390625° CapGrowBeyondFirst leftover
    // under the 0.972 trio.
    [Tooltip("Plant→Fall cycle-0: grow the 39.893092311918735504150390625° hip-lift cap a quarter toward 40° so leftover at CapGrowBeyondFirst has 0.02672692202031612396240234375° of room. 0 = former 39.893092311918735504150390625° leftover.")]
    public float walkPlantFallLiftCapGrowPastFirst = 1f;
    // WALK-2 initiation is knee lift + stance drive + forward lean. The
    // triangle, friction fade, ThighHold, pelvis tilt, stance-knee
    // straighten, rock-lead CoP, ankleScale, the 55° fold cap,
    // hipBalanceScale 1, suppressWalkLean, the 76° grow ceiling, the
    // fold-hold seed, the 0.625 lift pair, the matching 0.625 cap
    // floor, the 0.719…0.972 trio, PlusFirst, CapGrowFirst,
    // CapGrowMoreFirst, CapGrowYetFirst, CapGrowEvenFirst,
    // CapGrowStillFirst, CapGrowAgainFirst, CapGrowAlsoFirst,
    // CapGrowExtraFirst, CapGrowPlusFirst, CapGrowFurtherFirst,
    // CapGrowOnwardFirst, CapGrowAheadFirst, CapGrowAlongFirst,
    // CapGrowForthFirst, CapGrowBeyondFirst and CapGrowPastFirst already survive
    // Plant→Fall. The remaining 0.08018076606094837188720703125° to
    // sci_a does not.
    // CapGrowPastFirst wrote leftover 39.91981923393905162811279296875° (Lerp
    // 39.893092311918735504150390625→40 at 0.25) after the couple Min, so a
    // 39.893092311918735504150390625° thigh has 0.02672692202031612396240234375° of room
    // (~0.020 cm of a 0.43 m thigh). fade stays Max(0.02672692202031612396240234375/
    // 43.91981923393905162811279296875, 0.972) = 0.972; liftSignal stays −0.945
    // until the thigh walks onto 39.91981923393905162811279296875°, then room ≤ 0
    // again. Extra hipLift at that leftover still cannot raise past
    // the cap. DualLoadMinSpan still never opens, liftGateBlend = 0,
    // Fall→Reach still waits on air. Same quarter as fold grow:
    // Lerp(39.91981923393905162811279296875, 40, 0.25) = 39.9398644254542887210845947265625°.
    // At a 39.91981923393905162811279296875° thigh, room = 0.0200451915152370929718017578125°
    // (~0.015 cm). fade stays Max(0.0200451915152370929718017578125/
    // 43.9398644254542887210845947265625, 0.972) so liftSignal stays −0.945 until
    // the thigh walks; still 0.0601355745457112789154052734375° short of sci_a
    // 40°, still not −swingHipFlex. Deadzone still 4° past the new
    // cap. Air Fall/Reach keeps 39.91981923393905162811279296875°. Catch / later
    // cycles keep the envelope. Hip-unload hold is still a no-op
    // (ThighHold already owns the swing hip on Fall via fold hold).
    // Not trailing A2 and not a PD retune.
    // 0 = former 39.91981923393905162811279296875° CapGrowPastFirst leftover
    // under the 0.972 trio.
    [Tooltip("Plant→Fall cycle-0: grow the 39.91981923393905162811279296875° hip-lift cap a quarter toward 40° so leftover at CapGrowPastFirst has 0.0200451915152370929718017578125° of room. 0 = former 39.91981923393905162811279296875° leftover.")]
    public float walkPlantFallLiftCapGrowOverFirst = 1f;
    // WALK-2 initiation is knee lift + stance drive + forward lean. The
    // triangle, friction fade, ThighHold, pelvis tilt, stance-knee
    // straighten, rock-lead CoP, ankleScale, the 55° fold cap,
    // hipBalanceScale 1, suppressWalkLean, the 76° grow ceiling, the
    // fold-hold seed, the 0.625 lift pair, the matching 0.625 cap
    // floor, the 0.719…0.972 trio, PlusFirst, CapGrowFirst,
    // CapGrowMoreFirst, CapGrowYetFirst, CapGrowEvenFirst,
    // CapGrowStillFirst, CapGrowAgainFirst, CapGrowAlsoFirst,
    // CapGrowExtraFirst, CapGrowPlusFirst, CapGrowFurtherFirst,
    // CapGrowOnwardFirst, CapGrowAheadFirst, CapGrowAlongFirst,
    // CapGrowForthFirst, CapGrowBeyondFirst, CapGrowPastFirst and
    // CapGrowOverFirst already survive Plant→Fall. The remaining
    // 0.0601355745457112789154052734375° to sci_a does not.
    // CapGrowOverFirst wrote leftover 39.9398644254542887210845947265625° (Lerp
    // 39.91981923393905162811279296875→40 at 0.25) after the couple Min, so a
    // 39.91981923393905162811279296875° thigh has 0.0200451915152370929718017578125° of room
    // (~0.015 cm of a 0.43 m thigh). fade stays Max(0.0200451915152370929718017578125/
    // 43.9398644254542887210845947265625, 0.972) = 0.972; liftSignal stays −0.945
    // until the thigh walks onto 39.9398644254542887210845947265625°, then room ≤ 0
    // again. Extra hipLift at that leftover still cannot raise past
    // the cap. DualLoadMinSpan still never opens, liftGateBlend = 0,
    // Fall→Reach still waits on air. Same quarter as fold grow:
    // Lerp(39.9398644254542887210845947265625, 40, 0.25) = 39.954898319090716540813446044921875°.
    // At a 39.9398644254542887210845947265625° thigh, room = 0.015033893636427819728851318359375°
    // (~0.011 cm). fade stays Max(0.015033893636427819728851318359375/
    // 43.954898319090716540813446044921875, 0.972) so liftSignal stays −0.945 until
    // the thigh walks; still 0.045101680909283459186553955078125° short of sci_a
    // 40°, still not −swingHipFlex. Deadzone still 4° past the new
    // cap. Air Fall/Reach keeps 39.9398644254542887210845947265625°. Catch / later
    // cycles keep the envelope. Hip-unload hold is still a no-op
    // (ThighHold already owns the swing hip on Fall via fold hold).
    // Not trailing A2 and not a PD retune.
    // 0 = former 39.9398644254542887210845947265625° CapGrowOverFirst leftover
    // under the 0.972 trio.
    [Tooltip("Plant→Fall cycle-0: grow the 39.9398644254542887210845947265625° hip-lift cap a quarter toward 40° so leftover at CapGrowOverFirst has 0.015033893636427819728851318359375° of room. 0 = former 39.9398644254542887210845947265625° leftover.")]
    public float walkPlantFallLiftCapGrowAcrossFirst = 1f;
    // WALK-2 initiation is knee lift + stance drive + forward lean. The
    // triangle, friction fade, ThighHold, pelvis tilt, stance-knee
    // straighten, rock-lead CoP, ankleScale, the 55° fold cap,
    // hipBalanceScale 1, suppressWalkLean, the 76° grow ceiling, the
    // fold-hold seed, the 0.625 lift pair, the matching 0.625 cap
    // floor, the 0.719…0.972 trio, PlusFirst, CapGrowFirst through
    // CapGrowAcrossFirst already survive Plant→Fall. The leftover hip
    // cap is spent: AcrossFirst leftover 39.954898319090716540813446044921875°
    // is 0.045° short of sci_a and ~0.011 cm of a 0.43 m thigh; fade
    // stays Max(room/cap, 0.972) so liftSignal stays −0.945, extra
    // hipLift at room ≤ 0 cannot raise past that leftover, and another
    // 0.25 of 0.045° would buy ~0.008 cm. DualLoadMinSpan still never
    // opens, liftGateBlend = 0, Fall→Reach still waits on air. The
    // GROUNDED FOLD is still the first-quarter 76°
    // (2·L·cos(38°) ≈ 0.79 L). Knee torque is thigh–shin, so more fold
    // shortens the swing chain without asking the hip past sci_a 40°.
    // Same quarter as fold grow: Lerp(76, 140, 0.25) = 92°,
    // 2·L·cos(46°) ≈ 0.70 L (~8 cm of a 0.86 m chain vs 76°). Still
    // far from the 0.34 L squat at 140°. Seed already Maxes to
    // groundedCap, so the extra shortening is live on the first
    // grounded Fall frame. The 0.972 trio already unweights the sole
    // (WALK-2 forbids a backward slide on µ=0.4). Air keeps 140°.
    // Catch / later cycles keep the envelope. Hip-unload hold is still
    // a no-op (ThighHold already owns the swing hip on Fall via fold
    // hold). Not trailing A2, not −swingHipFlex, not a PD retune, and
    // not another leftover-cap quarter.
    // 0 = former 76° grow cap on dirt.
    [Tooltip("Plant→Fall cycle-0: grow the grounded Fall/Reach fold a second quarter from 76° toward reachKneeFlex (92°). 0 = former 76° cap on dirt.")]
    public float walkPlantFallFoldGrowMoreFirst = 1f;
    // WALK-2 initiation is knee lift + stance drive + forward lean. The
    // triangle, friction fade, ThighHold, pelvis tilt, stance-knee
    // straighten, rock-lead CoP, ankleScale, the 55° fold cap,
    // hipBalanceScale 1, suppressWalkLean, the 76° grow ceiling, the
    // fold-hold seed, the 0.625 lift pair, the matching 0.625 cap
    // floor, the 0.719…0.972 trio, PlusFirst, CapGrowFirst through
    // CapGrowAcrossFirst and FoldGrowMoreFirst already survive
    // Plant→Fall. Leftover-cap grow is spent (AcrossFirst 0.045° /
    // ~0.011 cm, fade stuck at 0.972 so liftSignal stays −0.945).
    // DualLoadMinSpan still never opens, liftGateBlend = 0,
    // Fall→Reach still waits on air. FoldGrowMoreFirst wrote 92°
    // (2·L·cos(46°) ≈ 0.70 L). Knee torque is thigh–shin, so more
    // fold shortens the swing chain without asking the hip past
    // sci_a 40°. Same quarter as fold grow: Lerp(92, 140, 0.25) =
    // 104°, 2·L·cos(52°) ≈ 0.62 L (~7 cm of a 0.86 m chain vs 92°).
    // Still far from the 0.34 L squat at 140°. Seed already Maxes
    // to groundedCap, so the extra shortening is live on the first
    // grounded Fall frame. The 0.972 trio already unweights the
    // sole (WALK-2 forbids a backward slide on µ=0.4). Air keeps
    // 140°. Catch / later cycles keep the envelope. Hip-unload
    // hold is still a no-op (ThighHold already owns the swing hip
    // on Fall via fold hold). Not trailing A2, not −swingHipFlex,
    // not a PD retune, and not another leftover-cap quarter.
    // 0 = former 92° grow cap on dirt.
    [Tooltip("Plant→Fall cycle-0: grow the grounded Fall/Reach fold a third quarter from 92° toward reachKneeFlex (104°). 0 = former 92° cap on dirt.")]
    public float walkPlantFallFoldGrowYetFirst = 1f;
    // WALK-2 initiation is knee lift + stance drive + forward lean. The
    // triangle, friction fade, ThighHold, pelvis tilt, stance-knee
    // straighten, rock-lead CoP, ankleScale, the 55° fold cap,
    // hipBalanceScale 1, suppressWalkLean, the 76° grow ceiling, the
    // fold-hold seed, the 0.625 lift pair, the matching 0.625 cap
    // floor, the 0.719…0.972 trio, PlusFirst, CapGrowFirst through
    // CapGrowAcrossFirst, FoldGrowMoreFirst and FoldGrowYetFirst
    // already survive Plant→Fall. Leftover-cap grow is spent
    // (AcrossFirst 0.045° / ~0.011 cm, fade stuck at 0.972 so
    // liftSignal stays −0.945). DualLoadMinSpan still never opens,
    // liftGateBlend = 0, Fall→Reach still waits on air.
    // FoldGrowYetFirst wrote 104° (2·L·cos(52°) ≈ 0.62 L). Knee
    // torque is thigh–shin, so more fold shortens the swing chain
    // without asking the hip past sci_a 40°. Same quarter as fold
    // grow: Lerp(104, 140, 0.25) = 113°, 2·L·cos(56.5°) ≈ 0.55 L
    // (~6 cm of a 0.86 m chain vs 104°). Still far from the 0.34 L
    // squat at 140°. Seed already Maxes to groundedCap, so the extra
    // shortening is live on the first grounded Fall frame. The 0.972
    // trio already unweights the sole (WALK-2 forbids a backward
    // slide on µ=0.4). Air keeps 140°. Catch / later cycles keep
    // the envelope. Hip-unload hold is still a no-op (ThighHold
    // already owns the swing hip on Fall via fold hold). Not
    // trailing A2, not −swingHipFlex, not a PD retune, and not
    // another leftover-cap quarter.
    // 0 = former 104° grow cap on dirt.
    [Tooltip("Plant→Fall cycle-0: grow the grounded Fall/Reach fold a fourth quarter from 104° toward reachKneeFlex (113°). 0 = former 104° cap on dirt.")]
    public float walkPlantFallFoldGrowEvenFirst = 1f;
    // WALK-2 initiation is knee lift + stance drive + forward lean. The
    // triangle, friction fade, ThighHold, pelvis tilt, stance-knee
    // straighten, rock-lead CoP, ankleScale, the 55° fold cap,
    // hipBalanceScale 1, suppressWalkLean, the 76° grow ceiling, the
    // fold-hold seed, the 0.625 lift pair, the matching 0.625 cap
    // floor, the 0.719…0.972 trio, PlusFirst, CapGrowFirst through
    // CapGrowAcrossFirst, FoldGrowMoreFirst, FoldGrowYetFirst and
    // FoldGrowEvenFirst already survive Plant→Fall. Leftover-cap
    // grow is spent (AcrossFirst 0.045° / ~0.011 cm, fade stuck at
    // 0.972 so liftSignal stays −0.945). DualLoadMinSpan still never
    // opens, liftGateBlend = 0, Fall→Reach still waits on air.
    // FoldGrowEvenFirst wrote 113° (2·L·cos(56.5°) ≈ 0.55 L). Knee
    // torque is thigh–shin, so more fold shortens the swing chain
    // without asking the hip past sci_a 40°. Same quarter as fold
    // grow: Lerp(113, 140, 0.25) = 120°, 2·L·cos(60°) ≈ 0.50 L
    // (~4 cm of a 0.86 m chain vs 113°). Still far from the 0.34 L
    // squat at 140°. Seed already Maxes to groundedCap, so the extra
    // shortening is live on the first grounded Fall frame. The 0.972
    // trio already unweights the sole (WALK-2 forbids a backward
    // slide on µ=0.4). Air keeps 140°. Catch / later cycles keep
    // the envelope. Hip-unload hold is still a no-op (ThighHold
    // already owns the swing hip on Fall via fold hold). Not
    // trailing A2, not −swingHipFlex, not a PD retune, and not
    // another leftover-cap quarter.
    // 0 = former 113° grow cap on dirt.
    [Tooltip("Plant→Fall cycle-0: grow the grounded Fall/Reach fold a fifth quarter from 113° toward reachKneeFlex (120°). 0 = former 113° cap on dirt.")]
    public float walkPlantFallFoldGrowStillFirst = 1f;
    // WALK-2 initiation is knee lift + stance drive + forward lean. The
    // triangle, friction fade, ThighHold, pelvis tilt, stance-knee
    // straighten, rock-lead CoP, ankleScale, the 55° fold cap,
    // hipBalanceScale 1, suppressWalkLean, the 76° grow ceiling, the
    // fold-hold seed, the 0.625 lift pair, the matching 0.625 cap
    // floor, the 0.719…0.972 trio, PlusFirst, CapGrowFirst through
    // CapGrowAcrossFirst, FoldGrowMoreFirst through FoldGrowStillFirst
    // already survive Plant→Fall. Leftover-cap grow is spent
    // (AcrossFirst 0.045° / ~0.011 cm, fade stuck at 0.972 so
    // liftSignal stays −0.945). DualLoadMinSpan still never opens,
    // liftGateBlend = 0, Fall→Reach still waits on air.
    // FoldGrowStillFirst wrote 120° (2·L·cos(60°) ≈ 0.50 L). Knee
    // torque is thigh–shin, so more fold shortens the swing chain
    // without asking the hip past sci_a 40°. Same quarter as fold
    // grow: Lerp(120, 140, 0.25) = 125°, 2·L·cos(62.5°) ≈ 0.46 L
    // (~3 cm of a 0.86 m chain vs 120°). Still far from the 0.34 L
    // squat at 140°. Seed already Maxes to groundedCap, so the extra
    // shortening is live on the first grounded Fall frame. The 0.972
    // trio already unweights the sole (WALK-2 forbids a backward
    // slide on µ=0.4). Air keeps 140°. Catch / later cycles keep
    // the envelope. Hip-unload hold is still a no-op (ThighHold
    // already owns the swing hip on Fall via fold hold). Not
    // trailing A2, not −swingHipFlex, not a PD retune, and not
    // another leftover-cap quarter.
    // 0 = former 120° grow cap on dirt.
    [Tooltip("Plant→Fall cycle-0: grow the grounded Fall/Reach fold a sixth quarter from 120° toward reachKneeFlex (125°). 0 = former 120° cap on dirt.")]
    public float walkPlantFallFoldGrowAgainFirst = 1f;
    // WALK-2 initiation is knee lift + stance drive + forward lean. The
    // triangle, friction fade, ThighHold, pelvis tilt, stance-knee
    // straighten, rock-lead CoP, ankleScale, the 55° fold cap,
    // hipBalanceScale 1, suppressWalkLean, the 76° grow ceiling, the
    // fold-hold seed, the 0.625 lift pair, the matching 0.625 cap
    // floor, the 0.719…0.972 trio, PlusFirst, CapGrowFirst through
    // CapGrowAcrossFirst, FoldGrowMoreFirst through FoldGrowAgainFirst
    // already survive Plant→Fall. Leftover-cap grow is spent
    // (AcrossFirst 0.045° / ~0.011 cm, fade stuck at 0.972 so
    // liftSignal stays −0.945). DualLoadMinSpan still never opens,
    // liftGateBlend = 0, Fall→Reach still waits on air.
    // FoldGrowAgainFirst wrote 125° (2·L·cos(62.5°) ≈ 0.46 L). Knee
    // torque is thigh–shin, so more fold shortens the swing chain
    // without asking the hip past sci_a 40°. Same quarter as fold
    // grow: Lerp(125, 140, 0.25) = 129°, 2·L·cos(64.5°) ≈ 0.43 L
    // (~3 cm of a 0.86 m chain vs 125°). Still far from the 0.34 L
    // squat at 140°. Seed already Maxes to groundedCap, so the extra
    // shortening is live on the first grounded Fall frame. The 0.972
    // trio already unweights the sole (WALK-2 forbids a backward
    // slide on µ=0.4). Air keeps 140°. Catch / later cycles keep
    // the envelope. Hip-unload hold is still a no-op (ThighHold
    // already owns the swing hip on Fall via fold hold). Not
    // trailing A2, not −swingHipFlex, not a PD retune, and not
    // another leftover-cap quarter.
    // 0 = former 125° grow cap on dirt.
    [Tooltip("Plant→Fall cycle-0: grow the grounded Fall/Reach fold a seventh quarter from 125° toward reachKneeFlex (129°). 0 = former 125° cap on dirt.")]
    public float walkPlantFallFoldGrowAlsoFirst = 1f;
    // WALK-2 initiation is knee lift + stance drive + forward lean. The
    // triangle, friction fade, ThighHold, pelvis tilt, stance-knee
    // straighten, rock-lead CoP, ankleScale, the 55° fold cap,
    // hipBalanceScale 1, suppressWalkLean, the 76° grow ceiling, the
    // fold-hold seed, the 0.625 lift pair, the matching 0.625 cap
    // floor, the 0.719…0.972 trio, PlusFirst, CapGrowFirst through
    // CapGrowAcrossFirst, FoldGrowMoreFirst through FoldGrowAlsoFirst
    // already survive Plant→Fall. Leftover-cap grow is spent
    // (AcrossFirst 0.045° / ~0.011 cm, fade stuck at 0.972 so
    // liftSignal stays −0.945). DualLoadMinSpan still never opens,
    // liftGateBlend = 0, Fall→Reach still waits on air.
    // FoldGrowAlsoFirst wrote 129° (2·L·cos(64.5°) ≈ 0.43 L). Knee
    // torque is thigh–shin, so more fold shortens the swing chain
    // without asking the hip past sci_a 40°. Same quarter as fold
    // grow: Lerp(129, 140, 0.25) = 132°, 2·L·cos(66°) ≈ 0.41 L
    // (~2 cm of a 0.86 m chain vs 129°). Still far from the 0.34 L
    // squat at 140°. Seed already Maxes to groundedCap, so the extra
    // shortening is live on the first grounded Fall frame. The 0.972
    // trio already unweights the sole (WALK-2 forbids a backward
    // slide on µ=0.4). Air keeps 140°. Catch / later cycles keep
    // the envelope. Hip-unload hold is still a no-op (ThighHold
    // already owns the swing hip on Fall via fold hold). Not
    // trailing A2, not −swingHipFlex, not a PD retune, and not
    // another leftover-cap quarter.
    // 0 = former 129° grow cap on dirt.
    [Tooltip("Plant→Fall cycle-0: grow the grounded Fall/Reach fold an eighth quarter from 129° toward reachKneeFlex (132°). 0 = former 129° cap on dirt.")]
    public float walkPlantFallFoldGrowExtraFirst = 1f;
    // WALK-2 initiation is knee lift + stance drive + forward lean. The
    // triangle, friction fade, ThighHold, pelvis tilt, stance-knee
    // straighten, rock-lead CoP, ankleScale, the 55° fold cap,
    // hipBalanceScale 1, suppressWalkLean, the 76° grow ceiling, the
    // fold-hold seed, the 0.625 lift pair, the matching 0.625 cap
    // floor, the 0.719…0.972 trio, lift-trio PlusFirst, CapGrowFirst
    // through CapGrowAcrossFirst, FoldGrowMoreFirst through FoldGrowExtraFirst
    // already survive Plant→Fall. Leftover-cap grow is spent
    // (AcrossFirst 0.045° / ~0.011 cm, fade stuck at 0.972 so
    // liftSignal stays −0.945). DualLoadMinSpan still never opens,
    // liftGateBlend = 0, Fall→Reach still waits on air.
    // FoldGrowExtraFirst wrote 132° (2·L·cos(66°) ≈ 0.41 L). Knee
    // torque is thigh–shin, so more fold shortens the swing chain
    // without asking the hip past sci_a 40°. Same quarter as fold
    // grow: Lerp(132, 140, 0.25) = 134°, 2·L·cos(67°) ≈ 0.39 L
    // (~1 cm of a 0.86 m chain vs 132°). Still far from the 0.34 L
    // squat at 140°. Seed already Maxes to groundedCap, so the extra
    // shortening is live on the first grounded Fall frame. The 0.972
    // trio already unweights the sole (WALK-2 forbids a backward
    // slide on µ=0.4). Air keeps 140°. Catch / later cycles keep
    // the envelope. Hip-unload hold is still a no-op (ThighHold
    // already owns the swing hip on Fall via fold hold). Not
    // trailing A2, not −swingHipFlex, not a PD retune, and not
    // another leftover-cap quarter.
    // 0 = former 132° grow cap on dirt.
    [Tooltip("Plant→Fall cycle-0: grow the grounded Fall/Reach fold a ninth quarter from 132° toward reachKneeFlex (134°). 0 = former 132° cap on dirt.")]
    public float walkPlantFallFoldGrowPlusFirst = 1f;
    // WALK-2 initiation is knee lift + stance drive + forward lean. The
    // triangle, friction fade, ThighHold, pelvis tilt, stance-knee
    // straighten, rock-lead CoP, ankleScale, the 55° fold cap,
    // hipBalanceScale 1, suppressWalkLean, the 76° grow ceiling, the
    // fold-hold seed, the 0.625 lift pair, the matching 0.625 cap
    // floor, the 0.719…0.972 trio, lift-trio PlusFirst, CapGrowFirst
    // through CapGrowAcrossFirst, FoldGrowMoreFirst through FoldGrowPlusFirst
    // already survive Plant→Fall. Leftover-cap grow is spent
    // (AcrossFirst 0.045° / ~0.011 cm, fade stuck at 0.972 so
    // liftSignal stays −0.945). DualLoadMinSpan still never opens,
    // liftGateBlend = 0, Fall→Reach still waits on air.
    // FoldGrowPlusFirst wrote 134° (2·L·cos(67°) ≈ 0.39 L). Knee
    // torque is thigh–shin, so more fold shortens the swing chain
    // without asking the hip past sci_a 40°. Same quarter as fold
    // grow: Lerp(134, 140, 0.25) = 136°, 2·L·cos(68°) ≈ 0.37 L
    // (~1 cm of a 0.86 m chain vs 134°). Still far from the 0.34 L
    // squat at 140°. Seed already Maxes to groundedCap, so the extra
    // shortening is live on the first grounded Fall frame. The 0.972
    // trio already unweights the sole (WALK-2 forbids a backward
    // slide on µ=0.4). Air keeps 140°. Catch / later cycles keep
    // the envelope. Hip-unload hold is still a no-op (ThighHold
    // already owns the swing hip on Fall via fold hold). Not
    // trailing A2, not −swingHipFlex, not a PD retune, and not
    // another leftover-cap quarter.
    // 0 = former 134° grow cap on dirt.
    [Tooltip("Plant→Fall cycle-0: grow the grounded Fall/Reach fold a tenth quarter from 134° toward reachKneeFlex (136°). 0 = former 134° cap on dirt.")]
    public float walkPlantFallFoldGrowFurtherFirst = 1f;
    // WALK-2 initiation is knee lift + stance drive + forward lean. The
    // triangle, friction fade, ThighHold, pelvis tilt, stance-knee
    // straighten, rock-lead CoP, ankleScale, the 55° fold cap,
    // hipBalanceScale 1, suppressWalkLean, the 76° grow ceiling, the
    // fold-hold seed, the 0.625 lift pair, the matching 0.625 cap
    // floor, the 0.719…0.972 trio, lift-trio PlusFirst, CapGrowFirst
    // through CapGrowAcrossFirst, FoldGrowMoreFirst through FoldGrowFurtherFirst
    // already survive Plant→Fall. Leftover-cap grow is spent
    // (AcrossFirst 0.045° / ~0.011 cm, fade stuck at 0.972 so
    // liftSignal stays −0.945). DualLoadMinSpan still never opens,
    // liftGateBlend = 0, Fall→Reach still waits on air.
    // FoldGrowFurtherFirst wrote 136° (2·L·cos(68°) ≈ 0.37 L). Knee
    // torque is thigh–shin, so more fold shortens the swing chain
    // without asking the hip past sci_a 40°. Same quarter as fold
    // grow: Lerp(136, 140, 0.25) = 137°, 2·L·cos(68.5°) ≈ 0.37 L
    // (~1 cm of a 0.86 m chain vs 136°). Still far from the 0.34 L
    // squat at 140°. Seed already Maxes to groundedCap, so the extra
    // shortening is live on the first grounded Fall frame. The 0.972
    // trio already unweights the sole (WALK-2 forbids a backward
    // slide on µ=0.4). Air keeps 140°. Catch / later cycles keep
    // the envelope. Hip-unload hold is still a no-op (ThighHold
    // already owns the swing hip on Fall via fold hold). Not
    // trailing A2, not −swingHipFlex, not a PD retune, and not
    // another leftover-cap quarter.
    // 0 = former 136° grow cap on dirt.
    [Tooltip("Plant→Fall cycle-0: grow the grounded Fall/Reach fold an eleventh quarter from 136° toward reachKneeFlex (137°). 0 = former 136° cap on dirt.")]
    public float walkPlantFallFoldGrowOnwardFirst = 1f;
    // WALK-2 initiation is knee lift + stance drive + forward lean. The
    // triangle, friction fade, ThighHold, pelvis tilt, stance-knee
    // straighten, rock-lead CoP, ankleScale, the 55° fold cap,
    // hipBalanceScale 1, suppressWalkLean, the 76° grow ceiling, the
    // fold-hold seed, the 0.625 lift pair, the matching 0.625 cap
    // floor, the 0.719…0.972 trio, lift-trio PlusFirst, CapGrowFirst
    // through CapGrowAcrossFirst, FoldGrowMoreFirst through FoldGrowOnwardFirst
    // already survive Plant→Fall. Leftover-cap grow is spent
    // (AcrossFirst 0.045° / ~0.011 cm, fade stuck at 0.972 so
    // liftSignal stays −0.945). DualLoadMinSpan still never opens,
    // liftGateBlend = 0, Fall→Reach still waits on air.
    // FoldGrowOnwardFirst wrote 137° (2·L·cos(68.5°) ≈ 0.37 L). Knee
    // torque is thigh–shin, so more fold shortens the swing chain
    // without asking the hip past sci_a 40°. Same quarter as fold
    // grow: Lerp(137, 140, 0.25) = 137.75° → 138°, 2·L·cos(69°) ≈ 0.36 L
    // (~0.7 cm of a 0.86 m chain vs 137°). Still far from the 0.34 L
    // squat at 140°. Seed already Maxes to groundedCap, so the extra
    // shortening is live on the first grounded Fall frame. The 0.972
    // trio already unweights the sole (WALK-2 forbids a backward
    // slide on µ=0.4). Air keeps 140°. Catch / later cycles keep
    // the envelope. Hip-unload hold is still a no-op (ThighHold
    // already owns the swing hip on Fall via fold hold). Not
    // trailing A2, not −swingHipFlex, not a PD retune, and not
    // another leftover-cap quarter.
    // 0 = former 137° grow cap on dirt.
    [Tooltip("Plant→Fall cycle-0: grow the grounded Fall/Reach fold a twelfth quarter from 137° toward reachKneeFlex (138°). 0 = former 137° cap on dirt.")]
    public float walkPlantFallFoldGrowAheadFirst = 1f;
    // WALK-2 initiation is knee lift + stance drive + forward lean. The
    // triangle, friction fade, ThighHold, pelvis tilt, stance-knee
    // straighten, rock-lead CoP, ankleScale, the 55° fold cap,
    // hipBalanceScale 1, suppressWalkLean, the 76° grow ceiling, the
    // fold-hold seed, the 0.625 lift pair, the matching 0.625 cap
    // floor, the 0.719…0.972 trio, lift-trio PlusFirst, CapGrowFirst
    // through CapGrowAcrossFirst, FoldGrowMoreFirst through FoldGrowAheadFirst
    // already survive Plant→Fall. Leftover-cap grow is spent
    // (AcrossFirst 0.045° / ~0.011 cm, fade stuck at 0.972 so
    // liftSignal stays −0.945). DualLoadMinSpan still never opens,
    // liftGateBlend = 0, Fall→Reach still waits on air.
    // FoldGrowAheadFirst wrote 138° (2·L·cos(69°) ≈ 0.36 L). Knee
    // torque is thigh–shin, so more fold shortens the swing chain
    // without asking the hip past sci_a 40°. Same quarter as fold
    // grow: Lerp(138, 140, 0.25) = 138.5° → 139°, 2·L·cos(69.5°) ≈ 0.35 L
    // (~0.7 cm of a 0.86 m chain vs 138°). Still short of the 0.34 L
    // squat at 140°. Seed already Maxes to groundedCap, so the extra
    // shortening is live on the first grounded Fall frame. The 0.972
    // trio already unweights the sole (WALK-2 forbids a backward
    // slide on µ=0.4). Air keeps 140°. Catch / later cycles keep
    // the envelope. Hip-unload hold is still a no-op (ThighHold
    // already owns the swing hip on Fall via fold hold). Not
    // trailing A2, not −swingHipFlex, not a PD retune, and not
    // another leftover-cap quarter.
    // 0 = former 138° grow cap on dirt.
    [Tooltip("Plant→Fall cycle-0: grow the grounded Fall/Reach fold a thirteenth quarter from 138° toward reachKneeFlex (139°). 0 = former 138° cap on dirt.")]
    public float walkPlantFallFoldGrowAlongFirst = 1f;
    // WALK-2 initiation is knee lift + stance drive + forward lean. The
    // triangle, friction fade, ThighHold, pelvis tilt, stance-knee
    // straighten, rock-lead CoP, ankleScale, the 55° fold cap,
    // hipBalanceScale 1, suppressWalkLean, the 76° grow ceiling, the
    // fold-hold seed, the 0.625 lift pair, the matching 0.625 cap
    // floor, the 0.719…0.972 trio, PlusFirst, CapGrowFirst through
    // CapGrowAcrossFirst and FoldGrowMoreFirst through FoldGrowAlongFirst
    // already survive Plant→Fall. Leftover-cap grow is spent (AcrossFirst
    // 0.045° / ~0.011 cm, fade stuck at 0.972 so liftSignal stays −0.945).
    // DualLoadMinSpan still never opens, liftGateBlend = 0, Fall→Reach
    // still waits on air. FoldGrowAlongFirst wrote 139°
    // (2·L·cos(69.5°) ≈ 0.35 L). Knee torque is thigh–shin, so more
    // fold shortens the swing chain without asking the hip past sci_a
    // 40°. Same quarter as fold grow: Lerp(139, 140, 0.25) = 139.25° →
    // 139°, 2·L·cos(69.625°) ≈ 0.35 L (~0.2 cm of a 0.86 m chain vs
    // 139°). Still short of the 0.34 L squat at 140°. Seed already
    // Maxes to groundedCap, so the extra shortening is live on the
    // first grounded Fall frame. The 0.972 trio already unweights the
    // sole (WALK-2 forbids a backward slide on µ=0.4). Air keeps 140°.
    // Catch / later cycles keep the envelope. Hip-unload hold is still
    // a no-op (ThighHold already owns the swing hip on Fall via fold
    // hold). Not trailing A2, not −swingHipFlex, not a PD retune, and
    // not another leftover-cap quarter.
    // 0 = former 139° grow cap on dirt.
    [Tooltip("Plant→Fall cycle-0: grow the grounded Fall/Reach fold a fourteenth quarter from 139° toward reachKneeFlex (139.25°). 0 = former 139° cap on dirt.")]
    public float walkPlantFallFoldGrowCloseFirst = 1f;
    // WALK-2 initiation is knee lift + stance drive + forward lean. The
    // triangle, friction fade, ThighHold, pelvis tilt, stance-knee
    // straighten, rock-lead CoP, ankleScale, the 55° fold cap,
    // hipBalanceScale 1, suppressWalkLean, the 76° grow ceiling, the
    // fold-hold seed, the 0.625 lift pair, the matching 0.625 cap
    // floor, the 0.719…0.972 trio, PlusFirst, CapGrowFirst through
    // CapGrowAcrossFirst and FoldGrowMoreFirst through FoldGrowCloseFirst
    // already survive Plant→Fall. Leftover-cap grow is spent (AcrossFirst
    // 0.045° / ~0.011 cm, fade stuck at 0.972 so liftSignal stays −0.945).
    // DualLoadMinSpan still never opens, liftGateBlend = 0, Fall→Reach
    // still waits on air. FoldGrowCloseFirst wrote 139.25°
    // (2·L·cos(69.625°) ≈ 0.35 L). Knee torque is thigh–shin, so more
    // fold shortens the swing chain without asking the hip past sci_a
    // 40°. Same quarter as fold grow: Lerp(139.25, 140, 0.25) = 139.44°
    // → 139.5°, 2·L·cos(69.72°) ≈ 0.35 L (~0.1 cm of a 0.86 m chain vs
    // 139.25°). Still short of the 0.34 L squat at 140°. Seed already
    // Maxes to groundedCap, so the extra shortening is live on the
    // first grounded Fall frame. The 0.972 trio already unweights the
    // sole (WALK-2 forbids a backward slide on µ=0.4). Air keeps 140°.
    // Catch / later cycles keep the envelope. Hip-unload hold is still
    // a no-op (ThighHold already owns the swing hip on Fall via fold
    // hold). Not trailing A2, not −swingHipFlex, not a PD retune, and
    // not another leftover-cap quarter.
    // 0 = former 139.25° grow cap on dirt.
    [Tooltip("Plant→Fall cycle-0: grow the grounded Fall/Reach fold a fifteenth quarter from 139.25° toward reachKneeFlex (139.5°). 0 = former 139.25° cap on dirt.")]
    public float walkPlantFallFoldGrowNearFirst = 1f;
    // WALK-2 initiation is knee lift + stance drive + forward lean. The
    // triangle, friction fade, ThighHold, pelvis tilt, stance-knee
    // straighten, rock-lead CoP, ankleScale, the 55° fold cap,
    // hipBalanceScale 1, suppressWalkLean, the 76° grow ceiling, the
    // fold-hold seed, the 0.625 lift pair, the matching 0.625 cap
    // floor, the 0.719…0.972 trio, PlusFirst, CapGrowFirst through
    // CapGrowAcrossFirst and FoldGrowMoreFirst through FoldGrowNearFirst
    // already survive Plant→Fall. Leftover-cap grow is spent (AcrossFirst
    // 0.045° / ~0.011 cm, fade stuck at 0.972 so liftSignal stays −0.945).
    // DualLoadMinSpan still never opens, liftGateBlend = 0, Fall→Reach
    // still waits on air. Nested 0.25 from Plant 55° is 55+85*(1−0.75^15)
    // ≈ 138.86°, not the rounded 139.5° comments. Another 0.25 would
    // write ~139.15° (~0.3 cm of a 0.86 m chain) and still miss the 0.34 L
    // squat at 140°. Hip-balance hold + suppressWalkLean already keep the
    // strut GrowFirst opened the ceiling for, so the remaining knee-lift
    // is the pin: groundedCap = reachKneeFlex. 2·L·cos(70°) ≈ 0.34 L
    // (~0.8 cm vs the nested 138.86°). Seed already Maxes to groundedCap,
    // so the shortening is live on the first grounded Fall frame. The
    // 0.972 trio already unweights the sole (WALK-2 forbids a backward
    // slide on µ=0.4). Air already keeps 140°. Catch / later cycles keep
    // the envelope. Hip-unload hold is still a no-op (ThighHold already
    // owns the swing hip on Fall via fold hold). Not leftover-cap, not
    // Clamp01 trio grow, not trailing A2, not −swingHipFlex, not a PD
    // retune, and not another millimetre 0.25.
    // 0 = former nested ~138.86° / claimed 139.5° grow cap on dirt.
    [Tooltip("Plant→Fall cycle-0: pin the grounded Fall/Reach fold to reachKneeFlex (140°). 0 = former nested ~138.86° cap on dirt.")]
    public float walkPlantFallFoldGrowReachFirst = 1f;
    // WALK-2 initiation is knee lift + stance drive + forward lean. The
    // triangle, friction fade, ThighHold, pelvis tilt, stance-knee
    // straighten, rock-lead CoP, ankleScale, the 55° fold cap,
    // hipBalanceScale 1, suppressWalkLean, the 76° grow ceiling, the
    // fold-hold seed, the 0.625 lift pair, the matching 0.625 cap
    // floor, the 0.719…0.972 trio, PlusFirst, CapGrowFirst through
    // CapGrowAcrossFirst and FoldGrowMoreFirst through FoldGrowReachFirst
    // already survive Plant→Fall. Leftover-cap grow is spent (AcrossFirst
    // 0.045° / ~0.011 cm, fade stuck at 0.972 so liftSignal stays −0.945).
    // DualLoadMinSpan still never opens, liftGateBlend = 0, Fall→Reach
    // still waits on air. The knee pin put 140° (0.34 L) on the first
    // grounded Fall frame; opening DualLoad to feed that unload would
    // Lerp dualSupportStandCap 0.40→0.55 at blend 0.25 — the recorded
    // cap fold — so the gate stays shut. The STANCE hip drive does not
    // read that cap. walkPlantStanceDriveFirst × walkPlantFallStanceDriveHoldFirst
    // already hold walkStanceHipExtend, but they multiply the DualLoad
    // 0.5 floor (airRelease 0, span never measured), so the leading hip
    // carries 0.15×0.5 = 0.075 of the 0.25 clamp — half the pelvis-forward
    // the 140° shin needs if the stacked sole is to unload. Same quarter
    // as fold grow: Lerp(0.5, 1, 0.25) = 0.625 on that load term only
    // (drive 0.09375, still under walkStanceHipExtendMax). firstRoom still
    // fades at plantComLeadMax so the hip does not stall the gate. Plant
    // clock 1 already writes the grown endpoint so EnterFall is not a
    // step. Catch / later cycles keep DualLoad / pushAfterAir. Not
    // leftover-cap, not Clamp01 trio grow, not another millimetre 0.25,
    // not trailing A2, not −swingHipFlex, not a DualLoad open, not a PD
    // retune, and not a board slot (walkMoveDir / walkTargetSpeed /
    // walkLatePush).
    // 0 = former 0.5-floor drive under the 140° shin.
    [Tooltip("Plant→Fall cycle-0: grow the stance-hip drive load a quarter from the DualLoad 0.5 floor toward 1 so the 140° shin is carried forward. 0 = former 0.5-floor drive.")]
    public float walkPlantFallStanceDriveGrowFirst = 1f;
    // WALK-2 initiation is knee lift + stance drive + forward lean. The
    // triangle, friction fade, ThighHold, pelvis tilt, stance-knee
    // straighten, rock-lead CoP, ankleScale, the 55° fold cap,
    // hipBalanceScale 1, suppressWalkLean, the 76° grow ceiling, the
    // fold-hold seed, the 0.625 lift pair, the matching 0.625 cap
    // floor, the 0.719…0.972 trio, PlusFirst, CapGrowFirst through
    // CapGrowAcrossFirst, FoldGrowMoreFirst through FoldGrowReachFirst
    // and StanceDriveGrowFirst already survive Plant→Fall. Leftover-cap
    // grow is spent (AcrossFirst 0.045° / ~0.011 cm, fade stuck at 0.972
    // so liftSignal stays −0.945). DualLoadMinSpan still never opens,
    // liftGateBlend = 0, Fall→Reach still waits on air. The knee pin put
    // 140° (0.34 L) on the first grounded Fall frame; opening DualLoad to
    // feed that unload would Lerp dualSupportStandCap 0.40→0.55 at blend
    // 0.25 — the recorded cap fold — so the gate stays shut. GrowFirst
    // raised the DualLoad 0.5 floor to 0.625 (drive 0.09375 of the 0.25
    // clamp). Leftover to the honest leading-foot load is 0.375; the 140°
    // shin still sits on a stacked sole. Same quarter on that leftover
    // only: Lerp(0.625, 1, 0.25) = 0.719 (drive 0.1078, still under
    // walkStanceHipExtendMax). firstRoom still fades at plantComLeadMax
    // so the hip does not stall the gate. Plant clock 1 already writes
    // the grown endpoint so EnterFall is not a step. Catch / later
    // cycles keep DualLoad / pushAfterAir. Not leftover-cap, not Clamp01
    // trio grow, not another millimetre 0.25, not trailing A2, not
    // −swingHipFlex, not a DualLoad open, not a PD retune, and not a
    // board slot (walkMoveDir / walkTargetSpeed / walkLatePush).
    // 0 = former 0.625-load drive under the 140° shin.
    [Tooltip("Plant→Fall cycle-0: grow the stance-hip drive load a second quarter from 0.625 toward 1 so the 140° shin is carried forward. 0 = former 0.625-load drive.")]
    public float walkPlantFallStanceDriveGrowMoreFirst = 1f;
    // WALK-2 initiation is knee lift + stance drive + forward lean. The
    // triangle, friction fade, ThighHold, pelvis tilt, stance-knee
    // straighten, rock-lead CoP, ankleScale, the 55° fold cap,
    // hipBalanceScale 1, suppressWalkLean, the 76° grow ceiling, the
    // fold-hold seed, the 0.625 lift pair, the matching 0.625 cap
    // floor, the 0.719…0.972 trio, PlusFirst, CapGrowFirst through
    // CapGrowAcrossFirst, FoldGrowMoreFirst through FoldGrowReachFirst,
    // StanceDriveGrowFirst and StanceDriveGrowMoreFirst already survive
    // Plant→Fall. Leftover-cap grow is spent (AcrossFirst 0.045° /
    // ~0.011 cm, fade stuck at 0.972 so liftSignal stays −0.945).
    // DualLoadMinSpan still never opens, liftGateBlend = 0, Fall→Reach
    // still waits on air. The knee pin put 140° (0.34 L) on the first
    // grounded Fall frame; opening DualLoad to feed that unload would
    // Lerp dualSupportStandCap 0.40→0.55 at blend 0.25 — the recorded
    // cap fold — so the gate stays shut. GrowMoreFirst raised the
    // DualLoad 0.625 load to 0.719 (drive 0.1078 of the 0.25 clamp).
    // Leftover to the honest leading-foot load is 0.281; the 140° shin
    // still sits on a stacked sole. Same quarter on that leftover only:
    // Lerp(0.719, 1, 0.25) = 0.789 (drive 0.1184, still under
    // walkStanceHipExtendMax). firstRoom still fades at plantComLeadMax
    // so the hip does not stall the gate. Plant clock 1 already writes
    // the grown endpoint so EnterFall is not a step. Catch / later
    // cycles keep DualLoad / pushAfterAir. Not leftover-cap, not Clamp01
    // trio grow, not another millimetre 0.25, not trailing A2, not
    // −swingHipFlex, not a DualLoad open, not a PD retune, and not a
    // board slot (walkMoveDir / walkTargetSpeed / walkLatePush).
    // 0 = former 0.719-load drive under the 140° shin.
    [Tooltip("Plant→Fall cycle-0: grow the stance-hip drive load a third quarter from 0.719 toward 1 so the 140° shin is carried forward. 0 = former 0.719-load drive.")]
    public float walkPlantFallStanceDriveGrowStillFirst = 1f;
    // WALK-2 initiation is knee lift + stance drive + forward lean. The
    // triangle, friction fade, ThighHold, pelvis tilt, stance-knee
    // straighten, rock-lead CoP, ankleScale, the 55° fold cap,
    // hipBalanceScale 1, suppressWalkLean, the 76° grow ceiling, the
    // fold-hold seed, the 0.625 lift pair, the matching 0.625 cap
    // floor, the 0.719…0.972 trio, PlusFirst, CapGrowFirst through
    // CapGrowAcrossFirst, FoldGrowMoreFirst through FoldGrowReachFirst,
    // StanceDriveGrowFirst through StanceDriveGrowStillFirst already
    // survive Plant→Fall. Leftover-cap grow is spent (AcrossFirst
    // 0.045° / ~0.011 cm, fade stuck at 0.972 so liftSignal stays
    // −0.945). DualLoadMinSpan still never opens, liftGateBlend = 0,
    // Fall→Reach still waits on air. The knee pin put 140° (0.34 L)
    // on the first grounded Fall frame; opening DualLoad to feed that
    // unload would Lerp dualSupportStandCap 0.40→0.55 at blend 0.25
    // — the recorded cap fold — so the gate stays shut. GrowStillFirst
    // raised the DualLoad 0.719 load to 0.789 (drive 0.1184 of the
    // 0.25 clamp). Leftover to the honest leading-foot load is 0.211;
    // the 140° shin still sits on a stacked sole. Pinning 0.789→1.0
    // is a 27% drive jump; leftover is not millimetres yet (knee pin
    // waited until 0.045°). Same quarter on that leftover only:
    // Lerp(0.789, 1, 0.25) = 0.842 (drive 0.1263, still under
    // walkStanceHipExtendMax). firstRoom still fades at plantComLeadMax
    // so the hip does not stall the gate. Plant clock 1 already writes
    // the grown endpoint so EnterFall is not a step. Catch / later
    // cycles keep DualLoad / pushAfterAir. Not leftover-cap, not Clamp01
    // trio grow, not another millimetre 0.25, not trailing A2, not
    // −swingHipFlex, not a DualLoad open, not a PD retune, and not a
    // board slot (walkMoveDir / walkTargetSpeed / walkLatePush).
    // 0 = former 0.789-load drive under the 140° shin.
    [Tooltip("Plant→Fall cycle-0: grow the stance-hip drive load a fourth quarter from 0.789 toward 1 so the 140° shin is carried forward. 0 = former 0.789-load drive.")]
    public float walkPlantFallStanceDriveGrowYetFirst = 1f;
    // WALK-2 initiation is knee lift + stance drive + forward lean. The
    // triangle, friction fade, ThighHold, pelvis tilt, stance-knee
    // straighten, rock-lead CoP, ankleScale, the 55° fold cap,
    // hipBalanceScale 1, suppressWalkLean, the 76° grow ceiling, the
    // fold-hold seed, the 0.625 lift pair, the matching 0.625 cap
    // floor, the 0.719…0.972 trio, PlusFirst, CapGrowFirst through
    // CapGrowAcrossFirst, FoldGrowMoreFirst through FoldGrowReachFirst,
    // StanceDriveGrowFirst through StanceDriveGrowYetFirst already
    // survive Plant→Fall. Leftover-cap grow is spent (AcrossFirst
    // 0.045° / ~0.011 cm, fade stuck at 0.972 so liftSignal stays
    // −0.945). DualLoadMinSpan still never opens, liftGateBlend = 0,
    // Fall→Reach still waits on air. The knee pin put 140° (0.34 L)
    // on the first grounded Fall frame; opening DualLoad to feed that
    // unload would Lerp dualSupportStandCap 0.40→0.55 at blend 0.25
    // — the recorded cap fold — so the gate stays shut. GrowYetFirst
    // raised the DualLoad 0.789 load to 0.842 (drive 0.1263 of the
    // 0.25 clamp). Leftover to the honest leading-foot load is 0.158;
    // the 140° shin still sits on a stacked sole. Pinning 0.842→1.0
    // is a 19% drive jump; leftover is not millimetres yet (knee pin
    // waited until 0.045°). Same quarter on that leftover only:
    // Lerp(0.842, 1, 0.25) = 0.881 (drive 0.1322, still under
    // walkStanceHipExtendMax). firstRoom still fades at plantComLeadMax
    // so the hip does not stall the gate. Plant clock 1 already writes
    // the grown endpoint so EnterFall is not a step. Catch / later
    // cycles keep DualLoad / pushAfterAir. Not leftover-cap, not Clamp01
    // trio grow, not another millimetre 0.25, not trailing A2, not
    // −swingHipFlex, not a DualLoad open, not a PD retune, and not a
    // board slot (walkMoveDir / walkTargetSpeed / walkLatePush).
    // 0 = former 0.842-load drive under the 140° shin.
    [Tooltip("Plant→Fall cycle-0: grow the stance-hip drive load a fifth quarter from 0.842 toward 1 so the 140° shin is carried forward. 0 = former 0.842-load drive.")]
    public float walkPlantFallStanceDriveGrowFarFirst = 1f;
    // WALK-2 initiation is knee lift + stance drive + forward lean. The
    // triangle, friction fade, ThighHold, pelvis tilt, stance-knee
    // straighten, rock-lead CoP, ankleScale, the 55° fold cap,
    // hipBalanceScale 1, suppressWalkLean, the 76° grow ceiling, the
    // fold-hold seed, the 0.625 lift pair, the matching 0.625 cap
    // floor, the 0.719…0.972 trio, PlusFirst, CapGrowFirst through
    // CapGrowAcrossFirst, FoldGrowMoreFirst through FoldGrowReachFirst,
    // StanceDriveGrowFirst through StanceDriveGrowFarFirst already
    // survive Plant→Fall. Leftover-cap grow is spent (AcrossFirst
    // 0.045° / ~0.011 cm, fade stuck at 0.972 so liftSignal stays
    // −0.945). DualLoadMinSpan still never opens, liftGateBlend = 0,
    // Fall→Reach still waits on air. The knee pin put 140° (0.34 L)
    // on the first grounded Fall frame; opening DualLoad to feed that
    // unload would Lerp dualSupportStandCap 0.40→0.55 at blend 0.25
    // — the recorded cap fold — so the gate stays shut. GrowFarFirst
    // raised the DualLoad 0.842 load to 0.881 (drive 0.1322 of the
    // 0.25 clamp). Leftover to the honest leading-foot load is 0.119;
    // the 140° shin still sits on a stacked sole. Pinning 0.881→1.0
    // is a 13% drive jump; leftover is not millimetres yet (knee pin
    // waited until 0.045°). Same quarter on that leftover only:
    // Lerp(0.881, 1, 0.25) = 0.911 (drive 0.1367, still under
    // walkStanceHipExtendMax). firstRoom still fades at plantComLeadMax
    // so the hip does not stall the gate. Plant clock 1 already writes
    // the grown endpoint so EnterFall is not a step. Catch / later
    // cycles keep DualLoad / pushAfterAir. Not leftover-cap, not Clamp01
    // trio grow, not another millimetre 0.25, not trailing A2, not
    // −swingHipFlex, not a DualLoad open, not a PD retune, and not a
    // board slot (walkMoveDir / walkTargetSpeed / walkLatePush).
    // 0 = former 0.881-load drive under the 140° shin.
    [Tooltip("Plant→Fall cycle-0: grow the stance-hip drive load a sixth quarter from 0.881 toward 1 so the 140° shin is carried forward. 0 = former 0.881-load drive.")]
    public float walkPlantFallStanceDriveGrowFartherFirst = 1f;
    // WALK-2 initiation is knee lift + stance drive + forward lean. The
    // triangle, friction fade, ThighHold, pelvis tilt, stance-knee
    // straighten, rock-lead CoP, ankleScale, the 55° fold cap,
    // hipBalanceScale 1, suppressWalkLean, the 76° grow ceiling, the
    // fold-hold seed, the 0.625 lift pair, the matching 0.625 cap
    // floor, the 0.719…0.972 trio, PlusFirst, CapGrowFirst through
    // CapGrowAcrossFirst, FoldGrowMoreFirst through FoldGrowReachFirst,
    // StanceDriveGrowFirst through StanceDriveGrowFartherFirst already
    // survive Plant→Fall. Leftover-cap grow is spent (AcrossFirst
    // 0.045° / ~0.011 cm, fade stuck at 0.972 so liftSignal stays
    // −0.945). DualLoadMinSpan still never opens, liftGateBlend = 0,
    // Fall→Reach still waits on air. The knee pin put 140° (0.34 L)
    // on the first grounded Fall frame; opening DualLoad to feed that
    // unload would Lerp dualSupportStandCap 0.40→0.55 at blend 0.25
    // — the recorded cap fold — so the gate stays shut. GrowFartherFirst
    // raised the DualLoad 0.881 load to 0.911 (drive 0.1367 of the
    // 0.25 clamp). Leftover to the honest leading-foot load is 0.089;
    // the 140° shin still sits on a stacked sole. Pinning 0.911→1.0
    // is a 10% drive jump; leftover is not millimetres yet (knee pin
    // waited until 0.045°). Same quarter on that leftover only:
    // Lerp(0.911, 1, 0.25) = 0.933 (drive 0.1400, still under
    // walkStanceHipExtendMax). firstRoom still fades at plantComLeadMax
    // so the hip does not stall the gate. Plant clock 1 already writes
    // the grown endpoint so EnterFall is not a step. Catch / later
    // cycles keep DualLoad / pushAfterAir. Not leftover-cap, not Clamp01
    // trio grow, not another millimetre 0.25, not trailing A2, not
    // −swingHipFlex, not a DualLoad open, not a PD retune, and not a
    // board slot (walkMoveDir / walkTargetSpeed / walkLatePush).
    // 0 = former 0.911-load drive under the 140° shin.
    [Tooltip("Plant→Fall cycle-0: grow the stance-hip drive load a seventh quarter from 0.911 toward 1 so the 140° shin is carried forward. 0 = former 0.911-load drive.")]
    public float walkPlantFallStanceDriveGrowFarthestFirst = 1f;
    // WALK-2 initiation is knee lift + stance drive + forward lean. The
    // triangle, friction fade, ThighHold, pelvis tilt, stance-knee
    // straighten, rock-lead CoP, ankleScale, the 55° fold cap,
    // hipBalanceScale 1, suppressWalkLean, the 76° grow ceiling, the
    // fold-hold seed, the 0.625 lift pair, the matching 0.625 cap
    // floor, the 0.719…0.972 trio, PlusFirst, CapGrowFirst through
    // CapGrowAcrossFirst, FoldGrowMoreFirst through FoldGrowReachFirst,
    // StanceDriveGrowFirst through StanceDriveGrowFarthestFirst already
    // survive Plant→Fall. Leftover-cap grow is spent (AcrossFirst
    // 0.045° / ~0.011 cm, fade stuck at 0.972 so liftSignal stays
    // −0.945). DualLoadMinSpan still never opens, liftGateBlend = 0,
    // Fall→Reach still waits on air. The knee pin put 140° (0.34 L)
    // on the first grounded Fall frame; opening DualLoad to feed that
    // unload would Lerp dualSupportStandCap 0.40→0.55 at blend 0.25
    // — the recorded cap fold — so the gate stays shut. GrowFarthestFirst
    // raised the DualLoad 0.911 load to 0.933 (drive 0.1400 of the
    // 0.25 clamp). Leftover to the honest leading-foot load is 0.067;
    // the 140° shin still sits on a stacked sole. Pinning 0.933→1.0
    // is a 7% drive jump; leftover is not millimetres yet (knee pin
    // waited until 0.045°). Same quarter on that leftover only:
    // Lerp(0.933, 1, 0.25) = 0.950 (drive 0.1425, still under
    // walkStanceHipExtendMax). firstRoom still fades at plantComLeadMax
    // so the hip does not stall the gate. Plant clock 1 already writes
    // the grown endpoint so EnterFall is not a step. Catch / later
    // cycles keep DualLoad / pushAfterAir. Not leftover-cap, not Clamp01
    // trio grow, not another millimetre 0.25, not trailing A2, not
    // −swingHipFlex, not a DualLoad open, not a PD retune, and not a
    // board slot (walkMoveDir / walkTargetSpeed / walkLatePush).
    // 0 = former 0.933-load drive under the 140° shin.
    [Tooltip("Plant→Fall cycle-0: grow the stance-hip drive load an eighth quarter from 0.933 toward 1 so the 140° shin is carried forward. 0 = former 0.933-load drive.")]
    public float walkPlantFallStanceDriveGrowOutFirst = 1f;
    // WALK-2 initiation is knee lift + stance drive + forward lean. The
    // triangle, friction fade, ThighHold, pelvis tilt, stance-knee
    // straighten, rock-lead CoP, ankleScale, the 55° fold cap,
    // hipBalanceScale 1, suppressWalkLean, the 76° grow ceiling, the
    // fold-hold seed, the 0.625 lift pair, the matching 0.625 cap
    // floor, the 0.719…0.972 trio, PlusFirst, CapGrowFirst through
    // CapGrowAcrossFirst, FoldGrowMoreFirst through FoldGrowReachFirst,
    // StanceDriveGrowFirst through StanceDriveGrowOutFirst already
    // survive Plant→Fall. Leftover-cap grow is spent (AcrossFirst
    // 0.045° / ~0.011 cm, fade stuck at 0.972 so liftSignal stays
    // −0.945). DualLoadMinSpan still never opens, liftGateBlend = 0,
    // Fall→Reach still waits on air. The knee pin put 140° (0.34 L)
    // on the first grounded Fall frame; opening DualLoad to feed that
    // unload would Lerp dualSupportStandCap 0.40→0.55 at blend 0.25
    // — the recorded cap fold — so the gate stays shut. GrowOutFirst
    // raised the DualLoad 0.933 load to 0.950 (drive 0.1425 of the
    // 0.25 clamp). Leftover to the honest leading-foot load is 0.050;
    // the 140° shin still sits on a stacked sole. Pinning 0.950→1.0
    // is a 5% drive jump; leftover is not millimetres yet (knee pin
    // waited until 0.045°). Same quarter on that leftover only:
    // Lerp(0.950, 1, 0.25) = 0.962 (drive 0.1444, still under
    // walkStanceHipExtendMax). firstRoom still fades at plantComLeadMax
    // so the hip does not stall the gate. Plant clock 1 already writes
    // the grown endpoint so EnterFall is not a step. Catch / later
    // cycles keep DualLoad / pushAfterAir. Not leftover-cap, not Clamp01
    // trio grow, not another millimetre 0.25, not trailing A2, not
    // −swingHipFlex, not a DualLoad open, not a PD retune, and not a
    // board slot (walkMoveDir / walkTargetSpeed / walkLatePush).
    // 0 = former 0.950-load drive under the 140° shin.
    [Tooltip("Plant→Fall cycle-0: grow the stance-hip drive load a ninth quarter from 0.950 toward 1 so the 140° shin is carried forward. 0 = former 0.950-load drive.")]
    public float walkPlantFallStanceDriveGrowOutMoreFirst = 1f;
    // WALK-2 initiation is knee lift + stance drive + forward lean. The
    // triangle, friction fade, ThighHold, pelvis tilt, stance-knee
    // straighten, rock-lead CoP, ankleScale, the 55° fold cap,
    // hipBalanceScale 1, suppressWalkLean, the 76° grow ceiling, the
    // fold-hold seed, the 0.625 lift pair, the matching 0.625 cap
    // floor, the 0.719…0.972 trio, PlusFirst, CapGrowFirst through
    // CapGrowAcrossFirst, FoldGrowMoreFirst through FoldGrowReachFirst
    // and StanceDriveGrowFirst through StanceDriveGrowOutMoreFirst already
    // survive Plant→Fall. Leftover-cap grow is spent (AcrossFirst
    // 0.045° / ~0.011 cm, fade stuck at 0.972 so liftSignal stays
    // −0.945). DualLoadMinSpan still never opens, liftGateBlend = 0,
    // Fall→Reach still waits on air. The knee pin put 140° (0.34 L)
    // on the first grounded Fall frame; opening DualLoad to feed that
    // unload would Lerp dualSupportStandCap 0.40→0.55 at blend 0.25
    // — the recorded cap fold — so the gate stays shut. GrowOutMoreFirst
    // raised the DualLoad 0.950 load to 0.962 (drive 0.1444 of the
    // 0.25 clamp). Leftover to the honest leading-foot load is 0.038;
    // the 140° shin still sits on a stacked sole. Pinning 0.962→1.0
    // is a 4% drive jump; leftover is not millimetres yet (knee pin
    // waited until 0.045°). Same quarter on that leftover only:
    // Lerp(0.962, 1, 0.25) = 0.972 (drive 0.1457, still under
    // walkStanceHipExtendMax). firstRoom still fades at plantComLeadMax
    // so the hip does not stall the gate. Plant clock 1 already writes
    // the grown endpoint so EnterFall is not a step. Catch / later
    // cycles keep DualLoad / pushAfterAir. Not leftover-cap, not Clamp01
    // trio grow, not another millimetre 0.25, not trailing A2, not
    // −swingHipFlex, not a DualLoad open, not a PD retune, and not a
    // board slot (walkMoveDir / walkTargetSpeed / walkLatePush).
    // 0 = former 0.962-load drive under the 140° shin.
    [Tooltip("Plant→Fall cycle-0: grow the stance-hip drive load a tenth quarter from 0.962 toward 1 so the 140° shin is carried forward. 0 = former 0.962-load drive.")]
    public float walkPlantFallStanceDriveGrowOutStillFirst = 1f;
    // WALK-2 initiation is knee lift + stance drive + forward lean. The
    // triangle, friction fade, ThighHold, pelvis tilt, stance-knee
    // straighten, rock-lead CoP, ankleScale, the 55° fold cap,
    // hipBalanceScale 1, suppressWalkLean, the 76° grow ceiling, the
    // fold-hold seed, the 0.625 lift pair, the matching 0.625 cap
    // floor, the 0.719…0.979 trio, PlusFirst, CapGrowFirst through
    // CapGrowAcrossFirst, FoldGrowMoreFirst through FoldGrowReachFirst
    // and StanceDriveGrowFirst through StanceDriveGrowOutStillFirst already
    // survive Plant→Fall. Leftover-cap grow is spent (AcrossFirst
    // 0.045° / ~0.011 cm, fade stuck at 0.972 so liftSignal stays
    // −0.945). DualLoadMinSpan still never opens, liftGateBlend = 0,
    // Fall→Reach still waits on air. The knee pin put 140° (0.34 L)
    // on the first grounded Fall frame; opening DualLoad to feed that
    // unload would Lerp dualSupportStandCap 0.40→0.55 at blend 0.25
    // — the recorded cap fold — so the gate stays shut. GrowOutStillFirst
    // raised the DualLoad 0.962 load to 0.972 (drive 0.1457 of the
    // 0.25 clamp). Leftover to the honest leading-foot load is 0.028;
    // the 140° shin still sits on a stacked sole. Pinning 0.972→1.0
    // is a 3% drive jump; leftover is not millimetres yet (knee pin
    // waited until 0.045°). Same quarter on that leftover only:
    // Lerp(0.972, 1, 0.25) = 0.979 (drive 0.1468, still under
    // walkStanceHipExtendMax). firstRoom still fades at plantComLeadMax
    // so the hip does not stall the gate. Plant clock 1 already writes
    // the grown endpoint so EnterFall is not a step. Catch / later
    // cycles keep DualLoad / pushAfterAir. Not leftover-cap, not Clamp01
    // trio grow, not another millimetre 0.25, not trailing A2, not
    // −swingHipFlex, not a DualLoad open, not a PD retune, and not a
    // board slot (walkMoveDir / walkTargetSpeed / walkLatePush).
    // 0 = former 0.972-load drive under the 140° shin.
    [Tooltip("Plant→Fall cycle-0: grow the stance-hip drive load an eleventh quarter from 0.972 toward 1 so the 140° shin is carried forward. 0 = former 0.972-load drive.")]
    public float walkPlantFallStanceDriveGrowOutYetFirst = 1f;
    // 22:44 finished the Plant pose, then Fall threw it away. EnterFall
    // resets PhaseAge, so Fall's fold / hipLift / ankleLift clocks start at
    // 0: a Plant that already wrote swingKneeFlex (55°) is unfolded on the
    // first 5 ms Fall frame (reachKnee 55→0). That is the same PhaseAge-reset
    // family as the vault / arm clocks, and it is why Plant lift never
    // survived into the phase whose job is the lift: reachKneeFolding drops,
    // DrivePelvis comes back on the trailing hip, the shin lengthens, the
    // foot is on dirt, Fall→Reach waits on air. Mix, not a new gain: Fall
    // and early Reach (extend still 0) keep Max(clocked, last Plant pose)
    // until the 2× ramp overtakes. Reach must not hold past ReachExtendProgress
    // — that lerp is landDeg, and Max with 55° would fight the plant. Catch
    // is already on landDeg. 0 = former Fall start at 0.
    [Tooltip("Fall/Reach: keep the Plant swing fold/lift until the Fall clock overtakes it. 0 = former PhaseAge-reset unfold.")]
    public float walkPlantFallFoldHold = 1f;
    // Third consumer of that same ramp, and the only one still nailed to its
    // floor. dualSupportSwingScale softens THREE commands — swing unload,
    // swing knee and the unloading pelvis tilt — but only the first two read
    // the ramp (unloadSoft = Lerp(dualSoft, 1, liftGateBlend)); the tilt reads
    // the bare floor, so standLegPelvisTilt arrives as 10° x 0.32 = 3.2° even
    // at the instant the swing foot is at full clearance and the pelvis is
    // free to tilt. The tilt is not a cosmetic assist: hip flexion alone
    // raises the thigh relative to the pelvis, while tilting the pelvis
    // raises the whole swing hip, which is the term that buys ground
    // clearance at the foot without asking the swing knee for more flex
    // (more knee shortens the leg the placement solver has to land on).
    // Same argument as the cap and unload: the floor is a guard against
    // unloading a leg that still carries weight, and a foot in the air
    // carries none. At contact airRelease is 0, so the tilt reads exactly
    // dualSupportSwingScale as before and the recorded 0.55 / 0.70 folds
    // (which raised the dual-support floor) are untouched. Tilt therefore
    // ends up quadratic in the release, since standLegLevel already ramps
    // with it through the cap — the same shape unload has, gentle where the
    // weight is and full only in flight.
    [Tooltip("Fall-catch: swing-foot air release lifts the pelvis-tilt softener too. 0 = frozen dualSupportSwingScale floor.")]
    public float walkStandTiltAirRelease = 1f;
    // Extra swing unload/knee scale on walk-split on top of dualSoft. Tilt
    // stays on dualSoft — otherwise fold after swap. 1 = as now.
    [Tooltip("Extra unload/knee scale on walk-split (not for tilt). 1 = off.")]
    public float walkSwingLiftScale = 1f;
    // Near the end of Stance — walkSingleSupport. Early stance-only drops.
    [Tooltip("Seconds of Stance before lift starts.")]
    public float walkStanceLiftDelay = 6f;
    [Tooltip("Seconds of liftAge ramp after the delay.")]
    public float walkStanceLiftRamp = 2f;
    // Separate from liftRamp: speeding weight (1.5) worsened swing (0.57)
    // and sat (opt5). Default = liftRamp; CLI -walkWeightRamp for sweeps.
    [Tooltip("Seconds of weightBlend (unload/cap) ramp in Stance with CoM on support.")]
    public float walkWeightRamp = 2f;
    // Run: shorter delay/ramp, else lift cannot finish at runStance 4.
    [Tooltip("walkStanceLiftDelay when intent.run=1.")]
    public float runStanceLiftDelay = 2f;
    [Tooltip("walkWeightRamp when intent.run=1.")]
    public float runWeightRamp = 1.5f;
    // Lift only when CoM is over the stance foot, not by timer alone:
    // else delay=4 + lift=2.5 still folds (~22 s, walk_l25_d4).
    [Tooltip("|CoM−stance| ≤ this on walk dual-support to enable lift.")]
    public float walkLiftComMax = 0.10f;
    // Swing toe-off only on walk at high liftBlend; global
    // swingAnkleGroundedToeOff on dual-support drops (~17 s).
    [Tooltip("Max swing toe-off on walk at liftBlend=1, deg. Forward swing only. 0 = off.")]
    public float walkSwingToeOffMax = 0f;
    // Extra unload and knee flex from liftAct — CLI only; default 0.
    [Tooltip("Extra hip-unload at liftAct=1 on walk-split.")]
    public float walkLiftUnloadBias = 0.02f;
    [Tooltip("Fraction of swingKneeFlex on the grounded swing at liftAct=1.")]
    public float walkKneePeelMax = 0.05f;
    // Stance-foot push at full weightBlend: CoM over support alone
    // does not lift the swing (ankleBalance≈0). Not a PD to an angle —
    // only a signal into ankleBalance. 0 = off. CLI -walkStancePush.
    [Tooltip("Extra plantar on the stance ankle at weightBlend≈1. 0 = off.")]
    public float walkStancePush = 0.05f;
    [Tooltip("walkStancePush scale for run. 0 = no push-off in run.")]
    public float runStancePushScale = 0f;
    // Hip half of push-off. The ankle rolls the body over the toe; the hip
    // carries the pelvis over the planted foot. Both hips only held the pelvis
    // upright, so the stance leg never drove the trunk forward and the step
    // ended under the CoM. Extension is plus on this joint. Gated on the same
    // load × CoM-lead window as the ankle push and clamped, so it cannot turn
    // into a hip that outruns its own foot. 0 = former pelvis-only stance hip.
    [Tooltip("Stance-hip extension bias at full load and full CoM lead. 0 = off.")]
    public float walkStanceHipExtend = 0.15f;
    [Tooltip("Clamp on the stance-hip extension bias.")]
    public float walkStanceHipExtendMax = 0.25f;
    // walkStancePush / walkStanceHipExtend sat behind pushAfterAir, which on
    // fall-catch is SwingWasAirborne: Plant/Fall/Reach report PhaseCode 0, so
    // the stance ankle and stance hip cannot push until the swing has already
    // left. That is the right closed-chain ban for a feet-together Stance,
    // and the wrong one here — the feet are a step apart, CoM past the stance
    // foot is exactly the geometry where plantarflexion and hip extension
    // roll the body forward, and waiting for air is the stall: no push → no
    // travel → swing never unloads → no air. Signed stanceLead is the same
    // window those two levers already multiply by; this only lets that
    // product be non-zero. 0 = former airborne latch.
    [Tooltip("Stance push-off and hip extend may fire once CoM has passed the stance foot, even if the swing is still down. Legacy Stance ramps this with the geo-lift clock. 0 = former airborne-only gate.")]
    public float walkStancePushOnLead = 1f;
    // Double-support push-off. Plant is the longest phase of the cycle and
    // the only window where the trailing foot is still down with CoM already
    // past it — so its plantarflexion is the one moment that puts CoP behind
    // CoM and rolls the body over the leading foot. The leading ankle and the
    // stance hip cannot do it: CoM sits behind them right after a catch, and
    // pushing a foot the body has not reached yet brakes. Both are correctly
    // gated off by the signed lead window, which left the whole double-support
    // phase with no propulsion at all — the body waits out plantMinDuration
    // and Fall starts from wherever the catch left it. The trailing ankle got
    // the shared support-midpoint error instead (≈0 on a settled Plant), so it
    // idled. This is the A2 burst of a real step, routed the same way Fall and
    // Reach already route that foot: a signal into ankleBalance, not an
    // ankle-angle PD (balance-actuators.mdc). 0 = former idle Plant ankle.
    [Tooltip("Trailing-ankle push-off in double-support Plant. 0 = off.")]
    public float walkPlantPushScale = 0.35f;
    [Tooltip("Rear moment arm where the Plant push reaches full scale, m.")]
    public float walkPlantPushLeadWindow = 0.08f;
    // The forward side of the Plant CoM setpoint, and the only phase that
    // never had one. StepPhaseDriver opens Plant→Fall on the EXTRAPOLATED CoM
    // reaching fallComLeadTrigger (0.015 m) ahead of the stance foot, while
    // fc.comLead in Plant is ≤ 0 by construction: ApplyRecoverBrake asks for
    // −catchComLead (0.10 m BEHIND the foot) on a tipping trunk and fades to
    // exactly 0 on a settled one. So the stance ankle spends a calm Plant
    // regulating the CoM — and, through walkXCoMWeight, the very capture point
    // the gate reads — onto the foot centre with the full ankle loop
    // (ankleComP 14 / ankleComD 8). The phase waits for a roll-over its own
    // setpoint forbids: the gate can then only open on the
    // plantLeadWaitSeconds deadline, and Fall starts from a body at rest with
    // the heel (0.13 m) as its whole moment arm — the shuffle
    // fallComLeadTrigger was written to prevent.
    // This is not a new actuator and not another push. Moving the setpoint
    // ahead of the foot makes the same ankle loop dorsiflex, CoP goes behind
    // the CoM, and the body rolls onto the ball of the foot. That is how a
    // real body starts a step (the CoP moves backward first), and double
    // support is the cheapest place to pay for it: both feet are down, so the
    // reaction is shared. 0 = former hold-on-the-foot Plant.
    [Tooltip("Forward CoM setpoint over the stance foot during Plant, m — the roll-over the Plant→Fall gate waits for. 0 = off.")]
    public float walkPlantRockLead = 0.03f;
    // The trunk half of that same roll-over, and the half Plant still does not
    // have at all. Fall leans the trunk forward (fallForwardLeanScale) and
    // Reach applies 0.75 × fallPelvisLean; Plant sets suppressWalkLean, which
    // zeroes both the base walk lean and the speed lean, and until the landing
    // brake fades it commands an actively BACKWARD one. So the longest phase of
    // the cycle — the phase whose exit gate is literally "the extrapolated CoM
    // has reached fallComLeadTrigger ahead of the stance foot" — is the only
    // phase that does nothing to move that CoM.
    // walkPlantRockLead moves the CoP out from under the mass; this moves the
    // mass. They are not the same lever and neither replaces the other: the
    // ankle setpoint works through one sole (0.13 m of heel behind the foot
    // centre), while the trunk carries torso + head + arms — about half the
    // body at roughly 0.35 m above the hip, so a 9° lean is ~2.7 cm of CoM
    // travel against a 1.5 cm trigger.
    // The third-law objection is already answered in this file for Fall: a
    // forward trunk lean drives the pelvis back through the joint pair, and
    // that reaction is affordable only while BOTH feet are down. Plant is the
    // other double-support phase, and unlike Fall it is double support for its
    // whole length — so the recorded argument applies here with more room, not
    // less.
    // Size, clock and guards are the ones this phase already owns: the amount
    // is driver.fallForwardLeanScale × fallPelvisLean, so the trunk never leans
    // further than the neighbouring phase already commands; it ramps in over
    // plantMinDuration exactly as the rock setpoint does (a step in a pelvis
    // target is the sci_a shove); it carries the same two-sided calm, so a
    // trunk tipping either way collapses it; and it fades to exactly zero as
    // the extrapolated lead reaches the trigger. That last one is what keeps it
    // one-sided: the lean cannot roll the body past plantComLeadMax (which
    // would shut the gate and arm the recovery ladder), and the Plant→Fall edge
    // hands over at zero, where Fall's own lean ramp starts, instead of
    // stepping. 0 = former upright / braked Plant.
    [Tooltip("Forward trunk lean in Plant as a fraction of the Fall lean, faded out as the extrapolated CoM reaches the Plant→Fall trigger. 0 = former upright Plant.")]
    public float walkPlantLeanRoll = 1f;
    // walkPlantLeanRoll still skipped cycle 0. That was to keep the recorded
    // feet-together first step, and it left gait initiation without the trunk
    // half of the roll-over: rock lead already fires on cycle 0 (the leading
    // ankle), trailing A2 / hip still need a rear moment arm the quiet stand
    // does not have, and suppressWalkLean zeroes the base walk lean. So the
    // first Fall started from a body at rest — the shuffle fallComLeadTrigger
    // was written to prevent — while every later Plant already leaned 9°.
    // WALK-2 initiation is knee lift + stance drive + forward lean, and the
    // lean is the only one of those three that is legal with both feet under
    // the hip: third-law reaction is shared, amount / clock / calm / fade are
    // the same numbers cycle > 0 already uses, and Fall still receives zero
    // at the edge. 0 = former cycle > 0 only.
    [Tooltip("Plant trunk lean also on cycle 0 (gait initiation). 0 = former later-cycle only.")]
    public float walkPlantLeanFirst = 1f;
    // The hip half of the Plant push-off, and the last half of that mechanism
    // this phase is missing. walkPlantPushScale gave the trailing ANKLE its A2
    // burst; the trailing HIP still gets nothing — exactly the hole the ankle
    // was in before that lever. walkStanceHipExtend does not cover it twice
    // over: it routes to the LEADING leg, and its envelope is pushWindow, which
    // is zero through the whole early Plant because the CoM sits BEHIND the foot
    // that was just caught. So in the longest phase of the cycle one joint pair
    // of the rear leg pushes and the other idles.
    // Geometry says the rear hip is the one holding the moment arm: the
    // trailing foot is already behind the CoM (that is the quantity
    // walkPlantPushLeadWindow measures), and extension — plus on this joint
    // (balance-actuators.mdc) — rotates the thigh's lower end back; against a
    // planted foot the reaction carries the pelvis forward instead. That is
    // pre-swing hip drive, routed through the same counter channel
    // walkStanceHipExtend already uses: a bias inside the pelvis loop's own
    // signal, not a new actuator and not a hip-angle PD.
    // The rear leg is also the SWING-role leg, so this bias lands on a joint
    // that already carries swingUnload (a flexor bias). Hence the dosing: a
    // fraction of the accepted stance-hip number rather than a gain of its own,
    // so it can never command more than the neighbouring lever; the same
    // plantComLeadMax room the ankle burst fades on; and death as the rear foot
    // begins to clear — the moment that foot is leaving, its hip belongs to the
    // lift, not to the push. 0 = former idle Plant hip.
    [Tooltip("Trailing-hip extension in double-support Plant, as a fraction of walkStanceHipExtend. 0 = off.")]
    public float walkPlantHipExtend = 1f;
    // A toe scrape is a foot on the ground, not a foot in flight. Fall/Reach/
    // Catch compute a real command for that foot — the propulsive half of its
    // own CoM loop plus the ankleLift dorsiflexor that unweights it — and then
    // DriveSwingOrStanceAnkle threw all of it away: heelAirPose forces the
    // airborne branch on an unloaded heel, and world-flat returns before
    // ControlAnkleForBalance is ever reached. So on the one contact state the
    // step needs to break, the ankle ran an angle PD to a world pose instead.
    // That construct is banned on a grounded foot for a measured reason
    // (balance-actuators.mdc): the sole is pressed to the dirt, so the torque
    // turns the whole body rather than the joint. The original point of
    // heelAirPose — do not inherit a planted CoM loop through the long end of
    // the sole — is already paid for upstream, where the scrape signal is
    // clamped to its propulsive half, so the heel press it guarded against
    // cannot come back. Only fires when that block actually wrote a command;
    // an uncommanded scrape keeps the air pose. 0 = the former world-flat.
    [Tooltip("Route the Fall/Reach/Catch scrape command to a grounded swing ankle instead of the airborne world-flat pose. 0 = former pose.")]
    public float walkScrapeAnkleDrive = 1f;
    [Tooltip("Target forward COM speed in walk, m/s.")]
    public float walkTargetSpeed = 1.0f;
    [Tooltip("Additional stance push per (target-current) speed, 1/s.")]
    public float walkSpeedPushGain = 0.35f;
    [Tooltip("Clamp for speed-based extra stance push.")]
    public float walkSpeedPushMax = 0.25f;
    [Tooltip("Allow stance push while ankle balance is slightly negative.")]
    public float walkPushCounterAllowance = 0.20f;
    [Tooltip("Phase-envelope propulsion gain on walk stance.")]
    public float walkPropulsionGain = 0f;
    [Tooltip("Clamp for phase-envelope propulsion signal.")]
    public float walkPropulsionMax = 0f;
    [Tooltip("Stance phase progress where propulsion starts (0..1).")]
    public float walkPropulsionStart = 0.30f;
    [Tooltip("Stance phase progress where propulsion peaks (0..1).")]
    public float walkPropulsionPeak = 0.65f;
    [Tooltip("Stance phase progress where propulsion fades to zero (0..1).")]
    public float walkPropulsionEnd = 0.92f;
    [Tooltip("Disable propulsion if absolute torso tilt is above this angle, deg.")]
    public float walkPropulsionTiltGuardDeg = 12f;
    [Tooltip("Enable propulsion only when |CoM-stance| is within this window, m.")]
    public float walkPropulsionComWindow = 0.08f;
    [Tooltip("Minimum weightBlend for propulsion to reach stance ankle.")]
    public float walkPropulsionMinWeight = 0.15f;
    [Tooltip("Extra counter-allowance while applying propulsion push.")]
    public float walkPropulsionCounterAllowance = 0.15f;
    [Tooltip("Multiplier for |stanceComOff| gate when speed-push is active.")]
    public float walkSpeedPushComGateScale = 2f;
    // The stance push-off carried a hard boolean CoM gate, comLiftGate >= 1,
    // i.e. lead <= walkLiftComMax (0.10 m). comLiftGate is the swing-LIFT
    // gate — it guards unloading a leg that still carries weight — and
    // borrowing it here switched plantarflexion off exactly where it is most
    // propulsive. Geometry disagrees: stanceComOff is measured from the foot
    // CENTRE, and the toe edge sits footSize.x/2 = 0.13 m ahead of it, so CoP
    // can still be placed ahead of CoM up to that lead. A 0.22 m step landing
    // catchFootLead 0.08 m ahead of CoM puts the whole Reach/Catch lead range
    // at roughly 0…0.14 m, so the only push the body has died over most of it.
    // Past the toe there is no sole left to press and the torque merely
    // rotates the body about the toe — that is the m·g·arm ceiling
    // (balance-actuators.mdc), not a knob. So fade to zero AT the toe instead
    // of stepping to zero at 0.10 m: dropping a saturated ankle from full
    // push to nothing in one 5 ms step is a torque step, the same shape that
    // threw the pelvis back in sci_a. 1 = fade to the toe edge.
    // 0 = the former hard cut.
    [Tooltip("Stance push-off fade from walkLiftComMax toward the toe edge. 0 = former hard cut at walkLiftComMax.")]
    public float walkPushToeFade = 1f;
    [Tooltip("Use XCoM in stance ankle error (0=CoM, 1=full XCoM).")]
    public float walkXCoMWeight = 0.6f;
    [Tooltip("Effective COM height (m) for XCoM omega0 = sqrt(g/h).")]
    public float walkXCoMHeight = 1.0f;
    // Every capture-point reader on the walk path extrapolates position by
    // v/omega0 — 0.32 s of CoM velocity at walkXCoMHeight 1 m: the stance
    // ankle shift above, the Plant->Fall lead gate
    // (StepPhaseDriver.StanceLeadCapture) and the step target
    // (ActiveCatchFootLead). The velocity all three read is a one-step
    // difference of CoM position at 200 Hz, so a heel strike — which really
    // does arrest a whole leg inside one 5 ms step — swings the capture point
    // by centimetres in a single tick. That arrives at the ankle setpoint as
    // a torque step (the shape that walked the trunk back 1.11 m in sci_a) and
    // at the phase gate as chatter, at the one instant of the cycle where the
    // trunk is least protected. The same sample punches a transient hole in
    // walkSpeedError, which six terms read (lean, swing hip, knee, step
    // length, push, drive).
    // The point mass of the LIP model has no legs: what the extrapolation
    // wants is the sustained momentum of the body, not the impulse of one limb
    // landing. A first-order lag supplies it, and it is deliberately ONE
    // shared reading — "when the leg is released", "where it lands" and "how
    // hard the ankle presses" must judge the same number, not three copies of
    // a spiky one. The quiet-stance ankle keeps the raw sample below, so
    // nothing outside walk moves. 0 = the former raw velocity everywhere.
    [Tooltip("Low-pass time constant for the CoM velocity read by walk capture-point terms, s. 0 = raw one-step velocity.")]
    public float walkComVelFilterTau = 0.06f;
    private float walkComVelFiltered;
    private bool hasWalkComVelSample;
    [Tooltip("Auto lean gain from walk speed error, deg per (m/s).")]
    public float walkSpeedLeanGain = 8f;
    [Tooltip("Clamp for auto lean generated by speed error, deg.")]
    public float walkSpeedLeanMax = 6f;
    [Tooltip("Torso share of auto lean relative to pelvis lean.")]
    public float walkSpeedLeanTorsoScale = 1.2f;
    [Tooltip("Direct stance-ankle drive gain from walk speed error, 1/s.")]
    public float walkSpeedDriveGain = 0f;
    [Tooltip("Clamp for direct speed drive applied to stance ankle.")]
    public float walkSpeedDriveMax = 0.5f;
    [Tooltip("Minimum standLegLevel before direct speed drive applies.")]
    public float walkSpeedDriveStartLevel = 0.2f;
    [Tooltip("Additional swingHipFlex per 1 m/s speed error, deg.")]
    public float walkSpeedSwingHipGain = 10f;
    [Tooltip("Additional swingKneeFlex per 1 m/s speed error, deg.")]
    public float walkSpeedSwingKneeGain = 14f;
    [Tooltip("Clamp for speed-based swing flex boost, deg.")]
    public float walkSpeedSwingFlexMax = 16f;
    [Tooltip("Base extra swing hip flex on walk, deg.")]
    public float walkSwingHipBoost = 6f;
    [Tooltip("Base extra swing knee flex on walk, deg.")]
    public float walkSwingKneeBoost = 10f;
    [Tooltip("Desired step length from stance to swing foot on walk, m.")]
    public float walkStepLength = 0.22f;
    [Tooltip("Extra desired step length per 1 m/s target speed, m.")]
    public float walkStepLengthSpeedGain = 0f;
    [Tooltip("Hip target correction from step-length error, deg per m.")]
    public float walkStepPlacementGain = 30f;
    [Tooltip("Clamp for step-placement hip correction, deg.")]
    public float walkStepPlacementMax = 10f;
    [Tooltip("Fraction of step-placement correction applied while grounded.")]
    public float walkStepPlacementGroundFraction = 0f;
    // Landing behind the CoM is what turns a step into a stumble: the new
    // stance foot starts with the body already past it, so the catch spends
    // the next cycle recovering instead of travelling. The placement floor is
    // the capture point plus this margin. 0 = step length only.
    [Tooltip("Metres the landing foot must clear the capture point by, m.")]
    public float walkStepComMargin = 0.06f;
    // walkStepLength was a FLOOR under the capture step, so every fall-catch
    // step over-strode. Foot placement decides how much of the body's forward
    // speed survives the step: land exactly on the capture point
    // (CoM + comVel/omega0) and the body arrives at the top of the new arc with
    // zero speed; land short of it and residual speed carries the roll on; land
    // BEYOND it and the body stops before the top and rocks back. A slow body
    // has its capture point about 0.11 m ahead of the stance foot, so a 0.22 m
    // floor planted the foot roughly a capture point too far every single
    // cycle. That is a stop commanded once per step, which is why adding
    // push-off and lift levers could not produce travel. Stride length is an
    // anatomical CAP, not a target: the target is the capture step, and it
    // grows with speed by itself through xcomShift. 0 = the former floor.
    [Tooltip("1 = step target is the capture point (walkStepLength caps it). 0 = former floor.")]
    public float walkStepCaptureTarget = 1f;
    // Only a guard against a degenerate (negative) capture step putting the
    // foot behind the stance foot. Body colliders ignore each other, so a
    // short step is geometrically free.
    [Tooltip("Shortest commanded step from stance to swing foot, m.")]
    public float walkStepMinLength = 0.08f;
    // How far ahead of the CoM the foot lands is written twice, against two
    // different references, and the two disagree. Here the command is the
    // capture point plus walkStepComMargin, so the commanded lead over the CoM
    // is xcomShift + 0.06 — about 0.062 m on a body at 6 mm/s. The fall-catch
    // FSM judges the same landing with StepPhaseDriver.catchFootLead = 0.08 m
    // (IsSwingFootAheadOfCom, same foot centre, same CoM), so the plant this
    // gait commands cannot satisfy the exit it is aiming for: Catch then leaves
    // only through trunkCaught, which waits for the trunk to settle, or times
    // out into EnterStandingPlant. That is standing between plants, not a
    // caught fall, and it is the shape of the recorded 0.208 m of travel.
    // Both edges of the admissible band are geometry, not tuning: below
    // catchFootLead the plant has no moment arm about the CoM, and past the
    // heel edge (footSize.x/2 = 0.13 m from the foot centre) the ankle can no
    // longer put CoP behind the CoM at all, so the next Plant→Fall has nothing
    // to topple with. catchFootLead + pad = 0.10 m sits inside that band.
    // The pad is the CoM's own advance during the last centimetres of descent:
    // the command is issued in flight, the criterion is read at touchdown.
    // One-sided (Max), so a fast body keeps its longer capture step and no
    // accepted stance or push number can be exceeded through this path.
    // 0 = the former command, blind to the criterion.
    [Tooltip("1 = commanded landing point also meets StepPhaseDriver.catchFootLead. 0 = former capture-point-only command.")]
    public float walkStepCatchLeadSync = 1f;
    [Tooltip("Extra metres past catchFootLead in the commanded landing point (CoM advance during the last descent), m.")]
    public float walkStepCatchLeadPad = 0.02f;
    [Tooltip("Stance phase progress when placement control becomes active.")]
    public float walkPlacementSyncStart = 0.72f;
    [Tooltip("Ground clearance threshold (m) for touchdown-sync window.")]
    public float walkTouchdownSyncClearance = 0.06f;
    // Late swing had no velocity match. The hip held the placement target and
    // the shin extended toward reachKneeLandDeg, so the foot was still
    // travelling forward at the moment it met the ground. The ground charges
    // twice for that, and neither charge is a gain to be tuned away.
    // (1) A sole that lands moving forward is a brake: friction acts backward
    // on it at µ=0.4 and eats the forward momentum the whole cycle just built.
    // (2) Heel-strike collision loss scales with the square of that relative
    // speed, which is the largest single energy sink in any walking model.
    // Retraction is also the one swing-hip action whose third-law reaction
    // helps rather than hurts: the leg's backward momentum has to be balanced
    // by forward momentum of the rest of the body, so where flexion throws the
    // trunk back (the scissor, travel −1.110 m) extension carries it forward.
    // Dosed by the foot's own forward ground speed, so it self-extinguishes at
    // zero instead of ratcheting the leg backward, and only over the last
    // walkTouchdownSyncClearance metres of DESCENT — the same band is crossed
    // on the way up at lift-off, where retraction would fight the reach it has
    // just started. At the ~−46° base target a few degrees still leave the
    // command far more flexed than swingHipAngleAtLatch (~0…−16°), so the
    // anti-stomp Min guard on latchedHipTarget is not weakened.
    // 0 = the former forward-travelling touchdown.
    [Tooltip("Swing-hip retraction near touchdown, deg per 1 m/s of swing-foot forward ground speed. 0 = no velocity match.")]
    public float walkSwingRetractGain = 6f;
    [Tooltip("Clamp for swing-hip touchdown retraction, deg.")]
    public float walkSwingRetractMax = 6f;
    // Where the swing foot lands was commanded by a constant, not by the
    // target. walkStepCaptureTarget made desiredStep the capture point
    // (~0.11 m at this gait's speed), but the angle that actually puts the
    // foot there was never computed: the air target is
    // -(swingHipFlex 40 + walkSwingHipBoost 6 + speed boost up to 10), i.e.
    // 46…56° of hip flexion, and the placement loop is left ±10°
    // (walkStepPlacementMax) to correct it. Gain 30 deg/m saturates that
    // clamp at 0.33 m of step error, so on every cycle the constant wins and
    // the loop that is supposed to choose the landing point is pinned.
    //
    // Two measurable consequences, both of them already recorded symptoms.
    // (1) Overshoot: 46° of thigh plus a 35° landing knee puts the ankle
    // 0.43·sin46 + 0.43·sin11 = 0.39 m ahead of the hip — about three capture
    // points, and a foot planted that far ahead is the step-ending impact
    // walkStepCaptureTarget was written to remove. (2) The same pose cannot
    // reach the ground: it drops only 0.43·cos46 + 0.43·cos11 = 0.72 m of the
    // 0.86 m the leg needs, so the ankle hangs ~0.17 m up and the foot can
    // only land after the body sinks — feet splaying apart with pelvisDrop,
    // not a step.
    //
    // So solve for the angle instead of biasing a constant. The leg is
    // measured, not assumed: hip anchor to ankle anchor gives the current
    // strut length and its forward angle at whatever the knee happens to be
    // folded to, so the target follows the fold as the shin extends and no
    // segment size is hard-coded. Rotating the thigh forward by Δ rotates
    // that whole strut forward by Δ and lowers jointAngle by Δ (flexion is
    // minus, balance-actuators.mdc), which is the entire conversion.
    //
    // Clamped into [-swingHipFlexEffective, 0]: the solve may only ask for
    // LESS flexion than today's command, never more, so no accepted push or
    // stance number can be exceeded through this path, and it can never
    // command extension (that is the foot stomping back, and retraction stays
    // walkSwingRetractGain's job near touchdown). The anti-stomp
    // Min(swingHipAngleAtLatch, …) guard is kept on top. Air only —
    // a grounded swing keeps its dosed target bitwise.
    // 0 = the former constant-flexion reach.
    [Tooltip("Solve the airborne swing-hip target from leg geometry so the foot lands on desiredStep. 0 = former constant swingHipFlex reach.")]
    public float walkSwingPlaceGeometry = 1f;
    // One error must have one controller. The solve above is a complete
    // inverse-kinematic command for desiredStep, and placementHipBoost is a
    // proportional correction on that same step error, subtracted from the
    // solved target — so the thigh is driven past the solution it just
    // computed. The overshoot is systematic, not a transient: the swing foot
    // begins its flight BEHIND the stance foot, so stepErr keeps one sign for
    // most of the flight (0.16 m of error is 4.8° at gain 30, and the clamp
    // allows 10°), and it enters with the flexion sign, i.e. always toward
    // more reach. It also undoes the one-sided guarantee the solve is written
    // around: the clamp into [-swingHipFlexEffective, 0] is defeated one line
    // later, and the extra flexion is bought with the third-law shove on the
    // pelvis (Muscle.Apply puts −tau on Pelvis) — the very reaction every
    // hipLift cap in this gait exists to bound, and the recorded sign of a
    // pelvis thrown backward (sci_a, travel −1.110 m).
    //
    // The solve needs no proportional backstop. It already degrades to the
    // former constant when the point is out of reach (wantFwd > strutLen pins
    // Asin at 90°, so the clamp returns −swingHipFlexEffective), and it asks
    // for near-zero flexion once the hip has ridden past the landing point.
    // So the boost is dropped exactly where the solve owns the target and kept
    // everywhere else — a grounded swing has no solve at all and its dosed
    // target stays bitwise. The anti-stomp Min(latch, solve) was the other
    // actuator on that same target: it kept a 32° leftover against a ~25°
    // landing, so dropping the boost alone left placement with no command.
    // The solve owns that Min too.
    // 0 = the former stacked pair and latch Min.
    [Tooltip("Let the geometric swing solve own the airborne hip target (no P-boost, no latch Min). 0 = former stacked pair and Min.")]
    public float walkSwingPlaceGeoOnly = 1f;
    // The solve and the landing target were measured from two different
    // points on the same foot. desiredStep, placementStanceX and currentStep
    // are all bodyState footPosition — the foot segment centre — and so is
    // the criterion the FSM judges the plant by (IsSwingFootAheadOfCom reads
    // that same position). The strut the solve rotates, though, ends at the
    // ankle HINGE, and Human.CreateLeg puts that hinge (footSize.x/2 −
    // ankleHeelOffset) = 0.13 − 0.065 = 0.065 m BEHIND the centre. Aiming the
    // hinge at a centre-referenced target therefore sets the sole one heel
    // offset too far forward, every airborne solve, with no error term that
    // can see it: the placement error is centre-to-centre and reads zero
    // exactly when the hinge is on the target.
    //
    // 0.065 m against a commanded step near 0.10 m is a 65% overshoot, and
    // overshooting is not a cosmetic miss here — landing past the capture
    // point is the recorded way this gait stops itself once per step
    // (walkStepCaptureTarget), and it lands the sole further ahead than the
    // catch criterion the command was just synced to (walkStepCatchLeadSync).
    // The offset is measured, not taken from a size field: the ankle hinge
    // sits ON the foot segment, so that joint's transform position IS the
    // point bodyState reports, and the difference carries a footSize change
    // and the live foot pitch by itself. Max(0) keeps the correction
    // one-sided — it can only ask for LESS reach, so no accepted stance or
    // push number can be exceeded through this path.
    // 0 = the former hinge-referenced target.
    [Tooltip("Aim the geometric swing solve at the foot centre (what desiredStep and the catch criterion measure) instead of the ankle hinge. 0 = former hinge-referenced target.")]
    public float walkSwingPlaceFootRef = 1f;
    // The other half of consequence (2) above, and the half that solve did
    // not close. How far FORWARD the swing foot points is now measured; how
    // LONG the leg is on the way down is still a constant. Every airborne
    // path arrives at reachKneeLandDeg 35° through its own clock, and with
    // the two 0.43 m bars that holds the hip→ankle strut at
    // 2·0.43·cos(17.5°) = 0.82 m while the hip rides ~0.86 m above the ankle
    // it has to place. Even with the hip solve asking for only ~25° instead
    // of 46°, the commanded pose still reaches about 5 cm short of the dirt,
    // so the sole cannot touch until the body sinks onto it — which is
    // exactly the recorded failure: pelvisDrop pays for the contact and the
    // feet splay wider every cycle (walk2_restore3, 0.27 m apart).
    //
    // Solve the landing knee instead of commanding it. The bars are measured
    // from the hinge anchors (both thigh points sit in thigh local space,
    // both shin points in shin local space — Human.CreateLeg), so no segment
    // size is duplicated here and a resize carries by itself. Law of cosines
    // gives the flexion at which the strut is exactly long enough to set the
    // sole down from where it hangs right now; at flex 0 the bars are
    // collinear, which is also the hinge zero, so that angle IS the
    // jointAngle to hold.
    //
    // One-sided by Min: the solve may only STRAIGHTEN the shin, never fold it
    // deeper than the phase already commands. No accepted stance or push
    // number can be exceeded through this path, the knee cannot be driven
    // into its 120° stop by it, and the fold clocks keep owning lift-off.
    // Ramped by touchdownWindow over the last walkTouchdownSyncClearance of
    // descent, not switched: a step in a knee target is the pelvis shove this
    // whole gait is written around, and while the foot is still climbing the
    // fold has to stay untouched or the leg would never clear the ground.
    // 0 = the former constant landing knee.
    [Tooltip("Solve the landing swing-knee flex from leg geometry so the sole reaches the ground without the body sinking. 0 = former constant reachKneeLandDeg.")]
    public float walkSwingLandKneeGeometry = 1f;
    [Tooltip("Floor under the solved landing knee flex (deg) so the shin does not land on its own 0 stop.")]
    public float walkLandKneeMinDeg = 8f;
    // Shuffle: swingFoot ~0.92 because DualLoadMinSpan never opens on stacked
    // feet, so liftGateBlend stays 0, Plant is dual-support, and the 140° Fall
    // pin squats the pelvis onto both soles (closed chain). Opening DualLoad
    // would Lerp dualSupportStandCap 0.40→0.55 — the recorded cap fold.
    // Zeroing the swing ankle (walkPlantSwingAnkleReleaseFirst) is the other
    // closed door: the stance ankle is the CoM loop, and this gait cannot
    // afford to drop it. MixHipHold is already off on a walk split.
    //
    // Horizontal statics cannot name a trailing foot when both sit under the
    // hip. Vertical statics can: a straight stance is ~0.86 m hip-to-ankle
    // and a flexed swing is shorter, so the pelvis has to sit on the long
    // leg and the short sole unloads. Knee torque is thigh–shin, not a
    // pelvis pair. Stance ankle CoM, pelvis world-upright, DualLoad and
    // the stand cap are not touched. Inverse of LandingKneeFlexDeg: fold
    // just enough that the sole would clear if the hip holds, then lock
    // the stance knee at 0 so the hip does hold. Gated on DualLoad still
    // shut (liftGateBlend near 0) so later cycles keep the existing vault.
    // Fall-catch later cycles already write this. WALK-1 Stance also
    // writes it (walkStanceGeoLift): the 8 s clock never opens DualLoad,
    // scissor/toe-off/airHold are closed, and DrivePelvis on the planted
    // swing is the extensor that pins the sole. 0 = former fold-clock /
    // 5° stance bend only (and bitwise WALK-1 before this lift).
    [Tooltip("Stacked dual-support: geometric swing-knee clearance + stance knee 0. Not DualLoad, not ankle-zero, not a pelvis fight. Also WALK-1 Stance. 0 = off.")]
    public float walkGeoSwingLift = 1f;
    [Tooltip("Sole height the swing-knee solve aims for while the stance strut is locked, m. Must exceed BodyStateEstimator.groundProbeThickness (0.06) or the probe still reports grounded and Fall never sees air. 0.08 netted inside that 6 cm band after a 2 cm closed-chain hip drop: firstAir never, then UpdateState fallCoMOffset 0.30 at walk start +1.30 s (burn_stag 6.300). 0.12 is 2× the probe.")]
    public float walkGeoSwingLiftClearance = 0.12f;
    // WALK-1 pin after walkGeoSwingLift: the knee command is on the
    // joint, but hip/knee JointFriction is built at K = 12 past the
    // explicit-damper bound, so stopTorque 15 N·m eats the whole relative
    // speed every step. Gravity on the swing chain is ~11 N·m — less
    // than that ceiling — so the pair is a weld and LiftKneeFlexDeg
    // never shortens the strut. Fall-catch already fades this weld from
    // DualLoad / airRelease; WALK-1 never enters that block
    // (liftGateBlend stays 0 on stacked soles). This is that same
    // envelope on the geo-lift clock. Stance hip/knee keep built K
    // (pelvis world-upright lives there). Stance ankle CoM and both
    // ankle dampers stay — not an ankle-zero, not a pelvis fight, not
    // toe-off / scissor / airHold. 0 = former weld, geo command unused.
    [Tooltip("WALK-1 Stance: fade swing hip/knee viscosity on the geo-lift clock so the knee can shorten. Stance damper and ankle CoM stay. 0 = former weld.")]
    public float walkStanceGeoFriction = 1f;
    // burn_walk / burn_stag / probe_late: geo-lift armed on the Stance
    // idle clock (0.10 s) while both soles still shared one X. Swing air
    // at +0.25 s, then UpdateState fallCoMOffset 0.30 at +1.30…1.34 s —
    // the first transfer, not a late gait. The named stance must keep
    // standLeg so the stance ankle can roll CoM onto that plate; the
    // lift clock starts only after that lead. 0 = former idle-clock lift
    // on stacked feet. Quiet / oneg / pf23 never set moveX.
    [Tooltip("First Stance: >0 waits for PlantStandReady (xi past the stance toe) before geo-lift. 0 = idle-clock lift.")]
    public float walkFirstLiftLead = 0.03f;
    // The 8 s Stance progress clock parks walkComLeadX at millimetres
    // for seconds, so the 3 cm lift gate never opens and the idle clock
    // pops the swing instead. First dual-support ramps the 6 cm setpoint
    // in this window. Later stances keep the former progress/geoU ramp.
    // 0 = former 8 s Stance progress.
    [Tooltip("First Stance: seconds to ramp walkComLeadX onto the stance sole. 0 = former 8 s Stance progress.")]
    public float walkFirstLeadSeconds = 0.40f;
    // walkStanceHipExtend already drives the pelvis over the planted foot on
    // cycle 0 (walkPlantStanceDriveFirst). walkStancePush does not: it still
    // waits for pushWindow = Clamp01(stanceLead / 0.08 m). Stacked feet have
    // ~0 lead, so the ankle half of that same push-off never arrives and
    // travelX stalls — the hip extends, the sole does not roll. This is not
    // trailing A2 (no rear moment arm) and not an ankle-angle PD: the same
    // plantar overlay later cycles already use, scaled by the hip envelope
    // that already passed clock / room / load. Subtract the part pushWindow
    // already delivered so a millimetre of lead is not added twice.
    // 0 = former wait-for-lead.
    [Tooltip("Cycle-0: stance-ankle plantarflex with the hip drive, without waiting for CoM lead. 0 = former pushWindow gate.")]
    public float walkPlantStancePushFirst = 1f;
    // walkStepPlacementGain 30 / Max 10 already exist, but grounded frames
    // multiply by walkStepPlacementGroundFraction (0), so the hip never aims
    // the foot ahead of the CoM until the swing is airborne. DualLoadMinSpan
    // is 4 cm of HORIZONTAL span; geo lift is vertical. Without an X target
    // the next Catch lands under the CoM, the cycle spends itself recovering,
    // and travel dies once per step. A still-straight loaded swing cannot
    // take this (sci_a). A knee already folded (geo ~35° or the 140° pin)
    // shortens the chain, so hip flexion reaches the sole forward instead of
    // shoving the pelvis. ThighHold already dropped DrivePelvis on that fold;
    // this only replaces its 8–32° lift cap with the capture-point target.
    // 0 = former grounded × 0.
    [Tooltip("Folded/light swing: aim the hip at the capture-point landing even if the probe still says grounded. 0 = former air-only placement.")]
    public float walkStepPlaceOnFold = 1f;
    // Passive viscosity is not a damper on a leg — at the built K it is a
    // stuck hinge. Human builds hip and knee with JointFriction K = 12 and
    // maxTorque 15 N·m, and K·dt·(1/I1+1/I2) is past 1 there, so the damper
    // is permanently on its stopTorque clamp: it removes the WHOLE relative
    // joint speed every step, up to that 15 N·m ceiling. A swing leg is
    // thigh+shin+foot, about 14 kg with its CoM roughly 0.45 m below the hip,
    // which makes ~11 N·m of gravity torque at 10 deg from vertical — less
    // than the ceiling. So the passive pair alone can hold the swing leg
    // still anywhere near vertical: a ballistic swing is impossible, and every
    // degree of it has to be bought from the hip muscle, whose reaction lands
    // on the pelvis (third law). That reaction is the thing the whole hip-lift
    // cap / hip-hip couple series was written to bound, so the tax is paid
    // twice — once in torque, once in the caps that torque needs.
    // The same defect was closed twice on this body by measurement, not taste:
    // neck 36 -> 0.00784 (tau ~0.20 s) and arms 12 -> 0.178 (tau ~0.10 s),
    // both because stable PD already holds the joint and the damper only
    // welded it. The legs were never revisited after SPD, and they carry the
    // strongest muscles, so the weld hid behind them instead of being seen.
    // Airborne swing leg only: an unloaded leg buys no stability from
    // viscosity, while the stance leg keeps its full damper because that is
    // where balance actually lives. K = pairedInertia / tau, the same relation
    // used for the neck and the arms, so it does not depend on a hand-fitted
    // number. One-sided by Min against the built value: this can only LOWER
    // viscosity, never raise it. 0 = keep the built weld.
    [Tooltip("Passive hip/knee viscosity time constant on the AIRBORNE swing leg during fall-catch walk (s). K = pairedInertia/tau, applied only where it is below the built value. 0 = former built weld.")]
    public float walkSwingLegFrictionTau = 0.10f;
    // The third joint of the same leg, left out of the line above. The ankle
    // is welded harder than the hip and the knee, not softer: Human builds it
    // with K = 3 at maxTorque 15 N·m, and the pair foot+shin gives
    // 1/I = 1/0.0061 + 1/0.050, that is a paired inertia near 0.0055 kg·m².
    // K*dt/I is then about 2.7 — past the explicit-damper bound of 2, so the
    // shin-foot damper sits on its stopTorque clamp exactly like the neck at
    // 115 and the arms at 3.4/11.8/5.1 did before they were lowered. It kills
    // the WHOLE relative shin-foot speed every step, so in the air the foot is
    // not a joint, it is the far end of the shin.
    //
    // That is the pose half of foot clearance, and it is the half nothing has
    // touched. Every air command the swing foot is given — world-flat
    // (fallSwingAirAnkle), the heel cock over the last 8 cm (fallSwingAirHeel),
    // the dorsiflexor lifts (fallSwingAnkleLift / fallSwingReachAnkleLift /
    // fallSwingCatchAirAnkleLift) and walkSwingToeOffMax — is an SPD target on
    // this very joint. Against a clamped damper those targets cannot move the
    // sole: it keeps whatever angle it had at lift-off. Reach folds the knee to
    // 140°, so a sole rigid with the shin points its toe down and forward while
    // the leg extends into Catch, which is the scrape the FSM carries a dozen
    // named workarounds for. Raising the hip cannot fix it — toe-down under a
    // lifted hip is still toe-down.
    //
    // Same one-sided treatment as the hips and knees, same envelope, same
    // reasoning as the neck and the arms: SPD already holds the joint, and an
    // unloaded foot buys no stability from viscosity. The stance ankle keeps
    // its full built damper, which is where the ankle rule (no positional PD,
    // no muscle damper — viscosity is passive) actually applies: a sole pressed
    // to the ground turns joint torque into body rotation, an airborne one does
    // not. The touchdown fade is the reason the heel strike is damped as
    // before: at contact the envelope is 0 and K is the built 3 again.
    // 0 = keep the built weld.
    [Tooltip("Passive ankle viscosity time constant on the AIRBORNE swing foot during fall-catch walk (s). K = pairedInertia/tau, applied only where it is below the built value. 0 = former built weld.")]
    public float walkSwingAnkleFrictionTau = 0.10f;
    // 23:00 held the Plant fold command into Fall, then the damper ate it.
    // The envelope above is clearance-only (airRelease): viscosity is the
    // built weld until the sole actually leaves. That is the right heel-strike
    // restore — a catch plant has a negative span, so liftGateBlend stays 0
    // and the landing knee is still welded — and the wrong one on the
    // unloading trailing foot, which is still on the dirt by construction
    // (Plant / Fall timeout / Reach with swingFoot ~0.96). The 55° leftover
    // is an SPD target on that very hip and knee; against stopTorque 15 N·m
    // the pair holds ~14 kg still, so the command never shortens the chain
    // and Fall→Reach keeps waiting on air. liftGateBlend is already the
    // measured unload (walkDualLoadRelease): 0 while the rear carries the
    // weight, 1 when CoM has rolled onto the leading foot. Same Max as the
    // cap / tilt / rear-hip share, not a new tau. Mix: 0 is clearance-only.
    [Tooltip("Fall-catch: measured swing-foot unload also fades swing-leg viscosity (same liftGateBlend as the lift ramp). 0 = former clearance-only weld until air.")]
    public float walkSwingFrictionLoadShare = 1f;
    // 23:11 opened that envelope on dirt (Max(airRelease, liftGateBlend)), then
    // FadeJointFriction still Lerped K. Hip/knee are built at K = 12 past the
    // explicit-damper bound, so the pair sits on stopTorque 15 N·m until the
    // lerp drops K below I/Δt — about fade 0.7 on the knee. The Plant fold
    // ramp opens at fade 0.5 (same mid-span as 22:00), which means the first
    // third of the 55° leftover fights the same weld 23:11 named. Airborne
    // already ends at fade 1 (K = I/tau, stopTorque never binds). Snap K to
    // that tau value once the dirt envelope is past 0.5 — the fold gate —
    // and leave the touchdown lerp alone (that envelope is airRelease, not
    // this snap). Mix: 0 is the 23:11 lerp on dirt too.
    [Tooltip("Fall-catch: on dirt, jump swing-leg K to I/tau once unload-share ≥ 0.5 (leave the stopTorque weld). 0 = former linear K lerp, still welded until fade ~0.7.")]
    public float walkSwingFrictionClampSnap = 1f;
    // 23:22 jumped K on dirt (airRelease ≈ 0, loadFade ≥ 0.5), then dropped
    // the snap the moment the probe lost contact. The first millimetres of
    // lift-off have airRelease ~0.02, so fade = Max(0.02, loadFade) while the
    // foot is still dual-support, or fade = 0.02 once !grounded kills the
    // share — and Lerp(K=12, I/tau, 0.02) is the same stopTorque weld 23:22
    // named. The fold that just freed the chain slams shut, the sole replants,
    // Fall→Reach waits on air. Touchdown does not latch: a catch plant has
    // loadFade 0 (negative span), so 23:22 never arms and the descent lerp
    // is unchanged. Hold the dirt snap until fade itself is past the weld
    // (~0.7 on the knee, the number 23:22 already measured) or the envelope
    // closes. Mix: 0 is the 23:22 dirt-only pulse.
    [Tooltip("Fall-catch: keep the dirt tau-K snap through the first millimetres of lift-off, until fade ≥ 0.7 (lerp itself leaves stopTorque). 0 = former dirt-only pulse, weld returns at first clearance.")]
    public float walkSwingFrictionSnapHold = 1f;
    // Not another setpoint for that sole, and not a gain: the SPD DENOMINATOR
    // of the joint the line above just unwelded. ControlJointToAngleStable
    // divides by 1 + k*dt/I, where k is the damping the step is about to
    // apply, and it always built k from the FLEXOR torque. That is exact for
    // every joint in the body except one: the ankle is the only asymmetric
    // muscle pair (Human.ankleFlexorTorque 22 against ankleExtensorTorque 65,
    // the 3:1 that balance-actuators.mdc derives from m*g*arm at heel and
    // toe). So a plantarflex command was divided by a denominator sized for a
    // third of its own torque, i.e. the implicit step was under-damped by that
    // factor exactly where the muscle is strongest.
    //
    // The frame this decides is touchdown. In Reach the knee is folded to its
    // 120° stop, so the world-flat target (jointAngle = shin rotation) sits
    // past the -45° ankle stop and the sole simply parks at full dorsiflexion,
    // toe up, on a near-zero command. Then Catch extends the leg and that same
    // target sweeps back toward zero: the sole must PLANTARFLEX ~50° in the
    // last tenth of a second before the heel lands, and that is the calf's
    // 65 N·m against a paired shin-foot inertia near 0.0055 kg*m². An
    // under-damped implicit step there overshoots world-flat, which puts the
    // toe down at contact — the scrape the FSM carries a dozen named
    // workarounds for. Before walkSwingAnkleFrictionTau the clamped damper hid
    // this; now the sole is free to follow the command, so the command has to
    // be integrated correctly.
    //
    // Which muscle acts is not a guess: ApplySignal engages the extensor on a
    // positive signal, and the sign of the signal equals the sign of the raw
    // command because the denominator is positive. So the muscle can be picked
    // before the division with no circularity, and on the dorsiflexion half of
    // the sweep the number is the former one bitwise. 0 = former denominator.
    [Tooltip("Fall-catch walk: build the airborne swing-ankle SPD denominator from the muscle that actually drives the step (calf 65 N·m on plantarflex, not the 22 N·m dorsiflexor). 0 = former flexor-only denominator.")]
    public float walkSwingAnkleSpdTorque = 1f;
    [Tooltip("Extra stance knee bend on walk, deg.")]
    public float walkStanceKneeBend = 10f;
    // The SCHEDULE of that bend, not its size. It is applied as
    // walkStanceKneeBend x Lerp(0.5, 1, liftGateBlend), and on the fall-catch
    // gait liftGateBlend is no longer a clock: since walkAirReleaseAuthority it
    // IS the swing foot's own air release (clearance / walkTouchdownSyncClearance).
    // So the schedule reads "the further the swing foot is off the dirt, the
    // more the stance knee bends" — and the stance knee is the only strut
    // carrying the body at exactly that moment. That is backwards twice over.
    //
    // Physically: hip-to-ankle on a two-bar leg is 2*L*cos(knee/2), so the
    // stance hip sits ~4 mm lower at 6+10 deg than at 6+5 deg, and ~9 mm lower
    // than at 6 deg. That drop comes straight off the swing foot's clearance,
    // which is the input this very schedule reads — a loop that spends its own
    // lift. Single support is where the strut must be TALLEST: it is the phase
    // the Fall/Reach vault already straightens the knee toward 0 for.
    //
    // It is also a silent regression. Until walkAirReleaseAuthority revived the
    // blend, liftGateBlend was identically 0 for the whole fall-catch cycle
    // (weightBlend is computed only when !fallCatchWalk), so every recorded
    // fall-catch number was taken at the 0.5 end — a flat 5 deg. Reviving the
    // blend doubled this term to 10 deg without anyone choosing 10, and the
    // note at the vault call site still warns that even 5 deg here "yields the
    // strut and the pelvis settles at the shorter leg (shuffle)".
    //
    // This fades the SAME 0.5 end out to 0 as the swing foot clears instead of
    // growing it to 1. One-sided by construction: the bend can only ever be
    // less than the 0.5 the accepted runs used, never more, so no new sink can
    // come from it. The blend is continuous (a clearance ratio), so there is no
    // step in the knee target — the shape every other target in this gait is
    // ramped to avoid. At liftGateBlend = 0 both schedules read 0.5, so double
    // support, quiet stance, crouch, cold one-leg and legacy walk are bitwise
    // unchanged even at 1. 0 = the former growing schedule, bit for bit.
    [Tooltip("Fade the fall-catch stance knee bend out as the swing foot clears instead of growing it. 1 = fade to 0 at full lift, 0 = former Lerp(0.5,1,liftGateBlend).")]
    public float walkStanceKneeBendLiftFade = 1f;
    // kneeRecoveryFlex is a QUIET-STANCE reflex: at |CoM offset| >
    // recoveryCoMOffset (0.15 m) on two feet the emergency really is "get
    // lower and buy time", so 12° into both knees is right. On the fall-catch
    // gait the same automaton reads a different quantity — during single
    // support UpdateState is fed stanceComOff (the CoM lead over the STANCE
    // foot), and this gait drives that lead past 0.15 m by design: the Fall
    // CoP setpoint is rolled out to the toe (0.13 m) and the recorded catch
    // offsets are 0.19…0.60 m. So the reflex fires every cycle, and it fires
    // on the only loaded leg, in the phase where the vault has already yielded
    // (Catch / Recover) and that leg is the whole strut. Flexing it there
    // costs hip height (pelvisDrop, the sink guard's own quantity), shortens
    // the reach the placement solve just paid for, and pulls against the
    // stance hip extend and the ankle push-off on the same leg.
    // The size of that flex is not in anyone's accounting: the note at the
    // Catch vault yield describes the trailing knee going "0° → ~16°
    // (kneeTarget + bend)", i.e. kneeBaseAngle 6 plus walkStanceKneeBend 10 —
    // computed with kneeTarget at its quiet value. With the reflex live it is
    // 6 + 12 + 10 = 28°, and the yield clock only softens when that arrives.
    // Dropping the reflex restores the 16° pose that comment was written for.
    // Fraction of kneeRecoveryFlex the fall-catch STANCE knee keeps. The swing
    // knee is untouched (it is the base of the swing-flex path), and every
    // non-fall-catch path — quiet stance, crouch, push, cold one-leg, legacy
    // walk — never reads this field. 1 = former behaviour, bit for bit.
    [Tooltip("Fraction of kneeRecoveryFlex kept on the STANCE knee during fall-catch walk. 0 = none (the quiet-stance reflex stays off the loaded strut), 1 = former behaviour.")]
    public float walkStanceKneeRecoveryFlex = 0f;
    [Tooltip("Base forward pelvis lean on walk, deg.")]
    public float walkForwardPelvisLean = 2f;
    [Tooltip("Base forward torso lean on walk, deg.")]
    public float walkForwardTorsoLean = 4f;
    // Stance extend at full weightBlend: pelvis up → swing loses GRF.
    // Not swing flex (holdknee/gk25 fold). 0 = off. CLI -walkStanceExtend.
    [Tooltip("Stance-extend fraction (knee/pelvis→0) at weightBlend=1. 0 = off.")]
    public float walkStanceExtend = 0f;
    // Walking is a controlled forward fall, not stepping in place. While the
    // ankle target stays "CoM over support", it pulls the body back, and a
    // support swap travels 0.21 m in 35 s. Here the target is walkComLeadX
    // ahead of the stance foot: gravity supplies the drive, the swing plants.
    // No invisible force. Ramped on the WALK-1 geo-lift clock so the first
    // 5 ms of Stance is not an ankle-torque step on stacked soles.
    // 0 = former behaviour. CLI -walkComLead.
    [Tooltip("Metres past the support front edge to hold CoM when moveX≠0. 0 = former mid-support hold.")]
    public float walkComLeadX = 0.06f;
    // WALK-2 Fall: the fall forward must come from the ankle setpoint, not
    // from switching the ankle off. Killing ankleBalance (ankleScale 0) leaves
    // gravity plus the pelvis lean reaction, and the body drifts backwards.
    // Here the centre of pressure moves fallComLead metres ahead of the CoM
    // instead: the same balance loop now holds a target the body cannot reach
    // standing still, so it topples forward over a loaded, controlled ankle.
    // Only read while useFallCatchWalk≠0 and the FSM is in Fall.
    [Tooltip("Metres the ankle CoP target moves forward during Fall (WALK-2).")]
    public float fallComLead = 0.025f;
    // fallComLead is a fixed 0.025 m, so the Fall ankle loop pins the CoM
    // two and a half centimetres ahead of the stance foot centre and holds it
    // there for the whole phase. That is a stationary setpoint, and the body
    // obediently stands on it: single support cannot carry the trunk any
    // further than the target it is given, whatever else pushes. It is also
    // why the feet keep drawing apart — the swing plants a step ahead while
    // the CoM stays put, so every cycle ends in a wider split and the CoM sits
    // behind the new support instead of over it.
    //
    // Real stance is a roll: the CoP travels from heel to toe while the body
    // vaults across the foot. So the setpoint has to travel too, and its end
    // is geometry, not a number — the sole. Past the toe the CoP cannot be put
    // ahead of the CoM any more (balance-actuators.mdc: the ankle ceiling is
    // m·g·arm about that edge), so the toe is where Fall must hand over to the
    // catch. StanceToeReach() reads that edge off the foot collider, the same
    // measurement the push-off fade already uses.
    //
    // The fraction keeps the target inside the sole rather than on its edge,
    // where the ankle would have no restoring authority left. At 0.75 of a
    // 0.13 m sole it ends at 0.0975 m — just inside walkLiftComMax 0.10, so
    // the swing-lift gate stays fully open across the whole roll. 0 = the
    // former fixed setpoint.
    [Tooltip("Fraction of the sole (foot centre → toe) the Fall CoM setpoint rolls out to. 0 = former fixed fallComLead.")]
    public float walkFallComRollToe = 0.75f;
    // Swing pose turns on only after lift-off (swingHipLatched), and lift-off
    // needs the thigh already forward — a closed loop: both hips hold the
    // pelvis upright, neither steps out, the foot never leaves the ground
    // (swingFoot ≈ 1.0, travel 0.12 m in 35 s). The scissor gives the swing
    // a forward pose while still grounded, once weightBlend shows load on support.
    // 0 = off, former behaviour. CLI -walkSwingScissor.
    [Tooltip("weightBlend from which the grounded swing reaches forward. 0 = off.")]
    public float walkSwingScissorLevel = 0.20f;
    // Reaching the thigh forward throws the pelvis back (third law): full
    // −swingHipFlex on the ground does lift off (swingFoot 0.50 vs 0.999),
    // but the human travels backward and folds in 7 s. Grounded reach is
    // a fraction; in the air the pose stays full. CLI -walkSwingScissorFlex.
    [Tooltip("Fraction of swingHipFlex in the grounded scissor pose. 1 = full reach.")]
    public float walkSwingScissorFlex = 0.25f;
    // Swing lift as a hip-to-hip couple, not a pelvis shove.
    //
    // Every swing-hip torque is a HingeJoint2D pair: the thigh gets +tau and
    // the pelvis gets -tau. The pelvis is the balance variable, so that
    // reaction is exactly why the grounded lift had to be capped at 8-24 deg
    // and why the full scissor threw the trunk back (sci_a, travel -1.11 m).
    // Raising the cap or the bias cannot fix it — the harder the lift, the
    // harder the shove, and the pelvis loop spends its authority undoing it.
    //
    // A real body flexes the swing hip against the STANCE hip: the reaction
    // runs down the loaded leg into the ground instead of into the trunk.
    // Feed the same signal to the stance hip with the opposite sign and the
    // two pelvis reactions cancel (both hips share the pelvis as
    // connectedBody and both carry hipMuscleTorque, so equal signal is equal
    // torque). What is left on the stance thigh is an extension torque
    // against a planted foot — contralateral push-off, the direction the
    // step already wants.
    //
    // This is not an ankle change: the swing-ankle CoM loop, ankleLift and
    // comLead are untouched. It is not a new PD and not a gain retune: the
    // counter is the signal the swing hip already applied, one 5 ms step
    // old (the muscle activation rate limit smooths far more than that).
    // 0 = former behaviour, the pelvis eats the reaction.
    [Tooltip("Fraction of the swing-hip signal fed back to the stance hip. 0 = off.")]
    public float walkSwingLiftCouple = 1f;
    [Tooltip("Clamp on the stance-hip counter signal. Keeps the pelvis loop's own authority.")]
    public float walkSwingLiftCoupleMax = 0.6f;
    // The 8/16/24 deg lift caps exist only to bound the third-law shove.
    // While the reaction is being absorbed by the stance leg that reason is
    // gone, so the thigh may walk further before the fade. Hard-clamped at
    // coupleCapCeilingDeg — this widens a bounded cap, it does not open the
    // 40 deg scissor pose.
    [Tooltip("Scale on the grounded hip-lift cap while the couple is active. 1 = off.")]
    public float walkSwingLiftCoupleCapScale = 1.5f;
    // Airborne Reach carries the foot forward, or it carries nothing: this is
    // the only window in the fall-catch cycle where the swing leg is free and
    // still has time to choose where it lands. Right now it is spent holding
    // the thigh at world vertical (ApplyFallCatchSwingThighHold via
    // reachAirHold), so the folded leg comes back down roughly under its own
    // hip and the step length is whatever the trunk drifted — travel 0.208 m
    // in 35 s. The step-placement target (latchedHipTarget minus
    // placementHipBoost) only reaches the hip in Catch, by which time
    // Reach->Catch has usually already fired on a planted foot.
    //
    // The reach was closed for a real reason, not a wrong one: the latch fires
    // on the FIRST air frame, so the hip target stepped from ~0 to
    // -swingHipFlex in one 5 ms tick. On a 100 N·m hip that is an instantly
    // saturated flexor, and the pair puts -100 N·m straight on the pelvis
    // (sci_a: trunk walked back 1.11 m). The discontinuity was the fault, not
    // the pose. Start the target at the angle the thigh already holds at
    // lift-off, so the SPD error is zero on the first air frame, and walk it
    // onto the placement target over this many seconds. Peak hip torque is
    // then bounded by the ramp instead of by a cap on the command, and the
    // leftover reaction is what walkSwingLiftCouple already routes into the
    // stance hip. 0 = the former hold-only Reach.
    [Tooltip("Seconds to ramp the airborne Reach swing hip onto the step-placement target. 0 = former hold-only Reach.")]
    public float walkReachAirSwingSeconds = 0.20f;
    // Debug: stance-hip counter signal from the last FixedUpdate.
    [System.NonSerialized]
    public float lastSwingLiftCounter;
    // Debug: stance-hip extension bias from the last FixedUpdate.
    [System.NonSerialized]
    public float lastStanceHipExtend;
    // Debug: trailing-hip extension bias in Plant from the last FixedUpdate.
    [System.NonSerialized]
    public float lastPlantHipExtend;
    // After a scrape hipair goes upright again and plants the foot. Hold
    // flex for hold more seconds while grounded (not a commit before first air). CLI, default 0.
    [Tooltip("Seconds to hold swing-hip flex after !grounded. 0 = off.")]
    public float walkSwingAirHold = 0f;
    // After latch: keep hip flex until the heel is loaded. The toe does not plant.
    // CLI -walkSwingHeelPlant 1; default 0 = hipair (!grounded).
    [Tooltip("1 = swing pose from !heelLoaded after latch. 0 = hipair.")]
    public float walkSwingHeelPlant = 0f;
    // After a scrape hipair keeps the latch → grounded knee stays off. Clearing
    // the latch on plant restores groundKnee without kneer (that one left latch).
    // CLI -walkSwingUnlatchPlant 1; default 0.
    [Tooltip("1 = clear swingHipLatched when the swing is grounded. 0 = sticky latch.")]
    public float walkSwingUnlatchPlant = 0f;
    // Latch from the first air frame kills groundKnee on plant.
    // Continuous !grounded (s) required before latch. 0 = hipair (immediate).
    [Tooltip("Seconds of continuous air before latch. 0 = from the first frame.")]
    public float walkSwingLatchAir = 0f;
    // In the air hipair holds ankle joint=0 (foot ⊥ shin) → knee flex
    // points the toe down and it scrapes. 1 = world-horizontal foot target.
    [Tooltip("1 = after latch in the air, foot to world horizontal. 0 = joint 0.")]
    public float walkSwingAirLevel = 0f;
    // While liftBlend grows, level catches the cap faster — else unload
    // is weak at cap 0.4→1 and rate 0.5/s.
    [Tooltip("standLegRate scale at liftBlend=1 on walk. 1 = off.")]
    public float walkLiftLevelRateScale = 1f;
    // Debug: actual dual-support scale from the last FixedUpdate.
    [System.NonSerialized] public float lastDualSoft = 1f;
    [System.NonSerialized] public float lastLiftBlend;
    public float standLegRatePerSecond = 1.5f;
    [Tooltip("Release of the one-leg pose at standLeg=0 — faster than lift so the transfer window does not drift.")]
    public float standLegReleaseRatePerSecond = 4f;
    // The walk contract is "hold the key to walk, release to stand", so a
    // release lands in the middle of a step far more often than at a stand:
    // the swing leg is usually airborne with a folded knee and a latched hip.
    // heldStandCmd was zeroed on every walk step, so at that moment the
    // support sign vanished in one 5 ms step while standLegLevel was still
    // near 1 — splitLegs went false, both legs took the symmetric quiet-stance
    // command, and the airborne foot planted with no role. That is exactly the
    // failure closed for the one-leg probe on 01.09 (release with the sign
    // held survives, retfix_onegl30), only on the path Play uses every time
    // the player lets go. Recording the live support here hands the release to
    // that same measured landing instead. The window is bounded by the level's
    // own decay — standLegReleaseRatePerSecond 4/s, so at most 0.25 s.
    [Tooltip("1 = keep the walk support sign through the release window (until standLegLevel decays). 0 = former instant drop.")]
    public float walkEndHoldSupport = 1f;
    // 0 — dual support, 1 — full swing pose. Smoothed like crouchLevel.
    public float standLegLevel;

    // Error and speed are divided by references, so the PD input is
    // dimensionless and the gains are order-one. Before that, 0.03° of
    // error was enough to slam activation to 1: the loop was a switch
    // and always ran at full power.
    [Header("PD input scaling")]
    public float errorReferenceDegrees = 10f;
    public float speedReferenceDegPerSec = 200f;
    // Swing-leg proportional band on the fall-catch gait. The shared reference
    // 10° with hipPGain 1.5 saturates the hip signal past 6.7° of error, and the
    // air swing is commanded ~40° of travel (swingHipFlex) with a 140° knee
    // fold: for the whole flight the loop is a switch at full muscle torque and
    // only becomes proportional in the last few degrees before the target.
    // Two consequences, both of them things the geometry cannot fix. The
    // setpoints the swing solver computes — footAhead in centimetres,
    // swingRetract and LandingKneeFlexDeg in degrees — are invisible behind the
    // clamp, because 40° of error and 41° of error both come out as signal 1.
    // And while the signal is clamped the D term cannot brake either, so the
    // leg arrives at the pose at speed and pays its third-law reaction into the
    // pelvis — the shove the hipLift caps (16 / 24 / 32°) exist to bound.
    // One-sided by the Max at the use site: a wider reference can only lower
    // |P·error|, never raise it, so no recorded run gains extensor or flexor
    // torque from this. Peak authority is untouched as well — 40° with
    // hipPGain 1.5 still saturates above 26.7° of error, so lift-off breaks the
    // leg loose exactly as before and the band opens over the final approach,
    // which is where both the setpoints and the damping live.
    [Tooltip("Swing hip/knee error reference (deg) on fall-catch walk. <= errorReferenceDegrees = former shared band.")]
    public float walkSwingErrorReferenceDegrees = 40f;

    [Header("Joint PD")]
    // hipP/hipD do not feed an angle in dual support while |hip| stays
    // inside hipHoldBand: the pelvis is held to world vertical by
    // pelvisPGain/pelvisDGain. Past the band, MixHipHold adds the same
    // hip P/D toward hipHoldAngle so gravity cannot open a split while
    // the pelvis stays upright. Quiet dual support only — a walk split
    // IS hip travel past the band, and holding it to 0 extends a flexing
    // swing and brakes a planted stance thigh (travelX dies, then splay).
    // Swing still uses hipP/hipD after lift-off via the joint pose.
    public float hipPGain = 1.5f;
    public float hipDGain = 0.4f;
    [Tooltip("If |hip jointAngle − hipHoldAngle| exceeds this (deg), quiet-stance hips also PD toward hipHoldAngle. 0 = pelvis-only. Off during walk/one-leg split.")]
    public float hipHoldBand = 20f;
    [Tooltip("Stance hip joint-angle rest (deg). Not hipBaseAngle (that is pelvis world tilt).")]
    public float hipHoldAngle = 0f;
    public float kneePGain = 1.2f;
    public float kneeDGain = 0.5f;
    public float neckPGain = 1.5f;
    public float neckDGain = 0.4f;
    // Neck and head target world vertical, not the angle to the parent.
    // Their own error reference, like lumbar and pelvis: ComputeSignal used
    // 10°, and that saturated the joint from fractions of a degree.
    public float neckTargetTilt = 0f;
    public float neckErrorReferenceDegrees = 25f;
    // Stable PD only for neck and head. The flag stays so the stand can
    // compare the old behaviour on the same build: without it a fix and a
    // rebuild look the same. Arms are always on SPD — see the block below.
    public bool useStablePd = true;
    [Header("Arms")]
    // Joint pose, not world vertical: otherwise the arm is a pendulum and swings.
    // Elbow flexion is minus (−140…0). Zero is the "straight arm" stop; without
    // a muscle gravity held the joint there ~98% of the time. Target a bit minus.
    // SPD always: elbow K·Δt/Ieff ≈ 11.8, wrist ≈ 5.1, shoulder ≈ 3.4 —
    // explicit PD is unstable. useStablePd does not apply to the arms.
    public float shoulderPGain = 1.5f;
    public float shoulderDGain = 0.4f;
    public float shoulderBaseAngle = 0f;
    public float shoulderErrorReferenceDegrees = 25f;
    public float elbowPGain = 1.2f;
    public float elbowDGain = 0.4f;
    public float elbowBaseAngle = -20f;
    public float elbowErrorReferenceDegrees = 25f;
    public float wristPGain = 1.0f;
    public float wristDGain = 0.4f;
    public float wristBaseAngle = 0f;
    public float wristErrorReferenceDegrees = 25f;
    // Target offset from balanceSignal (CoM + tilt rate): CoM ahead —
    // shoulders plus (hand back), elbow toward zero. Zero — static pose.
    public float armShoulderBalanceGain = 25f;
    public float armElbowBalanceGain = 10f;
    public float armWristBalanceGain = 0f;
    // Arm swing on walk: opposite the legs. Legacy Stance/Transfer still uses
    // sin(phaseAge / stanceDuration). Fall-catch cycles are tenths of a
    // second, so that clock never leaves ~0 — TryFallCatchArmSwing drives
    // the same contra pose from the FSM instead. Not CoM-balance: that one
    // gave elbowLimit and fold after swap.
    [Tooltip("Shoulder amplitude on walk, deg. Legacy: sin of Stance. Fall-catch: Fall/Reach envelope.")]
    public float walkArmShoulderSwing = 15f;
    [Tooltip("Extra forward elbow flex on walk, deg.")]
    public float walkArmElbowSwing = 5f;
    [Tooltip("Fraction of arm amplitude in Transfer (decay). 0 = base pose.")]
    public float walkArmTransferCarry = 0.35f;
    [Tooltip("Balance-to-shoulder gain during walk arm swing.")]
    public float walkArmBalanceShoulderGain = 4f;
    [Tooltip("Balance-to-elbow gain during walk arm swing.")]
    public float walkArmBalanceElbowGain = 2f;
    // Contra-swing from the measured thigh split instead of a phase clock.
    // Two things are wrong with a clock envelope here. It peaks at mid-swing
    // and returns to the base pose at touchdown, which is a quarter cycle out
    // of gait: the arm belongs at its extreme when the contralateral heel
    // lands and at neutral in mid-stance. And it is a magnitude (0..1), so the
    // arm's angular momentum reverses halfway through a step whose leg sweep
    // keeps one sign — the arms cancel the leg for the first half of the step
    // and add to it for the second, which is the opposite of what an arm swing
    // is for. The hip jointAngle is pelvis-minus-thigh, the same frame the
    // shoulder target already lives in, so the split IS the quantity to
    // oppose: it is zero on a symmetric stance, it is continuous across every
    // FSM edge (there is no clock to reset, so no phase-edge snap to patch),
    // and it needs no phase at all. Amplitude ceiling stays
    // walkArmShoulderSwing; full amplitude at one swingHipFlex of split.
    [Tooltip("1 = walk arm swing from the measured thigh split. 0 = phase-clock envelope.")]
    public float walkArmLegTrack = 1f;
    [Tooltip("Additional late-stance ankle push near phase end.")]
    public float walkLatePushGain = 0f;
    [Tooltip("Stance progress where late push starts (0..1).")]
    public float walkLatePushStart = 0.72f;
    [Tooltip("Clamp for late-stance ankle push signal.")]
    public float walkLatePushMax = 0f;
    [Tooltip("Require swing airborne for late push-off.")]
    public bool walkLatePushNeedsAir = true;
    [Tooltip("Scale late push by touchdown window (0/1).")]
    public bool walkLatePushNeedsTouchdownWindow = true;
    [Header("Pelvis to world vertical")]
    // Same as lumbar: error and speed come only from VestibularSystem.
    // The hip joint lives on the thigh, the pelvis is connectedBody, so the
    // P/D sign matches ControlJointToAngle, not lumbar: a positive
    // signal turns the pelvis counterclockwise (increases jointAngle).
    // P and D are plus; a negative value inverts the loop.
    public float pelvisPGain = 2.0f;
    public float pelvisDGain = 0.3f;
    public float pelvisErrorReferenceDegrees = 25f;
    // Cold one-leg CLI only. Defaults 0: extra P/D on the lift is what
    // dumped left support (oneleg_perf_l). Left support adds
    // standLegPelvisTilt +10°; extra P saturates both hips as extensors
    // so swing unload cannot flex, then after the forward foot leaves the
    // support jumps to the rear sole with CoM already ahead. Extra D=2
    // with speedReference 200 also made a 100 Hz pelvis limit cycle
    // (oneleg_perf reversalHz ≈ 200, span 6.2°). Quiet stance / walk
    // never add these. CLI -oneLegPelvisP / -oneLegPelvisD still exist
    // but only apply in true single support (one foot down).
    [Tooltip("Extra pelvis P on cold one-leg single support. 0 = quiet-stance P. Do not use during lift.")]
    public float oneLegPelvisPBoost = 0f;
    [Tooltip("Extra pelvis D on cold one-leg single support. 0 = quiet-stance D.")]
    public float oneLegPelvisDBoost = 0f;
    [Header("Lumbar")]
    // Reference is the chest world vertical, not the angle to the pelvis.
    // Otherwise when the pelvis topples the lumbar rides 35 kg of chest down with it.
    // Error and speed only from VestibularSystem: mixing world tilt with
    // joint.jointSpeed is forbidden — those are different frames.
    public float lumbarTargetTilt = 0f;
    // P and D are plus. In the formula below they enter as −P and −D because
    // the error is still target − tilt: that keeps the same arithmetic as
    // the working pair P=−2, D=−0.3, without flipping the damper sign.
    public float lumbarPGain = 2.0f;
    public float lumbarDGain = 0.3f;
    public float lumbarErrorReferenceDegrees = 25f;
    [Header("Ankle as pendulum")]
    // Torque against CoM offset, not against the joint angle.
    // Overturning moment is m*g*d ≈ 687*d N·m. On a 1.75 m body the
    // CoM is lower and inertia is smaller: former P=7 and D=4 left torso
    // sway and offset RMS worse than the baseline. P=14 holds CoM tighter,
    // D=8 kills speed after the startup squat.
    public float ankleComP = 14f;
    public float ankleComD = 8f;
    public float ankleLimitMargin = 15f;
    // VEST-2: quiet dual used to copy one midpoint ankleBalance onto both
    // plates. Plantarflex at a foot CoM has already passed is heel-lift /
    // push-off (pf23r bothFeet 0.975, travel 0.465 m). 1 = mute that plus
    // on the trailing plate; the lead keeps the shared restoring command
    // until its toes. Walk / march / CaptureWalk never read this.
    // 0 = VEST-1 copy.
    [Tooltip("Quiet dual: 1 = no plantarflex on a plate CoM has passed. 0 = both ankles copy midpoint.")]
    public float quietAnklePerFoot = 1f;
    // After the per-foot gate, quiet dual only. pf23v2: rear plus should
    // sit at 0 while the lead still plantarflexes. Walk leaves these at 0.
    [Tooltip("Last quiet left ankle command after the per-foot gate.")]
    public float lastQuietAnkleL;
    [Tooltip("Last quiet right ankle command after the per-foot gate.")]
    public float lastQuietAnkleR;

    [Header("Toe MTP (not the ankle CoM loop)")]
    // SPD on the new hinge. Quiet stance holds 0 (flat plate). Airborne
    // is minus (toe up). Capture walk adds a late-swing plus (push-off),
    // not a mid-stance weld. Soft P/D so the body can roll over the ball;
    // ankleComP / ankleComD are untouched.
    public float toePGain = 0.8f;
    public float toeDGain = 0.25f;
    public float toeErrorReferenceDegrees = 25f;
    [Tooltip("Airborne toe-up (deg). Minus on the MTP.")]
    public float toeClearanceDeg = 12f;
    [Tooltip("Capture walk: stance toe-down in the last 30% of T (deg). Plus on the MTP.")]
    public float captureToePushDeg = 12f;
    [Tooltip("Capture walk: extra toe-down per degree of forward trunk lean. Keep small: 1 maps a dump onto the MTP stop and pins CoP at the tip.")]
    public float captureToeLeanGain = 0.1f;

    [Header("Muscle activation speed")]
    public float muscleActivationSpeed = 20f;

    [Header("Leg muscles")]
    public Muscle leftHipFlexor;
    public Muscle leftHipExtensor;
    public Muscle rightHipFlexor;
    public Muscle rightHipExtensor;
    public Muscle leftKneeFlexor;
    public Muscle leftKneeExtensor;
    public Muscle rightKneeFlexor;
    public Muscle rightKneeExtensor;
    public Muscle leftAnkleFlexor;
    public Muscle leftAnkleExtensor;
    public Muscle rightAnkleFlexor;
    public Muscle rightAnkleExtensor;
    public Muscle leftToeFlexor;
    public Muscle leftToeExtensor;
    public Muscle rightToeFlexor;
    public Muscle rightToeExtensor;

    [Header("Lumbar muscles")]
    public Muscle lumbarFlexor;
    public Muscle lumbarExtensor;

    [Header("Neck and head muscles")]
    public Muscle neckFlexor;
    public Muscle neckExtensor;
    public Muscle headFlexor;
    public Muscle headExtensor;

    [Header("Arm muscles")]
    public Muscle leftShoulderFlexor;
    public Muscle leftShoulderExtensor;
    public Muscle rightShoulderFlexor;
    public Muscle rightShoulderExtensor;
    public Muscle leftElbowFlexor;
    public Muscle leftElbowExtensor;
    public Muscle rightElbowFlexor;
    public Muscle rightElbowExtensor;
    public Muscle leftWristFlexor;
    public Muscle leftWristExtensor;
    public Muscle rightWristFlexor;
    public Muscle rightWristExtensor;

    private HingeJoint2D leftHipJoint;
    private HingeJoint2D rightHipJoint;
    private HingeJoint2D leftKneeJoint;
    private HingeJoint2D rightKneeJoint;
    private HingeJoint2D leftAnkleJoint;
    private HingeJoint2D rightAnkleJoint;
    private HingeJoint2D leftToeJoint;
    private HingeJoint2D rightToeJoint;
    private HingeJoint2D lumbarJoint;
    private HingeJoint2D neckJoint;
    private HingeJoint2D headJoint;
    private HingeJoint2D leftShoulderJoint;
    private HingeJoint2D rightShoulderJoint;
    private HingeJoint2D leftElbowJoint;
    private HingeJoint2D rightElbowJoint;
    private HingeJoint2D leftWristJoint;
    private HingeJoint2D rightWristJoint;

    // Paired inertia is lazy, on the first FixedUpdate: in Awake
    // Rigidbody2D.inertia has not yet been recomputed from the collider.
    private float neckEffectiveInertia;
    private float headEffectiveInertia;
    private float leftShoulderEffectiveInertia;
    private float rightShoulderEffectiveInertia;
    private float leftElbowEffectiveInertia;
    private float rightElbowEffectiveInertia;
    private float leftWristEffectiveInertia;
    private float rightWristEffectiveInertia;
    private float lastWalkStanceProgress;
    // Last Plant swing pose, frozen into Fall. Written only while Plant
    // actually commanded a fold; Fall/Reach read it under walkPlantFallFoldHold.
    private float lastPlantSwingReachKnee;
    private float lastPlantSwingHipLift;
    private float lastPlantSwingAnkleLift;
    private float lastPlantSwingThighHold;
    // Cycle-0 standing-height geo swing-knee, latched once. Live geo
    // follows a sagging hip toward 120°; Catch must not hold that tuck.
    private float lastFirstStepGeoKnee;
    // Cycle-0 Catch plant pose. 10°/10° on a 10° stance-knee hip drop
    // GetContacts with ~4.5 cm span. Latch through Catch AND Recover:
    // leaving Catch used to restore hipLift / catchKnee and lift the sole
    // before the 0.05 s RecoverTouchdownMinAge swap. Later cycles clear it.
    private bool firstStepCatchPlantLatched;
    private float leftKneeEffectiveInertia;
    private float rightKneeEffectiveInertia;
    private float leftHipEffectiveInertia;
    private float rightHipEffectiveInertia;
    private float leftAnkleEffectiveInertia;
    private float rightAnkleEffectiveInertia;
    private float leftToeEffectiveInertia;
    private float rightToeEffectiveInertia;
    private float getUpAge;
    private float getUpRiseBlend;
    // Seconds the current GETUP attempt has had at least one sole on the
    // slab. Cleared on lift-off so an airborne tuck cannot re-arm.
    private float getUpContactAge;
    // Ages of the two sequenced poses (GETUP-1). The sit is held before the
    // curl starts, the curl is held for the tuck budget: both poses have to be
    // visible in the 5 fps capture, not one tick of a gate.
    private float getUpSitAge;
    private float getUpTuckAge;
    // Sequenced latches (GETUP-1). Sit before tuck — knee-first pike was wrong.
    private bool getUpSitLatched;
    private bool getUpTuckLatched;
    private bool getUpPikeLatched; // alias of tuck latch for older call sites
    // Hip extension during sit-up only (deg, plus). Deep tuck uses getUpHipFlex
    // in the other (flexion) direction.
    private const float GetUpSitHipFlex = 35f;
    // Knee stay near stance during sit-up (deg, plus). Tuck needs ≥70.
    private const float GetUpSitKneeFlex = 20f;
    private const float GetUpTuckKneeMin = 70f;
    // The sit is a pose of the sequence, not the tick the latch fires on:
    // 0.6 s is three frames of the 5 fps trial capture, so "sitting" is
    // visible in the video before the knees fold.
    private const float GetUpSitHoldSeconds = 0.6f;

    private VestibularSystem vestibularSystem;
    private CenterOfMassCalculator comCalculator;
    private BodyStateEstimator bodyState;
    private MotionIntent intent;
    private StepPhaseDriver stepPhaseDriver;
    // Looked up once: a population has no driver, GetComponent every 200 Hz
    // step will never find one. Late-add — only Bind/Ensure, not FixedUpdate.
    private bool stepPhaseDriverSearched;
    // Sole geometry for the push-off window (StanceToeReach), read once.
    private float stanceToeReachCached;
    private bool stanceToeReachResolved;
    // Two-bar leg for the landing-knee solve, read once from the hinge
    // anchors. Both legs are built by the same CreateLeg call, so one pair
    // covers them.
    private float legThighLength;
    private float legShinLength;
    private bool legLinkResolved;
    // Passive dampers of both legs plus the viscosity the builder gave them and
    // the paired inertia that turns a time constant into K. Resolved once on
    // first use, never in Awake: Rigidbody2D.inertia is not from the collider
    // yet and the friction components are added after AddComponent.
    private JointFriction leftHipFriction;
    private JointFriction rightHipFriction;
    private JointFriction leftKneeFriction;
    private JointFriction rightKneeFriction;
    private JointFriction leftAnkleFriction;
    private JointFriction rightAnkleFriction;
    private float leftHipFrictionBase;
    private float rightHipFrictionBase;
    private float leftKneeFrictionBase;
    private float rightKneeFrictionBase;
    private float leftAnkleFrictionBase;
    private float rightAnkleFrictionBase;
    private float leftHipFrictionInertia;
    private float rightHipFrictionInertia;
    private float leftKneeFrictionInertia;
    private float rightKneeFrictionInertia;
    private float leftAnkleFrictionInertia;
    private float rightAnkleFrictionInertia;
    private bool legFrictionResolved;
    // GETUP-1: lumbar damper (Human lumbarFriction 30 / max 40) welds the
    // trunk while muscles are muted on a −98° plank. Cached like the legs.
    private JointFriction lumbarFrictionComp;
    private float lumbarFrictionBase;
    private float lumbarFrictionInertia;
    // True while a leg is running below its built viscosity. Quiet stance,
    // crouch, the pushes and cold one-leg never set it, so they do not
    // touch a JointFriction field at all. WALK-1 with moveX may fade the
    // swing hip/knee weld (walkStanceGeoFriction); stance joints stay.
    private bool legFrictionFaded;
    // walkSwingFrictionSnapHold: 23:22 armed this on dirt, then lost it at
    // the first millimetre of air. Cleared when the swing side changes, when
    // fade itself is past the weld, or when the envelope is closed.
    private bool swingFrictionSnapLatched;
    // 0 = none, −1 left swing, +1 right swing. A role swap must not keep
    // tau K on the hip that just became stance.
    private int swingFrictionSnapHoldSide;
    // Geo-lift hold after CoM is on the named stance sole. Not PhaseAge:
    // a late firstStacked split would then snap walkGeoLiftU to 1.
    private float walkFirstLiftHoldAge;
    // 0 = none, −1 right swing, +1 left swing. A role swap restarts the hold.
    private int walkFirstLiftStanceSign;
    private Rigidbody2D leftThighRb;
    private Rigidbody2D rightThighRb;
    // Foot bodies for the touchdown velocity match. The sole's own travel is
    // what friction and the heel-strike collision see, so it is read from the
    // body rather than differenced from bodyState positions.
    private Rigidbody2D leftFootRb;
    private Rigidbody2D rightFootRb;
    private BoxCollider2D leftHeelBox;
    private BoxCollider2D leftToeBox;
    private BoxCollider2D rightHeelBox;
    private BoxCollider2D rightToeBox;
    // After the first swing lift-off do not return the hip to the pelvis
    // because of a foot scrape: else the thigh extends and the leg stomps back.
    private bool swingHipLatched;
    // Remaining flex hold after a scrape (walkSwingAirHold).
    private float swingAirHoldRemain;
    // Continuous swing !grounded for walkSwingLatchAir.
    private float swingAirStreak;
    // Last support command: on a sign change reset the level and the latch.
    private float heldStandCmd;
    // Quiet one-leg release: how long the swing has stayed airborne after
    // intent.standLeg went to 0. Caps the pose freeze so a stuck swing
    // cannot hold the split forever.
    private float quietReleaseAirSeconds;
    // Swing angle on the latch frame. After lift-off the target is not weaker
    // than this flex: Min(angleAtLatch, −swingHipFlex) — more negative = more flex.
    private float swingHipAngleAtLatch;
    // Swing-hip command from the previous FixedUpdate and the side it was on
    // (-1 left, +1 right, 0 none). The stance hip cancels its pelvis reaction
    // on the next step; one 200 Hz step of lag is below the muscle rate limit.
    private float lastSwingHipCommand;
    private int lastSwingHipSide;
    // Smoothed march pelvis bias (plus = back). Not a walk field.
    private float marchPelvisBias;
    // True swing air long enough for the ankle cap. Scrape must not set this.
    private bool marchComHold;
    // Hip/knee pose is up: toe/heel gates may run. Earlier than this they
    // zero plantarflex while the thigh is still peeling (march_ank).
    private bool marchToeHold;

    // WALK-2 fall–catch balance scaling (useFallCatchWalk=0 leaves defaults).
    private struct FallCatchBalance
    {
        public float ankleScale;
        public float hipBalanceScale;
        public float pelvisLean;
        public float torsoLean;
        public bool forceSwingFlex;
        public float catchKnee;
        // Swing knee flex in Fall/Reach (deg), applied even while grounded.
        public float reachKnee;
        // Fall/Reach: 0–1, lerp stance knee toward 0 so the support strut
        // holds pelvis height while the swing chain shortens.
        public float stanceVault;
        // Fall/Reach: 0–1, flexor-only stop against swing-thigh extension
        // past world vertical. 0 = keep the hip silenced.
        public float thighHold;
        // Fall/Reach: 0–1, extra flexor bias so the grounded swing knee
        // rises instead of the foot sliding back. 0 = stop only.
        public float hipLift;
        // Fall/Reach: 0–1, fade the grounded swing ankle's toe grip and
        // add a small dorsiflexor so hipLift can raise the foot. 0 = keep
        // the own-error / Reach propulsive loop.
        public float ankleLift;
        // Fall/Reach/Catch: 1 = airborne swing foot world-horizontal.
        // 0 = latched joint 0 (foot along shin). Ignored while grounded.
        public float airAnkle;
        // Fall/Reach/Catch: degrees of airborne dorsiflex (toe-up) blended
        // in over the last centimetres of clearance. 0 = world-flat only.
        public float airHeel;
        // Metres the ankle CoP target moves along the walk direction.
        public float comLead;
        // Catch: landing (swing) ankle takes the CoM loop once the foot is down.
        public bool catchAnkleOnSwing;
        // Catch/Recover planted swing knee (deg). >0.5 = hold this instead
        // of 0.15×catchKnee. 0 = former straighten snap.
        public float plantKnee;
        // Post-swap Plant, and Fall/Reach when vaultRamp starts from leftover
        // (or landDeg when leftover already arrived): hold this on the new
        // stance (the just-planted catch foot). Degrees: leftover Catch shin,
        // then landDeg. 0 = quiet stance knee.
        public float stancePlantKnee;
        // Plant/Fall/Reach/Catch/Recover: CoM offset past fallCoMOffset is
        // the gait, not a ragdoll. RelaxAllMuscles would drop the catch.
        public bool ignoreFalling;
        // Catch/Recover: drop walk auto/base lean and the speed-drive
        // ankle push. Those still point the trunk forward and add +moveX
        // on the stance ankle, which is the opposite of a landing brake.
        public bool suppressWalkLean;
    }

    void Awake()
    {
        vestibularSystem = GetComponent<VestibularSystem>();
        comCalculator = GetComponent<CenterOfMassCalculator>();
        bodyState = GetComponent<BodyStateEstimator>();
        intent = GetComponent<MotionIntent>();
        CacheStepPhaseDriverOnce();

        leftHipJoint = GetJoint("LeftLegThigh");
        rightHipJoint = GetJoint("RightLegThigh");
        leftKneeJoint = GetJoint("LeftLegShin");
        rightKneeJoint = GetJoint("RightLegShin");
        leftAnkleJoint = GetJoint("LeftLegFoot");
        rightAnkleJoint = GetJoint("RightLegFoot");
        leftToeJoint = GetJoint("LeftLegToe");
        rightToeJoint = GetJoint("RightLegToe");
        lumbarJoint = GetJoint("Torso");
        neckJoint = GetJoint("Neck");
        headJoint = GetJoint("Head");
        leftShoulderJoint = GetJoint("LeftArmUpper");
        rightShoulderJoint = GetJoint("RightArmUpper");
        leftElbowJoint = GetJoint("LeftArmLower");
        rightElbowJoint = GetJoint("RightArmLower");
        leftWristJoint = GetJoint("LeftArmHand");
        rightWristJoint = GetJoint("RightArmHand");
        if (leftHipJoint != null)
            leftThighRb = leftHipJoint.attachedRigidbody;
        if (rightHipJoint != null)
            rightThighRb = rightHipJoint.attachedRigidbody;
        // The ankle hinge lives on the foot segment, so its attachedRigidbody
        // is the foot itself (Human.CreateLeg).
        if (leftAnkleJoint != null)
        {
            leftFootRb = leftAnkleJoint.attachedRigidbody;
            leftHeelBox = leftAnkleJoint.GetComponent<BoxCollider2D>();
        }
        if (rightAnkleJoint != null)
        {
            rightFootRb = rightAnkleJoint.attachedRigidbody;
            rightHeelBox = rightAnkleJoint.GetComponent<BoxCollider2D>();
        }
        if (leftToeJoint != null)
            leftToeBox = leftToeJoint.GetComponent<BoxCollider2D>();
        if (rightToeJoint != null)
            rightToeBox = rightToeJoint.GetComponent<BoxCollider2D>();
    }

    // One lookup. HeadlessTrial attaches the driver before BuildHuman, so Awake sees it.
    // If AddComponent moves after Awake — explicit Bind/Ensure from outside.
    private void CacheStepPhaseDriverOnce()
    {
        if (stepPhaseDriverSearched) return;
        stepPhaseDriver = GetComponent<StepPhaseDriver>();
        stepPhaseDriverSearched = true;
    }

    public void BindStepPhaseDriver(StepPhaseDriver driver)
    {
        stepPhaseDriver = driver;
        stepPhaseDriverSearched = true;
    }

    // Repeat GetComponent only on an explicit call after a late-add.
    public void EnsureStepPhaseDriver()
    {
        stepPhaseDriverSearched = false;
        CacheStepPhaseDriverOnce();
    }

    // WALK-1 travel: after geo-lift shortens the swing knee, a fraction of
    // swingHipFlex reaches the sole forward so the next stance can sit
    // ahead of this one. Silence until u is up — a pose SPD on a straight
    // loaded swing is sci_a (travel −1.11 m). Knee torque is thigh–shin,
    // hip couple (walkSwingLiftCouple) still dumps the reaction into the
    // planted stance hip. Not an ankle-angle PD, not a gain retune.
    private float WalkStanceGeoLiftHip(
        HingeJoint2D hip, Muscle flexor, Muscle extensor,
        ref float inertia, float u, float errorRef)
    {
        if (u < 0.05f || hip == null)
        {
            ApplySignal(0f, flexor, extensor);
            return 0f;
        }
        float reach = -swingHipFlex * Mathf.Clamp01(walkSwingScissorFlex) * u;
        return ControlJointToAngleStable(
            hip, reach, hipPGain, hipDGain, errorRef,
            flexor, extensor, ref inertia);
    }

    private HingeJoint2D GetJoint(string childName)
    {
        Transform t = transform.Find(childName);
        return t != null ? t.GetComponent<HingeJoint2D>() : null;
    }

    // Foot centre → toe edge, metres: how far ahead of bodyState footPosition
    // the sole still reaches, which is how far CoP can travel ahead of CoM.
    // Read from the heel+toe boxes, not hard-coded, so a footSize change carries.
    // On a 0.26 m plate that is still 0.13 m — the split does not shorten the arm.
    private float StanceToeReach()
    {
        if (stanceToeReachResolved) return stanceToeReachCached;
        stanceToeReachResolved = true;
        float length = 0f;
        BoxCollider2D heel = null;
        if (leftAnkleJoint != null)
            heel = leftAnkleJoint.GetComponent<BoxCollider2D>();
        if (heel == null && rightAnkleJoint != null)
            heel = rightAnkleJoint.GetComponent<BoxCollider2D>();
        if (heel != null)
            length += heel.size.x;
        BoxCollider2D toe = null;
        if (leftToeJoint != null)
            toe = leftToeJoint.GetComponent<BoxCollider2D>();
        if (toe == null && rightToeJoint != null)
            toe = rightToeJoint.GetComponent<BoxCollider2D>();
        if (toe != null)
            length += toe.size.x;
        stanceToeReachCached = length > 0.001f ? length * 0.5f : 0.13f;
        return stanceToeReachCached;
    }

    // Bar lengths of the leg linkage, metres. The hip hinge sits on the thigh
    // and the knee hinge on the shin (Human.CreateLeg), so hip.anchor and
    // knee.connectedAnchor are both thigh-local, and knee.anchor and
    // ankle.connectedAnchor are both shin-local: the bars are anchor
    // distances, not a size field this class would have to keep in sync.
    // Resolved once on first use like the paired inertias — the builder wires
    // the anchors after AddComponent, so Awake is not a safe moment to read
    // them (autoConfigureConnectedAnchor is off, HumanSegment.ConnectTo).
    private void ResolveLegLinkOnce()
    {
        if (legLinkResolved) return;
        legLinkResolved = true;
        if (leftHipJoint != null && leftKneeJoint != null)
            legThighLength = Vector2.Distance(
                leftHipJoint.anchor, leftKneeJoint.connectedAnchor);
        if (leftKneeJoint != null && leftAnkleJoint != null)
            legShinLength = Vector2.Distance(
                leftKneeJoint.anchor, leftAnkleJoint.connectedAnchor);
    }

    // Knee flexion (deg) at which the hip→ankle strut is exactly long enough
    // to set this sole on the ground from where it hangs right now
    // (walkSwingLandKneeGeometry above). The forward offset is whatever the
    // hip solve has already chosen, so the length asked for here matches the
    // reach actually being made, and the hip is taken to hold its height
    // while the ankle covers the remaining clearance — the point of the solve
    // is that the leg, not the pelvis, pays for the last centimetres.
    // Returns -1 on a degenerate pose (ankle level with the hip, or an
    // unresolvable link): the caller then leaves the command alone.
    private float LandingKneeFlexDeg(HingeJoint2D hip, HingeJoint2D ankle, float clearance)
    {
        if (hip == null || ankle == null) return -1f;
        ResolveLegLinkOnce();
        float bars = legThighLength * legShinLength;
        if (bars < 1e-6f) return -1f;
        Vector2 hipPoint = hip.transform.TransformPoint(hip.anchor);
        Vector2 anklePoint = ankle.transform.TransformPoint(ankle.anchor);
        float strutDown = hipPoint.y - anklePoint.y;
        if (strutDown <= 0.05f) return -1f;
        float needDown = strutDown + Mathf.Max(0f, clearance);
        float fwd = anklePoint.x - hipPoint.x;
        float needLen = Mathf.Sqrt(needDown * needDown + fwd * fwd);
        // Law of cosines on the two bars. Straight (flex 0) gives needLen =
        // L1 + L2 and cos = 1, so a leg that cannot reach even fully extended
        // clamps to 0 and the caller's floor keeps it off the hinge stop.
        float cos = (needLen * needLen
            - legThighLength * legThighLength
            - legShinLength * legShinLength) / (2f * bars);
        return Mathf.Acos(Mathf.Clamp(cos, -1f, 1f)) * Mathf.Rad2Deg;
    }

    // Same band StepPhaseDriver uses for NearContact: GetContacts can be
    // empty on a pitched toe while the solver still holds the sole, and
    // then both local flags go false, DualLoad never opens, and the FSM
    // cannot pick a support. Clearance inside 2×contactOffset is that
    // contact, not air.
    private bool FootSupportDown(bool left)
    {
        if (bodyState == null) return false;
        if (left ? bodyState.leftFootGrounded : bodyState.rightFootGrounded)
            return true;
        float clearance = left
            ? bodyState.leftFootGroundClearance
            : bodyState.rightFootGroundClearance;
        return clearance <= Physics2D.defaultContactOffset * 2f;
    }

    // Inverse of LandingKneeFlexDeg (walkGeoSwingLift above). Same cached
    // bars, same hip/ankle hinges, no alloc. Shorten the hip→ankle strut
    // by `clearance` metres so the sole would rise if the hip holds height.
    // Returns -1 on a degenerate pose; 120 when the asked lift is taller
    // than the current strut (caller clamps to the hinge).
    private float LiftKneeFlexDeg(HingeJoint2D hip, HingeJoint2D ankle, float clearance)
    {
        if (hip == null || ankle == null) return -1f;
        ResolveLegLinkOnce();
        float bars = legThighLength * legShinLength;
        if (bars < 1e-6f) return -1f;
        Vector2 hipPoint = hip.transform.TransformPoint(hip.anchor);
        Vector2 anklePoint = ankle.transform.TransformPoint(ankle.anchor);
        float strutDown = hipPoint.y - anklePoint.y;
        if (strutDown <= 0.05f) return -1f;
        float lift = Mathf.Max(0.01f, clearance);
        float needDown = strutDown - lift;
        if (needDown < 0.05f) return 120f;
        float fwd = anklePoint.x - hipPoint.x;
        float needLen = Mathf.Sqrt(needDown * needDown + fwd * fwd);
        float minLen = Mathf.Abs(legThighLength - legShinLength) + 0.01f;
        float maxLen = legThighLength + legShinLength;
        needLen = Mathf.Clamp(needLen, minLen, maxLen);
        float cos = (needLen * needLen
            - legThighLength * legThighLength
            - legShinLength * legShinLength) / (2f * bars);
        return Mathf.Acos(Mathf.Clamp(cos, -1f, 1f)) * Mathf.Rad2Deg;
    }

    // The four leg dampers, their built viscosity and their paired inertia.
    // Same lazy moment as the paired inertias of SPD: the builder adds the
    // components and the collider sets inertia after AddComponent, so Awake
    // cannot read either. Returns false until the joints exist.
    private bool ResolveLegFrictionOnce()
    {
        if (legFrictionResolved) return true;
        if (leftHipJoint == null || rightHipJoint == null
            || leftKneeJoint == null || rightKneeJoint == null) return false;
        leftHipFriction = leftHipJoint.GetComponent<JointFriction>();
        rightHipFriction = rightHipJoint.GetComponent<JointFriction>();
        leftKneeFriction = leftKneeJoint.GetComponent<JointFriction>();
        rightKneeFriction = rightKneeJoint.GetComponent<JointFriction>();
        // The ankle hinge lives on the foot (Human.CreateLeg), so its damper is
        // the foot's, exactly as the hip damper is the thigh's. Not in the null
        // guard above: a body without feet still gets its hips and knees faded.
        if (leftAnkleJoint != null)
            leftAnkleFriction = leftAnkleJoint.GetComponent<JointFriction>();
        if (rightAnkleJoint != null)
            rightAnkleFriction = rightAnkleJoint.GetComponent<JointFriction>();
        // The built value, not Human.hipFriction: GameProcess and HeadlessTrial
        // scale that field before the body is assembled, and the damper has to
        // come back to exactly what this body was given.
        if (leftHipFriction != null) leftHipFrictionBase = leftHipFriction.damping;
        if (rightHipFriction != null) rightHipFrictionBase = rightHipFriction.damping;
        if (leftKneeFriction != null) leftKneeFrictionBase = leftKneeFriction.damping;
        if (rightKneeFriction != null) rightKneeFrictionBase = rightKneeFriction.damping;
        if (leftAnkleFriction != null) leftAnkleFrictionBase = leftAnkleFriction.damping;
        if (rightAnkleFriction != null) rightAnkleFrictionBase = rightAnkleFriction.damping;
        leftHipFrictionInertia = EffectiveInertia(leftHipJoint);
        rightHipFrictionInertia = EffectiveInertia(rightHipJoint);
        leftKneeFrictionInertia = EffectiveInertia(leftKneeJoint);
        rightKneeFrictionInertia = EffectiveInertia(rightKneeJoint);
        leftAnkleFrictionInertia = EffectiveInertia(leftAnkleJoint);
        rightAnkleFrictionInertia = EffectiveInertia(rightAnkleJoint);
        if (lumbarJoint != null)
        {
            lumbarFrictionComp = lumbarJoint.GetComponent<JointFriction>();
            if (lumbarFrictionComp != null)
            {
                lumbarFrictionBase = lumbarFrictionComp.damping;
                lumbarFrictionInertia = EffectiveInertia(lumbarJoint);
            }
        }
        legFrictionResolved = true;
        return true;
    }

    // GETUP-1 must move the heap. EnterGettingUp → ResetCaptureWalk restores
    // hip/knee K=12 (stopTorque weld) and lumbar K=30; quiet SPD then
    // saturates on a −98° plank (getUpAttempts 1, freeze to 40 s). Same
    // swing-peel envelope on BOTH legs + lumbar; restore on FinishGetUp.
    // Cached JointFriction only — no Find, no alloc. Not a PD retune.
    private void ApplyGetUpFrictionPeel(bool peel)
    {
        if (!ResolveLegFrictionOnce()) return;
        float fade = peel ? 1f : 0f;
        float snap = peel ? 1f : 0f;
        float tau = Mathf.Max(0.05f, walkSwingLegFrictionTau);
        float ankleTau = Mathf.Max(0.05f, walkSwingAnkleFrictionTau);
        FadeJointFriction(leftHipFriction, leftHipFrictionBase,
            leftHipFrictionInertia, tau, fade, snap);
        FadeJointFriction(rightHipFriction, rightHipFrictionBase,
            rightHipFrictionInertia, tau, fade, snap);
        FadeJointFriction(leftKneeFriction, leftKneeFrictionBase,
            leftKneeFrictionInertia, tau, fade, snap);
        FadeJointFriction(rightKneeFriction, rightKneeFrictionBase,
            rightKneeFrictionInertia, tau, fade, snap);
        FadeJointFriction(leftAnkleFriction, leftAnkleFrictionBase,
            leftAnkleFrictionInertia, ankleTau, fade, snap);
        FadeJointFriction(rightAnkleFriction, rightAnkleFrictionBase,
            rightAnkleFrictionInertia, ankleTau, fade, snap);
        FadeJointFriction(lumbarFrictionComp, lumbarFrictionBase,
            lumbarFrictionInertia, tau, fade, snap);
        legFrictionFaded = peel;
    }

    // fade 0 restores the built viscosity exactly; fade 1 gives K = I/tau.
    // Min against the built value keeps the change one-sided — a small tau can
    // never make a joint stiffer than the body was built with.
    // snap: the linear fade is a no-op while K is still past I/Δt (stopTorque
    // kills relative speed every step). Jump to the tau value so the envelope
    // that opened the fold actually frees the joint. Touchdown keeps snap 0.
    private static void FadeJointFriction(JointFriction friction, float builtK,
                                         float pairedInertia, float tau, float fade,
                                         float snap)
    {
        if (friction == null) return;
        float k = builtK;
        if ((fade > 0.0001f || snap > 0.5f) && tau > 0.0001f && pairedInertia > 0f)
        {
            float targetK = Mathf.Min(builtK, pairedInertia / tau);
            k = snap > 0.5f ? targetK : Mathf.Lerp(builtK, targetK, fade);
        }
        friction.damping = k;
    }

    // Written for all four joints on every step this runs, so a fade can never
    // outlive the swing that asked for it — EndWalk, a phase change, a fall or
    // a role swap all arrive with fade 0 and put the built value back.
    // stanceHipFade / stanceHipSnap: cycle-0 first-step trail strut
    // (comment 38/41). Comment 37 faded only the leading hip; swingFade
    // is clearance so the trail hip stayed K=12 on dirt, and both ankles
    // stayed K=3. Both hips and both ankles take this envelope so both
    // struts can rotate about the ankles (equal WORLD thigh tilt,
    // comment 41). Knees keep the built damper (strut at kneeBase).
    // 0 = former swing-only envelope.
    private void ApplySwingLegFriction(float swingFade, bool swingIsLeft, float snap,
                                      float stanceHipFade = 0f, float stanceHipSnap = 0f,
                                      float stanceAnkleSnap = 0f,
                                      bool includeAnkle = true,
                                      bool includeKnee = true)
    {
        if (!ResolveLegFrictionOnce()) return;
        float tau = Mathf.Max(0f, walkSwingLegFrictionTau);
        float fade = Mathf.Clamp01(swingFade);
        float leftFade = swingIsLeft ? fade : 0f;
        float rightFade = swingIsLeft ? 0f : fade;
        float leftSnap = swingIsLeft ? snap : 0f;
        float rightSnap = swingIsLeft ? 0f : snap;
        float leftHipFade = leftFade;
        float rightHipFade = rightFade;
        float leftHipSnap = leftSnap;
        float rightHipSnap = rightSnap;
        float leftKneeFade = includeKnee ? leftFade : 0f;
        float rightKneeFade = includeKnee ? rightFade : 0f;
        float leftKneeSnap = includeKnee ? leftSnap : 0f;
        float rightKneeSnap = includeKnee ? rightSnap : 0f;
        float leftAnkleFade = includeAnkle ? leftFade : 0f;
        float rightAnkleFade = includeAnkle ? rightFade : 0f;
        float leftAnkleSnap = includeAnkle ? leftSnap : 0f;
        float rightAnkleSnap = includeAnkle ? rightSnap : 0f;
        if (stanceHipFade > 0.0001f || stanceHipSnap > 0.5f)
        {
            // Comment 38/41: trail hip is also welded on dirt (swingFade is
            // clearance). Ankle K=3 on I/10 is the remaining 0 DOF of a
            // strut about the ankle. Both sides, hips and ankles. Knees
            // stay. Both hips are on WORLD thigh tilt (comment 41), not
            // a leading DrivePelvis weld.
            float leadFade = Mathf.Clamp01(stanceHipFade);
            leftHipFade = Mathf.Max(leftHipFade, leadFade);
            rightHipFade = Mathf.Max(rightHipFade, leadFade);
            leftHipSnap = Mathf.Max(leftHipSnap, stanceHipSnap);
            rightHipSnap = Mathf.Max(rightHipSnap, stanceHipSnap);
            leftAnkleFade = Mathf.Max(leftAnkleFade, leadFade);
            rightAnkleFade = Mathf.Max(rightAnkleFade, leadFade);
            leftAnkleSnap = Mathf.Max(leftAnkleSnap, stanceHipSnap);
            rightAnkleSnap = Mathf.Max(rightAnkleSnap, stanceHipSnap);
        }
        // Comment 44: cycle-0 Plant needs the shin to rotate over a flat
        // sole (inverted pendulum). Comment 45: hip K still stays — the
        // hip lock is a joint SPD to the quiet snapshot, not a damper
        // fade. Ankle K=3 is the weld that froze that DoF; snap to tau
        // K on both ankles, not the hips.
        if (stanceAnkleSnap > 0.5f)
        {
            leftAnkleSnap = Mathf.Max(leftAnkleSnap, stanceAnkleSnap);
            rightAnkleSnap = Mathf.Max(rightAnkleSnap, stanceAnkleSnap);
        }
        FadeJointFriction(leftHipFriction, leftHipFrictionBase,
            leftHipFrictionInertia, tau, leftHipFade, leftHipSnap);
        FadeJointFriction(leftKneeFriction, leftKneeFrictionBase,
            leftKneeFrictionInertia, tau, leftKneeFade, leftKneeSnap);
        FadeJointFriction(rightHipFriction, rightHipFrictionBase,
            rightHipFrictionInertia, tau, rightHipFade, rightHipSnap);
        FadeJointFriction(rightKneeFriction, rightKneeFrictionBase,
            rightKneeFrictionInertia, tau, rightKneeFade, rightKneeSnap);
        // The ankle rides the same envelope with its own time constant: the
        // built K there is 3, not 12, and the paired inertia is ten times
        // smaller, so one tau for all three joints would be a different regime
        // on each. Written unconditionally like the other four, so the restore
        // path (fade 0 on EndWalk, phase change, fall or role swap) puts the
        // built weld back on the sole as well.
        float ankleTau = Mathf.Max(0f, walkSwingAnkleFrictionTau);
        FadeJointFriction(leftAnkleFriction, leftAnkleFrictionBase,
            leftAnkleFrictionInertia, ankleTau, leftAnkleFade, leftAnkleSnap);
        FadeJointFriction(rightAnkleFriction, rightAnkleFrictionBase,
            rightAnkleFrictionInertia, ankleTau, rightAnkleFade, rightAnkleSnap);
        legFrictionFaded = fade > 0.0001f || snap > 0.5f
            || stanceHipFade > 0.0001f || stanceHipSnap > 0.5f
            || stanceAnkleSnap > 0.5f;
    }

    // Forward CoM velocity as every walk capture-point term must read it
    // (walkComVelFilterTau). Public because StepPhaseDriver judges the phase
    // and the step target from the same quantity the stance ankle presses on.
    // With the filter off this is the raw estimator sample, so tau 0 keeps the
    // former trajectory to the bit.
    public float WalkComVelocityX
    {
        get
        {
            if (walkComVelFilterTau <= 0.0001f || !hasWalkComVelSample)
                return bodyState != null ? bodyState.comVelocity.x : 0f;
            return walkComVelFiltered;
        }
    }

    // One pole on the estimator's one-step CoM velocity. alpha = dt/(tau+dt)
    // is the implicit-step solution of a first-order lag, so it cannot
    // overshoot at any tau — the same reason the SPD denominator is written
    // that way (balance-actuators.mdc) — and needs no clamp. Two floats, no
    // allocation, called once per step.
    private void UpdateWalkComVelocityFilter()
    {
        float raw = bodyState != null ? bodyState.comVelocity.x : 0f;
        if (walkComVelFilterTau <= 0.0001f || !hasWalkComVelSample)
        {
            // Off, or the first sample: track the raw signal. Keeping the
            // state live while the filter is off means switching it on from
            // the Inspector mid-run starts from the body's present speed, not
            // from a number it left seconds ago.
            walkComVelFiltered = raw;
            hasWalkComVelSample = true;
            return;
        }
        float dt = Time.fixedDeltaTime;
        float alpha = dt / (walkComVelFilterTau + dt);
        walkComVelFiltered += (raw - walkComVelFiltered) * alpha;
    }

    // LEVEL 2: -poseDrive 1. The pose library takes every joint on the step it
    // is active, so it is the single writer, and it is resolved lazily because
    // Human adds it after this component exists.
    private PoseDriveController poseDrive;
    private bool poseDriveResolved;

    private bool PoseDriveActive()
    {
        if (!poseDriveResolved)
        {
            poseDriveResolved = true;
            poseDrive = GetComponent<PoseDriveController>();
        }
        return poseDrive != null && poseDrive.active;
    }

    void FixedUpdate()
    {
        float comOffset = comCalculator != null ? comCalculator.GetCoMOffsetX() : 0f;
        float angularVelocity = vestibularSystem != null ? vestibularSystem.GetBodyAngularVelocity() : 0f;
        // Before anything may return early (startup hold, Falling): a filter
        // that skips steps is a filter with an unknown time constant.
        UpdateWalkComVelocityFilter();

        UpdateCrouchLevel();
        UpdateLeanLevel();
        UpdateStandLegLevel();
        // Settled standing hip bar before BeginWalk (walkTime 5): world
        // Y from sensors, so the first onset collapsed test is not the
        // kinematic thigh+shin fallback.
        if (stepPhaseDriver == null || !stepPhaseDriver.walkActive)
            TryCaptureStandHipReference();

        // LEVEL 2 pose drive. One switch, one writer: while it is on, every
        // joint belongs to the pose library and the automaton below is skipped,
        // including the locomotion automaton - the pose tick names the state
        // itself (getUpAttempts 7000 came from entering the old get up every
        // step of a pose run). The old path still runs with -poseDrive 0.
        if (PoseDriveActive())
        {
            poseDrive.Tick(comOffset);
            return;
        }

        UpdateLocomotionState(comOffset);

        // GETUP-1: prone heap (−70° plank) + walk key is TickGetUp.
        // A mid-walk dump under GetUpProneDeg stays StandingRestore so
        // CaptureWalk can resume; that path must not eat GETUP-1.
        if (locomotionState == LocomotionState.GettingUp)
        {
            TickGetUp(comOffset);
            return;
        }
        if (locomotionState == LocomotionState.Fallen)
        {
            RelaxAllMuscles();
            return;
        }

        // WALK-3: the capture-point walker owns every leg and trunk joint
        // while it runs (BalanceControllerCaptureWalk.cs). Default on:
        // -walk 1 is this walker. -captureWalk 0 restores WalkLift.
        if (CaptureWalkActive())
        {
            TickCaptureWalk(comOffset);
            // DualSupport dump arms StandingRestore inside TickCaptureWalk.
            // Returning here left the walk commands as leftover activation
            // and let the recorder sample Falling (p_auto 7.55 s). Fall
            // through so the same physics step is a two-foot Plant:
            // walkSoft off, quiet hip/knee, Falling never named.
            if (stepPhaseDriver == null
                || (!stepPhaseDriver.StandingRestore
                    && !stepPhaseDriver.StandingSurrender))
                return;
        }

        bool walkRequested = stepPhaseDriver != null && stepPhaseDriver.walkActive;
        // Standing Plant: TiltSettled + CoM-to-stance is still shut, so
        // the joints belong to quiet two-foot stance. walkRequested stays
        // true so Tick can resume; walkSoft must not, or moveX=1 keeps
        // rolling a heap (lead never re-enters 0.10 m, the walker never
        // starts). Falling is not named while this is up, so a tipped
        // BeginWalk is a stand-up, not RelaxAllMuscles. Happy path never
        // raises restore.
        bool standingPlant = walkRequested
            && stepPhaseDriver.walkRecoverAnyPose > 0.5f
            && (stepPhaseDriver.StandingRestore
                || stepPhaseDriver.StandingSurrender);
        // Any pose outside BeginWalk's Plant→Fall band (TiltSettled
        // 12° + both soles + signed CoM-to-stance ≤ plantComLeadMax):
        // pike / extend here until that gate opens. Gating restore on
        // tilt ≥ 12° left a 45 cm one-leg squat on quiet PD, then GETUP
        // ate the frame (p_auto 19:10, SS 72 %, knee 97°). World-upright
        // hips on a −90° pelvis still sit on the +45° stop — restore
        // owns that. Happy path never raises restore (tilt 2.1°, |lead| ~0).
        if (standingPlant && !stepPhaseDriver.StandingPlantHoldReady)
        {
            TickStandingPlantRestore(comOffset);
            return;
        }
        bool walkSoft = walkRequested && !standingPlant;
        float standCmd = intent != null ? intent.standLeg : 0f;
        if (standingPlant)
        {
            heldStandCmd = 0f;
            standCmd = 0f;
        }
        else if (!walkSoft)
        {
            bool requestSplit = Mathf.Abs(standCmd) > 0.5f;
            if (requestSplit)
            {
                float requestSign = Mathf.Sign(standCmd);
                bool hadSplit = Mathf.Abs(heldStandCmd) > 0.5f;
                if (!hadSplit || Mathf.Sign(heldStandCmd) != requestSign)
                {
                    standLegLevel = 0f;
                    swingHipLatched = false;
                }
                heldStandCmd = requestSign;
            }
            else if (standLegLevel <= 0.001f)
            {
                heldStandCmd = 0f;
            }

            standCmd = heldStandCmd;
        }
        else if (walkEndHoldSupport > 0.5f && stepPhaseDriver.useFallCatchWalk != 0)
        {
            // Source of the hold above (walkEndHoldSupport). Track what the
            // driver commands right now, zero included: a surrender Plant
            // stands on both feet on purpose, and carrying a stale split
            // through that release would fight the recovery ladder. Nothing
            // reads heldStandCmd while walkActive, so the walk itself is
            // untouched — the value only matters on the frame walk ends.
            heldStandCmd = Mathf.Abs(standCmd) > 0.5f ? Mathf.Sign(standCmd) : 0f;
        }
        else
        {
            heldStandCmd = 0f;
        }

        // Cycle-0 Reach/Catch/Recover after the first lift is still a
        // split step. SinkCollapsed used to Plant+standLeg=0 on the
        // plant frame (initiation height already near 0.25 m), and both
        // hips then ran ControlHipsToWorldUpright — the pelvis sat 0.65 m
        // in 0.4 s. If the driver still writes 0 while the phase is the
        // landing, keep the stance sign so the landing hip is not the
        // quiet dual DrivePelvis.
        if (walkSoft && stepPhaseDriver != null && stepPhaseDriver.useFallCatchWalk != 0
            && stepPhaseDriver.FallCatchCycleCount == 0
            && !stepPhaseDriver.StandingSurrender
            && Mathf.Abs(standCmd) < 0.5f)
        {
            int keepPhase = stepPhaseDriver.FallCatchPhaseCode;
            if (keepPhase == (int)StepPhaseDriver.FallCatchPhase.Reach
                || keepPhase == (int)StepPhaseDriver.FallCatchPhase.Catch
                || keepPhase == (int)StepPhaseDriver.FallCatchPhase.Recover)
            {
                float keep = stepPhaseDriver.CurrentStanceSign;
                if (Mathf.Abs(keep) > 0.5f)
                    standCmd = keep;
            }
        }

        bool splitLegs = standLegLevel > 0.001f && Mathf.Abs(standCmd) > 0.5f;
        bool leftIsSwing = splitLegs && standCmd > 0.5f;
        bool rightIsSwing = splitLegs && standCmd < -0.5f;
        bool leftGrounded = FootSupportDown(true);
        bool rightGrounded = FootSupportDown(false);
        bool leftHeel = bodyState != null && bodyState.leftFootHeelLoaded;
        bool rightHeel = bodyState != null && bodyState.rightFootHeelLoaded;
        bool walkDualSupport = walkSoft && leftGrounded && rightGrounded;
        bool fallCatchWalk = walkSoft && stepPhaseDriver != null && stepPhaseDriver.useFallCatchWalk != 0;
        // Companion to airHeel: Catch plant is the heel, not a toe scrape.
        // Hip pose stays on !grounded (sci_a). 0 = former any-contact.
        bool heelAirPose = fallCatchWalk && stepPhaseDriver.fallSwingHeelPlant > 0.5f;
        int fallCatchPhaseCode = fallCatchWalk ? stepPhaseDriver.FallCatchPhaseCode : -1;
        // The landing brake is signed: it must act on a trunk tipping the way
        // we walk and release on one that has already gone past upright the
        // other way. |tilt| alone kept pushing backward through the overshoot.
        float fcLeadDir = intent != null && Mathf.Abs(intent.moveX) > 0.01f
            ? Mathf.Sign(intent.moveX)
            : 1f;
        float bodyTilt = vestibularSystem != null ? vestibularSystem.GetBodyTilt() : 0f;
        if (!fallCatchWalk
            || stepPhaseDriver.FallCatchCycleCount != 0
            || (fallCatchPhaseCode != (int)StepPhaseDriver.FallCatchPhase.Fall
                && fallCatchPhaseCode != (int)StepPhaseDriver.FallCatchPhase.Reach
                && fallCatchPhaseCode != (int)StepPhaseDriver.FallCatchPhase.Catch
                && fallCatchPhaseCode != (int)StepPhaseDriver.FallCatchPhase.Recover))
            firstStepCatchPlantLatched = false;
        FallCatchBalance fc = ComputeFallCatchBalance(
            stepPhaseDriver, fallCatchPhaseCode, fallComLead,
            bodyTilt, angularVelocity, fcLeadDir, walkPlantRockLead, walkPlantLeanRoll,
            walkPlantLeanFirst, lastFirstStepGeoKnee, ref firstStepCatchPlantLatched);
        bool firstStepToeCatch = FirstStepToeCatchReady();

        float stanceComOff = 0f;
        if (splitLegs && bodyState != null && comCalculator != null)
        {
            Vector2 comSt = comCalculator.GetCenterOfMass();
            float stanceX = leftIsSwing ? bodyState.rightFootPosition.x
                : bodyState.leftFootPosition.x;
            stanceComOff = comSt.x - stanceX;
        }

        bool running = intent != null && intent.run > 0.5f;
        float activeStanceDuration = stepPhaseDriver != null
            ? Mathf.Max(0.1f, stepPhaseDriver.ActiveStanceDurationNow())
            : 6f;
        float activeLiftDelay = running ? runStanceLiftDelay : walkStanceLiftDelay;
        activeLiftDelay = Mathf.Min(activeLiftDelay, activeStanceDuration * 0.60f);
        float activeWeightRamp = running && runWeightRamp > 0.01f
            ? runWeightRamp
            : (walkWeightRamp > 0.01f ? walkWeightRamp : walkStanceLiftRamp);
        activeWeightRamp = Mathf.Min(activeWeightRamp, activeStanceDuration * 0.60f);

        float comLiftGate = 0f;
        if (walkSoft && splitLegs)
        {
            float absSt = Mathf.Abs(stanceComOff);
            float comMax = Mathf.Max(0.01f, walkLiftComMax);
            if (absSt <= comMax)
                comLiftGate = 1f;
            else if (absSt <= comMax * 2f)
                comLiftGate = 1f - (absSt - comMax) / comMax;
        }

        // WALK-1 part 1: weight on support when |CoM−stance| ≤ walkLiftComMax — not delay=6.
        // WalkStanceClock, not PhaseCode==0: PhaseCode is Unload/Clear/Advance
        // once the swing knee shortens, and that overlay is still Stance.
        bool walkStancePhase = walkSoft && stepPhaseDriver != null
            && stepPhaseDriver.WalkStanceClock;
        float weightBlend = 0f;
        if (!fallCatchWalk && walkStancePhase && splitLegs && comLiftGate >= 1f)
        {
            float age = stepPhaseDriver.PhaseAge();
            weightBlend = Mathf.Clamp01(age / Mathf.Max(0.05f, activeWeightRamp));
        }

        // Vault-before-push: extend stance (walkStanceExtend × weightBlend) first;
        // unload/peel/scissor only after minimal swing clearance; push only after air.
        float swingClearance = 0f;
        if (splitLegs && bodyState != null)
        {
            swingClearance = leftIsSwing
                ? bodyState.leftFootGroundClearance
                : bodyState.rightFootGroundClearance;
        }
        TickWalkLift(walkSoft, fallCatchWalk, splitLegs, leftIsSwing, rightIsSwing,
            leftGrounded, rightGrounded, comLiftGate, walkStancePhase, swingClearance);
        float minSwingClearance = Mathf.Max(0.005f, walkTouchdownSyncClearance * 0.08f);
        float clearanceGate = 1f;
        if (walkStancePhase && splitLegs)
            clearanceGate = Mathf.Clamp01(swingClearance / minSwingClearance);
        float liftGateBlend = weightBlend * clearanceGate;
        bool pushAfterAir = !walkStancePhase || stepPhaseDriver == null
            || stepPhaseDriver.SwingWasAirborne;

        // How much of the body the stance foot is actually carrying. weightBlend
        // above is the legacy 8 s Stance ramp and is computed only when
        // !fallCatchWalk, so on the gait Play walks it is zero for the whole
        // cycle — while every push-off gate below asks weightBlend >= 0.5. That
        // is the reason walkStancePush and walkSpeedPushGain never reached an
        // ankle and each step ended where it started. Measure the load instead
        // of clocking it: a grounded stance foot takes half the weight in dual
        // support and all of it once the swing foot is off the ground.
        float stanceLoad = weightBlend;
        // Trailing-foot share from the same dual-support statics, or −1 if
        // this step did not measure one (airborne swing, span too small,
        // flag off). plantHipExtend and the Plant trailing-ankle burst both
        // read it so neither rear joint is scaled by the leading foot's load
        // (the hip used to, via the 0.5 floor; the ankle never did — fallback
        // 1, not stanceLoad, or flag 0 would halve the 15:05 burst).
        float dualRearShare = -1f;
        // Plant DrivePelvis release under walkPlantKneeRamp. 1 = folding
        // test unchanged (ramp off, or not in that Plant block). Remapped
        // plantFold otherwise: reachKneeFolding waits until this is >= 0.5
        // so the rear hip keeps DrivePelvis / plantHipExtend through the
        // first half of the 22:36 span.
        float plantHipRelease = 1f;
        // How far the swing leg's passive viscosity is faded this step, 0 = the
        // built value. Fall-catch writes it from DualLoad / airRelease;
        // WALK-1 writes it from the geo-lift clock (walkStanceGeoFriction).
        // Quiet stance, crouch, the pushes and cold one-leg stay at 0.
        float swingFrictionFade = 0f;
        // walkSwingFrictionClampSnap: dirt-only jump to tau K. Touchdown
        // (airRelease > 0) keeps this 0 so the last centimetres still lerp.
        float swingFrictionSnap = 0f;
        // A new Plant is a new trailing foot. Clear last cycle's leftover
        // before this phase writes, so an early Plant→Fall (clock < 0.5,
        // no fold) is bitwise the former Fall start at 0. Also clear on
        // surrender (splitLegs false), which never enters the block below.
        // fallSwingPlantAirHold: Plant 32° fold can now make Plant's own
        // first millimetre of air. The fold block requires swingGroundedNow
        // and this clear used to zero lastPlantSwing on that same airborne
        // frame, so hipair took −swingHipFlex. Keep the last grounded Plant
        // pose across later Plant air; the enter frame (PhaseAge 0) still
        // clears leftover so a new trailing foot is the former Fall start.
        // Flag 0 is the former per-frame clear.
        bool plantSwingGroundedNow = leftIsSwing ? leftGrounded : rightGrounded;
        if (fallCatchWalk
            && fallCatchPhaseCode == (int)StepPhaseDriver.FallCatchPhase.Plant)
        {
            float plantAge = stepPhaseDriver != null ? stepPhaseDriver.PhaseAge() : 0f;
            bool keepPlantAirPose = plantAge > 0.0001f
                && stepPhaseDriver != null
                && stepPhaseDriver.fallSwingPlantAirHold > 0.5f
                && splitLegs
                && !plantSwingGroundedNow
                && (lastPlantSwingReachKnee > 0.5f
                    || lastPlantSwingHipLift > 0.001f
                    || lastPlantSwingThighHold > 0.001f);
            if (!keepPlantAirPose)
            {
                lastPlantSwingReachKnee = 0f;
                lastPlantSwingHipLift = 0f;
                lastPlantSwingAnkleLift = 0f;
                lastPlantSwingThighHold = 0f;
            }
        }
        if (fallCatchWalk && splitLegs && bodyState != null)
        {
            bool stanceGroundedNow = leftIsSwing ? rightGrounded : leftGrounded;
            float airRelease = Mathf.Clamp01(
                swingClearance / Mathf.Max(0.005f, walkTouchdownSyncClearance));
            stanceLoad = stanceGroundedNow
                ? Mathf.Lerp(0.5f, 1f, airRelease)
                : 0f;

            // Same dead-clock hole as weightBlend, one layer up. liftGateBlend
            // is weightBlend × clearanceGate, so it is also zero for the whole
            // fall-catch cycle — and it is the ramp argument of BOTH softeners:
            // dualSupportStandCap froze standLegLevel at 0.40 and unloadSoft
            // froze at dualSupportSwingScale 0.32. Their product is 0.13, so
            // swingHipUnloadBias arrived as 0.041 of the tuned 0.32 and the
            // swing knee target as 40% of swingKneeFlex. That is why every
            // hip-lift cap in the 16°→24° series could not get the foot off
            // the dirt: the command behind those caps was never at full size.
            //
            // The clamp is not a stability constant, it is a guard against
            // unloading a leg that still carries weight. A swing foot in the
            // air carries none, so measure the release instead of clocking it.
            // Dual support keeps the accepted 0.40 / 0.32 floor (the recorded
            // 0.55 and 0.70 folds raised that floor); the ceiling opens only
            // as the foot actually clears. The old note "a brief lift-off
            // sends level→1 and folds after plant" was written against the
            // binary grounded flag — clearance is continuous, so a scrape
            // frame reads ≈0 and a touchdown fades the authority back out
            // over the last walkTouchdownSyncClearance metres of descent.
            if (stanceGroundedNow && walkAirReleaseAuthority > 0.001f)
                liftGateBlend = airRelease * Mathf.Clamp01(walkAirReleaseAuthority);

            // walkDualLoadRelease above: while both feet are down the clearance
            // ramp is pinned at zero, so read the weight share instead. The
            // roll of the CoM from the trailing foot to the leading one IS the
            // unloading of the trailing leg — full span rolled, no weight left
            // on it. Max, not replace: an airborne foot is already released and
            // the clearance ramp owns that half of the cycle.
            bool swingGroundedNow = leftIsSwing ? leftGrounded : rightGrounded;
            if (stanceGroundedNow && swingGroundedNow && comCalculator != null
                && (walkDualLoadRelease > 0.001f || walkStanceLoadShare > 0.001f)
                && intent != null && Mathf.Abs(intent.moveX) > 0.01f)
            {
                float loadDir = Mathf.Sign(intent.moveX);
                float swingFootX = leftIsSwing
                    ? bodyState.leftFootPosition.x
                    : bodyState.rightFootPosition.x;
                float stanceFootX = leftIsSwing
                    ? bodyState.rightFootPosition.x
                    : bodyState.leftFootPosition.x;
                // Negative span is a swing foot standing AHEAD of its own
                // stance (a failed step, a re-seat): there is no trailing leg
                // to unload, so the guard leaves that pose alone.
                float loadSpan = (stanceFootX - swingFootX) * loadDir;
                if (loadSpan >= DualLoadMinSpan)
                {
                    float rolled =
                        (comCalculator.GetCenterOfMass().x - swingFootX) * loadDir;
                    float frontShare = Mathf.Clamp01(rolled / loadSpan);
                    // Cycle-0 3 cm lunge: frontShare ≈ 0.5 at rest is quiet
                    // stance. Do not open the cap / 55° / unload on that
                    // number. Comment 48: StanceLead ≥ 0 does not unload the
                    // planted trailer — opening DualLoad there is the −X
                    // flexor. Keep frontShare 0 while the swing sole is
                    // down. Later cycles keep the span share.
                    bool cycle0 = stepPhaseDriver != null
                        && stepPhaseDriver.FallCatchCycleCount <= 0;
                    if (cycle0 && FirstStepTrailStillLoaded(stepPhaseDriver))
                        frontShare = 0f;
                    else if (cycle0)
                        frontShare = 1f;
                    if (walkDualLoadRelease > 0.001f)
                    {
                        liftGateBlend = Mathf.Max(
                            liftGateBlend,
                            frontShare * Mathf.Clamp01(walkDualLoadRelease));
                    }
                    // walkStanceLoadShare above. Same share, opposite feet:
                    // the leading foot's load into stanceLoad (the 0.5 floor
                    // was half of the weight the stance ankle should push with
                    // once CoM is on it), the trailing foot's load stashed for
                    // the Plant rear hip AND the trailing-ankle A2 burst
                    // (that 0.5 floor kept extending a rear hip that no
                    // longer carries anything; the ankle had no floor, so
                    // it kept pushing at 1). Mix with the floor so 0 is the
                    // former Lerp, plantHipExtend proxy, and unscaled A2.
                    if (walkStanceLoadShare > 0.001f)
                    {
                        float mix = Mathf.Clamp01(walkStanceLoadShare);
                        float floorLoad = stanceLoad;
                        float frontHonest = Mathf.Max(airRelease, frontShare);
                        stanceLoad = Mathf.Lerp(floorLoad, frontHonest, mix);
                        dualRearShare = Mathf.Lerp(floorLoad, 1f - frontShare, mix);
                    }
                }
            }

            // Cycle 0 used to inject liftGateBlend from plantMinDuration /
            // Fall=1, then from DualLoad frontShare once MinSpan matched
            // the 3 cm viewpoint lunge. Both told the controller the
            // trail was unloaded at rest: frontShare is (x_com − x_trail)
            // / span = 0.5 when CoM sits at mid-stance. That is quiet
            // standing, so oneg 16° / unload 0.32 / 55° still squatted a
            // µ=1.2 pair. Cycle-0 dirt now opens DualLoad only once
            // StanceLead ≥ 0 (trail N≈0), then at 1 — not StanceLead /
            // trigger, which is 0 at that same event. Restore / surrender
            // still keep 0.

            // Let the airborne swing leg swing (walkSwingLegFrictionTau above).
            // The envelope is the swing foot's own clearance — the same
            // airRelease the lift authority uses — so viscosity is FULL at the
            // frame of contact and the heel strike is damped exactly as before,
            // a scrape reads about zero, and nothing steps: the damper fades
            // out as the foot leaves and back in over the last
            // walkTouchdownSyncClearance metres of descent.
            // Both feet off the ground is not a swing, it is a fall, so the
            // stance foot must be down for any of this.
            // Either time constant opens the envelope, so the sole can be
            // isolated from the hip and the knee in one run
            // (-walkSwingFrictionTau 0 -walkSwingAnkleFrictionTau 0.10) instead
            // of the two changes only ever being measurable together.
            // walkSwingFrictionLoadShare: clearance-only left the Plant/Fall
            // leftover fold fighting the built weld on dirt. liftGateBlend is
            // already the unload; Max so a catch plant (share not measured,
            // blend 0) stays the former heel-strike restore. Flag 0 is
            // airRelease bit for bit.
            // walkSwingFrictionClampSnap: that Max still Lerped K, and the
            // knee stays on stopTorque until fade ~0.7. Snap to I/tau at the
            // same 0.5 gate the fold ramp opens, only while still on dirt
            // (airRelease 0) so the touchdown lerp is unchanged.
            // walkSwingFrictionSnapHold: that dirt pulse dies at the first
            // millimetre of clearance (airRelease ~0.02), and lerp at 0.02
            // is the weld again. Hold until fade itself is past ~0.7 — the
            // number 23:22 already measured as I/Δt on the knee — or the
            // envelope closes. A catch plant never arms (loadFade 0).
            if (stanceGroundedNow && !running
                && (walkSwingLegFrictionTau > 0.0001f
                    || walkSwingAnkleFrictionTau > 0.0001f)
                && intent != null && Mathf.Abs(intent.moveX) > 0.01f)
            {
                swingFrictionFade = airRelease;
                if (walkSwingFrictionLoadShare > 0.001f
                    || walkPlantSwingFrictionFirst > 0.5f)
                {
                    float loadFade = walkSwingFrictionLoadShare > 0.001f
                        ? liftGateBlend * Mathf.Clamp01(walkSwingFrictionLoadShare)
                        : 0f;
                    // walkPlantSwingFrictionFirst above. Cycle 0 has no
                    // DualLoadMinSpan, so liftGateBlend stays 0 and the 03:45
                    // fold fights the built weld. Same plantMinDuration clock
                    // as the knee; snap still opens at 0.5. Later cycles keep
                    // the share. Restore / surrender keep the weld (brake).
                    if (walkPlantSwingFrictionFirst > 0.5f
                        && fallCatchPhaseCode == (int)StepPhaseDriver.FallCatchPhase.Plant
                        && stepPhaseDriver.FallCatchCycleCount <= 0
                        && !stepPhaseDriver.StandingRestore
                        && !stepPhaseDriver.StandingSurrender
                        && stepPhaseDriver.PlantGateReady)
                    {
                        float firstClock = Mathf.Clamp01(
                            stepPhaseDriver.PhaseAge()
                            / Mathf.Max(0.05f, stepPhaseDriver.plantMinDuration));
                        loadFade = Mathf.Max(loadFade, firstClock);
                    }
                    // walkPlantFallFrictionHoldFirst above. Plant already
                    // faded to 1 (EnterFall requires plantAged); Fall's
                    // PhaseAge is 0, DualLoadMinSpan never opens, airRelease
                    // is 0 on dirt, so the 03:58 fade dies and
                    // ApplySwingLegFriction(0) welds the shin the fold
                    // hold is commanding. Hold that endpoint. Catch /
                    // later cycles keep DualLoad / airRelease.
                    if (walkPlantFallFrictionHoldFirst > 0.5f
                        && walkPlantSwingFrictionFirst > 0.5f
                        && (fallCatchPhaseCode == (int)StepPhaseDriver.FallCatchPhase.Fall
                            || fallCatchPhaseCode == (int)StepPhaseDriver.FallCatchPhase.Reach
                            || fallCatchPhaseCode == (int)StepPhaseDriver.FallCatchPhase.Catch)
                        && stepPhaseDriver.FallCatchCycleCount <= 0
                        && !stepPhaseDriver.StandingRestore
                        && !stepPhaseDriver.StandingSurrender
                        && swingGroundedNow
                        && intent != null && Mathf.Abs(intent.moveX) > 0.01f)
                    {
                        loadFade = Mathf.Max(loadFade, 1f);
                    }
                    swingFrictionFade = Mathf.Max(swingFrictionFade, loadFade);
                    if (walkSwingFrictionClampSnap > 0.001f
                        && airRelease <= 0.001f
                        && loadFade >= 0.5f)
                        swingFrictionSnap = 1f;
                    if (walkSwingFrictionSnapHold > 0.001f
                        && walkSwingFrictionClampSnap > 0.001f)
                    {
                        int holdSide = leftIsSwing ? -1 : 1;
                        if (swingFrictionSnapHoldSide != 0
                            && swingFrictionSnapHoldSide != holdSide)
                            swingFrictionSnapLatched = false;
                        swingFrictionSnapHoldSide = holdSide;
                        if (swingFrictionSnap > 0.5f)
                            swingFrictionSnapLatched = true;
                        // Lerp(K=12, I/tau, fade) leaves stopTorque at ~0.7
                        // on the knee. Holding past that is the airborne
                        // tau endpoint, which lerp already is. fade 0 is
                        // the restore (EndWalk / catch plant / role swap).
                        if (swingFrictionFade >= 0.7f
                            || swingFrictionFade <= 0.0001f)
                            swingFrictionSnapLatched = false;
                        if (swingFrictionSnapLatched)
                            swingFrictionSnap = 1f;
                    }
                }
            }

            // walkPlantSwingKnee above: start the Fall knee-lift in late
            // Plant, once the CoM has already rolled onto the leading foot.
            // ComputeFallCatchBalance ran before this blend existed, and
            // Plant wrote reachKnee 0, so the trailing hip stayed on the
            // pelvis loop for the whole phase. Same hold / hipLift /
            // ankleLift Fall already uses — not −swingHipFlex (sci_a) and
            // not ankleScale 0. Fold is swingKneeFlex, not reachKneeFlex
            // 140°: that fold on a still-shared Plant is a squat.
            if (walkPlantSwingKnee > 0.001f
                && fallCatchPhaseCode == (int)StepPhaseDriver.FallCatchPhase.Plant
                && stanceGroundedNow && swingGroundedNow
                && (stepPhaseDriver.FallCatchCycleCount > 0
                    || walkPlantSwingKneeFirst > 0.5f)
                && !stepPhaseDriver.StandingRestore
                && !stepPhaseDriver.StandingSurrender
                && stepPhaseDriver.PlantGateReady
                && intent != null && Mathf.Abs(intent.moveX) > 0.01f)
            {
                float plantKneeClock = Mathf.Clamp01(liftGateBlend)
                    * Mathf.Clamp01(walkPlantSwingKnee);
                // Cycle 0 used to Max a plantMinDuration clock, then DualLoad
                // frontShare ≥ 0.5, onto this. Both folded 55° at rest
                // (3 cm lunge share is 0.5 standing). DualLoad now opens
                // only once StanceLead ≥ 0 (trail N≈0), then at 1 so
                // clock ≥ 0.5 is that event, not 7.5 mm past the foot.
                // walkPlantSwingKneeFirst still lets cycle 0 enter this
                // block at all.
                // swingKneeFlex is 55°, so plantKneeClock > 0.001 already
                // writes reachKnee > 0.5° — and reachKneeFolding's 0.5° test
                // then takes the trailing hip off the pelvis loop and starts
                // ankleLift. That killed walkPlantHipExtend (needs
                // DrivePelvis) and faded the trailing A2 burst on the first
                // centimetre of CoM roll, which is the opposite of "once the
                // CoM has rolled onto the leading foot". Half the span is
                // that event: the rear then carries less than half the
                // weight, so the fold is pre-swing, not a squat. Flag 0
                // still skips this block entirely. walkPlantKneeRamp then
                // spends the remaining half-span so the gate itself is not
                // a 27.5° step, scales thighHold with that pose, and keeps
                // DrivePelvis until plantFold >= 0.5 (plantHipRelease).
                if (plantKneeClock >= 0.5f)
                {
                    // walkPlantKneeRamp above. Gate stays mid-span (22:00);
                    // the pose ramps from 0 there instead of jumping to
                    // swingKneeFlex × 0.5. thighHold follows plantFold;
                    // plantHipRelease is that same number so DrivePelvis
                    // waits for ThighHold ≥ 0.5. Flag 0 is the multiply.
                    float plantFold = plantKneeClock;
                    if (walkPlantKneeRamp > 0.001f)
                    {
                        float remapped = Mathf.Clamp01((plantKneeClock - 0.5f) / 0.5f);
                        plantFold = Mathf.Lerp(
                            plantKneeClock, remapped, Mathf.Clamp01(walkPlantKneeRamp));
                    }
                    if (plantFold > 0.001f)
                    {
                        float plantAnkleLift = Mathf.Clamp01(stepPhaseDriver.fallSwingAnkleLift);
                        if (stepPhaseDriver.fallSwingFallAnkleLift > 0.5f && plantAnkleLift > 0.001f)
                            plantAnkleLift = 0.5f;
                        // Cycle-0 Plant dirt: 55° × plantFold is the squat
                        // even after StanceLead ≥ 0, because EnterFall still
                        // waits plantMinDuration and a 55° chain in that
                        // freeze window drops the hip. AnkleLift may
                        // unweight; do not seed lastPlant. Fall opens
                        // DualLoad / 55° once the phase actually changes.
                        bool firstStepDirt = stepPhaseDriver.FallCatchCycleCount == 0
                            && swingGroundedNow
                            && !stepPhaseDriver.StandingRestore
                            && !stepPhaseDriver.StandingSurrender;
                        if (firstStepDirt)
                        {
                            plantHipRelease = 0f;
                            fc.ankleLift = Mathf.Max(
                                fc.ankleLift, plantAnkleLift * plantFold);
                            lastPlantSwingAnkleLift = fc.ankleLift;
                        }
                        else
                        {
                            fc.reachKnee = Mathf.Max(
                                fc.reachKnee,
                                Mathf.Max(0f, swingKneeFlex) * plantFold);
                            float plantThighHold = Mathf.Clamp01(
                                stepPhaseDriver.fallSwingThighHold);
                            if (walkPlantKneeRamp > 0.001f)
                            {
                                plantThighHold *= plantFold;
                                plantHipRelease = plantFold;
                            }
                            fc.thighHold = Mathf.Max(fc.thighHold, plantThighHold);
                            float plantHipLift = Mathf.Clamp01(stepPhaseDriver.fallSwingHipLift);
                            if (stepPhaseDriver.fallSwingFallHipLift > 0.5f && plantHipLift > 0.001f)
                                plantHipLift = 0.5f;
                            fc.hipLift = Mathf.Max(fc.hipLift, plantHipLift * plantFold);
                            fc.ankleLift = Mathf.Max(
                                fc.ankleLift, plantAnkleLift * plantFold);
                            lastPlantSwingReachKnee = fc.reachKnee;
                            lastPlantSwingHipLift = fc.hipLift;
                            lastPlantSwingAnkleLift = fc.ankleLift;
                            lastPlantSwingThighHold = fc.thighHold;
                        }
                    }
                }
            }

            // 00:53 can now keep ThighHold on Catch→Reach timeout air.
            // Plant 32° fold writes only while swingGroundedNow, so the
            // millimetre that leftover + hipLift 0.5 just earned dropped
            // reachKnee / thighHold to 0. Keep Max from the last grounded
            // Plant pose so ThighHold still has terms (keepThighHoldAir
            // needs them) and hipair does not take −swingHipFlex.
            // Fall/Reach leftover is still walkPlantFallFoldHold.
            // Quiet when 0: former pose drop.
            if (stepPhaseDriver.fallSwingPlantAirHold > 0.5f
                && fallCatchPhaseCode == (int)StepPhaseDriver.FallCatchPhase.Plant
                && !swingGroundedNow
                && (lastPlantSwingReachKnee > 0.5f
                    || lastPlantSwingHipLift > 0.001f
                    || lastPlantSwingThighHold > 0.001f))
            {
                fc.hipLift = Mathf.Max(fc.hipLift, lastPlantSwingHipLift);
                fc.ankleLift = Mathf.Max(fc.ankleLift, lastPlantSwingAnkleLift);
                fc.thighHold = Mathf.Max(fc.thighHold, lastPlantSwingThighHold);
                fc.reachKnee = Mathf.Max(fc.reachKnee, lastPlantSwingReachKnee);
                // Drive world-flat on that millimetre. Quiet muscles
                // (01:45) left the toe to gravity; latched joint=0
                // paddles it into dirt. Ankle torque is shin–foot, not
                // a pelvis pair — hip stays on ThighHold keep / the
                // stance pelvis loop. Same pose Fall/Reach already
                // write. Flag 0 is airAnkle 0 (then Clear or joint=0).
                if (stepPhaseDriver.fallSwingPlantAirAnkleLevel > 0.5f)
                {
                    fc.airAnkle = Mathf.Max(
                        fc.airAnkle,
                        Mathf.Clamp01(stepPhaseDriver.fallSwingAirAnkle));
                    fc.airHeel = Mathf.Max(
                        fc.airHeel,
                        Mathf.Max(0f, stepPhaseDriver.fallSwingAirHeel));
                }
            }

            // walkPlantFallFoldHold above. Fall's PhaseAge is 0 on the
            // Plant→Fall tick, so the 2× fold/lift clocks write 0 and
            // unfold the Plant pose in one step. Keep Max until the ramp
            // overtakes. Reach extend is landDeg — do not hold past it.
            if (walkPlantFallFoldHold > 0.001f
                && (fallCatchPhaseCode == (int)StepPhaseDriver.FallCatchPhase.Fall
                    || fallCatchPhaseCode == (int)StepPhaseDriver.FallCatchPhase.Reach))
            {
                fc.hipLift = Mathf.Max(fc.hipLift, lastPlantSwingHipLift);
                fc.ankleLift = Mathf.Max(fc.ankleLift, lastPlantSwingAnkleLift);
                fc.thighHold = Mathf.Max(fc.thighHold, lastPlantSwingThighHold);
                bool holdKnee = fallCatchPhaseCode
                    == (int)StepPhaseDriver.FallCatchPhase.Fall;
                // Catch→Reach already keeps fold via its own latches; this
                // leftover is the first Fall→Reach of the cycle. Grounded
                // ReachExtendProgress is 0, so a bounce toward landDeg
                // would otherwise be raised back to the Plant 55°.
                if (!holdKnee
                    && fallCatchPhaseCode == (int)StepPhaseDriver.FallCatchPhase.Reach
                    && !stepPhaseDriver.ReachResumedFromCatch
                    && !stepPhaseDriver.ReachResumedFromScrape
                    && stepPhaseDriver.ReachExtendProgress() <= 0.001f)
                    holdKnee = true;
                if (holdKnee)
                    fc.reachKnee = Mathf.Max(fc.reachKnee, lastPlantSwingReachKnee);
            }
            // walkPlantFallFoldCapFirst above. Fold hold Maxes Plant 55°
            // across PhaseAge 0, then Fall's 2× clock overtakes to 140°
            // in 0.15 s while DualLoadMinSpan never opens. That 140° on
            // a still-shared plant is the squat walkPlantSwingKnee named
            // — pelvis drops onto both soles, the lift-phase strut
            // yields, Fall→Reach waits on air. Cap at the Plant pose
            // while the sole is down. Air keeps 140°. Catch / later
            // cycles keep the envelope. Min so a millimetre of air that
            // opened the Fall clock is not clipped twice.
            if (walkPlantFallFoldCapFirst > 0.5f
                && walkPlantFallFoldHold > 0.001f
                && (fallCatchPhaseCode == (int)StepPhaseDriver.FallCatchPhase.Fall
                    || fallCatchPhaseCode == (int)StepPhaseDriver.FallCatchPhase.Reach)
                && stepPhaseDriver.FallCatchCycleCount <= 0
                && !stepPhaseDriver.StandingRestore
                && !stepPhaseDriver.StandingSurrender
                && swingGroundedNow
                && lastPlantSwingReachKnee > 0.5f
                && intent != null && Mathf.Abs(intent.moveX) > 0.01f)
            {
                float groundedCap = lastPlantSwingReachKnee;
                // walkPlantFallFoldGrowFirst above. Fold cap pinned 55°
                // so 140° would not squat a sagging pelvis. Hip-balance
                // hold + suppressWalkLean now keep that strut, so 55° is
                // the Plant floor, not a ceiling. Quarter of the
                // remaining Fall fold (76°) is still ~0.79 L, not the
                // 0.34 L squat. Air keeps 140°. Flag 0 is the 55° pin.
                if (walkPlantFallFoldGrowFirst > 0.5f
                    && walkPlantFallSuppressWalkLeanFirst > 0.5f
                    && stepPhaseDriver.reachKneeFlex > lastPlantSwingReachKnee + 0.5f)
                {
                    groundedCap = Mathf.Lerp(
                        lastPlantSwingReachKnee,
                        stepPhaseDriver.reachKneeFlex,
                        0.25f);
                    // walkPlantFallFoldGrowMoreFirst above. First quarter
                    // wrote 76°. Leftover-cap grow through AcrossFirst is
                    // spent (0.011 cm at the hip, liftSignal stuck at
                    // −0.945). Same 0.25 of the remaining 64° so the
                    // shin shortens to 92° (2·L·cos(46°) ≈ 0.70 L vs
                    // 0.79 L). Flag 0 keeps 76°. Air keeps 140°.
                    if (walkPlantFallFoldGrowMoreFirst > 0.5f)
                    {
                        groundedCap = Mathf.Lerp(
                            groundedCap,
                            stepPhaseDriver.reachKneeFlex,
                            0.25f);
                        // walkPlantFallFoldGrowYetFirst above. Second
                        // quarter wrote 92°. Leftover-cap grow through
                        // AcrossFirst is spent (0.011 cm at the hip,
                        // liftSignal stuck at −0.945). Same 0.25 of the
                        // remaining 48° so the shin shortens to 104°
                        // (2·L·cos(52°) ≈ 0.62 L vs 0.70 L). Flag 0
                        // keeps 92°. Air keeps 140°.
                        if (walkPlantFallFoldGrowYetFirst > 0.5f)
                        {
                            groundedCap = Mathf.Lerp(
                                groundedCap,
                                stepPhaseDriver.reachKneeFlex,
                                0.25f);
                            // walkPlantFallFoldGrowEvenFirst above. Third
                            // quarter wrote 104°. Leftover-cap grow through
                            // AcrossFirst is spent (0.011 cm at the hip,
                            // liftSignal stuck at −0.945). Same 0.25 of the
                            // remaining 36° so the shin shortens to 113°
                            // (2·L·cos(56.5°) ≈ 0.55 L vs 0.62 L). Flag 0
                            // keeps 104°. Air keeps 140°.
                            if (walkPlantFallFoldGrowEvenFirst > 0.5f)
                            {
                                groundedCap = Mathf.Lerp(
                                    groundedCap,
                                    stepPhaseDriver.reachKneeFlex,
                                    0.25f);
                                // walkPlantFallFoldGrowStillFirst above. Fourth
                                // quarter wrote 113°. Leftover-cap grow through
                                // AcrossFirst is spent (0.011 cm at the hip,
                                // liftSignal stuck at −0.945). Same 0.25 of the
                                // remaining 27° so the shin shortens to 120°
                                // (2·L·cos(60°) ≈ 0.50 L vs 0.55 L). Flag 0
                                // keeps 113°. Air keeps 140°.
                                if (walkPlantFallFoldGrowStillFirst > 0.5f)
                                {
                                    groundedCap = Mathf.Lerp(
                                        groundedCap,
                                        stepPhaseDriver.reachKneeFlex,
                                        0.25f);
                                    // walkPlantFallFoldGrowAgainFirst above. Fifth
                                    // quarter wrote 120°. Leftover-cap grow through
                                    // AcrossFirst is spent (0.011 cm at the hip,
                                    // liftSignal stuck at −0.945). Same 0.25 of the
                                    // remaining 20° so the shin shortens to 125°
                                    // (2·L·cos(62.5°) ≈ 0.46 L vs 0.50 L). Flag 0
                                    // keeps 120°. Air keeps 140°.
                                    if (walkPlantFallFoldGrowAgainFirst > 0.5f)
                                    {
                                        groundedCap = Mathf.Lerp(
                                            groundedCap,
                                            stepPhaseDriver.reachKneeFlex,
                                            0.25f);
                                        // walkPlantFallFoldGrowAlsoFirst above. Sixth
                                        // quarter wrote 125°. Leftover-cap grow through
                                        // AcrossFirst is spent (0.011 cm at the hip,
                                        // liftSignal stuck at −0.945). Same 0.25 of the
                                        // remaining 15° so the shin shortens to 129°
                                        // (2·L·cos(64.5°) ≈ 0.43 L vs 0.46 L). Flag 0
                                        // keeps 125°. Air keeps 140°.
                                        if (walkPlantFallFoldGrowAlsoFirst > 0.5f)
                                        {
                                            groundedCap = Mathf.Lerp(
                                                groundedCap,
                                                stepPhaseDriver.reachKneeFlex,
                                                0.25f);
                                            // walkPlantFallFoldGrowExtraFirst above. Seventh
                                            // quarter wrote 129°. Leftover-cap grow through
                                            // AcrossFirst is spent (0.011 cm at the hip,
                                            // liftSignal stuck at −0.945). Same 0.25 of the
                                            // remaining 11° so the shin shortens to 132°
                                            // (2·L·cos(66°) ≈ 0.41 L vs 0.43 L). Flag 0
                                            // keeps 129°. Air keeps 140°.
                                            if (walkPlantFallFoldGrowExtraFirst > 0.5f)
                                            {
                                                groundedCap = Mathf.Lerp(
                                                    groundedCap,
                                                    stepPhaseDriver.reachKneeFlex,
                                                    0.25f);
                                                // walkPlantFallFoldGrowPlusFirst above. Eighth
                                                // quarter wrote 132°. Leftover-cap grow through
                                                // AcrossFirst is spent (0.011 cm at the hip,
                                                // liftSignal stuck at −0.945). Same 0.25 of the
                                                // remaining 8° so the shin shortens to 134°
                                                // (2·L·cos(67°) ≈ 0.39 L vs 0.41 L). Flag 0
                                                // keeps 132°. Air keeps 140°.
                                                if (walkPlantFallFoldGrowPlusFirst > 0.5f)
                                                {
                                                    groundedCap = Mathf.Lerp(
                                                        groundedCap,
                                                        stepPhaseDriver.reachKneeFlex,
                                                        0.25f);
                                                    // walkPlantFallFoldGrowFurtherFirst above. Ninth
                                                    // quarter wrote 134°. Leftover-cap grow through
                                                    // AcrossFirst is spent (0.011 cm at the hip,
                                                    // liftSignal stuck at −0.945). Same 0.25 of the
                                                    // remaining 6° so the shin shortens to 136°
                                                    // (2·L·cos(68°) ≈ 0.37 L vs 0.39 L). Flag 0
                                                    // keeps 134°. Air keeps 140°.
                                                    if (walkPlantFallFoldGrowFurtherFirst > 0.5f)
                                                    {
                                                        groundedCap = Mathf.Lerp(
                                                            groundedCap,
                                                            stepPhaseDriver.reachKneeFlex,
                                                            0.25f);
                                                        // walkPlantFallFoldGrowOnwardFirst above. Tenth
                                                        // quarter wrote 136°. Leftover-cap grow through
                                                        // AcrossFirst is spent (0.011 cm at the hip,
                                                        // liftSignal stuck at −0.945). Same 0.25 of the
                                                        // remaining 4° so the shin shortens to 137°
                                                        // (2·L·cos(68.5°) ≈ 0.37 L vs 0.37 L). Flag 0
                                                        // keeps 136°. Air keeps 140°.
                                                        if (walkPlantFallFoldGrowOnwardFirst > 0.5f)
                                                        {
                                                            groundedCap = Mathf.Lerp(
                                                                groundedCap,
                                                                stepPhaseDriver.reachKneeFlex,
                                                                0.25f);
                                                            // walkPlantFallFoldGrowAheadFirst above. Eleventh
                                                            // quarter wrote 137°. Leftover-cap grow through
                                                            // AcrossFirst is spent (0.011 cm at the hip,
                                                            // liftSignal stuck at −0.945). Same 0.25 of the
                                                            // remaining 3° so the shin shortens to 138°
                                                            // (2·L·cos(69°) ≈ 0.36 L vs 0.37 L). Flag 0
                                                            // keeps 137°. Air keeps 140°.
                                                            if (walkPlantFallFoldGrowAheadFirst > 0.5f)
                                                            {
                                                                groundedCap = Mathf.Lerp(
                                                                    groundedCap,
                                                                    stepPhaseDriver.reachKneeFlex,
                                                                    0.25f);
                                                                // walkPlantFallFoldGrowAlongFirst above. Twelfth
                                                                // quarter wrote 138°. Leftover-cap grow through
                                                                // AcrossFirst is spent (0.011 cm at the hip,
                                                                // liftSignal stuck at −0.945). Same 0.25 of the
                                                                // remaining 2° so the shin shortens to 139°
                                                                // (2·L·cos(69.5°) ≈ 0.35 L vs 0.36 L). Flag 0
                                                                // keeps 138°. Air keeps 140°.
                                                                if (walkPlantFallFoldGrowAlongFirst > 0.5f)
                                                                {
                                                                    groundedCap = Mathf.Lerp(
                                                                        groundedCap,
                                                                        stepPhaseDriver.reachKneeFlex,
                                                                        0.25f);
                                                                    // walkPlantFallFoldGrowCloseFirst above. Thirteenth
                                                                    // quarter wrote 139°. Leftover-cap grow through
                                                                    // AcrossFirst is spent (0.011 cm at the hip,
                                                                    // liftSignal stuck at −0.945). Same 0.25 of the
                                                                    // remaining 1° so the shin shortens to 139.25°
                                                                    // (2·L·cos(69.625°) ≈ 0.35 L vs 0.35 L). Flag 0
                                                                    // keeps 139°. Air keeps 140°.
                                                                    if (walkPlantFallFoldGrowCloseFirst > 0.5f)
                                                                    {
                                                                        groundedCap = Mathf.Lerp(
                                                                            groundedCap,
                                                                            stepPhaseDriver.reachKneeFlex,
                                                                            0.25f);
                                                                        // walkPlantFallFoldGrowNearFirst above. Fourteenth
                                                                        // quarter wrote 139.25°. Leftover-cap grow through
                                                                        // AcrossFirst is spent (0.011 cm at the hip,
                                                                        // liftSignal stuck at −0.945). Same 0.25 of the
                                                                        // remaining 0.75° so the shin shortens to 139.5°
                                                                        // (2·L·cos(69.72°) ≈ 0.35 L vs 0.35 L). Flag 0
                                                                        // keeps 139.25°. Air keeps 140°.
                                                                        if (walkPlantFallFoldGrowNearFirst > 0.5f)
                                                                        {
                                                                            groundedCap = Mathf.Lerp(
                                                                                groundedCap,
                                                                                stepPhaseDriver.reachKneeFlex,
                                                                                0.25f);
                                                                            // walkPlantFallFoldGrowReachFirst above. Nested
                                                                            // 0.25 from Plant 55° is ~138.86°, not the
                                                                            // rounded 139.5°. Leftover-cap grow through
                                                                            // AcrossFirst is spent (0.011 cm at the hip,
                                                                            // liftSignal stuck at −0.945). Pin to
                                                                            // reachKneeFlex so the shin is the 0.34 L
                                                                            // squat Fall's 2× clock already commands in
                                                                            // air. Flag 0 keeps the nested ~138.86°.
                                                                            // Air keeps 140°.
                                                                            if (walkPlantFallFoldGrowReachFirst > 0.5f)
                                                                            {
                                                                                groundedCap =
                                                                                    stepPhaseDriver.reachKneeFlex;
                                                                            }
                                                                        }
                                                                    }
                                                                }
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                // walkPlantFallFoldSeedFirst above. Grow raised the Min
                // to 76°; fold hold still Maxes Plant 55° at PhaseAge 0,
                // so the first ~8 Fall frames sit on the pose that already
                // failed to clear. Max to the grow cap on dirt so that
                // shortening is live on the first grounded Fall frame.
                // Flag 0 waits for the 2× clock. Air keeps 140°.
                if (walkPlantFallFoldSeedFirst > 0.5f
                    && walkPlantFallFoldGrowFirst > 0.5f)
                    fc.reachKnee = Mathf.Max(fc.reachKnee, groundedCap);
                fc.reachKnee = Mathf.Min(fc.reachKnee, groundedCap);
                // walkPlantFallLiftSeedFirst above. Fold seed put 76°
                // on the shin immediately; hipLift/ankleLift stayed at
                // the Plant 0.5 leftover sized for 55°. Extra knee
                // flex without that pair is a backward slide on µ=0.4
                // (WALK-2 forbids that start). Same 0.25 toward 1 as
                // the fold grow. Flag 0 keeps the Plant leftover.
                // Air keeps 0.5 (block is swingGroundedNow).
                if (walkPlantFallLiftSeedFirst > 0.5f
                    && walkPlantFallFoldSeedFirst > 0.5f
                    && walkPlantFallFoldGrowFirst > 0.5f)
                {
                    const float growT = 0.25f;
                    fc.hipLift = Mathf.Max(
                        fc.hipLift,
                        Mathf.Lerp(lastPlantSwingHipLift, 1f, growT));
                    fc.ankleLift = Mathf.Max(
                        fc.ankleLift,
                        Mathf.Lerp(lastPlantSwingAnkleLift, 1f, growT));
                }
                // walkPlantFallLiftGrowFirst above. Floor seed matched
                // 0.625 so leftover 32° holds −0.391. Remaining gap to 1
                // is 0.375 of bias; a 1.0 floor into 40° is sci_a. Same
                // 0.25 of that leftover so the trio stays matched (0.719,
                // liftSignal −0.517). Flag 0 keeps 0.625. Air keeps 0.625
                // (this block is swingGroundedNow).
                if (walkPlantFallLiftGrowFirst > 0.5f
                    && walkPlantFallLiftFloorSeedFirst > 0.5f
                    && walkPlantFallLiftSeedFirst > 0.5f
                    && walkPlantFallFoldSeedFirst > 0.5f
                    && walkPlantFallFoldGrowFirst > 0.5f)
                {
                    const float growT = 0.25f;
                    fc.hipLift = Mathf.Max(
                        fc.hipLift,
                        Mathf.Lerp(fc.hipLift, 1f, growT));
                    fc.ankleLift = Mathf.Max(
                        fc.ankleLift,
                        Mathf.Lerp(fc.ankleLift, 1f, growT));
                    // walkPlantFallLiftGrowMoreFirst above. GrowFirst
                    // put 0.719; leftover 32° holds −0.517. Remaining
                    // gap to 1 is 0.281 of bias; a 1.0 floor into 40°
                    // is sci_a. Same 0.25 of that leftover so the trio
                    // stays matched (0.789, liftSignal −0.623). Flag 0
                    // keeps 0.719. Air keeps 0.719 (this block is
                    // swingGroundedNow).
                    if (walkPlantFallLiftGrowMoreFirst > 0.5f)
                    {
                        fc.hipLift = Mathf.Max(
                            fc.hipLift,
                            Mathf.Lerp(fc.hipLift, 1f, growT));
                        fc.ankleLift = Mathf.Max(
                            fc.ankleLift,
                            Mathf.Lerp(fc.ankleLift, 1f, growT));
                        // walkPlantFallLiftGrowYetFirst above. GrowMore
                        // put 0.789; leftover 32° holds −0.623. Remaining
                        // gap to 1 is 0.211 of bias; a 1.0 floor into 40°
                        // is sci_a. Same 0.25 of that leftover so the trio
                        // stays matched (0.842, liftSignal −0.709). Flag 0
                        // keeps 0.789. Air keeps 0.789 (this block is
                        // swingGroundedNow).
                        if (walkPlantFallLiftGrowYetFirst > 0.5f)
                        {
                            fc.hipLift = Mathf.Max(
                                fc.hipLift,
                                Mathf.Lerp(fc.hipLift, 1f, growT));
                            fc.ankleLift = Mathf.Max(
                                fc.ankleLift,
                                Mathf.Lerp(fc.ankleLift, 1f, growT));
                            // walkPlantFallLiftGrowEvenFirst above. GrowYet
                            // put 0.842; leftover 32° holds −0.709. Remaining
                            // gap to 1 is 0.158 of bias; a 1.0 floor into 40°
                            // is sci_a. Same 0.25 of that leftover so the trio
                            // stays matched (0.882, liftSignal −0.778). Flag 0
                            // keeps 0.842. Air keeps 0.842 (this block is
                            // swingGroundedNow).
                            if (walkPlantFallLiftGrowEvenFirst > 0.5f)
                            {
                                fc.hipLift = Mathf.Max(
                                    fc.hipLift,
                                    Mathf.Lerp(fc.hipLift, 1f, growT));
                                fc.ankleLift = Mathf.Max(
                                    fc.ankleLift,
                                    Mathf.Lerp(fc.ankleLift, 1f, growT));
                                // walkPlantFallLiftGrowStillFirst above. GrowEven
                                // put 0.882; leftover 32° holds −0.778. Remaining
                                // gap to 1 is 0.118 of bias; a 1.0 floor into 40°
                                // is sci_a. Same 0.25 of that leftover so the trio
                                // stays matched (0.912, liftSignal −0.831). Flag 0
                                // keeps 0.882. Air keeps 0.882 (this block is
                                // swingGroundedNow).
                                if (walkPlantFallLiftGrowStillFirst > 0.5f)
                                {
                                    fc.hipLift = Mathf.Max(
                                        fc.hipLift,
                                        Mathf.Lerp(fc.hipLift, 1f, growT));
                                    fc.ankleLift = Mathf.Max(
                                        fc.ankleLift,
                                        Mathf.Lerp(fc.ankleLift, 1f, growT));
                                    // walkPlantFallLiftGrowAgainFirst above. GrowStill
                                    // put 0.912; leftover 32° holds −0.831. Remaining
                                    // gap to 1 is 0.088 of bias; a 1.0 floor into 40°
                                    // is sci_a. Same 0.25 of that leftover so the trio
                                    // stays matched (0.934, liftSignal −0.872). Flag 0
                                    // keeps 0.912. Air keeps 0.912 (this block is
                                    // swingGroundedNow).
                                    if (walkPlantFallLiftGrowAgainFirst > 0.5f)
                                    {
                                        fc.hipLift = Mathf.Max(
                                            fc.hipLift,
                                            Mathf.Lerp(fc.hipLift, 1f, growT));
                                        fc.ankleLift = Mathf.Max(
                                            fc.ankleLift,
                                            Mathf.Lerp(fc.ankleLift, 1f, growT));
                                        // walkPlantFallLiftGrowAlsoFirst above. GrowAgain
                                        // put 0.934; leftover 32° holds −0.872. Remaining
                                        // gap to 1 is 0.066 of bias; a 1.0 floor into 40°
                                        // is sci_a. Same 0.25 of that leftover so the trio
                                        // stays matched (0.951, liftSignal −0.904). Flag 0
                                        // keeps 0.934. Air keeps 0.934 (this block is
                                        // swingGroundedNow).
                                        if (walkPlantFallLiftGrowAlsoFirst > 0.5f)
                                        {
                                            fc.hipLift = Mathf.Max(
                                                fc.hipLift,
                                                Mathf.Lerp(fc.hipLift, 1f, growT));
                                            fc.ankleLift = Mathf.Max(
                                                fc.ankleLift,
                                                Mathf.Lerp(fc.ankleLift, 1f, growT));
                                            // walkPlantFallLiftGrowExtraFirst above. GrowAlso
                                            // put 0.951; leftover 32° holds −0.904. Remaining
                                            // gap to 1 is 0.049 of bias; a 1.0 floor into 40°
                                            // is sci_a. Same 0.25 of that leftover so the trio
                                            // stays matched (0.963, liftSignal −0.927). Flag 0
                                            // keeps 0.951. Air keeps 0.951 (this block is
                                            // swingGroundedNow).
                                            if (walkPlantFallLiftGrowExtraFirst > 0.5f)
                                            {
                                                fc.hipLift = Mathf.Max(
                                                    fc.hipLift,
                                                    Mathf.Lerp(fc.hipLift, 1f, growT));
                                                fc.ankleLift = Mathf.Max(
                                                    fc.ankleLift,
                                                    Mathf.Lerp(fc.ankleLift, 1f, growT));
                                                // walkPlantFallLiftGrowPlusFirst above. GrowExtra
                                                // put 0.963; leftover 32° holds −0.927. Remaining
                                                // gap to 1 is 0.037 of bias; a 1.0 floor into 40°
                                                // is sci_a. Same 0.25 of that leftover so the trio
                                                // stays matched (0.972, liftSignal −0.945). Flag 0
                                                // keeps 0.963. Air keeps 0.963 (this block is
                                                // swingGroundedNow).
                                                if (walkPlantFallLiftGrowPlusFirst > 0.5f)
                                                {
                                                    fc.hipLift = Mathf.Max(
                                                        fc.hipLift,
                                                        Mathf.Lerp(fc.hipLift, 1f, growT));
                                                    fc.ankleLift = Mathf.Max(
                                                        fc.ankleLift,
                                                        Mathf.Lerp(fc.ankleLift, 1f, growT));
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                // walkPlantFallLiftFloorSeedFirst is applied later, at
                // hipLiftFloor: the 0.625 bias still met a 0.5 fade floor
                // so leftover at 32° halved it. Grow then raises that
                // matched floor with the pair. Same 0.25. Air keeps
                // 0.5 / 0.625 (this block is swingGroundedNow).
            }
            // walkPlantFallHipBalanceHoldFirst above. Fold cap kept the
            // Plant 55° on dirt, then Fall's calm envelope cut the stance
            // hip loop to 0.15 (Reach 0.25). Cycle 0 never opens
            // DualLoadMinSpan, so that is the lift-phase strut: the
            // pelvis sags into the 55° the cap just saved. Hold Plant's
            // 1 while the swing sole is down. Air keeps 0.15 / 0.25.
            // Catch / later cycles keep the envelope. Max so a DualLoad
            // millimetre that already wrote 1 is not assigned twice.
            if (walkPlantFallHipBalanceHoldFirst > 0.5f
                && walkPlantFallFoldCapFirst > 0.5f
                && (fallCatchPhaseCode == (int)StepPhaseDriver.FallCatchPhase.Fall
                    || fallCatchPhaseCode == (int)StepPhaseDriver.FallCatchPhase.Reach)
                && stepPhaseDriver != null
                && stepPhaseDriver.FallCatchCycleCount <= 0
                && !stepPhaseDriver.StandingRestore
                && !stepPhaseDriver.StandingSurrender
                && swingGroundedNow
                && intent != null && Mathf.Abs(intent.moveX) > 0.01f)
            {
                fc.hipBalanceScale = Mathf.Max(fc.hipBalanceScale, 1f);
            }
            // walkPlantFallSuppressWalkLeanFirst above. Hip-balance hold
            // kept the stance-hip loop at 1, then Fall/Reach dropped
            // Plant's suppressWalkLean. Cycle 0 never opens
            // DualLoadMinSpan, so walkSpeedError is a full 1 m/s miss:
            // 6° auto + 2° base on the pelvis, 7.2° + 4° on the chest,
            // on top of the held 9° fc lean. Lumbar ±20° takes that
            // chest step as a stop slam; the third-law reaction on the
            // pelvis cancels the roll-over. Hold Plant's suppress while
            // the swing sole is down. Air keeps speed/base lean. Catch
            // / later cycles keep RecoverBrake / Plant suppress.
            if (walkPlantFallSuppressWalkLeanFirst > 0.5f
                && walkPlantFallHipBalanceHoldFirst > 0.5f
                && (fallCatchPhaseCode == (int)StepPhaseDriver.FallCatchPhase.Fall
                    || fallCatchPhaseCode == (int)StepPhaseDriver.FallCatchPhase.Reach)
                && stepPhaseDriver != null
                && stepPhaseDriver.FallCatchCycleCount <= 0
                && !stepPhaseDriver.StandingRestore
                && !stepPhaseDriver.StandingSurrender
                && swingGroundedNow
                && intent != null && Mathf.Abs(intent.moveX) > 0.01f)
            {
                fc.suppressWalkLean = true;
            }
            // walkPlantFallLeanHoldFirst above. Plant already leaned to
            // clock 1 (EnterFall requires plantAged). Fall's PhaseAge
            // is 0, so fallLiftClock is 0 and Fall assigns pelvisLean
            // = 0 — a 9°→0 step, the third initiation leg. Comment 31:
            // hold the full 9°, not leanRoom × calm (Capture room is 0
            // as soon as any forward velocity appears; calm at 9° is
            // ≈ 0.33). Fall's own clock Maxes when it overtakes. Catch
            // / later cycles keep envelopes.
            if (walkPlantFallLeanHoldFirst > 0.5f
                && walkPlantLeanFirst > 0.5f
                && walkPlantLeanRoll > 0.0001f
                && (fallCatchPhaseCode == (int)StepPhaseDriver.FallCatchPhase.Fall
                    || fallCatchPhaseCode == (int)StepPhaseDriver.FallCatchPhase.Reach)
                && stepPhaseDriver != null
                && stepPhaseDriver.FallCatchCycleCount <= 0
                && !stepPhaseDriver.StandingRestore
                && !stepPhaseDriver.StandingSurrender
                && stepPhaseDriver.fallComLeadTrigger > 0.0001f
                && intent != null && Mathf.Abs(intent.moveX) > 0.01f)
            {
                // Comment 31: Plant's cycle-0 lean is not × RecoverBlend
                // and not faded on Capture. EnterFall must not step that
                // 9° down — Capture leanRoom is 0 the moment any forward
                // velocity appears, and calm at 9° is ≈ 0.33.
                float heldLean = stepPhaseDriver.fallPelvisLean
                    * Mathf.Max(0f, stepPhaseDriver.fallForwardLeanScale)
                    * Mathf.Clamp01(walkPlantLeanRoll);
                fc.pelvisLean = Mathf.Max(fc.pelvisLean, heldLean);
                fc.torsoLean = Mathf.Max(fc.torsoLean, heldLean);
            }
            // walkPlantFallRockHoldFirst above. Plant already rocked to
            // clock 1 (EnterFall requires plantAged). Fall replaces
            // comLead with fallComLead × blend (0.025 m) and the sole
            // roll starts at PhaseAge 0, so the first Fall frames pin
            // CoM on a stationary setpoint. Grounded Reach writes
            // comLead 0 — the WALK-2 root on the lift-phase strut.
            // Hold the Plant endpoint. Catch / later cycles keep
            // fallComLead / RecoverBrake. Max so the Fall toe-roll is
            // not capped at 0.03 once rollT overtakes.
            if (walkPlantFallRockHoldFirst > 0.5f
                && walkPlantRockLead > 0.0001f
                && (fallCatchPhaseCode == (int)StepPhaseDriver.FallCatchPhase.Fall
                    || fallCatchPhaseCode == (int)StepPhaseDriver.FallCatchPhase.Reach)
                && stepPhaseDriver != null
                && stepPhaseDriver.FallCatchCycleCount <= 0
                && !stepPhaseDriver.StandingRestore
                && !stepPhaseDriver.StandingSurrender
                && intent != null && Mathf.Abs(intent.moveX) > 0.01f)
            {
                float leadBound = Mathf.Max(0.02f, stepPhaseDriver.plantComLeadMax);
                float rock = Mathf.Min(walkPlantRockLead, leadBound * 0.5f);
                float rockRoom = Mathf.Clamp01(
                    1f - stepPhaseDriver.StanceLead / leadBound);
                // This block is cycle-0 only. Plant rock is not × RecoverBlend
                // (comment 30); EnterFall must not step that 3 cm setpoint down.
                float heldRock = rock * rockRoom;
                fc.comLead = Mathf.Max(fc.comLead, heldRock);
            }
            // walkPlantFallAnkleScaleHoldFirst above. Plant/Fall already
            // wrote ankleScale 1. Reach's calm envelope cuts it to 0.25
            // on the first grounded Fall→Reach frame (Fall times out at
            // 0.15 s with the swing still down). The rock-lead hold then
            // keeps a 0.03 m setpoint whose torque never arrives. Hold
            // scale 1 only while the swing sole is down AND comLead is
            // live — scale 1 with comLead 0 is the WALK-2 root at full
            // strength (stance ankle holds CoM over the stacked feet).
            // Air Reach keeps 0.25. Catch / later cycles keep envelopes.
            if (walkPlantFallAnkleScaleHoldFirst > 0.5f
                && walkPlantFallRockHoldFirst > 0.5f
                && walkPlantRockLead > 0.0001f
                && (fallCatchPhaseCode == (int)StepPhaseDriver.FallCatchPhase.Fall
                    || fallCatchPhaseCode == (int)StepPhaseDriver.FallCatchPhase.Reach)
                && stepPhaseDriver != null
                && stepPhaseDriver.FallCatchCycleCount <= 0
                && !stepPhaseDriver.StandingRestore
                && !stepPhaseDriver.StandingSurrender
                && swingGroundedNow
                && fc.comLead > 0.0001f
                && intent != null && Mathf.Abs(intent.moveX) > 0.01f)
            {
                fc.ankleScale = Mathf.Max(fc.ankleScale, 1f);
            }
        }
        // Restore is not conditional on the gate: the step that closes it still
        // has to put the built viscosity back. Envelope closed (fade 0, no
        // snap) also drops the lift-off latch, so EndWalk / a fall / no
        // moveX cannot leak tau K onto the next quiet stance.
        // Hip snap stays off (that plus WORLD shear split the legs). Ankle
        // K=3 welds the shin to the flat foot: while cycle-0 Plant still
        // has CoM behind the leading sole, snap only the ankles (comment
        // 44) so the trailing heel-press can rotate the stack about the
        // ground. Hips keep DrivePelvis / built K. StanceLead < 0 is the
        // brake window, not "swing still down" — once CoM is on the lead
        // that ankle must hold, so K returns.
        float stanceHipFade = 0f;
        float stanceHipSnap = 0f;
        float stanceAnkleSnap = 0f;
        if (fallCatchWalk
            && fallCatchPhaseCode == (int)StepPhaseDriver.FallCatchPhase.Plant
            && stepPhaseDriver != null
            && stepPhaseDriver.FallCatchCycleCount <= 0
            && stepPhaseDriver.SwingFootIsGrounded
            && stepPhaseDriver.StanceLead < 0f
            && !stepPhaseDriver.StandingRestore
            && !stepPhaseDriver.StandingSurrender
            && stepPhaseDriver.PlantGateReady)
        {
            stanceAnkleSnap = 1f;
        }
        // WALK-1 geo-lift clock. Shared with the knee/hip write below:
        // DualLoad never opens on stacked soles, so the fall-catch
        // friction block above never runs and the hip/knee weld ate
        // LiftKneeFlexDeg. Quiet / oneg / pushes have no moveX.
        float walkGeoLiftU = 0f;
        bool walkStanceGeoLift = false;
        bool walk1GeoFriction = false;
        int liftStanceSign = leftIsSwing ? 1 : (rightIsSwing ? -1 : 0);
        if (!walkStancePhase || !splitLegs || liftStanceSign == 0)
        {
            walkFirstLiftHoldAge = 0f;
            walkFirstLiftStanceSign = 0;
        }
        if (!fallCatchWalk
            && walkSoft
            && !running
            && walkGeoSwingLift > 0.5f
            && splitLegs
            && walkStancePhase
            && intent != null
            && Mathf.Abs(intent.moveX) > 0.01f
            && stepPhaseDriver != null)
        {
            if (liftStanceSign != walkFirstLiftStanceSign
                || stepPhaseDriver.PhaseAge() < 0.02f)
            {
                walkFirstLiftHoldAge = 0f;
                walkFirstLiftStanceSign = liftStanceSign;
            }
            float dur = Mathf.Max(0.05f, walkLiftUnloadSeconds);
            // PlantStandReady is xi past the stance toe. That event is
            // the lift — not Idle 0.10 s, not the split. Flag 0 is the
            // former Stance-enter clock. dualSupportStandCap 0.40 stays
            // until air.
            float liftLead = Mathf.Max(0f, walkFirstLiftLead);
            // Dirt Unload is the knee shorten. Waiting for xi past the
            // toe (PlantStandReady / walkFirstLiftLead 0.03) is the
            // closed loop: both soles down, walkPhase stuck at 0.
            // WalkLift overlay starts that shorten on the first Stance
            // frames; this clock must start with it, not after the toe.
            bool comOnStance = useWalkLift != 0
                || liftLead <= 0.0001f
                || stepPhaseDriver.PlantStandReady
                || stepPhaseDriver.PhaseAge()
                    >= Mathf.Max(0.05f, walkLiftIdleSeconds);
            if (comOnStance)
                walkFirstLiftHoldAge += Time.fixedDeltaTime;
            else
                walkFirstLiftHoldAge = 0f;
            if (comOnStance)
                walkGeoLiftU = Smooth01(
                    walkFirstLiftHoldAge / dur);
            // Dirt Unload used to suppress this once WalkLift owned the
            // swing. Knee last-write then took Unload's 35°; hip last-write
            // still took unload−5° (WalkLiftOwns first). burn_walk: kneeL
            // 42°, both soles down, EnterStance 5.79, then UpdateState
            // 0.30 at 5.835. Dirt clearance is geo-lift (knee + hip).
            // WalkLift IK is the air track and Advance plant. Quiet /
            // oneg never set moveX.
            walkStanceGeoLift = walkDualSupport
                && walkGeoLiftU > 0.001f;
            walk1GeoFriction = walkStanceGeoFriction > 0.5f
                && (walkSwingLegFrictionTau > 0.0001f
                    || walkSwingAnkleFrictionTau > 0.0001f);
        }
        if (walk1GeoFriction)
        {
            bool stanceDown = leftIsSwing ? rightGrounded : leftGrounded;
            bool swingDown = leftIsSwing ? leftGrounded : rightGrounded;
            if (stanceDown)
            {
                float airRelease = Mathf.Clamp01(
                    swingClearance
                    / Mathf.Max(0.005f, walkTouchdownSyncClearance));
                // Dirt: geo clock. First air: hold so the weld cannot
                // re-clamp and re-plant (same millimetre hole as
                // walkSwingFrictionSnapHold on fall-catch).
                float geoFade = swingDown
                    ? walkGeoLiftU
                    : Mathf.Max(walkGeoLiftU, airRelease);
                swingFrictionFade = Mathf.Max(swingFrictionFade, geoFade);
                if (walkSwingFrictionClampSnap > 0.001f
                    && swingDown
                    && walkGeoLiftU >= 0.5f)
                    swingFrictionSnap = 1f;
                if (walkSwingFrictionSnapHold > 0.001f
                    && walkSwingFrictionClampSnap > 0.001f)
                {
                    int holdSide = leftIsSwing ? -1 : 1;
                    if (swingFrictionSnapHoldSide != 0
                        && swingFrictionSnapHoldSide != holdSide)
                        swingFrictionSnapLatched = false;
                    swingFrictionSnapHoldSide = holdSide;
                    if (swingFrictionSnap > 0.5f)
                        swingFrictionSnapLatched = true;
                    if (swingFrictionFade >= 0.7f
                        || swingFrictionFade <= 0.0001f)
                        swingFrictionSnapLatched = false;
                    if (swingFrictionSnapLatched)
                        swingFrictionSnap = 1f;
                }
            }
        }
        // WalkLift Unload owns the swing hip/knee command, so geo-lift
        // (walkStanceGeoLift) is skipped. The hip/knee weld (K=12 stopTorque)
        // is stronger than that command: HoldWalkLiftShorten then does
        // not shorten the strut and swingFoot stays 1. Same envelope as
        // walkStanceGeoFriction, on the Unload clock, hip/knee only.
        if (WalkLiftOwnsSwing()
            && walkStanceGeoFriction > 0.5f
            && !fallCatchWalk
            && (walkLiftPhase == WalkLiftPhase.Unload
                || walkLiftPhase == WalkLiftPhase.Clear))
        {
            float u = walkLiftPhase == WalkLiftPhase.Clear
                ? 1f
                : Smooth01(
                    walkLiftPhaseAge
                    / Mathf.Max(0.05f, walkLiftUnloadSeconds));
            swingFrictionFade = Mathf.Max(swingFrictionFade, u);
            if (walkSwingFrictionClampSnap > 0.001f && u >= 0.5f)
                swingFrictionSnap = 1f;
        }
        // First-stance roll: fade the swing weld before Unload so xi can
        // reach the stance toe. includeAnkle stays fallCatchWalk below.
        if (FirstWalkSwingYield()
            && walkStanceGeoFriction > 0.5f
            && (walkSwingLegFrictionTau > 0.0001f
                || walkSwingAnkleFrictionTau > 0.0001f))
            swingFrictionFade = Mathf.Max(swingFrictionFade, 1f);
        if (swingFrictionFade <= 0.0001f && swingFrictionSnap <= 0.5f
            && stanceHipFade <= 0.0001f && stanceHipSnap <= 0.5f
            && stanceAnkleSnap <= 0.5f)
        {
            swingFrictionSnapLatched = false;
            swingFrictionSnapHoldSide = 0;
        }
        if (swingFrictionFade > 0.0001f || swingFrictionSnap > 0.5f
            || stanceHipFade > 0.0001f || stanceHipSnap > 0.5f
            || stanceAnkleSnap > 0.5f
            || legFrictionFaded)
        {
            ApplySwingLegFriction(
                swingFrictionFade, leftIsSwing, swingFrictionSnap,
                stanceHipFade, stanceHipSnap, stanceAnkleSnap,
                fallCatchWalk);
        }

        // Timed liftAge kept for ankle fade only; cap/unload/scissor use liftGateBlend.
        // PhaseCode==0 on purpose: WalkStanceClock stays true during Unload,
        // and opening this 8 s ramp there lifted dualSupportStandCap off 0.40
        // with both soles down (burn_walk standLegLevel 1.000).
        float liftAge = 0f;
        if (!fallCatchWalk && walkSoft && splitLegs && stepPhaseDriver != null
            && stepPhaseDriver.PhaseCode == 0)
        {
            float age = stepPhaseDriver.PhaseAge();
            liftAge = Mathf.Clamp01(
                (age - activeLiftDelay) / Mathf.Max(0.05f, walkStanceLiftRamp));
        }

        float liftBlend = 0f;
        if (!fallCatchWalk && walkSoft && splitLegs && stepPhaseDriver != null
            && stepPhaseDriver.PhaseCode == 0)
            liftBlend = liftAge * comLiftGate;
        lastLiftBlend = liftBlend;
        // Unload/knee is squared: early dual-support does not jerk, end of Stance is sharper.
        float liftAct = liftBlend * liftBlend;

        // Both feet down, CoM over support — the loop looks like single-support.
        bool walkSingleSupport = walkDualSupport && splitLegs && liftBlend >= 0.5f;

        // Cap level by a ramp, not by lifting the clamp: else unload never lifts the foot.
        // capBlend takes both timed lift and weight on support — else cap=0.4
        // keeps unload at zero for almost all of Stance.
        //
        // Cycle-0 geo lift (walkGeoSwingLift) shortens the swing while the
        // stance knee is locked at 0. That only unloads the swing sole if
        // the long strut holds 70 kg. DualLoadMinSpan never opens on stacked
        // feet, so liftGateBlend stays 0 and this cap freezes standLegLevel
        // at 0.40 — the pelvis sags with the short leg, the probe stays
        // grounded, Reach never sees air, and µ=1.2 cannot slide the sole
        // to the catch plant. Skip the cap on that window only: Plant/Fall/
        // Reach/Catch/Recover of cycle 0 while DualLoad is still shut.
        // Recover used to clip standLegLevel to 0.40 on the swap frame
        // (comment 19) and sag the 8°/6° sole off GetContacts. Later
        // cycles keep 0.40. Not a DualLoad open (that Lerp to 0.55 is
        // the recorded cap fold on the 140° pin).
        if (walkSoft && dualSupportStandCap > 0.01f)
        {
            bool firstStepGeoHold = fallCatchWalk
                && walkGeoSwingLift > 0.5f
                && stepPhaseDriver != null
                && stepPhaseDriver.FallCatchCycleCount == 0
                && liftGateBlend < 0.05f
                && (fallCatchPhaseCode == (int)StepPhaseDriver.FallCatchPhase.Plant
                    || fallCatchPhaseCode == (int)StepPhaseDriver.FallCatchPhase.Fall
                    || fallCatchPhaseCode == (int)StepPhaseDriver.FallCatchPhase.Reach
                    || fallCatchPhaseCode == (int)StepPhaseDriver.FallCatchPhase.Catch
                    || fallCatchPhaseCode == (int)StepPhaseDriver.FallCatchPhase.Recover);
            // WalkLift never DualSupport-Transfer. The cap is for that
            // clock. After lift-off walkDualSupport is false, so the
            // clamp slammed 1.0→0.40 (walklift_air 6.19) while the
            // swing was still reaching. Skip only AFTER a plant.
            // First Stance dirt keeps 0.40 (the walkFirstLiftLead open
            // is gone below). Later airborne Advance still skips so
            // the clamp cannot slam 1.0→0.40.
            bool walkLiftSkipCap = useWalkLift != 0 && !walkDualSupport
                && stepPhaseDriver != null && stepPhaseDriver.SwapCount > 0;
            if (!firstStepGeoHold && !walkLiftSkipCap)
            {
                float cap = dualSupportStandCap;
                float capBlend = Mathf.Max(liftBlend, liftGateBlend);
                // Dirt must keep dualSupportStandCap 0.40. Opening
                // capBlend on walkFirstLiftLead 0.03 (stanceComOff past
                // 3 cm) let standLegLevel hit 1.000 at t=5.70 with both
                // soles still down (burn_walk CSV). Swing knee sat at
                // 42° on the slab, CoM ran past both toes, then
                // UpdateState fallCoMOffset 0.30 at t=5.835 /
                // RelaxAllMuscles. t=6.300 is already Falling (torso
                // −24°). liftBlend / liftGateBlend stay 0 on stacked
                // dirt (8 s clock, DualLoad shut). Quiet / oneg never
                // set walkSoft.
                if (capBlend > 0.001f)
                    cap = Mathf.Lerp(dualSupportStandCap, 1f, capBlend);
                if (standLegLevel > cap)
                    standLegLevel = cap;
            }
        }

        // Fall from the stance foot. Dual-support is still two feet:
        // mid-pair comOffset. The liftBlend lerp to stanceComOff with
        // both soles down is burn_stag / p_capds survivedSeconds 6.300
        // (walkTime 5 + 1.30 s): standLegLevel→1, liftBlend→1, mid-feet
        // still ~0.22 m, stance-only past fallCoMOffset 0.30, then
        // RelaxAllMuscles. True air still reads the support plate.
        float fallOffset = comOffset;
        bool singleSupport = splitLegs && (
            (leftIsSwing && !leftGrounded && rightGrounded) ||
            (rightIsSwing && !rightGrounded && leftGrounded));
        if (singleSupport && splitLegs)
            fallOffset = stanceComOff;
        UpdateState(fallOffset);
        // The quiet-stance automaton treats |CoM offset| > 0.30 m as a
        // finished fall and zeroes every muscle. A walking catch lives
        // past that offset on purpose: the swing is still reaching, and
        // RelaxAllMuscles is exactly the 90° faceplant.
        // The exemption is a window, not a licence. Held unconditionally it
        // hides a finished fall: the pelvis lies on the slab, both hip
        // extensors saturate against the +45° stop holding the trunk at a
        // constant tilt, and the run reports "did not fall" for the whole
        // walk window.
        // CaptureWalk returns before this automaton. WalkLift may live
        // past 0.30 m only while it actually owns the swing or geo-lift
        // is shortening the trail. Dual-support no longer feeds
        // stance-only into UpdateState (that was burn_stag 6.300).
        // OwnsSwing / geoLift is the airborne catch window.
        // BodyCollapsed still spends it. Fall-catch keeps fc.ignoreFalling.
        bool walkLeaveCatch = walkSoft && !fallCatchWalk
            && (WalkLiftOwnsSwing() || walkGeoLiftU > 0.001f)
            && (stepPhaseDriver == null || !stepPhaseDriver.BodyCollapsed);
        // Standing Plant is the restore BeginWalk already raised
        // (TiltSettled 12° / 40 °/s + signed CoM-to-stance ≤
        // plantComLeadMax). walkSoft is off so moveX=1 cannot roll the
        // heap. Naming Falling here started the 1.5 s grace, then
        // RelaxAllMuscles a tipped / airborne / over-led start — that
        // is the collapse, not a stand-up. Quiet two-foot PD stands
        // the body; Tick starts the walker when that same gate opens.
        // Happy path never raises restore (tilt 2.1°, |lead| ~0).
        // TiltSettled / plantComLeadMax stay the happy-path numbers.
        if (standingPlant)
        {
            fallCatchIgnoreFallAge = 0f;
            if (currentState == BalanceState.Falling)
                currentState = BalanceState.Recovery;
        }
        else if (currentState == BalanceState.Falling
            && ((fallCatchWalk && fc.ignoreFalling) || walkLeaveCatch))
        {
            fallCatchIgnoreFallAge += Time.fixedDeltaTime;
            if (!FallCatchExemptionSpent())
                currentState = BalanceState.Recovery;
        }
        else if (currentState != BalanceState.Falling)
        {
            fallCatchIgnoreFallAge = 0f;
        }

        if (currentState == BalanceState.Falling)
        {
            RelaxAllMuscles();
            return;
        }

        float offsetN = comOffset / Mathf.Max(0.01f, comOffsetReference);
        float tiltSpeedN = angularVelocity / Mathf.Max(1f, tiltSpeedReference);
        float balanceSignal = Mathf.Clamp(
            comProportionalGain * offsetN + comDerivativeGain * tiltSpeedN,
            -maxBalanceSignal, maxBalanceSignal);

        float comVelX = bodyState != null ? bodyState.comVelocity.x : 0f;
        // The stance ankle of quiet stance keeps the raw sample above: its D
        // term is a damper on the real motion of the body, and a lag in a
        // damper is phase the loop cannot afford. The walk terms below read
        // the filtered one — they extrapolate the future, and there the limb
        // impulse of a heel strike is noise, not signal.
        float comVelWalkX = WalkComVelocityX;
        float walkMoveDir = 0f;
        float walkSpeedError = 0f;
        if (walkSoft && !running && intent != null && Mathf.Abs(intent.moveX) > 0.01f)
        {
            walkMoveDir = Mathf.Sign(intent.moveX);
            float forwardSpeed = comVelWalkX * walkMoveDir;
            walkSpeedError = Mathf.Max(0f, walkTargetSpeed - forwardSpeed);
        }
        // WALK-1 Stance lift. Clock and DualLoad gate were set with the
        // swing-friction fade above (same idle / unload). Swing hip is
        // silent until geoU ramps, then a fraction of swingHipFlex
        // reaches the sole forward. Stance hip still holds world-upright
        // plus walkStanceHipExtend; stance ankle tracks walkComLeadX.
        if (walkStanceGeoLift && walkGeoLiftU > stanceLoad)
            stanceLoad = walkGeoLiftU;
        // Push-off is a geometry window, not a clock. Plantarflexion at a foot
        // the CoM has already passed rolls the body forward over the toe; the
        // same torque at a foot still ahead of the CoM pushes the body back.
        // Both push gates used |stanceComOff|, so for half of every cycle the
        // "push-off" braked. The signed lead is also its own late-stance
        // envelope: it grows exactly as the body rides over the support, which
        // is what walkStanceProgress was trying to approximate (and cannot —
        // it divides a 0.5 s fall-catch phase by stanceDuration 6 s).
        float stanceLead = stanceComOff * (walkMoveDir != 0f ? walkMoveDir : 1f);
        float pushWindow = walkMoveDir != 0f
            ? Mathf.Clamp01(stanceLead / Mathf.Max(0.01f, walkPropulsionComWindow))
            : 1f;
        // walkStancePushOnLead above. Applied here because stanceLead is
        // not known at the airborne latch. Quiet stance, pushes, crouch
        // and cold one-leg never enter: they have walkMoveDir 0.
        // Legacy Stance used to wait for SwingWasAirborne, so walkStancePush
        // and walkStanceHipExtend never fired on a planted swing — the
        // 0.2 m shuffle. Same signed-lead window the two levers already
        // multiply by. Flag 0 restores the airborne-only gate.
        if (walkStancePushOnLead > 0.5f
            && walkMoveDir != 0f && stanceLead > 0.001f
            && (fallCatchWalk || walkGeoLiftU > 0.001f))
            pushAfterAir = true;
        float walkAutoLean = 0f;
        float walkAutoTorsoLean = 0f;
        if (walkMoveDir != 0f)
        {
            // burn_stag: walkSpeedError = walkTargetSpeed 1.0 from rest
            // saturates walkSpeedLeanMax 6° on the first Stance frame
            // (plus walkForwardTorsoLean 4° → torso −12° at t=5.15 with
            // both feet still down). That lean is the inverted-pendulum
            // x0 that UpdateState fallCoMOffset 0.30 names Falling at
            // t=6.300. Scale by the speed we already have so rest is
            // upright; v_des still gets the full 6°. Quiet / oneg never
            // set walkMoveDir.
            float leanMag = Mathf.Clamp(
                walkSpeedError * Mathf.Max(0f, walkSpeedLeanGain),
                0f,
                Mathf.Max(0f, walkSpeedLeanMax));
            float speedN = Mathf.Clamp01(
                Mathf.Max(0f, comVelWalkX * walkMoveDir)
                / Mathf.Max(0.05f, walkTargetSpeed));
            leanMag *= speedN;
            walkAutoLean = leanMag * walkMoveDir;
            walkAutoTorsoLean = walkAutoLean * Mathf.Max(0f, walkSpeedLeanTorsoScale);
        }
        float walkBasePelvisLean = 0f;
        float walkBaseTorsoLean = 0f;
        if (walkMoveDir != 0f)
        {
            walkBasePelvisLean = walkMoveDir * Mathf.Max(0f, walkForwardPelvisLean);
            walkBaseTorsoLean = walkMoveDir * Mathf.Max(0f, walkForwardTorsoLean);
        }
        if (fallCatchWalk && fc.suppressWalkLean)
        {
            walkAutoLean = 0f;
            walkAutoTorsoLean = 0f;
            walkBasePelvisLean = 0f;
            walkBaseTorsoLean = 0f;
        }
        // burn_stag: walkForwardTorsoLean 4° + walkForwardPelvisLean 2°
        // from rest is the inverted-pendulum x0 that UpdateState
        // fallCoMOffset 0.30 names Falling at t=6.300 (walk start +1.30 s).
        // Speed lean is already × v (0 at rest). The leftover pose is not.
        // Hold it for the whole first step (SwapCount 0), not only until
        // ComLeftSupport: that event is when WalkLift Unloads, and slamming
        // 4°+2° on the same frame is the catch dump. After the first plant
        // the pose is the gait lean. Clips still skip only while the CoM
        // is on the plate (FirstWalkRollOpen). No new switch. PD / signs
        // / lumbarP unchanged. Quiet / oneg never set walkMoveDir.
        else if (FirstWalkPoseLeanHold())
        {
            walkBasePelvisLean = 0f;
            walkBaseTorsoLean = 0f;
        }

        // Pelvis holds world vertical, not the angle to the thigh: else the
        // leg sway copies one-to-one into the pelvis and the lumbar uses the
        // whole ±20° travel. hipBalanceGain still leans the target toward CoM.
        // crouchHipFlex is not in the target — crouch sets pelvis tilt.
        // VEST-1: do not zero hipBalanceGain on the quiet path — oneleg_sym3
        // / pf23b / pb23b all dumped backward at 2.05 s (pelvis +86°) without it.
        float pelvisTarget = Mathf.Clamp(
            hipBaseAngle - hipBalanceGain * balanceSignal * fc.hipBalanceScale
                - crouchLevel * crouchPelvisTilt - leanLevel * leanPelvisTilt - walkAutoLean - walkBasePelvisLean
                - fc.pelvisLean,
            -60f, 60f);
        // March in place: pelvis stays world vertical. Do not chase CoM
        // with hipBalanceGain (minus = forward; march_ip rolled +X) and
        // do not command a plus lean (march_ph dumped −X, same bistable
        // well as CaptureWalk). Quiet one-leg keeps hipBaseAngle above.
        if (Marching())
        {
            marchPelvisBias = Mathf.MoveTowards(
                marchPelvisBias, 0f, Mathf.Max(0f, marchTiltRate) * Time.fixedDeltaTime);
            pelvisTarget = Mathf.Clamp(
                -crouchLevel * crouchPelvisTilt - leanLevel * leanPelvisTilt,
                -60f, 60f);
        }
        else
        {
            marchPelvisBias = Mathf.MoveTowards(
                marchPelvisBias, 0f, Mathf.Max(0f, marchTiltRate) * Time.fixedDeltaTime);
        }
        // Vault: pelvis toward vertical at weightBlend (before unload/scissor/push).
        if (!fallCatchWalk && walkSoft && walkStanceExtend > 0.001f && weightBlend > 0.001f)
            pelvisTarget = Mathf.Lerp(pelvisTarget, 0f, Mathf.Clamp01(walkStanceExtend * weightBlend));
        float kneeTarget = kneeBaseAngle + crouchLevel * crouchKneeFlex;
        // The same target without the Recovery term (walkStanceKneeRecoveryFlex
        // above). Clamped the same way, so a crouch that already sits on the
        // 115° stop reads the stop on both, and the two collapse to one number
        // whenever the reflex is not active — which is what keeps every
        // non-walk path bitwise.
        float kneeTargetNoRecovery = Mathf.Clamp(kneeTarget, 0f, 115f);
        // Recovery knee flex on dual-support split folds the chain
        // after transfer. On walkSingleSupport — same as one-leg.
        if (currentState == BalanceState.Recovery
            && !(splitLegs && leftGrounded && rightGrounded && !walkSingleSupport))
            kneeTarget += kneeRecoveryFlex;
        kneeTarget = Mathf.Clamp(kneeTarget, 0f, 115f);

        float velN = comVelX / Mathf.Max(0.05f, comVelocityReference);

        // Soft dual-support. Unload/knee move toward one-leg with weightBlend;
        // tilt does the same on the fall-catch walk (walkStandTiltAirRelease),
        // and stays on the bare floor on the legacy clocked walk.
        float dualSoft = 1f;
        if (walkSoft && splitLegs)
            dualSoft = Mathf.Clamp01(dualSupportSwingScale);
        float tiltSoft = dualSoft;
        // walkStandTiltAirRelease above. Gated on fallCatchWalk with a walk
        // direction: there liftGateBlend is the swing foot's own clearance
        // (walkAirReleaseAuthority), a measured quantity. On the legacy
        // !fallCatchWalk walk the same variable is still the 8 s Stance clock,
        // where a soft tilt is what stopped the fold after swap — so that
        // path, cold one-leg, quiet stance, the pushes and crouch stay bitwise.
        if (fallCatchWalk && walkMoveDir != 0f && walkStandTiltAirRelease > 0.001f)
        {
            tiltSoft = Mathf.Lerp(dualSoft, 1f,
                liftGateBlend * Mathf.Clamp01(walkStandTiltAirRelease));
        }
        float unloadSoft = Mathf.Lerp(dualSoft, 1f, liftGateBlend);
        float liftMult = Mathf.Lerp(1f, Mathf.Max(1f, walkSwingLiftScale), liftAct);
        float liftSoft = unloadSoft * liftMult;
        lastDualSoft = dualSoft;

        // Unload the swing by pelvis tilt:
        // (swing on the right). On right support tilt plants both feet.
        // March skips the +10°: left-only tilt is a CoM shift, and the
        // clock would send the body +X on every left lift (oneleg_perf_l).
        if (!Marching() && splitLegs && standLegPelvisTilt > 0.01f && standCmd < -0.5f)
        {
            pelvisTarget = Mathf.Clamp(
                pelvisTarget + standLegLevel * standLegPelvisTilt * tiltSoft,
                -60f, 60f);
        }
        else if (!Marching() && splitLegs && standLegPelvisTilt > 0.01f && standCmd > 0.5f)
        {
            // walkStandLegPelvisTiltMirror above: on the walk the left swing
            // reaches forward exactly like the right one, so it gets the same
            // assist with the same sign. Outside that path the former backward
            // fraction (default 0 = nothing) is untouched.
            float tiltMirror = fallCatchWalk && walkMoveDir != 0f
                ? Mathf.Clamp01(walkStandLegPelvisTiltMirror)
                : 0f;
            if (tiltMirror > 0.001f)
            {
                pelvisTarget = Mathf.Clamp(
                    pelvisTarget + standLegLevel * standLegPelvisTilt * tiltMirror * tiltSoft,
                    -60f, 60f);
            }
            else if (backPelvisTiltFraction > 0.001f)
            {
                pelvisTarget = Mathf.Clamp(
                    pelvisTarget - standLegLevel * standLegPelvisTilt * backPelvisTiltFraction * tiltSoft,
                    -60f, 60f);
            }
        }
        // walkPlantTiltFirst above. Cycle 0 has no DualLoadMinSpan, so
        // liftGateBlend stays 0 and the product is 1.28° of the 10° that
        // lifted oneg_l. Same plantMinDuration clock as lean / knee /
        // stance hip / friction / straighten / unload. Max so a
        // millimetre of DualLoad that opened tiltSoft is not added twice.
        // DrivePelvis stays (this is the target). Restore / surrender keep
        // the 1.28° (brake). Same plus on both walk supports as the mirror.
        // Comment 33: on cycle 0 this 10° is another pelvis bow while
        // the trail still pins (StanceLead < 0). Comment 48: plus is
        // backward world tilt (hipBaseAngle: minus = forward). oneg_l
        // used it to raise a FORWARD swing; here the swing is the
        // planted trailer, so the same plus dumps the chest −X. Forward
        // lean is already walkPlantLeanFirst 9°. Do not write the plus.
        if (false && walkPlantTiltFirst > 0.5f
            && fallCatchWalk
            && fallCatchPhaseCode == (int)StepPhaseDriver.FallCatchPhase.Plant
            && stepPhaseDriver != null
            && stepPhaseDriver.FallCatchCycleCount <= 0
            && !FirstStepTrailStillLoaded(stepPhaseDriver)
            && !stepPhaseDriver.StandingRestore
            && !stepPhaseDriver.StandingSurrender
            && stepPhaseDriver.PlantGateReady
            && splitLegs && standLegPelvisTilt > 0.01f
            && walkMoveDir != 0f
            && intent != null && Mathf.Abs(intent.moveX) > 0.01f)
        {
            float firstClock = Mathf.Clamp01(
                stepPhaseDriver.PhaseAge()
                / Mathf.Max(0.05f, stepPhaseDriver.plantMinDuration));
            float firstTilt = standLegPelvisTilt * firstClock;
            float alreadyTilt = standLegLevel * standLegPelvisTilt * tiltSoft;
            if (standCmd > 0.5f)
                alreadyTilt *= Mathf.Clamp01(walkStandLegPelvisTiltMirror);
            else if (standCmd > -0.5f)
                alreadyTilt = 0f;
            if (firstTilt > alreadyTilt + 0.01f
                && (standCmd < -0.5f
                    || (standCmd > 0.5f
                        && walkStandLegPelvisTiltMirror > 0.001f)))
            {
                pelvisTarget = Mathf.Clamp(
                    pelvisTarget + (firstTilt - alreadyTilt),
                    -60f, 60f);
            }
        }
        // walkPlantFallTiltHoldFirst above. Plant already tilted to
        // clock 1 (EnterFall requires plantAged). Fall's PhaseAge is 0,
        // DualLoadMinSpan never opens, liftGateBlend stays 0, so
        // alreadyTilt is 1.28° of the 10° that raised the swing hip —
        // the stance loop yanks toward vertical and drops that hip
        // (and fights the held 9° lean). Hold the endpoint. Catch /
        // later cycles keep DualLoad tiltSoft. Max so a millimetre of
        // DualLoad that opened tiltSoft is not added twice.
        // Comment 48: this hold is the same plus-backward 10° through
        // Fall/Reach, with no FirstStepTrailStillLoaded gate, so it
        // wrote the chest −X the moment EnterFall fired. Off with the
        // Plant plus.
        if (false && walkPlantFallTiltHoldFirst > 0.5f
            && walkPlantTiltFirst > 0.5f
            && fallCatchWalk
            && (fallCatchPhaseCode == (int)StepPhaseDriver.FallCatchPhase.Fall
                || fallCatchPhaseCode == (int)StepPhaseDriver.FallCatchPhase.Reach)
            && stepPhaseDriver != null
            && stepPhaseDriver.FallCatchCycleCount <= 0
            && !stepPhaseDriver.StandingRestore
            && !stepPhaseDriver.StandingSurrender
            && splitLegs && standLegPelvisTilt > 0.01f
            && walkMoveDir != 0f
            && intent != null && Mathf.Abs(intent.moveX) > 0.01f)
        {
            float heldTilt = standLegPelvisTilt;
            float alreadyTilt = standLegLevel * standLegPelvisTilt * tiltSoft;
            if (standCmd > 0.5f)
                alreadyTilt *= Mathf.Clamp01(walkStandLegPelvisTiltMirror);
            else if (standCmd > -0.5f)
                alreadyTilt = 0f;
            if (heldTilt > alreadyTilt + 0.01f
                && (standCmd < -0.5f
                    || (standCmd > 0.5f
                        && walkStandLegPelvisTiltMirror > 0.001f)))
            {
                pelvisTarget = Mathf.Clamp(
                    pelvisTarget + (heldTilt - alreadyTilt),
                    -60f, 60f);
            }
        }

        // CoM ahead turns the body forward.
        // Walk Stance: support from the stance foot — else mid holds CoM
        // between the feet, comLiftGate/liftBlend never open, the swing never lifts.
        // Transfer: mid (lerp by liftBlend), as in walk_mid — else fold on swap.
        float walkStanceProgress = 0f;
        if (walkStancePhase && stepPhaseDriver != null)
        {
            float phaseDur = Mathf.Max(0.1f, stepPhaseDriver.stanceDuration);
            walkStanceProgress = Mathf.Clamp01(stepPhaseDriver.PhaseAge() / phaseDur);
        }
        lastWalkStanceProgress = walkStanceProgress;
        // Shift the target ahead of support: the ankle stops pulling the body
        // back, and the human travels where moveX asks. A parked walkComLeadX
        // is a stationary setpoint — the body stands on it and travel dies
        // at that offset — so on WALK-1 Stance the magnitude follows geoU
        // (0 → walkComLeadX as the swing shortens). Quiet / oneg / pushes
        // never set walkMoveDir, so they stay bitwise.
        float comLead = 0f;
        if (walkSoft && intent != null && walkComLeadX > 0.0001f
            && walkStancePhase)
        {
            float leadMag = walkComLeadX * Mathf.Clamp(Mathf.Abs(intent.moveX), 0f, 1f);
            // walkGeoLiftU is the clock even after DualLoad / single-support
            // flips walkStanceGeoLift off. Using only that boolean dropped
            // comLead from walkComLeadX to PhaseAge/stanceDuration the
            // moment the swing shortened, and the stance ankle yanked CoM
            // back onto the old sole — travel died at one 6 cm park.
            // Transfer still keeps comLead 0 (mid-ankle).
            float ramp = walkGeoLiftU > 0.001f
                ? Mathf.Max(walkGeoLiftU, walkStanceProgress)
                : walkStanceProgress;
            // First step: ramp walkComLeadX in walkFirstLeadSeconds so the
            // named stance ankle can roll CoM to the toe. The 8 s Stance
            // clock left lead at millimetres. After the first swap the
            // former progress/geoU ramp stays.
            float firstLeadT = Mathf.Max(0f, walkFirstLeadSeconds);
            if (firstLeadT > 0.0001f
                && stepPhaseDriver != null
                && stepPhaseDriver.SwapCount <= 0)
            {
                ramp = Mathf.Max(
                    ramp,
                    Mathf.Clamp01(stepPhaseDriver.PhaseAge() / firstLeadT));
            }
            // After split the error is stanceComOff: target the STANCE
            // toe plus walkComLeadX, not mid-support of both soles. The
            // pair polygon grows when CoM sits on the named plate, so
            // a mid-edge target never asks the ankle to leave that plate.
            float halfSupport = splitLegs
                ? StanceToeReach()
                : (comCalculator != null
                    ? comCalculator.GetSupportWidth() * 0.5f
                    : 0f);
            comLead = (halfSupport + leadMag) * ramp * Mathf.Sign(intent.moveX);
        }
        float walkLiftLead = WalkLiftComLeadNow();
        if (Mathf.Abs(walkLiftLead) > 0.0001f)
            comLead = walkLiftLead;
        // WALK-2 Fall: CoP ahead of CoM (positive comLead) so the body
        // topples forward on a loaded ankle. Catch/Recover: CoP behind
        // CoM by catchComLead (≈0.10 m), so the landing ankle saturates
        // against the rotation before the trunk reaches 90°.
        if (fallCatchWalk && Mathf.Abs(fc.comLead) > 0.0001f)
        {
            float leadDir = intent != null && Mathf.Abs(intent.moveX) > 0.01f
                ? Mathf.Sign(intent.moveX)
                : 1f;
            float phaseLead = fc.comLead;
            // Roll the Fall setpoint across the sole instead of parking it at
            // fallComLead. Later-cycle Catch/Recover put the target BEHIND
            // the CoM on purpose (brake); do not roll those. Cycle-0 Catch
            // is still the initiation fall (trail down, no RecoverBrake).
            if (walkFallComRollToe > 0.001f && walkMoveDir != 0f && splitLegs
                && (fallCatchPhaseCode == (int)StepPhaseDriver.FallCatchPhase.Fall
                    || (fallCatchPhaseCode == (int)StepPhaseDriver.FallCatchPhase.Catch
                        && stepPhaseDriver.FallCatchCycleCount == 0
                        && stepPhaseDriver.SwingFootIsGrounded)))
            {
                float rollEnd = StanceToeReach() * Mathf.Clamp01(walkFallComRollToe);
                if (rollEnd > fallComLead)
                {
                    // Ramped on the clock every other Fall target already uses:
                    // a step in the ankle setpoint is a step in the ankle
                    // torque, and that shape is what throws the pelvis back.
                    float rollT = Mathf.Clamp01(
                        stepPhaseDriver.PhaseAge()
                        / Mathf.Max(0.02f, stepPhaseDriver.fallKneeRampSeconds));
                    // Later cycles: fc.comLead already carries RecoverBlend,
                    // which collapses to zero on a trunk that is tipping —
                    // do not roll further then (positive feedback on a
                    // body already over). Cycle 0: that same envelope is
                    // 0 at ~21° of torso while CoM is still ~6.5 cm inside
                    // the sole. The 23.5° lean is then a lunge the ankle
                    // holds (setpoint behind CoM → plantarflex). Keep the
                    // target AHEAD of CoM until the toe. Past the toe the
                    // ankle has no moment arm (balance-actuators.mdc) and
                    // Catch owns the plant. Do not restore a backward fall:
                    // a trunk going the other way keeps the later-cycle
                    // envelope (fc.comLead already * blendBack).
                    float fallEnvelope = Mathf.Clamp01(
                        fc.comLead / Mathf.Max(0.0001f, fallComLead));
                    float want = Mathf.Lerp(fallComLead, rollEnd, rollT)
                        * fallEnvelope;
                    if (stepPhaseDriver.FallCatchCycleCount == 0
                        && fallEnvelope > 0.0001f)
                    {
                        float leadNow = stepPhaseDriver.StanceLead;
                        // Only while CoM is still behind the toe. A target
                        // behind CoM here is the plantarflex that holds the
                        // 23.5° lunge. Do not scale by remaining sole: that
                        // put the setpoint back behind CoM.
                        if (leadNow < rollEnd)
                        {
                            want = Mathf.Max(
                                want,
                                Mathf.Min(rollEnd, leadNow + 0.02f));
                        }
                    }
                    phaseLead = Mathf.Max(phaseLead, want);
                }
            }
            comLead += phaseLead * leadDir;
        }
        float comRef = Mathf.Max(0.01f, comOffsetReference);
        float ankleOffsetN = (comOffset - comLead) / comRef;
        float xcomShift = 0f;
        float xcomW = Mathf.Clamp01(walkXCoMWeight);
        if (walkSoft && xcomW > 0.0001f)
        {
            float h = Mathf.Max(0.1f, walkXCoMHeight);
            float omega0 = Mathf.Sqrt(9.81f / h);
            xcomShift = comVelWalkX / Mathf.Max(0.1f, omega0);
        }
        float comOffsetXCoM = comOffset + xcomShift;
        if (splitLegs && bodyState != null && comCalculator != null)
        {
            float stanceN = (stanceComOff - comLead) / comRef;
            float stanceXCoMN = ((stanceComOff + xcomShift) - comLead) / comRef;
            if (walkDualSupport)
            {
                float baseN = walkStancePhase
                    ? stanceN
                    : Mathf.Lerp((comOffset - comLead) / comRef, stanceN, liftBlend);
                float baseXCoMN = walkStancePhase
                    ? stanceXCoMN
                    : Mathf.Lerp((comOffsetXCoM - comLead) / comRef, stanceXCoMN, liftBlend);
                ankleOffsetN = Mathf.Lerp(baseN, baseXCoMN, xcomW);
            }
            else if (Marching() && leftGrounded && rightGrounded)
            {
                float clock = intent != null ? intent.standLeg : 0f;
                if (Mathf.Abs(clock) > 0.5f)
                {
                    // Clocked lift still dual: midpoint CoM keeps the swing
                    // loaded (first lift of march_air never peeled). Pull
                    // onto the support so the swing can unload.
                    ankleOffsetN = stanceN;
                }
                else
                {
                    // Plant: stance-only would yank CoM onto the old
                    // support and walk +X into the next lift.
                    ankleOffsetN = (comOffset - comLead) / comRef;
                }
            }
            else if (Marching() && leftGrounded != rightGrounded
                && marchAnkleRefScale < 0.999f)
            {
                // Default 1 = march_hold ankle. march_mid at 0.5 dumped −X.
                ankleOffsetN = stanceComOff
                    / Mathf.Max(0.02f, comRef * Mathf.Max(0.2f, marchAnkleRefScale));
            }
            else
                ankleOffsetN = Mathf.Lerp(stanceN, stanceXCoMN, xcomW);
        }
        else if (xcomW > 0.0001f)
            ankleOffsetN = Mathf.Lerp(ankleOffsetN, (comOffsetXCoM - comLead) / comRef, xcomW);
        // Catch plant: the restoring moment is about the landing foot, not
        // the old stance. While the swing is still in the air the error
        // stays on the stance ankle (it already started arresting via the
        // negative comLead above).
        if (fallCatchWalk && fc.catchAnkleOnSwing && splitLegs
            && bodyState != null && comCalculator != null)
        {
            bool swingDown = leftIsSwing ? leftGrounded : rightGrounded;
            if (heelAirPose)
                swingDown = leftIsSwing ? leftHeel : rightHeel;
            if (swingDown)
            {
                Vector2 comNow = comCalculator.GetCenterOfMass();
                float swingX = leftIsSwing
                    ? bodyState.leftFootPosition.x
                    : bodyState.rightFootPosition.x;
                float catchOff = comNow.x - swingX;
                float catchN = (catchOff - comLead) / comRef;
                float catchXCoMN = ((catchOff + xcomShift) - comLead) / comRef;
                ankleOffsetN = Mathf.Lerp(catchN, catchXCoMN, xcomW);
            }
        }
        float velNAnkle = velN;
        ankleOffsetN = CompressWalkAnkleDorsiError(
            ankleOffsetN, velN, comRef, walkMoveDir,
            fallCatchWalk, walkSoft, splitLegs, out velNAnkle);
        float ankleBalance = Mathf.Clamp(
            ankleComP * ankleOffsetN + ankleComD * velNAnkle,
            -1f, 1f);
        if (fallCatchWalk)
            ankleBalance *= fc.ankleScale;
        ankleBalance = CapWalkAnkleForwardDrive(
            ankleBalance, walkMoveDir, fallCatchWalk, walkSoft, splitLegs);

        if (!splitLegs)
        {
            swingHipLatched = false;
            swingAirHoldRemain = 0f;
            swingAirStreak = 0f;
        }
        else if ((leftIsSwing && !leftGrounded) || (rightIsSwing && !rightGrounded))
        {
            swingAirStreak += Time.fixedDeltaTime;
            // 0 = hipair: latch from the first frame. Else — after the streak.
            float needAir = walkSwingLatchAir > 0.001f ? walkSwingLatchAir : 0f;
            if (swingAirStreak >= needAir - 1e-6f)
            {
                if (!swingHipLatched)
                {
                    HingeJoint2D swingJoint = leftIsSwing ? leftHipJoint : rightHipJoint;
                    swingHipAngleAtLatch = swingJoint != null ? swingJoint.jointAngle
                        : -(Marching() ? marchHipFlex : swingHipFlex);
                }
                swingHipLatched = true;
                if (walkSwingAirHold > 0.001f)
                    swingAirHoldRemain = walkSwingAirHold;
            }
        }
        else
        {
            swingAirStreak = 0f;
            if (walkSwingUnlatchPlant > 0.5f && swingHipLatched
                && ((leftIsSwing && leftGrounded) || (rightIsSwing && rightGrounded)))
            {
                swingHipLatched = false;
                swingAirHoldRemain = 0f;
            }
            else if (swingAirHoldRemain > 0f)
                swingAirHoldRemain = Mathf.Max(0f, swingAirHoldRemain - Time.fixedDeltaTime);
        }

        // −swingHipFlex pose: hipair = air only; heelPlant = until
        // the heel plants (the toe does not drop flex). airHold is a timer, rejected.
        bool heelPlant = walkSwingHeelPlant > 0.5f;
        bool airHold = swingAirHoldRemain > 0.001f;
        // Scissor breaks the latch loop; dose flex by weightBlend once CoM is on support.
        bool scissor = !fallCatchWalk && walkSoft && splitLegs && walkSwingScissorLevel > 0.001f
            && standLegLevel >= walkSwingScissorLevel;
        float scissorAmt = scissor && comLiftGate >= 1f
            ? Mathf.Clamp01(walkSwingScissorFlex * liftGateBlend * liftGateBlend)
            : 0f;
        bool scissorPose = scissorAmt > 0.001f;
        bool leftAir = swingHipLatched && (heelPlant ? !leftHeel : (!leftGrounded || airHold));
        bool rightAir = swingHipLatched && (heelPlant ? !rightHeel : (!rightGrounded || airHold));
        bool leftSwingHip = leftIsSwing && swingHipFlex > 0.5f && (leftAir || scissorPose);
        bool rightSwingHip = rightIsSwing && swingHipFlex > 0.5f && (rightAir || scissorPose);
        // Unload-shorten-clear owns the swing from load+timer, not from
        // airborne. The scissor is the closed loop this replaces.
        if (WalkLiftOwnsSwing())
        {
            scissor = false;
            scissorAmt = 0f;
            scissorPose = false;
            leftSwingHip = leftIsSwing;
            rightSwingHip = rightIsSwing;
        }
        // March air pose is the clock, not a scrape. leftAir needs latch,
        // so a 90° hip never starts if extras slam the first millimetre
        // of air (march_ank: bothFeet 0.97, knee mean 0). Force the swing
        // hip once THAT sole is up. Use intent.standLeg, not heldStandCmd:
        // plant zeros the clock while the level still splits.
        // CoM extras wait on marchComHold.
        float marchClock = intent != null ? intent.standLeg : 0f;
        if (Marching() && Mathf.Abs(marchClock) > 0.5f)
        {
            if (leftIsSwing && !leftGrounded)
                leftSwingHip = true;
            if (rightIsSwing && !rightGrounded)
                rightSwingHip = true;
        }
        marchComHold = Marching()
            && Mathf.Abs(marchClock) > 0.5f
            && swingAirStreak >= Mathf.Max(0f, marchComDelay)
            && ((leftIsSwing && !leftGrounded && rightGrounded)
                || (rightIsSwing && !rightGrounded && leftGrounded));
        // march_air CSV, second lift: air 9.96, hip −42 / knee 57 at 10.09,
        // actAnkleR = 1.0 (vault) by 10.17, delay-0.25 pin at 10.21 already
        // past the toe. Cap after 0.08 s of air; toe/heel only once the
        // 90° pose is on. Quiet one-leg never sets this.
        float swingHipNow = 0f;
        float swingKneeNow = 0f;
        if (leftIsSwing)
        {
            if (leftHipJoint != null) swingHipNow = leftHipJoint.jointAngle;
            if (leftKneeJoint != null) swingKneeNow = leftKneeJoint.jointAngle;
        }
        else if (rightIsSwing)
        {
            if (rightHipJoint != null) swingHipNow = rightHipJoint.jointAngle;
            if (rightKneeJoint != null) swingKneeNow = rightKneeJoint.jointAngle;
        }
        marchToeHold = marchComHold
            && swingHipNow <= -Mathf.Max(0f, marchPoseHip)
            && swingKneeNow >= Mathf.Max(0f, marchPoseKnee);
        // scrapeLiftAirHold: airLiftHold already wrote ThighHold on the
        // Catch scrape. forceSwingFlex then took −swingHipFlex the first
        // airborne frame — the hip pair of scrapeExtend AirAge's 70°→35°.
        // Keep ThighHold through Catch air. scrapeLiftAirEnterHold then
        // keeps that ThighHold on Catch→Reach (CatchScrapedThisPhase is
        // cleared on bounce). scrapeLiftAirCatchHold then keeps it on
        // Reach→Catch (ReachResumedFromCatchScrapeAir is cleared).
        // scrapeLiftAirCatchFold then keeps the Reach knee fold on that
        // same airborne Catch (no 70°→35°). scrapeLiftAirEnterFold then
        // keeps that fold on Catch→Reach (Last is the Catch-enter freeze,
        // not the fold override). True airborne Catch (never
        // scraped) still reaches. Quiet when airLiftHold is 0.
        bool catchScrapeLift = fallCatchWalk
            && stepPhaseDriver.fallSwingCatchAirLiftHold > 0.5f
            && stepPhaseDriver.fallSwingCatchScrapeLiftAirHold > 0.5f
            && !stepPhaseDriver.SwingFootIsPlanted
            && (fc.thighHold > 0.001f || fc.hipLift > 0.001f)
            && ((fallCatchPhaseCode == (int)StepPhaseDriver.FallCatchPhase.Catch
                    && (stepPhaseDriver.CatchScrapedThisPhase
                        || (stepPhaseDriver.CatchResumedFromReachScrapeAir
                            && stepPhaseDriver.fallSwingCatchScrapeLiftAirCatchHold > 0.5f)))
                || (fallCatchPhaseCode == (int)StepPhaseDriver.FallCatchPhase.Reach
                    && stepPhaseDriver.ReachResumedFromCatchScrapeAir
                    && stepPhaseDriver.fallSwingCatchScrapeLiftAirEnterHold > 0.5f));
        // Fall→Reach first air: hipair latches on the same frame and
        // takes −swingHipFlex (sci_a). scrapeLiftAirHold only skips the
        // forceSwingFlex add; this clears the latch pose so ThighHold
        // continues. Catch→Reach still reached until
        // fallSwingCatchAirEnterHold. Quiet when 0.
        bool reachAirHold = fallCatchWalk
            && stepPhaseDriver.fallSwingReachAirHold > 0.5f
            && fallCatchPhaseCode == (int)StepPhaseDriver.FallCatchPhase.Reach
            && !stepPhaseDriver.SwingFootIsGrounded
            && (fc.thighHold > 0.001f || fc.hipLift > 0.001f)
            && !stepPhaseDriver.ReachResumedFromCatch;
        // 23:39 snap-hold can now make Fall's own first millimetre of
        // air. hipair latches on that same frame (walkSwingLatchAir 0)
        // and takes −swingHipFlex — Reach's hold never ran, Fall→Reach
        // is gated on air that this yank replants. Same keep as Reach.
        // Quiet when 0: former latch jump. forceSwingFlex stays off in
        // Fall, so skip-add is a no-op; the clear below is the keep.
        bool fallAirHold = fallCatchWalk
            && stepPhaseDriver.fallSwingFallAirHold > 0.5f
            && fallCatchPhaseCode == (int)StepPhaseDriver.FallCatchPhase.Fall
            && !stepPhaseDriver.SwingFootIsGrounded
            && (fc.thighHold > 0.001f || fc.hipLift > 0.001f);
        // 00:41 can now keep 32° on Catch→Reach scrape. Reach→Catch
        // timeout air still takes hipair −swingHipFlex: Catch-air already
        // writes ThighHold terms (fallSwingCatchAirLiftHold), but
        // keepThighHoldAir is scrape-only, so ApplyFallCatchSwingThighHold
        // never runs in never-scraped air (needs grounded || keep).
        // Same keep as Fall / Reach. Planted Catch stays off. Quiet
        // when 0: former latch jump.
        bool catchAirHold = fallCatchWalk
            && stepPhaseDriver.fallSwingCatchAirHold > 0.5f
            && fallCatchPhaseCode == (int)StepPhaseDriver.FallCatchPhase.Catch
            && !stepPhaseDriver.SwingFootIsGrounded
            && (fc.thighHold > 0.001f || fc.hipLift > 0.001f);
        // 00:42 can now keep ThighHold on that Catch-air. Catch timeout
        // without a plant bounces to Reach (catchMax, !swingDown).
        // reachAirHold excludes ReachResumedFromCatch, so the bounce
        // takes hipair −swingHipFlex: the 32° Catch-air thigh yanks to
        // 40°, the pelvis eats the reaction, the sole replants.
        // scrape Catch→Reach already has scrapeLiftAirEnterHold. Same
        // keep: clear the latch pose and continue ThighHold. Quiet when
        // catchAirHold is 0 (Catch already jumped).
        bool catchAirEnterHold = fallCatchWalk
            && stepPhaseDriver.fallSwingCatchAirEnterHold > 0.5f
            && stepPhaseDriver.fallSwingCatchAirHold > 0.5f
            && fallCatchPhaseCode == (int)StepPhaseDriver.FallCatchPhase.Reach
            && stepPhaseDriver.ReachResumedFromCatch
            && !stepPhaseDriver.ReachResumedFromCatchScrapeAir
            && !stepPhaseDriver.SwingFootIsGrounded
            && (fc.thighHold > 0.001f || fc.hipLift > 0.001f);
        // 00:53 can now keep ThighHold on Catch→Reach timeout air.
        // Plant 32° fold can now make Plant's own first millimetre of
        // air. hipair latches on that same frame (walkSwingLatchAir 0)
        // and takes −swingHipFlex — Fall's hold never ran, Plant→Fall
        // is gated on air that this yank replants. Same keep as Fall:
        // clear the latch pose and continue ThighHold. forceSwingFlex
        // stays off in Plant, so skip-add is a no-op; the clear below
        // is the keep. Quiet when 0: former latch jump.
        bool plantAirHold = fallCatchWalk
            && stepPhaseDriver.fallSwingPlantAirHold > 0.5f
            && fallCatchPhaseCode == (int)StepPhaseDriver.FallCatchPhase.Plant
            && !stepPhaseDriver.SwingFootIsGrounded
            && (fc.thighHold > 0.001f || fc.hipLift > 0.001f);
        bool keepThighHoldAir = catchScrapeLift || reachAirHold || fallAirHold
            || catchAirHold || catchAirEnterHold || plantAirHold;
        // Airborne Reach may carry the foot forward instead of only holding the
        // thigh upright, as a ramp off the pose it already has. The stance foot
        // has to be down: with both feet in the air the hip flexor has nothing
        // to react against but the pelvis, and that is exactly the shove this
        // ramp exists to avoid. Catch keeps its own behaviour — this is the
        // flight window, not the landing one.
        bool reachAirStanceDown = leftIsSwing ? rightGrounded : leftGrounded;
        bool reachAirSwing = reachAirHold && splitLegs && reachAirStanceDown
            && swingHipLatched && swingHipFlex > 0.5f
            && walkReachAirSwingSeconds > 0.001f;
        if (fallCatchWalk && fc.forceSwingFlex)
        {
            bool leftReach = leftIsSwing && swingHipFlex > 0.5f;
            bool rightReach = rightIsSwing && swingHipFlex > 0.5f;
            // Airborne hip pose only. −swingHipFlex on the ground is the
            // third-law shove (sci_a, −1.11 m). The grounded lift is the
            // stance-knee vault, not a hip target.
            if (leftReach && leftGrounded) leftReach = false;
            if (rightReach && rightGrounded) rightReach = false;
            if (keepThighHoldAir)
            {
                leftReach = false;
                rightReach = false;
            }
            if (leftReach)
                leftSwingHip = true;
            if (rightReach)
                rightSwingHip = true;
        }
        // hipair already latched on this air frame. Clearing the pose is
        // the keep: skip-add alone (catchScrapeLift) leaves −swingHipFlex.
        // Fall first air has no forceSwingFlex add, so the clear is the
        // whole keep (fallSwingFallAirHold). Catch timeout air has
        // forceSwingFlex on, so skip-add AND the clear (fallSwingCatchAirHold).
        // Catch→Reach timeout air has the same add (reachAirHold excludes
        // ReachResumedFromCatch), so skip-add AND the clear
        // (fallSwingCatchAirEnterHold). Plant first air has no
        // forceSwingFlex add, so the clear is the whole keep
        // (fallSwingPlantAirHold). That Plant-air leftover into Reach
        // still ramped reachAirSwing to −swingHipFlex; fallSwingReachAirSwingCeil
        // clamps the ramp to coupleCapCeilingDeg.
        // Catch→Reach scrape-air (ReachResumedFromCatchScrapeAir) is the
        // leftover: catchAirEnterHold excludes that latch, reachAirHold
        // excludes ReachResumedFromCatch, catchAirHold is Catch-only, so
        // skip-add was the whole keep. leftAir is already true on that
        // bounce, so leftSwingHip starts on and takes Min(angleAtLatch,
        // −swingHipFlex). A 32° leftover thigh yanks to 40° (sci_a),
        // the pelvis eats the reaction, the sole replants. Clear here
        // so ThighHold at coupleCapCeilingDeg actually runs. Quiet when
        // scrapeLiftAirEnterHold is 0 (catchScrapeLift off on this bounce).
        // Recover can still take that pose: no keep writes ThighHold
        // there (landing hip), and a millimetre of lost toe (sticky latch)
        // starts leftSwingHip on. fallSwingHipairCeil clamps that target
        // to coupleCapCeilingDeg; ThighHold / reachAirSwing stay as they
        // are. Flag 0 is the former −swingHipFlex.
        // fallSwingRecoverHipairClear then drops the pose itself on
        // Recover: planted Catch already gives the trunk to the landing
        // hip, and a 32° Recover flexor still spends the 0.05 s window
        // fighting that loop. Not a ThighHold keep. Flag 0 is the
        // former clamp. Recover-air still SPD the latched ankle to
        // joint=0 (airAnkle 0, world-flat off); fallSwingRecoverAirAnkleClear
        // quiets that pair. Plant-air is the same joint=0 path on the
        // trailing fold; fallSwingPlantAirAnkleClear quiets it.
        bool recoverHipairClear = fallCatchWalk
            && stepPhaseDriver.fallSwingRecoverHipairClear > 0.5f
            && fallCatchPhaseCode == (int)StepPhaseDriver.FallCatchPhase.Recover;
        if (reachAirHold || fallAirHold || catchAirHold || catchAirEnterHold
            || plantAirHold || catchScrapeLift || recoverHipairClear
            || (fallCatchWalk
                && stepPhaseDriver != null
                && stepPhaseDriver.FallCatchCycleCount == 0
                && firstStepCatchPlantLatched
                && (fallCatchPhaseCode == (int)StepPhaseDriver.FallCatchPhase.Fall
                    || fallCatchPhaseCode == (int)StepPhaseDriver.FallCatchPhase.Reach
                    || fallCatchPhaseCode == (int)StepPhaseDriver.FallCatchPhase.Catch
                    || fallCatchPhaseCode == (int)StepPhaseDriver.FallCatchPhase.Recover)))
        {
            if (leftIsSwing) leftSwingHip = false;
            if (rightIsSwing) rightSwingHip = false;
        }
        float swingUnload = 0f;
        if (splitLegs)
        {
            float unloadBase = swingHipUnloadBias * liftSoft;
            // Peel/unload after vault clearance gate, not on a closed chain.
            float earlyAct = Mathf.Max(liftAct, liftGateBlend);
            if (walkSoft && earlyAct > 0.001f && walkLiftUnloadBias > 0.001f)
                unloadBase += earlyAct * walkLiftUnloadBias;
            swingUnload = standLegLevel * unloadBase;
            // walkPlantSwingUnloadFirst: DualLoad frontShare ≥ 0.5 on the
            // 3 cm lunge was rest, so a plantMinDuration clock here wrote
            // 0.32 on a still-loaded µ=1.2 hip (sci_a). Only boost after
            // CoM is on the leading foot (trail N≈0). Restore / surrender
            // keep DualLoad 0.
            if (walkPlantSwingUnloadFirst > 0.5f
                && !FirstStepTrailStillLoaded(stepPhaseDriver)
                && fallCatchWalk
                && (fallCatchPhaseCode == (int)StepPhaseDriver.FallCatchPhase.Plant
                    || fallCatchPhaseCode == (int)StepPhaseDriver.FallCatchPhase.Fall
                    || fallCatchPhaseCode == (int)StepPhaseDriver.FallCatchPhase.Catch)
                && stepPhaseDriver != null
                && stepPhaseDriver.FallCatchCycleCount <= 0
                && !stepPhaseDriver.StandingRestore
                && !stepPhaseDriver.StandingSurrender
                && (fallCatchPhaseCode != (int)StepPhaseDriver.FallCatchPhase.Plant
                    || stepPhaseDriver.PlantGateReady)
                && intent != null && Mathf.Abs(intent.moveX) > 0.01f)
            {
                float firstClock = fallCatchPhaseCode
                    == (int)StepPhaseDriver.FallCatchPhase.Plant
                    ? Mathf.Clamp01(
                        stepPhaseDriver.PhaseAge()
                        / Mathf.Max(0.05f, stepPhaseDriver.plantMinDuration))
                    : 1f;
                swingUnload = Mathf.Max(
                    swingUnload,
                    swingHipUnloadBias * firstClock);
            }
        }
        float leftSwingUnload = 0f;
        if (leftIsSwing && !leftSwingHip)
        {
            // Airborne Recover is the landing leg with a lost toe, not a
            // swing to unload. Planted Recover already took this bias
            // (hipair was off with the heel down).
            if (!(recoverHipairClear && !leftGrounded))
            {
                leftSwingUnload = swingUnload;
                if (standCmd > 0.5f)
                    leftSwingUnload *= backSwingHipUnloadScale;
            }
        }
        float rightSwingUnload = 0f;
        if (rightIsSwing && !rightSwingHip
            && !(recoverHipairClear && !rightGrounded))
            rightSwingUnload = swingUnload * forwardSwingHipUnloadScale;

        // Reach lifts with the knee. Pelvis-upright on a planted swing is an
        // extensor against that fold (swingFoot 0.96). The stance hip still
        // owns pelvis orientation. Two-sided world-vertical SPD on the
        // grounded swing hip torqued the pelvis against that loop (20:25).
        // Silence is not enough either: a free hip + 140° knee lets the
        // thigh collapse backward, which is a squat on the swing, not a
        // lift. Flexor-only past vertical (ApplyFallCatchSwingThighHold)
        // is a stop, not a pose; hipLift is a flexor bias so the knee rises
        // instead of the foot sliding back — not −swingHipFlex (sci_a).
        // Fall lift-off may fade at 16° (fallSwingFallHipLiftCap); Reach
        // may use the same 16° (fallSwingReachHipLiftCap); Catch scrape
        // keeps 8°. Fall may also write 0.5 bias
        // (fallSwingFallHipLift) so that 16° room is actually walked;
        // Reach may write the same 0.5 (fallSwingReachHipLift); Catch
        // scrape keeps 0.25. ankleLift unweights that same foot
        // (fade toe grip / slight toe-up); Fall may use 0.5
        // (fallSwingFallAnkleLift), Reach may write the same 0.5
        // (fallSwingReachAnkleLift), Catch air may write the same 0.5
        // (fallSwingCatchAirAnkleLift), Catch scrape may write the same
        // 0.5 (fallSwingCatchScrapeAnkleLift), Catch→Reach scrape may
        // write the same 0.5 (fallSwingCatchReachScrapeAnkleLift),
        // so µ=0.4 does not turn the shortening into a backward slide.
        // Height still comes from the stance vault. Planted Catch/Recover
        // keep the pelvis loop — the landing hip must take the trunk.
        // Air/scrape Catch keeps thighHold until heelLoaded
        // (fallSwingCatchAirLiftHold). scrapeLiftAirHold then keeps that
        // ThighHold after lift-off so forceSwingFlex does not take
        // −swingHipFlex on Catch scrape then air. scrapeLiftAirEnterHold
        // then keeps it on Catch→Reach (CatchScrapedThisPhase is cleared).
        // scrapeLiftAirCatchHold then keeps it on Reach→Catch
        // (ReachResumedFromCatchScrapeAir is cleared). scrapeLiftAirCatchFold
        // then keeps the Reach knee fold on that Catch (no 70°→35°).
        // scrapeLiftAirEnterFold then keeps that fold on Catch→Reach.
        // fallSwingReachAirHold then keeps ThighHold on Fall→Reach first
        // air: hipair latches the same frame and would yank 16°→40°.
        // fallSwingFallAirHold then keeps it on Fall's own first air
        // (23:39 can now produce that millimetre; Reach's hold never ran).
        // fallSwingCatchAirHold then keeps it on Reach→Catch timeout air
        // (00:41 can now hold 32° there; scrape-then-air already has
        // scrapeLiftAirHold, but never-scraped Catch still took hipair).
        // fallSwingCatchAirEnterHold then keeps it on that Catch→Reach
        // bounce (reachAirHold excludes ReachResumedFromCatch; scrape
        // Catch→Reach already has scrapeLiftAirEnterHold).
        // fallSwingPlantAirHold then keeps it on Plant's own first air
        // (00:53 can now produce that millimetre; Fall's hold never ran).
        // That 16° Fall cap still had room=0 at a 16° Plant leftover
        // thigh; fallSwingFallHipLiftCapWide keeps 24° in Fall
        // (Catch scrape already 24°). That 16° Reach cap still had
        // room=0 once Fall walked into 24°; fallSwingReachHipLiftCapWide
        // keeps 24° in Reach. That 16° Catch-air cap still had room=0
        // once Reach walked into 24°; fallSwingCatchAirHipLiftCapWide
        // keeps 24° in Catch-air. Plant may then use 24°
        // (fallSwingPlantHipLiftCapWide).
        // That 24° Plant leftover still had room=0 at Fall's 24° cap;
        // fallSwingFallHipLiftCapCeil keeps 32° in Fall. That 24° Reach
        // cap still had room=0 once Fall walked into 32°;
        // fallSwingReachHipLiftCapCeil keeps 32° in Reach. That 24°
        // Catch-air cap still had room=0 once Reach walked into 32°;
        // fallSwingCatchAirHipLiftCapCeil keeps 32° in Catch-air.
        // That 24° Plant leftover still had room=0 at Plant's 24° cap;
        // fallSwingPlantHipLiftCapCeil keeps 32° in Plant.
        // That 24° Catch scrape still had room=0 once Catch-air walked
        // into 32°; fallSwingCatchScrapeHipLiftCapCeil keeps 32° in
        // Catch scrape.
        // fallSwingReachHipLiftCap then keeps that 16° fade-out so the
        // Fall thigh is not immediately past an 8° cap (liftSignal 0).
        // fallSwingReachHipLift then writes 0.5 in Reach so that 16°
        // room is actually walked (0.25 is −0.20 at vertical).
        // fallSwingReachAnkleLift then writes 0.5 in Reach so leftover
        // plantarflex after FallAnklePush cancels to 0 (0.25 still
        // leaves 0.5 of a full push on µ=0.4).
        // fallSwingReachHipLiftCapHold then floors that fade at 0.5 so
        // the 16° cap is a hold against gravity (P to the cap is 0 at
        // the target, liftSignal 0, thigh drops, foot replants). Catch
        // scrape may floor the same way (fallSwingCatchScrapeHipLiftCapHold).
        // fallSwingFallHipLiftCapHold then floors the same fade in Fall
        // (Reach floor is Reach-only; Fall at the cap still had
        // liftSignal 0, so the thigh dropped before Fall→Reach air).
        // Height still comes from the stance vault: leftover→0 used to
        // share the fold clock, so cycle-0 quiet knee (~2 mm) lost the
        // height fight until late in the ramp. fallSwingFallVaultLead
        // finishes that strut at 2× fold clock; Reach continues it.
        // hipLift / ankleLift still shared the 1× fold ramp after that;
        // fallSwingFallLiftLead finishes those two at 2× so the foot
        // rises before 140° fold. Fold still shared that 1× ramp
        // (70° at lift=1); fallSwingFallFoldLead finishes 140° with
        // that lift. That 16° Fall/Reach thigh still met an 8° Catch
        // cap on timeout air (liftSignal 0, foot replants);
        // fallSwingCatchAirHipLiftCap keeps the same 16° in Catch air.
        // That 16° Catch air still had
        // liftSignal 0 at the cap (Fall/Reach already floor at 0.5);
        // fallSwingCatchAirHipLiftCapHold floors Catch air the same
        // way. That 16°/0.5 Catch
        // air still wrote hipLift 0.25 (liftSignal −0.125 at the cap
        // vs Fall/Reach −0.25); fallSwingCatchAirHipLift writes 0.5
        // in Catch air (Catch scrape keeps 0.25). That 16°/0.5 Catch
        // air still wrote ankleLift 0.25 (leftover plantarflex on
        // µ=0.4); fallSwingCatchAirAnkleLift writes 0.5 in Catch air
        // (Catch scrape may write 0.5). That 16° Catch air still met an
        // 8° scrape cap (liftSignal 0); fallSwingCatchScrapeHipLiftCap
        // keeps 16° in Catch scrape. That 16° scrape still faded to
        // liftSignal 0 at the cap; fallSwingCatchScrapeHipLiftCapHold
        // floors Catch scrape at 0.5. That floor still writes
        // hipLift 0.25 (liftSignal −0.125 at the cap vs Fall/Reach/Catch-air
        // −0.25); fallSwingCatchScrapeHipLift writes 0.5 in Catch scrape.
        // That 16°/0.5 hipLift still wrote ankleLift 0.25 (leftover
        // plantarflex on µ=0.4); fallSwingCatchScrapeAnkleLift writes
        // 0.5 in Catch scrape. That 16°/0.5/0.5 scrape still shared the
        // 1× fold ramp (vault 1 at 0.075 s, lift still half);
        // fallSwingCatchScrapeLiftLead finishes those two at 2× so the
        // jammed toe rises before 140° fold. That 16° scrape still met
        // an 8° Catch→Reach cap (ReachResumedFromScrape, liftSignal 0);
        // fallSwingCatchReachScrapeHipLiftCap keeps 16° on that bounce.
        // That 16° Catch→Reach scrape still faded to liftSignal 0 at the
        // cap; fallSwingCatchReachScrapeHipLiftCapHold floors it at 0.5.
        // That floor still writes hipLift 0.25 (liftSignal −0.125 vs
        // Fall/Reach/Catch-air/Catch-scrape −0.25);
        // fallSwingCatchReachScrapeHipLift writes 0.5 on that bounce.
        // That 16°/0.5 hipLift still wrote ankleLift 0.25 (leftover
        // plantarflex on µ=0.4); fallSwingCatchReachScrapeAnkleLift
        // writes 0.5 on that bounce. That 16° cap still had room=0 at a
        // 16° Catch-scrape thigh (floor holds −0.25, thigh cannot walk
        // further); fallSwingCatchReachScrapeHipLiftCapWide keeps 24°
        // on that bounce. That same room=0 still pinned Catch scrape at
        // 16°; fallSwingCatchScrapeHipLiftCapWide keeps 24° in Catch
        // scrape. Fall may then use 24° (fallSwingFallHipLiftCapWide);
        // Reach may then use 24° (fallSwingReachHipLiftCapWide);
        // Catch-air may then use 24° (fallSwingCatchAirHipLiftCapWide);
        // Plant may then use 24° (fallSwingPlantHipLiftCapWide).
        // That 24° Plant leftover still had room=0 at Fall's 24° cap;
        // fallSwingFallHipLiftCapCeil keeps 32° in Fall. That 24° Reach
        // cap still had room=0 once Fall walked into 32°;
        // fallSwingReachHipLiftCapCeil keeps 32° in Reach. That 24°
        // Catch-air cap still had room=0 once Reach walked into 32°;
        // fallSwingCatchAirHipLiftCapCeil keeps 32° in Catch-air.
        // That 24° Plant leftover still had room=0 at Plant's 24° cap;
        // fallSwingPlantHipLiftCapCeil keeps 32° in Plant.
        // That 24° Catch scrape still had room=0 once Catch-air walked
        // into 32°; fallSwingCatchScrapeHipLiftCapCeil keeps 32° in
        // Catch scrape. That 24° Catch→Reach scrape still had room=0
        // once Catch scrape walked into 32°;
        // fallSwingCatchReachScrapeHipLiftCapCeil keeps 32° on that bounce.
        bool leftDrivePelvis = !leftSwingHip;
        bool rightDrivePelvis = !rightSwingHip;
        // Catch scrape (grounded, heel still up) used to drop thighHold, so
        // the pelvis loop extended against the fold. Air lift hold keeps
        // those terms until heelLoaded; planted Catch stays off so the
        // landing hip takes the trunk.
        bool catchAirLift = fallCatchWalk
            && fallCatchPhaseCode == (int)StepPhaseDriver.FallCatchPhase.Catch
            && (fc.thighHold > 0.001f || fc.hipLift > 0.001f);
        // After the first airborne plant the landing hip must not join
        // the stance DrivePelvis. Latch used to wait AheadOfStance, so a
        // just-down sole still stacked both hips on pelvis-upright and
        // the trunk sat. Airborne-then-down is that plant, latch or not.
        bool firstStepLandingDown = fallCatchWalk
            && stepPhaseDriver != null
            && stepPhaseDriver.SwingWasAirborne
            && (leftIsSwing ? leftGrounded : rightGrounded);
        bool firstStepRecoverHold = fallCatchWalk
            && stepPhaseDriver != null
            && stepPhaseDriver.FallCatchCycleCount == 0
            && (firstStepCatchPlantLatched || firstStepLandingDown)
            && (fallCatchPhaseCode == (int)StepPhaseDriver.FallCatchPhase.Catch
                || fallCatchPhaseCode == (int)StepPhaseDriver.FallCatchPhase.Recover);
        // Comments 37–47 (WORLD shear, hip snap) tried to manufacture the
        // 1.5 cm onto a leading stance. PickColdStanceBehindCom then stood
        // on the trailer so the gate opened without that 1.5 cm — and
        // handed swing to the already-leading foot (backward step). Stance
        // is the lead again. Comment 48: FirstStepTrailStillLoaded is the
        // planted trailing sole, not StanceLead ≥ 0. DrivePelvis stays on
        // the stacked hips and the 55° dirt fold stays off until air —
        // except FirstStepToeCatchReady, where the trail is light and the
        // missing actuator is the capture-point hip (catch-step), not
        // hipLift. No WORLD shear.
        bool firstStepGroundedDrive = fallCatchWalk
            && FirstStepTrailStillLoaded(stepPhaseDriver)
            && !firstStepCatchPlantLatched
            && !firstStepToeCatch
            && !stepPhaseDriver.StandingRestore
            && !stepPhaseDriver.StandingSurrender;
        // Cycle 0 lives in Catch, and Catch is where the placement hip was
        // dropped. catchAirLift needs thighHold or hipLift, and the cycle-0
        // pose zeroes both while the trail sole is down, so reachKneeFolding
        // was false: the swing hip went back to DrivePelvis (trail thigh held
        // under the hip) and the branch that writes latchedHipTarget was
        // skipped. Fall is the only other window and it lasts
        // fallKneeRampSeconds (0.15 s), of which travelPlaceLight needs
        // reachKnee ≥ 20° of the 55° ramp — the capture-point hip owned the
        // joint for ~0.1 s of a 25 s roll, so the swing foot never reached
        // the 2 cm span IsSwingFootAheadOfStance asks for (walkSwap 0).
        // Keep that window open through Catch while the toe catch is armed.
        // hipLift / DualLoad / +10° back stay off; this is the same solve,
        // one phase later.
        // Air after the first lift is Reach, not Catch. Catch-only left
        // DrivePelvis on that hip and travelPlace never owned it.
        bool firstStepToeCatchPlace = firstStepToeCatch
            && (fallCatchPhaseCode == (int)StepPhaseDriver.FallCatchPhase.Reach
                || fallCatchPhaseCode == (int)StepPhaseDriver.FallCatchPhase.Catch);
        bool reachKneeFolding = fallCatchWalk && fc.reachKnee > 0.5f
            && !firstStepGroundedDrive
            && (fallCatchPhaseCode == (int)StepPhaseDriver.FallCatchPhase.Fall
                || fallCatchPhaseCode == (int)StepPhaseDriver.FallCatchPhase.Reach
                || catchAirLift
                || firstStepToeCatchPlace
                || firstStepRecoverHold
                || (fallCatchPhaseCode == (int)StepPhaseDriver.FallCatchPhase.Plant
                    && walkPlantSwingKnee > 0.001f
                    && plantHipRelease >= 0.5f));
        if (reachKneeFolding)
        {
            if (leftIsSwing && leftGrounded)
                leftDrivePelvis = false;
            if (rightIsSwing && rightGrounded)
                rightDrivePelvis = false;
        }
        // scrapeLiftAirHold keeps ThighHold in Catch air, so leftSwingHip
        // stays off. scrapeLiftAirEnterHold does the same on Catch→Reach.
        // scrapeLiftAirCatchHold does the same on Reach→Catch.
        // scrapeLiftAirCatchFold then keeps the Reach knee fold on that
        // Catch (no 70°→35°). scrapeLiftAirEnterFold then keeps that
        // fold on Catch→Reach (stale Last).
        // Without this the pelvis loop extends the airborne swing hip —
        // the original airLiftHold hole, one contact later.
        if (keepThighHoldAir || firstStepRecoverHold)
        {
            if (leftIsSwing) leftDrivePelvis = false;
            if (rightIsSwing) rightDrivePelvis = false;
        }
        // Recover air after the clear: do not hand the millimetre of
        // lost toe to pelvis-upright SPD. That is the original
        // airLiftHold shove (open chain, third law into the pelvis).
        // Grounded Recover keeps leftDrivePelvis — that is the landing
        // hip the clear exists for.
        if (recoverHipairClear)
        {
            if (leftIsSwing && !leftGrounded) leftDrivePelvis = false;
            if (rightIsSwing && !rightGrounded) rightDrivePelvis = false;
        }
        // WALK-1 geo lift: the stance hip is the pelvis loop. DrivePelvis
        // on the planted swing is an extensor against the knee shorten.
        if (walkStanceGeoLift)
        {
            if (leftIsSwing) leftDrivePelvis = false;
            if (rightIsSwing) rightDrivePelvis = false;
        }
        // First-stance roll: same DrivePelvis drop, before geo-lift /
        // Unload. Two pelvis-upright hips are the mid-pair statue
        // (burn_stag 9 mm). Stance ankle still rolls; swing is quiet
        // until PlantStandReady. Not a scissor pose.
        if (FirstWalkSwingYield())
        {
            if (leftIsSwing) leftDrivePelvis = false;
            if (rightIsSwing) rightDrivePelvis = false;
        }

        // Hip-to-hip couple: cancel the pelvis reaction of the swing hip on
        // the stance hip. Walk-lift ADVANCE uses this step's predicted τsw
        // (mechanism 2). Every other walk still uses last step's command.
        // Only while the stance foot is down — an airborne stance leg has
        // nothing to push the reaction into, and cold oneg is untouched.
        float coupleAmt = Mathf.Clamp01(walkSwingLiftCouple);
        float leftHipCounter = 0f;
        float rightHipCounter = 0f;
        bool liftCoupleActive = false;
        if (WalkLiftOwnsSwing() && !walkLiftDirtLift)
        {
            ApplyWalkLiftReactionCouple(
                ref leftHipCounter, ref rightHipCounter,
                leftDrivePelvis, rightDrivePelvis, leftGrounded, rightGrounded,
                pelvisTarget);
            liftCoupleActive = Mathf.Abs(leftHipCounter) + Mathf.Abs(rightHipCounter) > 0.0001f;
        }
        else if (walkSoft && splitLegs && coupleAmt > 0.001f
            && lastSwingHipSide != 0 && Mathf.Abs(lastSwingHipCommand) > 0.0001f)
        {
            float counterMax = Mathf.Max(0f, walkSwingLiftCoupleMax);
            float counter = Mathf.Clamp(
                -lastSwingHipCommand * coupleAmt, -counterMax, counterMax);
            // Stance is the leg the swing was not on.
            if (lastSwingHipSide < 0 && rightDrivePelvis && rightGrounded)
            {
                rightHipCounter = counter;
                liftCoupleActive = true;
            }
            else if (lastSwingHipSide > 0 && leftDrivePelvis && leftGrounded)
            {
                leftHipCounter = counter;
                liftCoupleActive = true;
            }
        }
        lastSwingLiftCounter = leftHipCounter + rightHipCounter;
        ApplyWalkLiftStanceHipBias(ref leftHipCounter, ref rightHipCounter);
        lastSwingLiftCounter = leftHipCounter + rightHipCounter;

        // Stance hip extension — the second half of push-off, and the half the
        // human body never had (the dog has walkHipExtend, this controller had
        // nothing). Both hips were only holding the pelvis to world vertical,
        // so nothing drove the pelvis over the planted foot: the trunk waited
        // for the ankle while the swing leg reached, and the step ended under
        // the CoM again. Extension is the plus side of this joint (flexion is
        // minus, balance-actuators.mdc), so a positive bias rotates the thigh's
        // lower end back; with the foot planted that carries the pelvis forward
        // instead. Same envelope as the ankle push — load on the foot times the
        // signed CoM lead — so it only fires when the geometry makes it
        // propulsive, and it goes through the existing counter channel, which
        // clamps the sum and leaves the pelvis loop its authority.
        float stanceHipExtend = 0f;
        if (walkSoft && splitLegs && walkMoveDir != 0f && pushAfterAir
            && walkStanceHipExtend > 0.001f)
        {
            stanceHipExtend = Mathf.Clamp(
                walkStanceHipExtend * stanceLoad * pushWindow,
                0f, Mathf.Max(0f, walkStanceHipExtendMax));
            if (stanceHipExtend > 0.0001f)
            {
                if (leftIsSwing && rightDrivePelvis && rightGrounded)
                    rightHipCounter += stanceHipExtend;
                else if (rightIsSwing && leftDrivePelvis && leftGrounded)
                    leftHipCounter += stanceHipExtend;
                else
                    stanceHipExtend = 0f;
            }
        }
        // WALK-1 Stance: stacked soles have stanceLead ≈ 0, so pushWindow
        // stays 0 and walkStanceHipExtend never carried the pelvis over the
        // planted foot. walkComLeadX then parks CoM 6 cm ahead of the same
        // X and the next cycle has nothing to add. Geo-lift is the
        // initiation envelope (same clock as the swing-knee shorten).
        // Negative lead still brakes — skip that side. Quiet / oneg /
        // pf23 never set walkMoveDir. Flag walkStancePushOnLead 0 keeps
        // the former wait-for-air / pushWindow product.
        if (!fallCatchWalk && walkStancePushOnLead > 0.5f
            && walkSoft && splitLegs && walkMoveDir != 0f
            && walkStanceHipExtend > 0.001f
            && walkGeoLiftU > 0.001f
            && stanceLead >= -0.02f)
        {
            float geoRoom = stanceLead >= 0f
                ? 1f
                : Mathf.Clamp01(1f + stanceLead / 0.02f);
            float geoExtend = Mathf.Clamp(
                walkStanceHipExtend * Mathf.Max(stanceLoad, walkGeoLiftU) * geoRoom,
                0f, Mathf.Max(0f, walkStanceHipExtendMax));
            if (geoExtend > stanceHipExtend + 0.0001f)
            {
                float delta = geoExtend - stanceHipExtend;
                if (leftIsSwing && rightDrivePelvis && rightGrounded)
                {
                    rightHipCounter += delta;
                    stanceHipExtend = geoExtend;
                }
                else if (rightIsSwing && leftDrivePelvis && leftGrounded)
                {
                    leftHipCounter += delta;
                    stanceHipExtend = geoExtend;
                }
            }
        }
        // walkPlantStanceDriveFirst above. Same STANCE hip as the block
        // just above, not the trailing swing-role hip. Cycle 0 only:
        // later cycles already have pushWindow once CoM has passed the
        // leading foot, and the rear hip is plantHipExtend. Max so a
        // millimetre of lead that opened pushAfterAir is not added twice.
        // StanceLead ≥ 0: extending this hip while CoM is still BEHIND
        // the leading foot is a brake (comment 29). Trailing A2 / hip
        // own that window; this drive starts when FallLeadReady does.
        if (fallCatchWalk && walkPlantStanceDriveFirst > 0.5f
            && walkSoft && splitLegs && walkMoveDir != 0f
            && walkStanceHipExtend > 0.001f
            && fallCatchPhaseCode == (int)StepPhaseDriver.FallCatchPhase.Plant
            && stepPhaseDriver != null
            && stepPhaseDriver.FallCatchCycleCount <= 0
            && stanceLead >= 0f
            && !stepPhaseDriver.StandingRestore
            && !stepPhaseDriver.StandingSurrender
            && stepPhaseDriver.PlantGateReady)
        {
            float firstClock = Mathf.Clamp01(
                stepPhaseDriver.PhaseAge()
                / Mathf.Max(0.05f, stepPhaseDriver.plantMinDuration));
            float firstRoom = Mathf.Clamp01(
                1f - stanceLead
                / Mathf.Max(0.02f, stepPhaseDriver.plantComLeadMax));
            // walkPlantFallStanceDriveGrowFirst / GrowMoreFirst /
            // GrowStillFirst / GrowYetFirst / GrowFarFirst /
            // GrowFartherFirst / GrowFarthestFirst / GrowOutFirst /
            // GrowOutMoreFirst / GrowOutStillFirst above.
            // DualLoadMinSpan never opens, so stanceLoad is the 0.5
            // floor and GrowOutMoreFirst's Plant endpoint is 0.962 —
            // still short of the leading-hip carry the 140° pin needs.
            // Same 0.25 of that leftover (0.972 load, drive 0.1457) so
            // EnterFall is not a step. Flag 0 keeps the 0.5 floor;
            // GrowMoreFirst 0 keeps 0.625; GrowStillFirst 0 keeps 0.719;
            // GrowYetFirst 0 keeps 0.789; GrowFarFirst 0 keeps 0.842;
            // GrowFartherFirst 0 keeps 0.881; GrowFarthestFirst 0 keeps
            // 0.911; GrowOutFirst 0 keeps 0.933; GrowOutMoreFirst 0
            // keeps 0.950; GrowOutStillFirst 0 keeps 0.962.
            float driveLoad = stanceLoad;
            if (walkPlantFallStanceDriveGrowFirst > 0.5f
                && walkPlantFallFoldGrowReachFirst > 0.5f)
            {
                driveLoad = Mathf.Lerp(stanceLoad, 1f, 0.25f);
                if (walkPlantFallStanceDriveGrowMoreFirst > 0.5f)
                {
                    driveLoad = Mathf.Lerp(driveLoad, 1f, 0.25f);
                    if (walkPlantFallStanceDriveGrowStillFirst > 0.5f)
                    {
                        driveLoad = Mathf.Lerp(driveLoad, 1f, 0.25f);
                        if (walkPlantFallStanceDriveGrowYetFirst > 0.5f)
                        {
                            driveLoad = Mathf.Lerp(driveLoad, 1f, 0.25f);
                            if (walkPlantFallStanceDriveGrowFarFirst > 0.5f)
                            {
                                driveLoad = Mathf.Lerp(driveLoad, 1f, 0.25f);
                                if (walkPlantFallStanceDriveGrowFartherFirst > 0.5f)
                                {
                                    driveLoad = Mathf.Lerp(driveLoad, 1f, 0.25f);
                                    if (walkPlantFallStanceDriveGrowFarthestFirst > 0.5f)
                                    {
                                        driveLoad = Mathf.Lerp(driveLoad, 1f, 0.25f);
                                        if (walkPlantFallStanceDriveGrowOutFirst > 0.5f)
                                        {
                                            driveLoad = Mathf.Lerp(driveLoad, 1f, 0.25f);
                                            if (walkPlantFallStanceDriveGrowOutMoreFirst > 0.5f)
                                            {
                                                driveLoad = Mathf.Lerp(driveLoad, 1f, 0.25f);
                                                if (walkPlantFallStanceDriveGrowOutStillFirst > 0.5f)
                                                    driveLoad = Mathf.Lerp(driveLoad, 1f, 0.25f);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            float firstExtend = Mathf.Clamp(
                walkStanceHipExtend * driveLoad * firstClock * firstRoom,
                0f, Mathf.Max(0f, walkStanceHipExtendMax));
            if (firstExtend > stanceHipExtend + 0.0001f)
            {
                float delta = firstExtend - stanceHipExtend;
                if (leftIsSwing && rightDrivePelvis && rightGrounded)
                {
                    rightHipCounter += delta;
                    stanceHipExtend = firstExtend;
                }
                else if (rightIsSwing && leftDrivePelvis && leftGrounded)
                {
                    leftHipCounter += delta;
                    stanceHipExtend = firstExtend;
                }
            }
        }
        // walkPlantFallStanceDriveHoldFirst above. Plant already drove to
        // clock 1 (EnterFall requires plantAged). Fall's PhaseAge is 0,
        // pushAfterAir is 0 while the swing has never left, pushWindow
        // is 0 while CoM sits on the stacked feet, so the later-cycle
        // extend is 0 and the stance hip is world-upright again — the
        // 03:52 drive as a no-op one phase later. Hold that endpoint.
        // Catch / later cycles keep pushAfterAir / pushWindow. Max so a
        // millimetre of lead that opened the later-cycle block is not
        // added twice.
        if (fallCatchWalk && walkPlantFallStanceDriveHoldFirst > 0.5f
            && walkPlantStanceDriveFirst > 0.5f
            && walkSoft && splitLegs && walkMoveDir != 0f
            && walkStanceHipExtend > 0.001f
            && (fallCatchPhaseCode == (int)StepPhaseDriver.FallCatchPhase.Fall
                || fallCatchPhaseCode == (int)StepPhaseDriver.FallCatchPhase.Reach)
            && stepPhaseDriver != null
            && stepPhaseDriver.FallCatchCycleCount <= 0
            && stanceLead >= 0f
            && !stepPhaseDriver.StandingRestore
            && !stepPhaseDriver.StandingSurrender
            && intent != null && Mathf.Abs(intent.moveX) > 0.01f)
        {
            float firstRoom = Mathf.Clamp01(
                1f - stanceLead
                / Mathf.Max(0.02f, stepPhaseDriver.plantComLeadMax));
            // walkPlantFallStanceDriveGrowFirst / GrowMoreFirst /
            // GrowStillFirst / GrowYetFirst / GrowFarFirst /
            // GrowFartherFirst / GrowFarthestFirst / GrowOutFirst /
            // GrowOutMoreFirst / GrowOutStillFirst above. Hold copied
            // the Plant 0.962 endpoint; the 140° pin still sits on a
            // stacked sole. Same 0.25 of that leftover (0.972 load,
            // drive 0.1457). Flag 0 keeps the 0.5 floor; GrowMoreFirst 0
            // keeps 0.625; GrowStillFirst 0 keeps 0.719; GrowYetFirst 0
            // keeps 0.789; GrowFarFirst 0 keeps 0.842; GrowFartherFirst 0
            // keeps 0.881; GrowFarthestFirst 0 keeps 0.911; GrowOutFirst 0
            // keeps 0.933; GrowOutMoreFirst 0 keeps 0.950;
            // GrowOutStillFirst 0 keeps 0.962.
            // Catch / later cycles keep DualLoad / pushAfterAir.
            float driveLoad = stanceLoad;
            if (walkPlantFallStanceDriveGrowFirst > 0.5f
                && walkPlantFallFoldGrowReachFirst > 0.5f)
            {
                driveLoad = Mathf.Lerp(stanceLoad, 1f, 0.25f);
                if (walkPlantFallStanceDriveGrowMoreFirst > 0.5f)
                {
                    driveLoad = Mathf.Lerp(driveLoad, 1f, 0.25f);
                    if (walkPlantFallStanceDriveGrowStillFirst > 0.5f)
                    {
                        driveLoad = Mathf.Lerp(driveLoad, 1f, 0.25f);
                        if (walkPlantFallStanceDriveGrowYetFirst > 0.5f)
                        {
                            driveLoad = Mathf.Lerp(driveLoad, 1f, 0.25f);
                            if (walkPlantFallStanceDriveGrowFarFirst > 0.5f)
                            {
                                driveLoad = Mathf.Lerp(driveLoad, 1f, 0.25f);
                                if (walkPlantFallStanceDriveGrowFartherFirst > 0.5f)
                                {
                                    driveLoad = Mathf.Lerp(driveLoad, 1f, 0.25f);
                                    if (walkPlantFallStanceDriveGrowFarthestFirst > 0.5f)
                                    {
                                        driveLoad = Mathf.Lerp(driveLoad, 1f, 0.25f);
                                        if (walkPlantFallStanceDriveGrowOutFirst > 0.5f)
                                        {
                                            driveLoad = Mathf.Lerp(driveLoad, 1f, 0.25f);
                                            if (walkPlantFallStanceDriveGrowOutMoreFirst > 0.5f)
                                            {
                                                driveLoad = Mathf.Lerp(driveLoad, 1f, 0.25f);
                                                if (walkPlantFallStanceDriveGrowOutStillFirst > 0.5f)
                                                    driveLoad = Mathf.Lerp(driveLoad, 1f, 0.25f);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            float firstExtend = Mathf.Clamp(
                walkStanceHipExtend * driveLoad * firstRoom,
                0f, Mathf.Max(0f, walkStanceHipExtendMax));
            if (firstExtend > stanceHipExtend + 0.0001f)
            {
                float delta = firstExtend - stanceHipExtend;
                if (leftIsSwing && rightDrivePelvis && rightGrounded)
                {
                    rightHipCounter += delta;
                    stanceHipExtend = firstExtend;
                }
                else if (rightIsSwing && leftDrivePelvis && leftGrounded)
                {
                    leftHipCounter += delta;
                    stanceHipExtend = firstExtend;
                }
            }
        }
        lastStanceHipExtend = stanceHipExtend;

        // Hip half of the double-support push-off (walkPlantHipExtend above).
        // The gate is the trailing-ankle burst's gate, for the reasons already
        // recorded there: only a settled Plant (PlantGateReady), never while the
        // recovery ladder holds — a restore or surrender Plant is a brake, and a
        // push there is the fold that ladder exists to prevent. Cycle 0: the
        // 3 cm viewpoint lunge is not a rear ANKLE arm (A2 stays later-cycle,
        // comment 30) and is not this DrivePelvis counter (comment 35
        // cancelled, comment 43 does not restore it — the translator is
        // plantRockLead on the leading ankle, not a DrivePelvis counter).
        // Later cycles keep the counter on a catch plant behind CoM.
        float plantHipExtend = 0f;
        if (fallCatchWalk && splitLegs && walkMoveDir != 0f
            && walkPlantHipExtend > 0.001f && walkStanceHipExtend > 0.001f
            && fallCatchPhaseCode == (int)StepPhaseDriver.FallCatchPhase.Plant
            && stepPhaseDriver.FallCatchCycleCount > 0
            && !stepPhaseDriver.StandingRestore
            && !stepPhaseDriver.StandingSurrender
            && stepPhaseDriver.PlantGateReady
            && bodyState != null && comCalculator != null)
        {
            bool trailGrounded = leftIsSwing ? leftGrounded : rightGrounded;
            if (trailGrounded)
            {
                float trailFootX = leftIsSwing
                    ? bodyState.leftFootPosition.x
                    : bodyState.rightFootPosition.x;
                // Walk coordinates. Positive means the CoM has already passed
                // the rear foot, which is the only geometry where extending
                // that hip is propulsive rather than a brake.
                float trailLead =
                    (comCalculator.GetCenterOfMass().x - trailFootX) * fcLeadDir;
                float trailWindow = Mathf.Clamp01(
                    trailLead / Mathf.Max(0.01f, walkPlantPushLeadWindow));
                // Same fade as the ankle burst: past plantComLeadMax the
                // Plant→Fall gate shuts and the ladder arms, so the phase must
                // hand the body over instead of stalling it.
                float plantRoom = Mathf.Clamp01(
                    1f - stanceLead
                    / Mathf.Max(0.02f, stepPhaseDriver.plantComLeadMax));
                // Fade on the trailing foot's OWN clearance, not
                // liftGateBlend: after walkDualLoadRelease that blend is the
                // CoM weight-share, so (1 − blend) killed this bias in the
                // middle of double support — the one window where extending
                // the rear hip is push-off. Clearance is zero until the foot
                // actually leaves, which is when the hip belongs to the lift.
                float trailClear = leftIsSwing
                    ? bodyState.leftFootGroundClearance
                    : bodyState.rightFootGroundClearance;
                float trailAir = Mathf.Clamp01(
                    trailClear / Mathf.Max(0.005f, walkTouchdownSyncClearance));
                float rearShare = dualRearShare >= 0f ? dualRearShare : stanceLoad;
                float rearLoad = rearShare * (1f - trailAir);
                plantHipExtend = Mathf.Clamp(
                    walkStanceHipExtend * Mathf.Clamp01(walkPlantHipExtend)
                        * rearLoad * trailWindow * plantRoom,
                    0f, Mathf.Max(0f, walkStanceHipExtendMax));
                if (plantHipExtend > 0.0001f)
                {
                    if (leftIsSwing && leftDrivePelvis && leftGrounded)
                        leftHipCounter += plantHipExtend;
                    else if (rightIsSwing && rightDrivePelvis && rightGrounded)
                        rightHipCounter += plantHipExtend;
                    else
                        plantHipExtend = 0f;
                }
            }
        }
        lastPlantHipExtend = plantHipExtend;

        // Cycle-0 Plant keeps DrivePelvis: the pelvis-upright loop holds
        // the trunk over the leading support while the trailer unloads.
        // Quiet stance / walk split / EndWalk keep hipHold off on a split.
        ControlHipsToWorldUpright(
            pelvisTarget,
            leftDrivePelvis,
            rightDrivePelvis,
            leftSwingUnload,
            rightSwingUnload,
            leftHipCounter,
            rightHipCounter,
            !(walkSoft || splitLegs));

        // After lift-off the target is not weaker than the latch angle: else
        // the thigh extends and the 26 cm foot stomps back.
        float speedSwingBoost = Mathf.Clamp(
            walkSpeedError * Mathf.Max(0f, walkSpeedSwingHipGain),
            0f,
            Mathf.Max(0f, walkSpeedSwingFlexMax));
        float baseSwingHipBoost = walkSoft ? Mathf.Max(0f, walkSwingHipBoost) : 0f;
        float placementHipBoost = 0f;
        float touchdownWindow = 0f;
        // Kept for the geometric swing solve below: it needs the landing
        // target in metres, and swingHipFlexEffective (its bound) is not
        // known until after this block.
        float placementDesiredStep = 0f;
        float placementStanceX = 0f;
        bool placementSolved = false;
        // walkStepPlaceOnFold above. Knee angle is last step's pose (this
        // frame's geo-lift command writes after the hips), so a still-straight
        // strut cannot take the path. Plus = flexion.
        bool travelPlaceLight = false;
        if (fallCatchWalk && walkStepPlaceOnFold > 0.5f && splitLegs
            && walkMoveDir != 0f)
        {
            HingeJoint2D placeKnee = leftIsSwing ? leftKneeJoint : rightKneeJoint;
            float kneeNow = placeKnee != null ? placeKnee.jointAngle : 0f;
            float clearanceNow = bodyState != null
                ? (leftIsSwing
                    ? bodyState.leftFootGroundClearance
                    : bodyState.rightFootGroundClearance)
                : 0f;
            travelPlaceLight = kneeNow >= 20f || clearanceNow >= 0.01f
                || (firstStepToeCatch && fc.reachKnee >= 20f);
        }
        // Cycle-0 Plant/Fall DIRT: DrivePelvis on the stacked hip.
        // walkStepPlaceOnFold then last-writes the 4 cm geo solve —
        // Asin(0.04 / 0.78 m) ≈ 3° — over that hip the moment the knee is
        // ≥ 20°. Keep DrivePelvis through Plant/Fall so the 3° unwind does
        // not glue a µ=1.2 sole. FirstStepToeCatchReady is the dirt
        // exception (CoM already toward the toe).
        // After lift-off that guard is the miss: Reach is an open chain,
        // DualLoadMinSpan / ThighHold keep the trailer under the hip, and
        // the 8° plant latches only once the swing is already ahead — so
        // the foot never walks forward of CoM (1.5 m toe-roll, torso −71).
        // Air must run the capture / catch-lead solve. Later cycles place.
        // Planted AND already ahead: this same 8° snap is Recover's miss.
        // Catch Recovers on AheadOfStance; firstStepCatchPlant then writes
        // −8° hip / 6° knee and retracts below FootLeadDeadzone before
        // RecoverTouchdownMinAge, abort-to-Plant, walkSwap 0. Dirt-behind
        // only. A forward plant keeps the geo hip until EnterFallCatchPlant.
        bool swingStillDown = leftIsSwing ? leftGrounded : rightGrounded;
        bool firstStepHoldLift = fallCatchWalk
            && stepPhaseDriver != null
            && stepPhaseDriver.FallCatchCycleCount == 0
            && !firstStepToeCatch
            && !stepPhaseDriver.SwingWasAirborne
            && swingStillDown
            && !stepPhaseDriver.SwingFootAheadOfStance
            && (fallCatchPhaseCode == (int)StepPhaseDriver.FallCatchPhase.Plant
                || fallCatchPhaseCode == (int)StepPhaseDriver.FallCatchPhase.Fall
                || fallCatchPhaseCode == (int)StepPhaseDriver.FallCatchPhase.Reach
                || fallCatchPhaseCode == (int)StepPhaseDriver.FallCatchPhase.Catch
                || fallCatchPhaseCode == (int)StepPhaseDriver.FallCatchPhase.Recover);
        bool firstStepCatchPlant = firstStepHoldLift
            && firstStepCatchPlantLatched
            && (fallCatchPhaseCode == (int)StepPhaseDriver.FallCatchPhase.Fall
                || fallCatchPhaseCode == (int)StepPhaseDriver.FallCatchPhase.Reach
                || fallCatchPhaseCode == (int)StepPhaseDriver.FallCatchPhase.Catch
                || fallCatchPhaseCode == (int)StepPhaseDriver.FallCatchPhase.Recover);
        if (splitLegs && bodyState != null)
        {
            float clearance = leftIsSwing
                ? bodyState.leftFootGroundClearance
                : bodyState.rightFootGroundClearance;
            float touchH = Mathf.Max(0.005f, walkTouchdownSyncClearance);
            touchdownWindow = Mathf.Clamp01(1f - Mathf.Max(0f, clearance) / touchH);
        }
        if (walkSoft && splitLegs && bodyState != null && walkMoveDir != 0f)
        {
            float placementScale = 0f;
            float placementPhase = Mathf.Clamp01(walkPlacementSyncStart);
            if (fallCatchWalk)
            {
                // The fall-catch FSM reports PhaseCode 0 for Plant/Fall/Reach,
                // and those phases last ~0.5-1 s while walkStanceProgress is
                // normalised by stanceDuration (6 s). That ratio never reaches
                // walkPlacementSyncStart, so on the path Play actually walks
                // the placement window never opened at all. The swing first
                // leaves the ground in Fall — that is the first open chain
                // where the hip can put the foot ahead of the CoM without
                // the sci_a shove. Reach and Catch keep the same window so
                // a short Fall still commands the landing. Grounded frames
                // still multiply by walkStepPlacementGroundFraction (0).
                if (fallCatchPhaseCode == (int)StepPhaseDriver.FallCatchPhase.Fall
                    || fallCatchPhaseCode == (int)StepPhaseDriver.FallCatchPhase.Reach
                    || fallCatchPhaseCode == (int)StepPhaseDriver.FallCatchPhase.Catch
                    || (travelPlaceLight
                        && stepPhaseDriver != null
                        && stepPhaseDriver.FallCatchCycleCount == 0
                        && fallCatchPhaseCode == (int)StepPhaseDriver.FallCatchPhase.Plant))
                    placementScale = 1f;
            }
            else if (walkStancePhase && walkStanceProgress >= placementPhase)
                placementScale = (walkStanceProgress - placementPhase) / Mathf.Max(0.01f, 1f - placementPhase);
            bool swingAirNow = leftIsSwing ? !leftGrounded : !rightGrounded;
            if (swingAirNow)
                // Where the foot lands is decided while the leg is in the air,
                // and the hip needs the whole flight to get there. Scaling by
                // touchdownWindow gave the correction only inside the last 6 cm
                // of descent — the one moment it can no longer move the landing
                // point, and can only jam the ankle on contact. Full authority
                // while airborne; the error itself fades as the foot passes the
                // stance foot, so this does not become a permanent reach.
                placementScale = Mathf.Clamp01(placementScale);
            else
                placementScale = Mathf.Clamp01(placementScale) * Mathf.Clamp01(walkStepPlacementGroundFraction);
            // walkStepPlaceOnFold above. Grounded × 0 left the landing under
            // the CoM. A folded or already-clearing swing can take the same
            // Fall/Reach/Catch window the air path uses. Plant keeps DrivePelvis.
            if (placementScale <= 0.0001f && travelPlaceLight && fallCatchWalk
                && (fallCatchPhaseCode == (int)StepPhaseDriver.FallCatchPhase.Fall
                    || fallCatchPhaseCode == (int)StepPhaseDriver.FallCatchPhase.Reach
                    || fallCatchPhaseCode == (int)StepPhaseDriver.FallCatchPhase.Catch
                    || (stepPhaseDriver != null
                        && stepPhaseDriver.FallCatchCycleCount == 0
                        && fallCatchPhaseCode == (int)StepPhaseDriver.FallCatchPhase.Plant)))
                placementScale = 1f;
            if (placementScale <= 0.0001f)
                placementScale = 0f;

            float strideCap = Mathf.Max(0.05f, walkStepLength)
                + walkSpeedError * Mathf.Max(0f, walkStepLengthSpeedGain);
            // The foot has to land in FRONT of the CoM, and walkStepLength is
            // measured from the stance foot — so while the body rides forward
            // the same 0.22 m can still put the foot down behind the CoM. Then
            // the new stance starts already tipped past its own support: that
            // is a stumble the catch has to absorb, not a step, and travel
            // stalls because the next cycle spends itself recovering. The
            // capture point (CoM plus comVel/omega0, already computed for the
            // ankle) plus a margin, all in walk coordinates from the stance
            // foot. Faster body, longer step — no separate clock.
            float captureStep = stanceLead + xcomShift * walkMoveDir
                + Mathf.Max(0f, walkStepComMargin);
            // Which of the two is the target and which is the bound is the
            // whole point. As a floor (Max) the 0.22 m stride won every cycle
            // this gait has ever run, because a body at ~0.2 m/s has a capture
            // step near 0.11 m; the foot then landed about one capture point
            // too far and the arc could not be completed. As a cap the stride
            // only limits how long a fast body may reach, and the commanded
            // step follows the body's own speed.
            float desiredStep;
            if (fallCatchWalk && walkStepCaptureTarget > 0.5f)
            {
                // walkStepCatchLeadSync above. captureStep − stanceLead is the
                // commanded lead of the landing foot over the CoM, and it is
                // the very quantity StepPhaseDriver.IsSwingFootAheadOfCom
                // measures. Raise the command to the criterion it is judged
                // by; never lower it. ActiveCatchFootLead, not the field: the
                // criterion is the capture point on a slow body
                // (catchLeadCapture), and syncing to the raw 0.08 m would
                // command the over-long braking step the criterion no longer
                // asks for. With catchLeadCapture 0 the call returns
                // catchFootLead and this is the former line.
                float syncedStep = captureStep;
                if (walkStepCatchLeadSync > 0.5f && stepPhaseDriver != null)
                    syncedStep = Mathf.Max(
                        captureStep,
                        stanceLead
                            + stepPhaseDriver.ActiveCatchFootLead()
                            + Mathf.Max(0f, walkStepCatchLeadPad));
                // Cycle 0 DIRT (never flew): walkStepMinLength 0.08 plus
                // catch-lead sync asks for a slide µ=1.2 cannot make on
                // stacked soles. Command DualLoadMinSpan — the same X
                // PickStanceFromFeet / IsSwingFootAheadOfStance need.
                // After lift-off that 2 cm target is behind a CoM that has
                // already rolled toward the toe, so the geo hip would aim
                // the trailer under the hip. Air / FirstStepToeCatchReady /
                // any later cycle: capture + catchFootLead, and never cap
                // it behind CoM. walkStepLength 0.22 is from the stance
                // foot: once StanceLead has passed the toe the same 0.22 m
                // lands behind the mass. Recover then swapped onto that
                // sole and GetCoMOffsetX exceeded fallCoMOffset.
                bool swingAirPlace = leftIsSwing ? !leftGrounded : !rightGrounded;
                bool firstStepDirtPlace = stepPhaseDriver != null
                    && stepPhaseDriver.FallCatchCycleCount == 0
                    && !firstStepToeCatch
                    && !swingAirPlace
                    && !stepPhaseDriver.SwingWasAirborne;
                if (firstStepDirtPlace)
                    desiredStep = DualLoadMinSpan;
                else
                {
                    float stepMin = Mathf.Max(0f, walkStepMinLength);
                    // Capture is the floor on every cycle. As a cycle-0-only
                    // raise, cycle 1 Clamp(synced, 0.08, 0.22) commanded a
                    // plant behind CoM the moment the first swap put the
                    // mass past the new stance.
                    float floor = Mathf.Max(stepMin, captureStep);
                    desiredStep = Mathf.Clamp(
                        syncedStep,
                        floor,
                        Mathf.Max(floor, strideCap));
                }
            }
            else
                desiredStep = Mathf.Max(strideCap, captureStep);
            float stanceX = leftIsSwing ? bodyState.rightFootPosition.x : bodyState.leftFootPosition.x;
            float swingX = leftIsSwing ? bodyState.leftFootPosition.x : bodyState.rightFootPosition.x;
            placementDesiredStep = desiredStep;
            placementStanceX = stanceX;
            placementSolved = true;
            float currentStep = (swingX - stanceX) * walkMoveDir;
            float stepErr = desiredStep - currentStep;
            placementHipBoost = Mathf.Clamp(
                stepErr * Mathf.Max(0f, walkStepPlacementGain),
                -Mathf.Max(0f, walkStepPlacementMax),
                Mathf.Max(0f, walkStepPlacementMax)) * placementScale;
        }
        float swingHipFlexEffective = swingHipFlex + baseSwingHipBoost + speedSwingBoost;
        float airFlexTarget = -swingHipFlexEffective;
        if (Marching())
            airFlexTarget = -marchHipFlex;
        // Turn the landing target (metres) into the hip angle that reaches it
        // (walkSwingPlaceGeometry above). Only +X: every other swing term in
        // this block flexes toward +X unconditionally, so a mirrored solve
        // would be the one direction-aware target here and would disagree with
        // its own bound. The −X walk is a dead branch (moveX is always +1).
        // True only when the solve below really produced the target, so the
        // placement boost is dropped for that frame and nowhere else.
        bool airFlexSolved = false;
        if (fallCatchWalk && walkSwingPlaceGeometry > 0.5f && placementSolved
            && walkMoveDir > 0f && (swingHipLatched || travelPlaceLight)
            && !firstStepHoldLift)
        {
            bool swingAirGeo = leftIsSwing ? !leftGrounded : !rightGrounded;
            HingeJoint2D geoHip = leftIsSwing ? leftHipJoint : rightHipJoint;
            HingeJoint2D geoAnkle = leftIsSwing ? leftAnkleJoint : rightAnkleJoint;
            if ((swingAirGeo || travelPlaceLight) && geoHip != null && geoAnkle != null)
            {
                // Hinge anchors are the joint centres themselves (the hip
                // hinge sits on the thigh, the ankle hinge on the foot), so
                // this is the real leg, folded exactly as it is right now.
                Vector2 hipPoint = geoHip.transform.TransformPoint(geoHip.anchor);
                Vector2 anklePoint = geoAnkle.transform.TransformPoint(geoAnkle.anchor);
                float strutDown = hipPoint.y - anklePoint.y;
                // Guard the degenerate strut: with the ankle level with the
                // hip (or above it) the forward angle is not a placement any
                // more, and Atan2 would swing through ±90°.
                if (strutDown > 0.05f)
                {
                    float strutFwd = (anklePoint.x - hipPoint.x) * walkMoveDir;
                    float strutLen = Mathf.Sqrt(strutFwd * strutFwd + strutDown * strutDown);
                    float strutNow = Mathf.Atan2(strutFwd, strutDown) * Mathf.Rad2Deg;
                    // desiredStep is measured from the stance foot; the strut
                    // is measured from the hip.
                    //
                    // walkSwingPlaceFootRef above: the strut ends at the ankle
                    // hinge, while desiredStep (and the catch criterion) speak
                    // about the foot centre, which sits this far ahead of the
                    // hinge. Take it off the target so the SOLE lands on the
                    // commanded point instead of overshooting it by one heel
                    // offset. The hinge lives on the foot segment, so its
                    // transform is the very position bodyState reports.
                    float footAhead = 0f;
                    if (walkSwingPlaceFootRef > 0.5f)
                        footAhead = Mathf.Max(0f,
                            (geoAnkle.transform.position.x - anklePoint.x) * walkMoveDir);
                    float wantFwd = placementDesiredStep
                        - (hipPoint.x - placementStanceX) * walkMoveDir
                        - footAhead;
                    float strutWant = Mathf.Asin(
                        Mathf.Clamp(wantFwd / strutLen, -1f, 1f)) * Mathf.Rad2Deg;
                    airFlexTarget = Mathf.Clamp(
                        geoHip.jointAngle - (strutWant - strutNow),
                        -Mathf.Max(0f, swingHipFlexEffective), 0f);
                    airFlexSolved = true;
                }
            }
        }
        // Anti-stomp Min keeps a CONSTANT −swingHipFlex target from walking
        // toward 0: that extension plants the 26 cm foot backward. The
        // geometric solve is the other command: it already aims at
        // desiredStep (capture + catchFootLead, ahead of CoM) and is
        // clamped to [-swingHipFlexEffective, 0], so a LESS-flexed result
        // is the overshoot coming home, not a stomp. Min(latch, solve) on
        // a 32° leftover vs a ~25° landing keeps the leftover, the sole
        // lands past the capture point, and travel dies once per step
        // (walkStepCaptureTarget). walkSwingPlaceGeoOnly already dropped
        // the P-boost that used to fight this; without handing the target
        // to the solve, that drop left placement with no actuator.
        // Couple ceiling still Max()s to −32° below. Flag 0 of geo-only
        // keeps Min (and the stacked boost).
        float latchedHipTarget;
        if (swingHipLatched && !(airFlexSolved && walkSwingPlaceGeoOnly > 0.5f))
            latchedHipTarget = Mathf.Min(swingHipAngleAtLatch, airFlexTarget);
        else
            latchedHipTarget = airFlexTarget;
        // walkSwingPlaceGeoOnly above: the solved target already zeroes this
        // very step error, so the boost here would be the second controller on
        // it. Dropping it also shrinks the commanded jump at the latch frame
        // when the solve is off (the target moves toward swingHipAngleAtLatch,
        // never away), which is the same discontinuity walkReachAirSwingSeconds
        // ramps out. When the solve owns the target, the Min is already off.
        float placementAirBoost = placementHipBoost;
        if (airFlexSolved && walkSwingPlaceGeoOnly > 0.5f)
            placementAirBoost = 0f;
        latchedHipTarget -= placementAirBoost;
        // Velocity match at touchdown (walkSwingRetractGain above): take out the
        // swing foot's remaining forward ground speed with hip extension instead
        // of planting a sole that is still sliding forward. One-sided by the
        // clamp — a foot already moving backward asks for nothing — and scaled
        // by touchdownWindow so the target ramps in over the last centimetres
        // instead of stepping, which is the shape that shoves the pelvis.
        float swingRetract = 0f;
        if (walkSoft && fallCatchWalk && splitLegs && walkMoveDir != 0f
            && swingHipLatched && walkSwingRetractGain > 0.001f
            && touchdownWindow > 0.001f)
        {
            bool swingAirRetract = leftIsSwing ? !leftGrounded : !rightGrounded;
            Rigidbody2D swingFootRb = leftIsSwing ? leftFootRb : rightFootRb;
            if (swingAirRetract && swingFootRb != null)
            {
                Vector2 swingFootVel = swingFootRb.linearVelocity;
                // Descending only: the same clearance band is crossed going up.
                if (swingFootVel.y < 0f)
                    swingRetract = Mathf.Clamp(
                        swingFootVel.x * walkMoveDir * walkSwingRetractGain,
                        0f, Mathf.Max(0f, walkSwingRetractMax)) * touchdownWindow;
            }
        }
        // Extension is plus on the hip (balance-actuators.mdc), so this reduces
        // flexion. Reach-air and Catch-air both read latchedHipTarget, which is
        // exactly the window where the foot is descending onto the dirt.
        latchedHipTarget += swingRetract;
        // Zero error on the first air frame, the full placement target by the
        // end of the ramp. swingAirStreak is the swing foot's continuous air
        // time and resets on contact, so a scrape restarts the ramp instead of
        // resuming mid-flight at an angle the thigh is nowhere near.
        float reachAirSwingTarget = latchedHipTarget;
        if (reachAirSwing)
            reachAirSwingTarget = Mathf.Lerp(
                swingHipAngleAtLatch,
                latchedHipTarget,
                Mathf.Clamp01(swingAirStreak / Mathf.Max(0.01f, walkReachAirSwingSeconds)));
        // 01:05 can now keep Plant first air. Tick −50 then EnterReach on
        // airborne Fall, so the next Reach frame takes reachAirSwing:
        // Min(angleAtLatch, −swingHipFlex). A 32° leftover thigh is
        // already at coupleCapCeilingDeg, so the ramp walks to −40°
        // (sci_a). Clamp the ramp to that ceiling; ThighHold keeps and
        // hipair stay off. Flag 0 is the former −swingHipFlex.
        if (reachAirSwing
            && stepPhaseDriver != null
            && stepPhaseDriver.fallSwingReachAirSwingCeil > 0.5f)
        {
            const float coupleCapCeilingDegReach = 32f;
            reachAirSwingTarget = Mathf.Max(reachAirSwingTarget, -coupleCapCeilingDegReach);
        }
        // 01:15 can now clear hipair on Catch→Reach scrape-air. Recover
        // still has no keep (landing hip must take the trunk), and a
        // millimetre of lost catch-toe starts leftSwingHip on: Min(
        // angleAtLatch, −swingHipFlex) = −40° when leftover is already
        // at coupleCapCeilingDeg. 01:12 only clamped the Reach ramp
        // (reachAirHold). Clamp the hipair pose to the same ceiling so
        // Recover's 0.05 s plant window is not a sci_a shove. Retract
        // already ran: a less-flexed target stays. Flag 0 is the former
        // −swingHipFlex. fallSwingRecoverHipairClear then drops Recover
        // hipair entirely (this clamp is a no-op there); other phases
        // still hit the 32° ceiling.
        if (fallCatchWalk && (leftSwingHip || rightSwingHip)
            && stepPhaseDriver != null
            && stepPhaseDriver.fallSwingHipairCeil > 0.5f)
        {
            const float coupleCapCeilingDegHipair = 32f;
            latchedHipTarget = Mathf.Max(latchedHipTarget, -coupleCapCeilingDegHipair);
        }
        // On the ground the reach is dosed: the reaction goes into the pelvis, not the step.
        float groundedHipTarget = -swingHipFlexEffective * scissorAmt;
        // placementScale already carries walkStepPlacementGroundFraction for a
        // grounded swing; the second factor squared it (0.5 arrived as 0.25).
        groundedHipTarget -= placementHipBoost;
        // Fall lift-off: 8° of a 0.43 m thigh is ~0.4 cm up, so the 140°
        // fold still slides along µ=0.4 and Fall→Reach never sees air.
        // 16° is still far from sci_a 40°. Catch scrape may use 16°
        // (fallSwingCatchScrapeHipLiftCap); Catch→Reach scrape may use
        // 16° (fallSwingCatchReachScrapeHipLiftCap); Catch scrape may
        // then use 24° (fallSwingCatchScrapeHipLiftCapWide); Catch→Reach
        // scrape may then use 24° (fallSwingCatchReachScrapeHipLiftCapWide);
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
        // Catch→Reach scrape
        // may floor that fade at 0.5 (fallSwingCatchReachScrapeHipLiftCapHold);
        // Catch→Reach scrape may write hipLift 0.5
        // (fallSwingCatchReachScrapeHipLift);
        // hipLift / ankleLift
        // on scrape may write 0.5 (fallSwingCatchScrapeHipLift /
        // fallSwingCatchScrapeAnkleLift).
        // Reach may use 16° (fallSwingReachHipLiftCap) so a 16° Fall
        // thigh is not immediately past the cap (liftSignal 0, hold
        // only fires past vertical going back). Flag 0 is the former
        // cap. Not −swingHipFlex. Quiet when 0.
        // Bias 0.25 never walks into that 16° room (liftSignal −0.20 at
        // vertical); fallSwingFallHipLift writes 0.5 in Fall only;
        // fallSwingReachHipLift writes 0.5 in Reach (Catch scrape keeps
        // 0.25). That 16°/0.5 Reach still faded to liftSignal 0 at the
        // cap; fallSwingReachHipLiftCapHold floors the fade at 0.5.
        // That same P=0 still drops
        // the Fall thigh at 16°; fallSwingFallHipLiftCapHold floors
        // Fall the same way (Reach floor is Reach-only).
        // leftover→0 still shared that fold clock; fallSwingFallVaultLead
        // finishes the strut at 2× so height is there before 140° fold.
        // hipLift / ankleLift still shared that 1× ramp (vault 1 at
        // 0.075 s, lift still half); fallSwingFallLiftLead finishes those
        // two at 2× so the foot rises before 140° fold. Fold still
        // shared that 1× ramp (70° at lift=1); fallSwingFallFoldLead
        // finishes 140° with that lift. Timeout Catch still faded at 8°,
        // so a 16° Reach thigh was already past the cap (liftSignal 0);
        // fallSwingCatchAirHipLiftCap keeps 16° in Catch air.
        // That 16° Catch air still faded to liftSignal 0 at the cap;
        // fallSwingCatchAirHipLiftCapHold floors Catch air at 0.5.
        // That floor still writes
        // hipLift 0.25 (liftSignal −0.125 at the cap vs Fall/Reach
        // −0.25); fallSwingCatchAirHipLift writes 0.5 in Catch air
        // (Catch scrape keeps 0.25). That 16°/0.5 Catch air still
        // wrote ankleLift 0.25; fallSwingCatchAirAnkleLift writes 0.5
        // in Catch air (Catch scrape keeps 0.25). That 16° Catch air
        // still met an 8° scrape cap (liftSignal 0); fallSwingCatchScrapeHipLiftCap
        // keeps 16° in Catch scrape. That 16° scrape still faded to
        // liftSignal 0 at the cap; fallSwingCatchScrapeHipLiftCapHold
        // floors Catch scrape at 0.5. That floor still writes
        // hipLift 0.25 (liftSignal −0.125 at the cap vs Fall/Reach/Catch-air
        // −0.25); fallSwingCatchScrapeHipLift writes 0.5 in Catch scrape.
        // That 16°/0.5 hipLift still wrote ankleLift 0.25; fallSwingCatchScrapeAnkleLift
        // writes 0.5 in Catch scrape. That 16°/0.5/0.5 scrape still
        // shared the 1× fold ramp; fallSwingCatchScrapeLiftLead
        // finishes those two at 2× so the jammed toe rises before
        // 140° fold. That 16° scrape still met an 8° Catch→Reach cap
        // (ReachResumedFromScrape, liftSignal 0); fallSwingCatchReachScrapeHipLiftCap
        // keeps 16° on that bounce. That 16° Catch→Reach scrape still
        // faded to liftSignal 0 at the cap; fallSwingCatchReachScrapeHipLiftCapHold
        // floors it at 0.5. That floor still writes hipLift 0.25
        // (liftSignal −0.125 vs Fall/Reach/Catch-air/Catch-scrape −0.25);
        // fallSwingCatchReachScrapeHipLift writes 0.5 on that bounce.
        // That 16°/0.5 hipLift still wrote ankleLift 0.25; fallSwingCatchReachScrapeAnkleLift
        // writes 0.5 on that bounce. That 16° cap still had room=0 at a
        // 16° Catch-scrape thigh; fallSwingCatchReachScrapeHipLiftCapWide
        // keeps 24° on that bounce. That same room=0 still pinned Catch
        // scrape at 16°; fallSwingCatchScrapeHipLiftCapWide keeps 24°
        // in Catch scrape.
        float hipLiftCapDeg = 8f;
        if (fallCatchWalk
            && fallCatchPhaseCode == (int)StepPhaseDriver.FallCatchPhase.Fall
            && stepPhaseDriver.fallSwingFallHipLiftCap > 0.5f)
        {
            hipLiftCapDeg = 16f;
            // 16° cap has room=0 at a 16° Plant/Fall thigh (floor holds
            // −0.25, thigh cannot walk further). 23:47 can now keep
            // ThighHold on Fall's first air; without room past 16° the
            // fold still scrapes and that millimetre replants. 24° is
            // still far from sci_a 40°. Flag 0 is the former 16°.
            if (stepPhaseDriver.fallSwingFallHipLiftCapWide > 0.5f)
            {
                hipLiftCapDeg = 24f;
                // 24° cap has room=0 once 00:09 walked Plant leftover into
                // 24° (floor holds −0.25, thigh cannot walk further). 32° is
                // the couple ceiling already on this path — not sci_a 40°.
                // Flag 0 is the former 24°.
                if (stepPhaseDriver.fallSwingFallHipLiftCapCeil > 0.5f)
                    hipLiftCapDeg = 32f;
            }
        }
        else if (fallCatchWalk
            && fallCatchPhaseCode == (int)StepPhaseDriver.FallCatchPhase.Reach
            && stepPhaseDriver.fallSwingReachHipLiftCap > 0.5f
            && !stepPhaseDriver.ReachResumedFromScrape)
        {
            hipLiftCapDeg = 16f;
            // 16° cap has room=0 once 23:53 walked Fall into 24° (floor
            // holds −0.25, thigh drops ~8° on Fall→Reach, sole replants).
            // 24° is still far from sci_a 40°. Flag 0 is the former 16°.
            if (stepPhaseDriver.fallSwingReachHipLiftCapWide > 0.5f)
            {
                hipLiftCapDeg = 24f;
                // 24° cap has room=0 once 00:16 walked Fall into 32° (floor
                // holds −0.25, thigh drops ~8° on Fall→Reach, sole replants).
                // 32° is the couple ceiling already on this path — not sci_a 40°.
                // Flag 0 is the former 24°.
                if (stepPhaseDriver.fallSwingReachHipLiftCapCeil > 0.5f)
                    hipLiftCapDeg = 32f;
            }
        }
        else if (fallCatchWalk
            && fallCatchPhaseCode == (int)StepPhaseDriver.FallCatchPhase.Reach
            && stepPhaseDriver.fallSwingCatchReachScrapeHipLiftCap > 0.5f
            && stepPhaseDriver.ReachResumedFromScrape)
        {
            hipLiftCapDeg = 16f;
            // 16° cap has room=0 at a 16° Catch-scrape thigh (floor holds
            // −0.25, thigh cannot walk further). 24° is still far from
            // sci_a 40°. Flag 0 is the former 16°.
            if (stepPhaseDriver.fallSwingCatchReachScrapeHipLiftCapWide > 0.5f)
            {
                hipLiftCapDeg = 24f;
                // 24° cap has room=0 once 00:36 walked Catch scrape into
                // 32° (floor holds −0.25, thigh drops ~8° on Catch→Reach
                // scrape, sole replants). 32° is the couple ceiling already
                // on this path — not sci_a 40°. Flag 0 is the former 24°.
                if (stepPhaseDriver.fallSwingCatchReachScrapeHipLiftCapCeil > 0.5f)
                    hipLiftCapDeg = 32f;
            }
        }
        else if (fallCatchWalk
            && fallCatchPhaseCode == (int)StepPhaseDriver.FallCatchPhase.Catch
            && stepPhaseDriver.fallSwingCatchAirHipLiftCap > 0.5f
            && !stepPhaseDriver.SwingFootIsGrounded)
        {
            hipLiftCapDeg = 16f;
            // 16° cap has room=0 once 23:58 walked Reach into 24° (floor
            // holds −0.25, thigh drops ~8° on Reach→Catch, sole replants).
            // 24° is still far from sci_a 40°. Flag 0 is the former 16°.
            if (stepPhaseDriver.fallSwingCatchAirHipLiftCapWide > 0.5f)
            {
                hipLiftCapDeg = 24f;
                // 24° cap has room=0 once 00:25 walked Reach into 32° (floor
                // holds −0.25, thigh drops ~8° on Reach→Catch, sole replants).
                // 32° is the couple ceiling already on this path — not sci_a 40°.
                // Flag 0 is the former 24°.
                // 00:41 raised this cap, but never-scraped timeout air still
                // took hipair (keepThighHoldAir was scrape-only);
                // fallSwingCatchAirHold is the keep, not another cap.
                // 00:42 is that keep; Catch→Reach bounce still took hipair
                // (reachAirHold excludes ReachResumedFromCatch);
                // fallSwingCatchAirEnterHold is the bounce keep.
                if (stepPhaseDriver.fallSwingCatchAirHipLiftCapCeil > 0.5f)
                    hipLiftCapDeg = 32f;
            }
        }
        else if (fallCatchWalk
            && fallCatchPhaseCode == (int)StepPhaseDriver.FallCatchPhase.Catch
            && stepPhaseDriver.fallSwingCatchScrapeHipLiftCap > 0.5f
            && stepPhaseDriver.SwingFootIsGrounded)
        {
            hipLiftCapDeg = 16f;
            // 16° cap has room=0 at a 16° Fall/Reach/Catch-air thigh
            // (floor holds −0.25, thigh cannot walk further). 24° is
            // still far from sci_a 40°. Flag 0 is the former 16°.
            if (stepPhaseDriver.fallSwingCatchScrapeHipLiftCapWide > 0.5f)
            {
                hipLiftCapDeg = 24f;
                // 24° cap has room=0 once 00:28 walked Catch-air into 32°
                // (floor holds −0.25, thigh drops ~8° on scrape, sole
                // replants). 32° is the couple ceiling already on this
                // path — not sci_a 40°. Flag 0 is the former 24°.
                if (stepPhaseDriver.fallSwingCatchScrapeHipLiftCapCeil > 0.5f)
                    hipLiftCapDeg = 32f;
            }
        }
        else if (fallCatchWalk
            && fallCatchPhaseCode == (int)StepPhaseDriver.FallCatchPhase.Plant
            && walkPlantSwingKnee > 0.001f
            && fc.hipLift > 0.001f)
        {
            hipLiftCapDeg = 16f;
            // 16° cap has room=0 at a 16° Plant leftover thigh (floor
            // holds −0.25, thigh cannot walk further). 00:03 can now keep
            // 24° in Catch-air; without room past 16° the fold still
            // scrapes and Fall→Reach waits on air. 24° is still far
            // from sci_a 40°. Flag 0 is the former 16°.
            if (stepPhaseDriver.fallSwingPlantHipLiftCapWide > 0.5f)
            {
                hipLiftCapDeg = 24f;
                // 24° cap has room=0 once leftover + hipLift 0.5 sit there
                // (floor holds −0.25, thigh cannot walk further). 32° is
                // the couple ceiling already on this path — not sci_a 40°.
                // Flag 0 is the former 24°.
                if (stepPhaseDriver.fallSwingPlantHipLiftCapCeil > 0.5f)
                    hipLiftCapDeg = 32f;
            }
            // 00:09 can now keep 24° in Plant. hipLift 0.5 walks that gap,
            // so Fall arrives at 24°; without room past 24° the fold still
            // scrapes. Fall 32° is fallSwingFallHipLiftCapCeil. 00:28 can
            // now keep 32° in Catch-air; Plant 32° is
            // fallSwingPlantHipLiftCapCeil.
            // That 32° Plant fold can now make Plant's own first air;
            // hipair still took −swingHipFlex there (Fall's hold is
            // Fall-only). fallSwingPlantAirHold is the keep, not another
            // cap. That Plant-air leftover into Reach still ramped
            // reachAirSwing to −swingHipFlex; fallSwingReachAirSwingCeil
            // clamps the ramp to coupleCapCeilingDeg. coupleCapCeilingDeg
            // stays 32°.
        }
        float hipLiftFloor = 0f;
        if (fallCatchWalk
            && fallCatchPhaseCode == (int)StepPhaseDriver.FallCatchPhase.Fall
            && stepPhaseDriver.fallSwingFallHipLiftCapHold > 0.5f)
            hipLiftFloor = 0.5f;
        else if (fallCatchWalk
            && fallCatchPhaseCode == (int)StepPhaseDriver.FallCatchPhase.Reach
            && stepPhaseDriver.fallSwingReachHipLiftCapHold > 0.5f
            && !stepPhaseDriver.ReachResumedFromScrape)
            hipLiftFloor = 0.5f;
        else if (fallCatchWalk
            && fallCatchPhaseCode == (int)StepPhaseDriver.FallCatchPhase.Reach
            && stepPhaseDriver.fallSwingCatchReachScrapeHipLiftCapHold > 0.5f
            && stepPhaseDriver.ReachResumedFromScrape)
            hipLiftFloor = 0.5f;
        else if (fallCatchWalk
            && fallCatchPhaseCode == (int)StepPhaseDriver.FallCatchPhase.Catch
            && stepPhaseDriver.fallSwingCatchAirHipLiftCapHold > 0.5f
            && !stepPhaseDriver.SwingFootIsGrounded)
            hipLiftFloor = 0.5f;
        else if (fallCatchWalk
            && fallCatchPhaseCode == (int)StepPhaseDriver.FallCatchPhase.Catch
            && stepPhaseDriver.fallSwingCatchScrapeHipLiftCapHold > 0.5f
            && stepPhaseDriver.SwingFootIsGrounded)
            hipLiftFloor = 0.5f;
        else if (fallCatchWalk
            && fallCatchPhaseCode == (int)StepPhaseDriver.FallCatchPhase.Plant
            && walkPlantSwingKnee > 0.001f
            && fc.hipLift > 0.001f)
            hipLiftFloor = 0.5f;
        // walkPlantFallLiftFloorSeedFirst above. Lift seed put hipLift
        // at 0.625; this floor stayed 0.5, so at the 32° leftover the
        // extra flexor is halved (liftSignal −0.3125 vs −0.391). Same
        // 0.25 grow. Only while the swing sole is down and the Fall/
        // Reach hold already armed a floor — a 0 floor is P=0 at the
        // cap (flag 0 of CapHold), do not invent one. Air keeps 0.5.
        // Catch / later cycles keep the envelope.
        if (walkPlantFallLiftFloorSeedFirst > 0.5f
            && walkPlantFallLiftSeedFirst > 0.5f
            && walkPlantFallFoldSeedFirst > 0.5f
            && walkPlantFallFoldGrowFirst > 0.5f
            && hipLiftFloor > 0.001f
            && (fallCatchPhaseCode == (int)StepPhaseDriver.FallCatchPhase.Fall
                || fallCatchPhaseCode == (int)StepPhaseDriver.FallCatchPhase.Reach)
            && stepPhaseDriver != null
            && stepPhaseDriver.FallCatchCycleCount <= 0
            && !stepPhaseDriver.StandingRestore
            && !stepPhaseDriver.StandingSurrender
            && plantSwingGroundedNow
            && intent != null && Mathf.Abs(intent.moveX) > 0.01f)
        {
            const float growT = 0.25f;
            hipLiftFloor = Mathf.Max(
                hipLiftFloor,
                Mathf.Lerp(hipLiftFloor, 1f, growT));
            // walkPlantFallLiftGrowFirst above. Floor seed put 0.625;
            // the pair grow is 0.719. Match the floor so leftover 32°
            // holds −0.517, not −0.719·0.625. Same 0.25. Air keeps 0.625
            // (this block is plantSwingGroundedNow). Flag 0 keeps 0.625.
            if (walkPlantFallLiftGrowFirst > 0.5f)
            {
                hipLiftFloor = Mathf.Max(
                    hipLiftFloor,
                    Mathf.Lerp(hipLiftFloor, 1f, growT));
                // walkPlantFallLiftGrowMoreFirst above. GrowFirst put
                // 0.719. Match the floor so leftover 32° holds −0.623,
                // not −0.789·0.719. Same 0.25. Air keeps 0.719 (this
                // block is plantSwingGroundedNow). Flag 0 keeps 0.719.
                if (walkPlantFallLiftGrowMoreFirst > 0.5f)
                {
                    hipLiftFloor = Mathf.Max(
                        hipLiftFloor,
                        Mathf.Lerp(hipLiftFloor, 1f, growT));
                    // walkPlantFallLiftGrowYetFirst above. GrowMore put
                    // 0.789. Match the floor so leftover 32° holds −0.709,
                    // not −0.842·0.789. Same 0.25. Air keeps 0.789 (this
                    // block is plantSwingGroundedNow). Flag 0 keeps 0.789.
                    if (walkPlantFallLiftGrowYetFirst > 0.5f)
                    {
                        hipLiftFloor = Mathf.Max(
                            hipLiftFloor,
                            Mathf.Lerp(hipLiftFloor, 1f, growT));
                        // walkPlantFallLiftGrowEvenFirst above. GrowYet put
                        // 0.842. Match the floor so leftover 32° holds −0.778,
                        // not −0.882·0.842. Same 0.25. Air keeps 0.842 (this
                        // block is plantSwingGroundedNow). Flag 0 keeps 0.842.
                        if (walkPlantFallLiftGrowEvenFirst > 0.5f)
                        {
                            hipLiftFloor = Mathf.Max(
                                hipLiftFloor,
                                Mathf.Lerp(hipLiftFloor, 1f, growT));
                            // walkPlantFallLiftGrowStillFirst above. GrowEven put
                            // 0.882. Match the floor so leftover 32° holds −0.831,
                            // not −0.912·0.882. Same 0.25. Air keeps 0.882 (this
                            // block is plantSwingGroundedNow). Flag 0 keeps 0.882.
                            if (walkPlantFallLiftGrowStillFirst > 0.5f)
                            {
                                hipLiftFloor = Mathf.Max(
                                    hipLiftFloor,
                                    Mathf.Lerp(hipLiftFloor, 1f, growT));
                                // walkPlantFallLiftGrowAgainFirst above. GrowStill put
                                // 0.912. Match the floor so leftover 32° holds −0.872,
                                // not −0.934·0.912. Same 0.25. Air keeps 0.912 (this
                                // block is plantSwingGroundedNow). Flag 0 keeps 0.912.
                                if (walkPlantFallLiftGrowAgainFirst > 0.5f)
                                {
                                    hipLiftFloor = Mathf.Max(
                                        hipLiftFloor,
                                        Mathf.Lerp(hipLiftFloor, 1f, growT));
                                    // walkPlantFallLiftGrowAlsoFirst above. GrowAgain put
                                    // 0.934. Match the floor so leftover 32° holds −0.904,
                                    // not −0.951·0.934. Same 0.25. Air keeps 0.934 (this
                                    // block is plantSwingGroundedNow). Flag 0 keeps 0.934.
                                    if (walkPlantFallLiftGrowAlsoFirst > 0.5f)
                                    {
                                        hipLiftFloor = Mathf.Max(
                                            hipLiftFloor,
                                            Mathf.Lerp(hipLiftFloor, 1f, growT));
                                        // walkPlantFallLiftGrowExtraFirst above. GrowAlso put
                                        // 0.951. Match the floor so leftover 32° holds −0.927,
                                        // not −0.963·0.951. Same 0.25. Air keeps 0.951 (this
                                        // block is plantSwingGroundedNow). Flag 0 keeps 0.951.
                                        if (walkPlantFallLiftGrowExtraFirst > 0.5f)
                                        {
                                            hipLiftFloor = Mathf.Max(
                                                hipLiftFloor,
                                                Mathf.Lerp(hipLiftFloor, 1f, growT));
                                            // walkPlantFallLiftGrowPlusFirst above. GrowExtra put
                                            // 0.963. Match the floor so leftover 32° holds −0.945,
                                            // not −0.972·0.963. Same 0.25. Air keeps 0.963 (this
                                            // block is plantSwingGroundedNow). Flag 0 keeps 0.963.
                                            if (walkPlantFallLiftGrowPlusFirst > 0.5f)
                                            {
                                                hipLiftFloor = Mathf.Max(
                                                    hipLiftFloor,
                                                    Mathf.Lerp(hipLiftFloor, 1f, growT));
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
        // The cap bounds the third-law shove, and this step the stance hip is
        // paying that shove back (liftCoupleActive). Widen it so the thigh has
        // room to actually raise the knee, hard-clamped well short of the
        // 40 deg scissor pose that walked the trunk backwards.
        // Same 32° as fallSwingReachAirSwingCeil: the Reach placement
        // ramp must not walk past this into −swingHipFlex / sci_a.
        const float coupleCapCeilingDeg = 32f;
        if (liftCoupleActive && walkSwingLiftCoupleCapScale > 1.001f)
            hipLiftCapDeg = Mathf.Min(coupleCapCeilingDeg,
                hipLiftCapDeg * walkSwingLiftCoupleCapScale);
        // walkPlantFallLiftCapGrowFirst above. PlusFirst holds leftover
        // 32° at −0.945 (room ≤ 0). Extra hipLift does not raise past
        // CapCeil, and the couple Min above would clip a 34° write
        // back to 32°. Same 0.25 of the 8° to sci_a so the thigh has
        // 2° of room (Lerp 32→40 = 34). Flag 0 keeps 32°. Air keeps
        // 32° (this block is plantSwingGroundedNow). Catch / later
        // cycles keep the envelope.
        if (walkPlantFallLiftCapGrowFirst > 0.5f
            && walkPlantFallLiftGrowPlusFirst > 0.5f
            && walkPlantFallLiftFloorSeedFirst > 0.5f
            && walkPlantFallLiftSeedFirst > 0.5f
            && walkPlantFallFoldSeedFirst > 0.5f
            && walkPlantFallFoldGrowFirst > 0.5f
            && hipLiftCapDeg > 31.5f
            && (fallCatchPhaseCode == (int)StepPhaseDriver.FallCatchPhase.Fall
                || fallCatchPhaseCode == (int)StepPhaseDriver.FallCatchPhase.Reach)
            && stepPhaseDriver != null
            && stepPhaseDriver.FallCatchCycleCount <= 0
            && !stepPhaseDriver.StandingRestore
            && !stepPhaseDriver.StandingSurrender
            && plantSwingGroundedNow
            && intent != null && Mathf.Abs(intent.moveX) > 0.01f)
        {
            const float growT = 0.25f;
            const float sciADeg = 40f;
            hipLiftCapDeg = Mathf.Max(
                hipLiftCapDeg,
                Mathf.Lerp(coupleCapCeilingDeg, sciADeg, growT));
            // walkPlantFallLiftCapGrowMoreFirst above. CapGrowFirst wrote
            // leftover 34°. Extra hipLift at room ≤ 0 does not raise past
            // that cap. Same 0.25 of the remaining 6° to sci_a so the
            // thigh has 1.5° of room (Lerp 34→40 = 35.5). Flag 0 keeps
            // 34°. Air keeps 34° (this block is plantSwingGroundedNow).
            // Catch / later cycles keep the envelope.
            if (walkPlantFallLiftCapGrowMoreFirst > 0.5f)
            {
                const float capGrowFirstDeg = 34f;
                hipLiftCapDeg = Mathf.Max(
                    hipLiftCapDeg,
                    Mathf.Lerp(capGrowFirstDeg, sciADeg, growT));
                // walkPlantFallLiftCapGrowYetFirst above. CapGrowMoreFirst
                // wrote leftover 35.5°. Extra hipLift at room ≤ 0 does
                // not raise past that cap. Same 0.25 of the remaining
                // 4.5° to sci_a so the thigh has 1.125° of room (Lerp
                // 35.5→40 = 36.625). Flag 0 keeps 35.5°. Air keeps
                // 35.5° (this block is plantSwingGroundedNow). Catch /
                // later cycles keep the envelope.
                if (walkPlantFallLiftCapGrowYetFirst > 0.5f)
                {
                    const float capGrowMoreFirstDeg = 35.5f;
                    hipLiftCapDeg = Mathf.Max(
                        hipLiftCapDeg,
                        Mathf.Lerp(capGrowMoreFirstDeg, sciADeg, growT));
                    // walkPlantFallLiftCapGrowEvenFirst above. CapGrowYetFirst
                    // wrote leftover 36.625°. Extra hipLift at room ≤ 0
                    // does not raise past that cap. Same 0.25 of the
                    // remaining 3.375° to sci_a so the thigh has
                    // 0.84375° of room (Lerp 36.625→40 = 37.46875). Flag
                    // 0 keeps 36.625°. Air keeps 36.625° (this block is
                    // plantSwingGroundedNow). Catch / later cycles keep
                    // the envelope.
                    if (walkPlantFallLiftCapGrowEvenFirst > 0.5f)
                    {
                        const float capGrowYetFirstDeg = 36.625f;
                        hipLiftCapDeg = Mathf.Max(
                            hipLiftCapDeg,
                            Mathf.Lerp(capGrowYetFirstDeg, sciADeg, growT));
                        // walkPlantFallLiftCapGrowStillFirst above.
                        // CapGrowEvenFirst wrote leftover 37.46875°.
                        // Extra hipLift at room ≤ 0 does not raise past
                        // that cap. Same 0.25 of the remaining 2.53125°
                        // to sci_a so the thigh has 0.6328125° of room
                        // (Lerp 37.46875→40 = 38.1015625). Flag 0 keeps
                        // 37.46875°. Air keeps 37.46875° (this block is
                        // plantSwingGroundedNow). Catch / later cycles
                        // keep the envelope.
                        if (walkPlantFallLiftCapGrowStillFirst > 0.5f)
                        {
                            const float capGrowEvenFirstDeg = 37.46875f;
                            hipLiftCapDeg = Mathf.Max(
                                hipLiftCapDeg,
                                Mathf.Lerp(capGrowEvenFirstDeg, sciADeg, growT));
                            // walkPlantFallLiftCapGrowAgainFirst above.
                            // CapGrowStillFirst wrote leftover
                            // 38.1015625°. Extra hipLift at room ≤ 0
                            // does not raise past that cap. Same 0.25
                            // of the remaining 1.8984375° to sci_a so
                            // the thigh has 0.474609375° of room (Lerp
                            // 38.1015625→40 = 38.576171875). Flag 0
                            // keeps 38.1015625°. Air keeps 38.1015625°
                            // (this block is plantSwingGroundedNow).
                            // Catch / later cycles keep the envelope.
                            if (walkPlantFallLiftCapGrowAgainFirst > 0.5f)
                            {
                                const float capGrowStillFirstDeg = 38.1015625f;
                                hipLiftCapDeg = Mathf.Max(
                                    hipLiftCapDeg,
                                    Mathf.Lerp(capGrowStillFirstDeg, sciADeg, growT));
                                // walkPlantFallLiftCapGrowAlsoFirst above.
                                // CapGrowAgainFirst wrote leftover
                                // 38.576171875°. Extra hipLift at room ≤ 0
                                // does not raise past that cap. Same 0.25
                                // of the remaining 1.423828125° to sci_a so
                                // the thigh has 0.35595703125° of room
                                // (Lerp 38.576171875→40 = 38.93212890625).
                                // Flag 0 keeps 38.576171875°. Air keeps
                                // 38.576171875° (this block is
                                // plantSwingGroundedNow). Catch / later
                                // cycles keep the envelope.
                                if (walkPlantFallLiftCapGrowAlsoFirst > 0.5f)
                                {
                                    const float capGrowAgainFirstDeg = 38.576171875f;
                                    hipLiftCapDeg = Mathf.Max(
                                        hipLiftCapDeg,
                                        Mathf.Lerp(capGrowAgainFirstDeg, sciADeg, growT));
                                    // walkPlantFallLiftCapGrowExtraFirst above.
                                    // CapGrowAlsoFirst wrote leftover
                                    // 38.93212890625°. Extra hipLift at room ≤ 0
                                    // does not raise past that cap. Same 0.25
                                    // of the remaining 1.06787109375° to sci_a so
                                    // the thigh has 0.2669677734375° of room
                                    // (Lerp 38.93212890625→40 = 39.1990966796875).
                                    // Flag 0 keeps 38.93212890625°. Air keeps
                                    // 38.93212890625° (this block is
                                    // plantSwingGroundedNow). Catch / later
                                    // cycles keep the envelope.
                                    if (walkPlantFallLiftCapGrowExtraFirst > 0.5f)
                                    {
                                        const float capGrowAlsoFirstDeg = 38.93212890625f;
                                        hipLiftCapDeg = Mathf.Max(
                                            hipLiftCapDeg,
                                            Mathf.Lerp(capGrowAlsoFirstDeg, sciADeg, growT));
                                        // walkPlantFallLiftCapGrowPlusFirst above.
                                        // CapGrowExtraFirst wrote leftover
                                        // 39.1990966796875°. Extra hipLift at room ≤ 0
                                        // does not raise past that cap. Same 0.25
                                        // of the remaining 0.8009033203125° to sci_a so
                                        // the thigh has 0.200225830078125° of room
                                        // (Lerp 39.1990966796875→40 = 39.399322509765625).
                                        // Flag 0 keeps 39.1990966796875°. Air keeps
                                        // 39.1990966796875° (this block is
                                        // plantSwingGroundedNow). Catch / later
                                        // cycles keep the envelope.
                                        if (walkPlantFallLiftCapGrowPlusFirst > 0.5f)
                                        {
                                            const float capGrowExtraFirstDeg = 39.1990966796875f;
                                            hipLiftCapDeg = Mathf.Max(
                                                hipLiftCapDeg,
                                                Mathf.Lerp(capGrowExtraFirstDeg, sciADeg, growT));
                                            // walkPlantFallLiftCapGrowFurtherFirst above.
                                            // CapGrowPlusFirst wrote leftover
                                            // 39.399322509765625°. Extra hipLift at room ≤ 0
                                            // does not raise past that cap. Same 0.25
                                            // of the remaining 0.600677490234375° to sci_a so
                                            // the thigh has 0.15016937255859375° of room
                                            // (Lerp 39.399322509765625→40 = 39.54949188232421875).
                                            // Flag 0 keeps 39.399322509765625°. Air keeps
                                            // 39.399322509765625° (this block is
                                            // plantSwingGroundedNow). Catch / later
                                            // cycles keep the envelope.
                                            if (walkPlantFallLiftCapGrowFurtherFirst > 0.5f)
                                            {
                                                const float capGrowPlusFirstDeg = 39.399322509765625f;
                                                hipLiftCapDeg = Mathf.Max(
                                                    hipLiftCapDeg,
                                                    Mathf.Lerp(capGrowPlusFirstDeg, sciADeg, growT));
                                                // walkPlantFallLiftCapGrowOnwardFirst above.
                                                // CapGrowFurtherFirst wrote leftover
                                                // 39.54949188232421875°. Extra hipLift at room ≤ 0
                                                // does not raise past that cap. Same 0.25
                                                // of the remaining 0.45050811767578125° to sci_a so
                                                // the thigh has 0.1126270294189453125° of room
                                                // (Lerp 39.54949188232421875→40 = 39.6621189117431640625).
                                                // Flag 0 keeps 39.54949188232421875°. Air keeps
                                                // 39.54949188232421875° (this block is
                                                // plantSwingGroundedNow). Catch / later
                                                // cycles keep the envelope.
                                                if (walkPlantFallLiftCapGrowOnwardFirst > 0.5f)
                                                {
                                                    const float capGrowFurtherFirstDeg = 39.54949188232421875f;
                                                    hipLiftCapDeg = Mathf.Max(
                                                        hipLiftCapDeg,
                                                        Mathf.Lerp(capGrowFurtherFirstDeg, sciADeg, growT));
                                                    // walkPlantFallLiftCapGrowAheadFirst above.
                                                    // CapGrowOnwardFirst wrote leftover
                                                    // 39.6621189117431640625°. Extra hipLift at room ≤ 0
                                                    // does not raise past that cap. Same 0.25
                                                    // of the remaining 0.3378810882568359375° to sci_a so
                                                    // the thigh has 0.084470272064208984375° of room
                                                    // (Lerp 39.6621189117431640625→40 = 39.746589183807373046875).
                                                    // Flag 0 keeps 39.6621189117431640625°. Air keeps
                                                    // 39.6621189117431640625° (this block is
                                                    // plantSwingGroundedNow). Catch / later
                                                    // cycles keep the envelope.
                                                    if (walkPlantFallLiftCapGrowAheadFirst > 0.5f)
                                                    {
                                                        const float capGrowOnwardFirstDeg = 39.6621189117431640625f;
                                                        hipLiftCapDeg = Mathf.Max(
                                                            hipLiftCapDeg,
                                                            Mathf.Lerp(capGrowOnwardFirstDeg, sciADeg, growT));
                                                        // walkPlantFallLiftCapGrowAlongFirst above.
                                                        // CapGrowAheadFirst wrote leftover
                                                        // 39.746589183807373046875°. Extra hipLift at room ≤ 0
                                                        // does not raise past that cap. Same 0.25
                                                        // of the remaining 0.253410816192626953125° to sci_a so
                                                        // the thigh has 0.06335270404815673828125° of room
                                                        // (Lerp 39.746589183807373046875→40 = 39.80994188785552978515625).
                                                        // Flag 0 keeps 39.746589183807373046875°. Air keeps
                                                        // 39.746589183807373046875° (this block is
                                                        // plantSwingGroundedNow). Catch / later
                                                        // cycles keep the envelope.
                                                        if (walkPlantFallLiftCapGrowAlongFirst > 0.5f)
                                                        {
                                                            const float capGrowAheadFirstDeg = 39.746589183807373046875f;
                                                            hipLiftCapDeg = Mathf.Max(
                                                                hipLiftCapDeg,
                                                                Mathf.Lerp(capGrowAheadFirstDeg, sciADeg, growT));
                                                            // walkPlantFallLiftCapGrowForthFirst above.
                                                            // CapGrowAlongFirst wrote leftover
                                                            // 39.80994188785552978515625°. Extra hipLift at room ≤ 0
                                                            // does not raise past that cap. Same 0.25
                                                            // of the remaining 0.19005811214447021484375° to sci_a so
                                                            // the thigh has 0.0475145280361175537109375° of room
                                                            // (Lerp 39.80994188785552978515625→40 = 39.8574564158916473388671875).
                                                            // Flag 0 keeps 39.80994188785552978515625°. Air keeps
                                                            // 39.80994188785552978515625° (this block is
                                                            // plantSwingGroundedNow). Catch / later
                                                            // cycles keep the envelope.
                                                            if (walkPlantFallLiftCapGrowForthFirst > 0.5f)
                                                            {
                                                                const float capGrowAlongFirstDeg = 39.80994188785552978515625f;
                                                                hipLiftCapDeg = Mathf.Max(
                                                                    hipLiftCapDeg,
                                                                    Mathf.Lerp(capGrowAlongFirstDeg, sciADeg, growT));
                                                                // walkPlantFallLiftCapGrowBeyondFirst above.
                                                                // CapGrowForthFirst wrote leftover
                                                                // 39.8574564158916473388671875°. Extra hipLift at room ≤ 0
                                                                // does not raise past that cap. Same 0.25
                                                                // of the remaining 0.1425435841083526611328125° to sci_a so
                                                                // the thigh has 0.035635896027088165283203125° of room
                                                                // (Lerp 39.8574564158916473388671875→40 = 39.893092311918735504150390625).
                                                                // Flag 0 keeps 39.8574564158916473388671875°. Air keeps
                                                                // 39.8574564158916473388671875° (this block is
                                                                // plantSwingGroundedNow). Catch / later
                                                                // cycles keep the envelope.
                                                                if (walkPlantFallLiftCapGrowBeyondFirst > 0.5f)
                                                                {
                                                                    const float capGrowForthFirstDeg = 39.8574564158916473388671875f;
                                                                    hipLiftCapDeg = Mathf.Max(
                                                                        hipLiftCapDeg,
                                                                        Mathf.Lerp(capGrowForthFirstDeg, sciADeg, growT));
                                                                    // walkPlantFallLiftCapGrowPastFirst above.
                                                                    // CapGrowBeyondFirst wrote leftover
                                                                    // 39.893092311918735504150390625°. Extra hipLift at room ≤ 0
                                                                    // does not raise past that cap. Same 0.25
                                                                    // of the remaining 0.106907688081264495849609375° to sci_a so
                                                                    // the thigh has 0.02672692202031612396240234375° of room
                                                                    // (Lerp 39.893092311918735504150390625→40 = 39.91981923393905162811279296875).
                                                                    // Flag 0 keeps 39.893092311918735504150390625°. Air keeps
                                                                    // 39.893092311918735504150390625° (this block is
                                                                    // plantSwingGroundedNow). Catch / later
                                                                    // cycles keep the envelope.
                                                                    if (walkPlantFallLiftCapGrowPastFirst > 0.5f)
                                                                    {
                                                                        const float capGrowBeyondFirstDeg = 39.893092311918735504150390625f;
                                                                        hipLiftCapDeg = Mathf.Max(
                                                                            hipLiftCapDeg,
                                                                            Mathf.Lerp(capGrowBeyondFirstDeg, sciADeg, growT));
                                                                        // walkPlantFallLiftCapGrowOverFirst above.
                                                                        // CapGrowPastFirst wrote leftover
                                                                        // 39.91981923393905162811279296875°. Extra hipLift at room ≤ 0
                                                                        // does not raise past that cap. Same 0.25
                                                                        // of the remaining 0.08018076606094837188720703125° to sci_a so
                                                                        // the thigh has 0.0200451915152370929718017578125° of room
                                                                        // (Lerp 39.91981923393905162811279296875→40 = 39.9398644254542887210845947265625).
                                                                        // Flag 0 keeps 39.91981923393905162811279296875°. Air keeps
                                                                        // 39.91981923393905162811279296875° (this block is
                                                                        // plantSwingGroundedNow). Catch / later
                                                                        // cycles keep the envelope.
                                                                        if (walkPlantFallLiftCapGrowOverFirst > 0.5f)
                                                                        {
                                                                            const float capGrowPastFirstDeg = 39.91981923393905162811279296875f;
                                                                            hipLiftCapDeg = Mathf.Max(
                                                                                hipLiftCapDeg,
                                                                                Mathf.Lerp(capGrowPastFirstDeg, sciADeg, growT));
                                                                            // walkPlantFallLiftCapGrowAcrossFirst above.
                                                                            // CapGrowOverFirst wrote leftover
                                                                            // 39.9398644254542887210845947265625°. Extra hipLift at room ≤ 0
                                                                            // does not raise past that cap. Same 0.25
                                                                            // of the remaining 0.0601355745457112789154052734375° to sci_a so
                                                                            // the thigh has 0.015033893636427819728851318359375° of room
                                                                            // (Lerp 39.9398644254542887210845947265625→40 = 39.954898319090716540813446044921875).
                                                                            // Flag 0 keeps 39.9398644254542887210845947265625°. Air keeps
                                                                            // 39.9398644254542887210845947265625° (this block is
                                                                            // plantSwingGroundedNow). Catch / later
                                                                            // cycles keep the envelope.
                                                                            if (walkPlantFallLiftCapGrowAcrossFirst > 0.5f)
                                                                            {
                                                                                const float capGrowOverFirstDeg = 39.9398644254542887210845947265625f;
                                                                                hipLiftCapDeg = Mathf.Max(
                                                                                    hipLiftCapDeg,
                                                                                    Mathf.Lerp(capGrowOverFirstDeg, sciADeg, growT));
                                                                            }
                                                                        }
                                                                    }
                                                                }
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
        // Proportional band for the swing leg (walkSwingErrorReferenceDegrees
        // above). Stance keeps the shared reference: the loaded strut is not
        // tracking a geometric setpoint, and a softer approach there is a sag.
        // Gated on fallCatchWalk (which requires walkActive) plus the split, so
        // quiet stance, crouch, the pushes, cold one-leg and legacy
        // useFallCatchWalk=0 walk never reach this line.
        float swingErrorRef = errorReferenceDegrees;
        if (fallCatchWalk && splitLegs && walkMoveDir != 0f)
            swingErrorRef = Mathf.Max(errorReferenceDegrees, walkSwingErrorReferenceDegrees);
        // Signal the swing hip actually applied this step. The stance hip
        // cancels its pelvis reaction on the next one.
        float swingHipCommand = 0f;
        int swingHipSide = 0;
        // Open-chain catch-step: travelPlace already solved the hip that
        // puts the sole on desiredStep (capture + catchFootLead, ahead
        // of CoM). reachAirSwing would take that same target and Max()
        // it to −32°; at FirstStepLiftKneeDeg the shin then points back
        // and the foot cannot pass CoM. Dirt Plant/Fall still uses
        // ThighHold (firstStepHoldLift).
        bool catchStepPlaceHip = travelPlaceLight && placementSolved && !firstStepHoldLift;
        float walkLiftHipRef = Mathf.Max(errorReferenceDegrees, walkSwingErrorReferenceDegrees);
        // Cycle-0 air plant: hipair would take −swingHipFlex (32° cannot
        // reach). The 8° SPD runs only after the 55° lift has latched air.
        if (WalkLiftOwnsSwing() && leftIsSwing && !walkLiftDirtLift)
        {
            swingHipCommand = ControlJointToAngleStable(
                leftHipJoint, walkLiftHipTarget,
                hipPGain, hipDGain, walkLiftHipRef,
                leftHipFlexor, leftHipExtensor, ref leftHipEffectiveInertia);
            swingHipSide = -1;
        }
        else if (firstStepCatchPlant && leftIsSwing)
        {
            swingHipCommand = ControlJointToAngleStable(
                leftHipJoint, -FirstStepPlantHipDeg,
                hipPGain, hipDGain, swingErrorRef,
                leftHipFlexor, leftHipExtensor, ref leftHipEffectiveInertia);
            swingHipSide = -1;
        }
        else if (leftSwingHip && !WalkLiftOwnsSwing())
        {
            float target = !leftAir && scissorPose ? groundedHipTarget : latchedHipTarget;
            swingHipCommand = ControlJointToAngleStable(leftHipJoint, target,
                hipPGain, hipDGain, swingErrorRef,
                leftHipFlexor, leftHipExtensor, ref leftHipEffectiveInertia);
            swingHipSide = -1;
        }
        else if (reachAirSwing && leftIsSwing && !catchStepPlaceHip)
        {
            // Ramped reach replaces the upright hold for this window only. The
            // hip-lift caps are not needed here: they bound the ThighHold
            // shove, and the ramp bounds the same reaction by construction.
            // Catch-step: travelPlace already solved desiredStep (ahead of
            // CoM). This branch ramps to that target then Max()s it to −32°,
            // which with a 55° shin puts the ankle behind the hip.
            swingHipCommand = ControlJointToAngleStable(leftHipJoint, reachAirSwingTarget,
                hipPGain, hipDGain, swingErrorRef,
                leftHipFlexor, leftHipExtensor, ref leftHipEffectiveInertia);
            swingHipSide = -1;
        }
        else if (reachKneeFolding && leftIsSwing && (leftGrounded || keepThighHoldAir || firstStepRecoverHold || catchStepPlaceHip))
        {
            // walkStepPlaceOnFold: ThighHold already owns this hip (DrivePelvis
            // is off). Aim the capture-point landing instead of the 8–32° lift
            // cap, so the folded sole goes ahead of the CoM rather than under
            // the hip. hipairCeil still clamps to 32°. Cycle-0 firstStepHoldLift
            // keeps the 16–32° lift hip on Plant/Fall: the 4 cm solve is a
            // 3° unwind. Catch plant SPD is the first-step branch above.
            if (travelPlaceLight && placementSolved && !firstStepHoldLift)
            {
                swingHipCommand = ControlJointToAngleStable(leftHipJoint, latchedHipTarget,
                    hipPGain, hipDGain, swingErrorRef,
                    leftHipFlexor, leftHipExtensor, ref leftHipEffectiveInertia);
            }
            else
            {
                swingHipCommand = ApplyFallCatchSwingThighHold(
                    leftHipJoint, leftThighRb, leftHipFlexor, leftHipExtensor,
                    fc.thighHold, fc.hipLift, hipLiftCapDeg, hipLiftFloor);
            }
            swingHipSide = -1;
        }
        else if ((walkStanceGeoLift || walkLiftDirtLift) && leftIsSwing)
        {
            // Lift is the swing knee. Hip SPD into a planted sole is
            // the closed chain (hold −5° / scissor sci_a). JointFriction
            // holds the thigh; Unload already faded that weld.
            ApplySignal(0f, leftHipFlexor, leftHipExtensor);
            swingHipCommand = 0f;
            swingHipSide = -1;
        }
        else if (FirstWalkSwingYield() && leftIsSwing)
        {
            ApplySignal(0f, leftHipFlexor, leftHipExtensor);
            swingHipCommand = 0f;
            swingHipSide = -1;
        }
        if (WalkLiftOwnsSwing() && rightIsSwing && !walkLiftDirtLift)
        {
            swingHipCommand = ControlJointToAngleStable(
                rightHipJoint, walkLiftHipTarget,
                hipPGain, hipDGain, walkLiftHipRef,
                rightHipFlexor, rightHipExtensor, ref rightHipEffectiveInertia);
            swingHipSide = 1;
        }
        else if (firstStepCatchPlant && rightIsSwing)
        {
            swingHipCommand = ControlJointToAngleStable(
                rightHipJoint, -FirstStepPlantHipDeg,
                hipPGain, hipDGain, swingErrorRef,
                rightHipFlexor, rightHipExtensor, ref rightHipEffectiveInertia);
            swingHipSide = 1;
        }
        else if (rightSwingHip && !WalkLiftOwnsSwing())
        {
            float target = !rightAir && scissorPose ? groundedHipTarget : latchedHipTarget;
            swingHipCommand = ControlJointToAngleStable(rightHipJoint, target,
                hipPGain, hipDGain, swingErrorRef,
                rightHipFlexor, rightHipExtensor, ref rightHipEffectiveInertia);
            swingHipSide = 1;
        }
        else if (reachAirSwing && rightIsSwing && !catchStepPlaceHip)
        {
            swingHipCommand = ControlJointToAngleStable(rightHipJoint, reachAirSwingTarget,
                hipPGain, hipDGain, swingErrorRef,
                rightHipFlexor, rightHipExtensor, ref rightHipEffectiveInertia);
            swingHipSide = 1;
        }
        else if (reachKneeFolding && rightIsSwing && (rightGrounded || keepThighHoldAir || firstStepRecoverHold || catchStepPlaceHip))
        {
            if (travelPlaceLight && placementSolved && !firstStepHoldLift)
            {
                swingHipCommand = ControlJointToAngleStable(rightHipJoint, latchedHipTarget,
                    hipPGain, hipDGain, swingErrorRef,
                    rightHipFlexor, rightHipExtensor, ref rightHipEffectiveInertia);
            }
            else
            {
                swingHipCommand = ApplyFallCatchSwingThighHold(
                    rightHipJoint, rightThighRb, rightHipFlexor, rightHipExtensor,
                    fc.thighHold, fc.hipLift, hipLiftCapDeg, hipLiftFloor);
            }
            swingHipSide = 1;
        }
        else if ((walkStanceGeoLift || walkLiftDirtLift) && rightIsSwing)
        {
            ApplySignal(0f, rightHipFlexor, rightHipExtensor);
            swingHipCommand = 0f;
            swingHipSide = 1;
        }
        else if (FirstWalkSwingYield() && rightIsSwing)
        {
            ApplySignal(0f, rightHipFlexor, rightHipExtensor);
            swingHipCommand = 0f;
            swingHipSide = 1;
        }
        // Quiet stance, crouch and cold oneg leave both at zero, so the next
        // step's couple gate is closed and those paths stay bitwise.
        lastSwingHipCommand = walkSoft && splitLegs ? swingHipCommand : 0f;
        lastSwingHipSide = walkSoft && splitLegs ? swingHipSide : 0;

        // Keep the quiet-stance Recovery reflex off the loaded strut
        // (walkStanceKneeRecoveryFlex above). Only the fall-catch split gait
        // reads this: with the flag at 1, or with the reflex inactive, or on any
        // other path, stanceKneeTarget IS kneeTarget and nothing changes. The
        // stance overrides further down (vault, stancePlantKnee, stanceKneeBend)
        // still write on top of this — this only moves the number they fall
        // back to once the vault has yielded.
        float stanceKneeTarget = kneeTarget;
        if (fallCatchWalk && splitLegs && kneeTarget > kneeTargetNoRecovery + 0.0001f)
            stanceKneeTarget = Mathf.Lerp(
                kneeTargetNoRecovery,
                kneeTarget,
                Mathf.Clamp01(walkStanceKneeRecoveryFlex));

        // Bend the swing knee only after lift-off: while the foot is down the
        // chain is closed and swingKneeFlex folds the pelvis with the support.
        // After latch flex like the hip — else the leg never leaves support.
        float leftKnee = leftIsSwing ? kneeTarget : stanceKneeTarget;
        float rightKnee = rightIsSwing ? kneeTarget : stanceKneeTarget;
        float swingKneeBoost = Mathf.Clamp(
            walkSpeedError * Mathf.Max(0f, walkSpeedSwingKneeGain),
            0f,
            Mathf.Max(0f, walkSpeedSwingFlexMax));
        float baseSwingKneeBoost = walkSoft ? Mathf.Max(0f, walkSwingKneeBoost) : 0f;
        float swingKneeFlexEffective = swingKneeFlex + baseSwingKneeBoost + swingKneeBoost;
        if (splitLegs)
        {
            if (leftIsSwing)
            {
                float groundKnee = swingKneeGroundedFraction * liftSoft;
                if (standCmd > 0.5f)
                    groundKnee *= backSwingKneeGroundScale;
                float earlyActL = Mathf.Max(liftAct, liftGateBlend);
                if (walkSoft && !swingHipLatched && earlyActL > 0.001f && walkKneePeelMax > 0.001f)
                    groundKnee = Mathf.Max(groundKnee, earlyActL * walkKneePeelMax);
                if (walkSoft && FirstWalkRollOpen())
                    groundKnee = 0f;
                // Full flex only in the air. On latched&&grounded — kneeTarget
                // (no groundKnee): rearm of the fraction after a scrape gave swing 0.62 / drop 0.05.
                // March: 90° as soon as the clocked swing sole is up. Latch
                // was the closed loop with CoM extras. Quiet one-leg keeps latch.
                float marchClockKnee = intent != null ? intent.standLeg : 0f;
                if (Marching() && Mathf.Abs(marchClockKnee) > 0.5f && !leftGrounded)
                {
                    leftKnee = Mathf.Clamp(marchKneeFlex, 0f, 115f);
                }
                else if (swingHipLatched && !leftGrounded)
                {
                    leftKnee = Mathf.Clamp(
                        kneeBaseAngle + standLegLevel * swingKneeFlexEffective,
                        0f, 115f);
                }
                else if (!swingHipLatched)
                    leftKnee = Mathf.Clamp(
                        kneeTarget + standLegLevel * swingKneeFlexEffective * groundKnee,
                        0f, 115f);
            }
            if (rightIsSwing)
            {
                float groundKnee = swingKneeGroundedFraction * forwardSwingKneeGroundScale * liftSoft;
                float earlyActR = Mathf.Max(liftAct, liftGateBlend);
                if (walkSoft && !swingHipLatched && earlyActR > 0.001f && walkKneePeelMax > 0.001f)
                    groundKnee = Mathf.Max(groundKnee, earlyActR * walkKneePeelMax);
                if (walkSoft && FirstWalkRollOpen())
                    groundKnee = 0f;
                if (Marching() && Mathf.Abs(intent != null ? intent.standLeg : 0f) > 0.5f
                    && !rightGrounded)
                {
                    rightKnee = Mathf.Clamp(marchKneeFlex, 0f, 115f);
                }
                else if (swingHipLatched && !rightGrounded)
                {
                    rightKnee = Mathf.Clamp(
                        kneeBaseAngle + standLegLevel * swingKneeFlexEffective,
                        0f, 115f);
                }
                else if (!swingHipLatched)
                    rightKnee = Mathf.Clamp(
                        kneeTarget + standLegLevel * swingKneeFlexEffective * groundKnee,
                        0f, 115f);
            }
            // Vault: straighten the stance knee with weightBlend — vertical unload.
            if (!fallCatchWalk && walkStanceExtend > 0.001f && weightBlend > 0.001f)
            {
                float ext = Mathf.Clamp01(walkStanceExtend * weightBlend);
                if (!leftIsSwing)
                    leftKnee = Mathf.Lerp(leftKnee, 0f, ext);
                if (!rightIsSwing)
                    rightKnee = Mathf.Lerp(rightKnee, 0f, ext);
            }
            // WALK-2 Fall/Reach: full swing-knee even while grounded. The
            // previous gate (!grounded × standLegLevel) was a closed loop —
            // Fall waits for air, air needs this flex — so swingFoot stayed
            // 0.9976 and Reach almost never started. Degrees, not a fraction
            // of the 8 s lift ramp: fall-catch cycles are a few tenths of a second.
            // Cycle-0 dirt: firstStepGroundedDrive keeps DrivePelvis (no
            // hipLift / DualLoad / +10° back — that triad is GRF −X).
            // Knee-only still has to run: the helper zeros fold while
            // StanceLead < 0 and caps at FirstStepLiftKneeDeg after it.
            // Gating the shin on that same flag left reachKnee 0 at 23.5°
            // lean, walkSwap 0. Later cycles and airborne cycle-0 keep
            // the former !firstStepGroundedDrive path.
            if (fallCatchWalk && fc.reachKnee > 0.5f
                && (!firstStepGroundedDrive || fc.hipLift <= 0.001f))
            {
                // Joint stop is 0…+120. The 115 clamp was a quiet-stance
                // margin; it threw away the last 25° of reachKneeFlex 140,
                // so the shin never shortened enough to lift a loaded foot.
                if (leftIsSwing)
                    leftKnee = Mathf.Clamp(kneeBaseAngle + fc.reachKnee, 0f, 120f);
                if (rightIsSwing)
                    rightKnee = Mathf.Clamp(kneeBaseAngle + fc.reachKnee, 0f, 120f);
            }
            // Post-swap Plant / ramped Fall vault: the new stance is the
            // catch foot at leftover (landDeg once that lerp arrived).
            // Apply that pose BEFORE the vault Lerp so Fall lengthens
            // leftover→0 on the knee clock. After vault this would
            // overwrite the strut back to leftover and cancel the ramp.
            if (fallCatchWalk && splitLegs && fc.stancePlantKnee > 0.5f)
            {
                if (!leftIsSwing)
                    leftKnee = Mathf.Clamp(kneeBaseAngle + fc.stancePlantKnee, 0f, 115f);
                if (!rightIsSwing)
                    rightKnee = Mathf.Clamp(kneeBaseAngle + fc.stancePlantKnee, 0f, 115f);
            }
            // WALK-2 Fall/Reach vault: stance knee to 0. The swing chain at
            // ~120° is 0.43 m hip-to-ankle; a straight stance is 0.86 m.
            // walkStanceKneeBend below would add 5° of stance flex while
            // liftGateBlend is 0 on this gait — that yields the strut and
            // the pelvis settles at the shorter leg (shuffle). Ankle CoM /
            // lead loops are unchanged. Vault from leftover frozen at
            // Plant exit when that pose was set above; otherwise from
            // kneeTarget (cycle-0 quiet Fall). fallSwingFallVaultLead
            // saturates leftover→0 at 2× fold clock so the quiet 6°→0
            // strut is tall before the swing shortens (same-clock vault
            // left the stance shorter for most of the 0.15 s).
            if (fallCatchWalk && fc.stanceVault > 0.001f)
            {
                float ext = Mathf.Clamp01(fc.stanceVault);
                if (!leftIsSwing)
                    leftKnee = Mathf.Lerp(leftKnee, 0f, ext);
                if (!rightIsSwing)
                    rightKnee = Mathf.Lerp(rightKnee, 0f, ext);
            }
            // WALK-2 Catch: in the air keep the reach extension so the foot
            // can pass. On the plant, hold landDeg when asked (catchLandKnee):
            // 0.15×catchKnee (~8°) was sized for a 140° scrape counted as
            // plant; after heel-first extend that snap is an extensor yank
            // on a just-loaded heel (hold-fold). 0.3×55° still folded when
            // the plant was any contact — that path is flag 0.
            if (fallCatchWalk && fc.catchKnee > 0.5f)
            {
                const float catchPlantKneeScale = 0.15f;
                // In the air keep whatever the reach had already extended to
                // (fc.reachKnee). Min(catchKnee, reachKnee) used to clip a
                // still-folding shin (70°…140°) down to 55° on the first
                // Catch frame — the same yank catchExtendClock removes.
                // landDeg (35) is already below catchKnee, so flag 0 matches.
                float airKnee = fc.reachKnee > 0.5f
                    ? fc.reachKnee
                    : fc.catchKnee;
                float plantKnee = fc.plantKnee > 0.5f
                    ? fc.plantKnee
                    : fc.catchKnee * catchPlantKneeScale;
                if (leftIsSwing)
                {
                    bool planted = heelAirPose ? leftHeel : leftGrounded;
                    float k = planted ? plantKnee : airKnee;
                    leftKnee = Mathf.Clamp(kneeBaseAngle + k, 0f, 115f);
                }
                if (rightIsSwing)
                {
                    bool planted = heelAirPose ? rightHeel : rightGrounded;
                    float k = planted ? plantKnee : airKnee;
                    rightKnee = Mathf.Clamp(kneeBaseAngle + k, 0f, 115f);
                }
            }
            // walkStanceKneeBendLiftFade above: on the fall-catch gait the
            // blend is the swing foot's own clearance, so growing the bend
            // with it shortens the only strut exactly when the foot is
            // highest. Fade the same 0.5 end out instead. Both schedules meet
            // at liftGateBlend = 0, and the flag is fall-catch only.
            // walkPlantStanceStraightFirst: cycle 0 has no DualLoadMinSpan,
            // so liftGateBlend stays 0 and that fade never starts — the
            // quiet first Plant keeps the extra 5° on the only strut.
            // Same plantMinDuration clock as lean / knee / hip / friction.
            float stanceBendBlend = liftGateBlend;
            if (walkPlantStanceStraightFirst > 0.5f
                && fallCatchWalk
                && fallCatchPhaseCode == (int)StepPhaseDriver.FallCatchPhase.Plant
                && stepPhaseDriver != null
                && stepPhaseDriver.FallCatchCycleCount <= 0
                && !stepPhaseDriver.StandingRestore
                && !stepPhaseDriver.StandingSurrender
                && stepPhaseDriver.PlantGateReady)
            {
                float firstClock = Mathf.Clamp01(
                    stepPhaseDriver.PhaseAge()
                    / Mathf.Max(0.05f, stepPhaseDriver.plantMinDuration));
                stanceBendBlend = Mathf.Max(stanceBendBlend, firstClock);
            }
            // walkPlantFallStanceStraightFirst above. Plant already faded
            // the extra 5° to clock 1 (EnterFall requires plantAged).
            // Fall's PhaseAge is 0, DualLoadMinSpan never opens,
            // liftGateBlend stays 0, vaultClock is 0, so the extra 5°
            // returns on the only strut — ~4 mm of hip drop stolen from
            // the swing clearance the fold/drive/lean just bought.
            // Hold that endpoint. Catch / later cycles keep DualLoad /
            // vault (stanceVault > 0 skips the extra anyway). Max so a
            // millimetre of DualLoad that opened liftGateBlend is not
            // added twice.
            if (walkPlantFallStanceStraightFirst > 0.5f
                && walkPlantStanceStraightFirst > 0.5f
                && fallCatchWalk
                && (fallCatchPhaseCode == (int)StepPhaseDriver.FallCatchPhase.Fall
                    || fallCatchPhaseCode == (int)StepPhaseDriver.FallCatchPhase.Reach)
                && stepPhaseDriver != null
                && stepPhaseDriver.FallCatchCycleCount <= 0
                && !stepPhaseDriver.StandingRestore
                && !stepPhaseDriver.StandingSurrender
                && intent != null && Mathf.Abs(intent.moveX) > 0.01f)
            {
                stanceBendBlend = Mathf.Max(stanceBendBlend, 1f);
            }
            float bendSchedule = Mathf.Lerp(0.5f, 1f, stanceBendBlend);
            if (fallCatchWalk && walkStanceKneeBendLiftFade > 0.001f)
                bendSchedule = Mathf.Lerp(
                    bendSchedule,
                    Mathf.Lerp(0.5f, 0f, stanceBendBlend),
                    Mathf.Clamp01(walkStanceKneeBendLiftFade));
            float stanceKneeBend = Mathf.Max(0f, walkStanceKneeBend) * bendSchedule;
            if (stanceKneeBend > 0.001f && fc.stanceVault <= 0.001f
                && fc.stancePlantKnee <= 0.5f)
            {
                if (!leftIsSwing)
                    leftKnee = Mathf.Clamp(leftKnee + stanceKneeBend, 0f, 115f);
                if (!rightIsSwing)
                    rightKnee = Mathf.Clamp(rightKnee + stanceKneeBend, 0f, 115f);
            }
            // walkGeoSwingLift above. DualLoadMinSpan never opens on stacked
            // feet, so liftGateBlend stays 0 and the 140° Fall pin squats.
            // Vertical unload: stance knee 0 is the long strut, swing knee
            // is the shortened chain. Last write on BOTH knees for this
            // window so the 5° stance bend above cannot put flex back on
            // the only strut. ASSIGN, not Max: fc.reachKnee already wrote
            // 140° on Fall/Reach, and Max(140, geo~35) is a no-op — that
            // is the squat, not a lift. Plant has reachKnee 0, so assign
            // is the same 35° Max would have been. Ankle CoM / DualLoad /
            // pelvis PD / MixHipHold / stand cap are not touched. Air and
            // Catch keep the landing solve. Flag 0 is the former
            // fold-clock path. Cycle-0 Catch keeps this write so
            // 0.15×catchKnee cannot straighten the first plant.
            if (!fallCatchWalk || stepPhaseDriver == null
                || stepPhaseDriver.FallCatchCycleCount != 0
                || (fallCatchPhaseCode == (int)StepPhaseDriver.FallCatchPhase.Plant
                    && stepPhaseDriver.PhaseAge() < 0.02f))
                lastFirstStepGeoKnee = 0f;
            if (fallCatchWalk && walkGeoSwingLift > 0.5f
                && splitLegs && walkMoveDir != 0f
                && liftGateBlend < 0.05f
                && stepPhaseDriver != null
                && stepPhaseDriver.FallCatchCycleCount != 0
                && !stepPhaseDriver.StandingRestore
                && !stepPhaseDriver.StandingSurrender
                && (fallCatchPhaseCode == (int)StepPhaseDriver.FallCatchPhase.Plant
                    || fallCatchPhaseCode == (int)StepPhaseDriver.FallCatchPhase.Fall
                    || fallCatchPhaseCode == (int)StepPhaseDriver.FallCatchPhase.Reach))
            {
                bool swingDown = leftIsSwing ? leftGrounded : rightGrounded;
                bool stanceDown = leftIsSwing ? rightGrounded : leftGrounded;
                if (swingDown && stanceDown)
                {
                    float liftClear = walkGeoSwingLiftClearance;
                    float geoKnee = LiftKneeFlexDeg(
                        leftIsSwing ? leftHipJoint : rightHipJoint,
                        leftIsSwing ? leftAnkleJoint : rightAnkleJoint,
                        liftClear);
                    if (geoKnee >= 0f)
                    {
                        float command = Mathf.Clamp(geoKnee, 0f, 120f);
                        if (leftIsSwing)
                        {
                            leftKnee = command;
                            rightKnee = 0f;
                        }
                        else
                        {
                            rightKnee = command;
                            leftKnee = 0f;
                        }
                    }
                }
            }
            // WALK-1 Stance: same last-write as the fall-catch block above.
            // Ramp is walkGeoLiftU (same clock as the hip/knee weld fade).
            // Stance knee 0 is the strut. Stance ankle CoM is not touched.
            if (walkLiftDirtLift || walkStanceGeoLift)
            {
                bool swingDown = leftIsSwing ? leftGrounded : rightGrounded;
                bool stanceDown = leftIsSwing ? rightGrounded : leftGrounded;
                if (swingDown && stanceDown)
                {
                    float geoKnee = LiftKneeFlexDeg(
                        leftIsSwing ? leftHipJoint : rightHipJoint,
                        leftIsSwing ? leftAnkleJoint : rightAnkleJoint,
                        walkGeoSwingLiftClearance);
                    if (geoKnee >= 0f)
                    {
                        float u = WalkLiftDirtBlend(walkGeoLiftU);
                        float want = Mathf.Max(geoKnee, walkLiftKneeFlex);
                        float command = Mathf.Lerp(
                            kneeTarget, Mathf.Clamp(want, 0f, 120f), u);
                        float strut = Mathf.Lerp(kneeTarget, 0f, u);
                        if (leftIsSwing)
                        {
                            leftKnee = command;
                            rightKnee = strut;
                        }
                        else
                        {
                            rightKnee = command;
                            leftKnee = strut;
                        }
                    }
                }
            }
            // Cycle-0 air plant: last-write the swing 6° only. Stance
            // stays the vault strut (knee 0) — matching 10°/10° on both
            // legs was the same-length squat that kept both soles loaded.
            if (firstStepCatchPlant)
            {
                if (leftIsSwing)
                    leftKnee = FirstStepPlantKneeDeg;
                else
                    rightKnee = FirstStepPlantKneeDeg;
            }
            // Landing knee from geometry (walkSwingLandKneeGeometry above).
            // Last write on the swing knee on purpose: every fold clock, the
            // Catch air/plant split and the stance blocks have already had
            // their say, so this is the one place where the command that will
            // actually meet the dirt can be checked against the length the
            // leg needs. Stance knees are untouched — only the swing side.
            // Descending only: the same clearance band is crossed on the way
            // up, where straightening would cancel the lift-off the fold
            // clocks just paid for. Gated on fallCatchWalk (which requires
            // walkActive), so quiet stance, crouch, the pushes, cold one-leg
            // and legacy useFallCatchWalk=0 walk all stay bitwise.
            if (fallCatchWalk && walkSwingLandKneeGeometry > 0.5f
                && !firstStepHoldLift
                && walkMoveDir != 0f && touchdownWindow > 0.001f
                && bodyState != null)
            {
                bool swingAirLand = leftIsSwing ? !leftGrounded : !rightGrounded;
                Rigidbody2D swingFootLandRb = leftIsSwing ? leftFootRb : rightFootRb;
                if (swingAirLand && swingFootLandRb != null
                    && swingFootLandRb.linearVelocity.y < 0f)
                {
                    float landClearance = leftIsSwing
                        ? bodyState.leftFootGroundClearance
                        : bodyState.rightFootGroundClearance;
                    float landKnee = LandingKneeFlexDeg(
                        leftIsSwing ? leftHipJoint : rightHipJoint,
                        leftIsSwing ? leftAnkleJoint : rightAnkleJoint,
                        landClearance);
                    if (landKnee >= 0f)
                    {
                        float landCeiling = Mathf.Max(
                            landKnee, Mathf.Max(0f, walkLandKneeMinDeg));
                        if (leftIsSwing)
                            leftKnee = Mathf.Lerp(
                                leftKnee, Mathf.Min(leftKnee, landCeiling), touchdownWindow);
                        else
                            rightKnee = Mathf.Lerp(
                                rightKnee, Mathf.Min(rightKnee, landCeiling), touchdownWindow);
                    }
                }
            }
        }
        // Same band on the airborne swing knee: the fold and the landing knee
        // just solved above are commanded in degrees, and kneePGain 1.2 saturates
        // past 8.3° of the shared reference — so a 140° fold and a 35° landing
        // ceiling are the same signal 1 until the last few degrees. The grounded
        // swing knee keeps the shared reference: in Plant that leg still carries
        // weight through a closed chain, where a softer approach is a sag, not a
        // cleaner reach. Stance side is untouched on both counts.
        float leftKneeErrorRef = errorReferenceDegrees;
        float rightKneeErrorRef = errorReferenceDegrees;
        if (swingErrorRef > errorReferenceDegrees)
        {
            if (leftIsSwing && !leftGrounded)
                leftKneeErrorRef = swingErrorRef;
            else if (rightIsSwing && !rightGrounded)
                rightKneeErrorRef = swingErrorRef;
        }
        // 01:36 quieted Recover-air ankle. Recover still writes
        // catchKnee / reachKnee = CatchReachShinDeg (landDeg), so the
        // airborne swing knee SPD that extend on an open chain: hip
        // silent, ankle silent, shin yank fold→landDeg. Extensor slaps
        // the quiet foot into the dirt, Recover's 0.05 s plant window
        // misses. Quiet muscles; planted Recover already took catchKnee
        // plant. Quiet when recoverHipairClear /
        // fallSwingRecoverAirKneeClear is 0.
        // Air / Advance: WalkLift IK or plant hold. Dirt Unload/Clear:
        // geo-lift already wrote LiftKneeFlexDeg; Unload's 35° and hip
        // hold −5° are the closed chain (burn_walk 5.835, both soles
        // down). Advance scrape still last-writes WalkLift so geo
        // cannot keep 8 cm of clearance into the plant.
        if (!walkLiftDirtLift)
            ApplyWalkLiftKneeTargets(ref leftKnee, ref rightKnee);
        if (WalkLiftOwnsSwing())
        {
            if (leftIsSwing)
                leftKneeErrorRef = Mathf.Max(leftKneeErrorRef, walkSwingErrorReferenceDegrees);
            if (rightIsSwing)
                rightKneeErrorRef = Mathf.Max(rightKneeErrorRef, walkSwingErrorReferenceDegrees);
        }
        bool recoverAirKneeClear = recoverHipairClear
            && stepPhaseDriver.fallSwingRecoverAirKneeClear > 0.5f;
        if (recoverAirKneeClear && leftIsSwing && !leftGrounded)
            ApplySignal(0f, leftKneeFlexor, leftKneeExtensor);
        else
            ControlJointToAngleStable(leftKneeJoint, leftKnee, kneePGain, kneeDGain,
                leftKneeErrorRef, leftKneeFlexor, leftKneeExtensor, ref leftKneeEffectiveInertia);
        if (recoverAirKneeClear && rightIsSwing && !rightGrounded)
            ApplySignal(0f, rightKneeFlexor, rightKneeExtensor);
        else
            ControlJointToAngleStable(rightKneeJoint, rightKnee, kneePGain, kneeDGain,
                rightKneeErrorRef, rightKneeFlexor, rightKneeExtensor, ref rightKneeEffectiveInertia);

        // Grounded swing: on walk Stance — stance only (like cold oneg);
        // mid on Transfer dual; on liftBlend fade the swing ankle.
        float leftAnkleBalance;
        float rightAnkleBalance;
        // WalkLift plant is a right-ankle CoM hold under a −13° trunk
        // (walklift_restore: no sole left before Falling). EnterStance
        // then labels that foot swing and this rule zeroes it (actAnkleR
        // 0.73→0 at 7.85, offset 0.004→0.066). Share both CoM after the
        // first swap until a sole actually leaves.
        bool walkLiftShareAnkles = useWalkLift != 0
            && walkDualSupport
            && stepPhaseDriver != null
            && stepPhaseDriver.SwapCount > 0;
        if (walkLiftShareAnkles)
        {
            // spent0: both = ankleBalance (actAnkle 1/1 by 7.86) over-braked
            // the −13° plant and dumped −X. restore: grounded swing = 0
            // dumped +X. Keep a fraction of the old strut.
            float keep = Mathf.Clamp01(walkLiftSharedSwingAnkle);
            leftAnkleBalance = splitLegs && leftIsSwing && leftGrounded
                ? ankleBalance * keep : ankleBalance;
            rightAnkleBalance = splitLegs && rightIsSwing && rightGrounded
                ? ankleBalance * keep : ankleBalance;
        }
        else if (walkDualSupport && walkStancePhase
            && (!fallCatchWalk
                || (fallCatchPhaseCode == (int)StepPhaseDriver.FallCatchPhase.Plant
                    && stepPhaseDriver != null
                    && stepPhaseDriver.FallCatchCycleCount <= 0)))
        {
            // Cycle-0 Plant is the same roll as legacy Stance: only the
            // named stance ankle drives CoM to the toe. Both-ankle CoM
            // on stacked soles holds mid-pair (burn_stag 9 mm).
            leftAnkleBalance = splitLegs && leftIsSwing && leftGrounded ? 0f : ankleBalance;
            rightAnkleBalance = splitLegs && rightIsSwing && rightGrounded ? 0f : ankleBalance;
        }
        else if (walkDualSupport && liftBlend < 0.001f)
        {
            leftAnkleBalance = ankleBalance;
            rightAnkleBalance = ankleBalance;
        }
        else if (walkDualSupport)
        {
            leftAnkleBalance = splitLegs && leftIsSwing && leftGrounded
                ? ankleBalance * (1f - liftAct) : ankleBalance;
            rightAnkleBalance = splitLegs && rightIsSwing && rightGrounded
                ? ankleBalance * (1f - liftAct) : ankleBalance;
        }
        else
        {
            // Cold one-leg: the airborne swing foot is an open chain.
            // CoM ankle torque there only twitches the free plate
            // (oneleg_base: actAnkleL 0.025/0.11 every 5 ms = 100 Hz).
            // Walk / WalkLift keep the former grounded-swing mute.
            bool coldOneLegAnkle = splitLegs
                && (stepPhaseDriver == null || !stepPhaseDriver.walkActive);
            if (coldOneLegAnkle)
            {
                // March plant: the clocked "swing" is on the ground. Muting
                // its ankle leaves one foot to haul CoM and walks the body.
                if (Marching() && leftGrounded && rightGrounded)
                {
                    float clock = intent != null ? intent.standLeg : 0f;
                    if (Mathf.Abs(clock) > 0.5f)
                    {
                        // Clocked lift still dual: both-ankle CoM pins the
                        // swing in the closed chain (first lift of march_air:
                        // hipR only −13°, right sole almost never left).
                        leftAnkleBalance = leftIsSwing ? 0f : ankleBalance;
                        rightAnkleBalance = rightIsSwing ? 0f : ankleBalance;
                    }
                    else
                    {
                        leftAnkleBalance = ankleBalance;
                        rightAnkleBalance = ankleBalance;
                    }
                }
                else
                {
                    leftAnkleBalance = leftIsSwing ? 0f : ankleBalance;
                    rightAnkleBalance = rightIsSwing ? 0f : ankleBalance;
                    if (marchComHold)
                    {
                        float cap = Mathf.Clamp(marchAnkleCap, 0.05f, 1f);
                        float pinned = Mathf.Clamp(ankleBalance, -cap, cap);
                        if (marchToeHold)
                        {
                            if (stanceComOff > Mathf.Max(0f, marchAnkleToeGate))
                                pinned = Mathf.Min(pinned, 0f);
                            if (stanceComOff < -Mathf.Max(0f, marchAnkleHeelGate))
                                pinned = Mathf.Max(pinned, 0f);
                        }
                        if (!leftIsSwing)
                            leftAnkleBalance = pinned;
                        if (!rightIsSwing)
                            rightAnkleBalance = pinned;
                    }
                }
            }
            else
            {
                leftAnkleBalance = splitLegs && leftIsSwing && leftGrounded ? 0f : ankleBalance;
                rightAnkleBalance = splitLegs && rightIsSwing && rightGrounded ? 0f : ankleBalance;
            }
        }
        // VEST-2 quiet dual only. Midpoint ankleBalance stays the pendulum
        // error (a per-foot centre offset would saturate on the 1.5 cm hip
        // split). Plantarflex (plus) on a plate whose centre CoM has passed
        // is heel-lift, not a restoring CoP — that is the pf23r shuffle.
        // A toe-only gate never fires: the 12 cm push still sits inside the
        // 13 cm toe reach of both 3 cm-split plates. Mute the trail by
        // centre; if both centres are behind CoM, keep the anterior plate
        // until its toes (strategy 1 ceiling). Dorsiflex is unchanged.
        if (QuietVestibularPath()
            && quietAnklePerFoot > 0.5f
            && !splitLegs
            && leftGrounded && rightGrounded
            && comCalculator != null
            && bodyState != null)
        {
            float comX = comCalculator.GetCenterOfMass().x;
            float leftPlate = bodyState.leftFootPosition.x;
            float rightPlate = bodyState.rightFootPosition.x;
            float leftToe = QuietFootToeX(true);
            float rightToe = QuietFootToeX(false);
            leftAnkleBalance = QuietAnkleNoPushOff(
                leftAnkleBalance, comX, leftPlate, leftToe);
            rightAnkleBalance = QuietAnkleNoPushOff(
                rightAnkleBalance, comX, rightPlate, rightToe);
            QuietKeepAnteriorPlus(
                ankleBalance, comX,
                leftPlate, rightPlate, leftToe, rightToe,
                ref leftAnkleBalance, ref rightAnkleBalance);
            lastQuietAnkleL = leftAnkleBalance;
            lastQuietAnkleR = rightAnkleBalance;
        }
        else
        {
            lastQuietAnkleL = 0f;
            lastQuietAnkleR = 0f;
        }
        // walkPlantSwingAnkleReleaseFirst above. Fall-catch dual support
        // always takes liftBlend < 0.001, so BOTH ankles get the CoM loop.
        // With plantRockLead the shared leading-foot error is heel pressure
        // on the trail (CoP behind CoM) — the translator a leading-only
        // couple cannot be (comment 32). Fade that loop only once CoM is
        // ON the leading foot (StanceLead ≥ 0, same event as FallLeadReady).
        // Do not use FirstStepTrailStillLoaded here: its Capture early-out
        // would zero the trail on forward velocity while position is still
        // −1.5 cm, which is the pin returning. DualLoad / 55° still read
        // that helper. Restore / surrender keep the hold (brake). Later
        // cycles never enter this block.
        if (walkPlantSwingAnkleReleaseFirst > 0.5f
            && fallCatchWalk
            && fallCatchPhaseCode == (int)StepPhaseDriver.FallCatchPhase.Plant
            && stepPhaseDriver != null
            && stepPhaseDriver.FallCatchCycleCount <= 0
            && !stepPhaseDriver.StandingRestore
            && !stepPhaseDriver.StandingSurrender
            && stepPhaseDriver.PlantGateReady
            && splitLegs
            && intent != null && Mathf.Abs(intent.moveX) > 0.01f
            && !(stepPhaseDriver.SwingFootIsGrounded
                && stepPhaseDriver.StanceLead < 0f))
        {
            float firstClock = Mathf.Clamp01(
                stepPhaseDriver.PhaseAge()
                / Mathf.Max(0.05f, stepPhaseDriver.plantMinDuration));
            float release = 1f - firstClock;
            if (leftIsSwing && leftGrounded)
                leftAnkleBalance *= release;
            if (rightIsSwing && rightGrounded)
                rightAnkleBalance *= release;
        }
        // Cycle-0 Plant, CoM still behind the leading sole: every CoP on
        // that foot is ahead of CoM (a brake). Zero the leading ankle
        // loop so it does not pin; plantRockLead on the shared signal is
        // then a trailing heel-press (dorsiflex, CoP behind CoM, forward
        // GRF). StanceLead < 0 is that brake, not "swing still down" —
        // once CoM is on the lead the stance ankle must hold. Restore /
        // surrender keep the hold.
        if (fallCatchWalk
            && fallCatchPhaseCode == (int)StepPhaseDriver.FallCatchPhase.Plant
            && stepPhaseDriver != null
            && stepPhaseDriver.FallCatchCycleCount <= 0
            && stepPhaseDriver.SwingFootIsGrounded
            && stepPhaseDriver.StanceLead < 0f
            && !stepPhaseDriver.StandingRestore
            && !stepPhaseDriver.StandingSurrender
            && stepPhaseDriver.PlantGateReady
            && splitLegs)
        {
            if (leftIsSwing)
                rightAnkleBalance = 0f;
            else
                leftAnkleBalance = 0f;
        }
        // Quiet-stance rule zeroes a grounded swing ankle so it does not
        // fight the support. Catch is the opposite: that foot is the new
        // support and must push back against the rotation.
        if (fallCatchWalk && fc.catchAnkleOnSwing && splitLegs)
        {
            bool leftPlant = heelAirPose ? leftHeel : leftGrounded;
            bool rightPlant = heelAirPose ? rightHeel : rightGrounded;
            if (leftIsSwing && leftPlant)
                leftAnkleBalance = ankleBalance;
            if (rightIsSwing && rightPlant)
                rightAnkleBalance = ankleBalance;
        }
        // Fall/Reach: the trailing (swing) foot is the last ankle with a
        // moment arm under CoM. Dual-support routing hands it the STANCE
        // error, and after a catch the stance is the LEADING foot with CoM
        // behind it, so the rear ankle was ordered to dorsiflex: the heel
        // pressed down, the foot stayed pinned (0.96 grounded) and 140° of
        // knee flex fought a loaded ankle. Zeroing that signal freed the
        // foot (0.24) but deleted the only forward drive left in the step:
        // CoP then sat ahead of CoM through the whole cycle and the body
        // ratcheted backward (comTravelX −0.36 m, trunk 92.9° the wrong way).
        // The routing was wrong, not the loop. Give this ankle its own
        // error: in a healthy step CoM is ahead of the rear foot, which asks
        // for plantarflexion — push-off. That puts CoP behind CoM (forward
        // moment) and rolls the weight onto the toe, which is how a foot
        // leaves the ground, not how it is held down. Pinning is the other
        // half of the loop, so in Reach — the window whose job is the lift —
        // only the propulsive half survives. Fall used to keep the full
        // loop (a body going backward needs its heels); Fall→Reach is
        // gated on air, so that heel-press held swingFoot ~0.96 and
        // Reach never started. fallSwingFallAnklePush uses the same
        // propulsive half in Fall. Flag 0 is the former full Fall loop.
        // Stance ankle / comLead still drive.
        // Same routing one phase earlier, for the double-support window this
        // block never covered. Only on a settled Plant — the very band
        // StepPhaseDriver opens Fall on — and never while the recovery ladder
        // is holding: a restore or a surrender Plant is a brake, and a push
        // there is the fold it exists to prevent. Cycle 0 stays out: the
        // 3 cm viewpoint lunge on a 26 cm foot puts the toe ahead of CoM,
        // so plantarflex is a brake (comment 30). Later cycles have a
        // catch plant behind CoM.
        bool plantTrailingPush = fallCatchWalk && splitLegs
            && walkPlantPushScale > 0.001f
            && walkMoveDir != 0f
            && fallCatchPhaseCode == (int)StepPhaseDriver.FallCatchPhase.Plant
            && stepPhaseDriver.FallCatchCycleCount > 0
            && !stepPhaseDriver.StandingRestore
            && !stepPhaseDriver.StandingSurrender
            && stepPhaseDriver.PlantGateReady;
        // The burst must hand the body to Fall, not stall Plant. Past
        // plantComLeadMax the Plant→Fall gate closes and the ladder arms, so
        // fade the push to zero as CoM approaches that lead.
        float plantPushRoom = plantTrailingPush
            ? Mathf.Clamp01(1f - stanceLead
                / Mathf.Max(0.02f, stepPhaseDriver.plantComLeadMax))
            : 0f;

        // Set only where this block really writes a foot's signal. A scrape
        // with no command must keep the air pose: ankleBalance there is the
        // quiet-stance zero, and a relaxed ankle only drags the toe.
        bool leftScrapeAnkleCmd = false;
        bool rightScrapeAnkleCmd = false;

        if (fallCatchWalk && splitLegs && bodyState != null && comCalculator != null
            && (fallCatchPhaseCode == (int)StepPhaseDriver.FallCatchPhase.Fall
                || fallCatchPhaseCode == (int)StepPhaseDriver.FallCatchPhase.Reach
                || (fallCatchPhaseCode == (int)StepPhaseDriver.FallCatchPhase.Catch
                    && fc.ankleLift > 0.001f)
                || (fallCatchPhaseCode == (int)StepPhaseDriver.FallCatchPhase.Plant
                    && fc.ankleLift > 0.001f)))
        {
            bool swingGroundedNow = leftIsSwing ? leftGrounded : rightGrounded;
            if (swingGroundedNow)
            {
                float ankleLift = Mathf.Clamp01(fc.ankleLift);
                // walkPlantSwingAnkleKeepFirst above. Cycle 0 has no rear
                // moment arm, so the swing-foot CoM loop here is the same
                // dual-support hold walkPlantSwingAnkleReleaseFirst just
                // faded: plantFold writes ankleLift and this assignment
                // puts plantarflex back on the stacked sole. Keep the
                // released signal; ankleLift is only the toe-up that lets
                // the shin shorten without a µ=0.4 slide. Later cycles
                // keep the overwrite (trailing A2 still owns that sole).
                // Restore / surrender keep the overwrite (brake).
                // walkPlantFallAnkleKeepFirst: the same keep through
                // Plant→Fall / grounded Reach. FallCatchCycleCount stays 0
                // until Recover→Plant, so the first Fall is still cycle 0.
                // Propulsive-half is the later-cycle A2 gate; stacked
                // feet fail it and it zeroes rock-lead / Plant dorsiflex.
                // Cycle-0 Catch has no plant yet: the "swing foot" here is
                // still the trailing sole, and its own CoM offset is the
                // whole roll (CoM is past the LEAD), so the else branch below
                // saturates plantarflex and presses that toe into µ=1.2 dirt.
                // The 55° knee then folds against a loaded ankle — the same
                // pin the Fall/Reach keep exists for, one phase later. Once
                // the sole has left and the plant is latched the foot really
                // is the new support, so the overwrite comes back.
                bool keepReleased = walkPlantSwingAnkleKeepFirst > 0.5f
                    && fallCatchPhaseCode == (int)StepPhaseDriver.FallCatchPhase.Plant
                    && stepPhaseDriver.FallCatchCycleCount <= 0
                    && !stepPhaseDriver.StandingRestore
                    && !stepPhaseDriver.StandingSurrender
                    && stepPhaseDriver.PlantGateReady
                    && intent != null && Mathf.Abs(intent.moveX) > 0.01f;
                if (!keepReleased
                    && walkPlantFallAnkleKeepFirst > 0.5f
                    && (fallCatchPhaseCode == (int)StepPhaseDriver.FallCatchPhase.Fall
                        || fallCatchPhaseCode == (int)StepPhaseDriver.FallCatchPhase.Reach
                        || (fallCatchPhaseCode == (int)StepPhaseDriver.FallCatchPhase.Catch
                            && !firstStepCatchPlantLatched))
                    && stepPhaseDriver.FallCatchCycleCount <= 0
                    && !stepPhaseDriver.StandingRestore
                    && !stepPhaseDriver.StandingSurrender
                    && intent != null && Mathf.Abs(intent.moveX) > 0.01f)
                {
                    keepReleased = true;
                }
                float swingAnkle;
                if (keepReleased)
                {
                    float current = leftIsSwing ? leftAnkleBalance : rightAnkleBalance;
                    // walkPlantFallAnkleReleaseHoldFirst above. Plant already
                    // faded this sole to zero; Fall dual-support restores the
                    // midpoint CoM loop because the release block never runs
                    // here and PhaseAge is 0. Hold that zero. Plant keep
                    // still reads the faded current (clock already at 1).
                    if (walkPlantFallAnkleReleaseHoldFirst > 0.5f
                        && (fallCatchPhaseCode == (int)StepPhaseDriver.FallCatchPhase.Fall
                            || fallCatchPhaseCode == (int)StepPhaseDriver.FallCatchPhase.Reach)
                        && stepPhaseDriver.FallCatchCycleCount <= 0
                        && !stepPhaseDriver.StandingRestore
                        && !stepPhaseDriver.StandingSurrender
                        && intent != null && Mathf.Abs(intent.moveX) > 0.01f)
                    {
                        current = 0f;
                    }
                    float dir = Mathf.Abs(fcLeadDir) > 0.01f ? Mathf.Sign(fcLeadDir) : 1f;
                    float walkSigned = current * dir;
                    if (ankleLift > 0.001f)
                        walkSigned = walkSigned * (1f - ankleLift) - ankleLift;
                    swingAnkle = Mathf.Clamp(walkSigned * dir, -1f, 1f);
                }
                else
                {
                    float swingFootX = leftIsSwing
                        ? bodyState.leftFootPosition.x
                        : bodyState.rightFootPosition.x;
                    float swingOffsetN =
                        (comCalculator.GetCenterOfMass().x - swingFootX) / comRef;
                    swingAnkle = Mathf.Clamp(
                        ankleComP * swingOffsetN + ankleComD * velN, -1f, 1f) * fc.ankleScale;
                    if (fallCatchPhaseCode == (int)StepPhaseDriver.FallCatchPhase.Reach
                        || fallCatchPhaseCode == (int)StepPhaseDriver.FallCatchPhase.Catch
                        || fallCatchPhaseCode == (int)StepPhaseDriver.FallCatchPhase.Plant
                        || (fallCatchPhaseCode == (int)StepPhaseDriver.FallCatchPhase.Fall
                            && stepPhaseDriver.fallSwingFallAnklePush > 0.5f))
                    {
                        // Propulsive half only, in walk coordinates: a toe push
                        // can still let the foot go, a heel press cannot.
                        // Catch scrape is the same window as Reach scrape.
                        // Fall used to keep the heel-press half: Fall→Reach
                        // waits for air, so that pin held swingFoot ~0.96
                        // and Reach never started. fallSwingFallAnklePush
                        // uses this same half in Fall. Flag 0 is the former
                        // full Fall loop. Stance ankle / comLead still drive.
                        swingAnkle = Mathf.Max(0f, swingAnkle * fcLeadDir) * fcLeadDir;
                    }
                    // Hip lift (21:12) still lost to the toe: plantarflexion
                    // plus µ=0.4 turns a shortening shin into a backward slide.
                    // Fade that grip; if the loop was pushing, add a small
                    // dorsiflexor (toe-up). Heel-press is only faded, not
                    // deepened — that pin is the 0.96 grounded foot. Not an
                    // ankle-angle PD. Stance comLead keeps the forward drive.
                    // Fall may write 0.5 (fallSwingFallAnkleLift) so a full
                    // leftover push cancels to 0 after FallAnklePush; Reach
                    // may write 0.5 (fallSwingReachAnkleLift); Catch air
                    // may write 0.5 (fallSwingCatchAirAnkleLift); Catch
                    // scrape may write 0.5 (fallSwingCatchScrapeAnkleLift);
                    // Catch→Reach scrape may write 0.5
                    // (fallSwingCatchReachScrapeAnkleLift).
                    if (ankleLift > 0.001f)
                    {
                        float dir = Mathf.Abs(fcLeadDir) > 0.01f ? Mathf.Sign(fcLeadDir) : 1f;
                        float walkSigned = swingAnkle * dir;
                        if (walkSigned > 0f)
                            walkSigned = walkSigned * (1f - ankleLift) - ankleLift;
                        else
                            walkSigned *= (1f - ankleLift);
                        swingAnkle = Mathf.Clamp(walkSigned * dir, -1f, 1f);
                    }
                }
                if (leftIsSwing)
                {
                    leftAnkleBalance = swingAnkle;
                    leftScrapeAnkleCmd = true;
                }
                else
                {
                    rightAnkleBalance = swingAnkle;
                    rightScrapeAnkleCmd = true;
                }
            }
        }

        // Double-support push-off, Plant only. Kept out of the block above on
        // purpose: that one overwrites the foot's signal, and here the rear
        // ankle may still be the only heel holding a body that drifts back
        // (CoM can sit ahead of the rear foot and behind the midpoint at the
        // same time — a 0.22 m step puts 0.08 m and −0.03 m on the same
        // frame). So this is additive feedforward on top of the loop, not a
        // replacement: the push can cancel a heel press, never delete it.
        if (plantTrailingPush && bodyState != null && comCalculator != null)
        {
            bool plantSwingGrounded = leftIsSwing ? leftGrounded : rightGrounded;
            if (plantSwingGrounded)
            {
                float trailFootX = leftIsSwing
                    ? bodyState.leftFootPosition.x
                    : bodyState.rightFootPosition.x;
                // Everything here is in walk coordinates, then converted back
                // to the joint sign once at the end.
                float trailLead =
                    (comCalculator.GetCenterOfMass().x - trailFootX) * fcLeadDir;
                // Propulsive half of the rear foot's own CoM loop. Clamp01,
                // not Clamp(-1,1): a heel press on the trailing foot is the
                // pin that held swingFoot at 0.96, and this window's job is
                // to let that foot go.
                float pushLoop = Mathf.Clamp01(
                    ankleComP * (trailLead / comRef) + ankleComD * velN * fcLeadDir);
                // Dose it. The raw loop saturates at a 0.11 m rear arm, and a
                // saturated rear ankle held for a whole 0.5 s Plant is a hop,
                // not a push-off. Scale by the moment arm the push actually
                // has (zero right after the catch, when the feet are still
                // level) and by the room left before the gate closes.
                float trailWindow = Mathf.Clamp01(
                    trailLead / Mathf.Max(0.01f, walkPlantPushLeadWindow));
                // When the trailing knee is folding, this plantarflex is the
                // same toe-pin ankleLift exists to cancel. Fade with it so
                // the A2 burst does not put the lift back on the dirt.
                // Flag 0 leaves ankleLift 0 and this product is 1.
                // walkStanceLoadShare: trailing share on this ankle, same
                // number the rear hip already reads. Fallback 1 — this burst
                // never had a 0.5 floor, so stanceLoad would regress it.
                // Flag 0 leaves dualRearShare at −1 and this product is 1.
                float plantRearLoad = dualRearShare >= 0f ? dualRearShare : 1f;
                float push = pushLoop * Mathf.Clamp01(walkPlantPushScale)
                    * trailWindow * plantPushRoom
                    * (1f - Mathf.Clamp01(fc.ankleLift))
                    * plantRearLoad;
                if (push > 0.001f)
                {
                    float current = (leftIsSwing ? leftAnkleBalance : rightAnkleBalance)
                        * fcLeadDir;
                    float merged = Mathf.Clamp(current + push, -1f, 1f) * fcLeadDir;
                    if (leftIsSwing)
                        leftAnkleBalance = merged;
                    else
                        rightAnkleBalance = merged;
                }
            }
        }

        // Stance push-off: blocked until swingWasAirborne — no horizontal
        // thrust on a closed chain before the swing foot leaves the ground.
        float runPushScale = running ? Mathf.Max(0f, runStancePushScale) : 1f;
        float speedPush = 0f;
        float speedDriveSignal = 0f;
        float propulsionPush = 0f;
        if (walkSoft && !running && walkMoveDir != 0f && bodyState != null && pushAfterAir)
        {
            speedPush = Mathf.Clamp(walkSpeedError * Mathf.Max(0f, walkSpeedPushGain), 0f, Mathf.Max(0f, walkSpeedPushMax));
            if (splitLegs && standLegLevel >= Mathf.Max(0f, walkSpeedDriveStartLevel))
            {
                float drive = Mathf.Clamp(walkSpeedError * Mathf.Max(0f, walkSpeedDriveGain), 0f, Mathf.Max(0f, walkSpeedDriveMax));
                speedDriveSignal = drive * walkMoveDir;
            }
        }
        if (fallCatchWalk && fc.suppressWalkLean)
            speedDriveSignal = 0f;
        if (walkSoft && !running && splitLegs && walkMoveDir != 0f && walkStancePhase && pushAfterAir)
        {
            float phase = Mathf.Clamp01(walkStanceProgress);
            float start = Mathf.Clamp01(walkPropulsionStart);
            float peak = Mathf.Clamp(walkPropulsionPeak, start + 0.01f, 0.99f);
            float end = Mathf.Clamp(walkPropulsionEnd, peak + 0.01f, 1f);
            float envelope = 0f;
            if (phase > start && phase < end)
            {
                if (phase <= peak)
                    envelope = (phase - start) / Mathf.Max(0.01f, peak - start);
                else
                    envelope = (end - phase) / Mathf.Max(0.01f, end - peak);
            }

            if (envelope > 0.0001f)
            {
                float absTilt = vestibularSystem != null ? Mathf.Abs(vestibularSystem.GetBodyTilt()) : 0f;
                bool safeTilt = absTilt <= Mathf.Max(1f, walkPropulsionTiltGuardDeg);
                bool stanceGrounded = leftIsSwing ? rightGrounded : leftGrounded;
                bool safeCom = Mathf.Abs(stanceComOff) <= Mathf.Max(0.01f, walkPropulsionComWindow);
                if (safeTilt && safeCom && stanceGrounded)
                {
                    float stanceWeight = Mathf.Lerp(0.25f, 1f, Mathf.Clamp01(weightBlend));
                    propulsionPush = Mathf.Clamp(
                        envelope * stanceWeight * Mathf.Max(0f, walkPropulsionGain),
                        0f,
                        Mathf.Max(0f, walkPropulsionMax));
                }
            }
        }
        float totalPush = 0f;
        if (pushAfterAir)
            totalPush = walkStancePush * runPushScale + speedPush;
        if (pushAfterAir && walkSoft && splitLegs && stepPhaseDriver != null && stepPhaseDriver.PhaseCode == 0)
        {
            float phaseProgress = walkStanceProgress;
            float lateStart = Mathf.Clamp01(walkLatePushStart);
            bool swingAirNow = leftIsSwing ? !leftGrounded : !rightGrounded;
            if (phaseProgress > lateStart && (!walkLatePushNeedsAir || swingAirNow))
            {
                float lateT = (phaseProgress - lateStart) / Mathf.Max(0.01f, 1f - lateStart);
                float latePush = Mathf.Clamp(
                    lateT * Mathf.Max(0f, walkLatePushGain),
                    0f,
                    Mathf.Max(0f, walkLatePushMax));
                if (walkLatePushNeedsTouchdownWindow)
                    latePush *= touchdownWindow;
                totalPush += latePush;
            }
        }
        float speedComGate = Mathf.Max(0.01f, walkLiftComMax);
        if (speedPush > 0.001f)
            speedComGate *= Mathf.Max(0.1f, walkSpeedPushComGateScale);
        // comLiftGate is the swing-lift gate, not a push gate: it shuts at
        // walkLiftComMax (0.10 m) while the sole still has toe reach ahead of
        // the CoM, so it deleted the push at the propulsive peak — and deleted
        // it in one 5 ms step. Hold full authority out to the old gate, then
        // fade over the sole that is really left. The window ends AT the toe
        // because past it the CoP cannot get ahead of the CoM any more: the
        // torque only rotates the body about the toe edge.
        float toeReach = walkPushToeFade > 0.001f ? StanceToeReach() : 0f;
        float pushHoldLead = Mathf.Max(0.01f, walkLiftComMax);
        float pushToeEnd = pushHoldLead
            + (toeReach - pushHoldLead) * Mathf.Clamp01(walkPushToeFade);
        // Only on the signed fall-catch window. Run leaves walkMoveDir at 0,
        // so pushWindow degrades to 1 there and the boolean gate is the only
        // CoM guard left — and the legacy Stance/Transfer path owns the
        // recorded walk numbers. Both keep the former gate bit for bit.
        bool pushToeFade = fallCatchWalk && walkMoveDir != 0f
            && pushToeEnd > pushHoldLead + 0.001f;
        float pushToeRoom = 1f;
        if (pushToeFade && stanceLead > pushHoldLead)
        {
            pushToeRoom = Mathf.Clamp01(
                1f - (stanceLead - pushHoldLead) / (pushToeEnd - pushHoldLead));
        }
        // The |stanceComOff| guard measures the same quantity, and at target
        // speed it collapses back to walkLiftComMax — which would cut the new
        // tail off again. Where the fade is live, the geometric window is the
        // tighter of the two, so let this one out to it. The negative side is
        // untouched: pushWindow already needs a positive lead.
        float pushComGate = pushToeFade
            ? Mathf.Max(speedComGate, pushToeEnd)
            : speedComGate;
        // stanceLoad, not weightBlend: on the fall-catch gait weightBlend is
        // always 0, so this whole block was dead code. pushWindow keeps the
        // toe-down torque on the propulsive side of the CoM.
        if (walkSoft && splitLegs && totalPush > 0.001f && pushAfterAir && stanceLoad >= 0.5f
            && pushWindow > 0.001f
            && (pushToeFade ? pushToeRoom > 0.001f : comLiftGate >= 1f)
            && Mathf.Abs(stanceComOff) <= pushComGate)
        {
            float push = totalPush * stanceLoad * pushWindow * pushToeRoom;
            float allowNeg = Mathf.Max(0f, walkPushCounterAllowance);
            if (leftIsSwing && rightAnkleBalance >= -allowNeg)
                rightAnkleBalance = Mathf.Clamp(rightAnkleBalance + push, -1f, 1f);
            else if (rightIsSwing && leftAnkleBalance >= -allowNeg)
                leftAnkleBalance = Mathf.Clamp(leftAnkleBalance + push, -1f, 1f);
        }
        // WALK-1: geo hip-extend already wrote lastStanceHipExtend without
        // pushWindow. The stance ankle still waited for signed lead /
        // pushWindow, so the hip carried the pelvis and the sole did not
        // roll. Same plantar overlay as walkPlantStancePushFirst, but only
        // once CoM has actually passed the foot (plantar at lead 0 is a
        // brake on this plate). Quiet / oneg / pf23 never enter.
        if (!fallCatchWalk && walkStancePushOnLead > 0.5f
            && walkSoft && splitLegs && walkMoveDir != 0f
            && walkStancePush > 0.001f
            && lastStanceHipExtend > 0.0001f
            && stanceLead > 0.001f)
        {
            float hipScale = lastStanceHipExtend
                / Mathf.Max(0.001f, walkStanceHipExtend);
            float already = 0f;
            if (pushAfterAir && stanceLoad >= 0.5f && pushWindow > 0.001f
                && (pushToeFade ? pushToeRoom > 0.001f : comLiftGate >= 1f)
                && Mathf.Abs(stanceComOff) <= pushComGate)
                already = stanceLoad * pushWindow * pushToeRoom;
            float extra = Mathf.Max(0f, hipScale - already);
            float push = walkStancePush * extra;
            if (push > 0.001f)
            {
                float allowNeg = Mathf.Max(0f, walkPushCounterAllowance);
                if (leftIsSwing && rightAnkleBalance >= -allowNeg)
                    rightAnkleBalance = Mathf.Clamp(rightAnkleBalance + push, -1f, 1f);
                else if (rightIsSwing && leftAnkleBalance >= -allowNeg)
                    leftAnkleBalance = Mathf.Clamp(leftAnkleBalance + push, -1f, 1f);
            }
        }
        // walkPlantStancePushFirst above. Cycle-0 hip drive already wrote
        // lastStanceHipExtend without pushWindow; the stance ankle still
        // waited for signed lead. Same plantar overlay, same envelope.
        if (fallCatchWalk && walkPlantStancePushFirst > 0.5f
            && walkSoft && splitLegs && walkMoveDir != 0f
            && walkStancePush > 0.001f
            && stepPhaseDriver != null
            && stepPhaseDriver.FallCatchCycleCount <= 0
            && lastStanceHipExtend > 0.0001f)
        {
            float hipScale = lastStanceHipExtend
                / Mathf.Max(0.001f, walkStanceHipExtend);
            float already = 0f;
            if (pushAfterAir && stanceLoad >= 0.5f && pushWindow > 0.001f
                && (pushToeFade ? pushToeRoom > 0.001f : comLiftGate >= 1f)
                && Mathf.Abs(stanceComOff) <= pushComGate)
                already = stanceLoad * pushWindow * pushToeRoom;
            float extra = Mathf.Max(0f, hipScale - already);
            float push = walkStancePush * extra;
            if (push > 0.001f)
            {
                float allowNeg = Mathf.Max(0f, walkPushCounterAllowance);
                if (leftIsSwing && rightAnkleBalance >= -allowNeg)
                    rightAnkleBalance = Mathf.Clamp(rightAnkleBalance + push, -1f, 1f);
                else if (rightIsSwing && leftAnkleBalance >= -allowNeg)
                    leftAnkleBalance = Mathf.Clamp(leftAnkleBalance + push, -1f, 1f);
            }
        }
        if (walkSoft && splitLegs && propulsionPush > 0.001f && pushAfterAir
            && weightBlend >= Mathf.Clamp01(walkPropulsionMinWeight))
        {
            float allowNeg = Mathf.Max(0f, walkPushCounterAllowance)
                + Mathf.Max(0f, walkPropulsionCounterAllowance);
            if (leftIsSwing && rightAnkleBalance >= -allowNeg)
                rightAnkleBalance = Mathf.Clamp(rightAnkleBalance + propulsionPush, -1f, 1f);
            else if (rightIsSwing && leftAnkleBalance >= -allowNeg)
                leftAnkleBalance = Mathf.Clamp(leftAnkleBalance + propulsionPush, -1f, 1f);
        }
        if (walkSoft && !running && splitLegs && Mathf.Abs(speedDriveSignal) > 0.001f)
        {
            if (leftIsSwing && rightGrounded)
                rightAnkleBalance = Mathf.Clamp(rightAnkleBalance + speedDriveSignal, -1f, 1f);
            else if (rightIsSwing && leftGrounded)
                leftAnkleBalance = Mathf.Clamp(leftAnkleBalance + speedDriveSignal, -1f, 1f);
        }

        ApplyWalkLiftAnkleRemainder(ref leftAnkleBalance, ref rightAnkleBalance);

        float leftClearance = bodyState != null ? bodyState.leftFootGroundClearance : 0.5f;
        float rightClearance = bodyState != null ? bodyState.rightFootGroundClearance : 0.5f;
        // 01:28 cleared Recover hipair. Recover-air still SPD the ankle
        // to joint=0 (sticky latch, airAnkle 0 so world-flat stays off).
        // 01:36 quiets that Recover pair. Plant-air leftover is now
        // world-flat drive (fallSwingPlantAirAnkleLevel): same airAnkle
        // Fall/Reach already write. The quiet skip runs only when that
        // drive is off.
        bool recoverAirAnkleClear = recoverHipairClear
            && stepPhaseDriver.fallSwingRecoverAirAnkleClear > 0.5f;
        // Plant-air world-flat (fallSwingPlantAirAnkleLevel) is the
        // drive that replaced this skip: silence and joint=0 both
        // replant. Level on → do not zero; DriveSwing takes airAnkle.
        bool plantAirAnkleClear = plantAirHold
            && stepPhaseDriver.fallSwingPlantAirAnkleClear > 0.5f
            && stepPhaseDriver.fallSwingPlantAirAnkleLevel < 0.5f;
        bool skipLatchedAirAnkle = recoverAirAnkleClear || plantAirAnkleClear;
        // The Plant ankle CoM loop stays live on cycle 0: plantRockLead is
        // the only external CoP the initiation has, and it is already in
        // comLead.
        DriveSwingOrStanceAnkle(leftIsSwing, false, leftGrounded, leftAnkleJoint,
            leftAnkleBalance, leftAnkleFlexor, leftAnkleExtensor, ref leftAnkleEffectiveInertia,
            fc.airAnkle, fc.airHeel, leftClearance, leftHeel, heelAirPose, leftScrapeAnkleCmd,
            skipLatchedAirAnkle);
        DriveSwingOrStanceAnkle(rightIsSwing, true, rightGrounded, rightAnkleJoint,
            rightAnkleBalance, rightAnkleFlexor, rightAnkleExtensor, ref rightAnkleEffectiveInertia,
            fc.airAnkle, fc.airHeel, rightClearance, rightHeel, heelAirPose, rightScrapeAnkleCmd,
            skipLatchedAirAnkle);

        // MTP is a new hinge: hold it flat on a planted sole, toe-up in
        // the air. The fall-catch FSM does not write this joint.
        DriveToeJoint(true, FootSupportDown(true) ? 0f : -Mathf.Max(0f, toeClearanceDeg));
        DriveToeJoint(false, FootSupportDown(false) ? 0f : -Mathf.Max(0f, toeClearanceDeg));

        ControlLumbarToWorldUpright(
            WalkLiftLumbarTarget(
                lumbarTargetTilt - crouchLevel * crouchTorsoLean - leanLevel * leanTorsoAngle - walkAutoTorsoLean - walkBaseTorsoLean
                - fc.torsoLean));

        ControlNeckAndHeadToWorldUpright();
        ControlArmsForBalance(balanceSignal);
    }

    private void UpdateState(float comOffset)
    {
        float absOffset = Mathf.Abs(comOffset);
        // c125adbd 6.300 = walkTime 5 + 1.30 s: DualSupport still both-down,
        // |offset| crossed fallCoMOffset 0.30, then this named Falling.
        // CaptureWalk that same step had already raised StandingRestore,
        // FixedUpdate fell through, and quiet world-upright hips froze
        // the sprawl (torso −70°, state Recovery). A two-foot walk/skate
        // with |tilt| still under the GETUP heap is that orbit, not a
        // finished fall. Heap (tilt ≥ getUpFallenAbsTilt / sink) is
        // GETUP-1. PD gains unchanged.
        if (absOffset > fallCoMOffset)
        {
            if (WalkTwoFootSkateNotHeap())
                currentState = absOffset > recoveryCoMOffset
                    ? BalanceState.Recovery
                    : BalanceState.Balancing;
            else
                currentState = BalanceState.Falling;
        }
        else if (absOffset > recoveryCoMOffset) currentState = BalanceState.Recovery;
        else currentState = BalanceState.Balancing;
    }

    // DualSupport both-down at the 0.30 m quiet-stance band. Cached refs.
    private bool WalkTwoFootSkateNotHeap()
    {
        if (stepPhaseDriver == null || !stepPhaseDriver.walkActive)
            return false;
        if (stepPhaseDriver.BodyCollapsed)
            return false;
        if (bodyState == null
            || !bodyState.leftFootGrounded
            || !bodyState.rightFootGrounded)
            return false;
        float tilt = vestibularSystem != null ? vestibularSystem.GetBodyTilt() : 0f;
        return Mathf.Abs(tilt) < Mathf.Max(20f, getUpFallenAbsTilt);
    }

    // Two ways to spend the walk catch exemption: the body no longer has the
    // height a catch needs (the same fold the walk FSM aborts on), or the
    // exemption has outlived any flight. Either way the CoM is past
    // fallCoMOffset and no swing is going to reach anything.
    private bool FallCatchExemptionSpent()
    {
        // Standing Plant: quiet two-foot PD is standing up. The 1.5 s
        // grace used to spend anyway and RelaxAllMuscles a tipped
        // BeginWalk whose |CoM offset| sat past fallCoMOffset 0.30.
        // Tick still owns the resume (TiltSettled + CoM-to-stance).
        if (stepPhaseDriver != null
            && stepPhaseDriver.walkRecoverAnyPose > 0.5f
            && (stepPhaseDriver.StandingRestore
                || stepPhaseDriver.StandingSurrender))
            return false;
        if (stepPhaseDriver != null && stepPhaseDriver.BodyCollapsed)
        {
            // Cycle-0 Fall/Reach/Catch/Recover already sit near
            // sinkAbortDrop (24° lean + 55° fold). That is the first
            // plant, not the 0.87 m heap. Spending here on the plant
            // frame RelaxAllMuscles the catch (CoM past 0.30) and is
            // the same death as standLeg=0 DrivePelvis. The grace
            // clock still spends it; a later-cycle Plant heap still
            // spends immediately.
            int ph = stepPhaseDriver.FallCatchPhaseCode;
            bool firstStepLanding =
                stepPhaseDriver.FallCatchCycleCount == 0
                && (ph == (int)StepPhaseDriver.FallCatchPhase.Fall
                    || ph == (int)StepPhaseDriver.FallCatchPhase.Reach
                    || ph == (int)StepPhaseDriver.FallCatchPhase.Catch
                    || ph == (int)StepPhaseDriver.FallCatchPhase.Recover);
            if (!firstStepLanding)
                return true;
        }
        return fallCatchFallGraceSeconds > 0.001f
            && fallCatchIgnoreFallAge >= fallCatchFallGraceSeconds;
    }

    // Crouch target is a step; the regulator sets the descent rate.
    // No MotionIntent — we stand, so the stand and extra humans do not break.
    private void UpdateCrouchLevel()
    {
        float desired = 0f;
        if (intent != null)
            desired = Mathf.Clamp01(intent.crouch);
        float rate = desired < crouchLevel ? crouchReleaseRatePerSecond : crouchRatePerSecond;
        crouchLevel = Mathf.MoveTowards(
            crouchLevel, desired, Mathf.Max(0f, rate) * Time.fixedDeltaTime);
    }

    // Lean is a −1/0/+1 step from the keyboard; the regulator sets the slew.
    private void UpdateLeanLevel()
    {
        float desired = 0f;
        if (intent != null)
            desired = Mathf.Clamp(intent.lean, -1f, 1f);
        leanLevel = Mathf.MoveTowards(
            leanLevel, desired, Mathf.Max(0f, leanRatePerSecond) * Time.fixedDeltaTime);
    }

    // Intent is a −1/0/+1 step; the regulator sets the lift rate.
    private void UpdateStandLegLevel()
    {
        float desired = 0f;
        if (intent != null && Mathf.Abs(intent.standLeg) > 0.5f)
            desired = 1f;
        float rate = standLegRatePerSecond;
        if (desired < 0.5f && standLegLevel > desired)
            rate = standLegReleaseRatePerSecond;
        else if (desired > 0.5f && lastLiftBlend > 0.001f
                 && stepPhaseDriver != null && stepPhaseDriver.walkActive
                 && walkLiftLevelRateScale > 1.01f)
            rate *= 1f + lastLiftBlend * (walkLiftLevelRateScale - 1f);

        // Quiet release only. Walk keeps walkEndHoldSupport. Do not decay
        // the one-leg pose while the swing is still airborne: at 4/s the
        // level hits 0 in 0.25 s, left-support plant is ~0.29 s (forward
        // peel from standLegPelvisTilt +10°). oneleg_sym_l4: knee already
        // unwound 60°→16° in that window, then dual commands from 25.254
        // planted at 25.294 with comOffset −0.19 (support midpoint jumps
        // +X; CoM stays ~0.054), old stance unloads, Falling at 27.06.
        // Freeze level until plant, then the usual 4/s decay. 1 s cap so
        // a stuck swing cannot hold the split forever.
        bool quietPath = (stepPhaseDriver == null || !stepPhaseDriver.walkActive)
            && (intent == null || intent.march < 0.5f);
        if (quietPath && desired < 0.5f && standLegLevel > 0.001f
            && Mathf.Abs(heldStandCmd) > 0.5f)
        {
            bool swingDown = heldStandCmd > 0.5f
                ? FootSupportDown(true)
                : FootSupportDown(false);
            if (!swingDown)
            {
                quietReleaseAirSeconds += Time.fixedDeltaTime;
                if (quietReleaseAirSeconds < 1f)
                    desired = standLegLevel;
            }
            else
                quietReleaseAirSeconds = 0f;
        }
        else
            quietReleaseAirSeconds = 0f;

        standLegLevel = Mathf.MoveTowards(
            standLegLevel, desired, Mathf.Max(0f, rate) * Time.fixedDeltaTime);
    }

    // Dimensionless PD signal in [-1, 1] — goes straight into muscle activation.
    private float ComputeSignal(HingeJoint2D joint, float targetAngle, float pGain, float dGain)
    {
        float error = Mathf.DeltaAngle(joint.jointAngle, targetAngle) / errorReferenceDegrees;
        float speed = joint.jointSpeed / speedReferenceDegPerSec;
        return Mathf.Clamp(pGain * error - dGain * speed, -1f, 1f);
    }

    // One convention for every joint: a positive signal means
    // "increase the joint angle", and that is extensor work. Verified on the knee:
    // counterclockwise torque (flexor) decreases jointAngle.
    private void ApplySignal(float signal, Muscle flexor, Muscle extensor)
    {
        if (signal > 0f)
        {
            UpdateMuscle(extensor, signal);
            UpdateMuscle(flexor, 0f);
        }
        else
        {
            UpdateMuscle(extensor, 0f);
            UpdateMuscle(flexor, -signal);
        }
    }

    // Kept for compatibility; every call has moved to Stable PD.
    private void ControlJointToAngle(HingeJoint2D joint, float targetAngle, float pGain, float dGain, Muscle flexor, Muscle extensor)
    {
        if (joint == null || flexor == null || extensor == null) return;
        ApplySignal(ComputeSignal(joint, targetAngle, pGain, dGain), flexor, extensor);
    }

    // intent.march is the stand clock, not walkActive. WalkLift stays off.
    private bool Marching()
    {
        return intent != null && intent.march > 0.5f
            && (stepPhaseDriver == null || !stepPhaseDriver.walkActive);
    }

    // Quiet dual, quiet one-leg, and a push: not walk, not the march clock.
    private bool QuietVestibularPath()
    {
        bool walking = stepPhaseDriver != null && stepPhaseDriver.walkActive;
        return !walking && !Marching();
    }

    // Anatomical +X edge of the last grounded box on that plate (toe if
    // the human faces right). Not Collider2D.bounds: plantarflex lifts the
    // heel, and AABB max.x then tracks the heel as if it were extra toe
    // reach, so KeepAnteriorPlus would keep restoring after the sole is gone.
    // Fallback: plate centre plus half sole — StanceToeReach, already cached.
    private float QuietFootToeX(bool left)
    {
        float heelToe = QuietColliderForwardX(left ? leftHeelBox : rightHeelBox);
        float toeToe = QuietColliderForwardX(left ? leftToeBox : rightToeBox);
        float maxX = heelToe > toeToe ? heelToe : toeToe;
        if (!float.IsNegativeInfinity(maxX))
            return maxX;
        float centre = bodyState != null
            ? (left ? bodyState.leftFootPosition.x : bodyState.rightFootPosition.x)
            : 0f;
        return centre + StanceToeReach();
    }

    // Local +X face of the box, world X of its two corners. Stack, no alloc.
    private static float QuietColliderForwardX(BoxCollider2D box)
    {
        if (box == null)
            return float.NegativeInfinity;
        Vector2 off = box.offset;
        Vector2 size = box.size;
        float localX = off.x + size.x * 0.5f;
        Transform t = box.transform;
        float y0 = off.y - size.y * 0.5f;
        float y1 = off.y + size.y * 0.5f;
        float a = t.TransformPoint(new Vector3(localX, y0, 0f)).x;
        float b = t.TransformPoint(new Vector3(localX, y1, 0f)).x;
        return a > b ? a : b;
    }

    // Plus ankleBalance is plantarflex. Past this plate's centre that
    // torque lifts the heel (the rear shuffle); past the toes the plate
    // has no restoring CoP left. Dorsiflex (minus) stays: a back lean
    // still needs a heel CoP, and pb23 already holds both feet down.
    private static float QuietAnkleNoPushOff(
        float shared, float comX, float plateX, float toeX)
    {
        if (shared <= 0f)
            return shared;
        if (comX >= toeX || comX >= plateX)
            return 0f;
        return shared;
    }

    // Stance width is ~3 cm; a 12 cm forward offset is ahead of both
    // centres. Strategy 1 still has CoP on the anterior sole until the
    // toes, so that plate keeps the shared plus. No new torque sign.
    private static void QuietKeepAnteriorPlus(
        float shared, float comX,
        float leftPlate, float rightPlate,
        float leftToe, float rightToe,
        ref float leftCmd, ref float rightCmd)
    {
        if (shared <= 0f)
            return;
        if (leftCmd > 0f || rightCmd > 0f)
            return;
        bool leftAnterior = leftPlate >= rightPlate;
        if (leftAnterior)
        {
            if (comX < leftToe)
                leftCmd = shared;
            else if (comX < rightToe)
                rightCmd = shared;
        }
        else if (comX < rightToe)
            rightCmd = shared;
        else if (comX < leftToe)
            leftCmd = shared;
    }

    // Pelvis to world vertical through the hips. Error and speed are pelvis
    // tilt and ω, not jointAngle/jointSpeed: do not mix world and joint.
    // The sign does not copy lumbar. The lumbar joint sits on the chest, the
    // hip on the thigh, so the pelvis gets torque with a minus. A positive
    // signal (extensor) turns the pelvis counterclockwise and increases jointAngle.
    // While the swing is grounded it stays here: unload shifts its signal to
    // flexor, support gets clean PD. After latch the swing goes to a joint pose.
    // leftCounter / rightCounter: swing-hip reaction cancellation. Zero on
    // every path that is not a walk split, and zero keeps the former signal
    // bitwise (no extra Clamp), same reason as WithHipUnload.
    private void ControlHipsToWorldUpright(float targetTilt, bool driveLeft, bool driveRight,
                                          float leftUnload, float rightUnload,
                                          float leftCounter = 0f, float rightCounter = 0f,
                                          bool hipHold = true,
                                          float leftHoldAngle = float.NaN,
                                          float rightHoldAngle = float.NaN,
                                          float holdBandDeg = -1f)
    {
        if (vestibularSystem == null) return;

        float tilt = vestibularSystem.GetPelvisTilt();
        float angVel = vestibularSystem.GetPelvisAngularVelocity();
        float error = Mathf.DeltaAngle(tilt, targetTilt) / Mathf.Max(1f, pelvisErrorReferenceDegrees);
        float speed = angVel / Mathf.Max(1f, speedReferenceDegPerSec);
        float pGain = pelvisPGain;
        float dGain = pelvisDGain;
        bool coldOneLeg = standLegLevel > 0.5f
            && (stepPhaseDriver == null || !stepPhaseDriver.walkActive);
        // Only once a sole is actually up. Dual-support lift on LEFT
        // support is a closed chain: the +10° pelvis target plus extra P
        // saturates both hips as extensors (oneleg_perf_l hipL +13° /
        // hipR +2° at 6 s, then dump). Right support has no extra tilt,
        // so the same boost looked like a hold.
        bool oneLegSingle = coldOneLeg && bodyState != null
            && (bodyState.leftFootGrounded != bodyState.rightFootGrounded);
        if (oneLegSingle)
        {
            pGain += Mathf.Max(0f, oneLegPelvisPBoost);
            dGain += Mathf.Max(0f, oneLegPelvisDBoost);
        }
        // March extra P is off by default: planted extensor carries the
        // pelvis +X (march_air 10.21 s, stance hip −3°→+8°, then vault).
        if (marchComHold)
            pGain += Mathf.Max(0f, marchStancePelvisP);
        float signal = Mathf.Clamp(pGain * error - dGain * speed, -1f, 1f);

        if (driveLeft)
            ApplySignal(WithHipCounter(
                    WithHipUnload(
                        MixHipHold(signal, leftHipJoint, hipHold, leftHoldAngle, holdBandDeg),
                        leftUnload),
                    leftCounter),
                leftHipFlexor, leftHipExtensor);
        if (driveRight)
            ApplySignal(WithHipCounter(
                    WithHipUnload(
                        MixHipHold(signal, rightHipJoint, hipHold, rightHoldAngle, holdBandDeg),
                        rightUnload),
                    rightCounter),
                rightHipFlexor, rightHipExtensor);
    }

    // counter=0 — the same signal, no extra Clamp: every non-walk path stays
    // bitwise the former one.
    private static float WithHipCounter(float signal, float counter)
    {
        if (counter == 0f) return signal;
        return Mathf.Clamp(signal + counter, -1f, 1f);
    }

    // Pelvis-upright PD is the same on both hips, so a quiet pelvis does
    // not stop a gravity split. Past hipHoldBand, add the existing hip
    // joint P/D toward hipHoldAngle. Quiet stance stays inside the band
    // (soff_squat maxHip ≈ 2°). Crouch commands a deep flexion — skip.
    // Walk / one-leg split skip too: a step and a lift ARE hip travel
    // past the band, and posing those hips to 0 extends the unloading
    // swing (no lift-off) and brakes the planted stance thigh (travelX
    // reversed, then the feet splay). The +45 hip extension stop is the
    // walk-time split wall; this mix is the quiet-stance motor before
    // that stop. Same hipP/hipD numbers, not a retune. Sign unchanged.
    private float MixHipHold(float pelvisSignal, HingeJoint2D hip, bool allowHold = true,
                             float holdAngleDeg = float.NaN, float holdBandDeg = -1f)
    {
        if (!allowHold || hip == null || crouchLevel > 0.001f)
            return pelvisSignal;
        float band = holdBandDeg >= 0f ? holdBandDeg : hipHoldBand;
        if (holdBandDeg < 0f && hipHoldBand < 0.5f)
            return pelvisSignal;
        float angle = float.IsNaN(holdAngleDeg) ? hipHoldAngle : holdAngleDeg;
        float excess = Mathf.Abs(hip.jointAngle - angle) - band;
        if (excess <= 0f)
            return pelvisSignal;
        float pose = ComputeSignal(hip, angle, hipPGain, hipDGain);
        float mix = Mathf.Clamp01(excess / Mathf.Max(1f, band));
        return Mathf.Clamp(pelvisSignal + pose * mix, -1f, 1f);
    }

    // unload=0 — the same signal, no extra Clamp: the dual-support path is
    // bitwise the former one. Minus unload pulls toward flexor (decreases jointAngle).
    private static float WithHipUnload(float signal, float unload)
    {
        if (unload == 0f) return signal;
        return Mathf.Clamp(signal - unload, -1f, 1f);
    }

    // Fall/Reach swing hip (grounded, Catch scrape-air, or Fall→Reach
    // first air). Two flexor-only terms, one ApplySignal:
    // hold = stop past vertical (21:06); lift = bias that prefers the knee
    // up instead of the foot sliding back. Not a −swingHipFlex pose (sci_a)
    // and not two-sided world-vertical SPD (20:25). Both 0 keep the silenced
    // hip. Same hip P/D numbers, not a retune.
    // Returns the applied signal so the stance hip can cancel its pelvis
    // reaction (walkSwingLiftCouple).
    private float ApplyFallCatchSwingThighHold(
        HingeJoint2D hip, Rigidbody2D thigh, Muscle flexor, Muscle extensor,
        float hold, float lift, float liftCapDeg, float liftFloor = 0f)
    {
        if (flexor == null || extensor == null)
            return 0f;
        float holdAmt = Mathf.Clamp01(hold);
        float liftAmt = Mathf.Clamp01(lift);
        if ((holdAmt <= 0.001f && liftAmt <= 0.001f) || hip == null)
        {
            ApplySignal(0f, flexor, extensor);
            return 0f;
        }
        if (thigh == null)
            thigh = hip.attachedRigidbody;
        if (thigh == null)
        {
            ApplySignal(0f, flexor, extensor);
            return 0f;
        }

        const float deadzoneDeg = 4f;
        // Lift fades out once the thigh is this far forward of vertical:
        // enough for the knee to rise, not a 40° scissor into the pelvis.
        // Fall lift-off may pass 16° (fallSwingFallHipLiftCap); 8° of a
        // 0.43 m thigh is ~0.4 cm up, so the fold still slides. Flag 0
        // keeps 8°. Reach may pass 16° (fallSwingReachHipLiftCap);
        // Catch scrape still passed 8°; fallSwingCatchScrapeHipLiftCap
        // may pass 16°. Fall bias may be
        // 0.5 (fallSwingFallHipLift) so the thigh actually walks that
        // room; Reach may write the same 0.5 (fallSwingReachHipLift);
        // 0.25 at vertical is only −0.20 and never arrives.
        // Reach may then floor the fade at 0.5 (fallSwingReachHipLiftCapHold)
        // so the cap is a hold against gravity, not P=0 at the target.
        // Fall may floor the same way (fallSwingFallHipLiftCapHold);
        // Catch air may floor the same way (fallSwingCatchAirHipLiftCapHold);
        // Catch scrape may floor the same way (fallSwingCatchScrapeHipLiftCapHold);
        // Reach floor is Reach-only. leftover→0 on the fold clock
        // still left the stance short; fallSwingFallVaultLead is the
        // 2× vault clock, not another fade. hipLift / ankleLift on that
        // same 1× ramp still left the foot on dirt; fallSwingFallLiftLead
        // is the 2× lift clock, not sci_a. Fold still shared that 1×
        // ramp (70° at lift=1); fallSwingFallFoldLead is the 2× fold.
        // Timeout Catch still faded at 8°; fallSwingCatchAirHipLiftCap
        // is the 16° Catch-air cap, not 3× clock.
        // That 16° Catch air still had P=0 at the cap;
        // fallSwingCatchAirHipLiftCapHold floors Catch air at 0.5.
        // That floor still writes
        // hipLift 0.25 (liftSignal −0.125 at the cap vs Fall/Reach
        // −0.25); fallSwingCatchAirHipLift writes 0.5 in Catch air
        // (scrape keeps 0.25). That 16°/0.5 Catch air still wrote
        // ankleLift 0.25; fallSwingCatchAirAnkleLift writes 0.5 in
        // Catch air (scrape may write 0.5). That 16° Catch air still
        // met an 8° scrape cap; fallSwingCatchScrapeHipLiftCap is
        // the 16° Catch-scrape cap. That 16° scrape still had P=0
        // at the cap; fallSwingCatchScrapeHipLiftCapHold floors Catch
        // scrape at 0.5, not 3× clock. That floor still writes
        // hipLift 0.25 (liftSignal −0.125 at the cap vs
        // Fall/Reach/Catch-air −0.25); fallSwingCatchScrapeHipLift
        // writes 0.5 in Catch scrape. That 16°/0.5 hipLift still wrote
        // ankleLift 0.25; fallSwingCatchScrapeAnkleLift writes 0.5 in
        // Catch scrape. That 16°/0.5/0.5 scrape still shared the 1×
        // fold ramp; fallSwingCatchScrapeLiftLead is the 2× scrape
        // lift clock, not 3× and not sci_a. That 16° scrape still met
        // an 8° Catch→Reach cap; fallSwingCatchReachScrapeHipLiftCap
        // is the 16° Catch→Reach-scrape cap, not 3× clock. That 16°
        // Catch→Reach scrape still had P=0 at the cap;
        // fallSwingCatchReachScrapeHipLiftCapHold floors it at 0.5.
        // That floor still writes hipLift 0.25 (liftSignal −0.125 vs
        // Fall/Reach/Catch-air/Catch-scrape −0.25);
        // fallSwingCatchReachScrapeHipLift writes 0.5 on that bounce.
        // That 16°/0.5 hipLift still wrote ankleLift 0.25;
        // fallSwingCatchReachScrapeAnkleLift writes 0.5 on that bounce.
        // That 16° cap still had room=0 at a 16° Catch-scrape thigh;
        // fallSwingCatchReachScrapeHipLiftCapWide is the 24° Catch→Reach
        // scrape cap, not 3× clock and not sci_a. That same room=0 still
        // pinned Catch scrape at 16°; fallSwingCatchScrapeHipLiftCapWide
        // is the 24° Catch-scrape cap, not 3× clock and not sci_a.
        // That same room=0 still pinned Fall at 16° after 23:47 kept
        // ThighHold on Fall's first air; fallSwingFallHipLiftCapWide
        // is the 24° Fall cap, not 3× clock and not sci_a. That same
        // room=0 still pinned Reach at 16° after Fall walked into 24°;
        // fallSwingReachHipLiftCapWide is the 24° Reach cap, not 3×
        // clock and not sci_a. That same room=0 still pinned Catch-air
        // at 16° after Reach walked into 24°; fallSwingCatchAirHipLiftCapWide
        // is the 24° Catch-air cap, not 3× clock and not sci_a. That same
        // room=0 still pinned Plant at 16°; fallSwingPlantHipLiftCapWide
        // is the 24° Plant cap, not 3× clock and not sci_a. That same
        // room=0 still pinned Fall at 24° after 00:09 walked Plant into
        // 24°; fallSwingFallHipLiftCapCeil is the 32° Fall cap, not 3×
        // clock and not sci_a. That same room=0 still pinned Reach at
        // 24° after Fall walked into 32°; fallSwingReachHipLiftCapCeil
        // is the 32° Reach cap, not 3× clock and not sci_a. That same
        // room=0 still pinned Catch-air at 24° after Reach walked into
        // 32°; fallSwingCatchAirHipLiftCapCeil is the 32° Catch-air cap,
        // not 3× clock and not sci_a. That same room=0 still pinned Plant
        // at 24° after leftover + hipLift 0.5 sat there;
        // fallSwingPlantHipLiftCapCeil is the 32° Plant cap, not 3×
        // clock and not sci_a. That same room=0 still pinned Catch
        // scrape at 24° after Catch-air walked into 32°;
        // fallSwingCatchScrapeHipLiftCapCeil is the 32° Catch-scrape
        // cap, not 3× clock and not sci_a. That same room=0 still pinned
        // Catch→Reach scrape at 24° after Catch scrape walked into 32°;
        // fallSwingCatchReachScrapeHipLiftCapCeil is the 32° Catch→Reach
        // scrape cap, not 3× clock and not sci_a.
        float liftCap = Mathf.Max(1f, liftCapDeg);
        float tiltFromUp = Mathf.DeltaAngle(0f, thigh.rotation);
        float speed = thigh.angularVelocity / Mathf.Max(1f, speedReferenceDegPerSec);

        float holdSignal = 0f;
        float extErrorDeg = -tiltFromUp;
        if (holdAmt > 0.001f && extErrorDeg > deadzoneDeg)
        {
            float error = (extErrorDeg - deadzoneDeg) / Mathf.Max(1f, errorReferenceDegrees);
            holdSignal = Mathf.Clamp(
                (-hipPGain * error + hipDGain * speed) * holdAmt,
                -1f, 0f);
        }

        float liftSignal = 0f;
        if (liftAmt > 0.001f)
        {
            // tiltFromUp > 0 = knee forward. Fade as we approach the cap so
            // the third-law shove stays bounded.
            float room = liftCap - tiltFromUp;
            float floor = Mathf.Clamp01(liftFloor);
            if (room > 0f)
            {
                float span = liftCap + deadzoneDeg;
                float fade = Mathf.Clamp01(room / Mathf.Max(1f, span));
                // P to the cap is 0 at the target, so gravity drops a 16°
                // thigh and the foot replants. A floor holds that operating
                // point; flag 0 is the former fade-to-zero.
                if (floor > 0.001f)
                    fade = Mathf.Max(fade, floor);
                liftSignal = -liftAmt * fade;
            }
            else if (floor > 0.001f)
            {
                // Past the cap: decay the floor over the deadzone so we
                // do not keep lifting into sci_a 40°.
                liftSignal = -liftAmt * floor
                    * Mathf.Clamp01(1f + room / deadzoneDeg);
            }
        }

        float thighSignal = Mathf.Clamp(holdSignal + liftSignal, -1f, 0f);
        ApplySignal(thighSignal, flexor, extensor);
        return thighSignal;
    }

    // Swing in the air: foot perpendicular to the shin (ankle 0), unless
    // fall-catch asked for world-flat. Near the ground, a small dorsiflex
    // (airHeel) makes the heel land first so the toe does not stub.
    // heelAirPose: a toe scrape is still this airborne pose — Catch must
    // not inherit a planted CoM loop from the long end of the sole.
    // Hip flex stays gated on !grounded (third law). Grounded toe-off
    // and stance-only CoM are set above, before this method is called.
    // scrapeCommand: Fall/Reach/Catch wrote this foot's own propulsive signal
    // above while it is still touching. That command is the mechanism the lift
    // is built on, so a real contact must not be re-labelled as flight and
    // handed an angle PD to a world pose (walkScrapeAnkleDrive).
    private void DriveSwingOrStanceAnkle(bool isSwing, bool isForwardSwing, bool grounded,
                                         HingeJoint2D joint, float ankleBalance,
                                         Muscle flexor, Muscle extensor, ref float ankleInertia,
                                         float fallCatchAirAnkle, float airHeelDeg,
                                         float footClearance, bool heelLoaded, bool heelAirPose,
                                         bool scrapeCommand, bool skipLatchedAirAnkle)
    {
        if (DriveWalkLiftAnkle(isSwing, grounded, joint, flexor, extensor, ref ankleInertia))
            return;
        // walkSwingAnkleSpdTorque above: integrate this joint with the torque
        // it is about to apply. Gated on the fall-catch walk so quiet stance,
        // the pushes, crouch, the cold one-leg probe (oneg_l / det_r*) and
        // legacy useFallCatchWalk=0 walk keep the former denominator bitwise.
        bool ankleDriveTorque = walkSwingAnkleSpdTorque > 0.5f
            && stepPhaseDriver != null && stepPhaseDriver.walkActive
            && stepPhaseDriver.useFallCatchWalk != 0;

        if (isSwing && grounded && !(heelAirPose && !heelLoaded) && !swingHipLatched && isForwardSwing
            && stepPhaseDriver != null && stepPhaseDriver.walkActive
            && lastLiftBlend > 0.35f && walkSwingToeOffMax > 0.1f)
        {
            float toe = lastLiftBlend * lastLiftBlend * walkSwingToeOffMax;
            ControlJointToAngleStable(joint, toe, kneePGain, kneeDGain,
                errorReferenceDegrees, flexor, extensor, ref ankleInertia,
                ankleDriveTorque);
            return;
        }
        if (isSwing && grounded && !(heelAirPose && !heelLoaded) && !swingHipLatched && isForwardSwing
            && swingAnkleGroundedToeOff > 0.1f)
        {
            ControlJointToAngleStable(joint, swingAnkleGroundedToeOff, kneePGain, kneeDGain,
                errorReferenceDegrees, flexor, extensor, ref ankleInertia,
                ankleDriveTorque);
            return;
        }
        bool airborne = !grounded;
        // 01:28 can now clear Recover hipair. Recover still leaves
        // airAnkle/airHeel at 0, but sticky latch plus !grounded still
        // SPD joint=0 (foot ⊥ shin) — the hipair ankle pair of that
        // pose. Knee flex then points the toe down and it scrapes.
        // Quiet muscles; do not hand CoM ankle to an open chain.
        // Planted Recover already took catchAnkleOnSwing. Quiet when
        // recoverHipairClear / fallSwingRecoverAirAnkleClear is 0.
        // 01:36 is Recover-air only. Plant-air now drives world-flat
        // (fallSwingPlantAirAnkleLevel) so this skip is off there;
        // flag 0 of that drive still quiets via plantAirAnkleClear.
        if (skipLatchedAirAnkle && isSwing && airborne)
        {
            ApplySignal(0f, flexor, extensor);
            return;
        }
        // Only while Fall/Reach/Catch asked for the air pose
        // (airAnkle/airHeel), or Plant-air wrote it
        // (fallSwingPlantAirAnkleLevel). Recover still leaves both at 0
        // so a heel flicker cannot SPD a standing ankle to world-flat.
        // A commanded scrape stays grounded: the sole is on the dirt, so the
        // foot gets its CoM/lift signal (the propulsive half and the ankleLift
        // dorsiflexor already computed for it), not a world-pose spring the
        // contact would turn into body rotation.
        bool scrapeDrive = isSwing && grounded && heelAirPose && !heelLoaded
            && scrapeCommand && walkScrapeAnkleDrive > 0.5f;
        if (!scrapeDrive && isSwing && heelAirPose && !heelLoaded
            && (fallCatchAirAnkle > 0.5f || airHeelDeg > 0.001f))
            airborne = true;
        if (isSwing && airborne)
        {
            // jointAngle = shin.rot − foot.rot. Foot horizontal (foot≈0) →
            // target = shin angle; else 0 pulls the toe down when the knee
            // flexes. Fall-catch Reach folds to ~140°, so joint 0 is a
            // paddle into the dirt. World-flat is airborne only.
            bool worldFlat = fallCatchAirAnkle > 0.5f
                || (swingHipLatched && walkSwingAirLevel > 0.5f);
            if (worldFlat && joint != null && joint.connectedBody != null)
            {
                float flat = Mathf.DeltaAngle(0f, joint.connectedBody.rotation);
                // Plus ankle = plantarflex (toe down). Heel-first is minus
                // from world-flat, blended over the last 8 cm so a high
                // Reach stays level and only the plant cocks the toe up.
                float heel = Mathf.Max(0f, airHeelDeg);
                const float heelWindow = 0.08f;
                if (heel > 0.001f)
                {
                    float t = 1f - Mathf.Clamp01(footClearance / heelWindow);
                    flat -= heel * t;
                }
                ControlJointToAngleStable(joint, flat, hipPGain, hipDGain,
                    errorReferenceDegrees, flexor, extensor, ref ankleInertia,
                    ankleDriveTorque);
                return;
            }
            if (swingHipLatched)
            {
                ControlJointToAngleStable(joint, 0f, hipPGain, hipDGain,
                    errorReferenceDegrees, flexor, extensor, ref ankleInertia,
                    ankleDriveTorque);
                return;
            }
        }
        ControlAnkleForBalance(joint, ankleBalance, flexor, extensor);
    }

    // Lumbar holds the chest to world vertical. Error and speed are
        // absolute torso tilt and angular velocity, not the joint angle:
        // when the pelvis topples the chest must brace, not ride down with it.
        // Inputs match the working version (error = target − tilt, world speed).
        // Gains are plus, so in P*e − D*ω they enter with a minus:
        // that is exactly the old P=−2 and D=−0.3, not a new damper.
    private void ControlLumbarToWorldUpright(float targetTilt)
    {
        if (lumbarFlexor == null || lumbarExtensor == null) return;
        if (vestibularSystem == null) return;

        float tilt = vestibularSystem.GetBodyTilt();
        float angVel = vestibularSystem.GetBodyAngularVelocity();
        float error = Mathf.DeltaAngle(tilt, targetTilt) / Mathf.Max(1f, lumbarErrorReferenceDegrees);
        float speed = angVel / Mathf.Max(1f, speedReferenceDegPerSec);
        float signal = Mathf.Clamp((-lumbarPGain) * error - (-lumbarDGain) * speed, -1f, 1f);
        ApplySignal(signal, lumbarFlexor, lumbarExtensor);
    }

    // Neck and head to world vertical. They used to hold the angle to the parent
    // (ControlJointToAngle to zero), i.e. they copied every torso topple —
    // the same error already fixed on lumbar and pelvis.
    //
    // What held them was brute force, not the regulator: 15 N·m on paired
    // inertia 0.0015 kg·m² is about 9900 rad/s², and PD reversed the joint
    // every physics step. The head spanned 9.8° at 25 Hz. But that chatter
    // was load-bearing: it welded 5.7 kg of head to the chest, and the
    // forward push sat on that weld. Just lowering torque fails — verified,
    // the threshold drops from 21 N·s below 18 (labels `nm*`). Target first, then torque.
    //
    // Both joints sit on their own segment, like lumbar on the chest, so the
    // sign copies lumbar, not pelvis: plus P and D enter as −P and −D.
    private void ControlNeckAndHeadToWorldUpright()
    {
        if (vestibularSystem == null) return;

        ControlSegmentToWorldUpright(
            vestibularSystem.GetNeckTilt(), vestibularSystem.GetNeckAngularVelocity(),
            neckJoint, neckFlexor, neckExtensor, ref neckEffectiveInertia);
        ControlSegmentToWorldUpright(
            vestibularSystem.GetHeadTilt(), vestibularSystem.GetHeadAngularVelocity(),
            headJoint, headFlexor, headExtensor, ref headEffectiveInertia);
    }

    private void ControlSegmentToWorldUpright(float tilt, float angVel, HingeJoint2D joint,
                                              Muscle flexor, Muscle extensor, ref float cachedInertia)
    {
        if (flexor == null || extensor == null) return;

        // Stable PD (Tan, Liu, Turk, 2011): take the error from the next
        // step, not the current one, and divide the signal by (1 + K·Δt/I).
        // Ordinary explicit PD diverges when K·Δt/I exceeds 2 — the same
        // rule written in balance-actuators for JointFriction. At the neck
        // that ratio is 5.7 at inertia 0.0015 kg·m², hence chatter of
        // 3.3° per physics step. The denominator kills overshoot, and the
        // predicted angle adds phase lead, so the damper can stay strong:
        // it is what keeps the head off the stop on a push.
        float tiltForError = tilt;
        float damping = 0f;

        if (useStablePd)
        {
            float dt = Time.fixedDeltaTime;
            tiltForError = tilt + angVel * dt;

            if (cachedInertia <= 0f)
                cachedInertia = EffectiveInertia(joint);

            if (cachedInertia > 0f)
            {
                // K in N·m·s/rad: normalized dGain × muscle ceiling, converted
                // from degrees to radians. Else the ratio is not dimensionless.
                float k = neckDGain * flexor.maxTorque * Mathf.Rad2Deg
                          / Mathf.Max(1f, speedReferenceDegPerSec);
                damping = k * dt / cachedInertia;
            }
        }

        float error = Mathf.DeltaAngle(tiltForError, neckTargetTilt) / Mathf.Max(1f, neckErrorReferenceDegrees);
        float speed = angVel / Mathf.Max(1f, speedReferenceDegPerSec);
        float raw = (-neckPGain) * error - (-neckDGain) * speed;
        float signal = Mathf.Clamp(raw / (1f + damping), -1f, 1f);
        ApplySignal(signal, flexor, extensor);
    }

    // Base pose plus CoM counter-reach. On walk — opposite-phase swing
    // by Stance phase; CoM-balance is off on walk (fold after swap).
    private void ControlArmsForBalance(float balanceSignal)
    {
        float lSh, rSh, lEl, rEl, lWr, rWr;
        if (stepPhaseDriver != null && stepPhaseDriver.walkActive)
        {
            ComputeWalkArmTargets(out lSh, out rSh, out lEl, out rEl, out lWr, out rWr);
            float walkArmSignal = Mathf.Clamp(balanceSignal, -1f, 1f);
            lSh = Mathf.Clamp(lSh + walkArmSignal * walkArmBalanceShoulderGain, -85f, 85f);
            rSh = Mathf.Clamp(rSh + walkArmSignal * walkArmBalanceShoulderGain, -85f, 85f);
            lEl = Mathf.Clamp(lEl + walkArmSignal * walkArmBalanceElbowGain, -135f, -5f);
            rEl = Mathf.Clamp(rEl + walkArmSignal * walkArmBalanceElbowGain, -135f, -5f);
        }
        else if (crouchLevel > 0.001f && standLegLevel < 0.01f)
            ComputeCrouchArmTargets(out lSh, out rSh, out lEl, out rEl, out lWr, out rWr);
        else
        {
            float armSignal = standLegLevel > 0.01f ? 0f : balanceSignal;
            lSh = rSh = Mathf.Clamp(
                shoulderBaseAngle + armShoulderBalanceGain * armSignal, -85f, 85f);
            lEl = rEl = Mathf.Clamp(
                elbowBaseAngle + armElbowBalanceGain * armSignal, -135f, -5f);
            lWr = rWr = Mathf.Clamp(
                wristBaseAngle + armWristBalanceGain * armSignal, -55f, 55f);
        }

        ApplyArmPose(lSh, rSh, lEl, rEl, lWr, rWr);
    }

    // Arms down toward the ground: minus on the shoulder is forward-down; elbow almost straight.
    // Plus on the left shoulder (as on walk) took the arm back and up.
    private void ComputeCrouchArmTargets(out float leftShoulder, out float rightShoulder,
                                         out float leftElbow, out float rightElbow,
                                         out float leftWrist, out float rightWrist)
    {
        float t = Mathf.Clamp01(crouchLevel);
        float sh = crouchArmShoulder * t;
        float leftBaseShoulder = Mathf.Lerp(shoulderBaseAngle, shoulderBaseAngle - sh * 1.08f, t);
        float rightBaseShoulder = Mathf.Lerp(shoulderBaseAngle, shoulderBaseAngle - sh * 0.92f, t);
        float baseElbow = Mathf.Lerp(elbowBaseAngle, crouchArmElbow, t);
        float baseWrist = Mathf.Lerp(wristBaseAngle, crouchArmWrist, t);

        float supportStart = Mathf.Clamp01(crouchHandPoseStart);
        float supportBlend = Mathf.InverseLerp(supportStart, 1f, t);
        float spread = supportBlend * Mathf.Max(0f, crouchHandSupportSpread);
        float centerShoulder = Mathf.Lerp(
            0.5f * (leftBaseShoulder + rightBaseShoulder),
            -Mathf.Abs(crouchHandSupportShoulder),
            supportBlend);
        leftShoulder = centerShoulder - spread;
        rightShoulder = centerShoulder + spread;

        leftElbow = Mathf.Lerp(baseElbow, crouchHandSupportElbow, supportBlend);
        rightElbow = Mathf.Lerp(baseElbow, crouchHandSupportElbow, supportBlend);
        leftWrist = Mathf.Lerp(baseWrist, crouchHandSupportWrist, supportBlend);
        rightWrist = Mathf.Lerp(baseWrist, crouchHandSupportWrist, supportBlend);

        leftShoulder = Mathf.Clamp(leftShoulder, -85f, 85f);
        rightShoulder = Mathf.Clamp(rightShoulder, -85f, 85f);
        leftElbow = Mathf.Clamp(leftElbow, -135f, -5f);
        rightElbow = Mathf.Clamp(rightElbow, -135f, -5f);
        leftWrist = Mathf.Clamp(leftWrist, -55f, 55f);
        rightWrist = Mathf.Clamp(rightWrist, -55f, 55f);
    }

    // Opposite phase: left swing → right shoulder forward (− shoulder).
    private void ComputeWalkArmTargets(out float leftShoulder, out float rightShoulder,
                                       out float leftElbow, out float rightElbow,
                                       out float leftWrist, out float rightWrist)
    {
        leftShoulder = shoulderBaseAngle;
        rightShoulder = shoulderBaseAngle;
        leftElbow = elbowBaseAngle;
        rightElbow = elbowBaseAngle;
        leftWrist = wristBaseAngle;
        rightWrist = wristBaseAngle;

        if (stepPhaseDriver == null || walkArmShoulderSwing < 0.1f)
            return;

        float standSign;
        float swing;
        if (stepPhaseDriver.useFallCatchWalk != 0
            && stepPhaseDriver.FallCatchPhaseCode >= 0)
        {
            // walkArmLegTrack above. Sign is the clock path's own convention:
            // a forward left thigh (hip flexion is minus) must put the RIGHT
            // shoulder forward (minus), so the split enters the right arm
            // unchanged and the left mirrors it. No stance sign is needed —
            // the split already carries which leg is ahead, including the
            // frames where the FSM has no opinion (Plant, restore, surrender),
            // where a symmetric stance reads zero and the arms sit at base.
            if (walkArmLegTrack > 0.001f && leftHipJoint != null && rightHipJoint != null)
            {
                float split = leftHipJoint.jointAngle - rightHipJoint.jointAngle;
                float norm = Mathf.Clamp(
                    split / Mathf.Max(1f, swingHipFlex), -1f, 1f)
                    * Mathf.Clamp01(walkArmLegTrack);
                ApplyWalkArmDelta(
                    walkArmShoulderSwing * norm, Mathf.Abs(norm),
                    out leftShoulder, out rightShoulder,
                    out leftElbow, out rightElbow,
                    out leftWrist, out rightWrist);
                return;
            }
            if (!TryFallCatchArmSwing(out standSign, out swing))
                return;
        }
        else
        {
            if (TryWalkLiftArmSwing(out standSign, out swing))
            {
                ApplyWalkArmSwing(standSign, swing,
                    out leftShoulder, out rightShoulder,
                    out leftElbow, out rightElbow,
                    out leftWrist, out rightWrist);
                return;
            }
            int phase = stepPhaseDriver.PhaseCode;
            float standCmd = intent != null ? intent.standLeg : 0f;

            if (phase == 0 && Mathf.Abs(standCmd) > 0.5f)
            {
                float age = stepPhaseDriver.PhaseAge();
                float dur = Mathf.Max(0.1f, stepPhaseDriver.stanceDuration);
                swing = Mathf.Sin(Mathf.Clamp01(age / dur) * Mathf.PI);
                standSign = standCmd;
            }
            else if (phase == 1 && walkArmTransferCarry > 0.001f
                     && Mathf.Abs(stepPhaseDriver.CurrentStanceSign) > 0.5f)
            {
                // Transfer: decaying swing tail, not a cut to the base pose.
                float age = stepPhaseDriver.PhaseAge();
                float decayDur = Mathf.Min(1.5f, stepPhaseDriver.transferMaxDuration);
                float decay = 1f - Mathf.Clamp01(age / Mathf.Max(0.05f, decayDur));
                swing = walkArmTransferCarry * decay;
                standSign = stepPhaseDriver.CurrentStanceSign;
            }
            else
                return;
        }

        ApplyWalkArmSwing(standSign, swing,
            out leftShoulder, out rightShoulder, out leftElbow, out rightElbow,
            out leftWrist, out rightWrist);
    }

    // Fall-catch arm clock. PhaseCode maps Plant/Fall/Reach → 0 and
    // Catch/Recover → 1, then the legacy path divides PhaseAge by
    // stanceDuration 6 s. Every FSM edge resets that age, so the sine
    // peaks at sin(0.15π/6) ≈ 0.08 — arms look welded. Same contra
    // convention as ApplyWalkArmSwing (left swing → right shoulder minus).
    // Plant stays the quiet pose so restore / first stance do not shove
    // the trunk through the shoulders.
    //
    // fallSwingArmClock: Catch used to restart at swing=1 after Reach had
    // already decayed to transferCarry, and Plant returned false so the
    // 0.05 s Recover tail snapped to quiet — a shoulder yank on the plant
    // (same family as the Catch vault drop). Continue Reach→Catch→Recover
    // →Plant; keep the pre-swap stance sign so the role flip does not
    // reverse the arms mid-decay. Flag 0 = former restart / snap.
    //
    // fallSwingCatchAirArmHold: same Catch PhaseAge reset as the vault
    // yield. Timeout Catch (foot still up) used to lerp reachSwing→carry
    // in the air; Catch→Reach then restored LastReachExtendProgress and
    // snapped the shoulders back. Plant age is 0 until the heel loads;
    // Recover/Plant Catch-end uses LastCatchPlantDuration. Flag 0 is the
    // former Catch PhaseAge (decay in air).
    //
    // fallSwingReachArmClock: Fall already ramps swing on PhaseAge.
    // Reach used to start at 1, so an early lift (the Reach gate) jumped
    // ~0.5→1 on the first air frame — the shoulder pair of the knee
    // fold that reachLiftClock already continues. Same vault clock.
    // Flag 0 is the former Reach start at 1.
    //
    // fallSwingCatchArmClock: Catch still built reachSwing from 1 after
    // Reach had continued the vault clock. A heel before the 0.15 s ramp
    // finishes jumped ~0.5→1 on the first Catch frame — the shoulder pair
    // of catchExtendClock. Freeze LastFall+LastReach as CatchReachFoldDeg
    // does; timeout Catch (clock already 1) is unchanged. Recover/Plant
    // Catch-end uses the same start. Flag 0 is the former Catch start at 1.
    //
    // fallSwingCatchReachArmClock: Catch→Reach still skipped the vault
    // clock (reachArm=1). LastReachExtendProgress holds the envelope
    // only when extend is already high; an early Reach gate then
    // trunkGone Catch (extend 0) holds FallCatchArmReachStart (~0.5)
    // for catchMax, then the bounce lerps from 1. Do not add LastCatch
    // onto the 0.15 s vault clock — air Catch is not arm progress
    // (same as catchAirArmHold) and would saturate to 1 after 0.55 s.
    // Continue the frozen Catch envelope. Timeout-to-Reach from Fall
    // is unchanged. Flag 0 is the former Catch→Reach start at 1.
    // The shin pair of that bounce is fallSwingCatchReachLiftClock;
    // finishing that frozen ramp on the new Reach PhaseAge is
    // fallSwingCatchReachLiftAge. The same stall on the shoulders
    // (reachArm held at FallCatchArmReachStart) is
    // fallSwingCatchReachArmAge. scrapeArmHold then freezes that extraAge
    // on a toe-scrape resume so the bounce does not finish ~0.5→1 on dirt.
    // scrapeArmAirAge then continues extraAge from lift-off (not PhaseAge).
    // scrapeLiftAirEnterFold then skips Lerp(fold, landDeg, extend) on
    // Catch→Reach scrape-air; scrapeLiftAirEnterArm skips the matching
    // Lerp(reachArm, carry, extend) so Last does not quiet the shoulders
    // against that folded shin. scrapeLiftAirCatchArm skips the matching
    // Last-carry in Catch itself (the Catch-phase pair of catchFold).
    // scrapeLiftAirCatchEndArm skips the matching Last-carry in
    // Recover/Plant CatchEnd after Catch flags clear (the Recover/Plant
    // pair of catchArm). scrapeLiftAirCatchGroundArm skips the matching
    // Last-carry on grounded Catch (toe-down after catchArm; plant-clock
    // arrive toward carry, the Catch-phase pair of catchEndArm).
    // scrapeLiftAirCatchAirArm skips the matching Last-carry on a true
    // airborne Catch (never scraped: timeout/abort from Reach with the
    // toe still up; keep reachArm until dirt, extraAge 0).
    // scrapeLiftAirCatchAirEndArm skips the matching Last-carry in
    // Recover/Plant CatchEnd after that keep (scrape latch is scrape-only).
    // scrapeLiftAirCatchAirGroundArm skips the matching Last-carry on
    // grounded never-scrape Catch (catchAirArm is air-only; plant-clock
    // arrive toward carry, the Catch-phase pair of catchAirEndArm).
    // scrapeLiftAirEnterGroundArm skips the matching Last-carry on
    // grounded Reach after enterArm (enterArm is air-only).
    // scrapeLiftAirEnterGroundFold skips the matching fold→landDeg Lerp
    // on grounded Reach after enterFold (enterFold is air-only).
    // scrapeLiftAirCatchGroundFold skips the matching fold→landDeg Lerp
    // on grounded Catch after catchFold (catchFold is air-only).
    // scrapeLiftAirCatchAirGroundFold skips the matching fold→landDeg Lerp
    // on grounded never-scrape Catch after catchAirGroundArm (catchFold /
    // catchGroundFold are scrape-only). scrapeLiftAirCatchAirFold skips
    // the matching fold→landDeg Lerp on never-scrape air Catch after
    // catchAirArm (catchFold is scrape-only). scrapeLiftAirCatchAirEnterFold
    // skips the matching fold→landDeg Lerp on never-scrape Catch→Reach
    // after catchAirFold (enterFold is scrape-only). scrapeLiftAirCatchAirEnterArm
    // skips the matching Last-carry on that same bounce (enterArm is
    // scrape-only). scrapeLiftAirCatchAirEnterGroundArm skips the matching
    // Last-carry on grounded never-scrape Reach (enterGroundArm is
    // scrape-only). scrapeLiftAirCatchAirEnterGroundFold skips the matching
    // fold→landDeg Lerp on that same toe-down (enterGroundFold is
    // scrape-only). scrapeLiftAirCatchAirEnterEndArm skips the matching
    // Last-carry in Recover/Plant CatchEnd after that bounce's planted
    // Catch (RecoveredFromCatchAir needs heel-down during Catch).
    // scrapeLiftAirCatchAirEnterCatchArm skips the matching Last-carry
    // in Catch itself after that planted enter (catchAirGroundArm needs
    // CatchPlantAge < PhaseAge). scrapeLiftAirCatchAirEnterCatchFold skips
    // Max(Last, CatchPlantAge) on that same planted Catch (catchAirGroundFold
    // needs !planted). scrapeLiftAirCatchAirEnterEndFold skips
    // Max(Last, held+PhaseAge) in Recover/Plant after that Catch
    // (CatchReachExtendProgress still Max Last). scrapeLiftAirCatchEndFold
    // skips Max(Last, held+PhaseAge) in Recover/Plant after scrape then
    // air (RecoveredFromCatchScrapeAir). scrapeLiftAirCatchGroundCatchFold
    // skips Max(Last, CatchPlantAge) on planted scrape Catch
    // (catchGroundFold needs !planted). scrapeLiftAirCatchAirEndFold
    // skips Max(Last, held+PhaseAge) in Recover/Plant after never-scrape
    // air (RecoveredFromCatchAir). scrapeLiftAirCatchAirGroundCatchFold
    // skips Max(Last, CatchPlantAge) on planted never-scrape Catch
    // (catchAirGroundFold needs !planted; CatchPlantAge < PhaseAge).
    // scrapeLiftAirCatchPlantExitFold skips Max(Last, held) at Plant→Fall
    // leftover freeze after those Recover skips.
    private bool TryFallCatchArmSwing(out float standSign, out float swing)
    {
        standSign = stepPhaseDriver.CurrentStanceSign;
        swing = 0f;
        if (Mathf.Abs(standSign) < 0.5f)
            return false;

        int phase = stepPhaseDriver.FallCatchPhaseCode;
        float age = stepPhaseDriver.PhaseAge();
        float carry = Mathf.Clamp01(walkArmTransferCarry);
        float ramp = Mathf.Max(0.05f, stepPhaseDriver.catchMinDuration);
        bool clock = stepPhaseDriver.fallSwingArmClock > 0.5f;
        bool plantClock = clock && stepPhaseDriver.fallSwingCatchAirArmHold > 0.5f;
        switch (phase)
        {
            case (int)StepPhaseDriver.FallCatchPhase.Plant:
                if (!clock
                    || stepPhaseDriver.FallCatchCycleCount <= 0
                    || stepPhaseDriver.StandingRestore)
                    return false;
                {
                    float decay = Mathf.Clamp01(
                        (stepPhaseDriver.LastRecoverDuration + age) / ramp);
                    swing = FallCatchArmCatchEnd(carry, plantClock) * (1f - decay);
                    float held = stepPhaseDriver.LastRecoverStanceSign;
                    if (Mathf.Abs(held) > 0.5f)
                        standSign = held;
                    return swing > 0.001f;
                }
            case (int)StepPhaseDriver.FallCatchPhase.Fall:
                swing = Mathf.Clamp01(
                    age / Mathf.Max(0.02f, stepPhaseDriver.fallKneeRampSeconds));
                return true;
            case (int)StepPhaseDriver.FallCatchPhase.Reach:
            {
                // clock uses Last, which Tick already froze on scrapeExtendHold.
                // clock-off still reads live Progress; freeze that too so
                // reachArm→carry does not finish on dirt when armClock is 0.
                float extend = clock
                    ? stepPhaseDriver.LastReachExtendProgress
                    : stepPhaseDriver.ReachExtendProgress();
                if (stepPhaseDriver.ReachResumedFromScrape
                    && stepPhaseDriver.fallSwingCatchReachScrapeExtendHold > 0.5f)
                {
                    extend = stepPhaseDriver.LastReachExtendProgress;
                    // scrapeExtendHold latched Last for the whole resumed
                    // Reach, so a later lift-off never finished 70°→35°
                    // / reachArm→carry. Resume from that lift-off (not
                    // live Progress: scrapeExtend counted dirt as air).
                    // Flag 0 is the former frozen Last even in air.
                    if (stepPhaseDriver.fallSwingCatchReachScrapeExtendAirAge > 0.5f
                        && !stepPhaseDriver.SwingFootIsGrounded)
                        extend = stepPhaseDriver.ScrapeResumeExtendProgress();
                }
                float reachArm = 1f;
                if (clock
                    && stepPhaseDriver.fallSwingReachArmClock > 0.5f)
                {
                    if (!stepPhaseDriver.ReachResumedFromCatch)
                        reachArm = FallCatchVaultClock(
                            stepPhaseDriver, StepPhaseDriver.FallCatchPhase.Reach);
                    else if (stepPhaseDriver.fallSwingCatchReachArmClock > 0.5f)
                    {
                        float extra = stepPhaseDriver.fallSwingCatchReachArmAge > 0.5f
                            ? stepPhaseDriver.PhaseAge()
                            : 0f;
                        if (stepPhaseDriver.ReachResumedFromScrape
                            && stepPhaseDriver.fallSwingCatchReachScrapeArmHold > 0.5f)
                        {
                            extra = 0f;
                            // scrapeArmHold latched extraAge=0 for the
                            // whole resumed Reach, so a later lift-off
                            // never finished ~0.5→1. Resume from that
                            // lift-off (not PhaseAge: scrape time would
                            // saturate the 0.15 s ramp). Flag 0 is the
                            // former extraAge=0 even in air.
                            if (stepPhaseDriver.fallSwingCatchReachScrapeArmAirAge > 0.5f
                                && !stepPhaseDriver.SwingFootIsGrounded)
                                extra = stepPhaseDriver.ScrapeResumeAirAge();
                        }
                        reachArm = FallCatchArmReachStart(stepPhaseDriver, extra);
                    }
                }
                swing = Mathf.Lerp(reachArm, carry, extend);
                // scrapeLiftAirEnterFold kept Reach fold (skip
                // Lerp(fold, landDeg, extend)), but Catch→Reach still
                // Lerped reachArm→carry on the same Last. Last is the
                // Catch-enter freeze (or AirAge resume), not the fold
                // override, so the bounce is ~0.5→carry on the same
                // airborne toe — the shoulder pair of enterFold's 140°
                // keep. Keep reachArm (skip the extend Lerp) until the
                // toe meets dirt. Grounded Reach Last-carry is
                // enterGroundArm. True airborne Catch Last-carry is
                // catchAirArm. Flag 0 is the former carry blend on the bounce.
                if (stepPhaseDriver.fallSwingCatchScrapeLiftAirEnterArm > 0.5f
                    && stepPhaseDriver.fallSwingCatchScrapeLiftAirEnterFold > 0.5f
                    && stepPhaseDriver.fallSwingCatchScrapeLiftAirEnterHold > 0.5f
                    && stepPhaseDriver.fallSwingCatchScrapeLiftAirHold > 0.5f
                    && stepPhaseDriver.fallSwingCatchAirLiftHold > 0.5f
                    && stepPhaseDriver.fallSwingCatchScrapeLiftAirCatchFold > 0.5f
                    && stepPhaseDriver.ReachResumedFromCatchScrapeAir
                    && !stepPhaseDriver.SwingFootIsGrounded)
                    swing = reachArm;
                // scrapeLiftAirEnterArm kept reachArm in air, but grounded
                // Reach still Lerped reachArm→carry on Last. Last is the
                // Catch-enter freeze (or AirAge resume), not the enterArm
                // override, so the first toe-down Reach frame is
                // ~0.5→carry against leftover shin — the dirt sibling of
                // enterArm. Keep reachArm (skip the extend Lerp) on dirt
                // too. ReachResumedFromCatchScrapeAir survives the
                // toe-down. Grounded Reach fold after that keep is
                // enterGroundFold. Grounded never-scrape Catch Last-carry is
                // catchAirGroundArm. Flag 0 is the former Last-carry on grounded Reach.
                if (stepPhaseDriver.fallSwingCatchScrapeLiftAirEnterGroundArm > 0.5f
                    && stepPhaseDriver.fallSwingCatchScrapeLiftAirEnterArm > 0.5f
                    && stepPhaseDriver.fallSwingCatchScrapeLiftAirEnterFold > 0.5f
                    && stepPhaseDriver.fallSwingCatchScrapeLiftAirEnterHold > 0.5f
                    && stepPhaseDriver.fallSwingCatchScrapeLiftAirHold > 0.5f
                    && stepPhaseDriver.fallSwingCatchAirLiftHold > 0.5f
                    && stepPhaseDriver.fallSwingCatchScrapeLiftAirCatchFold > 0.5f
                    && stepPhaseDriver.ReachResumedFromCatchScrapeAir
                    && stepPhaseDriver.SwingFootIsGrounded)
                    swing = reachArm;
                // scrapeLiftAirCatchAirEnterFold kept Reach fold on
                // never-scrape Catch→Reach, but Catch→Reach still Lerped
                // reachArm→carry on Last. enterArm requires
                // ReachResumedFromCatchScrapeAir, so timeout/abort Catch
                // with the toe still up then bouncing to Reach is
                // ~vault→carry against leftover shin — the Catch→Reach
                // shoulder pair of catchAirEnterFold. Keep reachArm
                // (skip the extend Lerp) until the toe meets dirt.
                // extraAge 0 on Catch; the bounce already continues
                // reachArm on Reach PhaseAge (armAge). Heel plant still
                // Lerps. Grounded never-scrape Reach Last-carry is
                // catchAirEnterGroundArm (enterGroundArm is scrape-only).
                // Flag 0 is the former Last-carry on the never-scrape bounce.
                if (stepPhaseDriver.fallSwingCatchScrapeLiftAirCatchAirEnterArm > 0.5f
                    && stepPhaseDriver.fallSwingCatchScrapeLiftAirCatchAirEnterFold > 0.5f
                    && stepPhaseDriver.fallSwingCatchScrapeLiftAirCatchAirFold > 0.5f
                    && stepPhaseDriver.fallSwingCatchScrapeLiftAirCatchAirArm > 0.5f
                    && stepPhaseDriver.fallSwingCatchScrapeLiftAirCatchArm > 0.5f
                    && stepPhaseDriver.ReachResumedFromCatch
                    && !stepPhaseDriver.ReachResumedFromCatchScrapeAir
                    && !stepPhaseDriver.SwingFootIsGrounded)
                    swing = reachArm;
                // scrapeLiftAirCatchAirEnterArm kept reachArm in air, but
                // grounded never-scrape Reach still Lerped reachArm→carry
                // on Last. enterGroundArm requires
                // ReachResumedFromCatchScrapeAir, so timeout/abort Catch
                // with the toe still up then bouncing to Reach then
                // toe-down is ~vault→carry against leftover shin — the
                // dirt sibling of catchAirEnterArm. Keep reachArm (skip
                // the extend Lerp) on dirt too. ReachResumedFromCatch
                // survives the toe-down. Heel plant still leaves Reach
                // for Catch. Grounded never-scrape Reach fold after
                // that keep is catchAirEnterGroundFold (enterGroundFold
                // is scrape-only). Flag 0 is the former Last-carry on
                // grounded never-scrape Reach. Recover/Plant CatchEnd
                // Last-carry after that bounce's planted Catch is
                // fallSwingCatchScrapeLiftAirCatchAirEnterEndArm.
                // Catch-phase Last-carry on that planted Catch is
                // fallSwingCatchScrapeLiftAirCatchAirEnterCatchArm.
                // Catch-phase shin Max(Last) on that planted Catch is
                // fallSwingCatchScrapeLiftAirCatchAirEnterCatchFold.
                // Recover/Plant shin Max(Last) after that Catch is
                // fallSwingCatchScrapeLiftAirCatchAirEnterEndFold.
                // Catch planted scrape shin Max(Last) is
                // fallSwingCatchScrapeLiftAirCatchGroundCatchFold.
                // Never-scrape air Recover/Plant shin Max(Last) is
                // fallSwingCatchScrapeLiftAirCatchAirEndFold.
                // Catch planted never-scrape shin Max(Last) is
                // fallSwingCatchScrapeLiftAirCatchAirGroundCatchFold.
                if (stepPhaseDriver.fallSwingCatchScrapeLiftAirCatchAirEnterGroundArm > 0.5f
                    && stepPhaseDriver.fallSwingCatchScrapeLiftAirCatchAirEnterArm > 0.5f
                    && stepPhaseDriver.fallSwingCatchScrapeLiftAirCatchAirEnterFold > 0.5f
                    && stepPhaseDriver.fallSwingCatchScrapeLiftAirCatchAirFold > 0.5f
                    && stepPhaseDriver.fallSwingCatchScrapeLiftAirCatchAirArm > 0.5f
                    && stepPhaseDriver.fallSwingCatchScrapeLiftAirCatchArm > 0.5f
                    && stepPhaseDriver.ReachResumedFromCatch
                    && !stepPhaseDriver.ReachResumedFromCatchScrapeAir
                    && stepPhaseDriver.SwingFootIsGrounded)
                    swing = reachArm;
                return true;
            }
            case (int)StepPhaseDriver.FallCatchPhase.Catch:
                if (clock)
                {
                    // scrapeLiftAirCatchFold kept Reach fold in Catch, but
                    // Catch still Lerped reachArm→carry on Last, then toward
                    // carry on catchAge. Last is the Catch-enter freeze (or
                    // AirAge resume), not the fold override, so the bounce
                    // is ~0.5→carry on the same airborne toe — the shoulder
                    // pair of catchFold's 140° keep. Keep reachArm (skip
                    // both Last and catchAge Lerps) until the toe meets
                    // dirt. extraAge from lift-off so scrape PhaseAge does
                    // not saturate the 0.15 s ramp. Grounded Catch Last-carry
                    // is catchGroundArm. True airborne Catch Last-carry is
                    // catchAirArm. Flag 0 is the former Last-carry in Catch.
                    // Catch→Reach keep is fallSwingCatchScrapeLiftAirEnterArm.
                    // Never-scrape Catch→Reach keep is
                    // fallSwingCatchScrapeLiftAirCatchAirEnterArm.
                    // Recover/Plant CatchEnd Last-carry after Catch flags
                    // clear is fallSwingCatchScrapeLiftAirCatchEndArm.
                    bool catchScrapeAirArm =
                        stepPhaseDriver.fallSwingCatchScrapeLiftAirCatchArm > 0.5f
                        && stepPhaseDriver.fallSwingCatchScrapeLiftAirCatchFold > 0.5f
                        && stepPhaseDriver.fallSwingCatchAirLiftHold > 0.5f
                        && stepPhaseDriver.fallSwingCatchScrapeLiftAirHold > 0.5f
                        && !stepPhaseDriver.SwingFootIsGrounded
                        && (stepPhaseDriver.CatchScrapedThisPhase
                            || (stepPhaseDriver.CatchResumedFromReachScrapeAir
                                && stepPhaseDriver.fallSwingCatchScrapeLiftAirCatchHold > 0.5f));
                    float extra = 0f;
                    if (catchScrapeAirArm && stepPhaseDriver.ScrapeResumeAirActive)
                        extra = stepPhaseDriver.ScrapeResumeAirAge();
                    if (catchScrapeAirArm)
                    {
                        swing = FallCatchArmReachStart(stepPhaseDriver, extra);
                        return true;
                    }
                    // scrapeLiftAirCatchArm kept reachArm on scrape then
                    // air, but a true airborne Catch (never scraped) still
                    // Lerped reachArm→carry on Last. Last is the Reach
                    // freeze at Catch enter, not a scrape: timeout/abort
                    // Catch with the toe still up is ~vault→carry against
                    // the catching shin — quiet shoulders before the heel
                    // meets dirt. Keep reachArm (skip Last) until the toe
                    // meets dirt. extraAge stays 0: air Catch is not arm
                    // progress (same as catchArmClock). Recover/Plant
                    // CatchEnd after that keep is catchAirEndArm.
                    // Grounded never-scrape Catch Last-carry is
                    // catchAirGroundArm. Shin pair of this air keep is
                    // catchAirFold. Grounded Reach Last-carry after
                    // enterArm is enterGroundArm. Grounded Reach fold after
                    // that keep is enterGroundFold. Flag 0 is the former
                    // Last-carry on never-scrape air. scrape-air keep is catchArm.
                    bool catchAirArm =
                        stepPhaseDriver.fallSwingCatchScrapeLiftAirCatchAirArm > 0.5f
                        && stepPhaseDriver.fallSwingCatchScrapeLiftAirCatchArm > 0.5f
                        && !stepPhaseDriver.SwingFootIsGrounded
                        && !stepPhaseDriver.CatchScrapedThisPhase
                        && !stepPhaseDriver.CatchResumedFromReachScrapeAir;
                    if (catchAirArm)
                    {
                        swing = FallCatchArmReachStart(stepPhaseDriver);
                        return true;
                    }
                    // scrapeLiftAirCatchArm kept reachArm in air, but
                    // grounded Catch still Lerped reachArm→carry on Last
                    // (then toward carry on CatchPlantAge). Last is the
                    // Catch-enter freeze, not the catchArm override, so
                    // the first toe-down frame is ~0.5→carry against
                    // leftover shin — Recover catchEndArm then skipLast
                    // would snap back to reachArm. Skip Last and arrive
                    // on plant-clock (0 until heel) so CatchEnd continues
                    // the same envelope. Shin pair of this skip is
                    // catchGroundFold. Grounded never-scrape Catch
                    // Last-carry is catchAirGroundArm. Flag 0 is the
                    // former Last-carry on dirt.
                    bool catchGroundArm =
                        stepPhaseDriver.fallSwingCatchScrapeLiftAirCatchGroundArm > 0.5f
                        && stepPhaseDriver.fallSwingCatchScrapeLiftAirCatchEndArm > 0.5f
                        && stepPhaseDriver.fallSwingCatchScrapeLiftAirCatchArm > 0.5f
                        && stepPhaseDriver.fallSwingCatchScrapeLiftAirCatchFold > 0.5f
                        && stepPhaseDriver.fallSwingCatchAirLiftHold > 0.5f
                        && stepPhaseDriver.fallSwingCatchScrapeLiftAirHold > 0.5f
                        && plantClock
                        && stepPhaseDriver.SwingFootIsGrounded
                        && (stepPhaseDriver.CatchScrapedThisPhase
                            || (stepPhaseDriver.CatchResumedFromReachScrapeAir
                                && stepPhaseDriver.fallSwingCatchScrapeLiftAirCatchHold > 0.5f));
                    if (catchGroundArm)
                    {
                        swing = Mathf.Lerp(
                            FallCatchArmReachStart(stepPhaseDriver),
                            carry,
                            Mathf.Clamp01(stepPhaseDriver.CatchPlantAge() / ramp));
                        return true;
                    }
                    // scrapeLiftAirCatchAirArm kept reachArm in never-scrape
                    // air, but grounded never-scrape Catch still Lerped
                    // reachArm→carry on Last (then toward carry on
                    // CatchPlantAge). Last is the Reach freeze at Catch
                    // enter, not the catchAirArm override, so the first
                    // toe-down after a fly is ~vault→carry against leftover
                    // shin — Recover catchAirEndArm then skipLast would
                    // snap back to reachArm. Skip Last and arrive on
                    // plant-clock (0 until heel) so CatchEnd continues the
                    // same envelope. Heel-only Catch (CatchPlantAge tracks
                    // PhaseAge: planted at enter) does not skip. Grounded
                    // Reach fold after enterGroundArm is enterGroundFold.
                    // Grounded never-scrape Catch fold after this keep is
                    // catchAirGroundFold. Heel plant skip Last on that shin
                    // is catchAirGroundCatchFold. Flag 0 is the former
                    // Last-carry on dirt after never-scrape air. scrape
                    // dirt is catchGroundArm.
                    bool catchAirGroundArm =
                        stepPhaseDriver.fallSwingCatchScrapeLiftAirCatchAirGroundArm > 0.5f
                        && stepPhaseDriver.fallSwingCatchScrapeLiftAirCatchAirEndArm > 0.5f
                        && stepPhaseDriver.fallSwingCatchScrapeLiftAirCatchAirArm > 0.5f
                        && stepPhaseDriver.fallSwingCatchScrapeLiftAirCatchArm > 0.5f
                        && plantClock
                        && stepPhaseDriver.SwingFootIsGrounded
                        && !stepPhaseDriver.CatchScrapedThisPhase
                        && !stepPhaseDriver.CatchResumedFromReachScrapeAir
                        && stepPhaseDriver.CatchPlantAge() + 0.0001f < age;
                    if (catchAirGroundArm)
                    {
                        swing = Mathf.Lerp(
                            FallCatchArmReachStart(stepPhaseDriver),
                            carry,
                            Mathf.Clamp01(stepPhaseDriver.CatchPlantAge() / ramp));
                        return true;
                    }
                    // scrapeLiftAirCatchAirEnterEndArm already starts
                    // Recover/Plant CatchEnd from reachArm after planted
                    // Catch enter. Catch-phase still Lerped reachArm→carry
                    // on Last (then toward carry on CatchPlantAge).
                    // catchAirGroundArm needs CatchPlantAge < PhaseAge, and
                    // planted-at-enter has catchPlantEnterTime ==
                    // phaseEnterTime, so the first Catch frame after the
                    // bounce is ~vault→carry against leftover shin — Recover
                    // skipLast would snap back to reachArm. Skip Last and
                    // arrive on plant-clock from reachArm so CatchEnd
                    // continues the same envelope. CatchResumedFromReachAir
                    // is live during Catch (cleared on Catch exit). Flag 0
                    // is the former Last-carry on planted Catch after
                    // never-scrape Catch→Reach. Catch-phase shin skip Last
                    // on that planted Catch is catchAirEnterCatchFold
                    // (catchAirGroundFold needs !planted). Recover/Plant
                    // shin skip Last after that Catch is catchAirEnterEndFold
                    // (CatchReachExtendProgress still Max Last).
                    bool catchAirEnterCatchArm =
                        stepPhaseDriver.fallSwingCatchScrapeLiftAirCatchAirEnterCatchArm > 0.5f
                        && stepPhaseDriver.fallSwingCatchScrapeLiftAirCatchAirEnterEndArm > 0.5f
                        && stepPhaseDriver.fallSwingCatchScrapeLiftAirCatchAirEnterGroundFold > 0.5f
                        && stepPhaseDriver.fallSwingCatchScrapeLiftAirCatchAirEnterGroundArm > 0.5f
                        && stepPhaseDriver.fallSwingCatchScrapeLiftAirCatchAirEnterArm > 0.5f
                        && stepPhaseDriver.fallSwingCatchScrapeLiftAirCatchAirEnterFold > 0.5f
                        && stepPhaseDriver.fallSwingCatchScrapeLiftAirCatchAirFold > 0.5f
                        && stepPhaseDriver.fallSwingCatchScrapeLiftAirCatchAirArm > 0.5f
                        && stepPhaseDriver.fallSwingCatchScrapeLiftAirCatchArm > 0.5f
                        && plantClock
                        && stepPhaseDriver.SwingFootIsGrounded
                        && stepPhaseDriver.CatchResumedFromReachAir
                        && !stepPhaseDriver.CatchScrapedThisPhase
                        && !stepPhaseDriver.CatchResumedFromReachScrapeAir;
                    if (catchAirEnterCatchArm)
                    {
                        swing = Mathf.Lerp(
                            FallCatchArmReachStart(stepPhaseDriver),
                            carry,
                            Mathf.Clamp01(stepPhaseDriver.CatchPlantAge() / ramp));
                        return true;
                    }
                    float reachSwing = Mathf.Lerp(
                        FallCatchArmReachStart(stepPhaseDriver),
                        carry,
                        stepPhaseDriver.LastReachExtendProgress);
                    float catchAge = plantClock
                        ? stepPhaseDriver.CatchPlantAge()
                        : age;
                    swing = Mathf.Lerp(
                        reachSwing, carry, Mathf.Clamp01(catchAge / ramp));
                    return true;
                }
                swing = Mathf.Lerp(
                    1f,
                    carry,
                    Mathf.Clamp01(age / ramp));
                return true;
            case (int)StepPhaseDriver.FallCatchPhase.Recover:
                if (clock)
                {
                    swing = FallCatchArmCatchEnd(carry, plantClock)
                        * (1f - Mathf.Clamp01(age / ramp));
                    return swing > 0.001f;
                }
                swing = carry * (1f - Mathf.Clamp01(age / ramp));
                return swing > 0.001f;
            default:
                return false;
        }
    }

    // Frozen Fall+Reach vault clock. Catch and Catch→Reach read this so
    // a short Fall (the early-lift Reach gate) does not jump the shin or
    // the arms to 1. LastCatch stays off: air Catch is not lift/arm
    // progress and would saturate the 0.15 s ramp after catchMax.
    // extraAge is the resumed Reach PhaseAge (fallSwingCatchReachLiftAge
    // on the shin, fallSwingCatchReachArmAge on the shoulders,
    // fallSwingCatchReachVaultAge on the trailing strut) so the freeze
    // does not stall the remaining 70°→140° fold / ~0.5→1 envelope /
    // leftover vault. scrapeLiftHold / scrapeVaultHold / scrapeArmHold
    // pass 0 on a toe-scrape resume: that extraAge would finish the fold /
    // leftover→0 / ~0.5→1 envelope on dirt. scrapeLiftAirAge then
    // continues fold extraAge from lift-off (not PhaseAge). scrapeVaultAirAge
    // continues leftover vault extraAge from that same lift-off (yield
    // remain on dirt). scrapeArmAirAge continues arm extraAge from that
    // same lift-off. scrapeExtendHold then freezes
    // the Lerp(fold, landDeg, extend) / arm-carry parameter (scrapeExtend
    // still ticks the air clock). scrapeExtendAirAge then continues that
    // Last from lift-off (not live Progress). CatchReachFoldDeg still
    // passes 0: Catch must not keep folding.
    private static float FallCatchFallReachClock(StepPhaseDriver driver)
    {
        return FallCatchFallReachClock(driver, 0f);
    }

    private static float FallCatchFallReachClock(StepPhaseDriver driver, float extraAge)
    {
        float ramp = Mathf.Max(0.02f, driver.fallKneeRampSeconds);
        return Mathf.Clamp01(
            (driver.LastFallDuration + driver.LastReachDuration
             + Mathf.Max(0f, extraAge)) / ramp);
    }

    // Catch used to lerp from 1. Reach with reachArmClock is already
    // Lerp(vaultClock, carry, extend); a heel before the ramp finishes
    // then jumped ~0.5→1. Same frozen LastFall+LastReach as the shin fold.
    // Catch→Reach reads this same start (fallSwingCatchReachArmClock)
    // instead of 1: LastCatch must not join the 0.15 s vault clock.
    // extraAge is the resumed Reach PhaseAge (fallSwingCatchReachArmAge)
    // so the freeze does not stall the remaining ~0.5→1 envelope.
    // scrapeArmHold passes 0 on a toe-scrape resume. scrapeArmAirAge then
    // continues extraAge from lift-off (not PhaseAge). Recover still
    // passes 0: landing Catch is not arm progress. Catch scrape-air
    // (catchArm) passes lift-off extraAge so Last-carry does not quiet
    // the shoulders against catchFold.
    // scrapeLiftAirEnterArm then skips Lerp(this start, carry, extend)
    // on Catch→Reach scrape-air (the shoulder pair of enterFold).
    // scrapeLiftAirCatchAirEnterArm skips Lerp(this start, carry, extend)
    // on never-scrape Catch→Reach (the shoulder pair of catchAirEnterFold;
    // enterArm is scrape-only).
    // scrapeLiftAirCatchArm skips Lerp(this start, carry, Last) in Catch
    // scrape-air (the Catch-phase shoulder pair of catchFold), passing
    // extraAge from lift-off. scrapeLiftAirCatchEndArm skips Lerp(this
    // start, carry, Last) in Recover/Plant CatchEnd after that keep
    // (plant-clock arrive still blends toward carry).
    // scrapeLiftAirCatchGroundArm skips Lerp(this start, carry, Last) on
    // grounded Catch after that keep (plant-clock arrive from reachArm).
    // scrapeLiftAirCatchAirArm skips Lerp(this start, carry, Last) on a
    // true airborne Catch (never scraped; extraAge 0).
    // scrapeLiftAirCatchAirEndArm skips Lerp(this start, carry, Last) in
    // Recover/Plant CatchEnd after that keep (plant-clock arrive still
    // blends toward carry).
    // scrapeLiftAirCatchAirGroundArm skips Lerp(this start, carry, Last)
    // on grounded never-scrape Catch after that keep (plant-clock arrive
    // from reachArm).
    // scrapeLiftAirEnterGroundArm skips Lerp(this start, carry, extend)
    // on grounded Reach after enterArm (enterArm is air-only).
    // scrapeLiftAirEnterGroundFold is the shin pair of that keep
    // (fold→landDeg on the same toe-down).
    // scrapeLiftAirCatchGroundFold is the shin pair of catchGroundArm
    // (fold→landDeg on grounded Catch after catchFold).
    // scrapeLiftAirCatchAirGroundFold is the shin pair of catchAirGroundArm
    // (fold→landDeg on grounded never-scrape Catch after that keep).
    // scrapeLiftAirCatchAirFold is the shin pair of catchAirArm
    // (fold→landDeg on never-scrape air Catch).
    // scrapeLiftAirCatchAirEnterFold is the Catch→Reach shin pair of
    // catchAirFold (fold→landDeg on the never-scrape bounce; enterFold
    // is scrape-only).
    // scrapeLiftAirCatchAirEnterArm is the Catch→Reach shoulder pair of
    // that shin keep (Last-carry on the same bounce; enterArm is
    // scrape-only).
    // scrapeLiftAirCatchAirEnterGroundArm skips Lerp(this start, carry,
    // extend) on grounded never-scrape Reach after that keep
    // (enterGroundArm is scrape-only).
    // scrapeLiftAirCatchAirEnterGroundFold is the shin pair of that dirt
    // keep (fold→landDeg on the same toe-down; enterGroundFold is
    // scrape-only).
    // scrapeLiftAirCatchAirEnterEndArm skips Lerp(this start, carry, Last)
    // in Recover/Plant CatchEnd after that bounce's planted Catch
    // (RecoveredFromCatchAir needs heel-down during Catch).
    // scrapeLiftAirCatchAirEnterCatchArm skips Lerp(this start, carry, Last)
    // in Catch itself after that planted enter (CatchResumedFromReachAir;
    // catchAirGroundArm needs CatchPlantAge < PhaseAge).
    // scrapeLiftAirCatchAirEnterCatchFold skips Max(Last, CatchPlantAge)
    // in CatchReachExtendProgress on that same planted Catch
    // (catchAirGroundFold needs !planted).
    // scrapeLiftAirCatchAirEnterEndFold skips Max(Last, held+PhaseAge)
    // in Recover/Plant after that Catch (Last is the Reach freeze).
    // scrapeLiftAirCatchEndFold skips Max(Last, held+PhaseAge) in
    // Recover/Plant after scrape then air (RecoveredFromCatchScrapeAir).
    // scrapeLiftAirCatchGroundCatchFold skips Max(Last, CatchPlantAge) on
    // planted scrape Catch (catchGroundFold needs !planted).
    // scrapeLiftAirCatchGroundCatchEndFold skips Max(Last, held+PhaseAge)
    // in Recover/Plant after never-air scrape (RecoveredFromCatchGroundCatch).
    // scrapeLiftAirCatchAirEndFold skips Max(Last, held+PhaseAge) in
    // Recover/Plant after never-scrape air (RecoveredFromCatchAir).
    // scrapeLiftAirCatchAirGroundCatchFold skips Max(Last, CatchPlantAge)
    // on planted never-scrape Catch (catchAirGroundFold needs !planted).
    // Flag 0 / reachArmClock 0 keeps the former start at 1.
    private static float FallCatchArmReachStart(StepPhaseDriver driver)
    {
        return FallCatchArmReachStart(driver, 0f);
    }

    private static float FallCatchArmReachStart(StepPhaseDriver driver, float extraAge)
    {
        if (driver.fallSwingArmClock <= 0.5f
            || driver.fallSwingReachArmClock <= 0.5f
            || driver.fallSwingCatchArmClock <= 0.5f)
            return 1f;
        return FallCatchFallReachClock(driver, extraAge);
    }

    // Catch exit envelope: Reach end blended toward transferCarry over
    // catchMin. Recover/Plant decay from this, not from a fresh 1.
    // plantClock counts only heel-loaded Catch time — air Catch is not
    // arrival at carry (same reason CatchPlantAge is 0 until plant).
    // Catch scrape-air keep (skip Last-carry) is catchArm; Recover/Plant
    // CatchEnd skip Last (start from reachArm, plant-clock arrive
    // unchanged) is catchEndArm. Grounded Catch skip Last (same plant-
    // clock arrive from reachArm) is catchGroundArm. True airborne Catch
    // skip Last (keep reachArm until dirt) is catchAirArm. Shin pair of
    // that air keep is catchAirFold. Recover/Plant
    // skip Last after that keep is catchAirEndArm. Grounded never-scrape
    // Catch skip Last (same plant-clock arrive from reachArm) is
    // catchAirGroundArm. Shin pair of that dirt skip is catchAirGroundFold.
    // Grounded Reach skip
    // Last after enterArm is enterGroundArm. Grounded Reach fold after
    // that keep is enterGroundFold. Never-scrape Catch→Reach skip Last
    // after catchAirEnterFold is catchAirEnterArm (enterArm is
    // scrape-only). Grounded never-scrape Reach skip Last after that
    // keep is catchAirEnterGroundArm (enterGroundArm is scrape-only).
    // Grounded never-scrape Reach fold after that keep is
    // catchAirEnterGroundFold (enterGroundFold is scrape-only).
    // Recover/Plant skip Last after that bounce's planted Catch is
    // catchAirEnterEndArm (RecoveredFromCatchAir needs heel-down during
    // Catch). Catch-phase skip Last on that planted Catch is
    // catchAirEnterCatchArm (CatchResumedFromReachAir; catchAirGroundArm
    // needs CatchPlantAge < PhaseAge). Catch-phase shin skip Last on
    // that planted Catch is catchAirEnterCatchFold (catchAirGroundFold
    // needs !planted). Recover/Plant shin skip Last after that Catch is
    // catchAirEnterEndFold (CatchReachExtendProgress still Max Last).
    // Recover/Plant shin skip Last after scrape then air is catchEndFold
    // (CatchReachExtendProgress Recover/Plant still Max Last).
    // Catch planted scrape shin skip Last is catchGroundCatchFold
    // (catchGroundFold needs !planted). Recover/Plant shin skip Last
    // after never-scrape air Catch is catchAirEndFold
    // (CatchReachExtendProgress Recover/Plant still Max Last).
    // Catch planted never-scrape shin skip Last is catchAirGroundCatchFold
    // (catchAirGroundFold needs !planted; CatchPlantAge < PhaseAge).
    // Recover/Plant shin skip Last after never-air scrape is
    // catchGroundCatchEndFold (CatchReachExtendProgress Recover/Plant
    // still Max Last). FallCatchArmCatchEnd skip Last on that same Recover
    // is catchEndArm (RecoveredFromCatchGroundCatch).
    // Plant→Fall leftover skip Last after those Recover skips is
    // catchPlantExitFold.
    // Flag 0 / no
    // RecoveredFromCatchScrapeAir / RecoveredFromCatchAir /
    // RecoveredFromCatchAirEnter / RecoveredFromCatchGroundCatch is the former
    // Last blend.
    private float FallCatchArmCatchEnd(float carry, bool plantClock)
    {
        float reachStart = FallCatchArmReachStart(stepPhaseDriver);
        bool skipLast = (stepPhaseDriver.fallSwingCatchScrapeLiftAirCatchEndArm > 0.5f
                && (stepPhaseDriver.RecoveredFromCatchScrapeAir
                    || stepPhaseDriver.RecoveredFromCatchGroundCatch))
            || (stepPhaseDriver.fallSwingCatchScrapeLiftAirCatchAirEndArm > 0.5f
                && stepPhaseDriver.RecoveredFromCatchAir)
            || (stepPhaseDriver.fallSwingCatchScrapeLiftAirCatchAirEnterEndArm > 0.5f
                && stepPhaseDriver.RecoveredFromCatchAirEnter);
        float reachSwing = skipLast
            ? reachStart
            : Mathf.Lerp(
                reachStart,
                carry,
                stepPhaseDriver.LastReachExtendProgress);
        float held = plantClock
            ? stepPhaseDriver.LastCatchPlantDuration
            : stepPhaseDriver.LastCatchDuration;
        float arrive = Mathf.Clamp01(
            held / Mathf.Max(0.05f, stepPhaseDriver.catchMinDuration));
        return Mathf.Lerp(reachSwing, carry, arrive);
    }

    private void ApplyWalkArmSwing(float standSign, float swing,
                                   out float leftShoulder, out float rightShoulder,
                                   out float leftElbow, out float rightElbow,
                                   out float leftWrist, out float rightWrist)
    {
        float amp = walkArmShoulderSwing * swing;
        float contra = -Mathf.Sign(standSign);
        ApplyWalkArmDelta(contra * amp, swing,
            out leftShoulder, out rightShoulder,
            out leftElbow, out rightElbow,
            out leftWrist, out rightWrist);
    }

    // Signed contra-swing pose. rightDelta is the right shoulder offset in
    // degrees (minus = forward, the convention above) and the left arm mirrors
    // it; swingMag is that offset as a fraction of walkArmShoulderSwing and
    // only scales the elbow/wrist share. Both callers — the phase clock and
    // the measured thigh split — hand in their own number and get the same
    // pose out, so the two differ in the signal only, not in the pose.
    private void ApplyWalkArmDelta(float rightDelta, float swingMag,
                                   out float leftShoulder, out float rightShoulder,
                                   out float leftElbow, out float rightElbow,
                                   out float leftWrist, out float rightWrist)
    {
        leftShoulder = shoulderBaseAngle;
        rightShoulder = shoulderBaseAngle;
        leftElbow = elbowBaseAngle;
        rightElbow = elbowBaseAngle;
        leftWrist = wristBaseAngle;
        rightWrist = wristBaseAngle;

        if (swingMag < 0.001f)
            return;

        float leftDelta = -rightDelta;

        leftShoulder = Mathf.Clamp(shoulderBaseAngle + leftDelta, -85f, 85f);
        rightShoulder = Mathf.Clamp(shoulderBaseAngle + rightDelta, -85f, 85f);

        if (walkArmElbowSwing > 0.01f)
        {
            float elAmp = walkArmElbowSwing * swingMag;
            if (rightDelta < -0.01f)
                rightElbow = Mathf.Clamp(elbowBaseAngle - elAmp, -135f, -5f);
            if (leftDelta < -0.01f)
                leftElbow = Mathf.Clamp(elbowBaseAngle - elAmp, -135f, -5f);
        }

        leftWrist = Mathf.Clamp(wristBaseAngle + leftDelta * 0.15f, -55f, 55f);
        rightWrist = Mathf.Clamp(wristBaseAngle + rightDelta * 0.15f, -55f, 55f);
    }

    private void ApplyArmPose(float leftShoulder, float rightShoulder,
                              float leftElbow, float rightElbow,
                              float leftWrist, float rightWrist)
    {
        ControlJointToAngleStable(
            leftShoulderJoint, leftShoulder, shoulderPGain, shoulderDGain,
            shoulderErrorReferenceDegrees, leftShoulderFlexor, leftShoulderExtensor,
            ref leftShoulderEffectiveInertia);
        ControlJointToAngleStable(
            rightShoulderJoint, rightShoulder, shoulderPGain, shoulderDGain,
            shoulderErrorReferenceDegrees, rightShoulderFlexor, rightShoulderExtensor,
            ref rightShoulderEffectiveInertia);

        ControlJointToAngleStable(
            leftElbowJoint, leftElbow, elbowPGain, elbowDGain,
            elbowErrorReferenceDegrees, leftElbowFlexor, leftElbowExtensor,
            ref leftElbowEffectiveInertia);
        ControlJointToAngleStable(
            rightElbowJoint, rightElbow, elbowPGain, elbowDGain,
            elbowErrorReferenceDegrees, rightElbowFlexor, rightElbowExtensor,
            ref rightElbowEffectiveInertia);

        ControlJointToAngleStable(
            leftWristJoint, leftWrist, wristPGain, wristDGain,
            wristErrorReferenceDegrees, leftWristFlexor, leftWristExtensor,
            ref leftWristEffectiveInertia);
        ControlJointToAngleStable(
            rightWristJoint, rightWrist, wristPGain, wristDGain,
            wristErrorReferenceDegrees, rightWristFlexor, rightWristExtensor,
            ref rightWristEffectiveInertia);
    }

    // Stable PD to a joint angle. Same formula as the neck, sign as
    // ControlJointToAngle: P*error − D*speed. Denominator 1+K·Δt/I always,
    // no useStablePd flag: else elbow and wrist enter a limit cycle.
    // Returns the applied signal so a caller can cancel its third-law
    // reaction elsewhere (swing hip -> stance hip). Ignoring it is fine.

    // Predicted SPD signal without ApplySignal. Walk-lift couple reads
    // this step's swing-hip command before the stance hip fires, so the
    // pelvis reaction is cancelled on the same FixedUpdate. Does not
    // change the applied SPD formula.
    private float PredictStableJointSignal(HingeJoint2D joint, float targetAngle,
                                          float pGain, float dGain, float errorRef,
                                          Muscle flexor, Muscle extensor, ref float cachedInertia)
    {
        if (joint == null || flexor == null || extensor == null) return 0f;

        if (joint.useLimits)
            targetAngle = Mathf.Clamp(targetAngle, joint.limits.min, joint.limits.max);

        float dt = Time.fixedDeltaTime;
        float angleForError = joint.jointAngle + joint.jointSpeed * dt;

        float error = Mathf.DeltaAngle(angleForError, targetAngle) / Mathf.Max(1f, errorRef);
        float speed = joint.jointSpeed / speedReferenceDegPerSec;
        float raw = pGain * error - dGain * speed;
        float driveTorque = flexor.maxTorque;

        float damping = 0f;

        if (cachedInertia <= 0f)
            cachedInertia = EffectiveInertia(joint);

        if (cachedInertia > 0f)
        {
            float k = dGain * driveTorque * Mathf.Rad2Deg
                      / Mathf.Max(1f, speedReferenceDegPerSec);
            damping = k * dt / cachedInertia;
        }

        return Mathf.Clamp(raw / (1f + damping), -1f, 1f);
    }

    private float ControlJointToAngleStable(HingeJoint2D joint, float targetAngle,
                                           float pGain, float dGain, float errorRef,
                                           Muscle flexor, Muscle extensor, ref float cachedInertia)
    {
        return ControlJointToAngleStable(joint, targetAngle, pGain, dGain, errorRef,
            flexor, extensor, ref cachedInertia, false);
    }

    // directionalDriveTorque: build the denominator from the muscle this step
    // will actually engage instead of always from the flexor. See
    // walkSwingAnkleSpdTorque — the ankle is the only asymmetric pair in the
    // body, so on every other joint the two are the same float and this
    // argument changes nothing.
    private float ControlJointToAngleStable(HingeJoint2D joint, float targetAngle,
                                           float pGain, float dGain, float errorRef,
                                           Muscle flexor, Muscle extensor, ref float cachedInertia,
                                           bool directionalDriveTorque)
    {
        if (joint == null || flexor == null || extensor == null) return 0f;

        if (joint.useLimits)
            targetAngle = Mathf.Clamp(targetAngle, joint.limits.min, joint.limits.max);

        float dt = Time.fixedDeltaTime;
        float angleForError = joint.jointAngle + joint.jointSpeed * dt;

        float error = Mathf.DeltaAngle(angleForError, targetAngle) / Mathf.Max(1f, errorRef);
        float speed = joint.jointSpeed / speedReferenceDegPerSec;
        float raw = pGain * error - dGain * speed;
        // ApplySignal sends a positive signal to the extensor, and the sign of
        // the signal is the sign of raw because the denominator below is
        // positive — so the acting muscle is known before the division.
        float driveTorque = directionalDriveTorque && raw > 0f
            ? extensor.maxTorque
            : flexor.maxTorque;

        float damping = 0f;

        if (cachedInertia <= 0f)
            cachedInertia = EffectiveInertia(joint);

        if (cachedInertia > 0f)
        {
            float k = dGain * driveTorque * Mathf.Rad2Deg
                      / Mathf.Max(1f, speedReferenceDegPerSec);
            damping = k * dt / cachedInertia;
        }

        float signal = Mathf.Clamp(raw / (1f + damping), -1f, 1f);
        ApplySignal(signal, flexor, extensor);
        return signal;
    }

    // MTP pose. Plus = toe down relative to the heel (push-off / heel
    // rise). Minus = toe up (swing clearance). Zero is a flat plate.
    // SPD, same formula as the other light hinges. Not the ankle CoM loop.
    private void DriveToeJoint(bool left, float targetDeg)
    {
        HingeJoint2D joint = left ? leftToeJoint : rightToeJoint;
        Muscle flexor = left ? leftToeFlexor : rightToeFlexor;
        Muscle extensor = left ? leftToeExtensor : rightToeExtensor;
        if (joint == null || flexor == null || extensor == null) return;
        if (left)
            ControlJointToAngleStable(joint, targetDeg, toePGain, toeDGain,
                toeErrorReferenceDegrees, flexor, extensor, ref leftToeEffectiveInertia);
        else
            ControlJointToAngleStable(joint, targetDeg, toePGain, toeDGain,
                toeErrorReferenceDegrees, flexor, extensor, ref rightToeEffectiveInertia);
    }

    // Paired joint inertia: both bodies rotate toward each other, so the
    // denominator is 1/(1/I₁ + 1/I₂), not one segment's inertia. The head
    // is tied to a light neck, and paired inertia is a third of own — using
    // one body understates stiffness by a factor of three.
    private static float EffectiveInertia(HingeJoint2D joint)
    {
        if (joint == null) return 0f;

        Rigidbody2D self = joint.attachedRigidbody;
        Rigidbody2D connected = joint.connectedBody;
        float a = self != null ? self.inertia : 0f;
        float b = connected != null ? connected.inertia : 0f;

        if (a <= 0f) return b;
        if (b <= 0f) return a;
        return 1f / (1f / a + 1f / b);
    }

    // The ankle is not a positional joint but a pendulum drive: it gets
    // only torque against CoM offset. A spring and viscosity to a target
    // angle had to go: the foot is pressed to the ground, so muscle torque
    // turns the whole body, not the joint, and "damping" the angle
    // rocked the human down in 3 seconds. Viscosity comes from JointFriction.
    // A command that drives the joint into the last ankleLimitMargin degrees
    // of the stop is zeroed: else the foot jams at ±45° and there is no "back" torque.
    // Quiet path: 8° (zeros at +37°), not 15° (zeros at +30°, pf23 cutoff)
    // and not 2° (too close to the stop). Walk/catch keep 15°.
    private void ControlAnkleForBalance(HingeJoint2D joint, float balanceCommand, Muscle flexor, Muscle extensor)
    {
        if (joint == null || flexor == null || extensor == null) return;
        float signal = ProtectJointLimit(joint, Mathf.Clamp(balanceCommand, -1f, 1f));
        ApplySignal(signal, flexor, extensor);
    }

    private float ProtectJointLimit(HingeJoint2D joint, float signal)
    {
        if (!joint.useLimits) return signal;

        float angle = joint.jointAngle;
        float min = joint.limits.min;
        float max = joint.limits.max;
        // Walk/catch keep ankleLimitMargin 15°. Quiet only: 8° so a toed
        // forward push still has extensor at +30° (pf23 died there) without
        // driving into the last 2° of the stop. hipBalanceGain is unchanged.
        float margin = QuietVestibularPath() ? 8f : ankleLimitMargin;
        // A positive signal increases the angle, a negative one decreases it.
        if (signal > 0f && angle >= max - margin) return 0f;
        if (signal < 0f && angle <= min + margin) return 0f;
        return signal;
    }

    private void UpdateMuscle(Muscle muscle, float targetActivation)
    {
        if (muscle == null) return;
        // Instant zeroing was bang-bang: extensor→0 in one step, flexor
        // comes on at full torque — a limit cycle on light joints.
        muscle.activation = Mathf.MoveTowards(
            muscle.activation, targetActivation,
            muscleActivationSpeed * Time.fixedDeltaTime);
    }

    // 0 = Catch-hard restoring (tipped or spinning), 1 = hips hold / next Fall allowed.
    // Catch restoring peaks ~72-80 deg/s (walk2_catch). A full -catchComLead
    // slams the landing ankle into +45, then ProtectJointLimit zeroes the
    // last ankleLimitMargin and the brake dies. Fade CoP as tilt/omega drop so
    // the ankle leaves the stop and the hips take the trunk.
    //
    // walk2_blend (omegaRef 80, linear): Recover→Plant at tilt 7° / omega 55°/s
    // still had blend 0.31; 0.05 s later Fall at omega +49 (blend 0.39 added
    // forward CoP) and Catch cycle 1 went to 101°. Residual spin must keep
    // the brake: full CoP by ~0.7 of abort and by 50 deg/s, square so the
    // mid-range does not dump the ankle.
    //
    // Both inputs are signed along the walk direction: plus = the trunk is
    // going the way we walk. On |tilt| the brake was positive feedback past
    // upright — walk2_blend2 Catch whipped −15° / −121 deg/s to +24° /
    // +464 deg/s and still held the full backward CoP at the far end.
    private static float FallCatchRecoverBlend(
        float forwardTiltDeg, float forwardOmegaDegPerSec, float abortDeg)
    {
        float abort = Mathf.Max(8f, abortDeg);
        float tiltN = Mathf.Clamp01(Mathf.Max(0f, forwardTiltDeg) / (abort * 0.7f));
        const float omegaRef = 50f;
        float omegaN = Mathf.Clamp01(Mathf.Max(0f, forwardOmegaDegPerSec) / omegaRef);
        float raw = 1f - Mathf.Max(tiltN, omegaN);
        return raw * raw;
    }

    // blendBack is the same shape read on the backward side. The brake used
    // to take only the forward blend, so it was a rectifier: a trunk going
    // forward got the full backward CoP and a −12° lean, a trunk going
    // backward got nothing (blend = 1 reads as calm). Over the oscillation
    // the forward half of every cycle was braked and the backward half was
    // free, so the body ratcheted backward at a steady 9 mm/s
    // (walk_fix3: comSpeedX −0.0091, comTravelX −0.3633, CoM 0.91 m behind
    // support at the end). Signed hard mirrors the brake instead: forward CoP
    // and a forward lean pull a backward-tipping trunk back to upright, the
    // same way the original arm pulls a forward-tipping one. Do NOT feed the
    // two-sided calm into hard — that would apply the BACKWARD brake to a
    // trunk already going backward (the walk2_blend2 whip, mirrored).
    private static void ApplyRecoverBrake(
        ref FallCatchBalance fc,
        StepPhaseDriver driver,
        float lean,
        float fallComLead,
        float blend,
        float blendBack)
    {
        // + = trunk tipping the way we walk (brake backward, as before),
        // − = trunk tipping backward (mirror). Identical to 1 − blend while
        // the trunk never passes upright the other way.
        float hard = blendBack - blend;
        // Trunk calm (blend ≈ 1 at +1.2°) used to mirror hard negative:
        // forward CoP + forward lean on a plant CoM had already passed.
        // Arrest the foot the brake actually acts on while CoM is ahead of
        // it. Plant: new stance. Catch/Recover with catchAnkleOnSwing: the
        // landing sole. Never a trailing swing (that lead is always plus
        // and would freeze a healthy negative-lead Plant).
        //
        // That arrest is a POSITION error and it must not enter the trunk
        // channel. As a floor on `hard` it did: past plantComLeadMax it
        // saturated hard = 1, and hard drives the leans, so the brake
        // commanded −fallPelvisLean on BOTH pelvis and torso (12° + 12°
        // backward) plus the full −catchComLead CoP, held open-loop until
        // the mass crossed the sole. Proportional-only on an inverted
        // pendulum with no rate term: the trunk overshot to +22° backward.
        // Max() also deleted the mirror (hard < 0), the one term that damps
        // that backward swing, so the whip ran unopposed into the ankle stop
        // where ProtectJointLimit zeroes the command — the brake dies there
        // and the body finishes the episode falling forward.
        //
        // Keep the arrest, but as CoP only: summed (not Max) so the mirror
        // survives, dead-banded at the same lead the driver calls caught (the
        // sole runs to +0.195 m at the toe, so a CoM a few cm ahead of the
        // ankle is still supported — there is nothing to arrest), scaled by
        // the room left to plantComLeadMax, and capped below the stop.
        float arrestLead = driver.StanceLead;
        if (fc.catchAnkleOnSwing && driver.SwingFootIsGrounded)
            arrestLead = driver.SwingLead;
        float arrestCop = 0f;
        float caught = Mathf.Max(0f, driver.plantCaughtLeadMax);
        float arrestScale = Mathf.Clamp01(driver.plantArrestCopScale);
        if (arrestLead > caught && arrestScale > 0.0001f)
        {
            float bound = Mathf.Max(0.02f, driver.plantComLeadMax);
            // How far the trunk has already come back, read off the same
            // shape the brake itself uses: 0 upright or tipping our way,
            // 1 at 0.7·abort of backward tilt or 50 deg/s of backward rate.
            // The arrest releases there — a trunk already rotating backward
            // does not need more backward CoP, it needs the mirror.
            float back = 1f - Mathf.Sqrt(Mathf.Clamp01(blendBack));
            arrestCop = Mathf.Clamp01((arrestLead - caught) / bound)
                * (1f - back) * arrestScale;
        }
        fc.ankleScale = 1f;
        // Hips take the trunk only after the CoP brake has mostly come off.
        // walk2_blend Plant blend 0.31 still handed 31% of the hip loop while
        // the landing ankle was the restoring moment; delay that takeover.
        // Two-sided: on a backward tip blend is 1, and handing the full
        // "lean toward CoM" loop over there commands the pelvis further
        // back — straight against the mirrored brake.
        float calm = Mathf.Min(blend, blendBack);
        fc.hipBalanceScale = Mathf.Max(0f, (calm - 0.2f) / 0.8f);
        // Leans stay on the trunk term alone: a lean moves the CoM itself, so
        // asking for one on a CoM-position error is the whip above. A calm
        // trunk with the mass still ahead of the sole gets CoP and nothing
        // else, which is what "fall forward, the ankle catches it" means.
        fc.pelvisLean = -lean * hard;
        fc.torsoLean = -lean * hard;
        fc.comLead = -Mathf.Max(driver.catchComLead, fallComLead)
            * Mathf.Clamp(hard + arrestCop, -1f, 1f);
        fc.ignoreFalling = true;
        fc.suppressWalkLean = true;
    }

    // Reach continues the Fall vault clock: leaving Fall resets PhaseAge,
    // so a foot that left at 0.08 s of a 0.15 s ramp must not jump to 1.
    // Catch→Reach resets PhaseAge again. LastFallDuration alone then
    // restarts a short Fall at ~0.5 and bends the already-vaulted stance
    // toward landDeg. Resume keeps LastReach. LastCatch on the 0.15 s
    // ramp saturates after catchMax — air Catch is not vault progress
    // (catchAirVaultRemain already held the Fall+Reach remainder).
    // fallSwingCatchReachVaultAge continues that remainder on the new
    // Reach PhaseAge. scrapeVaultHold freezes that extraAge on a toe
    // scrape (leftover→0 on dirt). scrapeVaultAirAge then continues
    // extraAge from lift-off (not PhaseAge). Fall→Reach must not add
    // lastReachDuration (that is the previous cycle).
    private static float FallCatchVaultClock(
        StepPhaseDriver driver, StepPhaseDriver.FallCatchPhase phase)
    {
        float ramp = Mathf.Max(0.02f, driver.fallKneeRampSeconds);
        if (phase == StepPhaseDriver.FallCatchPhase.Fall)
            return Mathf.Clamp01(driver.PhaseAge() / ramp);
        if (phase == StepPhaseDriver.FallCatchPhase.Reach)
        {
            float age = driver.PhaseAge();
            if (driver.ReachResumedFromCatch
                && driver.fallSwingCatchReachVaultClock > 0.5f)
            {
                if (driver.fallSwingCatchReachVaultAge > 0.5f)
                {
                    float extra = age;
                    if (driver.ReachResumedFromScrape
                        && driver.fallSwingCatchReachScrapeVaultHold > 0.5f)
                    {
                        extra = 0f;
                        // scrapeVaultHold latched extraAge=0 for the
                        // whole resumed Reach, so a later lift-off
                        // never finished leftover→0. Resume from that
                        // lift-off (not PhaseAge: scrape time would
                        // saturate the 0.15 s ramp). Arm extraAge
                        // resume is scrapeArmAirAge. Flag 0 is the
                        // former extraAge=0 even in air.
                        if (driver.fallSwingCatchReachScrapeVaultAirAge > 0.5f
                            && !driver.SwingFootIsGrounded)
                            extra = driver.ScrapeResumeAirAge();
                    }
                    return FallCatchFallReachClock(driver, extra);
                }
                age += driver.LastReachDuration + driver.LastCatchDuration;
            }
            return Mathf.Clamp01(
                (driver.LastFallDuration + age) / ramp);
        }
        return 1f;
    }

    // Instant vault (flag 0) is Lerp(knee, 0, 1) on the first Fall frame —
    // leftover→0 (or landDeg→0) on the plant heel after plantStanceKnee.
    // Ramp on the swing-knee clock and keep leftover frozen at Plant exit
    // as the Lerp start (happy-path leftover 1 is landDeg). Cycle-0 /
    // StandingRestore stay on the quiet knee: that foot was already the strut.
    // fallSwingFallVaultLead then doubles that ramp clock so leftover→0
    // wins the height fight before 140° fold; Reach must use the same
    // multiply or Fall→Reach snaps leftover toward plantKnee.
    private static void ApplyFallCatchVault(
        ref FallCatchBalance fc, StepPhaseDriver driver, float vaultClock)
    {
        float vault = Mathf.Clamp01(driver.fallStanceVault);
        if (driver.fallSwingVaultRamp > 0.5f)
        {
            float clock = Mathf.Clamp01(vaultClock);
            if (driver.fallSwingFallVaultLead > 0.5f)
                clock = Mathf.Clamp01(clock * 2f);
            vault *= clock;
            if (driver.fallSwingPlantStanceKnee > 0.5f
                && driver.FallCatchCycleCount > 0
                && !driver.StandingRestore)
            {
                fc.stancePlantKnee = Mathf.Max(0f, driver.reachKneeLandDeg);
                if (driver.fallSwingCatchExtendClock > 0.5f
                    && driver.fallSwingPlantStanceExtendClock > 0.5f
                    && driver.fallSwingVaultFromLeftover > 0.5f)
                    fc.stancePlantKnee = Mathf.Max(
                        0f, driver.LastPlantStanceShinDeg);
            }
        }
        fc.stanceVault = vault;
    }

    // Vault lead already finishes leftover→0 at 2× fold clock. hipLift /
    // ankleLift still shared that 0.15 s ramp, so the unweighted foot
    // stayed on dirt until late (vault tall, swing still short). Double
    // those two (still a ramp, not instant 0.5; fold stays 1×). Reach /
    // Catch air continue the multiply or Fall→Reach snaps hipLift down.
    // Catch scrape may double the same way
    // (fallSwingCatchScrapeLiftLead). Quiet when 0:
    // former same-clock lift. CLI -fallSwingFallLiftLead.
    private static float FallCatchLiftLeadClock(
        StepPhaseDriver driver, float clock)
    {
        clock = Mathf.Clamp01(clock);
        if (driver.fallSwingFallLiftLead > 0.5f)
            clock = Mathf.Clamp01(clock * 2f);
        return clock;
    }

    // Fall / Reach / Catch air already finish hipLift/ankleLift at 2×
    // (fallSwingFallLiftLead). Catch scrape still shared the 1× fold
    // ramp, so vault=1 met a half lift on the jammed toe. Double those
    // two on scrape (still a ramp, not instant 0.5; not 3×; not sci_a).
    // Reach resumed from scrape continues the multiply or Catch→Reach
    // snaps hipLift down. Quiet when 0: former same-clock scrape lift.
    // CLI -fallSwingCatchScrapeLiftLead.
    private static float FallCatchScrapeLiftLeadClock(
        StepPhaseDriver driver, float clock)
    {
        clock = Mathf.Clamp01(clock);
        if (driver.fallSwingCatchScrapeLiftLead > 0.5f)
            clock = Mathf.Clamp01(clock * 2f);
        return clock;
    }

    // Vault and lift already finish at 2×. Fold still shared the 0.15 s
    // ramp, so lift=1 met a 70° shin and the foot stayed on dirt. Double
    // that fold (still a ramp, not instant 140°; not sci_a; not 3×
    // vault/lift). Reach / Catch continue the multiply on the frozen
    // Fall+Reach age or Fall→Reach snaps 140°→70°. Quiet when 0:
    // former same-clock fold. CLI -fallSwingFallFoldLead.
    private static float FallCatchFoldLeadClock(
        StepPhaseDriver driver, float clock)
    {
        clock = Mathf.Clamp01(clock);
        if (driver.fallSwingFallFoldLead > 0.5f)
            clock = Mathf.Clamp01(clock * 2f);
        return clock;
    }

    // Recover continues the Catch yield clock: leaving Catch resets PhaseAge,
    // so a plant that yielded 0.10 s of a 0.22 s ramp must not jump to 0.
    // Airborne Catch is not that plant: PhaseAge still ticks, so yield 1→0
    // while the trailing knee is the only strut, then Catch→Reach restores
    // vault clock to 1 and snaps ~16° back to 0°. Plant age is 0 until the
    // heel loads; Recover continues LastCatchPlantDuration. Flag 0 is the
    // former Catch PhaseAge (yield in air).
    // fallSwingCatchAirVaultRemain: that hold at clock 0 forced
    // stanceVault=1. Early Reach gate then trunkGone Catch jumped
    // leftover→0 on the only strut. Yield starts at 1 − Fall+Reach
    // clock (no LastCatch: air is not vault progress). Catch→Reach
    // continues that remainder on the new Reach PhaseAge
    // (fallSwingCatchReachVaultAge). Planted Catch / Recover continue
    // from that start. Flag 0 is the former hold-at-1.
    // scrapeYieldHold reuses this Catch clock on a Catch→Reach toe
    // scrape so ApplyFallCatchVault cannot pose leftover on the trailing
    // strut. scrapeVaultAirAge passes lift-off extraAge so remain can
    // finish leftover→0 while this yield path stays on (dirt, or
    // yieldAirAge 0). scrapeYieldAirAge then takes ApplyFallCatchVault
    // once the toe leaves. Flag yieldHold 0 is the former leftover
    // plantKnee on dirt.
    private static float CatchVaultYieldClock(
        StepPhaseDriver driver, StepPhaseDriver.FallCatchPhase phase)
    {
        return CatchVaultYieldClock(driver, phase, 0f);
    }

    private static float CatchVaultYieldClock(
        StepPhaseDriver driver, StepPhaseDriver.FallCatchPhase phase, float extraAge)
    {
        float ramp = Mathf.Max(0.05f, driver.catchMinDuration);
        bool plantClock = driver.fallSwingCatchAirVaultHold > 0.5f;
        float remain = 0f;
        if (plantClock && driver.fallSwingCatchAirVaultRemain > 0.5f)
            remain = 1f - FallCatchFallReachClock(driver, extraAge);
        if (phase == StepPhaseDriver.FallCatchPhase.Catch)
        {
            if (plantClock && !driver.SwingFootIsPlanted)
                return remain;
            float age = plantClock ? driver.CatchPlantAge() : driver.PhaseAge();
            return Mathf.Max(remain, Mathf.Clamp01(age / ramp));
        }
        if (phase == StepPhaseDriver.FallCatchPhase.Recover)
        {
            float held = plantClock
                ? driver.LastCatchPlantDuration
                : driver.LastCatchDuration;
            return Mathf.Max(
                remain,
                Mathf.Clamp01((held + driver.PhaseAge()) / ramp));
        }
        return 1f;
    }

    // Instant Catch yield (flag 0) drops stanceVault on the first Catch
    // frame — trailing knee 0°→~16° (kneeTarget + bend) and the pelvis
    // sits on the new plant before the catch ankle bites. Ramp 1→0 on
    // the catchMin clock. Do not set stancePlantKnee here: that pose is
    // the NEW stance after swap, not the trailing strut.
    // Reach already ramps fold / extend. Catch used to jump to landDeg
    // because leaving Reach resets PhaseAge and ReachExtendProgress
    // returns 0 on a heel plant. Freeze the Fall+Reach fold clock at
    // Catch enter; keep the air extend clock while the heel is up;
    // leftover extend uses catchMin once planted. Recover continues
    // that planted remainder (same PhaseAge reset as the vault yield).
    // fallSwingCatchAirExtendHold: air Catch is not that plant. PhaseAge
    // still ticks, so leftover jumped to 1 on the first heel-loaded
    // frame (fold→landDeg), then Recover used LastCatchDuration (air
    // included) and stayed at landDeg. Plant age is 0 until the heel
    // loads; Recover continues LastCatchPlantDuration. Plant after the
    // swap continues that remainder (fallSwingPlantStanceExtendClock)
    // so Recover's 0.05 s exit does not jump leftover to landDeg.
    // Fall/Reach vault starts from that leftover frozen at Plant exit
    // (fallSwingVaultFromLeftover), not from a fresh landDeg.
    // Air Catch still Max(Last, ReachExtendProgress). A toe scrape is
    // not that air (fallSwingCatchScrapeExtendHold). scrapeExtendAirAge
    // then resumes Last from lift-off (live Progress counted dirt as air).
    // fallSwingCatchAirExtendHold flag 0 is the former Catch PhaseAge leftover.
    private static float CatchReachFoldDeg(StepPhaseDriver driver)
    {
        float flex = Mathf.Max(0f, driver.reachKneeFlex);
        if (driver.fallSwingReachLiftClock <= 0.5f)
            return flex;
        return flex * FallCatchFoldLeadClock(
            driver, FallCatchFallReachClock(driver));
    }

    private static float CatchReachExtendProgress(
        StepPhaseDriver driver, StepPhaseDriver.FallCatchPhase phase)
    {
        float start = Mathf.Clamp01(driver.LastReachExtendProgress);
        float ramp = Mathf.Max(0.05f, driver.catchMinDuration);
        bool plantClock = driver.fallSwingCatchAirExtendHold > 0.5f;
        if (phase == StepPhaseDriver.FallCatchPhase.Catch)
        {
            if (!driver.SwingFootIsPlanted)
            {
                // scrapeExtendHold (Catch→Reach) freezes Last after the
                // bounce. Catch itself still Max'd Last with live Progress:
                // scrapeExtend ticks on a heel-up toe, so CatchReachShinDeg
                // finished 70°→35° on dirt, then the bounce snapped Last.
                // Freeze at Last on a grounded scrape; airborne Catch
                // still ramps. Flag 0 is the former live Progress.
                // scrapeExtendAirAge (Catch): that freeze then Max'd Last
                // with live Progress after the toe left (scrapeExtend
                // counted dirt as air), so the first air frame jumped
                // 70°→35°. Resume from lift-off only. Flag 0 is the
                // former live Progress even in air.
                // scrapeExtendEnterHold then keeps that Last across
                // Catch→Reach enter (no Max with live Progress).
                // scrapeExtendAirEnterHold: that keep is grounded-only.
                // After Catch scrape then lift-off, AirAge already resumed
                // Last; the airborne bounce still Max'd live Progress
                // (dirt counted as air). Keep Last and latch
                // ReachResumedFromScrape so Tick does not then write that
                // jump. Flag 0 is the former Max even after Catch scrape
                // then air.
                if (driver.fallSwingCatchScrapeExtendHold > 0.5f
                    && driver.SwingFootIsGrounded)
                    return start;
                if (driver.fallSwingCatchScrapeExtendHold > 0.5f
                    && driver.fallSwingCatchScrapeExtendAirAge > 0.5f
                    && driver.ScrapeResumeAirActive
                    && !driver.SwingFootIsGrounded)
                    return driver.ScrapeResumeExtendProgress();
                return Mathf.Max(start, driver.ReachExtendProgress());
            }
            float age = plantClock ? driver.CatchPlantAge() : driver.PhaseAge();
            // scrapeLiftAirCatchAirEnterCatchArm already skips Last-carry
            // on planted Catch after never-scrape Catch→Reach.
            // CatchReachShinDeg still Max(Last, CatchPlantAge):
            // catchAirGroundFold needs !planted, and planted-at-enter has
            // catchPlantEnterTime == phaseEnterTime so CatchPlantAge
            // tracks PhaseAge. Last is the Reach freeze (fold was kept
            // by enterGroundFold, but Last still holds Reach extend), so
            // the first Catch frame is 140°→~56° against leftover shin —
            // the shin pair of catchAirEnterCatchArm. Skip Last and
            // arrive on plant-clock. Flag 0 is the former
            // Max(Last, CatchPlantAge). Recover/Plant still Max Last —
            // catchAirEnterEndFold.
            bool catchAirEnterCatchFold =
                driver.fallSwingCatchScrapeLiftAirCatchAirEnterCatchFold > 0.5f
                && driver.fallSwingCatchScrapeLiftAirCatchAirEnterCatchArm > 0.5f
                && driver.fallSwingCatchScrapeLiftAirCatchAirEnterEndArm > 0.5f
                && driver.fallSwingCatchScrapeLiftAirCatchAirEnterGroundFold > 0.5f
                && driver.fallSwingCatchScrapeLiftAirCatchAirEnterGroundArm > 0.5f
                && driver.fallSwingCatchScrapeLiftAirCatchAirEnterArm > 0.5f
                && driver.fallSwingCatchScrapeLiftAirCatchAirEnterFold > 0.5f
                && driver.fallSwingCatchScrapeLiftAirCatchAirFold > 0.5f
                && driver.fallSwingCatchScrapeLiftAirCatchAirArm > 0.5f
                && driver.fallSwingCatchScrapeLiftAirCatchArm > 0.5f
                && driver.CatchResumedFromReachAir
                && !driver.CatchScrapedThisPhase
                && !driver.CatchResumedFromReachScrapeAir;
            if (catchAirEnterCatchFold)
                return Mathf.Clamp01(age / ramp);
            // catchGroundFold already keeps Reach fold through grounded
            // scrape Catch until heel plant. CatchReachShinDeg still
            // Max(Last, CatchPlantAge) once planted: catchGroundFold
            // needs !planted, and CatchPlantAge is 0 at heel-down (or
            // tracks PhaseAge if planted at enter). Last is the
            // Catch-enter freeze (or AirAge resume), not the
            // catchGroundFold override, so the first planted Catch
            // frame is 140°→~56° against leftover shin — the planted
            // shin pair of catchGroundArm. Skip Last and arrive on
            // plant-clock. Flag 0 is the former Max(Last, CatchPlantAge).
            // Recover/Plant skip Last after never-scrape air is
            // catchAirEndFold (RecoveredFromCatchAir). Recover/Plant skip
            // Last after never-air scrape is catchGroundCatchEndFold
            // (RecoveredFromCatchGroundCatch).
            bool catchGroundCatchFold =
                driver.fallSwingCatchScrapeLiftAirCatchGroundCatchFold > 0.5f
                && driver.fallSwingCatchScrapeLiftAirCatchEndFold > 0.5f
                && driver.fallSwingCatchScrapeLiftAirCatchGroundFold > 0.5f
                && driver.fallSwingCatchScrapeLiftAirCatchGroundArm > 0.5f
                && driver.fallSwingCatchScrapeLiftAirCatchFold > 0.5f
                && driver.fallSwingCatchAirLiftHold > 0.5f
                && driver.fallSwingCatchScrapeLiftAirHold > 0.5f
                && (driver.CatchScrapedThisPhase
                    || (driver.CatchResumedFromReachScrapeAir
                        && driver.fallSwingCatchScrapeLiftAirCatchHold > 0.5f));
            if (catchGroundCatchFold)
                return Mathf.Clamp01(age / ramp);
            // catchAirGroundFold already keeps Reach fold through grounded
            // never-scrape Catch until heel plant. CatchReachShinDeg still
            // Max(Last, CatchPlantAge) once planted: catchAirGroundFold
            // needs !planted, and CatchPlantAge is 0 at heel-down.
            // Last is the Reach freeze (fold was kept by catchAirFold /
            // catchAirGroundFold), so the first planted Catch frame is
            // 140°→~56° against leftover shin — the planted shin pair of
            // catchAirGroundArm. Skip Last and arrive on plant-clock.
            // CatchPlantAge < PhaseAge so heel-only / planted-at-enter
            // stay on Max (those are catchAirEnterCatchFold). Flag 0 is
            // the former Max(Last, CatchPlantAge). Recover/Plant skip
            // Last after this Catch is catchAirEndFold.
            bool catchAirGroundCatchFold =
                driver.fallSwingCatchScrapeLiftAirCatchAirGroundCatchFold > 0.5f
                && driver.fallSwingCatchScrapeLiftAirCatchAirEndFold > 0.5f
                && driver.fallSwingCatchScrapeLiftAirCatchAirGroundFold > 0.5f
                && driver.fallSwingCatchScrapeLiftAirCatchAirGroundArm > 0.5f
                && driver.fallSwingCatchScrapeLiftAirCatchAirEndArm > 0.5f
                && driver.fallSwingCatchScrapeLiftAirCatchAirFold > 0.5f
                && driver.fallSwingCatchScrapeLiftAirCatchAirArm > 0.5f
                && driver.fallSwingCatchScrapeLiftAirCatchArm > 0.5f
                && !driver.CatchScrapedThisPhase
                && !driver.CatchResumedFromReachScrapeAir
                && driver.CatchPlantAge() + 0.0001f < driver.PhaseAge();
            if (catchAirGroundCatchFold)
                return Mathf.Clamp01(age / ramp);
            return Mathf.Max(start, Mathf.Clamp01(age / ramp));
        }
        // catchAirEnterCatchFold already skipped Last on planted Catch.
        // Recover/Plant still Max(Last, held+PhaseAge): Last is the Reach
        // freeze, so Catch plant-clock fold then Recover Max Last yanks
        // 140°→~56° against leftover shin — the Recover/Plant shin pair
        // of catchAirEnterEndArm. Skip Last and continue plant-clock.
        // RecoveredFromCatchAirEnter survives Recover→Plant. Flag 0 is
        // the former Max Last after that Catch.
        // scrapeLiftAirCatchEndArm already skips Last-carry on Recover/Plant
        // after scrape then air. CatchReachExtendProgress still Max(Last,
        // held+PhaseAge): Last is the Catch-enter freeze (or AirAge
        // resume), so Catch plant-clock fold then Recover Max Last yanks
        // 140°→~56° against leftover shin — the Recover/Plant shin pair of
        // catchEndArm. Skip Last and continue plant-clock.
        // RecoveredFromCatchScrapeAir survives Recover→Plant. Flag 0 is
        // the former Max Last after scrape then air.
        // scrapeLiftAirCatchAirEndArm already skips Last-carry on Recover/Plant
        // after never-scrape air Catch. CatchReachExtendProgress still Max(Last,
        // held+PhaseAge): Last is the Reach freeze at Catch enter, so Catch
        // plant-clock fold then Recover Max Last yanks 140°→~56° against
        // leftover shin — the Recover/Plant shin pair of catchAirEndArm.
        // Skip Last and continue plant-clock.
        // RecoveredFromCatchAir survives Recover→Plant. Flag 0 is the
        // former Max Last after never-scrape air Catch.
        // scrapeLiftAirCatchGroundCatchFold already skipped Last on planted
        // scrape Catch. Recover/Plant still Max(Last, held+PhaseAge):
        // CatchScrapedThisPhase clears on Catch exit, RecoveredFromCatchScrapeAir
        // needs lift-off, so never-air Recover Max Last yanks 140°→~56°
        // against leftover shin — the Recover/Plant shin pair of
        // catchGroundArm. Skip Last and continue plant-clock.
        // RecoveredFromCatchGroundCatch survives Recover→Plant. Flag 0 is
        // the former Max Last after never-air scrape.
        // ComputePlantExitStanceShinDeg still Max'd Last at Plant→Fall
        // after those Recover skips — catchPlantExitFold.
        bool catchAirEnterEndFold =
            driver.fallSwingCatchScrapeLiftAirCatchAirEnterEndFold > 0.5f
            && driver.fallSwingCatchScrapeLiftAirCatchAirEnterCatchFold > 0.5f
            && driver.fallSwingCatchScrapeLiftAirCatchAirEnterCatchArm > 0.5f
            && driver.fallSwingCatchScrapeLiftAirCatchAirEnterEndArm > 0.5f
            && driver.fallSwingCatchScrapeLiftAirCatchAirEnterGroundFold > 0.5f
            && driver.fallSwingCatchScrapeLiftAirCatchAirEnterGroundArm > 0.5f
            && driver.fallSwingCatchScrapeLiftAirCatchAirEnterArm > 0.5f
            && driver.fallSwingCatchScrapeLiftAirCatchAirEnterFold > 0.5f
            && driver.fallSwingCatchScrapeLiftAirCatchAirFold > 0.5f
            && driver.fallSwingCatchScrapeLiftAirCatchAirArm > 0.5f
            && driver.fallSwingCatchScrapeLiftAirCatchArm > 0.5f
            && driver.RecoveredFromCatchAirEnter;
        bool catchEndFold =
            driver.fallSwingCatchScrapeLiftAirCatchEndFold > 0.5f
            && driver.fallSwingCatchScrapeLiftAirCatchEndArm > 0.5f
            && driver.fallSwingCatchScrapeLiftAirCatchGroundFold > 0.5f
            && driver.fallSwingCatchScrapeLiftAirCatchGroundArm > 0.5f
            && driver.fallSwingCatchScrapeLiftAirCatchFold > 0.5f
            && driver.fallSwingCatchAirLiftHold > 0.5f
            && driver.fallSwingCatchScrapeLiftAirHold > 0.5f
            && driver.RecoveredFromCatchScrapeAir;
        bool catchAirEndFold =
            driver.fallSwingCatchScrapeLiftAirCatchAirEndFold > 0.5f
            && driver.fallSwingCatchScrapeLiftAirCatchAirEndArm > 0.5f
            && driver.fallSwingCatchScrapeLiftAirCatchAirGroundFold > 0.5f
            && driver.fallSwingCatchScrapeLiftAirCatchAirGroundArm > 0.5f
            && driver.fallSwingCatchScrapeLiftAirCatchAirFold > 0.5f
            && driver.fallSwingCatchScrapeLiftAirCatchAirArm > 0.5f
            && driver.fallSwingCatchScrapeLiftAirCatchArm > 0.5f
            && driver.RecoveredFromCatchAir;
        bool catchGroundCatchEndFold =
            driver.fallSwingCatchScrapeLiftAirCatchGroundCatchEndFold > 0.5f
            && driver.fallSwingCatchScrapeLiftAirCatchGroundCatchFold > 0.5f
            && driver.fallSwingCatchScrapeLiftAirCatchEndFold > 0.5f
            && driver.fallSwingCatchScrapeLiftAirCatchEndArm > 0.5f
            && driver.fallSwingCatchScrapeLiftAirCatchGroundFold > 0.5f
            && driver.fallSwingCatchScrapeLiftAirCatchGroundArm > 0.5f
            && driver.fallSwingCatchScrapeLiftAirCatchFold > 0.5f
            && driver.fallSwingCatchAirLiftHold > 0.5f
            && driver.fallSwingCatchScrapeLiftAirHold > 0.5f
            && driver.RecoveredFromCatchGroundCatch;
        if (phase == StepPhaseDriver.FallCatchPhase.Recover)
        {
            float held = plantClock
                ? driver.LastCatchPlantDuration
                : driver.LastCatchDuration;
            float clock = Mathf.Clamp01((held + driver.PhaseAge()) / ramp);
            if (catchAirEnterEndFold || catchEndFold || catchAirEndFold
                || catchGroundCatchEndFold)
                return clock;
            return Mathf.Max(start, clock);
        }
        if (phase == StepPhaseDriver.FallCatchPhase.Plant)
        {
            float held = plantClock
                ? driver.LastCatchPlantDuration + driver.LastRecoverDuration
                : driver.LastCatchDuration + driver.LastRecoverDuration;
            float clock = Mathf.Clamp01((held + driver.PhaseAge()) / ramp);
            if (catchAirEnterEndFold || catchEndFold || catchAirEndFold
                || catchGroundCatchEndFold)
                return clock;
            return Mathf.Max(start, clock);
        }
        return 1f;
    }

    private static float CatchReachShinDeg(
        StepPhaseDriver driver, StepPhaseDriver.FallCatchPhase phase)
    {
        return Mathf.Lerp(
            CatchReachFoldDeg(driver),
            Mathf.Max(0f, driver.reachKneeLandDeg),
            CatchReachExtendProgress(driver, phase));
    }

    private static void ApplyCatchVaultYield(
        ref FallCatchBalance fc, StepPhaseDriver driver, float yieldClock)
    {
        if (driver.fallSwingCatchVaultYield <= 0.5f
            || driver.fallStanceVault <= 0.001f)
            return;
        float vault = Mathf.Clamp01(driver.fallStanceVault)
            * (1f - Mathf.Clamp01(yieldClock));
        if (vault > 0.001f)
            fc.stanceVault = vault;
    }

    // WALK-2: phase-dependent ankle/pelvis/torso scaling. useFallCatchWalk=0 → identity.
    private static FallCatchBalance ComputeFallCatchBalance(
        StepPhaseDriver driver, int phaseCode, float fallComLead,
        float tiltDeg, float omegaDegPerSec, float leadDir, float plantRockLead,
        float plantLeanRoll, float plantLeanFirst, float firstStepGeoKnee,
        ref bool firstStepCatchPlantLatched)
    {
        var fc = new FallCatchBalance
        {
            ankleScale = 1f,
            hipBalanceScale = 1f
        };
        if (driver == null || driver.useFallCatchWalk == 0 || phaseCode < 0)
            return fc;
        // Cycle 0 no longer geos (comment 21); keep the arg so the
        // call site / latch field stay bitwise for later cycles.
        _ = firstStepGeoKnee;

        float lean = driver.fallPelvisLean;
        // Forward tilt is negative world tilt (hipBaseAngle convention:
        // minus = forward), so flip the sign into walk coordinates.
        float dir = Mathf.Abs(leadDir) > 0.01f ? Mathf.Sign(leadDir) : 1f;
        float forwardTilt = -tiltDeg * dir;
        float forwardOmega = -omegaDegPerSec * dir;
        float blend = FallCatchRecoverBlend(forwardTilt, forwardOmega, driver.fallAbortDeg);
        // blend is one-sided on purpose (the CoP brake must not push a trunk
        // that already went past upright the other way), so a trunk tipping
        // BACKWARD reads exactly like a calm one: blend = 1. That is right
        // for the SIGN of the brake (it must not keep pushing backward past
        // upright — see ApplyRecoverBrake) and wrong for how hard the brake
        // and the leans work: Reach then cuts the stance
        // ankle to 25% and applies the full forward pelvis/chest lean, whose
        // third-law reaction drives the pelvis further back. Positive feedback
        // (walk_fix2: comTravelX -1.34 m, tilt 90 deg backward in walk
        // coordinates, CoM 0.96 m behind support). Mirror the same shape onto
        // the backward side and use the smaller: 1 only when the trunk is
        // calm both ways. Identical to blend on a healthy forward fall.
        float blendBack = FallCatchRecoverBlend(
            -forwardTilt, -forwardOmega, driver.fallAbortDeg);
        float calm = Mathf.Min(blend, blendBack);

        switch ((StepPhaseDriver.FallCatchPhase)phaseCode)
        {
            case StepPhaseDriver.FallCatchPhase.Plant:
                // After Recover the body is already past this foot. Identity
                // ankle plus walk lean would shove CoP forward and reopen
                // the 108° vault. First plant is still a quiet start; after
                // a catch the Recover brake stays on the new stance (roles
                // already swapped — do not catchAnkleOnSwing, that would
                // be the trailing foot). Hold that brake until StepPhaseDriver
                // sees CoM within plantComLeadMax and TiltSettled: fading it
                // over plantMinDuration was the cadence pass that let Plant
                // stand without a restoring CoP and Fall from 21° to 102°.
                // StandingRestore is a failed Fall/Reach, or a Plant outside
                // the TiltSettled + signed-lead band: same stance, same
                // brake, even when FallCatchCycleCount is still 0.
                fc.ignoreFalling = true;
                fc.suppressWalkLean = true;
                if (driver.FallCatchCycleCount > 0 || driver.StandingRestore)
                    ApplyRecoverBrake(ref fc, driver, lean, fallComLead, blend, blendBack);
                // walkPlantRockLead above: command the roll-over this phase is
                // waiting for. Only on the band StepPhaseDriver would already
                // release a Fall on, and never while the recovery ladder holds:
                // a restore or a surrender Plant is a brake, and rolling the CoM
                // forward there is the fold that brake exists to prevent (a
                // surrender also drops splitLegs, so the lead would tip the
                // quiet two-foot stance instead of one support foot).
                // Cycle 0 is included — unlike the trailing push, which needs a
                // rear moment arm the feet-together stand does not have. Here the
                // lever is the leading ankle itself, and the first Fall of a cold
                // BeginWalk released on the same deadline as every later one.
                // Comment 43: cycle 0 is included. The comment above already
                // said so; `FallCatchCycleCount != 0` had shut the only
                // external CoP and left FallLeadReady waiting on −1.5 cm.
                if (plantRockLead > 0.0001f
                    && driver.PlantGateReady
                    && !driver.StandingRestore
                    && !driver.StandingSurrender
                    && driver.PlantRolledFromBehind)
                {
                    // Never out to the gate's own upper bound: CoM past
                    // plantComLeadMax shuts Plant→Fall again and arms the
                    // recovery ladder. Half the bound as the ceiling, then faded
                    // by the room actually left — so the setpoint is a roll-over,
                    // not a lean the phase cannot end from.
                    float leadBound = Mathf.Max(0.02f, driver.plantComLeadMax);
                    float rock = Mathf.Min(plantRockLead, leadBound * 0.5f);
                    // Full exactly when plantMinDuration expires: a step in the
                    // ankle setpoint is a step in the ankle torque, and that
                    // shape is the shove every other Fall target is ramped to
                    // avoid. The roll then arrives as the age gate opens.
                    float rockClock = Mathf.Clamp01(
                        driver.PhaseAge() / Mathf.Max(0.05f, driver.plantMinDuration));
                    float rockRoom = Mathf.Clamp01(1f - driver.StanceLead / leadBound);
                    // Additive on top of the brake, not a Max: where both are
                    // live the sum is the smaller net lead. Later cycles use
                    // two-sided calm so a trunk tipping either way collapses
                    // this term. Cycle 0 must not: RecoverBlend at 9° is
                    // (1 − 9/(0.7·30))² ≈ 0.33, and that is the initiation
                    // lean, not a fall (comment 30). PlantGateReady already
                    // excludes restore / surrender.
                    float rockCalm = driver.FallCatchCycleCount == 0 ? 1f : calm;
                    fc.comLead += rock * rockClock * rockRoom * rockCalm;
                }
                // walkPlantLeanRoll above: the trunk half of the same
                // roll-over. Same gate as the setpoint — the band the driver
                // would already release a Fall on, and never while the recovery
                // ladder holds. Cycle 0 used to be excluded so a cold
                // BeginWalk kept its recorded feet-together trajectory; that
                // also left the first Fall with no trunk lean at all (rock
                // lead only). walkPlantLeanFirst lets the same 9° / clock /
                // fade start that first step; trailing A2 / hip stay on
                // cycle > 0 (no rear arm while the feet are together). With
                // the trigger off (fallComLeadTrigger 0) the gate never
                // waits for a roll-over at all, so there is nothing here to
                // start. Flag 0 is the former later-cycle only.
                if (plantLeanRoll > 0.0001f
                    && driver.fallComLeadTrigger > 0.0001f
                    && (driver.FallCatchCycleCount > 0
                        || plantLeanFirst > 0.5f)
                    && driver.PlantGateReady
                    && !driver.StandingRestore
                    && !driver.StandingSurrender
                    && driver.PlantRolledFromBehind)
                {
                    // Later cycles: StanceLeadCapture is the number
                    // FallLeadReady reads, and the lean fades out as that
                    // capture point reaches the trigger. Cycle 0
                    // FallLeadReady is StanceLead ≥ 0. Comment 31: keep
                    // the full 9° until EnterFall (not × RecoverBlend, not
                    // faded on Capture). Comment 32: that lean about a
                    // free hip is a bow; trail CoM is the CoP translator.
                    // Comment 33: pelvisLean on a free hip is a bow.
                    // Comment 34: hip-lock + pelvis track still left
                    // StanceLead at −1.5 cm (overconstrained stack, 0.9 cm
                    // of chest). Comment 35: trailing-hip extend as a
                    // DrivePelvis counter is cancelled by same-hip P.
                    // Comment 36: trail leaves DrivePelvis and SPD-extends.
                    // Comment 37/38: both hips took that plus pose; 38
                    // snapped dampers. Comment 39: same joint target is a
                    // sit. Comment 40: trail WORLD tilt + leading DrivePelvis
                    // still sat (DrivePelvis flexed the lead). Comment 41:
                    // both hips hold WORLD thigh tilt, DrivePelvis off —
                    // lumbar 9° is the comment-33/38 bow again. Comment 43:
                    // those hip poses sat (internal couple) and plantRockLead
                    // was gated off cycle 0. Comment 44: pelvisLean is the
                    // WORLD target DrivePelvis holds (inverted pendulum);
                    // lumbar-only left ~1.35 cm. Later cycles keep the
                    // Capture fade and two-sided calm.
                    float leanRoom = driver.FallCatchCycleCount == 0
                        ? 1f
                        : Mathf.Clamp01(
                            (driver.fallComLeadTrigger - driver.StanceLeadCapture)
                                / driver.fallComLeadTrigger);
                    float leanClock = Mathf.Clamp01(
                        driver.PhaseAge() / Mathf.Max(0.05f, driver.plantMinDuration));
                    float leanCalm = driver.FallCatchCycleCount == 0 ? 1f : calm;
                    float leanRoll = lean * Mathf.Max(0f, driver.fallForwardLeanScale)
                        * Mathf.Clamp01(plantLeanRoll) * leanClock * leanRoom * leanCalm;
                    // Additive on the brake, exactly as the setpoint above is,
                    // not a Max: where both are live the sum is the smaller net
                    // lean. On the band this gate defines the brake's own hard
                    // is already near zero, so the two rarely overlap.
                    // Comment 46 skipped this on cycle 0 (WORLD shear, lumbar
                    // third-law was a backward couple with DrivePelvis off).
                    // Cycle 0 is leading-foot stance again: walkPlantLeanFirst
                    // holds the inverted-pendulum target (minus = forward)
                    // while the trailing heel-press walks the 1.5 cm.
                    fc.torsoLean += leanRoll;
                    fc.pelvisLean += leanRoll;
                }
                // Role-swap Plant only: the new stance is the catch shin.
                // StandingRestore kept the old stance (already the strut).
                // Cycle-0 is the quiet start. Instant landDeg (flag 0) is
                // the former snap after Recover's 0.05 s exit, while leftover
                // still ramps on catchMin. Continue CatchReachShinDeg so a
                // short Recover does not yank fold→landDeg on the only
                // loaded heel; happy-path leftover 1 is still landDeg.
                if (driver.fallSwingPlantStanceKnee > 0.5f
                    && driver.FallCatchCycleCount > 0
                    && !driver.StandingRestore)
                {
                    fc.stancePlantKnee = Mathf.Max(0f, driver.reachKneeLandDeg);
                    if (driver.fallSwingCatchExtendClock > 0.5f
                        && driver.fallSwingPlantStanceExtendClock > 0.5f)
                        fc.stancePlantKnee = CatchReachShinDeg(
                            driver, StepPhaseDriver.FallCatchPhase.Plant);
                    // fallSwingPlantMidStanceExtend: the pose above is the
                    // landing flex, and it used to stand frozen for the whole
                    // phase — the new stance carried the body ~5.4 cm below a
                    // straight leg until the next Fall vaulted it. Extend it
                    // here on the roll-over Plant is already waiting for. The
                    // driver applies the identical schedule to the number it
                    // freezes at Plant→Fall (LastPlantStanceShinDeg, the vault
                    // start), so the strut only ever lengthens: Plant hands
                    // Fall an already-tall leg instead of a 0.075 s snap.
                    fc.stancePlantKnee = driver.ApplyPlantMidStanceExtend(
                        fc.stancePlantKnee, driver.PhaseAge());
                }
                // Cycle 0: keep ankleScale 1. Fading it with StanceLead
                // (comment 27) zeroed plantRockLead — the CoP that walks
                // CoM onto the already-leading foot — and left only the
                // 9° trunk lean. Third law then holds StanceLead at
                // ~−1.5 cm, FallLeadReady never fires (comment 28). Later
                // cycles keep ankleScale 1 here as well.
                break;
            case StepPhaseDriver.FallCatchPhase.Fall:
                // Ankle stays fully loaded and the trunk stays vertical. The
                // lean was the drift: tilting pelvis and torso forward pushes
                // the pelvis back through the joint pair (third law), the same
                // reaction the scissor showed (travel −1.110 m, run sci_a).
                // Forward motion comes from the CoP shift below instead.
                // Plant→Fall is the CoM / TiltSettled gate; this comLead
                // is the drive. Zero while still tipped: blend is 0 until
                // the trunk is back under the abort band, so a new Fall
                // does not add forward CoP on a body that has not stood.
                // Knee lift starts here: Fall→Reach is gated on air, so a
                // grounded swing must already be shortening or Reach never runs.
                // forceSwingFlex stays off — a Fall scissor is the same
                // third-law shove as the lean we removed. Grounded swing hip
                // is not a two-sided world-vertical SPD (that fought the
                // stance loop). A flexor-only stop past vertical keeps the
                // thigh from collapsing backward while the knee folds.
                // hipLift is a small flexor bias (not −swingHipFlex) so the
                // knee rises instead of the foot sliding back. 8° of
                // fade-out is ~0.4 cm of knee rise — not enough for
                // Fall→Reach air. fallSwingFallHipLiftCap uses 16° in
                // Fall only. Reach may use 16° (fallSwingReachHipLiftCap)
                // so that Fall thigh is not immediately past the cap;
                // Catch air may use 16° (fallSwingCatchAirHipLiftCap)
                // so that Reach thigh is not immediately past an 8°
                // Catch cap; Catch scrape may use 16°
                // (fallSwingCatchScrapeHipLiftCap). That 16° Catch air
                // still had liftSignal 0 at the cap; Catch air may
                // floor it (fallSwingCatchAirHipLiftCapHold); Catch
                // scrape may floor it (fallSwingCatchScrapeHipLiftCapHold).
                // That floor still wrote
                // hipLift 0.25 (−0.125 at the cap); Catch air may
                // write 0.5 (fallSwingCatchAirHipLift); Catch scrape
                // keeps 0.25. Catch air may then write ankleLift 0.5
                // (fallSwingCatchAirAnkleLift); Catch scrape may write
                // the same 0.5 (fallSwingCatchScrapeAnkleLift).
                // 0.25 bias never
                // walks into that room (liftSignal −0.20 at vertical);
                // fallSwingFallHipLift uses 0.5 in Fall only;
                // fallSwingReachHipLift uses 0.5 in Reach (Catch scrape
                // keeps 0.25) so the unweighted foot (FallAnkleLift)
                // can rise with the 140° fold. That 16°/0.5 Fall still
                // had liftSignal 0 at the cap (P to the target); Reach
                // already floors it (fallSwingReachHipLiftCapHold);
                // fallSwingFallHipLiftCapHold floors Fall the same way.
                // ankleLift fades the swing
                // toe grip on the same clock; fallSwingFallAnkleLift uses
                // 0.5 in Fall only; fallSwingReachAnkleLift uses 0.5 in
                // Reach (Catch scrape may write 0.5) so leftover plantarflex
                // after FallAnklePush does not slide the rise.
                // fallSwingCatchAirAnkleLift uses 0.5 in Catch air;
                // fallSwingCatchScrapeAnkleLift uses 0.5 in Catch scrape.
                // airAnkle holds the foot world-flat after lift-off: joint 0 at 140° knee
                // points the toe into the dirt. airHeel cocks the toe up in
                // the last centimetres so the heel plants first (the toe is
                // the long end). Stance vault ramps leftover→0 on the same
                // clock as the swing fold (fallSwingVaultRamp): a first-frame
                // Lerp to 0 popped the plant heel after plantStanceKnee.
                // fallSwingFallVaultLead doubles that vault clock so the
                // strut is tall before 140° fold (cycle-0 quiet 6°→0 is
                // ~2 mm on the same 0.15 s — the shorter leg until late).
                // Reach continues the same multiply in ApplyFallCatchVault.
                // hipLift / ankleLift still shared the 1× fold ramp after
                // that; fallSwingFallLiftLead doubles those two so the
                // foot rises before 140° fold. Fold still shared that
                // 1× ramp (70° at lift=1); fallSwingFallFoldLead doubles
                // the knee so the chain is short with that lift. Reach /
                // Catch continue that clock. Timeout Catch still faded at
                // 8°; fallSwingCatchAirHipLiftCap keeps 16° in air.
                // That 16° Catch air still had liftSignal 0 at the cap;
                // fallSwingCatchAirHipLiftCapHold floors Catch air at
                // 0.5. That floor still
                // writes hipLift 0.25 (liftSignal −0.125 at the cap
                // vs Fall/Reach −0.25); fallSwingCatchAirHipLift
                // writes 0.5 in Catch air (scrape keeps 0.25). That
                // 16°/0.5 Catch air still wrote ankleLift 0.25;
                // fallSwingCatchAirAnkleLift writes 0.5 in Catch air
                // (scrape may write 0.5). That 16° Catch air still met an
                // 8° scrape cap; fallSwingCatchScrapeHipLiftCap keeps
                // 16° in Catch scrape. That 16° scrape still had
                // liftSignal 0 at the cap; fallSwingCatchScrapeHipLiftCapHold
                // floors Catch scrape at 0.5. That floor still writes
                // hipLift 0.25 (liftSignal −0.125 at the cap vs
                // Fall/Reach/Catch-air −0.25); fallSwingCatchScrapeHipLift
                // writes 0.5 in Catch scrape. That 16°/0.5 hipLift still
                // wrote ankleLift 0.25; fallSwingCatchScrapeAnkleLift
                // writes 0.5 in Catch scrape. That 16° scrape still met
                // an 8° Catch→Reach cap; fallSwingCatchReachScrapeHipLiftCap
                // keeps 16° on that bounce. That 16° Catch→Reach scrape
                // still faded to liftSignal 0; fallSwingCatchReachScrapeHipLiftCapHold
                // floors it at 0.5. That floor still writes hipLift 0.25;
                // fallSwingCatchReachScrapeHipLift writes 0.5 on that
                // bounce. That 16°/0.5 hipLift still wrote ankleLift 0.25;
                // fallSwingCatchReachScrapeAnkleLift writes 0.5 on that
                // bounce. That 16° cap still had room=0 at a 16°
                // Catch-scrape thigh; fallSwingCatchReachScrapeHipLiftCapWide
                // keeps 24° on that bounce. That same room=0 still pinned
                // Catch scrape at 16°; fallSwingCatchScrapeHipLiftCapWide
                // keeps 24° in Catch scrape. That same room=0 still pinned
                // Fall at 16° after 23:47 kept ThighHold on Fall's first
                // air; fallSwingFallHipLiftCapWide keeps 24° in Fall.
                // That same room=0 still pinned Reach at 16° after Fall
                // walked into 24°; fallSwingReachHipLiftCapWide keeps 24°
                // in Reach. That same room=0 still pinned Catch-air at
                // 16° after Reach walked into 24°;
                // fallSwingCatchAirHipLiftCapWide keeps 24° in Catch-air.
                // That same room=0 still pinned Plant at 16°;
                // fallSwingPlantHipLiftCapWide keeps 24° in Plant.
                // That same room=0 still pinned Fall at 24° after 00:09
                // walked Plant leftover into 24°;
                // fallSwingFallHipLiftCapCeil keeps 32° in Fall.
                // That same room=0 still pinned Reach at 24° after Fall
                // walked into 32°; fallSwingReachHipLiftCapCeil keeps
                // 32° in Reach. That same room=0 still pinned Catch-air
                // at 24° after Reach walked into 32°;
                // fallSwingCatchAirHipLiftCapCeil keeps 32° in Catch-air.
                // That same room=0 still pinned Plant at 24° after leftover
                // + hipLift 0.5 sat there;
                // fallSwingPlantHipLiftCapCeil keeps 32° in Plant.
                // That same room=0 still pinned Catch scrape at 24°
                // after Catch-air walked into 32°;
                // fallSwingCatchScrapeHipLiftCapCeil keeps 32° in
                // Catch scrape. That same room=0 still pinned
                // Catch→Reach scrape at 24° after Catch scrape walked
                // into 32°; fallSwingCatchReachScrapeHipLiftCapCeil
                // keeps 32° on that bounce.
                // fallSwingVaultFromLeftover: Plant leftover may still be
                // short of landDeg; vault from that frozen shin, not landDeg.
                fc.ankleScale = 1f;
                // Two-sided (calm), not blend: blend reads a trunk tipping
                // BACKWARD as perfectly calm, so it cut the hip loop to 15%
                // exactly when that loop is the only thing left to arrest a
                // backward fall. Identical to blend on a forward fall.
                fc.hipBalanceScale = Mathf.Lerp(1f, 0.15f, calm);
                // Cycle 0: RecoverBlend is torso angle. At ~21° it is
                // already 0 while CoM is still inside the sole — a held
                // lunge, not a fall. *blend then zeros this drive and the
                // stance ankle holds (23.5°, walkSwap 0). blendBack is 0
                // only on a backward trunk (walk_fix2). Later cycles keep
                // blend so a new Fall on a body that has not stood does
                // not add forward CoP.
                fc.comLead = Mathf.Max(0f, fallComLead)
                    * (driver.FallCatchCycleCount == 0 ? blendBack : blend);
                // Ramp the grounded swing knee instead of jumping to full
                // flex: 10°→57° in 0.08 s buckled the leg and gave the trunk
                // −174 deg/s before Reach even started (walk2_blend2).
                // Same clock as the knee ramp: a step-1 yank is the scissor.
                float fallLiftClock = Mathf.Clamp01(
                    driver.PhaseAge() / Mathf.Max(0.02f, driver.fallKneeRampSeconds));
                // Forward lean during the topple. Fall used to write 0 here
                // while Reach wrote lean × 0.75, so the phase the whole gait
                // names its fall after had no lean at all and the only thing
                // driving the body over the support was fallComLead — 0.025 m
                // of CoP offset, against a 0.10 m backward CoP in every brake
                // phase. A trunk lean moves the CoM itself instead of asking
                // the ankle for torque it cannot make (the m·g·arm ceiling in
                // balance-actuators.mdc), and Fall is the cheapest place in
                // the cycle to pay for it: both feet are still down, so the
                // reaction on the pelvis is shared, and ankleScale is 1 here
                // against 0.25 in Reach.
                // calm, not blend — same reason Reach uses it: leaning the
                // chest forward drives the pelvis back, so on a trunk already
                // going backward this lean is positive feedback (walk_fix2).
                // fallLiftClock, not a constant, for the reason every other
                // Fall target is ramped: a step in a pelvis target is the
                // sci_a shove. The ramp also removes the 9° discontinuity the
                // Fall→Reach edge used to carry.
                float fallLean = lean * Mathf.Max(0f, driver.fallForwardLeanScale)
                    * calm * fallLiftClock;
                fc.pelvisLean = fallLean;
                fc.torsoLean = fallLean;
                // Vault and lift already 2× this clock; fold stayed 1× so
                // lift=1 met 70° of 140°. fallSwingFallFoldLead finishes
                // the short chain with that lift (still a ramp).
                fc.reachKnee = Mathf.Max(0f, driver.reachKneeFlex)
                    * FallCatchFoldLeadClock(driver, fallLiftClock);
                fc.thighHold = Mathf.Clamp01(driver.fallSwingThighHold);
                ApplyFallCatchVault(ref fc, driver, fallLiftClock);
                float hipLiftAmt = Mathf.Clamp01(driver.fallSwingHipLift);
                // 0.25 never walks the 16° cap (liftSignal −0.20 at
                // vertical); 0.5 is −0.40 and still fades at 16° — not
                // sci_a 40°. Reach/Catch scrape keep 0.25. Flag 0 is
                // the former bias. Zero stay zero so -fallSwingHipLift 0
                // still disables. That fade is still 0 at the cap;
                // fallSwingFallHipLiftCapHold floors it at 0.5 in
                // ApplyFallCatchSwingThighHold (Reach floor is Reach-only).
                // leftover→0 on that same clock still lost the height
                // fight; fallSwingFallVaultLead is the 2× vault, not
                // another hipLift fade. hipLift / ankleLift still shared
                // that 1× ramp; fallSwingFallLiftLead is the 2× lift.
                // Fold still shared the 1× ramp after that (70° at
                // lift=1); fallSwingFallFoldLead is the 2× fold. Reach
                // continues that clock.
                if (driver.fallSwingFallHipLift > 0.5f && hipLiftAmt > 0.001f)
                    hipLiftAmt = 0.5f;
                float fallLeadClock = FallCatchLiftLeadClock(driver, fallLiftClock);
                fc.hipLift = hipLiftAmt * fallLeadClock;
                float ankleLiftAmt = Mathf.Clamp01(driver.fallSwingAnkleLift);
                // 0.25 fade still leaves 0.5 of a full plantarflex after
                // FallAnklePush; µ=0.4 slides the 16° hipLift. 0.5 cancels
                // that push to 0 — not a 1.0 dorsiflexor (heel-press).
                // Reach may write 0.5 (fallSwingReachAnkleLift); Catch
                // air may write 0.5 (fallSwingCatchAirAnkleLift); Catch
                // scrape may write 0.5 (fallSwingCatchScrapeAnkleLift);
                // Catch→Reach scrape may write 0.5
                // (fallSwingCatchReachScrapeAnkleLift).
                // Flag 0 is the former fade.
                // Zero stay zero so -fallSwingAnkleLift 0 still disables.
                if (driver.fallSwingFallAnkleLift > 0.5f && ankleLiftAmt > 0.001f)
                    ankleLiftAmt = 0.5f;
                fc.ankleLift = ankleLiftAmt * fallLeadClock;
                fc.airAnkle = Mathf.Clamp01(driver.fallSwingAirAnkle);
                fc.airHeel = Mathf.Max(0f, driver.fallSwingAirHeel);
                fc.ignoreFalling = true;
                break;
            case StepPhaseDriver.FallCatchPhase.Reach:
                // The stance ankle used to sit at 10% for the whole reach —
                // 0.68 s of one-leg standing with no CoP authority, and the
                // trunk ran to −28° before the foot was placed. Give the
                // authority back exactly while the trunk is going (blend 0)
                // and step aside when it is calm (blend 1), so a healthy
                // forward fall is still not fought.
                // Cadence leftovers (ankleScale floor 0.55, lean Lerp 0.35
                // at blend 0, a Reach comLead) are not restored: they ran
                // the trunk to 120°. Blend still kills lean when tipped.
                // Two-sided (calm), not blend: a trunk already tipping
                // backward is not a calm trunk and must keep the ankle.
                // scrapeBrakeHold: Catch→Reach on a toe scrape still took
                // this Reach envelope (calm-reduced ankle + forward lean)
                // after Catch RecoverBrake. The stance heel is the only
                // real plant; cutting its CoP while the swing toe jams is
                // the trunk pair of scrapeArmHold. Keep the Catch brake;
                // the swing pose below still runs. Air bounce unchanged.
                // scrapeBrakeAirAge: that hold latched RecoverBrake for
                // the whole resumed Reach, so a later lift-off never took
                // Reach lean. Resume from that lift-off (the foot is up;
                // the body must keep falling). Flag 0 is the former Catch
                // brake even in air.
                bool scrapeBrake = driver.ReachResumedFromScrape
                    && driver.fallSwingCatchReachScrapeBrakeHold > 0.5f;
                if (scrapeBrake
                    && driver.fallSwingCatchReachScrapeBrakeAirAge > 0.5f
                    && !driver.SwingFootIsGrounded)
                    scrapeBrake = false;
                if (scrapeBrake)
                    ApplyRecoverBrake(
                        ref fc, driver, lean, fallComLead, blend, blendBack);
                else
                {
                    fc.ankleScale = Mathf.Lerp(1f, 0.25f, calm);
                    // 0.25 was a constant: it cut the hip strategy whatever the
                    // trunk did. Keep that value for a forward fall (blendBack 1
                    // there — the reach must not fight the fall it is catching)
                    // and hand the loop back when the trunk goes BACKWARD, where
                    // leaning the chest back drives the hips forward and is the
                    // only recovery a single-support reach has.
                    fc.hipBalanceScale = Mathf.Lerp(1f, 0.25f, blendBack);
                    fc.pelvisLean = lean * 0.75f * calm;
                    fc.torsoLean = lean * 0.75f * calm;
                }
                fc.forceSwingFlex = true;
                // fallSwingReachAirHold: Fall hipLift 0.5 + 16° can now
                // make this air. Hipair then latches the same frame and
                // takes −swingHipFlex (sci_a). Keep ThighHold; Catch→Reach
                // (ReachResumedFromCatch) still reached until
                // fallSwingCatchAirEnterHold. Flag 0 is the
                // former latch jump.
                // fallSwingReachHipLiftCap: that air still faded hipLift
                // at 8°, so a 16° Fall thigh had liftSignal 0 (hold only
                // fires past vertical going back) and dropped ~8°. Same
                // 16° fade-out as Fall; Catch scrape (ReachResumedFromScrape)
                // may use 16° (fallSwingCatchReachScrapeHipLiftCap). That
                // 16° Reach still had room=0 once Fall walked into 24°;
                // fallSwingReachHipLiftCapWide keeps 24° in Reach.
                // That 24° Reach still had room=0 once Fall walked into
                // 32°; fallSwingReachHipLiftCapCeil keeps 32° in Reach.
                // That 24° Catch-air still had room=0 once Reach walked
                // into 32°; fallSwingCatchAirHipLiftCapCeil keeps 32° in
                // Catch-air. That same room=0 still pinned Plant at 24°;
                // fallSwingPlantHipLiftCapCeil keeps 32° in Plant. That
                // same room=0 still pinned Catch scrape at 24° after
                // Catch-air walked into 32°;
                // fallSwingCatchScrapeHipLiftCapCeil keeps 32° in Catch
                // scrape. That 24° Catch→Reach scrape still had room=0
                // once Catch scrape walked into 32°;
                // fallSwingCatchReachScrapeHipLiftCapCeil keeps 32° on
                // that bounce. That
                // 16° Catch→Reach scrape still faded to liftSignal 0 at
                // the cap; fallSwingCatchReachScrapeHipLiftCapHold floors
                // it at 0.5. That floor still writes hipLift 0.25
                // (liftSignal −0.125 vs Fall/Reach/Catch-air/Catch-scrape
                // −0.25); fallSwingCatchReachScrapeHipLift writes 0.5 on
                // that bounce. That 16°/0.5 hipLift still wrote ankleLift
                // 0.25; fallSwingCatchReachScrapeAnkleLift writes 0.5 on
                // that bounce. That 16° cap still had room=0 at a 16°
                // Catch-scrape thigh; fallSwingCatchReachScrapeHipLiftCapWide
                // keeps 24° on that bounce. That same room=0 still pinned
                // Catch scrape at 16°; fallSwingCatchScrapeHipLiftCapWide
                // keeps 24° in Catch scrape. Flag 0
                // is the former 8°.
                // fallSwingReachHipLift: that 16° Reach still wrote 0.25
                // (liftSignal −0.20 at vertical), so a Fall thigh that
                // drops a few degrees never walks back and the foot
                // replants. 0.5 is −0.40 and still fades at 16°; Catch
                // scrape may write 0.5 (fallSwingCatchReachScrapeHipLift).
                // Flag 0 is the former 0.25.
                // fallSwingReachAnkleLift: that 16°/0.5 Reach still wrote
                // 0.25 fade, so leftover plantarflex after FallAnklePush
                // still slides on µ=0.4 and the foot replants. 0.5
                // cancels a full push to 0; Catch air may write the
                // same 0.5 (fallSwingCatchAirAnkleLift); Catch scrape
                // may write the same 0.5 (fallSwingCatchScrapeAnkleLift);
                // Catch→Reach scrape may write the same 0.5
                // (fallSwingCatchReachScrapeAnkleLift).
                // Flag 0 is the former 0.25.
                // fallSwingReachHipLiftCapHold: that 16°/0.5/0.5 Reach
                // still had liftSignal 0 at the cap (P to the target;
                // hold only fires past vertical going back), so gravity
                // dropped the thigh and the foot replanted. 0.5 fade
                // floor holds the operating point (−0.25 at the cap when
                // hipLift is 0.5) and decays over the 4° deadzone past
                // it; Catch scrape may floor the same way
                // (fallSwingCatchScrapeHipLiftCapHold); Catch→Reach scrape
                // may floor the same way
                // (fallSwingCatchReachScrapeHipLiftCapHold). Flag 0 is the
                // former P=0 at the cap.
                // scrapeLiftAirEnterHold: Catch scrape then air already
                // kept ThighHold. CatchScrapedThisPhase is cleared on this
                // bounce, so forceSwingFlex would take −swingHipFlex
                // (sci_a). catchScrapeLift suppresses that hip pose when
                // ReachResumedFromCatchScrapeAir is latched. scrapeLiftAirCatchHold
                // then keeps ThighHold on the return Reach→Catch (that
                // latch is cleared). True airborne Catch (never scraped)
                // still reaches. 01:12 then clamped Reach air-swing;
                // Catch→Reach scrape-air still left hipair on (skip-add
                // only). The keep now clears that pose.
                // Hold reachKneeFlex until the foot is up. ReachExtendProgress
                // used to tick from phase enter, so a grounded Reach (Fall
                // timeout, or a scrape) extended toward landDeg and pinned
                // the foot. Progress is air-clocked; after heel-plant a toe
                // scrape still counts as air for that clock (scrapeExtend)
                // so the shin can drop the heel. Hip pose stays grounded-gated.
                // fallSwingReachLiftClock: Fall already ramps fold / hipLift /
                // ankleLift on PhaseAge. Reach used to start those at 1, so
                // an early lift (the Reach gate) jumped 70°→140° on the first
                // air frame. Same vault clock as the stance strut; flag 0 is
                // the former full fold. Timeout-to-Reach (clock already 1)
                // matches the old Lerp start.
                // fallSwingCatchReachResume: Catch→Reach in air used to
                // reset PhaseAge and reachAirEnterTime, so this clock
                // restarted from LastFallDuration / 0 and ReachExtendProgress
                // jumped to 0 — shin 35°→140° mid-air. Keep the air extend
                // clock; Fall→Reach is unchanged.
                // fallSwingCatchReachScrapeResume: Catch timeout also
                // bounces on a toe scrape. Air-only resume then reset the
                // clock (reachAirEnterTime -1) and refolded 35°→140° on
                // dirt. Resume on !planted; lift / arm / vault follow
                // ReachResumedFromCatch. scrapeLiftHold / scrapeVaultHold /
                // scrapeArmHold then freeze those extraAge clocks on dirt.
                // Flag 0 is the former air-only
                // resume.
                // fallSwingCatchReachScrapeLiftHold: that scrape still
                // added the new Reach PhaseAge onto the frozen fold
                // (liftAge). Catch airLiftHold had stopped at ~70°;
                // the bounce then finished 70°→140° on dirt, so airHeel
                // could not drop the heel. Freeze extraAge on a scrape
                // resume; an airborne Catch→Reach still ramps. Flag 0
                // is the former scrape liftAge.
                // fallSwingCatchReachScrapeLiftAirAge: that freeze then
                // still held extraAge=0 after the toe left, so fold /
                // hipLift / ankleLift sat at Catch enter for the whole
                // second Reach. Continue from lift-off only. Flag 0 is
                // the former extraAge=0 even in air.
                // fallSwingCatchReachScrapeVaultHold: that scrape still
                // added the new Reach PhaseAge onto the frozen vault
                // remainder (vaultAge). Catch airVaultRemain had held
                // leftover; the bounce then finished leftover→0 on the
                // only planted heel. Freeze extraAge on a scrape resume;
                // an airborne Catch→Reach still ramps. Flag 0 is the
                // former scrape vaultAge.
                // fallSwingCatchReachScrapeVaultAirAge: that freeze then
                // still held extraAge=0 / Catch remain after the toe left,
                // so leftover vault sat at Catch enter for the whole
                // second Reach. Continue from lift-off only (yield remain
                // on dirt, leftover→0 in air, no leftover plantKnee).
                // Flag 0 is the former extraAge=0 even in air.
                // fallSwingCatchReachScrapeArmHold: that scrape still
                // added the new Reach PhaseAge onto the frozen arm
                // envelope (armAge). Catch airArmHold had held ~0.5;
                // the bounce then finished ~0.5→1 on dirt — third-law
                // pair of the fold / leftover yank, trunk yanks while
                // airHeel cannot plant. Freeze extraAge on a scrape
                // resume; an airborne Catch→Reach still ramps. Flag 0
                // is the former scrape armAge.
                // fallSwingCatchReachScrapeArmAirAge: that freeze then
                // still held extraAge=0 after the toe left, so reachArm
                // sat at Catch enter (~0.5) for the whole second Reach.
                // Continue from lift-off only. Flag 0 is the former
                // extraAge=0 even in air.
                // fallSwingCatchReachScrapeBrakeHold: those extraAge
                // freezes still left the trunk envelope. Catch
                // RecoverBrake (full stance ankle, signed CoP) dropped
                // to Reach lean / Lerp 1→0.25 ankle on dirt — the
                // stance pair of scrapeArmHold, while the swing toe is
                // jammed and airHeel cannot plant. Keep the Catch
                // brake; Reach swing pose still runs. Air Catch→Reach
                // still takes Reach lean. Flag 0 is the former Reach
                // envelope on scrape.
                // fallSwingCatchReachScrapeBrakeAirAge: that freeze then
                // still held RecoverBrake after the toe left, so Reach
                // lean sat off for the whole second Reach — the trunk
                // pair of scrapeArmAirAge. Continue from lift-off only.
                // Flag 0 is the former Catch brake even in air.
                // fallSwingCatchReachScrapeYieldHold: that scrape still
                // ran ApplyFallCatchVault, which sets stancePlantKnee to
                // leftover then Lerp(that, 0, frozenClock). Catch air
                // yield never poses leftover on the trailing strut.
                // Early-lift bounce (clock ~0.5) held a near-quiet strut
                // then re-bent ~35°→17° on the only heel. Keep Catch
                // yield (Catch clock, !planted → remain). scrapeVaultAirAge
                // continues that remain on dirt. Air Catch→Reach
                // still vaults. Flag 0 is the former leftover plantKnee
                // on scrape.
                // fallSwingCatchReachScrapeYieldAirAge: that freeze then
                // still held Catch yield after the toe left, so leftover
                // plantKnee sat off for the whole second Reach — the strut
                // pair of scrapeBrakeAirAge. Continue from lift-off only
                // (ApplyFallCatchVault, leftover pose in air). Flag 0 is
                // the former Catch yield even in air.
                // fallSwingCatchReachScrapeExtendHold: scrapeExtend still
                // treats a heel-up scrape as air, so ReachExtendProgress
                // keeps ticking. Lerp(frozenFold, landDeg, extend) then
                // finishes 70°→35° on the jammed toe — the shin pair of
                // scrapeLiftHold — and TryFallCatchArmSwing lerps
                // reachArm→carry on that same extend (Last updates every
                // Reach tick when !holdFold). Keep the Catch-enter extend;
                // airborne Catch→Reach still ramps. Flag 0 is the former
                // scrape extend ramp.
                // fallSwingCatchReachScrapeExtendAirAge: that freeze then
                // still held Last after the toe left, so Lerp(fold, landDeg)
                // / arm carry sat at Catch enter for the whole second
                // Reach. Continue from lift-off only (not live Progress:
                // scrapeExtend counted dirt as air). Flag 0 is the former
                // frozen Last even in air.
                // fallSwingCatchReachScrapeExtendEnterHold: Catch
                // scrapeExtendHold froze CatchReachShinDeg at Last, but
                // Catch→Reach enter still Max'd Last with live Progress.
                // scrapeExtend ticks on heel-up, so that Max is the
                // bounce frame of 70°→35° / carry. Keep Last; airborne
                // bounce still Max's. Flag 0 is the former enter Max.
                // fallSwingCatchScrapeExtendAirEnterHold: that keep is
                // grounded-only. After Catch scrape then lift-off, AirAge
                // already resumed Last; the timeout bounce still Max'd
                // live Progress and Tick wrote the jump. Keep Last and
                // latch ReachResumedFromScrape. Flag 0 is the former Max
                // even after Catch scrape then air.
                // fallSwingCatchScrapeLiftAirEnterHold: that same bounce
                // still took forceSwingFlex −swingHipFlex after Catch
                // scrapeLiftAirHold. Keep ThighHold (catchScrapeLift)
                // through the resumed Reach. Flag 0 is the former jump.
                // 01:12 clamped Reach air-swing; that ramp is reachAirHold
                // only. Catch→Reach scrape-air still skip-added and
                // left hipair on (leftAir already true). The keep now
                // clears that pose so ThighHold at 32° runs. Flag 0 is
                // the former skip-add (hipair −swingHipFlex).
                // fallSwingCatchScrapeLiftAirCatchHold: that keep is
                // Catch→Reach only. Reach→Catch still took −swingHipFlex
                // after ReachResumedFromCatchScrapeAir cleared. Keep
                // ThighHold through that resumed Catch. Flag 0 is the
                // former return jump. scrapeLiftAirCatchFold then keeps
                // the Reach fold on that Catch (no 70°→35°).
                // scrapeLiftAirEnterFold then keeps that fold on
                // Catch→Reach: Last is stale, so Lerp(fold, landDeg, Last)
                // would drop the foot the Catch keep just raised.
                // scrapeLiftAirEnterGroundFold then keeps that fold after
                // the toe meets dirt (enterFold is air-only).
                // fallSwingCatchReachLiftClock: resume still forced
                // reachLiftClock = 1. Early Reach gate then trunkGone Catch
                // (extend 0) held CatchReachFoldDeg (~70°) then bounced to
                // 140° — the shin pair of catchReachArmClock. Continue the
                // frozen Fall+Reach clock; do not add LastCatch (0.55 s air
                // Catch would saturate the 0.15 s ramp to 1). Flag 0 is the
                // former resume at 1. Happy-path (clock already 1) is
                // unchanged.
                // fallSwingCatchReachLiftAge: that freeze also stalled the
                // leftover fold. The new Reach PhaseAge is 0, so hipLift /
                // ankleLift / fold sat at ~0.5 for the whole second Reach
                // and the foot never finished shortening. Add only that
                // age — still no LastCatch. Flag 0 is the 23:18 freeze.
                // fallSwingCatchReachArmAge: same stall on the shoulders
                // (reachArm held at FallCatchArmReachStart). TryFallCatchArmSwing
                // adds that same PhaseAge unless scrapeArmHold freezes it
                // on a toe scrape; scrapeArmAirAge then continues extraAge
                // from lift-off. Catch/Recover still pass 0.
                // fallSwingCatchReachVaultClock: same reset, the strut.
                // ApplyFallCatchVault still used LastFall+PhaseAge 0, so a
                // short Fall bent 0° toward landDeg. Resume keeps LastReach.
                // fallSwingCatchReachVaultAge: LastCatch on that 0.15 s
                // ramp saturates after catchMax (air Catch is not vault
                // progress). Continue the Fall+Reach remainder on the new
                // Reach PhaseAge — still no LastCatch. Flag 0 is the former
                // LastReach+LastCatch saturate. Flag vaultClock 0 is the
                // former restart.
                float reachLiftClock = 1f;
                if (driver.fallSwingReachLiftClock > 0.5f)
                {
                    if (driver.ReachResumedFromCatch)
                    {
                        if (driver.fallSwingCatchReachLiftClock > 0.5f)
                        {
                            float extra = driver.fallSwingCatchReachLiftAge > 0.5f
                                ? driver.PhaseAge()
                                : 0f;
                            if (driver.ReachResumedFromScrape
                                && driver.fallSwingCatchReachScrapeLiftHold > 0.5f)
                            {
                                extra = 0f;
                                // scrapeLiftHold latched extraAge=0 for the
                                // whole resumed Reach, so a later lift-off
                                // never finished 70°→140°. Resume from that
                                // lift-off (not PhaseAge: scrape time would
                                // saturate the 0.15 s ramp). Vault extraAge
                                // resume is scrapeVaultAirAge. Arm extraAge
                                // resume is scrapeArmAirAge. Flag 0 is the
                                // former extraAge=0 even in air.
                                if (driver.fallSwingCatchReachScrapeLiftAirAge > 0.5f
                                    && !driver.SwingFootIsGrounded)
                                    extra = driver.ScrapeResumeAirAge();
                            }
                            reachLiftClock = FallCatchFallReachClock(driver, extra);
                        }
                        else
                            reachLiftClock = 1f;
                    }
                    else
                        reachLiftClock = FallCatchVaultClock(
                            driver, StepPhaseDriver.FallCatchPhase.Reach);
                }
                float extend = driver.ReachExtendProgress();
                if (driver.ReachResumedFromCatch)
                    extend = Mathf.Max(extend, driver.LastReachExtendProgress);
                if (driver.ReachResumedFromScrape
                    && driver.fallSwingCatchReachScrapeExtendHold > 0.5f)
                {
                    extend = driver.LastReachExtendProgress;
                    // scrapeExtendHold latched Last for the whole resumed
                    // Reach, so a later lift-off never finished 70°→35°.
                    // Resume from that lift-off (not live Progress:
                    // scrapeExtend counted dirt as air). Flag 0 is the
                    // former frozen Last even in air.
                    if (driver.fallSwingCatchReachScrapeExtendAirAge > 0.5f
                        && !driver.SwingFootIsGrounded)
                        extend = driver.ScrapeResumeExtendProgress();
                }
                float fold = Mathf.Max(0f, driver.reachKneeFlex)
                    * FallCatchFoldLeadClock(driver, reachLiftClock);
                fc.reachKnee = Mathf.Lerp(
                    fold,
                    Mathf.Max(0f, driver.reachKneeLandDeg),
                    extend);
                // scrapeLiftAirCatchFold kept Reach fold in Catch, but
                // Catch→Reach still Lerped fold→landDeg on Last. Last is
                // the Catch-enter freeze (or AirAge resume), not the fold
                // override, so the bounce is 140°→~56° on the same
                // airborne toe — the shin pair of scrapeLiftAirEnterHold.
                // Keep fold (skip the extend Lerp) until the toe meets
                // dirt. Grounded Reach fold is enterGroundFold. Never-scrape
                // Catch→Reach fold is catchAirEnterFold (enterFold is
                // scrape-only). Flag 0 is the former
                // plant-extend on the bounce. Shoulder pair of this skip
                // (Last-carry) is fallSwingCatchScrapeLiftAirEnterArm.
                // Grounded Reach Last-carry after that keep is
                // fallSwingCatchScrapeLiftAirEnterGroundArm.
                // Catch-phase Last-carry (same airborne Catch as catchFold)
                // is fallSwingCatchScrapeLiftAirCatchArm. Recover/Plant
                // CatchEnd Last-carry after Catch flags clear is
                // fallSwingCatchScrapeLiftAirCatchEndArm. Grounded Catch
                // Last-carry on toe-down after that keep is
                // fallSwingCatchScrapeLiftAirCatchGroundArm. True airborne
                // Catch Last-carry (never scraped) is
                // fallSwingCatchScrapeLiftAirCatchAirArm. Recover/Plant
                // CatchEnd Last-carry after that keep (scrape latch is
                // scrape-only) is fallSwingCatchScrapeLiftAirCatchAirEndArm.
                // Grounded never-scrape Catch Last-carry on toe-down after
                // that keep is fallSwingCatchScrapeLiftAirCatchAirGroundArm.
                if (driver.fallSwingCatchScrapeLiftAirEnterFold > 0.5f
                    && driver.fallSwingCatchScrapeLiftAirEnterHold > 0.5f
                    && driver.fallSwingCatchScrapeLiftAirHold > 0.5f
                    && driver.fallSwingCatchAirLiftHold > 0.5f
                    && driver.fallSwingCatchScrapeLiftAirCatchFold > 0.5f
                    && driver.ReachResumedFromCatchScrapeAir
                    && !driver.SwingFootIsGrounded)
                    fc.reachKnee = fold;
                // scrapeLiftAirEnterFold kept Reach fold in air, but
                // grounded Reach still Lerped fold→landDeg on Last.
                // Last is the Catch-enter freeze (or AirAge resume), not
                // the enterFold override, so the first toe-down Reach
                // frame is 140°→~56° against leftover shin — the shin
                // pair of enterGroundArm. Keep fold (skip the extend
                // Lerp) on dirt too. ReachResumedFromCatchScrapeAir
                // survives the toe-down. Heel plant still leaves Reach
                // for Catch. Grounded Catch fold is catchGroundFold.
                // Flag 0 is the former landDeg lerp
                // on grounded Reach.
                if (driver.fallSwingCatchScrapeLiftAirEnterGroundFold > 0.5f
                    && driver.fallSwingCatchScrapeLiftAirEnterGroundArm > 0.5f
                    && driver.fallSwingCatchScrapeLiftAirEnterArm > 0.5f
                    && driver.fallSwingCatchScrapeLiftAirEnterFold > 0.5f
                    && driver.fallSwingCatchScrapeLiftAirEnterHold > 0.5f
                    && driver.fallSwingCatchScrapeLiftAirHold > 0.5f
                    && driver.fallSwingCatchAirLiftHold > 0.5f
                    && driver.fallSwingCatchScrapeLiftAirCatchFold > 0.5f
                    && driver.ReachResumedFromCatchScrapeAir
                    && driver.SwingFootIsGrounded)
                    fc.reachKnee = fold;
                // scrapeLiftAirCatchAirFold kept Reach fold in never-scrape
                // air Catch, but Catch→Reach still Lerped fold→landDeg on
                // Last. enterFold requires ReachResumedFromCatchScrapeAir,
                // so timeout/abort Catch with the toe still up then bouncing
                // to Reach is 140°→~56° against leftover shin — the
                // Catch→Reach shin pair of catchAirFold. Keep fold (skip
                // the extend Lerp) until the toe meets dirt. extraAge 0 on
                // Catch; the bounce already continues fold on Reach
                // PhaseAge (liftAge). Heel plant still Lerps. Grounded
                // never-scrape Reach fold is catchAirEnterGroundFold
                // (enterGroundFold is scrape-only). Flag 0 is the former
                // landDeg lerp on the never-scrape bounce. Shoulder pair
                // of this skip is catchAirEnterArm (enterArm is
                // scrape-only). Grounded never-scrape Reach Last-carry
                // after that keep is catchAirEnterGroundArm
                // (enterGroundArm is scrape-only).
                if (driver.fallSwingCatchScrapeLiftAirCatchAirEnterFold > 0.5f
                    && driver.fallSwingCatchScrapeLiftAirCatchAirFold > 0.5f
                    && driver.fallSwingCatchScrapeLiftAirCatchAirArm > 0.5f
                    && driver.fallSwingCatchScrapeLiftAirCatchArm > 0.5f
                    && driver.ReachResumedFromCatch
                    && !driver.ReachResumedFromCatchScrapeAir
                    && !driver.SwingFootIsGrounded)
                    fc.reachKnee = fold;
                // scrapeLiftAirCatchAirEnterFold kept Reach fold in air,
                // but grounded never-scrape Reach still Lerped
                // fold→landDeg on Last. enterGroundFold requires
                // ReachResumedFromCatchScrapeAir, so timeout/abort Catch
                // with the toe still up then bouncing to Reach then
                // toe-down is 140°→~56° against leftover shin — the dirt
                // sibling of catchAirEnterFold, the shin pair of
                // catchAirEnterGroundArm. Keep fold (skip the extend
                // Lerp) on dirt too. ReachResumedFromCatch survives the
                // toe-down. Heel plant still leaves Reach for Catch.
                // Flag 0 is the former landDeg lerp on grounded
                // never-scrape Reach. Recover/Plant CatchEnd Last-carry
                // after that bounce's planted Catch is
                // fallSwingCatchScrapeLiftAirCatchAirEnterEndArm.
                // Catch-phase Last-carry on that planted Catch is
                // fallSwingCatchScrapeLiftAirCatchAirEnterCatchArm.
                // Catch-phase shin Max(Last, CatchPlantAge) on that
                // planted Catch is
                // fallSwingCatchScrapeLiftAirCatchAirEnterCatchFold.
                // Recover/Plant shin Max(Last, held+PhaseAge) after that
                // Catch is
                // fallSwingCatchScrapeLiftAirCatchAirEnterEndFold.
                // Catch planted scrape shin Max(Last, CatchPlantAge) is
                // fallSwingCatchScrapeLiftAirCatchGroundCatchFold.
                // Never-scrape air Recover/Plant shin Max(Last) is
                // fallSwingCatchScrapeLiftAirCatchAirEndFold.
                // Catch planted never-scrape shin Max(Last) is
                // fallSwingCatchScrapeLiftAirCatchAirGroundCatchFold.
                if (driver.fallSwingCatchScrapeLiftAirCatchAirEnterGroundFold > 0.5f
                    && driver.fallSwingCatchScrapeLiftAirCatchAirEnterGroundArm > 0.5f
                    && driver.fallSwingCatchScrapeLiftAirCatchAirEnterArm > 0.5f
                    && driver.fallSwingCatchScrapeLiftAirCatchAirEnterFold > 0.5f
                    && driver.fallSwingCatchScrapeLiftAirCatchAirFold > 0.5f
                    && driver.fallSwingCatchScrapeLiftAirCatchAirArm > 0.5f
                    && driver.fallSwingCatchScrapeLiftAirCatchArm > 0.5f
                    && driver.ReachResumedFromCatch
                    && !driver.ReachResumedFromCatchScrapeAir
                    && driver.SwingFootIsGrounded)
                    fc.reachKnee = fold;
                // scrapeYieldHold: Catch→Reach on a toe scrape still took
                // ApplyFallCatchVault leftover plantKnee. Keep Catch yield
                // on dirt. scrapeYieldAirAge: that hold latched yield for
                // the whole resumed Reach, so a later lift-off never took
                // the Reach vault. Resume from that lift-off (the foot is
                // up; leftover plantKnee in air is the airborne bounce).
                // Flag 0 is the former Catch yield even in air.
                if (driver.ReachResumedFromScrape
                    && driver.fallSwingCatchReachScrapeYieldHold > 0.5f
                    && !(driver.fallSwingCatchReachScrapeYieldAirAge > 0.5f
                        && !driver.SwingFootIsGrounded))
                {
                    float yieldExtra = 0f;
                    if (driver.fallSwingCatchReachScrapeVaultAirAge > 0.5f
                        && !driver.SwingFootIsGrounded)
                        yieldExtra = driver.ScrapeResumeAirAge();
                    ApplyCatchVaultYield(
                        ref fc, driver,
                        CatchVaultYieldClock(
                            driver, StepPhaseDriver.FallCatchPhase.Catch, yieldExtra));
                }
                else
                    ApplyFallCatchVault(
                        ref fc, driver,
                        FallCatchVaultClock(driver, StepPhaseDriver.FallCatchPhase.Reach));
                fc.thighHold = Mathf.Clamp01(driver.fallSwingThighHold);
                float reachHipLiftAmt = Mathf.Clamp01(driver.fallSwingHipLift);
                // 0.25 never walks the 16° cap (liftSignal −0.20 at
                // vertical); 0.5 is −0.40 and still fades at 16° — not
                // sci_a 40°. Catch scrape (ReachResumedFromScrape) may
                // write 0.5 (fallSwingCatchReachScrapeHipLift). Flag 0
                // is the former bias. Zero stay zero so -fallSwingHipLift
                // 0 still disables. That fade is still 0 at the cap;
                // fallSwingReachHipLiftCapHold floors it at 0.5 in
                // ApplyFallCatchSwingThighHold; Catch→Reach scrape floors
                // the same way (fallSwingCatchReachScrapeHipLiftCapHold).
                if (driver.fallSwingReachHipLift > 0.5f
                    && reachHipLiftAmt > 0.001f
                    && !driver.ReachResumedFromScrape)
                    reachHipLiftAmt = 0.5f;
                // Catch→Reach scrape still wrote 0.25 at the 16°/0.5
                // floor (liftSignal −0.125 vs Fall/Reach/Catch-air/
                // Catch-scrape −0.25). 0.5 matches those so the floor
                // actually holds; ankleLift on that resume may then write
                // 0.5 (fallSwingCatchReachScrapeAnkleLift). Flag 0 is the
                // former bias. Zero stay zero so
                // -fallSwingHipLift 0 still disables. Not sci_a 40°.
                if (driver.fallSwingCatchReachScrapeHipLift > 0.5f
                    && reachHipLiftAmt > 0.001f
                    && driver.ReachResumedFromScrape)
                    reachHipLiftAmt = 0.5f;
                float reachLeadClock = driver.ReachResumedFromScrape
                    ? FallCatchScrapeLiftLeadClock(driver, reachLiftClock)
                    : FallCatchLiftLeadClock(driver, reachLiftClock);
                fc.hipLift = reachHipLiftAmt * reachLeadClock;
                float reachAnkleLiftAmt = Mathf.Clamp01(driver.fallSwingAnkleLift);
                // 0.25 fade still leaves 0.5 of leftover plantarflex after
                // FallAnklePush; µ=0.4 slides the 16°/0.5 hipLift. 0.5
                // cancels that push to 0 — not a 1.0 dorsiflexor
                // (heel-press). Catch air may write 0.5
                // (fallSwingCatchAirAnkleLift); Catch scrape may write
                // 0.5 (fallSwingCatchScrapeAnkleLift); Catch→Reach scrape
                // may write 0.5 (fallSwingCatchReachScrapeAnkleLift).
                // Flag 0 is the
                // former fade. Zero stay zero so
                // -fallSwingAnkleLift 0 still disables.
                if (driver.fallSwingReachAnkleLift > 0.5f
                    && reachAnkleLiftAmt > 0.001f
                    && !driver.ReachResumedFromScrape)
                    reachAnkleLiftAmt = 0.5f;
                // Catch→Reach scrape still wrote 0.25: leftover
                // plantarflex * (1−0.25) − 0.25 is 0.5 of a full push
                // on µ=0.4, so the 16°/0.5 hipLift slides. 0.5 cancels
                // that push to 0 — same as Fall/Reach/Catch-air/
                // Catch-scrape, not a 1.0 dorsiflexor. Flag 0 is the
                // former fade. Zero stay zero so -fallSwingAnkleLift
                // 0 still disables. Not sci_a 40°.
                if (driver.fallSwingCatchReachScrapeAnkleLift > 0.5f
                    && reachAnkleLiftAmt > 0.001f
                    && driver.ReachResumedFromScrape)
                    reachAnkleLiftAmt = 0.5f;
                fc.ankleLift = reachAnkleLiftAmt * reachLeadClock;
                fc.airAnkle = Mathf.Clamp01(driver.fallSwingAirAnkle);
                fc.airHeel = Mathf.Max(0f, driver.fallSwingAirHeel);
                fc.ignoreFalling = true;
                break;
            case StepPhaseDriver.FallCatchPhase.Catch:
                // Landing brake: CoP well behind CoM (catchComLead, not the
                // 2.5 cm Fall lead), hips hold a backward pelvis/chest
                // target, hipBalanceScale 0 so CoM-ahead does not dive the
                // trunk further. Negative pelvisLean is subtracted → plus
                // world tilt = backward (hipBaseAngle comment: minus = forward).
                // Reach hip stays on only in the air (forceSwingFlex + plant).
                // Same shaped brake as Recover, not a fixed one. A constant
                // full -catchComLead with hipBalanceScale 0 was a whip, not
                // a brake: it kept pushing backward after the trunk had
                // already passed upright (walk2_blend2: −15° / −121 deg/s
                // to +24° / +464 deg/s in 0.14 s, both ankles welded on the
                // +45° stop, and ProtectJointLimit then zeroed them).
                // Cycle 0 without a stance-span plant: this brake +
                // 0.15×catchKnee is a landing snap under the hip on stacked
                // soles (walkSwap=0). Grounded Catch keeps DrivePelvis and
                // the oneg knee (no 55° last-write) until GetContacts
                // clears. Air Catch plants 8°/6° and latches through
                // Recover. Recover swap is PickStanceFromFeet.
                bool firstStepPlace = driver.FallCatchCycleCount == 0;
                if (firstStepPlace)
                {
                    float leanAmt = lean * Mathf.Max(0f, driver.fallForwardLeanScale);
                    fc.pelvisLean += leanAmt;
                    fc.torsoLean += leanAmt;
                    fc.ignoreFalling = true;
                    // Cycle-0 Fall times out to Catch at fallKneeRampSeconds
                    // with the trail still down. comLead 0 + ankleScale 1
                    // is the WALK-2 root: the stance ankle holds CoM over
                    // the stacked feet, so the 23.5° lean is a lunge and
                    // Catch never sees a fall to catch. Same forward CoP
                    // as Fall. blendBack: do not add it on a backward trunk.
                    fc.comLead = Mathf.Max(0f, fallComLead) * blendBack;
                    fc.ankleScale = 1f;
                    // Planted ahead of stance: stop shoving CoM forward.
                    // Recover swaps on that span; the landing ankle still
                    // starts the CoP arrest here so Plant does not inherit
                    // a running lead (the trunk lean on the same error was
                    // the +22° bounce).
                    // catchKnee stays 0 (comment 18: brake+catchKnee lifted
                    // the 8°/6° plant).
                    if (driver.SwingFootIsGrounded && driver.SwingFootAheadOfStance)
                    {
                        fc.catchAnkleOnSwing = true;
                        ApplyRecoverBrake(
                            ref fc, driver, lean, fallComLead, blend, blendBack);
                        fc.catchKnee = 0f;
                        fc.plantKnee = 0f;
                    }
                }
                else
                {
                    fc.catchAnkleOnSwing = true;
                    ApplyRecoverBrake(ref fc, driver, lean, fallComLead, blend, blendBack);
                }
                fc.forceSwingFlex = true;
                fc.catchKnee = firstStepPlace ? 0f : driver.catchKneeFlex;
                // Catch on a timeout still has the foot in the air: keep the
                // landing extension so the knee does not refold right before
                // touchdown. A heel plant holds landDeg when catchLandKnee
                // is on — do not snap to 0.15×catchKnee on that same heel.
                // fallSwingCatchExtendClock: Reach was still Lerp(fold, landDeg,
                // progress). Instant landDeg is 70°→35° on an early heel (the
                // Reach gate). Continue that lerp; flag 0 is the old jump.
                // fallSwingCatchAirExtendHold: leftover still used Catch
                // PhaseAge, so a timeout Catch counted air as plant and
                // snapped fold→landDeg on the first heel-loaded frame.
                // Plant age holds the fold until the heel loads; flag 0 is
                // the old air leftover.
                // fallSwingCatchScrapeExtendHold: air Catch still Max'd
                // Last with live ReachExtendProgress. scrapeExtend ticks
                // on a heel-up scrape, so CatchReachShinDeg finished
                // 70°→35° on the jammed toe, then Catch→Reach
                // scrapeExtendHold froze Last and snapped back. Keep Last
                // on this scrape; true air still ramps. Flag 0 is the
                // former live Progress.
                // fallSwingCatchScrapeExtendAirAge: that freeze then
                // Max'd Last with live Progress after the toe left, so
                // the first air frame jumped 70°→35° (scrapeExtend
                // counted dirt as air). Resume from lift-off only.
                // Flag 0 is the former live Progress even in air.
                // fallSwingCatchReachScrapeExtendEnterHold: Catch→Reach
                // enter still Max'd Last with live Progress, so the
                // bounce frame wrote 70°→35° / carry into Last and
                // scrapeExtendHold (Tick) froze at that new value. Keep
                // Last; airborne bounce still Max's. Flag 0 is the
                // former enter Max.
                // fallSwingCatchScrapeExtendAirEnterHold: that keep is
                // grounded-only. After Catch scrape then lift-off, AirAge
                // already resumed Last; the timeout bounce still Max'd
                // live Progress and Tick wrote the jump. Keep Last and
                // latch ReachResumedFromScrape. Flag 0 is the former Max
                // even after Catch scrape then air.
                // fallSwingPlantStanceExtendClock: Recover then swaps and
                // Plant used to command landDeg, so a 0.05 s Recover still
                // jumped leftover on the new stance. Continue that lerp.
                // fallSwingVaultFromLeftover: Fall then vaulted from landDeg
                // anyway; leftover still on catchMin jumped fold→landDeg
                // on the only loaded heel. Vault from the frozen Plant shin.
                fc.reachKnee = Mathf.Max(0f, driver.reachKneeLandDeg);
                if (driver.fallSwingCatchLandKnee > 0.5f)
                    fc.plantKnee = Mathf.Max(0f, driver.reachKneeLandDeg);
                if (driver.fallSwingCatchExtendClock > 0.5f)
                {
                    float shin = CatchReachShinDeg(
                        driver, StepPhaseDriver.FallCatchPhase.Catch);
                    fc.reachKnee = shin;
                    if (driver.fallSwingCatchLandKnee > 0.5f)
                        fc.plantKnee = shin;
                }
                // scrapeLiftAirCatchHold kept ThighHold on Reach→Catch, but
                // CatchReachShinDeg still Lerped fold→landDeg. After scrape
                // then air that is 70°→35° on the toe that just left — the
                // shin pair of the hip keep, swingFoot 0.92. Keep the Reach
                // fold and continue extraAge from lift-off (not PhaseAge:
                // scrape time would saturate the 0.15 s ramp on dirt).
                // Heel plant still takes landDeg. Stance ankle / pelvis
                // loop unchanged. Flag 0 is the former plant-extend.
                bool catchScrapeAirFold =
                    driver.fallSwingCatchScrapeLiftAirCatchFold > 0.5f
                    && driver.fallSwingCatchAirLiftHold > 0.5f
                    && driver.fallSwingCatchScrapeLiftAirHold > 0.5f
                    && !driver.SwingFootIsGrounded
                    && (driver.CatchScrapedThisPhase
                        || (driver.CatchResumedFromReachScrapeAir
                            && driver.fallSwingCatchScrapeLiftAirCatchHold > 0.5f));
                if (catchScrapeAirFold)
                {
                    float extra = driver.ScrapeResumeAirActive
                        ? driver.ScrapeResumeAirAge()
                        : 0f;
                    fc.reachKnee = Mathf.Max(0f, driver.reachKneeFlex)
                        * FallCatchFoldLeadClock(
                            driver, FallCatchFallReachClock(driver, extra));
                    // Skip catchKnee's 0.15× straighten / 115 clamp so the
                    // fold can reach 120° (same stop as Fall/Reach lift).
                    fc.catchKnee = 0f;
                }
                // scrapeLiftAirCatchAirArm kept reachArm in never-scrape
                // air, but Catch still Lerped fold→landDeg on
                // CatchReachShinDeg (catchFold is scrape-only). Last /
                // Progress is the Reach freeze at Catch enter, not a
                // scrape, so timeout/abort Catch with the toe still up is
                // 140°→~56° against leftover shin — the shin pair of
                // catchAirArm. Keep fold (skip the landDeg Lerp) until
                // the toe meets dirt. extraAge 0: air Catch is not scrape
                // lift-off. Heel plant still takes landDeg. Grounded
                // never-scrape Catch fold is catchAirGroundFold. Flag 0
                // is the former plant-extend on never-scrape air.
                bool catchAirFold =
                    driver.fallSwingCatchScrapeLiftAirCatchAirFold > 0.5f
                    && driver.fallSwingCatchScrapeLiftAirCatchAirArm > 0.5f
                    && driver.fallSwingCatchScrapeLiftAirCatchArm > 0.5f
                    && !driver.SwingFootIsGrounded
                    && !driver.CatchScrapedThisPhase
                    && !driver.CatchResumedFromReachScrapeAir;
                if (catchAirFold)
                {
                    fc.reachKnee = Mathf.Max(0f, driver.reachKneeFlex)
                        * FallCatchFoldLeadClock(
                            driver, FallCatchFallReachClock(driver));
                    // Skip catchKnee's 0.15× straighten / 115 clamp so the
                    // fold can reach 120° (same stop as Fall/Reach lift).
                    fc.catchKnee = 0f;
                }
                // scrapeLiftAirCatchFold kept Reach fold in air, but
                // grounded Catch still Lerped fold→landDeg on
                // CatchReachShinDeg (catchFold is air-only). Last/Progress
                // is the Catch-enter freeze (or AirAge resume), not the
                // fold override, so the first toe-down Catch frame is
                // 140°→~56° against leftover shin — the shin pair of
                // catchGroundArm. Keep fold (skip the landDeg Lerp) on
                // dirt too until heel plant. Heel plant skip Last is
                // catchGroundCatchFold (plant-clock fold→landDeg). Grounded
                // never-scrape Catch fold is
                // catchAirGroundFold (shin pair of catchAirGroundArm).
                // Flag 0 is the former plant-extend on grounded Catch.
                bool catchGroundFold =
                    driver.fallSwingCatchScrapeLiftAirCatchGroundFold > 0.5f
                    && driver.fallSwingCatchScrapeLiftAirCatchGroundArm > 0.5f
                    && driver.fallSwingCatchScrapeLiftAirCatchFold > 0.5f
                    && driver.fallSwingCatchAirLiftHold > 0.5f
                    && driver.fallSwingCatchScrapeLiftAirHold > 0.5f
                    && driver.SwingFootIsGrounded
                    && !driver.SwingFootIsPlanted
                    && (driver.CatchScrapedThisPhase
                        || (driver.CatchResumedFromReachScrapeAir
                            && driver.fallSwingCatchScrapeLiftAirCatchHold > 0.5f));
                if (catchGroundFold)
                {
                    float extra = driver.ScrapeResumeAirActive
                        ? driver.ScrapeResumeAirAge()
                        : 0f;
                    fc.reachKnee = Mathf.Max(0f, driver.reachKneeFlex)
                        * FallCatchFoldLeadClock(
                            driver, FallCatchFallReachClock(driver, extra));
                    // Skip catchKnee's 0.15× straighten / 115 clamp so the
                    // fold can reach 120° (same stop as Fall/Reach lift).
                    fc.catchKnee = 0f;
                }
                // scrapeLiftAirCatchAirArm kept reachArm in never-scrape
                // air, but grounded never-scrape Catch still Lerped
                // fold→landDeg on CatchReachShinDeg (catchFold /
                // catchGroundFold are scrape-only). Last/Progress is the
                // Reach freeze at Catch enter, not a scrape, so the first
                // toe-down after a fly is 140°→~56° against leftover shin —
                // the shin pair of catchAirGroundArm. Keep fold (skip the
                // landDeg Lerp) on dirt too until heel plant. extraAge 0:
                // air Catch is not scrape lift-off. Heel-only Catch
                // (CatchPlantAge tracks PhaseAge) does not skip — planted
                // Catch after never-scrape Catch→Reach is
                // catchAirEnterCatchFold (skip Last, plant-clock arrive).
                // Heel plant skip Last is catchAirGroundCatchFold
                // (plant-clock fold→landDeg). Flag 0 is the former
                // plant-extend on dirt after never-scrape air. scrape
                // dirt is catchGroundFold.
                bool catchAirGroundFold =
                    driver.fallSwingCatchScrapeLiftAirCatchAirGroundFold > 0.5f
                    && driver.fallSwingCatchScrapeLiftAirCatchAirGroundArm > 0.5f
                    && driver.fallSwingCatchScrapeLiftAirCatchAirEndArm > 0.5f
                    && driver.fallSwingCatchScrapeLiftAirCatchAirArm > 0.5f
                    && driver.fallSwingCatchScrapeLiftAirCatchArm > 0.5f
                    && driver.SwingFootIsGrounded
                    && !driver.SwingFootIsPlanted
                    && !driver.CatchScrapedThisPhase
                    && !driver.CatchResumedFromReachScrapeAir
                    && driver.CatchPlantAge() + 0.0001f < driver.PhaseAge();
                if (catchAirGroundFold)
                {
                    fc.reachKnee = Mathf.Max(0f, driver.reachKneeFlex)
                        * FallCatchFoldLeadClock(
                            driver, FallCatchFallReachClock(driver));
                    // Skip catchKnee's 0.15× straighten / 115 clamp so the
                    // fold can reach 120° (same stop as Fall/Reach lift).
                    fc.catchKnee = 0f;
                }
                fc.catchAnkleOnSwing = true;
                // Heel-first plant: keep the air pose until heelLoaded, not
                // any contact. A toe scrape used to straighten and take the
                // CoM loop under the hip.
                fc.airAnkle = Mathf.Clamp01(driver.fallSwingAirAnkle);
                fc.airHeel = Mathf.Max(0f, driver.fallSwingAirHeel);
                // fallSwingCatchAirLiftHold: timeout Catch zeroed hipLift /
                // ankleLift / thighHold. A toe scrape then (grounded, heel
                // still up) handed the swing hip to the pelvis loop —
                // extensor against the fold, the 0.96 grounded foot, the
                // hip pair of catchAirVaultHold. Hold the frozen Fall+Reach
                // clock until heelLoaded; do not add LastCatch or Catch
                // PhaseAge (air is not lift progress, same as fold extraAge
                // 0). Planted Catch stays silent so the landing hip takes
                // the trunk. Catch→Reach already resumes that clock
                // (liftAge) in free air; a scrape resume freezes extraAge
                // (scrapeLiftHold) so the bounce does not finish 70°→140°
                // on dirt. Flag 0 is the former Catch lift at 0.
                // fallSwingCatchScrapeLiftAirHold: those fc terms still lost
                // the hip on Catch scrape then air — forceSwingFlex took
                // −swingHipFlex the first airborne frame (sci_a). Keep
                // ThighHold through that air. Flag 0 is the former jump.
                // fallSwingCatchScrapeLiftAirEnterHold: Catch→Reach still
                // cleared CatchScrapedThisPhase, so forceSwingFlex took
                // −swingHipFlex on the bounce. Keep ThighHold through that
                // resumed Reach. Flag 0 is the former Reach jump.
                // fallSwingCatchAirEnterHold: never-scraped Catch→Reach
                // still took hipair (reachAirHold excludes
                // ReachResumedFromCatch). Keep ThighHold through that
                // bounce. Flag 0 is the former Reach jump.
                // fallSwingPlantAirHold: Plant 32° fold still took hipair
                // on Plant's own first air (Fall's hold never ran).
                // Keep ThighHold through that millimetre. Flag 0 is the
                // former Plant jump.
                // fallSwingCatchScrapeLiftAirCatchHold: that keep is
                // Catch→Reach only. Reach→Catch still cleared
                // ReachResumedFromCatchScrapeAir, so forceSwingFlex took
                // −swingHipFlex on the return. Keep ThighHold through that
                // resumed Catch. Flag 0 is the former Catch jump.
                // fallSwingCatchScrapeLiftAirCatchFold: that keep still
                // commanded CatchReachShinDeg (70°→35°) on the airborne
                // toe. Keep the Reach fold until heel plant. Flag 0 is
                // the former plant-extend. scrapeLiftAirCatchArm is the
                // Catch-phase shoulder pair of that keep (Last-carry).
                // scrapeLiftAirCatchEndArm is the Recover/Plant CatchEnd
                // pair of that keep (stale Last after Catch flags clear).
                // scrapeLiftAirEnterFold is the Catch→Reach shin pair of
                // that keep (stale Last). scrapeLiftAirEnterGroundFold is
                // the dirt sibling of that shin keep (enterFold is air-only).
                // scrapeLiftAirCatchGroundFold is the dirt sibling of
                // catchFold (catchFold is air-only; shin pair of
                // catchGroundArm). scrapeLiftAirCatchAirGroundFold is the
                // dirt sibling of never-scrape Catch (catchFold /
                // catchGroundFold are scrape-only; shin pair of
                // catchAirGroundArm). scrapeLiftAirCatchAirFold is the
                // air sibling of never-scrape Catch (shin pair of
                // catchAirArm). scrapeLiftAirCatchAirEnterFold is the
                // Catch→Reach sibling of that shin keep (enterFold is
                // scrape-only). scrapeLiftAirCatchAirEnterArm is the
                // Catch→Reach shoulder pair of that shin keep (enterArm
                // is scrape-only). scrapeLiftAirCatchAirEnterGroundArm is
                // the dirt sibling of that keep. scrapeLiftAirCatchAirEnterGroundFold
                // is the shin pair of that dirt keep. scrapeLiftAirCatchAirEnterEndArm
                // is the Recover/Plant CatchEnd pair after that bounce's
                // planted Catch (RecoveredFromCatchAir needs heel-down
                // during Catch). scrapeLiftAirCatchAirEnterCatchArm is the
                // Catch-phase pair of that CatchEnd skip (catchAirGroundArm
                // needs CatchPlantAge < PhaseAge). scrapeLiftAirCatchAirEnterCatchFold
                // is the Catch-phase shin pair of that skip (catchAirGroundFold
                // needs !planted; skip Last, plant-clock fold→landDeg).
                // scrapeLiftAirCatchAirEnterEndFold is the Recover/Plant
                // shin pair of that skip (CatchReachExtendProgress still
                // Max Last; skip Last, continue plant-clock fold→landDeg).
                // scrapeLiftAirCatchEndFold is the Recover/Plant shin pair
                // of catchEndArm after scrape then air (CatchReachExtendProgress
                // still Max Last; skip Last, continue plant-clock).
                // scrapeLiftAirCatchGroundCatchFold is the Catch-phase
                // planted scrape shin pair of catchGroundFold (needs
                // !planted; skip Last, plant-clock fold→landDeg).
                // scrapeLiftAirCatchGroundCatchEndFold is the Recover/Plant
                // shin pair of that skip after never-air scrape
                // (CatchReachExtendProgress still Max Last; skip Last,
                // continue plant-clock). FallCatchArmCatchEnd skip Last
                // on that same Recover is catchEndArm
                // (RecoveredFromCatchGroundCatch).
                // scrapeLiftAirCatchAirEndFold is the Recover/Plant shin
                // pair of catchAirEndArm after never-scrape air
                // (CatchReachExtendProgress still Max Last; skip Last,
                // continue plant-clock). scrapeLiftAirCatchAirGroundCatchFold
                // is the Catch-phase planted never-scrape shin pair of
                // catchAirGroundFold (needs !planted; skip Last,
                // plant-clock fold→landDeg).
                if (driver.fallSwingCatchAirLiftHold > 0.5f
                    && !driver.SwingFootIsPlanted)
                {
                    float liftClock = FallCatchFallReachClock(driver);
                    // Fall / Reach already 2× hipLift/ankleLift
                    // (fallSwingFallLiftLead). Catch air must continue
                    // that clock or the first Catch frame snaps hipLift
                    // down. Scrape (still on dirt) may double the same
                    // way (fallSwingCatchScrapeLiftLead).
                    // hipLiftCapDeg for this air is 16°
                    // (fallSwingCatchAirHipLiftCap). Catch scrape may
                    // use the same 16° (fallSwingCatchScrapeHipLiftCap);
                    // hipLift / ankleLift on scrape may write 0.5
                    // (fallSwingCatchScrapeHipLift /
                    // fallSwingCatchScrapeAnkleLift). That 16°
                    // Catch air still had
                    // liftSignal 0 at the cap (P to the target);
                    // fallSwingCatchAirHipLiftCapHold floors Catch air
                    // at 0.5 in ApplyFallCatchSwingThighHold. Catch scrape
                    // may floor the same way (fallSwingCatchScrapeHipLiftCapHold).
                    // That floor still writes
                    // hipLift 0.25 (liftSignal −0.125 at the cap vs
                    // Fall/Reach −0.25); fallSwingCatchAirHipLift
                    // writes 0.5 in Catch air (scrape may write 0.5). That
                    // 16°/0.5 Catch air still wrote ankleLift 0.25
                    // (leftover plantarflex on µ=0.4); fallSwingCatchAirAnkleLift
                    // writes 0.5 in Catch air (scrape may write 0.5). That
                    // 16°/0.5 floor on scrape still wrote hipLift 0.25
                    // (liftSignal −0.125 at the cap vs Fall/Reach/Catch-air
                    // −0.25); fallSwingCatchScrapeHipLift writes 0.5 in
                    // Catch scrape. That 16°/0.5 hipLift still wrote
                    // ankleLift 0.25; fallSwingCatchScrapeAnkleLift
                    // writes 0.5 in Catch scrape. That 16° scrape still
                    // met an 8° Catch→Reach cap; fallSwingCatchReachScrapeHipLiftCap
                    // keeps 16°. That bounce still wrote hipLift 0.25;
                    // fallSwingCatchReachScrapeHipLift writes 0.5. That
                    // 16°/0.5 hipLift still wrote ankleLift 0.25;
                    // fallSwingCatchReachScrapeAnkleLift writes 0.5.
                    // That 16° cap still had room=0 at a 16° Catch-scrape
                    // thigh; fallSwingCatchReachScrapeHipLiftCapWide
                    // keeps 24°. That same room=0 still pinned Catch scrape
                    // at 16°; fallSwingCatchScrapeHipLiftCapWide keeps 24°.
                    // That 24° scrape still had room=0 once Catch-air
                    // walked into 32°; fallSwingCatchScrapeHipLiftCapCeil
                    // keeps 32°. That 24° Catch→Reach scrape still had
                    // room=0 once Catch scrape walked into 32°;
                    // fallSwingCatchReachScrapeHipLiftCapCeil keeps 32°.
                    if (!driver.SwingFootIsGrounded)
                        liftClock = FallCatchLiftLeadClock(driver, liftClock);
                    else
                        liftClock = FallCatchScrapeLiftLeadClock(driver, liftClock);
                    fc.thighHold = Mathf.Clamp01(driver.fallSwingThighHold);
                    float catchHipLiftAmt = Mathf.Clamp01(driver.fallSwingHipLift);
                    // 0.25 at the 16°/0.5 floor is only −0.125 at the
                    // cap (Fall/Reach write 0.5 → −0.25). Gravity then
                    // drops the thigh and the foot replants. 0.5 matches
                    // Fall/Reach so the floor actually holds; Catch
                    // scrape kept 0.25. Flag 0 is the former bias.
                    // Zero stay zero so -fallSwingHipLift 0 still
                    // disables. Not sci_a 40°.
                    if (driver.fallSwingCatchAirHipLift > 0.5f
                        && catchHipLiftAmt > 0.001f
                        && !driver.SwingFootIsGrounded)
                        catchHipLiftAmt = 0.5f;
                    // Catch scrape (grounded, heel still up) still wrote
                    // 0.25 at the 16°/0.5 floor (liftSignal −0.125 vs
                    // Fall/Reach/Catch-air −0.25). 0.5 matches those so
                    // the floor actually holds; ankleLift may then write
                    // 0.5 (fallSwingCatchScrapeAnkleLift).
                    // Flag 0 is the former bias. Zero stay zero so
                    // -fallSwingHipLift 0 still disables. Not sci_a 40°.
                    if (driver.fallSwingCatchScrapeHipLift > 0.5f
                        && catchHipLiftAmt > 0.001f
                        && driver.SwingFootIsGrounded)
                        catchHipLiftAmt = 0.5f;
                    fc.hipLift = catchHipLiftAmt * liftClock;
                    float catchAnkleLiftAmt = Mathf.Clamp01(driver.fallSwingAnkleLift);
                    // 0.25 fade still leaves 0.5 of leftover plantarflex
                    // after FallAnklePush; µ=0.4 slides the 16°/0.5
                    // hipLift. 0.5 cancels that push to 0 — same as
                    // Fall/Reach, not a 1.0 dorsiflexor. Catch scrape
                    // may write the same 0.5
                    // (fallSwingCatchScrapeAnkleLift). Flag 0 is the
                    // former fade. Zero stay zero so -fallSwingAnkleLift
                    // 0 still disables.
                    if (driver.fallSwingCatchAirAnkleLift > 0.5f
                        && catchAnkleLiftAmt > 0.001f
                        && !driver.SwingFootIsGrounded)
                        catchAnkleLiftAmt = 0.5f;
                    // Catch scrape (grounded, heel still up) still wrote
                    // 0.25: leftover plantarflex * (1−0.25) − 0.25 is 0.5
                    // of a full push on µ=0.4, so the 16°/0.5 hipLift
                    // slides. 0.5 cancels that push to 0 — same as
                    // Fall/Reach/Catch-air, not a 1.0 dorsiflexor.
                    // Flag 0 is the former fade. Zero stay zero so
                    // -fallSwingAnkleLift 0 still disables. Not sci_a 40°.
                    if (driver.fallSwingCatchScrapeAnkleLift > 0.5f
                        && catchAnkleLiftAmt > 0.001f
                        && driver.SwingFootIsGrounded)
                        catchAnkleLiftAmt = 0.5f;
                    fc.ankleLift = catchAnkleLiftAmt * liftClock;
                }
                // Trailing (old) stance was vaulted to 0 in Fall/Reach.
                // Dropping stanceVault here snapped that knee 0°→~16° on
                // the first Catch frame — pelvis onto the new plant.
                // Yield 1→0 on catchMin; Recover continues the clock.
                // fallSwingCatchAirVaultHold: timeout Catch is still air,
                // so PhaseAge yield collapsed the only strut, then
                // Catch→Reach restored vault to 1. Plant age holds the
                // vault until the heel loads; flag 0 is the old air yield.
                // fallSwingCatchAirVaultRemain: that hold at clock 0
                // forced vault=1, so an early Reach gate yanked leftover→0
                // in the air. Keep the Fall+Reach remainder; flag 0 is
                // the former hold-at-1. Catch→Reach used to add LastCatch
                // and saturate the 0.15 s vault ramp (leftover→0 on the
                // only strut). fallSwingCatchReachVaultAge continues that
                // remainder on the new Reach PhaseAge; flag 0 is that
                // LastReach+LastCatch saturate.
                // Cycle-0 placement Catch: the swing just cleared (geo
                // 8 cm past the 0.06 m probe). Commanding reachKneeFlex
                // 140° here last-writes AFTER that lift — airborne Catch
                // skips the geo assign (both soles must be down), the
                // swing tucks under the hip, the stance vault yields, and
                // sinkAbort Planted at ~4 s with walkSwap=0. Keep the
                // Fall fold / geo knee; hold the stance strut; ThighHold
                // so DrivePelvis / −swingHipFlex do not fight the 4 cm
                // placement. Later cycles still yield and landDeg.
                if (firstStepPlace)
                {
                    fc.catchKnee = 0f;
                    fc.plantKnee = 0f;
                    fc.ignoreFalling = true;
                    fc.thighHold = Mathf.Max(
                        fc.thighHold, Mathf.Clamp01(driver.fallSwingThighHold));
                    if (driver.fallStanceVault > 0.001f)
                        fc.stanceVault = Mathf.Clamp01(driver.fallStanceVault);
                    // Pose (oneg dirt vs 8°/6° air plant) is ApplyFirstStepCycle0Pose.
                }
                else
                {
                    ApplyCatchVaultYield(
                        ref fc, driver,
                        CatchVaultYieldClock(driver, StepPhaseDriver.FallCatchPhase.Catch));
                }
                break;
            case StepPhaseDriver.FallCatchPhase.Recover:
                // Cycle-0 never-air dirt: hold 8°/6° / hipLift 0 / vault
                // so NearContact survives. After air that snap retracts
                // the capture plant behind CoM. ApplyRecoverBrake +
                // catchKnee used to lift that sole (comment 18) —
                // catchKnee stays 0. The landing ankle must still arrest.
                if (driver.FallCatchCycleCount == 0 && firstStepCatchPlantLatched
                    && !driver.SwingWasAirborne)
                {
                    fc.ignoreFalling = true;
                    fc.forceSwingFlex = true;
                    fc.catchKnee = 0f;
                    fc.plantKnee = 0f;
                    fc.thighHold = Mathf.Max(
                        fc.thighHold, Mathf.Clamp01(driver.fallSwingThighHold));
                    if (driver.fallStanceVault > 0.001f)
                        fc.stanceVault = Mathf.Clamp01(driver.fallStanceVault);
                    fc.reachKnee = FirstStepPlantKneeDeg;
                    fc.hipLift = 0f;
                    fc.ankleLift = 0f;
                    fc.catchAnkleOnSwing = true;
                    ApplyRecoverBrake(
                        ref fc, driver, lean, fallComLead, blend, blendBack);
                    fc.catchKnee = 0f;
                    fc.plantKnee = 0f;
                }
                else if (driver.FallCatchCycleCount == 0 && driver.SwingWasAirborne)
                {
                    // Capture plant: ankle brake on the catch foot, no
                    // catchKnee fold and no 8° hip. reachKnee / hipLift
                    // stay what Catch wrote so the sole remains ahead of
                    // CoM until EnterFallCatchPlant.
                    fc.ignoreFalling = true;
                    fc.catchKnee = 0f;
                    fc.plantKnee = 0f;
                    fc.catchAnkleOnSwing = true;
                    ApplyRecoverBrake(
                        ref fc, driver, lean, fallComLead, blend, blendBack);
                    fc.catchKnee = 0f;
                    fc.plantKnee = 0f;
                }
                else
                {
                    fc.catchAnkleOnSwing = true;
                    ApplyRecoverBrake(ref fc, driver, lean, fallComLead, blend, blendBack);
                    fc.catchKnee = driver.catchKneeFlex;
                    if (driver.fallSwingCatchLandKnee > 0.5f)
                        fc.plantKnee = Mathf.Max(0f, driver.reachKneeLandDeg);
                    if (driver.fallSwingCatchExtendClock > 0.5f)
                    {
                        float shin = CatchReachShinDeg(
                            driver, StepPhaseDriver.FallCatchPhase.Recover);
                        fc.reachKnee = shin;
                        if (driver.fallSwingCatchLandKnee > 0.5f)
                            fc.plantKnee = shin;
                    }
                    ApplyCatchVaultYield(
                        ref fc, driver,
                        CatchVaultYieldClock(driver, StepPhaseDriver.FallCatchPhase.Recover));
                }
                break;
        }
        ApplyFirstStepCycle0Pose(
            driver,
            (StepPhaseDriver.FallCatchPhase)phaseCode,
            ref fc,
            ref firstStepCatchPlantLatched);
        return fc;
    }

        // Cycle 0: 140° Fall pin / hipLift on a still-loaded trail is
        // the squat (hip height follows the short chain). Grounded
        // behind the stance foot (StanceLead < 0): reachKnee 0 so
        // DrivePelvis stays (vault / ankleLift). Once CoM is ON that
        // foot, knee-only FirstStepLiftKneeDeg — not hipLift, not
        // DualLoad, not +10° back. Last-write 0 until air was why a
        // 23.5° lean never lifted (walkSwap 0). Air plant latch is the
        // 8°/6° pose once the swing is ahead of stance, not the peel
        // frame. Restore / Plant are not this step.
        private static void ApplyFirstStepCycle0Pose(
            StepPhaseDriver driver,
            StepPhaseDriver.FallCatchPhase phase,
            ref FallCatchBalance fc,
            ref bool firstStepCatchPlantLatched)
        {
            if (driver == null || driver.FallCatchCycleCount != 0)
                return;
            if (phase != StepPhaseDriver.FallCatchPhase.Fall
                && phase != StepPhaseDriver.FallCatchPhase.Reach
                && phase != StepPhaseDriver.FallCatchPhase.Catch
                && phase != StepPhaseDriver.FallCatchPhase.Recover)
                return;
            if (driver.StandingRestore || driver.StandingSurrender)
                return;

            // Latch the 8°/6° plant only on stacked dirt that never flew
            // and is already ahead of stance. After air that snap retracts
            // a capture plant (hip 8° ≈ 6 cm from the hip) behind a CoM
            // that initiation already walked 0.2–0.3 m past the old stance.
            // Recover then swapped on AheadOfStance and the new support
            // started in Falling. Keep the capture hip until the sole is
            // AheadOfCom; 8°/6° stays the never-air dirt pose.
            if (driver.SwingFootIsGrounded
                && driver.SwingFootAheadOfStance
                && !driver.SwingWasAirborne)
                firstStepCatchPlantLatched = true;

            if (firstStepCatchPlantLatched && !driver.SwingWasAirborne)
            {
                fc.catchKnee = 0f;
                fc.plantKnee = 0f;
                fc.hipLift = 0f;
                fc.ankleLift = 0f;
                fc.reachKnee = FirstStepPlantKneeDeg;
                fc.thighHold = Mathf.Max(
                    fc.thighHold, Mathf.Clamp01(driver.fallSwingThighHold));
                if (driver.fallStanceVault > 0.001f)
                    fc.stanceVault = Mathf.Clamp01(driver.fallStanceVault);
                fc.ignoreFalling = true;
                fc.forceSwingFlex = true;
                return;
            }

            // After air: LiftKneeDeg is the dirt peel, not a catch-step.
            // Hip 15–32° with knee 55° puts the ankle BEHIND the hip
            // (shin 55° from the thigh, lower end −X). Reach's fold
            // clock is 140° until ReachExtendProgress (reachExtendStart
            // waits 0.25 of timeout). Pinning 55° kept that tucked
            // shin; leaving 140° tucks the foot under the hip. Extend
            // 55° → landDeg on the fold ramp so the sole can pass CoM.
            // Grounded after air: Clamp to [landDeg, 55] so DrivePelvis
            // cannot zero the shin and a leftover 140° cannot squat.
            float landDeg = Mathf.Max(0f, driver.reachKneeLandDeg);
            if (driver.SwingWasAirborne || !driver.SwingFootIsGrounded)
            {
                if (!driver.SwingFootIsGrounded)
                {
                    // Fall air is still the lift: keep LiftKneeDeg.
                    // Reach ages from reachAirEnterTime so Fall→Reach
                    // does not snap 35° back to 55° on PhaseAge 0.
                    // Catch/Recover air must not restart that ramp
                    // (EnterCatch stamps reachAirEnterTime = now).
                    float clock = 0f;
                    if (phase == StepPhaseDriver.FallCatchPhase.Reach)
                        clock = Mathf.Clamp01(
                            driver.ReachAirAge()
                            / Mathf.Max(0.02f, driver.fallKneeRampSeconds));
                    else if (phase == StepPhaseDriver.FallCatchPhase.Catch
                        || phase == StepPhaseDriver.FallCatchPhase.Recover)
                        clock = 1f;
                    fc.reachKnee = Mathf.Lerp(FirstStepLiftKneeDeg, landDeg, clock);
                    fc.ignoreFalling = true;
                    fc.catchKnee = 0f;
                    fc.plantKnee = 0f;
                    return;
                }
                fc.reachKnee = Mathf.Clamp(
                    fc.reachKnee, landDeg, FirstStepLiftKneeDeg);
                fc.ignoreFalling = true;
                fc.catchKnee = 0f;
                fc.plantKnee = 0f;
                if (driver.SwingFootIsGrounded)
                    fc.catchAnkleOnSwing = true;
                return;
            }

            if (phase == StepPhaseDriver.FallCatchPhase.Recover)
                return;

            if (FirstStepTrailStillLoaded(driver))
            {
                fc.catchKnee = 0f;
                fc.plantKnee = 0f;
                fc.hipLift = 0f;
                fc.thighHold = 0f;
                fc.forceSwingFlex = false;
                // Catch writes catchAnkleOnSwing for the foot that just
                // landed AHEAD. On cycle 0 nothing has landed: that flag
                // moves the shared ankle CoM error onto the TRAILING sole
                // (CoM is past the lead, so the error saturates) and hands
                // it to that grounded ankle. Plantarflex then pins the toe
                // the 55° fold is trying to peel. The lead keeps the CoM
                // loop until the sole has left and the plant is latched.
                fc.catchAnkleOnSwing = false;
                if (driver.fallStanceVault > 0.001f)
                    fc.stanceVault = Mathf.Clamp01(driver.fallStanceVault);
                float ankleLiftAmt = Mathf.Clamp01(driver.fallSwingAnkleLift);
                if (driver.fallSwingFallAnkleLift > 0.5f && ankleLiftAmt > 0.001f)
                    ankleLiftAmt = 0.5f;
                fc.ankleLift = Mathf.Max(fc.ankleLift, ankleLiftAmt);
                fc.ignoreFalling = true;
                // Knee-only after the standing band already opened Fall
                // (BeginCycle RecoverStandReady / plantAged). StanceLead
                // ≥ 0 never arrives on stacked soles, so this used to
                // write reachKnee 0 the whole Fall (knees 3–7°, walkSwap
                // 0). Hip lift / DualLoad stay off on this planted trailer.
                if (phase == StepPhaseDriver.FallCatchPhase.Fall)
                {
                    float clock = Mathf.Clamp01(
                        driver.PhaseAge()
                        / Mathf.Max(0.02f, driver.fallKneeRampSeconds));
                    fc.reachKnee = FirstStepLiftKneeDeg * clock;
                }
                else
                    fc.reachKnee = FirstStepLiftKneeDeg;
                return;
            }

            // Comment 48: hipLift / DualLoad / +10° back on a planted
            // trailer is the closed-chain flexor (GRF −X, walkSwap 1,
            // comTravelX −1.4 m). Knee-only is the branch above. Keep
            // hipLift / thighHold off on leftover dirt so Fall cannot
            // seed that squat if the helper's StanceLead gate is closed.
            fc.ignoreFalling = true;
            if (driver.fallStanceVault > 0.001f)
                fc.stanceVault = Mathf.Clamp01(driver.fallStanceVault);
            if (driver.SwingFootIsGrounded)
            {
                fc.reachKnee = 0f;
                fc.hipLift = 0f;
                fc.thighHold = 0f;
            }
        }

        // Cycle-0 dirt: the trailing sole still shares µ=1.2. StanceLead
        // ≥ 0 does not unload it. Capture ≥ trigger is forward velocity
        // while the foot is still down — that used to open DualLoad /
        // hipLift / +10° back on a planted flexor (GRF −X). Air is the
        // hip unload. Knee-only in Fall is the lift, not that triad.
        // FallLeadReady on cycle 0 is the standing band at BeginCycle,
        // not StanceLead ≥ 0.
        private static bool FirstStepTrailStillLoaded(StepPhaseDriver driver)
        {
            if (driver == null || driver.FallCatchCycleCount != 0)
                return false;
            // After air the grounded swing is the capture plant, not a
            // stacked trailer. Treating it as loaded zeroed the geo hip.
            return driver.SwingFootIsGrounded && !driver.SwingWasAirborne;
        }

        // Cycle-0 catch-step. Catch/Recover already require the swing
        // AHEAD of stance/CoM. FirstStepTrailStillLoaded is contact, so
        // that judge stays false while DrivePelvis holds the trailer
        // under the hip and desiredStep is DualLoadMinSpan (behind a
        // CoM that has rolled toward the toe). Open the existing
        // capture-point hip once CoM is ON the lead and at least a
        // quarter of the way to the toe — trail N≈0, inverted pendulum
        // about the stance ankle, hipLift / DualLoad / +10° still off.
        // Air is the same open chain: DualLoadMinSpan / firstStepHoldLift
        // were dirt guards. Once the trail has left, command capture +
        // catchFootLead so the foot is placed in front of CoM.
        private bool FirstStepToeCatchReady()
        {
            if (stepPhaseDriver == null || stepPhaseDriver.FallCatchCycleCount != 0)
                return false;
            if (firstStepCatchPlantLatched
                || stepPhaseDriver.StandingRestore
                || stepPhaseDriver.StandingSurrender)
                return false;
            int phase = stepPhaseDriver.FallCatchPhaseCode;
            if (phase != (int)StepPhaseDriver.FallCatchPhase.Fall
                && phase != (int)StepPhaseDriver.FallCatchPhase.Reach
                && phase != (int)StepPhaseDriver.FallCatchPhase.Catch
                && phase != (int)StepPhaseDriver.FallCatchPhase.Recover)
                return false;
            if (!stepPhaseDriver.SwingFootIsGrounded)
                return true;
            if (stepPhaseDriver.StanceLead < 0f)
                return false;
            float toe = Mathf.Max(0.05f, StanceToeReach());
            return stepPhaseDriver.StanceLead >= toe * 0.25f;
        }

    private bool WalkKeyHeld()
    {
        return stepPhaseDriver != null && stepPhaseDriver.walkActive;
    }

    private bool WalkRecoverAnyPoseOn()
    {
        return stepPhaseDriver != null && stepPhaseDriver.walkRecoverAnyPose > 0.5f;
    }

    // Heap on the slab, not a 12–30° Plant restore and not a live catch.
    // Quiet stance: |tilt| ~1°, pelvis still at spawn. A forward sprawl
    // is torsoTilt −70° with CoM a metre ahead — world-upright hips then
    // sit on the +45° stop (GETUP-1 freeze).
    private bool IsFallenHeap(float comOffset)
    {
        // p_auto f4cabb44 t=5.965: DualSupport peel, both soles still
        // down, |tilt| crossed 45° before Swing froze. LiveFirstCatch
        // used to require Swing + one foot, so EnterGettingUp ran,
        // ResetCaptureWalk, walkPhase −1, GETUP tuck 34 s / sag 0.75 m.
        // First-step DualSupport at 45° is the inverted-pendulum
        // orbit, not GETUP-1. TickCaptureWalk returns that peel to
        // standing Plant (TiltSettled + CoM-to-stance). Face-plant
        // is tilt AND pelvis on the slab.
        if (CaptureWalkLiveFirstCatch(comOffset))
            return false;

        // LiveFirstCatch is already false on a prone trunk. Same 1°
        // slack as IsProneFallen so a −69.5° plank is a heap.
        if (IsProneFallen())
            return true;
        float tilt = vestibularSystem != null ? vestibularSystem.GetBodyTilt() : 0f;
        float absTilt = Mathf.Abs(tilt);
        bool tiltHeap = absTilt >= GetUpSprawlDeg();
        bool pelvisDown = stepPhaseDriver != null && stepPhaseDriver.PelvisDroppedFromStand;
        bool sink = stepPhaseDriver != null && stepPhaseDriver.BodyCollapsed;
        if (tiltHeap && pelvisDown)
            return true;
        return sink && pelvisDown;
    }

    // Live capture walk is an inverted-pendulum orbit, not GETUP-1.
    // p_auto 17:32: first swap at 5.645 (tilt +0.81°), then
    // CaptureWalkStepCount==1 dropped this shield, |tilt| crossed
    // getUpFallenAbsTilt 45 at 6.220, EnterGettingUp at 6.225,
    // ResetCaptureWalk, walkPhase −1, GETUP 33.8 s. Cause is the
    // second Swing's plantMiss veto then Recovery chair — gait now
    // names that sole a plant. This shield stays until CoM has sunk
    // so a 45° orbit cannot freeze GETUP while the step is still live.
    private bool CaptureWalkLiveFirstCatch(float comOffset)
    {
        if (useCaptureWalker == 0)
            return false;
        if (stepPhaseDriver == null || !stepPhaseDriver.walkActive)
            return false;
        if (CaptureWalkStateCode == (int)CaptureWalkState.RecoveryStep)
            return false;
        _ = comOffset;
        // A DualSupport orbit under GetUpProneDeg is still a step.
        // The −70° hip-stop plank (GETUP-1 freeze, state Walking /
        // attempts 0) is not: BodyCollapsed stayed false on that pose, so
        // this used to return true and UpdateLocomotionState never left
        // Walking. Drop the shield on a deep trunk or a sprawl with
        // pelvis on the slab. PD / signs stay.
        if (IsProneFallen())
            return false;
        float tilt = vestibularSystem != null ? vestibularSystem.GetBodyTilt() : 0f;
        float absTilt = Mathf.Abs(tilt);
        // Sprawl+15 (~60°) with no BodyCollapsed was still "live catch"
        // and left locomotion Walking for 32 s (measured −70.4° / state 1).
        if (absTilt >= GetUpSprawlDeg() + 15f)
            return false;
        if (absTilt >= GetUpSprawlDeg() && stepPhaseDriver.PelvisDroppedFromStand)
            return false;
        return !stepPhaseDriver.BodyCollapsed;
    }

    // GETUP-1 sprawl: the +45° hip stop holds torsoTilt ≈ −70°. One
    // degree of slack so a −69.5° limit-cycle still counts as fallen.
    private bool IsProneFallen()
    {
        float tilt = vestibularSystem != null ? vestibularSystem.GetBodyTilt() : 0f;
        return Mathf.Abs(tilt) >= GetUpProneDeg() - 1f;
    }

    private void UpdateLocomotionState(float comOffset)
    {
        bool heap = IsFallenHeap(comOffset);
        if (heap && getUpFallTime < 0f)
            getUpFallTime = Time.fixedTime;

        bool walkKey = WalkKeyHeld();
        // GETUP-1: walk key on a prone / deep heap is GettingUp, even when
        // walkRecoverAnyPose is 1. The StandingRestore shortcut left
        // locomotion Walking on a −70.4° plank with attempts 0. Mild
        // DualSupport squat (~45°, not prone) may still restore.
        if (locomotionState == LocomotionState.GettingUp)
        {
            if (!walkKey)
            {
                ApplyGetUpFrictionPeel(false);
                locomotionState = LocomotionState.Fallen;
                getUpSitLatched = false;
                getUpTuckLatched = false;
                getUpPikeLatched = false;
                getUpSitAge = 0f;
                getUpTuckAge = 0f;
                getUpPose = 1; // lying / fallen again
                return;
            }
            return;
        }

        if (heap && walkKey)
        {
            float tilt = vestibularSystem != null ? vestibularSystem.GetBodyTilt() : 0f;
            float absTilt = Mathf.Abs(tilt);
            // Deep heap → GettingUp. Mild ~45° DualSupport squat with
            // walkRecoverAnyPose still uses StandingRestore (not 34 s of
            // false GETUP). Sprawl+15 (~60°) covers the −70° plank that
            // left locomotion Walking / attempts 0. A pelvis that has
            // already sunk past the collapse drop at ~−55° (tilt below
            // sprawl+15) also froze under StandingRestore for the rest of
            // the run with getUpAttempts 0 — it must enter GettingUp too.
            bool collapsed = stepPhaseDriver != null && stepPhaseDriver.BodyCollapsed;
            bool deepHeap = IsProneFallen()
                || absTilt >= GetUpSprawlDeg() + 15f
                || collapsed;
            if (deepHeap || !WalkRecoverAnyPoseOn())
            {
                if (locomotionState != LocomotionState.GettingUp)
                    EnterGettingUp();
                return;
            }
            locomotionState = LocomotionState.Walking;
            stepPhaseDriver.HoldGetUpPlant();
            if (currentState == BalanceState.Falling)
                currentState = BalanceState.Recovery;
            return;
        }
        else if (heap)
        {
            locomotionState = LocomotionState.Fallen;
            if (getUpPose < 1)
                getUpPose = 1;
            if (getUpPoseMax < 1)
                getUpPoseMax = 1;
            return;
        }

        getUpRiseBlend = 0f;
        getUpAge = 0f;
        getUpContactAge = 0f;
        getUpSitLatched = false;
        getUpTuckLatched = false;
        getUpPikeLatched = false;
        getUpSitAge = 0f;
        getUpTuckAge = 0f;
        if (walkKey)
            locomotionState = LocomotionState.Walking;
        else if (currentState == BalanceState.Falling)
            locomotionState = LocomotionState.Falling;
        else
            locomotionState = LocomotionState.Standing;
    }

    private void EnterGettingUp()
    {
        locomotionState = LocomotionState.GettingUp;
        getUpAttempts++;
        getUpAge = 0f;
        getUpRiseBlend = 0f;
        getUpContactAge = 0f;
        getUpSitLatched = false;
        getUpTuckLatched = false;
        getUpPikeLatched = false;
        getUpSitAge = 0f;
        getUpTuckAge = 0f;
        getUpPose = 1; // lying
        if (getUpPoseMax < 1)
            getUpPoseMax = 1;
        currentState = BalanceState.Recovery;
        ResetCaptureWalk();
        // ResetCaptureWalk re-welds hip/knee K=12; peel so TickGetUp can
        // actually rotate the sit / tuck (burn_walk freeze with attempts=1).
        ApplyGetUpFrictionPeel(true);
        // Walk-orbit / Max peak on StandingComY kept GetUpComAtStandHeight
        // false for the whole 33 s TickGetUp (burn_walk getUpSuccess
        // false). Clear both CoM bars; pelvis spawn Y stays for the
        // risen check. Capture off until NotifyStoodUp → BeginWalk.
        if (stepPhaseDriver != null)
        {
            stepPhaseDriver.HoldGetUpPlant();
            stepPhaseDriver.ClearStandingComBarForGetUp();
        }
        captureStandHipValid = false;
    }

    // Standing Plant after a mid-walk dump (not GETUP-1). BeginWalk's
    // Plant→Fall band is still shut. Extend hipHold / kneeBase; world-
    // upright hips only inside 12°. The prone pike lives in TickGetUp.
    // Same SPD, same signs. Cached joints only. Happy-path 9° onset
    // never enters (restore is false).
    private void TickStandingPlantRestore(float comOffset)
    {
        if (currentState == BalanceState.Falling)
            currentState = BalanceState.Recovery;

        bool leftGrounded = bodyState != null && bodyState.leftFootGrounded;
        bool rightGrounded = bodyState != null && bodyState.rightFootGrounded;
        bool anyFoot = leftGrounded || rightGrounded;
        bool bothFeet = leftGrounded && rightGrounded;
        float torsoTilt = vestibularSystem != null ? vestibularSystem.GetBodyTilt() : 0f;
        float absTilt = Mathf.Abs(torsoTilt);
        float proneDeg = GetUpProneDeg();
        // GETUP-1 owns the prone pike. A 40° walk dump that restore
        // still sees must extend (hipHold / kneeBase), not tuck — that
        // tuck was the 45 cm one-leg squat. Prone here is only a safety
        // net if locomotion has not yet switched to GettingUp.
        bool pike = GetUpShouldPike(anyFoot, absTilt, proneDeg);
        float plantTiltMax = stepPhaseDriver != null
            ? stepPhaseDriver.StandingPlantTiltMaxDeg
            : 12f;
        // World-upright only inside the same 12° band BeginWalk uses.
        // Outside it the +45° hip stop is the GETUP freeze.
        bool hipsMayStand = bothFeet && !pike && absTilt < plantTiltMax;

        float hipTarget;
        float kneeTarget;
        if (!anyFoot)
        {
            hipTarget = hipHoldAngle;
            kneeTarget = kneeBaseAngle;
        }
        else if (pike)
        {
            hipTarget = -Mathf.Max(10f, getUpHipFlex);
            kneeTarget = Mathf.Max(40f, getUpKneeFlex);
        }
        else
        {
            hipTarget = hipHoldAngle;
            kneeTarget = kneeBaseAngle;
        }

        if (hipsMayStand)
        {
            ControlHipsToWorldUpright(hipBaseAngle, true, true, 0f, 0f);
        }
        else
        {
            ControlJointToAngleStable(leftHipJoint, hipTarget, hipPGain, hipDGain,
                errorReferenceDegrees, leftHipFlexor, leftHipExtensor, ref leftHipEffectiveInertia);
            ControlJointToAngleStable(rightHipJoint, hipTarget, hipPGain, hipDGain,
                errorReferenceDegrees, rightHipFlexor, rightHipExtensor, ref rightHipEffectiveInertia);
        }
        ControlJointToAngleStable(leftKneeJoint, kneeTarget, kneePGain, kneeDGain,
            errorReferenceDegrees, leftKneeFlexor, leftKneeExtensor, ref leftKneeEffectiveInertia);
        ControlJointToAngleStable(rightKneeJoint, kneeTarget, kneePGain, kneeDGain,
            errorReferenceDegrees, rightKneeFlexor, rightKneeExtensor, ref rightKneeEffectiveInertia);

        float ankleBalance = 0f;
        if (bothFeet && absTilt < proneDeg)
        {
            float velX = bodyState != null ? bodyState.comVelocity.x : 0f;
            float offsetN = comOffset / Mathf.Max(0.01f, comOffsetReference);
            float velN = velX / Mathf.Max(0.05f, comVelocityReference);
            ankleBalance = Mathf.Clamp(
                ankleComP * offsetN + ankleComD * velN, -1f, 1f);
        }
        ControlAnkleForBalance(leftAnkleJoint, ankleBalance, leftAnkleFlexor, leftAnkleExtensor);
        ControlAnkleForBalance(rightAnkleJoint, ankleBalance, rightAnkleFlexor, rightAnkleExtensor);
        DriveToeJoint(true, 0f);
        DriveToeJoint(false, 0f);
        ControlLumbarToWorldUpright(0f);
        ControlNeckAndHeadToWorldUpright();
        ApplyArmPose(
            shoulderBaseAngle - crouchHandSupportShoulder,
            shoulderBaseAngle - crouchHandSupportShoulder,
            crouchHandSupportElbow,
            crouchHandSupportElbow,
            crouchHandSupportWrist,
            crouchHandSupportWrist);
    }

    private void TickGetUp(float comOffset)
    {
        // Headless Tick is after this FixedUpdate. Hold every step so
        // CaptureWalk cannot DualSupport a −20° trunk before success.
        if (stepPhaseDriver != null)
            stepPhaseDriver.HoldGetUpPlant();
        // Keep the weld peeled every step: CaptureWalk / other paths
        // may restore built K between frames.
        ApplyGetUpFrictionPeel(true);

        float dt = Time.fixedDeltaTime;
        getUpAge += dt;

        bool leftGrounded = bodyState != null && bodyState.leftFootGrounded;
        bool rightGrounded = bodyState != null && bodyState.rightFootGrounded;
        bool anyFoot = leftGrounded || rightGrounded;
        bool bothFeet = leftGrounded && rightGrounded;
        if (anyFoot)
            getUpContactAge += dt;
        else
            getUpContactAge = 0f;

        float torsoTilt = vestibularSystem != null ? vestibularSystem.GetBodyTilt() : 0f;
        float absTilt = Mathf.Abs(torsoTilt);
        float proneDeg = GetUpProneDeg();
        float sitDoneDeg = GetUpSitDoneDeg();
        float hipsStandDeg = GetUpHipsStandDeg();
        // Tuck target. The stable PD holds the knee about 1° under the target
        // (p_ds plateau 89.2 at target 90) and the requirement's bar is a real
        // 90° fold, so the target sits above it. The hinge stop is 120.
        float tuckKneeTarget = Mathf.Min(120f,
            Mathf.Max(GetUpTuckKneeMin, getUpKneeFlex) + 6f);

        float kneeL = leftKneeJoint != null ? leftKneeJoint.jointAngle : 0f;
        float kneeR = rightKneeJoint != null ? rightKneeJoint.jointAngle : 0f;
        float kneeMin = Mathf.Min(kneeL, kneeR);

        // Pose order (fixed): lying → sit up → tuck knees ≥70° → push → stand.
        // Knee-first pike skipped the sit (D) and never reached 70°.
        if (!getUpSitLatched)
        {
            getUpPose = 1; // lying / sitting up
            if (absTilt <= sitDoneDeg)
                getUpSitLatched = true;
        }
        if (getUpSitLatched && getUpPose < 2)
            getUpPose = 2; // sitting

        if (getUpSitLatched)
            getUpSitAge += dt;

        // The sit is a pose of the sequence and must be visible (5 fps), so
        // the curl waits GetUpSitHoldSeconds before it may latch.
        bool sitHeld = getUpSitLatched && getUpSitAge >= GetUpSitHoldSeconds;

        // Tuck latch. The corrected mechanics (18.09) bring the knees to the
        // TORSO from the ground, so the curl has to start with the feet still
        // in the air. The old latch demanded a planted sole AND kneeMin >= 70
        // before the tuck target that raises the knee was ever commanded —
        // p_sw1: anyFoot 12 % of the fall window with the feet up, knee frozen
        // at the 20° sit target. A gate that can never pass, and the pose sat
        // at "sitting" for the last 34 s of the run. The sole contact age is
        // kept for the planted path.
        bool mayTuck = sitHeld
            && (!anyFoot || getUpContactAge >= Mathf.Max(0.05f, getUpTuckMinSeconds));
        if (mayTuck && !getUpTuckLatched)
        {
            getUpTuckLatched = true;
            getUpPikeLatched = true;
        }

        if (getUpTuckLatched)
            getUpTuckAge += dt;

        // Pose 3 is the fold, not the command: report "knees tucked" only once
        // the knee has actually reached the tuck bar, so the trial number and
        // the frames tell the same story.
        bool tuckFolded = getUpTuckLatched && kneeMin >= GetUpTuckKneeMin;
        if (tuckFolded && getUpPose < 3)
            getUpPose = 3; // knees tucked
        if (getUpPoseMax < getUpPose)
            getUpPoseMax = getUpPose;

        // Hold the curl until the knee has folded onto a sole that is back
        // down, with getUpTuckMaxSeconds as the budget (the field was written
        // for the Inspector and never read on this path). Only then have the
        // heels come under the centre of mass and the rise start.
        bool tuckDone = getUpTuckLatched
            && ((tuckFolded && anyFoot)
                || getUpTuckAge >= Mathf.Max(0.2f, getUpTuckMaxSeconds));

        bool sittingUp = !getUpSitLatched;
        bool tucking = getUpTuckLatched && !tuckDone;
        bool rising = tuckDone;
        // Sit reached but the curl has not latched yet: hold the sit pose.
        bool holdSit = getUpSitLatched && !getUpTuckLatched;

        float hipTarget;
        float kneeTarget;
        if (sittingUp || holdSit)
        {
            // Sit the trunk first. Hip EXTENSION pushes the pelvis up off a
            // prone fold (pelvis −111° / torso −91°): the ±20° lumbar alone
            // cannot lift the torso, and mild flexion only folded the legs
            // and dropped the pelvis further (GETUP-1 freeze at pose 1).
            // Extension raises the pelvis, the torso follows, then the tuck
            // reverses into deep flexion. Knees stay near stance so the sit
            // is visible before the tuck.
            getUpRiseBlend = 0f;
            hipTarget = Mathf.Max(10f, GetUpSitHipFlex);
            kneeTarget = Mathf.Max(kneeBaseAngle, GetUpSitKneeFlex);
        }
        else if (tucking)
        {
            // Closed chain after the sit: heels toward the pelvis, knees ≥70°.
            getUpRiseBlend = 0f;
            hipTarget = -Mathf.Max(10f, getUpHipFlex);
            kneeTarget = tuckKneeTarget;
        }
        else
        {
            float riseT = Mathf.Max(0.2f, getUpRiseSeconds);
            getUpRiseBlend = Mathf.Clamp01(getUpRiseBlend + dt / riseT);
            hipTarget = hipHoldAngle;
            kneeTarget = kneeBaseAngle;
        }

        // World-upright hips on a −70° trunk sat on the +45° stop
        // (GETUP-1 freeze). Only inside the GETUP-1 stand band after tuck.
        bool hipsMayStand = rising
            && getUpRiseBlend > 0.15f
            && bothFeet
            && absTilt < hipsStandDeg;

        // Local P boost only — not a stable-PD retune.
        float hipP = hipPGain;
        float kneeP = kneePGain;
        if (sittingUp || tucking || holdSit || (rising && absTilt >= hipsStandDeg))
        {
            hipP = Mathf.Max(hipP, 4f);
            kneeP = Mathf.Max(kneeP, 3f);
        }

        if (hipsMayStand)
        {
            ControlHipsToWorldUpright(hipBaseAngle, true, true, 0f, 0f);
        }
        else
        {
            ControlJointToAngleStable(leftHipJoint, hipTarget, hipP, hipDGain,
                errorReferenceDegrees, leftHipFlexor, leftHipExtensor, ref leftHipEffectiveInertia);
            ControlJointToAngleStable(rightHipJoint, hipTarget, hipP, hipDGain,
                errorReferenceDegrees, rightHipFlexor, rightHipExtensor, ref rightHipEffectiveInertia);
        }
        ControlJointToAngleStable(leftKneeJoint, kneeTarget, kneeP, kneeDGain,
            errorReferenceDegrees, leftKneeFlexor, leftKneeExtensor, ref leftKneeEffectiveInertia);
        ControlJointToAngleStable(rightKneeJoint, kneeTarget, kneeP, kneeDGain,
            errorReferenceDegrees, rightKneeFlexor, rightKneeExtensor, ref rightKneeEffectiveInertia);

        // Ankle CoM on a 1 m offset with airborne feet kicks the
        // soles away. Quiet loop only once both plates carry and the
        // trunk is not a plank.
        float ankleBalance = 0f;
        if (bothFeet && absTilt < proneDeg)
        {
            float velX = bodyState != null ? bodyState.comVelocity.x : 0f;
            float offsetN = comOffset / Mathf.Max(0.01f, comOffsetReference);
            float velN = velX / Mathf.Max(0.05f, comVelocityReference);
            ankleBalance = Mathf.Clamp(
                ankleComP * offsetN + ankleComD * velN, -1f, 1f);
        }
        ControlAnkleForBalance(leftAnkleJoint, ankleBalance, leftAnkleFlexor, leftAnkleExtensor);
        ControlAnkleForBalance(rightAnkleJoint, ankleBalance, rightAnkleFlexor, rightAnkleExtensor);
        DriveToeJoint(true, 0f);
        DriveToeJoint(false, 0f);

        // Sit-up needs lumbar toward world up from the first step. Tuck
        // keeps it; only a re-prone plank drops it so the third law does
        // not fight a closed-chain heel tuck.
        if (sittingUp || getUpSitLatched)
        {
            ControlLumbarToWorldUpright(0f);
            ControlNeckAndHeadToWorldUpright();
        }
        ApplyGetUpArmPush(sittingUp || holdSit, tucking, rising, absTilt, sitDoneDeg);

        if (GetUpSucceeded(comOffset, bothFeet))
            FinishGetUp();
    }

    // Push-up from the prone heap. Quiet crouchHandSupport (elbow −10)
    // left shoulder ~−5 / elbow ~+2 for the whole GettingUp window.
    // Local P boost only — not a stable-PD retune. Same joint signs.
    private void ApplyGetUpArmPush(bool sittingUp, bool tucking, bool rising,
                                   float absTilt, float sitDoneDeg)
    {
        float shMag = Mathf.Max(Mathf.Abs(crouchHandSupportShoulder), getUpArmShoulder);
        float elFlex = Mathf.Max(40f, getUpArmElbowFlex);
        float shoulder = Mathf.Clamp(-shMag, -85f, 85f);
        float elbow;
        float wrist = crouchHandSupportWrist;
        if (sittingUp || tucking || absTilt >= sitDoneDeg)
        {
            // Push off the slab: the elbow extends through the sit / tuck
            // so the hand presses the ground and the shoulder reaction lifts
            // the torso. A bent elbow only braces and leaves the torso on a
            // −90° fold (GETUP-1). Blend over getUpAge so the press ramps in
            // instead of slamming the hand down.
            float straight = crouchHandSupportElbow;
            float press = Mathf.Clamp01(getUpAge / Mathf.Max(0.25f, getUpRiseSeconds * 0.5f));
            elbow = Mathf.Lerp(-elFlex, straight, press);
            elbow = Mathf.Clamp(elbow, -135f, -5f);
        }
        else if (rising)
        {
            // Extend while the trunk comes up after the tuck.
            float straight = crouchHandSupportElbow;
            elbow = Mathf.Lerp(-elFlex, straight, Mathf.Clamp01(getUpRiseBlend));
            elbow = Mathf.Clamp(elbow, -135f, -5f);
            shoulder = Mathf.Lerp(shoulder, -Mathf.Abs(crouchHandSupportShoulder),
                Mathf.Clamp01(getUpRiseBlend));
            shoulder = Mathf.Clamp(shoulder, -85f, 85f);
        }
        else
        {
            shoulder = Mathf.Clamp(
                shoulderBaseAngle - crouchHandSupportShoulder, -85f, 85f);
            elbow = crouchHandSupportElbow;
        }

        float shP = Mathf.Max(shoulderPGain, 3f);
        float elP = Mathf.Max(elbowPGain, 2.5f);
        ControlJointToAngleStable(
            leftShoulderJoint, shoulder, shP, shoulderDGain,
            shoulderErrorReferenceDegrees, leftShoulderFlexor, leftShoulderExtensor,
            ref leftShoulderEffectiveInertia);
        ControlJointToAngleStable(
            rightShoulderJoint, shoulder, shP, shoulderDGain,
            shoulderErrorReferenceDegrees, rightShoulderFlexor, rightShoulderExtensor,
            ref rightShoulderEffectiveInertia);
        ControlJointToAngleStable(
            leftElbowJoint, elbow, elP, elbowDGain,
            elbowErrorReferenceDegrees, leftElbowFlexor, leftElbowExtensor,
            ref leftElbowEffectiveInertia);
        ControlJointToAngleStable(
            rightElbowJoint, elbow, elP, elbowDGain,
            elbowErrorReferenceDegrees, rightElbowFlexor, rightElbowExtensor,
            ref rightElbowEffectiveInertia);
        ControlJointToAngleStable(
            leftWristJoint, wrist, wristPGain, wristDGain,
            wristErrorReferenceDegrees, leftWristFlexor, leftWristExtensor,
            ref leftWristEffectiveInertia);
        ControlJointToAngleStable(
            rightWristJoint, wrist, wristPGain, wristDGain,
            wristErrorReferenceDegrees, rightWristFlexor, rightWristExtensor,
            ref rightWristEffectiveInertia);
    }

    private float GetUpSprawlDeg()
    {
        return Mathf.Max(20f, getUpFallenAbsTilt);
    }

    private float GetUpProneDeg()
    {
        return Mathf.Max(70f, GetUpSprawlDeg() + 20f);
    }

    private float GetUpSquatDeg()
    {
        float squat = Mathf.Max(20f, getUpSquatAbsTilt);
        return Mathf.Min(squat, GetUpProneDeg() - 5f);
    }

    // Sit-up done when |tilt| is at or below this. Slightly above the
    // squat band so the sit is a real torso rise before the knee tuck.
    private float GetUpSitDoneDeg()
    {
        float sit = Mathf.Max(GetUpSquatDeg(), getUpSquatAbsTilt + 10f);
        return Mathf.Min(sit, GetUpProneDeg() - 5f);
    }

    private float GetUpHipsStandDeg()
    {
        return Mathf.Max(20f, Mathf.Abs(getUpStandTiltDeg) + 5f);
    }

    private bool GetUpShouldPike(bool anyFoot, float absTilt, float squatDeg)
    {
        // Kept for StandingPlantRestore. Sequenced TickGetUp no longer
        // uses this; sit comes before tuck.
        if (!anyFoot)
            return false;
        return absTilt >= squatDeg;
    }

    // Tick / BeginWalk standing band opened (TiltSettled + CoM-to-stance).
    // GETUP success is that gate after a heap, not the −20° handoff.
    public void NotifyStoodUpFromPlant()
    {
        if (getUpAttempts > 0 && !getUpSuccess)
        {
            getUpSuccess = true;
            if (getUpFallTime >= 0f)
                getUpSeconds = Mathf.Max(0f, Time.fixedTime - getUpFallTime);
        }
        getUpRiseBlend = 1f;
        getUpPose = 4;
        if (getUpPoseMax < 4)
            getUpPoseMax = 4;
        if (locomotionState == LocomotionState.GettingUp)
        {
            ApplyGetUpFrictionPeel(false);
            locomotionState = LocomotionState.Walking;
        }
        if (currentState == BalanceState.Falling)
            currentState = BalanceState.Balancing;
    }

    private bool GetUpComAtStandHeight()
    {
        if (stepPhaseDriver == null)
            return true;
        // After ClearStandingComBarForGetUp there is no CoM bar: risen
        // means pelvis back near the quiet spawn column (defect 8),
        // not a walk-orbit peak the heap can never reach.
        if (!stepPhaseDriver.HasStandingComY)
            return !stepPhaseDriver.PelvisDroppedFromStand;
        if (comCalculator == null)
            return false;
        Vector2 com = comCalculator.GetCenterOfMass();
        return com.y + 0.05f >= stepPhaseDriver.StandingComY;
    }

    private bool GetUpSucceeded(float comOffset, bool bothFeet)
    {
        if (!bothFeet)
            return false;
        // Must have sat and tucked — a rise that skipped either pose is wrong.
        if (!getUpSitLatched || !getUpTuckLatched)
            return false;
        float tilt = vestibularSystem != null ? vestibularSystem.GetBodyTilt() : 0f;
        if (tilt <= getUpStandTiltDeg)
            return false;
        if (tilt >= Mathf.Abs(getUpStandTiltDeg) && getUpStandTiltDeg < 0f)
            return false;
        // GETUP-1: CoM over the feet, not the quiet Recovery 0.15 m
        // band that a walk dump never re-enters (finalComOffset 1.25).
        if (Mathf.Abs(comOffset) > fallCoMOffset)
            return false;
        if (stepPhaseDriver == null)
            return false;
        // Do not gate on BodyCollapsed: a walk-orbit Max bar (or the
        // pre-fall standComHeight) stays ~0.25 m above a rising heap,
        // so SinkCollapsed stayed true for the whole 33 s TickGetUp
        // (burn_walk getUpSuccess false / gettingUpSeconds 33.5).
        // Height is GetUpComAtStandHeight; sink is for walk abort only.
        return GetUpComAtStandHeight();
    }

    private void FinishGetUp()
    {
        getUpSuccess = true;
        if (getUpFallTime >= 0f)
            getUpSeconds = Mathf.Max(0f, Time.fixedTime - getUpFallTime);
        locomotionState = LocomotionState.Walking;
        currentState = BalanceState.Balancing;
        getUpRiseBlend = 1f;
        getUpPose = 4;
        if (getUpPoseMax < 4)
            getUpPoseMax = 4;
        // Quiet stance weld back before BeginWalk Plant→Fall resumes.
        ApplyGetUpFrictionPeel(false);
        // Re-measure standing bars from the pose we just reached so
        // the next DualSupport onset does not keep a pre-fall peak.
        captureStandHipValid = false;
        TryCaptureStandHipReference();
        if (stepPhaseDriver != null)
        {
            // Capture stays off until the BeginWalk standing band
            // (TiltSettled 12° + CoM-to-stance). Opening at ±20° dumped
            // again. When the band is already open, NotifyStoodUp is
            // BeginWalk resume (walker starts next Tick).
            if (stepPhaseDriver.walkRecoverAnyPose > 0.5f)
            {
                if (stepPhaseDriver.StandingPlantHoldReady)
                    stepPhaseDriver.NotifyStoodUp();
                else
                    stepPhaseDriver.HoldGetUpPlant();
            }
            else
                stepPhaseDriver.NotifyStoodUp();
        }
    }

    private void RelaxAllMuscles()
    {
        UpdateMuscle(leftHipFlexor, 0f); UpdateMuscle(leftHipExtensor, 0f);
        UpdateMuscle(rightHipFlexor, 0f); UpdateMuscle(rightHipExtensor, 0f);
        UpdateMuscle(leftKneeFlexor, 0f); UpdateMuscle(leftKneeExtensor, 0f);
        UpdateMuscle(rightKneeFlexor, 0f); UpdateMuscle(rightKneeExtensor, 0f);
        UpdateMuscle(leftAnkleFlexor, 0f); UpdateMuscle(leftAnkleExtensor, 0f);
        UpdateMuscle(rightAnkleFlexor, 0f); UpdateMuscle(rightAnkleExtensor, 0f);
        UpdateMuscle(lumbarFlexor, 0f); UpdateMuscle(lumbarExtensor, 0f);
        UpdateMuscle(neckFlexor, 0f); UpdateMuscle(neckExtensor, 0f);
        UpdateMuscle(headFlexor, 0f); UpdateMuscle(headExtensor, 0f);
        UpdateMuscle(leftShoulderFlexor, 0f); UpdateMuscle(leftShoulderExtensor, 0f);
        UpdateMuscle(rightShoulderFlexor, 0f); UpdateMuscle(rightShoulderExtensor, 0f);
        UpdateMuscle(leftElbowFlexor, 0f); UpdateMuscle(leftElbowExtensor, 0f);
        UpdateMuscle(rightElbowFlexor, 0f); UpdateMuscle(rightElbowExtensor, 0f);
        UpdateMuscle(leftWristFlexor, 0f); UpdateMuscle(leftWristExtensor, 0f);
        UpdateMuscle(rightWristFlexor, 0f); UpdateMuscle(rightWristExtensor, 0f);
    }
}