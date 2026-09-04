using UnityEngine;
using System.Collections.Generic;

// Сенсорный слой: собирает информацию о состоянии тела, но не управляет им.
[DefaultExecutionOrder(-100)]
public class BodyStateEstimator : MonoBehaviour
{
    // ─── ССЫЛКИ НА ДРУГИЕ КОМПОНЕНТЫ ───
    private VestibularSystem vestibular;
    private CenterOfMassCalculator comCalculator;

    // ─── ССЫЛКИ НА СУСТАВЫ ───
    private HingeJoint2D leftHipJoint;
    private HingeJoint2D rightHipJoint;
    private HingeJoint2D leftKneeJoint;
    private HingeJoint2D rightKneeJoint;
    private HingeJoint2D leftAnkleJoint;
    private HingeJoint2D rightAnkleJoint;
    private HingeJoint2D lumbarJoint;

    // ─── ССЫЛКИ НА СТОПЫ ───
    private Transform leftFoot;
    private Transform rightFoot;
    private Collider2D leftFootCollider;
    private Collider2D rightFootCollider;

    private Transform leftHand;
    private Transform rightHand;
    private Collider2D leftHandCollider;
    private Collider2D rightHandCollider;
    private Collider2D groundCollider;
    private BalanceController balance;
    private MotionIntent intent;

    [Header("Проба контакта")]
    [Tooltip("Толщина пробы под нижней кромкой стопы (юниты). Только OverlapBox.")]
    public float groundProbeThickness = 0.06f;
    [Tooltip("GetContacts NonAlloc вместо OverlapBox под стопой. Откат — false.")]
    public bool useGetContactsProbe = true;

    // Переиспользуемые буферы: без new на каждом шаге физики.
    private readonly Collider2D[] groundProbeHits = new Collider2D[8];
    private readonly ContactPoint2D[] groundContacts = new ContactPoint2D[8];
    private ContactFilter2D groundContactFilter;
    private bool groundContactFilterReady;

    // Линейка запаса: saturция буфера 8 — скрытый потолок будущих коллайдеров.
    // Только счётчики, на grounded не влияют.
    public int probeCallCount;
    public int probeSaturatedCount;
    public int probeHitSum;

    // ─── ДАННЫЕ СОСТОЯНИЯ (публичные для логирования) ───
    [Header("Центр масс")]
    public Vector2 comPosition;
    public Vector2 comVelocity;

    [Header("Опора")]
    public float supportMinX;
    public float supportMaxX;
    public float supportCenterX;
    public float supportWidth;
    public float supportMargin; // + внутри опоры, 0 на границе, - снаружи

    [Header("Стопы")]
    public Vector2 leftFootPosition;
    public Vector2 rightFootPosition;
    public bool leftFootGrounded;
    public bool rightFootGrounded;
    // Контакт пятки (point.x ≤ центр стопы). Для CoM/fell не используется.
    public bool leftFootHeelLoaded;
    public bool rightFootHeelLoaded;
    public bool leftHandGrounded;
    public bool rightHandGrounded;

    [Header("Углы суставов (градусы)")]
    public float leftHipAngle;
    public float rightHipAngle;
    public float leftKneeAngle;
    public float rightKneeAngle;
    public float leftAnkleAngle;
    public float rightAnkleAngle;
    public float lumbarAngle;

    [Header("Угловые скорости суставов (град/с)")]
    public float leftHipAngularVelocity;
    public float rightHipAngularVelocity;
    public float leftKneeAngularVelocity;
    public float rightKneeAngularVelocity;
    public float leftAnkleAngularVelocity;
    public float rightAnkleAngularVelocity;
    public float lumbarAngularVelocity;

    [Header("Вестибулярная система")]
    public float torsoTilt;               // наклон торса (градусы)
    public float torsoAngularVelocity;     // угловая скорость торса (град/с)
    public float pelvisTilt;              // наклон таза к мировой вертикали (градусы)
    public float pelvisAngularVelocity;   // угловая скорость таза (град/с)

    // ─── ВНУТРЕННИЕ ПЕРЕМЕННЫЕ ДЛЯ РАСЧЁТА СКОРОСТИ COM ───
    private Vector2 previousComPosition;
    private bool hasPreviousCom = false;

    // ─── AWAKE: ПОЛУЧАЕМ ССЫЛКИ ───
    void Awake()
    {
        vestibular = GetComponent<VestibularSystem>();
        comCalculator = GetComponent<CenterOfMassCalculator>();

        leftHipJoint = GetJoint("LeftLegThigh");
        rightHipJoint = GetJoint("RightLegThigh");
        leftKneeJoint = GetJoint("LeftLegShin");
        rightKneeJoint = GetJoint("RightLegShin");
        leftAnkleJoint = GetJoint("LeftLegFoot");
        rightAnkleJoint = GetJoint("RightLegFoot");
        lumbarJoint = GetJoint("Torso");

        leftFoot = transform.Find("LeftLegFoot");
        rightFoot = transform.Find("RightLegFoot");
        leftFootCollider = leftFoot != null ? leftFoot.GetComponent<Collider2D>() : null;
        rightFootCollider = rightFoot != null ? rightFoot.GetComponent<Collider2D>() : null;

        leftHand = transform.Find("LeftArmHand");
        rightHand = transform.Find("RightArmHand");
        leftHandCollider = leftHand != null ? leftHand.GetComponent<Collider2D>() : null;
        rightHandCollider = rightHand != null ? rightHand.GetComponent<Collider2D>() : null;
        balance = GetComponent<BalanceController>();
        intent = GetComponent<MotionIntent>();
        GameObject ground = GameObject.Find("Ground");
        groundCollider = ground != null ? ground.GetComponent<Collider2D>() : null;

        if (comCalculator == null)
        {
            Debug.LogError("BodyStateEstimator: CenterOfMassCalculator не найден!");
        }
        if (vestibular == null)
        {
            Debug.LogError("BodyStateEstimator: VestibularSystem не найден!");
        }
    }

    // ─── ПОИСК СУСТАВА ПО ИМЕНИ ДОЧЕРНЕГО ОБЪЕКТА ───
    private HingeJoint2D GetJoint(string childName)
    {
        Transform t = transform.Find(childName);
        return t != null ? t.GetComponent<HingeJoint2D>() : null;
    }

    // ─── FIXEDUPDATE: СБОР ДАННЫХ ───
    void FixedUpdate()
    {
        // 1. Центр масс
        if (comCalculator != null)
        {
            comPosition = comCalculator.GetCenterOfMass();

            // Скорость COM = (текущий - предыдущий) / dt
            if (hasPreviousCom)
            {
                comVelocity = (comPosition - previousComPosition) / Time.fixedDeltaTime;
            }
            else
            {
                comVelocity = Vector2.zero;
                hasPreviousCom = true;
            }
            previousComPosition = comPosition;
        }

        // 2. Позиции стоп
        if (leftFoot != null) leftFootPosition = leftFoot.position;
        if (rightFoot != null) rightFootPosition = rightFoot.position;

        // 3. Контакт стоп с землёй (heel — отдельно, на grounded не влияет)
        leftFootGrounded = IsSegmentGrounded(leftFootCollider, out leftFootHeelLoaded);
        rightFootGrounded = IsSegmentGrounded(rightFootCollider, out rightFootHeelLoaded);
        // Кисти не пробуем, пока присед мелкий: в стойке руки в воздухе,
        // а OverlapBox всё равно обходил их коллайдеры каждый шаг.
        float slop = balance != null ? Mathf.Max(0f, balance.crouchHandGroundSlop) : 0.06f;
        float handGroundY = ResolveGroundTopY() + slop;
        leftHandGrounded = IsHandNearGround(leftHandCollider, handGroundY);
        rightHandGrounded = IsHandNearGround(rightHandCollider, handGroundY);

        // 4. Границы опоры и запас устойчивости
        CalculateSupport();

        // 5. Углы суставов
        leftHipAngle = leftHipJoint != null ? leftHipJoint.jointAngle : 0f;
        rightHipAngle = rightHipJoint != null ? rightHipJoint.jointAngle : 0f;
        leftKneeAngle = leftKneeJoint != null ? leftKneeJoint.jointAngle : 0f;
        rightKneeAngle = rightKneeJoint != null ? rightKneeJoint.jointAngle : 0f;
        leftAnkleAngle = leftAnkleJoint != null ? leftAnkleJoint.jointAngle : 0f;
        rightAnkleAngle = rightAnkleJoint != null ? rightAnkleJoint.jointAngle : 0f;
        lumbarAngle = lumbarJoint != null ? lumbarJoint.jointAngle : 0f;

        // 6. Угловые скорости суставов
        leftHipAngularVelocity = leftHipJoint != null ? leftHipJoint.jointSpeed : 0f;
        rightHipAngularVelocity = rightHipJoint != null ? rightHipJoint.jointSpeed : 0f;
        leftKneeAngularVelocity = leftKneeJoint != null ? leftKneeJoint.jointSpeed : 0f;
        rightKneeAngularVelocity = rightKneeJoint != null ? rightKneeJoint.jointSpeed : 0f;
        leftAnkleAngularVelocity = leftAnkleJoint != null ? leftAnkleJoint.jointSpeed : 0f;
        rightAnkleAngularVelocity = rightAnkleJoint != null ? rightAnkleJoint.jointSpeed : 0f;
        lumbarAngularVelocity = lumbarJoint != null ? lumbarJoint.jointSpeed : 0f;

        // 7. Вестибулярные данные: торс и таз в одной системе отсчёта
        if (vestibular != null)
        {
            torsoTilt = vestibular.GetBodyTilt();
            torsoAngularVelocity = vestibular.GetBodyAngularVelocity();
            pelvisTilt = vestibular.GetPelvisTilt();
            pelvisAngularVelocity = vestibular.GetPelvisAngularVelocity();
        }
    }

    // ─── ПРОВЕРКА КОНТАКТА СТОПЫ С ЗЕМЛЁЙ ───
    // GetContacts — контакты PhysX со слоем Ground. OverlapBox — геометрическая
    // проба 0.06 м под кромкой (useGetContactsProbe = false).
    // heelLoaded: есть контакт с point.x ≤ центра стопы (человек смотрит в +X).
    private bool IsSegmentGrounded(Collider2D col, out bool heelLoaded)
    {
        heelLoaded = false;
        if (col == null) return false;

        int hitCount;
        if (useGetContactsProbe)
        {
            if (!groundContactFilterReady)
            {
                groundContactFilter = new ContactFilter2D();
                groundContactFilter.SetLayerMask(GroundLayers.Mask);
                groundContactFilter.useTriggers = false;
                groundContactFilterReady = true;
            }
            hitCount = col.GetContacts(groundContactFilter, groundContacts);
            float midX = col.bounds.center.x;
            int n = Mathf.Min(hitCount, groundContacts.Length);
            for (int i = 0; i < n; i++)
            {
                if (groundContacts[i].point.x <= midX)
                {
                    heelLoaded = true;
                    break;
                }
            }
        }
        else
        {
            Bounds bounds = col.bounds;
            Vector2 probeCenter = new Vector2(bounds.center.x, bounds.min.y);
            Vector2 probeSize = new Vector2(bounds.size.x * 0.9f, groundProbeThickness);
            hitCount = Physics2D.OverlapBoxNonAlloc(
                probeCenter, probeSize, 0f, groundProbeHits, GroundLayers.Mask);
            // Без точек контакта пятку не отличить — считаем любой контакт пяткой.
            heelLoaded = hitCount > 0;
        }

        probeCallCount++;
        probeHitSum += hitCount;
        int bufLen = useGetContactsProbe ? groundContacts.Length : groundProbeHits.Length;
        if (hitCount >= bufLen)
            probeSaturatedCount++;
        return hitCount > 0;
    }

    // Кисть: проба PhysX плюс допуск к y=−2.0 в глубоком приседе — ладонь
    // не всегда пробивает коллайдер, но опора по X уже расширена.
    private bool IsHandNearGround(Collider2D col, float handGroundY)
    {
        if (col == null) return false;
        bool heelIgnored;
        if (IsSegmentGrounded(col, out heelIgnored)) return true;
        return col.bounds.min.y <= handGroundY;
    }

    private float ResolveGroundTopY()
    {
        if (groundCollider == null)
        {
            GameObject ground = GameObject.Find("Ground");
            if (ground != null)
                groundCollider = ground.GetComponent<Collider2D>();
        }
        return groundCollider != null ? groundCollider.bounds.max.y : -2f;
    }

    // ─── РАСЧЁТ ОБЛАСТИ ОПОРЫ И ЗАПАСА УСТОЙЧИВОСТИ ───
    // Опора — объединение коллайдеров пяток и пальцев, которые касаются земли.
    private void CalculateSupport()
    {
        if (leftFoot == null || rightFoot == null)
        {
            supportMinX = 0f;
            supportMaxX = 0f;
            supportCenterX = 0f;
            supportWidth = 0f;
            supportMargin = 0f;
            return;
        }

        bool hasSupport = false;
        float minX = 0f;
        float maxX = 0f;

        ExpandSupport(leftFootCollider, leftFootGrounded, ref hasSupport, ref minX, ref maxX);
        ExpandSupport(rightFootCollider, rightFootGrounded, ref hasSupport, ref minX, ref maxX);

        // На приседе кисти на земле — третья и четвёртая точки опоры.
        if (balance != null && balance.crouchLevel >= balance.crouchHandSupportMin)
        {
            ExpandSupport(leftHandCollider, leftHandGrounded, ref hasSupport, ref minX, ref maxX);
            ExpandSupport(rightHandCollider, rightHandGrounded, ref hasSupport, ref minX, ref maxX);
        }

        // Обе стопы в воздухе: опоры нет, запас считаем от середины стоп.
        if (!hasSupport)
        {
            supportCenterX = (leftFoot.position.x + rightFoot.position.x) * 0.5f;
            supportMinX = supportCenterX;
            supportMaxX = supportCenterX;
            supportWidth = 0f;
            supportMargin = -Mathf.Abs(comPosition.x - supportCenterX);
            return;
        }

        supportMinX = minX;
        supportMaxX = maxX;
        supportCenterX = (supportMinX + supportMaxX) * 0.5f;
        supportWidth = supportMaxX - supportMinX;

        // Если опора нулевая, margin = расстояние COM до центра
        if (supportWidth < 0.001f)
        {
            supportMargin = -Mathf.Abs(comPosition.x - supportCenterX);
            return;
        }

        if (comPosition.x >= supportMinX && comPosition.x <= supportMaxX)
        {
            // COM внутри опоры: margin = минимальное расстояние до краёв
            float distLeft = comPosition.x - supportMinX;
            float distRight = supportMaxX - comPosition.x;
            supportMargin = Mathf.Min(distLeft, distRight);
        }
        else
        {
            // COM снаружи: margin = -расстояние до ближайшего края
            float distToLeft = Mathf.Abs(comPosition.x - supportMinX);
            float distToRight = Mathf.Abs(comPosition.x - supportMaxX);
            supportMargin = -Mathf.Min(distToLeft, distToRight);
        }
    }

    private static void ExpandSupport(Collider2D col, bool grounded, ref bool hasSupport, ref float minX, ref float maxX)
    {
        if (col == null || !grounded) return;
        Bounds b = col.bounds;
        minX = hasSupport ? Mathf.Min(minX, b.min.x) : b.min.x;
        maxX = hasSupport ? Mathf.Max(maxX, b.max.x) : b.max.x;
        hasSupport = true;
    }
}