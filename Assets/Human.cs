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
        CreateArm("RightArm", -1, torso,
            RIGHT_ARM_UPPER_ORDER, RIGHT_ARM_LOWER_ORDER, RIGHT_ARM_HAND_ORDER,
            RightUpperArmColor, RightLowerArmColor, RightHandColor);
        CreateArm("LeftArm", 1, torso,
            LEFT_ARM_UPPER_ORDER, LEFT_ARM_LOWER_ORDER, LEFT_ARM_HAND_ORDER,
            LeftUpperArmColor, LeftLowerArmColor, LeftHandColor);

        CreateLeg("RightLeg", 1, pelvis,
            RIGHT_LEG_THIGH_ORDER, RIGHT_LEG_SHIN_ORDER, RIGHT_LEG_FOOT_ORDER,
            RightThighColor, RightShinColor, RightFootColor);
        CreateLeg("LeftLeg", -1, pelvis,
            LEFT_LEG_THIGH_ORDER, LEFT_LEG_SHIN_ORDER, LEFT_LEG_FOOT_ORDER,
            LeftThighColor, LeftShinColor, LeftFootColor);

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

        // Friction, then muscles — one FixedUpdate, order is explicit.
        ActuatorDriver actuators = gameObject.AddComponent<ActuatorDriver>();
        actuators.RebuildCache();

        // Arms. Owns the shoulder/elbow activations only, and rewrites them
        // after BalanceController (order 0) and before the actuator pass above
        // applies them (order 50): reciprocal swing on the walk, press on the
        // fall. Everything else on the arm stays with the balance controller.
        ArmDrive armDrive = gameObject.AddComponent<ArmDrive>();
        armDrive.Bind();

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
        string albedoKey = null)
    {
        GameObject segmentObject = new GameObject(name);
        HumanSegment segment = segmentObject.AddComponent<HumanSegment>();
        float segmentMass = totalMass * massFraction;
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

    private void CreateArm(string sideName, float direction, HumanSegment torso,
                           int upperOrder, int lowerOrder, int handOrder,
                           Color upperColor, Color lowerColor, Color handColor)
    {
        float shoulderX = direction * shoulderHalfSpacing * LateralProjection;
        float visualShift = direction * visualSideOffset;
        // Drop from chest top, not a fraction of height: after the torso split
        // absolute shoulder height must stay +0.125 from the root.
        // shoulderY is in chest local (joint anchor); shoulderRootY is from the root
        // (segment placement — they are children of Human, not Torso).
        float shoulderY = torsoSize.y * 0.5f - shoulderDropFromNeck;
        float shoulderRootY = torso.transform.localPosition.y + shoulderY;

        HumanSegment upper = CreateSegment(sideName + "Upper", upperArmSize, UPPER_ARM_MASS_FRACTION, upperColor, upperOrder);
        upper.transform.localPosition = new Vector3(shoulderX, shoulderRootY - upperArmSize.y * 0.5f, 0);
        upper.SetVisualOffset(visualShift);
        upper.ConnectTo(torso,
            new Vector2(0, upperArmSize.y * 0.5f),
            new Vector2(shoulderX, shoulderY),
            -90f, 90f);
        AddFriction(upper, shoulderFriction);
        AddMuscles(upper, shoulderMuscleTorque);

        HumanSegment lower = CreateSegment(sideName + "Lower", lowerArmSize, LOWER_ARM_MASS_FRACTION, lowerColor, lowerOrder);
        lower.transform.localPosition = new Vector3(shoulderX, shoulderRootY - upperArmSize.y - lowerArmSize.y * 0.5f, 0);
        lower.SetVisualOffset(visualShift);
        lower.ConnectTo(upper,
            new Vector2(0, lowerArmSize.y * 0.5f),
            new Vector2(0, -upperArmSize.y * 0.5f),
            -140f, 0f);
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

    private void CreateLeg(string sideName, float direction, HumanSegment pelvis,
                           int thighOrder, int shinOrder, int footOrder,
                           Color thighColor, Color shinColor, Color footColor)
    {
        float hipX = direction * hipHalfSpacing * LateralProjection;
        // Visual-only side swap for legs: keep physics anchors/segments in place,
        // only mirror which leg is drawn closer to the camera.
        float visualShift = -direction * visualSideOffset;
        // hipY is in pelvis local (hip anchor). Leg segments are children of the root,
        // so they are placed from hipRootY = −0.25, as before the torso split.
        float hipY = -pelvisSize.y * 0.5f;
        float hipRootY = pelvis.transform.localPosition.y + hipY;

        HumanSegment thigh = CreateSegment(sideName + "Thigh", thighSize, THIGH_MASS_FRACTION, thighColor, thighOrder);
        thigh.transform.localPosition = new Vector3(hipX, hipRootY - thighSize.y * 0.5f, 0);
        thigh.SetVisualOffset(visualShift);
        thigh.ConnectTo(pelvis,
            new Vector2(0, thighSize.y * 0.5f),
            new Vector2(hipX, hipY),
            hipLimitMin, hipLimitMax);
        AddFriction(thigh, hipFriction);
        AddMuscles(thigh, hipMuscleTorque);

        HumanSegment shin = CreateSegment(sideName + "Shin", shinSize, SHIN_MASS_FRACTION, shinColor, shinOrder);
        shin.transform.localPosition = new Vector3(hipX, hipRootY - thighSize.y - shinSize.y * 0.5f, 0);
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
        float heelCenterX = hipX - ankleHeelOffset + heelHalf;
        float ankleAnchorX = hipX - heelCenterX;
        // Heel centre sits on the shin tip so the ankle hinge is mid-plate,
        // not on the top edge. Sole is half a foot-height higher than the
        // old hang-below-shin layout.
        float footY = hipRootY - thighSize.y - shinSize.y;

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
