using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;

// Run one human without the editor window, for numerical balance analysis.
// Activated by the -trial argument on the built player's command line:
//   BalanceTrial.exe -batchmode -nographics -trial -duration 10 -label base -logFile -
//   -fixedDelta 0.005 — physics step (0.02 = 50 Hz, 0.005 = 200 Hz)
//   -activationSpeed 10 — muscle force rise speed
//   -pushImpulse 20 -pushTime 5 — torso push (N·s, + forward) at second 5
//   -crouch 1 -crouchTime 5 -crouchHold 10 — crouch target via MotionIntent
//   -lean 1|-1 -leanTime 5 -leanHold 10 — trunk lean (+ forward, − back)
//   -standLeg left|right|0 -standLegTime 5 -standLegHold 10 — one-leg stance
//   -march 1 -marchTime 5 -marchDuration 24 -marchHold 2.5 -marchPlant 1.5
//     -marchFirst left|right — in-place clock on standLeg (not walk / WalkLift)
//   -marchHip / -marchKnee — air pose while marching (deg; hip minus, knee plus)
//   -marchStanceP / -marchAnkleCap / -marchAnkleToe / -marchAnkleHeel
//     / -marchComDelay / -marchPoseHip / -marchPoseKnee
//     — vertical pelvis; ankle cap after true air; toe/heel after pose-up
//   -dog 1 — run a dog instead of a human (humanCount = 0, flat ground)
//   -dog 1 -bird 1 — both bodies in one scene (bird is not dropped)
//   -dogBird 1 — dog trial also spawns a live bird inside DogDrive.noticeRange
//   -dogBirdX 2 — bird X from the dog, m (clamped inside noticeRange; default 2)
//   -dogLeap 1 -dogLeapTime 2 -dogPreyX 1.4 — leap attack after settle, prey mark
    //   -dogWalk 1 -dogWalkTime 1 — clocked gait after settle (not human -walk)
    //   -dogRunSpeed 1.4 — bound / gallop speed, m/s (0 = old tiny diagonal)
//   -flower 1 — garden bed (picture only, no hinges). -flowerKind rose|…
//   -flowerCount 12 — how many plants in the row
//   -tree 1 — tree (can pair with -bird: birds on branches)
//   -treeKind oak|pine|willow|bush|poplar|apple|lilac|hazel|cedar|plum — crown recipe (default oak)
//   -treeSeed 1 — local fork Random
//   -treePerch 1 — seat birds on PlantTree.GetPerchSlots
//   -treePerchLoad 4 — injected sitter load, kg, at one perch slot (plant applies it)
//   -treePerchSlot 0 — which slot (twigs first, same order as GetPerchSlots)
//   -treePerchLoadUntil 4 — release the load at 4 s, so one run shows bend + return
//   -treeWind 0 — no sail (check that branches hold the pose)
//   -apple 1 — apple tree (AppleTree : TreeBase : PlantBase; sway + fruit)
//   -appleSeed 1 — local fork Random
//   -appleWind 0 — no sail (hold); default 0.12 sways the crown
//   -appleHeight 2.5 — force crown height, m (overrides the seed-derived 2–5 m)
//   -bird 1 — run a bird instead of a human (human flags are not read)
//   -birdMode stand|fly|walk|glide|sit|attack -takeoffDelay 0 — stance with no takeoff
//   -birdRig ragdoll|flock — ragdoll for stance, flock for a pack (one body)
//   -birdFacing 1|-1 — nose along +X or −X (flock: picture on Look)
    //   -birdKind crow|chicken — body recipe (chicken: ground, not cruise)
    //   -birdDrive wander|prey — brain writes mode (flock: wander + -birdCount)
    //   -birdPrey 10 — live Insect on dirt ahead, m (prey drive defaults to 10; 0 — none)
    //   -birdThreat 8 — threat mark ahead, m (0 — none; cry + Fly, not Attack)
    //   -threatRange 12 — “sees threat” range, m
//   -glideLead / -sitMin / -sitMax / -flyMin / -flyMax / -flockLeash / -aiStride
//     / -homeFaceDeadzone — wander timing, flock brain stride, Glide nose deadzone at the slot
//   -birdDriveEach 1 — N× BirdDrive (per bird) instead of one BirdFlockDrive
//   -flapHz / -flapAmp / -hover — flap and mean thrust (fractions of weight)
//   -stopFlap 4 — drop the flap at second 4: default is glide and land
//   -stopFlapMode glide|stand — glide (shallow landing) or stand (stone drop)
//   -walk 1 -walkTime 5 -walkDuration 30 -walkStance 10 -walkTransfer 8 -walkFirst right
//   -useFallCatchWalk 1 -fallComLead 0.025 -fallComLeadTrigger 0.015 -plantMinDuration 0.5 -plantLeadWait 1 -reachTimeout 1 -fallAbortDeg 30
//   -catchLeadCapture 1 -catchFootLeadMin 0.035 -catchLeadMargin 0.015 (0 = former constant catchFootLead)
//   -plantCaughtLead 0.05 -plantArrestCop 0.5 — Plant roll latch and the
//   CoP-only lead arrest (0 / 0 = former lead ≤ 0 latch with the saturating
//   brake). Recover swap does not wait for the band.
    //   -fallPelvisLean 12 -fallLeanScale 0.75 -catchKneeFlex 55 -reachKneeFlex 140 -fallStanceVault 1 -fallSwingThighHold 1 -fallSwingHipLift 0.25 -fallSwingAnkleLift 0.25 -fallSwingAirAnkle 1 -fallSwingAirHeel 10 -fallSwingHeelPlant 1 -fallSwingScrapeExtend 1 -fallSwingCatchLandKnee 1 -fallSwingPlantStanceKnee 1 -fallSwingVaultRamp 1 -fallSwingCatchVaultYield 1 -fallSwingArmClock 1 -fallSwingReachLiftClock 1 -fallSwingReachArmClock 1 -fallSwingCatchArmClock 1 -fallSwingCatchExtendClock 1 -fallSwingCatchReachResume 1 -fallSwingCatchReachScrapeResume 1 -fallSwingCatchReachScrapeLiftHold 1 -fallSwingCatchReachScrapeLiftAirAge 1 -fallSwingCatchReachScrapeVaultHold 1 -fallSwingCatchReachScrapeVaultAirAge 1 -fallSwingCatchReachScrapeArmHold 1 -fallSwingCatchReachScrapeArmAirAge 1 -fallSwingCatchReachScrapeBrakeHold 1 -fallSwingCatchReachScrapeBrakeAirAge 1 -fallSwingCatchReachScrapeYieldHold 1 -fallSwingCatchReachScrapeYieldAirAge 1 -fallSwingCatchReachScrapeExtendHold 1 -fallSwingCatchReachScrapeExtendAirAge 1 -fallSwingCatchReachVaultClock 1 -fallSwingCatchReachVaultAge 1 -fallSwingCatchReachArmClock 1 -fallSwingCatchReachLiftClock 1 -fallSwingCatchReachLiftAge 1 -fallSwingCatchReachArmAge 1 -fallSwingCatchAirVaultHold 1 -fallSwingCatchAirVaultRemain 1 -fallSwingCatchAirArmHold 1 -fallSwingCatchAirExtendHold 1 -fallSwingCatchScrapeExtendHold 1 -fallSwingCatchScrapeExtendAirAge 1 -fallSwingCatchScrapeExtendAirEnterHold 1 -fallSwingCatchReachScrapeExtendEnterHold 1 -fallSwingCatchAirLiftHold 1 -fallSwingCatchScrapeLiftAirHold 1 -fallSwingCatchScrapeLiftAirEnterHold 1 -fallSwingCatchScrapeLiftAirCatchHold 1 -fallSwingCatchScrapeLiftAirCatchFold 1 -fallSwingCatchScrapeLiftAirEnterFold 1 -fallSwingCatchScrapeLiftAirEnterArm 1 -fallSwingCatchScrapeLiftAirCatchArm 1 -fallSwingCatchScrapeLiftAirCatchEndArm 1 -fallSwingCatchScrapeLiftAirCatchGroundArm 1 -fallSwingCatchScrapeLiftAirCatchAirArm 1 -fallSwingCatchScrapeLiftAirCatchAirEndArm 1 -fallSwingCatchScrapeLiftAirCatchAirGroundArm 1 -fallSwingCatchScrapeLiftAirEnterGroundArm 1 -fallSwingCatchScrapeLiftAirEnterGroundFold 1 -fallSwingCatchScrapeLiftAirCatchGroundFold 1 -fallSwingCatchScrapeLiftAirCatchAirGroundFold 1 -fallSwingCatchScrapeLiftAirCatchAirFold 1 -fallSwingCatchScrapeLiftAirCatchAirEnterFold 1 -fallSwingCatchScrapeLiftAirCatchAirEnterArm 1 -fallSwingCatchScrapeLiftAirCatchAirEnterGroundArm 1 -fallSwingCatchScrapeLiftAirCatchAirEnterGroundFold 1 -fallSwingCatchScrapeLiftAirCatchAirEnterEndArm 1 -fallSwingCatchScrapeLiftAirCatchAirEnterCatchArm 1 -fallSwingCatchScrapeLiftAirCatchAirEnterCatchFold 1 -fallSwingCatchScrapeLiftAirCatchAirEnterEndFold 1 -fallSwingCatchScrapeLiftAirCatchEndFold 1 -fallSwingCatchScrapeLiftAirCatchGroundCatchFold 1 -fallSwingCatchScrapeLiftAirCatchGroundCatchEndFold 1 -fallSwingCatchScrapeLiftAirCatchAirEndFold 1 -fallSwingCatchScrapeLiftAirCatchAirGroundCatchFold 1 -fallSwingCatchScrapeLiftAirCatchPlantExitFold 1 -fallSwingPlantStanceExtendClock 1 -fallSwingVaultFromLeftover 1 -fallSwingFallAnklePush 1 -fallSwingFallHipLiftCap 1 -fallSwingFallAnkleLift 1 -fallSwingFallHipLift 1 -fallSwingReachAirHold 1 -fallSwingReachHipLiftCap 1 -fallSwingReachHipLift 1 -fallSwingReachAnkleLift 1 -fallSwingReachHipLiftCapHold 1 -fallSwingFallHipLiftCapHold 1 -fallSwingFallVaultLead 1 -fallSwingFallLiftLead 1 -fallSwingFallFoldLead 1 -fallSwingCatchAirHipLiftCap 1 -fallSwingCatchAirHipLiftCapHold 1 -fallSwingCatchAirHipLift 1 -fallSwingCatchAirAnkleLift 1 -fallSwingCatchScrapeHipLiftCap 1 -fallSwingCatchScrapeHipLiftCapHold 1 -fallSwingCatchScrapeHipLift 1 -fallSwingCatchScrapeAnkleLift 1 -fallSwingCatchScrapeLiftLead 1 -fallSwingCatchScrapeHipLiftCapWide 1 -fallSwingCatchScrapeHipLiftCapCeil 1 -fallSwingCatchReachScrapeHipLiftCap 1 -fallSwingCatchReachScrapeHipLiftCapHold 1 -fallSwingCatchReachScrapeHipLift 1 -fallSwingCatchReachScrapeAnkleLift 1 -fallSwingCatchReachScrapeHipLiftCapWide 1 -fallSwingCatchReachScrapeHipLiftCapCeil 1 -fallSwingCatchAirHold 1 -fallSwingCatchAirEnterHold 1 -fallSwingPlantAirHold 1 -fallSwingReachAirSwingCeil 1 -fallSwingHipairCeil 1 -fallSwingRecoverHipairClear 1 — WALK-2 FSM
//   Play/WebGL uses StepPhaseDriver field defaults (useFallCatchWalk 1,
//   reachTimeout 1, fallAbortDeg 30, reachKneeFlex 140). The stand still
//   defaults -useFallCatchWalk 0 so a quiet -walk 1 stays the legacy path.
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

    // -visual without -nographics: CameraFollow.LateUpdate may run, and
    // HumanSegment uses collider-sized sprites instead of 1 m placeholders.
    public static bool VisualCapture { get; private set; }

    // Safety cap: if physics hangs, the process must not live forever.
    private const float REAL_TIME_LIMIT_SECONDS = 600f;
    private static readonly WaitForFixedUpdate FixedStep = new WaitForFixedUpdate();

    private float duration = 10f;
    private string label = "trial";
    private string outputFolder;
    private bool visual;
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

    // March in place: clocked standLeg, not walk / WalkLift / SIMBICON.
    private float marchTarget;
    private float marchTime = 5f;
    private float marchDuration = 24f;
    private float marchHold = 2.5f;
    private float marchPlant = 1.5f;
    private float marchFirst = -1f;

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
    private float walkTransferTouchdownMaxAge = 1.2f;
    private float walkTransferComMax;
    private float walkTransferFallbackComMax;
    private float walkTransferLevelMax;
    private float walkFirstStance;
    private int useFallCatchWalk;
    private float fallComLead;
    private float fallComLeadTrigger;
    private float fallPelvisLean;
    private float fallForwardLeanScale;
    private float catchKneeFlex;
    private float catchLeadCapture;
    private float catchFootLeadMin;
    private float catchLeadCaptureMargin;
    private float reachKneeFlex;
    private float fallStanceVault;
    private float fallSwingThighHold;
    private float fallSwingHipLift;
    private float fallSwingAnkleLift;
    private float fallSwingAirAnkle;
    private float fallSwingAirHeel;
    private float fallSwingHeelPlant;
    private float fallSwingScrapeExtend;
    private float fallSwingCatchLandKnee;
    private float fallSwingPlantStanceKnee;
    // Plant mid-stance extension of the new stance knee. 0 = former frozen
    // landDeg for the whole phase.
    private float fallSwingPlantMidStanceExtend;
    private float fallSwingVaultRamp;
    private float fallSwingCatchVaultYield;
    private float fallSwingArmClock;
    private float fallSwingReachLiftClock;
    private float fallSwingReachArmClock;
    private float fallSwingCatchArmClock;
    private float fallSwingCatchExtendClock;
    private float fallSwingCatchReachResume;
    private float fallSwingCatchReachScrapeResume;
    private float fallSwingCatchReachScrapeLiftHold;
    private float fallSwingCatchReachScrapeLiftAirAge;
    private float fallSwingCatchReachScrapeVaultHold;
    private float fallSwingCatchReachScrapeVaultAirAge;
    private float fallSwingCatchReachScrapeArmHold;
    private float fallSwingCatchReachScrapeArmAirAge;
    private float fallSwingCatchReachScrapeBrakeHold;
    private float fallSwingCatchReachScrapeBrakeAirAge;
    private float fallSwingCatchReachScrapeYieldHold;
    private float fallSwingCatchReachScrapeYieldAirAge;
    private float fallSwingCatchReachScrapeExtendHold;
    private float fallSwingCatchReachScrapeExtendAirAge;
    private float fallSwingCatchReachVaultClock;
    private float fallSwingCatchReachVaultAge;
        private float fallSwingCatchReachArmClock;
        private float fallSwingCatchReachLiftClock;
        private float fallSwingCatchReachLiftAge;
        private float fallSwingCatchReachArmAge;
    private float fallSwingCatchAirVaultHold;
    private float fallSwingCatchAirVaultRemain;
    private float fallSwingCatchAirArmHold;
    private float fallSwingCatchAirExtendHold;
    private float fallSwingCatchScrapeExtendHold;
    private float fallSwingCatchScrapeExtendAirAge;
    private float fallSwingCatchScrapeExtendAirEnterHold;
    private float fallSwingCatchReachScrapeExtendEnterHold;
    private float fallSwingCatchAirLiftHold;
    private float fallSwingCatchScrapeLiftAirHold;
    private float fallSwingCatchScrapeLiftAirEnterHold;
    private float fallSwingCatchScrapeLiftAirCatchHold;
    private float fallSwingCatchScrapeLiftAirCatchFold;
    private float fallSwingCatchScrapeLiftAirEnterFold;
    private float fallSwingCatchScrapeLiftAirEnterArm;
    private float fallSwingCatchScrapeLiftAirCatchArm;
    private float fallSwingCatchScrapeLiftAirCatchEndArm;
    private float fallSwingCatchScrapeLiftAirCatchGroundArm;
    private float fallSwingCatchScrapeLiftAirCatchAirArm;
    private float fallSwingCatchScrapeLiftAirCatchAirEndArm;
    private float fallSwingCatchScrapeLiftAirCatchAirGroundArm;
    private float fallSwingCatchScrapeLiftAirEnterGroundArm;
    private float fallSwingCatchScrapeLiftAirEnterGroundFold;
    private float fallSwingCatchScrapeLiftAirCatchGroundFold;
    private float fallSwingCatchScrapeLiftAirCatchAirGroundFold;
    private float fallSwingCatchScrapeLiftAirCatchAirFold;
    private float fallSwingCatchScrapeLiftAirCatchAirEnterFold;
    private float fallSwingCatchScrapeLiftAirCatchAirEnterArm;
    private float fallSwingCatchScrapeLiftAirCatchAirEnterGroundArm;
    private float fallSwingCatchScrapeLiftAirCatchAirEnterGroundFold;
    private float fallSwingCatchScrapeLiftAirCatchAirEnterEndArm;
    private float fallSwingCatchScrapeLiftAirCatchAirEnterCatchArm;
    private float fallSwingCatchScrapeLiftAirCatchAirEnterCatchFold;
    private float fallSwingCatchScrapeLiftAirCatchAirEnterEndFold;
    private float fallSwingCatchScrapeLiftAirCatchEndFold;
    private float fallSwingCatchScrapeLiftAirCatchGroundCatchFold;
    private float fallSwingCatchScrapeLiftAirCatchGroundCatchEndFold;
    private float fallSwingCatchScrapeLiftAirCatchAirEndFold;
    private float fallSwingCatchScrapeLiftAirCatchAirGroundCatchFold;
    private float fallSwingCatchScrapeLiftAirCatchPlantExitFold;
    private float fallSwingPlantStanceExtendClock;
    private float fallSwingVaultFromLeftover;
    private float fallSwingFallAnklePush;
    private float fallSwingFallHipLiftCap;
    private float fallSwingFallAnkleLift;
    private float fallSwingFallHipLift;
    private float fallSwingReachAirHold;
    private float fallSwingFallAirHold;
    private float fallSwingFallHipLiftCapWide;
    private float fallSwingReachHipLiftCapWide;
    private float fallSwingCatchAirHipLiftCapWide;
    private float fallSwingPlantHipLiftCapWide;
    private float fallSwingFallHipLiftCapCeil;
    private float fallSwingReachHipLiftCapCeil;
    private float fallSwingCatchAirHipLiftCapCeil;
    private float fallSwingPlantHipLiftCapCeil;
    private float fallSwingCatchScrapeHipLiftCapCeil;
    private float fallSwingCatchReachScrapeHipLiftCapCeil;
    private float fallSwingCatchAirHold;
    private float fallSwingCatchAirEnterHold;
    private float fallSwingPlantAirHold;
    private float fallSwingReachAirSwingCeil;
    private float fallSwingHipairCeil;
    private float fallSwingRecoverHipairClear;
    private float fallSwingRecoverAirAnkleClear;
    private float fallSwingPlantAirAnkleClear;
    private float fallSwingPlantAirAnkleLevel;
    private float fallSwingRecoverAirKneeClear;
    private float fallSwingReachHipLiftCap;
    private float fallSwingReachHipLift;
    private float fallSwingReachAnkleLift;
    private float fallSwingReachHipLiftCapHold;
    private float fallSwingFallHipLiftCapHold;
    private float fallSwingFallVaultLead;
    private float fallSwingFallLiftLead;
    private float fallSwingFallFoldLead;
    private float fallSwingCatchAirHipLiftCap;
    private float fallSwingCatchAirHipLiftCapHold;
    private float fallSwingCatchAirHipLift;
    private float fallSwingCatchAirAnkleLift;
    private float fallSwingCatchScrapeHipLiftCap;
    private float fallSwingCatchScrapeHipLiftCapHold;
    private float fallSwingCatchScrapeHipLift;
    private float fallSwingCatchScrapeAnkleLift;
    private float fallSwingCatchScrapeLiftLead;
    private float fallSwingCatchScrapeHipLiftCapWide;
    private float fallSwingCatchReachScrapeHipLiftCap;
    private float fallSwingCatchReachScrapeHipLiftCapHold;
    private float fallSwingCatchReachScrapeHipLift;
    private float fallSwingCatchReachScrapeAnkleLift;
    private float fallSwingCatchReachScrapeHipLiftCapWide;
    private float plantMinDuration;
    private float plantLeadWaitSeconds;
    private float reachTimeout;
    private float fallAbortDeg;
    private float plantStallReseatSeconds;
    private float plantReseatLeadMargin;
    private float plantStallStandSeconds;
    private float plantClearRestoreOnGate;
    private float reachGroundStallSeconds;
    private float fallCatchBounceMax;
    private float sinkAbortDrop;
    private float plantAirLowestFoot;
    private float walkRecoverAnyPose;
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
    private bool spawnApple;
    private AppleTree appleTree;
    private AppleTrialRecorder appleRecorder;
    private PlantFlower[] trialFlowers;
    private FlowerTrialRecorder flowerRecorder;
    private bool spawnDog;
    private bool spawnDogBird;
    private float dogBirdX = 2f;
    private Dog dog;
    private DogTrialRecorder dogRecorder;
    private bool dogLeap;
    private float dogLeapTime = 2f;
    private float dogPreyX = 1.4f;
    private bool dogWalk;
    private float dogWalkTime = 1f;
    private Bird bird;
    private Bird[] trialBirds;
    private BirdFlockDrive trialFlockDrive;
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
        VisualCapture = Array.IndexOf(args, "-visual") >= 0
            && Array.IndexOf(args, "-nographics") < 0;

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
        visual = HasArg("-visual");
        if (visual && !VisualCapture)
        {
            Debug.LogWarning("HeadlessTrial: -visual skipped under -nographics.");
            visual = false;
        }
        spawnBird = GetFloatArg("-bird", 0f) > 0.5f;
        spawnTree = GetFloatArg("-tree", 0f) > 0.5f;
        spawnFlower = GetFloatArg("-flower", 0f) > 0.5f;
        spawnApple = GetFloatArg("-apple", 0f) > 0.5f;
        treePerch = GetFloatArg("-treePerch", spawnTree && spawnBird ? 1f : 0f) > 0.5f;
        birdDriveKind = GetStringArg("-birdDrive", "").Trim().ToLowerInvariant();
        trialBirdKind = Bird.ParseKind(GetStringArg("-birdKind", "crow"));
        birdDriveCount = Mathf.Max(1, Mathf.RoundToInt(GetFloatArg("-birdCount", 1f)));
        birdDriveSeed = Mathf.RoundToInt(GetFloatArg("-birdDriveSeed", 1f));
        spawnDog = GetFloatArg("-dog", 0f) > 0.5f;
        spawnDogBird = GetFloatArg("-dogBird", 0f) > 0.5f;
        dogBirdX = GetFloatArg("-dogBirdX", 2f);
        dogLeap = GetFloatArg("-dogLeap", 0f) > 0.5f;
        dogLeapTime = GetFloatArg("-dogLeapTime", 2f);
        dogPreyX = GetFloatArg("-dogPreyX", 1.4f);
        dogWalk = GetFloatArg("-dogWalk", 0f) > 0.5f;
        dogWalkTime = GetFloatArg("-dogWalkTime", 1f);
        // Hunt: a live bird in the dog trial, not a DogPrey mark.
        if (spawnDog && spawnDogBird)
            spawnBird = true;
        // Empty drive on a dog+bird scene is wander so Flee can Fly.
        if (spawnDog && spawnBird && string.IsNullOrEmpty(birdDriveKind))
            birdDriveKind = "wander";
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
        marchTarget = Mathf.Clamp01(GetFloatArg("-march", 0f));
        marchTime = GetFloatArg("-marchTime", 5f);
        marchDuration = GetFloatArg("-marchDuration", 24f);
        marchHold = GetFloatArg("-marchHold", 2.5f);
        marchPlant = GetFloatArg("-marchPlant", 1.5f);
        marchFirst = ParseStandLegArg(GetStringArg("-marchFirst", "left"));
        if (Mathf.Abs(marchFirst) < 0.5f)
            marchFirst = -1f;

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
        walkTransferTouchdownMaxAge = GetFloatArg("-walkTransferTouchdownMaxAge", 1.2f);
        walkTransferComMax = GetFloatArg("-walkTransferCom", 0.10f);
        walkTransferFallbackComMax = GetFloatArg("-walkTransferFallbackCom", 0.12f);
        walkTransferLevelMax = GetFloatArg("-walkTransferLevel", 0.05f);
        // -useFallCatchWalk stays 0 unless Extra asks: WALK-2 bypass for
        // legacy -walk 1. Play AddComponent uses the driver field (1).
        // reachTimeout / fallAbortDeg / reachKneeFlex defaults must match
        // StepPhaseDriver — Play never runs this override.
        useFallCatchWalk = Mathf.RoundToInt(GetFloatArg("-useFallCatchWalk", 0f));
        fallComLead = GetFloatArg("-fallComLead", 0.025f);
        fallComLeadTrigger = GetFloatArg("-fallComLeadTrigger", 0.015f);
        fallPelvisLean = GetFloatArg("-fallPelvisLean", 12f);
        fallForwardLeanScale = GetFloatArg("-fallLeanScale", 0.75f);
        catchKneeFlex = GetFloatArg("-catchKneeFlex", 55f);
        // Capture-point Catch lead. -catchLeadCapture 0 restores the former
        // constant catchFootLead (0.08 m) in both the criterion and the
        // commanded step that is synced to it.
        catchLeadCapture = GetFloatArg("-catchLeadCapture", 1f);
        catchFootLeadMin = GetFloatArg("-catchFootLeadMin", 0.035f);
        catchLeadCaptureMargin = GetFloatArg("-catchLeadMargin", 0.015f);
        reachKneeFlex = GetFloatArg("-reachKneeFlex", 140f);
        fallStanceVault = GetFloatArg("-fallStanceVault", 1f);
        fallSwingThighHold = GetFloatArg("-fallSwingThighHold", 1f);
        fallSwingHipLift = GetFloatArg("-fallSwingHipLift", 0.25f);
        fallSwingAnkleLift = GetFloatArg("-fallSwingAnkleLift", 0.25f);
        fallSwingAirAnkle = GetFloatArg("-fallSwingAirAnkle", 1f);
        fallSwingAirHeel = GetFloatArg("-fallSwingAirHeel", 10f);
        fallSwingHeelPlant = GetFloatArg("-fallSwingHeelPlant", 1f);
        fallSwingScrapeExtend = GetFloatArg("-fallSwingScrapeExtend", 1f);
        fallSwingCatchLandKnee = GetFloatArg("-fallSwingCatchLandKnee", 1f);
        fallSwingPlantStanceKnee = GetFloatArg("-fallSwingPlantStanceKnee", 1f);
        fallSwingPlantMidStanceExtend = GetFloatArg("-walkPlantMidExtend", 1f);
        fallSwingVaultRamp = GetFloatArg("-fallSwingVaultRamp", 1f);
        fallSwingCatchVaultYield = GetFloatArg("-fallSwingCatchVaultYield", 1f);
        fallSwingArmClock = GetFloatArg("-fallSwingArmClock", 1f);
        fallSwingReachLiftClock = GetFloatArg("-fallSwingReachLiftClock", 1f);
        fallSwingReachArmClock = GetFloatArg("-fallSwingReachArmClock", 1f);
        fallSwingCatchArmClock = GetFloatArg("-fallSwingCatchArmClock", 1f);
        fallSwingCatchExtendClock = GetFloatArg("-fallSwingCatchExtendClock", 1f);
        fallSwingCatchReachResume = GetFloatArg("-fallSwingCatchReachResume", 1f);
        fallSwingCatchReachScrapeResume = GetFloatArg("-fallSwingCatchReachScrapeResume", 1f);
        fallSwingCatchReachScrapeLiftHold = GetFloatArg("-fallSwingCatchReachScrapeLiftHold", 1f);
        fallSwingCatchReachScrapeLiftAirAge = GetFloatArg("-fallSwingCatchReachScrapeLiftAirAge", 1f);
        fallSwingCatchReachScrapeVaultHold = GetFloatArg("-fallSwingCatchReachScrapeVaultHold", 1f);
        fallSwingCatchReachScrapeVaultAirAge = GetFloatArg("-fallSwingCatchReachScrapeVaultAirAge", 1f);
        fallSwingCatchReachScrapeArmHold = GetFloatArg("-fallSwingCatchReachScrapeArmHold", 1f);
        fallSwingCatchReachScrapeArmAirAge = GetFloatArg("-fallSwingCatchReachScrapeArmAirAge", 1f);
        fallSwingCatchReachScrapeBrakeHold = GetFloatArg("-fallSwingCatchReachScrapeBrakeHold", 1f);
        fallSwingCatchReachScrapeBrakeAirAge = GetFloatArg("-fallSwingCatchReachScrapeBrakeAirAge", 1f);
        fallSwingCatchReachScrapeYieldHold = GetFloatArg("-fallSwingCatchReachScrapeYieldHold", 1f);
        fallSwingCatchReachScrapeYieldAirAge = GetFloatArg("-fallSwingCatchReachScrapeYieldAirAge", 1f);
        fallSwingCatchReachScrapeExtendHold = GetFloatArg("-fallSwingCatchReachScrapeExtendHold", 1f);
        fallSwingCatchReachScrapeExtendAirAge = GetFloatArg("-fallSwingCatchReachScrapeExtendAirAge", 1f);
        fallSwingCatchReachVaultClock = GetFloatArg("-fallSwingCatchReachVaultClock", 1f);
        fallSwingCatchReachVaultAge = GetFloatArg("-fallSwingCatchReachVaultAge", 1f);
        fallSwingCatchReachArmClock = GetFloatArg("-fallSwingCatchReachArmClock", 1f);
        fallSwingCatchReachLiftClock = GetFloatArg("-fallSwingCatchReachLiftClock", 1f);
        fallSwingCatchReachLiftAge = GetFloatArg("-fallSwingCatchReachLiftAge", 1f);
        fallSwingCatchReachArmAge = GetFloatArg("-fallSwingCatchReachArmAge", 1f);
        fallSwingCatchAirVaultHold = GetFloatArg("-fallSwingCatchAirVaultHold", 1f);
        fallSwingCatchAirVaultRemain = GetFloatArg("-fallSwingCatchAirVaultRemain", 1f);
        fallSwingCatchAirArmHold = GetFloatArg("-fallSwingCatchAirArmHold", 1f);
        fallSwingCatchAirExtendHold = GetFloatArg("-fallSwingCatchAirExtendHold", 1f);
        fallSwingCatchScrapeExtendHold = GetFloatArg("-fallSwingCatchScrapeExtendHold", 1f);
        fallSwingCatchScrapeExtendAirAge = GetFloatArg("-fallSwingCatchScrapeExtendAirAge", 1f);
        fallSwingCatchScrapeExtendAirEnterHold = GetFloatArg("-fallSwingCatchScrapeExtendAirEnterHold", 1f);
        fallSwingCatchReachScrapeExtendEnterHold = GetFloatArg("-fallSwingCatchReachScrapeExtendEnterHold", 1f);
        fallSwingCatchAirLiftHold = GetFloatArg("-fallSwingCatchAirLiftHold", 1f);
        fallSwingCatchScrapeLiftAirHold = GetFloatArg("-fallSwingCatchScrapeLiftAirHold", 1f);
        fallSwingCatchScrapeLiftAirEnterHold = GetFloatArg("-fallSwingCatchScrapeLiftAirEnterHold", 1f);
        fallSwingCatchScrapeLiftAirCatchHold = GetFloatArg("-fallSwingCatchScrapeLiftAirCatchHold", 1f);
        fallSwingCatchScrapeLiftAirCatchFold = GetFloatArg("-fallSwingCatchScrapeLiftAirCatchFold", 1f);
        fallSwingCatchScrapeLiftAirEnterFold = GetFloatArg("-fallSwingCatchScrapeLiftAirEnterFold", 1f);
        fallSwingCatchScrapeLiftAirEnterArm = GetFloatArg("-fallSwingCatchScrapeLiftAirEnterArm", 1f);
        fallSwingCatchScrapeLiftAirCatchArm = GetFloatArg("-fallSwingCatchScrapeLiftAirCatchArm", 1f);
        fallSwingCatchScrapeLiftAirCatchEndArm = GetFloatArg("-fallSwingCatchScrapeLiftAirCatchEndArm", 1f);
        fallSwingCatchScrapeLiftAirCatchGroundArm = GetFloatArg("-fallSwingCatchScrapeLiftAirCatchGroundArm", 1f);
        fallSwingCatchScrapeLiftAirCatchAirArm = GetFloatArg("-fallSwingCatchScrapeLiftAirCatchAirArm", 1f);
        fallSwingCatchScrapeLiftAirCatchAirEndArm = GetFloatArg("-fallSwingCatchScrapeLiftAirCatchAirEndArm", 1f);
        fallSwingCatchScrapeLiftAirCatchAirGroundArm = GetFloatArg("-fallSwingCatchScrapeLiftAirCatchAirGroundArm", 1f);
        fallSwingCatchScrapeLiftAirEnterGroundArm = GetFloatArg("-fallSwingCatchScrapeLiftAirEnterGroundArm", 1f);
        fallSwingCatchScrapeLiftAirEnterGroundFold = GetFloatArg("-fallSwingCatchScrapeLiftAirEnterGroundFold", 1f);
        fallSwingCatchScrapeLiftAirCatchGroundFold = GetFloatArg("-fallSwingCatchScrapeLiftAirCatchGroundFold", 1f);
        fallSwingCatchScrapeLiftAirCatchAirGroundFold = GetFloatArg("-fallSwingCatchScrapeLiftAirCatchAirGroundFold", 1f);
        fallSwingCatchScrapeLiftAirCatchAirFold = GetFloatArg("-fallSwingCatchScrapeLiftAirCatchAirFold", 1f);
        fallSwingCatchScrapeLiftAirCatchAirEnterFold = GetFloatArg("-fallSwingCatchScrapeLiftAirCatchAirEnterFold", 1f);
        fallSwingCatchScrapeLiftAirCatchAirEnterArm = GetFloatArg("-fallSwingCatchScrapeLiftAirCatchAirEnterArm", 1f);
        fallSwingCatchScrapeLiftAirCatchAirEnterGroundArm = GetFloatArg("-fallSwingCatchScrapeLiftAirCatchAirEnterGroundArm", 1f);
        fallSwingCatchScrapeLiftAirCatchAirEnterGroundFold = GetFloatArg("-fallSwingCatchScrapeLiftAirCatchAirEnterGroundFold", 1f);
        fallSwingCatchScrapeLiftAirCatchAirEnterEndArm = GetFloatArg("-fallSwingCatchScrapeLiftAirCatchAirEnterEndArm", 1f);
        fallSwingCatchScrapeLiftAirCatchAirEnterCatchArm = GetFloatArg("-fallSwingCatchScrapeLiftAirCatchAirEnterCatchArm", 1f);
        fallSwingCatchScrapeLiftAirCatchAirEnterCatchFold = GetFloatArg("-fallSwingCatchScrapeLiftAirCatchAirEnterCatchFold", 1f);
        fallSwingCatchScrapeLiftAirCatchAirEnterEndFold = GetFloatArg("-fallSwingCatchScrapeLiftAirCatchAirEnterEndFold", 1f);
        fallSwingCatchScrapeLiftAirCatchEndFold = GetFloatArg("-fallSwingCatchScrapeLiftAirCatchEndFold", 1f);
        fallSwingCatchScrapeLiftAirCatchGroundCatchFold = GetFloatArg("-fallSwingCatchScrapeLiftAirCatchGroundCatchFold", 1f);
        fallSwingCatchScrapeLiftAirCatchGroundCatchEndFold = GetFloatArg("-fallSwingCatchScrapeLiftAirCatchGroundCatchEndFold", 1f);
        fallSwingCatchScrapeLiftAirCatchAirEndFold = GetFloatArg("-fallSwingCatchScrapeLiftAirCatchAirEndFold", 1f);
        fallSwingCatchScrapeLiftAirCatchAirGroundCatchFold = GetFloatArg("-fallSwingCatchScrapeLiftAirCatchAirGroundCatchFold", 1f);
        fallSwingCatchScrapeLiftAirCatchPlantExitFold = GetFloatArg("-fallSwingCatchScrapeLiftAirCatchPlantExitFold", 1f);
        fallSwingPlantStanceExtendClock = GetFloatArg("-fallSwingPlantStanceExtendClock", 1f);
        fallSwingVaultFromLeftover = GetFloatArg("-fallSwingVaultFromLeftover", 1f);
        fallSwingFallAnklePush = GetFloatArg("-fallSwingFallAnklePush", 1f);
        fallSwingFallHipLiftCap = GetFloatArg("-fallSwingFallHipLiftCap", 1f);
        fallSwingFallAnkleLift = GetFloatArg("-fallSwingFallAnkleLift", 1f);
        fallSwingFallHipLift = GetFloatArg("-fallSwingFallHipLift", 1f);
        fallSwingReachAirHold = GetFloatArg("-fallSwingReachAirHold", 1f);
        // Fall's own first air (23:39 can now produce it). 0 = former hipair jump.
        fallSwingFallAirHold = GetFloatArg("-fallSwingFallAirHold", 1f);
        // Fall 16° cap has room=0 once 23:47 keeps ThighHold there. 0 = former 16°.
        fallSwingFallHipLiftCapWide = GetFloatArg("-fallSwingFallHipLiftCapWide", 1f);
        // Reach 16° cap has room=0 once 23:53 walked Fall into 24°. 0 = former 16°.
        fallSwingReachHipLiftCapWide = GetFloatArg("-fallSwingReachHipLiftCapWide", 1f);
        // Catch-air 16° cap has room=0 once 23:58 walked Reach into 24°. 0 = former 16°.
        fallSwingCatchAirHipLiftCapWide = GetFloatArg("-fallSwingCatchAirHipLiftCapWide", 1f);
        // Plant 16° cap has room=0 once leftover + hipLift 0.5 sit there. 0 = former 16°.
        fallSwingPlantHipLiftCapWide = GetFloatArg("-fallSwingPlantHipLiftCapWide", 1f);
        // Fall 24° cap has room=0 once 00:09 walked Plant leftover into 24°. 0 = former 24°.
        fallSwingFallHipLiftCapCeil = GetFloatArg("-fallSwingFallHipLiftCapCeil", 1f);
        // Reach 24° cap has room=0 once 00:16 walked Fall into 32°. 0 = former 24°.
        fallSwingReachHipLiftCapCeil = GetFloatArg("-fallSwingReachHipLiftCapCeil", 1f);
        // Catch-air 24° cap has room=0 once 00:25 walked Reach into 32°. 0 = former 24°.
        fallSwingCatchAirHipLiftCapCeil = GetFloatArg("-fallSwingCatchAirHipLiftCapCeil", 1f);
        // Plant 24° cap has room=0 once leftover + hipLift 0.5 sit there. 0 = former 24°.
        fallSwingPlantHipLiftCapCeil = GetFloatArg("-fallSwingPlantHipLiftCapCeil", 1f);
        // Catch scrape 24° cap has room=0 once 00:28 walked Catch-air into 32°. 0 = former 24°.
        fallSwingCatchScrapeHipLiftCapCeil = GetFloatArg("-fallSwingCatchScrapeHipLiftCapCeil", 1f);
        // Catch→Reach scrape 24° cap has room=0 once 00:36 walked Catch scrape into 32°. 0 = former 24°.
        fallSwingCatchReachScrapeHipLiftCapCeil = GetFloatArg("-fallSwingCatchReachScrapeHipLiftCapCeil", 1f);
        // Reach→Catch timeout air still takes hipair −swingHipFlex. 0 = former latch jump.
        fallSwingCatchAirHold = GetFloatArg("-fallSwingCatchAirHold", 1f);
        // Catch-air keep still lost the hip on Catch→Reach bounce. 0 = former latch jump.
        fallSwingCatchAirEnterHold = GetFloatArg("-fallSwingCatchAirEnterHold", 1f);
        // Plant 32° fold still lost the hip on Plant's own first air. 0 = former latch jump.
        fallSwingPlantAirHold = GetFloatArg("-fallSwingPlantAirHold", 1f);
        // Plant-air leftover into Reach still ramped reachAirSwing to −swingHipFlex. 0 = former −40°.
        fallSwingReachAirSwingCeil = GetFloatArg("-fallSwingReachAirSwingCeil", 1f);
        // Recover lost-toe hipair still took −swingHipFlex (01:12 is Reach ramp only). 0 = former −40°.
        fallSwingHipairCeil = GetFloatArg("-fallSwingHipairCeil", 1f);
        // Recover still took the clamped pose (landing hip off). 0 = former 01:23 clamp.
        fallSwingRecoverHipairClear = GetFloatArg("-fallSwingRecoverHipairClear", 1f);
        // Recover-air still SPD latched ankle to joint=0 (foot ⊥ shin). 0 = former joint=0.
        fallSwingRecoverAirAnkleClear = GetFloatArg("-fallSwingRecoverAirAnkleClear", 1f);
        // Plant-air still SPD latched ankle to joint=0 (foot ⊥ shin). 0 = former joint=0.
        fallSwingPlantAirAnkleClear = GetFloatArg("-fallSwingPlantAirAnkleClear", 1f);
        // Plant-air drive world-flat instead of quiet / joint=0. 0 = former 01:45 skip.
        fallSwingPlantAirAnkleLevel = GetFloatArg("-fallSwingPlantAirAnkleLevel", 1f);
        // Recover-air still SPD swing knee to landDeg. 0 = former extend yank.
        fallSwingRecoverAirKneeClear = GetFloatArg("-fallSwingRecoverAirKneeClear", 1f);
        fallSwingReachHipLiftCap = GetFloatArg("-fallSwingReachHipLiftCap", 1f);
        fallSwingReachHipLift = GetFloatArg("-fallSwingReachHipLift", 1f);
        fallSwingReachAnkleLift = GetFloatArg("-fallSwingReachAnkleLift", 1f);
        fallSwingReachHipLiftCapHold = GetFloatArg("-fallSwingReachHipLiftCapHold", 1f);
        fallSwingFallHipLiftCapHold = GetFloatArg("-fallSwingFallHipLiftCapHold", 1f);
        fallSwingFallVaultLead = GetFloatArg("-fallSwingFallVaultLead", 1f);
        fallSwingFallLiftLead = GetFloatArg("-fallSwingFallLiftLead", 1f);
        fallSwingFallFoldLead = GetFloatArg("-fallSwingFallFoldLead", 1f);
        fallSwingCatchAirHipLiftCap = GetFloatArg("-fallSwingCatchAirHipLiftCap", 1f);
        fallSwingCatchAirHipLiftCapHold = GetFloatArg("-fallSwingCatchAirHipLiftCapHold", 1f);
        fallSwingCatchAirHipLift = GetFloatArg("-fallSwingCatchAirHipLift", 1f);
        fallSwingCatchAirAnkleLift = GetFloatArg("-fallSwingCatchAirAnkleLift", 1f);
        fallSwingCatchScrapeHipLiftCap = GetFloatArg("-fallSwingCatchScrapeHipLiftCap", 1f);
        fallSwingCatchScrapeHipLiftCapHold = GetFloatArg("-fallSwingCatchScrapeHipLiftCapHold", 1f);
        fallSwingCatchScrapeHipLift = GetFloatArg("-fallSwingCatchScrapeHipLift", 1f);
        fallSwingCatchScrapeAnkleLift = GetFloatArg("-fallSwingCatchScrapeAnkleLift", 1f);
        fallSwingCatchScrapeLiftLead = GetFloatArg("-fallSwingCatchScrapeLiftLead", 1f);
        fallSwingCatchScrapeHipLiftCapWide = GetFloatArg("-fallSwingCatchScrapeHipLiftCapWide", 1f);
        fallSwingCatchReachScrapeHipLiftCap = GetFloatArg("-fallSwingCatchReachScrapeHipLiftCap", 1f);
        fallSwingCatchReachScrapeHipLiftCapHold = GetFloatArg("-fallSwingCatchReachScrapeHipLiftCapHold", 1f);
        fallSwingCatchReachScrapeHipLift = GetFloatArg("-fallSwingCatchReachScrapeHipLift", 1f);
        fallSwingCatchReachScrapeAnkleLift = GetFloatArg("-fallSwingCatchReachScrapeAnkleLift", 1f);
        fallSwingCatchReachScrapeHipLiftCapWide = GetFloatArg("-fallSwingCatchReachScrapeHipLiftCapWide", 1f);
        plantMinDuration = GetFloatArg("-plantMinDuration", 0.50f);
        // Roll-over hold before a new Fall. -plantLeadWait 0 or
        // -fallComLeadTrigger 0 each restore the former release on
        // plantMinDuration alone.
        plantLeadWaitSeconds = GetFloatArg("-plantLeadWait", 1.0f);
        reachTimeout = GetFloatArg("-reachTimeout", 1.0f);
        fallAbortDeg = GetFloatArg("-fallAbortDeg", 30f);
        // Recovery ladder. Every one of these is inert on a healthy cycle;
        // 0 restores the former "no deadline" behavior for each rung
        // separately, so a regression can name the rung it blames.
        plantStallReseatSeconds = GetFloatArg("-plantStallReseat", 1.5f);
        plantReseatLeadMargin = GetFloatArg("-plantReseatLead", 0.05f);
        plantStallStandSeconds = GetFloatArg("-plantStallStand", 3f);
        // StandingRestore latched until Fall after a TiltSettled flicker, so
        // Plant push-off stayed off. 0 = former latch.
        plantClearRestoreOnGate = GetFloatArg("-plantClearRestoreOnGate", 1f);
        reachGroundStallSeconds = GetFloatArg("-reachGroundStall", 1.5f);
        fallCatchBounceMax = GetFloatArg("-fallCatchBounceMax", 4f);
        sinkAbortDrop = GetFloatArg("-sinkAbortDrop", 0.25f);
        plantAirLowestFoot = GetFloatArg("-plantAirLowestFoot", 1f);
        walkRecoverAnyPose = GetFloatArg("-walkRecoverAnyPose", 1f);
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
        bool wideBird = spawnBird && (birdDriveKind == "wander" || birdDriveKind == "prey" || birdDriveCount > 1);
        bool dogBirdScene = spawnDog && spawnBird;
        float packSpan = Mathf.Max(0, birdDriveCount - 1) * BirdPackSpacing;
        float birdGroundWidth = Mathf.Max(48f, packSpan + 16f);
        ground.groundSize = new Vector2(wideBird || dogBirdScene ? birdGroundWidth : 20f, 2f);
        ground.groundPosition = new Vector2(0f, -3f);
        ground.groundFriction = GetFloatArg("-groundFriction", ground.groundFriction);
        ground.BuildGround();

        if (spawnDog)
        {
            BuildDogScene();
            if (spawnBird)
            {
                BuildBirdScene();
                WireDogBirdHunt();
            }
            return;
        }

        if (spawnTree)
            BuildTreeScene();

        if (spawnApple)
            BuildAppleScene();

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

        if (spawnTree || spawnFlower || spawnApple)
            return;

        GameObject humanObject = new GameObject("TrialHuman");
        // y = −0.82 puts the feet exactly on the top of the ground (−2.0):
        // (pelvisSize.y + torsoSize.y)/2 + thigh + shin + foot
        // = 0.25 + 0.43 + 0.43 + 0.07 = 1.18.
        humanObject.transform.position = new Vector2(0f, startY);

        human = humanObject.AddComponent<Human>();
        human.footFriction = GetFloatArg("-footFriction", human.footFriction);
        human.footHeelShare = GetFloatArg("-footHeelShare", human.footHeelShare);
        // The hip arc bounds every hip target (ControlJointToAngleStable clamps
        // to joint.limits), so the split guard on the extension side is a
        // physics parameter the stand must be able to sweep without a rebuild.
        human.hipLimitMin = GetFloatArg("-hipLimitMin", human.hipLimitMin);
        human.hipLimitMax = GetFloatArg("-hipLimitMax", human.hipLimitMax);
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
        stepDriver.walkComTriggerMinStanceFraction = GetFloatArg("-walkComTriggerMinStanceFraction", stepDriver.walkComTriggerMinStanceFraction);
        stepDriver.walkTiltAbortDeg = GetFloatArg("-walkTiltAbort", stepDriver.walkTiltAbortDeg);
        stepDriver.walkTiltRecoverDeg = GetFloatArg("-walkTiltRecover", stepDriver.walkTiltRecoverDeg);
        stepDriver.walkCadenceStanceGain = walkCadenceStanceGain;
        stepDriver.walkCadenceMinStance = walkCadenceMinStance;
        stepDriver.transferMaxDuration = walkTransferMaxDuration;
        stepDriver.transferMinDuration = walkTransferMinDuration;
        stepDriver.transferTouchdownMaxAge = walkTransferTouchdownMaxAge;
        stepDriver.walkCadenceTransferGain = walkCadenceTransferGain;
        stepDriver.walkCadenceMinTransfer = walkCadenceMinTransfer;
        stepDriver.transferComOffsetMax = walkTransferComMax;
        stepDriver.transferFallbackComMax = walkTransferFallbackComMax;
        stepDriver.transferStandLegLevelMax = walkTransferLevelMax;
        stepDriver.firstStance = walkFirstStance;
        ApplyFallCatchWalkFields(stepDriver);
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
        c.pawComDeadzone = GetFloatArg("-pawComDead", c.pawComDeadzone);
        c.pawComVelDeadzone = GetFloatArg("-pawComVelDead", c.pawComVelDeadzone);
        c.pawGroundHoldSeconds = GetFloatArg("-pawGroundHold", c.pawGroundHoldSeconds);
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
        c.heightDeadzone = GetFloatArg("-heightDead", c.heightDeadzone);
        c.heightVelDeadzone = GetFloatArg("-heightVelDead", c.heightVelDeadzone);
        c.poseHoldDeadzoneDeg = GetFloatArg("-poseDead", c.poseHoldDeadzoneDeg);
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
        c.walkStanceSweepDeg = GetFloatArg("-dogWalkSweep", c.walkStanceSweepDeg);
        c.walkChestTilt = GetFloatArg("-dogWalkChest", c.walkChestTilt);
        c.walkLumbarP = GetFloatArg("-dogWalkLumbarP", c.walkLumbarP);
        c.walkHipP = GetFloatArg("-dogWalkHipP", c.walkHipP);
        c.walkKneeP = GetFloatArg("-dogWalkKneeP", c.walkKneeP);
        c.walkActivationSpeed = GetFloatArg("-dogWalkAct", c.walkActivationSpeed);
        c.runSpeed = GetFloatArg("-dogRunSpeed", c.runSpeed);
        c.runSpeedRef = GetFloatArg("-dogRunSpeedRef", c.runSpeedRef);
        c.runStride = GetFloatArg("-dogRunStride", c.runStride);
        c.runHipFlex = GetFloatArg("-dogRunHipFlex", c.runHipFlex);
        c.runShoulderFlex = GetFloatArg("-dogRunShoulderFlex", c.runShoulderFlex);
        c.runHipExtend = GetFloatArg("-dogRunHipExt", c.runHipExtend);
        c.runShoulderExtend = GetFloatArg("-dogRunShoulderExt", c.runShoulderExtend);
        c.runKneeFlex = GetFloatArg("-dogRunKneeFlex", c.runKneeFlex);
        c.runElbowFlex = GetFloatArg("-dogRunElbowFlex", c.runElbowFlex);
        c.runChestLean = GetFloatArg("-dogRunChestLean", c.runChestLean);
        c.runPawPlant = GetFloatArg("-dogRunPlant", c.runPawPlant);
    }

    private void BuildBirdScene()
    {
        string rigDefault = spawnDogBird && !HasArg("-birdRig") ? "flock" : "ragdoll";
        string rigRaw = GetStringArg("-birdRig", rigDefault).Trim().ToLowerInvariant();
        bool flockRig = rigRaw == "flock";
        int count = flockRig && (birdDriveKind == "wander" || birdDriveKind == "prey" || treePerch)
            ? birdDriveCount
            : 1;
        // Hunt is one quarry next to the dog, not a pack on the dog's chest.
        if (spawnDog)
            count = 1;
        Bird[] pack = new Bird[count];
        float spacing = BirdPackSpacing;
        float originX = spawnDog
            ? DogHuntBirdOriginX()
            : -0.5f * (count - 1) * spacing;
        float birdY = spawnDog
            ? Bird.KindStandingRootY(trialBirdKind, -2f)
            : startY;

        for (int i = 0; i < count; i++)
        {
            GameObject birdObject = new GameObject(count == 1 ? "TrialBird" : "TrialBird_" + (i + 1));
            birdObject.transform.position = new Vector2(originX + i * spacing, birdY);
            Bird next = birdObject.AddComponent<Bird>();
            next.rig = flockRig ? BirdRig.Flock : BirdRig.Ragdoll;
            next.kind = trialBirdKind;
            next.ApplyKind(trialBirdKind);
            next.minLimbInertia = GetFloatArg("-minInertia", next.minLimbInertia);
            next.minBodyInertia = GetFloatArg("-bodyInertia", next.minBodyInertia);
            birdObject.transform.position = new Vector2(originX + i * spacing, birdY);
            FactionStamp.Bird(birdObject);
            next.BuildBird();
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
        BirdFlockDrive spawnedFlock = null;
        trialFlockDrive = null;
        if ((birdDriveKind == "wander" || birdDriveKind == "prey") && count > 1 && !driveEach)
        {
            GameObject host = new GameObject("BirdFlockDrive");
            BirdFlockDrive flock = host.AddComponent<BirdFlockDrive>();
            spawnedFlock = flock;
            trialFlockDrive = flock;
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
        else if ((birdDriveKind == "wander" || birdDriveKind == "prey") && count > 1 && driveEach)
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
            BirdDrive.Kind driveKind = birdDriveKind == "prey"
                ? BirdDrive.Kind.Prey
                : BirdDrive.Kind.Wander;
            for (int i = 0; i < pack.Length; i++)
            {
                if (pack[i] == null) continue;
                BirdDrive drive = pack[i].gameObject.AddComponent<BirdDrive>();
                drive.allowHeadless = true;
                drive.kind = driveKind;
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
        bool preyKind = birdDriveKind == "prey";
        float preyOff = GetFloatArg("-birdPrey", preyKind ? 10f : 0f);
        if (preyKind && Mathf.Abs(preyOff) < 0.01f)
            preyOff = 10f;
        if (preyKind || Mathf.Abs(preyOff) > 0.01f)
        {
            Transform preyMark = Insect.SpawnOnDirt(originX + preyOff, -2f);
            BindBirdPrey(pack, spawnedFlock, preyMark);
        }

        float threatOff = GetFloatArg("-birdThreat", 0f);
        if (Mathf.Abs(threatOff) > 0.01f)
        {
            GameObject mark = new GameObject("BirdThreat");
            mark.transform.position = new Vector2(originX + threatOff, startY);
            FactionStamp.Wolf(mark);
            float threatRange = GetFloatArg("-threatRange", 12f);
            BindBirdThreat(pack, spawnedFlock, mark.transform, threatRange);
        }
    }

    // Spawn already stamped Insects. Bind the live quarry without a scene scan.
    private static void BindBirdPrey(Bird[] pack, BirdFlockDrive flock, Transform prey)
    {
        if (prey == null)
            return;
        if (flock != null)
            flock.BindPrey(prey);
        if (pack == null)
            return;
        for (int i = 0; i < pack.Length; i++)
        {
            if (pack[i] == null)
                continue;
            BirdDrive drive = pack[i].GetComponent<BirdDrive>();
            if (drive != null)
                drive.BindPrey(i == 0 ? prey : null);
        }
    }

    // Same pack the spawn loop already holds. Not FindObjectsByType.
    private static void BindBirdThreat(
        Bird[] pack, BirdFlockDrive flock, Transform threat, float threatRange)
    {
        if (threat == null)
            return;
        if (flock != null)
        {
            flock.threat = threat;
            flock.threatRange = threatRange;
        }

        if (pack == null)
            return;
        for (int i = 0; i < pack.Length; i++)
        {
            if (pack[i] == null)
                continue;
            BirdDrive drive = pack[i].GetComponent<BirdDrive>();
            if (drive == null)
                continue;
            drive.threat = threat;
            drive.threatRange = threatRange;
        }
    }

    // Live quarry for DogDrive.ResolvePrey: inside noticeRange, on dirt,
    // facing the dog so BirdDrive.SeesAhead can Flee. Brains stay silent
    // on the stand unless allowHeadless is set here.
    private float DogHuntBirdOriginX()
    {
        float dogX = dog != null ? dog.transform.position.x : 0f;
        float notice = 8f;
        if (dog != null)
        {
            DogDrive drive = dog.GetComponent<DogDrive>();
            if (drive != null)
                notice = Mathf.Max(0.05f, drive.noticeRange);
        }

        float x = dogBirdX;
        if (Mathf.Abs(x) < 0.4f)
            x = 2f;
        float maxOff = notice * 0.75f;
        if (Mathf.Abs(x) > maxOff)
            x = Mathf.Sign(x) * maxOff;
        return dogX + x;
    }

    private void WireDogBirdHunt()
    {
        if (dog == null || bird == null)
            return;

        DogDrive dogDrive = dog.GetComponent<DogDrive>();
        if (dogDrive != null)
            dogDrive.allowHeadless = true;

        BirdDrive[] drives = FindObjectsByType<BirdDrive>(FindObjectsSortMode.None);
        if (drives == null || drives.Length == 0)
        {
            BirdDrive drive = bird.gameObject.AddComponent<BirdDrive>();
            drive.allowHeadless = true;
            drive.kind = BirdDrive.Kind.Wander;
            drive.wanderSeed = birdDriveSeed;
            drives = new[] { drive };
            if (bird.controller != null)
            {
                bird.controller.takeoffDelay = 0f;
                bird.controller.turnAfter = 0f;
                bird.controller.mode = BirdMode.Sit;
            }
        }

        for (int i = 0; i < drives.Length; i++)
        {
            if (drives[i] == null)
                continue;
            drives[i].allowHeadless = true;
            drives[i].threat = dog.transform;
        }

        BirdFlockDrive flockDrive = FindFirstObjectByType<BirdFlockDrive>();
        if (flockDrive != null)
            flockDrive.threat = dog.transform;

        if (!HasArg("-birdFacing") && bird.controller != null)
        {
            float dx = dog.transform.position.x - bird.transform.position.x;
            bird.controller.SetFacing(dx >= 0f ? 1f : -1f);
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
        // Injected perch load: bend and return are measured with THIS, so a
        // broken bird layer can never make the tree look like it passes.
        plantTree.perchLoadKg = GetFloatArg("-treePerchLoad", 0f);
        plantTree.perchLoadSlot = Mathf.RoundToInt(GetFloatArg("-treePerchSlot", 0f));
        plantTree.perchLoadUntilSeconds = GetFloatArg("-treePerchLoadUntil", 0f);
        plantTree.BuildTree();
    }

    private void BuildAppleScene()
    {
        GameObject appleObject = new GameObject("TrialAppleTree");
        appleObject.transform.position = new Vector2(0f, PlantBase.RootY(-2f));
        appleTree = appleObject.AddComponent<AppleTree>();
        appleTree.seed = Mathf.RoundToInt(GetFloatArg("-appleSeed", 1f));
        float forcedHeight = GetFloatArg("-appleHeight", 0f);
        if (forcedHeight > 0f)
            appleTree.appleHeightM = forcedHeight;
        appleTree.windScale = GetFloatArg("-appleWind", appleTree.windScale);
        appleTree.windSpeed = GetFloatArg("-appleWindSpeed", appleTree.windSpeed);
        appleTree.Build();
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
        int lastShot = -1;
        while (simulated < duration)
        {
            yield return FixedStep;
            simulated += Time.fixedDeltaTime;
            plantRecorder.Sample(simulated);

            // Same capture as the human, bird and dog trials. Without it every
            // tree acceptance was a number only and no one ever looked at a
            // tree. A plant change is accepted by a picture.
            if (visual)
            {
                int shotIdx = (int)(simulated / 0.2f);
                if (shotIdx != lastShot)
                {
                    lastShot = shotIdx;
                    string shotPath = Path.Combine(outputFolder, "shot_" + label + "_" + shotIdx.ToString("00") + ".png");
                    yield return CaptureVisualFrame(shotPath);
                }
            }

            if (Time.realtimeSinceStartup > realDeadline)
            {
                Debug.LogWarning("HeadlessTrial: real-time limit exceeded, run aborted.");
                break;
            }
        }

        Finish(simulated, Time.realtimeSinceStartup - trialWallStart);
    }

    private IEnumerator RunAppleTrial()
    {
        appleRecorder = new AppleTrialRecorder(appleTree, runInfo);
        float trialWallStart = Time.realtimeSinceStartup;
        float simulated = 0f;
        float realDeadline = Time.realtimeSinceStartup + REAL_TIME_LIMIT_SECONDS;
        int lastShot = -1;
        while (simulated < duration)
        {
            yield return FixedStep;
            simulated += Time.fixedDeltaTime;
            appleRecorder.Sample(simulated);
            if (visual)
            {
                int shotIdx = (int)(simulated / 0.2f);
                if (shotIdx != lastShot)
                {
                    lastShot = shotIdx;
                    string shotPath = Path.Combine(outputFolder, "shot_" + label + "_" + shotIdx.ToString("00") + ".png");
                    yield return CaptureVisualFrame(shotPath);
                }
            }
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
        target.toeMuscleTorque *= muscle;
        target.neckMuscleTorque *= muscle;
        target.lumbarMuscleTorque *= muscle;

        target.hipFriction *= friction;
        target.kneeFriction *= friction;
        target.ankleFriction *= friction;
        target.shoulderFriction *= friction;
        target.elbowFriction *= friction;
        target.wristFriction *= friction;
        target.toeFriction *= friction;
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
        target.toeFriction = GetFloatArg("-toeFriction", target.toeFriction);
    }

    // Neck and head muscle torque: absolute value, after the -muscle multiplier.
    // One field for both joints, same as in Human. It needs its own flag because
    // at 0.0015 kg·m² inertia the old 15 N·m gave 9900 rad/s², and PD
    // reversed the joint every physics step.
    private static void ApplyMuscleOverrides(Human target)
    {
        if (target == null) return;
        target.neckMuscleTorque = GetFloatArg("-neckMuscle", target.neckMuscleTorque);
        target.toeMuscleTorque = GetFloatArg("-toeTorque", target.toeMuscleTorque);
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
        // Quiet-stance anti-split mix. 0 = former pelvis-only hips.
        // Walk / one-leg already skip the mix in ControlHipsToWorldUpright.
        balance.hipHoldBand = GetFloatArg("-hipHoldBand", balance.hipHoldBand);
        balance.hipHoldAngle = GetFloatArg("-hipHoldAngle", balance.hipHoldAngle);
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
        balance.marchHipFlex = GetFloatArg("-marchHip", balance.marchHipFlex);
        balance.marchKneeFlex = GetFloatArg("-marchKnee", balance.marchKneeFlex);
        balance.marchPelvisHold = GetFloatArg("-marchPelvisHold", balance.marchPelvisHold);
        balance.marchComPelvisGain = GetFloatArg("-marchPelvisCom", balance.marchComPelvisGain);
        balance.marchPelvisComClamp = GetFloatArg("-marchPelvisClamp", balance.marchPelvisComClamp);
        balance.marchAnkleRefScale = GetFloatArg("-marchAnkleRef", balance.marchAnkleRefScale);
        balance.marchStancePelvisP = GetFloatArg("-marchStanceP", balance.marchStancePelvisP);
        balance.marchAnkleCap = GetFloatArg("-marchAnkleCap", balance.marchAnkleCap);
        balance.marchAnkleToeGate = GetFloatArg("-marchAnkleToe", balance.marchAnkleToeGate);
        balance.marchAnkleHeelGate = GetFloatArg("-marchAnkleHeel", balance.marchAnkleHeelGate);
        balance.marchComDelay = GetFloatArg("-marchComDelay", balance.marchComDelay);
        balance.marchPoseHip = GetFloatArg("-marchPoseHip", balance.marchPoseHip);
        balance.marchPoseKnee = GetFloatArg("-marchPoseKnee", balance.marchPoseKnee);
        balance.marchPrepareTilt = GetFloatArg("-marchPrepare", balance.marchPrepareTilt);
        balance.marchLiftTilt = GetFloatArg("-marchLift", balance.marchLiftTilt);
        balance.marchTiltRate = GetFloatArg("-marchTiltRate", balance.marchTiltRate);
        balance.swingHipUnloadBias = GetFloatArg("-swingHipUnload", balance.swingHipUnloadBias);
        balance.swingKneeGroundedFraction = GetFloatArg("-swingKneeGround", balance.swingKneeGroundedFraction);
        balance.forwardSwingHipUnloadScale = GetFloatArg("-forwardUnloadScale", balance.forwardSwingHipUnloadScale);
        balance.forwardSwingKneeGroundScale = GetFloatArg("-forwardKneeScale", balance.forwardSwingKneeGroundScale);
        balance.backSwingHipUnloadScale = GetFloatArg("-backUnloadScale", balance.backSwingHipUnloadScale);
        balance.backSwingKneeGroundScale = GetFloatArg("-backKneeScale", balance.backSwingKneeGroundScale);
        balance.backPelvisTiltFraction = GetFloatArg("-backPelvisTilt", balance.backPelvisTiltFraction);
        balance.swingAnkleGroundedToeOff = GetFloatArg("-swingAnkleToeOff", balance.swingAnkleGroundedToeOff);
        balance.standLegPelvisTilt = GetFloatArg("-standPelvisTilt", balance.standLegPelvisTilt);
        balance.oneLegPelvisPBoost = GetFloatArg("-oneLegPelvisP", balance.oneLegPelvisPBoost);
        balance.oneLegPelvisDBoost = GetFloatArg("-oneLegPelvisD", balance.oneLegPelvisDBoost);
        // Same tilt on right support during fall-catch walk. 0 = former
        // left-support-only tilt, bit for bit.
        balance.walkStandLegPelvisTiltMirror = GetFloatArg(
            "-walkStandTiltMirror", balance.walkStandLegPelvisTiltMirror);
        balance.dualSupportSwingScale = GetFloatArg("-dualSupportSwing", balance.dualSupportSwingScale);
        balance.dualSupportStandCap = GetFloatArg("-dualSupportCap", balance.dualSupportStandCap);
        balance.walkAirReleaseAuthority = GetFloatArg("-walkAirRelease", balance.walkAirReleaseAuthority);
        // Same air release on the pelvis-tilt softener. 0 = the former frozen
        // dualSupportSwingScale floor on the tilt, bit for bit.
        balance.walkStandTiltAirRelease = GetFloatArg(
            "-walkStandTiltAir", balance.walkStandTiltAirRelease);
        // Measured double-support weight share on the same ramp. 0 = the
        // former clearance-only release, bit for bit.
        balance.walkDualLoadRelease = GetFloatArg(
            "-walkDualLoadRelease", balance.walkDualLoadRelease);
        // Measured leading/trailing share on stanceLoad / Plant rear hip
        // and the Plant trailing-ankle A2 burst. -walkStanceLoadShare 0
        // restores the former 0.5 dual-support floor and the unscaled A2.
        balance.walkStanceLoadShare = GetFloatArg(
            "-walkStanceLoadShare", balance.walkStanceLoadShare);
        balance.walkReachAirSwingSeconds = GetFloatArg("-walkReachAirSwing", balance.walkReachAirSwingSeconds);
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
        balance.walkStanceHipExtend = GetFloatArg("-walkStanceHipExtend", balance.walkStanceHipExtend);
        balance.walkStanceHipExtendMax = GetFloatArg("-walkStanceHipExtendMax", balance.walkStanceHipExtendMax);
        // Stance push-off once CoM has passed the stance foot (fall-catch
        // and WALK-1 Stance after geo-lift). -walkStancePushOnLead 0
        // restores the former airborne-only gate.
        balance.walkStancePushOnLead = GetFloatArg(
            "-walkStancePushOnLead", balance.walkStancePushOnLead);
        balance.walkPlantPushScale = GetFloatArg("-walkPlantPush", balance.walkPlantPushScale);
        balance.walkPlantPushLeadWindow = GetFloatArg("-walkPlantPushLead", balance.walkPlantPushLeadWindow);
        // Plant roll-over setpoint. -walkPlantRock 0 restores the former
        // hold-the-CoM-on-the-foot Plant (release on plantLeadWait alone).
        balance.walkPlantRockLead = GetFloatArg("-walkPlantRock", balance.walkPlantRockLead);
        // Trunk half of that roll-over. -walkPlantLean 0 restores the former
        // upright / braked Plant, bit for bit.
        balance.walkPlantLeanRoll = GetFloatArg("-walkPlantLean", balance.walkPlantLeanRoll);
        // Same lean on cycle 0. -walkPlantLeanFirst 0 restores the former
        // later-cycle-only gate (feet-together first step had rock lead only).
        balance.walkPlantLeanFirst = GetFloatArg(
            "-walkPlantLeanFirst", balance.walkPlantLeanFirst);
        // Hip half of the Plant push-off, as a fraction of walkStanceHipExtend.
        // -walkPlantHipExtend 0 restores the former idle Plant hip, bit for bit.
        balance.walkPlantHipExtend = GetFloatArg(
            "-walkPlantHipExtend", balance.walkPlantHipExtend);
        // Support identity through the walk-release window.
        // -walkEndHoldSupport 0 restores the former instant drop, bit for bit.
        balance.walkEndHoldSupport = GetFloatArg(
            "-walkEndHoldSupport", balance.walkEndHoldSupport);
        // Plant trailing-knee lift once the CoM has rolled off that foot.
        // -walkPlantSwingKnee 0 restores the former Plant with no swing fold.
        balance.walkPlantSwingKnee = GetFloatArg(
            "-walkPlantSwingKnee", balance.walkPlantSwingKnee);
        // Remap that fold across the remaining half-span. 0 = 22:00
        // swingKneeFlex × clock (27.5° step at mid-span, full thighHold,
        // DrivePelvis drop at reachKnee > 0.5°).
        balance.walkPlantKneeRamp = GetFloatArg(
            "-walkPlantKneeRamp", balance.walkPlantKneeRamp);
        // Same Plant swing-knee fold on cycle 0. -walkPlantSwingKneeFirst 0
        // restores the former later-cycle-only gate (feet-together first
        // step had lean / rock lead and a straight trailing shin).
        balance.walkPlantSwingKneeFirst = GetFloatArg(
            "-walkPlantSwingKneeFirst", balance.walkPlantSwingKneeFirst);
        // Same Plant stance-hip drive on cycle 0. -walkPlantStanceDriveFirst 0
        // restores the former wait-for-lead (pushAfterAir × pushWindow).
        balance.walkPlantStanceDriveFirst = GetFloatArg(
            "-walkPlantStanceDriveFirst", balance.walkPlantStanceDriveFirst);
        // Same Plant swing-leg viscosity fade on cycle 0. -walkPlantSwingFrictionFirst 0
        // restores the former DualLoad-only weld (the 03:45 fold command
        // fought stopTorque 15 N·m until a millimetre of air).
        balance.walkPlantSwingFrictionFirst = GetFloatArg(
            "-walkPlantSwingFrictionFirst", balance.walkPlantSwingFrictionFirst);
        // Same Plant stance-knee extra-bend fade on cycle 0.
        // -walkPlantStanceStraightFirst 0 restores the former DualLoad-only
        // 5° shuffle flex (liftGateBlend stays 0 while the feet sit under
        // the hip, so LiftFade never starts).
        balance.walkPlantStanceStraightFirst = GetFloatArg(
            "-walkPlantStanceStraightFirst", balance.walkPlantStanceStraightFirst);
        // Same Plant swing-hip flexor unload on cycle 0.
        // -walkPlantSwingUnloadFirst 0 restores the former DualLoad-only
        // 0.041 bias (liftGateBlend stays 0 while the feet sit under the
        // hip, so unloadSoft × standLegLevel never leaves the 0.32 × 0.40
        // floor). DrivePelvis stays; this is not ThighHold.
        balance.walkPlantSwingUnloadFirst = GetFloatArg(
            "-walkPlantSwingUnloadFirst", balance.walkPlantSwingUnloadFirst);
        // Same Plant pelvis-tilt unload on cycle 0. -walkPlantTiltFirst 0
        // restores the former DualLoad-only 1.28° (liftGateBlend stays 0
        // while the feet sit under the hip, so tiltSoft × standLegLevel
        // never leaves the 0.32 × 0.40 floor). DrivePelvis stays; this is
        // that loop's target, not ThighHold.
        balance.walkPlantTiltFirst = GetFloatArg(
            "-walkPlantTiltFirst", balance.walkPlantTiltFirst);
        // Same Plant swing-hip ThighHold on cycle 0. -walkPlantHipReleaseFirst 0
        // restores the former DrivePelvis lock (plantHipRelease stays 0 while
        // the feet sit under the hip, so reachKneeFolding never opens and
        // hipLift / thighHold are stored but never applied). This is the
        // later-cycle plantFold hand-off, not −swingHipFlex.
        balance.walkPlantHipReleaseFirst = GetFloatArg(
            "-walkPlantHipReleaseFirst", balance.walkPlantHipReleaseFirst);
        // Same Plant swing-ankle CoM release on cycle 0. -walkPlantSwingAnkleReleaseFirst 0
        // restores the former dual-support CoM hold on both soles (liftBlend
        // stays 0 on fall-catch, DualLoadMinSpan never opens while the feet
        // sit under the hip, so the assigned swing ankle cancels rock lead /
        // stance-hip / lean). Not trailing A2 and not an ankle-angle PD.
        balance.walkPlantSwingAnkleReleaseFirst = GetFloatArg(
            "-walkPlantSwingAnkleReleaseFirst", balance.walkPlantSwingAnkleReleaseFirst);
        // Same Plant released swing-ankle through the ankleLift overwrite.
        // -walkPlantSwingAnkleKeepFirst 0 restores the former CoM overwrite
        // once plantFold writes ankleLift (the swing sole's own loop puts
        // plantarflex back on the stacked foot and cancels rock lead /
        // stance-hip / lean). Dorsiflex unweight stays; this is not trailing
        // A2 and not an ankle-angle PD.
        balance.walkPlantSwingAnkleKeepFirst = GetFloatArg(
            "-walkPlantSwingAnkleKeepFirst", balance.walkPlantSwingAnkleKeepFirst);
        // Same Plant released swing-ankle through Plant→Fall / grounded Reach.
        // -walkPlantFallAnkleKeepFirst 0 restores the former CoM overwrite
        // plus propulsive-half clamp on the first Fall frame (stacked soles
        // have no trailing arm, so that clamp deletes dorsiflex and ankleLift
        // mix from zero stays zero). Dorsiflex unweight stays; this is not
        // trailing A2 and not an ankle-angle PD. Catch / later cycles keep
        // the overwrite.
        balance.walkPlantFallAnkleKeepFirst = GetFloatArg(
            "-walkPlantFallAnkleKeepFirst", balance.walkPlantFallAnkleKeepFirst);
        // Same Fall keep, but hold the Plant release endpoint (zero) instead
        // of dual-support CoM restored by the PhaseAge reset. -walkPlantFallAnkleReleaseHoldFirst 0
        // restores the former keep of leftAnkleBalance (midpoint loop on
        // stacked soles, which cancels rock lead / stance-hip / lean).
        balance.walkPlantFallAnkleReleaseHoldFirst = GetFloatArg(
            "-walkPlantFallAnkleReleaseHoldFirst", balance.walkPlantFallAnkleReleaseHoldFirst);
        // Same Fall keep of the Plant swing-leg viscosity fade. EnterFall
        // resets PhaseAge, DualLoadMinSpan never opens, airRelease is 0 on
        // dirt, so loadFade is 0 and ApplySwingLegFriction(0) restores the
        // weld on the shin walkPlantFallFoldHold is holding at 55°.
        // -walkPlantFallFrictionHoldFirst 0 restores that weld on the first
        // Fall/Reach frame. Catch / later cycles keep DualLoad / airRelease.
        balance.walkPlantFallFrictionHoldFirst = GetFloatArg(
            "-walkPlantFallFrictionHoldFirst", balance.walkPlantFallFrictionHoldFirst);
        // Same Fall keep of the Plant stance-hip drive. EnterFall resets
        // PhaseAge, DualLoadMinSpan never opens, the swing has never left
        // so pushAfterAir is 0, CoM sits on the stacked feet so pushWindow
        // is 0, and the later-cycle extend is 0: the stance hip is
        // world-upright again on the phase whose job is the lift.
        // -walkPlantFallStanceDriveHoldFirst 0 restores that world-upright
        // stance hip on the first Fall/Reach frame. Catch / later cycles
        // keep pushAfterAir / pushWindow. Not trailing A2.
        balance.walkPlantFallStanceDriveHoldFirst = GetFloatArg(
            "-walkPlantFallStanceDriveHoldFirst", balance.walkPlantFallStanceDriveHoldFirst);
        // Same Fall keep of the Plant trunk lean. EnterFall resets
        // PhaseAge, DualLoadMinSpan never opens, CoM stays on the
        // stacked feet so leanRoom stays ~1, and Fall assigns
        // fallLean * fallLiftClock with PhaseAge 0: the 9° Plant lean
        // is a step to 0 on the first Fall frame — the third leg of
        // the initiation triangle. -walkPlantFallLeanHoldFirst 0
        // restores that 9°→0 drop. Catch / later cycles keep Fall/
        // Reach envelopes. Happy-path leanRoom→0 at the gate is a
        // no-op.
        balance.walkPlantFallLeanHoldFirst = GetFloatArg(
            "-walkPlantFallLeanHoldFirst", balance.walkPlantFallLeanHoldFirst);
        // Same Fall keep of the Plant pelvis-tilt unload. EnterFall
        // resets PhaseAge, DualLoadMinSpan never opens, liftGateBlend
        // stays 0, so tiltSoft × standLegLevel is 1.28° of the 10° that
        // raised the swing hip: DrivePelvis yanks toward vertical and
        // drops that hip (and fights the held 9° lean).
        // -walkPlantFallTiltHoldFirst 0 restores that 10°→1.28° drop
        // on the first Fall/Reach frame. Catch / later cycles keep
        // DualLoad tiltSoft. DrivePelvis stays; this is that loop's
        // target, not ThighHold.
        balance.walkPlantFallTiltHoldFirst = GetFloatArg(
            "-walkPlantFallTiltHoldFirst", balance.walkPlantFallTiltHoldFirst);
        // Same Fall keep of the Plant stance-knee straighten. EnterFall
        // resets PhaseAge, DualLoadMinSpan never opens, liftGateBlend
        // stays 0, so the fade schedule returns to its 0.5 end: extra 5°
        // of stance flex on the only strut. Fall vaultClock is 0 on
        // that same frame, so stanceVault does not skip the extra.
        // Those 5° drop the stance hip ~4 mm (2·L·cos(knee/2)), which
        // is swing-foot clearance. Hip-unload hold would be a no-op
        // (ThighHold already owns the swing hip on Fall via fold hold).
        // -walkPlantFallStanceStraightFirst 0 restores that extra 5° on
        // the first Fall/Reach frame. Catch / later cycles keep DualLoad
        // / vault. Not trailing A2 and not a Fall vault to 0°.
        balance.walkPlantFallStanceStraightFirst = GetFloatArg(
            "-walkPlantFallStanceStraightFirst", balance.walkPlantFallStanceStraightFirst);
        // Same Fall keep of the Plant rock-lead CoP. EnterFall replaces
        // comLead with fallComLead × blend (0.025 m) and the sole roll
        // starts at PhaseAge 0, so the first Fall frames pin CoM on a
        // stationary setpoint. Grounded Reach writes comLead 0: the
        // WALK-2 root (stance ankle holds CoM over the stacked feet)
        // while Fall→Reach still waits on air. Lean hold is the trunk
        // half of that roll-over; this is the ankle half. Hip-unload
        // hold would still be a no-op (ThighHold already owns the
        // swing hip on Fall via fold hold).
        // -walkPlantFallRockHoldFirst 0 restores 0.03→0.025 (Fall) /
        // 0.03→0 (Reach) on the first Fall/Reach frame. Catch / later
        // cycles keep fallComLead / RecoverBrake. Not trailing A2 and
        // not an ankle-angle PD.
        balance.walkPlantFallRockHoldFirst = GetFloatArg(
            "-walkPlantFallRockHoldFirst", balance.walkPlantFallRockHoldFirst);
        // Same Fall keep of Plant/Fall ankleScale 1. Fall already writes
        // 1, then after fallKneeRampSeconds the driver enters Reach with
        // the swing still on dirt. Reach Lerp(1, 0.25, calm) on a stacked
        // trunk is 0.25: the rock-lead hold's 0.03 m setpoint then makes
        // 25% of its torque, so CoM cannot finish the roll-over that
        // Fall→Reach is waiting on. Hold 1 while the swing sole is down
        // and comLead is live (scale 1 with comLead 0 is the WALK-2
        // root at full strength). Air Reach keeps 0.25. Hip-unload hold
        // would still be a no-op (ThighHold already owns the swing hip
        // on Fall via fold hold).
        // -walkPlantFallAnkleScaleHoldFirst 0 restores Reach 0.25 on
        // the first grounded Fall→Reach frame. Catch / later cycles
        // keep the Reach envelope. Not trailing A2 and not an
        // ankle-angle PD.
        balance.walkPlantFallAnkleScaleHoldFirst = GetFloatArg(
            "-walkPlantFallAnkleScaleHoldFirst", balance.walkPlantFallAnkleScaleHoldFirst);
        // Same Fall cap of the Plant swing fold. walkPlantFallFoldHold
        // Maxes Plant 55° across PhaseAge 0, then Fall's 2× clock
        // overtakes to reachKneeFlex 140° in fallKneeRampSeconds while
        // DualLoadMinSpan never opens. 140° on a still-shared plant is
        // the squat that drops the pelvis onto both soles (hip-to-ankle
        // 2·L·cos(knee/2): 55° ~0.78 L, 140° ~0.34 L). Cap at the Plant
        // pose while the swing sole is down. Air Fall/Reach keeps 140°.
        // Hip-unload hold would still be a no-op (ThighHold already
        // owns the swing hip on Fall via fold hold).
        // -walkPlantFallFoldCapFirst 0 restores the grounded ramp to
        // 140° on the first Fall/Reach. Catch / later cycles keep the
        // envelope. Not trailing A2 and not −swingHipFlex.
        balance.walkPlantFallFoldCapFirst = GetFloatArg(
            "-walkPlantFallFoldCapFirst", balance.walkPlantFallFoldCapFirst);
        // Same Fall hold of the Plant stance-hip loop scale. Plant writes
        // hipBalanceScale 1, then a calm stacked Fall (DualLoadMinSpan
        // never opens) Lerp(1, 0.15, calm) with calm = 1, Reach 0.25.
        // The 0.15 is a committed-topple cut; on dirt it sags the pelvis
        // into the 55° fold the cap just saved. Hold 1 while the swing
        // sole is down. Air Fall/Reach keeps 0.15 / 0.25. Hip-unload
        // hold would still be a no-op (ThighHold already owns the swing
        // hip on Fall via fold hold).
        // -walkPlantFallHipBalanceHoldFirst 0 restores Fall 0.15 /
        // Reach 0.25 on the first grounded Fall/Reach. Catch / later
        // cycles keep the envelope. Not trailing A2 and not a PD retune.
        balance.walkPlantFallHipBalanceHoldFirst = GetFloatArg(
            "-walkPlantFallHipBalanceHoldFirst", balance.walkPlantFallHipBalanceHoldFirst);
        // Same Fall keep of Plant suppressWalkLean. Plant zeros
        // walkAutoLean / walkBasePelvisLean (and the torso pair);
        // Fall/Reach never set the flag. Cycle 0 is a stacked trunk at
        // rest (DualLoadMinSpan never opens), so walkSpeedError = 1 and
        // the first grounded Fall frame adds 8° pelvis / 11.2° chest on
        // top of the held 9° fc lean. Lumbar ±20° takes that as a stop
        // slam; the third-law reaction cancels the roll-over Fall→Reach
        // is waiting on. Hold Plant's suppress while the swing sole is
        // down. Air Fall/Reach keeps speed/base lean. Hip-unload hold
        // would still be a no-op (ThighHold already owns the swing hip
        // on Fall via fold hold).
        // -walkPlantFallSuppressWalkLeanFirst 0 restores the 8°/11°
        // speed+base lean step on the first grounded Fall/Reach. Catch
        // / later cycles keep RecoverBrake / Plant suppress. Not
        // trailing A2, not a PD retune, and not a walkTargetSpeed
        // change.
        balance.walkPlantFallSuppressWalkLeanFirst = GetFloatArg(
            "-walkPlantFallSuppressWalkLeanFirst", balance.walkPlantFallSuppressWalkLeanFirst);
        // Same Fall raise of the grounded fold ceiling. Fold cap pinned
        // the shin at Plant 55° so 140° would not squat a sagging pelvis.
        // Hip-balance hold + suppressWalkLean now keep that strut (loop 1,
        // no 8°/11° lumbar slam), so 55° is the Plant floor. Quarter of
        // the remaining Fall fold (Lerp 55→140 at 0.25 = 76°) is still
        // ~0.79 L, not the 0.34 L squat. Air Fall/Reach keeps 140°.
        // Hip-unload hold would still be a no-op (ThighHold already owns
        // the swing hip on Fall via fold hold).
        // -walkPlantFallFoldGrowFirst 0 restores the former 55° pin on
        // the first grounded Fall/Reach. Catch / later cycles keep the
        // envelope. Not trailing A2, not −swingHipFlex, and not a PD
        // retune.
        balance.walkPlantFallFoldGrowFirst = GetFloatArg(
            "-walkPlantFallFoldGrowFirst", balance.walkPlantFallFoldGrowFirst);
        // Same Fall seed of the grow cap. Grow raised Min to 76°; fold
        // hold still Maxes Plant 55° at PhaseAge 0, and Fall's 2× clock
        // starts at 0, so the first ~8 Fall frames stay at the 55° that
        // already failed to clear. DualLoadMinSpan never opens: that is
        // the lift phase. Max to the grow cap on dirt so the extra
        // shortening (2·L·cos(38°) vs 2·L·cos(27.5°)) is commanded on
        // the first grounded Fall frame. Clock still Maxes if it would
        // exceed 76; Min still caps. Air Fall/Reach keeps 140°.
        // Hip-unload hold would still be a no-op (ThighHold already owns
        // the swing hip on Fall via fold hold).
        // -walkPlantFallFoldSeedFirst 0 restores the climb from Plant
        // 55° on the first grounded Fall/Reach. Catch / later cycles
        // keep the envelope. Not trailing A2, not −swingHipFlex, and
        // not a PD retune.
        balance.walkPlantFallFoldSeedFirst = GetFloatArg(
            "-walkPlantFallFoldSeedFirst", balance.walkPlantFallFoldSeedFirst);
        // Same Fall seed of the lift pair under that 76° fold. Fold
        // seed Maxes reachKnee immediately; hipLift/ankleLift stay at
        // the Plant leftover sized for 55° (0.5 cap × plantFold, fold
        // hold Maxes it, 2× lift clock starts at 0). Extra knee flex
        // without that flexor/unweight pair slides the foot back on
        // µ=0.4 — the backward start WALK-2 forbids. DualLoadMinSpan
        // never opens: that is the lift phase. Same quarter as fold
        // grow (Lerp Plant leftover → 1 at 0.25 = 0.625). Still a
        // Clamp01 bias, still fades at the 16–32° cap, not −swingHipFlex
        // and not a 1.0 dorsiflexor. Air Fall/Reach keeps Plant/Fall 0.5.
        // Hip-unload hold would still be a no-op (ThighHold already owns
        // the swing hip on Fall via fold hold).
        // -walkPlantFallLiftSeedFirst 0 restores the Plant 0.5
        // hipLift/ankleLift under the seeded 76° fold. Catch / later
        // cycles keep the envelope. Not trailing A2, not −swingHipFlex,
        // and not a PD retune.
        balance.walkPlantFallLiftSeedFirst = GetFloatArg(
            "-walkPlantFallLiftSeedFirst", balance.walkPlantFallLiftSeedFirst);
        // Same Fall seed of the cap floor under that 0.625 hipLift.
        // Lift seed raised the bias; ApplyFallCatchSwingThighHold still
        // floors the fade at 0.5, so at the 32° leftover (CapCeil) the
        // extra flexor is halved (liftSignal −0.3125 vs −0.391). DualLoadMinSpan
        // never opens: that is the lift phase. Same quarter as fold
        // grow / lift seed (Lerp 0.5 → 1 at 0.25 = 0.625). Still a
        // Clamp01 hold at the cap, not −swingHipFlex and not a 1.0
        // floor into 40°. Air Fall/Reach keeps the 0.5 floor.
        // Hip-unload hold would still be a no-op (ThighHold already owns
        // the swing hip on Fall via fold hold).
        // -walkPlantFallLiftFloorSeedFirst 0 restores the former 0.5
        // floor under the seeded 0.625 hipLift. Catch / later cycles
        // keep the envelope. Not trailing A2, not −swingHipFlex, and
        // not a PD retune.
        balance.walkPlantFallLiftFloorSeedFirst = GetFloatArg(
            "-walkPlantFallLiftFloorSeedFirst", balance.walkPlantFallLiftFloorSeedFirst);
        // Same Fall grow of the matched 0.625 trio. Floor seed matched
        // hipLift so leftover 32° (CapCeil) holds −0.391; remaining gap
        // to 1.0 is 0.375 of bias. DualLoadMinSpan never opens: that is
        // the lift phase. Same quarter as fold grow / lift seed / floor
        // seed (Lerp 0.625 → 1 at 0.25 = 0.719 on hipLift, ankleLift and
        // the floor together). liftSignal −0.517. Still a Clamp01 hold
        // at the cap, not −swingHipFlex and not a 1.0 floor into 40°.
        // Air Fall/Reach keeps 0.625. Hip-unload hold would still be a
        // no-op (ThighHold already owns the swing hip on Fall via fold
        // hold).
        // -walkPlantFallLiftGrowFirst 0 restores the former matched
        // 0.625 trio under the seeded 76° fold. Catch / later cycles
        // keep the envelope. Not trailing A2, not −swingHipFlex, and
        // not a PD retune.
        balance.walkPlantFallLiftGrowFirst = GetFloatArg(
            "-walkPlantFallLiftGrowFirst", balance.walkPlantFallLiftGrowFirst);
        // Same Fall second grow of the matched 0.719 trio. GrowFirst
        // matched leftover 32° (CapCeil) to −0.517; remaining gap to
        // 1.0 is 0.281 of bias. DualLoadMinSpan never opens: that is
        // the lift phase. Same quarter as fold grow / lift seed /
        // floor seed / GrowFirst (Lerp 0.719 → 1 at 0.25 = 0.789 on
        // hipLift, ankleLift and the floor together). liftSignal
        // −0.623. Still a Clamp01 hold at the cap, not −swingHipFlex
        // and not a 1.0 floor into 40°. Air Fall/Reach keeps 0.719.
        // Hip-unload hold would still be a no-op (ThighHold already
        // owns the swing hip on Fall via fold hold).
        // -walkPlantFallLiftGrowMoreFirst 0 restores the former matched
        // 0.719 trio under the seeded 76° fold. Catch / later cycles
        // keep the envelope. Not trailing A2, not −swingHipFlex, and
        // not a PD retune.
        balance.walkPlantFallLiftGrowMoreFirst = GetFloatArg(
            "-walkPlantFallLiftGrowMoreFirst", balance.walkPlantFallLiftGrowMoreFirst);
        // Same Fall third grow of the matched 0.789 trio. GrowMoreFirst
        // matched leftover 32° (CapCeil) to −0.623; remaining gap to
        // 1.0 is 0.211 of bias. DualLoadMinSpan never opens: that is
        // the lift phase. Same quarter as fold grow / lift seed /
        // floor seed / GrowFirst / GrowMore (Lerp 0.789 → 1 at 0.25
        // = 0.842 on hipLift, ankleLift and the floor together).
        // liftSignal −0.709. Still a Clamp01 hold at the cap, not
        // −swingHipFlex and not a 1.0 floor into 40°. Air Fall/Reach
        // keeps 0.789. Hip-unload hold would still be a no-op
        // (ThighHold already owns the swing hip on Fall via fold
        // hold).
        // -walkPlantFallLiftGrowYetFirst 0 restores the former matched
        // 0.789 trio under the seeded 76° fold. Catch / later cycles
        // keep the envelope. Not trailing A2, not −swingHipFlex, and
        // not a PD retune.
        balance.walkPlantFallLiftGrowYetFirst = GetFloatArg(
            "-walkPlantFallLiftGrowYetFirst", balance.walkPlantFallLiftGrowYetFirst);
        // Same Fall fourth grow of the matched 0.842 trio. GrowYetFirst
        // matched leftover 32° (CapCeil) to −0.709; remaining gap to
        // 1.0 is 0.158 of bias. DualLoadMinSpan never opens: that is
        // the lift phase. Same quarter as fold grow / lift seed /
        // floor seed / GrowFirst / GrowMore / GrowYet (Lerp 0.842
        // → 1 at 0.25 = 0.882 on hipLift, ankleLift and the floor
        // together). liftSignal −0.778. Still a Clamp01 hold at the
        // cap, not −swingHipFlex and not a 1.0 floor into 40°. Air
        // Fall/Reach keeps 0.842. Hip-unload hold would still be a
        // no-op (ThighHold already owns the swing hip on Fall via
        // fold hold).
        // -walkPlantFallLiftGrowEvenFirst 0 restores the former matched
        // 0.842 trio under the seeded 76° fold. Catch / later cycles
        // keep the envelope. Not trailing A2, not −swingHipFlex, and
        // not a PD retune.
        balance.walkPlantFallLiftGrowEvenFirst = GetFloatArg(
            "-walkPlantFallLiftGrowEvenFirst", balance.walkPlantFallLiftGrowEvenFirst);
        // Same Fall fifth grow of the matched 0.882 trio. GrowEvenFirst
        // matched leftover 32° (CapCeil) to −0.778; remaining gap to
        // 1.0 is 0.118 of bias. DualLoadMinSpan never opens: that is
        // the lift phase. Same quarter as fold grow / lift seed /
        // floor seed / GrowFirst / GrowMore / GrowYet / GrowEven
        // (Lerp 0.882 → 1 at 0.25 = 0.912 on hipLift, ankleLift and
        // the floor together). liftSignal −0.831. Still a Clamp01
        // hold at the cap, not −swingHipFlex and not a 1.0 floor
        // into 40°. Air Fall/Reach keeps 0.882. Hip-unload hold
        // would still be a no-op (ThighHold already owns the swing
        // hip on Fall via fold hold).
        // -walkPlantFallLiftGrowStillFirst 0 restores the former
        // matched 0.882 trio under the seeded 76° fold. Catch /
        // later cycles keep the envelope. Not trailing A2, not
        // −swingHipFlex, and not a PD retune.
        balance.walkPlantFallLiftGrowStillFirst = GetFloatArg(
            "-walkPlantFallLiftGrowStillFirst", balance.walkPlantFallLiftGrowStillFirst);
        // Same Fall sixth grow of the matched 0.912 trio. GrowStillFirst
        // matched leftover 32° (CapCeil) to −0.831; remaining gap to
        // 1.0 is 0.088 of bias. DualLoadMinSpan never opens: that is
        // the lift phase. Same quarter as fold grow / lift seed /
        // floor seed / GrowFirst / GrowMore / GrowYet / GrowEven /
        // GrowStill (Lerp 0.912 → 1 at 0.25 = 0.934 on hipLift,
        // ankleLift and the floor together). liftSignal −0.872. Still
        // a Clamp01 hold at the cap, not −swingHipFlex and not a 1.0
        // floor into 40°. Air Fall/Reach keeps 0.912. Hip-unload hold
        // would still be a no-op (ThighHold already owns the swing
        // hip on Fall via fold hold).
        // -walkPlantFallLiftGrowAgainFirst 0 restores the former
        // matched 0.912 trio under the seeded 76° fold. Catch /
        // later cycles keep the envelope. Not trailing A2, not
        // −swingHipFlex, and not a PD retune.
        balance.walkPlantFallLiftGrowAgainFirst = GetFloatArg(
            "-walkPlantFallLiftGrowAgainFirst", balance.walkPlantFallLiftGrowAgainFirst);
        // Same Fall seventh grow of the matched 0.934 trio. GrowAgainFirst
        // matched leftover 32° (CapCeil) to −0.872; remaining gap to
        // 1.0 is 0.066 of bias. DualLoadMinSpan never opens: that is
        // the lift phase. Same quarter as fold grow / lift seed /
        // floor seed / GrowFirst / GrowMore / GrowYet / GrowEven /
        // GrowStill / GrowAgain (Lerp 0.934 → 1 at 0.25 = 0.951 on
        // hipLift, ankleLift and the floor together). liftSignal
        // −0.904. Still a Clamp01 hold at the cap, not −swingHipFlex
        // and not a 1.0 floor into 40°. Air Fall/Reach keeps 0.934.
        // Hip-unload hold would still be a no-op (ThighHold already
        // owns the swing hip on Fall via fold hold).
        // -walkPlantFallLiftGrowAlsoFirst 0 restores the former
        // matched 0.934 trio under the seeded 76° fold. Catch /
        // later cycles keep the envelope. Not trailing A2, not
        // −swingHipFlex, and not a PD retune.
        balance.walkPlantFallLiftGrowAlsoFirst = GetFloatArg(
            "-walkPlantFallLiftGrowAlsoFirst", balance.walkPlantFallLiftGrowAlsoFirst);
        // Same Fall eighth grow of the matched 0.951 trio. GrowAlsoFirst
        // matched leftover 32° (CapCeil) to −0.904; remaining gap to
        // 1.0 is 0.049 of bias. DualLoadMinSpan never opens: that is
        // the lift phase. Same quarter as fold grow / lift seed /
        // floor seed / GrowFirst / GrowMore / GrowYet / GrowEven /
        // GrowStill / GrowAgain / GrowAlso (Lerp 0.951 → 1 at 0.25
        // = 0.963 on hipLift, ankleLift and the floor together).
        // liftSignal −0.927. Still a Clamp01 hold at the cap, not
        // −swingHipFlex and not a 1.0 floor into 40°. Air Fall/Reach
        // keeps 0.951. Hip-unload hold would still be a no-op
        // (ThighHold already owns the swing hip on Fall via fold
        // hold).
        // -walkPlantFallLiftGrowExtraFirst 0 restores the former
        // matched 0.951 trio under the seeded 76° fold. Catch /
        // later cycles keep the envelope. Not trailing A2, not
        // −swingHipFlex, and not a PD retune.
        balance.walkPlantFallLiftGrowExtraFirst = GetFloatArg(
            "-walkPlantFallLiftGrowExtraFirst", balance.walkPlantFallLiftGrowExtraFirst);
        // Same Fall ninth grow of the matched 0.963 trio. GrowExtraFirst
        // matched leftover 32° (CapCeil) to −0.927; remaining gap to
        // 1.0 is 0.037 of bias. DualLoadMinSpan never opens: that is
        // the lift phase. Same quarter as fold grow / lift seed /
        // floor seed / GrowFirst / GrowMore / GrowYet / GrowEven /
        // GrowStill / GrowAgain / GrowAlso / GrowExtra (Lerp 0.963 → 1
        // at 0.25 = 0.972 on hipLift, ankleLift and the floor together).
        // liftSignal −0.945. Still a Clamp01 hold at the cap, not
        // −swingHipFlex and not a 1.0 floor into 40°. Air Fall/Reach
        // keeps 0.963. Hip-unload hold would still be a no-op
        // (ThighHold already owns the swing hip on Fall via fold
        // hold).
        // -walkPlantFallLiftGrowPlusFirst 0 restores the former
        // matched 0.963 trio under the seeded 76° fold. Catch /
        // later cycles keep the envelope. Not trailing A2, not
        // −swingHipFlex, and not a PD retune.
        balance.walkPlantFallLiftGrowPlusFirst = GetFloatArg(
            "-walkPlantFallLiftGrowPlusFirst", balance.walkPlantFallLiftGrowPlusFirst);
        // Same Fall quarter of the 32° CapCeil leftover toward sci_a 40°.
        // PlusFirst matched leftover 32° (room ≤ 0) to −0.945; extra
        // hipLift at room ≤ 0 does not raise past CapCeil, and the
        // couple Min clips a pre-clamp 34° write back to 32°.
        // DualLoadMinSpan never opens: that is the lift phase. Same
        // quarter as fold grow (Lerp 32 → 40 at 0.25 = 34° after the
        // couple clamp). At a 32° thigh, room = 2°. liftSignal stays
        // −0.945 until the thigh walks. Still 6° short of sci_a, not
        // −swingHipFlex. Air Fall/Reach keeps 32°. Hip-unload hold
        // would still be a no-op (ThighHold already owns the swing hip
        // on Fall via fold hold).
        // -walkPlantFallLiftCapGrowFirst 0 restores the former 32°
        // CapCeil leftover under the 0.972 trio. Catch / later cycles
        // keep the envelope. Not trailing A2, not −swingHipFlex, and
        // not a PD retune.
        balance.walkPlantFallLiftCapGrowFirst = GetFloatArg(
            "-walkPlantFallLiftCapGrowFirst", balance.walkPlantFallLiftCapGrowFirst);
        // Same Fall second quarter of the 34° CapGrowFirst leftover
        // toward sci_a 40°. CapGrowFirst wrote leftover 34° (room 2°
        // at a 32° thigh); extra hipLift at room ≤ 0 on 34° does not
        // raise past that cap. DualLoadMinSpan never opens: that is
        // the lift phase. Same quarter as fold grow (Lerp 34 → 40 at
        // 0.25 = 35.5°). At a 34° thigh, room = 1.5°. liftSignal stays
        // −0.945 until the thigh walks. Still 4.5° short of sci_a, not
        // −swingHipFlex. Air Fall/Reach keeps 34°. Hip-unload hold
        // would still be a no-op (ThighHold already owns the swing hip
        // on Fall via fold hold).
        // -walkPlantFallLiftCapGrowMoreFirst 0 restores the former 34°
        // CapGrowFirst leftover under the 0.972 trio. Catch / later
        // cycles keep the envelope. Not trailing A2, not −swingHipFlex,
        // and not a PD retune.
        balance.walkPlantFallLiftCapGrowMoreFirst = GetFloatArg(
            "-walkPlantFallLiftCapGrowMoreFirst", balance.walkPlantFallLiftCapGrowMoreFirst);
        // Same Fall third quarter of the 35.5° CapGrowMoreFirst leftover
        // toward sci_a 40°. CapGrowMoreFirst wrote leftover 35.5° (room
        // 1.5° at a 34° thigh); extra hipLift at room ≤ 0 on 35.5° does
        // not raise past that cap. DualLoadMinSpan never opens: that is
        // the lift phase. Same quarter as fold grow (Lerp 35.5 → 40 at
        // 0.25 = 36.625°). At a 35.5° thigh, room = 1.125°. liftSignal
        // stays −0.945 until the thigh walks. Still 3.375° short of
        // sci_a, not −swingHipFlex. Air Fall/Reach keeps 35.5°.
        // Hip-unload hold would still be a no-op (ThighHold already
        // owns the swing hip on Fall via fold hold).
        // -walkPlantFallLiftCapGrowYetFirst 0 restores the former 35.5°
        // CapGrowMoreFirst leftover under the 0.972 trio. Catch /
        // later cycles keep the envelope. Not trailing A2, not
        // −swingHipFlex, and not a PD retune.
        balance.walkPlantFallLiftCapGrowYetFirst = GetFloatArg(
            "-walkPlantFallLiftCapGrowYetFirst", balance.walkPlantFallLiftCapGrowYetFirst);
        // Same Fall fourth quarter of the 36.625° CapGrowYetFirst leftover
        // toward sci_a 40°. CapGrowYetFirst wrote leftover 36.625° (room
        // 1.125° at a 35.5° thigh); extra hipLift at room ≤ 0 on 36.625°
        // does not raise past that cap. DualLoadMinSpan never opens:
        // that is the lift phase. Same quarter as fold grow (Lerp
        // 36.625 → 40 at 0.25 = 37.46875°). At a 36.625° thigh, room =
        // 0.84375°. liftSignal stays −0.945 until the thigh walks.
        // Still 2.53125° short of sci_a, not −swingHipFlex. Air Fall/
        // Reach keeps 36.625°. Hip-unload hold would still be a no-op
        // (ThighHold already owns the swing hip on Fall via fold hold).
        // -walkPlantFallLiftCapGrowEvenFirst 0 restores the former
        // 36.625° CapGrowYetFirst leftover under the 0.972 trio. Catch
        // / later cycles keep the envelope. Not trailing A2, not
        // −swingHipFlex, and not a PD retune.
        balance.walkPlantFallLiftCapGrowEvenFirst = GetFloatArg(
            "-walkPlantFallLiftCapGrowEvenFirst", balance.walkPlantFallLiftCapGrowEvenFirst);
        // Same Fall fifth quarter of the 37.46875° CapGrowEvenFirst leftover
        // toward sci_a 40°. CapGrowEvenFirst wrote leftover 37.46875°
        // (room 0.84375° at a 36.625° thigh); extra hipLift at room ≤ 0
        // on 37.46875° does not raise past that cap. DualLoadMinSpan
        // never opens: that is the lift phase. Same quarter as fold
        // grow (Lerp 37.46875 → 40 at 0.25 = 38.1015625°). At a
        // 37.46875° thigh, room = 0.6328125°. liftSignal stays −0.945
        // until the thigh walks. Still 1.8984375° short of sci_a, not
        // −swingHipFlex. Air Fall/Reach keeps 37.46875°. Hip-unload
        // hold would still be a no-op (ThighHold already owns the swing
        // hip on Fall via fold hold).
        // -walkPlantFallLiftCapGrowStillFirst 0 restores the former
        // 37.46875° CapGrowEvenFirst leftover under the 0.972 trio.
        // Catch / later cycles keep the envelope. Not trailing A2, not
        // −swingHipFlex, and not a PD retune.
        balance.walkPlantFallLiftCapGrowStillFirst = GetFloatArg(
            "-walkPlantFallLiftCapGrowStillFirst", balance.walkPlantFallLiftCapGrowStillFirst);
        // Same Fall sixth quarter of the 38.1015625° CapGrowStillFirst leftover
        // toward sci_a 40°. CapGrowStillFirst wrote leftover 38.1015625°
        // (room 0.6328125° at a 37.46875° thigh); extra hipLift at room ≤ 0
        // on 38.1015625° does not raise past that cap. DualLoadMinSpan
        // never opens: that is the lift phase. Same quarter as fold
        // grow (Lerp 38.1015625 → 40 at 0.25 = 38.576171875°). At a
        // 38.1015625° thigh, room = 0.474609375°. liftSignal stays −0.945
        // until the thigh walks. Still 1.423828125° short of sci_a, not
        // −swingHipFlex. Air Fall/Reach keeps 38.1015625°. Hip-unload
        // hold would still be a no-op (ThighHold already owns the swing
        // hip on Fall via fold hold).
        // -walkPlantFallLiftCapGrowAgainFirst 0 restores the former
        // 38.1015625° CapGrowStillFirst leftover under the 0.972 trio.
        // Catch / later cycles keep the envelope. Not trailing A2, not
        // −swingHipFlex, and not a PD retune.
        balance.walkPlantFallLiftCapGrowAgainFirst = GetFloatArg(
            "-walkPlantFallLiftCapGrowAgainFirst", balance.walkPlantFallLiftCapGrowAgainFirst);
        // Same Fall seventh quarter of the 38.576171875° CapGrowAgainFirst leftover
        // toward sci_a 40°. CapGrowAgainFirst wrote leftover 38.576171875°
        // (room 0.474609375° at a 38.1015625° thigh); extra hipLift at room ≤ 0
        // on 38.576171875° does not raise past that cap. DualLoadMinSpan
        // never opens: that is the lift phase. Same quarter as fold
        // grow (Lerp 38.576171875 → 40 at 0.25 = 38.93212890625°). At a
        // 38.576171875° thigh, room = 0.35595703125°. liftSignal stays −0.945
        // until the thigh walks. Still 1.06787109375° short of sci_a, not
        // −swingHipFlex. Air Fall/Reach keeps 38.576171875°. Hip-unload
        // hold would still be a no-op (ThighHold already owns the swing hip
        // on Fall via fold hold).
        // -walkPlantFallLiftCapGrowAlsoFirst 0 restores the former
        // 38.576171875° CapGrowAgainFirst leftover under the 0.972 trio.
        // Catch / later cycles keep the envelope. Not trailing A2, not
        // −swingHipFlex, and not a PD retune.
        balance.walkPlantFallLiftCapGrowAlsoFirst = GetFloatArg(
            "-walkPlantFallLiftCapGrowAlsoFirst", balance.walkPlantFallLiftCapGrowAlsoFirst);
        // Same Fall eighth quarter of the 38.93212890625° CapGrowAlsoFirst leftover
        // toward sci_a 40°. CapGrowAlsoFirst wrote leftover 38.93212890625°
        // (room 0.35595703125° at a 38.576171875° thigh); extra hipLift at room ≤ 0
        // on 38.93212890625° does not raise past that cap. DualLoadMinSpan
        // never opens: that is the lift phase. Same quarter as fold
        // grow (Lerp 38.93212890625 → 40 at 0.25 = 39.1990966796875°). At a
        // 38.93212890625° thigh, room = 0.2669677734375°. liftSignal stays −0.945
        // until the thigh walks. Still 0.8009033203125° short of sci_a, not
        // −swingHipFlex. Air Fall/Reach keeps 38.93212890625°. Hip-unload
        // hold would still be a no-op (ThighHold already owns the swing hip
        // on Fall via fold hold).
        // -walkPlantFallLiftCapGrowExtraFirst 0 restores the former
        // 38.93212890625° CapGrowAlsoFirst leftover under the 0.972 trio.
        // Catch / later cycles keep the envelope. Not trailing A2, not
        // −swingHipFlex, and not a PD retune.
        balance.walkPlantFallLiftCapGrowExtraFirst = GetFloatArg(
            "-walkPlantFallLiftCapGrowExtraFirst", balance.walkPlantFallLiftCapGrowExtraFirst);
        // Same Fall ninth quarter of the 39.1990966796875° CapGrowExtraFirst leftover
        // toward sci_a 40°. CapGrowExtraFirst wrote leftover 39.1990966796875°
        // (room 0.2669677734375° at a 38.93212890625° thigh); extra hipLift at room ≤ 0
        // on 39.1990966796875° does not raise past that cap. DualLoadMinSpan
        // never opens: that is the lift phase. Same quarter as fold
        // grow (Lerp 39.1990966796875 → 40 at 0.25 = 39.399322509765625°). At a
        // 39.1990966796875° thigh, room = 0.200225830078125°. liftSignal stays −0.945
        // until the thigh walks. Still 0.600677490234375° short of sci_a, not
        // −swingHipFlex. Air Fall/Reach keeps 39.1990966796875°. Hip-unload
        // hold would still be a no-op (ThighHold already owns the swing hip
        // on Fall via fold hold).
        // -walkPlantFallLiftCapGrowPlusFirst 0 restores the former
        // 39.1990966796875° CapGrowExtraFirst leftover under the 0.972 trio.
        // Catch / later cycles keep the envelope. Not trailing A2, not
        // −swingHipFlex, and not a PD retune.
        balance.walkPlantFallLiftCapGrowPlusFirst = GetFloatArg(
            "-walkPlantFallLiftCapGrowPlusFirst", balance.walkPlantFallLiftCapGrowPlusFirst);
        // Same Fall tenth quarter of the 39.399322509765625° CapGrowPlusFirst leftover
        // toward sci_a 40°. CapGrowPlusFirst wrote leftover 39.399322509765625°
        // (room 0.200225830078125° at a 39.1990966796875° thigh); extra hipLift at room ≤ 0
        // on 39.399322509765625° does not raise past that cap. DualLoadMinSpan
        // never opens: that is the lift phase. Same quarter as fold
        // grow (Lerp 39.399322509765625 → 40 at 0.25 = 39.54949188232421875°). At a
        // 39.399322509765625° thigh, room = 0.15016937255859375°. liftSignal stays −0.945
        // until the thigh walks. Still 0.45050811767578125° short of sci_a, not
        // −swingHipFlex. Air Fall/Reach keeps 39.399322509765625°. Hip-unload
        // hold would still be a no-op (ThighHold already owns the swing hip
        // on Fall via fold hold).
        // -walkPlantFallLiftCapGrowFurtherFirst 0 restores the former
        // 39.399322509765625° CapGrowPlusFirst leftover under the 0.972 trio.
        // Catch / later cycles keep the envelope. Not trailing A2, not
        // −swingHipFlex, and not a PD retune.
        balance.walkPlantFallLiftCapGrowFurtherFirst = GetFloatArg(
            "-walkPlantFallLiftCapGrowFurtherFirst", balance.walkPlantFallLiftCapGrowFurtherFirst);
        // Same Fall eleventh quarter of the 39.54949188232421875° CapGrowFurtherFirst leftover
        // toward sci_a 40°. CapGrowFurtherFirst wrote leftover 39.54949188232421875°
        // (room 0.15016937255859375° at a 39.399322509765625° thigh); extra hipLift at room ≤ 0
        // on 39.54949188232421875° does not raise past that cap. DualLoadMinSpan
        // never opens: that is the lift phase. Same quarter as fold
        // grow (Lerp 39.54949188232421875 → 40 at 0.25 = 39.6621189117431640625°). At a
        // 39.54949188232421875° thigh, room = 0.1126270294189453125°. liftSignal stays −0.945
        // until the thigh walks. Still 0.3378810882568359375° short of sci_a, not
        // −swingHipFlex. Air Fall/Reach keeps 39.54949188232421875°. Hip-unload
        // hold would still be a no-op (ThighHold already owns the swing hip
        // on Fall via fold hold).
        // -walkPlantFallLiftCapGrowOnwardFirst 0 restores the former
        // 39.54949188232421875° CapGrowFurtherFirst leftover under the 0.972 trio.
        // Catch / later cycles keep the envelope. Not trailing A2, not
        // −swingHipFlex, and not a PD retune.
        balance.walkPlantFallLiftCapGrowOnwardFirst = GetFloatArg(
            "-walkPlantFallLiftCapGrowOnwardFirst", balance.walkPlantFallLiftCapGrowOnwardFirst);
        // Same Fall twelfth quarter of the 39.6621189117431640625° CapGrowOnwardFirst leftover
        // toward sci_a 40°. CapGrowOnwardFirst wrote leftover 39.6621189117431640625°
        // (room 0.1126270294189453125° at a 39.54949188232421875° thigh); extra hipLift at room ≤ 0
        // on 39.6621189117431640625° does not raise past that cap. DualLoadMinSpan
        // never opens: that is the lift phase. Same quarter as fold
        // grow (Lerp 39.6621189117431640625 → 40 at 0.25 = 39.746589183807373046875°). At a
        // 39.6621189117431640625° thigh, room = 0.084470272064208984375°. liftSignal stays −0.945
        // until the thigh walks. Still 0.253410816192626953125° short of sci_a, not
        // −swingHipFlex. Air Fall/Reach keeps 39.6621189117431640625°. Hip-unload
        // hold would still be a no-op (ThighHold already owns the swing hip
        // on Fall via fold hold).
        // -walkPlantFallLiftCapGrowAheadFirst 0 restores the former
        // 39.6621189117431640625° CapGrowOnwardFirst leftover under the 0.972 trio.
        // Catch / later cycles keep the envelope. Not trailing A2, not
        // −swingHipFlex, and not a PD retune.
        balance.walkPlantFallLiftCapGrowAheadFirst = GetFloatArg(
            "-walkPlantFallLiftCapGrowAheadFirst", balance.walkPlantFallLiftCapGrowAheadFirst);
        // Same Fall thirteenth quarter of the 39.746589183807373046875° CapGrowAheadFirst leftover
        // toward sci_a 40°. CapGrowAheadFirst wrote leftover 39.746589183807373046875°
        // (room 0.084470272064208984375° at a 39.6621189117431640625° thigh); extra hipLift at room ≤ 0
        // on 39.746589183807373046875° does not raise past that cap. DualLoadMinSpan
        // never opens: that is the lift phase. Same quarter as fold
        // grow (Lerp 39.746589183807373046875 → 40 at 0.25 = 39.80994188785552978515625°). At a
        // 39.746589183807373046875° thigh, room = 0.06335270404815673828125°. liftSignal stays −0.945
        // until the thigh walks. Still 0.19005811214447021484375° short of sci_a, not
        // −swingHipFlex. Air Fall/Reach keeps 39.746589183807373046875°. Hip-unload
        // hold would still be a no-op (ThighHold already owns the swing hip
        // on Fall via fold hold).
        // -walkPlantFallLiftCapGrowAlongFirst 0 restores the former
        // 39.746589183807373046875° CapGrowAheadFirst leftover under the 0.972 trio.
        // Catch / later cycles keep the envelope. Not trailing A2, not
        // −swingHipFlex, and not a PD retune.
        balance.walkPlantFallLiftCapGrowAlongFirst = GetFloatArg(
            "-walkPlantFallLiftCapGrowAlongFirst", balance.walkPlantFallLiftCapGrowAlongFirst);
        // Same Fall fourteenth quarter of the 39.80994188785552978515625° CapGrowAlongFirst leftover
        // toward sci_a 40°. CapGrowAlongFirst wrote leftover 39.80994188785552978515625°
        // (room 0.06335270404815673828125° at a 39.746589183807373046875° thigh); extra hipLift at room ≤ 0
        // on 39.80994188785552978515625° does not raise past that cap. DualLoadMinSpan
        // never opens: that is the lift phase. Same quarter as fold
        // grow (Lerp 39.80994188785552978515625 → 40 at 0.25 = 39.8574564158916473388671875°). At a
        // 39.80994188785552978515625° thigh, room = 0.0475145280361175537109375°. liftSignal stays −0.945
        // until the thigh walks. Still 0.1425435841083526611328125° short of sci_a, not
        // −swingHipFlex. Air Fall/Reach keeps 39.80994188785552978515625°. Hip-unload
        // hold would still be a no-op (ThighHold already owns the swing hip
        // on Fall via fold hold).
        // -walkPlantFallLiftCapGrowForthFirst 0 restores the former
        // 39.80994188785552978515625° CapGrowAlongFirst leftover under the 0.972 trio.
        // Catch / later cycles keep the envelope. Not trailing A2, not
        // −swingHipFlex, and not a PD retune.
        balance.walkPlantFallLiftCapGrowForthFirst = GetFloatArg(
            "-walkPlantFallLiftCapGrowForthFirst", balance.walkPlantFallLiftCapGrowForthFirst);
        // Same Fall fifteenth quarter of the 39.8574564158916473388671875° CapGrowForthFirst leftover
        // toward sci_a 40°. CapGrowForthFirst wrote leftover 39.8574564158916473388671875°
        // (room 0.0475145280361175537109375° at a 39.80994188785552978515625° thigh); extra hipLift at room ≤ 0
        // on 39.8574564158916473388671875° does not raise past that cap. DualLoadMinSpan
        // never opens: that is the lift phase. Same quarter as fold
        // grow (Lerp 39.8574564158916473388671875 → 40 at 0.25 = 39.893092311918735504150390625°). At a
        // 39.8574564158916473388671875° thigh, room = 0.035635896027088165283203125°. liftSignal stays −0.945
        // until the thigh walks. Still 0.106907688081264495849609375° short of sci_a, not
        // −swingHipFlex. Air Fall/Reach keeps 39.8574564158916473388671875°. Hip-unload
        // hold would still be a no-op (ThighHold already owns the swing hip
        // on Fall via fold hold).
        // -walkPlantFallLiftCapGrowBeyondFirst 0 restores the former
        // 39.8574564158916473388671875° CapGrowForthFirst leftover under the 0.972 trio.
        // Catch / later cycles keep the envelope. Not trailing A2, not
        // −swingHipFlex, and not a PD retune.
        balance.walkPlantFallLiftCapGrowBeyondFirst = GetFloatArg(
            "-walkPlantFallLiftCapGrowBeyondFirst", balance.walkPlantFallLiftCapGrowBeyondFirst);
        // Same Fall sixteenth quarter of the 39.893092311918735504150390625° CapGrowBeyondFirst leftover
        // toward sci_a 40°. CapGrowBeyondFirst wrote leftover 39.893092311918735504150390625°
        // (room 0.035635896027088165283203125° at a 39.8574564158916473388671875° thigh); extra hipLift at room ≤ 0
        // on 39.893092311918735504150390625° does not raise past that cap. DualLoadMinSpan
        // never opens: that is the lift phase. Same quarter as fold
        // grow (Lerp 39.893092311918735504150390625 → 40 at 0.25 = 39.91981923393905162811279296875°). At a
        // 39.893092311918735504150390625° thigh, room = 0.02672692202031612396240234375°. liftSignal stays −0.945
        // until the thigh walks. Still 0.08018076606094837188720703125° short of sci_a, not
        // −swingHipFlex. Air Fall/Reach keeps 39.893092311918735504150390625°. Hip-unload
        // hold would still be a no-op (ThighHold already owns the swing hip
        // on Fall via fold hold).
        // -walkPlantFallLiftCapGrowPastFirst 0 restores the former
        // 39.893092311918735504150390625° CapGrowBeyondFirst leftover under the 0.972 trio.
        // Catch / later cycles keep the envelope. Not trailing A2, not
        // −swingHipFlex, and not a PD retune.
        balance.walkPlantFallLiftCapGrowPastFirst = GetFloatArg(
            "-walkPlantFallLiftCapGrowPastFirst", balance.walkPlantFallLiftCapGrowPastFirst);
        // Same Fall seventeenth quarter of the 39.91981923393905162811279296875° CapGrowPastFirst leftover
        // toward sci_a 40°. CapGrowPastFirst wrote leftover 39.91981923393905162811279296875°
        // (room 0.02672692202031612396240234375° at a 39.893092311918735504150390625° thigh); extra hipLift at room ≤ 0
        // on 39.91981923393905162811279296875° does not raise past that cap. DualLoadMinSpan
        // never opens: that is the lift phase. Same quarter as fold
        // grow (Lerp 39.91981923393905162811279296875 → 40 at 0.25 = 39.9398644254542887210845947265625°). At a
        // 39.91981923393905162811279296875° thigh, room = 0.0200451915152370929718017578125°. liftSignal stays −0.945
        // until the thigh walks. Still 0.0601355745457112789154052734375° short of sci_a, not
        // −swingHipFlex. Air Fall/Reach keeps 39.91981923393905162811279296875°. Hip-unload
        // hold would still be a no-op (ThighHold already owns the swing hip
        // on Fall via fold hold).
        // -walkPlantFallLiftCapGrowOverFirst 0 restores the former
        // 39.91981923393905162811279296875° CapGrowPastFirst leftover under the 0.972 trio.
        // Catch / later cycles keep the envelope. Not trailing A2, not
        // −swingHipFlex, and not a PD retune.
        balance.walkPlantFallLiftCapGrowOverFirst = GetFloatArg(
            "-walkPlantFallLiftCapGrowOverFirst", balance.walkPlantFallLiftCapGrowOverFirst);
        // Same Fall eighteenth quarter of the 39.9398644254542887210845947265625° CapGrowOverFirst leftover
        // toward sci_a 40°. CapGrowOverFirst wrote leftover 39.9398644254542887210845947265625°
        // (room 0.0200451915152370929718017578125° at a 39.91981923393905162811279296875° thigh); extra hipLift at room ≤ 0
        // on 39.9398644254542887210845947265625° does not raise past that cap. DualLoadMinSpan
        // never opens: that is the lift phase. Same quarter as fold
        // grow (Lerp 39.9398644254542887210845947265625 → 40 at 0.25 = 39.954898319090716540813446044921875°). At a
        // 39.9398644254542887210845947265625° thigh, room = 0.015033893636427819728851318359375°. liftSignal stays −0.945
        // until the thigh walks. Still 0.045101680909283459186553955078125° short of sci_a, not
        // −swingHipFlex. Air Fall/Reach keeps 39.9398644254542887210845947265625°. Hip-unload
        // hold would still be a no-op (ThighHold already owns the swing hip
        // on Fall via fold hold).
        // -walkPlantFallLiftCapGrowAcrossFirst 0 restores the former
        // 39.9398644254542887210845947265625° CapGrowOverFirst leftover under the 0.972 trio.
        // Catch / later cycles keep the envelope. Not trailing A2, not
        // −swingHipFlex, and not a PD retune.
        balance.walkPlantFallLiftCapGrowAcrossFirst = GetFloatArg(
            "-walkPlantFallLiftCapGrowAcrossFirst", balance.walkPlantFallLiftCapGrowAcrossFirst);
        // Same Fall second quarter of the grounded fold. FoldGrowFirst
        // wrote 76° (Lerp 55→140 at 0.25). Leftover-cap grow through
        // AcrossFirst is spent: 0.045° short of sci_a, ~0.011 cm of a
        // 0.43 m thigh, fade stuck at 0.972 so liftSignal stays −0.945.
        // DualLoadMinSpan never opens: that is the lift phase. Knee
        // torque is thigh–shin, so more fold shortens the swing chain
        // without asking the hip past 40°. Same quarter as fold grow
        // (Lerp 76 → 140 at 0.25 = 92°, 2·L·cos(46°) ≈ 0.70 L vs
        // 2·L·cos(38°) ≈ 0.79 L, ~8 cm of a 0.86 m chain). Seed already
        // Maxes to groundedCap. Air Fall/Reach keeps 140°. Hip-unload
        // hold would still be a no-op (ThighHold already owns the swing
        // hip on Fall via fold hold).
        // -walkPlantFallFoldGrowMoreFirst 0 restores the former 76°
        // grow cap on the first grounded Fall/Reach. Catch / later
        // cycles keep the envelope. Not trailing A2, not −swingHipFlex,
        // and not a PD retune.
        balance.walkPlantFallFoldGrowMoreFirst = GetFloatArg(
            "-walkPlantFallFoldGrowMoreFirst", balance.walkPlantFallFoldGrowMoreFirst);
        // Same Fall third quarter of the grounded fold. FoldGrowMoreFirst
        // wrote 92° (Lerp 76→140 at 0.25). Leftover-cap grow through
        // AcrossFirst is spent: 0.045° short of sci_a, ~0.011 cm of a
        // 0.43 m thigh, fade stuck at 0.972 so liftSignal stays −0.945.
        // DualLoadMinSpan never opens: that is the lift phase. Knee
        // torque is thigh–shin, so more fold shortens the swing chain
        // without asking the hip past 40°. Same quarter as fold grow
        // (Lerp 92 → 140 at 0.25 = 104°, 2·L·cos(52°) ≈ 0.62 L vs
        // 2·L·cos(46°) ≈ 0.70 L, ~7 cm of a 0.86 m chain). Seed already
        // Maxes to groundedCap. Air Fall/Reach keeps 140°. Hip-unload
        // hold would still be a no-op (ThighHold already owns the swing
        // hip on Fall via fold hold).
        // -walkPlantFallFoldGrowYetFirst 0 restores the former 92°
        // grow cap on the first grounded Fall/Reach. Catch / later
        // cycles keep the envelope. Not trailing A2, not −swingHipFlex,
        // and not a PD retune.
        balance.walkPlantFallFoldGrowYetFirst = GetFloatArg(
            "-walkPlantFallFoldGrowYetFirst", balance.walkPlantFallFoldGrowYetFirst);
        // Same Fall fourth quarter of the grounded fold. FoldGrowYetFirst
        // wrote 104° (Lerp 92→140 at 0.25). Leftover-cap grow through
        // AcrossFirst is spent: 0.045° short of sci_a, ~0.011 cm of a
        // 0.43 m thigh, fade stuck at 0.972 so liftSignal stays −0.945.
        // DualLoadMinSpan never opens: that is the lift phase. Knee
        // torque is thigh–shin, so more fold shortens the swing chain
        // without asking the hip past 40°. Same quarter as fold grow
        // (Lerp 104 → 140 at 0.25 = 113°, 2·L·cos(56.5°) ≈ 0.55 L vs
        // 2·L·cos(52°) ≈ 0.62 L, ~6 cm of a 0.86 m chain). Seed already
        // Maxes to groundedCap. Air Fall/Reach keeps 140°. Hip-unload
        // hold would still be a no-op (ThighHold already owns the swing
        // hip on Fall via fold hold).
        // -walkPlantFallFoldGrowEvenFirst 0 restores the former 104°
        // grow cap on the first grounded Fall/Reach. Catch / later
        // cycles keep the envelope. Not trailing A2, not −swingHipFlex,
        // and not a PD retune.
        balance.walkPlantFallFoldGrowEvenFirst = GetFloatArg(
            "-walkPlantFallFoldGrowEvenFirst", balance.walkPlantFallFoldGrowEvenFirst);
        // Same Fall fifth quarter of the grounded fold. FoldGrowEvenFirst
        // wrote 113° (Lerp 104→140 at 0.25). Leftover-cap grow through
        // AcrossFirst is spent: 0.045° short of sci_a, ~0.011 cm of a
        // 0.43 m thigh, fade stuck at 0.972 so liftSignal stays −0.945.
        // DualLoadMinSpan never opens: that is the lift phase. Knee
        // torque is thigh–shin, so more fold shortens the swing chain
        // without asking the hip past 40°. Same quarter as fold grow
        // (Lerp 113 → 140 at 0.25 = 120°, 2·L·cos(60°) ≈ 0.50 L vs
        // 2·L·cos(56.5°) ≈ 0.55 L, ~4 cm of a 0.86 m chain). Seed already
        // Maxes to groundedCap. Air Fall/Reach keeps 140°. Hip-unload
        // hold would still be a no-op (ThighHold already owns the swing
        // hip on Fall via fold hold).
        // -walkPlantFallFoldGrowStillFirst 0 restores the former 113°
        // grow cap on the first grounded Fall/Reach. Catch / later
        // cycles keep the envelope. Not trailing A2, not −swingHipFlex,
        // and not a PD retune.
        balance.walkPlantFallFoldGrowStillFirst = GetFloatArg(
            "-walkPlantFallFoldGrowStillFirst", balance.walkPlantFallFoldGrowStillFirst);
        // Same Fall sixth quarter of the grounded fold. FoldGrowStillFirst
        // wrote 120° (Lerp 113→140 at 0.25). Leftover-cap grow through
        // AcrossFirst is spent: 0.045° short of sci_a, ~0.011 cm of a
        // 0.43 m thigh, fade stuck at 0.972 so liftSignal stays −0.945.
        // DualLoadMinSpan never opens: that is the lift phase. Knee
        // torque is thigh–shin, so more fold shortens the swing chain
        // without asking the hip past 40°. Same quarter as fold grow
        // (Lerp 120 → 140 at 0.25 = 125°, 2·L·cos(62.5°) ≈ 0.46 L vs
        // 2·L·cos(60°) ≈ 0.50 L, ~3 cm of a 0.86 m chain). Seed already
        // Maxes to groundedCap. Air Fall/Reach keeps 140°. Hip-unload
        // hold would still be a no-op (ThighHold already owns the swing
        // hip on Fall via fold hold).
        // -walkPlantFallFoldGrowAgainFirst 0 restores the former 120°
        // grow cap on the first grounded Fall/Reach. Catch / later
        // cycles keep the envelope. Not trailing A2, not −swingHipFlex,
        // and not a PD retune.
        balance.walkPlantFallFoldGrowAgainFirst = GetFloatArg(
            "-walkPlantFallFoldGrowAgainFirst", balance.walkPlantFallFoldGrowAgainFirst);
        // Same Fall seventh quarter of the grounded fold. FoldGrowAgainFirst
        // wrote 125° (Lerp 120→140 at 0.25). Leftover-cap grow through
        // AcrossFirst is spent: 0.045° short of sci_a, ~0.011 cm of a
        // 0.43 m thigh, fade stuck at 0.972 so liftSignal stays −0.945.
        // DualLoadMinSpan never opens: that is the lift phase. Knee
        // torque is thigh–shin, so more fold shortens the swing chain
        // without asking the hip past 40°. Same quarter as fold grow
        // (Lerp 125 → 140 at 0.25 = 129°, 2·L·cos(64.5°) ≈ 0.43 L vs
        // 2·L·cos(62.5°) ≈ 0.46 L, ~3 cm of a 0.86 m chain). Seed already
        // Maxes to groundedCap. Air Fall/Reach keeps 140°. Hip-unload
        // hold would still be a no-op (ThighHold already owns the swing
        // hip on Fall via fold hold).
        // -walkPlantFallFoldGrowAlsoFirst 0 restores the former 125°
        // grow cap on the first grounded Fall/Reach. Catch / later
        // cycles keep the envelope. Not trailing A2, not −swingHipFlex,
        // and not a PD retune.
        balance.walkPlantFallFoldGrowAlsoFirst = GetFloatArg(
            "-walkPlantFallFoldGrowAlsoFirst", balance.walkPlantFallFoldGrowAlsoFirst);
        // Same Fall eighth quarter of the grounded fold. FoldGrowAlsoFirst
        // wrote 129° (Lerp 125→140 at 0.25). Leftover-cap grow through
        // AcrossFirst is spent: 0.045° short of sci_a, ~0.011 cm of a
        // 0.43 m thigh, fade stuck at 0.972 so liftSignal stays −0.945.
        // DualLoadMinSpan never opens: that is the lift phase. Knee
        // torque is thigh–shin, so more fold shortens the swing chain
        // without asking the hip past 40°. Same quarter as fold grow
        // (Lerp 129 → 140 at 0.25 = 132°, 2·L·cos(66°) ≈ 0.41 L vs
        // 2·L·cos(64.5°) ≈ 0.43 L, ~2 cm of a 0.86 m chain). Seed already
        // Maxes to groundedCap. Air Fall/Reach keeps 140°. Hip-unload
        // hold would still be a no-op (ThighHold already owns the swing
        // hip on Fall via fold hold).
        // -walkPlantFallFoldGrowExtraFirst 0 restores the former 129°
        // grow cap on the first grounded Fall/Reach. Catch / later
        // cycles keep the envelope. Not trailing A2, not −swingHipFlex,
        // and not a PD retune.
        balance.walkPlantFallFoldGrowExtraFirst = GetFloatArg(
            "-walkPlantFallFoldGrowExtraFirst", balance.walkPlantFallFoldGrowExtraFirst);
        // WALK-2 initiation is knee lift + stance drive + forward lean.
        // Leftover-cap grow through AcrossFirst is spent: 0.045° short
        // of sci_a, ~0.011 cm of a 0.43 m thigh, fade stuck at 0.972
        // so liftSignal stays −0.945. DualLoadMinSpan never opens:
        // that is the lift phase. Knee torque is thigh–shin, so more
        // fold shortens the swing chain without asking the hip past 40°.
        // Same quarter as fold grow (Lerp 132 → 140 at 0.25 = 134°,
        // 2·L·cos(67°) ≈ 0.39 L vs 2·L·cos(66°) ≈ 0.41 L, ~1 cm of a
        // 0.86 m chain). Seed already Maxes to groundedCap. Air Fall/Reach
        // keeps 140°. Hip-unload hold would still be a no-op (ThighHold
        // already owns the swing hip on Fall via fold hold).
        // -walkPlantFallFoldGrowPlusFirst 0 restores the former 132°
        // grow cap on the first grounded Fall/Reach. Catch / later
        // cycles keep the envelope. Not trailing A2, not −swingHipFlex,
        // and not a PD retune.
        balance.walkPlantFallFoldGrowPlusFirst = GetFloatArg(
            "-walkPlantFallFoldGrowPlusFirst", balance.walkPlantFallFoldGrowPlusFirst);
        // WALK-2 initiation is knee lift + stance drive + forward lean.
        // Leftover-cap grow through AcrossFirst is spent: 0.045° short
        // of sci_a, ~0.011 cm of a 0.43 m thigh, fade stuck at 0.972
        // so liftSignal stays −0.945. DualLoadMinSpan never opens:
        // that is the lift phase. Knee torque is thigh–shin, so more
        // fold shortens the swing chain without asking the hip past 40°.
        // Same quarter as fold grow (Lerp 134 → 140 at 0.25 = 136°,
        // 2·L·cos(68°) ≈ 0.37 L vs 2·L·cos(67°) ≈ 0.39 L, ~1 cm of a
        // 0.86 m chain). Seed already Maxes to groundedCap. Air Fall/Reach
        // keeps 140°. Hip-unload hold would still be a no-op (ThighHold
        // already owns the swing hip on Fall via fold hold).
        // -walkPlantFallFoldGrowFurtherFirst 0 restores the former 134°
        // grow cap on the first grounded Fall/Reach. Catch / later
        // cycles keep the envelope. Not trailing A2, not −swingHipFlex,
        // and not a PD retune.
        balance.walkPlantFallFoldGrowFurtherFirst = GetFloatArg(
            "-walkPlantFallFoldGrowFurtherFirst", balance.walkPlantFallFoldGrowFurtherFirst);
        // WALK-2 initiation is knee lift + stance drive + forward lean.
        // Leftover-cap grow through AcrossFirst is spent: 0.045° short
        // of sci_a, ~0.011 cm of a 0.43 m thigh, fade stuck at 0.972
        // so liftSignal stays −0.945. DualLoadMinSpan never opens:
        // that is the lift phase. Knee torque is thigh–shin, so more
        // fold shortens the swing chain without asking the hip past 40°.
        // Same quarter as fold grow (Lerp 136 → 140 at 0.25 = 137°,
        // 2·L·cos(68.5°) ≈ 0.37 L vs 2·L·cos(68°) ≈ 0.37 L, ~1 cm of a
        // 0.86 m chain). Seed already Maxes to groundedCap. Air Fall/Reach
        // keeps 140°. Hip-unload hold would still be a no-op (ThighHold
        // already owns the swing hip on Fall via fold hold).
        // -walkPlantFallFoldGrowOnwardFirst 0 restores the former 136°
        // grow cap on the first grounded Fall/Reach. Catch / later
        // cycles keep the envelope. Not trailing A2, not −swingHipFlex,
        // and not a PD retune.
        balance.walkPlantFallFoldGrowOnwardFirst = GetFloatArg(
            "-walkPlantFallFoldGrowOnwardFirst", balance.walkPlantFallFoldGrowOnwardFirst);
        // WALK-2 initiation is knee lift + stance drive + forward lean.
        // Leftover-cap grow through AcrossFirst is spent: 0.045° short
        // of sci_a, ~0.011 cm of a 0.43 m thigh, fade stuck at 0.972
        // so liftSignal stays −0.945. DualLoadMinSpan never opens:
        // that is the lift phase. Knee torque is thigh–shin, so more
        // fold shortens the swing chain without asking the hip past 40°.
        // Same quarter as fold grow (Lerp 137 → 140 at 0.25 = 137.75°
        // → 138°, 2·L·cos(69°) ≈ 0.36 L vs 2·L·cos(68.5°) ≈ 0.37 L,
        // ~0.7 cm of a 0.86 m chain). Seed already Maxes to groundedCap.
        // Air Fall/Reach keeps 140°. Hip-unload hold would still be a
        // no-op (ThighHold already owns the swing hip on Fall via
        // fold hold).
        // -walkPlantFallFoldGrowAheadFirst 0 restores the former 137°
        // grow cap on the first grounded Fall/Reach. Catch / later
        // cycles keep the envelope. Not trailing A2, not −swingHipFlex,
        // and not a PD retune.
        balance.walkPlantFallFoldGrowAheadFirst = GetFloatArg(
            "-walkPlantFallFoldGrowAheadFirst", balance.walkPlantFallFoldGrowAheadFirst);
        // WALK-2 initiation is knee lift + stance drive + forward lean.
        // Leftover-cap grow through AcrossFirst is spent: 0.045° short
        // of sci_a, ~0.011 cm of a 0.43 m thigh, fade stuck at 0.972
        // so liftSignal stays −0.945. DualLoadMinSpan never opens:
        // that is the lift phase. Knee torque is thigh–shin, so more
        // fold shortens the swing chain without asking the hip past 40°.
        // Same quarter as fold grow (Lerp 138 → 140 at 0.25 = 138.5°
        // → 139°, 2·L·cos(69.5°) ≈ 0.35 L vs 2·L·cos(69°) ≈ 0.36 L,
        // ~0.7 cm of a 0.86 m chain). Seed already Maxes to groundedCap.
        // Air Fall/Reach keeps 140°. Hip-unload hold would still be a
        // no-op (ThighHold already owns the swing hip on Fall via
        // fold hold).
        // -walkPlantFallFoldGrowAlongFirst 0 restores the former 138°
        // grow cap on the first grounded Fall/Reach. Catch / later
        // cycles keep the envelope. Not trailing A2, not −swingHipFlex,
        // and not a PD retune.
        balance.walkPlantFallFoldGrowAlongFirst = GetFloatArg(
            "-walkPlantFallFoldGrowAlongFirst", balance.walkPlantFallFoldGrowAlongFirst);
        // Same Fall grow of the grounded fold a fourteenth quarter.
        // Leftover-cap grow through AcrossFirst is spent (0.045° short
        // of sci_a, ~0.011 cm of a 0.43 m thigh, fade stuck at 0.972
        // so liftSignal stays −0.945. DualLoadMinSpan never opens:
        // that is the lift phase. Knee torque is thigh–shin, so more
        // fold shortens the swing chain without asking the hip past 40°.
        // Same quarter as fold grow (Lerp 139 → 140 at 0.25 = 139.25°
        // → 139°, 2·L·cos(69.625°) ≈ 0.35 L vs 2·L·cos(69.5°) ≈ 0.35 L,
        // ~0.2 cm of a 0.86 m chain). Seed already Maxes to groundedCap.
        // Air Fall/Reach keeps 140°. Hip-unload hold would still be a
        // no-op (ThighHold already owns the swing hip on Fall via
        // fold hold).
        // -walkPlantFallFoldGrowCloseFirst 0 restores the former 139°
        // grow cap on the first grounded Fall/Reach. Catch / later
        // cycles keep the envelope. Not trailing A2, not −swingHipFlex,
        // and not a PD retune.
        balance.walkPlantFallFoldGrowCloseFirst = GetFloatArg(
            "-walkPlantFallFoldGrowCloseFirst", balance.walkPlantFallFoldGrowCloseFirst);
        // Same Fall grow of the grounded fold a fifteenth quarter.
        // Leftover-cap grow through AcrossFirst is spent (0.045° short
        // of sci_a, ~0.011 cm of a 0.43 m thigh, fade stuck at 0.972
        // so liftSignal stays −0.945. DualLoadMinSpan never opens:
        // that is the lift phase. Knee torque is thigh–shin, so more
        // fold shortens the swing chain without asking the hip past 40°.
        // Same quarter as fold grow (Lerp 139.25 → 140 at 0.25 = 139.44°
        // → 139.5°, 2·L·cos(69.72°) ≈ 0.35 L vs 2·L·cos(69.625°) ≈ 0.35 L,
        // ~0.1 cm of a 0.86 m chain). Seed already Maxes to groundedCap.
        // Air Fall/Reach keeps 140°. Hip-unload hold would still be a
        // no-op (ThighHold already owns the swing hip on Fall via
        // fold hold).
        // -walkPlantFallFoldGrowNearFirst 0 restores the former 139.25°
        // grow cap on the first grounded Fall/Reach. Catch / later
        // cycles keep the envelope. Not trailing A2, not −swingHipFlex,
        // and not a PD retune.
        balance.walkPlantFallFoldGrowNearFirst = GetFloatArg(
            "-walkPlantFallFoldGrowNearFirst", balance.walkPlantFallFoldGrowNearFirst);
        // Same Fall pin of the grounded fold to reachKneeFlex. Nested
        // 0.25 from Plant 55° is ~138.86°, not the rounded 139.5°.
        // Leftover-cap grow through AcrossFirst is spent: 0.045° short
        // of sci_a, ~0.011 cm of a 0.43 m thigh, fade stuck at 0.972
        // so liftSignal stays −0.945. DualLoadMinSpan never opens:
        // that is the lift phase. Knee torque is thigh–shin, so the
        // pin shortens the swing chain without asking the hip past 40°.
        // 2·L·cos(70°) ≈ 0.34 L vs 2·L·cos(69.43°) ≈ 0.35 L, ~0.8 cm
        // of a 0.86 m chain. Seed already Maxes to groundedCap. Air
        // Fall/Reach already keeps 140°. Hip-unload hold would still
        // be a no-op (ThighHold already owns the swing hip on Fall via
        // fold hold).
        // -walkPlantFallFoldGrowReachFirst 0 restores the former nested
        // ~138.86° grow cap on the first grounded Fall/Reach. Catch /
        // later cycles keep the envelope. Not trailing A2, not
        // −swingHipFlex, and not a PD retune.
        balance.walkPlantFallFoldGrowReachFirst = GetFloatArg(
            "-walkPlantFallFoldGrowReachFirst", balance.walkPlantFallFoldGrowReachFirst);
        // Same Fall grow of the stance-hip drive load. DualLoadMinSpan
        // never opens, so the held walkStanceHipExtend still multiplies
        // the 0.5 floor: 0.15×0.5 = 0.075 of the 0.25 clamp — half the
        // pelvis-forward the 140° pin needs. Opening DualLoad to feed
        // that unload would Lerp dualSupportStandCap 0.40→0.55 at
        // blend 0.25 (the recorded cap fold). Same quarter on the load
        // term only (Lerp 0.5→1 at 0.25 = 0.625, drive 0.09375). Plant
        // clock 1 already writes it so EnterFall is not a step.
        // firstRoom still fades at plantComLeadMax.
        // -walkPlantFallStanceDriveGrowFirst 0 restores the former
        // 0.5-floor drive under the 140° shin. Catch / later cycles
        // keep DualLoad / pushAfterAir. Not leftover-cap, not
        // Clamp01, not another millimetre 0.25, not trailing A2, not
        // −swingHipFlex, not a DualLoad open, and not a PD retune.
        balance.walkPlantFallStanceDriveGrowFirst = GetFloatArg(
            "-walkPlantFallStanceDriveGrowFirst", balance.walkPlantFallStanceDriveGrowFirst);
        // Same Fall grow of the stance-hip drive load a second quarter.
        // DualLoadMinSpan never opens, so GrowFirst still multiplies
        // 0.625: 0.15×0.625 = 0.09375 of the 0.25 clamp — short of the
        // pelvis-forward the 140° pin needs. Opening DualLoad to feed
        // that unload would Lerp dualSupportStandCap 0.40→0.55 at
        // blend 0.25 (the recorded cap fold). Same quarter on the load
        // term only (Lerp 0.625→1 at 0.25 = 0.719, drive 0.1078). Plant
        // clock 1 already writes it so EnterFall is not a step.
        // firstRoom still fades at plantComLeadMax.
        // -walkPlantFallStanceDriveGrowMoreFirst 0 restores the former
        // 0.625-load drive under the 140° shin. Catch / later cycles
        // keep DualLoad / pushAfterAir. Not leftover-cap, not
        // Clamp01, not another millimetre 0.25, not trailing A2, not
        // −swingHipFlex, not a DualLoad open, and not a PD retune.
        balance.walkPlantFallStanceDriveGrowMoreFirst = GetFloatArg(
            "-walkPlantFallStanceDriveGrowMoreFirst", balance.walkPlantFallStanceDriveGrowMoreFirst);
        // Same Fall grow of the stance-hip drive load a third quarter.
        // DualLoadMinSpan never opens, so GrowMoreFirst still multiplies
        // 0.719: 0.15×0.719 = 0.1078 of the 0.25 clamp — short of the
        // pelvis-forward the 140° pin needs. Opening DualLoad to feed
        // that unload would Lerp dualSupportStandCap 0.40→0.55 at
        // blend 0.25 (the recorded cap fold). Same quarter on the load
        // term only (Lerp 0.719→1 at 0.25 = 0.789, drive 0.1184). Plant
        // clock 1 already writes it so EnterFall is not a step.
        // firstRoom still fades at plantComLeadMax.
        // -walkPlantFallStanceDriveGrowStillFirst 0 restores the former
        // 0.719-load drive under the 140° shin. Catch / later cycles
        // keep DualLoad / pushAfterAir. Not leftover-cap, not
        // Clamp01, not another millimetre 0.25, not trailing A2, not
        // −swingHipFlex, not a DualLoad open, and not a PD retune.
        balance.walkPlantFallStanceDriveGrowStillFirst = GetFloatArg(
            "-walkPlantFallStanceDriveGrowStillFirst", balance.walkPlantFallStanceDriveGrowStillFirst);
        // Same Fall grow of the stance-hip drive load a fourth quarter.
        // DualLoadMinSpan never opens, so GrowStillFirst still multiplies
        // 0.789: 0.15×0.789 = 0.1184 of the 0.25 clamp — short of the
        // pelvis-forward the 140° pin needs. Opening DualLoad to feed
        // that unload would Lerp dualSupportStandCap 0.40→0.55 at
        // blend 0.25 (the recorded cap fold). Pinning 0.789→1.0 is a
        // 27% drive jump; leftover is not millimetres yet. Same quarter
        // on the load term only (Lerp 0.789→1 at 0.25 = 0.842, drive
        // 0.1263). Plant clock 1 already writes it so EnterFall is not
        // a step. firstRoom still fades at plantComLeadMax.
        // -walkPlantFallStanceDriveGrowYetFirst 0 restores the former
        // 0.789-load drive under the 140° shin. Catch / later cycles
        // keep DualLoad / pushAfterAir. Not leftover-cap, not
        // Clamp01, not another millimetre 0.25, not trailing A2, not
        // −swingHipFlex, not a DualLoad open, and not a PD retune.
        balance.walkPlantFallStanceDriveGrowYetFirst = GetFloatArg(
            "-walkPlantFallStanceDriveGrowYetFirst", balance.walkPlantFallStanceDriveGrowYetFirst);
        // Same Fall grow of the stance-hip drive load a fifth quarter.
        // DualLoadMinSpan never opens, so GrowYetFirst still multiplies
        // 0.842: 0.15×0.842 = 0.1263 of the 0.25 clamp — short of the
        // pelvis-forward the 140° pin needs. Opening DualLoad to feed
        // that unload would Lerp dualSupportStandCap 0.40→0.55 at
        // blend 0.25 (the recorded cap fold). Pinning 0.842→1.0 is a
        // 19% drive jump; leftover is not millimetres yet. Same quarter
        // on the load term only (Lerp 0.842→1 at 0.25 = 0.881, drive
        // 0.1322). Plant clock 1 already writes it so EnterFall is not
        // a step. firstRoom still fades at plantComLeadMax.
        // -walkPlantFallStanceDriveGrowFarFirst 0 restores the former
        // 0.842-load drive under the 140° shin. Catch / later cycles
        // keep DualLoad / pushAfterAir. Not leftover-cap, not
        // Clamp01, not another millimetre 0.25, not trailing A2, not
        // −swingHipFlex, not a DualLoad open, and not a PD retune.
        balance.walkPlantFallStanceDriveGrowFarFirst = GetFloatArg(
            "-walkPlantFallStanceDriveGrowFarFirst", balance.walkPlantFallStanceDriveGrowFarFirst);
        // Same Fall grow of the stance-hip drive load a sixth quarter.
        // DualLoadMinSpan never opens, so GrowFarFirst still multiplies
        // 0.881: 0.15×0.881 = 0.1322 of the 0.25 clamp — short of the
        // pelvis-forward the 140° pin needs. Opening DualLoad to feed
        // that unload would Lerp dualSupportStandCap 0.40→0.55 at
        // blend 0.25 (the recorded cap fold). Pinning 0.881→1.0 is a
        // 13% drive jump; leftover is not millimetres yet. Same quarter
        // on the load term only (Lerp 0.881→1 at 0.25 = 0.911, drive
        // 0.1367). Plant clock 1 already writes it so EnterFall is not
        // a step. firstRoom still fades at plantComLeadMax.
        // -walkPlantFallStanceDriveGrowFartherFirst 0 restores the former
        // 0.881-load drive under the 140° shin. Catch / later cycles
        // keep DualLoad / pushAfterAir. Not leftover-cap, not
        // Clamp01, not another millimetre 0.25, not trailing A2, not
        // −swingHipFlex, not a DualLoad open, and not a PD retune.
        balance.walkPlantFallStanceDriveGrowFartherFirst = GetFloatArg(
            "-walkPlantFallStanceDriveGrowFartherFirst", balance.walkPlantFallStanceDriveGrowFartherFirst);
        // Same Fall grow of the stance-hip drive load a seventh quarter.
        // DualLoadMinSpan never opens, so GrowFartherFirst still multiplies
        // 0.911: 0.15×0.911 = 0.1367 of the 0.25 clamp — short of the
        // pelvis-forward the 140° pin needs. Opening DualLoad to feed
        // that unload would Lerp dualSupportStandCap 0.40→0.55 at
        // blend 0.25 (the recorded cap fold). Pinning 0.911→1.0 is a
        // 10% drive jump; leftover is not millimetres yet. Same quarter
        // on the load term only (Lerp 0.911→1 at 0.25 = 0.933, drive
        // 0.1400). Plant clock 1 already writes it so EnterFall is not
        // a step. firstRoom still fades at plantComLeadMax.
        // -walkPlantFallStanceDriveGrowFarthestFirst 0 restores the former
        // 0.911-load drive under the 140° shin. Catch / later cycles
        // keep DualLoad / pushAfterAir. Not leftover-cap, not
        // Clamp01, not another millimetre 0.25, not trailing A2, not
        // −swingHipFlex, not a DualLoad open, and not a PD retune.
        balance.walkPlantFallStanceDriveGrowFarthestFirst = GetFloatArg(
            "-walkPlantFallStanceDriveGrowFarthestFirst", balance.walkPlantFallStanceDriveGrowFarthestFirst);
        // Same Fall grow of the stance-hip drive load an eighth quarter.
        // DualLoadMinSpan never opens, so GrowFarthestFirst still multiplies
        // 0.933: 0.15×0.933 = 0.1400 of the 0.25 clamp — short of the
        // pelvis-forward the 140° pin needs. Opening DualLoad to feed
        // that unload would Lerp dualSupportStandCap 0.40→0.55 at
        // blend 0.25 (the recorded cap fold). Pinning 0.933→1.0 is a
        // 7% drive jump; leftover is not millimetres yet. Same quarter
        // on the load term only (Lerp 0.933→1 at 0.25 = 0.950, drive
        // 0.1425). Plant clock 1 already writes it so EnterFall is not
        // a step. firstRoom still fades at plantComLeadMax.
        // -walkPlantFallStanceDriveGrowOutFirst 0 restores the former
        // 0.933-load drive under the 140° shin. Catch / later cycles
        // keep DualLoad / pushAfterAir. Not leftover-cap, not
        // Clamp01, not another millimetre 0.25, not trailing A2, not
        // −swingHipFlex, not a DualLoad open, and not a PD retune.
        balance.walkPlantFallStanceDriveGrowOutFirst = GetFloatArg(
            "-walkPlantFallStanceDriveGrowOutFirst", balance.walkPlantFallStanceDriveGrowOutFirst);
        // Same Fall grow of the stance-hip drive load a ninth quarter.
        // DualLoadMinSpan never opens, so GrowOutFirst still multiplies
        // 0.950: 0.15×0.950 = 0.1425 of the 0.25 clamp — short of the
        // pelvis-forward the 140° pin needs. Opening DualLoad to feed
        // that unload would Lerp dualSupportStandCap 0.40→0.55 at
        // blend 0.25 (the recorded cap fold). Pinning 0.950→1.0 is a
        // 5% drive jump; leftover is not millimetres yet. Same quarter
        // on the load term only (Lerp 0.950→1 at 0.25 = 0.962, drive
        // 0.1444). Plant clock 1 already writes it so EnterFall is not
        // a step. firstRoom still fades at plantComLeadMax.
        // -walkPlantFallStanceDriveGrowOutMoreFirst 0 restores the former
        // 0.950-load drive under the 140° shin. Catch / later cycles
        // keep DualLoad / pushAfterAir. Not leftover-cap, not
        // Clamp01, not another millimetre 0.25, not trailing A2, not
        // −swingHipFlex, not a DualLoad open, and not a PD retune.
        balance.walkPlantFallStanceDriveGrowOutMoreFirst = GetFloatArg(
            "-walkPlantFallStanceDriveGrowOutMoreFirst", balance.walkPlantFallStanceDriveGrowOutMoreFirst);
        // Same Fall grow of the stance-hip drive load a tenth quarter.
        // DualLoadMinSpan never opens, so GrowOutMoreFirst still multiplies
        // 0.962: 0.15×0.962 = 0.1444 of the 0.25 clamp — short of the
        // pelvis-forward the 140° pin needs. Opening DualLoad to feed
        // that unload would Lerp dualSupportStandCap 0.40→0.55 at
        // blend 0.25 (the recorded cap fold). Pinning 0.962→1.0 is a
        // 4% drive jump; leftover is not millimetres yet. Same quarter
        // on the load term only (Lerp 0.962→1 at 0.25 = 0.972, drive
        // 0.1457). Plant clock 1 already writes it so EnterFall is not
        // a step. firstRoom still fades at plantComLeadMax.
        // -walkPlantFallStanceDriveGrowOutStillFirst 0 restores the former
        // 0.962-load drive under the 140° shin. Catch / later cycles
        // keep DualLoad / pushAfterAir. Not leftover-cap, not
        // Clamp01, not another millimetre 0.25, not trailing A2, not
        // −swingHipFlex, not a DualLoad open, and not a PD retune.
        balance.walkPlantFallStanceDriveGrowOutStillFirst = GetFloatArg(
            "-walkPlantFallStanceDriveGrowOutStillFirst", balance.walkPlantFallStanceDriveGrowOutStillFirst);
        // Keep the Plant swing fold/lift across Plant→Fall (PhaseAge reset).
        // -walkPlantFallFoldHold 0 restores the former Fall start at 0.
        balance.walkPlantFallFoldHold = GetFloatArg(
            "-walkPlantFallFoldHold", balance.walkPlantFallFoldHold);
        balance.walkScrapeAnkleDrive = GetFloatArg("-walkScrapeAnkleDrive", balance.walkScrapeAnkleDrive);
        balance.walkPushToeFade = GetFloatArg("-walkPushToeFade", balance.walkPushToeFade);
        // Fall CoM setpoint roll across the sole. 0 = former fixed fallComLead.
        balance.walkFallComRollToe = GetFloatArg("-walkFallComRoll", balance.walkFallComRollToe);
        balance.walkTargetSpeed = GetFloatArg("-walkTargetSpeed", balance.walkTargetSpeed);
        balance.walkSpeedPushGain = GetFloatArg("-walkSpeedPushGain", balance.walkSpeedPushGain);
        balance.walkSpeedPushMax = GetFloatArg("-walkSpeedPushMax", balance.walkSpeedPushMax);
        balance.walkPushCounterAllowance = GetFloatArg("-walkPushCounterAllowance", balance.walkPushCounterAllowance);
        balance.walkPropulsionGain = GetFloatArg("-walkPropulsionGain", balance.walkPropulsionGain);
        balance.walkPropulsionMax = GetFloatArg("-walkPropulsionMax", balance.walkPropulsionMax);
        balance.walkPropulsionStart = GetFloatArg("-walkPropulsionStart", balance.walkPropulsionStart);
        balance.walkPropulsionPeak = GetFloatArg("-walkPropulsionPeak", balance.walkPropulsionPeak);
        balance.walkPropulsionEnd = GetFloatArg("-walkPropulsionEnd", balance.walkPropulsionEnd);
        balance.walkPropulsionTiltGuardDeg = GetFloatArg("-walkPropulsionTiltGuard", balance.walkPropulsionTiltGuardDeg);
        balance.walkPropulsionComWindow = GetFloatArg("-walkPropulsionComWindow", balance.walkPropulsionComWindow);
        balance.walkPropulsionMinWeight = GetFloatArg("-walkPropulsionMinWeight", balance.walkPropulsionMinWeight);
        balance.walkPropulsionCounterAllowance = GetFloatArg("-walkPropulsionCounterAllowance", balance.walkPropulsionCounterAllowance);
        balance.walkSpeedPushComGateScale = GetFloatArg("-walkSpeedPushComGateScale", balance.walkSpeedPushComGateScale);
        balance.walkXCoMWeight = GetFloatArg("-walkXCoMWeight", balance.walkXCoMWeight);
        balance.walkXCoMHeight = GetFloatArg("-walkXCoMHeight", balance.walkXCoMHeight);
        balance.walkComVelFilterTau = GetFloatArg("-walkComVelFilterTau", balance.walkComVelFilterTau);
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
        balance.walkStepComMargin = GetFloatArg("-walkStepComMargin", balance.walkStepComMargin);
        // Capture-point step target. 0 = former walkStepLength floor.
        balance.walkStepCaptureTarget = GetFloatArg("-walkStepCapture", balance.walkStepCaptureTarget);
        balance.walkStepMinLength = GetFloatArg("-walkStepMin", balance.walkStepMinLength);
        // Landing point also meets catchFootLead. 0 = former blind command.
        balance.walkStepCatchLeadSync = GetFloatArg("-walkStepCatchSync", balance.walkStepCatchLeadSync);
        balance.walkStepCatchLeadPad = GetFloatArg("-walkStepCatchPad", balance.walkStepCatchLeadPad);
        balance.walkPlacementSyncStart = GetFloatArg("-walkPlacementSyncStart", balance.walkPlacementSyncStart);
        balance.walkTouchdownSyncClearance = GetFloatArg("-walkTouchdownSyncClearance", balance.walkTouchdownSyncClearance);
        // Touchdown velocity match. 0 = former forward-travelling plant.
        balance.walkSwingRetractGain = GetFloatArg("-walkSwingRetract", balance.walkSwingRetractGain);
        balance.walkSwingRetractMax = GetFloatArg("-walkSwingRetractMax", balance.walkSwingRetractMax);
        // Geometric swing placement. 0 = former constant swingHipFlex reach.
        balance.walkSwingPlaceGeometry = GetFloatArg("-walkSwingPlaceGeo", balance.walkSwingPlaceGeometry);
        // Solve owns the air hip target. 0 = former placement boost stacked on top.
        balance.walkSwingPlaceGeoOnly = GetFloatArg("-walkSwingPlaceGeoOnly", balance.walkSwingPlaceGeoOnly);
        // Solve aims the foot centre, not the ankle hinge (one heel offset
        // shorter). 0 = former hinge-referenced target.
        balance.walkSwingPlaceFootRef = GetFloatArg("-walkSwingPlaceFootRef", balance.walkSwingPlaceFootRef);
        // Geometric landing knee. 0 = former constant reachKneeLandDeg.
        balance.walkSwingLandKneeGeometry = GetFloatArg("-walkLandKneeGeo", balance.walkSwingLandKneeGeometry);
        balance.walkLandKneeMinDeg = GetFloatArg("-walkLandKneeMin", balance.walkLandKneeMinDeg);
        // Stacked dual-support: geometric swing-knee clearance + stance
        // knee 0. Not DualLoad, not ankle-zero, not a pelvis fight.
        // Clearance default 0.08: probe is 0.06, so 0.04 never un-grounds.
        // Also WALK-1 Stance (DrivePelvis off the planted swing).
        // -walkGeoSwingLift 0 restores the former fold-clock path.
        balance.walkGeoSwingLift = GetFloatArg("-walkGeoSwingLift", balance.walkGeoSwingLift);
        balance.walkGeoSwingLiftClearance = GetFloatArg(
            "-walkGeoSwingLiftClearance", balance.walkGeoSwingLiftClearance);
        // WALK-1: fade the swing hip/knee weld on that same clock.
        // 0 = former K=12 stopTorque, geo knee unused.
        balance.walkStanceGeoFriction = GetFloatArg(
            "-walkStanceGeoFriction", balance.walkStanceGeoFriction);
        // Cycle-0 stance-ankle push with the hip drive. 0 = former
        // pushWindow wait (stacked feet never plantarflex).
        balance.walkPlantStancePushFirst = GetFloatArg(
            "-walkPlantStancePushFirst", balance.walkPlantStancePushFirst);
        // Folded/light swing placement ahead of CoM. 0 = former grounded × 0.
        balance.walkStepPlaceOnFold = GetFloatArg(
            "-walkStepPlaceOnFold", balance.walkStepPlaceOnFold);
        // Swing-leg proportional band. <= errorReferenceDegrees (10) = former
        // shared band, i.e. a swing hip and knee saturated for the whole flight.
        balance.walkSwingErrorReferenceDegrees = GetFloatArg("-walkSwingErrorRef", balance.walkSwingErrorReferenceDegrees);
        // Swing-leg viscosity, seconds. 0 = former built weld on hip and knee.
        balance.walkSwingLegFrictionTau = GetFloatArg("-walkSwingFrictionTau", balance.walkSwingLegFrictionTau);
        // Swing-ankle viscosity, seconds. 0 = former built weld on the sole.
        // Independent of the line above on purpose: the two can be measured
        // apart (-walkSwingFrictionTau 0 -walkSwingAnkleFrictionTau 0.10).
        balance.walkSwingAnkleFrictionTau = GetFloatArg(
            "-walkSwingAnkleFrictionTau", balance.walkSwingAnkleFrictionTau);
        // Unload-share on that same envelope. 0 = former weld until air.
        balance.walkSwingFrictionLoadShare = GetFloatArg(
            "-walkSwingFrictionLoadShare", balance.walkSwingFrictionLoadShare);
        // Dirt snap to tau K at unload 0.5. 0 = former linear K lerp.
        balance.walkSwingFrictionClampSnap = GetFloatArg(
            "-walkSwingFrictionClampSnap", balance.walkSwingFrictionClampSnap);
        // Hold that snap through first millimetres of air. 0 = 23:22 pulse.
        balance.walkSwingFrictionSnapHold = GetFloatArg(
            "-walkSwingFrictionSnapHold", balance.walkSwingFrictionSnapHold);
        // SPD denominator of that same sole. 0 = former flexor-only denominator
        // (plantarflex integrated as if the calf were the 22 N·m dorsiflexor).
        balance.walkSwingAnkleSpdTorque = GetFloatArg(
            "-walkSwingAnkleSpdTorque", balance.walkSwingAnkleSpdTorque);
        balance.walkStanceKneeBend = GetFloatArg("-walkStanceKneeBend", balance.walkStanceKneeBend);
        // Schedule of that bend on the fall-catch gait. 0 = the former
        // schedule that GREW the bend as the swing foot cleared.
        balance.walkStanceKneeBendLiftFade = GetFloatArg(
            "-walkStanceBendFade", balance.walkStanceKneeBendLiftFade);
        // Fraction of kneeRecoveryFlex left on the fall-catch stance knee.
        // 1 = former behaviour (the quiet-stance reflex on the loaded strut).
        balance.walkStanceKneeRecoveryFlex = GetFloatArg(
            "-walkStanceKneeRecovery", balance.walkStanceKneeRecoveryFlex);
        balance.walkForwardPelvisLean = GetFloatArg("-walkForwardPelvisLean", balance.walkForwardPelvisLean);
        balance.walkForwardTorsoLean = GetFloatArg("-walkForwardTorsoLean", balance.walkForwardTorsoLean);
        balance.walkStanceExtend = GetFloatArg("-walkStanceExtend", balance.walkStanceExtend);
        balance.walkComLeadX = GetFloatArg("-walkComLead", balance.walkComLeadX);
        // First-transfer: geo-lift waits for this CoM lead on the named
        // stance sole. 0 = former idle-clock lift on stacked feet.
        balance.walkFirstLiftLead = GetFloatArg(
            "-walkFirstLiftLead", balance.walkFirstLiftLead);
        balance.walkFirstLeadSeconds = GetFloatArg(
            "-walkFirstLeadSeconds", balance.walkFirstLeadSeconds);
        balance.useWalkLift = Mathf.RoundToInt(GetFloatArg("-walkLift", balance.useWalkLift));
        balance.walkLiftUnloadFromDirt = GetFloatArg(
            "-walkLiftDirt", balance.walkLiftUnloadFromDirt);
        balance.walkLiftKneeFlex = GetFloatArg("-walkLiftKnee", balance.walkLiftKneeFlex);
        balance.walkLiftStepLength = GetFloatArg("-walkLiftStep", balance.walkLiftStepLength);
        balance.walkLiftStepMax = GetFloatArg("-walkLiftStepMax", balance.walkLiftStepMax);
        balance.walkLiftAnkleFwdDriveMax = GetFloatArg("-walkLiftAnkleFwd", balance.walkLiftAnkleFwdDriveMax);
        balance.walkAnkleDorsiHold = GetFloatArg("-walkAnkleDorsiHold", balance.walkAnkleDorsiHold);
        balance.walkAnkleDorsiFade = GetFloatArg("-walkAnkleDorsiFade", balance.walkAnkleDorsiFade);
        balance.walkLiftCaptureLookahead = GetFloatArg("-walkLiftCapture", balance.walkLiftCaptureLookahead);
        balance.walkLiftClearHeight = GetFloatArg("-walkLiftClear", balance.walkLiftClearHeight);
        balance.walkLiftUnloadSeconds = GetFloatArg("-walkLiftUnloadT", balance.walkLiftUnloadSeconds);
        balance.walkLiftClearSeconds = GetFloatArg("-walkLiftClearT", balance.walkLiftClearSeconds);
        balance.walkLiftAdvanceSeconds = GetFloatArg("-walkLiftAdvanceT", balance.walkLiftAdvanceSeconds);
        balance.walkLiftAdvanceHipRamp = GetFloatArg("-walkLiftAdvanceRamp", balance.walkLiftAdvanceHipRamp);
        balance.walkLiftHipRateDeg = GetFloatArg("-walkLiftHipRate", balance.walkLiftHipRateDeg);
        balance.walkLiftCoupleHipShare = GetFloatArg("-walkLiftCoupleHip", balance.walkLiftCoupleHipShare);
        balance.walkLiftCoupleAnkleShare = GetFloatArg("-walkLiftCoupleAnkle", balance.walkLiftCoupleAnkleShare);
        balance.walkLiftCoupleHipMax = GetFloatArg("-walkLiftCoupleHipMax", balance.walkLiftCoupleHipMax);
        balance.walkLiftReactionCap = GetFloatArg("-walkLiftReactionCap", balance.walkLiftReactionCap);
        balance.walkLiftArmGain = GetFloatArg("-walkLiftArmGain", balance.walkLiftArmGain);
        balance.walkLiftHoldTrunk = GetFloatArg("-walkLiftHoldTrunk", balance.walkLiftHoldTrunk);
        balance.walkLiftStancePdScale = GetFloatArg("-walkLiftPdScale", balance.walkLiftStancePdScale);
        balance.fallComLead = fallComLead;
        balance.walkSwingScissorLevel = GetFloatArg("-walkSwingScissor", balance.walkSwingScissorLevel);
        balance.walkSwingScissorFlex = GetFloatArg("-walkSwingScissorFlex", balance.walkSwingScissorFlex);
        // Swing-hip reaction couple. 0 = former behaviour (pelvis eats the reaction).
        balance.walkSwingLiftCouple = GetFloatArg("-walkSwingLiftCouple", balance.walkSwingLiftCouple);
        balance.walkSwingLiftCoupleMax = GetFloatArg("-walkSwingLiftCoupleMax", balance.walkSwingLiftCoupleMax);
        balance.walkSwingLiftCoupleCapScale = GetFloatArg("-walkSwingLiftCoupleCap", balance.walkSwingLiftCoupleCapScale);
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
        balance.walkArmLegTrack = GetFloatArg("-walkArmLegTrack", balance.walkArmLegTrack);
        balance.walkLatePushGain = GetFloatArg("-walkLatePushGain", balance.walkLatePushGain);
        balance.walkLatePushStart = GetFloatArg("-walkLatePushStart", balance.walkLatePushStart);
        balance.walkLatePushMax = GetFloatArg("-walkLatePushMax", balance.walkLatePushMax);
        balance.walkLatePushNeedsAir = GetFloatArg("-walkLatePushNeedsAir", balance.walkLatePushNeedsAir ? 1f : 0f) > 0.5f;
        balance.walkLatePushNeedsTouchdownWindow = GetFloatArg("-walkLatePushNeedsTouchdownWindow", balance.walkLatePushNeedsTouchdownWindow ? 1f : 0f) > 0.5f;
        balance.standLegRatePerSecond = GetFloatArg("-standLegRate", balance.standLegRatePerSecond);
        balance.standLegReleaseRatePerSecond = GetFloatArg("-standLegReleaseRate", balance.standLegReleaseRatePerSecond);

        // WALK-3 capture-point walker. Default on: it owns walk cadence.
        // -captureWalk 0 restores the closed WalkLift / Stance-clock path.
        // Only -walk 1 windows reach this walker at all.
        balance.useCaptureWalker = Mathf.RoundToInt(
            GetFloatArg("-captureWalk", balance.useCaptureWalker));
        balance.captureStepDuration = GetFloatArg("-captureStepT", balance.captureStepDuration);
        balance.captureSwingXShare = GetFloatArg("-captureSwingX", balance.captureSwingXShare);
        balance.captureTargetSpeed = GetFloatArg("-captureSpeed", balance.captureTargetSpeed);
        balance.captureRunSpeedScale = GetFloatArg("-captureRunScale", balance.captureRunSpeedScale);
        balance.captureSpeedGain = GetFloatArg("-captureKv", balance.captureSpeedGain);
        balance.captureLookahead = GetFloatArg("-captureLookahead", balance.captureLookahead);
        balance.captureStepMin = GetFloatArg("-captureStepMin", balance.captureStepMin);
        balance.captureStepMax = GetFloatArg("-captureStepMax", balance.captureStepMax);
        balance.captureSwingApex = GetFloatArg("-captureApex", balance.captureSwingApex);
        balance.captureReachMargin = GetFloatArg("-captureReach", balance.captureReachMargin);
        balance.captureStepBackMax = GetFloatArg("-captureStepBack", balance.captureStepBackMax);
        balance.captureAbandonPutDown = Mathf.RoundToInt(
            GetFloatArg("-capturePutDown", balance.captureAbandonPutDown));
        balance.captureFootLandLift = GetFloatArg("-captureLandLift", balance.captureFootLandLift);
        balance.captureFootPress = GetFloatArg("-captureFootPress", balance.captureFootPress);
        balance.captureFootPitchDeg = GetFloatArg("-captureFootPitch", balance.captureFootPitchDeg);
        balance.captureDoubleSupportMin = GetFloatArg("-captureDsMin", balance.captureDoubleSupportMin);
        balance.captureDoubleSupportMax = GetFloatArg("-captureDsMax", balance.captureDoubleSupportMax);
        balance.captureDoubleSupportShare = GetFloatArg("-captureDsShare", balance.captureDoubleSupportShare);
        balance.captureTouchdownFrames = Mathf.RoundToInt(
            GetFloatArg("-captureTdFrames", balance.captureTouchdownFrames));
        balance.captureTouchdownBlendSeconds = GetFloatArg("-captureTdBlend", balance.captureTouchdownBlendSeconds);
        balance.captureLandKneeFlex = GetFloatArg("-captureLandKnee", balance.captureLandKneeFlex);
        balance.captureStanceLegLength = GetFloatArg("-captureLegLength", balance.captureStanceLegLength);
        balance.captureLandLegLength = GetFloatArg("-captureLandLength", balance.captureLandLegLength);
        balance.captureEntryRampSeconds = GetFloatArg("-captureEntryRamp", balance.captureEntryRampSeconds);
        balance.captureLegPushCap = GetFloatArg("-captureLegPushCap", balance.captureLegPushCap);
        balance.captureSupportKneeExtendRate = GetFloatArg("-captureKneeRate", balance.captureSupportKneeExtendRate);
        // Loaded-hip ceiling, one number per ground arm: heel 0.065 m and toe
        // 0.195 m are not the same lever, so they are not the same cap.
        balance.captureStanceHipTorqueCap = GetFloatArg("-captureHipCap", balance.captureStanceHipTorqueCap);
        balance.captureStanceHipTorqueCapForward = GetFloatArg("-captureHipCapFwd", balance.captureStanceHipTorqueCapForward);
        balance.captureStancePlantPress = GetFloatArg("-captureStancePress", balance.captureStancePlantPress);
        balance.captureDoubleSupportHoldSeconds = GetFloatArg("-captureDsHold", balance.captureDoubleSupportHoldSeconds);
        balance.captureTrunkLeanDeg = GetFloatArg("-captureTrunkLean", balance.captureTrunkLeanDeg);
        balance.capturePelvisLeanDeg = GetFloatArg("-capturePelvisLean", balance.capturePelvisLeanDeg);
        balance.captureLeanPlantFraction = GetFloatArg("-captureLeanPlant", balance.captureLeanPlantFraction);
        balance.captureCopLead = GetFloatArg("-captureCopLead", balance.captureCopLead);
        balance.captureCopScale = GetFloatArg("-captureCopScale", balance.captureCopScale);
        balance.captureCopTravelBias = GetFloatArg("-captureCopTravel", balance.captureCopTravelBias);
        balance.captureLaunchSeconds = GetFloatArg("-captureLaunchT", balance.captureLaunchSeconds);
        balance.captureLaunchAnkleCap = GetFloatArg("-captureLaunchCap", balance.captureLaunchAnkleCap);
        balance.captureLaunchSpeed = GetFloatArg("-captureLaunchSpeed", balance.captureLaunchSpeed);
        balance.captureLaunchLead = GetFloatArg("-captureLaunchLead", balance.captureLaunchLead);
        balance.captureCatchPast = GetFloatArg("-captureCatchPast", balance.captureCatchPast);
        balance.toePGain = GetFloatArg("-toeP", balance.toePGain);
        balance.toeDGain = GetFloatArg("-toeD", balance.toeDGain);
        balance.toeClearanceDeg = GetFloatArg("-toeClear", balance.toeClearanceDeg);
        balance.captureToePushDeg = GetFloatArg("-captureToePush", balance.captureToePushDeg);
        balance.captureToeLeanGain = GetFloatArg("-captureToeLean", balance.captureToeLeanGain);
        balance.captureRecoveryMargin = GetFloatArg("-captureRecoverMargin", balance.captureRecoveryMargin);
        balance.captureRecoveryDuration = GetFloatArg("-captureRecoverT", balance.captureRecoveryDuration);
        balance.captureSwingOvertime = GetFloatArg("-captureOvertime", balance.captureSwingOvertime);
        balance.captureCollapseDrop = GetFloatArg("-captureCollapseDrop", balance.captureCollapseDrop);

        // Speed at which muscle activation catches the controller command.
        // 500 — almost instant; 10 — the muscle rises in ~0.1 s.
        balance.muscleActivationSpeed = GetFloatArg("-activationSpeed", balance.muscleActivationSpeed);
    }

    // Picture only: same CameraFollow lens as Play Mode. Do not set
    // orthographicSize or a Y shift here — LateUpdate owns the frame.
    // WaitForEndOfFrame lets that LateUpdate run before ReadPixels.
    private IEnumerator CaptureVisualFrame(string path)
    {
        if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
        {
            Debug.LogWarning("HeadlessTrial: -visual skipped under -nographics.");
            yield break;
        }

        Camera cam = EnsureVisualFollowCamera();
        if (cam == null)
            yield break;

        yield return new WaitForEndOfFrame();

        const int w = 1920;
        const int h = 1080;
        RenderTexture rt = RenderTexture.GetTemporary(w, h, 24);
        RenderTexture prevTarget = cam.targetTexture;
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture prevActive = RenderTexture.active;
        RenderTexture.active = rt;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0, false);
        tex.Apply(false, false);
        RenderTexture.active = prevActive;
        cam.targetTexture = prevTarget;
        RenderTexture.ReleaseTemporary(rt);
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Destroy(tex);
        WriteFrameReadyMarker(path, w, h);
    }

    // Bind the trial human to CameraFollow. Size and Y come from the
    // component (frameFullBody), same as GameProcess SampleScene stand.
    private Camera EnsureVisualFollowCamera()
    {
        Camera cam = Camera.main;
        if (cam == null)
            cam = FindFirstObjectByType<Camera>();
        if (cam == null)
        {
            GameObject go = new GameObject("VisCam");
            cam = go.AddComponent<Camera>();
            go.tag = "MainCamera";
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = SceneLighting.NightSky;
        }

        cam.orthographic = true;
        CameraFollow follow = cam.GetComponent<CameraFollow>();
        if (follow == null)
            follow = cam.gameObject.AddComponent<CameraFollow>();
        if (appleTree != null)
        {
            // Apple trial: fixed lens on the tree. Zoomed tighter than the
            // Play `Tree` scene so the crown fills the 1080p frame.
            follow.frameFullBody = false;
            float height = appleTree.HeightMeters;
            cam.orthographicSize = height * 0.62f + 0.5f;
            cam.transform.position = new Vector3(
                appleTree.transform.position.x,
                appleTree.transform.position.y + height * 0.5f,
                -10f);
        }
        else if (human == null && plantTree != null)
        {
            // Tree trial: the same lens as Play scene `Tree` (GameProcess
            // treeOnly: size 4.6, offset 1.1). frameFullBody off — the stand
            // lens reads human head/feet sprites that are null here.
            follow.frameFullBody = false;
            follow.offset = new Vector3(0f, 1.1f, -10f);
            cam.orthographicSize = 4.6f;
            follow.Bind(plantTree);
        }
        else
        {
            follow.frameFullBody = true;
            if (human != null)
                follow.Bind(human);
        }
        return cam;
    }

    private static void WriteFrameReadyMarker(string pngPath, int width, int height)
    {
        string folder = Path.GetDirectoryName(pngPath);
        if (string.IsNullOrEmpty(folder))
            folder = ".";
        string readyPath = Path.Combine(folder, "frame.ready.json");
        string json =
            "{\n" +
            "  \"png\": " + JsonQuote(pngPath) + ",\n" +
            "  \"width\": " + width.ToString(CultureInfo.InvariantCulture) + ",\n" +
            "  \"height\": " + height.ToString(CultureInfo.InvariantCulture) + ",\n" +
            "  \"writtenUtc\": " + JsonQuote(DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)) + "\n" +
            "}\n";
        File.WriteAllText(readyPath, json);
    }

    private static string JsonQuote(string value)
    {
        if (value == null)
            value = "";
        return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    }

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

        if (spawnApple)
        {
            yield return RunAppleTrial();
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
        int lastShot = -1;

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
            if (marchTarget > 0.5f)
                UpdateMarchIntent(simulated);
            else if (walkTarget > 0.5f || runTarget > 0.5f)
                UpdateWalkIntent(simulated);
            else
                UpdateStandLegIntent(simulated, ref standLegMarked, ref standLegReleased);

            recorder.Sample(simulated);

            if (visual)
            {
                int shotIdx = (int)(simulated / 0.2f);
                if (shotIdx != lastShot)
                {
                    lastShot = shotIdx;
                    string shotPath = Path.Combine(outputFolder, "shot_" + label + "_" + shotIdx.ToString("00") + ".png");
                    yield return CaptureVisualFrame(shotPath);
                }
            }

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
        int lastShot = -1;

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

            // Same frame capture as the human and bird trials: the dog had none, so
            // there were no dog videos to look at. Yury asked for dog video reports.
            if (visual)
            {
                int shotIdx = (int)(simulated / 0.2f);
                if (shotIdx != lastShot)
                {
                    lastShot = shotIdx;
                    string shotPath = Path.Combine(outputFolder, "shot_" + label + "_" + shotIdx.ToString("00") + ".png");
                    yield return CaptureVisualFrame(shotPath);
                }
            }

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
        birdRecorder = new BirdTrialRecorder(bird, runInfo, trialFlockDrive);
        if (plantTree != null)
            plantRecorder = new PlantTrialRecorder(plantTree, runInfo);

        float trialWallStart = Time.realtimeSinceStartup;
        float simulated = 0f;
        float realDeadline = Time.realtimeSinceStartup + REAL_TIME_LIMIT_SECONDS;
        bool pushed = false;
        bool flapStopped = false;
        int lastShot = -1;

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

            if (visual)
            {
                int shotIdx = (int)(simulated / 0.2f);
                if (shotIdx != lastShot)
                {
                    lastShot = shotIdx;
                    string shotPath = Path.Combine(outputFolder, "shot_" + label + "_" + shotIdx.ToString("00") + ".png");
                    yield return CaptureVisualFrame(shotPath);
                }
            }

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
        stepDriver.walkComTriggerMinStanceFraction = GetFloatArg("-walkComTriggerMinStanceFraction", stepDriver.walkComTriggerMinStanceFraction);
        stepDriver.walkTiltAbortDeg = GetFloatArg("-walkTiltAbort", stepDriver.walkTiltAbortDeg);
        stepDriver.walkTiltRecoverDeg = GetFloatArg("-walkTiltRecover", stepDriver.walkTiltRecoverDeg);
        stepDriver.walkCadenceStanceGain = walkCadenceStanceGain;
        stepDriver.walkCadenceMinStance = walkCadenceMinStance;
        stepDriver.transferMaxDuration = walkTransferMaxDuration;
        stepDriver.transferMinDuration = walkTransferMinDuration;
        stepDriver.transferTouchdownMaxAge = walkTransferTouchdownMaxAge;
        stepDriver.walkCadenceTransferGain = walkCadenceTransferGain;
        stepDriver.walkCadenceMinTransfer = walkCadenceMinTransfer;
        stepDriver.transferComOffsetMax = walkTransferComMax;
        stepDriver.transferFallbackComMax = walkTransferFallbackComMax;
        stepDriver.transferStandLegLevelMax = walkTransferLevelMax;
        stepDriver.firstStance = walkFirstStance;
        ApplyFallCatchWalkFields(stepDriver);
        stepDriver.Tick(time);
    }

    // Clocked one-leg: left up, plant, right up, plant. Not walkActive,
    // not WalkLift, not a forward wish. Plant must outlast
    // standLegReleaseRatePerSecond (4/s → 0.25 s) so heldStandCmd can
    // decay before the opposite sign, same as the one-leg release fix.
    private void UpdateMarchIntent(float time)
    {
        if (intent == null) return;

        bool window = marchTarget > 0.5f
            && time >= marchTime && time < marchTime + marchDuration;
        intent.march = window ? 1f : 0f;
        intent.moveX = 0f;
        if (!window)
        {
            intent.standLeg = 0f;
            return;
        }

        float hold = Mathf.Max(0.1f, marchHold);
        float plant = Mathf.Max(0.25f, marchPlant);
        float half = hold + plant;
        float cycle = 2f * half;
        float pos = (time - marchTime) % cycle;
        float sign0 = marchFirst < 0f ? -1f : 1f;
        if (pos < hold)
            intent.standLeg = sign0;
        else if (pos < hold + plant)
            intent.standLeg = 0f;
        else if (pos < hold + plant + hold)
            intent.standLeg = -sign0;
        else
            intent.standLeg = 0f;
    }

    private void ApplyFallCatchWalkFields(StepPhaseDriver driver)
    {
        if (driver == null) return;
        // Play never runs this: it uses StepPhaseDriver field defaults.
        // Keep CLI fallbacks in Start() equal to those fields, except
        // useFallCatchWalk (0 here, 1 on the driver for Play/WebGL).
        driver.useFallCatchWalk = useFallCatchWalk;
        driver.fallComLeadTrigger = fallComLeadTrigger;
        driver.fallPelvisLean = fallPelvisLean;
        driver.fallForwardLeanScale = fallForwardLeanScale;
        driver.catchKneeFlex = catchKneeFlex;
        driver.catchLeadCapture = catchLeadCapture;
        driver.catchFootLeadMin = catchFootLeadMin;
        driver.catchLeadCaptureMargin = catchLeadCaptureMargin;
        driver.reachKneeFlex = reachKneeFlex;
        driver.fallStanceVault = fallStanceVault;
        driver.fallSwingThighHold = fallSwingThighHold;
        driver.fallSwingHipLift = fallSwingHipLift;
        driver.fallSwingAnkleLift = fallSwingAnkleLift;
        driver.fallSwingAirAnkle = fallSwingAirAnkle;
        driver.fallSwingAirHeel = fallSwingAirHeel;
        driver.fallSwingHeelPlant = fallSwingHeelPlant;
        driver.fallSwingScrapeExtend = fallSwingScrapeExtend;
        driver.fallSwingCatchLandKnee = fallSwingCatchLandKnee;
        driver.fallSwingPlantStanceKnee = fallSwingPlantStanceKnee;
        driver.fallSwingPlantMidStanceExtend = fallSwingPlantMidStanceExtend;
        driver.fallSwingVaultRamp = fallSwingVaultRamp;
        driver.fallSwingCatchVaultYield = fallSwingCatchVaultYield;
        driver.fallSwingArmClock = fallSwingArmClock;
        driver.fallSwingReachLiftClock = fallSwingReachLiftClock;
        driver.fallSwingReachArmClock = fallSwingReachArmClock;
        driver.fallSwingCatchArmClock = fallSwingCatchArmClock;
        driver.fallSwingCatchExtendClock = fallSwingCatchExtendClock;
        driver.fallSwingCatchReachResume = fallSwingCatchReachResume;
        driver.fallSwingCatchReachScrapeResume = fallSwingCatchReachScrapeResume;
        driver.fallSwingCatchReachScrapeLiftHold = fallSwingCatchReachScrapeLiftHold;
        driver.fallSwingCatchReachScrapeLiftAirAge = fallSwingCatchReachScrapeLiftAirAge;
        driver.fallSwingCatchReachScrapeVaultHold = fallSwingCatchReachScrapeVaultHold;
        driver.fallSwingCatchReachScrapeVaultAirAge = fallSwingCatchReachScrapeVaultAirAge;
        driver.fallSwingCatchReachScrapeArmHold = fallSwingCatchReachScrapeArmHold;
        driver.fallSwingCatchReachScrapeArmAirAge = fallSwingCatchReachScrapeArmAirAge;
        driver.fallSwingCatchReachScrapeBrakeHold = fallSwingCatchReachScrapeBrakeHold;
        driver.fallSwingCatchReachScrapeBrakeAirAge = fallSwingCatchReachScrapeBrakeAirAge;
        driver.fallSwingCatchReachScrapeYieldHold = fallSwingCatchReachScrapeYieldHold;
        driver.fallSwingCatchReachScrapeYieldAirAge = fallSwingCatchReachScrapeYieldAirAge;
        driver.fallSwingCatchReachScrapeExtendHold = fallSwingCatchReachScrapeExtendHold;
        driver.fallSwingCatchReachScrapeExtendAirAge = fallSwingCatchReachScrapeExtendAirAge;
        driver.fallSwingCatchReachVaultClock = fallSwingCatchReachVaultClock;
        driver.fallSwingCatchReachVaultAge = fallSwingCatchReachVaultAge;
        driver.fallSwingCatchReachArmClock = fallSwingCatchReachArmClock;
        driver.fallSwingCatchReachLiftClock = fallSwingCatchReachLiftClock;
        driver.fallSwingCatchReachLiftAge = fallSwingCatchReachLiftAge;
        driver.fallSwingCatchReachArmAge = fallSwingCatchReachArmAge;
        driver.fallSwingCatchAirVaultHold = fallSwingCatchAirVaultHold;
        driver.fallSwingCatchAirVaultRemain = fallSwingCatchAirVaultRemain;
        driver.fallSwingCatchAirArmHold = fallSwingCatchAirArmHold;
        driver.fallSwingCatchAirExtendHold = fallSwingCatchAirExtendHold;
        driver.fallSwingCatchScrapeExtendHold = fallSwingCatchScrapeExtendHold;
        driver.fallSwingCatchScrapeExtendAirAge = fallSwingCatchScrapeExtendAirAge;
        driver.fallSwingCatchScrapeExtendAirEnterHold = fallSwingCatchScrapeExtendAirEnterHold;
        driver.fallSwingCatchReachScrapeExtendEnterHold = fallSwingCatchReachScrapeExtendEnterHold;
        driver.fallSwingCatchAirLiftHold = fallSwingCatchAirLiftHold;
        driver.fallSwingCatchScrapeLiftAirHold = fallSwingCatchScrapeLiftAirHold;
        driver.fallSwingCatchScrapeLiftAirEnterHold = fallSwingCatchScrapeLiftAirEnterHold;
        driver.fallSwingCatchScrapeLiftAirCatchHold = fallSwingCatchScrapeLiftAirCatchHold;
        driver.fallSwingCatchScrapeLiftAirCatchFold = fallSwingCatchScrapeLiftAirCatchFold;
        driver.fallSwingCatchScrapeLiftAirEnterFold = fallSwingCatchScrapeLiftAirEnterFold;
        driver.fallSwingCatchScrapeLiftAirEnterArm = fallSwingCatchScrapeLiftAirEnterArm;
        driver.fallSwingCatchScrapeLiftAirCatchArm = fallSwingCatchScrapeLiftAirCatchArm;
        driver.fallSwingCatchScrapeLiftAirCatchEndArm = fallSwingCatchScrapeLiftAirCatchEndArm;
        driver.fallSwingCatchScrapeLiftAirCatchGroundArm = fallSwingCatchScrapeLiftAirCatchGroundArm;
        driver.fallSwingCatchScrapeLiftAirCatchAirArm = fallSwingCatchScrapeLiftAirCatchAirArm;
        driver.fallSwingCatchScrapeLiftAirCatchAirEndArm = fallSwingCatchScrapeLiftAirCatchAirEndArm;
        driver.fallSwingCatchScrapeLiftAirCatchAirGroundArm = fallSwingCatchScrapeLiftAirCatchAirGroundArm;
        driver.fallSwingCatchScrapeLiftAirEnterGroundArm = fallSwingCatchScrapeLiftAirEnterGroundArm;
        driver.fallSwingCatchScrapeLiftAirEnterGroundFold = fallSwingCatchScrapeLiftAirEnterGroundFold;
        driver.fallSwingCatchScrapeLiftAirCatchGroundFold = fallSwingCatchScrapeLiftAirCatchGroundFold;
        driver.fallSwingCatchScrapeLiftAirCatchAirGroundFold = fallSwingCatchScrapeLiftAirCatchAirGroundFold;
        driver.fallSwingCatchScrapeLiftAirCatchAirFold = fallSwingCatchScrapeLiftAirCatchAirFold;
        driver.fallSwingCatchScrapeLiftAirCatchAirEnterFold = fallSwingCatchScrapeLiftAirCatchAirEnterFold;
        driver.fallSwingCatchScrapeLiftAirCatchAirEnterArm = fallSwingCatchScrapeLiftAirCatchAirEnterArm;
        driver.fallSwingCatchScrapeLiftAirCatchAirEnterGroundArm = fallSwingCatchScrapeLiftAirCatchAirEnterGroundArm;
        driver.fallSwingCatchScrapeLiftAirCatchAirEnterGroundFold = fallSwingCatchScrapeLiftAirCatchAirEnterGroundFold;
        driver.fallSwingCatchScrapeLiftAirCatchAirEnterEndArm = fallSwingCatchScrapeLiftAirCatchAirEnterEndArm;
        driver.fallSwingCatchScrapeLiftAirCatchAirEnterCatchArm = fallSwingCatchScrapeLiftAirCatchAirEnterCatchArm;
        driver.fallSwingCatchScrapeLiftAirCatchAirEnterCatchFold = fallSwingCatchScrapeLiftAirCatchAirEnterCatchFold;
        driver.fallSwingCatchScrapeLiftAirCatchAirEnterEndFold = fallSwingCatchScrapeLiftAirCatchAirEnterEndFold;
        driver.fallSwingCatchScrapeLiftAirCatchEndFold = fallSwingCatchScrapeLiftAirCatchEndFold;
        driver.fallSwingCatchScrapeLiftAirCatchGroundCatchFold = fallSwingCatchScrapeLiftAirCatchGroundCatchFold;
        driver.fallSwingCatchScrapeLiftAirCatchGroundCatchEndFold = fallSwingCatchScrapeLiftAirCatchGroundCatchEndFold;
        driver.fallSwingCatchScrapeLiftAirCatchAirEndFold = fallSwingCatchScrapeLiftAirCatchAirEndFold;
        driver.fallSwingCatchScrapeLiftAirCatchAirGroundCatchFold = fallSwingCatchScrapeLiftAirCatchAirGroundCatchFold;
        driver.fallSwingCatchScrapeLiftAirCatchPlantExitFold = fallSwingCatchScrapeLiftAirCatchPlantExitFold;
        driver.fallSwingPlantStanceExtendClock = fallSwingPlantStanceExtendClock;
        driver.fallSwingVaultFromLeftover = fallSwingVaultFromLeftover;
        driver.fallSwingFallAnklePush = fallSwingFallAnklePush;
        driver.fallSwingFallHipLiftCap = fallSwingFallHipLiftCap;
        driver.fallSwingFallAnkleLift = fallSwingFallAnkleLift;
        driver.fallSwingFallHipLift = fallSwingFallHipLift;
        driver.fallSwingReachAirHold = fallSwingReachAirHold;
        driver.fallSwingFallAirHold = fallSwingFallAirHold;
        driver.fallSwingFallHipLiftCapWide = fallSwingFallHipLiftCapWide;
        driver.fallSwingReachHipLiftCapWide = fallSwingReachHipLiftCapWide;
        driver.fallSwingCatchAirHipLiftCapWide = fallSwingCatchAirHipLiftCapWide;
        driver.fallSwingPlantHipLiftCapWide = fallSwingPlantHipLiftCapWide;
        driver.fallSwingFallHipLiftCapCeil = fallSwingFallHipLiftCapCeil;
        driver.fallSwingReachHipLiftCapCeil = fallSwingReachHipLiftCapCeil;
        driver.fallSwingCatchAirHipLiftCapCeil = fallSwingCatchAirHipLiftCapCeil;
        driver.fallSwingPlantHipLiftCapCeil = fallSwingPlantHipLiftCapCeil;
        driver.fallSwingCatchScrapeHipLiftCapCeil = fallSwingCatchScrapeHipLiftCapCeil;
        driver.fallSwingCatchReachScrapeHipLiftCapCeil = fallSwingCatchReachScrapeHipLiftCapCeil;
        driver.fallSwingCatchAirHold = fallSwingCatchAirHold;
        driver.fallSwingCatchAirEnterHold = fallSwingCatchAirEnterHold;
        driver.fallSwingPlantAirHold = fallSwingPlantAirHold;
        driver.fallSwingReachAirSwingCeil = fallSwingReachAirSwingCeil;
        driver.fallSwingHipairCeil = fallSwingHipairCeil;
        driver.fallSwingRecoverHipairClear = fallSwingRecoverHipairClear;
        driver.fallSwingRecoverAirAnkleClear = fallSwingRecoverAirAnkleClear;
        driver.fallSwingPlantAirAnkleClear = fallSwingPlantAirAnkleClear;
        driver.fallSwingPlantAirAnkleLevel = fallSwingPlantAirAnkleLevel;
        driver.fallSwingRecoverAirKneeClear = fallSwingRecoverAirKneeClear;
        driver.fallSwingReachHipLiftCap = fallSwingReachHipLiftCap;
        driver.fallSwingReachHipLift = fallSwingReachHipLift;
        driver.fallSwingReachAnkleLift = fallSwingReachAnkleLift;
        driver.fallSwingReachHipLiftCapHold = fallSwingReachHipLiftCapHold;
        driver.fallSwingFallHipLiftCapHold = fallSwingFallHipLiftCapHold;
        driver.fallSwingFallVaultLead = fallSwingFallVaultLead;
        driver.fallSwingFallLiftLead = fallSwingFallLiftLead;
        driver.fallSwingFallFoldLead = fallSwingFallFoldLead;
        driver.fallSwingCatchAirHipLiftCap = fallSwingCatchAirHipLiftCap;
        driver.fallSwingCatchAirHipLiftCapHold = fallSwingCatchAirHipLiftCapHold;
        driver.fallSwingCatchAirHipLift = fallSwingCatchAirHipLift;
        driver.fallSwingCatchAirAnkleLift = fallSwingCatchAirAnkleLift;
        driver.fallSwingCatchScrapeHipLiftCap = fallSwingCatchScrapeHipLiftCap;
        driver.fallSwingCatchScrapeHipLiftCapHold = fallSwingCatchScrapeHipLiftCapHold;
        driver.fallSwingCatchScrapeHipLift = fallSwingCatchScrapeHipLift;
        driver.fallSwingCatchScrapeAnkleLift = fallSwingCatchScrapeAnkleLift;
        driver.fallSwingCatchScrapeLiftLead = fallSwingCatchScrapeLiftLead;
        driver.fallSwingCatchScrapeHipLiftCapWide = fallSwingCatchScrapeHipLiftCapWide;
        driver.fallSwingCatchReachScrapeHipLiftCap = fallSwingCatchReachScrapeHipLiftCap;
        driver.fallSwingCatchReachScrapeHipLiftCapHold = fallSwingCatchReachScrapeHipLiftCapHold;
        driver.fallSwingCatchReachScrapeHipLift = fallSwingCatchReachScrapeHipLift;
        driver.fallSwingCatchReachScrapeAnkleLift = fallSwingCatchReachScrapeAnkleLift;
        driver.fallSwingCatchReachScrapeHipLiftCapWide = fallSwingCatchReachScrapeHipLiftCapWide;
        driver.plantMinDuration = plantMinDuration;
        driver.plantLeadWaitSeconds = plantLeadWaitSeconds;
        driver.reachTimeout = reachTimeout;
        driver.fallAbortDeg = fallAbortDeg;
        driver.plantStallReseatSeconds = plantStallReseatSeconds;
        driver.plantReseatLeadMargin = plantReseatLeadMargin;
        driver.plantStallStandSeconds = plantStallStandSeconds;
        driver.plantClearRestoreOnGate = plantClearRestoreOnGate;
        driver.reachGroundStallSeconds = reachGroundStallSeconds;
        driver.fallCatchBounceMax = fallCatchBounceMax;
        driver.sinkAbortDrop = sinkAbortDrop;
        driver.plantAirLowestFoot = plantAirLowestFoot;
        driver.walkRecoverAnyPose = walkRecoverAnyPose;
        // Recover→Plant reads recoverComOffsetMax, not transferComOffsetMax.
        // The latter is the legacy mid-support Transfer band and is assigned
        // above; it never entered this method, so a -walkTransferCom sweep
        // changed IsTransferReady only (unused on the fall-catch Recover
        // exit). Same CLI, this field, catch-foot metric.
        driver.recoverComOffsetMax = GetFloatArg("-walkTransferCom", driver.recoverComOffsetMax);
        // Caught band shared by the Plant roll latch and the CoP arrest.
        // Recover swap does not wait for it. -plantCaughtLead 0 restores
        // the former "CoM behind the ankle" latch, bit for bit.
        driver.plantCaughtLeadMax = GetFloatArg(
            "-plantCaughtLead", driver.plantCaughtLeadMax);
        // Ceiling on the lead-arrest half of that brake. -plantArrestCop 0
        // removes the arrest (trunk term only), 1 is the saturating floor
        // that bounced the trunk to +22° backward.
        driver.plantArrestCopScale = GetFloatArg(
            "-plantArrestCop", driver.plantArrestCopScale);
        // Runaway escape: Plant leaves for Reach and the aborts stop
        // preferring a two-foot stand while the CoM is past the support.
        // -walkKeepStepping 0 restores the former ladder (brake, re-seat,
        // surrender) bit for bit.
        driver.walkRunawayKeepStepping = GetFloatArg(
            "-walkKeepStepping", driver.walkRunawayKeepStepping);
        driver.plantRunawayStepSeconds = GetFloatArg(
            "-plantRunawayStep", driver.plantRunawayStepSeconds);
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
            else if (spawnApple)
            {
                File.WriteAllText(csvPath, "t\n");
                summary = appleRecorder.BuildSummaryJson(simulated, wallSeconds);
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
            RequestedMarch = marchTarget,
            RequestedMarchTime = marchTime,
            RequestedMarchDuration = marchDuration,
            RequestedMarchHold = marchHold,
            RequestedMarchPlant = marchPlant,
            RequestedMarchFirst = marchFirst,
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
