using System;
using System.Globalization;
using System.Text;
using UnityEngine;

// Контекст одного прогона: что запросили и какой билд проверили.
// Fingerprint берётся из build-manifest.json, не пересчитывается из исходников плеера.
public sealed class TrialRunInfo
{
    public string Label = "trial";
    public float Duration;
    public float StartY = -0.82f;
    public float RequestedPushImpulse;
    public float RequestedPushTime = 5f;
    public float MuscleMultiplier = 1f;
    public float FrictionMultiplier = 1f;
    public string SourceFingerprint = "";
    public string ManifestUnityVersion = "";
    public string ManifestBuildGuid = "";
    public string ManifestBuildTimeUtc = "";
    public string ManifestPath = "";
    public string[] CommandArgs = Array.Empty<string>();
}

// Снимает состояние человека на каждом физическом шаге прогона и считает
// сводные метрики. Ничего не прикладывает к телу — только читает,
// как и остальной сенсорный слой.
public class TrialRecorder
{
    // Активацию считаем насыщенной чуть ниже единицы: PD-регулятор
    // упирается в потолок и дальше теряет управляемость.
    private const float SATURATION_THRESHOLD = 0.999f;

    // Считаем, что человек вернулся в равновесие, когда смещение центра масс
    // снова укладывается в сантиметр: в спокойной позе оно около 7 мм.
    private const float SETTLED_COM_OFFSET = 0.01f;

    private readonly Human human;
    private readonly string label;
    private readonly TrialRunInfo info;

    private readonly BalanceController balance;
    private readonly BodyStateEstimator state;
    private readonly CenterOfMassCalculator com;
    private readonly VestibularSystem vestibular;
    private readonly Transform head;

    private readonly StringBuilder rows = new StringBuilder();

    private int samples;
    private double sumComOffsetSquared;
    private double sumAbsComOffset;
    private double sumTorsoAngVelSquared;
    private float maxAbsTilt;
    private float fallTime = -1f;
    private int samplesBalancing;
    private int samplesRecovery;
    private int samplesFalling;
    private int samplesSaturated;
    private int samplesBothFeet;
    private float startHeadY;
    private float minHeadY = float.MaxValue;
    private float lastHeadY;
    private float lastTilt;
    private float lastComOffset;
    private float lastPelvisTilt;
    private float lastLumbarAngle;
    private float maxAbsLumbarAngle;
    private float maxAbsPelvisTilt;

    private float pushTime = -1f;
    private float pushImpulse;
    private float maxAbsComOffsetAfterPush;
    private float maxAbsTiltAfterPush;
    private float lastUnsettledTime = -1f;

    public TrialRecorder(Human human, string label)
        : this(human, new TrialRunInfo { Label = label })
    {
    }

    public TrialRecorder(Human human, TrialRunInfo info)
    {
        this.human = human;
        this.info = info ?? new TrialRunInfo();
        this.label = this.info.Label ?? "trial";

        balance = human.GetComponent<BalanceController>();
        state = human.GetComponent<BodyStateEstimator>();
        com = human.GetComponent<CenterOfMassCalculator>();
        vestibular = human.GetComponent<VestibularSystem>();
        head = human.headSegment != null ? human.headSegment.transform : null;

        startHeadY = head != null ? head.position.y : 0f;
        lastHeadY = startHeadY;

        rows.AppendLine(string.Join(";", new[]
        {
            "time", "state", "torsoTilt", "torsoAngVel", "pelvisTilt", "comX", "comOffset", "supportMargin",
            "leftGrounded", "rightGrounded", "headY",
            "hipL", "hipR", "kneeL", "kneeR", "ankleL", "ankleR", "lumbar",
            "actHipL", "actHipR", "actKneeL", "actKneeR", "actAnkleL", "actAnkleR"
        }));
    }

    // Вызывается стендом в момент толчка, чтобы отделить метрики восстановления
    // от того, что происходило до возмущения.
    public void MarkPush(float time, float impulse)
    {
        pushTime = time;
        pushImpulse = impulse;
    }

    // Вызывается после каждого физического шага.
    public void Sample(float time)
    {
        samples++;

        float tilt = vestibular != null ? vestibular.GetBodyTilt() : 0f;
        float angVel = vestibular != null ? vestibular.GetBodyAngularVelocity() : 0f;
        float comOffset = com != null ? com.GetCoMOffsetX() : 0f;
        Vector2 comPosition = com != null ? com.GetCenterOfMass() : Vector2.zero;
        float headY = head != null ? head.position.y : 0f;

        float supportMargin = state != null ? state.supportMargin : 0f;
        bool leftGrounded = state != null && state.leftFootGrounded;
        bool rightGrounded = state != null && state.rightFootGrounded;

        float actHipL = MaxActivation(balance?.leftHipFlexor, balance?.leftHipExtensor);
        float actHipR = MaxActivation(balance?.rightHipFlexor, balance?.rightHipExtensor);
        float actKneeL = MaxActivation(balance?.leftKneeFlexor, balance?.leftKneeExtensor);
        float actKneeR = MaxActivation(balance?.rightKneeFlexor, balance?.rightKneeExtensor);
        float actAnkleL = MaxActivation(balance?.leftAnkleFlexor, balance?.leftAnkleExtensor);
        float actAnkleR = MaxActivation(balance?.rightAnkleFlexor, balance?.rightAnkleExtensor);

        BalanceController.BalanceState currentState = balance != null
            ? balance.currentState
            : BalanceController.BalanceState.Balancing;

        // ─── НАКОПЛЕНИЕ МЕТРИК ───
        sumComOffsetSquared += (double)comOffset * comOffset;
        sumAbsComOffset += Mathf.Abs(comOffset);
        sumTorsoAngVelSquared += (double)angVel * angVel;
        maxAbsTilt = Mathf.Max(maxAbsTilt, Mathf.Abs(tilt));
        minHeadY = Mathf.Min(minHeadY, headY);
        lastHeadY = headY;
        lastTilt = tilt;
        lastComOffset = comOffset;
        float lumbar = state != null ? state.lumbarAngle : 0f;
        float pelvisTilt = state != null ? state.pelvisTilt : 0f;
        lastLumbarAngle = lumbar;
        lastPelvisTilt = pelvisTilt;
        maxAbsLumbarAngle = Mathf.Max(maxAbsLumbarAngle, Mathf.Abs(lumbar));
        maxAbsPelvisTilt = Mathf.Max(maxAbsPelvisTilt, Mathf.Abs(pelvisTilt));

        if (pushTime >= 0f && time >= pushTime)
        {
            maxAbsComOffsetAfterPush = Mathf.Max(maxAbsComOffsetAfterPush, Mathf.Abs(comOffset));
            maxAbsTiltAfterPush = Mathf.Max(maxAbsTiltAfterPush, Mathf.Abs(tilt));
            if (Mathf.Abs(comOffset) > SETTLED_COM_OFFSET) lastUnsettledTime = time;
        }

        switch (currentState)
        {
            case BalanceController.BalanceState.Balancing: samplesBalancing++; break;
            case BalanceController.BalanceState.Recovery: samplesRecovery++; break;
            case BalanceController.BalanceState.Falling: samplesFalling++; break;
        }

        if (currentState == BalanceController.BalanceState.Falling && fallTime < 0f)
            fallTime = time;

        if (Mathf.Max(Mathf.Max(actHipL, actHipR), Mathf.Max(actKneeL, actKneeR)) >= SATURATION_THRESHOLD)
            samplesSaturated++;

        if (leftGrounded && rightGrounded) samplesBothFeet++;

        rows.AppendLine(string.Join(";", new[]
        {
            F(time), ((int)currentState).ToString(CultureInfo.InvariantCulture),
            F(tilt), F(angVel), F(state != null ? state.pelvisTilt : 0f), F(comPosition.x), F(comOffset), F(supportMargin),
            leftGrounded ? "1" : "0", rightGrounded ? "1" : "0", F(headY),
            F(state != null ? state.leftHipAngle : 0f), F(state != null ? state.rightHipAngle : 0f),
            F(state != null ? state.leftKneeAngle : 0f), F(state != null ? state.rightKneeAngle : 0f),
            F(state != null ? state.leftAnkleAngle : 0f), F(state != null ? state.rightAnkleAngle : 0f),
            F(state != null ? state.lumbarAngle : 0f),
            F(actHipL), F(actHipR), F(actKneeL), F(actKneeR), F(actAnkleL), F(actAnkleR)
        }));
    }

    public string BuildCsv()
    {
        return rows.ToString();
    }

    public string BuildSummaryJson(float simulatedSeconds)
    {
        float rmsComOffset = samples > 0 ? Mathf.Sqrt((float)(sumComOffsetSquared / samples)) : 0f;
        float meanAbsComOffset = samples > 0 ? (float)(sumAbsComOffset / samples) : 0f;
        float rmsTorsoAngVel = samples > 0 ? Mathf.Sqrt((float)(sumTorsoAngVelSquared / samples)) : 0f;
        float survived = fallTime >= 0f ? fallTime : simulatedSeconds;
        float headDrop = startHeadY - lastHeadY;
        // Сколько секунд после толчка смещение центра масс не влезало в норму.
        float recoverySeconds = pushTime >= 0f && lastUnsettledTime >= 0f
            ? lastUnsettledTime - pushTime
            : 0f;

        StringBuilder json = new StringBuilder();
        json.Append("{");
        AppendStr(json, "label", label, false);
        AppendNum(json, "simulatedSeconds", simulatedSeconds);
        json.Append(",\"samples\":").Append(samples.ToString(CultureInfo.InvariantCulture));
        AppendNum(json, "fixedDeltaTime", Time.fixedDeltaTime);
        AppendNum(json, "muscleActivationSpeed", balance != null ? balance.muscleActivationSpeed : 0f);
        AppendNum(json, "survivedSeconds", survived);
        json.Append(",\"fell\":").Append(fallTime >= 0f ? "true" : "false");
        AppendNum(json, "maxAbsTorsoTilt", maxAbsTilt);
        AppendNum(json, "maxAbsLumbarAngle", maxAbsLumbarAngle);
        AppendNum(json, "maxAbsPelvisTilt", maxAbsPelvisTilt);
        AppendNum(json, "finalPelvisTilt", lastPelvisTilt);
        AppendNum(json, "finalLumbarAngle", lastLumbarAngle);
        AppendNum(json, "finalTorsoTilt", lastTilt);
        AppendNum(json, "rmsComOffset", rmsComOffset);
        AppendNum(json, "rmsTorsoAngVel", rmsTorsoAngVel);
        AppendNum(json, "meanAbsComOffset", meanAbsComOffset);
        AppendNum(json, "finalComOffset", lastComOffset);
        AppendNum(json, "headDrop", headDrop);
        AppendNum(json, "minHeadY", minHeadY);
        AppendNum(json, "pushImpulse", pushImpulse);
        AppendNum(json, "pushTime", pushTime);
        AppendNum(json, "maxComOffsetAfterPush", maxAbsComOffsetAfterPush);
        AppendNum(json, "maxTiltAfterPush", maxAbsTiltAfterPush);
        AppendNum(json, "recoverySeconds", recoverySeconds);
        AppendNum(json, "muscleSaturationFraction", Fraction(samplesSaturated));
        AppendNum(json, "bothFeetGroundedFraction", Fraction(samplesBothFeet));
        AppendNum(json, "balancingFraction", Fraction(samplesBalancing));
        AppendNum(json, "recoveryFraction", Fraction(samplesRecovery));
        AppendNum(json, "fallingFraction", Fraction(samplesFalling));
        AppendNum(json, "muscleMultiplier", human.muscleMultiplier);
        AppendNum(json, "frictionMultiplier", human.frictionMultiplier);
        AppendNum(json, "lumbarFrictionMaxTorque", human.lumbarFrictionMaxTorque);
        AppendNum(json, "hipPGain", balance != null ? balance.hipPGain : 0f);
        AppendNum(json, "kneePGain", balance != null ? balance.kneePGain : 0f);
        AppendNum(json, "ankleComD", balance != null ? balance.ankleComD : 0f);
        AppendNum(json, "kneeBaseAngle", balance != null ? balance.kneeBaseAngle : 0f);
        AppendNum(json, "ankleComP", balance != null ? balance.ankleComP : 0f);
        AppendNum(json, "comProportionalGain", balance != null ? balance.comProportionalGain : 0f);

        AppendNum(json, "duration", info.Duration);
        AppendNum(json, "startY", info.StartY);
        AppendNum(json, "hipBaseAngle", balance != null ? balance.hipBaseAngle : 0f);
        AppendNum(json, "hipDGain", balance != null ? balance.hipDGain : 0f);
        AppendNum(json, "kneeDGain", balance != null ? balance.kneeDGain : 0f);
        AppendNum(json, "comDerivativeGain", balance != null ? balance.comDerivativeGain : 0f);
        AppendNum(json, "hipBalanceGain", balance != null ? balance.hipBalanceGain : 0f);
        AppendNum(json, "lumbarPGain", balance != null ? balance.lumbarPGain : 0f);
        AppendNum(json, "lumbarDGain", balance != null ? balance.lumbarDGain : 0f);
        AppendNum(json, "lumbarTargetTilt", balance != null ? balance.lumbarTargetTilt : 0f);
        AppendNum(json, "lumbarErrorReferenceDegrees", balance != null ? balance.lumbarErrorReferenceDegrees : 0f);
        AppendNum(json, "lumbarFriction", human.lumbarFriction);
        AppendNum(json, "recoveryCoMOffset", balance != null ? balance.recoveryCoMOffset : 0f);
        AppendNum(json, "fallCoMOffset", balance != null ? balance.fallCoMOffset : 0f);
        AppendStr(json, "unityVersion", Application.unityVersion);
        AppendStr(json, "buildGuid", Application.buildGUID);
        AppendStr(json, "sourceFingerprint", info.SourceFingerprint ?? "");
        AppendStr(json, "muscleSaturationScope", "hips,knees");

        json.Append(",\"inputs\":{");
        AppendStr(json, "label", info.Label ?? "", false);
        AppendNum(json, "duration", info.Duration);
        AppendNum(json, "startY", info.StartY);
        AppendNum(json, "pushImpulse", info.RequestedPushImpulse);
        AppendNum(json, "pushTime", info.RequestedPushTime);
        AppendNum(json, "fixedDelta", Time.fixedDeltaTime);
        AppendNum(json, "muscle", info.MuscleMultiplier);
        AppendNum(json, "friction", info.FrictionMultiplier);
        json.Append("}");

        json.Append(",\"meta\":{");
        AppendStr(json, "unityVersion", Application.unityVersion, false);
        AppendStr(json, "buildGuid", Application.buildGUID);
        AppendStr(json, "sourceFingerprint", info.SourceFingerprint ?? "");
        AppendStr(json, "manifestUnityVersion", info.ManifestUnityVersion ?? "");
        AppendStr(json, "manifestBuildGuid", info.ManifestBuildGuid ?? "");
        AppendStr(json, "manifestBuildTimeUtc", info.ManifestBuildTimeUtc ?? "");
        AppendStr(json, "buildManifestPath", info.ManifestPath ?? "");
        json.Append("}");

        AppendCommandArgs(json, info.CommandArgs);
        AppendApplied(json, human, balance);

        json.Append("}");
        return json.ToString();
    }

    private void AppendApplied(StringBuilder json, Human h, BalanceController b)
    {
        float actualMass;
        int bodyCount;
        SumRigidbodies(h, out actualMass, out bodyCount);

        json.Append(",\"applied\":{");
        AppendNum(json, "totalMass", h.totalMass, false);
        AppendNum(json, "actualRigidbodyMass", actualMass);
        json.Append(",\"rigidbodyCount\":").Append(bodyCount.ToString(CultureInfo.InvariantCulture));
        AppendNum(json, "muscleMultiplier", h.muscleMultiplier);
        AppendNum(json, "frictionMultiplier", h.frictionMultiplier);
        AppendNum(json, "viewAngleDegrees", h.viewAngleDegrees);
        AppendNum(json, "hipHalfSpacing", h.hipHalfSpacing);
        AppendNum(json, "shoulderHalfSpacing", h.shoulderHalfSpacing);
        AppendNum(json, "ankleHeelOffset", h.ankleHeelOffset);
        AppendNum(json, "shoulderDropFromNeck", h.shoulderDropFromNeck);
        AppendVec2(json, "pelvisSize", h.pelvisSize);
        AppendVec2(json, "torsoSize", h.torsoSize);
        AppendVec2(json, "headSize", h.headSize);
        AppendVec2(json, "neckSize", h.neckSize);
        AppendVec2(json, "upperArmSize", h.upperArmSize);
        AppendVec2(json, "lowerArmSize", h.lowerArmSize);
        AppendVec2(json, "handSize", h.handSize);
        AppendVec2(json, "thighSize", h.thighSize);
        AppendVec2(json, "shinSize", h.shinSize);
        AppendVec2(json, "footSize", h.footSize);
        AppendNum(json, "lumbarFriction", h.lumbarFriction);
        AppendNum(json, "lumbarFrictionMaxTorque", h.lumbarFrictionMaxTorque);
        AppendNum(json, "hipFriction", h.hipFriction);
        AppendNum(json, "kneeFriction", h.kneeFriction);
        AppendNum(json, "ankleFriction", h.ankleFriction);
        AppendNum(json, "neckFriction", h.neckFriction);
        AppendNum(json, "shoulderFriction", h.shoulderFriction);
        AppendNum(json, "elbowFriction", h.elbowFriction);
        AppendNum(json, "wristFriction", h.wristFriction);
        AppendNum(json, "lumbarMuscleTorque", h.lumbarMuscleTorque);
        AppendNum(json, "hipMuscleTorque", h.hipMuscleTorque);
        AppendNum(json, "kneeMuscleTorque", h.kneeMuscleTorque);
        AppendNum(json, "ankleExtensorTorque", h.ankleExtensorTorque);
        AppendNum(json, "ankleFlexorTorque", h.ankleFlexorTorque);
        AppendNum(json, "neckMuscleTorque", h.neckMuscleTorque);
        AppendNum(json, "shoulderMuscleTorque", h.shoulderMuscleTorque);
        AppendNum(json, "elbowMuscleTorque", h.elbowMuscleTorque);
        AppendNum(json, "wristMuscleTorque", h.wristMuscleTorque);
        if (b != null)
        {
            AppendNum(json, "hipBaseAngle", b.hipBaseAngle);
            AppendNum(json, "kneeBaseAngle", b.kneeBaseAngle);
            AppendNum(json, "kneeRecoveryFlex", b.kneeRecoveryFlex);
            AppendNum(json, "hipPGain", b.hipPGain);
            AppendNum(json, "hipDGain", b.hipDGain);
            AppendNum(json, "kneePGain", b.kneePGain);
            AppendNum(json, "kneeDGain", b.kneeDGain);
            AppendNum(json, "hipBalanceGain", b.hipBalanceGain);
            AppendNum(json, "lumbarPGain", b.lumbarPGain);
            AppendNum(json, "lumbarDGain", b.lumbarDGain);
            AppendNum(json, "lumbarTargetTilt", b.lumbarTargetTilt);
            AppendNum(json, "lumbarErrorReferenceDegrees", b.lumbarErrorReferenceDegrees);
            AppendNum(json, "ankleComP", b.ankleComP);
            AppendNum(json, "ankleComD", b.ankleComD);
            AppendNum(json, "ankleLimitMargin", b.ankleLimitMargin);
            AppendNum(json, "comOffsetReference", b.comOffsetReference);
            AppendNum(json, "tiltSpeedReference", b.tiltSpeedReference);
            AppendNum(json, "comVelocityReference", b.comVelocityReference);
            AppendNum(json, "comProportionalGain", b.comProportionalGain);
            AppendNum(json, "comDerivativeGain", b.comDerivativeGain);
            AppendNum(json, "maxBalanceSignal", b.maxBalanceSignal);
            AppendNum(json, "errorReferenceDegrees", b.errorReferenceDegrees);
            AppendNum(json, "speedReferenceDegPerSec", b.speedReferenceDegPerSec);
            AppendNum(json, "recoveryCoMOffset", b.recoveryCoMOffset);
            AppendNum(json, "fallCoMOffset", b.fallCoMOffset);
            AppendNum(json, "muscleActivationSpeed", b.muscleActivationSpeed);
            AppendNum(json, "neckPGain", b.neckPGain);
            AppendNum(json, "neckDGain", b.neckDGain);
        }

        AppendJoints(json, h);
        json.Append("}");
    }

    private static void SumRigidbodies(Human h, out float mass, out int count)
    {
        mass = 0f;
        count = 0;
        if (h == null) return;
        Rigidbody2D[] bodies = h.GetComponentsInChildren<Rigidbody2D>();
        for (int i = 0; i < bodies.Length; i++)
        {
            if (bodies[i] == null) continue;
            mass += bodies[i].mass;
            count++;
        }
    }

    private static void AppendJoints(StringBuilder json, Human h)
    {
        json.Append(",\"joints\":{");
        bool first = true;
        first = AppendNamedJoint(json, "lumbar", FindJoint(h, "Torso"), first);
        first = AppendNamedJoint(json, "neck", FindJoint(h, "Neck"), first);
        first = AppendNamedJoint(json, "head", FindJoint(h, "Head"), first);
        first = AppendSymmetricJoint(json, "shoulder", FindJoint(h, "LeftArmUpper"), FindJoint(h, "RightArmUpper"), first);
        first = AppendSymmetricJoint(json, "elbow", FindJoint(h, "LeftArmLower"), FindJoint(h, "RightArmLower"), first);
        first = AppendSymmetricJoint(json, "wrist", FindJoint(h, "LeftArmHand"), FindJoint(h, "RightArmHand"), first);
        first = AppendSymmetricJoint(json, "hip", FindJoint(h, "LeftLegThigh"), FindJoint(h, "RightLegThigh"), first);
        first = AppendSymmetricJoint(json, "knee", FindJoint(h, "LeftLegShin"), FindJoint(h, "RightLegShin"), first);
        AppendSymmetricJoint(json, "ankle", FindJoint(h, "LeftLegFoot"), FindJoint(h, "RightLegFoot"), first);
        json.Append("}");
    }

    private static HingeJoint2D FindJoint(Human h, string childName)
    {
        if (h == null) return null;
        Transform t = h.transform.Find(childName);
        return t != null ? t.GetComponent<HingeJoint2D>() : null;
    }

    private static bool SameLimits(HingeJoint2D a, HingeJoint2D b)
    {
        if (a == null || b == null) return false;
        return a.useLimits == b.useLimits
            && Mathf.Approximately(a.limits.min, b.limits.min)
            && Mathf.Approximately(a.limits.max, b.limits.max);
    }

    private static bool AppendSymmetricJoint(StringBuilder json, string name, HingeJoint2D left, HingeJoint2D right, bool first)
    {
        if (SameLimits(left, right))
            return AppendNamedJoint(json, name, left, first);

        first = AppendNamedJoint(json, name + "Left", left, first);
        return AppendNamedJoint(json, name + "Right", right, first);
    }

    private static bool AppendNamedJoint(StringBuilder json, string name, HingeJoint2D joint, bool first)
    {
        if (joint == null) return first;
        if (!first) json.Append(',');
        json.Append('"').Append(name).Append("\":{");
        json.Append("\"useLimits\":").Append(joint.useLimits ? "true" : "false");
        json.Append(",\"min\":").Append(F(joint.limits.min));
        json.Append(",\"max\":").Append(F(joint.limits.max));
        json.Append('}');
        return false;
    }

    private static void AppendVec2(StringBuilder json, string key, Vector2 value, bool comma = true)
    {
        if (comma) json.Append(',');
        json.Append('"').Append(key).Append("\":{\"x\":").Append(F(value.x)).Append(",\"y\":").Append(F(value.y)).Append('}');
    }

    private static void AppendCommandArgs(StringBuilder json, string[] args)
    {
        json.Append(",\"commandArgs\":[");
        if (args != null)
        {
            for (int i = 0; i < args.Length; i++)
            {
                if (i > 0) json.Append(',');
                json.Append('"').Append(Escape(args[i])).Append('"');
            }
        }
        json.Append(']');
    }

    private float Fraction(int count)
    {
        return samples > 0 ? count / (float)samples : 0f;
    }

    private static float MaxActivation(Muscle flexor, Muscle extensor)
    {
        float a = flexor != null ? flexor.activation : 0f;
        float b = extensor != null ? extensor.activation : 0f;
        return Mathf.Max(a, b);
    }

    private static void AppendNum(StringBuilder json, string key, float value, bool comma = true)
    {
        if (comma) json.Append(',');
        json.Append('"').Append(key).Append("\":").Append(F(value));
    }

    private static void AppendStr(StringBuilder json, string key, string value, bool comma = true)
    {
        if (comma) json.Append(',');
        json.Append('"').Append(key).Append("\":\"").Append(Escape(value ?? "")).Append('"');
    }

    private static string Escape(string value)
    {
        if (string.IsNullOrEmpty(value)) return "";

        StringBuilder sb = new StringBuilder(value.Length + 8);
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            switch (c)
            {
                case '\\': sb.Append("\\\\"); break;
                case '"': sb.Append("\\\""); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (c < ' ')
                        sb.AppendFormat(CultureInfo.InvariantCulture, "\\u{0:x4}", (int)c);
                    else
                        sb.Append(c);
                    break;
            }
        }

        return sb.ToString();
    }

    private static string F(float value)
    {
        return value.ToString("F4", CultureInfo.InvariantCulture);
    }
}
