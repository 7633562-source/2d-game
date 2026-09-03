using UnityEngine;

// Segmental sagittal dog. Sibling of Human, not a subclass.
// Reuses HumanSegment, Muscle, JointFriction. Control is DogStanceController.
public class Dog : MonoBehaviour
{
    [Header("Mass")]
    public float totalMass = 25f;
    public bool normalizeMass = true;

    [Header("Picture")]
    public float visualSideOffset = 0.04f;
    public float jointMarkerDiameter = 0.025f;

    // The pre-build default (StandingRootY) and the fields must never
    // drift apart: the stand asks for a spawn height before the body
    // exists, and a stale constant buries the dog or hangs it in the air.
    private const float DefaultChestH = 0.14f;
    private const float DefaultUpperLen = 0.17f;
    private const float DefaultLowerLen = 0.17f;
    private const float DefaultPawH = 0.05f;
    // Planted leftover (c39 / DOG-2 c53): rake −25 / +25. Columns 0/0
    // unload the rear in ~0.15 s (c41, c52). The rake is still a sit;
    // StandingRootY must use the same numbers as the fields.
    private const float DefaultHipAngle = -25f;
    private const float DefaultShoulderAngle = 25f;
    private const float DefaultKneeAngle = 0f;
    private const float DefaultElbowAngle = 0f;

    [Header("Segment sizes (collider, m)")]
    public Vector2 chestSize = new Vector2(0.36f, DefaultChestH);
    public Vector2 pelvisSize = new Vector2(0.22f, 0.13f);
    public Vector2 neckSize = new Vector2(0.10f, 0.08f);
    public Vector2 headSize = new Vector2(0.14f, 0.10f);
    public Vector2 jawSize = new Vector2(0.10f, 0.032f);
    public Vector2 tailSize = new Vector2(0.20f, 0.05f);
    public Vector2 frontUpperSize = new Vector2(0.07f, DefaultUpperLen);
    public Vector2 frontLowerSize = new Vector2(0.06f, DefaultLowerLen);
    public Vector2 rearThighSize = new Vector2(0.08f, DefaultUpperLen);
    public Vector2 rearShinSize = new Vector2(0.06f, DefaultLowerLen);
    public Vector2 pawSize = new Vector2(0.10f, DefaultPawH);

    [Header("Sagittal layout (m)")]
    public float shoulderLocalX = 0.10f;
    public float hipLocalX = -0.04f;
    public float pawHeelOffset = 0.025f;

    // Build-time joint angles; DogStanceController holds the same pose.
    // Pair each change with -hipBase / -shoulderBase or the first step
    // drags the limb. Do not return to 0/0 without a new mechanism.
    [Header("Build pose (deg)")]
    public float spawnHipAngle = DefaultHipAngle;
    public float spawnShoulderAngle = DefaultShoulderAngle;
    public float spawnKneeAngle = DefaultKneeAngle;
    public float spawnElbowAngle = DefaultElbowAngle;
    public int tailSegmentCount = 5;
    public float tailSpringStiffness = 2f;
    public float tailSpringMaxTorque = 2f;

    [Header("Joint friction K")]
    public float lumbarFriction = 8f;
    public float lumbarFrictionMaxTorque = 20f;
    public float neckFriction = 0.01f;
    public float jawFriction = 0.01f;
    public float tailFriction = 0.4f;
    public float hipFriction = 3f;
    public float kneeFriction = 3f;
    public float shoulderFriction = 3f;
    public float elbowFriction = 3f;
    public float pawFriction = 1f;

    [Header("Muscle torque ceilings (N·m)")]
    public float lumbarMuscleTorque = 80f;
    public float neckMuscleTorque = 8f;
    public float jawMuscleTorque = 2.5f;
    public float tailMuscleTorque = 3f;
    public float hipMuscleTorque = 40f;
    public float kneeMuscleTorque = 45f;
    public float shoulderMuscleTorque = 35f;
    public float elbowMuscleTorque = 40f;
    // Geometry at 25 kg, toe ~0.07 m, heel ~0.03 m.
    public float pawExtensorTorque = 17f;
    public float pawFlexorTorque = 7f;

    public VestibularSystem vestibularSystem;
    public DogStanceController stance;
    public HumanSegment chestSegment;
    public HumanSegment pelvisSegment;
    public HumanSegment headSegment;
    public HumanSegment jawSegment;
    public BoxCollider2D jawCollider;

    // Painted parts carry their own fur; tint only dims the far side.
    private static readonly Color Near = Color.white;
    private static readonly Color Far = DimFar(Color.white);

    private static Color DimFar(Color c)
    {
        return new Color(c.r * 0.52f, c.g * 0.52f, c.b * 0.52f, 1f);
    }

    // Pre-build fallback for callers that need a height before the body
    // exists. Derived from the same defaults as the fields, so a changed
    // spawn angle or segment length cannot silently desync it from
    // StandingRootOffset. Prefer the instance version once a Dog exists.
    public static float StandingRootY(float groundY = -2f)
    {
        float upperW = -DefaultShoulderAngle;
        float lowerW = upperW - DefaultElbowAngle;
        float dropF = BoneDrop(DefaultUpperLen, upperW)
                      + BoneDrop(DefaultLowerLen, lowerW) + DefaultPawH;
        return groundY + DefaultChestH * 0.5f + dropF;
    }

    public float StandingRootOffset(float groundY = -2f)
    {
        LimbWorldAngles(out _, out _, out float upperW, out float lowerW);
        float dropF = BoneDrop(frontUpperSize.y, upperW) + BoneDrop(frontLowerSize.y, lowerW) + pawSize.y;
        return groundY + chestSize.y * 0.5f + dropF;
    }

    private void LimbWorldAngles(out float thighW, out float shinW, out float upperW, out float lowerW)
    {
        // jointAngle = parentRot − childRot. Trunk at 0.
        thighW = -spawnHipAngle;
        shinW = thighW - spawnKneeAngle;
        upperW = -spawnShoulderAngle;
        lowerW = upperW - spawnElbowAngle;
    }

    private static float BoneDrop(float length, float worldDeg)
    {
        return length * Mathf.Cos(worldDeg * Mathf.Deg2Rad);
    }

    private static Vector2 RotateZ(float deg, Vector2 v)
    {
        float r = deg * Mathf.Deg2Rad;
        float c = Mathf.Cos(r);
        float s = Mathf.Sin(r);
        return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
    }

    private static void PlaceRotated(HumanSegment seg, Vector2 jointRoot, float worldDeg, float length)
    {
        Vector2 center = jointRoot + RotateZ(worldDeg, new Vector2(0f, -length * 0.5f));
        seg.transform.localPosition = new Vector3(center.x, center.y, 0f);
        seg.transform.localRotation = Quaternion.Euler(0f, 0f, worldDeg);
        if (seg.rb != null)
            seg.rb.rotation = worldDeg;
    }

    public Vector2 GetFollowPoint()
    {
        if (stance != null && stance.hasCom)
            return stance.comPosition;
        if (chestSegment != null && chestSegment.rb != null)
            return chestSegment.rb.worldCenterOfMass;
        return transform.position;
    }

    public void BuildDog()
    {
        foreach (Transform child in transform)
            Destroy(child.gameObject);

        DogStanceController oldStance = GetComponent<DogStanceController>();
        if (oldStance != null)
            Destroy(oldStance);
        VestibularSystem oldVest = GetComponent<VestibularSystem>();
        if (oldVest != null)
            Destroy(oldVest);
        ActuatorDriver oldAct = GetComponent<ActuatorDriver>();
        if (oldAct != null)
            Destroy(oldAct);
        JawStrike oldJaw = GetComponent<JawStrike>();
        if (oldJaw != null)
            Destroy(oldJaw);
        Damageable oldLife = GetComponent<Damageable>();
        if (oldLife != null)
            Destroy(oldLife);
        OrganismVoice oldVoice = GetComponent<OrganismVoice>();
        if (oldVoice != null)
            Destroy(oldVoice);

        float pelvisCenterX = -chestSize.x * 0.5f - pelvisSize.x * 0.5f;
        float pelvisCenterY = (pelvisSize.y - chestSize.y) * 0.5f;

        HumanSegment pelvis = CreateSegment("Pelvis", pelvisSize, 0.140f, Near, 0, albedoKey: ArtLibrary.DogPelvis);
        pelvis.transform.localPosition = new Vector3(pelvisCenterX, pelvisCenterY, 0f);
        pelvisSegment = pelvis;

        HumanSegment chest = CreateSegment("Chest", chestSize, 0.220f, Near, 0, albedoKey: ArtLibrary.DogChest);
        chest.transform.localPosition = Vector3.zero;
        chest.ConnectTo(
            pelvis,
            new Vector2(-chestSize.x * 0.5f, 0f),
            new Vector2(pelvisSize.x * 0.5f, 0f),
            -20f, 20f);
        AddFriction(chest, lumbarFriction, lumbarFrictionMaxTorque);
        AddMuscles(chest, lumbarMuscleTorque);
        chestSegment = chest;

        HumanSegment neck = CreateSegment("Neck", neckSize, 0.030f, Near, 1, albedoKey: ArtLibrary.DogNeck);
        neck.transform.localPosition = new Vector3(chestSize.x * 0.5f + neckSize.x * 0.5f, 0f, 0f);
        neck.ConnectTo(
            chest,
            new Vector2(-neckSize.x * 0.5f, 0f),
            new Vector2(chestSize.x * 0.5f, 0f),
            -40f, 40f);
        AddFriction(neck, neckFriction);
        AddMuscles(neck, neckMuscleTorque);

        HumanSegment head = CreateSegment(
            "Head", headSize, 0.070f, Near, 2, HumanVisualShape.Ellipse, ArtLibrary.DogHead);
        head.transform.localPosition = new Vector3(
            chestSize.x * 0.5f + neckSize.x + headSize.x * 0.5f, 0f, 0f);
        head.ConnectTo(
            neck,
            new Vector2(-headSize.x * 0.5f, 0f),
            new Vector2(neckSize.x * 0.5f, 0f),
            -30f, 30f);
        AddFriction(head, neckFriction);
        AddMuscles(head, neckMuscleTorque);
        headSegment = head;
        AttachJaw(head);

        CreateTail(pelvis, pelvisCenterX, pelvisCenterY);

        CreateFrontLeg("FrontRight", 1f, chest, 6, 7, 8, Near, Near, Near);
        CreateFrontLeg("FrontLeft", -1f, chest, -3, -2, -1, Far, Far, Far);
        CreateRearLeg("RearRight", 1f, pelvis, 3, 4, 5, Near, Near, Near);
        CreateRearLeg("RearLeft", -1f, pelvis, -6, -5, -4, Far, Far, Far);

        if (normalizeMass)
            NormalizeMass();

        DisableCollisionsBetweenSegments();

        vestibularSystem = gameObject.AddComponent<VestibularSystem>();
        stance = gameObject.AddComponent<DogStanceController>();
        AttachBite();
        stance.Bind(this);

        ActuatorDriver actuators = gameObject.AddComponent<ActuatorDriver>();
        actuators.RebuildCache();
    }

    // Lower jaw. Hinge at the TMJ (caudal-ventral on the head). Closed
    // is jointAngle 0; plus drops the teeth (clockwise vs the head).
    // Trigger: the jaw must not shove the world. Hits go through JawStrike.
    private void AttachJaw(HumanSegment head)
    {
        Vector2 tmj = new Vector2(-headSize.x * 0.18f, -headSize.y * 0.38f);
        Vector2 center = (Vector2)head.transform.localPosition + tmj + new Vector2(jawSize.x * 0.5f, 0f);
        HumanSegment jaw = CreateSegment("Jaw", jawSize, 0.008f, Near, 3, albedoKey: ArtLibrary.DogJaw);
        jaw.transform.localPosition = new Vector3(center.x, center.y, 0f);
        jaw.ConnectTo(
            head,
            new Vector2(-jawSize.x * 0.5f, 0f),
            tmj,
            0f, 40f);
        AddFriction(jaw, jawFriction);
        AddMuscles(jaw, jawMuscleTorque);
        if (jaw.collider != null)
            jaw.collider.isTrigger = true;
        jawSegment = jaw;
        jawCollider = jaw.collider;
    }

    private void AttachBite()
    {
        Damageable life = gameObject.AddComponent<Damageable>();
        life.maxHealth = 16f;
        life.health = life.maxHealth;

        gameObject.AddComponent<OrganismVoice>();

        JawStrike strike = gameObject.AddComponent<JawStrike>();
        strike.Bind(jawCollider, stance);
    }

    private void CreateFrontLeg(
        string sideName,
        float sideSign,
        HumanSegment chest,
        int upperOrder,
        int lowerOrder,
        int pawOrder,
        Color upperColor,
        Color lowerColor,
        Color pawColor)
    {
        float visualShift = sideSign * visualSideOffset;
        LimbWorldAngles(out _, out _, out float upperW, out float lowerW);
        Vector2 shoulder = new Vector2(shoulderLocalX, -chestSize.y * 0.5f);
        Vector2 elbow = shoulder + RotateZ(upperW, new Vector2(0f, -frontUpperSize.y));
        Vector2 ankle = elbow + RotateZ(lowerW, new Vector2(0f, -frontLowerSize.y));

        HumanSegment upper = CreateSegment(sideName + "Upper", frontUpperSize, 0.040f, upperColor, upperOrder, albedoKey: ArtLibrary.DogFrontUpper);
        PlaceRotated(upper, shoulder, upperW, frontUpperSize.y);
        upper.SetVisualOffset(visualShift);
        upper.ConnectTo(
            chest,
            new Vector2(0f, frontUpperSize.y * 0.5f),
            new Vector2(shoulderLocalX, -chestSize.y * 0.5f),
            -80f, 80f);
        AddFriction(upper, shoulderFriction);
        AddMuscles(upper, shoulderMuscleTorque);

        HumanSegment lower = CreateSegment(sideName + "Lower", frontLowerSize, 0.028f, lowerColor, lowerOrder, albedoKey: ArtLibrary.DogFrontLower);
        PlaceRotated(lower, elbow, lowerW, frontLowerSize.y);
        lower.SetVisualOffset(visualShift);
        lower.ConnectTo(
            upper,
            new Vector2(0f, frontLowerSize.y * 0.5f),
            new Vector2(0f, -frontUpperSize.y * 0.5f),
            0f, 120f);
        AddFriction(lower, elbowFriction);
        AddMuscles(lower, elbowMuscleTorque);

        PlacePaw(sideName + "Paw", ankle, lower, pawOrder, pawColor, visualShift, 0.012f);
    }

    private void CreateRearLeg(
        string sideName,
        float sideSign,
        HumanSegment pelvis,
        int thighOrder,
        int shinOrder,
        int pawOrder,
        Color thighColor,
        Color shinColor,
        Color pawColor)
    {
        float visualShift = sideSign * visualSideOffset;
        LimbWorldAngles(out float thighW, out float shinW, out float upperW, out float lowerW);
        float dropF = BoneDrop(frontUpperSize.y, upperW) + BoneDrop(frontLowerSize.y, lowerW) + pawSize.y;
        float dropR = BoneDrop(rearThighSize.y, thighW) + BoneDrop(rearShinSize.y, shinW) + pawSize.y;
        float soleY = -chestSize.y * 0.5f - dropF;
        float hipRootX = pelvis.transform.localPosition.x + hipLocalX;
        float hipRootY = soleY + dropR;
        float hipInPelvisY = hipRootY - pelvis.transform.localPosition.y;
        Vector2 hip = new Vector2(hipRootX, hipRootY);
        Vector2 knee = hip + RotateZ(thighW, new Vector2(0f, -rearThighSize.y));
        Vector2 ankle = knee + RotateZ(shinW, new Vector2(0f, -rearShinSize.y));

        HumanSegment thigh = CreateSegment(sideName + "Thigh", rearThighSize, 0.080f, thighColor, thighOrder, albedoKey: ArtLibrary.DogThigh);
        PlaceRotated(thigh, hip, thighW, rearThighSize.y);
        thigh.SetVisualOffset(visualShift);
        thigh.ConnectTo(
            pelvis,
            new Vector2(0f, rearThighSize.y * 0.5f),
            new Vector2(hipLocalX, hipInPelvisY),
            -80f, 80f);
        AddFriction(thigh, hipFriction);
        AddMuscles(thigh, hipMuscleTorque);

        HumanSegment shin = CreateSegment(sideName + "Shin", rearShinSize, 0.040f, shinColor, shinOrder, albedoKey: ArtLibrary.DogShin);
        PlaceRotated(shin, knee, shinW, rearShinSize.y);
        shin.SetVisualOffset(visualShift);
        shin.ConnectTo(
            thigh,
            new Vector2(0f, rearShinSize.y * 0.5f),
            new Vector2(0f, -rearThighSize.y * 0.5f),
            0f, 120f);
        AddFriction(shin, kneeFriction);
        AddMuscles(shin, kneeMuscleTorque);

        PlacePaw(sideName + "Paw", ankle, shin, pawOrder, pawColor, visualShift, 0.012f);
    }

    // Five vertebrae, not one rod. Distal boxes taper. The first joint
    // still has antagonist muscles so DogStanceController can hold it;
    // the rest are friction + a rest spring (no CoM loop on the chain).
    private void CreateTail(HumanSegment pelvis, float pelvisCenterX, float pelvisCenterY)
    {
        int count = Mathf.Max(1, tailSegmentCount);
        float segLen = tailSize.x / count;
        float massEach = 0.020f / count;
        HumanSegment parent = pelvis;
        Vector2 tip = new Vector2(pelvisCenterX - pelvisSize.x * 0.5f, pelvisCenterY);

        for (int i = 0; i < count; i++)
        {
            float taper = count == 1 ? 1f : Mathf.Lerp(1f, 0.45f, i / (float)(count - 1));
            Vector2 size = new Vector2(segLen, tailSize.y * taper);
            string name = i == 0 ? "Tail" : "Tail" + (i + 1);
            HumanSegment seg = CreateSegment(name, size, massEach, Near, -1 - i, albedoKey: ArtLibrary.DogTail);
            Vector2 center = tip + new Vector2(-segLen * 0.5f, 0f);
            seg.transform.localPosition = new Vector3(center.x, center.y, 0f);
            if (seg.rb != null)
                seg.rb.rotation = 0f;

            Vector2 parentAnchor = i == 0
                ? new Vector2(-pelvisSize.x * 0.5f, 0f)
                : new Vector2(-parent.size.x * 0.5f, 0f);
            seg.ConnectTo(
                parent,
                new Vector2(segLen * 0.5f, 0f),
                parentAnchor,
                -40f, 40f);
            AddFriction(seg, tailFriction);
            AddTailSpring(seg);
            if (i == 0)
                AddMuscles(seg, tailMuscleTorque);

            parent = seg;
            tip = center + new Vector2(-segLen * 0.5f, 0f);
        }
    }

    private void AddTailSpring(HumanSegment segment)
    {
        if (segment == null || segment.joint == null) return;
        TreeSpring spring = segment.gameObject.AddComponent<TreeSpring>();
        spring.restAngle = 0f;
        spring.stiffness = tailSpringStiffness;
        spring.maxTorque = tailSpringMaxTorque;
        spring.holdTorque = 0f;
        spring.worldStiffness = 0f;
    }

    private void PlacePaw(
        string name,
        Vector2 ankle,
        HumanSegment parent,
        int order,
        Color color,
        float visualShift,
        float massFraction)
    {
        float footHalf = pawSize.x * 0.5f;
        float footCenterX = ankle.x - pawHeelOffset + footHalf;
        float ankleAnchorX = ankle.x - footCenterX;
        float footY = ankle.y - pawSize.y * 0.5f;

        HumanSegment paw = CreateSegment(name, pawSize, massFraction, color, order, albedoKey: ArtLibrary.DogPaw);
        paw.transform.localPosition = new Vector3(footCenterX, footY, 0f);
        paw.transform.localRotation = Quaternion.identity;
        if (paw.rb != null)
            paw.rb.rotation = 0f;
        paw.SetVisualOffset(visualShift);
        paw.ConnectTo(
            parent,
            new Vector2(ankleAnchorX, pawSize.y * 0.5f),
            new Vector2(0f, -parent.size.y * 0.5f),
            -45f, 45f);
        AddFriction(paw, pawFriction);
        AddMuscles(paw, pawFlexorTorque, pawExtensorTorque);
    }

    private HumanSegment CreateSegment(
        string name,
        Vector2 size,
        float massFraction,
        Color color,
        int sortingOrder,
        HumanVisualShape visualShape = HumanVisualShape.Capsule,
        string albedoKey = null)
    {
        GameObject segmentObject = new GameObject(name);
        HumanSegment segment = segmentObject.AddComponent<HumanSegment>();
        float segmentMass = Mathf.Max(0.02f, totalMass * massFraction);
        segment.Initialize(name, size, segmentMass, color, transform, sortingOrder, visualShape, albedoKey);
        segment.jointMarkerDiameter = jointMarkerDiameter;
        return segment;
    }

    private void AddFriction(HumanSegment segment, float damping, float maxTorque = 15f)
    {
        if (segment.joint == null) return;
        JointFriction friction = segment.gameObject.AddComponent<JointFriction>();
        friction.damping = damping;
        friction.maxTorque = maxTorque;
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

    private void NormalizeMass()
    {
        if (totalMass <= 0.01f) return;
        HumanSegment[] segs = GetComponentsInChildren<HumanSegment>();
        float sum = 0f;
        for (int i = 0; i < segs.Length; i++)
        {
            if (segs[i] != null && segs[i].rb != null)
                sum += segs[i].mass;
        }
        if (sum <= 0.01f) return;
        float scale = totalMass / sum;
        for (int i = 0; i < segs.Length; i++)
        {
            HumanSegment s = segs[i];
            if (s == null || s.rb == null) continue;
            float mass = s.mass * scale;
            s.mass = mass;
            s.rb.mass = mass;
        }
    }

    private void DisableCollisionsBetweenSegments()
    {
        Collider2D[] all = GetComponentsInChildren<Collider2D>();
        for (int i = 0; i < all.Length; i++)
        {
            for (int j = i + 1; j < all.Length; j++)
                Physics2D.IgnoreCollision(all[i], all[j], true);
        }
    }
}
