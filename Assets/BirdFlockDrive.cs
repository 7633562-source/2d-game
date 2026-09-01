using UnityEngine;

// Один мозг на стаю: дешёвый Update, не луч на каждую особь.
// Пишет только mode и facing. Тягу по-прежнему даёт BirdFlight.
[DefaultExecutionOrder(-50)]
public class BirdFlockDrive : MonoBehaviour
{
    [Tooltip("Стенд включает явно.")]
    public bool allowHeadless;
    public int seed = 1;
    public float sitMin = 1.6f;
    public float sitMax = 4.2f;
    public float flyMin = 2.4f;
    public float flyMax = 5.8f;
    [Tooltip("Enter Glide this many seconds before the fly phase ends.")]
    public float glideLead = 1.1f;
    public float turnMin = 1.2f;
    public float turnMax = 3.2f;
    [Tooltip("Не улетать от центра стаи дальше этого, м.")]
    public float flockLeash = 6f;
    [Tooltip("Мозг раз в N кадров Update. 1 — каждый кадр. Поведение грубее, CPU дешевле.")]
    public int aiStride = 1;
    [Tooltip("If set, leash and facing go to the tree perch, not the spawn line.")]
    public bool useTreePerch = false;
    [Tooltip("Hold Glide facing while |x − homeX| is inside this, m.")]
    public float homeFaceDeadzone = 0.15f;
    [Tooltip("Chicken walk bout, s.")]
    public float walkMin = 1.8f;
    public float walkMax = 3.8f;
    [Tooltip("Marker ahead along facing. Empty = no alarm.")]
    public Transform threat;
    [Tooltip("See the marker and cry, m.")]
    public float threatRange = 12f;
    [Tooltip("Keep Fly this long after see/hear, s.")]
    public float alertHold = 2.2f;

    public int MemberCount { get; private set; }
    public int TakeoffCount { get; private set; }
    public int WalkCount { get; private set; }
    public int PeckCount { get; private set; }
    public int LandCount { get; private set; }
    public int FacingFlipCount { get; private set; }
    public int PerchLandCount { get; private set; }
    public int DirtRejectCount { get; private set; }
    public int CryCount { get; private set; }

    private struct Member
    {
        public BirdController control;
        public BirdSensors sensors;
        public Rigidbody2D body;
        public BirdWander wander;
        public ChickenWander chicken;
        public OrganismVoice voice;
        public float homeX;
        public float perchX;
        public bool hasPerch;
        public float alertUntil;
    }

    private Member[] members;
    private readonly SoundEvent[] heardBuf = new SoundEvent[8];
    private bool ready;
    private int frameCounter;

    public void Bind(Bird[] birds, int bindSeed)
    {
        seed = bindSeed;
        frameCounter = 0;
        if (birds == null || birds.Length == 0)
        {
            members = null;
            MemberCount = 0;
            ready = false;
            return;
        }

        members = new Member[birds.Length];
        MemberCount = birds.Length;
        float now = Time.time;
        for (int i = 0; i < birds.Length; i++)
        {
            Bird bird = birds[i];
            if (bird == null) continue;
            BirdController control = bird.controller;
            // controller уже кэширует Body — без Find на Bind.
            Rigidbody2D body = control != null ? control.BodyRb : null;
            if (body == null)
            {
                Transform bodyT = bird.transform.Find("Body");
                body = bodyT != null ? bodyT.GetComponent<Rigidbody2D>() : null;
            }

            if (control != null)
            {
                control.takeoffDelay = 0f;
                control.turnAfter = 0f;
                control.mode = BirdMode.Sit;
            }

            float face = control != null ? control.FacingSign() : 1f;
            BirdWander wander = null;
            ChickenWander hen = null;
            if (bird.kind == BirdKind.Chicken)
            {
                hen = new ChickenWander();
                hen.Reset(seed + i * 17, now + i * 0.07f, sitMin, sitMax, face);
            }
            else
            {
                wander = new BirdWander();
                // Own RNG per bird. BindPerches must not Reset.
                wander.Reset(seed + i * 17, now + i * 0.07f, sitMin, sitMax, face);
            }
            OrganismVoice voice = bird.GetComponent<OrganismVoice>();
            if (voice == null)
                voice = bird.gameObject.AddComponent<OrganismVoice>();
            members[i] = new Member
            {
                control = control,
                sensors = bird.sensors,
                body = body,
                wander = wander,
                chicken = hen,
                voice = voice,
                homeX = body != null ? body.position.x : bird.transform.position.x
            };
        }

        ready = true;
    }

    // Plant handshake: slots from PlantTree.GetPerchSlots.
    // Writes homeX / perchX only. Does not Reset wander (individual seed stays).
    public void BindPerches(Vector2[] slots)
    {
        if (!ready || members == null || slots == null || slots.Length == 0)
            return;
        useTreePerch = true;
        for (int i = 0; i < members.Length; i++)
        {
            Vector2 slot = slots[i % slots.Length];
            members[i].perchX = slot.x;
            members[i].hasPerch = true;
            members[i].homeX = slot.x;
        }
    }

    void Update()
    {
        if (HeadlessTrial.Active && !allowHeadless) return;
        if (!ready || members == null) return;

        int stride = aiStride < 1 ? 1 : aiStride;
        frameCounter++;
        if ((frameCounter % stride) != 0)
            return;

        // Центроид только если leash живой и нет посадочных слотов —
        // иначе каждая особь уже знает свой homeX/perchX.
        float centroid = 0f;
        bool needCentroid = !useTreePerch && flockLeash > 0.01f;
        if (needCentroid)
        {
            int alive = 0;
            for (int i = 0; i < members.Length; i++)
            {
                Rigidbody2D body = members[i].body;
                if (body == null) continue;
                centroid += body.position.x;
                alive++;
            }

            if (alive > 0)
                centroid /= alive;
        }

        float now = Time.time;
        int heardCount = SoundBus.CollectRecent(now, 0.6f, heardBuf);
        int takeoffs = 0;
        int walks = 0;
        int pecks = 0;
        int lands = 0;
        int flips = 0;
        int perchLands = 0;
        int dirt = 0;
        int cries = 0;
        for (int i = 0; i < members.Length; i++)
        {
            // Без копии struct — иначе лишняя работа на N особях каждый кадр.
            BirdController control = members[i].control;
            BirdSensors sensors = members[i].sensors;
            BirdWander wander = members[i].wander;
            ChickenWander hen = members[i].chicken;
            if (control == null || sensors == null)
                continue;
            if (wander == null && hen == null)
                continue;

            Rigidbody2D body = members[i].body;
            Vector2 pos = body != null
                ? body.position
                : new Vector2(members[i].homeX, 0f);
            if (TickAlarm(i, control, members[i].voice, pos, now, heardCount))
            {
                if (members[i].voice != null)
                    cries += members[i].voice.CryCount;
                continue;
            }

            float home = members[i].hasPerch
                ? members[i].perchX
                : needCentroid ? centroid : members[i].homeX;
            BirdMode mode;
            float face;
            if (hen != null)
            {
                hen.Step(
                    pos.x, home, now,
                    sitMin, sitMax, walkMin, walkMax,
                    turnMin, turnMax, flockLeash,
                    out mode, out face);
                walks += hen.walkCount;
                pecks += hen.peckCount;
                lands += hen.sitCount;
                flips += hen.facingFlipCount;
            }
            else
            {
                bool onPerch = members[i].hasPerch && sensors.treeSupport;
                wander.Step(
                    sensors.bothGrounded, pos.x, home, now,
                    sitMin, sitMax, flyMin, flyMax, glideLead,
                    turnMin, turnMax, flockLeash,
                    members[i].hasPerch, onPerch, homeFaceDeadzone,
                    out mode, out face);
                takeoffs += wander.takeoffCount;
                lands += wander.landCount;
                flips += wander.facingFlipCount;
                perchLands += wander.perchLandCount;
                dirt += wander.dirtRejectCount;
            }
            control.mode = mode;
            if (control.FacingSign() != face)
                control.SetFacing(face);
            if (members[i].voice != null)
                cries += members[i].voice.CryCount;
        }

        TakeoffCount = takeoffs;
        WalkCount = walks;
        PeckCount = pecks;
        LandCount = lands;
        FacingFlipCount = flips;
        PerchLandCount = perchLands;
        DirtRejectCount = dirt;
        CryCount = cries;
    }

    private bool TickAlarm(
        int i,
        BirdController control,
        OrganismVoice voice,
        Vector2 pos,
        float now,
        int heardCount)
    {
        if (voice == null || control == null)
            return false;

        float dx = 0f;
        bool saw = threat != null && OrganismAlert.SeesAhead(
            pos.x, control.FacingSign(), threat.position.x, threatRange, 0.12f, out dx);
        bool heard = OrganismAlert.HearsForeignAlarm(
            voice.SourceId, pos, heardBuf, heardCount);

        if (saw)
        {
            voice.Cry(VoiceKind.Alarm);
            control.SetFacing(dx > 0f ? -1f : 1f);
            members[i].alertUntil = now + alertHold;
            control.mode = members[i].chicken != null ? BirdMode.Walk : BirdMode.Fly;
            return true;
        }

        if (heard || now < members[i].alertUntil)
        {
            if (heard)
                members[i].alertUntil = now + alertHold;
            control.mode = members[i].chicken != null ? BirdMode.Walk : BirdMode.Fly;
            return true;
        }

        return false;
    }
}
