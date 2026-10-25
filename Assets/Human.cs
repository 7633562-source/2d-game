using UnityEngine;

public class Human : MonoBehaviour
{
    [Header("Total body mass (kg)")]
    public float totalMass = 70f;

    // Each segment gets its own semi-transparent color on the collider box.
    // No Human/* PNG — the boxes are a layout guide while art is redrawn.
    private static readonly Color PelvisColor = new Color(0.05f, 0.15f, 0.6f, 0.5f);
    private static readonly Color TorsoColor = new Color(0.1f, 0.2f, 1f, 0.5f);
    private static readonly Color NeckColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
    private static readonly Color HeadColor = new Color(1f, 1f, 0f, 0.5f);

    private static readonly Color RightUpperArmColor = new Color(0f, 0.6f, 0f, 0.5f);
    private static readonly Color LeftUpperArmColor = new Color(0.4f, 0.8f, 0.2f, 0.5f);
    private static readonly Color RightLowerArmColor = new Color(0.8f, 0.1f, 0.1f, 0.5f);
    private static readonly Color LeftLowerArmColor = new Color(1f, 0.5f, 0.5f, 0.5f);
    private static readonly Color RightHandColor = new Color(1f, 0.5f, 0f, 0.5f);
    private static readonly Color LeftHandColor = new Color(1f, 0.7f, 0.3f, 0.5f);

    private static readonly Color RightThighColor = new Color(0.5f, 0f, 0.5f, 0.5f);
    private static readonly Color LeftThighColor = new Color(0.7f, 0.4f, 0.7f, 0.5f);
    private static readonly Color RightShinColor = new Color(0.5f, 0.25f, 0f, 0.5f);
    private static readonly Color LeftShinColor = new Color(0.7f, 0.45f, 0.2f, 0.5f);
    private static readonly Color RightFootColor = new Color(0.4f, 0f, 0f, 0.5f);
    private static readonly Color LeftFootColor = new Color(0.8f, 0.3f, 0.3f, 0.5f);

    [Header("Multipliers (for logging)")]
    public float muscleMultiplier = 1f;
    public float frictionMultiplier = 1f;

    // System references
    public VestibularSystem vestibularSystem;
    public CenterOfMassCalculator comCalculator;
    public BodyStateEstimator bodyStateEstimator;   // added
    public HumanSegment headSegment;

    // Mass fractions. Pelvis and chest together are 0.4346 — the former whole torso,
    // so total body mass stays 70 kg (de Leva, 1996).
    private const float PELVIS_MASS_FRACTION = 0.1117f;
    private const float TORSO_MASS_FRACTION = 0.3229f;
    private const float HEAD_MASS_FRACTION = 0.0694f;
    private const float NECK_MASS_FRACTION = 0.012f;
    private const float UPPER_ARM_MASS_FRACTION = 0.0271f;
    private const float LOWER_ARM_MASS_FRACTION = 0.0162f;
    private const float HAND_MASS_FRACTION = 0.0061f;
    private const float THIGH_MASS_FRACTION = 0.1416f;
    private const float SHIN_MASS_FRACTION = 0.0433f;
    private const float FOOT_MASS_FRACTION = 0.0137f;

    // The camera looks 80° off the frontal plane, almost in profile.
    // Left-right spacing between paired joints shrinks on screen by
    // cos(80°) ≈ 0.17. In the sagittal 2D model the X axis is
    // forward-back, so an X spread is only a viewpoint shift,
    // not a real step: otherwise the human stands in a permanent lunge.
    [Header("Viewpoint: almost profile")]
    public float viewAngleDegrees = 80f;

    [Header("Anatomical half-spacing between paired joints (m)")]
    public float hipHalfSpacing = 0.085f;
    public float shoulderHalfSpacing = 0.185f;

    // -buildLegOrder 1: LeftLeg and LeftArm are created before their right peers, so the joint and
    // contact order in Box2D is left-first. -shoulderCenterX 1: both arm anchors sit at x = 0.
    // Both are skeleton changes, default off, read once per BuildHuman.
    private bool buildLegLeftFirst;
    private bool shoulderCenterXOn;

    // CYCLE 91: THE STRIDE STANCE (owner's 28.09 reference, refs/owner-stance-20260928.md).
    // The split stand: the front ankle lands stanceStrideFront metres AHEAD of the
    // pelvis, the back ankle stanceStrideBack metres BEHIND it. Unlike hipHalfSpacing
    // (only the near-profile projection of the lateral spread), this is a real fore-aft
    // base - the axis a 2D fall actually happens along. HeadlessTrial computes the pair
    // from -stanceStride with the owner's measured ratios (front +0.076 H, back
    // -0.110 H, H = 1.75 m stature); zero (the default) keeps every earlier take's
    // geometry bit for bit. Front = the RightLeg (direction +1, the forward leg).
    public float stanceStrideFront = 0f;   // m: front ankle ahead of the pelvis (+X)
    public float stanceStrideBack = 0f;    // m: back ankle behind the pelvis (applied -X)

    private float LateralProjection => Mathf.Cos(viewAngleDegrees * Mathf.Deg2Rad);

    // Colored boxes sit on the collider. A side shift was for painted
    // overlap; it slid the rectangle off the BoxCollider2D.
    [Header("Picture offset (draw only)")]
    public float visualSideOffset = 0f;

    // 0.1 units at torso width 0.24 made a blob half a trunk wide per joint.
    [Header("Joint marker diameter (m)")]
    public float jointMarkerDiameter = 0.03f;

    // Height 1.75 m at 70 kg: lengths are fractions of stature per Winter (2009);
    // widths are anteroposterior size in side view (torso is chest depth, not shoulders).
    // Vertical chain pelvis+torso+neck+head+thigh+shin+foot = 1.75.
    [Header("Segment sizes")]
    public Vector2 pelvisSize = new Vector2(0.24f, 0.16f);
    public Vector2 torsoSize = new Vector2(0.24f, 0.34f);
    public Vector2 headSize = new Vector2(0.20f, 0.23f);
    public Vector2 neckSize = new Vector2(0.12f, 0.09f);
    public Vector2 upperArmSize = new Vector2(0.10f, 0.33f);
    public Vector2 lowerArmSize = new Vector2(0.08f, 0.34f);
    // 0.32 m was almost a forearm: the hand read as a third link.
    // Winter: hand ≈ 0.108 × 1.75 m. Width is side view, not a palm in front view.
    // Stance vis_hand (0.19) was accepted. Do not revert to 0.32 without a walk summary.
    // Physical hand length is 0.32 (CoM/wrist). 0.19 broke walk/stance fold ~25 s
    // (handfix0). Draw it shorter on Visual only, not this field.
    public Vector2 handSize = new Vector2(0.06f, 0.16f);
    public Vector2 thighSize = new Vector2(0.16f, 0.43f);
    public Vector2 shinSize = new Vector2(0.11f, 0.43f);
    // Adult foot length, height ≈ sole plus instep. Split in CreateLeg into
    // heel + toe of this same length (footHeelShare); do not grow footSize
    // to add the toe.
    public Vector2 footSize = new Vector2(0.26f, 0.09f);

    // Heel fraction of footSize.x. Toe is the rest. 0.70 / 0.30 puts the
    // MTP hinge at the ball, 0.117 m ahead of the ankle, and keeps the
    // 0.065 m heel and 0.195 m toe arms of the rigid plate.
    [Header("Foot split (heel / toe)")]
    [Range(0.4f, 0.85f)]
    public float footHeelShare = 0.70f;

    // Sole-to-ground grip. 0.4 is the former behavior: a collider with no
    // material takes Unity's default, i.e. a smooth sole. Higher is
    // footwear. The pair is sqrt(µ_foot · µ_ground), so one
    // foot cannot raise grip above the square root of ground friction.
    [Header("Sole-to-ground friction")]
    public float footFriction = 1.2f;

    // The ankle sits nearer the heel: 6.5 cm of heel behind it and 19.5 cm
    // of toe in front, so the foot reaches farther forward than back.
    [Header("Ankle offset toward the heel (m)")]
    public float ankleHeelOffset = 0.065f;

    // The shoulder stays at the former absolute height +0.125 from the root:
    // chest top 0.25 minus this drop. Raise it to anatomical height
    // separately — otherwise one step changes both the lumbar and the arm lever.
    [Header("Shoulder relative to chest top (m)")]
    public float shoulderDropFromNeck = 0.09f;

    // Palm-to-ground grip. The fall press is a closed chain: the elbow can
    // only lift the torso while the hand holds the slab, so the palm gets a
    // material instead of Unity's smooth default. Same pairing rule as the
    // sole — sqrt(µ_palm · µ_ground).
    // 1.0 gripped less than the sole (1.2): a press that slides is a press that
    // lifts nothing, so the palm is over the sole value on purpose.
    [Header("Palm-to-ground friction")]
    public float handFriction = 1.4f;

    // Segment masses are the same, sizes are smaller — torso inertia dropped
    // about threefold. The former friction did not damp PD; the torso wobbled at ~30 Hz.
    [Header("Joint friction")]
    public float lumbarFriction = 30f;
    // 40 N·m saturates at 76 °/s. Raising it to 80/160/240 did not remove chatter,
    // it sped it up: rmsTorsoAngVel 55 → 74 → 123 → 164 °/s.
    public float lumbarFrictionMaxTorque = 40f;
    // τ = Ieff/K ≈ 0.20 s at neck Ieff 0.001567. The former 36 welded
    // the head to the torso: stop-torque clips everything above Ieff/Δt ≈ 0.31, so K=36
    // is indistinguishable from K=0.31. SPD holds the joints; the weld is no longer needed.
    // The head shares this same field.
    public float neckFriction = 0.00784f;
    // Arms: τ ≈ 0.10 s. Ieff from arm_stand — shoulder 0.0177722, elbow
    // 0.00509677, wrist 0.00117525. The former 12 / 12 / 1.2 welded the joints
    // (stepRatio 3.4 / 11.8 / 5.1). SPD already holds the pose; τ=0.20 drops
    // the forward push 24 N·s, so we stopped at 0.10.
    public float shoulderFriction = 0.177722f;
    public float elbowFriction = 0.0509677f;
    public float wristFriction = 0.0117525f;
    public float hipFriction = 12f;
    public float kneeFriction = 12f;
    public float ankleFriction = 3f;
    // MTP is a light hinge: K here is JointFriction on the toe, not sole
    // µ (that is footFriction on both boxes). SPD holds the pose, so this
    // stays below the weld clamp.
    public float toeFriction = 0.02f;

    // HingeJoint2D has no spring-on-angle — only this arc plus muscle PD.
    // Flexion is minus. The arc is the last word on every hip command:
    // ControlJointToAngleStable clamps its target to joint.limits. The swing
    // pose asks for swingHipFlex 40 plus the walk boosts (up to 62) and the
    // step-placement correction (±10), and crouch passes a transient before
    // the pelvis tilt catches the folding knee. A −45 flexion stop pins all of
    // that on the stop: the pose saturates and placement loses the room it
    // needs to lengthen a short step. Flexion keeps the former −90.
    // Extension is barely used in stance (maxHip ≈ +2), so +45 stops the
    // back leg of a gravity split without a new motor.
    [Header("Leg hinge limits (deg)")]
    public float hipLimitMin = -90f;
    public float hipLimitMax = 45f;
    public float kneeLimitMin = 0f;
    public float kneeLimitMax = 120f;
    // MTP: minus = toe up (swing clearance), plus = toe down / heel rise
    // (push-off). jointAngle = heel − toe, same convention as the ankle.
    public float toeLimitMin = -35f;
    public float toeLimitMax = 50f;

    // Ceilings are close to what stance needs: at 70 kg
    // and a 5 cm CoM offset the ankle needs about 35 N·m.
    // The former 150–300 N·m arrived in full and tore the body apart in two physics steps.
    [Header("Muscle torque ceilings")]
    // About 35 kg sits above the lumbar, its center roughly 0.30 m above the joint.
    // Gravity gives ≈ 104·sin θ N·m, i.e. 36 N·m at a 20° lean.
    // 90 N·m is a 2.5× margin and still below the hip (100 N·m).
    public float lumbarMuscleTorque = 90f;
    public float neckMuscleTorque = 15f;
    public float shoulderMuscleTorque = 40f;
    public float elbowMuscleTorque = 30f;
    public float wristMuscleTorque = 10f;
    public float hipMuscleTorque = 100f;
    // 130 N·m: a squat folds the knee far from −8°, and 80 N·m
    // cannot hold the thigh lever. Ceiling from the work plan, not from de Leva.
    public float kneeMuscleTorque = 130f;
    // The human ankle is sharply asymmetric: plantar flexors (calf,
    // hold against a forward fall) are three times stronger than dorsiflexors (front of
    // the shin, hold against a backward fall). In the model those are extensor and flexor.
    // Ceilings come from foot geometry: the foot will not transmit a larger moment;
    // the body simply starts rotating about the toe or the heel.
    // Foot 0.26 m, heel 0.065 m, toe 0.195 m, m*g = 70*9.81 ≈ 687 N.
    // Forward: 687*(toe 0.195)/2 ≈ 67 N·m per foot (rounded to 65).
    // Backward:  687*(heel 0.065)/2 ≈ 22 N·m per foot.
    public float ankleExtensorTorque = 65f;
    public float ankleFlexorTorque = 22f;
    // Pose hold on the MTP, not a second ankle. 12 N·m on a 0.078 m toe
    // is ~0.22 BW — enough to keep the plate, not to lift 70 kg.
    public float toeMuscleTorque = 12f;

    // Draw order
    private const int LEFT_LEG_THIGH_ORDER = -6;
    private const int LEFT_LEG_SHIN_ORDER = -5;
    private const int LEFT_LEG_FOOT_ORDER = -4;
    private const int LEFT_ARM_UPPER_ORDER = -3;
    private const int LEFT_ARM_LOWER_ORDER = -2;
    private const int LEFT_ARM_HAND_ORDER = -1;
    private const int TORSO_ORDER = 0;
    private const int NECK_ORDER = 1;
    private const int HEAD_ORDER = 2;
    private const int RIGHT_LEG_THIGH_ORDER = 3;
    private const int RIGHT_LEG_SHIN_ORDER = 4;
    private const int RIGHT_LEG_FOOT_ORDER = 5;
    private const int RIGHT_ARM_UPPER_ORDER = 6;
    private const int RIGHT_ARM_LOWER_ORDER = 7;
    private const int RIGHT_ARM_HAND_ORDER = 8;

    public void BuildHuman()
    {
        // -buildLegOrder / -shoulderCenterX (default off): the limb creation order and the arm
        // anchor x are chosen here, once, before anything is assembled.
        buildLegLeftFirst = LauncherArgs.Float("-buildLegOrder", 0f) > 0.5f;
        shoulderCenterXOn = LauncherArgs.Float("-shoulderCenterX", 0f) > 0.5f;

        // WALK CYCLE 66: the foreshortening is applied BEFORE anything is assembled, so
        // the whole rig (positions, anchors, boxes, masses) is born with L'.
        ForeshortenPrepare();

        foreach (Transform child in transform)
        {
            Destroy(child.gameObject);
        }

        // The root stays at the center of the old whole torso. Pelvis and chest
        // are placed so the hips sit at y = −0.25 and the neck base
        // at y = +0.25: the vertical chain and spawn point do not change.
        float trunkHeight = pelvisSize.y + torsoSize.y;
        float pelvisCenterY = -trunkHeight * 0.5f + pelvisSize.y * 0.5f;
        float torsoCenterY = -trunkHeight * 0.5f + pelvisSize.y + torsoSize.y * 0.5f;

        HumanSegment pelvis = CreateSegment("Pelvis", pelvisSize, PELVIS_MASS_FRACTION, PelvisColor, TORSO_ORDER);
        pelvis.transform.localPosition = new Vector3(0, pelvisCenterY, 0);

        HumanSegment torso = CreateSegment("Torso", torsoSize, TORSO_MASS_FRACTION, TorsoColor, TORSO_ORDER);
        torso.transform.localPosition = new Vector3(0, torsoCenterY, 0);
        torso.ConnectTo(pelvis,
            new Vector2(0, -torsoSize.y * 0.5f),
            new Vector2(0, pelvisSize.y * 0.5f),
            -20f, 20f);
        // The lumbar carries ~35 kg of upper body: the default friction ceiling
        // 15 N·m is too small, and viscosity silently hits it. The ceiling is set
        // separately so viscosity stays linear on fast impacts.
        AddFriction(torso, lumbarFriction, lumbarFrictionMaxTorque);
        AddMuscles(torso, lumbarMuscleTorque);

        HumanSegment neck = CreateSegment("Neck", neckSize, NECK_MASS_FRACTION, NeckColor, NECK_ORDER);
        neck.transform.localPosition = new Vector3(0, torsoCenterY + torsoSize.y * 0.5f + neckSize.y * 0.5f, 0);
        neck.ConnectTo(torso,
            new Vector2(0, -neckSize.y * 0.5f),
            new Vector2(0, torsoSize.y * 0.5f),
            -30f, 30f);
        AddFriction(neck, neckFriction);
        AddMuscles(neck, neckMuscleTorque);

        HumanSegment head = CreateSegment("Head", headSize, HEAD_MASS_FRACTION, HeadColor, HEAD_ORDER);
        head.transform.localPosition = new Vector3(0, torsoCenterY + torsoSize.y * 0.5f + neckSize.y + headSize.y * 0.5f, 0);
        head.ConnectTo(neck,
            new Vector2(0, -headSize.y * 0.5f),
            new Vector2(0, neckSize.y * 0.5f),
            -30f, 30f);
        AddFriction(head, neckFriction);
        AddMuscles(head, neckMuscleTorque);
        headSegment = head;

        // Swap shoulder attachment sides: right arm uses the former left anchor,
        // left arm uses the former right anchor.
        if (buildLegLeftFirst)
        {
            CreateArm("LeftArm", 1, torso,
                LEFT_ARM_UPPER_ORDER, LEFT_ARM_LOWER_ORDER, LEFT_ARM_HAND_ORDER,
                LeftUpperArmColor, LeftLowerArmColor, LeftHandColor);
            CreateArm("RightArm", -1, torso,
                RIGHT_ARM_UPPER_ORDER, RIGHT_ARM_LOWER_ORDER, RIGHT_ARM_HAND_ORDER,
                RightUpperArmColor, RightLowerArmColor, RightHandColor);
        }
        else
        {
            CreateArm("RightArm", -1, torso,
                RIGHT_ARM_UPPER_ORDER, RIGHT_ARM_LOWER_ORDER, RIGHT_ARM_HAND_ORDER,
                RightUpperArmColor, RightLowerArmColor, RightHandColor);
            CreateArm("LeftArm", 1, torso,
                LEFT_ARM_UPPER_ORDER, LEFT_ARM_LOWER_ORDER, LEFT_ARM_HAND_ORDER,
                LeftUpperArmColor, LeftLowerArmColor, LeftHandColor);
        }

        if (buildLegLeftFirst)
        {
            CreateLeg("LeftLeg", -1, pelvis,
                LEFT_LEG_THIGH_ORDER, LEFT_LEG_SHIN_ORDER, LEFT_LEG_FOOT_ORDER,
                LeftThighColor, LeftShinColor, LeftFootColor);
            CreateLeg("RightLeg", 1, pelvis,
                RIGHT_LEG_THIGH_ORDER, RIGHT_LEG_SHIN_ORDER, RIGHT_LEG_FOOT_ORDER,
                RightThighColor, RightShinColor, RightFootColor);
        }
        else
        {
            CreateLeg("RightLeg", 1, pelvis,
                RIGHT_LEG_THIGH_ORDER, RIGHT_LEG_SHIN_ORDER, RIGHT_LEG_FOOT_ORDER,
                RightThighColor, RightShinColor, RightFootColor);
            CreateLeg("LeftLeg", -1, pelvis,
                LEFT_LEG_THIGH_ORDER, LEFT_LEG_SHIN_ORDER, LEFT_LEG_FOOT_ORDER,
                LeftThighColor, LeftShinColor, LeftFootColor);
        }

        DisableCollisionsBetweenSegments();

        // Sensors are added only after every segment is built:
        // their Awake finds bodies via transform.Find once and does not
        // search again. VestibularSystem used to sit higher in the file, before neck and
        // head existed, and silently returned zeros for them — torso and pelvis already
        // existed by then, so torso tilt worked and nothing reported the error.
        vestibularSystem = gameObject.AddComponent<VestibularSystem>();

        comCalculator = gameObject.AddComponent<CenterOfMassCalculator>();
        comCalculator.Initialize();

        // Add the sensor layer
        bodyStateEstimator = gameObject.AddComponent<BodyStateEstimator>();

        SetupBalanceController();

        // LEVEL 2 pose drive. The component is always built, but it only takes
        // the joints over when -poseDrive 1 is on the command line; with the
        // switch off the tree behaves exactly as before (rollback is one flag).
        PoseDriveController poseDrive = gameObject.AddComponent<PoseDriveController>();
        poseDrive.Initialize(this, GetComponent<BalanceController>());

        // Balance assist (owner's decision 03.10): added only when -assist or -pushAt
        // is on the command line; without them the body is the pure-physics one.
        BalanceAssist.AttachIfRequested(gameObject);

        // Friction, then muscles — one FixedUpdate, order is explicit.
        ActuatorDriver actuators = gameObject.AddComponent<ActuatorDriver>();
        actuators.RebuildCache();

        // Arms. Owns the shoulder/elbow activations only, and rewrites them
        // after BalanceController (order 0) and before the actuator pass above
        // applies them (order 50): reciprocal swing on the walk, press on the
        // fall. Everything else on the arm stays with the balance controller.
        ArmDrive armDrive = gameObject.AddComponent<ArmDrive>();
        armDrive.Bind();

        // The leg strike (kick cycle k1). Owns ONLY the kicking leg's hip/knee/
        // ankle muscles while a stroke is in flight; it is ticked at execution
        // order 30 (after BalanceController's 0 and ArmDrive's 10, before the
        // actuator pass at 50), so the pose path's early return cannot skip it
        // and the support leg's writers are never touched. Nothing is written
        // while the kick is off (R2).
        LegStrikeDrive legStrike = gameObject.AddComponent<LegStrikeDrive>();
        legStrike.Bind();

        Damageable life = gameObject.AddComponent<Damageable>();
        life.maxHealth = 100f;
        life.health = 100f;
    }

    public void AddNumberLabel(int number)
    {
        if (headSegment == null) return;
        GameObject labelObj = new GameObject("NumberLabel");
        Transform labelParent = headSegment.visual != null ? headSegment.visual : headSegment.transform;
        labelObj.transform.SetParent(labelParent, false);
        labelObj.transform.localPosition = new Vector3(0, headSegment.size.y * 0.5f + 0.15f, -0.1f);
        TextMesh textMesh = labelObj.AddComponent<TextMesh>();
        textMesh.text = number.ToString();
        textMesh.fontSize = 36;
        textMesh.characterSize = 0.05f;
        textMesh.color = Color.black;
        textMesh.anchor = TextAnchor.MiddleCenter;
        Renderer renderer = labelObj.GetComponent<Renderer>();
        renderer.sortingOrder = 100;
    }

    private HumanSegment CreateSegment(
        string name,
        Vector2 size,
        float massFraction,
        Color color,
        int sortingOrder,
        HumanVisualShape visualShape = HumanVisualShape.Box,
        string albedoKey = null,
        float massScale = 1f)
    {
        GameObject segmentObject = new GameObject(name);
        HumanSegment segment = segmentObject.AddComponent<HumanSegment>();
        float segmentMass = totalMass * massFraction * massScale;
        segment.Initialize(name, size, segmentMass, color, transform, sortingOrder, visualShape, albedoKey);
        // Before ConnectTo: the marker is created there and reads this size.
        segment.jointMarkerDiameter = jointMarkerDiameter;
        return segment;
    }

    private void AddFriction(HumanSegment segment, float damping, float maxTorque = 15f)
    {
        if (segment.joint != null)
        {
            JointFriction friction = segment.gameObject.AddComponent<JointFriction>();
            friction.damping = damping;
            friction.maxTorque = maxTorque;
        }
    }

    private void AddMuscles(HumanSegment segment, float maxTorque)
    {
        AddMuscles(segment, maxTorque, maxTorque);
    }

    private void AddMuscles(HumanSegment segment, float flexorTorque, float extensorTorque)
    {
        if (segment.joint == null) return;
        Muscle flexor = segment.gameObject.AddComponent<Muscle>();
        flexor.maxTorque = flexorTorque;
        flexor.direction = 1f;
        flexor.activation = 0f;
        Muscle extensor = segment.gameObject.AddComponent<Muscle>();
        extensor.maxTorque = extensorTorque;
        extensor.direction = -1f;
        extensor.activation = 0f;
    }

    // ─── WALK CYCLE 66 — THE FORESHORTENING, THE PHYSICS HALF ───
    //
    // -foreshorten <0..0.4> on the THIGH and the UPPER ARM (the SAME flag the art half
    // already carries for the picture — ART-H5f, HumanArt): the segment's length
    // L' = L·(1−v) and, with it, (a) its BoxCollider2D box along the bone, (b) BOTH
    // hinge anchors (±L'/2) so the chain never detaches, (c) its mass — the flavour is
    // the switch's (-fsMass hold|project). The owner's correction, 24.09: «я не про
    // визуал, я про физику, скорее».
    //
    // THE LENGTHS ARE THE SINGLE SOURCE. Every planner that matters reads either the
    // size fields below (PoseDriveController: poseIn.thighM, legLengthM,
    // pelvisCenterToSole; StepPhaseDriver: the leg height) or the built hinge anchors
    // (BalanceController.ResolveLegLinkOnce: the two-bar leg), and both are read AFTER
    // the build — so a body born short re-solves with no pop and no second re-read.
    // v = 0 (the default) writes nothing: the accepted rows stay byte-identical.
    //
    // A VALUE THAT MOVES after the build (-fsRampAt <s> + -fsRamp <v per second>) is
    // the rate-limited channel (FixedUpdate below): only the geometry DESCRIPTION
    // moves — the collider box, the anchors, the mass; no body is teleported, the
    // joints re-solve the chain, and the lazy readers are poked so nothing keeps a
    // stale length. -fsRamp 0 at t0 is the instant jump, kept as the pop control.
    public const float ForeshortenMax = 0.40f;   // the art half's own ceiling

    private float foreshortenBaseThighY;
    private float foreshortenBaseUpperY;
    private float foreshortenApplied;        // v currently written into the body
    private float foreshortenTarget;         // v asked for
    private float foreshortenRampAt;         // s; < 0 = applied at build
    private float foreshortenRamp;           // v per second; <= 0 = instant
    private float foreshortenClock;
    private bool foreshortenChannelArmed;
    private bool foreshortenMassProjected;
    private BalanceController balanceController;

    // The built mass of a shortened segment comes from CreateSegment's own arithmetic
    // (totalMass × fraction × k), so a runtime change lands on the built value.
    private float ForeshortenMassScale()
    {
        return foreshortenMassProjected ? 1f - foreshortenApplied : 1f;
    }

    private void ForeshortenPrepare()
    {
        foreshortenBaseThighY = thighSize.y;
        foreshortenBaseUpperY = upperArmSize.y;
        foreshortenMassProjected = HeadlessTrial.ForeshortenMassProjected;
        foreshortenTarget = Mathf.Clamp(HeadlessTrial.Foreshorten, 0f, ForeshortenMax);
        foreshortenRampAt = HeadlessTrial.ForeshortenRampAt;
        foreshortenRamp = HeadlessTrial.ForeshortenRamp;
        if (foreshortenTarget <= 0f)
            return;
        if (foreshortenRampAt < 0f)
            ApplyForeshortenValue(foreshortenTarget);   // born short
        else
            foreshortenChannelArmed = true;              // a value that moves
    }

    // The single writer of the applied value. The fields are scaled, so the builder
    // (positions, anchors, boxes, markers) and every reader of Human.thighSize /
    // upperArmSize follow L' with no second code path.
    private void ApplyForeshortenValue(float v)
    {
        foreshortenApplied = Mathf.Clamp(v, 0f, ForeshortenMax);
        float k = 1f - foreshortenApplied;
        thighSize = new Vector2(thighSize.x, foreshortenBaseThighY * k);
        upperArmSize = new Vector2(upperArmSize.x, foreshortenBaseUpperY * k);
    }

    // The rate-limited channel: a length that moves after the build.
    private void FixedUpdate()
    {
        if (!foreshortenChannelArmed)
            return;
        foreshortenClock += Time.fixedDeltaTime;
        if (foreshortenClock < foreshortenRampAt)
            return;
        float step = foreshortenRamp > 0f ? foreshortenRamp * Time.fixedDeltaTime : float.MaxValue;
        float next = Mathf.Min(foreshortenApplied + step, foreshortenTarget);
        if (next <= foreshortenApplied)
        {
            foreshortenChannelArmed = false;
            return;
        }
        ApplyForeshortenValue(next);
        PortForeshortenToBuiltBody();
    }

    // The runtime port: the built segments take the new length. The child ends follow
    // through the joints (their own anchors are on the child and do not move), and the
    // balance controller's lazily-resolved bars / paired inertias are cleared so the
    // next use re-measures them instead of planning on a stale length.
    private void PortForeshortenToBuiltBody()
    {
        float k = 1f - foreshortenApplied;
        PortBone("LeftLegThigh", "LeftLegShin", foreshortenBaseThighY * k, THIGH_MASS_FRACTION);
        PortBone("RightLegThigh", "RightLegShin", foreshortenBaseThighY * k, THIGH_MASS_FRACTION);
        PortBone("LeftArmUpper", "LeftArmLower", foreshortenBaseUpperY * k, UPPER_ARM_MASS_FRACTION);
        PortBone("RightArmUpper", "RightArmLower", foreshortenBaseUpperY * k, UPPER_ARM_MASS_FRACTION);
        if (balanceController != null)
            balanceController.OnBodyGeometryChanged();
    }

    private void PortBone(string boneName, string childName, float lengthY, float massFraction)
    {
        Transform boneTransform = transform.Find(boneName);
        if (boneTransform == null)
            return;
        HumanSegment bone = boneTransform.GetComponent<HumanSegment>();
        if (bone == null)
            return;
        bone.size = new Vector2(bone.size.x, lengthY);
        if (bone.collider != null)
            bone.collider.size = bone.size;
        // The hip / shoulder anchor sits on the bone (+L'/2); the joint the CHILD
        // carries points back at the bone's far end (−L'/2) and is the bone's to write.
        if (bone.joint != null)
            bone.joint.anchor = new Vector2(0f, lengthY * 0.5f);
        Transform childTransform = transform.Find(childName);
        HumanSegment child = childTransform != null ? childTransform.GetComponent<HumanSegment>() : null;
        if (child != null && child.joint != null)
            child.joint.connectedAnchor = new Vector2(0f, -lengthY * 0.5f);
        float mass = totalMass * massFraction * ForeshortenMassScale();
        bone.mass = mass;
        if (bone.rb != null)
            bone.rb.mass = mass;
    }

    private void CreateArm(string sideName, float direction, HumanSegment torso,
                           int upperOrder, int lowerOrder, int handOrder,
                           Color upperColor, Color lowerColor, Color handColor)
    {
        float shoulderX = shoulderCenterXOn ? 0f : direction * shoulderHalfSpacing * LateralProjection;
        float visualShift = direction * visualSideOffset;
        // Drop from chest top, not a fraction of height: after the torso split
        // absolute shoulder height must stay +0.125 from the root.
        // shoulderY is in chest local (joint anchor); shoulderRootY is from the root
        // (segment placement — they are children of Human, not Torso).
        float shoulderY = torsoSize.y * 0.5f - shoulderDropFromNeck;
        float shoulderRootY = torso.transform.localPosition.y + shoulderY;

        HumanSegment upper = CreateSegment(sideName + "Upper", upperArmSize, UPPER_ARM_MASS_FRACTION, upperColor, upperOrder,
            HumanVisualShape.Box, null, ForeshortenMassScale());
        upper.transform.localPosition = new Vector3(shoulderX, shoulderRootY - upperArmSize.y * 0.5f, 0);
        upper.SetVisualOffset(visualShift);
        // Shoulder range -160..90, not the former symmetric +-90. A real shoulder
        // swings about 180 deg (flexion to overhead), and the fall press needs all
        // of it: measured p_arms23, with the body lying down the arm's rest
        // direction sits at 85-120 deg world and the floor lies 90-130 deg below
        // that, so a +-90 hinge cannot even aim the arm at the palm it has to
        // press on — the aim flipped between its two stops every frame and the
        // arm hung in the air (handY -1.36 against a -1.99 slab).
        upper.ConnectTo(torso,
            new Vector2(0, upperArmSize.y * 0.5f),
            new Vector2(shoulderX, shoulderY),
            -160f, 90f);
        AddFriction(upper, shoulderFriction);
        AddMuscles(upper, shoulderMuscleTorque);

        HumanSegment lower = CreateSegment(sideName + "Lower", lowerArmSize, LOWER_ARM_MASS_FRACTION, lowerColor, lowerOrder);
        lower.transform.localPosition = new Vector3(shoulderX, shoulderRootY - upperArmSize.y - lowerArmSize.y * 0.5f, 0);
        lower.SetVisualOffset(visualShift);
        // Elbow flexion to -165, not -150, and the reason is a chord, not anatomy.
        // The press is a closed chain: the arm can only push the shoulder away
        // from its own palm, along the chord, so the LIFT fraction of a stroke is
        // drop/chord (drop = how far the shoulder sits above the slab). A lying
        // shoulder sits 0.12 m over the slab, and the shortest chord this rig can
        // fold to is what fixes the press. Measured p_arms21 at -140: plant 0.30 m
        // aside, push 57 deg off vertical, shoulder rise 70 mm. Measured p_arms21
        // -> p_arms22 at -150: plant 0.25 m aside. Measured 19.09 (p_arms70, this
        // rig, press ON) against the same build with the press OFF (p_arms71):
        // the stroke opens the elbow 40-80 deg and the shoulder pivot rises
        // 0.005 m -- the run's whole shoulder band (-1.835..-1.709 m) is present
        // with the arms doing NOTHING, while the body is shoved 2.08 m along the
        // slab (comX 1.501 -> 3.582) against 0.09 m with the press off. The chord
        // at the plant is 0.28 m long against a 0.12 m drop, i.e. a 25 deg push.
        // At -165 the same rig folds to a 0.136 m chord, which puts the palm
        // within 0.065 m of straight under the shoulder at that drop (a 61 deg
        // push) and makes "the palm under the shoulder" reachable at all.
        lower.ConnectTo(upper,
            new Vector2(0, lowerArmSize.y * 0.5f),
            new Vector2(0, -upperArmSize.y * 0.5f),
            -165f, 0f);
        AddFriction(lower, elbowFriction);
        AddMuscles(lower, elbowMuscleTorque);

        HumanSegment hand = CreateSegment(sideName + "Hand", handSize, HAND_MASS_FRACTION, handColor, handOrder);
        hand.transform.localPosition = new Vector3(shoulderX, shoulderRootY - upperArmSize.y - lowerArmSize.y - handSize.y * 0.5f, 0);
        hand.SetVisualOffset(visualShift);
        hand.ConnectTo(lower,
            new Vector2(0, handSize.y * 0.5f),
            new Vector2(0, -lowerArmSize.y * 0.5f),
            -60f, 60f);
        AddFriction(hand, wristFriction);
        AddMuscles(hand, wristMuscleTorque);
        // The fall press is a closed chain: the elbow cannot lift the torso
        // off a smooth slab, so the palm carries a material beside the sole.
        if (hand.collider != null)
            hand.collider.sharedMaterial = PalmMaterial();
    }

    // One material for both feet: Box2D combines pair friction as
    // sqrt(µ1 · µ2), so the value only makes sense together with the ground.
    private PhysicsMaterial2D soleMaterial;

    private PhysicsMaterial2D SoleMaterial()
    {
        if (soleMaterial == null)
        {
            soleMaterial = new PhysicsMaterial2D("HumanSole");
            soleMaterial.friction = Mathf.Max(0f, footFriction);
            soleMaterial.bounciness = 0f;
        }

        return soleMaterial;
    }

    private PhysicsMaterial2D palmMaterial;

    private PhysicsMaterial2D PalmMaterial()
    {
        if (palmMaterial == null)
        {
            palmMaterial = new PhysicsMaterial2D("HumanPalm");
            palmMaterial.friction = Mathf.Max(0f, handFriction);
            palmMaterial.bounciness = 0f;
        }

        return palmMaterial;
    }

    // ─── THE HAND GRIP (walk cycle 40): the palm's own gripped material ───
    // Yury's word (22.09): a man can raise the friction of his own hands and
    // grip the ground, and that is what helps him get up. The pattern is the
    // dog's claws, verbatim in shape (Dog.cs ClawContactMaterial / SetClawGrip,
    // dog cycle 31): ONE extra material beside the palm's own, swapped onto the
    // palm's collider while a grip holds and released back to the SAME
    // PalmMaterial instance the build assigned, so a free palm is identical to
    // a palm that was never gripped.
    // The knob is `-poseHandGripMu <f>` - a SCALE on the palm's own value, the
    // dog's `-dogClawFriction` pattern, read here because the material lives
    // here. Default 3.0: 1.4 * 3.0 = 4.2, the same order of magnitude as the
    // dog's claw material (4.0).
    // Nothing here decides WHEN a grip holds: the get-up law reads the palm's
    // own measured contact and calls SetHandGrip (PoseDriveController, walk
    // cycle 40). This file owns WHAT the swap is. Default OFF by construction:
    // with -poseHandGrip unset nothing ever calls the handle, and the palm
    // carries PalmMaterial() on every frame of every pre-existing row.
    public float gripFrictionScale = 3.0f;
    // Negative means "use the serialized field above".
    public static float GripFrictionScaleOverride = -1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ReadGripFrictionSwitch()
    {
        string[] args = LauncherArgs.Args();
        for (int i = 0; i + 1 < args.Length; i++)
        {
            if (!ArgsAudit.Hit(args[i], "-poseHandGripMu"))
                continue;
            float parsed;
            if (float.TryParse(args[i + 1], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out parsed)
                && parsed >= 0f && parsed <= 100f)
                GripFrictionScaleOverride = parsed;
        }
    }

    private PhysicsMaterial2D gripMaterial;

    public PhysicsMaterial2D GripContactMaterial()
    {
        if (gripMaterial == null)
        {
            gripMaterial = new PhysicsMaterial2D("HumanGrip");
            float scale = GripFrictionScaleOverride >= 0f
                ? GripFrictionScaleOverride : gripFrictionScale;
            gripMaterial.friction = Mathf.Max(0f, handFriction * scale);
            gripMaterial.bounciness = 0f;
        }

        return gripMaterial;
    }

    // The grip's own handle: engaged, the palm's collider carries the grip
    // material; released, the same PalmMaterial instance the build assigned.
    public void SetHandGrip(Collider2D handCol, bool engaged)
    {
        if (handCol == null)
            return;
        handCol.sharedMaterial = engaged ? GripContactMaterial() : PalmMaterial();
    }

    private void CreateLeg(string sideName, float direction, HumanSegment pelvis,
                           int thighOrder, int shinOrder, int footOrder,
                           Color thighColor, Color shinColor, Color footColor)
    {
        float hipX = direction * hipHalfSpacing * LateralProjection;
        // CYCLE 92 (rig fix, owner 28.09: "the thighs must attach to the torso"): the
        // stride stance is a HIP SWING about the rest pivot, not a translation. The
        // pivot (hipX, hipRootY) stays inside the torso's lower silhouette at every
        // stride: the bone bar and the hip markers never leave it, the thigh top always
        // starts under the torso, and the whole straight leg rotates by the angle that
        // puts the ANKLE on the owner's split-stance offset (front +0.076 H, back
        // -0.110 H; the same ankle positions the c91 translation delivered). The foot
        // plate stays level, so the shin's lean over it reads as the reference's ankle.
        // Zero stride => the same build, bit for bit.
        float strideTarget = direction > 0f ? stanceStrideFront : -stanceStrideBack;
        float legLength = thighSize.y + shinSize.y;
        float strideAngle = Mathf.Abs(strideTarget) > 1e-6f
            ? Mathf.Asin(Mathf.Clamp(strideTarget / legLength, -0.9f, 0.9f))
            : 0f;
        float strideCos = Mathf.Cos(strideAngle);
        float strideSin = Mathf.Sin(strideAngle);
        // Visual-only side swap for legs: keep physics anchors/segments in place,
        // only mirror which leg is drawn closer to the camera.
        float visualShift = -direction * visualSideOffset;
        // hipY is in pelvis local (hip anchor). Leg segments are children of the root,
        // so they are placed from hipRootY = −0.25, as before the torso split.
        float hipY = -pelvisSize.y * 0.5f;
        float hipRootY = pelvis.transform.localPosition.y + hipY;
        // Swung placement: a rest-frame offset (dx, dy) from the pivot, rotated by the
        // stride swing (identity when the stride is zero).
        float strideRotX(float dx, float dy) => dx * strideCos - dy * strideSin;
        float strideRotY(float dx, float dy) => dx * strideSin + dy * strideCos;
        Quaternion strideRot = Quaternion.Euler(0f, 0f, strideAngle * Mathf.Rad2Deg);

        HumanSegment thigh = CreateSegment(sideName + "Thigh", thighSize, THIGH_MASS_FRACTION, thighColor, thighOrder,
            HumanVisualShape.Box, null, ForeshortenMassScale());
        thigh.transform.localPosition = new Vector3(
            hipX + strideRotX(0f, -thighSize.y * 0.5f),
            hipRootY + strideRotY(0f, -thighSize.y * 0.5f), 0);
        thigh.transform.localRotation = strideRot;
        thigh.SetVisualOffset(visualShift);
        thigh.ConnectTo(pelvis,
            new Vector2(0, thighSize.y * 0.5f),
            new Vector2(hipX, hipY),
            hipLimitMin, hipLimitMax);
        AddFriction(thigh, hipFriction);
        AddMuscles(thigh, hipMuscleTorque);

        HumanSegment shin = CreateSegment(sideName + "Shin", shinSize, SHIN_MASS_FRACTION, shinColor, shinOrder);
        shin.transform.localPosition = new Vector3(
            hipX + strideRotX(0f, -thighSize.y - shinSize.y * 0.5f),
            hipRootY + strideRotY(0f, -thighSize.y - shinSize.y * 0.5f), 0);
        shin.transform.localRotation = strideRot;
        shin.SetVisualOffset(visualShift);
        // 0…+120, not −120…0. jointAngle = parent angle minus own, so
        // a positive knee angle takes the shin bottom backward — human
        // flexion when looking right. The former range allowed only
        // the reverse, ostrich flexion: the knee went past the thigh–ankle line.
        shin.ConnectTo(thigh,
            new Vector2(0, shinSize.y * 0.5f),
            new Vector2(0, -thighSize.y * 0.5f),
            kneeLimitMin, kneeLimitMax);
        AddFriction(shin, kneeFriction);
        AddMuscles(shin, kneeMuscleTorque);

        // Heel + toe of the same total length. The ankle stays ankleHeelOffset
        // behind the heel back (6.5 cm); the MTP sits at the ball. Mass
        // splits with length so the foot fraction stays 0.0137.
        float heelShare = Mathf.Clamp(footHeelShare, 0.4f, 0.85f);
        Vector2 heelSize = new Vector2(footSize.x * heelShare, footSize.y);
        Vector2 toeSize = new Vector2(footSize.x - heelSize.x, footSize.y);
        float heelHalf = heelSize.x * 0.5f;
        float toeHalf = toeSize.x * 0.5f;
        // The ankle lands on the swung leg's tip; the plate stays level (sole flat),
        // so the anchor's local x is the same value at any stride.
        float ankleX = hipX + legLength * strideSin;
        float ankleY = hipRootY - legLength * strideCos;
        float heelCenterX = ankleX - ankleHeelOffset + heelHalf;
        float ankleAnchorX = ankleX - heelCenterX;
        // Heel centre sits on the shin tip so the ankle hinge is mid-plate,
        // not on the top edge. Sole is half a foot-height higher than the
        // old hang-below-shin layout.
        float footY = ankleY;

        HumanSegment heel = CreateSegment(sideName + "Foot", heelSize,
            FOOT_MASS_FRACTION * heelShare, footColor, footOrder);
        if (heel.collider != null)
            heel.collider.sharedMaterial = SoleMaterial();
        heel.transform.localPosition = new Vector3(heelCenterX, footY, 0);
        heel.SetVisualOffset(visualShift);
        heel.ConnectTo(shin,
            new Vector2(ankleAnchorX, 0f),
            new Vector2(0, -shinSize.y * 0.5f),
            -45f, 45f);
        AddFriction(heel, ankleFriction);
        AddMuscles(heel, ankleFlexorTorque, ankleExtensorTorque);

        Color toeColor = new Color(footColor.r * 0.5f, footColor.g * 0.5f,
            footColor.b * 0.5f, footColor.a);
        float toeCenterX = heelCenterX + heelHalf + toeHalf;
        HumanSegment toe = CreateSegment(sideName + "Toe", toeSize,
            FOOT_MASS_FRACTION * (1f - heelShare), toeColor, footOrder);
        if (toe.collider != null)
            toe.collider.sharedMaterial = SoleMaterial();
        toe.transform.localPosition = new Vector3(toeCenterX, footY, 0);
        toe.SetVisualOffset(visualShift);
        // MTP at the ball: mid-height of the abutting heel/toe edges.
        // Zero jointAngle is a flat plate. Plus takes the toe down relative
        // to the heel (heel can rise, toe stays on the slab). Minus is
        // clearance in the air.
        toe.ConnectTo(heel,
            new Vector2(-toeHalf, 0f),
            new Vector2(heelHalf, 0f),
            toeLimitMin, toeLimitMax);
        AddFriction(toe, toeFriction);
        AddMuscles(toe, toeMuscleTorque);
    }

    private void SetupBalanceController()
    {
        BalanceController bc = gameObject.AddComponent<BalanceController>();
        balanceController = bc;

        Transform rightThigh = transform.Find("RightLegThigh");
        Transform rightShin = transform.Find("RightLegShin");
        Transform rightFoot = transform.Find("RightLegFoot");
        Transform rightToe = transform.Find("RightLegToe");
        Transform leftThigh = transform.Find("LeftLegThigh");
        Transform leftShin = transform.Find("LeftLegShin");
        Transform leftFoot = transform.Find("LeftLegFoot");
        Transform leftToe = transform.Find("LeftLegToe");

        if (rightThigh && rightShin && leftThigh && leftShin && rightFoot && leftFoot)
        {
            Muscle[] rHip = rightThigh.GetComponents<Muscle>();
            Muscle[] rKnee = rightShin.GetComponents<Muscle>();
            Muscle[] rFoot = rightFoot.GetComponents<Muscle>();
            bc.rightHipFlexor = System.Array.Find(rHip, m => m.direction == 1f);
            bc.rightHipExtensor = System.Array.Find(rHip, m => m.direction == -1f);
            bc.rightKneeFlexor = System.Array.Find(rKnee, m => m.direction == 1f);
            bc.rightKneeExtensor = System.Array.Find(rKnee, m => m.direction == -1f);
            bc.rightAnkleFlexor = System.Array.Find(rFoot, m => m.direction == 1f);
            bc.rightAnkleExtensor = System.Array.Find(rFoot, m => m.direction == -1f);

            Muscle[] lHip = leftThigh.GetComponents<Muscle>();
            Muscle[] lKnee = leftShin.GetComponents<Muscle>();
            Muscle[] lFoot = leftFoot.GetComponents<Muscle>();
            bc.leftHipFlexor = System.Array.Find(lHip, m => m.direction == 1f);
            bc.leftHipExtensor = System.Array.Find(lHip, m => m.direction == -1f);
            bc.leftKneeFlexor = System.Array.Find(lKnee, m => m.direction == 1f);
            bc.leftKneeExtensor = System.Array.Find(lKnee, m => m.direction == -1f);
            bc.leftAnkleFlexor = System.Array.Find(lFoot, m => m.direction == 1f);
            bc.leftAnkleExtensor = System.Array.Find(lFoot, m => m.direction == -1f);
        }

        BindMusclePair(rightToe, out bc.rightToeFlexor, out bc.rightToeExtensor);
        BindMusclePair(leftToe, out bc.leftToeFlexor, out bc.leftToeExtensor);

        Transform torsoTransform = transform.Find("Torso");
        if (torsoTransform)
        {
            Muscle[] lumbarMuscles = torsoTransform.GetComponents<Muscle>();
            bc.lumbarFlexor = System.Array.Find(lumbarMuscles, m => m.direction == 1f);
            bc.lumbarExtensor = System.Array.Find(lumbarMuscles, m => m.direction == -1f);
        }

        Transform neckTransform = transform.Find("Neck");
        Transform headTransform = transform.Find("Head");
        if (neckTransform)
        {
            Muscle[] neckMuscles = neckTransform.GetComponents<Muscle>();
            bc.neckFlexor = System.Array.Find(neckMuscles, m => m.direction == 1f);
            bc.neckExtensor = System.Array.Find(neckMuscles, m => m.direction == -1f);
        }
        if (headTransform)
        {
            Muscle[] headMuscles = headTransform.GetComponents<Muscle>();
            bc.headFlexor = System.Array.Find(headMuscles, m => m.direction == 1f);
            bc.headExtensor = System.Array.Find(headMuscles, m => m.direction == -1f);
        }

        BindArmMuscles("RightArm",
            out bc.rightShoulderFlexor, out bc.rightShoulderExtensor,
            out bc.rightElbowFlexor, out bc.rightElbowExtensor,
            out bc.rightWristFlexor, out bc.rightWristExtensor);
        BindArmMuscles("LeftArm",
            out bc.leftShoulderFlexor, out bc.leftShoulderExtensor,
            out bc.leftElbowFlexor, out bc.leftElbowExtensor,
            out bc.leftWristFlexor, out bc.leftWristExtensor);
    }

    private void BindArmMuscles(string sideName,
                                out Muscle shoulderFlex, out Muscle shoulderExt,
                                out Muscle elbowFlex, out Muscle elbowExt,
                                out Muscle wristFlex, out Muscle wristExt)
    {
        BindMusclePair(transform.Find(sideName + "Upper"), out shoulderFlex, out shoulderExt);
        BindMusclePair(transform.Find(sideName + "Lower"), out elbowFlex, out elbowExt);
        BindMusclePair(transform.Find(sideName + "Hand"), out wristFlex, out wristExt);
    }

    private static void BindMusclePair(Transform t, out Muscle flexor, out Muscle extensor)
    {
        flexor = null;
        extensor = null;
        if (t == null) return;
        Muscle[] muscles = t.GetComponents<Muscle>();
        flexor = System.Array.Find(muscles, m => m.direction == 1f);
        extensor = System.Array.Find(muscles, m => m.direction == -1f);
    }

    private void DisableCollisionsBetweenSegments()
    {
        Collider2D[] allColliders = GetComponentsInChildren<Collider2D>();
        for (int i = 0; i < allColliders.Length; i++)
        {
            for (int j = i + 1; j < allColliders.Length; j++)
            {
                Physics2D.IgnoreCollision(allColliders[i], allColliders[j], true);
            }
        }
    }
}
