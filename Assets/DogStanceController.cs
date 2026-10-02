using UnityEngine;

// Quadruped stance. Not BalanceController. Forces only via Muscle activations.
[DefaultExecutionOrder(0)]
public partial class DogStanceController : MonoBehaviour
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
    // INVERTED heap (brief item 2, measured on dog_gu14). The tuck folds the
    // limbs onto the BELLY, and a dog lying on its back has that belly pointing
    // at the sky: all four limbs folding together lift the paws off the floor
    // and their reactions cancel, so the dog rocked through ±25° around pitch
    // 180° from 33.6 s to the end of the run with 0 paws down and nothing
    // changed. Righting needs a NET trunk rotation — what a cat gets by folding
    // one half of the body while the other half reaches, so the two half-body
    // reactions do not cancel — and a ground contact is the strongest lever
    // there is: while inverted the paws DO touch (10 % of the inverted frames),
    // but a contact lasts 1-2 frames, so a push that waits getUpTuckSeconds for
    // the tuck is never reached.
    [Tooltip("|chestPitch| above this is a dog on its BACK, not a side or belly heap, deg.")]
    public float getUpInvertedAbsPitch = 100f;
    [Tooltip("Seconds each half of the inverted-heap righting cycle holds before the driving pair swaps, s. Fold one pair while the other reaches straight past its trunk end (cat reflex), then swap.")]
    public float getUpInvertedSwapSeconds = 0.35f;
    // Witnesses (read-only for the recorder / the report).
    [System.NonSerialized]
    public bool getUpInvertedNow;
    [System.NonSerialized]
    public bool getUpInvertedFrontHalf;
    public int getUpAttempts;
    public bool getUpSuccess;
    public float getUpSeconds = -1f;
    public float getUpFallTime = -1f;
    // Witness for the recorder / CSV: true while the get-up commands the push
    // half of the tuck-push cycle (limbs extending against the floor).
    [System.NonSerialized]
    public bool getUpPushingNow;
    // ─── THE HAND-OVER (cycle 34) ───
    // The c33 landing row carried the crossing and stopped there: the c33 worker's
    // own hand-back names the wall as the HAND-OVER — the trunk crossed the band's
    // lower edge (|chestPitch| 69.642 at f3865) and the belly get-up never
    // converted it. Read frame by frame off that row: the tuck half of the belly
    // tuck-push cycle was commanded for ALL 322 belly-side frames and the
    // extension for NONE of them (push 0 on every one), while the plant the c33
    // cycle had built let go exactly there (the RR pad left the slab at f3860,
    // claws released with it, four paws 0.000 over f3860..f3878 — the deepest part
    // of the crossing had NO floor at all).
    // getUpHandOver is that hand-over's own weight, 0..1, and it is a SIGNAL, not
    // a flag: it is live only in a get-up episode that has BEEN on its back (the
    // latch below, so the fall's own tumble frames and the quiet run cannot read
    // it) and only while the trunk is a heap on the belly side of the card's line.
    // CYCLE 36: the plant's own hold is NOT part of this gate any more. c34 read it
    // here, and the c34 row measured the cost — the frames where a front paw could
    // land are the frames with a hind pad DOWN (f3836-f3850, f4499-f4514), and there
    // the reach read 0 by construction, while its 30 live frames were the airborne
    // parts of the rock (0 of 30 with any paw down). The hold now sits where it can
    // do work: the extension's own floor test (the tuck/push clock below), which is
    // what an extension needs. Its weight is the crossing's own depth. Everything
    // this signal drives: the front limbs' reach to the slab (CatchFront), the
    // tuck's settle before the extension, and the recorder's handOver column.
    [System.NonSerialized]
    public float getUpHandOver;
    // Witness for the recorder / CSV: true while the get-up commands the tuck half
    // of the belly tuck-push cycle (the counterpart of getUpPushingNow).
    [System.NonSerialized]
    public bool getUpPikeNow;
    // ─── THE COIL (cycle 71): the get-up family's own flag, DEFAULT OFF ───
    // Set from the trial's own command line (-dogCoil 1). With it off every branch
    // below reads exactly the accepted law, bit for bit; with it on the get-up
    // bends (coil), waits for a real plant and pushes from the bent pose.
    [System.NonSerialized]
    public bool coilWanted;
    // The latch: this get-up episode has had the trunk on its back (a heap with
    // |chestPitch| at or over getUpInvertedAbsPitch). Cleared by EnterGettingUp.
    private bool getUpBellySeen;
    // ─── THE CROSSING'S OWN MEMORY (cycle 44) ───
    // The latch above is cleared by EnterGettingUp, so the get-up that starts AFTER
    // a stand begins with the episode's own history thrown away: on the c40 row the
    // get-up that starts at f3527 therefore owns no belly-side frame at all, and the
    // front pads hang through its whole arrival (frontClearM > 0.05 on 979 frames of
    // f3527-f5000, mean 0.163 m — inside the front limb's own 0.34 m). This is the
    // same witness (a heap with |chestPitch| at or over the inverted bar) KEPT for
    // the episode. It is the round's separator, and it is exact by construction: the
    // witness cannot be read before the fall's own onset — the c40 row's tumble
    // (f666-f837, 172 frames) never reaches |chestPitch| 100 — so every read of it is
    // DARK on all 172 tumble frames (where the withdrawn c43 pick armed at f699) and
    // on the walk row's own frames 0..666.
    private bool getUpCrossedSeen;
    // The front reach's own geometry (cycle 34): the sole's target is the slab
    // GetUpHandFrontM in FRONT of the CoM — the mirror of the catch's back-plant
    // (GetUpCatchBackM behind it), solved with the same law of cosines and the
    // front limb's own lengths (frontUpperLen / frontLowerLen) off the front
    // shoulder's pivot. The clearance is the catch's own reach value.
    private const float GetUpHandFrontM = 0.08f;
    // ─── THE COIL (cycle 71) ───
    // The coil's own weight, 0..1: ramps up while the dog is INVERTED (the state
    // the fall leaves it in) and fades the moment the trunk leaves its back. It
    // blends the four limbs from the ballistic fold/straight pair swap to the
    // belly side's own tuck solve (limbs BENT, paws under the trunk) and gates
    // the push on a real plant. Never a flag: every read is a weight.
    private float getUpCoilW;
    // The coil's own ramp and its bounded hold: a coil that no plant ever
    // answers still breathes (the extension fires on the bound, the way the
    // accepted tuck-push clock bounds its own halves).
    private const float GetUpCoilRampSeconds = 0.30f;
    private const float GetUpCoilMaxSeconds = 0.60f;
    // ─── THE REACH (cycle 72) ───
    // The coil BENDS the limbs; this is the plant that HOLDS. A pad that finds
    // the floor while the trunk is on its back is held toward the slab UNDER the
    // CoM for GetUpReachHoldSeconds: the c71 rows measured what a touch without a
    // hold buys — 5-14 % contact over the inverted window, impulses at the noise
    // floor — so the extension's own trigger (plantHeld: a rear pad or a claw on
    // the slab) almost never sees a plant and fires off the 0.6 s bound instead.
    // The hold is armed by a FRESH touch only (never re-armed by a contact the
    // latch itself is carrying), it is bounded, and it is cleared the moment the
    // trunk leaves the inverted band. Flag-gated: with -dogCoil off nothing here
    // is read. Indices match the recorder: 0 FR, 1 FL, 2 RR, 3 RL.
    private readonly float[] getUpReachLatchedAt = new float[4] { -1f, -1f, -1f, -1f };
    private readonly bool[] getUpReachWasDown = new bool[4];
    // 0.20 s: the dose that ships (v1). v2's 0.06 s probe measured a LOWER contact rate on
    // both falls (std 14.8 % vs 13.0 %, wt3 9.7 % vs 15.4 %) and lost the wt3 crossing just
    // the same, so the brief's own value stands.
    private const float GetUpReachHoldSeconds = 0.20f;
    // ─── THE RELEASE BEFORE THE LINE (cycle 73) ───
    // The c73a row (the aim dose, +0.08 in front) measured the coupling the c72 rows also
    // show: a held contact feeds plantHeld, the pump's extension runs off a plant instead
    // of the rock's own half, and the rock that carried the c71 crossing never develops
    // again — the entry row crossed with push live 94 f; with the latch live the push runs
    // 532-857 f and the trunk's returns turn short of the line. So the hold's own territory
    // ENDS before the trunk reaches the tipping line: it exists only above the catch's own
    // arm point (getUpInvertedAbsPitch + GetUpCatchHoldDeg = 128, the band's own ceiling,
    // where the accepted catch's plant has already fired), and it is released the moment
    // the trunk descends past it — so the whole [line, arm] window is the accepted law's
    // own and the crossing carries no latched strut, while the deep-band plant (the c72
    // goal) lives above 128. The first dose (the margin at +15) measured the std row's
    // first-ever exit from the inverted band (3.46 s, f4293-f4643) — the line window
    // cleared is what moved it; this dose widens that clearing to the catch's own arm.
    private const float GetUpReachLineGuardDeg = 28f;

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
    // Cycle 32: the neck's own hinge angle (deg, the neck's jointAngle), so the
    // fold's follow is recorded frame by frame beside the lumbar's.
    public float neckAngle;
    public float chestY;
    public float pelvisY;
    public int probeCallCount;
    public int probeSaturatedCount;
    public int probeHitSum;

    // ─── CYCLE 1 INSTRUMENTS (read-only; nothing here feeds a control law) ───
    // Per-paw ground reaction — the quantity that reaches the gate and the CSV
    // today but never the demand, so it can be measured without changing the
    // run. pawNormalImpulse[i] is the solver impulse along that paw's contact
    // normals in THIS physics step, N*s — the same reading the human lane
    // publishes as fzL/fzR (ContactPoint2D.normalImpulse / dt is the force).
    // Paw order FR, FL, RR, RL — the order of the CSV grounded flags.
    public readonly float[] pawNormalImpulse = new float[4];

    // ─── CYCLE 6 SENSOR: the load each paw actually carries ───
    // The same manifold read (pawCol.GetContacts) that produces
    // pawNormalImpulse, kept per paw as the VERTICAL component and divided by
    // m*g*dt: pawLoadFraction[i] = Fn_i/(m*g), so 1 = that paw carries the whole
    // body weight and 0.5 = its share in a two-paw stance. Read by the stance
    // sweep only (ApplyWalkPropulsion); no other control law reads it and no
    // published column changes.
    private readonly float[] pawLoadFraction = new float[4];
    private readonly float[] pawVerticalImpulse = new float[4];

    // CYCLE 6: a stance limb carrying this share of the body weight sweeps with
    // the full stance sweep; below it the sweep fades linearly to 0. 0.5 = the
    // share a paw holds in a two-paw stance, so a loaded limb is bit-identical
    // to the pre-change build.
    public float walkStanceLoadRef = 0.5f;

    private readonly ContactPoint2D[] loadContacts = new ContactPoint2D[16];
    // sum(vertical Fn)/(m*g) for the frame, dimensionless: 1 = the paws carry
    // the whole weight, ~0 = the body is on its belly.
    public float pawWeightFraction;

    // ─── CYCLE 3 SENSOR (read-only; no control law reads any of it) ───
    // supportMargin above is the CoM PROJECTION against the support interval: it
    // asks only where the body is, never where it is going. A body travelling at
    // 1 m/s through a 0.5 m interval reads as "inside" up to the instant it is
    // not — the human capture walker left its support at 1.26 m/s in single
    // support and the projection never said so. These three fields are the same
    // measurement with the momentum in it: the LINEAR INVERTED PENDULUM
    // extrapolation of the CoM (the capture point), against the SAME interval.
    //
    //     omega = sqrt(g / h)        h = CoM height over the cached slab top
    //     xi    = x_com + v_x / omega    (CapturePointPlanner's convention)
    //
    // h is the live CoM height over walkGroundTopY (the slab top measured from
    // the ground collider at Start — rig geometry, not a controller-captured
    // bar), floored at CapturePointHeightFloorM so a body flat on the floor
    // cannot make omega singular; capturePointHeight publishes h, so the frames
    // where the model is out of its range are visible in the recording instead
    // of implied.
    public float capturePointX;
    public float capturePointHeight;
    // Sign as supportMargin: + inside the support interval, 0 on the edge,
    // − outside, m.
    public float capturePointMargin;
    // True while this Sense tick saw a live support interval. The else branch of
    // the support block leaves supportMinX/MaxX at their last values, so the
    // capture margin cannot be computed from them alone.
    public bool hasSupportInterval;
    public const float CapturePointHeightFloorM = 0.10f;
    // The controller's OWN heap bar in the Walk branch, m: WalkStandDrop
    // (standComHeight - comY), the value the walk fall test in Sense reads.
    // Published read-only so the recorder can log it beside its independent
    // trunkDrop; a gap over 0.05 m means the automaton is measuring a different
    // body than the recorder.
    public float walkStandDropBar;

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
    // CYCLE 28: the pelvis's own box, bound once — the rump witness reads its
    // corners (the pelvis's collider is a BoxCollider2D of the pelvis's size).
    private BoxCollider2D pelvisBox;
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
    // ─── THE CARRY (cycle 78): the push half's own drive off the planted pad ───
    // On the frames right after the trunk crosses onto the belly side, the
    // extension that crossed with it (the pump's plant-gated push) is not cut
    // short by the crossing: while the plant still holds, the stroke keeps its
    // floor and runs (the belly branch re-clocks instead of ending). Measured
    // reason: the old belly branch ended the carried stroke on the crossing
    // frame (phaseAge is the inverted half's clock, already past
    // getUpPushSeconds) and re-zeroed the phase clock, after which the arm's
    // own tuckSettle (0.39-0.42 s at the shallow hand-over) could never fit
    // inside a plant's life (0.05-0.09 s) - so the planted window below the
    // line ran the tuck (fold) and the extension never drove off the plant
    // (c77 dose f3888-3897; landed wt3 f2437-2445: pike through the plant).
    private bool getUpPushCarried;
    // ─── THE SEQUENCE (cycle 79): fronts sit, then the rear drive ───
    // R5's order, delivered inside the carry's own window (the pose block's own
    // note below): armed ONLY by the carry - the stroke that crossed the line
    // with the plant still under it - so every unwitnessed frame stays the
    // accepted law. The two clocks are bounded by the plant's own measured life
    // (0.05-0.09 s): the sit's say is short, the rear drive needs its travel
    // inside the same window.
    private bool getUpSeqArmed;
    private float getUpSeqBorn = -1f;
    private const float GetUpSeqSitSeconds = 0.03f;
    private const float GetUpSeqDriveRampSeconds = 0.05f;
    // ─── THE FLIP (cycle 80): the scripted supine recovery (the owner's order, 24.09) ───
    // A dog that ends on its back thrashes: measured, the wt3 row rocks around
    // +-180 for 17 s (3441 inverted frames) and never crosses. The owner's own
    // recovery: the dog pulls its limbs to the body (the TUCK), is rolled
    // smoothly 180 deg in-plane onto its belly (THE FLIP), and the accepted
    // get-up (the coil -> plant -> push) does the rest (THE HAND-OVER).
    // SUPINE-GATED: armed only from a heap on its back (the law's own
    // `inverted` witness) with the trunk LOW (trunkBar >= getUpConnectDrop, the
    // low contact) and the trunk near-fully inverted (GetUpFlipArmDeg, the
    // hysteresis bar above the law's own 100 bar); one flip per episode; every
    // read below sits behind coilWanted, so with the family's flag off every
    // other row is the accepted law, bit for bit.
    // THE ROLL IS THE ROOT DRIVEN: the script's own (theta, omega, alpha)
    // family (the smoothstep and its two analytic derivatives) is handed to the
    // trunk's pitch as a feed-forward PD, delivered as a torque pair on the two
    // trunk bodies (chest + pelvis) with each torque proportional to its own
    // body's inertia - so the trunk turns as ONE root and the lumbar hinge is
    // never loaded (its relative angle cannot change by construction). The four
    // limbs are held at the belly-side tuck solve (the pose block below), so
    // what rolls over the floor is the trunk's own back -> side -> belly
    // surface; the contacts stay the solver's (nothing is disabled, nothing is
    // teleported) and the torque budget (GetUpFlipRollAlphaMax) pays for
    // exactly that. The hand-over lands the trunk prone with the script's own
    // omega at zero; the pose then releases to the accepted get-up over
    // GetUpFlipReleaseSeconds.
    private bool getUpFlipUsed;              // one flip per get-up episode
    private float getUpFlipBorn = -1f;       // the arm frame (the tuck's own clock)
    private float getUpFlipPitchU;           // the trunk's unwrapped pitch, deg
    private float getUpFlipPitchPrev;        // the previous raw pitch (the unwrap's step)
    private bool getUpFlipPrevValid;
    private float getUpFlipFromU;            // the roll's own base (the tuck's end angle)
    private bool getUpFlipRollStarted;
    private bool getUpFlipInRoll;            // the roll half of the drive (kd / budget switch)
    private float getUpFlipTargetU;          // the roll's unwrapped target, deg
    private float getUpFlipTargetRate;       // the script's own target rate, deg/s
    private float getUpFlipTargetAcc;        // the script's own target accel, deg/s^2
    private float getUpFlipW;                // the pose hold's own weight, 0..1
    private float getUpFlipInertia;          // the root inertia (measured at the arm)
    private bool getUpFlipHanded;            // the trunk has been handed back to the solver
    private bool getUpFlipRolling;           // the drive is live (the tuck's freeze or the roll)
    private Rigidbody2D flipChestRb;
    private Rigidbody2D flipPelvisRb;
    private const float GetUpFlipArmDeg = 150f;         // the hysteresis bar (the law's inverted bar is 100)
    private const float GetUpFlipTuckSeconds = 0.30f;   // the limbs pull in; the rock is braked to rest
    private const float GetUpFlipRollSeconds = 0.45f;   // ~180 deg in-plane: peak rate ~750 deg/s
    private const float GetUpFlipSettleDeg = 25f;       // the hand-over's own bar: the trunk is THERE
    private const float GetUpFlipSettleRate = 150f;     // ... and nearly still (deg/s) - no pop
    private const float GetUpFlipSettleSeconds = 0.50f; // the settle's own timeout (a stuck roll still hands over)
    private const float GetUpFlipReleaseSeconds = 0.12f;
    private const float GetUpFlipDir = -1f;             // -1: the head leads over the top (the rump passes under)
    private const float GetUpFlipKp = 60f;              // 1/s^2: angle error (deg) -> accel (deg/s^2)
    private const float GetUpFlipKd = 14f;              // 1/s: rate error (deg/s) -> accel
    private const float GetUpFlipHoldKd = 6.0f;         // the tuck's own brake (no snap: the rock dies over ~0.2 s)
    private const float GetUpFlipRollAlphaMax = 16000f; // deg/s^2 (the script's own peak is ~5100)
    private const float GetUpFlipHoldAlphaMax = 9000f;
    private const float GetUpFlipTorqueGain = 1.2f;     // the root torque's own gain over the measured inertia
    // Inverted-heap righting cycle: getUpRightBorn = the frame the current half
    // started, getUpRightFront = the pair driving that half (it folds on the
    // tuck half and extends on the push half; measured from the trunk, never
    // from a state name).
    private float getUpRightBorn = -1f;
    private bool getUpRightFront = true;
    private float getUpRiseBlend;
    private float getUpContactAge;
    // ROCK-GATE (cycle 25): the righting PUMP is driven by the MEASURED trunk
    // rock, not by the fixed getUpInvertedSwapSeconds clock. getUpRockRate =
    // the unwrapped trunk pitch rate (deg/s, EMA-smoothed; its SIGN is the way
    // the trunk is rocking right now — the raw chestPitch wraps at +-180 and
    // at 200 Hz no single step can be a wrap, so the unwrap is one comparison);
    // getUpRockPrevPitch = the previous frame's chestPitch for that unwrap;
    // getUpRockDir = the latched rock direction (+1 = the trunk rocks toward
    // the INCREASING unwrapped pitch, -1 = toward the decreasing one, 0 = not
    // measured yet). The deadband + the latch are the whole hysteresis: a new
    // half is declared when the rock really reverses, never on a timer.
    private float getUpRockPrevPitch;
    private const float GetUpRockTauSeconds = 0.02f;
    private const float GetUpRockDeadbandDegPerSec = 60f;
    // ─── THE CLIMB (cycle 37): the stroke's own depth ───
    // Measured on the c36 row (the block table in the cycle-37 report): from the
    // fall to the first righting the trunk rocks with the SAME envelope for 14 s
    // — the card's own dev (180 - |unwrapped pitch|) sits at -33..-47 for 13 s
    // while the crossing needs -80 — and the pump's own clock is not the cost:
    // getUpInvertedSwapSeconds fired on 0 of the 169 halves the gate declared
    // (measured period 0.379 s mean at a 15-degree stroke, min 0.055 s), so the
    // phase is already the rock's own reversal and the cadence is at its
    // ceiling. The direction is what is not: the drive strokes as hard on the
    // way BACK as on the way over, so the pump holds a limit cycle whose centre
    // never walks, and the limbs are already at the torque ceiling (sat mean
    // 0.988 on that row) — at a saturated torque the stroke's own TRAVEL is what
    // turns into rotation. So the half that rotates the trunk TOWARD the tipping
    // line (a negative rockRate — the direction the fall put it in, and the one
    // the card's crossing needs) strokes GetUpClimbCommitGain deeper, and the
    // half that brings it back keeps the accepted depth: a rectified drive, not
    // a bigger one. Continuous, read off the pump's own measured rock rate.
    private const float GetUpClimbCommitDegPerSec = 60f;   // full commitment at the gate's own deadband
    private const float GetUpClimbCommitGain = 0.75f;      // +75 pct stroke depth on the committed half
    // Witnesses (read-only for the recorder / the report).
    [System.NonSerialized]
    public float getUpRockRate;
    [System.NonSerialized]
    public int getUpRockDir;
    // CYCLE 37 THE CLIMB: the weight on the committed half's stroke depth,
    // 0..1 (0 = the accepted depth, 1 = GetUpClimbCommitGain deeper). Read by
    // the pump, published on the row as the column `climbCommit` so the
    // attribution is frame by frame, not inferred from the joint angles.
    [System.NonSerialized]
    public float getUpClimbCommit;
    // ─── THE RELEASE (cycle 38): the deep half is given back as the crossing completes ───
    // The committed half took the trunk TO the card's line; the c37 row measured
    // what it does PAST it. The weight is keyed to the rock rate's sign alone, so
    // it re-armed on a trunk that was already over the line (1574 frames after the
    // righting still commanded the deeper stroke, dev -451.6, the run mid-roll) and
    // the dog ratcheted THROUGH the crossing instead of settling on it. That trunk
    // is the belly-side law's (the c36-landed reach / plant / push), so the pump's
    // deep half is released as the crossing completes: the release accumulates the
    // crossing's OWN depth — the same handDepth the hand-over reads, 0 at the card's
    // line and 1 at the band's lower edge — one crossing at a time, and HOLDS it, so
    // a trunk that is past the line is never driven past it again at a later wrap
    // (the far side reads the same wrapped |pitch| the near side does). A weight in
    // 0..1, never a flag: the mirror of the ramp (the rock's own rate) that arms the
    // stroke. It is folded into getUpClimbCommit itself, so the row's own column
    // names the fade frame by frame.
    private float getUpClimbRelease;
    // ─── THE HOLD (cycle 39): the pose the row reaches is HELD ───
    // The c38 landing row reached a real near-stand pose (f2643-f2650: four paws,
    // the trunk at the stand bar, |pitch| 13.8 -> 10.2) and lost it four frames
    // later (f2654: all four pads off the floor while the trunk pitched forward,
    // past the card's line by f2726, the run ending 11.4 s later on the far side,
    // getUpTrunkBarFinal +0.4190). The mechanism, read frame by frame on that row:
    // the HEAP bar (getUpConnectDrop, 0.20 m below the stand) ends the get-up's own
    // territory in the MIDDLE of the rise — the trunk crossed it at f2636 with the
    // pelvis still 43.7 deg off vertical — and the phase clock, the hand-over's
    // front reach (handOver 1.00 -> 0.00) and the push (1 -> 0) were all reset in
    // that one step. The get-up let go of a trunk it was still carrying, and the
    // ordinary stance law has no drive for a trunk that arrives at the top of the
    // rise with the rise's own momentum (comY +1.67 m/s over the push's last 14
    // frames, comX +1.0 m/s) — the stance law's OWN capture-point margin runs
    // +0.100 (f2643) -> -0.046 (f2650) and then the pads leave the floor.
    //
    // So the get-up keeps its own drive on the trunk through the heap -> stance
    // transition and hands it over to the ordinary stance law only when the stand
    // is STABLE. Every witness is the law's own, none of them new:
    //   ARM — the trunk's own height (trunkBar inside getUpStandDrop, the same bar
    //     GetUpIsStanding measures) with the pads down (getUpStandMinPaws), on an
    //     episode that has been on its back (getUpBellySeen) and a trunk under the
    //     squat bar: the rise has arrived. A latch, so the arm cannot chatter.
    //   WEIGHT — a ramp (GetUpHoldRampSeconds), a signal in 0..1, never a flag.
    //   RELEASE — TickGetUpSense's own hand-over clock (the stand held
    //     getUpPlantSeconds), i.e. the exact frame the ordinary stance law takes
    //     the dog anyway (BeginWalkAfterGetUp), so the hold can never outlive its
    //     own episode.
    //   DRIVE — the hand-over's own front reach (the c33/c36 replacement contact:
    //     the front pads servoed GetUpHandFrontM in front of the CoM, so they keep
    //     stepping under a CoM that is running forward), the rise's own pelvis
    //     lift KEPT (the height servo's D term is the law's one brake on the
    //     arrival — see the lift's own note), and the hold's own brake: the four
    //     limb hinges' D gains scaled by (1 + GetUpHoldBrakeGain * hold), because
    //     the pose the row reaches is the torqued-saturated tail of the rise's
    //     extension stroke and its overshoot is what topples the dog.
    // The first c39 row (dog_c39_w04) measured the phases half: with the clock
    // kept, the reach live and the lift faded the pose lasted the SAME 8 frames
    // (f2643-f2650) and the trunk went over as before (past the line at f2711,
    // 15 frames EARLIER than c38's f2726) — the phases' own action in those frames
    // was the accepted one, so the loss is the stroke's momentum, not the reset.
    // Its own side effect is on the record too: with the release keyed to the
    // plant clock, a pose that is lost keeps the hold latched (handOver 1.00 from
    // f2651 to the end on that row) — the brake below is therefore the hold's own
    // real work, and the release's gap is the next cycle's lever.
    private const float GetUpHoldRampSeconds = 0.05f;
    // The brake's own weight on the hinges' D gains while the hold is live
    // (dGain * (1 + this * hold): the same drives, resisting the rate).
    private const float GetUpHoldBrakeGain = 3f;
    private bool getUpHoldSeen;
    private float getUpHoldW;
    // ─── THE SEPARATOR THAT SURVIVES THE TUMBLE (cycle 44): the reach's own arming ───
    // The reach above is gated on the belly-side phase, and that phase needs
    // getUpBellySeen — cleared by EnterGettingUp. The third amount of weight is the pads'
    // own clearance in the ARRIVAL: dead under 0.05 m (a pad already on the floor is not a
    // pad to reach with), full at 0.22 m, faded by the trunk's own flatness to 0 at 40 deg.
    // Measured on dog_c40_w04 (see the cycle report): with the crossing's own memory and
    // the stand band as the arm, this weight is live on 93 frames of the 5001-frame row —
    // all of them inside the run's own end, f4265-f4955 — and on none before it. At the
    // c43 pick's own 0.03 m deadband the weight would also read 11 frames inside the rise
    // (f2672-f2725, mean 0.031), i.e. inside the c40 armour; the 0.05 m deadband is what
    // makes the pick act on the end and nowhere else.
    private const float GetUpFrontPlantDeadM = 0.05f;   // the pad is hanging (c36 column)
    private const float GetUpFrontPlantBandM = 0.17f;   // full arming at 0.22 m
    private const float GetUpFrontPlantFlatDeg = 40f;   // the trunk's own flatness envelope
    // ─── THE ARRIVAL (cycle 40): the stroke's TERMINAL phase decelerates ───
    // The c39 rows measured where the pose is lost: the eight frames the recorder
    // calls the pose (f2643-f2650) are the TAIL of the rise's own extension stroke,
    // and the stroke's own momentum is created in the 0.1 s before it. Read frame
    // by frame on the row of record (dog_c39b_w04): at f2630 the pelvis is 0.197 m
    // below the measured stand (getUpStandPelvisY) and nearly at rest (pelvisVelY
    // +0.29 m/s); over f2630 -> f2650 it rises 0.267 m at up to +3.62 m/s (mean
    // +3.16 m/s over the last 14 frames), the CoM's own rise rate peaks at 2.62 m/s
    // and its forward rate at 1.11 m/s, and the last pad leaves at f2652 with the
    // capture margin already going (-0.170). Every push cycle BEFORE f2630 is a
    // wobble that falls back (the trunkBar hovers 0.23-0.27 for 0.5 s).
    //
    // Both c39 levers acted on the last 0.04 s of that stroke and moved nothing:
    // adding gain to the drives (the hinges' D x4) does not help a torque-saturated
    // stroke (sat 0.983 -> 0.934, the pose, the last pad and the card's line
    // unchanged), and a brake armed on the trunk's own arrival has 0.03 s of stroke
    // left to act on. So this cycle's lever is the stroke's own TERMINAL PHASE, keyed
    // to the RATE the stroke is delivering the trunk at, and it is a RAMP OF THE
    // PUSH'S OWN DRIVE DOWN (a timing change: the same extension targets, delivered
    // with less drive, so the body arrives slower) — NOT a gain bump:
    //   ARM — the law's own stand reference (GetUpTrunkDropBar <= GetUpArriveDepth,
    //     0.23 m, i.e. the trunk is inside the top of the get-up's own rise) with a
    //     paw on the floor, on an episode that has been on its back and a trunk on
    //     the belly side of the card's line. Measured, it arms at f2630 and nowhere
    //     earlier in that episode.
    //   WEIGHT — a continuous signal in 0..1 from the pelvis's OWN rise rate
    //     (pelvisVelY, the same rate the accepted height servo's D term brakes on):
    //     0 at or below GetUpArriveVelLo (0.30 m/s) and 1 at GetUpArriveVelHi
    //     (1.20 m/s), smoothed over GetUpArriveTauSeconds. A governor: at the rate
    //     it bounds the drive falls, so it cannot stall the rise (a stalled stroke
    //     reads rate 0 and gets the full drive back).
    //   DRIVE — the extension's own P gains of the two limb pairs are faded by
    //     (1 - GetUpArriveFade * w), i.e. the push that launched the c39 pose is
    //     delivered at 55 pct, and the law's own brake keeps its room (the height
    //     servo's lift is clamped at heightMaxDeg, and the c39 arrival already read
    //     the clamp: heightP * err - heightD * pelvisVelY = -4.2 - 25.0 = -29 deg
    //     against a +-22 deg clamp, so the one brake the c39 report names was
    //     truncated by 7 deg at the exact frame it was needed).
    private const float GetUpArriveTauSeconds = 0.02f;
    private const float GetUpArriveVelLo = 0.30f;
    private const float GetUpArriveVelHi = 1.20f;
    private const float GetUpArriveDepth = 0.23f;
    private const float GetUpArriveFade = 0.45f;
    private const float GetUpArriveLiftGain = 1f;
    private float getUpArriveW;
    // ─── THE CATCH (cycle 26): the hold at the card's line ───
    // The c25 pump took the trunk to the card's line and the crossing did NOT
    // hold. Measured on that landing row (dog_c25_w04): through the whole
    // excursion f4532-4541 no paw was on the floor and no extension was
    // commanded (g=0000, push=0); the frame the trunk crossed, |pitch| < 100
    // turned the inverted righting branch off, so the BELLY tuck (all four
    // limbs folded under the trunk) owned the deepest pose the dog ever
    // reached — the rump slid and the trunk fell back onto its back (f4542-4554,
    // pitch 104 -> 120, the rear pair even extending into the floor at +36 and
    // +92 while the trunk went the other way). The catch gives that pose a
    // LEVER instead: in the band just past the line the HIND pads are servoed
    // to the floor — the pad's own measured clearance (paw collider min y over
    // the slab top, the reading the walk diag prints) closes the loop and
    // nothing else — so the support moves off the rump and under the CoM, and
    // a pad that holds switches the servo's sole target from a clearance to a
    // press: the extension.
    // The geometry the reach needs, measured on the same row with the same
    // chain: at the apex (f4538, pitch 93.6, pelvisTilt 117.7) the hips sit
    // 16.5 cm over the floor, a straight hind leg reaches 22 cm BELOW it and
    // the hip's own +-90 limit covers the ~48-53 deg the floor needs — while
    // the deepest hind pad contact the law ever made was dev -69 (f4544, rlG)
    // and the front pads never reached past dev -34: the reach was never
    // commanded, and the front pair cannot do this job.
    private const float GetUpCatchBandDeg = 30f;
    // ─── THE DWELL (cycle 33): the plant's clock, below the earlier rock ───
    // The c32 row measured the wall: the trunk crossed (f4521..f4530, 10 frames,
    // min 95.627) and rocked back, because the touch (f4532) lands 11 frames
    // AFTER the crossing starts — the pad's own travel needs 18 frames from the
    // arm (the floor dev -87.7 -> +79.8) and by then the trunk is already on the
    // rising part of the rock. So the arm moves up: the reach and the fold start
    // earlier and the pad is on the floor (gripped) before the trunk reaches the
    // line. The ceiling is the ROCK, not the anatomy: on the same row the rock
    // BEFORE this one bottoms out at |pitch| 130.973 (f4380) — an arm at or above
    // that trough would fire on an earlier rock, fold there and fade (the c32
    // runs 2/3 artefact), so the arm stays 3 deg under it. 128 = the line's own
    // 100 + GetUpCatchHoldDeg (28): the arm is still the band's first frame.
    // The brace keeps the crossing's own clock (braceW, cycle 30) — an earlier
    // arm of the whole branch is what c29 measured as a killer, so the brace is
    // NOT part of this move.
    private const float GetUpCatchHoldDeg = 28f;
    // ─── THE EARLY PLANT (cycle 29): the catch's TIMING, not its geometry ───
    // The c28 row measured the miss: the trunk rolls to its own contact's edge
    // and tips back at f4538, and the catching pad only plants at f4542 — four
    // frames late, so the crossing held 10 frames against a bar of 20. The pad's
    // clearance closes at ~1.8 m/s through the band, and the reach itself needs
    // 8-10 frames to close (c28: armed f4532, grounded f4542). Arming the catch
    // AT the line (|chestPitch| 100, the latch's original threshold) therefore
    // starts the reach too late by construction — the pad is still falling when
    // the trunk turns. This constant arms it this many degrees ABOVE the line
    // instead: on the c28 row's own descent the trunk falls through 105 at
    // f4523 — fifteen frames before the tip, nine before the old arm point — so
    // the reach gets its measured 8-10 frames plus 5-7 frames of margin to put
    // the pad on the floor BEFORE the tipping point: the pivot the pump's next
    // (front-drive) stroke needs. 105 and no more: the band's own ceiling is the
    // line + GetUpCatchHoldDeg (115) and every degree above 105 rides the
    // catch's servo over more of the rock's descent — a rock that turns around
    // above the line must not be steered by the catch. 105 is the smallest
    // threshold that still buys the whole pre-tip window — and that record is
    // KEPT; the c31 arm below is the band's ceiling instead.
    // ─── THE FOLD'S OWN CLOCK (cycle 31): the band's ceiling, not 105 ───
    // c30 §5a measured what the fold's swing costs when it runs between 105 and
    // the line: 104 deg of hip in 12 frames, 0 -> 0.895 deg/frame² of
    // deceleration against c28's unarmed descent, landing the brake exactly on
    // the trunk's own ~101.5-102 hesitation and turning the roll one frame
    // before c28 re-accelerated through that same hesitation to the line. The
    // swing is a fixed momentum transfer — it cannot be removed, only placed —
    // and the window it must vacate is the ~9 frames from 105 to the line while
    // the swing needs 12. So the fold's clock is the band's own CEILING
    // (getUpInvertedAbsPitch + GetUpCatchHoldDeg = 115): armed there, its 12
    // frames run while the trunk is still above 105, the pad is already on the
    // floor — pressed, claws engaged, the c31 grip — when the trunk reaches the
    // hesitation, and the lever waits where the trunk needs it. The brace and
    // the boosts stay on the crossing's own clock (braceW, cycle 30): the whole
    // descent from the arm to the line still carries none of them. The value is
    // the line's own 100 + GetUpCatchHoldDeg; with the line at its
    // published default the arm frame is the band's first frame. Cycle 33 moved
    // the hold to 28 (the DWELL's clock): the touch must precede the crossing's
    // start, and the measured pad travel needs 18 frames from the arm. The bound
    // on the ceiling is the previous rock's trough (|pitch| 130.973, f4380), so
    // the arm reads 128 — the plant acts only in the get-up it belongs to.
    private const float GetUpCatchArmDeg = 128f;
    // ─── THE SPLIT (cycle 30): the brace's own clock ───
    // c29 armed this whole branch at 105 and measured what its other half does to
    // the trunk: the front-limb brace (elbows 14 -> 91-110) and the P boosts turn
    // the roll 5-6 frames after the arm in BOTH rows (c28: arm f4532 at 99.71 ->
    // turn f4538 at 92.95; c29: arm f4523 at 104.54 -> turn f4528 at 101.99), so
    // an early whole-branch arm brakes the descent BEFORE the line — c29's
    // crossing |pitch| <= 100 held 0 frames against c28's 10. The fold alone is a
    // POSITIONING motion: it needs the trunk's nine frames of warning and it does
    // not brake (c29's own clearance curve closed 0.338 -> 0.047 m in 11 frames
    // with no contact until f4535). This constant is the other clock's width: the
    // brace and the boosts come in over the crossing's own first degrees — 0 at
    // the line (getUpInvertedAbsPitch, the latch's original arm, 100) and their
    // full c29 values this far past it. Four degrees is about half the c28
    // crossing (the line 100 -> the tip 92.95 over 8 frames), so the brace is
    // complete three to four frames after the line — the same arm-plus-5-to-6-
    // frames the old law turned the row on — while the whole descent from 105 to
    // the line carries none of it.
    private const float GetUpCatchBraceDeg = 4f;
    private const float GetUpCatchPawDropM = 0.04f;
    private const float GetUpCatchSoleM = 0.005f;
    private const float GetUpCatchPressM = 0.05f;
    // ─── THE CLAWS (cycle 31): the grip that turns the touch into a fulcrum ───
    // The designer's own order (21.09): the hind foot presses the ground, the
    // claws dig in, the paw FIXES — and the leg's own drive then levers the
    // body up over that fulcrum. The numbers that order answers: every pad that
    // reached the floor on this line carried its load for a handful of frames
    // and slipped on (c26's touch 2 frames; c27 the sole-comX +0.179 m; c28
    // +0.243; c30's RR touch f4535 with dRR +0.333 m and sumFnOverWeight 5.188
    // -> 0 in five frames) — the contact never HELD. The grip is the state the
    // contact was missing, and the three numbers below are its whole envelope,
    // each read from the pad's own contact (never from a state name):
    //   GetUpClawEngageLoad — the pad's measured normal load, in body weights
    //     (Fn/(m*g), the sumFnOverWeight unit), at or over which a PRESSED pad's
    //     claws engage: 0.25 = a quarter of the dog's own weight on that one
    //     pad. No grip in the air, no grip without load.
    //   GetUpClawSlipM — the anchored slip (the pad's own sole point's drift
    //     since the engage, m) that TEARS the claws out: the tear releases the
    //     grip and latches until the pad unloads and re-presses. 0.02 m.
    //   GetUpClawReleaseLoad — the load under which a gripped pad is off the
    //     ground and the grip simply ends (no tear): 0.05 body weights.
    // The capacity's own coefficient is the material's (-dogClawFriction, the
    // Dog.cs block) and is measured every frame as mu_pair * Fn, mu_pair from
    // the engine's own combine function. The knobs a stand may sweep are read
    // by this file's own reader below (the -dogSideOffset pattern Dog.cs has
    // carried since the art A/B; the dog's other CLI flags live in
    // HeadlessTrial, which this cycle does not touch).
    private const float GetUpClawEngageLoad = 0.25f;
    // Cycle 33 (the DWELL's grip): the c31/c32 rows both died on the same
    // measured number — the plant's own travel through the touch is 0.0105 ->
    // 0.0269 m in one step, so a 0.02 m budget tears by construction and the
    // grip is gone before the trunk reaches the trough. The budget is set to the
    // measured plant-scale demand (0.03), nothing else about the claws moves
    // (capacity, releases, material, witness — c31 as landed).
    private const float GetUpClawSlipM = 0.03f;
    private const float GetUpClawReleaseLoad = 0.05f;
    // Negative means "use the const above": -dogClawEngage <f> / -dogClawSlip <f>.
    public static float ClawEngageOverride = -1f;
    public static float ClawSlipOverride = -1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ReadClawSwitches()
    {
        string[] args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i + 1 < args.Length; i++)
        {
            float parsed;
            if (!float.TryParse(args[i + 1], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out parsed))
                continue;
            if (args[i] == "-dogClawEngage" && parsed > 0f)
                ClawEngageOverride = parsed;
            else if (args[i] == "-dogClawSlip" && parsed > 0f)
                ClawSlipOverride = parsed;
        }
    }

    // ─── THE FOLD (cycle 32): the trunk's own curl ───
    // The designer's order (21.09 16:18): the torso folds FAR more than the
    // brick's ±20 — "she is far more flexible; right now she is like a brick" —
    // and that flexibility has to help her get up. The RIG's own range is the
    // anatomical split in Dog.cs (lumbar flexion +40 / dorsal extension −20, the
    // neck's +50 / −35), with the ACCEPTED range kept as the rig's built
    // default: the stand, the walk (which rides the flexion stop for 84 frames
    // before the fall) and the fall were all measured in it, and Dog.cs's
    // SetTrunkFold is the handle that opens the wide range. This block is the
    // get-up's own USE of it, on the band's own clock: the fold opens with the
    // ARM (GetUpCatchArmDeg — the frame the branch that plants and levers
    // starts) and runs through the touch, the lever and the crossing.
    //   GetUpFoldLumbarDeg — the commanded lumbar FLEXION, deg of the hinge's
    //     own jointAngle (the rest hold is +8; positive = toward the belly).
    //     The rig's ceiling is +40; the measured landing (the first c32 stand)
    //     put the joint's own top at 42.08 — the solver's ~2° stretch past the
    //     stop — so the target is set at the top of the anatomy's own range, not
    //     under it: the curl's job is to HOLD the C there, and a target 2° lower
    //     would brake the crossing exactly where the trunk needs the fold (the
    //     brief's own warning: carry the trunk THROUGH, do not brake into it).
    //     At +39 the drive pulls at 4 % of its authority at the measured top —
    //     a hold, not a wall.
    //   GetUpFoldNeckDeg — the neck's own curl, deg: the head tucks toward the
    //     chest WITH the trunk, commanded as the neck's world target
    //     (chestTilt − this) so the curl is a real joint angle and not a world
    //     pose the neck cannot hold.
    //   GetUpFoldGateDeg — the window past the ACCEPTED flexion stop (20°) in
    //     which the curl's own law takes over from the accepted world loop. The
    //     fold can only act where the old clamp bound: inside the window (and
    //     with the same saturated drive the accepted law already had) the two
    //     laws are the same signal, so the trunk's trajectory is unchanged
    //     until the joint actually crosses the stop.
    //   GetUpFoldPGain / GetUpFoldDGain — the curl's own gains (the house
    //     values, but the fold's OWN knobs: the accepted world loop's
    //     lumbarPGain/lumbarDGain stay untouched, so no accepted row can move
    //     through this block).
    //   GetUpFoldRampSeconds / GetUpFoldFadeSeconds — the clock's own slopes, s:
    //     the ramp is LINEAR (first c32 landing's own defect: a shaped value fed
    //     back into its own accumulator never reached 1 — it topped at 0.226 in
    //     a 31-frame band, so the curl was commanded at a quarter strength).
    //     Linear reaches the target in the ramp's own time; a signal, never a
    //     flag.
    private const float GetUpFoldLumbarDeg = 39f;
    private const float GetUpFoldNeckDeg = 34f;
    private const float GetUpFoldGateDeg = 4f;
    private const float GetUpFoldPGain = 2f;
    private const float GetUpFoldDGain = 0.3f;
    private const float GetUpFoldRampSeconds = 0.03f;
    private const float GetUpFoldFadeSeconds = 0.15f;
    // The range's own release margin, deg: once the fold's weight is zero the
    // rig's range closes back to the accepted one — but only when the joint is
    // ALREADY inside it (within this margin), so the closing limit never has to
    // squeeze a folded trunk back (the first c32 landing put the joint at 39.9°
    // when the weight hit zero, and the limit pushed it 18° over 7 frames: an
    // artefact, not physics — the honest envelope has no pose snaps, and a
    // shrinking limit under the dog is one).
    // The stay-open clause is gated by getUpFoldRan: the first take wrote the
    // margin WITHOUT that gate, so the range opened wherever the lumbar rode
    // past +21 — the walk and the fall — and moved a run that never gets up
    // (identity broken). The fold may only touch the rig in a get-up that has
    // actually folded.
    private const float GetUpFoldCloseMarginDeg = 1f;
    private bool getUpFoldRan;

    // Witnesses (read-only for the recorder / the report). dev = |chestPitch|
    // - 180 (deg): the displacement off the flat-on-back pose, negative at the
    // card's line, the sign the cycle reports print.
    [System.NonSerialized]
    public int getUpCatchArmed;
    [System.NonSerialized]
    public int getUpCatchPad;
    [System.NonSerialized]
    public int getUpCatchPaw;
    [System.NonSerialized]
    public float getUpCatchDev;
    [System.NonSerialized]
    public int getUpCatchPush;
    [System.NonSerialized]
    public float getUpCatchClear;
    // ─── THE FRONT REACH (cycle 36): the front pads' own clearance ───
    // The catch has rearClearM for the hind pad (the servo's own input) and the
    // front limbs had NOTHING: the c34 report's second lane asked for exactly this
    // column before any target could be chosen. Read every get-up frame off the two
    // front pads' own colliders, the same way the hind pair is read — the lowest
    // front sole's height over the slab, m. Nothing in the control math reads it:
    // it is the witness the reach's own geometry is decided by.
    [System.NonSerialized]
    public float getUpFrontClear;
    // The catch's latch: the frame before this one (|chestPitch|), so the arm
    // can see the trunk fall THROUGH the line instead of merely being under it.
    private float getUpCatchPrevAbsPitch = 180f;
    // ─── THE BACK-PLANT (cycle 27): the support BEHIND the centre of mass ───
    // The c26 catch landed the hind pad 0.20-0.39 m FORWARD of the CoM
    // (dog_c26_w04 f4540: comX +2.401 against pawSoleXRR +2.605, the RL pad
    // +2.789), so the ground reaction at the support torqued the trunk
    // BACKWARD for the whole crossing: the hold ran 11 frames against a bar of
    // 20 and the trunk fell back onto its back (c26 report 7). This cycle aims
    // the same reach the other way — the sole's target is the slab BEHIND the
    // centre of mass — so the support's reaction and the hip's own pull both
    // carry the trunk forward.
    // The window the two hinges leave is this cycle's arithmetic, and it is
    // narrow. The thigh's world tilt is tD = pelvisTilt - hipAngle with the hip
    // hinge -90..+90 (Dog.cs ConnectTo(pelvis, ..., -90, 90)), so at the
    // crossing's pelvisTilt 122.6 the thigh is never less than 32.6 deg forward
    // of straight down; the knee hinge is 0..120 (Dog.cs ConnectTo(thigh, ...,
    // 0, 120)). With the hip 0.196 m over the slab the shin then folds to
    // sD = -85.7 (knee 118.3) and the sole lands 0.17*(sin32.6 - sin85.7) =
    // -0.078 m — 7.8 cm behind the hip's own pivot — while the hip itself sits
    // about 0.04 m forward of the CoM in that pose (f4540's chain, comX
    // +2.401 against hip +2.439). That is the whole lever available.
    private const float GetUpCatchBackM = 0.08f;      // the sole's target, m behind comX
    private const float GetUpCatchHipMaxDeg = 90f;    // the hip hinge's own ceiling
    private const float GetUpCatchKneeMaxDeg = 120f;  // the knee hinge's own ceiling
    // Witnesses (read-only for the recorder / the report): the signed distance
    // of the catching pad's sole and of that leg's hip pivot to the CoM, m.
    // + = forward of the CoM (the side c26 landed on), - = behind it, the side
    // this cycle needs. Both read 0 while the catch's band is not armed.
    [System.NonSerialized]
    public float getUpCatchSoleCom;
    [System.NonSerialized]
    public float getUpCatchHipCom;
    // ─── THE RUMP-CORNER HOLD (cycle 28): the one contact that needs no reach ───
    // The c27 back-plant moved the touch's pad the way it was aimed (sole-comX
    // +0.409 -> +0.179 at f4544) and the primary geometry then refused: the
    // limb's own hip pivot rides 0.088-0.134 m FORWARD of the CoM (the pelvis's
    // tilt maps it there by construction), the hip hinge's +-90 caps the
    // backward arm, and over the whole band the best reachable sole-x is
    // comX + 0.019 (f4540) — the hind pad can touch the CoM's own x and never
    // get behind it. The pose's only contact that CAN sit behind the CoM is the
    // body's own rear corner: the pelvis's rump, on which the pump's rock
    // already pivots, under the body for the whole band, needing no reach at
    // all. This cycle presses that corner onto the slab (the hind hips' flexor
    // side) so the CoM is trapped between the rump (behind) and the pads (in
    // front) and the pump's next stroke starts from the line instead of from
    // the back. The c27 measured what the OTHER press costs: the extension
    // through the pads raised the pelvis +0.077 m over the band and that rise
    // is what took the fold's room away (c27 report 4.3) — so this press has to
    // keep the pelvis LOW, and the flexor side is the side that does it.
    private const float GetUpRumpHoldM = 0.012f;      // the corner is ON the slab under this, m
    // Cycle 33 (the DWELL's support): the c32 row measured the press losing the
    // crossing — the corner cleared the hold at f4515 and reached 0.046 m at
    // f4530 (0.054 at f4531) while the press ran only 5 -> 20 deg of its 30, so
    // the window is tightened (the press now bites at the corner's own 0.02 m)
    // and the ceiling raised 30 -> 40 (the hip hinge's +-90 stays the hard
    // bound). c27's measurement is the sign: the extension through the pads
    // raised the pelvis +0.077 m; the flexor side is the side that keeps it low.
    // Cycle 34 (THE HAND-OVER) SPLIT THIS PAIR and sent both back to c32's values,
    // and the c33 row's own press column (rumpClearM, live on all 206 armed
    // frames) says exactly what each constant could buy:
    //   the DEG ceiling is COMMANDED almost everywhere — rumpLack > 0 on 201 of
    //   the 206 frames, i.e. the press ran at ~full flexion through the crossing —
    //   and the corner rose anyway (0.0280 -> 0.0824 m over the dwelling crossing,
    //   f3830..f3887). The extra 10 deg bought no corner hold, and its direction
    //   is against the hand-over's own need: hind FLEXION folds the limb whose
    //   extension is the conversion (the pad left the slab at f3860 and the trunk
    //   rocked back over the line).
    //   the M span acts only where the corner sits between the hold and
    //   hold+span (0.012..0.042 m at 0.03; 0.012..0.062 m at 0.05): 111 of the
    //   206 armed frames differ between the two settings (90 are past both, 5 are
    //   on the slab), and the difference is bounded by Deg*(1-lack) <= 18 deg of
    //   hip target on the frames where the corner is just lifting off. The
    //   corner's own path tracks the rock's see-saw (corr with rumpComM -0.19,
    //   with |chestPitch| +0.11), not the press, so nothing measured pays for the
    //   tighter window: it goes back to c32's 0.05 with the ceiling.
    // Both reverts make the dwelling crossing attributable to the plant's clock,
    // the band and the claw budget, and the hand-over to its own signal.
    //
    // ─── CYCLE 36 (THE FRONT REACH): THE PAIR GOES BACK, AND ITS COLUMN IS READ ───
    // The c34 hand-back's third lane, done as ONE edit with the hand-over's law
    // changed in the same build: the pair returns to c33's values (M 0.03, Deg 40)
    // and the corner's own column (rumpClearM — the press's own INPUT, measured off
    // the pelvis's box, not a command) is what the isolation is read from: the c34
    // row's crossing put that corner at 0.1234..0.1839 m against c33's 0.0280..0.0824
    // with the pair reverted, i.e. the reverted press holds the slab LESS well. The
    // c36 row's own rumpClearM over the same windows is the number the report carries,
    // and the confound is named there, not hidden: the reach's new command is in the
    // same build, so the corner's path is attributable to the press PAIR and to the
    // load the front limbs now take, not to the press alone. The ceiling keeps its
    // c33 direction (hind FLEXION) — the extension's own floor test now reads the
    // plant's hold, and the plant is exactly this corner plus the pads it sits with.
    private const float GetUpRumpPressM = 0.03f;      // the clearance the press works over, m
    private const float GetUpRumpPressDeg = 40f;      // the flexor bias' own ceiling, deg
    // Witnesses (read-only for the recorder / the report): the pelvis's own rear
    // corner (the box's caudal-dorsal corner — the rump) and the lowest of the
    // pelvis's four corners (the one that can hold the slab), each as a height
    // over the slab and a signed distance to the CoM (m, + = forward of it).
    // All four read 0 while the catch's band is not armed.
    [System.NonSerialized]
    public float getUpRumpClear;
    [System.NonSerialized]
    public float getUpRumpCom;
    [System.NonSerialized]
    public float getUpRumpLowClear;
    [System.NonSerialized]
    public float getUpRumpLowCom;
    // ─── THE CLAWS' WITNESSES (cycle 31) ───
    // All of them read 0 while no grip is held:
    //   getUpClawGripR / getUpClawGripL — each hind pad's own engage signal,
    //     dimensionless 0..1: its measured normal load Fn/(m*g) against the
    //     engage threshold, so 0 = the pad is in the air and 1 = it presses at
    //     or over the bar (the state machine's own input, published raw);
    //   getUpClawPaw — the GRIPPED pad: -1 none, 0 RR, 1 RL, 3 both
    //     (catchPaw's own encoding);
    //   getUpClawSlip — the anchored slip, m: the gripped pad's own sole point
    //     against the point it was anchored on at its engage (the greater of
    //     the two pads; kept on the frame a grip is torn out, 0 otherwise);
    //   getUpClawFn / getUpClawTf / getUpClawCap — the gripped contacts'
    //     normal force, tangential load and CAPACITY, N: capacity = mu_pair *
    //     Fn with mu_pair the engine's own combine of the pad's claw material
    //     and the slab's (PhysicsMaterial2D.GetCombinedValues);
    //   getUpClawFrac — Tf / Cap, dimensionless: the fraction of the capacity
    //     the grip is carrying (1 = the solver's own sliding limit — the load
    //     it can no longer hold);
    //   getUpClawMoment — the gripped contacts' moment about the CoM, N*m
    //     (z = r x F from the engine's own contact normal/tangent/impulses);
    //   getUpClawMuPair — the pair coefficient the capacity above was computed
    //     with, dimensionless (cached: neither material changes at runtime);
    //   getUpClawRipout — 1 on the step a grip was TORN OUT (the slip release
    //     while the pad was still loaded), else 0.
    [System.NonSerialized]
    public float getUpClawGripR;
    [System.NonSerialized]
    public float getUpClawGripL;
    [System.NonSerialized]
    public int getUpClawPaw = -1;
    [System.NonSerialized]
    public float getUpClawSlip;
    [System.NonSerialized]
    public float getUpClawFn;
    [System.NonSerialized]
    public float getUpClawTf;
    [System.NonSerialized]
    public float getUpClawCap;
    [System.NonSerialized]
    public float getUpClawFrac;
    [System.NonSerialized]
    public float getUpClawMoment;
    [System.NonSerialized]
    public float getUpClawMuPair;
    [System.NonSerialized]
    public int getUpClawRipout;
    // The claws' own state, per hind pad (RR, RL): the grip, the tear that
    // latches until the pad unloads, and the anchor the slip is measured from.
    private bool clawGripRState;
    private bool clawGripLState;
    private bool clawTornR;
    private bool clawTornL;
    private Vector2 clawAnchorR;
    private Vector2 clawAnchorL;
    // This step's tear (1) — the witness getUpClawRipout publishes it.
    private int clawRipoutNow;
    // The pair coefficient the capacity is computed with, cached on the first
    // engage: the claw material's friction and the slab's own, combined by the
    // engine's own function.
    private float clawMuPairCache = -1f;
    // ─── THE FOLD'S WITNESSES (cycle 32) ───
    // All of them read 0 while the fold is not running (before the band's arm):
    //   getUpFoldWeight — the fold's own clock, dimensionless 0..1: the ramp
    //     from the band's own arm (GetUpFoldRampSeconds), faded out
    //     (GetUpFoldFadeSeconds) when the band lets go. The recorder logs it
    //     frame by frame — a signal, never a flag;
    //   getUpFoldDeg — the commanded lumbar FLEXION in force, deg (0 while the
    //     accepted world law drives; +39 at full weight) = GetUpFoldLumbarDeg
    //     x weight — the curl's own target, inside the rig's +40 range;
    //   getUpFoldNeckDeg — the commanded neck flexion in force, deg (0..+34);
    //   getUpFoldFlexLimitDeg — the lumbar's ACTIVE flexion limit, deg: 20 =
    //     the accepted range (the rig's built default), 40 = the fold's range
    //     open (Dog.LumbarFlexionMaxDeg) — the rig's own state, published as
    //     the number it is;
    //   getUpFoldPastStopFrames — the frames this get-up has spent with the
    //     joint beyond the accepted flexion stop (20) — the fold's own
    //     excursion past the brick's clamp, counted where the old clamp bound;
    //   getUpFoldPastStopMaxDeg — the deepest such angle, deg.
    [System.NonSerialized]
    public float getUpFoldWeight;
    [System.NonSerialized]
    public float getUpFoldDeg;
    [System.NonSerialized]
    public float getUpFoldNeckDeg;
    [System.NonSerialized]
    public float getUpFoldFlexLimitDeg;
    [System.NonSerialized]
    public int getUpFoldRangeOpen;
    [System.NonSerialized]
    public int getUpFoldPastStopFrames;
    [System.NonSerialized]
    public float getUpFoldPastStopMaxDeg;
    // The fold's own state: the rig's range is open (the handle was called),
    // and the deepest excursion past the stop this get-up reached.
    private bool getUpFoldRigOpen;
    private float getUpFoldPastStopMax;
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
        // CYCLE 28: the pelvis's own box, for the rump witness (no Find at 200 Hz).
        pelvisBox = pelvisTf != null ? pelvisTf.GetComponent<BoxCollider2D>() : null;
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
        // Cycle 80: the flip's own two trunk bodies (the root drive's carriers).
        Transform flipChest = transform.Find("Chest");
        Transform flipPelvis = transform.Find("Pelvis");
        flipChestRb = flipChest != null ? flipChest.GetComponent<Rigidbody2D>() : null;
        flipPelvisRb = flipPelvis != null ? flipPelvis.GetComponent<Rigidbody2D>() : null;
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
        // ─── CYCLE 31: the grip may not outlive the get-up ───
        // While the automaton is in GettingUp the catch's own branch owns the
        // claws (engage, hold, release). The step the state moves on, whatever
        // grip is left is let go and the pads carry their paw material again —
        // the same step, so no claw batch can outlive the pose that grew it.
        if (currentState != StanceState.GettingUp)
            ReleaseClaws();
        // ─── CYCLE 32: the fold may not outlive the get-up ───
        // Same rule, same step: the fold's weight goes to zero (Rest) and the
        // rig's range closes back to the accepted one the stand and the walk
        // were measured in.
        if (currentState != StanceState.GettingUp)
            ReleaseTrunkFold();
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
            hasSupportInterval = true;
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
            hasSupportInterval = false;
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
        // Cycle 32: the neck's own hinge angle, same read, so the fold's
        // follow (the head tucking with the trunk) is a column of the row.
        neckAngle = neckJoint != null ? neckJoint.jointAngle : 0f;
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
        // CYCLE 1: read-only instrument pass, after every sensor of this frame is
        // settled. It writes the public values declared with the read-only
        // sensors and returns; nothing in this method reads them back.
        SampleCycle1Instruments();
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
        getUpPushCarried = false;
        getUpSeqArmed = false;
        getUpSeqBorn = -1f;
        // Cycle 80: the flip's own latch starts clear with the episode (the arm
        // is one-shot; a fresh fall may flip again).
        getUpFlipUsed = false;
        getUpFlipBorn = -1f;
        getUpFlipPrevValid = false;
        getUpFlipRollStarted = false;
        getUpFlipInRoll = false;
        getUpFlipHanded = false;
        getUpFlipW = 0f;
        getUpFlipRolling = false;
        // The righting cycle (inverted heap) also starts fresh: no half held and
        // the front pair drives first.
        getUpRightBorn = -1f;
        getUpRightFront = true;
        // The rock gate starts unmeasured: the first direction latch is the
        // first rate the trunk shows; until it speaks the front pair drives
        // (the pre-cycle-25 default), so a fall with no measurable rate yet is
        // no worse than the accepted law.
        getUpRockRate = 0f;
        getUpRockPrevPitch = float.NaN;
        getUpRockDir = 0;
        getUpPushingNow = false;
        // Cycle 34: the hand-over's own phase and weight start clear, and a fresh
        // episode has not been on its back yet (the latch arms in DriveGetUp).
        getUpHandOver = 0f;
        // Cycle 38: the release starts clear too — a fresh episode's deep half is
        // fully armed and no crossing has been completed yet.
        getUpClimbRelease = 0f;
        // Cycle 39: the hold starts clear — the rise has not arrived yet.
        getUpHoldSeen = false;
        // Cycle 40: the arrival's own weight starts clear with the episode.
        getUpArriveW = 0f;
        getUpHoldW = 0f;
        // Cycle 71: the coil's own weight starts clear with the episode too.
        getUpCoilW = 0f;
        // Cycle 72: the reach's own latch starts clear with the episode.
        for (int i = 0; i < 4; i++)
        {
            getUpReachLatchedAt[i] = -1f;
            getUpReachWasDown[i] = false;
        }
        // Cycle 36: the front pads' own clearance witness starts clear with it.
        getUpFrontClear = 0f;
        getUpBellySeen = false;
        getUpPikeNow = false;
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
        // A dog this far past upright is on its BACK (measured, not a state
        // name: shoulders and hips above the spine, belly up, every paw in the
        // air — dog_rd1 frame 190), and the tuck it is about to get was written
        // for a heap with a floor under its belly.
        bool inverted = heap && absPitch >= getUpInvertedAbsPitch;
        getUpInvertedNow = inverted;
        // ─── THE HAND-OVER (cycle 34): the phase, and its own weight ───
        // The tumble that PUTS a dog on its back is not the hand-over, so the
        // phase is latched by the episode's own history: only a get-up that has
        // had the trunk on its back can own it (on the c33 row the latch arms at
        // f899; the fall's own tumble frames 609..812 and the whole quiet row
        // never read it).
        if (inverted)
        {
            getUpBellySeen = true;
            getUpCrossedSeen = true;   // c44: the same witness, kept for the episode
        }
        // The belly get-up's own territory: the episode has been on its back and
        // the trunk is now a heap on the belly side of the card's line — every
        // frame of it a frame the catch's own band has let go of.
        bool bellySide = getUpBellySeen && heap && absPitch < getUpInvertedAbsPitch;
        // The plant's own hold, as the row measures it: a hind pad on the slab, or
        // a claw gripping it (the c33 cycle's own witness columns).
        bool plantHeld = rearRightGrounded || rearLeftGrounded
                         || getUpClawGripR > 0f || getUpClawGripL > 0f;
        // ─── THE HOLD (cycle 39): the rise's own drive survives the transition ───
        // See the field's own note for the c38 frames and the mechanism. Armed by
        // the trunk's own arrival in the stand band with the pads down, released by
        // TickGetUpSense's own hand-over clock — the law's own two witnesses.
        // The episode's own history gates the arm: the hold belongs to a get-up
        // that has been on its BACK (getUpBellySeen — the same latch the hand-over
        // reads) and to an upright trunk (|pitch| under the squat bar, so the
        // heap-by-pitch test cannot make this a belly-side frame). Without it the
        // arm fires in the FALL's own tumble: on the c38 row the trunk is briefly
        // on four paws with trunkBar +0.116 at f764-f769, 70 frames before the
        // trunk ever reaches its back, and a latch armed there would hold the
        // whole 5.35 s climb. Measured on that row, the gated arm fires on ONE
        // span and it is the pose itself: f2642..f2650 (0.040 s).
        if (getUpBellySeen && absPitch < squatDeg
            && trunkBar <= getUpStandDrop
            && GroundedPawCount() >= getUpStandMinPaws)
            getUpHoldSeen = true;
        bool standHeld = getUpPlantBorn >= 0f
            && Time.fixedTime - getUpPlantBorn >= getUpPlantSeconds;
        // CYCLE 40: the release the c39 rows measured as missing. Keyed to the
        // plant's clock, a hold whose pose is LOST stays latched for the whole
        // episode (handOver 1.000 on 2350 frames, both c39 rows: the pads drop to
        // 2 at f2651 and getUpPlantBorn is cleared before the 0.20 s clock can
        // ever run out). The pose's own loss is the pads leaving the floor, so the
        // release reads it directly — the same witness the arm needs (the pads
        // down), the other way round.
        bool poseGone = getUpHoldSeen && GroundedPawCount() < getUpStandMinPaws;
        if (standHeld || poseGone)
            getUpHoldSeen = false;
        getUpHoldW = getUpHoldSeen
            ? Mathf.Clamp01(getUpHoldW + dt / Mathf.Max(0.01f, GetUpHoldRampSeconds))
            : 0f;
        // ─── THE COIL (cycle 71): bend while on the back ───
        // The weight exists only while the get-up family's flag is on; `inverted`
        // is the law's own witness for the trunk on its back (heap + the inverted
        // bar), so the coil cannot read on an upright frame by construction.
        if (coilWanted)
            getUpCoilW = Mathf.Clamp01(getUpCoilW
                + (inverted ? 1f : -1f) * dt / Mathf.Max(0.01f, GetUpCoilRampSeconds));
        // ─── THE FLIP (cycle 80): the arm and the script's own clocks ───
        // SUPINE-GATED: the arm reads the law's own witnesses only - a heap on
        // its back (`inverted`), the trunk LOW (the low contact, the same
        // connect drop the heap test uses) and the trunk near-fully inverted
        // (the flip's own hysteresis bar, above the law's 100). One flip per
        // episode. Nothing below is read unless the family's flag is on.
        if (coilWanted)
        {
            if (!getUpFlipUsed && inverted && trunkBar >= getUpConnectDrop
                && absPitch >= GetUpFlipArmDeg)
            {
                getUpFlipUsed = true;
                getUpFlipBorn = Time.fixedTime;
                getUpFlipPitchU = chestPitch;
                getUpFlipPitchPrev = chestPitch;
                getUpFlipPrevValid = true;
                getUpFlipRollStarted = false;
                getUpFlipInRoll = false;
                getUpFlipRolling = true;   // the tuck's freeze is a drive too
                getUpFlipHanded = false;
                getUpFlipW = 0f;
                // The root inertia the drive is scaled by: measured once here
                // from the whole heap (the pose changes little during a flip).
                ComputeFlipInertia();
                // The flip owns the pump for its own duration: the righting
                // cycle is re-zeroed so the belly pump starts a fresh tuck at
                // the hand-over (the same reset EnterGettingUp does), and the
                // c79 carry / sequence (armed only through the crossing) stay
                // clear - the flip never crosses the band from above.
                getUpPikeBorn = -1f;
                getUpPhaseBorn = -1f;
                getUpPushPhase = false;
                getUpPushCarried = false;
                getUpSeqArmed = false;
                getUpSeqBorn = -1f;
                getUpRightBorn = -1f;
                getUpRockDir = 0;
                getUpRockRate = 0f;
                getUpRockPrevPitch = float.NaN;
                getUpPushingNow = false;
            }
            if (getUpFlipUsed)
            {
                // The unwrap: the same one-step comparison the rock gate uses
                // (at 200 Hz no single step can be a wrap), so the roll's own
                // target never wraps and the error never jumps 360.
                if (!getUpFlipPrevValid)
                {
                    getUpFlipPitchU = chestPitch;
                    getUpFlipPitchPrev = chestPitch;
                    getUpFlipPrevValid = true;
                }
                float flipStep = chestPitch - getUpFlipPitchPrev;
                if (flipStep > 180f) flipStep -= 360f;
                else if (flipStep < -180f) flipStep += 360f;
                getUpFlipPitchPrev = chestPitch;
                getUpFlipPitchU += flipStep;
                float flipAge = Time.fixedTime - getUpFlipBorn;
                float flipRollEnd = GetUpFlipTuckSeconds
                    + Mathf.Max(0.05f, GetUpFlipRollSeconds);
                if (!getUpFlipHanded)
                {
                    if (flipAge < GetUpFlipTuckSeconds)
                    {
                        // THE TUCK: the limbs pull to the body (the pose block)
                        // and the rock is braked to rest - the roll starts from
                        // a still trunk, so no velocity step is ever handed to
                        // the solver.
                        getUpFlipTargetU = getUpFlipPitchU;
                        getUpFlipTargetRate = 0f;
                        getUpFlipTargetAcc = 0f;
                        getUpFlipRolling = true;
                        getUpFlipInRoll = false;
                        getUpFlipW = Mathf.SmoothStep(0f, 1f,
                            flipAge / Mathf.Max(0.01f, GetUpFlipTuckSeconds));
                    }
                    else if (flipAge < flipRollEnd)
                    {
                        float flipT = Mathf.Max(0.05f, GetUpFlipRollSeconds);
                        float flipU = (flipAge - GetUpFlipTuckSeconds) / flipT;
                        // THE FLIP: the scripted 180 deg in-plane roll. theta,
                        // omega and alpha are the smoothstep's own analytic
                        // family (S, dS/du, d2S/du2), so the rate the owner
                        // asked for ("plavno") is a property of the script, not
                        // of the gains; the PD below only makes the solver
                        // track it.
                        if (!getUpFlipRollStarted)
                        {
                            getUpFlipRollStarted = true;
                            getUpFlipFromU = getUpFlipPitchU;
                        }
                        float flipUc = Mathf.Clamp01(flipU);
                        float flipS = flipUc * flipUc * flipUc
                            * (flipUc * (flipUc * 6f - 15f) + 10f);
                        float flipDS = 30f * flipUc * flipUc * (1f - flipUc) * (1f - flipUc);
                        float flipDDS = 60f * flipUc * (2f * flipUc - 1f) * (flipUc - 1f);
                        getUpFlipTargetU = getUpFlipFromU + GetUpFlipDir * 180f * flipS;
                        getUpFlipTargetRate = GetUpFlipDir * 180f * flipDS / flipT;
                        getUpFlipTargetAcc = GetUpFlipDir * 180f * flipDDS / (flipT * flipT);
                        getUpFlipRolling = true;
                        getUpFlipInRoll = true;
                        getUpFlipW = 1f;
                    }
                    else
                    {
                        // THE SETTLE: the script's own end (the prone target,
                        // rate 0) is held until the trunk is physically THERE -
                        // so the hand-over can never pop: the velocities are
                        // already small when the solver takes over. The timeout
                        // hands a stuck roll over anyway.
                        getUpFlipTargetU = getUpFlipFromU + GetUpFlipDir * 180f;
                        getUpFlipTargetRate = 0f;
                        getUpFlipTargetAcc = 0f;
                        getUpFlipRolling = true;
                        getUpFlipInRoll = true;   // the roll's own brake budget
                        getUpFlipW = 1f;
                        bool flipSettled = flipChestRb != null
                            && Mathf.Abs(getUpFlipTargetU - getUpFlipPitchU)
                                <= GetUpFlipSettleDeg
                            && Mathf.Abs(flipChestRb.angularVelocity)
                                <= GetUpFlipSettleRate;
                        if (flipSettled || flipAge >= flipRollEnd + GetUpFlipSettleSeconds)
                            getUpFlipHanded = true;
                    }
                }
                if (getUpFlipHanded)
                {
                    // THE HAND-OVER: the drive is off, the trunk is prone and
                    // nearly still; the pose releases to the accepted get-up's
                    // own targets over the release's own clock. The solver owns
                    // the trunk again - the same coil -> plant -> push the
                    // standing rows use does the rest.
                    getUpFlipInRoll = false;
                    getUpFlipW = Mathf.Clamp01(getUpFlipW
                        - dt / Mathf.Max(0.01f, GetUpFlipReleaseSeconds));
                    if (getUpFlipW <= 0f)
                    {
                        getUpFlipRolling = false;
                        // Re-arm: once the trunk is clearly out of the inverted
                        // regime a fresh fall may flip again - the latch is per
                        // supine stretch, not per trial.
                        if (Mathf.Abs(absPitch) < 90f) getUpFlipUsed = false;
                    }
                }
            }
        }
        // ─── THE ARRIVAL (cycle 40): the stroke's terminal phase ───
        // See the field's own note for the measured frames. The arm is the law's
        // own stand reference (trunkBar, the same drop the card reads) and the
        // weight is the pelvis's own rise rate; both are the accepted law's
        // witnesses, neither is new. Outside the belly get-up's own rising stretch
        // the want is exactly 0, so the quiet and the walk lanes never read it.
        float arriveWant = 0f;
        if (getUpBellySeen && !inverted && trunkBar <= GetUpArriveDepth
            && GroundedPawCount() >= 1)
            arriveWant = Mathf.Clamp01((pelvisVelY - GetUpArriveVelLo)
                / Mathf.Max(0.01f, GetUpArriveVelHi - GetUpArriveVelLo));
        getUpArriveW = Mathf.Clamp01(getUpArriveW
            + (arriveWant - getUpArriveW) * Mathf.Min(1f, dt / Mathf.Max(0.001f, GetUpArriveTauSeconds)));
        // The weight: the crossing's own depth past the line, 1 at the band's
        // lower edge (GetUpCatchBandDeg below the line — where the code's own
        // comment puts the hand-over).
        //
        // ─── CYCLE 36: THE PLANT'S HOLD LEAVES THE REACH ───
        // c34 gated this weight on !plantHeld as well, and the c34 row's own numbers
        // measured what that cost: the frames where a front paw could land are
        // exactly the frames where a hind pad is DOWN (f3836-f3850: 15 frames with
        // the trunk already 74-99 deg; f4499-f4514: 16 frames) and there the reach
        // read 0 BY CONSTRUCTION — the 30 frames it did own were the airborne parts
        // of the rock (0 of 30 with any paw down, 0 front contacts). The reach's
        // weight is the CROSSING'S OWN DEPTH (the ramp above: 0 at the card's line,
        // 1 at the band's lower edge) and its live gate the get-up's own belly-side
        // phase. The plant's own hold moves to the EXTENSION's floor test (the
        // tuck/push clock below, where it belongs: an extension needs something to
        // extend AGAINST). Nothing else about the hand-over changes: it still
        // commands the front limbs only, and it is still applied last.
        float handDepth = Mathf.Clamp01((getUpInvertedAbsPitch - absPitch)
            / Mathf.Max(1f, GetUpCatchBandDeg));
        getUpHandOver = bellySide ? Mathf.SmoothStep(0f, 1f, handDepth) : 0f;
        // CYCLE 39 THE HOLD: the same reach, kept live through the heap -> stance
        // transition (the field's own note). Nothing else about the hand-over
        // changes: it is still the front limbs only, still applied last.
        if (getUpHoldW > getUpHandOver)
            getUpHandOver = getUpHoldW;
        // Inverted heaps are due the moment they exist: while the trunk is
        // belly-up the only floor the limbs can reach is the one they touch for
        // 1-2 frames at a time, and the upright contact/age gate threw every one
        // of those contacts away (dog_rd1: 58 contact frames while inverted, 0
        // push frames, because getUpContactAge never reached getUpTuckMinSeconds).
        bool tuckDue = inverted || (heap
            && (anyPaw ? getUpContactAge >= tuckMin : getUpHeapAge >= tuckMin));
        // THE HAND-OVER (cycle 34): while the trunk is on the belly side of the
        // line in an episode that has been on its back, the plant's own FRESH
        // contact must not wipe the cycle. The c33 row's own numbers are the
        // reason: its pads ground for 25..27 frames at a time in that descent
        // (f3833..f3859, f3879..f3887) against the tuck's 30-frame settle
        // (getUpTuckMinSeconds 0.15 s = 30 frames at 200 Hz), so tuckDue fell
        // false on every grounded frame, the phase clock was zeroed on all of
        // them, and the branch that could start the push never ran on a frame with
        // a paw down. The extension's gate needs the clock AND a paw at the same
        // instant, and the two never coincided: the age stood at 15.1 s only while
        // four paws were in the air (f3830..f3832) and never passed 0.090 s once a
        // pad had touched — 0 push frames on 322 belly-side frames. The tuck is
        // what PUTS the paw on the floor, so the law's own comment ("the push
        // starts the moment one lands") was unreachable through this wipe.
        // CYCLE 39: the hold keeps the get-up's own clock through the heap ->
        // stance transition as well — the phase that carried the trunk up is not
        // wiped while the pose it reached is still being held.
        if (!tuckDue && !bellySide && getUpHoldW <= 0f)
        {
            getUpPikeBorn = -1f;
            getUpPhaseBorn = -1f;
            getUpPushPhase = false;
            getUpPushCarried = false;
            getUpSeqArmed = false;
            getUpSeqBorn = -1f;
            getUpRightBorn = -1f;
            // The rock gate dies with the half it drove: the next inverted
            // stretch measures its own rock from scratch.
            getUpRockRate = 0f;
            getUpRockPrevPitch = float.NaN;
            getUpRockDir = 0;
        }
        else
        {
            if (getUpPikeBorn < 0f)
            {
                getUpPikeBorn = Time.fixedTime;
                getUpPhaseBorn = Time.fixedTime;
                getUpPushPhase = false;
            }
            if (inverted)
            {
                // ROCK-GATE (cycle 25) instead of the cat-reflex clock. The
                // cycle-24 landing row measured the wall this replaces: the
                // fixed getUpInvertedSwapSeconds half never stays in phase with
                // the rock (measured apex spacing on that row: 0.10-0.35 s and
                // drifting), so the two half-body reactions cancelled on
                // average, the rock stayed bounded at +-30 deg off the back for
                // 4163 of 4163 inverted frames and the trunk never crossed the
                // tipping line. The pump gets its PHASE from the measured rock:
                // the pair that strokes is the pair whose reaction pushes the
                // way the trunk is already going, and the half ends when the
                // trunk really reverses — never on a timer. A paw that lands
                // still starts the extension AT ONCE (the contact is the
                // strongest lever there is, and while inverted a contact lasts
                // 1-2 frames, so waiting getUpTuckSeconds for it never let a
                // single one of them reach the floor) — but the pair that
                // extends is the rock's pair now, not the contact's.
                if (getUpRightBorn < 0f)
                {
                    getUpRightBorn = Time.fixedTime;
                    getUpRightFront = true;
                    getUpPushPhase = false;
                    getUpRockRate = 0f;
                    getUpRockPrevPitch = chestPitch;
                    getUpRockDir = 0;
                }
                float halfAge = Time.fixedTime - getUpRightBorn;
                float half = Mathf.Max(0.10f, getUpInvertedSwapSeconds);
                // MEASURE the rock, in the one frame the law already owns:
                // chestPitch wraps at +-180, and at 200 Hz a single step (max
                // ~7 deg on the fastest measured rock) can never be a wrap, so
                // one comparison unwraps it exactly. The EMA (tau 0.02 s) kills
                // the one-frame noise the recorder's 3-frame difference carries.
                if (float.IsNaN(getUpRockPrevPitch))
                    getUpRockPrevPitch = chestPitch;
                float rockStep = chestPitch - getUpRockPrevPitch;
                if (rockStep > 180f) rockStep -= 360f;
                else if (rockStep < -180f) rockStep += 360f;
                getUpRockPrevPitch = chestPitch;
                getUpRockRate += (rockStep / dt - getUpRockRate)
                    * Mathf.Clamp01(dt / GetUpRockTauSeconds);
                // Hysteresis: the half flips on the rock's own reversal (the
                // apex), and only when the rock really moves the other way —
                // the deadband is what keeps the pair from chattering in the
                // 0.05 s the trunk spends inside +-100 deg/s at an apex.
                bool rockTurned = false;
                if (getUpRockDir == 0)
                {
                    if (getUpRockRate >= GetUpRockDeadbandDegPerSec) getUpRockDir = 1;
                    else if (getUpRockRate <= -GetUpRockDeadbandDegPerSec) getUpRockDir = -1;
                }
                else if (getUpRockDir > 0)
                {
                    if (getUpRockRate <= -GetUpRockDeadbandDegPerSec)
                    {
                        getUpRockDir = -1;
                        rockTurned = true;
                    }
                }
                else if (getUpRockRate >= GetUpRockDeadbandDegPerSec)
                {
                    getUpRockDir = 1;
                    rockTurned = true;
                }
                // THE PUMP: drive the pair whose stroke is WITH the rock. The
                // measured contrast on dog_c24_w04 (dev-band x rate-sign cells,
                // 4028 frames, the pair's own reaction with the pendulum term
                // conditioned out): rear-drive's trunk reaction is the
                // positive-pitch stroke and front-drive's the negative one, by
                // +465 deg/s^2 weighted. So: the trunk rocking toward the
                // increasing unwrapped pitch = rear drives; toward the
                // decreasing one = front drives; not measured yet = the
                // accepted law's first half (front).
                getUpRightFront = getUpRockDir <= 0;
                // ─── THE COIL (cycle 71): the push waits for a real PLANT ───
                // With the family on, the extension is no longer keyed to the
                // rock's own clock alone: the four limbs hold the coil (bent, paws
                // under the trunk) until a pad is PLANTED — plantHeld (a rear pad
                // on the slab or a claw gripping it, the law's own witness) — and
                // then the extension drives the trunk about that contact. The
                // bounded coil (GetUpCoilMaxSeconds) keeps the pump breathing when
                // no plant answers. With the flag off this reads exactly `anyPaw`.
                bool pushReady = coilWanted
                    ? (plantHeld || halfAge >= GetUpCoilMaxSeconds)
                    : anyPaw;
                if (pushReady && !getUpPushPhase)
                {
                    getUpPushPhase = true;
                    getUpRightBorn = Time.fixedTime;
                }
                else if (getUpPushPhase && (rockTurned || halfAge >= half))
                {
                    // The extension ends when the rock has turned — the half IS
                    // the rock's half now; getUpInvertedSwapSeconds survives
                    // only as the bound on one extension, not as the phase.
                    getUpPushPhase = false;
                    getUpRightBorn = Time.fixedTime;
                }
                getUpInvertedFrontHalf = getUpRightFront;
                // The carry's own witness: whether this inverted frame ends
                // with the extension live - the belly branch reads it on its
                // first frames to keep the stroke on its plant across the line.
                getUpPushCarried = getUpPushPhase;
            }
            else
            {
                getUpRightBorn = -1f;
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
                // THE HAND-OVER (cycle 34): the extension's own trigger. The
                // tuck's settle is scaled by the hand-over's weight, so the deeper
                // the crossing — the further past the line, and the longer the
                // plant's own hold has been gone — the sooner the extension takes
                // over from the tuck, down to "the moment a paw is on the floor"
                // at the band's lower edge. A signal (a continuous weight on the
                // accepted clock), never a flag.
                float tuckSettle = Mathf.Max(0.10f, getUpTuckSeconds)
                    * (1f - getUpHandOver);
                // ─── CYCLE 36: THE EXTENSION'S FLOOR TEST IS THE PLANT ───
                // This test was `anyPaw` (any of the four pads, front paws
                // included). With the reach now live on the belly side, a front pad
                // can touch while the hind plant is gone — and a front pad alone is
                // not what an extension of the BODY is driven against: the c34 row's
                // own 37 extension frames were 37/37 against a grounded hind pad
                // (34 of them with a claw gripping it). The floor this test asks for
                // is the PLANT'S own hold — a hind pad on the slab, or a claw
                // gripping it (the same `plantHeld` the reach no longer reads). The
                // push then starts the moment the plant really has the floor and
                // stops the moment it lets go, which is what the extension needs;
                // the reach, meanwhile, is free to be live in the same frames.
                if (coilWanted && getUpPushCarried && getUpPushPhase && plantHeld)
                {
                    // THE CARRY (cycle 78): the stroke crossed the line with the
                    // plant still under it - the plant is the floor it keeps
                    // driving off. Re-clock the half (the extension gets its own
                    // window, bounded by getUpPushSeconds) instead of ending here.
                    getUpPhaseBorn = Time.fixedTime;
                    getUpPushCarried = false;
                    // THE SEQUENCE (cycle 79): the same witness arms R5's order -
                    // the stroke that crossed WITH the plant is delivered fronts
                    // first, then as the rear drive (see the pose block's note).
                    getUpSeqArmed = true;
                    getUpSeqBorn = Time.fixedTime;
                }
                else if (plantHeld && !getUpPushPhase && phaseAge >= tuckSettle)
                {
                    getUpPushPhase = true;
                    getUpPhaseBorn = Time.fixedTime;
                    getUpPushCarried = false;
                }
                else if (getUpPushPhase
                         && (!plantHeld || phaseAge >= Mathf.Max(0.10f, getUpPushSeconds)))
                {
                    getUpPushPhase = false;
                    getUpPhaseBorn = Time.fixedTime;
                    getUpPushCarried = false;
                    getUpSeqArmed = false;
                    getUpSeqBorn = -1f;
                }
                else
                {
                    getUpPushCarried = false;
                }
            }
        }

        // Tuck = limbs folded under the trunk (the paws reach the floor under
        // the body); push = those same limbs extending against it. The rise
        // blend drives the extension, so the push is the old rise branch aimed
        // at a body that is still on the ground.
        // A contact shorter than the tuck's own settle age is not a floor to EXTEND against:
        // tuckDue is false for it, so the old line commanded the standing base pose on exactly the
        // 579 frames of the accepted get-up where a heap meets a fresh contact (mean load 0.70-1.24
        // m*g, max 8.32 m*g) with the trunk already rotating (|rate| mean 168-222, p95 471-528 deg/s).
        // The tuck is the pose that PUTS a paw down; hold it instead.
        bool pike = (tuckDue || (heap && getUpContactAge < tuckMin)) && !getUpPushPhase;
        bool rising = !pike;
        getUpPushingNow = heap && getUpPushPhase;
        // The tuck half's own witness (cycle 34, append-only to the row): the
        // counterpart of getUpPushingNow, so the belly get-up's own engagement can
        // be read frame by frame instead of inferred from the joint angles.
        getUpPikeNow = pike;

        float hipTarget;
        float kneeTarget;
        float shoulderTarget;
        float elbowTarget;
        // ─── THE CLIMB (cycle 37): the committed half's stroke depth ───
        // The pump's own weight, read off the pump's own measurement: the sign of
        // the rock rate is the way the trunk is turning (negative = toward the
        // tipping line, the direction the fall put it in), and its size is how
        // hard. 0 while the rock comes back, 1 at the gate's own deadband and
        // above — a continuous signal, never a flag.
        getUpClimbCommit = getUpInvertedNow
            ? Mathf.Clamp01(-getUpRockRate / GetUpClimbCommitDegPerSec) : 0f;
        // ─── THE RELEASE (cycle 38) ───
        // As the crossing completes, the deep half is given back (see the field's
        // own note): the release takes on the crossing's own depth — the hand-over's
        // own handDepth, 0 at the card's line and 1 at the band's lower edge — one
        // crossing at a time and holds it. The belly side (the c36 reach / plant /
        // push, live in exactly these frames: handOver 1.00 through the c37 row's
        // f3990-f4100 with pike/push alternating) then owns the trunk, and the trunk
        // that rolls past the line cannot be stroked further by the pump at the far
        // side's own wrap. Nothing else of the pump changes.
        if (bellySide)
        {
            float crossDepth = Mathf.SmoothStep(0f, 1f, handDepth);
            getUpClimbRelease = 1f - (1f - getUpClimbRelease) * (1f - crossDepth);
        }
        getUpClimbCommit *= 1f - getUpClimbRelease;
        float climbScale = 1f + GetUpClimbCommitGain * getUpClimbCommit;
        bool hipIn = false;
        if (pike)
        {
            getUpRiseBlend = 0f;
            float tuckHip = Mathf.Max(8f, getUpHipFlex);
            float tuckKnee = Mathf.Clamp(Mathf.Max(40f, getUpKneeFlex), 0f, 120f);
            float lead = Mathf.Max(0.05f, getUpKneeLeadSeconds);
            float pikeAge = getUpPikeBorn >= 0f ? Time.fixedTime - getUpPikeBorn : 0f;
            hipIn = pikeAge >= lead;
            if (inverted)
            {
                // Righting half: the driving pair folds under the trunk, the
                // other pair reaches STRAIGHT past its trunk end (knee / elbow
                // to 0) so the two halves of the body turn the trunk the same
                // way. No knee lead here — a swing is not a rise, and the shape
                // has to change fast enough for the reaction to add up.
                hipIn = true;
                // CYCLE 37: the committed half's stroke travels further — at a
                // torque the limbs already saturate, the stroke's own travel is
                // what turns into rotation (see GetUpClimbCommitDegPerSec).
                float climbHip = tuckHip * climbScale;
                float climbKnee = Mathf.Clamp(tuckKnee * climbScale, 0f, 120f);
                float pairHip, pairKnee, pairSho, pairElb;
                if (getUpRightFront)
                {
                    pairSho = shoulderBaseAngle + climbHip;
                    pairElb = climbKnee;
                    pairHip = hipBaseAngle + climbHip;
                    pairKnee = 0f;
                }
                else
                {
                    pairHip = hipBaseAngle - climbHip;
                    pairKnee = climbKnee;
                    pairSho = shoulderBaseAngle - climbHip;
                    pairElb = 0f;
                }
                // ─── THE COIL (cycle 71, v3): the fold is the accepted law's own ───
                // The coil's pose blends are withdrawn with v1/v2 (both measured
                // negative); the family now carries the push's own trigger only.
                hipTarget = pairHip;
                kneeTarget = pairKnee;
                shoulderTarget = pairSho;
                elbowTarget = pairElb;
            }
            else
            {
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
        }
        else
        {
            float riseT = Mathf.Max(0.2f, getUpRiseSeconds);
            getUpRiseBlend = Mathf.Clamp01(getUpRiseBlend + dt / riseT);
            if (inverted)
            {
                // Push half of the righting cycle: the driving pair EXTENDS
                // against whatever it has on the floor while the other pair
                // folds, so the ground reaction turns the trunk instead of
                // sliding it (the same extension took the trunk 50° → 162° in
                // 0.7 s on dog_gu14). Not the quiet base pose: that would set
                // all four limbs swinging together again.
                float pushHip = Mathf.Max(8f, getUpHipFlex);
                float pushKnee = Mathf.Clamp(Mathf.Max(40f, getUpKneeFlex), 0f, 120f);
                // CYCLE 37: the same committed-half depth on the extension.
                float climbPushHip = pushHip * climbScale;
                float climbPushKnee = Mathf.Clamp(pushKnee * climbScale, 0f, 120f);
                float pairHip, pairKnee, pairSho, pairElb;
                if (getUpRightFront)
                {
                    pairSho = shoulderBaseAngle - climbPushHip;
                    pairElb = 0f;
                    pairHip = hipBaseAngle - climbPushHip;
                    pairKnee = climbPushKnee;
                }
                else
                {
                    pairHip = hipBaseAngle + climbPushHip;
                    pairKnee = 0f;
                    pairSho = shoulderBaseAngle + climbPushHip;
                    pairElb = climbPushKnee;
                }
                // ─── THE COIL (cycle 71, v3): the pump's own pairs, unblended ───
                // v1 (all four extended) and v2 (fold-half coil) both let the trunk
                // rock between -105 and -244 without ever crossing: the accepted
                // pump's asymmetry is load-bearing. v3 keeps the accepted stroke
                // here, bit for bit; the family's only live lever is the push's own
                // trigger (the plant gate above).
                hipTarget = pairHip;
                kneeTarget = pairKnee;
                shoulderTarget = pairSho;
                elbowTarget = pairElb;
            }
            else
            {
                hipTarget = hipBaseAngle;
                kneeTarget = kneeBaseAngle;
                shoulderTarget = shoulderBaseAngle;
                elbowTarget = elbowBaseAngle;
            }
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
        // ─── THE ARRIVAL (cycle 40): the push's own drive ramps down ───
        // The stroke that carries the trunk up is the same two drives, and the
        // c39 rows measured that its terminal phase is where the pose is thrown
        // away (see the field's own note): so while the arrival's weight is live
        // the extension is delivered with less drive — the same targets, a longer
        // stroke — instead of with more (the c39 brake: sat 0.983 -> 0.934 and the
        // pose, the last pad and the card's line all unmoved). Signature only: at
        // weight 0 the factor is exactly 1, bit for bit.
        float arriveDrive = 1f - GetUpArriveFade * getUpArriveW;
        hipP *= arriveDrive;
        kneeP *= arriveDrive;

        // ─── THE CATCH (cycle 26) ───
        // The band: a heap whose trunk is at the card's line (|chestPitch| at
        // the inverted bar) or just past it. Everything above this line is the
        // accepted law; everything below it happens only in the band, which the
        // c25 landing row entered for exactly 10 frames (f4532-4541) before
        // falling back — so the pump, the walk and every pre-line pose are
        // untouched by construction.
        float clearR = rearRightPawCol != null
            ? rearRightPawCol.bounds.min.y - walkGroundTopY : float.MaxValue;
        float clearL = rearLeftPawCol != null
            ? rearLeftPawCol.bounds.min.y - walkGroundTopY : float.MaxValue;
        // The pads' own clearance is logged every get-up frame: it is the
        // catch's input AND the geometry the report has to carry.
        getUpCatchClear = Mathf.Min(clearR, clearL);
        // ─── THE FRONT REACH (cycle 36): the front pads' own clearance ───
        // The mirror of the line above, on the front pair — the column the front
        // limbs never had (the c34 hand-back's second lane). Read-only: the reach
        // below is a POSITION solve, not a clearance servo, so nothing here feeds
        // the control math.
        float clearFrontR = frontRightPawCol != null
            ? frontRightPawCol.bounds.min.y - walkGroundTopY : float.MaxValue;
        float clearFrontL = frontLeftPawCol != null
            ? frontLeftPawCol.bounds.min.y - walkGroundTopY : float.MaxValue;
        getUpFrontClear = Mathf.Min(clearFrontR, clearFrontL);
        // The catch's own latch. It ARMS GetUpCatchArmDeg above the crossing
        // (cycle 29: 105, not the line's 100 — see the constant): the trunk
        // falling THROUGH that threshold from the inverted side (last frame at
        // or over it, this one under it). The c28 row's own descent does that at
        // f4523, nine frames before it reaches the line, so the reach is already
        // closing when the trunk gets there. The fall-through on the way ONTO
        // the back — |pitch| rising to the bar, f813-825 on the c26 first run —
        // crosses the threshold upward and can never arm, while the c25 row's
        // entry (f4532) does.
        // It HOLDS while the trunk stays in the band: from the line down
        // GetUpCatchBandDeg (where the belly get-up takes over) to
        // GetUpCatchHoldDeg OVER it — that overshoot is the line's own wall, the
        // frames the c25 row fell back on (f4542 on, pitch 104 -> 120). The hold
        // is the point of the cycle: the press must not stop the moment the
        // trunk brushes the bar.
        bool catchInBand = heap
            && absPitch < getUpInvertedAbsPitch + GetUpCatchHoldDeg
            && absPitch >= getUpInvertedAbsPitch - GetUpCatchBandDeg;
        if (getUpCatchArmed == 0 && catchInBand
            && getUpCatchPrevAbsPitch >= GetUpCatchArmDeg
            && absPitch < GetUpCatchArmDeg)
            getUpCatchArmed = 1;
        if (!catchInBand) getUpCatchArmed = 0;
        getUpCatchPrevAbsPitch = absPitch;
        // ─── THE FOLD (cycle 32): the trunk's own clock ───
        // The arm (GetUpCatchArmDeg) is the frame this branch — the one that
        // plants the hind pad and levers — starts, so the fold's own weight
        // ramps from it and fades when the band lets go. A signal, 0..1, read
        // by everything below: the trunk's own law (DriveLumbarFold), the neck's
        // target, the rig's range (Dog.SetTrunkFold) and the recorder's columns.
        float foldWant = getUpCatchArmed == 1 ? 1f : 0f;
        float foldSlope = foldWant > getUpFoldWeight
            ? 1f / Mathf.Max(0.01f, GetUpFoldRampSeconds)
            : -1f / Mathf.Max(0.01f, GetUpFoldFadeSeconds);
        getUpFoldWeight = Mathf.Clamp01(getUpFoldWeight + foldSlope * dt);
        if (getUpFoldWeight > 0f)
            getUpFoldRan = true;
        bool foldRig = getUpFoldWeight > 0f
            || (getUpFoldRan
                && lumbarAngle > Dog.LumbarAcceptedMaxDeg + GetUpFoldCloseMarginDeg);
        if (foldRig != getUpFoldRigOpen)
        {
            getUpFoldRigOpen = foldRig;
            if (body != null)
                body.SetTrunkFold(foldRig);
        }
        getUpFoldRangeOpen = getUpFoldRigOpen ? 1 : 0;
        getUpFoldFlexLimitDeg = getUpFoldRigOpen
            ? Dog.LumbarFlexionMaxDeg : Dog.LumbarAcceptedMaxDeg;
        getUpFoldDeg = GetUpFoldLumbarDeg * getUpFoldWeight;
        getUpFoldNeckDeg = GetUpFoldNeckDeg * getUpFoldWeight;
        // The excursion past the brick's own clamp (the accepted +20): counted
        // only while the fold is running, so the number is the FOLD's, not the
        // solver's stretch elsewhere in the run.
        if (getUpFoldWeight > 0f && lumbarAngle > Dog.LumbarAcceptedMaxDeg)
        {
            getUpFoldPastStopFrames++;
            if (lumbarAngle > getUpFoldPastStopMax)
                getUpFoldPastStopMax = lumbarAngle;
        }
        getUpFoldPastStopMaxDeg = getUpFoldPastStopMax;
        float hipTargetL = hipTarget, hipTargetR = hipTarget;
        float kneeTargetL = kneeTarget, kneeTargetR = kneeTarget;
        // ─── THE SPLIT (cycle 30): ONE BRANCH, TWO CLOCKS ───
        // The c29 row armed this whole branch at GetUpCatchArmDeg and both halves
        // acted at once: the fold put the pad on the floor ON TIME (the clearance
        // closed 0.338 -> 0.047 m in 11 frames, the RR pad grounded f4535, three
        // frames before the c28 tip f4538) and the front-limb brace plus the P
        // boosts turned the roll 5-6 frames later — at 101.99, half a degree SHORT
        // of the line, so the crossing never happened (|pitch| <= 100 held 0
        // frames, c28: 10). The branch is read on the two clocks the c29 report
        // named, both from signals the law already carries, neither of them a new
        // flag:
        //   (1) THE FOLD — the back-plant solve, its rump press and its witnesses:
        //       armed at GetUpCatchArmDeg (105, kept from c29). It is a POSITIONING
        //       motion: it needs the trunk's own nine frames of warning and it does
        //       not brake (c29: the clearance closed to 0.047 m with no contact).
        //   (2) THE FRONT-LIMB BRACE and the P BOOSTS — the crossing's own clock:
        //       the line, getUpInvertedAbsPitch (100, the latch's original arm).
        //       Their reaction on the trunk IS the brake (c29 report 3a: the elbows
        //       14 -> 91-110 and the boosts, against a fold that only re-poses the
        //       limb), so it must arrive where the trunk's own hesitation is past
        //       and the pad is already down; arriving at 105 is what killed the
        //       crossing. Read as a continuous weight, not a flag: 0 at or above
        //       the line, 1 GetUpCatchBraceDeg past it (smoothstepped), so every
        //       degree of the crossing buys brace and the descent to the line
        //       carries none.
        float braceW = Mathf.SmoothStep(0f, 1f,
            Mathf.Clamp01((getUpInvertedAbsPitch - absPitch) / GetUpCatchBraceDeg));
        if (getUpCatchArmed == 1)
        {
            bool padR = rearRightGrounded, padL = rearLeftGrounded;
            getUpCatchArmed = 1;
            getUpCatchPad = padR || padL ? 1 : 0;
            getUpCatchPaw = padR ? (padL ? 3 : 0) : (padL ? 1 : -1);
            getUpCatchPush = getUpCatchPad;
            if (getUpCatchPad == 1 && getUpCatchDev == 0f)
                getUpCatchDev = absPitch - 180f;      // the dev at the touch
            // THE REACH, per hind leg (cycle 27: THE BACK-PLANT) — the c26
            // catch aimed the other way. The sole's target is the slab
            // GetUpCatchBackM BEHIND the CoM: the floor BEHIND the hip line
            // instead of in front of it. The chord from the hip's own pivot to
            // that target gives the thigh's world tilt and the knee's fold
            // (SolveWalkLegIk, the same law of cosines the walk reads); a chord
            // steeper back than the hip hinge's own +-90 is refused by the
            // hinge, so the hip is clamped and the shin alone then folds to the
            // slab — the pad lands behind the hip line in both branches. Both
            // sides reach; the pad that finds the floor is the one the report
            // names.
            hipTargetR = CatchBack(padR ? -GetUpCatchPressM : GetUpCatchSoleM,
                rearRightHipJoint, rearRightKneeJoint, out kneeTargetR);
            hipTargetL = CatchBack(padL ? -GetUpCatchPressM : GetUpCatchSoleM,
                rearLeftHipJoint, rearLeftKneeJoint, out kneeTargetL);
            hipTarget = Mathf.Max(hipTargetL, hipTargetR);
            kneeTarget = Mathf.Max(kneeTargetL, kneeTargetR);
            // The witness: the lower hind pad's sole and that leg's own hip
            // pivot, signed against the CoM (read-only — the touch's numbers).
            bool soleFromR = clearR <= clearL;
            getUpCatchSoleCom = CatchSoleX(soleFromR ? rearRightPawJoint : rearLeftPawJoint)
                - comPosition.x;
            getUpCatchHipCom = CatchPivot(soleFromR ? rearRightHipJoint : rearLeftHipJoint).x
                - comPosition.x;
            // ─── THE RUMP-CORNER HOLD (cycle 28) ───
            // The witness the rump has never had (c27 report 6): the pelvis's own
            // rear corner in the world — its height over the slab and its x
            // against the CoM — read from the pelvis's own box, no pose assumed.
            RumpCorners(out Vector2 rumpCorner, out Vector2 lowCorner);
            getUpRumpClear = rumpCorner.y - walkGroundTopY;
            getUpRumpCom = rumpCorner.x - comPosition.x;
            getUpRumpLowClear = lowCorner.y - walkGroundTopY;
            getUpRumpLowCom = lowCorner.x - comPosition.x;
            // THE PRESS: a clearance servo on that corner, driven by the hind
            // hips' FLEXOR side. Nothing while the corner is on the slab;
            // GetUpRumpPressDeg of extra flexion when it is GetUpRumpPressM or
            // more above it. Bounded by the hip hinge's own range, band-only: the
            // press folds the hind limbs instead of letting them lever the pelvis
            // up, so the body's weight stays on the rump and the corner keeps the
            // slab the pump's next stroke needs.
            float rumpLack = Mathf.Clamp01((getUpRumpClear - GetUpRumpHoldM) / GetUpRumpPressM);
            float rumpPress = GetUpRumpPressDeg * rumpLack;
            hipTargetR = Mathf.Clamp(hipTargetR - rumpPress,
                -GetUpCatchHipMaxDeg, GetUpCatchHipMaxDeg);
            hipTargetL = Mathf.Clamp(hipTargetL - rumpPress,
                -GetUpCatchHipMaxDeg, GetUpCatchHipMaxDeg);
            // Clock 2 (the crossing's own): the brace and the boosts, weighted by
            // braceW above. At the line the weight is 0, so both targets are
            // exactly the law the descent already ran on — the front-limb brace is
            // absent above the line BY CONSTRUCTION, not by a second threshold.
            shoulderTarget = Mathf.Lerp(shoulderTarget,
                shoulderBaseAngle + Mathf.Max(8f, getUpHipFlex), braceW);
            elbowTarget = Mathf.Lerp(elbowTarget,
                Mathf.Clamp(Mathf.Max(40f, getUpKneeFlex), 0f, 120f), braceW);
            // The drives: the brace's own (the knee P is also the elbow's drive).
            // The fold's own travel is the base law's — it is a positioning motion
            // and the c29 numbers show it closes on the base drive.
            hipP = Mathf.Max(hipP, 4f * braceW);
            kneeP = Mathf.Max(kneeP, 5f * braceW);
            // ─── THE CLAWS (cycle 31) ───
            // The grip, on this branch's own clock: the pad's measured load and
            // this branch's plant command. Placed last so the swap it makes (the
            // pad's own contact material) is in the collider before this step's
            // solve and every target above is already set — the grip and the
            // reach are one motion (reach early, grip, the trunk arrives, the
            // lever). See UpdateClawGrip / ClawPad / ReadClawContact below.
            UpdateClawGrip();
        }
        else
        {
            getUpCatchArmed = 0;
            getUpCatchPad = 0;
            getUpCatchPaw = -1;
            getUpCatchPush = 0;
            getUpCatchDev = 0f;
            getUpCatchSoleCom = 0f;
            getUpCatchHipCom = 0f;
            getUpRumpClear = 0f;
            getUpRumpCom = 0f;
            getUpRumpLowClear = 0f;
            getUpRumpLowCom = 0f;
            // CYCLE 31: the band let go — so do the claws, the same step, with
            // no tear recorded (the pad was not ripped out; the branch ended).
            ReleaseClaws();
        }

        // ─── THE HAND-OVER (cycle 34): the front reach, applied LAST ───
        // The catch's block above owns the band and the hind pads; the front limbs
        // are free wherever the catch no longer commands them, and the hand-over is
        // exactly that: the belly side of the line with the plant's own hold gone.
        // The reach is weighted by the hand-over's own signal, so at weight 0 the
        // accepted tuck/brace targets stand untouched, and at weight 1 the front
        // paws are commanded to the slab in front of the chest — the replacement
        // contact the c33 row never had. The drives are the catch's own (a P boost
        // on the limb that has to travel, none otherwise).
        // ─── CYCLE 44: THE SEPARATOR THAT SURVIVES THE TUMBLE ───
        // The reach's weight above is the belly-side phase, and that phase needs
        // getUpBellySeen — which EnterGettingUp clears. On the c40 row the get-up that
        // starts at f3527 therefore commands NO front reach, and the frames it does not
        // own are exactly the arrival's own frames: the trunk at the stand bar with the
        // pads still hanging (frontClearM > 0.05 on 979 frames of f3527-f5000, 306 of
        // them with a front pad on the floor and only 13 on four paws). The withdrawn
        // c43 pick gated on the pads' clearance alone; its own first divergence was
        // f699, INSIDE the fall, because neither a pitch envelope nor the pads'
        // clearance can tell the FALL from the FLOOR (the tumble's 172 frames bring the
        // trunk back through the flat band with the pads hanging). The arm here is the
        // phase the round named, getUpCrossedSeen — the episode's own crossing, kept —
        // plus the law's own stand bar (getUpStandDrop, the same reference the hold's
        // arm reads). Both are dark before the fall's own onset by construction.
        // Measured on dog_c40_w04: 93 live frames, every one of them inside the run's
        // own end, f4265-f4955, and none anywhere else in the 5001-frame row.
        float frontReachW = getUpHandOver;
        if (getUpCrossedSeen && !inverted && trunkBar <= getUpStandDrop)
        {
            float frontPlantW = Mathf.Clamp01((getUpFrontClear - GetUpFrontPlantDeadM)
                / GetUpFrontPlantBandM);
            float frontFlatW = Mathf.Clamp01(1f - Mathf.Abs(chestPitch)
                / GetUpFrontPlantFlatDeg);
            frontReachW = Mathf.Max(frontReachW,
                frontPlantW * frontFlatW * (1f - getUpHoldW));
        }
        // Mathf.Max above: every frame the hand-over already acted on is bit-for-bit
        // the accepted law (at weight 0 the tuck/brace targets stand untouched).
        if (frontReachW > 0f)
        {
            float shFrontR = CatchFront(GetUpCatchSoleM, frontRightShoulderJoint,
                frontRightElbowJoint, out float elbFrontR);
            float shFrontL = CatchFront(GetUpCatchSoleM, frontLeftShoulderJoint,
                frontLeftElbowJoint, out float elbFrontL);
            shoulderTarget = Mathf.Lerp(shoulderTarget, Mathf.Max(shFrontR, shFrontL),
                frontReachW);
            elbowTarget = Mathf.Lerp(elbowTarget, Mathf.Max(elbFrontR, elbFrontL),
                frontReachW);
            hipP = Mathf.Max(hipP, 4f * frontReachW);
            kneeP = Mathf.Max(kneeP, 5f * frontReachW);
        }

        // ─── THE HOLD (cycle 39): the brake on the arrival ───
        // The first c39 row (dog_c39_w04) measured the pose itself: it is not a
        // settled stand but the TAIL of the rise's own extension stroke — kneeL
        // 51.7 -> 15.7 deg and elbL 68.3 -> 17.2 deg in the 8 frames the four pads
        // are down (against the quiet stand's own 29.5 / 29.3), the CoM rising
        // 1.4 m/s and running 1.0 m/s forward, the pads off the floor at f2651 and
        // the trunk past the card's line by f2711. The stroke that carries the
        // trunk up is torque-saturated (sat mean 0.983) and its own overshoot is
        // what topples the dog: so while the hold is live the hinges are DAMPED —
        // the same drives, resisting the rate that the momentum is riding. The
        // weight is the hold's own (the trunk's arrival at the bar with the pads
        // down), so at the pose it is 0.1 -> 1.0 over GetUpHoldRampSeconds and
        // nowhere else in the episode.
        float hipD = hipDGain * (1f + GetUpHoldBrakeGain * getUpHoldW);
        float kneeD = kneeDGain * (1f + GetUpHoldBrakeGain * getUpHoldW);

        // ─── THE REACH (cycle 72): the touch becomes a PLANT ───
        // Applied LAST, exactly like the hand-over's own front reach: every frame
        // the latch is dark is bit-for-bit the accepted pose (and with the family's
        // flag off nothing in this block is even read). The c71 rows: a pad that
        // finds the floor lets go 1-2 frames later (contact 5-14 % of the inverted
        // window, "loaded" frames — impulse > 20 N·s — exactly 0), so plantHeld,
        // the extension's own trigger, sees a plant only by luck. The latch holds
        // the touch it was given: the limb is solved (the same law of cosines the
        // catch and the hand-over read) toward the slab UNDER the CoM — the target
        // is (comX, slab) — for GetUpReachHoldSeconds, and only a NEW touch re-arms
        // it, so the hold cannot become a freeze. Nothing that is NOT touching is
        // re-posed: the pump's ballistic fold/straight asymmetry stays the
        // accepted law's own (c71 §2: replacing it cost 0 crossings in 20.8 s).
        if (coilWanted)
        {
            // Cycle 73: the hold lives only above the line's own margin — released before the
            // trunk reaches the tipping line, so the crossing carries no latched strut; the
            // deep-band plant (the c72 goal) stays. See GetUpReachLineGuardDeg's own note.
            if (!inverted || Mathf.Abs(chestPitch) < getUpInvertedAbsPitch + GetUpReachLineGuardDeg)
            {
                for (int i = 0; i < 4; i++)
                    getUpReachLatchedAt[i] = -1f;
            }
            else
            {
                bool[] downPaw = { frontRightGrounded, frontLeftGrounded, rearRightGrounded, rearLeftGrounded };
                for (int i = 0; i < 4; i++)
                {
                    if (downPaw[i] && !getUpReachWasDown[i])
                        getUpReachLatchedAt[i] = Time.fixedTime;   // a FRESH touch arms the hold
                    getUpReachWasDown[i] = downPaw[i];
                    if (getUpReachLatchedAt[i] >= 0f
                        && Time.fixedTime - getUpReachLatchedAt[i] > GetUpReachHoldSeconds)
                        getUpReachLatchedAt[i] = -1f;
                }
                bool holdR = getUpReachLatchedAt[2] >= 0f;
                bool holdL = getUpReachLatchedAt[3] >= 0f;
                if (holdR)
                    hipTargetR = ReachHoldPad(GetUpCatchSoleM, rearRightHipJoint,
                        rearRightKneeJoint, pelvisTilt, rearThighLen, rearShinLen, out kneeTargetR);
                if (holdL)
                    hipTargetL = ReachHoldPad(GetUpCatchSoleM, rearLeftHipJoint,
                        rearLeftKneeJoint, pelvisTilt, rearThighLen, rearShinLen, out kneeTargetL);
                if (holdR || holdL)
                {
                    hipTarget = Mathf.Max(hipTargetL, hipTargetR);
                    kneeTarget = Mathf.Max(kneeTargetL, kneeTargetR);
                }
                // The front limbs: the same hold, the hand-over's own arm shape
                // (the pose commands one shoulder/elbow target for both sides).
                float shF = shoulderTarget, elbF = elbowTarget;
                if (getUpReachLatchedAt[0] >= 0f)
                {
                    float shR = ReachHoldPad(GetUpCatchSoleM, frontRightShoulderJoint,
                        frontRightElbowJoint, chestPitch, frontUpperLen, frontLowerLen, out float elbR);
                    shF = Mathf.Max(shF, shR);
                    elbF = Mathf.Max(elbF, elbR);
                }
                if (getUpReachLatchedAt[1] >= 0f)
                {
                    float shL = ReachHoldPad(GetUpCatchSoleM, frontLeftShoulderJoint,
                        frontLeftElbowJoint, chestPitch, frontUpperLen, frontLowerLen, out float elbL);
                    shF = Mathf.Max(shF, shL);
                    elbF = Mathf.Max(elbF, elbL);
                }
                shoulderTarget = shF;
                elbowTarget = elbF;
            }
        }

        // ─── THE SEQUENCE (cycle 79): fronts sit, then the rear drive ───
        // R5's order, delivered inside the CARRY's own window (armed by the
        // re-clock above; nothing here is read on any other frame). The pair
        // that crossed the line with the plant under it is delivered fronts
        // first - the front pair's extension is the sit (the chest turns up
        // over the folded rears) - then the REAR pair's drive off the plant
        // (the lift). Applied AFTER the catch, the hand-over and the reach, on
        // the same targets the joints read below: while the sequence is live
        // the catch's brace (the fronts folded - the crossing's own brake) and
        // its back-plant solve (the rears reaching behind the CoM) give the
        // stroke its floor; on every unwitnessed frame they are the accepted
        // law, bit for bit. The weights are clocks bounded by the plant's own
        // measured life (0.05-0.09 s): ramp in, hold, never step.
        if (coilWanted && getUpSeqArmed)
        {
            float seqAge = Time.fixedTime - getUpSeqBorn;
            float seqFrontW = Mathf.SmoothStep(0f, 1f,
                Mathf.Clamp01(seqAge / GetUpSeqSitSeconds));
            float seqRearW = Mathf.SmoothStep(0f, 1f,
                Mathf.Clamp01((seqAge - GetUpSeqSitSeconds) / GetUpSeqDriveRampSeconds));
            shoulderTarget = Mathf.Lerp(shoulderTarget, shoulderBaseAngle, seqFrontW);
            elbowTarget = Mathf.Lerp(elbowTarget, elbowBaseAngle, seqFrontW);
            hipTargetL = Mathf.Lerp(hipTargetL, hipBaseAngle, seqRearW);
            hipTargetR = Mathf.Lerp(hipTargetR, hipBaseAngle, seqRearW);
            kneeTargetL = Mathf.Lerp(kneeTargetL, kneeBaseAngle, seqRearW);
            kneeTargetR = Mathf.Lerp(kneeTargetR, kneeBaseAngle, seqRearW);
        }

        // ─── THE FLIP (cycle 80): the limbs held at the tuck ───
        // The flip's own pose (armed by the supine gate above; nothing here is
        // read on any other frame): the four limbs at the belly-side tuck solve
        // - the same targets the accepted pump commands in its pike (the limbs
        // pull to the body, paws to the chest), held through the roll. The block
        // rides the flip's own weight: the arm eases the pose in over the tuck's
        // clock, the hand-over returns the accepted law's own targets over the
        // release's.
        if (coilWanted && getUpFlipW > 0f)
        {
            float flipTuckHip = Mathf.Max(8f, getUpHipFlex);
            float flipTuckKnee = Mathf.Clamp(Mathf.Max(40f, getUpKneeFlex), 0f, 120f);
            float flipW = Mathf.Clamp01(getUpFlipW);
            shoulderTarget = Mathf.Lerp(shoulderTarget, shoulderBaseAngle + flipTuckHip, flipW);
            elbowTarget = Mathf.Lerp(elbowTarget, flipTuckKnee, flipW);
            hipTargetL = Mathf.Lerp(hipTargetL, hipBaseAngle - flipTuckHip, flipW);
            hipTargetR = Mathf.Lerp(hipTargetR, hipBaseAngle - flipTuckHip, flipW);
            kneeTargetL = Mathf.Lerp(kneeTargetL, flipTuckKnee, flipW);
            kneeTargetR = Mathf.Lerp(kneeTargetR, flipTuckKnee, flipW);
        }

        float pawBalance = PawComCommand();
        DrivePaw(frontRightPawJoint, frontRightPawFlexor, frontRightPawExtensor, frontRightGrounded, pawBalance);
        DrivePaw(frontLeftPawJoint, frontLeftPawFlexor, frontLeftPawExtensor, frontLeftGrounded, pawBalance);
        DrivePaw(rearRightPawJoint, rearRightPawFlexor, rearRightPawExtensor, rearRightGrounded, pawBalance);
        DrivePaw(rearLeftPawJoint, rearLeftPawFlexor, rearLeftPawExtensor, rearLeftGrounded, pawBalance);

        ControlJointStable(rearLeftHipJoint, hipTargetL, hipP, hipD,
            rearLeftHipFlexor, rearLeftHipExtensor, ref rearLeftHipInertia);
        ControlJointStable(rearRightHipJoint, hipTargetR, hipP, hipD,
            rearRightHipFlexor, rearRightHipExtensor, ref rearRightHipInertia);
        ControlJointStable(frontLeftShoulderJoint, shoulderTarget, hipP, hipD,
            frontLeftShoulderFlexor, frontLeftShoulderExtensor, ref frontLeftShoulderInertia);
        ControlJointStable(frontRightShoulderJoint, shoulderTarget, hipP, hipD,
            frontRightShoulderFlexor, frontRightShoulderExtensor, ref frontRightShoulderInertia);

        // Open the V only on rise with paws down — HeightLift toward
        // spawn inverted-V during pike fights the tuck.
        float lift = 0f;
        if (rising && getUpRiseBlend > 0.15f && AllPawsGrounded())
            // CYCLE 39: the rise's own pelvis lift is KEPT. The hold's first row
            // (dog_c39_w04) faded it by the hold's weight and measured what that
            // costs: the height servo's D term IS the law's one brake on the
            // arrival (lift = heightP * pelvisErr − heightD * pelvisVelY, and the
            // trunk arrives at the bar rising 1.4 m/s), so fading it took the
            // crossing 15 frames EARLIER (past the card's line at f2711 against
            // c38's f2726) and left the last-5-s mean |pitch| worse (168.70
            // against 151.67). The hold's brake is added below instead.
            lift = HeightLiftDeg();
        kneeTarget = Mathf.Clamp(kneeTarget - lift, 0f, 120f);
        // The back-plant folds each hind knee by its own pad's clearance (c26
        // made the hip drive per-side; the pair reads one target otherwise).
        kneeTargetL = Mathf.Clamp(kneeTargetL - lift, 0f, 120f);
        kneeTargetR = Mathf.Clamp(kneeTargetR - lift, 0f, 120f);
        elbowTarget = Mathf.Clamp(elbowTarget - lift, 0f, 120f);

        ControlJointStable(rearLeftKneeJoint, kneeTargetL, kneeP, kneeD,
            rearLeftKneeFlexor, rearLeftKneeExtensor, ref rearLeftKneeInertia);
        ControlJointStable(rearRightKneeJoint, kneeTargetR, kneeP, kneeD,
            rearRightKneeFlexor, rearRightKneeExtensor, ref rearRightKneeInertia);
        ControlJointStable(frontLeftElbowJoint, elbowTarget, kneeP, kneeD,
            frontLeftElbowFlexor, frontLeftElbowExtensor, ref frontLeftElbowInertia);
        ControlJointStable(frontRightElbowJoint, elbowTarget, kneeP, kneeD,
            frontRightElbowFlexor, frontRightElbowExtensor, ref frontRightElbowInertia);

        bool lumbarReady = lumbarDelaySeconds <= 0f || spawnedFixedTime < 0f
            || Time.fixedTime - spawnedFixedTime >= lumbarDelaySeconds;
        if (lumbarReady)
        {
            if (useLumbarJointRest)
                ControlJointStable(lumbarJoint, lumbarJointRestDeg, lumbarPGain, lumbarDGain,
                    lumbarFlexor, lumbarExtensor, ref lumbarInertia);
            else if (getUpFoldWeight > 0f)
                // Cycle 32: the fold owns the trunk's drive (inside, the two
                // laws are the same saturated signal — see DriveLumbarFold).
                DriveLumbarFold(chestTargetTilt);
            else
                DriveLumbarToWorld(chestTargetTilt);
        }

        ControlJointStable(tailJoint, IdleTailTarget(), tailPGain, tailDGain,
            tailFlexor, tailExtensor, ref tailInertia);
        DriveJaw();
        // ─── THE FLIP (cycle 80): the root drive ───
        DriveFlipRoot();

        if (vestibular != null)
        {
            float neckGoal = IdleNeckWorldTarget();
            // ─── THE FOLD (cycle 32): the neck follows the curl ───
            // The head tucks toward the chest WITH the trunk: the neck's world
            // target becomes the chest's own tilt minus the commanded flexion
            // (GetUpFoldNeckDeg), weighted by the fold's own clock — so the curl
            // is a joint angle the neck can hold, not a world pose it cannot
            // reach. At zero weight this is the accepted IdleNeckWorldTarget().
            if (getUpFoldNeckDeg > 0f)
                neckGoal = Mathf.Lerp(neckGoal, chestPitch - getUpFoldNeckDeg,
                    getUpFoldWeight);
            ControlSegmentToWorldUpright(
                vestibular.GetNeckTilt(), vestibular.GetNeckAngularVelocity(),
                neckJoint, neckFlexor, neckExtensor, ref neckEffectiveInertia, neckGoal);
            ControlSegmentToWorldUpright(
                vestibular.GetHeadTilt(), vestibular.GetHeadAngularVelocity(),
                headJoint, headFlexor, headExtensor, ref headEffectiveInertia, neckGoal);
        }
    }

    // ─── THE FLIP (cycle 80): the root drive ───
    // The scripted phase's own torque pair. The total torque is the root
    // inertia (measured by ComputeFlipInertia) times the commanded angular
    // acceleration, split between the two trunk bodies in proportion to their
    // own inertia - so the trunk turns as ONE root and the lumbar hinge never
    // sees a relative load. The PD tracks the script's (theta, omega) and its
    // budget is the roll's own alpha clamp (the tuck's freeze uses the hold's,
    // with the gentle brake). Nothing is applied unless the flip is live; the
    // two trunk bodies are cached at Bind. AddTorque is the same authority a
    // Muscle spends - here it is spent on the root, only while the flip is
    // live.
    private void DriveFlipRoot()
    {
        if (!getUpFlipUsed || !getUpFlipRolling) return;
        if (flipChestRb == null || flipPelvisRb == null) return;
        float flipErr = getUpFlipTargetU - getUpFlipPitchU;
        float flipRate = flipChestRb.angularVelocity;
        float flipKd = getUpFlipInRoll ? GetUpFlipKd : GetUpFlipHoldKd;
        float flipAlpha = getUpFlipTargetAcc + GetUpFlipKp * flipErr
            + flipKd * (getUpFlipTargetRate - flipRate);
        float flipLim = getUpFlipInRoll ? GetUpFlipRollAlphaMax : GetUpFlipHoldAlphaMax;
        flipAlpha = Mathf.Clamp(flipAlpha, -flipLim, flipLim);
        float flipSplit = flipChestRb.inertia + flipPelvisRb.inertia;
        if (flipSplit <= 0f || getUpFlipInertia <= 0f) return;
        float flipTorque = getUpFlipInertia * (flipAlpha * Mathf.Deg2Rad)
            * GetUpFlipTorqueGain;
        flipChestRb.AddTorque(flipTorque * (flipChestRb.inertia / flipSplit),
            ForceMode2D.Force);
        flipPelvisRb.AddTorque(flipTorque * (flipPelvisRb.inertia / flipSplit),
            ForceMode2D.Force);
    }

    // The root inertia: the whole heap's moment about its own mass centre
    // (Sum J_i + m_i*r_i^2) - the scale the root drive's torque is built from,
    // so the flip's authority is the body's own physics, not a blind number.
    // Measured once per flip (the pose changes little during one).
    private void ComputeFlipInertia()
    {
        getUpFlipInertia = 0f;
        if (bodies == null || bodies.Length == 0) return;
        Vector2 com = Vector2.zero;
        float massSum = 0f;
        for (int i = 0; i < bodies.Length; i++)
        {
            Rigidbody2D b = bodies[i];
            if (b == null) continue;
            com += b.worldCenterOfMass * b.mass;
            massSum += b.mass;
        }
        if (massSum <= 0f) return;
        com /= massSum;
        float sum = 0f;
        for (int i = 0; i < bodies.Length; i++)
        {
            Rigidbody2D b = bodies[i];
            if (b == null) continue;
            Vector2 r = b.worldCenterOfMass - com;
            sum += b.inertia + b.mass * r.sqrMagnitude;
        }
        getUpFlipInertia = sum;
    }

    // ─── THE CATCH (cycle 27): the back-plant's own geometry ───
    // The c26 solve took the leg straight DOWN the hip's own vertical: the pad was
    // commanded to the straight-leg angle that put it on the target sole, tilted
    // FORWARD of the hip — and the landing row measured what that cost (dog_c26_w04
    // f4540: the sole touched 0.20 m in front of the CoM and the ground reaction
    // fought the righting for the whole crossing). This solve aims the same triangle
    // the other way: the sole's target is the slab GetUpCatchBackM BEHIND the CoM,
    // and the pose that puts it there comes out of the two-link law of cosines the
    // walk's own IK reads (SolveWalkLegIk) — the chord from the hip's pivot to the
    // target is the thigh's world tilt, the fold closes the shin on it. The frame is
    // the one the joint identity lives in: thighWorld = pelvisTilt - hipAngle, down
    // positive, +x forward (Dog.cs's standing pose: hip -12 -> thigh world +12, knee
    // +24 -> shin world -12). A chord steeper back than the hip hinge's own +-90
    // (Dog.cs ConnectTo(pelvis, ..., -90, 90)) cannot be posed, so the hip is clamped
    // to the hinge and the shin alone folds to the slab: the hip's height from the
    // pivot's own transform, minus the thigh's share, is the vertical the shin has to
    // cover, and the negative root of its acos is the fold that lands the pad BEHIND
    // the knee (the positive root is the forward one c26 took). Returns the hip's
    // joint angle; the knee's fold rides out through kneeAng.
    private float CatchBack(float sole, HingeJoint2D hip, HingeJoint2D knee,
        out float kneeAng)
    {
        float hipAng = hip != null ? hip.jointAngle : 0f;
        kneeAng = knee != null ? knee.jointAngle : 0f;
        if (hip == null)
            return hipAng;
        Vector2 hipPt = CatchPivot(hip);
        if (SolveWalkLegIk(hipPt,
                new Vector2(comPosition.x - GetUpCatchBackM,
                    walkGroundTopY + sole + GetUpCatchPawDropM),
                rearThighLen, rearShinLen, out float thighW, out float kneeFlex))
        {
            hipAng = pelvisTilt - thighW;
            kneeAng = kneeFlex;
        }
        if (hipAng > GetUpCatchHipMaxDeg)
        {
            // The hinge owns the chord: the thigh takes the hinge's own tilt and the
            // shin alone folds to the slab.
            hipAng = GetUpCatchHipMaxDeg;
            float thigh = pelvisTilt - hipAng;
            float overFloor = hipPt.y - walkGroundTopY - sole - GetUpCatchPawDropM
                - rearThighLen * Mathf.Cos(thigh * Mathf.Deg2Rad);
            float cosShin = Mathf.Clamp(overFloor / Mathf.Max(0.01f, rearShinLen), -1f, 1f);
            kneeAng = thigh + Mathf.Acos(cosShin) * Mathf.Rad2Deg;
        }
        hipAng = Mathf.Clamp(hipAng, -GetUpCatchHipMaxDeg, GetUpCatchHipMaxDeg);
        kneeAng = Mathf.Clamp(kneeAng, 0f, GetUpCatchKneeMaxDeg);
        return hipAng;
    }

    // ─── THE HAND-OVER (cycle 34): the FRONT reach ───
    // The catch's back-plant puts the HIND pad on the slab before the crossing.
    // The c33 landing row then measured the wall the c33 worker's hand-back named:
    // at the hand-over that plant's own hold is gone (the RR pad left the slab at
    // f3860 and no front paw ever came down — 0 of 322 belly-side frames carried
    // one), so the crossing had NO floor to extend against and the trunk rocked
    // back over the line. This is the same law of cosines aimed the other way: the
    // sole's target is the slab GetUpHandFrontM in FRONT of the CoM, the chord from
    // the front shoulder's own pivot sets the upper limb's world tilt and the fold
    // closes the lower one on it — the pose that brings the front paws under the
    // chest, which is the milestone the card asks for before the rise ("belly down,
    // paws under"). The front limb's frame is the walk's own (TryWalkPeelIk:
    // parentTilt = chestPitch, joint = parent - world tilt), and the bounds are the
    // hinges' own (the shoulder +-90 like the hip, the elbow 0..120 like the knee),
    // so nothing this solve can ask for is outside the rig's accepted ranges.
    private float CatchFront(float sole, HingeJoint2D shoulder, HingeJoint2D elbow,
        out float elbowAng)
    {
        float shAng = shoulder != null ? shoulder.jointAngle : shoulderBaseAngle;
        elbowAng = elbow != null ? elbow.jointAngle : elbowBaseAngle;
        if (shoulder == null)
            return shAng;
        Vector2 shPt = CatchPivot(shoulder);
        Vector2 tgt = new Vector2(comPosition.x + GetUpHandFrontM,
            walkGroundTopY + sole + GetUpCatchPawDropM);
        if (SolveWalkLegIk(shPt, tgt, frontUpperLen, frontLowerLen,
                out float upperW, out float elbowFlex))
        {
            shAng = chestPitch - upperW;
            elbowAng = elbowFlex;
        }
        else
        {
            // ─── CYCLE 36: THE REACH'S OWN LIMIT ───
            // The chord is longer than frontUpperLen + frontLowerLen: the solve
            // refuses and the c34 law left the limb on its tuck/brace pose, i.e.
            // the reach commanded NOTHING exactly where it was needed. The c36 row's
            // own geometry (measured off the accepted row, the new frontClearM
            // column) says that is not an edge case at the hand-over's trunk angles:
            // at |pitch| 74-100 the front shoulder's pivot sits 0.53-0.69 m over the
            // slab, so the sole is 0.21-0.36 m above it and NO pose of a 0.34 m limb
            // can put it on the floor — the seven |pitch| bands measured say the
            // front pads only ever touch with the pivot at 0.19-0.33 m. So the limb
            // is not held back: it is aimed STRAIGHT along the chord, at the target,
            // at full extension (elbow 0) — the farthest the reach can go, and a
            // committed sweep of the front limbs toward the floor (their own
            // reaction is a forward/down pitch of the trunk, the same inertial drive
            // the righting pump runs on) instead of a folded limb parked in the air.
            // The moment the trunk brings the shoulder inside the limb's own reach
            // the branch above takes over again and the sole lands on the target:
            // this is a continuous signal, not a mode.
            Vector2 chord = tgt - shPt;
            float chordTilt = Mathf.Atan2(chord.x, -chord.y) * Mathf.Rad2Deg;
            shAng = chestPitch - chordTilt;
            elbowAng = 0f;
        }
        shAng = Mathf.Clamp(shAng, -GetUpCatchHipMaxDeg, GetUpCatchHipMaxDeg);
        elbowAng = Mathf.Clamp(elbowAng, 0f, GetUpCatchKneeMaxDeg);
        return shAng;
    }

    // The catch's own two points, in world space: the hip's pivot (where the chord
    // starts from, and the point the walk's IK reads) and the paw's sole point (the
    // point the recorder's pawSoleX columns read).
    private static Vector2 CatchPivot(HingeJoint2D hip)
    {
        return hip != null ? (Vector2)hip.transform.TransformPoint(hip.anchor) : Vector2.zero;
    }

    private static float CatchSoleX(HingeJoint2D paw)
    {
        return paw != null ? paw.transform.TransformPoint(paw.anchor).x : 0f;
    }

    // ─── THE REACH (cycle 72): the pad's own hold ───
    // The catch aims the HIND pad at the slab GetUpCatchBackM behind the CoM and
    // the hand-over aims the FRONT pad GetUpHandFrontM in front of it; both run on
    // the belly side. This is the same solve for the other state — the trunk still
    // on its back — and its target is the slab UNDER the CoM: the pad the latch
    // caught is solved back to (comX, walkGroundTopY + sole + GetUpCatchPawDropM),
    // which is where the coil's own reach is asked to bring it. The frame is the
    // same joint identity the two accepted solves read (joint = parentTilt − world
    // tilt: hipAng = pelvisTilt − thighW for the hind limb, shAng = chestPitch −
    // upperW for the front), and the c36 limit branch is kept: a chord outside the
    // limb is aimed STRAIGHT along it instead of refusing, so the hold is always a
    // commanded pose, never a dropped one. Returns the hip/shoulder angle; the
    // knee/elbow rides out through kneeAng.
    private float ReachHoldPad(float sole, HingeJoint2D hip, HingeJoint2D knee,
        float parentTilt, float l1, float l2, out float kneeAng)
    {
        float hipAng = hip != null ? hip.jointAngle : 0f;
        kneeAng = knee != null ? knee.jointAngle : 0f;
        if (hip == null)
            return hipAng;
        Vector2 hipPt = CatchPivot(hip);
        Vector2 tgt = new Vector2(comPosition.x,
            walkGroundTopY + sole + GetUpCatchPawDropM);
        if (SolveWalkLegIk(hipPt, tgt, l1, l2, out float thighW, out float kneeFlex))
        {
            hipAng = parentTilt - thighW;
            kneeAng = kneeFlex;
        }
        else
        {
            Vector2 chord = tgt - hipPt;
            float chordTilt = Mathf.Atan2(chord.x, -chord.y) * Mathf.Rad2Deg;
            hipAng = parentTilt - chordTilt;
            kneeAng = 0f;
        }
        hipAng = Mathf.Clamp(hipAng, -GetUpCatchHipMaxDeg, GetUpCatchHipMaxDeg);
        kneeAng = Mathf.Clamp(kneeAng, 0f, GetUpCatchKneeMaxDeg);
        return hipAng;
    }

    // ─── THE RUMP CORNER (cycle 28) ───
    // The pelvis's box in the world, read from the pelvis's own collider (a
    // BoxCollider2D the size of the pelvis, centred on the pelvis's transform).
    // Dog.cs's pelvis frame: local +x is the head's side and local +y the dog's
    // back (the hip anchors sit at local −y, under the box), so the box's
    // (−x, +y) corner is the RUMP — the corner the pump's rock pivots on when
    // the dog is on its back, and the one this cycle's hold presses. rump = that
    // corner; low = the box's lowest corner in the world (the corner that can
    // actually hold the slab). Both are world points; the caller signs them
    // against the CoM and the slab. No allocation, no Find.
    private void RumpCorners(out Vector2 rump, out Vector2 low)
    {
        rump = Vector2.zero;
        low = Vector2.zero;
        if (pelvisTf == null)
            return;
        Vector2 half;
        Vector2 off = Vector2.zero;
        if (pelvisBox != null)
        {
            half = pelvisBox.size * 0.5f;
            off = pelvisBox.offset;
        }
        else
        {
            half = body != null ? body.pelvisSize * 0.5f : new Vector2(0.11f, 0.065f);
        }
        rump = pelvisTf.TransformPoint(new Vector3(off.x - half.x, off.y + half.y, 0f));
        low = rump;
        Vector2 corner = pelvisTf.TransformPoint(new Vector3(off.x + half.x, off.y + half.y, 0f));
        if (corner.y < low.y) low = corner;
        corner = pelvisTf.TransformPoint(new Vector3(off.x - half.x, off.y - half.y, 0f));
        if (corner.y < low.y) low = corner;
        corner = pelvisTf.TransformPoint(new Vector3(off.x + half.x, off.y - half.y, 0f));
        if (corner.y < low.y) low = corner;
    }

    // ─── THE CLAWS (cycle 31) ───
    // The designer's order in code: while the catch's branch owns the band, a
    // hind pad whose own measured load is at or over GetUpClawEngageLoad has
    // its claws ENGAGED — the pad's collider carries Dog.ClawContactMaterial()
    // (the same collider, a friction the paw's own 0.4 cannot give) and the
    // solver's own friction holds the contact up to a FINITE capacity
    // (mu_pair * Fn, published every frame). The grip ends on three measured
    // events and only them: the pad's load falls to nothing (it is off the
    // floor — a plain release), the pad's own sole point drifts
    // GetUpClawSlipM from the anchor the grip was set on (the RIP-OUT: the
    // claws tore out of the ground — the tear latches while the pad stays
    // loaded, so a torn pad cannot re-dig without a fresh press), or this
    // branch ends (the controller's own release, no tear recorded).
    // What the grip does to the BODY is not written anywhere here: no force,
    // no torque, no weld, no pose — with the foot fixed, the fold, the press
    // and the extension ALREADY in this branch become the lever, exactly as
    // the designer described (ступня прижата, когти вцепились, лапа — рычаг).
    private void UpdateClawGrip()
    {
        clawRipoutNow = 0;
        float gripR, slipR, fnR, ftR, capR, momR;
        float gripL, slipL, fnL, ftL, capL, momL;
        ClawPad(rearRightPawCol, rearRightPawJoint,
            ref clawGripRState, ref clawTornR, ref clawAnchorR,
            out gripR, out slipR, out fnR, out ftR, out capR, out momR);
        ClawPad(rearLeftPawCol, rearLeftPawJoint,
            ref clawGripLState, ref clawTornL, ref clawAnchorL,
            out gripL, out slipL, out fnL, out ftL, out capL, out momL);
        getUpClawGripR = gripR;
        getUpClawGripL = gripL;
        getUpClawPaw = clawGripRState ? (clawGripLState ? 3 : 0)
                                      : (clawGripLState ? 1 : -1);
        getUpClawSlip = Mathf.Max(slipR, slipL);
        getUpClawFn = fnR + fnL;
        getUpClawTf = ftR + ftL;
        getUpClawCap = capR + capL;
        getUpClawFrac = getUpClawCap > 0.01f ? getUpClawTf / getUpClawCap : 0f;
        getUpClawMoment = momR + momL;
        getUpClawMuPair = clawRipoutNow != 0 || clawGripRState || clawGripLState
            ? ClawPairMu() : 0f;
        getUpClawRipout = clawRipoutNow;
    }

    // One hind pad's grip this step. fn / ft (N), the capacity (N) and the
    // contact's moment about the CoM (N*m) are published while a grip is held —
    // and on the frame one is torn out, where they are the numbers the tear
    // happened under. The state machine is the three events above; the engage
    // gate is the pad's own measured load, never a flag.
    private void ClawPad(Collider2D col, HingeJoint2D paw,
        ref bool engaged, ref bool torn, ref Vector2 anchor,
        out float grip, out float slip, out float fn, out float ft,
        out float cap, out float moment)
    {
        grip = 0f;
        slip = 0f;
        fn = 0f;
        ft = 0f;
        cap = 0f;
        moment = 0f;
        float fnRaw, ftRaw, momentRaw;
        Vector2 point;
        ReadClawContact(col, out fnRaw, out ftRaw, out momentRaw, out point);
        float weight = body != null ? body.totalMass * Physics2D.gravity.magnitude : 0f;
        float loadW = weight > 0.01f ? fnRaw / weight : 0f;
        float engageLoad = ClawEngageOverride >= 0f
            ? ClawEngageOverride : GetUpClawEngageLoad;
        float slipLimit = ClawSlipOverride >= 0f ? ClawSlipOverride : GetUpClawSlipM;
        bool wasGripped = engaged;
        // The engage signal, raw: how far this pad's own load is toward the bar.
        grip = engageLoad > 0.0001f ? Mathf.Clamp01(loadW / engageLoad) : 0f;
        if (engaged)
        {
            slip = Vector2.Distance(CatchSolePoint(paw), anchor);
            if (loadW <= GetUpClawReleaseLoad || slip > slipLimit)
            {
                // A tear while the pad is still loaded is the RIP-OUT; an
                // unloaded pad is simply off the floor (no tear).
                if (slip > slipLimit && loadW > GetUpClawReleaseLoad)
                {
                    torn = true;
                    clawRipoutNow = 1;
                }
                engaged = false;
                ApplyClawGrip(col, false);
            }
        }
        else if (!torn && loadW >= engageLoad)
        {
            // ENGAGE: a REAL contact (the normal force is measured from the
            // manifold, not a flag) and this branch's plant command. The anchor
            // is the pad's own sole point at this frame.
            engaged = true;
            anchor = CatchSolePoint(paw);
            ApplyClawGrip(col, true);
        }
        if (torn && loadW <= GetUpClawReleaseLoad)
            torn = false;                      // off the floor: fresh claws
        if (!engaged && !wasGripped)
            return;                            // no grip here and none dropped
        fn = fnRaw;
        ft = ftRaw;
        cap = ClawPairMu() * fnRaw;
        moment = momentRaw;
    }

    // The pad's own manifold this step: the contact forces summed over the
    // pad's contact points (N), their moment about the CoM (N*m) and the
    // strongest point's world position. The same GetContacts the cycle-1
    // instruments read — this method's own buffer, no counter and no gate
    // touched — so the claws' numbers and the impulse columns come from one
    // manifold. tangentImpulse is the engine's own signed impulse along the
    // contact tangent; the load the claws carry is its magnitude.
    private readonly ContactPoint2D[] clawContacts = new ContactPoint2D[16];

    private void ReadClawContact(Collider2D col, out float fn, out float ft,
        out float moment, out Vector2 point)
    {
        fn = 0f;
        ft = 0f;
        moment = 0f;
        point = Vector2.zero;
        if (col == null)
            return;
        if (!groundContactFilterReady)
        {
            groundContactFilter = new ContactFilter2D();
            groundContactFilter.SetLayerMask(GroundLayers.Mask);
            groundContactFilter.useTriggers = false;
            groundContactFilterReady = true;
        }
        int hits = col.GetContacts(groundContactFilter, clawContacts);
        int used = Mathf.Min(hits, clawContacts.Length);
        float dt = Mathf.Max(0.0001f, Time.fixedDeltaTime);
        float strongest = 0f;
        for (int ci = 0; ci < used; ci++)
        {
            ContactPoint2D c = clawContacts[ci];
            float fni = c.normalImpulse / dt;
            float fti = c.tangentImpulse / dt;
            fn += fni;
            ft += Mathf.Abs(fti);
            // ContactPoint2D carries no tangent vector: the engine's own contact
            // tangent is the normal's perpendicular (Box2D's b2Cross(normal, 1)
            // = (normal.y, -normal.x)), the direction tangentImpulse is signed
            // along — so this is the contact force with the engine's own sign.
            Vector2 tangent = new Vector2(c.normal.y, -c.normal.x);
            Vector2 force = c.normal * fni + tangent * fti;
            Vector2 r = c.point - comPosition;
            moment += r.x * force.y - r.y * force.x;
            if (c.normalImpulse > strongest)
            {
                strongest = c.normalImpulse;
                point = c.point;
            }
        }
    }

    // The material swap, through the Dog that owns both materials (Dog.cs): a
    // missing body or collider is a no-op; the exchange itself is at most one
    // assignment per pad per step.
    private void ApplyClawGrip(Collider2D col, bool grip)
    {
        if (body != null)
            body.SetClawGrip(col, grip);
    }

    // The controller's own release: the branch ended, or the automaton left the
    // get-up. Both pads go back to their paw material, the state forgets the
    // grip, and a later band may engage the claws again from scratch.
    private void ReleaseClaws()
    {
        if (clawGripRState)
        {
            clawGripRState = false;
            ApplyClawGrip(rearRightPawCol, false);
        }
        if (clawGripLState)
        {
            clawGripLState = false;
            ApplyClawGrip(rearLeftPawCol, false);
        }
        clawTornR = false;
        clawTornL = false;
        clawRipoutNow = 0;
        getUpClawGripR = 0f;
        getUpClawGripL = 0f;
        getUpClawPaw = -1;
        getUpClawSlip = 0f;
        getUpClawFn = 0f;
        getUpClawTf = 0f;
        getUpClawCap = 0f;
        getUpClawFrac = 0f;
        getUpClawMoment = 0f;
        getUpClawMuPair = 0f;
        getUpClawRipout = 0;
    }

    // The pair coefficient the claws hold by: the ENGINE'S OWN combine of the
    // two materials' coefficients (PhysicsMaterial2D.GetCombinedValues — the
    // function the 2D contact response reads), not a rule assumed here. The
    // pad's material while a grip holds is Dog.ClawContactMaterial()
    // (-dogClawFriction); the slab's is the ground's own (GroundBuilder's
    // GroundDirt, 0.4). Cached on first use: neither material changes at
    // runtime. The capacity column is mu_pair * Fn from this number, and the
    // measured Tf/Fn the recorder publishes is the check on it.
    private float ClawPairMu()
    {
        if (clawMuPairCache >= 0f)
            return clawMuPairCache;
        PhysicsMaterial2D padMat = body != null ? body.ClawContactMaterial() : null;
        PhysicsMaterial2D dirtMat = null;
        GameObject ground = GameObject.Find("Ground");
        if (ground != null)
        {
            Collider2D groundCol = ground.GetComponent<Collider2D>();
            if (groundCol != null)
                dirtMat = groundCol.sharedMaterial;
        }
        float padMu = padMat != null ? Mathf.Max(0f, padMat.friction) : 0f;
        float dirtMu = dirtMat != null ? Mathf.Max(0f, dirtMat.friction) : 0.4f;
        PhysicsMaterialCombine2D padMode = padMat != null
            ? padMat.frictionCombine : PhysicsMaterialCombine2D.Average;
        PhysicsMaterialCombine2D dirtMode = dirtMat != null
            ? dirtMat.frictionCombine : padMode;
        clawMuPairCache = Mathf.Max(0f,
            PhysicsMaterial2D.GetCombinedValues(padMu, dirtMu, padMode, dirtMode));
        return clawMuPairCache;
    }

    // The pad's own sole point in the world — the point the recorder's pawSoleX
    // columns read (CatchSoleX is its x) and the point a grip is anchored on.
    private static Vector2 CatchSolePoint(HingeJoint2D paw)
    {
        return paw != null
            ? (Vector2)paw.transform.TransformPoint(paw.anchor) : Vector2.zero;
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
        // CYCLE 13: the second, independent unload exit. A half can also END by
        // the support being handed over - the loaded pair unloaded while every
        // paw is still DOWN - instead of only by the fold's lift-and-return.
        // It ADDS an exit and withholds nothing, so it cannot park the stride
        // the way the two measured fold gates did (cycles 5/7). Detail on
        // WalkHandOverUnloaded below.
        else if (WalkHandOverUnloaded())
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

    // CYCLE 13: THE SECOND, INDEPENDENT UNLOAD SIGNAL (the hand-over).
    // Cycle 7 7.1: the fold is the ONLY leave mechanism the walk has and it
    // "is both the unload command and the clock", so a half could only ever end
    // through the fold's lift-and-return; cycle 6 7.2: "the lever is the half's
    // END condition, not the sweep"; cycle 10 8.3: the half "ends on a paw's
    // presence, not its load". This is that second signal, and it is a LOAD
    // reading - the cycle-6 sensor (pawLoadFraction), the same one the stance
    // sweep reads - not the clock and not the fold's sweep: the support has
    // been HANDED OVER.
    //
    // All four paws are DOWN (no paw mid-flight, so the swap cannot abandon a
    // swing, and the pair that leaves is left ON the ground - not folded away).
    // The pair that carried the body - this half's stance pair - no longer holds
    // one paw's two-paw share (walkStanceLoadRef) between the two of them, while
    // the other diagonal carries a full share on one paw.
    //
    // Unlike the two measured fold gates this does not WITHHOLD the fold: the
    // arm, the fold and the air clock are untouched, and the exit only ADDS a
    // way for the half to end, so it has no way to park the stride - it can end
    // a half early only when the load has already moved off the loaded pair.
    private bool WalkHandOverUnloaded()
    {
        if (!walkCaptureArmed)
            return false;
        if (!frontRightGrounded || !frontLeftGrounded
            || !rearRightGrounded || !rearLeftGrounded)
            return false;
        int oldFront = WalkLimbIsStance(true, true) ? 0 : 1;
        int oldRear = WalkLimbIsStance(false, true) ? 2 : 3;
        int newFront = oldFront == 0 ? 1 : 0;
        int newRear = oldRear == 2 ? 3 : 2;
        float oldLoad = pawLoadFraction[oldFront] + pawLoadFraction[oldRear];
        float newLoad = Mathf.Max(pawLoadFraction[newFront], pawLoadFraction[newRear]);
        return oldLoad <= walkStanceLoadRef && newLoad >= walkStanceLoadRef;
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
        // CYCLE 6: `sweep` is the PUSH (u-ramped, signed along facing); the dual
        // extra-extend further down is posture. The two used to be one variable.
        float sweep = 0f;
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
            sweep = Mathf.Lerp(-2f, walkStanceSweepDeg, u) * tame;
        }

        if (!walkCaptureArmed)
        {
            float age = walkHalfBorn >= 0f ? Time.fixedTime - walkHalfBorn : 0f;
            float ramp = Mathf.Clamp01(age / Mathf.Max(0.04f, WalkMinDual()));
            float dual = Mathf.Lerp(4f, 8f, ramp) * tame;
            float dualHold = dual * dir;
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
        float posture = 0f;
        if (!swingAirNow)
        {
            // Peel: keep Dual extra-extend so the stance pair holds
            // the girdle while the trail shortens. Clamping to 3°
            // after Arm dropped the pelvis into the 6 cm chord.
            float age = walkHalfBorn >= 0f ? Time.fixedTime - walkHalfBorn : 0f;
            float ramp = Mathf.Clamp01(age / Mathf.Max(0.04f, WalkMinDual()));
            posture = Mathf.Lerp(4f, 8f, ramp) * tame;
        }

        // CYCLE 6, THE ONE CHANGE. The u-ramped sweep is the PUSH, so it is
        // spent on the paw that is carrying the body; the extra-extend above is
        // posture (it holds the girdle open through the peel) and stays
        // unscaled. Per stance limb the angle added is now
        //     max(sweep * loadShare, posture) * dir
        // instead of
        //     max(sweep, posture) * dir
        // which is IDENTICAL on a limb carrying walkStanceLoadRef of the body
        // (0.5 bw = a paw's share in a two-paw stance) and falls back to the
        // posture term on a stance limb with no contact.
        // Measured reason (cycle 5, DOGDIAG + the accepted walk trace): the
        // intended stance diagonal holds one of its two paws 8-13 cm up through
        // most of the half, so part of the sweep budget was spent on air, and
        // the per-frame speed of the accepted walk FALLS as the flag count of
        // "grounded" paws rises (0 paws 0.566 m/s, 4 paws 0.277 m/s). The load
        // is the cycle-1 instrument, read by no control law before this cycle.
        // ─── CYCLE 24, THE ONE CHANGE: THE LOADED STANCE HOLD ───
        // The sweep above is an OPEN-LOOP rotation. A loaded pad's own world X
        // follows its own limb angle (cycle 23, this build: -1.6..-2.2 mm/deg,
        // all four paws the same sign) and the pads out-travel the body 4.88x:
        // the position is angle-driven, NOTHING in the law holds it, and the
        // forward motion is bought with the slide. The hold is a world-X servo
        // on the pad the limb is carrying: the pad's own world step this frame
        // (v * dt) is answered by a counter-rotation of half that step over the
        // measured coupling (0.5 / 2 mm per deg = 0.25 deg per mm), so a pad
        // sliding caudally is pulled cranially and a pad dragged cranially is
        // let go caudally: the pad keeps the ground and the trunk rides over it.
        // The world frame is the one the slab holds, and the coupling was
        // measured in this law's own joint convention (the CSV joint columns are
        // joint.jointAngle, the target's convention), so the term is signed in
        // the world frame: the pad's step and the joint's convention flip
        // together when SetFacing mirrors the dog (DogCosmetics :16-30).
        // The sweep stays the push: the term is spent through the SAME stance
        // role and the SAME load share as the spend, so a stance limb in the air
        // keeps the sweep and gets no hold, and the swap that flips the diagonal
        // releases it - the half's own Arm ramp (walkHalfBorn, above) is its only
        // clock. No new field, no new constant, no latch state.
        float holdDt = Time.fixedDeltaTime;
        Rigidbody2D holdBodyFr = frontRightPawCol != null ? frontRightPawCol.attachedRigidbody : null;
        Rigidbody2D holdBodyFl = frontLeftPawCol != null ? frontLeftPawCol.attachedRigidbody : null;
        Rigidbody2D holdBodyRr = rearRightPawCol != null ? rearRightPawCol.attachedRigidbody : null;
        Rigidbody2D holdBodyRl = rearLeftPawCol != null ? rearLeftPawCol.attachedRigidbody : null;
        float holdFl = Mathf.Max(sweep * WalkStanceLoadShare(1), posture) * dir
            + 0.25f * (holdBodyFl != null ? holdBodyFl.linearVelocity.x : 0f)
                * holdDt * 1000f * WalkStanceLoadShare(1);
        float holdFr = Mathf.Max(sweep * WalkStanceLoadShare(0), posture) * dir
            + 0.25f * (holdBodyFr != null ? holdBodyFr.linearVelocity.x : 0f)
                * holdDt * 1000f * WalkStanceLoadShare(0);
        float holdRl = Mathf.Max(sweep * WalkStanceLoadShare(3), posture) * dir
            + 0.25f * (holdBodyRl != null ? holdBodyRl.linearVelocity.x : 0f)
                * holdDt * 1000f * WalkStanceLoadShare(3);
        float holdRr = Mathf.Max(sweep * WalkStanceLoadShare(2), posture) * dir
            + 0.25f * (holdBodyRr != null ? holdBodyRr.linearVelocity.x : 0f)
                * holdDt * 1000f * WalkStanceLoadShare(2);
        if (flStance)
            flSh = Mathf.Clamp(flSh + holdFl, -90f, 90f);
        if (frStance)
            frSh = Mathf.Clamp(frSh + holdFr, -90f, 90f);
        if (rlStance)
            rlHip = Mathf.Clamp(rlHip + holdRl, -90f, 90f);
        if (rrStance)
            rrHip = Mathf.Clamp(rrHip + holdRr, -90f, 90f);
    }

    // CYCLE 6: how much of the stance sweep a limb is allowed to spend, from
    // the load it is measurably carrying (pawLoadFraction = Fn/(m*g), filled by
    // SampleCycle1Instruments from the contact manifold). 1 at
    // walkStanceLoadRef and above, 0 for a limb in the air. Paw order matches
    // PawCollider and the CSV: FR 0, FL 1, RR 2, RL 3.
    private float WalkStanceLoadShare(int paw)
    {
        if (walkStanceLoadRef <= 0.001f)
            return 1f;
        return Mathf.Clamp01(pawLoadFraction[paw] / walkStanceLoadRef);
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

    // CYCLE 1 INSTRUMENT (read-only, measure only): the per-paw ground reaction
    // the demand never sees, in the form the human lane already records as
    // fzL/fzR: Fn = ContactPoint2D.normalImpulse / dt along the contact normal,
    // here kept as the step's impulse per paw. It re-queries the manifolds
    // ProbePaw queried, into its own buffer, and touches no counter, no gate and
    // no command — the point of the cycle is that the physics numbers do not move.
    private void SampleCycle1Instruments()
    {
        walkStandDropBar = WalkStandDrop();
        if (!groundContactFilterReady)
        {
            groundContactFilter = new ContactFilter2D();
            groundContactFilter.SetLayerMask(GroundLayers.Mask);
            groundContactFilter.useTriggers = false;
            groundContactFilterReady = true;
        }

        float verticalImpulse = 0f;
        for (int pk = 0; pk < 4; pk++)
        {
            Collider2D pawCol = PawCollider(pk);
            float pawImpulse = 0f;
            float pawVertical = 0f;
            if (pawCol != null)
            {
                int hits = pawCol.GetContacts(groundContactFilter, loadContacts);
                int used = Mathf.Min(hits, loadContacts.Length);
                for (int ci = 0; ci < used; ci++)
                {
                    pawImpulse += loadContacts[ci].normalImpulse;
                    float vertical = loadContacts[ci].normalImpulse * loadContacts[ci].normal.y;
                    verticalImpulse += vertical;
                    pawVertical += vertical;
                }
            }
            pawNormalImpulse[pk] = pawImpulse;
            // CYCLE 6: the same term kept per paw. The running sum above adds
            // the identical values in the identical order, so pawWeightFraction,
            // the published columns and the physics do not move.
            pawVerticalImpulse[pk] = pawVertical;
        }

        float bodyWeight = body != null ? body.totalMass * Physics2D.gravity.magnitude : 0f;
        pawWeightFraction = bodyWeight > 0.01f
            ? verticalImpulse / Time.fixedDeltaTime / bodyWeight
            : 0f;
        // CYCLE 6 SENSOR: each paw's own share of the body weight, Fn_i/(m*g),
        // from the SAME manifold read. Read by the stance sweep only.
        float loadRef = bodyWeight * Time.fixedDeltaTime;
        for (int pk = 0; pk < 4; pk++)
            pawLoadFraction[pk] = loadRef > 0.0001f
                ? Mathf.Clamp01(pawVerticalImpulse[pk] / loadRef)
                : 0f;

        // ─── CYCLE 3 SENSOR: the capture point over the SAME support interval ───
        // Read-only, like everything else here: the physics numbers of the run
        // must stay bit-identical (the cycle's acceptance). h is the live CoM
        // height over the slab top, floored so a body on the floor cannot make
        // omega singular; the guard on the root keeps the divide finite if the
        // slab was never found.
        float cpHeight = Mathf.Max(CapturePointHeightFloorM, comPosition.y - walkGroundTopY);
        float cpOmega = cpHeight > 0.001f
            ? Mathf.Sqrt(Physics2D.gravity.magnitude / cpHeight)
            : 0f;
        capturePointHeight = cpHeight;
        capturePointX = cpOmega > 0.001f ? comPosition.x + comVelocity.x / cpOmega : comPosition.x;
        if (hasSupportInterval
            && capturePointX >= supportMinX && capturePointX <= supportMaxX)
        {
            capturePointMargin = Mathf.Min(capturePointX - supportMinX, supportMaxX - capturePointX);
        }
        else if (hasSupportInterval)
        {
            capturePointMargin = -Mathf.Min(Mathf.Abs(capturePointX - supportMinX),
                Mathf.Abs(capturePointX - supportMaxX));
        }
        else
        {
            capturePointMargin = 0f;
        }
    }

    // Paw order FR, FL, RR, RL — the order of the CSV grounded flags.
    private Collider2D PawCollider(int index)
    {
        switch (index)
        {
            case 0: return frontRightPawCol;
            case 1: return frontLeftPawCol;
            case 2: return rearRightPawCol;
            case 3: return rearLeftPawCol;
            default: return null;
        }
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
        // CYCLE 40: the arrival's own weight widens the law's own brake's room.
        // The c39 arrival read this term at its clamp (60 * -0.0706 - 8 * 3.14 =
        // -29.4 deg against +-22), i.e. the one brake the law owns on the rise was
        // truncated exactly where it was needed; with the weight at 0 this is
        // heightMaxDeg, bit for bit.
        float liftMax = heightMaxDeg * (1f + GetUpArriveLiftGain * getUpArriveW);
        return Mathf.Clamp(raw, -liftMax, liftMax);
    }

    private void DriveLumbarToWorld(float targetTilt)
    {
        if (lumbarFlexor == null || lumbarExtensor == null || vestibular == null) return;
        float signal = LumbarWorldSignal(targetTilt);
        ApplySignal(ProtectJointLimit(lumbarJoint, signal), lumbarFlexor, lumbarExtensor);
    }

    // The accepted world loop's own signal (the law every row up to c31 ran on),
    // split out so the fold can blend against it without a second copy of it.
    // Unchanged arithmetic: the chest's tilt to the target, the pelvis's own
    // error if asked for, the joint's rest term if asked for, the open gate.
    private float LumbarWorldSignal(float targetTilt)
    {
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
        return signal;
    }

    // ─── THE FOLD'S OWN LAW (cycle 32) ───
    // The trunk's curl, commanded as a JOINT angle inside the rig's wide range
    // (GetUpFoldLumbarDeg = +39 against the rig's +40 — Dog.cs): positive =
    // ventral flexion, the chest curling toward the belly. Two clocks decide
    // which law the muscle hears:
    //   the GATE — 0 at the accepted flexion stop (20°), 1 GetUpFoldGateDeg
    //     past it. Inside the window the accepted world loop is in charge, so
    //     the trunk's trajectory is the accepted one exactly; the fold can only
    //     act where the old clamp bound, which is the whole point of the cycle;
    //   the WEIGHT — the fold's own ramp from the band's arm.
    // Both laws are saturated at the same signal (+) in the band (the world
    // loop wants far more flexion than the trunk has), so the handover itself
    // moves nothing: the difference is that the curl's law STOPS at its target
    // instead of riding the stop, which is what holds the trunk's C.
    private void DriveLumbarFold(float worldTarget)
    {
        if (lumbarFlexor == null || lumbarExtensor == null || vestibular == null) return;
        float signal = LumbarWorldSignal(worldTarget);
        float angle = lumbarJoint != null ? lumbarJoint.jointAngle : 0f;
        float gate = Mathf.Clamp01((angle - Dog.LumbarAcceptedMaxDeg)
                                   / Mathf.Max(1f, GetUpFoldGateDeg));
        if (gate > 0f && lumbarJoint != null)
        {
            float curl = LumbarJointSignal(GetUpFoldLumbarDeg);
            signal = Mathf.Lerp(signal, curl, gate * getUpFoldWeight);
        }
        ApplySignal(ProtectJointLimit(lumbarJoint, signal), lumbarFlexor, lumbarExtensor);
    }

    // The joint loop's own signal, the ControlJointStable arithmetic with the
    // fold's own gains (GetUpFoldPGain / GetUpFoldDGain): the accepted world
    // loop's lumbarPGain/lumbarDGain are never read here, so the stand, the walk
    // and the fall keep the law they were measured in.
    private float LumbarJointSignal(float targetDeg)
    {
        float dt = Time.fixedDeltaTime;
        float angleForError = lumbarJoint.jointAngle + lumbarJoint.jointSpeed * dt;
        if (lumbarInertia <= 0f)
            lumbarInertia = EffectiveInertia(lumbarJoint);
        float damping = 0f;
        if (lumbarInertia > 0f)
        {
            float k = GetUpFoldDGain * lumbarFlexor.maxTorque * Mathf.Rad2Deg
                      / Mathf.Max(1f, speedReferenceDegPerSec);
            damping = k * dt / lumbarInertia;
        }
        float errDeg = Mathf.DeltaAngle(angleForError, targetDeg);
        float error = errDeg / Mathf.Max(1f, errorReferenceDegrees);
        float speed = lumbarJoint.jointSpeed / speedReferenceDegPerSec;
        float raw = GetUpFoldPGain * error - GetUpFoldDGain * speed;
        return Mathf.Clamp(raw / (1f + damping), -1f, 1f);
    }

    // The fold's own release (the automaton left the get-up): the weight to zero
    // and the rig's range closed back to the accepted one, the same step.
    private void ReleaseTrunkFold()
    {
        if (getUpFoldWeight == 0f && !getUpFoldRigOpen && !getUpFoldRan
            && getUpFoldDeg == 0f && getUpFoldNeckDeg == 0f)
            return;
        getUpFoldWeight = 0f;
        getUpFoldRan = false;
        getUpFoldDeg = 0f;
        getUpFoldNeckDeg = 0f;
        if (getUpFoldRigOpen)
        {
            getUpFoldRigOpen = false;
            if (body != null)
                body.SetTrunkFold(false);
        }
        getUpFoldRangeOpen = 0;
        getUpFoldFlexLimitDeg = Dog.LumbarAcceptedMaxDeg;
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
