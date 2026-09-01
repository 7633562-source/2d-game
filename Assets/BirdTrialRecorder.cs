using System;
using System.Globalization;
using System.Text;
using UnityEngine;

// Сводка прогона птицы. Как TrialRecorder: только читает, сил нет.
// fell по CoM недостаточно — оседание и кульбит ловим отдельно.
public class BirdTrialRecorder
{
    private const float SATURATION_THRESHOLD = 0.999f;
    private const float PITCH_FALL_DEGREES = 55f;
    private const float BODY_DROP_FALL = 0.05f;
    private const float COM_OFFSET_FALL = 0.25f;

    private readonly Bird bird;
    private readonly TrialRunInfo info;
    private readonly BirdSensors sensors;
    private readonly BirdController control;
    private readonly Transform body;

    private readonly StringBuilder rows = new StringBuilder();
    private readonly char[] numberBuffer = new char[32];
    private int samples;
    private double sumComOffsetSquared;
    private float maxAbsPitch;
    private float maxAbsComOffset;
    private float fallTime = -1f;
    private string fallReason = "";
    private int samplesBothFeet;
    private int samplesSaturated;
    private float startBodyY;
    private float minBodyY = float.MaxValue;
    private float lastBodyY;
    private float lastPitch;
    private float lastComOffset;
    private float lastLiftY;
    private float maxLiftY;
    private double sumLiftY;
    private float maxComY;
    private float minComY = float.MaxValue;
    private float lastHip;
    private float lastKnee;
    private int lastMode;
    private bool lastBothGrounded;
    private double sumAbsLiftY;
    private float maxAbsShoulderSpeed;
    private int samplesAirborne;
    private float maxBodyY = float.MinValue;
    private int samplesSit;
    private int samplesWalk;
    private int samplesFly;
    private int samplesGlide;
    private int samplesPeck;
    private int lastFacingSign = 1;
    private bool enteredFlight;

    public BirdTrialRecorder(Bird bird, TrialRunInfo info)
    {
        this.bird = bird;
        this.info = info;
        sensors = bird != null ? bird.sensors : null;
        control = bird != null ? bird.controller : null;
        Transform bodyT = bird != null ? bird.transform.Find("Body") : null;
        body = bodyT;

        rows.AppendLine("t,bodyPitch,bodyY,comX,comY,comOffset,leftG,rightG,liftY,mode,hipL,kneeL,hipR,kneeR");
    }

    public void Sample(float time)
    {
        if (sensors == null || body == null) return;

        float pitch = sensors.bodyPitch;
        float bodyY = body.position.y;
        float comOff = sensors.comOffsetX;
        float liftY = control != null ? control.debugLiftY : 0f;
        int mode = control != null ? (int)control.mode : 0;

        if (samples == 0)
        {
            startBodyY = bodyY;
            minBodyY = bodyY;
            maxComY = sensors.comPosition.y;
            minComY = sensors.comPosition.y;
        }

        if (bodyY < minBodyY) minBodyY = bodyY;
        if (bodyY > maxBodyY) maxBodyY = bodyY;
        lastBodyY = bodyY;
        lastPitch = pitch;
        lastComOffset = comOff;
        lastLiftY = liftY;
        lastMode = mode;
        lastBothGrounded = sensors.bothGrounded;
        if (liftY > maxLiftY) maxLiftY = liftY;
        sumLiftY += liftY;
        sumAbsLiftY += Mathf.Abs(liftY);
        if (sensors.comPosition.y > maxComY) maxComY = sensors.comPosition.y;
        if (sensors.comPosition.y < minComY) minComY = sensors.comPosition.y;

        float absPitch = Mathf.Abs(pitch);
        if (absPitch > maxAbsPitch) maxAbsPitch = absPitch;
        float absOff = Mathf.Abs(comOff);
        if (absOff > maxAbsComOffset) maxAbsComOffset = absOff;
        sumComOffsetSquared += comOff * comOff;

        if (sensors.bothGrounded) samplesBothFeet++;
        if (!sensors.leftFootGrounded && !sensors.rightFootGrounded) samplesAirborne++;
        if (mode == (int)BirdMode.Sit) samplesSit++;
        else if (mode == (int)BirdMode.Walk) samplesWalk++;
        else if (mode == (int)BirdMode.Fly) samplesFly++;
        else if (mode == (int)BirdMode.Glide) samplesGlide++;
        else if (mode == (int)BirdMode.Peck) samplesPeck++;
        if (mode == (int)BirdMode.Fly
            || mode == (int)BirdMode.Glide
            || mode == (int)BirdMode.Attack)
            enteredFlight = true;
        if (control != null)
            lastFacingSign = control.FacingSign() >= 0f ? 1 : -1;
        if (LegSaturated()) samplesSaturated++;
        if (control != null && control.leftShoulderJoint != null)
        {
            float sh = Mathf.Abs(control.leftShoulderJoint.jointSpeed);
            if (sh > maxAbsShoulderSpeed) maxAbsShoulderSpeed = sh;
        }

        if (fallTime < 0f)
        {
            float drop = startBodyY - minBodyY;
            if (absPitch >= PITCH_FALL_DEGREES)
            {
                fallTime = time;
                fallReason = "pitch";
            }
            else if (!enteredFlight
                && drop >= BODY_DROP_FALL
                && sensors.bothGrounded
                && (mode <= 1 || mode == (int)BirdMode.Sit || mode == (int)BirdMode.Peck)
                && !sensors.nearPerch)
            {
                fallTime = time;
                fallReason = "bodyDrop";
            }
            else if (absOff >= COM_OFFSET_FALL)
            {
                fallTime = time;
                fallReason = "comOffset";
            }
        }

        float hipL = control != null && control.leftHipJoint != null ? control.leftHipJoint.jointAngle : 0f;
        float kneeL = control != null && control.leftKneeJoint != null ? control.leftKneeJoint.jointAngle : 0f;
        float hipR = control != null && control.rightHipJoint != null ? control.rightHipJoint.jointAngle : 0f;
        float kneeR = control != null && control.rightKneeJoint != null ? control.rightKneeJoint.jointAngle : 0f;
        lastHip = hipL;
        lastKnee = kneeL;

        AppendCsvFloat(time);
        rows.Append(',');
        AppendCsvFloat(pitch);
        rows.Append(',');
        AppendCsvFloat(bodyY);
        rows.Append(',');
        AppendCsvFloat(sensors.comPosition.x);
        rows.Append(',');
        AppendCsvFloat(sensors.comPosition.y);
        rows.Append(',');
        AppendCsvFloat(comOff);
        rows.Append(',').Append(sensors.leftFootGrounded ? '1' : '0');
        rows.Append(',').Append(sensors.rightFootGrounded ? '1' : '0');
        rows.Append(',');
        AppendCsvFloat(liftY);
        rows.Append(',');
        AppendCsvInt(mode);
        rows.Append(',');
        AppendCsvFloat(hipL);
        rows.Append(',');
        AppendCsvFloat(kneeL);
        rows.Append(',');
        AppendCsvFloat(hipR);
        rows.Append(',');
        AppendCsvFloat(kneeR);
        rows.AppendLine();

        samples++;
    }

    public string BuildCsv()
    {
        return rows.ToString();
    }

    public string BuildSummaryJson(float simulatedSeconds, float wallSeconds = -1f)
    {
        float bodyDrop = startBodyY - minBodyY;
        float rmsCom = samples > 0 ? Mathf.Sqrt((float)(sumComOffsetSquared / samples)) : 0f;
        float survived = fallTime >= 0f ? fallTime : simulatedSeconds;
        float meanLift = samples > 0 ? (float)(sumLiftY / samples) : 0f;

        StringBuilder json = new StringBuilder();
        json.Append("{");
        AppendStr(json, "subject", "bird", false);
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
        AppendStr(json, "fallReason", fallReason);
        AppendNum(json, "maxAbsBodyPitch", maxAbsPitch);
        AppendNum(json, "finalBodyPitch", lastPitch);
        AppendNum(json, "bodyDrop", bodyDrop);
        AppendNum(json, "startBodyY", startBodyY);
        AppendNum(json, "minBodyY", minBodyY);
        AppendNum(json, "finalBodyY", lastBodyY);
        AppendNum(json, "rmsComOffset", rmsCom);
        AppendNum(json, "maxAbsComOffset", maxAbsComOffset);
        AppendNum(json, "finalComOffset", lastComOffset);
        AppendNum(json, "bothFeetGroundedFraction", samples > 0 ? samplesBothFeet / (float)samples : 0f);
        AppendNum(json, "muscleSaturationFraction", samples > 0 ? samplesSaturated / (float)samples : 0f);
        AppendNum(json, "meanLiftY", meanLift);
        AppendNum(json, "meanAbsLiftY", samples > 0 ? (float)(sumAbsLiftY / samples) : 0f);
        AppendNum(json, "maxLiftY", maxLiftY);
        AppendNum(json, "maxBodyY", maxBodyY);
        AppendNum(json, "maxAbsShoulderSpeed", maxAbsShoulderSpeed);
        AppendNum(json, "airborneFraction", samples > 0 ? samplesAirborne / (float)samples : 0f);
        AppendNum(json, "minComY", minComY < float.MaxValue ? minComY : 0f);
        AppendNum(json, "maxComY", maxComY);
        AppendNum(json, "finalHipAngle", lastHip);
        AppendNum(json, "finalKneeAngle", lastKnee);
        json.Append(",\"finalMode\":").Append(lastMode.ToString(CultureInfo.InvariantCulture));
        AppendNum(json, "sitFraction", samples > 0 ? samplesSit / (float)samples : 0f);
        AppendNum(json, "walkFraction", samples > 0 ? samplesWalk / (float)samples : 0f);
        AppendNum(json, "flyFraction", samples > 0 ? samplesFly / (float)samples : 0f);
        AppendNum(json, "glideFraction", samples > 0 ? samplesGlide / (float)samples : 0f);
        AppendNum(json, "peckFraction", samples > 0 ? samplesPeck / (float)samples : 0f);
        json.Append(",\"enteredFlight\":").Append(enteredFlight ? "true" : "false");
        json.Append(",\"finalFacing\":").Append(lastFacingSign.ToString(CultureInfo.InvariantCulture));
        bool satOnPerch = lastMode == (int)BirdMode.Sit
            || lastMode == (int)BirdMode.Peck
            || lastMode <= 1;
        bool landedUpright = satOnPerch
            && Mathf.Abs(lastPitch) < 25f
            && lastBothGrounded
            && bodyDrop < 0.12f;
        json.Append(",\"landedUpright\":").Append(landedUpright ? "true" : "false");
        if (bird != null && bird.flight != null)
            AppendNum(json, "hoverMean", bird.flight.hoverMean);
        if (control != null)
        {
            AppendNum(json, "takeoffDelay", control.takeoffDelay);
            AppendNum(json, "flapFrequency", control.flapFrequency);
            AppendNum(json, "flapAmplitude", control.flapAmplitude);
            AppendNum(json, "flapStrokeGain", control.flapStrokeGain);
            AppendNum(json, "jointPGain", control.jointPGain);
            AppendNum(json, "pitchPGain", control.pitchPGain);
            AppendNum(json, "pitchTarget", control.pitchTarget);
        }
        if (info != null)
        {
            AppendNum(json, "duration", info.Duration);
            AppendNum(json, "startY", info.StartY);
            AppendStr(json, "sourceFingerprint", info.SourceFingerprint);
        }
        AppendApplied(json);
        json.Append("}");
        return json.ToString();
    }

    private void AppendApplied(StringBuilder json)
    {
        int bodyCount = 0;
        int continuous = 0;
        if (bird != null)
        {
            Rigidbody2D[] rbs = bird.GetComponentsInChildren<Rigidbody2D>();
            bodyCount = rbs.Length;
            for (int i = 0; i < rbs.Length; i++)
            {
                if (rbs[i] != null && rbs[i].collisionDetectionMode == CollisionDetectionMode2D.Continuous)
                    continuous++;
            }
        }

        int muscles = bird != null ? bird.GetComponentsInChildren<Muscle>().Length : 0;
        int frictions = bird != null ? bird.GetComponentsInChildren<JointFriction>().Length : 0;
        BirdSensors s = sensors;

        json.Append(",\"applied\":{");
        AppendStr(json, "birdKind", bird != null ? bird.kind.ToString() : "Crow", false);
        json.Append(",\"rigidbodyCount\":").Append(bodyCount.ToString(CultureInfo.InvariantCulture));
        json.Append(",\"continuousBodyCount\":").Append(continuous.ToString(CultureInfo.InvariantCulture));
        json.Append(",\"muscleBehaviourCount\":").Append(muscles.ToString(CultureInfo.InvariantCulture));
        json.Append(",\"frictionBehaviourCount\":").Append(frictions.ToString(CultureInfo.InvariantCulture));
        json.Append(",\"velocityIterations\":").Append(Physics2D.velocityIterations.ToString(CultureInfo.InvariantCulture));
        json.Append(",\"positionIterations\":").Append(Physics2D.positionIterations.ToString(CultureInfo.InvariantCulture));
        json.Append(",\"flockRig\":").Append(control != null && control.flockRig ? "true" : "false");
        BirdDrive[] drives = UnityEngine.Object.FindObjectsByType<BirdDrive>(FindObjectsSortMode.None);
        BirdFlockDrive flock = UnityEngine.Object.FindFirstObjectByType<BirdFlockDrive>();
        if (drives != null && drives.Length > 0)
        {
            int takeoffs = 0, lands = 0, flips = 0, perchLands = 0, dirt = 0;
            for (int i = 0; i < drives.Length; i++)
            {
                if (drives[i] == null) continue;
                takeoffs += drives[i].TakeoffCount;
                lands += drives[i].LandCount;
                flips += drives[i].FacingFlipCount;
                perchLands += drives[i].PerchLandCount;
                dirt += drives[i].DirtRejectCount;
            }

            BirdDrive drive = drives[0];
            json.Append(",\"birdDrive\":true");
            AppendStr(json, "birdDriveKind", drives.Length > 1 ? "WanderEach" : drive.kind.ToString());
            if (drives.Length > 1)
                json.Append(",\"flockMemberCount\":").Append(drives.Length.ToString(CultureInfo.InvariantCulture));
            json.Append(",\"takeoffCount\":").Append(takeoffs.ToString(CultureInfo.InvariantCulture));
            int walks = 0;
            for (int i = 0; i < drives.Length; i++)
            {
                if (drives[i] != null)
                    walks += drives[i].WalkCount;
            }
            json.Append(",\"walkCount\":").Append(walks.ToString(CultureInfo.InvariantCulture));
            int pecks = 0;
            for (int i = 0; i < drives.Length; i++)
            {
                if (drives[i] != null)
                    pecks += drives[i].PeckCount;
            }
            json.Append(",\"peckCount\":").Append(pecks.ToString(CultureInfo.InvariantCulture));
            json.Append(",\"landCount\":").Append(lands.ToString(CultureInfo.InvariantCulture));
            json.Append(",\"facingFlipCount\":").Append(flips.ToString(CultureInfo.InvariantCulture));
            json.Append(",\"perchLandCount\":").Append(perchLands.ToString(CultureInfo.InvariantCulture));
            json.Append(",\"dirtRejectCount\":").Append(dirt.ToString(CultureInfo.InvariantCulture));
            int cries = 0;
            for (int i = 0; i < drives.Length; i++)
            {
                if (drives[i] != null)
                    cries += drives[i].CryCount;
            }
            json.Append(",\"cryCount\":").Append(cries.ToString(CultureInfo.InvariantCulture));
            json.Append(",\"hasThreat\":").Append(drive.threat != null ? "true" : "false");
            AppendNum(json, "threatRange", drive.threatRange);
            AppendNum(json, "glideLead", drive.glideLead);
            AppendNum(json, "sitMin", drive.sitMin);
            AppendNum(json, "sitMax", drive.sitMax);
            AppendNum(json, "flyMin", drive.flyMin);
            AppendNum(json, "flyMax", drive.flyMax);
            AppendNum(json, "flockLeash", drive.leash);
            AppendNum(json, "homeFaceDeadzone", drive.homeFaceDeadzone);
        }
        else if (flock != null)
        {
            json.Append(",\"birdDrive\":true");
            AppendStr(json, "birdDriveKind", "WanderFlock");
            json.Append(",\"flockMemberCount\":").Append(flock.MemberCount.ToString(CultureInfo.InvariantCulture));
            json.Append(",\"takeoffCount\":").Append(flock.TakeoffCount.ToString(CultureInfo.InvariantCulture));
            json.Append(",\"walkCount\":").Append(flock.WalkCount.ToString(CultureInfo.InvariantCulture));
            json.Append(",\"peckCount\":").Append(flock.PeckCount.ToString(CultureInfo.InvariantCulture));
            json.Append(",\"landCount\":").Append(flock.LandCount.ToString(CultureInfo.InvariantCulture));
            json.Append(",\"facingFlipCount\":").Append(flock.FacingFlipCount.ToString(CultureInfo.InvariantCulture));
            json.Append(",\"perchLandCount\":").Append(flock.PerchLandCount.ToString(CultureInfo.InvariantCulture));
            json.Append(",\"dirtRejectCount\":").Append(flock.DirtRejectCount.ToString(CultureInfo.InvariantCulture));
            json.Append(",\"cryCount\":").Append(flock.CryCount.ToString(CultureInfo.InvariantCulture));
            json.Append(",\"hasThreat\":").Append(flock.threat != null ? "true" : "false");
            AppendNum(json, "threatRange", flock.threatRange);
            AppendNum(json, "glideLead", flock.glideLead);
            json.Append(",\"aiStride\":").Append(flock.aiStride.ToString(CultureInfo.InvariantCulture));
            AppendNum(json, "sitMin", flock.sitMin);
            AppendNum(json, "sitMax", flock.sitMax);
            AppendNum(json, "flyMin", flock.flyMin);
            AppendNum(json, "flyMax", flock.flyMax);
            AppendNum(json, "flockLeash", flock.flockLeash);
            AppendNum(json, "homeFaceDeadzone", flock.homeFaceDeadzone);
        }
        else
            json.Append(",\"birdDrive\":false");
        int segments = bird != null ? bird.GetComponentsInChildren<HumanSegment>(true).Length : 0;
        json.Append(",\"humanSegmentCount\":").Append(segments.ToString(CultureInfo.InvariantCulture));
        json.Append(",\"hasBeak\":").Append(bird != null && bird.beakCollider != null ? "true" : "false");
        BeakStrike strike = bird != null ? bird.GetComponent<BeakStrike>() : null;
        json.Append(",\"beakHitCount\":").Append((strike != null ? strike.HitCount : 0).ToString(CultureInfo.InvariantCulture));
        if (bird != null)
            AppendNum(json, "beakSizeX", bird.beakSize.x);
        if (s != null && s.probeCallCount > 0)
        {
            AppendNum(json, "meanProbeHits", s.probeHitSum / (float)s.probeCallCount);
            AppendNum(json, "probeSaturatedFraction", s.probeSaturatedCount / (float)s.probeCallCount);
            json.Append(",\"probeCallCount\":").Append(s.probeCallCount.ToString(CultureInfo.InvariantCulture));
        }
        json.Append("}");
    }

    private bool LegSaturated()
    {
        if (control == null) return false;
        float max = 0f;
        IncludeActivation(ref max, control.leftHipFlexor);
        IncludeActivation(ref max, control.leftHipExtensor);
        IncludeActivation(ref max, control.rightHipFlexor);
        IncludeActivation(ref max, control.rightHipExtensor);
        IncludeActivation(ref max, control.leftKneeFlexor);
        IncludeActivation(ref max, control.leftKneeExtensor);
        IncludeActivation(ref max, control.rightKneeFlexor);
        IncludeActivation(ref max, control.rightKneeExtensor);
        return max >= SATURATION_THRESHOLD;
    }

    private static void IncludeActivation(ref float max, Muscle muscle)
    {
        if (muscle != null && muscle.activation > max)
            max = muscle.activation;
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

    private void AppendCsvInt(int value)
    {
        Span<char> buffer = numberBuffer;
        if (value.TryFormat(buffer, out int written, default, CultureInfo.InvariantCulture))
        {
            rows.Append(numberBuffer, 0, written);
            return;
        }

        rows.Append(value.ToString(CultureInfo.InvariantCulture));
    }

    private static void AppendStr(StringBuilder json, string key, string value, bool comma = true)
    {
        if (comma) json.Append(',');
        json.Append('"').Append(key).Append("\":\"");
        if (!string.IsNullOrEmpty(value))
            json.Append(value.Replace("\\", "\\\\").Replace("\"", "\\\""));
        json.Append('"');
    }

    private static void AppendNum(StringBuilder json, string key, float value)
    {
        json.Append(",\"").Append(key).Append("\":");
        json.Append(value.ToString("G9", CultureInfo.InvariantCulture));
    }
}
