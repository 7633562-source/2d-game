using UnityEngine;

// Small sagittal quadruped. Sibling of Dog, not a subclass.
// Reuses HumanSegment, Muscle, JointFriction. Quiet pose is
// HedgehogStanceController; crawl translation is a body force, not a gait.
public class Hedgehog : MonoBehaviour
{
    [Header("Mass")]
    public float totalMass = 1.2f;
    public bool normalizeMass = true;

    [Header("Picture")]
    public float visualSideOffset = 0.025f;
    public float jointMarkerDiameter = 0.016f;

    // Squat dome: short legs and a flat trunk keep CoM near the paws so
    // the crawl force cannot pitch the body over the toes.
    private const float DefaultChestH = 0.048f;
    private const float DefaultUpperLen = 0.028f;
    private const float DefaultLowerLen = 0.022f;
    private const float DefaultPawH = 0.018f;
    private const float DefaultHipAngle = -32f;
    private const float DefaultShoulderAngle = 32f;
    private const float DefaultKneeAngle = 18f;
    private const float DefaultElbowAngle = 18f;

    [Header("Segment sizes (collider, m)")]
    public Vector2 chestSize = new Vector2(0.20f, DefaultChestH);
    public Vector2 pelvisSize = new Vector2(0.11f, 0.042f);
    public Vector2 neckSize = new Vector2(0.045f, 0.032f);
    public Vector2 headSize = new Vector2(0.07f, 0.042f);
    public Vector2 jawSize = new Vector2(0.055f, 0.018f);
    public Vector2 tailSize = new Vector2(0.035f, 0.018f);
    public Vector2 frontUpperSize = new Vector2(0.028f, DefaultUpperLen);
    public Vector2 frontLowerSize = new Vector2(0.022f, DefaultLowerLen);
    public Vector2 rearThighSize = new Vector2(0.030f, DefaultUpperLen);
    public Vector2 rearShinSize = new Vector2(0.022f, DefaultLowerLen);
    public Vector2 pawSize = new Vector2(0.058f, DefaultPawH);

    [Header("Sagittal layout (m)")]
    public float shoulderLocalX = 0.06f;
    public float hipLocalX = -0.03f;
    public float pawHeelOffset = 0.014f;

    [Header("Build pose (deg)")]
    public float spawnHipAngle = DefaultHipAngle;
    public float spawnShoulderAngle = DefaultShoulderAngle;
    public float spawnKneeAngle = DefaultKneeAngle;
    public float spawnElbowAngle = DefaultElbowAngle;

    [Header("Joint friction K")]
    public float lumbarFriction = 0.6f;
    public float lumbarFrictionMaxTorque = 1.6f;
    public float neckFriction = 0.004f;
    public float jawFriction = 0.004f;
    public float tailFriction = 0.03f;
    public float hipFriction = 0.18f;
    public float kneeFriction = 0.18f;
    public float shoulderFriction = 0.18f;
    public float elbowFriction = 0.18f;
    public float pawFriction = 0.06f;

    [Header("Muscle torque ceilings (N·m)")]
    public float lumbarMuscleTorque = 4f;
    public float neckMuscleTorque = 0.5f;
    public float jawMuscleTorque = 0.25f;
    public float tailMuscleTorque = 0.2f;
    public float hipMuscleTorque = 2.5f;
    public float kneeMuscleTorque = 2.8f;
    public float shoulderMuscleTorque = 2.2f;
    public float elbowMuscleTorque = 2.5f;
    public float pawExtensorTorque = 1.0f;
    public float pawFlexorTorque = 0.4f;

    public VestibularSystem vestibularSystem;
    public HedgehogStanceController stance;
    public HumanSegment chestSegment;
    public HumanSegment pelvisSegment;
    public HumanSegment headSegment;
    public HumanSegment jawSegment;
    public BoxCollider2D jawCollider;

    private static readonly Color PelvisColor = new Color(0.28f, 0.18f, 0.10f, 0.7f);
    private static readonly Color ChestColor = new Color(0.38f, 0.24f, 0.12f, 0.7f);
    private static readonly Color NeckColor = new Color(0.42f, 0.30f, 0.18f, 0.7f);
    private static readonly Color HeadColor = new Color(0.55f, 0.40f, 0.22f, 0.75f);
    private static readonly Color JawColor = new Color(0.72f, 0.52f, 0.32f, 0.75f);
    private static readonly Color TailColor = new Color(0.32f, 0.20f, 0.10f, 0.7f);
    private static readonly Color QuillColor = new Color(0.18f, 0.12f, 0.08f, 0.85f);

    private static readonly Color FrontUpperNear = new Color(0.40f, 0.26f, 0.12f, 0.7f);
    private static readonly Color FrontUpperFar = new Color(0.28f, 0.18f, 0.08f, 0.55f);
    private static readonly Color FrontLowerNear = new Color(0.48f, 0.32f, 0.16f, 0.7f);
    private static readonly Color FrontLowerFar = new Color(0.34f, 0.22f, 0.10f, 0.55f);
    private static readonly Color RearThighNear = new Color(0.36f, 0.22f, 0.10f, 0.7f);
    private static readonly Color RearThighFar = new Color(0.24f, 0.15f, 0.07f, 0.55f);
    private static readonly Color RearShinNear = new Color(0.44f, 0.28f, 0.14f, 0.7f);
    private static readonly Color RearShinFar = new Color(0.30f, 0.19f, 0.09f, 0.55f);
    private static readonly Color PawNear = new Color(0.16f, 0.10f, 0.06f, 0.8f);
    private static readonly Color PawFar = new Color(0.22f, 0.14f, 0.08f, 0.6f);

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

    public static Hedgehog Spawn(Vector2 position, string objectName = "Hedgehog")
    {
        GameObject go = new GameObject(objectName);
        Hedgehog body = go.AddComponent<Hedgehog>();
        go.transform.position = position;
        body.BuildHedgehog();
        FactionStamp.Mark(go, FactionId.Wildlife, SubfactionId.None);
        return body;
    }

    private void LimbWorldAngles(out float thighW, out float shinW, out float upperW, out float lowerW)
    {
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

    public void BuildHedgehog()
    {
        foreach (Transform child in transform)
            Destroy(child.gameObject);

        HedgehogStanceController oldStance = GetComponent<HedgehogStanceController>();
        if (oldStance != null)
            Destroy(oldStance);
        VestibularSystem oldVest = GetComponent<VestibularSystem>();
        if (oldVest != null)
            Destroy(oldVest);
        ActuatorDriver oldAct = GetComponent<ActuatorDriver>();
        if (oldAct != null)
            Destroy(oldAct);
        HedgehogBite oldBite = GetComponent<HedgehogBite>();
        if (oldBite != null)
            Destroy(oldBite);
        Damageable oldLife = GetComponent<Damageable>();
        if (oldLife != null)
            Destroy(oldLife);
        OrganismVoice oldVoice = GetComponent<OrganismVoice>();
        if (oldVoice != null)
            Destroy(oldVoice);

        float pelvisCenterX = -chestSize.x * 0.5f - pelvisSize.x * 0.5f;
        float pelvisCenterY = (pelvisSize.y - chestSize.y) * 0.5f;

        HumanSegment pelvis = CreateSegment("Pelvis", pelvisSize, 0.110f, PelvisColor, 0);
        pelvis.transform.localPosition = new Vector3(pelvisCenterX, pelvisCenterY, 0f);
        pelvisSegment = pelvis;

        HumanSegment chest = CreateSegment("Chest", chestSize, 0.170f, ChestColor, 0);
        chest.transform.localPosition = Vector3.zero;
        chest.ConnectTo(
            pelvis,
            new Vector2(-chestSize.x * 0.5f, 0f),
            new Vector2(pelvisSize.x * 0.5f, 0f),
            -20f, 20f);
        AddFriction(chest, lumbarFriction, lumbarFrictionMaxTorque);
        AddMuscles(chest, lumbarMuscleTorque);
        chestSegment = chest;
        AttachQuills(chest);

        HumanSegment neck = CreateSegment("Neck", neckSize, 0.022f, NeckColor, 1);
        neck.transform.localPosition = new Vector3(chestSize.x * 0.5f + neckSize.x * 0.5f, 0f, 0f);
        neck.ConnectTo(
            chest,
            new Vector2(-neckSize.x * 0.5f, 0f),
            new Vector2(chestSize.x * 0.5f, 0f),
            -40f, 40f);
        AddFriction(neck, neckFriction);
        AddMuscles(neck, neckMuscleTorque);

        HumanSegment head = CreateSegment("Head", headSize, 0.050f, HeadColor, 2);
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

        HumanSegment tail = CreateSegment("Tail", tailSize, 0.012f, TailColor, -1);
        Vector2 tailCenter = new Vector2(
            pelvisCenterX - pelvisSize.x * 0.5f - tailSize.x * 0.5f,
            pelvisCenterY);
        tail.transform.localPosition = new Vector3(tailCenter.x, tailCenter.y, 0f);
        tail.ConnectTo(
            pelvis,
            new Vector2(tailSize.x * 0.5f, 0f),
            new Vector2(-pelvisSize.x * 0.5f, 0f),
            -40f, 40f);
        AddFriction(tail, tailFriction);
        AddMuscles(tail, tailMuscleTorque);

        CreateFrontLeg("FrontRight", 1f, chest, 6, 7, 8, FrontUpperNear, FrontLowerNear, PawNear);
        CreateFrontLeg("FrontLeft", -1f, chest, -3, -2, -1, FrontUpperFar, FrontLowerFar, PawFar);
        CreateRearLeg("RearRight", 1f, pelvis, 3, 4, 5, RearThighNear, RearShinNear, PawNear);
        CreateRearLeg("RearLeft", -1f, pelvis, -6, -5, -4, RearThighFar, RearShinFar, PawFar);

        if (normalizeMass)
            NormalizeMass();
        LowerTrunkCenterOfMass();

        DisableCollisionsBetweenSegments();

        vestibularSystem = gameObject.AddComponent<VestibularSystem>();
        stance = gameObject.AddComponent<HedgehogStanceController>();
        if (GetComponent<HedgehogDrive>() == null)
            gameObject.AddComponent<HedgehogDrive>();
        AttachBite();
        stance.Bind(this);

        ActuatorDriver actuators = gameObject.AddComponent<ActuatorDriver>();
        actuators.RebuildCache();
    }

    private void AttachJaw(HumanSegment head)
    {
        Vector2 tmj = new Vector2(headSize.x * 0.08f, -headSize.y * 0.12f);
        Vector2 center = (Vector2)head.transform.localPosition + tmj + new Vector2(jawSize.x * 0.5f, 0f);
        HumanSegment jaw = CreateSegment("Jaw", jawSize, 0.008f, JawColor, 3);
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
        life.maxHealth = 6f;
        life.health = life.maxHealth;

        gameObject.AddComponent<OrganismVoice>();

        HedgehogBite strike = gameObject.AddComponent<HedgehogBite>();
        strike.Bind(jawCollider, stance);
    }

    // Picture only: spines ride the chest Visual offset, not the rigidbody.
    private void AttachQuills(HumanSegment chest)
    {
        if (chest == null)
            return;
        Vector2 quill = new Vector2(0.010f, 0.032f);
        float[] along = { -0.05f, -0.02f, 0.01f, 0.04f };
        for (int i = 0; i < along.Length; i++)
        {
            GameObject go = new GameObject("Quill_" + i);
            HumanSegment spike = go.AddComponent<HumanSegment>();
            spike.InitializeVisualOnly(
                "Quill_" + i,
                quill,
                QuillColor,
                chest.transform,
                4,
                HumanVisualShape.Capsule);
            spike.transform.localPosition = new Vector3(along[i], chestSize.y * 0.42f, 0f);
            spike.transform.localRotation = Quaternion.Euler(0f, 0f, (i % 2 == 0) ? -12f : 10f);
        }
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

        HumanSegment upper = CreateSegment(sideName + "Upper", frontUpperSize, 0.048f, upperColor, upperOrder);
        PlaceRotated(upper, shoulder, upperW, frontUpperSize.y);
        upper.SetVisualOffset(visualShift);
        upper.ConnectTo(
            chest,
            new Vector2(0f, frontUpperSize.y * 0.5f),
            new Vector2(shoulderLocalX, -chestSize.y * 0.5f),
            -80f, 80f);
        AddFriction(upper, shoulderFriction);
        AddMuscles(upper, shoulderMuscleTorque);

        HumanSegment lower = CreateSegment(sideName + "Lower", frontLowerSize, 0.040f, lowerColor, lowerOrder);
        PlaceRotated(lower, elbow, lowerW, frontLowerSize.y);
        lower.SetVisualOffset(visualShift);
        lower.ConnectTo(
            upper,
            new Vector2(0f, frontLowerSize.y * 0.5f),
            new Vector2(0f, -frontUpperSize.y * 0.5f),
            0f, 120f);
        AddFriction(lower, elbowFriction);
        AddMuscles(lower, elbowMuscleTorque);

        PlacePaw(sideName + "Paw", ankle, lower, pawOrder, pawColor, visualShift, 0.028f);
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

        HumanSegment thigh = CreateSegment(sideName + "Thigh", rearThighSize, 0.075f, thighColor, thighOrder);
        PlaceRotated(thigh, hip, thighW, rearThighSize.y);
        thigh.SetVisualOffset(visualShift);
        thigh.ConnectTo(
            pelvis,
            new Vector2(0f, rearThighSize.y * 0.5f),
            new Vector2(hipLocalX, hipInPelvisY),
            -80f, 80f);
        AddFriction(thigh, hipFriction);
        AddMuscles(thigh, hipMuscleTorque);

        HumanSegment shin = CreateSegment(sideName + "Shin", rearShinSize, 0.048f, shinColor, shinOrder);
        PlaceRotated(shin, knee, shinW, rearShinSize.y);
        shin.SetVisualOffset(visualShift);
        shin.ConnectTo(
            thigh,
            new Vector2(0f, rearShinSize.y * 0.5f),
            new Vector2(0f, -rearThighSize.y * 0.5f),
            0f, 120f);
        AddFriction(shin, kneeFriction);
        AddMuscles(shin, kneeMuscleTorque);

        PlacePaw(sideName + "Paw", ankle, shin, pawOrder, pawColor, visualShift, 0.028f);
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

        HumanSegment paw = CreateSegment(name, pawSize, massFraction, color, order);
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
        int sortingOrder)
    {
        GameObject segmentObject = new GameObject(name);
        HumanSegment segment = segmentObject.AddComponent<HumanSegment>();
        float segmentMass = Mathf.Max(0.01f, totalMass * massFraction);
        segment.Initialize(name, size, segmentMass, color, transform, sortingOrder, HumanVisualShape.Box);
        segment.jointMarkerDiameter = jointMarkerDiameter;
        return segment;
    }

    private void AddFriction(HumanSegment segment, float damping, float maxTorque = 1.2f)
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

    // Geometric CoM of a flat box sits at mid-height. Shift it into the
    // belly so a horizontal crawl force does not lever the trunk over the paws.
    private void LowerTrunkCenterOfMass()
    {
        OffsetComTowardBelly(chestSegment, 0.32f);
        OffsetComTowardBelly(pelvisSegment, 0.32f);
        OffsetComTowardBelly(headSegment, 0.18f);
    }

    private static void OffsetComTowardBelly(HumanSegment segment, float fractionOfHeight)
    {
        if (segment == null || segment.rb == null)
            return;
        segment.rb.centerOfMass = new Vector2(0f, -segment.size.y * fractionOfHeight);
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
