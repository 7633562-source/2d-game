using System;
using System.Globalization;
using System.Text;
using UnityEngine;

// Context of one run: what was requested and which build was checked.
// Fingerprint comes from build-manifest.json; it is not recomputed from the player sources.
public sealed class TrialRunInfo
{
    public string Label = "trial";
    public float Duration;
    public float StartY = -0.82f;
    public float RequestedPushImpulse;
    public float RequestedPushTime = 5f;
    public float RequestedCrouch;
    public float RequestedCrouchTime = 5f;
    public float RequestedCrouchHold = 10f;
    public float RequestedLean;
    public float RequestedLeanTime = 5f;
    public float RequestedLeanHold = 10f;
    public float RequestedStandLeg;
    public float RequestedStandLegTime = 5f;
    public float RequestedStandLegHold = 10f;
    public float RequestedWalk;
    public float RequestedWalkTime = 5f;
    public float RequestedWalkDuration = 30f;
    public float RequestedWalkStance = 8f;
    public float RequestedRun;
    public float RequestedRunTime = 5f;
    public float RequestedRunHold = 20f;
    public float RequestedWalkTransfer = 8f;
    public float RequestedWalkTransferCom = 0.10f;
    public float RequestedWalkTransferFallbackCom = 0.12f;
    public float RequestedWalkTransferLevel = 0.05f;
    public float RequestedWalkFirst = 1f;
    public float MuscleMultiplier = 1f;
    public float FrictionMultiplier = 1f;
    public string SourceFingerprint = "";
    public string ManifestUnityVersion = "";
    public string ManifestBuildGuid = "";
    public string ManifestBuildTimeUtc = "";
    public string ManifestPath = "";
    public string[] CommandArgs = Array.Empty<string>();
}

// Samples the human on every physics step of the run and computes
// summary metrics. Applies nothing to the body — read-only,
// same as the rest of the sensor layer.
public class TrialRecorder
{
    // Activation is saturated just below one: the PD controller
    // hits the ceiling and then loses authority.
    private const float SATURATION_THRESHOLD = 0.999f;

    // The human is settled when the CoM offset
    // fits back into a centimetre: in quiet stance it is about 7 mm.
    private const float SETTLED_COM_OFFSET = 0.01f;

    // After crouch release the pelvis should rise almost to the start
    // height. 5 cm is more than stance sag and less than a visible squat.
    private const float PELVIS_RECOVER_TOLERANCE = 0.05f;

    // A joint is at the stop if the angle is within 1° of min/max. This is not force, it is
    // geometry: arms and head stick at the limit when K is too small.
    private const float LIMIT_MARGIN_DEGREES = 1f;

    private readonly Human human;
    private readonly string label;
    private readonly TrialRunInfo info;

    private readonly BalanceController balance;
    private readonly BodyStateEstimator state;
    private readonly CenterOfMassCalculator com;
    private readonly VestibularSystem vestibular;
    private readonly Transform head;
    private readonly Transform pelvis;
    private readonly Collider2D leftHandCollider;
    private readonly Collider2D rightHandCollider;

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
    private float minHandY = float.MaxValue;
    private float lastHeadY;
    private float startPelvisY;
    private float minPelvisY = float.MaxValue;
    private float lastPelvisY;
    private float minHipAngle = float.MaxValue;
    private float maxHipAngle = float.MinValue;
    private float minKneeAngle = float.MaxValue;
    private float maxKneeAngle = float.MinValue;
    private float maxAbsHipAngle;
    private float maxAbsKneeAngle;
    private float maxCrouchLevel;
    private float maxStandLegLevel;

    private float crouchStartTime = -1f;
    private float crouchReleaseTime = -1f;
    private float requestedCrouchLevel;
    private int samplesCrouchWindow;
    private int samplesBothFeetCrouch;
    private int samplesBothHandsCrouch;
    private int samplesLeftHandCrouch;
    private int samplesRightHandCrouch;

    private float standLegStartTime = -1f;
    private float standLegReleaseTime = -1f;
    private float requestedStandLeg;
    private int samplesStandLegWindow;
    private int samplesBothFeetStandLeg;
    private int samplesSwingGrounded;
    private int samplesStanceGrounded;
    private int samplesLeftGrounded;
    private int samplesRightGrounded;
    private float lastTilt;
    private float lastComOffset;
    // Distance travelled by the CoM along X. Without it a support swap counted
    // as walking while the body stayed put: 0.21 m in 35 s is marching in place.
    private float startComX;
    private float lastComX;
    private bool hasStartComX;
    private float lastPelvisTilt;
    private float lastLumbarAngle;
    private float maxAbsLumbarAngle;
    private float maxAbsPelvisTilt;

    private float pushTime = -1f;
    private float pushImpulse;
    private float maxAbsComOffsetAfterPush;
    private float maxAbsTiltAfterPush;
    private float lastUnsettledTime = -1f;

    // Passive viscosity of neck/head/arms: snapshot of Ieff and RMS relative
    // speed. Does not affect torques — only reads the already built body.
    private readonly JointDampingWatch neckWatch;
    private readonly JointDampingWatch headWatch;
    private readonly JointDampingWatch shoulderWatch;
    private readonly JointDampingWatch elbowWatch;
    private readonly JointDampingWatch wristWatch;

    private StepPhaseDriver stepDriver;
    private MotionIntent motionIntent;
    private int maxWalkSwapCount;
    private int lastWalkPhase;
    private float lastStandLegCmd;

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
        Transform pelvisTransform = human.transform.Find("Pelvis");
        pelvis = pelvisTransform;
        Transform leftHandT = human.transform.Find("LeftArmHand");
        Transform rightHandT = human.transform.Find("RightArmHand");
        leftHandCollider = leftHandT != null ? leftHandT.GetComponent<Collider2D>() : null;
        rightHandCollider = rightHandT != null ? rightHandT.GetComponent<Collider2D>() : null;

        startHeadY = head != null ? head.position.y : 0f;
        lastHeadY = startHeadY;
        startPelvisY = pelvis != null ? pelvis.position.y : 0f;
        lastPelvisY = startPelvisY;
        minPelvisY = startPelvisY;

        neckWatch = JointDampingWatch.Single("neck", FindJoint(human, "Neck"));
        headWatch = JointDampingWatch.Single("head", FindJoint(human, "Head"));
        shoulderWatch = JointDampingWatch.Pair(
            "shoulder",
            FindJoint(human, "LeftArmUpper"),
            FindJoint(human, "RightArmUpper"));
        elbowWatch = JointDampingWatch.Pair(
            "elbow",
            FindJoint(human, "LeftArmLower"),
            FindJoint(human, "RightArmLower"));
        wristWatch = JointDampingWatch.Pair(
            "wrist",
            FindJoint(human, "LeftArmHand"),
            FindJoint(human, "RightArmHand"));

        stepDriver = human.GetComponent<StepPhaseDriver>();
        motionIntent = human.GetComponent<MotionIntent>();

        // Header into the same rows: Join+new[] would allocate an array and an intermediate string for nothing.
        rows.Append("time").Append(';')
            .Append("state").Append(';')
            .Append("torsoTilt").Append(';')
            .Append("torsoAngVel").Append(';')
            .Append("pelvisTilt").Append(';')
            .Append("comX").Append(';')
            .Append("comOffset").Append(';')
            .Append("supportMargin").Append(';')
            .Append("leftGrounded").Append(';')
            .Append("rightGrounded").Append(';')
            .Append("headY").Append(';')
            .Append("hipL").Append(';')
            .Append("hipR").Append(';')
            .Append("kneeL").Append(';')
            .Append("kneeR").Append(';')
            .Append("ankleL").Append(';')
            .Append("ankleR").Append(';')
            .Append("lumbar").Append(';')
            .Append("actHipL").Append(';')
            .Append("actHipR").Append(';')
            .Append("actKneeL").Append(';')
            .Append("actKneeR").Append(';')
            .Append("actAnkleL").Append(';')
            .Append("actAnkleR").Append(';')
            .Append("pelvisY").Append(';')
            .Append("crouchLevel").Append(';')
            .Append("standLegLevel").Append(';')
            .Append("standLegCmd").Append(';')
            .Append("walkPhase").Append(';')
            .Append("walkSwap").Append(';')
            .Append("dualSoft").Append(';')
            .Append("neckAngle").Append(';')
            .Append("headAngle").Append(';')
            .Append("shoulderAngle").Append(';')
            .Append("elbowAngle").AppendLine();
    }

    public void BindWalkDriver(StepPhaseDriver driver)
    {
        stepDriver = driver;
        if (human != null)
            motionIntent = human.GetComponent<MotionIntent>();
    }

    // Crouch window: from press to release. Needed to separate
    // pelvis drop and support from the startup settle.
    public void MarkCrouch(float time, float level)
    {
        crouchStartTime = time;
        requestedCrouchLevel = level;
    }

    public void MarkCrouchRelease(float time)
    {
        crouchReleaseTime = time;
    }

    public void MarkStandLeg(float time, float side)
    {
        standLegStartTime = time;
        requestedStandLeg = side;
    }

    public void MarkStandLegRelease(float time)
    {
        standLegReleaseTime = time;
    }

    // Called by the stand at the push so recovery metrics
    // are separated from what happened before the disturbance.
    public void MarkPush(float time, float impulse)
    {
        pushTime = time;
        pushImpulse = impulse;
    }

    // Called after every physics step.
    public void Sample(float time)
    {
        samples++;

        float tilt = vestibular != null ? vestibular.GetBodyTilt() : 0f;
        float angVel = vestibular != null ? vestibular.GetBodyAngularVelocity() : 0f;
        float comOffset = com != null ? com.GetCoMOffsetX() : 0f;
        Vector2 comPosition = com != null ? com.GetCenterOfMass() : Vector2.zero;
        if (!hasStartComX)
        {
            startComX = comPosition.x;
            hasStartComX = true;
        }
        lastComX = comPosition.x;
        float headY = head != null ? head.position.y : 0f;
        float pelvisY = pelvis != null ? pelvis.position.y : 0f;
        float crouchLevel = balance != null ? balance.crouchLevel : 0f;
        float standLegLevel = balance != null ? balance.standLegLevel : 0f;
        float dualSoft = balance != null ? balance.lastDualSoft : 1f;
        float standLegCmd = motionIntent != null ? motionIntent.standLeg : 0f;
        if (stepDriver == null && human != null)
            stepDriver = human.GetComponent<StepPhaseDriver>();
        if (motionIntent == null && human != null)
            motionIntent = human.GetComponent<MotionIntent>();
        standLegCmd = motionIntent != null ? motionIntent.standLeg : standLegCmd;
        int walkPhase = stepDriver != null && stepDriver.walkActive ? stepDriver.PhaseCode : -1;
        int walkSwap = stepDriver != null ? stepDriver.SwapCount : 0;
        maxWalkSwapCount = Mathf.Max(maxWalkSwapCount, walkSwap);
        lastWalkPhase = walkPhase;
        lastStandLegCmd = standLegCmd;

        float supportMargin = state != null ? state.supportMargin : 0f;
        bool leftGrounded = state != null && state.leftFootGrounded;
        bool rightGrounded = state != null && state.rightFootGrounded;
        bool leftHandGrounded = state != null && state.leftHandGrounded;
        bool rightHandGrounded = state != null && state.rightHandGrounded;

        float actHipL = MaxActivation(balance?.leftHipFlexor, balance?.leftHipExtensor);
        float actHipR = MaxActivation(balance?.rightHipFlexor, balance?.rightHipExtensor);
        float actKneeL = MaxActivation(balance?.leftKneeFlexor, balance?.leftKneeExtensor);
        float actKneeR = MaxActivation(balance?.rightKneeFlexor, balance?.rightKneeExtensor);
        float actAnkleL = MaxActivation(balance?.leftAnkleFlexor, balance?.leftAnkleExtensor);
        float actAnkleR = MaxActivation(balance?.rightAnkleFlexor, balance?.rightAnkleExtensor);

        BalanceController.BalanceState currentState = balance != null
            ? balance.currentState
            : BalanceController.BalanceState.Balancing;

        // ─── METRIC ACCUMULATION ───
        sumComOffsetSquared += (double)comOffset * comOffset;
        sumAbsComOffset += Mathf.Abs(comOffset);
        sumTorsoAngVelSquared += (double)angVel * angVel;
        maxAbsTilt = Mathf.Max(maxAbsTilt, Mathf.Abs(tilt));
        minHeadY = Mathf.Min(minHeadY, headY);
        if (leftHandCollider != null)
            minHandY = Mathf.Min(minHandY, leftHandCollider.bounds.min.y);
        if (rightHandCollider != null)
            minHandY = Mathf.Min(minHandY, rightHandCollider.bounds.min.y);
        lastHeadY = headY;
        minPelvisY = Mathf.Min(minPelvisY, pelvisY);
        lastPelvisY = pelvisY;
        maxCrouchLevel = Mathf.Max(maxCrouchLevel, crouchLevel);
        maxStandLegLevel = Mathf.Max(maxStandLegLevel, standLegLevel);
        lastTilt = tilt;
        lastComOffset = comOffset;

        float hipL = state != null ? state.leftHipAngle : 0f;
        float hipR = state != null ? state.rightHipAngle : 0f;
        float kneeL = state != null ? state.leftKneeAngle : 0f;
        float kneeR = state != null ? state.rightKneeAngle : 0f;
        minHipAngle = Mathf.Min(minHipAngle, Mathf.Min(hipL, hipR));
        maxHipAngle = Mathf.Max(maxHipAngle, Mathf.Max(hipL, hipR));
        minKneeAngle = Mathf.Min(minKneeAngle, Mathf.Min(kneeL, kneeR));
        maxKneeAngle = Mathf.Max(maxKneeAngle, Mathf.Max(kneeL, kneeR));
        maxAbsHipAngle = Mathf.Max(maxAbsHipAngle, Mathf.Max(Mathf.Abs(hipL), Mathf.Abs(hipR)));
        maxAbsKneeAngle = Mathf.Max(maxAbsKneeAngle, Mathf.Max(Mathf.Abs(kneeL), Mathf.Abs(kneeR)));

        bool inCrouchWindow = crouchStartTime >= 0f && time >= crouchStartTime
            && (crouchReleaseTime < 0f || time <= crouchReleaseTime);
        if (inCrouchWindow)
        {
            samplesCrouchWindow++;
            if (leftGrounded && rightGrounded) samplesBothFeetCrouch++;
            if (leftHandGrounded && rightHandGrounded) samplesBothHandsCrouch++;
            if (leftHandGrounded) samplesLeftHandCrouch++;
            if (rightHandGrounded) samplesRightHandCrouch++;
        }

        bool inStandLegWindow = standLegStartTime >= 0f && time >= standLegStartTime
            && (standLegReleaseTime < 0f || time <= standLegReleaseTime);
        // Walk: support window — Stance phases (not transfer), sign from standLegCmd.
        bool inWalkStanceWindow = stepDriver != null && stepDriver.walkActive
            && walkPhase == 0 && Mathf.Abs(standLegCmd) > 0.5f;
        if (inStandLegWindow || inWalkStanceWindow)
        {
            samplesStandLegWindow++;
            if (leftGrounded && rightGrounded) samplesBothFeetStandLeg++;
            float stanceSign = inStandLegWindow ? requestedStandLeg : standLegCmd;
            bool leftIsStance = stanceSign < -0.5f;
            bool rightIsStance = stanceSign > 0.5f;
            if (leftIsStance)
            {
                if (leftGrounded) samplesStanceGrounded++;
                if (rightGrounded) samplesSwingGrounded++;
            }
            else if (rightIsStance)
            {
                if (rightGrounded) samplesStanceGrounded++;
                if (leftGrounded) samplesSwingGrounded++;
            }
        }

        if (leftGrounded) samplesLeftGrounded++;
        if (rightGrounded) samplesRightGrounded++;
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

        SampleDampingWatch(neckWatch);
        SampleDampingWatch(headWatch);
        SampleDampingWatch(shoulderWatch);
        SampleDampingWatch(elbowWatch);
        SampleDampingWatch(wristWatch);

        // The same column set, without new[] and Join on every 200 Hz step.
        rows.Append(F(time)).Append(';')
            .Append(((int)currentState).ToString(CultureInfo.InvariantCulture)).Append(';')
            .Append(F(tilt)).Append(';')
            .Append(F(angVel)).Append(';')
            .Append(F(state != null ? state.pelvisTilt : 0f)).Append(';')
            .Append(F(comPosition.x)).Append(';')
            .Append(F(comOffset)).Append(';')
            .Append(F(supportMargin)).Append(';')
            .Append(leftGrounded ? "1" : "0").Append(';')
            .Append(rightGrounded ? "1" : "0").Append(';')
            .Append(F(headY)).Append(';')
            .Append(F(state != null ? state.leftHipAngle : 0f)).Append(';')
            .Append(F(state != null ? state.rightHipAngle : 0f)).Append(';')
            .Append(F(state != null ? state.leftKneeAngle : 0f)).Append(';')
            .Append(F(state != null ? state.rightKneeAngle : 0f)).Append(';')
            .Append(F(state != null ? state.leftAnkleAngle : 0f)).Append(';')
            .Append(F(state != null ? state.rightAnkleAngle : 0f)).Append(';')
            .Append(F(state != null ? state.lumbarAngle : 0f)).Append(';')
            .Append(F(actHipL)).Append(';')
            .Append(F(actHipR)).Append(';')
            .Append(F(actKneeL)).Append(';')
            .Append(F(actKneeR)).Append(';')
            .Append(F(actAnkleL)).Append(';')
            .Append(F(actAnkleR)).Append(';')
            .Append(F(pelvisY)).Append(';')
            .Append(F(crouchLevel)).Append(';')
            .Append(F(standLegLevel)).Append(';')
            .Append(F(standLegCmd)).Append(';')
            .Append(walkPhase.ToString(CultureInfo.InvariantCulture)).Append(';')
            .Append(walkSwap.ToString(CultureInfo.InvariantCulture)).Append(';')
            .Append(F(dualSoft)).Append(';')
            .Append(F(WatchAngle(neckWatch))).Append(';')
            .Append(F(WatchAngle(headWatch))).Append(';')
            .Append(F(WatchAngle(shoulderWatch))).Append(';')
            .Append(F(WatchAngle(elbowWatch))).AppendLine();
    }

    private static float WatchAngle(JointDampingWatch watch)
    {
        return watch != null ? watch.FirstAngle() : 0f;
    }

    public string BuildCsv()
    {
        return rows.ToString();
    }

    public string BuildSummaryJson(float simulatedSeconds, float wallSeconds = -1f)
    {
        float rmsComOffset = samples > 0 ? Mathf.Sqrt((float)(sumComOffsetSquared / samples)) : 0f;
        float meanAbsComOffset = samples > 0 ? (float)(sumAbsComOffset / samples) : 0f;
        float rmsTorsoAngVel = samples > 0 ? Mathf.Sqrt((float)(sumTorsoAngVelSquared / samples)) : 0f;
        float survived = fallTime >= 0f ? fallTime : simulatedSeconds;
        float headDrop = startHeadY - lastHeadY;
        float pelvisDrop = startPelvisY - minPelvisY;
        float pelvisRecoverError = startPelvisY - lastPelvisY;
        bool returnedToStand = crouchReleaseTime >= 0f
            && fallTime < 0f
            && pelvisRecoverError <= PELVIS_RECOVER_TOLERANCE;
        float crouchBothFeet = samplesCrouchWindow > 0
            ? samplesBothFeetCrouch / (float)samplesCrouchWindow
            : 0f;
        float crouchBothHands = samplesCrouchWindow > 0
            ? samplesBothHandsCrouch / (float)samplesCrouchWindow
            : 0f;
        float crouchLeftHand = samplesCrouchWindow > 0
            ? samplesLeftHandCrouch / (float)samplesCrouchWindow
            : 0f;
        float crouchRightHand = samplesCrouchWindow > 0
            ? samplesRightHandCrouch / (float)samplesCrouchWindow
            : 0f;
        float standLegBothFeet = samplesStandLegWindow > 0
            ? samplesBothFeetStandLeg / (float)samplesStandLegWindow
            : 0f;
        float swingFootGrounded = samplesStandLegWindow > 0
            ? samplesSwingGrounded / (float)samplesStandLegWindow
            : 0f;
        float stanceFootGrounded = samplesStandLegWindow > 0
            ? samplesStanceGrounded / (float)samplesStandLegWindow
            : 0f;
        // How many seconds after the push the CoM offset stayed outside the settled band.
        float recoverySeconds = pushTime >= 0f && lastUnsettledTime >= 0f
            ? lastUnsettledTime - pushTime
            : 0f;

        StringBuilder json = new StringBuilder();
        json.Append("{");
        AppendStr(json, "label", label, false);
        AppendNum(json, "simulatedSeconds", simulatedSeconds);
        if (wallSeconds >= 0f)
        {
            AppendNum(json, "wallSeconds", wallSeconds);
            if (simulatedSeconds > 0f)
                AppendNum(json, "wallPerSim", wallSeconds / simulatedSeconds);
        }
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
        AppendNum(json, "comTravelX", lastComX - startComX);
        AppendNum(json, "comSpeedX",
            simulatedSeconds > 0f ? (lastComX - startComX) / simulatedSeconds : 0f);
        AppendNum(json, "headDrop", headDrop);
        AppendNum(json, "minHeadY", minHeadY);
        AppendNum(json, "pushImpulse", pushImpulse);
        AppendNum(json, "pushTime", pushTime);
        AppendNum(json, "maxComOffsetAfterPush", maxAbsComOffsetAfterPush);
        AppendNum(json, "maxTiltAfterPush", maxAbsTiltAfterPush);
        AppendNum(json, "recoverySeconds", recoverySeconds);
        AppendNum(json, "muscleSaturationFraction", Fraction(samplesSaturated));
        AppendNum(json, "bothFeetGroundedFraction", Fraction(samplesBothFeet));
        AppendNum(json, "leftFootGroundedFraction", Fraction(samplesLeftGrounded));
        AppendNum(json, "rightFootGroundedFraction", Fraction(samplesRightGrounded));
        AppendNum(json, "balancingFraction", Fraction(samplesBalancing));
        AppendNum(json, "recoveryFraction", Fraction(samplesRecovery));
        AppendNum(json, "fallingFraction", Fraction(samplesFalling));
        AppendNum(json, "startPelvisY", startPelvisY);
        AppendNum(json, "minPelvisY", minPelvisY);
        AppendNum(json, "finalPelvisY", lastPelvisY);
        AppendNum(json, "pelvisDrop", pelvisDrop);
        AppendNum(json, "pelvisRecoverError", pelvisRecoverError);
        json.Append(",\"returnedToStand\":").Append(returnedToStand ? "true" : "false");
        AppendNum(json, "minHipAngle", FiniteOrZero(minHipAngle));
        AppendNum(json, "maxHipAngle", FiniteOrZero(maxHipAngle));
        AppendNum(json, "minKneeAngle", FiniteOrZero(minKneeAngle));
        AppendNum(json, "maxKneeAngle", FiniteOrZero(maxKneeAngle));
        AppendNum(json, "maxAbsHipAngle", maxAbsHipAngle);
        AppendNum(json, "maxAbsKneeAngle", maxAbsKneeAngle);
        AppendNum(json, "maxCrouchLevel", maxCrouchLevel);
        AppendNum(json, "crouchBothFeetGroundedFraction", crouchBothFeet);
        AppendNum(json, "minHandY", minHandY > 100f ? 0f : minHandY);
        AppendNum(json, "crouchBothHandsGroundedFraction", crouchBothHands);
        AppendNum(json, "crouchLeftHandGroundedFraction", crouchLeftHand);
        AppendNum(json, "crouchRightHandGroundedFraction", crouchRightHand);
        AppendNum(json, "crouchTime", crouchStartTime);
        AppendNum(json, "crouchReleaseTime", crouchReleaseTime);
        AppendNum(json, "requestedCrouch", requestedCrouchLevel);
        AppendNum(json, "maxStandLegLevel", maxStandLegLevel);
        AppendNum(json, "standLegBothFeetGroundedFraction", standLegBothFeet);
        AppendNum(json, "swingFootGroundedFraction", swingFootGrounded);
        AppendNum(json, "stanceFootGroundedFraction", stanceFootGrounded);
        AppendNum(json, "standLegTime", standLegStartTime);
        AppendNum(json, "standLegReleaseTime", standLegReleaseTime);
        AppendNum(json, "requestedStandLeg", requestedStandLeg);
        AppendNum(json, "walkSwapCount", maxWalkSwapCount);
        AppendNum(json, "walkTransferReadyFrames",
            stepDriver != null ? stepDriver.TransferReadyFrames : 0f);
        AppendNum(json, "finalWalkPhase", lastWalkPhase);
        AppendNum(json, "finalStandLegCmd", lastStandLegCmd);
        AppendNum(json, "rmsNeckJointSpeed", RmsSpeed(neckWatch));
        AppendNum(json, "rmsHeadJointSpeed", RmsSpeed(headWatch));
        AppendNum(json, "rmsShoulderJointSpeed", RmsSpeed(shoulderWatch));
        AppendNum(json, "rmsElbowJointSpeed", RmsSpeed(elbowWatch));
        AppendNum(json, "rmsWristJointSpeed", RmsSpeed(wristWatch));
        AppendNum(json, "neckLimitFraction", LimitFraction(neckWatch));
        AppendNum(json, "headLimitFraction", LimitFraction(headWatch));
        AppendNum(json, "shoulderLimitFraction", LimitFraction(shoulderWatch));
        AppendNum(json, "elbowLimitFraction", LimitFraction(elbowWatch));
        AppendNum(json, "wristLimitFraction", LimitFraction(wristWatch));
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
        AppendNum(json, "crouchRatePerSecond", balance != null ? balance.crouchRatePerSecond : 0f);
        AppendNum(json, "crouchReleaseRatePerSecond", balance != null ? balance.crouchReleaseRatePerSecond : 0f);
            AppendNum(json, "crouchKneeFlex", balance != null ? balance.crouchKneeFlex : 0f);
            AppendNum(json, "crouchHipFlex", balance != null ? balance.crouchHipFlex : 0f);
            AppendNum(json, "crouchPelvisTilt", balance != null ? balance.crouchPelvisTilt : 0f);
            AppendNum(json, "crouchTorsoLean", balance != null ? balance.crouchTorsoLean : 0f);
            AppendNum(json, "pelvisPGain", balance != null ? balance.pelvisPGain : 0f);
            AppendNum(json, "pelvisDGain", balance != null ? balance.pelvisDGain : 0f);
            AppendNum(json, "pelvisErrorReferenceDegrees", balance != null ? balance.pelvisErrorReferenceDegrees : 0f);
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
        AppendNum(json, "crouch", info.RequestedCrouch);
        AppendNum(json, "crouchTime", info.RequestedCrouchTime);
        AppendNum(json, "crouchHold", info.RequestedCrouchHold);
        AppendNum(json, "lean", info.RequestedLean);
        AppendNum(json, "leanTime", info.RequestedLeanTime);
        AppendNum(json, "leanHold", info.RequestedLeanHold);
        AppendNum(json, "standLeg", info.RequestedStandLeg);
        AppendNum(json, "standLegTime", info.RequestedStandLegTime);
        AppendNum(json, "standLegHold", info.RequestedStandLegHold);
        AppendNum(json, "walk", info.RequestedWalk);
        AppendNum(json, "walkTime", info.RequestedWalkTime);
        AppendNum(json, "walkDuration", info.RequestedWalkDuration);
        AppendNum(json, "run", info.RequestedRun);
        AppendNum(json, "runTime", info.RequestedRunTime);
        AppendNum(json, "runHold", info.RequestedRunHold);
        AppendNum(json, "walkStance", info.RequestedWalkStance);
        AppendNum(json, "walkTransfer", info.RequestedWalkTransfer);
        AppendNum(json, "walkTransferCom", info.RequestedWalkTransferCom);
        AppendNum(json, "walkTransferFallbackCom", info.RequestedWalkTransferFallbackCom);
        AppendNum(json, "walkTransferLevel", info.RequestedWalkTransferLevel);
        AppendNum(json, "walkFirst", info.RequestedWalkFirst);
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
        json.Append(",\"continuousBodyCount\":").Append(CountContinuous(h).ToString(CultureInfo.InvariantCulture));
        json.Append(",\"muscleBehaviourCount\":").Append(CountBehaviours<Muscle>(h).ToString(CultureInfo.InvariantCulture));
        json.Append(",\"frictionBehaviourCount\":").Append(CountBehaviours<JointFriction>(h).ToString(CultureInfo.InvariantCulture));
        json.Append(",\"velocityIterations\":").Append(Physics2D.velocityIterations.ToString(CultureInfo.InvariantCulture));
        json.Append(",\"positionIterations\":").Append(Physics2D.positionIterations.ToString(CultureInfo.InvariantCulture));
        if (state != null && state.probeCallCount > 0)
        {
            AppendNum(json, "meanProbeHits", state.probeHitSum / (float)state.probeCallCount);
            AppendNum(json, "probeSaturatedFraction", state.probeSaturatedCount / (float)state.probeCallCount);
        }
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
        // Check contact, not intent: the pair is
        // sqrt(µ_foot · µ_ground); one foot does not set grip by itself.
        AppendNum(json, "footFriction", h.footFriction);
        float groundMu = ResolveGroundFriction();
        AppendNum(json, "groundFriction", groundMu);
        AppendNum(json, "contactFriction", Mathf.Sqrt(Mathf.Max(0f, h.footFriction) * Mathf.Max(0f, groundMu)));
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
            AppendNum(json, "crouchRatePerSecond", b.crouchRatePerSecond);
            AppendNum(json, "crouchReleaseRatePerSecond", b.crouchReleaseRatePerSecond);
            AppendNum(json, "crouchKneeFlex", b.crouchKneeFlex);
            AppendNum(json, "crouchHipFlex", b.crouchHipFlex);
            AppendNum(json, "crouchPelvisTilt", b.crouchPelvisTilt);
            AppendNum(json, "crouchTorsoLean", b.crouchTorsoLean);
            AppendNum(json, "leanRatePerSecond", b.leanRatePerSecond);
            AppendNum(json, "leanTorsoAngle", b.leanTorsoAngle);
            AppendNum(json, "leanPelvisTilt", b.leanPelvisTilt);
            AppendNum(json, "crouchArmShoulder", b.crouchArmShoulder);
            AppendNum(json, "crouchArmElbow", b.crouchArmElbow);
            AppendNum(json, "crouchArmWrist", b.crouchArmWrist);
            AppendNum(json, "crouchHandSupportMin", b.crouchHandSupportMin);
            AppendNum(json, "crouchHandGroundSlop", b.crouchHandGroundSlop);
            AppendNum(json, "crouchHandPoseStart", b.crouchHandPoseStart);
            AppendNum(json, "crouchHandSupportShoulder", b.crouchHandSupportShoulder);
            AppendNum(json, "crouchHandSupportElbow", b.crouchHandSupportElbow);
            AppendNum(json, "crouchHandSupportWrist", b.crouchHandSupportWrist);
            AppendNum(json, "crouchHandSupportSpread", b.crouchHandSupportSpread);
            AppendNum(json, "crouchHandBalanceSpread", b.crouchHandBalanceSpread);
            AppendNum(json, "swingHipFlex", b.swingHipFlex);
            AppendNum(json, "swingKneeFlex", b.swingKneeFlex);
            AppendNum(json, "swingKneeGroundedFraction", b.swingKneeGroundedFraction);
            AppendNum(json, "swingHipUnloadBias", b.swingHipUnloadBias);
            AppendNum(json, "forwardSwingHipUnloadScale", b.forwardSwingHipUnloadScale);
            AppendNum(json, "forwardSwingKneeGroundScale", b.forwardSwingKneeGroundScale);
            AppendNum(json, "backSwingHipUnloadScale", b.backSwingHipUnloadScale);
            AppendNum(json, "backSwingKneeGroundScale", b.backSwingKneeGroundScale);
            AppendNum(json, "backPelvisTiltFraction", b.backPelvisTiltFraction);
            AppendNum(json, "swingAnkleGroundedToeOff", b.swingAnkleGroundedToeOff);
            AppendNum(json, "standLegPelvisTilt", b.standLegPelvisTilt);
            AppendNum(json, "dualSupportSwingScale", b.dualSupportSwingScale);
            AppendNum(json, "dualSupportStandCap", b.dualSupportStandCap);
            AppendNum(json, "walkSwingLiftScale", b.walkSwingLiftScale);
            AppendNum(json, "walkStanceLiftDelay", b.walkStanceLiftDelay);
            AppendNum(json, "walkStanceLiftRamp", b.walkStanceLiftRamp);
            AppendNum(json, "walkWeightRamp", b.walkWeightRamp);
            AppendNum(json, "runStanceLiftDelay", b.runStanceLiftDelay);
            AppendNum(json, "runWeightRamp", b.runWeightRamp);
            AppendNum(json, "walkLiftComMax", b.walkLiftComMax);
            AppendNum(json, "walkSwingToeOffMax", b.walkSwingToeOffMax);
            AppendNum(json, "walkLiftLevelRateScale", b.walkLiftLevelRateScale);
            AppendNum(json, "walkLiftUnloadBias", b.walkLiftUnloadBias);
            AppendNum(json, "walkKneePeelMax", b.walkKneePeelMax);
            AppendNum(json, "walkStancePush", b.walkStancePush);
            AppendNum(json, "runStancePushScale", b.runStancePushScale);
            AppendNum(json, "walkTargetSpeed", b.walkTargetSpeed);
            AppendNum(json, "walkSpeedPushGain", b.walkSpeedPushGain);
            AppendNum(json, "walkSpeedPushMax", b.walkSpeedPushMax);
            AppendNum(json, "walkSpeedPushComGateScale", b.walkSpeedPushComGateScale);
            AppendNum(json, "walkXCoMWeight", b.walkXCoMWeight);
            AppendNum(json, "walkXCoMHeight", b.walkXCoMHeight);
            AppendNum(json, "walkSpeedLeanGain", b.walkSpeedLeanGain);
            AppendNum(json, "walkSpeedLeanMax", b.walkSpeedLeanMax);
            AppendNum(json, "walkSpeedLeanTorsoScale", b.walkSpeedLeanTorsoScale);
            AppendNum(json, "walkSpeedDriveGain", b.walkSpeedDriveGain);
            AppendNum(json, "walkSpeedDriveMax", b.walkSpeedDriveMax);
            AppendNum(json, "walkSpeedDriveStartLevel", b.walkSpeedDriveStartLevel);
            AppendNum(json, "walkSpeedSwingHipGain", b.walkSpeedSwingHipGain);
            AppendNum(json, "walkSpeedSwingKneeGain", b.walkSpeedSwingKneeGain);
            AppendNum(json, "walkSpeedSwingFlexMax", b.walkSpeedSwingFlexMax);
            AppendNum(json, "walkSwingHipBoost", b.walkSwingHipBoost);
            AppendNum(json, "walkSwingKneeBoost", b.walkSwingKneeBoost);
            AppendNum(json, "walkStepLength", b.walkStepLength);
            AppendNum(json, "walkStepLengthSpeedGain", b.walkStepLengthSpeedGain);
            AppendNum(json, "walkStepPlacementGain", b.walkStepPlacementGain);
            AppendNum(json, "walkStepPlacementMax", b.walkStepPlacementMax);
            AppendNum(json, "walkStepPlacementGroundFraction", b.walkStepPlacementGroundFraction);
            AppendNum(json, "walkPlacementSyncStart", b.walkPlacementSyncStart);
            AppendNum(json, "walkTouchdownSyncClearance", b.walkTouchdownSyncClearance);
            AppendNum(json, "walkStanceKneeBend", b.walkStanceKneeBend);
            AppendNum(json, "walkForwardPelvisLean", b.walkForwardPelvisLean);
            AppendNum(json, "walkForwardTorsoLean", b.walkForwardTorsoLean);
            AppendNum(json, "walkStanceExtend", b.walkStanceExtend);
            AppendNum(json, "walkComLeadX", b.walkComLeadX);
            AppendNum(json, "walkSwingScissorLevel", b.walkSwingScissorLevel);
            AppendNum(json, "walkSwingScissorFlex", b.walkSwingScissorFlex);
            if (stepDriver != null)
            {
                AppendNum(json, "stanceComTrigger", stepDriver.stanceComTrigger);
                AppendNum(json, "stanceComMinAge", stepDriver.stanceComMinAge);
                AppendNum(json, "stanceComTriggerPerSpeed", stepDriver.stanceComTriggerPerSpeed);
                AppendNum(json, "walkCadenceStanceGain", stepDriver.walkCadenceStanceGain);
                AppendNum(json, "walkCadenceMinStance", stepDriver.walkCadenceMinStance);
                AppendNum(json, "transferMinDuration", stepDriver.transferMinDuration);
                AppendNum(json, "walkCadenceTransferGain", stepDriver.walkCadenceTransferGain);
                AppendNum(json, "walkCadenceMinTransfer", stepDriver.walkCadenceMinTransfer);
            }
            AppendNum(json, "walkSwingAirHold", b.walkSwingAirHold);
            AppendNum(json, "walkSwingHeelPlant", b.walkSwingHeelPlant);
            AppendNum(json, "walkSwingUnlatchPlant", b.walkSwingUnlatchPlant);
            AppendNum(json, "walkSwingLatchAir", b.walkSwingLatchAir);
            AppendNum(json, "walkSwingAirLevel", b.walkSwingAirLevel);
            AppendNum(json, "walkArmShoulderSwing", b.walkArmShoulderSwing);
            AppendNum(json, "walkArmElbowSwing", b.walkArmElbowSwing);
            AppendNum(json, "walkArmTransferCarry", b.walkArmTransferCarry);
            AppendNum(json, "walkArmBalanceShoulderGain", b.walkArmBalanceShoulderGain);
            AppendNum(json, "walkArmBalanceElbowGain", b.walkArmBalanceElbowGain);
            AppendNum(json, "walkLatePushGain", b.walkLatePushGain);
            AppendNum(json, "walkLatePushStart", b.walkLatePushStart);
            AppendNum(json, "walkLatePushMax", b.walkLatePushMax);
            AppendNum(json, "walkLatePushNeedsAir", b.walkLatePushNeedsAir ? 1f : 0f);
            AppendNum(json, "walkLatePushNeedsTouchdownWindow", b.walkLatePushNeedsTouchdownWindow ? 1f : 0f);
            AppendNum(json, "standLegRatePerSecond", b.standLegRatePerSecond);
            AppendNum(json, "hipPGain", b.hipPGain);
            AppendNum(json, "hipDGain", b.hipDGain);
            AppendNum(json, "pelvisPGain", b.pelvisPGain);
            AppendNum(json, "pelvisDGain", b.pelvisDGain);
            AppendNum(json, "pelvisErrorReferenceDegrees", b.pelvisErrorReferenceDegrees);
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
            AppendNum(json, "neckTargetTilt", b.neckTargetTilt);
            AppendNum(json, "neckErrorReferenceDegrees", b.neckErrorReferenceDegrees);
            AppendNum(json, "useStablePd", b.useStablePd ? 1f : 0f);
            AppendNum(json, "shoulderPGain", b.shoulderPGain);
            AppendNum(json, "shoulderDGain", b.shoulderDGain);
            AppendNum(json, "shoulderBaseAngle", b.shoulderBaseAngle);
            AppendNum(json, "shoulderErrorReferenceDegrees", b.shoulderErrorReferenceDegrees);
            AppendNum(json, "elbowPGain", b.elbowPGain);
            AppendNum(json, "elbowDGain", b.elbowDGain);
            AppendNum(json, "elbowBaseAngle", b.elbowBaseAngle);
            AppendNum(json, "elbowErrorReferenceDegrees", b.elbowErrorReferenceDegrees);
            AppendNum(json, "wristPGain", b.wristPGain);
            AppendNum(json, "wristDGain", b.wristDGain);
            AppendNum(json, "wristBaseAngle", b.wristBaseAngle);
            AppendNum(json, "wristErrorReferenceDegrees", b.wristErrorReferenceDegrees);
            AppendNum(json, "armShoulderBalanceGain", b.armShoulderBalanceGain);
            AppendNum(json, "armElbowBalanceGain", b.armElbowBalanceGain);
            AppendNum(json, "armWristBalanceGain", b.armWristBalanceGain);
        }

        AppendJoints(json, h);
        AppendJointDamping(json);
        AppendPhysicsJobOptions(json);
        json.Append("}");
    }

    // Actual jobOptions on the first step — without them you cannot see whether determinism reset.
    private static void AppendPhysicsJobOptions(StringBuilder json)
    {
        PhysicsJobOptions2D options = Physics2D.jobOptions;
        json.Append(",\"physicsJobOptions\":{");
        json.Append("\"useMultithreading\":").Append(options.useMultithreading ? "1" : "0");
        json.Append(",\"useConsistencySorting\":").Append(options.useConsistencySorting ? "1" : "0");
        json.Append('}');
    }

    // Snapshot of Ieff and the applied K. Ieff = 1/(1/Iself+1/Iconnected):
    // this effective inertia is what gives the pair its relative acceleration,
    // not I of one segment. For the head, connected is the thin neck.
    private void AppendJointDamping(StringBuilder json)
    {
        json.Append(",\"jointDamping\":{");
        bool first = true;
        first = AppendDampingSnapshot(json, neckWatch, first);
        first = AppendDampingSnapshot(json, headWatch, first);
        first = AppendDampingSnapshot(json, shoulderWatch, first);
        first = AppendDampingSnapshot(json, elbowWatch, first);
        AppendDampingSnapshot(json, wristWatch, first);
        json.Append("}");
    }

    private static bool AppendDampingSnapshot(StringBuilder json, JointDampingWatch watch, bool first)
    {
        if (watch == null) return first;
        watch.CaptureIfNeeded();
        if (!first) json.Append(',');
        json.Append('"').Append(watch.Name).Append("\":{");
        AppendPrecise(json, "selfInertia", watch.SelfInertia, false);
        AppendPrecise(json, "connectedInertia", watch.ConnectedInertia);
        AppendPrecise(json, "ieff", watch.Ieff);
        AppendPrecise(json, "damping", watch.Damping);
        AppendPrecise(json, "maxTorque", watch.MaxTorque);
        AppendPrecise(json, "tauSeconds", watch.TauSeconds);
        AppendPrecise(json, "stepRatio", watch.StepRatio);
        json.Append('}');
        return false;
    }

    private static void SampleDampingWatch(JointDampingWatch watch)
    {
        if (watch == null) return;
        watch.CaptureIfNeeded();
        watch.Sample();
    }

    private static float RmsSpeed(JointDampingWatch watch)
    {
        if (watch == null || watch.SpeedSamples <= 0) return 0f;
        return Mathf.Sqrt((float)(watch.SumSpeedSquared / watch.SpeedSamples));
    }

    private static float LimitFraction(JointDampingWatch watch)
    {
        if (watch == null || watch.LimitSamples <= 0) return 0f;
        return watch.LimitHits / (float)watch.LimitSamples;
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

    private static int CountContinuous(Human h)
    {
        if (h == null) return 0;
        int n = 0;
        Rigidbody2D[] bodies = h.GetComponentsInChildren<Rigidbody2D>();
        for (int i = 0; i < bodies.Length; i++)
        {
            if (bodies[i] != null
                && bodies[i].collisionDetectionMode == CollisionDetectionMode2D.Continuous)
                n++;
        }
        return n;
    }

    private static int CountBehaviours<T>(Human h) where T : MonoBehaviour
    {
        return h == null ? 0 : h.GetComponentsInChildren<T>().Length;
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

    // Ground is built by GroundBuilder, not by the body: without its material
    // the sole friction figure says nothing about grip.
    private static float ResolveGroundFriction()
    {
        GameObject ground = GameObject.Find("Ground");
        if (ground == null)
            return 0.4f;
        Collider2D col = ground.GetComponent<Collider2D>();
        if (col == null)
            return 0.4f;
        return col.sharedMaterial != null ? col.sharedMaterial.friction : 0.4f;
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

    private static float FiniteOrZero(float value)
    {
        return float.IsInfinity(value) || float.IsNaN(value) ? 0f : value;
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

    // Inertias and tau are small (10⁻³…10⁻⁴): four F4 digits are not enough.
    private static void AppendPrecise(StringBuilder json, string key, float value, bool comma = true)
    {
        if (comma) json.Append(',');
        json.Append('"').Append(key).Append("\":").Append(value.ToString("G6", CultureInfo.InvariantCulture));
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

    // One joint kind: a singleton (neck, head) or a left/right pair.
    // Ieff and K are taken from the first live hinge; RMS and stops — from all.
    private sealed class JointDampingWatch
    {
        public readonly string Name;
        public readonly HingeJoint2D[] Joints;

        public float SelfInertia;
        public float ConnectedInertia;
        public float Ieff;
        public float Damping;
        public float MaxTorque;
        public float TauSeconds;
        public float StepRatio;
        public double SumSpeedSquared;
        public int SpeedSamples;
        public int LimitHits;
        public int LimitSamples;

        private bool captured;

        private JointDampingWatch(string name, HingeJoint2D[] joints)
        {
            Name = name;
            Joints = joints ?? Array.Empty<HingeJoint2D>();
        }

        public static JointDampingWatch Single(string name, HingeJoint2D joint)
        {
            return new JointDampingWatch(name, joint != null ? new[] { joint } : Array.Empty<HingeJoint2D>());
        }

        public static JointDampingWatch Pair(string name, HingeJoint2D left, HingeJoint2D right)
        {
            int count = (left != null ? 1 : 0) + (right != null ? 1 : 0);
            HingeJoint2D[] joints = new HingeJoint2D[count];
            int i = 0;
            if (left != null) joints[i++] = left;
            if (right != null) joints[i] = right;
            return new JointDampingWatch(name, joints);
        }

        public void CaptureIfNeeded()
        {
            if (captured) return;
            captured = true;

            HingeJoint2D joint = FirstAlive();
            if (joint == null) return;

            Rigidbody2D self = joint.attachedRigidbody;
            Rigidbody2D connected = joint.connectedBody;
            SelfInertia = self != null ? self.inertia : 0f;
            ConnectedInertia = connected != null ? connected.inertia : 0f;
            Ieff = EffectiveInertia(SelfInertia, ConnectedInertia);

            JointFriction friction = joint.GetComponent<JointFriction>();
            Damping = friction != null ? friction.damping : 0f;
            MaxTorque = friction != null ? friction.maxTorque : 0f;

            if (Damping > 0f && Ieff > 0f)
            {
                TauSeconds = Ieff / Damping;
                StepRatio = Damping * Time.fixedDeltaTime / Ieff;
            }
        }

        public void Sample()
        {
            for (int i = 0; i < Joints.Length; i++)
            {
                HingeJoint2D joint = Joints[i];
                if (joint == null) continue;

                float speed = joint.jointSpeed;
                SumSpeedSquared += (double)speed * speed;
                SpeedSamples++;

                LimitSamples++;
                if (IsNearLimit(joint)) LimitHits++;
            }
        }

        // Angle of the group's first live joint. RMS speed alone is not enough: 360 °/s
        // can be a fine 100 Hz tremor or a large 12 Hz sway, and on screen
        // those are completely different. Amplitude is visible only from the angle.
        public float FirstAngle()
        {
            HingeJoint2D joint = FirstAlive();
            return joint != null ? joint.jointAngle : 0f;
        }

        private HingeJoint2D FirstAlive()
        {
            for (int i = 0; i < Joints.Length; i++)
            {
                if (Joints[i] != null) return Joints[i];
            }
            return null;
        }

        private static bool IsNearLimit(HingeJoint2D joint)
        {
            if (joint == null || !joint.useLimits) return false;
            float angle = joint.jointAngle;
            float min = joint.limits.min;
            float max = joint.limits.max;
            return angle <= min + LIMIT_MARGIN_DEGREES || angle >= max - LIMIT_MARGIN_DEGREES;
        }

        // The same sum of inverse inertias as in JointFriction: the pair τ, −τ
        // gives relative acceleration τ·(1/I₁ + 1/I₂).
        private static float EffectiveInertia(float iSelf, float iConnected)
        {
            float inv = 0f;
            if (iSelf > 0f) inv += 1f / iSelf;
            if (iConnected > 0f) inv += 1f / iConnected;
            return inv > 0f ? 1f / inv : 0f;
        }
    }
}
