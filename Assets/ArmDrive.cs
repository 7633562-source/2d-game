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
//  2. FALL. Per side: fold the arm down onto the slab, latch the palm's world
//     point, then press. Measured (p_arms20): with the target chasing the
//     shoulder the arm walked its hand along the floor — 191 strokes of 0.18 s,
//     2.7 deg of elbow extension, the palm 0.9 m in the air on the left side.
//     A press needs a FIXED palm: the aim holds the upper arm on the latched
//     point while the elbow extends, the arm becomes a strut, and the only way
//     the chain can give is the shoulder leaving that point — the body rising.
//     Real contact (Collider2D.IsTouchingLayers) starts the stroke and holds it:
//     a stroke is latched for pressMinStrokeSeconds and only a contact loss that
//     lasts pressLostContactSeconds may abandon it.
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
// Returns true when both palms are in real contact; the return value is a
// convenience, the caller may ignore it. A request older than
// pressRequestHoldSeconds is dropped, so a get-up that stops asking cannot leave
// the arms latched. Per-side read-back for the get-up and the recorder:
// HandContactL/R, PressPhaseL/R (0 idle 1 lift 2 press 3 hold 4 plant),
// PressStrokeExtensionL/R, PressShoulderLiftL/R, PressLiftMax, PressStrokesL/R.
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
    // The press is a closed chain, and it only works if the palm is a FIXED
    // point: the hand target is latched in the world when the palm lands and is
    // not moved again until the stroke ends. Measured p_arms20 with a target
    // that followed the shoulder: 191 strokes per side, 2.7 deg of elbow
    // extension, the arm walked its hand along the slab and the body never rose.
    // The stroke runs Press (elbow extends, shoulder aimed at the latched
    // point) -> Hold -> Lift (palm unloaded, arm folds again) -> Plant (reach
    // the next latched point, the stroke starts on real contact).
    // Plant geometry: the palm lands on the slab at the SMALLEST lateral offset
    // the arm can manage — the closer the palm is to under the shoulder, the more
    // of the elbow extension goes into lifting the body instead of shoving it
    // sideways. The offset is the horizontal leg of the shortest chord the folded
    // arm can make: with the elbow at its flexion stop the arm spans
    // sqrt(L1^2+L2^2-2 L1 L2 cos interior), and anything shorter is unreachable.
    // Measured p_arms21 with a fixed 0.30 m offset: the push left the slab at
    // 57 deg off vertical, the palm slipped and the shoulder rose 70 mm.
    public float plantSideMax = 0.35f;
    public float plantFoldMarginDeg = 8f;
    public float plantReachSeconds = 1.2f;
    // Palm centre this far below the slab top: the collider is pressed onto the
    // ground rather than resting on it, which is what makes IsTouchingLayers
    // fire (a geometric slop test does not).
    public float handGroundSink = 0.02f;
    public float pressElbowTarget = -4f;
    // Optional rate limit on the stroke's elbow goal, deg/s (0 = off). Measured
    // p_arms21: driving the elbow straight in one step straightened 100 deg in
    // 0.12 s. With the palm loaded the slab itself limits the rate, so this is
    // left off by default and kept as an A/B switch.
    public float pressStrokeDegPerSecond = 0f;
    // The stroke has done its work when the elbow stops extending this long.
    public float pressStallSeconds = 0.5f;
    // A stroke is latched for at least this long, and contact has to be lost for
    // pressLostContactSeconds before it may be abandoned: the churn of p_arms20
    // (0.18 s per "stroke") was a contact flicker, not a lost slab.
    public float pressMinStrokeSeconds = 0.8f;
    public float pressMaxSeconds = 3.0f;
    public float pressLostContactSeconds = 0.15f;
    public float pressReengageDeg = 12f;
    public float pressReengageCooldownSeconds = 0.4f;
    public float liftHoldSeconds = 0.30f;
    // Shoulder bias added to the aim during the stroke, degrees.
    public float pressShoulderBias = 0f;
    // A stroke ends when the elbow is within this of the extension target.
    public float pressDoneMarginDeg = 6f;
    // Aim gain of the shoulder onto the latched palm (1 = point at it each step).
    public float pressAimGain = 1f;
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
    // Largest shoulder rise measured inside one press stroke, per side, metres:
    // the palm is latched on the slab, so this is the push itself, not the
    // pelvis (which only follows if the legs hold).
    public float PressShoulderLiftL { get; private set; }
    public float PressShoulderLiftR { get; private set; }
    // Lateral offset of the latched palm from the shoulder pivot, metres.
    public float PressPlantOffsetL { get; private set; }
    public float PressPlantOffsetR { get; private set; }
    // The elbow angle the plant asked for, degrees: how much fold the stroke
    // starts from, i.e. the room the press has to extend into.
    public float PressPlantFoldL { get; private set; }
    public float PressPlantFoldR { get; private set; }
    // Shoulder pivot height and the latched palm height, metres. The gap between
    // them is what the arm has to span, so a press that cannot reach the slab is
    // told from one that reaches it and is not loaded.
    public float PressPivotYL { get { return ShoulderPivotWorld(SideL).y; } }
    public float PressPivotYR { get { return ShoulderPivotWorld(SideR).y; } }
    public float PressPalmYL { get { return PlantTarget(SideL).y; } }
    public float PressPalmYR { get { return PlantTarget(SideR).y; } }
    // World direction the upper arm points, degrees, and the centre of the
    // direction window the shoulder can reach (rest direction) — so an aim that
    // the joint range cannot serve is a measured distance, not an opinion.
    public float PressAimHaveL { get { return UpperArmWorldDeg(SideL); } }
    public float PressAimHaveR { get { return UpperArmWorldDeg(SideR); } }
    public float PressRestL { get { return UpperArmRestDeg(SideL); } }
    public float PressRestR { get { return UpperArmRestDeg(SideR); } }
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
    private readonly float[] strokeStartShoulderY = new float[2];
    private readonly float[] strokeShoulderLift = new float[2];
    private readonly float[] lostContactTime = new float[2];
    private readonly float[] strokeStall = new float[2];
    private readonly float[] pressAimWorld = new float[2];
    private readonly int[] aimSignVote = new int[2];
    private readonly float[] aimHavePrev = new float[2];
    private readonly float[] aimThPrev = new float[2];
    private readonly bool[] aimSignSeen = new bool[2];
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
    private readonly float[] plantY = new float[2];

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
    private const string ArgPlantSide = "-armPlantSide";
    private const string ArgStrokeRate = "-armStrokeRate";
    private const string ArgStrokeMin = "-armStrokeMin";
    private const string ArgAimGain = "-armAimGain";
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

        Vector2 shoulderPivot = ShoulderPivotWorld(i);
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
        shoulderTarget = ShoulderAngleForDirection(i, beta + alpha);
        elbowTarget = ClampElbow(i, interior - 180f);
    }

    // World position of the shoulder hinge: the upper arm's pivot, not its centre.
    private Vector2 ShoulderPivotWorld(int i)
    {
        if (shoulder[i] == null || shoulder[i].attachedRigidbody == null)
            return upper[i] != null ? (Vector2)upper[i].position : Vector2.zero;
        Transform armRig = shoulder[i].attachedRigidbody.transform;
        return (Vector2)armRig.position + (Vector2)(armRig.rotation * (Vector3)shoulder[i].anchor);
    }

    private void UpdatePress(float dt)
    {
        float baseShoulder = balance != null ? balance.shoulderBaseAngle : 0f;
        float baseElbow = balance != null ? balance.elbowBaseAngle : -20f;

        for (int i = 0; i < 2; i++)
        {
            TrackAimSign(i);
            bool contact = i == SideL ? (HandContactL || HandNearGroundL)
                                      : (HandContactR || HandNearGroundR);
            bool realContact = i == SideL ? HandContactL : HandContactR;
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
                if (realContact) lostContactTime[i] = 0f;
                else lostContactTime[i] += dt;

                float palmY = PalmCentreHeight(i);

                if (phase[i] == PhaseIdle)
                {
                    // A fall just latched: latch a plant point from the pose the
                    // body landed in, then reach for it.
                    ChoosePlantPoint(i, palmY);
                    phase[i] = PhasePlant;
                    phaseAge[i] = 0f;
                }

                if (phase[i] == PhaseLift)
                {
                    // Palm unloaded: the arm folds for the next stroke. Same solve
                    // as the plant, but no stroke may start here.
                    SolveArm(i, PlantTarget(i), out shoulderTarget, out elbowTarget);
                    if (phaseAge[i] >= liftHoldSeconds)
                    {
                        phase[i] = PhasePlant;
                        phaseAge[i] = 0f;
                    }
                }
                else if (phase[i] == PhasePlant)
                {
                    // Reach for the latched point until the palm is really on the
                    // slab. That contact is the only thing that starts a stroke.
                    // The height tracks the wrist: the palm's lowest point is what
                    // has to reach the slab, and that depends on the hand's angle.
                    plantY[i] = PalmCentreHeight(i);
                    SolveArm(i, PlantTarget(i), out shoulderTarget, out elbowTarget);
                    if (realContact)
                    {
                        BeginStroke(i, elbowAngle);
                    }
                    else if (phaseAge[i] >= plantReachSeconds)
                    {
                        // The body rolled and the latched point is out of reach:
                        // take a fresh one from where the shoulder is now.
                        ChoosePlantPoint(i, palmY);
                        phaseAge[i] = 0f;
                    }
                }
                else
                {
                    // Press/Hold: the palm sits on the slab at a latched world
                    // point and does not move. The upper arm is aimed at that
                    // point and the elbow driven straight: the arm can only get
                    // longer along the shoulder-palm line, and the slab will not
                    // let the palm move, so the shoulder is what has to go — the
                    // body rising. The aim must be expressible: see the shoulder
                    // range in Human.CreateArm.
                    shoulderTarget = ShoulderAngleForDirection(i, pressAimWorld[i]) + pressShoulderBias;
                    float strokeGoal = pressStrokeDegPerSecond > 0f
                        ? Mathf.Max(pressElbowTarget,
                            strokeStartElbow[i] + pressStrokeDegPerSecond * phaseAge[i])
                        : pressElbowTarget;
                    elbowTarget = ClampElbow(i, strokeGoal);

                    if (phase[i] == PhasePress)
                    {
                        TrackStroke(i, elbowAngle, dt);
                        bool strokeDone = phaseAge[i] >= pressMaxSeconds
                                          || (phaseAge[i] >= pressMinStrokeSeconds
                                              && strokeStall[i] >= pressStallSeconds);
                        bool slabLost = lostContactTime[i] >= pressLostContactSeconds
                                        && phaseAge[i] >= pressMinStrokeSeconds;
                        if (strokeDone || slabLost)
                        {
                            phase[i] = PhaseHold;
                            phaseAge[i] = 0f;
                        }
                    }
                    else
                    {
                        // Hold: keep the loaded pose. Another stroke only after a
                        // dwell and only for a reason — the slab is gone, the body
                        // sank and refolded the arm, or the elbow ran out of room
                        // (straight = the press has given all it has). Measured
                        // p_arms20: the old immediate re-engage on the elbow angle
                        // alone ran 191 strokes of 0.18 s and 2.7 deg each;
                        // p_arms27 without the out-of-room case sat in hold for
                        // 33 s and pressed nothing.
                        bool sank = elbowAngle < strokeStartElbow[i] + pressReengageDeg;
                        bool outOfRoom = elbowAngle >= pressElbowTarget - pressDoneMarginDeg;
                        if (phaseAge[i] >= pressReengageCooldownSeconds
                            && (lostContactTime[i] >= pressLostContactSeconds || sank || outOfRoom))
                        {
                            ChoosePlantPoint(i, palmY);
                            phase[i] = PhaseLift;
                            phaseAge[i] = 0f;
                        }
                    }
                }
            }
            else if (!down && phase[i] != PhaseIdle)
            {
                // Standing again: hand the arms back to the balance controller.
                phase[i] = PhaseIdle;
                strokeStartElbow[i] = 0f;
                lostContactTime[i] = 0f;
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

        if (armDebug > 0f) LogArmState();
    }

    // Number dump for the press, off unless -armDebug 1. One line per side every
    // 0.25 s while the arms are working: the geometry a later change has to be
    // argued from (pivot, latched palm, the fold the solve asks for, the joint
    // range it has to live in) next to where the palm actually is.
    private void LogArmState()
    {
        if (Time.frameCount % 50 != 0) return;
        for (int i = 0; i < 2; i++)
        {
            Vector2 pivot = ShoulderPivotWorld(i);
            Vector2 palm = PlantTarget(i);
            float sh = shoulder[i] != null ? shoulder[i].jointAngle : 0f;
            float el = elbow[i] != null ? elbow[i].jointAngle : 0f;
            float shLo = shoulder[i] != null && shoulder[i].useLimits ? shoulder[i].limits.min : 0f;
            float shHi = shoulder[i] != null && shoulder[i].useLimits ? shoulder[i].limits.max : 0f;
            float elLo = elbow[i] != null && elbow[i].useLimits ? elbow[i].limits.min : 0f;
            Vector2 handPos = hand[i] != null ? (Vector2)hand[i].position : Vector2.zero;
            Debug.Log(string.Format(
                "[ARM] t={0:F2} {1} ph={2} age={3:F2} touch={4} lost={5:F2} " +
                "pivot=({6:F2},{7:F2}) palm=({8:F2},{9:F2}) hand=({10:F2},{11:F2}) " +
                "sh={12:F1}[{13:F0},{14:F0}] el={15:F1}[{16:F0}] plantFold={17:F1} " +
                "tgt=({18:F1},{19:F1})", Time.fixedTime, i == SideL ? "L" : "R",
                phase[i], phaseAge[i], (i == SideL ? HandContactL : HandContactR) ? 1 : 0,
                lostContactTime[i], pivot.x, pivot.y, palm.x, palm.y, handPos.x, handPos.y,
                sh, shLo, shHi, el, elLo,
                i == SideL ? PressPlantFoldL : PressPlantFoldR, outShoulder[i], outElbow[i]));
        }
    }

    private void BeginStroke(int i, float elbowAngle)
    {
        phase[i] = PhasePress;
        phaseAge[i] = 0f;
        strokeStartElbow[i] = elbowAngle;
        strokeStartShoulder[i] = shoulder[i] != null ? shoulder[i].jointAngle : 0f;
        strokeStartPelvisY[i] = pelvisBody != null ? pelvisBody.position.y : 0f;
        strokeStartShoulderY[i] = ShoulderPivotWorld(i).y;
        strokeExt[i] = 0f;
        strokeShoulderExt[i] = 0f;
        strokeShoulderLift[i] = 0f;
        strokeStall[i] = 0f;
        // Freeze the aim for the whole stroke: the upper arm holds one world
        // direction while the elbow extends. Measured p_arms25 with the aim
        // re-solved every step to point at the palm: the elbow opened from -93 to
        // +2 in 0.10 s (950 deg/s) and the HAND rose 0.9 m off a slab it was
        // gripping — the joists chased each other and the arm moved itself. With
        // the direction frozen the chord can only grow along one line, the slab
        // blocks the palm, and the shoulder is what has to move.
        pressAimWorld[i] = UpperArmWorldDeg(i);
        strokeCount[i]++;
    }

    private void TrackStroke(int i, float elbowAngle, float dt)
    {
        // Extension is positive: the elbow angle grows toward 0.
        float ext = elbowAngle - strokeStartElbow[i];
        if (ext > strokeExt[i] + 0.5f)
        {
            strokeExt[i] = ext;
            strokeStall[i] = 0f;
        }
        else
        {
            strokeStall[i] += dt;
        }
        float shoulderAngle = shoulder[i] != null ? shoulder[i].jointAngle : 0f;
        float shoulderExt = shoulderAngle - strokeStartShoulder[i];
        if (shoulderExt > strokeShoulderExt[i]) strokeShoulderExt[i] = shoulderExt;
        float pelvisY = pelvisBody != null ? pelvisBody.position.y : 0f;
        float lift = pelvisY - strokeStartPelvisY[i];
        if (lift > PressLiftMax) PressLiftMax = lift;
        // The palm is latched on the slab through the stroke, so the shoulder
        // pivot rising is the push itself, independent of what the legs do.
        float shoulderLift = ShoulderPivotWorld(i).y - strokeStartShoulderY[i];
        if (shoulderLift > strokeShoulderLift[i])
        {
            strokeShoulderLift[i] = shoulderLift;
            if (i == SideL) PressShoulderLiftL = shoulderLift;
            else PressShoulderLiftR = shoulderLift;
        }
    }

    // ─── plant geometry ───

    private Vector2 PlantTarget(int i)
    {
        return new Vector2(plantX[i], plantY[i]);
    }

    // Where the palm's centre has to sit to press the slab: the hand's own live
    // half-height above the ground top, sunk handGroundSink below it, so the
    // collider is squeezed onto the ground instead of hovering over it.
    // Deliberately NOT a fixed half-height: measured p_arms24 the left hand
    // landed flat (an AABB half-height of 0.03, not the 0.08 the target assumed),
    // stopped 25 mm over the slab and tripped the estimator's slop band with no
    // real contact at all — while the right hand, one centimetre lower and
    // standing on its end, gripped.
    private float PalmCentreHeight(int i)
    {
        float halfY = 0.06f;
        if (hand[i] != null && handCol[i] != null)
            halfY = hand[i].position.y - handCol[i].bounds.min.y;
        return groundTopY - handGroundSink + Mathf.Max(0.01f, halfY);
    }

    // Latch the palm's world point for the coming stroke: directly under the
    // shoulder when the folded arm is short enough to allow it, otherwise as
    // close to that as the elbow's flexion stop permits. The palm does not move
    // for the whole stroke, which is what lets the elbow extension become a push.
    private void ChoosePlantPoint(int i, float palmY)
    {
        Vector2 pivot = ShoulderPivotWorld(i);
        float drop = Mathf.Max(0.02f, pivot.y - palmY);
        // Fold only to the stop plus a margin: a chord that asks for the exact
        // stop leaves the elbow with no torque to hold the palm down.
        float foldMin = (elbow[i] != null && elbow[i].useLimits ? elbow[i].limits.min : -150f)
                        + plantFoldMarginDeg;
        float interiorMin = Mathf.Clamp(180f + foldMin, 5f, 175f) * Mathf.Deg2Rad;
        float minChord = Mathf.Sqrt(Mathf.Max(0.0001f,
            linkL1[i] * linkL1[i] + linkL2[i] * linkL2[i]
            - 2f * linkL1[i] * linkL2[i] * Mathf.Cos(interiorMin)));

        float offset = drop < minChord ? Mathf.Sqrt(minChord * minChord - drop * drop) : 0f;
        offset = Mathf.Clamp(offset, 0f, plantSideMax);

        // The fold the latched point asks for: the stroke starts here, so this is
        // the room the press has. Logged beside the delivered elbow angle, which
        // is how a jammed joint is told from a loaded one.
        float chord = Mathf.Sqrt(offset * offset + drop * drop);
        float cosInterior = (linkL1[i] * linkL1[i] + linkL2[i] * linkL2[i] - chord * chord)
                            / (2f * linkL1[i] * linkL2[i]);
        float plantFold = Mathf.Acos(Mathf.Clamp(cosInterior, -1f, 1f)) * Mathf.Rad2Deg - 180f;

        if (i == SideL)
        {
            PressPlantOffsetL = offset;
            PressPlantFoldL = plantFold;
        }
        else
        {
            PressPlantOffsetR = offset;
            PressPlantFoldR = plantFold;
        }

        float side = i == SideL ? -1f : 1f;
        plantX[i] = pivot.x + side * offset;
        plantY[i] = palmY;
    }

    // Which way the world direction of this upper arm moves with its joint angle.
    // It is NOT the same for the two sides: the arms are mirrored, and measured
    // p_arms26 the left arm's rest direction (jointAngle 0) came out at +160 deg
    // while the right arm's came out at -85 deg. Aiming with one assumed sign
    // left the right arm unable to point at its own palm at all: it sat jammed on
    // its elbow stop with the palm 0.36 m over the slab, phase "plant", forever.
    // The sign is voted from the motion both joints actually make.
    private void TrackAimSign(int i)
    {
        if (shoulder[i] == null || shoulder[i].attachedRigidbody == null) return;
        float have = UpperArmWorldDeg(i);
        float th = shoulder[i].jointAngle;
        if (aimSignSeen[i])
        {
            float dHave = Mathf.DeltaAngle(aimHavePrev[i], have);
            float dTh = th - aimThPrev[i];
            if (Mathf.Abs(dTh) > 0.4f && Mathf.Abs(dHave) > 0.4f)
                aimSignVote[i] += dHave * dTh > 0f ? 1 : -1;
        }
        aimSignSeen[i] = true;
        aimHavePrev[i] = have;
        aimThPrev[i] = th;
    }

    // World direction the upper arm points, degrees.
    private float UpperArmWorldDeg(int i)
    {
        if (shoulder[i] == null || shoulder[i].attachedRigidbody == null) return 0f;
        Rigidbody2D arm = shoulder[i].attachedRigidbody;
        Vector2 axis = (Vector2)(arm.transform.rotation * Vector3.down);
        return Mathf.Atan2(axis.y, axis.x) * Mathf.Rad2Deg;
    }

    // Centre of the world directions the shoulder can serve: the rest direction
    // (jointAngle 0) the hinge is built around. The reachable window is this
    // +- the joint's range, so an aim outside it is a joint-range question.
    private float UpperArmRestDeg(int i)
    {
        if (shoulder[i] == null) return 0f;
        return UpperArmWorldDeg(i) - shoulder[i].jointAngle;
    }

    // Joint angle that points the upper arm along a world direction. A joint
    // angle increment rotates the limb by the same increment in the world, so
    // the rest direction (the direction at jointAngle 0) is the arm's own axis
    // minus its joint angle, and the target is DeltaAngle(rest, want).
    // Deliberately NOT the incremental "current angle + shortest arc" form the
    // old aim used: with the arm pointing away from the target the shortest arc
    // is +-180 and the sign is a coin flip, and measured p_arms20 the coin came
    // up on the wrong side — the joint clamped at -90 with the palm 0.9 m in the
    // air (handY -1.12 against a -2.00 slab). Here the representative of the
    // requested angle inside the joint limits is chosen instead.
    private float ShoulderAngleForDirection(int i, float wantDeg)
    {
        if (shoulder[i] == null) return 0f;
        // The world direction moves with the joint angle as dir = rest + sign *
        // jointAngle, sign = +-1 per side, so the joint angle that points the arm
        // at wantDeg is jointAngle + sign * (wantDeg - dir).
        float sign = AimSign(i);
        float target = shoulder[i].jointAngle
            + sign * Mathf.DeltaAngle(UpperArmWorldDeg(i), wantDeg) * Mathf.Clamp01(pressAimGain);
        if (shoulder[i].useLimits)
        {
            float lo = shoulder[i].limits.min;
            float hi = shoulder[i].limits.max;
            float now = shoulder[i].jointAngle;
            float best = Mathf.Clamp(target, lo, hi);
            float alt = target + (target >= 0f ? -360f : 360f);
            if (alt >= lo && alt <= hi && Mathf.Abs(alt - now) < Mathf.Abs(best - now)) best = alt;
            target = best;
        }
        return target;
    }

    // +1 or -1: how this side's joint angle maps onto a world direction change.
    private float AimSign(int i)
    {
        return aimSignVote[i] >= 0 ? 1f : -1f;
    }

    public float PressAimSignL { get { return AimSign(SideL); } }
    public float PressAimSignR { get { return AimSign(SideR); } }

    private float ClampElbow(int i, float target)
    {
        if (elbow[i] != null && elbow[i].useLimits)
            target = Mathf.Clamp(target, elbow[i].limits.min, elbow[i].limits.max);
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
        plantSideMax = ReadFloat(args, ArgPlantSide, plantSideMax);
        pressStrokeDegPerSecond = ReadFloat(args, ArgStrokeRate, pressStrokeDegPerSecond);
        pressMinStrokeSeconds = ReadFloat(args, ArgStrokeMin, pressMinStrokeSeconds);
        pressAimGain = ReadFloat(args, ArgAimGain, pressAimGain);
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
