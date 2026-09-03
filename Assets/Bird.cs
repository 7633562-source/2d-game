using System.Collections.Generic;
using UnityEngine;

public enum BirdRig
{
    // Стенд: суставы и мышцы, как проверка стойки.
    Ragdoll = 0,
    // Сцена: одно тело. Крылья и лапы — картинка. Десятки особей.
    Flock = 1
}

// Species recipe on the same Bird body. Crow is the flying canon.
public enum BirdKind
{
    Crow = 0,
    Chicken = 1
}

// Сборка сегментарной птицы. Слой тела птицы, не человека.
// Сегмент берём HumanSegment: это общий 2D-примитив (коробка + Visual),
// анатомию человека он не содержит. Мышцы и трение — те же актюаторы.
public class Bird : MonoBehaviour
{
    [Header("Общая масса, кг")]
    public float totalMass = 0.5f;
    [Tooltip("Crow — полёт. Chicken — земля, короткий прыжок.")]
    public BirdKind kind = BirdKind.Crow;
    [Tooltip("Flock — одно тело для стаи. Ragdoll — только стенд стойки.")]
    public BirdRig rig = BirdRig.Flock;

    public BirdSensors sensors;
    public BirdController controller;
    public BirdFlight flight;
    public HumanSegment headSegment;
    public HumanSegment beakSegment;
    public Transform headVisual;
    public Transform neckVisual;
    public Transform beakVisual;
    public BoxCollider2D beakCollider;
    public BoxCollider2D perchCollider;
    public Transform leftWingRoot;
    public Transform rightWingRoot;
    public Transform leftUlna;
    public Transform rightUlna;
    public Transform leftThigh;
    public Transform rightThigh;
    // Только картинка. Scale.x = facing; Rigidbody2D и perch на корпусе не трогаем.
    public Transform lookRoot;

    // Layout guide: one semi-transparent box per collider. No Crow/Chicken PNG.
    private static readonly Color BodyColor = new Color(0.1f, 0.2f, 1f, 0.5f);
    private static readonly Color NeckColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
    private static readonly Color HeadColor = new Color(1f, 1f, 0f, 0.5f);
    private static readonly Color BeakColor = new Color(1f, 0.55f, 0.1f, 0.5f);
    private static readonly Color TailColor = new Color(0.1f, 0.75f, 0.85f, 0.5f);
    private static readonly Color HumerusNear = new Color(0f, 0.6f, 0f, 0.5f);
    private static readonly Color HumerusFar = new Color(0.4f, 0.8f, 0.2f, 0.5f);
    private static readonly Color UlnaNear = new Color(0.8f, 0.1f, 0.1f, 0.5f);
    private static readonly Color UlnaFar = new Color(1f, 0.5f, 0.5f, 0.5f);
    private static readonly Color HandNear = new Color(1f, 0.5f, 0f, 0.5f);
    private static readonly Color HandFar = new Color(1f, 0.7f, 0.3f, 0.5f);
    private static readonly Color ThighNear = new Color(0.5f, 0f, 0.5f, 0.5f);
    private static readonly Color ThighFar = new Color(0.7f, 0.4f, 0.7f, 0.5f);
    private static readonly Color ShankNear = new Color(0.5f, 0.25f, 0f, 0.5f);
    private static readonly Color ShankFar = new Color(0.7f, 0.45f, 0.2f, 0.5f);
    private static readonly Color FootNear = new Color(0.4f, 0f, 0f, 0.5f);
    private static readonly Color FootFar = new Color(0.8f, 0.3f, 0.3f, 0.5f);

    private static PhysicsMaterial2D flockFootMaterial;

    private static PhysicsMaterial2D FlockFootMaterial()
    {
        if (flockFootMaterial == null)
        {
            flockFootMaterial = new PhysicsMaterial2D("BirdFlockFoot");
            flockFootMaterial.friction = 0.22f;
            flockFootMaterial.bounciness = 0f;
        }
        return flockFootMaterial;
    }

    [Header("Ракурс: вид почти в профиль")]
    public float viewAngleDegrees = 80f;
    [Tooltip("Не входит в якоря суставов: иначе шаг вперёд, не ракурс.")]
    public float hipHalfSpacing = 0.012f;
    public float visualSideOffset = 0.028f;
    public float jointMarkerDiameter = 0.018f;

    [Header("Размеры сегментов, м")]
    public Vector2 bodySize = new Vector2(0.14f, 0.06f);
    public Vector2 neckSize = new Vector2(0.028f, 0.035f);
    public Vector2 headSize = new Vector2(0.042f, 0.038f);
    public Vector2 beakSize = new Vector2(0.032f, 0.012f);
    public Vector2 tailSize = new Vector2(0.09f, 0.022f);
    public Vector2 wingHumerusSize = new Vector2(0.032f, 0.055f);
    public Vector2 wingUlnaSize = new Vector2(0.040f, 0.070f);
    public Vector2 wingHandSize = new Vector2(0.070f, 0.110f);
    public Vector2 thighSize = new Vector2(0.022f, 0.040f);
    public Vector2 shankSize = new Vector2(0.018f, 0.055f);
    public Vector2 footSize = new Vector2(0.05f, 0.022f);

    [Header("Якоря на корпусе")]
    public float shoulderForward = 0.02f;
    public float hipBack = 0f;
    public float ankleHeelOffset = 0.012f;

    [Header("Минимальная инерция сегмента, кг·м²")]
    [Tooltip("2D-срез лапы даёт I ~ 10⁻⁶, и шаг 5 мс на таком I либо расходится, либо SPD душит момент.")]
    public float minLimbInertia = 0.001f;
    [Tooltip("Слегка тяжелее 2D-среза, чтобы жест взмаха не крутил грудку.")]
    public float minBodyInertia = 0.015f;

    [Header("Трение в суставах")]
    public float neckFriction = 0.002f;
    public float beakFriction = 0.0012f;
    public float tailFriction = 0.012f;
    public float shoulderFriction = 0.0008f;
    public float elbowFriction = 0.0005f;
    public float wristFriction = 0.0003f;
    public float hipFriction = 0.02f;
    public float kneeFriction = 0.015f;
    public float ankleFriction = 0.008f;

    [Header("Максимальные моменты мышц, Н·м")]
    public float neckMuscleTorque = 0.12f;
    public float beakMuscleTorque = 0.06f;
    public float tailMuscleTorque = 0.18f;
    public float shoulderMuscleTorque = 0.28f;
    public float elbowMuscleTorque = 0.20f;
    public float wristMuscleTorque = 0.08f;
    public float hipMuscleTorque = 0.50f;
    public float kneeMuscleTorque = 0.40f;
    public float ankleExtensorTorque = 0.22f;
    public float ankleFlexorTorque = 0.10f;

    private const float BODY_MASS = 0.622f;
    private const float NECK_MASS = 0.025f;
    private const float HEAD_MASS = 0.068f;
    private const float BEAK_MASS = 0.012f;
    private const float TAIL_MASS = 0.025f;
    private const float HUMERUS_MASS = 0.016f;
    private const float ULNA_MASS = 0.013f;
    private const float HAND_MASS = 0.020f;
    private const float THIGH_MASS = 0.035f;
    private const float SHANK_MASS = 0.028f;
    private const float FOOT_MASS = 0.012f;

    private const int LEFT_WING_HUMERUS = -6;
    private const int LEFT_WING_ULNA = -5;
    private const int LEFT_WING_HAND = -4;
    private const int LEFT_LEG_THIGH = -3;
    private const int LEFT_LEG_SHANK = -2;
    private const int LEFT_LEG_FOOT = -1;
    private const int BODY_ORDER = 0;
    private const int TAIL_ORDER = 0;
    private const int NECK_ORDER = 1;
    private const int HEAD_ORDER = 2;
    private const int BEAK_ORDER = 3;
    private const int RIGHT_LEG_THIGH = 4;
    private const int RIGHT_LEG_SHANK = 5;
    private const int RIGHT_LEG_FOOT = 6;
    private const int RIGHT_WING_HUMERUS = 7;
    private const int RIGHT_WING_ULNA = 8;
    private const int RIGHT_WING_HAND = 9;

    // Стопы на поверхности groundY: высота корня = цепочка ноги + полкорпуса.
    public static float StandingRootY(float groundY = -2f)
    {
        return groundY + 0.022f + 0.055f + 0.040f + 0.060f * 0.5f;
    }

    public float StandingRootOffset(float groundY = -2f)
    {
        return groundY + footSize.y + shankSize.y + thighSize.y + bodySize.y * 0.5f;
    }

    // Sensors and flight: instance sizes after ApplyKind, else crow canon.
    public static float StanceRootY(Bird owner, float groundY = -2f)
    {
        return owner != null ? owner.StandingRootOffset(groundY) : StandingRootY(groundY);
    }

    // Spawn before the instance exists. Keep in sync with ApplyKind sizes.
    public static float KindStandingRootY(BirdKind recipe, float groundY = -2f)
    {
        if (recipe == BirdKind.Chicken)
            return groundY + 0.024f + 0.060f + 0.050f + 0.10f * 0.5f;
        return StandingRootY(groundY);
    }

    public static BirdKind ParseKind(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return BirdKind.Crow;
        switch (raw.Trim().ToLowerInvariant())
        {
            case "chicken":
            case "hen":
                return BirdKind.Chicken;
            default:
                return BirdKind.Crow;
        }
    }

    // Crow numbers are the flying canon. Chicken is heavier, short-winged.
    public void ApplyKind(BirdKind recipe)
    {
        kind = recipe;
        totalMass = 0.5f;
        bodySize = new Vector2(0.14f, 0.06f);
        neckSize = new Vector2(0.028f, 0.035f);
        headSize = new Vector2(0.042f, 0.038f);
        beakSize = new Vector2(0.032f, 0.012f);
        tailSize = new Vector2(0.09f, 0.022f);
        wingHumerusSize = new Vector2(0.032f, 0.055f);
        wingUlnaSize = new Vector2(0.040f, 0.070f);
        wingHandSize = new Vector2(0.070f, 0.110f);
        thighSize = new Vector2(0.022f, 0.040f);
        shankSize = new Vector2(0.018f, 0.055f);
        footSize = new Vector2(0.05f, 0.022f);
        if (recipe != BirdKind.Chicken) return;

        totalMass = 1.8f;
        bodySize = new Vector2(0.18f, 0.10f);
        neckSize = new Vector2(0.024f, 0.048f);
        headSize = new Vector2(0.040f, 0.036f);
        beakSize = new Vector2(0.046f, 0.016f);
        tailSize = new Vector2(0.055f, 0.040f);
        wingHumerusSize = new Vector2(0.028f, 0.040f);
        wingUlnaSize = new Vector2(0.030f, 0.048f);
        wingHandSize = new Vector2(0.042f, 0.058f);
        thighSize = new Vector2(0.030f, 0.050f);
        shankSize = new Vector2(0.022f, 0.060f);
        footSize = new Vector2(0.055f, 0.024f);
        minBodyInertia = 0.028f;
    }

    private void ApplyFlightKind()
    {
        if (flight == null) return;
        if (kind != BirdKind.Chicken) return;
        // < 1: a hop, not a cruise. Brain should prefer Walk/Sit.
        flight.hoverMean = 0.55f;
        flight.forwardThrust = 0.12f;
        flight.glideLift = 0.32f;
        flight.pitchTarget = 4f;
        flight.flarePitch = 14f;
        flight.verticalDamping = 5.5f;
    }

    private void ApplyControlKind()
    {
        if (controller == null) return;
        if (kind != BirdKind.Chicken) return;
        controller.flapFrequency = 4f;
        controller.walkThrust = 1.6f;
        controller.walkSpeed = 0.40f;
        controller.takeoffDelay = 0f;
        controller.sitHipAngle = 18f;
        controller.mode = BirdMode.Sit;
    }

    public void BuildBird()
    {
        ApplyKind(kind);
        foreach (Transform child in transform)
            Destroy(child.gameObject);

        if (rig == BirdRig.Flock)
        {
            BuildFlock();
            return;
        }

        HumanSegment body = CreateSegment("Body", bodySize, BODY_MASS, BodyColor, BODY_ORDER);
        body.transform.localPosition = Vector3.zero;
        AddBodyAero(body);
        AddBirdFlight(body);
        ApplyFlightKind();

        HumanSegment neck = CreateSegment("Neck", neckSize, NECK_MASS, NeckColor, NECK_ORDER);
        float bodyTop = bodySize.y * 0.5f;
        neck.transform.localPosition = new Vector3(bodySize.x * 0.22f, bodyTop + neckSize.y * 0.5f, 0f);
        neck.ConnectTo(body,
            new Vector2(0f, -neckSize.y * 0.5f),
            new Vector2(bodySize.x * 0.22f, bodyTop),
            -40f, 50f);
        AddFriction(neck, neckFriction, 0.4f);
        AddMuscles(neck, neckMuscleTorque);

        HumanSegment head = CreateSegment("Head", headSize, HEAD_MASS, HeadColor, HEAD_ORDER);
        head.transform.localPosition = new Vector3(
            neck.transform.localPosition.x,
            neck.transform.localPosition.y + neckSize.y * 0.5f + headSize.y * 0.5f,
            0f);
        head.ConnectTo(neck,
            new Vector2(0f, -headSize.y * 0.5f),
            new Vector2(0f, neckSize.y * 0.5f),
            -35f, 35f);
        AddFriction(head, neckFriction, 0.4f);
        AddMuscles(head, neckMuscleTorque);
        headSegment = head;
        AttachRagdollBeak(head);

        HumanSegment tail = CreateSegment("Tail", tailSize, TAIL_MASS, TailColor, TAIL_ORDER);
        float bodyBack = -bodySize.x * 0.5f;
        tail.transform.localPosition = new Vector3(bodyBack - tailSize.x * 0.5f, 0f, 0f);
        tail.ConnectTo(body,
            new Vector2(tailSize.x * 0.5f, 0f),
            new Vector2(bodyBack, 0f),
            -50f, 50f);
        AddFriction(tail, tailFriction, 0.5f);
        AddMuscles(tail, tailMuscleTorque);

        CreateWing("RightWing", 1f, body,
            RIGHT_WING_HUMERUS, RIGHT_WING_ULNA, RIGHT_WING_HAND,
            HumerusNear, UlnaNear, HandNear);
        CreateWing("LeftWing", -1f, body,
            LEFT_WING_HUMERUS, LEFT_WING_ULNA, LEFT_WING_HAND,
            HumerusFar, UlnaFar, HandFar);

        CreateLeg("RightLeg", 1f, body,
            RIGHT_LEG_THIGH, RIGHT_LEG_SHANK, RIGHT_LEG_FOOT,
            ThighNear, ShankNear, FootNear);
        CreateLeg("LeftLeg", -1f, body,
            LEFT_LEG_THIGH, LEFT_LEG_SHANK, LEFT_LEG_FOOT,
            ThighFar, ShankFar, FootFar);

        DisableCollisionsBetweenSegments();

        sensors = gameObject.AddComponent<BirdSensors>();
        sensors.Initialize();
        controller = gameObject.AddComponent<BirdController>();
        BindController(controller);
        ApplyControlKind();
        AttachHit();

        ActuatorDriver actuators = gameObject.AddComponent<ActuatorDriver>();
        actuators.RebuildCache();
    }

    public void AddNumberLabel(int number)
    {
        Transform labelParent = null;
        if (headVisual != null)
        {
            Transform vis = headVisual.Find("Visual");
            labelParent = vis != null ? vis : headVisual;
        }
        else if (headSegment != null)
            labelParent = headSegment.visual != null ? headSegment.visual : headSegment.transform;
        if (labelParent == null)
        {
            Transform body = transform.Find("Body");
            labelParent = body != null ? body : transform;
        }
        GameObject labelObj = new GameObject("NumberLabel");
        labelObj.transform.SetParent(labelParent, false);
        float headHalf = headVisual != null ? headSize.y * 0.5f
            : (headSegment != null ? headSegment.size.y * 0.5f : 0.04f);
        labelObj.transform.localPosition = new Vector3(0f, headHalf + 0.08f, -0.1f);
        TextMesh textMesh = labelObj.AddComponent<TextMesh>();
        textMesh.text = "B" + number;
        textMesh.fontSize = 28;
        textMesh.characterSize = 0.04f;
        textMesh.color = Color.black;
        textMesh.anchor = TextAnchor.MiddleCenter;
        Renderer renderer = labelObj.GetComponent<Renderer>();
        renderer.sortingOrder = 100;
    }

    // Одно тело: масса и коллайдер на корпусе, остальное — дети без физики.
    private void BuildFlock()
    {
        HumanSegment body = CreateSegment("Body", bodySize, 1f, BodyColor, BODY_ORDER);
        body.transform.localPosition = Vector3.zero;
        if (body.rb != null)
        {
            // Headless сравнивает CSV: без интерполяции. В Play — один корпус,
            // Interpolate дешевле, чем 16 тел, и кадр 60 Гц не дёргает.
            body.rb.interpolation = HeadlessTrial.Active
                ? RigidbodyInterpolation2D.None
                : RigidbodyInterpolation2D.Interpolate;
            body.rb.collisionDetectionMode = CollisionDetectionMode2D.Discrete;
            body.rb.sleepMode = RigidbodySleepMode2D.StartAwake;
        }

        float legH = thighSize.y + shankSize.y + footSize.y;
        perchCollider = body.gameObject.AddComponent<BoxCollider2D>();
        perchCollider.size = new Vector2(Mathf.Max(footSize.x, bodySize.x * 0.55f), legH);
        perchCollider.offset = new Vector2(0f, -bodySize.y * 0.5f - legH * 0.5f);
        perchCollider.sharedMaterial = FlockFootMaterial();

        AddBodyAero(body);
        AddBirdFlight(body);
        ApplyFlightKind();

        // Разворот — зеркало Look. Корпус с коллайдером всегда scale 1:
        // иначе perch уезжает и PhysX 2D ломает нормали.
        GameObject lookObject = new GameObject("Look");
        lookRoot = lookObject.transform;
        lookRoot.SetParent(body.transform, false);
        lookRoot.localPosition = Vector3.zero;
        lookRoot.localScale = Vector3.one;

        float bodyTop = bodySize.y * 0.5f;
        Transform neck = CreateVisualPart("Neck", neckSize, NeckColor, NECK_ORDER, lookRoot);
        neck.localPosition = new Vector3(bodySize.x * 0.22f, bodyTop + neckSize.y * 0.5f, 0f);

        Transform head = CreateVisualPart("Head", headSize, HeadColor, HEAD_ORDER, neck);
        head.localPosition = new Vector3(0f, neckSize.y * 0.5f + headSize.y * 0.5f, 0f);
        neckVisual = neck;
        headVisual = head;
        headSegment = null;
        AttachFlockBeak(head);

        Transform tail = CreateVisualPart("Tail", tailSize, TailColor, TAIL_ORDER, lookRoot);
        tail.localPosition = new Vector3(-bodySize.x * 0.5f - tailSize.x * 0.5f, 0f, 0f);

        rightWingRoot = CreateVisualWing(1f, lookRoot);
        leftWingRoot = CreateVisualWing(-1f, lookRoot);
        rightUlna = rightWingRoot != null ? rightWingRoot.Find("RightWingUlna") : null;
        leftUlna = leftWingRoot != null ? leftWingRoot.Find("LeftWingUlna") : null;

        rightThigh = CreateVisualLeg(1f, lookRoot);
        leftThigh = CreateVisualLeg(-1f, lookRoot);

        sensors = gameObject.AddComponent<BirdSensors>();
        sensors.Initialize();
        controller = gameObject.AddComponent<BirdController>();
        controller.flockRig = true;
        ApplyControlKind();
        ApplyLookFacing(controller.FacingSign());
        AttachHit();
    }

    // Картинка смотрит в сторону полёта. Физика корпуса не масштабируется.
    public void ApplyLookFacing(float sign)
    {
        if (lookRoot == null) return;
        lookRoot.localScale = new Vector3(sign >= 0f ? 1f : -1f, 1f, 1f);
    }

    // Flock: спрайт как у сегмента, без HumanSegment на каждом пере.
    private Transform CreateVisualPart(
        string name, Vector2 size, Color color, int order, Transform parent)
    {
        GameObject go = new GameObject(name);
        HumanSegment bake = go.AddComponent<HumanSegment>();
        bake.InitializeVisualOnly(name, size, color, parent, order, HumanVisualShape.Box);
        Object.DestroyImmediate(bake);
        return go.transform;
    }

    private static void OffsetVisual(Transform bone, float x, float y)
    {
        if (bone == null) return;
        Transform visual = bone.Find("Visual");
        if (visual != null)
            visual.localPosition = new Vector3(x, y, 0f);
    }

    private Transform CreateVisualWing(float direction, Transform body)
    {
        float visualShift = direction * visualSideOffset;
        float shoulderY = bodySize.y * 0.30f;
        const float foldDeg = -90f;
        string side = direction > 0f ? "RightWing" : "LeftWing";
        bool near = direction > 0f;

        Transform humerus = CreateVisualPart(side + "Humerus", wingHumerusSize, near ? HumerusNear : HumerusFar, direction > 0f ? RIGHT_WING_HUMERUS : LEFT_WING_HUMERUS, body);
        humerus.localPosition = new Vector3(shoulderForward, shoulderY, 0f);
        humerus.localRotation = Quaternion.Euler(0f, 0f, foldDeg);
        OffsetVisual(humerus, visualShift, -wingHumerusSize.y * 0.5f);

        Transform ulna = CreateVisualPart(side + "Ulna", wingUlnaSize, near ? UlnaNear : UlnaFar, direction > 0f ? RIGHT_WING_ULNA : LEFT_WING_ULNA, humerus);
        ulna.localPosition = new Vector3(0f, -wingHumerusSize.y, 0f);
        OffsetVisual(ulna, visualShift, -wingUlnaSize.y * 0.5f);

        Transform hand = CreateVisualPart(side + "Hand", wingHandSize, near ? HandNear : HandFar, direction > 0f ? RIGHT_WING_HAND : LEFT_WING_HAND, ulna);
        hand.localPosition = new Vector3(0f, -wingUlnaSize.y, 0f);
        OffsetVisual(hand, visualShift, -wingHandSize.y * 0.5f);

        return humerus;
    }

    private Transform CreateVisualLeg(float direction, Transform body)
    {
        float visualShift = direction * visualSideOffset;
        float hipY = -bodySize.y * 0.5f;
        string side = direction > 0f ? "RightLeg" : "LeftLeg";
        bool near = direction > 0f;

        Transform thigh = CreateVisualPart(side + "Thigh", thighSize, near ? ThighNear : ThighFar, direction > 0f ? RIGHT_LEG_THIGH : LEFT_LEG_THIGH, body);
        thigh.localPosition = new Vector3(-hipBack, hipY, 0f);
        OffsetVisual(thigh, visualShift, -thighSize.y * 0.5f);

        Transform shank = CreateVisualPart(side + "Shank", shankSize, near ? ShankNear : ShankFar, direction > 0f ? RIGHT_LEG_SHANK : LEFT_LEG_SHANK, thigh);
        shank.localPosition = new Vector3(0f, -thighSize.y, 0f);
        OffsetVisual(shank, visualShift, -shankSize.y * 0.5f);

        Transform foot = CreateVisualPart(side + "Foot", footSize, near ? FootNear : FootFar, direction > 0f ? RIGHT_LEG_FOOT : LEFT_LEG_FOOT, shank);
        foot.localPosition = new Vector3(-ankleHeelOffset + footSize.x * 0.5f, -shankSize.y, 0f);
        OffsetVisual(foot, visualShift, -footSize.y * 0.5f);

        return thigh;
    }

    private void CreateWing(string sideName, float direction, HumanSegment body,
                            int humerusOrder, int ulnaOrder, int handOrder,
                            Color humerusColor, Color ulnaColor, Color handColor)
    {
        float visualShift = direction * visualSideOffset;
        float shoulderY = bodySize.y * 0.30f;
        Vector2 shoulderRoot = (Vector2)body.transform.localPosition
            + new Vector2(shoulderForward, shoulderY);

        // Сложено вдоль спины: кость смотрит в −X. jointAngle ≈ 90° при
        // корпусе 0, см. соглашение jointAngle = родитель − свой.
        const float foldDeg = -90f;
        Quaternion fold = Quaternion.Euler(0f, 0f, foldDeg);
        Vector2 tipDir = new Vector2(-1f, 0f);

        float humerusLen = wingHumerusSize.y;
        float ulnaLen = wingUlnaSize.y;
        float handLen = wingHandSize.y;

        HumanSegment humerus = CreateSegment(sideName + "Humerus", wingHumerusSize, HUMERUS_MASS, humerusColor, humerusOrder, HumanVisualShape.Box, true);
        humerus.transform.localPosition = shoulderRoot + tipDir * (humerusLen * 0.5f);
        humerus.transform.localRotation = fold;
        humerus.SetVisualOffset(visualShift);
        humerus.ConnectTo(body,
            new Vector2(0f, humerusLen * 0.5f),
            new Vector2(shoulderForward, shoulderY),
            25f, 165f);
        AddFriction(humerus, shoulderFriction, 0.5f);
        AddMuscles(humerus, shoulderMuscleTorque);

        HumanSegment ulna = CreateSegment(sideName + "Ulna", wingUlnaSize, ULNA_MASS, ulnaColor, ulnaOrder, HumanVisualShape.Box, true);
        ulna.transform.localPosition = shoulderRoot + tipDir * (humerusLen + ulnaLen * 0.5f);
        ulna.transform.localRotation = fold;
        ulna.SetVisualOffset(visualShift);
        ulna.ConnectTo(humerus,
            new Vector2(0f, ulnaLen * 0.5f),
            new Vector2(0f, -humerusLen * 0.5f),
            -100f, 5f);
        AddFriction(ulna, elbowFriction, 0.4f);
        AddMuscles(ulna, elbowMuscleTorque);

        HumanSegment hand = CreateSegment(sideName + "Hand", wingHandSize, HAND_MASS, handColor, handOrder, HumanVisualShape.Box, true);
        hand.transform.localPosition = shoulderRoot + tipDir * (humerusLen + ulnaLen + handLen * 0.5f);
        hand.transform.localRotation = fold;
        hand.SetVisualOffset(visualShift);
        hand.ConnectTo(ulna,
            new Vector2(0f, handLen * 0.5f),
            new Vector2(0f, -ulnaLen * 0.5f),
            -55f, 55f);
        AddFriction(hand, wristFriction, 0.3f);
        AddMuscles(hand, wristMuscleTorque);
    }

    private void CreateLeg(string sideName, float direction, HumanSegment body,
                           int thighOrder, int shankOrder, int footOrder,
                           Color thighColor, Color shankColor, Color footColor)
    {
        float visualShift = direction * visualSideOffset;
        float hipY = -bodySize.y * 0.5f;
        // Разнос ног только на Visual: ось X — вперёд, не влево-вправо.
        Vector2 hipRoot = (Vector2)body.transform.localPosition + new Vector2(-hipBack, hipY);

        HumanSegment thigh = CreateSegment(sideName + "Thigh", thighSize, THIGH_MASS, thighColor, thighOrder, HumanVisualShape.Box, true);
        thigh.transform.localPosition = new Vector3(hipRoot.x, hipRoot.y - thighSize.y * 0.5f, 0f);
        thigh.SetVisualOffset(visualShift);
        thigh.ConnectTo(body,
            new Vector2(0f, thighSize.y * 0.5f),
            new Vector2(-hipBack, hipY),
            -80f, 80f);
        AddFriction(thigh, hipFriction, 0.8f);
        AddMuscles(thigh, hipMuscleTorque);

        HumanSegment shank = CreateSegment(sideName + "Shank", shankSize, SHANK_MASS, shankColor, shankOrder, HumanVisualShape.Box, true);
        shank.transform.localPosition = new Vector3(hipRoot.x, hipRoot.y - thighSize.y - shankSize.y * 0.5f, 0f);
        shank.SetVisualOffset(visualShift);
        // Плюс уводит низ голени назад — как колено человека после разворота.
        shank.ConnectTo(thigh,
            new Vector2(0f, shankSize.y * 0.5f),
            new Vector2(0f, -thighSize.y * 0.5f),
            0f, 130f);
        AddFriction(shank, kneeFriction, 0.8f);
        AddMuscles(shank, kneeMuscleTorque);

        float footHalf = footSize.x * 0.5f;
        float footCenterX = hipRoot.x - ankleHeelOffset + footHalf;
        float ankleAnchorX = hipRoot.x - footCenterX;
        float footY = hipRoot.y - thighSize.y - shankSize.y - footSize.y * 0.5f;

        HumanSegment foot = CreateSegment(sideName + "Foot", footSize, FOOT_MASS, footColor, footOrder, HumanVisualShape.Box, true);
        foot.transform.localPosition = new Vector3(footCenterX, footY, 0f);
        foot.SetVisualOffset(visualShift);
        foot.ConnectTo(shank,
            new Vector2(ankleAnchorX, footSize.y * 0.5f),
            new Vector2(0f, -shankSize.y * 0.5f),
            -40f, 40f);
        AddFriction(foot, ankleFriction, 0.5f);
        AddMuscles(foot, ankleFlexorTorque, ankleExtensorTorque);
    }

    private HumanSegment CreateSegment(
        string name,
        Vector2 size,
        float massFraction,
        Color color,
        int sortingOrder,
        HumanVisualShape visualShape = HumanVisualShape.Box,
        bool floorInertia = false,
        string albedoKey = null)
    {
        GameObject segmentObject = new GameObject(name);
        HumanSegment segment = segmentObject.AddComponent<HumanSegment>();
        float segmentMass = totalMass * massFraction;
        segment.Initialize(name, size, segmentMass, color, transform, sortingOrder, visualShape, albedoKey);
        segment.jointMarkerDiameter = jointMarkerDiameter;
        if (segment.rb != null)
        {
            segment.rb.sleepMode = rig == BirdRig.Ragdoll
                ? RigidbodySleepMode2D.NeverSleep
                : RigidbodySleepMode2D.StartAwake;
            // Ноги и крылья: 2D-срез даёт I ~ 10⁻⁶, SPD иначе душит момент.
            if (floorInertia && minLimbInertia > 0f && segment.rb.inertia < minLimbInertia)
                segment.rb.inertia = minLimbInertia;
            if (name == "Body" && minBodyInertia > 0f && segment.rb.inertia < minBodyInertia)
                segment.rb.inertia = minBodyInertia;
        }
        return segment;
    }

    private void AddBirdFlight(HumanSegment body)
    {
        flight = body.gameObject.AddComponent<BirdFlight>();
    }

    private void AddBodyAero(HumanSegment body)
    {
        BodyAero aero = body.gameObject.AddComponent<BodyAero>();
        aero.frontalArea = bodySize.x * bodySize.y * 0.85f;
        // Силу прикладывает BirdFlight: один FixedUpdate на особь, не два.
        aero.generateForce = false;
        aero.enabled = false;
    }

    private void AddFriction(HumanSegment segment, float damping, float maxTorque)
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

    private void BindController(BirdController bc)
    {
        BindPair("RightWingHumerus", out bc.rightShoulderJoint, out bc.rightShoulderFlexor, out bc.rightShoulderExtensor);
        BindPair("LeftWingHumerus", out bc.leftShoulderJoint, out bc.leftShoulderFlexor, out bc.leftShoulderExtensor);
        BindPair("RightWingUlna", out bc.rightElbowJoint, out bc.rightElbowFlexor, out bc.rightElbowExtensor);
        BindPair("LeftWingUlna", out bc.leftElbowJoint, out bc.leftElbowFlexor, out bc.leftElbowExtensor);
        BindPair("RightWingHand", out bc.rightWristJoint, out bc.rightWristFlexor, out bc.rightWristExtensor);
        BindPair("LeftWingHand", out bc.leftWristJoint, out bc.leftWristFlexor, out bc.leftWristExtensor);
        BindPair("RightLegThigh", out bc.rightHipJoint, out bc.rightHipFlexor, out bc.rightHipExtensor);
        BindPair("LeftLegThigh", out bc.leftHipJoint, out bc.leftHipFlexor, out bc.leftHipExtensor);
        BindPair("RightLegShank", out bc.rightKneeJoint, out bc.rightKneeFlexor, out bc.rightKneeExtensor);
        BindPair("LeftLegShank", out bc.leftKneeJoint, out bc.leftKneeFlexor, out bc.leftKneeExtensor);
        BindPair("RightLegFoot", out bc.rightAnkleJoint, out bc.rightAnkleFlexor, out bc.rightAnkleExtensor);
        BindPair("LeftLegFoot", out bc.leftAnkleJoint, out bc.leftAnkleFlexor, out bc.leftAnkleExtensor);
        BindPair("Neck", out bc.neckJoint, out bc.neckFlexor, out bc.neckExtensor);
        BindPair("Head", out bc.headJoint, out bc.headFlexor, out bc.headExtensor);
        BindPair("Beak", out bc.beakJoint, out bc.beakFlexor, out bc.beakExtensor);
        BindPair("Tail", out bc.tailJoint, out bc.tailFlexor, out bc.tailExtensor);
    }

    // Beak is a real segment: the only collider that may hurt an enemy.
    private void AttachRagdollBeak(HumanSegment head)
    {
        float beakX = head.transform.localPosition.x + headSize.x * 0.5f + beakSize.x * 0.5f;
        float beakY = head.transform.localPosition.y - headSize.y * 0.12f;
        HumanSegment beak = CreateSegment("Beak", beakSize, BEAK_MASS, BeakColor, BEAK_ORDER);
        beak.transform.localPosition = new Vector3(beakX, beakY, 0f);
        beak.ConnectTo(head,
            new Vector2(-beakSize.x * 0.5f, 0f),
            new Vector2(headSize.x * 0.5f, -headSize.y * 0.12f),
            -20f, 20f);
        AddFriction(beak, beakFriction, 0.25f);
        AddMuscles(beak, beakMuscleTorque);
        if (beak.collider != null)
            beak.collider.isTrigger = true;
        beakSegment = beak;
        beakVisual = beak.transform;
        beakCollider = beak.collider;
    }

    // Flock: no extra Rigidbody2D. Trigger rides on the body via Look.
    private void AttachFlockBeak(Transform head)
    {
        Transform beak = CreateVisualPart("Beak", beakSize, BeakColor, BEAK_ORDER, head);
        beak.localPosition = new Vector3(headSize.x * 0.5f + beakSize.x * 0.5f, -headSize.y * 0.12f, 0f);
        BoxCollider2D col = beak.gameObject.AddComponent<BoxCollider2D>();
        col.size = beakSize;
        col.isTrigger = true;
        beakVisual = beak;
        beakCollider = col;
        beakSegment = null;
    }

    private void AttachHit()
    {
        Damageable life = GetComponent<Damageable>();
        if (life == null)
            life = gameObject.AddComponent<Damageable>();
        life.maxHealth = kind == BirdKind.Chicken ? 8f : 10f;
        life.health = life.maxHealth;

        BeakStrike strike = GetComponent<BeakStrike>();
        if (strike == null)
            strike = gameObject.AddComponent<BeakStrike>();
        strike.damage = kind == BirdKind.Chicken ? 0.35f : 0.5f;
        strike.Bind(beakCollider);
    }

    private void BindPair(string childName, out HingeJoint2D joint, out Muscle flexor, out Muscle extensor)
    {
        joint = null;
        flexor = null;
        extensor = null;
        Transform t = transform.Find(childName);
        if (t == null) return;
        joint = t.GetComponent<HingeJoint2D>();
        Muscle[] muscles = t.GetComponents<Muscle>();
        flexor = System.Array.Find(muscles, m => m.direction == 1f);
        extensor = System.Array.Find(muscles, m => m.direction == -1f);
    }

    // Wood is not a wall. Birds fly through the crown and sit only on BirdPerch.
    public static void GhostTreeWood(Bird[] birds, PlantTree tree)
    {
        if (tree == null)
            return;
        GhostTreeWood(birds, new[] { tree });
    }

    public static void GhostTreeWood(Bird[] birds, PlantTree[] trees)
    {
        if (birds == null || trees == null)
            return;

        List<Collider2D> birdCols = new List<Collider2D>(32);
        List<Collider2D> tmp = new List<Collider2D>(8);
        for (int i = 0; i < birds.Length; i++)
        {
            if (birds[i] == null)
                continue;
            tmp.Clear();
            birds[i].GetComponentsInChildren<Collider2D>(tmp);
            birdCols.AddRange(tmp);
        }

        if (birdCols.Count == 0)
            return;

        List<Collider2D> wood = new List<Collider2D>(32);
        for (int t = 0; t < trees.Length; t++)
        {
            PlantTree tree = trees[t];
            if (tree == null)
                continue;
            tree.EnsureBirdPerches();
            wood.Clear();
            tree.GetComponentsInChildren<Collider2D>(wood);
            for (int w = 0; w < wood.Count; w++)
            {
                Collider2D wc = wood[w];
                if (wc == null || wc.GetComponent<BirdPerch>() != null)
                    continue;
                for (int b = 0; b < birdCols.Count; b++)
                {
                    if (birdCols[b] != null)
                        Physics2D.IgnoreCollision(wc, birdCols[b], true);
                }
            }
        }
    }

    private void DisableCollisionsBetweenSegments()
    {
        Collider2D[] allColliders = GetComponentsInChildren<Collider2D>();
        for (int i = 0; i < allColliders.Length; i++)
        {
            for (int j = i + 1; j < allColliders.Length; j++)
                Physics2D.IgnoreCollision(allColliders[i], allColliders[j], true);
        }
    }
}
