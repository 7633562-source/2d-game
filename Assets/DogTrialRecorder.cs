using System;
using System.Globalization;
using System.Text;
using UnityEngine;

// Dog stand summary. Reads only. New keys; does not touch human TrialRecorder fields.
public class DogTrialRecorder
{
    private const float SATURATION_THRESHOLD = 0.999f;
    private const float SETTLED_COM_OFFSET = 0.01f;

    private readonly Dog dog;
    private readonly TrialRunInfo info;
    private readonly DogStanceController stance;
    private readonly Transform chest;
    private readonly Transform pelvis;

    private readonly StringBuilder rows = new StringBuilder();
    private readonly char[] numberBuffer = new char[32];
    private int samples;
    private double sumComOffsetSquared;
    private float maxAbsChestPitch;
    private float maxAbsPelvisTilt;
    private float maxAbsLumbarAngle;
    private float maxAbsComOffset;
    private float fallTime = -1f;
    private int samplesSaturated;
    private int samplesFourFeet;
    private float startChestY;
    private float startPelvisY;
    private float minChestY = float.MaxValue;
    private float minPelvisY = float.MaxValue;
    private float lastChestPitch;
    private float lastPelvisTilt;
    private float lastLumbar;
    private float lastComOffset;
    private float lastChestY;
    private float lastPelvisY;
    private float pushTime = -1f;
    private float pushImpulse;
    private float lastUnsettledTime = -1f;
    private readonly JawStrike jawStrike;
    private readonly Damageable preyLife;
    private string lastState = "";
    private int lastLeapCount;
    private float lastJawAngle;
    private int maxLeapCount;
    private bool sawLeapCrouch;
    private bool sawLeapPush;
    private bool sawLeapAir;
    private bool sawLeapBite;
    private bool walkWindowOpen;
    private float walkStartComX;
    private float walkStartTime = -1f;
    private float walkEndComX;
    private float walkEndTime = -1f;
    private int samplesWalkActive;
    private int lastWalkCycleCount;
    private int lastSenseTick;
    private float sensorComXMin = float.MaxValue;
    private float sensorComXMax = float.MinValue;
    private float sensorMarginMin = float.MaxValue;
    private float sensorMarginMax = float.MinValue;
    private int samplesOnsetWindow;
    private int samplesOnsetCollapsed;
    private int samplesOnsetPosed;

    public DogTrialRecorder(Dog dog, TrialRunInfo info)
    {
        this.dog = dog;
        this.info = info;
        stance = dog != null ? dog.stance : null;
        chest = dog != null ? dog.transform.Find("Chest") : null;
        pelvis = dog != null ? dog.transform.Find("Pelvis") : null;

        jawStrike = dog != null ? dog.GetComponent<JawStrike>() : null;
        DogPrey prey = UnityEngine.Object.FindFirstObjectByType<DogPrey>();
        preyLife = prey != null ? prey.GetComponent<Damageable>() : null;

        rows.AppendLine("t,chestPitch,pelvisTilt,lumbar,chestY,pelvisY,comX,comY,comOffset,frG,flG,rrG,rlG,sat,state,leapCount,jawAngle");
    }

    public void MarkPush(float time, float impulse)
    {
        pushTime = time;
        pushImpulse = impulse;
    }

    public void Sample(float time)
    {
        if (stance == null || chest == null) return;

        float chestY = chest.position.y;
        float pelvisY = pelvis != null ? pelvis.position.y : chestY;
        float pitch = stance.chestPitch;
        float pTilt = stance.pelvisTilt;
        float lumbar = stance.lumbarAngle;
        float comOff = stance.comOffsetX;

        if (samples == 0)
        {
            startChestY = chestY;
            startPelvisY = pelvisY;
            minChestY = chestY;
            minPelvisY = pelvisY;
        }

        if (chestY < minChestY) minChestY = chestY;
        if (pelvisY < minPelvisY) minPelvisY = pelvisY;
        lastChestY = chestY;
        lastPelvisY = pelvisY;
        lastChestPitch = pitch;
        lastPelvisTilt = pTilt;
        lastLumbar = lumbar;
        lastComOffset = comOff;

        float absPitch = Mathf.Abs(pitch);
        if (absPitch > maxAbsChestPitch) maxAbsChestPitch = absPitch;
        float absPelvis = Mathf.Abs(pTilt);
        if (absPelvis > maxAbsPelvisTilt) maxAbsPelvisTilt = absPelvis;
        float absLumbar = Mathf.Abs(lumbar);
        if (absLumbar > maxAbsLumbarAngle) maxAbsLumbarAngle = absLumbar;
        float absOff = Mathf.Abs(comOff);
        if (absOff > maxAbsComOffset) maxAbsComOffset = absOff;
        sumComOffsetSquared += comOff * comOff;

        if (stance.AllPawsGrounded()) samplesFourFeet++;
        float maxActivation = stance.MaxStanceActivation();
        if (maxActivation >= SATURATION_THRESHOLD) samplesSaturated++;

        if (fallTime < 0f && (stance.currentState == DogStanceController.StanceState.Falling
            || stance.currentState == DogStanceController.StanceState.Fallen))
            fallTime = time;

        if (absOff > SETTLED_COM_OFFSET)
            lastUnsettledTime = time;

        lastState = stance.currentState.ToString();
        lastLeapCount = stance.leapCount;
        lastJawAngle = stance.jawAngle;
        if (stance.leapCount > maxLeapCount)
            maxLeapCount = stance.leapCount;
        if (stance.currentState == DogStanceController.StanceState.LeapCrouch) sawLeapCrouch = true;
        if (stance.currentState == DogStanceController.StanceState.LeapPush) sawLeapPush = true;
        if (stance.currentState == DogStanceController.StanceState.LeapAir) sawLeapAir = true;
        if (stance.currentState == DogStanceController.StanceState.LeapBite) sawLeapBite = true;

        if (stance.IsWalk() || stance.walkWanted)
        {
            if (!walkWindowOpen)
            {
                walkStartComX = stance.comPosition.x;
                walkStartTime = time;
                walkWindowOpen = true;
            }
            walkEndComX = stance.comPosition.x;
            walkEndTime = time;
            samplesWalkActive++;
        }
        lastWalkCycleCount = stance.walkCycleCount;
        lastSenseTick = stance.senseTick;
        float comX = stance.comPosition.x;
        if (comX < sensorComXMin) sensorComXMin = comX;
        if (comX > sensorComXMax) sensorComXMax = comX;
        float margin = stance.supportMargin;
        if (margin < sensorMarginMin) sensorMarginMin = margin;
        if (margin > sensorMarginMax) sensorMarginMax = margin;
        if (walkWindowOpen && walkStartTime >= 0f && time - walkStartTime <= 3.001f)
        {
            samplesOnsetWindow++;
            if (stance.WalkCollapsed) samplesOnsetCollapsed++;
            if (stance.WalkPosed) samplesOnsetPosed++;
        }

        AppendCsvFloat(time);
        rows.Append(',');
        AppendCsvFloat(pitch);
        rows.Append(',');
        AppendCsvFloat(pTilt);
        rows.Append(',');
        AppendCsvFloat(lumbar);
        rows.Append(',');
        AppendCsvFloat(chestY);
        rows.Append(',');
        AppendCsvFloat(pelvisY);
        rows.Append(',');
        AppendCsvFloat(stance.comPosition.x);
        rows.Append(',');
        AppendCsvFloat(stance.comPosition.y);
        rows.Append(',');
        AppendCsvFloat(comOff);
        rows.Append(',').Append(stance.frontRightGrounded ? '1' : '0');
        rows.Append(',').Append(stance.frontLeftGrounded ? '1' : '0');
        rows.Append(',').Append(stance.rearRightGrounded ? '1' : '0');
        rows.Append(',').Append(stance.rearLeftGrounded ? '1' : '0');
        rows.Append(',');
        AppendCsvFloat(maxActivation);
        rows.Append(',').Append((int)stance.currentState);
        rows.Append(',');
        AppendCsvFloat(stance.leapCount);
        rows.Append(',');
        AppendCsvFloat(stance.jawAngle);
        rows.AppendLine();

        samples++;
    }

    public string BuildCsv()
    {
        return rows.ToString();
    }

    public string BuildSummaryJson(float simulatedSeconds, float wallSeconds = -1f)
    {
        float trunkDrop = Mathf.Max(startChestY - minChestY, startPelvisY - minPelvisY);
        float rmsCom = samples > 0 ? Mathf.Sqrt((float)(sumComOffsetSquared / samples)) : 0f;
        float survived = fallTime >= 0f ? fallTime : simulatedSeconds;
        float recovery = 0f;
        if (pushTime >= 0f && lastUnsettledTime >= pushTime)
            recovery = lastUnsettledTime - pushTime;

        StringBuilder json = new StringBuilder();
        json.Append("{");
        AppendStr(json, "subject", "dog", false);
        AppendStr(json, "label", info != null ? info.Label : "trial");
        AppendNum(json, "simulatedSeconds", simulatedSeconds);
        if (wallSeconds >= 0f)
        {
            AppendNum(json, "wallSeconds", wallSeconds);
            if (simulatedSeconds > 0f)
                AppendNum(json, "wallPerSim", wallSeconds / simulatedSeconds);
        }
        json.Append(",\"samples\":").Append(samples.ToString(CultureInfo.InvariantCulture));
        AppendNum(json, "fixedDeltaTime", Time.fixedDeltaTime);
        AppendNum(json, "survivedSeconds", survived);
        json.Append(",\"fell\":").Append(fallTime >= 0f ? "true" : "false");
        AppendNum(json, "maxAbsChestPitch", maxAbsChestPitch);
        AppendNum(json, "maxAbsPelvisTilt", maxAbsPelvisTilt);
        AppendNum(json, "maxAbsLumbarAngle", maxAbsLumbarAngle);
        AppendNum(json, "trunkDrop", trunkDrop);
        AppendNum(json, "chestDrop", startChestY - minChestY);
        AppendNum(json, "pelvisDrop", startPelvisY - minPelvisY);
        AppendNum(json, "startChestY", startChestY);
        AppendNum(json, "minChestY", minChestY);
        AppendNum(json, "finalChestY", lastChestY);
        AppendNum(json, "finalChestPitch", lastChestPitch);
        AppendNum(json, "finalPelvisTilt", lastPelvisTilt);
        AppendNum(json, "finalLumbarAngle", lastLumbar);
        AppendNum(json, "rmsComOffset", rmsCom);
        AppendNum(json, "maxAbsComOffset", maxAbsComOffset);
        AppendNum(json, "finalComOffset", lastComOffset);
        AppendNum(json, "fourFeetGroundedFraction", samples > 0 ? samplesFourFeet / (float)samples : 0f);
        AppendNum(json, "muscleSaturationFraction", samples > 0 ? samplesSaturated / (float)samples : 0f);
        AppendNum(json, "recoverySeconds", recovery);
        AppendStr(json, "currentState", lastState);
        AppendNum(json, "leapCount", lastLeapCount);
        AppendNum(json, "maxLeapCount", maxLeapCount);
        AppendNum(json, "jawAngle", lastJawAngle);
        json.Append(",\"sawLeapCrouch\":").Append(sawLeapCrouch ? "true" : "false");
        json.Append(",\"sawLeapPush\":").Append(sawLeapPush ? "true" : "false");
        json.Append(",\"sawLeapAir\":").Append(sawLeapAir ? "true" : "false");
        json.Append(",\"sawLeapBite\":").Append(sawLeapBite ? "true" : "false");
        float walkWindow = walkWindowOpen && walkEndTime > walkStartTime
            ? walkEndTime - walkStartTime
            : 0f;
        float travelX = walkWindowOpen ? walkEndComX - walkStartComX : 0f;
        float speedX = walkWindow > 0.01f ? travelX / walkWindow : 0f;
        AppendNum(json, "comTravelX", travelX);
        AppendNum(json, "comSpeedX", speedX);
        AppendNum(json, "comSpeedKmh", speedX * 3.6f);
        AppendNum(json, "walkWindowSeconds", walkWindow);
        json.Append(",\"walkCycleCount\":").Append(lastWalkCycleCount.ToString(CultureInfo.InvariantCulture));
        AppendNum(json, "walkActiveFraction", samples > 0 ? samplesWalkActive / (float)samples : 0f);
        json.Append(",\"senseTick\":").Append(lastSenseTick.ToString(CultureInfo.InvariantCulture));
        AppendNum(json, "sensorComXMin",
            samples > 0 && sensorComXMin < float.MaxValue * 0.5f ? sensorComXMin : 0f);
        AppendNum(json, "sensorComXMax",
            samples > 0 && sensorComXMax > float.MinValue * 0.5f ? sensorComXMax : 0f);
        AppendNum(json, "sensorSupportMarginMin",
            samples > 0 && sensorMarginMin < float.MaxValue * 0.5f ? sensorMarginMin : 0f);
        AppendNum(json, "sensorSupportMarginMax",
            samples > 0 && sensorMarginMax > float.MinValue * 0.5f ? sensorMarginMax : 0f);
        AppendNum(json, "walkOnsetSeconds", samplesOnsetWindow * Time.fixedDeltaTime);
        AppendNum(json, "walkOnsetCollapsedFraction",
            samplesOnsetWindow > 0 ? samplesOnsetCollapsed / (float)samplesOnsetWindow : 0f);
        AppendNum(json, "walkOnsetPosedFraction",
            samplesOnsetWindow > 0 ? samplesOnsetPosed / (float)samplesOnsetWindow : 0f);
        json.Append(",\"walkOnsetCollapsed\":")
            .Append(samplesOnsetCollapsed > 0 ? "true" : "false");
        json.Append(",\"walkOnsetPosed\":")
            .Append(samplesOnsetPosed > 0 ? "true" : "false");
        AppendStr(json, "walkOnsetLog", FormatWalkOnsetLog());
        json.Append(",\"getUpAttempts\":").Append(stance.getUpAttempts.ToString(CultureInfo.InvariantCulture));
        json.Append(",\"getUpSuccess\":").Append(stance.getUpSuccess ? "true" : "false");
        AppendNum(json, "getUpSeconds", stance.getUpSeconds);
        AppendNum(json, "getUpFallTime", stance.getUpFallTime);
        json.Append(",\"jawStrikeHits\":").Append((jawStrike != null ? jawStrike.HitCount : 0).ToString(CultureInfo.InvariantCulture));
        json.Append(",\"preyHurtCount\":").Append((preyLife != null ? preyLife.HurtCount : 0).ToString(CultureInfo.InvariantCulture));
        json.Append(",\"preyPresent\":").Append(preyLife != null ? "true" : "false");
        if (info != null)
        {
            AppendNum(json, "duration", info.Duration);
            AppendNum(json, "startY", info.StartY);
            AppendNum(json, "pushImpulse", info.RequestedPushImpulse);
            AppendNum(json, "pushTime", info.RequestedPushTime);
            AppendStr(json, "sourceFingerprint", info.SourceFingerprint);
        }
        if (pushTime >= 0f)
        {
            AppendNum(json, "appliedPushTime", pushTime);
            AppendNum(json, "appliedPushImpulse", pushImpulse);
        }
        AppendApplied(json);
        json.Append("}");
        return json.ToString();
    }

    private void AppendApplied(StringBuilder json)
    {
        int bodyCount = 0;
        int continuous = 0;
        if (dog != null)
        {
            Rigidbody2D[] rbs = dog.GetComponentsInChildren<Rigidbody2D>();
            bodyCount = rbs.Length;
            for (int i = 0; i < rbs.Length; i++)
            {
                if (rbs[i] != null && rbs[i].collisionDetectionMode == CollisionDetectionMode2D.Continuous)
                    continuous++;
            }
        }

        int muscles = dog != null ? dog.GetComponentsInChildren<Muscle>().Length : 0;
        int frictions = dog != null ? dog.GetComponentsInChildren<JointFriction>().Length : 0;
        DogStanceController c = stance;

        json.Append(",\"applied\":{");
        json.Append("\"subject\":\"dog\"");
        if (dog != null)
        {
            AppendNum(json, "totalMass", dog.totalMass);
            AppendVec2(json, "chestSize", dog.chestSize);
            AppendVec2(json, "pelvisSize", dog.pelvisSize);
            AppendVec2(json, "pawSize", dog.pawSize);
            AppendNum(json, "spawnHipAngle", dog.spawnHipAngle);
            AppendNum(json, "spawnShoulderAngle", dog.spawnShoulderAngle);
            AppendNum(json, "spawnKneeAngle", dog.spawnKneeAngle);
            AppendNum(json, "spawnElbowAngle", dog.spawnElbowAngle);
            AppendNum(json, "tailSegmentCount", dog.tailSegmentCount);
            AppendNum(json, "lumbarFriction", dog.lumbarFriction);
            AppendNum(json, "hipFriction", dog.hipFriction);
            AppendNum(json, "kneeFriction", dog.kneeFriction);
            AppendNum(json, "shoulderFriction", dog.shoulderFriction);
            AppendNum(json, "elbowFriction", dog.elbowFriction);
            AppendNum(json, "pawFriction", dog.pawFriction);
            AppendNum(json, "neckFriction", dog.neckFriction);
            AppendNum(json, "lumbarMuscleTorque", dog.lumbarMuscleTorque);
            AppendNum(json, "hipMuscleTorque", dog.hipMuscleTorque);
            AppendNum(json, "kneeMuscleTorque", dog.kneeMuscleTorque);
            AppendNum(json, "shoulderMuscleTorque", dog.shoulderMuscleTorque);
            AppendNum(json, "elbowMuscleTorque", dog.elbowMuscleTorque);
            AppendNum(json, "pawExtensorTorque", dog.pawExtensorTorque);
            AppendNum(json, "pawFlexorTorque", dog.pawFlexorTorque);
            AppendNum(json, "neckMuscleTorque", dog.neckMuscleTorque);
            AppendNum(json, "jawMuscleTorque", dog.jawMuscleTorque);
            json.Append(",\"hasJaw\":").Append(dog.jawCollider != null ? "true" : "false");
        }
        json.Append(",\"rigidbodyCount\":").Append(bodyCount.ToString(CultureInfo.InvariantCulture));
        json.Append(",\"continuousBodyCount\":").Append(continuous.ToString(CultureInfo.InvariantCulture));
        json.Append(",\"muscleBehaviourCount\":").Append(muscles.ToString(CultureInfo.InvariantCulture));
        json.Append(",\"frictionBehaviourCount\":").Append(frictions.ToString(CultureInfo.InvariantCulture));
        json.Append(",\"velocityIterations\":").Append(Physics2D.velocityIterations.ToString(CultureInfo.InvariantCulture));
        json.Append(",\"positionIterations\":").Append(Physics2D.positionIterations.ToString(CultureInfo.InvariantCulture));
        if (c != null)
        {
            AppendNum(json, "pawComP", c.pawComP);
            AppendNum(json, "pawComD", c.pawComD);
            AppendNum(json, "pawComDeadzone", c.pawComDeadzone);
            AppendNum(json, "pawComVelDeadzone", c.pawComVelDeadzone);
            AppendNum(json, "pawGroundHoldSeconds", c.pawGroundHoldSeconds);
            AppendNum(json, "chestPGain", c.chestPGain);
            AppendNum(json, "chestDGain", c.chestDGain);
            AppendNum(json, "chestTargetTilt", c.chestTargetTilt);
            AppendNum(json, "pelvisPGain", c.pelvisPGain);
            AppendNum(json, "pelvisDGain", c.pelvisDGain);
            AppendNum(json, "pelvisTargetTilt", c.pelvisTargetTilt);
            AppendNum(json, "lumbarPGain", c.lumbarPGain);
            AppendNum(json, "lumbarDGain", c.lumbarDGain);
            AppendNum(json, "lumbarPelvisP", c.lumbarPelvisP);
            AppendNum(json, "lumbarPelvisD", c.lumbarPelvisD);
            AppendNum(json, "lumbarStopP", c.lumbarStopP);
            AppendNum(json, "lumbarRestP", c.lumbarRestP);
            AppendNum(json, "lumbarDelaySeconds", c.lumbarDelaySeconds);
            AppendNum(json, "lumbarOpenGateDeg", c.lumbarOpenGateDeg);
            json.Append(",\"useLumbarJointRest\":").Append(c.useLumbarJointRest ? "true" : "false");
            AppendNum(json, "lumbarJointRestDeg", c.lumbarJointRestDeg);
            AppendNum(json, "hipPGain", c.hipPGain);
            AppendNum(json, "kneePGain", c.kneePGain);
            AppendNum(json, "startupHipP", c.startupHipP);
            AppendNum(json, "startupHipSeconds", c.startupHipSeconds);
            AppendNum(json, "hipBaseAngle", c.hipBaseAngle);
            AppendNum(json, "shoulderBaseAngle", c.shoulderBaseAngle);
            AppendNum(json, "kneeBaseAngle", c.kneeBaseAngle);
            AppendNum(json, "elbowBaseAngle", c.elbowBaseAngle);
            AppendNum(json, "heightP", c.heightP);
            AppendNum(json, "heightD", c.heightD);
            AppendNum(json, "heightMaxDeg", c.heightMaxDeg);
            AppendNum(json, "heightHipMaxDeg", c.heightHipMaxDeg);
            AppendNum(json, "heightDeadzone", c.heightDeadzone);
            AppendNum(json, "heightVelDeadzone", c.heightVelDeadzone);
            AppendNum(json, "poseHoldDeadzoneDeg", c.poseHoldDeadzoneDeg);
            AppendNum(json, "startupHoldSeconds", c.startupHoldSeconds);
            AppendNum(json, "jawOpen", c.jawOpen);
            AppendNum(json, "jawAngle", c.jawAngle);
            AppendStr(json, "currentState", c.currentState.ToString());
            AppendNum(json, "leapCount", c.leapCount);
            AppendNum(json, "leapCrouchSeconds", c.leapCrouchSeconds);
            AppendNum(json, "leapPushSeconds", c.leapPushSeconds);
            AppendNum(json, "leapAirSeconds", c.leapAirSeconds);
            AppendNum(json, "leapBiteSeconds", c.leapBiteSeconds);
            AppendNum(json, "leapBiteRange", c.leapBiteRange);
            AppendNum(json, "leapPawPlant", c.leapPawPlant);
            AppendNum(json, "leapActivationSpeed", c.leapActivationSpeed);
            AppendNum(json, "leapAimX", c.LeapAim.x);
            AppendNum(json, "leapAimY", c.LeapAim.y);
            json.Append(",\"leapTargetSet\":").Append(c.leapTarget != null ? "true" : "false");
            json.Append(",\"walkWanted\":").Append(c.walkWanted ? "true" : "false");
            AppendNum(json, "walkPeriod", c.walkPeriod);
            AppendNum(json, "walkDuty", c.walkDuty);
            AppendNum(json, "walkRearLag", c.walkRearLag);
            AppendNum(json, "walkHipFlex", c.walkHipFlex);
            AppendNum(json, "walkShoulderFlex", c.walkShoulderFlex);
            AppendNum(json, "walkHipExtend", c.walkHipExtend);
            AppendNum(json, "walkShoulderExtend", c.walkShoulderExtend);
            AppendNum(json, "walkKneeFlex", c.walkKneeFlex);
            AppendNum(json, "walkElbowFlex", c.walkElbowFlex);
            AppendNum(json, "walkPawPlant", c.walkPawPlant);
            AppendNum(json, "walkFrontPlantScale", c.walkFrontPlantScale);
            AppendNum(json, "walkChestTilt", c.walkChestTilt);
            AppendNum(json, "walkLumbarP", c.walkLumbarP);
            AppendNum(json, "walkHipP", c.walkHipP);
            AppendNum(json, "walkKneeP", c.walkKneeP);
            AppendNum(json, "walkActivationSpeed", c.walkActivationSpeed);
            AppendNum(json, "runSpeed", c.runSpeed);
            AppendNum(json, "runSpeedRef", c.runSpeedRef);
            AppendNum(json, "runStride", c.runStride);
            AppendNum(json, "runHipFlex", c.runHipFlex);
            AppendNum(json, "runShoulderFlex", c.runShoulderFlex);
            AppendNum(json, "runHipExtend", c.runHipExtend);
            AppendNum(json, "runShoulderExtend", c.runShoulderExtend);
            AppendNum(json, "runKneeFlex", c.runKneeFlex);
            AppendNum(json, "runElbowFlex", c.runElbowFlex);
            AppendNum(json, "runChestLean", c.runChestLean);
            AppendNum(json, "runPawPlant", c.runPawPlant);
            json.Append(",\"walkCycleCount\":").Append(c.walkCycleCount.ToString(CultureInfo.InvariantCulture));
            AppendNum(json, "walkPhase", c.walkPhase);
            json.Append(",\"walkCollapsed\":").Append(c.WalkCollapsed ? "true" : "false");
            json.Append(",\"walkPosed\":").Append(c.WalkPosed ? "true" : "false");
            json.Append(",\"senseTick\":").Append(c.senseTick.ToString(CultureInfo.InvariantCulture));
            AppendNum(json, "supportMargin", c.supportMargin);
            json.Append(",\"jawStrikeHits\":").Append((jawStrike != null ? jawStrike.HitCount : 0).ToString(CultureInfo.InvariantCulture));
            json.Append(",\"preyHurtCount\":").Append((preyLife != null ? preyLife.HurtCount : 0).ToString(CultureInfo.InvariantCulture));
            AppendNum(json, "targetPelvisY", c.targetPelvisY);
            AppendNum(json, "neckPGain", c.neckPGain);
            AppendNum(json, "neckDGain", c.neckDGain);
            json.Append(",\"useStablePd\":").Append(c.useStablePd ? "true" : "false");
            AppendNum(json, "muscleActivationSpeed", c.muscleActivationSpeed);
            AppendNum(json, "fallCoMOffset", c.fallCoMOffset);
            if (c.probeCallCount > 0)
            {
                AppendNum(json, "meanProbeHits", c.probeHitSum / (float)c.probeCallCount);
                AppendNum(json, "probeSaturatedFraction", c.probeSaturatedCount / (float)c.probeCallCount);
            }
        }
        json.Append("}");
    }

    private string FormatWalkOnsetLog()
    {
        if (stance == null || stance.WalkOnsetLogCount <= 0)
            return "";
        var log = new StringBuilder();
        for (int i = 0; i < DogStanceController.WalkOnsetLogCapacity; i++)
        {
            float age;
            bool collapsed;
            bool posed;
            float comX;
            float margin;
            float hipDrop;
            float comDrop;
            float columnStanding;
            byte reason;
            if (!stance.TryGetWalkOnsetSample(i, out age, out collapsed,
                    out posed, out comX, out margin, out hipDrop, out comDrop,
                    out columnStanding, out reason))
                continue;
            if (log.Length > 0)
                log.Append('|');
            log.Append(age.ToString("0.00", CultureInfo.InvariantCulture));
            log.Append(",c").Append(collapsed ? "1" : "0");
            log.Append(",p").Append(posed ? "1" : "0");
            log.Append(",x").Append(comX.ToString("0.000", CultureInfo.InvariantCulture));
            log.Append(",m").Append(margin.ToString("0.000", CultureInfo.InvariantCulture));
            log.Append(",hd").Append(hipDrop.ToString("0.000", CultureInfo.InvariantCulture));
            log.Append(",cd").Append(comDrop.ToString("0.000", CultureInfo.InvariantCulture));
            log.Append(",cs").Append(columnStanding.ToString("0.000", CultureInfo.InvariantCulture));
            log.Append(",r").Append(reason.ToString(CultureInfo.InvariantCulture));
        }
        return log.ToString();
    }

    private void AppendCsvFloat(float value)
    {
        Span<char> buffer = numberBuffer;
        if (value.TryFormat(buffer, out int written, "G9", CultureInfo.InvariantCulture))
        {
            rows.Append(numberBuffer, 0, written);
            return;
        }

        rows.Append(value.ToString("G9", CultureInfo.InvariantCulture));
    }

    private static void AppendStr(StringBuilder json, string key, string value, bool comma = true)
    {
        if (comma) json.Append(',');
        json.Append('"').Append(key).Append("\":\"");
        if (!string.IsNullOrEmpty(value))
            json.Append(value.Replace("\\", "\\\\").Replace("\"", "\\\""));
        json.Append('"');
    }

    private static void AppendNum(StringBuilder json, string key, float value, bool comma = true)
    {
        if (comma) json.Append(',');
        json.Append('"').Append(key).Append("\":");
        json.Append(value.ToString("G9", CultureInfo.InvariantCulture));
    }

    private static void AppendVec2(StringBuilder json, string key, Vector2 value)
    {
        json.Append(",\"").Append(key).Append("\":{\"x\":");
        json.Append(value.x.ToString("G9", CultureInfo.InvariantCulture));
        json.Append(",\"y\":");
        json.Append(value.y.ToString("G9", CultureInfo.InvariantCulture));
        json.Append('}');
    }
}
