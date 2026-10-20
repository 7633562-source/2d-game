using UnityEngine;

// Circumstances, not a force. In Update writes only mode and facing.
// Prey — hunt Insect (Forage → Pursue → Peck). Wander — sit / fly / land.
[DefaultExecutionOrder(-50)]
public class BirdDrive : MonoBehaviour
{
    public enum Kind
    {
        Prey = 0,
        Wander = 1
    }

    [Header("Policy")]
    public Kind kind = Kind.Prey;
    [Tooltip("The stand turns this on explicitly. Play is not headless anyway.")]
    public bool allowHeadless;

    [Header("Prey")]
    public Transform prey;
    [Tooltip("Notice prey and take off, m.")]
    public float noticeRange = 40f;
    [Tooltip("Start the stoop, m along X.")]
    public float stoopRange = 6f;
    [Tooltip("Stoop only above this height over stance, m.")]
    public float stoopMinHeight = 0.45f;
    [Tooltip("Count as arrived / overshot, m.")]
    public float arriveSlop = 1.2f;
    [Tooltip("After landing, sit before taking off again, s.")]
    public float sitHold = 2.2f;

    [Header("Random flight")]
    public int wanderSeed = 1;
    [Tooltip("Crow sit bout, s. With flyMin/Max this is about 70% of forage time.")]
    public float sitMin = 12f;
    public float sitMax = 22f;
    [Tooltip("Crow fly bout, s. About 30% of forage with sitMin/Max.")]
    public float flyMin = 2.5f;
    public float flyMax = 5.5f;
    [Tooltip("Enter Glide this many seconds before the fly phase ends.")]
    public float glideLead = 1.6f;
    public float turnMin = 1.1f;
    public float turnMax = 2.8f;
    [Tooltip("Facing flips while flying, s. Longer than the 0.4 s pitch-soft window.")]
    public float flyTurnMin = 1.2f;
    public float flyTurnMax = 2.4f;
    [Tooltip("Turn toward home if it flew farther than this, m. 0 — no leash.")]
    public float leash = 7f;
    [Tooltip("When a perch is bound, turn back at this distance from the slot, m. 0 — use leash.")]
    public float orbitRadius = 4.6f;
    [Tooltip("Climb this far above the pad before counting orbit laps, m.")]
    public float cruiseClearance = 1.15f;
    [Tooltip("Short Glide pulses during Fly, s. 0 — no pulse (cruise until bout Glide).")]
    public float weaveGlide = 0f;
    [Tooltip("Hold Glide facing while |x − homeX| is inside this, m.")]
    public float homeFaceDeadzone = 0.15f;
    [Tooltip("Dirt forage walk bout, s. Crow and hen both use this.")]
    public float walkMin = 1.8f;
    public float walkMax = 3.8f;

    [Header("Threat")]
    [Tooltip("Marker ahead along facing. Empty = no alarm.")]
    public Transform threat;
    [Tooltip("See the marker and cry, m.")]
    public float threatRange = 12f;
    [Tooltip("Keep Fly this long after see/hear, s.")]
    public float alertHold = 2.2f;

    [Header("Crow")]
    [Tooltip("Fly up when the player human is closer than this along X, m. 0 — off.")]
    public float keepAwayRange = 4.2f;
    [Tooltip("Leave keep-away when farther than range times this.")]
    public float keepAwayRelease = 1.45f;
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

    [Header("Hen")]
    [Tooltip("Walk toward the first living hen if farther than this, m.")]
    public float henFollowSlack = Chicken.FollowSlack;
    [Tooltip("Peck if another hen pecks inside this, m.")]
    public float henPeckTogether = Chicken.PeckTogether;

    [Header("Life")]
    [Tooltip("Self vs hostile. Stronger other → Flee; weaker → Pursue.")]
    public float ownStrength = 1f;
    [Tooltip("Strength of a Hostile threat marker. Rel decides Hostile.")]
    public float threatStrength = 2f;
    [Tooltip("Strength of a Hostile prey marker. Rel decides Hostile.")]
    public float preyStrength = 0.5f;

    public LifeState Life { get; private set; } = LifeState.Forage;
    public int LifeSwitchCount { get; private set; }
    // Spawn is Forage. First Update may already Pursue (insect ahead);
    // the summary `life` field is the last state (Forage after the eat).
    public bool SawLifeForage { get; private set; } = true;
    public bool SawLifePursue { get; private set; }

    public int TakeoffCount => wander != null
        ? wander.takeoffCount
        : pecker != null ? pecker.takeoffCount : 0;
    public int WalkCount => huntWalkCount
        + (wander != null
            ? wander.walkCount
            : chicken != null ? chicken.walkCount : 0);
    public int PeckCount => huntPeckCount
        + (wander != null
            ? wander.peckCount
            : pecker != null ? pecker.peckCount
            : chicken != null ? chicken.peckCount : 0);
    public int PreyEatenCount { get; private set; }
    public int BeakHitCount => beakHit != null ? beakHit.HitCount : 0;
    public int LandCount => wander != null
        ? wander.landCount
        : pecker != null ? pecker.sitCount
        : chicken != null ? chicken.sitCount : 0;
    public int FacingFlipCount => wander != null
        ? wander.facingFlipCount
        : pecker != null ? pecker.facingFlipCount
        : chicken != null ? chicken.facingFlipCount : 0;
    public int PerchLandCount => wander != null
        ? wander.perchLandCount
        : pecker != null ? pecker.perchLandCount : 0;
    public int DirtRejectCount => wander != null
        ? wander.dirtRejectCount
        : pecker != null ? pecker.dirtRejectCount : 0;
    public int CryCount => voice != null ? voice.CryCount : 0;
    public int MobCount { get; private set; }
    public int DrumStartleCount { get; private set; }
    // Cached stamps. Recorder latches Rel without GetComponent / Find.
    internal FactionMember HuntSelfFaction => selfFaction;
    internal FactionMember HuntPreyFaction => preyFaction;
    // Brain write, not TickFlock contact Sit/Fly. Onset / huntFlyFraction
    // used control.mode and counted leftover pad Sit as the hunt body;
    // Park Clear (include Nothing) only relives on Peck — lastPreyMode
    // is that Peck/Walk intent after ClampInsectHuntMode.
    internal BirdMode HuntBodyMode => lastPreyMode;
    // Body IsHuntSoil (preferDirtLand or dirt Peck). preferDirtLand alone
    // lied after HoldDirtAfterHunt cleared soil while mode was still Peck.
    internal bool HuntPreferDirt => control != null && control.IsHuntSoil();
    public float BoundPerchY => homeY;
    internal float SocialX => bodyRb != null ? bodyRb.position.x : transform.position.x;
    internal BirdMode SocialMode => control != null ? control.mode : BirdMode.Sit;

    private BirdController control;
    private BirdSensors sensors;
    private OrganismVoice voice;
    private readonly SoundEvent[] heardBuf = new SoundEvent[8];
    private float alertUntil;
    private Rigidbody2D bodyRb;
    private bool wasAirborne;
    private float restUntil;
    private float homeX;
    private float homeY;
    private bool hasPerch;
    // Body ReleasePerch on preferDirtLand. Brain still had hasPerch +
    // homeX on the twig — leash / BIRD-5 dirtReject ForceFly yanked
    // walkThrust off the Insect. Drop the slot for the eat; restore
    // after the quarry is gone.
    private bool huntLeftSlot;
    private float huntSlotX;
    private float huntSlotY;
    private BirdWander wander;
    private ChickenWander chicken;
    private WoodpeckerWander pecker;
    private Bird bodyKind;
    private BirdForageStyle forageStyle;
    private BirdMode lastPreyMode = BirdMode.Sit;
    private FactionMember selfFaction;
    private Transform threatCached;
    private FactionMember threatFaction;
    private Transform preyCached;
    private FactionMember preyFaction;
    private Insect preyInsect;
    private Damageable preyLife;
    private BeakStrike beakHit;
    private int huntWalkCount;
    private int huntPeckCount;
    private bool hadLivePrey;
    private bool beakStrikeResolved;
    // First Update stays Forage. ChooseWake on that frame used to write
    // Pursue before the stand sampled (WaitForFixedUpdate can run after
    // Update). BindPrey already armed Walk; Peck and Hurt wait Pursue.
    private bool lifeWakeArmed;
    // After the eat, ChooseLife is Forage. wander.Step used that frame
    // to write leftover Fly (expired Sit bout from Start, never Stepped).
    private float postHuntSitUntil;
    private Transform player;
    private bool keepAway;
    // B-HERO-2 r2: until when this bird holds the hero-out steer (mob lanes).
    private float heroOutUntil;
    private bool wasDrumming;
    private float drumIgnoreUntil;
    private float soloGlideAt = -1f;
    // One PlayerInputSource in the scene. Sticky after hit; miss retries at 0.5 s.
    private static Transform sharedPlayer;
    private static float sharedPlayerScanAt;

    void Awake()
    {
        // Controller Awake is order 0; BodyRb is not ready here (−50).
        control = GetComponent<BirdController>();
        sensors = GetComponent<BirdSensors>();
        bodyKind = GetComponent<Bird>();
        beakHit = GetComponent<BeakStrike>();
        if (beakHit != null)
            beakHit.BindHunter(this);
        voice = GetComponent<OrganismVoice>();
        if (voice == null)
            voice = gameObject.AddComponent<OrganismVoice>();
    }

    void Start()
    {
        EnsureBodyRb();

        homeX = bodyRb != null ? bodyRb.position.x : transform.position.x;
        if (control != null)
        {
            control.takeoffDelay = 0f;
            control.turnAfter = 0f;
        }

        EnsureSelfFaction();
        RefreshMarkerFactions();
        forageStyle = bodyKind != null ? bodyKind.ForageStyle : BirdForageStyle.CrowCruise;
        if (forageStyle == BirdForageStyle.ChickenGround)
        {
            chicken = new ChickenWander();
            float henFace = control != null ? control.FacingSign() : 1f;
            chicken.Reset(wanderSeed, Time.time, 1.2f, 3.4f, henFace);
            BirdSocial.AddHen(this);
        }
        else if (forageStyle == BirdForageStyle.WoodpeckerDrum)
        {
            pecker = new WoodpeckerWander();
            float peckFace = control != null ? control.FacingSign() : 1f;
            pecker.Reset(wanderSeed, Time.time, 14f, 28f, peckFace);
        }
        else if (kind == Kind.Wander)
            EnsureWanderClock();
        else
        {
            // Insect on dirt: walk and Peck. Do not sit three seconds then stoop.
            restUntil = PreyIsInsect() ? Time.time : Time.time + Mathf.Max(sitHold, 3f);
        }

        // BindPrey may already have written Walk. A Sit here used to
        // land between that write and TryArm, and leftover Fly pitch sat
        // on HuntBeak (Rel never Hostile). Arm Walk first; Sit only if
        // there is no live Insect in noticeRange. Peck waits Pursue.
        // Life stays Forage until ChooseWake so the stand samples
        // Forage→Pursue.
        if (!TryArmBoundInsectHunt() && control != null && Life != LifeState.Pursue)
            control.mode = BirdMode.Sit;
    }

    // Spawn / stand bind the live Insect after AddComponent. Field-assign
    // used to leave Start with prey null, then wander Sit until Update.
    public void BindPrey(Transform quarry)
    {
        prey = quarry;
        if (quarry == null)
        {
            preyCached = null;
            preyFaction = null;
            preyInsect = null;
            preyLife = null;
            return;
        }

        EnsureBodyRb();
        EnsureSelfFaction();
        RefreshMarkerFactions();
        TryArmBoundInsectHunt();
    }

    private void EnsureBodyRb()
    {
        if (bodyRb != null)
            return;
        if (control == null)
            control = GetComponent<BirdController>();
        bodyRb = control != null ? control.BodyRb : null;
        if (bodyRb != null)
            return;
        Transform body = transform.Find("Body");
        bodyRb = body != null ? body.GetComponent<Rigidbody2D>() : null;
    }

    void OnDestroy()
    {
        BirdSocial.DropHen(this);
        BirdSocial.DropCrow(this);
    }

    void Update()
    {
        if (HeadlessTrial.Active && !allowHeadless) return;
        if (control == null || sensors == null) return;

        // Law: Forage until the bound Insect is in noticeRange, then
        // ChooseWake Pursue. Out of range stays Forage (wander). Alarm
        // cannot abort a seen eat. A stronger wolf still Flee.
        RefreshMarkerFactions();
        DropDeadPrey();
        if (HoldSpawnForage())
            return;
        EnterLife(ChooseLife());
        // Bound Insect in notice is food. Leftover Alarm Flee wrote Fly
        // before Rel stamped Hostile; huntOnsetLog never reached Pursue.
        // A seen stronger wolf still Flee.
        if (Life == LifeState.Flee
            && BoundInsectInNotice(
                bodyRb != null ? bodyRb.position.x : transform.position.x)
            && !SeesStrongerThreat())
            EnterLife(QuarryIsInsectHunt() ? LifeState.Pursue : LifeState.Forage);
        if (Life == LifeState.Flee)
        {
            TickFlee();
            ClearCrowPose();
            ArmInsectEatGate();
            return;
        }

        if (Life == LifeState.Sleep)
        {
            WriteSleepBody();
            ClearCrowPose();
            ArmInsectEatGate();
            return;
        }

        if (Life == LifeState.Pursue)
        {
            TickPrey();
            ClearCrowPose();
            ArmInsectEatGate();
            return;
        }

        TickForage();
        ArmInsectEatGate();
    }

    // Spawn Life is Forage. Arm Walk / preferDirtLand (soil, walkThrust)
    // without ChooseWake. HuntBeak waits Peck. The stand still samples
    // Forage→Pursue. Do not TickPrey here: Peck waits Pursue
    // (card: see Hostile Insects → Pursue → dirt Peck).
    private bool HoldSpawnForage()
    {
        if (lifeWakeArmed)
            return false;
        lifeWakeArmed = true;
        RefreshMarkerFactions();
        DropDeadPrey();
        float selfX = bodyRb != null ? bodyRb.position.x : transform.position.x;
        if (BoundInsectInNotice(selfX))
        {
            GlanceBoundInsect();
            HoldWanderOffHunt();
            ArmForageHuntWalk();
            ClearCrowPose();
        }
        else
            TickForage();
        ArmInsectEatGate();
        return true;
    }

    // Solo has no FlockDrive ArmInsectEat. BindHunter can miss if
    // BeakStrike was not on the root in Awake; Hurt Insects only
    // while Pursue (dirt-forage Peck must not eat). One lookup if Awake missed.
    private void ArmInsectEatGate()
    {
        if (beakHit == null && !beakStrikeResolved)
        {
            beakHit = GetComponent<BeakStrike>();
            beakStrikeResolved = true;
        }
        if (beakHit == null)
            return;
        beakHit.BindHunter(this);
        beakHit.ArmInsectEat(Life == LifeState.Pursue);
    }

    // Spawn stamps Wildlife.Bird. A leftover Wildlife.None made Rel
    // Neutral, then TickPrey wrote leftover stoop Fly at the Insect.
    private void EnsureSelfFaction()
    {
        if (selfFaction == null)
            selfFaction = GetComponent<FactionMember>();
        if (selfFaction == null
            || selfFaction.faction != FactionId.Wildlife
            || selfFaction.subfaction != SubfactionId.Bird)
            selfFaction = FactionStamp.Bird(gameObject);
    }

    private void EnterLife(LifeState next)
    {
        if (next == LifeState.Forage)
            SawLifeForage = true;
        if (next == LifeState.Pursue)
        {
            SawLifePursue = true;
            // Soil / walkThrust before TickPrey. Leftover Alarm Flee
            // cleared preferDirtLand, then the same Update Pursued —
            // InsectHuntOnDirt still saw leftover treeSupport and the
            // first Peck never armed (HuntBeak stays Walk-parked).
            // Drop hasPerch: body ReleasePerch alone left homeX on the
            // twig (leash / dirtReject ForceFly off the Insect).
            if (control != null && PreyIsLive() && PreyIsInsect())
                ArmDirtInsectHunt();
        }
        if (next == Life)
            return;
        Life = next;
        LifeSwitchCount++;
    }

    private LifeState ChooseLife()
    {
        RefreshMarkerFactions();
        DropDeadPrey();
        GlanceBoundInsect();
        float selfX = bodyRb != null ? bodyRb.position.x : transform.position.x;
        Vector2 pos = bodyRb != null ? bodyRb.position : (Vector2)transform.position;
        float dx;
        bool threatHostile = MarkerIsHostile(selfFaction, threat, threatFaction);
        // Bound Insect in noticeRange is food. Rel Hostile is the hunt
        // gate (card). BeakStrike is still the mouth. Glance turns the
        // nose; do not wait for minAhead. First see is noticeRange;
        // Pursue holds that insect until it is gone — a leftover cruise
        // past the leash used to drop Forage and re-arm Fly.
        bool insectNear = BoundInsectInNotice(selfX);
        // Card: Forage + sees Insects (weaker Hostile) → Pursue.
        // Rel is the hunt gate. Distance-only Pursue used to hunt a
        // Neutral leftover and also "see" far behind the back. Glance
        // already turned; MayPursue is the see (cone, or PeckHold
        // overlap). Alarm stays muted on insectNear so TickFlee Fly
        // cannot abort the turn.
        float face = control != null ? control.FacingSign() : 1f;
        bool sawInsectAhead = prey != null && Insect.MayPursue(
            selfX, face, prey.position.x, noticeRange, Life == LifeState.Pursue);
        bool insectHunt = QuarryIsInsectHunt()
            && (sawInsectAhead || Life == LifeState.Pursue);
        bool sawInsect = insectHunt;
        bool preyHostile = sawInsect
            || (PreyIsLive() && !PreyIsInsect()
                && MarkerIsHostile(selfFaction, prey, preyFaction));
        bool sawThreat = threatHostile && OrganismAlert.SeesAhead(
            selfX, control.FacingSign(), threat.position.x, threatRange, 0.12f, out dx);
        bool sawPrey = sawInsect
            || (preyHostile && !insectHunt && prey != null
                && Mathf.Abs(prey.position.x - selfX) < noticeRange);
        bool heard = voice != null && OrganismAlert.HearsForeignAlarm(
            voice.SourceId, pos, Time.time, 0.6f, heardBuf);
        // Live Rel-Hostile Insect is food. Leftover pack Alarm used to
        // ChooseWake Flee → Fly before Glance could Pursue. Mute on the
        // hunt, not only on noticeRange — closing past the leash must
        // not Flee. Mute the same Alarm while Rel is still Neutral
        // (Wildlife.None one frame): Bound Insect in notice is food,
        // TickFlee Fly skipped Forage→Pursue.
        if (insectHunt || insectNear)
            heard = false;
        bool stronger = sawThreat && OrganismLife.HostileIsStronger(ownStrength, threatStrength);
        // Insects are always weaker food. Do not Flee them via preyStrength.
        bool weaker = insectHunt
            || (sawPrey
                && !OrganismLife.HostileIsStronger(ownStrength, PreyStrength()));
        return OrganismLife.ChooseWake(
            stronger, weaker, heard,
            insectHunt || insectNear ? false : Time.time < alertUntil);
    }

    private void RefreshMarkerFactions()
    {
        EnsureSelfFaction();
        if (threat != threatCached)
        {
            threatCached = threat;
            threatFaction = threat != null ? threat.GetComponent<FactionMember>() : null;
        }

        if (prey != preyCached)
        {
            preyCached = prey;
            Insect.BindHuntRefs(prey, out preyInsect, out preyFaction, out preyLife);
            return;
        }

        // First bind can run before Insect.Awake stamps faction 3
        // (AddComponent Start order). preyCached then matches and the
        // old retry required preyInsect != null, so TickPrey saw an
        // unmarked marker and wrote leftover Fly.
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
            HoldWanderOffHunt();
            // BIRD-5: dirt is not home — restore the twig slot after the
            // eat so forage can Fly back.
            if (huntLeftSlot)
            {
                hasPerch = true;
                homeX = huntSlotX;
                homeY = huntSlotY;
                huntLeftSlot = false;
            }
        }
        hadLivePrey = live;
        if (live)
            return;
        // Destroyed Insect is Unity-null, not C# null. Clear so hasPrey
        // is false after the eat, not a stale fake reference.
        prey = null;
        preyCached = null;
        preyFaction = null;
        preyInsect = null;
        preyLife = null;
    }

    private bool PreyIsLive()
    {
        if (prey == null)
            return false;
        if (preyLife != null && preyLife.IsDead)
            return false;
        return true;
    }

    private bool PreyIsInsect()
    {
        if (preyInsect != null)
            return true;
        return preyFaction != null && preyFaction.faction == FactionId.Insects;
    }

    // Hunt does not Step wander. Hold leftover Fly/Glide/Forage and an
    // expired Sit bout so the first Forage Step is not leftover cruise Fly.
    private void HoldWanderOffHunt()
    {
        float now = Time.time;
        if (wander != null)
            wander.HoldHuntSit(now, sitMin, sitMax, turnMin, turnMax);
        if (chicken != null)
            chicken.HoldHuntSit(now, 1.2f, 3.4f);
        if (pecker != null)
            pecker.HoldHuntSit(now, 14f, 28f);
        if (control != null)
            control.takeoffDelay = 0f;
    }

    // Seen stronger Hostile. Pack Alarm is not this — that leftover Fly
    // aborted Forage→Pursue while the Insect was still food.
    private bool SeesStrongerThreat()
    {
        if (threat == null || control == null)
            return false;
        if (!MarkerIsHostile(selfFaction, threat, threatFaction))
            return false;
        if (!OrganismLife.HostileIsStronger(ownStrength, threatStrength))
            return false;
        float selfX = bodyRb != null ? bodyRb.position.x : transform.position.x;
        float dx;
        return OrganismAlert.SeesAhead(
            selfX, control.FacingSign(), threat.position.x, threatRange, 0.12f, out dx);
    }

    // Bound Insect in noticeRange: arm Walk and preferDirtLand
    // (soil, walkThrust, lift off). HuntBeak is Peck/Attack only —
    // Walk must not keep that query live for 8 m.
    // Peck waits Pursue. Do not EnterLife Pursue here — spawn is
    // Forage; ChooseWake on the first Update is Forage + see Hostile
    // → Pursue. A BindPrey Pursue skipped every Forage sample on the
    // stand. Do not TickPrey: that is the eat path.
    private bool TryArmBoundInsectHunt()
    {
        if (control == null)
            return false;
        RefreshMarkerFactions();
        DropDeadPrey();
        if (!PreyIsLive() || prey == null || !PreyIsInsect())
            return false;
        float selfX = bodyRb != null ? bodyRb.position.x : transform.position.x;
        if (Mathf.Abs(prey.position.x - selfX) >= noticeRange)
            return false;
        GlanceBoundInsect();
        HoldWanderOffHunt();
        // Start ran before BindPrey (AddComponent), saw prey null, and
        // armed sitHold. HoldLandSit then kept Sit after the eat.
        restUntil = Time.time;
        ArmForageHuntWalk();
        ArmInsectEatGate();
        return true;
    }

    // Forage Walk toward a bound Insect. Not Peck: Hurt waits Pursue.
    // preferDirtLand is soil / walkThrust, not a live HuntBeak (Peck).
    private void ArmForageHuntWalk()
    {
        if (control == null || prey == null)
            return;
        float selfX = bodyRb != null ? bodyRb.position.x : transform.position.x;
        ArmDirtInsectHunt();
        WritePreyMode(BirdMode.Walk, prey.position.x - selfX);
    }

    // Soil + leave the twig slot. Body ReleasePerch needs preferDirtLand;
    // brain must drop hasPerch or homeX stays on the pad and leash /
    // dirtReject ForceFly aborts the close (Park Clear until Peck).
    private void ArmDirtInsectHunt()
    {
        if (control == null)
            return;
        control.preferDirtLand = true;
        if (!hasPerch)
            return;
        huntLeftSlot = true;
        huntSlotX = homeX;
        huntSlotY = homeY;
        hasPerch = false;
        if (bodyRb != null)
            homeX = bodyRb.position.x;
    }

    // Forage + bound Hostile Insect in noticeRange → Pursue, then dirt Peck.
    // Rel is the hunt gate. A Neutral stamp must not steal Forage→Pursue.
    private bool TryEnterInsectHunt()
    {
        if (prey == null || control == null)
            return false;
        if (!PreyIsInsect())
            Insect.BindHuntRefs(prey, out preyInsect, out preyFaction, out preyLife);
        if (!PreyIsLive() || !PreyIsInsect())
            return false;
        float selfX = bodyRb != null ? bodyRb.position.x : transform.position.x;
        if (Mathf.Abs(prey.position.x - selfX) >= noticeRange)
            return false;
        if (!QuarryIsInsectHunt())
            return false;
        GlanceBoundInsect();
        float face = control.FacingSign();
        if (!Insect.MayPursue(
            selfX, face, prey.position.x, noticeRange, Life == LifeState.Pursue)
            && Life != LifeState.Pursue)
            return false;
        EnterLife(LifeState.Pursue);
        TickPrey();
        return true;
    }

    private bool BoundInsectInNotice(float selfX)
    {
        if (!PreyIsLive() || prey == null || !PreyIsInsect())
            return false;
        return Mathf.Abs(prey.position.x - selfX) < noticeRange;
    }

    // Bound quarry only — not a scene scan. Play Bird is Kind.Wander with
    // prey set; Kind.Prey-only glance left that crow Forage forever when
    // the nose pointed the other way. Other wanderers have prey == null.
    private void GlanceBoundInsect()
    {
        // Leftover Alarm Flee skipped the turn, then TickFlee wrote Fly
        // before MayPursue could fire. A real stronger wolf still Flee.
        // Pursue still glances outside PeckHold — WantCloseFace used to
        // be the only turn, and it reversed walkThrust inside the strip.
        if (Life == LifeState.Sleep)
            return;
        if (Life == LifeState.Flee && SeesStrongerThreat())
            return;
        if (!PreyIsLive() || !PreyIsInsect() || prey == null || control == null)
            return;
        float selfX = bodyRb != null ? bodyRb.position.x : transform.position.x;
        float face;
        if (!Insect.WantHuntFace(
            selfX, prey.position.x, noticeRange, control.FacingSign(),
            Life == LifeState.Pursue, out face))
            return;
        control.SetFacing(face);
    }

    // Dirt Walk/Peck. Rel Hostile is the hunt gate. A missing stamp is
    // not food — restamp, then BirdHuntsInsect. Null→true used to
    // Pursue while the summary latched Neutral.
    private bool QuarryIsInsectHunt()
    {
        if (!PreyIsLive() || !PreyIsInsect())
            return false;
        EnsureSelfFaction();
        if (prey != null
            && (preyFaction == null || preyFaction.faction != FactionId.Insects))
            Insect.BindHuntRefs(prey, out preyInsect, out preyFaction, out preyLife);
        return FactionTable.BirdHuntsInsect(selfFaction, preyFaction);
    }

    private float PreyStrength()
    {
        return preyInsect != null ? preyInsect.ownStrength : preyStrength;
    }

    // Stamped marker: FactionTable. Empty marker: v1 Hostile stand-in.
    private static bool MarkerIsHostile(
        FactionMember self, Transform marker, FactionMember other)
    {
        if (marker == null)
            return false;
        if (self == null || other == null)
            return true;
        return FactionTable.IsHostile(self, other);
    }

    private void TickForage()
    {
        // ChooseLife should already have Pursued. Backup must run
        // before StayOnDirtAfterHunt: a Forage Walk arms preferDirtLand,
        // then the air-hold returned and never entered Pursue, so
        // BeakStrike never Hurts (Rel Hostile only while Pursue).
        if (TryEnterInsectHunt())
            return;

        // Bound Insect in noticeRange stays Walk while Forage. Rel
        // Hostile is TryEnterInsectHunt. wander.Step Fly here used to
        // cruise past Rel (huntFlyFraction) while Hurt waited Pursue.
        float forageX = bodyRb != null ? bodyRb.position.x : transform.position.x;
        if (BoundInsectInNotice(forageX))
        {
            ArmForageHuntWalk();
            ClearCrowPose();
            return;
        }

        EnsureWanderClock();

        if (Time.time < postHuntSitUntil)
        {
            if (HoldDirtAfterHunt())
                return;
            if (StayOnDirtAfterHunt())
                return;
            bool onDirtSit = sensors != null && sensors.bothGrounded;
            if (onDirtSit)
                WritePreyMode(BirdMode.Sit, 0f);
            else
            {
                if (control != null)
                    control.preferDirtLand = true;
                WritePreyMode(BirdMode.Walk, 0f);
            }
            return;
        }

        if (HoldDirtAfterHunt())
            return;
        if (StayOnDirtAfterHunt())
            return;

        if (forageStyle == BirdForageStyle.ChickenGround)
        {
            TickChicken();
            return;
        }

        if (forageStyle == BirdForageStyle.WoodpeckerDrum)
        {
            TickWoodpecker();
            return;
        }

        if (kind == Kind.Wander)
        {
            TickWander();
            return;
        }

        // Prey drive has no wander clock. Leaving mode untouched in the
        // air re-armed leftover Fly after the eat (huntFlyFraction).
        bool onDirt = sensors != null && sensors.bothGrounded;
        if (onDirt)
        {
            WritePreyMode(BirdMode.Sit, 0f);
            if (control != null)
                control.preferDirtLand = false;
        }
        else
        {
            if (control != null)
                control.preferDirtLand = true;
            WritePreyMode(BirdMode.Walk, 0f);
        }
    }

    // After the eat, ChooseLife is Forage before TickPrey. Clearing
    // preferDirtLand in the air re-armed leftover hoverMean Fly.
    // Wander used to skip this and write Fly the same frame as the eat.
    // true = held this frame (caller must not wander.Step).
    private bool HoldDirtAfterHunt()
    {
        if (control == null)
            return false;
        // Live Hostile Insect mid-hunt still needs soil. A Forage flicker
        // that cleared preferDirtLand here while mode was Peck parked
        // HuntBeak one Update before BeakStrike Hurt (Rel Hostile missed).
        // Body IsHuntSoil covers the lag; brain must keep preferDirtLand
        // and not fall through to wander Fly. Far Forage (Sit leftover)
        // still wanders — BoundInsectInNotice already armed Walk closer.
        if (PreyIsLive() && PreyIsInsect())
        {
            bool midHunt = lastPreyMode == BirdMode.Walk || lastPreyMode == BirdMode.Peck
                || control.mode == BirdMode.Walk || control.mode == BirdMode.Peck;
            if (midHunt)
            {
                ArmDirtInsectHunt();
                return true;
            }
            return false;
        }
        bool landHunt = lastPreyMode == BirdMode.Walk || lastPreyMode == BirdMode.Peck
            || control.mode == BirdMode.Walk || control.mode == BirdMode.Peck;
        if (!landHunt)
            return false;
        bool onDirt = sensors != null && sensors.bothGrounded;
        if (onDirt)
        {
            // Write Sit before clearing soil — preferDirtLand false with
            // leftover Peck desynced HuntPreferDirt from IsHuntSoil.
            WritePreyMode(BirdMode.Sit, 0f);
            control.preferDirtLand = false;
            return false;
        }
        control.preferDirtLand = true;
        WritePreyMode(BirdMode.Walk, 0f);
        return true;
    }

    private bool StayOnDirtAfterHunt()
    {
        if (control == null || !control.IsHuntSoil())
            return false;
        if (sensors != null && sensors.bothGrounded)
            return false;
        WritePreyMode(BirdMode.Walk, 0f);
        return true;
    }

    private void WriteSleepBody()
    {
        if (control == null)
            return;
        control.preferDirtLand = false;
        control.mode = BirdMode.Sit;
    }

    // Plant handshake: one slot. Writes homeX / homeY and seats the body RB.
    public void BindPerch(Vector2 slot)
    {
        homeX = slot.x;
        homeY = slot.y;
        hasPerch = true;
        if (bodyKind != null && bodyKind.WantsTreeSeat)
        {
            float face = control != null ? control.FacingSign() : 1f;
            bodyKind.PlaceOnSlot(slot, face);
        }
    }

    private void TickWander()
    {
        EnsureWanderClock();
        if (wander == null) return;

        Vector2 pos = bodyRb != null ? bodyRb.position : (Vector2)transform.position;
        bool onPerch = hasPerch && sensors.treeSupport;
        float leashNow = hasPerch && orbitRadius > 0.01f ? orbitRadius : leash;
        float padY = hasPerch ? homeY : pos.y;
        wander.Step(
            sensors.bothGrounded, pos.x, homeX, Time.time,
            sitMin, sitMax, walkMin, walkMax, flyMin, flyMax, glideLead,
            flyTurnMin, flyTurnMax, leashNow,
            hasPerch, onPerch, homeFaceDeadzone,
            pos.y, padY, cruiseClearance, weaveGlide,
            turnMin, turnMax,
            out BirdMode mode, out float face);
        FinishSoloGlide(wander, pos, ref mode);
        if (HoldLandSit(wander, Time.time))
            mode = BirdMode.Sit;
        ApplyCrowSocial(wander, pos, ref mode, ref face);
        control.preferDirtLand = wander.DirtBout;
        // Dirt forage Peck is Forage. A bound Insect must not Peck
        // until ChooseWake Pursue (huntOnsetLog Forage/Peck skip).
        mode = ClampInsectHuntMode(
            mode, Life == LifeState.Pursue, PreyIsLive() && PreyIsInsect());
        if (PreyIsLive() && PreyIsInsect())
        {
            control.preferDirtLand = true;
            lastPreyMode = mode;
        }
        control.mode = mode;
        if (control.FacingSign() != face)
            control.SetFacing(face);
        WriteCrowPose(wander);
    }

    // Play Bird is Kind.Wander. AddComponent Start used to run as default
    // Prey (kind not yet assigned) and skip this clock, so TickForage after
    // the eat returned without a mode write. Create once, not per Update.
    private void EnsureWanderClock()
    {
        if (wander != null)
            return;
        if (kind != Kind.Wander)
            return;
        if (forageStyle == BirdForageStyle.ChickenGround
            || forageStyle == BirdForageStyle.WoodpeckerDrum)
            return;
        wander = new BirdWander();
        float face = control != null ? control.FacingSign() : 1f;
        wander.Reset(wanderSeed, Time.time, sitMin, sitMax, face);
        BirdSocial.AddCrow(this);
    }

    // Solo wander only. Flock lands via ForceGlide + HoldLandSit.
    // TickFlock Sit on dirt needs bothGrounded. Crow Glide in the 1.8 m
    // flare holds lift ≈ weight, so the feet never probe and landCount
    // stays 0. Sit cuts the flap; the body drops the last metre.
    private void FinishSoloGlide(BirdWander wander, Vector2 pos, ref BirdMode mode)
    {
        if (hasPerch || wander == null)
        {
            soloGlideAt = -1f;
            return;
        }

        if (mode == BirdMode.Fly)
        {
            soloGlideAt = -1f;
            return;
        }

        if (mode != BirdMode.Glide)
            return;

        if (soloGlideAt < 0f)
            soloGlideAt = Time.time;

        float height = pos.y - Bird.StanceRootY(bodyKind);
        bool inFlare = height < 2.0f;
        bool waited = Time.time >= soloGlideAt + 1.6f;
        if (!inFlare && !waited)
            return;

        wander.ForceSit(Time.time, sitMin, sitMax, turnMin, turnMax);
        mode = BirdMode.Sit;
        soloGlideAt = -1f;
    }

    // Shared with BirdFlockDrive. FindAny — no sort. Not a per-frame walk.
    // Miss: wait 5 s. Headless hunt has no player; 0.5 s Find was a poll.
    internal static Transform CachedPlayer(float now)
    {
        if (sharedPlayer != null)
            return sharedPlayer;
        if (now < sharedPlayerScanAt)
            return null;
        PlayerInputSource src = Object.FindAnyObjectByType<PlayerInputSource>();
        if (src != null)
        {
            sharedPlayer = src.transform;
            return sharedPlayer;
        }

        sharedPlayerScanAt = now + 5f;
        return null;
    }

    // B-HERO-2 (-birdHeroBody): the keep-away x must be the hero's BODY - the root
    // never leaves the spawn (autoSyncTransforms off). Pelvis first, root fallback;
    // bound once, one [B-HERO] bind line.
    internal static string PlayerBodyName = "";
    private static Transform sharedPlayerBodyT;
    private static Rigidbody2D sharedPlayerBodyRb;
    private static bool sharedPlayerBodyLogged;

    internal static bool HeroBodyX(float now, out float x)
    {
        x = 0f;
        if (sharedPlayerBodyT == null)
        {
            Transform root = CachedPlayer(now);
            if (root == null)
                return false;
            Transform body = root.Find("Pelvis");
            sharedPlayerBodyT = body != null ? body : root;
            sharedPlayerBodyRb = sharedPlayerBodyT.GetComponent<Rigidbody2D>();
            PlayerBodyName = sharedPlayerBodyT.name;
            if (!sharedPlayerBodyLogged)
            {
                sharedPlayerBodyLogged = true;
                Debug.Log("[B-HERO] bind body=" + PlayerBodyName);
            }
        }
        x = sharedPlayerBodyRb != null ? sharedPlayerBodyRb.position.x : sharedPlayerBodyT.position.x;
        return true;
    }

    // B-HERO-2 r2: same bind, full position - the mob-lane standoff is 2-D.
    internal static bool HeroBodyPos(float now, out Vector2 p)
    {
        p = Vector2.zero;
        float x;
        if (!HeroBodyX(now, out x))
            return false;
        p = sharedPlayerBodyRb != null
            ? sharedPlayerBodyRb.position
            : (Vector2)sharedPlayerBodyT.position;
        return true;
    }

    private void ApplyKeepAway(BirdWander wander, Vector2 pos, ref BirdMode mode, ref float face)
    {
        if (keepAwayRange < 0.05f || wander == null)
        {
            keepAway = false;
            return;
        }

        player = CachedPlayer(Time.time);
        if (player == null)
        {
            keepAway = false;
            return;
        }

        float px = player.position.x;
        if (Bird.HeroBodyKeepAway)
        {
            float bx;
            if (HeroBodyX(Time.time, out bx))
                px = bx;
        }
        float dist = Mathf.Abs(pos.x - px);
        float leave = keepAwayRange * Mathf.Max(1.05f, keepAwayRelease);
        if (keepAway)
        {
            if (dist > leave)
                keepAway = false;
        }
        else if (dist < keepAwayRange || (Bird.HeroBodyKeepAway && dist < Bird.HeroStandoff))
            keepAway = true;

        if (!keepAway)
            return;

        // Takeoff from dirt/pad. Do not ForceFly every airborne frame:
        // that cleared dirtBout and forbade Glide-to-dirt while the
        // player stood under the apple (orbit 4.6 m < keep-away leave).
        bool grounded = sensors != null && sensors.bothGrounded;
        bool onDirt = mode == BirdMode.Sit
            || mode == BirdMode.Walk
            || mode == BirdMode.Peck;
        if (grounded || onDirt)
        {
            wander.ForceFly(Time.time, flyMin, flyMax, flyTurnMin, flyTurnMax);
            mode = BirdMode.Fly;
        }
        FaceAway(pos.x, px, ref face);
    }

    // B-HERO-2 r2: the minimum standoff in the per-bird mob lanes. Inside HeroAvoid
    // (2-D) of the hero BODY the crow steers away from him instead of diving and
    // holds the steer HeroOutHold s, so it never crowds or lands on the hero.
    private bool HeroOutSteer(Vector2 pos, float now, out float face)
    {
        face = 1f;
        if (!Bird.HeroBodyKeepAway)
            return false;
        Vector2 hp;
        if (!HeroBodyPos(now, out hp))
            return false;
        float ox = pos.x - hp.x;
        float oy = pos.y - hp.y;
        if (ox * ox + oy * oy < Bird.HeroAvoid * Bird.HeroAvoid && heroOutUntil <= now)
            heroOutUntil = now + Bird.HeroOutHold;
        if (heroOutUntil <= now)
            return false;
        face = pos.x >= hp.x ? 1f : -1f;
        return true;
    }

    private void ApplyCrowSocial(BirdWander wander, Vector2 pos, ref BirdMode mode, ref float face)
    {
        // Bound Insect is food. Keep-away / pack mob / drum ForceFly
        // here is leftover cruise while Forage should glance then Pursue.
        // Flock ApplyCrowOverlay already skips the whole overlay.
        if (PreyIsLive() && PreyIsInsect())
            return;

        ApplyKeepAway(wander, pos, ref mode, ref face);
        if (keepAway || wander == null)
            return;

        float now = Time.time;
        bool pack = BirdSocial.CrowDriveCount >= 2;
        if (threat != null && pack && mobNotice > 0.05f
            && Mathf.Abs(pos.x - threat.position.x) < mobNotice)
            PulseMob(threat.position.x, now);

        float outFace;
        if (pack && HeroOutSteer(pos, now, out outFace))
        {
            wander.ForceFly(now, flyMin, flyMax, flyTurnMin, flyTurnMax);
            face = outFace;
            mode = BirdMode.Fly;
            return;
        }

        float mobX;
        if (pack && BirdSocial.Mobbing(now, out mobX))
        {
            wander.ForceFly(now, flyMin, flyMax, flyTurnMin, flyTurnMax);
            float dx = mobX - pos.x;
            if (Mathf.Abs(dx) > 0.12f)
                face = dx > 0f ? 1f : -1f;
            bool air = sensors == null || !sensors.bothGrounded;
            mode = air && Mathf.Abs(dx) < mobStoop ? BirdMode.Attack : BirdMode.Fly;
            return;
        }

        float drumX;
        if (drumStartleRange > 0.05f
            && now >= drumIgnoreUntil
            && BirdSocial.Drumming(now, out drumX)
            && Mathf.Abs(pos.x - drumX) < drumStartleRange)
        {
            BirdMode prior = mode;
            wander.ForceFly(now, flyMin, flyMax, flyTurnMin, flyTurnMax);
            mode = BirdMode.Fly;
            FaceAway(pos.x, drumX, ref face);
            if (prior == BirdMode.Sit || prior == BirdMode.Peck || prior == BirdMode.Walk)
                DrumStartleCount++;
            if (drumStartleCool > 0.05f)
                drumIgnoreUntil = now + drumStartleCool;
        }
    }

    private void PulseMob(float x, float now)
    {
        if (!BirdSocial.Mobbing(now, out _))
            MobCount++;
        BirdSocial.PulseMob(x, now, mobHold);
    }

    private static void FaceAway(float selfX, float otherX, ref float face)
    {
        float dx = selfX - otherX;
        if (Mathf.Abs(dx) <= 0.12f)
            return;
        face = dx > 0f ? 1f : -1f;
    }

    private void WriteCrowPose(BirdWander wander)
    {
        if (control == null)
            return;
        if (wander == null)
        {
            ClearCrowPose();
            return;
        }

        control.idleLook = wander.IdleLook;
        control.idlePreen = wander.IdlePreen;
        control.idleHop = wander.IdleHop;
        control.playRoll = wander.PlayRoll;
        control.searchCrouch = wander.SearchCrouch;
        control.peckWindup = wander.PeckWindup;
        control.peckStrike = wander.PeckStrike;
        control.peckHeadUp = wander.PeckHeadUp;
        control.peckMiss = wander.PeckMiss;
    }

    private void ClearCrowPose()
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

    private void TickChicken()
    {
        if (chicken == null) return;

        Vector2 pos = bodyRb != null ? bodyRb.position : (Vector2)transform.position;
        float henHome = homeX;
        float henLeash = leash;
        BirdDrive leader = BirdSocial.HenCount >= 2 ? BirdSocial.HenAt(0) : null;
        if (leader != null && leader != this && henFollowSlack > 0.05f)
        {
            henHome = leader.SocialX;
            henLeash = henFollowSlack;
        }

        chicken.Step(
            pos.x, henHome, Time.time,
            1.2f, 3.4f, walkMin, walkMax,
            turnMin, turnMax, henLeash,
            out BirdMode mode, out float face);
        ApplyHenSocial(leader, pos, ref mode, ref face);
        // Neighbour pebble Peck is Forage. Bound Insects wait Pursue.
        mode = ClampInsectHuntMode(
            mode, Life == LifeState.Pursue, PreyIsLive() && PreyIsInsect());
        if (PreyIsLive() && PreyIsInsect())
        {
            control.preferDirtLand = true;
            lastPreyMode = mode;
        }
        control.mode = mode;
        if (control.FacingSign() != face)
            control.SetFacing(face);
        ClearCrowPose();
    }

    private void TickWoodpecker()
    {
        if (pecker == null) return;
        if (control != null)
            control.preferDirtLand = false;

        Vector2 pos = bodyRb != null ? bodyRb.position : (Vector2)transform.position;
        bool onPerch = hasPerch && sensors.treeSupport;
        float leashNow = hasPerch && orbitRadius > 0.01f ? orbitRadius : leash;
        pecker.Step(
            sensors.bothGrounded, pos.x, homeX, Time.time,
            14f, 28f, 0.7f, 1.6f, 0.35f,
            turnMin, turnMax, leashNow,
            hasPerch, onPerch,
            out BirdMode mode, out float face);
        bool drumNow = pecker.Drumming && sensors != null && sensors.treeSupport;
        if (drumNow && !wasDrumming)
            BirdSocial.PulseDrum(pos.x, Time.time, Woodpecker.DrumPulseHold);
        wasDrumming = drumNow;
        mode = ClampInsectHuntMode(
            mode, Life == LifeState.Pursue, PreyIsLive() && PreyIsInsect());
        if (PreyIsLive() && PreyIsInsect())
        {
            control.preferDirtLand = true;
            lastPreyMode = mode;
        }
        control.mode = mode;
        if (control.FacingSign() != face)
            control.SetFacing(face);
        ClearCrowPose();
    }

    private void TickPrey()
    {
        // Card: Forage + see Insects → Pursue → dirt Peck. The unmarked
        // stoop Fly/Attack (hoverMean) is not an eat — HuntBeak never Rel
        // Hostile, huntFlyFraction > 0. Cache miss still Walk/Peck.
        if (prey != null && !PreyIsInsect())
            Insect.BindHuntRefs(prey, out preyInsect, out preyFaction, out preyLife);
        if (forageStyle == BirdForageStyle.ChickenGround)
            TickChickenPrey();
        else
            TickInsectPrey();
    }

    // Bound slot only. Flock Look still carries a disabled Perch box;
    // treeSupport on that AABB is not a twig and must not write leftover Fly.
    // IsHuntSoil is the dirt hunt (preferDirtLand or dirt Peck): the pad is
    // already dropped, so a leftover NearPerch AABB must not block Peck.
    private bool HuntMustLeavePerch()
    {
        // Body IsHuntSoil — not preferDirtLand alone. HoldDirtAfterHunt
        // Forage flicker cleared the flag while dirt Peck still needed
        // OnDirtPlate true for HuntBeak Rel Hostile.
        if (control != null && control.IsHuntSoil())
            return false;
        // Dirt-height close is soil even if hasPerch / treeSupport still
        // lag after ArmDirtInsectHunt (body ReleasePerch one step late).
        float height = bodyRb != null
            ? bodyRb.position.y - Bird.StanceRootY(bodyKind)
            : 0f;
        if (height < Insect.DirtHuntHeight)
            return false;
        // Bound Hostile Insect is dirt food. Leftover NearPerch AABB
        // (treeSupport without a slot) is soil plus twigs — not a twig
        // sit. Treating it as perch skipped the first Pursue Peck.
        if (Life == LifeState.Pursue && PreyIsLive() && PreyIsInsect())
            return false;
        return hasPerch && sensors != null && sensors.treeSupport;
    }

    private bool InsectHuntOnDirt()
    {
        bool onPerch = HuntMustLeavePerch();
        bool grounded = sensors != null && sensors.bothGrounded;
        float height = bodyRb != null
            ? bodyRb.position.y - Bird.StanceRootY(bodyKind)
            : 0f;
        return Insect.OnDirtPlate(grounded, onPerch, height);
    }

    // Insect on dirt: Walk then Peck. Not a stoop Attack. Eat is BeakStrike.
    private void TickInsectPrey()
    {
        if (control == null)
            return;

        if (!PreyIsLive())
        {
            // Sit before clearing soil — preferDirtLand false with leftover
            // Peck desynced HuntPreferDirt from body IsHuntSoil.
            if (InsectHuntOnDirt())
            {
                WritePreyMode(BirdMode.Sit, 0f);
                control.preferDirtLand = false;
            }
            else
            {
                control.preferDirtLand = true;
                WritePreyMode(BirdMode.Walk, 0f);
            }
            return;
        }

        bool grounded = sensors != null && sensors.bothGrounded;
        if (!grounded)
            wasAirborne = true;
        else if (wasAirborne)
            wasAirborne = false;

        // Soil first: InsectHuntOnDirt reads preferDirtLand. HuntBeak
        // (body) is a Dynamic solid only while Peck — leftover hover
        // / NearPerch kept OnDirtPlate false, first Peck never armed,
        // Rel Hostile never Hurt. Walk already asked for the dirt film.
        // Drop hasPerch: body ReleasePerch alone left homeX on the twig.
        // OnDirtPlate now treats dirt-height as soil even if a leftover
        // pad AABB still reports perched (preferDirtLand flicker).
        ArmDirtInsectHunt();
        HoldWanderOffHunt();

        float selfX = bodyRb != null ? bodyRb.position.x : transform.position.x;
        float dx = prey.position.x - selfX;
        float dist = Mathf.Abs(dx);
        bool pecking = Insect.InPeckWindow(dist, lastPreyMode == BirdMode.Peck);
        // TickPrey is Pursue only. Rel Hostile is the eat gate — Peck
        // without BirdHuntsInsect armed HuntBeak while Hurt stayed
        // Neutral. Dirt is preferDirtLand; close is InPeckWindow.
        bool eat = pecking && QuarryIsInsectHunt();
        // Walk still faces the quarry (walkThrust uses facing). HuntBeak
        // is body-X ±PeckHold, but a close SetFacing still reverses vx
        // and walks the slab off Rel Hostile. Hold the nose in the strip
        // and on the first eat frame (last mode is still Walk).
        float wantFace = dx > 0f ? 1f : -1f;
        if (Insect.WantCloseFace(
            dx, lastPreyMode == BirdMode.Peck || eat, control.FacingSign()))
            control.SetFacing(wantFace);

        if (eat)
            WritePreyMode(BirdMode.Peck, dx);
        else
            WritePreyMode(BirdMode.Walk, dx);
    }

    // Hen Pursue is Walk / Peck on dirt. Never Fly, Glide, or a stoop Attack.
    private void TickChickenPrey()
    {
        if (control == null)
            return;

        if (!PreyIsLive())
        {
            if (sensors != null && sensors.bothGrounded)
            {
                WritePreyMode(BirdMode.Sit, 0f);
                control.preferDirtLand = false;
            }
            else
            {
                control.preferDirtLand = true;
                WritePreyMode(BirdMode.Walk, 0f);
            }
            return;
        }

        ArmDirtInsectHunt();
        HoldWanderOffHunt();

        float selfX = bodyRb != null ? bodyRb.position.x : transform.position.x;
        float dx = prey.position.x - selfX;
        float dist = Mathf.Abs(dx);
        bool pecking = Insect.InPeckWindow(dist, lastPreyMode == BirdMode.Peck);
        // TickChickenPrey is Pursue only. Rel Hostile is the mouth.
        bool eat = pecking && QuarryIsInsectHunt();
        // Same close-face as TickInsectPrey: hold the nose in PeckHold
        // and on the first eat frame, not only after lastPreyMode Peck.
        float wantFace = dx > 0f ? 1f : -1f;
        if (Insect.WantCloseFace(
            dx, lastPreyMode == BirdMode.Peck || eat, control.FacingSign()))
            control.SetFacing(wantFace);

        if (eat)
            WritePreyMode(BirdMode.Peck, dx);
        else
            WritePreyMode(BirdMode.Walk, dx);
    }

    // After TickFlock Sit, hold Sit for sitHold in Forage too — not only Pursue.
    private bool HoldLandSit(BirdWander wander, float now)
    {
        bool down = sensors != null
            && (sensors.bothGrounded || sensors.treeSupport);
        if (!down)
        {
            wasAirborne = true;
            return false;
        }

        if (wasAirborne)
        {
            wasAirborne = false;
            // Pad Sit must outlast TickFlock's one-step Sit. Dirt is a plant.
            float hold = hasPerch ? sitHold : 0.85f;
            restUntil = now + hold;
            if (wander != null)
                wander.ForceSit(now, sitMin, sitMax, turnMin, turnMax);
        }

        return now < restUntil;
    }

    private void FacePrey(float dx)
    {
        // Insect HuntBeak is ±PeckHold. A leave-Sit glance inside the
        // strip reversed walkThrust and walked Rel Hostile off the bug.
        float dead = PreyIsInsect() ? Insect.PeckHold : 0.12f;
        if (Mathf.Abs(dx) <= dead)
            return;
        control.SetFacing(dx > 0f ? 1f : -1f);
    }

    // Card: Forage + see Hostile Insects → Pursue → dirt Peck.
    // Wander / hen Step write mode directly; leftover Fly/Peck there
    // skipped WritePreyMode and filled huntOnsetLog Forage/Peck.
    // Sit/Stand is leftover perch or takeoffDelay — HuntBeak lives
    // only on Peck, so a sit never Rel Hostile.
    internal static BirdMode ClampInsectHuntMode(
        BirdMode next, bool pursuing, bool insectLive)
    {
        if (!insectLive)
            return next;
        if (next == BirdMode.Fly || next == BirdMode.Glide || next == BirdMode.Attack
            || next == BirdMode.Sit || next == BirdMode.Stand)
            return BirdMode.Walk;
        if (next == BirdMode.Peck && !pursuing)
            return BirdMode.Walk;
        return next;
    }

    private void WritePreyMode(BirdMode next, float dx)
    {
        bool leaveSit = lastPreyMode == BirdMode.Sit && next != BirdMode.Sit;
        bool enterFly = next == BirdMode.Fly && lastPreyMode != BirdMode.Fly;
        bool enterAttack = next == BirdMode.Attack && lastPreyMode != BirdMode.Attack;
        if (leaveSit || enterFly || enterAttack)
            FacePrey(dx);
        next = ClampInsectHuntMode(
            next, Life == LifeState.Pursue, PreyIsLive() && PreyIsInsect());
        if (next == BirdMode.Walk && lastPreyMode != BirdMode.Walk)
            huntWalkCount++;
        if (next == BirdMode.Peck && lastPreyMode != BirdMode.Peck)
            huntPeckCount++;
        control.mode = next;
        lastPreyMode = next;
    }

    // Flee body: cry if the stronger hostile is ahead, face away, Fly (hen: Walk).
    private void TickFlee()
    {
        if (control == null)
            return;
        control.preferDirtLand = false;

        float selfX = bodyRb != null ? bodyRb.position.x : transform.position.x;
        Vector2 pos = bodyRb != null ? bodyRb.position : (Vector2)transform.position;
        float dx = 0f;
        bool saw = threat != null && OrganismAlert.SeesAhead(
            selfX, control.FacingSign(), threat.position.x, threatRange, 0.12f, out dx);
        bool heard = voice != null && OrganismAlert.HearsForeignAlarm(
            voice.SourceId, pos, Time.time, 0.6f, heardBuf);

        if (saw)
        {
            bool pack = forageStyle == BirdForageStyle.CrowCruise
                && BirdSocial.CrowDriveCount >= 2;
            if (pack)
            {
                PulseMob(threat.position.x, Time.time);
                control.SetFacing(dx > 0f ? 1f : -1f);
            }
            else
            {
                if (voice != null)
                    voice.Cry(VoiceKind.Alarm);
                control.SetFacing(dx > 0f ? -1f : 1f);
            }

            alertUntil = Time.time + alertHold;
        }
        else if (heard)
            alertUntil = Time.time + alertHold;

        float outFace;
        if (forageStyle == BirdForageStyle.CrowCruise
            && HeroOutSteer(pos, Time.time, out outFace))
        {
            control.SetFacing(outFace);
            control.mode = BirdMode.Fly;
            return;
        }

        float mobX;
        if (forageStyle == BirdForageStyle.CrowCruise
            && BirdSocial.CrowDriveCount >= 2
            && BirdSocial.Mobbing(Time.time, out mobX))
        {
            float mdx = mobX - selfX;
            if (Mathf.Abs(mdx) > 0.12f)
                control.SetFacing(mdx > 0f ? 1f : -1f);
            bool air = sensors == null || !sensors.bothGrounded;
            control.mode = air && Mathf.Abs(mdx) < mobStoop
                ? BirdMode.Attack
                : BirdMode.Fly;
            return;
        }

        control.mode = bodyKind != null ? bodyKind.FleeBodyMode : BirdMode.Fly;
    }

    private void ApplyHenSocial(BirdDrive leader, Vector2 pos, ref BirdMode mode, ref float face)
    {
        if (BirdSocial.HenCount < 2)
            return;

        if (leader != null && leader != this && henFollowSlack > 0.05f)
        {
            float lx = leader.SocialX;
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
        // Bound Insect is food. A neighbour pebble Peck here is Forage
        // and would fill huntOnsetLog Forage/Peck before ChooseWake.
        if (PreyIsLive() && PreyIsInsect())
            return;

        for (int i = 0; i < BirdSocial.HenCount; i++)
        {
            BirdDrive other = BirdSocial.HenAt(i);
            if (other == null || other == this)
                continue;
            if (other.SocialMode != BirdMode.Peck)
                continue;
            // Dirt forage copies a pebble Peck. A Pursue eat is Insects
            // food — Rel Hostile on a Forage hen would skip Forage→Pursue.
            if (other.Life == LifeState.Pursue)
                continue;
            if (Mathf.Abs(pos.x - other.SocialX) > henPeckTogether)
                continue;
            mode = BirdMode.Peck;
            return;
        }
    }
}
