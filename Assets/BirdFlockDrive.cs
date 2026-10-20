using UnityEngine;

// One brain for the flock: a cheap Update, not a ray per individual.
// Writes only mode and facing. Thrust still comes from BirdFlight.
[DefaultExecutionOrder(-50)]
public class BirdFlockDrive : MonoBehaviour
{
    [Tooltip("The stand turns this on explicitly.")]
    public bool allowHeadless;
    public int seed = 1;
    [Tooltip("Crow sit bout, s. With flyMin/Max this is about 70% of forage time.")]
    public float sitMin = 12f;
    public float sitMax = 22f;
    [Tooltip("Crow fly bout, s. About 30% of forage with sitMin/Max.")]
    public float flyMin = 2.5f;
    public float flyMax = 5.5f;
    [Tooltip("Enter Glide this many seconds before the fly phase ends.")]
    public float glideLead = 1.6f;
    public float turnMin = 1.2f;
    public float turnMax = 3.2f;
    [Tooltip("Facing flips while flying, s. Longer than the 0.4 s pitch-soft window.")]
    public float flyTurnMin = 1.2f;
    public float flyTurnMax = 2.4f;
    [Tooltip("Do not fly farther than this from the flock centroid, m.")]
    public float flockLeash = 3.6f;
    [Tooltip("Brain every N Update frames. 1 — every frame. Coarser behavior, cheaper CPU.")]
    public int aiStride = 1;
    [Tooltip("If set, leash and facing go to the tree perch, not the spawn line.")]
    public bool useTreePerch = false;
    [Tooltip("When a perch is bound, turn back at this distance from the slot, m. 0 — use flockLeash.")]
    public float orbitRadius = 4.6f;
    [Tooltip("Climb this far above the pad before counting orbit laps, m.")]
    public float cruiseClearance = 1.15f;
    [Tooltip("Short Glide pulses during Fly, s. 0 — no pulse (pack cruise until the land wave).")]
    public float weaveGlide = 0f;
    [Tooltip("Face the centroid/slot once farther than this along X, m. Inside — wander heading.")]
    public float cohesionRadius = 2.2f;
    [Tooltip("B-TURN-3 (-birdFlockSpace): separation push for airborne members, m/s^2.")]
    public float spaceSeparation = 6f;
    [Tooltip("B-TURN-3 (-birdFlockSpace): alignment pull toward the neighbours' velocity, m/s^2.")]
    public float spaceAlign = 2.5f;
    [Tooltip("B-TURN-3 (-birdFlockSpace): separation + alignment radius, m.")]
    public float spaceRadius = 1.2f;
    [Tooltip("Pack takeoff stagger per bird, s. 0 — lockstep.")]
    public float takeoffStagger = 0.08f;
    [Tooltip("Shared Glide after the startle fly, s. Pack lands together.")]
    public float packLandHold = 1.4f;
    [Tooltip("Dirt Sit after a pack land, s. Pad still uses sitHold.")]
    public float dirtSitHold = 0.85f;
    [Tooltip("If an Apple tree exists, reseat the flock on its pads (plant handshake).")]
    public bool preferAppleTree = true;
    [Tooltip("Hold Glide facing while |x − homeX| is inside this, m.")]
    public float homeFaceDeadzone = 0.15f;
    [Tooltip("Chicken walk bout, s.")]
    public float walkMin = 1.8f;
    public float walkMax = 3.8f;
    [Tooltip("Marker ahead along facing. Empty = no alarm.")]
    public Transform threat;
    [Tooltip("See the marker and cry, m.")]
    public float threatRange = 12f;
    [Tooltip("Live Insect on dirt. Empty = forage only. Not a world scan.")]
    public Transform prey;
    [Tooltip("Notice prey and Pursue, m along X.")]
    public float noticeRange = 40f;
    [Tooltip("After TickFlock Sit, keep Sit this long so wander does not write Fly, s.")]
    public float sitHold = 2.2f;
    [Tooltip("Keep Fly this long after see/hear, s.")]
    public float alertHold = 2.2f;

    [Header("Crow flock")]
    [Tooltip("One perched crow startles; the pack takes off together, then forage settles.")]
    public float startleMin = 16f;
    public float startleMax = 34f;
    [Tooltip("Shared fly after a startle, s.")]
    public float startleHold = 3.2f;
    [Tooltip("Fly up when the player human is closer than this along X, m. 0 — off.")]
    public float keepAwayRange = 4.2f;
    [Tooltip("Leave keep-away when farther than range times this.")]
    public float keepAwayRelease = 1.45f;
    [Tooltip("Chase pair interval, s. Dive/roll already live in BirdWander.")]
    public float playMin = 7f;
    public float playMax = 14f;
    [Tooltip("Chase bout, s.")]
    public float playHold = 1.6f;
    [Tooltip("Pack dives a threat when this many metres along X. 0 — off.")]
    public float mobNotice = Crow.MobNotice;
    [Tooltip("Shared dive after a crow notices the threat, s.")]
    public float mobHold = Crow.MobHold;
    [Tooltip("Attack stoop once closer than this along X, m.")]
    public float mobStoop = Crow.MobStoop;
    [Tooltip("Take off when a perched woodpecker drums inside this, m. 0 — off.")]
    public float drumStartleRange = Crow.DrumStartle;
    [Tooltip("Ignore further drums after a startle takeoff, s. 0 — keep ForceFly while Drumming.")]
    public float drumStartleCool = Crow.DrumStartleCool;
    [Tooltip("Walk toward the first hen if farther than this, m.")]
    public float henFollowSlack = Chicken.FollowSlack;
    [Tooltip("Peck if another hen pecks inside this, m.")]
    public float henPeckTogether = Chicken.PeckTogether;

    [Header("Life")]
    public float ownStrength = 1f;
    public float threatStrength = 2f;

    public int MemberCount { get; private set; }
    public int TakeoffCount { get; private set; }
    public int WalkCount { get; private set; }
    public int PeckCount { get; private set; }
    public int LandCount { get; private set; }
    public int FacingFlipCount { get; private set; }
    public int PerchLandCount { get; private set; }
    public int DirtRejectCount { get; private set; }
    public int CryCount { get; private set; }
    public int LifeSwitchCount { get; private set; }
    public int StartleCount { get; private set; }
    public int MobCount { get; private set; }
    public int DrumStartleCount { get; private set; }
    public int PreyEatenCount { get; private set; }
    // Cached stamps. Recorder latches Rel without GetComponent / Find.
    internal FactionMember HuntSelfFaction
    {
        get
        {
            if (members == null || members.Length == 0)
                return null;
            int i = lockedHunter >= 0 && lockedHunter < members.Length
                ? lockedHunter
                : 0;
            return members[i].faction;
        }
    }
    internal FactionMember HuntPreyFaction => preyFaction;
    // Locked seer's hunt write (lastHuntMode), not TickFlock Sit/Fly.
    // Pack[0] control.mode used to fill huntFlyFraction / onset while
    // the hunter Walk/Pecked. Park Clear only relives HuntBeak on Peck;
    // lastHuntMode is that Peck/Walk after ClampInsectHuntMode.
    internal BirdMode HuntBodyMode
    {
        get
        {
            if (members == null || members.Length == 0)
                return BirdMode.Sit;
            int i = lockedHunter >= 0 && lockedHunter < members.Length
                ? lockedHunter
                : 0;
            return members[i].lastHuntMode;
        }
    }
    // Body IsHuntSoil (preferDirtLand or dirt Peck). preferDirtLand alone
    // lied after HoldDirtAfterHunt cleared soil while mode was still Peck.
    internal bool HuntPreferDirt
    {
        get
        {
            if (members == null || members.Length == 0)
                return false;
            int i = lockedHunter >= 0 && lockedHunter < members.Length
                ? lockedHunter
                : 0;
            BirdController c = members[i].control;
            return c != null && c.IsHuntSoil();
        }
    }
    public int BeakHitCount
    {
        get
        {
            if (members == null)
                return 0;
            int hits = 0;
            for (int i = 0; i < members.Length; i++)
            {
                BeakStrike cached = members[i].beakHit;
                if (cached != null)
                    hits += cached.HitCount;
            }

            return hits;
        }
    }
    public LifeState Life { get; private set; } = LifeState.Forage;
    // B-NAT-0 ruler: the member's OWN life state. The per-member array is private to
    // this drive, and the recorder must not guess one pack life for five birds.
    // Read-only: no write path, no behaviour change.
    public LifeState LifeOf(int index)
    {
        if (members == null || index < 0 || index >= members.Length)
            return Life;
        return members[index].life;
    }
    // Bind starts Forage. Pack `Life` after the eat is Forage again.
    public bool SawLifeForage { get; private set; } = true;
    public bool SawLifePursue { get; private set; }

    private struct Member
    {
        public BirdController control;
        public BirdSensors sensors;
        public Rigidbody2D body;
        public BirdWander wander;
        public ChickenWander chicken;
        public WoodpeckerWander pecker;
        public Bird bird;
        public OrganismVoice voice;
        public FactionMember faction;
        public BeakStrike beakHit;
        public float homeX;
        public float perchX;
        public float perchY;
        public bool hasPerch;
        public float alertUntil;
        public LifeState life;
        public bool keepAway;
        public bool wasAirborne;
        public float restUntil;
        public bool wasDrumming;
        public float drumIgnoreUntil;
        public BirdMode lastHuntMode;
        public bool huntHeld;
        // Body ReleasePerch on preferDirtLand (bird). Brain still had
        // hasPerch + homeX on the twig — leash / BIRD-5 dirtReject
        // ForceFly yanked walkThrust off the Insect before Peck Bake.
        // Drop the slot for the eat; restore after the quarry is gone.
        public bool huntLeftSlot;
        // req-237 Part 2 (level only): the pack-flush latch, this member's seeded
        // flush jitter (0 until Bind fills it), the chosen resettle site and the
        // apple pad backup.
        public bool flushed;
        public float flushJit;
        public int siteKind;
        public float appleX;
        public float appleY;
        // r4: the hover probe's own pacing (one line per member per second).
        public float hoverLogUntil;
        // r5: when this member's pad approach first read as a stall (0 = not stalled).
        public float stallStart;
        // B-HERO-2 r2: until when this member holds the hero-out steer (mob lane).
        public float heroOutUntil;
    }

    private Member[] members;
    private readonly SoundEvent[] heardBuf = new SoundEvent[8];
    private bool ready;
    private bool didPreferApple;
    private int frameCounter;
    private System.Random rng;
    private int crowCount;
    private Transform player;
    private float playerX;
    private bool hasPlayer;
    private float nextStartle;
    private float startleBegin;
    private float startleUntil;
    private float packLandUntil;
    private float startleFace = 1f;
    private int startleSource = -1;
    private float nextPlay;
    private float playUntil;
    private Transform threatCached;
    private FactionMember threatFaction;
    private Transform preyCached;
    private FactionMember preyFaction;
    private Insect preyInsect;
    private Damageable preyLife;
    private bool hadLivePrey;
    private int huntWalkCount;
    private int huntPeckCount;
    private int playA = -1;
    private int playB = -1;
    private int henLeader = -1;
    private int henCount;
    private float mobUntil;
    private float mobX;
    private float mobBorn;
    private float mobRestUntil;
    private float calmUntil;
    private bool calmOn;
    private float postHuntSitUntil;
    private int lockedHunter = -1;
    // First Update stays pack Forage. ChooseWake / huntI overlay used
    // to write Pursue before the stand sampled. BindPrey already armed
    // Walk; Peck and Hurt still wait Pursue.
    private bool lifeWakeArmed;

    // req-237 Part 2 (-level1CrowLife, level-scoped, default off): the level's
    // flock reads the hero PELVIS body (the root never leaves the spawn), starts
    // seated and flushes as one pack off the crown, then resettles to seeded
    // sites. Off: every branch below is the old read, byte-identical.
    public bool levelCrowLife;
    public Rigidbody2D levelHeroBody;
    private float levelHomeX;
    private Vector2[] levelSites;
    private int[] levelSiteKinds;

    public void Bind(Bird[] birds, int bindSeed)
    {
        seed = bindSeed;
        rng = new System.Random(bindSeed);
        frameCounter = 0;
        didPreferApple = false;
        crowCount = 0;
        player = null;
        hasPlayer = false;
        startleBegin = 0f;
        startleUntil = 0f;
        packLandUntil = 0f;
        startleFace = 1f;
        startleSource = -1;
        playUntil = 0f;
        playA = -1;
        playB = -1;
        henLeader = -1;
        henCount = 0;
        mobUntil = 0f;
        mobX = 0f;
        StartleCount = 0;
        MobCount = 0;
        DrumStartleCount = 0;
        PreyEatenCount = 0;
        hadLivePrey = false;
        huntWalkCount = 0;
        huntPeckCount = 0;
        lockedHunter = -1;
        preyCached = null;
        preyFaction = null;
        preyInsect = null;
        preyLife = null;
        postHuntSitUntil = 0f;
        lifeWakeArmed = false;
        float bindNow = Time.time;
        nextStartle = bindNow + RandRange(4.5f, 8.5f);
        // req-237 Part 2: the level's seated start holds the pack's own alarm off
        // until the seeded 16-34 s window (Part 1: it was never reached - the
        // frozen-root mob kept re-arming). Off: the 4.5-8.5 s above stands.
        if (levelCrowLife)
            nextStartle = bindNow + RandRange(startleMin, startleMax);
        nextPlay = bindNow + RandRange(6f, 12f);
        if (birds == null || birds.Length == 0)
        {
            members = null;
            MemberCount = 0;
            ready = false;
            return;
        }

        members = new Member[birds.Length];
        MemberCount = birds.Length;
        LifeSwitchCount = 0;
        Life = LifeState.Forage;
        SawLifeForage = true;
        SawLifePursue = false;
        float now = Time.time;
        for (int i = 0; i < birds.Length; i++)
        {
            Bird bird = birds[i];
            if (bird == null) continue;
            BirdController control = bird.controller;
            // controller already caches Body — no Find on Bind.
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
            WoodpeckerWander drum = null;
            BirdForageStyle style = bird.ForageStyle;
            if (style == BirdForageStyle.ChickenGround)
            {
                hen = new ChickenWander();
                hen.Reset(seed + i * 17, now + i * 0.07f, 1.2f, 3.4f, face);
            }
            else if (style == BirdForageStyle.WoodpeckerDrum)
            {
                drum = new WoodpeckerWander();
                drum.Reset(seed + i * 17, now + i * 0.07f, 14f, 28f, face);
            }
            else
            {
                wander = new BirdWander();
                // Own RNG per bird. BindPerches must not Reset.
                wander.Reset(seed + i * 17, now + i * 0.07f, sitMin, sitMax, face);
                // Pack takeoff is ForceFly (startle / join). Solo dirt Fly is scatter.
                wander.allowSelfFly = false;
                crowCount++;
            }

            if (hen != null)
            {
                henCount++;
                if (henLeader < 0)
                    henLeader = i;
            }
            OrganismVoice voice = bird.GetComponent<OrganismVoice>();
            if (voice == null)
                voice = bird.gameObject.AddComponent<OrganismVoice>();
            FactionMember faction = bird.GetComponent<FactionMember>();
            if (faction == null
                || faction.faction != FactionId.Wildlife
                || faction.subfaction != SubfactionId.Bird)
                faction = FactionStamp.Bird(bird.gameObject);
            members[i] = new Member
            {
                bird = bird,
                control = control,
                sensors = bird.sensors,
                body = body,
                wander = wander,
                chicken = hen,
                pecker = drum,
                voice = voice,
                faction = faction,
                beakHit = bird.GetComponent<BeakStrike>(),
                homeX = body != null ? body.position.x : bird.transform.position.x,
                life = LifeState.Forage,
                wasAirborne = false,
                restUntil = 0f,
                huntHeld = false
            };
        }

        ready = true;
        // req-237 Part 2: seeded per-member flush jitter (the pack arms over a
        // few tenths of a second and the takeoff vector differs per flock seed).
        if (levelCrowLife)
        {
            for (int i = 0; i < members.Length; i++)
                members[i].flushJit = RandRange(0.85f, 0.95f);
        }
        if (prey != null)
            ArmBoundInsectHunt();
    }

    // Spawn / stand set the quarry after Bind. First Simulate must already
    // have preferDirtLand on the hunter or HuntBeak stays at ParkY.
    public void BindPrey(Transform quarry)
    {
        prey = quarry;
        if (quarry == null)
        {
            preyCached = null;
            preyFaction = null;
            preyInsect = null;
            preyLife = null;
            lockedHunter = -1;
            return;
        }

        ArmBoundInsectHunt();
    }

    // Forage → Pursue on the first Update (ChooseWake). BindPrey only
    // arms Walk and preferDirtLand (soil / walkThrust). HuntBeak is
    // Peck/Attack only — do not keep that query live for the 8 m close.
    // Peck waits Pursue. EnterLife Pursue here skipped every Forage
    // sample on the stand. Do not TickInsectPrey: that is the eat path.
    private void ArmBoundInsectHunt()
    {
        if (!ready || members == null)
            return;
        RefreshPreyFaction();
        DropDeadPrey();
        if (!PreyIsLive() || !PreyIsInsect() || prey == null)
            return;
        int huntI = ClosestInsectHunter();
        if (huntI < 0)
            return;
        ArmForageHuntWalk(huntI);
    }

    // Stride skip is cheap: one locked hunter, no pack centroid / overlay.
    // HoldSpawnForage already armed Walk; this is ChooseWake Pursue.
    private void TickInsectHunterOnStrideSkip()
    {
        if (!ready || members == null)
            return;
        RefreshPreyFaction();
        DropDeadPrey();
        if (!PreyIsLive() || !PreyIsInsect() || prey == null)
            return;
        int huntI = ClosestInsectHunter();
        if (huntI < 0)
            return;
        BirdController control = members[huntI].control;
        BirdSensors sensors = members[huntI].sensors;
        if (control == null || sensors == null)
            return;
        Rigidbody2D body = members[huntI].body;
        Vector2 pos = body != null
            ? body.position
            : new Vector2(members[huntI].homeX, 0f);
        float now = Time.time;
        LifeState next = ChooseMemberLife(
            huntI, control, members[huntI].voice, pos, now, 0, huntI);
        if (next == LifeState.Forage)
            SawLifeForage = true;
        if (next == LifeState.Pursue)
        {
            SawLifePursue = true;
            // Solo EnterLife arms soil on Pursue. ChooseWake Flee→Pursue
            // skips AbortHunterFlee (life already Pursue); TickFlee had
            // cleared preferDirtLand. Park Clear (include Nothing) only
            // Bake-relives on Peck — walkThrust must arm before TickInsectPrey.
            // Also drop hasPerch: body ReleasePerch alone left homeX on the
            // twig so leash / dirtReject ForceFly aborted the close.
            ArmDirtInsectHunt(huntI, control);
        }
        if (next != members[huntI].life)
        {
            members[huntI].life = next;
            LifeSwitchCount++;
        }

        if (AbortHunterFleeForInsect(huntI, control, pos))
            LifeSwitchCount++;

        if (members[huntI].life == LifeState.Forage)
        {
            float skipFace = control.FacingSign();
            bool saw = Insect.MayPursue(
                pos.x, skipFace, prey.position.x, noticeRange, false);
            if (MemberHuntsBoundInsect(huntI) && saw)
            {
                members[huntI].life = LifeState.Pursue;
                SawLifePursue = true;
                LifeSwitchCount++;
                // Park Clear leaves include Nothing until Peck Bake.
                // Soil before TickInsectPrey — same as EnterLife Pursue.
                ArmDirtInsectHunt(huntI, control);
            }
            else
            {
                ArmForageHuntWalk(huntI);
                ClearCrowPose(control);
                BeakStrike closed = members[huntI].beakHit;
                if (closed != null)
                    closed.ArmInsectEat(false);
                Life = LifeState.Forage;
                return;
            }
        }

        BeakStrike strike = members[huntI].beakHit;
        if (strike != null)
            strike.ArmInsectEat(members[huntI].life == LifeState.Pursue);
        if (members[huntI].life != LifeState.Pursue)
            return;
        members[huntI].huntHeld = true;
        HoldWanderOffHunt(huntI, now);
        TickInsectPrey(huntI, control, sensors, pos);
        ClearCrowPose(control);
        Life = LifeState.Pursue;
    }

    // Spawn pack Life is Forage. Arm Walk without ChooseWake so
    // preferDirtLand / walkThrust close on dirt. HuntBeak waits Peck.
    // The stand still samples Forage→Pursue. Peck waits Pursue.
    // Runs even when aiStride would skip this frame.
    private bool HoldSpawnForage()
    {
        if (lifeWakeArmed)
            return false;
        lifeWakeArmed = true;
        RefreshPreyFaction();
        DropDeadPrey();
        Life = LifeState.Forage;
        SawLifeForage = true;
        if (!PreyIsLive() || !PreyIsInsect() || prey == null)
            return true;
        int huntI = ClosestInsectHunter();
        if (huntI < 0)
            return true;
        members[huntI].life = LifeState.Forage;
        ArmForageHuntWalk(huntI);
        BirdController control = members[huntI].control;
        ClearCrowPose(control);
        BeakStrike strike = members[huntI].beakHit;
        if (strike != null)
            strike.ArmInsectEat(false);
        return true;
    }

    // Forage Walk toward the bound Insect. Not Peck: Hurt waits Pursue.
    // preferDirtLand is soil / walkThrust, not a live HuntBeak (Peck).
    private void ArmForageHuntWalk(int huntI)
    {
        if (members == null || huntI < 0 || huntI >= members.Length)
            return;
        BirdController control = members[huntI].control;
        if (control == null)
            return;
        members[huntI].huntHeld = true;
        HoldWanderOffHunt(huntI, Time.time);
        ArmDirtInsectHunt(huntI, control);
        WriteHuntMode(huntI, control, BirdMode.Walk);
    }

    // Soil + leave the twig slot. Body ReleasePerch needs preferDirtLand;
    // brain must drop hasPerch or homeX stays on the pad and leash /
    // dirtReject ForceFly aborts the close (Park Clear until Peck).
    private void ArmDirtInsectHunt(int huntI, BirdController control)
    {
        if (control == null)
            return;
        control.preferDirtLand = true;
        if (members == null || huntI < 0 || huntI >= members.Length)
            return;
        if (!members[huntI].hasPerch)
            return;
        members[huntI].huntLeftSlot = true;
        members[huntI].hasPerch = false;
        Rigidbody2D body = members[huntI].body;
        if (body != null)
            members[huntI].homeX = body.position.x;
    }

    // Plant handshake: slots from PlantTree.GetPerchSlots.
    // Spreads the bodies across the crown, then writes homeX / perchX / perchY.
    // Does not Reset wander (individual seed stays).
    public void BindPerches(Vector2[] slots, int paddedSlots = -1)
    {
        if (!ready || members == null || slots == null || slots.Length == 0)
            return;
        useTreePerch = true;
        Bird[] pack = new Bird[members.Length];
        for (int i = 0; i < members.Length; i++)
            pack[i] = members[i].bird;
        Bird.SpreadSeat(pack, slots);
        // req-237 r3 (level only): the crown's own pad list covers only part of
        // the slot band (rounds 1/2: 9 and 13 of 20 bound; an empty slot never
        // binds). Every seated member gets its own pad under LastSeatSlot.
        if (levelCrowLife)
        {
            int made = 0;
            for (int i = 0; i < members.Length; i++)
            {
                if (members[i].bird == null || !members[i].bird.WantsTreeSeat)
                    continue;
                Vector2 slot = members[i].bird.LastSeatSlot;
                GameObject pad = new GameObject("BirdPerch");
                pad.transform.SetParent(transform, true);
                pad.transform.position = new Vector3(slot.x, slot.y - 0.012f, 0f);
                pad.AddComponent<BirdPerch>();
                BoxCollider2D col = pad.AddComponent<BoxCollider2D>();
                col.size = new Vector2(0.12f, 0.024f);
                col.isTrigger = true;
                GroundLayers.Apply(pad);
                made++;
            }
            if (made > 0)
                Debug.Log("LEVEL1 seat pads=" + made);
        }
        // req-237 Part 2: the crown centre - the pack anchor the level's flush
        // and yard-return rules read. Off: unused, no behaviour.
        float lx = 0f;
        for (int i = 0; i < slots.Length; i++)
            lx += slots[i].x;
        levelHomeX = lx / slots.Length;
        for (int i = 0; i < members.Length; i++)
        {
            if (members[i].bird == null || !members[i].bird.WantsTreeSeat)
            {
                members[i].hasPerch = false;
                continue;
            }

            Vector2 slot = members[i].bird != null
                ? members[i].bird.LastSeatSlot
                : slots[i % slots.Length];
            members[i].perchX = slot.x;
            members[i].perchY = slot.y;
            members[i].hasPerch = true;
            members[i].homeX = slot.x;
            // req-237 Part 2: the apple pad backup for the yard-return rule.
            members[i].appleX = slot.x;
            members[i].appleY = slot.y;
        }
    }

    // req-237 Part 2 (level only): the resettle sites the flushed pack draws
    // from by seeded choice. kinds: 1 fence, 2 lamp, 3 dirt.
    public void SetLevelSites(Vector2[] sites, int[] kinds)
    {
        levelSites = sites;
        levelSiteKinds = kinds;
    }

    // Plant handshake: Apple is opt-in and may not be trees[0].
    // Reseat + BindPerches only. Does not Reset wander.
    private void PreferAppleTree()
    {
        if (!preferAppleTree || members == null)
            return;

        // GameProcess already caches the apple. Do not FindObjectsByType
        // (BIRD-13: no world scan). Null on Bird / Flock50 / the stand.
        PlantTree apple = LevelGrove.Apple;
        if (apple == null || apple.ResolvedKind != TreeKind.Apple)
            return;

        Bird[] pack = new Bird[members.Length];
        int n = 0;
        for (int i = 0; i < members.Length; i++)
        {
            if (members[i].bird == null || !members[i].bird.WantsTreeSeat)
                continue;
            pack[n++] = members[i].bird;
        }

        if (n == 0)
            return;

        if (n != pack.Length)
        {
            Bird[] crows = new Bird[n];
            for (int i = 0; i < n; i++)
                crows[i] = pack[i];
            pack = crows;
        }

        Bird.GhostTreeWood(pack, apple);
        // GameProcess may already SeatBirds in Start. A second SeatBirds here
        // is after the first physics step and teleports the pack.
        bool alreadySeated = false;
        for (int i = 0; i < members.Length; i++)
        {
            if (members[i].hasPerch)
            {
                alreadySeated = true;
                break;
            }
        }

        if (!alreadySeated)
            PlantTree.SeatBirds(pack, apple);
        BindPerches(apple.GetPerchSlots());
    }

    void Update()
    {
        if (HeadlessTrial.Active && !allowHeadless) return;
        if (!ready || members == null) return;

        if (!didPreferApple)
        {
            didPreferApple = true;
            PreferAppleTree();
        }

        int stride = aiStride < 1 ? 1 : aiStride;
        frameCounter++;
        if (HoldSpawnForage())
            return;
        // Pack scan stays on aiStride. The locked Insect hunter still
        // ChooseWake Forage→Pursue on skip frames — leftover TickFlock
        // Fly / ArmInsectEat(false) from HoldSpawnForage used to sit
        // until the next stride and skip Rel Hostile Peck.
        if ((frameCounter % stride) != 0)
        {
            TickInsectHunterOnStrideSkip();
            return;
        }

        // Centroid only if the leash is live and there are no perch slots —
        // otherwise each bird already knows its homeX/perchX.
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
        RefreshPreyFaction();
        DropDeadPrey();
        if (members != null)
        {
            for (int i = 0; i < members.Length; i++)
                EnsureMemberBird(i);
        }
        bool insectLive = PreyIsLive() && PreyIsInsect();
        if (crowCount > 0)
        {
            // Bound Insect is food. Keep-away Find is not a hunt scan;
            // skip it so a leftover player poll cannot run during eat.
            if (!insectLive)
            {
                RefreshPlayer(now);
                TickFlockEvents(now);
            }
        }
        int huntI = ClosestInsectHunter();
        int heardCount = SoundBus.CollectRecent(now, 0.6f, heardBuf);
        int takeoffs = 0;
        int walks = 0;
        int pecks = 0;
        int lands = 0;
        int flips = 0;
        int perchLands = 0;
        int dirt = 0;
        int cries = 0;
        int switches = LifeSwitchCount;
        LifeState packLife = LifeState.Forage;
        for (int i = 0; i < members.Length; i++)
        {
            // No struct copy — otherwise extra work on N birds every frame.
            BirdController control = members[i].control;
            BirdSensors sensors = members[i].sensors;
            BirdWander wander = members[i].wander;
            ChickenWander hen = members[i].chicken;
            WoodpeckerWander drum = members[i].pecker;
            if (control == null || sensors == null)
                continue;
            // Hunt the bound Insect even if this member has no wander clock
            // (AddComponent Start missed Kind.Wander). Forage Step still
            // needs a clock — skip that write below, not ChooseMemberLife.
            bool forageBrain = wander != null || hen != null || drum != null;
            if (!forageBrain && !(insectLive && i == huntI))
                continue;

            Rigidbody2D body = members[i].body;
            Vector2 pos = body != null
                ? body.position
                : new Vector2(members[i].homeX, 0f);
            LifeState next = ChooseMemberLife(i, control, members[i].voice, pos, now, heardCount, huntI);
            if (next == LifeState.Forage)
                SawLifeForage = true;
            if (next == LifeState.Pursue)
            {
                SawLifePursue = true;
                // Solo EnterLife arms soil on Pursue. ChooseWake can jump
                // Flee→Pursue without AbortHunterFlee; TickFlee cleared
                // preferDirtLand. Park Clear only Bake-relives HuntBeak on
                // Peck — dirt Walk must arm before TickInsectPrey.
                // Drop hasPerch so leash / dirtReject do not abort the close
                // after body ReleasePerch (bird).
                if (insectLive && i == huntI)
                    ArmDirtInsectHunt(i, control);
            }
            if (next != members[i].life)
            {
                members[i].life = next;
                switches++;
            }

            if (insectLive && i == huntI && AbortHunterFleeForInsect(i, control, pos))
                switches++;

            // BeakStrike is order 20. HuntBeak is Peck only. Hurt Insects
            // only while this member Pursues.
            BeakStrike strike = members[i].beakHit;
            // huntI in noticeRange is food, but Pursue is Rel Hostile.
            // Overlay used to stamp Pursue on a Neutral leftover, then
            // TickInsectPrey sat in PeckReach while Hurt stayed gated.
            // Rel miss: Walk, stay Forage. wander.Step Fly is leftover
            // cruise (huntFlyFraction). Rel hit: ChooseWake already
            // Pursued; this is only the Rel-true backup.
            if (insectLive && i == huntI && members[i].life == LifeState.Forage)
            {
                float huntFace = control.FacingSign();
                bool saw = prey != null && Insect.MayPursue(
                    pos.x, huntFace, prey.position.x, noticeRange, false);
                if (MemberHuntsBoundInsect(i) && saw)
                {
                    members[i].life = LifeState.Pursue;
                    SawLifePursue = true;
                    switches++;
                    // Park Clear include Nothing until Peck Bake. Soil
                    // before TickInsectPrey — same as EnterLife Pursue.
                    ArmDirtInsectHunt(i, control);
                }
                else
                {
                    ArmForageHuntWalk(i);
                    ClearCrowPose(control);
                    if (strike != null)
                        strike.ArmInsectEat(false);
                    if (members[i].voice != null)
                        cries += members[i].voice.CryCount;
                    continue;
                }
            }

            if (strike != null)
                strike.ArmInsectEat(members[i].life == LifeState.Pursue);

            // Bound Insect is food. Pack Alarm Flee on a bystander used
            // to win pack Life and hide the hunter's Pursue.
            if (insectLive)
            {
                if (members[i].life == LifeState.Pursue)
                    packLife = LifeState.Pursue;
                else if (members[i].life == LifeState.Flee && packLife != LifeState.Pursue)
                    packLife = LifeState.Flee;
            }
            else if (members[i].life == LifeState.Flee)
                packLife = LifeState.Flee;
            else if (members[i].life == LifeState.Pursue && packLife != LifeState.Flee)
                packLife = LifeState.Pursue;

            if (members[i].life == LifeState.Flee)
            {
                TickFlee(i, control, members[i].voice, pos, now, heardCount);
                ClearCrowPose(control);
                if (members[i].voice != null)
                    cries += members[i].voice.CryCount;
                continue;
            }

            if (members[i].life == LifeState.Pursue)
            {
                members[i].huntHeld = true;
                HoldWanderOffHunt(i, now);
                TickInsectPrey(i, control, sensors, pos);
                ClearCrowPose(control);
                if (members[i].voice != null)
                    cries += members[i].voice.CryCount;
                continue;
            }

            if (!forageBrain)
            {
                if (members[i].voice != null)
                    cries += members[i].voice.CryCount;
                continue;
            }

            if (members[i].huntHeld && now < postHuntSitUntil)
            {
                if (!HoldDirtAfterHunt(i, control, sensors))
                {
                    bool onDirt = sensors != null && sensors.bothGrounded;
                    if (onDirt)
                        WriteHuntMode(i, control, BirdMode.Sit);
                    else
                    {
                        control.preferDirtLand = true;
                        WriteHuntMode(i, control, BirdMode.Walk);
                    }
                }
                ClearCrowPose(control);
                if (members[i].voice != null)
                    cries += members[i].voice.CryCount;
                continue;
            }

            if (now >= postHuntSitUntil)
                members[i].huntHeld = false;

            if (HoldDirtAfterHunt(i, control, sensors))
            {
                ClearCrowPose(control);
                if (members[i].voice != null)
                    cries += members[i].voice.CryCount;
                continue;
            }

            float home = members[i].hasPerch
                ? members[i].perchX
                : needCentroid ? centroid : members[i].homeX;
            float leash = members[i].hasPerch && orbitRadius > 0.01f
                ? orbitRadius
                : flockLeash;
            BirdMode mode;
            float face;
            if (hen != null)
            {
                control.preferDirtLand = false;
                float henHome = home;
                float henLeash = flockLeash;
                if (henCount >= 2 && henLeader >= 0 && henLeader != i
                    && henFollowSlack > 0.05f
                    && henLeader < members.Length
                    && members[henLeader].body != null)
                {
                    henHome = members[henLeader].body.position.x;
                    henLeash = henFollowSlack;
                }

                hen.Step(
                    pos.x, henHome, now,
                    1.2f, 3.4f, walkMin, walkMax,
                    turnMin, turnMax, henLeash,
                    out mode, out face);
                ApplyHenOverlay(i, pos, ref mode, ref face);
                walks += hen.walkCount;
                pecks += hen.peckCount;
                lands += hen.sitCount;
                flips += hen.facingFlipCount;
                ClearCrowPose(control);
            }
            else if (drum != null)
            {
                control.preferDirtLand = false;
                bool onPad = members[i].hasPerch && sensors.treeSupport;
                drum.Step(
                    sensors.bothGrounded, pos.x, home, now,
                    14f, 28f, 0.7f, 1.6f, 0.35f,
                    turnMin, turnMax, leash,
                    members[i].hasPerch, onPad,
                    out mode, out face);
                bool drumNow = drum.Drumming && sensors.treeSupport;
                if (drumNow && !members[i].wasDrumming)
                    BirdSocial.PulseDrum(pos.x, now, Woodpecker.DrumPulseHold);
                members[i].wasDrumming = drumNow;
                takeoffs += drum.takeoffCount;
                pecks += drum.peckCount;
                lands += drum.sitCount;
                flips += drum.facingFlipCount;
                perchLands += drum.perchLandCount;
                dirt += drum.dirtRejectCount;
                ClearCrowPose(control);
            }
            else
            {
                bool onPerch = members[i].hasPerch && sensors.treeSupport;
                float padY = members[i].hasPerch ? members[i].perchY : pos.y;
                wander.Step(
                    sensors.bothGrounded, pos.x, home, now,
                    sitMin, sitMax, walkMin, walkMax, flyMin, flyMax, glideLead,
                    flyTurnMin, flyTurnMax, leash,
                    members[i].hasPerch, onPerch, homeFaceDeadzone,
                    pos.y, padY, cruiseClearance, weaveGlide,
                    turnMin, turnMax,
                    out mode, out face);
                takeoffs += wander.takeoffCount;
                walks += wander.walkCount;
                pecks += wander.peckCount;
                lands += wander.landCount;
                flips += wander.facingFlipCount;
                perchLands += wander.perchLandCount;
                dirt += wander.dirtRejectCount;
                // req-237 r4: the site landing. The approach glide slope-couples to
                // the forward speed and freezes ~0.7 m over the pad (r3), so the last
                // 0.85 m of a released member's approach is a drop: no-lift mode, the
                // aero and the ground end it, HoldLandSit catches the touch.
                if (levelCrowLife && members[i].flushed && !members[i].keepAway
                    && !sensors.bothGrounded && !sensors.treeSupport
                    && members[i].body != null
                    && Mathf.Abs(members[i].body.position.x - members[i].homeX) < 1.6f)
                {
                    Vector2 bp = members[i].body.position;
                    bool overPad = members[i].hasPerch && bp.y > members[i].perchY + 0.30f;
                    bool stalled = members[i].body.linearVelocity.magnitude < 0.7f;
                    if ((overPad || stalled) && now >= members[i].hoverLogUntil)
                    {
                        members[i].hoverLogUntil = now + 1f;
                        // req-237 r6 (probe only, no behaviour): name the hover. bt is the
                        // weld test - a Kinematic body ignores lift; a Dynamic one is a force
                        // balance, then hm/gl/lhd say which lane holds it.
                        System.Globalization.CultureInfo inv = System.Globalization.CultureInfo.InvariantCulture;
                        BirdFlight fl = members[i].bird != null ? members[i].bird.flight : null;
                        Vector2 hv = members[i].body.linearVelocity;
                        Debug.Log("LEVEL1 HOVER t=" + now.ToString("F2", inv)
                            + " id=" + (i + 1) + " x=" + bp.x.ToString("F2", inv) + " ph=" + wander.PhaseWord + " md=" + (int)mode
                            + " dy=" + (bp.y - members[i].perchY).ToString("F2", inv)
                            + " v=" + hv.magnitude.ToString("F2", inv)
                            + " vy=" + hv.y.ToString("F2", inv)
                            + " hp=" + (members[i].hasPerch ? 1 : 0)
                            + " bt=" + (int)members[i].body.bodyType
                            + " gs=" + members[i].body.gravityScale.ToString("F2", inv)
                            + " sim=" + (members[i].body.simulated ? 1 : 0)
                            + " cons=" + (int)members[i].body.constraints
                            + " cm=" + (int)control.mode
                            + " lf=" + (control.WantsDirtLandingFlap() ? 1 : 0)
                            + " land=" + (control.FlyingLanding() ? 1 : 0)
                            + " brake=" + (control.BrakingStrokesActive() ? 1 : 0)
                            + " pd=" + (control.preferDirtLand ? 1 : 0)
                            + " soil=" + (control.IsHuntSoil() ? 1 : 0)
                            + " perch=" + (control.IsPerched ? 1 : 0)
                            + " dirt=" + (wander.DirtBout ? 1 : 0)
                            + " hm=" + (fl != null ? fl.hoverMean.ToString("F2", inv) : "-")
                            + " gl=" + (fl != null ? fl.glideLift.ToString("F2", inv) : "-")
                            + " gss=" + (fl != null ? fl.glideSinkSpeed.ToString("F2", inv) : "-")
                            + " lhd=" + (fl != null ? fl.lastHoverDemand.ToString("F2", inv) : "-"));
                    }
                    if (overPad || stalled || bp.y - members[i].perchY < 0.30f)
                    {
                        // req-237 r7: the held dirt descend. The r6 probe named the
                        // hover: with mode Glide and no dirt ask the landing flap
                        // (WantsDirtLandingFlap) runs and its mean lift is 1.01-1.045
                        // of weight - a slow-climb hover ~1 m over the pad; the r5
                        // one-shot flip was cleared by the wander within a second and
                        // the flap came back. Hold the dirt ask every frame while the
                        // member is airborne in its band: IsHuntSoil kills the flap,
                        // the glide (glideLift 0.86 of weight) sinks it to support.
                        if (members[i].stallStart <= 0f)
                        {
                            members[i].stallStart = now;
                            Level1CrowLog(i, "ground", "stallDirt",
                                Level1SiteName(members[i].siteKind), bp.x);
                        }
                        wander.ForceDescentNow(true);
                        mode = BirdMode.Glide;
                    }
                }
                else
                    members[i].stallStart = 0f;
                if (HoldLandSit(i, wander, sensors, now))
                    mode = BirdMode.Sit;
                ApplyCrowOverlay(i, wander, pos, home, now, ref mode, ref face);
                control.preferDirtLand = wander.DirtBout;
                WriteCrowPose(control, wander, now, i);
            }
            // Hunter must not leftover-Fly / Forage-Peck if Step ran.
            // Bystander dirt forage Peck stays (BIRD-11).
            mode = BirdDrive.ClampInsectHuntMode(
                mode,
                members[i].life == LifeState.Pursue,
                insectLive && i == huntI);
            if (insectLive && i == huntI)
            {
                ArmDirtInsectHunt(i, control);
                members[i].lastHuntMode = mode;
            }
            control.mode = mode;
            if (control.FacingSign() != face)
                control.SetFacing(face);
            if (members[i].voice != null)
                cries += members[i].voice.CryCount;
        }

        // B-TURN-3: separation + alignment for the airborne members (O(N^2), N <= 50).
        TickFlockSpacing();

        TakeoffCount = takeoffs;
        WalkCount = walks + huntWalkCount;
        PeckCount = pecks + huntPeckCount;
        LandCount = lands;
        FacingFlipCount = flips;
        PerchLandCount = perchLands;
        DirtRejectCount = dirt;
        CryCount = cries;
        LifeSwitchCount = switches;
        Life = packLife;
    }

    private LifeState ChooseMemberLife(
        int i,
        BirdController control,
        OrganismVoice voice,
        Vector2 pos,
        float now,
        int heardCount,
        int huntI)
    {
        float dx;
        RefreshThreatFaction();
        bool threatHostile = MarkerIsHostile(members[i].faction, threat, threatFaction);
        // B-FLOCK-2: the calm window closes here (one [B-CALM] end line).
        if (calmOn && now >= calmUntil)
        {
            calmOn = false;
            Debug.Log("[B-CALM] end t=" + now.ToString("F1"));
        }
        // Inside the calm window the mark is not read: the alert/flee state toward
        // it is cleared and held off, and no alarm re-arms from that static mark.
        bool calm = Bird.MobCalmKnob > 0f && now < calmUntil;
        bool saw = !calm && threatHostile && control != null && OrganismAlert.SeesAhead(
            pos.x, control.FacingSign(), threat.position.x, threatRange, 0.12f, out dx);
        bool heard = !calm && voice != null && OrganismAlert.HearsForeignAlarm(
            voice.SourceId, pos, heardBuf, heardCount);
        bool stronger = saw && OrganismLife.HostileIsStronger(ownStrength, threatStrength);
        bool insectNear = PreyIsLive() && PreyIsInsect() && prey != null
            && Mathf.Abs(prey.position.x - pos.x) < noticeRange;
        // Card: Forage + sees Insects (weaker Hostile) → Pursue.
        // Rel is the hunt gate. Distance-only Pursue hunted a Neutral
        // leftover and "saw" far behind the back. ClosestInsectHunter
        // already glanced; MayPursue is the see. Alarm stays muted on
        // insectNear so TickFlee Fly cannot abort the turn.
        float face = control != null ? control.FacingSign() : 1f;
        bool sawInsectAhead = prey != null && Insect.MayPursue(
            pos.x, face, prey.position.x, noticeRange,
            members[i].life == LifeState.Pursue);
        bool insectHunt = i == huntI && MemberHuntsBoundInsect(i)
            && (sawInsectAhead || members[i].life == LifeState.Pursue);
        bool sawInsect = insectHunt;
        bool preyHostile = sawInsect
            || (PreyIsLive() && !PreyIsInsect()
                && MarkerIsHostile(members[i].faction, prey, preyFaction));
        bool sawPrey = sawInsect
            || (preyHostile && !insectHunt && prey != null
                && Mathf.Abs(prey.position.x - pos.x) < noticeRange);
        bool weaker = insectHunt
            || (sawPrey
                && !OrganismLife.HostileIsStronger(ownStrength, PreyStrength()));
        // Hunter next to the Insect mutes leftover pack Alarm so
        // ChooseWake does not Flee → TickFlee Fly before the hunter Pursues.
        // Mute the same Alarm while Rel is still Neutral (one stamp
        // frame): TickFlee Fly skipped Forage→Pursue. A seen stronger
        // wolf still Flee.
        if (insectHunt || (i == huntI && insectNear))
            heard = false;
        return OrganismLife.ChooseWake(
            stronger, weaker, heard,
            calm || insectHunt || (i == huntI && insectNear)
                ? false
                : now < members[i].alertUntil);
    }

    // Solo BirdDrive already aborts leftover Alarm Flee. Pack ChooseWake
    // can still Flee (null-faction threat counts Hostile) then TickFlee
    // Fly before Rel Hostile Pursue. Bound Insect in notice is food.
    // Caller counts the life switch (pack loop uses a local tally).
    private bool AbortHunterFleeForInsect(int i, BirdController control, Vector2 pos)
    {
        if (members == null || i < 0 || i >= members.Length)
            return false;
        if (members[i].life != LifeState.Flee)
            return false;
        if (!PreyIsLive() || !PreyIsInsect() || prey == null)
            return false;
        if (Mathf.Abs(prey.position.x - pos.x) >= noticeRange)
            return false;
        if (SeesStrongerThreat(i, control, pos))
            return false;
        LifeState next = MemberHuntsBoundInsect(i)
            ? LifeState.Pursue
            : LifeState.Forage;
        if (next == members[i].life)
            return false;
        members[i].life = next;
        if (next == LifeState.Pursue)
        {
            SawLifePursue = true;
            // TickFlee cleared preferDirtLand. Park Clear (include
            // Nothing) only Bake-relives on Peck; soil / walkThrust
            // must arm before TickInsectPrey this same Update.
            // Drop hasPerch: body ReleasePerch alone left homeX on the
            // twig (leash / dirtReject ForceFly off the Insect).
            ArmDirtInsectHunt(i, control);
        }
        else
            SawLifeForage = true;
        return true;
    }

    private bool SeesStrongerThreat(int i, BirdController control, Vector2 pos)
    {
        if (threat == null || control == null)
            return false;
        RefreshThreatFaction();
        if (!MarkerIsHostile(members[i].faction, threat, threatFaction))
            return false;
        if (!OrganismLife.HostileIsStronger(ownStrength, threatStrength))
            return false;
        float dx;
        return OrganismAlert.SeesAhead(
            pos.x, control.FacingSign(), threat.position.x, threatRange, 0.12f, out dx);
    }

    // One Insect, one hunter. A 50-pack Walk pile is not forage.
    // Stick to the locked seer so a closer crow behind the quarry
    // does not steal the eat. Acquire in noticeRange; hold until the
    // insect is gone. Unlocking at the leash dropped pack Forage Fly.
    private int ClosestInsectHunter()
    {
        if (!PreyIsLive() || prey == null || members == null || !PreyIsInsect())
        {
            lockedHunter = -1;
            return -1;
        }

        float preyX = prey.position.x;
        if (lockedHunter >= 0 && lockedHunter < members.Length)
        {
            if (MemberCanHuntInsect(lockedHunter))
            {
                Rigidbody2D lockedBody = members[lockedHunter].body;
                float lockedX = lockedBody != null
                    ? lockedBody.position.x
                    : members[lockedHunter].homeX;
                BirdController locked = members[lockedHunter].control;
                // Walk still glances. Peck must not: leftover
                // WantHuntFace inside PeckHold reversed walkThrust
                // and walked the body-X slab off Rel Hostile.
                if (locked != null
                    && members[lockedHunter].lastHuntMode != BirdMode.Peck)
                {
                    float face;
                    if (Insect.WantHuntFace(
                        lockedX, preyX, noticeRange, locked.FacingSign(),
                        true, out face))
                        locked.SetFacing(face);
                }

                return lockedHunter;
            }

            lockedHunter = -1;
        }

        // One hunter. Face the bound Insect this frame. Other wanderers
        // keep BIRD-3 — they do not turn. Not a scene scan.
        GlanceClosestForageInsect();

        int huntI = -1;
        float best = noticeRange;
        for (int i = 0; i < members.Length; i++)
        {
            if (!MemberCanHuntInsect(i))
                continue;
            Rigidbody2D body = members[i].body;
            float x = body != null ? body.position.x : members[i].homeX;
            float d = Mathf.Abs(preyX - x);
            if (d >= best)
                continue;
            best = d;
            huntI = i;
        }

        lockedHunter = huntI;
        return huntI;
    }

    // Stamped Wildlife.Bird next to a live Insect. Rel Hostile is the
    // mouth; a Rel miss used to leave huntI at −1 and pack Forage Fly.
    private bool MemberCanHuntInsect(int i)
    {
        if (members == null || i < 0 || i >= members.Length)
            return false;
        if (members[i].control == null)
            return false;
        EnsureMemberBird(i);
        if (!PreyIsLive() || !PreyIsInsect())
            return false;
        FactionMember hunter = members[i].faction;
        if (hunter == null)
            return false;
        return hunter.faction == FactionId.Wildlife
            && hunter.subfaction == SubfactionId.Bird;
    }

    // Card Rel gate. A missing stamp is not food — restamp, then
    // BirdHuntsInsect. Null→true used to Pursue while Rel latched Neutral.
    private bool MemberHuntsBoundInsect(int i)
    {
        if (!MemberCanHuntInsect(i))
            return false;
        if (preyFaction == null || preyFaction.faction != FactionId.Insects)
            RefreshPreyFaction();
        return FactionTable.BirdHuntsInsect(members[i].faction, preyFaction);
    }

    // Closest Forage member in noticeRange, any facing. Bound quarry.
    private void GlanceClosestForageInsect()
    {
        if (!PreyIsLive() || prey == null || members == null || !PreyIsInsect())
            return;

        float preyX = prey.position.x;
        int faceI = -1;
        float best = noticeRange;
        for (int i = 0; i < members.Length; i++)
        {
            if (members[i].life != LifeState.Forage)
                continue;
            if (!MemberCanHuntInsect(i))
                continue;
            BirdController control = members[i].control;
            if (control == null)
                continue;
            Rigidbody2D body = members[i].body;
            float x = body != null ? body.position.x : members[i].homeX;
            float d = Mathf.Abs(preyX - x);
            if (d >= best)
                continue;
            best = d;
            faceI = i;
        }

        if (faceI < 0)
            return;
        BirdController hunter = members[faceI].control;
        if (hunter == null)
            return;
        Rigidbody2D hunterBody = members[faceI].body;
        float selfX = hunterBody != null
            ? hunterBody.position.x
            : members[faceI].homeX;
        float face;
        if (!Insect.WantHuntFace(
            selfX, preyX, noticeRange, hunter.FacingSign(), out face))
            return;
        hunter.SetFacing(face);
    }

    private void EnsureMemberBird(int i)
    {
        if (members == null || i < 0 || i >= members.Length || members[i].bird == null)
            return;
        FactionMember f = members[i].faction;
        if (f == null
            || f.faction != FactionId.Wildlife
            || f.subfaction != SubfactionId.Bird)
            members[i].faction = FactionStamp.Bird(members[i].bird.gameObject);
    }

    private void RefreshThreatFaction()
    {
        if (threat == threatCached)
            return;
        threatCached = threat;
        threatFaction = threat != null ? threat.GetComponent<FactionMember>() : null;
    }

    private void RefreshPreyFaction()
    {
        if (prey != preyCached)
        {
            preyCached = prey;
            Insect.BindHuntRefs(prey, out preyInsect, out preyFaction, out preyLife);
            return;
        }

        if (prey != null
            && (preyInsect == null
                || preyFaction == null
                || preyFaction.faction != FactionId.Insects))
            Insect.BindHuntRefs(prey, out preyInsect, out preyFaction, out preyLife);
    }

    private void DropDeadPrey()
    {
        bool live = PreyIsLive();
        if (hadLivePrey && !live)
        {
            PreyEatenCount++;
            postHuntSitUntil = Time.time + Mathf.Max(0.85f, sitHold);
            if (members != null)
            {
                for (int i = 0; i < members.Length; i++)
                {
                    if (members[i].huntHeld)
                        HoldWanderOffHunt(i, Time.time);
                    // BIRD-5: dirt is not home — restore the twig slot
                    // after the eat so forage can Fly back.
                    if (members[i].huntLeftSlot)
                    {
                        members[i].hasPerch = true;
                        members[i].homeX = members[i].perchX;
                        members[i].huntLeftSlot = false;
                    }
                }
            }
        }
        hadLivePrey = live;
        if (live)
            return;
        prey = null;
        preyCached = null;
        preyFaction = null;
        preyInsect = null;
        preyLife = null;
        lockedHunter = -1;
    }

    private bool PreyIsLive()
    {
        if (prey == null)
            return false;
        if (preyLife != null && preyLife.IsDead)
            return false;
        return true;
    }

    private float PreyStrength()
    {
        return preyInsect != null ? preyInsect.ownStrength : 0.5f;
    }

    private bool PreyIsInsect()
    {
        if (preyInsect != null)
            return true;
        return preyFaction != null && preyFaction.faction == FactionId.Insects;
    }

    // Insect on dirt: Walk then Peck. Not a stoop Attack. Eat is BeakStrike.
    private void TickInsectPrey(int i, BirdController control, BirdSensors sensors, Vector2 pos)
    {
        if (control == null)
            return;

        if (prey != null && !PreyIsInsect())
            Insect.BindHuntRefs(prey, out preyInsect, out preyFaction, out preyLife);

        // Rel not Hostile: still Walk/Peck. A stamp miss is not leftover Fly.
        if (!PreyIsLive())
        {
            if (InsectHuntOnDirt(i, sensors, pos))
            {
                WriteHuntMode(i, control, BirdMode.Sit);
                control.preferDirtLand = false;
            }
            else
            {
                control.preferDirtLand = true;
                WriteHuntMode(i, control, BirdMode.Walk);
            }
            return;
        }

        // Soil first: InsectHuntOnDirt reads preferDirtLand. HuntBeak
        // is Dynamic only while Peck — leftover hover / NearPerch kept
        // OnDirtPlate false and Rel Hostile never Hurt.
        // Drop hasPerch: body ReleasePerch needs preferDirtLand; brain
        // homeX on the twig still leash / dirtReject ForceFly.
        // OnDirtPlate now treats dirt-height as soil even if a leftover
        // pad AABB still reports perched (preferDirtLand flicker).
        ArmDirtInsectHunt(i, control);
        HoldWanderOffHunt(i, Time.time);

        float dx = prey.position.x - pos.x;
        float dist = Mathf.Abs(dx);
        bool pecking = Insect.InPeckWindow(dist, members[i].lastHuntMode == BirdMode.Peck);
        // TickInsectPrey is Pursue only. Rel Hostile is the eat gate —
        // Peck without BirdHuntsInsect armed HuntBeak while Hurt stayed
        // Neutral. Dirt is preferDirtLand; close is InPeckWindow.
        bool eat = pecking && MemberHuntsBoundInsect(i);
        // First Peck still had lastHuntMode Walk, so WantCloseFace
        // flipped walkThrust; HuntBeak is body-X now, but vx along the
        // new face still walks the slab off Rel Hostile. Hold the nose
        // in PeckHold and on this eat frame.
        if (Insect.WantCloseFace(
                dx,
                members[i].lastHuntMode == BirdMode.Peck || eat,
                control.FacingSign()))
            control.SetFacing(dx > 0f ? 1f : -1f);
        if (eat)
            WriteHuntMode(i, control, BirdMode.Peck);
        else
            WriteHuntMode(i, control, BirdMode.Walk);
    }

    // Hunt does not Step wander. Hold leftover Fly/Glide/Forage and an
    // expired Sit bout so the first Forage Step is not leftover cruise Fly.
    private void HoldWanderOffHunt(int i, float now)
    {
        BirdWander wander = members[i].wander;
        if (wander != null)
            wander.HoldHuntSit(now, sitMin, sitMax, turnMin, turnMax);
        if (members[i].chicken != null)
            members[i].chicken.HoldHuntSit(now, 1.2f, 3.4f);
        if (members[i].pecker != null)
            members[i].pecker.HoldHuntSit(now, 14f, 28f);
        if (members[i].control != null)
            members[i].control.takeoffDelay = 0f;
    }

    private bool HuntMustLeavePerch(int i, BirdSensors sensors, Vector2 pos)
    {
        BirdController control = members[i].control;
        // Body IsHuntSoil — not preferDirtLand alone. HoldDirtAfterHunt
        // Forage flicker cleared the flag while dirt Peck still needed
        // OnDirtPlate true for HuntBeak Rel Hostile.
        if (control != null && control.IsHuntSoil())
            return false;
        // Dirt-height close is soil even if hasPerch / treeSupport still
        // lag after ArmDirtInsectHunt (body ReleasePerch one step late).
        float height = pos.y - Bird.StanceRootY(members[i].bird);
        if (height < Insect.DirtHuntHeight)
            return false;
        // Bound Hostile Insect is dirt food. Leftover NearPerch AABB
        // is soil plus twigs — not a twig sit. Treating it as perch
        // skipped the first Pursue Peck (HuntBeak stayed Walk-parked).
        if (members[i].life == LifeState.Pursue && PreyIsLive() && PreyIsInsect())
            return false;
        return members[i].hasPerch && sensors != null && sensors.treeSupport;
    }

    private bool InsectHuntOnDirt(int i, BirdSensors sensors, Vector2 pos)
    {
        bool onPerch = HuntMustLeavePerch(i, sensors, pos);
        bool grounded = sensors != null && sensors.bothGrounded;
        float height = pos.y - Bird.StanceRootY(members[i].bird);
        return Insect.OnDirtPlate(grounded, onPerch, height);
    }

    private void WriteHuntMode(int i, BirdController control, BirdMode next)
    {
        next = BirdDrive.ClampInsectHuntMode(
            next,
            members[i].life == LifeState.Pursue,
            PreyIsLive() && PreyIsInsect());
        BirdMode last = members[i].lastHuntMode;
        if (next == BirdMode.Walk && last != BirdMode.Walk)
            huntWalkCount++;
        if (next == BirdMode.Peck && last != BirdMode.Peck)
            huntPeckCount++;
        control.mode = next;
        members[i].lastHuntMode = next;
    }

    // Same-frame eat: Forage then wander.Step used to write leftover Fly.
    private bool HoldDirtAfterHunt(int i, BirdController control, BirdSensors sensors)
    {
        if (control == null)
            return false;
        // Live Hostile Insect mid-hunt still needs soil. A Forage flicker
        // that cleared preferDirtLand here while mode was Peck parked
        // HuntBeak one Update before BeakStrike Hurt (Rel Hostile missed).
        // Body IsHuntSoil covers the lag; brain must keep preferDirtLand
        // and not fall through to wander Fly. Far Forage (Sit leftover)
        // still wanders — ClosestInsectHunter already armed Walk closer.
        if (PreyIsLive() && PreyIsInsect())
        {
            BirdMode cur = control.mode;
            BirdMode lastMode = members[i].lastHuntMode;
            bool midHunt = lastMode == BirdMode.Walk || lastMode == BirdMode.Peck
                || cur == BirdMode.Walk || cur == BirdMode.Peck;
            if (midHunt)
            {
                ArmDirtInsectHunt(i, control);
                return true;
            }
            return false;
        }
        BirdMode last = members[i].lastHuntMode;
        bool landHunt = last == BirdMode.Walk || last == BirdMode.Peck
            || control.mode == BirdMode.Walk || control.mode == BirdMode.Peck;
        if (!landHunt)
            return false;
        bool onDirt = sensors != null && sensors.bothGrounded;
        if (onDirt)
        {
            // Write Sit before clearing soil — preferDirtLand false with
            // leftover Peck desynced HuntPreferDirt from IsHuntSoil.
            WriteHuntMode(i, control, BirdMode.Sit);
            control.preferDirtLand = false;
            return false;
        }

        control.preferDirtLand = true;
        WriteHuntMode(i, control, BirdMode.Walk);
        return true;
    }

    private static bool MarkerIsHostile(
        FactionMember self, Transform marker, FactionMember other)
    {
        if (marker == null)
            return false;
        if (self == null || other == null)
            return true;
        return FactionTable.IsHostile(self, other);
    }

    // Forage too: sitHold is not only Pursue. TickFlock Sit must not lose to Fly.
    private bool HoldLandSit(int i, BirdWander wander, BirdSensors sensors, float now)
    {
        bool down = sensors != null
            && (sensors.bothGrounded || sensors.treeSupport);
        if (!down)
        {
            members[i].wasAirborne = true;
            return false;
        }

        if (members[i].wasAirborne)
        {
            members[i].wasAirborne = false;
            float hold = members[i].hasPerch ? sitHold : Mathf.Max(0.35f, dirtSitHold);
            members[i].restUntil = now + hold;
            if (wander != null)
                wander.ForceSit(now, sitMin, sitMax, turnMin, turnMax);
        }

        return now < members[i].restUntil;
    }

    private void TickFlee(
        int i,
        BirdController control,
        OrganismVoice voice,
        Vector2 pos,
        float now,
        int heardCount)
    {
        if (control == null)
            return;
        control.preferDirtLand = false;

        float dx = 0f;
        bool saw = threat != null && OrganismAlert.SeesAhead(
            pos.x, control.FacingSign(), threat.position.x, threatRange, 0.12f, out dx);
        bool heard = voice != null && OrganismAlert.HearsForeignAlarm(
            voice.SourceId, pos, heardBuf, heardCount);

        if (saw)
        {
            bool pack = members[i].wander != null && crowCount >= 2;
            if (pack && threat != null)
            {
                PulseMob(threat.position.x, now);
                control.SetFacing(dx > 0f ? 1f : -1f);
            }
            else
            {
                if (voice != null)
                    voice.Cry(VoiceKind.Alarm);
                control.SetFacing(dx > 0f ? -1f : 1f);
            }

            members[i].alertUntil = now + alertHold;
        }
        else if (heard)
            members[i].alertUntil = now + alertHold;

        if (members[i].wander != null && crowCount >= 2 && now < mobUntil)
        {
            // B-HERO-2 r2: the minimum standoff covers the threat/mob lane too. A
            // crow within HeroAvoid of the hero BODY (2-D, pelvis; root fallback)
            // does not stoop: it steers away from him and holds the steer
            // HeroOutHold s, so it never crowds or lands on the standing hero.
            if (Bird.HeroBodyKeepAway && hasPlayer)
            {
                Vector2 hp;
                if (BirdDrive.HeroBodyPos(now, out hp))
                {
                    float ox = pos.x - hp.x;
                    float oy = pos.y - hp.y;
                    if (ox * ox + oy * oy < Bird.HeroAvoid * Bird.HeroAvoid
                        && members[i].heroOutUntil <= now)
                    {
                        members[i].heroOutUntil = now + Bird.HeroOutHold;
                        if (levelCrowLife)
                            Level1CrowLog(i, "takeoff", "standoff", "apple", pos.x);
                    }
                }
            }
            if (members[i].heroOutUntil > now)
            {
                float ox = pos.x - playerX;
                if (Mathf.Abs(ox) > 0.12f)
                    control.SetFacing(ox > 0f ? 1f : -1f);
                control.mode = BirdMode.Fly;
                return;
            }
            float mdx = mobX - pos.x;
            if (Mathf.Abs(mdx) > 0.12f)
                control.SetFacing(mdx > 0f ? 1f : -1f);
            bool air = members[i].sensors == null || !members[i].sensors.bothGrounded;
            control.mode = air && Mathf.Abs(mdx) < mobStoop
                ? BirdMode.Attack
                : BirdMode.Fly;
            return;
        }

        control.mode = members[i].bird != null
            ? members[i].bird.FleeBodyMode
            : BirdMode.Fly;
    }

    private void RefreshPlayer(float now)
    {
        hasPlayer = false;
        if (keepAwayRange < 0.05f)
            return;
        // req-237 Part 2: the level reads the hero PELVIS body - the root never
        // leaves the spawn (autoSyncTransforms off; Part 1: seeX stood at -8.000
        // while the pelvis walked to +1.077).
        if (levelCrowLife && levelHeroBody != null)
        {
            playerX = levelHeroBody.position.x;
            hasPlayer = true;
            return;
        }
        player = BirdDrive.CachedPlayer(now);
        if (player == null)
            return;
        playerX = player.position.x;
        // B-HERO-2: the hero's body, not the root that stays at the spawn.
        float bx;
        if (Bird.HeroBodyKeepAway && BirdDrive.HeroBodyX(now, out bx))
            playerX = bx;
        hasPlayer = true;
    }

    private void TickFlockEvents(float now)
    {
        if (rng == null)
            rng = new System.Random(seed);

        TickCrowMob(now);
        TickLevelYardReturn();

        // B-FLOCK-2 r2: inside the calm window the startle is not armed - the landed
        // pack stays on the dirt until the next bout or the window's end.
        if (now >= startleUntil && now >= packLandUntil && now >= nextStartle && now >= mobUntil
            && !(Bird.MobCalmKnob > 0f && now < calmUntil)
            && !(levelCrowLife && Level1FlushedAny()))
        {
            startleSource = PickSittingCrow();
            if (startleSource < 0)
                startleSource = PickAnyCrow();
            if (startleSource >= 0)
            {
                startleBegin = now;
                startleUntil = now + Mathf.Max(0.8f, startleHold);
                packLandUntil = startleUntil + Mathf.Max(0.4f, packLandHold);
                nextStartle = packLandUntil + RandRange(startleMin, startleMax);
                StartleCount++;
                BirdController src = members[startleSource].control;
                startleFace = src != null ? src.FacingSign() : 1f;
                if (members[startleSource].voice != null)
                    members[startleSource].voice.Cry(VoiceKind.Alarm);
            }
            else
                nextStartle = now + RandRange(4f, 9f);
        }

        if (now >= playUntil && now >= nextPlay
            && now >= startleUntil && now >= packLandUntil && now >= mobUntil)
        {
            int a;
            int b;
            PickAirPair(out a, out b);
            if (a >= 0 && b >= 0 && a != b)
            {
                playA = a;
                playB = b;
                playUntil = now + Mathf.Max(0.4f, playHold);
                nextPlay = playUntil + RandRange(playMin, playMax);
            }
            else
                nextPlay = now + RandRange(3f, 6f);
        }
    }

    // req-237 Part 2: the level's startle is the seated pack's own alarm - while
    // the pack is flushed out to the sites it is not armed (the flush trip is
    // the story there, and the resettle holds until the hero leaves the yard).
    private bool Level1FlushedAny()
    {
        if (members == null)
            return false;
        for (int i = 0; i < members.Length; i++)
        {
            if (members[i].flushed)
                return true;
        }
        return false;
    }

    private int PickSittingCrow()
    {
        int pick = -1;
        int n = 0;
        for (int i = 0; i < members.Length; i++)
        {
            if (members[i].wander == null || members[i].control == null)
                continue;
            BirdMode m = members[i].control.mode;
            if (m != BirdMode.Sit && m != BirdMode.Peck)
                continue;
            n++;
            if (rng.Next(n) == 0)
                pick = i;
        }

        return pick;
    }

    private int PickAnyCrow()
    {
        int pick = -1;
        int n = 0;
        for (int i = 0; i < members.Length; i++)
        {
            if (members[i].wander == null || members[i].control == null)
                continue;
            n++;
            if (rng.Next(n) == 0)
                pick = i;
        }

        return pick;
    }

    private void PickAirPair(out int a, out int b)
    {
        a = -1;
        b = -1;
        int n = 0;
        for (int i = 0; i < members.Length; i++)
        {
            if (members[i].wander == null || members[i].control == null)
                continue;
            if (!members[i].control.IsFlightMode())
                continue;
            n++;
            if (rng.Next(n) == 0)
            {
                b = a;
                a = i;
            }
            else if (b < 0 || rng.Next(n) == 0)
                b = i;
        }

        if (a == b)
            b = -1;
    }

    private void ApplyCrowOverlay(
        int i,
        BirdWander wander,
        Vector2 pos,
        float home,
        float now,
        ref BirdMode mode,
        ref float face)
    {
        if (PreyIsLive() && PreyIsInsect())
            return;

        float stagger = Mathf.Max(0f, takeoffStagger) * i;
        bool startling = now >= startleBegin + stagger && now < startleUntil;
        bool packingLand = now >= startleUntil && now < packLandUntil;
        bool chasing = now < playUntil && (i == playA || i == playB);
        bool away = TickKeepAway(i, pos);
        float drumX = 0f;
        bool drum = drumStartleRange > 0.05f
            && now >= members[i].drumIgnoreUntil
            && BirdSocial.Drumming(now, out drumX)
            && Mathf.Abs(pos.x - drumX) < drumStartleRange;
        bool mob = !levelCrowLife && crowCount >= 2 && now < mobUntil;
        ApplyCohesion(pos.x, home, ref face);

        if (packingLand && !away && !drum && !mob)
        {
            wander.ForceGlide(!members[i].hasPerch);
            if (mode == BirdMode.Fly || mode == BirdMode.Attack)
                mode = BirdMode.Glide;
            return;
        }

        if (!startling && !chasing && !away && !drum && !mob)
            return;

        // Keep-away while already airborne: face out and let Wander
        // Glide to dirt. ForceFly every frame cleared dirtBout.
        if (away && !startling && !chasing && !drum && !mob)
        {
            bool grounded = members[i].sensors != null
                && members[i].sensors.bothGrounded;
            bool onDirt = mode == BirdMode.Sit
                || mode == BirdMode.Walk
                || mode == BirdMode.Peck;
            if (grounded || onDirt)
            {
                wander.ForceFly(now, flyMin, flyMax, flyTurnMin, flyTurnMax);
                mode = BirdMode.Fly;
            }
            if (levelCrowLife)
            {
                // req-237 r2: the flushed level member streams straight to its
                // drawn site (homeX). Round 1 faced it away from the hero and
                // the west-bound pack flew east first, then back - the fence
                // sites were reached 10+ s late (K3 0/12).
                float sdx = members[i].homeX - pos.x;
                if (Mathf.Abs(sdx) > 0.12f)
                    face = sdx > 0f ? 1f : -1f;
            }
            else if (hasPlayer)
                FaceAwayFrom(pos.x, playerX, ref face);
            return;
        }

        // Chase is air play. Do not yank a sitter into a solo Fly — that is scatter.
        if (chasing && !startling && !away && !drum && !mob)
        {
            if (mode != BirdMode.Fly && mode != BirdMode.Glide && mode != BirdMode.Attack)
                return;
            int other = i == playA ? playB : playA;
            if (other >= 0 && other < members.Length && members[other].body != null)
            {
                float dx = members[other].body.position.x - pos.x;
                if (Mathf.Abs(dx) > 0.12f)
                    face = dx > 0f ? 1f : -1f;
            }
            return;
        }

        BirdMode prior = mode;
        wander.ForceFly(now, flyMin, flyMax, flyTurnMin, flyTurnMax);
        mode = BirdMode.Fly;
        if (away && hasPlayer)
            FaceAwayFrom(pos.x, playerX, ref face);
        else if (mob)
        {
            float dx = mobX - pos.x;
            if (Mathf.Abs(dx) > 0.12f)
                face = dx > 0f ? 1f : -1f;
            bool air = members[i].sensors == null || !members[i].sensors.bothGrounded;
            if (air && Mathf.Abs(dx) < mobStoop)
                mode = BirdMode.Attack;
        }
        else if (drum)
        {
            FaceAwayFrom(pos.x, drumX, ref face);
            if (prior == BirdMode.Sit || prior == BirdMode.Peck || prior == BirdMode.Walk)
                DrumStartleCount++;
            if (drumStartleCool > 0.05f)
                members[i].drumIgnoreUntil = now + drumStartleCool;
        }
        else if (startling)
            face = startleFace;
    }

    private void ApplyCohesion(float x, float home, ref float face)
    {
        if (cohesionRadius < 0.05f)
            return;
        float dx = x - home;
        if (Mathf.Abs(dx) <= cohesionRadius)
            return;
        face = dx > 0f ? -1f : 1f;
    }

    private void TickCrowMob(float now)
    {
        // req-237 Part 2: the level's mob layer is held off. Part 1: it armed
        // from the FROZEN root within 12 m with no faction check and re-armed
        // every second - it was the lifter, not the crowd. The level's flush is
        // the keep-away instead.
        if (levelCrowLife)
            return;
        if (threat == null || crowCount < 2 || mobNotice < 0.05f)
            return;

        float tx = threat.position.x;
        for (int i = 0; i < members.Length; i++)
        {
            if (members[i].wander == null || members[i].body == null)
                continue;
            if (Mathf.Abs(members[i].body.position.x - tx) > mobNotice)
                continue;
            PulseMob(tx, now);
            return;
        }
    }

    // B-TURN-3: separation + alignment in the flock context. O(N^2), N <= 50, and
    // only for airborne members. Reads the same member table the brains read; the
    // steering vector goes to BirdFlight, which owns the forces (the flock Update
    // itself still writes only mode and facing). -birdFlockSpace gates it; off, the
    // vectors stay zero and every older row keeps its shape.
    private void TickFlockSpacing()
    {
        if (!Bird.FlockSpace || members == null || members.Length < 2)
            return;
        float sep = Bird.FlockSepKnob >= 0f ? Bird.FlockSepKnob : spaceSeparation;
        float ali = Bird.FlockAliKnob >= 0f ? Bird.FlockAliKnob : spaceAlign;
        float radius = Mathf.Max(0.1f, spaceRadius);
        float alignRadius = radius * 3f;
        for (int i = 0; i < members.Length; i++)
        {
            BirdFlight flight = members[i].bird != null ? members[i].bird.flight : null;
            if (flight == null)
                continue;
            Rigidbody2D body = members[i].body;
            if (body == null
                || (members[i].sensors != null && members[i].sensors.bothGrounded))
            {
                flight.flockSteer = Vector2.zero;
                continue;
            }

            Vector2 pos = body.position;
            Vector2 vel = body.linearVelocity;
            Vector2 push = Vector2.zero;
            Vector2 match = Vector2.zero;
            int seen = 0;
            for (int j = 0; j < members.Length; j++)
            {
                if (j == i)
                    continue;
                Rigidbody2D other = members[j].body;
                if (other == null)
                    continue;
                Vector2 d = pos - other.position;
                float dist = d.magnitude;
                if (dist < 0.001f)
                {
                    // Exactly coincident: a deterministic side by index, not a NaN.
                    d = new Vector2(i < j ? -1f : 1f, 0f);
                    dist = 0.001f;
                }
                if (dist < radius)
                    push += d / (dist * dist);
                // Alignment is a flight thing: a sitting neighbour's zero velocity
                // must not read as "the pack is braking".
                bool otherAir = members[j].sensors == null || !members[j].sensors.bothGrounded;
                if (dist < alignRadius && otherAir)
                {
                    match += other.linearVelocity;
                    seen++;
                }
            }

            Vector2 steer = Vector2.zero;
            if (push != Vector2.zero)
                steer += push.normalized * sep;
            if (seen > 0)
            {
                Vector2 diff = match / seen - vel;
                if (diff != Vector2.zero)
                    steer += diff.normalized * ali;
            }
            // Cap so a tight knot cannot throw a body: one gravity is the scale.
            flight.flockSteer = Vector2.ClampMagnitude(steer, 12f);
        }
    }

    private void PulseMob(float x, float now)
    {
        // B-FLOCK-1: the rest window and the bout cut live at this chokepoint - the alarm
        // path (SeesAhead, ~TickAlert) pulses too, so a guard in TickCrowMob alone leaks.
        if (Bird.MobRestKnob > 0f && now < mobRestUntil)
            return;
        // B-FLOCK-2: the calm window is a longer rest for the mob layer - a pulse
        // from the same static mark cannot re-arm the pack inside it.
        if (Bird.MobCalmKnob > 0f && now < calmUntil)
            return;
        if (Bird.MobBoutKnob > 0f && now < mobUntil && now - mobBorn > Bird.MobBoutKnob)
        {
            mobRestUntil = now + Mathf.Max(1f, Bird.MobRestKnob);
            mobUntil = now;
            Debug.Log("[B-MOB] bout end t=" + now.ToString("F1") + " dur="
                      + (now - mobBorn).ToString("F1") + " rest="
                      + (mobRestUntil - now).ToString("F1"));
            if (Bird.MobCalmKnob > 0f)
            {
                calmUntil = now + Mathf.Max(1f, Bird.MobCalmKnob);
                calmOn = true;
                Debug.Log("[B-CALM] start t=" + now.ToString("F1") + " dur="
                          + (calmUntil - now).ToString("F1"));
            }
            return;
        }
        mobX = x;
        float until = now + Mathf.Max(0.4f, mobHold);
        if (until > mobUntil)
        {
            if (now >= mobUntil)
            {
                MobCount++;
                mobBorn = now;
            }
            mobUntil = until;
        }

        BirdSocial.PulseMob(x, now, mobHold);
    }

    private void ApplyHenOverlay(int i, Vector2 pos, ref BirdMode mode, ref float face)
    {
        if (henCount < 2)
            return;

        if (henLeader >= 0 && henLeader != i && henFollowSlack > 0.05f
            && henLeader < members.Length && members[henLeader].body != null)
        {
            float lx = members[henLeader].body.position.x;
            float gap = pos.x - lx;
            if (Mathf.Abs(gap) > henFollowSlack)
            {
                face = gap > 0f ? -1f : 1f;
                if (mode == BirdMode.Sit)
                    mode = BirdMode.Walk;
                return;
            }
        }

        if (henPeckTogether < 0.05f)
            return;
        // Locked hunter next to a live Insect must not copy a pebble Peck
        // (huntOnsetLog Forage/Peck). Follow above still runs.
        if (PreyIsLive() && PreyIsInsect() && i == lockedHunter)
            return;

        for (int j = 0; j < members.Length; j++)
        {
            if (j == i || members[j].chicken == null || members[j].control == null)
                continue;
            if (members[j].control.mode != BirdMode.Peck)
                continue;
            // Dirt forage copies a pebble Peck. Insect eat is Pursue on
            // the one hunter — a Forage copy would Rel Hostile without
            // Forage→Pursue and steal the quarry.
            if (members[j].life == LifeState.Pursue)
                continue;
            if (members[j].body == null)
                continue;
            if (Mathf.Abs(pos.x - members[j].body.position.x) > henPeckTogether)
                continue;
            mode = BirdMode.Peck;
            return;
        }
    }

    private bool TickKeepAway(int i, Vector2 pos)
    {
        if (keepAwayRange < 0.05f || !hasPlayer)
        {
            members[i].keepAway = false;
            return false;
        }
        if (levelCrowLife)
            return TickKeepAwayLevel(i, pos);

        float dist = Mathf.Abs(pos.x - playerX);
        float enter = keepAwayRange;
        float leave = keepAwayRange * Mathf.Max(1.05f, keepAwayRelease);
        if (members[i].keepAway)
        {
            if (dist > leave)
                members[i].keepAway = false;
        }
        else if (dist < enter || (Bird.HeroBodyKeepAway && dist < Bird.HeroStandoff))
            members[i].keepAway = true;

        return members[i].keepAway;
    }

    // req-237 Part 2 (level only): the PACK flush off the crown. The crown spans
    // 3.6 m - a per-crow radius staggered the takeoff over ~6 s (Part 1), so the
    // pack flushes as one crowd when the hero comes within keepAwayRange of the
    // crown centre, spread by each member's seeded jitter. Once flushed the latch
    // holds (no re-arm): a member stops fleeing when IT is past the release
    // radius, then it resettles at its drawn site. The pack returns to the apple
    // only after the hero leaves the yard (TickLevelYardReturn).
    private bool TickKeepAwayLevel(int i, Vector2 pos)
    {
        // B-HERO-2: the minimum standoff. Inside HeroStandoff of the hero body the
        // crow steers out at once and its resettle target leaves the radius.
        if (Bird.HeroBodyKeepAway && !members[i].keepAway
            && Mathf.Abs(pos.x - playerX) < Bird.HeroStandoff)
        {
            members[i].flushed = true;
            members[i].keepAway = true;
            PickResettleSite(i);
            Level1CrowLog(i, "takeoff", "standoff", "apple", pos.x);
            return true;
        }
        if (!members[i].flushed)
        {
            float jit = members[i].flushJit > 0.01f ? members[i].flushJit : 1f;
            if (Mathf.Abs(levelHomeX - playerX) < keepAwayRange * jit)
            {
                members[i].flushed = true;
                members[i].keepAway = true;
                PickResettleSite(i);
                Level1CrowLog(i, "takeoff", "keepAway", "apple", pos.x);
            }
        }
        else if (members[i].keepAway)
        {
            float leave = keepAwayRange * Mathf.Max(1.05f, keepAwayRelease);
            if (Mathf.Abs(pos.x - playerX) > leave)
            {
                members[i].keepAway = false;
                Level1CrowLog(i, members[i].siteKind == 3 ? "ground" : "perch", "keepAway",
                    Level1SiteName(members[i].siteKind), pos.x);
            }
        }
        return members[i].keepAway;
    }

    // req-237 Part 2: the seeded draw over the level's site table (the level
    // repeats entries to weight the mix). Dirt drops the perch, fence / lamp
    // keep the target as the member's pad so Wander flies it there.
    private void PickResettleSite(int i)
    {
        if (levelSites == null || levelSites.Length == 0)
            return;
        if (rng == null)
            rng = new System.Random(seed);
        int pick = rng.Next(levelSites.Length);
        members[i].siteKind = levelSiteKinds != null && pick < levelSiteKinds.Length
            ? levelSiteKinds[pick]
            : 1;
        Vector2 site = levelSites[pick];
        // B-HERO-2: never a pad inside the hero standoff (4 draws, then take it;
        // the per-tick standoff re-flush is the backstop).
        if (Bird.HeroBodyKeepAway)
        {
            int guard = 0;
            while (guard++ < 4 && Mathf.Abs(site.x - playerX) < Bird.HeroStandoff)
            {
                pick = rng.Next(levelSites.Length);
                members[i].siteKind = levelSiteKinds != null && pick < levelSiteKinds.Length
                    ? levelSiteKinds[pick]
                    : 1;
                site = levelSites[pick];
            }
        }
        if (members[i].siteKind == 3)
        {
            members[i].hasPerch = false;
            members[i].homeX = site.x;
        }
        else
        {
            members[i].hasPerch = true;
            members[i].perchX = site.x;
            members[i].perchY = site.y;
            members[i].homeX = site.x;
        }
    }

    // req-237 Part 2: the yard-return rule. Once the hero is farther from the
    // crown centre than keepAwayRange * keepAwayRelease the pack comes home: the
    // flush latch clears and every member's apple pad is restored.
    private void TickLevelYardReturn()
    {
        if (!levelCrowLife || !hasPlayer || members == null)
            return;
        float leave = keepAwayRange * Mathf.Max(1.05f, keepAwayRelease);
        if (Mathf.Abs(playerX - levelHomeX) < leave)
            return;
        for (int i = 0; i < members.Length; i++)
        {
            if (!members[i].flushed)
                continue;
            members[i].flushed = false;
            members[i].keepAway = false;
            members[i].siteKind = 0;
            members[i].hasPerch = true;
            members[i].perchX = members[i].appleX;
            members[i].perchY = members[i].appleY;
            members[i].homeX = members[i].appleX;
            Level1CrowLog(i, "return", "none", "apple",
                members[i].body != null ? members[i].body.position.x : members[i].homeX);
        }
    }

    // req-237 Part 2: the level's act-site names for the decision log.
    private static string Level1SiteName(int kind)
    {
        if (kind == 1)
            return "fence";
        if (kind == 2)
            return "lamp";
        if (kind == 3)
            return "dirt";
        return "apple";
    }

    // req-237 Part 2: one line per crow decision (level only).
    private void Level1CrowLog(int i, string act, string cause, string site, float x)
    {
        Debug.Log("LEVEL1 CROW t="
            + Time.time.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)
            + " id=" + (i + 1) + " act=" + act + " cause=" + cause
            + " site=" + site + " x="
            + x.ToString("F2", System.Globalization.CultureInfo.InvariantCulture));
    }

    // req-237 Part 1 diagnosis (read-only, no behaviour): what the flock itself reads
    // as the hero, the windows it acts on, and one member's support state. Only the
    // headless level calls these; the game never does.
    public bool Level1HasPlayer { get { return hasPlayer; } }
    public float Level1PlayerSeeX { get { return playerX; } }
    public float Level1StartleBegin { get { return startleBegin; } }
    public float Level1StartleUntil { get { return startleUntil; } }
    public float Level1MobUntil { get { return mobUntil; } }
    public float Level1PlayUntil { get { return playUntil; } }
    public int Level1PlayA { get { return playA; } }
    public int Level1PlayB { get { return playB; } }

    public void Level1MemberDiag(int i, out bool perch, out bool ground, out bool keepAway,
        out int wanderTakeoffs, out float alertUntil, out bool hasPerch, out bool treeSupport,
        out bool bothGrounded)
    {
        perch = false; ground = false; keepAway = false; wanderTakeoffs = 0; alertUntil = 0f;
        hasPerch = false; treeSupport = false; bothGrounded = false;
        if (i < 0 || members == null || i >= members.Length)
            return;
        Member m = members[i];
        hasPerch = m.hasPerch;
        keepAway = m.keepAway;
        alertUntil = m.alertUntil;
        wanderTakeoffs = m.wander != null ? m.wander.takeoffCount : 0;
        treeSupport = m.sensors != null && m.sensors.treeSupport;
        bothGrounded = m.sensors != null && m.sensors.bothGrounded;
        perch = hasPerch && treeSupport;
        ground = bothGrounded && !perch;
    }

    private static void FaceAwayFrom(float selfX, float otherX, ref float face)
    {
        float dx = selfX - otherX;
        if (Mathf.Abs(dx) <= 0.12f)
            return;
        face = dx > 0f ? 1f : -1f;
    }

    private void WriteCrowPose(BirdController control, BirdWander wander, float now, int i)
    {
        if (control == null || wander == null)
            return;
        control.idleLook = wander.IdleLook;
        control.idlePreen = wander.IdlePreen;
        control.idleHop = wander.IdleHop;
        bool chasing = now < playUntil && (i == playA || i == playB);
        control.playRoll = chasing ? Mathf.Sin(now * 8.5f) : wander.PlayRoll;
        control.searchCrouch = wander.SearchCrouch;
        control.peckWindup = wander.PeckWindup;
        control.peckStrike = wander.PeckStrike;
        control.peckHeadUp = wander.PeckHeadUp;
        control.peckMiss = wander.PeckMiss;
    }

    private static void ClearCrowPose(BirdController control)
    {
        if (control == null)
            return;
        if (control.idleLook == 0f && control.idlePreen == 0f
            && control.idleHop == 0f && control.playRoll == 0f
            && control.searchCrouch == 0f && control.peckWindup == 0f
            && control.peckStrike == 0f && control.peckHeadUp == 0f
            && !control.peckMiss)
            return;
        control.idleLook = 0f;
        control.idlePreen = 0f;
        control.idleHop = 0f;
        control.playRoll = 0f;
        control.searchCrouch = 0f;
        control.peckWindup = 0f;
        control.peckStrike = 0f;
        control.peckHeadUp = 0f;
        control.peckMiss = false;
    }

    private float RandRange(float min, float max)
    {
        if (rng == null)
            rng = new System.Random(seed);
        if (max < min)
            max = min;
        return (float)(min + rng.NextDouble() * (max - min));
    }
}
