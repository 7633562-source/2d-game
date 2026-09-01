using UnityEngine;

// Балансировщик на основе центра масс.
// Колени слегка согнуты — так живой человек гасит качку.
// Голеностоп позу не держит вовсе: он только толкает центр масс обратно
// над стопами. Команда, которая вдавливает сустав в предел, отсекается.
// При падении (> fallCoMOffset) мышцы отключаются.
public class BalanceController : MonoBehaviour
{
    public enum BalanceState
    {
        Balancing,
        Recovery,
        Falling
    }

    [Header("Состояние системы")]
    public BalanceState currentState = BalanceState.Balancing;

    [Header("Пороги конечного автомата")]
    public float recoveryCoMOffset = 0.15f;
    public float fallCoMOffset = 0.30f;

    [Header("Коэффициенты балансировки")]
    // Входы нормированы: 10 см смещения и 30 °/с наклона дают единицу.
    // Раньше складывали метры и град/с — скорость торса сразу забивала сигнал.
    public float comOffsetReference = 0.10f;
    public float tiltSpeedReference = 30f;
    public float comVelocityReference = 0.30f;
    public float comProportionalGain = 1.2f;
    public float comDerivativeGain = 0.35f;
    public float maxBalanceSignal = 1.0f;

    [Header("Целевые углы (градусы)")]
    // 5° оставляли таз на −6.5° в мире, и поясница компенсировала это
    // почти до упора. 10° ставит таз около вертикали, рабочая точка
    // поясницы уходит в середину диапазона ±20°.
    public float hipBaseAngle = 10f;
    public float kneeBaseAngle = -8f;
    public float kneeRecoveryFlex = 12f;
    public float hipBalanceGain = 8f;

    // Ошибка и скорость делятся на опорные величины, поэтому вход PD
    // безразмерный, а коэффициенты имеют порядок единицы. До нормировки
    // ошибки в 0.03° хватало, чтобы активация упёрлась в 1: регулятор
    // работал выключателем и всегда на полной мощности.
    [Header("Нормировка входа PD")]
    public float errorReferenceDegrees = 10f;
    public float speedReferenceDegPerSec = 200f;

    [Header("PD-регуляторы суставов")]
    public float hipPGain = 1.5f;
    public float hipDGain = 0.4f;
    public float kneePGain = 1.2f;
    public float kneeDGain = 0.5f;
    public float neckPGain = 1.5f;
    public float neckDGain = 0.4f;
    [Header("Поясница")]
    // Опора — мировая вертикаль груди, не угол относительно таза.
    // Иначе при завале таза поясница везёт 35 кг верха вниз вместе с ним.
    // Ошибка и скорость только из VestibularSystem: смешивать мировой
    // наклон с joint.jointSpeed нельзя, это разные системы отсчёта.
    public float lumbarTargetTilt = 0f;
    // P и D плюсовые. В формуле ниже они входят как −P и −D, потому что
    // ошибка по-прежнему target − tilt: так сохраняется та же арифметика,
    // что у рабочей пары P=−2, D=−0.3, без смены знака демпфера.
    public float lumbarPGain = 2.0f;
    public float lumbarDGain = 0.3f;
    public float lumbarErrorReferenceDegrees = 25f;
    [Header("Голеностоп как маятник")]
    // Момент против смещения CoM, а не против угла сустава.
    // Опрокидывающий момент равен m*g*d ≈ 687*d Н·м. У тела ростом 1.75 м
    // центр масс ниже, инерция меньше: прежние P=7 и D=4 оставляли качку
    // торса и СКО смещения хуже эталона. P=14 держит CoM плотнее, D=8
    // гасит скорость после стартового приседания.
    public float ankleComP = 14f;
    public float ankleComD = 8f;
    public float ankleLimitMargin = 15f;

    [Header("Скорость активации мышц")]
    public float muscleActivationSpeed = 20f;

    [Header("Мышцы ног")]
    public Muscle leftHipFlexor;
    public Muscle leftHipExtensor;
    public Muscle rightHipFlexor;
    public Muscle rightHipExtensor;
    public Muscle leftKneeFlexor;
    public Muscle leftKneeExtensor;
    public Muscle rightKneeFlexor;
    public Muscle rightKneeExtensor;
    public Muscle leftAnkleFlexor;
    public Muscle leftAnkleExtensor;
    public Muscle rightAnkleFlexor;
    public Muscle rightAnkleExtensor;

    [Header("Мышцы поясницы")]
    public Muscle lumbarFlexor;
    public Muscle lumbarExtensor;

    [Header("Мышцы шеи и головы")]
    public Muscle neckFlexor;
    public Muscle neckExtensor;
    public Muscle headFlexor;
    public Muscle headExtensor;

    private HingeJoint2D leftHipJoint;
    private HingeJoint2D rightHipJoint;
    private HingeJoint2D leftKneeJoint;
    private HingeJoint2D rightKneeJoint;
    private HingeJoint2D leftAnkleJoint;
    private HingeJoint2D rightAnkleJoint;
    private HingeJoint2D lumbarJoint;
    private HingeJoint2D neckJoint;
    private HingeJoint2D headJoint;

    private VestibularSystem vestibularSystem;
    private CenterOfMassCalculator comCalculator;
    private BodyStateEstimator bodyState;

    void Awake()
    {
        vestibularSystem = GetComponent<VestibularSystem>();
        comCalculator = GetComponent<CenterOfMassCalculator>();
        bodyState = GetComponent<BodyStateEstimator>();

        leftHipJoint = GetJoint("LeftLegThigh");
        rightHipJoint = GetJoint("RightLegThigh");
        leftKneeJoint = GetJoint("LeftLegShin");
        rightKneeJoint = GetJoint("RightLegShin");
        leftAnkleJoint = GetJoint("LeftLegFoot");
        rightAnkleJoint = GetJoint("RightLegFoot");
        lumbarJoint = GetJoint("Torso");
        neckJoint = GetJoint("Neck");
        headJoint = GetJoint("Head");
    }

    private HingeJoint2D GetJoint(string childName)
    {
        Transform t = transform.Find(childName);
        return t != null ? t.GetComponent<HingeJoint2D>() : null;
    }

    void FixedUpdate()
    {
        float comOffset = comCalculator != null ? comCalculator.GetCoMOffsetX() : 0f;
        float angularVelocity = vestibularSystem != null ? vestibularSystem.GetBodyAngularVelocity() : 0f;

        UpdateState(comOffset);

        if (currentState == BalanceState.Falling)
        {
            RelaxAllMuscles();
            return;
        }

        float offsetN = comOffset / Mathf.Max(0.01f, comOffsetReference);
        float tiltSpeedN = angularVelocity / Mathf.Max(1f, tiltSpeedReference);
        float balanceSignal = Mathf.Clamp(
            comProportionalGain * offsetN + comDerivativeGain * tiltSpeedN,
            -maxBalanceSignal, maxBalanceSignal);

        // Бёдра чуть уводят таз навстречу смещению. Колени остаются мягко согнутыми
        // и в Recovery сгибаются ещё — появляется ход, которого нет у жёсткой палки.
        float hipTarget = Mathf.Clamp(hipBaseAngle - hipBalanceGain * balanceSignal, -45f, 45f);
        float kneeTarget = kneeBaseAngle;
        if (currentState == BalanceState.Recovery)
            kneeTarget -= kneeRecoveryFlex;

        float comVelX = bodyState != null ? bodyState.comVelocity.x : 0f;
        float velN = comVelX / Mathf.Max(0.05f, comVelocityReference);
        // CoM впереди крутит тело вперёд. Возвращает его extensor голеностопа,
        // то есть положительный сигнал в общем соглашении.
        float ankleBalance = Mathf.Clamp(
            ankleComP * offsetN + ankleComD * velN,
            -1f, 1f);

        ControlJointToAngle(leftHipJoint, hipTarget, hipPGain, hipDGain, leftHipFlexor, leftHipExtensor);
        ControlJointToAngle(rightHipJoint, hipTarget, hipPGain, hipDGain, rightHipFlexor, rightHipExtensor);

        ControlJointToAngle(leftKneeJoint, kneeTarget, kneePGain, kneeDGain, leftKneeFlexor, leftKneeExtensor);
        ControlJointToAngle(rightKneeJoint, kneeTarget, kneePGain, kneeDGain, rightKneeFlexor, rightKneeExtensor);

        ControlAnkleForBalance(leftAnkleJoint, ankleBalance, leftAnkleFlexor, leftAnkleExtensor);
        ControlAnkleForBalance(rightAnkleJoint, ankleBalance, rightAnkleFlexor, rightAnkleExtensor);

        ControlLumbarToWorldUpright();

        // Шея и голова держат вертикаль обычным PD, без мёртвой зоны: в ней
        // мышцы расслаблялись полностью и голова успевала завалиться на упор.
        ControlJointToAngle(neckJoint, 0f, neckPGain, neckDGain, neckFlexor, neckExtensor);
        ControlJointToAngle(headJoint, 0f, neckPGain, neckDGain, headFlexor, headExtensor);
    }

    private void UpdateState(float comOffset)
    {
        float absOffset = Mathf.Abs(comOffset);
        if (absOffset > fallCoMOffset) currentState = BalanceState.Falling;
        else if (absOffset > recoveryCoMOffset) currentState = BalanceState.Recovery;
        else currentState = BalanceState.Balancing;
    }

    // Безразмерный сигнал PD в диапазоне [-1, 1] — прямо годится в активацию мышцы.
    private float ComputeSignal(HingeJoint2D joint, float targetAngle, float pGain, float dGain)
    {
        float error = Mathf.DeltaAngle(joint.jointAngle, targetAngle) / errorReferenceDegrees;
        float speed = joint.jointSpeed / speedReferenceDegPerSec;
        return Mathf.Clamp(pGain * error - dGain * speed, -1f, 1f);
    }

    // Единое соглашение для всех суставов: положительный сигнал означает
    // «увеличить угол сустава», и это работа extensor. Проверено на колене:
    // момент против часовой (flexor) уменьшает jointAngle.
    private void ApplySignal(float signal, Muscle flexor, Muscle extensor)
    {
        if (signal > 0f)
        {
            UpdateMuscle(extensor, signal);
            UpdateMuscle(flexor, 0f);
        }
        else
        {
            UpdateMuscle(extensor, 0f);
            UpdateMuscle(flexor, -signal);
        }
    }

    private void ControlJointToAngle(HingeJoint2D joint, float targetAngle, float pGain, float dGain, Muscle flexor, Muscle extensor)
    {
        if (joint == null || flexor == null || extensor == null) return;
        ApplySignal(ComputeSignal(joint, targetAngle, pGain, dGain), flexor, extensor);
    }

    // Поясница держит грудь к мировой вертикали. Ошибка и скорость —
    // абсолютный наклон и угловая скорость торса, не угол сустава:
    // когда таз заваливается, грудь должна упереться, а не ехать вместе с ним.
    // Входы как у рабочей версии (error = target − tilt, скорость мировая).
    // Коэффициенты плюсовые, поэтому в P*e − D*ω они входят со знаком минус:
    // это ровно старые P=−2 и D=−0.3, а не новый демпфер.
    private void ControlLumbarToWorldUpright()
    {
        if (lumbarFlexor == null || lumbarExtensor == null) return;
        if (vestibularSystem == null) return;

        float tilt = vestibularSystem.GetBodyTilt();
        float angVel = vestibularSystem.GetBodyAngularVelocity();
        float error = Mathf.DeltaAngle(tilt, lumbarTargetTilt) / Mathf.Max(1f, lumbarErrorReferenceDegrees);
        float speed = angVel / Mathf.Max(1f, speedReferenceDegPerSec);
        float signal = Mathf.Clamp((-lumbarPGain) * error - (-lumbarDGain) * speed, -1f, 1f);
        ApplySignal(signal, lumbarFlexor, lumbarExtensor);
    }

    // Голеностоп — не позиционный сустав, а маятниковый привод: он получает
    // только момент против смещения центра масс. Пружину и вязкость к целевому
    // углу пришлось убрать: стопа прижата к земле, поэтому момент мышцы уходит
    // не в поворот сустава, а в разворот всего тела, и «демпфирование» угла
    // раскачивало человека до падения за 3 секунды. Вязкость даёт JointFriction.
    // Команда, которая вдавливает сустав в последние ankleLimitMargin градусов
    // до упора, обнуляется: иначе стопа клинит на ±45° и момента «назад» нет.
    private void ControlAnkleForBalance(HingeJoint2D joint, float balanceCommand, Muscle flexor, Muscle extensor)
    {
        if (joint == null || flexor == null || extensor == null) return;
        float signal = ProtectJointLimit(joint, Mathf.Clamp(balanceCommand, -1f, 1f));
        ApplySignal(signal, flexor, extensor);
    }

    private float ProtectJointLimit(HingeJoint2D joint, float signal)
    {
        if (!joint.useLimits) return signal;

        float angle = joint.jointAngle;
        float min = joint.limits.min;
        float max = joint.limits.max;
        // Положительный сигнал увеличивает угол, отрицательный уменьшает.
        if (signal > 0f && angle >= max - ankleLimitMargin) return 0f;
        if (signal < 0f && angle <= min + ankleLimitMargin) return 0f;
        return signal;
    }

    private void UpdateMuscle(Muscle muscle, float targetActivation)
    {
        if (muscle == null) return;
        if (targetActivation == 0f)
        {
            muscle.activation = 0f; // мгновенно расслабляем
        }
        else
        {
            muscle.activation = Mathf.MoveTowards(muscle.activation, targetActivation, muscleActivationSpeed * Time.fixedDeltaTime);
        }
    }

    private void RelaxAllMuscles()
    {
        UpdateMuscle(leftHipFlexor, 0f); UpdateMuscle(leftHipExtensor, 0f);
        UpdateMuscle(rightHipFlexor, 0f); UpdateMuscle(rightHipExtensor, 0f);
        UpdateMuscle(leftKneeFlexor, 0f); UpdateMuscle(leftKneeExtensor, 0f);
        UpdateMuscle(rightKneeFlexor, 0f); UpdateMuscle(rightKneeExtensor, 0f);
        UpdateMuscle(leftAnkleFlexor, 0f); UpdateMuscle(leftAnkleExtensor, 0f);
        UpdateMuscle(rightAnkleFlexor, 0f); UpdateMuscle(rightAnkleExtensor, 0f);
        UpdateMuscle(lumbarFlexor, 0f); UpdateMuscle(lumbarExtensor, 0f);
        UpdateMuscle(neckFlexor, 0f); UpdateMuscle(neckExtensor, 0f);
        UpdateMuscle(headFlexor, 0f); UpdateMuscle(headExtensor, 0f);
    }
}