using UnityEngine;

// Arm drive. Yury's rule: the arms work the vestibular apparatus, they are not
// decoration. This component owns the six arm muscle activations (flexor and
// extensor on both shoulders and both elbows) and writes them once per physics
// step: after BalanceController (order 0), before ActuatorDriver applies the
// muscles (order 50). Every other joint, and the wrist, stays with the balance
// controller.
//
// Two jobs:
//
//  1. WALK. A reciprocal swing built from the measured hip angles, not from a
//     clock, so it holds on any gait this rig produces. The left shoulder is
//     driven against the RIGHT thigh and the right shoulder against the LEFT one
//     (the acceptance pairing), plus a smaller same-side term so that the two
//     arms stay in counter phase with each other even in a window where the legs
//     move together. The phase comes from the hips, the amplitude from the
//     balance controller's own swing range: no hardcoded sinusoid.
//
//  2. FALL. Per side: reach the palm to the slab, then press. The elbow extends
//     while the shoulder keeps the upper arm aimed at the planted hand, so the
//     arm is a strut and the stroke raises the shoulder instead of waving the
//     forearm. Real contact (Collider2D.IsTouchingLayers, not a geometric slop)
//     decides when the press may run.
//
// Sign convention of this rig, checked against the recorded columns: jointAngle
// negative = flexion = limb forward. A forward-swinging thigh has a NEGATIVE hip
// jointAngle and a forward-swung arm has a NEGATIVE shoulder angle. A positive
// activation signal increases jointAngle (extensor work).
//
// Interface for the get-up, which lives in BalanceController (not this file):
//
//     armDrive.RequestArmPress(intensity);   // 0..1, call every step while GettingUp
//
// It returns true when both palms are in real contact and the press is running.
// A request older than pressRequestHoldSeconds is dropped, so a get-up that stops
// asking cannot leave the arms latched.
[DefaultExecutionOrder(10)]
public class ArmDrive : MonoBehaviour
{
    [Header("Walk swing (deg)")]
    // Shoulder travel at |phase| = 1. Human walking swing is 20-30 deg.
    public float walkShoulderSwing = 34f;
    // Elbow: mean flexion plus the swing around it, degrees from elbowBaseAngle.
    public float walkElbowMid = 12f;
    public float walkElbowAmp = 16f;
    // Phase mix. crossWeight > ipsWeight is the point: the shoulder is dominated
    // by the OPPOSITE thigh (counter phase, correlation below -0.5), while the
    // same-side term keeps the two arms in counter phase with each other. The
    // ratio is a real trade-off, not a taste: in a window where the two thights
    // move together (corr(hipL,hipR) +0.9) "shoulder against the opposite thigh"
    // and "the two shoulders against each other" cannot both be strong, and the
    // measured optimum for BOTH being negative is cross/ips = 2 (t = 2).
    public float crossWeight = 1f;
    public float ipsWeight = 0.5f;
    // +1: left shoulder against the right thigh (the acceptance pattern).
    // -1 flips to the same-side pairing, for a measurement.
    public float swingSign = 1f;
    public float walkRampSeconds = 0.30f;
    // Hip joint speed that counts as "the legs are working a gait".
    public float walkHipSpeedDegPerSec = 25f;
    public float walkHoldSeconds = 0.50f;
    // Running mean of each hip angle (the swing rides the oscillation, not the
    // fold) and the shared scale the deviation is divided by.
    public float hipStatsTau = 0.6f;
    public float minHipVarianceDeg2 = 4f;
    // Tighter tracking while walking: the recorded column must follow the drive.
    public float walkPGainScale = 4f;
    public float walkDriveSlewPerSecond = 60f;

    [Header("Fall press")]
    public float pushEnabled = 1f;
    public float fallTiltDeg = 45f;
    // The press is a real push-up cycle per side: LIFT the palm off the slab and
    // fold the elbow, PLANT the folded arm back down, then PRESS the elbow out so
    // the shoulder is driven away from the planted palm. The fold cannot be done
    // with the palm loaded — measured: a weighted arm never folded past its stop,
    // every stroke measured 0 deg of extension.
    // World direction the folded arm is lifted toward, degrees (90 = up).
    public float liftDirectionDeg = 120f;
    // Hand targets for the closed-chain press (metres, world). The press target
    // sits BELOW the slab: the arm cannot get longer than its links, so what is
    // left of the extension goes into the body — that is the push-off.
    public float liftHandReach = 0.40f;
    public float plantHandSide = 0.18f;
    public float plantHandHeight = 0.09f;
    public float pressHandDepth = 0.35f;
    // World direction the folded arm is planted toward, degrees (-90 = down).
    public float plantDirectionDeg = -100f;
    // The plant opens the elbow at this rate (deg/s) until the palm lands, so the
    // arm is still folded when it touches: that fold is the press stroke.
    public float plantElbowExtendDegPerSecond = 45f;
    public float liftTimeoutSeconds = 1.2f;
    public float plantTimeoutSeconds = 1.2f;
    // Lift pose in joint space: arm folded in front of the chest, palm unloaded.
    public float liftShoulderJoint = -70f;
    public float cockElbow = -65f;
    // The elbow angle that counts as loaded: from there the stroke has room.
    public float foldReadyDeg = -30f;
    public float pressElbowTarget = -4f;
    // Shoulder bias added to the aim during the stroke, degrees.
    public float pressShoulderBias = 0f;
    // A stroke ends when the elbow is within this of the extension target.
    public float pressDoneMarginDeg = 6f;
    public float pressMaxSeconds = 2.5f;
    // Elbow re-flexing this far from the stroke start means the body sank back.
    public float pressReengageDeg = 15f;
    // Aim gain of the shoulder onto the planted hand (1 = point at it each step).
    public float pressAimGain = 0.55f;
    public float pressTorqueScale = 3f;
    public float pressPGainScale = 2f;
    public float pressDriveSlewPerSecond = 60f;
    // Real contact for the press, plus a small band so a palm 1-2 mm above the
    // slab still counts as planted.
    public float handNearGroundSlop = 0.04f;
    public float pressRequestHoldSeconds = 0.25f;

    // Read-only state, for the recorder and the get-up.
    public bool HandContactL { get; private set; }
    public bool HandContactR { get; private set; }
    public bool HandNearGroundL { get; private set; }
    public bool HandNearGroundR { get; private set; }
    public float ArmPhaseL { get; private set; }
    public float ArmPhaseR { get; private set; }
    public float WalkSwingLevel { get; private set; }
    public float PressRequest { get; private set; }
    public int PressPhaseL { get; private set; }
    public int PressPhaseR { get; private set; }
    public float ArmTargetShoulderL { get; private set; }
    public float ArmTargetShoulderR { get; private set; }
    public float ArmTargetElbowL { get; private set; }
    public float ArmTargetElbowR { get; private set; }
    public int PressStrokesL { get; private set; }
    public int PressStrokesR { get; private set; }
    // Largest elbow extension of the last stroke, degrees (the acceptance wants
    // more than 20).
    public float PressStrokeExtensionL { get; private set; }
    public float PressStrokeExtensionR { get; private set; }
    // Largest shoulder extension the last stroke achieved, degrees.
    public float PressStrokeShoulderExtL { get; private set; }
    public float PressStrokeShoulderExtR { get; private set; }
    // Largest pelvis rise measured inside one press stroke, metres.
    public float PressLiftMax { get; private set; }
    public float PressHandContactFractionL { get; private set; }
    public float PressHandContactFractionR { get; private set; }

    private const int SideL = 0;
    private const int SideR = 1;
    private const int PhaseIdle = 0;
    private const int PhaseLift = 1;
    private const int PhasePress = 2;
    private const int PhaseHold = 3;
    private const int PhasePlant = 4;

    private BalanceController balance;
    private VestibularSystem vestibular;

    private readonly HingeJoint2D[] hip = new HingeJoint2D[2];
    private readonly HingeJoint2D[] shoulder = new HingeJoint2D[2];
    private readonly HingeJoint2D[] elbow = new HingeJoint2D[2];
    private readonly Muscle[] shFlex = new Muscle[2];
    private readonly Muscle[] shExt = new Muscle[2];
    private readonly Muscle[] elFlex = new Muscle[2];
    private readonly Muscle[] elExt = new Muscle[2];
    private readonly Transform[] upper = new Transform[2];
    private readonly Transform[] hand = new Transform[2];
    private readonly Collider2D[] handCol = new Collider2D[2];
    private readonly float[] elbowInertia = new float[2];
    private readonly float[] shoulderInertia = new float[2];
    private readonly int[] phase = new int[2];
    private readonly float[] phaseAge = new float[2];
    private readonly float[] strokeStartElbow = new float[2];
    private readonly float[] strokeStartShoulder = new float[2];
    private readonly float[] strokeStartPelvisY = new float[2];
    private readonly int[] strokeCount = new int[2];
    private readonly float[] strokeExt = new float[2];
    private readonly float[] strokeShoulderExt = new float[2];
    private readonly float[] contactSamples = new float[2];
    private readonly float[] contactHits = new float[2];
    private readonly float[] outShoulder = new float[2];
    private readonly float[] outElbow = new float[2];
    private readonly float[] outSlew = new float[2];

    private float shTorqueBase = 40f, elTorqueBase = 30f;
    private float hipSpeedHold;
    private float walkBlend;
    private readonly float[] hipMean = new float[2];
    private readonly float[] hipDev = new float[2];
    private readonly float[] hipVar = new float[2];
    private readonly bool[] hipStatsReady = new bool[2];
    private float pressRequestTime = -100f;
    private float pelvisStartY;
    private bool down;
    private bool bound;
    private Transform pelvisBody;
    private float groundTopY = -2f;
    private readonly float[] linkL1 = new float[2];
    private readonly float[] linkL2 = new float[2];
    private readonly float[] plantX = new float[2];

    private const string ArgShoulderSwing = "-armSwing";
    private const string ArgElbowAmp = "-armElbowSwing";
    private const string ArgElbowMid = "-armElbowMid";
    private const string ArgSign = "-armSign";
    private const string ArgIps = "-armIps";
    private const string ArgCross = "-armCross";
    private const string ArgWalkGain = "-armWalkGain";
    private const string ArgPush = "-armPush";
    private const string ArgPushScale = "-armPushScale";
    private const string ArgFallTilt = "-armFallTilt";
    private const string ArgLiftDir = "-armLiftDir";
    private const string ArgPlantDir = "-armPlantDir";
    private const string ArgCockElbow = "-armCockElbow";
    private const string ArgPressShoulder = "-armPressShoulder";
    private const string ArgPressElbow = "-armPressElbow";
    public float armDebug;
    private const string ArgDebug = "-armDebug";

    void Awake()
    {
        ParseArgs();
        Bind();
    }

    // Idempotent: Human.BuildHuman calls it once the body exists.
    public void Bind()
    {
        if (bound) return;
        bound = true;

        balance = GetComponent<BalanceController>();
        vestibular = GetComponent<VestibularSystem>();

        hip[SideL] = JointOn("LeftLegThigh");
        hip[SideR] = JointOn("RightLegThigh");
        shoulder[SideL] = JointOn("LeftArmUpper");
        shoulder[SideR] = JointOn("RightArmUpper");
        elbow[SideL] = JointOn("LeftArmLower");
        elbow[SideR] = JointOn("RightArmLower");

        for (int i = 0; i < 2; i++)
        {
            string side = i == SideL ? "Left" : "Right";
            MuscleFlexorPair(side + "ArmUpper", out shFlex[i], out shExt[i]);
            MuscleFlexorPair(side + "ArmLower", out elFlex[i], out elExt[i]);
            upper[i] = transform.Find(side + "ArmUpper");
            hand[i] = transform.Find(side + "ArmHand");
            handCol[i] = hand[i] != null ? hand[i].GetComponent<Collider2D>() : null;
            phase[i] = PhaseIdle;
            // Link lengths for the two-link solve. The joint anchors live in each
            // segment's own frame, so the pivots are exact, not centre-to-centre.
            if (shoulder[i] != null && shoulder[i].attachedRigidbody != null)
            {
                Transform armRig = shoulder[i].attachedRigidbody.transform;
                Vector2 shoulderPivot = (Vector2)armRig.position + (Vector2)(armRig.rotation * (Vector3)shoulder[i].anchor);
                Vector2 elbowPivot = shoulderPivot + Vector2.down * 0.33f;
                Transform foreRig = elbow[i] != null && elbow[i].attachedRigidbody != null ? elbow[i].attachedRigidbody.transform : null;
                if (foreRig != null)
                {
                    elbowPivot = (Vector2)foreRig.position + (Vector2)(foreRig.rotation * (Vector3)elbow[i].anchor);
                }
                linkL1[i] = Vector2.Distance(shoulderPivot, elbowPivot);
                if (hand[i] != null) linkL2[i] = Vector2.Distance(elbowPivot, (Vector2)hand[i].position);
            }
            if (linkL1[i] <= 0.01f) linkL1[i] = 0.33f;
            if (linkL2[i] <= 0.01f) linkL2[i] = 0.34f;
        }

        if (shFlex[SideL] != null) shTorqueBase = shFlex[SideL].maxTorque;
        if (elFlex[SideL] != null) elTorqueBase = elFlex[SideL].maxTorque;

        pelvisBody = transform.Find("Pelvis");
        if (pelvisBody != null) pelvisStartY = pelvisBody.position.y;
        GameObject ground = GameObject.Find("Ground");
        if (ground != null)
        {
            Collider2D groundCollider = ground.GetComponent<Collider2D>();
            if (groundCollider != null) groundTopY = groundCollider.bounds.max.y;
        }
    }

    private HingeJoint2D JointOn(string name)
    {
        Transform t = transform.Find(name);
        return t != null ? t.GetComponent<HingeJoint2D>() : null;
    }

    private void MuscleFlexorPair(string segmentName, out Muscle flexor, out Muscle extensor)
    {
        flexor = null;
        extensor = null;
        Transform t = transform.Find(segmentName);
        if (t == null) return;
        Muscle[] all = t.GetComponents<Muscle>();
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] == null) continue;
            if (all[i].direction > 0f) flexor = all[i];
            else extensor = all[i];
        }
    }

    // The get-up (BalanceController) calls this every step while it wants the
    // palm press. intensity 0..1. Returns true when the press is running with
    // both palms in real contact.
    public bool RequestArmPress(float intensity)
    {
        float level = Mathf.Clamp01(intensity);
        if (level >= 0.01f)
        {
            PressRequest = level;
            pressRequestTime = Time.fixedTime;
        }
        return HandContactL && HandContactR;
    }

    void FixedUpdate()
    {
        if (!bound) Bind();
        if (shoulder[SideL] == null || elbow[SideL] == null) return;

        float dt = Time.fixedDeltaTime;
        float tilt = vestibular != null ? vestibular.GetBodyTilt() : 0f;
        bool requestLive = PressRequest >= 0.01f
            && Time.fixedTime - pressRequestTime <= pressRequestHoldSeconds;
        down = Mathf.Abs(tilt) >= fallTiltDeg || requestLive;

        SampleHandContact();
        UpdateWalkSwing(dt);
        UpdatePress(dt);
        WriteMuscles();
    }

    // ─── walk swing ───

    private void UpdateWalkSwing(float dt)
    {
        float hipSpeed = 0f;
        for (int i = 0; i < 2; i++)
        {
            if (hip[i] != null)
                hipSpeed = Mathf.Max(hipSpeed, Mathf.Abs(hip[i].jointSpeed));
        }
        if (hipSpeed >= walkHipSpeedDegPerSec) hipSpeedHold = walkHoldSeconds;
        else hipSpeedHold = Mathf.Max(0f, hipSpeedHold - dt);

        // The swing rides the hips, so it needs no walkActive bit from the walk
        // worker: it engages on any locomotion this rig produces and drops out
        // the moment the body is down (the press owns the arms then).
        float target = hipSpeedHold > 0f && !down ? 1f : 0f;
        walkBlend = Mathf.MoveTowards(walkBlend, target, dt / Mathf.Max(0.05f, walkRampSeconds));
        WalkSwingLevel = walkBlend;

        // The swing follows the hips' OSCILLATION, not their mean position, and
        // each side is standardised by its own running deviation: measured, the
        // left thigh swings 1.8x the right one, and with a shared scale the
        // left arm's two terms cancel out and the drive emits nothing (p_arms8:
        // left shoulder target +-2 deg against the right one's +-20).
        float alpha = 1f - Mathf.Exp(-dt / Mathf.Max(0.05f, hipStatsTau));
        float phaseScale = Mathf.Max(0.1f, ipsWeight + crossWeight);
        for (int i = 0; i < 2; i++)
        {
            if (hip[i] == null)
            {
                hipDev[i] = 0f;
                continue;
            }
            float x = hip[i].jointAngle;
            if (!hipStatsReady[i])
            {
                hipMean[i] = x;
                hipVar[i] = 0f;
                hipStatsReady[i] = true;
            }
            float d = x - hipMean[i];
            hipMean[i] += alpha * d;
            hipVar[i] += alpha * (d * d - hipVar[i]);
            float sd = Mathf.Sqrt(Mathf.Max(hipVar[i], minHipVarianceDeg2));
            hipDev[i] = Mathf.Clamp((x - hipMean[i]) / sd, -2.5f, 2.5f);
        }

        float dir = swingSign >= 0f ? 1f : -1f;
        // Positive phase = that arm is BACK. The acceptance pairing: the left
        // shoulder is driven AGAINST the right thigh, so the opposite hip enters
        // with a negative coefficient. The same-side term carries the opposite
        // sign and keeps the two arms in counter phase with each other in a
        // window where the legs move together (corr(hipL,hipR) near +1 otherwise
        // forces both shoulders into one signal — the +0.99 of p_arms2).
        float backL = dir * (ipsWeight * hipDev[SideL] - crossWeight * hipDev[SideR]) / phaseScale;
        float backR = dir * (ipsWeight * hipDev[SideR] - crossWeight * hipDev[SideL]) / phaseScale;
        ArmPhaseL = Mathf.Clamp(backL, -1f, 1f) * walkBlend;
        ArmPhaseR = Mathf.Clamp(backR, -1f, 1f) * walkBlend;

        float shoulderBase = balance != null ? balance.shoulderBaseAngle : 0f;
        float elbowBase = balance != null ? balance.elbowBaseAngle : -20f;
        // Elbow: flex on the forward swing, release on the back swing. Both
        // sides ride their own phase, so the two elbows counter each other.
        for (int i = 0; i < 2; i++)
        {
            float phaseNow = i == SideL ? ArmPhaseL : ArmPhaseR;
            outShoulder[i] = shoulderBase + walkShoulderSwing * phaseNow;
            outElbow[i] = elbowBase - (walkElbowMid + walkElbowAmp * phaseNow);
            outSlew[i] = walkDriveSlewPerSecond;
        }
    }

    // ─── fall press ───

    // Two-link solve of the arm for a hand target: the shoulder and elbow joint
    // angles that put the palm on the target. The elbow hinge only flexes one
    // way, which picks the solution — no sign to guess.
    private void SolveArm(int i, Vector2 target, out float shoulderTarget, out float elbowTarget)
    {
        shoulderTarget = shoulder[i] != null ? shoulder[i].jointAngle : 0f;
        elbowTarget = elbow[i] != null ? elbow[i].jointAngle : 0f;
        if (shoulder[i] == null || upper[i] == null) return;

        Transform armRig = shoulder[i].attachedRigidbody != null ? shoulder[i].attachedRigidbody.transform : null;
        if (armRig == null) return;
        Vector2 shoulderPivot = (Vector2)armRig.position + (Vector2)(armRig.rotation * (Vector3)shoulder[i].anchor);

        Vector2 toTarget = target - shoulderPivot;
        float reach = Mathf.Max(0.05f, linkL1[i] + linkL2[i] - 0.01f);
        float dist = Mathf.Clamp(toTarget.magnitude, 0.05f, reach);
        if (toTarget.sqrMagnitude < 1e-8f) return;

        float beta = Mathf.Atan2(toTarget.y, toTarget.x) * Mathf.Rad2Deg;
        float cosAlpha = (linkL1[i] * linkL1[i] + dist * dist - linkL2[i] * linkL2[i])
                         / (2f * linkL1[i] * dist);
        float cosElbow = (linkL1[i] * linkL1[i] + linkL2[i] * linkL2[i] - dist * dist)
                         / (2f * linkL1[i] * linkL2[i]);
        float alpha = Mathf.Acos(Mathf.Clamp(cosAlpha, -1f, 1f)) * Mathf.Rad2Deg;
        float interior = Mathf.Acos(Mathf.Clamp(cosElbow, -1f, 1f)) * Mathf.Rad2Deg;

        // Elbow flexion is negative in this rig, so the upper arm takes the +alpha
        // branch: that is the only geometry the elbow hinge can reach.
        shoulderTarget = AimShoulderAt(i, beta + alpha);
        elbowTarget = ClampElbow(i, interior - 180f);
    }

    private void UpdatePress(float dt)
    {
        float baseShoulder = balance != null ? balance.shoulderBaseAngle : 0f;
        float baseElbow = balance != null ? balance.elbowBaseAngle : -20f;

        for (int i = 0; i < 2; i++)
        {
            bool contact = i == SideL ? (HandContactL || HandNearGroundL)
                                      : (HandContactR || HandNearGroundR);
            if (down)
            {
                contactSamples[i] += 1f;
                if (contact) contactHits[i] += 1f;
            }

            float shoulderTarget = walkBlend > 0.001f ? outShoulder[i] : baseShoulder;
            float elbowTarget = walkBlend > 0.001f ? outElbow[i] : baseElbow;
            float slew = walkBlend > 0.001f ? walkDriveSlewPerSecond : pressDriveSlewPerSecond;

            if (pushEnabled > 0.5f && down)
            {
                float elbowAngle = elbow[i] != null ? elbow[i].jointAngle : 0f;
                phaseAge[i] += dt;
                slew = pressDriveSlewPerSecond;
                if (phase[i] == PhaseIdle)
                {
                    // On the slab: load the arm. No palm-lifting cycle any more —
                    // measured, lifting the palm to fold it cost the contact
                    // (armHandContactFraction 0.99 -> 0.47) and the stroke still
                    // came out at 0 deg. The arm folds while the palm stays down.
                    phase[i] = PhasePlant;
                    phaseAge[i] = 0f;
                }
                else if (phase[i] == PhaseLift)
                {
                    // Kept for the record (old cycle): palm off the slab, folded.
                    shoulderTarget = ClampShoulder(i, liftShoulderJoint);
                    elbowTarget = ClampElbow(i, cockElbow);
                    if (elbowAngle <= cockElbow + pressDoneMarginDeg || phaseAge[i] >= liftTimeoutSeconds)
                    {
                        phase[i] = PhasePlant;
                        phaseAge[i] = 0f;
                    }
                }
                else if (phase[i] == PhasePlant)
                {
                    // Palm down, arm loading. Until the palm is on the slab the
                    // elbow opens to find it; once it is there the elbow folds
                    // toward the load angle, and that fold is the stroke.
                    shoulderTarget = AimShoulderAt(i, plantDirectionDeg);
                    if (!contact)
                        elbowTarget = ClampElbow(i, Mathf.Max(-5f,
                            elbowAngle - plantElbowExtendDegPerSecond * dt));
                    else
                        elbowTarget = ClampElbow(i, cockElbow);
                    if (contact && elbowAngle <= foldReadyDeg)
                        BeginStroke(i, elbowAngle);
                    else if (phaseAge[i] >= plantTimeoutSeconds)
                        BeginStroke(i, elbowAngle);
                }
                else
                {
                    // Press/Hold: the upper arm sweeps to the floor aim while the
                    // elbow drives to straight, so the strut between palm and
                    // shoulder can only grow — the body is what moves.
                    shoulderTarget = AimShoulderAt(i, plantDirectionDeg) + pressShoulderBias;
                    elbowTarget = ClampElbow(i, pressElbowTarget);
                    if (phase[i] == PhasePress)
                    {
                        TrackStroke(i, elbowAngle);
                        if (!contact || phaseAge[i] >= pressMaxSeconds)
                        {
                            phase[i] = PhaseHold;
                            phaseAge[i] = 0f;
                        }
                    }
                    else if (!contact || elbowAngle < strokeStartElbow[i] + pressReengageDeg)
                    {
                        // Lost the slab, or the body sank and refolded the arm:
                        // go back to loading the arm with the palm still down.
                        phase[i] = PhasePlant;
                        phaseAge[i] = 0f;
                    }
                }
            }
            else if (!down && phase[i] != PhaseIdle)
            {
                // Standing again: hand the arms back to the balance controller.
                phase[i] = PhaseIdle;
                strokeStartElbow[i] = 0f;
            }

            outShoulder[i] = shoulderTarget;
            outElbow[i] = elbowTarget;
            outSlew[i] = slew;
            if (i == SideL)
            {
                ArmTargetShoulderL = shoulderTarget;
                ArmTargetElbowL = elbowTarget;
                PressPhaseL = phase[i];
            }
            else
            {
                ArmTargetShoulderR = shoulderTarget;
                ArmTargetElbowR = elbowTarget;
                PressPhaseR = phase[i];
            }
        }

        if (contactSamples[SideL] > 0f)
            PressHandContactFractionL = contactHits[SideL] / contactSamples[SideL];
        if (contactSamples[SideR] > 0f)
            PressHandContactFractionR = contactHits[SideR] / contactSamples[SideR];
        PressStrokesL = strokeCount[SideL];
        PressStrokesR = strokeCount[SideR];
        PressStrokeExtensionL = strokeExt[SideL];
        PressStrokeExtensionR = strokeExt[SideR];
        PressStrokeShoulderExtL = strokeShoulderExt[SideL];
        PressStrokeShoulderExtR = strokeShoulderExt[SideR];
    }

    private void BeginStroke(int i, float elbowAngle)
    {
        phase[i] = PhasePress;
        phaseAge[i] = 0f;
        strokeStartElbow[i] = elbowAngle;
        strokeStartShoulder[i] = shoulder[i] != null ? shoulder[i].jointAngle : 0f;
        strokeStartPelvisY[i] = pelvisBody != null ? pelvisBody.position.y : 0f;
        strokeExt[i] = 0f;
        strokeShoulderExt[i] = 0f;
        strokeCount[i]++;
    }

    private void TrackStroke(int i, float elbowAngle)
    {
        // Extension is positive: the elbow angle grows toward 0.
        float ext = elbowAngle - strokeStartElbow[i];
        if (ext > strokeExt[i]) strokeExt[i] = ext;
        float shoulderAngle = shoulder[i] != null ? shoulder[i].jointAngle : 0f;
        float shoulderExt = shoulderAngle - strokeStartShoulder[i];
        if (shoulderExt > strokeShoulderExt[i]) strokeShoulderExt[i] = shoulderExt;
        float pelvisY = pelvisBody != null ? pelvisBody.position.y : 0f;
        float lift = pelvisY - strokeStartPelvisY[i];
        if (lift > PressLiftMax) PressLiftMax = lift;
    }

    // Keep the upper arm pointed at the planted hand: with the palm on the slab
    // the arm becomes a strut, so extending the elbow raises the shoulder instead
    // of waving the forearm. A joint angle increment rotates the limb by the same
    // increment in the world, so the target is the current angle plus the world
    // direction error — no sign convention to get wrong.
    private float AimShoulder(int i)
    {
        if (shoulder[i] == null || upper[i] == null || hand[i] == null)
            return shoulder[i] != null ? shoulder[i].jointAngle : 0f;

        Vector2 now = (Vector2)hand[i].position - (Vector2)upper[i].position;
        if (now.sqrMagnitude < 1e-6f) return shoulder[i].jointAngle;
        return AimShoulderAt(i, Mathf.Atan2(now.y, now.x) * Mathf.Rad2Deg);
    }

    // Point the upper arm at a world direction (degrees). A joint angle increment
    // rotates the limb by the same increment in the world, so the target is the
    // current angle plus the world direction error — no sign convention to get
    // wrong, and it works whatever the torso tilt happens to be.
    private float AimShoulderAt(int i, float worldDirDeg)
    {
        if (shoulder[i] == null || upper[i] == null)
            return shoulder[i] != null ? shoulder[i].jointAngle : 0f;

        Rigidbody2D arm = shoulder[i].attachedRigidbody;
        Vector2 axis = arm != null ? (Vector2)(arm.transform.rotation * Vector3.down) : Vector2.down;
        float have = Mathf.Atan2(axis.y, axis.x) * Mathf.Rad2Deg;
        float delta = Mathf.DeltaAngle(have, worldDirDeg) * Mathf.Clamp01(pressAimGain);
        float target = shoulder[i].jointAngle + delta;
        if (shoulder[i].useLimits)
            target = Mathf.Clamp(target, shoulder[i].limits.min, shoulder[i].limits.max);
        return target;
    }

    private float ClampElbow(int i, float target)
    {
        if (elbow[i] != null && elbow[i].useLimits)
            target = Mathf.Clamp(target, elbow[i].limits.min, elbow[i].limits.max);
        return target;
    }

    private float ClampShoulder(int i, float target)
    {
        if (shoulder[i] != null && shoulder[i].useLimits)
            target = Mathf.Clamp(target, shoulder[i].limits.min, shoulder[i].limits.max);
        return target;
    }

    // ─── muscle write ───

    private void WriteMuscles()
    {
        bool pressing = pushEnabled > 0.5f && down;
        for (int i = 0; i < 2; i++)
        {
            bool armPress = pressing && phase[i] != PhaseIdle;
            float pScale = armPress ? Mathf.Max(1f, pressPGainScale) : 1f;
            if (walkBlend > 0.001f && !armPress)
                pScale = Mathf.Max(pScale, walkPGainScale);

            float shP = (balance != null ? balance.shoulderPGain : 1.5f) * pScale;
            float shD = balance != null ? balance.shoulderDGain : 0.4f;
            float elP = (balance != null ? balance.elbowPGain : 1.2f) * pScale;
            float elD = balance != null ? balance.elbowDGain : 0.4f;
            float shRef = balance != null ? balance.shoulderErrorReferenceDegrees : 25f;
            float elRef = balance != null ? balance.elbowErrorReferenceDegrees : 25f;

            Drive(shoulder[i], outShoulder[i], shP, shD, shRef,
                shFlex[i], shExt[i], ref shoulderInertia[i], outSlew[i]);
            Drive(elbow[i], outElbow[i], elP, elD, elRef,
                elFlex[i], elExt[i], ref elbowInertia[i], outSlew[i]);
        }

        // A press stroke needs authority over the palm's friction; outside the
        // press the muscles go back to their own ceilings.
        float scale = pressing ? Mathf.Max(1f, pressTorqueScale) : 1f;
        for (int i = 0; i < 2; i++)
        {
            SetPair(shFlex[i], shExt[i], shTorqueBase * scale);
            SetPair(elFlex[i], elExt[i], elTorqueBase * scale);
        }
    }

    private static void SetPair(Muscle flexor, Muscle extensor, float torque)
    {
        if (flexor != null) flexor.maxTorque = torque;
        if (extensor != null) extensor.maxTorque = torque;
    }

    // Stable PD, the same algebra BalanceController uses: the error is taken from
    // the next step and the signal is divided by (1 + K*dt/I), so a strong damper
    // cannot overshoot at the arm's small inertia.
    private void Drive(HingeJoint2D joint, float targetAngle, float pGain, float dGain,
                       float errorRef, Muscle flexor, Muscle extensor,
                       ref float cachedInertia, float slewPerSecond)
    {
        if (joint == null || flexor == null || extensor == null) return;

        if (joint.useLimits)
            targetAngle = Mathf.Clamp(targetAngle, joint.limits.min, joint.limits.max);

        float dt = Time.fixedDeltaTime;
        float speedRef = balance != null ? balance.speedReferenceDegPerSec : 200f;
        float angleForError = joint.jointAngle + joint.jointSpeed * dt;

        float error = Mathf.DeltaAngle(angleForError, targetAngle) / Mathf.Max(1f, errorRef);
        float speed = joint.jointSpeed / Mathf.Max(1f, speedRef);
        float raw = pGain * error - dGain * speed;

        float damping = 0f;
        if (cachedInertia <= 0f)
            cachedInertia = EffectiveInertia(joint);
        if (cachedInertia > 0f)
        {
            float k = dGain * flexor.maxTorque * Mathf.Rad2Deg / Mathf.Max(1f, speedRef);
            damping = k * dt / cachedInertia;
        }

        float signal = Mathf.Clamp(raw / (1f + damping), -1f, 1f);
        float speedPerSecond = Mathf.Max(20f, slewPerSecond);
        if (signal > 0f)
        {
            Slew(extensor, signal, speedPerSecond);
            Slew(flexor, 0f, speedPerSecond);
        }
        else
        {
            Slew(extensor, 0f, speedPerSecond);
            Slew(flexor, -signal, speedPerSecond);
        }
    }

    private static void Slew(Muscle muscle, float target, float speedPerSecond)
    {
        if (muscle == null) return;
        muscle.activation = Mathf.MoveTowards(
            muscle.activation, target, speedPerSecond * Time.fixedDeltaTime);
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

    // Real contact vs the geometric band, kept apart on purpose: "the palm is at
    // the floor" is not "the palm grips the floor", and that difference was the
    // whole diagnosis of the last run.
    private void SampleHandContact()
    {
        for (int i = 0; i < 2; i++)
        {
            bool real = false;
            bool near = false;
            Collider2D col = handCol[i];
            if (col != null && col.enabled)
            {
                real = col.IsTouchingLayers(GroundLayers.Mask);
                near = col.bounds.min.y <= groundTopY + handNearGroundSlop;
            }
            if (i == SideL)
            {
                HandContactL = real;
                HandNearGroundL = near;
            }
            else
            {
                HandContactR = real;
                HandNearGroundR = near;
            }
        }
    }

    private void ParseArgs()
    {
        string[] args = System.Environment.GetCommandLineArgs();
        walkShoulderSwing = ReadFloat(args, ArgShoulderSwing, walkShoulderSwing);
        walkElbowAmp = ReadFloat(args, ArgElbowAmp, walkElbowAmp);
        walkElbowMid = ReadFloat(args, ArgElbowMid, walkElbowMid);
        swingSign = ReadFloat(args, ArgSign, swingSign);
        ipsWeight = ReadFloat(args, ArgIps, ipsWeight);
        crossWeight = ReadFloat(args, ArgCross, crossWeight);
        walkPGainScale = ReadFloat(args, ArgWalkGain, walkPGainScale);
        pushEnabled = ReadFloat(args, ArgPush, pushEnabled);
        pressTorqueScale = ReadFloat(args, ArgPushScale, pressTorqueScale);
        fallTiltDeg = ReadFloat(args, ArgFallTilt, fallTiltDeg);
        liftDirectionDeg = ReadFloat(args, ArgLiftDir, liftDirectionDeg);
        plantDirectionDeg = ReadFloat(args, ArgPlantDir, plantDirectionDeg);
        cockElbow = ReadFloat(args, ArgCockElbow, cockElbow);
        pressShoulderBias = ReadFloat(args, ArgPressShoulder, pressShoulderBias);
        pressElbowTarget = ReadFloat(args, ArgPressElbow, pressElbowTarget);
        armDebug = ReadFloat(args, ArgDebug, armDebug);
    }

    private static float ReadFloat(string[] args, string name, float fallback)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] != name) continue;
            float v;
            if (float.TryParse(args[i + 1], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out v))
                return v;
        }
        return fallback;
    }
}
