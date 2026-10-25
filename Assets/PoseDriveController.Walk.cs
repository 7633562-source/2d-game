using System.Globalization;
using UnityEngine;

// PoseDriveController: the walk (step-up reflex, settle step, step clock, TickWalk, leg IK).
public partial class PoseDriveController
{
    // ─── walk reflex ───
    // Set only by WalkReflex; 0 = inert. ReflexFloorOffsetM: local floor height above the datum, m,
    // added to ground heights and swing clearance so a step lands on a rise. Lift, stumble: TickWalk.
    public float ReflexFloorOffsetM;
    public float ReflexLiftBoostM;
    public float ReflexStumbleVelMps;
    public float ReflexStumbleHoldS = 0.10f;
    public int ReflexStumbleSteps;      // recorder: recovery steps fired
    // Placement x the law asks this step; read only by WalkReflex's diagnostic line.
    public float ReflexPlaceX;
    // Datum every height in this file is measured from; equals groundY while ReflexFloorOffsetM is 0.
    private float FloorDatumM => groundY + ReflexFloorOffsetM;
    private float reflexStumbleHoldT;
    private bool reflexStumbleLatch;
    // Read only by WalkReflex's floor probe.
    public bool Active => active;
    public float GroundYDatum => groundY;
    public float ReflexEyeX => pelvisBody != null ? pelvisBody.position.x : transform.position.x;
    // The walk's leg state, mirrored each TickWalk frame for WalkReflex's datum gate and diagnostics;
    // nothing in this file reads them.
    public bool ReflexLeftDown, ReflexRightDown;
    public float ReflexLeftFootY, ReflexRightFootY;
    public float ReflexLeftFootX, ReflexRightFootX;
    public bool ReflexBothSolesDown => ReflexLeftDown && ReflexRightDown;
    // While true the stance ankle's push flick (pushDeliverP) is not amplified: a toe standing at a
    // riser would roll against the wall. Set by WalkReflex only; false = normal flick.
    public bool ReflexHoldPush;
    // Per-leg step-up factor (0 = off), set by WalkReflex while that sole stands on a rise: scales the
    // leg's support drive in DriveOneLeg (knee, and the ankle push via the shared kneeP).
    // ReflexStepUpHipShare also applies the factor to hip extension (0 = hip unboosted).
    public float ReflexStepUpL, ReflexStepUpR;
    public float ReflexStepUpHipShare;
    // Share of the step-up boost given to ankle stiffness: 1 = the boosted kneeP also drives the
    // plantar roll; 0 = knee only, a compliant toe that cannot launch the body past its support.
    public float ReflexStepUpAnkle = 1f;

    // Step clock advances at 1/ReflexCadenceScale (1 = normal cadence); set by WalkReflex only.
    public float ReflexCadenceScale = 1f;
    // Set by WalkReflex for pace recovery. ReflexBandScale scales the step band's ceiling (1 = inert).
    // ReflexCatchShare: share of the pendulum constant in the catch point; 0.5 = default (a lead the
    // walk can take back), 1.0 = the arrest point (a braking catch that sheds surplus speed).
    public float ReflexBandScale = 1f;
    public float ReflexCatchShare = 0.5f;

    // ─── release ramp (-clReleaseRamp s, the stop's coast-down) ───
    // Game-side (UpdateReleaseRamp): on the walk key's release edge, while the gait was walking, the
    // walk command every layer reads decays 1 -> ClReleaseRampFloor over s seconds, then drops to 0.
    // -clReleaseRampKeep k holds the span's end at the floor k more seconds before that drop.
    // -clReleaseRampFade f (0 = off) spreads that drop instead: the floor decays to 0 over f more
    // seconds, so the command never leaves the floor in one tick.
    // The span is the harness brake's own shape, so -assistBrake's speed governor acts on the ramp
    // (its band is 0.05 < |moveX| < 0.95) and the catch keeps its writer. A key pressed again inside
    // the ramp returns the command to 1 at once - the key never left, no stop, no entry gate.
    // -clReleaseRampCatch c: while the ramp runs the step placement's catch share rises 0.5 -> c on
    // the ramp's clock (the harness brake's law at -walkPlanBrakeCatch) and is restored to 0.5 when
    // the ramp ends or is cancelled. 0 = off.
    private float clReleaseRamp;                       // -clReleaseRamp: seconds, 0 = off
    private const float ClReleaseRampFloor = 0.2f;     // the ramp's end value before the drop to 0
    private float clReleaseRampT = -1f;                // the ramp's clock, s; < 0 = not ramping
    private float clReleaseRampDir = 1f;               // the direction the released command ramps down
    private float clReleaseRampPrev;                   // the command the previous tick saw (the edge)
    private float clReleaseRampV;                      // the value this ramp last wrote (its own echo)
    private float clReleaseRampCatch;                  // -clReleaseRampCatch: catch share at the ramp's end, 0 = off
    private bool clReleaseRampCatchWrote;              // this ramp raised ReflexCatchShare and owes the restore
    private float clReleaseRampKeep;                   // -clReleaseRampKeep: seconds held at the floor past the span, 0 = off
    private float clReleaseRampKeepT = -1f;            // the keep's clock, s; < 0 = not keeping
    private float clReleaseRampFade;                   // -clReleaseRampFade: seconds of floor->0 decay past the span, 0 = off
    private float clReleaseRampFadeT = -1f;            // the fade's clock, s; < 0 = not fading
    private float clReleaseRampShape;                  // -clReleaseRampShape: the span decay's exponent, 0 = off (linear)
    private float clReleaseRampClose;                  // -clReleaseRampClose: the post-coast stand's close bar, m, 0 = off
    private bool clReleaseRampPost;                    // the stand right after a coast (cleared by the next walk)
    private float clReleaseRampPress;                  // -clReleaseRampPress: the press-end bar on the coast's command, 0 = off
    private float clReleaseRampPressFall;              // -clReleaseRampPressFall: seconds of press->floor glide before the end, 0 = off
    private float clReleaseRampFallT = -1f;            // the glide's clock, s; < 0 = not gliding
    private float clReleaseRampFallV0;                 // the command the press caught (the glide's start)
    private bool clReleaseRampPressSpan;               // -clReleaseRampPressSpan: the caught press rides the span, false = off
    private bool clReleaseRampPressRide;               // a press is riding the span to its end
    // ─── phase release (-clReleasePhase 1, default off; req-117): see ClReleasePhaseKey ───
    private bool clReleasePhaseOn;
    private bool clReleasePhasePend;
    private bool clReleasePhasePrev;      // the previous evaluation's return
    private float clReleasePhaseD, clReleasePhaseW, clReleasePhaseMax;
    private float clReleasePhaseLandU = 1f;   // -clReleasePhaseLandU: the plant hook's own bar; 1 = today's 0.99995
    private float clReleasePhaseT;        // the pend's tick, s
    private float clReleasePhaseR = -1f;  // this walk's last right plant, s; < 0 = none
    private float clReleasePhaseRn = -1f; // the first right plant after the pend; < 0 = none
    // -clReleasePhaseSide: 0 right only (default), 1 either plant, 2 alternate per stop. The stop's
    // own side is picked when its hold starts (clReleaseSideStop) and the alternator flips after it.
    private int clReleasePhaseSide;
    private int clReleaseSideStop;
    private int clReleaseSideAlt;
    // -clReleasePhaseHold (req-144 / req-147, default 0 = off): a key-up the phase release would pass
    // while the walk runs is held like a pend and released exactly like one - at the walk's next RIGHT
    // plant (+ D), the walk's end, the cap or another action. 2 = only when this walk has no accepted
    // right plant yet (R < 0); 1 = req-144 round 5's arm (R < 0, or a foot in the air, or the walk
    // <= 2.6 s old).
    private int clReleasePhaseHold;
    private float clTap2WalkT;            // the live walk's start, s (value 1's short-walk term)

    // ─── settle step ───
    // On walk-key release take one more step, placed at the arrest point, and end on its plant with
    // both feet down, so the stand never inherits a moving body. -poseWalkSettle S: budget s, 0 = off.
    private float walkSettleBudget;
    private float walkSettleMps = 0.10f;
    private float walkSettleCatch = 1.0f;
    private float walkSettleLeadM;
    private bool walkSettleLatch;      // the settle owns the walk
    private float walkSettleT;         // seconds under the latch
    private int walkSettleSwaps;       // swaps fired under the latch
    private bool walkSettleArmed;      // a swap fired post-latch: the plan is arrest
    private bool walkSettleWasWalk;    // the walk ran last tick (the drop's own edge)
    private bool walkSettleSpent;      // the one capture swing is spent (c115b)
    private float walkSettleThreatM;   // threat re-arm bar, m (-poseWalkSettleThreat)
    // While settling: walkSettlePush scales the push flick's amplification (0 = none: the arrest step
    // sheds speed); walkSettleBendDeg floors the stance knee (0 = off) so the load absorbs, not vaults.
    // walkSettleHandOn: the stand starts from the walk-end pelvis height and blends back over BlendS. CYCLE 169 landed: on; -poseWalkSettleHand 0 = off
    private float walkSettlePush = 0f;    // CYCLE 123 landed: 0 (the arrest sheds the push; -poseWalkSettlePush 1 restores)
    private float walkSettleBendDeg = 24f;    // CYCLE 123 landed: 24 (the arrest knee floor); -poseWalkSettleBend 0 = off
    // Load-driven knee give while settling: sole load above the carry bar (body weights) adds
    // walkSettleBendLoad deg per body weight to the stance-knee floor, capped at walkSettleBendCap.
    private float walkSettleBendLoad;       // deg per body-weight over the bar (0 = off)
    private float walkSettleBendBar = 0.7f; // the carry bar, body weights
    private float walkSettleBendCap = 45f;  // deg ceiling on the give
    // Handoff hold: after release the stand's stance knees stay bent >= walkSettleHoldDeg (0 = off)
    // until |comVelX| stays under the bar for the quiet span, a key press, a fall, or the safety cap.
    private float walkSettleHoldDeg;           // deg floor through the handoff (0 = off)
    private float walkSettleHoldMps = 0.15f;   // the quiet bar, m/s
    private float walkSettleHoldQuiet = 0.25f; // quiet span to end the hold, s
    private float walkSettleHoldMax = 2.0f;    // the safety cap, s
    private bool walkSettleHoldLive;           // the hold owns the stance knees
    private float walkSettleHoldT;             // the hold's own clock, s
    private float walkSettleHoldQT;            // the quiet span's own clock, s
    // State exit: an armed settle also releases once both soles are down, |comVelX| <= the move bar
    // and support margin >= its floor for the quiet span - a single plant frame can be missed.
    private float walkSettleExitMps = 0.35f;    // the move bar, m/s (0 = off); CYCLE 122 landed 0.35
    private float walkSettleExitQuiet = 0.15f;  // the held span that concludes, s
    private float walkSettleExitMargin = 0.05f; // the support floor, m
    private float walkSettleExitQT;             // the held span's own clock, s
    // Walk-on guard: the release arms BalanceController.settleReleaseGuardT, so FallReflex starts no
    // new swing until the body reads quiet. Window in s, 0 = off.
    private float walkSettleGuardWin = 4.0f;    // the guard window, s (0 = off); CYCLE 121 landed 4.0
    private bool walkSettleHandOn;
    private float walkSettleBlendS = 0.35f;
    private float walkSettleHandT;         // the capture window's clock
    private float walkSettleHandPelvisY;   // the walk-end pelvis height
    // Trunk lean WalkReflex adds to the torso target at high pace, deg (+ = trunk back, 0 = off):
    // surplus speed pitches the trunk forward faster than the fixed tilt target can hold it.
    public float ReflexTorsoLeanDeg;

    // ─── step clock ───
    private int swingSign = 1;          // +1 = the left leg is the swing leg
    private float stepClock;
    private float stanceFootX;
    private float swingFootX0;
    private float phaseForLog;
    // CoM x at the last swap (NaN before the first step). The swing descent's `need` is measured from
    // it, not from the stance foot, which after a placement sits ahead of the CoM and is never reached.
    private float comXSwap = float.NaN;

    // Hip height above the floor at the swap that started this step (NaN before the first frame).
    // Constant through the step, so the height loop's error is the step's own sink; a live height
    // would make that error identically zero.
    private float hipHeightStepDatumM = float.NaN;

    // True once the swing foot has left the ground since its swap, so its next contact is this step's
    // plant. Cleared at every swap and at the plant.
    private bool swingAirborneThisStep;

    // Support-sole hold, m: accumulated lowering asked of the support sole while the swing foot is
    // airborne, so the support stays inside the 0.02 m ground-contact band (see TickWalk).
    private float supportHoldM;

    // -poseTailHold, s (0 = off): on a tail-latched step the load-triggered swap waits until the
    // incoming support's load has read >= leadSwapLoadN continuously this long; the plate load
    // flickers as the foot arrives, so one frame over the gate is not yet a plant.
    private float poseTailHoldSeconds = 0f;

    // Seconds the incoming support's load has stayed >= leadSwapLoadN without a break; read only by
    // ledOut once this step's tail latch is set.
    private float poseTailLoadHeld;

    // Tail latch: set while this step's tail gate is open (support unloaded, swing foot airborne,
    // CoM lead reached), cleared at every swap. Confines the tail hold to the step's own tail.
    private bool poseTailSeenThisStep;

    // Tail-gate support-load bound, N, used only on the step after a flight tail (one opened at the
    // default bound): the outgoing support still carries load there, so the default gate never opens.
    // 50 = the default bound. A tail opened only by this wider bound does not license the next step.
    private float poseNextTailLoadN = 50f;
    // This step's tail opened at the default bound (a flight tail); passed on to the next step at swap.
    private bool poseTailFlightThisStep;
    private bool poseTailFlightPrevStep;

    // Support-sole clearance on the previous walk frame; the hold's rise term is measured from it and
    // only feeds the hold above the cap, so frames below the cap never add to it.
    private float supportHoldClearPrevM;

    // Afterfall crawl (first get-up stage, see DriveAfterfallCrawl). The latches keep each joint's
    // angle at the moment of the fall: the settled pose varies, and the tuck must keep the same shape.
    private bool afterfallCrawlOn;
    private float afterfallStartTime;
    private float afterfallHipL, afterfallHipR;
    private float afterfallKneeL, afterfallKneeR;
    private float afterfallAnkleL, afterfallAnkleR;

    // ─── settle step logic ───
    private void UpdateWalkSettle(bool walkKey, float tilt, float pelvisY, float comVelX,
                                  bool leftDown, bool rightDown)
    {
        if (walkSettleBudget <= 0f) return;
        if (walkKey)
        {
            if (walkSettleLatch) ReleaseWalkSettle("key");
            walkSettleSpent = false;
            walkSettleHoldLive = false;   // CYCLE 118: a real key cancels the hold
            return;
        }
        if (walkSettleLatch)
        {
            walkSettleT += Time.fixedDeltaTime;
            // State exit (see the fields): while armed, a body planted, slow and supported for the quiet
            // span is released to the stand as if on a plant frame.
            if (walkSettleExitMps > 0.001f && walkSettleArmed)
            {
                bool heldNow = leftDown && rightDown
                    && Mathf.Abs(comVelX) <= walkSettleExitMps
                    && (state == null || state.supportMargin >= walkSettleExitMargin);
                walkSettleExitQT = heldNow ? walkSettleExitQT + Time.fixedDeltaTime : 0f;
                if (walkSettleExitQT >= walkSettleExitQuiet)
                {
                    ReleaseWalkSettle("exit");
                    return;
                }
            }
            if (walkSettleT >= walkSettleBudget + 2f) ReleaseWalkSettle("cap");
            return;
        }
        // Handoff hold: ends on a quiet span, a down body or the safety cap, never on a fixed timer.
        if (walkSettleHoldLive)
        {
            walkSettleHoldT += Time.fixedDeltaTime;
            if (Mathf.Abs(comVelX) <= walkSettleHoldMps) walkSettleHoldQT += Time.fixedDeltaTime;
            else walkSettleHoldQT = 0f;
            bool done = walkSettleHoldQT >= walkSettleHoldQuiet
                || walkSettleHoldT >= walkSettleHoldMax;
            if (done || IsDown(tilt, pelvisY))
            {
                walkSettleHoldLive = false;
                Debug.Log(string.Format(CultureInfo.InvariantCulture,
                    "[walkSettle] hold end {0} t={1:F2} v={2:F3}",
                    walkSettleHoldQT >= walkSettleHoldQuiet ? "quiet"
                        : walkSettleHoldT >= walkSettleHoldMax ? "cap" : "down",
                    walkSettleHoldT, comVelX));
            }
        }
        // One capture swing per walk: `spent` blocks re-latching, which would keep the settle walking.
        // With -poseWalkSettleThreat T it may fire again only while the stand's support margin < T.
        bool threat = walkSettleThreatM > 0f && walkSettleSpent
            && state != null && state.supportMargin < walkSettleThreatM;
        if (!walkSettleWasWalk && !threat) return;
        if (walkSettleSpent && !threat) return;
        if (Mathf.Abs(comVelX) <= walkSettleMps) return;
        if (Mathf.Abs(tilt) > 25f) return;
        if (IsDown(tilt, pelvisY)) return;
        if (!standCaptured) return;
        walkSettleLatch = true;
        walkSettleT = 0f;
        walkSettleSwaps = 0;
        walkSettleArmed = false;
        walkSettleExitQT = 0f;
        Debug.Log(string.Format(CultureInfo.InvariantCulture,
            "[walkSettle] enter v={0:F3} budget={1:F1}", comVelX, walkSettleBudget));
    }

    // A plant while latched is the settle's release frame (both feet down). The first plant may still
    // carry a pre-latch plan, so only an armed plant releases, unless the body is already below the
    // moving bar.
    private void SettleOnPlant(bool leftDown, bool rightDown, float comVelX)
    {
        if (!walkSettleLatch) return;
        // Diagnostic: log every plant frame the latch sees, with both contact flags and the armed state.
        Debug.Log(string.Format(CultureInfo.InvariantCulture,
            "[walkSettle] plant t={0:F2} L={1} R={2} v={3:F3} armed={4} swaps={5} m={6:F3}",
            walkSettleT, leftDown ? 1 : 0, rightDown ? 1 : 0, comVelX,
            walkSettleArmed ? 1 : 0, walkSettleSwaps,
            state != null ? state.supportMargin : 0f));
        if (!leftDown || !rightDown) return;
        if (Mathf.Abs(comVelX) <= walkSettleMps) { ReleaseWalkSettle("quiet plant"); return; }
        if (!walkSettleArmed) return;
        ReleaseWalkSettle("plant");
    }

    private void ReleaseWalkSettle(string why)
    {
        if (!walkSettleLatch) return;
        walkSettleLatch = false;
        walkSettleSpent = true;
        // Walk-on guard: a catch step fired right after the release turns it into a walk-on, so arm the
        // fall reflex's deferral window (not on a key return).
        if (walkSettleGuardWin > 0.001f && why != "key" && balance != null)
        {
            balance.settleReleaseGuardT = walkSettleGuardWin;
            Debug.Log(string.Format(CultureInfo.InvariantCulture,
                "[walkSettle] guard arm {0} w={1:F2} v={2:F3}", why, walkSettleGuardWin,
                state != null ? state.comVelocity.x : 0f));
        }
        // Arm the handoff hold on release (not on a key return); the body's own quiet ends it.
        if (walkSettleHoldDeg > 0.001f && why != "key")
        {
            walkSettleHoldLive = true;
            walkSettleHoldT = 0f;
            walkSettleHoldQT = 0f;
            Debug.Log(string.Format(CultureInfo.InvariantCulture,
                "[walkSettle] hold arm v={0:F3}", state != null ? state.comVelocity.x : 0f));
        }
        // Handoff capture: record the walk-end pelvis height the stand blends back from.
        if (walkSettleHandOn)
        {
            walkSettleHandT = walkSettleBlendS;
            walkSettleHandPelvisY = pelvisBody != null ? pelvisBody.position.y : 0f;
        }
        Debug.Log(string.Format(CultureInfo.InvariantCulture,
            "[walkSettle] release {0} t={1:F2} swaps={2} v={3:F3} margin={4:F3}",
            why, walkSettleT, walkSettleSwaps,
            state != null ? state.comVelocity.x : 0f,
            state != null ? state.supportMargin : 0f));
    }

    // ─── walk entry ───
    // First walk frame after the stand (see clWalkEntryMode). Fresh = what the first walk starts with:
    // clock 0, both datums re-latched from this frame, tail latches clear. Standing on one foot, the
    // free foot is the swing; on both (mode 2 only), the rear foot is, when the feet are staggered.
    private void EnterWalkFresh(bool leftDown, bool rightDown, float leftFootX, float rightFootX)
    {
        const float freeLoadN = 50f;
        const float carryLoadN = 250f;
        bool leftFree = !leftDown || poseGroundFzLeftN <= freeLoadN;
        bool rightFree = !rightDown || poseGroundFzRightN <= freeLoadN;
        bool onLeft = rightFree && leftDown && poseGroundFzLeftN >= carryLoadN;
        bool onRight = leftFree && rightDown && poseGroundFzRightN >= carryLoadN;
        if (onLeft != onRight)
        {
            swingSign = onLeft ? -1 : 1;
        }
        else
        {
            if (clWalkEntryMode < 2) return;
            float dir = intent != null && intent.moveX < 0f ? -1f : 1f;
            float leftLead = (leftFootX - rightFootX) * dir;
            // -clEntrySwingFirst (default 0 = the lead decides as before): 1 the right leg always
            // swings first, 2 it alternates per fresh entry (left first on the take's first walk).
            if (clEntrySwingFirst > 0)
                swingSign = clEntrySwingFirst == 1 ? -1 : ((clEntryAlt++ & 1) == 0 ? 1 : -1);
            else if (Mathf.Abs(leftLead) > clEntryRearM) swingSign = leftLead > 0f ? -1 : 1;
            else if (clEntryLoadOn && leftDown && rightDown)
            {
                // Level feet: the foot that carries less swings first - the CoM stands over the other.
                float fzSum = poseGroundFzLeftN + poseGroundFzRightN;
                if (fzSum > 100f
                    && Mathf.Abs(poseGroundFzLeftN - poseGroundFzRightN) > 0.15f * fzSum)
                    swingSign = poseGroundFzLeftN < poseGroundFzRightN ? 1 : -1;
            }
        }
        // One line per fresh entry: which foot swings first and what it starts from (lead = left
        // foot x minus right foot x; hips in deg).
        Debug.Log(string.Format(CultureInfo.InvariantCulture,
            "[walkEntry] t={0:F3} swing={1} lead={2:F3} fzL={3:F0} fzR={4:F0} v={5:F3} hipL={6:F1} hipR={7:F1} tilt={8:F1}",
            Time.fixedTime, swingSign > 0 ? "L" : "R", leftFootX - rightFootX,
            poseGroundFzLeftN, poseGroundFzRightN,
            state != null ? state.comVelocity.x : 0f,
            leftHipJoint != null ? leftHipJoint.jointAngle : 0f,
            rightHipJoint != null ? rightHipJoint.jointAngle : 0f,
            state != null ? state.pelvisTilt : 0f));
        stepClock = 0f;
        comXSwap = float.NaN;
        hipHeightStepDatumM = float.NaN;
        swingAirborneThisStep = false;
        poseTailLoadHeld = 0f;
        poseTailSeenThisStep = false;
        poseTailFlightThisStep = false;
        poseTailFlightPrevStep = false;
        swingFootX0 = swingSign > 0 ? leftFootX : rightFootX;
        stanceFootX = swingSign > 0 ? rightFootX : leftFootX;
    }

    // ─── walk pose ───
    private void TickWalk(float tilt, float pelvisTilt, float pelvisAngVel,
                          float pelvisY, float pelvisVelY, float comX, float comVelX,
                          bool leftDown, bool rightDown, float leftFootX, float rightFootX,
                          float leftFootY, float rightFootY)
    {
        float dt = Time.fixedDeltaTime;
        float halfCycle = swingDuration + stanceDuration;
        float swingFootX = swingSign > 0 ? leftFootX : rightFootX;
        float stanceX = swingSign > 0 ? rightFootX : leftFootX;
        bool swingPlanted = swingSign > 0 ? leftDown : rightDown;
        // Mirror the leg state for WalkReflex (its datum gate and diagnostics; not read here).
        ReflexLeftDown = leftDown;
        ReflexRightDown = rightDown;
        ReflexLeftFootY = leftFootY;
        ReflexRightFootY = rightFootY;
        ReflexLeftFootX = leftFootX;
        ReflexRightFootX = rightFootX;

        // Plant frame: the swing foot's first contact after its own lift (it starts the step planted).
        // Read before the swap block, so a plant that fires the swap is credited to the step it ends.
        if (!swingPlanted) swingAirborneThisStep = true;
        bool posePlantFrame = swingPlanted && swingAirborneThisStep;
        // The planting side, read before the swap below can flip swingSign on this same frame.
        bool posePlantLeft = posePlantFrame && swingSign > 0;
        if (posePlantFrame) swingAirborneThisStep = false;

        // The step clock runs at 1/ReflexCadenceScale (clamped 0.35..1; 1 = normal), so phase, swap
        // timing, the push flick and the bob all speed up when WalkReflex raises the cadence.
        stepClock += dt / Mathf.Clamp(ReflexCadenceScale, 0.35f, 1f);
        // A swap while the swing foot is airborne is a flight phase: the body lands on folded legs the
        // 130 N*m knee ceiling cannot straighten. So the swap waits for the foot to plant, with a hard
        // timeout (half cycle + 0.15 s) so a swing that never lands cannot stall the gait.
        bool maySwap = swingPlanted || stepClock >= halfCycle + 0.15f;
        bool clockDone = stepClock >= halfCycle;
        // The swap waits for the incoming support (the swing foot) to carry load: vertical sole-plate
        // force >= 250 N, about a third of the rig's quiet weight. The planted timeout and clockDone stay
        // the fallbacks, so a foot that never loads still ends its step on the clock.
        const float leadSwapLoadN = 250f;
        float swingFootLoadN = swingSign > 0 ? poseGroundFzLeftN : poseGroundFzRightN;
        bool supportLoaded = swingFootLoadN >= leadSwapLoadN;
        // The load counts as a transfer only on a foot the body is over: the incoming foot must be within
        // bodyAtSupportMeters ahead of the CoM. Swapping onto a foot far ahead while the old support is
        // still loaded leaves the body behind its support and stalls the walk.
        const float bodyAtSupportMeters = 0.15f;
        bool bodyAtSupport = swingFootX - comX <= bodyAtSupportMeters;
        // Minimum step time before the load trigger may fire, so a landing impact spike cannot end the
        // step. Separate from swingDuration (phase, halfCycle and the clock paths keep that), and short
        // enough to allow a quick catch step at speed.
        const float swingFloorSeconds = 0.20f;
        // Tail hold: count continuous seconds of support load; once this step's tail latch is set the
        // load trigger also needs poseTailHoldSeconds of it (0 = no hold).
        poseTailLoadHeld = supportLoaded ? poseTailLoadHeld + dt : 0f;
        bool ledOut = stepClock >= swingFloorSeconds && supportLoaded && bodyAtSupport
            && (!poseTailSeenThisStep || poseTailLoadHeld >= poseTailHoldSeconds);
        // Stumble step: a CoM moving backward faster than ReflexStumbleVelMps for ReflexStumbleHoldS with
        // the swing foot planted forces one swap; the capture point then places the foot behind the body.
        // The latch clears once the backward speed falls under half the bar: one stumble, one step.
        bool reflexStep = false;
        if (ReflexStumbleVelMps > 0.001f)
        {
            if (swingPlanted && comVelX <= -ReflexStumbleVelMps && !reflexStumbleLatch)
                reflexStumbleHoldT += dt;
            else if (comVelX > -ReflexStumbleVelMps * 0.5f)
            {
                reflexStumbleHoldT = 0f;
                reflexStumbleLatch = false;
            }
            if (reflexStumbleHoldT >= ReflexStumbleHoldS && !reflexStumbleLatch)
            {
                reflexStumbleLatch = true;
                reflexStumbleHoldT = 0f;
                reflexStep = true;
                ReflexStumbleSteps++;
            }
        }
        if (maySwap && (clockDone || ledOut || reflexStep))
        {
            // Recorder: why this swap fired. The planted timeout outranks the rest; it flags a swing that
            // never landed within the half cycle plus slack.
            poseAskSwapReason = stepClock >= halfCycle + 0.15f
                ? PoseSwapReasonPlantedTimeout
                : (ledOut && !clockDone ? PoseSwapReasonLead : PoseSwapReasonClock);
            stepClock -= Mathf.Min(stepClock, halfCycle);
            // req-203 SYM: read-source switch (-poseWalkSymRead). 0 (the default) = the read at
            // the plant frame (the req-201 source); 1 = the read at the load-taking swap (the
            // req-202 source). The inactive read is skipped whole, so each value reproduces its
            // own round bit-for-bit.
            if (poseWalkSymRead >= 0.5f)
            {
                // req-202 SYM: the completed step's length is read at the load-taking swap (the
                // incoming support loaded, the body over it), not at the plant frame - a flutter-
                // touch plant reads a bogus gap. Outside the sane band the previous value stays.
                bool symSwapLeft = swingSign > 0;
                float symLeadSwapM = symSwapLeft ? leftFootX - rightFootX : rightFootX - leftFootX;
                if (supportLoaded && bodyAtSupport && symLeadSwapM >= symLeadMinM && symLeadSwapM <= symLeadMaxM)
                {
                    if (symSwapLeft) { symLeadLM = symLeadSwapM; symLeadLSeen = true; }
                    else { symLeadRM = symLeadSwapM; symLeadRSeen = true; }
                    // The event integrator folds the L/R difference in once per completed step; it is
                    // frozen under the settle latch (stop/settle) and outside Balancing (fall-catch).
                    if (poseWalkSymTrimGain != 0f && !walkSettleLatch && symLeadLSeen && symLeadRSeen
                        && balance != null && balance.currentState == BalanceController.BalanceState.Balancing)
                        symTrim = Mathf.Clamp(symTrim + poseWalkSymTrimGain * (symLeadRM - symLeadLM),
                            -poseWalkSymTrimMax, poseWalkSymTrimMax);
                }
            }
            swingSign = -swingSign;
            poseStepCount++;
            // A swap under the settle latch plans the incoming swing with the arrest share; its plant may
            // release the settle (see SettleOnPlant).
            if (walkSettleLatch)
            {
                walkSettleSwaps++;
                walkSettleArmed = true;
            }
            // CoM x at the swap: the datum the descent's `need` is measured from.
            comXSwap = comX;
            // The new step's hip-height datum, latched once per swap.
            hipHeightStepDatumM = pelvisY - FloorDatumM;
            swingFootX0 = swingSign > 0 ? leftFootX : rightFootX;
            stanceFootX = swingSign > 0 ? rightFootX : leftFootX;
            swingFootX = swingSign > 0 ? leftFootX : rightFootX;
            // The new swing leg has just landed; re-arm plant detection for its coming lift and plant.
            swingAirborneThisStep = false;
            // The tail latch is per step: clear it, and pass a flight tail on as the next step's license for
            // the wider tail bound.
            poseTailFlightPrevStep = poseTailFlightThisStep;
            poseTailFlightThisStep = false;
            poseTailSeenThisStep = false;
        }

        // Recorder: this frame's step-clock decisions and the index of the step now in force.
        poseAskMaySwap = maySwap ? 1 : 0;
        poseAskClockDone = clockDone ? 1 : 0;
        poseAskLedOut = ledOut ? 1 : 0;
        poseAskStepIndex = poseStepCount;
        poseAskDriveState = PoseDriveStateWalk;

        float s = Mathf.Clamp01(stepClock / Mathf.Max(0.05f, swingDuration));
        phaseForLog = s;

        // `need`: CoM advance since the swap as a fraction of the swap lead (0 = none, 1 = the new support
        // is needed). Measured from the CoM, not the stance foot, which sits ahead after a placement.
        // It schedules the swing's descent.
        if (float.IsNaN(comXSwap)) comXSwap = comX;
        // The lead is a distance but the descent needs time: at speed the CoM covers it only after the
        // foot lands. So the lead is whole up to needLeadRefMps and shrinks above it (floor 0.35 of it),
        // keeping the descent onset inside the swing.
        const float needLeadRefMps = 0.55f;
        const float needLeadShrinkPerMps = 2.5f;
        const float needLeadFloorFrac = 0.35f;
        float needLeadM = comLeadSwapMeters * Mathf.Max(needLeadFloorFrac,
            1f - needLeadShrinkPerMps * Mathf.Max(0f, comVelX - needLeadRefMps));
        float need = Mathf.Clamp01((comX - comXSwap) / Mathf.Max(0.01f, needLeadM));
        // Stop (see clStopLandPhase): under the settle latch the clock also paces the descent.
        if (clStopLandPhase > 0f && walkSettleLatch)
            need = Mathf.Max(need, Mathf.Clamp01((s - PoseLibrary.FoldKey)
                / Mathf.Max(0.05f, clStopLandPhase - PoseLibrary.FoldKey)));

        // Tail gate: swing foot airborne with a foot down, the support's load at or under its bound, and
        // need >= PlantHoldNeedMin. It only sets this step's tail latch.
        float plantTailLoadN = swingSign > 0 ? poseGroundFzRightN : poseGroundFzLeftN;
        // The bound is the default except on the step after a flight tail (see poseNextTailLoadN).
        float plantTailBoundN = poseTailFlightPrevStep
            ? Mathf.Max(PoseLibrary.SupportHoldLoadN, poseNextTailLoadN)
            : PoseLibrary.SupportHoldLoadN;
        bool plantTailGate = !swingPlanted && (leftDown || rightDown)
            && plantTailLoadN <= plantTailBoundN
            && need >= PoseLibrary.PlantHoldNeedMin;
        if (plantTailGate)
        {
            poseTailSeenThisStep = true;
            // Only a tail opened at the default bound (a flight) licenses the next step's wider bound.
            if (plantTailBoundN <= PoseLibrary.SupportHoldLoadN) poseTailFlightThisStep = true;
        }

        // ─── balance layer: two requests, metres ───
        // 2. Hip height: the height the step started from (latched at its swap), so the error is the
        // step's own sink; capped at the stand frame's height.
        float standHipHeightM = standPelvisY - FloorDatumM;
        float hipHeightLiveM = pelvisY - FloorDatumM;
        // Read, never re-latched here (that would zero the error); the first step, with no swap yet,
        // takes this frame's live height.
        if (float.IsNaN(hipHeightStepDatumM)) hipHeightStepDatumM = hipHeightLiveM;
        // Step bob: lift = amp * (1 - cos(2*pi*phase)) / 2, 0 at both ends of the step and peak midway;
        // phase is clamped at 1 so a late swap holds it. Never asks below the step datum; -poseBobAmp 0
        // = off; the stand frame stays the ceiling.
        float bobPhase01 = Mathf.Clamp01(stepClock / Mathf.Max(0.05f, halfCycle));
        float bobM = bobAmpMeters * (1f - Mathf.Cos(2f * Mathf.PI * bobPhase01)) * 0.5f;
        float hipHeightTargetM = Mathf.Min(standHipHeightM, hipHeightStepDatumM + bobM);
        // Recorder: the height request and the live height.
        poseAskHipHeightRequestM = hipHeightTargetM;
        poseAskHipHeightLiveM = hipHeightLiveM;
        // 1. Foot placement, m: the capture point (CoM after one pendulum time constant) plus the
        // stability margin, clamped to the step band.
        float pendulum = Mathf.Sqrt(Mathf.Max(0.05f, (pelvisY - FloorDatumM) / 9.81f));
        // Catch share 0.5 (ReflexCatchShare) of the pendulum constant asks a lead the walk can take back;
        // the full constant is the arrest point and brakes the body. Under the settle latch the share is
        // walkSettleCatch, plus walkSettleLeadM.
        float settleShareNow = walkSettleLatch ? walkSettleCatch : ReflexCatchShare;
        float captureX = comX + settleShareNow * comVelX * pendulum
            + (walkSettleLatch ? walkSettleLeadM : 0f);
        // The law value and band edges are named so the resolver below can report which one decided.
        float placeLawX = captureX + placementMarginMeters;
        float placeBandFloorX = stanceFootX + stepMinMeters;
        // The default step band caps the walk's speed; above it the plan cannot reach ahead of the body
        // and the walk pitches over. WalkReflex scales the ceiling only (1 = default, clamped 0.5..2.5).
        float placeBandCeilX = stanceFootX + stepMaxMeters
            * Mathf.Clamp(ReflexBandScale, 0.5f, 2.5f);
        float placeX = Mathf.Clamp(placeLawX, placeBandFloorX, placeBandCeilX);
        // Recorder: which branch of the clamp decided placeX (floor, ceiling or law); the reach guard
        // below may relabel it.
        int placeTerm = placeLawX < placeBandFloorX ? PosePlaceTermBandFloor
            : (placeLawX > placeBandCeilX ? PosePlaceTermBandCeiling : PosePlaceTermLaw);
        // Reach guard: the sole may land no further from the hip anchor than sqrt(leg^2 - hipToSole^2),
        // ramped in by `need`; beyond it the leg cannot reach the floor. Measured from the hip joint
        // (pelvisSize.y/2 below the pelvis centre, as in SolveLegIk), not from the pelvis centre.
        float hipX = pelvisBody != null ? pelvisBody.position.x : 0f;
        // -poseAimHipAnchor 1: the aim reads the swing hip's own hinge anchor, not the pelvis centre
        // (14.76 mm apart); the anchor is live, so the hip dynamic's write is what the aim sees.
        if (poseAimHipAnchor && pelvisBody != null)
        {
            HingeJoint2D aimHipJoint = swingSign > 0 ? leftHipJoint : rightHipJoint;
            if (aimHipJoint != null)
                hipX = pelvisBody.transform.TransformPoint(aimHipJoint.connectedAnchor).x;
        }
        float legLengthM = human.thighSize.y + human.shinSize.y;
        // The hip anchor shares the pelvis centre's x (Human.CreateLeg), so hipX above serves for both.
        // -poseAimHipAnchor moves it onto the anchor itself.
        float hipAnchorY = pelvisY - human.pelvisSize.y * 0.5f;
        float hipToSoleM = (hipAnchorY - FloorDatumM) - human.footSize.y * 0.5f;
        if (hipToSoleM >= legLengthM)
        {
            // Hip anchor beyond leg length (e.g. a fully extended stand): the reach circle misses the floor,
            // so apply no clamp - the step band alone bounds placeX. Publish the sentinel, since 0 means the
            // pose gait is not driving.
            poseAskReachXM = PoseReachInertM;
            // Recorder: the reach guard is inert this frame.
            placeTerm = PosePlaceTermReachInert;
        }
        else
        {
            float reachXM = Mathf.Sqrt(legLengthM * legLengthM - hipToSoleM * hipToSoleM);
            float placeGuardedX = Mathf.Lerp(placeX,
                Mathf.Clamp(placeX, hipX - reachXM, hipX + reachXM), need);
            // Recorder: label the guard only if it moved the value (a value inside the reach box returns
            // unchanged, so != is exact).
            if (placeGuardedX != placeX) placeTerm = PosePlaceTermReachGuard;
            placeX = placeGuardedX;
            poseAskReachXM = reachXM;
        }
        // Recorder: the guard's centre x, to read against the placement and comX.
        poseAskHipXM = hipX;
        // req-201 SYM: the step-length symmetry trim. The commanded placement pivot of a left-lead
        // step (left = swingSign > 0) moves +trim/2, of a right-lead step -trim/2; at trim = 0
        // (the default) the branch is skipped and the pivot is bit-identical.
        if (symTrim != 0f)
            placeX += (swingSign > 0) ? 0.5f * symTrim : -0.5f * symTrim;

        // ─── pose-path placement (recorder) ───
        // placeX is the fully resolved pivot (band and reach applied), the value passed as footPlaceX;
        // the sole columns are the step clock's swing and stance foot reads. Nothing reads them back.
        poseAskPlaceXM = placeX;
        poseAskSwingSoleXM = swingFootX;
        poseAskStanceSoleXM = stanceFootX;
        poseAskResolvedTerm = placeTerm;
        if (posePlantFrame)
        {
            // req-203 SYM: the plant-frame read (the req-201 source of round 1), active when
            // -poseWalkSymRead 0 (the default); at 1 the read moves to the load-taking swap.
            if (poseWalkSymRead < 0.5f)
            {
                float symLeadNowM = posePlantLeft ? leftFootX - rightFootX : rightFootX - leftFootX;
                if (posePlantLeft) { symLeadLM = symLeadNowM; symLeadLSeen = true; }
                else { symLeadRM = symLeadNowM; symLeadRSeen = true; }
                // The event integrator folds the L/R difference in once per completed step; it is
                // frozen under the settle latch (stop/settle) and in the automaton's recovery (fall-catch).
                if (poseWalkSymTrimGain != 0f && !walkSettleLatch && symLeadLSeen && symLeadRSeen
                    && balance != null && balance.currentState == BalanceController.BalanceState.Balancing)
                    symTrim = Mathf.Clamp(symTrim + poseWalkSymTrimGain * (symLeadRM - symLeadLM),
                        -poseWalkSymTrimMax, poseWalkSymTrimMax);
            }
            // Per-plant row (see WalkPlantLog), from the same reads as the brake distance; writes no joint.
            RecordPlantRow(posePlantLeft, poseAskStepIndex, comX, pelvisY, s,
                stepClock, leftFootX, leftFootY, rightFootX, rightFootY);
            releasePhasePlantLeadM = (posePlantLeft ? leftFootX : rightFootX) - comX;
            ReleasePhaseOnPlant(posePlantLeft, s);
            // Brake distance: how far the placement pivot sits ahead of the measured CoM on the plant frame.
            // If the plant fired the swap this frame, the pivot is the one published after the swap.
            placeLeadLatchM = placeX - comX;
            // The settle's release check (see SettleOnPlant).
            SettleOnPlant(leftDown, rightDown, comVelX);
        }
        poseAskPlaceLeadM = placeLeadLatchM;

        // ─── support sole hold ───
        // Swing airborne, support unloaded: clearance above SupportHoldClearM (plus its rise) feeds a
        // sole-lowering hold so the contact flag (0.02 m band) never breaks; otherwise the hold decays.
        float supportClearM = swingSign > 0
            ? (state != null ? state.rightFootGroundClearance : 0f)
            : (state != null ? state.leftFootGroundClearance : 0f);
        float supportFootLoadN = swingSign > 0 ? poseGroundFzRightN : poseGroundFzLeftN;
        bool supportHoldGate = !swingPlanted && (leftDown || rightDown)
            && supportFootLoadN <= PoseLibrary.SupportHoldLoadN;
        float supportHoldExcessM = supportHoldGate
            ? Mathf.Max(0f, supportClearM - PoseLibrary.SupportHoldClearM) : 0f;
        if (supportHoldExcessM > 0f)
        {
            // The hold also grows by the sole's rise since the previous frame, but only above the cap, so
            // frames below the cap never recruit it.
            float riseM = Mathf.Max(0f, supportClearM - supportHoldClearPrevM);
            supportHoldClearPrevM = supportClearM;
            supportHoldM = Mathf.Min(PoseLibrary.SupportHoldCeilM,
                supportHoldM + supportHoldExcessM + riseM);
        }
        else
        {
            supportHoldClearPrevM = supportClearM;
            supportHoldM *= PoseLibrary.SupportHoldReleasePerFrame;
            if (supportHoldM < 0.0002f) supportHoldM = 0f;
        }
        // Stiffen the support joints only while the sole is in the contact flag's break zone
        // (>= SupportHoldStiffClearM); below it the asks use the normal gains.
        bool stanceHoldLive = supportHoldM > 0f
            && supportClearM >= PoseLibrary.SupportHoldStiffClearM;

        // Swing-ankle placement carry: the same tail gate (support unloaded, need >= SwingPlaceNeedMin,
        // wider bound after a flight tail), ramped in over swingAnkleRampSeconds (0 = instant).
        float swingPlaceBoundN = poseTailFlightPrevStep
            ? Mathf.Max(PoseLibrary.SupportHoldLoadN, poseNextTailLoadN)
            : PoseLibrary.SupportHoldLoadN;
        bool swingPlaceGate = !swingPlanted && (leftDown || rightDown)
            && supportFootLoadN <= swingPlaceBoundN
            && need >= SwingPlaceNeedMin;
        if (swingPlaceGate) swingAnkleRampT += dt; else swingAnkleRampT = 0f;
        float swingPlaceRamp = swingAnkleRampSeconds <= 0f
            ? 1f : Mathf.Clamp01(swingAnkleRampT / swingAnkleRampSeconds);

        // ─── handover: metres to degrees ───
        // The pose library is the only writer of the six leg joints from here; DriveHips is not called.
        // The trunk is held by the pose's pelvis target and the library's bounded stance-hip correction.
        PoseLibrary.Inputs poseIn = new PoseLibrary.Inputs();
        poseIn.swingPhase = s;
        poseIn.landingShare = swingLandShare;
        poseIn.footPlaceX = placeX;
        // Descent-arithmetic gate for the library (off = the drawn knee bias).
        poseIn.placeKneeLive = placeKneeLive;
        // The swing ankle's gated, ramped carry (0 = the drawn ankle).
        poseIn.swingAnkleDeg = swingPlaceGate ? swingAnkleDeg * swingPlaceRamp : 0f;
        poseIn.hipHeightM = hipHeightLiveM;
        poseIn.hipHeightTargetM = hipHeightTargetM;
        // req-226 AIM (mode 2): the landing is aimed from the swing hip's own anchor x, cancelling
        // the +-14.76 mm measurement offset the pelvis-centre aim carries; mode 0/1 leave hipX alone.
        float hipDynAimM = hipDynMode == 2 && hipDynGate && !poseAimHipAnchor
            && leftHipJoint != null && rightHipJoint != null
            ? hipDynAim * (swingSign > 0 ? leftHipJoint.connectedAnchor.x : rightHipJoint.connectedAnchor.x)
            : 0f;
        poseIn.pelvisX = hipX + hipDynAimM;
        hipDynAimLive = hipDynAimM;
        // The same `need` that ramps the reach guard also drives the library's descent.
        poseIn.supportNeed = need;
        poseIn.pelvisTiltDeg = pelvisTilt;
        poseIn.pelvisAngVelDeg = pelvisAngVel;
        poseIn.pelvisVelY = pelvisVelY;
        poseIn.thighM = human.thighSize.y;
        poseIn.shinM = human.shinSize.y;
        // The ankle sits at the heel plate's centre, so the sole hangs half a foot below the hinge.
        poseIn.soleBelowAnkleM = human.footSize.y * 0.5f;
        // Support-sole hold, m: the library turns it into stance ankle press and knee extension
        // (0 = none), plus the foot lever the ankle press is computed against.
        poseIn.stanceSoleHoldM = supportHoldM;
        poseIn.stanceSoleLeverM = human.footSize.x * (1f - human.footHeelShare * 0.5f);
        // Swing clearance: -poseLift plus WalkReflex's boost before a rise (0 without the reflex).
        poseIn.swingLiftM = swingLiftMeters + ReflexLiftBoostM;
        // Published for WalkReflex's diagnostic line only.
        ReflexPlaceX = placeX;
        // Stance knee keys for the library (default = the drawn values).
        poseIn.stanceFoldKneeDeg = stanceFoldKneeDeg;
        poseIn.stancePlaceKneeDeg = stancePlaceKneeDeg;
        poseIn.stanceLoadKneeDeg = stanceLoadKneeDeg;
        poseIn.heightGainDegPerM = heightGainDegPerMeter;
        poseIn.heightDampDegPerMps = heightDampDegPerMeterPerSecond;
        poseIn.hipMin = human.hipLimitMin;
        poseIn.hipMax = human.hipLimitMax;
        poseIn.kneeMin = human.kneeLimitMin;
        poseIn.kneeMax = human.kneeLimitMax;
        poseIn.ankleLimit = AnkleLimitDeg;
        poseIn.toeMin = human.toeLimitMin;
        poseIn.toeMax = human.toeLimitMax;
        PoseLibrary.Targets pose = PoseLibrary.Sample(poseIn);

        // The pose's trunk target plus WalkReflex's lean; DriveTrunk reads it.
        torsoTargetDeg = pose.torsoTiltTargetDeg + ReflexTorsoLeanDeg;   // WALK REFLEX v2f
        poseAskPelvisTiltTargetDeg = pose.pelvisTiltTargetDeg;
        // No IK on the pose path, so the IK-saturation witness is false; PoseLibrary.ClampLeg bounds
        // every angle to the rig's limits instead.
        IkSaturated = false;
        IkSolveTick = Time.frameCount;

        // One writer per joint: the library's swing/stance legs map to rig sides by the step clock.
        bool leftIsSwing = swingSign > 0;
        PoseLibrary.LegPose leftLeg = leftIsSwing ? pose.swingLeg : pose.stanceLeg;
        PoseLibrary.LegPose rightLeg = leftIsSwing ? pose.stanceLeg : pose.swingLeg;
        // Recorder: the six joint requests per rig side, before ActuatorMixer's live-limit clamp.
        poseAskHipLDeg = leftLeg.hip;
        poseAskKneeLDeg = leftLeg.knee;
        poseAskAnkleLDeg = leftLeg.ankle;
        poseAskHipRDeg = rightLeg.hip;
        poseAskKneeRDeg = rightLeg.knee;
        poseAskAnkleRDeg = rightLeg.ankle;
        // Push flick: the stance ankle's gain boost is a smoothstep pulse (half-width PushFlickHalfS)
        // centred at swingDuration + PushFlickShiftS on the step clock, over the loaded roll before the
        // foot lifts; the factor is 1 outside the pulse, and pushDeliverP 1 disables it.
        const float PushFlickHalfS = 0.05f;
        const float PushFlickShiftS = 0.20f;
        float flickU = (stepClock - (swingDuration + PushFlickShiftS - PushFlickHalfS))
            / (2f * PushFlickHalfS);
        float flickScale = 0f;
        if (flickU > 0f && flickU < 1f)
        {
            float flickT = flickU < 0.5f ? flickU * 2f : (1f - flickU) * 2f;
            flickScale = flickT * flickT * (3f - 2f * flickT);
        }
        bool supportDown = swingSign > 0 ? rightDown : leftDown;
        // No amplified flick while WalkReflex holds it (a toe at a riser would roll against the wall).
        // Under the settle latch walkSettlePush scales the amplification: the arrest step sheds speed.
        float pushMax = walkSettleLatch && walkSettlePush < 1f
            ? 1f + (pushDeliverP - 1f) * walkSettlePush : pushDeliverP;
        float pushPFactor = pushDeliverP > 1f && supportDown && !ReflexHoldPush
            ? Mathf.Lerp(1f, pushMax, flickScale) : 1f;
        // A leg with a step-up factor gets no amplified flick: on a rise the climb's push replaces it.
        float flickL = ReflexStepUpL > 0f ? 1f : pushPFactor;
        float flickR = ReflexStepUpR > 0f ? 1f : pushPFactor;
        // Settle stance-knee floor: walkSettleBendDeg, or the load-driven give if larger (load above
        // walkSettleBendBar body weights, walkSettleBendLoad deg each, capped). Off = -inf, no floor.
        float kneeFloorDeg = walkSettleLatch && walkSettleBendDeg > 0.001f
            ? walkSettleBendDeg : float.NegativeInfinity;
        if (walkSettleLatch && walkSettleBendLoad > 0.001f)
        {
            float fzHot = Mathf.Max(poseGroundFzLeftN, poseGroundFzRightN);
            float bwN = Mathf.Max(1f, PoseRigWeightN());
            float give = walkSettleBendLoad * Mathf.Max(0f, fzHot / bwN - walkSettleBendBar);
            give = Mathf.Min(give, walkSettleBendCap);
            if (give > 0.001f && give > kneeFloorDeg) kneeFloorDeg = give;
        }
        float kneeLOut = Mathf.Max(leftLeg.knee, kneeFloorDeg);
        float kneeROut = Mathf.Max(rightLeg.knee, kneeFloorDeg);
        DriveOneLeg(true, leftLeg.hip, kneeLOut, true, leftLeg.ankle,
            stanceHoldLive && !leftIsSwing, leftIsSwing ? 1f : flickL,
            stepUpFactor: ReflexStepUpL);
        DriveOneLeg(false, rightLeg.hip, kneeROut, true, rightLeg.ankle,
            stanceHoldLive && leftIsSwing, leftIsSwing ? flickR : 1f,
            stepUpFactor: ReflexStepUpR);
        DriveToes(leftLeg.toe, rightLeg.toe);

        // Arms counter the opposite leg: the left arm swings with the right leg, so each shoulder stays
        // anti-correlated with its own-side leg whenever the arm amplitude is non-zero.
        float armPhase = Mathf.Cos(Mathf.PI * s) * Mathf.Sign(swingSign);
        float shoulderL = armSwingDeg * armPhase;
        float shoulderR = -shoulderL;
        float elbowL = -20f - 12f * Mathf.Abs(armPhase) * (armPhase > 0f ? 1f : 0f);
        float elbowR = -20f - 12f * Mathf.Abs(armPhase) * (armPhase < 0f ? 1f : 0f);
        DriveArms(shoulderL, shoulderR, elbowL, elbowR);
    }

    // Planar two-link IK (a = thigh, b = shin; knee forward): theta = phi + alpha, flexion = 2*alpha.
    // Unused by the walk (the pose library holds joint angles); kept as a reference solver.
    private void SolveLegIk(float ankleX, float ankleY, float pelvisY, float pelvisTilt,
                            out float hipDeg, out float kneeDeg, float maxShare = 0.995f)
    {
        float a = human.thighSize.y;
        float b = human.shinSize.y;
        float hipY = pelvisY - human.pelvisSize.y * 0.5f;
        float dx = ankleX - pelvisBody.position.x;
        // Keep the ankle target at least 0.12 m below the hip: the solve has no branch for a target
        // above it, and a sunk pelvis must not yield a degenerate direction.
        float dy = Mathf.Min(ankleY - hipY, -0.12f);
        float reach = Mathf.Sqrt(dx * dx + dy * dy);
        float maxReach = (a + b) * maxShare;
        float length = Mathf.Clamp(reach, (a + b) * 0.45f, maxReach);
        // Recorder: the solve clamped - dy (ankle target within 0.12 m of the hip, as when the pelvis
        // sinks) or length (target out of reach).
        IkSaturated = ((ankleY - hipY) > -0.12f) || Mathf.Abs(length - reach) > 1e-4f;
        IkSolveTick = Time.frameCount;
        float alpha = Mathf.Acos(Mathf.Clamp(length / (a + b), 0f, 1f));
        float phi = Mathf.Atan2(dx, Mathf.Max(0.01f, -dy));
        float thighWorld = phi + alpha;
        kneeDeg = 2f * alpha * Mathf.Rad2Deg;
        // jointAngle(hip) = pelvis rotation - thigh world rotation: a thigh rotated forward lowers the
        // joint angle below the pelvis upright zero.
        hipDeg = pelvisTilt - thighWorld * Mathf.Rad2Deg;
    }

    // The stop's coast-down (see the field block above): one call per tick of the walk/stand path.
    private void UpdateReleaseRamp()
    {
        if (intent == null) return;
        float dt = Time.fixedDeltaTime;
        if (dt <= 0f) dt = 0.005f;
        float raw = intent.moveXRaw;
        if (clReleaseRampFallT >= 0f)
        {
            // The press glide (-clReleaseRampPressFall, see the switch): the press caught the coast
            // but the end waits - the command runs on to the floor over f, then the end fires (the
            // coast still reads live, so the sources keep the step driver off until it does).
            clReleaseRampFallT += dt;
            float uf = Mathf.Clamp01(clReleaseRampFallT / clReleaseRampPressFall);
            clReleaseRampV = clReleaseRampDir * Mathf.Lerp(Mathf.Abs(clReleaseRampFallV0), ClReleaseRampFloor, uf);
            intent.moveXRaw = clReleaseRampV;
            clReleaseRampPrev = clReleaseRampV;
            ReleaseRampCatch(1f);
            if (clReleaseRampFallT >= clReleaseRampPressFall)
            {
                clReleaseRampFallT = -1f;
                clReleaseRampT = -1f;
                clReleaseRampV = 0f;
                intent.moveXRaw = 0f;
                clReleaseRampPrev = 0f;
                RestoreReleaseRampCatch();
                if (raw >= 0.999f)
                {
                    // The feet's split at the end (the gate's own stagger read), for the round's log.
                    float pressStagger = 0f;
                    if (leftAnkleJoint != null && rightAnkleJoint != null)
                        pressStagger = Mathf.Abs(leftAnkleJoint.transform.TransformPoint(leftAnkleJoint.anchor).x
                            - rightAnkleJoint.transform.TransformPoint(rightAnkleJoint.anchor).x);
                    Debug.Log(string.Format(CultureInfo.InvariantCulture,
                        "[releaseRamp] end t={0:F3} s={1:F2} press stag={2:F3}",
                        Time.fixedTime, clReleaseRamp, pressStagger));
                }
                else
                {
                    Debug.Log(string.Format(CultureInfo.InvariantCulture,
                        "[releaseRamp] end t={0:F3} s={1:F2}", Time.fixedTime, clReleaseRamp));
                }
                ArmReleaseRampPost();
            }
            return;
        }
        if (clReleaseRampT >= 0f)
        {
            // A fresh press - exactly 1 from either writer; this ramp's own values stay below 0.999 -
            // cancels the coast-down: the command stands at 1, the key never went up. With
            // -clReleaseRampPress v the press ENDS the coast instead while its command is <= v: the
            // span's own end (the gate then takes the held key as a press from a stand), logged
            // with the press mark so the tools can tell the two ends apart.
            // req-148 (req-145's fix): with -clReleaseRampShape the first values ARE >= 0.999 (shape 2
            // starts at 0.999995), and the keyboard writes the key once a rendered frame - on the ticks
            // between two frames this read is the ramp's own last write. That echo is not a press.
            // (Exponents >= 3 start at exactly 1: a press on those first ticks is seen a little late.)
            if (raw >= 0.999f && raw != clReleaseRampV)
            {
                if (clReleaseRampPressSpan && clReleaseRampPress > 0f
                    && Mathf.Abs(clReleaseRampV) <= clReleaseRampPress)
                {
                    // The ride (-clReleaseRampPressSpan): the caught press does not end the coast here -
                    // the span runs on to the floor; the span-end block below fires the press end.
                    if (!clReleaseRampPressRide)
                    {
                        clReleaseRampPressRide = true;
                        Debug.Log(string.Format(CultureInfo.InvariantCulture,
                            "[releaseRamp] ride t={0:F3} v={1:F2} span={2:F2}",
                            Time.fixedTime, clReleaseRampV, clReleaseRamp));
                    }
                }
                else if (clReleaseRampPress > 0f && Mathf.Abs(clReleaseRampV) <= clReleaseRampPress)
                {
                    if (clReleaseRampPressFall > 0f)
                    {
                        // The press caught the coast but the end waits (-clReleaseRampPressFall): the
                        // command runs on to the floor over f first (the glide stage above fires the
                        // end); until then the coast still reads live and the gate keeps waiting.
                        clReleaseRampFallT = 0f;
                        clReleaseRampFallV0 = clReleaseRampV;
                        clReleaseRampPrev = clReleaseRampV;
                        ReleaseRampCatch(1f);
                        Debug.Log(string.Format(CultureInfo.InvariantCulture,
                            "[releaseRamp] fall t={0:F3} v={1:F2} f={2:F2}",
                            Time.fixedTime, clReleaseRampFallV0, clReleaseRampPressFall));
                        return;
                    }
                    clReleaseRampT = -1f;
                    clReleaseRampV = 0f;
                    intent.moveXRaw = 0f;
                    clReleaseRampPrev = 0f;
                    RestoreReleaseRampCatch();
                    // The feet's split at the press (the gate's own stagger read), for the round's log.
                    float pressStagger = 0f;
                    if (leftAnkleJoint != null && rightAnkleJoint != null)
                        pressStagger = Mathf.Abs(leftAnkleJoint.transform.TransformPoint(leftAnkleJoint.anchor).x
                            - rightAnkleJoint.transform.TransformPoint(rightAnkleJoint.anchor).x);
                    Debug.Log(string.Format(CultureInfo.InvariantCulture,
                        "[releaseRamp] end t={0:F3} s={1:F2} press stag={2:F3}",
                        Time.fixedTime, clReleaseRamp, pressStagger));
                    ArmReleaseRampPost();
                    return;
                }
                if (!clReleaseRampPressRide)
                {
                    clReleaseRampT = -1f;
                    clReleaseRampPrev = raw;
                    RestoreReleaseRampCatch();
                    return;
                }
            }
            clReleaseRampT += dt;
            if (clReleaseRampT >= clReleaseRamp)
            {
                if (clReleaseRampPressRide)
                {
                    // The ride's end (-clReleaseRampPressSpan): the span is spent - the end fires here
                    // as the press end would have, a span later (the held key is the gate's key).
                    clReleaseRampPressRide = false;
                    clReleaseRampT = -1f;
                    clReleaseRampV = 0f;
                    intent.moveXRaw = 0f;
                    clReleaseRampPrev = 0f;
                    RestoreReleaseRampCatch();
                    if (raw >= 0.999f)
                    {
                        float pressStagger = 0f;
                        if (leftAnkleJoint != null && rightAnkleJoint != null)
                            pressStagger = Mathf.Abs(leftAnkleJoint.transform.TransformPoint(leftAnkleJoint.anchor).x
                                - rightAnkleJoint.transform.TransformPoint(rightAnkleJoint.anchor).x);
                        Debug.Log(string.Format(CultureInfo.InvariantCulture,
                            "[releaseRamp] end t={0:F3} s={1:F2} press stag={2:F3}",
                            Time.fixedTime, clReleaseRamp, pressStagger));
                    }
                    else
                    {
                        Debug.Log(string.Format(CultureInfo.InvariantCulture,
                            "[releaseRamp] end t={0:F3} s={1:F2} ride=1",
                            Time.fixedTime, clReleaseRamp));
                    }
                    ArmReleaseRampPost();
                    return;
                }
                if (clReleaseRampKeep > 0f)
                {
                    // The span is spent but the keep holds the coast at the floor (see the switch): the
                    // walk window stays open at the floor k more seconds, so the stop lands from a live
                    // slow walk instead of the span end's cut; only then does the command drop to 0.
                    clReleaseRampT = -1f;
                    clReleaseRampKeepT = 0f;
                    clReleaseRampV = clReleaseRampDir * ClReleaseRampFloor;
                    intent.moveXRaw = clReleaseRampV;
                    clReleaseRampPrev = clReleaseRampV;
                    ReleaseRampCatch(1f);
                    Debug.Log(string.Format(CultureInfo.InvariantCulture,
                        "[releaseRamp] keep start t={0:F3} s={1:F2} k={2:F2}",
                        Time.fixedTime, clReleaseRamp, clReleaseRampKeep));
                    return;
                }
                if (clReleaseRampFade > 0f)
                {
                    // The span is spent but the coast is not: the command decays floor -> 0 over f
                    // more seconds instead of the cut at the floor (see the switch), so the arrest
                    // is spread instead of dropped; the catch, if on, rides the fade at its value.
                    clReleaseRampT = -1f;
                    clReleaseRampFadeT = 0f;
                    clReleaseRampV = clReleaseRampDir * ClReleaseRampFloor;
                    intent.moveXRaw = clReleaseRampV;
                    clReleaseRampPrev = clReleaseRampV;
                    ReleaseRampCatch(1f);
                    Debug.Log(string.Format(CultureInfo.InvariantCulture,
                        "[releaseRamp] fade start t={0:F3} s={1:F2} f={2:F2}",
                        Time.fixedTime, clReleaseRamp, clReleaseRampFade));
                    return;
                }
                // The span is spent: the command drops to 0 - the arrest the ramp exists to soften.
                clReleaseRampT = -1f;
                clReleaseRampV = 0f;
                intent.moveXRaw = 0f;
                clReleaseRampPrev = 0f;
                RestoreReleaseRampCatch();
                Debug.Log(string.Format(CultureInfo.InvariantCulture,
                    "[releaseRamp] end t={0:F3} s={1:F2}", Time.fixedTime, clReleaseRamp));
                ArmReleaseRampPost();
                return;
            }
            float u = clReleaseRampT / clReleaseRamp;
            float us = clReleaseRampShape > 0f ? Mathf.Pow(u, clReleaseRampShape) : u;
            clReleaseRampV = clReleaseRampDir * Mathf.Lerp(1f, ClReleaseRampFloor, us);
            intent.moveXRaw = clReleaseRampV;
            clReleaseRampPrev = clReleaseRampV;
            ReleaseRampCatch(u);
            return;
        }
        // The keep (see the switch): the span's end held at the floor; the drop here is the coast's
        // real end. A press cancels it exactly as it cancels the ramp.
        if (clReleaseRampKeepT >= 0f)
        {
            if (raw >= 0.999f)
            {
                clReleaseRampKeepT = -1f;
                clReleaseRampPrev = raw;
                RestoreReleaseRampCatch();
                return;
            }
            clReleaseRampKeepT += dt;
            if (clReleaseRampKeepT >= clReleaseRampKeep)
            {
                clReleaseRampKeepT = -1f;
                clReleaseRampV = 0f;
                intent.moveXRaw = 0f;
                clReleaseRampPrev = 0f;
                RestoreReleaseRampCatch();
                Debug.Log(string.Format(CultureInfo.InvariantCulture,
                    "[releaseRamp] end t={0:F3} s={1:F2} keep={2:F2}",
                    Time.fixedTime, clReleaseRamp, clReleaseRampKeep));
                ArmReleaseRampPost();
                return;
            }
            clReleaseRampV = clReleaseRampDir * ClReleaseRampFloor;
            intent.moveXRaw = clReleaseRampV;
            clReleaseRampPrev = clReleaseRampV;
            ReleaseRampCatch(1f);
            return;
        }
        // The fade (see the switch): the coast's second stage - the floor decays to 0 within f, so
        // the drop to 0 lands from ~0; a press cancels it exactly as it cancels the ramp.
        if (clReleaseRampFadeT >= 0f)
        {
            if (raw >= 0.999f)
            {
                clReleaseRampFadeT = -1f;
                clReleaseRampPrev = raw;
                RestoreReleaseRampCatch();
                return;
            }
            clReleaseRampFadeT += dt;
            if (clReleaseRampFadeT >= clReleaseRampFade)
            {
                clReleaseRampFadeT = -1f;
                clReleaseRampV = 0f;
                intent.moveXRaw = 0f;
                clReleaseRampPrev = 0f;
                RestoreReleaseRampCatch();
                Debug.Log(string.Format(CultureInfo.InvariantCulture,
                    "[releaseRamp] end t={0:F3} s={1:F2} fade={2:F2}",
                    Time.fixedTime, clReleaseRamp, clReleaseRampFade));
                ArmReleaseRampPost();
                return;
            }
            float uf = clReleaseRampFadeT / clReleaseRampFade;
            clReleaseRampV = clReleaseRampDir * Mathf.Lerp(ClReleaseRampFloor, 0f, uf);
            intent.moveXRaw = clReleaseRampV;
            clReleaseRampPrev = clReleaseRampV;
            ReleaseRampCatch(1f);
            return;
        }
        // The release edge: a command that read as a walk (>= 0.5) last tick and reads 0 now, with
        // the gait actually driving last tick. Only then does the coast-down arm.
        if (clReleaseRamp > 0f && !intent.holdWalk && walkSettleWasWalk
            && clReleaseRampPrev >= 0.5f && raw < 0.01f)
        {
            clReleaseRampDir = clReleaseRampPrev > 0f ? 1f : -1f;
            clReleaseRampT = dt;
            float u = clReleaseRampT / clReleaseRamp;
            float us = clReleaseRampShape > 0f ? Mathf.Pow(u, clReleaseRampShape) : u;
            clReleaseRampV = clReleaseRampDir * Mathf.Lerp(1f, ClReleaseRampFloor, us);
            intent.moveXRaw = clReleaseRampV;
            clReleaseRampPrev = clReleaseRampV;
            ReleaseRampCatch(u);
            Debug.Log(string.Format(CultureInfo.InvariantCulture,
                "[releaseRamp] start t={0:F3} s={1:F2} dir={2:F0} catch={3:F2} shape={4:F2}",
                Time.fixedTime, clReleaseRamp, clReleaseRampDir, clReleaseRampCatch, clReleaseRampShape));
            return;
        }
        clReleaseRampPrev = raw;
    }

    // The coast's press (-clReleaseRampPress, see the switch): true while a fresh press (a writer
    // holding raw at 1) would END the live coast this tick - the key sources read it before they
    // would start the step driver, so the press is the entry gate's from its first tick (no
    // BeginWalk, no Tick before the gate opens). False with the switch off or no coast live.
    public bool ClReleaseRampPressEnds => clReleaseRampT >= 0f && clReleaseRampPress > 0f
        && Mathf.Abs(clReleaseRampV) <= clReleaseRampPress;

    // The phase release (req-117): the source hands the walk key in and acts on the return; off = down
    // back. A key-up further than W past this walk's last right plant pends: the key reads held until the
    // first right plant after the pend + D, the cap, or another action key; the ending go is logged.
    // req-121: R lives with the walk (cleared in Tick); a pend dies ('end') when the walk stops under it.
    public bool ClReleasePhaseKey(bool down, bool other)
    {
        if (!clReleasePhaseOn || intent == null) return down;
        float now = Time.fixedTime;
        if (down)
        {
            if (clReleasePhasePend) ReleasePhaseGo("key");
            clReleasePhasePrev = true;
            return true;
        }
        if (clReleasePhasePend)
        {
            if (!walkSettleWasWalk) { ReleasePhaseGo("end"); return false; }
            if (other) { ReleasePhaseGo("other"); return false; }
            if (clReleasePhaseRn >= 0f && now >= clReleasePhaseRn + clReleasePhaseD + Time.fixedDeltaTime * 0.5f)
            { ReleasePhaseGo("plant"); return false; }
            if (now >= clReleasePhaseT + clReleasePhaseMax - Time.fixedDeltaTime * 0.5f) { ReleasePhaseGo("cap"); return false; }
            clReleasePhasePrev = true;
            return true;
        }
        bool edge = clReleasePhasePrev;
        clReleasePhasePrev = false;
        bool pendNow = edge && clReleasePhaseR >= 0f && now - clReleasePhaseR > clReleasePhaseW
            && walkSettleWasWalk && !intent.holdWalk && !ClReleaseCoastLive();
        // -clReleasePhaseHold (req-144 / req-147): a key-up that would pass while the walk runs is
        // held instead; the pend's own Rn / D release ends it at the walk's next right plant. 2 = only
        // with no accepted right plant in this walk yet (R < 0); 1 adds round 5's terms (a foot in
        // the air, or the walk <= 2.6 s old).
        bool tap2Now = clReleasePhaseHold > 0 && edge && !pendNow && walkSettleWasWalk && !intent.holdWalk
            && !ClReleaseCoastLive() && state != null
            && (clReleasePhaseR < 0f || (clReleasePhaseHold == 1
                && (!(state.leftFootGrounded && state.rightFootGrounded) || now - clTap2WalkT <= 2.6f)));
        if (pendNow || tap2Now)
        {
            clReleasePhasePend = true;
            clReleasePhaseT = now;
            clReleasePhaseRn = -1f;
            clReleasePhasePrev = true;
            if (clReleasePhaseSide == 2)
            {
                // -clReleasePhaseSide 2: this stop accepts the side the alternator hands it, then flips.
                clReleaseSideStop = clReleaseSideAlt;
                clReleaseSideAlt ^= 1;
            }
            if (tap2Now)
                Debug.Log(string.Format(CultureInfo.InvariantCulture,
                    "[tap2] hold t={0:F3} R={1:F3} ld={2} rd={3} age={4:F3}", now, clReleasePhaseR,
                    state.leftFootGrounded ? 1 : 0, state.rightFootGrounded ? 1 : 0, now - clTap2WalkT));
            else
                Debug.Log(string.Format(CultureInfo.InvariantCulture, "[releasePhase] pend t={0:F3} R={1:F3}", now, clReleasePhaseR));
            return true;
        }
        if (edge) Debug.Log(string.Format(CultureInfo.InvariantCulture, "[releasePhase] pass t={0:F3} R={1:F3}", now, clReleasePhaseR));
        return false;
    }

    private void ReleasePhaseGo(string why)
    {
        Debug.Log(string.Format(CultureInfo.InvariantCulture, "[releasePhase] go t={0:F3} why={1} Rn={2:F3}", Time.fixedTime, why, clReleasePhaseRn));
        clReleasePhasePend = false; clReleasePhaseRn = -1f; clReleasePhasePrev = false;
    }

    private bool ClReleaseCoastLive()
    {
        return clReleaseRampT >= 0f || clReleaseRampFallT >= 0f || clReleaseRampFadeT >= 0f || clReleaseRampKeepT >= 0f;
    }

    // The plant hook (after RecordPlantRow): a right plant = plantLeft 0 + landBlendU 1; feeds R, then Rn.
    // -clReleasePhaseLandU < 1 moves the bar off 0.99995 to u and adds a lead >= 0.20 term for an early
    // landing only (blendU < 0.99995); a full landing is accepted as with the switch off (drops logged).
    private void ReleasePhaseOnPlant(bool plantLeft, float swingPhase)
    {
        if (!clReleasePhaseOn || !ReleaseSideAccepted(plantLeft)) return;
        float land = Mathf.Clamp(swingLandShare, PoseLibrary.FoldKey + 0.05f, 0.98f);
        float blendU = Mathf.Clamp01((swingPhase - PoseLibrary.FoldKey) / Mathf.Max(0.05f, land - PoseLibrary.FoldKey));
        float bar = clReleasePhaseLandU < 1f ? clReleasePhaseLandU : 0.99995f;
        if (blendU < bar || (clReleasePhaseLandU < 1f && blendU < 0.99995f && releasePhasePlantLeadM < 0.20f))
        {
            Debug.Log(string.Format(CultureInfo.InvariantCulture,
                "[releasePhase] skip t={0:F3} u={1:F4} pend={2}", Time.fixedTime, blendU, clReleasePhasePend ? 1 : 0));
            return;
        }
        clReleasePhaseR = Time.fixedTime;
        if (clReleasePhasePend && clReleasePhaseRn < 0f && clReleasePhaseR > clReleasePhaseT) clReleasePhaseRn = clReleasePhaseR;
    }

    // -clReleasePhaseSide: which plant the phase release accepts. 0 = right only (default), 1 = either
    // plant, 2 = the side this stop's alternator picked (right on the take's first stop).
    private bool ReleaseSideAccepted(bool plantLeft)
    {
        if (clReleasePhaseSide == 0) return !plantLeft;
        if (clReleasePhaseSide == 1) return true;
        return plantLeft == (clReleaseSideStop == 1);
    }

    // The stand the coast leaves (see -clReleaseRampClose): stamped at the coast's end, cleared by the
    // next walk. While set, the stance close arms at the coast's bar - the feet a coast leaves between
    // the gate's ready bar and the plain close bar are levelled instead of waiting out the gate.
    private void ArmReleaseRampPost()
    {
        if (clReleaseRampClose <= 0f) return;
        clReleaseRampPost = true;
        Debug.Log(string.Format(CultureInfo.InvariantCulture,
            "[releaseRamp] post t={0:F3} m={1:F2}", Time.fixedTime, clReleaseRampClose));
    }

    // The catch seat of the coast-down (see the field block above): while the ramp runs the placement's
    // catch share rises 0.5 -> c on the ramp's own clock, the harness brake's law at -walkPlanBrakeCatch,
    // and RestoreReleaseRampCatch puts the landed 0.5 back the moment the ramp ends or is cancelled.
    // 0 = off: no write at all, so takes without the switch stay tick for tick the ramp-only ones.
    private void ReleaseRampCatch(float u)
    {
        if (clReleaseRampCatch <= 0f) return;
        ReflexCatchShare = Mathf.Lerp(0.5f, clReleaseRampCatch, Mathf.Clamp01(u));
        clReleaseRampCatchWrote = true;
    }

    private void RestoreReleaseRampCatch()
    {
        if (!clReleaseRampCatchWrote) return;
        ReflexCatchShare = 0.5f;
        clReleaseRampCatchWrote = false;
    }
}
