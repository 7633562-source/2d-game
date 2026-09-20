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
    private float captureSwingAge => captureGait.SwingAge;
    private bool captureSwingLeftGround => captureGait.SwingLeftGround;
    private int captureStanceSign => captureGait.StanceFoot;
    private int captureAbandonStreak => captureGait.AbandonStreak;
    private int captureStepCount => captureGait.StepCount;
    private float captureBothDownAge => captureGait.BothDownAge;
    private int captureBothAirSteps => captureGait.BothAirSteps;
    private int captureWatchdogCount => captureGait.WatchdogCount;
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
    public int CaptureWalkStepCount => captureWalkStepAcc + captureStepCount + poseDriveStepCount;
    public int CaptureWalkBothAirSteps => captureBothAirSteps;
    public int CaptureWalkWatchdogCount => captureWatchdogCount;
    public int CaptureStanceFootSign => captureStanceSign >= 0 ? 1 : -1;
    public int CaptureSwingFootSign => -CaptureStanceFootSign;
    public float CaptureWalkCapturePointX => captureLastCapturePointX;
    public float CaptureWalkFootTargetX => capturePlan.valid ? capturePlan.targetX : 0f;
    public float CaptureWalkBStar => capturePlan.valid ? capturePlan.bStar : 0f;
    public float CaptureWalkOmega0 => captureLastOmega0;

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
    public bool CaptureWalkRecovering => captureState == CaptureWalkState.RecoveryStep;
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

    // Standing Plant: every grounded hip still sits on a column.
    // slackMeters is the same plantComLeadMax (0.10 m) the Plant→Fall
    // standing band uses: a quiet knee (~6°, a few mm of chord) passes;
    // a 0.20 m chair does not. Measured settled hip-over-ground is the
    // bar when known; kinematic bars only before that measure.
    // captureCollapseDrop (0.30 m) is only the first-DS abort.
    public bool CaptureColumnIsStanding(float slackMeters)
    {
        ResolveLegLinkOnce();
        if (leftHipJoint == null || rightHipJoint == null || bodyState == null)
            return true;
        float slack = Mathf.Max(0.05f, slackMeters);
        if (captureCollapseDrop > 0.05f)
            slack = Mathf.Min(slack, captureCollapseDrop);
        float bars = CaptureStandingHipBars();
        if (bars < 0.1f)
            return true;
        float minLen = bars - slack;
        if (bodyState.leftFootGrounded && !HipColumnStanding(true, minLen))
            return false;
        if (bodyState.rightFootGrounded && !HipColumnStanding(false, minLen))
            return false;
        return true;
    }

    // Settled world hip-over-sole. Sensors after tilt settles; not the
    // geometric thigh+shin sum until the first quiet sample exists.
    private float CaptureStandingHipBars()
    {
        if (captureStandHipValid && captureStandHipOverGround > 0.1f)
            return captureStandHipOverGround;
        ResolveLegLinkOnce();
        return legThighLength + legShinLength;
    }

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

    // Collapse terms for gait + onset log. reason: 0 none, 1 hip, 2 com,
    // 3 column (first matching term — which bar is wrong).
    private void EvaluateCaptureCollapseTerms(bool stanceIsLeft, float groundY,
        out bool hipCollapsed, out float hipDrop, out float comDrop,
        out bool columnStanding, out byte reason)
    {
        TryCaptureStandHipReference();
        float bars = CaptureStandingHipBars();
        float hipLen = CaptureHipPoint(stanceIsLeft).y - groundY;
        hipDrop = bars - hipLen;
        hipCollapsed = bars > 0.1f
            && hipDrop >= Mathf.Max(0.05f, captureCollapseDrop);

        comDrop = stepPhaseDriver != null
            ? stepPhaseDriver.StandingComDropMeters
            : 0f;
        bool comCollapsed = stepPhaseDriver != null
            && stepPhaseDriver.BodyCollapsed;

        float colSlack = stepPhaseDriver != null
            ? Mathf.Max(0.05f, stepPhaseDriver.plantComLeadMax)
            : 0.10f;
        columnStanding = CaptureColumnIsStanding(colSlack);
        bool colCollapsed = !columnStanding;

        reason = 0;
        if (hipCollapsed)
            reason = 1;
        else if (comCollapsed)
            reason = 2;
        else if (colCollapsed)
            reason = 3;

        captureLastHipDrop = hipDrop;
        captureLastComDrop = comDrop;
        captureLastColumnStanding = columnStanding;
        captureLastCollapseReason = reason;
    }

    private bool HipColumnStanding(bool left, float minLen)
    {
        HingeJoint2D hip = left ? leftHipJoint : rightHipJoint;
        if (hip == null)
            return false;
        float hipY = hip.transform.TransformPoint(hip.anchor).y;
        float footY = left ? bodyState.leftFootPosition.y : bodyState.rightFootPosition.y;
        return hipY - footY >= minLen;
    }

    // The one gate. Flag off, or walk not requested, and this file is dead
    // code: FixedUpdate falls through to the legacy path unchanged.
    private bool CaptureWalkActive()
    {
        if (locomotionState == LocomotionState.GettingUp
            || locomotionState == LocomotionState.Fallen)
        {
            if (captureWalkWasActive)
                ResetCaptureWalk();
            return false;
        }
        bool on = useCaptureWalker != 0
                  && stepPhaseDriver != null && stepPhaseDriver.walkActive;
        if (!on)
        {
            if (captureWalkWasActive)
                ResetCaptureWalk();
            return false;
        }
        // StandingRestore is the standing Plant (tipped BeginWalk or
        // a mid-walk heap), not the quiet launch. BeginWalk / Tick
        // already raised or cleared restore on TiltSettled + signed
        // CoM-to-stance (not the column test — that froze a quiet 6°
        // knee). Onset is DualSupport lean + one swing knee.
        if (stepPhaseDriver.StandingRestore || stepPhaseDriver.StandingSurrender)
        {
            // Quiet two-foot Plant — Gait never built World this step.
            // Still fill onset diagnostics so a false BodyCollapsed
            // (wrong standing bar) shows hipDrop / comDrop / column /
            // reason. posed stays 0 until DualSupport writes.
            TryCaptureStandHipReference();
            bool stanceLeft = true;
            if (bodyState != null)
            {
                if (bodyState.rightFootGrounded && !bodyState.leftFootGrounded)
                    stanceLeft = false;
                else if (bodyState.leftFootGrounded && bodyState.rightFootGrounded)
                    stanceLeft = captureStanceSign >= 0;
            }
            ResolveCaptureFeetOnce();
            Vector2 ankle = CaptureAnklePoint(stanceLeft);
            float groundY = CaptureGroundY(stanceLeft, ankle);
            EvaluateCaptureCollapseTerms(stanceLeft, groundY,
                out bool hipCol, out _, out _, out _, out _);
            // Column stays in the onset reason code only (see Tick).
            CaptureWalkCollapsed = hipCol || stepPhaseDriver.BodyCollapsed;
            CaptureWalkPosed = false;
            StepCaptureOnsetLog();
            if (captureWalkWasActive)
                ResetCaptureWalk();
            return false;
        }
        if (!captureWalkWasActive)
        {
            captureWalkWasActive = true;
            // Freeze the pose walk began in: the ramp leaves from what the
            // body measures, not from a constant. Standing hip bar must
            // exist before the first collapsed test (settled world Y).
            TryCaptureStandHipReference();
            captureEntryAge = 0f;
            captureEntryBlend = 0f;
            captureLaunchAge = 0f;
            captureEntryKneeDeg = MeasuredStanceKneeDeg();
            EnterCaptureDoubleSupport(PickCaptureStanceSign());
            // A latch or a support sign left by a legacy walk would be a
            // second opinion about which leg is which.
            swingHipLatched = false;
            swingAirHoldRemain = 0f;
            swingAirStreak = 0f;
            heldStandCmd = 0f;
        }
        return true;
    }

    private void ResetCaptureWalk()
    {
        captureWalkWasActive = false;
        capturePlan.valid = false;
        captureWalkStepAcc += captureGait.StepCount;
        captureGait.ResetPhase();
        captureEntryAge = 0f;
        captureEntryBlend = 0f;
        captureLaunchAge = 0f;
        captureEntryKneeDeg = float.NaN;
        captureDirtLiftAge = -1f;
        ClearDirtFoldLatch();
        captureLaunchComX = float.NaN;
        captureLaunchHipY = float.NaN;
        captureLeftSupportKneeHeld = false;
        captureRightSupportKneeHeld = false;
        captureStanceAnchorValid = false;
        captureAirborneAge = 0f;
        // 6j: the delivered-geometry instruments have no value while the walker
        // is off; 0 is the sentinel the recorder writes, never NaN.
        captureLastSwingAskX = 0f;
        captureLastSwingSoleX = 0f;
        captureLastSwingTrackAge = 0f;
        captureLastPlanTargetX = 0f;
        // 6k: the solver's-own-answer instruments, same sentinel law: 0 while
        // the walker is off, never NaN.
        captureLastSwingSolvedSoleX = 0f;
        captureLastSwingSolvedResidualX = 0f;
        captureLastSwingSolveFrameX = 0f;
        captureLastSwingSolveFrameAngleDeg = 0f;
        // 6l: the applied-pose instruments, same sentinel law: 0 while the
        // walker is off, never NaN.
        captureLastSwingAppliedSoleX = 0f;
        captureLastSwingAppliedResidualX = 0f;
        captureLastSwingAppliedFrameX = 0f;
        captureLastSwingAppliedFrameAngleDeg = 0f;
        // 6n: the ask's own Y and the pre-bound flexion demand. Same sentinel
        // law as the 6j/6k/6l sets above: 0 while the walker is off, never NaN.
        captureLastSwingAskY = 0f;
        captureLastSwingHipDemandDeg = 0f;
        // 6o: the frame the plan in force was built in. Same sentinel law: 0
        // while the walker is off, never NaN.
        captureLastPlanFrameTiltDeg = 0f;
        // 6p: the bounded posture answer the delivered swing command carried.
        // Same sentinel law: 0 while the walker is off, never NaN.
        captureLastSwingPostureBiasDeg = 0f;
        // 6q: which path held the joints. 0 is the level 1 sentinel, so a
        // reset row reads as "the old path" and never as the pose library.
        captureLastWalkPath = WalkPathLevel1;
        ApplySwingLegFriction(0f, false, 0f);
    }

    // Heap restore only. Call AFTER GaitStateMachine.Apply. A same-step
    // freeze to Swing is a step, not a restore.
    //
    // After the first plant CoM sits ahead of the new stance — that is
    // the walking orbit (xi past the ankle, residual speed). Judging
    // that DualSupport with the quiet standing band (TiltSettled 12° /
    // StanceLead vs plantComLeadMax 0.10 m / |offset| vs fallCoMOffset
    // 0.30) aborted the transfer: StandingRestore handed the quiet
    // automaton the joints, UpdateState named Falling at 6.300 s
    // (walk start +1.30, p_capds swap 2) and RelaxAllMuscles dropped
    // the catch. Onset DualSupport (stepCount 0) still never restores
    // from lead — firstHeap already returned a launch squat. Collapse
    // (hip / com / column — CaptureWalkCollapsed) is the chair.
    // Cached refs only.
    private bool CaptureWalkShouldReturnToStandingPlant(bool collapsed)
    {
        if (stepPhaseDriver == null || stepPhaseDriver.walkRecoverAnyPose <= 0.5f)
            return false;
        if (captureState != CaptureWalkState.DoubleSupport)
            return false;
        if (captureStepCount == 0)
            return false;
        return collapsed;
    }

    // Same standing Plant BeginWalk already uses: TiltSettled (12° /
    // 40 °/s) + signed CoM-to-stance ≤ plantComLeadMax, then the walker
    // starts again. plantComLeadMax / TiltSettled stay the happy-path
    // numbers. No Find / GetComponent.
    private void ReturnCaptureWalkToStandingPlant()
    {
        if (stepPhaseDriver != null)
            stepPhaseDriver.RequestStandingPlant();
        // DualSupport dump is a failed Plant, not Falling. Quiet stance
        // owns the joints this same FixedUpdate (CaptureWalkActive falls
        // through). Tick waits on TiltSettled + signed CoM-to-stance,
        // then walk starts. Not the old column test: that froze a quiet
        // 6° knee (phase 0 / 35 s).
        if (currentState == BalanceState.Falling)
            currentState = BalanceState.Recovery;
        ResetCaptureWalk();
    }

    // First pick only: stance is the LEADING sole, so the trailing leg is the
    // one that steps forward. Same geometric rule the legacy
    // PickStanceFromFeet follows. After a landing the roles are handed over by
    // the plan, not re-derived from geometry.
    private int PickCaptureStanceSign()
    {
        float dir = CaptureWalkDir();
        float lead = (CaptureFootCenterX(false) - CaptureFootCenterX(true)) * dir;
        return lead > 0f ? 1 : -1;
    }

    private float CaptureWalkDir()
    {
        if (intent != null && Mathf.Abs(intent.moveX) > 0.01f)
            return Mathf.Sign(intent.moveX);
        return 1f;
    }

    // Roles live on GaitStateMachine. Contact facts live on ContactSensor.
    // Drive loaded = role MayCarry AND touching: a swing toe scrape is not
    // a second support.
    private bool CaptureIsDualSupport => captureGait.IsDualSupport;

    private bool CaptureIsStanceFoot(bool left) => captureGait.IsStanceFoot(left);

    private bool CaptureIsSwingFoot(bool left) => captureGait.IsSwingFoot(left);

    private bool CaptureFootTouching(bool left) => captureContact.Touching(left);

    // Caudal heel pin, not the filled heel AABB. A swing toe scrape is
    // not this. Waiting for estimator Heel never froze DualSupport
    // (burn_walk walkSwap 0 / swingFoot 0.955).
    private bool CaptureHeelGrounded(bool left) => captureContact.HeelPin(left);

    // WALK CYCLE 6: the swing foot of the roles in force, whichever side it is.
    // The hand-back (TapAbortTiltPast) is gated on this: a bar crossed while the
    // swing sole is still in the air is a placement order, not a surrender.
    private bool CaptureSwingSoleAirborne()
    {
        return !CaptureFootTouching(!captureGait.IsStanceLeft);
    }

    private bool CaptureFootSupporting(bool left) => captureContact.Supporting(left);

    private bool CaptureFootLoaded(bool left)
    {
        return captureGait.MayCarry(left) && captureContact.Touching(left);
    }

    private float CaptureDesiredSpeed()
    {
        float v = Mathf.Abs(captureTargetSpeed);
        if (intent != null && intent.run > 0.5f)
            v *= Mathf.Max(1f, captureRunSpeedScale);
        return v;
    }

    // Smoothstep 0…1 of the CoP ankle-cap clock (captureLaunchSeconds).
    // Not the onset lean: ramping 9°/10° on this 0.70 s clock left the
    // trunk quiet while DualSupport sat in phase 0 (comX 0.07 m).
    private float CaptureLaunchU()
    {
        float t = Mathf.Max(0.05f, captureLaunchSeconds);
        float u = Mathf.Clamp01(captureLaunchAge / t);
        return u * u * (3f - 2f * u);
    }

    // Onset lean, then one swing knee. 0.12 s is 24 physics steps: enough
    // for the 9°/10° chest/pelvis targets to unload the trailer without
    // the 0.70 s CoP-cap wait that froze the gait. Smoothstep.
    private float CaptureOnsetLeanU()
    {
        float t = Mathf.Max(0.05f, CaptureDirtOnsetLeanSeconds);
        float u = Mathf.Clamp01(captureLaunchAge / t);
        return u * u * (3f - 2f * u);
    }

    // First DualSupport CoP setpoint (STANCE sole only). Quiet CoM
    // already sits on stanceCentre + captureLaunchLead (~0.07 m), so
    // that target was P-error 0. Seek launch ComX + lead so P can
    // actually roll. Trail CoP on the same point is a second pin —
    // do not call this for the swing foot. Trail hip is a latched
    // hold from DualSupport frame 0, not a Zero shear.
    private float CaptureOnsetCopTargetX(float dir)
    {
        float lead = Mathf.Max(0f, captureLaunchLead);
        bool stanceIsLeft = captureGait.IsStanceLeft;
        float fromStance = CaptureFootCenterX(stanceIsLeft) + lead * dir;
        if (float.IsNaN(captureLaunchComX))
            return fromStance;
        float fromLaunch = captureLaunchComX + lead * dir;
        return (fromLaunch - fromStance) * dir >= 0f ? fromLaunch : fromStance;
    }

    // Whole walker: state transitions first, then one write per joint.
    // comOffset is the quiet mid-support reading FixedUpdate already
    // computed. Do not feed UpdateState here: stance-only or mid-feet
    // past fallCoMOffset 0.30 is the 6.300 s dump of a live step.
    private void TickCaptureWalk(float comOffset)
    {
        _ = comOffset;
        // WALK-1: fresh tap. Every owner starts at None, so a joint no drive
        // method writes this frame is visibly unowned instead of stale.
        walkTap.walkTicks++;
        walkTap.tick = Time.frameCount;
        walkTap.active = 1;
        walkTap.abortCode = TapAbortNone;
        walkTap.ownerHipL = walkTap.ownerHipR = TapOwnerNone;
        walkTap.ownerKneeL = walkTap.ownerKneeR = TapOwnerNone;
        walkTap.ownerAnkleL = walkTap.ownerAnkleR = TapOwnerNone;
        walkTap.reqHipL = walkTap.reqHipR = float.NaN;
        walkTap.reqKneeL = walkTap.reqKneeR = float.NaN;
        walkTap.reqAnkleL = walkTap.reqAnkleR = float.NaN;
        walkTap.copCmdL = walkTap.copCmdR = float.NaN;
        tapWriterHip = TapOwnerNone;
        tapWriterKnee = TapOwnerNone;
        tapWriterAnkle = TapOwnerNone;
        float dt = Time.fixedDeltaTime;
        ResolveLegLinkOnce();
        captureMixer.activationSpeed = muscleActivationSpeed;
        captureMixer.speedReference = speedReferenceDegPerSec;
        captureLaunchAge += dt;
        // Onset lean is CaptureOnsetLeanU (0.12 s), not CaptureLaunchU
        // (0.70 s CoP cap). Holding 9°/10° on the cap clock left DualSupport
        // in phase 0: both soles down, swap 0, comX 0.07 m. Before that
        // launch ramp the same trial travelled ~1.2 m. Knee chord follows
        // this lean; CoP still uses CaptureLaunchU inside DriveCaptureCop.
        captureEntryAge = captureLaunchAge;
        captureEntryBlend = CaptureOnsetLeanU();

        bool firstDs = captureStepCount == 0
                       && captureState == CaptureWalkState.DoubleSupport;

        // BodyStateEstimator (−100) already wrote this step's CoM.
        // Reading the estimator is the proof the gait uses the live
        // sensor, not a spawn snapshot. Same cache as GetCenterOfMass.
        Vector2 com = CaptureLiveCom();
        if (float.IsNaN(captureLaunchComX))
        {
            captureLaunchComX = com.x;
            captureLaunchHipY = CaptureHipPoint(captureGait.IsStanceLeft).y;
        }
        float comVelX = WalkComVelocityX;
        float dir = CaptureWalkDir();

        ResolveCaptureFeetOnce();
        captureContact.Sample(
            bodyState, com,
            captureLeftFootBox, captureLeftToeBox,
            captureRightFootBox, captureRightToeBox);

        bool stanceIsLeft = captureGait.IsStanceLeft;
        Vector2 stanceAnkle = CaptureAnklePoint(stanceIsLeft);
        Vector2 swingAnkle = CaptureAnklePoint(!stanceIsLeft);
        float groundY = CaptureGroundY(stanceIsLeft, stanceAnkle);
        // h is measured to the loaded sole, not to y = -2: the model must
        // keep working on a curb. The planner owns that reading.
        var live = capturePlanner.Measure(com.x, com.y, comVelX, groundY);
        float omega0 = live.omega0;
        captureLastOmega0 = live.omega0;
        captureLastCapturePointX = live.xi;

        EvaluateCaptureCollapseTerms(stanceIsLeft, groundY,
            out bool hipCollapsed, out _, out _, out _, out _);
        // Hip + settled CoM bar. Column is logged in reason (3) but
        // does not abort: plantComLeadMax slack (~0.10 m) is tighter
        // than captureCollapseDrop (0.30) and a 9° onset lean would
        // false-positive the first DualSupport.
        bool comCollapsed = stepPhaseDriver != null
            && stepPhaseDriver.BodyCollapsed;
        bool collapsed = hipCollapsed || comCollapsed;
        // posed is the onset lean clock, not CoM travel. Lean tests
        // moved torsoTilt to −14° while comX stayed 0.069 m and
        // supportMargin 0.14 — Gait waiting for CoM to leave the
        // polygon never froze Swing. firstDs posed once lean is
        // ready (or 0.18 s); later steps are posed after the first
        // plant. Heap is not posed.
        // firstPosed used to freeze Swing WHILE the trail still
        // touched: DriveCaptureSwingLeg then yanked a planted sole,
        // third-law sat the pelvis, UpdateState fallCoMOffset 0.30
        // named Falling at walk start +1.30 s (c125adbd 6.300,
        // auto1343 swap 0 / drop 0.87 / torso 90°). DualSupport
        // peels with the caudal rocker; Gait firstAir waits for
        // that pin to open. posed must not skip that air.
        // Caudal pin, not the filled heel AABB: 12° plantarflex never
        // emptied estimator Heel (burn_walk swap 0 / swingFoot 0.955).
        bool trailHeel = CaptureHeelGrounded(!stanceIsLeft);
        // After the first plant, freeze only when CoM has arrived on
        // the lead plate and the new trail heel is up. p_auto 17:32
        // froze the second Swing 35 ms after swap 5.645 (DS min 0.03)
        // while catchOwns had already lifted the rear: that airborne
        // trail then missed freeze-xi, plantMiss vetoed the sole, and
        // late opened Recovery at 6.095. 2 cm behind the lead centre
        // is still on the 0.26 m plate (captureCopLead 0.015).
        bool comOnLead = (com.x - CaptureFootCenterX(stanceIsLeft)) * dir
                         >= -0.02f;
        bool posed = firstDs
            ? !collapsed && !trailHeel
              && (CaptureOnsetLeanU() >= CaptureDirtOnsetLeanReady
                  || captureLaunchAge >= CaptureDirtOnsetFoldSeconds)
            : captureStepCount > 0 && !collapsed && !trailHeel && comOnLead;
        CaptureWalkCollapsed = collapsed;
        CaptureWalkPosed = posed;
        StepCaptureOnsetLog();
        // First stacked DS: a heap is a standing Plant, not a capture
        // launch. Later steps catch with Recovery. Restore before
        // Decide so emergency cannot steal a chair into RecoveryStep.
        // collapsed = hip | BodyCollapsed (settle bar). Column is log-only.
        // Do not test plantComLeadMax 0.10 here: a live dirt-peel is
        // ~8 cm of shared pelvis drop, and firstDs stays true until
        // the first freeze — that test would abort the trail-up.
        if (firstDs && collapsed)
        {
            walkTap.abortCode = TapAbortCollapsed;
            ReturnCaptureWalkToStandingPlant();
            return;
        }
        // Same standing band BeginWalk used (tilt 12° + signed
        // CoM-to-stance ≤ plantComLeadMax). DualSupport that still
        // opened on a tipped / over-led / sunk pose hands the joints
        // back — do not peel. Hold that abort until onset lean has
        // actually started (CaptureDirtOnsetPlantHoldU): Headless
        // Tick is after physics, so one 5 ms frame missed the heap.
        // Pose only, not omega: mixer start can spike 40 °/s on a
        // quiet 2° trunk. After that U the 9° command is the step;
        // re-testing 12° there killed geo-lift. Quiet start passes.
        if (firstDs
            && CaptureOnsetLeanU() < CaptureDirtOnsetPlantHoldU
            && stepPhaseDriver != null
            && stepPhaseDriver.walkRecoverAnyPose > 0.5f
            && !stepPhaseDriver.StandingPlantHoldReady)
        {
            walkTap.abortCode = TapAbortPoseHold;
            ReturnCaptureWalkToStandingPlant();
            return;
        }
        // After the hold, a SKATE (trail still down) that has already
        // lost the standing band is a heap: cycle-0 PlantComReady
        // 0.30 m and fallAbortDeg 30, not 12° / 0.10 m. Do not apply
        // that bound to a live first Swing. From a 9° rest bow
        // x0 ≈ h·sin 9° ≈ 0.13 m, e^{ωT} ≈ 2.4 at T = 0.26 s, so CoM
        // lead is already ~0.31 m — the same 0.30 UpdateState named
        // Falling at walk start +1.30 s (c125adbd 6.300). Aborting
        // that orbit is the symptom. p_auto slid because the trail
        // never left; a planted catch lives past 0.30 on purpose.
        if (firstDs
            && CaptureFootTouching(!stanceIsLeft)
            && stepPhaseDriver != null
            && bodyState != null)
        {
            float plantFootX = stanceIsLeft
                ? bodyState.leftFootPosition.x
                : bodyState.rightFootPosition.x;
            float plantLead = (com.x - plantFootX) * dir;
            if (stepPhaseDriver.StandingPlantHeap(plantLead))
            {
                walkTap.abortCode = TapAbortHeap;
                ReturnCaptureWalkToStandingPlant();
                return;
            }
        }
        // First DualSupport / Swing at the GETUP bar (45°) is a failed
        // peel, not GETUP-1. Happy-path 9° onset never sees this.
        // Tick / BeginWalk then wait TiltSettled 12° + signed
        // CoM-to-stance ≤ plantComLeadMax and start again. p_auto
        // 16:59: |offset| 0.30 then EnterGettingUp / walkPhase −1.
        if (captureStepCount == 0
            && stepPhaseDriver != null
            && stepPhaseDriver.walkRecoverAnyPose > 0.5f
            && vestibularSystem != null
            && Mathf.Abs(vestibularSystem.GetBodyTilt())
                >= Mathf.Max(20f, getUpFallenAbsTilt)
            // WALK CYCLE 6: the hand-back waits for the foot. While the swing
            // sole is still in the air this bar is a PLACEMENT order — the
            // swing reads the same number and drives the sole onto the slab
            // (DriveCaptureSwingLeg, landNow) — and handing the joints back
            // over a hovering foot is what put 1401 N = 2.02 W on the one sole
            // still on the slab at 6.00 s. Unchanged the moment the sole is
            // down: that is the failed peel this abort was written for.
            && !CaptureSwingSoleAirborne())
        {
            walkTap.abortCode = TapAbortTiltPast;
            ReturnCaptureWalkToStandingPlant();
            return;
        }

        float gaitShare = CaptureFrontShare(com.x, stanceAnkle.x, swingAnkle.x, dir);
        // StanceToeReach is centre→toe. Gait compares to the ANKLE, so
        // passing 0.13 m here put the "toe" 5 cm past the real plate
        // (ankle is ahead of centre). Emergency then waited while CoM
        // already rotated about the real toe — burn_walk margin −0.174.
        float gaitToeArm = (CaptureFootCenterX(stanceIsLeft)
                            + StanceToeReach() * dir
                            - stanceAnkle.x) * dir;
        if (gaitToeArm < 0.05f)
            gaitToeArm = 0.05f;

        var gaitDecision = captureGait.Decide(
            new GaitStateMachine.Timing
            {
                // After the first plant the 0.10 s / 0.62 share wait is a
                // free-pelvis window: swap at 5.675, pelvis +8.3, then
                // +21.2 at 5.80 before the second Swing even freezes, and
                // that Swing never leaves (both soles down, walkSwap 1).
                // Next step as soon as contact has settled a few frames
                // and CoM is on the lead half.
                doubleSupportMin = captureStepCount > 0
                    ? Mathf.Min(captureDoubleSupportMin, 0.03f)
                    : captureDoubleSupportMin,
                doubleSupportMax = captureDoubleSupportMax,
                doubleSupportShare = captureStepCount > 0
                    ? Mathf.Min(captureDoubleSupportShare, 0.40f)
                    : captureDoubleSupportShare,
                doubleSupportHold = captureDoubleSupportHoldSeconds,
                touchdownFrames = captureTouchdownFrames,
                touchdownBlendSeconds = captureTouchdownBlendSeconds,
                recoveryMargin = captureRecoveryMargin,
                swingOvertime = captureSwingOvertime,
                // DualSupport with the swing still on the slab is the
                // dirt lift, not a stuck plant. capturePhaseWatchdog 0.5
                // opened RecoveryStep of that glued pair (burn_walk:
                // walkTime 5, watchdog ~5.50, fell 5.87). Recovery is
                // IsSingleSupport, so TickCaptureWalk fed
                // com.x - stanceAnkle.x into UpdateState and
                // fallCoMOffset 0.30 tripped while mid-feet was still
                // ~0.22 m and both soles were down.
                // Zeroing only firstDs left the same 0.5 s ceiling on
                // the DualSupport AFTER a plant. TickWatchdog runs
                // before DecideDoubleSupport: once share rolls onto
                // the lead (skip ends) at StateAge >= 0.5, Recovery
                // wins over the periodic Swing freeze. p_cap / p_capds
                // named Falling at 6.29 / 6.30 s with swap 2 —
                // walkTime 5 + that post-plant 0.5. Freeze of first
                // Swing waits for trail-up (KneeLift in DualSupport),
                // not a travel latch on a glued pair. DualSupport
                // watchdog stays 0. Swing peel while touching skips
                // the 0.5 s ceiling for one extra fold clock. Emergency (xi past the
                // toe) still freezes from DecideDoubleSupport. PD /
                // signs / fallCoMOffset 0.30 unchanged.
                phaseWatchdog = captureState == CaptureWalkState.DoubleSupport
                    ? 0f
                    : capturePhaseWatchdog
            },
            new GaitStateMachine.World
            {
                dt = dt,
                xi = captureLastCapturePointX,
                comX = com.x,
                dir = dir,
                stanceAnkleX = stanceAnkle.x,
                swingAnkleX = swingAnkle.x,
                // First DS: same toe as StepPhaseDriver.ComLeftSupport.
                // recoveryMargin on that radius asked xi 6 cm past the
                // toe before the first freeze — still inside fallCoMOffset
                // 0.30 from mid-feet if the pair has not split.
                // Ankle → real toe, not centre→toe on an ankle origin.
                supportRadius = gaitToeArm,
                // 0: aheadLimit is the toe. catchPast 0.08 from the ankle
                // is still on the plate — after a good plant that is the
                // orbit, and it stole DS into a 120° Recovery squat.
                catchPast = 0f,
                share = gaitShare,
                launchComX = captureLaunchComX,
                comLead = Mathf.Max(0.01f, captureLaunchLead),
                collapsed = collapsed,
                posed = posed,
                // Recovery on the stacked quiet onset pair is the 1.3 s
                // fold. Post-plant comPastToe with a pinned trail is handled
                // in GaitStateMachine (Swing, not Recovery) — do not blanket
                // suppressEmergency on the heel pin: that blocked the Swing
                // open and left DualSupport to fold (burn_walk 5.905 chair).
                // Onset stacked suppress stays. PD / signs unchanged.
                suppressEmergency = firstDs
                    && Mathf.Abs((stanceAnkle.x - swingAnkle.x) * dir)
                        < 0.04f,
                planValid = capturePlan.valid,
                planRecovery = capturePlan.recovery,
                planTargetX = capturePlan.targetX,
                planStartX = capturePlan.startX,
                planDuration = capturePlan.duration,
                planSwingSide = capturePlan.swingSide
            },
            new GaitStateMachine.Contacts
            {
                leftGrounded = captureContact.Grounded(true),
                rightGrounded = captureContact.Grounded(false),
                leftHeel = captureContact.HeelPin(true),
                rightHeel = captureContact.HeelPin(false),
                leftTouching = captureContact.Touching(true),
                rightTouching = captureContact.Touching(false),
                leftLoaded = captureContact.Loaded(true),
                rightLoaded = captureContact.Loaded(false),
                leftSupporting = CaptureFootSupporting(true),
                rightSupporting = CaptureFootSupporting(false)
            });
        // WALK-1: what the soles were doing, straight off the contact sensor.
        walkTap.touchL = captureContact.Touching(true) ? 1 : 0;
        walkTap.touchR = captureContact.Touching(false) ? 1 : 0;
        walkTap.loadedL = captureContact.Loaded(true) ? 1 : 0;
        walkTap.loadedR = captureContact.Loaded(false) ? 1 : 0;
        walkTap.supportL = CaptureFootSupporting(true) ? 1 : 0;
        walkTap.supportR = CaptureFootSupporting(false) ? 1 : 0;
        walkTap.roleL = captureGait.IsStanceFoot(true) ? TapRoleStance : TapRoleSwing;
        walkTap.roleR = captureGait.IsStanceFoot(false) ? TapRoleStance : TapRoleSwing;
        // p_auto 19:42: DecideSwing late used freeze age (0.415) while
        // the curve tracks air age (0.380). Recovery of that still-
        // pressing periodic sole is the chair (getUpFallTime 6.225,
        // swap 2). Gait now waits on SwingAirAge and will not b*=0
        // an airborne cruise. Refuse here too so a leftover emergency
        // freeze cannot squat a sole that is already planting.
        // Glued both-heel Recovery while CoM is still on the plate
        // stays the 120° chair — DualSupport catch past the toe is
        // not this path (gait IsSingleSupport is false in DS).
        bool gluedChair = CaptureHeelGrounded(true) && CaptureHeelGrounded(false);
        bool airbornePeriodicPlant = captureGait.IsSingleSupport
            && captureSwingLeftGround
            && capturePlan.valid
            && !capturePlan.recovery;
        // burn_cap c125adbd: RecoveryStep at t=5.920 (swap 1), fell at
        // 6.275 via fallCoMOffset 0.30. !BodyCollapsed here let a Max'd
        // standComHeight keep SinkCollapsed true on a live orbit, so the
        // refuse never fired and the 120° chair ran. Collapse is the
        // CaptureWalk hip/column bit; sink is a standing-bar term.
        // Glued both-heel Recovery is always the chair — even when CoM
        // has already passed the lead toe (burn_walk 5.905). Gait now
        // opens Swing on that frame; this refuse is the belt if a
        // leftover emergency freeze still names Recovery.
        bool refuseRecoveryChair = gaitDecision.enter
            && gaitDecision.next == CaptureWalkState.RecoveryStep
            && captureStepCount > 0
            && !collapsed
            && (airbornePeriodicPlant || gluedChair);
        if (refuseRecoveryChair)
        {
            if (gluedChair)
            {
                // Overdue transfer: keep the freeze, name Swing so the
                // trail peels instead of a DualSupport fold-in-place.
                gaitDecision.next = CaptureWalkState.Swing;
                gaitDecision.emergency = false;
                gaitDecision.watchdog = false;
            }
            else
            {
                gaitDecision.enter = false;
                gaitDecision.freezePlan = false;
                gaitDecision.emergency = false;
                gaitDecision.watchdog = false;
            }
        }
        if (gaitDecision.watchdog)
            captureStanceAnchorValid = false;
        if (gaitDecision.freezePlan)
        {
            // Do not declare swingIsLeft here: TickCaptureWalk already
            // owns that name later in this method (CS0136 at 11:46).
            FreezeFootstepPlan(
                gaitDecision.swingSide, com, comVelX, omega0, dir,
                gaitDecision.emergency,
                CaptureAnklePoint(gaitDecision.swingSide < 0), groundY,
                CaptureFootCenterX(stanceIsLeft));
        }
        captureGait.Apply(gaitDecision);
        if (gaitDecision.swapRoles)
        {
            capturePlan.valid = false;
            captureStanceAnchorValid = false;
            captureDirtLiftAge = -1f;
            ClearDirtFoldLatch();
        }
        // WALK CYCLE 6i: the swing sole's own airborne clock, advanced here
        // because this is the one place that sees every state (Swing,
        // TouchdownBlend, RecoveryStep) with the roles already handed over by
        // Apply. Touching again ends it: a sole on the slab has no air age.
        if (gaitDecision.swapRoles)
            captureAirborneAge = 0f;
        else if (!CaptureFootTouching(!captureGait.IsStanceLeft))
            captureAirborneAge += dt;
        else
            captureAirborneAge = 0f;
        // WALK-1: gait evidence, published after Apply so the swap latch is
        // this frame's. roleL/R are re-read below; these are the machine's.
        walkTap.dualSupport = captureGait.IsDualSupport ? 1 : 0;
        walkTap.swapReason = captureGait.LastSwapReason;
        walkTap.swapMode = captureGait.LastSwapMode;
        walkTap.swapAge = captureGait.LastSwapStateAge;
        walkTap.gaitSteps = captureGait.StepCount;
        walkTap.entryReason = captureGait.LastEntryReason;
        walkTap.gateMask = captureGait.LastGateMask;
        walkTap.rejectCode = captureGait.LastRejectCode;
        walkTap.planValid = capturePlan.valid ? 1 : 0;
        // The walker's own state, so the CSV's walkPhase column can be told
        // apart from the StepPhaseDriver fallback (they share the 0..3 codes).
        walkTap.gaitState = (int)captureGait.State;
        walkTap.gaitStateAge = captureGait.StateAge;
        walkTap.roleL = captureGait.IsStanceFoot(true) ? TapRoleStance : TapRoleSwing;
        walkTap.roleR = captureGait.IsStanceFoot(false) ? TapRoleStance : TapRoleSwing;

        // The switch may have swapped roles: re-read the sides before any
        // joint is written, so no hinge gets last frame's owner.
        stanceIsLeft = captureStanceSign < 0;
        stanceAnkle = CaptureAnklePoint(stanceIsLeft);
        swingAnkle = CaptureAnklePoint(!stanceIsLeft);
        LatchCaptureStanceAnchor(stanceIsLeft);

        // After Apply: a hip that has sat is a chair, in every phase.
        // DualSupport-only restore left Swing in Falling with muscles
        // still on (CaptureWalkActive returns before RelaxAllMuscles).
        // Onset firstHeap already returned a launch squat.
        // collapsed = hip | BodyCollapsed; column is onset log only.
        if (collapsed && !firstDs)
        {
            walkTap.abortCode = TapAbortCollapsed;
            ReturnCaptureWalkToStandingPlant();
            return;
        }
        if (CaptureWalkShouldReturnToStandingPlant(collapsed))
        {
            walkTap.abortCode = TapAbortReturnPlant;
            ReturnCaptureWalkToStandingPlant();
            return;
        }
        // DualSupport / TouchdownBlend with no loaded sole is a vault,
        // not a peel (peel keeps the stance plate). Standing Plant;
        // Tick waits TiltSettled + CoM-to-stance + both planted, then
        // walk starts. Quiet launch never BothAir. Swing BothAir is
        // the airborne catch — DriveCaptureSwingLeg still plants.
        if (captureGait.IsDualSupport
            && captureContact.BothAir
            && stepPhaseDriver != null
            && stepPhaseDriver.walkRecoverAnyPose > 0.5f)
        {
            walkTap.abortCode = TapAbortBothAir;
            ReturnCaptureWalkToStandingPlant();
            return;
        }
        // Quiet-stance UpdateState(fallCoMOffset 0.30) is not a walking
        // event. Stance-only (com.x - stanceAnkle.x) crossed 0.30 at
        // 6.300 s while the swing was still the catch, or while the
        // trail was still down and GaitStateMachine had already named
        // Swing / Recovery (p_capds swap 2, mid-feet ~0.22). DualSupport
        // both-down already skipped; a live step must too. Collapse
        // restored above; the quiet automaton names Falling after
        // StandingRestore falls through. Do not lie to GaitStateMachine
        // that the swing is up: DualSupport peels with KneeLift, then
        // Gait freezes Swing on trail-up. Do not freeze from a travel
        // latch while the sole still welds.

        float rollShare = CaptureFrontShare(com.x, stanceAnkle.x, swingAnkle.x, dir);
        // Trail still holds CoM (share below the lead half): keep the
        // hip/knee weld. Peeling that damper with KneeLift is the 6.3 s
        // dump at swap 1. Peel a true airborne swing, the first-step
        // dirt lift (take/fold stays in DualSupport), or post-plant DS
        // once CoM has arrived on the lead.
        bool trailCarries = CaptureTrailStillCarries();
        // Re-sample after Apply: a swap this step flipped which sole
        // is the trail. CoM-past-lead is the walking orbit, not an
        // unloaded rear — peeling that pin is the 6.300 s dump.
        trailHeel = CaptureHeelGrounded(!stanceIsLeft);
        // First DualSupport before fold: fade trail HIP and ANKLE so the
        // latched hip SPD is not a K=12 weld; keep trail KNEE K until
        // fold. A limp knee plus KneeLift from frame 0 (or DualSupportMax
        // 0.30) is the loaded-trail chair / 6.300 s dump. After fold the
        // whole swing chain peels. Post-plant DS peels an unloaded trail
        // even while the toe still scrapes — waiting for union-air is
        // the sticky-µ skate (footFriction 1.2, swap 0).
        bool onsetFold = firstDs
                         && CaptureDirtFoldMayOpen(stanceIsLeft);
        bool peelSwing = captureGait.IsSingleSupport
            || (firstDs && onsetFold)
            || (captureGait.State == CaptureWalkState.DoubleSupport
                && captureGait.StepCount > 0
                && !trailCarries);
        bool swingIsLeft = captureGait.IsSwingFoot(true);
        // Peel the trail knee while the caudal pin is down so the
        // rocker-matched flex is not welded by JointFriction. A locked
        // knee made plantarflex isometric (burn_walk swap 0).
        if (firstDs && trailHeel)
            ApplySwingLegFriction(1f, swingIsLeft, 1f, includeAnkle: true, includeKnee: true);
        else
            ApplySwingLegFriction(peelSwing ? 1f : 0f, swingIsLeft, peelSwing ? 1f : 0f);

        // Trunk. CaptureBalance owns the one-way lumbar target and the PD
        // signal; ActuatorMixer writes the muscles. SPD has no I-term to
        // wind up; the analog of "reset the integrator on support change" is
        // ZeroCaptureLegMuscles on freeze (activation lag 50 ms).
        if (lumbarFlexor != null && lumbarExtensor != null && vestibularSystem != null)
        {
            var trunk = captureBalance.Evaluate(
                new CaptureBalance.Pose
                {
                    lumbarBaseTilt = lumbarTargetTilt,
                    trunkLeanDeg = Mathf.Abs(captureTrunkLeanDeg) * CaptureLeanPhase(),
                    dir = dir,
                    entryBlend = captureEntryBlend,
                    holdTrunk = CaptureBalance.HoldTrunk(captureState, captureEntryBlend),
                    hasTilt = true,
                    measuredTilt = vestibularSystem.GetBodyTilt()
                },
                new CaptureBalance.Gains
                {
                    lumbarP = lumbarPGain,
                    lumbarD = lumbarDGain,
                    lumbarErrorRef = lumbarErrorReferenceDegrees,
                    speedRef = speedReferenceDegPerSec
                },
                vestibularSystem.GetBodyAngularVelocity());
            captureMixer.ApplySignal(trunk.lumbarSignal, lumbarFlexor, lumbarExtensor);
        }
        ControlNeckAndHeadToWorldUpright();
        DriveCaptureArms();

        switch (captureState)
        {
            case CaptureWalkState.DoubleSupport:
            {
                if (captureStepCount == 0)
                {
                    // Onset: RestoreHip on the lead, latch-hold the trail
                    // hip (quiet joint, not Zero) through lean. Trail
                    // ankle plantarflexes on the lean clock (not Zero):
                    // a world-flat plate kept the heel in the manifold
                    // so fold never saw air (c125adbd 6.300). Fold then
                    // rockers the trail (plantarflex + MTP plus + the
                    // 3 cm rocker knee). Clearance IK waits for the
                    // caudal pin to open — 0.12 m IK while pinned is
                    // the 0.29 m sit. Stance hip stays up. Stance MTP = 0.
                    StepCaptureDirtLiftClock();
                    DriveCaptureStancePlant(stanceIsLeft);
                    if (onsetFold)
                        DriveCaptureSwingKneeLift(!stanceIsLeft, true);
                    else
                    {
                        DriveCaptureTrailHipHold(!stanceIsLeft);
                        DriveCaptureHoldKnee(!stanceIsLeft, false);
                        // Fold waits on the lean clock + 0.10 s min DS,
                        // not a 3 cm hip-Y hold (that shut the rocker
                        // on the 9° bow). Until it opens, plantarflex
                        // the trail so the plate is already a toe
                        // rocker. MTP plus is on the same clock
                        // (DriveCaptureToes) so the ball stays the
                        // pivot. Stance MTP stays 0.
                        DriveCaptureDirtFoldAnkle(
                            !stanceIsLeft, CaptureOnsetLeanU());
                    }
                    if (CaptureFootLoaded(stanceIsLeft))
                        DriveCaptureCop(stanceIsLeft, dir, com, comVelX, 1f,
                            CaptureOnsetCopTargetX(dir));
                    break;
                }
                float share = Mathf.Clamp01(rollShare);
                // After the first plant the trail still carries CoM until
                // that mass sits on the lead plate. KneeLift / RestoreHip
                // 10° on the loaded rear is pelvis +8→+21° in 0.1 s, then
                // fallCoMOffset 0.30 at 6.3 s with swap 1. Hold the
                // measured chord while CoM is still on the rear sole.
                // Once the lead has the mass, the trail is the swing:
                // drive it whether the sole still scrapes or catchOwns
                // already took it off. Mute hip/knee on that airborne
                // trail (only peel when heel/toe still down) lets
                // gravity drop the foot back onto the plate — both
                // soles down, second Swing never leaves, then the
                // 6.2 s GETUP freeze (p_auto 17:32 swap 1). Loaded
                // rear still holds above. Not scissor, not toe-off.
                if (trailCarries)
                {
                    captureDirtLiftAge = -1f;
                    ClearDirtFoldLatch();
                    DriveCaptureLoadedStanceHip(stanceIsLeft);
                    DriveCaptureHoldKnee(stanceIsLeft);
                    DriveCaptureHoldMeasuredHip(!stanceIsLeft, true);
                    DriveCaptureHoldKnee(!stanceIsLeft);
                }
                else
                {
                    DriveCaptureLoadedStanceHip(stanceIsLeft);
                    DriveCaptureHoldKnee(stanceIsLeft);
                    DriveCaptureSwingKneeLift(!stanceIsLeft);
                }
                // CoP follows the measured roll while the trail still
                // carries (CoM behind the lead centre). Forcing stanceW=1
                // then put CoP on the new sole while CoM was still on
                // the rear (capwalk1 sit). Once CoM has arrived, the
                // lead takes the whole ankle loop so the contacting
                // trail can go quiet.
                float stanceW = trailCarries ? share : 1f;
                float swingW = 1f - share;
                // Share is the torque split. The CoM setpoint is the lead
                // sole: a per-foot centre on the trail (weight ~1 after a
                // plant) is happy when CoM sits 1.5 cm ahead of the REAR,
                // so share never reaches 0.62 and the next swing never
                // honestly fires. Same ankle loop, one target.
                float comSetpoint = CaptureFootCenterX(stanceIsLeft)
                                    + captureCopLead * dir;
                if (CaptureFootLoaded(stanceIsLeft))
                    DriveCaptureCop(stanceIsLeft, dir, com, comVelX, stanceW, comSetpoint);
                // Dirt-lift owns the trail ankle (plantarflex / heel
                // rise). CoP after KneeLift would re-weld a Zero plate
                // and the heel stays down (grounded = heel OR toe).
                if (trailCarries && CaptureFootLoaded(!stanceIsLeft))
                    DriveCaptureCop(!stanceIsLeft, dir, com, comVelX, swingW, comSetpoint);
                break;
            }
            case CaptureWalkState.Swing:
            case CaptureWalkState.RecoveryStep:
            {
                // Recovery: plant-IK knee holds the 120° squat the second
                // landing arrived in (capwalk16), so the catch cannot leave.
                // Length-servo the stance toward walking chord; hip still
                // restores world pelvis. Periodic Swing keeps the plant IK.
                if (captureState == CaptureWalkState.RecoveryStep)
                {
                    DriveCaptureRestoreHip(stanceIsLeft, true);
                    DriveCaptureLegLengthKnee(stanceIsLeft);
                }
                else
                    DriveCaptureStancePlant(stanceIsLeft);
                if (CaptureFootLoaded(stanceIsLeft))
                    DriveCaptureCop(stanceIsLeft, dir, com, comVelX, 1f);
                // DualSupport peels with the caudal rocker, then Gait
                // freezes Swing on that pin opening. A leftover glued
                // Swing frame keeps the rocker ONLY while the heel pin
                // is still down — 0.12 m IK into that pin is the sit
                // (p_auto). After the pin opens, a remaining toe scrape
                // is clearance: DriveCaptureSwingLeg peels in place
                // (Y at apex, X frozen). Waiting for union-air to leave
                // KneeLift is the closed loop (burn_walk swingFoot
                // 0.62). After the sole has been up, touching is the
                // plant (DriveCaptureSwingLeg). Recovery keeps catch X.
                if (captureState == CaptureWalkState.Swing
                    && CaptureHeelGrounded(!stanceIsLeft)
                    && !captureSwingLeftGround)
                    DriveCaptureSwingKneeLift(!stanceIsLeft);
                else
                    DriveCaptureSwingLeg();
                break;
            }
            case CaptureWalkState.TouchdownBlend:
            {
                float blend = Mathf.Clamp01(captureStateAge
                    / Mathf.Max(0.01f, captureTouchdownBlendSeconds));
                bool landingIsLeft = capturePlan.swingSide < 0;
                bool oldStanceDown = CaptureFootTouching(!landingIsLeft);
                // ─── WALK CYCLE 6h: an AIRBORNE landing sole keeps its ASK ───
                // The blend is reached 25 ms after a freeze: three frames of
                // peel scrape inside the 2×contactOffset band latch
                // SwingLeftGround once, ContactFrames then reach
                // touchdownFrames and DecideSwing's periodicCatch (plantMiss
                // bypassed) enters this state while the sole is still 0.02 m
                // off the slab. p_auto (defaults, walk start 5): third swing
                // frozen at walk-relative +1.795, blend entered at +1.820 and
                // held 0.445 s with the landing sole OFF the slab in 78 of 89
                // frames (soleClearL to 0.124 m). The plant asks below are the
                // asks of a sole already down — RestoreHip 7 / LandingKnee 10 /
                // CopAnkle 13, then Idle 15 — so the LAST placement ask
                // (TapOwnerSwingLeg 5 / TapOwnerSwingAnkle 12, the 6g capture
                // point) was written at +1.815 and the sole fell back onto the
                // peel X: the hull front stayed at x 0.4128 while comX ran
                // 0.3335 -> 0.6015, supportMargin crossed zero at +2.020
                // (hullMax 0.4120 vs comX 0.4121, comVelX 0.494), the walk
                // handed back at +2.290 (TapAbortCollapsed) and the fall
                // criterion fired at +2.385 — 0.73 m of travel lost against
                // p_start8, whose third swing did NOT trip this (its blend
                // entered with the landing touching 19/19) and kept the
                // placement drive for its full 0.275 s. An airborne sole is a
                // swing: give it the swing/placement ask, and leave the
                // plant/CoP asks to the sole that is actually loaded. Not the
                // landing knee blend, not a new toggle: the state test is the
                // contact sensor's own Touching bit.
                if (!CaptureFootTouching(landingIsLeft))
                {
                    if (oldStanceDown)
                        DriveCaptureStancePlant(!landingIsLeft);
                    else
                    {
                        DriveCaptureHoldMeasuredHip(!landingIsLeft, false);
                        DriveCaptureHoldKnee(!landingIsLeft, false);
                    }
                    // CoP stays on the loaded plate (the old stance): the
                    // landing is not support until it touches.
                    if (oldStanceDown && CaptureFootLoaded(!landingIsLeft))
                        DriveCaptureCop(!landingIsLeft, dir, com, comVelX, 1f,
                            CaptureFootCenterX(!landingIsLeft)
                            + captureCopLead * dir);
                    if (capturePlan.valid && !capturePlan.recovery
                        && capturePlan.swingSide != 0)
                        DriveCaptureSwingLeg();
                    else
                        DriveCaptureSwingKneeLift(landingIsLeft);
                    break;
                }
                // Old stance plant-knee while that plate still carries;
                // both hips restore world pelvis on a two-foot landing.
                // Landing hip used last-IK thigh, which tracks the sit
                // the same way HOLD does once the thigh has arrived.
                // If the old stance has already left, plant-IK of that
                // rear is the p_auto invert (swing 91 % / stance 3 %):
                // the catch is the support, the labeled stance is air.
                // Gait swaps on catchOwns; until then CoP stays on the
                // landing, not on the airborne rear.
                if (oldStanceDown)
                    DriveCaptureStancePlant(!landingIsLeft);
                else
                {
                    DriveCaptureHoldMeasuredHip(!landingIsLeft, false);
                    DriveCaptureHoldKnee(!landingIsLeft, false);
                }
                DriveCaptureRestoreHip(landingIsLeft, CaptureFootLoaded(landingIsLeft));
                DriveCaptureLandingKnee(landingIsLeft, blend);
                // Blend is the knee handover. CoP stays on the roll: at
                // blend=1 with share still low the landing is in front of
                // CoM, and dumping CoP onto it is the capwalk1 sit.
                float share = oldStanceDown
                    ? Mathf.Clamp01(
                        CaptureFrontShare(com.x, stanceAnkle.x, swingAnkle.x, dir))
                    : 1f;
                float comSetpoint = CaptureFootCenterX(landingIsLeft)
                                    + captureCopLead * dir;
                if (CaptureFootLoaded(landingIsLeft))
                    DriveCaptureCop(landingIsLeft, dir, com, comVelX, share, comSetpoint);
                if (oldStanceDown && CaptureFootLoaded(!landingIsLeft))
                    DriveCaptureCop(!landingIsLeft, dir, com, comVelX, 1f - share, comSetpoint);
                break;
            }
        }

        DriveCaptureToes();
    }

    private void EnterCaptureDoubleSupport(int stanceSign)
    {
        capturePlan.valid = false;
        // A new stance leg latches its own plant on the first frame it is
        // measured down; the old leg's anchor is not its contact.
        captureStanceAnchorValid = false;
        captureDirtLiftAge = -1f;
        ClearDirtFoldLatch();
        captureLaunchComX = float.NaN;
        captureLaunchHipY = float.NaN;
        captureGait.Begin(stanceSign);
    }

    // After a forward plant the new stance is ahead of CoM. The rear
    // still carries until CoM has arrived ON that lead plate. Share
    // 0.40 of a 15 cm plant is 6 cm off the rear sole — still the
    // trail's 0.26 m plate — and KneeLift / RestoreHip 10° there is
    // the 6.300 s dump at swap 1 (pelvis +8→+21° in 0.1 s). Do not
    // OR Supporting: overlapping plates both Support while CoM is
    // between them. Cached refs only.
    private bool CaptureTrailStillCarries()
    {
        if (captureGait.State != CaptureWalkState.DoubleSupport) return false;
        if (captureGait.StepCount <= 0) return false;
        bool stanceIsLeft = captureGait.IsStanceLeft;
        if (!CaptureFootLoaded(!stanceIsLeft)) return false;
        float dir = CaptureWalkDir();
        Vector2 com = CaptureLiveCom();
        float lead = CaptureFootCenterX(stanceIsLeft);
        return (com.x - lead) * dir < 0f;
    }

    // Measured weight roll from the trailing sole to the leading one.
    // Arguments are named stance (leadX) and swing (rearX) as the gait
    // currently labels them. After a forward plant the swing is AHEAD of
    // that stance: span is negative. Returning 1 there called the roll
    // finished while CoM was still on the old sole, DoubleSupport then
    // put CoP on the new foot, and capwalk1 sat backward (+88° pelvis,
    // one swap, 0.60 m travel). Measure the roll onto the ahead landing.
    private float CaptureFrontShare(float comX, float leadX, float rearX, float dir)
    {
        float span = (leadX - rearX) * dir;
        if (span < -0.02f)
        {
            float width = (rearX - leadX) * dir;
            if (width < 0.02f) return 0.5f;
            return Mathf.Clamp01((comX - leadX) * dir / width);
        }
        if ((comX - leadX) * dir > 0.02f) return 1f;
        if (span < 0.02f) return 0.5f;
        return Mathf.Clamp01((comX - rearX) * dir / span);
    }

    // Glue: gait says freeze, the planner writes xi and the landing X from
    // positions and velocity, muscles are snapped here. gluedReplan is a
    // gait fact (still in single support, sole still touching) passed in
    // so the planner never reads walkPhase.
    private void FreezeFootstepPlan(int swingSide, Vector2 com, float comVelX,
                                    float omega0, float dir, bool recovery,
                                    Vector2 swingAnkle, float groundY, float stanceX)
    {
        bool swingIsLeft = swingSide < 0;
        Vector2 hipNow = CaptureHipPoint(swingIsLeft);
        bool airborne = !CaptureFootTouching(swingIsLeft);
        bool gluedReplan = !airborne && captureGait.IsSingleSupport;
        // First periodic freeze: measured com.x / v are still the stacked
        // two-pin reading (burn_walk 0.0668 m). The 9°/10° onset lean is
        // stored as torso tilt, not as CoM travel, so PlaceFootX from rest
        // plants at stepMin 5 cm. The moment the trail leaves, that bow
        // is an inverted pendulum about the stance ankle; xi runs past the
        // 5 cm sole and lostFwd opened Recovery (burn_cap 6.275 s, knee
        // 123°). Seed the plan with the tilt pendulum: +rotation is CCW
        // (head to −X), forward lean is minus, CoM to +X. Recovery keeps
        // live xi — that catch must land ON the falling mass.
        float freezeComX = com.x;
        float freezeVelX = comVelX;
        if (!recovery && vestibularSystem != null)
        {
            float h = Mathf.Max(0.20f, com.y - groundY);
            float tiltRad = vestibularSystem.GetBodyTilt() * Mathf.Deg2Rad;
            float tiltX = stanceX - h * Mathf.Sin(tiltRad);
            if ((tiltX - freezeComX) * dir > 0f)
                freezeComX = tiltX;
            float tiltVelX = -vestibularSystem.GetBodyAngularVelocity()
                             * Mathf.Deg2Rad * h;
            if (tiltVelX * dir > freezeVelX * dir)
                freezeVelX = tiltVelX;
        }
        // Recovery that plants ON the pitching CoM follows the dump into
        // −98° (burn_walk swap 2 / 6.455 s). Seed the catch ahead of live
        // CoM so PlaceFootX / DriveCaptureSwingLeg land in front of the
        // mass. Periodic tilt-seed above stays; happy-path stepMin stays.
        if (recovery)
        {
            float catchLead = Mathf.Max(0.05f, StanceToeReach() * 0.35f);
            float ahead = com.x + catchLead * dir;
            if ((ahead - freezeComX) * dir > 0f)
                freezeComX = ahead;
        }
        // WALK CYCLE 6d: the plan's step time is the walker's own measured CoM
        // speed, not the constant captureStepDuration — see
        // CaptureStepDurationForSpeed. The tilt-seeded freezeVelX is NOT the
        // speed to use here: that seed is the first-freeze estimator for a
        // stacked two-pin reading (from rest the measured v is 0), and during
        // the walk it carries the trunk's own fold, so a fold would set the
        // step time instead of the mass (tiltVelX = -angVel*h = 1.49 m/s at the
        // measured -100 deg/s). The live CoM speed is the quantity the step
        // has to keep up with.
        float planSpeedX = WalkComVelocityX;
        if (float.IsNaN(planSpeedX)) planSpeedX = 0f;
        capturePlan = capturePlanner.Freeze(
            CapturePlannerPlanModel(planSpeedX),
            new CapturePointPlanner.Body
            {
                comX = freezeComX,
                comVelX = freezeVelX,
                omega0 = omega0,
                dir = dir,
                vDes = recovery ? 0f : CaptureDesiredSpeed() * dir,
                recovery = recovery,
                swingSide = swingSide,
                swingAnkleX = swingAnkle.x,
                swingAnkleY = swingAnkle.y,
                hipX = hipNow.x,
                hipY = hipNow.y,
                groundY = groundY,
                soleThickness = CaptureAnkleHeightOnSlab(),
                stanceX = stanceX,
                airborne = airborne,
                abandonStreak = captureAbandonStreak,
                holdStart = gluedReplan && capturePlan.valid,
                heldStartX = capturePlan.startX,
                heldStartY = capturePlan.startY,
                heldApex = capturePlan.apex
            });
        // ─── WALK CYCLE 6o: the frame this plan (and its target) is built in ───
        // Read HERE, on the freeze, so the column IS the frame the target was
        // computed in rather than a value that happens to agree with it later.
        // The same estimator the recorder's pelvisTilt column and the hip
        // command's parent frame read (VestibularSystem.GetPelvisTilt), so the
        // reader's subtraction pelvisTilt - capturePlanFrameTiltDeg is one
        // quantity in one convention. Recorder-only: nothing reads it back; 0
        // with no tilt source, reset in ResetCaptureWalk.
        captureLastPlanFrameTiltDeg = vestibularSystem != null
            ? vestibularSystem.GetPelvisTilt()
            : 0f;
        captureGait.NotifyPlanFrozen(gluedReplan);
        // SPD has no integrator. Muscle.activation is a first-order lag at
        // muscleActivationSpeed 20: reversing a saturated stance extensor
        // takes ~100 ms, and that leftover length-servo is a hop at release.
        // Snap the NEW swing to 0; DriveCaptureSwingLeg writes the IK command
        // later in this same step, so the ramp starts from zero, not from
        // the previous owner's saturation. The stance side is not touched.
        // Skip on a glued re-plan: that snap is the peel reset.
        // Skip on an airborne late re-freeze of the same swing: zeroing
        // activationSpeed 20 mid-air drops the hanging sole for ~100 ms
        // and the second plant never arrives (p_auto 17:32).
        if (!gluedReplan)
        {
            bool sameAirSwing = captureGait.IsSingleSupport
                                && capturePlan.valid
                                && capturePlan.swingSide == swingSide
                                && airborne;
            if (!sameAirSwing)
                ZeroCaptureLegMuscles(swingIsLeft);
        }
        if (swingIsLeft) captureLeftSupportKneeHeld = false;
        else captureRightSupportKneeHeld = false;
        // Plant and stance control leave this foot on this frame, before
        // any lift. The next drive switch writes swing IK, not hold-hip,
        // CoP, or plant IK; CaptureFootLoaded is false for the swing even
        // if the toe is still touching. Waiting for !grounded here is the
        // closed loop that froze the walker at ~55°.
    }

    private CapturePointPlanner.Model CapturePlannerModel()
    {
        return new CapturePointPlanner.Model
        {
            lookahead = captureLookahead,
            speedGain = captureSpeedGain,
            stepDuration = captureStepDuration,
            recoveryDuration = captureRecoveryDuration,
            stepMin = captureStepMin,
            stepMax = captureStepMax,
            reachMargin = captureReachMargin,
            recoveryMargin = captureRecoveryMargin,
            stanceLegLength = captureStanceLegLength,
            landLegLength = captureLandLegLength,
            thighLength = legThighLength,
            shinLength = legShinLength,
            swingApex = captureSwingApex,
            footLandLift = captureFootLandLift,
            abandonPutDown = captureAbandonPutDown,
            catchingPast = StanceToeReach()
        };
    }

    // ─── WALK CYCLE 6d: the step finishes in the time the speed allows ───
    // The periodic orbit was a CONSTANT T (captureStepDuration 0.26 s): one
    // step time, whatever the mass is doing. But the swing's landing X is
    // CAPTURE PLACEMENT, recomputed from live state every frame —
    // xCom + captureComLandingScale * v / omega0 = xCom + 0.116 * v (0.37/3.2,
    // 0.075 m at the c6c_walk 0.65 m/s) — so the point the sole is chasing runs
    // forward with the mass while the foot is still in the air, and how far it
    // runs in one step is v * T. At the walking 0.55 m/s that is 0.14 m, at the
    // c6c_walk 0.73 m/s it is 0.19 m, and the air time is not the walker's to
    // spend: the sole has to be DOWN and confirmed inside the support margin
    // the CoM is eating at e^{omega t}. c6c_walk third swing (frozen 6.885 s):
    // soleClearL 0.018 -> 0.161 m, fzL 0.00 N for its last 0.28 s, supportMargin
    // 0.161 -> 0.008 m at 7.085 and -0.019 m at 7.125 s, the third plant never
    // arrived and the run ended 7.14 s with torsoTilt -58.6 and the loaded hip
    // (CSV request = ActuatorMixer.CapLoadedHip's clamped target, i.e. the
    // measured joint + the 45 N*m heel band = 3.0 deg at hipMuscleTorque 100 /
    // hipPGain 1.5 / errorRef 10) and the stance ankle both already pinned at
    // their walls, so no torque was left to answer the fold.
    // So do not hold T: hold the TRAVEL per step at the walking length
    // (captureTargetSpeed * captureStepDuration = 0.143 m) and let the cadence
    // follow the mass. The chase the foot has to finish shrinks with the speed
    // (v * T = v_des * captureStepDuration = 0.143 m at any speed over v_des)
    // instead of growing, the sole is down sooner after the release, and the
    // support margin is caught by the next plant rather than by the trunk.
    // v <= v_des returns captureStepDuration exactly, so the first freeze from
    // rest, the launch and every slow step are unchanged to the bit.
    private CapturePointPlanner.Model CapturePlannerPlanModel(float comVelX)
    {
        CapturePointPlanner.Model model = CapturePlannerModel();
        // 6d is switchable so a walk can be measured with and without it: 0
        // restores the constant-T orbit of every cycle before 6d, to the bit.
        if (captureStepSpeedAdapt != 0)
            model.stepDuration = CaptureStepDurationForSpeed(comVelX);
        return model;
    }

    private float CaptureStepDurationForSpeed(float comVelX)
    {
        // The reference is the walking v_des of THIS mode, CaptureDesiredSpeed
        // (0.55, or 0.88 under -run 1): the cadence only has to give way once
        // the mass is travelling faster than the walk asked it to.
        float vDes = CaptureDesiredSpeed();
        float v = Mathf.Abs(comVelX);
        if (vDes < 0.05f || v <= vDes)
            return captureStepDuration;
        return Mathf.Max(captureStepDuration * vDes / v,
            CaptureSwingDurationFloorSeconds);
    }

    // Swing leg. The curve is SwingTrajectory; this method only glues that
    // pose to the two-link IK. One solve gives the hip and the knee together,
    // so the two hinges cannot disagree about where the foot is going; the
    // ankle only points the sole. While the sole is still down the curve
    // peels in place (Y at apex, X frozen) so freeze cannot write startY
    // and weld the foot that DualSupport just shortened.
    //
    // Once the caudal heel pin is open, a remaining toe scrape is clearance
    // — not a second weld. Freezing X until union-air left CoM past the
    // stance toe; Recovery then planted ON the falling mass and the trunk
    // followed into −98° (burn_walk swap 2 / 6.455 s). Travel X on that
    // peel (same catchSlide as Recovery) so the first plant catches ahead
    // of the dump. Not global toe-off, not scissor, not heel-plant latch.
    private void DriveCaptureSwingLeg()
    {
        tapWriterHip = TapOwnerSwingLeg;
        tapWriterKnee = TapOwnerSwingLeg;
        bool left = capturePlan.swingSide < 0;
        HingeJoint2D hip = left ? leftHipJoint : rightHipJoint;
        // WALK CYCLE 6f: the pelvis this ask is written in is the PLAN's
        // walking tilt (CapturePelvisWorldTarget), not the live measured one.
        // The budget behind that change is in the comment on the ask at the
        // end of this method.
        float pelvisAskDeg = CapturePelvisWorldTarget();
        // ─── WALK CYCLE 6n: ONE read of the frame the ask is delivered in,
        //     and the flexion allowance that belongs to that frame ───
        // 6m writes this command against the hinge's own parent body (the
        // measured pelvis) and publishes it as captureSwingAppliedFrameAngleDeg;
        // the ask below is built for the same body, so the frame is read here,
        // once, at the top of the method: the 6n allowance is derived from it,
        // the ask is bounded by that allowance BEFORE the solve, and the hip
        // command at the end of the method is written inside the same number.
        // Nothing between this read and the publication moves the pelvis (no
        // command of a frame has reached physics until the tick returns), so
        // this is the value 6l/6m published, read one page earlier.
        float deliveredFrameDeg = hip != null && hip.connectedBody != null
            ? Mathf.DeltaAngle(0f, hip.connectedBody.rotation)
            : pelvisAskDeg;
        float hipAllowanceDeg = CaptureSwingHipAllowanceDeg(hip, deliveredFrameDeg);
        // ─── WALK CYCLE 6o: THE PITCH THE PLAN DID NOT BUILD ITS TARGET IN ───
        // The plant is closed on the frame (6m/6n) and the walk still dies as a
        // forward pitch: measured c6n_walk, the target the swing leg is handed
        // jumps 0.5303 -> 0.7839 m at t 7.150 with the body already -58.2 deg,
        // and the pelvis owns nothing that answers it. Read on the same run:
        // in DualSupport the loaded stance hip's restore is CLIPPED to the 45 N*m
        // heel band (reqHipR = measured + 2.6 deg, appTorqueHipR -19..-25 N*m)
        // while the body folds -43 -> -56 deg in 0.2 s, and the trail hip is a
        // zero-error HoldMeasured (ownerHipL 2, appTorqueHipL -3 N*m): the pitch
        // error is either cancelled or clipped, so no delivered ask carries it.
        // The one joint that can still be asked is the swing hip, and it is the
        // pitch's OWN owner: its flexion reaction is what folds the pelvis
        // forward (6f: -6.9 -> -51.5 deg in 0.25 s at the second swap). So the
        // frame the plan target was built in — capturePlanFrameTiltDeg, the
        // pelvis tilt at the freeze — bounds the flexion that ask may carry:
        // the drift between that frame and the frame the target LANDS in (the
        // measured pelvis this command is written on) is paid on the ask, the
        // same way 6n pays its allowance: retract X, never lift Y, upstream of
        // the solve, so solved == applied == ask still holds and the plant is
        // not touched. The bound only ever TIGHTENS the 6n allowance (a body
        // that pitched BACK does not earn itself extra flexion), it is capped
        // (CaptureSwingPlanFrameBoundDeg), and the demand it refuses stays
        // readable in captureSwingHipDemandDeg, which keeps its 6n meaning as
        // the PRE-bound demand of the point handed to the solver.
        float hipBoundDeg = CaptureSwingPitchBoundDeg(hipAllowanceDeg);
        // ─── WALK CYCLE 6p: THE BOUNDED POSTURE ERROR GETS AN OWNER ───
        // Read on the c6o_walk row at t 7.00: the split pelvisTilt -
        // capturePlanFrameTiltDeg is still -35.89 deg while the walk sits in the
        // post-plant DualSupport the 6.845 swap opened, and the code says what
        // carries it there. The posture error IS measured every frame of that
        // window — the loaded hip's restore is called from DriveCaptureStancePlant
        // in Swing, from the blend's landing hip, and from
        // DriveCaptureLoadedStanceHip in DualSupport — so the freeze is NOT the
        // last time the body is read. What is missing is the DELIVERY: the ask is
        // bounded by the walker's own heel arm (CapLoadedHip clips it to the
        // measured joint + heelBand, ActuatorMixer 118-128: 3.0 deg = 45 N*m,
        // which is what the 6o note measured as reqHipR = measured + 2.6), and the
        // other loaded hip's slot in that state is a zero-error HoldMeasured
        // (DriveCaptureHoldMeasuredHip, the trailCarries branch). So the error
        // reaches the joints as a CAP and never as a bounded command: no
        // delivered ask answers the pitch.
        // The one joint whose reaction the pelvis feels with no sole to roll is
        // the swing hip — and it is the pitch's own driver (6f: a hip flexor's
        // reaction posteriorly tilts the pelvis, so the flexion this command
        // already carries IS a forward pitch on it). So the same bounded drift 6o
        // derives (CapturePlanFrameDriftDeg, one source for the charge and the
        // answer) is spent here as a RESTORING bias on the command that joint is
        // given: positive = the ask sits on the EXTENSION side of the pose the IK
        // returned, the direction that pitches the pelvis back instead of folding
        // it. Bounded by the walker's own 45 N*m band expressed in degrees
        // (CaptureSwingPostureBiasDeg: 45 N*m at hipMuscleTorque 100 / hipPGain
        // 1.5 / errorReferenceDegrees 10 = 3.0 deg), so ONE joint answers the
        // pitch with the same bounded dose the loaded hip's ask is capped by. The
        // 6o charge on the ask's X is untouched — the leg keeps its allowance and
        // its step — and the answer costs the APPLIED sole the bias instead
        // (published as the 6l residual; the geometry's own gain is at most
        // l1 + l2 = 0.86 m/rad, so 3 deg cannot move it past 0.045 m, under the
        // 0.05 m the plant's bar allows). Read once, at the top, like the 6n/6o
        // reads: nothing between here and the command below moves the pelvis.
        float postureBiasDeg = CaptureSwingPostureBiasDeg(left);
        captureLastSwingPostureBiasDeg = postureBiasDeg;

        if (capturePlan.valid && hip != null)
        {
            // WALK CYCLE 6b: the hinge the placement is measured from. Read
            // once, here, because the reach disc below has to bound the plan
            // target BEFORE the trajectory is built; nothing in this method
            // moves the hinge before the two-link solve at the end, so the
            // IK reads the same point it always did.
            Vector2 hipPoint = hip.transform.TransformPoint(hip.anchor);
            // Freeze age includes the glued peel. Feeding it to X the
            // moment the probe drops is a shuffle. Periodic swing tracks
            // air time; recovery keeps freeze age so the catch X still
            // travels while the sole is down (capwalk16).
            // Enter Swing resets SwingLeftGround even when catchOwns
            // already lifted the trail. Passing live air as leftGround
            // and swingApex 0 keeps the planner's leftover apex: the
            // Evaluate !leftGround path would stack captureSwingApex
            // on startY and hop.
            bool heelPin = CaptureHeelGrounded(left);
            bool swingAir = !CaptureFootTouching(left);
            bool heelFreedPeel = !heelPin && CaptureFootTouching(left)
                                 && !captureSwingLeftGround;
            Vector2 liveCom = CaptureLiveCom();
            float dir = capturePlan.dir;
            // CoM already past the stance toe: the swing must catch ahead,
            // not wait for union-air (that followed the dump to −98°).
            float toeArm = Mathf.Max(0.05f, StanceToeReach());
            bool comPastToe = (liveCom.x - capturePlan.stanceX) * dir
                              > toeArm * 0.5f;
            bool catchNow = capturePlan.recovery || heelFreedPeel || comPastToe;
            // WALK CYCLE 6: the walker's own bar, read here as a PLACEMENT
            // order. TapAbortTiltPast is the bar this swing crossed at
            // 5.940 s; it fired with the sole still 0.180 m in the air, handed
            // both legs back to the Plant (legLayer 21 -> 17) and the only
            // loaded sole then took 1401 N = 2.02 W. A bar crossed with a foot
            // in the air is an order to PUT THAT FOOT DOWN, not to let go:
            // catchNow drops the arc's ease and the disc below hands the solver
            // the slab point, so the swing lands and the roles can transfer.
            // This and the abort read the same number, so the sole's own
            // contact is the only thing that decides which of the two acts.
            bool landNow = swingAir && !heelPin
                && vestibularSystem != null
                && Mathf.Abs(vestibularSystem.GetBodyTilt())
                   >= Mathf.Max(20f, getUpFallenAbsTilt);
            if (landNow) catchNow = true;
            bool leftGround = captureSwingLeftGround || swingAir || heelFreedPeel
                              || (comPastToe && CaptureFootTouching(left));
            float trackAge;
            if (catchNow)
                trackAge = captureSwingAge;
            else if (leftGround)
                trackAge = captureGait.SwingAirAge;
            else
                trackAge = 0f;
            // ─── WALK CYCLE 6i: an AIRBORNE ask is driven on AIR time ───
            // The ages above stop the moment the gait leaves Swing
            // (DecideSwing owns SwingAge / SwingAirAge, and Enter clears the
            // air pair for TouchdownBlend), so the 6h airborne-placement path
            // was driving the curve with the age the step had 20 ms after its
            // own freeze: the X it handed the solver was startX + 6.6 % of the
            // step — the previous plant's X — however far the live capture ask
            // had travelled (c6h_walk: ask 0.435 -> 0.742, sole 0.204 -> 0.220,
            // comX 0.328 -> 0.686, trunk -44.5 deg). The clock below only
            // exceeds those ages when the gait's have stopped; while the sole
            // is down it is 0, and while the gait is still in Swing it is the
            // peel-shorter of the two. One law for the placement: the sole in
            // the air is asked for the capture point at its OWN plant, so feed
            // the curve the time the sole has actually been off the slab.
            trackAge = Mathf.Max(trackAge, captureAirborneAge);
            float planTargetX = capturePlan.targetX;
            // Catch the fall: if CoM already leads the frozen plant, push
            // the goal ahead of CoM (small lead). Planting on freeze-xi
            // under a pitching trunk is following the dump into Recovery.
            float catchLead = Mathf.Max(0.03f, captureCopLead);
            // CAPTURE PLACEMENT (this cycle). The destination is the CoM's OWN
            // capture point, recomputed from measured state on every frame:
            //     xLand = xCom + vCom * sqrt(h/g) * kLead,   1/omega0 = sqrt(h/g)
            // so the step is a consequence of the mass leaving the support
            // area, not of the frozen plan: a frozen target parks the sole
            // where the CoM already was (auto_walk_fc1: soleClear 0.10 -> 0.05,
            // fzL 0 N, CoM past the hull 5.56 s, fall 6.155 s). One step of a
            // LIPM about the new sole keeps its speed when kLead = tanh(ωT/2)
            // = 0.39 at the measured ω 3.2, T 0.26; the field's 0.37 sits just
            // under that, and 1 (landing ON the capture point) arrests the
            // mass at the plant and leaves the walker parked.
            // captureComLandingScale 0 turns the path off.
            float xLandNow = liveCom.x + catchLead * dir;
            if (captureComLandingScale > 0f)
            {
                float omega = Mathf.Max(0.5f, captureLastOmega0);
                float comVelLand = WalkComVelocityX;
                if (float.IsNaN(comVelLand)) comVelLand = 0f;
                // ─── WALK CYCLE 6g: the ask is the capture point AT THE PLANT ───
                // The sole lands a whole swing AFTER the frame this is written
                // in, and the mass travels v*(duration - trackAge) in that
                // time, so xCom + k*v/omega alone is v*T short of where the
                // mass will be when the sole is down. The sole then lands
                // BEHIND the CoM however well the leg tracks the ask:
                // c6f_walk, third swing (frozen 7.06-7.10, v 1.09-1.61,
                // duration 0.16-0.26): asked 0.874 -> 1.267 (0.116*v), the sole
                // arrived at 0.809 with fzL 0 N, 0.27 m behind the CoM
                // (1.081) and 0.77 m behind the live capture point (1.583),
                // touched 7.34 (soleClearL 0.016, fzL 338 N), 0.24 s after
                // supportMargin crossed zero (+0.012 at 7.08, -0.009 at 7.10,
                // -0.266 at 7.30), run over 7.38 s. The LIPM's own budget is
                // the neutral lead about the NEW sole, so the plant, not the
                // freeze, is what k is a fraction of: the write-in lead is
                // v*T + k*v/omega, and only k*v/omega of it survives as the
                // delivered step. The arrest fraction itself is untouched.
                float swingRemain = Mathf.Max(0f, capturePlan.duration - trackAge);
                float xLand = liveCom.x
                              + comVelLand * swingRemain
                              + comVelLand / omega * captureComLandingScale;
                // 5 mm minimum forward lead: the sole is never asked for a
                // point on or behind the mass, but this is a floor and not the
                // old fixed 3 cm lever — the placement is the capture point.
                float lead = Mathf.Max((xLand - liveCom.x) * dir, 0.005f);
                xLandNow = liveCom.x + lead * dir;
                if ((xLandNow - planTargetX) * dir > 0f) planTargetX = xLandNow;
            }
            else if ((liveCom.x - planTargetX) * dir > 0.02f)
            {
                planTargetX = xLandNow;
            }
            // ─── WALK CYCLE 6b, site 1 of 2: the LATCHED plan target ───
            // The latched land point is pulled into the reach disc around the
            // CURRENT hip BEFORE the trajectory is built, at the plan's own
            // land height. Further down the lateral footprint span is measured
            // from the trajectory Y (shallower than the drop the ankle finally
            // takes) and about hipRef, which may be the FREEZE hip
            // (capturePlan.hipLandX) sitting ahead of the live one, so a
            // latched target could still leave |target − hip| > l1 + l2 — and
            // then SolveCaptureLegIk clamps the chord itself and hands back the
            // fully extended leg: auto_walk_c6, walk command 5.0 s, swing
            // 5.175–5.935 s never landed, ikSaturated 1 on all 127 swing
            // frames, reqKnee pinned 17.4920 (= l1 + l2 − 0.01), reqHip −65,
            // fzL 0.00 N, captureStepCount 0, the CoM turned back at 5.575 s
            // and the trunk reached the getUp bar (walkTapAbort 6, the swing
            // abort at BalanceControllerCaptureWalk.cs:1088). The disc keeps
            // the plan's landY, so the sole is still asked for the slab: only
            // X is retracted, to the farthest land point the leg can reach.
            float landSpan = CaptureSwingReachSpanX(hipPoint, capturePlan.landAnkleY);
            planTargetX = Mathf.Clamp(planTargetX,
                hipPoint.x - landSpan, hipPoint.x + landSpan);
            var pose = captureSwing.Evaluate(
                new SwingTrajectory.Path
                {
                    startX = capturePlan.startX,
                    startY = capturePlan.startY,
                    targetX = planTargetX,
                    landY = capturePlan.landAnkleY,
                    apex = capturePlan.apex,
                    duration = capturePlan.duration,
                    xShare = captureSwingXShare,
                    swingApex = swingAir ? 0f : captureSwingApex,
                    press = captureFootPress,
                    catchSlide = catchNow
                },
                trackAge,
                leftGround,
                CaptureFootTouching(left));
            float x = pose.x;
            float y = pose.y;
            // Live reach still tracks a SLOW hip (the sole would hang).
            // Floor the span at the freeze footprint: a taut walking pose
            // sits near drop==reach, so any vault or a collapsing dump
            // zeros the live span and retracts the catch under the hip —
            // the step that should have stopped the −27° lean. The planner
            // owns the span; the trajectory does not read the hip.
            // CAPTURE PLACEMENT (this cycle): that floor was measured with the
            // FREEZE hip, which sits higher over the slab than the placing hip,
            // so it licenses a lateral offset the current drop cannot pay for:
            // the chord then exceeds the bars, SolveCaptureLegIk saturates on
            // l1+l2−0.01 and the sole hangs centimetres above the slab instead
            // of landing (auto_walk_fc1: ikSaturated 1 and reqKnee pinned at
            // 17.49 — the fully extended leg — for the whole swing). On the
            // placement path the footprint is the live one, so the sole xLandNow
            // is a sole the leg can actually put on the slab. Old path keeps the
            // floor.
            float liveSpan = capturePlanner.ReachSpanX(
                hipPoint.y - y, CapturePlannerModel());
            float span = liveSpan;
            if (span < 0f)
                span = capturePlan.reachSpan;
            else if (capturePlan.reachSpan > 0f && captureComLandingScale <= 0f)
                span = Mathf.Max(span, capturePlan.reachSpan);
            // First catch used to skip this clamp so a sagging hip could
            // not park the sole under the mass. Unclamped X plus a world
            // Y that stays at freeze height is the p_auto sit: hip −90 /
            // knee 110 in the first 0.3 s of Swing (16:26, SS 61 %,
            // sag 0.26 m, swap 0). span is Max(live, freeze reachSpan),
            // so a later drop cannot shrink the footprint.
            // Retracting onto the circle while the plan is already a
            // forward catch plants behind arriving CoM (burn_walk
            // 6.355 / swap 2). Far-edge toward the target; if that
            // edge is already behind CoM, keep the catch ahead of CoM
            // (not under the hip — that followed the −98° pitch).
            if (span >= 0f && !capturePlan.recovery)
            {
                float hipRef = hipPoint.x;
                if ((capturePlan.hipLandX - hipRef) * dir > 0f)
                    hipRef = capturePlan.hipLandX;
                float wantX = Mathf.Clamp(x, hipRef - span, hipRef + span);
                if ((planTargetX - hipRef) * dir > span)
                    wantX = hipRef + span * dir;
                if ((liveCom.x - wantX) * dir > 0.02f)
                {
                    // CAPTURE PLACEMENT: the goal is the capture landing point,
                    // not a fixed 3 cm. The far reach edge is the fallback when
                    // the capture point sits outside the footprint.
                    float maxReach = hipRef + span * dir;
                    wantX = (xLandNow - hipRef) * dir <= span
                        ? xLandNow
                        : maxReach;
                }
                x = Mathf.Lerp(x, wantX, Mathf.Max(pose.xEase, catchNow ? 1f : 0f));
            }
            else if (span >= 0f && capturePlan.recovery
                     && (liveCom.x - x) * dir > 0.02f)
            {
                // Recovery: still plant ahead of a pitching CoM when the
                // frozen xi already sits under the mass. CAPTURE PLACEMENT:
                // same capture landing point as the periodic step, so one rule
                // owns where the sole goes.
                float hipRef = hipPoint.x;
                float maxReach = hipRef + span * dir;
                x = (xLandNow - hipRef) * dir <= span ? xLandNow : maxReach;
            }
            // ─── WALK CYCLE 6c: the PEEL is a LIFT, in place ───
            // A sole that is still on the slab is peeled about its own
            // footprint; the curve's X may travel only once the sole is
            // AIRBORNE. c6b_walk, second swing (swap 5.79 s): the arc was
            // already past its 0.26 s duration (SwingAge 1.0 s), so the
            // commanded point was the LAND point on the slab, 0.85 m from
            // the placing hip — the solver's own chord clamp (reqKneeR
            // 17.4920 = the straight leg, reqHipR -65.00) — and the sole
            // never left: fzR > 1 N in 199 of the 229 frames, soleClearR
            // 0.017 m, the toe scraping forward for a full second (the
            // pelvis folded -4.5 -> -90.0 deg, 280 deg/s in the first
            // 0.1 s) and the run ended at 6.885 s. The FIRST step's peel
            // (5.155 s) folded instead — reqKneeL 47.79 = a 0.786 m chord
            // on 0.86 m bars — and its sole left the slab at 5.17 s,
            // 0.015 s in. Same law here: while the sole is down and this
            // swing has never been airborne, the foot's OWN X is the
            // point — a pinned foot's measured X is a held value, not a
            // live one — and CaptureSwingAnkleYForChord below keeps the Y
            // at the lift floor. The leg then FOLDS (chord ~0.70-0.75 m,
            // 55-66 deg of knee, the lift cap is 70), the ankle rises out
            // of the contact band, SwingLeftGround latches, and the
            // airborne curve owns the travel, the catch and the landing.
            // The reach disc just below still bounds that point (X back,
            // Y kept), so a peel cannot ask for a chord the bars cannot
            // give. Recovery keeps its catch X: planting ahead of a
            // pitching CoM is that path's whole job.
            if (!capturePlan.recovery && !captureSwingLeftGround
                && CaptureFootTouching(left))
                x = CaptureAnklePoint(left).x;
            y = CaptureSwingAnkleYForChord(hipPoint, y, left);
            // ─── WALK CYCLE 6: the point handed to the solver, one law ───
            // The disc, now on the (x, y) the two-link solve is about to be
            // asked for. Everything the placement path clamps above is a bound
            // on X alone — the planner's footprint measured about the freeze
            // hip, the capture landing point, the latched plan target — so the
            // chord can still leave the bars (the trajectory's Y is the curve's,
            // not the landY those spans were measured at) and the solver's
            // answer to that is the fully extended leg and a hanging sole.
            // Outside the disc the X comes back and the HEIGHT STAYS: the
            // retracted point is a point at the landing height, i.e. on the
            // slab, which is the one coordinate the swing sole cannot give up.
            // A radial clamp — the nearest point of the disc — would lift the
            // ankle by the same distance the X came back; that is the 0.180 m
            // hover and the 0.00 N swing sole of auto_walk_c6.
            Vector2 placement = SwingPlacementWithinReach(hipPoint, new Vector2(x, y));
            // ─── WALK CYCLE 6n: the flexion allowance, spent on the ASK ───
            // 6m made the delivered command and the ask one frame and left the
            // two numbers that were picked when the command still carried the
            // PLAN's tilt: the plant's ask reads -35.75 deg in the plan frame
            // and needs ~70.6 deg of real flexion in the measured one, so the
            // calibrated 65 deg allowance refuses flexion the leg would have to
            // give for the sole to go where the walker asked. c6m_walk's plant
            // is that refusal: reqHipL pegged 65.000 from 7.170 to the fall
            // with the applied chain short of the ask by 0.0165 / 0.0796 /
            // 0.2168 m on the ramp's last rows, the sole parked 0.15 m under a
            // hip that keeps folding. hipAllowanceDeg is the same bound
            // re-derived in the frame the ask is delivered in
            // (CaptureSwingHipAllowanceDeg), and it is spent HERE, on the point
            // handed to the solver, so the ask the plant receives is one the leg
            // can deliver in that frame and the command at the end of this
            // method is never clamped after the fact (solved == applied == ask,
            // the 6k/6l residuals stay 0). The demand the allowance refuses
            // stays readable: captureSwingHipDemandDeg is the PRE-bound value.
            captureLastSwingHipDemandDeg =
                CaptureSwingHipDemandDeg(hipPoint, placement, deliveredFrameDeg);
            // 6o: the bound spent here is the 6n allowance TIGHTENED by the
            // plan-frame drift (hipBoundDeg), not the allowance alone. Same law
            // as 6n — retract X at the same Y, before the solve — and the
            // PRE-bound demand above is what keeps a fold this bound refuses
            // readable in the row.
            if (captureLastSwingHipDemandDeg < hipBoundDeg)
                placement = SwingPlacementWithinFlexion(hipPoint, placement,
                    deliveredFrameDeg, hipBoundDeg);
            x = placement.x;
            y = placement.y;
            // ─── WALK CYCLE 6j: publish what this frame HANDS the solver ───
            // The four values that make the delivered plant geometry
            // measurable instead of inferred: the plan target in force after
            // every clamp above, the age the curve was actually evaluated at
            // (post-6i Max), the X this frame asks the solver for, and the
            // swing ankle's OWN measured world X in the same frame - no
            // command of this frame has reached physics yet, so that is the
            // sole position the ask is built against. Nothing reads them
            // back: recorder-only instruments.
            captureLastPlanTargetX = planTargetX;
            captureLastSwingTrackAge = trackAge;
            captureLastSwingAskX = placement.x;
            captureLastSwingAskY = placement.y;
            captureLastSwingSoleX = CaptureAnklePoint(left).x;
            // ─── WALK CYCLE 6k: publish the SOLVER'S OWN ANSWER ───
            // 6j ended at the point the solver was HANDED. These add the point
            // the solver's own returned angles put the ankle at, the gap
            // between the two, and the frame the answer is written in, so the
            // 0.457 m at the plant is split into (a) the solver's own
            // input-to-solution kinematics and (b) whatever the drive makes of
            // it. The FK below reproduces the handed point by construction
            // unless the chord clamp or a singular pair fired, so a ~0 residual
            // reads as "not the solver" and a ~0.46 m residual reads as "the
            // solver did it, and the frame values are what it did it with".
            // Recorder-only: nothing reads these back.
            bool solved = SolveCaptureLegIk(hipPoint, new Vector2(x, y),
                    out float thighDeg, out float kneeDeg);
            if (solved)
            {
                captureLastIkThighDeg = thighDeg;
                captureLastIkKneeDeg = kneeDeg;
            }
            captureLastSwingSolveFrameX = hipPoint.x;
            captureLastSwingSolveFrameAngleDeg = pelvisAskDeg;
            if (solved)
            {
                captureLastSwingSolvedSoleX =
                    CaptureSolvedAnklePoint(hipPoint, thighDeg, kneeDeg).x;
                captureLastSwingSolvedResidualX =
                    captureLastSwingAskX - captureLastSwingSolvedSoleX;
            }
            else
            {
                // No pose to publish (singular or zero-length links): the same
                // 0 sentinel the off-walker case uses, never NaN.
                captureLastSwingSolvedSoleX = 0f;
                captureLastSwingSolvedResidualX = 0f;
            }
        }

        // ─── WALK CYCLE 6f: WHO PITCHES THE TRUNK - the FEED, not the ask ───
        // The algebra is right: jointAngle = pelvis - thighWorld is what puts
        // the sole where the IK asked. The defect was the FEED. Handed the
        // live measured pelvis, the ask is a function of the fold itself: it
        // follows the sag down at the fold's own rate, reaches the clamp while
        // the measured joint lags it, and the PD then holds a FULL-SCALE
        // FLEXION for as long as the fold lasts. A hip flexor's reaction
        // posteriorly tilts the pelvis, so that flexion is a forward pitch on
        // the pelvis the swing leg hangs from - the loop that took the pelvis
        // -6.9 -> -51.5 deg in 0.25 s at the second swap.
        //
        // c6e_walk, 6.415-6.570 s (200 Hz, the 29 frames with the swing hip in
        // ownerHipR 5):
        //   pelvisTilt  -11.07 -> -34.44      hipR (measured) -5.65 -> -49.82
        //   reqHipR     -14.37 -> -65.00 (the CaptureClampSwingHipDeg floor;
        //               pegged for all 50 frames of 6.900-7.145 to the fall)
        //   demandHipR  ramps -0.16 -> -1.20 (|demand| > 1 = "more torque than
        //               the muscle can deliver"), mean |demand| 0.51 over the
        //               29 frames, appTorqueHipR to 97.2 N*m of the 100 ceiling
        //   forward-pitch impulse on the pelvis +6.92 N*m*s (flexion positive)
        //   = -99 deg/s of pelvis-unit fold rate at I = 4 kg*m^2, against a
        //   measured |pelvVel| of 152.9 deg/s in 6.45-6.70 and an external
        //   m*g*(comX - xCopMeas) of only +32.3 N*m in that same window. The
        //   swing-hip reaction, not the ground, is what the fold's onset is
        //   made of; the ground then owns it (+48 -> +176 N*m from 6.70,
        //   dcm 0.39 -> 0.83, fzL 0 N from 6.94, dcm/supportMargin past the
        //   hull at 7.105, fall 7.145).
        //
        // The counterfactual, from this same recording: with the plan's tilt
        // (CapturePelvisWorldTarget, late-Swing -10..-3) the ask is less
        // flexed by exactly the sag (plan - measured = +9.5 deg at 6.445,
        // +31.4 at 6.545), so the same demand moves by hipPGain/errorRef*that
        // = 0.15/deg: the -1.20 flexion reads as an EXTENSION demand and
        // saturates at +1.00 from 6.46 on. First order the forward impulse
        // +6.92 N*m*s becomes a restoring +10.1..+14.5 N*m*s - the -99 deg/s
        // fold turns into a braking push of +144..+208 deg/s at the same
        // inertia. The unloaded swing leg becomes the strut that holds the
        // pelvis up over the stance foot: the law the file header has named
        // since the first pass ("hip joint = walking pelvis lean - IK thigh").
        // The measured tilt is what capwalk3 (+137 deg, CoM 39 cm behind the
        // feet) was measured with.
        //
        // Untouched on purpose: the IK point, the reach disc, the in-place
        // peel, the CaptureClampSwingHipDeg floor, every cap and gain. The
        // peel's own lift ask (DriveCaptureSwingKneeLift) still reads the
        // live pelvis; it opened the fold alone with a 35 ms -1.349..-0.288
        // ramp at 6.385-6.415 (~3.2 N*m*s of the same forward pitch) and is
        // out of this cycle's scope.
        // jointAngle = parent - child, so the hip angle that puts the thigh
        // at a world tilt is parent - thighWorld. Cap before the
        // hinge stop: SPD to −90 / 110° is the first-transfer chair.
        // ─── WALK CYCLE 6m: the apply frame is the MEASURED parent ───
        // 6l measured the plant: the ask the solver was handed and the
        // world thigh it returned are one frame (solvedSole - appliedSole
        // 0.5015 m), and the command was written against the PLAN's tilt
        // (pelvisAskDeg, -3.01 at the plant and frozen there) while the
        // body the hinge hangs from was measured -37.82. jointAngle =
        // parent - child is incremented against the JOINT'S OWN parent
        // body, so the parent here is the hinge's connectedBody rotation:
        // the same value 6l publishes as captureSwingAppliedFrameAngleDeg,
        // and the same conversion the in-place lift has always used
        // (DriveCaptureSwingKneeLift: GetPelvisTilt() - thighDeg). The ask
        // itself was never in the plan frame - it is built from the
        // measured hinge, the measured sole and the live CoM - so nothing
        // is re-projected: the command is written in the frame the body is
        // actually in, and the returned world thigh survives to the sole.
        // ─── WALK CYCLE 6n: the frame read is the method's own, one page up,
        // and the floor is that frame's flexion allowance ───
        // The parent is the same read the 6n allowance was derived from and the
        // ask was bounded by (deliveredFrameDeg, top of this method), so the
        // command below cannot be written in a frame other than the one the ask
        // was made deliverable in. The floor is CaptureSwingHipAllowanceDeg's
        // number — TIGHTENED by the 6o plan-frame bound (hipBoundDeg, top of
        // this method), which is the same number the ask above was bounded by,
        // so the command and the ask still carry one bound and not two — NOT the
        // plan-frame CaptureClampSwingHipDeg floor: the ask is
        // now inside the allowance before the solve, so this is the net that
        // catches a solve the ask bound could not see (a singular pair, a chord
        // clamp), and with the ask inside it the applied pose lands on the ask
        // instead of being silently 0.2168 m short of it. The other two callers
        // of CaptureClampSwingHipDeg (the in-place peel and the lift hold) keep
        // that plan-frame floor unchanged: they are not this cycle's subject.
        // ─── WALK CYCLE 6p: the answer is spent on the COMMAND, downstream of the
        //     6n/6o bound, and on the same two numbers ───
        // The 6n/6o bound floors a FLEXION ask (retract X, never lift Y). The 6p
        // bias is not a flexion ask — it is the joint that takes the posture off
        // the pelvis — so it is spent here, downstream of that floor, instead of
        // being absorbed by it, and the result is re-clamped to the same floor and
        // the same +15 hold ceiling the command already had. Nothing above this
        // line changes: the ask, the reach disc, the in-place peel, the solve and
        // the pre-bound demand column are the 6n/6o ones (postureBiasDeg 0 gives
        // the 6n/6o command back to the digit).
        float hipCommandDeg = CaptureSwingHipClampDeg(hipBoundDeg,
            deliveredFrameDeg - captureLastIkThighDeg);
        hipCommandDeg = Mathf.Clamp(hipCommandDeg + postureBiasDeg,
            hipBoundDeg, CaptureSwingHipHoldMaxDeg);
        ApplyCaptureHip(left, hipCommandDeg);
        // Swing knee is never a loaded support, even if the toe still
        // touches. The 1 BW ceiling on a still-down swing is the weld
        // that kept both soles in the closed loop.
        ApplyCaptureKnee(left,
            Mathf.Clamp(captureLastIkKneeDeg, 0f, CaptureSwingKneeCapDeg()),
            false);
        DriveCaptureSwingAnkle(left);
        // ─── WALK CYCLE 6l: publish the pose that was APPLIED, not the ask ───
        // 6k closed the solver: its own returned angles, run forward, land on
        // the ask to the digit on every frame, so the 0.457 m at the plant is
        // made downstream of it. Downstream has two halves and the 6j/6k
        // columns cannot tell them apart - the FRAME the returned WORLD angles
        // are written into a joint command against (the plan's pelvis tilt at
        // the plant: -3.00 deg, frozen, on a body measured 22-35 deg down) and
        // the CLAMPS/TRACKING between those angles and the targets the joints
        // are actually given. This block rebuilds 6k's own two-link forward
        // kinematics on:
        //   * the MEASURED parent - the hip hinge's parent body rotation, the
        //     frame jointAngle = parent - child is really incremented in, so
        //     the thigh world the driven hip command produces is parent - hip;
        //   * the POST-CLAMP targets ApplyCaptureHip / ApplyCaptureKnee just
        //     handed the PD - the tap holds exactly those (after the hip bound
        //     and the knee support bound), not the solver's raw ask.
        // With them published, the CSV splits the 6k delivery by subtraction:
        //     solvedSole - appliedSole   = the clamps / tracking
        //     appliedSole - measuredSole = the parent frame
        // The hinge point is re-read here rather than carried out of the branch
        // above: nothing in this method moves the hinge between that read and
        // this one (no command of this frame has reached physics yet), so it is
        // the same point 6k published as captureSwingSolveFrameX and the two
        // frames subtract directly. Read-only: no gain, limit, threshold, sign
        // or target in this file reads any of these fields back.
        if (capturePlan.valid && hip != null && hip.connectedBody != null)
        {
            Vector2 appliedHinge = hip.transform.TransformPoint(hip.anchor);
            // 6m: the same frame the hip command above was written against, so
            // the published frame IS the frame the applied pose was built in.
            // 6n: that read is now the method's own single read
            // (deliveredFrameDeg, taken before the ask was built and bounded),
            // so the published frame and the frame the 6n allowance and the ask
            // bound used are one variable and not two reads that happen to
            // agree. Nothing between the read and this line moves the pelvis
            // (no command of this frame has reached physics yet), so the value
            // is the one 6l published.
            captureLastSwingAppliedFrameX = appliedHinge.x;
            captureLastSwingAppliedFrameAngleDeg = deliveredFrameDeg;
            // The tap is NaN on a joint no drive method wrote this frame (it is
            // cleared at the top of every walk tick), so a NaN here is the same
            // "no applied pose" case 6k answers with its 0 sentinel.
            float hipAppliedDeg = left ? walkTap.reqHipL : walkTap.reqHipR;
            float kneeAppliedDeg = left ? walkTap.reqKneeL : walkTap.reqKneeR;
            if (!float.IsNaN(hipAppliedDeg) && !float.IsNaN(kneeAppliedDeg))
            {
                float thighAppliedDeg = deliveredFrameDeg - hipAppliedDeg;
                captureLastSwingAppliedSoleX =
                    CaptureSolvedAnklePoint(appliedHinge, thighAppliedDeg,
                        kneeAppliedDeg).x;
                captureLastSwingAppliedResidualX =
                    captureLastSwingAskX - captureLastSwingAppliedSoleX;
            }
            else
            {
                captureLastSwingAppliedSoleX = 0f;
                captureLastSwingAppliedResidualX = 0f;
            }
        }
    }

    // Swing ankle owns foot orientation only. jointAngle = shin - foot, so
    // the measured shin tilt is world-flat and subtracting toe-up cocks the
    // heel for a heel-first plant. Plus on this joint is plantarflexion.
    private void DriveCaptureSwingAnkle(bool left)
    {
        // Landing cock (shin − pitch, heel down) while the toe still
        // scrapes puts the caudal corner back on the dirt — SwingLeftGround
        // never latches and the 3 cm rocker is the only lift (burn_walk
        // swingFoot 0.62 / swap 2). Keep the peel plantarflex until the
        // sole is actually up. Stance MTP stays 0 (not global toe-off).
        if (!captureSwingLeftGround && CaptureFootTouching(left))
        {
            DriveCaptureDirtFoldAnkle(left, 1f);
            return;
        }
        HingeJoint2D joint = left ? leftAnkleJoint : rightAnkleJoint;
        Muscle flexor = left ? leftAnkleFlexor : rightAnkleFlexor;
        Muscle extensor = left ? leftAnkleExtensor : rightAnkleExtensor;
        if (joint == null || flexor == null || extensor == null) return;
        float shinDeg = joint.connectedBody != null
            ? Mathf.DeltaAngle(0f, joint.connectedBody.rotation)
            : 0f;
        float phase = capturePlan.valid && capturePlan.duration > 0.001f
            ? Mathf.Clamp01(captureSwingAge / capturePlan.duration)
            : 1f;
        // World-flat (toeUp = 0) until phase 0.5 welds a still-down heel to
        // the slab: the hip/knee peel 7 cm while the ankle PD holds the
        // sole at rotation 0. Recovery T = 0.22 s spends its first half
        // in that weld, which is why ph=3 never set captureSwingLeftGround.
        // Cock only after the sole has been up.
        float toeUp = Mathf.Max(0f, captureFootPitchDeg)
                      * Mathf.Clamp01((phase - 0.5f) * 2f);
        if (!captureSwingLeftGround)
            toeUp = Mathf.Max(0f, captureFootPitchDeg);
        // WALK-1: the ankle target this method asks for, before the PD.
        if (left)
        {
            walkTap.reqAnkleL = shinDeg - toeUp;
            walkTap.ownerAnkleL = TapOwnerSwingAnkle;
        }
        else
        {
            walkTap.reqAnkleR = shinDeg - toeUp;
            walkTap.ownerAnkleR = TapOwnerSwingAnkle;
        }
        if (left)
            captureMixer.JointStable(joint, shinDeg - toeUp, hipPGain, hipDGain,
                errorReferenceDegrees, flexor, extensor, ref leftAnkleEffectiveInertia);
        else
            captureMixer.JointStable(joint, shinDeg - toeUp, hipPGain, hipDGain,
                errorReferenceDegrees, flexor, extensor, ref rightAnkleEffectiveInertia);
        // WALK-1 completeness: the swing-ankle request into the registry.
        NoteLegAngle(joint, shinDeg - toeUp, captureMixer.lastDemand,
            TapOwnerSwingAnkle);
    }

    // Dirt-fold ankle. jointAngle = shin − foot, so shinDeg is a
    // world-flat plate. Plus on this hinge is plantarflex (heel rise).
    // DualSupport onset lean also calls this (not only fold): Zero
    // until MayOpen left the heel pinned so firstAir never, then
    // UpdateState fallCoMOffset 0.30 at walk start +1.30 s.
    // Subtracting captureFootPitchDeg is the landing
    // cock (heel down) — the opposite peel. Same SPD pair, same
    // gains and signs. Reuses captureToePushDeg (no new switch): that
    // is already the plantarflex pose the stance uses late in T.
    // Command it on the trail only; stance MTP stays 0 (not global
    // toe-off). Muscle activationSpeed 20 is the torque ramp.
    private void DriveCaptureDirtFoldAnkle(bool left, float foldU)
    {
        HingeJoint2D joint = left ? leftAnkleJoint : rightAnkleJoint;
        Muscle flexor = left ? leftAnkleFlexor : rightAnkleFlexor;
        Muscle extensor = left ? leftAnkleExtensor : rightAnkleExtensor;
        if (joint == null || flexor == null || extensor == null) return;
        float shinDeg = joint.connectedBody != null
            ? Mathf.DeltaAngle(0f, joint.connectedBody.rotation)
            : 0f;
        float peel = Mathf.Max(0f, captureToePushDeg);
        float want = shinDeg + peel;
        float held = float.IsNaN(captureDirtFoldAnkleDeg)
            ? joint.jointAngle : captureDirtFoldAnkleDeg;
        // Finish the rocker in ~70 ms, not the 0.20 s fold clock: a
        // world-flat lerp through that clock is the heel pin again.
        float u = Mathf.Clamp01(foldU * 3f);
        if (u < 0.35f) u = 0.35f;
        float target = Mathf.LerpAngle(held, want, u);
        // WALK-1: the folded ankle target this method asks for.
        if (left)
        {
            walkTap.reqAnkleL = target;
            walkTap.ownerAnkleL = TapOwnerDirtFoldAnkle;
        }
        else
        {
            walkTap.reqAnkleR = target;
            walkTap.ownerAnkleR = TapOwnerDirtFoldAnkle;
        }
        if (left)
            captureMixer.JointStable(joint, target, hipPGain, hipDGain,
                errorReferenceDegrees, flexor, extensor, ref leftAnkleEffectiveInertia);
        else
            captureMixer.JointStable(joint, target, hipPGain, hipDGain,
                errorReferenceDegrees, flexor, extensor, ref rightAnkleEffectiveInertia);
        // WALK-1 completeness: the dirt-fold ankle request into the registry.
        NoteLegAngle(joint, target, captureMixer.lastDemand, TapOwnerDirtFoldAnkle);
    }

    // 1 at step onset (DS and early swing): the forward lean that actually
    // moves CoM. 0 at plant: quiet / upright so the pendulum does not grow
    // past the toe. A constant 6° sat back, a constant 9–12° vaulted —
    // capwalk4/5/6, no static middle. Smoothstep over T so the hip is not
    // a step change at freeze.
    private float CaptureLeanPhase()
    {
        // Catch the dump: once |tilt| leaves the Plant band the 9°/10°
        // launch lean follows the pitch into −98° (onset r then heap).
        // Kill forward lean; plant fraction is the upright handoff.
        // Happy-path onset stays under StandingPlantTiltMaxDeg 12°.
        float plantTiltMax = stepPhaseDriver != null
            ? stepPhaseDriver.StandingPlantTiltMaxDeg
            : 12f;
        float dumpDeg = Mathf.Max(plantTiltMax, Mathf.Abs(getUpStandTiltDeg));
        if (vestibularSystem != null
            && Mathf.Abs(vestibularSystem.GetBodyTilt()) >= dumpDeg)
            return Mathf.Clamp(captureLeanPlantFraction, -0.25f, 1f);

        float plant = Mathf.Clamp(captureLeanPlantFraction, -0.25f, 1f);
        switch (captureState)
        {
            case CaptureWalkState.Swing:
            case CaptureWalkState.RecoveryStep:
            {
                float t = capturePlan.valid
                    ? capturePlan.duration
                    : captureStepDuration;
                float u = Mathf.Clamp01(captureSwingAge / Mathf.Max(0.05f, t));
                float fade = u * u * (3f - 2f * u);
                return Mathf.Lerp(1f, plant, fade);
            }
            case CaptureWalkState.TouchdownBlend:
                return plant;
            default:
                // DualSupport. First stacked DS needs the onset lean.
                // After a plant the trail still carries: keeping 1 here
                // is RestoreHip 10° / chest 9° on the new lead while CoM
                // is on the rear — pelvis +8→+21° in 0.1 s, then
                // fallCoMOffset 0.30 at 6.300 s (swap 1). Quiet until
                // the lead has the mass; then lean for the next lift.
                if (captureStepCount == 0) return 1f;
                if (CaptureTrailStillCarries()) return plant;
                return 1f;
        }
    }

    // Walking pelvis world tilt. Forward is minus. Hip HOLD used the
    // measured tilt as the joint target, so P error was identically 0 and
    // the swing-hip reaction sat the trunk. Quiet stance already holds
    // hipBaseAngle (−3). Peak lean is only at onset; plant returns here.
    private float CapturePelvisWorldTarget()
    {
        float dir = CaptureWalkDir();
        float peak = -Mathf.Abs(capturePelvisLeanDeg) * dir;
        float walking = Mathf.Lerp(hipBaseAngle, peak, CaptureLeanPhase());
        return Mathf.Lerp(hipBaseAngle, walking, captureEntryBlend);
    }

    // Loaded hip restores world pelvis tilt. joint = pelvisTarget −
    // measured thigh, so P error is pelvis − target. Passing the
    // measured tilt (HOLD) or the IK thigh once it has arrived is
    // identically zero error: the swing reaction then rocks the
    // pelvis +8→+21° in 0.1 s (capwalk14) or sits it plus (capwalk3).
    private void DriveCaptureRestoreHip(bool left, bool loadedCap)
    {
        tapWriterHip = TapOwnerRestoreHip;
        HingeJoint2D hip = left ? leftHipJoint : rightHipJoint;
        if (hip == null || hip.attachedRigidbody == null) return;
        ApplyCaptureHip(left,
            CaptureBalance.HipHoldTarget(CapturePelvisWorldTarget(),
                hip.attachedRigidbody.rotation),
            loadedCap);
    }

    private void DriveCaptureLandingHip(bool left)
    {
        DriveCaptureRestoreHip(left, CaptureFootLoaded(left));
        // WALK-1: LandingHip IS a restore of the landing thigh's angle, so
        // relabel the write this call just made; the two callers then differ
        // in the table.
        if (left)
        {
            if (walkTap.ownerHipL == TapOwnerRestoreHip)
                walkTap.ownerHipL = TapOwnerLandingHip;
        }
        else if (walkTap.ownerHipR == TapOwnerRestoreHip)
        {
            walkTap.ownerHipR = TapOwnerLandingHip;
        }
    }

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

    private void StepCaptureDirtLiftClock()
    {
        if (captureDirtLiftAge < 0f)
            captureDirtLiftAge = 0f;
        else
            captureDirtLiftAge += Time.fixedDeltaTime;
    }

    private float CaptureDirtLiftU()
    {
        float t = Mathf.Max(0.05f, CaptureDirtLiftSeconds);
        float u = Mathf.Clamp01(Mathf.Max(0f, captureDirtLiftAge) / t);
        return u * u * (3f - 2f * u);
    }

    private float CaptureDirtTakeU()
    {
        float take = Mathf.Clamp01(2f * CaptureDirtLiftU());
        return take * take * (3f - 2f * take);
    }

    private float CaptureDirtFoldU()
    {
        if (captureDirtFoldAge < 0f) return 0f;
        float t = Mathf.Max(0.05f, CaptureDirtFoldSeconds);
        float u = Mathf.Clamp01(captureDirtFoldAge / t);
        return u * u * (3f - 2f * u);
    }

    // First DualSupport: lean, then peel the trail knee about a thigh
    // latched at DualSupport frame 0. Opening fold on the 0.18 s lean
    // clock WITH a Zero hip flexed a sagging trail (chair). Waiting for
    // captureLaunchLead is the freeze: stacked µ=1.2 soles bow, they do
    // not translate (burn_walk 0.0668 m / 40 s / swap 0). DualSupportMax
    // KneeLift of a limp loaded trail is the 6.300 s dump. Fold after
    // lean with the stance hip still up, or the trail already left.
    // Later steps fold as soon as the trail is not carrying.
    private bool CaptureDirtFoldMayOpen(bool stanceIsLeft)
    {
        if (captureDirtFoldAge >= 0f) return true;
        if (captureStepCount > 0) return true;
        float onsetLeanU = CaptureOnsetLeanU();
        if (onsetLeanU < CaptureDirtOnsetLeanReady
            && captureLaunchAge < CaptureDirtOnsetFoldSeconds)
            return false;
        if (captureGait.StateAge < Mathf.Max(0f, captureDoubleSupportMin))
            return false;
        if (!CaptureFootLoaded(!stanceIsLeft)) return true;
        // Do not wait for a 3 cm hip-Y hold. p_auto window 5–6 s drops
        // the pelvis 0.29 m in one second: in the 0.12 s lean clock
        // that is ~3.5 cm, so CaptureDirtLiftDropM 0.03 shut fold on
        // the 9° bow itself. KneeLift never ran, left sole dragged
        // 0.95, firstAir never, then |offset| 0.30 at walk start
        // +1.30 s (c125adbd 6.300 / posed 0 / swap 0). A real chair
        // is firstHeap / captureCollapseDrop 0.30 m. Refusing the
        // peel because the hip moved 3 cm IS the dump.
        return true;
    }

    private void ClearDirtFoldLatch()
    {
        captureDirtFoldAge = -1f;
        captureDirtFoldKneeDeg = float.NaN;
        captureDirtFoldAnkleDeg = float.NaN;
        captureDirtFoldHipDeg = float.NaN;
        captureDirtFoldLiftX = float.NaN;
        captureDirtFoldLiftY = float.NaN;
        captureDirtFoldLiftRelY = float.NaN;
        captureDirtFoldHeelFreed = false;
    }

    private void StepCaptureDirtFoldClock(bool allowFold, HingeJoint2D hip,
                                         HingeJoint2D ankle, float quiet)
    {
        // Once fold has opened, keep folding even if share chatters.
        // Reset is explicit (swap, trailCarries, ResetCaptureWalk).
        if (captureDirtFoldAge >= 0f)
        {
            captureDirtFoldAge += Time.fixedDeltaTime;
            return;
        }
        if (!allowFold) return;
        captureDirtFoldAge = 0f;
        captureDirtFoldKneeDeg = LatchDirtFoldKnee(hip, ankle, quiet);
        captureDirtFoldAnkleDeg = ankle != null ? ankle.jointAngle : 0f;
        // Hip was latched on DualSupport frame 0 (DriveCaptureTrailHipHold).
        // Re-reading here would take a sagged thigh if hold lagged.
        if (float.IsNaN(captureDirtFoldHipDeg))
            captureDirtFoldHipDeg = hip != null ? hip.jointAngle : 0f;
        LatchDirtFoldLiftPoint(hip, ankle);
    }

    // World ankle the airborne peel IK must reach. X is the fold-open
    // hinge, not live: feeding live X is a shuffle. Y is latched as a
    // hip-relative offset so a later stance dip does not chase the
    // knee to 120°, and a holding hip still commands the clearance
    // floor. Live LiftKneeFlexDeg would chase a dropping hip.
    private void LatchDirtFoldLiftPoint(HingeJoint2D hip, HingeJoint2D ankle)
    {
        if (ankle == null)
        {
            captureDirtFoldLiftX = float.NaN;
            captureDirtFoldLiftY = float.NaN;
            captureDirtFoldLiftRelY = float.NaN;
            return;
        }
        Vector2 anklePoint = ankle.transform.TransformPoint(ankle.anchor);
        bool left = ankle == leftAnkleJoint;
        float floorY = CaptureSwingClearanceAnkleY(left, anklePoint);
        captureDirtFoldLiftX = anklePoint.x;
        captureDirtFoldLiftY = floorY;
        if (hip == null)
        {
            captureDirtFoldLiftRelY = float.NaN;
            return;
        }
        float hipY = hip.transform.TransformPoint(hip.anchor).y;
        float maxY = hipY - 0.08f;
        if (captureDirtFoldLiftY > maxY)
            captureDirtFoldLiftY = maxY;
        captureDirtFoldLiftRelY = captureDirtFoldLiftY - hipY;
    }

    // Two-link IK to the latched lift point. Hip may rotate only as far
    // as that point needs (thigh toward vertical). That is not scissor
    // (−swingHipFlex / a forward reach) and not RestoreHip (world pelvis
    // on a second column).
    private bool TryDirtFoldLiftIk(HingeJoint2D hip, out float thighDeg, out float kneeDeg)
    {
        thighDeg = 0f;
        kneeDeg = 0f;
        if (hip == null) return false;
        if (float.IsNaN(captureDirtFoldLiftX) || float.IsNaN(captureDirtFoldLiftRelY))
            return false;
        Vector2 hipPoint = hip.transform.TransformPoint(hip.anchor);
        // Live Y tracks the hip. Clamping to the latched world floor
        // while the hip drops is the 120° chase (shorter chord into
        // the slab). Floor is only the kinematic hip-0.08 / bars cap.
        float y = hipPoint.y + captureDirtFoldLiftRelY;
        float maxY = hipPoint.y - 0.08f;
        float minY = hipPoint.y - (legThighLength + legShinLength - 0.02f);
        if (y > maxY) y = maxY;
        if (y < minY) y = minY;
        return SolveCaptureLegIk(hipPoint, new Vector2(captureDirtFoldLiftX, y),
            out thighDeg, out kneeDeg);
    }

    // Knee flexion whose hip-ankle chord is `dropM` shorter than the
    // straight bars. Fallback when the live strut cannot be measured.
    private float CaptureKneeFlexForHipDrop(float dropM)
    {
        float bars = legThighLength + legShinLength;
        if (bars < 0.1f) return 32f;
        return CaptureKneeFlexForLength(Mathf.Max(0.05f, bars - Mathf.Max(0f, dropM)));
    }

    // Absolute swing-knee target that would raise the sole by the air-path
    // clearance if the stance hip holds height. Latched once: calling
    // LiftKneeFlexDeg every step chases the shortening strut to 120°.
    //
    // 3 cm off the STRAIGHT bars is already spent by an ~19° entry knee
    // (p_auto maxKnee 25.5°, fold never opened; after take the leftover
    // chord is ~1 cm, inside 2×contactOffset). Grounded is heel OR toe,
    // so that millimetre never fires firstAir. walkGeoSwingLiftClearance
    // 0.12 is 2× groundProbeThickness 0.06 so a 2 cm hip drop still
    // leaves the band (burn_stag: no firstAir, fallCoMOffset 0.30 at
    // 6.300 s). Same Max as the airborne path (captureSwingApex).
    private float LatchDirtFoldKnee(HingeJoint2D hip, HingeJoint2D ankle, float quiet)
    {
        float clearance = Mathf.Max(captureSwingApex, walkGeoSwingLiftClearance);
        if (clearance < CaptureDirtLiftDropM)
            clearance = CaptureDirtLiftDropM;
        float fromBars = CaptureKneeFlexForHipDrop(clearance);
        float geo = LiftKneeFlexDeg(hip, ankle, clearance);
        if (geo < 0f)
            geo = fromBars;
        float need = Mathf.Max(geo, fromBars);
        float lo = Mathf.Max(quiet, kneeBaseAngle);
        float hi = Mathf.Min(CaptureDirtFoldKneeMax, 120f);
        if (need < lo) need = lo;
        if (need > hi) need = hi;
        return need;
    }

    // Knee that matches heel-rise about the toe. Plantarflex α on
    // the toe arm raises the ankle arm·sin(α) ≈ 3 cm at 12°. That is
    // NOT the 0.12 m clearance chord: commanding that while the
    // caudal pin is still down is a squat (p_auto 5–6 s). Cached
    // boxes only — this foot's toe, not a leftover left box.
    private float CaptureRockerKneeDeg(bool left, float quiet)
    {
        ResolveCaptureFeetOnce();
        float arm = StanceToeReach();
        BoxCollider2D toe = left ? captureLeftToeBox : captureRightToeBox;
        if (toe != null)
            arm += toe.size.x * 0.5f;
        float peel = Mathf.Max(0f, captureToePushDeg) * Mathf.Deg2Rad;
        float rise = Mathf.Sin(peel) * Mathf.Max(0.05f, arm);
        if (rise < CaptureDirtLiftDropM)
            rise = CaptureDirtLiftDropM;
        float need = CaptureKneeFlexForHipDrop(rise);
        float lo = Mathf.Max(quiet, kneeBaseAngle);
        float hi = Mathf.Min(CaptureDirtFoldKneeMax, 120f);
        if (need < lo) need = lo;
        if (need > hi) need = hi;
        return need;
    }

    // DualSupport peels here, then Gait freezes Swing on caudal rise.
    // While the rear heel corner is still a pin, do not command the
    // 0.12 m clearance IK: that squat into the pin drops the pelvis
    // (p_auto 5–6 s, drop 0.29 m then 0.85 m, swap 0). A quiet locked
    // knee is the other failure: plantarflex against a welded chord is
    // isometric, so the filled heel AABB never empties (burn_walk).
    // Plantarflex + MTP plus + a rocker-matched knee (~3 cm, same as
    // 12° about the ball) lets the caudal corner rise; CoP is already
    // on the stance sole. After the pin opens, in-place lift IK
    // (X frozen, Y hip-relative apex) even if the toe still scrapes —
    // waiting for union-air is the closed loop (burn_walk swingFoot
    // 0.62). Stance RestoreHip + stance knee (≤15°) still hold pelvis
    // height. Not Zero hip, not RestoreHip, not scissor, not global
    // toe-off (stance MTP is 0).
    private void DriveCaptureSwingKneeLift(bool left, bool allowFold = true)
    {
        tapWriterHip = TapOwnerSwingKneeLift;
        tapWriterKnee = TapOwnerSwingKneeLift;
        HingeJoint2D hip = left ? leftHipJoint : rightHipJoint;
        HingeJoint2D ankle = left ? leftAnkleJoint : rightAnkleJoint;
        float quiet = float.IsNaN(captureEntryKneeDeg) ? kneeBaseAngle : captureEntryKneeDeg;
        StepCaptureDirtFoldClock(allowFold, hip, ankle, quiet);
        float hipHold = float.IsNaN(captureDirtFoldHipDeg)
            ? (hip != null ? hip.jointAngle : 0f)
            : captureDirtFoldHipDeg;
        if (CaptureHeelGrounded(left))
        {
            captureDirtFoldHeelFreed = false;
            DriveCaptureDirtFoldAnkle(left, CaptureDirtFoldU());
            ApplyCaptureHip(left, hipHold, false);
            ApplyCaptureKnee(left, CaptureRockerKneeDeg(left, quiet), false);
            return;
        }
        if (!captureDirtFoldHeelFreed)
        {
            captureDirtFoldHeelFreed = true;
            if (ankle != null && !float.IsNaN(captureDirtFoldLiftRelY))
                captureDirtFoldLiftX = ankle.transform.TransformPoint(ankle.anchor).x;
        }
        // Heel pin is open. A remaining toe scrape is clearance, not
        // a second pin: 0.12 m IK into the HEEL pin was the sit
        // (p_auto knee 56°, sag 13.5 cm). Holding the 3 cm rocker
        // until union-air is the other closed loop — the probe is
        // heel OR toe plus 2×contactOffset, so firstAir never
        // latches (burn_walk swingFoot 0.62). In-place lift IK
        // (X frozen, Y hip-relative apex) even if the toe scrapes.
        // Ankle keeps plantarflex until air; landing cock would
        // re-plant the heel. Stance MTP stays 0.
        if (CaptureFootTouching(left))
            DriveCaptureDirtFoldAnkle(left, 1f);
        else
            DriveCaptureSwingAnkle(left);
        float kneeLo = Mathf.Max(quiet, kneeBaseAngle);
        float kneeHi = CaptureInPlaceLiftKneeCapDeg();
        if (TryDirtFoldLiftIk(hip, out float thighDeg, out float kneeDeg)
            && vestibularSystem != null)
        {
            float hipIk = CaptureClampSwingHipDeg(
                vestibularSystem.GetPelvisTilt() - thighDeg);
            ApplyCaptureHip(left, hipIk, false);
            ApplyCaptureKnee(left, Mathf.Clamp(kneeDeg, kneeLo, kneeHi), false);
            return;
        }
        ApplyCaptureHip(left, CaptureClampSwingHipDeg(hipHold), false);
        float clearance = Mathf.Max(captureSwingApex, CaptureDirtLiftDropM);
        float geo = LiftKneeFlexDeg(hip, ankle, clearance);
        if (geo < 0f)
            geo = float.IsNaN(captureDirtFoldKneeDeg) ? kneeHi : captureDirtFoldKneeDeg;
        ApplyCaptureKnee(left, Mathf.Clamp(geo, 0f, kneeHi), false);
    }

    // Stance KNEE holds a chord, not live IK length. Plant IK to a
    // dropping hip is positive feedback: hip down → more flex → more drop
    // (launch 5.3 s: both knees 69/52, then 120). Hip restore still owns
    // world pelvis. First step: quiet strut, then take-half extend
    // toward 0. The walking 0.97 target is 28° and still sits.
    // Later steps cap live IK at that walking chord.
    private void DriveCaptureStancePlant(bool left)
    {
        tapWriterKnee = TapOwnerStancePlant;
        HingeJoint2D hip = left ? leftHipJoint : rightHipJoint;
        HingeJoint2D knee = left ? leftKneeJoint : rightKneeJoint;
        DriveCaptureRestoreHip(left, true);
        float walking = CaptureStanceKneeDeg();
        float quiet = float.IsNaN(captureEntryKneeDeg) ? kneeBaseAngle : captureEntryKneeDeg;
        float kneeTarget = walking;
        if (captureStepCount == 0)
        {
            // Quiet strut, then take-half extend toward 0. The walking
            // 0.97 chord is 28° — Min(plant, 28°) still sits 3.8 cm.
            // Cap 15°: the squat was the stance hinge following the
            // swing fold in the closed chain, not a missing PD.
            kneeTarget = quiet;
            if (captureDirtLiftAge >= 0f)
                kneeTarget = Mathf.Lerp(quiet, 0f, CaptureDirtTakeU());
            kneeTarget = Mathf.Clamp(kneeTarget, 0f, CaptureFirstStanceKneeMaxDeg);
        }
        else if (hip != null)
        {
            Vector2 hipPoint = hip.transform.TransformPoint(hip.anchor);
            Vector2 planted = CapturePlantedAnkleTarget(left);
            if (SolveCaptureLegIk(hipPoint, planted, out float _, out float plantKnee))
                kneeTarget = Mathf.Min(plantKnee, walking);
            else if (knee != null)
                kneeTarget = Mathf.Min(knee.jointAngle, walking);
        }
        ApplyCaptureKnee(left, kneeTarget, true);
    }

    private void DriveCaptureHoldHip(bool left)
    {
        DriveCaptureRestoreHip(left, CaptureFootLoaded(left));
    }

    // Zero SPD error on the measured hip joint. RestoreHip writes
    // pelvisTarget − thighWorld, so both planted hips fight to lean
    // the same pelvis (isometric two-strut). The trail after a plant
    // must keep the thigh it landed with until CoM leaves its plate.
    private void DriveCaptureHoldMeasuredHip(bool left, bool loadedCap)
    {
        tapWriterHip = TapOwnerHoldMeasured;
        HingeJoint2D hip = left ? leftHipJoint : rightHipJoint;
        if (hip == null) return;
        ApplyCaptureHip(left, hip.jointAngle, loadedCap);
    }

    // ─── WALK CYCLE 6e: the LOADED lead hip is a support, not a hold ───
    // Measured on c6d_walk (walk command 5.005 s, fall 7.285 s): in EVERY
    // DualSupport frame the walker handed the hip whose own foot carries the
    // body to DriveCaptureHoldMeasuredHip — a zero-error SPD on the angle the
    // hip is already sagging to — so the loaded lead asked for 0 N*m in
    // exactly the window where the pelvis folded -6.9 -> -67.8 deg and the
    // trunk -2.8 -> -47.7 deg. The CSV is unambiguous: ownerHipL 2 with
    // reqHipL -10.932 at 6.400 (hipL -10.932), reqHipL -55.063 at 6.700
    // (hipL -55.063), reqHipL -65.181 at 6.800 (hipL -65.181) — the request
    // IS the measured angle, so the walker asked nothing while the load was
    // 532-1215 N.
    //
    // The same hip gets the pelvis restore one state later (Swing / Recovery,
    // owner 7), and the loaded-hip cap clips that ask to the measured + 3.0
    // deg = 45 N*m (heelNm 45 / hipMuscleTorque 100 / hipPGain 1.5 /
    // errorReferenceDegrees 10), i.e. 6.500 reqHipL -26.028 against a measured
    // -26.998. So the ask on a loaded hip is ALREADY the walker's ceiling
    // whenever the restore owns it; the handover — 0 N*m or the ceiling — is
    // the only lever the stance-hip chain has, and this is the frame it owns.
    //
    // The guard is not optional. The walk's lean phase is still asking for a
    // forward pelvis (CapturePelvisWorldTarget -10 at 6.400 against a measured
    // -6.93), and that ask, converted to a joint angle, is a 3.07 deg FLEXION:
    // handing it to a loaded hip would be a ~46 N*m push INTO the fold. A
    // loaded hip is asked for the posture it stands in that keeps the trunk
    // over the foot, never for a flexion past the angle it is in.
    //
    // The trail keeps its own call untouched: the two-strut fight is a plant
    // problem (capwalk14: pelvis +8 -> +21 deg in 0.1 s with the trail asleep),
    // not a lead problem, and the trail's latched chord is what stops the
    // rear's length-servo from peeling.
    private void DriveCaptureLoadedStanceHip(bool left)
    {
        HingeJoint2D hip = left ? leftHipJoint : rightHipJoint;
        if (hip == null || hip.attachedRigidbody == null)
        {
            DriveCaptureHoldMeasuredHip(left, true);
            return;
        }
        float ask = CaptureBalance.HipHoldTarget(CapturePelvisWorldTarget(),
            hip.attachedRigidbody.rotation);
        if (ask <= hip.jointAngle)
        {
            DriveCaptureHoldMeasuredHip(left, true);
            return;
        }
        DriveCaptureRestoreHip(left, true);
    }

    // First DualSupport trail hip: SPD to the quiet joint latched on
    // the first walking frame. Zero muscles let the thigh sag; live
    // HoldMeasuredHip follows that sag; RestoreHip is a second column.
    // KneeLift then shortens about this same angle.
    private void DriveCaptureTrailHipHold(bool left)
    {
        tapWriterHip = TapOwnerTrailHipHold;
        HingeJoint2D hip = left ? leftHipJoint : rightHipJoint;
        if (float.IsNaN(captureDirtFoldHipDeg))
            captureDirtFoldHipDeg = hip != null ? hip.jointAngle : 0f;
        ApplyCaptureHip(left, captureDirtFoldHipDeg, false);
    }

    // Snap one leg's hip+knee activations to 0. Called on the NEW swing at
    // freeze, before this step's IK writes the next command.
    private void ZeroCaptureLegMuscles(bool left)
    {
        ActuatorMixer.ZeroLeg(
            left ? leftHipFlexor : rightHipFlexor,
            left ? leftHipExtensor : rightHipExtensor,
            left ? leftKneeFlexor : rightKneeFlexor,
            left ? leftKneeExtensor : rightKneeExtensor);
        // WALK-1: the muscles are snapped to zero, so there is no angle request
        // outstanding. The request column carries the MEASURED joint angle (a
        // held value, not an ask) instead of NaN, and the owner says Zero.
        if (left)
        {
            walkTap.ownerHipL = TapOwnerZero;
            walkTap.ownerKneeL = TapOwnerZero;
            walkTap.reqHipL = leftHipJoint != null ? leftHipJoint.jointAngle : 0f;
            walkTap.reqKneeL = leftKneeJoint != null ? leftKneeJoint.jointAngle : 0f;
            NoteLegTorque(leftHipJoint, 0f, TapOwnerZero);
            NoteLegTorque(leftKneeJoint, 0f, TapOwnerZero);
        }
        else
        {
            walkTap.ownerHipR = TapOwnerZero;
            walkTap.ownerKneeR = TapOwnerZero;
            walkTap.reqHipR = rightHipJoint != null ? rightHipJoint.jointAngle : 0f;
            walkTap.reqKneeR = rightKneeJoint != null ? rightKneeJoint.jointAngle : 0f;
            NoteLegTorque(rightHipJoint, 0f, TapOwnerZero);
            NoteLegTorque(rightKneeJoint, 0f, TapOwnerZero);
        }
    }

    // The plant is a point on the SLAB, not a point on the foot, and not a
    // point below it. Reading the live hinge back as the IK target made the
    // servo track a peeling sole with zero error. Commanding 5 mm UNDER the
    // latch is the opposite failure: a length-servo into the ground, which
    // is the micro-hop (both soles in the air). The latched contact is the
    // target. A risen sole already sits above it, so peel recovery does not
    // need an extra press.
    private Vector2 CapturePlantedAnkleTarget(bool left)
    {
        if (!captureStanceAnchorValid) return CaptureAnklePoint(left);
        return captureStanceAnchor;
    }

    // Follow the loaded sole horizontally (it is where it is, and pulling it
    // sideways only fights friction) and DOWNWARD only: a foot flattening or a
    // body sinking is a real new contact height, a foot rising is the failure.
    // While the sole is off the slab the anchor is frozen — that stale point is
    // exactly what brings the leg back down onto it.
    //
    // During Swing / Recovery the whole point is a contract, not a tracker.
    // Following X while the sole still reports down lets a heel peel carry
    // the target onto the toe; Min(Y) never asks it back onto the original
    // patch, so the servo stays at zero error through the lift. Age is 0
    // on the frame those states start, so that frame still takes the current
    // contact; after that the point is frozen for the rest of the step.
    private void LatchCaptureStanceAnchor(bool left)
    {
        if (!CaptureFootLoaded(left)) return;
        Vector2 ankle = CaptureAnklePoint(left);
        if (!captureStanceAnchorValid)
        {
            captureStanceAnchor = ankle;
            captureStanceAnchorValid = true;
            return;
        }
        bool freeze = (captureState == CaptureWalkState.Swing
                        || captureState == CaptureWalkState.RecoveryStep)
                       && captureStateAge > 0.0001f;
        if (freeze) return;
        captureStanceAnchor.x = ankle.x;
        captureStanceAnchor.y = Mathf.Min(captureStanceAnchor.y, ankle.y);
    }

    // Stance knee owns leg length: the commanded flexion is the one whose
    // two bars span the wanted hip-over-sole distance. Lead sole only.
    // The trail after a forward plant is a longer chord; the same target
    // there is a taut yank that peels the loaded rear.
    private void DriveCaptureLegLengthKnee(bool left)
    {
        tapWriterKnee = TapOwnerLegLengthKnee;
        ApplyCaptureKnee(left, CaptureStanceKneeDeg());
    }

    // Trail knee in double support: zero SPD error on the measured angle,
    // so the chord behind the hip stays the length that already reaches
    // the slab. Do not length-servo it to mid-stance.
    private void DriveCaptureHoldKnee(bool left, bool limitPush = true)
    {
        tapWriterKnee = TapOwnerHoldKnee;
        HingeJoint2D joint = left ? leftKneeJoint : rightKneeJoint;
        if (joint == null) return;
        float target = joint.jointAngle;
        // First stacked DS: zero SPD error on the measured angle lets
        // gravity sag the trail while the stance strut holds. Cap at
        // the quiet entry chord so the pair cannot sit. After a plant the
        // trail may still carry at landing flex; leave that length.
        // limitPush false: pose only, no 1 BW ceiling — a loaded trail
        // knee during onset lean is the second strut that parks CoM.
        if (captureStepCount == 0)
        {
            float quiet = float.IsNaN(captureEntryKneeDeg)
                ? kneeBaseAngle : captureEntryKneeDeg;
            target = Mathf.Min(target, quiet);
        }
        ApplyCaptureKnee(left, target, limitPush);
    }

    // The landing knee changes owner once, over the blend: the same
    // controller walks its target from the IK angle the foot touched down
    // with to the leg length it will hold as stance. The shock flex fades on
    // the same clock, so it is a schedule, not a second regulator.
    private void DriveCaptureLandingKnee(bool left, float blend)
    {
        tapWriterKnee = TapOwnerLandingKnee;
        float b = Mathf.Clamp01(blend);
        float target = Mathf.Lerp(captureLastIkKneeDeg, CaptureStanceKneeDeg(), b)
                       + Mathf.Max(0f, captureLandKneeFlex) * (1f - b);
        ApplyCaptureKnee(left, target);
    }

    private float CaptureStanceKneeDeg()
    {
        float bars = legThighLength + legShinLength;
        if (bars < 0.1f) return kneeBaseAngle;
        float walking = CaptureKneeFlexForLength(
            Mathf.Clamp01(captureStanceLegLength) * bars);
        if (float.IsNaN(captureEntryKneeDeg)) return walking;
        // Shortening the leg is lowering 70 kg: the knee has to ride down, not
        // be handed the bottom of the squat in one physics step.
        return Mathf.Lerp(captureEntryKneeDeg, walking, captureEntryBlend);
    }

    // Knee flexion the body stands in right now, averaged over the pair. The
    // entry ramp leaves from this, so the first walking frame commands the pose
    // the stance controller was already holding.
    private float MeasuredStanceKneeDeg()
    {
        int n = 0;
        float sum = 0f;
        if (leftKneeJoint != null) { sum += leftKneeJoint.jointAngle; n++; }
        if (rightKneeJoint != null) { sum += rightKneeJoint.jointAngle; n++; }
        return n > 0 ? sum / n : kneeBaseAngle;
    }

    // The captureLaunchAnkleCap ceiling is a LAUNCH term, not a walk term: it
    // may only hold the stance ankle down while the launch ramp itself is
    // still ramping. CaptureOnsetLeanU() is that clock (0.12 s of onset lean);
    // from the moment it reaches 1 the same branch already commands the full
    // ceiling (cap = Lerp(launchCap, 1, max(u, onsetLeanU))), so the gate adds
    // no new number and removes no launch behaviour.
    //
    // Before this gate the periodic step used captureLaunchAnkleCap as its own
    // ceiling on every frame of Swing / RecoveryStep / TouchdownBlend
    // (`cap = launchCap` in DriveCaptureCop). walk_place1 (build f9d594f0) is
    // that bug: the CoP loop's raw demand ran 3.4 -> 21.5 while copCmdR sat at
    // exactly 0.2800 and appTorqueAnkleR at 18.2 N*m = 0.28 * 65 for the whole
    // step, i.e. the one actuator that can hold a CoP used 28 % of its
    // authority while the trunk pitched -5 -> -45 deg at 60-70 deg/s.
    private bool CaptureLaunchAnkleCapActive()
    {
        return captureStepCount == 0
               && captureState == CaptureWalkState.DoubleSupport
               && CaptureOnsetLeanU() < 1f;
    }

    // Stance ankle owns the CoP. Existing ankleComP / ankleComD loop.
    // Weight is the load share. copTargetX, when given, is the CoM setpoint
    // (lead sole in dual support); omitted, it is this foot's centre.
    private void DriveCaptureCop(bool left, float dir, Vector2 com, float comVelX,
                                 float weight, float copTargetX = float.NaN)
    {
        HingeJoint2D joint = left ? leftAnkleJoint : rightAnkleJoint;
        Muscle flexor = left ? leftAnkleFlexor : rightAnkleFlexor;
        Muscle extensor = left ? leftAnkleExtensor : rightAnkleExtensor;
        if (joint == null || flexor == null || extensor == null) return;
        float copTarget = float.IsNaN(copTargetX)
            ? CaptureFootCenterX(left)
              + (captureStepCount > 0 ? captureCopLead * dir : 0f)
            : copTargetX;
        float err = com.x - copTarget;
        float errN = err / Mathf.Max(0.01f, comOffsetReference);
        float u = CaptureLaunchU();
        float launchCap = Mathf.Clamp(captureLaunchAnkleCap, 0.05f, 1f);
        float copVDes;
        float cap;
        bool launchDs = captureStepCount == 0
                        && captureState == CaptureWalkState.DoubleSupport;
        bool transferDs = captureStepCount > 0
                          && captureState == CaptureWalkState.DoubleSupport;
        if (launchDs)
        {
            // Onset lean is done in 0.12 s. Holding this cap on
            // captureLaunchSeconds (0.70) left |command| at 0.28 while
            // DualSupport parked at 0.07 m (phase 0 / 35 s / swap 0).
            // After the lean clock the STANCE ankle rolls CoM; the
            // trail stays Zero until fold so it is not a second pin.
            // Lean targets / trail-Zero during that clock are unchanged.
            float launchU = Mathf.Max(u, CaptureOnsetLeanU());
            float vSeek = Mathf.Lerp(0.10f, Mathf.Max(0.10f, captureLaunchSpeed), launchU);
            copVDes = vSeek * dir;
            cap = Mathf.Lerp(launchCap, 1f, launchU);
        }
        else if (transferDs)
        {
            // Post-plant DS must finish the roll onto the lead. v_des=0
            // is the quiet-stance catch: D brakes the leftover speed and
            // CoM never arrives on the lead plate, which is why HoldKnee
            // looked like a weld and KneeLift then dumped the rear (6.3 s).
            float vSeek = Mathf.Max(0.10f, captureLaunchSpeed);
            copVDes = vSeek * dir;
            cap = 1f;
        }
        else
        {
            // Single support / blend: do not fully brake. Match a share
            // of current forward speed so D is quiet on a captured step.
            // Ceiling is the launch cap ONLY inside the launch itself; a
            // loaded stance ankle on the periodic step keeps the full
            // authority the quiet-stance CoP loop has (see
            // CaptureLaunchAnkleCapActive). Plus past the toe is still
            // clipped below. P still pulls toward the setpoint.
            float vFwd = comVelX * dir;
            float vCap = Mathf.Max(0.05f, CaptureDesiredSpeed());
            float fade = Mathf.Clamp01(captureCopTravelBias)
                         * Mathf.Clamp01(Mathf.Max(0f, vFwd) / vCap);
            copVDes = fade * comVelX;
            // Was `cap = launchCap` on every frame of this branch: Swing,
            // RecoveryStep and TouchdownBlend then ran the loaded stance
            // ankle at 0.28 x 65 = 18.2 N*m against a raw demand of 3.4-21.5
            // (walk_place1). The gate is false here by construction — the
            // first DualSupport is owned by launchDs above — so this is the
            // full ceiling, and it keeps the rule in one visible place.
            cap = CaptureLaunchAnkleCapActive() ? launchCap : 1f;
        }
        float velN = (comVelX - copVDes) / Mathf.Max(0.05f, comVelocityReference);
        float raw = ankleComP * errN + ankleComD * velN;
        float command = Mathf.Clamp(raw, -cap, cap)
                        * Mathf.Clamp01(weight) * Mathf.Max(0f, captureCopScale);
        // Plus = increase jointAngle = extensor = plantarflex (toe down,
        // 65 N·m). Minus = flexor = dorsiflex (22 N·m). Once CoM has left
        // the toe, plus is the vault motor, not a brake.
        float toeX = CaptureFootCenterX(left) + StanceToeReach() * dir;
        if ((com.x - toeX) * dir > 0f)
            command = Mathf.Min(command, 0f);
        captureMixer.ApplyAnkle(joint, command, flexor, extensor, ankleLimitMargin);
        // WALK-1: the CoP loop commands TORQUE, not an angle; record which
        // joint it took and the command, and leave reqAnkle* NaN.
        if (left)
        {
            walkTap.copCmdL = command;
            walkTap.ownerAnkleL = TapOwnerCopAnkle;
        }
        else
        {
            walkTap.copCmdR = command;
            walkTap.ownerAnkleR = TapOwnerCopAnkle;
        }
        // WALK-1 completeness: the loop's OWN demand before the cap and the
        // weight scale, so the CSV can tell a ceiling that is too small from a
        // demand that is wrong. Recorded as a torque request (no angle).
        NoteLegTorque(joint, raw, TapOwnerCopAnkle);
    }

    // MTP pose for the capture walker. Zero is a flat plate in mid-stance.
    // Push-off is plus (toe down, heel rise) on the STANCE sole in the last
    // 30 % of T, and on the trailing sole through TouchdownBlend. Lean adds
    // only a tenth of a degree per degree of trunk — gain 1 mapped a 52°
    // dump onto the 50° MTP stop and pinned CoP at the tip. A swing toe
    // may touch with loaded false: that is clearance (minus), not a flat
    // weld and not a second support. Airborne is the same toe-up.
    private void DriveCaptureToes()
    {
        DriveToeJoint(true, CaptureToeTargetDeg(true));
        DriveToeJoint(false, CaptureToeTargetDeg(false));
    }

    private float CaptureToeTargetDeg(bool left)
    {
        if (!CaptureFootTouching(left))
            return -Mathf.Max(0f, toeClearanceDeg);

        bool isStance = CaptureIsStanceFoot(left);
        bool single = !CaptureIsDualSupport;
        // Swing role, even if the toe still touches: clearance, not a
        // flat weld. Returning 0 here sat both soles down in Swing and
        // froze comX — touching is not loaded, and it must not block the
        // swing. TouchdownBlend is dual support: the landing sole is
        // still the swing sign until the swap, but it is planting, so
        // keep it flat (0), not peeled.
        if (CaptureIsSwingFoot(left) && captureState == CaptureWalkState.TouchdownBlend)
            return 0f;
        if (CaptureIsSwingFoot(left) && single)
            return -Mathf.Max(0f, toeClearanceDeg);
        // DualSupport swing: MTP plus while the caudal pin is down so
        // the ball stays the rocker pivot. Minus on that clock unloaded
        // the toe and left the 0.18 m heel box as the only contact
        // (estimator Heel never cleared, burn_walk swap 0). After the
        // pin opens, minus is clearance. Stance MTP stays 0.
        if (CaptureIsSwingFoot(left)
            && captureState == CaptureWalkState.DoubleSupport
            && (captureDirtFoldAge >= 0f
                || (captureStepCount == 0 && CaptureOnsetLeanU() > 0.001f)))
        {
            if (CaptureHeelGrounded(left))
                return Mathf.Max(0f, captureToePushDeg);
            return -Mathf.Max(0f, toeClearanceDeg);
        }

        float target = 0f;
        float dir = CaptureWalkDir();
        float tilt = vestibularSystem != null ? vestibularSystem.GetBodyTilt() : 0f;
        float forward = dir >= 0f ? Mathf.Max(0f, -tilt) : Mathf.Max(0f, tilt);
        target += forward * Mathf.Max(0f, captureToeLeanGain);

        if (isStance && captureState == CaptureWalkState.TouchdownBlend)
        {
            // Trailing sole, load already on the landing foot: this is
            // toe-off. Full push, not a hold from the first frame of Swing.
            target = Mathf.Max(target, Mathf.Max(0f, captureToePushDeg));
        }
        else if (isStance
                 && (captureState == CaptureWalkState.Swing
                     || captureState == CaptureWalkState.RecoveryStep))
        {
            float duration = capturePlan.valid
                ? capturePlan.duration
                : captureStepDuration;
            float T = Mathf.Max(0.05f, duration);
            // Last 30 % of T: heel rise as the CoM passes the ball. Plus
            // from frame 0 of Swing stood the body on its toes, shortened
            // the support, and dumped the trunk 81°.
            float pushBlend = Mathf.InverseLerp(0.70f, 1f, captureStateAge / T);
            target = Mathf.Max(target, pushBlend * Mathf.Max(0f, captureToePushDeg));
        }
        return target;
    }

    private void ApplyCaptureHip(bool left, float targetDeg)
    {
        ApplyCaptureHip(left, targetDeg, false);
    }

    private void ApplyCaptureHip(bool left, float targetDeg, bool loaded)
    {
        HingeJoint2D joint = left ? leftHipJoint : rightHipJoint;
        Muscle flexor = left ? leftHipFlexor : rightHipFlexor;
        Muscle extensor = left ? leftHipExtensor : rightHipExtensor;
        if (joint == null || flexor == null || extensor == null) return;
        if (loaded)
        {
            targetDeg = ActuatorMixer.CapLoadedHip(joint, flexor, extensor, targetDeg,
                new ActuatorMixer.HipCap
                {
                    heelNm = captureStanceHipTorqueCap,
                    toeNm = captureStanceHipTorqueCapForward,
                    pGain = hipPGain,
                    errorRef = errorReferenceDegrees
                });
        }
        // WALK-1: the target actually handed to the PD, after the load cap,
        // next to the method that asked for it.
        if (left)
        {
            walkTap.reqHipL = targetDeg;
            walkTap.ownerHipL = tapWriterHip;
        }
        else
        {
            walkTap.reqHipR = targetDeg;
            walkTap.ownerHipR = tapWriterHip;
        }
        if (left)
            captureMixer.JointStable(joint, targetDeg, hipPGain, hipDGain,
                errorReferenceDegrees, flexor, extensor, ref leftHipEffectiveInertia);
        else
            captureMixer.JointStable(joint, targetDeg, hipPGain, hipDGain,
                errorReferenceDegrees, flexor, extensor, ref rightHipEffectiveInertia);
        // WALK-1 completeness: the same request into the controller registry,
        // so the row still names the owner on a frame the walker does not tick.
        NoteLegAngle(joint, targetDeg, captureMixer.lastDemand, tapWriterHip);
    }

    private void ApplyCaptureKnee(bool left, float targetDeg, bool limitPush = true)
    {
        HingeJoint2D joint = left ? leftKneeJoint : rightKneeJoint;
        Muscle flexor = left ? leftKneeFlexor : rightKneeFlexor;
        Muscle extensor = left ? leftKneeExtensor : rightKneeExtensor;
        if (joint == null || flexor == null || extensor == null) return;
        targetDeg = CaptureSupportKneeTarget(left, joint, targetDeg, limitPush);
        // WALK-1: the target after the loaded-knee bounds.
        if (left)
        {
            walkTap.reqKneeL = targetDeg;
            walkTap.ownerKneeL = tapWriterKnee;
        }
        else
        {
            walkTap.reqKneeR = targetDeg;
            walkTap.ownerKneeR = tapWriterKnee;
        }
        if (left)
            captureMixer.JointStable(joint, targetDeg, kneePGain, kneeDGain,
                errorReferenceDegrees, flexor, extensor, ref leftKneeEffectiveInertia);
        else
            captureMixer.JointStable(joint, targetDeg, kneePGain, kneeDGain,
                errorReferenceDegrees, flexor, extensor, ref rightKneeEffectiveInertia);
        // WALK-1 completeness: the knee request into the controller registry.
        NoteLegAngle(joint, targetDeg, captureMixer.lastDemand, tapWriterKnee);
    }

    // Everything this walker is allowed to do to a LOADED knee. Two bounds,
    // both written on the TARGET the existing knee SPD is handed — no gain, no
    // torque sign, no muscle and no ankle is touched here.
    //
    // Force. jointAngle = thigh - shin and knee flexion is plus, so the command
    // that straightens the leg is a target BELOW the measured angle, and a
    // 1.2 / 10 deg servo saturates at about 8 deg of it. Eight degrees of sag is
    // therefore not a settling leg but 130 N*m on a 6 cm arm = three body
    // weights, and in double support two of them. The ceiling converts the
    // wanted push of captureLegPushCap body weights into the largest
    // straightening error that may be asked for. Holding the body needs exactly
    // 1/captureLegPushCap of that, so a settled stance leg never meets the
    // ceiling: it clips the transient, not the support.
    //
    // Rate. captureLandKneeFlex 12 deg removed over the 0.09 s blend is 133 deg/s
    // of commanded straightening on the leg that has just accepted the whole
    // body, and past T the swing IK keeps pressing its own sole captureFootPress
    // below the slab — both are leg presses against the ground, both bounded here.
    //
    // In the air neither bound applies: a swing leg cannot push the slab, and it
    // needs its full reach.
    private float CaptureSupportKneeTarget(bool left, HingeJoint2D joint, float targetDeg,
                                           bool limitPush = true)
    {
        // The 1 BW ceiling limits STRAIGHTENING (Max(target, measured-ceiling)).
        // It does not command extra flex on a plant that is ON the slab — that
        // was the 03:47 reading of a 5 mm below-slab target with the cap on,
        // which then turned the cap off and made the press a hop. Plant IK
        // toward a frozen contact still needs the cap: without it a millimetre
        // of commanded penetration is three body weights. In the air neither
        // bound applies: a swing leg cannot push the slab, and it needs its
        // full reach.
        // Swing peel (limitPush false) must reach the clearance knee.
        // Falling through set held=true and the 90 deg/s straighten
        // floor on a still-down sole; the swing is not a support.
        if (!limitPush || !CaptureFootLoaded(left))
        {
            if (left) captureLeftSupportKneeHeld = false;
            else captureRightSupportKneeHeld = false;
            return targetDeg;
        }

        float measured = joint.jointAngle + joint.jointSpeed * Time.fixedDeltaTime;
        float bounded = targetDeg;
        if (limitPush)
        {
            float ceiling = CaptureKneeStraightenCeilingDeg(left, measured);
            bounded = Mathf.Max(targetDeg, measured - ceiling);
            // Straighten cap cannot stop extra flex. Walking chord + 6°
            // is 34° / 3.8 cm — over the 3 cm launch budget. First step
            // holds the quiet entry (~6° + 6° = 12°, drop ~0.5 cm).
            // Swing uses limitPush false and keeps reach.
            float flexCap = CaptureStanceKneeDeg() + 6f;
            if (captureStepCount == 0)
            {
                float quiet = float.IsNaN(captureEntryKneeDeg)
                    ? kneeBaseAngle : captureEntryKneeDeg;
                flexCap = Mathf.Min(quiet + 6f, CaptureFirstStanceKneeMaxDeg);
            }
            bounded = Mathf.Min(bounded, flexCap);
        }

        float rate = Mathf.Max(0f, captureSupportKneeExtendRate);
        bool held = left ? captureLeftSupportKneeHeld : captureRightSupportKneeHeld;
        if (held && rate > 0f)
        {
            float last = left ? captureLeftSupportKneeTarget : captureRightSupportKneeTarget;
            bounded = Mathf.Max(bounded, last - rate * Time.fixedDeltaTime);
        }
        else if (!limitPush && rate > 0f)
        {
            bounded = Mathf.Max(bounded, measured - rate * Time.fixedDeltaTime);
        }

        if (left)
        {
            captureLeftSupportKneeTarget = bounded;
            captureLeftSupportKneeHeld = true;
        }
        else
        {
            captureRightSupportKneeTarget = bounded;
            captureRightSupportKneeHeld = true;
        }
        return bounded;
    }

    // Largest straightening error the knee servo may be handed on a loaded leg.
    // Arm of the knee out of the hip-ankle line: the interior angle is
    // 180 - flex, so D^2 = l1^2 + l2^2 + 2*l1*l2*cos(flex) and
    // d = l1*l2*sin(flex)/D. Push along the leg is F = tau/d, so the activation
    // that pushes `share` of captureLegPushCap body weights is F*d / maxTorque,
    // and the servo reaches that activation at error = a*errorRef/pGain.
    // A straight knee gives d -> 0: it also needs no torque to carry the body,
    // so a vanishing ceiling there is the physics, not a lost regulator.
    private float CaptureKneeStraightenCeilingDeg(bool left, float flexDeg)
    {
        Muscle straighten = left ? leftKneeFlexor : rightKneeFlexor;
        float torque = straighten != null ? straighten.maxTorque : 0f;
        float l1 = legThighLength;
        float l2 = legShinLength;
        if (torque <= 0.001f || l1 < 0.0001f || l2 < 0.0001f || kneePGain <= 0.0001f)
            return Mathf.Max(1f, errorReferenceDegrees);

        float flex = Mathf.Clamp(flexDeg, 0f, 170f) * Mathf.Deg2Rad;
        float chord = Mathf.Sqrt(Mathf.Max(0.0001f,
            l1 * l1 + l2 * l2 + 2f * l1 * l2 * Mathf.Cos(flex)));
        float arm = l1 * l2 * Mathf.Sin(flex) / chord;
        // Both soles down: the pair together may push captureLegPushCap, not
        // one each — two legs at the cap is twice the body weight, which is a jump.
        // One sole down: the cap is 1 body weight, hold without a launch.
        // 1.25 was the hop budget that threw the loaded foot during the swing.
        bool pair = CaptureFootLoaded(true) && CaptureFootLoaded(false);
        float share = pair ? 0.5f : 1f;
        float cap = pair ? captureLegPushCap : Mathf.Min(captureLegPushCap, 1f);
        float push = Mathf.Max(0f, cap) * CaptureBodyWeightN() * share;
        float activation = Mathf.Clamp01(push * arm / torque);
        return activation * Mathf.Max(1f, errorReferenceDegrees)
               / Mathf.Max(0.0001f, kneePGain);
    }

    // Same lazy moment as CaptureSoleThickness: the builder sets the masses
    // after AddComponent, so Awake cannot read them. Root has no body of its
    // own, the same filter CenterOfMassCalculator uses.
    private float CaptureBodyWeightN()
    {
        if (captureBodyWeightResolved) return captureBodyWeightN;
        captureBodyWeightResolved = true;
        float mass = 0f;
        Rigidbody2D[] segments = GetComponentsInChildren<Rigidbody2D>();
        for (int i = 0; i < segments.Length; i++)
        {
            if (segments[i] != null && segments[i].gameObject != gameObject)
                mass += segments[i].mass;
        }
        captureBodyWeightN = Mathf.Max(1f, mass) * 9.81f;
        return captureBodyWeightN;
    }

    // Arms are inertia and picture here, not a balancer: the contra-swing is
    // read from the measured thigh split, the same signal the legacy
    // walkArmLegTrack path uses, so this walker needs no phase clock of its
    // own and no new gain.
    private void DriveCaptureArms()
    {
        float norm = 0f;
        if (leftHipJoint != null && rightHipJoint != null && swingHipFlex > 1f)
        {
            norm = Mathf.Clamp(
                (leftHipJoint.jointAngle - rightHipJoint.jointAngle) / swingHipFlex,
                -1f, 1f);
        }
        ApplyWalkArmDelta(walkArmShoulderSwing * norm, Mathf.Abs(norm),
            out float lSh, out float rSh, out float lEl, out float rEl,
            out float lWr, out float rWr);
        ApplyArmPose(lSh, rSh, lEl, rEl, lWr, rWr);
    }

    // Analytic 2-link IK, knee-forward branch. A segment at world rotation
    // r points from its top joint to its bottom one along (sin r, -cos r):
    // rotation 0 is straight down and plus is counterclockwise, which takes
    // the lower end toward +X. So the chord hip->ankle has world angle
    // atan2(dx, -dy), the thigh leads that chord by alpha, and the knee
    // flexion is the full included angle — positive, which is human
    // flexion under jointAngle = parent - child.
    private bool SolveCaptureLegIk(Vector2 hipPoint, Vector2 targetAnkle,
                                   out float thighWorldDeg, out float kneeFlexDeg)
    {
        thighWorldDeg = 0f;
        kneeFlexDeg = 0f;
        float l1 = legThighLength;
        float l2 = legShinLength;
        if (l1 < 0.0001f || l2 < 0.0001f) return false;
        Vector2 r = targetAnkle - hipPoint;
        float dRaw = r.magnitude;
        float d = dRaw;
        if (d < 0.0001f) return false;
        // Never ask for a locked knee or a folded singularity: both are
        // hinge stops, and a stop is not a controllable pose.
        d = Mathf.Clamp(d, Mathf.Abs(l1 - l2) + 0.02f, l1 + l2 - 0.01f);
        // C1 witness (recorder only): the request was outside what the two links
        // can reach, so the pose the caller asked for is not the pose the leg
        // takes. Nothing in this file reads it.
        CaptureIkSaturated = Mathf.Abs(d - dRaw) > 1e-4f;
        CaptureIkSolveTick = Time.frameCount;
        float chordDeg = Mathf.Atan2(r.x, -r.y) * Mathf.Rad2Deg;
        float cosKnee = (l1 * l1 + l2 * l2 - d * d) / (2f * l1 * l2);
        kneeFlexDeg = 180f - Mathf.Acos(Mathf.Clamp(cosKnee, -1f, 1f)) * Mathf.Rad2Deg;
        float cosAlpha = (d * d + l1 * l1 - l2 * l2) / (2f * d * l1);
        float alphaDeg = Mathf.Acos(Mathf.Clamp(cosAlpha, -1f, 1f)) * Mathf.Rad2Deg;
        thighWorldDeg = chordDeg + alphaDeg;
        return true;
    }

    // ─── WALK CYCLE 6k: forward kinematics of the solver's OWN answer ───
    // Same two links and the same segment law as SolveCaptureLegIk: a segment
    // at world rotation r runs from its top joint to its bottom one along
    // (sin r, -cos r), and jointAngle = parent - child puts the shin one
    // kneeFlex behind the thigh. The returned thighWorldDeg is already world,
    // so this is the ankle the solver BELIEVES it commanded - not the
    // measured leg (CaptureAnklePoint). Recorder-only.
    private Vector2 CaptureSolvedAnklePoint(Vector2 hipPoint, float thighWorldDeg,
                                            float kneeFlexDeg)
    {
        float thigh = thighWorldDeg * Mathf.Deg2Rad;
        float shin = (thighWorldDeg - kneeFlexDeg) * Mathf.Deg2Rad;
        return new Vector2(
            hipPoint.x + legThighLength * Mathf.Sin(thigh)
                       + legShinLength * Mathf.Sin(shin),
            hipPoint.y - legThighLength * Mathf.Cos(thigh)
                       - legShinLength * Mathf.Cos(shin));
    }

    // Unloaded swing hip. Quiet hold is near 0; walk flex is minus
    // swingHipFlex (40). The first catch that aimed at the −90 stop
    // sat the pelvis (third law) while the stance strut was still
    // capped at 15°. Later steps may reach a little further. A plan
    // that already sits past the freeze circle must use that extra
    // flex on step 0 too — otherwise ClampToReach's xiLand floor is
    // a hanging sole (no plant) and the 6.3 s overrun repeats.
    // Same SPD, same signs — only the target is bounded.
    private float CaptureClampSwingHipDeg(float targetDeg)
    {
        float flex = Mathf.Max(20f, swingHipFlex);
        bool longCatch = capturePlan.valid
            && (capturePlan.recovery
                || (capturePlan.reachSpan > 0.01f
                    && (capturePlan.targetX - capturePlan.stanceX)
                       * capturePlan.dir
                       > capturePlan.reachSpan + 0.01f));
        float lo = (captureStepCount == 0 && !longCatch)
            ? -flex
            : -(flex + 25f);
        if (targetDeg < lo) return lo;
        if (targetDeg > CaptureSwingHipHoldMaxDeg) return CaptureSwingHipHoldMaxDeg;
        return targetDeg;
    }

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

    private float CaptureSwingHipAllowanceDeg(HingeJoint2D hip, float frameDeg)
    {
        float flex = Mathf.Max(20f, swingHipFlex);
        bool longCatch = capturePlan.valid
            && (capturePlan.recovery
                || (capturePlan.reachSpan > 0.01f
                    && (capturePlan.targetX - capturePlan.stanceX)
                       * capturePlan.dir
                       > capturePlan.reachSpan + 0.01f));
        float planFloor = (captureStepCount == 0 && !longCatch)
            ? -flex
            : -(flex + 25f);
        float planTilt = CapturePelvisWorldTarget();
        float floor = frameDeg - (planTilt - planFloor);
        // A joint with no limits enabled has no stop to read (Unity leaves
        // limits.min at 0 there), so the floor stays a pure frame conversion
        // instead of being turned into a positive number by a limit that is
        // not in force. HumanSegment.ConnectTo assigns useLimits = true with
        // hipLimitMin for every hip, so on this rig the stop always binds.
        if (hip != null && hip.useLimits)
        {
            float stopFloor = hip.limits.min + CaptureSwingHipStopMarginDeg;
            if (floor < stopFloor) floor = stopFloor;
        }
        return floor;
    }

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

    // ─── WALK CYCLE 6p: ONE source for the bounded plan-frame drift ───
    // The 6o charge on the leg's flexion and the 6p answer on the delivered
    // command are the same quantity — the drift between the frame the plan in
    // force was built in and the frame the body is measured in now — so it is
    // read here once, in one place, and both spend it. 0 with no tilt source, no
    // freeze yet (the 0 sentinel of capturePlanFrameTiltDeg), a body at or behind
    // its plan frame (a body that pitched BACK earns no answer and no charge), and
    // never more than CaptureSwingPlanFrameBoundDeg. CaptureSwingPitchBoundDeg's
    // own output is unchanged to the bit by this extraction: allowance + 0 is the
    // allowance, and the ceiling below is applied exactly where it was.
    private float CapturePlanFrameDriftDeg()
    {
        if (vestibularSystem == null) return 0f;
        // 0 is the "no plan frame published yet" sentinel (ResetCaptureWalk),
        // so a walk tick that has never frozen a plan is not bounded by a
        // frame it never had.
        if (captureLastPlanFrameTiltDeg == 0f) return 0f;
        // DeltaAngle(from, to) = to - from, so this is the FREEZE tilt minus the
        // tilt now: positive when the body has moved forward of the frame its
        // target was built in (forward is minus on every tilt in this rig).
        float driftDeg = Mathf.DeltaAngle(vestibularSystem.GetPelvisTilt(),
            captureLastPlanFrameTiltDeg);
        if (driftDeg <= 0f) return 0f;
        if (driftDeg > CaptureSwingPlanFrameBoundDeg)
            driftDeg = CaptureSwingPlanFrameBoundDeg;
        return driftDeg;
    }

    private float CaptureSwingPitchBoundDeg(float allowanceDeg)
    {
        float bound = allowanceDeg + CapturePlanFrameDriftDeg();
        if (bound > CaptureSwingHipHoldMaxDeg) bound = CaptureSwingHipHoldMaxDeg;
        return bound;
    }

    // ─── WALK CYCLE 6p: the bounded posture error, spent as an ANSWER on the
    //     delivered command ───
    // Same drift as the 6o charge above, bounded here by what the walker may ask
    // a joint to answer the posture WITH: its own loaded-hip heel band in degrees
    // (ActuatorMixer.HipTorqueBandDeg, the conversion CapLoadedHip itself uses:
    // captureStanceHipTorqueCap 45 N*m at hipMuscleTorque 100 / hipPGain 1.5 /
    // errorReferenceDegrees 10 = 3.0 deg). That is the one part of the posture
    // error the walker's own numbers already license on a hip; 6p spends it on
    // the hip whose reaction the pelvis feels with no sole to roll (the swing
    // leg, 6f), instead of only using it to retract the step. A cap of 0 ("no
    // cap") leaves the drift itself as the bound, which is the same convention
    // CapLoadedHip has. 0 = no source, no freeze, no positive drift, or muscles
    // that carry no torque to convert.
    private float CaptureSwingPostureBiasDeg(bool left)
    {
        float biasDeg = CapturePlanFrameDriftDeg();
        if (biasDeg <= 0f) return 0f;
        Muscle flexor = left ? leftHipFlexor : rightHipFlexor;
        Muscle extensor = left ? leftHipExtensor : rightHipExtensor;
        float maxTorque = Mathf.Max(flexor != null ? flexor.maxTorque : 0f,
                                    extensor != null ? extensor.maxTorque : 0f);
        if (maxTorque <= 0.001f) return 0f;
        float bandDeg = ActuatorMixer.HipTorqueBandDeg(captureStanceHipTorqueCap,
            maxTorque, hipPGain, errorReferenceDegrees);
        if (biasDeg > bandDeg) biasDeg = bandDeg;
        return biasDeg;
    }

    // The same bound, spent on a command instead of on an ask. Only the floor
    // differs from CaptureClampSwingHipDeg: the walk swing path passes 6n's
    // delivered-frame allowance, whose upper bound (CaptureSwingHipHoldMaxDeg,
    // the +15 hold) is the plan-frame law the other callers keep.
    private float CaptureSwingHipClampDeg(float floorDeg, float targetDeg)
    {
        if (targetDeg < floorDeg) return floorDeg;
        if (targetDeg > CaptureSwingHipHoldMaxDeg) return CaptureSwingHipHoldMaxDeg;
        return targetDeg;
    }

    // The hip flexion the point about to be handed to the solver CARRIES in the
    // frame it is delivered in, deg: that frame minus the world thigh
    // SolveCaptureLegIk returns for the point (more negative = more flexion).
    // This is the PRE-BOUND demand — published next to the bounded ask, so a
    // demand the allowance refuses stays readable in the row instead of being
    // hidden by the bound. Same two links and the same solve as the delivery
    // itself; the C1 witnesses that solve writes are re-written by the caller's
    // own solve of the final point in the same frame, so nothing is left stale.
    // 0 = no pose (singular pair / zero-length links), the sentinel 6k uses.
    private float CaptureSwingHipDemandDeg(Vector2 hipPoint, Vector2 target,
                                           float frameDeg)
    {
        float kneeDeg;
        if (!SolveCaptureLegIk(hipPoint, target, out float thighDeg, out kneeDeg))
            return 0f;
        return frameDeg - thighDeg;
    }

    // ─── WALK CYCLE 6n: the ask, bounded by the allowance, BEFORE the plant ───
    // The allowance has to answer the ask the walker now delivers, and the ask
    // is what the sole is measured against, so it is bounded here — upstream of
    // the solve and the plant — instead of the joint command being clamped
    // downstream of them, where the deficit is silent (6l: 0.5015 m of applied
    // pose lost under a pegged command; 6m: 0.2168 m on the ramp's last rows).
    // The law is the reach disc's own: RETRACT X, NEVER LIFT Y. The handed point
    // keeps the height the walker asked for (the slab under a landing sole, the
    // clearance floor above a peeling one) and comes back toward the hip until
    // the IK's flexion fits the allowance, so the command written from it needs
    // no post-solve shortening and the planted sole is the sole that was asked
    // for. The disc's rear edge is always inside the allowance (there the thigh
    // points BACK, i.e. extension, so the demand is at its most positive), so a
    // bracket exists by construction; the search bisects it and keeps the
    // fitting side as its invariant, which is what makes it safe even where the
    // demand is not monotone in X (near the front edge the chord angle grows
    // with X while the knee straightens, so the demand peaks just inside it).
    // Cost: 10 pure two-link solves per walk frame, bounded.
    private Vector2 SwingPlacementWithinFlexion(Vector2 hipPoint, Vector2 target,
                                                float frameDeg, float allowanceDeg)
    {
        float reach = CaptureSwingReachRadius();
        float dy = target.y - hipPoint.y;
        if (reach <= 0f || Mathf.Abs(dy) >= reach) return target;
        float span = Mathf.Sqrt(reach * reach - dy * dy);
        float lo = hipPoint.x - span;
        float hi = target.x;
        if (hi > hipPoint.x + span) hi = hipPoint.x + span;
        // The ask itself is the bracket's high end and it does NOT fit (the
        // caller only calls this when its demand is outside the allowance), so
        // an ask already at the rear disc edge has nowhere to come back to.
        if (hi <= lo) return target;
        // The bracket's low end must actually fit; the rear edge is extension
        // (its demand is the most positive at this height), so this can only
        // fail when the FRAME itself already sits below the allowance — the
        // pelvis folded past the joint's stop margin. Then no point at this
        // height fits, the ask is left exactly as the plan asked it and the
        // post-solve clamp is the net again (the pre-6n behaviour, with the
        // demand column showing why).
        if (CaptureSwingHipDemandDeg(hipPoint, new Vector2(lo, target.y), frameDeg)
            < allowanceDeg)
            return target;
        for (int i = 0; i < 10; i++)
        {
            float mid = 0.5f * (lo + hi);
            if (CaptureSwingHipDemandDeg(hipPoint, new Vector2(mid, target.y),
                    frameDeg) >= allowanceDeg)
                lo = mid;
            else
                hi = mid;
        }
        return new Vector2(lo, target.y);
    }

    // Unloaded swing knee. 110° is a squat chord (~0.50 m on 0.86 m
    // bars). First step: the walk swing flex (55), not the 100° lift
    // ceiling. Later: dirt-fold 70, still short of the 120 stop.
    // Airborne catch only — DualSupport in-place peel uses
    // CaptureInPlaceLiftKneeCapDeg so 12 cm clearance can exist.
    private float CaptureSwingKneeCapDeg()
    {
        float walk = Mathf.Max(kneeBaseAngle + 4f, swingKneeFlex);
        if (captureStepCount == 0)
            return Mathf.Min(CaptureDirtFoldKneeMax, walk);
        return Mathf.Min(CaptureDirtFoldKneeMax, 80f);
    }

    // Still-touching peel (DualSupport rocker done, or Swing before
    // SwingLeftGround). X is frozen; the knee only shortens the strut.
    private float CaptureInPlaceLiftKneeCapDeg()
    {
        if (captureStepCount == 0)
            return CaptureDirtFoldKneeMax;
        return CaptureDirtFoldLiftKneeMax;
    }

    // Hip–ankle length at a given flexion. Inverse of
    // CaptureKneeFlexForLength: d² = l1² + l2² + 2 l1 l2 cos(flex).
    private float CaptureLegLengthForKneeFlex(float flexDeg)
    {
        float l1 = legThighLength;
        float l2 = legShinLength;
        if (l1 < 0.0001f || l2 < 0.0001f) return l1 + l2;
        float flex = Mathf.Clamp(flexDeg, 1f, 170f) * Mathf.Deg2Rad;
        return Mathf.Sqrt(l1 * l1 + l2 * l2 + 2f * l1 * l2 * Mathf.Cos(flex));
    }

    // Ankle Y that puts the sole about captureSwingApex above the slab.
    // Adding walkGeoSwingLiftClearance 0.12 onto the live hinge (or
    // onto the slab as if the hinge sat in the dirt) is a 56° knee
    // while the pair still touches — p_auto first-second sag 13.5 cm.
    // Probe 0.06; apex 0.07 plus half the sole clears it. Already-high
    // ankles hold, they do not stack. Cached boxes only.
    private float CaptureSwingClearanceAnkleY(bool left, Vector2 anklePoint)
    {
        float slab = CaptureGroundY(left, anklePoint);
        float clearance = Mathf.Max(captureSwingApex, CaptureDirtLiftDropM);
        float want = slab + CaptureAnkleHeightOnSlab() + clearance;
        if (anklePoint.y > want)
            want = anklePoint.y;
        return want;
    }

    // Peel on dirt: keep the commanded ankle at clearance so DualSupport
    // KneeLift is not written back onto the slab. Airborne plant: that
    // same floor (slab + sole + captureSwingApex 0.07) plus the live-hinge
    // ratchet in CaptureSwingClearanceAnkleY held the third-swing sole
    // 7 cm up. burn_walk / c125adbd: left air 5.840, never returned,
    // GaitStateMachine.DecideSwing late (SwingAge 0.415 > captureStepDuration
    // 0.26 + captureSwingOvertime 0.15) opened RecoveryStep at t=6.220,
    // then |comOffset| 0.30 / getUpFallTime 6.24. Let trajectory landY
    // (and press after T) reach the slab. Same SPD, same signs.
    private float CaptureSwingAnkleYForChord(Vector2 hipPoint, float y, bool left)
    {
        Vector2 ankle = CaptureAnklePoint(left);
        bool peeling = !captureSwingLeftGround && CaptureFootTouching(left);
        if (peeling)
        {
            float liftFloor = CaptureSwingClearanceAnkleY(left, ankle);
            if (y < liftFloor)
                y = liftFloor;
            float peelMax = hipPoint.y - 0.08f;
            if (peelMax > liftFloor && y > peelMax)
                y = peelMax;
            return y;
        }
        float airMax = hipPoint.y - 0.08f;
        if (y > airMax)
            y = airMax;
        return y;
    }

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

    private float CaptureSwingReachRadius()
    {
        float bars = legThighLength + legShinLength;
        return bars > 0.0001f ? bars - CaptureSwingReachReserveM : 0f;
    }

    // Edge of that disc at a given target height: the farthest a land point
    // may sit from hipPoint.x while the sole is still asked for `targetY`.
    // The trajectory takes X and the plan's own landY, so this is the disc
    // edge a latched plan target is pulled back to — the landing depth is
    // preserved, which is what lets the sole reach the slab.
    private float CaptureSwingReachSpanX(Vector2 hipPoint, float targetY)
    {
        float reach = CaptureSwingReachRadius();
        float dy = hipPoint.y - targetY;
        float rest = reach * reach - dy * dy;
        return rest > 0f ? Mathf.Sqrt(rest) : 0f;
    }

    // The point handed to the solver: the requested X, kept when the leg can
    // reach it at the requested height, and otherwise retracted to the edge of
    // the disc AT THAT SAME HEIGHT. Retract X, never lift Y — the landing
    // height is the slab, and it is the only coordinate the swing sole cannot
    // give up. A radial clamp (the nearest point of the disc) answers an
    // over-reach by pulling the point toward the hip, which raises the ankle by
    // the same distance the X came back: auto_walk_c6 swung 0.94 s with the
    // sole 0.180 m over the slab, fzL 0.00 N and no swap, while the IK saturated
    // every frame on reqKnee 17.4920.
    //
    // Y itself moves only when the requested height is outside the disc — a hip
    // that has sagged, or lifted, so far that no X can pay for the drop. The
    // ankle is then asked for the lowest (or highest) point of the disc, i.e.
    // straight under the hip, so the sole still comes down instead of the solve
    // saturating. Same law for the periodic step and for Recovery.
    private Vector2 SwingPlacementWithinReach(Vector2 hipPoint, Vector2 target)
    {
        float reach = CaptureSwingReachRadius();
        if (reach <= 0f) return target;
        float dy = target.y - hipPoint.y;
        if (Mathf.Abs(dy) >= reach)
            return new Vector2(hipPoint.x, hipPoint.y + Mathf.Sign(dy) * reach);
        float span = Mathf.Sqrt(reach * reach - dy * dy);
        return new Vector2(
            Mathf.Clamp(target.x, hipPoint.x - span, hipPoint.x + span),
            target.y);
    }

    // Knee flexion whose two bars span distance d between hip and ankle.
    // Same law of cosines as the IK, without a direction.
    private float CaptureKneeFlexForLength(float d)
    {
        float l1 = legThighLength;
        float l2 = legShinLength;
        if (l1 < 0.0001f || l2 < 0.0001f) return kneeBaseAngle;
        d = Mathf.Clamp(d, Mathf.Abs(l1 - l2) + 0.02f, l1 + l2 - 0.01f);
        float cos = (l1 * l1 + l2 * l2 - d * d) / (2f * l1 * l2);
        return 180f - Mathf.Acos(Mathf.Clamp(cos, -1f, 1f)) * Mathf.Rad2Deg;
    }

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

    private Vector2 CaptureLiveCom()
    {
        // Prefer the estimator sample from this FixedUpdate. Zero tick
        // or a missing component falls back to the rigidbody sum, then
        // the root (that last fallback is the frozen-x failure mode).
        if (bodyState != null && bodyState.sensorTick > 0)
            return bodyState.comPosition;
        if (comCalculator != null)
            return comCalculator.GetCenterOfMass();
        return (Vector2)transform.position;
    }

    // First 3 s of walkActive, one sample per 0.1 s bucket. No alloc.
    // Collapse/posed are the World bits; hipDrop / comDrop / column /
    // reason name which standing term fired (GETUP-1 false positive).
    private void StepCaptureOnsetLog()
    {
        if (stepPhaseDriver == null || !stepPhaseDriver.walkActive)
            return;
        float start = stepPhaseDriver.WalkStartTime;
        if (start < 0f)
            return;
        float age = Time.fixedTime - start;
        if (age < -0.0001f || age > 3.001f)
            return;
        int bucket = Mathf.Clamp(Mathf.FloorToInt(age * 10f + 1e-4f), 0,
            CaptureOnsetLogCapacity - 1);
        if (captureOnsetWritten[bucket])
            return;
        captureOnsetWritten[bucket] = true;
        captureOnsetT[bucket] = age;
        Vector2 com = CaptureLiveCom();
        captureOnsetComX[bucket] = com.x;
        captureOnsetMargin[bucket] = bodyState != null ? bodyState.supportMargin : 0f;
        byte flags = 0;
        if (CaptureWalkCollapsed) flags |= 1;
        if (CaptureWalkPosed) flags |= 2;
        captureOnsetFlags[bucket] = flags;
        captureOnsetHipDrop[bucket] = captureLastHipDrop;
        captureOnsetComDrop[bucket] = captureLastComDrop;
        captureOnsetColumn[bucket] = (byte)(captureLastColumnStanding ? 1 : 0);
        captureOnsetReason[bucket] = captureLastCollapseReason;
        CaptureOnsetLogCount++;
    }

    public bool TryGetCaptureOnsetSample(int index, out float age,
        out bool collapsed, out bool posed, out float comX, out float margin)
    {
        float hipDrop;
        float comDrop;
        bool columnStanding;
        byte reason;
        return TryGetCaptureOnsetSample(index, out age, out collapsed, out posed,
            out comX, out margin, out hipDrop, out comDrop, out columnStanding,
            out reason);
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

    private float CaptureFootCenterX(bool left)
    {
        if (bodyState != null)
            return left ? bodyState.leftFootPosition.x : bodyState.rightFootPosition.x;
        return CaptureAnklePoint(left).x;
    }
}
