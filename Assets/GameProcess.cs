using UnityEngine;
using System.Collections.Generic;

public class GameProcess : MonoBehaviour
{
    [Header("Human spawn")]
    public int humanCount = 1;
    public Vector2 humanStartPosition = new Vector2(-36.75f, -0.82f); // feet touch the ground
    public float spacing = 1.5f;

    [Header("Bird spawn")]
    [Tooltip("0 — do not spawn. Flock Play is BirdFlock50. World may hold a small flock.")]
    public int birdCount = 0;
    [Tooltip("Offset of the first bird ahead of the human, m.")]
    public float birdOffsetX = 1.6f;
    public float birdSpacing = 1.2f;
    [Tooltip("If on and a bird exists — camera follows the bird, not the human.")]
    public bool cameraFollowsBird = false;
    [Tooltip("+1 nose along +X, −1 along −X. Not a body scale.")]
    public float birdFacing = 1f;
    [Tooltip("Even indices one way, odd the other.")]
    public bool alternateBirdFacing = false;
    [Tooltip("Seconds until the flock turns around. 0 — off.")]
    public float birdTurnAfter = 0f;
    [Tooltip("Bird brain. Writes mode only, not force.")]
    public bool birdDrive = false;
    [Tooltip("prey — hunt Insect (Peck on dirt). wander — random flight.")]
    public string birdDriveKind = "wander";
    [Tooltip("crow — flight. chicken — ground, short hop.")]
    public string birdKind = "crow";
    [Tooltip("Separate hens in the yard. Do not mix with the flock birdKind.")]
    public int chickenCount = 0;
    public float chickenOffsetX = 12f;
    public float chickenSpacing = 0.9f;
    [Tooltip("World pack: this many hens stay on chickenOffsetX (live near the camera). 0 — all use the offset row.")]
    public int chickenNearbyCount = 0;
    [Tooltip("World pack: remaining hens start here, m ahead of the human. 0 — no park row.")]
    public float chickenParkOffsetX = 0f;
    [Tooltip("Prey ahead of the first bird, m. Prey only.")]
    public float preyOffsetX = 10f;
    [Tooltip("Threat ahead of the first bird, m. 0 — no mark, wander stays as before.")]
    public float threatOffsetX = 0f;

    [Header("Human numbers to log")]
    public int[] logHumanNumbers = new int[] { 1, 10, 20, 30, 40, 50 };

    [Header("Trees")]
    [Tooltip("0 — do not spawn. SampleScene and World stay 0. Play is scene Tree.")]
    public int treeCount = 0;
    [Tooltip("Offset of the first tree from the human (or from origin), m.")]
    public float treeOffsetX = 2.2f;
    public float treeSpacing = 5f;
    [Tooltip("If on and a tree exists — camera follows the crown.")]
    public bool cameraFollowsTree = false;
    [Tooltip("Empty — the yard gallery (KindFromGalleryIndex). \"apple\" builds an AppleTree instead: "
        + "one species per scene (Yury 18.09), the apple lane's own rig.")]
    public string treeSpecies = "";
    [Tooltip("Apple crown height, m. 0 = the seed's 2–5 m. The TreeApple scene proof pins it.")]
    public float treeAppleHeightM = 0f;
    [Tooltip("Tree seed. -1 = 1 + i * 17 as before.")]
    public int treeSeed = -1;

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

    [Header("Flowers")]
    [Tooltip("0 — do not spawn. Play gallery is scene Tree. Not World.")]
    public int flowerCount = 0;
    [Tooltip("Offset of the first flower from origin (or from the human), m.")]
    public float flowerOffsetX = 0f;
    public float flowerSpacing = 0.55f;

    [Header("Campfire lab")]
    [Tooltip("Play is scene Campfire. World/Level1 still plant via LevelGenerator.")]
    public bool spawnCampfire = false;

    [Header("Play capture")]
    [Tooltip("Attach PlayCaptureRecorder in non-headless Play mode.")]
    public bool enablePlayCapture = true;

    private GroundBuilder groundBuilder;
    private LevelGenerator levelGenerator;
    private List<Human> humans = new List<Human>();
    private List<Bird> birds = new List<Bird>();
    private List<PlantTree> trees = new List<PlantTree>();
    private List<AppleTree> appleTrees = new List<AppleTree>();
    private List<PlantFlower> flowers = new List<PlantFlower>();
    private List<Dog> dogs = new List<Dog>();
    private Camera playCamera;
    private CameraFollow playFollow;
    private BirdFlockDrive flockDrive;
    // One Insect for Play birds/hens. World keeps the generator — no crowd.
    private Transform birdPreyMark;

    void Start()
    {
        // In a headless run HeadlessTrial builds the scene (one human);
        // otherwise fifty more would spawn and the result would be meaningless.
        if (HeadlessTrial.Active) return;

        // 200 Hz: at 50 Hz the controller cannot keep up and rocks the body.
        Time.fixedDeltaTime = 0.005f;

        // Scene Bird keeps humanCount = 0. The old clamp "always ≥ 1"
        // used to sneak a human into flight and eat a second island of 16 bodies.
        if (humanCount < 0) humanCount = 0;
        if (birdCount < 0) birdCount = 0;
        if (treeCount < 0) treeCount = 0;
        if (dogCount < 0) dogCount = 0;
        if (chickenCount < 0) chickenCount = 0;
        if (flowerCount < 0) flowerCount = 0;
        if (humanCount < 1 && birdCount < 1 && treeCount < 1 && dogCount < 1 && chickenCount < 1 && flowerCount < 1 && !spawnCampfire)
        {
            Debug.LogError("GameProcess: need humanCount ≥ 1, birdCount ≥ 1, chickenCount ≥ 1, treeCount ≥ 1, flowerCount ≥ 1, dogCount ≥ 1, or spawnCampfire.");
            return;
        }
        if (logHumanNumbers == null || logHumanNumbers.Length == 0)
            logHumanNumbers = new int[] { 1 };

        float startX = -(humanCount - 1) * spacing / 2f;
        humanStartPosition = new Vector2(startX, -0.82f);

        groundBuilder = GetComponent<GroundBuilder>();
        if (groundBuilder == null)
        {
            Debug.LogError("GroundBuilder not found on GameManager!");
            return;
        }

        // Yard is the generator on World. Bird / Chicken / Dog / Tree /
        // Campfire scenes have no LevelGenerator, so a flock or dogs here
        // do not flatten the yard.
        levelGenerator = GetComponent<LevelGenerator>();
        if (levelGenerator != null)
        {
            levelGenerator.ApplyOverrideFlag();
            if (humanCount > 1)
                levelGenerator.profile = LevelProfile.Flat;
        }

        // World.unity leftover Inspector is bird/chicken/dog 1 and tree/flower 1.
        // OPT-3 rest distances assume the showcase pack (dogs 26, crows 39, hens 12).
        ApplyWorldShowcasePack();

        bool wantYard = levelGenerator != null
            && levelGenerator.profile != LevelProfile.Flat
            && humanCount >= 1;

        if (wantYard)
        {
            if (levelGenerator.profile == LevelProfile.Test1)
                startX = LevelTest1.PlayerX;
            humanStartPosition = new Vector2(startX, levelGenerator.SpawnY);
            try
            {
                levelGenerator.Begin();
            }
            catch (System.Exception e)
            {
                // Yard pictures must not skip CreateHumans. WebGL then shows
                // an empty World with no hero and no Walk button.
                Debug.LogException(e);
            }
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
        CreateFlowers();
        SeatBirdsOnTrees();
        CreateDogs();
        CreateCampfire();
        SetupCameraFollow();
        SetupNightLighting();
        SetupPlayCapture();
        if (levelGenerator != null
            && levelGenerator.profile != LevelProfile.Flat
            && humans.Count > 0)
            levelGenerator.SetFollowTarget(humans[0].transform);
        SeatBirdsOnYardGrove();
        DisableCollisionsBetweenHumans();
        DisableCollisionsWithBirds();
        EnsureWorldPerfMonitor();
        // autoSyncTransforms is off. NpcRest.ProbeX reads Rigidbody2D.position
        // (chest / Body), not the host Transform still sitting on dirt spawn.
        // A full-scene SyncTransforms here was the BIRD-12 flock stall and
        // is not required for rest/wake.
        AttachNpcRest();

        // One hero in World — no GlobalHumanLogger: Find every 33 ms
        // eats the main thread. Laboratory crowd — log as before.
        if (humanCount > 1)
        {
            SetupGlobalLogger();
            Debug.Log($"Logger set for {logHumanNumbers.Length} human(s): [{string.Join(", ", logHumanNumbers)}]");
        }
    }

    private void CreateHumans()
    {
        if (humanCount < 1) return;

        for (int i = 0; i < humanCount; i++)
        {
            int humanNumber = i + 1;

            // A single human — the same multipliers as on the stand.
            // Otherwise Play Mode ran friction 0.75 while thresholds were taken at 1.
            // Population spread stays only when there are many humans.
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
            human.toeMuscleTorque *= muscleMultiplier;
            human.neckMuscleTorque *= muscleMultiplier;
            human.lumbarMuscleTorque *= muscleMultiplier;

            human.hipFriction *= frictionMultiplier;
            human.kneeFriction *= frictionMultiplier;
            human.ankleFriction *= frictionMultiplier;
            human.shoulderFriction *= frictionMultiplier;
            human.elbowFriction *= frictionMultiplier;
            human.wristFriction *= frictionMultiplier;
            human.toeFriction *= frictionMultiplier;
            human.neckFriction *= frictionMultiplier;
            human.lumbarFriction *= frictionMultiplier;

            human.muscleMultiplier = muscleMultiplier;
            human.frictionMultiplier = frictionMultiplier;

            // Intent and keyboard only on the one being played.
            // The rest at a large humanCount stay without input.
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
        // Test1 apple is planted in LevelGenerator.Begin, before this spawn.
        if (LevelGrove.Apple != null)
            originX = LevelGrove.Apple.transform.position.x;
        float groundY = SurfaceAt(originX);

        string driveKind = (birdDriveKind ?? "wander").Trim().ToLowerInvariant();
        bool wander = birdDrive && driveKind != "prey";
        bool preyDrive = birdDrive && driveKind == "prey";
        Transform prey = birdPreyMark;
        if (prey == null && WantsPlayInsect())
        {
            prey = Insect.SpawnOnDirt(originX + preyOffsetX, groundY);
            birdPreyMark = prey;
        }
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
            // Stamp before BuildBird so BeakStrike.Awake sees Rel Hostile.
            FactionStamp.Bird(birdObject);
            bird.BuildBird();
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
                    drive.threat = threat;
                    drive.BindPrey(i == 0 ? prey : null);
                }
                else if (wander && birdCount == 1)
                {
                    bird.controller.mode = BirdMode.Sit;
                    bird.controller.takeoffDelay = 0f;
                    BirdDrive drive = birdObject.AddComponent<BirdDrive>();
                    drive.kind = BirdDrive.Kind.Wander;
                    drive.wanderSeed = 1;
                    drive.threat = threat;
                    drive.BindPrey(prey);
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
            // TextMesh on dozens of birds costs more than the flock physics itself.
            // World already has a yard — debug numbers are not the picture.
            if (birdCount <= 8 && levelGenerator == null)
                bird.AddNumberLabel(i + 1);
            birds.Add(bird);
        }

        if (wander && birdCount > 1)
        {
            GameObject host = new GameObject("BirdFlockDrive");
            flockDrive = host.AddComponent<BirdFlockDrive>();
            flockDrive.threat = threat;
            flockDrive.Bind(birds.ToArray(), 1);
            flockDrive.BindPrey(prey);
        }
    }

    // Hens are a second spawn. Do not fold them into birdKind or FlockDrive.
    private void CreateChickens()
    {
        if (chickenCount < 1) return;

        float originX = humans.Count > 0
            ? humans[0].transform.position.x + chickenOffsetX
            : chickenOffsetX;
        // World showcase: two hens at x≈12 stay live (OPT-3). The rest
        // sit past the walker rest band so a wide Game view cannot keep
        // all ten on the 200 Hz island. Scene Chicken leaves park at 0.
        bool parkFar = chickenParkOffsetX > 0.01f && chickenNearbyCount < chickenCount;
        float parkX = parkFar
            ? (humans.Count > 0
                ? humans[0].transform.position.x + chickenParkOffsetX
                : chickenParkOffsetX)
            : originX;
        int nearby = parkFar ? Mathf.Max(0, chickenNearbyCount) : chickenCount;
        if (birdPreyMark == null && WantsPlayInsect())
        {
            float preyX = originX + preyOffsetX;
            birdPreyMark = Insect.SpawnOnDirt(preyX, SurfaceAt(preyX));
        }

        for (int i = 0; i < chickenCount; i++)
        {
            float x = i < nearby
                ? originX + i * chickenSpacing
                : parkX + (i - nearby) * chickenSpacing;
            float groundY = SurfaceAt(x);
            GameObject henObject = new GameObject("Chicken_" + (i + 1));
            Bird hen = henObject.AddComponent<Bird>();
            hen.rig = BirdRig.Flock;
            hen.kind = BirdKind.Chicken;
            hen.ApplyKind(BirdKind.Chicken);
            henObject.transform.position = new Vector2(x, hen.StandingRootOffset(groundY));
            FactionStamp.Bird(henObject);
            hen.BuildBird();
            if (hen.controller != null)
            {
                hen.controller.mode = BirdMode.Sit;
                hen.controller.takeoffDelay = 0f;
                hen.controller.SetFacing(birdFacing >= 0f ? 1f : -1f);
            }

            BirdDrive drive = henObject.AddComponent<BirdDrive>();
            // Hunter hen: Kind.Prey so GlanceBoundInsect faces the Insect
            // (Forage → Pursue). The rest stay wander. One quarry.
            drive.kind = (i == 0 && birdPreyMark != null)
                ? BirdDrive.Kind.Prey
                : BirdDrive.Kind.Wander;
            drive.wanderSeed = 11 + i * 13;
            // One Insect, one hunter. Binding every hen made a Walk pile.
            drive.BindPrey(i == 0 ? birdPreyMark : null);
            birds.Add(hen);
        }
    }

    // One Insect on Play Bird / Chicken. Forage until seen, then Pursue.
    // Scene Bird already sets preyOffsetX; a wander crow must still spawn
    // that quarry (not only Kind.Prey / hens). World is generator. Flock50
    // is 50 — not a crowd of insects, and not this Play leash.
    private bool WantsPlayInsect()
    {
        if (!birdDrive || levelGenerator != null)
            return false;
        string kind = (birdDriveKind ?? "wander").Trim().ToLowerInvariant();
        if (kind == "prey")
            return true;
        if (Mathf.Abs(preyOffsetX) <= 0.01f)
            return false;
        int hunters = chickenCount > 0 ? chickenCount : birdCount;
        return hunters >= 1 && hunters <= 8;
    }

    private float SurfaceAt(float x)
    {
        if (levelGenerator != null && levelGenerator.profile == LevelProfile.Test1)
            return LevelTest1.SurfaceAt(x);
        return LevelGenerator.DefaultSurfaceY;
    }

    private static Transform SpawnBirdThreat(Vector2 position)
    {
        GameObject mark = new GameObject("BirdThreat");
        mark.transform.position = position;
        SpriteRenderer sprite = mark.AddComponent<SpriteRenderer>();
        sprite.sprite = LevelArt.White();
        sprite.drawMode = SpriteDrawMode.Sliced;
        sprite.size = new Vector2(0.16f, 0.16f);
        sprite.color = new Color(0.65f, 0.08f, 0.05f, 1f);
        sprite.sortingOrder = 4;
        SpriteLighting.ApplyLit(sprite);
        FactionStamp.Wolf(mark);
        return mark.transform;
    }

    private void CreateTrees()
    {
        if (treeCount < 1) return;
        // World / Level1 grove is LevelGrove Static. Leftover Inspector
        // treeCount=1 used to plant a Sway (16 RB) on the hero camera.
        if (levelGenerator != null)
            return;

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
            TreeKind kind = PlantTree.KindFromGalleryIndex(i);
            // One species per scene (Yury 18.09): the spawn config names the kind.
            if (!string.IsNullOrEmpty(treeSpecies))
                kind = PlantTree.ParseKind(treeSpecies);
            int seed = treeSeed >= 0 ? treeSeed : 1 + i * 17;
            float x = originX + i * treeSpacing;

            if (kind == TreeKind.Apple)
            {
                // The apple lane's rig (AppleTree : TreeBase) with the fruit and
                // the 2–5 m crown; 16 RB for one tree on the hero camera is the
                // scene's own choice, Tree.unity already sways its first tree.
                GameObject appleObject = new GameObject("Tree_" + (i + 1) + "_Apple");
                AppleTree apple = appleObject.AddComponent<AppleTree>();
                apple.seed = seed;
                if (treeAppleHeightM > 0f)
                    apple.appleHeightM = treeAppleHeightM;
                appleObject.transform.position = new Vector2(x, PlantTree.RootY(groundY));
                apple.Build();
                appleTrees.Add(apple);
                continue;
            }

            GameObject treeObject = new GameObject("Tree_" + (i + 1) + "_" + kind);
            PlantTree tree = treeObject.AddComponent<PlantTree>();
            tree.kind = kind;
            tree.seed = seed;
            // One Sway is a Human of bodies. A row is a gallery: first sways.
            if (treeCount > 1 && i > 0)
                tree.rig = TreeRig.Static;
            treeObject.transform.position = new Vector2(x, PlantTree.RootY(groundY));
            tree.BuildTree();
            trees.Add(tree);
        }
    }

    private void CreateFlowers()
    {
        if (flowerCount < 1) return;
        // World / Level1 flowers are the yard picture, not this gallery.
        // Leftover Inspector flowerCount=1 used to plant a bed on the hero camera.
        if (levelGenerator != null)
            return;

        float groundY = LevelGenerator.DefaultSurfaceY;
        // Absolute X in the plant gallery. A human shifts the bed; trees do not.
        float originX = flowerOffsetX;
        if (humans.Count > 0)
            originX = humans[0].transform.position.x + flowerOffsetX;

        for (int i = 0; i < flowerCount; i++)
        {
            FlowerKind kind = PlantFlower.KindFromIndex(i);
            GameObject flowerObject = new GameObject("Flower_" + (i + 1) + "_" + kind);
            PlantFlower flower = (PlantFlower)flowerObject.AddComponent(PlantFlower.ComponentType(kind));
            flower.kind = kind;
            flower.seed = 1 + i * 11;
            flowerObject.transform.position = new Vector2(
                originX + i * flowerSpacing,
                PlantFlower.RootY(groundY));
            flower.BuildFlower();
            flowers.Add(flower);
        }
    }

    private void SeatBirdsOnTrees()
    {
        if (trees.Count == 0 || birds.Count == 0) return;
        Bird[] pack = birds.ToArray();
        Bird.GhostTreeWood(pack, trees.ToArray());
        Vector2[] slots = trees[0].GetPerchSlots();
        // BindPerches already PlaceOnSlot / SpreadSeat. Do not SeatBirds:
        // that moves only the host, and the next simulate snaps Body RB
        // back to dirt spawn so NpcRest rest/wake sees the spawn line.
        if (flockDrive != null)
            flockDrive.BindPerches(slots);
        else
            Bird.SpreadSeat(pack, slots);
    }

    // Grove + apple plant in LevelGenerator.Begin. Pads only if a bird sits.
    private void SeatBirdsOnYardGrove()
    {
        PlantTree[] grove = LevelGrove.Planted;
        if (grove == null || grove.Length == 0 || birds.Count == 0)
            return;

        Bird[] crows = CrowsOnly();
        if (crows.Length == 0)
            return;

        PlantTree perch = LevelGrove.Apple != null
            ? LevelGrove.Apple
            : NearestTree(grove, 40f);
        if (perch == null)
            return;
        Bird.GhostTreeWood(crows, grove);
        Vector2[] slots = perch.GetPerchSlots();
        if (flockDrive != null)
            flockDrive.BindPerches(slots);
        else
            Bird.SpreadSeat(crows, slots);
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

    // Scene Campfire only. World / Level1 still plant via LevelGenerator.
    private void CreateCampfire()
    {
        if (!spawnCampfire || levelGenerator != null)
            return;
        Campfire.SpawnAt(Campfire.LabX, -2f);
    }

    // World Test1 showcase (not Level1). Inspector leftover 1/1/1 would
    // skip the OPT-3 pack: far dogs/crows never spawn, so they never rest.
    // Hero Human, 200 Hz, and Level1 birdCount=20 stay as the scene set them.
    private const int WorldShowcaseBirdCount = 6;
    private const int WorldShowcaseChickenCount = 10;
    private const int WorldShowcaseDogCount = 3;
    private const int WorldShowcaseNearbyHenCount = 2;
    private const float WorldShowcaseBirdOffsetX = 39f;
    private const float WorldShowcaseChickenOffsetX = 12f;
    private const float WorldShowcaseChickenSpacing = 0.5f;
    private const float WorldShowcaseChickenParkOffsetX = 32f;
    private const float WorldShowcaseDogOffsetX = 26f;
    private const float WorldShowcaseDogSpacing = 2f;

    private void ApplyWorldShowcasePack()
    {
        if (levelGenerator == null)
            return;
        if (levelGenerator.profile != LevelProfile.Test1)
            return;
        if (levelGenerator.plantDrawnLevel)
            return;

        birdCount = WorldShowcaseBirdCount;
        chickenCount = WorldShowcaseChickenCount;
        dogCount = WorldShowcaseDogCount;
        treeCount = 0;
        flowerCount = 0;
        birdOffsetX = WorldShowcaseBirdOffsetX;
        chickenOffsetX = WorldShowcaseChickenOffsetX;
        chickenSpacing = WorldShowcaseChickenSpacing;
        chickenNearbyCount = WorldShowcaseNearbyHenCount;
        chickenParkOffsetX = WorldShowcaseChickenParkOffsetX;
        dogOffsetX = WorldShowcaseDogOffsetX;
        dogSpacing = WorldShowcaseDogSpacing;
        if (birdKind == null || birdKind.Trim().Length == 0)
            birdKind = "crow";
    }

    // Off-camera dogs, birds, and hedgehogs rest. Distance is the
    // physics body X (NpcRest.probe), not the species host: chest / Body
    // walk while the host stays at spawn. The human never enters the list.
    // Bind also finds LevelGrove plants once.
    // HeadlessTrial never attaches: the stand has no camera follow.
    private void AttachNpcRest()
    {
        if (HeadlessTrial.Active)
            return;
        // SampleScene / Tree / Campfire have no NPC ragdolls. NpcRestEarly
        // would still occupy a FixedUpdate on the hero island. World and
        // Level1 keep LevelGenerator (grove hedgehog is not in dogs/birds).
        if (dogs.Count == 0 && birds.Count == 0 && levelGenerator == null)
            return;
        NpcRest rest = GetComponent<NpcRest>();
        if (rest == null)
            rest = gameObject.AddComponent<NpcRest>();
        // Flock body is 0.14 m. 0.10 m past the half-width puts the sprite
        // off the Level1 right edge (player −8, apple sitters at x>0.9).
        // Do not keep 0.35: that left x=1.2 live (already off-frame).
        rest.flyViewMargin = 0.10f;
        // Hens keep viewMargin 4 m (World x=12 live). Dogs / hedgehog
        // rest 1.5 m past the clip so 18 Dynamic bodies do not ride
        // the hen band while already off-camera.
        rest.quadViewMargin = 1.5f;
        // Flock hens at x=12 rest-decide in LateUpdate (one body).
        // NpcRestEarly stays off that World spawn island. A dog /
        // hedgehog leaving the clip still gets the same physics step.
        rest.nearRestBand = 0.5f;
        rest.physicsRestMinBodies = 8;
        // Walker-band leftover stays Dynamic (npcLiveHens 2). NpcRest
        // drops BirdController / rest loop there until the clip — Sit
        // or Walk/Peck, not only Sit. Probe unsim, still Dynamic.
        // Sleep on dirt was a no-op (Ground wakes StartAwake).
        // drivenExternally skips sensors/flight FixedUpdate.
        // Not energyRest, not BirdSensors / BirdFlight enabled=false.
        // CameraFollow snaps in LateUpdate, after the first FixedUpdate.
        // Bind Tick in Start would otherwise rest against the leftover
        // scene X (BirdFlock50 lens 29.4, flock around 0).
        SnapPlayCameraToFollow();
        // BindFlock clears CameraFollow.bird. Do not keepAwake the whole
        // pack: wanderers past the padded view must rest. Snap already
        // put the lens on the centroid, so in-frame birds stay Dynamic.
        // Level1 follows the human; those 20 apple crows rest per-root.
        // World wander crows have no BirdDrive — pass the pack brain so
        // rest can freeze it (leftover Sit, not a ForceFly on wake).
        // Freeze follows flock-owned live count. A live woodpecker
        // (BirdDrive) must not keep the crow brain writing Fly.
        // Human is never keepAwake. NpcRest drops Muscle/Friction/BodyAero
        // / strike / TreeSpring (dog tail) off-camera; sensors and
        // BirdFlight stay enabled. Rest is Kinematic + simulated=false
        // so leftover sit leaves the 200 Hz contact island. Pose is
        // pinned to physics X before unsim (dirt Transform is stale).
        // Discrete over leftover Paw CCD; colliders and Joint2D off so
        // leftover sit leaves the broadphase. Far leftover sit latches
        // that island once. Physics Tick is only a heavy walker in the
        // near-rest band; flock hens park in LateUpdate.
        // HuntBeak is a world-root query (OverlapCollider
        // needs an attached body; Body child used BirdCore). Bind pairs
        // it once to the bird that Awoke it. Rest parks like Bird
        // ParkHuntSlab: unsim first, Kinematic, StartAwake, trigger,
        // pose at (0, ParkY), then include Nothing. Bird 19:40
        // re-latches Default include after a bodyType flip; Unity 6
        // does not zero an already-Kinematic leftover, so Default
        // ×Default would ride a simulated leak. Live Peck is Dynamic
        // NeverSleep solid — leaving that after unsim is a 200 Hz
        // island. Monitor counts huntBeakRb so npcLiveBodies is not
        // stuck at 0. Wake leaves unsim; BakeHuntSlab owns live
        // simulated, dirt pose, and Default include. Stay Kinematic.
        // Tick does not walk roots.
        // BirdController is off leftover Sit so RestorePeckReach is
        // not 3×/frame. BodyAero is in the rest loop. Rest disables
        // Dog so leftover sit does not keep Dog.LateUpdate. Pass
        // birdPreyMark so Bind latches the Play quarry without a
        // second species scan. Live BakeHuntSlab owns enable.
        rest.Bind(dogs, birds, playCamera, FollowedRestRoot(playFollow), false, flockDrive, birdPreyMark);
        WorldPerformanceMonitor monitor = GetComponent<WorldPerformanceMonitor>();
        if (monitor != null)
            monitor.BindNpcRest(rest);
    }

    // Scene Dog / Bird / Chicken: the followed body stays Dynamic.
    // Human and tree are not in the rest list.
    private static Transform FollowedRestRoot(CameraFollow follow)
    {
        if (follow == null)
            return null;
        if (follow.dog != null)
            return follow.dog.transform;
        if (follow.bird != null)
            return follow.bird.transform;
        if (follow.hedgehog != null)
            return follow.hedgehog.transform;
        return null;
    }

    // Yard, flock overview, and followed-NPC scenes (Dog / Bird / Chicken).
    // SampleScene has no LevelGenerator and no followed rest root — skip
    // so the stand camera keeps humanStandOffset. Chicken extra hens are
    // not keepAwake; a leftover scene X would freeze them for the first
    // physics step before CameraFollow LateUpdate.
    private void SnapPlayCameraToFollow()
    {
        if (playCamera == null)
            return;
        bool flockOverview = cameraFollowsBird && birds.Count >= 10;
        if (levelGenerator == null && !flockOverview && FollowedRestRoot(playFollow) == null)
            return;
        CameraFollow follow = playFollow;
        Vector3 off = follow != null ? follow.offset : new Vector3(0f, 1.1f, -10f);
        Vector2 point;
        if (flockOverview)
        {
            int n = 0;
            float sx = 0f;
            float sy = 0f;
            for (int i = 0; i < birds.Count; i++)
            {
                if (birds[i] == null)
                    continue;
                Vector3 p = birds[i].transform.position;
                // autoSyncTransforms is off. Body RB is the perch / fly
                // pose; the host can still sit on dirt spawn.
                if (birds[i].controller != null && birds[i].controller.BodyRb != null)
                {
                    Vector2 bp = birds[i].controller.BodyRb.position;
                    p.x = bp.x;
                    p.y = bp.y;
                }
                sx += p.x;
                sy += p.y;
                n++;
            }
            if (n < 1)
                return;
            point = new Vector2(sx / n, sy / n);
        }
        else if (follow != null && follow.dog != null)
            point = follow.dog.GetFollowPoint();
        else if (follow != null && follow.bird != null)
        {
            point = follow.bird.transform.position;
            if (follow.bird.controller != null && follow.bird.controller.BodyRb != null)
                point = follow.bird.controller.BodyRb.position;
        }
        else if (follow != null && follow.hedgehog != null)
            point = follow.hedgehog.GetFollowPoint();
        else if (humans.Count > 0 && humans[0] != null)
            point = humans[0].transform.position;
        else if (follow != null && follow.tree != null)
            point = follow.tree.transform.position;
        else
            return;

        playCamera.transform.position = new Vector3(point.x + off.x, point.y + off.y, off.z);
    }

    // Picture only: CameraFollow in LateUpdate, does not touch forces.
    private void SetupCameraFollow()
    {
        Camera cam = Camera.main;
        if (cam == null)
            cam = FindAnyObjectByType<Camera>();
        if (cam == null) return;
        playCamera = cam;
        if (humans.Count == 0 && birds.Count == 0 && trees.Count == 0 && dogs.Count == 0)
        {
            if (spawnCampfire)
                FrameCampfire(cam);
            return;
        }

        cam.backgroundColor = SceneLighting.NightSky;
        cam.orthographic = true;

        CameraFollow follow = cam.GetComponent<CameraFollow>();
        if (follow == null)
            follow = cam.gameObject.AddComponent<CameraFollow>();
        playFollow = follow;

        // Flock in a row: 50×1.2 m ≈ 59 m. A 3.2 frame sees 1–2 birds.
        // Width = 2 * size * aspect; at 16:9 size ≈ 17 covers the row.
        // Do not touch physics or the step — picture only.
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
            // Stand: 1.77 m chain centered with about 1.1 m margin. World/levels stay wider.
            // Bird / Chicken keep the old 4.0 tight frame; do not steal it.
            bool treeOnly = trees.Count > 0 && humans.Count == 0 && birds.Count == 0 && dogs.Count == 0;
            bool dogOnly = dogs.Count > 0 && humans.Count == 0 && birds.Count == 0 && trees.Count == 0;
            bool tight = levelGenerator == null || levelGenerator.profile == LevelProfile.Flat;
            bool humanStand = tight && humans.Count > 0 && birds.Count == 0
                && trees.Count == 0 && dogs.Count == 0;
            if (treeOnly)
                cam.orthographicSize = 4.6f;
            else if (dogOnly)
                cam.orthographicSize = 3.6f;
            else if (humanStand)
            {
                follow.frameFullBody = true;
                follow.offset = follow.humanStandOffset;
                cam.orthographic = true;
                cam.orthographicSize = follow.humanStandOrthoSize;
            }
            else
                cam.orthographicSize = tight ? 4.0f : 5f;
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

    private static void FrameCampfire(Camera cam)
    {
        cam.orthographic = true;
        cam.orthographicSize = 2.6f;
        cam.transform.position = new Vector3(0f, -0.85f, -10f);
        cam.backgroundColor = SceneLighting.NightSky;
    }

    // Picture only. The headless stand never reaches here (Start returns earlier).
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
        // Pole base on the stance line y = −2.
        Vector3 torchPos = new Vector3(root.x + 1.25f, -2f, 0f);
        Torch.Spawn(torchPos);
    }

    private void SetupPlayCapture()
    {
        if (!enablePlayCapture)
            return;
        if (GetComponent<PlayCaptureRecorder>() != null)
            return;

        PlayCaptureRecorder capture = gameObject.AddComponent<PlayCaptureRecorder>();
        // Keep defaults in one place; this log gives the output folder contract.
        Debug.Log("PlayCaptureRecorder: armed (Tools/force-play-capture.flag, F8 toggle, output Trials/Captures).");
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
                // Human–human pairs are already off above. Here — bird–bird
                // and human–bird: otherwise 16+15 bodies stick together at spawn.
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

    // Batch (run-world-perf.ps1) always; Play Mode — only Tools/force-world-perf.flag.
    // Attach before NpcRest so BindNpcRest can cache the component (OnEnable
    // of a late add would miss NpcRest if BindNpcRest did not run).
    private void EnsureWorldPerfMonitor()
    {
        if (humanCount > 1 || levelGenerator == null)
            return;

        Application.targetFrameRate = 60;
        QualitySettings.vSyncCount = 0;
        if (GetComponent<WorldPerformanceMonitor>() == null && ShouldAttachWorldPerfMonitor())
        {
            gameObject.AddComponent<WorldPerformanceMonitor>();
            Debug.Log("WorldPerformanceMonitor: frame capture on (see world_perf.txt).");
        }
    }

    private static bool ShouldAttachWorldPerfMonitor()
    {
        if (Application.isBatchMode)
            return true;

        string flag = System.IO.Path.Combine(Application.dataPath, "..", "Tools", "force-world-perf.flag");
        return System.IO.File.Exists(flag);
    }
}