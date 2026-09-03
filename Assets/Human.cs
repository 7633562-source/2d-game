using UnityEngine;

public class Human : MonoBehaviour
{
    [Header("Общая масса тела (кг)")]
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

    [Header("Множители (для логирования)")]
    public float muscleMultiplier = 1f;
    public float frictionMultiplier = 1f;

    // Ссылки на системы
    public VestibularSystem vestibularSystem;
    public CenterOfMassCalculator comCalculator;
    public BodyStateEstimator bodyStateEstimator;   // добавлено
    public HumanSegment headSegment;

    // Коэффициенты массы. Таз и грудь вместе 0.4346 — как прежний цельный торс,
    // поэтому суммарная масса тела остаётся 70 кг (de Leva, 1996).
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

    // Камера смотрит под 80° к фронтальной плоскости, то есть почти в профиль.
    // Поперечные (лево-право) расстояния между парными суставами сжимаются на
    // экране в cos(80°) ≈ 0.17 раза. В сагиттальной 2D-модели ось X — это
    // «вперёд-назад», поэтому разнос по X — только визуальный сдвиг ракурса,
    // а не настоящий шаг: иначе человек вечно стоит в выпаде.
    [Header("Ракурс: вид почти в профиль")]
    public float viewAngleDegrees = 80f;

    [Header("Анатомическое полурасстояние между парными суставами (м)")]
    public float hipHalfSpacing = 0.085f;
    public float shoulderHalfSpacing = 0.185f;

    private float LateralProjection => Mathf.Cos(viewAngleDegrees * Mathf.Deg2Rad);

    // Colored boxes sit on the collider. A side shift was for painted
    // overlap; it slid the rectangle off the BoxCollider2D.
    [Header("Разнос картинки (только отрисовка)")]
    public float visualSideOffset = 0f;

    // 0.1 юнита при ширине торса 0.24 давало пятно в пол-туловища на сустав.
    [Header("Диаметр маркера сустава (м)")]
    public float jointMarkerDiameter = 0.03f;

    // Рост 1.75 м при 70 кг: длины — доли роста по Winter (2009), ширины —
    // переднезадний размер при виде сбоку (у торса это глубина груди, не плечи).
    // Вертикальная цепь pelvis+torso+neck+head+thigh+shin+foot = 1.75.
    [Header("Размеры сегментов")]
    public Vector2 pelvisSize = new Vector2(0.24f, 0.16f);
    public Vector2 torsoSize = new Vector2(0.24f, 0.34f);
    public Vector2 headSize = new Vector2(0.20f, 0.23f);
    public Vector2 neckSize = new Vector2(0.12f, 0.09f);
    public Vector2 upperArmSize = new Vector2(0.10f, 0.33f);
    public Vector2 lowerArmSize = new Vector2(0.08f, 0.34f);
    // 0.32 м было почти предплечье: кисть читалась как третье звено.
    // Winter: кисть ≈ 0.108 × 1.75 м. Ширина — вид сбоку, не ладонь в фас.
    // Стойка vis_hand (0.19) принята. Откат на 0.32 без сводки walk не держим.
    // Физическая длина кисти 0.32 (CoM/запястье). 0.19 ломал walk/стойку fold ~25 с
    // (handfix0). Короче рисовать — только Visual, не это поле.
    public Vector2 handSize = new Vector2(0.06f, 0.16f);
    public Vector2 thighSize = new Vector2(0.16f, 0.43f);
    public Vector2 shinSize = new Vector2(0.11f, 0.43f);
    // Один прямоугольник: длина взрослой стопы, высота ~подошва плюс подъём.
    // Двухзвенная плюсна на сотне существ не окупается — см. баланс-правила.
    public Vector2 footSize = new Vector2(0.26f, 0.07f);

    // Сцепление подошвы с грунтом. 0.4 — прежнее поведение: коллайдер без
    // материала берёт юнитивский дефолт, то есть гладкую подошву. Выше —
    // обувь. Пара считается как sqrt(µ_стопы · µ_грунта), поэтому одна
    // стопа не поднимает сцепление выше корня из трения земли.
    [Header("Трение подошвы о грунт")]
    public float footFriction = 0.4f;

    // Голеностоп стоит не в середине стопы, а ближе к пятке: 25% длины стопы
    // назад (6.5 см пятки) и 19.5 см носка вперёд. Поэтому наклониться вперёд
    // человек может заметно сильнее, чем назад, — как и живой.
    [Header("Смещение голеностопа к пятке (м)")]
    public float ankleHeelOffset = 0.065f;

    // Плечо остаётся на прежней абсолютной высоте +0.125 от корня:
    // верх груди 0.25 минус этот отступ. Поднимать к анатомической высоте
    // отдельно — иначе за один шаг меняется и поясница, и рычаг рук.
    [Header("Плечо относительно верха груди (м)")]
    public float shoulderDropFromNeck = 0.09f;

    // Масса сегментов та же, размеры меньше — момент инерции торса упал
    // примерно втрое. Прежнее трение не гасило PD, торс болтался на ~30 Гц.
    [Header("Трение в суставах")]
    public float lumbarFriction = 30f;
    // 40 Н·м насыщается при 76 °/с. Поднять до 80/160/240 не убрало дрожь,
    // а разогнало её: rmsTorsoAngVel 55 → 74 → 123 → 164 °/с.
    public float lumbarFrictionMaxTorque = 40f;
    // τ = Ieff/K ≈ 0.20 с при Ieff шеи 0.001567. Прежние 36 приваривали
    // голову к торсу: стоп-момент режет всё выше Ieff/Δt ≈ 0.31, и K=36
    // неотличим от K=0.31. SPD держит суставы, сварной шов больше не нужен.
    // Голова делит это же поле.
    public float neckFriction = 0.00784f;
    // Руки: τ ≈ 0.10 с. Ieff из arm_stand — плечо 0.0177722, локоть
    // 0.00509677, кисть 0.00117525. Прежние 12 / 12 / 1.2 варили суставы
    // (stepRatio 3.4 / 11.8 / 5.1). SPD уже держит позу; τ=0.20 роняет
    // толчок вперёд 24 Н·с, поэтому остановились на 0.10.
    public float shoulderFriction = 0.177722f;
    public float elbowFriction = 0.0509677f;
    public float wristFriction = 0.0117525f;
    public float hipFriction = 12f;
    public float kneeFriction = 12f;
    public float ankleFriction = 3f;

    // Потолки близки к тому, что нужно для удержания позы: при массе 70 кг
    // и смещении центра масс 5 см голеностопу требуется около 35 Н·м.
    // Прежние 150–300 Н·м подавались целиком и разносили тело за два шага физики.
    [Header("Максимальные моменты мышц")]
    // Над поясницей ~35 кг, их центр примерно на 0.30 м выше сустава.
    // Гравитация даёт ≈ 104·sin θ Н·м, то есть 36 Н·м при наклоне 20°.
    // 90 Н·м — запас 2.5 раза, и всё ещё ниже бедра (100 Н·м).
    public float lumbarMuscleTorque = 90f;
    public float neckMuscleTorque = 15f;
    public float shoulderMuscleTorque = 40f;
    public float elbowMuscleTorque = 30f;
    public float wristMuscleTorque = 10f;
    public float hipMuscleTorque = 100f;
    // 130 Н·м: присед складывает колено далеко от −8°, и 80 Н·м
    // не держат рычаг бедра. Потолок из плана работ, не из de Leva.
    public float kneeMuscleTorque = 130f;
    // Голеностоп у человека резко асимметричен: подошвенные сгибатели (икра,
    // держат от падения вперёд) втрое сильнее тыльных (передняя поверхность
    // голени, держат от падения назад). В модели это extensor и flexor.
    // Потолки взяты по геометрии стопы: больший момент стопа всё равно не
    // передаст, тело просто начнёт вращаться вокруг носка или пятки.
    // Стопа 0.26 м, пятка 0.065 м, носок 0.195 м, m*g = 70*9.81 ≈ 687 Н.
    // Вперёд: 687*(носок 0.195)/2 ≈ 67 Н·м на стопу (округлено до 65).
    // Назад:  687*(пятка 0.065)/2 ≈ 22 Н·м на стопу.
    public float ankleExtensorTorque = 65f;
    public float ankleFlexorTorque = 22f;

    // Порядок отрисовки
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

        // Корень остаётся в центре старого цельного торса. Таз и грудь
        // ставятся так, чтобы бёдра были на y = −0.25, а основание шеи
        // на y = +0.25: вертикальная цепь и точка спавна не меняются.
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
        // Поясница несёт ~35 кг верха тела: потолок трения по умолчанию
        // 15 Н·м мал, и вязкость молча упрётся в него. Потолок задаётся
        // отдельно, чтобы вязкость оставалась линейной на быстрых ударах.
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

        // Сенсоры добавляются только после того, как построены все сегменты:
        // их Awake ищет тела через transform.Find один раз и больше не
        // повторяет. VestibularSystem стоял выше по файлу, до создания шеи и
        // головы, и молча отдавал по ним нули — торс и таз к тому моменту уже
        // существовали, поэтому наклон торса работал и ошибку ничего не выдало.
        vestibularSystem = gameObject.AddComponent<VestibularSystem>();

        comCalculator = gameObject.AddComponent<CenterOfMassCalculator>();
        comCalculator.Initialize();

        // Добавляем сенсорный слой
        bodyStateEstimator = gameObject.AddComponent<BodyStateEstimator>();

        SetupBalanceController();

        // Трение, затем мышцы — один FixedUpdate, порядок явный.
        ActuatorDriver actuators = gameObject.AddComponent<ActuatorDriver>();
        actuators.RebuildCache();

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
        // До ConnectTo: маркер создаётся там и читает этот размер.
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
        // Отступ от верха груди, не доля высоты: после разреза торса
        // абсолютная высота плеча должна остаться +0.125 от корня.
        // shoulderY — в локали груди (якорь сустава), shoulderRootY — от корня
        // (расстановка сегментов, они дети Human, а не Torso).
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
    }

    // Один материал на обе стопы: Box2D комбинирует трение пары как
    // sqrt(µ1 · µ2), поэтому величина имеет смысл только вместе с грунтом.
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

    private void CreateLeg(string sideName, float direction, HumanSegment pelvis,
                           int thighOrder, int shinOrder, int footOrder,
                           Color thighColor, Color shinColor, Color footColor)
    {
        float hipX = direction * hipHalfSpacing * LateralProjection;
        // Visual-only side swap for legs: keep physics anchors/segments in place,
        // only mirror which leg is drawn closer to the camera.
        float visualShift = -direction * visualSideOffset;
        // hipY — в локали таза (якорь бедра). Сегменты ноги — дети корня,
        // поэтому их расставляем от hipRootY = −0.25, как до разреза торса.
        float hipY = -pelvisSize.y * 0.5f;
        float hipRootY = pelvis.transform.localPosition.y + hipY;

        HumanSegment thigh = CreateSegment(sideName + "Thigh", thighSize, THIGH_MASS_FRACTION, thighColor, thighOrder);
        thigh.transform.localPosition = new Vector3(hipX, hipRootY - thighSize.y * 0.5f, 0);
        thigh.SetVisualOffset(visualShift);
        // ±90°: присед уводит таз назад дальше прежнего упора ±60°.
        thigh.ConnectTo(pelvis,
            new Vector2(0, thighSize.y * 0.5f),
            new Vector2(hipX, hipY),
            -90f, 90f);
        AddFriction(thigh, hipFriction);
        AddMuscles(thigh, hipMuscleTorque);

        HumanSegment shin = CreateSegment(sideName + "Shin", shinSize, SHIN_MASS_FRACTION, shinColor, shinOrder);
        shin.transform.localPosition = new Vector3(hipX, hipRootY - thighSize.y - shinSize.y * 0.5f, 0);
        shin.SetVisualOffset(visualShift);
        // 0…+120, а не −120…0. jointAngle = угол родителя минус свой, поэтому
        // положительный угол колена уводит низ голени назад — это человеческое
        // сгибание при взгляде вправо. Прежний диапазон разрешал только
        // обратное, страусиное: колено уходило за линию бедро–голеностоп.
        shin.ConnectTo(thigh,
            new Vector2(0, shinSize.y * 0.5f),
            new Vector2(0, -thighSize.y * 0.5f),
            0f, 120f);
        AddFriction(shin, kneeFriction);
        AddMuscles(shin, kneeMuscleTorque);

        // Центр стопы: пятка на ankleHeelOffset позади голеностопа, носок
        // впереди. Якорь считается от центра, а не зашит под конкретную длину.
        float footHalf = footSize.x * 0.5f;
        float footCenterX = hipX - ankleHeelOffset + footHalf;
        float ankleAnchorX = hipX - footCenterX;
        float footY = hipRootY - thighSize.y - shinSize.y - footSize.y * 0.5f;

        HumanSegment foot = CreateSegment(sideName + "Foot", footSize, FOOT_MASS_FRACTION, footColor, footOrder);
        if (foot.collider != null)
            foot.collider.sharedMaterial = SoleMaterial();
        foot.transform.localPosition = new Vector3(footCenterX, footY, 0);
        foot.SetVisualOffset(visualShift);
        foot.ConnectTo(shin,
            new Vector2(ankleAnchorX, footSize.y * 0.5f),
            new Vector2(0, -shinSize.y * 0.5f),
            -45f, 45f);
        AddFriction(foot, ankleFriction);
        AddMuscles(foot, ankleFlexorTorque, ankleExtensorTorque);
    }

    private void SetupBalanceController()
    {
        BalanceController bc = gameObject.AddComponent<BalanceController>();

        Transform rightThigh = transform.Find("RightLegThigh");
        Transform rightShin = transform.Find("RightLegShin");
        Transform rightFoot = transform.Find("RightLegFoot");
        Transform leftThigh = transform.Find("LeftLegThigh");
        Transform leftShin = transform.Find("LeftLegShin");
        Transform leftFoot = transform.Find("LeftLegFoot");

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