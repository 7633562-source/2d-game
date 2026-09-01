using UnityEngine;
using System.Collections.Generic;

// Сенсорный слой: собирает информацию о состоянии тела, но не управляет им.
// Содержит данные о центре масс, его скорости, опоре, суставах и вестибулярной системе.
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

    [Header("Проба контакта стопы")]
    [Tooltip("Толщина пробы под нижней кромкой стопы (юниты).")]
    public float groundProbeThickness = 0.06f;

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

        // 3. Контакт стоп с землёй
        leftFootGrounded = IsFootGrounded(leftFootCollider);
        rightFootGrounded = IsFootGrounded(rightFootCollider);

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
    // Проба ставится у нижней кромки стопы. Раньше бокс висел вокруг центра
    // стопы и не доставал до опоры на половину её толщины, поэтому контакт
    // не фиксировался ни разу за прогон.
    private bool IsFootGrounded(Collider2D footCollider)
    {
        if (footCollider == null) return false;

        Bounds bounds = footCollider.bounds;
        Vector2 probeCenter = new Vector2(bounds.center.x, bounds.min.y);
        Vector2 probeSize = new Vector2(bounds.size.x * 0.9f, groundProbeThickness);

        Collider2D[] hits = Physics2D.OverlapBoxAll(probeCenter, probeSize, 0f);

        foreach (var hit in hits)
        {
            // Если коллайдер не принадлежит человеку (не является дочерним),
            // считаем, что есть контакт с внешним объектом (землёй).
            if (!hit.transform.IsChildOf(transform))
            {
                return true;
            }
        }
        return false;
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