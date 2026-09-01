using UnityEngine;

// Упрощённый полёт: не пластина на каждом звене, а одна сила на корпус.
// Взмах — выключатель: Fly даёт тягу в такт маху, Glide — несущую ~v²,
// без маха подъёма нет. Тангаж пружиной, иначе падение идёт кульбитом.
[DefaultExecutionOrder(50)]
public class BirdFlight : MonoBehaviour
{
    [Header("Тяга")]
    [Tooltip("Средний подъём в долях веса при непрерывном взмахе. >1 — медленно набирает высоту.")]
    public float hoverMean = 1.12f;
    [Tooltip("Доля тяги на вверхнем ходе. Меньше 1 — птица слегка пульсирует с махом.")]
    [Range(0f, 1f)]
    public float upstrokeLift = 0.4f;
    [Tooltip("Планирование: доля веса. <1 — пологая посадка, не зависание.")]
    [Range(0f, 1f)]
    public float glideLift = 0.55f;
    [Tooltip("Тяга вперёд вдоль корпуса, Н, в такт взмаху.")]
    public float forwardThrust = 0.35f;
    [Tooltip("Вязкость по вертикали, Н·с/м. Чтобы не улетать ракетой.")]
    public float verticalDamping = 3.5f;
    [Tooltip("Удержание тангажа в полёте, Н·м/рад. Иначе жест взмаха делает петлю.")]
    public float pitchStiffness = 6f;
    [Tooltip("Демпфер тангажа, Н·м·с/рад.")]
    public float pitchDamping = 0.45f;
    [Tooltip("Целевой тангаж в полёте, градусы. Плюс — нос вверх.")]
    public float pitchTarget = 8f;
    [Tooltip("Вспышка у земли, градусы. Гасит вертикаль перед касанием.")]
    public float flarePitch = 22f;
    [Tooltip("Высота вспышки над стойкой, м.")]
    public float flareHeight = 0.45f;
    [Tooltip("Пике: подъём в долях веса. Меньше 1 — тонет носом вперёд.")]
    public float attackLift = 0.42f;
    [Tooltip("Пике: тяга вдоль носа, Н.")]
    public float attackThrust = 1.15f;
    [Tooltip("Пике: тангаж, градусы. Минус — нос вниз при facing +1.")]
    public float attackPitch = -22f;

    [Header("Состояние (только чтение)")]
    public Vector2 lastForce;
    public float lastLiftY;

    private Rigidbody2D rb;
    private Bird bird;
    private BirdController control;
    private BirdSensors sensors;
    private BodyAero aero;
    private float lastFace = 1f;
    private float flipSoftUntil;
    private bool faceLatched;

    // true — BirdController calls Tick; own FixedUpdate returns.
    [System.NonSerialized]
    public bool drivenExternally;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        bird = GetComponentInParent<Bird>();
        control = bird != null ? bird.controller : GetComponentInParent<BirdController>();
        aero = GetComponent<BodyAero>();
        if (rb == null)
            Debug.LogError("BirdFlight нужен Rigidbody2D на корпусе.");
    }

    void FixedUpdate()
    {
        if (drivenExternally) return;
        Tick();
    }

    public void Tick()
    {
        lastForce = Vector2.zero;
        lastLiftY = 0f;
        if (rb == null) return;
        if (control == null && bird != null)
            control = bird.controller;
        if (control == null) return;
        if (sensors == null && bird != null)
            sensors = bird.sensors;

        bool flapping = control.mode == BirdMode.Fly;
        bool gliding = control.mode == BirdMode.Glide;
        bool attacking = control.mode == BirdMode.Attack;
        // Стая: пружина всегда. После касания трение опоры крутит корпус
        // (bird_flock_coast: посадка при 1°, через 0.4 с уже 55°).
        bool holdPitch = flapping || gliding || attacking || control.flockRig;

        float mass = bird != null ? bird.totalMass : rb.mass;
        float weight = mass * Mathf.Abs(Physics2D.gravity.y);

        if (flapping || gliding || attacking)
            ApplyLiftAndThrust(flapping, attacking, weight);

        if (aero != null)
            aero.Apply();

        // Без маха подъёма нет, но у стаи ориентацию всё равно держим:
        // иначе посадка идёт грудкой и кульбитом (bird_flock_coast).
        if (sensors != null && pitchStiffness > 0f && holdPitch)
            ApplyPitch(PitchGoal(flapping, gliding, attacking));
    }

    private void ApplyLiftAndThrust(bool flapping, bool attacking, float weight)
    {
        Vector2 velocity = rb.linearVelocity;
        float lift;
        float thrustN;
        float extraDrag = 0f;

        if (attacking)
        {
            // Пике: не зависание. Подъёма меньше веса — тонет по ходу носа.
            lift = weight * attackLift;
            thrustN = attackThrust;
        }
        else if (flapping)
        {
            float down = Mathf.Clamp(control.downstrokeFraction, 0.25f, 0.5f);
            float beat = control.debugDownstroke ? 1f : upstrokeLift;
            float meanBeat = down * 1f + (1f - down) * upstrokeLift;
            float envelope = beat / Mathf.Max(0.05f, meanBeat);
            lift = weight * hoverMean * envelope;
            thrustN = forwardThrust * envelope;
        }
        else
        {
            // Планирование: несущая ~ v². Малая скорость — срыв, птица садится.
            float speed = velocity.magnitude;
            float q = 0.5f * 1.2f * speed * speed;
            const float glideArea = 0.035f;
            float speedLift = q * glideArea * 1.1f;
            lift = Mathf.Min(weight * glideLift, speedLift + weight * 0.12f);
            thrustN = forwardThrust * 0.15f;
            if (speed > 0.3f)
                extraDrag = Mathf.Min(0.5f * 1.2f * speed * speed * 0.04f * 0.7f, 8f);
        }

        lift -= verticalDamping * velocity.y;
        if (lift < 0f) lift = 0f;

        // Нос — facing, не transform.right: корпус не крутим на 180° (опоры вверх).
        float face = control.FacingSign();
        float rad = rb.rotation * Mathf.Deg2Rad;
        Vector2 heading = new Vector2(Mathf.Cos(rad) * face, Mathf.Sin(rad) * face);
        Vector2 force = Vector2.up * lift + heading * thrustN;
        if (extraDrag > 0f)
            force += -velocity.normalized * extraDrag;
        rb.AddForce(force, ForceMode2D.Force);
        lastForce = force;
        lastLiftY = force.y;
    }

    private float PitchGoal(bool flapping, bool gliding, bool attacking)
    {
        // Нос вверх при полёте в −X — отрицательный мировой угол, не +8°.
        float face = control.FacingSign();
        // Sit/стойка/шаг — горизонт. Иначе у земли срабатывала вспышка 12°
        // и «сидит» держала нос как на посадке.
        if (control.mode == BirdMode.Sit
            || control.mode == BirdMode.Stand
            || control.mode == BirdMode.Walk
            || control.mode == BirdMode.Peck)
            return 0f;
        if (attacking) return attackPitch * face;
        if (flapping) return pitchTarget * face;

        float height = rb.position.y - Bird.StanceRootY(bird);
        // Same as TickFlock: dirt uses StandingRootY, a twig uses NearPerch.
        bool flare = rb.linearVelocity.y < 0.2f
            && (height < flareHeight || sensors.nearPerch);
        if (gliding)
            return (flare ? flarePitch : 6f) * face;
        return 0f;
    }

    private void ApplyPitch(float target)
    {
        // Разворот мозга меняет знак цели на 16°. Жёсткая пружина делает петлю.
        float face = control.FacingSign();
        if (!faceLatched)
        {
            lastFace = face;
            faceLatched = true;
        }
        else if (Mathf.Abs(face - lastFace) > 0.5f)
        {
            // Do not extend the window: Glide-to-homeX may flip every
            // Update around the slot (BIRD-5). Chatter would pin K at 0.4.
            if (Time.fixedTime >= flipSoftUntil)
                flipSoftUntil = Time.fixedTime + 0.4f;
            lastFace = face;
        }

        float stiff = pitchStiffness;
        if (Time.fixedTime < flipSoftUntil)
            stiff *= 0.4f;

        float errRad = Mathf.DeltaAngle(sensors.bodyPitch, target) * Mathf.Deg2Rad;
        float rateRad = sensors.bodyPitchRate * Mathf.Deg2Rad;
        float torque = stiff * errRad - pitchDamping * rateRad;
        rb.AddTorque(torque, ForceMode2D.Force);
    }
}
