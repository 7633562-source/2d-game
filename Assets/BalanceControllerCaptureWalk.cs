using UnityEngine;

// WALK-3 — model-based capture-point walker.
//
// This is a SECOND walker standing beside the legacy fall-catch FSM, not a
// patch on it. Default useCaptureWalker 1: this walker owns -walk 1.
// 0 is WalkLift (-captureWalk 0). The single entry gate is
// CaptureWalkActive() in BalanceController.FixedUpdate.
//
// Model. A trunk over one sole is an inverted pendulum, so the quantity
// that decides where the next foot must land is not the position of the
// CoM but the capture point (divergent component of motion):
//
//     omega0 = sqrt(g / h)         h = CoM height over the stance sole
//     xi     = x_com + v / omega0
//
// Standing still gives xi = x_com; the faster the body travels the further
// ahead the sole has to reach. Landing exactly ON xi brings the pendulum to
// rest — that is the emergency step, not a gait. Periodic walking needs the
// sole slightly BEHIND xi so the body keeps a residual forward velocity for
// the next step:
//
//     b*     = v_des * T / (exp(omega0 * T) - 1)
//     xi(T)  = p + (xi - p) e^{ωT}     p = stance sole
//     x_foot = xi(T) - b* + kv * (v - v_des)
//
// b* is the offset of the periodic orbit for step time T (exp(omega0*T) - 1
// is how much one step grows the pendulum state), and the kv term is a
// one-step correction of the speed error. Emergency stop is b* = 0.
//
// The landing point is the LIPM capture point at touchdown, not Euler v*T.
// A free particle travels v*T; an inverted pendulum grows as e^{ωT} about
// the stance sole. From rest v*T is 0, so freeze-xi plus cruise b* / kv put
// the first sole behind live xi (~15 cm). Skipping the live reach clamp
// on that first catch then left X unclamped: world Y at freeze height
// plus a long target next to a dropping hip is hip −90 / knee 110
// (p_auto 16:26). Clamp uses Max(live, freeze reachSpan) so a sag
// cannot shrink the footprint; hip/knee targets stay off the hinge
// stops. Ankle Y stays at slab+clearance — restoring chord length
// by driving Y onto the slab welds the sole (p_auto 16:59).
// Landing ahead of the CoM is normal walking; the catch is
// landing on or ahead of freeze-xi, and not behind landing-xi by more
// than captureRecoveryMargin.
//
// Frozen plan. The legacy path recomputes the landing point every frame
// from a capture point that the moving swing leg itself perturbs, so the
// target walks away from the foot chasing it. Here the plan is written
// ONCE, on the frame the foot is released, at the predicted touchdown xi.
// The swing tracks that world point. Residual xi past freeze-xi is the
// gait: a returned sole is a plant. An airborne miss re-freezes cruise
// from live xi. Recovery b*=0 is only a fallen catch, not the second step.
//
// One owner per joint — never two controllers on one hinge:
//
//     swing hip + knee   while the CAUDAL heel corner is still a pin:
//                        latched hip, rocker-matched knee (~3 cm /
//                        12° plantarflex about the ball), plantarflex
//                        + MTP plus. A quiet locked knee made that
//                        plantarflex isometric (burn_walk swap 0).
//                        0.12 m clearance IK into the pin is the sit
//                        (p_auto 5–6 s). Gait freezes Swing when the
//                        caudal corner rises, not when the filled heel
//                        AABB empties. After the pin the 2-link IK
//                        raises the free foot (X frozen, Y hip-relative)
//                        even if the toe still scrapes. Not scissor:
//                        no −swingHipFlex, no forward reach. After air
//                        the frozen plan plants from that height.
//     swing ankle        DualSupport onset: plantarflex on the lean
//                        clock (plus, heel rise), not Zero. Zero and
//                        world-flat left heel OR toe in the manifold
//                        (burn_walk posed=0 / swingFoot 0.94 / swap 0;
//                        p_auto left 0.95 drag). Fold clock keeps that
//                        rocker. MTP plus on the same clock keeps the
//                        toe the pivot; MTP minus while the pin is
//                        down unloads the ball and leaves the 0.18 m
//                        heel box as the only contact. After caudal
//                        rise, MTP minus is clearance.
//     stance ankle       CoP regulator (existing ankleComP / ankleComD loop)
//     toe MTP            SPD: 0 flat in mid-stance, plus (down) only
//                        late in T on the stance sole (push-off), plus
//                        on the TRAIL while the caudal pin is down
//                        (the rocker pivot — not global toe-off: stance
//                        MTP stays 0), and minus (up) in the air. A
//                        swing toe may still TOUCH the slab with loaded
//                        false — that is clearance, not a second
//                        support, and it does not hold the toe at 0.
//                        Not the ankle CoM loop.
//     stance hip + knee  IK to the FROZEN plant during the swing, hip
//                        joint = walking pelvis lean − IK thigh (same
//                        algebra as ControlHipsToWorldUpright). Tracking
//                        measured pelvisTilt was zero P error and the
//                        swing-hip reaction sat the trunk plus (capwalk3
//                        +137, CoM 39 cm behind the feet). Trail hip in
//                        first DualSupport latches the trail hip joint
//                        on the first walking frame (quiet angle) and
//                        SPD-holds it. Zero through lean let the thigh
//                        sag; KneeLift about that sag is the chair
//                        (burn_stag 6.300 s, knee 121°). RestoreHip on
//                        the trailer is the other failure: a second
//                        column into the slab. Latched joint hold is
//                        neither. Knee HOLDS the quiet chord through
//                        onset lean, then shortens about that fixed
//                        thigh. First DualSupport CoP is the STANCE
//                        sole only. CoP on the trail too is a second
//                        pin (solver 50/50): P-error cancels inside
//                        the closed chain, comX stays ~0.069 m.
//                        captureLaunchLead travel never arrives on
//                        stacked soles (burn_walk 0.0668 m / 40 s /
//                        swap 0) so it is not the fold gate. Fold
//                        opens after onset lean with the stance hip
//                        still up; DualSupportMax of a loaded trail
//                        is still the 6.300 s dump. Gait first freeze
//                        is posed (onset lean clock), not CoM travel
//                        and not trail-up of a two-pin weld — lean
//                        tests hold comX at 0.069 m / margin 0.14 at
//                        14° torso. Swing keeps in-place lift IK
//                        while the sole touches. Later DualSupport
//                        still waits for trail-up. After a plant the
//                        trail HOLDS the measured thigh until CoM
//                        leaves its plate.
//     chest              lumbar SPD tracks the walking lean in every
//                        phase. One-way hold in Swing rode a dump past
//                        that lean to −90° (capwalk4). Stance hip
//                        restores the same pelvis lean, so this is not
//                        two controllers on the trunk.
//
// Cycle: DoubleSupport -> Swing -> TouchdownBlend -> DoubleSupport, plus
// RecoveryStep when the frozen plan can no longer catch xi. Six owners,
// no closed loop: GaitStateMachine holds walkPhase / state / stanceFoot /
// swingFoot and takes contact events plus xi; ContactSensor holds
// grounded / touching / loaded / supporting from colliders and positions
// and does not read walkPhase; CapturePointPlanner holds omega0, xi and
// the landing X from positions and velocity and does not read walkPhase;
// SwingTrajectory holds the airborne curve from the frozen start and
// goal; CaptureBalance holds the lumbar PD and the pelvis hip-hold
// targets; ActuatorMixer turns those targets plus the swing IK into
// Muscle.activation. FallRecovery (the RecoveryStep drive and freeze)
// stays here. Drive still combines gait.MayCarry with touching so a swing
// scrape is not a second support. Single-support phases have a 0.5 s
// watchdog. Double support with both soles down and CoM still on the
// trail does not: that timeout lifted the loaded rear (capwalk2 sit).
// Touchdown is a contact CONFIRMED over captureTouchdownFrames physics
// steps AFTER the swing sole has actually been up, so an empty contact
// manifold on a pitched toe is not a landing, and neither is the
// release-frame contact.
//
// Recovery is two-sided. xi past the TOE is a forward dump; xi past the
// HEEL is a backward dump. The old emergency was only (xi-ankle)*dir
// past the toe, so a +68° back lean never opened Recovery from
// DoubleSupport — collapsed blocked the periodic plan, watchdog froze
// ph=3 with both soles still down. While the swing has not actually
// been up, Y stays at the apex: planting at 70% of T, or snapping to
// landY after T because the sole is still touching, is a weld, and
// Recovery T = 0.22 s then re-zeroed the peel every 0.37 s. The ankle
// does not wait for phase 0.5 to cock the heel: world-flat on a glued
// Recovery sole is the same weld. A backward miss (live xi behind the
// frozen target) abandons even a recovery plan; the catch is land ON
// xi, not a 0.30 m ratchet from the swing ankle.
//
// PD gains, torque signs, the 200 Hz step, the ankle CoM loop and contact
// friction are untouched: this walker only chooses the targets those
// existing regulators are handed.
public partial class BalanceController
{
    [Header("WALK-3 capture-point walker (0 = legacy fall-catch FSM)")]
    [Tooltip("1 = the model-based capture-point walker owns walk. 0 = legacy StepPhaseDriver Stance/Transfer (WalkLift overlay). Headless -walk 1 uses this walker; -captureWalk 0 restores WalkLift.")]
    public int useCaptureWalker = 1;
    [Tooltip("Step time T of the periodic orbit, s. 0.32 let xi(T) grow past the 0.17 m freeze footprint (capwalk7 vault after 3 steps). 0.26 plants sooner so e^{ωT} is ~2.4 not ~3.")]
    public float captureStepDuration = 0.26f;
    [Tooltip("WALK CYCLE 6d gate. 1 = the plan's step time T follows the walker's own measured CoM speed: T = captureStepDuration while |v| <= CaptureDesiredSpeed, then T = captureStepDuration * v_des / |v|, floored at CaptureSwingDurationFloorSeconds, so the travel per step stays at the walking length while the cadence follows the mass. 0 = the constant captureStepDuration of every cycle before 6d, to the bit. A measured walk has to be able to name which of the two it ran.")]
    public int captureStepSpeedAdapt = 1;
    [Tooltip("Share of T over which the swing ankle covers its landing X. 1 = smoothstep over the whole step — the slow carry that lets the trunk dump. Below 1 the foot arrives early and waits on that X for the Y plant. Floor 0.20 so a tiny window cannot whip 16 kg of leg. 0.70 left X still travelling while CoM passed the toe.")]
    public float captureSwingXShare = 0.50f;
    [Tooltip("Desired speed v_des, m/s. The sign comes from intent.moveX.")]
    public float captureTargetSpeed = 0.55f;
    [Tooltip("Multiplier on v_des while intent.run = 1.")]
    public float captureRunSpeedScale = 1.6f;
    [Tooltip("kv in x_foot: one-step correction of the speed error, s.")]
    public float captureSpeedGain = 0.10f;
    [Tooltip("Share of LIPM xi(T) = p + (xi-p)e^{ωT} written into the frozen landing point. 1 = the pendulum predictor. 0 = freeze-xi (the Euler v*T miss from rest).")]
    public float captureLookahead = 1f;
    [Tooltip("Shortest step the plan may command, m, measured from the stance sole.")]
    public float captureStepMin = 0.05f;
    [Tooltip("Longest step the plan may command, m. Anatomy cap, not a target.")]
    public float captureStepMax = 0.42f;
    [Tooltip("Apex clearance of the swing ankle over its landing height, m.")]
    public float captureSwingApex = 0.07f;
    // The step is measured from the swing ANKLE, but what can be put on the
    // slab is decided by the HIP: two 0.43 m bars reach a circle of radius
    // 0.86 m, and a walking hip stands about 0.85 m over the slab, so the
    // sole can only be planted a dozen centimetres out of the vertical.
    // captureStepMax 0.42 m is therefore not a reach bound at all, and
    // SolveCaptureLegIk answers an out-of-reach target by clamping the chord:
    // the leg straightens toward the point and the ankle RISES as the target
    // moves further out. A plan that over-reaches parks the sole in the air,
    // contact is never confirmed, TouchdownBlend never runs, the roles never
    // swap — the body just holds the lean it caught itself in.
    [Tooltip("Fraction of the two bars a landing point may sit from the swing HIP. Outside that circle the leg cannot reach the slab at all and the sole hangs in the air. Must sit STRICTLY above captureStanceLegLength or the footprint is a point. 0 = no reach bound, i.e. the plans that never landed.")]
    public float captureReachMargin = 0.99f;
    [Tooltip("How far behind its own start an emergency plan may place the sole, m. captureStepMin is a FORWARD floor, so without this a foot that has over-reached can never be brought back under the body: every re-plan pushes it further out. 0 = the former forward-only ratchet.")]
    public float captureStepBackMax = 0.30f;
    [Tooltip("Consecutive abandoned plans on ONE swing after which the sole is placed straight under the predicted hip — the one point that is reachable at any speed — instead of at the model point. 0 = never, i.e. re-plan forever with the foot in the air.")]
    public int captureAbandonPutDown = 2;
    [Tooltip("Ankle target above the sole-on-slab height at plant, m.")]
    public float captureFootLandLift = 0.004f;
    [Tooltip("How far the target keeps sinking past T, m: presses the sole into contact.")]
    public float captureFootPress = 0.02f;
    [Tooltip("Toe-up of the swing foot over the second half of the swing, deg (heel first).")]
    public float captureFootPitchDeg = 8f;
    [Tooltip("Minimum double support before a new plan may freeze, s. 0.06 stomped (capwalk5, 29 swaps); 0.16 froze cadence to one step (capwalk6). 0.10 is the middle.")]
    public float captureDoubleSupportMin = 0.10f;
    [Tooltip("Longest stacked double support before the first step freezes, s. A spanned pair does not use this: that was lifting the loaded trail (capwalk2).")]
    public float captureDoubleSupportMax = 0.30f;
    [Tooltip("Share of the foot span the CoM must roll onto the leading sole before release, 0…1.")]
    public float captureDoubleSupportShare = 0.62f;
    [Tooltip("Consecutive physics steps of sole contact that count as a landing.")]
    public int captureTouchdownFrames = 3;
    [Tooltip("Seconds of role handover after a confirmed touchdown.")]
    public float captureTouchdownBlendSeconds = 0.09f;
    // A biped is statically supported only while a sole is on the slab. The
    // walker may hand the support over, but it may not have NO support: the
    // moment both feet are in the air the only torque left on the trunk is
    // gravity about the CoM, and 0.1 s of that is the -66 deg dive. So the
    // roles are swapped only after the swing sole is confirmed down AND both
    // soles have carried together for this long — the stance foot leaves
    // because the next one has already arrived, never before.
    [Tooltip("Both soles must be down together for this long after a landing before the roles swap and the next plan may release a foot, s. 0 = the former handover, which could release the old stance on the same step the new sole bounced.")]
    public float captureDoubleSupportHoldSeconds = 0.05f;
    [Tooltip("Extra knee flex on the landing leg, faded out over the blend, deg.")]
    public float captureLandKneeFlex = 12f;
    // The two bars are equal, so the hip-over-ankle chord is
    // bars * cos(flex / 2): the commanded knee is flex = 2 * acos(this).
    // 0.93 is therefore 43.1 deg of knee, not "nearly straight" — the knob
    // sits on the steep part of arccos, where one per cent of leg length is
    // 3 to 8 deg. Quiet stance measures 4.5 deg (chord 0.999 of the bars), so
    // 0.93 asked the pair of knees for a 6 cm two-leg squat on the frame walk
    // began. 0.99 = 16.2 deg, an ordinary mid-stance knee.
    //
    // 0.99 is also the number that made the whole walker degenerate, and in
    // three separate places at once. The hip then stands 0.851 m over the sole
    // on 0.86 m of bars, so (1) the footprint the leg can put a sole into is
    // sqrt(0.86^2 - 0.851^2) = 0.12 m, while the model asks for a step of about
    // v*T = 0.25 m — and with captureReachMargin 0.98 the reach circle (0.843 m)
    // is SHORTER than the leg itself, i.e. the footprint is exactly zero and
    // every landing point, frozen and tracked, collapses onto the one point
    // straight under the hip; (2) the stance-plant IK guard in
    // DriveCaptureStancePlant is bars - 0.01 = 0.85 m, below the 0.851 m the
    // pose itself demands, so the LOADED leg failed that test every frame and
    // ran on its hold-the-measured-pose branch — a frozen strut, not a
    // regulated support; (3) a two-link chord at d -> bars is a singular
    // Jacobian, so a centimetre of hip travel over the plant asks for an
    // unbounded joint rate and no cap, rate limit or gain can follow the
    // contact. A body vaulting over a rigid strut pitches the way the strut
    // leans, which is the +59 deg backward sway, and flips forward when the
    // CoM finally crosses the ankle. That gait still travelled 1.82 m: the
    // stride was pure hip lookahead, not a capture-point plant.
    //
    // 0.94 closed the empty footprint (0.81 m hip, 39.9 deg knee, 0.24 m
    // reach) and immediately sat the mass behind the 0.065 m heel arm. The
    // only way a planted pair shortens 4 cm is a chair squat, and 100 N*m of
    // the 130 knee ceiling is already spent just holding that pose, so the
    // trunk dumps +90 deg on the first step and travel collapses to 0.15 m.
    // Do not go below ~0.93 without raising Human.kneeMuscleTorque.
    //
    // 0.97 sits between those two failures: 0.834 m hip (28 deg knee), the
    // plant guard still has 1.6 cm at mid-stance, hold torque is 72 N*m, and
    // with captureReachMargin 0.99 the footprint is
    // 0.86 * sqrt(0.99^2 - 0.97^2) = 0.17 m. At T = 0.32 s the model asks
    // v*T = 0.176 m, which sits inside that circle instead of 0.25 m past it.
    // The freeze still used that 0.17 m as the plant circle (high hip as
    // drop), so xi(T) past it was clipped and the sole sat behind CoM
    // (capwalk7: 3 steps, then vault). Extra reach is the landing chord
    // only; this field stays 0.97.
    [Tooltip("Stance hip-over-sole as a fraction of the bars. Knee flex = 2*acos(this): 0.97 = 28 deg. Do not drop to 0.94 as the stance pose — that sat both knees.")]
    public float captureStanceLegLength = 0.97f;
    [Tooltip("Landing hip-over-sole as a fraction of the bars, used ONLY for the swing plant circle. Stance knee stays at captureStanceLegLength 0.97. 0.94 ≈ 40 deg / 0.29 m reach. Freeze used the high stance hip as drop, span 0.17 m, and clipped xi(T) — the capwalk7 toe vault. 0 = actual freeze drop.")]
    public float captureLandLegLength = 0.94f;
    [Tooltip("Seconds to fade the stance-knee and trunk-lean targets from the pose walk started in. 0 = the former step change on the first frame.")]
    public float captureEntryRampSeconds = 0.40f;
    // A leg that carries the body is a support, not a pogo stick, and the knee
    // is the one joint in this body whose muscle is far stronger than the
    // support it serves. For two bars the knee stands out of the hip-ankle line
    // by d = l1*l2*sin(flex)/D, and virtual work turns knee torque into a push
    // along the leg: F = tau / d. At the commanded 16 deg of stance d is 6.1 cm,
    // so 130 N*m is 2.1 kN = three body weights on ONE leg and six on the pair —
    // while the ankle (65 N*m over the 0.195 m toe) is by construction exactly
    // one. Nothing else here can throw the body off the slab.
    [Tooltip("Ceiling on the push the LOADED legs may put along their own axis, in body weights; the pair shares this number. 1 = they may hold the body and nothing more. Steady stance needs 1/this of the cap, so 1.25 leaves 25 % margin and changes nothing once the leg has settled — it only clips the transient that launches the body.")]
    public float captureLegPushCap = 1.25f;
    [Tooltip("Fastest the walker may ask a LOADED knee to straighten, deg/s. A walking hip rises about 0.1 m/s, which is 90 deg/s at this arm. 0 = no rate limit, i.e. the former step change.")]
    public float captureSupportKneeExtendRate = 90f;
    // The knee is not the only muscle that can throw the body off its own
    // sole. A thigh whose foot is planted is welded to the slab through the
    // shin, so hip torque there does not swing a leg — it rotates the whole
    // body about the contact, and the contact can only pay m*g*arm before the
    // sole itself turns: 70 kg on the 0.065 m heel arm is about 45 N*m.
    // Anything above that peels the heel, which is the first millimetre of
    // "both feet in the air".
    //
    // That arm is not the same in both directions, so one symmetric number is
    // wrong. jointAngle = pelvis - thigh, and a positive hip signal (target
    // above the measured angle) leans the pelvis back and carries the hip back
    // with it, so the load rolls onto the HEEL: 0.065 m, 45 N*m. A negative
    // signal rolls it onto the TOE: 0.195 m, 134 N*m. Capping both at 45
    // forbids exactly the torque that arrests a backward sway — the hip may
    // not pull the trunk forward over its own sole — while nothing stops the
    // body from continuing back. The forward cap is inert above
    // hipMuscleTorque by design: the toe can pay for anything the muscle makes.
    [Tooltip("Largest torque this walker may ask of a LOADED hip in the direction that rolls the load onto the HEEL, N*m. About m*g*heel arm (70 kg on 0.065 m = 45 N*m) — past that the sole rotates instead of the body. 0 = no cap. Only the position term is bounded; damping may still oppose motion.")]
    public float captureStanceHipTorqueCap = 45f;
    [Tooltip("Same ceiling in the direction that rolls the load onto the TOE, N*m (m*g*0.195 m = 134). This is the recovery from a backward sway, and the toe arm is three times the heel arm, so it must not share the heel number. Above hipMuscleTorque it is inert. 0 = no cap.")]
    public float captureStanceHipTorqueCapForward = 130f;
    [Tooltip("How far below the latched plant the stance ankle target is held, m. 0 = on the contact. A positive press is a length-servo into the slab: the knee extends, both soles leave, that is the micro-hop. Peel recovery does not need it — a risen sole already sits ABOVE the latch.")]
    public float captureStancePlantPress = 0f;
    [Tooltip("Peak forward chest lean at step ONSET, deg (minus on the world target). Held constant, 8–12 vaults and 5–6 sits — no static middle. This number is the launch; CaptureLeanPhase fades it toward plant.")]
    public float captureTrunkLeanDeg = 9f;
    [Tooltip("Peak forward pelvis lean at step ONSET, deg (minus). Loaded lead hip tracks the phased value, not the measured sit. 9–12 constant vaulted (capwalk4/6); 6 sat (capwalk5).")]
    public float capturePelvisLeanDeg = 10f;
    [Tooltip("Lean at plant as a fraction of the peak. 1 = hold the peak through landing (the vault). 0 = quiet hipBaseAngle / upright chest at plant. A small plus is a back lean into the new sole.")]
    public float captureLeanPlantFraction = 0f;
    [Tooltip("CoM setpoint ahead of the support centre, m, along walk dir. Dual support uses the LEAD sole as that centre so the trail ankle drives CoM onto the plant (a per-foot centre left CoM parked on the rear: capwalk2). Plus is forward.")]
    public float captureCopLead = 0.015f;
    [Tooltip("CAPTURE PLACEMENT: kLead on the CoM's own capture point xLand = xCom + vCom*sqrt(h/g), recomputed from measured state every frame, that the SWING sole is driven to. One LIPM step about the new sole keeps its speed at tanh(omega*T/2) (0.39 at omega 3.2 / T 0.26); 0.37 sits just under that, so the walk neither decays to a stop (which is what k above the neutral value does) nor runs away. 1 = full catch (the mass is arrested at the plant). 0 = off: the frozen plan target and the old fixed catch lead only (behaviour before this field, to the bit).")]
    public float captureComLandingScale = 0.37f;
    [Tooltip("Scale on the existing ankleComP/ankleComD CoP loop. 1 = that loop unchanged.")]
    public float captureCopScale = 1f;
    [Tooltip("When CoM is already ahead, fade D braking up to this share of the current forward speed (capped at v_des), so the toes do not fight a captured step. 0 = quiet-stance D (damps all velocity). Full v_des from rest saturates the flexor (capwalk4–8); zero seek parks and chatters (capwalk5/9). Launch uses captureLaunchSpeed, not this.")]
    public float captureCopTravelBias = 1f;
    [Tooltip("Seconds to ramp CoP lead and ankle speed-seek from quiet stance to the launch. Smoothstep. 0 = the former step of command -1.")]
    public float captureLaunchSeconds = 0.70f;
    [Tooltip("Ankle |command| ceiling at launch onset, 0…1. P+D saturates at 1 from a 1.5 cm error; 0.28 is ~6 N·m dorsiflex, enough to roll CoM without the vault. Ramps to 1 over captureLaunchSeconds.")]
    public float captureLaunchAnkleCap = 0.28f;
    [Tooltip("Speed the CoP D-term seeks at the end of the launch, m/s. Below v_des 0.55 so the 22 N·m flexor cannot pin. Onset seeks 0.10. 0 = capwalk9 park.")]
    public float captureLaunchSpeed = 0.22f;
    [Tooltip("First DualSupport CoM travel past the launch sample, m. Stance CoP seeks this point so P can roll CoM once the trail is not a second pin. Not the swing-fold gate: stacked soles never deliver this lead (burn_walk 0.0668 m). Gait still freezes Swing when that trail actually leaves.")]
    public float captureLaunchLead = 0.03f;
    [Tooltip("xi this far ahead of the stance ankle, m, used to open RecoveryStep. Live walk passes 0 so the gate is the toe (supportRadius). 0.08 is still on the plate and stole post-plant DS.")]
    public float captureCatchPast = 0.08f;
    [Tooltip("Capture point past the frozen landing point by this much, m, opens a re-plan during Swing.")]
    public float captureRecoveryMargin = 0.06f;
    [Tooltip("Step time T of an emergency step, s.")]
    public float captureRecoveryDuration = 0.22f;
    [Tooltip("Seconds past T without a confirmed landing before the plan is abandoned.")]
    public float captureSwingOvertime = 0.15f;
    [Tooltip("Hip height below the straight leg that stops planning new steps, m.")]
    public float captureCollapseDrop = 0.30f;
    [Tooltip("Hard ceiling on a single-support phase, s. Past this the plant is dropped and the walker takes a recovery step. Double support with both soles down and the CoM still on the trail does not use this: lifting that trail is the capwalk2 sit. 0 = off.")]
    public float capturePhaseWatchdog = 0.5f;

    private readonly ContactSensor captureContact = new ContactSensor();
    private readonly GaitStateMachine captureGait = new GaitStateMachine();
    private readonly CapturePointPlanner capturePlanner = new CapturePointPlanner();
    private readonly SwingTrajectory captureSwing = new SwingTrajectory();
    private readonly CaptureBalance captureBalance = new CaptureBalance();
    private readonly ActuatorMixer captureMixer = new ActuatorMixer();
    private CaptureWalkState captureState => captureGait.State;
    private float captureStateAge => captureGait.StateAge;
    private int captureStanceSign => captureGait.StanceFoot;
    private int captureStepCount => captureGait.StepCount;
    private bool captureWalkWasActive;
    // Standing-Plant restore zeroes gait StepCount so the next DualSupport
    // is onset again. Acc keeps the swaps already taken so the recorder
    // max still sees the whole walk (2 then restore then 4 → 6, not 4).
    private int captureWalkStepAcc;
    private CapturePointPlanner.Plan capturePlan;
    // Last accepted IK solve of the swing leg. The landing hip holds this
    // world thigh angle through the blend instead of being handed to a
    // second controller mid-air.
    private float captureLastIkThighDeg;
    private float captureLastIkKneeDeg = 20f;
    // ─── WALK CYCLE 6i: ONE continuous airborne clock for the swing sole ───
    // The gait's SwingAge / SwingAirAge advance ONLY inside DecideSwing, and
    // Enter(TouchdownBlend) clears SwingLeftGround and SwingAirAge outright
    // (GaitStateMachine.cs:665-679). An airborne landing sole driven from the
    // blend therefore sees a STALE age. c6h_walk, third swing: frozen at
    // walk-relative +1.795 (v 0.278, T = captureStepDuration 0.26, v < v_des),
    // blend entered +1.820 with the sole 0.020 m off the slab and held 0.580 s
    // until the swap at +2.400. SwingAge stopped at 0.020 s for that whole
    // 0.580 s, so DriveCaptureSwingLeg fed the curve trackAge 0.020 of its own
    // 0.26 s step: xEase = smoothstep(0.020 / (0.5 * 0.26)) = 0.066, i.e. the
    // curve handed the solver startX + 6.6 % of the step. The third sole
    // landed on the SECOND sole's X (its plate [0.1388, 0.4150] sits inside
    // the stance plate [0.1456, 0.4121], xCopMeas stays 0.333 through the
    // two-plate contact), while the live ask ran 0.435 -> 0.742 m and the CoM
    // covered 0.362 m in the same single support: a null step, and the trunk
    // answered with the -44.5 deg fold.
    // This clock starts when the SWING sole leaves the slab and runs until it
    // touches again, or the roles swap. It is only ever AHEAD of the gait's
    // ages once those have stopped, so the peel, the periodic Swing and the
    // Recovery catch keep the curve they had, to the bit.
    private float captureAirborneAge;
    // ─── C1 instrumentation (recorder only; the control math never reads it) ───
    // CaptureIkSaturated is set on every SolveCaptureLegIk with whether the chord
    // had to be clamped, CaptureIkSolveTick says when that solve happened.
    public bool CaptureIkSaturated;
    public int CaptureIkSolveTick = -1;
    private float captureLastCapturePointX;
    private float captureLastOmega0;
    private float captureSoleThickness;
    private bool captureSoleResolved;
    private BoxCollider2D captureLeftFootBox;
    private BoxCollider2D captureRightFootBox;
    private BoxCollider2D captureLeftToeBox;
    private BoxCollider2D captureRightToeBox;
    // Walk is requested from a quiet stance, so every walking target used to
    // be a step change on the frame the flag went up. errorReferenceDegrees is
    // 10, so a 38 deg jump of the knee target is 3.8 reference bands: the SPD
    // saturates on the first step and the pose is reached ballistically
    // instead of being regulated. This clock fades the standing pose into the
    // walking one, so the same regulators actually track their targets.
    private float captureEntryAge;
    private float captureEntryBlend;
    private float captureLaunchAge;
    private float captureEntryKneeDeg = float.NaN;
    // Dirt lift in DoubleSupport: stance extends on the take clock
    // (the long strut) while the swing peels in place. Age < 0 means
    // the clock has not started. Fold age is a second clock so a late
    // gate cannot slam the clearance pose in one step. Hip-hold plus
    // a 0.12 m chord into a heel-OR-toe pin sits the pelvis (p_auto
    // 5–6 s). Fold latches the airborne lift point (X frozen, Y
    // hip-relative). While the sole still touches, the command is
    // the rocker, not that point. Lift X/Y / relY are NaN until fold
    // opens.
    private float captureDirtLiftAge = -1f;
    private float captureDirtFoldAge = -1f;
    private float captureDirtFoldKneeDeg = float.NaN;
    private float captureDirtFoldAnkleDeg = float.NaN;
    private float captureDirtFoldHipDeg = float.NaN;
    private float captureDirtFoldLiftX = float.NaN;
    private float captureDirtFoldLiftY = float.NaN;
    private float captureDirtFoldLiftRelY = float.NaN;
    private bool captureDirtFoldHeelFreed;
    // CoM x on the first CaptureWalk tick of this episode. Stance CoP
    // seeks this sample plus captureLaunchLead. Gait freezes Swing on
    // trail-up, not from this travel (stacked share is 0.5; quiet
    // ankle lead is already ~7 cm).
    private float captureLaunchComX = float.NaN;
    // Stance-hip Y on that same first tick. Timer fold of a still-loaded
    // trail is the 6.300 s dump: DualSupportMax peeled a chair whose CoM
    // had only bowed (burn_walk 0.0668 m, then KneeLift). Fold still
    // refuses a sat hip (this Y minus CaptureDirtLiftDropM). It does
    // not wait for captureLaunchLead: that travel is the freeze.
    private float captureLaunchHipY = float.NaN;
    // Last knee target handed to a loaded leg, per side, for the extension
    // rate limit. `Held` is false while the foot is in the air, so a leg
    // restarts the limit from the pose it actually lands in.
    private float captureLeftSupportKneeTarget;
    private float captureRightSupportKneeTarget;
    private bool captureLeftSupportKneeHeld;
    private bool captureRightSupportKneeHeld;
    private float captureBodyWeightN;
    private bool captureBodyWeightResolved;
    // Where the loaded sole met the slab. Latched, not read live: see
    // LatchCaptureStanceAnchor.
    private Vector2 captureStanceAnchor;
    private bool captureStanceAnchorValid;

    public int CaptureWalkStateCode => (int)captureState;
    public float CaptureWalkStateAge => captureStateAge;
    // LEVEL 2: the pose drive publishes its own step count here, so walkSwap in
    // the trial JSON measures the gait that actually ran, whichever it is.
    public int poseDriveStepCount;
    // CYCLE 120 - THE WALK-ON GUARD (see PoseDriveController.ReleaseWalkSettle /
    // FallReflex): the settle release's own countdown, s. The release arms it;
    // FallReflex drains it and defers its catch-step fire while it lives. 0 = no
    // guard, the pre-120 law bit for bit.
    public float settleReleaseGuardT;
    public int CaptureWalkStepCount => captureWalkStepAcc + captureStepCount + poseDriveStepCount;
    public int CaptureStanceFootSign => captureStanceSign >= 0 ? 1 : -1;

    // ─── WALK CYCLE 6j instrumentation: the DELIVERED plant geometry ───
    // Four read-only publications of values DriveCaptureSwingLeg ALREADY
    // computes. They exist because the loss between the live capture ask and
    // the sole that actually lands (6i: ask 0.43-0.75 m, delivered plate
    // ankle ~0.182 m) could only be inferred from the existing columns to
    // ~0.03-0.04 m: neither the point handed to the solver, nor the plan
    // target in force after its clamps, nor the age the curve was evaluated
    // at, nor the swing ankle's own world X was recorded. These are
    // recorder-only instruments: no gain, limit, threshold, sign or target
    // anywhere reads them back, and 0 is the "walker not running" sentinel
    // (reset in ResetCaptureWalk), never NaN.
    private float captureLastSwingAskX;
    private float captureLastSwingSoleX;
    private float captureLastSwingTrackAge;
    private float captureLastPlanTargetX;
    // captureSwingAskX — the X of the point this frame handed to
    //    SolveCaptureLegIk (post reach disc, pre chord clamp), m.
    // captureSwingSoleX — the swing ankle's MEASURED world X in that same
    //    frame, m (CaptureAnklePoint).
    // captureSwingTrackAge — the age the swing curve was evaluated at, s,
    //    after the 6i airborne clock (trackAge after the Max).
    // capturePlanTargetX — the plan target in force that frame, m, after the
    //    reach-disc X clamp and the live-ask floor.
    public float captureSwingAskX => captureLastSwingAskX;
    public float captureSwingSoleX => captureLastSwingSoleX;
    public float captureSwingTrackAge => captureLastSwingTrackAge;
    public float capturePlanTargetX => captureLastPlanTargetX;

    // ─── WALK CYCLE 6k instrumentation: THE SOLVER'S OWN ANSWER ───
    // 6j proved the leg is where the solver told it to be (hip/knee/ankle
    // within 1-3 deg of req*) and the ankle 0.457 m short of the point the
    // solver was handed. That leaves two owners and the 6j columns cannot
    // separate them: (a) the solver's own input-to-solution kinematics (its
    // returned angles do not put the ankle at the ask), or (b) the delivered
    // pose is built from something other than the ask (the world ask is
    // converted into the parent's frame to make a joint command). The values
    // below are the solver's own answer and the frame that answer is written
    // in; 0 is the "walker not running" sentinel (reset in ResetCaptureWalk),
    // never NaN, and nothing reads them back — recorder-only instruments.
    private float captureLastSwingSolvedSoleX;
    private float captureLastSwingSolvedResidualX;
    private float captureLastSwingSolveFrameX;
    private float captureLastSwingSolveFrameAngleDeg;
    // captureSwingSolvedSoleX — world X of the ankle that the angles RETURNED
    //    by SolveCaptureLegIk put the ankle at this frame (forward kinematics
    //    of the returned thigh/knee, same two links), m. The solver's own
    //    answer, not the measured leg: CaptureAnklePoint is the measured one.
    // captureSwingSolvedResidualX — captureSwingAskX minus
    //    captureSwingSolvedSoleX, m. 0 while the walker is off; ~0 means the
    //    solver gave back exactly the point it was handed, and the gap lives
    //    downstream of it.
    // captureSwingSolveFrameX — world X of the hip hinge the chord was
    //    measured from this frame, m (the origin of the solve).
    // captureSwingSolveFrameAngleDeg — the tilt the returned WORLD thigh
    //    angle is converted into a hip joint command against
    //    (CapturePelvisWorldTarget, i.e. the plan's pelvis tilt, deg). The
    //    solve itself is world-space; this is the only body frame in the
    //    path, so it is the frame that lets the plant's -22.4 deg trunk tilt
    //    be tested as the owner.
    //    WALK CYCLE 6m: the value is UNCHANGED (still the plan's tilt) but
    //    its meaning is now only a reference: the returned angles are no
    //    longer converted against it (the command is written on the
    //    MEASURED parent, captureSwingAppliedFrameAngleDeg). Reading the
    //    two columns as a difference still gives the plan-vs-measured sag
    //    that 6l measured as the 0.5015 m of the plant.
    public float captureSwingSolvedSoleX => captureLastSwingSolvedSoleX;
    public float captureSwingSolvedResidualX => captureLastSwingSolvedResidualX;
    public float captureSwingSolveFrameX => captureLastSwingSolveFrameX;
    public float captureSwingSolveFrameAngleDeg => captureLastSwingSolveFrameAngleDeg;

    // ─── WALK CYCLE 6l instrumentation: THE POSE THAT WAS APPLIED ───
    // 6k put the loss downstream of the solver: captureSwingSolvedResidualX is
    // exactly 0.0000 on all 446 walk frames, while |solvedSole - measuredSole|
    // is 0.2047 m mean / 0.7150 max and at the plant (t 7.140) the ask and the
    // solver are together 0.6657 m against a measured sole of 0.2090 m. That
    // delivery has two halves and no column separated them: (i) the PARENT
    // FRAME - the solver's returned WORLD angles are written into a joint
    // command against captureSwingSolveFrameAngleDeg, which is the PLAN's tilt
    // (-3.00 deg at the plant and frozen there) while the body the hinge is on
    // is 19-30 deg further pitched; and (ii) the CLAMP/TRACKING between the
    // returned angles and the targets the joints are actually given
    // (reqHipR pegged at the -65.00 floor for the whole plant).
    // The four values below are the same two-link forward kinematics as 6k's,
    // rebuilt on the MEASURED parent frame and on the POST-CLAMP targets this
    // frame handed the actuators, so the 6k residual splits by subtraction:
    //     solvedSole - appliedSole   = the clamps / tracking
    //     appliedSole - measuredSole = the parent frame
    // 0 is the "walker not running" sentinel (reset in ResetCaptureWalk),
    // never NaN, and nothing in the control math reads them back: recorder-only
    // instruments, exactly like the 6j and 6k sets.
    private float captureLastSwingAppliedSoleX;
    private float captureLastSwingAppliedResidualX;
    private float captureLastSwingAppliedFrameX;
    private float captureLastSwingAppliedFrameAngleDeg;
    // captureSwingAppliedSoleX — world X of the ankle the pose that was
    //    ACTUALLY DRIVEN this frame puts the ankle at: the MEASURED hip hinge,
    //    the MEASURED parent tilt the joint command is incremented against,
    //    and the post-clamp hip/knee targets ApplyCaptureHip / ApplyCaptureKnee
    //    handed the PD this frame (identical link law, signs and segment
    //    lengths as CaptureSolvedAnklePoint). A COMMANDED pose, not the
    //    measured leg - that one is captureSwingSoleX.
    // captureSwingAppliedResidualX — captureSwingAskX minus
    //    captureSwingAppliedSoleX, m. 0 while the walker is off; ~0 means the
    //    applied chain already lands on the ask and the 6k residual is the
    //    parent frame, not the delivery.
    // captureSwingAppliedFrameX — world X of the MEASURED hip hinge this
    //    applied chain was built on, m, same units as captureSwingSolveFrameX.
    //    The solve is measured from that same hinge (6k) and no command of a
    //    frame has reached physics when either is read, so the two X columns
    //    coincide by construction and the frame difference lives entirely in
    //    the angle column below.
    // captureSwingAppliedFrameAngleDeg — the MEASURED tilt of the body that
    //    carries the hinge this frame (the hip hinge's own parent body, i.e.
    //    the pelvis whose rotation the joint angle is incremented against),
    //    deg, same sign convention as captureSwingSolveFrameAngleDeg: minus is
    //    forward, so the two columns subtract directly. The measured value is
    //    the one the joint command is really written in; the 6k column is the
    //    value it was SOLVED in.
    //    WALK CYCLE 6m: this is now the parent the returned hip/knee angles
    //    are APPLIED on as well - DriveCaptureSwingLeg writes the hip command
    //    against this same read - so the column stops being the 6l witness of
    //    the frame defect and becomes the frame of the applied pose. The 6l
    //    sub-face it supersedes is appliedSole - measuredSole, still the
    //    tracking between the command and the leg.
    public float captureSwingAppliedSoleX => captureLastSwingAppliedSoleX;
    public float captureSwingAppliedResidualX => captureLastSwingAppliedResidualX;
    public float captureSwingAppliedFrameX => captureLastSwingAppliedFrameX;
    public float captureSwingAppliedFrameAngleDeg => captureLastSwingAppliedFrameAngleDeg;

    // ─── WALK CYCLE 6n instrumentation: THE ASK'S OWN Y, AND THE FLEXION
    //     DEMAND IT CARRIES ───
    // 6m fixed the frame the ask is delivered in and left one quantity of that
    // ask unmeasured: its HEIGHT. The flexion an ask needs is a function of the
    // sole's height as much as its X — the solver's chord angle grows with the
    // point's X while its knee flex (hence the thigh's angle to the chord)
    // grows as the chord SHORTENS, i.e. as the ankle is asked HIGHER — so with
    // only the X column published, a flexion the ask needs cannot be attributed
    // to the X or to the Y, and an allowance must not be re-tuned against a
    // quantity nobody measures. Published first, before any number moved. 0 is
    // the "walker not running" sentinel (reset in ResetCaptureWalk), never NaN,
    // and nothing in the control math reads either of these back.
    private float captureLastSwingAskY;
    private float captureLastSwingHipDemandDeg;
    // captureSwingAskY — world Y of the point this frame handed to
    //    SolveCaptureLegIk, m (up is plus): the same point captureSwingAskX is
    //    the X of, i.e. after the reach disc, after
    //    CaptureSwingAnkleYForChord's clearance / hip-relative bounds and after
    //    the 6n flexion allowance. The clearance Y the ask is built against is
    //    therefore readable as a height, not inferred from the X.
    // captureSwingHipDemandDeg — the hip flexion the ask CARRIES in the frame
    //    it is delivered in, deg, with no allowance applied: that frame (the
    //    measured parent, the same read the command is written against) minus
    //    the world thigh SolveCaptureLegIk returns for the point. More negative
    //    = more flexion asked for. This is the PRE-BOUND demand: the 6n
    //    allowance spends itself on the ask, so this column is what keeps a
    //    demand the allowance refuses READABLE in the row instead of hidden by
    //    it. 0 = walker off / the solve returned no pose, never NaN.
    public float captureSwingAskY => captureLastSwingAskY;
    public float captureSwingHipDemandDeg => captureLastSwingHipDemandDeg;

    // ─── WALK CYCLE 6o instrumentation: THE FRAME THE PLAN TARGET IS BUILT IN ───
    // 6j/6k/6l/6m/6n all measure one walk FRAME's quantities: 6m splits the plan
    // frame from the measured frame inside a single tick, so the sole's delivery
    // is closed. The step TARGET is a different thing: it is built ONCE, on the
    // freeze, and then delivered over a whole swing while the body pitches. Every
    // column published so far says what the target is worth NOW (capturePlanTargetX,
    // captureSwingAskX) and what the body is doing NOW (pelvisTilt), and nothing says
    // what frame the target was BUILT in — so the 0.5303 -> 0.7839 m jump of the
    // target, delivered at a measured -37.6 deg, cannot be split into "the plan moved
    // it" from "the body left the plan's frame". Published first, before any number
    // moved (the walk lane's own law since 6j). 0 is the "walker not running"
    // sentinel (reset in ResetCaptureWalk), never NaN, and nothing in the control
    // math reads this column back: it is the frame the 6o bound below is derived in.
    private float captureLastPlanFrameTiltDeg;
    // capturePlanFrameTiltDeg — the MEASURED pelvis tilt at the freeze that built
    //    the plan in force, deg, world frame, same convention and sign as the
    //    recorder's pelvisTilt / captureSwingAppliedFrameAngleDeg (minus is
    //    forward), read from the same estimator the freeze's own tilt seed reads.
    //    Held while that plan is in force; the reader's subtraction is
    //      pelvisTilt - capturePlanFrameTiltDeg
    //    = how far the body has pitched since the target was built, i.e. the
    //    frame the target was NOT planned in (negative on the row = the body went
    //    forward of its plan). On the frames the ask is solved the walk's own plan
    //    tilt is also published as captureSwingSolveFrameAngleDeg; this column is
    //    the other half of that split — the frame the plan started from, not the
    //    plan's target lean. 0 = walker off / no freeze yet, never NaN.
    public float capturePlanFrameTiltDeg => captureLastPlanFrameTiltDeg;

    // ─── WALK CYCLE 6p instrumentation: THE POSTURE ANSWER SPENT ON THE
    //     DELIVERED COMMAND ───
    // 6o measured the split (pelvisTilt - capturePlanFrameTiltDeg) and paid it out
    // of the swing hip's FLEXION allowance — a charge on the stepping leg. What
    // the delivered hip command does with the posture ITSELF was not published:
    // the reflex is readable (captureSwingHipDemandDeg, the pre-bound demand) but
    // the bounded restoring answer the command carries was not. Published first,
    // before any number moved (the walk lane's own law since 6j). 0 is the
    // "walker not running" sentinel (reset in ResetCaptureWalk), never NaN, and
    // nothing in the control math reads this column back.
    private float captureLastSwingPostureBiasDeg;
    // captureSwingPostureBiasDeg — the bounded posture error the swing hip's
    //    DELIVERED command carried this frame, deg of hip joint angle, positive =
    //    the ask sits on the EXTENSION side of the pose the IK returned (the
    //    direction that pitches the pelvis BACK, i.e. the answer to a forward
    //    fold), 0 = no answer. Its bound is the walker's own loaded-hip heel band
    //    expressed in degrees (captureStanceHipTorqueCap 45 N*m through
    //    ActuatorMixer.HipTorqueBandDeg at hipMuscleTorque 100 / hipPGain 1.5 /
    //    errorReferenceDegrees 10 = 3.0 deg): the same bounded posture error the
    //    loaded hip's ask is capped by, delivered by the one joint whose reaction
    //    the pelvis feels with no sole to roll. Held like the 6j-6o set (the last
    //    value a delivered command carried). 0 while the walker is not running or
    //    with no positive plan-frame drift, never NaN.
    public float captureSwingPostureBiasDeg => captureLastSwingPostureBiasDeg;

    // ─── WALK CYCLE 6q recording: WHICH PATH RAN ───
    // 6j..6p published what the level 1 walker delivered, frame by frame. This
    // cycle added a SECOND path (the level 2 pose library, behind -poseDrive 1,
    // default off) that writes the same six leg joints, so a row has to say
    // which of the two produced it or the two cannot be told apart downstream.
    // Name, units, frame, sentinel: `captureWalkPath`, no units (a code), no
    // frame, 0 = the level 1 path (the capture walker and every layer before it,
    // and the sentinel reset in ResetCaptureWalk), 1 = the level 2 pose library
    // held the joints on this row. Recorder only: no gain, limit, threshold or
    // branch in the control math reads it, and nothing writes it but
    // PublishWalkPath below.
    public const int WalkPathLevel1 = 0;
    public const int WalkPathPoseLibrary = 1;
    private float captureLastWalkPath;
    public float captureWalkPath => captureLastWalkPath;

    // Single writer: BalanceController.FixedUpdate names the path it took on
    // this physics step, the way it names legReqLayer. Called once per step,
    // before the branch, so a row never carries the previous step's answer.
    public void PublishWalkPath(bool poseLibrary)
    {
        captureLastWalkPath = poseLibrary ? WalkPathPoseLibrary : WalkPathLevel1;
    }

    // ─── WALK-1 per-frame command tap (instrumentation only) ───
    // What the walker COMMANDED this frame, which drive method wrote it, and
    // how the gait machine got there. Nothing in the controller reads a field
    // of this struct: no gain, limit, threshold or sign depends on it, and the
    // codes below are frozen so an old CSV stays readable.
    // Owner codes, per joint and side, of the LAST writer this frame.
    public const int TapOwnerNone = 0;
    public const int TapOwnerStancePlant = 1;
    public const int TapOwnerHoldMeasured = 2;
    public const int TapOwnerHoldKnee = 3;
    public const int TapOwnerSwingKneeLift = 4;
    public const int TapOwnerSwingLeg = 5;
    public const int TapOwnerLandingHip = 6;
    public const int TapOwnerRestoreHip = 7;
    public const int TapOwnerLegLengthKnee = 8;
    public const int TapOwnerTrailHipHold = 9;
    public const int TapOwnerLandingKnee = 10;
    public const int TapOwnerDirtFoldAnkle = 11;
    public const int TapOwnerSwingAnkle = 12;
    public const int TapOwnerCopAnkle = 13;
    public const int TapOwnerZero = 14;
    // WALK-1 recorder completeness: the codes below name the LAYER that held a
    // joint on a frame the capture walker did not write it (the walker's own
    // drive-method codes 1..14 above stay the precise record while it runs).
    // They are append-only: 1..14 keep their meaning for every existing probe.
    public const int TapOwnerIdle = 15;          // no layer wrote it: no request
    public const int TapOwnerStanceLayer = 16;   // quiet stance / stance clock (WalkLift)
    public const int TapOwnerStandingPlant = 17; // TickStandingPlantRestore (Plant)
    public const int TapOwnerGetUp = 18;         // TickGetUp
    public const int TapOwnerFalling = 19;       // Fallen: RelaxAllMuscles, no drive
    public const int TapOwnerPoseDrive = 20;     // LEVEL 2 pose library
    public const int TapOwnerCaptureWalk = 21;   // the capture walker as a layer
    // KICK CYCLE k1: the leg strike (LegStrikeDrive, execution order 30). The
    // kicking leg's hip/knee/ankle while a stroke is in flight; 0 rows carry it.
    public const int TapOwnerKick = 22;

    // Abort codes: why TickCaptureWalk handed the joints back this frame.
    public const int TapAbortNone = 0;
    public const int TapAbortCollapsed = 1;
    public const int TapAbortPoseHold = 2;
    public const int TapAbortReturnPlant = 3;
    public const int TapAbortBothAir = 4;
    public const int TapAbortHeap = 5;
    public const int TapAbortTiltPast = 6;

    // Role codes for the tap: what the gait machine says each side is.
    public const int TapRoleStance = 1;
    public const int TapRoleSwing = 2;

    public struct CaptureWalkTap
    {
        public int tick;               // Time.frameCount of the last publish
        public int active;             // 1 = the walker ticked on that frame
        public long walkTicks;         // how many times the walker has ticked
        public int abortCode;          // why the tick handed the joints back
        public int ownerHipL, ownerHipR, ownerKneeL, ownerKneeR;
        public int ownerAnkleL, ownerAnkleR;
        // Requested TARGET ANGLE, degrees, per joint. NaN = this joint was
        // not commanded as an angle this frame (the CoP ankle loop is a
        // torque command, so it fills copCmd* instead).
        public float reqHipL, reqHipR, reqKneeL, reqKneeR, reqAnkleL, reqAnkleR;
        public float copCmdL, copCmdR;
        public int roleL, roleR;       // TapRoleStance / TapRoleSwing
        public int gaitState;          // (int)captureGait.State: 0 DS, 1 Swing,
                                       // 2 TouchdownBlend, 3 RecoveryStep
        public float gaitStateAge;     // captureGait.StateAge, seconds
        public int dualSupport;        // 1 = gait in DualSupport / TouchdownBlend
        public int swapReason;         // GaitStateMachine.Reason* of the last swap
        public int swapMode;           // 1 paired, 2 catchOwns
        public float swapAge;          // gait StateAge when the swap fired
        public int gaitSteps;          // GaitStateMachine.StepCount
        public int entryReason;        // this frame's phase-entry reason
        public int gateMask;           // first-step gate bits (GaitStateMachine.Gate*)
        public int rejectCode;         // why no step opened this frame
        public int planValid;          // the frozen footstep plan was accepted
        public int touchL, touchR;     // ContactSensor.Touching
        public int loadedL, loadedR;   // ContactSensor.Loaded
        public int supportL, supportR; // ContactSensor supporting / CaptureFootSupporting
    }

    public CaptureWalkTap walkTap;

    // Owner standing by to write the next hip / knee / ankle target. Set by
    // each drive method on entry, cleared every tick.
    private int tapWriterHip;
    private int tapWriterKnee;
    private int tapWriterAnkle;
    // Last World.collapsed / World.posed handed to GaitStateMachine.
    // TrialRecorder copies the first 3 s of walk into walkOnsetLog.
    public bool CaptureWalkCollapsed { get; private set; }
    public bool CaptureWalkPosed { get; private set; }
    public int CaptureOnsetLogCount { get; private set; }

    public const int CaptureOnsetLogCapacity = 31;
    private readonly float[] captureOnsetT = new float[CaptureOnsetLogCapacity];
    private readonly float[] captureOnsetComX = new float[CaptureOnsetLogCapacity];
    private readonly float[] captureOnsetMargin = new float[CaptureOnsetLogCapacity];
    private readonly byte[] captureOnsetFlags = new byte[CaptureOnsetLogCapacity];
    private readonly bool[] captureOnsetWritten = new bool[CaptureOnsetLogCapacity];
    // GETUP-1 / onset diagnostics: which standing term fired collapsed.
    // Preallocated; StepCaptureOnsetLog writes one bucket, no alloc.
    private readonly float[] captureOnsetHipDrop = new float[CaptureOnsetLogCapacity];
    private readonly float[] captureOnsetComDrop = new float[CaptureOnsetLogCapacity];
    private readonly byte[] captureOnsetColumn = new byte[CaptureOnsetLogCapacity];
    private readonly byte[] captureOnsetReason = new byte[CaptureOnsetLogCapacity];
    // Settled world hip-over-ground (sensors). Kinematic thigh+shin is
    // only the fallback before the first quiet measure — that fallback
    // is ~straight bars and false-positive collapsed when the quiet
    // knee already spends a few cm of chord.
    private float captureStandHipOverGround;
    private bool captureStandHipValid;
    private float captureLastHipDrop;
    private float captureLastComDrop;
    private bool captureLastColumnStanding = true;
    private byte captureLastCollapseReason;

    // Quiet two-foot measure. World Y, after BodyStateEstimator. Cached
    // joints only. Once valid it holds for the episode so a lean cannot
    // rewrite the bar down mid-onset.
    private void TryCaptureStandHipReference()
    {
        if (captureStandHipValid)
            return;
        if (bodyState == null || vestibularSystem == null)
            return;
        if (bodyState.sensorTick <= 0)
            return;
        if (!bodyState.leftFootGrounded || !bodyState.rightFootGrounded)
            return;
        if (Mathf.Abs(vestibularSystem.GetBodyTilt()) >= 12f)
            return;
        // Settled world sensors only — same bounce gate as CoM bar.
        if (Mathf.Abs(bodyState.comVelocity.y) > 0.12f)
            return;
        if (Mathf.Abs(bodyState.comVelocity.x) > 0.20f)
            return;
        ResolveLegLinkOnce();
        ResolveCaptureFeetOnce();
        float hl = CaptureHipPoint(true).y
            - CaptureGroundY(true, CaptureAnklePoint(true));
        float hr = CaptureHipPoint(false).y
            - CaptureGroundY(false, CaptureAnklePoint(false));
        float h = 0.5f * (hl + hr);
        if (h < 0.35f)
            return;
        captureStandHipOverGround = h;
        captureStandHipValid = true;
    }

    private bool CaptureFootTouching(bool left) => captureContact.Touching(left);

    // Dirt-lift clock in DualSupport. take = stance extend toward 0
    // (the long strut). Fold = swing-knee peel after the first-step
    // lean so the sole can leave the touching band. Fold has its own
    // age so a late gate cannot slam the clearance knee in one physics
    // step. 0.20 s is four activation time-constants at
    // muscleActivationSpeed 20.
    private const float CaptureDirtLiftSeconds = 0.24f;
    private const float CaptureDirtLiftDropM = 0.03f;
    private const float CaptureDirtFoldSeconds = 0.20f;
    private const float CaptureDirtFoldKneeMax = 70f;
    // In-place lift IK may need more than the 70° knee-only cap once
    // the hip is allowed to rotate to the lift pose. First Swing still
    // uses CaptureSwingKneeCapDeg (walk swingKneeFlex, not this 100):
    // 100–110° plus hip −90 is the 0.26 m sit. Stance strut stays at
    // CaptureFirstStanceKneeMaxDeg. 120° is the Recovery chair.
    private const float CaptureDirtFoldLiftKneeMax = 100f;
    // Hip flexion is minus. The hinge stop is −90; commanding that
    // stop on the first catch (p_auto 5.50 s) is a scissor, not a step.
    private const float CaptureSwingHipHoldMaxDeg = 15f;
    // First DualSupport: lean, then in-place lift IK of the trail
    // (X frozen, Y at clearance). Lean is CaptureOnsetLeanU (0.12 s).
    // Fold waits for that lean with the stance hip still up — not
    // captureLaunchLead (stacked soles never roll 3 cm: burn_walk
    // 0.0668 m / swap 0) and not captureDoubleSupportMax (loaded-trail
    // chair / 6.300 s). Hip-hold + knee-only never left the slab.
    // Later steps fold as soon as the trail is not carrying.
    private const float CaptureDirtOnsetLeanSeconds = 0.12f;
    private const float CaptureDirtOnsetLeanReady = 0.85f;
    // DualSupport abort into a standing Plant while onset lean has not
    // yet moved the trunk. 0.15 of CaptureOnsetLeanU is ~30 ms / ~1.4°
    // of the 9° command — still inside TiltSettled 12°. One physics
    // frame (5 ms) missed a tipped BeginWalk whose Headless Tick is
    // after physics. Waiting until CaptureDirtOnsetLeanReady (0.85)
    // re-tested 12° on that lean and killed geo-lift.
    private const float CaptureDirtOnsetPlantHoldU = 0.15f;
    private const float CaptureDirtOnsetFoldSeconds = 0.18f;
    // WALK CYCLE 6d — shortest air time a step may be given. The peel/fold
    // clock is 0.20 s, c6c_walk's first swing (peel + plant) took 0.24 s and
    // the emergency step is captureRecoveryDuration 0.22 s, so 0.16 s is under
    // anything this body has ever put on the slab: the floor is a backstop for
    // a runaway speed, not a working number (the c6c_walk peak 0.73 m/s asks
    // for 0.196 s, and 0.16 s is reached only past 0.89 m/s).
    private const float CaptureSwingDurationFloorSeconds = 0.16f;
    // First-step stance strut. Both knees to 120 was plant-IK on a
    // dropping hip. Onset may flex only the swing; this cap is the
    // 15° / 3 cm pelvis budget (quiet ~6° plus a few degrees of sag).
    private const float CaptureFirstStanceKneeMaxDeg = 15f;

    // ─── WALK CYCLE 6n: the flexion allowance in the DELIVERED frame ───
    // The number CaptureClampSwingHipDeg above returns is calibrated in the
    // PLAN's frame, and it was picked when the delivered command still carried
    // that frame: written against CapturePelvisWorldTarget (late-Swing −10..−3
    // deg) a hip command IS the thigh's world tilt, so "−40 (or −65 on a long
    // catch) of flexion" named a world pose. Since 6m the same command is
    // written against the MEASURED parent, and the same sole then needs the
    // plan-frame number PLUS the sag between the two frames: c6m_walk's plant
    // puts the ask at −35.75 deg in the plan frame and ~70.6 deg of real
    // flexion in the measured one, which the 65 deg floor refuses (reqHipL
    // pegged 65.000 to the fall). This is the same bound re-derived in the
    // frame the ask is delivered in:
    //     worldThighBound = planTilt − planFrameFloor   (the pose the number
    //                                                    named, world space)
    //     floor           = frameDeg − worldThighBound  (that same pose, in the
    //                                                    delivered frame)
    // and never below the joint's OWN stop less one margin. That last term is
    // the only part of the allowance a frame convention cannot move: the hinge
    // limit is read from the hinge itself (Human.hipLimitMin into
    // joint.limits.min, −90), the mixer re-clamps every target to it
    // (ActuatorMixer line 85), and "SPD to −90 / 110° is the first-transfer
    // chair" — so the allowance stops one margin short of the stop instead of
    // asking for it. The caller spends this number on the ASK, before the
    // solve (see SwingPlacementWithinFlexion), and writes no hip command below
    // it, so an ask inside the allowance is a command no clamp can shorten.
    private const float CaptureSwingHipStopMarginDeg = 5f;

    // ─── WALK CYCLE 6o: the plan-frame drift as a bound on the delivered ask ───
    // The 6n allowance above says how much flexion this LEG can deliver in the
    // frame the ask is handed over in. It says nothing about how much flexion the
    // BODY can afford: the swing hip's flexion is a forward pitch on the pelvis
    // the leg hangs from (6f), and the walk is measured dying of exactly that —
    // c6n_walk, the target frozen at 7.120 asks for a 0.62 m step (−85 deg of
    // flexion, the allowance floor, appTorqueHipL +90.7 N*m) with the body
    // already −57 deg and the pelvis nowhere to put the reaction. So the frame
    // the plan target was BUILT in is spent here: the drift between that frame
    // (capturePlanFrameTiltDeg, the pelvis tilt on the freeze) and the frame the
    // target LANDS in (the measured pelvis the command is written on) is removed
    // from the flexion the ask may carry. One degree of drift buys one degree less
    // fold — the frame difference is paid in the ask's X, which is the only
    // coordinate the swing sole can give up (6n's law: retract X, never lift Y).
    //
    // Bounded three ways, and it never loosens: a body that pitched BACKWARD does
    // not earn itself extra flexion (drift <= 0 returns the 6n allowance
    // untouched), the drift paid is capped at CaptureSwingPlanFrameBoundDeg, and
    // the result may not ask the hip for extension past its own hold ceiling
    // (CaptureSwingHipHoldMaxDeg). The demand this bound refuses is NOT hidden:
    // captureSwingHipDemandDeg stays the PRE-bound demand of the point handed to
    // the solve, and the ask stays inside the bound before the solve, so
    // solved == applied == ask (the 6l/6m/6n criterion) is untouched.
    //
    // 25 deg is 0.4 of the c6n_walk measured drift at the fall (−58.2 deg body
    // against a plan frozen near −18) and is the walker's own bound, not a
    // physical one: past it the bound stops tightening so a deeply pitched body
    // still gets a fold to plant the sole with instead of the plan's full reach.
    private const float CaptureSwingPlanFrameBoundDeg = 25f;

    // ─── WALK CYCLE 6: the swing reach disc, one rule for the placement ───
    //
    // A swing land point is a point only if the leg can put a sole on it:
    // |target − hip| <= bars − reserve, the disc about the CURRENT hip.
    // The reserve is the KNEE's authority, not the solver's edge. SolveCaptureLegIk
    // clamps its chord at bars − 0.01 = 0.85 m, which on these two 0.43 m bars
    // is a knee of 17.49 deg — the singular pose the solver itself refuses to
    // enter, and a leg at that angle cannot press a sole onto the slab: it can
    // only hang. auto_walk_c6 is that pose, held for the whole swing: reqHipL
    // pinned −65.00 in 125 frames, reqKneeL pinned 17.4920 in 127, soleClearL
    // 0.180 m, fzL 0.00 N, no swap. bars − 0.02 = 0.84 m is 24.8 deg of knee
    // at the far edge — still an ordinary near-straight landing leg, and the
    // point the solver's own clamp then never has to touch, so its saturation
    // witness (CaptureIkSaturated) stays 0 on the walk path.
    private const float CaptureSwingReachReserveM = 0.02f;

    // Slab height under one sole. The ankle hinge sits at plate mid-height
    // (Human.CreateLeg), not on the top edge: airborne ground is half a
    // plate below the hinge, not a full size.y. Using the old hang-below
    // arithmetic after that move commanded land Y ~4.5 cm into the slab.
    private float CaptureGroundY(bool left, Vector2 anklePoint)
    {
        // A loaded sole IS the slab, so measure its lowest corner and not its
        // hinge. The hinge sits on the top edge of the foot box, 0.065 m from
        // the heel end, so a plantarflexed stance foot — and the CoP loop keeps
        // this one near full plantarflexion while the body travels — carries
        // that hinge 2 to 3 cm HIGHER than a flat foot would. Reading the ground
        // off the hinge then lifts the swing target by the same 2 to 3 cm,
        // which is more than captureFootPress: the swing sole is commanded to a
        // point above the real slab and touchdown is never confirmed. bounds is
        // the world AABB, and for a rotated box its min.y is exactly the lowest
        // corner, which is what the contact sees.
        if (CaptureFootTouching(left))
        {
            float y = float.PositiveInfinity;
            BoxCollider2D heel = CaptureFootBox(left);
            BoxCollider2D toe = CaptureToeBox(left);
            if (heel != null) y = Mathf.Min(y, heel.bounds.min.y);
            if (toe != null) y = Mathf.Min(y, toe.bounds.min.y);
            if (y < float.PositiveInfinity) return y;
        }
        float clearance = 0f;
        if (bodyState != null)
        {
            clearance = Mathf.Max(0f, left
                ? bodyState.leftFootGroundClearance
                : bodyState.rightFootGroundClearance);
        }
        return anklePoint.y - CaptureAnkleHeightOnSlab() - clearance;
    }

    // Same lazy moment as StanceToeReach: the builder sizes the box after
    // AddComponent, so Awake cannot read it.
    // Ankle hinge is at plate mid-height. A sole on the slab puts that
    // hinge half a plate above bounds.min.y, not a full size.y above it.
    private float CaptureAnkleHeightOnSlab()
    {
        return 0.5f * CaptureSoleThickness();
    }

    private float CaptureSoleThickness()
    {
        ResolveCaptureFeetOnce();
        return captureSoleThickness;
    }

    private BoxCollider2D CaptureFootBox(bool left)
    {
        ResolveCaptureFeetOnce();
        return left ? captureLeftFootBox : captureRightFootBox;
    }

    private BoxCollider2D CaptureToeBox(bool left)
    {
        ResolveCaptureFeetOnce();
        return left ? captureLeftToeBox : captureRightToeBox;
    }

    private void ResolveCaptureFeetOnce()
    {
        if (captureSoleResolved) return;
        captureSoleResolved = true;
        captureSoleThickness = 0.07f;
        // The ankle hinge lives on the heel (Human.CreateLeg), so this is the
        // heel box. The toe is a sibling hinge on Left/RightLegToe.
        if (leftAnkleJoint != null)
            captureLeftFootBox = leftAnkleJoint.GetComponent<BoxCollider2D>();
        if (rightAnkleJoint != null)
            captureRightFootBox = rightAnkleJoint.GetComponent<BoxCollider2D>();
        if (leftToeJoint != null)
            captureLeftToeBox = leftToeJoint.GetComponent<BoxCollider2D>();
        if (rightToeJoint != null)
            captureRightToeBox = rightToeJoint.GetComponent<BoxCollider2D>();
        BoxCollider2D box = captureLeftFootBox != null
            ? captureLeftFootBox
            : captureRightFootBox;
        if (box != null && box.size.y > 0.001f)
            captureSoleThickness = box.size.y;
    }

    private Vector2 CaptureAnklePoint(bool left)
    {
        HingeJoint2D joint = left ? leftAnkleJoint : rightAnkleJoint;
        if (joint == null)
        {
            if (bodyState != null)
                return left ? bodyState.leftFootPosition : bodyState.rightFootPosition;
            return transform.position;
        }
        return joint.transform.TransformPoint(joint.anchor);
    }

    private Vector2 CaptureHipPoint(bool left)
    {
        HingeJoint2D joint = left ? leftHipJoint : rightHipJoint;
        if (joint == null) return transform.position;
        return joint.transform.TransformPoint(joint.anchor);
    }

    public bool TryGetCaptureOnsetSample(int index, out float age,
        out bool collapsed, out bool posed, out float comX, out float margin,
        out float hipDrop, out float comDrop, out bool columnStanding,
        out byte reason)
    {
        age = 0f;
        collapsed = false;
        posed = false;
        comX = 0f;
        margin = 0f;
        hipDrop = 0f;
        comDrop = 0f;
        columnStanding = true;
        reason = 0;
        if (index < 0 || index >= CaptureOnsetLogCapacity || !captureOnsetWritten[index])
            return false;
        age = captureOnsetT[index];
        collapsed = (captureOnsetFlags[index] & 1) != 0;
        posed = (captureOnsetFlags[index] & 2) != 0;
        comX = captureOnsetComX[index];
        margin = captureOnsetMargin[index];
        hipDrop = captureOnsetHipDrop[index];
        comDrop = captureOnsetComDrop[index];
        columnStanding = captureOnsetColumn[index] != 0;
        reason = captureOnsetReason[index];
        return true;
    }

}
