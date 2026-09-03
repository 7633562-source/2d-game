using UnityEngine;

public class Human : MonoBehaviour
{
    [Header("Общая масса тела (кг)")]
    public float totalMass = 70f;

    [Header("Цвета сегментов (с прозрачностью 50%)")]
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

    // Рост 1.75 м при 70 кг: длины — доли роста по Winter (2009), ширины —
    // переднезадний размер при виде сбоку (у торса это глубина груди, не плечи).
    // Вертикальная цепь pelvis+torso+neck+head+thigh+shin+foot = 1.75.
    [Header("Размеры сегментов")]
    public Vector2 pelvisSize = new Vector2(0.24f, 0.16f);
    public Vector2 torsoSize = new Vector2(0.24f, 0.34f);
    public Vector2 headSize = new Vector2(0.20f, 0.23f);
    public Vector2 neckSize = new Vector2(0.12f, 0.09f);
    public Vector2 upperArmSize = new Vector2(0.10f, 0.33f);
    public Vector2 lowerArmSize = new Vector2(0.08f, 0.26f);
    public Vector2 handSize = new Vector2(0.06f, 0.16f);
    public Vector2 thighSize = new Vector2(0.16f, 0.43f);
    public Vector2 shinSize = new Vector2(0.11f, 0.43f);
    // Один прямоугольник: длина взрослой стопы, высота ~подошва плюс подъём.
    // Двухзвенная плюсна на сотне существ не окупается — см. баланс-правила.
    public Vector2 footSize = new Vector2(0.26f, 0.07f);

    // Голеностоп стоит не в середине стопы, а ближе к пятке: 25% длины стопы
    // назад (6.5 см пятки) и 19.5 см носка вперёд. Поэтому наклониться вперёд
    // человек может заметно сильнее, чем назад, — как и живой.
    [Header("Смещение голеностопа к пятке (м)")]
    public float ankleHeelOffset = 0.065f;

    // Плечо остаётся на прежней абсолютной высоте +0.125 от корня:
    // верх груди 0.25 минус этот отступ. Поднимать к анатомической высоте
    // отдельно — иначе за один шаг меняется и поясница, и рычаг рук.
    [Header("Плечо относительно верха груди (м)")]
    public float shoulderDropFromNeck = 0.125f;

    // Масса сегментов та же, размеры меньше — момент инерции торса упал
    // примерно втрое. Прежнее трение не гасило PD, торс болтался на ~30 Гц.
    [Header("Трение в суставах")]
    public float lumbarFriction = 30f;
    // 40 Н·м насыщается при 76 °/с. Поднять до 80/160/240 не убрало дрожь,
    // а разогнало её: rmsTorsoAngVel 55 → 74 → 123 → 164 °/с.
    public float lumbarFrictionMaxTorque = 40f;
    public float neckFriction = 36f;
    public float shoulderFriction = 12f;
    public float elbowFriction = 12f;
    public float wristFriction = 1.2f;
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
    public float kneeMuscleTorque = 80f;
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

        vestibularSystem = gameObject.AddComponent<VestibularSystem>();

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

        CreateArm("RightArm", 1, torso,
            RIGHT_ARM_UPPER_ORDER, RIGHT_ARM_LOWER_ORDER, RIGHT_ARM_HAND_ORDER,
            RightUpperArmColor, RightLowerArmColor, RightHandColor);
        CreateArm("LeftArm", -1, torso,
            LEFT_ARM_UPPER_ORDER, LEFT_ARM_LOWER_ORDER, LEFT_ARM_HAND_ORDER,
            LeftUpperArmColor, LeftLowerArmColor, LeftHandColor);

        CreateLeg("RightLeg", 1, pelvis,
            RIGHT_LEG_THIGH_ORDER, RIGHT_LEG_SHIN_ORDER, RIGHT_LEG_FOOT_ORDER,
            RightThighColor, RightShinColor, RightFootColor);
        CreateLeg("LeftLeg", -1, pelvis,
            LEFT_LEG_THIGH_ORDER, LEFT_LEG_SHIN_ORDER, LEFT_LEG_FOOT_ORDER,
            LeftThighColor, LeftShinColor, LeftFootColor);

        DisableCollisionsBetweenSegments();

        comCalculator = gameObject.AddComponent<CenterOfMassCalculator>();
        comCalculator.Initialize();

        // Добавляем сенсорный слой
        bodyStateEstimator = gameObject.AddComponent<BodyStateEstimator>();

        SetupBalanceController();
    }

    public void AddNumberLabel(int number)
    {
        if (headSegment == null) return;
        GameObject labelObj = new GameObject("NumberLabel");
        labelObj.transform.SetParent(headSegment.transform, false);
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

    private HumanSegment CreateSegment(string name, Vector2 size, float massFraction, Color color, int sortingOrder)
    {
        GameObject segmentObject = new GameObject(name);
        HumanSegment segment = segmentObject.AddComponent<HumanSegment>();
        float segmentMass = totalMass * massFraction;
        segment.Initialize(name, size, segmentMass, color, transform, sortingOrder);
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
        // Отступ от верха груди, не доля высоты: после разреза торса
        // абсолютная высота плеча должна остаться +0.125 от корня.
        // shoulderY — в локали груди (якорь сустава), shoulderRootY — от корня
        // (расстановка сегментов, они дети Human, а не Torso).
        float shoulderY = torsoSize.y * 0.5f - shoulderDropFromNeck;
        float shoulderRootY = torso.transform.localPosition.y + shoulderY;

        HumanSegment upper = CreateSegment(sideName + "Upper", upperArmSize, UPPER_ARM_MASS_FRACTION, upperColor, upperOrder);
        upper.transform.localPosition = new Vector3(shoulderX, shoulderRootY - upperArmSize.y * 0.5f, 0);
        upper.ConnectTo(torso,
            new Vector2(0, upperArmSize.y * 0.5f),
            new Vector2(shoulderX, shoulderY),
            -90f, 90f);
        AddFriction(upper, shoulderFriction);
        AddMuscles(upper, shoulderMuscleTorque);

        HumanSegment lower = CreateSegment(sideName + "Lower", lowerArmSize, LOWER_ARM_MASS_FRACTION, lowerColor, lowerOrder);
        lower.transform.localPosition = new Vector3(shoulderX, shoulderRootY - upperArmSize.y - lowerArmSize.y * 0.5f, 0);
        lower.ConnectTo(upper,
            new Vector2(0, lowerArmSize.y * 0.5f),
            new Vector2(0, -upperArmSize.y * 0.5f),
            -140f, 0f);
        AddFriction(lower, elbowFriction);
        AddMuscles(lower, elbowMuscleTorque);

        HumanSegment hand = CreateSegment(sideName + "Hand", handSize, HAND_MASS_FRACTION, handColor, handOrder);
        hand.transform.localPosition = new Vector3(shoulderX, shoulderRootY - upperArmSize.y - lowerArmSize.y - handSize.y * 0.5f, 0);
        hand.ConnectTo(lower,
            new Vector2(0, handSize.y * 0.5f),
            new Vector2(0, -lowerArmSize.y * 0.5f),
            -60f, 60f);
        AddFriction(hand, wristFriction);
        AddMuscles(hand, wristMuscleTorque);
    }

    private void CreateLeg(string sideName, float direction, HumanSegment pelvis,
                           int thighOrder, int shinOrder, int footOrder,
                           Color thighColor, Color shinColor, Color footColor)
    {
        float hipX = direction * hipHalfSpacing * LateralProjection;
        // hipY — в локали таза (якорь бедра). Сегменты ноги — дети корня,
        // поэтому их расставляем от hipRootY = −0.25, как до разреза торса.
        float hipY = -pelvisSize.y * 0.5f;
        float hipRootY = pelvis.transform.localPosition.y + hipY;

        HumanSegment thigh = CreateSegment(sideName + "Thigh", thighSize, THIGH_MASS_FRACTION, thighColor, thighOrder);
        thigh.transform.localPosition = new Vector3(hipX, hipRootY - thighSize.y * 0.5f, 0);
        thigh.ConnectTo(pelvis,
            new Vector2(0, thighSize.y * 0.5f),
            new Vector2(hipX, hipY),
            -60f, 60f);
        AddFriction(thigh, hipFriction);
        AddMuscles(thigh, hipMuscleTorque);

        HumanSegment shin = CreateSegment(sideName + "Shin", shinSize, SHIN_MASS_FRACTION, shinColor, shinOrder);
        shin.transform.localPosition = new Vector3(hipX, hipRootY - thighSize.y - shinSize.y * 0.5f, 0);
        shin.ConnectTo(thigh,
            new Vector2(0, shinSize.y * 0.5f),
            new Vector2(0, -thighSize.y * 0.5f),
            -120f, 0f);
        AddFriction(shin, kneeFriction);
        AddMuscles(shin, kneeMuscleTorque);

        // Центр стопы: пятка на ankleHeelOffset позади голеностопа, носок
        // впереди. Якорь считается от центра, а не зашит под конкретную длину.
        float footHalf = footSize.x * 0.5f;
        float footCenterX = hipX - ankleHeelOffset + footHalf;
        float ankleAnchorX = hipX - footCenterX;
        float footY = hipRootY - thighSize.y - shinSize.y - footSize.y * 0.5f;

        HumanSegment foot = CreateSegment(sideName + "Foot", footSize, FOOT_MASS_FRACTION, footColor, footOrder);
        foot.transform.localPosition = new Vector3(footCenterX, footY, 0);
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