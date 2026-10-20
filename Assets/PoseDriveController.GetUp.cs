using System.Globalization;
using UnityEngine;

// PoseDriveController: the afterfall crawl, the fall guard and the get-up ladder (TickGetUp).
public partial class PoseDriveController
{
    // Afterfall crawl: each leg joint is driven as a DELTA from the angle latched when the body went
    // down (the settle pose varies, so a delta keeps the tuck's shape). A smoothstep ramp and a slow
    // rock keep the legs from punching or freezing. Arms are untouched: ArmDrive owns them.
    private void DriveAfterfallCrawl()
    {
        if (!afterfallCrawlOn)
        {
            afterfallCrawlOn = true;
            afterfallStartTime = Time.fixedTime;
            afterfallHipL = leftHipJoint != null ? leftHipJoint.jointAngle : 0f;
            afterfallHipR = rightHipJoint != null ? rightHipJoint.jointAngle : 0f;
            afterfallKneeL = leftKneeJoint != null ? leftKneeJoint.jointAngle : 0f;
            afterfallKneeR = rightKneeJoint != null ? rightKneeJoint.jointAngle : 0f;
            afterfallAnkleL = leftAnkleJoint != null ? leftAnkleJoint.jointAngle : 0f;
            afterfallAnkleR = rightAnkleJoint != null ? rightAnkleJoint.jointAngle : 0f;
        }
        float u = Time.fixedTime - afterfallStartTime;
        float ramp = Mathf.Clamp01(u / PoseLibrary.AfterfallRampSeconds);
        ramp = ramp * ramp * (3f - 2f * ramp);
        float rock = 0.7f + 0.3f * Mathf.Sin(2f * Mathf.PI * u / PoseLibrary.AfterfallRockSeconds);
        float tuck = ramp * rock;
        DriveOneLeg(true, afterfallHipL - PoseLibrary.AfterfallHipTuckDeg * tuck,
            afterfallKneeL + PoseLibrary.AfterfallKneeTuckDeg * tuck, true,
            afterfallAnkleL + PoseLibrary.AfterfallAnkleTuckDeg * tuck);
        DriveOneLeg(false, afterfallHipR - PoseLibrary.AfterfallHipTuckDeg * tuck,
            afterfallKneeR + PoseLibrary.AfterfallKneeTuckDeg * tuck, true,
            afterfallAnkleR + PoseLibrary.AfterfallAnkleTuckDeg * tuck);
    }

    // ─── get-up ladder and hand grip ───
    // Fallen + walk key held: Plant/Press/Tuck/Rise, each entered on a measured signal and time-bounded
    // (a spent bound is a failure). Arms are asked via ArmDrive.RequestArmPress; grip acts in-attempt.

    // Palm grip engage/release thresholds on the palm's measured normal load, in body weights.
    private const float GripEngageLoad = 0.25f;     // body weights on that palm
    private const float GripReleaseLoad = 0.05f;    // body weights
    // Anchored-slip budget, m: past it the palm's grip tears out (-poseHandGripSlip).
    private float handGripSlipMeters = 0.03f;
    private const float GripFootUnderBandM = 0.30f; // "the feet under the body"
    // Stage time bounds, s: settle + plant + press + tuck + rise = 9.75 s, inside the 10 s window.
    private const float GetUpSettleSeconds = 0.25f;
    private const float GetUpPlantSeconds = 2.0f;
    private const float GetUpPressSeconds = 2.5f;
    private const float GetUpTuckSeconds = 2.0f;
    private const float GetUpRiseSeconds = 3.0f;
    // Roll stage bound, s. The exit is the pose leaving the back; the bound only reports failure.
    // A rolling body needs a few seconds to cross ~100 deg; a shorter bound cuts the roll short.
    private const float GetUpRollSeconds = 3.0f;
    // A spent attempt leaves the body down; with the key still held, retry after this pause, s.
    private const float GetUpRetrySeconds = 2.0f;
    // IsDown's deep-drop term, m (get-up only): a sitting body drops its pelvis past this without
    // tripping the tilt gate; a crouch stays well under it.
    private const float GetUpSittingDropMeters = 0.50f;
    // Tuck draw doses, deg, at full draw. Conventions: hip minus = flexion, knee plus = flexion,
    // ankle plus = toe down. Joint limits bound them via ClampLeg/ActuatorMixer.
    private const float GetUpHipDrawDeg = 45f;
    private const float GetUpKneeDrawDeg = 55f;
    private const float GetUpAnkleDrawDeg = 20f;
    // Trunk world-tilt targets, deg (minus = forward): over the hands (Press), through the Tuck,
    // to nearly upright (Rise). Only the lumbar law chases these.
    private const float GetUpTrunkPressDeg = -45f;
    private const float GetUpTrunkTuckDeg = -40f;
    private const float GetUpTrunkRiseDeg = -10f;

    private bool GetUpActive()
    {
        return getUpEnabled && !getUpDone
            && (getUpStage != GetUpStage.Idle || getUpDriveOn);
    }

    // ─── fall guard ───
    // Called from Tick before the down test. Owns only ArmDrive's fallGuardReach and the press request
    // and never returns early: the walk's leg drive keeps ticking underneath it.
    private void TickFallGuard(float tilt, float pelvisY, float pelvisVelY)
    {
        // Lazy lookup: Human.cs adds ArmDrive after this component, so the Initialize-time lookup is null.
        if (armDrive == null) armDrive = GetComponent<ArmDrive>();
        float dt = Time.fixedDeltaTime;
        if (dt <= 0f) dt = 0.005f;
        int state = balance != null ? balance.GeometricFallState : BodySupportGeometry.Supported;
        // Arm the guard only when both agree: the support state says the body left its support and the
        // pelvis descent rate says it is really going down, not wobbling over a bump.
        bool goingOver = state >= BodySupportGeometry.Critical
            && pelvisVelY <= FallGuardDescendMetersPerSecond;
        fallGuardStateAge = goingOver ? fallGuardStateAge + dt : 0f;

        if (fallGuardLive && (!goingOver || IsDown(tilt, pelvisY))) EndFallGuard();
        if (!fallGuardLive && goingOver && fallGuardStateAge >= fallGuardHoldSeconds)
        {
            fallGuardLive = true;
            fallGuardFacing = intent != null && Mathf.Abs(intent.moveX) > 0.01f
                ? Mathf.Sign(intent.moveX)
                : 1f;
            if (armDrive != null)
                armDrive.fallGuardReach = fallGuardFacing * fallGuardReachMeters;
        }
        if (fallGuardLive && armDrive != null) armDrive.RequestArmPress(FallGuardPressLevel);
    }

    // Disarm: fallGuardReach 0 is ArmDrive's default and means off (the normal press latch and stroke).
    private void EndFallGuard()
    {
        fallGuardLive = false;
        fallGuardStateAge = 0f;
        if (armDrive != null) armDrive.fallGuardReach = 0f;
    }

    private void TickGetUp(float tilt, float pelvisTilt, float pelvisAngVel, float pelvisY,
                           float pelvisVelY, float comX, float comVelX,
                           bool leftDown, bool rightDown,
                           float leftFootX, float rightFootX, float leftFootY, float rightFootY)
    {
        float dt = Time.fixedDeltaTime;
        if (dt <= 0f) dt = 0.005f;
        float now = Time.fixedTime;
        HipDbgFrame("pre");   // ROUND 36 (probe): the entry state
        // Resolved lazily: Human.cs adds this component before ArmDrive, so Initialize-time lookups miss
        // ArmDrive and StepPhaseDriver.
        if (armDrive == null) armDrive = GetComponent<ArmDrive>();
        if (stepDriver == null) stepDriver = GetComponent<StepPhaseDriver>();
        // Back tracker: the roll dwell's clock; leaving the back re-arms the roll cap.
        UpdateBackTracker(now);
        bool down = IsDown(tilt, pelvisY);
        // Auto rise: with -autoGetUp D, a body down for D s without the key counts as the key held.
        bool walkKey = GetUpKeyOn(down);
        float comY = comCalculator != null ? comCalculator.GetCenterOfMass().y : 0f;
        float standComY = stepDriver != null && stepDriver.HasStandingComY
            ? stepDriver.StandingComY : float.NaN;
        float pelvisH = pelvisY - groundY;
        float standH = standPelvisY - groundY;

        // ─── stand bars ───
        // Standing = torso tilt > getUpStandTiltDeg, CoM y + 0.05 m >= StandingComY (the bar
        // GetUpComAtStandHeight uses), and both feet grounded within GripFootUnderBandM of the CoM x.
        bool tiltBar = tilt > getUpStandTiltDeg;
        bool comBar = !float.IsNaN(standComY) && comY + 0.05f >= standComY;
        bool leftUnder = Mathf.Abs(leftFootX - comX) <= GripFootUnderBandM;
        bool rightUnder = Mathf.Abs(rightFootX - comX) <= GripFootUnderBandM;
        bool feetBar = leftUnder && rightUnder && leftDown && rightDown;
        bool standing = tiltBar && comBar && feetBar;
        // Per-frame readings for the sit seat machine and the trace.
        seatGapL = comX - leftFootX;
        seatGapR = comX - rightFootX;
        seatBodyTilt = tilt;

        if (down && getUpDownSince < 0f)
        {
            getUpDownSince = now;
            if (getUpFallTime < 0f) getUpFallTime = now;
            if (balance != null && balance.getUpFallTime < 0f)
                balance.getUpFallTime = getUpFallTime;
            OpenGetUpTrace();
        }
        float downAge = getUpDownSince >= 0f ? now - getUpDownSince : 0f;

        // ─── no walk key, no rise ───
        if (!walkKey)
        {
            EndAttempt();
            if (balance != null)
            {
                balance.currentState = BalanceController.BalanceState.Falling;
                balance.locomotionState = BalanceController.LocomotionState.Fallen;
            }
            RelaxAll();
            WriteGetUpTrace(tilt, pelvisY, pelvisH, standH, comX, comY, standComY,
                leftFootX, rightFootX, leftFootY, rightFootY, leftDown, rightDown,
                leftUnder, rightUnder, tiltBar, comBar, feetBar, 0f);
            return;
        }

        // ─── fall frames, then the first stage ───
        if (getUpStage == GetUpStage.Idle)
        {
            // Sit-stand fire: once the sit window held -poseGetUpSitStandHold s, go straight to the Rise with
            // the pose latched as it sits (no settle/plant/tuck). The retry pause gates only the down route.
            if (getUpSitStandOn && getUpSitStandT >= getUpSitStandHold
                && SitStandWindow(tilt, leftDown, rightDown, pelvisY, pelvisVelY))
            {
                BeginSitAttempt(tilt, now, pelvisY);
            }
            else if (downAge < GetUpSettleSeconds || now < getUpRetryAfter)
            {
                // Settle: the fall's own frames read Falling; a body waiting out the retry pause reads Fallen.
                if (balance != null)
                {
                    balance.currentState = BalanceController.BalanceState.Falling;
                    balance.locomotionState = downAge < GetUpSettleSeconds
                        ? BalanceController.LocomotionState.Falling
                        : BalanceController.LocomotionState.Fallen;
                }
                RelaxAll();
                // Between attempts the afterfall crawl keeps the legs moving so the presses find purchase.
                DriveAfterfallCrawl();
                WriteGetUpTrace(tilt, pelvisY, pelvisH, standH, comX, comY, standComY,
                    leftFootX, rightFootX, leftFootY, rightFootY, leftDown, rightDown,
                    leftUnder, rightUnder, tiltBar, comBar, feetBar, 0f);
                return;
            }
            else
            {
                BeginAttempt(tilt, now);
            }
        }

        // ─── roll re-entry ───
        // Tossed onto the back mid-ladder: Plant/Press may not progress while supine, so the ladder
        // returns to the Roll and the plant must be re-earned off the back.
        if (getUpRollEnabled && !getUpRollBlocked && getUpStage != GetUpStage.Roll
            && BodyOnBack() && now - getUpBackSince >= getUpRollDwell)
        {
            GetUpStage wasStage = getUpStage;
            getUpTrunkStartDeg = tilt;      // the ramp starts from where the body is now
            getUpHandsPlanted = false;      // the plant is re-earned after the roll
            SetGetUpStage(GetUpStage.Roll, now);
            Debug.Log("GETUP-ROLL enter t=" + N(now, 3) + " tilt=" + N(tilt, 2)
                + " from=" + wasStage + " heldFor=" + N(now - getUpBackSince, 2)
                + " why=flip");
        }

        float stageAge = now - getUpStageEnterTime;
        bool planted = armDrive != null && armDrive.HandContactL && armDrive.HandContactR;
        if (planted) getUpHandsPlanted = true;
        if (feetBar) getUpReachedTuck = true;

        // ─── base settle clock ───
        // The fast-path fire needs the base state unbroken with a quiet pelvis vertical speed for
        // -poseGetUpBaseSettle s: a fire mid-motion turns an impact into a bounce. Refusals are logged.
        bool wouldFire = planted && feetBar && !getUpBaseFast
            && (getUpStage == GetUpStage.Press || getUpStage == GetUpStage.Tuck);
        bool settleOk = true;
        if (getUpBaseSettleOn && getUpBaseOn)
        {
            bool settleQuiet = wouldFire && Mathf.Abs(pelvisVelY) <= getUpBaseSettleVel;
            if (getUpBaseSettleDutyWin > 0f)
            {
                // Duty form: quiet fraction over the last W s; a ring keeps (t, quiet s) per frame, evicted past W.
                int cap = GetUpSettleDutyCap;
                if (getUpSettleDutyCount == cap)
                    getUpSettleDutySum -= getUpSettleDutyRingQ[getUpSettleDutyHead];
                else
                    getUpSettleDutyCount++;
                getUpSettleDutyRingT[getUpSettleDutyHead] = now;
                getUpSettleDutyRingQ[getUpSettleDutyHead] = settleQuiet ? dt : 0f;
                getUpSettleDutySum += getUpSettleDutyRingQ[getUpSettleDutyHead];
                getUpSettleDutyHead = (getUpSettleDutyHead + 1) % cap;
                int tail = (getUpSettleDutyHead - getUpSettleDutyCount + cap) % cap;
                while (getUpSettleDutyCount > 1
                    && now - getUpSettleDutyRingT[tail] > getUpBaseSettleDutyWin)
                {
                    getUpSettleDutySum -= getUpSettleDutyRingQ[tail];
                    tail = (tail + 1) % cap;
                    getUpSettleDutyCount--;
                }
                float dutSpan = Mathf.Max(now - getUpSettleDutyRingT[tail], 0.0001f);
                getUpBaseSettleDuty = Mathf.Clamp01(getUpSettleDutySum / dutSpan);
                if (getUpSettleDutyAge < getUpBaseSettleDutyWin) getUpSettleDutyAge += dt;
                settleOk = settleQuiet && getUpSettleDutyAge >= getUpBaseSettleDutyWin
                    && getUpBaseSettleDuty >= getUpBaseSettleDutyFrac;
            }
            else
            {
                // Debounce: a dropout <= Drop s does not reset the settle clock.
                if (settleQuiet) { getUpBaseSettleGap = 0f; getUpBaseSettleT += dt; }
                else
                {
                    getUpBaseSettleGap += dt;
                    if (getUpBaseSettleDrop > 0f
                        && getUpBaseSettleGap <= getUpBaseSettleDrop)
                        getUpBaseSettleT += dt;
                    else getUpBaseSettleT = 0f;
                }
                settleOk = getUpBaseSettleT >= getUpBaseSettleSec;
            }
            if (wouldFire && !settleOk && !getUpBaseSettleWarned)
            {
                getUpBaseSettleWarned = true;
                getUpBaseSettleBlocks++;
                Debug.Log("GETUP-BASE settle-block t=" + N(now, 3)
                    + " v=" + N(pelvisVelY, 2) + " dwell=" + N(getUpBaseSettleT, 2)
                    + " duty=" + N(getUpBaseSettleDuty, 2));
            }
            if (!wouldFire || settleOk) getUpBaseSettleWarned = false;
        }

        // ─── base load gate ───
        // Pose flags can read a base on unloaded soles: the fire waits for both soles >= BaseLoad x weight
        // for BaseLoadSec s (dropouts <= BaseLoadDrop s kept). Refusals log live fz. 0 = off (loadOk true).
        bool loadOk = true;
        if (getUpBaseOn && getUpBaseLoadFrac > 0f)
        {
            float loadThr = getUpBaseLoadFrac * PoseRigWeightN();
            bool bothLoaded = poseGroundFzLeftN >= loadThr && poseGroundFzRightN >= loadThr;
            if (bothLoaded) { getUpBaseLoadGap = 0f; getUpBaseLoadT += dt; }
            else
            {
                getUpBaseLoadGap += dt;
                if (getUpBaseLoadDrop > 0f && getUpBaseLoadGap <= getUpBaseLoadDrop)
                    getUpBaseLoadT += dt;
                else getUpBaseLoadT = 0f;
            }
            loadOk = getUpBaseLoadT >= getUpBaseLoadSec;
            if (wouldFire && !loadOk && !getUpBaseLoadWarned)
            {
                getUpBaseLoadWarned = true;
                getUpBaseLoadBlocks++;
                Debug.Log("GETUP-BASE load-block t=" + N(now, 3)
                    + " fzL=" + N(poseGroundFzLeftN, 0) + " fzR=" + N(poseGroundFzRightN, 0)
                    + " thr=" + N(loadThr, 0) + " dwell=" + N(getUpBaseLoadT, 2));
            }
            if (!wouldFire || loadOk) getUpBaseLoadWarned = false;
        }

        // ─── loaded-window extension ───
        // Tracks a Tuck window with both soles at the keep load; while live, the wait stops re-folding
        // (which would unload them). The Rise fires once the soles' sum holds the fire floor. Default off.
        bool loadExtFireReady = false;
        if (getUpBaseOn && getUpLoadExtOn && getUpStage == GetUpStage.Tuck)
        {
            float keepThr = getUpLoadExtKeep * PoseRigWeightN();
            float fireThr = getUpLoadExtFire * PoseRigWeightN();
            bool bothKeep = poseGroundFzLeftN >= keepThr && poseGroundFzRightN >= keepThr;
            bool sumHot = (poseGroundFzLeftN + poseGroundFzRightN) >= fireThr;
            if (bothKeep)
            {
                if (!getUpLoadExtLive)
                {
                    getUpLoadExtWindows++;
                    Debug.Log("GETUP-LOADEXT enter t=" + N(now, 3) + " n=" + getUpLoadExtWindows
                        + " fzL=" + N(poseGroundFzLeftN, 0) + " fzR=" + N(poseGroundFzRightN, 0)
                        + " keep=" + N(keepThr, 0) + " fire=" + N(fireThr, 0));
                }
                getUpLoadExtLive = true;
            }
            else if (getUpLoadExtLive)
            {
                getUpLoadExtLive = false;
                Debug.Log("GETUP-LOADEXT exit t=" + N(now, 3)
                    + " fzL=" + N(poseGroundFzLeftN, 0) + " fzR=" + N(poseGroundFzRightN, 0)
                    + " held=" + N(getUpLoadExtT, 2));
            }
            if (bothKeep && sumHot) { getUpLoadExtGap = 0f; getUpLoadExtT += dt; }
            else if (getUpLoadExtT > 0f)
            {
                getUpLoadExtGap += dt;
                if (getUpLoadExtDrop > 0f && getUpLoadExtGap <= getUpLoadExtDrop)
                    getUpLoadExtT += dt;
                else getUpLoadExtT = 0f;
            }
            loadExtFireReady = getUpLoadExtT >= getUpLoadExtSec;
        }
        else
        {
            getUpLoadExtLive = false;
        }
        // ─── loaded-window fire ───
        // The Rise fires on the loaded window itself, not the pose flags; the fire state is logged.
        if (getUpBaseOn && getUpLoadExtOn && !getUpBaseFast && loadExtFireReady
            && getUpStage == GetUpStage.Tuck)
        {
            getUpBaseFast = true;
            getUpLoadExtFires++;
            Debug.Log("GETUP-BASE fastpath t=" + N(now, 3) + " from=Tuck via=loadext held="
                + N(getUpLoadExtT, 2) + " fzL=" + N(poseGroundFzLeftN, 0) + " fzR="
                + N(poseGroundFzRightN, 0) + " pl=" + (getUpHandsPlanted ? "1" : "0")
                + " fb=" + (feetBar ? "1" : "0") + " comX=" + N(comX, 3));
            getUpPushTraceT0 = now;
            // Re-latch the hold pose at the fire: the attempt's latch is the near-straight landing pose, and
            // holding it would yank the folded Tuck straight at full torque (a launch). Hold from the fold.
            if (getUpLoadExtRelatch)
            {
                getUpHipL = leftHipJoint != null ? leftHipJoint.jointAngle : getUpHipL;
                getUpHipR = rightHipJoint != null ? rightHipJoint.jointAngle : getUpHipR;
                getUpKneeL = leftKneeJoint != null ? leftKneeJoint.jointAngle : getUpKneeL;
                getUpKneeR = rightKneeJoint != null ? rightKneeJoint.jointAngle : getUpKneeR;
                getUpAnkleL = leftAnkleJoint != null ? leftAnkleJoint.jointAngle : getUpAnkleL;
                getUpAnkleR = rightAnkleJoint != null ? rightAnkleJoint.jointAngle : getUpAnkleR;
                Debug.Log("GETUP-LOADEXT relatch t=" + N(now, 3)
                    + " hipL=" + N(getUpHipL, 1) + " kneeL=" + N(getUpKneeL, 1)
                    + " ankleL=" + N(getUpAnkleL, 1) + " hipR=" + N(getUpHipR, 1));
            }
            SetGetUpStage(GetUpStage.Rise, now);
            stageAge = 0f;
        }

        // ─── base fast-path ───
        // Feet under and grounded with the palms planted (and settle/load gates passed): start the Rise
        // from Press or Tuck at once, before the stage bounds carry the body off its base.
        if (getUpBaseOn && !getUpBaseFast && getUpHandsPlanted && feetBar && settleOk && loadOk
            && (getUpStage == GetUpStage.Press || getUpStage == GetUpStage.Tuck))
        {
            getUpBaseFast = true;
            Debug.Log("GETUP-BASE fastpath t=" + N(now, 3) + " from=" + getUpStage
                + " pelvH=" + N(pelvisH, 3) + " comX=" + N(comX, 3));
            getUpPushTraceT0 = now;
            SetGetUpStage(GetUpStage.Rise, now);
            stageAge = 0f;
        }
        // ─── arm press ask ───
        // RunSitSeat publishes the seat phase/press. Ask 1.0 in plant/press, 0.6 in the rise (palms must
        // not shove the body); base wait/gate hold use -poseGetUpBasePress (default 0: strokes lift feet).
        RunSitSeat(now, dt);
        float pressAsk = getUpStage == GetUpStage.Rise ? 0.6f : 1f;
        bool baseWaiting = getUpBaseOn && getUpStage == GetUpStage.Tuck
            && stageAge >= GetUpTuckSeconds * getUpPatience;
        // -poseGetUpBaseRise 1: the press hold covers the whole Rise, not only the contact-gate holds.
        bool gateHolding = getUpBaseOn && getUpStage == GetUpStage.Rise
            && (getUpBaseRise || (getUpStandGate && getUpStandGateInHold));
        if (baseWaiting || gateHolding)
            pressAsk = getUpBasePress;
        // ─── wait-press lift ───
        // During the wait/gate hold, with -poseGetUpWaitPress >= 0 (negative = off), the press ramps 0 ->
        // that level over the ramp seconds, restarting per wait episode; fz and pose are logged each 1 s.
        if (getUpWaitPress >= 0f && (baseWaiting || gateHolding))
        {
            if (getUpWaitPressT <= 0f)
            {
                Debug.Log("GETUP-WAITPRESS enter t=" + N(now, 3) + " level=" + N(getUpWaitPress, 2)
                    + " ramp=" + N(getUpWaitPressSec, 2) + " pelvH=" + N(pelvisH, 3)
                    + " fzL=" + N(poseGroundFzLeftN, 0) + " fzR=" + N(poseGroundFzRightN, 0));
                getUpWaitPressLogT = 1f;
            }
            getUpWaitPressT += dt;
            pressAsk = getUpWaitPress * Mathf.Clamp01(getUpWaitPressT / Mathf.Max(0.05f, getUpWaitPressSec));
            if (getUpWaitPressT >= getUpWaitPressLogT)
            {
                getUpWaitPressLogT = getUpWaitPressT + 1f;
                Debug.Log("GETUP-WAITPRESS t=" + N(now, 3) + " s=" + N(getUpWaitPressT, 2)
                    + " press=" + N(pressAsk, 2) + " pelvH=" + N(pelvisH, 3)
                    + " fzL=" + N(poseGroundFzLeftN, 0) + " fzR=" + N(poseGroundFzRightN, 0)
                    + " dn=" + (leftDown ? "1" : "0") + (rightDown ? "1" : "0"));
            }
        }
        else
        {
            getUpWaitPressT = 0f;
            getUpWaitPressLogT = 0f;
        }
        // Press lockout: with load-ext armed, the wait's press is forced to 0 (hard gate) and the legs own
        // the lift, since a press dose during the wait breaks the base. Inert when off.
        if (getUpLoadExtOn && (baseWaiting || gateHolding)) pressAsk = 0f;
        // While a seat attempt runs its machine owns the press ask; seatPressNow < 0 keeps the default ask.
        if (seatPhase >= 0 && seatPressNow >= 0f)
            pressAsk = seatPressNow;
        // Plant reach ahead of the shoulder, along the move input (else the fall's facing). Set for the
        // whole get-up, not only while the ask is live, so a latch taken during the load-ext lockout
        // lands ahead too. Cleared by EndGetUp; 0 = off.
        if (armDrive != null)
        {
            float face = intent != null && Mathf.Abs(intent.moveX) > 0.01f
                ? Mathf.Sign(intent.moveX)
                : fallGuardFacing;
            armDrive.getUpPlantReach = getUpPlantReachMeters > 0f
                ? face * getUpPlantReachMeters
                : 0f;
            armDrive.RequestArmPress(pressAsk);
        }

        // ─── palm grip: engage / hold / release / tear ───
        SampleHandGrip(dt);

        // ─── stages ───
        switch (getUpStage)
        {
            case GetUpStage.Roll:
                // Roll: on the back, rotate off it first - knees draw, trunk ramps toward the press target, and
                // ArmDrive won't latch a plant while on the back. Exit on the pose; the bound reports failure.
                DriveGetUpLegs(Mathf.Clamp01(0.5f * stageAge / (1.0f * getUpPatience)));
                DriveGetUpTrunk(getUpTrunkStartDeg, GetUpTrunkPressDeg,
                    Mathf.Clamp01(stageAge / (1.2f * getUpPatience)));
                if (!(getUpRollForce > 0.5f) && !BodyOnBack())
                {
                    getUpRollBounds = 0;        // a crossing clears the cap
                    Debug.Log("GETUP-ROLL out t=" + N(now, 3) + " reason=cross");
                    SetGetUpStage(GetUpStage.Plant, now);
                }
                else if (stageAge >= GetUpRollSeconds * getUpPatience)
                {
                    getUpRollBounds++;
                    if (getUpRollBounds >= getUpRollCap)
                        getUpRollBlocked = true;
                    Debug.Log("GETUP-ROLL out t=" + N(now, 3) + " reason=bound bounds="
                        + getUpRollBounds + " tilt=" + N(tilt, 2));
                    SetGetUpStage(GetUpStage.Plant, now);
                }
                break;

            case GetUpStage.Plant:
                // Plant: arms plant (ArmDrive) while the legs begin a slow tuck; exit on the palms' real contact.
                DriveGetUpLegs(Mathf.Clamp01(0.35f * stageAge / (1.5f * getUpPatience)));
                DriveGetUpTrunk(getUpTrunkStartDeg, GetUpTrunkPressDeg, Mathf.Clamp01(stageAge / (1.5f * getUpPatience)));
                if (getUpHandsPlanted || stageAge >= GetUpPlantSeconds * getUpPatience)
                    SetGetUpStage(GetUpStage.Press, now);
                break;

            case GetUpStage.Press:
                // Press: the trunk comes up over the planted hands; exit at pelvis height >= 0.45 x stand height.
                DriveGetUpLegs(Mathf.Clamp01(0.35f + 0.65f * stageAge / (GetUpPressSeconds * getUpPatience)));
                DriveGetUpTrunk(GetUpTrunkPressDeg, GetUpTrunkTuckDeg, Mathf.Clamp01(stageAge / (GetUpPressSeconds * getUpPatience)));
                if (getUpHandsPlanted && pelvisH >= 0.45f * standH)
                    SetGetUpStage(GetUpStage.Tuck, now);
                else if (stageAge >= GetUpPressSeconds * getUpPatience)
                    SetGetUpStage(GetUpStage.Tuck, now);   // the bound is a failure, reported
                break;

            case GetUpStage.Tuck:
                // Tuck: the hips become the trunk's lever (pelvis-upright law) so the pelvis rotates up over the
                // knees, and the legs unwind from the press depth over the stage bound to set the soles under the
                // body and on the slab (a held fold rolls the body over its head with the feet in the air).
                if (getUpBaseOn)
                {
                    // Base gate: past its bound the Tuck waits for the base state, re-seating the feet at
                    // -poseGetUpBaseDraw; hitting the cap aborts the attempt rather than rising off the base.
                    if (stageAge < GetUpTuckSeconds * getUpPatience)
                        DriveGetUpLegs(Mathf.Clamp01(1f - stageAge / (GetUpTuckSeconds * getUpPatience)));
                    else
                    {
                        getUpBaseWait += dt;
                        // While a loaded window is live the wait stops re-folding: the legs hold draw 0 so the soles keep
                        // the load the load-ext fire needs.
                        bool loadExtHold = getUpLoadExtOn && getUpLoadExtLive;
                        if (loadExtHold)
                            DriveGetUpLegs(0f);
                        else if (getUpBaseSquatDeg > 0f)
                            DriveGetUpLegsSquat(getUpBaseDraw);
                        else
                            DriveGetUpLegs(getUpBaseDraw);
                        if (getUpBaseWait >= getUpBaseMax)
                        {
                            getUpBaseAborts++;
                            Debug.Log("GETUP-BASE abort t=" + N(now, 3)
                                + " wait=" + N(getUpBaseWait, 2) + " pelvH=" + N(pelvisH, 3));
                            FinishAttempt(false, now);
                            WriteGetUpTrace(tilt, pelvisY, pelvisH, standH, comX, comY, standComY,
                                leftFootX, rightFootX, leftFootY, rightFootY, leftDown, rightDown,
                                leftUnder, rightUnder, tiltBar, comBar, feetBar, stageAge);
                            return;
                        }
                    }
                    DriveGetUpTrunk(GetUpTrunkTuckDeg, GetUpTrunkTuckDeg, 1f);
                    DriveHipsGetUp(pelvisTilt, pelvisAngVel);
                    if (feetBar && getUpHandsPlanted && settleOk && loadOk)
                        SetGetUpStage(GetUpStage.Rise, now);
                }
                else
                {
                    DriveGetUpLegs(Mathf.Clamp01(1f - stageAge / (GetUpTuckSeconds * getUpPatience)));
                    DriveGetUpTrunk(GetUpTrunkTuckDeg, GetUpTrunkTuckDeg, 1f);
                    DriveHipsGetUp(pelvisTilt, pelvisAngVel);
                    if ((feetBar && getUpHandsPlanted) || stageAge >= GetUpTuckSeconds * getUpPatience)
                        SetGetUpStage(GetUpStage.Rise, now);
                }
                break;

            case GetUpStage.Rise:
                // Rise: the legs extend under the climbing pelvis, the trunk is asked nearly upright and the
                // pelvis law keeps the hips working. The three stand bars end the ladder.
                float riseClock = stageAge;           // ROUND 30: the bound's own clock
                float riseShare = Mathf.Clamp01(stageAge / (GetUpRiseSeconds * getUpPatience));
                float trunkGovBiasNow = 0f;           // ROUND 42: the trunk governor's bias
                // ─── sit-stand extension ───
                // For a sit-fired attempt the Rise bound is deferred while the sit window holds (capped at
                // -poseGetUpSitStandMax s); when the window ends the normal bound resumes.
                if (getUpSitStandOn && getUpSitActive)
                {
                    // The blend clock stays raw (it runs over the live window); only the bound defers (see below).
                    bool sitWinLive = SitStandWindow(tilt, leftDown, rightDown, pelvisY, pelvisVelY);
                    getUpSitWinNow = sitWinLive;   // the trace reads the TRUE window
                    if (sitWinLive && getUpSitStandExt < getUpSitStandMax)
                        getUpSitStandExt += dt;
                    if (balance != null && getUpSitStandExt > balance.getUpSitExtMax)
                        balance.getUpSitExtMax = getUpSitStandExt;
                }
                // Arc clock: counts only while the extension is live (pull done) and the torso pitch rate is in
                // the gate; otherwise it freezes so the leg blend holds and the ask cannot feed its own runaway.
                getUpTracePelvVel = pelvisVelY;
                if (getUpSitActive && getUpSitArcSec > 0.001f)
                {
                    float arcInst = (tilt - getUpSitArcPrevTilt) / Mathf.Max(1e-5f, dt);
                    getUpSitArcPrevTilt = tilt;
                    getUpSitArcTiltRate += (arcInst - getUpSitArcTiltRate) * Mathf.Clamp01(20f * dt);
                    bool arcLive = getUpSitPullKey >= 0.999f;
                    bool arcCalm = getUpSitArcRate <= 0f
                        || Mathf.Abs(getUpSitArcTiltRate) <= getUpSitArcRate;
                    // Rock gate: when armed, the extension clock waits for the torso's rock to complete, so the legs
                    // stay folded while the torso sits up. Off (or no rock): no extra condition.
                    bool rockReady = getUpSitRockGate < 0.5f
                        || getUpSitRockSec <= 0.001f
                        || getUpSitRockClock >= getUpSitRockSec;
                    // CoM-over gate: when armed, the extension clock also waits until no foot is ahead of the CoM by
                    // more than the band, so the fold holds while the body is behind its support. 0 = off.
                    bool extOverReady = true;
                    if (getUpSitExtOver > 0.001f)
                        extOverReady = Mathf.Min(seatGapL, seatGapR) >= -getUpSitExtOver;
                    if (arcLive && arcCalm && rockReady && extOverReady)
                    {
                        if (getUpSitArcClock < 30f) getUpSitArcClock += dt;
                        getUpSitArcInHold = false;
                    }
                    else if (arcLive)
                    {
                        getUpSitArcHeld += dt;
                        if (!getUpSitArcInHold)
                        {
                            getUpSitArcInHold = true;
                            getUpSitArcHolds++;
                        }
                    }
                }
                if (getUpStandMode)
                {
                    // Stand mode: hold the fold (or the latched pose) first, then blend the legs to the stand pose.
                    // Contact gate: the rise clock holds while the soles are not both grounded, so the extension only
                    // advances over planted feet; capped by -poseGetUpStandGateMax. Off: riseClock == stageAge.
                    if (getUpStandGate)
                    {
                        if (!(leftDown && rightDown) && getUpStandGateHeld < getUpStandGateMax)
                        {
                            getUpStandGateHeld = Mathf.Min(getUpStandGateMax, getUpStandGateHeld + dt);
                            if (!getUpStandGateInHold)
                            {
                                getUpStandGateInHold = true;
                                getUpStandGateHolds++;
                            }
                        }
                        else if (leftDown && rightDown)
                        {
                            getUpStandGateInHold = false;
                        }
                        riseClock = Mathf.Max(0f, stageAge - getUpStandGateHeld);
                        riseShare = Mathf.Clamp01(riseClock / (GetUpRiseSeconds * getUpPatience));
                    }
                    // ─── move-guard ───
                    // The rise clock does not advance while a deck surface moves (TiltPlatform.AnyMoving): deck sweeps
                    // throw a half-risen body, so the blend runs over still windows. Hold capped by MoveGuardMax.
                    if (getUpMoveGuardOn)
                    {
                        bool deckMoving = TiltPlatform.AnyMoving();
                        if (deckMoving && getUpMoveGuardHeld < getUpMoveGuardMax)
                        {
                            getUpMoveGuardHeld = Mathf.Min(getUpMoveGuardMax, getUpMoveGuardHeld + dt);
                            if (!getUpMoveGuardInHold)
                            {
                                getUpMoveGuardInHold = true;
                                getUpMoveGuardHolds++;
                            }
                        }
                        else if (!deckMoving)
                        {
                            getUpMoveGuardInHold = false;
                        }
                        riseClock = Mathf.Max(0f, riseClock - getUpMoveGuardHeld);
                        riseShare = Mathf.Clamp01(riseClock / (GetUpRiseSeconds * getUpPatience));
                    }
                    // ─── trunk governor ───
                    // Mode bit 1: hold the rise clock while |tilt| > GovDeg. Mode bit 2: bias the trunk target away
                    // from the deviation (minus = forward, same axis as tilt), proportional to the degrees past GovDeg.
                    if (getUpTrunkGovMode == 1f || getUpTrunkGovMode == 3f)
                    {
                        float govDev = Mathf.Abs(tilt);
                        if (govDev > getUpTrunkGovDeg && getUpTrunkGovHeld < getUpTrunkGovMax)
                        {
                            getUpTrunkGovHeld = Mathf.Min(getUpTrunkGovMax, getUpTrunkGovHeld + dt);
                            if (!getUpTrunkGovInHold)
                            {
                                getUpTrunkGovInHold = true;
                                getUpTrunkGovHolds++;
                            }
                        }
                        else if (govDev <= getUpTrunkGovDeg)
                        {
                            getUpTrunkGovInHold = false;
                        }
                        riseClock = Mathf.Max(0f, riseClock - getUpTrunkGovHeld);
                        riseShare = Mathf.Clamp01(riseClock / (GetUpRiseSeconds * getUpPatience));
                    }
                    if (getUpTrunkGovMode == 2f || getUpTrunkGovMode == 3f)
                    {
                        float govDev = Mathf.Abs(tilt);
                        float govExcess = govDev - getUpTrunkGovDeg;
                        if (govExcess > 0f)
                        {
                            trunkGovBiasNow = -Mathf.Sign(tilt) * getUpTrunkGovK * govExcess;
                            // Cap the counter-lean: |tilt| can pass 90 deg in a tumble and slam the trunk target. 0 = uncapped.
                            if (getUpTrunkGovCap > 0f)
                                trunkGovBiasNow = Mathf.Clamp(trunkGovBiasNow,
                                    -getUpTrunkGovCap, getUpTrunkGovCap);
                            float govMag = Mathf.Abs(trunkGovBiasNow);
                            if (govMag > getUpTrunkGovBias) getUpTrunkGovBias = govMag;
                        }
                    }
                    if (getUpStandKneeClimb > 0f && getUpRiseStartPelvisY < -999f)
                        getUpRiseStartPelvisY = pelvisY;   // ROUND 35: the climb datum
                    if (seatPhase >= 0)
                    {
                        // Seat route drives (the seat machine owns the phase; see DriveGetUpLegsSeat).
                        DriveGetUpLegsSeat();
                    }
                    else if (getUpSitPullOn && getUpSitActive)
                    {
                        // Sit heel-pull: the pull phase folds the knees under the body, then the extension blends from the
                        // pulled pose. The key rides raw stageAge: 0..1 = pull, 1..2 = extension (trace column).
                        float pullSecP = Mathf.Max(0.05f, getUpSitPullSec);
                        if (stageAge < pullSecP)
                        {
                            float pullP = Mathf.Clamp01(stageAge / pullSecP);
                            getUpSitPullKey = pullP;
                            if (!getUpSitPullLogged)
                            {
                                getUpSitPullLogged = true;
                                Debug.Log("GETUP-SITPULL enter t=" + N(now, 3)
                                    + " knee=" + N(getUpKneeL + getUpSitPullKnee, 1)
                                    + " hip=" + N(getUpHipL - getUpSitPullHip, 1)
                                    + " ankle=" + N(getUpAnkleL - getUpSitPullAnkle, 1)
                                    + " sec=" + N(pullSecP, 2));
                            }
                            DriveGetUpLegsSitPull(pullP);
                        }
                        else
                        {
                            float pullExt = Mathf.Clamp01((stageAge - pullSecP)
                                / Mathf.Max(0.1f, GetUpRiseSeconds * getUpPatience - pullSecP));
                            // Arc route: the extension key comes from the rate-gated arc clock.
                            if (getUpSitArcSec > 0.001f) pullExt = SitArcKey();
                            getUpSitPullKey = 1f + pullExt;
                            DriveGetUpLegsStandPulled(pullExt);
                        }
                    }
                    else if (riseShare < getUpStandHold)
                        DriveGetUpLegs(getUpStandFrom > 0 ? 0f : 1f);
                    else
                        DriveGetUpLegsStand(Mathf.InverseLerp(getUpStandHold, 1f, riseShare), pelvisY);
                }
                else
                {
                    DriveGetUpLegs(Mathf.Clamp01(1f - stageAge / (GetUpRiseSeconds * getUpPatience)));
                }
                // A sit-fired attempt starts upright, so its trunk blends from its own latched tilt; blending from
                // the Tuck's -40 deg would yank the torso forward at the fire and dive the body face-first.
                float trunkFromDeg = getUpSitActive ? getUpTrunkStartDeg : GetUpTrunkTuckDeg;
                float trunkShare = riseShare;
                // Height-tie arming (getUpSitArcTieExt): 0 = live from the attempt's first frame; 1 = only while
                // the extension is live (pull done, so press and pull keep their default asks); 2 = off.
                bool arcTieLive = getUpSitActive && getUpSitArcSec > 0.001f
                    && (getUpSitArcTieExt < 0.5f
                        || (getUpSitArcTieExt < 1.5f && getUpSitPullKey >= 0.999f));
                if (arcTieLive)
                {
                    // Torso tied to height: the base lean blends out as the pelvis climbs toward stand height; the
                    // base lean is clamped to +-ArcTorso.
                    getUpSitArcHShare = Mathf.Clamp01((pelvisY - getUpSitArcBasePelvY)
                        / Mathf.Max(0.05f, standH - getUpSitArcBasePelvY));
                    trunkFromDeg = Mathf.Clamp(getUpTrunkStartDeg,
                        -getUpSitArcTorso, getUpSitArcTorso);
                    // Sit-up rock: the height tie alone deadlocks when the pelvis never climbs (the trunk never lifts,
                    // the CoM never comes over the feet). The rock clock runs from the fire and the tie takes the
                    // larger share, so the torso curls up first, then the extension works. 0 = height tie only.
                    if (getUpSitRockSec > 0.001f)
                    {
                        if (getUpSitRockClock < 30f) getUpSitRockClock += dt;
                        trunkShare = Mathf.Max(getUpSitArcHShare,
                            Mathf.Clamp01(getUpSitRockClock / getUpSitRockSec));
                    }
                    else
                    {
                        trunkShare = getUpSitArcHShare;
                    }
                }
                else if (getUpSitArcTieExt > 0f)
                {
                    getUpSitArcHShare = 0f;   // the tie is not armed (trace)
                }
                float trunkToDeg = GetUpTrunkRiseDeg;
                // Optional trunk lean over the weight-transfer (shift) stage; lean 0 = off.
                if (Mathf.Abs(getUpSitShiftLean) > 0.001f && getUpSitShiftOn
                    && getUpSitActive && seatPhase == 1 && stepDone && !sitShiftDone)
                {
                    trunkFromDeg = getUpTrunkStartDeg;
                    trunkToDeg = getUpTrunkStartDeg + getUpSitShiftLean * sitShiftFrac;
                    trunkShare = 1f;
                }
                // Seated heel-draw: drawing the heels under a seated torso (start pitch <= DrawSeat) throws it
                // backward. Armed, a forward counter-lean scaled by the pull's progress keeps it up; the gate reads
                // the pitch latched at the pull's entry. 0 = off.
                if (getUpSitDrawLean > 0.001f && getUpSitActive && seatPhase == 1
                    && getUpSitDragTilt <= getUpSitDrawSeat)
                {
                    float drawP = Mathf.Clamp01(seatPullT / Mathf.Max(0.05f, getUpSitPullSec));
                    trunkGovBiasNow -= getUpSitDrawLean * drawP;
                    if (!getUpSitDrawLogged)
                    {
                        getUpSitDrawLogged = true;
                        Debug.Log("GETUP-SITDRAW hold t=" + N(now, 3)
                            + " H=" + N(getUpSitDrawLean, 1)
                            + " seat=" + N(getUpSitDragTilt, 1)
                            + " thr=" + N(getUpSitDrawSeat, 1));
                    }
                }
                DriveGetUpTrunk(trunkFromDeg, trunkToDeg, trunkShare, trunkGovBiasNow);
                DriveHipsGetUp(pelvisTilt, pelvisAngVel);
                if (standing)
                {
                    getUpStandNamed = true;
                    getUpDone = true;
                    if (balance != null)
                    {
                        balance.getUpSuccess = true;
                        balance.getUpSeconds = getUpFallTime >= 0f ? now - getUpFallTime : -1f;
                    }
                    FinishAttempt(true, now);
                    WriteGetUpTrace(tilt, pelvisY, pelvisH, standH, comX, comY, standComY,
                        leftFootX, rightFootX, leftFootY, rightFootY, leftDown, rightDown,
                        leftUnder, rightUnder, tiltBar, comBar, feetBar, stageAge);
                    return;
                }
                // Rise bound: the base bound plus the capped seconds each enabled sit/seat phase has held, so the
                // extension keeps working over held windows; with those off it is the plain bound.
                float riseBound = GetUpRiseSeconds * getUpPatience
                    + (getUpSitStandOn && getUpSitActive ? getUpSitStandExt : 0f)
                    + (seatPhase >= 0 ? Mathf.Min(seatFrozenT, getUpSitSeatFrozenMax) : 0f)
                    + (getUpSitDrawPlant > 0.001f
                        ? Mathf.Min(seatDrawPlantT, getUpSitDrawPlantMax
                            * (getUpSitDrawPlantRe > 0.001f ? getUpSitDrawPlantEps : 1f))
                        : 0f)
                    + (getUpSitStepOn ? Mathf.Min(stepT, getUpSitStepMax) : 0f)
                    + (getUpSitShiftOn ? Mathf.Min(sitShiftT, getUpSitShiftMax) : 0f)
                    + (getUpSitPlantOn ? Mathf.Min(sitPlantT, getUpSitPlantMax) : 0f)
                    + (getUpSitExtArrestOn ? Mathf.Min(seatExtArrestT, getUpSitExtArrestMax) : 0f);
                if (riseClock >= riseBound)
                {
                    FinishAttempt(false, now);
                    WriteGetUpTrace(tilt, pelvisY, pelvisH, standH, comX, comY, standComY,
                        leftFootX, rightFootX, leftFootY, rightFootY, leftDown, rightDown,
                        leftUnder, rightUnder, tiltBar, comBar, feetBar, stageAge);
                    return;
                }
                break;
        }

        // ─── ladder state for the recorder ───
        if (balance != null)
        {
            balance.locomotionState = BalanceController.LocomotionState.GettingUp;
            balance.currentState = BalanceController.BalanceState.Recovery;
            // Recorder's get-up pose ladder: 1 lying, 2 sitting, 3 knees tucked, 4 standing - the stage in
            // force, not a claim of success.
            balance.getUpPose = (int)getUpStage;
            if (balance.getUpPoseMax < balance.getUpPose)
                balance.getUpPoseMax = balance.getUpPose;
            balance.getUpGateHeld = getUpStandGateHeld;    // ROUND 30: the contact gate (log mirror)
            balance.getUpGateHolds = getUpStandGateHolds;
            balance.getUpBaseWait = getUpBaseWait;         // ROUND 31: the base gate (log mirror)
            balance.getUpBaseAborts = getUpBaseAborts;
            balance.getUpBaseSettle = getUpBaseSettleT;    // ROUND 32: the settle gate (log mirror)
            balance.getUpBaseSettleBlocks = getUpBaseSettleBlocks;
            balance.getUpBaseLoad = getUpBaseLoadT;        // ROUND 35: the load gate (log mirror)
            balance.getUpBaseLoadBlocks = getUpBaseLoadBlocks;
            balance.getUpArbSuppL = getUpArbSuppLTot;      // ROUND 37: the hip arbitration (log mirror)
            balance.getUpArbSuppR = getUpArbSuppRTot;
            balance.getUpMoveGuardHeld = getUpMoveGuardHeld;   // ROUND 42 (log mirror)
            balance.getUpMoveGuardHolds = getUpMoveGuardHolds;
            balance.getUpTrunkGovHeld = getUpTrunkGovHeld;
            balance.getUpTrunkGovHolds = getUpTrunkGovHolds;
            balance.getUpTrunkGovBias = getUpTrunkGovBias;
            balance.getUpSitExt = getUpSitStandExt;        // ROUND 43 (log mirror)
            balance.getUpSitFires = getUpSitStandFires;
            balance.getUpSitSeatFrozen = Mathf.Max(balance.getUpSitSeatFrozen, seatFrozenT);
            balance.getUpSitSeatPasses = Mathf.Max(balance.getUpSitSeatPasses, seatGatePasses);
        }
        getUpDriveOn = true;
        HipDbgFrame("post");  // ROUND 36 (probe): the ladder's exit state

        WriteGetUpTrace(tilt, pelvisY, pelvisH, standH, comX, comY, standComY,
            leftFootX, rightFootX, leftFootY, rightFootY, leftDown, rightDown,
            leftUnder, rightUnder, tiltBar, comBar, feetBar, stageAge);
        PushTraceFrame(now, tilt);
    }
}
