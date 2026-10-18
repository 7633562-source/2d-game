using UnityEngine;

// Circumstances, not force. In Update writes Walk / EndWalk /
// LeapAttack / Bark / SetFacing / idle floats. Life is the shared tree.
// Not a keyboard and not PD. Default forage: follow the player at a sit
// distance (CoM / Torso, not spawn-stuck roots), or idle
// (sniff/scratch/yawn/look, tail wag) when sitting.
// Forage trot restores idleWag after Walk() so the tail reads as a
// hunt-vs-search flag; DriveWalk clocks that wag on walkAnimPhase so
// the gait is not a sit metronome on a bobbing trunk, and nods the
// neck. No-player wander also restores idleSniff so the neck tracks
// dirt. Pursue / Sleep / follow leave sniff 0.
// EndWalk after a Forage trot starts a short pant (idlePant) so sit
// after search is not the same pose as a cold idle. No player (scene
// Dog): walk a short leash from spawn, then sit and pant. Player gone
// mid-follow: EndWalk and sit — do not keep the follow trot into wander.
// Sit-arrival or the player walking into sitRange: Bark once and look
// up at the human. Night is Sleep. A live bird in noticeRange, or a
// DogPrey mark as fallback, still Pursue (notice / stoop / SetFacing
// from CoM, not the spawn root). Bird aim is the Body child: the
// Bird root stays at spawn like the dog. Lock is one Bark plus a
// short stand-stare (idleLook), then a stalk Walk (idleHunt: nose
// down, stiff tail, ears stay pinned) or Leap. After a leap that
// did not finish the mark, stand-snarl (huntRecover) so the next
// pounce is not an instant re-coil.
// huntRecover 0 = the old machine-gun leap. Hunt stalk / leap snarl
// is picture + DriveJaw on those flags, not a new idleSnarl.
// huntStalk 0 = the old still chase. A dead mark is eaten (gone):
// sit-chew (nose down, jaw snaps, no tongue), then Forage.
// eatChew 0 = the old post-leap pant.
[DefaultExecutionOrder(-50)]
public partial class DogDrive : MonoBehaviour
{
    [Header("Policy")]
    [Tooltip("Stand turns this on explicitly. Play is not headless.")]
    public bool allowHeadless;

    [Header("Prey")]
    public Transform prey;
    [Tooltip("See the mark and chase, m.")]
    public float noticeRange = 8f;
    [Tooltip("Leap when closer than this, m.")]
    public float stoopRange = 0.55f;
    [Tooltip("Stand-stare after the lock Bark, s. 0 = bark then chase.")]
    public float huntAlert = 0.55f;
    [Tooltip("Neck overlay during the lock. Mark sits near trunk height, so this is perk, not aim.")]
    public float huntAlertLook = 0.55f;
    [Tooltip("Pursue Walk overlay 0–1: nose down + stiff raised tail. 0 = still hold as CLI walk.")]
    public float huntStalk = 0.85f;
    [Tooltip("Stand-snarl after a leap before the next pounce, s. 0 = leap again at once.")]
    public float huntRecover = 0.70f;
    [Tooltip("Sit-chew after a kill, s. 0 = pant as after a wander.")]
    public float eatChew = 2.8f;

    [Header("Follow player")]
    [Tooltip("Start the trot when |dx| exceeds this, m.")]
    public float followRange = 2.2f;
    [Tooltip("Sit when |dx| is inside this, m. Keep below followRange.")]
    public float sitRange = 1.2f;
    [Tooltip("Do not flip facing while |dx| is this small, m.")]
    public float faceDeadzone = 0.15f;
    [Tooltip("Seconds between greeting barks.")]
    public float greetCooldown = 4f;
    [Tooltip("Hold the look-up after Bark, s.")]
    public float greetHold = 1.1f;

    [Header("Wander (no player)")]
    [Tooltip("Forage leash from spawn, m. Scene Dog has no player.")]
    public float wanderRadius = 4f;
    [Tooltip("One trot this far, m. Diagonal target is runSpeedRef 1.4 m/s (5 km/h); leftover sit-trot was 0.08 m/s.")]
    public float wanderStep = 1.0f;
    [Tooltip("Give up a trot after this, s.")]
    public float wanderMaxSeconds = 8f;
    [Tooltip("Arrive when |dx| is inside this, m.")]
    public float wanderArrive = 0.35f;

    [Header("Life")]
    public float ownStrength = 2f;
    public float preyStrength = 0.5f;
    [Tooltip("Enter Sleep at or above this Night01.")]
    public float sleepNightEnter = 0.58f;
    [Tooltip("Leave Sleep below this Night01. Keep under sleepNightEnter.")]
    public float sleepNightExit = 0.38f;

    public LifeState Life { get; private set; } = LifeState.Forage;

    private DogStanceController control;
    private Dog body;
    private float nextPreyScan;
    // One PlayerInputSource in the scene. Sticky after hit; miss retries at 0.5 s.
    // Human / dog roots stay at spawn; follow reads CoM / Torso, not those roots.
    private static Transform sharedPlayer;
    private static Transform sharedPlayerBody;
    private static CenterOfMassCalculator sharedPlayerCom;
    private static float sharedPlayerScanAt;
    // One scene walk for every dog. FindObjectsByType walks all
    // MonoBehaviours (yard ambient included), so three dogs must not
    // each allocate a Bird[] on the same 0.5 s tick.
    private static Bird[] sharedBirds;
    private static float sharedBirdScanAt;
    private static DogPrey sharedDogPrey;
    private static bool sharedDogPreyTried;
    private static float sharedDogPreyScanAt;
    private System.Random rng;
    private float nextIdle;
    private float idleUntil;
    private int idleKind;
    private float idleStarted;
    private bool nightSleep;
    private bool playerWasFar = true;
    private bool followingPlayer;
    private float nextGreet;
    private float greetUntil;
    private float attendLook;
    private float wanderHomeX;
    private float wanderTargetX;
    private float wanderGiveUp;
    private bool wanderGoing;
    private Damageable preyLife;
    private bool preyIsBird;
    private bool wasHunting;
    private float huntAlertUntil;
    private bool huntLeaping;
    private float huntRecoverUntil;
    private Transform deadLeapMark;
    private bool pendingEat;

    void Awake()
    {
        control = GetComponent<DogStanceController>();
        body = GetComponent<Dog>();
        rng = new System.Random(unchecked(GetHashCode() * 397));
        nextIdle = Time.time + Rand(1.2f, 2.8f);
        wanderHomeX = transform.position.x;
        if (prey != null)
            BindPrey(prey);
    }

    void Update()
    {
        if (HeadlessTrial.Active && !allowHeadless)
            return;
        if (control == null)
            control = GetComponent<DogStanceController>();
        if (body == null)
            body = GetComponent<Dog>();
        if (control == null || control.energyRest)
        {
            wanderGoing = false;
            followingPlayer = false;
            wasHunting = false;
            huntAlertUntil = 0f;
            huntLeaping = false;
            huntRecoverUntil = 0f;
            pendingEat = false;
            if (control != null)
            {
                control.EndWalk();
                control.ClearIdle();
            }
            return;
        }

        ResolvePrey();
        // Roots stay at spawn (same leftover as follow). A live bird
        // binds the Body child; DogPrey is the mark itself.
        float dist = PreyIsLive()
            ? Vector2.Distance(SelfWorldPos(), prey.position)
            : float.MaxValue;
        // A bird lock outlasts noticeRange so Flee+Fly is still a chase.
        // DogPrey stays the in-range mark.
        bool sawPrey = PreyIsLive()
            && (preyIsBird || dist <= noticeRange);
        // LEVEL 1 DOG (req-231): the level's own choice layer - a crow is chased
        // only when the seeded roll says so, never as a reflex. Off by default:
        // every other take keeps its bytes.
        if (level1Brain)
            sawPrey = Level1ChaseGate(Time.time, dist, sawPrey);
        bool weaker = sawPrey && !OrganismLife.HostileIsStronger(ownStrength, preyStrength);
        Life = OrganismLife.ChooseWake(false, weaker, false, false);

        if (Life == LifeState.Pursue)
        {
            nightSleep = false;
            wanderGoing = false;
            TickHunt(Time.time, dist);
            return;
        }

        // Mark died mid-leap: let DriveLeap finish. Do not Forage-Walk
        // on LeapAir (Walk() would no-op, but EndWalk / idle would fight).
        if (control.IsLeap())
            return;

        if (wasHunting)
        {
            wasHunting = false;
            huntAlertUntil = 0f;
            huntLeaping = false;
            huntRecoverUntil = 0f;
            control.EndWalk();
            if (pendingEat)
            {
                pendingEat = false;
                StartChew(Time.time);
            }
            else
                StartPant(Time.time);
        }

        if (Life != LifeState.Flee && WantNightSleep())
        {
            Life = LifeState.Sleep;
            wanderGoing = false;
            control.EndWalk();
            TickSleep();
            return;
        }

        // Hold the kill site. Follow / wander would Walk() and wipe chew.
        if (idleKind == 6)
        {
            if (control.walkWanted || control.IsWalk())
                control.EndWalk();
            TickIdle(Time.time, true);
            return;
        }

        Transform player = CachedPlayer(Time.time);
        if (player == null)
        {
            attendLook = 0f;
            playerWasFar = true;
            if (followingPlayer)
            {
                ReleaseFollow(Time.time);
                return;
            }

            TickWander(Time.time);
            return;
        }

        wanderGoing = false;
        followingPlayer = true;
        FollowPlayer(player);
        bool sitting = !control.walkWanted && !control.IsWalk();
        // The sniff stop owns the frame: TickIdle must not pick an act over the nose.
        if (level1Brain && l1SniffHold)
            return;
        TickIdle(Time.time, sitting);
    }

    // First notice: Bark once and hold a stand-stare so Forage→Pursue
    // is not an instant silent trot. Hunt never wags. Same Bark puff
    // as greet; idleLook perks the ears (sit path). Face and ranges
    // from CoM (FaceBodyToward), not the spawn root. Then stalk Walk
    // (idleHunt) or Leap. Prey health outlasts one bite, so a land
    // stand-snarl (huntRecover) sits between pounces. Walk() zeros
    // idle; restore hunt after it. CLI -dogLeap never enters here.
    private void TickHunt(float now, float dist)
    {
        if (control == null || prey == null)
            return;

        FaceBodyToward(prey.position.x);
        if (!wasHunting)
        {
            wasHunting = true;
            huntLeaping = false;
            huntRecoverUntil = 0f;
            control.Bark();
            float hold = Mathf.Max(0f, huntAlert);
            if (hold > 0f)
            {
                huntAlertUntil = now + hold;
                HoldHuntAlert();
                return;
            }
        }

        if (now < huntAlertUntil)
        {
            HoldHuntAlert();
            return;
        }

        huntAlertUntil = 0f;
        if (control.IsLeap())
        {
            huntLeaping = true;
            return;
        }

        if (huntLeaping)
        {
            huntLeaping = false;
            float recover = Mathf.Max(0f, huntRecover);
            if (recover > 0f)
            {
                huntRecoverUntil = now + recover;
                HoldHuntRecover();
                return;
            }
        }

        if (now < huntRecoverUntil)
        {
            HoldHuntRecover();
            return;
        }

        huntRecoverUntil = 0f;
        // LeapHuntTail raises the first hinge; LeapHuntDistalTail
        // perks Tail2–Tail5 Visual with that held 0.85 (amp 0 =
        // rest-spring stick; no distal muscle). LeapAttack zeros
        // idleHunt. Recover sit / stalk Walk perk those same
        // Visuals from idleHunt (SitHuntDistalTail /
        // WalkHuntDistalTail). Pursue picks this Walk (then Leap
        // in stoopRange). Forage uses ForageWalk. No play keys.
        if (dist <= stoopRange)
        {
            // The level's no-kill rule: stare at the mark, never leap / bite.
            if (level1Brain && level1NoKill)
                Level1HoldStare();
            else
                control.LeapAttack(prey);
        }
        else if (!control.standComHeightValid
                 && !control.IsFallen()
                 && !control.IsGettingUp())
            return;
        else
        {
            PickBoundGait();
            control.Walk();
            float stalk = Mathf.Clamp01(huntStalk);
            if (stalk > 0f)
                control.idleHunt = stalk;
        }
    }

    private void HoldHuntAlert()
    {
        control.EndWalk();
        control.ClearIdle();
        control.idleLook = huntAlertLook;
    }

    // Landed, mark still live. idleHunt keeps the stalk snarl / tail
    // raise without Walk() — a still coil, not a forage pant.
    private void HoldHuntRecover()
    {
        control.EndWalk();
        control.ClearIdle();
        float stalk = Mathf.Clamp01(huntStalk);
        if (stalk > 0f)
            control.idleHunt = stalk;
    }

    private void FollowPlayer(Transform player)
    {
        // A sniff stop holds the frame: stand, nose to the dirt, then trot on.
        if (level1Brain && Level1SniffTick(Time.time))
        {
            control.EndWalk();
            control.ClearIdle();
            control.idleSniff = 0.9f;
            return;
        }
        Vector2 self = SelfWorldPos();
        Vector2 other = PlayerWorldPos(player);
        float dx = other.x - self.x;
        float absDx = Mathf.Abs(dx);
        if (absDx > faceDeadzone)
            control.SetFacing(dx >= 0f ? 1f : -1f);

        // Human chest sits higher than the dog trunk. Neck overlay, not PD.
        float dy = other.y - self.y;
        attendLook = Mathf.Clamp(dy * 0.85f, -0.35f, 1f);

        float sit = Mathf.Max(0.05f, sitRange);
        float far = Mathf.Max(sit + 0.05f, followRange);
        bool walking = control.walkWanted || control.IsWalk();
        if (walking)
        {
            if (absDx <= sit)
            {
                control.EndWalk();
                playerWasFar = false;
                TryGreet(Time.time, other.x);
                StartPant(Time.time);
            }
            else
            {
                playerWasFar = true;
                ForageWalk();
            }
        }
        else if (absDx >= far)
        {
            playerWasFar = true;
            ForageWalk();
        }
        else
        {
            control.EndWalk();
            if (playerWasFar)
                TryGreet(Time.time, other.x);
            playerWasFar = false;
        }
        // The act the log names: trot after the hero, or sit near him.
        if (level1Brain)
            Level1Act(control.walkWanted || control.IsWalk() ? "follow" : "sit");
    }

    // One Call when the sit distance closes. Cooldown so hysteresis
    // does not yip every physics-frame of a walk stop.
    private void TryGreet(float now, float playerX)
    {
        if (control == null || now < nextGreet)
            return;
        nextGreet = now + Mathf.Max(0.8f, greetCooldown);
        greetUntil = now + Mathf.Max(0.2f, greetHold);
        if (idleKind != 0)
            ClearIdleAct();
        FaceBodyToward(playerX);
        control.Bark();
    }

    // Shared yard clock. No cycle (scene Dog, headless) keeps Night01 = 0.
    // Hysteresis so dusk does not flicker Forage / Sleep.
    private bool WantNightSleep()
    {
        float night = DayNightCycle.Night01;
        float enter = Mathf.Clamp01(sleepNightEnter);
        float exit = Mathf.Clamp(sleepNightExit, 0f, enter);
        if (nightSleep)
            nightSleep = night >= exit;
        else
            nightSleep = night >= enter;
        return nightSleep;
    }

    // Sit through the night. Not Walk: leftover sit dumps on a trot.
    // Neck / lids are idleSleep; wag stays off. SitSleepTail droops
    // the first hinge (amp 0 = rest). SitSleepRump hitches Pelvis
    // Visual with the sleep breath (amp 0 = statue rump).
    // SitSleepNose drops the snout (amp 0 = rest muzzle).
    // SitSleepEar hitches the Head-Visual fold with that same
    // breath (amp 0 = statue fold 0.88). SitSleepBrow hitches the
    // Head-Visual droop with that same breath (amp 0 = statue droop).
    // SitSleepMouth hitches the Head-Visual flews with that same
    // breath (amp 0 = statue droop). SitSleepWhisker hitches the
    // Head-Visual stamps with that same breath (amp 0 = statue
    // droop). SitSleepDistalTail limps Tail2–Tail5 Visual with
    // that same breath (amp 0 = rest-spring stick; no distal
    // muscle).
    private void TickSleep()
    {
        if (control == null || !control.CanIdle())
        {
            if (control != null)
                control.ClearIdle();
            return;
        }

        if (idleKind != 0)
            ClearIdleAct();
        greetUntil = 0f;
        attendLook = 0f;
        control.idleWag = 0f;
        control.idlePant = 0f;
        control.idleChew = 0f;
        control.idleHunt = 0f;
        control.idleSleep = 1f;
    }

    private void FaceToward(float x)
    {
        float dx = x - transform.position.x;
        if (Mathf.Abs(dx) <= faceDeadzone)
            return;
        control.SetFacing(dx >= 0f ? 1f : -1f);
    }

    // Follow / greet / hunt: wander FaceToward still uses the root
    // (leash is spawn). Hunt must not share that — stoopRange would
    // never close after the body left spawn.
    private void FaceBodyToward(float x)
    {
        float dx = x - SelfWorldPos().x;
        if (Mathf.Abs(dx) <= faceDeadzone)
            return;
        control.SetFacing(dx >= 0f ? 1f : -1f);
    }

    private Vector2 SelfWorldPos()
    {
        if (body != null)
            return body.GetFollowPoint();
        if (control != null && control.hasCom)
            return control.comPosition;
        return transform.position;
    }

    private static Vector2 PlayerWorldPos(Transform player)
    {
        if (sharedPlayerCom != null)
            return sharedPlayerCom.GetCenterOfMass();
        if (sharedPlayerBody != null)
            return sharedPlayerBody.position;
        return player.position;
    }

    // Follow Walk is not wanderGoing, so a vanished player used to keep
    // trotting: TickWander saw walkWanted, CanIdle was false, TickIdle
    // restored idleWag and returned. Sit first; later frames wander.
    private void ReleaseFollow(float now)
    {
        followingPlayer = false;
        greetUntil = 0f;
        if (control.walkWanted || control.IsWalk())
        {
            control.EndWalk();
            StartPant(now);
        }

        TickIdle(now, true);
    }

    // Scene Dog has no player, so follow never writes Walk. Short
    // Forage trots on a spawn leash (nose down), then sit idle as World.
    private void TickWander(float now)
    {
        if (control == null)
            return;
        if (wanderGoing)
        {
            TickWanderWalk(now);
            return;
        }

        bool sitting = !control.walkWanted && !control.IsWalk();
        if (idleKind == 0 && now >= nextIdle && WantWanderPick())
        {
            if (BeginWander(now))
                return;
        }

        TickIdle(now, sitting);
    }

    private bool WantWanderPick()
    {
        if (rng == null)
            return false;
        return rng.Next(0, 5) < 2;
    }

    private bool BeginWander(float now)
    {
        if (control == null || !control.CanIdle())
            return false;

        float selfX = SelfWorldPos().x;
        float radius = Mathf.Max(0.6f, wanderRadius);
        float lo = wanderHomeX - radius;
        float hi = wanderHomeX + radius;
        float step = Mathf.Max(0.25f, wanderStep) * Rand(0.7f, 1.3f);
        float dir = rng != null && rng.Next(0, 2) == 0 ? -1f : 1f;
        if (selfX + dir * step < lo || selfX + dir * step > hi)
            dir = -dir;
        float target = Mathf.Clamp(selfX + dir * step, lo, hi);
        if (Mathf.Abs(target - selfX) < wanderArrive)
            return false;

        ClearIdleAct();
        FaceBodyToward(target);
        wanderTargetX = target;
        wanderGiveUp = now + Mathf.Max(1f, wanderMaxSeconds);
        wanderGoing = true;
        ForageWalk(true);
        return true;
    }

    private void TickWanderWalk(float now)
    {
        float dx = wanderTargetX - SelfWorldPos().x;
        if (Mathf.Abs(dx) <= Mathf.Max(0.05f, wanderArrive) || now >= wanderGiveUp)
        {
            control.EndWalk();
            wanderGoing = false;
            StartPant(now);
            TickIdle(now, true);
            return;
        }

        FaceBodyToward(wanderTargetX);
        ForageWalk(true);
    }

    // After a kill: nose on the dirt, jaw snaps, no tongue and no wag.
    // idleKind 6 so TickIdle does not pick sniff/yawn over the meal.
    // eatChew 0 keeps the former post-leap pant.
    private void StartChew(float now)
    {
        if (control == null)
            return;
        float hold = Mathf.Max(0f, eatChew);
        if (hold <= 0f)
        {
            StartPant(now);
            return;
        }

        ClearIdleAct();
        idleKind = 6;
        idleStarted = now;
        idleUntil = now + hold * Rand(0.85f, 1.25f);
    }

    // After a Forage stop: jaw pump, no head lift (yawn is idleKind 4).
    // If Bark() is holding greetUntil, TickIdle keeps the look until then.
    private void StartPant(float now)
    {
        if (control == null)
            return;
        ClearIdleAct();
        idleKind = 5;
        idleStarted = now;
        idleUntil = now + Rand(1.8f, 2.8f);
        if (now < greetUntil)
            idleUntil = greetUntil + Rand(1.2f, 2.0f);
    }

    // Walk() zeros idle. Forage puts the wag back; DriveWalk nods from
    // that flag. WalkDistalTail limps Tail2–Tail5 Visual with that
    // trot clock (amp 0 = rest-spring stick; no distal muscle).
    // SitHuntDistalTail / WalkHuntDistalTail perk those same Visuals
    // on idleHunt. LeapHuntDistalTail perks those same Visuals on
    // IsLeap. No-player wander also restores idleSniff so the neck
    // tracks the dirt (same SPD drop as sit sniff). Follow skips sniff
    // (bobs at hold). Hunt never calls this: TickHunt writes idleHunt.
    // Gait is Walk vs Leap from life, not keys. Play never keeps the
    // leftover sit-trot (runSpeed 0 / dog_w10 0.286 km/h): Forage and
    // Pursue pick the diagonal. Cadence is stride/speed on the body.
    // Launch: Walk() writes phase 0 only after standComHeight is
    // the measured quiet-sit CoM (not spawn inverted-V). Dual
    // plus-paws the TRAIL of the DOG-3 diagonal (FR+RL vs FL+RR)
    // with Dual-soft hip extra-extending 4–8° caudal until
    // heel-rise, then Arms. Leftover-sit Dual-soft fought plus
    // 0.18 (dog_w68 leftover sit-trot). Dual lean ramps from Dual
    // t=0. DualReady Arms only on heel-rise or trail air — the
    // 0.12 s ceiling and a 3 cm CoM shift Zeroed a still-flat pad
    // (dog_w67 leftover sit-trot). Bound pairing dropped the
    // unsupported girdle. Dual/peel seek launch+lead. Dual plus-
    // paws the take. Dual-soft hip is Dual-only and trail-only.
    // After Arm the trail hip HOLDS leftover sit; the trail knee
    // is sit-relative +18° ON from Arm. After Arm the trail paw
    // Zeroes: plus 0.18 cannot pitch a loaded pad (human ankle).
    // Arm at minDual Zeroed a still-flat pad (dog_w66 leftover
    // sit-trot). Dual still plus-paws the take. Caudal 10° after
    // Arm skated the pad. Arm-tick U=0 jacked leftover sit.
    // Inverted-V 6 cm was ~63° (dog_w58). 3 mm never came on a
    // welded pad (dog_w63). dog_w62 more-minus was swing-flex
    // on a pin. Extra plus-knee shortened the column (dog_w61). After
    // true air hold the 6 cm fold and plant cranially, without 12 cm IK. Front swing stays a third
    // plant until the hind is air. Empty GetContacts inside
    // 2×contactOffset is not air. Stance keeps Dual extra-extend
    // through the peel. Free foot: cranial hip, not 12 cm IK.
    // Swing paw Zeroes after Arm (plus 0.18 cannot pitch a loaded
    // pad) and once the sole is air (plus 0.55 pinned the toe).
    // CoP walks at runSpeed only after the trail is air.
    // Distal opens after the hip is cranial. First peel stays armed
    // unless the trunk is a belly. walkPhase 1 when the rear swing
    // paw is up. Swap is that plant, not a hip slide. Front-stance
    // lean through the peel. DogDrive Walk() without keys.
    private void ForageWalk(bool track = false)
    {
        if (control == null)
            return;
        // Fallen / GettingUp: still call Walk() so the wish drives
        // Fallen→GettingUp; DriveGetUp tucks then rises; capture Walk
        // arms only after BeginWalk. standComHeight is cleared on
        // EnterGettingUp and re-measured after the rise.
        if (!control.standComHeightValid
            && !control.IsFallen()
            && !control.IsGettingUp())
            return;
        PickBoundGait();
        control.Walk();
        control.idleWag = 1f;
        if (track)
            control.idleSniff = 0.70f;
    }

    // DOG-4: the brain picks the gait. runSpeed 0 is the old diagonal
    // sit wiggle (0.286 km/h). The bound (both fronts, then both
    // rears) dumped the unsupported end. Canonical is the sagittal
    // diagonal: FR+RL vs FL+RR. Trail (rear swing) peels first;
    // front swing stays a plant until that hind is air. Stance
    // diagonal holds both trunk ends. Default runSpeedRef 1.4 is
    // 5 km/h. Duty > 0.50 sat on phase 0 (leftover 0.64). Headless
    // -dogWalk uses Walk() / EnsureBoundGait. No keys. Body owns
    // DualReady: Arm on SwingReadyToRocker (heel rose OR heel-free
    // + toe scrape) then TrailCatchSlide walks CoP without
    // union-air wait (dog_w72). dog_w73: EnsureBoundGait also caps
    // half ≤ 0.20 s (faster cadence) and GETUP leads with knees.
    // dog_w74: EnsureBoundGait caps half ≤ 0.18 s, HoldDrop 1.5 cm,
    // walkComMix floor 0.28 — raise step frequency / cut sag /
    // extend travel toward 5 km/h. The brain only writes Walk().
    private void PickBoundGait()
    {
        if (control == null)
            return;
        control.EnsureBoundGait();
    }

    // Shared among dogs. FindAny — no sort. Not a per-frame walk.
    // Bind CoM / Torso once: the Human root does not travel with the walk.
    private static Transform CachedPlayer(float now)
    {
        if (sharedPlayer != null)
            return sharedPlayer;
        sharedPlayerBody = null;
        sharedPlayerCom = null;
        if (now < sharedPlayerScanAt)
            return null;
        sharedPlayerScanAt = now + 0.5f;
        PlayerInputSource src = Object.FindAnyObjectByType<PlayerInputSource>();
        if (src == null)
        {
            sharedPlayer = null;
            return null;
        }

        sharedPlayer = src.transform;
        Human human = src.GetComponent<Human>();
        sharedPlayerCom = human != null ? human.comCalculator : null;
        Transform torso = sharedPlayer.Find("Torso");
        sharedPlayerBody = torso != null ? torso : sharedPlayer.Find("Pelvis");
        return sharedPlayer;
    }

    // Sniff / scratch / yawn / look / pant / chew while Forage sits. Not Walk:
    // leftover sit dumps on a trot. Tail wag is the calm hold between acts.
    // SitQuietRump hitches Pelvis Visual with the quiet-sit 0.0018 chest
    // (amp 0 = statue rump). SitQuietHead hitches the SPD neck on that
    // same 2.5 clock (amp 0 = statue world-0). SitQuietEar hitches the
    // Head-Visual ears on that same clock (amp 0 = statue rest fold 0.20).
    // SitQuietBrow hitches the Head-Visual brows on that same clock
    // (amp 0 = rest stamp). SitQuietMouth hitches the Head-Visual
    // flews on that same clock (amp 0 = rest flews). SitQuietWhisker
    // hitches the Head-Visual stamps on that same clock (amp 0 =
    // rest stamps). SitQuietDistalTail limps Tail2–Tail5 Visual
    // with the 8.5 Hz first-hinge wag (amp 0 = rest-spring stick;
    // no distal muscle).
    private void TickIdle(float now, bool sitting)
    {
        if (control == null)
            return;
        if (!sitting || !control.CanIdle())
        {
            // Follow still ticks idle after Walk(). Do not ClearIdle:
            // that would wipe the forage wag every frame of the trot.
            if (control.walkWanted || control.IsWalk())
            {
                ClearIdleAct();
                control.idleWag = 1f;
                return;
            }
            control.ClearIdle();
            idleKind = 0;
            nextIdle = now + 0.8f;
            return;
        }

        control.idleWag = 1f;
        if (now < greetUntil)
        {
            control.idleSniff = 0f;
            control.idleScratch = 0f;
            control.idleYawn = 0f;
            control.idlePant = 0f;
            control.idleChew = 0f;
            control.idleHunt = 0f;
            control.idleSleep = 0f;
            control.idleLook = attendLook;
            return;
        }

        if (idleKind != 0 && now >= idleUntil)
        {
            ClearIdleAct();
            nextIdle = now + Rand(1.6f, 4.2f);
        }

        if (idleKind == 0 && now >= nextIdle)
        {
            int pick = rng != null ? rng.Next(0, 10) : 0;
            idleStarted = now;
            if (pick < 3)
            {
                idleKind = 1;
                idleUntil = now + Rand(1.5f, 2.6f);
            }
            else if (pick < 6)
            {
                idleKind = 2;
                idleUntil = now + Rand(0.8f, 1.6f);
                if (sharedPlayer == null && rng != null && rng.Next(0, 2) == 0)
                    control.SetFacing(-control.FacingSign());
            }
            else if (pick < 8)
            {
                idleKind = 3;
                idleUntil = now + Rand(0.7f, 1.2f);
            }
            else
            {
                idleKind = 4;
                idleUntil = now + Rand(0.9f, 1.4f);
            }
        }

        WriteIdlePose(now);
    }

    private void WriteIdlePose(float now)
    {
        control.idleSniff = 0f;
        control.idleScratch = 0f;
        control.idleYawn = 0f;
        control.idlePant = 0f;
        control.idleChew = 0f;
        control.idleHunt = 0f;
        control.idleLook = 0f;
        control.idleSleep = 0f;
        if (idleKind == 1)
        {
            // Stay nose-down; a small bob reads as sniffing, not a nod.
            // SitSniffDistalTail limps Tail2–Tail5 Visual with that
            // bob (amp 0 = rest-spring stick; no distal muscle).
            control.idleSniff = 0.72f + 0.28f * Mathf.Sin(now * 3.1f);
        }
        else if (idleKind == 2)
        {
            // With a player, hold the look-up; random bob is the no-player glance.
            // SitLookDistalTail limps Tail2–Tail5 Visual with that
            // signed bob (amp 0 = rest-spring stick; no distal muscle).
            float bob = 0.18f * Mathf.Sin(now * 2.2f);
            control.idleLook = attendLook != 0f
                ? Mathf.Clamp(attendLook + bob, -1f, 1f)
                : Mathf.Sin(now * 2.2f);
        }
        else if (idleKind == 3)
        {
            // SitScratchDistalTail limps Tail2–Tail5 Visual with that
            // rake (amp 0 = rest-spring stick; no distal muscle).
            control.idleScratch = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(now * 14f));
        }
        else if (idleKind == 4)
        {
            // SitYawnDistalTail limps Tail2–Tail5 Visual with that
            // envelope (amp 0 = rest-spring stick; no distal muscle).
            control.idleYawn = YawnEnvelope(now);
        }
        else if (idleKind == 5)
        {
            // SitPantDistalTail limps Tail2–Tail5 Visual with that
            // hang (amp 0 = rest-spring stick; no distal muscle).
            control.idlePant = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(now * 6.5f));
        }
        else if (idleKind == 6)
        {
            // Closed between snaps so DriveJaw is a chew, not a pant hang.
            // SitChewDistalTail limps Tail2–Tail5 Visual with that
            // snap (amp 0 = rest-spring stick; no distal muscle).
            control.idleWag = 0f;
            control.idleSniff = 0.82f;
            control.idleChew = Mathf.Abs(Mathf.Sin(now * 11f));
        }
        else
            control.idleLook = attendLook;
    }

    private float YawnEnvelope(float now)
    {
        float dur = Mathf.Max(0.2f, idleUntil - idleStarted);
        float u = Mathf.Clamp01((now - idleStarted) / dur);
        if (u < 0.28f)
            return u / 0.28f;
        if (u < 0.68f)
            return 1f;
        return 1f - (u - 0.68f) / 0.32f;
    }

    private void ClearIdleAct()
    {
        idleKind = 0;
        if (control == null)
            return;
        control.idleSniff = 0f;
        control.idleScratch = 0f;
        control.idleYawn = 0f;
        control.idlePant = 0f;
        control.idleChew = 0f;
        control.idleHunt = 0f;
        control.idleLook = 0f;
        control.idleSleep = 0f;
    }

    private float Rand(float min, float max)
    {
        if (rng == null)
            return 0.5f * (min + max);
        return min + (float)rng.NextDouble() * (max - min);
    }

    private bool PreyIsLive()
    {
        if (prey == null)
            return false;
        if (preyLife == null)
            preyLife = prey.GetComponentInParent<Damageable>();
        return preyLife == null || !preyLife.IsDead;
    }

    private void BindPrey(Transform mark)
    {
        prey = mark;
        preyLife = mark != null ? mark.GetComponentInParent<Damageable>() : null;
        preyIsBird = mark != null && mark.GetComponentInParent<Bird>() != null;
    }

    // Body / CoM, not the Bird root (spawn-stuck, same leftover as follow).
    private static Vector2 BirdWorldPos(Bird bird)
    {
        if (bird == null)
            return Vector2.zero;
        if (bird.sensors != null)
            return bird.sensors.GetCenterOfMass();
        if (bird.controller != null && bird.controller.BodyRb != null)
            return bird.controller.BodyRb.position;
        if (bird.bodyCollider != null)
            return bird.bodyCollider.transform.position;
        return bird.transform.position;
    }

    private static Transform BirdAim(Bird bird)
    {
        if (bird == null)
            return null;
        if (bird.bodyCollider != null)
            return bird.bodyCollider.transform;
        Transform body = bird.transform.Find("Body");
        return body != null ? body : bird.transform;
    }

    private static bool BirdIsHuntLive(Bird bird)
    {
        if (bird == null)
            return false;
        if (bird.controller != null && bird.controller.energyRest)
            return false;
        // Damageable lives on the Bird root (same GO). No parent walk.
        if (!bird.TryGetComponent(out Damageable life))
            return true;
        return !life.IsDead;
    }

    // Shared among dogs. FindObjectsByType — no sort. Not a per-dog walk.
    private static Bird[] CachedBirds(float now)
    {
        if (sharedBirds != null && now < sharedBirdScanAt)
            return sharedBirds;
        sharedBirdScanAt = now + 0.5f;
        sharedBirds = Object.FindObjectsByType<Bird>(FindObjectsSortMode.None);
        return sharedBirds;
    }

    private static DogPrey CachedDogPrey(float now)
    {
        if (sharedDogPreyTried && now < sharedDogPreyScanAt)
            return sharedDogPrey;
        sharedDogPreyScanAt = now + 0.5f;
        sharedDogPreyTried = true;
        sharedDogPrey = Object.FindFirstObjectByType<DogPrey>();
        return sharedDogPrey;
    }

    // Nearest live Bird in noticeRange. Scan is 0.5 s, not every frame.
    // Resting off-camera birds are not quarry. Aim at Body so LeapAttack
    // tracks the flyer, not the spawn root.
    private Transform NearestLiveBird()
    {
        Bird[] birds = CachedBirds(Time.time);
        if (birds == null || birds.Length == 0)
            return null;

        Vector2 self = SelfWorldPos();
        float reach = Mathf.Max(0.05f, noticeRange);
        Transform best = null;
        float bestDist = reach;
        for (int i = 0; i < birds.Length; i++)
        {
            Bird bird = birds[i];
            if (!BirdIsHuntLive(bird))
                continue;
            float dist = Vector2.Distance(self, BirdWorldPos(bird));
            if (dist > bestDist)
                continue;
            Transform aim = BirdAim(bird);
            if (aim == null)
                continue;
            bestDist = dist;
            best = aim;
        }

        return best;
    }

    private void ClearPrey()
    {
        prey = null;
        preyLife = null;
        preyIsBird = false;
        // Do not null leapTarget: an in-flight leap still aims at the
        // last point, and DriveLeap must not FindFirstObjectByType at 200 Hz.
    }

    // Sticky on a live bird (chase after it leaves noticeRange).
    // A live DogPrey does not block a bird in range — the mark is
    // fallback. Dead = eaten: drop it so ChooseWake returns Forage.
    // Scan is 0.5 s, not every frame.
    private void ResolvePrey()
    {
        if (PreyIsLive() && preyIsBird)
        {
            deadLeapMark = null;
            return;
        }
        if (prey != null && !PreyIsLive())
        {
            if (preyLife != null && preyLife.IsDead)
                pendingEat = true;
            ClearPrey();
        }

        Transform markTf = control != null ? control.leapTarget : null;
        if (markTf == null)
            deadLeapMark = null;
        else if (markTf != deadLeapMark)
        {
            BindPrey(markTf);
            if (PreyIsLive())
            {
                deadLeapMark = null;
                if (preyIsBird)
                    return;
            }
            else
            {
                if (preyLife != null && preyLife.IsDead)
                    pendingEat = true;
                deadLeapMark = markTf;
                ClearPrey();
            }
        }

        if (Time.time < nextPreyScan)
            return;
        nextPreyScan = Time.time + 0.5f;

        Transform bird = NearestLiveBird();
        if (bird != null)
        {
            BindPrey(bird);
            deadLeapMark = null;
            return;
        }

        if (PreyIsLive())
            return;

        DogPrey mark = CachedDogPrey(Time.time);
        if (mark == null)
            return;
        BindPrey(mark.transform);
        if (preyLife != null && preyLife.IsDead)
            ClearPrey();
    }
}
