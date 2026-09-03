using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

// Прогон одного человека без окна редактора, для численного анализа баланса.
// Активируется аргументом -trial в командной строке собранного плеера:
//   BalanceTrial.exe -batchmode -nographics -trial -duration 10 -label base -logFile -
//   -fixedDelta 0.005 — шаг физики (0.02 = 50 Гц, 0.005 = 200 Гц)
//   -activationSpeed 10 — скорость набора силы мышцы
//   -pushImpulse 20 -pushTime 5 — толчок в торс (Н·с, + вперёд) на 5-й секунде
//   -crouch 1 -crouchTime 5 -crouchHold 10 — цель приседа через MotionIntent
//   -lean 1|-1 -leanTime 5 -leanHold 10 — наклон корпуса (+ вперёд, − назад)
//   -standLeg left|right|0 -standLegTime 5 -standLegHold 10 — одноногая стойка
//   -dog 1 — прогон собаки вместо человека (humanCount = 0, плоская земля)
//   -dogLeap 1 -dogLeapTime 2 -dogPreyX 1.4 — leap attack after settle, prey mark
//   -tree 1 — дерево (можно вместе с -bird: птицы на ветках)
//   -treeKind oak|pine|willow|bush|poplar — рецепт кроны (по умолчанию oak)
//   -treeSeed 1 — локальный Random вилки
//   -treePerch 1 — посадить птиц на PlantTree.GetPerchSlots
//   -treeWind 0 — без паруса (проверка, что ветки держат позу)
//   -bird 1 — прогон птицы вместо человека (флаги человека не читаются)
//   -birdMode stand|fly|walk|glide|sit|attack -takeoffDelay 0 — стойка без взлёта
//   -birdRig ragdoll|flock — ragdoll для стойки, flock для стаи (одно тело)
//   -birdFacing 1|-1 — нос в +X или −X (flock: картинка на Look)
    //   -birdKind crow|chicken — рецепт тела (курица: земля, не крейсер)
    //   -birdDrive wander|prey — мозг пишет mode (стая: wander + -birdCount)
    //   -birdThreat 8 — метка угрозы впереди, м (0 — нет; крик + Fly, не Attack)
    //   -threatRange 12 — дальность «видит угрозу», м
//   -glideLead / -sitMin / -sitMax / -flyMin / -flyMax / -flockLeash / -aiStride
//     / -homeFaceDeadzone — wander timing, flock brain stride, Glide nose deadzone at the slot
//   -birdDriveEach 1 — N× BirdDrive (особь) вместо одного BirdFlockDrive
//   -flapHz / -flapAmp / -hover — взмах и средняя тяга (доли веса)
//   -stopFlap 4 — на 4-й секунде убрать мах: по умолчанию планирование и посадка
//   -stopFlapMode glide|stand — glide (пологая посадка) или stand (падение камнем)
//   -walk 1 -walkTime 5 -walkDuration 30 -walkStance 10 -walkTransfer 8 -walkFirst right
//   -run 1 -runTime 5 -runHold 20 — бег (короче Stance), вместе с -walk или отдельно
//   -swingHip / -swingKnee — сгиб свинга (бедро минус, колено плюс)
//   -swingHipUnload — flexor-bias свинга при grounded (доля сигнала)
//   -pelvisP / -pelvisD / -pelvisRef — PD таза к мировой вертикали
//   -crouchPelvis — целевой наклон таза вперёд при crouch=1
//   -neckFriction / -shoulderFriction / -elbowFriction / -wristFriction —
//   абсолютные K пассивной вязкости до BuildHuman (голова делит neckFriction)
//   -neckMuscle — момент мышц шеи и головы, абсолютный, после -muscle
   //   -neckP / -neckD / -neckTilt / -neckRef — PD шеи и головы к мировой вертикали
   //   -shoulderP / -elbowP / -wristP и парные D/Base/Ref — суставная поза рук
   //   -armShoulderBal / -armElbowBal / -armWristBal — контрперенос от CoM (град/ед.)
   // Все коэффициенты баланса можно переопределить аргументами, не пересобирая плеер,
// поэтому один билд обслуживает любой перебор параметров.
public class HeadlessTrial : MonoBehaviour
{
    // GameProcess проверяет этот флаг и не строит своих 50 человек,
    // иначе прогон утонет в 700 Rigidbody2D.
    public static bool Active { get; private set; }

    // Предохранитель: если физика зависнет, процесс не должен жить вечно.
    private const float REAL_TIME_LIMIT_SECONDS = 600f;
    private static readonly WaitForFixedUpdate FixedStep = new WaitForFixedUpdate();

    private float duration = 10f;
    private string label = "trial";
    private string outputFolder;
    private float startY = -0.82f;

    // Толчок в торс: проверка, держит ли регулятор возмущение, а не только
    // сам себя. Без него равновесие может быть просто статической позой.
    private float pushTime;
    private float pushImpulse;
    private Rigidbody2D torsoBody;

    // Присед через MotionIntent, не через клавиатуру: в batchmode её нет.
    private float crouchTarget;
    private float crouchTime;
    private float crouchHold;
    private float leanTarget;
    private float leanTime;
    private float leanHold;
    private MotionIntent intent;

    // Одноногая стойка: −1 левая опора, +1 правая, 0 обе. Как присед —
    // намерение выставляем каждый шаг, чтобы живой ввод не стёр сценарий.
    private float standLegTarget;
    private float standLegTime;
    private float standLegHold;

    // Чередование опоры: reuse StepPhaseDriver, не SIMBICON.
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
    private bool treePerch;
    private PlantTree plantTree;
    private PlantTrialRecorder plantRecorder;
    private bool spawnDog;
    private Dog dog;
    private DogTrialRecorder dogRecorder;
    private bool dogLeap;
    private float dogLeapTime = 2f;
    private float dogPreyX = 1.4f;
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

        // Пол солвера — только CLI-перебор цикла 5; прод остаётся 8/3.
        int velocityIterations = Mathf.RoundToInt(GetFloatArg("-velocityIterations", Physics2D.velocityIterations));
        int positionIterations = Mathf.RoundToInt(GetFloatArg("-positionIterations", Physics2D.positionIterations));
        if (velocityIterations > 0)
            Physics2D.velocityIterations = velocityIterations;
        if (positionIterations > 0)
            Physics2D.positionIterations = positionIterations;

        // Шаг физики задаём до загрузки сцены: иначе первый FixedUpdate
        // ещё идёт со стандартными 0.02 с, и сравнение частот будет кривым.
        float fixedDelta = GetFloatArg("-fixedDelta", Time.fixedDeltaTime);
        if (fixedDelta > 0f)
            Time.fixedDeltaTime = fixedDelta;

        GameObject host = new GameObject("HeadlessTrial");
        DontDestroyOnLoad(host);
        host.AddComponent<HeadlessTrial>();
    }

    // Стенд сравнивает прогоны побитово: без этого PhysX 2D на грани
    // контуров (правая опора) один и тот же exe даёт fell / не fell.
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
        treePerch = GetFloatArg("-treePerch", spawnTree && spawnBird ? 1f : 0f) > 0.5f;
        birdDriveKind = GetStringArg("-birdDrive", "").Trim().ToLowerInvariant();
        trialBirdKind = Bird.ParseKind(GetStringArg("-birdKind", "crow"));
        birdDriveCount = Mathf.Max(1, Mathf.RoundToInt(GetFloatArg("-birdCount", 1f)));
        birdDriveSeed = Mathf.RoundToInt(GetFloatArg("-birdDriveSeed", 1f));
        spawnDog = GetFloatArg("-dog", 0f) > 0.5f;
        dogLeap = GetFloatArg("-dogLeap", 0f) > 0.5f;
        dogLeapTime = GetFloatArg("-dogLeapTime", 2f);
        dogPreyX = GetFloatArg("-dogPreyX", 1.4f);
        if (spawnDog && spawnBird)
        {
            Debug.LogWarning("HeadlessTrial: -dog and -bird both set; running dog.");
            spawnBird = false;
        }
        stopFlapTime = GetFloatArg("-stopFlap", 0f);
        stopFlapMode = GetStringArg("-stopFlapMode", "glide").Trim().ToLowerInvariant();
        // Птица ниже человека: −0.82 оставит её в воздухе на метр.
        // Собака: лапы на y = −2.0, корень у центра груди.
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
        walkStanceDuration = GetFloatArg("-walkStance", 8f);
        runTarget = Mathf.Clamp01(GetFloatArg("-run", 0f));
        runTime = GetFloatArg("-runTime", 5f);
        runHold = GetFloatArg("-runHold", 20f);
        runStanceDuration = GetFloatArg("-runStance", 4f);
        runTransferMaxDuration = GetFloatArg("-runTransfer", 4f);
        walkHeelStrikeMinAge = GetFloatArg("-walkHeelMin", 0f);
        walkHeelAirMin = GetFloatArg("-walkHeelAir", 0f);
        walkComTrigger = GetFloatArg("-walkComTrigger", 0f);
        walkComTriggerMinAge = GetFloatArg("-walkComTrigAge", 0.35f);
        walkFirstStance = ParseStandLegArg(GetStringArg("-walkFirst", "right"));
        walkTransferMaxDuration = GetFloatArg("-walkTransfer", 8f);
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

        // После сборки сцены: мир PhysX уже создан, jobOptions надо зафиксировать снова.
        ConfigureDeterministicPhysics();

        StartCoroutine(RunTrial());
    }

    // ─── СБОРКА МИНИМАЛЬНОЙ СЦЕНЫ: ЗЕМЛЯ + ОДИН ЧЕЛОВЕК ИЛИ ПТИЦА ───
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

        if (spawnTree)
            return;

        GameObject humanObject = new GameObject("TrialHuman");
        // y = −0.82 ставит стопы точно на верхнюю кромку земли (−2.0):
        // (pelvisSize.y + torsoSize.y)/2 + бедро + голень + стопа
        // = 0.25 + 0.43 + 0.43 + 0.07 = 1.18.
        humanObject.transform.position = new Vector2(0f, startY);

        human = humanObject.AddComponent<Human>();
        human.footFriction = GetFloatArg("-footFriction", human.footFriction);
        // MotionIntent и StepPhaseDriver до PlayerInputSource: у того
        // RequireComponent(StepPhaseDriver), иначе Unity создаст второй
        // экземпляр, а стенд будет Tick-ать «наш», метрики — чужой.
        intent = humanObject.AddComponent<MotionIntent>();
        stepDriver = humanObject.AddComponent<StepPhaseDriver>();
        stepDriver.externalDrive = true;
        stepDriver.stanceDuration = walkStanceDuration;
        stepDriver.stanceHeelStrikeMinAge = walkHeelStrikeMinAge;
        stepDriver.stanceHeelAirMin = walkHeelAirMin;
        stepDriver.stanceComTrigger = walkComTrigger;
        stepDriver.stanceComMinAge = walkComTriggerMinAge;
        stepDriver.transferMaxDuration = walkTransferMaxDuration;
        stepDriver.transferMinDuration = walkTransferMinDuration;
        stepDriver.transferComOffsetMax = walkTransferComMax;
        stepDriver.transferFallbackComMax = walkTransferFallbackComMax;
        stepDriver.transferStandLegLevelMax = walkTransferLevelMax;
        stepDriver.firstStance = walkFirstStance;
        humanObject.AddComponent<PlayerInputSource>();

        // Моменты и трение читаются при построении тела, поэтому меняем их до BuildHuman.
        float muscleMultiplier = GetFloatArg("-muscle", 1f);
        float frictionMultiplier = GetFloatArg("-friction", 1f);
        ApplyMultipliers(human, muscleMultiplier, frictionMultiplier);
        // Абсолютные K после множителя: диагностический перебор tau
        // не должен зависеть от -friction и не трогает дефолты Human.
        ApplyFrictionOverrides(human);
        ApplyMuscleOverrides(human);

        human.BuildHuman();

        // Коэффициенты регуляторов живут в BalanceController, который появляется
        // внутри BuildHuman, поэтому переопределяем их после сборки.
        BalanceController balance = human.GetComponent<BalanceController>();
        ApplyGains(balance);
        // Driver уже на объекте до Awake. Bind закрывает late-add одним
        // вызовом, без GetComponent в FixedUpdate.
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
            // Тайминг до Bind: Reset wander читает sitMin/sitMax.
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
            // Особь: свой BirdDrive и сид на каждую птицу (дороже shared flock Update).
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
                Debug.LogWarning("HeadlessTrial: превышен лимит реального времени, прогон прерван.");
                break;
            }
        }

        Finish(simulated, Time.realtimeSinceStartup - trialWallStart);
    }

    private void ApplyBirdGains(BirdController c)
    {
        if (c == null)
        {
            Debug.LogError("HeadlessTrial: BirdController не найден.");
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

    // Переопределяет только те K, которые явно передали в CLI.
    // Шея и голова делят одно поле neckFriction — отдельного флага для головы нет.
    private static void ApplyFrictionOverrides(Human target)
    {
        if (target == null) return;
        target.neckFriction = GetFloatArg("-neckFriction", target.neckFriction);
        target.shoulderFriction = GetFloatArg("-shoulderFriction", target.shoulderFriction);
        target.elbowFriction = GetFloatArg("-elbowFriction", target.elbowFriction);
        target.wristFriction = GetFloatArg("-wristFriction", target.wristFriction);
    }

    // Момент мышц шеи и головы: абсолютное значение, после множителя -muscle.
    // Одно поле на оба сустава, как и в Human. Нужен отдельным флагом, потому
    // что при инерции 0.0015 кг·м² прежние 15 Н·м давали 9900 рад/с², и PD
    // разворачивал сустав каждый шаг физики.
    private static void ApplyMuscleOverrides(Human target)
    {
        if (target == null) return;
        target.neckMuscleTorque = GetFloatArg("-neckMuscle", target.neckMuscleTorque);
    }

    private void ApplyGains(BalanceController balance)
    {
        if (balance == null)
        {
            Debug.LogError("HeadlessTrial: BalanceController не найден на человеке.");
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
        balance.standLegRatePerSecond = GetFloatArg("-standLegRate", balance.standLegRatePerSecond);
        balance.standLegReleaseRatePerSecond = GetFloatArg("-standLegReleaseRate", balance.standLegReleaseRatePerSecond);

        // Скорость, с которой активация мышцы догоняет команду регулятора.
        // 500 — почти мгновенно; 10 — мышца набирает силу за ~0.1 с.
        balance.muscleActivationSpeed = GetFloatArg("-activationSpeed", balance.muscleActivationSpeed);
    }

    // ─── САМ ПРОГОН: ШАГАЕМ ФИЗИКОЙ И СНИМАЕМ СОСТОЯНИЕ ───
    private IEnumerator RunTrial()
    {
        // Первый шаг пропускаем: на нём тело ещё оседает в суставах.
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
                Debug.LogWarning("HeadlessTrial: превышен лимит реального времени, прогон прерван.");
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

            dogRecorder.Sample(simulated);

            if (Time.realtimeSinceStartup > realDeadline)
            {
                Debug.LogWarning("HeadlessTrial: превышен лимит реального времени, прогон прерван.");
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
                // Без маха не зависает: планирует и садится. Камнем — stand.
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
                Debug.LogWarning("HeadlessTrial: превышен лимит реального времени, прогон прерван.");
                break;
            }
        }

        Finish(simulated, Time.realtimeSinceStartup - trialWallStart);
    }

    // Импульс прикладываем к торсу: так толкают человека в плечо, а не в стопу.
    private void ApplyPush(float time)
    {
        if (torsoBody == null)
        {
            Debug.LogWarning("HeadlessTrial: торс не найден, толчок не применён.");
            return;
        }

        torsoBody.AddForce(new Vector2(pushImpulse, 0f), ForceMode2D.Impulse);
        if (recorder != null)
            recorder.MarkPush(time, pushImpulse);
        if (dogRecorder != null)
            dogRecorder.MarkPush(time, pushImpulse);
    }

    // Каждое окно удержания выставляем заново: если PlayerInputSource
    // вдруг оживёт, он не сотрёт сценарий одним кадром.
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

    // Наклон через MotionIntent: +1 вперёд, −1 назад (как стрелки в Play).
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

    // Чередование standLeg по таймеру: обе опоры до walkTime, потом ±1 каждые walkStance.
    // -run 1 в окне runTime…runHold ставит intent.run (короче Stance).
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
        stepDriver.transferMaxDuration = walkTransferMaxDuration;
        stepDriver.transferMinDuration = walkTransferMinDuration;
        stepDriver.transferComOffsetMax = walkTransferComMax;
        stepDriver.transferFallbackComMax = walkTransferFallbackComMax;
        stepDriver.transferStandLegLevelMax = walkTransferLevelMax;
        stepDriver.firstStance = walkFirstStance;
        stepDriver.Tick(time);
    }

    // left / −1 — опора слева, right / +1 — справа, 0 / both — обе ноги.
    // Строка, не float: иначе «left» молча падает в дефолт, как запятая в числе.
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

        Debug.LogWarning($"HeadlessTrial: не разобрал -standLeg={raw}, беру 0 (обе ноги).");
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
            else
            {
                File.WriteAllText(csvPath, recorder.BuildCsv());
                summary = recorder.BuildSummaryJson(simulated, wallSeconds);
            }
            File.WriteAllText(Path.Combine(outputFolder, label + ".json"), summary);

            // Маркер в stdout, чтобы результат читался прямо из лога прогона.
            Debug.Log("TRIAL_SUMMARY " + summary);
            Debug.Log("TRIAL_CSV " + csvPath);
        }
        catch (Exception e)
        {
            Debug.LogError("HeadlessTrial: не удалось записать результаты: " + e.Message);
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
            Debug.LogWarning("HeadlessTrial: не прочитал build-manifest: " + e.Message);
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

    // ─── ЧТЕНИЕ АРГУМЕНТОВ КОМАНДНОЙ СТРОКИ ───
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

        // Инвариантная культура обязательна: на русской локали запятая
        // и точка разбираются иначе, и параметры молча съезжают.
        if (float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
            return value;

        Debug.LogWarning($"HeadlessTrial: не разобрал {name}={raw}, беру {fallback}.");
        return fallback;
    }
}
