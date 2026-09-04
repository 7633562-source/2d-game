using UnityEngine;
using System.Collections.Generic;

public class GameProcess : MonoBehaviour
{
    [Header("Настройки создания людей")]
    public int humanCount = 1;
    public Vector2 humanStartPosition = new Vector2(-36.75f, -0.82f); // стопы касаются земли
    public float spacing = 1.5f;

    [Header("Настройки создания птиц")]
    [Tooltip("0 — не спавнить. Play стаи — BirdFlock50. World может держать небольшую стаю.")]
    public int birdCount = 0;
    [Tooltip("Сдвиг первой птицы вперёд от человека, м.")]
    public float birdOffsetX = 1.6f;
    public float birdSpacing = 1.2f;
    [Tooltip("Если включено и есть птица — камера за ней, не за человеком.")]
    public bool cameraFollowsBird = false;
    [Tooltip("+1 нос в +X, −1 в −X. Не scale корпуса.")]
    public float birdFacing = 1f;
    [Tooltip("Чётные в одну сторону, нечётные в другую.")]
    public bool alternateBirdFacing = false;
    [Tooltip("Через столько секунд стая разворачивается. 0 — нет.")]
    public float birdTurnAfter = 0f;
    [Tooltip("Мозг птицы. Пишет только mode, не силу.")]
    public bool birdDrive = false;
    [Tooltip("prey — пике на метку. wander — случайный полёт.")]
    public string birdDriveKind = "wander";
    [Tooltip("crow — полёт. chicken — земля, короткий прыжок.")]
    public string birdKind = "crow";
    [Tooltip("Отдельные куры на дворе. Не смешивать с birdKind стаи.")]
    public int chickenCount = 0;
    public float chickenOffsetX = 12f;
    public float chickenSpacing = 0.9f;
    [Tooltip("Добыча впереди первой птицы, м. Только prey.")]
    public float preyOffsetX = 10f;
    [Tooltip("Угроза впереди первой птицы, м. 0 — нет метки, wander как был.")]
    public float threatOffsetX = 0f;

    [Header("Номера людей для логирования")]
    public int[] logHumanNumbers = new int[] { 1, 10, 20, 30, 40, 50 };

    [Header("Trees")]
    [Tooltip("0 — do not spawn. SampleScene and World stay 0. Play is scene Tree.")]
    public int treeCount = 0;
    [Tooltip("Offset of the first tree from the human (or from origin), m.")]
    public float treeOffsetX = 2.2f;
    public float treeSpacing = 5f;
    [Tooltip("If on and a tree exists — camera follows the crown.")]
    public bool cameraFollowsTree = false;

    [Header("Dogs")]
    [Tooltip("0 — do not spawn. Play stance is scene Dog. World may hold a few.")]
    public int dogCount = 0;
    [Tooltip("Offset of the first dog from the human (or from origin), m.")]
    public float dogOffsetX = 0f;
    public float dogSpacing = 1.5f;
    [Tooltip("If on and a dog exists — camera follows the dog.")]
    public bool cameraFollowsDog = false;
    [Tooltip("Leap mark ahead of the first dog, m. Play scene Dog.")]
    public float dogPreyOffsetX = 1.4f;

    private GroundBuilder groundBuilder;
    private LevelGenerator levelGenerator;
    private List<Human> humans = new List<Human>();
    private List<Bird> birds = new List<Bird>();
    private List<PlantTree> trees = new List<PlantTree>();
    private List<Dog> dogs = new List<Dog>();

    void Start()
    {
        // В headless-прогоне сцену строит HeadlessTrial (один человек),
        // иначе к нему добавились бы ещё 50 и результат стал бы бессмысленным.
        if (HeadlessTrial.Active) return;

        // 200 Гц: при 50 Гц регулятор не успевает и раскачивает тело.
        Time.fixedDeltaTime = 0.005f;

        // Сцена Bird держит humanCount = 0. Раньше зажим «всегда ≥ 1»
        // подсовывал человека в полёт и сжирал второй остров из 16 тел.
        if (humanCount < 0) humanCount = 0;
        if (birdCount < 0) birdCount = 0;
        if (treeCount < 0) treeCount = 0;
        if (dogCount < 0) dogCount = 0;
        if (chickenCount < 0) chickenCount = 0;
        if (humanCount < 1 && birdCount < 1 && treeCount < 1 && dogCount < 1 && chickenCount < 1)
        {
            Debug.LogError("GameProcess: нужен humanCount ≥ 1, birdCount ≥ 1, chickenCount ≥ 1, treeCount ≥ 1 или dogCount ≥ 1.");
            return;
        }
        if (logHumanNumbers == null || logHumanNumbers.Length == 0)
            logHumanNumbers = new int[] { 1 };

        float startX = -(humanCount - 1) * spacing / 2f;
        humanStartPosition = new Vector2(startX, -0.82f);

        groundBuilder = GetComponent<GroundBuilder>();
        if (groundBuilder == null)
        {
            Debug.LogError("GroundBuilder не найден на GameManager!");
            return;
        }

        // Yard is the generator on World. Bird / Chicken / Dog / Tree scenes
        // have no LevelGenerator, so a flock or dogs here do not flatten the yard.
        levelGenerator = GetComponent<LevelGenerator>();
        if (levelGenerator != null)
        {
            levelGenerator.ApplyOverrideFlag();
            if (humanCount > 1)
                levelGenerator.profile = LevelProfile.Flat;
        }

        bool wantYard = levelGenerator != null
            && levelGenerator.profile != LevelProfile.Flat
            && humanCount >= 1;

        if (wantYard)
        {
            humanStartPosition = new Vector2(startX, levelGenerator.SpawnY);
            levelGenerator.Begin();
        }
        else
        {
            if (levelGenerator != null && !wantYard)
                levelGenerator = null;
            groundBuilder.groundSize = new Vector2(400f, 2f);
            groundBuilder.groundPosition = new Vector2(0f, -3f);
            groundBuilder.BuildGround();
        }

        CreateHumans();
        CreateBirds();
        CreateChickens();
        CreateTrees();
        SeatBirdsOnTrees();
        CreateDogs();
        SetupCameraFollow();
        SetupNightLighting();
        if (levelGenerator != null
            && levelGenerator.profile != LevelProfile.Flat
            && humans.Count > 0)
            levelGenerator.SetFollowTarget(humans[0].transform);
        SeatBirdsOnYardGrove();
        DisableCollisionsBetweenHumans();
        DisableCollisionsWithBirds();

        // Один герой в World — без GlobalHumanLogger: Find каждые 33 мс
        // съедает главный поток. Лабораторная толпа — лог как раньше.
        if (humanCount > 1)
        {
            SetupGlobalLogger();
            Debug.Log($"Логгер настроен на {logHumanNumbers.Length} человек(а): [{string.Join(", ", logHumanNumbers)}]");
        }
        else if (levelGenerator != null)
        {
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            // Профайлер только в batch (run-world-perf.ps1) или по явному флагу.
            if (GetComponent<WorldPerformanceMonitor>() == null && ShouldAttachWorldPerfMonitor())
            {
                gameObject.AddComponent<WorldPerformanceMonitor>();
                Debug.Log("WorldPerformanceMonitor: замер кадра включён (см. world_perf.txt).");
            }
        }
    }

    private void CreateHumans()
    {
        if (humanCount < 1) return;

        for (int i = 0; i < humanCount; i++)
        {
            int humanNumber = i + 1;

            // Одиночный человек — те же множители, что на стенде.
            // Иначе Play Mode крутил трение 0.75, а пороги сняты на 1.
            // Разброс по популяции оставляем только когда людей много.
            float muscleMultiplier;
            float frictionMultiplier;
            if (humanCount == 1)
            {
                muscleMultiplier = 1f;
                frictionMultiplier = 1f;
            }
            else
            {
                float t = i / (float)(humanCount - 1);
                muscleMultiplier = 0.8f + t * 0.4f;
                frictionMultiplier = 0.5f + t * 0.5f;
            }

            GameObject humanObject = new GameObject("Human_" + humanNumber);
            humanObject.transform.position = new Vector2(
                humanStartPosition.x + i * spacing,
                humanStartPosition.y
            );

            Human human = humanObject.AddComponent<Human>();

            human.hipMuscleTorque *= muscleMultiplier;
            human.kneeMuscleTorque *= muscleMultiplier;
            human.ankleExtensorTorque *= muscleMultiplier;
            human.ankleFlexorTorque *= muscleMultiplier;
            human.shoulderMuscleTorque *= muscleMultiplier;
            human.elbowMuscleTorque *= muscleMultiplier;
            human.wristMuscleTorque *= muscleMultiplier;
            human.neckMuscleTorque *= muscleMultiplier;
            human.lumbarMuscleTorque *= muscleMultiplier;

            human.hipFriction *= frictionMultiplier;
            human.kneeFriction *= frictionMultiplier;
            human.ankleFriction *= frictionMultiplier;
            human.shoulderFriction *= frictionMultiplier;
            human.elbowFriction *= frictionMultiplier;
            human.wristFriction *= frictionMultiplier;
            human.neckFriction *= frictionMultiplier;
            human.lumbarFriction *= frictionMultiplier;

            human.muscleMultiplier = muscleMultiplier;
            human.frictionMultiplier = frictionMultiplier;

            // Намерение и клавиатура только у того, которым играют.
            // Остальные при большом humanCount остаются без ввода.
            if (i == 0)
            {
                humanObject.AddComponent<MotionIntent>();
                humanObject.AddComponent<StepPhaseDriver>();
                humanObject.AddComponent<PlayerInputSource>();
                FactionStamp.Player(humanObject);
            }

            human.BuildHuman();
            human.AddNumberLabel(humanNumber);

            humans.Add(human);
        }
    }

    private void CreateBirds()
    {
        if (birdCount < 1) return;

        float originX = humans.Count > 0
            ? humans[0].transform.position.x + birdOffsetX
            : 0f;
        float groundY = SurfaceAt(originX);

        string driveKind = (birdDriveKind ?? "wander").Trim().ToLowerInvariant();
        bool wander = birdDrive && driveKind != "prey";
        bool preyDrive = birdDrive && driveKind == "prey";
        Transform prey = null;
        if (preyDrive)
            prey = SpawnBirdPrey(new Vector2(originX + preyOffsetX, groundY + 0.04f));
        Transform threat = null;
        if (birdDrive && Mathf.Abs(threatOffsetX) > 0.01f)
            threat = SpawnBirdThreat(new Vector2(originX + threatOffsetX, groundY + 0.9f));
        SoundBus.Clear();

        for (int i = 0; i < birdCount; i++)
        {
            GameObject birdObject = new GameObject("Bird_" + (i + 1));
            Bird bird = birdObject.AddComponent<Bird>();
            bird.rig = BirdRig.Flock;
            bird.kind = Bird.ParseKind(birdKind);
            bird.ApplyKind(bird.kind);
            float x = originX + i * birdSpacing;
            float y = bird.StandingRootOffset(SurfaceAt(x));
            birdObject.transform.position = new Vector2(x, y);
            bird.BuildBird();
            FactionStamp.Bird(birdObject);
            if (bird.controller != null)
            {
                bird.controller.flapPhaseOffset = i * 0.13f;
                bird.controller.turnAfter = birdDrive ? 0f : birdTurnAfter;
                float face = birdFacing >= 0f ? 1f : -1f;
                if (alternateBirdFacing && !birdDrive && (i % 2) == 1)
                    face = -face;
                bird.controller.SetFacing(face);
                if (preyDrive)
                {
                    bird.controller.mode = BirdMode.Sit;
                    bird.controller.takeoffDelay = 0f;
                    BirdDrive drive = birdObject.AddComponent<BirdDrive>();
                    drive.kind = BirdDrive.Kind.Prey;
                    drive.prey = prey;
                    drive.threat = threat;
                }
                else if (wander && birdCount == 1)
                {
                    bird.controller.mode = BirdMode.Sit;
                    bird.controller.takeoffDelay = 0f;
                    BirdDrive drive = birdObject.AddComponent<BirdDrive>();
                    drive.kind = BirdDrive.Kind.Wander;
                    drive.wanderSeed = 1;
                    drive.threat = threat;
                }
                else if (wander)
                {
                    bird.controller.mode = BirdMode.Sit;
                    bird.controller.takeoffDelay = 0f;
                }
                else
                {
                    bird.controller.takeoffDelay = 0.7f + (i % 7) * 0.22f;
                    bird.controller.mode = BirdMode.Stand;
                }
            }
            // TextMesh на десятках особей дороже самой физики стаи.
            if (birdCount <= 8)
                bird.AddNumberLabel(i + 1);
            birds.Add(bird);
        }

        if (wander && birdCount > 1)
        {
            GameObject host = new GameObject("BirdFlockDrive");
            BirdFlockDrive flock = host.AddComponent<BirdFlockDrive>();
            flock.Bind(birds.ToArray(), 1);
        }
    }

    // Hens are a second spawn. Do not fold them into birdKind or FlockDrive.
    private void CreateChickens()
    {
        if (chickenCount < 1) return;

        float originX = humans.Count > 0
            ? humans[0].transform.position.x + chickenOffsetX
            : chickenOffsetX;

        for (int i = 0; i < chickenCount; i++)
        {
            float x = originX + i * chickenSpacing;
            float groundY = SurfaceAt(x);
            GameObject henObject = new GameObject("Chicken_" + (i + 1));
            Bird hen = henObject.AddComponent<Bird>();
            hen.rig = BirdRig.Flock;
            hen.kind = BirdKind.Chicken;
            hen.ApplyKind(BirdKind.Chicken);
            henObject.transform.position = new Vector2(x, hen.StandingRootOffset(groundY));
            hen.BuildBird();
            FactionStamp.Bird(henObject);
            if (hen.controller != null)
            {
                hen.controller.mode = BirdMode.Sit;
                hen.controller.takeoffDelay = 0f;
                hen.controller.SetFacing(birdFacing >= 0f ? 1f : -1f);
            }

            BirdDrive drive = henObject.AddComponent<BirdDrive>();
            drive.kind = BirdDrive.Kind.Wander;
            drive.wanderSeed = 11 + i * 13;
            birds.Add(hen);
        }
    }

    private float SurfaceAt(float x)
    {
        if (levelGenerator != null && levelGenerator.profile == LevelProfile.Test1)
            return LevelTest1.SurfaceAt(x);
        return LevelGenerator.DefaultSurfaceY;
    }

    // Метка добычи: не тело и не сила. BirdDrive читает только позицию.
    private static Transform SpawnBirdPrey(Vector2 position)
    {
        GameObject mark = new GameObject("BirdPrey");
        mark.transform.position = position;
        SpriteRenderer sprite = mark.AddComponent<SpriteRenderer>();
        Texture2D tex = new Texture2D(8, 8, TextureFormat.RGBA32, false);
        Color[] fill = new Color[64];
        Color pebble = new Color(0.35f, 0.22f, 0.12f, 1f);
        for (int i = 0; i < fill.Length; i++)
            fill[i] = pebble;
        tex.SetPixels(fill);
        tex.Apply(false, true);
        sprite.sprite = Sprite.Create(tex, new Rect(0, 0, 8, 8), new Vector2(0.5f, 0.5f), 50);
        sprite.sortingOrder = 4;
        SpriteLighting.ApplyLit(sprite);
        return mark.transform;
    }

    private static Transform SpawnBirdThreat(Vector2 position)
    {
        GameObject mark = new GameObject("BirdThreat");
        mark.transform.position = position;
        SpriteRenderer sprite = mark.AddComponent<SpriteRenderer>();
        Texture2D tex = new Texture2D(8, 8, TextureFormat.RGBA32, false);
        Color[] fill = new Color[64];
        Color threat = new Color(0.65f, 0.08f, 0.05f, 1f);
        for (int i = 0; i < fill.Length; i++)
            fill[i] = threat;
        tex.SetPixels(fill);
        tex.Apply(false, true);
        sprite.sprite = Sprite.Create(tex, new Rect(0, 0, 8, 8), new Vector2(0.5f, 0.5f), 50);
        sprite.sortingOrder = 4;
        SpriteLighting.ApplyLit(sprite);
        return mark.transform;
    }

    private void CreateTrees()
    {
        if (treeCount < 1) return;

        float groundY = LevelGenerator.DefaultSurfaceY;
        float originX = 0f;
        if (humans.Count > 0)
            originX = humans[0].transform.position.x + treeOffsetX;
        else if (birds.Count > 0)
            originX = birds[0].transform.position.x + treeOffsetX;
        else
            originX = treeOffsetX;

        for (int i = 0; i < treeCount; i++)
        {
            TreeKind kind = PlantTree.KindFromPlantIndex(i);
            GameObject treeObject = new GameObject("Tree_" + (i + 1) + "_" + kind);
            PlantTree tree = treeObject.AddComponent<PlantTree>();
            tree.kind = kind;
            tree.seed = 1 + i * 17;
            // One Sway is a Human of bodies. A row is a gallery: first sways.
            if (treeCount > 1 && i > 0)
                tree.rig = TreeRig.Static;
            treeObject.transform.position = new Vector2(
                originX + i * treeSpacing,
                PlantTree.RootY(groundY));
            tree.BuildTree();
            trees.Add(tree);
        }
    }

    private void SeatBirdsOnTrees()
    {
        if (trees.Count == 0 || birds.Count == 0) return;
        Bird.GhostTreeWood(birds.ToArray(), trees.ToArray());
        PlantTree.SeatBirds(birds.ToArray(), trees[0]);
        BirdFlockDrive flock = FindFirstObjectByType<BirdFlockDrive>();
        if (flock != null)
            flock.BindPerches(trees[0].GetPerchSlots());
    }

    // Grove plants after SetFollowTarget. Static trees, pads only if a bird sits.
    private void SeatBirdsOnYardGrove()
    {
        PlantTree[] grove = LevelGrove.Planted;
        if (grove == null || grove.Length == 0 || birds.Count == 0)
            return;

        Bird[] crows = CrowsOnly();
        if (crows.Length == 0)
            return;

        Bird.GhostTreeWood(crows, grove);
        PlantTree gate = NearestTree(grove, 40f);
        if (gate == null)
            return;
        PlantTree.SeatBirds(crows, gate);
        BirdFlockDrive flock = FindFirstObjectByType<BirdFlockDrive>();
        if (flock != null)
            flock.BindPerches(gate.GetPerchSlots());
    }

    private Bird[] CrowsOnly()
    {
        int n = 0;
        for (int i = 0; i < birds.Count; i++)
        {
            if (birds[i] != null && birds[i].kind != BirdKind.Chicken)
                n++;
        }

        Bird[] crows = new Bird[n];
        int w = 0;
        for (int i = 0; i < birds.Count; i++)
        {
            if (birds[i] != null && birds[i].kind != BirdKind.Chicken)
                crows[w++] = birds[i];
        }

        return crows;
    }

    private static PlantTree NearestTree(PlantTree[] grove, float x)
    {
        PlantTree best = null;
        float bestD = float.MaxValue;
        for (int i = 0; i < grove.Length; i++)
        {
            if (grove[i] == null)
                continue;
            float d = Mathf.Abs(grove[i].transform.position.x - x);
            if (d < bestD)
            {
                bestD = d;
                best = grove[i];
            }
        }

        return best;
    }

    private void CreateDogs()
    {
        if (dogCount < 1) return;

        float originX = 0f;
        if (humans.Count > 0)
            originX = humans[0].transform.position.x + dogOffsetX;
        else if (birds.Count > 0)
            originX = birds[0].transform.position.x + dogOffsetX;
        else if (trees.Count > 0)
            originX = trees[0].transform.position.x + dogOffsetX;
        else
            originX = dogOffsetX;

        for (int i = 0; i < dogCount; i++)
        {
            float x = originX + i * dogSpacing;
            float groundY = SurfaceAt(x);
            GameObject dogObject = new GameObject("Dog_" + (i + 1));
            Dog dog = dogObject.AddComponent<Dog>();
            dogObject.transform.position = new Vector2(
                x,
                dog.StandingRootOffset(groundY));
            dog.BuildDog();
            FactionStamp.Wolf(dogObject);
            dogs.Add(dog);
        }

        // Leap mark: position only. JawStrike reads Damageable. Not a force.
        float preyX = originX + dogPreyOffsetX;
        DogPrey prey = DogPrey.Spawn(new Vector2(preyX, SurfaceAt(preyX) + DogPrey.DefaultHeightAboveGround));
        for (int i = 0; i < dogs.Count; i++)
        {
            if (dogs[i] != null && dogs[i].stance != null && prey != null)
                dogs[i].stance.leapTarget = prey.transform;
        }
    }

    // Только картинка: CameraFollow в LateUpdate, силы не трогает.
    private void SetupCameraFollow()
    {
        Camera cam = Camera.main;
        if (cam == null) return;
        if (humans.Count == 0 && birds.Count == 0 && trees.Count == 0 && dogs.Count == 0) return;

        cam.backgroundColor = SceneLighting.NightSky;
        cam.orthographic = true;

        CameraFollow follow = cam.GetComponent<CameraFollow>();
        if (follow == null)
            follow = cam.gameObject.AddComponent<CameraFollow>();

        // Стая в ряд: 50×1.2 м ≈ 59 м. Кадр 3.2 видит 1–2 особи.
        // Ширина = 2 * size * aspect; при 16:9 size ≈ 17 закрывает ряд.
        // Физику и шаг не трогаем — только картинка.
        bool flockOverview = cameraFollowsBird && birds.Count >= 10;
        if (flockOverview)
        {
            float minX = birds[0].transform.position.x;
            float maxX = minX;
            for (int i = 1; i < birds.Count; i++)
            {
                float x = birds[i].transform.position.x;
                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
            }
            float span = Mathf.Max(4f, maxX - minX);
            float padding = 4f;
            float aspect = cam.aspect > 0.1f ? cam.aspect : 16f / 9f;
            cam.orthographicSize = (span + padding) * 0.5f / aspect;
            follow.BindFlock(birds);
        }
        else
        {
            // Стенд: человек ~1.75 м в кадре с запасом на голову. Мир/уровни — шире.
            bool treeOnly = trees.Count > 0 && humans.Count == 0 && birds.Count == 0 && dogs.Count == 0;
            bool dogOnly = dogs.Count > 0 && humans.Count == 0 && birds.Count == 0 && trees.Count == 0;
            bool tight = levelGenerator == null || levelGenerator.profile == LevelProfile.Flat;
            if (treeOnly)
                cam.orthographicSize = 4.6f;
            else if (dogOnly)
                cam.orthographicSize = 3.6f;
            else
                cam.orthographicSize = tight ? 3.2f : 5f;
            if (cameraFollowsDog && dogs.Count > 0)
                follow.Bind(dogs[0]);
            else if (cameraFollowsTree && trees.Count > 0)
                follow.Bind(trees[0]);
            else if (cameraFollowsBird && birds.Count > 0)
                follow.Bind(birds[0]);
            else if (humans.Count > 0)
                follow.Bind(humans[0]);
            else if (dogs.Count > 0)
                follow.Bind(dogs[0]);
            else if (trees.Count > 0)
                follow.Bind(trees[0]);
        }
    }

    // Только картинка. Стенд headless сюда не заходит (Start возвращает раньше).
    private void SetupNightLighting()
    {
        SceneLighting.Ensure(Camera.main);
        if (!SceneLighting.Enabled)
            return;
        if (humans.Count == 0 && birds.Count == 0 && trees.Count == 0 && dogs.Count == 0)
            return;
        Vector3 root = humans.Count > 0
            ? humans[0].transform.position
            : birds.Count > 0
                ? birds[0].transform.position
                : dogs.Count > 0
                    ? dogs[0].transform.position
                    : trees[0].transform.position;
        // Низ шеста на линии стояния y = −2.
        Vector3 torchPos = new Vector3(root.x + 1.25f, -2f, 0f);
        Torch.Spawn(torchPos);
    }

    private void DisableCollisionsBetweenHumans()
    {
        for (int i = 0; i < humans.Count; i++)
        {
            Collider2D[] collidersA = humans[i].GetComponentsInChildren<Collider2D>();
            for (int j = i + 1; j < humans.Count; j++)
            {
                Collider2D[] collidersB = humans[j].GetComponentsInChildren<Collider2D>();
                foreach (var ca in collidersA)
                {
                    foreach (var cb in collidersB)
                    {
                        Physics2D.IgnoreCollision(ca, cb, true);
                    }
                }
            }
        }
    }

    private void DisableCollisionsWithBirds()
    {
        if (birds.Count == 0) return;

        List<Collider2D[]> packs = new List<Collider2D[]>();
        foreach (Human human in humans)
            packs.Add(human.GetComponentsInChildren<Collider2D>());
        foreach (Bird bird in birds)
            packs.Add(bird.GetComponentsInChildren<Collider2D>());

        for (int i = 0; i < packs.Count; i++)
        {
            for (int j = i + 1; j < packs.Count; j++)
            {
                // Пары человек–человек уже выключены выше. Здесь — птица–птица
                // и человек–птица: иначе 16+15 тел липнут друг к другу на спавне.
                bool bothHuman = i < humans.Count && j < humans.Count;
                if (bothHuman) continue;
                foreach (Collider2D ca in packs[i])
                {
                    foreach (Collider2D cb in packs[j])
                        Physics2D.IgnoreCollision(ca, cb, true);
                }
            }
        }
    }

    private void SetupGlobalLogger()
    {
        GlobalHumanLogger logger = gameObject.AddComponent<GlobalHumanLogger>();
        logger.fileName = "human_log.txt";
        logger.logFolderPath = "";
        logger.logIntervalSeconds = 0.033f;

        List<Human> selectedHumans = new List<Human>();
        List<int> selectedNumbers = new List<int>();
        foreach (int num in logHumanNumbers)
        {
            if (num >= 1 && num <= humans.Count)
            {
                selectedHumans.Add(humans[num - 1]);
                selectedNumbers.Add(num);
            }
        }

        logger.SetHumansToLog(selectedHumans.ToArray(), selectedNumbers.ToArray());
    }

    // Batch (run-world-perf.ps1) всегда; Play Mode — только Tools/force-world-perf.flag.
    private static bool ShouldAttachWorldPerfMonitor()
    {
        if (Application.isBatchMode)
            return true;

        string flag = System.IO.Path.Combine(Application.dataPath, "..", "Tools", "force-world-perf.flag");
        return System.IO.File.Exists(flag);
    }
}