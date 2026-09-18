using UnityEngine;

// Quadruped stance. Not BalanceController. Forces only via Muscle activations.
[DefaultExecutionOrder(0)]
public class DogStanceController : MonoBehaviour
{
    public enum StanceState
    {
        Balancing,
        Recovery,
        Falling,
        Fallen,
        GettingUp,
        LeapCrouch,
        LeapPush,
        LeapAir,
        LeapBite,
        Walk
    }

    // World off-camera rest. NpcRest writes this. Not a pose and not a gain.
    [System.NonSerialized]
    public bool energyRest;

    // Left for NpcRest / old scenes. The dog has no keyboard: DogDrive
    // writes Walk / Leap / Bark. Do not hook Input System here again.
    [System.NonSerialized]
    public bool acceptsPlayInput;

    [Header("State")]
    public StanceState currentState = StanceState.Balancing;
    public float recoveryCoMOffset = 0.15f;
    public float fallCoMOffset = 0.30f;

    [Header("CoM / paw")]
    public float comOffsetReference = 0.10f;
    public float comVelocityReference = 0.30f;
    public float pawComP = 12f;
    public float pawComD = 7f;
    public float pawLimitMargin = 4f;
    // pawComP 12 / ref 0.10 m saturates at 0.83 cm. Quiet CoM noise
    // then bang-bangs every paw (same command, all four), which rocks
    // the soles, jumps the support AABB, and looks like stepping.
    // Soft deadzone: 0 = old bang-bang. Walk uses the same helper.
    // dog_ds2 (programmer) measured 0.02 / 0.08 as far too small: the
    // quiet stand fell after 7.07 s (fell true, trunkDrop 0.342, fourFeet
    // 0.637) because the saturated servo rocked the paws, and the walk
    // reared into a 41° rear sit. At 0.10 / 0.50 the same build stands
    // 10 s (fell false, trunkDrop 0.025, fourFeet 1.000) and walks
    // comSpeedX +0.073 with 65 cycles and +1.762 m of travel.
    public float pawComDeadzone = 0.10f;
    public float pawComVelDeadzone = 0.50f;
    // GetContacts can drop a CCD sole for a step while IsTouching is
    // true. A short hold only in quiet stance so stacked L/R paws do
    // not unload/reload. 0 = no hold. Walk / leap stay raw so a swing
    // can leave.
    public float pawGroundHoldSeconds = 0.03f;

    [Header("Chest / pelvis world pitch (deg)")]
    public float chestPGain = 2f;
    public float chestDGain = 0.3f;
    public float chestTargetTilt = -8f;
    public float chestErrorReferenceDegrees = 25f;
    public float pelvisPGain = 2f;
    public float pelvisDGain = 0.3f;
    public float pelvisTargetTilt = 0f;
    public float pelvisErrorReferenceDegrees = 25f;
    public float lumbarPGain = 2f;
    public float lumbarDGain = 0.3f;
    // c40: same joint also holds pelvis world 0. 0 = chest-only (c39).
    public float lumbarPelvisP = 0f;
    public float lumbarPelvisD = 0.3f;
    // Capped restore toward lumbar +8 (chest −8, pelvis 0). Not c29 (toward 0).
    public float lumbarStopP = 0f;
    public float lumbarStopAddCap = 0.40f;
    public float lumbarRestP = 0f;
    public float lumbarRestAddCap = 0.40f;
    // 0 = off. c37 open-gate was a no-op (world already unloads after slam).
    public float lumbarOpenGateDeg = 0f;
    // c38: hold lumbar joint at +8 (chest −8 + pelvis 0). Not c25 (joint 0).
    public bool useLumbarJointRest = false;
    public float lumbarJointRestDeg = 8f;

    [Header("Pelvis height (deg per metre, not AddForce)")]
    // Pre-bent inverted-V (knee/elbow 24): too-low → minus lift opens
    // the fold (bones more vertical, taller). Plus lift on this pose is
    // a crouch and the sploot that followed the A-rake. Do not zero
    // heightP to "fix" knee 0 — that was c41 mixed with columns 0/0.
    public float heightP = 60f;
    public float heightD = 8f;
    public float heightMaxDeg = 22f;
    public float heightHipMaxDeg = 0f;
    // Contact bounce of a few mm retargets knee/elbow every step and
    // the pose SPD chases it. Soft deadzone: 0 = old always-on lift.
    public float heightDeadzone = 0.01f;
    public float heightVelDeadzone = 0.05f;
    public float startupHoldSeconds = 0f;
    // c26 off all Drive. Here only chest world waits; legs still hold.
    public float lumbarDelaySeconds = 0f;
    public float targetPelvisY;
    public bool hasHeightTarget;
    public float pelvisVelY;
    // Quiet-sit CoM y after the body settles (rake + low vertical
    // speed), four paws down, world space. Not the inverted-V column:
    // spawn pelvis sits ~0.19 m above leftover sit, and Max(measured,
    // geometric chest-column) would arm WalkCollapsed on a quiet plant
    // (human SinkCollapsed). HeightLift still uses targetPelvisY so
    // leftover sit does not retarget that bar.
    public float standComHeight;
    public bool standComHeightValid;
    public float standPelvisY;
    // First-frame inverted-V pelvis / CoM (world). Onset log
    // columnStanding is drop from this column, not the settle bar.
    public float spawnColumnPelvisY;
    public float spawnColumnComY;
    public bool spawnColumnValid;

    [Header("Get up (Fallen → GettingUp → Walk)")]
    [Tooltip("Chest |pitch| that counts as a heap / prone, deg.")]
    public float getUpFallenAbsPitch = 55f;
    [Tooltip("Trunk drop from settle stand that counts as a heap, m.")]
    public float getUpFallenDrop = 0.40f;
    [Tooltip("|chestPitch − chestTarget| below this means upright enough to BeginWalk, deg.")]
    public float getUpStandPitchDeg = 18f;
    [Tooltip("CoM/pelvis drop below this means upright enough to BeginWalk, m.")]
    public float getUpStandDrop = 0.12f;
    [Tooltip("Paws that must be planted to call the dog upright (3, not 4: a trot passes through three-paw frames).")]
    public int getUpStandMinPaws = 3;
    [Tooltip("Paws must stay planted this long before BeginWalk, s.")]
    public float getUpPlantSeconds = 0.20f;
    // PHYSICAL stand reference. getUpStandDrop above was serialised but never
    // read, so GetUpIsStanding could only see grounded paws + pitch — and in a
    // side view a dog lying prone has a level trunk too: dog_gu5 read
    // getUpSuccess true at 4.58 s with the trunk 0.325 m below the standing
    // pose and the paws off the floor (the human fall-flag defect class).
    // These two are the highest trunk pose measured while the dog was provably
    // on its paws (TryCaptureGetUpStandTrunk), and a fall NEVER clears them:
    // they are what the rise is measured against.
    public float getUpStandChestY;
    public float getUpStandPelvisY;
    public bool getUpStandTrunkValid;
    // Tuck then rise — DriveQuietPlant alone left attempts=1 and a
    // −98° freeze to run end (same hole as human GETUP-1). Local
    // overrides only; do not retune quiet hipP / kneeP.
    // dog_w73: measured post-fall hip ~48° while knee only ~35° —
    // simultaneous hip+knee tuck starved the stifle. Knee leads;
    // hip waits getUpKneeLeadSeconds. Targets match that travel.
    [Tooltip("Extra hip/shoulder tuck past the quiet sit while GettingUp, deg.")]
    public float getUpHipFlex = 48f;
    [Tooltip("Knee/elbow tuck target while GettingUp, deg.")]
    public float getUpKneeFlex = 95f;
    [Tooltip("Seconds of knee/elbow tuck before hip/shoulder join the pike.")]
    public float getUpKneeLeadSeconds = 0.20f;
    [Tooltip("Feet must stay down this long before the pike latches, s.")]
    public float getUpTuckMinSeconds = 0.15f;
    [Tooltip("Trunk drop below the measured stand bar that counts as a heap the tuck must work from with NO paw on the floor, m. Without it the pike waited for a grounded paw, and a dog lying on its pelvis (0 paws grounded, pitch +49) held the quiet base pose from 21.4 s to the end of the run (dog_gu12, 18.6 s down, attempts 0).")]
    public float getUpConnectDrop = 0.20f;
    [Tooltip("Seconds the tuck (limbs folded under the trunk) is held before the push, s.")]
    public float getUpTuckSeconds = 0.45f;
    [Tooltip("Seconds the push (the same limbs extending against the floor) is held before the next tuck, s. A heap that does not rise re-tucks, so the dog keeps trying instead of freezing in one pose.")]
    public float getUpPushSeconds = 0.35f;
    [Tooltip("|chestPitch| above this keeps the closed-chain pike, deg.")]
    public float getUpSquatAbsPitch = 35f;
    [Tooltip("Seconds to blend from pike to quiet sit once upright enough.")]
    public float getUpRiseSeconds = 2.0f;
    public int getUpAttempts;
    public bool getUpSuccess;
    public float getUpSeconds = -1f;
    public float getUpFallTime = -1f;
    // Witness for the recorder / CSV: true while the get-up commands the push
    // half of the tuck-push cycle (limbs extending against the floor).
    [System.NonSerialized]
    public bool getUpPushingNow;

    [Header("Limb joint pose")]
    public float hipPGain = 2f;
    public float hipDGain = 0.3f;
    // c20 full-time hipP 4 lost plant. c59: P 8 for 0.25 s holds the
    // rake through the slam (drop 0.237 → 0.193, plant 0.992). P 12
    // and a 0.50 s window both lost plant.
    public float startupHipP = 8f;
    public float startupHipSeconds = 0.25f;
    // Must match Dog.spawnHipAngle / spawnShoulderAngle. Standing
    // inverted-V is hip −12 / shoulder −20 / knee 24 / elbow 24 (paws
    // under the joints, bones ~12° from vertical). A-rake +25 / −25 / 0 / 0
    // splayed straight legs. Leftover −25 / +25 / 0 / 0 was the X.
    // Columns 0/0 lost the plant (c41, c52).
    public float hipBaseAngle = -12f;
    public float shoulderBaseAngle = -20f;
    public float kneePGain = 3f;
    public float kneeDGain = 0.4f;
    public float kneeBaseAngle = 24f;
    public float elbowBaseAngle = 24f;
    // Quiet stance only. SPD still damps rate; P ignores sub-degree
    // chase that reads as a shift of weight. 0 = old always-on P.
    // Walk / leap / tail / jaw pass 0 so moving targets still track.
    public float poseHoldDeadzoneDeg = 1f;
    public float tailPGain = 0.8f;
    public float tailDGain = 0.3f;
    public float tailBaseAngle = 0f;

    [Header("Jaw (deg). Plus opens, 0 is closed.")]
    public float jawPGain = 2.5f;
    public float jawDGain = 0.4f;
    public float jawClosedAngle = 0f;
    public float jawOpenAngle = 35f;
    [Range(0f, 1f)]
    public float jawOpen;
    public bool jawStrikeArmed;
    public float jawAngle;

    [Header("Walk (CoM-capture gait, muscles only, no AddForce)")]
    // Canonical walk is the DOG-3 diagonal trot, not the leftover
    // sit-trot (runSpeed 0, dog_w10 0.286 km/h) and not a front/rear
    // bound. L/R share X, so the support AABB of a diagonal is still
    // front-X plus rear-X; the lift is Y. Bound (both fronts, then
    // both rears) dropped the unsupported end; peeling only one hind
    // of that bound left a 3-leg sit. Half 0: FR+RL plant, FL+RR
    // peel. Half 1: the other diagonal. Walk() onset only writes
    // walkPhase 0. Dual plants minDual (0.06 s), then Arms BOTH
    // swing limbs. Stance diagonal holds both trunk ends, so a
    // two-limb peel is not the bound squat. walkPhase 1 when the
    // rear swing paw is up. Quiet leftover sit already sits on the
    // cranial plate, so PawPd vs stance mid is P-error 0 — comX
    // froze ~0.07 m and DualReady waiting for 3 cm never armed
    // (same DualSupport park as the human 0.069 m / supportMargin
    // 0.14: Sense still writes every step, the paw loop holds the
    // polygon). A 0.30 s WalkDualSupportMax is longer than the
    // 0.229 s half at 5 km/h, so DualSupport ate the stride. Arm
    // at minDual. Hip-hold plus knee-only left the 0.10 m paw in
    // heel-OR-toe (closed chain). After Arm the peel is in-place
    // 2-link IK: paw X frozen, Y hip-relative so HeightLift does
    // not straighten the swing into dirt. Swing paw Zero (DOG-3).
    // Not Zero hip, not 10° tuck, not full flexHip on dirt. First
    // peel stays armed unless the trunk is a belly: the old inner
    // swap|folded|hardDrop reset dual on every 8° stance reaction.
    // DualTakeFinished plus-fold during dual dropped ~0.12 m and
    // abort-on-0.12 reset dual. Swing plant 0 until true air.
    // Swap clocks after that air. Nose-down lean only on front
    // stance — keep it through the peel (0.5× armed cancelled the
    // lean and dumped CoM back onto the swing).
    // Dual does not HeightLift (spawn target opened stance to 0°).
    // Armed height is from the Walk() sit; 3 cm sag opens stance
    // toward 0° (still ≤15° fold). Damper stays at tau.
    // Peel-U freeze at 2.8 cm left distal ~24° of the 80° chord
    // (paw stayed IsTouching, dog_w10 skate). minDual is still
    // 0.06 s so tick-0 does not Arm; the 0.12 m chord then ramps
    // on the Arm clock (0.08 s peel + 0.08 s air fit the 0.229 s
    // half).
    // Swing on the plate AFTER take: latched stifle/elbow shortens
    // the hip-paw chord by ~12 cm (same dirt-contact hole as the
    // human 3 cm bars). +5° caudal hip on a planted swing dumped
    // the pelvis (third law). hold+12° / gait flex 42° only ate
    // ~1–3 cm; the 0.10 m paw stays IsTouching (heel OR toe,
    // contactOffset). ~72° on the
    // SWING pair is that clearance, not the human launch squat
    // (both knees to 70 then 120). Hip P to full flexHip on a
    // planted swing still dumps. Latch the fold once per half,
    // do not chase to 120.
    // Air swing proximal is WalkAirReachSeconds of cranial flex, not
    // WalkCaptureSwingU: that reach wait is the same closed loop
    // as the human swing latch (pose needs travel, travel needs
    // pose) and planted the paw at the sit X. Contact flicker is
    // not air: three raw ungrounded steps latch walkSwungAir.
    // A 5 ms replant must not freeze the leave pose — that plus a
    // 0.15·half clock (~34 ms) completed hops at gone≈0 (dog_w10
    // sit-trot 0.286 km/h). Complete when the swing heel is
    // WalkPlantAheadMinM cranial of the leave. Pose size follows
    // along-facing speed, not desired runSpeed, so a sit does not
    // take the 20° run flex on tick 0. After arm / air / swap,
    // take a floor of the run pose — waiting for 1.4 m/s kept the
    // skate. Chest lean uses the same floor (k=0 used to cancel
    // runChestLean on a sit). Abort if the stance pair plus-folds
    // or the trunk is a belly, not a 3 cm dip during the stifle
    // peel. Paw is CoM grip on the moving stance plate, not a
    // facing conveyor and not a world-X pin at launch+3 cm.
    // dog_w53: Zero paw + hip-relative 12 cm IK after Arm.
    // Hole: the 2-link still ran while heel OR toe was a pin
    // (same as the human CaptureFootTouching rocker). The short
    // chord sat the shared pelvis instead of raising the sole;
    // a 4 cm dirt X-lead was a skate; hipY−8 cm ate leftover-sit
    // clearance (~14 cm hip–paw → ~6 cm lift, inside the 0.10 m
    // pad). firstAir never — leftover sit-trot. dog_w54: 6 cm
    // knee on any contact. Hole: that chord still ran against a
    // flat heel AND toe pin, and IsTouching with 0 manifold
    // points kept the swing "down" (CCD ghost — firstAir never).
    // dog_w55: plus paw first while the heel is in the manifold
    // (entry knee, hip hold). Rocker knee (~6 cm) only after
    // contacts sit on the toe. Empty GetContacts on the armed
    // swing counted as air (no IsTouching ghost). Hole: a CCD
    // sole still sits in 2×contactOffset with 0 points, so
    // firstAir fired in the band; then 12 cm IK with hip gap
    // 2 cm was a chair on leftover sit (hip–paw ~14 cm).
    // dog_w56: touching is heel/toe CORNERS vs the slab, not
    // empty GetContacts. Rocker only on a clear heel + toe
    // kiss. After true air, hold the 6 cm fold and swing the
    // hip cranially — no 12 cm IK. CoP still launch+lead
    // until firstAir.
    // dog_w57: ClassifyPawContacts still ORed a mid/caudal
    // GetContacts point onto a still-flat pad, so plus 0.18
    // never saw a heel leave (2 cm band needs ~37° of paw).
    // Swing peel is corners only. Dual already plus-paws the
    // swing (toe-off while stance extra-extends). Rocker
    // starts when the heel corner is 3 mm above the toe, even
    // if both still sit in the band.
    // dog_w58: that 3 mm gate never arrived. After Arm the
    // swing hip goes to full P on the sit chord, so plus 0.18
    // cannot pitch a loaded pad (closed chain) and
    // StepSwingRockerClock cleared rockerBorn every step the
    // heel stayed in the 2 cm band. 6 cm never ramped,
    // firstAir never, CoP stayed launch+lead — a skate, not a
    // step (same leftover sit-trot). Arm latched the trail
    // rocker on the Arm clock. Hole: 6 cm of knee into a
    // still-flat heel+toe pin is the human DualSupport squat
    // (KneeLift while the heel is in the manifold). dog_w59:
    // after Arm keep Dual-soft hip P and the leftover-sit
    // knee until the heel corner is 3 mm above the toe; plus
    // 0.18 pitches the pad. Then the 6 cm rocker. Front
    // swing stays a third plant until the hind is air.
    // Stance keeps Dual extra-extend through the peel so the
    // girdle holds. Swap is still that plant, not a hip slide.
    // dog_w60: Dual-soft hip still left a full-P sit knee on
    // the trail, so plus 0.18 fought a closed chain (same as
    // the human first-transfer DualSupport squat: loaded
    // knee holds the pin, pelvis sags, firstAir never). Dual
    // and heel-pin: trail knee Dual-soft 0.35 at leftover sit
    // until the heel is 3 mm up; then the 6 cm rocker. Front
    // elbow stays full-P plant until the hind is air.
    // dog_w61: Dual-soft hip AND Dual-soft trail knee is a
    // limp column — gravity plus-folds the sit (human DualSupport
    // knee 110° / sag 0.26 m), heel-rise never. After Arm the
    // trail hip holds leftover sit at full P; the knee extra is
    // a plus TARGET at full P, not a 0.35 gain cut. Dual-soft
    // hip stays Dual-only and trail-only (front third-plant
    // keeps full P). 6 cm still waits for the 3 mm rise.
    // dog_w62: extra plus-knee on that held hip still shortens
    // the hip-paw chord (same DualSupport squat, 8° not 110°).
    // Loaded plus-knee sits the pelvis; plus 0.18 cannot pitch
    // a pad whose ankle is dropping. After Arm the trail hip
    // took a 10° cranial TARGET (more minus) at full P.
    // Hole: more-minus on a planted trail is swing-flex on a
    // pin — DualSupport squat, firstAir never, leftover
    // sit-trot 0.286 km/h. Toe-off is the other sign: hip
    // plus throws the pelvis cranial (same as LeapPush 12°).
    // After Arm the trail hip takes a 10° caudal TARGET
    // (more plus) at full P; the knee stays leftover sit;
    // plus 0.18 still pitches. Dual-soft hip stays Dual-only.
    // 6 cm still waits for the rise. Air still goes minus.
    // dog_w64: that 6 cm was SwingFoldForClearance against the
    // STANDING inverted-V (~63° chair). Stance already opens
    // leftover sit ~24° → quiet 15° − peel lift 6° ≈ 9° after
    // Arm; a welded trail at 24° then jacks into the slab, so
    // 3 mm never comes and plus 0.18 cannot pitch. Dirt rocker
    // is now sit-relative +18° from Walk() entry, born at Arm
    // (matches the stance open, not a 6 cm DualSupport squat).
    // 12 cm free-foot fold still waits for true air.
    // dog_w65: caudal hip 10° on that still-down trail is a
    // caudal skate (foot slide, leftover sit-trot 0.286 km/h).
    // LeapPush plus is an explode with all four planted, not a
    // peel. And SwingRockerU was 0 on the Arm tick (a<=0), so
    // leftover sit stayed while stance already opened 6° — the
    // same jack as the 3 mm wait. After Arm the trail hip HOLDS
    // leftover sit; the +18° dirt rocker is ON from Arm (U=1),
    // not an 0.08 s lerp from 0. Plus 0.18 still pitches. Air
    // still minus. Front still a third plant until hind air.
    // dog_w66: plus 0.18 AFTER Arm cannot pitch a loaded pad
    // (human ankle: torque turns the trunk, not the sole). That
    // pin kept firstAir from arriving (leftover sit-trot). Dual
    // still plus-paws the take. After Arm the trail paw Zeroes.
    // Hip hold leftover sit. +18° ON from Arm. Front still a
    // third plant until hind air. Air still minus.
    // dog_w67: DualReady at minDual Armed at 0.06 s, then Zero
    // on a still-flat pad. Dual-soft hip + plus 0.18 is the only
    // pitch window — full-P hip after Arm cannot rotate the sole.
    // Arm when the trail heel is 3 mm up (or the paw is air),
    // then Zero + +18° on a toe kiss. Ceiling WalkDualSupportMax
    // so Dual cannot eat the 0.229 s half. Tick-0 still waits
    // minDual (CCD air miss is not an Arm).
    // dog_w68: that 0.12 s ceiling and WeightShiftedTowardStance
    // still Armed DualReady on a still-flat pad (Zero = leftover
    // sit-trot 0.286 km/h). DualReady Arms only on heel-rise or
    // trail air after minDual. Dual lean ramps from Dual t=0 so
    // CoM can leave the trail while plus 0.18 still pitches.
    // dog_w72: DualReady Arms on SwingReadyToRocker (heel rose OR
    // heel-free + toe scrape), not HeelRose alone — a flat pad that
    // already rolled onto the toe never made 3 mm heel>toe
    // (leftover sit-trot 0.286 km/h). After Arm, that same toe
    // scrape is TrailCatchSlide: CoP walks at runSpeed and the
    // air streak latches without waiting for union-air (human
    // heelFreedPeel / catchSlide; freeze-X followed the dump to
    // −98°). Lean hard-kills at |pitch|≥20° (human CaptureLean).
    // GettingUp peels limb+lumbar JointFriction (human GETUP weld
    // K left attempts=1); restore on BeginWalk / fall.
    // dog_w73: raise half cadence (stride/speed ≤ 0.20 s) and fight
    // pelvis sag earlier (peel stance lift + HoldDrop 2 cm). GETUP
    // starts at the knees — measured knee only 35° while hip 48°.
    // dog_w74: half ≤ 0.18 s (peel/air 0.06 fit minDual 0.06);
    // HoldDrop 1.5 cm + PeelLift 11° cut girdle sag; walkComMix
    // floor + faster GaitSpeedK floor extend travel past sit-trot.
    // dog_w69: Dual-soft trail still held leftover sit, so Dual-soft
    // 0.35 P fought plus 0.18 and the 3 mm rise never came (Dual
    // park = leftover sit-trot). Digitigrade toe-off is hip plus
    // (caudal) while Dual-soft yields onto the toe. Dual now extra-
    // extends the TRAIL hip the same 4–8° as stance. After Arm the
    // trail hip still HOLDS leftover sit — dog_w64 10° full-P caudal
    // skated a still-flat pad. Front swing stays a third plant.
    public float walkPeriod = 0.40f;
    [Tooltip("Forage trot neck nod, deg. Two dips per period (each diagonal plant). 0 disables. Hunt uses idleHunt instead of this bob. CLI walk stays still (idleWag 0, idleHunt 0).")]
    public float walkHeadBob = 6f;
    [Tooltip("Sit SetFacing: plant-hop then settle. 0 — old instant Look flip.")]
    [Range(0f, 1.5f)]
    public float sitTurnAmp = 1f;
    [Tooltip("Walk SetFacing: plant-hop then resume stride. 0 — old instant Look flip.")]
    [Range(0f, 1.5f)]
    public float walkTurnAmp = 1f;
    [Tooltip("Walk swing: lift limb pictures on the trot diagonal. 0 — old planted pictures.")]
    [Range(0f, 1.5f)]
    public float walkStrideAmp = 1f;
    [Tooltip("Walk withers: Chest Visual heave on the trot clock. 0 — old still trunk.")]
    [Range(0f, 1.5f)]
    public float walkBobAmp = 1f;
    [Tooltip("Walk rump: Pelvis Visual heave on the trot clock. 0 — old still pelvis (rear swing rode a statue).")]
    [Range(0f, 1.5f)]
    public float walkRumpAmp = 1f;
    [Tooltip("Forage Walk tail: first-hinge pose on the trot clock. 0 — old sit 8.5 Hz wag glued onto the trot.")]
    [Range(0f, 1.5f)]
    public float walkTailAmp = 1f;
    [Tooltip("Forage Walk: distal Tail2–Tail5 Visual limp hitch with the trot-clock first-hinge wag. 0 — rest-spring stick (WalkTail already clocks the first hinge; TreeSpring restAngle 0; no distal muscle).")]
    [Range(0f, 1.5f)]
    public float walkDistalTailAmp = 1f;
    [Tooltip("Forage Walk ears: flop on the trot clock. 0 — old walk pin (follow looked like hunt).")]
    [Range(0f, 1.5f)]
    public float walkEarAmp = 1f;
    [Tooltip("Forage Walk tongue + jaw crack on the trot clock. 0 — old closed follow (stiffer than hunt snarl).")]
    [Range(0f, 1.5f)]
    public float walkPantAmp = 1f;
    [Tooltip("Forage Walk whiskers: fan + twitch on the trot clock. 0 — old rest stamps (follow muzzle looked like a hold).")]
    [Range(0f, 1.5f)]
    public float walkWhiskerAmp = 1f;
    [Tooltip("Forage Walk lids: pant squint on the trot clock. 0 — old rest stare (follow eye looked like hunt-lock).")]
    [Range(0f, 1.5f)]
    public float walkSquintAmp = 1f;
    [Tooltip("Forage Walk nose: pant drop on the trot clock. 0 — old rest muzzle (follow snout looked like hunt-lock).")]
    [Range(0f, 1.5f)]
    public float walkNoseAmp = 1f;
    [Tooltip("Forage Walk pupil: pant drop on the trot clock. 0 — old rest eye (follow stare looked like hunt-lock).")]
    [Range(0f, 1.5f)]
    public float walkEyeAmp = 1f;
    [Tooltip("Forage Walk brow: pant crease on the trot clock. 0 — old rest forehead (follow looked like hunt-lock).")]
    [Range(0f, 1.5f)]
    public float walkBrowAmp = 1f;
    [Tooltip("Forage Walk breath: pale puff on the trot clock. 0 — hanging tongue with no air (follow pant looked like a stamp).")]
    [Range(0f, 1.5f)]
    public float walkPuffAmp = 1f;
    [Tooltip("Forage sit pant: pale puff on the idlePant pump. 0 — hanging tongue with no air after EndWalk.")]
    [Range(0f, 1.5f)]
    public float sitPuffAmp = 1f;
    [Tooltip("Forage sit pant: lid slit on the idlePant pump. 0 — round rest eye after EndWalk (tongue+puff still hunt-lock).")]
    [Range(0f, 1.5f)]
    public float sitSquintAmp = 1f;
    [Tooltip("Forage sit pant: snout drop on the idlePant pump. 0 — rest muzzle after EndWalk (tongue+puff+slit still hunt-lock).")]
    [Range(0f, 1.5f)]
    public float sitNoseAmp = 1f;
    [Tooltip("Forage sit pant: pupil drop on the idlePant pump. 0 — rest stare after EndWalk (tongue+puff+slit+snout still hunt-lock).")]
    [Range(0f, 1.5f)]
    public float sitEyeAmp = 1f;
    [Tooltip("Forage sit pant: brow crease on the idlePant pump. 0 — rest forehead after EndWalk (tongue+puff+slit+snout+eye still hunt-lock).")]
    [Range(0f, 1.5f)]
    public float sitBrowAmp = 1f;
    [Tooltip("Forage sit pant: whisker fan on the idlePant pump. 0 — rest stamps after EndWalk (tongue+puff+slit+snout+eye+brow still a hold muzzle).")]
    [Range(0f, 1.5f)]
    public float sitWhiskerAmp = 1f;
    [Tooltip("Forage sit pant: ear flop on the idlePant pump. 0 — rest half-fold after EndWalk (tongue+puff+slit+snout+eye+brow+whisker still a hold head).")]
    [Range(0f, 1.5f)]
    public float sitEarAmp = 1f;
    [Tooltip("Forage sit pant: Pelvis Visual heave on the idlePant pump. 0 — statue rump after EndWalk (chest already heaves; Walk already bobbed the rump).")]
    [Range(0f, 1.5f)]
    public float sitRumpAmp = 1f;
    [Tooltip("Forage sit pant: milder SPD neck drop on the idlePant pump. 0 — statue world-0 neck after EndWalk (jaw/chest/rump already pump; Walk already nods walkHeadBob).")]
    [Range(0f, 1.5f)]
    public float sitHeadAmp = 1f;
    [Tooltip("Forage sit scratch: pupil drop toward the kicking paw. 0 — rest stare on the kick (neck/ear/hind already tuck).")]
    [Range(0f, 1.5f)]
    public float sitScratchEyeAmp = 1f;
    [Tooltip("Forage sit scratch: lid slit on the kick. 0 — round rest eye on the kick (pupil already drops).")]
    [Range(0f, 1.5f)]
    public float sitScratchLidAmp = 1f;
    [Tooltip("Forage sit scratch: brow crease on the kick. 0 — rest forehead on the kick (pupil already drops, lid already slits).")]
    [Range(0f, 1.5f)]
    public float sitScratchBrowAmp = 1f;
    [Tooltip("Forage sit scratch: whisker fan on the kick. 0 — rest stamps on the kick (pupil/lid/brow already move).")]
    [Range(0f, 1.5f)]
    public float sitScratchWhiskerAmp = 1f;
    [Tooltip("Forage sit scratch: snout drop toward the kicking paw. 0 — rest muzzle on the kick (pupil/lid/brow/whisker already move).")]
    [Range(0f, 1.5f)]
    public float sitScratchNoseAmp = 1f;
    [Tooltip("Forage sit scratch: lip tuck toward the kicking paw. 0 — rest flews on the kick (pupil/lid/brow/whisker/snout already move).")]
    [Range(0f, 1.5f)]
    public float sitScratchMouthAmp = 1f;
    [Tooltip("Forage sit scratch: first-hinge droop with the rake. 0 — 8.5 Hz sit metronome (face/hind already kick).")]
    [Range(0f, 1.5f)]
    public float sitScratchTailAmp = 1f;
    [Tooltip("Forage sit scratch: distal Tail2–Tail5 Visual limp hitch with the rake. 0 — rest-spring stick (first hinge already SitScratchTail; TreeSpring restAngle 0; no distal muscle).")]
    [Range(0f, 1.5f)]
    public float sitScratchDistalTailAmp = 1f;
    [Tooltip("Forage sit scratch: Pelvis Visual heave hitch with the rake. 0 — statue rump (hind/face/tail already kick; Walk/pant/yawn/sniff already bobbed the rump).")]
    [Range(0f, 1.5f)]
    public float sitScratchRumpAmp = 1f;
    [Tooltip("Forage sit scratch: Chest Visual heave hitch with the rake. 0 — quiet-sit 0.0018 (hind/face/tail/rump already kick; BreathLift only reads pant/Sleep/walk/yawn/sniff).")]
    [Range(0f, 1.5f)]
    public float sitScratchBreathAmp = 1f;
    [Tooltip("Forage sit look: ear hitch with the neck bob. 0 — rest fold except perk at idleLook > 0.45 (neck already weaves).")]
    [Range(0f, 1.5f)]
    public float sitLookEarAmp = 1f;
    [Tooltip("Forage sit look: whisker fan with the neck bob. 0 — rest stamps (neck/ears already weave).")]
    [Range(0f, 1.5f)]
    public float sitLookWhiskerAmp = 1f;
    [Tooltip("Forage sit look: lid slit with look-down. 0 — round rest eye (pupil/ears/whiskers already weave).")]
    [Range(0f, 1.5f)]
    public float sitLookLidAmp = 1f;
    [Tooltip("Forage sit look: lip hitch with the neck bob. 0 — rest flews (pupil/ears/whiskers/lids already weave).")]
    [Range(0f, 1.5f)]
    public float sitLookMouthAmp = 1f;
    [Tooltip("Forage sit look: first-hinge hitch with the neck bob. 0 — 8.5 Hz sit metronome (face already weaves).")]
    [Range(0f, 1.5f)]
    public float sitLookTailAmp = 1f;
    [Tooltip("Forage sit look: distal Tail2–Tail5 Visual limp hitch with the signed bob. 0 — rest-spring stick (first hinge already SitLookTail; TreeSpring restAngle 0; no distal muscle).")]
    [Range(0f, 1.5f)]
    public float sitLookDistalTailAmp = 1f;
    [Tooltip("Forage sit look: Chest Visual heave hitch with the neck bob. 0 — quiet-sit 0.0018 (neck/face/tail already weave; BreathLift only reads pant/Sleep/walk/yawn/sniff/scratch).")]
    [Range(0f, 1.5f)]
    public float sitLookBreathAmp = 1f;
    [Tooltip("Forage sit look: Pelvis Visual heave hitch with the neck bob. 0 — statue rump (chest already heaves; Walk/pant/yawn/sniff/scratch already bobbed the rump).")]
    [Range(0f, 1.5f)]
    public float sitLookRumpAmp = 1f;
    [Tooltip("Hunt recover sit: Chest Visual coil heave on idleHunt. 0 — quiet-sit 0.0018 (snarl/paw/hackles already live; BreathLift only reads pant/Sleep/walk/yawn/sniff/scratch/look).")]
    [Range(0f, 1.5f)]
    public float sitHuntBreathAmp = 1f;
    [Tooltip("Hunt recover sit: Pelvis Visual coil hitch on idleHunt. 0 — statue rump (chest already heaves; ApplyRump only reads Walk/pant/yawn/sniff/scratch/look).")]
    [Range(0f, 1.5f)]
    public float sitHuntRumpAmp = 1f;
    [Tooltip("Hunt recover sit: SPD neck drop on idleHunt (same 12° as stalk WalkNeckWorldTarget). 0 — statue world-0 neck (chest/rump/snarl already coil; IdleNeckWorldTarget only reads sleep/sniff/scratch/look/yawn/hop/SitHead).")]
    [Range(0f, 1.5f)]
    public float sitHuntHeadAmp = 1f;
    [Tooltip("Hunt recover sit: lid slit on idleHunt. 0 — round rest eye (pupil already drops; SitSquint skips idleHunt).")]
    [Range(0f, 1.5f)]
    public float sitHuntLidAmp = 1f;
    [Tooltip("Hunt recover sit: pale puff on idleHunt, cranial of the fangs. 0 — silent snarl (chest/rump/neck/lid already coil; SitPuff/SitYawnPuff/SitChewPuff skip idleHunt, fangs own Jaw Visual).")]
    [Range(0f, 1.5f)]
    public float sitHuntPuffAmp = 1f;
    [Tooltip("Hunt recover sit: distal Tail2–Tail5 Visual perk hitch with the idleHunt first-hinge raise. 0 — rest-spring stick (IdleTailTarget already raises; TreeSpring restAngle 0; no distal muscle).")]
    [Range(0f, 1.5f)]
    public float sitHuntDistalTailAmp = 1f;
    [Tooltip("Hunt stalk Walk: lid slit on idleHunt. 0 — round rest eye (pupil already drops; WalkSquint skips idleHunt, SitHuntSquint skips Walk).")]
    [Range(0f, 1.5f)]
    public float walkHuntLidAmp = 1f;
    [Tooltip("Hunt stalk Walk: pale puff on idleHunt, cranial of the fangs. 0 — silent snarl (lid/pupil/nose already coil; WalkPuff skips idleHunt, SitHuntPuff skips Walk).")]
    [Range(0f, 1.5f)]
    public float walkHuntPuffAmp = 1f;
    [Tooltip("Hunt stalk Walk: distal Tail2–Tail5 Visual perk hitch with the idleHunt first-hinge raise. 0 — rest-spring stick (IdleTailTarget already raises; TreeSpring restAngle 0; no distal muscle).")]
    [Range(0f, 1.5f)]
    public float walkHuntDistalTailAmp = 1f;
    [Tooltip("Hunt leap: lid slit on IsLeap. 0 — round rest eye (brow already furrows; WalkHuntSquint/SitHuntSquint skip IsLeap; LeapAttack zeros idleHunt).")]
    [Range(0f, 1.5f)]
    public float leapHuntLidAmp = 1f;
    [Tooltip("Hunt leap: pupil drop on IsLeap. 0 — rest-Y stare (lid already slits, brow already furrows; WalkEye/SitEye skip IsLeap; LeapAttack zeros idleHunt).")]
    [Range(0f, 1.5f)]
    public float leapHuntEyeAmp = 1f;
    [Tooltip("Hunt leap: snout drop on IsLeap. 0 — rest muzzle (pupil already drops, brow/lips already IsLeap; WalkNose/SitNose skip IsLeap; LeapAttack zeros idleHunt).")]
    [Range(0f, 1.5f)]
    public float leapHuntNoseAmp = 1f;
    [Tooltip("Hunt leap: pale puff on IsLeap, cranial of the fangs. 0 — silent snarl (lid/pupil/nose already coil; WalkHuntPuff/SitHuntPuff skip IsLeap; LeapAttack zeros idleHunt; fangs own Jaw Visual).")]
    [Range(0f, 1.5f)]
    public float leapHuntPuffAmp = 1f;
    [Tooltip("Hunt leap: Chest Visual coil heave on IsLeap. 0 — statue chest (lid/pupil/nose/puff already coil; BreathLift/SitHuntBreath skip IsLeap; LeapAttack zeros idleHunt).")]
    [Range(0f, 1.5f)]
    public float leapHuntBreathAmp = 1f;
    [Tooltip("Hunt leap: Pelvis Visual coil hitch on IsLeap. 0 — statue rump (chest already heaves; ApplyRump/SitHuntRump skip IsLeap; LeapAttack zeros idleHunt).")]
    [Range(0f, 1.5f)]
    public float leapHuntRumpAmp = 1f;
    [Tooltip("Hunt leap: first-hinge stiff raise on IsLeap (same 16° as idleHunt). 0 — rest tail (chest/rump already hitch; DriveLeap holds tailBaseAngle; LeapAttack zeros idleHunt).")]
    [Range(0f, 1.5f)]
    public float leapHuntTailAmp = 1f;
    [Tooltip("Hunt leap: distal Tail2–Tail5 Visual perk hitch with the first-hinge stiff raise. 0 — rest-spring stick (LeapHuntTail already raises the first hinge; TreeSpring restAngle 0; no distal muscle).")]
    [Range(0f, 1.5f)]
    public float leapHuntDistalTailAmp = 1f;
    [Tooltip("Night Sleep: first-hinge limp droop on idleSleep. 0 — rest tail (chin/lids/ears already sleep; TickSleep zeros idleWag; IdleTailTarget falls to tailBaseAngle).")]
    [Range(0f, 1.5f)]
    public float sitSleepTailAmp = 1f;
    [Tooltip("Night Sleep: Pelvis Visual hitch with the sleep breath on idleSleep. 0 — statue rump (chest already heaves; ApplyRump only reads Walk/pant/yawn/sniff/scratch/look/hunt/leap/chew).")]
    [Range(0f, 1.5f)]
    public float sitSleepRumpAmp = 1f;
    [Tooltip("Night Sleep: snout drop on idleSleep. 0 — rest muzzle (chin/lids/ears/tail/chest/rump/mouth/whisker/brow already sleep; ApplyNose never read idleSleep).")]
    [Range(0f, 1.5f)]
    public float sitSleepNoseAmp = 1f;
    [Tooltip("Night Sleep: ear fold hitch with the sleep breath on idleSleep. 0 — statue fold 0.88 (chin/lids/tail/chest/rump/nose/mouth/whisker/brow already sleep; EarFoldWanted returns 0.88, no SitSleepEar).")]
    [Range(0f, 1.5f)]
    public float sitSleepEarAmp = 1f;
    [Tooltip("Night Sleep: brow droop hitch with the sleep breath on idleSleep. 0 — statue droop (chin/lids/ears/tail/chest/rump/nose/mouth/whisker already sleep; ApplyBrows holds sleep*4, no SitSleepBrow).")]
    [Range(0f, 1.5f)]
    public float sitSleepBrowAmp = 1f;
    [Tooltip("Night Sleep: lip droop hitch with the sleep breath on idleSleep. 0 — statue droop (chin/lids/ears/tail/chest/rump/nose/brows/whisker already sleep; ApplyMouth holds sleep*4, no SitSleepMouth).")]
    [Range(0f, 1.5f)]
    public float sitSleepMouthAmp = 1f;
    [Tooltip("Night Sleep: whisker droop hitch with the sleep breath on idleSleep. 0 — statue droop (chin/lids/ears/tail/chest/rump/nose/brows/mouth already sleep; ApplyWhiskers holds sleep*4, no SitSleepWhisker).")]
    [Range(0f, 1.5f)]
    public float sitSleepWhiskerAmp = 1f;
    [Tooltip("Night Sleep: distal Tail2–Tail5 Visual limp hitch with the sleep breath on idleSleep. 0 — rest-spring stick (first hinge already SitSleepTail; TreeSpring restAngle 0; no distal muscle).")]
    [Range(0f, 1.5f)]
    public float sitSleepDistalTailAmp = 1f;
    [Tooltip("Quiet Forage sit: Pelvis Visual hitch with the quiet-sit 0.0018 chest. 0 — statue rump (chest already heaves; ApplyRump only reads Walk/pant/yawn/sniff/scratch/look/hunt/leap/chew/Sleep).")]
    [Range(0f, 1.5f)]
    public float sitQuietRumpAmp = 1f;
    [Tooltip("Quiet Forage sit: SPD neck hitch with the quiet-sit 0.0018 chest. 0 — statue world-0 neck (chest/rump already hitch; IdleNeckWorldTarget only reads sleep/sniff/scratch/look/yawn/hop/SitHead/SitHuntHead/SitChewHead).")]
    [Range(0f, 1.5f)]
    public float sitQuietHeadAmp = 1f;
    [Tooltip("Quiet Forage sit: ear fold hitch with the quiet-sit 0.0018 chest. 0 — statue rest fold 0.20 (chest/rump/neck already hitch; EarFoldWanted returns 0.20, no SitQuietEar).")]
    [Range(0f, 1.5f)]
    public float sitQuietEarAmp = 1f;
    [Tooltip("Quiet Forage sit: brow hitch with the quiet-sit 0.0018 chest. 0 — rest stamp (chest/rump/neck/ears already hitch; ApplyBrows rest pose, no SitQuietBrow).")]
    [Range(0f, 1.5f)]
    public float sitQuietBrowAmp = 1f;
    [Tooltip("Quiet Forage sit: lip hitch with the quiet-sit 0.0018 chest. 0 — rest flews (chest/rump/neck/ears/brows already hitch; ApplyMouth rest pose, no SitQuietMouth).")]
    [Range(0f, 1.5f)]
    public float sitQuietMouthAmp = 1f;
    [Tooltip("Quiet Forage sit: whisker hitch with the quiet-sit 0.0018 chest. 0 — rest stamps (chest/rump/neck/ears/brows/mouth already hitch; ApplyWhiskers rest pose, no SitQuietWhisker).")]
    [Range(0f, 1.5f)]
    public float sitQuietWhiskerAmp = 1f;
    [Tooltip("Quiet Forage sit: distal Tail2–Tail5 Visual limp hitch with the 8.5 Hz first-hinge wag. 0 — rest-spring stick (IdleTailTarget already wags; TreeSpring restAngle 0; no distal muscle).")]
    [Range(0f, 1.5f)]
    public float sitQuietDistalTailAmp = 1f;
    [Tooltip("Forage sit sniff: first-hinge droop with the dirt bob. 0 — 8.5 Hz sit metronome (nose already tracks).")]
    [Range(0f, 1.5f)]
    public float sitSniffTailAmp = 1f;
    [Tooltip("Forage sit sniff: distal Tail2–Tail5 Visual limp hitch with the dirt bob. 0 — rest-spring stick (first hinge already SitSniffTail; TreeSpring restAngle 0; no distal muscle).")]
    [Range(0f, 1.5f)]
    public float sitSniffDistalTailAmp = 1f;
    [Tooltip("Forage sit sniff: Chest Visual heave hitch with the dirt bob. 0 — quiet-sit 0.0018 (nose/paw/tail already track).")]
    [Range(0f, 1.5f)]
    public float sitSniffBreathAmp = 1f;
    [Tooltip("Forage sit sniff: Pelvis Visual heave hitch with the dirt bob. 0 — statue rump (chest already heaves; Walk/pant/yawn already bobbed the rump).")]
    [Range(0f, 1.5f)]
    public float sitSniffRumpAmp = 1f;
    [Tooltip("Forage sit chew: Chest Visual hitch with the jaw snap. 0 — quiet-sit 0.0018 (jaw/morsel/paw already chew; SitSniffBreath skips wag-off sniff).")]
    [Range(0f, 1.5f)]
    public float sitChewBreathAmp = 1f;
    [Tooltip("Forage sit chew: Pelvis Visual hitch with the jaw snap. 0 — statue rump (chest already heaves; SitSniffRump skips wag-off sniff).")]
    [Range(0f, 1.5f)]
    public float sitChewRumpAmp = 1f;
    [Tooltip("Forage sit chew: first-hinge droop hitch with the jaw snap. 0 — rest tail (chest/rump already hitch; chew zeros wag, no SitChewTail).")]
    [Range(0f, 1.5f)]
    public float sitChewTailAmp = 1f;
    [Tooltip("Forage sit chew: distal Tail2–Tail5 Visual limp hitch with the jaw snap. 0 — rest-spring stick (first hinge already SitChewTail; TreeSpring restAngle 0; no distal muscle).")]
    [Range(0f, 1.5f)]
    public float sitChewDistalTailAmp = 1f;
    [Tooltip("Forage sit chew: lid slit hitch with the jaw snap. 0 — open sniff-track eye (jaw/chest/rump/tail already chew; SitSquint skips idleChew, no SitChewSquint).")]
    [Range(0f, 1.5f)]
    public float sitChewLidAmp = 1f;
    [Tooltip("Forage sit chew: ear fold hitch with the jaw snap. 0 — statue sniff half-fold 0.42 (jaw/chest/rump/tail/lid already chew; EarFoldWanted sniff before idleChew, no SitChewEar).")]
    [Range(0f, 1.5f)]
    public float sitChewEarAmp = 1f;
    [Tooltip("Forage sit chew: brow crease hitch with the jaw snap. 0 — statue sniff furrow (jaw/chest/rump/tail/lid/ear already chew; ApplyBrows reads idleSniff, WalkBrow/SitBrow skip idleChew, no SitChewBrow).")]
    [Range(0f, 1.5f)]
    public float sitChewBrowAmp = 1f;
    [Tooltip("Forage sit chew: whisker fan hitch with the jaw snap. 0 — sniff fan+11 Hz (jaw/chest/rump/tail/lid/ear/brow already chew; ApplyWhiskers reads idleSniff, WalkWhisker/SitWhisker skip idleChew, no SitChewWhisker).")]
    [Range(0f, 1.5f)]
    public float sitChewWhiskerAmp = 1f;
    [Tooltip("Forage sit chew: SPD neck hitch with the jaw snap. 0 — held sniff 16° (jaw/chest/rump/tail/lid/ear/brow/whisker already chew; IdleNeckWorldTarget reads idleSniff, SitHead skips idleChew, no SitChewHead).")]
    [Range(0f, 1.5f)]
    public float sitChewHeadAmp = 1f;
    [Tooltip("Forage sit chew: snout drop hitch with the jaw snap. 0 — held sniff drop (jaw/chest/rump/tail/lid/ear/brow/whisker/neck already chew; ApplyNose reads idleSniff, WalkNose/SitNose skip idleChew, no SitChewNose).")]
    [Range(0f, 1.5f)]
    public float sitChewNoseAmp = 1f;
    [Tooltip("Forage sit chew: lip tuck hitch with the jaw snap. 0 — held sniff tuck (jaw/chest/rump/tail/lid/ear/brow/whisker/neck/nose already chew; ApplyMouth reads idleSniff, no SitChewMouth).")]
    [Range(0f, 1.5f)]
    public float sitChewMouthAmp = 1f;
    [Tooltip("Forage sit chew: pupil drop hitch with the jaw snap. 0 — held sniff drop (jaw/chest/rump/tail/lid/ear/brow/whisker/neck/nose/lip already chew; ApplyEyes reads idleSniff, WalkEye/SitEye skip idleChew, no SitChewEye).")]
    [Range(0f, 1.5f)]
    public float sitChewEyeAmp = 1f;
    [Tooltip("Forage sit chew: pale puff hitch with the jaw snap, cranial of the morsel. 0 — silent lump (jaw/chest/rump/tail/lid/ear/brow/whisker/neck/nose/lip/eye already chew; SitYawnPuff/SitPuff skip idleChew, morsel owns Jaw Visual, no SitChewPuff).")]
    [Range(0f, 1.5f)]
    public float sitChewPuffAmp = 1f;
    [Tooltip("Forage sit yawn: first-hinge droop with the gape. 0 — 8.5 Hz sit metronome (jaw/head already stretch).")]
    [Range(0f, 1.5f)]
    public float sitYawnTailAmp = 1f;
    [Tooltip("Forage sit yawn: distal Tail2–Tail5 Visual limp hitch with the gape. 0 — rest-spring stick (first hinge already SitYawnTail; TreeSpring restAngle 0; no distal muscle).")]
    [Range(0f, 1.5f)]
    public float sitYawnDistalTailAmp = 1f;
    [Tooltip("Forage sit yawn: ear fold hitch with the gape. 0 — statue half-fold 0.50 (jaw/head/tongue/lids/tail already stretch).")]
    [Range(0f, 1.5f)]
    public float sitYawnEarAmp = 1f;
    [Tooltip("Forage sit yawn: whisker fan hitch with the gape. 0 — rest stamps (jaw/head/tongue/lids/tail/ears already stretch).")]
    [Range(0f, 1.5f)]
    public float sitYawnWhiskerAmp = 1f;
    [Tooltip("Forage sit yawn: brow raise hitch with the gape. 0 — rest forehead (jaw/head/tongue/lids/tail/ears/whiskers already stretch).")]
    [Range(0f, 1.5f)]
    public float sitYawnBrowAmp = 1f;
    [Tooltip("Forage sit yawn: snout lift hitch with the gape. 0 — rest muzzle (jaw/head/tongue/lids/tail/ears/whiskers/brow already stretch).")]
    [Range(0f, 1.5f)]
    public float sitYawnNoseAmp = 1f;
    [Tooltip("Forage sit yawn: lip stretch hitch with the gape. 0 — rest flews (jaw/head/tongue/lids/tail/ears/whiskers/brow/snout already stretch).")]
    [Range(0f, 1.5f)]
    public float sitYawnMouthAmp = 1f;
    [Tooltip("Forage sit yawn: pale puff hitch with the gape. 0 — silent tongue (jaw/head/tongue/lids/tail/ears/whiskers/brow/snout/lip already stretch).")]
    [Range(0f, 1.5f)]
    public float sitYawnPuffAmp = 1f;
    [Tooltip("Forage sit yawn: Chest Visual heave hitch with the gape. 0 — quiet-sit 0.0018 (jaw/head/tongue/lids/tail/ears/whiskers/brow/snout/lip/puff already stretch).")]
    [Range(0f, 1.5f)]
    public float sitYawnBreathAmp = 1f;
    [Tooltip("Forage sit yawn: Pelvis Visual heave hitch with the gape. 0 — statue rump (chest already heaves; Walk/pant already bobbed the rump).")]
    [Range(0f, 1.5f)]
    public float sitYawnRumpAmp = 1f;
    [Tooltip("Forage sit pant: first-hinge droop with the pump. 0 — 8.5 Hz sit metronome (jaw/chest/rump/neck already pant).")]
    [Range(0f, 1.5f)]
    public float sitPantTailAmp = 1f;
    [Tooltip("Forage sit pant: distal Tail2–Tail5 Visual limp hitch with the pump. 0 — rest-spring stick (first hinge already SitPantTail; TreeSpring restAngle 0; no distal muscle).")]
    [Range(0f, 1.5f)]
    public float sitPantDistalTailAmp = 1f;
    [Range(0.25f, 0.80f)]
    public float walkDuty = 0.38f;
    public float walkRearLag = 0.06f;
    // w5–w7: small proximal deltas keep the leftover sit; large ones pitch
    // the pelvis (L/R share X, so a diagonal is a pitch couple).
    public float walkHipFlex = 8f;
    public float walkShoulderFlex = 8f;
    // w10: extend 10 holds leftover sit (0.286 km/h). w11 at 14 dumped.
    public float walkHipExtend = 10f;
    public float walkShoulderExtend = 10f;
    public float walkKneeFlex = 42f;
    public float walkElbowFlex = 42f;
    public float walkStanceKnee = 24f;
    public float walkStanceElbow = 24f;
    [Range(0.15f, 0.80f)]
    public float walkSwingLiftFrac = 0.70f;
    public float walkPawPlant = 0.70f;
    [Range(0f, 1f)]
    public float walkFrontPlantScale = 0.40f;
    // After leave, swing paw is 0 until air. This is the still-down
    // swing's distal fold blend toward the latched 8 cm chord (not
    // walkKneeFlex 42 — that left the paw IsTouching). 0 = old
    // stance distal (slide). 1 = ramp to that fold. Not a paw plant.
    [Range(0f, 1f)]
    public float walkGroundedSwingPlant = 1f;
    [Range(0f, 1f)]
    public float walkComMix = 0.20f;
    // dog_ds3 (programmer). The stance limb sweeps caudally through the
    // half: that sweep IS the stride (speed ≈ cadence × reach). It was
    // 4–10° before (walkComMix nudge), measured at 2.7 cm of travel per
    // cycle, because the planted limb held the leftover-sit pose and the
    // CoM dragged the paws instead of riding them. 48° on this limb is
    // ~23 cm, so ~45° per half at the 2.8 Hz half rate is the 1.389 m/s
    // gate. Applied only to the stance-role columns, signed along facing
    // (hip plus = caudal, shoulder plus = the front mirror), and tamed by
    // walkTamePitch, so a fold cuts it. -dogWalkSweep tunes it.
    public float walkStanceSweepDeg = 45f;
    // Match chestTargetTilt / spawnChestPitch. −4 yanked the nose up
    // at the first Walk step and pitched the pelvis (third law).
    public float walkChestTilt = -8f;
    public float walkLumbarP = 0f;
    public float walkHipP = 3.5f;
    public float walkKneeP = 5f;
    public float walkActivationSpeed = 50f;
    public float walkFoldDrop = 0.50f;
    [Tooltip("CoM drop from measured spawn stance that counts as a belly, m. 0.001 disables. Fix the standComHeight reference; do not lower this to hide a geometric bar.")]
    public float sinkAbortDrop = 0.25f;
    public float walkFoldPitch = 70f;
    public float walkTamePitch = 45f;
    [Tooltip("Belly abort while the swing is still down, m. Values below 0.20 use 0.22 so the 0.12 m swing-knee chord is not an abort (that reset dual and froze phase 0). Stance plus-fold still aborts immediately.")]
    public float walkLaunchDrop = 0.22f;
    [Header("Run (bound / gallop pose size, muscles only, no AddForce)")]
    [Tooltip("Desired forward speed, m/s. Walk() reads this for pose size, cadence (stride/speed), and hip/shoulder push-off. 0 keeps the leftover sit-trot. Quiet stand is walkWanted false. Pairing is the DOG-3 diagonal (FR+RL vs FL+RR). Walk() only writes phase 0. Dual plus-paws the trail with Dual-soft hip until heel-rise, then Arms. Dual extra-extends the trail hip 4–8° (same ramp as stance) so Dual-soft can roll onto the toe; leftover-sit Dual-soft fought plus 0.18. DualReady does not Arm on a 0.12 s ceiling or a 3 cm CoM shift — those Zeroed a still-flat pad. Dual lean starts at Dual t=0. After Arm: hip HOLDS leftover sit, sit-relative +18° ON from Arm, trail paw Zero (plus 0.18 after Arm cannot pitch a loaded pad). Dual still plus-paws the take. Inverted-V 6 cm was a ~63° chair. True air holds that fold and plants cranially — not 12 cm IK. Swap is that plant, not a sit-X hop.")]
    public float runSpeed = 1.4f;
    [Tooltip("Speed the run flex / extend / lean are sized for, m/s.")]
    public float runSpeedRef = 1.4f;
    [Tooltip("Stride used to pick cadence: period = stride / runSpeed.")]
    public float runStride = 0.32f;
    public float runHipFlex = 20f;
    public float runShoulderFlex = 20f;
    public float runHipExtend = 16f;
    public float runShoulderExtend = 16f;
    public float runKneeFlex = 44f;
    public float runElbowFlex = 44f;
    [Tooltip("Extra nose-down world chest, deg, signed by facing. Stance chestTarget stays −8.")]
    public float runChestLean = 8f;
    [Range(0.15f, 0.90f)]
    public float runPawPlant = 0.78f;
    public bool walkWanted;
    // Discrete gait: −1 idle, 0 dual-support push, 1 swing lift.
    // Picture metronome is walkAnimPhase (0–1), not this. Stuck 0
    // was the leftover STEP that never left overlapping sit paws.
    public float walkPhase = -1f;
    public float walkAnimPhase;
    public int walkCycleCount;
    public int walkSwapCount;
    // Sagittal heading. +1 cranial +X (build pose), −1 look/trot toward −X.
    // Brain writes this; physics bodies are not scaled.
    public float facing = 1f;

    // Calm Forage overlays. Brain writes in Update; 0 = off.
    // Sniff/scratch steps stay on Visual so leftover sit does not trot.
    // idleSleep is night chin-down; lids are picture on Head Visual.
    // idlePant is a Forage-stop jaw pump; tongue is picture on Jaw Visual.
    [System.NonSerialized] public float idleSniff;
    [System.NonSerialized] public float idleScratch;
    [System.NonSerialized] public float idleYawn;
    [System.NonSerialized] public float idleLook;
    [System.NonSerialized] public float idleWag;
    [System.NonSerialized] public float idleSleep;
    // After a Forage trot. Jaw pump only — not a yawn (no neck lift).
    [System.NonSerialized] public float idlePant;
    // After a kill. Jaw snaps to closed; tongue stays off. Brain writes.
    [System.NonSerialized] public float idleChew;
    // Pursue Walk after the lock stare. Nose down + stiff tail raise.
    // Not idleSniff: that half-folds the ears. Walk pin stays. Brain writes.
    // DriveJaw growls a crack; fangs are picture on Jaw Visual.
    [System.NonSerialized] public float idleHunt;
    // Copied from Dog.breathPhase so SitQuietHead nods in phase with
    // BreathLift / SitQuietRump (same 2.5 clock). Not a brain float.
    [System.NonSerialized] public float sitBreathPhase;

    [Header("Leap attack (muscles only, no AddForce)")]
    public float leapCrouchSeconds = 0.35f;
    public float leapPushSeconds = 0.18f;
    public float leapAirSeconds = 0.70f;
    public float leapBiteSeconds = 0.40f;
    public float leapCrouchKnee = 55f;
    public float leapCrouchHip = -20f;
    public float leapCrouchShoulder = 20f;
    public float leapChestCoil = -14f;
    public float leapChestLaunch = 10f;
    public float leapActivationSpeed = 70f;
    public float leapBiteRange = 0.45f;
    public float leapPawPlant = 0.85f;
    public Transform leapTarget;
    public int leapCount;

    [Header("Neck / head Stable PD")]
    public float neckPGain = 1.5f;
    public float neckDGain = 0.4f;
    public float neckTargetTilt = 0f;
    public float neckErrorReferenceDegrees = 25f;
    public bool useStablePd = true;

    [Header("Shared PD refs")]
    public float errorReferenceDegrees = 10f;
    public float speedReferenceDegPerSec = 200f;
    public float muscleActivationSpeed = 20f;

    [Header("Read-only sensors")]
    public Vector2 comPosition;
    public Vector2 comVelocity;
    public float comOffsetX;
    public float supportMinX;
    public float supportMaxX;
    public float supportWidth;
    // Same sign as BodyStateEstimator.supportMargin: + inside the
    // paw AABB, 0 on the edge, − outside. Written every Sense tick
    // from live CoM, not a spawn snapshot.
    public float supportMargin;
    public bool hasCom;
    // Proof Sense ran this trial. Climbs at 200 Hz while walk
    // onset logs collapsed / posed.
    public int senseTick;
    // Dog analog of GaitStateMachine.World.collapsed / posed.
    // Recorders copy the first 3 s of walk into walkOnsetLog.
    public bool WalkCollapsed;
    public bool WalkPosed;
    public int WalkOnsetLogCount;
    public const int WalkOnsetLogCapacity = 31;
    public bool frontRightGrounded;
    public bool frontLeftGrounded;
    public bool rearRightGrounded;
    public bool rearLeftGrounded;
    public float chestPitch;
    public float pelvisTilt;
    public float lumbarAngle;
    public float chestY;
    public float pelvisY;
    public int probeCallCount;
    public int probeSaturatedCount;
    public int probeHitSum;

    public Muscle lumbarFlexor, lumbarExtensor;
    public Muscle neckFlexor, neckExtensor;
    public Muscle headFlexor, headExtensor;
    public Muscle tailFlexor, tailExtensor;
    public Muscle jawFlexor, jawExtensor;
    public Muscle frontRightShoulderFlexor, frontRightShoulderExtensor;
    public Muscle frontLeftShoulderFlexor, frontLeftShoulderExtensor;
    public Muscle frontRightElbowFlexor, frontRightElbowExtensor;
    public Muscle frontLeftElbowFlexor, frontLeftElbowExtensor;
    public Muscle rearRightHipFlexor, rearRightHipExtensor;
    public Muscle rearLeftHipFlexor, rearLeftHipExtensor;
    public Muscle rearRightKneeFlexor, rearRightKneeExtensor;
    public Muscle rearLeftKneeFlexor, rearLeftKneeExtensor;
    public Muscle frontRightPawFlexor, frontRightPawExtensor;
    public Muscle frontLeftPawFlexor, frontLeftPawExtensor;
    public Muscle rearRightPawFlexor, rearRightPawExtensor;
    public Muscle rearLeftPawFlexor, rearLeftPawExtensor;

    float lumbarEffectiveInertia;

    public HingeJoint2D lumbarJoint;
    public HingeJoint2D neckJoint;
    public HingeJoint2D headJoint;
    public HingeJoint2D tailJoint;
    public HingeJoint2D jawJoint;
    public HingeJoint2D frontRightShoulderJoint, frontLeftShoulderJoint;
    public HingeJoint2D frontRightElbowJoint, frontLeftElbowJoint;
    public HingeJoint2D rearRightHipJoint, rearLeftHipJoint;
    public HingeJoint2D rearRightKneeJoint, rearLeftKneeJoint;
    public HingeJoint2D frontRightPawJoint, frontLeftPawJoint;
    public HingeJoint2D rearRightPawJoint, rearLeftPawJoint;

    private VestibularSystem vestibular;
    private Transform chestTf;
    private Transform pelvisTf;
    private Transform headTf;
    private Transform jawTf;
    private Rigidbody2D[] bodies = System.Array.Empty<Rigidbody2D>();
    private Collider2D frontRightPawCol, frontLeftPawCol, rearRightPawCol, rearLeftPawCol;
    private readonly ContactPoint2D[] groundContacts = new ContactPoint2D[8];
    private ContactFilter2D groundContactFilter;
    private bool groundContactFilterReady;
    private Vector2 previousCom;
    private bool hasPreviousCom;
    private bool hasLastSupport;
    private float lastMinX;
    private float lastMaxX;
    private float cachedComFixedTime = -1f;
    private Vector2 cachedCom;
    private float neckEffectiveInertia;
    private float headEffectiveInertia;
    private float frontRightElbowInertia, frontLeftElbowInertia;
    private float rearRightKneeInertia, rearLeftKneeInertia;
    private float rearRightHipInertia, rearLeftHipInertia;
    private float frontRightShoulderInertia, frontLeftShoulderInertia;
    private float tailInertia;
    private float jawInertia;
    private float lumbarInertia;
    private float prevPelvisY;
    private bool hasPrevPelvisY;
    private float spawnedFixedTime = -1f;
    private float frontRightGroundUntil;
    private float frontLeftGroundUntil;
    private float rearRightGroundUntil;
    private float rearLeftGroundUntil;
    private OrganismVoice voice;
    private float barkUntil = -1f;
    private float biteOpenUntil = -1f;
    private float biteCloseUntil = -1f;
    private float walkClockStart = -1f;
    // Diagonal trot. walkHalf 0: FR+RL plant, FL+RR peel.
    // Walk() only writes phase 0. Dual plus-paws the trail with
    // Dual-soft hip until heel-rise, then Arms the TRAIL. Dual
    // extra-extends that trail hip 4–8° (same ramp as stance) so
    // Dual-soft rolls onto the toe — leftover-sit Dual-soft fought
    // plus 0.18 (dog_w68). Stance-mid PawPd is P-error 0 on leftover
    // sit. After Arm: hip HOLDS leftover sit, sit-relative +18° ON
    // from Arm. Dual plus-paws 0.18. After Arm the trail paw Zeroes
    // (plus 0.18 on a full-P hip cannot pitch the sole — human
    // ankle). Arm at minDual Zeroed a still-flat pad (dog_w66
    // leftover sit-trot). The 0.12 s DualReady ceiling and a 3 cm
    // CoM shift did the same (dog_w67). Dual lean ramps from Dual
    // t=0. Caudal 10° after Arm skated the planted pad (dog_w64).
    // Arm-tick U=0 left leftover sit while stance opened 6°.
    // Inverted-V 6 cm was ~63° (dog_w58); waiting for 3 mm
    // welded the pad (dog_w63). Extra plus-knee on a held hip
    // shortened the column (dog_w61). Dual-soft hip+knee dumped
    // the pelvis (dog_w60).
    // Front swing stays planted until that hind is air. Empty
    // GetContacts inside 2×contactOffset is not air. True air
    // holds that fold; 12 cm IK sat leftover sit.
    // Swing paw Zero once the sole is air (plus 0.55 kept the
    // toe in dirt).
    // 0.30 s DualSupportMax ate the 0.229 s half at 5 km/h.
    // A CoP that walked from t=0 while four paws were down
    // drifted without swaps. walkPhase stays 0 until the trail
    // paw is up. First-peel abort stays armed unless the trunk
    // is a belly. Air proximal blends to cranial flex on
    // walkAirBorn, not swingU. Stance stays on the Walk() chord.
    // Spawn-target HeightLift opened stance to 0° and unloaded
    // onto the swing; walk lift is from walkPelvisHoldY so a
    // leftover sit does not stand the plate up. DualTake plus-fold
    // during dual dropped ~0.12 m. Peel-U freeze at 2.8 cm left
    // distal ~24° of the 80° chord (paw down). Sag opens the
    // stance V; peel keeps ramping. Squat abort is stance
    // plus-fold past the Walk() sit, not the swing chord.
    // runStride/runSpeed is 0.229 s/half at 5 km/h. Rear-stance
    // lean is ~0 so CoM can move onto the hinds. Stance distal ≤15°.
    // 0.06 dual + 0.08 peel + 0.08 air ≈ 0.22 s on the 0.229 s half.
    private bool walkCaptureArmed;
    private bool walkSwungAir;
    private bool walkNearSwungAir;
    private float walkAirBorn = -1f;
    private int walkAirStreak;
    private int walkNearAirStreak;
    // Rocker clock. Trail is ON from Arm (sit-relative +18°,
    // U=1). Waiting for 3 mm welded the pad. Arm-tick U=0
    // left leftover sit while stance opened 6° (dog_w64).
    // Caudal hip 10° skated that pad. Front still waits for
    // hind air. Extra plus-knee sat leftover sit (dog_w61).
    private float walkTrailRockerBorn = -1f;
    private float walkNearRockerBorn = -1f;
    private bool frontRightHeel, frontRightToe;
    private bool frontLeftHeel, frontLeftToe;
    private bool rearRightHeel, rearRightToe;
    private bool rearLeftHeel, rearLeftToe;
    private float frontRightHeelY, frontRightToeY;
    private float frontLeftHeelY, frontLeftToeY;
    private float rearRightHeelY, rearRightToeY;
    private float rearLeftHeelY, rearLeftToeY;
    private int walkHalf;
    private float walkHalfBorn = -1f;
    private float walkSwingCaudalX;
    private float walkPelvisHoldY;
    private bool hasWalkPelvisHold;
    private float walkEntryHip;
    private float walkEntryKnee;
    private float walkEntryShoulder;
    private float walkEntryElbow;
    private bool hasWalkEntry;
    private float walkLatchHip;
    private float walkLatchKnee;
    private float walkLatchShoulder;
    private float walkLatchElbow;
    private bool hasWalkLatch;
    // Dirt rocker (~6 cm) and free-foot fold, latched at Arm.
    // Live 12 cm IK only after the sole leaves. Chasing a live
    // strut went to 120 on the human.
    private float walkFoldKnee;
    private float walkFoldElbow;
    // Dirt rocker (~6 cm), not the 12 cm free-foot fold.
    private float walkRockerKnee;
    private float walkRockerElbow;
    private bool hasWalkFold;
    private float walkFoldHipTrail;
    private float walkFoldHipNear;
    private float walkFoldLiftTrailX = float.NaN;
    // World floor (paw0 + 0.12 m). Live IK Y is hip + relY so a
    // stance HeightLift does not lengthen the chord into dirt.
    private float walkFoldLiftTrailY = float.NaN;
    private float walkFoldLiftTrailRelY = float.NaN;
    private float walkFoldLiftNearX = float.NaN;
    private float walkFoldLiftNearY = float.NaN;
    private float walkFoldLiftNearRelY = float.NaN;
    private bool hasWalkFoldLift;
    private float walkOnsetBorn = -1f;
    private readonly float[] walkOnsetT = new float[WalkOnsetLogCapacity];
    private readonly float[] walkOnsetComX = new float[WalkOnsetLogCapacity];
    private readonly float[] walkOnsetMargin = new float[WalkOnsetLogCapacity];
    private readonly float[] walkOnsetHipDrop = new float[WalkOnsetLogCapacity];
    private readonly float[] walkOnsetComDrop = new float[WalkOnsetLogCapacity];
    private readonly float[] walkOnsetColumn = new float[WalkOnsetLogCapacity];
    private readonly byte[] walkOnsetFlags = new byte[WalkOnsetLogCapacity];
    private readonly byte[] walkOnsetReason = new byte[WalkOnsetLogCapacity];
    private readonly bool[] walkOnsetWritten = new bool[WalkOnsetLogCapacity];
    private float getUpBorn = -1f;
    private float getUpPlantBorn = -1f;
    private float getUpPikeBorn = -1f;
    // Tuck ⇄ push cycle. getUpPhaseBorn = the frame the current half started,
    // getUpHeapAge = seconds the trunk has been a heap in this get-up (the
    // settle the tuck waits for when no paw is on the floor yet).
    private float getUpPhaseBorn = -1f;
    private float getUpHeapAge;
    private bool getUpPushPhase;
    private float getUpRiseBlend;
    private float getUpContactAge;
    // Cached at Bind. GETUP peel must not Find JointFriction at 200 Hz.
    private JointFriction lumbarFrictionComp;
    private JointFriction rearLeftHipFriction, rearRightHipFriction;
    private JointFriction rearLeftKneeFriction, rearRightKneeFriction;
    private JointFriction frontLeftShoulderFriction, frontRightShoulderFriction;
    private JointFriction frontLeftElbowFriction, frontRightElbowFriction;
    private JointFriction rearLeftPawFriction, rearRightPawFriction;
    private JointFriction frontLeftPawFriction, frontRightPawFriction;
    private float lumbarFrictionBase;
    private float rearLeftHipFrictionBase, rearRightHipFrictionBase;
    private float rearLeftKneeFrictionBase, rearRightKneeFrictionBase;
    private float frontLeftShoulderFrictionBase, frontRightShoulderFrictionBase;
    private float frontLeftElbowFrictionBase, frontRightElbowFrictionBase;
    private float rearLeftPawFrictionBase, rearRightPawFrictionBase;
    private float frontLeftPawFrictionBase, frontRightPawFrictionBase;
    private bool getUpFrictionPeeled;
    private const float GetUpFrictionTau = 0.10f;
    // Human CaptureLeanPhase kills lean at |tilt|≥20°. dog_w71 tame
    // at 0.6·45° still leaned into −98°.
    private const float WalkLeanKillAbsPitch = 20f;
    private float rearThighLen = 0.17f;
    private float rearShinLen = 0.17f;
    private float frontUpperLen = 0.17f;
    private float frontLowerLen = 0.17f;
    private float walkPawH = 0.05f;
    // Spawn slab is y = −2.0. Cached at Bind — not Find at 200 Hz.
    private float walkGroundTopY = -2f;
    // 0.08 sat inside IsTouching after a 2 cm closed-chain dip
    // (same hole as human groundProbeThickness 0.06). 0.12 is 2×
    // on a FREE foot. Dirt uses the rocker (~paw height).
    private const float WalkSwingClearanceM = 0.12f;
    // Kept for the unused inverted-V dirt chord. Paw box is 0.05 m
    // tall. Sit-relative extra is WalkSwingRockerExtraDeg (dog_w64).
    private const float WalkSwingRockerClearanceM = 0.06f;
    // Dual take: plus plant while Dual-soft hip extra-extends
    // 4–8° caudal (same ramp as stance), until the trail heel
    // rises. Leftover-sit Dual-soft fought plus 0.18 (dog_w68).
    // After Arm the trail Zeroes — plus 0.18 on a full-P hip
    // cannot pitch the sole (human ankle: torque turns the
    // trunk). Arm at minDual Zeroed a still-flat pad (dog_w66).
    // 0.55 speared the twin (L/R share X). Not a pose PD.
    private const float WalkSwingRockerPlant = 0.18f;
    // Min hip–paw Y on free-foot IK. 0.02 m was a chair on
    // leftover sit (hip–paw ~14 cm). dog_w56 does not run that
    // IK; the 6 cm rocker is the leave. Kept for the unused solver.
    private const float WalkSwingIkHipGapM = 0.08f;
    // Sole is down while a heel/toe corner is this close to the
    // slab. Empty GetContacts inside 2×contactOffset is not air
    // (dog_w55 firstAir-in-band). ~0.02 m is two default offsets.
    private const float WalkSoleBandM = 0.02f;
    // Plus 0.18 on a 5 cm heel is a few millimetres before the
    // 2 cm band clears (~37°). dog_w57 started the rocker on
    // this rise; full-P hip after Arm never produced it.
    // dog_w58 latched 6 cm at Arm into that pin. dog_w59
    // keeps Dual-soft hip until this rise, then the rocker.
    private const float WalkHeelRiseM = 0.003f;
    // Armed peel: open stance toward 0 so the girdle holds
    // while the trail shortens 6 cm. HeightLift waited for a
    // 5 mm drop and sat the pelvis into the chord.
    // dog_w73: 6° left rump sag on the slow half; 9° holds.
    // dog_w74: 11° on the 0.18 s half — girdle was still sinking.
    private const float WalkPeelStanceLiftDeg = 11f;
    private const float WalkSwingFoldMax = 80f;
    // In-place lift IK may need more than the 80° knee-only cap once
    // the hip is allowed to rotate to the lift pose. 120° is a chair.
    private const float WalkSwingIkKneeMax = 100f;
    // Hip may rotate only this far from the Arm hold toward the IK
    // thigh. Wider is scissor (−flexHip / a forward reach).
    private const float WalkSwingIkHipCap = 25f;
    // Air reach. 38° on a 0.27 m folded chord is ~18 cm — under
    // runStride 0.32. 48° is ~23 cm, the 0.70·stride complete gate.
    private const float WalkAirHipFlexMin = 48f;
    private const float WalkAirShoulderFlexMin = 42f;
    // Air proximal blend. 0.06 s froze the leave before the
    // 32° reach, then the 34 ms clock planted in place. 0.10 s
    // plus 0.10 peel plus 0.06 dual overran the 0.229 s half.
    // dog_w73: 0.07 fits the 0.20 s half (0.06 dual + peel + air).
    // dog_w74: 0.06 fits the 0.18 s half (0.06 dual + peel + air).
    private const float WalkAirReachSeconds = 0.06f;
    // Open distal after this fraction of airU (hip already cranial).
    // Opening on gone waited for a stride the folded chord cannot
    // make; opening at airU 0 planted at sit X (dog_w10 hop).
    private const float WalkAirOpenStart = 0.55f;
    // dog_ds2 (programmer). The air pose held the 48° cranial reach for the
    // whole run: the swing paw hung 0.20–0.26 m over the slab, the trail
    // never replanted, walkCycleCount stayed 0 for 24 s (dog_ds1). After the
    // reach the hip must come back toward the plant angle — that descent is
    // what puts the pad on the dirt. The angle sits this many degrees
    // cranial of the stance pose so the pad lands on new dirt, not the
    // take-off print.
    private const float WalkAirPlantLeadDeg = 6f;
    // dog_ds2 (programmer). The walk stance paw CoM grip is off — see
    // WalkStancePlantCommand: every non-zero level measured (1.0, 0.35,
    // −1.0) ended in the same 41° rear sit, only the removed seek walks
    // forward (+1.08 m, 46 cycles).
    // Cranial heel travel that counts as a stride, not a hop.
    // 2 cm is leftover sit wiggle; 0.15·half (~34 ms) at gone≈0
    // was the 0.286 km/h sit-trot.
    private const float WalkPlantAheadMinM = 0.08f;
    // Air hip 48° is the cranial lead. Dirt X-lead 4 cm was a
    // planted skate (heel OR toe, firstAir never).
    private const float WalkSwingReachLeadM = 0f;
    // Dual take: touching trail is plus 0.18 until heel-rise.
    // After Arm the trail paw Zeroes — plus 0.18 cannot pitch
    // a loaded pad (human ankle). Arm at minDual Zeroed a
    // still-flat pad (dog_w66). 0.55 speared the twin. Free
    // foot Zeroes. Not a pose PD.
    // Swing-knee peel to the 0.12 m chord after Arm, s.
    // 0.10 s plus 0.10 air plus 0.06 dual overran the 0.229 s half.
    // dog_w73: 0.07 with air 0.07 fits the faster 0.20 s half.
    // dog_w74: 0.06 with air 0.06 fits the 0.18 s half.
    private const float WalkSwingPeelSeconds = 0.06f;
    // Dual: trail hip is a soft yield so CoM can leave that hind.
    // Front swing shoulder stays full P (third plant). Zero hip
    // dropped the rump (dog_w40). Dual-soft hip+knee after Arm
    // was the limp squat (dog_w60).
    private const float WalkDualSwingHipPScale = 0.35f;
    // After Arm, trail hip HOLDS leftover sit. Dual extra-extend
    // on that hip is Dual-only (dog_w69). Caudal extra-extend 10°
    // on a still-down pad AFTER Arm skated the sole caudal
    // (foot slide, leftover sit-trot). LeapPush plus is four-
    // paw explode, not a peel. dog_w62 more-minus was swing-
    // flex on a pin. Not extra plus-knee (dog_w61) and not
    // Dual-soft hip+knee (dog_w60). Air still minus.
    private const float WalkHeelPinHipCaudalDeg = 0f;
    // Dirt rocker extra from leftover-sit distal, deg. Stance
    // opens ~24° → 9° after Arm (~3 cm of column). Matching
    // trail +18° (~3 cm shorten) unloads the pad instead of
    // jacking it. ON from Arm (U=1): the 0.08 s lerp left
    // leftover sit on the Arm tick. Inverted-V 6 cm was ~63°
    // (dog_w58). hold+12 never ran: hasWalkFold always latched
    // the chair chord.
    private const float WalkSwingRockerExtraDeg = 18f;
    // After Arm, Zero the swing hip inside this band. Deeper sit
    // (more minus) or more than 20° caudal gets a check-rein.
    private const float WalkSwingHipSagCap = 20f;
    // Dual CoP / firstCom travel. Quiet leftover sit already sits
    // on the cranial plate (~0.07 m), so stance-mid + this lead is
    // P-error 0. Seek launch CoM + this along facing — both halves.
    // Bound leftover flipped half 1 caudal and cancelled the stride
    // (dog_w10 0.286 km/h). Then Arm.
    private const float WalkLaunchLeadM = 0.03f;
    // Dual chest-lean ramp from Dual t=0. Waiting until minDual
    // left only 0.06 s of lean before the old 0.12 s DualReady
    // ceiling Armed a still-flat pad (dog_w67 leftover sit-trot).
    private const float WalkOnsetLeanSeconds = 0.12f;
    // History: 0.30 s DualReady ceiling ate the 0.229 s half;
    // 0.12 s Zeroed a still-flat pad (dog_w67). DualReady no
    // longer Arms on this clock — heel-rise or trail air only.
    private const float WalkDualSupportMax = 0.12f;
    // Stance distal stays under this. 24° leftover sit plus-fold
    // is the DualSupport squat (human 70° then 120°).
    private const float WalkStanceDistalMax = 15f;
    // Sag past the Walk() sit that opens stance toward 0°.
    // Acceptance is < 3 cm. Peel-U no longer freezes here.
    // dog_w73: full open by 2 cm so the girdle fights earlier.
    // dog_w74: 1.5 cm — rump still sagged past 2 cm on the slow half.
    private const float WalkHoldDropMax = 0.015f;
    // DOGDIAG (programmer slot, dog_ds1 blocker). One line every 0.25 s
    // while DriveWalk runs, so a frozen swing shows which gate is false.
    // Read only: no walk field is written here, the gait is unchanged.
    private float walkDiagNext;
    private int walkDiagWrites;
    private float walkComHoldX;
    private bool hasWalkComHold;
    private float sitTurnBorn;
    private float sitTurnUntil;
    private float walkTurnBorn;
    private float walkTurnUntil;
    private float leapPhaseStart = -1f;
    private bool leapBiteStarted;
    private float activationSpeedThisStep;
    private Vector2 leapAimPoint;
    private Dog body;

    public void Bind(Dog dog)
    {
        body = dog;
        vestibular = dog != null ? dog.vestibularSystem : GetComponent<VestibularSystem>();
        chestTf = dog != null && dog.chestSegment != null ? dog.chestSegment.transform : transform.Find("Chest");
        pelvisTf = dog != null && dog.pelvisSegment != null ? dog.pelvisSegment.transform : transform.Find("Pelvis");
        headTf = dog != null && dog.headSegment != null ? dog.headSegment.transform : transform.Find("Head");
        jawTf = dog != null && dog.jawSegment != null ? dog.jawSegment.transform : transform.Find("Jaw");
        CacheBodies();
        BindJointsAndMuscles();
        CacheWalkBones();
        CacheWalkGround();
        voice = GetComponent<OrganismVoice>();
        spawnedFixedTime = Time.fixedTime;
    }

    // Once at Bind. DriveWalk must not GetComponent sizes at 200 Hz.
    private void CacheWalkBones()
    {
        if (body == null)
            return;
        if (body.rearThighSize.y > 0.05f)
            rearThighLen = body.rearThighSize.y;
        if (body.rearShinSize.y > 0.05f)
            rearShinLen = body.rearShinSize.y;
        if (body.frontUpperSize.y > 0.05f)
            frontUpperLen = body.frontUpperSize.y;
        if (body.frontLowerSize.y > 0.05f)
            frontLowerLen = body.frontLowerSize.y;
        if (body.pawSize.y > 0.01f)
            walkPawH = body.pawSize.y;
    }

    // Once at Bind. Slab is y = −2.0 unless Ground exists.
    // ProbePaw must not Find at 200 Hz.
    private void CacheWalkGround()
    {
        walkGroundTopY = -2f;
        GameObject go = GameObject.Find("Ground");
        if (go == null)
            return;
        Collider2D col = go.GetComponent<Collider2D>();
        if (col != null)
            walkGroundTopY = col.bounds.max.y;
    }

    private void CacheBodies()
    {
        bodies = GetComponentsInChildren<Rigidbody2D>();
        frontRightPawCol = Col("FrontRightPaw");
        frontLeftPawCol = Col("FrontLeftPaw");
        rearRightPawCol = Col("RearRightPaw");
        rearLeftPawCol = Col("RearLeftPaw");
        SeedSupportFromPawBounds();
    }

    // First frames are airborne: one paw then makes a 10 cm island and
    // |CoM offset| jumps past 0.30 m. Seed the stance polygon so
    // "last support" exists before contact (same rule as human CoM).
    private void SeedSupportFromPawBounds()
    {
        bool found = false;
        float minX = 0f;
        float maxX = 0f;
        ExpandBounds(frontRightPawCol, ref found, ref minX, ref maxX);
        ExpandBounds(frontLeftPawCol, ref found, ref minX, ref maxX);
        ExpandBounds(rearRightPawCol, ref found, ref minX, ref maxX);
        ExpandBounds(rearLeftPawCol, ref found, ref minX, ref maxX);
        if (!found) return;
        lastMinX = minX;
        lastMaxX = maxX;
        hasLastSupport = true;
    }

    private static void ExpandBounds(Collider2D col, ref bool found, ref float minX, ref float maxX)
    {
        if (col == null) return;
        Bounds b = col.bounds;
        minX = found ? Mathf.Min(minX, b.min.x) : b.min.x;
        maxX = found ? Mathf.Max(maxX, b.max.x) : b.max.x;
        found = true;
    }

    private Collider2D Col(string childName)
    {
        Transform t = transform.Find(childName);
        return t != null ? t.GetComponent<Collider2D>() : null;
    }

    private void BindJointsAndMuscles()
    {
        BindPair("Chest", out lumbarJoint, out lumbarFlexor, out lumbarExtensor);
        BindPair("Neck", out neckJoint, out neckFlexor, out neckExtensor);
        BindPair("Head", out headJoint, out headFlexor, out headExtensor);
        BindPair("Tail", out tailJoint, out tailFlexor, out tailExtensor);
        BindPair("Jaw", out jawJoint, out jawFlexor, out jawExtensor);
        BindPair("FrontRightUpper", out frontRightShoulderJoint, out frontRightShoulderFlexor, out frontRightShoulderExtensor);
        BindPair("FrontLeftUpper", out frontLeftShoulderJoint, out frontLeftShoulderFlexor, out frontLeftShoulderExtensor);
        BindPair("FrontRightLower", out frontRightElbowJoint, out frontRightElbowFlexor, out frontRightElbowExtensor);
        BindPair("FrontLeftLower", out frontLeftElbowJoint, out frontLeftElbowFlexor, out frontLeftElbowExtensor);
        BindPair("RearRightThigh", out rearRightHipJoint, out rearRightHipFlexor, out rearRightHipExtensor);
        BindPair("RearLeftThigh", out rearLeftHipJoint, out rearLeftHipFlexor, out rearLeftHipExtensor);
        BindPair("RearRightShin", out rearRightKneeJoint, out rearRightKneeFlexor, out rearRightKneeExtensor);
        BindPair("RearLeftShin", out rearLeftKneeJoint, out rearLeftKneeFlexor, out rearLeftKneeExtensor);
        BindPair("FrontRightPaw", out frontRightPawJoint, out frontRightPawFlexor, out frontRightPawExtensor);
        BindPair("FrontLeftPaw", out frontLeftPawJoint, out frontLeftPawFlexor, out frontLeftPawExtensor);
        BindPair("RearRightPaw", out rearRightPawJoint, out rearRightPawFlexor, out rearRightPawExtensor);
        BindPair("RearLeftPaw", out rearLeftPawJoint, out rearLeftPawFlexor, out rearLeftPawExtensor);
        BindFrictions();
    }

    private void BindPair(string childName, out HingeJoint2D joint, out Muscle flexor, out Muscle extensor)
    {
        joint = null;
        flexor = null;
        extensor = null;
        Transform t = transform.Find(childName);
        if (t == null) return;
        joint = t.GetComponent<HingeJoint2D>();
        Muscle[] muscles = t.GetComponents<Muscle>();
        flexor = System.Array.Find(muscles, m => m != null && m.direction == 1f);
        extensor = System.Array.Find(muscles, m => m != null && m.direction == -1f);
    }

    // Once at Bind. DriveGetUp / DriveWalk must not GetComponent friction.
    private void BindFrictions()
    {
        BindFriction(lumbarJoint, out lumbarFrictionComp, out lumbarFrictionBase);
        BindFriction(rearLeftHipJoint, out rearLeftHipFriction, out rearLeftHipFrictionBase);
        BindFriction(rearRightHipJoint, out rearRightHipFriction, out rearRightHipFrictionBase);
        BindFriction(rearLeftKneeJoint, out rearLeftKneeFriction, out rearLeftKneeFrictionBase);
        BindFriction(rearRightKneeJoint, out rearRightKneeFriction, out rearRightKneeFrictionBase);
        BindFriction(frontLeftShoulderJoint, out frontLeftShoulderFriction, out frontLeftShoulderFrictionBase);
        BindFriction(frontRightShoulderJoint, out frontRightShoulderFriction, out frontRightShoulderFrictionBase);
        BindFriction(frontLeftElbowJoint, out frontLeftElbowFriction, out frontLeftElbowFrictionBase);
        BindFriction(frontRightElbowJoint, out frontRightElbowFriction, out frontRightElbowFrictionBase);
        BindFriction(rearLeftPawJoint, out rearLeftPawFriction, out rearLeftPawFrictionBase);
        BindFriction(rearRightPawJoint, out rearRightPawFriction, out rearRightPawFrictionBase);
        BindFriction(frontLeftPawJoint, out frontLeftPawFriction, out frontLeftPawFrictionBase);
        BindFriction(frontRightPawJoint, out frontRightPawFriction, out frontRightPawFrictionBase);
    }

    private static void BindFriction(HingeJoint2D joint, out JointFriction friction, out float baseK)
    {
        friction = null;
        baseK = 0f;
        if (joint == null)
            return;
        friction = joint.GetComponent<JointFriction>();
        if (friction != null)
            baseK = friction.damping;
    }

    // GETUP-1 must move the heap. Quiet SPD on welded K saturates on
    // a −98° plank (attempts=1). Same human peel: K = min(built, I/τ)
    // with snap so stopTorque does not hide the fade. Restore on
    // BeginWalk / fall. Cached refs only — no Find, no alloc.
    private void ApplyGetUpFrictionPeel(bool peel)
    {
        float fade = peel ? 1f : 0f;
        float snap = peel ? 1f : 0f;
        float tau = GetUpFrictionTau;
        FadeJointFriction(lumbarFrictionComp, lumbarFrictionBase, lumbarJoint, tau, fade, snap);
        FadeJointFriction(rearLeftHipFriction, rearLeftHipFrictionBase, rearLeftHipJoint, tau, fade, snap);
        FadeJointFriction(rearRightHipFriction, rearRightHipFrictionBase, rearRightHipJoint, tau, fade, snap);
        FadeJointFriction(rearLeftKneeFriction, rearLeftKneeFrictionBase, rearLeftKneeJoint, tau, fade, snap);
        FadeJointFriction(rearRightKneeFriction, rearRightKneeFrictionBase, rearRightKneeJoint, tau, fade, snap);
        FadeJointFriction(frontLeftShoulderFriction, frontLeftShoulderFrictionBase, frontLeftShoulderJoint, tau, fade, snap);
        FadeJointFriction(frontRightShoulderFriction, frontRightShoulderFrictionBase, frontRightShoulderJoint, tau, fade, snap);
        FadeJointFriction(frontLeftElbowFriction, frontLeftElbowFrictionBase, frontLeftElbowJoint, tau, fade, snap);
        FadeJointFriction(frontRightElbowFriction, frontRightElbowFrictionBase, frontRightElbowJoint, tau, fade, snap);
        FadeJointFriction(rearLeftPawFriction, rearLeftPawFrictionBase, rearLeftPawJoint, tau, fade, snap);
        FadeJointFriction(rearRightPawFriction, rearRightPawFrictionBase, rearRightPawJoint, tau, fade, snap);
        FadeJointFriction(frontLeftPawFriction, frontLeftPawFrictionBase, frontLeftPawJoint, tau, fade, snap);
        FadeJointFriction(frontRightPawFriction, frontRightPawFrictionBase, frontRightPawJoint, tau, fade, snap);
        getUpFrictionPeeled = peel;
    }

    private static void FadeJointFriction(
        JointFriction friction, float builtK, HingeJoint2D joint,
        float tau, float fade, float snap)
    {
        if (friction == null)
            return;
        float k = builtK;
        if ((fade > 0.0001f || snap > 0.5f) && tau > 0.0001f)
        {
            float paired = PairedInertia(joint);
            if (paired > 0f)
            {
                float targetK = Mathf.Min(builtK, paired / tau);
                k = snap > 0.5f ? targetK : Mathf.Lerp(builtK, targetK, fade);
            }
        }
        friction.damping = k;
    }

    private static float PairedInertia(HingeJoint2D joint)
    {
        if (joint == null || joint.connectedBody == null)
            return 0f;
        Rigidbody2D self = joint.attachedRigidbody;
        float inv = 0f;
        if (self != null && self.inertia > 0f)
            inv += 1f / self.inertia;
        if (joint.connectedBody.inertia > 0f)
            inv += 1f / joint.connectedBody.inertia;
        return inv > 0f ? 1f / inv : 0f;
    }

    void FixedUpdate()
    {
        if (energyRest) return;
        Sense();
        Drive();
    }

    private void Sense()
    {
        senseTick++;
        // Live rigidbody sum every physics step — not a spawn snapshot.
        // Quiet paw hold can keep x near the sit park under a chest
        // lean; that is the loop, not a frozen sensor (senseTick climbs).
        comPosition = ComputeCoM();
        hasCom = true;
        if (hasPreviousCom)
            comVelocity = (comPosition - previousCom) / Time.fixedDeltaTime;
        else
            comVelocity = Vector2.zero;
        previousCom = comPosition;
        hasPreviousCom = true;

        frontRightGrounded = LatchPawGrounded(frontRightPawCol, ref frontRightGroundUntil,
            walkCaptureArmed && !WalkLimbIsStance(true, true),
            out frontRightHeel, out frontRightToe, out frontRightHeelY, out frontRightToeY);
        frontLeftGrounded = LatchPawGrounded(frontLeftPawCol, ref frontLeftGroundUntil,
            walkCaptureArmed && !WalkLimbIsStance(true, false),
            out frontLeftHeel, out frontLeftToe, out frontLeftHeelY, out frontLeftToeY);
        rearRightGrounded = LatchPawGrounded(rearRightPawCol, ref rearRightGroundUntil,
            walkCaptureArmed && !WalkLimbIsStance(false, true),
            out rearRightHeel, out rearRightToe, out rearRightHeelY, out rearRightToeY);
        rearLeftGrounded = LatchPawGrounded(rearLeftPawCol, ref rearLeftGroundUntil,
            walkCaptureArmed && !WalkLimbIsStance(false, false),
            out rearLeftHeel, out rearLeftToe, out rearLeftHeelY, out rearLeftToeY);

        if (TryGetSupportBounds(out float minX, out float maxX))
        {
            supportMinX = minX;
            supportMaxX = maxX;
            supportWidth = maxX - minX;
            comOffsetX = comPosition.x - (minX + maxX) * 0.5f;
            if (comPosition.x >= minX && comPosition.x <= maxX)
                supportMargin = Mathf.Min(comPosition.x - minX, maxX - comPosition.x);
            else
            {
                float distLeft = Mathf.Abs(comPosition.x - minX);
                float distRight = Mathf.Abs(comPosition.x - maxX);
                supportMargin = -Mathf.Min(distLeft, distRight);
            }
        }
        else
        {
            supportWidth = 0f;
            comOffsetX = 0f;
            supportMargin = 0f;
        }

        if (vestibular != null)
        {
            chestPitch = vestibular.GetBodyTilt();
            pelvisTilt = vestibular.GetPelvisTilt();
        }
        lumbarAngle = lumbarJoint != null ? lumbarJoint.jointAngle : 0f;
        jawAngle = jawJoint != null ? jawJoint.jointAngle : 0f;

        chestY = chestTf != null ? chestTf.position.y : transform.position.y;
        pelvisY = pelvisTf != null ? pelvisTf.position.y : transform.position.y;
        if (hasPrevPelvisY)
            pelvisVelY = (pelvisY - prevPelvisY) / Time.fixedDeltaTime;
        else
            pelvisVelY = 0f;
        prevPelvisY = pelvisY;
        hasPrevPelvisY = true;
        TryCaptureGetUpStandTrunk();
        if (!hasHeightTarget)
        {
            targetPelvisY = pelvisY;
            hasHeightTarget = true;
            // Inverted-V column at first Sense with a pelvis sample.
            // Settle stand is captured later; onset columnStanding uses this.
            if (!spawnColumnValid && hasCom)
            {
                spawnColumnPelvisY = pelvisY;
                spawnColumnComY = comPosition.y;
                spawnColumnValid = true;
            }
        }
        TryCaptureSpawnStandHeight();
        MaybeRetargetStandFromSettledSit();

        if (IsLeap())
            AdvanceLeap();
        else if (currentState == StanceState.GettingUp)
            TickGetUpSense();
        else if (currentState == StanceState.Fallen
                 || currentState == StanceState.Falling
                 || IsFallenHeap())
            TickFallenSense();
        else if (walkWanted)
        {
            // Spawn must plant before the gait clock starts. Once Walk
            // is live, a swing paw in the air is a step, not a cancel.
            if (!IsWalk() && !AllPawsGrounded())
            {
                float absOff = Mathf.Abs(comOffsetX);
                if (absOff > fallCoMOffset)
                    EnterFallingOrFallen(true);
                else if (absOff > recoveryCoMOffset)
                    currentState = StanceState.Recovery;
                else
                    currentState = StanceState.Balancing;
            }
            else
            {
                // CoM leaving the stance plate is a step, not a fall. Fold
                // only on a real belly from the measured settle CoM — the
                // spawn column is the inverted-V, ~0.19 m above leftover
                // sit, so that bar reads every quiet plant as collapse.
                float drop = WalkStandDrop();
                float absPitch = Mathf.Abs(chestPitch);
                if (drop > walkFoldDrop || (drop > 0.32f && absPitch > 50f) || absPitch > walkFoldPitch)
                {
                    // Keep walkWanted: CLI -dogWalk calls Walk() once, and
                    // Falling→RelaxAll with a cleared wish froze 32 s.
                    // Capture clock clears; get-up owns the body until up.
                    ResetWalkClock();
                    EnterFallingOrFallen(true);
                }
                else if (currentState == StanceState.GettingUp
                         || currentState == StanceState.Fallen
                         || currentState == StanceState.Falling)
                {
                    // Walk() wish held through get-up; do not Arm capture.
                }
                else
                {
                    currentState = StanceState.Walk;
                    LatchWalkPelvisHold();
                    SnapshotWalkEntry();
                    AdvanceWalkClock();
                }
            }
        }
        else
        {
            float absOff = Mathf.Abs(comOffsetX);
            if (absOff > fallCoMOffset)
                EnterFallingOrFallen(false);
            else if (absOff > recoveryCoMOffset)
                currentState = StanceState.Recovery;
            else if (currentState != StanceState.Fallen
                     && currentState != StanceState.GettingUp)
                currentState = StanceState.Balancing;
        }

        UpdateWalkWorldFlags();
        StepWalkOnsetLog();
    }

    // Heap on the dirt: big trunk drop or prone pitch. Not CoM offset
    // alone — leftover sit already parks CoM near the mid-paws. State
    // Falling/Fallen is handled by the Sense branch, not this test.
    private bool IsFallenHeap()
    {
        if (IsLeap())
            return false;
        float drop = WalkStandDrop();
        if (!standComHeightValid && hasHeightTarget)
            drop = Mathf.Max(drop, Mathf.Max(0f, targetPelvisY - pelvisY));
        float absPitch = Mathf.Abs(chestPitch);
        if (absPitch >= getUpFallenAbsPitch)
            return true;
        if (drop >= getUpFallenDrop)
            return true;
        if (drop > 0.32f && absPitch > 50f)
            return true;
        return false;
    }

    private void EnterFallingOrFallen(bool keepWalkWish)
    {
        // Stale success is a lie in the other direction: the flag latched at
        // 4.58 s on a standing dog and was still true while the body lay
        // 0.325 m down for the next 10-25 s. Clear it the frame the fall is
        // seen; the next attempt has to re-earn it.
        getUpSuccess = false;
        if (getUpFallTime < 0f)
            getUpFallTime = Time.fixedTime;
        if (!keepWalkWish)
            walkWanted = false;
        walkTurnUntil = 0f;
        if (walkWanted)
        {
            if (currentState != StanceState.GettingUp)
                EnterGettingUp();
            return;
        }
        // Brief Falling (RelaxAll) then Fallen so Drive does not keep
        // a half-alive Walk capture.
        if (getUpFrictionPeeled)
            ApplyGetUpFrictionPeel(false);
        if (currentState != StanceState.Fallen)
            currentState = StanceState.Falling;
    }

    private void TickFallenSense()
    {
        if (getUpFallTime < 0f)
            getUpFallTime = Time.fixedTime;
        if (!IsFallenHeap()
            && currentState != StanceState.Falling
            && Mathf.Abs(comOffsetX) <= fallCoMOffset)
        {
            currentState = StanceState.Balancing;
            return;
        }
        if (walkWanted)
        {
            if (currentState != StanceState.GettingUp)
                EnterGettingUp();
            return;
        }
        // Promote Falling → Fallen once the wish is gone so Walk() can
        // still request get-up later (DogDrive every Update).
        if (currentState == StanceState.Falling
            || currentState == StanceState.Walk
            || currentState == StanceState.Balancing
            || currentState == StanceState.Recovery)
            currentState = StanceState.Fallen;
    }

    private void EnterGettingUp()
    {
        currentState = StanceState.GettingUp;
        getUpAttempts++;
        getUpSuccess = false;
        getUpBorn = Time.fixedTime;
        getUpPlantBorn = -1f;
        getUpRiseBlend = 0f;
        getUpContactAge = 0f;
        getUpPikeBorn = -1f;
        // The tuck-push cycle starts fresh: no heap age, no half held, so the
        // first tuck latches getUpTuckMinSeconds after the entry even when no
        // paw is grounded yet.
        getUpPhaseBorn = -1f;
        getUpHeapAge = 0f;
        getUpPushPhase = false;
        getUpPushingNow = false;
        // Pre-fall / mid-rake settle bar kept GetUpIsStanding and
        // WalkSinkCollapsed false-positives for 30+ s (human
        // ClearStandingComBarForGetUp). Clear world CoM; re-measure
        // after BeginWalkAfterGetUp.
        standComHeightValid = false;
        ResetWalkClock();
        // Quiet SPD on welded hip/knee/paw K freezes the pike
        // (human GETUP-1). Peel once per enter; restore on rise.
        ApplyGetUpFrictionPeel(true);
    }

    private void TickGetUpSense()
    {
        if (!walkWanted)
        {
            if (getUpFrictionPeeled)
                ApplyGetUpFrictionPeel(false);
            currentState = StanceState.Fallen;
            getUpPlantBorn = -1f;
            getUpPushingNow = false;
            return;
        }
        if (GetUpIsStanding())
        {
            if (getUpPlantBorn < 0f)
                getUpPlantBorn = Time.fixedTime;
            if (Time.fixedTime - getUpPlantBorn >= getUpPlantSeconds)
                BeginWalkAfterGetUp();
        }
        else
            getUpPlantBorn = -1f;
    }

    // The measured standing trunk pose, in world metres: the highest chest /
    // pelvis pose seen while the dog was provably on its paws — all four
    // grounded and the chest inside the upright pitch band — so a later
    // heap on the floor can never lower it (a prone dog in a side view has a
    // level trunk as well; only the height tells them apart). Never updated
    // during a get-up and never cleared by a fall: it is the bar the rise is
    // measured against. Skipped on a leap — a jump is not a stance.
    private void TryCaptureGetUpStandTrunk()
    {
        if (!hasCom || IsLeap())
            return;
        if (currentState == StanceState.GettingUp
            || currentState == StanceState.Falling
            || currentState == StanceState.Fallen)
            return;
        if (!AllPawsGrounded())
            return;
        if (Mathf.Abs(chestPitch - chestTargetTilt) > getUpStandPitchDeg)
            return;
        if (!getUpStandTrunkValid)
        {
            getUpStandChestY = chestY;
            getUpStandPelvisY = pelvisY;
            getUpStandTrunkValid = true;
            return;
        }
        // Both ends advance together, from ONE frame: mixing the best chest of
        // one stride with the best pelvis of another inflates the bar past what
        // the dog is holding (measured spread 4 cm on dog_side04).
        if (chestY > getUpStandChestY && pelvisY > getUpStandPelvisY)
        {
            getUpStandChestY = chestY;
            getUpStandPelvisY = pelvisY;
        }
    }

    private bool GetUpIsStanding()
    {
        // PHYSICAL test, the same rule the recorder applies (DogTrialRecorder
        // DOG_UP_DROP / paws >= 3): the trunk back within getUpStandDrop of the
        // measured standing pose with at least getUpStandMinPaws planted, and
        // the plant has to hold getUpPlantSeconds (TickGetUpSense). A state
        // name, a timer or a level chest alone is not success — with those
        // alone the dog called itself up at 4.58 s while lying 0.325 m down.
        if (!hasCom || !getUpStandTrunkValid)
            return false;
        if (GroundedPawCount() < getUpStandMinPaws)
            return false;
        float riseDrop = Mathf.Max(getUpStandChestY - chestY,
            getUpStandPelvisY - pelvisY);
        if (riseDrop > getUpStandDrop)
            return false;
        if (Mathf.Abs(chestPitch) >= getUpFallenAbsPitch)
            return false;
        return true;
    }

    // Trunk height below the measured standing pose, m — the bar the get-up
    // tuck works from and the same pose GetUpIsStanding measures the rise
    // against. Falls back to the settle CoM column before the first stand is
    // captured, so a dog that never stood still gets a heap test.
    public float GetUpTrunkDropBar()
    {
        if (getUpStandTrunkValid)
            return Mathf.Max(getUpStandChestY - chestY, getUpStandPelvisY - pelvisY);
        if (standComHeightValid && hasCom)
            return Mathf.Max(0f, standComHeight - comPosition.y);
        return 0f;
    }

    // Read-only witness for the recorder (no control use): 0 rearLeftHip,
    // 1 rearRightHip, 2 rearLeftKnee, 3 rearRightKnee, 4 frontLeftShoulder,
    // 5 frontRightShoulder, 6 frontLeftElbow, 7 frontRightElbow. Deg; NaN when
    // the joint is unbound. The dog CSV had no limb angles, so a frozen get-up
    // could not be told from a tuck that moved the joints and lost the trunk.
    public float GetUpLimbAngle(int index)
    {
        HingeJoint2D j;
        switch (index)
        {
            case 0: j = rearLeftHipJoint; break;
            case 1: j = rearRightHipJoint; break;
            case 2: j = rearLeftKneeJoint; break;
            case 3: j = rearRightKneeJoint; break;
            case 4: j = frontLeftShoulderJoint; break;
            case 5: j = frontRightShoulderJoint; break;
            case 6: j = frontLeftElbowJoint; break;
            case 7: j = frontRightElbowJoint; break;
            default: return float.NaN;
        }
        return j != null ? j.jointAngle : float.NaN;
    }


    // Capture walk stays off until upright. Then phase 0 DualSupport.
    private void BeginWalkAfterGetUp()
    {
        if (getUpBorn >= 0f && getUpSeconds < 0f)
            getUpSeconds = Time.fixedTime - getUpBorn;
        getUpSuccess = true;
        getUpPlantBorn = -1f;
        getUpPushingNow = false;
        // Re-measure settle stand in world space after the rise so the
        // pre-fall column does not arm WalkCollapsed on the first Dual.
        if (hasCom)
        {
            standComHeight = comPosition.y;
            standPelvisY = pelvisY;
            standComHeightValid = true;
        }
        if (getUpFrictionPeeled)
            ApplyGetUpFrictionPeel(false);
        ResetWalkClock();
        ClearWalkOnsetLog();
        walkPhase = 0f;
        currentState = StanceState.Walk;
        LatchWalkPelvisHold();
        SnapshotWalkEntry();
    }

    private void ClearWalkOnsetLog()
    {
        walkOnsetBorn = -1f;
        WalkOnsetLogCount = 0;
        for (int i = 0; i < WalkOnsetLogCapacity; i++)
            walkOnsetWritten[i] = false;
    }

    private void Drive()
    {
        activationSpeedThisStep = muscleActivationSpeed;

        if (currentState == StanceState.Falling
            || currentState == StanceState.Fallen)
        {
            RelaxAll();
            return;
        }

        if (IsLeap())
        {
            DriveLeap();
            return;
        }

        // GettingUp must move the heap (tuck → rise). Quiet plant alone
        // left getUpAttempts=1 and a −98° freeze to run end.
        if (currentState == StanceState.GettingUp)
        {
            DriveGetUp();
            return;
        }

        if (IsWalk())
        {
            DriveWalk();
            return;
        }

        if (startupHoldSeconds > 0f && spawnedFixedTime >= 0f
            && Time.fixedTime - spawnedFixedTime < startupHoldSeconds)
            return;

        DriveQuietPlant();
    }

    private void DriveQuietPlant()
    {
        float pawBalance = PawComCommand();

        DrivePaw(frontRightPawJoint, frontRightPawFlexor, frontRightPawExtensor, frontRightGrounded, pawBalance);
        DrivePaw(frontLeftPawJoint, frontLeftPawFlexor, frontLeftPawExtensor, frontLeftGrounded, pawBalance);
        DrivePaw(rearRightPawJoint, rearRightPawFlexor, rearRightPawExtensor, rearRightGrounded, pawBalance);
        DrivePaw(rearLeftPawJoint, rearLeftPawFlexor, rearLeftPawExtensor, rearLeftGrounded, pawBalance);

        // c13 plant-gated hip lift (8°) lost fourFeet (0.027). Reverted.
        // Height stays on knee/elbow only. Both minus: opening the
        // standing V raises; plus fold from 24° is a crouch / sploot.
        float lift = HeightLiftDeg();
        float kneeTarget = Mathf.Clamp(kneeBaseAngle - lift, 0f, 120f);
        float elbowTarget = Mathf.Clamp(elbowBaseAngle - lift, 0f, 120f);

        float hipP = hipPGain;
        if (startupHipSeconds > 0f && spawnedFixedTime >= 0f
            && Time.fixedTime - spawnedFixedTime < startupHipSeconds
            && startupHipP > 0f)
            hipP = startupHipP;
        // Get-up uses the rake P so a prone heap reaches plant faster.
        if (currentState == StanceState.GettingUp && startupHipP > hipP)
            hipP = startupHipP;

        ControlJointStable(rearLeftHipJoint, hipBaseAngle, hipP, hipDGain,
            rearLeftHipFlexor, rearLeftHipExtensor, ref rearLeftHipInertia, poseHoldDeadzoneDeg);
        ControlJointStable(rearRightHipJoint, hipBaseAngle, hipP, hipDGain,
            rearRightHipFlexor, rearRightHipExtensor, ref rearRightHipInertia, poseHoldDeadzoneDeg);
        ControlJointStable(frontLeftShoulderJoint, shoulderBaseAngle, hipP, hipDGain,
            frontLeftShoulderFlexor, frontLeftShoulderExtensor, ref frontLeftShoulderInertia, poseHoldDeadzoneDeg);
        ControlJointStable(frontRightShoulderJoint, shoulderBaseAngle, hipP, hipDGain,
            frontRightShoulderFlexor, frontRightShoulderExtensor, ref frontRightShoulderInertia, poseHoldDeadzoneDeg);

        bool lumbarReady = lumbarDelaySeconds <= 0f || spawnedFixedTime < 0f
            || Time.fixedTime - spawnedFixedTime >= lumbarDelaySeconds;
        if (lumbarReady)
        {
            if (useLumbarJointRest)
                ControlJointStable(lumbarJoint, lumbarJointRestDeg, lumbarPGain, lumbarDGain,
                    lumbarFlexor, lumbarExtensor, ref lumbarInertia);
            else
                DriveLumbarToWorld(chestTargetTilt);
        }

        ControlJointStable(rearLeftKneeJoint, kneeTarget, kneePGain, kneeDGain,
            rearLeftKneeFlexor, rearLeftKneeExtensor, ref rearLeftKneeInertia, poseHoldDeadzoneDeg);
        ControlJointStable(rearRightKneeJoint, kneeTarget, kneePGain, kneeDGain,
            rearRightKneeFlexor, rearRightKneeExtensor, ref rearRightKneeInertia, poseHoldDeadzoneDeg);
        ControlJointStable(frontLeftElbowJoint, elbowTarget, kneePGain, kneeDGain,
            frontLeftElbowFlexor, frontLeftElbowExtensor, ref frontLeftElbowInertia, poseHoldDeadzoneDeg);
        ControlJointStable(frontRightElbowJoint, elbowTarget, kneePGain, kneeDGain,
            frontRightElbowFlexor, frontRightElbowExtensor, ref frontRightElbowInertia, poseHoldDeadzoneDeg);

        ControlJointStable(tailJoint, IdleTailTarget(), tailPGain, tailDGain,
            tailFlexor, tailExtensor, ref tailInertia);

        DriveJaw();

        if (vestibular != null)
        {
            float neckGoal = IdleNeckWorldTarget();
            float headGoal = neckGoal;
            if (idleYawn > 0.01f)
                headGoal = Mathf.Clamp(neckGoal + 6f * idleYawn, -28f, 28f);
            ControlSegmentToWorldUpright(
                vestibular.GetNeckTilt(), vestibular.GetNeckAngularVelocity(),
                neckJoint, neckFlexor, neckExtensor, ref neckEffectiveInertia, neckGoal);
            ControlSegmentToWorldUpright(
                vestibular.GetHeadTilt(), vestibular.GetHeadAngularVelocity(),
                headJoint, headFlexor, headExtensor, ref headEffectiveInertia, headGoal);
        }
    }

    // Fallen heap → upright sit. Quiet plant alone held leftover sit
    // targets on a −98° trunk (attempts=1, freeze to run end). Pike
    // tucks the four columns under the trunk; rise blends to the
    // quiet sit. Same SPD / signs; local P boost only. Capture Walk
    // stays off until BeginWalkAfterGetUp.
    // dog_w73: knees first. Measured post-fall knee only ~35° while
    // hip already ~48° — simultaneous hip+knee closed-chain starved
    // the stifle. Lead knee/elbow to getUpKneeFlex, then hip.
    private void DriveGetUp()
    {
        float dt = Time.fixedDeltaTime;
        bool anyPaw = frontRightGrounded || frontLeftGrounded
                      || rearRightGrounded || rearLeftGrounded;
        if (anyPaw)
            getUpContactAge += dt;
        else
            getUpContactAge = 0f;

        float absPitch = Mathf.Abs(chestPitch);
        float squatDeg = Mathf.Max(20f, getUpSquatAbsPitch);
        float tuckMin = Mathf.Max(0.05f, getUpTuckMinSeconds);
        float hipsStandDeg = Mathf.Max(20f, getUpStandPitchDeg + 5f);

        // HEAP, not "a paw is down": a trunk this far below the measured stand
        // bar (the same bar GetUpIsStanding uses) is a dog on the floor
        // whatever the paws report. dog_gu12 lay with the pelvis 0.33 m down
        // and all four paws in the air: the old anyPaw gate kept the quiet base
        // pose for the last 18.6 s (dogGetUpAttempts 0). The tuck is the
        // movement that PUTS the paws on the floor, so it must be allowed to
        // start without one.
        float trunkBar = GetUpTrunkDropBar();
        bool heap = trunkBar >= getUpConnectDrop || absPitch >= squatDeg;
        if (heap)
            getUpHeapAge += dt;
        else
            getUpHeapAge = 0f;

        // A grounded paw settles the tuck on its contact age; a heap with
        // nothing grounded settles on its own age — there is no contact to wait
        // for, the tuck has to create it.
        bool tuckDue = heap && (anyPaw ? getUpContactAge >= tuckMin : getUpHeapAge >= tuckMin);
        if (!tuckDue)
        {
            getUpPikeBorn = -1f;
            getUpPhaseBorn = -1f;
            getUpPushPhase = false;
        }
        else
        {
            if (getUpPikeBorn < 0f)
            {
                getUpPikeBorn = Time.fixedTime;
                getUpPhaseBorn = Time.fixedTime;
                getUpPushPhase = false;
            }
            // A push needs a floor. The extension reacts on the trunk, and with
            // nothing planted that reaction is the only thing it can move:
            // dog_gu13 pushed 16 times after the second fall, every one with
            // 0 paws, and the dog ended on its back rocking through pitch ±180
            // from 20.6 s to the end of the run. With no contact the tuck alone
            // folds the limbs under the body and waits for a paw; the push
            // starts the moment one lands (and stops if the paw leaves again).
            // Tuck ⇄ push clock. The old latch released only when |pitch| fell
            // under the squat, which a pelvis-down heap never reaches, so the
            // dog froze in one tuck. A bounded tuck, then a bounded push,
            // repeating while the trunk is still a heap, is a dog that tries.
            float phaseAge = Time.fixedTime - getUpPhaseBorn;
            if (anyPaw && !getUpPushPhase && phaseAge >= Mathf.Max(0.10f, getUpTuckSeconds))
            {
                getUpPushPhase = true;
                getUpPhaseBorn = Time.fixedTime;
            }
            else if (getUpPushPhase
                     && (!anyPaw || phaseAge >= Mathf.Max(0.10f, getUpPushSeconds)))
            {
                getUpPushPhase = false;
                getUpPhaseBorn = Time.fixedTime;
            }
        }

        // Tuck = limbs folded under the trunk (the paws reach the floor under
        // the body); push = those same limbs extending against it. The rise
        // blend drives the extension, so the push is the old rise branch aimed
        // at a body that is still on the ground.
        bool pike = tuckDue && !getUpPushPhase;
        bool rising = !pike;
        getUpPushingNow = heap && getUpPushPhase;

        float hipTarget;
        float kneeTarget;
        float shoulderTarget;
        float elbowTarget;
        bool hipIn = false;
        if (pike)
        {
            getUpRiseBlend = 0f;
            float tuckHip = Mathf.Max(8f, getUpHipFlex);
            float tuckKnee = Mathf.Clamp(Mathf.Max(40f, getUpKneeFlex), 0f, 120f);
            float lead = Mathf.Max(0.05f, getUpKneeLeadSeconds);
            float pikeAge = getUpPikeBorn >= 0f ? Time.fixedTime - getUpPikeBorn : 0f;
            hipIn = pikeAge >= lead;
            // Rear: more-minus hip + plus knee. Front: more-plus
            // shoulder (caudal) + plus elbow — paws under the trunk.
            // Knee/elbow always full tuck on pike; hip waits the lead.
            kneeTarget = tuckKnee;
            elbowTarget = tuckKnee;
            if (hipIn)
            {
                hipTarget = hipBaseAngle - tuckHip;
                shoulderTarget = shoulderBaseAngle + tuckHip;
            }
            else
            {
                hipTarget = hipBaseAngle;
                shoulderTarget = shoulderBaseAngle;
            }
        }
        else
        {
            float riseT = Mathf.Max(0.2f, getUpRiseSeconds);
            getUpRiseBlend = Mathf.Clamp01(getUpRiseBlend + dt / riseT);
            hipTarget = hipBaseAngle;
            kneeTarget = kneeBaseAngle;
            shoulderTarget = shoulderBaseAngle;
            elbowTarget = elbowBaseAngle;
        }

        float hipP = hipPGain;
        float kneeP = kneePGain;
        if (startupHipP > hipP)
            hipP = startupHipP;
        // Quiet P saturates on a 70° pike error; boost only here.
        // Knee lead needs extra distal P so the stifle actually travels
        // (measured 35° of a 95° target when hip commanded first).
        if (pike)
        {
            kneeP = Mathf.Max(kneeP, 5f);
            if (hipIn)
                hipP = Mathf.Max(hipP, 4f);
        }
        else if (rising && absPitch >= hipsStandDeg)
        {
            hipP = Mathf.Max(hipP, 4f);
            kneeP = Mathf.Max(kneeP, 3f);
        }

        float pawBalance = PawComCommand();
        DrivePaw(frontRightPawJoint, frontRightPawFlexor, frontRightPawExtensor, frontRightGrounded, pawBalance);
        DrivePaw(frontLeftPawJoint, frontLeftPawFlexor, frontLeftPawExtensor, frontLeftGrounded, pawBalance);
        DrivePaw(rearRightPawJoint, rearRightPawFlexor, rearRightPawExtensor, rearRightGrounded, pawBalance);
        DrivePaw(rearLeftPawJoint, rearLeftPawFlexor, rearLeftPawExtensor, rearLeftGrounded, pawBalance);

        ControlJointStable(rearLeftHipJoint, hipTarget, hipP, hipDGain,
            rearLeftHipFlexor, rearLeftHipExtensor, ref rearLeftHipInertia);
        ControlJointStable(rearRightHipJoint, hipTarget, hipP, hipDGain,
            rearRightHipFlexor, rearRightHipExtensor, ref rearRightHipInertia);
        ControlJointStable(frontLeftShoulderJoint, shoulderTarget, hipP, hipDGain,
            frontLeftShoulderFlexor, frontLeftShoulderExtensor, ref frontLeftShoulderInertia);
        ControlJointStable(frontRightShoulderJoint, shoulderTarget, hipP, hipDGain,
            frontRightShoulderFlexor, frontRightShoulderExtensor, ref frontRightShoulderInertia);

        // Open the V only on rise with paws down — HeightLift toward
        // spawn inverted-V during pike fights the tuck.
        float lift = 0f;
        if (rising && getUpRiseBlend > 0.15f && AllPawsGrounded())
            lift = HeightLiftDeg();
        kneeTarget = Mathf.Clamp(kneeTarget - lift, 0f, 120f);
        elbowTarget = Mathf.Clamp(elbowTarget - lift, 0f, 120f);

        ControlJointStable(rearLeftKneeJoint, kneeTarget, kneeP, kneeDGain,
            rearLeftKneeFlexor, rearLeftKneeExtensor, ref rearLeftKneeInertia);
        ControlJointStable(rearRightKneeJoint, kneeTarget, kneeP, kneeDGain,
            rearRightKneeFlexor, rearRightKneeExtensor, ref rearRightKneeInertia);
        ControlJointStable(frontLeftElbowJoint, elbowTarget, kneeP, kneeDGain,
            frontLeftElbowFlexor, frontLeftElbowExtensor, ref frontLeftElbowInertia);
        ControlJointStable(frontRightElbowJoint, elbowTarget, kneeP, kneeDGain,
            frontRightElbowFlexor, frontRightElbowExtensor, ref frontRightElbowInertia);

        bool lumbarReady = lumbarDelaySeconds <= 0f || spawnedFixedTime < 0f
            || Time.fixedTime - spawnedFixedTime >= lumbarDelaySeconds;
        if (lumbarReady)
        {
            if (useLumbarJointRest)
                ControlJointStable(lumbarJoint, lumbarJointRestDeg, lumbarPGain, lumbarDGain,
                    lumbarFlexor, lumbarExtensor, ref lumbarInertia);
            else
                DriveLumbarToWorld(chestTargetTilt);
        }

        ControlJointStable(tailJoint, IdleTailTarget(), tailPGain, tailDGain,
            tailFlexor, tailExtensor, ref tailInertia);
        DriveJaw();

        if (vestibular != null)
        {
            float neckGoal = IdleNeckWorldTarget();
            ControlSegmentToWorldUpright(
                vestibular.GetNeckTilt(), vestibular.GetNeckAngularVelocity(),
                neckJoint, neckFlexor, neckExtensor, ref neckEffectiveInertia, neckGoal);
            ControlSegmentToWorldUpright(
                vestibular.GetHeadTilt(), vestibular.GetHeadAngularVelocity(),
                headJoint, headFlexor, headExtensor, ref headEffectiveInertia, neckGoal);
        }
    }

    public float FacingSign()
    {
        return facing >= 0f ? 1f : -1f;
    }

    // Brain writes the side. Picture on Visual; the trot inverts flex/extend.
    public void SetFacing(float sign)
    {
        float next = sign >= 0f ? 1f : -1f;
        if (next == FacingSign())
            return;
        facing = next;
        if (IsWalk() || walkWanted)
        {
            ResetWalkClock();
            NoteWalkTurn();
        }
        else
            NoteSitTurn();
        if (body != null)
            body.ApplyLookFacing(facing);
    }

    // Brain already wrote SetFacing (sit look-glance, greet, hunt stare).
    // Look scale flipped at once, so the turn read as a texture swap.
    // Hop near-front + near-hind then plant. Picture only — colliders
    // stay. Walk has its own hop (walkTurnAmp). Sleep / hunt-recover
    // plant / scratch skip. 0 = leave the instant flip. Not a yaw force.
    private void NoteSitTurn()
    {
        if (sitTurnAmp < 0.01f)
            return;
        if (!CanIdle())
            return;
        if (idleSleep > 0.01f)
            return;
        if (idleHunt > 0.01f)
            return;
        if (idleScratch > 0.01f)
            return;
        float now = Time.time;
        sitTurnBorn = now;
        sitTurnUntil = now + 0.36f;
    }

    // 0 at plant edges, peak mid-hop. Includes sitTurnAmp.
    public float SitTurnHop()
    {
        if (sitTurnAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (!CanIdle())
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (Time.time >= sitTurnUntil)
            return 0f;
        float dur = sitTurnUntil - sitTurnBorn;
        if (dur < 0.05f)
            return 0f;
        float p = Mathf.Clamp01((Time.time - sitTurnBorn) / dur);
        return Mathf.Clamp(sitTurnAmp, 0f, 1.5f) * Mathf.Sin(p * Mathf.PI);
    }

    // Brain already wrote SetFacing on a trot (wander FaceToward then
    // Walk, follow about-face, hunt chase). Look scale plus a clock
    // reset still read as a texture swap. Hop the same near pair as
    // sit, then the stride. Picture only — colliders stay, leftover
    // sit is not a freeze. Leap / Sleep skip. 0 = instant flip.
    // Not a yaw force.
    private void NoteWalkTurn()
    {
        if (walkTurnAmp < 0.01f)
            return;
        if (energyRest)
            return;
        if (IsLeap())
            return;
        if (!walkWanted && !IsWalk())
            return;
        float now = Time.time;
        walkTurnBorn = now;
        walkTurnUntil = now + 0.32f;
    }

    // 0 at plant edges, peak mid-hop. Includes walkTurnAmp.
    public float WalkTurnHop()
    {
        if (walkTurnAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (!walkWanted && !IsWalk())
            return 0f;
        if (Time.time >= walkTurnUntil)
            return 0f;
        float dur = walkTurnUntil - walkTurnBorn;
        if (dur < 0.05f)
            return 0f;
        float p = Mathf.Clamp01((Time.time - walkTurnBorn) / dur);
        return Mathf.Clamp(walkTurnAmp, 0f, 1.5f) * Mathf.Sin(p * Mathf.PI);
    }

    // Picture only. Leftover sit keeps the four colliders planted, so
    // a joint twitch still reads as a standing rake. Lift the SWING
    // diagonal Visuals (half 0: FL+RR, half 1: FR+RL) once those
    // knees peel. Dog LateUpdate poses proximal+mid+paw; paw-only
    // floated a stamp on planted thighs. About-face hop owns that
    // window. Leap / Sleep / rest skip. 0 = old planted pictures. Not
    // a lift force. Clock follows the knee ramp, not hip reach.
    public float WalkStrideHop(bool front, bool right)
    {
        if (walkStrideAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (!walkWanted && !IsWalk())
            return 0f;
        if (!walkCaptureArmed)
            return 0f;
        if (WalkLimbIsStance(front, right))
            return 0f;
        // Same clock as the swing-knee peel. SwingU stays 0 until the
        // paw already travelled (sit-trot closed loop).
        float u = SwingPeelU();
        if (walkSwungAir)
            u = Mathf.Max(u, WalkAirBlend());
        return Mathf.Clamp(walkStrideAmp, 0f, 1.5f) * Mathf.Sin(u * Mathf.PI);
    }

    // Picture only. DriveWalk leftover sit holds the trunk collider,
    // so swing columns still ride a statue chest. Same two-dip clock
    // as walkHeadBob (each diagonal plant). Forage idleWag only —
    // hunt stalk stays stiff, CLI -dogWalk leaves idleWag 0. Hop
    // mutes. Leap / Sleep / rest skip. 0 = old still withers. Not a
    // lift force. Same walkPhase, not a new period.
    public float WalkTrunkBob()
    {
        if (walkBobAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (!walkWanted && !IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleWag < 0.01f)
            return 0f;
        if (WalkTurnHop() > 0.01f)
            return 0f;
        return Mathf.Clamp(walkBobAmp, 0f, 1.5f)
            * WalkPulse();
    }

    // Picture only. DriveWalk leftover sit holds the pelvis collider,
    // so rear swing still rode a statue rump after withers heaved.
    // Same two-dip clock as WalkTrunkBob (in phase: a trot, not a
    // see-saw). Forage idleWag only — hunt stalk stays stiff, CLI
    // -dogWalk leaves idleWag 0. Hop mutes. Leap / Sleep / rest
    // skip. 0 = old still rump. Not a lift force. Sit pant after
    // EndWalk is SitRump on idlePant. Sit yawn is SitYawnRump on
    // idleYawn. Sit sniff is SitSniffRump on idleSniff. Same
    // walkPhase.
    public float WalkRumpBob()
    {
        if (walkRumpAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (!walkWanted && !IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleWag < 0.01f)
            return 0f;
        if (WalkTurnHop() > 0.01f)
            return 0f;
        return Mathf.Clamp(walkRumpAmp, 0f, 1.5f)
            * WalkPulse();
    }

    // Same first-hinge spring as sit idleWag. DriveWalk leftover sit
    // held the 8.5 Hz sit clock, so withers / rump / neck already
    // planted on walkPhase while the tail still sat-wagged. Same
    // two-dip clock as WalkTrunkBob (in phase: a trot, not a sit
    // metronome). Forage idleWag only — hunt stalk stays the stiff
    // raise, CLI -dogWalk leaves idleWag 0. Hop mutes (plant, not
    // 8.5 Hz). Leap / Sleep / rest skip. 0 = the old sit-rate wag
    // on the trot. Not a new idleTail (Walk() would zero it). Sit
    // pant after EndWalk hitches SitPantTail (droop with the
    // pump, not this clock). Sit look hitches SitLookTail (perk /
    // droop, not this clock). Sit sniff hitches SitSniffTail
    // (droop with the dirt bob). Sit scratch hitches
    // SitScratchTail (droop with the rake). Sit yawn hitches
    // SitYawnTail (droop with the gape). Sit chew hitches
    // SitChewTail (droop with the snap). Distal limp is
    // WalkDistalTail. Same walkPhase.
    public float WalkTail()
    {
        if (walkTailAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (!walkWanted && !IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleWag < 0.01f)
            return 0f;
        if (WalkTurnHop() > 0.01f)
            return 0f;
        return Mathf.Clamp(walkTailAmp, 0f, 1.5f)
            * WalkPulse();
    }

    // Picture only. Forage Walk already wags the first hinge
    // (WalkTail on walkPhase) and hitches withers / rump / neck /
    // face with the trot — leftover walk held Tail2–Tail5 on
    // TreeSpring restAngle 0 (muscles only on the first hinge —
    // by design) so the gait still read as a stiff caudal rod on
    // a bobbing trunk. Limp the distal Visuals (accumulating
    // 6°…12°) following that same two-dip clock (env already
    // pulses walkPhase; no extra clock, not Sleep's 1.6 hitch,
    // not sniff's ~3 Hz bob, not yawn's envelope, not pant's
    // 6.5 Hz pump, not chew's 11 Hz snap, not look's signed bob,
    // not scratch's ~14 Hz rake, not quiet sit's 8.5 Hz, and not
    // a new distal muscle). Plus = clockwise, tip down, same
    // sign as IdleTailTarget's +16° * WalkTail. Hop mutes
    // (plant). Hunt stalk stays the stiff raise. CLI -dogWalk
    // leaves idleWag 0. Leap / Sleep / rest skip. Wander sniff
    // keeps WalkTail (SitSniffDistalTail skips Walk). Colliders
    // stay. Drive already wrote idleWag. Sit sniff distal is
    // SitSniffDistalTail. Quiet sit distal is SitQuietDistalTail.
    // Hunt recover distal is SitHuntDistalTail. Hunt stalk distal
    // is WalkHuntDistalTail. Hunt leap distal is LeapHuntDistalTail.
    // No idleTail: Walk() would zero it. 0 = the old rest-spring
    // forage Walk stick.
    public float WalkDistalTail()
    {
        if (walkDistalTailAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (!walkWanted && !IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleWag < 0.01f)
            return 0f;
        if (WalkTurnHop() > 0.01f)
            return 0f;
        return Mathf.Clamp(walkDistalTailAmp, 0f, 1.5f)
            * WalkPulse();
    }

    // Picture only. Generic walkWanted pin made Forage follow read as
    // hunt. Same two-dip clock as walkHeadBob. Forage idleWag only —
    // hunt pin stays on idleHunt, wander sniff keeps the half-fold,
    // CLI -dogWalk leaves idleWag 0. Hop mutes. Leap / Sleep / rest
    // skip. 0 = the old walk pin. Not a hinge. Sit pant after
    // EndWalk is SitEar on idlePant. Same walkPhase.
    public float WalkEarFlop()
    {
        if (walkEarAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (!walkWanted && !IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idleWag < 0.01f)
            return 0f;
        if (WalkTurnHop() > 0.01f)
            return 0f;
        return Mathf.Clamp(walkEarAmp, 0f, 1.5f)
            * WalkPulseAbs();
    }

    // Picture + DriveJaw. Closed follow looked stiffer than the hunt
    // snarl crack. Same two-dip clock as walkHeadBob. Forage idleWag
    // only — hunt crack stays on idleHunt, wander sniff stays closed
    // (nose on dirt), CLI -dogWalk leaves idleWag 0. Hop mutes. Leap /
    // Sleep / rest skip. 0 = the old closed forage walk. Not a new
    // idlePant (Walk() would zero it). Sit pant after EndWalk is still
    // idlePant. Same walkPhase.
    public float WalkPant()
    {
        if (walkPantAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (!walkWanted && !IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idleWag < 0.01f)
            return 0f;
        if (WalkTurnHop() > 0.01f)
            return 0f;
        return Mathf.Clamp(walkPantAmp, 0f, 1.5f)
            * (0.55f + 0.45f * WalkPulseAbs());
    }

    // Picture only. Rest stamps next to a hanging tongue still read as
    // a hold muzzle. Same two-dip clock as walkHeadBob. Forage idleWag
    // only — hunt pin stays on idleHunt, wander sniff keeps its own fan,
    // CLI -dogWalk leaves idleWag 0. Hop mutes. Leap / Sleep / rest
    // skip. 0 = the old rest forage walk. Not a new idleWhisker
    // (Walk() would zero it). Sit sniff stays idleSniff. Sit pant
    // after EndWalk is SitWhisker on idlePant. Sit chew is
    // SitChewWhisker on idleChew. Same walkPhase.
    public float WalkWhisker()
    {
        if (walkWhiskerAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (!walkWanted && !IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idleWag < 0.01f)
            return 0f;
        if (WalkTurnHop() > 0.01f)
            return 0f;
        return Mathf.Clamp(walkWhiskerAmp, 0f, 1.5f)
            * (0.55f + 0.45f * WalkPulseAbs());
    }

    // Picture only. Rest pupil next to a hanging tongue still read as
    // a hunt-lock stare. Same two-dip clock as walkHeadBob. Forage
    // idleWag only — hunt recover owns SitHuntSquint, hunt stalk
    // Walk owns WalkHuntSquint, hunt leap owns LeapHuntSquint,
    // wander sniff keeps the open track
    // eye, CLI -dogWalk leaves idleWag 0. Hop mutes. Sleep /
    // rest skip. 0 = the old rest forage walk. Not a new idleBlink
    // (Walk() would zero it). Sleep / yawn / blink still own the
    // full slit. Sit pant after EndWalk is SitSquint on idlePant.
    // Same walkPhase.
    public float WalkSquint()
    {
        if (walkSquintAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (!walkWanted && !IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idleLook > 0.01f)
            return 0f;
        if (idleWag < 0.01f)
            return 0f;
        if (WalkTurnHop() > 0.01f)
            return 0f;
        return Mathf.Clamp(walkSquintAmp, 0f, 1.5f)
            * (0.55f + 0.45f * WalkPulseAbs());
    }

    // Picture only. Rest snout next to a hanging tongue still read as
    // a hunt-lock muzzle. Same two-dip clock as walkHeadBob. Forage
    // idleWag only — hunt drop stays on idleHunt, wander sniff keeps
    // its own dirt drop, CLI -dogWalk leaves idleWag 0. Hop mutes.
    // Leap owns LeapHuntNose. Sleep / rest skip. 0 = the old rest
    // forage walk. Not a new idleNose (Walk() would zero it). Sit
    // pant after EndWalk is SitNose on idlePant. Sit yawn is
    // SitYawnNose on idleYawn. Sit chew is SitChewNose on idleChew.
    // Same walkPhase.
    public float WalkNose()
    {
        if (walkNoseAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (!walkWanted && !IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idleWag < 0.01f)
            return 0f;
        if (WalkTurnHop() > 0.01f)
            return 0f;
        return Mathf.Clamp(walkNoseAmp, 0f, 1.5f)
            * (0.55f + 0.45f * WalkPulseAbs());
    }

    // Picture only. Rest pupil next to a dropped snout still read as
    // a hunt-lock stare through the squint. Same two-dip clock as
    // walkHeadBob. Forage idleWag only — hunt drop stays on idleHunt,
    // wander sniff keeps its own dirt drop, look-up stays idleLook,
    // CLI -dogWalk leaves idleWag 0. Hop mutes. Leap owns
    // LeapHuntEye. Sleep / rest skip. 0 = the old rest forage
    // walk. Not a new idleEye (Walk() would zero it). Sleep /
    // yawn / blink still hide the dot. Sit pant after EndWalk is
    // SitEye on idlePant. Sit chew is SitChewEye on idleChew. Same walkPhase.
    public float WalkEye()
    {
        if (walkEyeAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (!walkWanted && !IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idleLook > 0.01f)
            return 0f;
        if (idleWag < 0.01f)
            return 0f;
        if (WalkTurnHop() > 0.01f)
            return 0f;
        return Mathf.Clamp(walkEyeAmp, 0f, 1.5f)
            * (0.55f + 0.45f * WalkPulseAbs());
    }

    // Picture only. Rest brow next to a hanging tongue still read as
    // a hunt-lock forehead on the yellow box. Same two-dip clock as
    // walkHeadBob. Forage idleWag only — hunt furrow stays on
    // idleHunt, wander sniff keeps its own drop, look-up stays
    // idleLook, CLI -dogWalk leaves idleWag 0. Hop mutes. Leap /
    // Sleep owns SitSleepBrow / rest skip. 0 = the old rest forage
    // walk. Not a new idleBrow (Walk() would zero it). Sit pant
    // after EndWalk is SitBrow on idlePant. Sit yawn is SitYawnBrow
    // on idleYawn. Sit chew is SitChewBrow on idleChew. Same
    // walkPhase.
    public float WalkBrow()
    {
        if (walkBrowAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (!walkWanted && !IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idleLook > 0.01f)
            return 0f;
        if (idleWag < 0.01f)
            return 0f;
        if (WalkTurnHop() > 0.01f)
            return 0f;
        return Mathf.Clamp(walkBrowAmp, 0f, 1.5f)
            * (0.55f + 0.45f * WalkPulseAbs());
    }

    // Picture only. Hanging tongue with no air still read as a stamp,
    // not a pant (Bark puff is Call). Same two-dip clock as
    // walkHeadBob. Needs WalkPant so a closed mouth does not leak
    // vapor. Forage idleWag only — hunt recover owns SitHuntPuff,
    // hunt stalk owns WalkHuntPuff (this gate still needs WalkPant;
    // idleHunt skips), wander sniff stays closed, CLI -dogWalk
    // leaves idleWag 0. Hop mutes. Leap owns LeapHuntPuff. Sleep /
    // rest skip. 0 = the old silent forage walk tongue. Not a new
    // idlePant (Walk() would zero it). Sit pant after EndWalk is
    // SitPuff on idlePant. Sit yawn is SitYawnPuff on idleYawn.
    // Sit chew is SitChewPuff on idleChew (SitPuff / SitYawnPuff
    // skip idleChew). Hunt recover sit is SitHuntPuff on idleHunt
    // (this gate still needs WalkPant; SitPuff skips idleHunt).
    // Same walkPhase.
    public float WalkPuff()
    {
        if (walkPuffAmp < 0.01f)
            return 0f;
        if (WalkPant() < 0.01f)
            return 0f;
        return Mathf.Clamp(walkPuffAmp, 0f, 1.5f)
            * WalkPulseAbs();
    }

    // Picture only. Sit idlePant hung a tongue with no air after
    // EndWalk — same silent stamp WalkPuff already closed on the
    // trot. Pulse is the pump above the 0.55 hang so a closed mouth
    // does not leak. Brain already wrote idlePant; Walk owns
    // WalkPuff. Sit yawn is SitYawnPuff on idleYawn (this gate
    // still needs idlePant). Sit chew owns SitChewPuff (this
    // gate still needs idlePant; idleChew skips). Hunt recover
    // owns SitHuntPuff (this gate still needs idlePant; idleHunt
    // skips). Stalk Walk owns WalkHuntPuff. Leap owns LeapHuntPuff.
    // Sleep / Leap / rest skip. No idlePuff: Walk() would zero it.
    // 0 = the old silent sit tongue.
    public float SitPuff()
    {
        if (sitPuffAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idlePant < 0.01f)
            return 0f;
        float pulse = Mathf.Clamp01((idlePant - 0.55f) / 0.45f);
        return Mathf.Clamp(sitPuffAmp, 0f, 1.5f) * pulse;
    }

    // Picture only. Sit idlePant hung a round rest pupil after
    // EndWalk — same hunt-lock stare WalkSquint already closed on
    // the trot. Hold a thinner slit on the same 0.55 hang so the
    // eye does not flash open between pumps. Brain already wrote
    // idlePant; Walk owns WalkSquint. Hunt recover owns
    // SitHuntSquint. Hunt stalk Walk owns WalkHuntSquint.
    // Hunt leap owns LeapHuntSquint.
    // Sit chew owns SitChewSquint (wag-off meal; SitSquint skips
    // idleChew so leftover sit held the open sniff-track eye).
    // Sit chew ear is SitChewEar. Sit chew brow is SitChewBrow.
    // Look-up / Sleep / Leap / rest skip
    // (yawn / blink still own the full slit). No idleBlink:
    // Walk() would zero it. 0 = the old round sit-pant eye.
    // Pupil stays visible under the squint.
    public float SitSquint()
    {
        if (sitSquintAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleLook > 0.01f)
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idlePant < 0.01f)
            return 0f;
        float pulse = Mathf.Clamp01((idlePant - 0.55f) / 0.45f);
        return Mathf.Clamp(sitSquintAmp, 0f, 1.5f)
            * (0.55f + 0.45f * pulse);
    }

    // Picture only. Sit idlePant hung a rest muzzle after EndWalk —
    // same hunt-lock snout WalkNose already closed on the trot. Hold
    // a milder drop on the same 0.55 hang so the nose does not flash
    // rest between pumps. Brain already wrote idlePant; Walk owns
    // WalkNose. Hunt recover / stalk keep idleHunt. Leap owns
    // LeapHuntNose. Sleep owns SitSleepNose. Look-up / chew /
    // rest skip (sniff keeps its own dirt drop, sit yawn owns
    // SitYawnNose, sit chew owns SitChewNose). No
    // idleNose: Walk() would zero it. 0 = the old rest sit-pant
    // snout.
    public float SitNose()
    {
        if (sitNoseAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleLook > 0.01f)
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idlePant < 0.01f)
            return 0f;
        float pulse = Mathf.Clamp01((idlePant - 0.55f) / 0.45f);
        return Mathf.Clamp(sitNoseAmp, 0f, 1.5f)
            * (0.55f + 0.45f * pulse);
    }

    // Picture only. Sit idlePant hung a rest pupil after EndWalk —
    // same hunt-lock stare WalkEye already closed on the trot. Hold
    // a milder drop on the same 0.55 hang so the eye does not flash
    // rest between pumps. Brain already wrote idlePant; Walk owns
    // WalkEye. Hunt recover / stalk keep idleHunt. Leap owns
    // LeapHuntEye. Look-up / Sleep / rest skip (sniff keeps
    // its own dirt drop). Sit chew owns SitChewEye. No idleEye:
    // Walk() would zero it. 0 = the old rest sit-pant stare.
    // Pupil stays visible under the squint.
    public float SitEye()
    {
        if (sitEyeAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleLook > 0.01f)
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idlePant < 0.01f)
            return 0f;
        float pulse = Mathf.Clamp01((idlePant - 0.55f) / 0.45f);
        return Mathf.Clamp(sitEyeAmp, 0f, 1.5f)
            * (0.55f + 0.45f * pulse);
    }

    // Picture only. Sit idlePant hung a rest brow after EndWalk —
    // same hunt-lock forehead WalkBrow already closed on the trot.
    // Hold a milder crease on the same 0.55 hang so the yellow box
    // does not flash rest between pumps. Brain already wrote
    // idlePant; Walk owns WalkBrow. Hunt / look-up / Sleep
    // (SitSleepBrow) / Leap / rest skip (sniff keeps its own drop,
    // sit yawn owns SitYawnBrow, sit chew owns SitChewBrow). No idleBrow:
    // Walk() would zero it. 0 = the old rest sit-pant forehead.
    public float SitBrow()
    {
        if (sitBrowAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleLook > 0.01f)
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idlePant < 0.01f)
            return 0f;
        float pulse = Mathf.Clamp01((idlePant - 0.55f) / 0.45f);
        return Mathf.Clamp(sitBrowAmp, 0f, 1.5f)
            * (0.55f + 0.45f * pulse);
    }

    // Picture only. Sit idlePant hung rest whisker stamps after
    // EndWalk — same hold muzzle WalkWhisker already closed on the
    // trot. Hold a milder fan on the same 0.55 hang so the snout
    // does not flash rest between pumps. Brain already wrote
    // idlePant; Walk owns WalkWhisker. Hunt / look-up / chew /
    // Sleep / Leap / rest skip (sniff keeps its own fan, sit yawn
    // owns SitYawnWhisker, sit chew owns SitChewWhisker). No
    // idleWhisker: Walk() would zero it.
    // 0 = the old rest sit-pant stamps.
    public float SitWhisker()
    {
        if (sitWhiskerAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleLook > 0.01f)
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idlePant < 0.01f)
            return 0f;
        float pulse = Mathf.Clamp01((idlePant - 0.55f) / 0.45f);
        return Mathf.Clamp(sitWhiskerAmp, 0f, 1.5f)
            * (0.55f + 0.45f * pulse);
    }

    // Picture only. Sit idlePant hung a rest half-fold after
    // EndWalk — same hold head WalkEarFlop already closed on the
    // trot. Hold a milder flop on the same 0.55 hang so the ears
    // do not flash rest between pumps. Brain already wrote
    // idlePant; Walk owns WalkEarFlop. Hunt / look-up / chew /
    // Sleep / Leap / rest skip (sniff keeps its own half-fold,
    // chew owns SitChewEar, scratch keeps its kick, yawn owns
    // SitYawnEar). No idleEar:
    // Walk() would zero it. 0 = the old rest sit-pant half-fold.
    public float SitEar()
    {
        if (sitEarAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleLook > 0.01f)
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        if (idlePant < 0.01f)
            return 0f;
        float pulse = Mathf.Clamp01((idlePant - 0.55f) / 0.45f);
        return Mathf.Clamp(sitEarAmp, 0f, 1.5f)
            * (0.55f + 0.45f * pulse);
    }

    // Picture only. Sit idlePant heaved Chest Visual after EndWalk —
    // same statue rump WalkRumpBob already closed on the trot. Hold
    // a milder heave on the same 0.55 hang so the pelvis does not
    // flash rest between pumps. In phase with sit chest (a pant,
    // not a see-saw). Brain already wrote idlePant; Walk owns
    // WalkRumpBob. Sit yawn owns SitYawnRump. Sit sniff owns
    // SitSniffRump. Sit scratch owns SitScratchRump. Sit look
    // owns SitLookRump. Hunt / chew / Sleep / Leap skip.
    // Quiet sit owns SitQuietRump. Sleep owns SitSleepRump. No
    // idleRump: Walk() would zero it.
    // 0 = the old statue sit-pant rump.
    public float SitRump()
    {
        if (sitRumpAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleLook > 0.01f)
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        if (idlePant < 0.01f)
            return 0f;
        float pulse = Mathf.Clamp01((idlePant - 0.55f) / 0.45f);
        return Mathf.Clamp(sitRumpAmp, 0f, 1.5f)
            * (0.55f + 0.45f * pulse);
    }

    // Sit idlePant pumped the jaw / chest / rump after EndWalk —
    // same statue neck walkHeadBob already closed on the trot.
    // Milder nose-down on the same 0.55 hang so the head does not
    // flash world-0 between pumps. Same SPD world target as sniff /
    // look / walkHeadBob, not a new neckP. Brain already wrote
    // idlePant. Hunt recover owns SitHuntHead. Sit chew owns
    // SitChewHead. Quiet sit owns SitQuietHead. Look-up / chew /
    // Sleep / Leap / rest skip (sniff / scratch keep their own sit
    // necks). Sleep stay hold. No idleHead: Walk() would zero it. 0 =
    // the old statue sit-pant neck.
    public float SitHead()
    {
        if (sitHeadAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleLook > 0.01f)
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idlePant < 0.01f)
            return 0f;
        float pulse = Mathf.Clamp01((idlePant - 0.55f) / 0.45f);
        return Mathf.Clamp(sitHeadAmp, 0f, 1.5f)
            * (0.55f + 0.45f * pulse);
    }

    // Picture only. Sit idleScratch already tucked the neck, flinched
    // the near ear, and lifted the near-hind Visual — the pupil stayed
    // on rest-Y, so the kick read as a forage stare through the ear.
    // Drop the same Walk/SitEye slot toward that paw. Pulse is the
    // kick above the 0.55 hang so the dot does not flash horizon
    // between rakes. Brain already wrote idleScratch; Walk owns
    // WalkEye, sit pant owns SitEye. Leap owns LeapHuntEye. Hunt /
    // look-up / chew / Sleep / sniff / pant / rest skip. Sleep /
    // yawn / blink still hide the dot. No idleEye: Walk() would
    // zero it. 0 = the old rest scratch stare.
    public float SitScratchEye()
    {
        if (sitScratchEyeAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleLook > 0.01f)
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        if (idleScratch < 0.01f)
            return 0f;
        float pulse = Mathf.Clamp01((idleScratch - 0.55f) / 0.45f);
        return Mathf.Clamp(sitScratchEyeAmp, 0f, 1.5f)
            * (0.55f + 0.45f * pulse);
    }

    // Picture only. Sit idleScratch already dropped the pupil toward
    // the kicking paw — the lid stayed the round rest, so the kick
    // still read as a wide forage stare through the ear. Slit the
    // same Walk/SitSquint pair on that kick. Pulse is the rake above
    // the 0.55 hang so the eye does not flash open between rakes.
    // Brain already wrote idleScratch; Walk owns WalkSquint, sit
    // pant owns SitSquint. Sit chew owns SitChewSquint. Hunt /
    // look-up / Sleep / sniff / pant / Leap / rest skip. Sleep /
    // yawn / blink still own the
    // full cover. Pupil stays visible under the squint. No idleBlink:
    // Walk() would zero it. 0 = the old round scratch stare.
    public float SitScratchSquint()
    {
        if (sitScratchLidAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleLook > 0.01f)
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        if (idleScratch < 0.01f)
            return 0f;
        float pulse = Mathf.Clamp01((idleScratch - 0.55f) / 0.45f);
        return Mathf.Clamp(sitScratchLidAmp, 0f, 1.5f)
            * (0.55f + 0.45f * pulse);
    }

    // Picture only. Sit idleScratch already dropped the pupil and
    // slit the lid — the brow stayed the rest stamp, so the kick
    // still read as a forage stare on the yellow box. Crease the
    // same Walk/SitBrow slot on that kick. Pulse is the rake above
    // the 0.55 hang so the forehead does not flash rest between
    // rakes. Brain already wrote idleScratch; Walk owns WalkBrow,
    // sit pant owns SitBrow, sit yawn owns SitYawnBrow. Sit chew
    // owns SitChewBrow. Hunt / look-up / chew / Sleep / sniff /
    // pant / Leap / rest skip. Hunt furrow stays on idleHunt. No
    // idleBrow: Walk() would zero it. 0 = the old rest scratch
    // forehead.
    public float SitScratchBrow()
    {
        if (sitScratchBrowAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleLook > 0.01f)
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        if (idleScratch < 0.01f)
            return 0f;
        float pulse = Mathf.Clamp01((idleScratch - 0.55f) / 0.45f);
        return Mathf.Clamp(sitScratchBrowAmp, 0f, 1.5f)
            * (0.55f + 0.45f * pulse);
    }

    // Picture only. Sit idleScratch already dropped the pupil, slit
    // the lid, and creased the brow — the whiskers stayed rest
    // stamps, so the kick still read as a forage stare on the yellow
    // box. Fan the same Walk/SitWhisker slot on that kick. Pulse is
    // the rake above the 0.55 hang so the snout does not flash rest
    // between rakes. Brain already wrote idleScratch; Walk owns
    // WalkWhisker, sit pant owns SitWhisker. Hunt / look-up / chew /
    // Sleep / sniff / pant / Leap / rest skip. Hunt pin stays on
    // idleHunt. Sniff keeps its own fan. Sit yawn owns
    // SitYawnWhisker. Sit chew owns SitChewWhisker. No idleWhisker:
    // Walk() would zero it.
    // 0 = the old rest scratch stamps.
    public float SitScratchWhisker()
    {
        if (sitScratchWhiskerAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleLook > 0.01f)
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        if (idleScratch < 0.01f)
            return 0f;
        float pulse = Mathf.Clamp01((idleScratch - 0.55f) / 0.45f);
        return Mathf.Clamp(sitScratchWhiskerAmp, 0f, 1.5f)
            * (0.55f + 0.45f * pulse);
    }

    // Picture only. Sit idleScratch already dropped the pupil, slit
    // the lid, creased the brow, and fanned the whiskers — the snout
    // stayed the rest stamp, so the kick still read as a forage
    // stare on the yellow box. Drop the same Walk/SitNose slot toward
    // that paw. Pulse is the rake above the 0.55 hang so the muzzle
    // does not flash rest between rakes. Brain already wrote
    // idleScratch; Walk owns WalkNose, sit pant owns SitNose. Leap
    // owns LeapHuntNose. Hunt / look-up / chew / Sleep / sniff /
    // pant / rest skip. Hunt drop stays on idleHunt. Sniff keeps
    // its own dirt drop. No idleNose: Walk() would zero it. Sit
    // yawn owns SitYawnNose. 0 = the old rest scratch muzzle.
    public float SitScratchNose()
    {
        if (sitScratchNoseAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleLook > 0.01f)
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        if (idleScratch < 0.01f)
            return 0f;
        float pulse = Mathf.Clamp01((idleScratch - 0.55f) / 0.45f);
        return Mathf.Clamp(sitScratchNoseAmp, 0f, 1.5f)
            * (0.55f + 0.45f * pulse);
    }

    // Picture only. Sit idleScratch already dropped the pupil, slit
    // the lid, creased the brow, fanned the whiskers, and dropped the
    // snout — the lip stayed the rest stamp, so the kick still read
    // as a forage stare on the yellow box. Tuck the same Head-Visual
    // mouth toward that paw (sniff dirt drop, not hunt flews and not
    // a pant gape). Pulse is the rake above the 0.55 hang so the
    // flews do not flash rest between rakes. Brain already wrote
    // idleScratch; Walk / sit pant open via WalkPant / idlePant.
    // Quiet sit owns SitQuietMouth. Sleep owns SitSleepMouth. Hunt /
    // look-up / chew / sniff / pant / Leap / rest skip. Hunt raise
    // stays on idleHunt.
    // Sniff keeps its own dirt tuck. No idleMouth: Walk() would
    // zero it. 0 = the old rest scratch flews.
    public float SitScratchMouth()
    {
        if (sitScratchMouthAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleLook > 0.01f)
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        if (idleScratch < 0.01f)
            return 0f;
        float pulse = Mathf.Clamp01((idleScratch - 0.55f) / 0.45f);
        return Mathf.Clamp(sitScratchMouthAmp, 0f, 1.5f)
            * (0.55f + 0.45f * pulse);
    }

    // Picture only. Sit idleLook already weaves the neck, hops facing,
    // and lifts pupil / snout / brow — the ears stayed rest fold 0.20
    // except a binary perk at idleLook > 0.45, so the glance still
    // read as a statue head on a nodding neck. Hitch the same Head-
    // Visual ears with the signed bob (look-up perks, look-down folds).
    // Brain already wrote idleLook; Walk owns WalkEarFlop, sit pant
    // owns SitEar, scratch owns the near-ear kick, sit yawn owns
    // SitYawnEar. Hunt pin stays on idleHunt. Sniff keeps its own
    // half-fold. Sit chew owns SitChewEar. Sleep owns SitSleepEar.
    // Leap / rest skip. No idleEar:
    // Walk() would zero it. 0 = the old rest fold / binary perk.
    public float SitLookEar()
    {
        if (sitLookEarAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        if (Mathf.Abs(idleLook) < 0.01f)
            return 0f;
        return Mathf.Clamp(sitLookEarAmp, 0f, 1.5f)
            * Mathf.Clamp(idleLook, -1f, 1f);
    }

    // Picture only. Sit idleLook already weaves the neck, hops facing,
    // lifts pupil / snout / brow, and hitches the ears — the whiskers
    // stayed rest stamps, so the glance still read as a statue muzzle
    // on a nodding head. Fan the same Walk/SitWhisker slot with the
    // signed bob (look-up spreads, look-down tucks). Brain already
    // wrote idleLook; Walk owns WalkWhisker, sit pant owns SitWhisker,
    // scratch owns SitScratchWhisker, sit yawn owns SitYawnWhisker,
    // sit chew owns SitChewWhisker. Hunt pin stays on idleHunt.
    // Sniff keeps its own fan. Sleep / Leap / rest skip. No
    // idleWhisker: Walk() would zero it.
    // 0 = the old rest stamps.
    public float SitLookWhisker()
    {
        if (sitLookWhiskerAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        if (Mathf.Abs(idleLook) < 0.01f)
            return 0f;
        return Mathf.Clamp(sitLookWhiskerAmp, 0f, 1.5f)
            * Mathf.Clamp(idleLook, -1f, 1f);
    }

    // Picture only. Sit idleLook already weaves the neck, hops facing,
    // lifts pupil / snout / brow, hitches the ears, and fans the
    // whiskers — the lid stayed the round rest, so look-down still
    // read as a wide forage stare on a nodding head. Slit the same
    // Walk/SitSquint slot on the signed bob (look-down slits, look-up
    // stays open). Brain already wrote idleLook; Walk owns WalkSquint,
    // sit pant owns SitSquint, scratch owns SitScratchSquint. Hunt
    // recover owns SitHuntSquint. Hunt stalk Walk owns WalkHuntSquint.
    // Hunt leap owns LeapHuntSquint.
    // Sit chew owns SitChewSquint.
    // Sniff keeps the open track eye.
    // Sleep / yawn / blink still own the full cover. Pupil stays
    // visible under the squint. Sleep / Leap / rest skip. No
    // idleBlink: Walk() would zero it. 0 = the old round look eye.
    public float SitLookSquint()
    {
        if (sitLookLidAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        if (idleLook >= -0.01f)
            return 0f;
        return Mathf.Clamp(sitLookLidAmp, 0f, 1.5f)
            * Mathf.Clamp(-idleLook, 0f, 1f);
    }

    // Picture only. Sit idleLook already weaves the neck, hops facing,
    // lifts pupil / snout / brow, hitches the ears, fans the whiskers,
    // and slits look-down — the lip stayed the rest stamp (tiny
    // look×0.005 / 4°), so the glance still read as a forage stare
    // on the yellow box. Hitch the same Head-Visual mouth with the
    // signed bob (look-up lifts with the nose, look-down tucks like
    // sniff). Brain already wrote idleLook; Walk / sit pant open via
    // WalkPant / idlePant. Scratch owns SitScratchMouth. Sit yawn
    // owns SitYawnMouth. Sit chew owns SitChewMouth. Quiet sit
    // owns SitQuietMouth. Sleep owns SitSleepMouth. Hunt raise
    // stays on idleHunt. Sniff keeps its own dirt tuck. Pant /
    // yawn / chew still open with DriveJaw. Leap / rest skip. No
    // idleMouth: Walk() would zero it. 0 = the old rest look flews.
    public float SitLookMouth()
    {
        if (sitLookMouthAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        if (Mathf.Abs(idleLook) < 0.01f)
            return 0f;
        return Mathf.Clamp(sitLookMouthAmp, 0f, 1.5f)
            * Mathf.Clamp(idleLook, -1f, 1f);
    }

    // Same first-hinge spring as sit idleWag. Sit idleLook already
    // weaves the neck, hops facing, and hitches the face — leftover
    // sit held the 8.5 Hz forage metronome, so the glance still read
    // as a nodding statue with a sit wag. Signed: look-up perks
    // (minus, same as hunt raise), look-down droops. Brain already
    // wrote idleLook; Walk owns WalkTail, sit pant owns SitPantTail.
    // Sit sniff owns SitSniffTail. Sit scratch owns SitScratchTail.
    // Sit yawn owns SitYawnTail. Sit chew owns SitChewTail. Distal
    // limp is SitLookDistalTail. Hunt raise stays on idleHunt.
    // Sleep / Leap / rest skip. Quiet sit between acts keeps
    // 8.5 Hz. No idleTail: Walk() would zero it. 0 = the old
    // look metronome.
    public float SitLookTail()
    {
        if (sitLookTailAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        if (Mathf.Abs(idleLook) < 0.01f)
            return 0f;
        return Mathf.Clamp(sitLookTailAmp, 0f, 1.5f)
            * Mathf.Clamp(idleLook, -1f, 1f);
    }

    // Picture only. Sit look already hitches the first hinge
    // (SitLookTail) and weaves neck / face / chest / rump —
    // leftover sit held Tail2–Tail5 on TreeSpring restAngle 0
    // (muscles only on the first hinge — by design) so the glance
    // still read as a stiff caudal rod on a nodding neck. Limp
    // the distal Visuals (accumulating 6°…12°) following
    // idleLook's signed bob (env already weaves ~2.2 Hz; no extra
    // clock, not Sleep's 1.6 hitch, not sniff's ~3 Hz bob, not
    // yawn's envelope, not pant's 6.5 Hz pump, not chew's 11 Hz
    // snap, and not a new distal muscle). PoseVisual plus droops;
    // IdleTailTarget perks look-up with a minus on SitLookTail, so
    // this env is negated (look-up perks, look-down droops).
    // Colliders stay. Brain already wrote idleLook; Walk owns
    // WalkTail. Sit sniff distal is SitSniffDistalTail. Sit yawn
    // distal is SitYawnDistalTail. Sit pant distal is
    // SitPantDistalTail. Sit chew distal is SitChewDistalTail.
    // Sit scratch distal is SitScratchDistalTail. Hunt recover
    // distal is SitHuntDistalTail. Hunt stalk distal is
    // WalkHuntDistalTail. Hunt leap owns LeapHuntTail. Hunt leap
    // distal is LeapHuntDistalTail. Sleep distal is
    // SitSleepDistalTail. Quiet sit distal is SitQuietDistalTail.
    // No idleTail: Walk() would zero it. 0 = the old rest-spring
    // look stick.
    public float SitLookDistalTail()
    {
        if (sitLookDistalTailAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        if (Mathf.Abs(idleLook) < 0.01f)
            return 0f;
        return -Mathf.Clamp(sitLookDistalTailAmp, 0f, 1.5f)
            * Mathf.Clamp(idleLook, -1f, 1f);
    }

    // Picture only. Sit idleLook already weaves the neck, hops facing,
    // hitches the face, and hitches the tail — leftover sit held
    // quiet-sit 0.0018 (BreathLift only reads idlePant / Sleep /
    // walk / idleYawn / idleSniff / idleScratch) so the glance still
    // read as a statue chest on a nodding neck. Hitch with the signed
    // bob (look-up lifts, look-down settles). Not a 6.5 Hz pant and
    // not the quiet sin. Brain already wrote idleLook; Walk owns
    // WalkTrunkBob. Sit pant owns the idlePant branch in BreathLift.
    // Sit yawn owns SitYawnBreath. Sit sniff owns SitSniffBreath.
    // Sit scratch owns SitScratchBreath. Hunt recover owns
    // SitHuntBreath. Sleep / Leap / rest skip. Quiet sit between
    // acts stays the 0.0018 sin. Sit look rump is SitLookRump. No
    // idleBreath: Walk() would zero it. 0 = the old quiet-sit
    // look chest.
    public float SitLookBreath()
    {
        if (sitLookBreathAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        if (Mathf.Abs(idleLook) < 0.01f)
            return 0f;
        return Mathf.Clamp(sitLookBreathAmp, 0f, 1.5f)
            * Mathf.Clamp(idleLook, -1f, 1f);
    }

    // Picture only. Sit idleLook already weaves the neck, hops facing,
    // hitches the face, hitches the tail, and heaves Chest Visual —
    // leftover sit held a statue rump (ApplyRump only reads
    // WalkRumpBob / SitRump / SitYawnRump / SitSniffRump /
    // SitScratchRump) so the glance still read as a chest-only bob
    // on a nodding neck. Hitch with the signed bob (look-up lifts,
    // look-down settles). In phase with SitLookBreath (a glance,
    // not a see-saw, not a 6.5 Hz pant). Brain already wrote
    // idleLook; Walk owns WalkRumpBob. Sit pant owns SitRump.
    // Sit yawn owns SitYawnRump. Sit sniff owns SitSniffRump.
    // Sit scratch owns SitScratchRump. Hunt recover owns
    // SitHuntRump. Sit chew owns SitChewRump. Quiet sit owns
    // SitQuietRump. Sleep owns SitSleepRump. Sleep / Leap / rest
    // skip. No idleRump: Walk() would zero it.
    // 0 = the old statue look rump.
    public float SitLookRump()
    {
        if (sitLookRumpAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        if (Mathf.Abs(idleLook) < 0.01f)
            return 0f;
        return Mathf.Clamp(sitLookRumpAmp, 0f, 1.5f)
            * Mathf.Clamp(idleLook, -1f, 1f);
    }

    // Picture only. Hunt recover sit already snarls, pins, drops
    // the nose, raises hackles, and plants the near-front paw —
    // leftover sit held quiet-sit 0.0018 (BreathLift only reads
    // idlePant / Sleep / walk / idleYawn / idleSniff / idleScratch
    // / idleLook) so the 0.70 s coil still read as a statue chest
    // on a live snarl. Held heave with idleHunt (still coil, not
    // a 6.5 Hz pant and not the quiet sin). Brain already wrote
    // idleHunt; Walk owns WalkTrunkBob (stalk stays still). Sit
    // pant owns the idlePant branch in BreathLift. Sit yawn owns
    // SitYawnBreath. Sit sniff owns SitSniffBreath. Sit chew owns
    // SitChewBreath. Sit scratch owns SitScratchBreath. Sit look
    // owns SitLookBreath. Alert stare is idleLook, not this.
    // Sleep / rest skip. Hunt leap owns LeapHuntBreath.
    // Quiet sit between acts stays the 0.0018 sin. Hunt recover
    // rump is SitHuntRump. Hunt recover neck is SitHuntHead.
    // No idleBreath: Walk() would zero it.
    // 0 = the old quiet-sit recover chest.
    public float SitHuntBreath()
    {
        if (sitHuntBreathAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idleLook > 0.01f || idleLook < -0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        if (idleHunt < 0.01f)
            return 0f;
        return Mathf.Clamp(sitHuntBreathAmp, 0f, 1.5f)
            * Mathf.Clamp01(idleHunt);
    }

    // Picture only. Hunt recover sit already snarls, pins, drops
    // the nose, raises hackles, plants the near-front paw, and
    // heaves Chest Visual — leftover sit held a statue rump
    // (ApplyRump only reads WalkRumpBob / SitRump / SitYawnRump /
    // SitSniffRump / SitScratchRump / SitLookRump) so the 0.70 s
    // coil still read as a chest-only hitch on a live snarl. Held
    // hitch with idleHunt (still coil, not a 6.5 Hz pant, not a
    // see-saw). In phase with SitHuntBreath. Brain already wrote
    // idleHunt; Walk owns WalkRumpBob (stalk stays still). Sit
    // pant owns SitRump. Sit yawn owns SitYawnRump. Sit sniff
    // owns SitSniffRump. Sit scratch owns SitScratchRump. Sit
    // look owns SitLookRump. Sit chew owns SitChewRump. Alert stare is idleLook, not this.
    // Quiet sit owns SitQuietRump. Sleep owns SitSleepRump. Hunt leap owns LeapHuntRump.
    // Sleep / Leap / rest skip. Hunt recover neck is SitHuntHead.
    // No idleRump: Walk() would zero it. 0 = the old statue recover rump.
    public float SitHuntRump()
    {
        if (sitHuntRumpAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idleLook > 0.01f || idleLook < -0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        if (idleHunt < 0.01f)
            return 0f;
        return Mathf.Clamp(sitHuntRumpAmp, 0f, 1.5f)
            * Mathf.Clamp01(idleHunt);
    }

    // Sit hunt recover already snarls, pins, drops the nose,
    // raises hackles, plants the near-front paw, and heaves
    // Chest / Pelvis Visual — leftover sit held world 0
    // (IdleNeckWorldTarget only reads sleep / sniff / scratch /
    // look / yawn / hop / SitHead; SitHead skips idleHunt) so
    // the 0.70 s coil still read as a statue head on a live
    // snarl. Same 12° nose-down as stalk WalkNeckWorldTarget
    // (held coil, not a 6.5 Hz pant and not SitHead's 3°
    // pump). Brain already wrote idleHunt; Walk owns
    // WalkNeckWorldTarget (stalk already drops). Sit pant owns
    // SitHead. Sit chew owns SitChewHead. Quiet sit owns
    // SitQuietHead. Sit look / sniff / scratch / yawn keep their
    // own sit necks. Alert stare is idleLook, not this.
    // Sleep stay hold. Sleep / Leap / rest skip. Hunt recover lid
    // is SitHuntSquint. Hunt stalk lid is WalkHuntSquint. Hunt
    // leap lid is LeapHuntSquint. No idleHead: Walk() would zero
    // it. 0 = the old statue recover neck.
    public float SitHuntHead()
    {
        if (sitHuntHeadAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idleLook > 0.01f || idleLook < -0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        if (idleHunt < 0.01f)
            return 0f;
        return Mathf.Clamp(sitHuntHeadAmp, 0f, 1.5f)
            * Mathf.Clamp01(idleHunt);
    }

    // Picture only. Hunt recover sit already snarls, pins, drops
    // the nose and pupil, raises hackles, plants the near-front
    // paw, heaves Chest / Pelvis Visual, and drops the SPD neck
    // — leftover sit held the round rest (SitSquint / SitLookSquint
    // / SitScratchSquint all skip idleHunt) so the 0.70 s coil
    // still read as a wide forage stare on a live snarl. Held
    // slit with idleHunt (still coil, not a 6.5 Hz pant and not
    // SitSquint's pump). Same Walk/SitSquint pair. Brain already
    // wrote idleHunt; stalk Walk owns WalkHuntSquint. Leap owns
    // LeapHuntSquint (LeapAttack zeros idleHunt). Sit pant
    // owns SitSquint. Sit look owns SitLookSquint (look-down
    // only). Sit scratch owns SitScratchSquint. Sit chew owns
    // SitChewSquint. Alert stare is
    // idleLook, not this. Sleep / yawn / blink still own the
    // full cover. Pupil stays visible under the squint. Sleep /
    // rest skip. No idleBlink: Walk() would zero it. 0 =
    // the old round recover eye.
    public float SitHuntSquint()
    {
        if (sitHuntLidAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idleLook > 0.01f || idleLook < -0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        if (idleHunt < 0.01f)
            return 0f;
        return Mathf.Clamp(sitHuntLidAmp, 0f, 1.5f)
            * Mathf.Clamp01(idleHunt);
    }

    // Picture only. Hunt recover sit already snarls, pins, drops
    // the nose, raises hackles, plants the near-front paw, heaves
    // chest / rump, drops the neck, and slits the lid — leftover
    // sit held silent (SitPuff / SitYawnPuff / SitChewPuff skip
    // idleHunt; fangs own the Jaw-Visual teeth slot) so the
    // 0.70 s coil still read as a silent crack on a live snarl.
    // Held puff with idleHunt (still coil, not a 6.5 Hz pant).
    // Needs the hunt crack (DriveJaw already opens 0.20) so a
    // closed mouth does not leak vapor. Same PantPuff child,
    // cranial of the fangs. Brain already wrote idleHunt; Walk
    // forage owns WalkPuff. Stalk Walk owns WalkHuntPuff. Leap
    // owns LeapHuntPuff (LeapAttack zeros idleHunt). Sit pant
    // owns SitPuff. Sit yawn owns SitYawnPuff. Sit chew owns
    // SitChewPuff. Alert stare is idleLook, not this. Bark puff
    // is Call. Tongue stays off (fangs). Sleep / Leap / rest
    // skip. No idlePuff: Walk() would zero it. 0 = the old silent
    // recover snarl.
    public float SitHuntPuff()
    {
        if (sitHuntPuffAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idleLook > 0.01f || idleLook < -0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        if (idleHunt < 0.01f)
            return 0f;
        return Mathf.Clamp(sitHuntPuffAmp, 0f, 1.5f)
            * Mathf.Clamp01(idleHunt);
    }

    // Picture only. Hunt recover sit already raises the first
    // hinge (IdleTailTarget idleHunt, minus 16°) and coils lid /
    // pupil / nose / puff / chest / rump / neck — leftover sit
    // held Tail2–Tail5 on TreeSpring restAngle 0 (muscles only
    // on the first hinge — by design) so the 0.70 s coil still
    // read as a stiff caudal rod on a live snarl. Perk the distal
    // Visuals (accumulating 6°…12°) following that same held
    // idleHunt raise (env already coils; no extra clock, not
    // Sleep's 1.6 hitch, not sniff's ~3 Hz bob, not yawn's
    // envelope, not pant's 6.5 Hz pump, not chew's 11 Hz snap,
    // not look's signed bob, not scratch's ~14 Hz rake, not
    // quiet sit's 8.5 Hz, not WalkTail's two-dip, not
    // LeapHuntTail's IsLeap 0.85, and not a new distal muscle).
    // PoseVisual plus droops; IdleTailTarget perks with a minus
    // on idleHunt, so this env is negated (held raise, same
    // idleHunt as the hinge). Colliders stay. Brain already
    // wrote idleHunt; stalk Walk owns WalkHuntDistalTail. Leap
    // owns LeapHuntDistalTail (LeapAttack zeros idleHunt).
    // Forage Walk distal is WalkDistalTail. Quiet sit distal is
    // SitQuietDistalTail. Sleep distal is SitSleepDistalTail.
    // No idleTail: Walk() would zero it. 0 = the old rest-spring
    // recover sit stick.
    public float SitHuntDistalTail()
    {
        if (sitHuntDistalTailAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idleLook > 0.01f || idleLook < -0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        if (idleHunt < 0.01f)
            return 0f;
        return -Mathf.Clamp(sitHuntDistalTailAmp, 0f, 1.5f)
            * Mathf.Clamp01(idleHunt);
    }

    // Picture only. Hunt stalk Walk already drops the pupil, nose,
    // and SPD neck, pins ears, raises the tail, and snarls —
    // leftover walk held the round rest (WalkSquint skips idleHunt;
    // SitHuntSquint skips Walk) so the chase still read as a wide
    // forage stare on a live snarl. Held slit with idleHunt (still
    // coil, not a 6.5 Hz pant and not WalkSquint's two-dip pump).
    // Same Walk/SitSquint pair. Brain already wrote idleHunt;
    // recover sit owns SitHuntSquint. Leap owns LeapHuntSquint
    // (LeapAttack zeros idleHunt). Forage Walk owns WalkSquint.
    // Sit pant owns SitSquint. Alert stare is idleLook, not this.
    // Sleep / yawn / blink still own the full cover. Pupil stays
    // visible under the squint. CLI -dogWalk leaves idleHunt 0.
    // Hop keeps the coil (not a forage mute). Sleep / rest skip.
    // No idleBlink: Walk() would zero it. 0 = the old round
    // stalk eye.
    public float WalkHuntSquint()
    {
        if (walkHuntLidAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (!walkWanted && !IsWalk())
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idleLook > 0.01f || idleLook < -0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        if (idleHunt < 0.01f)
            return 0f;
        return Mathf.Clamp(walkHuntLidAmp, 0f, 1.5f)
            * Mathf.Clamp01(idleHunt);
    }

    // Picture only. Hunt stalk Walk already drops the pupil, nose,
    // and SPD neck, pins ears, raises the tail, slits the lid, and
    // snarls — leftover walk held silent (WalkPuff skips idleHunt
    // because WalkPant skips idleHunt; SitHuntPuff skips Walk) so
    // the chase still read as a silent crack on a live snarl.
    // Held puff with idleHunt (still coil, not a 6.5 Hz pant and
    // not WalkPuff's two-dip pump). Needs the hunt crack (DriveJaw
    // already opens 0.20) so a closed mouth does not leak vapor.
    // Same PantPuff child, cranial of the fangs. Brain already
    // wrote idleHunt; recover sit owns SitHuntPuff. Leap owns
    // LeapHuntPuff (LeapAttack zeros idleHunt; this gate skips
    // IsLeap). Forage Walk owns WalkPuff. Sit pant owns SitPuff.
    // Sit yawn owns SitYawnPuff. Sit chew owns SitChewPuff. Alert
    // stare is idleLook, not this. Bark puff is Call. Tongue stays
    // off (fangs). CLI -dogWalk leaves idleHunt 0. Hop keeps the
    // coil (not a forage mute). Sleep / rest skip. No idlePuff:
    // Walk() would zero it. 0 = the old silent stalk snarl.
    public float WalkHuntPuff()
    {
        if (walkHuntPuffAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (!walkWanted && !IsWalk())
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idleLook > 0.01f || idleLook < -0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        if (idleHunt < 0.01f)
            return 0f;
        return Mathf.Clamp(walkHuntPuffAmp, 0f, 1.5f)
            * Mathf.Clamp01(idleHunt);
    }

    // Picture only. Hunt stalk Walk already raises the first
    // hinge (IdleTailTarget idleHunt, minus 16°) and coils lid /
    // pupil / nose / puff / neck — leftover walk held Tail2–Tail5
    // on TreeSpring restAngle 0 (muscles only on the first hinge
    // — by design) so the chase still read as a stiff caudal rod
    // on a live snarl. Perk the distal Visuals (accumulating
    // 6°…12°) following that same held idleHunt raise (env
    // already coils; no extra clock, not Sleep's 1.6 hitch, not
    // sniff's ~3 Hz bob, not yawn's envelope, not pant's 6.5 Hz
    // pump, not chew's 11 Hz snap, not look's signed bob, not
    // scratch's ~14 Hz rake, not quiet sit's 8.5 Hz, not
    // WalkTail's two-dip, not LeapHuntTail's IsLeap 0.85, and
    // not a new distal muscle). PoseVisual plus droops;
    // IdleTailTarget perks with a minus on idleHunt, so this env
    // is negated (held raise, same idleHunt as the hinge). Hop
    // keeps the coil (not a forage mute). Colliders stay. Brain
    // already wrote idleHunt; recover sit owns SitHuntDistalTail.
    // Leap owns LeapHuntDistalTail (LeapAttack zeros idleHunt).
    // Forage Walk distal is WalkDistalTail. CLI -dogWalk leaves
    // idleHunt 0. Sleep distal is SitSleepDistalTail. No
    // idleTail: Walk() would zero it. 0 = the old rest-spring
    // stalk Walk stick.
    public float WalkHuntDistalTail()
    {
        if (walkHuntDistalTailAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (!walkWanted && !IsWalk())
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idleLook > 0.01f || idleLook < -0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        if (idleHunt < 0.01f)
            return 0f;
        return -Mathf.Clamp(walkHuntDistalTailAmp, 0f, 1.5f)
            * Mathf.Clamp01(idleHunt);
    }

    // Picture only. Hunt leap already furrows the brow, pins
    // ears, raises hackles, and snarls — leftover leap held the
    // round rest (WalkHuntSquint / SitHuntSquint skip IsLeap;
    // LeapAttack zeros idleHunt) so the pounce still read as a
    // wide forage stare on a live snarl. Held slit on IsLeap
    // (still coil, same 0.85 as stalk huntStalk, not a 6.5 Hz
    // pant and not WalkSquint's two-dip pump). Same Walk/SitSquint
    // pair. Body already wrote IsLeap; recover sit owns
    // SitHuntSquint. Stalk Walk owns WalkHuntSquint. Forage Walk
    // owns WalkSquint. Sit pant owns SitSquint. Alert stare is
    // idleLook, not this. Sleep / yawn / blink still own the
    // full cover. Pupil drop is LeapHuntEye (same leftover:
    // idleHunt 0, WalkEye/SitEye skip IsLeap). Snout drop is
    // LeapHuntNose (same leftover: idleHunt 0, WalkNose/SitNose
    // skip IsLeap). CLI -dogLeap
    // still slits from IsLeap(). Sleep / rest skip. No
    // idleBlink: Walk() / LeapAttack would zero it. 0 = the
    // old round leap eye.
    public float LeapHuntSquint()
    {
        if (leapHuntLidAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (!IsLeap())
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idleLook > 0.01f || idleLook < -0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        return Mathf.Clamp(leapHuntLidAmp, 0f, 1.5f) * 0.85f;
    }

    // Picture only. Hunt leap already furrows the brow and slits
    // the lid — leftover leap held rest-Y (WalkEye / SitEye skip
    // IsLeap; LeapAttack zeros idleHunt) so the pounce still read
    // as a forage stare through a live snarl slit. Held drop on
    // IsLeap (still coil, same 0.85 as stalk huntStalk, not a
    // 6.5 Hz pant and not WalkEye's two-dip pump). Same
    // Walk/SitEye pair. Body already wrote IsLeap; recover sit /
    // stalk Walk keep idleHunt. Forage Walk owns WalkEye. Sit
    // pant owns SitEye. Alert stare is idleLook, not this. Sleep
    // / yawn / blink still hide the dot. Snout drop is LeapHuntNose
    // (same leftover: idleHunt 0, WalkNose/SitNose skip IsLeap). CLI
    // -dogLeap still drops from IsLeap(). Sleep / rest skip. No
    // idleEye: Walk() / LeapAttack would zero it. 0 = the old rest-Y
    // leap stare.
    public float LeapHuntEye()
    {
        if (leapHuntEyeAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (!IsLeap())
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idleLook > 0.01f || idleLook < -0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        return Mathf.Clamp(leapHuntEyeAmp, 0f, 1.5f) * 0.85f;
    }

    // Picture only. Hunt leap already drops the pupil, slits the
    // lid, furrows the brow, and raises the flews — leftover leap
    // held the rest muzzle (WalkNose / SitNose skip IsLeap;
    // LeapAttack zeros idleHunt) so the pounce still read as a
    // forage snout on a live snarl. Held drop on IsLeap (still
    // coil, same 0.85 as stalk huntStalk, not a 6.5 Hz pant and
    // not WalkNose's two-dip pump). Same Walk/SitNose pair. Body
    // already wrote IsLeap; recover sit / stalk Walk keep
    // idleHunt. Forage Walk owns WalkNose. Sit pant owns SitNose.
    // Alert stare is idleLook, not this. Sleep owns SitSleepNose
    // (sprite stays on; lids own the eye). CLI -dogLeap still
    // drops from IsLeap(). Sleep / rest skip. No idleNose:
    // Walk() / LeapAttack would zero it. 0 = the old rest leap
    // muzzle.
    public float LeapHuntNose()
    {
        if (leapHuntNoseAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (!IsLeap())
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idleLook > 0.01f || idleLook < -0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        return Mathf.Clamp(leapHuntNoseAmp, 0f, 1.5f) * 0.85f;
    }

    // Picture only. Hunt leap already drops the pupil, slits the
    // lid, furrows the brow, drops the snout, and snarls —
    // leftover leap held silent (WalkHuntPuff / SitHuntPuff skip
    // IsLeap; LeapAttack zeros idleHunt; fangs own the Jaw-Visual
    // teeth slot) so the pounce still read as a silent crack on a
    // live snarl. Held puff on IsLeap (still coil, same 0.85 as
    // stalk huntStalk, not a 6.5 Hz pant and not WalkPuff's
    // two-dip pump). Needs the leap crack (DriveJaw already opens
    // 0.28 coil / 0.40 air) so a closed mouth does not leak vapor.
    // Same PantPuff child, cranial of the fangs. Body already
    // wrote IsLeap; recover sit owns SitHuntPuff. Stalk Walk owns
    // WalkHuntPuff. Forage Walk owns WalkPuff. Sit pant owns
    // SitPuff. Sit yawn owns SitYawnPuff. Sit chew owns
    // SitChewPuff. Alert stare is idleLook, not this. Bark puff
    // is Call. Tongue stays off (fangs). CLI -dogLeap still puffs
    // from IsLeap(). Sleep / rest skip. Chest heave is
    // LeapHuntBreath. No idlePuff: Walk() /
    // LeapAttack would zero it. 0 = the old silent leap snarl.
    public float LeapHuntPuff()
    {
        if (leapHuntPuffAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (!IsLeap())
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idleLook > 0.01f || idleLook < -0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        return Mathf.Clamp(leapHuntPuffAmp, 0f, 1.5f) * 0.85f;
    }

    // Picture only. Hunt leap already slits the lid, drops the
    // pupil, drops the snout, and puffs — leftover leap held a
    // statue chest (BreathLift / SitHuntBreath skip IsLeap;
    // LeapAttack zeros idleHunt) so the pounce still read as a
    // quiet-sit 0.0018 wobble on a live snarl. Held hitch on
    // IsLeap (still coil, same 0.85 as stalk huntStalk, not a
    // 6.5 Hz pant and not the quiet sin). Same Chest Visual as
    // SitHuntBreath. Body already wrote IsLeap; recover sit owns
    // SitHuntBreath. Stalk Walk stays still (WalkTrunkBob on
    // idleWag only). Forage sit pant owns the idlePant branch in
    // BreathLift. Sit yawn owns SitYawnBreath. Sit sniff owns
    // SitSniffBreath. Sit chew owns SitChewBreath. Sit scratch
    // owns SitScratchBreath. Sit look owns SitLookBreath. Alert
    // stare is idleLook, not this. Sleep / rest skip. Quiet sit
    // between acts stays the 0.0018 sin. Leap rump is LeapHuntRump.
    // CLI -dogLeap still heaves from
    // IsLeap(). No idleBreath: Walk() / LeapAttack would zero it.
    // 0 = the old statue leap chest.
    public float LeapHuntBreath()
    {
        if (leapHuntBreathAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (!IsLeap())
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idleLook > 0.01f || idleLook < -0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        return Mathf.Clamp(leapHuntBreathAmp, 0f, 1.5f) * 0.85f;
    }

    // Picture only. Hunt leap already slits the lid, drops the
    // pupil, drops the snout, puffs, and heaves Chest Visual —
    // leftover leap held a statue rump (ApplyRump / SitHuntRump
    // skip IsLeap; LeapAttack zeros idleHunt) so the pounce still
    // read as a chest-only hitch on a live snarl. Held hitch on
    // IsLeap (still coil, same 0.85 as stalk huntStalk, not a
    // 6.5 Hz pant, not a see-saw). Same Pelvis Visual as
    // SitHuntRump. In phase with LeapHuntBreath. Body already
    // wrote IsLeap; recover sit owns SitHuntRump. Stalk Walk
    // stays still (WalkRumpBob on idleWag only). Forage sit
    // pant owns SitRump. Sit yawn owns SitYawnRump. Sit sniff
    // owns SitSniffRump. Sit chew owns SitChewRump. Sit scratch
    // owns SitScratchRump. Sit look owns SitLookRump. Alert
    // stare is idleLook, not this. Quiet sit owns SitQuietRump.
    // Sleep owns SitSleepRump. Sleep / rest skip. CLI -dogLeap still heaves
    // from IsLeap(). Leap tail is LeapHuntTail. No idleRump:
    // Walk() / LeapAttack would zero it. 0 = the old statue leap rump.
    public float LeapHuntRump()
    {
        if (leapHuntRumpAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (!IsLeap())
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idleLook > 0.01f || idleLook < -0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        return Mathf.Clamp(leapHuntRumpAmp, 0f, 1.5f) * 0.85f;
    }

    // Same first-hinge spring as sit idleHunt. Hunt leap already
    // slits the lid, drops the pupil, drops the snout, puffs, and
    // heaves Chest / Pelvis Visual — leftover leap held rest
    // (DriveLeap pins tailBaseAngle; hunt raise lives on idleHunt;
    // LeapAttack zeros idleHunt) so the pounce still read as a
    // rest tail on a live snarl. Held raise on IsLeap (still coil,
    // same 0.85 as stalk huntStalk, minus, not an 8.5 Hz wag and
    // not a 6.5 Hz pant). Same 16° as IdleTailTarget hunt. Body
    // already wrote IsLeap; recover sit / stalk Walk keep idleHunt.
    // Forage Walk owns WalkTail. Sit look owns SitLookTail. Sit
    // sniff owns SitSniffTail. Sit scratch owns SitScratchTail.
    // Sit yawn owns SitYawnTail. Sit pant owns SitPantTail. Sit
    // chew owns SitChewTail. Alert stare is idleLook, not this.
    // Sleep owns SitSleepTail. Distal Tail2–Tail5 picture limp is
    // LeapHuntDistalTail. Rest skip. CLI -dogLeap still raises
    // from IsLeap(). No idleTail: Walk() / LeapAttack would zero
    // it. 0 = the old rest leap tail.
    public float LeapHuntTail()
    {
        if (leapHuntTailAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (!IsLeap())
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idleLook > 0.01f || idleLook < -0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        return Mathf.Clamp(leapHuntTailAmp, 0f, 1.5f) * 0.85f;
    }

    // Picture only. Hunt leap already raises the first hinge
    // (LeapHuntTail) and coils lid / pupil / nose / puff / chest /
    // rump — leftover leap held Tail2–Tail5 on TreeSpring restAngle
    // 0 (muscles only on the first hinge — by design) so the
    // pounce still read as a stiff caudal rod on a live snarl.
    // Perk the distal Visuals (accumulating 6°…12°) following that
    // same held 0.85 raise (env already coils; no extra clock, not
    // Sleep's 1.6 hitch, not sniff's ~3 Hz bob, not yawn's
    // envelope, not pant's 6.5 Hz pump, not chew's 11 Hz snap, not
    // look's signed bob, not scratch's ~14 Hz rake, not quiet
    // sit's 8.5 Hz, not WalkTail's two-dip, and not a new distal
    // muscle). PoseVisual plus droops; DriveLeap perks with a
    // minus on LeapHuntTail, so this env is negated (held raise,
    // same 0.85 as stalk huntStalk). Colliders stay. Body already
    // wrote IsLeap; recover sit owns SitHuntDistalTail. Stalk Walk
    // owns WalkHuntDistalTail. Forage Walk distal is WalkDistalTail.
    // Quiet sit distal is SitQuietDistalTail. Sleep distal is
    // SitSleepDistalTail. CLI -dogLeap still perks from IsLeap().
    // No idleTail: Walk() / LeapAttack would zero it. 0 = the old
    // rest-spring leap stick.
    public float LeapHuntDistalTail()
    {
        if (leapHuntDistalTailAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (!IsLeap())
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idleLook > 0.01f || idleLook < -0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        return -Mathf.Clamp(leapHuntDistalTailAmp, 0f, 1.5f) * 0.85f;
    }

    // Same first-hinge spring as sit idleHunt, opposite sign.
    // Night Sleep already drops the chin, shuts the lids, and
    // folds the ears — leftover sit held rest (TickSleep zeros
    // idleWag; IdleTailTarget falls to tailBaseAngle) so the hold
    // still read as an alert tail on a sleeping face. Held droop
    // (plus, limp, same 12° as sit sniff / yawn, not an 8.5 Hz wag
    // and not a 6.5 Hz pant). Distal Tail2–Tail5 stay rest spring
    // in physics (muscles only on the first hinge); picture limp
    // is SitSleepDistalTail. Brain already wrote idleSleep; Walk
    // owns WalkTail. Sit look
    // owns SitLookTail. Sit sniff owns SitSniffTail. Sit scratch
    // owns SitScratchTail. Sit yawn owns SitYawnTail. Sit pant
    // owns SitPantTail. Sit chew owns SitChewTail. Hunt raise
    // stays on idleHunt. Hunt leap owns LeapHuntTail. Sleep rump
    // is SitSleepRump. Quiet sit distal is SitQuietDistalTail. No
    // idleTail: Walk() would zero it. 0 = the old rest sleep tail.
    public float SitSleepTail()
    {
        if (sitSleepTailAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleLook > 0.01f || idleLook < -0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        if (idleSleep < 0.01f)
            return 0f;
        return Mathf.Clamp(sitSleepTailAmp, 0f, 1.5f)
            * Mathf.Clamp01(idleSleep);
    }

    // Picture only. Night Sleep already drops the chin, shuts the
    // lids, folds the ears, droops the tail, and heaves Chest
    // Visual (BreathLift 0.0024 slow sin) — leftover sit held a
    // statue rump (ApplyRump only reads WalkRumpBob / SitRump /
    // SitYawnRump / SitSniffRump / SitScratchRump / SitLookRump /
    // SitHuntRump / SitChewRump / LeapHuntRump) so the hold still
    // read as a chest-only hitch on a sleeping face. Hitch with
    // the sleep breath (same 1.6 clock, in phase, not a see-saw,
    // not a 6.5 Hz pant and not the quiet sin). Brain already wrote
    // idleSleep; Walk owns WalkRumpBob. Sit pant owns SitRump.
    // Sit yawn owns SitYawnRump. Sit sniff owns SitSniffRump.
    // Sit scratch owns SitScratchRump. Sit look owns SitLookRump.
    // Sit chew owns SitChewRump. Hunt recover owns SitHuntRump.
    // Hunt leap owns LeapHuntRump. Sleep nose is SitSleepNose.
    // Sleep ear is SitSleepEar. Quiet sit owns SitQuietRump.
    // Distal Tail2–Tail5 stay rest spring. No idleRump: Walk()
    // would zero it. 0 = the old statue sleep rump.
    public float SitSleepRump()
    {
        if (sitSleepRumpAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleLook > 0.01f || idleLook < -0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        if (idleSleep < 0.01f)
            return 0f;
        return Mathf.Clamp(sitSleepRumpAmp, 0f, 1.5f)
            * Mathf.Clamp01(idleSleep);
    }

    // Picture only. Night Sleep already drops the chin, shuts the
    // lids, folds the ears, droops the tail, heaves Chest Visual,
    // hitches the rump, and droops mouth / whisker / brow —
    // leftover sit held the rest muzzle (ApplyNose never read
    // idleSleep; TickSleep zeros idleWag; lids own the pupil, not
    // the snout) so the hold still read as a forage snout on a
    // sleeping face. Held drop (milder than sniff dirt, same 0.005
    // as sit pant, not a 6.5 Hz pant and not WalkNose's two-dip
    // pump). Sprite stays on (lids own the eye). Brain already
    // wrote idleSleep; Walk owns WalkNose. Sit pant owns SitNose.
    // Sit yawn owns SitYawnNose. Sit chew owns SitChewNose. Hunt
    // drop stays on idleHunt. Hunt leap owns LeapHuntNose. Sleep
    // ear is SitSleepEar. Quiet sit keeps rest. Distal Tail2–Tail5
    // stay rest spring. No idleNose: Walk() would zero it. 0 = the
    // old rest sleep muzzle.
    public float SitSleepNose()
    {
        if (sitSleepNoseAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleLook > 0.01f || idleLook < -0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        if (idleSleep < 0.01f)
            return 0f;
        return Mathf.Clamp(sitSleepNoseAmp, 0f, 1.5f)
            * Mathf.Clamp01(idleSleep);
    }

    // Picture only. Night Sleep already drops the chin, shuts the
    // lids, folds the ears to 0.88, droops the tail, heaves Chest
    // Visual, hitches the rump, drops the snout, and droops mouth /
    // whisker / brow — leftover sit held statue fold 0.88
    // (EarFoldWanted returns 0.88 on idleSleep; TickSleep zeros
    // idleWag) so the hold still read as statue ears on a sleeping
    // face. Hitch with the sleep breath (same 1.6 clock as
    // BreathLift / SitSleepRump, in phase, fold 0.88 ± 0.06, not a
    // 6.5 Hz pant and not WalkEarFlop). Brain already wrote
    // idleSleep; Walk owns WalkEarFlop. Sit pant owns SitEar.
    // Sit look owns SitLookEar. Sit yawn owns SitYawnEar. Sit chew
    // owns SitChewEar. Scratch keeps its kick. Hunt pin stays on
    // idleHunt. Hunt leap keeps the leap pin. Quiet sit owns
    // SitQuietEar. Distal Tail2–Tail5 stay rest spring. No idleEar:
    // Walk() would zero it. 0 = the old statue sleep fold 0.88.
    public float SitSleepEar()
    {
        if (sitSleepEarAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleLook > 0.01f || idleLook < -0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        if (idleSleep < 0.01f)
            return 0f;
        return Mathf.Clamp(sitSleepEarAmp, 0f, 1.5f)
            * Mathf.Clamp01(idleSleep);
    }

    // Picture only. Night Sleep already drops the chin, shuts the
    // lids, folds the ears, droops the tail, heaves Chest / Pelvis
    // Visual, drops the snout, hitches the ears with the breath,
    // and droops mouth / whisker / brow — leftover sit held the
    // statue sleep droop (ApplyBrows sleep*4 / y −0.003 / ang +10;
    // TickSleep zeros idleWag) so the hold still read as statue
    // brows on a sleeping face. Hitch with the sleep breath (same
    // 1.6 clock as BreathLift / SitSleepRump / SitSleepEar, in
    // phase, droop ± a small lift, not a 6.5 Hz pant and not
    // WalkBrow). Brain already wrote idleSleep; Walk owns WalkBrow.
    // Sit pant owns SitBrow. Sit yawn owns SitYawnBrow. Sit chew
    // owns SitChewBrow. Scratch owns SitScratchBrow. Hunt furrow
    // stays on idleHunt. Sleep mouth owns SitSleepMouth. Sleep
    // whisker owns SitSleepWhisker. Quiet sit owns SitQuietBrow.
    // Distal Tail2–Tail5 stay rest spring. No idleBrow: Walk()
    // would zero it. 0 = the old statue sleep droop.
    public float SitSleepBrow()
    {
        if (sitSleepBrowAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleLook > 0.01f || idleLook < -0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        if (idleSleep < 0.01f)
            return 0f;
        return Mathf.Clamp(sitSleepBrowAmp, 0f, 1.5f)
            * Mathf.Clamp01(idleSleep);
    }

    // Picture only. Night Sleep already drops the chin, shuts the
    // lids, folds the ears, droops the tail, heaves Chest / Pelvis
    // Visual, drops the snout, hitches the ears with the breath,
    // and hitches the brows — leftover sit held the statue sleep
    // droop (ApplyMouth sleep*4 / y −0.004 / ang −12; TickSleep
    // zeros idleWag) so the hold still read as statue flews on a
    // sleeping face. Hitch with the sleep breath (same 1.6 clock
    // as BreathLift / SitSleepRump / SitSleepEar / SitSleepBrow,
    // in phase, droop ± a small lift, not a 6.5 Hz pant, not a
    // hunt raise, and not SitLookMouth / SitYawnMouth). Brain
    // already wrote idleSleep; Walk / sit pant open via WalkPant /
    // idlePant. Scratch owns SitScratchMouth. Look owns
    // SitLookMouth. Sit yawn owns SitYawnMouth. Sit chew owns
    // SitChewMouth. Hunt raise stays on idleHunt. Sniff keeps
    // its own dirt tuck. Pant / yawn / chew still open with
    // DriveJaw. Quiet sit owns SitQuietMouth. Sleep whisker owns
    // SitSleepWhisker. Distal Tail2–Tail5 stay rest spring. No
    // idleMouth: Walk() would zero it. 0 = the old statue sleep
    // droop.
    public float SitSleepMouth()
    {
        if (sitSleepMouthAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleLook > 0.01f || idleLook < -0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        if (idleSleep < 0.01f)
            return 0f;
        return Mathf.Clamp(sitSleepMouthAmp, 0f, 1.5f)
            * Mathf.Clamp01(idleSleep);
    }

    // Picture only. Night Sleep already drops the chin, shuts the
    // lids, folds the ears, droops the tail, heaves Chest / Pelvis
    // Visual, drops the snout, and hitches the ears / brows /
    // flews with the breath — leftover sit held the statue sleep
    // droop (ApplyWhiskers sleep*4 / sleepAng 10°+30°; TickSleep
    // zeros idleWag) so the hold still read as statue stamps on a
    // sleeping face. Hitch with the sleep breath (same 1.6 clock
    // as BreathLift / SitSleepRump / SitSleepEar / SitSleepBrow /
    // SitSleepMouth, in phase, droop ± a small lift, not a 6.5 Hz
    // pant, not an 11 Hz sniff twitch, and not WalkWhisker). Brain
    // already wrote idleSleep; Walk owns WalkWhisker. Sit pant
    // owns SitWhisker. Scratch owns SitScratchWhisker. Look owns
    // SitLookWhisker. Sit yawn owns SitYawnWhisker. Sit chew owns
    // SitChewWhisker. Hunt pin stays on idleHunt. Sniff keeps its
    // own fan. Quiet sit owns SitQuietWhisker. Sleep distal owns
    // SitSleepDistalTail (picture; springs stay). No idleWhisker:
    // Walk() would zero it. 0 = the old statue sleep droop.
    public float SitSleepWhisker()
    {
        if (sitSleepWhiskerAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleLook > 0.01f || idleLook < -0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        if (idleSleep < 0.01f)
            return 0f;
        return Mathf.Clamp(sitSleepWhiskerAmp, 0f, 1.5f)
            * Mathf.Clamp01(idleSleep);
    }

    // Picture only. Night Sleep already droops the first hinge
    // (SitSleepTail) and hitches chin / lids / ears / brows /
    // flews / whiskers / chest / rump / snout with the 1.6 breath
    // — leftover sit held Tail2–Tail5 on TreeSpring restAngle 0
    // (muscles only on the first hinge — by design) so the hold
    // still read as a stiff caudal rod on a sleeping face. Limp
    // the distal Visuals (plus, accumulating 6°…12°, hitch ±2°
    // with the sleep breath, same 1.6 clock as BreathLift /
    // SitSleepRump / SitSleepEar / SitSleepBrow / SitSleepMouth /
    // SitSleepWhisker, in phase, not a 6.5 Hz pant and not a new
    // distal muscle). Colliders stay. Brain already wrote
    // idleSleep; Walk owns WalkTail. Sit sniff distal is
    // SitSniffDistalTail. Sit yawn distal is SitYawnDistalTail.
    // Sit pant distal is SitPantDistalTail. Sit chew distal is
    // SitChewDistalTail. Sit look distal is SitLookDistalTail.
    // Sit scratch distal is SitScratchDistalTail. Hunt raise stays on
    // idleHunt. Hunt leap owns LeapHuntTail. Hunt leap distal is
    // LeapHuntDistalTail. Sleep first hinge is SitSleepTail. Quiet
    // sit distal is SitQuietDistalTail. No idleTail: Walk() would
    // zero it. 0 = the old rest-spring sleep stick.
    public float SitSleepDistalTail()
    {
        if (sitSleepDistalTailAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleLook > 0.01f || idleLook < -0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        if (idleSleep < 0.01f)
            return 0f;
        return Mathf.Clamp(sitSleepDistalTailAmp, 0f, 1.5f)
            * Mathf.Clamp01(idleSleep);
    }

    // Picture only. Quiet Forage sit already heaves Chest Visual
    // (BreathLift 0.0018 sin at 2.5) and keeps the 8.5 Hz wag —
    // leftover sit held a statue rump (ApplyRump only reads
    // WalkRumpBob / SitRump / SitYawnRump / SitSniffRump /
    // SitScratchRump / SitLookRump / SitHuntRump / SitChewRump /
    // LeapHuntRump / SitSleepRump) so the hold still read as a
    // chest-only hitch on a sitting face. Hitch with the quiet
    // chest (same 2.5 clock as BreathLift, in phase, not a see-saw,
    // not a 6.5 Hz pant and not SitSleepRump's 1.6). No extra
    // idle float: between acts Drive already wrote idleWag and
    // cleared the others. Walk owns WalkRumpBob. Sit pant owns
    // SitRump. Sit yawn owns SitYawnRump. Sit sniff owns
    // SitSniffRump. Sit scratch owns SitScratchRump. Sit look
    // owns SitLookRump. Sit chew owns SitChewRump. Hunt recover
    // owns SitHuntRump. Hunt leap owns LeapHuntRump. Sleep owns
    // SitSleepRump. Distal Tail2–Tail5 limp is SitQuietDistalTail.
    // No idleRump: Walk() would zero it. 0 = the old statue quiet
    // sit rump.
    public float SitQuietRump()
    {
        if (sitQuietRumpAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleLook > 0.01f || idleLook < -0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        return Mathf.Clamp(sitQuietRumpAmp, 0f, 1.5f);
    }

    // Quiet Forage sit already heaves Chest Visual (BreathLift
    // 0.0018 sin at 2.5) and Pelvis Visual (SitQuietRump) —
    // leftover sit held world 0 (IdleNeckWorldTarget only reads
    // sleep / sniff / scratch / look / yawn / hop / SitHead /
    // SitHuntHead / SitChewHead; SitHead skips the quiet gap) so
    // the hold still read as a statue head on a hitching trunk.
    // Hitch with the quiet chest (same 2.5 clock as BreathLift /
    // SitQuietRump, in phase via sitBreathPhase, not a 6.5 Hz pant
    // and not SitHead's 3° pump). Milder 1.2° nose-down. No extra
    // idle float: between acts Drive already wrote idleWag and
    // cleared the others. Walk owns WalkNeckWorldTarget. Sit pant
    // owns SitHead. Hunt recover owns SitHuntHead. Sit chew owns
    // SitChewHead. Sit look / sniff / scratch / yawn keep their own
    // sit necks. Sleep stay hold. Quiet sit ears own SitQuietEar.
    // Quiet sit brows own SitQuietBrow. Quiet sit mouth owns
    // SitQuietMouth. Quiet sit whiskers own SitQuietWhisker.
    // Distal Tail2–Tail5 limp is SitQuietDistalTail. No
    // idleHead: Walk() would zero it. 0 = the old statue quiet sit
    // neck.
    public float SitQuietHead()
    {
        if (sitQuietHeadAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleLook > 0.01f || idleLook < -0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        return Mathf.Clamp(sitQuietHeadAmp, 0f, 1.5f);
    }

    // Picture only. Quiet Forage sit already heaves Chest Visual
    // (BreathLift 0.0018 sin at 2.5), Pelvis Visual (SitQuietRump),
    // and the SPD neck (SitQuietHead) — leftover sit held rest fold
    // 0.20 (EarFoldWanted default; TickIdle writes idleWag and
    // cleared the others) so the hold still read as statue ears on
    // a hitching trunk and neck. Hitch with the quiet chest (same
    // 2.5 clock as BreathLift / SitQuietRump / SitQuietHead, in
    // phase via sitBreathPhase, fold 0.20 ± 0.05, not a 6.5 Hz
    // pant and not WalkEarFlop). No extra idle float: between acts
    // Drive already wrote idleWag and cleared the others. Walk owns
    // WalkEarFlop. Sit pant owns SitEar. Sit look owns SitLookEar.
    // Sit yawn owns SitYawnEar. Sit chew owns SitChewEar. Scratch
    // keeps its kick. Hunt pin stays on idleHunt. Hunt leap keeps
    // the leap pin. Sleep owns SitSleepEar. Quiet sit brows own
    // SitQuietBrow. Quiet sit mouth owns SitQuietMouth. Quiet sit
    // whiskers own SitQuietWhisker. Distal Tail2–Tail5 limp is
    // SitQuietDistalTail. No idleEar: Walk() would zero
    // it. 0 = the old statue rest fold 0.20.
    public float SitQuietEar()
    {
        if (sitQuietEarAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleLook > 0.01f || idleLook < -0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        return Mathf.Clamp(sitQuietEarAmp, 0f, 1.5f);
    }

    // Picture only. Quiet Forage sit already heaves Chest Visual
    // (BreathLift 0.0018 sin at 2.5), Pelvis Visual (SitQuietRump),
    // the SPD neck (SitQuietHead), and the ears (SitQuietEar) —
    // leftover sit held the rest stamp (ApplyBrows rest pose;
    // TickIdle writes idleWag and cleared the others) so the hold
    // still read as a rest forehead on a hitching trunk, neck, and
    // ears. Hitch with the quiet chest (same 2.5 clock as BreathLift
    // / SitQuietRump / SitQuietHead / SitQuietEar, in phase via
    // sitBreathPhase, droop ± a small lift, not a 6.5 Hz pant and
    // not WalkBrow). No extra idle float: between acts Drive already
    // wrote idleWag and cleared the others. Walk owns WalkBrow. Sit
    // pant owns SitBrow. Sit yawn owns SitYawnBrow. Sit chew owns
    // SitChewBrow. Scratch owns SitScratchBrow. Hunt furrow stays on
    // idleHunt. Sleep owns SitSleepBrow. Quiet sit mouth owns
    // SitQuietMouth. Quiet sit whiskers own SitQuietWhisker. Distal
    // Tail2–Tail5 limp is SitQuietDistalTail. No idleBrow: Walk() would zero
    // it. 0 = the old rest quiet sit forehead.
    public float SitQuietBrow()
    {
        if (sitQuietBrowAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleLook > 0.01f || idleLook < -0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        return Mathf.Clamp(sitQuietBrowAmp, 0f, 1.5f);
    }

    // Picture only. Quiet Forage sit already heaves Chest Visual
    // (BreathLift 0.0018 sin at 2.5), Pelvis Visual (SitQuietRump),
    // the SPD neck (SitQuietHead), the ears (SitQuietEar), and the
    // brows (SitQuietBrow) — leftover sit held the rest flews
    // (ApplyMouth rest pose; TickIdle writes idleWag and cleared
    // the others) so the hold still read as rest flews on a
    // hitching trunk, neck, ears, and forehead. Hitch with the
    // quiet chest (same 2.5 clock as BreathLift / SitQuietRump /
    // SitQuietHead / SitQuietEar / SitQuietBrow, in phase via
    // sitBreathPhase, droop ± a small lift, not a 6.5 Hz pant,
    // not a hunt raise, and not SitLookMouth / SitYawnMouth). No
    // extra idle float: between acts Drive already wrote idleWag
    // and cleared the others. Walk / sit pant open via WalkPant /
    // idlePant. Scratch owns SitScratchMouth. Look owns
    // SitLookMouth. Sit yawn owns SitYawnMouth. Sit chew owns
    // SitChewMouth. Hunt raise stays on idleHunt. Sniff keeps
    // its own dirt tuck. Pant / yawn / chew still open with
    // DriveJaw. Sleep owns SitSleepMouth. Quiet sit whiskers
    // own SitQuietWhisker. Distal Tail2–Tail5 limp is SitQuietDistalTail. No
    // idleMouth: Walk() would zero it. 0 = the old rest quiet sit
    // flews.
    public float SitQuietMouth()
    {
        if (sitQuietMouthAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleLook > 0.01f || idleLook < -0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        return Mathf.Clamp(sitQuietMouthAmp, 0f, 1.5f);
    }

    // Picture only. Quiet Forage sit already heaves Chest Visual
    // (BreathLift 0.0018 sin at 2.5), Pelvis Visual (SitQuietRump),
    // the SPD neck (SitQuietHead), the ears (SitQuietEar), the
    // brows (SitQuietBrow), and the flews (SitQuietMouth) —
    // leftover sit held the rest stamps (ApplyWhiskers rest pose;
    // TickIdle writes idleWag and cleared the others) so the hold
    // still read as rest stamps on a hitching trunk, neck, ears,
    // forehead, and flews. Hitch with the quiet chest (same 2.5
    // clock as BreathLift / SitQuietRump / SitQuietHead /
    // SitQuietEar / SitQuietBrow / SitQuietMouth, in phase via
    // sitBreathPhase, droop ± a small lift, not a 6.5 Hz pant,
    // not an 11 Hz sniff twitch, and not WalkWhisker). No extra
    // idle float: between acts Drive already wrote idleWag and
    // cleared the others. Walk owns WalkWhisker. Sit pant owns
    // SitWhisker. Scratch owns SitScratchWhisker. Look owns
    // SitLookWhisker. Sit yawn owns SitYawnWhisker. Sit chew owns
    // SitChewWhisker. Hunt pin stays on idleHunt. Sniff keeps its
    // own fan. Sleep owns SitSleepWhisker. Distal Tail2–Tail5 limp
    // is SitQuietDistalTail. No idleWhisker: Walk() would zero it. 0 = the
    // old rest quiet sit stamps.
    public float SitQuietWhisker()
    {
        if (sitQuietWhiskerAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleLook > 0.01f || idleLook < -0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        return Mathf.Clamp(sitQuietWhiskerAmp, 0f, 1.5f);
    }

    // Picture only. Quiet Forage sit already wags the first hinge
    // (IdleTailTarget 8.5 Hz) and hitches chest / rump / neck /
    // face with the 2.5 breath — leftover sit held Tail2–Tail5 on
    // TreeSpring restAngle 0 (muscles only on the first hinge —
    // by design) so the hold still read as a stiff caudal rod on
    // a sitting wag. Limp the distal Visuals (accumulating 6°…12°)
    // following that same 8.5 Hz metronome (env already pulses;
    // Time.fixedTime so the pictures stay in phase with the hinge,
    // not Sleep's 1.6 hitch, not sniff's ~3 Hz bob, not yawn's
    // envelope, not pant's 6.5 Hz pump, not chew's 11 Hz snap, not
    // look's signed bob, not scratch's ~14 Hz rake, and not a
    // new distal muscle). Plus = clockwise, tip down, same sign
    // as IdleTailTarget's +16° sit wag. Colliders stay. Drive
    // already wrote idleWag and cleared the acts. Walk owns
    // WalkTail. Walk distal is WalkDistalTail. Sit sniff distal is SitSniffDistalTail. Sit yawn
    // distal is SitYawnDistalTail. Sit pant distal is
    // SitPantDistalTail. Sit chew distal is SitChewDistalTail.
    // Sit look distal is SitLookDistalTail. Sit scratch distal is
    // SitScratchDistalTail. Hunt recover distal is SitHuntDistalTail.
    // Hunt stalk distal is WalkHuntDistalTail. Hunt leap owns
    // LeapHuntTail. Hunt leap distal is LeapHuntDistalTail.
    // Sleep distal is SitSleepDistalTail. No idleTail: Walk() would
    // zero it. 0 = the old rest-spring quiet sit stick.
    public float SitQuietDistalTail()
    {
        if (sitQuietDistalTailAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleLook > 0.01f || idleLook < -0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idleWag < 0.01f)
            return 0f;
        return Mathf.Clamp(sitQuietDistalTailAmp, 0f, 1.5f)
            * idleWag * Mathf.Sin(Time.fixedTime * 8.5f);
    }

    // Same first-hinge spring as sit idleWag. Sit idleSniff already
    // drops the neck, shuffles the near-front paw, and weaves the
    // muzzle — leftover sit held the 8.5 Hz forage metronome, so
    // the track still read as a dirt statue with a sit wag. Droop
    // (plus, tip down) hitching with the sniff bob (idleSniff already
    // pulses ~3 Hz). Brain already wrote idleSniff; Walk owns
    // WalkTail, sit look owns SitLookTail, sit pant owns
    // SitPantTail. Sit scratch owns SitScratchTail. Sit yawn
    // owns SitYawnTail. Sit chew owns SitChewTail (wag-off meal;
    // this skip is the 0.08 chew dip, not rest). Hunt raise stays
    // on idleHunt. Hunt leap owns LeapHuntTail. Sleep owns
    // SitSleepTail. Distal Tail2–Tail5 limp is SitSniffDistalTail.
    // Sit yawn distal is SitYawnDistalTail. Rest skip. Quiet sit
    // between acts keeps 8.5 Hz. No idleTail: Walk() would zero it.
    // 0 = the old sniff metronome.
    public float SitSniffTail()
    {
        if (sitSniffTailAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idleLook > 0.01f || idleLook < -0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        if (idleSniff < 0.01f)
            return 0f;
        return Mathf.Clamp(sitSniffTailAmp, 0f, 1.5f)
            * Mathf.Clamp01(idleSniff);
    }

    // Picture only. Sit sniff already droops the first hinge
    // (SitSniffTail) and hitches neck / paw / muzzle / chest /
    // rump with the dirt bob — leftover sit held Tail2–Tail5 on
    // TreeSpring restAngle 0 (muscles only on the first hinge —
    // by design) so the track still read as a stiff caudal rod
    // on a dirt-tracking face. Limp the distal Visuals (plus,
    // accumulating 6°…12°) following idleSniff's ~3 Hz bob
    // (env already pulses; no extra clock, not a 6.5 Hz pant,
    // not Sleep's 1.6 hitch, and not a new distal muscle).
    // Colliders stay. Brain already wrote idleSniff; Walk owns
    // WalkTail. Sit yawn distal is SitYawnDistalTail. Sit pant
    // distal is SitPantDistalTail. Sit chew distal is
    // SitChewDistalTail. Sit look distal is SitLookDistalTail.
    // Sit scratch distal is SitScratchDistalTail. Hunt raise stays
    // on idleHunt. Hunt leap owns LeapHuntTail. Sleep distal is
    // SitSleepDistalTail. Quiet sit distal is SitQuietDistalTail.
    // No idleTail: Walk() would zero it.
    // 0 = the old rest-spring sniff stick.
    public float SitSniffDistalTail()
    {
        if (sitSniffDistalTailAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idleLook > 0.01f || idleLook < -0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        if (idleSniff < 0.01f)
            return 0f;
        return Mathf.Clamp(sitSniffDistalTailAmp, 0f, 1.5f)
            * Mathf.Clamp01(idleSniff);
    }

    // Picture only. Sit idleSniff already drops the neck, shuffles
    // the near-front paw, weaves the muzzle, and droops the tail —
    // leftover sit held quiet-sit 0.0018 (BreathLift only reads
    // idlePant / Sleep / walk / idleYawn) so the track still read
    // as a statue chest. Hitch with the sniff bob (idleSniff
    // already pulses ~3 Hz). Not a 6.5 Hz pant and not the quiet
    // sin. Brain already wrote idleSniff; Walk owns WalkTrunkBob.
    // Sit pant owns the idlePant branch in BreathLift. Sit yawn
    // owns SitYawnBreath. Sit scratch owns SitScratchBreath.
    // Sit look owns SitLookBreath. Hunt recover owns SitHuntBreath.
    // Sleep / Leap /
    // rest skip. Chew reuses idleSniff for the paw and zeros
    // idleWag; SitChewBreath owns that meal chest. Sit sniff rump
    // is SitSniffRump. No idleBreath: Walk() would zero it. 0 =
    // the old quiet-sit sniff chest.
    public float SitSniffBreath()
    {
        if (sitSniffBreathAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleWag < 0.01f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idleLook > 0.01f || idleLook < -0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        if (idleSniff < 0.01f)
            return 0f;
        return Mathf.Clamp(sitSniffBreathAmp, 0f, 1.5f)
            * Mathf.Clamp01(idleSniff);
    }

    // Picture only. After a kill the brain already snaps the jaw,
    // pulses the morsel, and reuses idleSniff on the near-front
    // paw — leftover sit held quiet-sit 0.0018 (BreathLift only
    // reads pant / Sleep / walk / yawn / sniff / scratch / look /
    // hunt; SitSniffBreath skips idleChew / wag-off) so the meal
    // still read as a statue chest on a chewing jaw. Hitch with
    // the snap (idleChew already Abs-sins ~11 Hz). Hang 0.55 so a
    // closed mouth is still a meal, not the quiet sin and not a
    // 6.5 Hz pant. Session is wag-off + sniff (WriteIdlePose chew
    // zeros idleWag; forage sniff keeps wag 1). Brain already
    // wrote idleChew; Walk owns WalkTrunkBob. Sit sniff owns
    // SitSniffBreath. Sit pant owns the idlePant branch. Sit yawn
    // owns SitYawnBreath. Sit scratch owns SitScratchBreath. Sit
    // look owns SitLookBreath. Hunt recover owns SitHuntBreath.
    // Sleep / Leap / rest skip. Sit chew rump is SitChewRump.
    // Sit chew tail is SitChewTail. Sit chew lid is SitChewSquint.
    // Sit chew ear is SitChewEar. Sit chew brow is SitChewBrow.
    // Sit chew whisker is SitChewWhisker. Sit chew neck is
    // SitChewHead. Sit chew snout is SitChewNose. Sit chew
    // lip is SitChewMouth. Sit chew pupil is SitChewEye.
    // Sit chew puff is SitChewPuff.
    // No idleBreath: Walk() would
    // zero it. 0 = the old quiet-sit chew chest.
    public float SitChewBreath()
    {
        if (sitChewBreathAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idleLook > 0.01f || idleLook < -0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        if (idleWag >= 0.01f)
            return 0f;
        if (idleSniff < 0.01f)
            return 0f;
        return Mathf.Clamp(sitChewBreathAmp, 0f, 1.5f)
            * (0.55f + 0.45f * Mathf.Clamp01(idleChew));
    }

    // Picture only. After a kill SitChewBreath already hitches
    // Chest Visual — leftover sit held a statue rump (ApplyRump
    // only reads WalkRumpBob / SitRump / SitYawnRump /
    // SitSniffRump / SitScratchRump / SitLookRump / SitHuntRump;
    // SitSniffRump skips wag-off sniff) so the meal still read as
    // a chest-only hitch on a chewing jaw. Hitch with the snap
    // (idleChew already Abs-sins ~11 Hz). Hang 0.55 so a closed
    // mouth is still a meal, not a statue and not a 6.5 Hz pant.
    // In phase with SitChewBreath (a meal, not a see-saw). Session
    // is wag-off + sniff. Brain already wrote idleChew; Walk owns
    // WalkRumpBob. Sit sniff owns SitSniffRump. Sit pant owns
    // SitRump. Sit yawn owns SitYawnRump. Sit scratch owns
    // SitScratchRump. Sit look owns SitLookRump. Hunt recover
    // owns SitHuntRump. Quiet sit / Sleep stay chest-only. Sleep /
    // Leap / rest skip. Sit chew tail is SitChewTail. Sit chew
    // lid is SitChewSquint. Sit chew ear is SitChewEar. Sit chew
    // brow is SitChewBrow. Sit chew whisker is SitChewWhisker.
    // Sit chew neck is SitChewHead. No
    // idleRump:
    // Walk() would zero it. 0 = the old statue chew rump.
    public float SitChewRump()
    {
        if (sitChewRumpAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idleLook > 0.01f || idleLook < -0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        if (idleWag >= 0.01f)
            return 0f;
        if (idleSniff < 0.01f)
            return 0f;
        return Mathf.Clamp(sitChewRumpAmp, 0f, 1.5f)
            * (0.55f + 0.45f * Mathf.Clamp01(idleChew));
    }

    // Same first-hinge spring as sit idleWag. After a kill
    // SitChewBreath / SitChewRump already hitch Chest / Pelvis —
    // leftover sit held rest (IdleTailTarget falls to tailBaseAngle:
    // chew zeros idleWag, SitSniffTail skips idleChew, no SitChewTail)
    // so the meal still read as a planted tail on a chewing jaw.
    // Droop (plus, tip down) hitching with the snap (idleChew already
    // Abs-sins ~11 Hz). Hang 0.55 so a closed mouth is still a meal,
    // not rest and not a 6.5 Hz pant. 10° so the snap does not rattle
    // like a full 16° wag. In phase with SitChewBreath / SitChewRump
    // (a meal, not a see-saw). Session is wag-off + sniff. Brain
    // already wrote idleChew; Walk owns WalkTail. Sit sniff owns
    // SitSniffTail. Sit pant owns SitPantTail. Sit yawn owns
    // SitYawnTail. Sit scratch owns SitScratchTail. Sit look owns
    // SitLookTail. Hunt raise stays on idleHunt. Hunt leap owns
    // LeapHuntTail. Sleep / rest skip. Sit chew lid is SitChewSquint. Sit chew ear is
    // SitChewEar. Sit chew brow is SitChewBrow. Sit chew whisker
    // is SitChewWhisker. Sit chew neck is SitChewHead. Distal limp
    // is SitChewDistalTail. Quiet sit distal is SitQuietDistalTail.
    // No idleTail:
    // Walk() would zero it. 0 = the old rest chew tail.
    public float SitChewTail()
    {
        if (sitChewTailAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idleLook > 0.01f || idleLook < -0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        if (idleWag >= 0.01f)
            return 0f;
        if (idleSniff < 0.01f)
            return 0f;
        return Mathf.Clamp(sitChewTailAmp, 0f, 1.5f)
            * (0.55f + 0.45f * Mathf.Clamp01(idleChew));
    }

    // Picture only. Sit chew already droops the first hinge
    // (SitChewTail) and snaps jaw / chest / rump / lid / ear /
    // brow / whisker / neck / snout / lip / pupil / puff —
    // leftover sit held Tail2–Tail5 on TreeSpring restAngle 0
    // (muscles only on the first hinge — by design) so the meal
    // still read as a stiff caudal rod on a chewing jaw. Limp
    // the distal Visuals (plus, accumulating 6°…12°) following
    // idleChew's hang 0.55 + 11 Hz snap (env already hitchs; no
    // extra clock, not Sleep's 1.6 hitch, not sniff's ~3 Hz bob,
    // not yawn's envelope, not pant's 6.5 Hz pump, and not a new
    // distal muscle). Colliders stay. Brain already wrote
    // idleChew; Walk owns WalkTail. Sit sniff distal is
    // SitSniffDistalTail. Sit yawn distal is SitYawnDistalTail.
    // Sit pant distal is SitPantDistalTail. Sit look distal is
    // SitLookDistalTail. Sit scratch distal is
    // SitScratchDistalTail. Hunt raise stays on idleHunt. Hunt leap owns
    // LeapHuntTail. Sleep distal is SitSleepDistalTail. Quiet
    // sit distal is SitQuietDistalTail. No
    // idleTail: Walk() would zero it. 0 = the old rest-spring
    // chew stick.
    public float SitChewDistalTail()
    {
        if (sitChewDistalTailAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idleLook > 0.01f || idleLook < -0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        if (idleWag >= 0.01f)
            return 0f;
        if (idleSniff < 0.01f)
            return 0f;
        return Mathf.Clamp(sitChewDistalTailAmp, 0f, 1.5f)
            * (0.55f + 0.45f * Mathf.Clamp01(idleChew));
    }

    // Picture only. After a kill SitChewBreath / SitChewRump /
    // SitChewTail already hitch Chest / Pelvis / first-hinge —
    // leftover sit held the open sniff-track eye (SitSquint /
    // SitLookSquint / SitScratchSquint all skip idleChew; sniff
    // keeps the open track eye) so the meal still read as a
    // wide forage stare on a chewing jaw. Hitch a thinner slit
    // with the snap (idleChew already Abs-sins ~11 Hz). Hang
    // 0.55 so a closed mouth is still a meal, not the open
    // track and not a 6.5 Hz pant. Same Walk/SitSquint pair.
    // In phase with SitChewBreath / SitChewRump / SitChewTail
    // (a meal, not a see-saw). Session is wag-off + sniff.
    // Brain already wrote idleChew; Walk owns WalkSquint. Sit
    // pant owns SitSquint. Sit look owns SitLookSquint
    // (look-down only). Sit scratch owns SitScratchSquint.
    // Hunt recover owns SitHuntSquint. Hunt stalk Walk owns
    // WalkHuntSquint. Hunt leap owns LeapHuntSquint. Sniff
    // keeps the open track eye (forage wag stays on). Alert
    // stare is idleLook, not this. Sleep / yawn / blink still
    // own the full cover. Pupil stays visible under the
    // squint. Sleep / Leap / rest skip. Sit chew ear is
    // SitChewEar. Sit chew brow is SitChewBrow. Sit chew whisker
    // is SitChewWhisker. No idleBlink: Walk()
    // would zero it. 0 = the old open sniff-track chew eye.
    public float SitChewSquint()
    {
        if (sitChewLidAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idleLook > 0.01f || idleLook < -0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        if (idleWag >= 0.01f)
            return 0f;
        if (idleSniff < 0.01f)
            return 0f;
        return Mathf.Clamp(sitChewLidAmp, 0f, 1.5f)
            * (0.55f + 0.45f * Mathf.Clamp01(idleChew));
    }

    // Picture only. After a kill SitChewBreath / SitChewRump /
    // SitChewTail / SitChewSquint already hitch Chest / Pelvis /
    // first-hinge / lids — leftover sit held the statue sniff
    // half-fold 0.42 (EarFoldWanted returns 0.42 on idleSniff
    // before idleChew; SitEar / SitLookEar / SitYawnEar skip
    // idleChew) so the meal still read as a sniff statue on a
    // chewing jaw. Hitch a fold with the snap (idleChew already
    // Abs-sins ~11 Hz). Hang 0.55 so a closed mouth is still a
    // meal, not the statue half-fold and not a 6.5 Hz pant.
    // Same Head-Visual pair. In phase with SitChewBreath /
    // SitChewRump / SitChewTail / SitChewSquint (a meal, not a
    // see-saw). Session is wag-off + sniff. Brain already wrote
    // idleChew; Walk owns WalkEarFlop. Sit pant owns SitEar.
    // Sit look owns SitLookEar. Sit yawn owns SitYawnEar.
    // Scratch keeps its kick. Hunt pin stays on idleHunt.
    // Sniff keeps the forage half-fold (forage wag stays on).
    // Sleep owns SitSleepEar. Leap / rest skip. Sit chew brow is SitChewBrow.
    // Sit chew whisker is SitChewWhisker.
    // No idleEar: Walk() would
    // zero it. 0 = the old statue sniff-fold chew ear.
    public float SitChewEar()
    {
        if (sitChewEarAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idleLook > 0.01f || idleLook < -0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        if (idleWag >= 0.01f)
            return 0f;
        if (idleSniff < 0.01f)
            return 0f;
        return Mathf.Clamp(sitChewEarAmp, 0f, 1.5f)
            * (0.55f + 0.45f * Mathf.Clamp01(idleChew));
    }

    // Picture only. After a kill SitChewBreath / SitChewRump /
    // SitChewTail / SitChewSquint / SitChewEar already hitch Chest /
    // Pelvis / first-hinge / lids / ears — leftover sit held the
    // statue sniff furrow (ApplyBrows reads idleSniff; WalkBrow /
    // SitBrow / SitScratchBrow / SitYawnBrow skip idleChew) so the
    // meal still read as a dirt-track forehead on a chewing jaw.
    // Hitch a crease with the snap (idleChew already Abs-sins
    // ~11 Hz). Hang 0.55 so a closed mouth is still a meal, not
    // the statue sniff furrow and not a 6.5 Hz pant. Same
    // Walk/SitBrow slot (crease, not a yawn raise). In phase with
    // SitChewBreath / SitChewRump / SitChewTail / SitChewSquint /
    // SitChewEar (a meal, not a see-saw). Session is wag-off +
    // sniff. Brain already wrote idleChew; Walk owns WalkBrow.
    // Sit pant owns SitBrow. Sit yawn owns SitYawnBrow. Scratch
    // owns SitScratchBrow. Hunt furrow stays on idleHunt. Sniff
    // keeps the forage drop (forage wag stays on). Sleep owns
    // SitSleepBrow. Leap / rest skip. Sit chew whisker is
    // SitChewWhisker. No idleBrow:
    // Walk() would zero it. 0 = the old
    // statue sniff-furrow chew brow.
    public float SitChewBrow()
    {
        if (sitChewBrowAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idleLook > 0.01f || idleLook < -0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        if (idleWag >= 0.01f)
            return 0f;
        if (idleSniff < 0.01f)
            return 0f;
        return Mathf.Clamp(sitChewBrowAmp, 0f, 1.5f)
            * (0.55f + 0.45f * Mathf.Clamp01(idleChew));
    }

    // Picture only. After a kill SitChewBreath / SitChewRump /
    // SitChewTail / SitChewSquint / SitChewEar / SitChewBrow already
    // hitch Chest / Pelvis / first-hinge / lids / ears / brow —
    // leftover sit held the sniff fan + 11 Hz twitch (ApplyWhiskers
    // reads idleSniff; WalkWhisker / SitWhisker / SitScratchWhisker
    // / SitLookWhisker / SitYawnWhisker skip idleChew) so the meal
    // still read as a dirt-track muzzle on a chewing jaw. Hitch a
    // milder Walk/SitWhisker fan with the snap (idleChew already
    // Abs-sins ~11 Hz). Hang 0.55 so a closed mouth is still a meal,
    // not the sniff fan+11 Hz and not a 6.5 Hz pant. Same
    // Walk/SitWhisker slot (fan, not a yawn stretch). In phase with
    // SitChewBreath / SitChewRump / SitChewTail / SitChewSquint /
    // SitChewEar / SitChewBrow (a meal, not a see-saw). Session is
    // wag-off + sniff. Brain already wrote idleChew; Walk owns
    // WalkWhisker. Sit pant owns SitWhisker. Sit yawn owns
    // SitYawnWhisker. Scratch owns SitScratchWhisker. Sit look
    // owns SitLookWhisker. Hunt pin stays on idleHunt. Sniff keeps
    // the forage fan (forage wag stays on). Sleep / Leap / rest
    // skip. Sit chew neck is SitChewHead. Sit chew snout is
    // SitChewNose. Sit chew lip is SitChewMouth. Sit chew
    // pupil is SitChewEye. No idleWhisker:
    // Walk() would zero it. 0 = the old sniff-fan chew stamps.
    public float SitChewWhisker()
    {
        if (sitChewWhiskerAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idleLook > 0.01f || idleLook < -0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        if (idleWag >= 0.01f)
            return 0f;
        if (idleSniff < 0.01f)
            return 0f;
        return Mathf.Clamp(sitChewWhiskerAmp, 0f, 1.5f)
            * (0.55f + 0.45f * Mathf.Clamp01(idleChew));
    }

    // Sit chew already snaps the jaw and hitches chest / rump /
    // tail / lid / ear / brow / whisker — leftover sit held sniff
    // 16° (IdleNeckWorldTarget reads idleSniff; SitHead skips
    // idleChew) so the meal still read as a dirt-track neck on a
    // chewing jaw. Hitch a milder SPD drop with the snap
    // (idleChew already Abs-sins ~11 Hz). Hang 0.55 so a closed
    // mouth is still a meal, not the held 16° sniff and not
    // SitHead's 3° pump. Same 16° as sit sniff, not a new neckP.
    // In phase with SitChewBreath / SitChewRump / SitChewTail /
    // SitChewSquint / SitChewEar / SitChewBrow / SitChewWhisker
    // (a meal, not a see-saw). Session is wag-off + sniff. Brain
    // already wrote idleChew; Walk owns WalkNeckWorldTarget. Sit
    // pant owns SitHead. Hunt recover owns SitHuntHead. Sit look
    // / sniff / scratch / yawn keep their own sit necks. Sleep /
    // Leap / rest skip. Sit chew snout is SitChewNose. Sit chew
    // lip is SitChewMouth. Sit chew pupil is SitChewEye. No
    // idleHead: Walk() would zero it.
    // 0 = the old held-sniff chew neck.
    public float SitChewHead()
    {
        if (sitChewHeadAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idleLook > 0.01f || idleLook < -0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        if (idleWag >= 0.01f)
            return 0f;
        if (idleSniff < 0.01f)
            return 0f;
        return Mathf.Clamp(sitChewHeadAmp, 0f, 1.5f)
            * (0.55f + 0.45f * Mathf.Clamp01(idleChew));
    }

    // Picture only. Sit chew already snaps the jaw and hitches
    // chest / rump / tail / lid / ear / brow / whisker / neck —
    // leftover sit held the sniff drop (ApplyNose reads idleSniff;
    // WalkNose / SitNose skip idleChew) so the meal still read as
    // a dirt-track muzzle on a chewing jaw. Hitch a milder drop
    // with the snap (idleChew already Abs-sins ~11 Hz). Hang 0.55
    // so a closed mouth is still a meal, not the held sniff drop
    // and not SitNose's 0.005 pant. Same 0.007 as sit sniff, not
    // a new idleNose. In phase with SitChewBreath / SitChewRump /
    // SitChewTail / SitChewSquint / SitChewEar / SitChewBrow /
    // SitChewWhisker / SitChewHead (a meal, not a see-saw).
    // Session is wag-off + sniff. Brain already wrote idleChew;
    // Walk owns WalkNose. Sit pant owns SitNose. Sit yawn owns
    // SitYawnNose. Scratch owns SitScratchNose. Hunt drop stays
    // on idleHunt. Sleep / Leap / rest skip. Sit chew lip is
    // SitChewMouth. Sit chew pupil is SitChewEye. No idleNose:
    // Walk() would zero it. 0 = the old held-sniff chew muzzle.
    public float SitChewNose()
    {
        if (sitChewNoseAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idleLook > 0.01f || idleLook < -0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        if (idleWag >= 0.01f)
            return 0f;
        if (idleSniff < 0.01f)
            return 0f;
        return Mathf.Clamp(sitChewNoseAmp, 0f, 1.5f)
            * (0.55f + 0.45f * Mathf.Clamp01(idleChew));
    }

    // Picture only. Sit chew already snaps the jaw and hitches
    // chest / rump / tail / lid / ear / brow / whisker / neck /
    // snout — leftover sit held the sniff tuck (ApplyMouth reads
    // idleSniff; Scratch / Look / Yawn skip idleChew) so the meal
    // still read as dirt-track flews on a chewing jaw. Hitch a
    // milder tuck with the snap (idleChew already Abs-sins ~11 Hz).
    // Hang 0.55 so a closed mouth is still a meal, not the held
    // sniff tuck and not SitYawnMouth's stretch lift. Same 0.006 /
    // 6° as sit sniff, not a hunt raise and not DriveJaw's open.
    // In phase with SitChewBreath / SitChewRump / SitChewTail /
    // SitChewSquint / SitChewEar / SitChewBrow / SitChewWhisker /
    // SitChewHead / SitChewNose (a meal, not a see-saw). Session
    // is wag-off + sniff. Brain already wrote idleChew; Walk /
    // sit pant open via WalkPant / idlePant. Scratch owns
    // SitScratchMouth. Look owns SitLookMouth. Sit yawn owns
    // SitYawnMouth. Quiet sit owns SitQuietMouth. Sleep owns
    // SitSleepMouth. Hunt raise stays on idleHunt. Sniff keeps
    // its own dirt tuck. Leap / rest skip. Sit chew
    // pupil is SitChewEye. No idleMouth: Walk() would zero it.
    // 0 = the old held-sniff chew flews.
    public float SitChewMouth()
    {
        if (sitChewMouthAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idleLook > 0.01f || idleLook < -0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        if (idleWag >= 0.01f)
            return 0f;
        if (idleSniff < 0.01f)
            return 0f;
        return Mathf.Clamp(sitChewMouthAmp, 0f, 1.5f)
            * (0.55f + 0.45f * Mathf.Clamp01(idleChew));
    }

    // Picture only. Sit chew already snaps the jaw and hitches
    // chest / rump / tail / lid / ear / brow / whisker / neck /
    // snout / lip — leftover sit held the sniff drop (ApplyEyes
    // reads idleSniff; WalkEye / SitEye skip idleChew) so the
    // meal still read as a dirt-track stare on a chewing jaw.
    // Hitch a milder drop with the snap (idleChew already
    // Abs-sins ~11 Hz). Hang 0.55 so a closed mouth is still a
    // meal, not the held sniff drop and not SitEye's 0.004 pant.
    // Same 0.008 as sit sniff, not a new idleEye. In phase with
    // SitChewBreath / SitChewRump / SitChewTail / SitChewSquint /
    // SitChewEar / SitChewBrow / SitChewWhisker / SitChewHead /
    // SitChewNose / SitChewMouth (a meal, not a see-saw).
    // Session is wag-off + sniff. Brain already wrote idleChew;
    // Walk owns WalkEye. Sit pant owns SitEye. Scratch owns
    // SitScratchEye. Leap owns LeapHuntEye. Hunt drop stays on
    // idleHunt. Look-up stays idleLook. Sleep / yawn / blink
    // still hide the dot. Pupil stays visible under SitChewSquint.
    // Sit chew puff is SitChewPuff. No idleEye: Walk() would
    // zero it. 0 = the old held-sniff chew stare.
    public float SitChewEye()
    {
        if (sitChewEyeAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idleLook > 0.01f || idleLook < -0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        if (idleWag >= 0.01f)
            return 0f;
        if (idleSniff < 0.01f)
            return 0f;
        return Mathf.Clamp(sitChewEyeAmp, 0f, 1.5f)
            * (0.55f + 0.45f * Mathf.Clamp01(idleChew));
    }

    // Picture only. Sit chew already snaps the jaw and hitches
    // chest / rump / tail / lid / ear / brow / whisker / neck /
    // snout / lip / pupil, and pulses the morsel — leftover sit
    // held silent (SitYawnPuff / SitPuff skip idleChew; morsel
    // owns the Jaw-Visual teeth slot) so the meal still read as a
    // silent lump on a chewing jaw. Pulse a cooler sliver cranial
    // of the morsel with the snap (idleChew already Abs-sins
    // ~11 Hz). No hang: vapor on a closed mouth would leak (SitPuff
    // only pumps above 0.55). Same PantPuff child, not a new
    // idlePuff. In phase with SitChewBreath / SitChewRump /
    // SitChewTail / SitChewSquint / SitChewEar / SitChewBrow /
    // SitChewWhisker / SitChewHead / SitChewNose / SitChewMouth /
    // SitChewEye (a meal, not a see-saw). Session is wag-off +
    // sniff. Brain already wrote idleChew; Walk owns WalkPuff.
    // Sit pant owns SitPuff. Sit yawn owns SitYawnPuff. Hunt
    // recover owns SitHuntPuff. Stalk Walk owns WalkHuntPuff.
    // Leap owns LeapHuntPuff. Bark puff is Call. Tongue stays off
    // (morsel). Sleep / Leap / rest skip. No idlePuff: Walk()
    // would zero it. 0 = the old silent chew lump.
    public float SitChewPuff()
    {
        if (sitChewPuffAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idleLook > 0.01f || idleLook < -0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        if (idleWag >= 0.01f)
            return 0f;
        if (idleSniff < 0.01f)
            return 0f;
        if (idleChew < 0.01f)
            return 0f;
        return Mathf.Clamp(sitChewPuffAmp, 0f, 1.5f)
            * Mathf.Clamp01(idleChew);
    }

    // Picture only. Sit idleSniff already heaves Chest Visual —
    // leftover sit held a statue rump (ApplyRump only reads
    // WalkRumpBob / SitRump / SitYawnRump) so the track still
    // read as a chest-only bob. Hitch with the sniff bob
    // (idleSniff already pulses ~3 Hz). In phase with
    // SitSniffBreath (a dirt track, not a see-saw, not a 6.5 Hz
    // pant). Brain already wrote idleSniff; Walk owns
    // WalkRumpBob. Sit pant owns SitRump. Sit yawn owns
    // SitYawnRump. Sit scratch owns SitScratchRump. Sit look
    // owns SitLookRump. Hunt recover owns SitHuntRump.
    // Quiet sit / Sleep stay chest-only. Sleep / Leap / rest
    // skip. Chew reuses idleSniff for the paw; SitChewRump owns
    // that meal rump; SitChewTail owns that meal tail (wag-off
    // is the session).
    // No idleRump: Walk() would zero it.
    // 0 = the old statue sniff rump.
    public float SitSniffRump()
    {
        if (sitSniffRumpAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleWag < 0.01f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idleLook > 0.01f || idleLook < -0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        if (idleSniff < 0.01f)
            return 0f;
        return Mathf.Clamp(sitSniffRumpAmp, 0f, 1.5f)
            * Mathf.Clamp01(idleSniff);
    }

    // Same first-hinge spring as sit idleWag. Sit idleScratch already
    // tucks the neck, kicks the near-hind, and weaves the face —
    // leftover sit held the 8.5 Hz forage metronome, so the kick
    // still read as an itch with a sit wag. Droop (plus, tip down)
    // hitching with the rake (idleScratch already pulses ~14 Hz
    // above the 0.55 hang). Brain already wrote idleScratch; Walk
    // owns WalkTail, sit look owns SitLookTail, sit sniff owns
    // SitSniffTail, sit yawn owns SitYawnTail, sit pant owns
    // SitPantTail. Hunt raise stays on idleHunt. Sit chew owns
    // SitChewTail. Sleep / Leap / rest skip. Quiet sit between
    // acts keeps 8.5 Hz. Distal limp is SitScratchDistalTail.
    // No idleTail: Walk() would zero it. 0 =
    // the old scratch metronome.
    public float SitScratchTail()
    {
        if (sitScratchTailAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idleLook > 0.01f || idleLook < -0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        if (idleScratch < 0.01f)
            return 0f;
        float pulse = Mathf.Clamp01((idleScratch - 0.55f) / 0.45f);
        return Mathf.Clamp(sitScratchTailAmp, 0f, 1.5f)
            * (0.55f + 0.45f * pulse);
    }

    // Picture only. Sit scratch already droops the first hinge
    // (SitScratchTail) and kicks hind / face / chest / rump —
    // leftover sit held Tail2–Tail5 on TreeSpring restAngle 0
    // (muscles only on the first hinge — by design) so the kick
    // still read as a stiff caudal rod on a raking paw. Limp
    // the distal Visuals (plus, accumulating 6°…12°) following
    // idleScratch's hang 0.55 + ~14 Hz rake (env already hitchs;
    // no extra clock, not Sleep's 1.6 hitch, not sniff's ~3 Hz
    // bob, not yawn's envelope, not pant's 6.5 Hz pump, not
    // chew's 11 Hz snap, not look's signed bob, and not a new
    // distal muscle). Colliders stay. Brain already wrote
    // idleScratch; Walk owns WalkTail. Sit sniff distal is
    // SitSniffDistalTail. Sit yawn distal is SitYawnDistalTail.
    // Sit pant distal is SitPantDistalTail. Sit chew distal is
    // SitChewDistalTail. Sit look distal is SitLookDistalTail.
    // Hunt raise stays on idleHunt. Hunt leap owns LeapHuntTail.
    // Sleep distal is SitSleepDistalTail. Quiet sit distal is
    // SitQuietDistalTail. No idleTail: Walk() would
    // zero it. 0 = the old rest-spring scratch stick.
    public float SitScratchDistalTail()
    {
        if (sitScratchDistalTailAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idleLook > 0.01f || idleLook < -0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        if (idleScratch < 0.01f)
            return 0f;
        float pulse = Mathf.Clamp01((idleScratch - 0.55f) / 0.45f);
        return Mathf.Clamp(sitScratchDistalTailAmp, 0f, 1.5f)
            * (0.55f + 0.45f * pulse);
    }

    // Picture only. Sit idleScratch already kicks the near-hind,
    // weaves the face, and droops the tail — leftover sit held a
    // statue rump (ApplyRump only reads WalkRumpBob / SitRump /
    // SitYawnRump / SitSniffRump) so the itch still read as a
    // planted pelvis on a kicking paw. Hitch with the rake
    // (idleScratch already pulses ~14 Hz above the 0.55 hang).
    // In phase with the hind and SitScratchBreath (an itch, not a
    // see-saw, not a 6.5 Hz pant). Brain already wrote idleScratch;
    // Walk owns WalkRumpBob. Sit pant owns SitRump. Sit yawn owns
    // SitYawnRump. Sit sniff owns SitSniffRump. Sit look owns
    // SitLookRump. Hunt recover owns SitHuntRump. Sit chew owns
    // SitChewRump. Quiet sit /
    // Sleep stay chest-only.
    // Sleep / Leap / rest skip. Chew zeros the scratch. No
    // idleRump: Walk() would zero it. 0 = the old statue
    // scratch rump.
    public float SitScratchRump()
    {
        if (sitScratchRumpAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idleLook > 0.01f || idleLook < -0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        if (idleScratch < 0.01f)
            return 0f;
        float pulse = Mathf.Clamp01((idleScratch - 0.55f) / 0.45f);
        return Mathf.Clamp(sitScratchRumpAmp, 0f, 1.5f)
            * (0.55f + 0.45f * pulse);
    }

    // Picture only. Sit idleScratch already kicks the near-hind,
    // weaves the face, droops the tail, and heaves the rump —
    // leftover sit held quiet-sit 0.0018 (BreathLift only reads
    // idlePant / Sleep / walk / idleYawn / idleSniff) so the itch
    // still read as a statue chest on a kicking paw. Hitch with
    // the rake (idleScratch already pulses ~14 Hz above the 0.55
    // hang). In phase with SitScratchRump (an itch, not a
    // see-saw, not a 6.5 Hz pant and not the quiet sin). Brain
    // already wrote idleScratch; Walk owns WalkTrunkBob. Sit
    // pant owns the idlePant branch in BreathLift. Sit yawn
    // owns SitYawnBreath. Sit sniff owns SitSniffBreath. Sit look
    // owns SitLookBreath. Hunt recover owns SitHuntBreath. Sleep /
    // Leap / rest skip.
    // Chew zeros the scratch. No idleBreath: Walk() would zero
    // it. 0 = the old quiet-sit scratch chest.
    public float SitScratchBreath()
    {
        if (sitScratchBreathAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idleLook > 0.01f || idleLook < -0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        if (idleScratch < 0.01f)
            return 0f;
        float pulse = Mathf.Clamp01((idleScratch - 0.55f) / 0.45f);
        return Mathf.Clamp(sitScratchBreathAmp, 0f, 1.5f)
            * (0.55f + 0.45f * pulse);
    }

    // Same first-hinge spring as sit idleWag. Sit idleYawn already
    // lifts the head and opens the jaw — leftover sit held the
    // 8.5 Hz forage metronome, so the stretch still read as a
    // yawn with a sit wag. Still droop (plus, tip down) following
    // the yawn envelope (idleYawn already rises / holds / falls).
    // Brain already wrote idleYawn; Walk owns WalkTail, sit look
    // owns SitLookTail, sit sniff owns SitSniffTail, sit scratch
    // owns SitScratchTail, sit pant owns SitPantTail. Hunt raise
    // stays on idleHunt. Sit chew owns SitChewTail. Distal limp
    // is SitYawnDistalTail. Sleep / Leap / rest skip. Quiet sit
    // between acts keeps 8.5 Hz. No idleTail: Walk() would zero
    // it. 0 = the old yawn metronome.
    public float SitYawnTail()
    {
        if (sitYawnTailAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idleLook > 0.01f || idleLook < -0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn < 0.01f)
            return 0f;
        return Mathf.Clamp(sitYawnTailAmp, 0f, 1.5f)
            * Mathf.Clamp01(idleYawn);
    }

    // Picture only. Sit yawn already droops the first hinge
    // (SitYawnTail) and stretches jaw / head / tongue / lids /
    // ears / whiskers / brow / snout / lip / puff / chest / rump
    // with the gape — leftover sit held Tail2–Tail5 on TreeSpring
    // restAngle 0 (muscles only on the first hinge — by design) so
    // the stretch still read as a stiff caudal rod on a gaping
    // jaw. Limp the distal Visuals (plus, accumulating 6°…12°)
    // following idleYawn's envelope (env already rises / holds /
    // falls; no extra clock, not a 6.5 Hz pant, not Sleep's 1.6
    // hitch, not sniff's ~3 Hz bob, and not a new distal muscle).
    // Colliders stay. Brain already wrote idleYawn; Walk owns
    // WalkTail. Sit sniff distal is SitSniffDistalTail. Sit pant
    // distal is SitPantDistalTail. Sit chew distal is
    // SitChewDistalTail. Sit look distal is SitLookDistalTail.
    // Sit scratch distal is SitScratchDistalTail. Hunt raise stays
    // on idleHunt. Hunt leap owns LeapHuntTail. Sleep distal is
    // SitSleepDistalTail. Quiet sit distal is SitQuietDistalTail.
    // No idleTail: Walk() would zero it.
    // 0 = the old rest-spring yawn stick.
    public float SitYawnDistalTail()
    {
        if (sitYawnDistalTailAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idleLook > 0.01f || idleLook < -0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn < 0.01f)
            return 0f;
        return Mathf.Clamp(sitYawnDistalTailAmp, 0f, 1.5f)
            * Mathf.Clamp01(idleYawn);
    }

    // Picture only. Sit idleYawn already lifts the head, opens the
    // jaw, shuts the lids, curls the tongue, and droops the tail —
    // leftover sit held a statue half-fold 0.50, so the stretch
    // still read as a forage head on a gaping jaw. Fold back with
    // the yawn envelope (idleYawn already rises / holds / falls).
    // Brain already wrote idleYawn; Walk owns WalkEarFlop, sit look
    // owns SitLookEar, sit pant owns SitEar, scratch owns the
    // near-ear kick. Hunt pin stays on idleHunt. Sniff keeps its
    // own half-fold. Sit chew owns SitChewEar. Sleep owns
    // SitSleepEar. Leap / rest skip. No idleEar:
    // Walk() would zero it. 0 = the old statue yawn half-fold.
    public float SitYawnEar()
    {
        if (sitYawnEarAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idleLook > 0.01f || idleLook < -0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn < 0.01f)
            return 0f;
        return Mathf.Clamp(sitYawnEarAmp, 0f, 1.5f)
            * Mathf.Clamp01(idleYawn);
    }

    // Picture only. Sit idleYawn already lifts the head, opens the
    // jaw, shuts the lids, curls the tongue, droops the tail, and
    // folds the ears — leftover sit held rest whisker stamps, so the
    // stretch still read as a forage muzzle on a gaping jaw. Fan
    // with the yawn envelope (idleYawn already rises / holds /
    // falls). No twitch: this is a slow stretch, not a 14 Hz rake.
    // Brain already wrote idleYawn; Walk owns WalkWhisker, sit look
    // owns SitLookWhisker, sit pant owns SitWhisker, scratch owns
    // SitScratchWhisker. Sit chew owns SitChewWhisker. Hunt pin
    // stays on idleHunt. Sniff keeps its own fan. Sleep / Leap /
    // rest skip. No idleWhisker: Walk() would zero it. 0 = the old
    // rest yawn stamps.
    public float SitYawnWhisker()
    {
        if (sitYawnWhiskerAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idleLook > 0.01f || idleLook < -0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn < 0.01f)
            return 0f;
        return Mathf.Clamp(sitYawnWhiskerAmp, 0f, 1.5f)
            * Mathf.Clamp01(idleYawn);
    }

    // Picture only. Sit idleYawn already lifts the head, opens the
    // jaw, shuts the lids, curls the tongue, droops the tail, folds
    // the ears, and fans the whiskers — leftover sit held the rest
    // brow stamp, so the stretch still read as a forage forehead on
    // a gaping jaw. Raise with the yawn envelope (idleYawn already
    // rises / holds / falls). Stretch, not a hunt furrow and not a
    // 14 Hz rake. Brain already wrote idleYawn; Walk owns WalkBrow,
    // sit look already lifts via idleLook, sit pant owns SitBrow,
    // scratch owns SitScratchBrow. Sit chew owns SitChewBrow.
    // Hunt furrow stays on idleHunt.
    // Sniff keeps its own drop. Sleep / Leap / rest skip. No
    // idleBrow: Walk() would zero it. 0 = the old rest yawn forehead.
    public float SitYawnBrow()
    {
        if (sitYawnBrowAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idleLook > 0.01f || idleLook < -0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn < 0.01f)
            return 0f;
        return Mathf.Clamp(sitYawnBrowAmp, 0f, 1.5f)
            * Mathf.Clamp01(idleYawn);
    }

    // Picture only. Sit idleYawn already lifts the head, opens the
    // jaw, shuts the lids, curls the tongue, droops the tail, folds
    // the ears, fans the whiskers, and raises the brow — leftover
    // sit held the rest muzzle stamp, so the stretch still read as
    // a forage snout on a gaping jaw. Lift with the yawn envelope
    // (idleYawn already rises / holds / falls). Stretch, not a hunt
    // drop and not a 14 Hz rake. Brain already wrote idleYawn;
    // Walk owns WalkNose, sit look already lifts via idleLook, sit
    // pant owns SitNose, scratch owns SitScratchNose. Hunt drop
    // stays on idleHunt. Sniff keeps its own dirt drop. Sleep /
    // Leap / rest skip. No idleNose: Walk() would zero it. 0 = the
    // old rest yawn muzzle.
    public float SitYawnNose()
    {
        if (sitYawnNoseAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idleLook > 0.01f || idleLook < -0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn < 0.01f)
            return 0f;
        return Mathf.Clamp(sitYawnNoseAmp, 0f, 1.5f)
            * Mathf.Clamp01(idleYawn);
    }

    // Picture only. Sit idleYawn already lifts the head, opens the
    // jaw, shuts the lids, curls the tongue, droops the tail, folds
    // the ears, fans the whiskers, raises the brow, and lifts the
    // snout — leftover sit held the rest flews (DriveJaw open is
    // the pant drop, not a stretch with that muzzle), so the gape
    // still read as forage flews on a stretching face. Lift with
    // the yawn envelope (idleYawn already rises / holds / falls).
    // Stretch, not a hunt raise and not an extra pant hang. Brain
    // already wrote idleYawn; Walk / sit pant open via WalkPant /
    // idlePant. Scratch owns SitScratchMouth. Look owns
    // SitLookMouth. Sit chew owns SitChewMouth. Quiet sit owns
    // SitQuietMouth. Sleep owns SitSleepMouth. Hunt raise stays
    // on idleHunt. Sniff keeps its own dirt tuck. Pant / yawn /
    // chew still open with DriveJaw. Leap / rest skip. No
    // idleMouth: Walk() would zero it. 0 = the old rest yawn flews.
    public float SitYawnMouth()
    {
        if (sitYawnMouthAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idleLook > 0.01f || idleLook < -0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn < 0.01f)
            return 0f;
        return Mathf.Clamp(sitYawnMouthAmp, 0f, 1.5f)
            * Mathf.Clamp01(idleYawn);
    }

    // Picture only. Sit idleYawn already lifts the head, opens the
    // jaw, shuts the lids, curls the tongue, droops the tail, folds
    // the ears, fans the whiskers, raises the brow, lifts the snout,
    // and stretches the lip — leftover sit held silent (SitPuff
    // only reads idlePant) so the gape still read as a silent
    // stretch. Hitch with the yawn envelope (idleYawn already
    // rises / holds / falls). Needs open gape (≥0.20, same as
    // tongue) so a closed start does not leak vapor. Slow stretch,
    // not a 6.5 Hz pant. Brain already wrote idleYawn; Walk owns
    // WalkPuff. Sit pant owns SitPuff. Sit chew owns SitChewPuff.
    // Hunt recover owns SitHuntPuff. Stalk Walk owns WalkHuntPuff.
    // Leap owns LeapHuntPuff. Bark puff is Call. Sleep / Leap /
    // rest skip. No idlePuff: Walk() would zero it. 0 = the old
    // silent yawn tongue.
    public float SitYawnPuff()
    {
        if (sitYawnPuffAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idleLook > 0.01f || idleLook < -0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn < 0.20f)
            return 0f;
        return Mathf.Clamp(sitYawnPuffAmp, 0f, 1.5f)
            * Mathf.Clamp01(idleYawn);
    }

    // Picture only. Sit idleYawn already lifts the head, opens the
    // jaw, shuts the lids, curls the tongue, droops the tail, folds
    // the ears, fans the whiskers, raises the brow, lifts the snout,
    // stretches the lip, and puffs — leftover sit held quiet-sit
    // 0.0018 (BreathLift only reads idlePant / Sleep / walk) so the
    // gape still read as a statue chest. Hitch with the yawn
    // envelope (idleYawn already rises / holds / falls). Slow
    // inhale, not a 6.5 Hz pant and not the quiet sin. Brain already
    // wrote idleYawn; Walk owns WalkTrunkBob. Sit pant owns the
    // idlePant branch in BreathLift. Sit yawn rump is SitYawnRump.
    // Hunt recover owns SitHuntBreath. Sit look owns
    // SitLookBreath. Sit sniff owns SitSniffBreath. Sit scratch
    // owns SitScratchBreath. Sleep /
    // Leap / rest skip. No idleBreath: Walk() would zero it.
    // 0 = the old quiet-sit yawn chest.
    public float SitYawnBreath()
    {
        if (sitYawnBreathAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idleLook > 0.01f || idleLook < -0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn < 0.01f)
            return 0f;
        return Mathf.Clamp(sitYawnBreathAmp, 0f, 1.5f)
            * Mathf.Clamp01(idleYawn);
    }

    // Picture only. Sit idleYawn already heaves Chest Visual —
    // leftover sit held a statue rump (ApplyRump only reads
    // WalkRumpBob / SitRump / idlePant) so the gape still read as
    // a chest-only inhale. Hitch with the yawn envelope (idleYawn
    // already rises / holds / falls). In phase with SitYawnBreath
    // (a stretch, not a see-saw, not a 6.5 Hz pant). Brain already
    // wrote idleYawn; Walk owns WalkRumpBob. Sit pant owns SitRump.
    // Sit scratch owns SitScratchRump. Sit look owns SitLookRump.
    // Hunt recover owns SitHuntRump. Sit sniff owns SitSniffRump.
    // Quiet sit / Sleep stay chest-only. Sleep / Leap / rest skip. No
    // idleRump: Walk() would zero it. 0 = the old statue yawn rump.
    public float SitYawnRump()
    {
        if (sitYawnRumpAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idleLook > 0.01f || idleLook < -0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idlePant > 0.01f)
            return 0f;
        if (idleYawn < 0.01f)
            return 0f;
        return Mathf.Clamp(sitYawnRumpAmp, 0f, 1.5f)
            * Mathf.Clamp01(idleYawn);
    }

    // Same first-hinge spring as sit idleWag. Sit idlePant already
    // pumps the jaw, chest, rump, and neck — leftover sit held the
    // 8.5 Hz forage metronome, so EndWalk still read as a pant with
    // a happy sit wag. Still droop (plus, tip down) hitching with
    // the pump (idlePant already hangs 0.55 + 6.5 Hz). Brain already
    // wrote idlePant; Walk owns WalkTail, sit look owns SitLookTail,
    // sit sniff owns SitSniffTail, sit scratch owns SitScratchTail,
    // sit yawn owns SitYawnTail. Hunt raise stays on idleHunt. Sit
    // chew owns SitChewTail. Distal limp is SitPantDistalTail.
    // Sleep / Leap / rest skip. Quiet sit between acts keeps
    // 8.5 Hz. No idleTail: Walk() would zero it. 0 = the old
    // pant metronome.
    public float SitPantTail()
    {
        if (sitPantTailAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idleLook > 0.01f || idleLook < -0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        if (idlePant < 0.01f)
            return 0f;
        float pulse = Mathf.Clamp01((idlePant - 0.55f) / 0.45f);
        return Mathf.Clamp(sitPantTailAmp, 0f, 1.5f)
            * (0.55f + 0.45f * pulse);
    }

    // Picture only. Sit pant already droops the first hinge
    // (SitPantTail) and pumps jaw / chest / rump / neck / tongue
    // / lids / snout / pupil / brow / whiskers / ears / puff
    // with the hang — leftover sit held Tail2–Tail5 on TreeSpring
    // restAngle 0 (muscles only on the first hinge — by design) so
    // EndWalk still read as a stiff caudal rod on a panting face.
    // Limp the distal Visuals (plus, accumulating 6°…12°) following
    // idlePant's hang 0.55 + 6.5 Hz pump (env already hitchs; no
    // extra clock, not Sleep's 1.6 hitch, not sniff's ~3 Hz bob,
    // not yawn's envelope, and not a new distal muscle). Colliders
    // stay. Brain already wrote idlePant; Walk owns WalkTail. Sit
    // sniff distal is SitSniffDistalTail. Sit yawn distal is
    // SitYawnDistalTail. Sit chew distal is SitChewDistalTail.
    // Sit look distal is SitLookDistalTail. Sit scratch distal is
    // SitScratchDistalTail. Hunt raise stays on idleHunt.
    // Hunt leap owns LeapHuntTail. Sleep distal is
    // SitSleepDistalTail. Quiet sit distal is SitQuietDistalTail.
    // No idleTail: Walk() would zero it.
    // 0 = the old rest-spring pant stick.
    public float SitPantDistalTail()
    {
        if (sitPantDistalTailAmp < 0.01f)
            return 0f;
        if (energyRest)
            return 0f;
        if (IsLeap())
            return 0f;
        if (walkWanted || IsWalk())
            return 0f;
        if (idleHunt > 0.01f)
            return 0f;
        if (idleChew > 0.08f)
            return 0f;
        if (idleSleep > 0.01f)
            return 0f;
        if (idleLook > 0.01f || idleLook < -0.01f)
            return 0f;
        if (idleSniff > 0.01f)
            return 0f;
        if (idleScratch > 0.01f)
            return 0f;
        if (idleYawn > 0.01f)
            return 0f;
        if (idlePant < 0.01f)
            return 0f;
        float pulse = Mathf.Clamp01((idlePant - 0.55f) / 0.45f);
        return Mathf.Clamp(sitPantDistalTailAmp, 0f, 1.5f)
            * (0.55f + 0.45f * pulse);
    }

    // CoM-capture bound: dual take (load stance, shorten swing 20°)
    // then swing distal latches a 0.12 m chord fold. Stance distal
    // stays on the Walk() chord, ≤15°. DogDrive or CLI writes this.
    // Not a keyboard.
    public void Walk()
    {
        if (IsLeap())
            return;
        // Wander / follow FaceToward then Walk() in the same Update:
        // sit hop would die here, so the about-face takes the walk hop.
        bool pendingSitTurn = sitTurnUntil > Time.time;
        ClearIdle();
        sitTurnUntil = 0f;
        walkWanted = true;
        // Canonical diagonal. runSpeed 0 is the leftover
        // sit-trot (dog_w10 0.286 km/h). CLI -dogWalk and Drive both
        // enter here so Play cannot keep duty 0.64. Quiet stand is
        // walkWanted false. Onset only writes phase 0. Dual plus-paws
        // the trail with Dual-soft hip until heel-rise, then Arms.
        // Dual extra-extends that trail hip 4–8° (same ramp as
        // stance) so Dual-soft rolls onto the toe. Dual-soft hip is
        // Dual-only and trail-only. After Arm: hip HOLDS leftover
        // sit; trail knee is sit-relative +18° ON from Arm; trail
        // paw Zeroes. Dual plus-paws 0.18 on the take. Plus 0.18
        // after Arm cannot pitch a loaded pad (human ankle). Arm at
        // minDual Zeroed a still-flat pad (dog_w66 leftover sit-trot).
        // DualReady 0.12 s ceiling and a 3 cm CoM shift did the same
        // (dog_w67). Dual lean ramps from Dual t=0. Caudal 10° after
        // Arm skated the pad.
        // Arm-tick U=0 jacked leftover sit.
        // Inverted-V 6 cm was ~63° (dog_w58). 3 mm never came
        // on a welded pad (dog_w63). dog_w62 more-minus was
        // swing-flex on a pin. Extra plus-knee shortened the
        // column (dog_w61). Dual-soft hip+knee was a limp squat
        // (dog_w60). Front swing stays a third plant until the
        // hind is air. True air holds that fold and plants
        // cranially — 12 cm IK was a chair. Distal opens after
        // the hip is cranial. Stance CoP walks at runSpeed only
        // after the trail is air. First peel stays armed unless
        // the trunk is a belly. walkPhase 1 when the rear swing
        // paw is up. Swap is that plant.
        EnsureBoundGait();
        if (pendingSitTurn)
            NoteWalkTurn();
        // Fallen / Falling: keep the wish, enter GettingUp in Sense.
        // DriveGetUp tucks then rises; do not Arm capture Walk until
        // BeginWalkAfterGetUp.
        if (currentState == StanceState.Falling
            || currentState == StanceState.Fallen
            || currentState == StanceState.GettingUp)
            return;
        if (!standComHeightValid)
            return;
        if (walkPhase < 0f)
            walkPhase = 0f;
        // Drive calls Walk() every Update. Hold is the measured
        // quiet-sit pelvis, not first-frame spawn (that column is
        // ~0.19 m above leftover sit and arms WalkCollapsed).
        LatchWalkPelvisHold();
        SnapshotWalkEntry();
    }

    // Canonical diagonal. Leftover sit-trot is runSpeed 0 / duty 0.64 /
    // swing flex 18° (dog_w10 0.286 km/h): the 0.10 m paw stayed
    // IsTouching and dragged. CLI -dogWalk and DogDrive both enter
    // here. Quiet stand does not call this. Desired speed is the
    // 5 km/h gate (1.389 m/s) when the leftover 0 is still on the
    // Inspector — muscles chase it, there is no AddForce. Walk()
    // onset is phase 0 only. Dual plus-paws the trail with Dual-soft
    // hip until heel-rise, then Arms (no spawn HeightLift). Dual
    // extra-extends that trail hip 4–8° (same ramp as stance) so
    // Dual-soft rolls onto the toe (dog_w69). Arm at minDual Zeroed
    // a still-flat pad (dog_w66 leftover sit-trot). 0.30 s
    // DualSupportMax ate the half. Dual-soft hip is Dual-only
    // and trail-only. After Arm the trail hip HOLDS leftover sit;
    // the trail knee is sit-relative +18° ON from Arm; the trail
    // paw Zeroes. Plus 0.18 after Arm cannot pitch a loaded pad.
    // Dual still plus-paws the take. Caudal 10° after Arm
    // skated the pad. Arm-tick U=0 jacked leftover sit. Inverted-V 6 cm
    // was ~63° (dog_w58). 3 mm never came on a welded pad (dog_w63).
    // dog_w62 more-minus was swing-flex on a pin. Extra plus-knee
    // shortened the column (dog_w61). Dual-soft hip+knee was
    // a limp squat (dog_w60). Front swing stays planted until
    // that hind is air. True air holds that fold and plants
    // cranially (12 cm IK sat leftover sit). Distal
    // opens after airU. Stance CoP walks at runSpeed only after
    // the trail is air; dual/peel stay on launch+lead so the sole
    // is not a treadmill. Clock is Dual-until-heel-rise
    // + 0.08 peel + 0.08 air on the 0.229 s half. DualReady
    // 0.12 s ceiling Zeroed a still-flat pad (dog_w67).
    public void EnsureBoundGait()
    {
        if (runSpeed <= 0.05f)
            runSpeed = Mathf.Max(1.389f, runSpeedRef);
        if (walkDuty > 0.50f)
            walkDuty = 0.38f;
        if (walkHipExtend > 12f)
            walkHipExtend = 12f;
        if (walkShoulderExtend > 12f)
            walkShoulderExtend = 12f;
        if (walkLaunchDrop > 0f && walkLaunchDrop < 0.20f)
            walkLaunchDrop = 0.22f;
        if (walkGroundedSwingPlant < 0.99f)
            walkGroundedSwingPlant = 1f;
        if (walkKneeFlex < 30f)
            walkKneeFlex = 42f;
        if (walkElbowFlex < 30f)
            walkElbowFlex = 42f;
        if (walkStanceKnee > WalkStanceDistalMax)
            walkStanceKnee = WalkStanceDistalMax;
        if (walkStanceElbow > WalkStanceDistalMax)
            walkStanceElbow = WalkStanceDistalMax;
        // 0.45·stride = 0.144 m → 2.3 km/h at the 0.229 s half.
        // 0.70·stride = 0.224 m is the 5 km/h order with body travel.
        if (walkSwingLiftFrac < 0.70f)
            walkSwingLiftFrac = 0.75f;
        // dog_w74: speed-error hip/shoulder degrees need a floor so
        // the first Dual already chases 1.4 m/s (sit-trot 0.286 km/h
        // left mix at 0 and never pushed).
        if (walkComMix < 0.28f)
            walkComMix = 0.28f;
        // dog_w73: 0.32/1.4 → 0.229 s half (~2.2 Hz pair). Cap so
        // half ≤ 0.20 s; peel 0.07 + air 0.07 + minDual 0.06 fit.
        // dog_w74: half ≤ 0.18 s (~2.8 Hz pair); peel/air 0.06 fit.
        // Leftover sit-trot skated one Dual forever (0.286 km/h).
        if (runSpeed > 0.05f && runStride / Mathf.Max(0.2f, runSpeed) > 0.18f)
            runStride = 0.18f * Mathf.Max(0.2f, runSpeed);
    }

    public void EndWalk()
    {
        walkWanted = false;
        walkTurnUntil = 0f;
        ResetWalkClock();
        if (IsWalk())
            currentState = StanceState.Balancing;
        else if (currentState == StanceState.GettingUp)
            currentState = StanceState.Fallen;
    }

    public bool CanIdle()
    {
        return !energyRest
               && currentState == StanceState.Balancing
               && !walkWanted
               && !IsLeap();
    }

    public bool IsFallen()
    {
        return currentState == StanceState.Fallen
               || currentState == StanceState.Falling;
    }

    public bool IsGettingUp()
    {
        return currentState == StanceState.GettingUp;
    }

    public void ClearIdle()
    {
        idleSniff = 0f;
        idleScratch = 0f;
        idleYawn = 0f;
        idleLook = 0f;
        idleWag = 0f;
        idleSleep = 0f;
        idlePant = 0f;
        idleChew = 0f;
        idleHunt = 0f;
    }

    // Nose-down sleep / sniff, chew hitch, scratch tuck toward the
    // near-hind, look bob, yawn lift, sit-pant drop, hunt-recover
    // stalk drop, quiet-sit breath hitch. Do not write
    // neckTargetTilt: that field is the leftover SPD hold, not an
    // idle gain.
    private float IdleNeckWorldTarget()
    {
        float t = neckTargetTilt;
        if (idleSleep > 0.01f)
            t -= 22f * idleSleep;
        // Sit chew already snaps the jaw and hitches chest / rump /
        // tail / lid / ear / brow / whisker. Chew reuses idleSniff
        // for the paw, so the 16° sniff path would win and the meal
        // would hold a dirt-track neck. Amp 0 keeps that. Hang 0.55
        // + snap hitch; same 16° as sit sniff, not a new neckP and
        // not SitHead's 3° pump. Before sniff: chew reuses idleSniff.
        float chewH = SitChewHead();
        if (chewH > 0.01f)
            t -= 16f * chewH;
        else if (idleSniff > 0.01f)
            t -= 16f * idleSniff;
        // Chin to the flank so the near-hind Visual meets the ear, not a
        // hold-stare while the shin twitches. Same SPD target, not neckP.
        if (idleScratch > 0.01f)
            t -= 10f * idleScratch;
        if (idleLook > 0.01f || idleLook < -0.01f)
            t += 12f * idleLook;
        if (idleYawn > 0.01f)
            t += 8f * idleYawn;
        // Sit glance already wrote SetFacing. A short nod reads the
        // plant-hop; same SPD world target, not a new neckP.
        float hop = SitTurnHop();
        if (hop > 0.01f)
            t += 8f * hop;
        else
        {
            float huntH = SitHuntHead();
            if (huntH > 0.01f)
                t -= 12f * huntH;
            else
            {
                float pantH = SitHead();
                if (pantH > 0.01f)
                    t -= 3f * pantH;
                else
                {
                    float quietH = SitQuietHead();
                    if (quietH > 0.01f)
                    {
                        float tBreath = Time.time + sitBreathPhase;
                        t -= 1.2f * quietH * (1f + Mathf.Sin(tBreath * 2.5f));
                    }
                }
            }
        }
        return Mathf.Clamp(t, -28f, 28f);
    }

    private float IdleTailTarget()
    {
        // Forage trot already wrote idleWag. Prefer the gait clock so
        // the tail is not a sit metronome on a bobbing trunk. Amp 0
        // keeps the old 8.5 Hz. Hop / hunt / CLI fall through.
        if ((walkWanted || IsWalk()) && idleWag >= 0.01f && idleHunt < 0.01f
            && walkTailAmp >= 0.01f)
            return tailBaseAngle + 16f * WalkTail();
        // Sit look already weaves the neck / face. TickIdle still
        // writes idleWag, so the 8.5 Hz path would win and the
        // glance would keep the sit metronome. Amp 0 keeps that.
        // Look-up perks (minus); look-down droops (plus).
        float lookT = SitLookTail();
        if (Mathf.Abs(lookT) > 0.01f)
            return tailBaseAngle - 16f * lookT;
        // Sit chew already snaps the jaw and hitches chest / rump.
        // Chew zeros idleWag so the 8.5 Hz path is dead, and
        // SitSniffTail skips idleChew except the 0.08 dip — leftover
        // sit held rest (tailBaseAngle). Amp 0 keeps that. Droop is
        // plus (clockwise, tip down) — same side as sniff. Hang 0.55
        // + snap hitch; 10° so the ~11 Hz snap does not rattle like
        // a full 16° wag. Before sniff: chew reuses idleSniff.
        float chewT = SitChewTail();
        if (chewT > 0.01f)
            return tailBaseAngle + 10f * chewT;
        // Sit sniff already weaves the neck / muzzle. TickIdle still
        // writes idleWag, so the 8.5 Hz path would win and the track
        // would keep the sit metronome. Amp 0 keeps that. Droop is
        // plus (clockwise, tip down) — same side as look-down.
        float sniffT = SitSniffTail();
        if (sniffT > 0.01f)
            return tailBaseAngle + 12f * sniffT;
        // Sit scratch already weaves the neck / hind / face.
        // TickIdle still writes idleWag, so the 8.5 Hz path would
        // win and the kick would keep the sit metronome. Amp 0
        // keeps that. Droop is plus (clockwise, tip down) — same
        // side as sniff / look-down. Hang 0.55 + rake hitch; 10°
        // so the ~14 Hz kick does not rattle like a full 16° wag.
        float scratchT = SitScratchTail();
        if (scratchT > 0.01f)
            return tailBaseAngle + 10f * scratchT;
        // Sit yawn already stretches the jaw / head. TickIdle still
        // writes idleWag, so the 8.5 Hz path would win and the gape
        // would keep the sit metronome. Amp 0 keeps that. Droop is
        // plus (clockwise, tip down) — same side as sniff / scratch.
        // Envelope hitch; 12° like sniff (slow stretch, not a 14 Hz
        // kick).
        float yawnT = SitYawnTail();
        if (yawnT > 0.01f)
            return tailBaseAngle + 12f * yawnT;
        // Sit pant already pumps the jaw / chest / rump / neck.
        // TickIdle still writes idleWag, so the 8.5 Hz path would
        // win and EndWalk would keep the sit metronome. Amp 0
        // keeps that. Droop is plus (clockwise, tip down) — same
        // side as sniff / yawn. Hang 0.55 + pump hitch; 10° so
        // the ~6.5 Hz pant does not rattle like a full 16° wag.
        float pantT = SitPantTail();
        if (pantT > 0.01f)
            return tailBaseAngle + 10f * pantT;
        // Night Sleep already drops the chin / lids / ears.
        // TickSleep zeros idleWag, so the 8.5 Hz path is dead —
        // leftover sit held rest (tailBaseAngle). Amp 0 keeps
        // that. Droop is plus (clockwise, tip down) — limp, same
        // 12° as sniff / yawn, not a wag. Distal physics stay rest
        // spring; picture limp is SitSleepDistalTail.
        float sleepT = SitSleepTail();
        if (sleepT > 0.01f)
            return tailBaseAngle + 12f * sleepT;
        if (idleWag >= 0.01f)
            return tailBaseAngle + 16f * idleWag * Mathf.Sin(Time.fixedTime * 8.5f);
        // Positive jointAngle droops a caudal tail (clockwise, tip down).
        // Hunt holds a stiff raise, not a wag. Distal Tail2–Tail5
        // picture perk is SitHuntDistalTail / WalkHuntDistalTail.
        // Leap owns LeapHuntTail (DriveLeap; LeapAttack zeros
        // idleHunt). Leap distal is LeapHuntDistalTail.
        if (idleHunt > 0.01f)
            return tailBaseAngle - 16f * idleHunt;
        return tailBaseAngle;
    }

    private void ResetWalkClock()
    {
        walkClockStart = -1f;
        walkPhase = -1f;
        walkAnimPhase = 0f;
        walkCaptureArmed = false;
        walkSwungAir = false;
        walkNearSwungAir = false;
        walkAirBorn = -1f;
        walkAirStreak = 0;
        walkNearAirStreak = 0;
        walkTrailRockerBorn = -1f;
        walkNearRockerBorn = -1f;
        walkHalf = 0;
        walkHalfBorn = -1f;
        walkSwingCaudalX = 0f;
        hasWalkPelvisHold = false;
        hasWalkEntry = false;
        hasWalkLatch = false;
        hasWalkFold = false;
        hasWalkFoldLift = false;
        walkFoldLiftTrailX = float.NaN;
        walkFoldLiftTrailY = float.NaN;
        walkFoldLiftTrailRelY = float.NaN;
        walkFoldLiftNearX = float.NaN;
        walkFoldLiftNearY = float.NaN;
        walkFoldLiftNearRelY = float.NaN;
        hasWalkComHold = false;
        // Keep walkOnsetLog: EndWalk must not wipe the first 3 s
        // the recorder reads at summary time.
    }

    public bool IsWalk()
    {
        return currentState == StanceState.Walk;
    }

    // Open a crack and cry. DogDrive or CLI call this. Not a keyboard.
    public void Bark()
    {
        barkUntil = Time.fixedTime + 0.16f;
        if (voice != null)
            voice.Cry(VoiceKind.Call);
    }

    // Open then slam shut and arm JawStrike. Closing is minus jointAngle.
    public void Bite()
    {
        biteOpenUntil = Time.fixedTime + 0.10f;
        biteCloseUntil = Time.fixedTime + 0.36f;
    }

    // Crouch, push with the planted paws, fly at the mark, bite.
    // Not a brain and not AddForce: the launch is knee/elbow extension
    // while the feet are still on the ground.
    public void LeapAttack(Transform target = null)
    {
        // Sit on this body often trips Falling from CoM offset. The
        // brain / CLI must still start the leap; DriveLeap, not RelaxAll,
        // owns the jump.
        if (IsLeap())
            return;
        EndWalk();
        ClearIdle();
        sitTurnUntil = 0f;
        walkTurnUntil = 0f;
        if (target != null)
            leapTarget = target;
        if (leapTarget == null)
        {
            // Once at the leap start, not in DriveLeap / AdvanceLeap.
            DogPrey mark = FindFirstObjectByType<DogPrey>();
            if (mark != null)
                leapTarget = mark.transform;
        }
        ResolveLeapAim();
        if (leapTarget == null)
        {
            Vector2 chest = chestTf != null ? (Vector2)chestTf.position : (Vector2)transform.position;
            leapAimPoint = chest + new Vector2(1.2f, 0f);
        }
        currentState = StanceState.LeapCrouch;
        leapPhaseStart = Time.fixedTime;
        leapBiteStarted = false;
        leapCount++;
    }

    public bool IsLeap()
    {
        return currentState == StanceState.LeapCrouch
               || currentState == StanceState.LeapPush
               || currentState == StanceState.LeapAir
               || currentState == StanceState.LeapBite;
    }

    public Vector2 LeapAim => leapAimPoint;

    private void ResolveLeapAim()
    {
        if (leapTarget != null)
            leapAimPoint = leapTarget.position;
        // Dead / missing mark: keep the last aim. Do not Find here (200 Hz).
    }

    private void AdvanceLeap()
    {
        ResolveLeapAim();
        float age = leapPhaseStart >= 0f ? Time.fixedTime - leapPhaseStart : 0f;

        switch (currentState)
        {
            case StanceState.LeapCrouch:
                if (age >= leapCrouchSeconds)
                    EnterLeapPhase(StanceState.LeapPush);
                break;
            case StanceState.LeapPush:
                if (AllPawsAirborne() || age >= leapPushSeconds)
                    EnterLeapPhase(StanceState.LeapAir);
                break;
            case StanceState.LeapAir:
                if (InLeapBiteRange() || age >= leapAirSeconds)
                {
                    EnterLeapPhase(StanceState.LeapBite);
                    TryStartLeapBite();
                }
                break;
            case StanceState.LeapBite:
                TryStartLeapBite();
                if (age >= leapBiteSeconds)
                    EndLeap();
                break;
        }
    }

    private void EnterLeapPhase(StanceState next)
    {
        currentState = next;
        leapPhaseStart = Time.fixedTime;
    }

    private void TryStartLeapBite()
    {
        if (leapBiteStarted)
            return;
        leapBiteStarted = true;
        Bite();
    }

    private void EndLeap()
    {
        float drop = hasHeightTarget ? Mathf.Max(0f, targetPelvisY - pelvisY) : 0f;
        // CoM leaving the paws is not a fall during the jump. Fold only
        // when the trunk has really settled onto the dirt.
        currentState = drop > 0.60f ? StanceState.Falling : StanceState.Balancing;
        leapPhaseStart = -1f;
    }

    private bool AllPawsAirborne()
    {
        return !frontRightGrounded && !frontLeftGrounded
               && !rearRightGrounded && !rearLeftGrounded;
    }

    private bool InLeapBiteRange()
    {
        return Vector2.Distance(MouthPoint(), leapAimPoint) < leapBiteRange;
    }

    private Vector2 MouthPoint()
    {
        if (jawTf != null)
            return jawTf.position;
        if (headTf != null)
            return headTf.position;
        if (chestTf != null)
            return (Vector2)chestTf.position + new Vector2(0.25f, 0f);
        return transform.position;
    }

    // Blend the leftover sit-trot toward the run pose from the
    // speed we actually have. Desired runSpeed 1.4 used to set k = 1
    // on the first Walk() tick, so a sit took the 20° run flex and
    // dumped. Live Walk() now floors k at 0.45 so the first dual
    // already has a run-sized push; air / swap raise it. Chest lean
    // uses the same k: multiplying runChestLean by raw along/ref
    // cancelled the lean on a sit, so CoM never left the mid-paws.
    private float GaitSpeedK()
    {
        if (runSpeed <= 0.05f)
            return 0f;
        float along = Mathf.Max(0f, AlongFacingSpeed());
        float k = Mathf.Clamp01(along / Mathf.Max(0.2f, runSpeedRef));
        // Waiting for 1.4 m/s kept walkHipFlex 8 and cancelled the
        // lean (dog_w10 skate). A live Walk() takes a floor so the
        // first dual already has a run-sized push; air / swap raise it.
        // dog_w74: raise the onset floor — 0.45 left the first half
        // on sit-sized flex and travel stalled under 1 m/s.
        if (walkWanted || IsWalk())
            k = Mathf.Max(k, 0.55f);
        if (walkCaptureArmed)
            k = Mathf.Max(k, 0.65f);
        if (walkSwungAir)
            k = Mathf.Max(k, 0.80f);
        if (walkSwapCount > 0)
            k = Mathf.Max(k, 0.90f);
        return k;
    }

    private float GaitAmp(float walkValue, float runValue)
    {
        if (runSpeed <= 0.05f)
            return walkValue;
        return Mathf.Lerp(walkValue, runValue, GaitSpeedK());
    }

    // One half of the bound. runSpeed 1.4 / runStride 0.32 → 0.229 s
    // (~5 km/h if the swing actually plants a stride ahead). Leftover
    // walkPeriod 0.40 with duty 0.64 sat on phase 0 for 0.256 s.
    // dog_w73: EnsureBoundGait caps stride/speed at 0.20; floor 0.14
    // so a slow Inspector stride cannot park the clock.
    // dog_w74: cap 0.18 / floor 0.12 — raise step frequency without
    // parking Dual forever (sit-trot 0.286 km/h).
    private float WalkPeriodSeconds()
    {
        if (runSpeed > 0.05f)
            return Mathf.Clamp(runStride / Mathf.Max(0.2f, runSpeed), 0.12f, 0.18f);
        return Mathf.Max(0.12f, walkPeriod);
    }

    // Grounded swing: shorten the hip-paw chord 0.12 m so the 0.10 m
    // paw leaves IsTouching (heel OR toe). Gait flex 42° / hold+12°
    // only ate ~1–3 cm — inside 2×contactOffset, same as the human
    // 3 cm bars. Dual keeps leftover sit. Peel from that sit on the
    // Arm clock — U from half-born after the 0.12 s lean was 1 on
    // the Arm tick (loaded 80° squat).
    // Air: keep the latched fold until the hip has reached, then
    // open. Opening on WalkAirBlend at reach 0 planted at sit X.
    // Opening on 70 % of WalkStrideNeed never opened (folded chord
    // ~10 cm vs 16 cm gate) — swap 0, trunk drift.
    private float WalkSwingDistal(bool isRear, bool pawGrounded, float reach)
    {
        float flex = isRear
            ? GaitAmp(walkKneeFlex, runKneeFlex)
            : GaitAmp(walkElbowFlex, runElbowFlex);
        float hold = WalkQuietDistal(isRear);
        bool thisAir = isRear ? walkSwungAir : walkNearSwungAir;
        if (!pawGrounded || thisAir)
        {
            float fromAir = hasWalkLatch
                ? (isRear ? walkLatchKnee : walkLatchElbow)
                : flex;
            // Open after the hip is cranial, not after heel travel.
            // gone≥0.70·need never arrived on a folded chord
            // (dog_w50 swap 0). Keep the latched fold until
            // WalkAirOpenStart of airU, then plant.
            float open = WalkSwingOpenU(reach);
            return Mathf.Lerp(fromAir, hold, open);
        }
        float cap = hasWalkFold
            ? (isRear ? walkFoldKnee : walkFoldElbow)
            : Mathf.Min(flex, hold + (isRear ? 22f : 12f));
        float u = Mathf.Clamp01(walkGroundedSwingPlant);
        // Dual keeps leftover sit (WalkEntryDistal). Peel from that
        // sit to the 0.12 m chord on the Arm clock. U from half-born
        // after a 0.12 s lean was already 1 on the Arm tick — a
        // loaded 80° squat. Lerp from quiet hold jumped 24°→15°
        // (opened the V, paw stayed down).
        float from = WalkEntryDistal(isRear);
        float ramp = SwingPeelU();
        return Mathf.Lerp(from, cap, u * ramp);
    }

    // Standing inverted-V: each bone ~12° from vertical. Hip hold
    // keeps that proximal; distal flex θ shortens
    // L1·cos(a) + L2·cos(a−θ) + pawH. Dirt rocker uses ~6 cm so
    // the pad can leave without sitting the pelvis; free-foot IK
    // still asks 12 cm.
    private float SwingFoldForClearance(bool isRear, float hold)
    {
        return SwingFoldForClearance(isRear, hold, WalkSwingClearanceM);
    }

    private float SwingFoldForClearance(bool isRear, float hold, float clearanceM)
    {
        float L1 = isRear ? rearThighLen : frontUpperLen;
        float L2 = isRear ? rearShinLen : frontLowerLen;
        if (L1 < 0.05f)
            L1 = 0.17f;
        if (L2 < 0.05f)
            L2 = 0.17f;
        float paw = walkPawH > 0.01f ? walkPawH : 0.05f;
        const float a = 12f;
        float c1 = Mathf.Cos(a * Mathf.Deg2Rad);
        float stand = L1 * c1 + L2 * c1 + paw;
        float want = stand - Mathf.Max(0.03f, clearanceM);
        float c2 = (want - L1 * c1 - paw) / L2;
        c2 = Mathf.Clamp(c2, -1f, 1f);
        float theta = a + Mathf.Acos(c2) * Mathf.Rad2Deg;
        if (theta < hold + 8f)
            theta = hold + 8f;
        if (theta > WalkSwingFoldMax)
            theta = WalkSwingFoldMax;
        return theta;
    }

    private float WalkSwingRockerDistal(bool isRear)
    {
        float from = WalkEntryDistal(isRear);
        float cap = hasWalkFold
            ? (isRear ? walkRockerKnee : walkRockerElbow)
            : from + 12f;
        cap = Mathf.Clamp(cap, from, WalkSwingFoldMax);
        return Mathf.Lerp(from, cap, SwingRockerU(isRear));
    }

    // Peel U from Arm for the trail (U=1 so leftover sit is not
    // left on the Arm tick). Front still ramps after hind air.
    // Arm U hitting 1 into a 63° chair was dog_w54; +18° is the
    // matching unload, not that squat.
    private float SwingRockerU(bool isRear)
    {
        float born = isRear ? walkTrailRockerBorn : walkNearRockerBorn;
        if (born < 0f)
            return 0f;
        // Trail: matching +18° is ON from Arm. a<=0 returned 0 on
        // the Arm tick, so leftover sit stayed while stance already
        // opened 6° — the same jack as waiting for 3 mm. Front
        // still ramps after hind air (not a 63° chair).
        if (isRear)
            return 1f;
        float a = Time.fixedTime - born;
        return a <= 0f ? 0f : Mathf.Clamp01(a / WalkSwingPeelSeconds);
    }

    // Half 0 peels RR + FL; half 1 peels RL + FR. Heel in the
    // slab band means keep entry knee (plus paw only).
    private bool SwingHeelPinned(bool isRear)
    {
        if (isRear)
            return walkHalf == 0 ? rearRightHeel : rearLeftHeel;
        return walkHalf == 0 ? frontLeftHeel : frontRightHeel;
    }

    private bool SwingToePinned(bool isRear)
    {
        if (isRear)
            return walkHalf == 0 ? rearRightToe : rearLeftToe;
        return walkHalf == 0 ? frontLeftToe : frontRightToe;
    }

    private void SwingSoleY(bool isRear, out float heelY, out float toeY)
    {
        if (isRear)
        {
            if (walkHalf == 0)
            {
                heelY = rearRightHeelY;
                toeY = rearRightToeY;
            }
            else
            {
                heelY = rearLeftHeelY;
                toeY = rearLeftToeY;
            }
            return;
        }

        if (walkHalf == 0)
        {
            heelY = frontLeftHeelY;
            toeY = frontLeftToeY;
        }
        else
        {
            heelY = frontRightHeelY;
            toeY = frontRightToeY;
        }
    }

    // Plus 0.18 pitches the pad a few millimetres long before the
    // 2 cm slab band clears. That relative rise is the rocker gate.
    private bool SwingHeelRose(bool isRear)
    {
        SwingSoleY(isRear, out float heelY, out float toeY);
        return heelY > toeY + WalkHeelRiseM;
    }

    private bool SwingReadyToRocker(bool isRear)
    {
        return SwingHeelRose(isRear)
               || (!SwingHeelPinned(isRear) && SwingToePinned(isRear));
    }

    // Trail after Arm: sit-relative +18° dirt rocker ON from Arm.
    // Hip HOLDS leftover sit (caudal 10° skated the pad). Extra
    // plus-knee on a caudal hip shortened the column (dog_w61).
    // Inverted-V 6 cm at Arm sat the pelvis (dog_w58, ~63°).
    // Dual-soft 0.35 toward sit dumped it (dog_w60). Front stays
    // leftover sit until the hind is air so both girdles do not
    // sag at once. Hip hold is WalkLimbTargets, not here.
    private float SwingDirtDistal(bool isRear)
    {
        if (!isRear && !walkSwungAir)
            return WalkEntryDistal(false);
        if (SwingRockerU(isRear) > 0f || SwingReadyToRocker(isRear))
            return WalkSwingRockerDistal(isRear);
        return WalkEntryDistal(isRear);
    }

    // Trail rocker is ON from Arm (sit-relative +18°, U=1).
    // Waiting for 3 mm welded the pad. Arm-tick U=0 left
    // leftover sit while stance opened 6° (dog_w64). Caudal
    // hip 10° skated that pad. Inverted-V 6 cm at Arm was
    // ~63° (dog_w58). Front waits until the hind is air.
    private void StepSwingRockerClock()
    {
        if (!walkCaptureArmed)
            return;
        if (!walkSwungAir)
        {
            walkNearRockerBorn = -1f;
            return;
        }
        if (walkNearRockerBorn < 0f && SwingReadyToRocker(false))
            walkNearRockerBorn = Time.fixedTime;
    }

    private void LatchWalkSwingFold()
    {
        if (hasWalkFold)
            return;
        // Dirt: leftover-sit distal + 18°, not inverted-V 6 cm
        // (~63° chair, dog_w58). Air still uses 12 cm clearance.
        float sitKn = WalkEntryDistal(true);
        float sitEl = WalkEntryDistal(false);
        walkRockerKnee = Mathf.Clamp(sitKn + WalkSwingRockerExtraDeg, sitKn, WalkSwingFoldMax);
        walkRockerElbow = Mathf.Clamp(sitEl + WalkSwingRockerExtraDeg, sitEl, WalkSwingFoldMax);
        walkFoldKnee = SwingFoldForClearance(true, WalkQuietDistal(true));
        walkFoldElbow = SwingFoldForClearance(false, WalkQuietDistal(false));
        hasWalkFold = true;
        HingeJoint2D trailHip = walkHalf == 0 ? rearRightHipJoint : rearLeftHipJoint;
        HingeJoint2D trailPaw = walkHalf == 0 ? rearRightPawJoint : rearLeftPawJoint;
        HingeJoint2D nearHip = walkHalf == 0 ? frontLeftShoulderJoint : frontRightShoulderJoint;
        HingeJoint2D nearPaw = walkHalf == 0 ? frontLeftPawJoint : frontRightPawJoint;
        walkFoldHipTrail = JointOr(trailHip, hipBaseAngle);
        walkFoldHipNear = JointOr(nearHip, shoulderBaseAngle);
        LatchWalkLiftPoint(trailHip, trailPaw,
            out walkFoldLiftTrailX, out walkFoldLiftTrailY, out walkFoldLiftTrailRelY);
        LatchWalkLiftPoint(nearHip, nearPaw,
            out walkFoldLiftNearX, out walkFoldLiftNearY, out walkFoldLiftNearRelY);
        hasWalkFoldLift = !float.IsNaN(walkFoldLiftTrailX) || !float.IsNaN(walkFoldLiftNearX);
    }

    // World paw X frozen at Arm (live X is a shuffle; a dirt lead
    // was a planted skate). Y floor is that paw plus 0.12 m so a
    // sagging hip does not drag the target into dirt. Live IK Y
    // rides the hip (relY). Gap to the hip is 2 cm: 8 cm ate the
    // leftover-sit 12 cm lift. Only used on a FREE foot.
    private void LatchWalkLiftPoint(HingeJoint2D hip, HingeJoint2D paw,
        out float x, out float y, out float relY)
    {
        x = float.NaN;
        y = float.NaN;
        relY = float.NaN;
        if (paw == null)
            return;
        Vector2 p = paw.transform.TransformPoint(paw.anchor);
        x = p.x + WalkSwingReachLeadM * FacingSign();
        y = p.y + WalkSwingClearanceM;
        if (hip == null)
            return;
        float hipY = hip.transform.TransformPoint(hip.anchor).y;
        float maxY = hipY - WalkSwingIkHipGapM;
        if (y > maxY)
            y = maxY;
        relY = y - hipY;
    }

    // Two-link IK to the latched lift point. dog_w56 does not call
    // this: 12 cm with hip gap 2 cm was a chair on leftover sit.
    // Hip may rotate only as far as that point needs (thigh toward
    // vertical). Not scissor (−flexHip / a forward reach) and not
    // Zero (chair). Live Y rides the hip (relY); floor is the Arm
    // world clearance. Gap to the hip is WalkSwingIkHipGapM.
    private bool TryWalkPeelIk(bool trail, out float proximal, out float distal)
    {
        proximal = WalkQuietProximal(trail);
        distal = WalkEntryDistal(trail);
        if (!walkCaptureArmed || !hasWalkFoldLift)
            return false;
        HingeJoint2D hip;
        float liftX;
        float liftFloorY;
        float liftRelY;
        float hipHold;
        float l1;
        float l2;
        float parentTilt;
        if (trail)
        {
            hip = walkHalf == 0 ? rearRightHipJoint : rearLeftHipJoint;
            liftX = walkFoldLiftTrailX;
            liftFloorY = walkFoldLiftTrailY;
            liftRelY = walkFoldLiftTrailRelY;
            hipHold = walkFoldHipTrail;
            l1 = rearThighLen;
            l2 = rearShinLen;
            parentTilt = pelvisTilt;
        }
        else
        {
            hip = walkHalf == 0 ? frontLeftShoulderJoint : frontRightShoulderJoint;
            liftX = walkFoldLiftNearX;
            liftFloorY = walkFoldLiftNearY;
            liftRelY = walkFoldLiftNearRelY;
            hipHold = walkFoldHipNear;
            l1 = frontUpperLen;
            l2 = frontLowerLen;
            parentTilt = chestPitch;
        }
        if (hip == null || float.IsNaN(liftX) || float.IsNaN(liftFloorY))
            return false;
        Vector2 hipPoint = hip.transform.TransformPoint(hip.anchor);
        float y = float.IsNaN(liftRelY) ? liftFloorY : hipPoint.y + liftRelY;
        if (y < liftFloorY)
            y = liftFloorY;
        float maxY = hipPoint.y - WalkSwingIkHipGapM;
        float minY = hipPoint.y - (l1 + l2 - 0.02f);
        if (y > maxY) y = maxY;
        if (y < minY) y = minY;
        if (!SolveWalkLegIk(hipPoint, new Vector2(liftX, y), l1, l2,
                out float thighWorld, out float kneeFlex))
            return false;
        float hipIk = parentTilt - thighWorld;
        float hipDelta = Mathf.DeltaAngle(hipHold, hipIk);
        hipIk = hipHold + Mathf.Clamp(hipDelta, -WalkSwingIkHipCap, WalkSwingIkHipCap);
        float quiet = WalkEntryDistal(trail);
        float kneeLo = Mathf.Max(quiet, trail ? kneeBaseAngle : elbowBaseAngle);
        float kneeHi = Mathf.Min(WalkSwingIkKneeMax, 120f);
        float kneeIk = Mathf.Clamp(kneeFlex, kneeLo, kneeHi);
        float u = SwingPeelU();
        proximal = Mathf.LerpAngle(hipHold, hipIk, u);
        distal = Mathf.Lerp(quiet, kneeIk, u);
        return true;
    }

    // Law of cosines. Same as human SolveCaptureLegIk: chord from
    // +Y down, plus knee = flexion. Dog hip jointAngle = parent − thigh.
    private static bool SolveWalkLegIk(Vector2 hipPoint, Vector2 targetPaw,
        float l1, float l2, out float thighWorldDeg, out float kneeFlexDeg)
    {
        thighWorldDeg = 0f;
        kneeFlexDeg = 0f;
        if (l1 < 0.0001f || l2 < 0.0001f)
            return false;
        Vector2 r = targetPaw - hipPoint;
        float d = r.magnitude;
        if (d < 0.0001f)
            return false;
        d = Mathf.Clamp(d, Mathf.Abs(l1 - l2) + 0.02f, l1 + l2 - 0.01f);
        float chordDeg = Mathf.Atan2(r.x, -r.y) * Mathf.Rad2Deg;
        float cosKnee = (l1 * l1 + l2 * l2 - d * d) / (2f * l1 * l2);
        kneeFlexDeg = 180f - Mathf.Acos(Mathf.Clamp(cosKnee, -1f, 1f)) * Mathf.Rad2Deg;
        float cosAlpha = (d * d + l1 * l1 - l2 * l2) / (2f * d * l1);
        float alphaDeg = Mathf.Acos(Mathf.Clamp(cosAlpha, -1f, 1f)) * Mathf.Rad2Deg;
        thighWorldDeg = chordDeg + alphaDeg;
        return true;
    }

    // Dog analog of Gait World.collapsed / posed. Posed is the onset
    // lean clock, not CoM travel (lean tests moved chest while comX
    // stayed on the sit park). GettingUp is not a walk onset collapse.
    private void UpdateWalkWorldFlags()
    {
        if ((!walkWanted && !IsWalk())
            || currentState == StanceState.GettingUp
            || currentState == StanceState.Fallen
            || currentState == StanceState.Falling)
        {
            WalkCollapsed = false;
            WalkPosed = false;
            return;
        }
        WalkCollapsed = WalkSinkCollapsed()
                        || WalkCaptureHardDrop()
                        || WalkStancePairFolded();
        float dualAge = walkHalfBorn >= 0f ? Time.fixedTime - walkHalfBorn : 0f;
        // Same Dual t=0 clock as DualLeanBoostDeg. Waiting until
        // minDual left WalkPosed false for the whole 0.12 s DualReady
        // ceiling that Zeroed a still-flat pad (dog_w67).
        float leanU = dualAge <= 0f ? 0f : Mathf.Clamp01(dualAge / WalkOnsetLeanSeconds);
        WalkPosed = !WalkCollapsed
                    && (walkCaptureArmed
                        || leanU >= 0.85f
                        || dualAge >= WalkOnsetLeanSeconds);
    }

    // First 3 s of walk, one sample per 0.1 s bucket. No alloc.
    // hipDrop / comDrop / columnStanding / reason show which collapse
    // term is wrong when onset stays DualSupport.
    private void StepWalkOnsetLog()
    {
        if (!walkWanted && !IsWalk())
            return;
        if (currentState == StanceState.GettingUp
            || currentState == StanceState.Fallen
            || currentState == StanceState.Falling)
            return;
        if (walkOnsetBorn < 0f)
            walkOnsetBorn = Time.fixedTime;
        float age = Time.fixedTime - walkOnsetBorn;
        if (age < -0.0001f || age > 3.001f)
            return;
        int bucket = Mathf.Clamp(Mathf.FloorToInt(age * 10f + 1e-4f), 0,
            WalkOnsetLogCapacity - 1);
        if (walkOnsetWritten[bucket])
            return;
        walkOnsetWritten[bucket] = true;
        walkOnsetT[bucket] = age;
        walkOnsetComX[bucket] = comPosition.x;
        walkOnsetMargin[bucket] = supportMargin;
        walkOnsetHipDrop[bucket] = WalkHipDrop();
        walkOnsetComDrop[bucket] = WalkStandDrop();
        walkOnsetColumn[bucket] = WalkColumnStandingDrop();
        walkOnsetReason[bucket] = WalkCollapseReasonCode();
        byte flags = 0;
        if (WalkCollapsed) flags |= 1;
        if (WalkPosed) flags |= 2;
        walkOnsetFlags[bucket] = flags;
        WalkOnsetLogCount++;
    }

    public bool TryGetWalkOnsetSample(int index, out float age,
        out bool collapsed, out bool posed, out float comX, out float margin)
    {
        float hipDrop, comDrop, columnStanding;
        byte reason;
        return TryGetWalkOnsetSample(index, out age, out collapsed, out posed,
            out comX, out margin, out hipDrop, out comDrop, out columnStanding,
            out reason);
    }

    public bool TryGetWalkOnsetSample(int index, out float age,
        out bool collapsed, out bool posed, out float comX, out float margin,
        out float hipDrop, out float comDrop, out float columnStanding,
        out byte reason)
    {
        age = 0f;
        collapsed = false;
        posed = false;
        comX = 0f;
        margin = 0f;
        hipDrop = 0f;
        comDrop = 0f;
        columnStanding = 0f;
        reason = 0;
        if (index < 0 || index >= WalkOnsetLogCapacity || !walkOnsetWritten[index])
            return false;
        age = walkOnsetT[index];
        collapsed = (walkOnsetFlags[index] & 1) != 0;
        posed = (walkOnsetFlags[index] & 2) != 0;
        comX = walkOnsetComX[index];
        margin = walkOnsetMargin[index];
        hipDrop = walkOnsetHipDrop[index];
        comDrop = walkOnsetComDrop[index];
        columnStanding = walkOnsetColumn[index];
        reason = walkOnsetReason[index];
        return true;
    }

    // reason: 0 none, 1 sink(com), 2 hardDrop(hip), 4 stanceFold,
    // 8 column(spawn). OR-bits. Pitch −98° with r=2 is hip drop +
    // |pitch|>50; r=1 is settle CoM; r=8 is inverted-V column FP.
    private byte WalkCollapseReasonCode()
    {
        byte r = 0;
        if (WalkSinkCollapsed()) r |= 1;
        if (WalkCaptureHardDrop()) r |= 2;
        if (WalkStancePairFolded()) r |= 4;
        // Column is diagnostic only — not an abort term. Large while
        // settle stand is honest leftover sit vs first-frame spawn.
        if (WalkColumnStandingDrop() >= 0.15f) r |= 8;
        return r;
    }

    private float WalkHipDrop()
    {
        float hold;
        if (hasWalkPelvisHold)
            hold = walkPelvisHoldY;
        else if (standComHeightValid)
            hold = standPelvisY;
        else if (spawnColumnValid)
            hold = spawnColumnPelvisY;
        else
            return 0f;
        return Mathf.Max(0f, hold - pelvisY);
    }

    // Drop from the first-frame inverted-V column (world). Large while
    // settle stand is honest leftover sit → false WalkCollapsed was
    // comparing live sit to this column.
    private float WalkColumnStandingDrop()
    {
        if (!spawnColumnValid)
            return 0f;
        float fromPelvis = Mathf.Max(0f, spawnColumnPelvisY - pelvisY);
        float fromCom = hasCom
            ? Mathf.Max(0f, spawnColumnComY - comPosition.y)
            : 0f;
        return Mathf.Max(fromPelvis, fromCom);
    }

    // First-half stance chord: the more extended of the Walk() sit
    // and digitigrade 24°, capped at 15°. Live plant-IK to a dropping
    // pelvis is plus-fold feedback (human launch 5.3 s: 69/52 then 120).
    private float WalkQuietDistal(bool isRear)
    {
        float hold = isRear ? walkStanceKnee : walkStanceElbow;
        if (hold > WalkStanceDistalMax)
            hold = WalkStanceDistalMax;
        if (!hasWalkEntry)
            return hold;
        float entry = isRear ? walkEntryKnee : walkEntryElbow;
        return Mathf.Clamp(Mathf.Min(entry, hold), 0f, hold);
    }

    // Dual-swing hold. Leftover sit is ~24°. Capping that at 15°
    // during onset opened all four columns (DualSupport chair).
    private float WalkEntryDistal(bool isRear)
    {
        if (!hasWalkEntry)
            return isRear ? kneeBaseAngle : elbowBaseAngle;
        return isRear ? walkEntryKnee : walkEntryElbow;
    }

    private float WalkQuietProximal(bool isRear)
    {
        if (!hasWalkEntry)
            return isRear ? hipBaseAngle : shoulderBaseAngle;
        return isRear ? walkEntryHip : walkEntryShoulder;
    }

    private void SnapshotWalkEntry()
    {
        if (hasWalkEntry)
            return;
        walkEntryHip = 0.5f * (JointOr(rearLeftHipJoint, hipBaseAngle)
            + JointOr(rearRightHipJoint, hipBaseAngle));
        walkEntryShoulder = 0.5f * (JointOr(frontLeftShoulderJoint, shoulderBaseAngle)
            + JointOr(frontRightShoulderJoint, shoulderBaseAngle));
        walkEntryKnee = 0.5f * (JointOr(rearLeftKneeJoint, kneeBaseAngle)
            + JointOr(rearRightKneeJoint, kneeBaseAngle));
        walkEntryElbow = 0.5f * (JointOr(frontLeftElbowJoint, elbowBaseAngle)
            + JointOr(frontRightElbowJoint, elbowBaseAngle));
        hasWalkEntry = true;
    }

    private static float JointOr(HingeJoint2D joint, float fallback)
    {
        return joint != null ? joint.jointAngle : fallback;
    }

    // Inverted-pendulum capture point. Same x + v/omega0 as the human
    // walk gate. Height is pelvis above y = −2 dirt.
    private float CapturePointX()
    {
        float h = Mathf.Max(0.1f, pelvisY + 2f);
        float omega0 = Mathf.Sqrt(9.81f / h);
        return comPosition.x + comVelocity.x / omega0;
    }

    // How far the swing pair has travelled cranially since the leave,
    // as a fraction of the stride need. Reach blend only — not the
    // STEP gate (that is SwingReadyToComplete).
    private float WalkCaptureSwingU()
    {
        if (!walkCaptureArmed)
            return 0f;
        float need = WalkStrideNeed();
        float gone = WalkSwingGoneM();
        return Mathf.Clamp01(gone / need);
    }

    private float AlongFacingSpeed()
    {
        return comVelocity.x * FacingSign();
    }

    // Capture-point past the stance heel by `past` metres, and off
    // the swing plate. past 0.08 is the post-swap analog so the
    // second half does not wait for fallCoMOffset 0.30 from mid-four.
    // Heel-only (no plate test) is always true for the rears: mid-trunk
    // CoM sits cranial of the caudal heels in leftover sit.
    private bool CaptureOnStance(float past)
    {
        if (!TryStanceBounds(out float minX, out float maxX))
            return false;
        float dir = FacingSign();
        float x = CapturePointX();
        float pad = pawComDeadzone;
        if (x < minX - pad || x > maxX + pad)
            return false;
        float heel = dir > 0f ? minX : maxX;
        if ((x - heel) * dir < past)
            return false;
        return LeftSwingPlate(x, dir);
    }

    // Early leave of phase 0 when capture-point CoM is on the named
    // stance soles and off the swing plate. Mid-trunk between the
    // two pairs is not a plate — that was leftover sit (dog_w10).
    // Do not arm on walkRearLag alone.
    private bool WeightOnStance()
    {
        return CaptureOnStance(pawComDeadzone);
    }

    // firstCom: CoM has travelled WalkLaunchLeadM toward this half's
    // stance from the Walk() / swap launch sample. Capture-point
    // travel parked on leftover sit (v≈0, same as comX). DualReady
    // no longer Arms on this (dog_w67 leftover: 3 cm or 1 cm after
    // lean Zeroed a still-flat pad). Kept as the travel test.
    private bool WeightShiftedTowardStance()
    {
        if (!hasWalkComHold)
            return false;
        float toward = WalkLaunchToward();
        float gone = (comPosition.x - walkComHoldX) * toward;
        if (gone >= WalkLaunchLeadM)
            return true;
        float dualAge = walkHalfBorn >= 0f ? Time.fixedTime - walkHalfBorn : 0f;
        return dualAge >= WalkMinDual() + WalkOnsetLeanSeconds && gone >= 0.01f;
    }

    // Diagonal trot: both halves seek cranial. Bound half 1
    // (rears only) used −Facing so CoM rolled onto the hinds;
    // that cancelled half-0 travel and left the sit-trot skate.
    private float WalkLaunchToward()
    {
        return FacingSign();
    }

    // Dual / peel CoP: launch+lead only after the first catch. First
    // half plants under the inverted-pendulum capture point when xi
    // races ahead — a fixed 3 cm lead followed the fall to −98°.
    // After Arm, TrailCatchSlide (heel free + toe scrape) walks the
    // CoP at runSpeed without waiting for union-air — freeze-X on a
    // still-touching toe was leftover sit-trot 0.286 km/h (human
    // heelFreedPeel / catchSlide). After firstAir, LatchWalkSwing
    // relatches the sample and max(lead, v·airAge) matches cadence.
    // P-error 0 at speed is a grip, not a skate.
    private float WalkOnsetCopTargetX()
    {
        float toward = WalkLaunchToward();
        float from = hasWalkComHold ? walkComHoldX : comPosition.x;
        if (!walkSwungAir)
        {
            if (TrailCatchSlide())
            {
                float period = WalkPeriodSeconds();
                float peelAge = walkClockStart >= 0f
                    ? Time.fixedTime - walkClockStart : 0f;
                float along = Mathf.Max(0f, runSpeed)
                    * Mathf.Min(Mathf.Max(0f, peelAge), period);
                float go = Mathf.Max(WalkLaunchLeadM, along);
                float catchX = from + go * toward;
                if (walkSwapCount == 0 && hasCom)
                {
                    float xi = CapturePointX();
                    if ((xi - from) * toward > go)
                        return xi;
                }
                return catchX;
            }
            float lead = from + WalkLaunchLeadM * toward;
            // First step must catch, not follow: when xi is ahead of
            // the launch lead, plant under the capture point.
            if (walkSwapCount == 0 && hasCom)
            {
                float xi = CapturePointX();
                if ((xi - from) * toward > WalkLaunchLeadM)
                    return xi;
            }
            return lead;
        }
        float halfPeriod = WalkPeriodSeconds();
        float airAge = walkAirBorn >= 0f ? Time.fixedTime - walkAirBorn : 0f;
        float airAlong = Mathf.Max(0f, runSpeed) * Mathf.Min(Mathf.Max(0f, airAge), halfPeriod);
        float airGo = Mathf.Max(WalkLaunchLeadM, airAlong);
        return from + airGo * toward;
    }

    // Extra nose-down while dual / peel still wait for CoM to leave
    // the swing. From Dual t=0: waiting until minDual left only
    // 0.06 s of lean before the old 0.12 s DualReady ceiling Armed
    // a still-flat pad (dog_w67 leftover sit-trot). runChestLean 8°
    // is already on from DriveWalk tick 0; this 4° is the extra
    // ramp, not a pitch slam. Stays through the peel
    // (walkCaptureArmed used to zero this and dump CoM back onto
    // the swing). Caps at 4° so world chest stays inside the ±20
    // lumbar. Air is the real unload. Diving trunk: hard kill at
    // |pitch|≥20° (human CaptureLeanPhase) — 0.6·tame still leaned
    // into −98°.
    private float DualLeanBoostDeg()
    {
        if (walkSwungAir)
            return 0f;
        if (Mathf.Abs(chestPitch) >= WalkLeanKillAbsPitch)
            return 0f;
        if (walkTamePitch > 1f && Mathf.Abs(chestPitch) > walkTamePitch * 0.6f)
            return 0f;
        float dualAge = walkHalfBorn >= 0f ? Time.fixedTime - walkHalfBorn : 0f;
        if (dualAge <= 0f)
            return 0f;
        return 4f * Mathf.Clamp01(dualAge / WalkOnsetLeanSeconds);
    }

    // Onset before the 0.12 m swing-knee peel. Walk() already wrote
    // phase 0. Dual-soft hip extra-extends 4–8° caudal (same ramp
    // as stance) + plus 0.18 + Dual-t=0 lean is the pitch window.
    // Leftover-sit Dual-soft fought plus 0.18 (dog_w68). firstCom of
    // 3 cm never arrives on leftover sit; 0.30 s DualSupportMax ate
    // the 0.229 s half. Arm at minDual Zeroed a still-flat pad
    // (dog_w66 leftover sit-trot). The 0.12 s ceiling and
    // WeightShiftedTowardStance did the same (dog_w67). Arm on
    // SwingReadyToRocker (heel rose OR heel-free + toe scrape) or
    // trail air — HeelRose alone never fired on a pad that already
    // rolled onto the toe (dog_w72 leftover sit-trot). Swing hip
    // Dual-soft until that Arm — Zero on loaded hips dumped
    // (dog_w40). walkPhase 1 only on rear-swing up.
    private bool DualReadyToPeel()
    {
        float dualAge = walkHalfBorn >= 0f ? Time.fixedTime - walkHalfBorn : 0f;
        if (dualAge < WalkMinDual())
            return false;
        if (TrailPawAir())
            return true;
        if (SwingReadyToRocker(true))
            return true;
        // dog_w68: CoM travel and WalkDualSupportMax must not Arm.
        // Both Zeroed a still-flat pad (leftover sit-trot 0.286 km/h).
        if (WeightShiftedTowardStance() || dualAge >= WalkDualSupportMax)
            return false;
        return false;
    }

    // After Arm: heel free + toe still on the slab is clearance, not
    // a second weld (human heelFreedPeel). Waiting for union-air
    // parked CoP and firstAir on leftover sit (0.286 km/h).
    private bool TrailCatchSlide()
    {
        if (!walkCaptureArmed || walkSwungAir)
            return false;
        if (TrailPawAir())
            return false;
        if (!TrailPawPlanted())
            return false;
        return !SwingHeelPinned(true) && SwingToePinned(true);
    }

    private float SwingPeelU()
    {
        if (!walkCaptureArmed || walkClockStart < 0f)
            return 0f;
        float a = Time.fixedTime - walkClockStart;
        // Arm clock, not half-born. DualReady Arms on heel-rise;
        // U from a later lean-wait age was 1 on the Arm tick
        // (loaded 80° squat). Keep ramping to the 0.12 m chord.
        // Freezing U at 2.8 cm sag left distal ~24° of ~80° — the
        // 0.10 m paw stayed IsTouching (dog_w10 skate). Stance
        // HeightLift fights the sag; abort is still belly / plus-fold,
        // not this ramp.
        return a <= 0f ? 0f : Mathf.Clamp01(a / WalkSwingPeelSeconds);
    }

    private float WalkMinDual()
    {
        // Dual does not plus-fold the swing (WalkEntryDistal sit),
        // so the take is Dual-soft hip + plus 0.18 until heel-rise.
        // Tick-0 peel dumped. After Arm, 0.06 s is the floor so a
        // CCD heel flicker is not an Arm. WalkDualSupportMax 0.12 s
        // is not an Arm trigger (that Zeroed a still-flat pad).
        return Mathf.Max(0.06f, walkRearLag);
    }

    // DOG-3 diagonal. Half 0 plants FR+RL (front==right), half 1
    // plants FL+RR. Bound pairing dropped the unsupported girdle.
    private bool WalkLimbIsStance(bool front, bool right)
    {
        bool diagA = front == right;
        return walkHalf == 0 ? diagA : !diagA;
    }

    private bool SwingPairAir()
    {
        return TrailPawAir();
    }

    // Rear of the swing diagonal. Half 0 peels RR; half 1 peels RL.
    // Front swing of that diagonal shares the front X with the
    // planted front, so lift-off is this hind.
    private bool TrailPawAir()
    {
        return walkHalf == 0 ? !rearRightGrounded : !rearLeftGrounded;
    }

    private bool TrailPawPlanted()
    {
        return walkHalf == 0 ? rearRightGrounded : rearLeftGrounded;
    }

    // Front of the swing diagonal. Half 0 peels FL; half 1 peels FR.
    private bool NearSwingPawAir()
    {
        return walkHalf == 0 ? !frontLeftGrounded : !frontRightGrounded;
    }

    private bool TryTrailPawX(bool groundedOnly, out float minX, out float maxX)
    {
        minX = 0f;
        maxX = 0f;
        bool found = false;
        if (walkHalf == 0)
        {
            if (groundedOnly)
                ExpandIfGrounded(rearRightPawCol, rearRightGrounded, ref found, ref minX, ref maxX);
            else
                ExpandBounds(rearRightPawCol, ref found, ref minX, ref maxX);
        }
        else if (groundedOnly)
            ExpandIfGrounded(rearLeftPawCol, rearLeftGrounded, ref found, ref minX, ref maxX);
        else
            ExpandBounds(rearLeftPawCol, ref found, ref minX, ref maxX);
        return found;
    }

    private float TrailCaudalX()
    {
        if (!TryTrailPawX(groundedOnly: false, out float minX, out float maxX))
            return comPosition.x;
        return FacingSign() > 0f ? minX : maxX;
    }

    private bool CaptureNearingStance(float margin)
    {
        if (!TryStanceBounds(out float minX, out float maxX))
            return false;
        float dir = FacingSign();
        float heel = dir > 0f ? minX : maxX;
        return (CapturePointX() - heel) * dir >= -Mathf.Max(0f, margin);
    }

    // True when the swing pair is already air, or capture-point CoM
    // has left that pair toward the stance plate. Rear swing: cranial
    // of the hind toes (onto the fronts). Front swing: caudal of the
    // front heels (onto the hinds). The old always-cranial test asked
    // the second half to wait until CoM passed the nose — that never
    // happens in leftover sit, then rollingCap peeled loaded fronts.
    private bool LeftSwingPlate(float x, float dir)
    {
        bool swingDown = (!WalkLimbIsStance(true, true) && frontRightGrounded)
            || (!WalkLimbIsStance(true, false) && frontLeftGrounded)
            || (!WalkLimbIsStance(false, true) && rearRightGrounded)
            || (!WalkLimbIsStance(false, false) && rearLeftGrounded);
        if (!swingDown)
            return true;
        if (!TryRolePaws(wantStance: false, groundedOnly: true, out float swMin, out float swMax)
            && !TryRolePaws(wantStance: false, groundedOnly: false, out swMin, out swMax))
            return true;
        float cranial = dir > 0f ? swMax : swMin;
        float caudal = dir > 0f ? swMin : swMax;
        return (x - cranial) * dir >= pawComDeadzone
            || (x - caudal) * dir <= -pawComDeadzone;
    }

    private bool TryStanceBounds(out float minX, out float maxX)
    {
        if (TryRolePaws(wantStance: true, groundedOnly: true, out minX, out maxX))
            return true;
        return TryRolePaws(wantStance: true, groundedOnly: false, out minX, out maxX);
    }

    // Picture metronome. walkPhase is the discrete 0/1 machine — sin of
    // that is always 0, so the forage wag / pant / hop read this clock.
    private float WalkPulse()
    {
        return Mathf.Sin(walkAnimPhase * 4f * Mathf.PI);
    }

    private float WalkPulseAbs()
    {
        return Mathf.Abs(WalkPulse());
    }

    private void AdvanceWalkClock()
    {
        if (walkHalfBorn < 0f)
            walkHalfBorn = Time.fixedTime;

        float dt = Time.fixedDeltaTime;
        float period = WalkPeriodSeconds();
        walkAnimPhase += dt / period;
        if (walkAnimPhase >= 1f)
            walkAnimPhase -= Mathf.Floor(walkAnimPhase);

        if (!walkCaptureArmed)
        {
            walkPhase = 0f;
            if (!hasWalkComHold && hasCom)
            {
                // Launch CoM, not capture-point: leftover sit has v≈0,
                // so xi parked at comX and firstCom never saw travel.
                walkComHoldX = comPosition.x;
                hasWalkComHold = true;
            }

            // Short plant, then peel. Tick-0 peel dumped (03:28).
            // DualReady always-true at minDual plus Zero on loaded
            // hips dumped (dog_w40). Dual-soft hip extra-extends
            // 4–8° caudal + plus 0.18 until heel-rise is the pitch
            // window (leftover-sit Dual-soft fought plus 0.18,
            // dog_w68). Arm at minDual Zeroed a still-flat pad
            // (dog_w66 leftover sit-trot). The 0.12 s DualReady
            // ceiling and a 3 cm CoM shift did the same (dog_w67).
            // DualReady owns minDual / heel-rise / trail air only.
            // A CCD air miss before minDual stays Dual plus, not an
            // Arm. Dual lean ramps from Dual t=0 so CoM can leave
            // the trail.
            if (DualReadyToPeel())
                ArmWalkCapture();
            if (!walkCaptureArmed)
                return;
        }

        bool trailAir = TrailPawAir();
        bool trailCatch = TrailCatchSlide();
        bool trailPlanted = TrailPawPlanted();
        // Union-air OR heel-free toe scrape (human heelFreedPeel).
        // Waiting for !IsTouching parked firstAir on leftover sit.
        if (trailAir || trailCatch)
        {
            if (walkAirStreak < 8)
                walkAirStreak++;
            // Three 5 ms steps: a CCD miss is not a leave.
            if (walkAirStreak >= 3)
            {
                if (!walkSwungAir)
                    LatchWalkSwing();
                walkSwungAir = true;
            }
        }
        else
            walkAirStreak = 0;

        // Front of the swing diagonal. L/R share X with the planted
        // front, but each paw has its own sole. Trail-up used to arm
        // catchPlant on a front that never left — that skate is
        // dog_w10. Latch this paw's air separately; DriveWalkPaw
        // Zeroes it until then.
        if (NearSwingPawAir())
        {
            if (walkNearAirStreak < 8)
                walkNearAirStreak++;
            if (walkNearAirStreak >= 3)
                walkNearSwungAir = true;
        }
        else
            walkNearAirStreak = 0;

        StepSwingRockerClock();

        // Fold may be armed while the trail still touches. Swing
        // (phase 1) only on trail-up of ONE paw. Arm+phase 1 on
        // both swing knees was a loaded squat (dog_w40 / human 6.300).
        walkPhase = walkSwungAir ? 1f : 0f;

        if (!walkSwungAir && WalkCaptureShouldAbort())
        {
            // First Dual→Swing must stay armed. The old inner
            // (swapCount>0 || folded || hardDrop) was always true
            // whenever ShouldAbort fired (folded|hardDrop), so
            // AbortWalkCapture reset halfBorn and DualSupport sat
            // through the window (swap 0). Stay armed on the first
            // peel unless the trunk is a belly. Later halves still
            // abort a stance plus-fold.
            if (walkSwapCount > 0 || WalkCaptureHardDrop() || WalkSinkCollapsed())
            {
                AbortWalkCapture();
                return;
            }
        }

        // Air first so a dirt slide is not a step. Completing at
        // gone≈0 after 34 ms was the sit-trot hop. Completing at
        // 8 cm with no CoM on the new plate was a 1.3 km/h march.
        // Waiting for 0.224 m of paw travel before the hip opened
        // never swapped (dog_w50). Plant-ahead or the stride.
        if (walkSwungAir && trailPlanted && SwingReadyToComplete())
            CompleteWalkCapture();
    }

    // 0.12 m from the Walk() sit is the swing-knee chord, not a
    // belly. Abort used walkLaunchDrop 0.12, reset to dual, and
    // glued phase 0. A real squat is the stance V plus-folding
    // past that sit, or a trunk dump with pitch. Swing distal
    // going plus is the lift, not the abort. Do not reset dual
    // on the peel sag — that is the DualSupport freeze.
    private bool WalkCaptureShouldAbort()
    {
        if (WalkStancePairFolded())
            return true;
        return WalkSinkCollapsed() || WalkCaptureHardDrop();
    }

    private bool WalkCaptureHardDrop()
    {
        float hold;
        if (hasWalkPelvisHold)
            hold = walkPelvisHoldY;
        else if (standComHeightValid)
            hold = standPelvisY;
        else
            hold = pelvisY;
        float drop = Mathf.Max(0f, hold - pelvisY);
        if (drop > 0.40f)
            return true;
        return drop > 0.28f && Mathf.Abs(chestPitch) > 50f;
    }

    // Measured quiet-sit CoM, not the inverted-V spawn column.
    // Geometric chest-column / spawn pelvis sits ~0.19–0.28 m above
    // leftover sit — that is sinkAbortDrop 0.25 on a plant, the
    // human SinkCollapsed false-positive. Unset bar is not collapsed.
    private bool WalkSinkCollapsed()
    {
        if (sinkAbortDrop <= 0.001f || !hasCom || !standComHeightValid)
            return false;
        // Heap is below any standing bar by definition. Do not arm
        // WalkCollapsed / abort while GettingUp or Fallen — that froze
        // human TickGetUp (BodyCollapsed) for 32 s after the fall.
        if (currentState == StanceState.GettingUp
            || currentState == StanceState.Fallen
            || currentState == StanceState.Falling)
            return false;
        return standComHeight - comPosition.y >= sinkAbortDrop;
    }

    private float WalkStandDrop()
    {
        if (standComHeightValid && hasCom)
            return Mathf.Max(0f, standComHeight - comPosition.y);
        return 0f;
    }

    // Once, after the rake window and vertical settle, four paws down.
    // Live CoM only, world space. Do not Max with StandingRootY / bone
    // column — that heap is the quiet sit reading as collapse (human
    // CaptureStandHeight never Max; settle sensors only).
    private void TryCaptureSpawnStandHeight()
    {
        if (standComHeightValid || !hasCom)
            return;
        if (!AllPawsGrounded())
            return;
        if (spawnedFixedTime >= 0f)
        {
            // Rake 0.25 s then leftover sit (~trunkDrop 0.193). Mid-slam
            // capture arms the inverted-V column → onset collapsed 99 %.
            float wait = Mathf.Max(0.70f, startupHipSeconds + 0.45f);
            if (Time.fixedTime - spawnedFixedTime < wait)
                return;
        }
        if (Mathf.Abs(pelvisVelY) > 0.08f)
            return;
        if (Mathf.Abs(comVelocity.y) > 0.12f)
            return;
        // Pitch settled like human TiltSettled — not mid-rake chest.
        if (Mathf.Abs(chestPitch - chestTargetTilt) > 14f)
            return;
        standComHeight = comPosition.y;
        standPelvisY = pelvisY;
        standComHeightValid = true;
    }

    // Quiet plant still reading a column-sized comDrop: retarget the
    // settle bar to live world CoM (human RetargetStandComIfColumn).
    // GettingUp may retarget too — otherwise a pre-fall column bar
    // blocks GetUpIsStanding until the run ends.
    private void MaybeRetargetStandFromSettledSit()
    {
        if (!standComHeightValid || !hasCom)
            return;
        if (!AllPawsGrounded())
            return;
        if (IsWalk()
            || currentState == StanceState.Fallen
            || currentState == StanceState.Falling
            || IsLeap())
            return;
        if (Mathf.Abs(comVelocity.x) > 0.20f || Mathf.Abs(comVelocity.y) > 0.12f)
            return;
        if (Mathf.Abs(pelvisVelY) > 0.08f)
            return;
        float pitchErr = Mathf.Abs(chestPitch - chestTargetTilt);
        if (pitchErr > 14f)
            return;
        float comDrop = standComHeight - comPosition.y;
        float column = WalkColumnStandingDrop();
        // Reference still looks like the spawn column vs leftover sit.
        if (comDrop < sinkAbortDrop * 0.40f && column < 0.10f)
            return;
        if (comDrop < 0.05f)
            return;
        standComHeight = comPosition.y;
        standPelvisY = pelvisY;
    }

    private void LatchWalkPelvisHold()
    {
        if (hasWalkPelvisHold || !standComHeightValid)
            return;
        walkPelvisHoldY = standPelvisY;
        hasWalkPelvisHold = true;
    }

    // Leftover sit distal is ~24°. The Walk() chord is 15°. Abort
    // used Min(hold+8, 19) so 24 > 19 fired on the sit itself,
    // DualTakeFinished armed, AbortWalkCapture reset dual, and
    // phase 0 froze (same hole as human StandingPlantReady).
    // Squat is plus-fold past the sit we started from, not past
    // the 15° we are opening toward. Stance command stays ≤15°.
    private bool WalkStancePairFolded()
    {
        const float slack = 8f;
        float elEntry = hasWalkEntry ? walkEntryElbow : elbowBaseAngle;
        float knEntry = hasWalkEntry ? walkEntryKnee : kneeBaseAngle;
        if (WalkLimbIsStance(true, true)
            && JointOr(frontRightElbowJoint, elbowBaseAngle) > elEntry + slack)
            return true;
        if (WalkLimbIsStance(true, false)
            && JointOr(frontLeftElbowJoint, elbowBaseAngle) > elEntry + slack)
            return true;
        if (WalkLimbIsStance(false, true)
            && JointOr(rearRightKneeJoint, kneeBaseAngle) > knEntry + slack)
            return true;
        if (WalkLimbIsStance(false, false)
            && JointOr(rearLeftKneeJoint, kneeBaseAngle) > knEntry + slack)
            return true;
        return false;
    }

    private void AbortWalkCapture()
    {
        walkCaptureArmed = false;
        walkSwungAir = false;
        walkNearSwungAir = false;
        walkAirBorn = -1f;
        walkAirStreak = 0;
        walkNearAirStreak = 0;
        walkTrailRockerBorn = -1f;
        walkNearRockerBorn = -1f;
        walkClockStart = -1f;
        walkPhase = 0f;
        walkHalfBorn = Time.fixedTime;
        hasWalkLatch = false;
        hasWalkFold = false;
        hasWalkFoldLift = false;
        walkFoldLiftTrailX = float.NaN;
        walkFoldLiftTrailY = float.NaN;
        walkFoldLiftTrailRelY = float.NaN;
        walkFoldLiftNearX = float.NaN;
        walkFoldLiftNearY = float.NaN;
        walkFoldLiftNearRelY = float.NaN;
        hasWalkComHold = false;
    }

    private void ArmWalkCapture()
    {
        // Launch the 0.12 m trail-knee ramp this tick. walkPhase
        // stays 0 until that paw is up (human Swing on trail-up).
        // Stance distal stays on the Walk() chord.
        walkCaptureArmed = true;
        walkSwungAir = false;
        walkNearSwungAir = false;
        walkAirBorn = -1f;
        walkAirStreak = 0;
        walkNearAirStreak = 0;
        // Sit-relative +18° ON from this tick (U=1). Inverted-V
        // 6 cm was ~63° (dog_w58). Waiting for 3 mm welded the
        // pad (dog_w63). Arm-tick U=0 left leftover sit while
        // stance opened 6°. Caudal hip 10° skated that pad.
        walkTrailRockerBorn = Time.fixedTime;
        walkNearRockerBorn = -1f;
        hasWalkLatch = false;
        hasWalkFold = false;
        LatchWalkSwingFold();
        walkClockStart = Time.fixedTime;
        walkSwingCaudalX = TrailCaudalX();
        walkPhase = 0f;
    }

    private void CompleteWalkCapture()
    {
        walkCaptureArmed = false;
        walkSwungAir = false;
        walkNearSwungAir = false;
        walkAirBorn = -1f;
        walkAirStreak = 0;
        walkNearAirStreak = 0;
        walkTrailRockerBorn = -1f;
        walkNearRockerBorn = -1f;
        hasWalkLatch = false;
        hasWalkFold = false;
        hasWalkFoldLift = false;
        walkFoldLiftTrailX = float.NaN;
        walkFoldLiftTrailY = float.NaN;
        walkFoldLiftTrailRelY = float.NaN;
        walkFoldLiftNearX = float.NaN;
        walkFoldLiftNearY = float.NaN;
        walkFoldLiftNearRelY = float.NaN;
        walkClockStart = -1f;
        walkHalf = walkHalf == 0 ? 1 : 0;
        walkHalfBorn = Time.fixedTime;
        walkSwapCount++;
        walkCycleCount++;
        walkPhase = 0f;
        hasWalkComHold = false;
    }

    private void LatchWalkSwing()
    {
        if (hasWalkLatch)
            return;
        if (walkHalf == 0)
        {
            walkLatchHip = JointOr(rearRightHipJoint, hipBaseAngle);
            walkLatchKnee = JointOr(rearRightKneeJoint, kneeBaseAngle);
            walkLatchShoulder = JointOr(frontLeftShoulderJoint, shoulderBaseAngle);
            walkLatchElbow = JointOr(frontLeftElbowJoint, elbowBaseAngle);
        }
        else
        {
            walkLatchHip = JointOr(rearLeftHipJoint, hipBaseAngle);
            walkLatchKnee = JointOr(rearLeftKneeJoint, kneeBaseAngle);
            walkLatchShoulder = JointOr(frontRightShoulderJoint, shoulderBaseAngle);
            walkLatchElbow = JointOr(frontRightElbowJoint, elbowBaseAngle);
        }
        hasWalkLatch = true;
        walkAirBorn = Time.fixedTime;
        // Reach is from this leave, not from ArmWalkCapture: the
        // caudal tuck before lift would make gone negative and
        // never swap (dog_w10 skate). Relatch CoP here so the
        // first air tick is not dualAge·runSpeed (~20 cm) of
        // P-error on a just-unloaded plate.
        walkSwingCaudalX = TrailCaudalX();
        if (hasCom)
        {
            walkComHoldX = comPosition.x;
            hasWalkComHold = true;
        }
    }

    // Clock blend after true air. Not WalkCaptureSwingU — that
    // fraction is zero until the paw has already travelled, so a
    // lerp to flexHip never left the sit X (dog_w10 skate).
    private float WalkAirBlend()
    {
        if (walkAirBorn < 0f)
            return walkSwungAir ? 1f : 0f;
        return Mathf.Clamp01((Time.fixedTime - walkAirBorn) / WalkAirReachSeconds);
    }

    private float SwingPlantNeed()
    {
        // Reach-blend length and STEP gate. Reuses -dogWalk
        // swing-lift frac, no new switch. 0.45·stride was 0.144 m
        // (2.3 km/h); Walk() floors frac at 0.70.
        return Mathf.Max(pawComDeadzone,
            Mathf.Clamp(walkSwingLiftFrac, 0.15f, 0.80f) * Mathf.Max(0.08f, runStride));
    }

    // Anti-hop floor plus the cadence stride. Completing at 8 cm
    // made a 0.35 m/s march (sit-trot order) even when the paw left.
    private float WalkStrideNeed()
    {
        return Mathf.Max(WalkPlantAheadMinM, SwingPlantNeed());
    }

    // Distal open after the hip has reached, not after gone and
    // not on the first air tick (that planted at sit X).
    private float WalkSwingOpenU(float reach)
    {
        if (!walkSwungAir && !walkNearSwungAir)
            return 0f;
        float u = WalkAirBlend();
        if (u <= WalkAirOpenStart)
            return 0f;
        float span = Mathf.Max(0.05f, 1f - WalkAirOpenStart);
        float open = (u - WalkAirOpenStart) / span;
        return Mathf.Max(Mathf.Clamp01(reach), Mathf.Clamp01(open));
    }

    private float WalkSwingGoneM()
    {
        return (TrailCaudalX() - walkSwingCaudalX) * FacingSign();
    }

    // dog_ds2 (programmer). Descent clock of the swing: 0 at the reach peak
    // (WalkAirReachSeconds after the leave), 1 when the plant angle must be
    // reached. Reach + descent fill one half of WalkPeriodSeconds, so the
    // pad comes down inside the cadence the clock already asks for.
    private float WalkAirPlantU()
    {
        if (walkAirBorn < 0f)
            return 0f;
        float age = Time.fixedTime - walkAirBorn;
        float span = Mathf.Max(WalkAirReachSeconds, 0.5f * WalkPeriodSeconds());
        return Mathf.Clamp01((age - WalkAirReachSeconds) / span);
    }

    // Catch place: capture-point sits on the swing plate, or the
    // swing heel has reached CoM (fronts planting ahead). A 2 cm
    // sit wiggle is not a step. Full 0.224 m of paw travel is the
    // 5 km/h stride when the trunk has already moved; requiring
    // that before CoM can arrive on a closer plant froze swap 0.
    private bool SwingPlantedAhead()
    {
        if (!TryTrailPawX(groundedOnly: true, out float minX, out float maxX))
            return false;
        float dir = FacingSign();
        float xcom = CapturePointX();
        float heel = dir > 0f ? minX : maxX;
        float gone = (heel - walkSwingCaudalX) * dir;
        if (gone <= WalkPlantAheadMinM)
            return false;
        if (gone >= WalkStrideNeed())
            return true;
        bool onPlate = xcom >= minX && xcom <= maxX;
        bool heelAtCom = (heel - xcom) * dir >= 0f;
        return onPlate || heelAtCom;
    }

    // True air, then a real stride. Clocked catch at 0.15·half
    // (~34 ms) with gone≈0 was the sit-trot hop (dog_w10 0.286 km/h).
    // Completing at WalkPlantAheadMinM 8 cm was a 1.3 km/h march.
    // Stance pair must still be down so a four-air hop is not a
    // stride. Heel must not have slipped caudal of the air snapshot.
    private bool SwingReadyToComplete()
    {
        if (walkAirBorn < 0f)
            return false;
        if (Time.fixedTime - walkAirBorn < WalkAirReachSeconds)
            return false;
        bool stanceDown = (WalkLimbIsStance(true, true) && frontRightGrounded)
            || (WalkLimbIsStance(true, false) && frontLeftGrounded)
            || (WalkLimbIsStance(false, true) && rearRightGrounded)
            || (WalkLimbIsStance(false, false) && rearLeftGrounded);
        if (!stanceDown)
            return false;
        if (SwingPlantedAhead())
            return true;
        if (!TryTrailPawX(groundedOnly: true, out float minX, out float maxX))
            return false;
        float dir = FacingSign();
        float heel = dir > 0f ? minX : maxX;
        float gone = (heel - walkSwingCaudalX) * dir;
        if (gone >= WalkStrideNeed())
            return true;
        // dog_ds2 (programmer). The stride branch above was the only exit,
        // so the first swing of a dog that is still slow could never finish:
        // dog_ds1 sat armed for 23 s with walkCycleCount 0 while the pad hung
        // 0.26 m over the slab. A swing that has finished its descent and has
        // come down at (or within a paw deadzone of) its take-off is a step:
        // complete the half and let the new stance pair push the CoM. The
        // stride branch still owns the fast case, and the pad must be down
        // (walkSwungAir && trailPlanted gate the caller).
        return WalkAirPlantU() >= 1f && gone > -pawComDeadzone;
    }

    private float PairCaudalX(bool rear)
    {
        if (!TryPairX(rear, groundedOnly: false, out float minX, out float maxX))
            return comPosition.x;
        return FacingSign() > 0f ? minX : maxX;
    }

    private bool TryPairX(bool rear, bool groundedOnly, out float minX, out float maxX)
    {
        minX = 0f;
        maxX = 0f;
        bool found = false;
        if (rear)
        {
            if (groundedOnly)
            {
                ExpandIfGrounded(rearRightPawCol, rearRightGrounded, ref found, ref minX, ref maxX);
                ExpandIfGrounded(rearLeftPawCol, rearLeftGrounded, ref found, ref minX, ref maxX);
            }
            else
            {
                ExpandBounds(rearRightPawCol, ref found, ref minX, ref maxX);
                ExpandBounds(rearLeftPawCol, ref found, ref minX, ref maxX);
            }
        }
        else
        {
            if (groundedOnly)
            {
                ExpandIfGrounded(frontRightPawCol, frontRightGrounded, ref found, ref minX, ref maxX);
                ExpandIfGrounded(frontLeftPawCol, frontLeftGrounded, ref found, ref minX, ref maxX);
            }
            else
            {
                ExpandBounds(frontRightPawCol, ref found, ref minX, ref maxX);
                ExpandBounds(frontLeftPawCol, ref found, ref minX, ref maxX);
            }
        }
        return found;
    }

    private bool TryRolePaws(bool wantStance, bool groundedOnly, out float minX, out float maxX)
    {
        minX = 0f;
        maxX = 0f;
        bool found = false;
        AddRolePaw(true, true, frontRightPawCol, frontRightGrounded, wantStance, groundedOnly,
            ref found, ref minX, ref maxX);
        AddRolePaw(true, false, frontLeftPawCol, frontLeftGrounded, wantStance, groundedOnly,
            ref found, ref minX, ref maxX);
        AddRolePaw(false, true, rearRightPawCol, rearRightGrounded, wantStance, groundedOnly,
            ref found, ref minX, ref maxX);
        AddRolePaw(false, false, rearLeftPawCol, rearLeftGrounded, wantStance, groundedOnly,
            ref found, ref minX, ref maxX);
        return found;
    }

    private void AddRolePaw(bool front, bool right, Collider2D col, bool grounded,
        bool wantStance, bool groundedOnly, ref bool found, ref float minX, ref float maxX)
    {
        if (WalkLimbIsStance(front, right) != wantStance)
            return;
        if (groundedOnly)
            ExpandIfGrounded(col, grounded, ref found, ref minX, ref maxX);
        else
            ExpandBounds(col, ref found, ref minX, ref maxX);
    }

    private void WalkLimbTargets(bool stanceCol, bool peelCol, bool isRear, float swingU, bool pawGrounded,
                                 bool nearColumn, HingeJoint2D proximalJoint,
                                 out float proximal, out float distal)
    {
        // swingU is only the reach blend after lift-off, not a lift-wait.
        float reach = (stanceCol || !peelCol) ? 0f : Mathf.Clamp01(swingU);

        float dir = FacingSign();
        float hipFlex = GaitAmp(walkHipFlex, runHipFlex);
        float shFlex = GaitAmp(walkShoulderFlex, runShoulderFlex);
        // Digitigrade leftover sit is ~0–24°. Swing peels the shank
        // ~8 cm off that chord (latched); stance stays the Walk()
        // chord ≤15° (propulsion extra-extend is the roll — a 12°
        // extHip after 15 ms of air was the flicker dump). Cap run
        // extend still lives in Walk(): 14° planted was the w11
        // pitch dump. L/R share X: a bound must not scissor ±hipFlex
        // (that sit-trot wiggle). DOG-3 still wants four distinct
        // joint targets: front/rear role already splits the girdles;
        // a few degrees of near/far bias is not a support split.
        // After Arm, both swing limbs of the diagonal take the 0.12 m
        // chord; stance FR+RL (or FL+RR) hold leftover sit so the
        // trunk keeps a front X and a rear X.
        float columnDeg = nearColumn ? -3f : 3f;
        float quietProx = WalkQuietProximal(isRear);
        float quietDist = WalkQuietDistal(isRear);
        float airU = WalkAirBlend();
        if (walkCaptureArmed && !stanceCol && !peelCol)
        {
            proximal = quietProx + columnDeg;
            distal = WalkEntryDistal(isRear) + (nearColumn ? 3f : -3f);
            distal = Mathf.Clamp(distal, 0f, 120f);
            proximal = Mathf.Clamp(proximal, -90f, 90f);
            return;
        }
        // Dual onset: stance already uses quietDist ≤15°. Swing stays
        // the leftover sit so the 0.12 s lean is not a four-leg squat.
        if (!walkCaptureArmed && !stanceCol)
        {
            proximal = quietProx + columnDeg;
            distal = WalkEntryDistal(isRear) + (nearColumn ? 3f : -3f);
            distal = Mathf.Clamp(distal, 0f, 120f);
            proximal = Mathf.Clamp(proximal, -90f, 90f);
            return;
        }
        if (isRear)
        {
            // Plus = caudal (lower end −X). Touching peel is hip
            // hold plus rocker knee, not 12 cm IK on a pin. Air
            // goes minus (cranial reach).
            float flexHip = hipBaseAngle - Mathf.Max(hipFlex, WalkAirHipFlexMin) * dir;
            if (stanceCol)
            {
                proximal = quietProx;
                distal = quietDist;
            }
            else if (!walkSwungAir)
            {
                // Hip HOLDS leftover sit + sit-relative +18° ON
                // from Arm. Trail paw Zeroes (plus 0.18 after Arm
                // cannot pitch a loaded pad). Caudal 10° skated
                // the planted pad (dog_w64). Arm-tick U=0 jacked
                // leftover sit while stance opened 6°. dog_w62
                // cranial was swing-flex on a pin. Inverted-V 6 cm
                // was ~63° (dog_w58). Extra plus-knee shortened
                // the column (dog_w61). Dual-soft hip+knee dumped
                // (dog_w60). Front stays a plant.
                proximal = quietProx + WalkHeelPinHipCaudalDeg * dir;
                distal = SwingDirtDistal(true);
            }
            else if (pawGrounded && SwingPlantedAhead())
            {
                // Real catch at WalkStrideNeed. A 5 ms flicker
                // during the reach must not freeze the leave fold,
                // and a short plant must not IK back to the sit X
                // (that hop was dog_w10).
                proximal = hasWalkLatch ? walkLatchHip
                    : (proximalJoint != null ? proximalJoint.jointAngle : quietProx);
                distal = hasWalkLatch ? walkLatchKnee : quietDist;
            }
            else
            {
                // True air: cranial hip, hold the latched 6 cm fold,
                // then open to plant. 12 cm IK with hip gap 2 cm was
                // a chair on leftover sit (hip–paw ~14 cm).
                float fromHip = hasWalkLatch ? walkLatchHip
                    : (proximalJoint != null ? proximalJoint.jointAngle : quietProx);
                float reachHip = Mathf.Lerp(fromHip, flexHip, Mathf.Max(airU, 0.35f));
                // dog_ds2: the reach alone froze the half (the pad hung
                // 0.26 m up and never replanted). Come back down to the
                // plant angle after the reach peak.
                float plantHip = quietProx - WalkAirPlantLeadDeg * dir;
                proximal = Mathf.Lerp(reachHip, plantHip, WalkAirPlantU());
                distal = WalkSwingDistal(true, pawGrounded, reach);
            }
            proximal += columnDeg;
        }
        else
        {
            // Same sign as the hip: plus = caudal tuck, minus =
            // cranial plant. Old reachSh added shFlex (caudal) so
            // the front swing never planted ahead of CoM.
            float reachSh = shoulderBaseAngle - Mathf.Max(shFlex, WalkAirShoulderFlexMin) * dir;
            if (stanceCol)
            {
                proximal = quietProx;
                distal = quietDist;
            }
            else if (!walkNearSwungAir)
            {
                // Sit until the hind is air; then the same 6 cm.
                // Folding with the trail sat the chest (shared X).
                proximal = quietProx;
                distal = SwingDirtDistal(false);
            }
            else if (pawGrounded && SwingPlantedAhead())
            {
                // Front shares X with the planted front. Freeze
                // only on a real stride catch, not the first touch
                // after the air clock (that skate is dog_w10).
                proximal = hasWalkLatch ? walkLatchShoulder
                    : (proximalJoint != null ? proximalJoint.jointAngle : quietProx);
                distal = hasWalkLatch ? walkLatchElbow : quietDist;
            }
            else
            {
                float fromSh = hasWalkLatch ? walkLatchShoulder
                    : (proximalJoint != null ? proximalJoint.jointAngle : quietProx);
                float reachShNow = Mathf.Lerp(fromSh, reachSh, Mathf.Max(airU, 0.35f));
                // Same descent as the rear: hold the reach is a frozen
                // half, the pad must come back down onto the dirt.
                float plantShNow = quietProx - WalkAirPlantLeadDeg * dir;
                proximal = Mathf.Lerp(reachShNow, plantShNow, WalkAirPlantU());
                distal = WalkSwingDistal(false, pawGrounded, reach);
            }
            proximal += columnDeg;
        }

        if (!stanceCol)
            distal += nearColumn ? 3f : -3f;

        distal = Mathf.Clamp(distal, 0f, 120f);
        proximal = Mathf.Clamp(proximal, -90f, 90f);
    }

    // Front vs rear would be a bound. DOG-3 is the diagonal:
    // walkHalf 0 plants FR+RL, peels FL+RR. Walk() onset is phase 0
    // only. Dual plus-paws the trail with Dual-soft hip until
    // heel-rise, then Arms (no spawn HeightLift). Dual extra-extends
    // that trail hip 4–8° (same ramp as stance) so Dual-soft rolls
    // onto the toe (dog_w69). Arm at minDual Zeroed a still-flat pad
    // (dog_w66 leftover sit-trot). 0.30 s DualSupportMax ate the
    // 0.229 s half. Dual: trail hip Dual-soft, trail knee leftover
    // sit at full P. After Arm: trail hip HOLDS leftover sit,
    // sit-relative +18° ON from Arm, trail paw Zero. Dual plus-paws
    // 0.18 on the take. Plus 0.18 after Arm cannot pitch a loaded
    // pad (human ankle). Caudal 10° after Arm skated the pad.
    // Arm-tick U=0 jacked leftover sit. Inverted-V 6 cm was ~63°
    // (dog_w58). 3 mm never came on a welded pad (dog_w63). dog_w62
    // more-minus was swing-flex on a pin. Extra plus-knee shortened
    // the column (dog_w61). Dual-soft hip+knee was a limp squat
    // (dog_w60). Front swing stays a third plant until that hind
    // is air.
    // True air holds that fold and plants cranially — 12 cm IK
    // sat the leftover-sit pelvis. First peel stays armed unless
    // the trunk is a belly. Stance distal stays ≤15° and keeps
    // Dual extra-extend through the peel. Dual/peel seek
    // launch+lead; CoP walks at runSpeed only after the trail is
    // air. Air distal stays folded until the hip has reached, then
    // opens to plant ahead. Swap is that plant, not a hip slide.
    // Front-stance lean through peel. No AddForce.
    private void DriveWalk()
    {
        activationSpeedThisStep = walkActivationSpeed > 0f ? walkActivationSpeed : muscleActivationSpeed;

        float swingU = walkCaptureArmed ? WalkCaptureSwingU() : 0f;

        bool frStance = WalkLimbIsStance(true, true);
        bool flStance = WalkLimbIsStance(true, false);
        bool rlStance = WalkLimbIsStance(false, false);
        bool rrStance = WalkLimbIsStance(false, true);
        bool frPeel = !frStance;
        bool flPeel = !flStance;
        bool rlPeel = !rlStance;
        bool rrPeel = !rrStance;
        bool frRole = frStance;
        bool flRole = flStance;
        bool rlRole = rlStance;
        bool rrRole = rrStance;
        // Real sole. Until phase 1 stanceCol is true so this is unused.
        // After leave a still-down swing shortens the stifle/elbow
        // (swing knee first) so the paw lifts instead of skating.
        bool frGroundPose = frontRightGrounded;
        bool flGroundPose = frontLeftGrounded;
        bool rlGroundPose = rearLeftGrounded;
        bool rrGroundPose = rearRightGrounded;

        WalkLimbTargets(frStance, frPeel, false, frPeel ? swingU : 0f, frGroundPose, true,
            frontRightShoulderJoint, out float frSh, out float frEl);
        WalkLimbTargets(flStance, flPeel, false, flPeel ? swingU : 0f, flGroundPose, false,
            frontLeftShoulderJoint, out float flSh, out float flEl);
        WalkLimbTargets(rlStance, rlPeel, true, rlPeel ? swingU : 0f, rlGroundPose, false,
            rearLeftHipJoint, out float rlHip, out float rlKn);
        WalkLimbTargets(rrStance, rrPeel, true, rrPeel ? swingU : 0f, rrGroundPose, true,
            rearRightHipJoint, out float rrHip, out float rrKn);

        // Dual: no HeightLift. Spawn targetPelvisY is the standing
        // spawn; leftover sit is already ~0.19 m down, so lift
        // saturated at 22° and StanceDistalHold drove the plate to
        // 0° — taller stance, weight on the swing, peel dumped.
        // Armed: lift from walkPelvisHoldY. 3 cm sag opens the
        // stance V all the way to 0° (still ≤15° fold). Peel also
        // opens a floor so the girdle holds while the trail
        // shortens — waiting for that sag sat the pelvis.
        float lift = walkCaptureArmed ? WalkStanceHeightLiftDeg() : 0f;
        if (walkCaptureArmed && !walkSwungAir)
            lift = Mathf.Max(lift, WalkPeelStanceLiftDeg);
        float kneeHold = !walkSwungAir ? WalkQuietDistal(true) : walkStanceKnee;
        float elbowHold = !walkSwungAir ? WalkQuietDistal(false) : walkStanceElbow;
        if (walkCaptureArmed)
        {
            if (frRole)
                frEl = StanceDistalHold(frEl - lift, elbowHold);
            if (flRole)
                flEl = StanceDistalHold(flEl - lift, elbowHold);
            if (rlRole)
                rlKn = StanceDistalHold(rlKn - lift, kneeHold);
            if (rrRole)
                rrKn = StanceDistalHold(rrKn - lift, kneeHold);
        }

        // dog_w1 hopped: plant 0.80 + 48° swing on a sit left the dirt.
        // Scale the gait down when the chest pitches, and do not thrust
        // from fewer than two paws (that is a jump, not a step).
        float tame = 1f;
        if (walkTamePitch > 1f)
        {
            float absPitch = Mathf.Abs(chestPitch);
            tame = absPitch <= walkTamePitch
                ? 1f
                : Mathf.Clamp01(Mathf.InverseLerp(walkTamePitch + 20f, walkTamePitch, absPitch));
        }

        ApplyWalkPropulsion(tame, rlStance, rrStance, flStance, frStance,
            ref rlHip, ref rrHip, ref flSh, ref frSh);
        // Dual onset lean is extra-extend + chest, not a swing fold.

        int groundedN = (frontRightGrounded ? 1 : 0) + (frontLeftGrounded ? 1 : 0)
                        + (rearRightGrounded ? 1 : 0) + (rearLeftGrounded ? 1 : 0);
        // Stance-role plant from the dual take. Waiting for
        // walkSwungAir zeroed the only push, so CoM never left
        // the mid-paws (dog_w10 0.286 km/h). Dual/peel seek
        // launch+lead; after air the CoP walks at runSpeed.
        // Walking it from halfBorn while the trail was down was
        // the treadmill. Dual plus-paws the trail (0.18) with Dual-
        // soft hip extra-extending 4–8° caudal until heel-rise.
        // Dual lean ramps from Dual t=0. DualReady Arms only on
        // heel-rise or trail air (dog_w68). Leftover-sit Dual-soft
        // fought plus 0.18 (dog_w69). After Arm the trail paw Zeroes
        // — plus 0.18 cannot pitch a loaded pad (human ankle). Arm
        // at minDual Zeroed a still-flat pad (dog_w66 leftover
        // sit-trot). Dual-soft hip is Dual-only / trail-only. After
        // Arm the trail hip HOLDS leftover sit; the knee is sit-
        // relative +18° ON from Arm. Caudal 10° after Arm skated
        // the pad (dog_w64). Arm-tick U=0 jacked leftover sit.
        // dog_w62 more-minus was swing-flex on a pin. Extra plus-knee
        // shortened the column (dog_w61). Dual-soft hip+knee dumped
        // the pelvis (dog_w60). Front swing stays a grip plant until
        // the hind is air. Plus 0.55 was a loaded squat. Front
        // scale waits until after air so the first take still grips.
        float rawPlant = 0f;
        if (groundedN >= 2)
            rawPlant = WalkStancePlantCommand(tame);
        float frontScale = (!walkCaptureArmed || !walkSwungAir)
            ? 1f
            : Mathf.Clamp01(walkFrontPlantScale);
        float frCmd = WalkPawCommand(frRole, frontRightGrounded, rawPlant * frontScale, 0f);
        float flCmd = WalkPawCommand(flRole, frontLeftGrounded, rawPlant * frontScale, 0f);
        float rrCmd = WalkPawCommand(rrRole, rearRightGrounded, rawPlant, 0f);
        float rlCmd = WalkPawCommand(rlRole, rearLeftGrounded, rawPlant, 0f);
        // Front swing is a third plant until the hind is air.
        // WalkPawCommand would Zero it; a limp twin dumps the chest.
        if (!walkSwungAir)
        {
            if (!frRole && frontRightGrounded)
                frCmd = rawPlant * frontScale * 0.5f;
            if (!flRole && frontLeftGrounded)
                flCmd = rawPlant * frontScale * 0.5f;
        }

        DriveWalkPaw(frRole, frontRightPawJoint, frontRightPawFlexor, frontRightPawExtensor,
            frontRightGrounded, frCmd, walkNearSwungAir, trailSwing: false);
        DriveWalkPaw(flRole, frontLeftPawJoint, frontLeftPawFlexor, frontLeftPawExtensor,
            frontLeftGrounded, flCmd, walkNearSwungAir, trailSwing: false);
        DriveWalkPaw(rrRole, rearRightPawJoint, rearRightPawFlexor, rearRightPawExtensor,
            rearRightGrounded, rrCmd, walkSwungAir, trailSwing: walkHalf == 0);
        DriveWalkPaw(rlRole, rearLeftPawJoint, rearLeftPawFlexor, rearLeftPawExtensor,
            rearLeftGrounded, rlCmd, walkSwungAir, trailSwing: walkHalf != 0);

        float hipP = walkHipP > 0f ? walkHipP : hipPGain;
        float kneeP = walkKneeP > 0f ? walkKneeP : kneePGain;

        DriveWalkHip(rlRole, rlPeel, walkHalf != 0, rearLeftHipJoint, rlHip, hipP,
            rearLeftHipFlexor, rearLeftHipExtensor, ref rearLeftHipInertia);
        DriveWalkHip(rrRole, rrPeel, walkHalf == 0, rearRightHipJoint, rrHip, hipP,
            rearRightHipFlexor, rearRightHipExtensor, ref rearRightHipInertia);
        DriveWalkHip(flRole, flPeel, false, frontLeftShoulderJoint, flSh, hipP,
            frontLeftShoulderFlexor, frontLeftShoulderExtensor, ref frontLeftShoulderInertia);
        DriveWalkHip(frRole, frPeel, false, frontRightShoulderJoint, frSh, hipP,
            frontRightShoulderFlexor, frontRightShoulderExtensor, ref frontRightShoulderInertia);

        float savedLumbarP = lumbarPGain;
        if (walkLumbarP > 0f)
            lumbarPGain = walkLumbarP;
        float chestGoal = walkChestTilt;
        if (runSpeed > 0.05f)
        {
            // Dual must move CoM. along/ref cancelled the lean on
            // leftover sit (0.079 m/s), then DualReady always-true
            // armed a loaded peel and abort reset phase 0.
            // Armed-still-down 0.5× dumped CoM back onto the swing.
            // Full half-lean from Walk(); DualLeanBoostDeg grows it
            // through the 0.12 s onset lean. Scale by tame: plant
            // alone went soft while lean stayed full and followed
            // the fall to −98°. Hard kill at |pitch|≥20° (human
            // CaptureLeanPhase). Paw seek is a CoP that walks at
            // runSpeed (stance-mid PawPd is P-error 0 on leftover
            // sit; a facing leap plant after Arm slid).
            float halfLean = WalkStanceLeanK() * tame;
            if (Mathf.Abs(chestPitch) >= WalkLeanKillAbsPitch)
                halfLean = 0f;
            chestGoal -= (runChestLean + DualLeanBoostDeg()) * FacingSign() * halfLean;
        }
        DriveLumbarToWorld(chestGoal);
        lumbarPGain = savedLumbarP;

        DriveWalkKnee(rlRole, true, walkHalf != 0, rearLeftKneeJoint, rlKn, kneeP,
            rearLeftKneeFlexor, rearLeftKneeExtensor, ref rearLeftKneeInertia);
        DriveWalkKnee(rrRole, true, walkHalf == 0, rearRightKneeJoint, rrKn, kneeP,
            rearRightKneeFlexor, rearRightKneeExtensor, ref rearRightKneeInertia);
        DriveWalkKnee(flRole, false, false, frontLeftElbowJoint, flEl, kneeP,
            frontLeftElbowFlexor, frontLeftElbowExtensor, ref frontLeftElbowInertia);
        DriveWalkKnee(frRole, false, false, frontRightElbowJoint, frEl, kneeP,
            frontRightElbowFlexor, frontRightElbowExtensor, ref frontRightElbowInertia);

        // Forage writes idleWag after Walk(); hunt writes idleHunt
        // (stiff raise, no wag). Forage gait uses WalkTail on
        // walkPhase; quiet sit keeps 8.5 Hz; sit look hitches
        // SitLookTail (perk / droop with the neck); sit sniff
        // hitches SitSniffTail (droop with the dirt bob); sit
        // scratch hitches SitScratchTail (droop with the rake);
        // sit yawn hitches SitYawnTail (droop with the gape);
        // sit pant hitches SitPantTail (droop with the pump, not
        // the sit metronome); sit chew hitches SitChewTail
        // (droop with the snap, not rest). Leap raise is
        // LeapHuntTail (DriveLeap). CLI / Sleep leave both 0.
        ControlJointStable(tailJoint, IdleTailTarget(), tailPGain, tailDGain,
            tailFlexor, tailExtensor, ref tailInertia);

        DriveJaw();

        if (vestibular != null)
        {
            float neckGoal = WalkNeckWorldTarget();
            ControlSegmentToWorldUpright(
                vestibular.GetNeckTilt(), vestibular.GetNeckAngularVelocity(),
                neckJoint, neckFlexor, neckExtensor, ref neckEffectiveInertia, neckGoal);
            ControlSegmentToWorldUpright(
                vestibular.GetHeadTilt(), vestibular.GetHeadAngularVelocity(),
                headJoint, headFlexor, headExtensor, ref headEffectiveInertia, neckGoal);
        }

        LogWalkDiag(frSh, frEl, flSh, flEl, rlHip, rlKn, rrHip, rrKn, tame);
    }

    // DOGDIAG (programmer slot). Writes one line every 0.25 s to
    // %LOCALAPPDATA%/Temp/grok-burn/dog_diag.txt and to the player log.
    // Everything is read-only: the walk state machine is not touched.
    // Columns: t arm sw nsw half dual ph airdt gone need reach peel open airU
    //          comX vX pitch tame | grounded fr fl rr rl
    //          | paw bottom vs slab (fr fl rr rl)
    //          | joint actual/target (FRsh FRel FLsh FLel RLhip RLkn RRhip RRkn)
    //          | latches and gate flags.
    private void LogWalkDiag(
        float frSh, float frEl, float flSh, float flEl,
        float rlHip, float rlKn, float rrHip, float rrKn, float tame)
    {
        // Trials only: Play must not pay for this probe.
        if (!HeadlessTrial.Active)
            return;
        const int maxLines = 140;
        if (walkDiagWrites >= maxLines)
            return;
        float t = Time.fixedTime;
        if (walkDiagNext <= 0f)
            walkDiagNext = t;
        if (t < walkDiagNext)
            return;
        walkDiagNext = t + 0.25f;
        walkDiagWrites++;

        var ic = System.Globalization.CultureInfo.InvariantCulture;
        string F(float v) { return v.ToString("F3", ic); }
        string JA(HingeJoint2D j) { return j == null ? "n" : F(j.jointAngle); }
        string PB(Collider2D c)
        {
            return c == null ? "n" : F(c.bounds.min.y - walkGroundTopY);
        }
        float reach = WalkCaptureSwingU();
        var sb = new System.Text.StringBuilder(400);
        sb.Append("DOGDIAG t=").Append(F(t));
        sb.Append(" arm=").Append(walkCaptureArmed ? 1 : 0);
        sb.Append(" sw=").Append(walkSwungAir ? 1 : 0);
        sb.Append(" nsw=").Append(walkNearSwungAir ? 1 : 0);
        sb.Append(" half=").Append(walkHalf);
        sb.Append(" dual=").Append(F(walkHalfBorn >= 0f ? t - walkHalfBorn : -1f));
        sb.Append(" ph=").Append(F(walkPhase));
        sb.Append(" airdt=").Append(F(walkAirBorn >= 0f ? t - walkAirBorn : -1f));
        sb.Append(" gone=").Append(F(WalkSwingGoneM()));
        sb.Append(" need=").Append(F(WalkStrideNeed()));
        sb.Append(" reach=").Append(F(reach));
        sb.Append(" peel=").Append(F(SwingPeelU()));
        sb.Append(" open=").Append(F(WalkSwingOpenU(reach)));
        sb.Append(" plant=").Append(F(WalkAirPlantU()));
        sb.Append(" airU=").Append(F(WalkAirBlend()));
        sb.Append(" comX=").Append(F(comPosition.x));
        sb.Append(" vX=").Append(F(comVelocity.x));
        sb.Append(" pitch=").Append(F(chestPitch));
        sb.Append(" tame=").Append(F(tame));
        sb.Append(" | g=").Append(frontRightGrounded ? 1 : 0)
            .Append(frontLeftGrounded ? 1 : 0)
            .Append(rearRightGrounded ? 1 : 0)
            .Append(rearLeftGrounded ? 1 : 0);
        sb.Append(" slab=").Append(F(walkGroundTopY));
        sb.Append(" pawY=").Append(PB(frontRightPawCol)).Append(",")
            .Append(PB(frontLeftPawCol)).Append(",")
            .Append(PB(rearRightPawCol)).Append(",")
            .Append(PB(rearLeftPawCol));
        sb.Append(" | FRsh=").Append(JA(frontRightShoulderJoint)).Append("/").Append(F(frSh));
        sb.Append(" FRel=").Append(JA(frontRightElbowJoint)).Append("/").Append(F(frEl));
        sb.Append(" FLsh=").Append(JA(frontLeftShoulderJoint)).Append("/").Append(F(flSh));
        sb.Append(" FLel=").Append(JA(frontLeftElbowJoint)).Append("/").Append(F(flEl));
        sb.Append(" RLhip=").Append(JA(rearLeftHipJoint)).Append("/").Append(F(rlHip));
        sb.Append(" RLkn=").Append(JA(rearLeftKneeJoint)).Append("/").Append(F(rlKn));
        sb.Append(" RRhip=").Append(JA(rearRightHipJoint)).Append("/").Append(F(rrHip));
        sb.Append(" RRkn=").Append(JA(rearRightKneeJoint)).Append("/").Append(F(rrKn));
        sb.Append(" | latchHip=").Append(F(walkLatchHip));
        sb.Append(" latchKn=").Append(F(walkLatchKnee));
        sb.Append(" latchSh=").Append(F(walkLatchShoulder));
        sb.Append(" latchEl=").Append(F(walkLatchElbow));
        sb.Append(" entryHip=").Append(F(walkEntryHip));
        sb.Append(" entryKn=").Append(F(walkEntryKnee));
        sb.Append(" quietProxR=").Append(F(WalkQuietProximal(true)));
        sb.Append(" quietDistR=").Append(F(WalkQuietDistal(true)));
        sb.Append(" rockerKn=").Append(F(walkRockerKnee));
        sb.Append(" foldKn=").Append(F(walkFoldKnee));
        sb.Append(" rockerEl=").Append(F(walkRockerElbow));
        sb.Append(" foldEl=").Append(F(walkFoldElbow));
        sb.Append(" rockerU=").Append(F(SwingRockerU(true)));
        sb.Append(" heel=").Append(SwingHeelRose(true) ? 1 : 0);
        sb.Append(" readyRocker=").Append(SwingReadyToRocker(true) ? 1 : 0);
        sb.Append(" dualReady=").Append(DualReadyToPeel() ? 1 : 0);
        sb.Append(" trailAir=").Append(TrailPawAir() ? 1 : 0);
        sb.Append(" trailPl=").Append(TrailPawPlanted() ? 1 : 0);
        sb.Append(" catchSlide=").Append(TrailCatchSlide() ? 1 : 0);
        sb.Append(" plantedAhead=").Append(SwingPlantedAhead() ? 1 : 0);
        sb.Append(" readyComplete=").Append(SwingReadyToComplete() ? 1 : 0);
        sb.Append(" abort=").Append(WalkCaptureShouldAbort() ? 1 : 0);
        string line = sb.ToString();
        try
        {
            string dir = System.IO.Path.Combine(
                System.Environment.GetEnvironmentVariable("LOCALAPPDATA") ?? ".",
                "Temp/grok-burn");
            System.IO.Directory.CreateDirectory(dir);
            System.IO.File.AppendAllText(System.IO.Path.Combine(dir, "dog_diag.txt"),
                line + System.Environment.NewLine);
        }
        catch (System.Exception)
        {
            // Diagnostics must never stop the trial.
        }
    }

    // Forage Walk() restores idleWag; that flag nods with the trot clock.
    // Two dips per period: FR+RL plant then FL+RR. Hunt writes idleHunt
    // instead — nose down, no bob — so the chase is not a follow nod.
    // Wander also restores idleSniff — same 16° sit-sniff drop, so the
    // search reads as a track, not a follow bob around 0. Follow leaves
    // sniff 0. Same SPD world target as sit idle — not a new neckP.
    private float WalkNeckWorldTarget()
    {
        float t = neckTargetTilt;
        if (idleSniff > 0.01f)
            t -= 16f * idleSniff;
        if (idleHunt > 0.01f)
            t -= 12f * idleHunt;
        // Walk glance already wrote SetFacing. A short nod reads the
        // plant-hop; same SPD world target, not a new neckP. Mute the
        // trot bob so the turn is not two nods.
        float hop = WalkTurnHop();
        if (hop > 0.01f)
            t += 8f * hop;
        else if (walkHeadBob != 0f && idleWag > 0.01f)
            t += walkHeadBob * idleWag * WalkPulse();
        return Mathf.Clamp(t, -28f, 28f);
    }

    private static float WalkPawCommand(
        bool stanceCol, bool grounded, float stanceCmd, float swingCmd)
    {
        if (!grounded)
            return 0f;
        return stanceCol ? stanceCmd : swingCmd;
    }

    // After minDual the trail hip stays soft 0.35 so CoM can leave
    // that hind. Dual extra-extends that hip 4–8° caudal (same
    // ramp as stance) so Dual-soft rolls onto the toe — leftover-
    // sit Dual-soft fought plus 0.18 (dog_w68). Front swing
    // shoulder is a third plant at full P — Dual-soft on both
    // girdles dumped the chest. After Arm the trail hip HOLDS
    // leftover sit at full P (dog_w64 caudal 10° skated the pad;
    // dog_w62 more-minus was swing-flex on a pin; extra plus-knee
    // shortened the column, dog_w61; Dual-soft hip+knee was the
    // limp squat, dog_w60). Sit-relative +18° dirt rocker is ON
    // from Arm. Trail paw Zeroes after Arm. JointFriction stays.
    // Air resumes full P.
    private bool WalkSwingHipLimp(bool peelCol)
    {
        return peelCol && false;
    }

    private void DriveWalkHip(
        bool stanceRole, bool peelCol, bool trailSwing, HingeJoint2D joint, float target, float pGain,
        Muscle flexor, Muscle extensor, ref float cachedInertia)
    {
        if (stanceRole)
        {
            ControlJointStable(joint, target, pGain, hipDGain, flexor, extensor, ref cachedInertia);
            return;
        }

        if (WalkSwingHipLimp(peelCol))
        {
            if (joint != null)
            {
                float hipNow = joint.jointAngle;
                float hipSag = hipNow - target;
                if (hipSag < -5f || hipSag > WalkSwingHipSagCap)
                {
                    float cappedHip = target + Mathf.Clamp(hipSag, -5f, WalkSwingHipSagCap);
                    ControlJointStable(joint, cappedHip, pGain, hipDGain, flexor, extensor, ref cachedInertia);
                    return;
                }
            }
            ActuatorMixer.Zero(flexor, extensor);
            return;
        }

        if (!walkCaptureArmed)
        {
            if (trailSwing)
            {
                float dualHipP = pGain * WalkDualSwingHipPScale;
                ControlJointStable(joint, target, dualHipP, hipDGain, flexor, extensor, ref cachedInertia);
                return;
            }
            ControlJointStable(joint, target, pGain, hipDGain, flexor, extensor, ref cachedInertia);
            return;
        }

        ControlJointStable(joint, target, pGain, hipDGain, flexor, extensor, ref cachedInertia);
    }

    // Four distinct knee/elbow targets from WalkLimbTargets, all
    // at full P. Extra plus-knee on a held hip shortened the
    // column (dog_w61). Dual-soft 0.35 at leftover sit let gravity
    // squat the trail (dog_w60). Hip HOLDS leftover sit after
    // Arm; dirt rocker is sit-relative +18° ON from Arm.
    private void DriveWalkKnee(
        bool stanceRole, bool isRear, bool trailSwing, HingeJoint2D joint, float target, float pGain,
        Muscle flexor, Muscle extensor, ref float cachedInertia)
    {
        // Role/side name the four distinct targets at the call site.
        _ = stanceRole;
        _ = isRear;
        _ = trailSwing;
        ControlJointStable(joint, target, pGain, kneeDGain, flexor, extensor, ref cachedInertia);
    }

    // Dual take: the TRAIL plus WalkSwingRockerPlant with Dual-soft
    // hip extra-extending 4–8° caudal until heel-rise (leftover-sit
    // Dual-soft fought plus 0.18, dog_w68). Dual lean ramps from
    // Dual t=0 so CoM can leave that hind. DualReady Arms only on
    // heel-rise or trail air — the 0.12 s ceiling and a 3 cm CoM
    // shift Zeroed a still-flat pad (dog_w67 leftover sit-trot
    // 0.286 km/h). After Arm the trail paw Zeroes — plus 0.18 on a
    // full-P hip cannot pitch the sole (human ankle: torque turns
    // the trunk). Arm at minDual Zeroed a still-flat pad (dog_w66
    // leftover sit-trot 0.286 km/h). After Arm the trail hip HOLDS
    // leftover sit and the knee is sit-relative +18° ON from Arm.
    // Caudal 10° after Arm skated the pad.
    // Arm-tick U=0 jacked leftover sit. Inverted-V 6 cm was ~63°
    // (dog_w58). 3 mm never came on a welded pad (dog_w63). dog_w62
    // more-minus was swing-flex on a pin. Extra plus-knee shortened
    // the column (dog_w61). Dual-soft hip+knee dumped the pelvis
    // (dog_w60). Front swing stays a grip plant until the hind is
    // air — plus 0.18 on the shared-X twin unloaded the chest
    // (dog_w57). 0.55 speared the twin; 12 cm IK while pinned sat
    // the pelvis. Free foot Zeroes so the pad is not a conveyor.
    // Stance paws stay DrivePaw (CoM grip). DrivePaw is a muscle
    // signal, not a pose PD.
    private void DriveWalkPaw(
        bool stanceRole, HingeJoint2D joint, Muscle flexor, Muscle extensor,
        bool grounded, float command, bool swingAir, bool trailSwing)
    {
        if (!stanceRole)
        {
            if (grounded && !swingAir)
            {
                if (joint == null || flexor == null || extensor == null)
                    return;
                if (!trailSwing && !walkSwungAir)
                {
                    DrivePaw(joint, flexor, extensor, true, command);
                    return;
                }
                if (walkCaptureArmed)
                {
                    ActuatorMixer.Zero(flexor, extensor);
                    return;
                }
                ApplySignal(ProtectJointLimit(joint, WalkSwingRockerPlant), flexor, extensor);
                return;
            }
            ActuatorMixer.Zero(flexor, extensor);
            return;
        }
        DrivePaw(joint, flexor, extensor, grounded, command);
    }

    // Both halves keep a front plant (FR or FL), so the chest lean
    // can stay full. Bound half 1 (rears only) used 0.12 and left
    // CoM on the fronts.
    private float WalkStanceLeanK()
    {
        return 1f;
    }

    // Dual / peel: CoP seek of launch+lead. After the trail is
    // air, the setpoint walks at runSpeed from the leave sample
    // so the sole is not a treadmill (dog_w51 vt from t=0 while
    // four paws were still down). Stance-mid PawPd was P-error 0
    // on leftover sit. Grip is the seek itself: saturated while
    // CoM lags the target, quiet at speed.
    private float WalkStancePlantCommand(float tame)
    {
        // dog_ds2 (programmer). The stance paw CoM seek is OFF.
        // Measured on the same build, only this term changed:
        //   seek as stock (saturates, ±1)  → 3 cycles, travel −0.80 m,
        //                                    41° rear sit (dog_ds5)
        //   seek capped at 0.35            → 6 cycles, travel −0.69 m,
        //                                    41° rear sit (dog_ds8)
        //   seek sign flipped (±1)         → 3 cycles, travel −1.55 m (dog_ds7)
        //   seek removed                   → 46 cycles, travel +1.08 m,
        //                                    comSpeedX +0.045 (dog_ds6/ds9)
        // The seek is a pose servo on the paw joint by the back door (the
        // card forbids paw PD) and at 0.83 cm of error it saturates on every
        // step, so it rocks the soles and pitches the trunk into the sit
        // instead of pulling the CoM. The card's stance plant is the
        // leap-style extensor along facing — WalkPlantCommand below.
        // dog_ds3 (programmer): wiring it is a REGRESSION. Measured on
        // one build, only this term changed (25 s, same label extras):
        //   plant  0.70 (walkPawPlant default) -> comSpeedX -0.056,
        //           travel -1.335 m, cycles 7
        //   plant -0.70                        -> comSpeedX -0.043,
        //           travel -1.038 m, cycles 5
        //   plant -0.30                        -> comSpeedX -0.039,
        //           travel -0.937 m, cycles 3
        // against the dog_ds2 baseline comSpeedX +0.073, travel
        // +1.762 m, cycles 65. Either sign stalls the gait clock and
        // rolls the trunk caudally, so the paw plant is NOT the missing
        // push. Left off: the stride has to come from the stance sweep
        // in ApplyWalkPropulsion (dog_ds3), not from the paw.
        _ = tame;
        return 0f;
    }

    // Leap-style paw extensor along facing. Fronts are weaker
    // (walkFrontPlantScale). Grip only — no speed servo. Chasing
    // runSpeed on the sole was the sit-trot skate (dog_w10 0.286 km/h).
    // walkComMix now extra-extends the propulsion hip/shoulder.
    private float WalkPlantCommand(bool frontColumn, float tame)
    {
        float plant = GaitAmp(walkPawPlant, runPawPlant) * FacingSign() * tame;
        if (frontColumn)
            plant *= Mathf.Clamp01(walkFrontPlantScale);
        return Mathf.Clamp(plant, -1f, 1f);
    }

    // Diagonal push-off: extra-extend the planted pair. Dual also
    // extra-extends the Dual-soft TRAIL hip 4–8° caudal so the
    // column rolls onto the toe (leftover-sit Dual-soft fought
    // plus 0.18). After leave, the stance pair rolls. Cap 3°
    // unless the rear swing is air this step: the walkSwungAir latch
    // used to open 8° after a 15 ms flicker, then the replant dumped.
    // Speed error is degrees, not paw. Shoulder plus is caudal
    // (same as hip): minus used to brake the fronts while the
    // rears pushed, so CoM sat on the cranial plate.
    private void ApplyWalkPropulsion(
        float tame,
        bool rlStance, bool rrStance, bool flStance, bool frStance,
        ref float rlHip, ref float rrHip, ref float flSh, ref float frSh)
    {
        float dir = FacingSign();
        float extra = 0f;
        if (walkComMix > 0f && walkStanceSweepDeg > 0.01f && runSpeed > 0.05f)
        {
            // dog_ds3 (programmer): the stance sweep, not a speed nudge.
            // u runs 0 → 1 across the half, so the planted limb extends
            // caudally while the paw holds the ground and the trunk rides
            // the paw instead of dragging it. The 4–10° walkComMix nudge
            // (dog_w74) was the whole stride and produced 2.7 cm of travel
            // per cycle at 2.8 Hz — the sit pose was held while the paw
            // skated. Signed along facing, tamed by walkTamePitch.
            float u = walkHalfBorn >= 0f
                ? Mathf.Clamp01((Time.fixedTime - walkHalfBorn)
                                / Mathf.Max(0.04f, WalkPeriodSeconds()))
                : 0f;
            extra = Mathf.Lerp(-2f, walkStanceSweepDeg, u) * tame;
        }

        if (!walkCaptureArmed)
        {
            float age = walkHalfBorn >= 0f ? Time.fixedTime - walkHalfBorn : 0f;
            float ramp = Mathf.Clamp01(age / Mathf.Max(0.04f, WalkMinDual()));
            extra = Mathf.Lerp(4f, 8f, ramp) * tame;
            float dualHold = extra * dir;
            if (flStance)
                flSh = Mathf.Clamp(flSh + dualHold, -90f, 90f);
            if (frStance)
                frSh = Mathf.Clamp(frSh + dualHold, -90f, 90f);
            if (rlStance)
                rlHip = Mathf.Clamp(rlHip + dualHold, -90f, 90f);
            if (rrStance)
                rrHip = Mathf.Clamp(rrHip + dualHold, -90f, 90f);
            // Dual-soft trail held leftover sit, so 0.35 P fought
            // plus 0.18 and heel-rise never came (dog_w68 leftover
            // sit-trot). Digitigrade toe-off is hip plus (caudal)
            // while Dual-soft yields onto the toe. Same 4–8° as
            // stance, Dual-only, trail-only. After Arm the trail
            // hip HOLDS leftover sit — dog_w64 10° full-P caudal
            // skated a still-flat pad. Front swing stays leftover
            // sit (third plant). Half 0 peels RR; half 1 peels RL.
            if (walkHalf == 0)
                rrHip = Mathf.Clamp(rrHip + dualHold, -90f, 90f);
            else
                rlHip = Mathf.Clamp(rlHip + dualHold, -90f, 90f);
            return;
        }

        bool swingAirNow = SwingPairAir();
        if (!swingAirNow)
        {
            // Peel: keep Dual extra-extend so the stance pair holds
            // the girdle while the trail shortens. Clamping to 3°
            // after Arm dropped the pelvis into the 6 cm chord.
            float age = walkHalfBorn >= 0f ? Time.fixedTime - walkHalfBorn : 0f;
            float ramp = Mathf.Clamp01(age / Mathf.Max(0.04f, WalkMinDual()));
            extra = Mathf.Max(extra, Mathf.Lerp(4f, 8f, ramp) * tame);
        }

        float hold = extra * dir;
        if (flStance)
            flSh = Mathf.Clamp(flSh + hold, -90f, 90f);
        if (frStance)
            frSh = Mathf.Clamp(frSh + hold, -90f, 90f);
        if (rlStance)
            rlHip = Mathf.Clamp(rlHip + hold, -90f, 90f);
        if (rrStance)
            rrHip = Mathf.Clamp(rrHip + hold, -90f, 90f);
    }

    // Crouch → plant extension → stretch in the air → bite. No AddForce:
    // the launch is knee/elbow opening while paws still push the ground.
    private void DriveLeap()
    {
        ResolveLeapAim();

        float kneeTarget = 0f;
        float elbowTarget = 0f;
        float hipTarget = hipBaseAngle;
        float shoulderTarget = shoulderBaseAngle;
        float chestTarget = chestTargetTilt;
        float pawCommand = 0f;
        bool plantPush = false;
        bool lookAtPrey = false;

        switch (currentState)
        {
            case StanceState.LeapCrouch:
                kneeTarget = leapCrouchKnee;
                elbowTarget = leapCrouchKnee;
                hipTarget = leapCrouchHip;
                shoulderTarget = leapCrouchShoulder;
                chestTarget = leapChestCoil;
                // Coil faces the mark. World 0 was a sit squat, not a hunt.
                lookAtPrey = true;
                break;
            case StanceState.LeapPush:
                activationSpeedThisStep = leapActivationSpeed;
                kneeTarget = 0f;
                elbowTarget = 0f;
                // Extension past the coil: hip plus throws the pelvis
                // cranial, shoulder minus is the front-column mirror.
                hipTarget = 12f;
                shoulderTarget = -12f;
                chestTarget = leapChestLaunch;
                pawCommand = leapPawPlant;
                plantPush = true;
                lookAtPrey = true;
                break;
            case StanceState.LeapAir:
            case StanceState.LeapBite:
                kneeTarget = 0f;
                elbowTarget = 0f;
                hipTarget = 12f;
                shoulderTarget = -12f;
                chestTarget = leapChestLaunch;
                lookAtPrey = true;
                break;
        }

        if (plantPush)
        {
            DrivePaw(frontRightPawJoint, frontRightPawFlexor, frontRightPawExtensor, frontRightGrounded, pawCommand);
            DrivePaw(frontLeftPawJoint, frontLeftPawFlexor, frontLeftPawExtensor, frontLeftGrounded, pawCommand);
            DrivePaw(rearRightPawJoint, rearRightPawFlexor, rearRightPawExtensor, rearRightGrounded, pawCommand);
            DrivePaw(rearLeftPawJoint, rearLeftPawFlexor, rearLeftPawExtensor, rearLeftGrounded, pawCommand);
        }
        else
        {
            float offsetN = comOffsetX / Mathf.Max(0.01f, comOffsetReference);
            float velN = comVelocity.x / Mathf.Max(0.05f, comVelocityReference);
            float pawBalance = Mathf.Clamp(pawComP * offsetN + pawComD * velN, -1f, 1f);
            DrivePaw(frontRightPawJoint, frontRightPawFlexor, frontRightPawExtensor, frontRightGrounded, pawBalance);
            DrivePaw(frontLeftPawJoint, frontLeftPawFlexor, frontLeftPawExtensor, frontLeftGrounded, pawBalance);
            DrivePaw(rearRightPawJoint, rearRightPawFlexor, rearRightPawExtensor, rearRightGrounded, pawBalance);
            DrivePaw(rearLeftPawJoint, rearLeftPawFlexor, rearLeftPawExtensor, rearLeftGrounded, pawBalance);
        }

        ControlJointStable(rearLeftHipJoint, hipTarget, hipPGain, hipDGain,
            rearLeftHipFlexor, rearLeftHipExtensor, ref rearLeftHipInertia);
        ControlJointStable(rearRightHipJoint, hipTarget, hipPGain, hipDGain,
            rearRightHipFlexor, rearRightHipExtensor, ref rearRightHipInertia);
        ControlJointStable(frontLeftShoulderJoint, shoulderTarget, hipPGain, hipDGain,
            frontLeftShoulderFlexor, frontLeftShoulderExtensor, ref frontLeftShoulderInertia);
        ControlJointStable(frontRightShoulderJoint, shoulderTarget, hipPGain, hipDGain,
            frontRightShoulderFlexor, frontRightShoulderExtensor, ref frontRightShoulderInertia);

        DriveLumbarToWorld(chestTarget);

        ControlJointStable(rearLeftKneeJoint, kneeTarget, kneePGain, kneeDGain,
            rearLeftKneeFlexor, rearLeftKneeExtensor, ref rearLeftKneeInertia);
        ControlJointStable(rearRightKneeJoint, kneeTarget, kneePGain, kneeDGain,
            rearRightKneeFlexor, rearRightKneeExtensor, ref rearRightKneeInertia);
        ControlJointStable(frontLeftElbowJoint, elbowTarget, kneePGain, kneeDGain,
            frontLeftElbowFlexor, frontLeftElbowExtensor, ref frontLeftElbowInertia);
        ControlJointStable(frontRightElbowJoint, elbowTarget, kneePGain, kneeDGain,
            frontRightElbowFlexor, frontRightElbowExtensor, ref frontRightElbowInertia);

        // Hunt raise lives on idleHunt; LeapAttack zeros it, so a
        // pin at tailBaseAngle dropped the tail while chest / rump
        // already coiled. Minus = raise, same 16° as IdleTailTarget.
        // Amp 0 keeps the old rest. No idleTail: LeapAttack would
        // zero it.
        ControlJointStable(tailJoint, tailBaseAngle - 16f * LeapHuntTail(),
            tailPGain, tailDGain, tailFlexor, tailExtensor, ref tailInertia);

        DriveJaw();

        if (vestibular != null)
        {
            float worldTarget = lookAtPrey ? LookTiltTowardAim() : neckTargetTilt;
            ControlSegmentToWorldUpright(
                vestibular.GetNeckTilt(), vestibular.GetNeckAngularVelocity(),
                neckJoint, neckFlexor, neckExtensor, ref neckEffectiveInertia, worldTarget);
            ControlSegmentToWorldUpright(
                vestibular.GetHeadTilt(), vestibular.GetHeadAngularVelocity(),
                headJoint, headFlexor, headExtensor, ref headEffectiveInertia, worldTarget);
        }
    }

    private float LookTiltTowardAim()
    {
        Vector2 from = MouthPoint();
        float look = Mathf.Atan2(leapAimPoint.y - from.y, leapAimPoint.x - from.x) * Mathf.Rad2Deg;
        return Mathf.Clamp(look, -30f, 30f);
    }

    private void DriveJaw()
    {
        float open = Mathf.Clamp01(jawOpen);
        float now = Time.fixedTime;
        if (now < barkUntil)
            open = 1f;
        if (currentState == StanceState.LeapCrouch)
            open = Mathf.Max(open, 0.28f);
        else if (currentState == StanceState.LeapPush
                 || currentState == StanceState.LeapAir)
            open = Mathf.Max(open, 0.40f);
        if (now < biteOpenUntil)
            open = 1f;
        else if (now < biteCloseUntil)
            open = 0f;
        else
        {
            open = Mathf.Max(open, Mathf.Clamp01(idleYawn));
            if (idlePant > 0.01f)
                open = Mathf.Max(open, 0.28f * Mathf.Clamp01(idlePant));
            float walkPant = WalkPant();
            if (walkPant > 0.01f)
                open = Mathf.Max(open, 0.28f * Mathf.Clamp01(walkPant));
            if (idleChew > 0.01f)
                open = Mathf.Max(open, 0.62f * Mathf.Clamp01(idleChew));
            // Hunt stalk growl. CLI walk leaves idleHunt 0 (closed).
            if (idleHunt > 0.01f)
                open = Mathf.Max(open, 0.20f * Mathf.Clamp01(idleHunt));
        }

        jawStrikeArmed = now >= biteOpenUntil && now < biteCloseUntil;
        float target = Mathf.Lerp(jawClosedAngle, jawOpenAngle, open);
        ControlJointStable(jawJoint, target, jawPGain, jawDGain,
            jawFlexor, jawExtensor, ref jawInertia);
    }

    private Vector2 ComputeCoM()
    {
        if (Time.fixedTime == cachedComFixedTime)
            return cachedCom;

        Vector2 sum = Vector2.zero;
        float mass = 0f;
        if (bodies == null || bodies.Length == 0)
            CacheBodies();
        for (int i = 0; i < bodies.Length; i++)
        {
            Rigidbody2D rb = bodies[i];
            if (rb == null || rb.gameObject == gameObject) continue;
            sum += rb.worldCenterOfMass * rb.mass;
            mass += rb.mass;
        }
        cachedCom = mass < 0.001f ? (Vector2)transform.position : sum / mass;
        cachedComFixedTime = Time.fixedTime;
        return cachedCom;
    }

    private bool TryGetSupportBounds(out float minX, out float maxX)
    {
        minX = 0f;
        maxX = 0f;
        bool found = false;
        ExpandIfGrounded(frontRightPawCol, frontRightGrounded, ref found, ref minX, ref maxX);
        ExpandIfGrounded(frontLeftPawCol, frontLeftGrounded, ref found, ref minX, ref maxX);
        ExpandIfGrounded(rearRightPawCol, rearRightGrounded, ref found, ref minX, ref maxX);
        ExpandIfGrounded(rearLeftPawCol, rearLeftGrounded, ref found, ref minX, ref maxX);
        if (found)
        {
            lastMinX = minX;
            lastMaxX = maxX;
            hasLastSupport = true;
            return true;
        }
        if (hasLastSupport)
        {
            minX = lastMinX;
            maxX = lastMaxX;
            return true;
        }
        return false;
    }

    private static void ExpandIfGrounded(Collider2D col, bool grounded, ref bool found, ref float minX, ref float maxX)
    {
        if (col == null || !grounded) return;
        Bounds b = col.bounds;
        minX = found ? Mathf.Min(minX, b.min.x) : b.min.x;
        maxX = found ? Mathf.Max(maxX, b.max.x) : b.max.x;
        found = true;
    }

    private bool ProbePaw(Collider2D col, bool swingPeel, out bool heel, out bool toe,
        out float heelY, out float toeY)
    {
        heel = false;
        toe = false;
        heelY = 0f;
        toeY = 0f;
        if (col == null)
            return false;
        if (!groundContactFilterReady)
        {
            groundContactFilter = new ContactFilter2D();
            groundContactFilter.SetLayerMask(GroundLayers.Mask);
            groundContactFilter.useTriggers = false;
            groundContactFilterReady = true;
        }
        int hitCount = col.GetContacts(groundContactFilter, groundContacts);
        probeCallCount++;
        if (hitCount > 0)
        {
            // Swing peel: corners vs the slab, not this manifold.
            // A mid/caudal GetContacts point ORed the heel true on
            // a still-flat pad, so plus 0.18 never started the
            // rocker (dog_w56 sit-trot). Stance still uses it.
            if (!swingPeel)
                ClassifyPawContacts(col, hitCount, out heel, out toe);
            probeHitSum += hitCount;
            if (hitCount >= groundContacts.Length)
                probeSaturatedCount++;
        }
        AddPawCornerPins(col, ref heel, ref toe, out heelY, out toeY);
        if (swingPeel)
        {
            float band = Mathf.Max(WalkSoleBandM, Physics2D.defaultContactOffset * 2f);
            float slab = walkGroundTopY + band;
            heel = heelY <= slab;
            toe = toeY <= slab;
            return heel || toe;
        }
        if (heel || toe)
            return true;
        // Empty manifold AND both corners above the slab band.
        // Quiet / stance CCD: IsTouching still counts so DrivePaw
        // does not unload. Armed swing never takes this path.
        if (col.IsTouching(groundContactFilter))
        {
            heel = true;
            toe = true;
            probeHitSum += 1;
            return true;
        }
        return false;
    }

    // World heel/toe of the paw box vs the cached slab. GetContacts
    // often returns one cranial point of a still-flat pad; the
    // caudal corner is the real heel pin.
    private void AddPawCornerPins(Collider2D col, ref bool heel, ref bool toe,
        out float heelY, out float toeY)
    {
        heelY = 0f;
        toeY = 0f;
        BoxCollider2D box = col as BoxCollider2D;
        if (box == null)
            return;
        float band = Mathf.Max(WalkSoleBandM, Physics2D.defaultContactOffset * 2f);
        float slab = walkGroundTopY + band;
        PawSoleCorners(box, out Vector2 heelPt, out Vector2 toePt);
        heelY = heelPt.y;
        toeY = toePt.y;
        if (heelY <= slab)
            heel = true;
        if (toeY <= slab)
            toe = true;
    }

    private void PawSoleCorners(BoxCollider2D box, out Vector2 heel, out Vector2 toe)
    {
        Vector2 size = box.size;
        Vector2 off = box.offset;
        float dir = FacingSign();
        Transform tf = box.transform;
        heel = tf.TransformPoint(new Vector2(off.x - 0.5f * size.x * dir, off.y - 0.5f * size.y));
        toe = tf.TransformPoint(new Vector2(off.x + 0.5f * size.x * dir, off.y - 0.5f * size.y));
    }

    // Caudal of paw mid = heel. A point inside 8 mm of mid is a
    // flat pin (unknown edge) — keep the heel so we do not rocker
    // a still-flat pad.
    private void ClassifyPawContacts(Collider2D col, int hitCount, out bool heel, out bool toe)
    {
        heel = false;
        toe = false;
        Bounds b = col.bounds;
        float mid = (b.min.x + b.max.x) * 0.5f;
        float dir = FacingSign();
        const float midBand = 0.008f;
        int n = Mathf.Min(hitCount, groundContacts.Length);
        for (int i = 0; i < n; i++)
        {
            float dx = (groundContacts[i].point.x - mid) * dir;
            if (dx < -midBand)
                heel = true;
            else if (dx > midBand)
                toe = true;
            else
            {
                heel = true;
                toe = true;
            }
        }
    }

    private bool LatchPawGrounded(Collider2D col, ref float holdUntil, bool swingPeel,
        out bool heel, out bool toe, out float heelY, out float toeY)
    {
        bool raw = ProbePaw(col, swingPeel, out heel, out toe, out heelY, out toeY);
        bool useHold = pawGroundHoldSeconds > 0f && !walkWanted && !IsLeap();
        if (raw)
        {
            if (useHold)
                holdUntil = Time.fixedTime + pawGroundHoldSeconds;
            return true;
        }
        if (useHold && Time.fixedTime < holdUntil)
        {
            heel = true;
            toe = true;
            return true;
        }
        return false;
    }

    // Same command on all four. Soft deadzone so cm-scale CoM noise
    // does not saturate the paws. Rate still damps a slide; both
    // small → zero (JointFriction holds the sole).
    private float PawComCommand()
    {
        return PawPd(comOffsetX, comVelocity.x);
    }

    private float PawPd(float offset, float vel)
    {
        float posDz = Mathf.Max(0f, pawComDeadzone);
        float absOff = Mathf.Abs(offset);
        if (posDz > 0f)
            offset = absOff <= posDz ? 0f : Mathf.Sign(offset) * (absOff - posDz);
        float velDz = Mathf.Max(0f, pawComVelDeadzone);
        if (Mathf.Abs(offset) < 0.0001f && Mathf.Abs(vel) < velDz)
            return 0f;
        float offsetN = offset / Mathf.Max(0.01f, comOffsetReference);
        float velN = vel / Mathf.Max(0.05f, comVelocityReference);
        return Mathf.Clamp(pawComP * offsetN + pawComD * velN, -1f, 1f);
    }

    // Dual CoP seek. Quiet pawComDeadzone 2 cm would eat WalkLaunchLeadM
    // 3 cm and leave P-error 0 on leftover sit (phase 0 freeze).
    private float WalkSeekPd(float offset, float vel)
    {
        float offsetN = offset / Mathf.Max(0.01f, comOffsetReference);
        float velN = vel / Mathf.Max(0.05f, comVelocityReference);
        return Mathf.Clamp(pawComP * offsetN + pawComD * velN, -1f, 1f);
    }

    public bool AllPawsGrounded()
    {
        return frontRightGrounded && frontLeftGrounded && rearRightGrounded && rearLeftGrounded;
    }

    // Planted paws this physics step. The get-up gate and the walk acceptance
    // both need per-paw contact, not the all-or-nothing flag.
    public int GroundedPawCount()
    {
        int n = 0;
        if (frontRightGrounded) n++;
        if (frontLeftGrounded) n++;
        if (rearRightGrounded) n++;
        if (rearLeftGrounded) n++;
        return n;
    }

    public float MaxStanceActivation()
    {
        float max = 0f;
        IncludeActivation(ref max, rearLeftHipFlexor);
        IncludeActivation(ref max, rearLeftHipExtensor);
        IncludeActivation(ref max, rearRightHipFlexor);
        IncludeActivation(ref max, rearRightHipExtensor);
        IncludeActivation(ref max, rearLeftKneeFlexor);
        IncludeActivation(ref max, rearLeftKneeExtensor);
        IncludeActivation(ref max, rearRightKneeFlexor);
        IncludeActivation(ref max, rearRightKneeExtensor);
        IncludeActivation(ref max, frontLeftShoulderFlexor);
        IncludeActivation(ref max, frontLeftShoulderExtensor);
        IncludeActivation(ref max, frontRightShoulderFlexor);
        IncludeActivation(ref max, frontRightShoulderExtensor);
        IncludeActivation(ref max, frontLeftElbowFlexor);
        IncludeActivation(ref max, frontLeftElbowExtensor);
        IncludeActivation(ref max, frontRightElbowFlexor);
        IncludeActivation(ref max, frontRightElbowExtensor);
        return max;
    }

    private static void IncludeActivation(ref float max, Muscle muscle)
    {
        if (muscle != null && muscle.activation > max)
            max = muscle.activation;
    }

    private void DrivePaw(HingeJoint2D joint, Muscle flexor, Muscle extensor, bool grounded, float command)
    {
        if (!grounded)
        {
            UpdateMuscle(flexor, 0f);
            UpdateMuscle(extensor, 0f);
            return;
        }
        if (joint == null || flexor == null || extensor == null) return;
        ApplySignal(ProtectJointLimit(joint, Mathf.Clamp(command, -1f, 1f)), flexor, extensor);
    }

    // Open toward 0 (taller inverted-V). Never plus-fold past the
    // Walk() chord — that was the launch squat (both pairs commanded
    // into flex, pelvis dropped).
    private static float StanceDistalHold(float commanded, float hold)
    {
        float cap = Mathf.Min(Mathf.Max(0f, hold), WalkStanceDistalMax);
        return Mathf.Clamp(commanded, 0f, cap);
    }

    // Walk height from the Walk() sit, not spawn targetPelvisY.
    // Leftover sit is already ~0.19 m down; HeightLiftDeg saturates
    // at 22° and opens the stance V to 0°, unloading onto the swing.
    // 3 cm of sag from the sit opens this chord all the way to 0°
    // (still ≤15° fold). Dual does not call this — lean first.
    private float WalkStanceHeightLiftDeg()
    {
        if (!hasWalkPelvisHold)
            return 0f;
        bool stanceDown = (WalkLimbIsStance(true, true) && frontRightGrounded)
            || (WalkLimbIsStance(true, false) && frontLeftGrounded)
            || (WalkLimbIsStance(false, true) && rearRightGrounded)
            || (WalkLimbIsStance(false, false) && rearLeftGrounded);
        if (!stanceDown)
            return 0f;
        float drop = walkPelvisHoldY - pelvisY;
        // dog_w74: fight on 1 mm sag / −0.05 m/s — waiting 2 mm
        // let the girdle sink past acceptance on the slow half.
        if (drop <= 0.001f && pelvisVelY >= -0.05f)
            return 0f;
        float u = Mathf.Clamp01(Mathf.Max(0f, drop) / WalkHoldDropMax);
        float damp = Mathf.Clamp(-pelvisVelY * 20f, -8f, 18f);
        return Mathf.Clamp(u * WalkStanceDistalMax + Mathf.Max(0f, damp), 0f, WalkStanceDistalMax);
    }

    // targetPelvisY − pelvisY > 0 → pelvis too low → raise.
    // Both distal joints minus lift: opening the 24° V (thigh/upper
    // already +12°) lengthens the drop. Hips stay the pose.
    private float HeightLiftDeg()
    {
        if (!hasHeightTarget) return 0f;
        bool anyPaw = frontRightGrounded || frontLeftGrounded
                      || rearRightGrounded || rearLeftGrounded;
        if (!anyPaw) return 0f;
        float err = targetPelvisY - pelvisY;
        float posDz = Mathf.Max(0f, heightDeadzone);
        float absErr = Mathf.Abs(err);
        if (posDz > 0f)
            err = absErr <= posDz ? 0f : Mathf.Sign(err) * (absErr - posDz);
        float velDz = Mathf.Max(0f, heightVelDeadzone);
        if (Mathf.Abs(err) < 0.0001f && Mathf.Abs(pelvisVelY) < velDz)
            return 0f;
        float raw = heightP * err - heightD * pelvisVelY;
        return Mathf.Clamp(raw, -heightMaxDeg, heightMaxDeg);
    }

    private void DriveLumbarToWorld(float targetTilt)
    {
        if (lumbarFlexor == null || lumbarExtensor == null || vestibular == null) return;
        float tilt = vestibular.GetBodyTilt();
        float angVel = vestibular.GetBodyAngularVelocity();
        float error = Mathf.DeltaAngle(tilt, targetTilt) / Mathf.Max(1f, chestErrorReferenceDegrees);
        float speed = angVel / Mathf.Max(1f, speedReferenceDegPerSec);
        float signal = (-lumbarPGain) * error - (-lumbarDGain) * speed;
        if (lumbarPelvisP > 0f && vestibular != null)
        {
            float pErr = Mathf.DeltaAngle(vestibular.GetPelvisTilt(), pelvisTargetTilt)
                         / Mathf.Max(1f, pelvisErrorReferenceDegrees);
            float pSpd = vestibular.GetPelvisAngularVelocity()
                         / Mathf.Max(1f, speedReferenceDegPerSec);
            signal += lumbarPelvisP * pErr - lumbarPelvisD * pSpd;
        }
        signal = Mathf.Clamp(signal, -1f, 1f);
        if (lumbarJoint != null && lumbarRestP > 0f)
        {
            float rest = -targetTilt;
            float add = lumbarRestP * (rest - lumbarJoint.jointAngle) / 20f;
            signal += Mathf.Clamp(add, -lumbarRestAddCap, lumbarRestAddCap);
            signal = Mathf.Clamp(signal, -1f, 1f);
        }
        if (lumbarOpenGateDeg > 0f && lumbarJoint != null
            && lumbarJoint.jointAngle > lumbarOpenGateDeg && signal > 0f)
            signal = 0f;
        ApplySignal(ProtectJointLimit(lumbarJoint, signal), lumbarFlexor, lumbarExtensor);
    }

    private void ControlSegmentToWorldUpright(float tilt, float angVel, HingeJoint2D joint,
                                              Muscle flexor, Muscle extensor, ref float cachedInertia,
                                              float worldTarget)
    {
        if (flexor == null || extensor == null) return;
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
                float k = neckDGain * flexor.maxTorque * Mathf.Rad2Deg
                          / Mathf.Max(1f, speedReferenceDegPerSec);
                damping = k * dt / cachedInertia;
            }
        }
        float error = Mathf.DeltaAngle(tiltForError, worldTarget) / Mathf.Max(1f, neckErrorReferenceDegrees);
        float speed = angVel / Mathf.Max(1f, speedReferenceDegPerSec);
        float raw = (-neckPGain) * error - (-neckDGain) * speed;
        float signal = Mathf.Clamp(raw / (1f + damping), -1f, 1f);
        ApplySignal(signal, flexor, extensor);
    }

    private void ControlJointStable(HingeJoint2D joint, float targetAngle,
                                    float pGain, float dGain,
                                    Muscle flexor, Muscle extensor, ref float cachedInertia,
                                    float holdDeadzoneDeg = 0f)
    {
        if (joint == null || flexor == null || extensor == null) return;
        float dt = Time.fixedDeltaTime;
        float angleForError = joint.jointAngle + joint.jointSpeed * dt;
        float damping = 0f;
        if (cachedInertia <= 0f)
            cachedInertia = EffectiveInertia(joint);
        if (cachedInertia > 0f)
        {
            float k = dGain * flexor.maxTorque * Mathf.Rad2Deg
                      / Mathf.Max(1f, speedReferenceDegPerSec);
            damping = k * dt / cachedInertia;
        }
        float errDeg = Mathf.DeltaAngle(angleForError, targetAngle);
        float dz = Mathf.Max(0f, holdDeadzoneDeg);
        if (dz > 0f)
        {
            float absErr = Mathf.Abs(errDeg);
            errDeg = absErr <= dz ? 0f : Mathf.Sign(errDeg) * (absErr - dz);
        }
        float error = errDeg / Mathf.Max(1f, errorReferenceDegrees);
        float speed = joint.jointSpeed / speedReferenceDegPerSec;
        float raw = pGain * error - dGain * speed;
        ApplySignal(Mathf.Clamp(raw / (1f + damping), -1f, 1f), flexor, extensor);
    }

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

    private float ProtectJointLimit(HingeJoint2D joint, float signal)
    {
        if (joint == null || !joint.useLimits) return signal;
        float angle = joint.jointAngle;
        float min = joint.limits.min;
        float max = joint.limits.max;
        if (signal > 0f && angle >= max - pawLimitMargin) return 0f;
        if (signal < 0f && angle <= min + pawLimitMargin) return 0f;
        return signal;
    }

    private void UpdateMuscle(Muscle muscle, float targetActivation)
    {
        if (muscle == null) return;
        float speed = activationSpeedThisStep > 0f ? activationSpeedThisStep : muscleActivationSpeed;
        muscle.activation = Mathf.MoveTowards(
            muscle.activation, targetActivation,
            speed * Time.fixedDeltaTime);
    }

    private void RelaxAll()
    {
        UpdateMuscle(lumbarFlexor, 0f);
        UpdateMuscle(lumbarExtensor, 0f);
        UpdateMuscle(neckFlexor, 0f);
        UpdateMuscle(neckExtensor, 0f);
        UpdateMuscle(headFlexor, 0f);
        UpdateMuscle(headExtensor, 0f);
        UpdateMuscle(tailFlexor, 0f);
        UpdateMuscle(tailExtensor, 0f);
        UpdateMuscle(jawFlexor, 0f);
        UpdateMuscle(jawExtensor, 0f);
        UpdateMuscle(frontRightShoulderFlexor, 0f);
        UpdateMuscle(frontRightShoulderExtensor, 0f);
        UpdateMuscle(frontLeftShoulderFlexor, 0f);
        UpdateMuscle(frontLeftShoulderExtensor, 0f);
        UpdateMuscle(frontRightElbowFlexor, 0f);
        UpdateMuscle(frontRightElbowExtensor, 0f);
        UpdateMuscle(frontLeftElbowFlexor, 0f);
        UpdateMuscle(frontLeftElbowExtensor, 0f);
        UpdateMuscle(rearRightHipFlexor, 0f);
        UpdateMuscle(rearRightHipExtensor, 0f);
        UpdateMuscle(rearLeftHipFlexor, 0f);
        UpdateMuscle(rearLeftHipExtensor, 0f);
        UpdateMuscle(rearRightKneeFlexor, 0f);
        UpdateMuscle(rearRightKneeExtensor, 0f);
        UpdateMuscle(rearLeftKneeFlexor, 0f);
        UpdateMuscle(rearLeftKneeExtensor, 0f);
        UpdateMuscle(frontRightPawFlexor, 0f);
        UpdateMuscle(frontRightPawExtensor, 0f);
        UpdateMuscle(frontLeftPawFlexor, 0f);
        UpdateMuscle(frontLeftPawExtensor, 0f);
        UpdateMuscle(rearRightPawFlexor, 0f);
        UpdateMuscle(rearRightPawExtensor, 0f);
        UpdateMuscle(rearLeftPawFlexor, 0f);
        UpdateMuscle(rearLeftPawExtensor, 0f);
    }
}
