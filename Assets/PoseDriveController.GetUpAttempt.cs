using System.Globalization;
using UnityEngine;

// PoseDriveController: get-up attempts, the hand grip sampling and the get-up trace.
public partial class PoseDriveController
{
    // Roll entry test: on the back per PoseOrientation.LyingBack; a missing/invalid core reads false.
    private bool BodyOnBack()
    {
        if (!getUpOrientTried)
        {
            getUpOrientTried = true;
            getUpOrient = GetComponent<PoseOrientation>();
        }
        return getUpOrient != null && getUpOrient.Valid && getUpOrient.LyingBack;
    }

    // Back tracker: the dwell's clock; leaving the back re-arms the roll cap so a later back episode
    // gets its own rolls.
    private void UpdateBackTracker(float now)
    {
        if (BodyOnBack())
        {
            if (getUpBackSince < 0f) getUpBackSince = now;
        }
        else
        {
            getUpBackSince = -1f;
            getUpRollBlocked = false;
            getUpRollBounds = 0;
        }
    }

    private void BeginAttempt(float tilt, float now)
    {
        getUpRollBounds = 0;                    // v2: the counters are per attempt
        getUpRollBlocked = false;
        getUpArbSuppL = 0;                      // ROUND 37: the arbitration counters
        getUpArbSuppR = 0;
        getUpArbFirstL = false;
        getUpArbFirstR = false;
        bool rollStart = getUpRollEnabled
            && (getUpRollForce > 0.5f
                || (BodyOnBack() && now - getUpBackSince >= getUpRollDwell));
        getUpStage = rollStart ? GetUpStage.Roll : GetUpStage.Plant;
        getUpStageEnterTime = now;
        getUpDone = false;
        getUpHandsPlanted = false;
        getUpReachedTuck = false;
        getUpStandNamed = false;
        getUpStandGateHeld = 0f;             // ROUND 30: the gate's counters are per attempt
        getUpStandGateHolds = 0;
        getUpStandGateInHold = false;
        getUpBaseWait = 0f;                  // ROUND 31: the base gate's attempt clocks
        getUpBaseFast = false;
        getUpBaseSettleT = 0f;               // ROUND 32: the settle dwell is per attempt
        getUpBaseSettleWarned = false;
        getUpBaseSettleGap = 0f;             // ROUND 33: the anti-flicker clocks
        getUpBaseLoadT = 0f;                 // ROUND 35: the load gate's clocks
        getUpBaseLoadGap = 0f;
        getUpBaseLoadWarned = false;
        getUpLoadExtT = 0f;                  // ROUND 39: the loaded-window clocks
        getUpLoadExtGap = 0f;
        getUpLoadExtLive = false;
        getUpMoveGuardHeld = 0f;             // ROUND 42: the move-guard's attempt clocks
        getUpMoveGuardHolds = 0;
        getUpMoveGuardInHold = false;
        getUpTrunkGovHeld = 0f;              // ROUND 42: the trunk governor's attempt clocks
        getUpTrunkGovHolds = 0;
        getUpTrunkGovInHold = false;
        getUpTrunkGovBias = 0f;
        getUpRiseStartPelvisY = -999f;       // ROUND 35: the climb datum is per attempt
        getUpBaseSettleDuty = 0f;
        getUpSettleDutyHead = 0;
        getUpSettleDutyCount = 0;
        getUpSettleDutySum = 0f;
        getUpSettleDutyAge = 0f;
        getUpDriveOn = true;
        getUpSitActive = false;      // ROUND 43b: a down-route attempt is never a sit one
        // Latch the settle pose, as the afterfall crawl does.
        getUpHipL = leftHipJoint != null ? leftHipJoint.jointAngle : 0f;
        getUpHipR = rightHipJoint != null ? rightHipJoint.jointAngle : 0f;
        getUpKneeL = leftKneeJoint != null ? leftKneeJoint.jointAngle : 0f;
        getUpKneeR = rightKneeJoint != null ? rightKneeJoint.jointAngle : 0f;
        getUpAnkleL = leftAnkleJoint != null ? leftAnkleJoint.jointAngle : 0f;
        getUpAnkleR = rightAnkleJoint != null ? rightAnkleJoint.jointAngle : 0f;
        getUpTrunkStartDeg = tilt;
        afterfallCrawlOn = false;
        if (rollStart)
            Debug.Log("GETUP-ROLL enter t=" + N(now, 3) + " tilt=" + N(tilt, 2)
                + " why=" + (getUpRollForce > 0.5f ? "force" : "start"));
        if (balance != null)
        {
            balance.getUpAttempts++;
            balance.getUpPose = 1;
            if (balance.getUpPoseMax < 1) balance.getUpPoseMax = 1;
            balance.locomotionState = BalanceController.LocomotionState.GettingUp;
            balance.currentState = BalanceController.BalanceState.Recovery;
        }
    }

    // Sit-fired attempt: BeginAttempt's resets minus the roll/Plant entry (the sit is the base, so the
    // ladder jumps to the Rise). The pose latch is taken here from the seated body: the hold phase
    // drives the legs to the latch, so a stale one would snap them away.
    private void BeginSitAttempt(float tilt, float now, float pelvisY)
    {
        getUpRollBounds = 0;
        getUpRollBlocked = false;
        getUpArbSuppL = 0;
        getUpArbSuppR = 0;
        getUpArbFirstL = false;
        getUpArbFirstR = false;
        getUpStage = GetUpStage.Rise;
        getUpStageEnterTime = now;
        getUpDone = false;
        getUpHandsPlanted = false;
        getUpReachedTuck = false;
        getUpStandNamed = false;
        getUpStandGateHeld = 0f;
        getUpStandGateHolds = 0;
        getUpStandGateInHold = false;
        getUpBaseWait = 0f;
        getUpBaseFast = false;
        getUpBaseSettleT = 0f;
        getUpBaseSettleWarned = false;
        getUpBaseSettleGap = 0f;
        getUpBaseLoadT = 0f;
        getUpBaseLoadGap = 0f;
        getUpBaseLoadWarned = false;
        getUpLoadExtT = 0f;
        getUpLoadExtGap = 0f;
        getUpLoadExtLive = false;
        getUpMoveGuardHeld = 0f;
        getUpMoveGuardHolds = 0;
        getUpMoveGuardInHold = false;
        getUpTrunkGovHeld = 0f;
        getUpTrunkGovHolds = 0;
        getUpTrunkGovInHold = false;
        getUpTrunkGovBias = 0f;
        getUpRiseStartPelvisY = -999f;
        getUpBaseSettleDuty = 0f;
        getUpSettleDutyHead = 0;
        getUpSettleDutyCount = 0;
        getUpSettleDutySum = 0f;
        getUpSettleDutyAge = 0f;
        getUpDriveOn = true;
        getUpHipL = leftHipJoint != null ? leftHipJoint.jointAngle : 0f;
        getUpHipR = rightHipJoint != null ? rightHipJoint.jointAngle : 0f;
        getUpKneeL = leftKneeJoint != null ? leftKneeJoint.jointAngle : 0f;
        getUpKneeR = rightKneeJoint != null ? rightKneeJoint.jointAngle : 0f;
        getUpAnkleL = leftAnkleJoint != null ? leftAnkleJoint.jointAngle : 0f;
        getUpAnkleR = rightAnkleJoint != null ? rightAnkleJoint.jointAngle : 0f;
        getUpTrunkStartDeg = tilt;
        afterfallCrawlOn = false;
        getUpSitActive = true;
        getUpSitStandExt = 0f;
        getUpSitStandT = 0f;
        getUpSitStandFires++;
        getUpSitPullKey = 0f;      // ROUND 44: the pull route's own clocks
        getUpSitPullLogged = false;
        getUpSitDrawLogged = false;   // CYCLE 108: the draw's hold line is per attempt
        seatDrawPlantT = 0f; seatDrawPlantK = 0f;
        seatDrawPlantLogged = false; seatDrawPlantReleased = false;   // CYCLE 109: per attempt
        seatDrawPlantEpT = 0f; seatDrawPlantEps = 0; seatDrawPlantPrevGate = false;   // CYCLE 114
        // Arc state; the fire is the base datum.
        getUpSitArcClock = 0f;
        getUpSitArcHeld = 0f;
        getUpSitArcHolds = 0;
        getUpSitArcInHold = false;
        getUpSitArcTiltRate = 0f;
        getUpSitArcHShare = 0f;
        getUpSitRockClock = 0f;    // CYCLE 106: the rock's clock, fresh at the fire
        getUpSitArcBasePelvY = pelvisY;
        getUpSitArcPrevTilt = tilt;
        if (getUpSitArcSec > 0.001f)
            Debug.Log("GETUP-SITARC enter t=" + N(now, 3) + " sec=" + N(getUpSitArcSec, 2)
                + " torso=" + N(getUpSitArcTorso, 1) + " rate=" + N(getUpSitArcRate, 1)
                + " tieext=" + N(getUpSitArcTieExt, 0)
                + " rock=" + N(getUpSitRockSec, 2) + " gate=" + N(getUpSitRockGate, 0)
                + " over=" + N(getUpSitExtOver, 2));
        seatPhase = -1;            // ROUND 45: the seat machine re-enters fresh
        ResetSitReseat();          // ROUND 49
        seatPressNow = 0f;
        seatGateNow = false;
        if (balance != null)
        {
            balance.getUpAttempts++;
            balance.getUpPose = 4;
            if (balance.getUpPoseMax < 4) balance.getUpPoseMax = 4;
            balance.getUpSitFires = getUpSitStandFires;
            balance.locomotionState = BalanceController.LocomotionState.GettingUp;
            balance.currentState = BalanceController.BalanceState.Recovery;
        }
        Debug.Log("GETUP-SITSTAND enter t=" + N(now, 3) + " drop=" + N(standPelvisY - pelvisY, 3)
            + " fzL=" + N(poseGroundFzLeftN, 0) + " fzR=" + N(poseGroundFzRightN, 0)
            + " tilt=" + N(tilt, 1));
    }

    private void SetGetUpStage(GetUpStage stage, float now)
    {
        getUpStage = stage;
        getUpStageEnterTime = now;
    }

    // Attempt end: grips released (nothing here acts outside the get-up window) and the ladder returns
    // to Idle, handing the body back to the walk path on the next step.
    private void FinishAttempt(bool stood, float now)
    {
        EndAttempt();
        getUpDriveOn = false;
        getUpRetryAfter = stood ? -1f : now + GetUpRetrySeconds * getUpPatience;
        // Restart the walk from a fresh step: the step clock stopped when the body went down, so a stale
        // phase would start the gait mid-swing.
        ResetStepClock();
        if (balance != null)
            balance.locomotionState = stood
                ? BalanceController.LocomotionState.Walking
                : BalanceController.LocomotionState.Fallen;
        if (getUpSitActive)
            Debug.Log("GETUP-SITSTAND end t=" + N(now, 3) + " ext=" + N(getUpSitStandExt, 2)
                + " stood=" + (stood ? 1 : 0));
        getUpSitActive = false;
        // Reset the sit-pull key so the trace does not show a stale value; every exit passes here.
        getUpSitPullKey = 0f;
        seatPhase = -1;
        ResetSitReseat();          // ROUND 49: no stale re-seat state
        // Hip arbitration report: one line per attempt that refused pelvis-law writes (raw counts).
        if (getUpHipArb > 0.5f && (getUpArbSuppL + getUpArbSuppR) > 0)
            Debug.Log("GETUP-HIPARB attempt t=" + N(now, 3) + " suppL=" + getUpArbSuppL
                + " suppR=" + getUpArbSuppR + " totL=" + getUpArbSuppLTot
                + " totR=" + getUpArbSuppRTot + (stood ? " stood" : " notstood"));
        // The trace is not closed here: one run = one file from the first down frame to the run's end, so
        // a retry cannot truncate earlier attempts.
    }

    // Every exit from the get-up passes here: grips let go and the ladder resets. Idempotent: called
    // every frame the body is not down, whether or not an attempt ran.
    private void EndAttempt()
    {
        if (!gripHeld[0] && !gripHeld[1] && getUpStage == GetUpStage.Idle && !getUpDriveOn)
            return;
        ReleaseGrip(0, false);
        ReleaseGrip(1, false);
        getUpStage = GetUpStage.Idle;
        getUpDownSince = -1f;
    }

    // Called from the walk/stand path once the body is no longer down: the get-up is over, grips are
    // off and the step clock restarts so the walk keys from zero.
    private void EndGetUp()
    {
        bool wasActive = getUpStage != GetUpStage.Idle || getUpDriveOn;
        EndAttempt();
        getUpDriveOn = false;
        // The plant reach must never outlive the get-up.
        if (armDrive != null) armDrive.getUpPlantReach = 0f;
        if (wasActive)
        {
            getUpRetryAfter = -1f;
            ResetStepClock();
        }
        // The trace writer stays open for the whole run so a later fall appends to the same file;
        // OnDestroy / OnApplicationQuit close it.
    }

    private void ResetStepClock()
    {
        stepClock = 0f;
        poseTailLoadHeld = 0f;
    }

    // ─── hand grip ───
    // Palm normal load comes from the solver's contact impulses on the hand's own collider (the same
    // source as the sole meter), in body weights: the engage measures the press, not its intent.
    private void SampleHandGrip(float dt)
    {
        float weightN = human != null ? Mathf.Abs(Physics2D.gravity.y) * human.totalMass : 0f;
        for (int side = 0; side < 2; side++)
        {
            gripTornThisStep[side] = false;
            Collider2D col = handCollider[side];
            if (col == null) continue;
            if (!handGripFilterReady)
            {
                handGripFilter = new ContactFilter2D();
                handGripFilter.SetLayerMask(GroundLayers.Mask);
                handGripFilter.useTriggers = false;
                handGripFilterReady = true;
            }
            int n = col.GetContacts(handGripFilter, handGripContacts);
            int count = Mathf.Min(n, handGripContacts.Length);
            float fn = 0f, tf = 0f;
            for (int k = 0; k < count; k++)
            {
                ContactPoint2D cp = handGripContacts[k];
                if (cp.normalImpulse * cp.normal.y <= 0f) continue;
                fn += cp.normalImpulse * cp.normal.y;
                tf += Mathf.Abs(cp.tangentImpulse);
            }
            gripFnN[side] = fn / dt;
            gripTfN[side] = tf / dt;
            gripLoad[side] = weightN > 1f ? gripFnN[side] / weightN : 0f;
            gripCapN[side] = PairMu() * gripFnN[side];
            if (!handGripEnabled || getUpStage == GetUpStage.Idle)
            {
                // No grip family or no attempt running: the palm keeps its own material and reads as a free palm.
                gripPressWants[side] = false;
                ReleaseGrip(side, false);
                continue;
            }
            // Press latch: a live press ask (ArmDrive's press request or a running stroke phase) over a real
            // touch engages the grip even when the measured palm load stays under the engage threshold.
            // -poseHandGripPress 0 = load-only engage.
            bool pressLive = handGripPressOn && PressWants(side);
            gripPressWants[side] = pressLive;
            if (!gripHeld[side])
            {
                if (gripLoad[side] >= GripEngageLoad || (pressLive && count > 0))
                {
                    gripHeld[side] = true;
                    gripUnloadAge[side] = 0f;
                    gripSlipM[side] = 0f;
                    gripAnchor[side] = col.bounds.center;
                    gripEngages[side]++;
                    if (pressLive && !gripPressLogged[side])
                    {
                        gripPressLogged[side] = true;
                        Debug.Log("GRIP-PRESS engage side=" + side
                            + " t=" + N(Time.fixedTime, 3)
                            + " fn=" + N(fn, 1) + " load=" + N(gripLoad[side], 4));
                    }
                    if (human != null) human.SetHandGrip(col, true);
                }
            }
            else
            {
                // Anchored slip: the palm's drift since the engage; past handGripSlipMeters the grip tears out.
                gripSlipM[side] = ((Vector2)col.bounds.center - gripAnchor[side]).magnitude;
                if (gripSlipM[side] > handGripSlipMeters)
                {
                    gripTears[side]++;
                    gripTornThisStep[side] = true;
                    ReleaseGrip(side, true);
                }
                else if (gripLoad[side] < GripReleaseLoad && !pressLive)
                {
                    gripUnloadAge[side] += dt;
                    if (gripUnloadAge[side] >= handGripHoldSeconds) ReleaseGrip(side, false);
                }
                else
                {
                    gripUnloadAge[side] = 0f;
                }
            }
        }
    }

    // Press ask per side: ArmDrive's press request (the get-up's ask) or a running stroke phase (1..4).
    private bool PressWants(int side)
    {
        if (armDrive == null) return false;
        if (armDrive.PressRequest >= 0.01f) return true;
        return (side == 0 ? armDrive.PressPhaseL : armDrive.PressPhaseR) >= 1;
    }

    // ─── grip capture (witness only; nothing reads it back) ───
    public int CaptureGripEnabled => handGripEnabled ? 1 : 0;
    public int CaptureGripStage => (int)getUpStage;
    public int CaptureGripWants(int side) => gripPressWants[side] ? 1 : 0;
    public int CaptureGripHeld(int side) => gripHeld[side] ? 1 : 0;
    public float CaptureGripFn(int side) => gripFnN[side];
    public float CaptureGripLoad(int side) => gripLoad[side];
    public float CaptureGripSlip(int side) => gripSlipM[side];
    public int CaptureGripEngages(int side) => gripEngages[side];
    public int CaptureGripTears(int side) => gripTears[side];
    public float CaptureGripMuMat => HandMaterialFriction(0);
    public float CaptureGripMuPair => PairMu();
    public float CaptureGripPalmX(int side) =>
        handCollider[side] != null ? handCollider[side].bounds.center.x : 0f;
    public float CaptureGripPalmY(int side) =>
        handCollider[side] != null ? handCollider[side].bounds.center.y : 0f;

    private void ReleaseGrip(int side, bool tore)
    {
        if (!gripHeld[side]) return;
        gripHeld[side] = false;
        gripUnloadAge[side] = 0f;
        if (human != null && handCollider[side] != null)
            human.SetHandGrip(handCollider[side], false);
    }

    // Palm-ground pair friction as the 2D engine combines it (PhysicsMaterial2D.GetCombinedValues),
    // computed once and cached.
    private float handGripMuCache = -1f;
    private float handGripMuGround = -1f;

    private float PairMu()
    {
        if (handGripMuCache >= 0f) return handGripMuCache;
        if (handGripMuGround < 0f)
        {
            GameObject ground = GameObject.Find("Ground");
            Collider2D groundCol = ground != null ? ground.GetComponent<Collider2D>() : null;
            handGripMuGround = groundCol != null && groundCol.sharedMaterial != null
                ? Mathf.Max(0f, groundCol.sharedMaterial.friction) : 0.4f;
        }
        float palmMu = 1.4f;
        PhysicsMaterialCombine2D palmMode = PhysicsMaterialCombine2D.Average;
        Collider2D palm = handCollider[0] != null ? handCollider[0] : handCollider[1];
        if (palm != null && palm.sharedMaterial != null)
        {
            palmMu = Mathf.Max(0f, palm.sharedMaterial.friction);
            palmMode = palm.sharedMaterial.frictionCombine;
        }
        handGripMuCache = Mathf.Max(0f,
            PhysicsMaterial2D.GetCombinedValues(palmMu, handGripMuGround, palmMode,
                PhysicsMaterialCombine2D.Average));
        return handGripMuCache;
    }

    // The palm's material friction in force: above the free palm's 1.4 while a grip holds.
    private float HandMaterialFriction(int side)
    {
        Collider2D col = handCollider[side];
        return col != null && col.sharedMaterial != null
            ? col.sharedMaterial.friction : 0f;
    }

    // ─── get-up trace (witness only; nothing reads it back) ───
    private void OpenGetUpTrace()
    {
        if (!getUpEnabled || getUpTrace != null) return;
        string label = GetString("-label", "trial");
        string folder = GetString("-out", ".");
        try
        {
            getUpTrace = new System.IO.StreamWriter(
                System.IO.Path.Combine(folder, label + "-getup.csv"), false);
            getUpTrace.WriteLine(
                "frame;t;stage;loco;tilt;pelvisY;pelvisH;standH;comX;comY;comStand;"
                + "footLx;footRx;footLy;footRy;footDownL;footDownR;underL;underR;"
                + "tiltBar;comBar;feetBar;handContactL;handContactR;pressPhaseL;pressPhaseR;"
                + "ask;gripL;gripR;gripLoadL;gripLoadR;fnL;fnR;tfL;tfR;capL;capR;muMat;muPair;"
                + "slipL;slipR;engagesL;engagesR;tearsL;tearsR;stageAge;gateHeld;gateHolds;baseWait;baseAborts;settleT;settleBlocks;settleDuty;settleGap"
                + ";sitWin;sitExt;sitPull;seatGate;seatFr;dragGapL;dragGapR;dragTip"
                + ";pvV;arcK;arcHld;hShare;tRate;tgtHipL"
                + ";rsL;rsR;rsT;tgtKneeL;tgtAnkleL;shiftT;shiftFzMax;shiftGap"
                + ";plantT;plantFrac;plantFzMax;drawPlantK;drawPlantT;drawPlantEps;drawPlantEpT");
        }
        catch (System.Exception)
        {
            getUpTrace = null;
        }
    }

    private void WriteGetUpTrace(float tilt, float pelvisY, float pelvisH, float standH,
                                 float comX, float comY, float standComY,
                                 float leftFootX, float rightFootX,
                                 float leftFootY, float rightFootY,
                                 bool leftDown, bool rightDown,
                                 bool leftUnder, bool rightUnder,
                                 bool tiltBar, bool comBar, bool feetBar, float stageAge)
    {
        if (!getUpEnabled || getUpTrace == null) return;
        getUpTraceRow.Clear();
        getUpTraceRow.Append(Time.frameCount).Append(';')
            .Append(N(Time.fixedTime, 3)).Append(';')
            .Append((int)getUpStage).Append(';')
            .Append((int)(balance != null ? balance.locomotionState
                : BalanceController.LocomotionState.Standing)).Append(';')
            .Append(N(tilt, 2)).Append(';').Append(N(pelvisY, 4)).Append(';')
            .Append(N(pelvisH, 4)).Append(';').Append(N(standH, 4)).Append(';')
            .Append(N(comX, 4)).Append(';').Append(N(comY, 4)).Append(';')
            .Append(float.IsNaN(standComY) ? "-1" : N(standComY, 4)).Append(';')
            .Append(N(leftFootX, 4)).Append(';').Append(N(rightFootX, 4)).Append(';')
            .Append(N(leftFootY, 4)).Append(';').Append(N(rightFootY, 4)).Append(';')
            .Append(leftDown ? 1 : 0).Append(';').Append(rightDown ? 1 : 0).Append(';')
            .Append(leftUnder ? 1 : 0).Append(';').Append(rightUnder ? 1 : 0).Append(';')
            .Append(tiltBar ? 1 : 0).Append(';').Append(comBar ? 1 : 0).Append(';')
            .Append(feetBar ? 1 : 0).Append(';')
            .Append(armDrive != null && armDrive.HandContactL ? 1 : 0).Append(';')
            .Append(armDrive != null && armDrive.HandContactR ? 1 : 0).Append(';')
            .Append(armDrive != null ? armDrive.PressPhaseL : -1).Append(';')
            .Append(armDrive != null ? armDrive.PressPhaseR : -1).Append(';')
            .Append(N(armDrive != null ? armDrive.PressRequest : 0f, 3)).Append(';')
            .Append(gripHeld[0] ? 1 : 0).Append(';').Append(gripHeld[1] ? 1 : 0).Append(';')
            .Append(N(gripLoad[0], 4)).Append(';').Append(N(gripLoad[1], 4)).Append(';')
            .Append(N(gripFnN[0], 2)).Append(';').Append(N(gripFnN[1], 2)).Append(';')
            .Append(N(gripTfN[0], 2)).Append(';').Append(N(gripTfN[1], 2)).Append(';')
            .Append(N(gripCapN[0], 2)).Append(';').Append(N(gripCapN[1], 2)).Append(';')
            .Append(N(HandMaterialFriction(0), 3)).Append(';').Append(N(PairMu(), 3)).Append(';')
            .Append(N(gripSlipM[0], 4)).Append(';').Append(N(gripSlipM[1], 4)).Append(';')
            .Append(gripEngages[0]).Append(';').Append(gripEngages[1]).Append(';')
            .Append(gripTears[0]).Append(';').Append(gripTears[1]).Append(';')
            .Append(N(stageAge, 3))
            .Append(';').Append(N(getUpStandGateHeld, 3)).Append(';').Append(getUpStandGateHolds)
            .Append(';').Append(N(getUpBaseWait, 3)).Append(';').Append(getUpBaseAborts)
            .Append(';').Append(N(getUpBaseSettleT, 3)).Append(';').Append(getUpBaseSettleBlocks)
            .Append(';').Append(N(getUpBaseSettleDuty, 3)).Append(';').Append(N(getUpBaseSettleGap, 3))
            .Append(';').Append(getUpSitWinNow ? 1 : 0).Append(';').Append(N(getUpSitStandExt, 3))
            .Append(';').Append(N(getUpSitPullKey, 3))
            .Append(';').Append(seatGateNow ? 1 : 0).Append(';').Append(N(seatFrozenT, 2))
            .Append(';').Append(N(seatGapL, 3)).Append(';').Append(N(seatGapR, 3))
            .Append(';').Append(N(seatTipDev, 1))
            .Append(';').Append(N(getUpTracePelvVel, 3)).Append(';').Append(N(getUpSitArcClock, 3))
            .Append(';').Append(N(getUpSitArcHeld, 3)).Append(';').Append(N(getUpSitArcHShare, 3))
            .Append(';').Append(N(getUpSitArcTiltRate, 1)).Append(';').Append(N(gusTgtHipL, 1))
            .Append(';').Append(N(seatResL, 3)).Append(';').Append(N(seatResR, 3))
            .Append(';').Append(N(seatReseatT, 3))
            .Append(';').Append(N(gusTgtKneeL, 1)).Append(';').Append(N(gusTgtAnkleL, 1))
            .Append(';').Append(N(sitShiftT, 3)).Append(';').Append(N(sitShiftFzMax, 0))
            .Append(';').Append(N(sitShiftGapMin, 3))
            .Append(';').Append(N(sitPlantT, 3)).Append(';').Append(N(sitPlantFrac, 3))
            .Append(';').Append(N(sitPlantFzMax, 0))
            .Append(';').Append(N(seatDrawPlantK, 3)).Append(';').Append(N(seatDrawPlantT, 3))
            .Append(';').Append(N(seatDrawPlantEps, 0)).Append(';').Append(N(seatDrawPlantEpT, 3));
        getUpTrace.WriteLine(getUpTraceRow.ToString());
        getUpTraceFlushAge += Time.fixedDeltaTime;
        if (getUpTraceFlushAge >= 0.2f)
        {
            getUpTrace.Flush();
            getUpTraceFlushAge = 0f;
        }
    }

    private void CloseGetUpTrace()
    {
        if (getUpTrace == null) return;
        try
        {
            getUpTrace.Flush();
            getUpTrace.Dispose();
        }
        catch (System.Exception)
        {
            // A closed run is not a crash: the rows already written stand.
        }
        getUpTrace = null;
    }
}
