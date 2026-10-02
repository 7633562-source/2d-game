using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Off-camera NPC rest. The hero Human stays at 200 Hz. Dogs, birds,
// and hedgehogs that sit beyond the view become Kinematic, leave the
// solver (simulated = false), and skip their FixedUpdate drive. On
// screen the body is unchanged. Kinematic leftover still paid ground
// contacts at 200 Hz; simulated=false drops that island. Before unsim,
// pin Transform to Rigidbody2D.position — autoSyncTransforms is off,
// so unsim would otherwise alias pose to dirt spawn and ProbeX would
// wake a far leftover onto the hero island. Wake puts simulated back
// first, then the latched pose, then bodyType. Physics rest is
// NpcRestEarly (order -120) so the same step skips sensors / muscles.
// LateUpdate stays here (110) after CameraFollow.
// HeadlessTrial never rests: the stand has no camera follow and must
// stay deterministic. Sensors and BirdFlight stay enabled (PERF-5).
// Rest also drops Muscle / JointFriction / BodyAero / strike /
// TreeSpring (dog tail) from the player loop. Rest also latches
// CollisionDetectionMode2D and writes Discrete before unsim:
// leftover Paw/Foot CCD is a 200 Hz solver bill, and simulated=
// false must not keep Continuous on the island. Rest writes
// Discrete twice (before and after Kinematic) so a type change
// does not restore Continuous, then disables latched colliders
// and Joint2D so leftover sit leaves the broadphase and the
// hinge island. Wake restores joints, then colliders except
// HuntBeak (BakeHuntSlab). Not BirdSensors / BirdFlight
// (PERF-5). Monitor RestingActiveCcdBodies must stay 0. On-screen
// bodies keep CCD. Far leftover sit latches island counts at the
// rest edge: Tick does not walk colliders / joints / CCD on a
// slot still metres past wake. Physics Tick (NpcRestEarly) only
// rest-decides live walkers — leftover sit cannot wake until
// LateUpdate moves the camera, so 18-body dogs stay off that
// path. Live-walker indices are rebuilt in LateUpdate; the
// physics pass walks that list, not every resting dog. When
// LiveCount is 0, NpcRestEarly leaves the 200 Hz list: leftover
// sit still cannot wake until CameraFollow, so an empty
// FixedUpdate on GameManager is a hero-island tax. Live flock
// hens (one body) rest-decide in LateUpdate — Early on those
// two World x=12 walkers was a hero-island FixedUpdate every
// display frame. Early arms only when a heavy island (dog /
// hedgehog, physicsRestMinBodies) sits in the near-rest band,
// so an 18-body walker leaving the clip skips the same step.
// Extra 200 Hz steps of that frame still disarm until LateUpdate.
// Rest also drops Dog.LateUpdate (16 face Apply* with no
// energyRest gate) by disabling the Dog behaviour; DriveIsOff
// counts that look script, not only stance. Near wake (and rest/wake edges) refresh the cache so
// a leak cannot ride into view. BirdFlockDrive freezes when every
// pack member (no BirdDrive) rests. A live woodpecker must not
// ForceFly leftover Sit.
// World hens at x=12 stay live (Dynamic) by contract. Sit leftover
// past the clip still paid BirdController FixedUpdate + LateUpdate
// Apply* (one flock body, eight sprite writes) inside the 4 m
// walker band. Sit-only park (17:40) came back on as soon as
// ChickenWander wrote Walk/Peck — forage off-clip is still a
// 200 Hz pose loop. Park any hen past the half-view until the
// body is in frame. BirdDrive stays on so forage can still write
// mode. Not energyRest (that is full rest). Not BirdSensors /
// BirdFlight (PERF-5). Look-park also parks HuntBeak so a leftover
// Walk BakeHuntSlab does not leave a simulated query on the hero
// island after the controller drops. Drive-off alone still left
// BodyAero / BeakStrike on the player loop and a Dynamic flock
// body in Simulate. Look-park drops that loop (Bind flags stay)
// and unsims the probe — still Dynamic, still in-frame when
// dx ≤ halfView. Sleep() on dirt was a no-op: Ground contact
// wakes StartAwake every step. simulated=false keeps the type
// Dynamic (npcLiveHens 2) and leaves Physics2D.Simulate. Pin
// Transform first (autoSyncTransforms is off). Sensors / flight
// stay enabled; look-park writes drivenExternally so their own
// FixedUpdate returns without enabled=false (PERF-5). Flock
// already latches that flag; this covers a leak after the
// controller drops.
// HuntBeak is a world-root Default query (Bird 15:26):
// OverlapCollider needs an attached body, and a Body child used
// BirdCore (Core×Default off). Bind latches that root once from
// GetRootGameObjects (previous sibling is the bird that Awoke it
// — not Tick, not FindObjectsByType on the frame path). Rest
// parks like Bird ParkHuntSlab: unsim first, Kinematic,
// StartAwake, trigger, FreezeAll, pose at (0, ParkY) on
// Rigidbody2D.position AND offset. Live Peck (Bird 19:20) is
// Dynamic NeverSleep solid — leaving that sleepMode / isTrigger
// after unsim is a 200 Hz island the moment simulated leaks,
// and the world-root RB is not in slot.bodies so npcLiveBodies
// stays 0 unless CountLiveBodyTypes also reads huntBeakRb.
// Bird 05:10 ParkHuntSlab ClearHuntQueryLayers (include
// Nothing / exclude all) — no longer fights this belt. Rest
// still clears the same masks: a simulated leak must not be
// Default×Default on the hero island if a live Peck path
// rewrote them before look-park / energyRest. BakeHuntSlab
// restores Default on live Peck. Origin+offset ParkY left a
// leftover AABB on the hero island if simulated flipped;
// disable was the 1×1 origin miss.
// Wake leaves it unsim — BakeHuntSlab / PrepareHuntQueryBody
// turns simulated on only while the dirt slab is live. Idle Sit
// must not put a HuntBeak back on the hero island. Stay
// Kinematic (not Dynamic, not Destroy). A leftover Body-child
// trigger still disables on rest. Orphan roots park at Bind.
// BirdController is disabled on rest (SetDriveEnabled): otherwise
// energyRest RestorePeckReach runs Update+FixedUpdate+LateUpdate
// and re-parks HuntBeak 3×/frame on leftover Sit. Not
// BirdSensors / BirdFlight (PERF-5). BodyAero is in the rest
// loop (empty FixedUpdate while BirdFlight.enabled).
// LateUpdate skips the leftover-sit island walk while the camera
// X / half-view are still: only live walkers can cross restAt
// without CameraFollow. 18-body dogs stay off that 60 Hz path.
// KeepAwakeCount no longer blocks that skip: Chicken leftover
// hens (1 followed, 4 off-camera) used to pay the island walk
// every LateUpdate. Camera motion already invalidates the skip.
// keepAwake bodies are a Bind index, not a full slot walk.
[DefaultExecutionOrder(110)]
public class NpcRest : MonoBehaviour
{
    [Header("Distance from camera, m")]
    [Tooltip("Far-yard hysteresis, m. Rest itself follows half the view plus margin, not this floor.")]
    public float restX = 18f;
    [Tooltip("Wake when closer than this. Must stay below the live rest distance.")]
    public float wakeX = 14f;
    [Tooltip("Hen rest past the visible half-width, m. World hens at x=12 must stay live (halfView≈8.9). Dogs and hedgehog use quadViewMargin.")]
    public float viewMargin = 4f;
    [Tooltip("Crow / woodpecker rest past the visible half-width, m. Flock body is 0.14 m; 0.10 m puts the sprite off the edge.")]
    public float flyViewMargin = 0.10f;
    [Tooltip("Dog / hedgehog rest past the visible half-width, m. Not the hen 4 m band: that kept 18 Dynamic hedgehog bodies live 4 m off-camera.")]
    public float quadViewMargin = 1.5f;
    [Tooltip("Do not rest tighter than this camera aspect. A square Game view would otherwise freeze World hens at x=12 (acceptance: they stay live at 16:9, ortho 5).")]
    public float minViewAspect = 16f / 9f;
    [Tooltip("Arm NpcRestEarly only when a live walker this close to restAt has at least physicsRestMinBodies. Flock hens rest in LateUpdate.")]
    public float nearRestBand = 0.5f;
    [Tooltip("Dog / hedgehog island. Flock hen / crow is one body — not this gate.")]
    public int physicsRestMinBodies = 8;

    public int RestingCount { get; private set; }
    public int LiveCount => SlotCount - RestingCount;
    public int SlotCount => slots != null ? slots.Length : 0;
    public int KeepAwakeCount { get; private set; }
    public int TightRestCount { get; private set; }
    public int LiveTightCount { get; private set; }
    // Physics-island bill on live slots: bodyType Dynamic right now.
    // Sit flock on a pad is already Kinematic — that is LiveKinematicBodies
    // plus BirdController.FixedUpdate (LiveTightCount), not a Dynamic island.
    // Level1 opening keeps the hedgehog (dx≈1.6, Dynamic) and apple Sit
    // flock (dx≈8 < flyRestAt, Kinematic Sit) live by contract.
    public int LiveDynamicBodies { get; private set; }
    public int LiveKinematicBodies { get; private set; }
    public int RestingBodies { get; private set; }
    // RestingBodies that left Physics2D.Simulate (simulated=false),
    // plus resting world-root HuntBeak Kinematic queries.
    public int RestingUnsimulatedBodies { get; private set; }
    // Live island: simulated Continuous (Foot / Paw / Toe). World spawn
    // leftover dogs rest, so this is 0 there. Level1 hedgehog in frame
    // is 4 by contract — that is the on-screen body, not a rest leak.
    public int LiveCcdBodies { get; private set; }
    // Resting physics bodies whose latched mode was Continuous.
    // Monitor proof that leftover sit had CCD, not that it still pays.
    public int RestingCcdBodies { get; private set; }
    // Resting bodies whose collisionDetectionMode is still Continuous
    // right now. Discrete + collider-off must keep this 0.
    public int RestingActiveCcdBodies { get; private set; }
    // Resting slot colliders with enabled=false (Bind arrays, not Find).
    public int RestingColliderOffCount { get; private set; }
    // Resting slot Joint2D with enabled=false (Bind arrays, not Find).
    public int RestingJointOffCount { get; private set; }
    // Resting slots whose island cache was not walked this Tick
    // (still past wake + pad). World spawn leftover dogs / far
    // crows / parked hens. Not a 200 Hz Find.
    public int FarRestSkipCount { get; private set; }
    // LateUpdate frames that skipped the leftover-sit island walk
    // (camera still; only live walkers rest-decide). Not a Find.
    public int FarRestTickSkipCount { get; private set; }
    // Muscle / JointFriction / BodyAero / strike / TreeSpring dropped
    // from the player loop while resting. Not BirdSensors / BirdFlight
    // (PERF-5).
    public int RestingLoopCount { get; private set; }
    // Live walkers inside nearRestBand of restAt (not keepAwake).
    // World hens at x=12 sit here; that does not arm Early.
    public int NearRestWalkCount { get; private set; }
    // Near-rest walkers with physicsBodyCount >= physicsRestMinBodies.
    // Monitor npcEarly follows this, not LiveCount.
    public int NearRestHeavyCount { get; private set; }
    // True while a heavy island needs a physics rest pass this
    // scene. Not earlyTick.enabled: Extra FixedUpdates disarm Early
    // until LateUpdate, and Update would otherwise report npcEarly=0
    // after the first step. Flock hens do not arm this.
    public bool PhysicsRestArmed => earlyTick != null
        && !HeadlessTrial.Active
        && KeepAwakeCount < SlotCount
        && NearRestHeavyCount > 0;
    // World spawn proof: leftover dogs and far crows rest; hens near x=12 stay live.
    public int RestingDogCount { get; private set; }
    public int RestingFlyerCount { get; private set; }
    public int LiveHenCount { get; private set; }
    public int RestingHenCount { get; private set; }
    // Live (not resting) hens whose BirdController is off: leftover
    // past the clip in the walker band (Sit or Walk/Peck). World
    // spawn: both x=12. Not RestingDriveOffCount (that requires
    // slot.resting).
    public int LiveHenDriveOffCount { get; private set; }
    // Live hens whose rest loop is off (look-park). BodyAero /
    // BeakStrike left the player loop. Bind enabled flags stay.
    // Not energyRest. Not BirdSensors / BirdFlight (PERF-5).
    public int LiveHenLoopOffCount { get; private set; }
    // Live look-park hens whose Dynamic probe left Simulate
    // (simulated=false, bodyType still Dynamic). Sleep on dirt
    // does not hold; this does. World spawn: both x=12.
    public int LiveHenUnsimCount { get; private set; }
    // Live look-park hens whose sensors/flight skip own
    // FixedUpdate (drivenExternally). Behaviours stay enabled.
    public int LiveHenDriveSkipCount { get; private set; }
    // Look-parked hens whose HuntBeak matches rest park: unsim,
    // Kinematic, StartAwake, trigger, include Nothing. Bird
    // ParkHuntSlab Default include / leftover Peck NeverSleep
    // solid is not this count.
    public int LiveHenHuntParkCount { get; private set; }
    public int RestingHedgehogCount { get; private set; }
    public int LiveHedgehogCount { get; private set; }
    // World crows have no per-bird BirdDrive. Rest must freeze the pack
    // brain or it writes Fly onto a Kinematic leftover Sit.
    public int LiveFlockDriveCount { get; private set; }
    // Flyers in a BirdFlockDrive pack (no per-bird BirdDrive). A live
    // woodpecker must not keep the crow brain on: that ForceFlys
    // Kinematic leftover Sit off-camera.
    public int LiveFlockOwnedCount { get; private set; }
    // Resting flyers whose HuntBeak left Simulate (Kinematic
    // simulated=false, or leftover child collider off).
    public int RestingHuntBeakCount { get; private set; }
    // Slots that have latched HuntBeak (world-root Kinematic or child).
    // Bind, not Tick. Proof that rest does not Find on the frame path.
    public int HuntBeakCacheCount { get; private set; }
    // Bind child-latch walks plus world-root sibling pairing.
    // Tick does not increment this.
    public int HuntBeakScanCount { get; private set; }
    // Play Bird / Chicken quarry. World / Level1 stay 0 — no crowd.
    public int InsectCount { get; private set; }
    // Always false: Tick does not poll scene roots. WORLD_PERF
    // npcHuntPoll=0 is the proof.
    public bool HuntBeakPollArmed { get; private set; }
    // Last Tick thresholds. Monitor reads these; 0 before the first camera tick.
    // LastRestAt / LastWakeAt are the hen band (World x=12 stays live).
    // LastQuadRestAt is dogs / hedgehog — tighter so leftover sit does not
    // pay 200 Hz while already off the clip plane.
    public float LastRestAt { get; private set; }
    public float LastWakeAt { get; private set; }
    public float LastFlyRestAt { get; private set; }
    public float LastFlyWakeAt { get; private set; }
    public float LastQuadRestAt { get; private set; }
    public float LastQuadWakeAt { get; private set; }
    public float LastCamX { get; private set; }
    public float LastHalfView { get; private set; }
    // Physics Tick is one rest decision per render frame. Extra 200 Hz
    // FixedUpdates skip; energyRest stays latched.
    public int PhysicsRestTicks { get; private set; }
    public int PhysicsRestSkips { get; private set; }
    // Live walkers visited by the physics Tick (not keepAwake, not
    // leftover sit). World spawn: nearby hens. Far dogs / crows /
    // parked hens are 0 here — they wait for LateUpdate.
    public int PhysicsLiveWalkCount { get; private set; }
    // Resting slots whose species drive AND Dog look are
    // enabled=false (Bind cache, not Find). World leftover sit
    // must match RestingCount. Not BirdSensors / BirdFlight (PERF-5).
    public int RestingDriveOffCount { get; private set; }
    // Contract proof: player Human is never a rest slot. Bind /
    // Absorb only — not Tick. WORLD_PERF npcHeroSlots must stay 0.
    public int HeroSlotCount { get; private set; }

    private Camera followCamera;
    private Transform followXf;
    private Transform keepAwakeRoot;
    private bool keepFollowedFlockAwakeFlag;
    private NpcRestEarly earlyTick;
    private Slot[] slots = System.Array.Empty<Slot>();
    private BirdFlockDrive[] flocks = System.Array.Empty<BirdFlockDrive>();
    private bool[] flockWasEnabled = System.Array.Empty<bool>();
    private int restPhysicsFrame = -1;
    // LateUpdate rebuild. Physics Tick walks these, not resting
    // 18-body dogs. Bind allocates once; Tick does not new[].
    private int[] liveWalkIndices = System.Array.Empty<int>();
    private int liveWalkIndexCount;
    // Bind / Absorb only. keepAwake does not change after MarkKeepAwake.
    // Far-island skip recounts these so LiveDynamicBodies stays honest
    // without walking leftover sit (Chicken followed hen).
    private int[] keepAwakeIndices = System.Array.Empty<int>();
    private int keepAwakeIndexCount;
    // Last full LateUpdate island walk. A still camera cannot wake
    // leftover sit; only live walkers can cross restAt.
    private float decidedCamX;
    private float decidedHalfView;
    private bool restTotalsLatched;
    private bool liveWalkRestDirty;
    // WORLD_PERF capture only. Far leftover sit normally keeps the
    // last LatchRestIsland / DriveIsOff cache; capture must re-walk
    // those islands so npcRestCcdNow / npcRestDriveOff match the
    // bodies this frame, not the previous rest edge.
    private bool captureIslandRecount;
    // Bind-only world-root HuntBeak pairing. Tick does not walk roots.
    private readonly List<GameObject> rootScratch = new List<GameObject>(64);
    private Transform[] insects = System.Array.Empty<Transform>();

    private sealed class Slot
    {
        public Transform root;
        // Physics body, not the species host. Dog / hedgehog / flock Body
        // is a child Rigidbody2D; the host stays at spawn while the chest
        // walks. Rest on root.x would keep a walker live off-camera.
        public Transform probe;
        public Rigidbody2D probeRb;
        public Rigidbody2D[] bodies;
        public RigidbodyInterpolation2D[] interpolation;
        // CCD at the rest edge. Rest writes Discrete then unsim; wake
        // restores this, not a permanent Discrete leftover on Paw.
        public CollisionDetectionMode2D[] detection;
        // Colliders at Bind. Rest disables them so leftover sit leaves
        // the contact island even if unsim keeps a fixture. Wake
        // restores except HuntBeak.
        public Collider2D[] colliders;
        public bool[] colliderEnabled;
        // Hinges at Bind. Rest disables them so leftover sit does not
        // keep a Box2D island through enabled Joint2D on unsim bodies.
        // Wake restores Bind-time enabled.
        public Joint2D[] joints;
        public bool[] jointEnabled;
        // Body type at the rest edge. A perched Flock bird is already
        // Kinematic on the pad; wake must restore that, not force Dynamic.
        public RigidbodyType2D[] bodyTypes;
        // simulated at the rest edge. Rest sets false so leftover sit
        // leaves Physics2D.Simulate; wake restores this before bodyType.
        public bool[] simulated;
        // Physics pose at the rest edge. autoSyncTransforms is off:
        // simulated=false aliases Rigidbody2D.position to Transform,
        // which can still sit on dirt spawn (BIRD-12). Pin + latch
        // so ProbeX does not wake a far leftover onto the hero island.
        public Vector2[] bodyPositions;
        public float[] bodyRotations;
        public float restProbeX;
        public DogStanceController dog;
        public Dog dogBody;
        public HedgehogStanceController hedgehog;
        public ActuatorDriver actuators;
        public BirdController bird;
        public BirdSensors sensors;
        public BirdFlight flight;
        // Update brain (BirdDrive / DogDrive / HedgehogDrive).
        // Rest disables it — BirdDrive has no energyRest gate. Not sensors
        // and not BirdFlight (PERF-5 keeps those enabled).
        public Behaviour intent;
        public bool intentEnabled;
        public bool birdEnabled;
        public bool dogEnabled;
        public bool dogBodyEnabled;
        public bool hedgehogEnabled;
        public bool actuatorsEnabled;
        public bool sensorsDriven;
        public bool flightDriven;
        // Muscle, JointFriction, BodyAero, BeakStrike, JawStrike,
        // HedgehogBite, TreeSpring (dog tail). Bind only. Not
        // sensors, not BirdFlight.
        public Behaviour[] loop;
        public bool[] loopEnabled;
        public bool resting;
        // Walker-band hen past the clip: drive + loop off, Dynamic
        // probe unsim. Not energyRest. Cleared on full rest.
        public bool lookParked;
        // Probe.simulated at the look-park edge. Wake restores this
        // (true) before a later energyRest latch. Do not write the
        // rest-edge simulated[] here — that would freeze unsim on
        // full-rest wake.
        public bool lookParkSimulated;
        public bool lookParkSensorsDriven;
        public bool lookParkFlightDriven;
        public float lookParkProbeX;
        // Scene Dog / Bird / Chicken: the followed body stays Dynamic
        // even if SmoothDamp puts the camera a metre off the root.
        public bool keepAwake;
        // Crow / woodpecker: rest just past the view. Hens walk onto
        // screen, so they keep the 4 m walker band (World x=12).
        public bool tightRest;
        // Pack member: tightRest and no BirdDrive. Woodpecker / solo
        // wander keep their own Update brain.
        public bool flockOwned;
        // World-root Kinematic HuntBeak query (or leftover Body child).
        // Bind latches from scene roots / probe children, not Tick.
        public BoxCollider2D huntBeak;
        public Rigidbody2D huntBeakRb;
        // Mode at the rest edge. BirdFlockDrive has no energyRest gate;
        // LateUpdate stamps this back so wake is leftover, not ForceFly.
        public BirdMode modeAtRest;
        // Non-Static bodies at Bind. Rest turns them Kinematic and
        // simulated=false; the count is the 200 Hz island, not a
        // per-step GetComponents.
        public int physicsBodyCount;
        // Island bill at the rest edge (and near-wake refresh).
        // Far leftover sit reuses these so LateUpdate does not
        // walk 18-body dogs every frame while they stay off-camera.
        public int restUnsimCached;
        public int restCcdLatchedCached;
        public int restCcdActiveCached;
        public int restColOffCached;
        public int restJointOffCached;
        // Bind / rest-edge: species + Dog look. Far leftover sit
        // reuses this so LateUpdate does not poll .enabled on 18-body
        // dogs. Dog.LateUpdate has no energyRest gate — the off flag
        // is the 60 Hz proof that those 16 Apply* left the player loop.
        public bool restDriveOffCached;
        public bool restHuntOffCached;
    }

    // dogs / birds from GameProcess. Scene Find once here also picks
    // LevelGrove plants (hedgehog, woodpecker) that those lists miss.
    // Camera comes from SetupCameraFollow — no per-frame Camera.main.
    // keepAwake is the CameraFollow dog/bird/hedgehog, never the hero.
    // keepFollowedFlockAwake: leftover hook. GameProcess snaps the
    // overview lens before Bind, then rests per-root. True would
    // freeze the whole pack awake (BirdFlock50 N=50 at 200 Hz).
    public void Bind(
        IReadOnlyList<Dog> dogs,
        IReadOnlyList<Bird> birds,
        Camera camera = null,
        Transform keepAwake = null,
        bool keepFollowedFlockAwake = false,
        BirdFlockDrive flock = null,
        Transform huntPrey = null)
    {
        followCamera = camera;
        if (followCamera == null)
            followCamera = Camera.main;
        followXf = followCamera != null ? followCamera.transform : null;
        keepAwakeRoot = keepAwake;
        keepFollowedFlockAwakeFlag = keepFollowedFlockAwake;

        Dog[] sceneDogs = FindScene<Dog>();
        Bird[] sceneBirds = FindScene<Bird>();
        Hedgehog[] sceneHogs = FindScene<Hedgehog>();

        int n = CountNonNull(dogs) + CountNonNull(birds)
            + sceneDogs.Length + sceneBirds.Length + sceneHogs.Length;
        Slot[] raw = n > 0 ? new Slot[n] : System.Array.Empty<Slot>();
        int w = 0;

        if (dogs != null)
        {
            for (int i = 0; i < dogs.Count; i++)
            {
                Dog dog = dogs[i];
                if (dog == null)
                    continue;
                w = AppendSlot(raw, w, dog.transform, dog.stance, null, null);
            }
        }

        if (birds != null)
        {
            for (int i = 0; i < birds.Count; i++)
            {
                Bird bird = birds[i];
                if (bird == null)
                    continue;
                w = AppendSlot(raw, w, bird.transform, null, bird.controller, null);
            }
        }

        for (int i = 0; i < sceneDogs.Length; i++)
        {
            Dog dog = sceneDogs[i];
            if (dog == null || ContainsRoot(raw, w, dog.transform))
                continue;
            w = AppendSlot(raw, w, dog.transform, dog.stance, null, null);
        }

        for (int i = 0; i < sceneBirds.Length; i++)
        {
            Bird bird = sceneBirds[i];
            if (bird == null || ContainsRoot(raw, w, bird.transform))
                continue;
            w = AppendSlot(raw, w, bird.transform, null, bird.controller, null);
        }

        for (int i = 0; i < sceneHogs.Length; i++)
        {
            Hedgehog hog = sceneHogs[i];
            if (hog == null || ContainsRoot(raw, w, hog.transform))
                continue;
            w = AppendSlot(raw, w, hog.transform, null, null, hog.stance);
        }

        if (w == raw.Length)
            slots = raw;
        else
        {
            slots = new Slot[w];
            for (int i = 0; i < w; i++)
                slots[i] = raw[i];
        }

        for (int i = 0; i < slots.Length; i++)
            MarkKeepAwake(slots[i]);
        RecountFlags();
        RecountHeroSlots();
        LatchInsects(huntPrey);
        CaptureFlocks(flock);
        LatchAllHuntBeaks();
        EnsureEarlyTick();
        SyncEarlyEnabled();
        restTotalsLatched = false;
        liveWalkRestDirty = false;
        Tick();
    }

    // HedgehogHost.Start is order 0 and may run after GameProcess Bind.
    // One Find here, not on the frame path. NpcRest is order 110.
    // Headless never attaches from GameProcess; if this object still
    // exists, do not Find birds or occupy FixedUpdate on the stand.
    void Start()
    {
        if (HeadlessTrial.Active)
            return;
        AbsorbScene();
        if (flocks.Length == 0)
            CaptureFlocks(null);
        LatchAllHuntBeaks();
        EnsureEarlyTick();
        SyncEarlyEnabled();
        Tick();
    }

    // WORLD_PERF WriteSummary runs from Update (before this LateUpdate).
    // One Tick here so capture JSON npc* match the same-frame rest belt,
    // not the previous display frame. Headless / empty Bind is a no-op.
    // Clear restTotalsLatched: a still camera would otherwise take the
    // CanSkipFarIsland path and leave RestingDogCount / RestingCcdNow
    // on the previous LateUpdate latch. Also arm captureIslandRecount
    // so far leftover sit re-runs LatchRestIsland / DriveIsOff instead
    // of the rest-edge cache (Continuous leak would otherwise stay
    // invisible to npcRestCcdNow). Not a Find. Not hero. Not 200 Hz.
    public void RefreshForCapture()
    {
        restTotalsLatched = false;
        liveWalkRestDirty = false;
        captureIslandRecount = true;
        Tick(true);
        captureIslandRecount = false;
    }

    // LateUpdate stays on this object (order 110, after CameraFollow 100).
    // Physics rest is NpcRestEarly at -120, before sensors (-100) and
    // muscles (50), so an already-off-screen body skips this step's 200 Hz
    // drive instead of paying it and resting afterward.
    private void EnsureEarlyTick()
    {
        if (earlyTick == null)
            earlyTick = GetComponent<NpcRestEarly>();
        if (earlyTick == null)
            earlyTick = gameObject.AddComponent<NpcRestEarly>();
        earlyTick.rest = this;
        SyncEarlyEnabled();
    }

    // Scene Dog / Bird (one followed body): every slot is keepAwake.
    // NpcRestEarly must not occupy a 200 Hz FixedUpdate on that island.
    // World leftover sit with LiveCount 0 is the same tax: wake is
    // LateUpdate (camera), so an empty FixedUpdate does not rest anyone.
    // Flock hens rest in LateUpdate (one body). Early is only a heavy
    // island about to leave the clip — extra physics steps of the same
    // frame still disarm until LateUpdate.
    private void SyncEarlyEnabled()
    {
        if (earlyTick == null)
            return;
        earlyTick.enabled = !HeadlessTrial.Active
            && KeepAwakeCount < SlotCount
            && NearRestHeavyCount > 0;
    }

    // Extra 200 Hz steps share last LateUpdate X. Drop Early from the
    // player loop until CameraFollow; the frame-count skip is backup
    // if a type change keeps this FixedUpdate in the current list.
    private void DisarmEarlyUntilCamera()
    {
        if (earlyTick != null)
            earlyTick.enabled = false;
    }

    public void TickFromPhysics()
    {
        // CameraFollow has not run this step. Rest against last LateUpdate
        // X so sensors / muscles skip. Extra FixedUpdates in the same
        // render frame share that X — do not walk the slots at 200 Hz.
        // A walking NPC can still cross restAt this first step; later
        // steps in the frame wait for LateUpdate (metres of hysteresis).
        // Leftover sit cannot wake until the camera moves — skip those
        // slots entirely (18-body dogs, far crows, parked hens).
        if (KeepAwakeCount >= SlotCount)
        {
            PhysicsRestSkips++;
            PhysicsLiveWalkCount = 0;
            DisarmEarlyUntilCamera();
            return;
        }

        int frame = Time.frameCount;
        if (frame == restPhysicsFrame)
        {
            PhysicsRestSkips++;
            DisarmEarlyUntilCamera();
            return;
        }

        restPhysicsFrame = frame;
        if (LiveCount < 1 || liveWalkIndexCount < 1 || NearRestHeavyCount < 1)
        {
            PhysicsRestSkips++;
            PhysicsLiveWalkCount = 0;
            HuntBeakPollArmed = false;
            if (LiveCount < 1)
                ApplyFlockDrives(false);
            SyncEarlyEnabled();
            return;
        }

        PhysicsRestTicks++;
        TickLiveWalkers();
        DisarmEarlyUntilCamera();
    }

    private void MarkKeepAwake(Slot slot)
    {
        if (slot == null)
            return;
        if (keepAwakeRoot != null && slot.root == keepAwakeRoot)
            slot.keepAwake = true;
        else if (keepFollowedFlockAwakeFlag && slot.bird != null)
            slot.keepAwake = true;
    }

    private void RecountFlags()
    {
        EnsureKeepAwakeBuffer();
        int awake = 0;
        int tight = 0;
        for (int i = 0; i < slots.Length; i++)
        {
            Slot slot = slots[i];
            if (slot == null)
                continue;
            if (slot.keepAwake)
                keepAwakeIndices[awake++] = i;
            if (slot.tightRest)
                tight++;
        }

        keepAwakeIndexCount = awake;
        KeepAwakeCount = awake;
        TightRestCount = tight;
    }

    private void AbsorbScene()
    {
        if (HeadlessTrial.Active)
            return;

        Dog[] sceneDogs = FindScene<Dog>();
        Bird[] sceneBirds = FindScene<Bird>();
        Hedgehog[] sceneHogs = FindScene<Hedgehog>();
        int extra = 0;
        for (int i = 0; i < sceneDogs.Length; i++)
        {
            Dog dog = sceneDogs[i];
            if (dog != null && !ContainsRoot(slots, slots.Length, dog.transform))
                extra++;
        }

        for (int i = 0; i < sceneBirds.Length; i++)
        {
            Bird bird = sceneBirds[i];
            if (bird != null && !ContainsRoot(slots, slots.Length, bird.transform))
                extra++;
        }

        for (int i = 0; i < sceneHogs.Length; i++)
        {
            Hedgehog hog = sceneHogs[i];
            if (hog != null && !ContainsRoot(slots, slots.Length, hog.transform))
                extra++;
        }

        if (extra < 1)
            return;

        Slot[] grown = new Slot[slots.Length + extra];
        for (int i = 0; i < slots.Length; i++)
            grown[i] = slots[i];
        int w = slots.Length;
        for (int i = 0; i < sceneDogs.Length; i++)
        {
            Dog dog = sceneDogs[i];
            if (dog == null || ContainsRoot(grown, w, dog.transform))
                continue;
            int next = AppendSlot(grown, w, dog.transform, dog.stance, null, null);
            if (next != w)
                MarkKeepAwake(grown[w]);
            w = next;
        }

        for (int i = 0; i < sceneBirds.Length; i++)
        {
            Bird bird = sceneBirds[i];
            if (bird == null || ContainsRoot(grown, w, bird.transform))
                continue;
            int next = AppendSlot(grown, w, bird.transform, null, bird.controller, null);
            if (next != w)
                MarkKeepAwake(grown[w]);
            w = next;
        }

        for (int i = 0; i < sceneHogs.Length; i++)
        {
            Hedgehog hog = sceneHogs[i];
            if (hog == null || ContainsRoot(grown, w, hog.transform))
                continue;
            int next = AppendSlot(grown, w, hog.transform, null, null, hog.stance);
            if (next != w)
                MarkKeepAwake(grown[w]);
            w = next;
        }

        if (w == grown.Length)
            slots = grown;
        else
        {
            slots = new Slot[w];
            for (int i = 0; i < w; i++)
                slots[i] = grown[i];
        }

        RecountFlags();
        RecountHeroSlots();
        LatchInsects(null);
        LatchAllHuntBeaks();
        SyncEarlyEnabled();
        restTotalsLatched = false;
        liveWalkRestDirty = false;
    }

    // Bind / Absorb only. AppendSlot already skips Human; this is the
    // monitor proof that a future leak cannot hide a hero in the list.
    private void RecountHeroSlots()
    {
        int n = 0;
        for (int i = 0; i < slots.Length; i++)
        {
            Slot slot = slots[i];
            if (slot == null || slot.root == null)
                continue;
            if (slot.root.GetComponent<Human>() != null)
                n++;
        }

        HeroSlotCount = n;
    }

    // Bind / Start / AbsorbScene only. SortMode is required on
    // 6000.5.8 (no parameterless FindObjectsByType). Not Tick.
    private static T[] FindScene<T>() where T : Object
    {
        T[] found = Object.FindObjectsByType<T>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        return found != null ? found : System.Array.Empty<T>();
    }

    private static int CountNonNull<T>(IReadOnlyList<T> list) where T : class
    {
        if (list == null)
            return 0;
        int n = 0;
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] != null)
                n++;
        }

        return n;
    }

    private static bool ContainsRoot(Slot[] list, int written, Transform root)
    {
        if (root == null)
            return true;
        for (int i = 0; i < written; i++)
        {
            if (list[i] != null && list[i].root == root)
                return true;
        }

        return false;
    }

    // Contract: the player Human is never a rest slot. Bind is spawn-only.
    private static int AppendSlot(
        Slot[] dest,
        int w,
        Transform root,
        DogStanceController dog,
        BirdController bird,
        HedgehogStanceController hedgehog)
    {
        if (dest == null || w < 0 || w >= dest.Length)
            return w;
        if (root != null && root.GetComponent<Human>() != null)
            return w;
        dest[w] = MakeSlot(root, dog, bird, hedgehog);
        return w + 1;
    }

    private static Slot MakeSlot(
        Transform root,
        DogStanceController dog,
        BirdController bird,
        HedgehogStanceController hedgehog)
    {
        Slot slot = new Slot
        {
            root = root,
            dog = dog,
            dogBody = PickDogBody(root, dog),
            bird = bird,
            hedgehog = hedgehog,
            actuators = root != null ? root.GetComponent<ActuatorDriver>() : null,
            sensors = root != null ? root.GetComponent<BirdSensors>() : null,
            flight = root != null ? root.GetComponentInChildren<BirdFlight>(true) : null,
            intent = PickIntent(root)
        };
        Bird body = root != null ? root.GetComponent<Bird>() : null;
        slot.tightRest = bird != null && body != null && body.kind != BirdKind.Chicken;
        slot.flockOwned = slot.tightRest && slot.intent == null;
        slot.modeAtRest = bird != null ? bird.mode : BirdMode.Sit;
        slot.birdEnabled = slot.bird == null || slot.bird.enabled;
        slot.dogEnabled = slot.dog == null || slot.dog.enabled;
        slot.dogBodyEnabled = slot.dogBody == null || slot.dogBody.enabled;
        slot.hedgehogEnabled = slot.hedgehog == null || slot.hedgehog.enabled;
        slot.actuatorsEnabled = slot.actuators == null || slot.actuators.enabled;
        slot.intentEnabled = slot.intent == null || slot.intent.enabled;
        if (root == null)
        {
            slot.bodies = System.Array.Empty<Rigidbody2D>();
            slot.interpolation = System.Array.Empty<RigidbodyInterpolation2D>();
            slot.detection = System.Array.Empty<CollisionDetectionMode2D>();
            slot.bodyTypes = System.Array.Empty<RigidbodyType2D>();
            slot.simulated = System.Array.Empty<bool>();
            slot.bodyPositions = System.Array.Empty<Vector2>();
            slot.bodyRotations = System.Array.Empty<float>();
            slot.probe = null;
            slot.probeRb = null;
            slot.physicsBodyCount = 0;
            FillLoop(slot);
            FillColliders(slot);
            FillJoints(slot);
            return slot;
        }

        slot.bodies = root.GetComponentsInChildren<Rigidbody2D>(true);
        slot.interpolation = new RigidbodyInterpolation2D[slot.bodies.Length];
        slot.detection = new CollisionDetectionMode2D[slot.bodies.Length];
        slot.bodyTypes = new RigidbodyType2D[slot.bodies.Length];
        slot.simulated = new bool[slot.bodies.Length];
        slot.bodyPositions = new Vector2[slot.bodies.Length];
        slot.bodyRotations = new float[slot.bodies.Length];
        int physics = 0;
        for (int i = 0; i < slot.bodies.Length; i++)
        {
            Rigidbody2D rb = slot.bodies[i];
            if (rb == null)
            {
                slot.interpolation[i] = RigidbodyInterpolation2D.None;
                slot.detection[i] = CollisionDetectionMode2D.Discrete;
                slot.bodyTypes[i] = RigidbodyType2D.Dynamic;
                slot.simulated[i] = true;
                slot.bodyPositions[i] = Vector2.zero;
                slot.bodyRotations[i] = 0f;
                continue;
            }

            slot.interpolation[i] = rb.interpolation;
            slot.detection[i] = rb.collisionDetectionMode;
            slot.bodyTypes[i] = rb.bodyType;
            slot.simulated[i] = rb.simulated;
            slot.bodyPositions[i] = rb.position;
            slot.bodyRotations[i] = rb.rotation;
            if (rb.bodyType != RigidbodyType2D.Static)
                physics++;
        }

        slot.physicsBodyCount = physics;
        slot.probe = PickProbe(root, slot.bodies, bird, out slot.probeRb);
        FillLoop(slot);
        FillColliders(slot);
        FillJoints(slot);
        return slot;
    }

    // Bind only. Drop these from Unity's player loop while resting.
    // BirdSensors and BirdFlight stay enabled (PERF-5 measured 0%).
    // Dog tail is TreeSpring, not Muscle: ActuatorDriver does not
    // set drivenExternally there, so rest must enabled=false or the
    // five vertebrae keep Apply() at 200 Hz on Kinematic bodies.
    private static void FillLoop(Slot slot)
    {
        Transform root = slot.root;
        if (root == null)
        {
            slot.loop = System.Array.Empty<Behaviour>();
            slot.loopEnabled = System.Array.Empty<bool>();
            return;
        }

        Muscle[] muscles = root.GetComponentsInChildren<Muscle>(true);
        JointFriction[] frictions = root.GetComponentsInChildren<JointFriction>(true);
        BodyAero[] aeros = root.GetComponentsInChildren<BodyAero>(true);
        TreeSpring[] springs = root.GetComponentsInChildren<TreeSpring>(true);
        // Flock BodyAero lives on Body (probe). Root children usually
        // include it; if Body is not under the host, still drop the
        // empty FixedUpdate (flight.enabled early-out) off leftover Sit.
        BodyAero probeAero = slot.probe != null
            ? slot.probe.GetComponent<BodyAero>()
            : null;
        bool probeAeroExtra = probeAero != null && !ContainsItem(aeros, probeAero);
        BeakStrike beak = root.GetComponent<BeakStrike>();
        JawStrike jaw = root.GetComponent<JawStrike>();
        HedgehogBite hogBite = root.GetComponent<HedgehogBite>();
        int n = muscles.Length + frictions.Length + aeros.Length + springs.Length
            + (probeAeroExtra ? 1 : 0);
        if (beak != null)
            n++;
        if (jaw != null)
            n++;
        if (hogBite != null)
            n++;
        if (n < 1)
        {
            slot.loop = System.Array.Empty<Behaviour>();
            slot.loopEnabled = System.Array.Empty<bool>();
            return;
        }

        Behaviour[] loop = new Behaviour[n];
        bool[] enabled = new bool[n];
        int w = 0;
        for (int i = 0; i < muscles.Length; i++)
            w = AppendLoop(loop, enabled, w, muscles[i]);
        for (int i = 0; i < frictions.Length; i++)
            w = AppendLoop(loop, enabled, w, frictions[i]);
        for (int i = 0; i < aeros.Length; i++)
            w = AppendLoop(loop, enabled, w, aeros[i]);
        if (probeAeroExtra)
            w = AppendLoop(loop, enabled, w, probeAero);
        for (int i = 0; i < springs.Length; i++)
            w = AppendLoop(loop, enabled, w, springs[i]);
        w = AppendLoop(loop, enabled, w, beak);
        w = AppendLoop(loop, enabled, w, jaw);
        w = AppendLoop(loop, enabled, w, hogBite);
        if (w == loop.Length)
        {
            slot.loop = loop;
            slot.loopEnabled = enabled;
            return;
        }

        Behaviour[] slim = new Behaviour[w];
        bool[] slimEnabled = new bool[w];
        for (int i = 0; i < w; i++)
        {
            slim[i] = loop[i];
            slimEnabled[i] = enabled[i];
        }

        slot.loop = slim;
        slot.loopEnabled = slimEnabled;
    }

    // Bind only. Rest disables these so leftover sit leaves the
    // contact island. Wake restores Bind-time enabled except HuntBeak.
    private static void FillColliders(Slot slot)
    {
        Transform root = slot.root;
        if (root == null)
        {
            slot.colliders = System.Array.Empty<Collider2D>();
            slot.colliderEnabled = System.Array.Empty<bool>();
            return;
        }

        slot.colliders = root.GetComponentsInChildren<Collider2D>(true);
        slot.colliderEnabled = new bool[slot.colliders.Length];
        for (int i = 0; i < slot.colliders.Length; i++)
        {
            Collider2D col = slot.colliders[i];
            slot.colliderEnabled[i] = col != null && col.enabled;
        }
    }

    // Bind only. Rest disables these so leftover sit does not keep a
    // hinge island. Wake restores Bind-time enabled.
    private static void FillJoints(Slot slot)
    {
        Transform root = slot.root;
        if (root == null)
        {
            slot.joints = System.Array.Empty<Joint2D>();
            slot.jointEnabled = System.Array.Empty<bool>();
            return;
        }

        slot.joints = root.GetComponentsInChildren<Joint2D>(true);
        slot.jointEnabled = new bool[slot.joints.Length];
        for (int i = 0; i < slot.joints.Length; i++)
        {
            Joint2D joint = slot.joints[i];
            slot.jointEnabled[i] = joint != null && joint.enabled;
        }
    }

    private static bool ContainsItem<T>(T[] list, T item) where T : class
    {
        if (list == null || item == null)
            return false;
        for (int i = 0; i < list.Length; i++)
        {
            if (list[i] == item)
                return true;
        }

        return false;
    }

    private static int AppendLoop(Behaviour[] loop, bool[] enabled, int w, Behaviour b)
    {
        if (b == null)
            return w;
        loop[w] = b;
        enabled[w] = b.enabled;
        return w + 1;
    }

    // Bind only. Dog.LateUpdate lives on Dog, not DogStanceController.
    private static Dog PickDogBody(Transform root, DogStanceController dog)
    {
        if (root != null)
        {
            Dog onRoot = root.GetComponent<Dog>();
            if (onRoot != null)
                return onRoot;
        }

        return dog != null ? dog.GetComponent<Dog>() : null;
    }

    // Bind only. Do not Find on the frame path.
    // ChickenWander is a plain helper on BirdDrive, not a Behaviour.
    private static Behaviour PickIntent(Transform root)
    {
        if (root == null)
            return null;
        BirdDrive birdDrive = root.GetComponent<BirdDrive>();
        if (birdDrive != null)
            return birdDrive;
        DogDrive dogDrive = root.GetComponent<DogDrive>();
        if (dogDrive != null)
            return dogDrive;
        return root.GetComponent<HedgehogDrive>();
    }

    private static Transform PickProbe(
        Transform root,
        Rigidbody2D[] bodies,
        BirdController bird,
        out Rigidbody2D probeRb)
    {
        if (bird != null && bird.BodyRb != null)
        {
            probeRb = bird.BodyRb;
            return bird.BodyRb.transform;
        }
        if (bodies != null)
        {
            for (int i = 0; i < bodies.Length; i++)
            {
                Rigidbody2D rb = bodies[i];
                if (rb != null && rb.bodyType != RigidbodyType2D.Static)
                {
                    probeRb = rb;
                    return rb.transform;
                }
            }
        }

        probeRb = null;
        return root;
    }

    void LateUpdate()
    {
        Tick(true);
    }

    private void Tick()
    {
        Tick(true);
    }

    // Physics path. Camera X is last LateUpdate. Leftover sit cannot
    // wake until CameraFollow moves. Only live walkers can cross restAt
    // this step. Walk the LateUpdate index list, not resting dogs.
    // Do not rewrite LateUpdate monitor totals besides RestingCount
    // when a walker crosses restAt (NpcRestEarly can then drop).
    private void TickLiveWalkers()
    {
        if (HeadlessTrial.Active || slots.Length == 0)
            return;
        if (KeepAwakeCount >= SlotCount)
            return;
        if (followCamera == null || followXf == null)
            return;

        float restAt = LastRestAt;
        float flyRestAt = LastFlyRestAt;
        float quadRestAt = LastQuadRestAt;
        if (restAt <= 0.01f)
        {
            Tick(true);
            return;
        }

        if (liveWalkIndexCount < 1 && LiveCount > KeepAwakeCount)
            RebuildLiveWalkIndices();

        RestDecideLiveWalkers(followXf.position.x, restAt, flyRestAt, quadRestAt, true);
        if (LiveCount < 1)
            SyncEarlyEnabled();
    }

    private static float SlotRestAt(Slot slot, float restAt, float flyRestAt, float quadRestAt)
    {
        if (slot == null)
            return restAt;
        if (slot.tightRest)
            return flyRestAt;
        if (slot.bird != null)
            return restAt;
        return quadRestAt;
    }

    private float NearRestMargin()
    {
        return nearRestBand > 0.05f ? nearRestBand : 0.5f;
    }

    private int PhysicsRestBodyFloor()
    {
        return physicsRestMinBodies > 1 ? physicsRestMinBodies : 8;
    }

    // Live walkers only. Bind arrays, no Find. fromPhysics writes
    // PhysicsLiveWalkCount; LateUpdate still-cam uses the same park.
    private void RestDecideLiveWalkers(
        float camX,
        float restAt,
        float flyRestAt,
        float quadRestAt,
        bool fromPhysics)
    {
        HuntBeakPollArmed = false;
        if (fromPhysics)
            PhysicsLiveWalkCount = 0;
        int visited = liveWalkIndexCount;
        int liveFlockOwned = 0;
        for (int k = 0; k < liveWalkIndexCount; k++)
        {
            int i = liveWalkIndices[k];
            if (i < 0 || i >= slots.Length)
                continue;
            Slot slot = slots[i];
            if (slot == null || slot.root == null)
                continue;
            if (slot.keepAwake || slot.resting)
                continue;

            float slotRestAt = SlotRestAt(slot, restAt, flyRestAt, quadRestAt);
            float dx = Mathf.Abs(ProbeX(slot) - camX);
            if (dx > slotRestAt)
            {
                ApplyRest(slot, true);
                liveWalkRestDirty = true;
                RestingCount++;
                liveWalkIndexCount--;
                liveWalkIndices[k] = liveWalkIndices[liveWalkIndexCount];
                k--;
            }
            else
            {
                // Same-step look-park when Early is already armed for a
                // heavy walker. World spawn hens do not arm Early.
                ParkLiveHenDrive(slot, dx, LastHalfView);
                if (slot.flockOwned)
                    liveFlockOwned++;
            }
        }

        if (fromPhysics)
            PhysicsLiveWalkCount = visited;
        LiveFlockOwnedCount = liveFlockOwned;
        if (liveFlockOwned > 0 || LiveFlockDriveCount > 0)
            ApplyFlockDrives(liveFlockOwned > 0);
    }

    private void RecountNearRest(float camX, float restAt, float flyRestAt, float quadRestAt)
    {
        float margin = NearRestMargin();
        int floor = PhysicsRestBodyFloor();
        int near = 0;
        int heavy = 0;
        for (int k = 0; k < liveWalkIndexCount; k++)
        {
            int i = liveWalkIndices[k];
            if (i < 0 || i >= slots.Length)
                continue;
            Slot slot = slots[i];
            if (slot == null || slot.root == null || slot.keepAwake || slot.resting)
                continue;
            float slotRestAt = SlotRestAt(slot, restAt, flyRestAt, quadRestAt);
            float dx = Mathf.Abs(ProbeX(slot) - camX);
            if (dx <= slotRestAt - margin)
                continue;
            near++;
            if (slot.physicsBodyCount >= floor)
                heavy++;
        }

        NearRestWalkCount = near;
        NearRestHeavyCount = heavy;
    }

    private void Tick(bool recountBodies)
    {
        if (HeadlessTrial.Active || slots.Length == 0)
        {
            HuntBeakPollArmed = false;
            FarRestSkipCount = 0;
            liveWalkIndexCount = 0;
            RestingDriveOffCount = 0;
            LiveHenDriveOffCount = 0;
            LiveHenLoopOffCount = 0;
            LiveHenUnsimCount = 0;
            LiveHenDriveSkipCount = 0;
            LiveHenHuntParkCount = 0;
            NearRestWalkCount = 0;
            NearRestHeavyCount = 0;
            SyncEarlyEnabled();
            return;
        }
        if (KeepAwakeCount >= SlotCount)
        {
            HuntBeakPollArmed = false;
            FarRestSkipCount = 0;
            liveWalkIndexCount = 0;
            RestingDriveOffCount = 0;
            LiveHenDriveOffCount = 0;
            LiveHenLoopOffCount = 0;
            LiveHenUnsimCount = 0;
            LiveHenDriveSkipCount = 0;
            LiveHenHuntParkCount = 0;
            NearRestWalkCount = 0;
            NearRestHeavyCount = 0;
            SyncEarlyEnabled();
            return;
        }

        // Camera is bound once from SetupCameraFollow. Do not Camera.main
        // every LateUpdate — that is a scene lookup on the 60 Hz path.
        if (followCamera == null || followXf == null)
        {
            HuntBeakPollArmed = false;
            FarRestSkipCount = 0;
            liveWalkIndexCount = 0;
            LiveHenDriveOffCount = 0;
            LiveHenLoopOffCount = 0;
            LiveHenUnsimCount = 0;
            LiveHenDriveSkipCount = 0;
            LiveHenHuntParkCount = 0;
            NearRestWalkCount = 0;
            NearRestHeavyCount = 0;
            SyncEarlyEnabled();
            return;
        }

        float camX = followXf.position.x;
        float aspect = followCamera.aspect > 0.1f ? followCamera.aspect : 16f / 9f;
        float minAspect = minViewAspect > 0.5f ? minViewAspect : 16f / 9f;
        if (aspect < minAspect)
            aspect = minAspect;
        float halfView = followCamera.orthographicSize * aspect;
        // Rest follows the visible half-width, not a 18 m floor. Max(restX,
        // view) kept off-screen NPCs live out to 18 m on a tight camera.
        // A zoomed flock overview grows this with ortho, so in-frame birds
        // stay Dynamic. restX − wakeX sizes the hen hysteresis (default 4 m).
        float restAt = halfView + viewMargin;
        float hysteresis = Mathf.Max(3f, restX - wakeX);
        float wakeAt = restAt - hysteresis;
        float minWake = halfView + 1f;
        if (wakeAt < minWake)
            wakeAt = minWake;
        if (wakeAt >= restAt)
            wakeAt = restAt * 0.75f;
        // Flyers do not need the hen 4 m approach. Level1 seats the
        // apple flock around x=0; player −8, halfView ≈ 8.9, right edge
        // ≈ +0.9. A 0.35 m fly band still kept x=1.2 live (0.31 m past
        // the edge). Rest as soon as the 0.14 m body is off the frame;
        // wake a few centimetres before the root would appear.
        float flyMargin = flyViewMargin > 0.02f ? flyViewMargin : 0.10f;
        float flyRestAt = halfView + flyMargin;
        float flyWakeAt = halfView + Mathf.Max(0.03f, flyMargin * 0.4f);
        if (flyWakeAt >= flyRestAt)
            flyWakeAt = halfView + flyMargin * 0.4f;
        // Dogs / hedgehog: leftover sit does not need the hen approach.
        // Wake still sits 1 m before the clip (minWake) so walking into
        // the square thaws the dogs before they appear. Rest 1.5 m past
        // the edge — a 4 m hen band left the Level1 hedgehog (18 Dynamic)
        // live while already off-camera.
        float quadMargin = quadViewMargin > 0.4f ? quadViewMargin : 1.5f;
        float quadRestAt = halfView + quadMargin;
        float quadWakeAt = minWake;
        if (quadWakeAt >= quadRestAt)
            quadRestAt = quadWakeAt + 0.4f;
        LastCamX = camX;
        LastHalfView = halfView;
        LastRestAt = restAt;
        LastWakeAt = wakeAt;
        LastFlyRestAt = flyRestAt;
        LastFlyWakeAt = flyWakeAt;
        LastQuadRestAt = quadRestAt;
        LastQuadWakeAt = quadWakeAt;

        // Leftover sit cannot wake until CameraFollow moves. Skip the
        // 18-body dog island walk on a still camera. Live walkers rest
        // here (flock hens) so NpcRestEarly is not a hero-island tax.
        if (recountBodies && CanSkipFarIsland(camX, halfView))
        {
            RestDecideLiveWalkers(camX, restAt, flyRestAt, quadRestAt, false);
            if (!liveWalkRestDirty)
            {
                FarRestTickSkipCount++;
                ParkLiveHenDrives(camX, halfView);
                RecountNearRest(camX, restAt, flyRestAt, quadRestAt);
                if (liveWalkIndexCount > 0 || keepAwakeIndexCount > 0)
                    RecountLiveOnly();
                RecountLiveHenDriveOff();
                HuntBeakPollArmed = false;
                // Leftover sit cannot spawn insects or thaw the pack.
                // World spawn: InsectCount 0, flock already frozen.
                if (InsectCount > 0)
                    RecountInsects();
                if (LiveFlockOwnedCount > 0 || LiveFlockDriveCount > 0)
                    ApplyFlockDrives(LiveFlockOwnedCount > 0);
                SyncEarlyEnabled();
                return;
            }
            // A hen parked: latch island totals below.
        }

        liveWalkRestDirty = false;
        decidedCamX = camX;
        decidedHalfView = halfView;
        restTotalsLatched = true;

        int resting = 0;
        int liveTight = 0;
        int liveDynamic = 0;
        int liveKinematic = 0;
        int restBodies = 0;
        int restDogs = 0;
        int restFlyers = 0;
        int liveHens = 0;
        int restHens = 0;
        int liveHenDriveOff = 0;
        int liveHenLoopOff = 0;
        int liveHenUnsim = 0;
        int liveHenDriveSkip = 0;
        int liveHenHuntPark = 0;
        int restHogs = 0;
        int liveHogs = 0;
        int liveFlockOwned = 0;
        int restLoop = 0;
        int restUnsim = 0;
        int restHunt = 0;
        int huntCache = 0;
        int liveCcd = 0;
        int restCcd = 0;
        int restCcdActive = 0;
        int restColOff = 0;
        int restJointOff = 0;
        int farSkip = 0;
        int driveOff = 0;
        int nearRest = 0;
        int nearHeavy = 0;
        for (int i = 0; i < slots.Length; i++)
        {
            Slot slot = slots[i];
            if (slot == null || slot.root == null)
                continue;

            float slotRestAt;
            float slotWakeAt;
            if (slot.tightRest)
            {
                slotRestAt = flyRestAt;
                slotWakeAt = flyWakeAt;
            }
            else if (slot.bird != null)
            {
                slotRestAt = restAt;
                slotWakeAt = wakeAt;
            }
            else
            {
                slotRestAt = quadRestAt;
                slotWakeAt = quadWakeAt;
            }
            float dx = Mathf.Abs(ProbeX(slot) - camX);
            // Far leftover sit cannot wake until dx drops into the
            // wake band. Skip the island walk (18-body dogs) until then.
            // Live walkers and the 1 m approach still decide every Tick.
            bool farRest = !slot.keepAwake && slot.resting && dx > slotWakeAt + 1f;
            bool wantRest;
            if (slot.keepAwake)
                wantRest = false;
            else if (farRest)
                wantRest = true;
            else
                wantRest = slot.resting ? dx > slotWakeAt : dx > slotRestAt;
            if (wantRest != slot.resting)
                ApplyRest(slot, wantRest);
            if (!slot.resting && !slot.keepAwake)
                ParkLiveHenDrive(slot, dx, halfView);
            if (slot.huntBeak != null)
                huntCache++;
            if (slot.resting)
            {
                resting++;
                restBodies += slot.physicsBodyCount;
                restCcd += slot.restCcdLatchedCached;
                if (recountBodies)
                {
                    // Ordinary still-cam: far leftover keeps the rest-edge
                    // latch (no 18-body walk). Capture forces a fresh
                    // LatchRestIsland so npcRestCcdNow cannot stay 0 on a
                    // Continuous leak that arrived after the rest edge.
                    if (farRest && !captureIslandRecount)
                        farSkip++;
                    else
                        LatchRestIsland(slot);
                    restUnsim += slot.restUnsimCached;
                    restCcdActive += slot.restCcdActiveCached;
                    restColOff += slot.restColOffCached;
                    restJointOff += slot.restJointOffCached;
                }
                if (slot.loop != null)
                    restLoop += slot.loop.Length;
                if (slot.dog != null)
                    restDogs++;
                if (slot.tightRest)
                    restFlyers++;
                if (slot.bird != null && !slot.tightRest)
                    restHens++;
                if (slot.hedgehog != null)
                    restHogs++;
                // Far leftover sit already latched HuntBeak / Dog look.
                // Do not poll .enabled on 18-body dogs every LateUpdate.
                // Capture polls once so npcRestDriveOff matches RestingCount.
                bool huntOff;
                bool driveOffSlot;
                if (farRest && !captureIslandRecount)
                {
                    huntOff = slot.restHuntOffCached;
                    driveOffSlot = slot.restDriveOffCached;
                }
                else
                {
                    huntOff = HuntBeakIsResting(slot);
                    driveOffSlot = DriveIsOff(slot);
                    if (captureIslandRecount)
                    {
                        slot.restHuntOffCached = huntOff;
                        slot.restDriveOffCached = driveOffSlot;
                    }
                }

                if (huntOff)
                    restHunt++;
                if (driveOffSlot)
                    driveOff++;
            }
            else
            {
                if (recountBodies)
                {
                    CountLiveBodyTypes(slot, out int dyn, out int kin, out int ccd);
                    liveDynamic += dyn;
                    liveKinematic += kin;
                    liveCcd += ccd;
                }
                if (slot.tightRest)
                    liveTight++;
                if (slot.flockOwned)
                    liveFlockOwned++;
                // Hens use the walker band (not tightRest). Scene Chicken keepAwake
                // is the followed hen, not this World pack count.
                if (slot.bird != null && !slot.tightRest)
                {
                    liveHens++;
                    if (slot.bird.enabled == false)
                        liveHenDriveOff++;
                    if (slot.lookParked)
                    {
                        liveHenLoopOff++;
                        if (LookParkProbeIsUnsim(slot))
                            liveHenUnsim++;
                        if (LookParkDriveSkipIsOn(slot))
                            liveHenDriveSkip++;
                        if (HuntBeakIsParked(slot))
                            liveHenHuntPark++;
                    }
                }
                if (slot.hedgehog != null)
                    liveHogs++;
                if (!slot.keepAwake)
                {
                    float margin = NearRestMargin();
                    if (dx > slotRestAt - margin)
                    {
                        nearRest++;
                        if (slot.physicsBodyCount >= PhysicsRestBodyFloor())
                            nearHeavy++;
                    }
                }
            }
        }

        if (recountBodies)
            FarRestSkipCount = farSkip;
        RestingCount = resting;
        LiveTightCount = liveTight;
        LiveFlockOwnedCount = liveFlockOwned;
        if (recountBodies)
        {
            LiveDynamicBodies = liveDynamic;
            LiveKinematicBodies = liveKinematic;
            LiveCcdBodies = liveCcd;
        }
        RestingBodies = restBodies;
        RestingCcdBodies = restCcd;
        if (recountBodies)
        {
            RestingActiveCcdBodies = restCcdActive;
            RestingColliderOffCount = restColOff;
            RestingJointOffCount = restJointOff;
            RestingUnsimulatedBodies = restUnsim;
        }
        else
            RestingUnsimulatedBodies = restBodies;
        RestingLoopCount = restLoop;
        RestingDogCount = restDogs;
        RestingFlyerCount = restFlyers;
        LiveHenCount = liveHens;
        RestingHenCount = restHens;
        LiveHenDriveOffCount = liveHenDriveOff;
        LiveHenLoopOffCount = liveHenLoopOff;
        LiveHenUnsimCount = liveHenUnsim;
        LiveHenDriveSkipCount = liveHenDriveSkip;
        LiveHenHuntParkCount = liveHenHuntPark;
        RestingHedgehogCount = restHogs;
        LiveHedgehogCount = liveHogs;
        HuntBeakPollArmed = false;
        HuntBeakCacheCount = huntCache;
        RestingHuntBeakCount = restHunt;
        RestingDriveOffCount = driveOff;
        NearRestWalkCount = nearRest;
        NearRestHeavyCount = nearHeavy;
        RecountInsects();
        // Freeze the pack brain on the physics Tick so BirdFlockDrive.Update
        // does not ForceFly a Kinematic leftover Sit the same frame.
        // Leftover-mode stamp stays LateUpdate (after a still-live pack mate).
        ApplyFlockDrives(liveFlockOwned > 0);
        // Pack brain frozen: leftover Sit cannot be ForceFly'd.
        if (recountBodies && liveFlockOwned > 0)
            HoldRestingFlockModes();
        RebuildLiveWalkIndices();
        SyncEarlyEnabled();
    }

    // Still camera: leftover sit cannot wake. Do not walk 18-body
    // dogs every LateUpdate. Dog / Bird with every slot keepAwake
    // already leave Tick before this. Chicken leftover hens used
    // to miss the skip (KeepAwakeCount 1). Camera motion wakes
    // them; a still lens cannot. New Absorb slots clear the latch.
    private bool CanSkipFarIsland(float camX, float halfView)
    {
        if (!restTotalsLatched || liveWalkRestDirty)
            return false;
        if (Mathf.Abs(camX - decidedCamX) > 0.05f)
            return false;
        if (Mathf.Abs(halfView - decidedHalfView) > 0.05f)
            return false;
        return true;
    }

    // Resting island totals stay latched. Live walkers can still
    // change Dynamic/CCD as they walk; leftover sit cannot.
    // keepAwake (followed Chicken hen) is Bind-cached, not leftover.
    private void RecountLiveOnly()
    {
        int liveTight = 0;
        int liveDynamic = 0;
        int liveKinematic = 0;
        int liveCcd = 0;
        int liveHens = 0;
        int liveHogs = 0;
        int liveFlockOwned = 0;
        for (int k = 0; k < liveWalkIndexCount; k++)
        {
            int i = liveWalkIndices[k];
            if (i < 0 || i >= slots.Length)
                continue;
            Slot slot = slots[i];
            if (slot == null || slot.root == null || slot.keepAwake || slot.resting)
                continue;
            AddLiveIsland(slot, ref liveTight, ref liveDynamic, ref liveKinematic,
                ref liveCcd, ref liveHens, ref liveHogs, ref liveFlockOwned);
        }

        for (int k = 0; k < keepAwakeIndexCount; k++)
        {
            int i = keepAwakeIndices[k];
            if (i < 0 || i >= slots.Length)
                continue;
            Slot slot = slots[i];
            if (slot == null || slot.root == null || slot.resting)
                continue;
            AddLiveIsland(slot, ref liveTight, ref liveDynamic, ref liveKinematic,
                ref liveCcd, ref liveHens, ref liveHogs, ref liveFlockOwned);
        }

        LiveTightCount = liveTight;
        LiveFlockOwnedCount = liveFlockOwned;
        LiveDynamicBodies = liveDynamic;
        LiveKinematicBodies = liveKinematic;
        LiveCcdBodies = liveCcd;
        LiveHenCount = liveHens;
        LiveHedgehogCount = liveHogs;
    }

    private static void AddLiveIsland(
        Slot slot,
        ref int liveTight,
        ref int liveDynamic,
        ref int liveKinematic,
        ref int liveCcd,
        ref int liveHens,
        ref int liveHogs,
        ref int liveFlockOwned)
    {
        CountLiveBodyTypes(slot, out int dyn, out int kin, out int ccd);
        liveDynamic += dyn;
        liveKinematic += kin;
        liveCcd += ccd;
        if (slot.tightRest)
            liveTight++;
        if (slot.flockOwned)
            liveFlockOwned++;
        if (slot.bird != null && !slot.tightRest)
            liveHens++;
        if (slot.hedgehog != null)
            liveHogs++;
    }

    // Bind once. Physics Tick must not new[] when a hen parks.
    private void EnsureLiveWalkBuffer()
    {
        int n = slots != null ? slots.Length : 0;
        if (liveWalkIndices != null && liveWalkIndices.Length >= n)
            return;
        liveWalkIndices = n > 0 ? new int[n] : System.Array.Empty<int>();
    }

    private void EnsureKeepAwakeBuffer()
    {
        int n = slots != null ? slots.Length : 0;
        if (keepAwakeIndices != null && keepAwakeIndices.Length >= n)
            return;
        keepAwakeIndices = n > 0 ? new int[n] : System.Array.Empty<int>();
    }

    // LateUpdate source of truth. keepAwake and leftover sit stay off
    // this list so NpcRestEarly does not walk 18-body dogs.
    private void RebuildLiveWalkIndices()
    {
        EnsureLiveWalkBuffer();
        int w = 0;
        if (slots == null)
        {
            liveWalkIndexCount = 0;
            return;
        }

        for (int i = 0; i < slots.Length; i++)
        {
            Slot slot = slots[i];
            if (slot == null || slot.root == null)
                continue;
            if (slot.keepAwake || slot.resting)
                continue;
            liveWalkIndices[w++] = i;
        }

        liveWalkIndexCount = w;
    }

    // Off-clip hens in the walker band: drop BirdController /
    // ActuatorDriver / rest loop without full rest. World x=12
    // stays Dynamic. Sit-only park restored the drive as soon as
    // Wander wrote Walk. Park any mode past the half-view.
    // BirdDrive stays on so forage can still write Walk; the next
    // LateUpdate turns the controller back on in frame. HuntBeak
    // parks with the drive so leftover Peck NeverSleep solid does
    // not sit on the hero island (world-root, not in slot.bodies). Loop drop does not rewrite Bind flags
    // (look-park then energyRest must still wake BodyAero).
    // Probe unsim leaves Simulate without Kinematic. Sleep on
    // dirt was a no-op (Ground wakes StartAwake). Not energyRest.
    // Sensors / flight stay enabled; drivenExternally skips their
    // own FixedUpdate (PERF-5).
    private void ParkLiveHenDrives(float camX, float halfView)
    {
        for (int k = 0; k < liveWalkIndexCount; k++)
        {
            int i = liveWalkIndices[k];
            if (i < 0 || i >= slots.Length)
                continue;
            Slot slot = slots[i];
            if (slot == null || slot.root == null)
                continue;
            ParkLiveHenDrive(slot, Mathf.Abs(ProbeX(slot) - camX), halfView);
        }
    }

    private static void ParkLiveHenDrive(Slot slot, float dx, float halfView)
    {
        if (slot == null || slot.resting || slot.keepAwake || slot.bird == null)
            return;
        if (slot.tightRest)
            return;
        ApplyLookPark(slot, dx > halfView);
    }

    // Drive + loop + unsim. Idempotent. Bind loopEnabled stays so
    // a later energyRest wake still restores BodyAero. Full rest
    // must unpark first so rest latches simulated=true, not the
    // look-park unsim.
    private static void ApplyLookPark(Slot slot, bool park)
    {
        if (slot == null)
            return;
        if (slot.lookParked == park)
        {
            if (park)
            {
                if (HuntBeakNeedsPark(slot))
                    RestHuntBeak(slot, true);
                HoldLookParkBody(slot);
                HoldLookParkDriveSkip(slot);
            }
            return;
        }

        slot.lookParked = park;
        if (park)
        {
            if (slot.bird != null && slot.bird.enabled)
                slot.bird.enabled = false;
            if (slot.actuators != null && slot.actuators.enabled)
                slot.actuators.enabled = false;
            SetLookParkLoop(slot, true);
            SetLookParkDriveSkip(slot, true);
            SetLookParkBody(slot, true);
            if (HuntBeakNeedsPark(slot))
                RestHuntBeak(slot, true);
            return;
        }

        SetLookParkBody(slot, false);
        SetLookParkDriveSkip(slot, false);
        SetLookParkLoop(slot, false);
        if (slot.bird != null)
            slot.bird.enabled = slot.birdEnabled;
        if (slot.actuators != null)
            slot.actuators.enabled = slot.actuatorsEnabled;
    }

    private static void SetLookParkLoop(Slot slot, bool park)
    {
        if (slot == null || slot.loop == null)
            return;
        Behaviour[] loop = slot.loop;
        bool[] enabled = slot.loopEnabled;
        for (int i = 0; i < loop.Length; i++)
        {
            Behaviour b = loop[i];
            if (b == null)
                continue;
            if (park)
                b.enabled = false;
            else if (enabled != null && i < enabled.Length)
                b.enabled = enabled[i];
        }
    }

    // Flock hen is one Dynamic body. simulated=false drops it from
    // Simulate without Kinematic (contract: npcLiveHens stay Dynamic).
    // Sleep() on dirt does not stick: Ground contact wakes StartAwake
    // every step. Pin Transform first — unsim aliases pose to that
    // Transform, and dirt spawn would pull ProbeX onto the hero.
    // Interpolate.None while parked; Bind interpolation comes back.
    private static void SetLookParkBody(Slot slot, bool park)
    {
        if (slot == null)
            return;
        Rigidbody2D rb = slot.probeRb;
        if (rb == null || rb.bodyType != RigidbodyType2D.Dynamic)
            return;
        if (park)
        {
            slot.lookParkProbeX = rb.simulated ? rb.position.x : ReadLiveProbeX(slot);
            slot.lookParkSimulated = rb.simulated;
            PinPhysicsPose(rb);
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.interpolation = RigidbodyInterpolation2D.None;
            if (rb.simulated)
                rb.simulated = false;
            return;
        }

        rb.simulated = slot.lookParkSimulated;
        RestoreProbeInterpolation(slot);
        if (rb.simulated)
            rb.WakeUp();
    }

    // Already look-parked: dirt Sleep used to leak simulated=true.
    // Unsim again without rewriting the wake latch.
    private static void HoldLookParkBody(Slot slot)
    {
        if (slot == null || !slot.lookParked)
            return;
        Rigidbody2D rb = slot.probeRb;
        if (rb == null || rb.bodyType != RigidbodyType2D.Dynamic)
            return;
        if (!rb.simulated)
            return;
        PinPhysicsPose(rb);
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        rb.interpolation = RigidbodyInterpolation2D.None;
        rb.simulated = false;
    }

    private static void SetLookParkDriveSkip(Slot slot, bool park)
    {
        if (slot == null)
            return;
        if (slot.sensors != null)
        {
            if (park)
            {
                slot.lookParkSensorsDriven = slot.sensors.drivenExternally;
                slot.sensors.drivenExternally = true;
            }
            else
                slot.sensors.drivenExternally = slot.lookParkSensorsDriven;
        }

        if (slot.flight != null)
        {
            if (park)
            {
                slot.lookParkFlightDriven = slot.flight.drivenExternally;
                slot.flight.drivenExternally = true;
            }
            else
                slot.flight.drivenExternally = slot.lookParkFlightDriven;
        }
    }

    private static void HoldLookParkDriveSkip(Slot slot)
    {
        if (slot == null || !slot.lookParked)
            return;
        if (slot.sensors != null && !slot.sensors.drivenExternally)
            slot.sensors.drivenExternally = true;
        if (slot.flight != null && !slot.flight.drivenExternally)
            slot.flight.drivenExternally = true;
    }

    private static bool LookParkProbeIsUnsim(Slot slot)
    {
        if (slot == null)
            return false;
        Rigidbody2D rb = slot.probeRb;
        return rb != null
            && rb.bodyType == RigidbodyType2D.Dynamic
            && !rb.simulated;
    }

    private static bool LookParkDriveSkipIsOn(Slot slot)
    {
        if (slot == null)
            return false;
        if (slot.sensors != null && !slot.sensors.drivenExternally)
            return false;
        if (slot.flight != null && !slot.flight.drivenExternally)
            return false;
        return slot.sensors != null || slot.flight != null;
    }

    private static void RestoreProbeInterpolation(Slot slot)
    {
        Rigidbody2D rb = slot.probeRb;
        if (rb == null || slot.bodies == null || slot.interpolation == null)
            return;
        Rigidbody2D[] bodies = slot.bodies;
        RigidbodyInterpolation2D[] interpolation = slot.interpolation;
        int n = bodies.Length < interpolation.Length ? bodies.Length : interpolation.Length;
        for (int i = 0; i < n; i++)
        {
            if (bodies[i] != rb)
                continue;
            rb.interpolation = interpolation[i];
            return;
        }
    }

    private void RecountLiveHenDriveOff()
    {
        int n = 0;
        int loopOff = 0;
        int unsim = 0;
        int driveSkip = 0;
        int huntPark = 0;
        for (int k = 0; k < liveWalkIndexCount; k++)
        {
            int i = liveWalkIndices[k];
            if (i < 0 || i >= slots.Length)
                continue;
            Slot slot = slots[i];
            if (slot == null || slot.root == null || slot.keepAwake || slot.resting)
                continue;
            if (slot.bird == null || slot.tightRest)
                continue;
            if (slot.bird.enabled == false)
                n++;
            if (slot.lookParked)
            {
                loopOff++;
                if (LookParkProbeIsUnsim(slot))
                    unsim++;
                if (LookParkDriveSkipIsOn(slot))
                    driveSkip++;
                if (HuntBeakIsParked(slot))
                    huntPark++;
            }
        }

        LiveHenDriveOffCount = n;
        LiveHenLoopOffCount = loopOff;
        LiveHenUnsimCount = unsim;
        LiveHenDriveSkipCount = driveSkip;
        LiveHenHuntParkCount = huntPark;
    }

    // Species + actuator + Dog look, not sensors / flight.
    // Dog.LateUpdate has no energyRest gate: leftover sit must
    // disable the Dog behaviour or 16 face Apply* stay on the
    // 60 Hz list after stance is already off.
    private static bool DriveIsOff(Slot slot)
    {
        if (slot == null || !slot.resting)
            return false;
        if (slot.bird != null && slot.bird.enabled)
            return false;
        if (slot.dog != null && slot.dog.enabled)
            return false;
        if (slot.dogBody != null && slot.dogBody.enabled)
            return false;
        if (slot.hedgehog != null && slot.hedgehog.enabled)
            return false;
        if (slot.actuators != null && slot.actuators.enabled)
            return false;
        return true;
    }

    private static void ApplyRest(Slot slot, bool rest)
    {
        if (rest && !slot.resting)
            slot.restProbeX = slot.lookParked ? slot.lookParkProbeX : ReadLiveProbeX(slot);
        // Full rest owns the island. Unpark first so rest latches
        // simulated=true / drivenExternally from Bind, not the
        // look-park unsim. Look-park reapplies after wake if the
        // hen is still past the clip.
        if (rest && slot.lookParked)
            ApplyLookPark(slot, false);
        slot.resting = rest;
        if (slot.dog != null)
        {
            slot.dog.energyRest = rest;
            // Drive is disabled below, so its energyRest EndWalk never
            // runs. Leftover sit, not a frozen Walk wish on wake.
            if (rest)
            {
                slot.dog.EndWalk();
                slot.dog.ClearIdle();
            }
        }
        if (slot.hedgehog != null)
        {
            slot.hedgehog.energyRest = rest;
            if (rest)
            {
                slot.hedgehog.EndWalk();
                slot.hedgehog.ClearCrawlGoal();
            }
        }
        if (slot.actuators != null)
            slot.actuators.energyRest = rest;
        if (slot.bird != null)
        {
            slot.bird.energyRest = rest;
            if (rest)
                slot.modeAtRest = slot.bird.mode;
            else
                slot.bird.mode = slot.modeAtRest;
        }
        if (slot.intent != null)
        {
            if (rest)
            {
                slot.intentEnabled = slot.intent.enabled;
                slot.intent.enabled = false;
            }
            else
                slot.intent.enabled = slot.intentEnabled;
        }

        // World-root Kinematic HuntBeak: park + unsim, keep the
        // collider. A leftover Body-child trigger still disables.
        // BakeHuntSlab owns simulated and the dirt pose on wake.
        RestHuntBeak(slot, rest);

        // Off-camera: drop the script from the 200 Hz / LateUpdate lists.
        // energyRest still gates any leftover caller (BeakStrike, Bite).
        // Do not enabled=false on BirdSensors / BirdFlight (PERF-5).
        SetDriveEnabled(slot, !rest);
        SetLoopEnabled(slot, !rest);

        // Skip own FixedUpdate on sensors / flight without enabled=false
        // (PERF-5). Flock already sets drivenExternally; ragdoll does not,
        // and the controller return would leave those ticks running.
        if (slot.sensors != null)
        {
            if (rest)
            {
                slot.sensorsDriven = slot.sensors.drivenExternally;
                slot.sensors.drivenExternally = true;
            }
            else
                slot.sensors.drivenExternally = slot.sensorsDriven;
        }
        if (slot.flight != null)
        {
            if (rest)
            {
                slot.flightDriven = slot.flight.drivenExternally;
                slot.flight.drivenExternally = true;
            }
            else
                slot.flight.drivenExternally = slot.flightDriven;
        }

        Rigidbody2D[] bodies = slot.bodies;
        for (int i = 0; i < bodies.Length; i++)
        {
            Rigidbody2D rb = bodies[i];
            if (rb == null || rb.bodyType == RigidbodyType2D.Static)
                continue;
            if (rest)
            {
                slot.bodyTypes[i] = rb.bodyType;
                if (slot.simulated != null && i < slot.simulated.Length)
                    slot.simulated[i] = rb.simulated;
                if (slot.bodyPositions != null && i < slot.bodyPositions.Length)
                    slot.bodyPositions[i] = rb.position;
                if (slot.bodyRotations != null && i < slot.bodyRotations.Length)
                    slot.bodyRotations[i] = rb.rotation;
                if (slot.detection != null && i < slot.detection.Length)
                    slot.detection[i] = rb.collisionDetectionMode;
                PinPhysicsPose(rb);
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
                rb.interpolation = RigidbodyInterpolation2D.None;
                // Discrete while still Dynamic: leftover Paw CCD is a
                // 200 Hz island cost. Write again after Kinematic so a
                // type change does not restore Continuous. unsim after,
                // so wake can restore the latched mode.
                rb.collisionDetectionMode = CollisionDetectionMode2D.Discrete;
                rb.bodyType = RigidbodyType2D.Kinematic;
                rb.collisionDetectionMode = CollisionDetectionMode2D.Discrete;
                // Card rest type is Kinematic. simulated=false then
                // drops leftover sit from Physics2D.Simulate so the
                // hero island stays 200 Hz without 18 hog contacts.
                // Transform is already pinned: unsim must not alias
                // position back to dirt spawn.
                rb.simulated = false;
            }
            else
            {
                // Leftover world HuntBeak RB (pre-compound). Child
                // HuntBeak has no Rigidbody2D, so this skip is idle.
                if (IsHuntBeak(rb))
                {
                    rb.simulated = false;
                    rb.interpolation = RigidbodyInterpolation2D.None;
                    continue;
                }

                // simulated first: a false body ignores bodyType writes
                // until it re-enters the solver.
                if (slot.simulated != null && i < slot.simulated.Length)
                    rb.simulated = slot.simulated[i];
                else
                    rb.simulated = true;
                if (slot.bodyPositions != null && i < slot.bodyPositions.Length)
                    rb.position = slot.bodyPositions[i];
                if (slot.bodyRotations != null && i < slot.bodyRotations.Length)
                    rb.rotation = slot.bodyRotations[i];
                PinPhysicsPose(rb);
                RigidbodyType2D resume = slot.bodyTypes[i];
                if (resume == RigidbodyType2D.Static)
                    resume = RigidbodyType2D.Dynamic;
                rb.bodyType = resume;
                if (slot.detection != null && i < slot.detection.Length)
                    rb.collisionDetectionMode = slot.detection[i];
                rb.interpolation = slot.interpolation[i];
                if (resume == RigidbodyType2D.Dynamic)
                    rb.WakeUp();
            }
        }

        // Joints then colliders: Discrete already stuck while Dynamic.
        // Off-camera leftover must not keep a hinge island or stay in
        // the broadphase. HuntBeak stays off on wake (BakeHuntSlab).
        // Not sensors, not BirdFlight.
        SetJointsEnabled(slot, !rest);
        SetCollidersEnabled(slot, !rest);
        if (rest)
        {
            LatchRestIsland(slot);
            slot.restDriveOffCached = DriveIsOff(slot);
            slot.restHuntOffCached = HuntBeakIsResting(slot);
        }
    }

    // Bind arrays, no Find. Rest edge plus near-wake Tick.
    // Far leftover sit keeps the last latch.
    private static void LatchRestIsland(Slot slot)
    {
        if (slot == null)
            return;
        slot.restUnsimCached = CountUnsimulatedBodies(slot);
        if (slot.huntBeakRb != null && !slot.huntBeakRb.simulated)
            slot.restUnsimCached++;
        slot.restCcdLatchedCached = CountLatchedCcd(slot);
        int activeCcd = CountActiveCcd(slot);
        if (activeCcd > 0)
        {
            ForceRestDiscrete(slot);
            activeCcd = CountActiveCcd(slot);
        }
        slot.restCcdActiveCached = activeCcd;
        slot.restColOffCached = CountColliderOff(slot);
        if (CountJointOn(slot) > 0)
            ForceRestJointsOff(slot);
        slot.restJointOffCached = CountJointOff(slot);
    }

    // Rest removes the driver from Unity's player loop. Wake restores
    // the Bind-time enabled flag. Sensors / flight stay enabled.
    private static void SetDriveEnabled(Slot slot, bool on)
    {
        if (slot == null)
            return;
        if (slot.bird != null)
        {
            if (!on)
            {
                // Look-park may already have disabled the hen controller.
                // Do not latch false or wake restores a dead drive.
                if (slot.bird.enabled)
                    slot.birdEnabled = true;
                slot.bird.enabled = false;
            }
            else
                slot.bird.enabled = slot.birdEnabled;
        }

        if (slot.dog != null)
        {
            if (!on)
            {
                if (slot.dog.enabled)
                    slot.dogEnabled = true;
                slot.dog.enabled = false;
            }
            else
                slot.dog.enabled = slot.dogEnabled;
        }

        if (slot.dogBody != null)
        {
            if (!on)
            {
                if (slot.dogBody.enabled)
                    slot.dogBodyEnabled = true;
                slot.dogBody.enabled = false;
            }
            else
                slot.dogBody.enabled = slot.dogBodyEnabled;
        }

        if (slot.hedgehog != null)
        {
            if (!on)
            {
                if (slot.hedgehog.enabled)
                    slot.hedgehogEnabled = true;
                slot.hedgehog.enabled = false;
            }
            else
                slot.hedgehog.enabled = slot.hedgehogEnabled;
        }

        if (slot.actuators != null)
        {
            if (!on)
            {
                if (slot.actuators.enabled)
                    slot.actuatorsEnabled = true;
                slot.actuators.enabled = false;
            }
            else
                slot.actuators.enabled = slot.actuatorsEnabled;
        }
    }

    // Rest removes Muscle / JointFriction / BodyAero / strike /
    // TreeSpring from the 200 Hz and Update lists. Wake restores
    // Bind-time enabled. Sensors / BirdFlight are not in this list
    // (PERF-5).
    private static void SetLoopEnabled(Slot slot, bool on)
    {
        if (slot == null || slot.loop == null)
            return;
        Behaviour[] loop = slot.loop;
        bool[] enabled = slot.loopEnabled;
        for (int i = 0; i < loop.Length; i++)
        {
            Behaviour b = loop[i];
            if (b == null)
                continue;
            if (!on)
            {
                // Look-park already set enabled=false. Do not latch that
                // over Bind true or wake leaves BodyAero dead.
                if (b.enabled && enabled != null && i < enabled.Length)
                    enabled[i] = true;
                b.enabled = false;
            }
            else if (enabled != null && i < enabled.Length)
                b.enabled = enabled[i];
        }
    }

    // Rest disables Bind-latched colliders. Wake restores except HuntBeak.
    private static void SetCollidersEnabled(Slot slot, bool on)
    {
        if (slot == null || slot.colliders == null)
            return;
        Collider2D[] colliders = slot.colliders;
        bool[] enabled = slot.colliderEnabled;
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider2D col = colliders[i];
            if (col == null)
                continue;
            if (IsHuntBeakCollider(col))
            {
                // RestHuntBeak / Bird ParkHuntSlab own this fixture.
                // Disable was the 1×1 origin miss on the next Peck.
                continue;
            }

            if (!on)
            {
                if (enabled != null && i < enabled.Length)
                    enabled[i] = col.enabled;
                col.enabled = false;
            }
            else if (enabled != null && i < enabled.Length)
                col.enabled = enabled[i];
        }
    }

    // Rest disables Bind-latched joints. Wake restores Bind-time enabled.
    private static void SetJointsEnabled(Slot slot, bool on)
    {
        if (slot == null || slot.joints == null)
            return;
        Joint2D[] joints = slot.joints;
        bool[] enabled = slot.jointEnabled;
        for (int i = 0; i < joints.Length; i++)
        {
            Joint2D joint = joints[i];
            if (joint == null)
                continue;
            if (!on)
            {
                if (enabled != null && i < enabled.Length)
                    enabled[i] = joint.enabled;
                joint.enabled = false;
            }
            else if (enabled != null && i < enabled.Length)
                joint.enabled = enabled[i];
        }
    }

    // Live bill: Dynamic is the 200 Hz island. In-frame Kinematic Sit
    // still pays BirdController until rest (LiveTightCount). Resting
    // slots drop the driver from the player loop (SetDriveEnabled).
    // World-root HuntBeak is not a child of the bird, so it is not
    // in slot.bodies. A leftover simulated query would otherwise
    // leave npcLiveBodies at 0 (hero island, monitor-blind).
    private static void CountLiveBodyTypes(
        Slot slot,
        out int dynamicBodies,
        out int kinematicBodies,
        out int ccdBodies)
    {
        dynamicBodies = 0;
        kinematicBodies = 0;
        ccdBodies = 0;
        if (slot == null)
            return;
        if (slot.bodies != null)
        {
            Rigidbody2D[] bodies = slot.bodies;
            for (int i = 0; i < bodies.Length; i++)
            {
                Rigidbody2D rb = bodies[i];
                if (rb == null)
                    continue;
                if (!rb.simulated)
                    continue;
                AddLiveBody(rb, ref dynamicBodies, ref kinematicBodies, ref ccdBodies);
            }
        }

        Rigidbody2D hunt = slot.huntBeakRb;
        if (hunt != null && hunt.simulated && !ContainsBody(slot.bodies, hunt))
            AddLiveBody(hunt, ref dynamicBodies, ref kinematicBodies, ref ccdBodies);
    }

    private static void AddLiveBody(
        Rigidbody2D rb,
        ref int dynamicBodies,
        ref int kinematicBodies,
        ref int ccdBodies)
    {
        if (rb.bodyType == RigidbodyType2D.Dynamic)
            dynamicBodies++;
        else if (rb.bodyType == RigidbodyType2D.Kinematic)
            kinematicBodies++;
        if (rb.bodyType != RigidbodyType2D.Static
            && rb.collisionDetectionMode == CollisionDetectionMode2D.Continuous)
            ccdBodies++;
    }

    private static bool ContainsBody(Rigidbody2D[] bodies, Rigidbody2D hunt)
    {
        if (bodies == null || hunt == null)
            return false;
        for (int i = 0; i < bodies.Length; i++)
        {
            if (bodies[i] == hunt)
                return true;
        }

        return false;
    }

    // Resting CCD bill from the latched mode, not the Discrete write.
    // Bind arrays, no per-step GetComponents.
    private static int CountLatchedCcd(Slot slot)
    {
        if (slot == null || slot.detection == null || slot.bodies == null)
            return 0;
        CollisionDetectionMode2D[] detection = slot.detection;
        Rigidbody2D[] bodies = slot.bodies;
        int n = detection.Length < bodies.Length ? detection.Length : bodies.Length;
        int ccd = 0;
        for (int i = 0; i < n; i++)
        {
            if (detection[i] != CollisionDetectionMode2D.Continuous)
                continue;
            Rigidbody2D rb = bodies[i];
            if (rb == null || rb.bodyType == RigidbodyType2D.Static)
                continue;
            ccd++;
        }

        return ccd;
    }

    // Current Continuous on a resting slot, not the latched array.
    // Discrete + collider-off must keep this 0.
    private static int CountActiveCcd(Slot slot)
    {
        if (slot == null || slot.bodies == null)
            return 0;
        Rigidbody2D[] bodies = slot.bodies;
        int ccd = 0;
        for (int i = 0; i < bodies.Length; i++)
        {
            Rigidbody2D rb = bodies[i];
            if (rb == null || rb.bodyType == RigidbodyType2D.Static)
                continue;
            if (rb.collisionDetectionMode == CollisionDetectionMode2D.Continuous)
                ccd++;
        }

        return ccd;
    }

    private static void ForceRestDiscrete(Slot slot)
    {
        if (slot == null || slot.bodies == null)
            return;
        Rigidbody2D[] bodies = slot.bodies;
        for (int i = 0; i < bodies.Length; i++)
        {
            Rigidbody2D rb = bodies[i];
            if (rb == null || rb.bodyType == RigidbodyType2D.Static)
                continue;
            if (rb.collisionDetectionMode == CollisionDetectionMode2D.Continuous)
                rb.collisionDetectionMode = CollisionDetectionMode2D.Discrete;
        }
    }

    private static int CountColliderOff(Slot slot)
    {
        if (slot == null || slot.colliders == null)
            return 0;
        Collider2D[] colliders = slot.colliders;
        int n = 0;
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null && !colliders[i].enabled)
                n++;
        }

        return n;
    }

    private static int CountJointOn(Slot slot)
    {
        if (slot == null || slot.joints == null)
            return 0;
        Joint2D[] joints = slot.joints;
        int n = 0;
        for (int i = 0; i < joints.Length; i++)
        {
            if (joints[i] != null && joints[i].enabled)
                n++;
        }

        return n;
    }

    private static int CountJointOff(Slot slot)
    {
        if (slot == null || slot.joints == null)
            return 0;
        Joint2D[] joints = slot.joints;
        int n = 0;
        for (int i = 0; i < joints.Length; i++)
        {
            if (joints[i] != null && !joints[i].enabled)
                n++;
        }

        return n;
    }

    private static void ForceRestJointsOff(Slot slot)
    {
        if (slot == null || slot.joints == null)
            return;
        Joint2D[] joints = slot.joints;
        for (int i = 0; i < joints.Length; i++)
        {
            Joint2D joint = joints[i];
            if (joint != null && joint.enabled)
                joint.enabled = false;
        }
    }

    private static bool IsHuntBeakCollider(Collider2D col)
    {
        return col != null && col.gameObject != null && col.gameObject.name == HuntBeakName;
    }

    // autoSyncTransforms is off. Rigidbody2D.position is the chest /
    // Body for Dynamic walkers and for Kinematic Sit after PlaceOnSlot.
    // Transform.position can still sit on the dirt spawn, and rest then
    // keeps a far flock live (or a nearby Sit frozen). Static is picture.
    // Resting slots use the latched pose: simulated=false aliases
    // Rigidbody2D.position to that stale Transform (BIRD-12 HuntBeak).
    private static float ProbeX(Slot slot)
    {
        if (slot == null)
            return 0f;
        if (slot.resting)
            return slot.restProbeX;
        if (slot.lookParked)
            return slot.lookParkProbeX;
        return ReadLiveProbeX(slot);
    }

    private static float ReadLiveProbeX(Slot slot)
    {
        if (slot == null)
            return 0f;
        Rigidbody2D rb = slot.probeRb;
        if (rb != null && rb.simulated && rb.bodyType != RigidbodyType2D.Static)
            return rb.position.x;
        Transform probe = slot.probe != null ? slot.probe : slot.root;
        return probe != null ? probe.position.x : 0f;
    }

    // World-root name from BirdController.EnsureHuntSlab. Park Y
    // matches HuntBeakParkY there — park, do not disable.
    private const string HuntBeakName = "HuntBeak";
    private const float HuntBeakParkY = 80f;

    // Bind / Start only. Play Bird / Chicken have one Insect; World and
    // Level1 have none. Do not FindObjectsByType on the frame path.
    private void LatchInsects(Transform known)
    {
        Insect[] found = FindScene<Insect>();
        int n = found != null ? found.Length : 0;
        if (known != null)
            n++;
        if (n < 1)
        {
            insects = System.Array.Empty<Transform>();
            InsectCount = 0;
            return;
        }

        Transform[] raw = new Transform[n];
        int w = 0;
        if (known != null)
            raw[w++] = known;
        if (found != null)
        {
            for (int i = 0; i < found.Length; i++)
            {
                Insect bug = found[i];
                if (bug == null)
                    continue;
                Transform t = bug.transform;
                if (ContainsTransform(raw, w, t))
                    continue;
                raw[w++] = t;
            }
        }

        if (w == raw.Length)
            insects = raw;
        else
        {
            insects = new Transform[w];
            for (int i = 0; i < w; i++)
                insects[i] = raw[i];
        }

        InsectCount = insects.Length;
    }

    private static bool ContainsTransform(Transform[] list, int written, Transform t)
    {
        if (t == null)
            return true;
        for (int i = 0; i < written; i++)
        {
            if (list[i] == t)
                return true;
        }

        return false;
    }

    private void RecountInsects()
    {
        int n = 0;
        for (int i = 0; i < insects.Length; i++)
        {
            if (insects[i] != null)
                n++;
        }

        InsectCount = n;
    }

    private static bool IsHuntBeak(Rigidbody2D rb)
    {
        return rb != null && rb.gameObject != null && rb.gameObject.name == HuntBeakName;
    }

    // Bind / AbsorbScene only. HuntBeak exists from BirdController.Awake.
    // Body-child leftover first, then world-root Kinematic pairing.
    // Previous sibling of a new HuntBeak is the bird that Awoke it.
    // Tick does not walk roots.
    private void LatchAllHuntBeaks()
    {
        if (HeadlessTrial.Active || slots == null)
            return;
        for (int i = 0; i < slots.Length; i++)
            LatchHuntBeakChild(slots[i]);
        LatchWorldHuntBeaks();
    }

    private void LatchHuntBeakChild(Slot slot)
    {
        if (slot == null || slot.huntBeak != null || slot.bird == null)
            return;
        Transform probe = slot.probe != null ? slot.probe : slot.root;
        if (probe == null)
            return;
        HuntBeakScanCount++;
        for (int i = 0; i < probe.childCount; i++)
        {
            Transform child = probe.GetChild(i);
            if (child == null || child.name != HuntBeakName)
                continue;
            slot.huntBeak = child.GetComponent<BoxCollider2D>();
            slot.huntBeakRb = child.GetComponent<Rigidbody2D>();
            return;
        }
    }

    // Bind / Start / AbsorbScene only. Pair each world-root HuntBeak
    // to the bird that created it (previous sibling). Unmatched
    // leftovers unsim. Tick does not call this.
    private void LatchWorldHuntBeaks()
    {
        Scene scene = gameObject.scene;
        if (!scene.IsValid() || !scene.isLoaded)
            return;
        HuntBeakScanCount++;
        scene.GetRootGameObjects(rootScratch);
        Transform prev = null;
        for (int i = 0; i < rootScratch.Count; i++)
        {
            GameObject go = rootScratch[i];
            if (go == null)
            {
                prev = null;
                continue;
            }

            if (go.name == HuntBeakName && !HuntBeakOwned(go))
            {
                Slot owner = OwnerForHuntBeak(prev);
                if (owner != null)
                    BindHuntBeak(owner, go);
            }

            prev = go.transform;
        }

        for (int i = 0; i < rootScratch.Count; i++)
        {
            GameObject go = rootScratch[i];
            if (go == null || go.name != HuntBeakName || HuntBeakOwned(go))
                continue;
            Slot owner = FirstBirdMissingHunt();
            if (owner == null || !BindHuntBeak(owner, go))
                RestOrphanHuntBeak(go);
        }
    }

    private bool HuntBeakOwned(GameObject go)
    {
        if (go == null || slots == null)
            return false;
        for (int i = 0; i < slots.Length; i++)
        {
            Slot slot = slots[i];
            if (slot != null && slot.huntBeak != null
                && slot.huntBeak.gameObject == go)
                return true;
        }

        return false;
    }

    private Slot OwnerForHuntBeak(Transform prev)
    {
        Slot slot = FindSlotByRoot(prev);
        if (slot == null || slot.bird == null || slot.huntBeak != null)
            return null;
        return slot;
    }

    private Slot FindSlotByRoot(Transform root)
    {
        if (root == null || slots == null)
            return null;
        for (int i = 0; i < slots.Length; i++)
        {
            Slot slot = slots[i];
            if (slot != null && slot.root == root)
                return slot;
        }

        return null;
    }

    private Slot FirstBirdMissingHunt()
    {
        if (slots == null)
            return null;
        for (int i = 0; i < slots.Length; i++)
        {
            Slot slot = slots[i];
            if (slot != null && slot.bird != null && slot.huntBeak == null)
                return slot;
        }

        return null;
    }

    private static bool BindHuntBeak(Slot slot, GameObject go)
    {
        if (slot == null || go == null || slot.huntBeak != null)
            return false;
        BoxCollider2D col = go.GetComponent<BoxCollider2D>();
        if (col == null)
            return false;
        slot.huntBeak = col;
        slot.huntBeakRb = go.GetComponent<Rigidbody2D>();
        return true;
    }

    private static void RestOrphanHuntBeak(GameObject go)
    {
        if (go == null)
            return;
        ParkHuntBeak(go.GetComponent<BoxCollider2D>());
    }

    // Rest: park like Bird ParkHuntSlab (unsim first, Kinematic,
    // StartAwake, trigger, FreezeAll, Rigidbody2D.position at ParkY).
    // Wake: leave unsim — BakeHuntSlab owns live simulated.
    // Origin+offset leftover sat on the hero island if simulated
    // flipped. Body-child leftover still disables (no world-root RB).
    private static void RestHuntBeak(Slot slot, bool rest)
    {
        if (slot == null)
            return;
        if (!rest)
            return;
        if (slot.huntBeakRb != null)
        {
            ParkHuntBeak(slot.huntBeak);
            return;
        }

        if (slot.huntBeak != null)
            slot.huntBeak.enabled = false;
    }

    private static bool HuntBeakIsResting(Slot slot)
    {
        if (slot == null || slot.huntBeak == null)
            return false;
        if (slot.huntBeakRb != null)
            return !slot.huntBeakRb.simulated;
        return !slot.huntBeak.enabled;
    }

    // Look-park: leftover Peck is Dynamic NeverSleep solid, or
    // a park that still carries Default include (pre-05:10 Bird,
    // or Bake before drive-off). Unsim alone is not parked —
    // Hold must catch sleepMode / trigger / include leak. Bind
    // cache, not Find. Skip a finished rest park so LateUpdate
    // does not rewrite ParkY.
    private static bool HuntBeakNeedsPark(Slot slot)
    {
        if (slot == null || slot.huntBeak == null)
            return false;
        return !HuntBeakIsParked(slot);
    }

    // Rest park after live Peck / Bird ParkHuntSlab. Unsim is not
    // enough: NeverSleep solid at ParkY is a 200 Hz island if
    // simulated leaks. Default include after a park that skipped
    // Clear (or Bake before drive-off) is the same leak. World-root
    // RB is not in slot.bodies.
    private static bool HuntBeakIsParked(Slot slot)
    {
        if (slot == null || slot.huntBeak == null)
            return false;
        if (slot.huntBeakRb != null)
        {
            Rigidbody2D rb = slot.huntBeakRb;
            if (rb.simulated)
                return false;
            if (rb.bodyType != RigidbodyType2D.Kinematic)
                return false;
            if (rb.sleepMode == RigidbodySleepMode2D.NeverSleep)
                return false;
            if (!slot.huntBeak.isTrigger)
                return false;
            if (rb.includeLayers.value != 0)
                return false;
            return slot.huntBeak.includeLayers.value == 0;
        }

        return !slot.huntBeak.enabled;
    }

    // Match Bird ParkHuntSlab pose, then drop contact masks.
    // Unsim first so ParkY does not bake into a simulated fixture
    // (1×1 origin miss). Physics park is Rigidbody2D.position at
    // (0, ParkY), not origin+offset — leftover origin pose sat on
    // the hero island if simulated flipped. Live Peck is Dynamic
    // NeverSleep solid — park is Kinematic, StartAwake, trigger.
    // Include Nothing so a simulated leak is not Default×Default
    // (BakeHuntSlab restores Default on live Peck). Do not Destroy.
    // Collider stays on.
    private static void ParkHuntBeak(BoxCollider2D col)
    {
        if (col == null)
            return;
        Rigidbody2D rb = col.attachedRigidbody;
        Vector2 park = new Vector2(0f, HuntBeakParkY);
        if (rb != null && rb.simulated)
            rb.simulated = false;
        if (rb != null)
        {
            if (rb.bodyType != RigidbodyType2D.Kinematic)
                rb.bodyType = RigidbodyType2D.Kinematic;
            rb.useFullKinematicContacts = true;
            rb.sleepMode = RigidbodySleepMode2D.StartAwake;
            rb.constraints = RigidbodyConstraints2D.FreezeAll;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.interpolation = RigidbodyInterpolation2D.None;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Discrete;
            rb.position = park;
            rb.rotation = 0f;
        }

        if (!col.isTrigger)
            col.isTrigger = true;

        Transform t = col.transform;
        t.SetPositionAndRotation(
            new Vector3(park.x, park.y, t.position.z),
            Quaternion.identity);
        if ((col.offset - park).sqrMagnitude > 0.0000001f)
            col.offset = park;
        if (col.gameObject.layer != 0)
            col.gameObject.layer = 0;
        ClearHuntBeakContactMask(col, rb);
        if (!col.enabled)
            col.enabled = true;
    }

    // Rest belt, not live Peck Rel Hostile. Matches Bird
    // ClearHuntQueryLayers. Unity 6 ANDs RB include/exclude with
    // the collider; leftover Default include on a simulated leak
    // is a 200 Hz island. No NameToLayer (masks are 0 / all).
    private static void ClearHuntBeakContactMask(BoxCollider2D col, Rigidbody2D rb)
    {
        if (col != null)
        {
            if (col.includeLayers.value != 0)
                col.includeLayers = 0;
            if (col.excludeLayers.value != Physics2D.AllLayers)
                col.excludeLayers = Physics2D.AllLayers;
        }

        if (rb == null)
            return;
        if (rb.includeLayers.value != 0)
            rb.includeLayers = 0;
        if (rb.excludeLayers.value != Physics2D.AllLayers)
            rb.excludeLayers = Physics2D.AllLayers;
    }

    // Write Transform to the physics pose before simulated=false.
    // No Physics2D.SyncTransforms — that is the whole scene (BIRD-12).
    private static void PinPhysicsPose(Rigidbody2D rb)
    {
        if (rb == null)
            return;
        Transform t = rb.transform;
        if (t == null)
            return;
        Vector2 p = rb.position;
        t.SetPositionAndRotation(
            new Vector3(p.x, p.y, t.position.z),
            Quaternion.Euler(0f, 0f, rb.rotation));
    }

    private static int CountUnsimulatedBodies(Slot slot)
    {
        int n = 0;
        if (slot == null || slot.bodies == null)
            return 0;
        Rigidbody2D[] bodies = slot.bodies;
        for (int i = 0; i < bodies.Length; i++)
        {
            Rigidbody2D rb = bodies[i];
            if (rb == null || rb.bodyType == RigidbodyType2D.Static)
                continue;
            if (!rb.simulated)
                n++;
        }

        return n;
    }

    // LateUpdate is after BirdFlockDrive (−50). Stamp leftover mode so a
    // still-live pack mate cannot ForceFly a Kinematic rest slot.
    private void HoldRestingFlockModes()
    {
        for (int i = 0; i < slots.Length; i++)
        {
            Slot slot = slots[i];
            if (slot == null || !slot.resting || !slot.flockOwned || slot.bird == null)
                continue;
            if (slot.bird.mode != slot.modeAtRest)
                slot.bird.mode = slot.modeAtRest;
        }
    }

    // Bind / Start only. World wander pack has no BirdDrive on the root;
    // BirdFlockDrive.Update would still ForceFly a Kinematic leftover Sit.
    // Disable when every flock-owned flyer rests. A live woodpecker
    // (BirdDrive) is not this gate. Hens are walkers, not this gate.
    private void CaptureFlocks(BirdFlockDrive known)
    {
        BirdFlockDrive[] found = FindScene<BirdFlockDrive>();
        int n = found != null ? found.Length : 0;
        bool knownMissing = known != null;
        if (knownMissing && found != null)
        {
            for (int i = 0; i < found.Length; i++)
            {
                if (found[i] == known)
                {
                    knownMissing = false;
                    break;
                }
            }
        }

        if (knownMissing)
            n++;
        if (n < 1)
        {
            flocks = System.Array.Empty<BirdFlockDrive>();
            flockWasEnabled = System.Array.Empty<bool>();
            LiveFlockDriveCount = 0;
            return;
        }

        flocks = new BirdFlockDrive[n];
        flockWasEnabled = new bool[n];
        int w = 0;
        if (found != null)
        {
            for (int i = 0; i < found.Length; i++)
            {
                BirdFlockDrive flock = found[i];
                if (flock == null)
                    continue;
                flocks[w] = flock;
                flockWasEnabled[w] = flock.enabled;
                w++;
            }
        }

        if (knownMissing)
        {
            flocks[w] = known;
            flockWasEnabled[w] = known.enabled;
            w++;
        }

        if (w == flocks.Length)
            return;

        BirdFlockDrive[] slim = new BirdFlockDrive[w];
        bool[] enabled = new bool[w];
        for (int i = 0; i < w; i++)
        {
            slim[i] = flocks[i];
            enabled[i] = flockWasEnabled[i];
        }

        flocks = slim;
        flockWasEnabled = enabled;
    }

    private void ApplyFlockDrives(bool anyLiveFlockOwned)
    {
        int live = 0;
        for (int i = 0; i < flocks.Length; i++)
        {
            BirdFlockDrive flock = flocks[i];
            if (flock == null)
                continue;
            if (!anyLiveFlockOwned)
            {
                if (flock.enabled)
                {
                    flockWasEnabled[i] = true;
                    flock.enabled = false;
                }
            }
            else if (flockWasEnabled[i])
                flock.enabled = true;
            if (flock.enabled)
                live++;
        }

        LiveFlockDriveCount = live;
    }
}

// Physics-step rest, before sensors / control / muscles. NpcRest itself
// stays at 110 so LateUpdate still runs after CameraFollow.
[DefaultExecutionOrder(-120)]
public sealed class NpcRestEarly : MonoBehaviour
{
    [System.NonSerialized]
    public NpcRest rest;

    void FixedUpdate()
    {
        if (rest != null)
            rest.TickFromPhysics();
    }
}
