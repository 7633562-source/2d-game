using UnityEngine;

// Segmental sagittal dog. Sibling of Human, not a subclass.
// Reuses HumanSegment, Muscle, JointFriction. Control is DogStanceController.
public class Dog : MonoBehaviour
{
    [Header("Mass")]
    public float totalMass = 25f;
    public bool normalizeMass = true;

    [Header("Picture")]
    // Picture-only sideways shift of a limb box, a depth hint the human
    // never uses (Human.visualSideOffset = 0, Human.cs). At 0.04 m on this
    // 0.36 m chest it slides the limb picture off the torso box and the sky
    // reads through the shoulder and the hip. Picture seam, not physics:
    // the hinge anchors sit at the segment ends, HumanSegment moves only the
    // child Visual (Rigidbody2D and collider stay put), the seam is absent
    // at limb-to-paw where both bones shift the same way, and it does not
    // grow with the load. Depth reads from the dimmed far side instead.
    // -dogSideOffset <f> restores the old value on the same build for an A/B.
    public float visualSideOffset = 0f;

    // Art A/B knob only. Negative means "use the serialized field above".
    public static float SideOffsetOverride = -1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ReadSideOffsetSwitch()
    {
        string[] args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i + 1 < args.Length; i++)
        {
            if (args[i] != "-dogSideOffset")
                continue;
            float parsed;
            if (float.TryParse(args[i + 1], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out parsed)
                && parsed >= 0f && parsed <= 0.5f)
                SideOffsetOverride = parsed;
        }
    }

    private float SideVisualOffset
    {
        get { return SideOffsetOverride >= 0f ? SideOffsetOverride : visualSideOffset; }
    }

    public float jointMarkerDiameter = 0.025f;

    // The pre-build default (StandingRootY) and the fields must never
    // drift apart: the stand asks for a spawn height before the body
    // exists, and a stale constant buries the dog or hangs it in the air.
    private const float DefaultChestH = 0.14f;
    private const float DefaultUpperLen = 0.17f;
    private const float DefaultLowerLen = 0.17f;
    private const float DefaultPawH = 0.05f;
    // Joint pose, not world tilt. jointAngle = parentRot − childRot.
    // Standing inverted-V: hip −12 → thigh world +12 (slightly cranial),
    // knee +24 → shin world −12, paw under the hip. Shoulder −20 with
    // chest at DefaultChestPitch → upper world +12, elbow +24 → forearm
    // world −12, paw under the shoulder. Each bone ~12° from vertical,
    // not the A-rake +25 / −25 / 0 / 0 (straight legs splayed, sploot)
    // and not the leftover −25 / +25 / 0 / 0 (X under the belly).
    // Columns 0/0 still unload the rear (c41, c52). Distal fold is the
    // new mechanism. StandingRootY must use these numbers.
    private const float DefaultHipAngle = -12f;
    private const float DefaultShoulderAngle = -20f;
    private const float DefaultKneeAngle = 24f;
    private const float DefaultElbowAngle = 24f;
    // Must match DogStanceController.chestTargetTilt. Building the chest
    // at 0 while the lumbar loop holds −8 is the 0.25 s sit slam (c23 CSV).
    private const float DefaultChestPitch = -8f;
    // Neck/head SPD holds world 0. Spawning them at chest pitch repeats
    // the slam on the light joints (reaction back into the chest).
    private const float DefaultNeckPitch = 0f;
    private const float DefaultShoulderLocalX = 0.10f;

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
    public float shoulderLocalX = DefaultShoulderLocalX;
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
    public float spawnChestPitch = DefaultChestPitch;
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

    // Contact friction of the pad against the ground (the collider-materials
    // rule, 18.09). Not the joint friction above: this is the pad's
    // sharedMaterial. 0.4 is the former behaviour - a collider with no
    // material takes Unity's default and paired with the dirt's own 0.4;
    // 1.2 matches the human sole, as the rule asks. A contact is a pair,
    // so the value only makes sense together with the ground.
    // CLI: -dogPawFriction <f>.
    [Header("Contact friction (paw / body)")]
    public float pawContactFriction = 1.2f;

    // Contact friction of the body plating - chest, pelvis, neck, head,
    // jaw, tail. The rule asks the body LOWER than the sole: explicit 0.4,
    // the value the old implicit default had. CLI: -dogBodyFriction <f>.
    public float bodyContactFriction = 0.4f;

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

    // ─── THE FOLD (cycle 32): the trunk's own range ───
    // The designer's order (21.09 16:18): the torso must bend FAR more than the
    // brick's ±20 — "she is far more flexible; right now she is like a brick",
    // and that flexibility has to help her get up. The split is the anatomy's
    // own. Units are the hinge's own jointAngle: jointAngle = parentWorld −
    // childWorld (Dog.cs's limb note), so at rest the lumbar reads +8 (chest −8
    // world, pelvis 0) and the neck reads −8 (chest −8, neck 0). Positive =
    // VENTRAL flexion (the chest, resp. the neck, curling toward the belly):
    // that is the deep one in a dog; the dorsal EXTENSION is far smaller.
    //    lumbar (Chest→Pelvis):     flexion +40 / extension −20  (was +20 / −20)
    //    neck (Chest→Neck), head:   flexion +50 / extension −35  (was ±30)
    // Where the flexion ceiling comes from: the two boxes' own first contact,
    // not a taste. The chest's rear-ventral corner sits chestSize.y/2 = 0.07 m
    // below the hinge, the pelvis's ventral face pelvisSize.y/2 = 0.065 m below
    // it, so the corner rides up into the pelvis at acos(0.065/0.07) = 21.8°:
    // past that the fold is the two boxes overlapping (the art's own hinge
    // overlap, a few millimetres at the commanded +39 — DogStanceController's
    // GetUpFoldLumbarDeg), and at +40 the corner is ~1.2 cm in with nothing
    // passing THROUGH anything. The extension side keeps the accepted −20: a
    // dog's dorsal range is small, and every accepted row was measured with it.
    // The ACCEPTED range stays the rig's BUILT default — the stand, the walk
    // (which rides the flexion stop for 84 frames before the fall) and the fall
    // were all measured in it, so the fold OPENS to the wide range only where
    // the get-up's own fold runs (SetTrunkFold below) and closes back when the
    // get-up ends. Nothing about the built rig changes.
    public const float LumbarFlexionMaxDeg = 40f;
    public const float LumbarExtensionMinDeg = -20f;
    public const float NeckFlexionMaxDeg = 50f;
    public const float NeckExtensionMinDeg = -35f;
    public const float LumbarAcceptedMaxDeg = 20f;
    public const float LumbarAcceptedMinDeg = -20f;
    public const float NeckAcceptedMaxDeg = 30f;
    public const float NeckAcceptedMinDeg = -30f;

    public VestibularSystem vestibularSystem;
    public DogStanceController stance;
    public HumanSegment chestSegment;
    public HumanSegment pelvisSegment;
    public HumanSegment headSegment;
    // Cycle 32: the neck's own segment, so the fold's handle can reach the
    // neck's hinge (SetTrunkFold below).
    public HumanSegment neckSegment;
    public HumanSegment jawSegment;
    public BoxCollider2D jawCollider;

    // Picture only. Physics stays +X; DogStanceController.SetFacing
    // mirrors Visual offset / flipX so the box dog can look −X.
    private HumanSegment[] lookSegs;
    private float[] lookOffsetRest;
    private HumanSegment sniffPawNear;
    private HumanSegment sniffPawFar;
    private HumanSegment scratchThigh;
    private HumanSegment scratchShin;
    private HumanSegment scratchPaw;
    private HumanSegment stridePawRL;
    private HumanSegment strideUpperFR;
    private HumanSegment strideLowerFR;
    private HumanSegment strideUpperFL;
    private HumanSegment strideLowerFL;
    private HumanSegment strideThighRL;
    private HumanSegment strideShinRL;
    private bool idleVisualApplied;
    private SpriteRenderer[] sleepLids;
    private float[] sleepLidY;
    private bool sleepLidsOn;
    private float nextBlinkAt;
    private float blinkUntil;
    private SpriteRenderer[] eyes;
    private bool eyesOn;
    private float lastEyeDir;
    private float lastEyeY = float.NaN;
    private SpriteRenderer[] brows;
    private float lastBrowDir;
    private float lastBrowPose = float.NaN;
    private SpriteRenderer nose;
    private float lastNoseDir;
    private float lastNoseY = float.NaN;
    private SpriteRenderer[] whiskers;
    private float lastWhiskerDir;
    private float lastWhiskerPose = float.NaN;
    private SpriteRenderer mouth;
    private float lastMouthDir;
    private float lastMouthPose = float.NaN;
    private OrganismVoice voice;
    private SpriteRenderer barkPuff;
    private bool barkPuffOn;
    private SpriteRenderer pantPuff;
    private bool pantPuffOn;
    private float lastPantPuffDir;
    private float lastPantPuffAmt = float.NaN;
    private bool lastPantPuffChew;
    private bool lastPantPuffHunt;
    private SpriteRenderer[] ears;
    private float earFold;
    private float lastEarFold = -1f;
    private float lastEarDir;
    private bool scratchEarOn;
    private bool flopEarOn;
    private bool sleepEarOn;
    private bool quietEarOn;
    private SpriteRenderer tongue;
    private bool tongueOn;
    private SpriteRenderer chewMorsel;
    private bool chewMorselOn;
    private SpriteRenderer[] snarlTeeth;
    private bool snarlOn;
    private SpriteRenderer[] hackles;
    private bool hacklesOn;
    private float hackleRaise;
    private float lastHackleRaise = -1f;
    private float lastHackleDir;
    private bool breathOn;
    private bool rumpOn;
    private HumanSegment[] distalTailSegs;
    private bool distalTailOn;
    private float breathPhase;

    private static readonly Color SleepLidColor = DogArt.Tint("Lid");
    private static readonly Color EyeNearColor = DogArt.Tint("EyeNear");
    private static readonly Color EyeFarColor = DogArt.Tint("EyeFar");
    private static readonly Color BrowNearColor = DogArt.Tint("BrowNear");
    private static readonly Color BrowFarColor = DogArt.Tint("BrowFar");
    private static readonly Color NoseColor = DogArt.Tint("Nose");
    private static readonly Color WhiskerColor = DogArt.Tint("Whisker");
    private static readonly Color MouthColor = DogArt.Tint("Mouth");
    private static readonly Color BarkPuffColor = DogArt.Tint("BarkPuff");
    private static readonly Color PantPuffColor = DogArt.Tint("PantPuff");
    private static readonly Color EarNearColor = DogArt.Tint("EarNear");
    private static readonly Color EarFarColor = DogArt.Tint("EarFar");
    private static readonly Color TongueColor = DogArt.Tint("Tongue");
    private static readonly Color ChewMorselColor = DogArt.Tint("Chew");
    private static readonly Color SnarlToothColor = DogArt.Tint("Tooth");
    private static readonly Color HackleNearColor = DogArt.Tint("HackleNear");
    private static readonly Color HackleFarColor = DogArt.Tint("HackleFar");

    // Flat procedural tints from Art/Dog/Palette.cs. BindPicture applies
    // the same at bake. No Dog/* PNG. Rainbow debug boxes are gone.
    private static readonly Color PelvisColor = DogArt.Tint("Pelvis");
    private static readonly Color ChestColor = DogArt.Tint("Chest");
    private static readonly Color NeckColor = DogArt.Tint("Neck");
    private static readonly Color HeadColor = DogArt.Tint("Head");
    private static readonly Color JawColor = DogArt.Tint("Jaw");
    private static readonly Color TailColor = DogArt.Tint("Tail");

    private static readonly Color FrontUpperNear = DogArt.Tint("FrontRightUpper");
    private static readonly Color FrontUpperFar = DogArt.Tint("FrontLeftUpper");
    private static readonly Color FrontLowerNear = DogArt.Tint("FrontRightLower");
    private static readonly Color FrontLowerFar = DogArt.Tint("FrontLeftLower");
    private static readonly Color RearThighNear = DogArt.Tint("RearRightThigh");
    private static readonly Color RearThighFar = DogArt.Tint("RearLeftThigh");
    private static readonly Color RearShinNear = DogArt.Tint("RearRightShin");
    private static readonly Color RearShinFar = DogArt.Tint("RearLeftShin");
    private static readonly Color PawNear = DogArt.Tint("FrontRightPaw");
    private static readonly Color PawFar = DogArt.Tint("FrontLeftPaw");

    // Pre-build fallback for callers that need a height before the body
    // exists. Derived from the same defaults as the fields, so a changed
    // spawn angle or segment length cannot silently desync it from
    // StandingRootOffset. Prefer the instance version once a Dog exists.
    public static float StandingRootY(float groundY = -2f)
    {
        float upperW = DefaultChestPitch - DefaultShoulderAngle;
        float lowerW = upperW - DefaultElbowAngle;
        Vector2 shoulder = RotateZ(DefaultChestPitch, new Vector2(DefaultShoulderLocalX, -DefaultChestH * 0.5f));
        float dropF = BoneDrop(DefaultUpperLen, upperW)
                      + BoneDrop(DefaultLowerLen, lowerW) + DefaultPawH;
        return groundY - (shoulder.y - dropF);
    }

    public float StandingRootOffset(float groundY = -2f)
    {
        LimbWorldAngles(out _, out _, out float upperW, out float lowerW);
        Vector2 shoulder = FrontShoulderLocal();
        float dropF = BoneDrop(frontUpperSize.y, upperW) + BoneDrop(frontLowerSize.y, lowerW) + pawSize.y;
        return groundY - (shoulder.y - dropF);
    }

    private void LimbWorldAngles(out float thighW, out float shinW, out float upperW, out float lowerW)
    {
        // jointAngle = parentRot − childRot. This minus is that identity,
        // not a second flip: hip −12 places the thigh at world +12.
        // Pelvis stays 0; chest is preloaded at spawnChestPitch so the
        // lumbar world loop starts at zero error instead of slamming −8°
        // into the +20 stop.
        thighW = -spawnHipAngle;
        shinW = thighW - spawnKneeAngle;
        upperW = spawnChestPitch - spawnShoulderAngle;
        lowerW = upperW - spawnElbowAngle;
    }

    private Vector2 FrontShoulderLocal()
    {
        return RotateZ(spawnChestPitch, new Vector2(shoulderLocalX, -chestSize.y * 0.5f));
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

        Vector2 chestCaudal = RotateZ(spawnChestPitch, new Vector2(-chestSize.x * 0.5f, 0f));
        float pelvisCenterX = chestCaudal.x - pelvisSize.x * 0.5f;
        float pelvisCenterY = chestCaudal.y;

        HumanSegment pelvis = CreateSegment("Pelvis", pelvisSize, 0.140f, PelvisColor, 0);
        if (pelvis.collider != null)
            pelvis.collider.sharedMaterial = BodyContactMaterial();
        pelvis.transform.localPosition = new Vector3(pelvisCenterX, pelvisCenterY, 0f);
        pelvisSegment = pelvis;

        HumanSegment chest = CreateSegment("Chest", chestSize, 0.220f, ChestColor, 0);
        if (chest.collider != null)
            chest.collider.sharedMaterial = BodyContactMaterial();
        chest.transform.localPosition = Vector3.zero;
        SetWorldPitch(chest, spawnChestPitch);
        chest.ConnectTo(
            pelvis,
            new Vector2(-chestSize.x * 0.5f, 0f),
            new Vector2(pelvisSize.x * 0.5f, 0f),
            LumbarAcceptedMinDeg, LumbarAcceptedMaxDeg);
        FinishHinge(chest);
        AddFriction(chest, lumbarFriction, lumbarFrictionMaxTorque);
        AddMuscles(chest, lumbarMuscleTorque);
        chestSegment = chest;

        HumanSegment neck = CreateSegment("Neck", neckSize, 0.030f, NeckColor, 1);
        if (neck.collider != null)
            neck.collider.sharedMaterial = BodyContactMaterial();
        PlacePitchedChild(
            neck, chest,
            new Vector2(-neckSize.x * 0.5f, 0f),
            new Vector2(chestSize.x * 0.5f, 0f),
            DefaultNeckPitch);
        neck.ConnectTo(
            chest,
            new Vector2(-neckSize.x * 0.5f, 0f),
            new Vector2(chestSize.x * 0.5f, 0f),
            NeckAcceptedMinDeg, NeckAcceptedMaxDeg);
        FinishHinge(neck);
        AddFriction(neck, neckFriction);
        AddMuscles(neck, neckMuscleTorque);
        neckSegment = neck;

        HumanSegment head = CreateSegment("Head", headSize, 0.070f, HeadColor, 2);
        if (head.collider != null)
            head.collider.sharedMaterial = BodyContactMaterial();
        PlacePitchedChild(
            head, neck,
            new Vector2(-headSize.x * 0.5f, 0f),
            new Vector2(neckSize.x * 0.5f, 0f),
            DefaultNeckPitch);
        head.ConnectTo(
            neck,
            new Vector2(-headSize.x * 0.5f, 0f),
            new Vector2(neckSize.x * 0.5f, 0f),
            NeckAcceptedMinDeg, NeckAcceptedMaxDeg);
        FinishHinge(head);
        AddFriction(head, neckFriction);
        AddMuscles(head, neckMuscleTorque);
        headSegment = head;
        AttachJaw(head);

        CreateTail(pelvis, pelvisCenterX, pelvisCenterY);

        CreateFrontLeg("FrontRight", 1f, chest, 6, 7, 8, FrontUpperNear, FrontLowerNear, PawNear);
        CreateFrontLeg("FrontLeft", -1f, chest, -3, -2, -1, FrontUpperFar, FrontLowerFar, PawFar);
        CreateRearLeg("RearRight", 1f, pelvis, 3, 4, 5, RearThighNear, RearShinNear, PawNear);
        CreateRearLeg("RearLeft", -1f, pelvis, -6, -5, -4, RearThighFar, RearShinFar, PawFar);

        if (normalizeMass)
            NormalizeMass();

        DisableCollisionsBetweenSegments();
        CacheLook();
        CacheIdleVisuals();

        vestibularSystem = gameObject.AddComponent<VestibularSystem>();
        stance = gameObject.AddComponent<DogStanceController>();
        if (GetComponent<DogDrive>() == null)
            gameObject.AddComponent<DogDrive>();
        AttachBite();
        voice = GetComponent<OrganismVoice>();
        stance.Bind(this);
        ApplySpawnPoseToStance(stance);
        ApplyLookFacing(stance.FacingSign());

        ActuatorDriver actuators = gameObject.AddComponent<ActuatorDriver>();
        actuators.RebuildCache();
    }

    // Lower jaw. Hinge at the TMJ (caudal-ventral on the head). Closed
    // is jointAngle 0; plus drops the teeth (clockwise vs the head).
    // Trigger: the jaw must not shove the world. Hits go through JawStrike.
    private void AttachJaw(HumanSegment head)
    {
        Vector2 tmj = new Vector2(-headSize.x * 0.18f, -headSize.y * 0.38f);
        float headPitch = SegmentPitch(head);
        Vector2 tmjWorld = (Vector2)head.transform.localPosition + RotateZ(headPitch, tmj);
        Vector2 center = tmjWorld + RotateZ(headPitch, new Vector2(jawSize.x * 0.5f, 0f));
        HumanSegment jaw = CreateSegment("Jaw", jawSize, 0.008f, JawColor, 3);
        if (jaw.collider != null)
            jaw.collider.sharedMaterial = BodyContactMaterial();
        jaw.transform.localPosition = new Vector3(center.x, center.y, 0f);
        SetWorldPitch(jaw, headPitch);
        jaw.ConnectTo(
            head,
            new Vector2(-jawSize.x * 0.5f, 0f),
            tmj,
            0f, 40f);
        FinishHinge(jaw);
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
        float visualShift = sideSign * SideVisualOffset;
        LimbWorldAngles(out _, out _, out float upperW, out float lowerW);
        Vector2 shoulder = FrontShoulderLocal();
        Vector2 elbow = shoulder + RotateZ(upperW, new Vector2(0f, -frontUpperSize.y));
        Vector2 ankle = elbow + RotateZ(lowerW, new Vector2(0f, -frontLowerSize.y));

        HumanSegment upper = CreateSegment(sideName + "Upper", frontUpperSize, 0.040f, upperColor, upperOrder);
        PlaceRotated(upper, shoulder, upperW, frontUpperSize.y);
        upper.SetVisualOffset(visualShift);
        upper.ConnectTo(
            chest,
            new Vector2(0f, frontUpperSize.y * 0.5f),
            new Vector2(shoulderLocalX, -chestSize.y * 0.5f),
            -90f, 90f);
        FinishHinge(upper);
        AddFriction(upper, shoulderFriction);
        AddMuscles(upper, shoulderMuscleTorque);

        HumanSegment lower = CreateSegment(sideName + "Lower", frontLowerSize, 0.028f, lowerColor, lowerOrder);
        PlaceRotated(lower, elbow, lowerW, frontLowerSize.y);
        lower.SetVisualOffset(visualShift);
        lower.ConnectTo(
            upper,
            new Vector2(0f, frontLowerSize.y * 0.5f),
            new Vector2(0f, -frontUpperSize.y * 0.5f),
            0f, 120f);
        FinishHinge(lower);
        AddFriction(lower, elbowFriction);
        AddMuscles(lower, elbowMuscleTorque);

        PlacePaw(sideName + "Paw", ankle, lower, pawOrder, pawColor, visualShift, 0.012f);
        AddShoulderBridge(chest, shoulderLocalX, -chestSize.y * 0.5f, frontUpperSize.x);
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
        float visualShift = sideSign * SideVisualOffset;
        LimbWorldAngles(out float thighW, out float shinW, out float upperW, out float lowerW);
        float dropF = BoneDrop(frontUpperSize.y, upperW) + BoneDrop(frontLowerSize.y, lowerW) + pawSize.y;
        float dropR = BoneDrop(rearThighSize.y, thighW) + BoneDrop(rearShinSize.y, shinW) + pawSize.y;
        float soleY = FrontShoulderLocal().y - dropF;
        float hipRootX = pelvis.transform.localPosition.x + hipLocalX;
        float hipRootY = soleY + dropR;
        float hipInPelvisY = hipRootY - pelvis.transform.localPosition.y;
        Vector2 hip = new Vector2(hipRootX, hipRootY);
        Vector2 knee = hip + RotateZ(thighW, new Vector2(0f, -rearThighSize.y));
        Vector2 ankle = knee + RotateZ(shinW, new Vector2(0f, -rearShinSize.y));

        HumanSegment thigh = CreateSegment(sideName + "Thigh", rearThighSize, 0.080f, thighColor, thighOrder);
        PlaceRotated(thigh, hip, thighW, rearThighSize.y);
        thigh.SetVisualOffset(visualShift);
        thigh.ConnectTo(
            pelvis,
            new Vector2(0f, rearThighSize.y * 0.5f),
            new Vector2(hipLocalX, hipInPelvisY),
            -90f, 90f);
        FinishHinge(thigh);
        AddFriction(thigh, hipFriction);
        AddMuscles(thigh, hipMuscleTorque);

        HumanSegment shin = CreateSegment(sideName + "Shin", rearShinSize, 0.040f, shinColor, shinOrder);
        PlaceRotated(shin, knee, shinW, rearShinSize.y);
        shin.SetVisualOffset(visualShift);
        shin.ConnectTo(
            thigh,
            new Vector2(0f, rearShinSize.y * 0.5f),
            new Vector2(0f, -rearThighSize.y * 0.5f),
            0f, 120f);
        FinishHinge(shin);
        AddFriction(shin, kneeFriction);
        AddMuscles(shin, kneeMuscleTorque);

        PlacePaw(sideName + "Paw", ankle, shin, pawOrder, pawColor, visualShift, 0.012f);
        AddHipBridge(pelvis, hipLocalX, hipInPelvisY, rearThighSize.x);
    }

    // Picture only, no collider, no Rigidbody2D. The hip anchor sits
    // (|hipInPelvisY| - pelvisSize.y * 0.5) ~= 0.043 m BELOW the pelvis box:
    // the rear leg is placed so the paw reaches the ground with the standing
    // pose, and for this body that point is under the box. The human attaches
    // every limb ON the torso box edge (Human.CreateLeg: hipY =
    // -pelvisSize.y * 0.5) and has no seam. So the box look needs a bridge or
    // the sky reads through the hip in a still frame at 1x (18.09, held
    // against the frames of dog_art1).
    //
    // One flat rectangle in the pelvis colour, on the pelvis Visual: it
    // reaches the pelvis box above (= no seam at limb-to-torso) and covers
    // the thigh's top edge at any hip angle below (the edge's corner sweeps a
    // circle of radius width/2 around the anchor). The joint disc
    // (jointMarkerDiameter) still sits on the anchor, drawn above this.
    // Box look only: the plated coat drew its own rump plates and a bare flat
    // boss among them is a different language.
    private void AddHipBridge(HumanSegment pelvis, float hipX, float hipY, float thighWidth)
    {
        if (pelvis == null || pelvis.visual == null || !DogArt.BoxLook)
            return;
        if (pelvis.visual.Find("HipBridge") != null)
            return;

        float dropPastBox = Mathf.Abs(hipY) - pelvis.size.y * 0.5f;
        float height = dropPastBox + thighWidth * 0.5f + 0.006f;
        GameObject go = new GameObject("HipBridge");
        go.transform.SetParent(pelvis.visual, false);
        go.transform.localPosition = new Vector3(hipX, hipY, 0f);
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = LevelArt.White();
        sr.color = DogArt.BoxTint("Pelvis");
        sr.drawMode = SpriteDrawMode.Sliced;
        sr.size = new Vector2(thighWidth * 1.25f, height);
        // Under the near thigh (its order is positive) and over the far one.
        sr.sortingOrder = pelvis.sortingOrder;
        SpriteLighting.ApplyLit(sr);
    }

    // Picture only, no collider, no Rigidbody2D. Mirror of AddHipBridge for the
    // front shoulder. The front upper hangs off the chest bottom edge, but the
    // chest is preloaded at spawnChestPitch (-8 deg) while the upper sits at
    // spawnChestPitch - spawnShoulderAngle (+12 deg), so the two box edges
    // cross at ~20 deg and an exact-size rectangle leaves a thin sky wedge at
    // the shoulder (18.09, held against the frames of dog_art1). The sideways
    // visualSideOffset is already 0, so this is an angular seam, not a shifted
    // picture. One flat rectangle in the chest colour, on the chest Visual,
    // reaches the chest box above and covers the upper's top edge below; the
    // joint disc (jointMarkerDiameter) still sits on the anchor, drawn above
    // this. Box look only: the plated coat drew its own withers plates.
    private void AddShoulderBridge(HumanSegment chest, float shoulderX, float shoulderY, float upperWidth)
    {
        if (chest == null || chest.visual == null || !DogArt.BoxLook)
            return;
        if (chest.visual.Find("ShoulderBridge") != null)
            return;

        float height = upperWidth * 0.5f + 0.006f;
        GameObject go = new GameObject("ShoulderBridge");
        go.transform.SetParent(chest.visual, false);
        go.transform.localPosition = new Vector3(shoulderX, shoulderY, 0f);
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = LevelArt.White();
        sr.color = DogArt.BoxTint("Chest");
        sr.drawMode = SpriteDrawMode.Sliced;
        sr.size = new Vector2(upperWidth * 1.25f, height);
        // Under the near upper (its order is positive) and over the far one.
        sr.sortingOrder = chest.sortingOrder;
        SpriteLighting.ApplyLit(sr);
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
            HumanSegment seg = CreateSegment(name, size, massEach, TailColor, -1 - i);
            if (seg.collider != null)
                seg.collider.sharedMaterial = BodyContactMaterial();
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
            FinishHinge(seg);
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

        HumanSegment paw = CreateSegment(name, pawSize, massFraction, color, order);
        if (paw.collider != null)
            paw.collider.sharedMaterial = PawContactMaterial();
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
        FinishHinge(paw);
        AddFriction(paw, pawFriction);
        AddMuscles(paw, pawFlexorTorque, pawExtensorTorque);
        AddPawClaws(paw);
    }

    // ─── THE CLAWS, SEEN (cycle 35) ───────────────────────────────────────
    // The claws' grip is a PHYSICS fact: while a pad's grip holds,
    // DogStanceController's ClawPad swaps its own friction material onto the
    // pad's collider and the pad's measured load is the capacity. Nothing was
    // drawn, so a designer watching the dwell slow-motion saw a grip he could
    // not see (Yury 21.09 17:40: "не вижу у собаки сцепления когтей"). These
    // plates are PICTURE ONLY: no collider, no Rigidbody2D, no material, no
    // joint, no force, no torque — the pad box's own size, the look and every
    // number a run writes stay exactly as they are. Two things are drawn:
    //   (a) the TIPS — three small marks at each pad's front edge, in the flat
    //       box look, so a paw reads as a paw and not as a bare box;
    //   (b) the STATE MARK — a post at the pad's front edge, LIVE ONLY while
    //       that pad's grip really holds. Driven by the same signals the
    //       recorder publishes: getUpClawPaw names WHICH pad holds,
    //       getUpClawGripR / getUpClawGripL carry that pad's own load toward
    //       the engage bar (the post's own length), and getUpClawRipout flags
    //       the step a grip was TORN OUT — the picture holds that flash for
    //       ClawTearLatchSeconds (a picture's own persistence: a 5 fps capture
    //       would otherwise never see a one-step tear) and the post turns from
    //       amber to red, the tips retract.
    // The tear's own pad is INFERRED, not read: the recorder publishes the tear
    // as one flag for the pair while each pad's torn latch stays private, so the
    // flash is painted on the pad that was gripping on the previous step and is
    // released on it — exact whenever one pad tears alone.
    // Every plate lives on the pad's Visual, so the marks ride the paw through
    // the whole run; a null Visual (or the plated look) is a no-op, exactly like
    // the box look's other plates, and nothing here runs in -nographics beyond
    // the same build-time creation the other picture plates already do.
    private const int ClawTipCount = 3;
    private const float ClawTipW = 0.040f;
    private const float ClawTipH = 0.018f;
    private const float ClawTipFreeReach = 0.012f;   // past the front edge, free
    private const float ClawTipGripReach = 0.055f;   // dug in
    private const float ClawPostW = 0.046f;
    private const float ClawPostMinH = 0.12f;
    private const float ClawPostMaxH = 0.34f;
    private const float ClawBurstSize = 0.15f;
    private const float ClawTearLatchSeconds = 0.45f;

    // The dog's own flat look is dark on the paws (Palette Paw 0.12/0.08/0.06),
    // so bone tips read on it; the state mark is deliberately NOT a coat colour
    // and is deliberately not lit — a state must read the same in the day scene
    // and in the night one.
    private static readonly Color ClawTipFreeColor = new Color(0.94f, 0.91f, 0.80f, 1f);
    private static readonly Color ClawTipGripColor = new Color(1.00f, 0.70f, 0.20f, 1f);
    private static readonly Color ClawPostGripColor = new Color(1.00f, 0.52f, 0.05f, 0.95f);
    private static readonly Color ClawPostTearColor = new Color(0.96f, 0.13f, 0.09f, 1f);

    // One pad's picture. tips[] is the look (always drawn); post and burst are
    // the state mark and exist only on the pads the claw law owns (the two hind
    // pads), because those are the only pads whose grip is real.
    private sealed class PawClawPicture
    {
        public SpriteRenderer[] tips;
        public SpriteRenderer post;
        public SpriteRenderer burst;
        public float frontEdgeX;     // the pad's own front edge, pad-local (m)
        public float soleY;          // the pad's own sole, pad-local (m)
        public float tipCenterY0;    // the lowest tip's own height
        public float tipStepY;
        public int clawSide = -1;    // 0 = grip R, 1 = grip L, -1 = not a claw pad
        public bool engaged;         // sampled once per physics step
        public bool wasEngaged;
        public float load;           // getUpClawGrip*: this pad's own load signal
        public float tearLeft;       // render-only: seconds of the tear's flash
    }

    private readonly PawClawPicture[] clawPictures = new PawClawPicture[4];
    private int clawPictureCount;

    // Build-time plates only. The paw's box, its collider and its material are
    // untouched: everything below hangs off paw.visual and has no physics.
    private void AddPawClaws(HumanSegment paw)
    {
        if (paw == null || paw.visual == null || !DogArt.BoxLook)
            return;
        if (paw.visual.Find("ClawTip0") != null || clawPictureCount >= clawPictures.Length)
            return;

        PawClawPicture pic = new PawClawPicture();
        pic.frontEdgeX = pawSize.x * 0.5f;
        pic.soleY = -pawSize.y * 0.5f;
        pic.tipStepY = ClawTipH * 1.15f;
        pic.tipCenterY0 = pic.soleY + ClawTipH * 0.5f + 0.004f;
        // The claw law's own pads (DogStanceController: rearRightPawCol /
        // rearLeftPawCol). The front pads get the tips and no state mark.
        pic.clawSide = paw.name == "RearRightPaw" ? 0
            : (paw.name == "RearLeftPaw" ? 1 : -1);

        pic.tips = new SpriteRenderer[ClawTipCount];
        for (int i = 0; i < ClawTipCount; i++)
        {
            GameObject tip = new GameObject("ClawTip" + i.ToString());
            tip.transform.SetParent(paw.visual, false);
            SpriteRenderer sr = tip.AddComponent<SpriteRenderer>();
            sr.sprite = LevelArt.White();
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.size = new Vector2(ClawTipW, ClawTipH);
            sr.color = ClawTipFreeColor;
            sr.sortingOrder = paw.sortingOrder + 1;
            SpriteLighting.ApplyLit(sr);
            pic.tips[i] = sr;
        }

        if (pic.clawSide >= 0)
        {
            pic.post = ClawPlate(paw, "ClawPost", ClawPostW, ClawPostMinH,
                ClawPostGripColor, paw.sortingOrder + 2);
            pic.burst = ClawPlate(paw, "ClawBurst", ClawBurstSize, ClawBurstSize,
                ClawPostTearColor, paw.sortingOrder + 3);
            pic.burst.enabled = false;
        }

        clawPictures[clawPictureCount] = pic;
        clawPictureCount++;
        ApplyClawPicture(pic);
    }

    private SpriteRenderer ClawPlate(HumanSegment paw, string plateName,
        float w, float h, Color color, int order)
    {
        GameObject go = new GameObject(plateName);
        go.transform.SetParent(paw.visual, false);
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = LevelArt.White();
        sr.drawMode = SpriteDrawMode.Sliced;
        sr.size = new Vector2(w, h);
        sr.color = color;
        sr.sortingOrder = order;
        return sr;
    }

    // The grip law runs inside DogStanceController's OWN FixedUpdate, and a
    // rendered frame can carry several physics steps: a tear followed by a fresh
    // dig inside one frame would never be painted at all. So the pads' state is
    // sampled once per STEP. This reads published values and writes no physics.
    void FixedUpdate()
    {
        SampleClawMarks();
    }

    private void SampleClawMarks()
    {
        if (clawPictureCount == 0)
            return;
        int paw = stance != null ? stance.getUpClawPaw : -1;   // -1 none, 0 R, 1 L, 3 both
        bool gripR = paw == 0 || paw == 3;
        bool gripL = paw == 1 || paw == 3;
        bool torn = stance != null && stance.getUpClawRipout != 0;
        float dt = Time.fixedDeltaTime;
        for (int i = 0; i < clawPictureCount; i++)
        {
            PawClawPicture pic = clawPictures[i];
            if (pic == null || pic.clawSide < 0)
                continue;
            bool nowGrip = pic.clawSide == 0 ? gripR : gripL;
            pic.wasEngaged = pic.engaged;
            pic.engaged = nowGrip;
            pic.load = stance == null ? 0f
                : (pic.clawSide == 0 ? stance.getUpClawGripR : stance.getUpClawGripL);
            if (torn && pic.wasEngaged && !pic.engaged)
                pic.tearLeft = ClawTearLatchSeconds;           // this pad's grip tore out
            else if (pic.tearLeft > 0f)
                pic.tearLeft = Mathf.Max(0f, pic.tearLeft - dt);
        }
    }

    // Paint only: local position, size and colour of the plates built above.
    // Called every rendered frame, before the capture reads the frame.
    private void ApplyClawMarks()
    {
        for (int i = 0; i < clawPictureCount; i++)
            ApplyClawPicture(clawPictures[i]);
    }

    private static void ApplyClawPicture(PawClawPicture pic)
    {
        if (pic == null || pic.tips == null)
            return;
        bool tearing = pic.tearLeft > 0f;
        bool dugIn = pic.engaged && !tearing;
        float reach = dugIn ? ClawTipGripReach : ClawTipFreeReach;
        Color tipColor = dugIn ? ClawTipGripColor : ClawTipFreeColor;
        float tipX = pic.frontEdgeX - ClawTipW * 0.5f + reach;
        for (int i = 0; i < pic.tips.Length; i++)
        {
            SpriteRenderer tip = pic.tips[i];
            if (tip == null)
                continue;
            tip.transform.localPosition =
                new Vector3(tipX, pic.tipCenterY0 + pic.tipStepY * i, 0f);
            tip.color = tipColor;
        }

        if (pic.post == null)
            return;
        bool markOn = pic.engaged || tearing;
        pic.post.enabled = markOn;
        if (!markOn)
        {
            if (pic.burst != null)
                pic.burst.enabled = false;
            return;
        }
        float h = tearing ? ClawPostMaxH
            : Mathf.Lerp(ClawPostMinH, ClawPostMaxH, Mathf.Clamp01(pic.load));
        float postX = pic.frontEdgeX + ClawPostW * 0.5f + 0.004f;
        pic.post.size = new Vector2(ClawPostW, h);
        pic.post.transform.localPosition = new Vector3(postX, pic.soleY + h * 0.5f, 0f);
        pic.post.color = tearing ? ClawPostTearColor : ClawPostGripColor;
        if (pic.burst != null)
        {
            pic.burst.enabled = tearing;
            if (tearing)
                pic.burst.transform.localPosition =
                    new Vector3(postX, pic.soleY + ClawPostMaxH + 0.004f, 0f);
        }
    }

    // Stance targets must match the build pose. CLI may still override
    // the controller after BuildDog; Play has no second pass.
    private void ApplySpawnPoseToStance(DogStanceController c)
    {
        if (c == null) return;
        c.chestTargetTilt = spawnChestPitch;
        c.walkChestTilt = spawnChestPitch;
        c.hipBaseAngle = spawnHipAngle;
        c.shoulderBaseAngle = spawnShoulderAngle;
        c.kneeBaseAngle = spawnKneeAngle;
        c.elbowBaseAngle = spawnElbowAngle;
        c.walkStanceKnee = spawnKneeAngle;
        c.walkStanceElbow = spawnElbowAngle;
    }

    private static float SegmentPitch(HumanSegment seg)
    {
        if (seg == null) return 0f;
        if (seg.rb != null)
            return seg.rb.rotation;
        return seg.transform.localEulerAngles.z;
    }

    // Child may hold a different world pitch than the parent (chest −8,
    // neck/head 0). Anchors stay in each body's local frame; the centers
    // are placed so the hinge is one world point.
    private static void PlacePitchedChild(
        HumanSegment child,
        HumanSegment parent,
        Vector2 childAnchor,
        Vector2 parentAnchor,
        float childPitch)
    {
        if (child == null || parent == null) return;
        Vector2 joint = (Vector2)parent.transform.localPosition
                        + RotateZ(SegmentPitch(parent), parentAnchor);
        Vector2 center = joint - RotateZ(childPitch, childAnchor);
        child.transform.localPosition = new Vector3(center.x, center.y, 0f);
        SetWorldPitch(child, childPitch);
    }

    private static void SetWorldPitch(HumanSegment seg, float deg)
    {
        if (seg == null) return;
        seg.transform.localRotation = Quaternion.Euler(0f, 0f, deg);
        if (seg.rb != null)
            seg.rb.rotation = deg;
    }

    private static void FinishHinge(HumanSegment seg)
    {
        if (seg == null || seg.joint == null) return;
        HingeJoint2D joint = seg.joint;
        Vector2 anchor = joint.anchor;
        Vector2 connected = joint.connectedAnchor;
        joint.autoConfigureConnectedAnchor = false;
        joint.anchor = anchor;
        joint.connectedAnchor = connected;
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

    // One material for all four pads and one for the body plating - the
    // Human.SoleMaterial pattern. A contact is a pair and Unity combines
    // it, so the value only makes sense together with the ground. Joint
    // friction (above) is a different thing: it lives inside the hinge.
    private PhysicsMaterial2D pawContactMaterial;

    private PhysicsMaterial2D PawContactMaterial()
    {
        if (pawContactMaterial == null)
        {
            pawContactMaterial = new PhysicsMaterial2D("DogPaw");
            pawContactMaterial.friction = Mathf.Max(0f, pawContactFriction);
            pawContactMaterial.bounciness = 0f;
        }

        return pawContactMaterial;
    }

    private PhysicsMaterial2D bodyContactMaterial;

    private PhysicsMaterial2D BodyContactMaterial()
    {
        if (bodyContactMaterial == null)
        {
            bodyContactMaterial = new PhysicsMaterial2D("DogBody");
            bodyContactMaterial.friction = Mathf.Max(0f, bodyContactFriction);
            bodyContactMaterial.bounciness = 0f;
        }

        return bodyContactMaterial;
    }

    // ─── THE CLAWS (cycle 31): the grip's own material and its handle ───
    // The designer's order (21.09): the hind foot presses the ground, the claws
    // dig in, the paw FIXES — and the leg's own drive then levers the body up
    // over that fulcrum. A paw's grip is its contact's friction, and the pads
    // carry their own material (DogPaw, above) — this is the one they carry
    // while a grip HOLDS: the same collider, the same dirt, a coefficient the
    // paw's own 0.4 cannot give. The solver's own friction is therefore the
    // claw's capacity (mu_pair * Fn, FINITE: above it the contact slides, and
    // DogStanceController reads that slide as the RIP-OUT). The frictionCombine
    // is left where EVERY material in this build leaves it (DogPaw, DogBody,
    // GroundBuilder's GroundDirt), so the pair stays the engine's own mixed
    // value and the report's capacity column is computed from the engine's own
    // PhysicsMaterial2D.GetCombinedValues, not from a rule assumed here.
    // 4.0 = ten times the paw's nominal 0.4. CLI: -dogClawFriction <f>.
    public float clawContactFriction = 4f;

    // Negative means "use the serialized field above". The pad's contact
    // friction is read by HeadlessTrial (-dogPawFriction); the CLAW's own knob
    // is read here, the -dogSideOffset pattern, because the material lives here.
    public static float ClawFrictionOverride = -1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ReadClawFrictionSwitch()
    {
        string[] args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i + 1 < args.Length; i++)
        {
            if (args[i] != "-dogClawFriction")
                continue;
            float parsed;
            if (float.TryParse(args[i + 1], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out parsed)
                && parsed >= 0f && parsed <= 100f)
                ClawFrictionOverride = parsed;
        }
    }

    private PhysicsMaterial2D clawContactMaterial;

    public PhysicsMaterial2D ClawContactMaterial()
    {
        if (clawContactMaterial == null)
        {
            clawContactMaterial = new PhysicsMaterial2D("DogClaw");
            clawContactMaterial.friction = Mathf.Max(0f,
                ClawFrictionOverride >= 0f ? ClawFrictionOverride : clawContactFriction);
            clawContactMaterial.bounciness = 0f;
        }

        return clawContactMaterial;
    }

    // The grip's HANDLE. DogStanceController's catch branch owns WHEN a claw is
    // engaged (the pad's own measured load and that branch's plant command);
    // this owns WHAT the swap is: the pad's collider carries the claw material
    // while the grip holds and its own paw material again the moment it
    // releases. Both materials live here, beside the body's own; the release
    // restores the SAME PawContactMaterial instance the build assigned, so a
    // released pad is bit-identical to never-clawed.
    public void SetClawGrip(Collider2D pawCol, bool engaged)
    {
        if (pawCol == null)
            return;
        pawCol.sharedMaterial = engaged ? ClawContactMaterial() : PawContactMaterial();
    }

    // ─── THE FOLD'S HANDLE (cycle 32) ───
    // The c31 SetClawGrip pattern: the law asks the rig for the fold's own
    // range, the rig answers, and the range's own numbers stay here (the
    // constants above). `open` widens the lumbar's flexion to +40 and the
    // neck's to +50 / −35; closed is the accepted range the rig was built with.
    // The extension side never moves (the accepted −20 / −30): the anatomical
    // dorsal range is small and every accepted row was measured in it.
    // A no-op when the range is already the one asked for, and SetJointRange
    // refuses to touch a hinge whose limits already read the target — so the
    // stand, the walk and the fall never touch a joint's limits at all.
    private bool trunkFoldOpen;

    public bool TrunkFoldOpen { get { return trunkFoldOpen; } }

    public void SetTrunkFold(bool open)
    {
        if (open == trunkFoldOpen)
            return;
        trunkFoldOpen = open;
        SetJointRange(chestSegment, LumbarAcceptedMinDeg,
            open ? LumbarFlexionMaxDeg : LumbarAcceptedMaxDeg);
        SetJointRange(neckSegment, NeckAcceptedMinDeg,
            open ? NeckFlexionMaxDeg : NeckAcceptedMaxDeg);
        SetJointRange(headSegment, NeckAcceptedMinDeg,
            open ? NeckFlexionMaxDeg : NeckAcceptedMaxDeg);
    }

    private static void SetJointRange(HumanSegment seg, float min, float max)
    {
        if (seg == null || seg.joint == null)
            return;
        HingeJoint2D joint = seg.joint;
        JointAngleLimits2D limits = joint.limits;
        if (Mathf.Abs(limits.min - min) < 1e-4f && Mathf.Abs(limits.max - max) < 1e-4f)
            return;                       // already this range: no touch, no solve change
        // Assigning limits while useLimits is already true is ignored on some
        // 2D builds (HumanSegment.ConnectTo's own note) — the same dance here.
        joint.useLimits = false;
        limits.min = min;
        limits.max = max;
        joint.limits = limits;
        joint.useLimits = true;
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

    // Once at build. ApplyLookFacing must not Find or GetComponents.
    private void CacheLook()
    {
        lookSegs = GetComponentsInChildren<HumanSegment>(true);
        lookOffsetRest = new float[lookSegs.Length];
        for (int i = 0; i < lookSegs.Length; i++)
            lookOffsetRest[i] = lookSegs[i] != null ? lookSegs[i].visualOffsetX : 0f;
    }

    // Picture only: invert the pair offset and flipX. Colliders stay put,
    // so a −X face is a look, not a lunge and not localScale on the body.
    public void ApplyLookFacing(float sign)
    {
        float dir = sign >= 0f ? 1f : -1f;
        bool flip = dir < 0f;
        if (lookSegs == null)
            return;
        for (int i = 0; i < lookSegs.Length; i++)
        {
            HumanSegment seg = lookSegs[i];
            if (seg == null)
                continue;
            float rest = lookOffsetRest != null && i < lookOffsetRest.Length
                ? lookOffsetRest[i]
                : seg.visualOffsetX;
            seg.SetVisualOffset(rest * dir);
            if (seg.sprite != null)
                seg.sprite.flipX = flip;
        }
    }

    // Once at build. Idle LateUpdate must not Find.
    private void CacheIdleVisuals()
    {
        sniffPawNear = FindLookSeg("FrontRightPaw");
        sniffPawFar = FindLookSeg("FrontLeftPaw");
        scratchThigh = FindLookSeg("RearRightThigh");
        scratchShin = FindLookSeg("RearRightShin");
        scratchPaw = FindLookSeg("RearRightPaw");
        stridePawRL = FindLookSeg("RearLeftPaw");
        strideUpperFR = FindLookSeg("FrontRightUpper");
        strideLowerFR = FindLookSeg("FrontRightLower");
        strideUpperFL = FindLookSeg("FrontLeftUpper");
        strideLowerFL = FindLookSeg("FrontLeftLower");
        strideThighRL = FindLookSeg("RearLeftThigh");
        strideShinRL = FindLookSeg("RearLeftShin");
        BuildSleepLids();
        BuildEyes();
        BuildBrows();
        BuildNose();
        BuildWhiskers();
        BuildMouth();
        BuildBarkPuff();
        BuildEars();
        BuildTongue();
        BuildPantPuff();
        BuildChewMorsel();
        BuildSnarlTeeth();
        BuildHackles();
        CacheDistalTail();
        breathPhase = (GetHashCode() & 31) * 0.19f;
        if (stance != null)
            stance.sitBreathPhase = breathPhase;
    }

    // Closed-eye slits on Head Visual. White rectangle + tint, no PNG.
    // Sleep holds them shut; yawn peak uses the same pair so a gape is
    // not a silent Bark. Forage blinks between acts — that only reads
    // once a pupil is there to cover. Forage Walk reuses a thinner
    // slit (WalkSquint) so a hanging tongue is not a hunt-lock stare.
    // Sit idlePant reuses that slit (SitSquint) so EndWalk is not a
    // round rest eye after the trot already squinted. Sit idleScratch
    // reuses that slit (SitScratchSquint) so the hind-paw kick is not
    // a round rest eye after the pupil already dropped. Sit idleLook
    // reuses that slit (SitLookSquint) so look-down is not a round
    // rest eye after the pupil already dropped. Look-up stays open.
    // Hunt recover sit reuses that slit (SitHuntSquint) so the
    // 0.70 s coil is not a round rest eye after the pupil already
    // drops. Hunt stalk Walk reuses that slit (WalkHuntSquint) so
    // the chase is not a round rest eye after the pupil already
    // drops. Hunt leap reuses that slit (LeapHuntSquint) so the
    // pounce is not a round rest eye after the brow already
    // furrows (WalkHuntSquint/SitHuntSquint skip IsLeap;
    // LeapAttack zeros idleHunt). Sit idleChew reuses that
    // slit (SitChewSquint) so the meal is not an open
    // sniff-track eye after the jaw already snaps (SitSquint
    // skips idleChew). No idleBlink: Walk() would
    // zero it.
    private void BuildSleepLids()
    {
        sleepLidY = new[] { 0.018f, 0.006f };
        sleepLids = new SpriteRenderer[2];
        if (headSegment == null || headSegment.visual == null)
            return;
        int order = headSegment.sortingOrder;
        sleepLids[0] = MakeSleepLid("SleepLidNear", headSegment.visual, order + 2);
        sleepLids[1] = MakeSleepLid("SleepLidFar", headSegment.visual, order - 1);
        if (sleepLids[1] != null)
            sleepLids[1].color = new Color(SleepLidColor.r, SleepLidColor.g, SleepLidColor.b, 0.55f);
        nextBlinkAt = Time.time + 0.6f + (GetHashCode() & 7) * 0.18f;
    }

    private static SpriteRenderer MakeSleepLid(string name, Transform parent, int order)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(0.032f, 0.018f, 0f);
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = LevelArt.White();
        sr.color = SleepLidColor;
        sr.drawMode = SpriteDrawMode.Sliced;
        sr.size = new Vector2(0.055f, 0.011f);
        sr.sortingOrder = order;
        sr.enabled = false;
        SpriteLighting.ApplyLit(sr);
        return sr;
    }

    // Dark pupil on Head Visual. White rectangle + tint, no PNG.
    // Without this idleLook / greet / hunt-lock is a blank yellow box
    // and the blink slit covers nothing. Forage Walk reuses a milder
    // pant drop (WalkEye) so a dropped snout is not a hunt-lock stare
    // through the squint. Sit idlePant reuses that drop (SitEye) so
    // EndWalk is not a rest stare after the trot already dropped the
    // pupil. Sit idleScratch reuses that drop (SitScratchEye) so the
    // hind-paw kick is not a rest stare through the ear. Hunt leap
    // reuses that drop (LeapHuntEye) so the pounce is not a rest-Y
    // stare after the lid already slits and the brow already furrows
    // (WalkEye/SitEye skip IsLeap; LeapAttack zeros idleHunt). Sit
    // idleChew reuses a drop hitch (SitChewEye) so the meal is not
    // a held sniff drop after the jaw / lid / ear / brow / whisker /
    // neck / nose / lip already snap. No idleEye: Walk() would zero it.
    private void BuildEyes()
    {
        eyes = new SpriteRenderer[2];
        if (headSegment == null || headSegment.visual == null)
            return;
        int order = headSegment.sortingOrder;
        eyes[0] = MakeEye("EyeNear", headSegment.visual, order + 1, EyeNearColor, new Vector2(0.018f, 0.014f));
        eyes[1] = MakeEye("EyeFar", headSegment.visual, order - 2, EyeFarColor, new Vector2(0.014f, 0.011f));
    }

    // Dark brow above the pupil on Head Visual. White rectangle + tint,
    // no PNG. Pupil without a brow still reads as a yellow box. Look
    // raises; sniff / hunt furrow; Sleep droops. Forage Walk reuses a
    // milder pant crease (WalkBrow) so a hanging tongue is not a
    // rest / hunt-lock forehead. Sit idlePant reuses that crease
    // (SitBrow) so EndWalk is not a rest forehead after the trot
    // already creased. Sit idleScratch reuses that crease
    // (SitScratchBrow) so the hind-paw kick is not a rest forehead
    // after the pupil already dropped and the lid already slit.
    // Sit idleYawn reuses a raise hitch (SitYawnBrow) so the gape
    // is not a rest forehead after the ears already fold and the
    // whiskers already fan. Sit idleChew reuses a crease hitch
    // (SitChewBrow) so the meal is not a statue sniff furrow after
    // the jaw / lid / ear already snap. Always on: lids own the
    // eye. No idleBrow: Walk() would zero it.
    private void BuildBrows()
    {
        brows = new SpriteRenderer[2];
        if (headSegment == null || headSegment.visual == null)
            return;
        int order = headSegment.sortingOrder;
        brows[0] = MakeBrow("BrowNear", headSegment.visual, order + 3, BrowNearColor, new Vector2(0.028f, 0.005f));
        brows[1] = MakeBrow("BrowFar", headSegment.visual, order, BrowFarColor, new Vector2(0.022f, 0.004f));
    }

    private static SpriteRenderer MakeBrow(string name, Transform parent, int order, Color color, Vector2 size)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(0.028f, 0.032f, 0f);
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = LevelArt.White();
        sr.color = color;
        sr.drawMode = SpriteDrawMode.Sliced;
        sr.size = size;
        sr.sortingOrder = order;
        SpriteLighting.ApplyLit(sr);
        return sr;
    }

    // Dark nose on the cranial Head Visual. White rectangle + tint, no PNG.
    // Pupil without a muzzle still reads as a yellow box. Look lifts;
    // sniff / hunt drop it (dirt / stalk). Sleep keeps it — lids own
    // the eye. Forage Walk reuses a milder pant drop (WalkNose) so a
    // hanging tongue is not a rest / hunt-lock snout. Sit idlePant
    // reuses that drop (SitNose) so EndWalk is not a rest muzzle
    // after the trot already dropped the snout. Sit idleScratch
    // reuses that drop (SitScratchNose) so the hind-paw kick is not
    // a rest muzzle after the pupil, lid, brow, and whiskers already
    // moved. Sit idleYawn reuses a lift hitch (SitYawnNose) so the
    // gape is not a rest muzzle after the brow already raises. Hunt
    // leap reuses that drop (LeapHuntNose) so the pounce is not a
    // rest muzzle after the pupil already drops and the brow/lips
    // already furrow (WalkNose/SitNose skip IsLeap; LeapAttack
    // zeros idleHunt). Sit idleChew reuses a drop hitch
    // (SitChewNose) so the meal is not a held sniff drop after
    // the jaw / lid / ear / brow / whisker / neck already snap.
    // Sit chew lip is SitChewMouth. No idleNose: Walk() would zero it.
    private void BuildNose()
    {
        if (headSegment == null || headSegment.visual == null)
            return;
        GameObject go = new GameObject("Nose");
        go.transform.SetParent(headSegment.visual, false);
        go.transform.localPosition = new Vector3(0.058f, -0.010f, 0f);
        nose = go.AddComponent<SpriteRenderer>();
        nose.sprite = LevelArt.White();
        nose.color = NoseColor;
        nose.drawMode = SpriteDrawMode.Sliced;
        nose.size = new Vector2(0.022f, 0.016f);
        nose.sortingOrder = headSegment.sortingOrder + 4;
        SpriteLighting.ApplyLit(nose);
    }

    // Three thin slices caudal of the nose. White rectangle + tint, no PNG.
    // Nose without whiskers still reads as a stamp on a yellow box. Sniff
    // fans and twitches; hunt / Leap pin back; Sleep droops. Forage Walk
    // fans a milder twitch (WalkWhisker) so a hanging tongue is not a
    // rest muzzle. Sit idlePant reuses that fan (SitWhisker) so
    // EndWalk is not rest stamps after the trot already fanned.
    // Sit idleScratch reuses that fan (SitScratchWhisker) so the
    // hind-paw kick is not rest stamps after the pupil, lid, and
    // brow already moved. Sit idleLook reuses a signed fan
    // (SitLookWhisker) so the neck bob is not rest stamps after
    // the ears already hitch. Sit idleYawn reuses a fold fan
    // (SitYawnWhisker) so the gape is not rest stamps after the
    // ears already fold. Sit idleChew reuses a fan hitch
    // (SitChewWhisker) so the meal is not a sniff fan+11 Hz after
    // the jaw / lid / ear / brow already snap. Quiet Forage sit
    // reuses a breath hitch (SitQuietWhisker) so the hold is not
    // rest stamps after chest / rump / neck / ears / brows /
    // flews already hitch. Always on: lids own the eye, not the
    // muzzle. No idleWhisker: Walk() would zero it.
    private void BuildWhiskers()
    {
        whiskers = new SpriteRenderer[3];
        if (headSegment == null || headSegment.visual == null)
            return;
        int order = headSegment.sortingOrder + 3;
        for (int i = 0; i < whiskers.Length; i++)
            whiskers[i] = MakeWhisker("Whisker" + i, headSegment.visual, order, i);
    }

    private static SpriteRenderer MakeWhisker(string name, Transform parent, int order, int index)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        float y = -0.008f + (index - 1) * 0.006f;
        go.transform.localPosition = new Vector3(0.050f, y, 0f);
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = LevelArt.White();
        sr.color = WhiskerColor;
        sr.drawMode = SpriteDrawMode.Sliced;
        sr.size = new Vector2(0.028f, 0.0045f);
        sr.sortingOrder = order;
        SpriteLighting.ApplyLit(sr);
        return sr;
    }

    // Dark lip ventral of the nose on Head Visual. White rectangle + tint,
    // no PNG. Whiskers without a mouth still read as stamps on a yellow
    // box. Hunt / Leap raise the flews; Sleep droops; pant / yawn / chew
    // open with DriveJaw. Sit idleScratch reuses a sniff-like tuck
    // (SitScratchMouth) so the hind-paw kick is not rest flews after
    // the snout already dropped. Sit idleLook reuses a signed hitch
    // (SitLookMouth) so the neck bob is not rest flews after the lids
    // already slit. Sit idleYawn reuses a lift hitch (SitYawnMouth)
    // so the gape is not rest flews after the snout already lifts.
    // Sit idleChew reuses a tuck hitch (SitChewMouth) so the meal
    // is not a held sniff tuck after the jaw / neck / nose already
    // snap. Quiet Forage sit reuses a breath hitch (SitQuietMouth)
    // so the hold is not rest flews after chest / rump / neck /
    // ears / brows already hitch. Night Sleep reuses a breath hitch
    // (SitSleepMouth) so the hold is not statue droop after the
    // brows already hitch (ApplyMouth held sleep*4). Always on:
    // lids own the eye. No idleMouth: Walk() would zero it.
    private void BuildMouth()
    {
        if (headSegment == null || headSegment.visual == null)
            return;
        GameObject go = new GameObject("Mouth");
        go.transform.SetParent(headSegment.visual, false);
        go.transform.localPosition = new Vector3(0.032f, -0.028f, 0f);
        mouth = go.AddComponent<SpriteRenderer>();
        mouth.sprite = LevelArt.White();
        mouth.color = MouthColor;
        mouth.drawMode = SpriteDrawMode.Sliced;
        mouth.size = new Vector2(0.038f, 0.007f);
        mouth.sortingOrder = headSegment.sortingOrder + 2;
        SpriteLighting.ApplyLit(mouth);
    }

    private static SpriteRenderer MakeEye(string name, Transform parent, int order, Color color, Vector2 size)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(0.030f, 0.016f, 0f);
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = LevelArt.White();
        sr.color = color;
        sr.drawMode = SpriteDrawMode.Sliced;
        sr.size = size;
        sr.sortingOrder = order;
        SpriteLighting.ApplyLit(sr);
        return sr;
    }

    // Breath puff on Jaw Visual while OrganismVoice.Call is fresh.
    // White rectangle + tint, no PNG. Physics jaw already opens via Bark.
    private void BuildBarkPuff()
    {
        if (jawSegment == null || jawSegment.visual == null)
            return;
        GameObject go = new GameObject("BarkPuff");
        go.transform.SetParent(jawSegment.visual, false);
        go.transform.localPosition = new Vector3(0.062f, 0.012f, 0f);
        barkPuff = go.AddComponent<SpriteRenderer>();
        barkPuff.sprite = LevelArt.White();
        barkPuff.color = BarkPuffColor;
        barkPuff.drawMode = SpriteDrawMode.Sliced;
        barkPuff.size = new Vector2(0.028f, 0.022f);
        barkPuff.sortingOrder = jawSegment.sortingOrder + 3;
        barkPuff.enabled = false;
        barkPuffOn = false;
        SpriteLighting.ApplyLit(barkPuff);
    }

    // Pricked vs pinned triangles on Head Visual. White rectangle + tint,
    // no PNG. Pose reads existing brain floats / Walk / Leap — not a hinge.
    // Sit idlePant reuses the walk flop (SitEar) so EndWalk is not a
    // rest half-fold after the trot already flopped. Sit idleLook
    // reuses a signed hitch (SitLookEar) so the neck bob is not a
    // rest fold except a binary perk at idleLook > 0.45. Sit idleYawn
    // reuses a fold hitch (SitYawnEar) so the gape is not a statue
    // half-fold after the jaw already opens. Sit idleChew reuses a
    // fold hitch (SitChewEar) so the meal is not a statue sniff
    // half-fold after the jaw already snaps. Quiet Forage sit
    // reuses a breath hitch (SitQuietEar) so the hold is not
    // statue rest fold 0.20 after chest / rump / neck already
    // hitch. No idleEar: Walk() would zero it.
    private void BuildEars()
    {
        ears = new SpriteRenderer[2];
        if (headSegment == null || headSegment.visual == null)
            return;
        int order = headSegment.sortingOrder;
        ears[0] = MakeEar("EarNear", headSegment.visual, order + 3, EarNearColor, new Vector2(0.038f, 0.055f));
        ears[1] = MakeEar("EarFar", headSegment.visual, order - 2, EarFarColor, new Vector2(0.032f, 0.046f));
        earFold = 0.20f;
    }

    private static SpriteRenderer MakeEar(string name, Transform parent, int order, Color color, Vector2 size)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(-0.016f, 0.052f, 0f);
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = LevelArt.White();
        sr.color = color;
        sr.drawMode = SpriteDrawMode.Sliced;
        sr.size = size;
        sr.sortingOrder = order;
        SpriteLighting.ApplyLit(sr);
        return sr;
    }

    // Flat pink box on Jaw Visual. No PNG. Shown while idlePant or
    // idleYawn (wider curl). Hidden during Bark puff / chew.
    private void BuildTongue()
    {
        if (jawSegment == null || jawSegment.visual == null)
            return;
        GameObject go = new GameObject("Tongue");
        go.transform.SetParent(jawSegment.visual, false);
        go.transform.localPosition = new Vector3(0.048f, -0.010f, 0f);
        tongue = go.AddComponent<SpriteRenderer>();
        tongue.sprite = LevelArt.White();
        tongue.color = TongueColor;
        tongue.drawMode = SpriteDrawMode.Sliced;
        tongue.size = new Vector2(0.042f, 0.014f);
        tongue.sortingOrder = jawSegment.sortingOrder + 2;
        tongue.enabled = false;
        tongueOn = false;
        SpriteLighting.ApplyLit(tongue);
    }

    // Pale breath on Jaw Visual while WalkPant, sit idlePant,
    // sit idleYawn, sit idleChew, hunt recover idleHunt, hunt
    // stalk Walk idleHunt, or hunt leap IsLeap hangs vapor. White
    // rectangle + tint, no PNG. Not Bark: Call keeps the cream
    // puff. Hidden during Bark. Sit chew reuses this sliver
    // (SitChewPuff) cranial of the morsel so the meal is not a
    // silent lump. Hunt recover reuses it (SitHuntPuff), stalk
    // Walk reuses it (WalkHuntPuff), and leap reuses it
    // (LeapHuntPuff) cranial of the fangs so the coil / chase /
    // pounce is not a silent snarl.
    private void BuildPantPuff()
    {
        if (jawSegment == null || jawSegment.visual == null)
            return;
        GameObject go = new GameObject("PantPuff");
        go.transform.SetParent(jawSegment.visual, false);
        go.transform.localPosition = new Vector3(0.072f, -0.006f, 0f);
        pantPuff = go.AddComponent<SpriteRenderer>();
        pantPuff.sprite = LevelArt.White();
        pantPuff.color = PantPuffColor;
        pantPuff.drawMode = SpriteDrawMode.Sliced;
        pantPuff.size = new Vector2(0.018f, 0.010f);
        pantPuff.sortingOrder = jawSegment.sortingOrder + 3;
        pantPuff.enabled = false;
        pantPuffOn = false;
        SpriteLighting.ApplyLit(pantPuff);
    }

    // Flat dark box at the teeth while idleChew. Same mark color as
    // DogPrey, LevelArt.White, no PNG. Hidden during Bark puff.
    private void BuildChewMorsel()
    {
        if (jawSegment == null || jawSegment.visual == null)
            return;
        GameObject go = new GameObject("ChewMorsel");
        go.transform.SetParent(jawSegment.visual, false);
        go.transform.localPosition = new Vector3(0.052f, -0.004f, 0f);
        chewMorsel = go.AddComponent<SpriteRenderer>();
        chewMorsel.sprite = LevelArt.White();
        chewMorsel.color = ChewMorselColor;
        chewMorsel.drawMode = SpriteDrawMode.Sliced;
        chewMorsel.size = new Vector2(0.022f, 0.016f);
        chewMorsel.sortingOrder = jawSegment.sortingOrder + 2;
        chewMorsel.enabled = false;
        chewMorselOn = false;
        SpriteLighting.ApplyLit(chewMorsel);
    }

    // Two cream slivers on Jaw Visual. No PNG. Hunt stalk / leap only —
    // Call is the puff, chew is the morsel. Pose reads idleHunt / IsLeap.
    private void BuildSnarlTeeth()
    {
        snarlTeeth = new SpriteRenderer[2];
        if (jawSegment == null || jawSegment.visual == null)
            return;
        int order = jawSegment.sortingOrder;
        snarlTeeth[0] = MakeSnarlTooth("SnarlNear", jawSegment.visual, order + 4, new Vector2(0.010f, 0.018f));
        snarlTeeth[1] = MakeSnarlTooth("SnarlFar", jawSegment.visual, order + 3, new Vector2(0.008f, 0.015f));
        if (snarlTeeth[1] != null)
        {
            Color far = SnarlToothColor;
            far.a = 0.62f;
            snarlTeeth[1].color = far;
        }
    }

    private static SpriteRenderer MakeSnarlTooth(string name, Transform parent, int order, Vector2 size)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(0.058f, -0.008f, 0f);
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = LevelArt.White();
        sr.color = SnarlToothColor;
        sr.drawMode = SpriteDrawMode.Sliced;
        sr.size = size;
        sr.sortingOrder = order;
        sr.enabled = false;
        SpriteLighting.ApplyLit(sr);
        return sr;
    }

    // Three dark slices along Chest Visual withers. No PNG. Pose reads
    // idleHunt / IsLeap — forage sit must not grow a ridge. No idleHackles:
    // Walk() would zero it on the first chase step.
    private void BuildHackles()
    {
        hackles = new SpriteRenderer[3];
        if (chestSegment == null || chestSegment.visual == null)
            return;
        int order = chestSegment.sortingOrder;
        hackles[0] = MakeHackle("HackleCaudal", chestSegment.visual, order + 1,
            HackleFarColor, new Vector2(0.016f, 0.020f));
        hackles[1] = MakeHackle("HackleMid", chestSegment.visual, order + 2,
            HackleNearColor, new Vector2(0.018f, 0.026f));
        hackles[2] = MakeHackle("HackleCranial", chestSegment.visual, order + 2,
            HackleNearColor, new Vector2(0.015f, 0.022f));
    }

    private static SpriteRenderer MakeHackle(
        string name, Transform parent, int order, Color color, Vector2 size)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(0f, 0.068f, 0f);
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = LevelArt.White();
        sr.color = color;
        sr.drawMode = SpriteDrawMode.Sliced;
        sr.size = size;
        sr.sortingOrder = order;
        sr.enabled = false;
        SpriteLighting.ApplyLit(sr);
        return sr;
    }

    private HumanSegment FindLookSeg(string name)
    {
        if (lookSegs == null)
            return null;
        for (int i = 0; i < lookSegs.Length; i++)
        {
            HumanSegment seg = lookSegs[i];
            if (seg != null && seg.name == name)
                return seg;
        }
        return null;
    }

    void LateUpdate()
    {
        // The claws' own picture first: it is drawn for the -visual trial too,
        // where the write-back below is switched off on purpose.
        ApplyClawMarks();
        if (HeadlessTrial.Active || stance == null)
            return;

        float face = stance.FacingSign();
        ApplySleepLids(stance.idleSleep, face);
        ApplyEyes(face);
        ApplyBrows(face);
        ApplyNose(face);
        ApplyWhiskers(face);
        ApplyMouth(face);
        ApplyBarkPuff(face);
        ApplyEars(face);
        ApplyTongue(face);
        ApplyPantPuff(face);
        ApplyChewMorsel(face);
        ApplySnarl(face);
        ApplyHackles(face);
        ApplyBreath();
        ApplyRump();
        ApplyDistalTail();

        float sniff = stance.idleSniff;
        float scratch = stance.idleScratch;
        // Wander writes idleSniff on a real trot; do not shuffle paw
        // Visuals on top of the swing. Sit sniff lifts the near-front
        // picture only (far stays planted). Recover sit writes idleHunt
        // without Walk: plant the same near-front Visual so the 0.70 s
        // snarl is not a statue. Stalk Walk / Leap keep colliders;
        // pictures stay on the swing / coil. Sit SetFacing hops the
        // same near pair then plants — Drive already flipped Look.
        // Walk SetFacing uses the same hop on the trot (wander /
        // follow / hunt about-face); Walk() after a sit flip takes it.
        // DriveWalk leftover sit keeps colliders planted, so the
        // swing joint twitch still reads as a rake: lift the swing
        // pair pictures on the CoM-capture bound (walkStrideAmp).
        // Both rears, then both fronts. Paw-only left a stamp on planted thighs.
        // Sit sniff stays sit-only. Hop owns the about-face window. Withers heave
        // is ApplyBreath / walkBobAmp / sitYawnBreathAmp /
        // sitSniffBreathAmp / sitScratchBreathAmp / sitLookBreathAmp /
        // sitHuntBreathAmp / sitChewBreathAmp / leapHuntBreathAmp; rump is
        // ApplyRump / walkRumpAmp / sitRumpAmp / sitYawnRumpAmp /
        // sitSniffRumpAmp / sitScratchRumpAmp / sitLookRumpAmp /
        // sitHuntRumpAmp / sitChewRumpAmp / leapHuntRumpAmp /
        // sitSleepRumpAmp / sitQuietRumpAmp — not this limb path. Sleep snout
        // is sitSleepNoseAmp / SitSleepNose. Sleep ear hitch
        // is sitSleepEarAmp / SitSleepEar. Sleep brow hitch
        // is sitSleepBrowAmp / SitSleepBrow. Sleep mouth hitch
        // is sitSleepMouthAmp / SitSleepMouth. Sleep whisker hitch
        // is sitSleepWhiskerAmp / SitSleepWhisker. Quiet sit neck is
        // sitQuietHeadAmp / SitQuietHead. Quiet sit ear hitch is
        // sitQuietEarAmp / SitQuietEar. Quiet sit brow hitch is
        // sitQuietBrowAmp / SitQuietBrow. Quiet sit mouth hitch is
        // sitQuietMouthAmp / SitQuietMouth. Quiet sit whisker hitch
        // is sitQuietWhiskerAmp / SitQuietWhisker. Quiet sit distal
        // limp is sitQuietDistalTailAmp / SitQuietDistalTail. Sit look
        // tail is IdleTailTarget / sitLookTailAmp; sit sniff droop
        // is sitSniffTailAmp; sit scratch droop is sitScratchTailAmp;
        // sit yawn droop is sitYawnTailAmp; sit pant droop is
        // sitPantTailAmp; sit chew droop is sitChewTailAmp; hunt leap
        // raise is leapHuntTailAmp / LeapHuntTail (DriveLeap); night
        // Sleep droop is sitSleepTailAmp / SitSleepTail on the first
        // hinge — not a Visual. Distal limp is sitSleepDistalTailAmp
        // / SitSleepDistalTail, sitSniffDistalTailAmp /
        // SitSniffDistalTail, sitYawnDistalTailAmp /
        // SitYawnDistalTail, sitPantDistalTailAmp /
        // SitPantDistalTail, sitChewDistalTailAmp /
        // SitChewDistalTail, sitLookDistalTailAmp /
        // SitLookDistalTail, sitScratchDistalTailAmp /
        // SitScratchDistalTail, sitQuietDistalTailAmp /
        // SitQuietDistalTail, sitHuntDistalTailAmp /
        // SitHuntDistalTail, walkHuntDistalTailAmp /
        // WalkHuntDistalTail, walkDistalTailAmp /
        // WalkDistalTail, and leapHuntDistalTailAmp /
        // LeapHuntDistalTail on Tail2–Tail5 Visual.
        bool walking = stance.walkWanted || stance.IsWalk();
        bool huntPlant = !walking && !stance.IsLeap() && stance.idleHunt > 0.01f;
        float hop = stance.SitTurnHop();
        float walkHop = stance.WalkTurnHop();
        bool sitTurn = hop > 0.01f && scratch < 0.01f && !huntPlant
                       && !(sniff > 0.01f && !walking);
        bool walkTurn = walkHop > 0.01f && !stance.IsLeap();
        bool walkStride = walking && !walkTurn && !stance.IsLeap()
                          && !stance.energyRest && stance.walkStrideAmp > 0.01f;
        bool any = (sniff > 0.01f && !walking) || scratch > 0.01f || huntPlant
                   || sitTurn || walkTurn || walkStride;
        if (!any)
        {
            if (!idleVisualApplied)
                return;
            RestoreIdleVisuals();
            idleVisualApplied = false;
            return;
        }

        if (walkStride)
            ApplyWalkStride();
        else
            ApplyIdleVisuals(sniff, scratch, huntPlant, sitTurn ? hop : (walkTurn ? walkHop : 0f));
        idleVisualApplied = true;
    }

    // Cranial face of the head. FlipX on the head sprite does not move
    // children, so lids follow FacingSign along X. Sleep = hold shut.
    // Yawn peak = the same slits (DriveJaw already opened). Awake = a
    // short pulse; off-camera rest skips it. Forage Walk reuses a
    // thinner slit (WalkSquint) so follow pant is not a hunt-lock
    // stare; sit idlePant reuses that slit (SitSquint) so EndWalk
    // is not a round rest eye after the trot. Sit idleScratch reuses
    // that slit (SitScratchSquint) so the hind-paw kick is not a
    // round rest eye after the pupil already dropped. Sit idleLook
    // reuses that slit (SitLookSquint) so look-down is not a round
    // rest eye after the pupil already dropped, the ears already
    // hitch, and the whiskers already fan. Look-up stays open.
    // Hunt recover sit reuses that slit (SitHuntSquint) so the
    // 0.70 s coil is not a round rest eye after the pupil already
    // drops. Hunt stalk Walk reuses that slit (WalkHuntSquint) so
    // the chase is not a round rest eye after the pupil already
    // drops (WalkSquint skips idleHunt; SitHuntSquint skips Walk).
    // Hunt leap reuses that slit (LeapHuntSquint) so the pounce
    // is not a round rest eye after the brow already furrows
    // (WalkHuntSquint / SitHuntSquint skip IsLeap; LeapAttack
    // zeros idleHunt). Sit idleChew reuses that slit
    // (SitChewSquint) so the meal is not an open sniff-track
    // eye after the jaw / chest / rump / tail already snap
    // (SitSquint skips idleChew; sniff keeps the open track
    // eye). Blink still owns the full cover. Pupil stays visible under
    // the squint (ApplyEyes does not hide it). No idleYawnLids /
    // idleBlink: Walk() would zero them.
    private void ApplySleepLids(float sleep, float face)
    {
        if (sleepLids == null)
            return;

        bool sleepOn = sleep > 0.01f;
        bool yawnOn = stance.idleYawn > 0.45f;
        float now = Time.time;
        if (sleepOn || yawnOn)
        {
            blinkUntil = 0f;
            nextBlinkAt = now + 0.8f;
        }
        else if (stance != null && stance.energyRest)
        {
            blinkUntil = 0f;
        }
        else if (now >= nextBlinkAt)
        {
            blinkUntil = now + 0.12f;
            float u = Mathf.Abs(Mathf.Sin(now * 12.9898f + GetHashCode()));
            nextBlinkAt = now + 2.2f + u * 2.4f;
        }

        float squint = Mathf.Clamp01(stance.WalkSquint());
        float sit = Mathf.Clamp01(stance.SitSquint());
        if (sit > squint)
            squint = sit;
        float scratchLid = Mathf.Clamp01(stance.SitScratchSquint());
        if (scratchLid > squint)
            squint = scratchLid;
        float lookLid = Mathf.Clamp01(stance.SitLookSquint());
        if (lookLid > squint)
            squint = lookLid;
        float huntLid = Mathf.Clamp01(stance.SitHuntSquint());
        if (huntLid > squint)
            squint = huntLid;
        float stalkLid = Mathf.Clamp01(stance.WalkHuntSquint());
        if (stalkLid > squint)
            squint = stalkLid;
        float leapLid = Mathf.Clamp01(stance.LeapHuntSquint());
        if (leapLid > squint)
            squint = leapLid;
        float chewLid = Mathf.Clamp01(stance.SitChewSquint());
        if (chewLid > squint)
            squint = chewLid;
        bool blinkOn = now < blinkUntil;
        bool full = sleepOn || yawnOn || blinkOn;
        bool on = full || squint > 0.04f;
        if (!on && !sleepLidsOn)
            return;

        float dir = face >= 0f ? 1f : -1f;
        float x = 0.032f * dir;
        bool flip = dir < 0f;
        for (int i = 0; i < sleepLids.Length; i++)
        {
            SpriteRenderer lid = sleepLids[i];
            if (lid == null)
                continue;
            lid.enabled = on;
            if (!on)
                continue;
            float y = sleepLidY != null && i < sleepLidY.Length ? sleepLidY[i] : 0.012f;
            if (!full)
                y -= 0.003f * squint;
            lid.transform.localPosition = new Vector3(x, y, 0f);
            lid.flipX = flip;
            if (full)
            {
                lid.size = new Vector2(0.055f, 0.011f);
                Color c = SleepLidColor;
                if (i == 1)
                    c.a = 0.55f;
                lid.color = c;
            }
            else
            {
                lid.size = new Vector2(0.052f, 0.005f + 0.004f * squint);
                Color c = SleepLidColor;
                c.a = (i == 0 ? 0.48f : 0.30f) * squint;
                lid.color = c;
            }
        }

        sleepLidsOn = on;
    }

    // Cranial face of the head, under the lid slits. FlipX on the head
    // sprite does not move children. Hide when Sleep / yawn peak / blink
    // slits are on so they cover a pupil, not a blank box. Forage Walk
    // squint leaves the dot visible under a thinner slit (WalkSquint)
    // and a milder pant drop (WalkEye) so follow is not a rest stare
    // next to a dropped snout. Sit idlePant reuses that drop (SitEye)
    // so EndWalk is not a rest stare after the trot. Sit idleScratch
    // reuses that drop (SitScratchEye) so the hind-paw kick is not a
    // rest stare through the ear. Hunt leap reuses that drop
    // (LeapHuntEye) so the pounce is not a rest-Y stare after the
    // lid already slits and the brow already furrows (WalkEye /
    // SitEye skip IsLeap; LeapAttack zeros idleHunt). Sit idleChew
    // reuses a drop hitch (SitChewEye) so the meal is not a held
    // sniff drop after the jaw / chest / rump / tail / lid / ear /
    // brow / whisker / neck / nose / lip already snap. Look lifts
    // the dot; sniff / hunt drop it — same SPD floats, not neckP.
    private void ApplyEyes(float face)
    {
        if (eyes == null)
            return;

        bool shut = stance.idleSleep > 0.01f
                    || stance.idleYawn > 0.45f
                    || Time.time < blinkUntil;
        bool on = !shut;
        float dir = face >= 0f ? 1f : -1f;
        float y = 0.016f + 0.010f * Mathf.Clamp(stance.idleLook, -1f, 1f);
        float sniff = Mathf.Clamp01(stance.idleSniff);
        float chewE = Mathf.Clamp01(stance.SitChewEye());
        // Chew reuses idleSniff for the paw. Skip that held drop so
        // SitChewEye can own the meal stare (amp 0 keeps sniff).
        if (chewE > 0.01f)
            sniff = 0f;
        if (sniff > 0.01f)
            y -= 0.008f * sniff;
        if (chewE > 0.01f)
            y -= 0.008f * chewE;
        if (stance.idleHunt > 0.01f)
            y -= 0.005f * Mathf.Clamp01(stance.idleHunt);
        float leapE = Mathf.Clamp01(stance.LeapHuntEye());
        if (leapE > 0.01f)
            y -= 0.005f * leapE;
        float walkE = Mathf.Clamp01(stance.WalkEye());
        float sitE = Mathf.Clamp01(stance.SitEye());
        if (sitE > walkE)
            walkE = sitE;
        float scratchE = Mathf.Clamp01(stance.SitScratchEye());
        if (scratchE > walkE)
            walkE = scratchE;
        if (walkE > 0.01f)
            y -= 0.004f * walkE;

        if (on == eyesOn && dir == lastEyeDir && Mathf.Abs(y - lastEyeY) < 0.0005f)
            return;
        eyesOn = on;
        lastEyeDir = dir;
        lastEyeY = y;

        bool flip = dir < 0f;
        float x = 0.030f * dir;
        for (int i = 0; i < eyes.Length; i++)
        {
            SpriteRenderer eye = eyes[i];
            if (eye == null)
                continue;
            eye.enabled = on;
            if (!on)
                continue;
            float yi = i == 0 ? y : y - 0.010f;
            eye.transform.localPosition = new Vector3(x, yi, 0f);
            eye.flipX = flip;
        }
    }

    // Above the pupil on Head Visual. FlipX on the head sprite does not
    // move children. Always on: Sleep / blink hide the pupil, not the
    // brow. Same SPD floats as the eye, not neckP. Forage Walk reuses a
    // milder pant crease (WalkBrow) so follow is not a rest stamp next
    // to a hanging tongue. Sit idlePant reuses that crease (SitBrow)
    // so EndWalk is not a rest forehead after the trot. Sit
    // idleScratch reuses that crease (SitScratchBrow) so the hind-paw
    // kick is not a rest forehead after the pupil and lid already
    // moved. Sit idleYawn reuses a raise hitch (SitYawnBrow) so the
    // gape is not a rest forehead after the ears already fold and
    // the whiskers already fan. Sit idleChew reuses a crease
    // hitch (SitChewBrow) so the meal is not a statue sniff furrow
    // after the jaw / lid / ear already snap. Hunt furrow stays
    // on idleHunt. Night Sleep reuses a breath hitch (SitSleepBrow)
    // so the hold is not statue droop after the ears already hitch
    // and the snout already dropped (ApplyBrows held sleep*4).
    // Quiet Forage sit reuses a breath hitch (SitQuietBrow) so the
    // hold is not a rest stamp after chest / rump / neck / ears
    // already hitch (ApplyBrows rest pose). Hitch with the quiet
    // chest (same 2.5 clock as BreathLift / SitQuietRump /
    // SitQuietHead / SitQuietEar, in phase, droop ± a small lift,
    // not a 6.5 Hz pant and not WalkBrow). Quiet sit mouth owns
    // SitQuietMouth on ApplyMouth. Sleep mouth owns SitSleepMouth
    // on ApplyMouth. Sleep whiskers own SitSleepWhisker on
    // ApplyWhiskers. Quiet sit whiskers own SitQuietWhisker on
    // ApplyWhiskers.
    private void ApplyBrows(float face)
    {
        if (brows == null)
            return;

        float sniff = Mathf.Clamp01(stance.idleSniff);
        float hunt = Mathf.Clamp01(stance.idleHunt);
        if (stance.IsLeap())
            hunt = Mathf.Max(hunt, 0.85f);
        float sleep = Mathf.Clamp01(stance.idleSleep);
        float look = Mathf.Clamp(stance.idleLook, -1f, 1f);
        float walkB = Mathf.Clamp01(stance.WalkBrow());
        float sitB = Mathf.Clamp01(stance.SitBrow());
        if (sitB > walkB)
            walkB = sitB;
        float scratchB = Mathf.Clamp01(stance.SitScratchBrow());
        if (scratchB > walkB)
            walkB = scratchB;
        float chewB = Mathf.Clamp01(stance.SitChewBrow());
        if (chewB > walkB)
            walkB = chewB;
        float yawnB = Mathf.Clamp01(stance.SitYawnBrow());
        float sleepB = Mathf.Clamp01(stance.SitSleepBrow());
        float quietB = Mathf.Clamp01(stance.SitQuietBrow());
        float hitch = 0f;
        if (sleepB > 0.01f)
        {
            float t = Time.time + breathPhase;
            hitch = sleepB * Mathf.Sin(t * 1.6f);
        }
        else if (quietB > 0.01f)
        {
            float t = Time.time + breathPhase;
            hitch = quietB * Mathf.Sin(t * 2.5f);
        }
        float pose = sniff + hunt * 2f + sleep * 4f + look * 0.1f + walkB * 3f
                     + yawnB * 3f + hitch;
        float dir = face >= 0f ? 1f : -1f;
        if (dir == lastBrowDir && Mathf.Abs(pose - lastBrowPose) < 0.004f)
            return;
        lastBrowDir = dir;
        lastBrowPose = pose;

        float y0 = 0.032f + 0.005f * look - 0.004f * sniff - 0.007f * hunt
                   - 0.003f * sleep - 0.004f * walkB + 0.004f * yawnB
                   + 0.0015f * hitch;
        float ang = 8f + 6f * look - 4f * sniff - 18f * hunt + 10f * sleep
                    - 10f * walkB + 14f * yawnB - 2f * hitch;
        bool flip = dir < 0f;
        for (int i = 0; i < brows.Length; i++)
        {
            SpriteRenderer brow = brows[i];
            if (brow == null)
                continue;
            float yi = i == 0 ? y0 : y0 - 0.008f;
            float len = (i == 0 ? 0.028f : 0.022f)
                        + 0.004f * look - 0.002f * sniff + 0.002f * hunt
                        + 0.003f * walkB + 0.003f * yawnB;
            float thick = i == 0 ? 0.005f : 0.004f;
            thick += 0.002f * hunt + 0.0015f * walkB;
            Quaternion rot = Quaternion.Euler(0f, 0f, ang * dir);
            Vector3 origin = new Vector3(0.026f * dir, yi, 0f);
            brow.transform.localPosition = origin + rot * Vector3.right * (len * 0.5f * dir);
            brow.transform.localRotation = rot;
            brow.flipX = flip;
            brow.size = new Vector2(len, thick);
        }
    }

    // Cranial snout of the head. FlipX on the head sprite does not move
    // children. Always on: Sleep / blink hide the pupil, not the muzzle.
    // Same SPD floats as the eye, not neckP. Forage Walk reuses a
    // milder pant drop (WalkNose) so follow is not a rest stamp next
    // to a hanging tongue. Sit idlePant reuses that drop (SitNose) so
    // EndWalk is not a rest muzzle after the trot. Sit idleScratch
    // reuses that drop (SitScratchNose) so the hind-paw kick is not
    // a rest muzzle after the pupil, lid, brow, and whiskers already
    // moved. Sit idleYawn reuses a lift hitch (SitYawnNose) so the
    // gape is not a rest muzzle after the brow already raises. Hunt
    // leap reuses that drop (LeapHuntNose) so the pounce is not a
    // rest muzzle after the pupil already drops and the brow/lips
    // already furrow (WalkNose/SitNose skip IsLeap; LeapAttack
    // zeros idleHunt). Sit idleChew reuses a drop hitch
    // (SitChewNose) so the meal is not a held sniff drop after
    // the jaw / chest / rump / tail / lid / ear / brow / whisker /
    // neck already snap. Sit chew lip is SitChewMouth. Hunt drop
    // stays on idleHunt. Night Sleep reuses that drop (SitSleepNose)
    // so the hold is not a rest forage muzzle after the lip /
    // whisker / brow already droop and the chest / rump already
    // hitch (ApplyNose never read idleSleep; lids own the pupil,
    // not the snout).
    private void ApplyNose(float face)
    {
        if (nose == null)
            return;
        float dir = face >= 0f ? 1f : -1f;
        float sniff = Mathf.Clamp01(stance.idleSniff);
        float chewN = Mathf.Clamp01(stance.SitChewNose());
        // Chew reuses idleSniff for the paw. Skip that held drop so
        // SitChewNose can own the meal muzzle (amp 0 keeps sniff).
        if (chewN > 0.01f)
            sniff = 0f;
        float y = -0.010f + 0.006f * Mathf.Clamp(stance.idleLook, -1f, 1f);
        if (sniff > 0.01f)
            y -= 0.007f * sniff;
        if (chewN > 0.01f)
            y -= 0.007f * chewN;
        if (stance.idleHunt > 0.01f)
            y -= 0.004f * Mathf.Clamp01(stance.idleHunt);
        float leapN = Mathf.Clamp01(stance.LeapHuntNose());
        if (leapN > 0.01f)
            y -= 0.004f * leapN;
        float walkN = Mathf.Clamp01(stance.WalkNose());
        float sitN = Mathf.Clamp01(stance.SitNose());
        if (sitN > walkN)
            walkN = sitN;
        float scratchN = Mathf.Clamp01(stance.SitScratchNose());
        if (scratchN > walkN)
            walkN = scratchN;
        if (walkN > 0.01f)
            y -= 0.005f * walkN;
        float yawnN = Mathf.Clamp01(stance.SitYawnNose());
        if (yawnN > 0.01f)
            y += 0.005f * yawnN;
        float sleepN = Mathf.Clamp01(stance.SitSleepNose());
        if (sleepN > 0.01f)
            y -= 0.005f * sleepN;

        if (dir == lastNoseDir && Mathf.Abs(y - lastNoseY) < 0.0005f)
            return;
        lastNoseDir = dir;
        lastNoseY = y;
        nose.transform.localPosition = new Vector3(0.058f * dir, y, 0f);
        nose.flipX = dir < 0f;
    }

    // Cranial snout, caudal of the nose. FlipX on the head sprite does
    // not move children. Always on: Sleep / blink hide the pupil, not
    // the muzzle. Same SPD floats as the nose, not neckP. Forage Walk
    // reuses a milder fan (WalkWhisker) so follow is not a rest stamp
    // next to a hanging tongue. Sit idlePant reuses that fan
    // (SitWhisker) so EndWalk is not rest stamps after the trot.
    // Sit idleScratch reuses that fan (SitScratchWhisker) so the
    // hind-paw kick is not rest stamps after the pupil, lid, and
    // brow already moved. Sit idleLook reuses a signed fan
    // (SitLookWhisker) so the neck bob is not rest stamps after
    // the ears already hitch. Sit idleYawn reuses a fold fan
    // (SitYawnWhisker) so the gape is not rest stamps after the
    // ears already fold. Sit idleChew reuses a fan hitch
    // (SitChewWhisker) so the meal is not a sniff fan+11 Hz after
    // the jaw / lid / ear / brow already snap. Quiet Forage sit
    // reuses a breath hitch (SitQuietWhisker) so the hold is not
    // rest stamps after chest / rump / neck / ears / brows /
    // flews already hitch (ApplyWhiskers rest pose). Hitch with
    // the quiet chest (same 2.5 clock as BreathLift / SitQuietRump
    // / SitQuietHead / SitQuietEar / SitQuietBrow / SitQuietMouth,
    // in phase, droop ± a small lift, not a 6.5 Hz pant and not
    // WalkWhisker). Night Sleep reuses a breath hitch
    // (SitSleepWhisker) so the hold is not statue droop after the
    // flews already hitch (ApplyWhiskers held sleep*4). Hitch with
    // the sleep breath (same 1.6 clock as BreathLift / SitSleepRump
    // / SitSleepEar / SitSleepBrow / SitSleepMouth, in phase, droop
    // ± a small lift). Hunt pin stays on idleHunt.
    private void ApplyWhiskers(float face)
    {
        if (whiskers == null)
            return;

        float sniff = Mathf.Clamp01(stance.idleSniff);
        float hunt = Mathf.Clamp01(stance.idleHunt);
        if (stance.IsLeap())
            hunt = Mathf.Max(hunt, 0.85f);
        float sleep = Mathf.Clamp01(stance.idleSleep);
        float look = Mathf.Clamp(stance.idleLook, -1f, 1f);
        float walkW = Mathf.Clamp01(stance.WalkWhisker());
        float sitW = Mathf.Clamp01(stance.SitWhisker());
        float scratchW = Mathf.Clamp01(stance.SitScratchWhisker());
        float lookW = stance.SitLookWhisker();
        float yawnW = Mathf.Clamp01(stance.SitYawnWhisker());
        float chewW = Mathf.Clamp01(stance.SitChewWhisker());
        float sleepW = Mathf.Clamp01(stance.SitSleepWhisker());
        float quietW = Mathf.Clamp01(stance.SitQuietWhisker());
        float hitch = 0f;
        if (sleepW > 0.01f)
        {
            float t = Time.time + breathPhase;
            hitch = sleepW * Mathf.Sin(t * 1.6f);
        }
        else if (quietW > 0.01f)
        {
            float t = Time.time + breathPhase;
            hitch = quietW * Mathf.Sin(t * 2.5f);
        }
        // Chew reuses idleSniff for the paw. Skip that fan+11 Hz so
        // SitChewWhisker can own the meal muzzle (amp 0 keeps sniff).
        if (chewW > 0.01f)
            sniff = 0f;
        float twitch = 0f;
        if (sniff > 0.01f)
            twitch = Mathf.Sin(Time.time * 11f);
        else if (scratchW > 0.01f)
            twitch = Mathf.Sin(Time.time * 16f);
        else if (sitW > 0.01f)
            twitch = Mathf.Sin(stance.idlePant * Mathf.PI);
        else if (walkW > 0.01f)
            twitch = Mathf.Sin(stance.walkAnimPhase * 4f * Mathf.PI);
        if (sitW > walkW)
            walkW = sitW;
        if (scratchW > walkW)
            walkW = scratchW;
        if (chewW > walkW)
            walkW = chewW;
        float pose = sniff + hunt * 2f + sleep * 4f + look * 0.1f
                     + twitch * 0.15f * sniff + walkW * 3f + twitch * 0.15f * walkW
                     + lookW * 3f + yawnW * 3f + hitch;
        float dir = face >= 0f ? 1f : -1f;
        if (dir == lastWhiskerDir && Mathf.Abs(pose - lastWhiskerPose) < 0.004f)
            return;
        lastWhiskerDir = dir;
        lastWhiskerPose = pose;

        float y0 = -0.008f + 0.004f * look - 0.005f * sniff - 0.003f * hunt
                    - 0.0015f * hitch;
        bool flip = dir < 0f;
        float lookAbs = Mathf.Abs(lookW);
        float lookBlend = Mathf.Clamp01(lookAbs);
        for (int i = 0; i < whiskers.Length; i++)
        {
            SpriteRenderer w = whiskers[i];
            if (w == null)
                continue;
            float restAng = (i - 1) * 14f + 2f * hitch;
            float sniffAng = (i - 1) * 28f + 6f * twitch;
            float walkAng = restAng + (i - 1) * 10f + 5f * twitch;
            float huntAng = (i - 1) * 6f + 48f;
            float sleepAng = (i - 1) * 10f + 30f + 2f * hitch;
            float lookAng = restAng + (i - 1) * 10f * lookW;
            float yawnAng = restAng + (i - 1) * 16f;
            float ang = restAng;
            if (lookAbs > 0.01f)
                ang = Mathf.Lerp(ang, lookAng, lookBlend);
            if (yawnW > 0.01f)
                ang = Mathf.Lerp(ang, yawnAng, yawnW);
            if (walkW > 0.01f)
                ang = Mathf.Lerp(ang, walkAng, walkW);
            if (sniff > 0.01f)
                ang = Mathf.Lerp(ang, sniffAng, sniff);
            if (hunt > 0.01f)
                ang = Mathf.Lerp(ang, huntAng, hunt);
            if (sleep > 0.01f)
                ang = Mathf.Lerp(ang, sleepAng, sleep);
            float len = 0.028f + 0.014f * sniff - 0.008f * hunt - 0.004f * sleep
                        + 0.008f * walkW + 0.006f * lookW + 0.007f * yawnW;
            float yi = y0 + (i - 1) * 0.006f;
            Quaternion rot = Quaternion.Euler(0f, 0f, ang * dir);
            // Center sits half-length along the slice so the fan grows
            // from the snout, not around the bar midpoint.
            Vector3 origin = new Vector3(0.046f * dir, yi, 0f);
            w.transform.localPosition = origin + rot * Vector3.right * (len * 0.5f * dir);
            w.transform.localRotation = rot;
            w.flipX = flip;
            w.size = new Vector2(len, 0.0045f);
        }
    }

    // Ventral of the nose on Head Visual. White rectangle + tint, no PNG.
    // Whiskers without a lip still read as stamps on a yellow box. Hunt
    // raises the flews so Jaw fangs are a snarl, not floating cream.
    // Pant / yawn / chew open with DriveJaw. Sleep droops. Forage Walk
    // reuses the pant gape (WalkPant) so follow is not closed-stiffer
    // than hunt. Sit idleScratch reuses a sniff-like tuck
    // (SitScratchMouth) so the hind-paw kick is not rest flews after
    // the snout already dropped. Sit idleLook reuses a signed hitch
    // (SitLookMouth) so the neck bob is not rest flews after the
    // pupil already dropped, the ears already hitched, the whiskers
    // already fanned, and the lids already slit. Look-up lifts with
    // the nose; look-down tucks. Sit idleYawn reuses a lift hitch
    // (SitYawnMouth) so the gape is not rest flews after the snout
    // already lifts. Stretch, not a hunt raise. Hunt raise stays
    // on idleHunt. Sit idleChew reuses a tuck hitch (SitChewMouth)
    // so the meal is not a held sniff tuck after the jaw / chest /
    // rump / tail / lid / ear / brow / whisker / neck / nose
    // already snap. Quiet Forage sit reuses a breath hitch
    // (SitQuietMouth) so the hold is not rest flews after chest /
    // rump / neck / ears / brows already hitch (ApplyMouth rest
    // pose). Hitch with the quiet chest (same 2.5 clock as
    // BreathLift / SitQuietRump / SitQuietHead / SitQuietEar /
    // SitQuietBrow, in phase, droop ± a small lift, not a 6.5 Hz
    // pant and not SitLookMouth / SitYawnMouth). Night Sleep
    // reuses a breath hitch (SitSleepMouth) so the hold is not
    // statue droop after the brows already hitch (ApplyMouth
    // held sleep*4). Hitch with the sleep breath (same 1.6 clock
    // as BreathLift / SitSleepRump / SitSleepEar / SitSleepBrow,
    // in phase, droop ± a small lift). Night Sleep whiskers own
    // SitSleepWhisker on ApplyWhiskers. Quiet sit
    // whiskers own SitQuietWhisker on ApplyWhiskers. Always on:
    // lids own the eye, not the muzzle. No idleMouth: Walk() would
    // zero it.
    private void ApplyMouth(float face)
    {
        if (mouth == null)
            return;

        float sniff = Mathf.Clamp01(stance.idleSniff);
        float chewM = Mathf.Clamp01(stance.SitChewMouth());
        // Chew reuses idleSniff for the paw. Skip that held tuck so
        // SitChewMouth can own the meal flews (amp 0 keeps sniff).
        if (chewM > 0.01f)
            sniff = 0f;
        float hunt = Mathf.Clamp01(stance.idleHunt);
        if (stance.IsLeap())
            hunt = Mathf.Max(hunt, 0.85f);
        float sleep = Mathf.Clamp01(stance.idleSleep);
        float look = Mathf.Clamp(stance.idleLook, -1f, 1f);
        float pant = Mathf.Clamp01(stance.idlePant);
        float walkPant = Mathf.Clamp01(stance.WalkPant());
        if (walkPant > pant)
            pant = walkPant;
        float yawn = Mathf.Clamp01(stance.idleYawn);
        float chew = Mathf.Clamp01(stance.idleChew);
        float scratchM = Mathf.Clamp01(stance.SitScratchMouth());
        float lookM = stance.SitLookMouth();
        float yawnM = Mathf.Clamp01(stance.SitYawnMouth());
        float sleepM = Mathf.Clamp01(stance.SitSleepMouth());
        float quietM = Mathf.Clamp01(stance.SitQuietMouth());
        float hitch = 0f;
        if (sleepM > 0.01f)
        {
            float t = Time.time + breathPhase;
            hitch = sleepM * Mathf.Sin(t * 1.6f);
        }
        else if (quietM > 0.01f)
        {
            float t = Time.time + breathPhase;
            hitch = quietM * Mathf.Sin(t * 2.5f);
        }
        float gape = BarkGape();
        float open = pant * 0.45f;
        if (yawn * 0.70f > open)
            open = yawn * 0.70f;
        if (gape * 0.80f > open)
            open = gape * 0.80f;
        if (chew * 0.22f > open)
            open = chew * 0.22f;
        float chewTick = chew > 0.01f ? Mathf.Sin(Time.time * 11f) : 0f;
        float pose = sniff + hunt * 2f + sleep * 4f + look * 0.1f + open * 3f
                     + chewTick * 0.2f * chew + scratchM + lookM + yawnM * 3f
                     + chewM + hitch;
        float dir = face >= 0f ? 1f : -1f;
        if (dir == lastMouthDir && Mathf.Abs(pose - lastMouthPose) < 0.004f)
            return;
        lastMouthDir = dir;
        lastMouthPose = pose;

        float y = -0.028f + 0.005f * look - 0.006f * sniff + 0.006f * hunt
                  - 0.004f * sleep - 0.008f * open + 0.003f * chewTick * chew
                  - 0.005f * scratchM + 0.005f * lookM + 0.005f * yawnM
                  - 0.006f * chewM - 0.0015f * hitch;
        // +Z is CCW: a bar grown cranial lifts the flews on hunt.
        // Scratch tucks caudal like sniff, not a snarl raise.
        // Sit look is signed: up follows the nose, down tucks.
        // Sit yawn lifts with the snout (stretch), not a hunt raise.
        // Sit chew tucks with the snap (same 0.006 / 6° as sniff).
        // Quiet sit droops ± a small lift with the 2.5 chest clock.
        // Sleep droops ± a small lift with the 1.6 sleep clock.
        float ang = 18f * hunt - 12f * sleep - 6f * sniff + 4f * look - 8f * open
                    - 5f * scratchM + 5f * lookM + 5f * yawnM - 6f * chewM
                    - 2f * hitch;
        float len = 0.038f + 0.012f * open - 0.006f * hunt;
        float thick = 0.007f + 0.005f * open + 0.003f * hunt;
        Quaternion rot = Quaternion.Euler(0f, 0f, ang * dir);
        Vector3 origin = new Vector3(0.032f * dir, y, 0f);
        mouth.transform.localPosition = origin + rot * Vector3.right * (len * 0.5f * dir);
        mouth.transform.localRotation = rot;
        mouth.flipX = dir < 0f;
        mouth.size = new Vector2(len, thick);
    }

    // Cranial of the jaw. FlipX on the jaw sprite does not move children.
    private void ApplyBarkPuff(float face)
    {
        if (barkPuff == null)
            return;
        float gape = BarkGape();
        bool on = gape > 0.01f;
        if (!on && !barkPuffOn)
            return;
        barkPuff.enabled = on;
        barkPuffOn = on;
        if (!on)
            return;

        float dir = face >= 0f ? 1f : -1f;
        barkPuff.transform.localPosition = new Vector3(0.062f * dir, 0.012f, 0f);
        barkPuff.flipX = dir < 0f;
        barkPuff.size = new Vector2(0.026f + 0.024f * gape, 0.016f + 0.018f * gape);
        Color c = BarkPuffColor;
        c.a = 0.22f + 0.63f * gape;
        barkPuff.color = c;
    }

    // Cranial of the jaw, below the teeth. FlipX on the jaw sprite does
    // not move children. Hide during Bark puff / chew so Call stays a
    // puff and the morsel owns the slot. Pant = hanging strip; yawn =
    // wider curl on the same sprite. Forage Walk reuses this strip
    // (WalkPant) so follow is not a closed mouth stiffer than hunt.
    private void ApplyTongue(float face)
    {
        if (tongue == null)
            return;
        float pant = stance.idlePant;
        float walkPant = stance.WalkPant();
        if (walkPant > pant)
            pant = walkPant;
        float yawn = stance.idleYawn;
        bool yawning = yawn > 0.20f;
        bool on = (pant > 0.01f || yawning) && BarkGape() < 0.15f
                  && stance.idleChew < 0.08f;
        if (!on && !tongueOn)
            return;
        tongue.enabled = on;
        tongueOn = on;
        if (!on)
            return;

        float dir = face >= 0f ? 1f : -1f;
        float u = yawning ? yawn : pant;
        float reach = yawning ? 0.038f + 0.028f * yawn : 0.032f + 0.022f * pant;
        float thick = yawning ? 0.014f + 0.012f * yawn : 0.011f + 0.008f * pant;
        float y = yawning ? -0.012f - 0.010f * yawn : -0.010f - 0.006f * pant;
        tongue.transform.localPosition = new Vector3(0.048f * dir, y, 0f);
        tongue.flipX = dir < 0f;
        tongue.size = new Vector2(reach, thick);
        Color c = TongueColor;
        c.a = 0.55f + 0.37f * u;
        tongue.color = c;
    }

    // Cranial of the hanging tongue. FlipX on the jaw sprite does not
    // move children. Bark puff owns Call; chew puff sits cranial of
    // the morsel so the teeth slot stays food. Forage Walk pulses a
    // cooler sliver (WalkPuff) so pant is breath, not a silent pink
    // stamp. Sit idlePant reuses that sliver (SitPuff) so EndWalk
    // is not a silent tongue after the trot. Sit idleYawn reuses
    // that sliver (SitYawnPuff) so the gape is not a silent stretch
    // after the tongue already curls. Sit idleChew reuses that
    // sliver (SitChewPuff) so the meal is not a silent lump after
    // the jaw / morsel already snap. Hunt recover sit reuses that
    // sliver (SitHuntPuff) so the 0.70 s coil is not a silent
    // crack after the snarl / chest / rump / neck / lid already
    // live (SitPuff / SitYawnPuff / SitChewPuff skip idleHunt;
    // fangs own the teeth slot). Hunt stalk Walk reuses that
    // sliver (WalkHuntPuff) so the chase is not a silent crack
    // after the lid / pupil / nose already coil (WalkPuff skips
    // idleHunt; SitHuntPuff skips Walk). Hunt leap reuses that
    // sliver (LeapHuntPuff) so the pounce is not a silent crack
    // after the lid / pupil / nose already coil (WalkHuntPuff /
    // SitHuntPuff skip IsLeap; LeapAttack zeros idleHunt; fangs
    // own the teeth slot). No idlePuff: Walk() / LeapAttack would
    // zero it.
    private void ApplyPantPuff(float face)
    {
        if (pantPuff == null)
            return;
        float puff = Mathf.Clamp01(stance.WalkPuff());
        float sit = Mathf.Clamp01(stance.SitPuff());
        if (sit > puff)
            puff = sit;
        float yawnP = Mathf.Clamp01(stance.SitYawnPuff());
        if (yawnP > puff)
            puff = yawnP;
        float chewP = Mathf.Clamp01(stance.SitChewPuff());
        if (chewP > puff)
            puff = chewP;
        float huntP = Mathf.Clamp01(stance.SitHuntPuff());
        float stalkP = Mathf.Clamp01(stance.WalkHuntPuff());
        if (stalkP > huntP)
            huntP = stalkP;
        float leapP = Mathf.Clamp01(stance.LeapHuntPuff());
        if (leapP > huntP)
            huntP = leapP;
        if (huntP > puff)
            puff = huntP;
        bool on = puff > 0.04f && BarkGape() < 0.15f;
        if (!on && !pantPuffOn)
            return;

        float dir = face >= 0f ? 1f : -1f;
        if (!on)
        {
            pantPuff.enabled = false;
            pantPuffOn = false;
            lastPantPuffAmt = float.NaN;
            lastPantPuffChew = false;
            lastPantPuffHunt = false;
            return;
        }

        bool chewPuff = chewP > 0.04f;
        bool huntPuff = huntP > 0.04f;
        if (pantPuffOn && dir == lastPantPuffDir
            && Mathf.Abs(puff - lastPantPuffAmt) < 0.01f
            && chewPuff == lastPantPuffChew
            && huntPuff == lastPantPuffHunt)
            return;
        lastPantPuffDir = dir;
        lastPantPuffAmt = puff;
        lastPantPuffChew = chewPuff;
        lastPantPuffHunt = huntPuff;
        pantPuffOn = true;
        pantPuff.enabled = true;
        // Chew: sit the sliver cranial of the morsel so food keeps
        // the teeth slot (morsel ~0.050, width ~0.028). Hunt
        // recover / stalk / leap: cranial of the near fang (~0.058)
        // so the snarl keeps the teeth slot.
        float x0 = chewPuff ? 0.078f : (huntPuff ? 0.082f : 0.068f);
        float y0 = chewPuff ? -0.004f : (huntPuff ? -0.002f : -0.006f);
        pantPuff.transform.localPosition = new Vector3(
            (x0 + 0.012f * puff) * dir,
            y0 - 0.004f * puff,
            0f);
        pantPuff.flipX = dir < 0f;
        pantPuff.size = new Vector2(0.014f + 0.016f * puff, 0.008f + 0.008f * puff);
        Color c = PantPuffColor;
        c.a = 0.12f + 0.30f * puff;
        pantPuff.color = c;
    }

    // Cranial of the jaw, at the teeth. Not the tongue: chew closes,
    // pant hangs open. FlipX on the jaw sprite does not move children.
    private void ApplyChewMorsel(float face)
    {
        if (chewMorsel == null)
            return;
        float chew = stance.idleChew;
        bool on = chew > 0.12f && BarkGape() < 0.15f;
        if (!on && !chewMorselOn)
            return;
        chewMorsel.enabled = on;
        chewMorselOn = on;
        if (!on)
            return;

        float dir = face >= 0f ? 1f : -1f;
        chewMorsel.transform.localPosition = new Vector3(0.050f * dir, -0.002f - 0.004f * chew, 0f);
        chewMorsel.flipX = dir < 0f;
        chewMorsel.size = new Vector2(0.016f + 0.012f * chew, 0.012f + 0.008f * chew);
        Color c = ChewMorselColor;
        c.a = 0.45f + 0.47f * chew;
        chewMorsel.color = c;
    }

    // Canines on the cranial jaw. FlipX on the jaw sprite does not move
    // children. Bark puff / chew / pant own that slot; snarl yields.
    private void ApplySnarl(float face)
    {
        if (snarlTeeth == null)
            return;
        float n = SnarlAmount();
        bool on = n > 0.04f && BarkGape() < 0.15f
                  && stance.idleChew < 0.08f
                  && stance.idlePant < 0.08f
                  && stance.WalkPant() < 0.08f;
        if (!on && !snarlOn)
            return;
        snarlOn = on;
        float dir = face >= 0f ? 1f : -1f;
        bool flip = dir < 0f;
        for (int i = 0; i < snarlTeeth.Length; i++)
        {
            SpriteRenderer tooth = snarlTeeth[i];
            if (tooth == null)
                continue;
            tooth.enabled = on;
            if (!on)
                continue;
            float x = (i == 0 ? 0.058f : 0.050f) * dir;
            float y = i == 0 ? -0.008f - 0.006f * n : -0.006f - 0.005f * n;
            tooth.transform.localPosition = new Vector3(x, y, 0f);
            tooth.flipX = flip;
            float w = i == 0 ? 0.009f + 0.006f * n : 0.007f + 0.005f * n;
            float h = i == 0 ? 0.014f + 0.016f * n : 0.012f + 0.012f * n;
            tooth.size = new Vector2(w, h);
        }
    }

    // Brain already wrote idleHunt; leap is the body of Pursue.
    // No idleSnarl: Walk() would zero it on the first chase step.
    private float SnarlAmount()
    {
        float n = 0f;
        if (stance.idleHunt > 0.01f)
            n = 0.42f * Mathf.Clamp01(stance.idleHunt);
        if (!stance.IsLeap())
            return n;
        DogStanceController.StanceState st = stance.currentState;
        if (st == DogStanceController.StanceState.LeapBite)
            return 1f;
        if (st == DogStanceController.StanceState.LeapAir)
            return 0.82f;
        return Mathf.Max(n, 0.58f);
    }

    // Dorsal Chest Visual. FlipX on the chest sprite does not move
    // children. Collider stays. Alert stare is idleLook — ridge stays
    // down until stalk / leap / recover write idleHunt or IsLeap.
    private void ApplyHackles(float face)
    {
        if (hackles == null)
            return;

        float want = HackleWanted();
        hackleRaise += (want - hackleRaise) * Mathf.Clamp01(Time.deltaTime * 8f);
        bool on = hackleRaise > 0.04f;
        if (!on && !hacklesOn)
            return;

        float dir = face >= 0f ? 1f : -1f;
        if (!on)
        {
            for (int i = 0; i < hackles.Length; i++)
            {
                if (hackles[i] != null)
                    hackles[i].enabled = false;
            }
            hacklesOn = false;
            lastHackleRaise = -1f;
            return;
        }

        if (hacklesOn && Mathf.Abs(hackleRaise - lastHackleRaise) < 0.002f && dir == lastHackleDir)
            return;
        lastHackleRaise = hackleRaise;
        lastHackleDir = dir;
        hacklesOn = true;

        float raise = hackleRaise;
        float rotZ = Mathf.Lerp(52f, -6f, raise) * dir;
        Quaternion rot = Quaternion.Euler(0f, 0f, rotZ);
        bool flip = dir < 0f;
        for (int i = 0; i < hackles.Length; i++)
        {
            SpriteRenderer spike = hackles[i];
            if (spike == null)
                continue;
            spike.enabled = true;
            float x0 = i == 0 ? -0.08f : (i == 1 ? 0.00f : 0.10f);
            float h0 = i == 1 ? 0.018f : 0.014f;
            float h1 = i == 1 ? 0.036f : 0.028f;
            float w = i == 1 ? 0.016f : 0.013f;
            spike.transform.localPosition = new Vector3(x0 * dir, 0.062f + 0.010f * raise, 0f);
            spike.transform.localRotation = rot;
            spike.flipX = flip;
            spike.size = new Vector2(w, h0 + (h1 - h0) * raise);
        }
    }

    // Brain already wrote idleHunt; leap is the body of Pursue.
    private float HackleWanted()
    {
        if (stance.energyRest)
            return 0f;
        if (stance.idleSleep > 0.01f)
            return 0f;
        if (stance.idleChew > 0.01f || stance.idlePant > 0.01f)
            return 0f;
        if (stance.IsLeap())
            return 1f;
        if (stance.idleHunt > 0.01f)
            return 0.78f * Mathf.Clamp01(stance.idleHunt);
        return 0f;
    }

    // Quiet sit / Sleep / pant heave on Chest Visual. Collider stays.
    // Forage trot reuses the same child: WalkTrunkBob on the head-bob
    // clock so leftover sit is not a statue withers. Sit idleYawn
    // reuses that heave (SitYawnBreath) so the gape is not a
    // quiet-sit 0.0018 wobble after the jaw already opens. Sit
    // idleSniff reuses that heave (SitSniffBreath) so the dirt
    // track is not a quiet-sit 0.0018 wobble after the nose already
    // tracks. Sit idleScratch reuses that heave (SitScratchBreath)
    // so the hind-paw kick is not a quiet-sit 0.0018 wobble after
    // the rump already hitches. Sit idleLook reuses that heave
    // (SitLookBreath) so the neck bob is not a quiet-sit 0.0018
    // wobble after the face and tail already weave. Hunt recover
    // sit reuses that heave (SitHuntBreath) so the 0.70 s coil is
    // not a quiet-sit 0.0018 wobble after the snarl and paw already
    // live. Sit idleChew reuses that heave (SitChewBreath) so the
    // meal is not a quiet-sit 0.0018 wobble after the jaw already
    // snaps and the paw already shuffles. Hunt leap reuses that
    // heave (LeapHuntBreath) so the pounce is not a statue chest
    // after the lid / pupil / nose / puff already coil
    // (BreathLift / SitHuntBreath skip IsLeap; LeapAttack zeros
    // idleHunt). No idleBreath: Walk() / LeapAttack would zero
    // it. Hunt stalk Walk / CLI walk / rest restore.
    private void ApplyBreath()
    {
        if (chestSegment == null || chestSegment.visual == null)
            return;

        float lift = BreathLift();
        if (Mathf.Abs(lift) <= 0.0004f && !breathOn)
            return;

        Transform vis = chestSegment.visual;
        vis.localPosition = new Vector3(
            chestSegment.visualOffsetX,
            chestSegment.visualOffsetY + lift,
            0f);
        breathOn = Mathf.Abs(lift) > 0.0004f;
    }

    // Quiet Forage sit hitches the pelvis with the 0.0018 chest. Forage trot heaves Pelvis Visual
    // on the same two-dip clock as withers so rear swing is not a
    // statue rump. Sit idlePant reuses that heave (SitRump) so
    // EndWalk is not a statue rump after the trot already bobbed.
    // Sit idleYawn reuses that heave (SitYawnRump) so the gape is
    // not a chest-only inhale after SitYawnBreath already lifted
    // the withers. Sit idleSniff reuses that heave (SitSniffRump)
    // so the dirt track is not a chest-only bob after
    // SitSniffBreath already lifted the withers. Sit idleScratch
    // reuses that heave (SitScratchRump) so the hind-paw kick is
    // not a statue rump after the face and tail already weave.
    // Sit idleLook reuses that heave (SitLookRump) so the neck bob
    // is not a chest-only hitch after SitLookBreath already lifted
    // the withers. Hunt recover sit reuses that heave (SitHuntRump)
    // so the 0.70 s coil is not a chest-only hitch after
    // SitHuntBreath already lifted the withers. Sit idleChew
    // reuses that heave (SitChewRump) so the meal is not a
    // chest-only hitch after SitChewBreath already lifted the
    // withers. Hunt leap reuses that heave (LeapHuntRump) so
    // the pounce is not a chest-only hitch after LeapHuntBreath
    // already lifted the withers (ApplyRump / SitHuntRump skip
    // IsLeap; LeapAttack zeros idleHunt). Night Sleep reuses that
    // heave (SitSleepRump) so the hold is not a chest-only hitch
    // after BreathLift already lifted the withers (ApplyRump
    // skipped idleSleep; TickSleep zeros idleWag). Hitch with the
    // sleep breath (same 1.6 clock as BreathLift, in phase, not a
    // see-saw). Quiet Forage sit reuses that heave (SitQuietRump)
    // so the hold is not a chest-only hitch after BreathLift already
    // lifted the withers (ApplyRump skipped the quiet 0.0018 path).
    // Hitch with the quiet chest (same 2.5 clock as BreathLift, in
    // phase, not a see-saw). Quiet sit neck is SitQuietHead on that
    // same clock (IdleNeckWorldTarget, not this Visual). Quiet sit
    // brows are SitQuietBrow on ApplyBrows, not this Visual. Quiet
    // sit mouth is SitQuietMouth on ApplyMouth, not this Visual.
    // Sleep mouth is SitSleepMouth on ApplyMouth, not this Visual.
    // Sleep whiskers are SitSleepWhisker on ApplyWhiskers, not
    // this Visual. Sleep / sit-sniff / sit-yawn / sit-pant /
    // sit-chew / sit-look / sit-scratch / quiet-sit distal Tail2–Tail5 are
    // SitSleepDistalTail / SitSniffDistalTail /
    // SitYawnDistalTail / SitPantDistalTail / SitChewDistalTail /
    // SitLookDistalTail / SitScratchDistalTail /
    // SitQuietDistalTail / SitHuntDistalTail /
    // WalkHuntDistalTail / WalkDistalTail / LeapHuntDistalTail
    // on ApplyDistalTail,
    // not this Visual. Quiet
    // sit whiskers are SitQuietWhisker on ApplyWhiskers, not this
    // Visual.
    // Collider stays.
    // No idleRump: Walk() would zero it. Hunt stalk / CLI walk /
    // rest restore.
    private void ApplyRump()
    {
        if (pelvisSegment == null || pelvisSegment.visual == null)
            return;

        float lift = 0.005f * stance.WalkRumpBob();
        float sit = 0.004f * stance.SitRump();
        if (sit > Mathf.Abs(lift))
            lift = sit;
        float yawn = 0.005f * stance.SitYawnRump();
        if (yawn > Mathf.Abs(lift))
            lift = yawn;
        float chewRump = 0.004f * stance.SitChewRump();
        if (chewRump > Mathf.Abs(lift))
            lift = chewRump;
        float sniff = 0.004f * stance.SitSniffRump();
        if (sniff > Mathf.Abs(lift))
            lift = sniff;
        float scratchRump = 0.004f * stance.SitScratchRump();
        if (scratchRump > Mathf.Abs(lift))
            lift = scratchRump;
        float lookRump = 0.004f * stance.SitLookRump();
        if (Mathf.Abs(lookRump) > Mathf.Abs(lift))
            lift = lookRump;
        float huntRump = 0.004f * stance.SitHuntRump();
        if (huntRump > Mathf.Abs(lift))
            lift = huntRump;
        float leapRump = 0.004f * stance.LeapHuntRump();
        if (leapRump > Mathf.Abs(lift))
            lift = leapRump;
        float sleepRump = stance.SitSleepRump();
        if (sleepRump > 0.01f)
        {
            float t = Time.time + breathPhase;
            float wave = 0.002f * sleepRump * (1f + Mathf.Sin(t * 1.6f));
            if (wave > Mathf.Abs(lift))
                lift = wave;
        }
        float quietRump = stance.SitQuietRump();
        if (quietRump > 0.01f)
        {
            float t = Time.time + breathPhase;
            float wave = 0.0018f * quietRump * (1f + Mathf.Sin(t * 2.5f));
            if (wave > Mathf.Abs(lift))
                lift = wave;
        }
        if (Mathf.Abs(lift) <= 0.0004f && !rumpOn)
            return;

        Transform vis = pelvisSegment.visual;
        vis.localPosition = new Vector3(
            pelvisSegment.visualOffsetX,
            pelvisSegment.visualOffsetY + lift,
            0f);
        rumpOn = Mathf.Abs(lift) > 0.0004f;
    }

    // Once at build. ApplyDistalTail must not Find.
    private void CacheDistalTail()
    {
        distalTailSegs = new HumanSegment[4];
        distalTailSegs[0] = FindLookSeg("Tail2");
        distalTailSegs[1] = FindLookSeg("Tail3");
        distalTailSegs[2] = FindLookSeg("Tail4");
        distalTailSegs[3] = FindLookSeg("Tail5");
    }

    // Picture only. Night Sleep already droops the first hinge
    // (SitSleepTail) and hitches the face / trunk with the 1.6
    // breath — leftover sit held Tail2–Tail5 on TreeSpring rest
    // (muscles only on the first hinge) so the hold still read as
    // a stiff caudal rod on a sleeping face. Sit sniff already
    // droops SitSniffTail with the dirt bob — leftover sit held
    // the same rest-spring stick on a tracking nose. Sit yawn
    // already droops SitYawnTail with the gape — leftover sit
    // held the same rest-spring stick on a stretching jaw. Sit
    // pant already droops SitPantTail with the hang — leftover
    // sit held the same rest-spring stick on a panting face. Sit
    // chew already droops SitChewTail with the snap — leftover
    // sit held the same rest-spring stick on a chewing jaw. Sit
    // scratch already droops SitScratchTail with the rake —
    // leftover sit held the same rest-spring stick on a raking
    // paw. Sit look already hitches SitLookTail with the signed
    // bob — leftover sit held the same rest-spring stick on a
    // nodding neck. Quiet sit already wags IdleTailTarget at
    // 8.5 Hz — leftover sit held the same rest-spring stick on a
    // sitting wag. Forage Walk already wags WalkTail on
    // walkPhase — leftover walk held the same rest-spring stick
    // on a bobbing trunk. Hunt recover sit already raises
    // IdleTailTarget on idleHunt — leftover sit held the same
    // rest-spring stick on a live snarl. Hunt stalk Walk already
    // raises IdleTailTarget on idleHunt — leftover walk held the
    // same rest-spring stick on a live snarl. Hunt leap already
    // raises LeapHuntTail — leftover leap held the same
    // rest-spring stick on a live snarl.
    // Plus = clockwise, tip down, same sign as SitSleepTail /
    // SitSniffTail / SitYawnTail / SitPantTail / SitChewTail /
    // SitScratchTail / IdleTailTarget sit wag / WalkTail.
    // Accumulating 6°…12°. Sleep hitch ±2° with BreathLift's 1.6
    // clock (in phase via breathPhase). Sniff follows idleSniff's
    // ~3 Hz bob (env already pulses; no extra clock). Yawn follows
    // idleYawn's envelope (env already rises / holds / falls; no
    // extra clock). Pant follows idlePant's hang 0.55 + 6.5 Hz
    // (env already hitchs; no extra clock). Chew follows idleChew's
    // hang 0.55 + 11 Hz snap (env already hitchs; no extra clock;
    // chew before sniff so the 0.08 dip does not steal the meal
    // onto SitSniffDistalTail). Look follows idleLook's signed
    // bob (env already weaves; no extra clock; PoseVisual plus
    // droops so SitLookDistalTail is negated vs SitLookTail).
    // Scratch follows idleScratch's hang 0.55 + ~14 Hz rake
    // (env already hitchs; no extra clock; after pant via env so
    // look-up negative env does not restore; look after scratch
    // via Abs). Quiet sit follows IdleTailTarget's 8.5 Hz
    // (env already pulses Time.fixedTime; no extra clock; after
    // look via Abs so look-up negative env does not restore).
    // Forage Walk follows WalkTail's two-dip clock (env already
    // pulses walkPhase; no extra clock; after quiet via Abs so a
    // signed sit env does not restore). Hunt recover sit follows
    // IdleTailTarget's held idleHunt raise (env already coils; no
    // extra clock; after walk via Abs so a signed walk env does
    // not restore; PoseVisual plus droops so SitHuntDistalTail is
    // negated vs IdleTailTarget). Hunt stalk Walk follows that
    // same held idleHunt raise (after recover via Abs so sit env
    // does not restore; PoseVisual plus droops so WalkHuntDistalTail
    // is negated vs IdleTailTarget). Hunt leap follows
    // LeapHuntTail's held 0.85 raise (env already coils; no extra
    // clock; after stalk via Abs so a signed walk env does not
    // restore; PoseVisual plus droops so LeapHuntDistalTail is
    // negated vs LeapHuntTail). Colliders stay. Amp 0 = the old
    // rest-spring stick. No idleTail: Walk() would zero it.
    private void ApplyDistalTail()
    {
        if (distalTailSegs == null)
            return;

        float env = stance.SitSleepDistalTail();
        float hitch = 0f;
        if (env >= 0.01f)
        {
            float t = Time.time + breathPhase;
            hitch = 2f * Mathf.Sin(t * 1.6f);
        }
        else
        {
            env = stance.SitChewDistalTail();
            if (env < 0.01f)
                env = stance.SitSniffDistalTail();
            if (env < 0.01f)
                env = stance.SitYawnDistalTail();
            if (env < 0.01f)
                env = stance.SitPantDistalTail();
            if (env < 0.01f)
                env = stance.SitScratchDistalTail();
            if (Mathf.Abs(env) < 0.01f)
                env = stance.SitLookDistalTail();
            if (Mathf.Abs(env) < 0.01f)
                env = stance.SitQuietDistalTail();
            if (Mathf.Abs(env) < 0.01f)
                env = stance.WalkDistalTail();
            if (Mathf.Abs(env) < 0.01f)
                env = stance.SitHuntDistalTail();
            if (Mathf.Abs(env) < 0.01f)
                env = stance.WalkHuntDistalTail();
            if (Mathf.Abs(env) < 0.01f)
                env = stance.LeapHuntDistalTail();
        }

        if (Mathf.Abs(env) < 0.01f)
        {
            if (!distalTailOn)
                return;
            for (int i = 0; i < distalTailSegs.Length; i++)
                RestoreVisual(distalTailSegs[i]);
            distalTailOn = false;
            return;
        }

        for (int i = 0; i < distalTailSegs.Length; i++)
        {
            float ang = env * (6f + 2f * i + hitch);
            PoseVisual(distalTailSegs[i], 0f, 0f, ang);
        }
        distalTailOn = true;
    }

    private float BreathLift()
    {
        if (stance.energyRest)
            return 0f;
        float leapB = stance.LeapHuntBreath();
        if (leapB > 0.01f)
            return 0.006f * leapB;
        if (stance.IsLeap())
            return 0f;
        if (stance.walkWanted || stance.IsWalk())
            return 0.006f * stance.WalkTrunkBob();

        float t = Time.time + breathPhase;
        if (stance.idleSleep > 0.01f)
            return 0.0024f * (1f + Mathf.Sin(t * 1.6f));
        float pant = stance.idlePant;
        if (pant > 0.01f)
            return 0.010f * pant;
        float yawnB = stance.SitYawnBreath();
        if (yawnB > 0.01f)
            return 0.008f * yawnB;
        float chewB = stance.SitChewBreath();
        if (chewB > 0.01f)
            return 0.005f * chewB;
        float sniffB = stance.SitSniffBreath();
        if (sniffB > 0.01f)
            return 0.005f * sniffB;
        float scratchB = stance.SitScratchBreath();
        if (scratchB > 0.01f)
            return 0.005f * scratchB;
        float lookB = stance.SitLookBreath();
        if (Mathf.Abs(lookB) > 0.01f)
            return 0.005f * lookB;
        float huntB = stance.SitHuntBreath();
        if (huntB > 0.01f)
            return 0.006f * huntB;
        return 0.0018f * (1f + Mathf.Sin(t * 2.5f));
    }

    // Fold 0 = perk at the player, 1 = pin (sleep / hunt). LateUpdate
    // picture only: Euler on Visual children, not a muscle. Sit scratch
    // rakes the near ear on the same kick clock as the hind paw. Forage
    // Walk flops both ears on the trot clock (WalkEarFlop); hunt pin
    // stays on idleHunt. Sit idlePant reuses that flop (SitEar) so
    // EndWalk is not a rest half-fold after the trot. Sit idleLook
    // reuses a signed hitch (SitLookEar) so the neck bob is not a
    // rest fold with a binary perk. Sit idleYawn reuses a fold hitch
    // (SitYawnEar) so the gape is not a statue half-fold. Sit idleChew
    // reuses a fold hitch (SitChewEar) so the meal is not a statue
    // sniff half-fold after the jaw / lid already snap. Night Sleep
    // reuses a breath hitch (SitSleepEar) so the hold is not statue
    // fold 0.88 after the snout already dropped. Quiet Forage sit
    // reuses a breath hitch (SitQuietEar) so the hold is not statue
    // rest fold 0.20 after chest / rump / neck already hitch.
    private void ApplyEars(float face)
    {
        if (ears == null)
            return;

        float want = EarFoldWanted();
        float sleepEar = stance.SitSleepEar();
        bool sleepHitch = sleepEar > 0.01f;
        if (sleepHitch)
        {
            float t = Time.time + breathPhase;
            want += 0.06f * sleepEar * Mathf.Sin(t * 1.6f);
            want = Mathf.Clamp(want, 0f, 1f);
        }
        float quietEar = stance.SitQuietEar();
        bool quietHitch = quietEar > 0.01f;
        if (quietHitch)
        {
            float t = Time.time + breathPhase;
            want += 0.05f * quietEar * Mathf.Sin(t * 2.5f);
            want = Mathf.Clamp(want, 0f, 1f);
        }
        earFold += (want - earFold) * Mathf.Clamp01(Time.deltaTime * 10f);
        float dir = face >= 0f ? 1f : -1f;
        bool scratchOn = stance.idleScratch > 0.01f;
        float kick = scratchOn
            ? 0.22f * stance.idleScratch * Mathf.Abs(Mathf.Sin(Time.time * 16f))
            : 0f;
        float flop = 0.16f * stance.WalkEarFlop();
        float sitFlop = 0.10f * stance.SitEar();
        if (sitFlop > flop)
            flop = sitFlop;
        bool flopOn = flop > 0.004f;
        // Leaving scratch / flop / sleep / quiet hitch must write once or the ear sticks.
        if (!scratchOn && !scratchEarOn && !flopOn && !flopEarOn
            && !sleepHitch && !sleepEarOn
            && !quietHitch && !quietEarOn
            && Mathf.Abs(earFold - lastEarFold) < 0.002f && dir == lastEarDir)
            return;
        scratchEarOn = scratchOn;
        flopEarOn = flopOn;
        sleepEarOn = sleepHitch;
        quietEarOn = quietHitch;
        lastEarFold = earFold;
        lastEarDir = dir;

        float fold = earFold;
        bool flip = dir < 0f;
        for (int i = 0; i < ears.Length; i++)
        {
            SpriteRenderer ear = ears[i];
            if (ear == null)
                continue;
            float foldI = fold + flop * (i == 0 ? 1f : 0.85f);
            if (i == 0)
                foldI += kick;
            float rotZ = Mathf.Lerp(-22f, 58f, foldI) * dir;
            float caudal = i == 0 ? -0.016f : -0.022f;
            float y0 = i == 0 ? 0.052f : 0.046f;
            ear.transform.localPosition = new Vector3(
                (caudal + 0.008f * foldI) * dir,
                y0 - 0.014f * foldI,
                0f);
            ear.transform.localRotation = Quaternion.Euler(0f, 0f, rotZ);
            ear.flipX = flip;
        }
    }

    // Brain already wrote these. Do not add an idleEar field Walk() would zero.
    // Sit pant flop is SitEar on idlePant, not a new float. Sit look
    // hitch is SitLookEar on idleLook, not a new float. Sit yawn
    // hitch is SitYawnEar on idleYawn, not a new float. Sit chew
    // hitch is SitChewEar on idleChew, not a new float. Sleep hitch
    // is SitSleepEar on idleSleep, not a new float. Quiet sit hitch
    // is SitQuietEar between acts, not a new float.
    private float EarFoldWanted()
    {
        if (stance.idleSleep > 0.01f)
            return 0.88f;
        if (stance.IsLeap())
            return 1f;
        // Recover sit writes idleHunt without Walk; keep the hunt pin.
        if (stance.idleHunt > 0.01f)
            return 0.70f;
        float chewEar = stance.SitChewEar();
        if (chewEar > 0.01f)
        {
            // Fold with the snap: sniff half-fold 0.42 → ~0.58 at
            // peak. Amp 0 keeps the old statue sniff half-fold 0.42.
            return Mathf.Clamp(0.42f + 0.16f * chewEar, 0.42f, 0.58f);
        }
        if (stance.idleSniff > 0.01f)
            return 0.42f;
        if (stance.walkWanted || stance.IsWalk())
        {
            // Forage follow writes idleWag. Rest-ish base; flop is
            // WalkEarFlop after smoothing. Amp 0 keeps the old pin.
            if (stance.walkEarAmp >= 0.01f && stance.idleWag >= 0.01f)
                return 0.18f;
            return 0.70f;
        }
        float lookEar = stance.SitLookEar();
        if (Mathf.Abs(lookEar) > 0.01f)
        {
            // Continuous with the neck: look-up perks, look-down folds.
            // Amp 0 keeps the old rest fold / binary perk below.
            return Mathf.Clamp(0.20f - 0.28f * lookEar, 0.04f, 0.45f);
        }
        if (stance.idleLook > 0.45f)
            return 0.04f;
        if (stance.idleScratch > 0.01f)
            return 0.38f;
        float yawnEar = stance.SitYawnEar();
        if (yawnEar > 0.01f)
        {
            // Fold back with the stretch: rest 0.20 → ~0.55 at peak.
            // Amp 0 keeps the old statue half-fold 0.50 below.
            return Mathf.Clamp(0.20f + 0.35f * yawnEar, 0.20f, 0.58f);
        }
        if (stance.idleYawn > 0.01f)
            return 0.50f;
        if (stance.idlePant > 0.01f)
        {
            // Rest-ish hang; flop is SitEar after smoothing. Amp 0
            // keeps the old static half-fold.
            if (stance.sitEarAmp >= 0.01f)
                return 0.28f;
            return 0.34f;
        }
        if (stance.idleChew > 0.01f)
            return 0.42f;
        return 0.20f;
    }

    private float BarkGape()
    {
        if (voice == null)
            return 0f;
        if (voice.LastKind != VoiceKind.Call && voice.LastKind != VoiceKind.Alarm)
            return 0f;
        float age = Time.time - voice.LastCryTime;
        if (age < 0f || age > 0.50f)
            return 0f;
        if (age < 0.08f)
            return age / 0.08f;
        if (age < 0.22f)
            return 1f;
        return 1f - (age - 0.22f) / 0.28f;
    }

    // Flat boxes only. Near-front paw-at-dirt is sit sniff; the far
    // picture stays planted (both-paw antiphase read as a march).
    // Sit scratch reaches the near ear: near-hind Visual cranial+up,
    // far hind planted. A held near-front lift on sit-hunt is the
    // recover plant. Sit glance hops that same near pair then plants
    // (SetFacing already flipped Look). Walk about-face reuses it
    // (wander FaceToward then Walk would have killed the sit hop).
    // Walk swing (ApplyWalkStride) lifts the other column so leftover
    // sit is not a freeze. Colliders stay planted.
    private void ApplyIdleVisuals(float sniff, float scratch, bool huntPlant, float hop)
    {
        RestoreVisual(stridePawRL);
        RestoreStrideProximal();
        float t = Time.time;
        if (hop > 0.01f)
        {
            PoseVisual(sniffPawNear, 0.018f * hop, 10f * hop);
            RestoreVisual(sniffPawFar);
            PoseVisual(scratchThigh, 0.008f * hop, -12f * hop);
            PoseVisual(scratchShin, 0.010f * hop, 14f * hop);
            PoseVisual(scratchPaw, 0.014f * hop, 8f * hop);
            PoseVisual(headSegment, 0.005f * hop, 7f * hop);
        }
        else if (sniff > 0.01f)
        {
            float lift = 0.016f * sniff;
            float step = t * 11f;
            PoseVisual(sniffPawNear, lift * Mathf.Abs(Mathf.Sin(step)), 8f * sniff * Mathf.Sin(step));
            RestoreVisual(sniffPawFar);
            RestoreVisual(headSegment);
        }
        else if (huntPlant)
        {
            // Hold, do not sniff-step: recover is 0.70 s, a sine through
            // zero would hide the plant. Pulse stays above ~0.7.
            float plant = Mathf.Clamp01(stance.idleHunt);
            float pulse = 0.74f + 0.26f * Mathf.Sin(t * 5.2f);
            PoseVisual(sniffPawNear, 0.016f * plant * pulse, 14f * plant);
            RestoreVisual(sniffPawFar);
            RestoreVisual(headSegment);
        }
        else
        {
            RestoreVisual(sniffPawNear);
            RestoreVisual(sniffPawFar);
            RestoreVisual(headSegment);
        }

        if (hop <= 0.01f)
        {
            if (scratch > 0.01f)
            {
                // Reach the near ear, not a planted shin twitch. Local X is
                // sagittal cranial (same axis as pair-spread). Collider stays.
                float dir = stance.FacingSign();
                float kick = Mathf.Abs(Mathf.Sin(t * 16f));
                float u = scratch;
                PoseVisual(scratchThigh, 0.012f * u * dir, 0.022f * u, -48f * u);
                PoseVisual(scratchShin, 0.022f * u * dir, 0.034f * u, 62f * u);
                PoseVisual(
                    scratchPaw,
                    (0.030f + 0.014f * kick) * u * dir,
                    (0.048f + 0.016f * kick) * u,
                    26f * u * (0.35f + 0.65f * Mathf.Sin(t * 16f)));
            }
            else
            {
                RestoreVisual(scratchThigh);
                RestoreVisual(scratchShin);
                RestoreVisual(scratchPaw);
            }
        }
    }

    // Leftover sit plants all four colliders, so DriveWalk's bound
    // still looks planted until capture-point CoM is on the stance
    // heel and off the swing plate (the STEP). Split may already be
    // rolling. Lift the swing pair pictures (both rears, then both
    // fronts) on that leave.
    // Paw-only floated a stamp on planted thighs. Stance column
    // restores. Hop owns the about-face. Colliders stay. No idleStride:
    // Walk() already owns the gait.
    private void ApplyWalkStride()
    {
        float dir = stance.FacingSign();
        PoseStrideLimb(strideUpperFR, strideLowerFR, sniffPawNear,
            stance.WalkStrideHop(true, true), dir);
        PoseStrideLimb(strideUpperFL, strideLowerFL, sniffPawFar,
            stance.WalkStrideHop(true, false), dir);
        PoseStrideLimb(strideThighRL, strideShinRL, stridePawRL,
            stance.WalkStrideHop(false, false), dir);
        PoseStrideLimb(scratchThigh, scratchShin, scratchPaw,
            stance.WalkStrideHop(false, true), dir);
        RestoreVisual(headSegment);
    }

    private static void PoseStrideLimb(
        HumanSegment proximal, HumanSegment mid, HumanSegment paw, float u, float dir)
    {
        if (u < 0.01f)
        {
            RestoreVisual(proximal);
            RestoreVisual(mid);
            RestoreVisual(paw);
            return;
        }
        PoseVisual(proximal, 0.004f * u * dir, 0.008f * u, -10f * u);
        PoseVisual(mid, 0.007f * u * dir, 0.012f * u, 14f * u);
        PoseVisual(paw, 0.010f * u * dir, 0.018f * u, 8f * u);
    }

    private void RestoreStrideProximal()
    {
        RestoreVisual(strideUpperFR);
        RestoreVisual(strideLowerFR);
        RestoreVisual(strideUpperFL);
        RestoreVisual(strideLowerFL);
        RestoreVisual(strideThighRL);
        RestoreVisual(strideShinRL);
    }

    private void RestoreIdleVisuals()
    {
        RestoreVisual(sniffPawNear);
        RestoreVisual(sniffPawFar);
        RestoreVisual(scratchThigh);
        RestoreVisual(scratchShin);
        RestoreVisual(scratchPaw);
        RestoreVisual(stridePawRL);
        RestoreStrideProximal();
        RestoreVisual(headSegment);
    }

    private static void PoseVisual(HumanSegment seg, float y, float zDeg)
    {
        PoseVisual(seg, 0f, y, zDeg);
    }

    private static void PoseVisual(HumanSegment seg, float x, float y, float zDeg)
    {
        if (seg == null || seg.visual == null)
            return;
        seg.visual.localPosition = new Vector3(seg.visualOffsetX + x, seg.visualOffsetY + y, 0f);
        seg.visual.localRotation = Quaternion.Euler(0f, 0f, zDeg);
    }

    private static void RestoreVisual(HumanSegment seg)
    {
        if (seg == null || seg.visual == null)
            return;
        seg.visual.localPosition = new Vector3(seg.visualOffsetX, seg.visualOffsetY, 0f);
        seg.visual.localRotation = Quaternion.identity;
    }
}
