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

        if (fallTime < 0f && stance.currentState == DogStanceController.StanceState.Falling)
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
