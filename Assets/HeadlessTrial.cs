using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

// Run one human without the editor window, for numerical balance analysis.
// Activated by the -trial argument on the built player's command line:
//   BalanceTrial.exe -batchmode -nographics -trial -duration 10 -label base -logFile -
//   -fixedDelta 0.005 — physics step (0.02 = 50 Hz, 0.005 = 200 Hz)
//   -activationSpeed 10 — muscle force rise speed
//   -pushImpulse 20 -pushTime 5 — torso push (N·s, + forward) at second 5
//   -crouch 1 -crouchTime 5 -crouchHold 10 — crouch target via MotionIntent
//   -lean 1|-1 -leanTime 5 -leanHold 10 — trunk lean (+ forward, − back)
//   -standLeg left|right|0 -standLegTime 5 -standLegHold 10 — one-leg stance
//   -dog 1 — run a dog instead of a human (humanCount = 0, flat ground)
//   -dogLeap 1 -dogLeapTime 2 -dogPreyX 1.4 — leap attack after settle, prey mark
    //   -dogWalk 1 -dogWalkTime 1 — clocked trot after settle (not human -walk)
//   -flower 1 — garden bed (picture only, no hinges). -flowerKind rose|…
//   -flowerCount 12 — how many plants in the row
//   -tree 1 — tree (can pair with -bird: birds on branches)
//   -treeKind oak|pine|willow|bush|poplar — crown recipe (default oak)
//   -treeSeed 1 — local fork Random
//   -treePerch 1 — seat birds on PlantTree.GetPerchSlots
//   -treeWind 0 — no sail (check that branches hold the pose)
//   -bird 1 — run a bird instead of a human (human flags are not read)
//   -birdMode stand|fly|walk|glide|sit|attack -takeoffDelay 0 — stance with no takeoff
//   -birdRig ragdoll|flock — ragdoll for stance, flock for a pack (one body)
//   -birdFacing 1|-1 — nose along +X or −X (flock: picture on Look)
    //   -birdKind crow|chicken — body recipe (chicken: ground, not cruise)
    //   -birdDrive wander|prey — brain writes mode (flock: wander + -birdCount)
    //   -birdThreat 8 — threat mark ahead, m (0 — none; cry + Fly, not Attack)
    //   -threatRange 12 — “sees threat” range, m
//   -glideLead / -sitMin / -sitMax / -flyMin / -flyMax / -flockLeash / -aiStride
//     / -homeFaceDeadzone — wander timing, flock brain stride, Glide nose deadzone at the slot
//   -birdDriveEach 1 — N× BirdDrive (per bird) instead of one BirdFlockDrive
//   -flapHz / -flapAmp / -hover — flap and mean thrust (fractions of weight)
//   -stopFlap 4 — drop the flap at second 4: default is glide and land
//   -stopFlapMode glide|stand — glide (shallow landing) or stand (stone drop)
//   -walk 1 -walkTime 5 -walkDuration 30 -walkStance 10 -walkTransfer 8 -walkFirst right
//   -run 1 -runTime 5 -runHold 20 — run (shorter Stance), with -walk or alone
//   -swingHip / -swingKnee — swing flexion (hip minus, knee plus)
//   -swingHipUnload — swing flexor-bias while grounded (signal fraction)
//   -pelvisP / -pelvisD / -pelvisRef — pelvis PD to the world vertical
//   -crouchPelvis — target pelvis tilt forward at crouch=1
//   -neckFriction / -shoulderFriction / -elbowFriction / -wristFriction —
//   absolute K of passive viscosity before BuildHuman (head shares neckFriction)
//   -neckMuscle — neck and head muscle torque, absolute, after -muscle
   //   -neckP / -neckD / -neckTilt / -neckRef — neck and head PD to the world vertical
   //   -shoulderP / -elbowP / -wristP and paired D/Base/Ref — arm joint pose
   //   -armShoulderBal / -armElbowBal / -armWristBal — CoM counter-reach (deg/unit)
   // All balance coefficients can be overridden by arguments without rebuilding the player,
// so one build serves any parameter sweep.
public class HeadlessTrial : MonoBehaviour
{
    // GameProcess checks this flag and does not build its own 50 humans,
    // or the run would drown in 700 Rigidbody2D.
    public static bool Active { get; private set; }

    // Safety cap: if physics hangs, the process must not live forever.
    private const float REAL_TIME_LIMIT_SECONDS = 600f;
    private static readonly WaitForFixedUpdate FixedStep = new WaitForFixedUpdate();

    private float duration = 10f;
    private string label = "trial";
    private string outputFolder;
    private float startY = -0.82f;

    // Torso push: checks whether the controller holds a disturbance, not only
    // itself. Without it, equilibrium may be just a static pose.
    private float pushTime;
    private float pushImpulse;
    private Rigidbody2D torsoBody;

    // Crouch via MotionIntent, not the keyboard: batchmode has none.
    private float crouchTarget;
    private float crouchTime;
    private float crouchHold;
    private float leanTarget;
    private float leanTime;
    private float leanHold;
    private MotionIntent intent;

    // One-leg stance: −1 left support, +1 right, 0 both. Same as crouch —
    // the intent is written every step so live input cannot erase the script.
    private float standLegTarget;
    private float standLegTime;
    private float standLegHold;

    // Support swap: reuse StepPhaseDriver, not SIMBICON.
    private float walkTarget;
    private float walkTime;
    private float walkDuration;
    private float walkStanceDuration;
    private float runTarget;
    private float runTime;
    private float runHold;
    private float runStanceDuration;
    private float runTransferMaxDuration;
    private float walkHeelStrikeMinAge = 0f;
    private float walkHeelAirMin = 0f;
    private float walkComTrigger = 0f;
    private float walkComTriggerMinAge = 0.35f;
    private float walkComTriggerPerSpeed = 0f;
    private float walkCadenceStanceGain = 0f;
    private float walkCadenceMinStance = 1.0f;
    private float walkCadenceTransferGain = 0f;
    private float walkCadenceMinTransfer = 0.5f;
    private float walkTransferMinDuration = 0.7f;
    private float walkTransferMaxDuration;
    private float walkTransferComMax;
    private float walkTransferFallbackComMax;
    private float walkTransferLevelMax;
    private float walkFirstStance;
    private StepPhaseDriver stepDriver;

    private Human human;
    private TrialRecorder recorder;
    private TrialRunInfo runInfo;
    private bool spawnBird;
    private bool spawnTree;
    private bool spawnFlower;
    private bool treePerch;
    private PlantTree plantTree;
    private PlantTrialRecorder plantRecorder;
    private PlantFlower[] trialFlowers;
    private FlowerTrialRecorder flowerRecorder;
    private bool spawnDog;
    private Dog dog;
    private DogTrialRecorder dogRecorder;
    private bool dogLeap;
    private float dogLeapTime = 2f;
    private float dogPreyX = 1.4f;
    private bool dogWalk;
    private float dogWalkTime = 1f;
    private Bird bird;
    private Bird[] trialBirds;
    private BirdTrialRecorder birdRecorder;
    private Rigidbody2D birdBody;
    private float stopFlapTime;
    private string stopFlapMode;
    private string birdDriveKind;
    private BirdKind trialBirdKind = BirdKind.Crow;
    private int birdDriveCount = 1;
    private int birdDriveSeed = 1;
    private const float BirdPackSpacing = 1.2f;

    [Serializable]
    private class BuildManifestDto
    {
        public string fingerprint;
        public string algorithm;
        public string unityVersion;
        public string unityPath;
        public string buildTimeUtc;
        public bool repositoryPresent;
        public bool gitAvailable;
        public string gitCommit;
        public bool gitDirty;
        public string buildGuid;
        public string outputPath;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        string[] args = Environment.GetCommandLineArgs();
        if (Array.IndexOf(args, "-trial") < 0) return;

        Active = true;

        ConfigureDeterministicPhysics();

        // Solver floor — CLI sweep of cycle 5 only; production stays 8/3.
        int velocityIterations = Mathf.RoundToInt(GetFloatArg("-velocityIterations", Physics2D.velocityIterations));
        int positionIterations = Mathf.RoundToInt(GetFloatArg("-positionIterations", Physics2D.positionIterations));
        if (velocityIterations > 0)
            Physics2D.velocityIterations = velocityIterations;
        if (positionIterations > 0)
            Physics2D.positionIterations = positionIterations;

        // Set the physics step before the scene loads: otherwise the first FixedUpdate
        // still runs at the default 0.02 s, and frequency comparisons go crooked.
        float fixedDelta = GetFloatArg("-fixedDelta", Time.fixedDeltaTime);
        if (fixedDelta > 0f)
            Time.fixedDeltaTime = fixedDelta;

        GameObject host = new GameObject("HeadlessTrial");
        DontDestroyOnLoad(host);
        host.AddComponent<HeadlessTrial>();
    }

    // The stand compares runs bitwise: without this, PhysX 2D on the edge of
    // contacts (right support) makes the same exe give fell / not fell.
    private static void ConfigureDeterministicPhysics()
    {
        UnityEngine.Random.InitState(0);

        PhysicsJobOptions2D options = Physics2D.jobOptions;
        options.useMultithreading = false;
        options.useConsistencySorting = true;
        Physics2D.jobOptions = options;
    }

    private void Start()
    {
        duration = GetFloatArg("-duration", 10f);
        label = GetStringArg("-label", "trial");
        outputFolder = GetStringArg("-out", DefaultOutputFolder());
        spawnBird = GetFloatArg("-bird", 0f) > 0.5f;
        spawnTree = GetFloatArg("-tree", 0f) > 0.5f;
        spawnFlower = GetFloatArg("-flower", 0f) > 0.5f;
        treePerch = GetFloatArg("-treePerch", spawnTree && spawnBird ? 1f : 0f) > 0.5f;
        birdDriveKind = GetStringArg("-birdDrive", "").Trim().ToLowerInvariant();
        trialBirdKind = Bird.ParseKind(GetStringArg("-birdKind", "crow"));
        birdDriveCount = Mathf.Max(1, Mathf.RoundToInt(GetFloatArg("-birdCount", 1f)));
        birdDriveSeed = Mathf.RoundToInt(GetFloatArg("-birdDriveSeed", 1f));
        spawnDog = GetFloatArg("-dog", 0f) > 0.5f;
        dogLeap = GetFloatArg("-dogLeap", 0f) > 0.5f;
        dogLeapTime = GetFloatArg("-dogLeapTime", 2f);
        dogPreyX = GetFloatArg("-dogPreyX", 1.4f);
        dogWalk = GetFloatArg("-dogWalk", 0f) > 0.5f;
        dogWalkTime = GetFloatArg("-dogWalkTime", 1f);
        if (spawnDog && spawnBird)
        {
            Debug.LogWarning("HeadlessTrial: -dog and -bird both set; running dog.");
            spawnBird = false;
        }
        stopFlapTime = GetFloatArg("-stopFlap", 0f);
        stopFlapMode = GetStringArg("-stopFlapMode", "glide").Trim().ToLowerInvariant();
        // The bird sits lower than the human: −0.82 would leave it a metre in the air.
        // Dog: paws at y = −2.0, root at the chest centre.
        startY = spawnDog
            ? GetFloatArg("-startY", Dog.StandingRootY(-2f))
            : spawnBird
                ? GetFloatArg("-startY", Bird.KindStandingRootY(trialBirdKind, -2f))
                : GetFloatArg("-startY", -0.82f);
        pushImpulse = GetFloatArg("-pushImpulse", 0f);
        pushTime = GetFloatArg("-pushTime", 5f);
        crouchTarget = Mathf.Clamp01(GetFloatArg("-crouch", 0f));
        crouchTime = GetFloatArg("-crouchTime", 5f);
        crouchHold = GetFloatArg("-crouchHold", 10f);
        leanTarget = Mathf.Clamp(GetFloatArg("-lean", 0f), -1f, 1f);
        leanTime = GetFloatArg("-leanTime", 5f);
        leanHold = GetFloatArg("-leanHold", 10f);
        standLegTarget = ParseStandLegArg(GetStringArg("-standLeg", "0"));
        standLegTime = GetFloatArg("-standLegTime", 5f);
        standLegHold = GetFloatArg("-standLegHold", 10f);

        walkTarget = Mathf.Clamp01(GetFloatArg("-walk", 0f));
        walkTime = GetFloatArg("-walkTime", 5f);
        walkDuration = GetFloatArg("-walkDuration", 30f);
        walkStanceDuration = GetFloatArg("-walkStance", 6f);
        runTarget = Mathf.Clamp01(GetFloatArg("-run", 0f));
        runTime = GetFloatArg("-runTime", 5f);
        runHold = GetFloatArg("-runHold", 20f);
        runStanceDuration = GetFloatArg("-runStance", 4f);
        runTransferMaxDuration = GetFloatArg("-runTransfer", 4f);
        walkHeelStrikeMinAge = GetFloatArg("-walkHeelMin", 0f);
        walkHeelAirMin = GetFloatArg("-walkHeelAir", 0f);
        walkComTrigger = GetFloatArg("-walkComTrigger", 0.01f);
        walkComTriggerMinAge = GetFloatArg("-walkComTrigAge", 0.25f);
        walkComTriggerPerSpeed = GetFloatArg("-walkComTriggerPerSpeed", 0f);
        walkCadenceStanceGain = GetFloatArg("-walkCadenceStanceGain", 0f);
        walkCadenceMinStance = GetFloatArg("-walkCadenceMinStance", 1.0f);
        walkCadenceTransferGain = GetFloatArg("-walkCadenceTransferGain", 0f);
        walkCadenceMinTransfer = GetFloatArg("-walkCadenceMinTransfer", 0.5f);
        walkFirstStance = ParseStandLegArg(GetStringArg("-walkFirst", "right"));
        walkTransferMaxDuration = GetFloatArg("-walkTransfer", 6f);
        walkTransferMinDuration = GetFloatArg("-walkTransferMin", 0.7f);
        walkTransferComMax = GetFloatArg("-walkTransferCom", 0.10f);
        walkTransferFallbackComMax = GetFloatArg("-walkTransferFallbackCom", 0.12f);
        walkTransferLevelMax = GetFloatArg("-walkTransferLevel", 0.05f);
        if (Mathf.Abs(walkFirstStance) < 0.5f)
            walkFirstStance = 1f;

        string provenanceError;
        if (!TryAcceptProvenance(out runInfo, out provenanceError))
        {
            Debug.LogError("TRIAL_PROVENANCE_FAILED " + provenanceError);
            Application.Quit(3);
            return;
        }

        BuildTrialScene();

        // After the scene is built: the PhysX world already exists, so jobOptions must be pinned again.
        ConfigureDeterministicPhysics();

        StartCoroutine(RunTrial());
    }

    // ─── MINIMAL SCENE BUILD: GROUND + ONE HUMAN OR BIRD ───
    private void BuildTrialScene()
    {
        GameObject groundHost = new GameObject("TrialGround");
        GroundBuilder ground = groundHost.AddComponent<GroundBuilder>();
        bool wideBird = spawnBird && (birdDriveKind == "wander" || birdDriveCount > 1);
        float packSpan = Mathf.Max(0, birdDriveCount - 1) * BirdPackSpacing;
        float birdGroundWidth = Mathf.Max(48f, packSpan + 16f);
        ground.groundSize = new Vector2(wideBird ? birdGroundWidth : 20f, 2f);
        ground.groundPosition = new Vector2(0f, -3f);
        ground.groundFriction = GetFloatArg("-groundFriction", ground.groundFriction);
        ground.BuildGround();

        if (spawnDog)
        {
            BuildDogScene();
            return;
        }

        if (spawnTree)
            BuildTreeScene();

        if (spawnFlower)
            BuildFlowerScene();

        if (spawnBird)
        {
            BuildBirdScene();
            if (spawnTree && plantTree != null)
            {
                Bird[] pack = trialBirds != null && trialBirds.Length > 0
                    ? trialBirds
                    : bird != null ? new[] { bird } : System.Array.Empty<Bird>();
                Bird.GhostTreeWood(pack, plantTree);
            }
            if (spawnTree && treePerch && plantTree != null && bird != null)
                SeatTrialBirdsOnTree();
            return;
        }

        if (spawnTree || spawnFlower)
            return;

        GameObject humanObject = new GameObject("TrialHuman");
        // y = −0.82 puts the feet exactly on the top of the ground (−2.0):
        // (pelvisSize.y + torsoSize.y)/2 + thigh + shin + foot
        // = 0.25 + 0.43 + 0.43 + 0.07 = 1.18.
        humanObject.transform.position = new Vector2(0f, startY);

        human = humanObject.AddComponent<Human>();
        human.footFriction = GetFloatArg("-footFriction", human.footFriction);
        // MotionIntent and StepPhaseDriver before PlayerInputSource: that one
        // has RequireComponent(StepPhaseDriver), or Unity would create a second
        // instance and the stand would Tick “ours” while metrics read the other.
        intent = humanObject.AddComponent<MotionIntent>();
        stepDriver = humanObject.AddComponent<StepPhaseDriver>();
        stepDriver.externalDrive = true;
        stepDriver.stanceDuration = walkStanceDuration;
        stepDriver.stanceHeelStrikeMinAge = walkHeelStrikeMinAge;
        stepDriver.stanceHeelAirMin = walkHeelAirMin;
        stepDriver.stanceComTrigger = walkComTrigger;
        stepDriver.stanceComMinAge = walkComTriggerMinAge;
        stepDriver.stanceComTriggerPerSpeed = walkComTriggerPerSpeed;
        stepDriver.walkCadenceStanceGain = walkCadenceStanceGain;
        stepDriver.walkCadenceMinStance = walkCadenceMinStance;
        stepDriver.transferMaxDuration = walkTransferMaxDuration;
        stepDriver.transferMinDuration = walkTransferMinDuration;
        stepDriver.walkCadenceTransferGain = walkCadenceTransferGain;
        stepDriver.walkCadenceMinTransfer = walkCadenceMinTransfer;
        stepDriver.transferComOffsetMax = walkTransferComMax;
        stepDriver.transferFallbackComMax = walkTransferFallbackComMax;
        stepDriver.transferStandLegLevelMax = walkTransferLevelMax;
        stepDriver.firstStance = walkFirstStance;
        humanObject.AddComponent<PlayerInputSource>();

        // Torques and friction are read when the body is built, so change them before BuildHuman.
        float muscleMultiplier = GetFloatArg("-muscle", 1f);
        float frictionMultiplier = GetFloatArg("-friction", 1f);
        ApplyMultipliers(human, muscleMultiplier, frictionMultiplier);
        // Absolute K after the multiplier: a diagnostic tau sweep
        // must not depend on -friction and must not touch Human defaults.
        ApplyFrictionOverrides(human);
        ApplyMuscleOverrides(human);

        human.BuildHuman();
        FactionStamp.Player(humanObject);

        // Controller gains live in BalanceController, which appears
        // inside BuildHuman, so override them after the build.
        BalanceController balance = human.GetComponent<BalanceController>();
        ApplyGains(balance);
        // The driver is already on the object before Awake. Bind closes a late-add
        // in one call, without GetComponent in FixedUpdate.
        if (balance != null)
            balance.BindStepPhaseDriver(stepDriver);

        Transform torso = human.transform.Find("Torso");
        torsoBody = torso != null ? torso.GetComponent<Rigidbody2D>() : null;
    }

    private void BuildDogScene()
    {
        GameObject dogObject = new GameObject("TrialDog");
        dog = dogObject.AddComponent<Dog>();
        ApplyDogBuildOverrides(dog);
        // Height follows the build pose, the way GameProcess does it. A
        // spawn angle changes the column drop, and the pre-build default
        // would then bury the paws or drop the dog in from the air.
        if (!HasArg("-startY"))
        {
            startY = dog.StandingRootOffset(-2f);
            // runInfo is filled by the provenance check, before any body
            // exists. Without this the summary reports a spawn height the
            // run never used, which is the defect this pass came to fix.
            if (runInfo != null)
                runInfo.StartY = startY;
        }
        dogObject.transform.position = new Vector2(0f, startY);
        dog.BuildDog();
        FactionStamp.Wolf(dogObject);
        ApplyDogGains(dog.stance);

        if (dogLeap || HasArg("-dogPreyX"))
        {
            DogPrey prey = DogPrey.Spawn(new Vector2(dogPreyX, -2f + DogPrey.DefaultHeightAboveGround));
            if (dog.stance != null && prey != null)
                dog.stance.leapTarget = prey.transform;
        }

        Transform chest = dog.transform.Find("Chest");
        torsoBody = chest != null ? chest.GetComponent<Rigidbody2D>() : null;
    }

    private static void ApplyDogBuildOverrides(Dog target)
    {
        if (target == null) return;
        float muscle = GetFloatArg("-muscle", 1f);
        float friction = GetFloatArg("-friction", 1f);
        target.lumbarMuscleTorque *= muscle;
        target.neckMuscleTorque *= muscle;
        target.tailMuscleTorque *= muscle;
        target.hipMuscleTorque *= muscle;
        target.kneeMuscleTorque *= muscle;
        target.shoulderMuscleTorque *= muscle;
        target.elbowMuscleTorque *= muscle;
        target.pawExtensorTorque *= muscle;
        target.pawFlexorTorque *= muscle;
        target.lumbarFriction *= friction;
        target.neckFriction *= friction;
        target.tailFriction *= friction;
        target.hipFriction *= friction;
        target.kneeFriction *= friction;
        target.shoulderFriction *= friction;
        target.elbowFriction *= friction;
        target.pawFriction *= friction;
        target.neckFriction = GetFloatArg("-neckFriction", target.neckFriction);
        target.hipFriction = GetFloatArg("-hipFriction", target.hipFriction);
        target.kneeFriction = GetFloatArg("-kneeFriction", target.kneeFriction);
        target.shoulderFriction = GetFloatArg("-shoulderFriction", target.shoulderFriction);
        target.elbowFriction = GetFloatArg("-elbowFriction", target.elbowFriction);
        target.pawFriction = GetFloatArg("-pawFriction", target.pawFriction);
        target.lumbarFriction = GetFloatArg("-lumbarFriction", target.lumbarFriction);
        target.lumbarMuscleTorque = GetFloatArg("-lumbarMuscle", target.lumbarMuscleTorque);
        target.neckMuscleTorque = GetFloatArg("-neckMuscle", target.neckMuscleTorque);
        target.pawExtensorTorque = GetFloatArg("-pawExtensor", target.pawExtensorTorque);
        target.pawFlexorTorque = GetFloatArg("-pawFlexor", target.pawFlexorTorque);
        // Limb rake without a rebuild. Pair each with the matching
        // -hipBase / -shoulderBase, or the pose target fights the build.
        target.spawnHipAngle = GetFloatArg("-spawnHip", target.spawnHipAngle);
        target.spawnShoulderAngle = GetFloatArg("-spawnShoulder", target.spawnShoulderAngle);
        target.spawnKneeAngle = GetFloatArg("-spawnKnee", target.spawnKneeAngle);
        target.spawnElbowAngle = GetFloatArg("-spawnElbow", target.spawnElbowAngle);
    }

    private static void ApplyDogGains(DogStanceController c)
    {
        if (c == null)
        {
            Debug.LogError("HeadlessTrial: DogStanceController not found.");
            return;
        }

        c.pawComP = GetFloatArg("-pawComP", c.pawComP);
        c.pawComD = GetFloatArg("-pawComD", c.pawComD);
        c.chestPGain = GetFloatArg("-chestP", c.chestPGain);
        c.chestDGain = GetFloatArg("-chestD", c.chestDGain);
        c.chestTargetTilt = GetFloatArg("-chestTilt", c.chestTargetTilt);
        c.pelvisPGain = GetFloatArg("-pelvisP", c.pelvisPGain);
        c.pelvisDGain = GetFloatArg("-pelvisD", c.pelvisDGain);
        c.pelvisTargetTilt = GetFloatArg("-pelvisTilt", c.pelvisTargetTilt);
        c.lumbarPGain = GetFloatArg("-lumbarP", c.lumbarPGain);
        c.lumbarDGain = GetFloatArg("-lumbarD", c.lumbarDGain);
        c.lumbarPelvisP = GetFloatArg("-lumbarPelvisP", c.lumbarPelvisP);
        c.lumbarPelvisD = GetFloatArg("-lumbarPelvisD", c.lumbarPelvisD);
        c.lumbarStopP = GetFloatArg("-lumbarStopP", c.lumbarStopP);
        c.lumbarRestP = GetFloatArg("-lumbarRestP", c.lumbarRestP);
        c.lumbarDelaySeconds = GetFloatArg("-lumbarDelay", c.lumbarDelaySeconds);
        c.lumbarOpenGateDeg = GetFloatArg("-lumbarOpenGate", c.lumbarOpenGateDeg);
        c.useLumbarJointRest = GetFloatArg("-lumbarJointRest", c.useLumbarJointRest ? 1f : 0f) > 0.5f;
        c.lumbarJointRestDeg = GetFloatArg("-lumbarRestDeg", c.lumbarJointRestDeg);
        c.hipPGain = GetFloatArg("-hipP", c.hipPGain);
        c.startupHipP = GetFloatArg("-startupHipP", c.startupHipP);
        c.startupHipSeconds = GetFloatArg("-startupHipSec", c.startupHipSeconds);
        c.hipDGain = GetFloatArg("-hipD", c.hipDGain);
        c.kneePGain = GetFloatArg("-kneeP", c.kneePGain);
        c.kneeDGain = GetFloatArg("-kneeD", c.kneeDGain);
        c.hipBaseAngle = GetFloatArg("-hipBase", c.hipBaseAngle);
        c.shoulderBaseAngle = GetFloatArg("-shoulderBase", c.shoulderBaseAngle);
        c.kneeBaseAngle = GetFloatArg("-kneeBase", c.kneeBaseAngle);
        c.elbowBaseAngle = GetFloatArg("-elbowBase", c.elbowBaseAngle);
        c.neckPGain = GetFloatArg("-neckP", c.neckPGain);
        c.neckDGain = GetFloatArg("-neckD", c.neckDGain);
        c.neckTargetTilt = GetFloatArg("-neckTilt", c.neckTargetTilt);
        c.useStablePd = GetFloatArg("-spd", c.useStablePd ? 1f : 0f) > 0.5f;
        c.muscleActivationSpeed = GetFloatArg("-activationSpeed", c.muscleActivationSpeed);
        c.fallCoMOffset = GetFloatArg("-fallOffset", c.fallCoMOffset);
        c.heightP = GetFloatArg("-heightP", c.heightP);
        c.heightD = GetFloatArg("-heightD", c.heightD);
        c.heightMaxDeg = GetFloatArg("-heightMax", c.heightMaxDeg);
        c.heightHipMaxDeg = GetFloatArg("-heightHipMax", c.heightHipMaxDeg);
        c.startupHoldSeconds = GetFloatArg("-startupHold", c.startupHoldSeconds);
        c.jawOpen = Mathf.Clamp01(GetFloatArg("-jawOpen", c.jawOpen));
        if (GetFloatArg("-dogBark", 0f) > 0.5f)
            c.Bark();
        if (GetFloatArg("-dogBite", 0f) > 0.5f)
            c.Bite();
        c.walkPeriod = GetFloatArg("-dogWalkPeriod", c.walkPeriod);
        c.walkDuty = GetFloatArg("-dogWalkDuty", c.walkDuty);
        c.walkRearLag = GetFloatArg("-dogWalkRearLag", c.walkRearLag);
        c.walkHipFlex = GetFloatArg("-dogWalkHipFlex", c.walkHipFlex);
        c.walkShoulderFlex = GetFloatArg("-dogWalkShoulderFlex", c.walkShoulderFlex);
        c.walkHipExtend = GetFloatArg("-dogWalkHipExt", c.walkHipExtend);
        c.walkShoulderExtend = GetFloatArg("-dogWalkShoulderExt", c.walkShoulderExtend);
        c.walkKneeFlex = GetFloatArg("-dogWalkKneeFlex", c.walkKneeFlex);
        c.walkElbowFlex = GetFloatArg("-dogWalkElbowFlex", c.walkElbowFlex);
        c.walkPawPlant = GetFloatArg("-dogWalkPlant", c.walkPawPlant);
        c.walkFrontPlantScale = GetFloatArg("-dogWalkFrontPlant", c.walkFrontPlantScale);
        c.walkChestTilt = GetFloatArg("-dogWalkChest", c.walkChestTilt);
        c.walkLumbarP = GetFloatArg("-dogWalkLumbarP", c.walkLumbarP);
        c.walkHipP = GetFloatArg("-dogWalkHipP", c.walkHipP);
        c.walkKneeP = GetFloatArg("-dogWalkKneeP", c.walkKneeP);
        c.walkActivationSpeed = GetFloatArg("-dogWalkAct", c.walkActivationSpeed);
    }

    private void BuildBirdScene()
    {
        string rigRaw = GetStringArg("-birdRig", "ragdoll").Trim().ToLowerInvariant();
        bool flockRig = rigRaw == "flock";
        int count = flockRig && (birdDriveKind == "wander" || treePerch)
            ? birdDriveCount
            : 1;
        Bird[] pack = new Bird[count];
        float spacing = BirdPackSpacing;
        float originX = -0.5f * (count - 1) * spacing;

        for (int i = 0; i < count; i++)
        {
            GameObject birdObject = new GameObject(count == 1 ? "TrialBird" : "TrialBird_" + (i + 1));
            birdObject.transform.position = new Vector2(originX + i * spacing, startY);
            Bird next = birdObject.AddComponent<Bird>();
            next.rig = flockRig ? BirdRig.Flock : BirdRig.Ragdoll;
            next.kind = trialBirdKind;
            next.ApplyKind(trialBirdKind);
            next.minLimbInertia = GetFloatArg("-minInertia", next.minLimbInertia);
            next.minBodyInertia = GetFloatArg("-bodyInertia", next.minBodyInertia);
            birdObject.transform.position = new Vector2(originX + i * spacing, startY);
            next.BuildBird();
            FactionStamp.Bird(birdObject);
            ApplyBirdGains(next.controller);
            if (next.flight != null)
            {
                next.flight.hoverMean = GetFloatArg("-hover", next.flight.hoverMean);
                next.flight.upstrokeLift = GetFloatArg("-upstrokeScale", next.flight.upstrokeLift);
                next.flight.glideLift = GetFloatArg("-glideLift", next.flight.glideLift);
            }

            if (next.controller != null)
            {
                next.controller.flapPhaseOffset = i * 0.13f;
                if (birdDriveKind == "wander" || birdDriveKind == "prey")
                {
                    next.controller.takeoffDelay = 0f;
                    next.controller.turnAfter = 0f;
                    next.controller.mode = BirdMode.Sit;
                }
            }

            pack[i] = next;
        }

        trialBirds = pack;
        bird = pack[0];
        Transform body = bird.transform.Find("Body");
        birdBody = body != null ? body.GetComponent<Rigidbody2D>() : null;
        torsoBody = birdBody;

        bool driveEach = GetFloatArg("-birdDriveEach", 0f) > 0.5f;
        if (birdDriveKind == "wander" && count > 1 && !driveEach)
        {
            GameObject host = new GameObject("BirdFlockDrive");
            BirdFlockDrive flock = host.AddComponent<BirdFlockDrive>();
            flock.allowHeadless = true;
            flock.seed = birdDriveSeed;
            // Timing before Bind: wander Reset reads sitMin/sitMax.
            flock.sitMin = GetFloatArg("-sitMin", flock.sitMin);
            flock.sitMax = GetFloatArg("-sitMax", flock.sitMax);
            flock.flyMin = GetFloatArg("-flyMin", flock.flyMin);
            flock.flyMax = GetFloatArg("-flyMax", flock.flyMax);
            flock.glideLead = GetFloatArg("-glideLead", flock.glideLead);
            flock.homeFaceDeadzone = GetFloatArg("-homeFaceDeadzone", flock.homeFaceDeadzone);
            flock.flockLeash = GetFloatArg("-flockLeash", flock.flockLeash);
            flock.walkMin = GetFloatArg("-walkMin", flock.walkMin);
            flock.walkMax = GetFloatArg("-walkMax", flock.walkMax);
            flock.aiStride = Mathf.Max(1, Mathf.RoundToInt(GetFloatArg("-aiStride", flock.aiStride)));
            flock.Bind(pack, birdDriveSeed);
            if (plantTree != null)
                flock.BindPerches(plantTree.GetPerchSlots());
        }
        else if (birdDriveKind == "wander" && count > 1 && driveEach)
        {
            // Per bird: its own BirdDrive and seed (costlier than a shared flock Update).
            float sitMin = GetFloatArg("-sitMin", 1.4f);
            float sitMax = GetFloatArg("-sitMax", 3.6f);
            float flyMin = GetFloatArg("-flyMin", 2.2f);
            float flyMax = GetFloatArg("-flyMax", 5.5f);
            float glideLead = GetFloatArg("-glideLead", 1.1f);
            float leash = GetFloatArg("-flockLeash", 7f);
            float homeFaceDeadzone = GetFloatArg("-homeFaceDeadzone", 0.15f);
            float walkMin = GetFloatArg("-walkMin", 1.8f);
            float walkMax = GetFloatArg("-walkMax", 3.8f);
            for (int i = 0; i < pack.Length; i++)
            {
                if (pack[i] == null) continue;
                BirdDrive drive = pack[i].gameObject.AddComponent<BirdDrive>();
                drive.allowHeadless = true;
                drive.kind = BirdDrive.Kind.Wander;
                drive.wanderSeed = birdDriveSeed + i * 17;
                drive.sitMin = sitMin;
                drive.sitMax = sitMax;
                drive.flyMin = flyMin;
                drive.flyMax = flyMax;
                drive.glideLead = glideLead;
                drive.leash = leash;
                drive.homeFaceDeadzone = homeFaceDeadzone;
                drive.walkMin = walkMin;
                drive.walkMax = walkMax;
            }
        }
        else if (birdDriveKind == "wander" || birdDriveKind == "prey")
        {
            BirdDrive drive = bird.gameObject.AddComponent<BirdDrive>();
            drive.allowHeadless = true;
            drive.kind = birdDriveKind == "prey" ? BirdDrive.Kind.Prey : BirdDrive.Kind.Wander;
            drive.wanderSeed = birdDriveSeed;
            drive.sitMin = GetFloatArg("-sitMin", drive.sitMin);
            drive.sitMax = GetFloatArg("-sitMax", drive.sitMax);
            drive.flyMin = GetFloatArg("-flyMin", drive.flyMin);
            drive.flyMax = GetFloatArg("-flyMax", drive.flyMax);
            drive.glideLead = GetFloatArg("-glideLead", drive.glideLead);
            drive.leash = GetFloatArg("-flockLeash", drive.leash);
            drive.homeFaceDeadzone = GetFloatArg("-homeFaceDeadzone", drive.homeFaceDeadzone);
            drive.walkMin = GetFloatArg("-walkMin", drive.walkMin);
            drive.walkMax = GetFloatArg("-walkMax", drive.walkMax);
        }

        SoundBus.Clear();
        float threatOff = GetFloatArg("-birdThreat", 0f);
        if (Mathf.Abs(threatOff) > 0.01f)
        {
            GameObject mark = new GameObject("BirdThreat");
            mark.transform.position = new Vector2(originX + threatOff, startY);
            float threatRange = GetFloatArg("-threatRange", 12f);
            BirdDrive[] drives = FindObjectsByType<BirdDrive>(FindObjectsSortMode.None);
            for (int i = 0; i < drives.Length; i++)
            {
                if (drives[i] == null) continue;
                drives[i].threat = mark.transform;
                drives[i].threatRange = threatRange;
            }

            BirdFlockDrive flockDrive = FindFirstObjectByType<BirdFlockDrive>();
            if (flockDrive != null)
            {
                flockDrive.threat = mark.transform;
                flockDrive.threatRange = threatRange;
            }
        }
    }

    private void BuildTreeScene()
    {
        GameObject treeObject = new GameObject("TrialTree");
        treeObject.transform.position = new Vector2(0f, PlantTree.RootY(-2f));
        plantTree = treeObject.AddComponent<PlantTree>();
        plantTree.kind = PlantTree.ParseKind(GetStringArg("-treeKind", "oak"));
        string treeRigRaw = GetStringArg("-treeRig", "sway").Trim().ToLowerInvariant();
        if (treeRigRaw == "static")
            plantTree.rig = TreeRig.Static;
        plantTree.seed = Mathf.RoundToInt(GetFloatArg("-treeSeed", 1f));
        plantTree.windScale = GetFloatArg("-treeWind", plantTree.windScale);
        plantTree.BuildTree();
    }

    private void SeatTrialBirdsOnTree()
    {
        Bird[] pack = trialBirds != null && trialBirds.Length > 0
            ? trialBirds
            : bird != null ? new[] { bird } : System.Array.Empty<Bird>();
        PlantTree.SeatBirds(pack, plantTree);
        Vector2[] slots = plantTree.GetPerchSlots();
        BirdFlockDrive flock = FindFirstObjectByType<BirdFlockDrive>();
        if (flock != null)
            flock.BindPerches(slots);
        else if (bird != null && slots.Length > 0)
        {
            BirdDrive drive = bird.GetComponent<BirdDrive>();
            if (drive != null)
                drive.BindPerch(slots[0]);
        }
    }

    private IEnumerator RunTreeTrial()
    {
        plantRecorder = new PlantTrialRecorder(plantTree, runInfo);
        float trialWallStart = Time.realtimeSinceStartup;
        float simulated = 0f;
        float realDeadline = Time.realtimeSinceStartup + REAL_TIME_LIMIT_SECONDS;
        while (simulated < duration)
        {
            yield return FixedStep;
            simulated += Time.fixedDeltaTime;
            plantRecorder.Sample(simulated);
            if (Time.realtimeSinceStartup > realDeadline)
            {
                Debug.LogWarning("HeadlessTrial: real-time limit exceeded, run aborted.");
                break;
            }
        }

        Finish(simulated, Time.realtimeSinceStartup - trialWallStart);
    }

    // Picture-only garden plants. No hinges. Default row is all 12 kinds.
    private void BuildFlowerScene()
    {
        string kindRaw = GetStringArg("-flowerKind", "");
        bool lockKind = !string.IsNullOrEmpty(kindRaw);
        FlowerKind locked = lockKind ? PlantFlower.ParseKind(kindRaw) : FlowerKind.Rose;
        int count = Mathf.Max(1, Mathf.RoundToInt(GetFloatArg("-flowerCount", lockKind ? 1f : 12f)));
        float spacing = GetFloatArg("-flowerSpacing", 0.55f);
        float originX = GetFloatArg("-flowerOffsetX", -3.3f);
        trialFlowers = new PlantFlower[count];
        int seed0 = Mathf.RoundToInt(GetFloatArg("-flowerSeed", 1f));
        for (int i = 0; i < count; i++)
        {
            FlowerKind kind = lockKind ? locked : PlantFlower.KindFromIndex(i);
            GameObject go = new GameObject("TrialFlower_" + (i + 1) + "_" + kind);
            go.transform.position = new Vector2(originX + i * spacing, PlantFlower.RootY(-2f));
            PlantFlower flower = (PlantFlower)go.AddComponent(PlantFlower.ComponentType(kind));
            flower.kind = kind;
            flower.seed = seed0 + i;
            flower.BuildFlower();
            trialFlowers[i] = flower;
        }
        flowerRecorder = new FlowerTrialRecorder(trialFlowers, runInfo);
    }

    private IEnumerator RunFlowerTrial()
    {
        if (flowerRecorder == null)
            flowerRecorder = new FlowerTrialRecorder(trialFlowers, runInfo);
        float trialWallStart = Time.realtimeSinceStartup;
        float simulated = 0f;
        float realDeadline = Time.realtimeSinceStartup + REAL_TIME_LIMIT_SECONDS;
        while (simulated < duration)
        {
            yield return FixedStep;
            simulated += Time.fixedDeltaTime;
            if (Time.realtimeSinceStartup > realDeadline)
            {
                Debug.LogWarning("HeadlessTrial: real-time limit exceeded, run aborted.");
                break;
            }
        }

        Finish(simulated, Time.realtimeSinceStartup - trialWallStart);
    }

    private void ApplyBirdGains(BirdController c)
    {
        if (c == null)
        {
            Debug.LogError("HeadlessTrial: BirdController not found.");
            return;
        }

        string modeRaw = GetStringArg("-birdMode", "stand").Trim().ToLowerInvariant();
        if (modeRaw == "fly") c.mode = BirdMode.Fly;
        else if (modeRaw == "walk") c.mode = BirdMode.Walk;
        else if (modeRaw == "glide") c.mode = BirdMode.Glide;
        else if (modeRaw == "sit") c.mode = BirdMode.Sit;
        else if (modeRaw == "attack") c.mode = BirdMode.Attack;
        else c.mode = BirdMode.Stand;

        c.takeoffDelay = GetFloatArg("-takeoffDelay", 0f);
        c.SetFacing(GetFloatArg("-birdFacing", 1f));
        c.turnAfter = GetFloatArg("-birdTurnAfter", 0f);
        c.flapFrequency = GetFloatArg("-flapHz", c.flapFrequency);
        c.flapAmplitude = GetFloatArg("-flapAmp", c.flapAmplitude);
        c.flapStrokeGain = GetFloatArg("-flapStroke", c.flapStrokeGain);
        c.flapMidAngle = GetFloatArg("-flapMid", c.flapMidAngle);
        c.jointPGain = GetFloatArg("-jointP", c.jointPGain);
        c.jointDGain = GetFloatArg("-jointD", c.jointDGain);
        c.pitchPGain = GetFloatArg("-pitchP", c.pitchPGain);
        c.pitchDGain = GetFloatArg("-pitchD", c.pitchDGain);
        c.pitchTarget = GetFloatArg("-pitchTarget", c.pitchTarget);
        c.ankleComP = GetFloatArg("-ankleComP", c.ankleComP);
        c.ankleComD = GetFloatArg("-ankleComD", c.ankleComD);
        c.standHipAngle = GetFloatArg("-standHip", c.standHipAngle);
        c.standKneeAngle = GetFloatArg("-standKnee", c.standKneeAngle);
        c.walkThrust = GetFloatArg("-walkThrust", c.walkThrust);
        c.walkFrequency = GetFloatArg("-walkHz", c.walkFrequency);
    }

    private void ApplyMultipliers(Human target, float muscle, float friction)
    {
        target.muscleMultiplier = muscle;
        target.frictionMultiplier = friction;

        target.hipMuscleTorque *= muscle;
        target.kneeMuscleTorque *= muscle;
        target.ankleExtensorTorque *= muscle;
        target.ankleFlexorTorque *= muscle;
        target.shoulderMuscleTorque *= muscle;
        target.elbowMuscleTorque *= muscle;
        target.wristMuscleTorque *= muscle;
        target.neckMuscleTorque *= muscle;
        target.lumbarMuscleTorque *= muscle;

        target.hipFriction *= friction;
        target.kneeFriction *= friction;
        target.ankleFriction *= friction;
        target.shoulderFriction *= friction;
        target.elbowFriction *= friction;
        target.wristFriction *= friction;
        target.neckFriction *= friction;
        target.lumbarFriction *= friction;
        target.lumbarFrictionMaxTorque = GetFloatArg("-lumbarFrictionCap", target.lumbarFrictionMaxTorque);
    }

    // Overrides only those K that were passed explicitly on the CLI.
    // Neck and head share one neckFriction field — there is no separate head flag.
    private static void ApplyFrictionOverrides(Human target)
    {
        if (target == null) return;
        target.neckFriction = GetFloatArg("-neckFriction", target.neckFriction);
        target.shoulderFriction = GetFloatArg("-shoulderFriction", target.shoulderFriction);
        target.elbowFriction = GetFloatArg("-elbowFriction", target.elbowFriction);
        target.wristFriction = GetFloatArg("-wristFriction", target.wristFriction);
    }

    // Neck and head muscle torque: absolute value, after the -muscle multiplier.
    // One field for both joints, same as in Human. It needs its own flag because
    // at 0.0015 kg·m² inertia the old 15 N·m gave 9900 rad/s², and PD
    // reversed the joint every physics step.
    private static void ApplyMuscleOverrides(Human target)
    {
        if (target == null) return;
        target.neckMuscleTorque = GetFloatArg("-neckMuscle", target.neckMuscleTorque);
    }

    private void ApplyGains(BalanceController balance)
    {
        if (balance == null)
        {
            Debug.LogError("HeadlessTrial: BalanceController not found on the human.");
            return;
        }

        balance.hipPGain = GetFloatArg("-hipP", balance.hipPGain);
        balance.hipDGain = GetFloatArg("-hipD", balance.hipDGain);
        balance.kneePGain = GetFloatArg("-kneeP", balance.kneePGain);
        balance.kneeDGain = GetFloatArg("-kneeD", balance.kneeDGain);
        balance.comProportionalGain = GetFloatArg("-comP", balance.comProportionalGain);
        balance.comDerivativeGain = GetFloatArg("-comD", balance.comDerivativeGain);

        balance.hipBaseAngle = GetFloatArg("-hipBase", balance.hipBaseAngle);
        balance.kneeBaseAngle = GetFloatArg("-kneeBase", balance.kneeBaseAngle);
        balance.hipBalanceGain = GetFloatArg("-hipBalance", balance.hipBalanceGain);
        balance.ankleComP = GetFloatArg("-ankleComP", balance.ankleComP);
        balance.ankleComD = GetFloatArg("-ankleComD", balance.ankleComD);
        balance.lumbarPGain = GetFloatArg("-lumbarP", balance.lumbarPGain);
        balance.lumbarDGain = GetFloatArg("-lumbarD", balance.lumbarDGain);
        balance.lumbarTargetTilt = GetFloatArg("-lumbarTilt", balance.lumbarTargetTilt);
        balance.lumbarErrorReferenceDegrees = GetFloatArg("-lumbarRef", balance.lumbarErrorReferenceDegrees);
        balance.recoveryCoMOffset = GetFloatArg("-recoveryOffset", balance.recoveryCoMOffset);
        balance.fallCoMOffset = GetFloatArg("-fallOffset", balance.fallCoMOffset);
        balance.crouchRatePerSecond = GetFloatArg("-crouchRate", balance.crouchRatePerSecond);
        balance.crouchReleaseRatePerSecond = GetFloatArg("-crouchReleaseRate", balance.crouchReleaseRatePerSecond);
        balance.crouchKneeFlex = GetFloatArg("-crouchKnee", balance.crouchKneeFlex);
        balance.crouchHipFlex = GetFloatArg("-crouchHip", balance.crouchHipFlex);
        balance.crouchPelvisTilt = GetFloatArg("-crouchPelvis", balance.crouchPelvisTilt);
        balance.crouchTorsoLean = GetFloatArg("-crouchLean", balance.crouchTorsoLean);
        balance.leanRatePerSecond = GetFloatArg("-leanRate", balance.leanRatePerSecond);
        balance.leanTorsoAngle = GetFloatArg("-leanTorso", balance.leanTorsoAngle);
        balance.leanPelvisTilt = GetFloatArg("-leanPelvis", balance.leanPelvisTilt);
        balance.crouchArmShoulder = GetFloatArg("-crouchArmShoulder", balance.crouchArmShoulder);
        balance.crouchArmElbow = GetFloatArg("-crouchArmElbow", balance.crouchArmElbow);
        balance.crouchArmWrist = GetFloatArg("-crouchArmWrist", balance.crouchArmWrist);
        balance.crouchHandSupportMin = GetFloatArg("-crouchHandMin", balance.crouchHandSupportMin);
        balance.crouchHandGroundSlop = GetFloatArg("-crouchHandSlop", balance.crouchHandGroundSlop);
        balance.crouchHandPoseStart = GetFloatArg("-crouchHandPoseStart", balance.crouchHandPoseStart);
        balance.crouchHandSupportShoulder = GetFloatArg("-crouchHandShoulder", balance.crouchHandSupportShoulder);
        balance.crouchHandSupportElbow = GetFloatArg("-crouchHandElbow", balance.crouchHandSupportElbow);
        balance.crouchHandSupportWrist = GetFloatArg("-crouchHandWrist", balance.crouchHandSupportWrist);
        balance.crouchHandSupportSpread = GetFloatArg("-crouchHandSpread", balance.crouchHandSupportSpread);
        balance.crouchHandBalanceSpread = GetFloatArg("-crouchHandBalSpread", balance.crouchHandBalanceSpread);
        balance.pelvisPGain = GetFloatArg("-pelvisP", balance.pelvisPGain);
        balance.pelvisDGain = GetFloatArg("-pelvisD", balance.pelvisDGain);
        balance.pelvisErrorReferenceDegrees = GetFloatArg("-pelvisRef", balance.pelvisErrorReferenceDegrees);
        balance.neckPGain = GetFloatArg("-neckP", balance.neckPGain);
        balance.neckDGain = GetFloatArg("-neckD", balance.neckDGain);
        balance.neckTargetTilt = GetFloatArg("-neckTilt", balance.neckTargetTilt);
        balance.neckErrorReferenceDegrees = GetFloatArg("-neckRef", balance.neckErrorReferenceDegrees);
        balance.useStablePd = GetFloatArg("-spd", balance.useStablePd ? 1f : 0f) > 0.5f;
        balance.shoulderPGain = GetFloatArg("-shoulderP", balance.shoulderPGain);
        balance.shoulderDGain = GetFloatArg("-shoulderD", balance.shoulderDGain);
        balance.shoulderBaseAngle = GetFloatArg("-shoulderBase", balance.shoulderBaseAngle);
        balance.shoulderErrorReferenceDegrees = GetFloatArg("-shoulderRef", balance.shoulderErrorReferenceDegrees);
        balance.elbowPGain = GetFloatArg("-elbowP", balance.elbowPGain);
        balance.elbowDGain = GetFloatArg("-elbowD", balance.elbowDGain);
        balance.elbowBaseAngle = GetFloatArg("-elbowBase", balance.elbowBaseAngle);
        balance.elbowErrorReferenceDegrees = GetFloatArg("-elbowRef", balance.elbowErrorReferenceDegrees);
        balance.wristPGain = GetFloatArg("-wristP", balance.wristPGain);
        balance.wristDGain = GetFloatArg("-wristD", balance.wristDGain);
        balance.wristBaseAngle = GetFloatArg("-wristBase", balance.wristBaseAngle);
        balance.wristErrorReferenceDegrees = GetFloatArg("-wristRef", balance.wristErrorReferenceDegrees);
        balance.armShoulderBalanceGain = GetFloatArg("-armShoulderBal", balance.armShoulderBalanceGain);
        balance.armElbowBalanceGain = GetFloatArg("-armElbowBal", balance.armElbowBalanceGain);
        balance.armWristBalanceGain = GetFloatArg("-armWristBal", balance.armWristBalanceGain);
        balance.swingHipFlex = GetFloatArg("-swingHip", balance.swingHipFlex);
        balance.swingKneeFlex = GetFloatArg("-swingKnee", balance.swingKneeFlex);
        balance.swingHipUnloadBias = GetFloatArg("-swingHipUnload", balance.swingHipUnloadBias);
        balance.swingKneeGroundedFraction = GetFloatArg("-swingKneeGround", balance.swingKneeGroundedFraction);
        balance.forwardSwingHipUnloadScale = GetFloatArg("-forwardUnloadScale", balance.forwardSwingHipUnloadScale);
        balance.forwardSwingKneeGroundScale = GetFloatArg("-forwardKneeScale", balance.forwardSwingKneeGroundScale);
        balance.backSwingHipUnloadScale = GetFloatArg("-backUnloadScale", balance.backSwingHipUnloadScale);
        balance.backSwingKneeGroundScale = GetFloatArg("-backKneeScale", balance.backSwingKneeGroundScale);
        balance.backPelvisTiltFraction = GetFloatArg("-backPelvisTilt", balance.backPelvisTiltFraction);
        balance.swingAnkleGroundedToeOff = GetFloatArg("-swingAnkleToeOff", balance.swingAnkleGroundedToeOff);
        balance.standLegPelvisTilt = GetFloatArg("-standPelvisTilt", balance.standLegPelvisTilt);
        balance.dualSupportSwingScale = GetFloatArg("-dualSupportSwing", balance.dualSupportSwingScale);
        balance.dualSupportStandCap = GetFloatArg("-dualSupportCap", balance.dualSupportStandCap);
        balance.walkSwingLiftScale = GetFloatArg("-walkSwingLift", balance.walkSwingLiftScale);
        balance.walkStanceLiftDelay = GetFloatArg("-walkLiftDelay", balance.walkStanceLiftDelay);
        balance.walkStanceLiftRamp = GetFloatArg("-walkLiftRamp", balance.walkStanceLiftRamp);
        balance.walkWeightRamp = GetFloatArg("-walkWeightRamp", balance.walkWeightRamp);
        balance.runStanceLiftDelay = GetFloatArg("-runLiftDelay", balance.runStanceLiftDelay);
        balance.runWeightRamp = GetFloatArg("-runWeightRamp", balance.runWeightRamp);
        balance.walkLiftComMax = GetFloatArg("-walkLiftCom", balance.walkLiftComMax);
        balance.walkSwingToeOffMax = GetFloatArg("-walkSwingToeOff", balance.walkSwingToeOffMax);
        balance.walkLiftLevelRateScale = GetFloatArg("-walkLiftLevelRate", balance.walkLiftLevelRateScale);
        balance.walkLiftUnloadBias = GetFloatArg("-walkLiftUnload", balance.walkLiftUnloadBias);
        balance.walkKneePeelMax = GetFloatArg("-walkKneePeel", balance.walkKneePeelMax);
        balance.walkStancePush = GetFloatArg("-walkStancePush", balance.walkStancePush);
        balance.runStancePushScale = GetFloatArg("-runStancePushScale", balance.runStancePushScale);
        balance.walkTargetSpeed = GetFloatArg("-walkTargetSpeed", balance.walkTargetSpeed);
        balance.walkSpeedPushGain = GetFloatArg("-walkSpeedPushGain", balance.walkSpeedPushGain);
        balance.walkSpeedPushMax = GetFloatArg("-walkSpeedPushMax", balance.walkSpeedPushMax);
        balance.walkSpeedPushComGateScale = GetFloatArg("-walkSpeedPushComGateScale", balance.walkSpeedPushComGateScale);
        balance.walkXCoMWeight = GetFloatArg("-walkXCoMWeight", balance.walkXCoMWeight);
        balance.walkXCoMHeight = GetFloatArg("-walkXCoMHeight", balance.walkXCoMHeight);
        balance.walkSpeedLeanGain = GetFloatArg("-walkSpeedLeanGain", balance.walkSpeedLeanGain);
        balance.walkSpeedLeanMax = GetFloatArg("-walkSpeedLeanMax", balance.walkSpeedLeanMax);
        balance.walkSpeedLeanTorsoScale = GetFloatArg("-walkSpeedLeanTorsoScale", balance.walkSpeedLeanTorsoScale);
        balance.walkSpeedDriveGain = GetFloatArg("-walkSpeedDriveGain", balance.walkSpeedDriveGain);
        balance.walkSpeedDriveMax = GetFloatArg("-walkSpeedDriveMax", balance.walkSpeedDriveMax);
        balance.walkSpeedDriveStartLevel = GetFloatArg("-walkSpeedDriveStartLevel", balance.walkSpeedDriveStartLevel);
        balance.walkSpeedSwingHipGain = GetFloatArg("-walkSpeedSwingHipGain", balance.walkSpeedSwingHipGain);
        balance.walkSpeedSwingKneeGain = GetFloatArg("-walkSpeedSwingKneeGain", balance.walkSpeedSwingKneeGain);
        balance.walkSpeedSwingFlexMax = GetFloatArg("-walkSpeedSwingFlexMax", balance.walkSpeedSwingFlexMax);
        balance.walkSwingHipBoost = GetFloatArg("-walkSwingHipBoost", balance.walkSwingHipBoost);
        balance.walkSwingKneeBoost = GetFloatArg("-walkSwingKneeBoost", balance.walkSwingKneeBoost);
        balance.walkStepLength = GetFloatArg("-walkStepLength", balance.walkStepLength);
        balance.walkStepLengthSpeedGain = GetFloatArg("-walkStepLengthSpeedGain", balance.walkStepLengthSpeedGain);
        balance.walkStepPlacementGain = GetFloatArg("-walkStepPlacementGain", balance.walkStepPlacementGain);
        balance.walkStepPlacementMax = GetFloatArg("-walkStepPlacementMax", balance.walkStepPlacementMax);
        balance.walkStepPlacementGroundFraction = GetFloatArg("-walkStepPlacementGroundFraction", balance.walkStepPlacementGroundFraction);
        balance.walkPlacementSyncStart = GetFloatArg("-walkPlacementSyncStart", balance.walkPlacementSyncStart);
        balance.walkTouchdownSyncClearance = GetFloatArg("-walkTouchdownSyncClearance", balance.walkTouchdownSyncClearance);
        balance.walkStanceKneeBend = GetFloatArg("-walkStanceKneeBend", balance.walkStanceKneeBend);
        balance.walkForwardPelvisLean = GetFloatArg("-walkForwardPelvisLean", balance.walkForwardPelvisLean);
        balance.walkForwardTorsoLean = GetFloatArg("-walkForwardTorsoLean", balance.walkForwardTorsoLean);
        balance.walkStanceExtend = GetFloatArg("-walkStanceExtend", balance.walkStanceExtend);
        balance.walkComLeadX = GetFloatArg("-walkComLead", balance.walkComLeadX);
        balance.walkSwingScissorLevel = GetFloatArg("-walkSwingScissor", balance.walkSwingScissorLevel);
        balance.walkSwingScissorFlex = GetFloatArg("-walkSwingScissorFlex", balance.walkSwingScissorFlex);
        balance.walkSwingAirHold = GetFloatArg("-walkSwingAirHold", balance.walkSwingAirHold);
        balance.walkSwingHeelPlant = GetFloatArg("-walkSwingHeelPlant", balance.walkSwingHeelPlant);
        balance.walkSwingUnlatchPlant = GetFloatArg("-walkSwingUnlatchPlant", balance.walkSwingUnlatchPlant);
        balance.walkSwingLatchAir = GetFloatArg("-walkSwingLatchAir", balance.walkSwingLatchAir);
        balance.walkSwingAirLevel = GetFloatArg("-walkSwingAirLevel", balance.walkSwingAirLevel);
        balance.walkArmShoulderSwing = GetFloatArg("-walkArmShoulder", balance.walkArmShoulderSwing);
        balance.walkArmElbowSwing = GetFloatArg("-walkArmElbow", balance.walkArmElbowSwing);
        balance.walkArmTransferCarry = GetFloatArg("-walkArmTransfer", balance.walkArmTransferCarry);
        balance.walkArmBalanceShoulderGain = GetFloatArg("-walkArmBalanceShoulderGain", balance.walkArmBalanceShoulderGain);
        balance.walkArmBalanceElbowGain = GetFloatArg("-walkArmBalanceElbowGain", balance.walkArmBalanceElbowGain);
        balance.walkLatePushGain = GetFloatArg("-walkLatePushGain", balance.walkLatePushGain);
        balance.walkLatePushStart = GetFloatArg("-walkLatePushStart", balance.walkLatePushStart);
        balance.walkLatePushMax = GetFloatArg("-walkLatePushMax", balance.walkLatePushMax);
        balance.walkLatePushNeedsAir = GetFloatArg("-walkLatePushNeedsAir", balance.walkLatePushNeedsAir ? 1f : 0f) > 0.5f;
        balance.walkLatePushNeedsTouchdownWindow = GetFloatArg("-walkLatePushNeedsTouchdownWindow", balance.walkLatePushNeedsTouchdownWindow ? 1f : 0f) > 0.5f;
        balance.standLegRatePerSecond = GetFloatArg("-standLegRate", balance.standLegRatePerSecond);
        balance.standLegReleaseRatePerSecond = GetFloatArg("-standLegReleaseRate", balance.standLegReleaseRatePerSecond);

        // Speed at which muscle activation catches the controller command.
        // 500 — almost instant; 10 — the muscle rises in ~0.1 s.
        balance.muscleActivationSpeed = GetFloatArg("-activationSpeed", balance.muscleActivationSpeed);
    }

    // ─── THE RUN ITSELF: STEP PHYSICS AND SAMPLE STATE ───
    private IEnumerator RunTrial()
    {
        // Skip the first step: the body is still settling in the joints.
        yield return FixedStep;

        if (spawnDog)
        {
            yield return RunDogTrial();
            yield break;
        }

        if (spawnBird)
        {
            yield return RunBirdTrial();
            yield break;
        }

        if (spawnFlower && !spawnTree)
        {
            yield return RunFlowerTrial();
            yield break;
        }

        if (spawnTree)
        {
            yield return RunTreeTrial();
            yield break;
        }

        recorder = new TrialRecorder(human, runInfo);
        recorder.BindWalkDriver(stepDriver);

        float trialWallStart = Time.realtimeSinceStartup;
        float simulated = 0f;
        float realDeadline = Time.realtimeSinceStartup + REAL_TIME_LIMIT_SECONDS;
        bool pushed = false;
        bool crouchMarked = false;
        bool crouchReleased = false;
        bool leanMarked = false;
        bool leanReleased = false;
        bool standLegMarked = false;
        bool standLegReleased = false;

        while (simulated < duration)
        {
            yield return FixedStep;

            simulated += Time.fixedDeltaTime;

            if (!pushed && pushImpulse != 0f && simulated >= pushTime)
            {
                ApplyPush(simulated);
                pushed = true;
            }

            UpdateCrouchIntent(simulated, ref crouchMarked, ref crouchReleased);
            UpdateLeanIntent(simulated, ref leanMarked, ref leanReleased);
            if (walkTarget > 0.5f || runTarget > 0.5f)
                UpdateWalkIntent(simulated);
            else
                UpdateStandLegIntent(simulated, ref standLegMarked, ref standLegReleased);

            recorder.Sample(simulated);

            if (Time.realtimeSinceStartup > realDeadline)
            {
                Debug.LogWarning("HeadlessTrial: real-time limit exceeded, run aborted.");
                break;
            }
        }

        Finish(simulated, Time.realtimeSinceStartup - trialWallStart);
    }

    private IEnumerator RunDogTrial()
    {
        dogRecorder = new DogTrialRecorder(dog, runInfo);

        float trialWallStart = Time.realtimeSinceStartup;
        float simulated = 0f;
        float realDeadline = Time.realtimeSinceStartup + REAL_TIME_LIMIT_SECONDS;
        bool pushed = false;
        bool leaped = false;
        bool walked = false;

        while (simulated < duration)
        {
            yield return FixedStep;
            simulated += Time.fixedDeltaTime;

            if (!pushed && pushImpulse != 0f && simulated >= pushTime)
            {
                ApplyPush(simulated);
                pushed = true;
            }

            if (!leaped && dogLeap && simulated >= dogLeapTime)
            {
                if (dog != null && dog.stance != null)
                    dog.stance.LeapAttack(dog.stance.leapTarget);
                leaped = true;
            }

            if (!walked && dogWalk && simulated >= dogWalkTime)
            {
                if (dog != null && dog.stance != null)
                    dog.stance.Walk();
                walked = true;
            }

            dogRecorder.Sample(simulated);

            if (Time.realtimeSinceStartup > realDeadline)
            {
                Debug.LogWarning("HeadlessTrial: real-time limit exceeded, run aborted.");
                break;
            }
        }

        Finish(simulated, Time.realtimeSinceStartup - trialWallStart);
    }

    private IEnumerator RunBirdTrial()
    {
        birdRecorder = new BirdTrialRecorder(bird, runInfo);
        if (plantTree != null)
            plantRecorder = new PlantTrialRecorder(plantTree, runInfo);

        float trialWallStart = Time.realtimeSinceStartup;
        float simulated = 0f;
        float realDeadline = Time.realtimeSinceStartup + REAL_TIME_LIMIT_SECONDS;
        bool pushed = false;
        bool flapStopped = false;

        while (simulated < duration)
        {
            yield return FixedStep;
            simulated += Time.fixedDeltaTime;

            if (!flapStopped && stopFlapTime > 0.01f && simulated >= stopFlapTime
                && bird != null && bird.controller != null)
            {
                // Without a flap it does not hover: it glides and lands. Stone drop — stand.
                bird.controller.mode = stopFlapMode == "stand" ? BirdMode.Stand : BirdMode.Glide;
                bird.controller.takeoffDelay = 0f;
                flapStopped = true;
            }

            if (!pushed && pushImpulse != 0f && simulated >= pushTime)
            {
                ApplyPush(simulated);
                pushed = true;
            }

            birdRecorder.Sample(simulated);
            if (plantRecorder != null)
                plantRecorder.Sample(simulated);

            if (Time.realtimeSinceStartup > realDeadline)
            {
                Debug.LogWarning("HeadlessTrial: real-time limit exceeded, run aborted.");
                break;
            }
        }

        Finish(simulated, Time.realtimeSinceStartup - trialWallStart);
    }

    // Apply the impulse to the torso: that is a shove at the shoulder, not the foot.
    private void ApplyPush(float time)
    {
        if (torsoBody == null)
        {
            Debug.LogWarning("HeadlessTrial: torso not found, push not applied.");
            return;
        }

        torsoBody.AddForce(new Vector2(pushImpulse, 0f), ForceMode2D.Impulse);
        if (recorder != null)
            recorder.MarkPush(time, pushImpulse);
        if (dogRecorder != null)
            dogRecorder.MarkPush(time, pushImpulse);
    }

    // Rewrite each hold window every step: if PlayerInputSource
    // suddenly wakes, it cannot erase the script in one frame.
    private void UpdateCrouchIntent(float time, ref bool marked, ref bool released)
    {
        if (intent == null) return;

        bool holding = crouchTarget > 0f && time >= crouchTime && time < crouchTime + crouchHold;
        intent.crouch = holding ? crouchTarget : 0f;

        if (holding && !marked)
        {
            recorder.MarkCrouch(time, crouchTarget);
            marked = true;
        }
        else if (marked && !released && !holding)
        {
            recorder.MarkCrouchRelease(time);
            released = true;
        }
    }

    // Lean via MotionIntent: +1 forward, −1 back (same as the Play arrows).
    private void UpdateLeanIntent(float time, ref bool marked, ref bool released)
    {
        if (intent == null) return;

        bool holding = Mathf.Abs(leanTarget) > 0.01f
            && time >= leanTime && time < leanTime + leanHold;
        intent.lean = holding ? leanTarget : 0f;

        if (holding && !marked)
            marked = true;
        else if (marked && !released && !holding)
            released = true;
    }

    // Timed standLeg swap: both supports until walkTime, then ±1 every walkStance.
    // -run 1 in the runTime…runHold window sets intent.run (shorter Stance).
    private void UpdateWalkIntent(float time)
    {
        if (intent == null || stepDriver == null) return;

        bool walkWindow = walkTarget > 0.5f && time >= walkTime && time < walkTime + walkDuration;
        bool runWindow = runTarget > 0.5f && time >= runTime && time < runTime + runHold;
        bool active = walkWindow || runWindow;
        if (!active)
        {
            intent.run = 0f;
            intent.moveX = 0f;
            if (stepDriver.walkActive)
                stepDriver.EndWalk();
            else
                intent.standLeg = 0f;
            return;
        }

        intent.moveX = 1f;
        intent.run = runWindow ? 1f : 0f;

        if (!stepDriver.walkActive)
            stepDriver.BeginWalk(runWindow ? runTime : walkTime);
        stepDriver.stanceDuration = walkStanceDuration;
        stepDriver.runStanceDuration = runStanceDuration;
        stepDriver.runTransferMaxDuration = runTransferMaxDuration;
        stepDriver.stanceHeelStrikeMinAge = walkHeelStrikeMinAge;
        stepDriver.stanceHeelAirMin = walkHeelAirMin;
        stepDriver.stanceComTrigger = walkComTrigger;
        stepDriver.stanceComMinAge = walkComTriggerMinAge;
        stepDriver.stanceComTriggerPerSpeed = walkComTriggerPerSpeed;
        stepDriver.walkCadenceStanceGain = walkCadenceStanceGain;
        stepDriver.walkCadenceMinStance = walkCadenceMinStance;
        stepDriver.transferMaxDuration = walkTransferMaxDuration;
        stepDriver.transferMinDuration = walkTransferMinDuration;
        stepDriver.walkCadenceTransferGain = walkCadenceTransferGain;
        stepDriver.walkCadenceMinTransfer = walkCadenceMinTransfer;
        stepDriver.transferComOffsetMax = walkTransferComMax;
        stepDriver.transferFallbackComMax = walkTransferFallbackComMax;
        stepDriver.transferStandLegLevelMax = walkTransferLevelMax;
        stepDriver.firstStance = walkFirstStance;
        stepDriver.Tick(time);
    }

    // left / −1 — left support, right / +1 — right, 0 / both — both feet.
    // A string, not a float: otherwise “left” silently falls back to the default, like a comma in a number.
    private void UpdateStandLegIntent(float time, ref bool marked, ref bool released)
    {
        if (intent == null) return;

        bool holding = Mathf.Abs(standLegTarget) > 0.5f
            && time >= standLegTime && time < standLegTime + standLegHold;
        intent.standLeg = holding ? standLegTarget : 0f;

        if (holding && !marked)
        {
            recorder.MarkStandLeg(time, standLegTarget);
            marked = true;
        }
        else if (marked && !released && !holding)
        {
            recorder.MarkStandLegRelease(time);
            released = true;
        }
    }

    private static float ParseStandLegArg(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return 0f;

        string key = raw.Trim().ToLowerInvariant();
        if (key == "left" || key == "l") return -1f;
        if (key == "right" || key == "r") return 1f;
        if (key == "both" || key == "none" || key == "0") return 0f;

        if (float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
        {
            if (value > 0.5f) return 1f;
            if (value < -0.5f) return -1f;
            return 0f;
        }

        Debug.LogWarning($"HeadlessTrial: could not parse -standLeg={raw}, using 0 (both feet).");
        return 0f;
    }

    private void Finish(float simulated, float wallSeconds = -1f)
    {
        try
        {
            Directory.CreateDirectory(outputFolder);

            string csvPath = Path.Combine(outputFolder, label + ".csv");
            string summary;
            if (spawnDog)
            {
                File.WriteAllText(csvPath, dogRecorder.BuildCsv());
                summary = dogRecorder.BuildSummaryJson(simulated, wallSeconds);
            }
            else if (spawnBird)
            {
                File.WriteAllText(csvPath, birdRecorder.BuildCsv());
                summary = birdRecorder.BuildSummaryJson(simulated, wallSeconds);
                if (plantRecorder != null)
                    summary = MergePlantJson(summary, plantRecorder);
            }
            else if (spawnTree)
            {
                File.WriteAllText(csvPath, "t\n");
                summary = plantRecorder.BuildSummaryJson(simulated, wallSeconds);
            }
            else if (spawnFlower)
            {
                File.WriteAllText(csvPath, "t\n");
                summary = flowerRecorder.BuildSummaryJson(simulated, wallSeconds);
            }
            else
            {
                File.WriteAllText(csvPath, recorder.BuildCsv());
                summary = recorder.BuildSummaryJson(simulated, wallSeconds);
            }
            File.WriteAllText(Path.Combine(outputFolder, label + ".json"), summary);

            // Marker in stdout so the result can be read straight from the run log.
            Debug.Log("TRIAL_SUMMARY " + summary);
            Debug.Log("TRIAL_CSV " + csvPath);
        }
        catch (Exception e)
        {
            Debug.LogError("HeadlessTrial: failed to write results: " + e.Message);
            Application.Quit(2);
            return;
        }

        Application.Quit(0);
    }

    private static string MergePlantJson(string summary, PlantTrialRecorder plant)
    {
        if (string.IsNullOrEmpty(summary) || plant == null)
            return summary;
        int close = summary.LastIndexOf('}');
        if (close < 0)
            return summary;
        StringBuilder extra = new StringBuilder(256);
        extra.Append(",\"treePresent\":true,\"tree\":{");
        StringBuilder inner = new StringBuilder(256);
        plant.AppendMetrics(inner);
        string body = inner.ToString();
        extra.Append(body.StartsWith(",") ? body.Substring(1) : body);
        extra.Append('}');
        return summary.Substring(0, close) + extra + summary.Substring(close);
    }

    private bool TryAcceptProvenance(out TrialRunInfo info, out string error)
    {
        info = null;
        error = null;

        BuildManifestDto manifest = LoadBuildManifest();
        string argFingerprint = GetStringArg("-sourceFingerprint", "");
        string manifestFingerprint = manifest != null ? manifest.fingerprint : "";
        string manifestGuid = manifest != null ? manifest.buildGuid : "";
        string appGuid = Application.buildGUID;

        if (!string.IsNullOrEmpty(argFingerprint) && !string.IsNullOrEmpty(manifestFingerprint)
            && argFingerprint != manifestFingerprint)
        {
            error = "sourceFingerprint arg != build-manifest.fingerprint";
            return false;
        }

        if (!string.IsNullOrEmpty(appGuid) && !string.IsNullOrEmpty(manifestGuid)
            && NormalizeGuid(appGuid) != NormalizeGuid(manifestGuid))
        {
            error = "Application.buildGUID != build-manifest.buildGuid";
            return false;
        }

        string fingerprint = !string.IsNullOrEmpty(manifestFingerprint)
            ? manifestFingerprint
            : (argFingerprint ?? "");

        info = new TrialRunInfo
        {
            Label = label,
            Duration = duration,
            StartY = startY,
            RequestedPushImpulse = pushImpulse,
            RequestedPushTime = pushTime,
            RequestedCrouch = crouchTarget,
            RequestedCrouchTime = crouchTime,
            RequestedCrouchHold = crouchHold,
            RequestedLean = leanTarget,
            RequestedLeanTime = leanTime,
            RequestedLeanHold = leanHold,
            RequestedStandLeg = standLegTarget,
            RequestedStandLegTime = standLegTime,
            RequestedStandLegHold = standLegHold,
            RequestedWalk = walkTarget,
            RequestedWalkTime = walkTime,
            RequestedWalkDuration = walkDuration,
            RequestedWalkStance = walkStanceDuration,
            RequestedRun = runTarget,
            RequestedRunTime = runTime,
            RequestedRunHold = runHold,
            RequestedWalkTransfer = walkTransferMaxDuration,
            RequestedWalkTransferCom = walkTransferComMax,
            RequestedWalkTransferFallbackCom = walkTransferFallbackComMax,
            RequestedWalkTransferLevel = walkTransferLevelMax,
            RequestedWalkFirst = walkFirstStance,
            MuscleMultiplier = GetFloatArg("-muscle", 1f),
            FrictionMultiplier = GetFloatArg("-friction", 1f),
            SourceFingerprint = fingerprint ?? "",
            ManifestUnityVersion = manifest != null ? manifest.unityVersion : "",
            ManifestBuildGuid = manifestGuid ?? "",
            ManifestBuildTimeUtc = manifest != null ? manifest.buildTimeUtc : "",
            ManifestPath = FindBuildManifestPath() ?? "",
            CommandArgs = Environment.GetCommandLineArgs()
        };
        return true;
    }

    private static string NormalizeGuid(string value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        return value.Replace("-", "").ToLowerInvariant();
    }

    private static BuildManifestDto LoadBuildManifest()
    {
        string path = FindBuildManifestPath();
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
            return null;

        try
        {
            return JsonUtility.FromJson<BuildManifestDto>(File.ReadAllText(path));
        }
        catch (Exception e)
        {
            Debug.LogWarning("HeadlessTrial: failed to read build-manifest: " + e.Message);
            return null;
        }
    }

    private static string FindBuildManifestPath()
    {
        string fromArg = GetStringArg("-buildManifest", null);
        if (!string.IsNullOrEmpty(fromArg) && File.Exists(fromArg))
            return fromArg;

        try
        {
            string parent = Directory.GetParent(Application.dataPath)?.FullName;
            if (!string.IsNullOrEmpty(parent))
            {
                string nextToExe = Path.Combine(parent, "build-manifest.json");
                if (File.Exists(nextToExe))
                    return nextToExe;
            }
        }
        catch
        {
        }

        return null;
    }

    private static string DefaultOutputFolder()
    {
        try
        {
            string parent = Directory.GetParent(Application.dataPath)?.FullName;
            if (!string.IsNullOrEmpty(parent))
                return Path.Combine(parent, "trials");
        }
        catch
        {
        }

        return Path.Combine(Directory.GetCurrentDirectory(), "trials");
    }

    // ─── COMMAND-LINE ARGUMENT PARSING ───
    private static string GetStringArg(string name, string fallback)
    {
        string[] args = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(args, name);
        if (index < 0 || index + 1 >= args.Length) return fallback;
        return args[index + 1];
    }

    private static bool HasArg(string name)
    {
        return Array.IndexOf(Environment.GetCommandLineArgs(), name) >= 0;
    }

    private static float GetFloatArg(string name, float fallback)
    {
        string raw = GetStringArg(name, null);
        if (string.IsNullOrEmpty(raw)) return fallback;

        // Invariant culture is required: on a Russian locale comma
        // and dot parse differently, and parameters silently slip.
        if (float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
            return value;

        Debug.LogWarning($"HeadlessTrial: could not parse {name}={raw}, using {fallback}.");
        return fallback;
    }
}
