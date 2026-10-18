using UnityEngine;

// CoM-based balancer. The capture-point walker (useCaptureWalker) lives in
// BalanceControllerCaptureWalk.cs beside the fall-catch walker here; both share the joint
// caches and SPD helpers so no hinge ever has two owners.
[DefaultExecutionOrder(0)]
public partial class BalanceController : MonoBehaviour
{
    public enum BalanceState
    {
        Balancing,
        Recovery,
        Falling
    }

    // Body-level locomotion state, separate from the CoM-offset BalanceState above.
    // Do not use BalanceState.Recovery as "getting up".
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
    // Get-up pose order: lying → sit up → tuck knees (≥70°, heels under the pelvis) → arm push →
    // stand; starting with the tuck skips the sit and fails. The walk key on a Fallen heap drives
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
    // Arms push off the ground while GettingUp: the elbow needs real travel (> 20°) and a non-zero
    // command; crouchHandSupportElbow (−10) alone is almost straight.
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
    // autoGetUpDwell D > 0: a keyless heap held D seconds starts the get-up itself, latching its own
    // press for the attempt; the latch clears once off the heap. 0 = a keyless heap stays down.
    [Tooltip("Seconds a keyless heap must hold before the man self-rises. 0 = off.")]
    public float autoGetUpDwell = 0f;
    private float autoGetUpAge;            // the keyless-heap clock, s
    private bool autoGetUpHeld;            // the self-press latched for this heap
    // Get-up pose: 0 none, 1 lying, 2 sitting, 3 knees tucked, 4 standing.
    [Tooltip("Highest GETUP pose reached this attempt (lying/sitting/tucked/standing).")]
    public int getUpPose;
    [Tooltip("Highest GETUP pose reached this run (latched).")]
    public int getUpPoseMax;
    // Stand-log slots for the get-up gates (through getUpSitSeatPasses); no current code writes
    // them, so they read 0. Rise contact gate: last attempt's held seconds and hold count.
    public float getUpGateHeld;
    public int getUpGateHolds;
    // Base gate: the Tuck's wait (s) and the cap aborts.
    public float getUpBaseWait;
    public int getUpBaseAborts;
    // Settle gate: dwell the base held this attempt and fires refused while it was unsettled.
    public float getUpBaseSettle;
    public int getUpBaseSettleBlocks;
    // Load gate: the per-foot load floor's dwell (max, s) and the refused fires.
    public float getUpBaseLoad;
    public int getUpBaseLoadBlocks;

    // Sign-aware hip arbitration: per-side running count of refused pelvis-law hip writes.
    public int getUpArbSuppL;
    public int getUpArbSuppR;

    // Move-guard and trunk governor: last attempt's held seconds and holds, largest counter-lean.
    public float getUpMoveGuardHeld;
    public int getUpMoveGuardHolds;
    public float getUpTrunkGovHeld;
    public int getUpTrunkGovHolds;
    public float getUpTrunkGovBias;

    // Sit-stand conversion: fires and deferred-bound seconds.
    public int getUpSitFires;
    public float getUpSitExt;
    public float getUpSitExtMax;
    // Sit re-seat (max per run): the pull's frozen seconds and its gate openings.
    public float getUpSitSeatFrozen;
    public int getUpSitSeatPasses;

    [Header("State-machine thresholds")]
    public float recoveryCoMOffset = 0.15f;
    public float fallCoMOffset = 0.30f;
    // A flight plus its plant is well under a second; a walk exemption held longer is not catching
    // a step but a heap commanding pelvis-to-vertical at saturation.
    [Tooltip("Seconds the fall-catch walk may keep the automaton in Recovery while |CoM offset| is past fallCoMOffset. 0 = unlimited (former behaviour).")]
    public float fallCatchFallGraceSeconds = 1.5f;
    // Seconds the exemption has run. Cleared only once back inside fallCoMOffset, so a spent
    // exemption cannot re-arm itself.
    private float fallCatchIgnoreFallAge;

    [Header("Balance gains")]
    // Inputs are normalized: 10 cm of offset and 30 °/s of tilt each give 1, so torso rate cannot
    // swamp the signal.
    public float comOffsetReference = 0.10f;
    public float tiltSpeedReference = 30f;
    public float comVelocityReference = 0.30f;
    public float comProportionalGain = 1.2f;
    public float comDerivativeGain = 0.35f;
    public float maxBalanceSignal = 1.0f;

    [Header("Target angles (degrees)")]
    // The hip value is a world pelvis-tilt target, not a joint angle: negative = forward (clockwise;
    // the human faces right). The knee holds its joint angle. −3/6 is the working stance.
    public float hipBaseAngle = -3f;
    public float kneeBaseAngle = 6f;
    public float kneeRecoveryFlex = 12f;
    public float hipBalanceGain = 8f;

    [Header("Landing transient (drop test, line 3, 27.09)")]
    // Landing from the air, quiet path only, 0 unless airborne: PREPARE (both feet unloaded, CoM
    // falling) flattens the plates and presets the pelvis back; YIELD folds the knees after contact.
    // Both add onto quiet-stance targets. Knee pre-fold and yield hurt the landing, so they ship 0.
    public bool landingTransient = true;
    [Tooltip("CoM descent (m/s) that arms the airborne prepare.")]
    public float landingPrepDescent = 1.0f;
    [Tooltip("Minimum airborne time (s) before the prepare may rise. The spawn's own drop-in is 1-3 steps and must not read as a fall (28.09: without this the quiet stand gained a phantom landing at t=0.01 and the 40 s stand row MOVED).")]
    public float landingPrepAirMin = 0.10f;
    [Tooltip("Seconds the prepare takes to reach full while airborne.")]
    public float landingPrepRamp = 0.10f;
    [Tooltip("Knee pre-fold (deg, both legs) at a full prepare. Measured harmful at 8-18 deg; ships 0.")]
    public float landingPrepKneeFlex = 0f;
    [Tooltip("Flat-plate PD gain per degree, added to the ankle balance signal (0 = no ankle prepare).")]
    public float landingPrepAnkleGain = 0.25f;
    [Tooltip("Bounded knee absorb (deg) at a full-speed arrival. Measured harmful at 20 deg; ships 0.")]
    public float landingYieldKneeFlex = 0f;
    [Tooltip("Seconds from the first contact to the release of the bounded knee fold.")]
    public float landingYieldTime = 0.35f;
    [Tooltip("Fraction of the yield window spent ramping the fold in (the rest releases it).")]
    public float landingYieldRiseFrac = 0.35f;
    [Tooltip("Arrival speed (m/s) that maps to a full yield.")]
    public float landingYieldImpactRef = 4.5f;
    // The solver's penetration correction can move a body far in one step while the grounded flags
    // still read air; that is not an arrival speed. Samples above the cap are dropped (a 1.5 m fall
    // tops out at ~5.4 m/s); rejections are logged once per run.
    [Tooltip("Arrival-speed samples above this cap (m/s) are solver artifacts, not a fall. Physical ceiling of the 1.5 m ladder is 5.4 m/s.")]
    public float landingImpactCap = 12f;
    // ─── Landing catch ───
    // After landing the CoM can creep past what the one-plate ankle loop holds (~9 cm, 65 N·m / 695 N);
    // a bounded backward pelvis lean scaled by the predicted lead brakes it (landing window only).
    [Tooltip("Catch dead zone (m): CoM lead over the support centre inside this band asks for no brake.")]
    public float landingCatchBand = 0.02f;
    [Tooltip("Backward pelvis lean (deg) per metre of predicted CoM lead beyond the band.")]
    public float landingCatchHipGain = 30f;
    [Tooltip("Cap on the catch pelvis lean (deg).")]
    public float landingCatchHipMax = 8f;
    [Tooltip("Seconds of CoM look-ahead (comOffset + v*this) the catch leans against.")]
    public float landingCatchLeadTime = 0.1f;
    [Tooltip("Hard ceiling on the catch window (s after the first contact).")]
    public float landingCatchMaxTime = 3f;
    [Tooltip("The lead must stay inside the band this long for the catch to end (s).")]
    public float landingCatchRelease = 0.5f;
    [Tooltip("Toe (MTP) press during the catch: deg per metre of CoM lead beyond the band. The toe is the last CoP lever the foot has - the ankle loop saturates at 65 N.m, the plate still has ~7 cm of sole forward of the measured CoP.")]
    public float landingCatchToeGain = 400f;
    [Tooltip("Cap on the catch toe press (deg).")]
    public float landingCatchToeMax = 20f;
    [Tooltip("Feed-forward prepare: backward pelvis preset (deg) while the rig is still in the air, so the arrival does not land with the CoM already ahead of the ankle.")]
    public float landingPrepPelvisDeg = 6f;

    [Header("Crouch")]
    // intent.crouch is a 0-or-1 target, so the level ramps here (else crouch=1 outruns a held key).
    // 0.6/s (full level in 1.67 s, ~65°/s of knee) lets the sole load follow and the load gate act;
    // faster folds unload the soles, overshoot and jam the knee on its stops.
    public float crouchRatePerSecond = 0.6f;
    public float crouchReleaseRatePerSecond = 0.9f;
    // Knee flexion is positive (range 0…120, stance +6°). kneeBaseAngle + 109 = 115°, the quiet
    // path's knee clamp, so 109 is the deepest value that still changes the pose.
    public float crouchKneeFlex = 109f;
    // Not part of the hip target (the pelvis–thigh joint is not held); kept for the stand log and
    // Inspector.
    public float crouchHipFlex = 32f;
    // Forward pelvis-tilt target at crouch=1 (forward = clockwise = minus on the world target).
    // Depth comes from the knee; this lean keeps the trunk over the feet. hipBaseAngle − 54 = −57°
    // nears the ±60° pelvis stop, leaving the balance loop ~3° of forward authority.
    public float crouchPelvisTilt = 54f;
    // Extra forward chest lean at crouch=1 (forward = clockwise = minus; the human faces right).
    public float crouchTorsoLean = 24f;
    // Crouch level the pose actually uses (stand log, Inspector).
    public float crouchLevel;
    // ─── Crouch vertical force channel ───
    // crouchLoadLevel is the legs' crouch level and moves only while the soles carry the body (band
    // ends are fractions of the rig's own weight). Arms and hand support still read crouchLevel.
    [Tooltip("Fold deeper only while the soles carry at least this fraction of body weight (0.90 = the acceptance band floor).")]
    public float crouchLoadFoldFloor = 0.90f;
    [Tooltip("Rise / push only while the ground reaction stays at or below this fraction of body weight (1.15 = the acceptance band ceiling).")]
    public float crouchLoadPushCeiling = 1.15f;
    [Tooltip("EMA time constant of the measured vertical load, s. The two soles rock the load between them at ~12 Hz; the sum is what this channel reads.")]
    public float crouchLoadFilterTau = 0.02f;
    // Crouch level the legs are given (public for Inspector/MCP). Exactly 0 on paths that never crouch.
    public float crouchLoadLevel;
    // Measured vertical ground reaction of both soles, N, EMA-filtered. Negative = no sample yet.
    private float crouchLoadFilteredN = -1f;
    // Sum of the rig's segment masses × g, N. 0 = not resolved yet.
    private float rigWeightN;
    // Own contact buffer and filter (TrialRecorder and BodyStateEstimator keep theirs).
    private ContactPoint2D[] crouchLoadContacts;
    private ContactFilter2D crouchLoadFilter;
    private bool crouchLoadFilterReady;

    // ─── Squat task ───
    // Stand→squat→stand state machine, armed only while MotionIntent.squatReps > 0. It sets the
    // crouch level target and judges reps; the crouch ramp, pose and joints still do the moving.
    public float squatDepthFlexDeg = 103.55f;     // 0.95 * crouchKneeFlex (109)
    public float squatReturnBandDeg = 10f;
    public float squatComSettleM = 0.05f;
    public float squatComRepBoundM = 0.15f;
    // Max vestibular body tilt per rep (GetBodyTilt, not trunk lean). The crouch itself reaches ~28°
    // at depth, so the bound sits above that and under getUpSquatAbsTilt (35°).
    public float squatTiltBoundDeg = 32f;
    public float squatLoadDipFloor = 0.5f;
    public float squatLoadDipSeconds = 0.15f;
    public float squatStandConfirmSeconds = 0.30f;
    // Dwell at the squat bottom, s. Longer dwells leave the stand too unquiet for its confirm gate,
    // so the next rep is refused and the body topples.
    public float squatBottomHoldSeconds = 0.40f;
    // Bottom settle gate (when armed; intent values override): |pelvis vY| <= squatBottomSettleMps
    // must hold squatBottomSettleSeconds. 0.06 m/s is well under fold descent (0.3–0.7 m/s) and above
    // sensor noise. A phase longer than squatPhaseWatchdogSeconds aborts the task.
    public float squatBottomSettleSeconds = 0.15f;
    public float squatBottomSettleMps = 0.06f;
    public float squatPhaseWatchdogSeconds = 6f;
    // Load-driven pacing: the task's level rate is the crouch rates (ceiling) scaled by the load term
    // (SquatStateMachine.TickLoadPace). After squatPaceStallSeconds with drive <= squatPaceDriveFloor
    // the stall guard holds the scale at squatPaceFloorScale (anti-freeze creep).
    public float squatPaceFloorScale = 0.35f;
    public float squatPaceStallSeconds = 0.50f;
    public float squatPaceDriveFloor = 0.05f;
    private float squatPaceScale;                 // last rate scale the machine returned
    private float squatBottomHoldUsed;            // the dwell this take actually ran
    private SquatStateMachine squatTask = new SquatStateMachine();
    // The squat machine's read of this step's CoM offset (the FixedUpdate local goes to
    // UpdateLocomotionState, which runs after the crouch line).
    private float squatComOffset;
    // Rise rate scale as applied this step (1 while not rising) and the flat release dose in use.
    private float squatReleaseScaleUsed = 1f;
    private float squatReleaseKnob = 1f;
    // Longest a single CoM hold may last during the rise, s, unless intent sets one; the backward
    // fork is decided in the rise's first half second.
    public float squatRiseComHoldMaxSeconds = 0.5f;

    // Arm raise: intent.armRaise carries the request; the targets below run on this component's step
    // clock and ArmDrive, the arms' only writer, emits them while ArmRaiseActive. Off while armRaise
    // is 0.
    private int armRaiseSteps = -1;               // -1 = not latched
    private bool armRaiseDoneLatched;
    private float armRaiseComMin;
    private float armRaiseComHoldEnd;
    public bool ArmRaiseActive { get; private set; }
    public float ArmRaiseShoulderTarget { get; private set; }
    public float ArmRaiseElbowTarget { get; private set; }
    public int ArmRaiseRepsDone { get; private set; }
    public float ArmRaiseComMinM => armRaiseComMin;
    public float ArmRaiseComHoldEndM => armRaiseComHoldEnd;

    // Published for TrialRecorder's summary only: nothing in this file reads them.
    public int SquatStateCode => (int)squatTask.State;
    public int SquatRepsRequested => squatTask.RepsRequested;
    public int SquatRepsDone => squatTask.RepsDone;
    public int SquatRepOkMask => squatTask.RepOkMask;
    public bool SquatAborted => squatTask.Aborted;
    public int SquatGateMask => squatTask.LastGateMask;
    public int SquatReason => squatTask.LastReason;
    public float SquatLastRepSeconds => squatTask.LastRepSeconds;
    public float SquatBottomKneeDeg => squatTask.LastBottomKneeDeg;
    public float SquatBottomComM => squatTask.LastBottomComM;
    public float SquatBottomTiltDeg => squatTask.LastBottomTiltDeg;
    public float SquatBottomLoad => squatTask.LastBottomLoad;
    // Squat pacing telemetry: bottom dwell used, load pace scale, stall-guard data. 0 while unarmed.
    public float SquatBottomHoldSeconds => squatBottomHoldUsed;
    public float SquatPaceScale => squatPaceScale;
    public bool SquatPaceFallbackFired => squatTask.PaceFallbackFired;
    public float SquatPaceStallSeconds => squatTask.PaceStallTotalSeconds;
    public int SquatPaceNoDriveFrames => squatTask.PaceNoDriveFrames;
    // Rise telemetry: flat dose, smallest scale, seconds the load gate froze the rise, stall-guard
    // fire. 0 while unarmed.
    public float SquatReleaseKnob => squatReleaseKnob;
    public float SquatReleaseScaleMin => squatTask.ReleaseScaleMin;
    public float SquatReleaseFrozenSeconds => squatTask.ReleaseFrozenSeconds;
    public bool SquatReleaseFallbackFired => squatTask.ReleaseFallbackFired;
    // Seconds the sink bypass took the rise off the load gate.
    public float SquatReleaseSinkSeconds => squatTask.ReleaseSinkSeconds;
    // Bottom settle gate telemetry.
    public float SquatBottomSettleAge => squatTask.BottomSettleAge;
    public float SquatBottomSettleExtraSeconds => squatTask.BottomSettleExtraSeconds;
    // Rise CoM-hold telemetry: fires, seconds held, sink-belt seconds.
    public int SquatComHoldFires => squatTask.ComHoldFires;
    public float SquatComHoldSeconds => squatTask.ComHoldSeconds;
    public float SquatComHoldSinkSeconds => squatTask.ComHoldSinkSeconds;
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
    // Knees stay put: only chest and pelvis tilt; the ankle holds CoM.
    [Tooltip("Slew rate of leanLevel toward intent.lean, 1/s.")]
    public float leanRatePerSecond = 1.5f;
    [Tooltip("Extra chest tilt at lean=+1, deg (forward = minus on the target).")]
    public float leanTorsoAngle = 12f;
    [Tooltip("Extra pelvis tilt at lean=+1, deg (forward = minus on the target).")]
    public float leanPelvisTilt = 8f;
    // While leaning, the pose path's ankle loop (A1) regulates CoM toward supportCenterX +
    // leanShiftLevel * leanComShift, so the CoM really travels. The shift is kept small: larger
    // shifts saturate A1's hold.
    [Tooltip("Forward lean's CoM travel at lean=+1, m (the support loop's target shift).")]
    public float leanComShift = 0.012f;
    // The shift's own slew (0.3/s ≈ 3.6 mm/s of target) also clocks the tilt's return: the release
    // is the risky part, so it is slow, while a forward lean tilts in at the intent's faster rate.
    [Tooltip("Slew rate of the lean's own level (and the tilt relay's return), 1/s.")]
    public float leanShiftRatePerSecond = 0.3f;
    // Current lean level −1…+1 for the pose and Inspector.
    public float leanLevel;

    // ─── Kick layer ───
    // Written by LegStrikeDrive (order −10) for the pose path; false/0 without a stroke. Targets: hip
    // minus = thigh forward, knee plus, ankle plus = toe down; kickShiftLevel scales the support shift.
    public bool kickStrokeActive;
    public bool kickSideRight = true;
    public float kickHipTargetDeg;
    public float kickKneeTargetDeg;
    public float kickAnkleTargetDeg;
    public float kickShiftLevel;
    // Support hip's plant angle, latched when the stroke fires; the pose path holds the support hip
    // in its band around it while kickShiftLevel is up.
    public float kickSupportHipDeg;
    // True while a stroke is due but the gate still defers it (load or phase criterion, before its
    // timeout). Written by LegStrikeDrive, read by PoseDriveController only under -kickWindDue.
    public bool kickFireHeld;
    // True during a stroke's lift phase (KickPhase 1: kicking foot still down, its load leaving).
    // Written by LegStrikeDrive, read by PoseDriveController only under -kickWindHead.
    public bool kickHeadActive;
    // One sole's load, N: upward contact normal impulse / dt, the same recipe as the crouch sampler
    // and TrialRecorder's fzL/fzR. Read-only; LegStrikeDrive's load gate uses it.
    public float KickSoleLoadN(bool rightSide, float dt)
    {
        if (dt <= 0f) dt = 0.005f;
        if (!crouchLoadFilterReady)
        {
            crouchLoadFilter = new ContactFilter2D();
            crouchLoadFilter.SetLayerMask(GroundLayers.Mask);
            crouchLoadFilter.useTriggers = false;
            crouchLoadFilterReady = true;
        }
        if (crouchLoadContacts == null) crouchLoadContacts = new ContactPoint2D[12];
        return rightSide
            ? SoleLoadN(rightHeelBox, dt) + SoleLoadN(rightToeBox, dt)
            : SoleLoadN(leftHeelBox, dt) + SoleLoadN(leftToeBox, dt);
    }

    [Header("One-leg stance")]
    // Swing flex: knee plus first (shorten the leg, lift the foot); hip minus only after lift-off,
    // latched no weaker than the lift-off angle (~37°), or the thigh extends and the foot stomps.
    public float swingHipFlex = 40f;
    public float swingKneeFlex = 55f;
    // March in place (intent.march, not walk): raised-leg pose, hip flexion minus, knee plus.
    // The hip stop is −90°, so 90 sits on the limit by design.
    [Header("March in place (not walk)")]
    [Tooltip("Air hip flexion (deg, minus) while marching. One-leg hold ignores this.")]
    public float marchHipFlex = 90f;
    [Tooltip("Air knee angle (deg, plus) while marching. Joint target, not added to kneeBase.")]
    public float marchKneeFlex = 90f;
    // The march pelvis target stays world vertical: pelvis tilt as CoM compensation is bistable.
    // CoM is held by a delayed ankle cap, then toe/heel gates after pose-up. Retired tilt fields stay
    // 0; do not add another cap, delay or tilt.
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
    // Grounded swing knee flex as a fraction of swingKneeFlex: full flex folds the chain, zero never
    // lifts the foot. After the latch the full swingKneeFlex applies.
    public float swingKneeGroundedFraction = 0.25f;
    // Extra swing hip signal toward the flexor (decreases jointAngle). While the foot is down both
    // hips stay on the pelvis PD; the bias unloads swing GRF so the knee lifts the foot instead of
    // folding a closed chain.
    public float swingHipUnloadBias = 0.32f;
    // On left support the swing (right leg, +X) reaches forward and needs its own unload/knee gains.
    public float forwardSwingHipUnloadScale = 1.3f;
    public float forwardSwingKneeGroundScale = 1.4f;
    // On right support the swing is the left leg behind (−X); pelvis tilt does not help there, so
    // it is unloaded the same way as the forward swing.
    public float backSwingHipUnloadScale = 1.3f;
    public float backSwingKneeGroundScale = 1.4f;
    // Fraction of standLegPelvisTilt applied backward on right support. 1.0 (−10°) plants both feet.
    public float backPelvisTiltFraction = 0f;
    // Plantarflex on the grounded forward swing. 0 = off; +12° drops left support.
    public float swingAnkleGroundedToeOff = 0f;
    // Pelvis tilt on left support: unloads the forward swing (right leg). Not on right support in
    // the one-leg probe: it blocks back-swing lift-off.
    public float standLegPelvisTilt = 10f;
    // Same tilt and sign on right support, fall-catch walk only: there the swing always reaches
    // forward whichever leg it is, so a left-only tilt makes the gait asymmetric.
    [Tooltip("Fraction of standLegPelvisTilt applied on RIGHT support during fall-catch walk, same sign as left support. 0 = former left-only tilt.")]
    public float walkStandLegPelvisTiltMirror = 1f;
    // While walkActive and split, soften swing unload/knee/tilt (not only in double support: the
    // lift-off frame would restore full). Arm counter-reach is off on walkActive.
    [Tooltip("Fraction of swing unload/knee/tilt on walk + split.")]
    public float dualSupportSwingScale = 0.32f;
    // Cap standLegLevel over all of walkActive: cutting only in double support lets a brief lift-off
    // send the level to 1 and fold after the plant. Cold one-leg (walkActive false) is uncapped.
    [Tooltip("Max standLegLevel while walkActive. 0 = no cap.")]
    public float dualSupportStandCap = 0.40f;
    // The cap and dualSoft are the floor of a ramp that lifts as the swing unloads, but the fall-catch
    // gait skips that ramp, leaving ~13% swing authority. This restores it from measured swing-foot
    // clearance. 0 = frozen floor.
    [Tooltip("Fall-catch: swing-foot air release lifts the cap/unload ramp. 0 = frozen floor.")]
    public float walkAirReleaseAuthority = 1f;
    // Double support: lift the same ramp from statics. The trailing foot carries
    // (x_lead − x_com) / (x_lead − x_trail) of the weight, so it is free once CoM is on the lead.
    // One-sided: it can only raise the ramp. 0 = clearance-only ramp.
    [Tooltip("Fall-catch double support: measured weight share off the swing foot lifts the cap/unload ramp. 0 = clearance-only ramp.")]
    public float walkDualLoadRelease = 1f;
    // Feet closer than this along the walk are one support and the share degenerates. Quiet-stance
    // soles sit ~3 cm apart in projection; 0.02 m matches PickStanceFromFeet's FootLeadDeadzone.
    private const float DualLoadMinSpan = 0.02f;
    // First step (cycle 0) air plant: swing hip SPD target −8° (flexion is minus) and swing knee 6°,
    // applied only once the dirt lift has latched air; the stance knee stays the straight strut.
    private const float FirstStepPlantHipDeg = 8f;
    private const float FirstStepPlantKneeDeg = 6f;
    // First-step dirt lift once CoM is on the leading foot. Matches swingKneeFlex; the full
    // reachKneeFlex (140°) on a stacked chain is a squat.
    private const float FirstStepLiftKneeDeg = 55f;
    // Double support: use the measured statics share instead of a 0.5 floor: leading share for
    // stanceLoad, trailing share for the Plant rear hip and ankle. 0 = 0.5 floor / full Plant A2.
    [Tooltip("Fall-catch double support: measured leading/trailing weight share for stanceLoad and the Plant rear hip/ankle. 0 = 0.5 floor / former Plant A2.")]
    public float walkStanceLoadShare = 1f;
    // Plant: fold the trailing knee by swingKneeFlex × measured unload (liftGateBlend) so the rear
    // foot lifts instead of shuffling; knee torque does not fight the pelvis loop, and a loaded foot
    // is not shortened. The trailing ankle fades its plantarflex. 0 = no swing fold in Plant.
    [Tooltip("Plant: fold the unloaded trailing knee (swingKneeFlex × load-share). 0 = former Plant, no swing fold.")]
    public float walkPlantSwingKnee = 1f;
    // Remap the 0.5…1 load share onto 0…1 of the fold so knee, hipLift, ankleLift and thighHold ramp
    // from 0 at the gate (no 27.5° step); DrivePelvis stays until plantFold >= 0.5.
    // 0 = swingKneeFlex × clock, full hold, immediate DrivePelvis drop.
    [Tooltip("Plant swing fold: remap the 0.5…1 load-share onto 0…1 pose (no 27.5° step at mid-span). 0 = former swingKneeFlex × clock.")]
    public float walkPlantKneeRamp = 1f;
    // Cycle 0 (gait start): both feet sit under the hip, DualLoadMinSpan never opens and load-share
    // terms stay 0; this and the *First flags below run them on the plantMinDuration clock instead.
    // Here: the Plant swing-knee fold (via walkPlantKneeRamp). 0 = later cycles only.
    [Tooltip("Plant swing-knee fold also on cycle 0 (gait initiation). 0 = former later-cycle only.")]
    public float walkPlantSwingKneeFirst = 1f;
    // Cycle-0 Plant: extend the stance hip on the Plant clock (plus = thigh lower end back, carries
    // the pelvis forward); its usual push gates read 0 before any CoM lead. Fades by plantComLeadMax.
    // 0 = wait for CoM lead / airborne swing.
    [Tooltip("Plant stance-hip drive on cycle 0 (gait initiation). 0 = former wait for CoM lead / airborne swing.")]
    public float walkPlantStanceDriveFirst = 1f;
    // Cycle-0 Plant: fade the swing-leg viscosity weld (stopTorque) on the Plant clock, same 0.5 snap
    // gate as later cycles, so the SPD can shorten the shin. 0 = weld until air.
    [Tooltip("Plant swing-leg viscosity fade on cycle 0 (gait initiation). 0 = former DualLoad-only weld until air.")]
    public float walkPlantSwingFrictionFirst = 1f;
    // Cycle-0 Plant: fade the stance knee's extra 5° bend on the Plant clock so the only strut stays
    // tall while the swing folds. 0 = extra 5° until DualLoad or air.
    [Tooltip("Plant stance-knee extra bend fades on cycle 0 (gait initiation). 0 = former DualLoad-only 5° shuffle flex.")]
    public float walkPlantStanceStraightFirst = 1f;
    // Cycle-0 Plant: apply swingHipUnloadBias (minus = thigh forward) on the Plant clock; the capped
    // liftSoft × standLegLevel product gives only ~0.041 of 0.32 on stacked feet. 0 = that 0.041.
    [Tooltip("Plant swing-hip flexor unload on cycle 0 (gait initiation). 0 = former DualLoad-only 0.041 until air.")]
    public float walkPlantSwingUnloadFirst = 1f;
    // Cycle-0 Plant: tilt the DrivePelvis target by standLegPelvisTilt (raises the swing hip for
    // clearance) on the Plant clock instead of the capped 1.28° of 10°. 0 = 1.28° until air.
    [Tooltip("Plant pelvis-tilt unload on cycle 0 (gait initiation). 0 = former DualLoad-only 1.28° until air.")]
    public float walkPlantTiltFirst = 1f;
    // Cycle-0 Plant: hand the swing hip from DrivePelvis to ThighHold once plantFold >= 0.5 (cap
    // 16–32°), as later cycles do. Restore/surrender keep DrivePelvis. 0 = DrivePelvis until air.
    [Tooltip("Plant swing-hip ThighHold on cycle 0 once plantFold >= 0.5. 0 = former DrivePelvis until DualLoad or air.")]
    public float walkPlantHipReleaseFirst = 1f;
    // Cycle-0 Plant: once CoM is on the leading foot, fade the grounded swing ankle's CoM hold by the
    // Plant clock; before that its heel press is kept. 0 = CoM hold on both soles until air.
    [Tooltip("Plant swing-ankle CoM release on cycle 0 (gait initiation). 0 = former dual-support CoM hold on both soles.")]
    public float walkPlantSwingAnkleReleaseFirst = 1f;
    // Cycle-0 Plant: when plantFold writes ankleLift, keep the released swing-ankle signal plus only
    // the dorsiflex unweight (toe-up so µ=0.4 does not slide the shin) instead of the swing foot's
    // CoM loop, whose plantarflex cancels the lean. 0 = CoM overwrite.
    [Tooltip("Plant cycle-0: keep released swing-ankle CoM; ankleLift is dorsiflex only. 0 = former CoM overwrite at plantFold.")]
    public float walkPlantSwingAnkleKeepFirst = 1f;
    // Cycle 0, swing sole down: carry that ankle keep into Fall/Reach (and Catch until the plant
    // latches), skipping the CoM overwrite and the propulsive-half clamp that deletes dorsiflex.
    // 0 = CoM overwrite + propulsive half on the first Fall/Reach frame.
    [Tooltip("Plant→Fall cycle-0: keep released swing-ankle CoM + dorsiflex; skip propulsive-half. 0 = former Fall overwrite.")]
    public float walkPlantFallAnkleKeepFirst = 1f;
    // With that keep, Fall/Reach use the Plant release endpoint (zero), not the current signal that
    // Fall's dual support refills with the CoM loop on both soles. 0 = keep the current signal.
    [Tooltip("Plant→Fall cycle-0: Fall/Reach keep uses the released zero, not dual-support CoM. 0 = former keep of current.")]
    public float walkPlantFallAnkleReleaseHoldFirst = 1f;
    // Cycle-0 grounded Fall/Reach: hold the Plant swing-leg viscosity fade at its endpoint (1);
    // otherwise the PhaseAge reset restores the weld on the folding shin. 0 = weld restored.
    [Tooltip("Plant→Fall cycle-0: keep the Plant swing-leg viscosity fade through grounded Fall/Reach. 0 = former weld restore on PhaseAge reset.")]
    public float walkPlantFallFrictionHoldFirst = 1f;
    // Cycle-0 Fall/Reach: hold the Plant stance-hip drive at its endpoint (still faded by
    // plantComLeadMax); otherwise the stance hip returns to world-upright. 0 = upright on reset.
    [Tooltip("Plant→Fall cycle-0: keep the Plant stance-hip drive through Fall/Reach. 0 = former world-upright stance hip on PhaseAge reset.")]
    public float walkPlantFallStanceDriveHoldFirst = 1f;
    // Cycle-0 Fall/Reach: hold the Plant trunk lean (leanRoom × calm) until Fall's own lean ramp
    // overtakes, so the lean does not step to 0 on the first Fall frame. 0 = drop on reset.
    [Tooltip("Plant→Fall cycle-0: keep the Plant trunk lean through Fall/Reach. 0 = former PhaseAge-reset 9° drop.")]
    public float walkPlantFallLeanHoldFirst = 1f;
    // Cycle-0 Fall/Reach: hold the Plant pelvis-tilt unload (DrivePelvis target) instead of the capped
    // 1.28°, which lets the pelvis loop drop the swing hip. 0 = 1.28° on reset.
    [Tooltip("Plant→Fall cycle-0: keep the Plant pelvis-tilt unload through Fall/Reach. 0 = former DualLoad-only 1.28° on PhaseAge reset.")]
    public float walkPlantFallTiltHoldFirst = 1f;
    // Cycle-0 Fall/Reach: keep the stance knee's extra bend faded to 0; the extra 5° would lower the
    // stance hip and spend swing-foot clearance. 0 = extra 5° on reset.
    [Tooltip("Plant→Fall cycle-0: keep the Plant stance-knee straighten through Fall/Reach. 0 = former extra 5° on PhaseAge reset.")]
    public float walkPlantFallStanceStraightFirst = 1f;
    // Cycle-0 Fall/Reach: hold the Plant rock-lead CoP setpoint (rockRoom × calm) until Fall's own
    // roll overtakes; otherwise Fall drops to fallComLead and Reach to 0. 0 = drop on reset.
    [Tooltip("Plant→Fall cycle-0: keep the Plant rock-lead CoP through Fall/Reach. 0 = former fallComLead 0.025 / Reach 0 on PhaseAge reset.")]
    public float walkPlantFallRockHoldFirst = 1f;
    // Cycle-0 grounded Reach with rock lead live: keep ankleScale 1 so the stance sole can finish the
    // roll-over; air Reach keeps 0.25 (do not fight a real fall). 0 = Reach 0.25.
    [Tooltip("Plant→Fall cycle-0: keep Fall ankleScale 1 through grounded Reach while rock-lead is live. 0 = former Reach 0.25 on PhaseAge reset.")]
    public float walkPlantFallAnkleScaleHoldFirst = 1f;
    // Cycle-0 Fall/Reach, swing sole down: cap the swing fold at the Plant value (55°); reachKneeFlex
    // (140°) on a shared plant is a squat. Air keeps 140°. 0 = ramp to 140° on dirt.
    [Tooltip("Plant→Fall cycle-0: cap grounded Fall/Reach swing fold at the Plant 55°. 0 = former ramp to reachKneeFlex 140° on dirt.")]
    public float walkPlantFallFoldCapFirst = 1f;
    // Cycle-0 Fall/Reach, swing sole down: keep hipBalanceScale 1 so the pelvis does not sag onto
    // both soles; air keeps Fall 0.15 / Reach 0.25 for a real fall. 0 = 0.15 / 0.25 on dirt.
    [Tooltip("Plant→Fall cycle-0: keep Plant hipBalanceScale 1 through grounded Fall/Reach. 0 = former Fall 0.15 / Reach 0.25 on a calm stacked trunk.")]
    public float walkPlantFallHipBalanceHoldFirst = 1f;
    // Cycle-0 Fall/Reach, swing sole down: keep Plant's suppressWalkLean so the speed/base pelvis and
    // chest lean do not step in on the first Fall frame (it would push the lumbar onto its ±20° stop).
    // Air keeps the speed/base lean. 0 = lean step on dirt.
    [Tooltip("Plant→Fall cycle-0: keep Plant suppressWalkLean through grounded Fall/Reach. 0 = former 8°/11° speed+base lean step on dirt.")]
    public float walkPlantFallSuppressWalkLeanFirst = 1f;
    // Cycle-0 Fall/Reach, swing sole down: raise the fold ceiling from the Plant 55° a quarter of the
    // way to reachKneeFlex (≈76°), now that hip scale and lean are held. Air keeps reachKneeFlex.
    // 0 = 55° pin on dirt.
    [Tooltip("Plant→Fall cycle-0: let grounded Fall/Reach fold grow from Plant 55° a quarter of the way to reachKneeFlex. 0 = former 55° pin on dirt.")]
    public float walkPlantFallFoldGrowFirst = 1f;
    // Seed the grounded Fall/Reach fold at that ≈76° ceiling on the first frame instead of waiting
    // for the 2× clock to climb from 55°. 0 = climb from the Plant 55°.
    [Tooltip("Plant→Fall cycle-0: seed grounded Fall/Reach fold at the 76° grow cap instead of waiting for the 2× clock. 0 = former climb from Plant 55°.")]
    public float walkPlantFallFoldSeedFirst = 1f;
    // With the seeded fold, raise hipLift/ankleLift a quarter from the Plant leftover toward 1
    // (0.5 → 0.625) so the fold lifts the knee instead of sliding the foot back. Air keeps 0.5.
    // 0 = Plant leftover.
    [Tooltip("Plant→Fall cycle-0: seed hipLift/ankleLift a quarter of the way from the Plant leftover toward 1 so the 76° fold lifts instead of sliding. 0 = former Plant 0.5 bias under the seeded fold.")]
    public float walkPlantFallLiftSeedFirst = 1f;
    // Raise ThighHold's cap fade floor the same quarter (0.5 → 0.625) so the seeded hipLift is not
    // halved at the 32° cap. Air keeps 0.5. 0 = 0.5 floor.
    [Tooltip("Plant→Fall cycle-0: seed the ThighHold cap floor a quarter of the way from 0.5 toward 1 so the 0.625 hipLift actually holds at 32°. 0 = former 0.5 floor that halves the seeded bias.")]
    public float walkPlantFallLiftFloorSeedFirst = 1f;
    // Cycle-0 grounded Fall/Reach: move the hipLift/ankleLift/floor trio a further quarter toward 1
    // (0.625 → 0.719), all three together so the cap hold stays matched. Each later *LiftGrow* flag
    // nests one more quarter step. 0 = stop at 0.625.
    [Tooltip("Plant→Fall cycle-0: grow the matched 0.625 hipLift/ankleLift/floor trio a quarter toward 1 so the 32° leftover holds −0.517 instead of −0.391. 0 = former 0.625 trio.")]
    public float walkPlantFallLiftGrowFirst = 1f;
    // Nested quarter step of the lift trio toward 1: 0.719 → 0.789 (0 = stop at 0.719).
    [Tooltip("Plant→Fall cycle-0: grow the matched 0.719 hipLift/ankleLift/floor trio a quarter toward 1 so the 32° leftover holds −0.623 instead of −0.517. 0 = former 0.719 trio.")]
    public float walkPlantFallLiftGrowMoreFirst = 1f;
    // Nested quarter step of the lift trio toward 1: 0.789 → 0.842 (0 = stop at 0.789).
    [Tooltip("Plant→Fall cycle-0: grow the matched 0.789 hipLift/ankleLift/floor trio a quarter toward 1 so the 32° leftover holds −0.709 instead of −0.623. 0 = former 0.789 trio.")]
    public float walkPlantFallLiftGrowYetFirst = 1f;
    // Nested quarter step of the lift trio toward 1: 0.842 → 0.882 (0 = stop at 0.842).
    [Tooltip("Plant→Fall cycle-0: grow the matched 0.842 hipLift/ankleLift/floor trio a quarter toward 1 so the 32° leftover holds −0.778 instead of −0.709. 0 = former 0.842 trio.")]
    public float walkPlantFallLiftGrowEvenFirst = 1f;
    // Nested quarter step of the lift trio toward 1: 0.882 → 0.912 (0 = stop at 0.882).
    [Tooltip("Plant→Fall cycle-0: grow the matched 0.882 hipLift/ankleLift/floor trio a quarter toward 1 so the 32° leftover holds −0.831 instead of −0.778. 0 = former 0.882 trio.")]
    public float walkPlantFallLiftGrowStillFirst = 1f;
    // Nested quarter step of the lift trio toward 1: 0.912 → 0.934 (0 = stop at 0.912).
    [Tooltip("Plant→Fall cycle-0: grow the matched 0.912 hipLift/ankleLift/floor trio a quarter toward 1 so the 32° leftover holds −0.872 instead of −0.831. 0 = former 0.912 trio.")]
    public float walkPlantFallLiftGrowAgainFirst = 1f;
    // Nested quarter step of the lift trio toward 1: 0.934 → 0.951 (0 = stop at 0.934).
    [Tooltip("Plant→Fall cycle-0: grow the matched 0.934 hipLift/ankleLift/floor trio a quarter toward 1 so the 32° leftover holds −0.904 instead of −0.872. 0 = former 0.934 trio.")]
    public float walkPlantFallLiftGrowAlsoFirst = 1f;
    // Nested quarter step of the lift trio toward 1: 0.951 → 0.963 (0 = stop at 0.951).
    [Tooltip("Plant→Fall cycle-0: grow the matched 0.951 hipLift/ankleLift/floor trio a quarter toward 1 so the 32° leftover holds −0.927 instead of −0.904. 0 = former 0.951 trio.")]
    public float walkPlantFallLiftGrowExtraFirst = 1f;
    // Nested quarter step of the lift trio toward 1: 0.963 → 0.972 (0 = stop at 0.963).
    [Tooltip("Plant→Fall cycle-0: grow the matched 0.963 hipLift/ankleLift/floor trio a quarter toward 1 so the 32° leftover holds −0.945 instead of −0.927. 0 = former 0.963 trio.")]
    public float walkPlantFallLiftGrowPlusFirst = 1f;
    // Cycle-0 grounded Fall/Reach: raise the hip-lift cap a quarter from 32° toward the 40° scissor
    // pose (sci_a) to 34°, after the couple clamp, so the held thigh has room. Each later *CapGrow*
    // flag nests one more quarter step toward 40°. Air keeps 32°. 0 = 32° cap.
    [Tooltip("Plant→Fall cycle-0: grow the 32° hip-lift cap a quarter toward 40° so leftover at CapCeil has 2° of room. 0 = former 32° leftover.")]
    public float walkPlantFallLiftCapGrowFirst = 1f;
    // Nested quarter step of the hip-lift cap toward 40°: 34° → 35.5° (0 = stop at 34°).
    [Tooltip("Plant→Fall cycle-0: grow the 34° hip-lift cap a quarter toward 40° so leftover at CapGrowFirst has 1.5° of room. 0 = former 34° leftover.")]
    public float walkPlantFallLiftCapGrowMoreFirst = 1f;
    // Nested quarter step of the hip-lift cap toward 40°: 35.5° → 36.625° (0 = stop at 35.5°).
    [Tooltip("Plant→Fall cycle-0: grow the 35.5° hip-lift cap a quarter toward 40° so leftover at CapGrowMoreFirst has 1.125° of room. 0 = former 35.5° leftover.")]
    public float walkPlantFallLiftCapGrowYetFirst = 1f;
    // Nested quarter step of the hip-lift cap toward 40°: 36.625° → ≈37.47° (0 = stop at 36.625°).
    [Tooltip("Plant→Fall cycle-0: grow the 36.625° hip-lift cap a quarter toward 40° so leftover at CapGrowYetFirst has 0.84375° of room. 0 = former 36.625° leftover.")]
    public float walkPlantFallLiftCapGrowEvenFirst = 1f;
    // Nested quarter step of the hip-lift cap toward 40°: ≈37.47° → ≈38.10° (0 = stop at ≈37.47°).
    [Tooltip("Plant→Fall cycle-0: grow the 37.46875° hip-lift cap a quarter toward 40° so leftover at CapGrowEvenFirst has 0.6328125° of room. 0 = former 37.46875° leftover.")]
    public float walkPlantFallLiftCapGrowStillFirst = 1f;
    // Nested quarter step of the hip-lift cap toward 40°: ≈38.10° → ≈38.58° (0 = stop at ≈38.10°).
    [Tooltip("Plant→Fall cycle-0: grow the 38.1015625° hip-lift cap a quarter toward 40° so leftover at CapGrowStillFirst has 0.474609375° of room. 0 = former 38.1015625° leftover.")]
    public float walkPlantFallLiftCapGrowAgainFirst = 1f;
    // Nested quarter step of the hip-lift cap toward 40°: ≈38.58° → ≈38.93° (0 = stop at ≈38.58°).
    [Tooltip("Plant→Fall cycle-0: grow the 38.576171875° hip-lift cap a quarter toward 40° so leftover at CapGrowAgainFirst has 0.35595703125° of room. 0 = former 38.576171875° leftover.")]
    public float walkPlantFallLiftCapGrowAlsoFirst = 1f;
    // Nested quarter step of the hip-lift cap toward 40°: ≈38.93° → ≈39.20° (0 = stop at ≈38.93°).
    [Tooltip("Plant→Fall cycle-0: grow the 38.93212890625° hip-lift cap a quarter toward 40° so leftover at CapGrowAlsoFirst has 0.2669677734375° of room. 0 = former 38.93212890625° leftover.")]
    public float walkPlantFallLiftCapGrowExtraFirst = 1f;
    // Nested quarter step of the hip-lift cap toward 40°: ≈39.20° → ≈39.40° (0 = stop at ≈39.20°).
    [Tooltip("Plant→Fall cycle-0: grow the 39.1990966796875° hip-lift cap a quarter toward 40° so leftover at CapGrowExtraFirst has 0.200225830078125° of room. 0 = former 39.1990966796875° leftover.")]
    public float walkPlantFallLiftCapGrowPlusFirst = 1f;
    // Nested quarter step of the hip-lift cap toward 40°: ≈39.40° → ≈39.55° (0 = stop at ≈39.40°).
    [Tooltip("Plant→Fall cycle-0: grow the 39.399322509765625° hip-lift cap a quarter toward 40° so leftover at CapGrowPlusFirst has 0.15016937255859375° of room. 0 = former 39.399322509765625° leftover.")]
    public float walkPlantFallLiftCapGrowFurtherFirst = 1f;
    // Nested quarter step of the hip-lift cap toward 40°: ≈39.55° → ≈39.66° (0 = stop at ≈39.55°).
    [Tooltip("Plant→Fall cycle-0: grow the 39.54949188232421875° hip-lift cap a quarter toward 40° so leftover at CapGrowFurtherFirst has 0.1126270294189453125° of room. 0 = former 39.54949188232421875° leftover.")]
    public float walkPlantFallLiftCapGrowOnwardFirst = 1f;
    // Nested quarter step of the hip-lift cap toward 40°: ≈39.66° → ≈39.75° (0 = stop at ≈39.66°).
    [Tooltip("Plant→Fall cycle-0: grow the 39.6621189117431640625° hip-lift cap a quarter toward 40° so leftover at CapGrowOnwardFirst has 0.084470272064208984375° of room. 0 = former 39.6621189117431640625° leftover.")]
    public float walkPlantFallLiftCapGrowAheadFirst = 1f;
    // Nested quarter step of the hip-lift cap toward 40°: ≈39.75° → ≈39.81° (0 = stop at ≈39.75°).
    [Tooltip("Plant→Fall cycle-0: grow the 39.746589183807373046875° hip-lift cap a quarter toward 40° so leftover at CapGrowAheadFirst has 0.06335270404815673828125° of room. 0 = former 39.746589183807373046875° leftover.")]
    public float walkPlantFallLiftCapGrowAlongFirst = 1f;
    // Nested quarter step of the hip-lift cap toward 40°: ≈39.81° → ≈39.86° (0 = stop at ≈39.81°).
    [Tooltip("Plant→Fall cycle-0: grow the 39.80994188785552978515625° hip-lift cap a quarter toward 40° so leftover at CapGrowAlongFirst has 0.0475145280361175537109375° of room. 0 = former 39.80994188785552978515625° leftover.")]
    public float walkPlantFallLiftCapGrowForthFirst = 1f;
    // Cycle-0 grounded Fall/Reach: one more 0.25 step of the swing hip-lift cap toward sciADeg 40°
    // (~39.857° -> ~39.893°), leaving a little thigh lift room below the scissor pose.
    // Nested under walkPlantFallLiftCapGrowForthFirst; air is unaffected. 0 = keep ~39.857°.
    [Tooltip("Plant→Fall cycle-0: grow the 39.8574564158916473388671875° hip-lift cap a quarter toward 40° so leftover at CapGrowForthFirst has 0.035635896027088165283203125° of room. 0 = former 39.8574564158916473388671875° leftover.")]
    public float walkPlantFallLiftCapGrowBeyondFirst = 1f;
    // Next 0.25 step of the cycle-0 grounded hip-lift cap toward 40° (~39.893° -> ~39.920°).
    // Nested under walkPlantFallLiftCapGrowBeyondFirst. 0 = keep ~39.893°.
    [Tooltip("Plant→Fall cycle-0: grow the 39.893092311918735504150390625° hip-lift cap a quarter toward 40° so leftover at CapGrowBeyondFirst has 0.02672692202031612396240234375° of room. 0 = former 39.893092311918735504150390625° leftover.")]
    public float walkPlantFallLiftCapGrowPastFirst = 1f;
    // Next 0.25 step of the cycle-0 grounded hip-lift cap toward 40° (~39.920° -> ~39.940°).
    // Nested under walkPlantFallLiftCapGrowPastFirst. 0 = keep ~39.920°.
    [Tooltip("Plant→Fall cycle-0: grow the 39.91981923393905162811279296875° hip-lift cap a quarter toward 40° so leftover at CapGrowPastFirst has 0.0200451915152370929718017578125° of room. 0 = former 39.91981923393905162811279296875° leftover.")]
    public float walkPlantFallLiftCapGrowOverFirst = 1f;
    // Last 0.25 step of the cycle-0 grounded hip-lift cap toward 40° (~39.940° -> ~39.955°).
    // Nested under walkPlantFallLiftCapGrowOverFirst. 0 = keep ~39.940°.
    [Tooltip("Plant→Fall cycle-0: grow the 39.9398644254542887210845947265625° hip-lift cap a quarter toward 40° so leftover at CapGrowOverFirst has 0.015033893636427819728851318359375° of room. 0 = former 39.9398644254542887210845947265625° leftover.")]
    public float walkPlantFallLiftCapGrowAcrossFirst = 1f;
    // Cycle-0 grounded Fall/Reach: 2nd 0.25 step of the swing knee-fold cap toward reachKneeFlex
    // (~76° -> ~92° from Plant 55°, reach 140°). Knee torque is thigh-shin, so more fold shortens
    // the swing chain (2·L·cos(flex/2)) without more hip flex. Air is not capped. 0 = stop at ~76°.
    [Tooltip("Plant→Fall cycle-0: grow the grounded Fall/Reach fold a second quarter from 76° toward reachKneeFlex (92°). 0 = former 76° cap on dirt.")]
    public float walkPlantFallFoldGrowMoreFirst = 1f;
    // 3rd 0.25 step of the cycle-0 grounded knee-fold cap toward reachKneeFlex (~92° -> ~104°).
    // Nested under walkPlantFallFoldGrowMoreFirst. 0 = stop at ~92°.
    [Tooltip("Plant→Fall cycle-0: grow the grounded Fall/Reach fold a third quarter from 92° toward reachKneeFlex (104°). 0 = former 92° cap on dirt.")]
    public float walkPlantFallFoldGrowYetFirst = 1f;
    // 4th 0.25 step of the cycle-0 grounded knee-fold cap toward reachKneeFlex (~104° -> ~113°).
    // Nested under walkPlantFallFoldGrowYetFirst. 0 = stop at ~104°.
    [Tooltip("Plant→Fall cycle-0: grow the grounded Fall/Reach fold a fourth quarter from 104° toward reachKneeFlex (113°). 0 = former 104° cap on dirt.")]
    public float walkPlantFallFoldGrowEvenFirst = 1f;
    // 5th 0.25 step of the cycle-0 grounded knee-fold cap toward reachKneeFlex (~113° -> ~120°).
    // Nested under walkPlantFallFoldGrowEvenFirst. 0 = stop at ~113°.
    [Tooltip("Plant→Fall cycle-0: grow the grounded Fall/Reach fold a fifth quarter from 113° toward reachKneeFlex (120°). 0 = former 113° cap on dirt.")]
    public float walkPlantFallFoldGrowStillFirst = 1f;
    // 6th 0.25 step of the cycle-0 grounded knee-fold cap toward reachKneeFlex (~120° -> ~125°).
    // Nested under walkPlantFallFoldGrowStillFirst. 0 = stop at ~120°.
    [Tooltip("Plant→Fall cycle-0: grow the grounded Fall/Reach fold a sixth quarter from 120° toward reachKneeFlex (125°). 0 = former 120° cap on dirt.")]
    public float walkPlantFallFoldGrowAgainFirst = 1f;
    // 7th 0.25 step of the cycle-0 grounded knee-fold cap toward reachKneeFlex (~125° -> ~128.6°).
    // Nested under walkPlantFallFoldGrowAgainFirst. 0 = stop at ~125°.
    [Tooltip("Plant→Fall cycle-0: grow the grounded Fall/Reach fold a seventh quarter from 125° toward reachKneeFlex (129°). 0 = former 125° cap on dirt.")]
    public float walkPlantFallFoldGrowAlsoFirst = 1f;
    // 8th 0.25 step of the cycle-0 grounded knee-fold cap toward reachKneeFlex (~128.6° -> ~131.5°).
    // Nested under walkPlantFallFoldGrowAlsoFirst. 0 = stop at ~128.6°.
    [Tooltip("Plant→Fall cycle-0: grow the grounded Fall/Reach fold an eighth quarter from 129° toward reachKneeFlex (132°). 0 = former 129° cap on dirt.")]
    public float walkPlantFallFoldGrowExtraFirst = 1f;
    // 9th 0.25 step of the cycle-0 grounded knee-fold cap toward reachKneeFlex (~131.5° -> ~133.6°).
    // Nested under walkPlantFallFoldGrowExtraFirst. 0 = stop at ~131.5°.
    [Tooltip("Plant→Fall cycle-0: grow the grounded Fall/Reach fold a ninth quarter from 132° toward reachKneeFlex (134°). 0 = former 132° cap on dirt.")]
    public float walkPlantFallFoldGrowPlusFirst = 1f;
    // 10th 0.25 step of the cycle-0 grounded knee-fold cap toward reachKneeFlex (~133.6° -> ~135.2°).
    // Nested under walkPlantFallFoldGrowPlusFirst. 0 = stop at ~133.6°.
    [Tooltip("Plant→Fall cycle-0: grow the grounded Fall/Reach fold a tenth quarter from 134° toward reachKneeFlex (136°). 0 = former 134° cap on dirt.")]
    public float walkPlantFallFoldGrowFurtherFirst = 1f;
    // 11th 0.25 step of the cycle-0 grounded knee-fold cap toward reachKneeFlex (~135.2° -> ~136.4°).
    // Nested under walkPlantFallFoldGrowFurtherFirst. 0 = stop at ~135.2°.
    [Tooltip("Plant→Fall cycle-0: grow the grounded Fall/Reach fold an eleventh quarter from 136° toward reachKneeFlex (137°). 0 = former 136° cap on dirt.")]
    public float walkPlantFallFoldGrowOnwardFirst = 1f;
    // 12th 0.25 step of the cycle-0 grounded knee-fold cap toward reachKneeFlex (~136.4° -> ~137.3°).
    // Nested under walkPlantFallFoldGrowOnwardFirst. 0 = stop at ~136.4°.
    [Tooltip("Plant→Fall cycle-0: grow the grounded Fall/Reach fold a twelfth quarter from 137° toward reachKneeFlex (138°). 0 = former 137° cap on dirt.")]
    public float walkPlantFallFoldGrowAheadFirst = 1f;
    // 13th 0.25 step of the cycle-0 grounded knee-fold cap toward reachKneeFlex (~137.3° -> ~138.0°).
    // Nested under walkPlantFallFoldGrowAheadFirst. 0 = stop at ~137.3°.
    [Tooltip("Plant→Fall cycle-0: grow the grounded Fall/Reach fold a thirteenth quarter from 138° toward reachKneeFlex (139°). 0 = former 138° cap on dirt.")]
    public float walkPlantFallFoldGrowAlongFirst = 1f;
    // 14th 0.25 step of the cycle-0 grounded knee-fold cap toward reachKneeFlex (~138.0° -> ~138.5°).
    // Nested under walkPlantFallFoldGrowAlongFirst. 0 = stop at ~138.0°.
    [Tooltip("Plant→Fall cycle-0: grow the grounded Fall/Reach fold a fourteenth quarter from 139° toward reachKneeFlex (139.25°). 0 = former 139° cap on dirt.")]
    public float walkPlantFallFoldGrowCloseFirst = 1f;
    // 15th 0.25 step of the cycle-0 grounded knee-fold cap toward reachKneeFlex (~138.5° -> ~138.9°).
    // Nested under walkPlantFallFoldGrowCloseFirst. 0 = stop at ~138.5°.
    [Tooltip("Plant→Fall cycle-0: grow the grounded Fall/Reach fold a fifteenth quarter from 139.25° toward reachKneeFlex (139.5°). 0 = former 139.25° cap on dirt.")]
    public float walkPlantFallFoldGrowNearFirst = 1f;
    // Cycle-0 grounded Fall/Reach: pin the knee-fold cap to reachKneeFlex (140°, strut ~0.34 L)
    // instead of the nested 0.25 steps (~138.9°), so the grounded shin gets the full Fall fold.
    // Also gates the stance-drive load grow. 0 = keep the nested ~138.9°.
    [Tooltip("Plant→Fall cycle-0: pin the grounded Fall/Reach fold to reachKneeFlex (140°). 0 = former nested ~138.86° cap on dirt.")]
    public float walkPlantFallFoldGrowReachFirst = 1f;
    // Cycle-0 stance-hip drive: raise its load 0.25 from the DualLoad 0.5 floor toward 1 (0.625) so
    // the stance hip carries the pelvis over the lead foot under the 140° shin. Applied in both the
    // Plant drive and the Fall/Reach hold, so EnterFall is not a step. 0 = keep the 0.5 floor.
    [Tooltip("Plant→Fall cycle-0: grow the stance-hip drive load a quarter from the DualLoad 0.5 floor toward 1 so the 140° shin is carried forward. 0 = former 0.5-floor drive.")]
    public float walkPlantFallStanceDriveGrowFirst = 1f;
    // 2nd 0.25 step of the cycle-0 stance-hip drive load toward 1 (0.625 -> 0.719).
    // Nested under walkPlantFallStanceDriveGrowFirst. 0 = keep 0.625.
    [Tooltip("Plant→Fall cycle-0: grow the stance-hip drive load a second quarter from 0.625 toward 1 so the 140° shin is carried forward. 0 = former 0.625-load drive.")]
    public float walkPlantFallStanceDriveGrowMoreFirst = 1f;
    // 3rd 0.25 step of the cycle-0 stance-hip drive load toward 1 (0.719 -> 0.789).
    // Nested under walkPlantFallStanceDriveGrowMoreFirst. 0 = keep 0.719.
    [Tooltip("Plant→Fall cycle-0: grow the stance-hip drive load a third quarter from 0.719 toward 1 so the 140° shin is carried forward. 0 = former 0.719-load drive.")]
    public float walkPlantFallStanceDriveGrowStillFirst = 1f;
    // 4th 0.25 step of the cycle-0 stance-hip drive load toward 1 (0.789 -> 0.842).
    // Nested under walkPlantFallStanceDriveGrowStillFirst. 0 = keep 0.789.
    [Tooltip("Plant→Fall cycle-0: grow the stance-hip drive load a fourth quarter from 0.789 toward 1 so the 140° shin is carried forward. 0 = former 0.789-load drive.")]
    public float walkPlantFallStanceDriveGrowYetFirst = 1f;
    // 5th 0.25 step of the cycle-0 stance-hip drive load toward 1 (0.842 -> 0.881).
    // Nested under walkPlantFallStanceDriveGrowYetFirst. 0 = keep 0.842.
    [Tooltip("Plant→Fall cycle-0: grow the stance-hip drive load a fifth quarter from 0.842 toward 1 so the 140° shin is carried forward. 0 = former 0.842-load drive.")]
    public float walkPlantFallStanceDriveGrowFarFirst = 1f;
    // 6th 0.25 step of the cycle-0 stance-hip drive load toward 1 (0.881 -> 0.911).
    // Nested under walkPlantFallStanceDriveGrowFarFirst. 0 = keep 0.881.
    [Tooltip("Plant→Fall cycle-0: grow the stance-hip drive load a sixth quarter from 0.881 toward 1 so the 140° shin is carried forward. 0 = former 0.881-load drive.")]
    public float walkPlantFallStanceDriveGrowFartherFirst = 1f;
    // 7th 0.25 step of the cycle-0 stance-hip drive load toward 1 (0.911 -> 0.933).
    // Nested under walkPlantFallStanceDriveGrowFartherFirst. 0 = keep 0.911.
    [Tooltip("Plant→Fall cycle-0: grow the stance-hip drive load a seventh quarter from 0.911 toward 1 so the 140° shin is carried forward. 0 = former 0.911-load drive.")]
    public float walkPlantFallStanceDriveGrowFarthestFirst = 1f;
    // 8th 0.25 step of the cycle-0 stance-hip drive load toward 1 (0.933 -> 0.950).
    // Nested under walkPlantFallStanceDriveGrowFarthestFirst. 0 = keep 0.933.
    [Tooltip("Plant→Fall cycle-0: grow the stance-hip drive load an eighth quarter from 0.933 toward 1 so the 140° shin is carried forward. 0 = former 0.933-load drive.")]
    public float walkPlantFallStanceDriveGrowOutFirst = 1f;
    // 9th 0.25 step of the cycle-0 stance-hip drive load toward 1 (0.950 -> 0.962).
    // Nested under walkPlantFallStanceDriveGrowOutFirst. 0 = keep 0.950.
    [Tooltip("Plant→Fall cycle-0: grow the stance-hip drive load a ninth quarter from 0.950 toward 1 so the 140° shin is carried forward. 0 = former 0.950-load drive.")]
    public float walkPlantFallStanceDriveGrowOutMoreFirst = 1f;
    // 10th and last 0.25 step of the cycle-0 stance-hip drive load toward 1 (0.962 -> 0.972).
    // Nested under walkPlantFallStanceDriveGrowOutMoreFirst. 0 = keep 0.962.
    [Tooltip("Plant→Fall cycle-0: grow the stance-hip drive load a tenth quarter from 0.962 toward 1 so the 140° shin is carried forward. 0 = former 0.962-load drive.")]
    public float walkPlantFallStanceDriveGrowOutStillFirst = 1f;
    // Meant as an 11th stance-drive load step (0.972 -> 0.979), but no code reads this field,
    // so it currently has no effect.
    [Tooltip("Plant→Fall cycle-0: grow the stance-hip drive load an eleventh quarter from 0.972 toward 1 so the 140° shin is carried forward. 0 = former 0.972-load drive.")]
    public float walkPlantFallStanceDriveGrowOutYetFirst = 1f;
    // EnterFall resets PhaseAge, so Fall's fold/hipLift/ankleLift clocks restart at 0 and would unfold
    // the Plant pose in one step. Fall (and Reach before extension starts) keep Max(clocked, last Plant
    // pose) until the 2x ramp overtakes; Reach never holds past landDeg. 0 = Fall restarts from 0.
    [Tooltip("Fall/Reach: keep the Plant swing fold/lift until the Fall clock overtakes it. 0 = former PhaseAge-reset unfold.")]
    public float walkPlantFallFoldHold = 1f;
    // Let the unloading pelvis tilt also ramp off the dualSupportSwingScale floor with swing air
    // release, like unload and knee. Tilt raises the whole swing hip, buying clearance without more
    // knee flex; at contact (release 0) it still reads the floor. 0 = tilt stays on the floor.
    [Tooltip("Fall-catch: swing-foot air release lifts the pelvis-tilt softener too. 0 = frozen dualSupportSwingScale floor.")]
    public float walkStandTiltAirRelease = 1f;
    // Extra swing unload/knee scale on walk-split, on top of dualSoft and ramped by liftAct.
    // Pelvis tilt stays on dualSoft; scaling it too folds the body after a support swap. 1 = off.
    [Tooltip("Extra unload/knee scale on walk-split (not for tilt). 1 = off.")]
    public float walkSwingLiftScale = 1f;
    // Swing lift starts near the end of Stance (walkSingleSupport); an earlier lift drops the body.
    [Tooltip("Seconds of Stance before lift starts.")]
    public float walkStanceLiftDelay = 6f;
    [Tooltip("Seconds of liftAge ramp after the delay.")]
    public float walkStanceLiftRamp = 2f;
    // Kept separate from walkStanceLiftRamp: a faster weight ramp weakens the swing and sits the body.
    // CLI -walkWeightRamp.
    [Tooltip("Seconds of weightBlend (unload/cap) ramp in Stance with CoM on support.")]
    public float walkWeightRamp = 2f;
    // Run uses a shorter delay and ramp, otherwise the lift cannot finish within a run Stance.
    [Tooltip("walkStanceLiftDelay when intent.run=1.")]
    public float runStanceLiftDelay = 2f;
    [Tooltip("walkWeightRamp when intent.run=1.")]
    public float runWeightRamp = 1.5f;
    // Lift only when CoM is over the stance foot, not by timer alone; a timer-only lift folds the body.
    [Tooltip("|CoM−stance| ≤ this on walk dual-support to enable lift.")]
    public float walkLiftComMax = 0.10f;
    // Swing toe-off on walk only, once liftBlend is high; the global swingAnkleGroundedToeOff in
    // dual support drops the body.
    [Tooltip("Max swing toe-off on walk at liftBlend=1, deg. Forward swing only. 0 = off.")]
    public float walkSwingToeOffMax = 0f;
    // Extra hip unload and grounded-swing knee peel from the early lift activation on walk-split.
    [Tooltip("Extra hip-unload at liftAct=1 on walk-split.")]
    public float walkLiftUnloadBias = 0.02f;
    [Tooltip("Fraction of swingKneeFlex on the grounded swing at liftAct=1.")]
    public float walkKneePeelMax = 0.05f;
    // Stance-foot push at full weightBlend: CoM over support alone does not lift the swing
    // (ankleBalance ≈ 0). A signal into ankleBalance, not a PD to an angle. 0 = off.
    [Tooltip("Extra plantar on the stance ankle at weightBlend≈1. 0 = off.")]
    public float walkStancePush = 0.05f;
    [Tooltip("walkStancePush scale for run. 0 = no push-off in run.")]
    public float runStancePushScale = 0f;
    // Hip half of push-off: the ankle rolls the body over the toe, the stance hip carries the pelvis
    // over the planted foot (extension is plus). Gated on the ankle push's load × CoM-lead window and
    // clamped, so the hip cannot outrun its own foot. 0 = pelvis-only stance hip.
    [Tooltip("Stance-hip extension bias at full load and full CoM lead. 0 = off.")]
    public float walkStanceHipExtend = 0.15f;
    [Tooltip("Clamp on the stance-hip extension bias.")]
    public float walkStanceHipExtendMax = 0.25f;
    // Let walkStancePush / walkStanceHipExtend fire on signed stanceLead (CoM past the stance foot)
    // instead of waiting for the swing to leave: with the feet a step apart that is where push-off
    // rolls the body forward, and waiting for air stalls travel. 0 = airborne-only gate.
    [Tooltip("Stance push-off and hip extend may fire once CoM has passed the stance foot, even if the swing is still down. Legacy Stance ramps this with the geo-lift clock. 0 = former airborne-only gate.")]
    public float walkStancePushOnLead = 1f;
    // Trailing-ankle A2 push-off in double-support Plant: the only window where that foot is down
    // with CoM already past it, so plantarflexion puts CoP behind CoM and rolls the body forward.
    // A signal into ankleBalance, not an ankle-angle PD (balance-actuators.mdc). 0 = off.
    [Tooltip("Trailing-ankle push-off in double-support Plant. 0 = off.")]
    public float walkPlantPushScale = 0.35f;
    [Tooltip("Rear moment arm where the Plant push reaches full scale, m.")]
    public float walkPlantPushLeadWindow = 0.08f;
    // Plant CoM setpoint ahead of the stance foot: the Plant→Fall gate waits for CoM to lead by
    // fallComLeadTrigger, so the same ankle loop dorsiflexes, CoP goes behind CoM and the body rolls
    // onto the ball of the foot while both feet share the reaction. 0 = hold CoM over the foot.
    [Tooltip("Forward CoM setpoint over the stance foot during Plant, m — the roll-over the Plant→Fall gate waits for. 0 = off.")]
    public float walkPlantRockLead = 0.03f;
    // Trunk half of the Plant roll-over (rock lead moves CoP, this moves mass): lean by
    // fallForwardLeanScale × fallPelvisLean, ramped over plantMinDuration, collapsed on trunk tip,
    // faded to 0 as the extrapolated CoM lead reaches the Fall trigger. 0 = upright Plant.
    [Tooltip("Forward trunk lean in Plant as a fraction of the Fall lean, faded out as the extrapolated CoM reaches the Plant→Fall trigger. 0 = former upright Plant.")]
    public float walkPlantLeanRoll = 1f;
    // Apply walkPlantLeanRoll on cycle 0 too: gait initiation needs the trunk lean, which is legal
    // with both feet under the hip (shared reaction). Same amount, clock, calm and fade as later
    // cycles. 0 = later cycles only.
    [Tooltip("Plant trunk lean also on cycle 0 (gait initiation). 0 = former later-cycle only.")]
    public float walkPlantLeanFirst = 1f;
    // Trailing-hip extension in Plant (pre-swing hip drive): against the planted rear foot it carries
    // the pelvis forward, via the walkStanceHipExtend counter channel and as a fraction of it. Fades
    // on plantComLeadMax room and ends as the rear foot starts to clear. 0 = idle Plant hip.
    [Tooltip("Trailing-hip extension in double-support Plant, as a fraction of walkStanceHipExtend. 0 = off.")]
    public float walkPlantHipExtend = 1f;
    // A toe scrape is a grounded foot: send the Fall/Reach/Catch scrape command to the balance ankle
    // instead of the airborne world-flat pose, since an angle PD on a pressed sole turns the body.
    // Only when that block wrote a command. 0 = world-flat pose.
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
    // Fade stance push-off from walkLiftComMax to zero at the toe edge (footSize.x/2 ahead of centre)
    // instead of cutting it at walkLiftComMax: past the toe there is no sole to press, and a one-step
    // cut of a saturated ankle is a torque step. 0 = hard cut at walkLiftComMax.
    [Tooltip("Stance push-off fade from walkLiftComMax toward the toe edge. 0 = former hard cut at walkLiftComMax.")]
    public float walkPushToeFade = 1f;
    [Tooltip("Use XCoM in stance ankle error (0=CoM, 1=full XCoM).")]
    public float walkXCoMWeight = 0.6f;
    [Tooltip("Effective COM height (m) for XCoM omega0 = sqrt(g/h).")]
    public float walkXCoMHeight = 1.0f;
    // Low-pass the CoM velocity read by walk capture-point terms (ankle shift, Plant→Fall gate, step
    // target, walkSpeedError): a heel strike swings the raw 200 Hz difference by cm in one tick.
    // One shared reading; quiet stance keeps the raw sample. 0 = raw velocity.
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
    // Landing behind the CoM turns a step into a stumble: the catch spends the next cycle recovering.
    // The placement floor is the capture point plus this margin. 0 = step length only.
    [Tooltip("Metres the landing foot must clear the capture point by, m.")]
    public float walkStepComMargin = 0.06f;
    // Step target is the capture point (CoM + comVel/omega0); walkStepLength only caps it. Landing on
    // it reaches the top of the arc with zero speed; landing beyond it stops the body and rocks it
    // back every step. 0 = walkStepLength as a floor.
    [Tooltip("1 = step target is the capture point (walkStepLength caps it). 0 = former floor.")]
    public float walkStepCaptureTarget = 1f;
    // Only guards against a negative capture step putting the foot behind the stance foot. Body
    // colliders ignore each other, so a short step is geometrically free.
    [Tooltip("Shortest commanded step from stance to swing foot, m.")]
    public float walkStepMinLength = 0.08f;
    // Make the commanded landing also meet StepPhaseDriver.catchFootLead (+ walkStepCatchLeadPad for
    // CoM advance during descent), otherwise Catch can only exit via trunkCaught or timeout. One-sided
    // Max, so a faster body keeps its longer capture step. 0 = capture-point-only command.
    [Tooltip("1 = commanded landing point also meets StepPhaseDriver.catchFootLead. 0 = former capture-point-only command.")]
    public float walkStepCatchLeadSync = 1f;
    [Tooltip("Extra metres past catchFootLead in the commanded landing point (CoM advance during the last descent), m.")]
    public float walkStepCatchLeadPad = 0.02f;
    [Tooltip("Stance phase progress when placement control becomes active.")]
    public float walkPlacementSyncStart = 0.72f;
    [Tooltip("Ground clearance threshold (m) for touchdown-sync window.")]
    public float walkTouchdownSyncClearance = 0.06f;
    // Swing-hip retraction near touchdown: a forward-moving sole brakes (µ=0.4) and heel-strike loss
    // grows with speed²; retraction's reaction pushes the trunk forward. Dosed by foot forward speed,
    // only over the last walkTouchdownSyncClearance of descent. 0 = off.
    [Tooltip("Swing-hip retraction near touchdown, deg per 1 m/s of swing-foot forward ground speed. 0 = no velocity match.")]
    public float walkSwingRetractGain = 6f;
    [Tooltip("Clamp for swing-hip touchdown retraction, deg.")]
    public float walkSwingRetractMax = 6f;
    // Solve the airborne swing-hip target from the measured hip→ankle strut so the foot lands on
    // desiredStep, instead of a constant 46-56° flexion that overshoots and leaves the foot hanging.
    // Clamped to [-swingHipFlexEffective, 0] (flexion minus); air only. 0 = constant-flexion reach.
    [Tooltip("Solve the airborne swing-hip target from leg geometry so the foot lands on desiredStep. 0 = former constant swingHipFlex reach.")]
    public float walkSwingPlaceGeometry = 1f;
    // Where the geometric solve owns the airborne hip target, drop placementHipBoost and the anti-stomp
    // Min(latch, solve): both act on the same step error and drive the thigh past the solution, paid
    // as a pelvis shove. Grounded swing is unchanged. 0 = stacked boost and latch Min.
    [Tooltip("Let the geometric swing solve own the airborne hip target (no P-boost, no latch Min). 0 = former stacked pair and Min.")]
    public float walkSwingPlaceGeoOnly = 1f;
    // Aim the swing solve at the foot centre (what desiredStep and IsSwingFootAheadOfCom measure), not
    // the ankle hinge 0.065 m behind it. The offset is measured from the transforms and one-sided
    // (Max 0), so it only reduces reach. 0 = hinge-referenced target.
    [Tooltip("Aim the geometric swing solve at the foot centre (what desiredStep and the catch criterion measure) instead of the ankle hinge. 0 = former hinge-referenced target.")]
    public float walkSwingPlaceFootRef = 1f;
    // Solve the landing knee flex by the law of cosines from the measured thigh/shin bars so the strut
    // just reaches the ground (a constant 35° leaves it ~5 cm short). Min: only straightens; ramped
    // by touchdownWindow over the last descent. 0 = constant reachKneeLandDeg.
    [Tooltip("Solve the landing swing-knee flex from leg geometry so the sole reaches the ground without the body sinking. 0 = former constant reachKneeLandDeg.")]
    public float walkSwingLandKneeGeometry = 1f;
    [Tooltip("Floor under the solved landing knee flex (deg) so the shin does not land on its own 0 stop.")]
    public float walkLandKneeMinDeg = 8f;
    // Stacked dual support: fold the swing knee just enough for the sole to clear (inverse of
    // LandingKneeFlexDeg) and lock the stance knee at 0, so the pelvis sits on the long leg and the
    // short sole unloads. Only while liftGateBlend ~0; also WALK-1 Stance. 0 = off.
    [Tooltip("Stacked dual-support: geometric swing-knee clearance + stance knee 0. Not DualLoad, not ankle-zero, not a pelvis fight. Also WALK-1 Stance. 0 = off.")]
    public float walkGeoSwingLift = 1f;
    [Tooltip("Sole height the swing-knee solve aims for while the stance strut is locked, m. Must exceed BodyStateEstimator.groundProbeThickness (0.06) or the probe still reports grounded and Fall never sees air. 0.08 netted inside that 6 cm band after a 2 cm closed-chain hip drop: firstAir never, then UpdateState fallCoMOffset 0.30 at walk start +1.30 s (burn_stag 6.300). 0.12 is 2× the probe.")]
    public float walkGeoSwingLiftClearance = 0.12f;
    // WALK-1 Stance: hip/knee JointFriction (K = 12, stopTorque 15 N·m > ~11 N·m swing-leg gravity)
    // welds the swing leg, so the geo-lift knee command cannot shorten it. Fade swing hip/knee
    // viscosity on the geo-lift clock; stance joints and ankles keep theirs. 0 = keep the weld.
    [Tooltip("WALK-1 Stance: fade swing hip/knee viscosity on the geo-lift clock so the knee can shorten. Stance damper and ankle CoM stay. 0 = former weld.")]
    public float walkStanceGeoFriction = 1f;
    // First Stance: arm geo-lift only after PlantStandReady (CoM past the stance toe), so the stance
    // keeps standLeg and its ankle can roll CoM onto that foot. 0 = idle-clock lift on stacked feet.
    [Tooltip("First Stance: >0 waits for PlantStandReady (xi past the stance toe) before geo-lift. 0 = idle-clock lift.")]
    public float walkFirstLiftLead = 0.03f;
    // The 8 s Stance progress clock holds walkComLeadX at millimetres for seconds, so the lift gate
    // never opens. First dual support ramps the setpoint over this time instead; later stances keep
    // the progress ramp. 0 = 8 s Stance progress.
    [Tooltip("First Stance: seconds to ramp walkComLeadX onto the stance sole. 0 = former 8 s Stance progress.")]
    public float walkFirstLeadSeconds = 0.40f;
    // Cycle 0: stacked feet have ~0 CoM lead, so pushWindow keeps walkStancePush off while the hip
    // already drives. Add the plantar overlay scaled by the hip-drive envelope, minus what pushWindow
    // already delivered. Not an ankle-angle PD. 0 = wait for lead.
    [Tooltip("Cycle-0: stance-ankle plantarflex with the hip drive, without waiting for CoM lead. 0 = former pushWindow gate.")]
    public float walkPlantStancePushFirst = 1f;
    // Grounded placement is scaled by walkStepPlacementGroundFraction (0). Once the swing knee is
    // folded (geo lift or the 140° pin), hip flexion reaches the sole forward instead of shoving the
    // pelvis, so aim it at the capture-point landing. 0 = air-only placement.
    [Tooltip("Folded/light swing: aim the hip at the capture-point landing even if the probe still says grounded. 0 = former air-only placement.")]
    public float walkStepPlaceOnFold = 1f;
    // Built hip/knee JointFriction (K = 12, 15 N·m) is past the explicit-damper bound: it removes all
    // relative speed and welds a ~14 kg swing leg (~11 N·m gravity). Airborne swing leg only:
    // K = Min(built, pairedInertia / tau). Stance keeps its damper. 0 = keep the built weld.
    [Tooltip("Passive hip/knee viscosity time constant on the AIRBORNE swing leg during fall-catch walk (s). K = pairedInertia/tau, applied only where it is below the built value. 0 = former built weld.")]
    public float walkSwingLegFrictionTau = 0.10f;
    // Swing-ankle counterpart: built K = 3 with paired inertia ~0.0055 kg·m² gives K·dt/I ≈ 2.7 > 2,
    // welding the foot to the shin so air ankle SPD targets cannot move the sole. K = Min(built,
    // I/tau) while airborne, built again at touchdown. 0 = keep the built weld.
    [Tooltip("Passive ankle viscosity time constant on the AIRBORNE swing foot during fall-catch walk (s). K = pairedInertia/tau, applied only where it is below the built value. 0 = former built weld.")]
    public float walkSwingAnkleFrictionTau = 0.10f;
    // Also fade swing-leg viscosity by measured unload (liftGateBlend: 0 rear loaded, 1 CoM on lead
    // foot), Max with clearance, so the weld does not eat a held Plant fold on a still-grounded
    // trailing foot. Catch plants keep blend 0. 0 = clearance-only fade until air.
    [Tooltip("Fall-catch: measured swing-foot unload also fades swing-leg viscosity (same liftGateBlend as the lift ramp). 0 = former clearance-only weld until air.")]
    public float walkSwingFrictionLoadShare = 1f;
    // Lerping K from the built 12 stays on the stopTorque weld until fade ~0.7 on the knee. On dirt,
    // once the unload envelope reaches 0.5 (the fold gate), snap K to I/tau; touchdown keeps the
    // airRelease lerp. 0 = linear K lerp on dirt too.
    [Tooltip("Fall-catch: on dirt, jump swing-leg K to I/tau once unload-share ≥ 0.5 (leave the stopTorque weld). 0 = former linear K lerp, still welded until fade ~0.7.")]
    public float walkSwingFrictionClampSnap = 1f;
    // Keep the dirt K snap through the first millimetres of lift-off, where airRelease ~0.02 makes
    // the lerp a weld again and the sole replants. Held until fade ≥ ~0.7, a swing-side change or
    // the envelope closes; catch plants never arm it. 0 = dirt-only snap.
    [Tooltip("Fall-catch: keep the dirt tau-K snap through the first millimetres of lift-off, until fade ≥ 0.7 (lerp itself leaves stopTorque). 0 = former dirt-only pulse, weld returns at first clearance.")]
    public float walkSwingFrictionSnapHold = 1f;
    // SPD divides by 1 + k·dt/I with k from the flexor torque, but the ankle is asymmetric (dorsiflexor
    // 22 vs calf 65 N·m): that under-damps plantarflex steps, overshooting toe-down at touchdown.
    // Pick k from the muscle the command sign engages. 0 = flexor-only denominator.
    [Tooltip("Fall-catch walk: build the airborne swing-ankle SPD denominator from the muscle that actually drives the step (calf 65 N·m on plantarflex, not the 22 N·m dorsiflexor). 0 = former flexor-only denominator.")]
    public float walkSwingAnkleSpdTorque = 1f;
    [Tooltip("Extra stance knee bend on walk, deg.")]
    public float walkStanceKneeBend = 10f;
    // Fall-catch stance knee bend: fade the 0.5 schedule to 0 as the swing foot clears, since bending
    // lowers the hip (2·L·cos(knee/2)) and spends clearance just when single support needs the tallest
    // strut. At liftGateBlend 0 both schedules read 0.5. 0 = growing Lerp(0.5, 1, liftGateBlend).
    [Tooltip("Fade the fall-catch stance knee bend out as the swing foot clears instead of growing it. 1 = fade to 0 at full lift, 0 = former Lerp(0.5,1,liftGateBlend).")]
    public float walkStanceKneeBendLiftFade = 1f;
    // kneeRecoveryFlex is a quiet-stance reflex (|CoM offset| > recoveryCoMOffset). Fall-catch drives
    // the stance CoM lead past that by design, so it would flex the only loaded strut every cycle.
    // Fraction kept on the fall-catch stance knee; swing knee and other paths unaffected. 1 = full.
    [Tooltip("Fraction of kneeRecoveryFlex kept on the STANCE knee during fall-catch walk. 0 = none (the quiet-stance reflex stays off the loaded strut), 1 = former behaviour.")]
    public float walkStanceKneeRecoveryFlex = 0f;
    [Tooltip("Base forward pelvis lean on walk, deg.")]
    public float walkForwardPelvisLean = 2f;
    [Tooltip("Base forward torso lean on walk, deg.")]
    public float walkForwardTorsoLean = 4f;
    // Stance extend at full weightBlend: raising the pelvis takes load off the swing foot. Not swing
    // flex. Legacy walk only (not fall-catch). 0 = off.
    [Tooltip("Stance-extend fraction (knee/pelvis→0) at weightBlend=1. 0 = off.")]
    public float walkStanceExtend = 0f;
    // Walking is a controlled forward fall: hold CoM walkComLeadX ahead of the stance foot so gravity
    // drives and the swing plants, instead of an ankle target pulling the body back. Ramped on the
    // WALK-1 geo-lift clock to avoid an ankle-torque step on stacked soles. 0 = mid-support hold.
    [Tooltip("Metres past the support front edge to hold CoM when moveX≠0. 0 = former mid-support hold.")]
    public float walkComLeadX = 0.06f;
    // WALK-2 Fall: move the ankle CoP target fallComLead ahead of the CoM so the body topples forward
    // over a loaded, controlled ankle (switching the ankle off lets it drift backward). Read only with
    // useFallCatchWalk≠0 in Fall.
    [Tooltip("Metres the ankle CoP target moves forward during Fall (WALK-2).")]
    public float fallComLead = 0.025f;
    // Roll the Fall CoM setpoint from foot centre toward the toe (StanceToeReach()), like CoP in real
    // stance; a fixed setpoint parks the body. 0.75 of the 0.13 m sole ends at 0.0975 m, inside the
    // sole and walkLiftComMax 0.10. 0 = fixed fallComLead.
    [Tooltip("Fraction of the sole (foot centre → toe) the Fall CoM setpoint rolls out to. 0 = former fixed fallComLead.")]
    public float walkFallComRollToe = 0.75f;
    // Swing pose starts only after lift-off, but lift-off needs the thigh forward: a deadlock with
    // both hips holding the pelvis upright. The scissor gives the grounded swing a forward pose once
    // weightBlend shows load on support. Legacy walk only. 0 = off.
    [Tooltip("weightBlend from which the grounded swing reaches forward. 0 = off.")]
    public float walkSwingScissorLevel = 0.20f;
    // Reaching the thigh forward throws the pelvis back (third law): full grounded −swingHipFlex lifts
    // the foot but the body travels backward and folds. Grounded reach is a fraction; air stays full.
    [Tooltip("Fraction of swingHipFlex in the grounded scissor pose. 1 = full reach.")]
    public float walkSwingScissorFlex = 0.25f;
    // Swing lift as a hip-to-hip couple: each hip torque also puts −tau on the pelvis, so the stance
    // hip gets the swing hip's last signal with opposite sign; the pelvis reactions cancel and the
    // stance thigh gets push-off extension. Ankle loops unchanged. 0 = pelvis takes the reaction.
    [Tooltip("Fraction of the swing-hip signal fed back to the stance hip. 0 = off.")]
    public float walkSwingLiftCouple = 1f;
    [Tooltip("Clamp on the stance-hip counter signal. Keeps the pelvis loop's own authority.")]
    public float walkSwingLiftCoupleMax = 0.6f;
    // The grounded lift caps only bound the third-law shove; while the stance leg absorbs it the cap
    // may widen, hard-clamped at coupleCapCeilingDeg (well short of the 40° scissor pose).
    [Tooltip("Scale on the grounded hip-lift cap while the couple is active. 1 = off.")]
    public float walkSwingLiftCoupleCapScale = 1.5f;
    // Airborne Reach is the only window to choose the landing. Start the hip target at the lift-off
    // thigh angle (zero SPD error) and ramp it onto the placement target, so the pelvis reaction is
    // bounded by the ramp. 0 = hold-only Reach (thigh at world vertical).
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
    // After a scrape, hipair goes upright and plants the foot. Keep the swing-hip flex this many
    // seconds after the foot touches down again (armed at the air latch). 0 = off.
    [Tooltip("Seconds to hold swing-hip flex after !grounded. 0 = off.")]
    public float walkSwingAirHold = 0f;
    // After the latch, keep the swing-hip flex until the heel is loaded; a toe touch does not drop it.
    // 0 = hipair (flex only while !grounded).
    [Tooltip("1 = swing pose from !heelLoaded after latch. 0 = hipair.")]
    public float walkSwingHeelPlant = 0f;
    // A sticky latch keeps the grounded knee (groundKnee) off after a scrape; clearing it when the
    // swing is grounded restores groundKnee. 0 = sticky latch.
    [Tooltip("1 = clear swingHipLatched when the swing is grounded. 0 = sticky latch.")]
    public float walkSwingUnlatchPlant = 0f;
    // Latching on the first air frame kills groundKnee on plant; require this much continuous
    // !grounded time first. 0 = hipair (latch immediately).
    [Tooltip("Seconds of continuous air before latch. 0 = from the first frame.")]
    public float walkSwingLatchAir = 0f;
    // In the air hipair holds ankle joint 0 (foot ⊥ shin), so knee flex points the toe down and it
    // scrapes. 1 = world-horizontal foot target after the latch.
    [Tooltip("1 = after latch in the air, foot to world horizontal. 0 = joint 0.")]
    public float walkSwingAirLevel = 0f;
    // Scale standLegRate up with liftBlend on walk so the level keeps up with the rising cap;
    // otherwise unload stays weak.
    [Tooltip("standLegRate scale at liftBlend=1 on walk. 1 = off.")]
    public float walkLiftLevelRateScale = 1f;
    // Debug: actual dual-support scale from the last FixedUpdate.
    [System.NonSerialized] public float lastDualSoft = 1f;
    [System.NonSerialized] public float lastLiftBlend;
    public float standLegRatePerSecond = 1.5f;
    [Tooltip("Release of the one-leg pose at standLeg=0 — faster than lift so the transfer window does not drift.")]
    public float standLegReleaseRatePerSecond = 4f;
    // A walk-key release usually lands mid-step with the swing airborne. Keep the walk support sign
    // until standLegLevel decays (≤ 0.25 s at standLegReleaseRatePerSecond 4/s), so the airborne
    // foot keeps its role and lands. 0 = drop the sign instantly.
    [Tooltip("1 = keep the walk support sign through the release window (until standLegLevel decays). 0 = former instant drop.")]
    public float walkEndHoldSupport = 1f;
    // 0 = dual support, 1 = full swing pose. Smoothed like crouchLevel.
    public float standLegLevel;

    // Error and speed are divided by references, so the PD input is dimensionless and the gains are
    // order-one; otherwise a fraction of a degree saturates activation.
    [Header("PD input scaling")]
    public float errorReferenceDegrees = 10f;
    public float speedReferenceDegPerSec = 200f;
    // Wider swing hip/knee proportional band on fall-catch walk: at the shared 10° with hipPGain 1.5
    // the ~40° air swing is a full-torque switch, hiding the solver setpoints and the D braking. Max
    // at use, so it only lowers |P·error|; 40° still saturates above 26.7°. ≤ shared = off.
    [Tooltip("Swing hip/knee error reference (deg) on fall-catch walk. <= errorReferenceDegrees = former shared band.")]
    public float walkSwingErrorReferenceDegrees = 40f;

    [Header("Joint PD")]
    // Quiet dual support: inside hipHoldBand hipP/hipD feed no angle (pelvisP/D hold the pelvis
    // vertical); past it MixHipHold adds hip P/D toward hipHoldAngle. Off on a walk split, which is
    // hip travel by design. The swing uses hipP/hipD after lift-off via the joint pose.
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
    // Neck and head target world vertical, not the angle to the parent. Own error reference (like
    // lumbar and pelvis): the shared 10° saturates the joint from fractions of a degree.
    public float neckTargetTilt = 0f;
    public float neckErrorReferenceDegrees = 25f;
    // Stable PD for neck and head; the flag allows comparing explicit PD on the same build.
    // Arms are always on SPD (see below).
    public bool useStablePd = true;
    [Header("Arms")]
    // Arm targets are joint poses, not world vertical (else a pendulum). Elbow flexion is minus
    // (−140…0); target slightly minus so gravity does not park it on the 0 stop. Always SPD: K·Δt/Ieff
    // ≈ 11.8 elbow, 5.1 wrist, 3.4 shoulder makes explicit PD unstable; useStablePd does not apply.
    public float shoulderPGain = 1.5f;
    public float shoulderDGain = 0.4f;
    public float shoulderBaseAngle = 0f;
    // Per-side shoulder base for the standing pose. NaN = follow shoulderBaseAngle;
    // -shoulderBaseL / -shoulderBaseR set an asymmetric pose.
    public float shoulderBaseL = float.NaN;
    public float shoulderBaseR = float.NaN;
    public float ShoulderBaseFor(int side)
    {
        float v = side == 0 ? shoulderBaseL : shoulderBaseR;
        return float.IsNaN(v) ? shoulderBaseAngle : v;
    }
    public float shoulderErrorReferenceDegrees = 25f;
    public float elbowPGain = 1.2f;
    public float elbowDGain = 0.4f;
    public float elbowBaseAngle = -20f;
    public float elbowErrorReferenceDegrees = 25f;
    public float wristPGain = 1.0f;
    public float wristDGain = 0.4f;
    public float wristBaseAngle = 0f;
    public float wristErrorReferenceDegrees = 25f;
    // Target offset from balanceSignal (CoM + tilt rate): CoM ahead -> shoulders plus (hand back),
    // elbow toward zero. Zero = static pose.
    public float armShoulderBalanceGain = 25f;
    public float armElbowBalanceGain = 10f;
    public float armWristBalanceGain = 0f;
    // Walk arm swing opposite the legs. Legacy Stance/Transfer uses sin(phaseAge / stanceDuration);
    // fall-catch cycles are too short for that clock, so TryFallCatchArmSwing drives the contra pose
    // from the FSM. Not CoM balance.
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
    // Contra-swing from the measured thigh split (hip jointAngle, same frame as the shoulder target)
    // instead of a phase clock: peaks at contralateral heel strike, keeps one sign across the step and
    // has no clock to reset at FSM edges. Full walkArmShoulderSwing at one swingHipFlex of split.
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
    // Like lumbar, error and speed come only from VestibularSystem. The hip joint lives on the thigh
    // with the pelvis as connectedBody, so a positive signal turns the pelvis CCW (increases
    // jointAngle). P and D are positive; a negative value inverts the loop.
    public float pelvisPGain = 2.0f;
    public float pelvisDGain = 0.3f;
    public float pelvisErrorReferenceDegrees = 25f;
    // Cold one-leg only, and only in true single support (exactly one foot down). Default 0.
    // Extra P during the lift saturates both hips as extensors so the swing cannot unload; extra D
    // can excite a pelvis limit cycle. Quiet stance and walk never add these.
    [Tooltip("Extra pelvis P on cold one-leg single support. 0 = quiet-stance P. Do not use during lift.")]
    public float oneLegPelvisPBoost = 0f;
    [Tooltip("Extra pelvis D on cold one-leg single support. 0 = quiet-stance D.")]
    public float oneLegPelvisDBoost = 0f;
    [Header("Lumbar")]
    // Reference is the chest's world tilt, not its angle to the pelvis, so a toppling pelvis does
    // not drag the chest down. Error and speed both come from VestibularSystem (world frame);
    // never mix in joint.jointSpeed, which is a different frame.
    public float lumbarTargetTilt = 0f;
    // Gains are positive. ControlLumbarToWorldUpright uses error = target − tilt and enters them
    // negated: signal = −P·e + D·ω (world angular speed, normalized).
    public float lumbarPGain = 2.0f;
    public float lumbarDGain = 0.3f;
    public float lumbarErrorReferenceDegrees = 25f;
    [Header("Ankle as pendulum")]
    // Ankle torque opposes the CoM offset (inverted pendulum), not the joint angle.
    // Overturning moment is m·g·d ≈ 687·d N·m (d = horizontal CoM offset in m).
    // P holds the CoM tight; D damps CoM speed after the startup squat.
    public float ankleComP = 14f;
    public float ankleComD = 8f;
    public float ankleLimitMargin = 15f;
    // Quiet dual stance: 1 = no plantarflex on a plate whose centre the CoM has passed (that is
    // heel-lift/push-off, not a restoring CoP); the anterior plate keeps the command to its toes.
    // 0 = both ankles copy the midpoint command. Walk modes never read this.
    [Tooltip("Quiet dual: 1 = no plantarflex on a plate CoM has passed. 0 = both ankles copy midpoint.")]
    public float quietAnklePerFoot = 1f;
    // Quiet dual ankle commands after the per-foot gate (diagnostic); 0 outside quiet dual stance.
    [Tooltip("Last quiet left ankle command after the per-foot gate.")]
    public float lastQuietAnkleL;
    [Tooltip("Last quiet right ankle command after the per-foot gate.")]
    public float lastQuietAnkleR;

    [Header("Toe MTP (not the ankle CoM loop)")]
    // Toe (MTP) hinge SPD. Quiet stance holds 0 (flat), airborne goes minus (toe up), capture walk
    // adds a plus push-off on the stance toe late in the step. Soft P/D lets the body roll over the
    // ball; the ankle CoM loop (ankleComP/D) is separate.
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

    // Paired inertias are computed lazily on the first FixedUpdate: in Awake Rigidbody2D.inertia
    // is not yet derived from the collider.
    private float neckEffectiveInertia;
    private float headEffectiveInertia;
    private float leftShoulderEffectiveInertia;
    private float rightShoulderEffectiveInertia;
    private float leftElbowEffectiveInertia;
    private float rightElbowEffectiveInertia;
    private float leftWristEffectiveInertia;
    private float rightWristEffectiveInertia;
    private float lastWalkStanceProgress;
    // Last Plant swing pose, written only while Plant actually commands a fold. Fall/Reach hold it
    // under walkPlantFallFoldHold so the PhaseAge reset does not unfold the leg.
    private float lastPlantSwingReachKnee;
    private float lastPlantSwingHipLift;
    private float lastPlantSwingAnkleLift;
    private float lastPlantSwingThighHold;
    // First-step swing-knee geo solve at standing height, latched once. The live solve follows a
    // sagging hip toward 120°, and Catch must not hold that tuck.
    private float lastFirstStepGeoKnee;
    // First-step Catch plant pose latch, held through Catch and Recover so leaving Catch does not
    // restore hipLift/catchKnee and lift the sole before the Recover swap. Cleared outside the
    // first cycle's Fall..Recover phases.
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
    // Seconds the current get-up attempt has had a sole on the ground. Cleared on lift-off so an
    // airborne tuck cannot re-arm.
    private float getUpContactAge;
    // Ages of the two sequenced get-up poses: the sit is held before the curl starts and the curl
    // for the tuck budget, so both poses are visible in the capture, not one tick of a gate.
    private float getUpSitAge;
    private float getUpTuckAge;
    // Get-up sequence latches: the sit must latch before the tuck.
    private bool getUpSitLatched;
    private bool getUpTuckLatched;
    private bool getUpPikeLatched; // alias of tuck latch for older call sites
    // Sit-up hip target (deg, plus = extension): raises the pelvis off a prone fold. The tuck uses
    // -getUpHipFlex (flexion) instead.
    private const float GetUpSitHipFlex = 35f;
    // Sit-up knee target (deg), near stance. GetUpTuckKneeMin is the knee fold that counts as tucked.
    private const float GetUpSitKneeFlex = 20f;
    private const float GetUpTuckKneeMin = 70f;
    // Sit hold before the curl may latch, so the sit is a visible pose of the sequence (three
    // frames of the 5 fps trial capture) rather than a single tick.
    private const float GetUpSitHoldSeconds = 0.6f;

    private VestibularSystem vestibularSystem;
    private CenterOfMassCalculator comCalculator;
    private BodyStateEstimator bodyState;
    private MotionIntent intent;
    private StepPhaseDriver stepPhaseDriver;
    // The driver is looked up once: a body without one would otherwise call GetComponent every
    // step. A driver added later must come through BindStepPhaseDriver / EnsureStepPhaseDriver.
    private bool stepPhaseDriverSearched;
    // Foot centre-to-toe reach for the push-off window (StanceToeReach), resolved once.
    private float stanceToeReachCached;
    private bool stanceToeReachResolved;
    // Thigh and shin lengths (m) for the landing/lift knee solves, read once from the hinge
    // anchors. Both legs come from the same CreateLeg call, so one pair covers both.
    private float legThighLength;
    private float legShinLength;
    private bool legLinkResolved;
    // Leg passive dampers, their built viscosity and the paired inertia that turns a time constant
    // into K. Resolved on first use, not in Awake: collider inertia and the friction components
    // are not in place yet.
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
    // Lumbar passive damper, cached like the legs; the get-up peel fades it so its built weld does
    // not lock the trunk.
    private JointFriction lumbarFrictionComp;
    private float lumbarFrictionBase;
    private float lumbarFrictionInertia;
    // True while any leg damper runs below its built viscosity; keeps the restore call running.
    // Quiet stance, crouch, pushes and cold one-leg never set it and never touch JointFriction.
    private bool legFrictionFaded;
    // walkSwingFrictionSnapHold latch: armed by the on-ground snap, held through the first
    // millimetres of air. Cleared on a swing-side change, once fade >= 0.7, or when the envelope
    // closes.
    private bool swingFrictionSnapLatched;
    // 0 = none, −1 left swing, +1 right swing. A role swap clears the latch so tau K does not stay
    // on the hip that just became stance.
    private int swingFrictionSnapHoldSide;
    // Geo-lift clock (s), counted once CoM is on the stance sole. Not PhaseAge: a late split would
    // then snap walkGeoLiftU straight to 1.
    private float walkFirstLiftHoldAge;
    // 0 = none, −1 right swing, +1 left swing. A role swap restarts the hold.
    private int walkFirstLiftStanceSign;
    private Rigidbody2D leftThighRb;
    private Rigidbody2D rightThighRb;
    // Foot bodies for the touchdown velocity match: the sole's own motion is what friction and the
    // heel strike see, so it is read from the body, not differenced from bodyState positions.
    private Rigidbody2D leftFootRb;
    private Rigidbody2D rightFootRb;
    private BoxCollider2D leftHeelBox;
    private BoxCollider2D leftToeBox;
    private BoxCollider2D rightHeelBox;
    private BoxCollider2D rightToeBox;
    // Set at the first swing lift-off: a foot scrape must not return the hip to the pelvis loop,
    // or the thigh extends and the leg stomps back down.
    private bool swingHipLatched;
    // Remaining flex hold (s) after a scrape (walkSwingAirHold).
    private float swingAirHoldRemain;
    // Continuous swing airborne time (s), compared with walkSwingLatchAir.
    private float swingAirStreak;
    // Last held support side (±1, 0 = none). A sign change resets standLegLevel and the swing latch.
    private float heldStandCmd;
    // Quiet one-leg release: seconds the swing has stayed airborne since intent.standLeg went to 0.
    // Caps the pose freeze so a stuck swing cannot hold the split forever.
    private float quietReleaseAirSeconds;
    // Swing hip angle on the latch frame. After lift-off the target is never weaker than this flex:
    // Min(angleAtLatch, −swingHipFlex); more negative = more flex.
    private float swingHipAngleAtLatch;
    // Previous step's swing-hip command and side (−1 left, +1 right, 0 none). The stance hip cancels
    // its pelvis reaction next step; one 200 Hz step of lag is below the muscle rate limit.
    private float lastSwingHipCommand;
    private int lastSwingHipSide;
    // Smoothed march pelvis bias (deg, plus = back). Not a walk field.
    private float marchPelvisBias;
    // Real swing air long enough for the march ankle cap. A scrape must not set this.
    private bool marchComHold;
    // March hip/knee pose is up, so toe/heel gates may run; earlier they would zero plantarflex
    // while the thigh is still peeling off.
    private bool marchToeHold;

    // Per-phase balance scaling for the fall-catch walk (useFallCatchWalk 0 leaves the defaults).
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
        // Fall/Reach: 0–1, fade the grounded swing ankle's toe grip and add a small dorsiflexor so
        // hipLift can raise the foot. 0 = keep the own-error / Reach propulsive loop.
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
        // Catch/Recover planted swing knee (deg). Above 0.5 it is held; otherwise
        // catchKnee × catchPlantKneeScale.
        public float plantKnee;
        // Post-swap Plant and Fall/Reach: knee flex (deg over kneeBaseAngle) held on the new stance
        // (the just-planted catch foot): the leftover Catch shin, then landDeg. 0 = quiet stance knee.
        public float stancePlantKnee;
        // Plant/Fall/Reach/Catch/Recover: CoM offset past fallCoMOffset is the gait, not a ragdoll;
        // RelaxAllMuscles there would drop the catch.
        public bool ignoreFalling;
        // Catch/Recover: drop the walk auto/base lean and the speed-drive ankle push; both pitch the
        // trunk forward and add +moveX on the stance ankle, the opposite of a landing brake.
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

    // Looked up once in Awake (HeadlessTrial adds the driver before BuildHuman). A driver added
    // after Awake must be bound with BindStepPhaseDriver or EnsureStepPhaseDriver.
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

    private HingeJoint2D GetJoint(string childName)
    {
        Transform t = transform.Find(childName);
        return t != null ? t.GetComponent<HingeJoint2D>() : null;
    }

    // Segment lengths changed after the build (Human foreshortening): drop the cached leg bars
    // and damper inertias so the next use re-measures them. Only that runtime path calls this.
    public void OnBodyGeometryChanged()
    {
        legLinkResolved = false;
        legFrictionResolved = false;
    }

    // Thigh/shin lengths (m) as hinge-anchor distances (hip.anchor to knee.connectedAnchor on the
    // thigh, knee.anchor to ankle.connectedAnchor on the shin). Lazy: the builder wires the
    // anchors after AddComponent, so Awake is too early.
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

    // ─── Geometric fall state ───
    // BodySupportGeometry level: 0 supported, 1 capture point outside support, 2 CoM outside,
    // 3 no support. Recomputed per read; h = CoM over the ankle hinges. Only TrialRecorder reads it.
    public int GeometricFallState
    {
        get
        {
            if (bodyState == null) return BodySupportGeometry.NoSupport;
            float footSum = 0f;
            int footCount = 0;
            if (leftAnkleJoint != null)
            {
                footSum += leftAnkleJoint.transform.TransformPoint(leftAnkleJoint.anchor).y;
                footCount++;
            }
            if (rightAnkleJoint != null)
            {
                footSum += rightAnkleJoint.transform.TransformPoint(rightAnkleJoint.anchor).y;
                footCount++;
            }
            float h = footCount > 0 ? bodyState.comPosition.y - footSum / footCount : 0f;
            bool hasSupport = bodyState.leftFootGrounded || bodyState.rightFootGrounded;
            return BodySupportGeometry.Level(hasSupport,
                bodyState.comPosition.x, bodyState.comVelocity.x, h,
                bodyState.supportMinX, bodyState.supportMaxX);
        }
    }

    // Down if grounded or within 2 × contactOffset of the ground (StepPhaseDriver's NearContact
    // band): GetContacts can be empty on a pitched toe while the solver still holds the sole.
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

    // Caches the leg and lumbar dampers, their built viscosity and paired inertia. Lazy like the
    // SPD inertias (not ready in Awake). Returns false until the hip and knee joints exist.
    private bool ResolveLegFrictionOnce()
    {
        if (legFrictionResolved) return true;
        if (leftHipJoint == null || rightHipJoint == null
            || leftKneeJoint == null || rightKneeJoint == null) return false;
        leftHipFriction = leftHipJoint.GetComponent<JointFriction>();
        rightHipFriction = rightHipJoint.GetComponent<JointFriction>();
        leftKneeFriction = leftKneeJoint.GetComponent<JointFriction>();
        rightKneeFriction = rightKneeJoint.GetComponent<JointFriction>();
        // The ankle damper lives on the foot (Human.CreateLeg). Outside the null guard so a body
        // without feet still gets its hips and knees faded.
        if (leftAnkleJoint != null)
            leftAnkleFriction = leftAnkleJoint.GetComponent<JointFriction>();
        if (rightAnkleJoint != null)
            rightAnkleFriction = rightAnkleJoint.GetComponent<JointFriction>();
        // Built damping, not Human.hipFriction: launchers scale that field before assembly, and the
        // restore must return exactly what this body was given.
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

    // Get-up: the built hip/knee/lumbar welds let quiet SPD saturate on a prone body without
    // moving it. Peel both legs and the lumbar to the swing tau envelope; restore on FinishGetUp.
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

    // fade 0 restores the built viscosity; fade 1 gives K = min(built, I/tau), never stiffer.
    // snap jumps straight to that value: while K > I/Δt stopTorque kills relative speed every step,
    // so a linear fade frees nothing. Touchdown keeps snap 0.
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

    // Forward CoM velocity for every walk capture-point term (filtered by walkComVelFilterTau).
    // Public so StepPhaseDriver judges phase and step target from the same signal the ankle uses.
    // With the filter off this is the raw estimator sample.
    public float WalkComVelocityX
    {
        get
        {
            if (walkComVelFilterTau <= 0.0001f || !hasWalkComVelSample)
                return bodyState != null ? bodyState.comVelocity.x : 0f;
            return walkComVelFiltered;
        }
    }

    // ─── Arm raise ───
    // Right arm: elbow bends, arm straightens to the up-forward diagonal, holds, lowers; repeats
    // per armRaiseReps/Period. Targets only; ArmDrive emits them while ArmRaiseActive. Off at 0.
    private void UpdateArmRaise()
    {
        ArmRaiseActive = false;
        bool on = intent != null && intent.armRaise > 0;
        if (!on)
        {
            armRaiseSteps = -1;
            armRaiseDoneLatched = false;
            return;
        }
        if (armRaiseSteps < 0)
        {
            armRaiseSteps = 0;
            armRaiseComMin = 0f;
            armRaiseComHoldEnd = 0f;
            ArmRaiseRepsDone = 0;
        }
        else
        {
            armRaiseSteps++;
        }
        if (armRaiseDoneLatched) return;
        float shoulderT = intent.armRaiseShoulder != 0f ? intent.armRaiseShoulder : -68f;
        float elbowT = intent.armRaiseElbow != 0f ? intent.armRaiseElbow : -6f;
        float bendT = intent.armRaiseBend != 0f ? intent.armRaiseBend : -50f;
        float hold = intent.armRaiseHold > 0f ? intent.armRaiseHold : 0.35f;
        int reps = intent.armRaiseReps > 0 ? intent.armRaiseReps : 1;
        float period = intent.armRaisePeriod >= 0.6f ? intent.armRaisePeriod : 3f;
        const float restShoulder = 0f;
        const float restElbow = -20f;      // the rig's own elbowBaseAngle (the hang)
        const float bendSeconds = 0.22f;   // elbow first: forearm up
        const float raiseSeconds = 0.33f;  // the straight diagonal
        const float lowerSeconds = 0.45f;  // back to the hang
        float cycle = bendSeconds + raiseSeconds + hold + lowerSeconds;
        float span = (reps - 1) * period + cycle;
        float age = armRaiseSteps * Time.fixedDeltaTime;
        if (age >= span)
        {
            armRaiseDoneLatched = true;
            return;
        }
        int rep = Mathf.FloorToInt(age / period);
        float local = age - rep * period;
        if (local >= cycle) return;                    // between raises: the hang
        if (local >= bendSeconds + raiseSeconds + hold)
            ArmRaiseRepsDone = Mathf.Max(ArmRaiseRepsDone, rep + 1);
        float sh;
        float el;
        if (local < bendSeconds)
        {
            float u = ArmRaiseSmooth(local / bendSeconds);
            sh = Mathf.Lerp(restShoulder, shoulderT * 0.35f, u);
            el = Mathf.Lerp(restElbow, bendT, u);
        }
        else if (local < bendSeconds + raiseSeconds)
        {
            float u = ArmRaiseSmooth((local - bendSeconds) / raiseSeconds);
            sh = Mathf.Lerp(shoulderT * 0.35f, shoulderT, u);
            el = Mathf.Lerp(bendT, elbowT, u);
        }
        else if (local < bendSeconds + raiseSeconds + hold)
        {
            sh = shoulderT;
            el = elbowT;
            armRaiseComHoldEnd = comCalculator != null ? comCalculator.GetCoMOffsetX() : 0f;
        }
        else
        {
            float u = ArmRaiseSmooth((local - bendSeconds - raiseSeconds - hold) / lowerSeconds);
            sh = Mathf.Lerp(shoulderT, restShoulder, u);
            el = Mathf.Lerp(elbowT, restElbow, u);
        }
        float comNow = comCalculator != null ? comCalculator.GetCoMOffsetX() : 0f;
        if (comNow < armRaiseComMin) armRaiseComMin = comNow;
        ArmRaiseShoulderTarget = sh;
        ArmRaiseElbowTarget = el;
        ArmRaiseActive = true;
    }

    private static float ArmRaiseSmooth(float u)
    {
        u = Mathf.Clamp01(u);
        return u * u * (3f - 2f * u);
    }

    // One-pole low-pass on the estimator's CoM velocity. alpha = dt/(tau+dt) is the implicit-step
    // solution of a first-order lag, so it cannot overshoot at any tau and needs no clamp.
    private void UpdateWalkComVelocityFilter()
    {
        float raw = bodyState != null ? bodyState.comVelocity.x : 0f;
        if (walkComVelFilterTau <= 0.0001f || !hasWalkComVelSample)
        {
            // Off, or first sample: track the raw signal, so switching the filter on mid-run starts from
            // the body's present speed.
            walkComVelFiltered = raw;
            hasWalkComVelSample = true;
            return;
        }
        float dt = Time.fixedDeltaTime;
        float alpha = dt / (walkComVelFilterTau + dt);
        walkComVelFiltered += (raw - walkComVelFiltered) * alpha;
    }

    // Level 2 pose drive (-poseDrive 1): while active the pose library is the single writer of
    // every joint. Resolved lazily because Human adds it after this component exists.
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
        // Squat task machine's copy of this step's CoM offset (the local is consumed later by
        // UpdateLocomotionState, after the crouch line).
        squatComOffset = comOffset;
        // Clear this step's leg-joint request record before any early return. Whatever no layer
        // writes is sealed by TrialRecorder (measured angle + named owner), never NaN.
        BeginLegRequestRecord();
        // Recorder only: names this step's leg writer (0 = level 1 walker, 1 = level 2 pose library).
        // No gain, limit or branch depends on it.
        PublishWalkPath(PoseDriveActive());
        // Before any early return (startup hold, Falling): a filter that skips steps
        // has an unknown time constant.
        UpdateWalkComVelocityFilter();
        // Landing transient: tick before any early-out so the airborne/contact state is never left
        // armed. All levels read 0 unless the rig has just been airborne.
        UpdateLandingTransient(comOffset);
        // Arm raise ticks before any early-out: its clock is its own step count, so a skipped step
        // would slip it.
        UpdateArmRaise();

        UpdateCrouchLevel();
        UpdateLeanLevel();
        UpdateStandLegLevel();
        // Not walking: capture the settled standing hip height (world Y from sensors) as the reference
        // for the walk-onset collapse test, instead of the thigh+shin kinematic fallback.
        if (stepPhaseDriver == null || !stepPhaseDriver.walkActive)
            TryCaptureStandHipReference();

        // Level 2 pose drive: one switch, one writer - every joint belongs to the pose library and
        // the pose tick names the state itself. The level-1 walker that ran without it was removed
        // (claude-015); a body without the pose drive is not driven.
        if (PoseDriveActive())
        {
            legReqLayer = TapOwnerPoseDrive;
            poseDrive.Tick(comOffset);
        }
    }

    // Crouch target is a step; the regulator sets the descent rate.
    private void UpdateCrouchLevel()
    {
        float desired = 0f;
        if (intent != null)
            desired = Mathf.Clamp01(intent.crouch);
        // Squat task: with a rep count on the intent the task machine owns the level target and
        // sequences the reps; with count 0 (every other path) the plain ramp below runs.
        float squatPace = 1f;
        float squatRelease = 1f;
        if (intent != null && intent.squatReps > 0)
            desired = TickSquatTask(out squatPace, out squatRelease);
        else if (squatTask.State != SquatState.Idle)
            squatTask.Disarm();
        float rate = desired < crouchLevel ? crouchReleaseRatePerSecond : crouchRatePerSecond;
        // The rate constants above are the ceiling; the squat load term scales them (1 when the task
        // is not armed).
        rate *= squatPace;
        squatPaceScale = squatPace;
        // Rise-only term on the release side; 1 unless the task is armed and rising.
        if (desired < crouchLevel) rate *= squatRelease;
        squatReleaseScaleUsed = squatRelease;
        crouchLevel = Mathf.MoveTowards(
            crouchLevel, desired, Mathf.Max(0f, rate) * Time.fixedDeltaTime);
        // Vertical force channel: crouchLevel is the requested pose (arms, hand support read it);
        // crouchLoadLevel is that pose throttled by the measured sole load, and only the leg/trunk
        // targets read it. Both are 0 on paths that never crouch.
        UpdateCrouchLoadMeter();
        crouchLoadLevel = CrouchLoadShapedLevel(crouchLevel);
    }

    // One step of the squat task machine. It owns the level TARGET; UpdateCrouchLevel's ramp
    // moves it. The load meter refreshes later this step, so load and level are one step old.
    // Also returns the pace/release rate scales; intent doses of 0 use the controller's values.
    private float TickSquatTask(out float paceScale, out float releaseScale)
    {
        SquatStateMachine.Tuning t = default;
        t.reps = intent.squatReps;
        t.dt = Time.fixedDeltaTime;
        t.kneeBaseDeg = kneeBaseAngle;
        t.depthFlexDeg = squatDepthFlexDeg;
        t.returnBandDeg = squatReturnBandDeg;
        t.comSettleM = squatComSettleM;
        t.comRepBoundM = squatComRepBoundM;
        t.tiltBoundDeg = squatTiltBoundDeg;
        t.loadFloor = crouchLoadFoldFloor;
        t.loadCeiling = crouchLoadPushCeiling;
        t.loadDipFloor = squatLoadDipFloor;
        t.loadDipMaxSeconds = squatLoadDipSeconds;
        t.standConfirmSeconds = squatStandConfirmSeconds;
        t.bottomHoldSeconds = intent.squatBottomHold > 0f
            ? intent.squatBottomHold : squatBottomHoldSeconds;
        t.phaseWatchdogSeconds = squatPhaseWatchdogSeconds;
        t.paceFloorScale = squatPaceFloorScale;
        t.paceStallSeconds = squatPaceStallSeconds;
        t.paceDriveFloor = squatPaceDriveFloor;
        // Rise term: intent 0 = plain clock (scale 1). The load gate reuses the fold law's floor, the
        // same band the leg load channel throttles with.
        t.releaseScale = intent.squatReleaseScale > 0f ? intent.squatReleaseScale : 1f;
        t.releaseLoadFloor = intent.squatReleaseLoad > 0 ? crouchLoadFoldFloor : 0f;
        // Rise guards (0 = off): a floor the rise never stops below, and a sink bypass (a body falling
        // onto the leg takes the plain clock).
        t.releaseSinkGuard = intent.squatReleaseSink > 0f ? intent.squatReleaseSink : 0f;
        t.releaseFloor = intent.squatReleaseFloor > 0f ? intent.squatReleaseFloor : 0f;
        // Bottom gate: armed by the intent (0 = plain dwell timer); settle time and speed fall back to
        // the controller's own values.
        t.bottomGate = intent.squatBottomGate > 0 ? 1f : 0f;
        t.bottomSettleSeconds = intent.squatBottomSettle > 0f
            ? intent.squatBottomSettle : squatBottomSettleSeconds;
        t.bottomSettleMps = intent.squatBottomMps > 0f
            ? intent.squatBottomMps : squatBottomSettleMps;
        // Rise CoM admittance: the intent arms the band (0 = off); scale falls back to 0 (a full
        // hold) and the max hold to squatRiseComHoldMaxSeconds.
        t.comBand = intent.squatRiseCom > 0f ? intent.squatRiseCom : 0f;
        t.comScale = intent.squatRiseComScale > 0f ? Mathf.Clamp01(intent.squatRiseComScale) : 0f;
        t.comHoldMax = intent.squatRiseComMax > 0f
            ? intent.squatRiseComMax : squatRiseComHoldMaxSeconds;
        squatReleaseKnob = t.releaseScale;
        squatBottomHoldUsed = t.bottomHoldSeconds;

        SquatStateMachine.Sensed s = default;
        float kneeLeft = bodyState != null ? bodyState.leftKneeAngle : 0f;
        float kneeRight = bodyState != null ? bodyState.rightKneeAngle : 0f;
        s.kneeDeg = Mathf.Max(kneeLeft, kneeRight);
        s.loadRatio = CrouchLoadRatio();
        s.comOffsetM = squatComOffset;
        s.tiltDeg = vestibularSystem != null ? vestibularSystem.GetBodyTilt() : 0f;
        s.bothFeetDown = bodyState != null
            && bodyState.leftFootGrounded && bodyState.rightFootGrounded;
        // Quiet means no fall sequence; a squat is not a walk, so Walking does not block the task.
        s.locomotionQuiet = locomotionState != LocomotionState.Falling
            && locomotionState != LocomotionState.Fallen
            && locomotionState != LocomotionState.GettingUp
            && !(poseDrive != null && poseDrive.ClStanceCloseBusy);
        s.level = crouchLevel;
        // Pelvis vertical velocity: the rise gate reads it, so a freeze never holds a body already
        // coming down onto the leg.
        s.pelvisVelY = vestibularSystem != null
            ? vestibularSystem.GetPelvisVerticalVelocity() : 0f;

        SquatStateMachine.Decision d = squatTask.Decide(t, s);
        squatTask.Apply(d);
        // The load term is a probe: intent.squatPace 0 leaves the scale at 1 (plain clock), so one
        // build runs the dose and its own control. Nothing else in this step reads the flag.
        paceScale = intent.squatPace > 0 ? d.paceScale : 1f;
        // Rise scale: the machine returns 1 in every state but the ascent, so no flag is needed.
        releaseScale = d.releaseScale;
        return d.levelTarget;
    }

    // Vertical load meter: one filtered sample of sole load per step. Read-only (no gate or joint
    // command), skipped while the crouch channel is asleep.
    private void UpdateCrouchLoadMeter()
    {
        bool awake = crouchLevel > 0.001f || crouchLoadLevel > 0.001f;
        if (!awake)
        {
            // No sample: reset to the sentinel so a later crouch starts from its own first reading.
            crouchLoadFilteredN = -1f;
            return;
        }
        if (!crouchLoadFilterReady)
        {
            crouchLoadFilter = new ContactFilter2D();
            crouchLoadFilter.SetLayerMask(GroundLayers.Mask);
            crouchLoadFilter.useTriggers = false;
            crouchLoadFilterReady = true;
        }
        if (crouchLoadContacts == null)
            crouchLoadContacts = new ContactPoint2D[12];

        float dt = Time.fixedDeltaTime;
        if (dt <= 0f) dt = 0.005f;
        // Same quantity TrialRecorder publishes as fzL/fzR: solver contact impulse along the normal,
        // upward part only, divided by dt.
        float raw = SoleLoadN(leftHeelBox, dt) + SoleLoadN(leftToeBox, dt)
            + SoleLoadN(rightHeelBox, dt) + SoleLoadN(rightToeBox, dt);
        if (crouchLoadFilteredN < 0f)
            crouchLoadFilteredN = raw;
        else
        {
            float tau = Mathf.Max(0f, crouchLoadFilterTau);
            float alpha = dt / (tau + dt);
            crouchLoadFilteredN += (raw - crouchLoadFilteredN) * alpha;
        }
    }

    private float SoleLoadN(Collider2D col, float dt)
    {
        if (col == null) return 0f;
        int n = col.GetContacts(crouchLoadFilter, crouchLoadContacts);
        int used = Mathf.Min(n, crouchLoadContacts.Length);
        float acc = 0f;
        for (int i = 0; i < used; i++)
        {
            float vertical = crouchLoadContacts[i].normalImpulse
                * crouchLoadContacts[i].normal.y;
            if (vertical > 0f) acc += vertical / dt;
        }
        return acc;
    }

    // Sole load as a fraction of the rig's weight. Negative = not available (no sample yet, or no
    // weight resolved); the caller then runs the pose on its plain clock.
    private float CrouchLoadRatio()
    {
        float weight = RigWeightN();
        if (weight <= 0.01f || crouchLoadFilteredN < 0f) return -1f;
        return crouchLoadFilteredN / weight;
    }

    // Sum of the rig's rigidbody masses × g, in N. Resolved once; read only by the crouch channel.
    private float RigWeightN()
    {
        if (rigWeightN > 0.01f) return rigWeightN;
        Rigidbody2D[] bodies = GetComponentsInChildren<Rigidbody2D>();
        float mass = 0f;
        for (int i = 0; i < bodies.Length; i++)
            if (bodies[i] != null) mass += bodies[i].mass;
        rigWeightN = mass * Physics2D.gravity.magnitude;
        return rigWeightN;
    }

    // Load-shaped level: fold only while the soles carry the body, unfold only while the reaction
    // stays under crouchLoadPushCeiling; each rate falls to 0 at its band edge and the position
    // servo brakes. Deeper fold = longer knee moment arm, so load says how much fold the leg holds.
    private float CrouchLoadShapedLevel(float target)
    {
        float level = crouchLoadLevel;
        float ratio = CrouchLoadRatio();
        if (ratio < 0f)
        {
            // Meter not available: plain pose clock.
            float plainRate = target < level
                ? crouchReleaseRatePerSecond
                : crouchRatePerSecond;
            return Mathf.MoveTowards(
                level, target, Mathf.Max(0f, plainRate) * Time.fixedDeltaTime);
        }
        if (target > level)
        {
            float gate = Mathf.Clamp01(
                (ratio - crouchLoadFoldFloor)
                / Mathf.Max(0.01f, 1f - crouchLoadFoldFloor));
            level = Mathf.MoveTowards(level, target,
                Mathf.Max(0f, crouchRatePerSecond) * gate * Time.fixedDeltaTime);
        }
        else if (target < level)
        {
            float gate = Mathf.Clamp01(
                (crouchLoadPushCeiling - ratio)
                / Mathf.Max(0.01f, crouchLoadPushCeiling - 1f));
            level = Mathf.MoveTowards(level, target,
                Mathf.Max(0f, crouchReleaseRatePerSecond) * gate * Time.fixedDeltaTime);
        }
        return level;
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

        // Quiet release only (walk keeps walkEndHoldSupport): hold the one-leg level while the lifted
        // foot is still airborne, else the knee unwinds before plant and the old stance unloads.
        // Then the usual decay; 1 s cap so a stuck swing cannot hold the split forever.
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

    // ─── leg request recorder ───
    // Recorder-only, never read by control: per leg joint, this step's angle request, raw demand
    // (muscle-ceiling units) and owner. Unwritten joints get the measured angle (reqFallbackMask).
    public const int LegJointHipL = 0;
    public const int LegJointHipR = 1;
    public const int LegJointKneeL = 2;
    public const int LegJointKneeR = 3;
    public const int LegJointAnkleL = 4;
    public const int LegJointAnkleR = 5;
    public const int LegJointCount = 6;

    private readonly float[] legReqDeg = new float[LegJointCount];
    private readonly int[] legReqOwner = new int[LegJointCount];
    private readonly float[] legReqDemand = new float[LegJointCount];
    private int legReqLayer;

    // Layer owning the legs this step (a TapOwner* code), stamped by FixedUpdate before each
    // branch; a drive method may pass a more precise code.
    public int LegRequestLayer => legReqLayer;
    public float LegRequestDeg(int i) => legReqDeg[i];
    public int LegRequestOwner(int i) => legReqOwner[i];
    public float LegRequestDemand(int i) => legReqDemand[i];

    // Side/kind of a leg joint from the cached hinge references. -1 = not one of the six leg
    // joints the walk tap records.
    public int LegJointIndex(HingeJoint2D joint)
    {
        if (joint == null) return -1;
        if (joint == leftHipJoint) return LegJointHipL;
        if (joint == rightHipJoint) return LegJointHipR;
        if (joint == leftKneeJoint) return LegJointKneeL;
        if (joint == rightKneeJoint) return LegJointKneeR;
        if (joint == leftAnkleJoint) return LegJointAnkleL;
        if (joint == rightAnkleJoint) return LegJointAnkleR;
        return -1;
    }

    public void BeginLegRequestRecord()
    {
        legReqLayer = TapOwnerStanceLayer;
        for (int i = 0; i < LegJointCount; i++)
        {
            legReqDeg[i] = float.NaN;
            legReqOwner[i] = TapOwnerNone;
            legReqDemand[i] = float.NaN;
        }
    }

    public void NoteLegAngle(int index, float targetDeg, float demand, int owner)
    {
        if (index < 0 || index >= LegJointCount) return;
        legReqDeg[index] = targetDeg;
        legReqDemand[index] = demand;
        legReqOwner[index] = owner != TapOwnerNone ? owner : legReqLayer;
    }

    // intent.march is the stand march clock, not walkActive; WalkLift stays off.
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

    // ─── landing transient ───
    // Airborne prepare + bounded contact yield, scaled by the measured arrival speed. Ticked every
    // physics step before any early-out, so a step that returns early cannot leave it armed.
    private float landingPrepLevel;
    private float landingPrepAirTime;
    private float landingYieldLevel;
    private float landingFlatWeight;
    private float landingImpactSpeed;
    private float landingYieldAge = -1f;
    private bool landingArmed;
    private float landingPrevComY;
    private bool landingPrevComValid;
    private int landingContactCount;
    private float landingMaxYieldLevel;
    private float landingMaxImpactSpeed;
    private bool landingImpactRejectLogged;
    // Landing catch state.
    private float landingCatchAge;
    private float landingCatchHoldTime;
    private float landingCatchBrakeDeg;
    private float landingCatchToeDeg;
    private float landingMaxCatchBrake;
    private int landingCatchFrames;
    private int landingLiveFrames;

    public float LandingMaxYieldLevel => landingMaxYieldLevel;
    public float LandingMaxImpactSpeed => landingMaxImpactSpeed;
    public int LandingContactCount => landingContactCount;
    public float LandingMaxCatchBrake => landingMaxCatchBrake;
    public int LandingCatchFrames => landingCatchFrames;
    public int LandingLiveFrames => landingLiveFrames;

    private void UpdateLandingTransient(float comOffset)
    {
        float dt = Mathf.Max(1e-4f, Time.fixedDeltaTime);
        // Quiet path only: the landing response belongs to the stand core, so an airborne step of the
        // walk or march must not arm a prepare or a fold.
        if (!landingTransient || !QuietVestibularPath() || bodyState == null || comCalculator == null)
        {
            ClearLandingTransient();
            return;
        }
        Vector2 com = comCalculator.GetCenterOfMass();
        float descent = landingPrevComValid ? (landingPrevComY - com.y) / dt : 0f;
        landingPrevComY = com.y;
        landingPrevComValid = true;

        bool inAir = !FootSupportDown(true) && !FootSupportDown(false);
        float ramp = Mathf.Max(0.01f, landingPrepRamp);
        if (inAir)
        {
            landingPrepAirTime += dt;
            // Prepare: only a real descent presets the legs, and only after enough air time to be a fall,
            // not the spawn drop-in. A fresh descent clears the arrival speed so it belongs to this flight.
            bool realFlight = landingPrepAirTime >= Mathf.Max(0f, landingPrepAirMin);
            if (landingPrepLevel <= 0.001f || !realFlight) landingImpactSpeed = 0f;
            landingPrepLevel = (realFlight && descent > Mathf.Max(0.05f, landingPrepDescent))
                ? Mathf.MoveTowards(landingPrepLevel, 1f, dt / ramp)
                : Mathf.MoveTowards(landingPrepLevel, 0f, dt / ramp);
            if (landingPrepLevel > 0.001f)
            {
                landingArmed = true;
                // The last airborne descent is the arrival speed (the contact step already absorbs part of
                // it). Samples above landingImpactCap are solver penetration correction, not an arrival.
                if (descent <= Mathf.Max(1f, landingImpactCap))
                {
                    if (descent > landingImpactSpeed) landingImpactSpeed = descent;
                }
                else if (!landingImpactRejectLogged)
                {
                    landingImpactRejectLogged = true;
                    Debug.LogWarning("LANDING_TRANSIENT rejected arrival sample: descent="
                        + descent.ToString("0.###") + " m/s at t="
                        + Time.fixedTime.ToString("0.###")
                        + " (solver correction; cap " + landingImpactCap.ToString("0.#") + ")");
                }
            }
            landingYieldAge = -1f;
            landingYieldLevel = 0f;
        }
        else
        {
            landingPrepAirTime = 0f;
            if (landingArmed)
            {
                // First support step after an armed descent: start the bounded yield. The prepare releases
                // over the same ramp.
                landingArmed = false;
                landingYieldAge = 0f;
                landingContactCount++;
                landingCatchAge = 0f;
                landingCatchHoldTime = 0f;
                // One log line per arrival: what was measured and what the yield folds against. Debug only.
                Debug.Log("LANDING_CONTACT #" + landingContactCount
                    + " t=" + Time.fixedTime.ToString("0.###")
                    + " impact=" + landingImpactSpeed.ToString("0.###")
                    + " m/s prep=" + landingPrepLevel.ToString("0.###")
                    + " yieldKnee=" + landingYieldLevel.ToString("0.###"));
            }
            landingPrepLevel = Mathf.MoveTowards(landingPrepLevel, 0f, dt / ramp);
            if (landingYieldAge >= 0f)
            {
                landingYieldAge += dt;
                float window = Mathf.Max(0.05f, landingYieldTime);
                float u = landingYieldAge / window;
                if (u >= 1f)
                {
                    landingYieldAge = -1f;
                    landingYieldLevel = 0f;
                }
                else
                {
                    float rise = Mathf.Clamp(landingYieldRiseFrac, 0.01f, 0.9f);
                    float env = u < rise ? u / rise : (1f - u) / (1f - rise);
                    float impactN = Mathf.Clamp01(
                        landingImpactSpeed / Mathf.Max(0.5f, landingYieldImpactRef));
                    landingYieldLevel = Mathf.Clamp01(env) * impactN;
                }
            }
        }
        // The flat-plate term follows the prepare level, i.e. airborne only: on the ground a hold-flat
        // torque fights the CoM loop and walks the body out of its support.
        landingFlatWeight = landingPrepLevel;
        // Landing catch: for up to landingCatchMaxTime after arrival, a predicted CoM lead past the
        // band asks for a bounded backward pelvis lean and toe press. Read from measured CoM and its
        // forward speed, so it is a balance action; zero unless a landing is live on the quiet path.
        float comVelXNow = bodyState != null ? bodyState.comVelocity.x : 0f;
        bool catchLive = landingContactCount > 0
            && QuietVestibularPath()
            && landingCatchAge < Mathf.Max(0.5f, landingCatchMaxTime);
        if (!inAir && catchLive)
        {
            landingCatchAge += dt;
            float band = Mathf.Max(0f, landingCatchBand);
            float lead = comOffset + Mathf.Max(0f, comVelXNow) * Mathf.Max(0f, landingCatchLeadTime);
            landingCatchBrakeDeg = Mathf.Clamp(
                Mathf.Max(0f, lead - band) * Mathf.Max(0f, landingCatchHipGain),
                0f, Mathf.Max(0f, landingCatchHipMax));
            landingCatchToeDeg = Mathf.Clamp(
                Mathf.Max(0f, lead - band) * Mathf.Max(0f, landingCatchToeGain),
                0f, Mathf.Max(0f, landingCatchToeMax));
            if (Mathf.Abs(lead) <= band) landingCatchHoldTime += dt;
            else landingCatchHoldTime = 0f;
        }
        else
        {
            landingCatchBrakeDeg = 0f;
            landingCatchToeDeg = 0f;
        }
        if (landingCatchBrakeDeg > 0f) landingCatchFrames++;
        if (landingCatchBrakeDeg > landingMaxCatchBrake) landingMaxCatchBrake = landingCatchBrakeDeg;
        landingLiveFrames++;
        if (landingYieldLevel > landingMaxYieldLevel) landingMaxYieldLevel = landingYieldLevel;
        if (landingImpactSpeed > landingMaxImpactSpeed) landingMaxImpactSpeed = landingImpactSpeed;
    }

    private void ClearLandingTransient()
    {
        landingPrepLevel = 0f;
        landingPrepAirTime = 0f;
        landingYieldLevel = 0f;
        landingFlatWeight = 0f;
        landingYieldAge = -1f;
        landingArmed = false;
        landingImpactSpeed = 0f;
        landingPrevComValid = false;
        landingPrevComY = 0f;
        landingCatchAge = 0f;
        landingCatchHoldTime = 0f;
        landingCatchBrakeDeg = 0f;
        landingCatchToeDeg = 0f;
    }

    // ControlJointToAngleStable: stable PD, P*error − D*speed over 1+K·Δt/I, always (elbow and wrist
    // limit-cycle without it). Returns the applied signal so a caller can cancel its third-law
    // reaction elsewhere (swing hip -> stance hip); ignoring it is fine.

    // Paired joint inertia 1/(1/I₁ + 1/I₂): both bodies rotate toward each other. One segment's
    // inertia understates SPD stiffness (head on the light neck: by 3×).
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

    // Called when the standing band opens (TiltSettled + CoM-to-stance). After a heap this gate,
    // not the −20° handoff, marks get-up success.
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

}