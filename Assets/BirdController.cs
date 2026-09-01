using UnityEngine;

public enum BirdMode
{
    Stand = 0,
    Walk = 1,
    Fly = 2,
    Glide = 3,
    // Отдых на опоре: крылья сложены, сама не взлетает.
    Sit = 4,
    // Пике на добычу: меньше подъёма, больше тяги. Не удар.
    Attack = 5,
    // Ground peck: neck dips, beak may Hurt. Not Fly.
    Peck = 6
}

// Управление птицей. Моменты — только через Muscle.ApplySignal.
// Подъём даёт BirdFlight на корпусе, пока режим Fly/Glide.
[DefaultExecutionOrder(0)]
public class BirdController : MonoBehaviour
{
    [Header("Режим")]
    // Стык с агентом ai: мозг пишет только mode (закон ai.mdc).
    // Формулу подъёма и взмах отсюда не отдаём.
    public BirdMode mode = BirdMode.Stand;
    // Таймер стенда/демо, не мозг. 0 — сам не взлетать (посадку не срывать).
    [Tooltip("Через столько секунд Stand сам переходит в Fly. 0 — не взлетать.")]
    public float takeoffDelay = 2f;
    // Сагиттальный разворот: не «улица влево», а нос в −X. Не scale тела.
    [Tooltip("+1 — нос в +X, −1 — нос в −X.")]
    public float facing = 1f;
    [Tooltip("Через столько секунд развернуться. 0 — не разворачиваться самим.")]
    public float turnAfter = 0f;
    [Tooltip("Сборка без суставов: крылья крутим как картинку.")]
    public bool flockRig;
    [Tooltip("Сдвиг фазы взмаха, с. Чтобы стая не махала хором.")]
    public float flapPhaseOffset;

    [Header("Взмах")]
    [Tooltip("Частота взмаха, Гц. Ворона ~5, здесь чуть выше: 2D-размах короче.")]
    public float flapFrequency = 7f;
    [Tooltip("Амплитуда плеча вокруг середины, градусы.")]
    public float flapAmplitude = 48f;
    [Tooltip("Середина хода плеча. ~90° — крыло вдоль спины (сустав смотрит в −X).")]
    public float flapMidAngle = 90f;
    [Tooltip("Доля периода на вниз. Меньше 0.5 — вниз быстрее, больше подъёма.")]
    [Range(0.25f, 0.5f)]
    public float downstrokeFraction = 0.38f;
    [Tooltip("Сгиб локтя на вверхнем махе: крыло короче, меньше отрицательного подъёма.")]
    public float upstrokeElbowFlex = 40f;
    [Tooltip("Перо на кисти при вверхнем махе, градусы. Ребро к потоку.")]
    public float upstrokeWristFeather = 35f;
    [Tooltip("Доля момента плеча на силовой ход. SPD к углу на 7 Гц замирает (bird_fly2).")]
    [Range(0f, 1f)]
    public float flapStrokeGain = 0.45f;
    [Tooltip("Подмешивание слежения за кинематической целью плеча.")]
    public float flapTrackP = 0.35f;

    [Header("Планирование")]
    public float glideShoulder = 95f;
    public float glideElbow = -8f;

    [Header("Стойка ног")]
    public float standHipAngle = 0f;
    public float standKneeAngle = 10f;
    public float standAnkleAngle = 0f;
    [Tooltip("Мировой тангаж корпуса в стойке. 0 — горизонталь.")]
    public float standBodyPitch = 0f;
    public float tuckHipAngle = 50f;
    public float tuckKneeAngle = 85f;
    [Tooltip("Сгиб ляжки в Sit, картинка. Физика корпуса не приседает отдельно.")]
    public float sitHipAngle = 28f;

    [Header("Походка")]
    public float walkFrequency = 2.5f;
    public float walkHipAmp = 22f;
    public float walkKneeAmp = 30f;
    [Tooltip("Тяга шага flock, Н. Должна быть больше трения опоры, иначе стоит.")]
    public float walkThrust = 2.2f;
    [Tooltip("Потолок скорости шага, м/с. Иначе съезжает с площадки стенда.")]
    public float walkSpeed = 0.7f;
    [Tooltip("Включать опору, когда высота над стойкой меньше этого, м.")]
    public float perchApproachHeight = 0.5f;
    [Tooltip("Считать отрыв только выше этого. Иначе Fly у земли сразу садится.")]
    public float takeoffClearance = 0.28f;

    [Header("PD суставов")]
    public float jointPGain = 2f;
    public float jointDGain = 0.35f;
    public float jointErrorRef = 25f;
    public float speedReferenceDegPerSec = 100f;

    [Header("Тангаж хвостом")]
    public float pitchPGain = 1.6f;
    public float pitchDGain = 0.35f;
    public float pitchTarget = 8f;
    public float pitchErrorRef = 25f;

    [Header("Голова")]
    public float headTargetTilt = 0f;
    [Tooltip("Neck dip on Peck, degrees. Negative is beak toward the dirt.")]
    public float peckDipDegrees = 48f;

    [Header("Голеностоп от CoM")]
    public float ankleComP = 14f;
    public float ankleComD = 8f;
    public float comOffsetReference = 0.05f;

    [Header("Отладка")]
    public float debugLiftY;
    public float debugFlapPhase;
    public bool debugDownstroke;

    public Muscle leftShoulderFlexor, leftShoulderExtensor;
    public Muscle rightShoulderFlexor, rightShoulderExtensor;
    public Muscle leftElbowFlexor, leftElbowExtensor;
    public Muscle rightElbowFlexor, rightElbowExtensor;
    public Muscle leftWristFlexor, leftWristExtensor;
    public Muscle rightWristFlexor, rightWristExtensor;
    public Muscle leftHipFlexor, leftHipExtensor;
    public Muscle rightHipFlexor, rightHipExtensor;
    public Muscle leftKneeFlexor, leftKneeExtensor;
    public Muscle rightKneeFlexor, rightKneeExtensor;
    public Muscle leftAnkleFlexor, leftAnkleExtensor;
    public Muscle rightAnkleFlexor, rightAnkleExtensor;
    public Muscle neckFlexor, neckExtensor;
    public Muscle headFlexor, headExtensor;
    public Muscle beakFlexor, beakExtensor;
    public Muscle tailFlexor, tailExtensor;

    public HingeJoint2D leftShoulderJoint, rightShoulderJoint;
    public HingeJoint2D leftElbowJoint, rightElbowJoint;
    public HingeJoint2D leftWristJoint, rightWristJoint;
    public HingeJoint2D leftHipJoint, rightHipJoint;
    public HingeJoint2D leftKneeJoint, rightKneeJoint;
    public HingeJoint2D leftAnkleJoint, rightAnkleJoint;
    public HingeJoint2D neckJoint, headJoint, beakJoint, tailJoint;

    private BirdSensors sensors;
    private BirdFlight flight;
    private Bird bird;
    private Rigidbody2D bodyRb;
    // Стая читает корпус без Find на Bind.
    public Rigidbody2D BodyRb => bodyRb;
    private float standElapsed;
    private bool wasAirborne;
    private float leftShoulderI, rightShoulderI;
    private float leftElbowI, rightElbowI;
    private float leftWristI, rightWristI;
    private float leftHipI, rightHipI;
    private float leftKneeI, rightKneeI;
    private float leftAnkleI, rightAnkleI;
    private float neckI, headI, beakI;
    private float flockWingFold;
    private float flockNeckZ;
    private float flockHeadZ;
    private float flockUlnaZ;
    private float flockLeftThighZ;
    private float flockRightThighZ;
    private bool didAutoTurn;

    void Awake()
    {
        sensors = GetComponent<BirdSensors>();
        flight = GetComponentInChildren<BirdFlight>();
        bird = GetComponent<Bird>();
        Transform body = transform.Find("Body");
        bodyRb = body != null ? body.GetComponent<Rigidbody2D>() : null;
        if (bird != null && bird.rig == BirdRig.Flock)
        {
            flockRig = true;
            if (sensors != null) sensors.drivenExternally = true;
            if (flight != null) flight.drivenExternally = true;
        }
    }

    public float FacingSign()
    {
        return facing >= 0f ? 1f : -1f;
    }

    // Мозг или спавн задаёт сторону. Картинка на Look, силы — в Tick / BirdFlight.
    public void SetFacing(float sign)
    {
        facing = sign >= 0f ? 1f : -1f;
        if (bird != null)
            bird.ApplyLookFacing(facing);
    }

    public bool IsFlightMode()
    {
        return mode == BirdMode.Fly || mode == BirdMode.Glide || mode == BirdMode.Attack;
    }

    void FixedUpdate()
    {
        if (sensors == null) return;

        if (mode == BirdMode.Stand && takeoffDelay > 0.01f)
        {
            standElapsed += Time.fixedDeltaTime;
            if (standElapsed >= takeoffDelay)
                mode = BirdMode.Fly;
        }

        if (turnAfter > 0.01f && !didAutoTurn && Time.fixedTime >= turnAfter)
        {
            SetFacing(-FacingSign());
            didAutoTurn = true;
        }

        bool flying = IsFlightMode();
        if (flockRig)
        {
            sensors.Tick();
            TickFlock(flying);
            if (flight != null)
                flight.Tick();
            SumDebugLift();
            return;
        }

        DriveWings();
        DriveLegs(flying);
        DriveNeckAndHead();
        DriveTail(flying);
        SumDebugLift();
    }

    // Стая: нет мышц и шарниров. Взмах — локальный поворот картинки,
    // подъём по-прежнему в BirdFlight по debugDownstroke.
    private void TickFlock(bool flying)
    {
        debugDownstroke = false;
        float shoulder = flapMidAngle;
        float elbow = -12f;
        if (mode == BirdMode.Fly)
            EvaluateFlap(out shoulder, out elbow, out _, out debugDownstroke);
        else if (mode == BirdMode.Glide)
        {
            shoulder = glideShoulder;
            elbow = glideElbow;
        }
        else if (mode == BirdMode.Attack)
        {
            // Пике: крыло прижато, подъём считает BirdFlight, не взмах.
            shoulder = flapMidAngle;
            elbow = -42f;
        }
        else if (mode == BirdMode.Sit || mode == BirdMode.Peck)
        {
            shoulder = flapMidAngle;
            elbow = -62f;
        }
        else
        {
            shoulder = flapMidAngle;
            elbow = -50f;
        }

        CacheFlockPose(shoulder, elbow, flying);

        // Flat height is vs dirt (y = −2). A tree slot sits higher, so
        // NearPerch is the approach gate on a branch (BIRD-5).
        float height = bodyRb != null ? bodyRb.position.y - Bird.StanceRootY(bird) : 99f;
        bool nearBranch = sensors.nearPerch;
        bool approaching = wasAirborne
            && sensors.bodyVelocity.y < 0.25f
            && (height < perchApproachHeight || nearBranch);
        // Опору включать до удара: иначе садится грудкой и кувыркается.
        // Не включать у самой стойки до отрыва — иначе Fly сразу гасится.
        SetPerchEnabled(!flying || approaching);

        if (!sensors.bothGrounded && height > takeoffClearance)
            wasAirborne = true;
        // Садиться только после отрыва: иначе Fly на земле сразу гасится.
        // Горизонталь не режем жёстко: планирование ещё несёт скорость.
        if (flying && wasAirborne && sensors.bothGrounded
            && sensors.bodyVelocity.y < 0.5f
            && Mathf.Abs(sensors.bodyVelocity.x) < 3.5f)
        {
            mode = BirdMode.Sit;
            takeoffDelay = 0f;
            wasAirborne = false;
            SettleLanding();
        }
    }

    // Импульс в FixedUpdate: погасить удар о землю, не мотор сустава.
    private void SettleLanding()
    {
        if (bodyRb == null) return;
        float mass = bodyRb.mass;
        Vector2 v = bodyRb.linearVelocity;
        float killX = -v.x * mass * 0.92f;
        float killY = v.y < 0f ? -v.y * mass : 0f;
        bodyRb.AddForce(new Vector2(killX, killY), ForceMode2D.Impulse);
        float wRad = bodyRb.angularVelocity * Mathf.Deg2Rad;
        bodyRb.AddTorque(-wRad * bodyRb.inertia, ForceMode2D.Impulse);
    }

    // Углы с шага физики, localRotation — в LateUpdate: картинка не 200 Гц.
    private void CacheFlockPose(float shoulder, float elbow, bool flying)
    {
        flockWingFold = -90f + (shoulder - flapMidAngle);
        flockUlnaZ = mode == BirdMode.Fly && !debugDownstroke ? -upstrokeElbowFlex : elbow;

        if (mode == BirdMode.Peck && sensors.bothGrounded)
        {
            flockLeftThighZ = -sitHipAngle * 0.35f;
            flockRightThighZ = -sitHipAngle * 0.35f;
            float bob = 7f * Mathf.Sin(Time.fixedTime * 14f);
            flockNeckZ = -peckDipDegrees + bob;
            flockHeadZ = flockNeckZ * 0.35f;
            return;
        }

        flockNeckZ = 0f;
        flockHeadZ = 0f;
        if (mode == BirdMode.Walk && sensors.bothGrounded)
        {
            float w = (Mathf.PI * 2f) * walkFrequency * Time.fixedTime;
            flockLeftThighZ = -walkHipAmp * Mathf.Sin(w);
            flockRightThighZ = -walkHipAmp * Mathf.Sin(w + Mathf.PI);
            if (bodyRb != null && FacingSign() * bodyRb.linearVelocity.x < walkSpeed)
                bodyRb.AddForce(new Vector2(walkThrust * FacingSign(), 0f), ForceMode2D.Force);
            return;
        }

        if (mode == BirdMode.Sit && sensors.bothGrounded)
        {
            flockLeftThighZ = -sitHipAngle;
            flockRightThighZ = -sitHipAngle;
            return;
        }

        bool tuck = flying && !sensors.bothGrounded;
        float z = tuck ? -tuckHipAngle : 0f;
        flockLeftThighZ = z;
        flockRightThighZ = z;
    }

    void LateUpdate()
    {
        if (!flockRig || bird == null) return;

        Quaternion wingRot = Quaternion.Euler(0f, 0f, flockWingFold);
        if (bird.leftWingRoot != null) bird.leftWingRoot.localRotation = wingRot;
        if (bird.rightWingRoot != null) bird.rightWingRoot.localRotation = wingRot;
        Quaternion ulnaRot = Quaternion.Euler(0f, 0f, flockUlnaZ);
        if (bird.leftUlna != null) bird.leftUlna.localRotation = ulnaRot;
        if (bird.rightUlna != null) bird.rightUlna.localRotation = ulnaRot;
        SetThigh(bird.leftThigh, flockLeftThighZ);
        SetThigh(bird.rightThigh, flockRightThighZ);
        if (bird.neckVisual != null)
            bird.neckVisual.localRotation = Quaternion.Euler(0f, 0f, flockNeckZ);
        if (bird.headVisual != null)
            bird.headVisual.localRotation = Quaternion.Euler(0f, 0f, flockHeadZ);
    }

    private static void SetThigh(Transform thigh, float zDeg)
    {
        if (thigh != null)
            thigh.localRotation = Quaternion.Euler(0f, 0f, zDeg);
    }

    private void SetPerchEnabled(bool enabled)
    {
        if (bird != null && bird.perchCollider != null)
            bird.perchCollider.enabled = enabled;
    }

    private void DriveWings()
    {
        float shoulder = flapMidAngle;
        float elbow = -12f;
        float wrist = 0f;
        debugDownstroke = false;

        if (mode == BirdMode.Fly)
        {
            EvaluateFlap(out shoulder, out elbow, out wrist, out debugDownstroke);
            // Жест взмаха. Подъём считает BirdFlight по этому же такту,
            // скорость крыла на тягу больше не влияет.
            DriveShoulderStroke(leftShoulderJoint, shoulder, leftShoulderFlexor, leftShoulderExtensor);
            DriveShoulderStroke(rightShoulderJoint, shoulder, rightShoulderFlexor, rightShoulderExtensor);
            ControlJointStable(leftElbowJoint, elbow, leftElbowFlexor, leftElbowExtensor, ref leftElbowI);
            ControlJointStable(rightElbowJoint, elbow, rightElbowFlexor, rightElbowExtensor, ref rightElbowI);
            ControlJointStable(leftWristJoint, wrist, leftWristFlexor, leftWristExtensor, ref leftWristI);
            ControlJointStable(rightWristJoint, wrist, rightWristFlexor, rightWristExtensor, ref rightWristI);
            return;
        }
        else if (mode == BirdMode.Glide)
        {
            shoulder = glideShoulder;
            elbow = glideElbow;
            wrist = 0f;
        }
        else if (mode == BirdMode.Attack)
        {
            shoulder = flapMidAngle;
            elbow = -42f;
            wrist = 8f;
        }
        else if (mode == BirdMode.Sit || mode == BirdMode.Peck)
        {
            shoulder = flapMidAngle;
            elbow = -62f;
            wrist = 12f;
        }
        else
        {
            // Стойка и шаг: крыло сложено вдоль спины, не несёт.
            shoulder = flapMidAngle;
            elbow = -50f;
            wrist = 10f;
        }

        ControlJointStable(leftShoulderJoint, shoulder, leftShoulderFlexor, leftShoulderExtensor, ref leftShoulderI);
        ControlJointStable(rightShoulderJoint, shoulder, rightShoulderFlexor, rightShoulderExtensor, ref rightShoulderI);
        ControlJointStable(leftElbowJoint, elbow, leftElbowFlexor, leftElbowExtensor, ref leftElbowI);
        ControlJointStable(rightElbowJoint, elbow, rightElbowFlexor, rightElbowExtensor, ref rightElbowI);
        ControlJointStable(leftWristJoint, wrist, leftWristFlexor, leftWristExtensor, ref leftWristI);
        ControlJointStable(rightWristJoint, wrist, rightWristFlexor, rightWristExtensor, ref rightWristI);
    }

    // Вниз уменьшает jointAngle (крыло к брюху), вверх — наоборот.
    // D по собственной скорости сюда не ставить: это вязкость против маха.
    private void DriveShoulderStroke(HingeJoint2D joint, float targetAngle, Muscle flexor, Muscle extensor)
    {
        float stroke = debugDownstroke ? -1f : 1f;
        float error = 0f;
        if (joint != null)
            error = Mathf.DeltaAngle(joint.jointAngle, targetAngle) / Mathf.Max(1f, jointErrorRef);
        float raw = flapStrokeGain * stroke + flapTrackP * error;
        ApplySignal(Mathf.Clamp(raw, -1f, 1f), flexor, extensor);
    }

    // Асимметричный цикл: вниз быстрее, чем вверх. На вниз локоть раскрыт
    // (полная площадь), на вверх слегка сложен и кисть перит — иначе
    // обратный ход бьёт вниз почти так же, как прямой бьёт вверх.
    private void EvaluateFlap(out float shoulder, out float elbow, out float wrist, out bool downstroke)
    {
        float period = 1f / Mathf.Max(0.5f, flapFrequency);
        float t = Mathf.Repeat(Time.fixedTime + flapPhaseOffset, period) / period;
        debugFlapPhase = t;

        float down = Mathf.Clamp(downstrokeFraction, 0.25f, 0.5f);
        float s;
        if (t < down)
        {
            downstroke = true;
            s = Mathf.Lerp(1f, -1f, t / down);
        }
        else
        {
            downstroke = false;
            s = Mathf.Lerp(-1f, 1f, (t - down) / Mathf.Max(0.05f, 1f - down));
        }

        shoulder = flapMidAngle + flapAmplitude * s;
        elbow = downstroke ? -5f : -upstrokeElbowFlex;
        wrist = downstroke ? 0f : upstrokeWristFeather;
    }

    private void DriveLegs(bool flying)
    {
        float leftHip = standHipAngle;
        float rightHip = standHipAngle;
        float leftKnee = standKneeAngle;
        float rightKnee = standKneeAngle;
        bool onGround = sensors.leftFootGrounded || sensors.rightFootGrounded;

        if (flying && !onGround && Mathf.Abs(sensors.bodyPitch) < 25f)
        {
            leftHip = rightHip = tuckHipAngle;
            leftKnee = rightKnee = tuckKneeAngle;
            ControlJointStable(leftHipJoint, leftHip, leftHipFlexor, leftHipExtensor, ref leftHipI);
            ControlJointStable(rightHipJoint, rightHip, rightHipFlexor, rightHipExtensor, ref rightHipI);
            ControlJointStable(leftKneeJoint, leftKnee, leftKneeFlexor, leftKneeExtensor, ref leftKneeI);
            ControlJointStable(rightKneeJoint, rightKnee, rightKneeFlexor, rightKneeExtensor, ref rightKneeI);
            ControlJointStable(leftAnkleJoint, 0f, leftAnkleFlexor, leftAnkleExtensor, ref leftAnkleI);
            ControlJointStable(rightAnkleJoint, 0f, rightAnkleFlexor, rightAnkleExtensor, ref rightAnkleI);
            return;
        }

        if ((mode == BirdMode.Sit || mode == BirdMode.Peck) && onGround)
        {
            leftHip = rightHip = sitHipAngle;
            leftKnee = rightKnee = standKneeAngle + 12f;
        }

        if (mode == BirdMode.Walk)
        {
            float w = (Mathf.PI * 2f) * walkFrequency * Time.fixedTime;
            float sL = Mathf.Sin(w);
            float sR = Mathf.Sin(w + Mathf.PI);
            leftHip = standHipAngle + walkHipAmp * sL;
            rightHip = standHipAngle + walkHipAmp * sR;
            leftKnee = standKneeAngle + walkKneeAmp * Mathf.Max(0f, sL);
            rightKnee = standKneeAngle + walkKneeAmp * Mathf.Max(0f, sR);
        }

        // Несущие, но лёгкие: устойчивый PD. Инерцию лап поднимает Bird.minLimbInertia,
        // иначе знаменатель душит момент (bird_stand) или явный PD расходится (bird_stand3).
        ControlJointStable(leftHipJoint, leftHip, leftHipFlexor, leftHipExtensor, ref leftHipI);
        ControlJointStable(rightHipJoint, rightHip, rightHipFlexor, rightHipExtensor, ref rightHipI);
        ControlJointStable(leftKneeJoint, leftKnee, leftKneeFlexor, leftKneeExtensor, ref leftKneeI);
        ControlJointStable(rightKneeJoint, rightKnee, rightKneeFlexor, rightKneeExtensor, ref rightKneeI);
        DriveAnklesForBalance();
    }

    // Голеностоп на земле — не пружина к углу: стопа прижата, момент уходит
    // в разворот корпуса. Только момент против смещения CoM, как у человека.
    private void DriveAnklesForBalance()
    {
        float offsetN = sensors.comOffsetX / Mathf.Max(0.01f, comOffsetReference);
        float velN = sensors.comVelocity.x / 0.4f;
        // Как у человека: P·offsetN напрямую, без 0.06. Иначе 0.08 Н·м
        // не держат гравитационный момент головы, и птица медленно
        // перекатывается через носок (bird_stand4: тангаж −40° при живых ногах).
        float signal = Mathf.Clamp(ankleComP * offsetN + ankleComD * velN, -1f, 1f);
        ApplySignal(signal, leftAnkleFlexor, leftAnkleExtensor);
        ApplySignal(signal, rightAnkleFlexor, rightAnkleExtensor);
    }

    private void DriveNeckAndHead()
    {
        // Мировая вертикаль, как шея человека: не угол к корпусу, иначе
        // голова едет вместе с тангажем и добавляет рычаг в пикирование.
        float target = headTargetTilt;
        if (mode == BirdMode.Peck)
            target = -peckDipDegrees;
        else if (mode == BirdMode.Attack)
            target = -18f;
        ControlWorldUpright(sensors.neckTilt, sensors.neckTiltRate,
            neckJoint, neckFlexor, neckExtensor, target, ref neckI);
        ControlWorldUpright(sensors.headTilt, sensors.headTiltRate,
            headJoint, headFlexor, headExtensor, target * 0.45f, ref headI);
        if (beakJoint != null)
            ControlJointStable(beakJoint, 0f, beakFlexor, beakExtensor, ref beakI);
    }

    private void DriveTail(bool flying)
    {
        if (tailJoint == null || sensors == null) return;

        // Хвост на дочернем сегменте, корпус — connectedBody, как бедро и таз:
        // плюсовой сигнал крутит корпус носом вверх. Цель — небольшой
        // положительный тангаж, чтобы взмах нёс вперёд-вверх, а не в петлю.
        float target = flying ? pitchTarget : 4f;
        float p = flying ? pitchPGain : 1.6f;
        float error = Mathf.DeltaAngle(sensors.bodyPitch, target) / Mathf.Max(1f, pitchErrorRef);
        float speed = sensors.bodyPitchRate / Mathf.Max(1f, speedReferenceDegPerSec);
        float raw = p * error - pitchDGain * speed;
        ApplySignal(Mathf.Clamp(raw, -1f, 1f), tailFlexor, tailExtensor);
    }

    private void ControlWorldUpright(float tilt, float angVel, HingeJoint2D joint,
                                     Muscle flexor, Muscle extensor, float target, ref float cachedI)
    {
        if (joint == null || flexor == null || extensor == null) return;

        float dt = Time.fixedDeltaTime;
        float tiltForError = tilt + angVel * dt;
        if (cachedI <= 0f)
            cachedI = EffectiveInertia(joint);

        float damping = 0f;
        if (cachedI > 0f)
        {
            float k = jointDGain * flexor.maxTorque * Mathf.Rad2Deg
                      / Mathf.Max(1f, speedReferenceDegPerSec);
            damping = k * dt / cachedI;
        }

        float error = Mathf.DeltaAngle(tiltForError, target) / Mathf.Max(1f, jointErrorRef);
        float spd = angVel / Mathf.Max(1f, speedReferenceDegPerSec);
        // Сустав на самом сегменте — знак как у поясницы человека: −P, −D.
        float raw = (-jointPGain) * error - (-jointDGain) * spd;
        ApplySignal(Mathf.Clamp(raw / (1f + damping), -1f, 1f), flexor, extensor);
    }

    private void ControlJointStable(HingeJoint2D joint, float targetAngle,
                                    Muscle flexor, Muscle extensor, ref float cachedI)
    {
        if (joint == null || flexor == null || extensor == null) return;

        float dt = Time.fixedDeltaTime;
        float angleForError = joint.jointAngle + joint.jointSpeed * dt;
        if (cachedI <= 0f)
            cachedI = EffectiveInertia(joint);

        float damping = 0f;
        if (cachedI > 0f)
        {
            float k = jointDGain * flexor.maxTorque * Mathf.Rad2Deg
                      / Mathf.Max(1f, speedReferenceDegPerSec);
            damping = k * dt / cachedI;
        }

        float error = Mathf.DeltaAngle(angleForError, targetAngle) / Mathf.Max(1f, jointErrorRef);
        float speed = joint.jointSpeed / speedReferenceDegPerSec;
        float raw = jointPGain * error - jointDGain * speed;
        ApplySignal(Mathf.Clamp(raw / (1f + damping), -1f, 1f), flexor, extensor);
    }

    // Положительный сигнал — увеличить jointAngle, это extensor.
    private static void ApplySignal(float signal, Muscle flexor, Muscle extensor)
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

    private static void UpdateMuscle(Muscle muscle, float activation)
    {
        if (muscle != null)
            muscle.activation = Mathf.Clamp01(activation);
    }

    private static float EffectiveInertia(HingeJoint2D joint)
    {
        if (joint == null) return 0f;
        Rigidbody2D self = joint.attachedRigidbody;
        Rigidbody2D connected = joint.connectedBody;
        float a = self != null ? self.inertia : 0f;
        float b = connected != null ? connected.inertia : 0f;
        if (a <= 0f) return b;
        if (b <= 0f) return a;
        return 1f / (1f / a + 1f / b);
    }

    private void SumDebugLift()
    {
        debugLiftY = flight != null ? flight.lastLiftY : 0f;
    }
}
