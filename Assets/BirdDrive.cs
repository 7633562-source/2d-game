using UnityEngine;

// Обстоятельства, не сила. В Update пишет только mode и facing.
// Prey — пике на метку. Wander — случайный цикл сидеть / лететь / сесть.
[DefaultExecutionOrder(-50)]
public class BirdDrive : MonoBehaviour
{
    public enum Kind
    {
        Prey = 0,
        Wander = 1
    }

    [Header("Политика")]
    public Kind kind = Kind.Prey;
    [Tooltip("Стенд включает явно. Play и так не headless.")]
    public bool allowHeadless;

    [Header("Добыча")]
    public Transform prey;
    [Tooltip("Заметить добычу и взлететь, м.")]
    public float noticeRange = 40f;
    [Tooltip("Начать пике, м по X.")]
    public float stoopRange = 6f;
    [Tooltip("Пике только выше этого над стойкой, м.")]
    public float stoopMinHeight = 0.45f;
    [Tooltip("Считать, что долетела / проскочила, м.")]
    public float arriveSlop = 1.2f;
    [Tooltip("После посадки сидеть, прежде чем снова взлететь, с.")]
    public float sitHold = 2.2f;

    [Header("Случайный полёт")]
    public int wanderSeed = 1;
    public float sitMin = 1.4f;
    public float sitMax = 3.6f;
    public float flyMin = 2.2f;
    public float flyMax = 5.5f;
    [Tooltip("Enter Glide this many seconds before the fly phase ends.")]
    public float glideLead = 1.1f;
    public float turnMin = 1.1f;
    public float turnMax = 2.8f;
    [Tooltip("Развернуть к дому, если улетела дальше, м. 0 — без поводка.")]
    public float leash = 7f;
    [Tooltip("Hold Glide facing while |x − homeX| is inside this, m.")]
    public float homeFaceDeadzone = 0.15f;
    [Tooltip("Chicken walk bout, s. Crow ignores this.")]
    public float walkMin = 1.8f;
    public float walkMax = 3.8f;

    [Header("Threat")]
    [Tooltip("Marker ahead along facing. Empty = no alarm.")]
    public Transform threat;
    [Tooltip("See the marker and cry, m.")]
    public float threatRange = 12f;
    [Tooltip("Keep Fly this long after see/hear, s.")]
    public float alertHold = 2.2f;

    [Header("Life")]
    [Tooltip("Self vs hostile. Stronger other → Flee; weaker → Pursue.")]
    public float ownStrength = 1f;
    [Tooltip("v1 stand-in until FactionMember. Threat marker strength.")]
    public float threatStrength = 2f;
    [Tooltip("v1 stand-in until FactionMember. Prey marker strength.")]
    public float preyStrength = 0.5f;

    public LifeState Life { get; private set; } = LifeState.Forage;
    public int LifeSwitchCount { get; private set; }

    public int TakeoffCount => wander != null ? wander.takeoffCount : 0;
    public int WalkCount => chicken != null ? chicken.walkCount : 0;
    public int PeckCount => chicken != null ? chicken.peckCount : 0;
    public int LandCount => wander != null ? wander.landCount : chicken != null ? chicken.sitCount : 0;
    public int FacingFlipCount => wander != null
        ? wander.facingFlipCount
        : chicken != null ? chicken.facingFlipCount : 0;
    public int PerchLandCount => wander != null ? wander.perchLandCount : 0;
    public int DirtRejectCount => wander != null ? wander.dirtRejectCount : 0;
    public int CryCount => voice != null ? voice.CryCount : 0;

    private BirdController control;
    private BirdSensors sensors;
    private OrganismVoice voice;
    private readonly SoundEvent[] heardBuf = new SoundEvent[8];
    private float alertUntil;
    private Rigidbody2D bodyRb;
    private bool wasAirborne;
    private float restUntil;
    private float homeX;
    private bool hasPerch;
    private BirdWander wander;
    private ChickenWander chicken;
    private Bird bodyKind;
    private BirdMode lastPreyMode = BirdMode.Sit;

    void Awake()
    {
        // Controller Awake is order 0; BodyRb is not ready here (−50).
        control = GetComponent<BirdController>();
        sensors = GetComponent<BirdSensors>();
        bodyKind = GetComponent<Bird>();
        voice = GetComponent<OrganismVoice>();
        if (voice == null)
            voice = gameObject.AddComponent<OrganismVoice>();
    }

    void Start()
    {
        bodyRb = control != null ? control.BodyRb : null;
        if (bodyRb == null)
        {
            Transform body = transform.Find("Body");
            bodyRb = body != null ? body.GetComponent<Rigidbody2D>() : null;
        }

        homeX = bodyRb != null ? bodyRb.position.x : transform.position.x;
        if (control != null)
        {
            control.takeoffDelay = 0f;
            control.turnAfter = 0f;
        }

        if (IsChicken())
        {
            chicken = new ChickenWander();
            float henFace = control != null ? control.FacingSign() : 1f;
            chicken.Reset(wanderSeed, Time.time, sitMin, sitMax, henFace);
            if (control != null)
                control.mode = BirdMode.Sit;
            return;
        }

        if (kind == Kind.Wander)
        {
            wander = new BirdWander();
            float face = control != null ? control.FacingSign() : 1f;
            wander.Reset(wanderSeed, Time.time, sitMin, sitMax, face);
            if (control != null)
                control.mode = BirdMode.Sit;
            return;
        }

        restUntil = Time.time + Mathf.Max(sitHold, 3f);
        if (control != null)
            control.mode = BirdMode.Sit;
    }

    void Update()
    {
        if (HeadlessTrial.Active && !allowHeadless) return;
        if (control == null || sensors == null) return;

        EnterLife(ChooseLife());
        if (Life == LifeState.Flee)
            TickFlee();
        else if (Life == LifeState.Pursue)
            TickPrey();
        else if (Life == LifeState.Sleep)
            WriteSleepBody();
        else
            TickForage();
    }

    private void EnterLife(LifeState next)
    {
        if (next == Life)
            return;
        Life = next;
        LifeSwitchCount++;
    }

    private LifeState ChooseLife()
    {
        float selfX = bodyRb != null ? bodyRb.position.x : transform.position.x;
        Vector2 pos = bodyRb != null ? bodyRb.position : (Vector2)transform.position;
        float dx;
        bool sawThreat = threat != null && OrganismAlert.SeesAhead(
            selfX, control.FacingSign(), threat.position.x, threatRange, 0.12f, out dx);
        bool sawPrey = prey != null
            && Mathf.Abs(prey.position.x - selfX) < noticeRange;
        bool heard = voice != null && OrganismAlert.HearsForeignAlarm(
            voice.SourceId, pos, Time.time, 0.6f, heardBuf);
        bool stronger = sawThreat && OrganismLife.HostileIsStronger(ownStrength, threatStrength);
        bool weaker = sawPrey && !OrganismLife.HostileIsStronger(ownStrength, preyStrength);
        return OrganismLife.ChooseWake(stronger, weaker, heard, Time.time < alertUntil);
    }

    private void TickForage()
    {
        if (IsChicken())
        {
            TickChicken();
            return;
        }

        if (kind == Kind.Wander)
        {
            TickWander();
            return;
        }

        if (sensors.bothGrounded)
            WritePreyMode(BirdMode.Sit, 0f);
    }

    private void WriteSleepBody()
    {
        if (control != null)
            control.mode = BirdMode.Sit;
    }

    private bool IsChicken()
    {
        return bodyKind != null && bodyKind.kind == BirdKind.Chicken;
    }

    // Plant handshake: one slot. Writes homeX only.
    public void BindPerch(Vector2 slot)
    {
        homeX = slot.x;
        hasPerch = true;
    }

    private void TickWander()
    {
        if (wander == null) return;

        Vector2 pos = bodyRb != null ? bodyRb.position : (Vector2)transform.position;
        bool onPerch = hasPerch && sensors.treeSupport;
        wander.Step(
            sensors.bothGrounded, pos.x, homeX, Time.time,
            sitMin, sitMax, flyMin, flyMax, glideLead,
            turnMin, turnMax, leash,
            hasPerch, onPerch, homeFaceDeadzone,
            out BirdMode mode, out float face);
        control.mode = mode;
        if (control.FacingSign() != face)
            control.SetFacing(face);
    }

    private void TickChicken()
    {
        if (chicken == null) return;

        Vector2 pos = bodyRb != null ? bodyRb.position : (Vector2)transform.position;
        chicken.Step(
            pos.x, homeX, Time.time,
            sitMin, sitMax, walkMin, walkMax,
            turnMin, turnMax, leash,
            out BirdMode mode, out float face);
        control.mode = mode;
        if (control.FacingSign() != face)
            control.SetFacing(face);
    }

    private void TickPrey()
    {
        bool grounded = sensors.bothGrounded;
        float height = bodyRb != null
            ? bodyRb.position.y - Bird.StanceRootY(bodyKind)
            : 0f;

        if (!grounded)
            wasAirborne = true;
        else if (wasAirborne)
        {
            wasAirborne = false;
            restUntil = Time.time + sitHold;
        }

        if (prey == null)
        {
            if (grounded)
                WritePreyMode(BirdMode.Sit, 0f);
            return;
        }

        float selfX = bodyRb != null ? bodyRb.position.x : transform.position.x;
        float dx = prey.position.x - selfX;
        float dist = Mathf.Abs(dx);
        bool ahead = dx * control.FacingSign() > arriveSlop * 0.25f;

        if (grounded)
        {
            if (Time.time < restUntil)
            {
                WritePreyMode(BirdMode.Sit, dx);
                return;
            }

            // Face on takeoff, not every frame — else Glide dies on overshoot.
            if (dist > arriveSlop && dist < noticeRange)
                WritePreyMode(BirdMode.Fly, dx);
            else
                WritePreyMode(BirdMode.Sit, dx);
            return;
        }

        if (ahead && dist < stoopRange && height > stoopMinHeight)
            WritePreyMode(BirdMode.Attack, dx);
        else if (!ahead || dist < arriveSlop)
            WritePreyMode(BirdMode.Glide, dx);
        else
            WritePreyMode(BirdMode.Fly, dx);
    }

    private void FacePrey(float dx)
    {
        if (Mathf.Abs(dx) <= 0.12f) return;
        control.SetFacing(dx > 0f ? 1f : -1f);
    }

    private void WritePreyMode(BirdMode next, float dx)
    {
        bool leaveSit = lastPreyMode == BirdMode.Sit && next != BirdMode.Sit;
        bool enterFly = next == BirdMode.Fly && lastPreyMode != BirdMode.Fly;
        bool enterAttack = next == BirdMode.Attack && lastPreyMode != BirdMode.Attack;
        if (leaveSit || enterFly || enterAttack)
            FacePrey(dx);
        control.mode = next;
        lastPreyMode = next;
    }

    // Flee body: cry if the stronger hostile is ahead, face away, Fly (hen: Walk).
    private void TickFlee()
    {
        if (control == null)
            return;

        float selfX = bodyRb != null ? bodyRb.position.x : transform.position.x;
        Vector2 pos = bodyRb != null ? bodyRb.position : (Vector2)transform.position;
        float dx = 0f;
        bool saw = threat != null && OrganismAlert.SeesAhead(
            selfX, control.FacingSign(), threat.position.x, threatRange, 0.12f, out dx);
        bool heard = voice != null && OrganismAlert.HearsForeignAlarm(
            voice.SourceId, pos, Time.time, 0.6f, heardBuf);

        if (saw)
        {
            if (voice != null)
                voice.Cry(VoiceKind.Alarm);
            control.SetFacing(dx > 0f ? -1f : 1f);
            alertUntil = Time.time + alertHold;
        }
        else if (heard)
            alertUntil = Time.time + alertHold;

        control.mode = IsChicken() ? BirdMode.Walk : BirdMode.Fly;
    }
}
