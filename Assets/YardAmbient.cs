using UnityEngine;

// Picture-only yard life: grass tufts, small saplings, wandering flies / butterflies,
// night fireflies, wind-fall leaves, standing puddles on the wet slabs,
// sparrows on the back-fence rails, laundry on the east-fence line,
// loafing cats on the low rail and courtyard bench, hanging lanterns on
// the path posts, an ajar five-bar gate on the west gate post, sparrow
// nests on the high rails, terracotta flower pots on the slab, birdhouses
// on the east post and two stakes, bird baths on the slab, frogs on the
// slab and puddle rims, dragonflies hovering the bowls and wet grates,
// bees hovering the terracotta blooms, snails climbing the clay,
// lizards basking the post lumber, grasshoppers sitting the dry grass,
// ants crawling the bare dirt, crickets sitting the dry grass at night,
// worms crawling the bare dirt after dusk, water striders skating the
// standing puddles by day, bats circling the hanging lanterns after dusk,
// spider webs in the bay between the back-fence rails, mice darting the
// night slab under the loafing cat and at the fire crumbs, rabbits
// nibbling the day grass, squirrels scampering the birdhouse roofs,
// owls perched on lantern post caps after dusk, raccoons washing the
// night slab at the firewood and berry crumbs, turtles basking the
// puddle banks by day, ducks loafing the water's edge, herons wading
// the puddles, minnows darting under the puddle film, cattails
// standing the wet banks, lily pads sitting the puddle film,
// dandelions standing the dry grass, mushrooms sitting the
// damp shade, ivy climbing the post lumber, ferns sitting
// the shade at a fence foot or grove, clover sitting the
// dry lawn, thistles sitting the dry waste verge,
// plantain sitting the trodden path edge, nettles
// sitting the damp fence-foot waste, docks sitting
// the open disturbed verge, brambles arching the
// fence-bay hedge, hollyhocks standing the sunny
// garden column, roses sitting the sunny garden
// shrub, lavender sitting the sunny herb border,
// irises standing the sunny garden fan, tulips
// standing the sunny garden cup, daffodils
// standing the sunny garden trumpet, daisies
// sitting the sunny garden disk, poppies the
// paper bowl, lilies the star, sunflowers the
// disk-on-a-stalk, violets the damp-shade face,
// crocuses the cool-lawn goblet, bluebells the
// hanging woodland bell, tomatoes the staked
// kitchen vine with hanging fruit, cabbages the
// round kitchen head, pumpkins the ribbed gourd
// on a dirt vine, corn the tall kitchen tassel,
// carrots the tapering kitchen root with ferny
// greens, leeks the blanched kitchen spear with
// an upright leaf fan, beans the kitchen pole
// with hanging pods, onions the round papery
// kitchen bulb in dirt with tubular greens,
// garlic the clustered cloves under paper,
// radishes the small round red root, potatoes
// the irregular brown kitchen tuber in dirt,
// beets the round purple kitchen root, parsnips
// the creamy kitchen cone in dirt, turnips the
// round white globe, rutabagas the larger yellow
// globe, kohlrabis the pale-green swollen stem
// on the slab, celeries the ribbed kitchen stalks,
// fennels the swollen pale bulb on the slab with
// a dill-like crown, asparagus the thin green
// spears from a scaly crown, rhubarbs the thick
// red stalks with a huge wrinkled leaf, kales the
// corrugated blue-green rosette, chards the bright
// stalks with a lobed leaf, spinaches the smooth
// dark-green low rosette, lettuces the loose
// pale-green head.
// No Rigidbody2D, no collider, no faction. This is
// not Insect (beak prey) and not Bird (no lift, no brain). Not Dog. Not
// PlantFlower. Not a Light2D. Not TreeRig.Sway: grove wood stays frozen;
// leaves are recycled ellipses so a Static tree still reads as living.
[DefaultExecutionOrder(125)]
public class YardAmbient : MonoBehaviour
{
    public const float CullX = 22f;
    public const int GrassOrder = 3;
    public const int GateOrder = 3;
    public const int HouseOrder = 4;
    public const int PotOrder = 4;
    public const int BathOrder = 4;
    public const int PuddleOrder = 4;
    public const int FrogOrder = 5;
    public const int HopperOrder = 5;
    public const int AntOrder = 5;
    public const int CricketOrder = 5;
    public const int WormOrder = 5;
    public const int StriderOrder = 5;
    public const int WebOrder = 4;
    public const int SpiderOrder = 5;
    public const int BatOrder = 11;
    public const int LeafOrder = 5;
    public const int ClothesOrder = 5;
    public const int CatOrder = 5;
    public const int MouseOrder = 5;
    public const int RabbitOrder = 5;
    public const int SquirrelOrder = 6;
    public const int RaccoonOrder = 6;
    public const int TurtleOrder = 6;
    public const int DuckOrder = 6;
    public const int HeronOrder = 6;
    public const int MinnowOrder = 4;
    public const int CattailOrder = 5;
    public const int LilyPadOrder = 5;
    public const int DandelionOrder = 4;
    public const int MushroomOrder = 4;
    public const int IvyOrder = 4;
    public const int FernOrder = 4;
    public const int CloverOrder = 4;
    public const int ThistleOrder = 4;
    public const int PlantainOrder = 4;
    public const int NettleOrder = 4;
    public const int DockOrder = 4;
    public const int BrambleOrder = 4;
    public const int HollyhockOrder = 4;
    public const int RoseOrder = 4;
    public const int LavenderOrder = 4;
    public const int IrisOrder = 4;
    public const int TulipOrder = 4;
    public const int DaffodilOrder = 4;
    public const int DaisyOrder = 4;
    public const int PoppyOrder = 4;
    public const int LilyOrder = 4;
    public const int SunflowerOrder = 4;
    public const int VioletOrder = 4;
    public const int CrocusOrder = 4;
    public const int BluebellOrder = 4;
    public const int TomatoOrder = 4;
    public const int CabbageOrder = 4;
    public const int PumpkinOrder = 4;
    public const int CornOrder = 4;
    public const int CarrotOrder = 4;
    public const int LeekOrder = 4;
    public const int BeanOrder = 4;
    public const int OnionOrder = 4;
    public const int GarlicOrder = 4;
    public const int RadishOrder = 4;
    public const int PotatoOrder = 4;
    public const int BeetOrder = 4;
    public const int ParsnipOrder = 4;
    public const int TurnipOrder = 4;
    public const int RutabagaOrder = 4;
    public const int KohlrabiOrder = 4;
    public const int CeleryOrder = 4;
    public const int FennelOrder = 4;
    public const int AsparagusOrder = 4;
    public const int RhubarbOrder = 4;
    public const int KaleOrder = 4;
    public const int ChardOrder = 4;
    public const int SpinachOrder = 4;
    public const int LettuceOrder = 4;
    public const int BroccoliOrder = 4;
    public const int CauliflowerOrder = 4;
    public const int BrusselsOrder = 4;
    public const int ArtichokeOrder = 4;
    public const int EggplantOrder = 4;
    public const int PepperOrder = 4;
    public const int CucumberOrder = 4;
    public const int MelonOrder = 4;
    public const int WatermelonOrder = 4;
    public const int ZucchiniOrder = 4;
    public const int PattypanOrder = 4;
    public const int ButternutOrder = 4;
    public const int AcornOrder = 4;
    public const int SpaghettiOrder = 4;
    public const int DelicataOrder = 4;
    public const int CarnivalOrder = 4;
    public const int SweetDumplingOrder = 4;
    public const int KabochaOrder = 4;
    public const int OwlOrder = 8;
    public const int NestOrder = 5;
    public const int SparrowOrder = 6;
    public const int SnailOrder = 6;
    public const int LizardOrder = 6;
    public const int LanternOrder = 7;
    public const int BugOrder = 7;
    public const int DragonflyOrder = 7;
    public const int BeeOrder = 8;
    public const int FireflyOrder = 9;
    public const int LanternGlowOrder = 10;
    public const int MothOrder = 19;

    private const int TuftsPerChunk = 8;
    private const int BladesPerTuft = 3;
    private const int SaplingsPerChunk = 2;
    private const int FliesPerChunk = 2;
    private const int ButterfliesPerChunk = 1;
    private const int FirefliesPerChunk = 4;
    private const int LeavesPerChunk = 4;
    private const int PuddlesPerChunk = 6;
    private const int SparrowsPerChunk = 3;
    private const int LaundryPerLine = 4;
    private const int CatsPerChunk = 2;
    private const int LanternsPerChunk = 3;
    private const int GatesPerChunk = 1;
    private const int NestsPerChunk = 2;
    private const int PotsPerChunk = 3;
    private const int HousesPerChunk = 2;
    private const int BathsPerChunk = 2;
    private const int FrogsPerChunk = 2;
    private const int DragonfliesPerChunk = 2;
    private const int BeesPerChunk = 3;
    private const int SnailsPerChunk = 3;
    private const int LizardsPerChunk = 2;
    private const int GrasshoppersPerChunk = 2;
    private const int AntsPerChunk = 2;
    private const int CricketsPerChunk = 2;
    private const int WormsPerChunk = 2;
    private const int StridersPerChunk = 2;
    private const int BatsPerChunk = 2;
    private const int WebsPerChunk = 2;
    private const int MicePerChunk = 2;
    private const int RabbitsPerChunk = 2;
    private const int SquirrelsPerChunk = 2;
    private const int RaccoonsPerChunk = 2;
    private const int TurtlesPerChunk = 2;
    private const int DucksPerChunk = 2;
    private const int HeronsPerChunk = 2;
    private const int MinnowsPerChunk = 2;
    private const int CattailsPerChunk = 2;
    private const int LilyPadsPerChunk = 2;
    private const int DandelionsPerChunk = 2;
    private const int MushroomsPerChunk = 2;
    private const int IviesPerChunk = 2;
    private const int FernsPerChunk = 2;
    private const int CloversPerChunk = 2;
    private const int ThistlesPerChunk = 2;
    private const int PlantainsPerChunk = 2;
    private const int NettlesPerChunk = 2;
    private const int DocksPerChunk = 2;
    private const int BramblesPerChunk = 2;
    private const int HollyhocksPerChunk = 2;
    private const int RosesPerChunk = 2;
    private const int LavendersPerChunk = 2;
    private const int IrisesPerChunk = 2;
    private const int TulipsPerChunk = 2;
    private const int DaffodilsPerChunk = 2;
    private const int DaisiesPerChunk = 2;
    private const int PoppiesPerChunk = 2;
    private const int LiliesPerChunk = 2;
    private const int SunflowersPerChunk = 2;
    private const int VioletsPerChunk = 2;
    private const int CrocusesPerChunk = 2;
    private const int BluebellsPerChunk = 2;
    private const int TomatoesPerChunk = 2;
    private const int CabbagesPerChunk = 2;
    private const int PumpkinsPerChunk = 2;
    private const int CornsPerChunk = 2;
    private const int CarrotsPerChunk = 2;
    private const int LeeksPerChunk = 2;
    private const int BeansPerChunk = 2;
    private const int OnionsPerChunk = 2;
    private const int GarlicsPerChunk = 2;
    private const int RadishesPerChunk = 2;
    private const int PotatoesPerChunk = 2;
    private const int BeetsPerChunk = 2;
    private const int ParsnipsPerChunk = 2;
    private const int TurnipsPerChunk = 2;
    private const int RutabagasPerChunk = 2;
    private const int KohlrabisPerChunk = 2;
    private const int CeleriesPerChunk = 2;
    private const int FennelsPerChunk = 2;
    private const int AsparagusPerChunk = 2;
    private const int RhubarbsPerChunk = 2;
    private const int KalesPerChunk = 2;
    private const int ChardsPerChunk = 2;
    private const int SpinachesPerChunk = 2;
    private const int LettucesPerChunk = 2;
    private const int BroccolisPerChunk = 2;
    private const int CauliflowersPerChunk = 2;
    private const int BrusselsPerChunk = 2;
    private const int ArtichokesPerChunk = 2;
    private const int EggplantsPerChunk = 2;
    private const int PeppersPerChunk = 2;
    private const int CucumbersPerChunk = 2;
    private const int MelonsPerChunk = 2;
    private const int WatermelonsPerChunk = 2;
    private const int ZucchinisPerChunk = 2;
    private const int PattypansPerChunk = 2;
    private const int ButternutsPerChunk = 2;
    private const int AcornsPerChunk = 2;
    private const int SpaghettisPerChunk = 2;
    private const int DelicatasPerChunk = 2;
    private const int CarnivalsPerChunk = 2;
    private const int SweetDumplingsPerChunk = 2;
    private const int KabochasPerChunk = 2;
    private const int OwlsPerChunk = 2;
    private const int FireMoths = 3;
    private const float FireflyNightOn = 0.38f;
    private const float DragonflyRoostNight = 0.42f;
    private const float BatHuntNight = 0.42f;
    private const float OwlRoostNight = 0.42f;
    private const float BeeRoostNight = 0.42f;
    private const float SparrowRoostNight = 0.55f;
    private const float LeafWind = 0.28f;

    private Transform[] sway;
    private float[] swayPhase;
    private float[] swayAmp;
    private float[] swaySpeed;
    private Quaternion[] swayRest;

    private Transform[] bugs;
    private Transform[] wingL;
    private Transform[] wingR;
    private Vector2[] bugHome;
    private float[] bugPhase;
    private float[] bugSpeed;
    private float[] bugSpanX;
    private float[] bugSpanY;
    private bool fireMoths;

    private Transform[] fireflies;
    private SpriteRenderer[] fireflySr;
    private Vector2[] fireflyHome;
    private float[] fireflyPhase;
    private float[] fireflySpeed;
    private float[] fireflySpanX;
    private float[] fireflySpanY;
    private float[] fireflyRate;
    private float[] fireflyBaseA;
    private bool firefliesLit;

    private Transform[] leaves;
    private float[] leafPhase;
    private float[] leafFall;
    private float[] leafDrift;
    private float[] leafSpin;
    private float[] leafHomeX;
    private float[] leafCeil;

    private Transform[] puddles;
    private SpriteRenderer[] puddleSr;
    private Transform[] puddleGlint;
    private SpriteRenderer[] puddleGlintSr;
    private float[] puddlePhase;
    private Vector3[] puddleScale;

    private Transform[] sparrows;
    private Transform[] sparrowWings;
    private float[] sparrowLeft;
    private float[] sparrowRight;
    private float[] sparrowY;
    private float[] sparrowPhase;
    private float[] sparrowFace;
    private float[] sparrowTuck;

    private Transform[] cats;
    private Transform[] catTails;
    private SpriteRenderer[] catEyes;
    private float[] catPhase;
    private float[] catFace;
    private Quaternion[] catTailRest;

    private SpriteRenderer[] lanternGlow;
    private float[] lanternPhase;

    private SpriteRenderer[] nestSit;
    private SpriteRenderer[] nestSitHead;
    private Transform[] nestSitXf;
    private float[] nestSitY;
    private float[] nestPhase;

    private SpriteRenderer[] houseSit;

    private Transform[] bathWater;
    private SpriteRenderer[] bathWaterSr;
    private Transform[] bathGlint;
    private SpriteRenderer[] bathGlintSr;
    private Transform[] bathDrinkXf;
    private SpriteRenderer[] bathDrink;
    private SpriteRenderer[] bathDrinkHead;
    private float[] bathDrinkY;
    private float[] bathPhase;
    private Vector3[] bathWaterScale;

    private Transform[] frogs;
    private SpriteRenderer[] frogThroat;
    private float[] frogX;
    private float[] frogY;
    private float[] frogFace;
    private float[] frogPhase;

    private Transform[] dragonflies;
    private Transform[] dragonWingL;
    private Transform[] dragonWingR;
    private float[] dragonX;
    private float[] dragonY;
    private float[] dragonFace;
    private float[] dragonPhase;
    private bool dragonfliesActive = true;

    private Transform[] bees;
    private Transform[] beeWingL;
    private Transform[] beeWingR;
    private float[] beeX;
    private float[] beeY;
    private float[] beeFace;
    private float[] beePhase;
    private bool beesActive = true;

    private Transform[] snails;
    private Transform[] snailAntL;
    private Transform[] snailAntR;
    private float[] snailX;
    private float[] snailY;
    private float[] snailFace;
    private float[] snailPhase;

    private Transform[] lizards;
    private Transform[] lizardTail;
    private float[] lizardX;
    private float[] lizardY;
    private float[] lizardFace;
    private float[] lizardPhase;

    private Transform[] hoppers;
    private Transform[] hopperHind;
    private Transform[] hopperAnt;
    private float[] hopperX;
    private float[] hopperY;
    private float[] hopperFace;
    private float[] hopperPhase;

    private Transform[] ants;
    private Transform[] antAnt;
    private float[] antX;
    private float[] antY;
    private float[] antFace;
    private float[] antPhase;

    private Transform[] crickets;
    private Transform[] cricketWing;
    private Transform[] cricketAnt;
    private float[] cricketX;
    private float[] cricketY;
    private float[] cricketFace;
    private float[] cricketPhase;

    private Transform[] worms;
    private Transform[] wormMid;
    private SpriteRenderer[] wormSheen;
    private float[] wormX;
    private float[] wormY;
    private float[] wormFace;
    private float[] wormPhase;

    private Transform[] striders;
    private Transform[] striderStroke;
    private SpriteRenderer[] striderDimple;
    private float[] striderX;
    private float[] striderY;
    private float[] striderFace;
    private float[] striderPhase;

    private Transform[] bats;
    private Transform[] batWingL;
    private Transform[] batWingR;
    private SpriteRenderer[] batEyes;
    private float[] batX;
    private float[] batY;
    private float[] batPhase;
    private bool batsActive;

    private Transform[] spiders;
    private Transform[] spiderLegs;
    private SpriteRenderer[] webDew;
    private float[] webX;
    private float[] webY;
    private float[] webSpanX;
    private float[] webSpanY;
    private float[] webFace;
    private float[] webPhase;

    private Transform[] mice;
    private Transform[] mouseTails;
    private SpriteRenderer[] mouseEyes;
    private float[] mouseX;
    private float[] mouseY;
    private float[] mouseFace;
    private float[] mousePhase;
    private Quaternion[] mouseTailRest;

    private Transform[] rabbits;
    private Transform[] rabbitHeads;
    private Transform[] rabbitEars;
    private float[] rabbitX;
    private float[] rabbitY;
    private float[] rabbitFace;
    private float[] rabbitPhase;
    private Quaternion[] rabbitHeadRest;
    private Quaternion[] rabbitEarRest;

    private Transform[] squirrels;
    private Transform[] squirrelTails;
    private float[] squirrelX;
    private float[] squirrelY;
    private float[] squirrelFace;
    private float[] squirrelPhase;
    private Quaternion[] squirrelTailRest;

    private Transform[] owls;
    private Transform[] owlHeads;
    private SpriteRenderer[] owlEyes;
    private float[] owlX;
    private float[] owlY;
    private float[] owlFace;
    private float[] owlPhase;
    private Quaternion[] owlHeadRest;

    private Transform[] raccoons;
    private Transform[] raccoonHeads;
    private Transform[] raccoonTails;
    private SpriteRenderer[] raccoonEyes;
    private float[] raccoonX;
    private float[] raccoonY;
    private float[] raccoonFace;
    private float[] raccoonPhase;
    private Quaternion[] raccoonHeadRest;
    private Quaternion[] raccoonTailRest;

    private Transform[] turtles;
    private Transform[] turtleHeads;
    private float[] turtleX;
    private float[] turtleY;
    private float[] turtleFace;
    private float[] turtlePhase;
    private Vector3[] turtleHeadRest;

    private Transform[] ducks;
    private Transform[] duckHeads;
    private float[] duckX;
    private float[] duckY;
    private float[] duckFace;
    private float[] duckPhase;
    private Quaternion[] duckHeadRest;

    private Transform[] herons;
    private Transform[] heronNecks;
    private float[] heronX;
    private float[] heronY;
    private float[] heronFace;
    private float[] heronPhase;
    private Quaternion[] heronNeckRest;

    private Transform[] minnows;
    private Transform[] minnowTails;
    private SpriteRenderer[] minnowFlash;
    private float[] minnowX;
    private float[] minnowY;
    private float[] minnowFace;
    private float[] minnowPhase;

    private Camera followCamera;
    private static Camera sharedCamera;
    private float midX;
    private float chunkLeft;
    private float chunkRight;
    private bool bugsActive = true;

    public static void PlantChunk(int chunkIndex, float chunkWidth, Transform parent)
    {
        if (parent == null || HeadlessTrial.Active)
            return;
        if (ChildNamed(parent, "YardAmbient") != null)
            return;

        GameObject go = new GameObject("YardAmbient");
        go.transform.SetParent(parent, false);
        YardAmbient amb = go.AddComponent<YardAmbient>();
        amb.BuildChunk(chunkIndex, chunkWidth);
    }

    public static Transform PlantFireMoths(Transform fire)
    {
        if (fire == null || HeadlessTrial.Active)
            return null;
        Transform existing = ChildNamed(fire, "FireMoths");
        if (existing != null)
            return existing;

        GameObject go = new GameObject("FireMoths");
        go.transform.SetParent(fire, false);
        go.transform.localPosition = Vector3.zero;
        YardAmbient amb = go.AddComponent<YardAmbient>();
        amb.BuildFireMoths();
        return go.transform;
    }

    void LateUpdate()
    {
        if (followCamera == null)
        {
            if (sharedCamera == null)
                sharedCamera = Camera.main;
            followCamera = sharedCamera;
            if (followCamera == null)
                return;
        }

        float night = DayNightCycle.Night01;
        // Visibility follows the clock even off-camera, so a far chunk does
        // not keep noon flies after dusk. Motion stays culled.
        bool wantBugs = fireMoths || night < 0.62f;
        SetBugsActive(wantBugs);
        bool wantFireflies = !fireMoths && night > FireflyNightOn;
        SetFirefliesActive(wantFireflies);
        bool wantDragonflies = !fireMoths && night < DragonflyRoostNight;
        SetDragonfliesActive(wantDragonflies);
        bool wantBees = !fireMoths && night < BeeRoostNight;
        SetBeesActive(wantBees);
        bool wantBats = !fireMoths && night > BatHuntNight;
        SetBatsActive(wantBats);

        float originX = fireMoths ? transform.position.x : midX;
        float dx = originX - followCamera.transform.position.x;
        if (dx * dx > CullX * CullX)
            return;

        float t = Time.time;
        TickSway(t);
        if (!fireMoths)
        {
            TickLeaves(t);
            TickPuddles(t, night);
            TickSparrows(t, night);
            TickCats(t, night);
            TickLanterns(t, night);
            TickNests(t, night);
            TickHouses(night);
            TickBaths(t, night);
            TickFrogs(t, night);
            if (wantDragonflies)
                TickDragonflies(t);
            if (wantBees)
                TickBees(t);
            TickSnails(t, night);
            TickLizards(t, night);
            TickGrasshoppers(t, night);
            TickAnts(t, night);
            TickCrickets(t, night);
            TickWorms(t, night);
            TickStriders(t, night);
            if (wantBats)
                TickBats(t, night);
            TickWebs(t, night);
            TickMice(t, night);
            TickRabbits(t, night);
            TickSquirrels(t, night);
            TickOwls(t, night);
            TickRaccoons(t, night);
            TickTurtles(t, night);
            TickDucks(t, night);
            TickHerons(t, night);
            TickMinnows(t, night);
        }
        if (wantBugs)
            TickBugs(t);
        if (wantFireflies)
            TickFireflies(t, night);
    }

    private void BuildChunk(int chunkIndex, float chunkWidth)
    {
        fireMoths = false;
        chunkLeft = chunkIndex * chunkWidth;
        chunkRight = chunkLeft + chunkWidth;
        midX = chunkLeft + chunkWidth * 0.5f;
        followCamera = sharedCamera != null ? sharedCamera : Camera.main;
        if (followCamera != null)
            sharedCamera = followCamera;

        int swayCount = TuftsPerChunk * BladesPerTuft + SaplingsPerChunk + LaundryPerLine
            + LanternsPerChunk + GatesPerChunk + NestsPerChunk + PotsPerChunk + HousesPerChunk
            + CattailsPerChunk + LilyPadsPerChunk + DandelionsPerChunk
            + MushroomsPerChunk + IviesPerChunk + FernsPerChunk
            + CloversPerChunk + ThistlesPerChunk + PlantainsPerChunk
            + NettlesPerChunk + DocksPerChunk + BramblesPerChunk
            + HollyhocksPerChunk + RosesPerChunk + LavendersPerChunk
            + IrisesPerChunk + TulipsPerChunk + DaffodilsPerChunk
            + DaisiesPerChunk + PoppiesPerChunk + LiliesPerChunk
            + SunflowersPerChunk + VioletsPerChunk + CrocusesPerChunk
            + BluebellsPerChunk + TomatoesPerChunk + CabbagesPerChunk
            + PumpkinsPerChunk + CornsPerChunk + CarrotsPerChunk
            + LeeksPerChunk + BeansPerChunk + OnionsPerChunk
            + GarlicsPerChunk + RadishesPerChunk + PotatoesPerChunk
            + BeetsPerChunk + ParsnipsPerChunk + TurnipsPerChunk
            + RutabagasPerChunk + KohlrabisPerChunk
            + CeleriesPerChunk + FennelsPerChunk
            + AsparagusPerChunk + RhubarbsPerChunk
            + KalesPerChunk + ChardsPerChunk
            + SpinachesPerChunk + LettucesPerChunk
            + BroccolisPerChunk + CauliflowersPerChunk
            + BrusselsPerChunk + ArtichokesPerChunk
            + EggplantsPerChunk + PeppersPerChunk
            + CucumbersPerChunk + MelonsPerChunk
            + WatermelonsPerChunk + ZucchinisPerChunk
            + PattypansPerChunk + ButternutsPerChunk
            + AcornsPerChunk + SpaghettisPerChunk
            + DelicatasPerChunk + CarnivalsPerChunk
            + SweetDumplingsPerChunk + KabochasPerChunk;
        AllocSway(swayCount);

        int bugCount = FliesPerChunk + ButterfliesPerChunk;
        AllocBugs(bugCount);
        AllocFireflies(FirefliesPerChunk);
        AllocLeaves(LeavesPerChunk);
        AllocPuddles(PuddlesPerChunk);
        AllocSparrows(SparrowsPerChunk);
        AllocCats(CatsPerChunk);
        AllocLanterns(LanternsPerChunk);
        AllocNests(NestsPerChunk);
        AllocHouses(HousesPerChunk);
        AllocBaths(BathsPerChunk);
        AllocFrogs(FrogsPerChunk);
        AllocDragonflies(DragonfliesPerChunk);
        AllocBees(BeesPerChunk);
        AllocSnails(SnailsPerChunk);
        AllocLizards(LizardsPerChunk);
        AllocGrasshoppers(GrasshoppersPerChunk);
        AllocAnts(AntsPerChunk);
        AllocCrickets(CricketsPerChunk);
        AllocWorms(WormsPerChunk);
        AllocStriders(StridersPerChunk);
        AllocBats(BatsPerChunk);
        AllocWebs(WebsPerChunk);
        AllocMice(MicePerChunk);
        AllocRabbits(RabbitsPerChunk);
        AllocSquirrels(SquirrelsPerChunk);
        AllocOwls(OwlsPerChunk);
        AllocRaccoons(RaccoonsPerChunk);
        AllocTurtles(TurtlesPerChunk);
        AllocDucks(DucksPerChunk);
        AllocHerons(HeronsPerChunk);
        AllocMinnows(MinnowsPerChunk);

        int swayWrite = 0;
        int seed = Mix(chunkIndex, 9041);

        for (int tuft = 0; tuft < TuftsPerChunk; tuft++)
        {
            float u = (tuft + 0.37f) / TuftsPerChunk;
            float x = chunkLeft + u * chunkWidth + (Hash01(seed, tuft) - 0.5f) * 0.55f;
            if (x < chunkLeft || x >= chunkRight)
                continue;
            if (NearFurniture(x) || LevelTest1.IsWetAt(x))
                continue;

            float surface = LevelTest1.SurfaceAt(x);
            float tuftPhase = Hash01(seed, tuft + 40) * 6.28f;
            for (int b = 0; b < BladesPerTuft && swayWrite < sway.Length; b++)
            {
                float bx = x + (b - 1) * 0.045f;
                float h = 0.10f + Hash01(seed, tuft * 3 + b) * 0.08f;
                float w = 0.016f + Hash01(seed, tuft * 5 + b + 7) * 0.008f;
                Color tint = BladeTint(Hash01(seed, tuft + b * 11));
                Transform pivot = MakePivot(
                    "Blade",
                    new Vector2(w, h),
                    new Vector3(bx, surface, 0f),
                    tint,
                    GrassOrder);
                BindSway(swayWrite++, pivot, tuftPhase + b * 0.31f, 4.5f + Hash01(seed, b) * 3f, 1.05f + Hash01(seed, tuft + 90) * 0.35f);
            }
        }

        int saplings = 0;
        for (int i = 0; i < 8 && saplings < SaplingsPerChunk && swayWrite < sway.Length; i++)
        {
            float x = chunkLeft + (0.18f + Hash01(seed, 200 + i) * 0.64f) * chunkWidth;
            if (NearFurniture(x) || NearGroveTree(x) || LevelTest1.IsWetAt(x))
                continue;
            float surface = LevelTest1.SurfaceAt(x);
            Transform sapling = MakeSapling(x, surface, seed + i);
            BindSway(swayWrite++, sapling, Hash01(seed, 300 + i) * 6.28f, 2.2f, 0.55f);
            saplings++;
        }

        PlantLaundry(seed, ref swayWrite);

        int bugWrite = 0;
        for (int i = 0; i < FliesPerChunk && bugWrite < bugs.Length; i++)
        {
            float x = chunkLeft + (0.2f + Hash01(seed, 400 + i) * 0.6f) * chunkWidth;
            float surface = LevelTest1.SurfaceAt(x);
            Transform fly = MakeBug(
                "YardFly",
                new Vector2(0.035f, 0.022f),
                new Vector3(x, surface + 0.28f, 0f),
                new Color(0.18f, 0.16f, 0.14f, 0.85f),
                BugOrder,
                true);
            BindBug(bugWrite++, fly, null, null, x, surface + 0.32f, 1.15f + Hash01(seed, 410 + i), 1.25f, 0.16f);
        }

        if (bugWrite < bugs.Length)
        {
            float x = chunkLeft + (0.25f + Hash01(seed, 500) * 0.5f) * chunkWidth;
            float surface = LevelTest1.SurfaceAt(x);
            Color wing = ButterflyTint(Hash01(seed, 508));
            Transform body = MakeButterfly(x, surface + 0.42f, wing, out Transform left, out Transform right);
            BindBug(
                bugWrite++,
                body,
                left,
                right,
                x,
                surface + 0.48f,
                0.48f + Hash01(seed, 512) * 0.22f,
                1.45f,
                0.24f);
        }

        if (bugWrite < bugs.Length)
            TrimBugs(bugWrite);

        int flyWrite = 0;
        for (int i = 0; i < FirefliesPerChunk && flyWrite < fireflies.Length; i++)
        {
            float x = chunkLeft + (0.12f + Hash01(seed, 600 + i) * 0.76f) * chunkWidth;
            if (x < chunkLeft || x >= chunkRight)
                continue;
            if (NearFurniture(x))
                continue;
            float surface = LevelTest1.SurfaceAt(x);
            float s = 0.028f + Hash01(seed, 610 + i) * 0.018f;
            Transform glow = MakeFirefly(
                new Vector2(s, s),
                new Vector3(x, surface + 0.22f + Hash01(seed, 620 + i) * 0.20f, 0f));
            BindFirefly(
                flyWrite++,
                glow,
                x,
                surface + 0.28f + Hash01(seed, 630 + i) * 0.16f,
                0.22f + Hash01(seed, 640 + i) * 0.18f,
                1.10f + Hash01(seed, 650 + i) * 0.90f,
                0.18f + Hash01(seed, 660 + i) * 0.12f,
                0.55f + Hash01(seed, 670 + i) * 0.85f,
                0.55f + Hash01(seed, 680 + i) * 0.40f);
        }

        if (flyWrite < fireflies.Length)
            TrimFireflies(flyWrite);
        SetFirefliesActive(false);

        int leafWrite = 0;
        for (int i = 0; i < LeavesPerChunk && leafWrite < leaves.Length; i++)
        {
            float x = chunkLeft + (0.10f + Hash01(seed, 700 + i) * 0.80f) * chunkWidth;
            if (x < chunkLeft || x >= chunkRight)
                continue;
            if (NearFurniture(x))
                continue;
            float surface = LevelTest1.SurfaceAt(x);
            float w = 0.046f + Hash01(seed, 710 + i) * 0.022f;
            float h = 0.026f + Hash01(seed, 720 + i) * 0.014f;
            Transform leaf = MakeLeaf(
                new Vector2(w, h),
                new Vector3(x, surface + 0.70f, 0f),
                LeafTint(Hash01(seed, 730 + i)));
            BindLeaf(
                leafWrite++,
                leaf,
                x,
                surface + 0.55f + Hash01(seed, 740 + i) * 0.95f,
                0.11f + Hash01(seed, 750 + i) * 0.10f,
                0.22f + Hash01(seed, 760 + i) * 0.38f,
                1.55f + Hash01(seed, 770 + i) * 1.85f);
        }

        if (leafWrite < leaves.Length)
            TrimLeaves(leafWrite);

        PlantPuddles(seed);
        PlantSparrows(seed);
        PlantCats(seed);
        PlantLanterns(seed, ref swayWrite);
        PlantGates(seed, ref swayWrite);
        PlantNests(seed, ref swayWrite);
        PlantPots(seed, ref swayWrite);
        PlantHouses(seed, ref swayWrite);
        PlantBaths(seed);
        PlantFrogs(seed);
        PlantDragonflies(seed);
        PlantBees(seed);
        PlantSnails(seed);
        PlantLizards(seed);
        PlantGrasshoppers(seed);
        PlantAnts(seed);
        PlantCrickets(seed);
        PlantWorms(seed);
        PlantStriders(seed);
        PlantBats(seed);
        PlantWebs(seed);
        PlantMice(seed);
        PlantRabbits(seed);
        PlantSquirrels(seed);
        PlantOwls(seed);
        PlantRaccoons(seed);
        PlantTurtles(seed);
        PlantDucks(seed);
        PlantHerons(seed);
        PlantMinnows(seed);
        PlantCattails(seed, ref swayWrite);
        PlantLilyPads(seed, ref swayWrite);
        PlantDandelions(seed, ref swayWrite);
        PlantMushrooms(seed, ref swayWrite);
        PlantIvies(seed, ref swayWrite);
        PlantFerns(seed, ref swayWrite);
        PlantClovers(seed, ref swayWrite);
        PlantThistles(seed, ref swayWrite);
        PlantPlantains(seed, ref swayWrite);
        PlantNettles(seed, ref swayWrite);
        PlantDocks(seed, ref swayWrite);
        PlantBrambles(seed, ref swayWrite);
        PlantHollyhocks(seed, ref swayWrite);
        PlantRoses(seed, ref swayWrite);
        PlantLavenders(seed, ref swayWrite);
        PlantIrises(seed, ref swayWrite);
        PlantTulips(seed, ref swayWrite);
        PlantDaffodils(seed, ref swayWrite);
        PlantDaisies(seed, ref swayWrite);
        PlantPoppies(seed, ref swayWrite);
        PlantLilies(seed, ref swayWrite);
        PlantSunflowers(seed, ref swayWrite);
        PlantViolets(seed, ref swayWrite);
        PlantCrocuses(seed, ref swayWrite);
        PlantBluebells(seed, ref swayWrite);
        PlantTomatoes(seed, ref swayWrite);
        PlantCabbages(seed, ref swayWrite);
        PlantPumpkins(seed, ref swayWrite);
        PlantCorns(seed, ref swayWrite);
        PlantCarrots(seed, ref swayWrite);
        PlantLeeks(seed, ref swayWrite);
        PlantBeans(seed, ref swayWrite);
        PlantOnions(seed, ref swayWrite);
        PlantGarlics(seed, ref swayWrite);
        PlantRadishes(seed, ref swayWrite);
        PlantPotatoes(seed, ref swayWrite);
        PlantBeets(seed, ref swayWrite);
        PlantParsnips(seed, ref swayWrite);
        PlantTurnips(seed, ref swayWrite);
        PlantRutabagas(seed, ref swayWrite);
        PlantKohlrabis(seed, ref swayWrite);
        PlantCeleries(seed, ref swayWrite);
        PlantFennels(seed, ref swayWrite);
        PlantAsparagus(seed, ref swayWrite);
        PlantRhubarbs(seed, ref swayWrite);
        PlantKales(seed, ref swayWrite);
        PlantChards(seed, ref swayWrite);
        PlantSpinaches(seed, ref swayWrite);
        PlantLettuces(seed, ref swayWrite);
        PlantBroccolis(seed, ref swayWrite);
        PlantCauliflowers(seed, ref swayWrite);
        PlantBrussels(seed, ref swayWrite);
        PlantArtichokes(seed, ref swayWrite);
        PlantEggplants(seed, ref swayWrite);
        PlantPeppers(seed, ref swayWrite);
        PlantCucumbers(seed, ref swayWrite);
        PlantMelons(seed, ref swayWrite);
        PlantWatermelons(seed, ref swayWrite);
        PlantZucchinis(seed, ref swayWrite);
        PlantPattypans(seed, ref swayWrite);
        PlantButternuts(seed, ref swayWrite);
        PlantAcorns(seed, ref swayWrite);
        PlantSpaghettis(seed, ref swayWrite);
        PlantDelicatas(seed, ref swayWrite);
        PlantCarnivals(seed, ref swayWrite);
        PlantSweetDumplings(seed, ref swayWrite);
        PlantKabochas(seed, ref swayWrite);
        if (swayWrite < sway.Length)
            TrimSway(swayWrite);
    }

    private void BuildFireMoths()
    {
        fireMoths = true;
        midX = transform.position.x;
        followCamera = sharedCamera != null ? sharedCamera : Camera.main;
        if (followCamera != null)
            sharedCamera = followCamera;
        AllocSway(0);
        AllocBugs(FireMoths);
        AllocFireflies(0);
        AllocLeaves(0);
        AllocPuddles(0);
        AllocSparrows(0);
        AllocCats(0);
        AllocLanterns(0);

        for (int i = 0; i < FireMoths; i++)
        {
            float phase = i * 2.094f;
            Transform moth = MakeBug(
                "YardMoth",
                new Vector2(0.040f, 0.024f),
                new Vector3(Mathf.Cos(phase) * 0.22f, 0.48f, 0f),
                new Color(0.72f, 0.62f, 0.42f, 0.80f),
                MothOrder,
                false);
            BindBug(i, moth, null, null, moth.localPosition.x, moth.localPosition.y, 1.1f + i * 0.17f, 0.28f, 0.10f);
        }
    }

    private void TickSway(float t)
    {
        if (sway == null)
            return;
        for (int i = 0; i < sway.Length; i++)
        {
            Transform pivot = sway[i];
            if (pivot == null)
                continue;
            float a = Mathf.Sin(t * swaySpeed[i] + swayPhase[i]) * swayAmp[i];
            pivot.localRotation = swayRest[i] * Quaternion.Euler(0f, 0f, a);
        }
    }

    private void SetBugsActive(bool on)
    {
        if (bugsActive == on || bugs == null)
            return;
        bugsActive = on;
        for (int i = 0; i < bugs.Length; i++)
        {
            if (bugs[i] != null)
                bugs[i].gameObject.SetActive(on);
        }
    }

    private void SetDragonfliesActive(bool on)
    {
        if (dragonfliesActive == on || dragonflies == null)
            return;
        dragonfliesActive = on;
        for (int i = 0; i < dragonflies.Length; i++)
        {
            if (dragonflies[i] != null)
                dragonflies[i].gameObject.SetActive(on);
        }
    }

    private void SetBeesActive(bool on)
    {
        if (beesActive == on || bees == null)
            return;
        beesActive = on;
        for (int i = 0; i < bees.Length; i++)
        {
            if (bees[i] != null)
                bees[i].gameObject.SetActive(on);
        }
    }

    private void SetBatsActive(bool on)
    {
        if (batsActive == on || bats == null)
            return;
        batsActive = on;
        for (int i = 0; i < bats.Length; i++)
        {
            if (bats[i] != null)
                bats[i].gameObject.SetActive(on);
        }
    }

    private void TickBugs(float t)
    {
        if (bugs == null)
            return;
        for (int i = 0; i < bugs.Length; i++)
        {
            Transform root = bugs[i];
            if (root == null)
                continue;

            if (fireMoths)
            {
                float ang = t * bugSpeed[i] + bugPhase[i];
                root.localPosition = new Vector3(
                    Mathf.Cos(ang) * bugSpanX[i],
                    0.42f + Mathf.Sin(ang * 2.1f) * bugSpanY[i],
                    0f);
            }
            else
            {
                // Two slow frequencies so a fly travels the chunk instead of
                // bobbing on one spot. Floor is the authored slab, not a ray.
                float w = t * bugSpeed[i] * 0.31f + bugPhase[i];
                float px = bugHome[i].x
                    + Mathf.Sin(w) * bugSpanX[i]
                    + Mathf.Sin(w * 0.47f + 1.1f) * bugSpanX[i] * 0.55f;
                float py = bugHome[i].y
                    + Mathf.Sin(w * 1.37f + bugPhase[i] * 0.71f) * bugSpanY[i];
                if (px < chunkLeft + 0.2f)
                    px = chunkLeft + 0.2f;
                if (px > chunkRight - 0.2f)
                    px = chunkRight - 0.2f;
                float floor = LevelTest1.SurfaceAt(px) + 0.12f;
                if (py < floor)
                    py = floor;
                root.position = new Vector3(px, py, 0f);
            }

            Transform left = wingL[i];
            Transform right = wingR[i];
            if (left == null || right == null)
                continue;
            float flap = Mathf.Sin(t * 9.5f + bugPhase[i]) * 28f;
            left.localRotation = Quaternion.Euler(0f, 0f, 18f + flap);
            right.localRotation = Quaternion.Euler(0f, 0f, -18f - flap);
        }
    }

    private void SetFirefliesActive(bool on)
    {
        if (firefliesLit == on || fireflies == null)
            return;
        firefliesLit = on;
        for (int i = 0; i < fireflies.Length; i++)
        {
            if (fireflies[i] != null)
                fireflies[i].gameObject.SetActive(on);
        }
    }

    private void TickFireflies(float t, float night)
    {
        if (fireflies == null)
            return;
        for (int i = 0; i < fireflies.Length; i++)
        {
            Transform root = fireflies[i];
            SpriteRenderer sr = fireflySr[i];
            if (root == null || sr == null)
                continue;

            float w = t * fireflySpeed[i] * 0.28f + fireflyPhase[i];
            float px = fireflyHome[i].x
                + Mathf.Sin(w) * fireflySpanX[i]
                + Mathf.Sin(w * 0.41f + 0.8f) * fireflySpanX[i] * 0.40f;
            float py = fireflyHome[i].y
                + Mathf.Sin(w * 1.19f + fireflyPhase[i] * 0.6f) * fireflySpanY[i];
            if (px < chunkLeft + 0.15f)
                px = chunkLeft + 0.15f;
            if (px > chunkRight - 0.15f)
                px = chunkRight - 0.15f;
            float floor = LevelTest1.SurfaceAt(px) + 0.10f;
            if (py < floor)
                py = floor;
            root.position = new Vector3(px, py, 0f);

            // Half-cycle dark: a firefly reads as a blink, not a lamp.
            float s = Mathf.Sin(t * fireflyRate[i] + fireflyPhase[i]);
            float pulse = s > 0f ? s * s : 0f;
            Color c = sr.color;
            c.a = night * fireflyBaseA[i] * pulse;
            sr.color = c;
            sr.enabled = c.a > 0.03f;
        }
    }

    // Recycle: u wraps 0 at the ceiling, 1 at the slab. Wind sign matches
    // the cloud belt and campfire smoke (+X). Not TreeRig.Sway.
    private void TickLeaves(float t)
    {
        if (leaves == null)
            return;
        for (int i = 0; i < leaves.Length; i++)
        {
            Transform root = leaves[i];
            if (root == null)
                continue;

            float u = t * leafFall[i] + leafPhase[i];
            u -= Mathf.Floor(u);

            float px = leafHomeX[i]
                + u * (leafDrift[i] + LeafWind)
                + Mathf.Sin(t * 0.85f + leafPhase[i]) * 0.16f;
            if (px < chunkLeft + 0.12f)
                px = chunkLeft + 0.12f;
            if (px > chunkRight - 0.12f)
                px = chunkRight - 0.12f;

            float floor = LevelTest1.SurfaceAt(px) + 0.02f;
            float py = leafCeil[i] + (floor - leafCeil[i]) * u;
            root.position = new Vector3(px, py, 0f);
            root.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * leafSpin[i] + leafPhase[i]) * 58f);
        }
    }

    // DrainDip / RainGutter: a cooler dirt slab plus a grate still reads as
    // a curb. Flat Unlit ellipses sit on the surface (no collider — a box
    // here is a wall for the feet) and borrow the yard clock so noon is
    // sky-blue, dusk blushes, night goes dark. Ripple is scale, not wind.
    private void PlantPuddles(int seed)
    {
        int write = 0;
        if (puddles == null)
            return;

        LevelTest1.Slab[] slabs = LevelTest1.Slabs;
        for (int s = 0; s < slabs.Length && write < puddles.Length; s++)
        {
            LevelTest1.Slab slab = slabs[s];
            if (slab.name != "DrainDip" && slab.name != "RainGutter")
                continue;

            float a = Mathf.Max(slab.left, chunkLeft);
            float b = Mathf.Min(slab.left + slab.width, chunkRight);
            if (b - a < 0.55f)
                continue;

            float surface = slab.surfaceY;
            // Flank the grate: a dip-wide skip from NearDecor would eat the
            // whole 4 m slab (grate 1.2–1.6 m + 0.55 m extra).
            float u0 = 0.12f;
            float u1 = 0.28f;
            float u2 = 0.86f;
            for (int i = 0; i < 3 && write < puddles.Length; i++)
            {
                float u = i == 0 ? u0 : (i == 1 ? u1 : u2);
                float x = slab.left + u * slab.width;
                if (x < a + 0.16f || x > b - 0.16f)
                    continue;
                if (LevelTest1.NearDecor(x, 0.22f))
                    continue;

                float w = 0.72f + Hash01(seed, 800 + s * 10 + i) * 0.55f;
                float h = 0.048f + Hash01(seed, 810 + s * 10 + i) * 0.022f;
                Transform root = MakePuddle(
                    new Vector2(w, h),
                    new Vector3(x, surface + h * 0.38f, 0f),
                    out Transform glint);
                BindPuddle(write++, root, glint, Hash01(seed, 820 + s * 10 + i) * 6.28f);
            }
        }

        if (write < puddles.Length)
            TrimPuddles(write);
    }

    private void TickPuddles(float t, float night)
    {
        if (puddles == null)
            return;

        float dusk = 1f - Mathf.Abs(night * 2f - 1f);
        if (dusk < 0f)
            dusk = 0f;
        Color body = Color.Lerp(
            new Color(0.22f, 0.46f, 0.58f, 0.46f),
            new Color(0.08f, 0.11f, 0.18f, 0.58f),
            night);
        if (dusk > 0.01f)
            body = Color.Lerp(body, new Color(0.62f, 0.30f, 0.16f, 0.50f), dusk * 0.55f);

        Color glint = body;
        glint.r = Mathf.Clamp01(body.r + 0.28f);
        glint.g = Mathf.Clamp01(body.g + 0.24f);
        glint.b = Mathf.Clamp01(body.b + 0.18f);
        glint.a = body.a * 0.55f;

        for (int i = 0; i < puddles.Length; i++)
        {
            Transform root = puddles[i];
            SpriteRenderer sr = puddleSr[i];
            if (root == null || sr == null)
                continue;

            sr.color = body;
            float wobble = Mathf.Sin(t * 1.05f + puddlePhase[i]) * 0.035f;
            Vector3 rest = puddleScale[i];
            root.localScale = new Vector3(rest.x * (1f + wobble), rest.y * (1f - wobble * 0.55f), 1f);

            Transform shine = puddleGlint[i];
            SpriteRenderer gsr = puddleGlintSr[i];
            if (shine == null || gsr == null)
                continue;
            gsr.color = glint;
            Vector3 lp = shine.localPosition;
            lp.x = Mathf.Sin(t * 0.62f + puddlePhase[i]) * 0.12f;
            shine.localPosition = lp;
        }
    }

    // East fence bay is the opening shot for World (x=0) and Level1 (x=−8).
    // Sparrows hop the high rail; the posts still read as empty sticks. A
    // short line at post-top height hangs cloth above the rail so hop and
    // roost stay clear. Same LateUpdate sway as the grass — not TreeRig.Sway.
    private void PlantLaundry(int seed, ref int swayWrite)
    {
        if (sway == null)
            return;

        LevelTest1.Clothesline[] lines = LevelTest1.Clotheslines;
        for (int L = 0; L < lines.Length; L++)
        {
            LevelTest1.Clothesline line = lines[L];
            float x0 = line.x0;
            float x1 = line.x1;
            if (x1 < x0)
            {
                float tmp = x0;
                x0 = x1;
                x1 = tmp;
            }

            float a = Mathf.Max(x0, chunkLeft);
            float b = Mathf.Min(x1, chunkRight);
            if (b - a < 1.4f)
                continue;

            MakeClothesLine(new Vector3((a + b) * 0.5f, line.y, 0f), b - a);

            for (int i = 0; i < LaundryPerLine && swayWrite < sway.Length; i++)
            {
                float u = 0.16f + i * 0.22f;
                float x = x0 + u * (x1 - x0);
                if (x < a + 0.12f || x > b - 0.12f)
                    continue;

                Vector2 size = LaundrySize(i);
                Transform item = MakeHang(
                    "YardLaundry",
                    size,
                    new Vector3(x, line.y, 0f),
                    LaundryTint(Hash01(seed, 1000 + L * 10 + i)),
                    ClothesOrder);
                BindSway(
                    swayWrite++,
                    item,
                    Hash01(seed, 1010 + L * 10 + i) * 6.28f,
                    3.2f + Hash01(seed, 1020 + i) * 1.6f,
                    0.78f + Hash01(seed, 1030 + i) * 0.28f);
            }
        }
    }

    // Back fence is the only furniture in the Level1 opening shot. Sky
    // flocks are distant silhouettes; flies wander grass. These sit on the
    // high rail (picture, no collider) so the posts read as a living yard.
    // Night01 tucks them — they do not take off (that would be Bird).
    private void PlantSparrows(int seed)
    {
        if (sparrows == null)
            return;

        int write = 0;
        LevelTest1.FenceRail[] rails = LevelTest1.FenceRails;
        for (int r = 0; r < rails.Length && write < sparrows.Length; r++)
        {
            LevelTest1.FenceRail rail = rails[r];
            float a = Mathf.Max(rail.x - rail.width * 0.5f, chunkLeft);
            float b = Mathf.Min(rail.x + rail.width * 0.5f, chunkRight);
            if (b - a < 0.55f)
                continue;

            int count = b - a > 3.4f ? 2 : 1;
            for (int i = 0; i < count && write < sparrows.Length; i++)
            {
                float u = (i + 0.32f) / count;
                float x = a + u * (b - a);
                x += (Hash01(seed, 900 + r * 10 + i) - 0.5f) * 0.22f;
                if (x < a + 0.16f || x > b - 0.16f)
                    continue;

                Transform root = MakeSparrow(new Vector3(x, rail.topY + 0.018f, 0f), out Transform wing);
                BindSparrow(write++, root, wing, a + 0.16f, b - 0.16f, rail.topY + 0.018f, Hash01(seed, 910 + r * 10 + i) * 6.28f);
            }
        }

        if (write < sparrows.Length)
            TrimSparrows(write);
    }

    private void TickSparrows(float t, float night)
    {
        if (sparrows == null)
            return;

        bool roost = night >= SparrowRoostNight;
        for (int i = 0; i < sparrows.Length; i++)
        {
            Transform root = sparrows[i];
            if (root == null)
                continue;

            float left = sparrowLeft[i];
            float right = sparrowRight[i];
            float span = right - left;
            if (span < 0.08f)
                continue;

            float beat = t * 0.17f + sparrowPhase[i];
            int slot = (int)beat;
            if (slot < 0)
                slot = -slot;
            float frac = beat - Mathf.Floor(beat);
            float u0 = 0.12f + Hash01(slot, 3) * 0.76f;
            float u1 = 0.12f + Hash01(slot + 1, 3) * 0.76f;
            float from = left + u0 * span;
            float to = left + u1 * span;
            if (roost)
            {
                Vector3 p = root.position;
                p.y = sparrowY[i];
                root.position = p;
            }
            else
            {
                float hop = 0f;
                float x = from;
                float face = to >= from ? 1f : -1f;
                if (frac > 0.78f)
                {
                    float h = (frac - 0.78f) / 0.22f;
                    x = from + (to - from) * h;
                    hop = Mathf.Sin(h * 3.1416f) * 0.042f;
                }

                if (x < left)
                    x = left;
                if (x > right)
                    x = right;
                root.position = new Vector3(x, sparrowY[i] + hop, 0f);

                if (face != sparrowFace[i])
                {
                    sparrowFace[i] = face;
                    root.localScale = new Vector3(face, 1f, 1f);
                }
            }

            // Local +X is the beak; scale.x already mirrors facing, so tuck
            // is always a clockwise nod onto the rail.
            float tuck = roost ? -24f : 0f;
            if (tuck != sparrowTuck[i])
            {
                sparrowTuck[i] = tuck;
                root.localRotation = Quaternion.Euler(0f, 0f, tuck);
            }

            Transform wing = sparrowWings[i];
            if (wing == null)
                continue;
            float flap = (!roost && frac > 0.78f) ? Mathf.Sin((frac - 0.78f) * 48f) * 18f : 6f;
            wing.localRotation = Quaternion.Euler(0f, 0f, flap);
        }
    }

    // Sparrows hop the high rail; the low board and courtyard bench stayed
    // empty wood. A loaf (picture, no collider) reads as a yard, not a Dog.
    // Night Unlit slits; tail is the same LateUpdate sway idea as grass.
    private void PlantCats(int seed)
    {
        if (cats == null)
            return;

        int write = 0;
        LevelTest1.CatPerch[] perches = LevelTest1.CatPerches;
        for (int i = 0; i < perches.Length && write < cats.Length; i++)
        {
            LevelTest1.CatPerch perch = perches[i];
            if (perch.x < chunkLeft + 0.12f || perch.x > chunkRight - 0.12f)
                continue;

            Transform root = MakeCat(
                new Vector3(perch.x, perch.y, 0f),
                perch.face,
                Hash01(seed, 1100 + i),
                out Transform tail,
                out SpriteRenderer eye);
            BindCat(write++, root, tail, eye, Hash01(seed, 1110 + i) * 6.28f, perch.face);
        }

        if (write < cats.Length)
            TrimCats(write);
    }

    private void TickCats(float t, float night)
    {
        if (cats == null)
            return;

        float eyeA = night * 0.92f;
        Color eye = new Color(0.82f, 0.92f, 0.28f, eyeA);
        bool showEye = eyeA > 0.04f;

        for (int i = 0; i < cats.Length; i++)
        {
            Transform root = cats[i];
            if (root == null)
                continue;

            float breath = 1f + Mathf.Sin(t * 1.35f + catPhase[i]) * 0.028f;
            root.localScale = new Vector3(catFace[i] * breath, breath, 1f);

            Transform tail = catTails[i];
            if (tail != null)
            {
                float flick = Mathf.Sin(t * 1.55f + catPhase[i]) * 11f;
                tail.localRotation = catTailRest[i] * Quaternion.Euler(0f, 0f, flick);
            }

            SpriteRenderer sr = catEyes[i];
            if (sr == null)
                continue;
            sr.color = eye;
            sr.enabled = showEye;
        }
    }

    // Entry and gate posts were empty sticks; World opening +X was empty
    // grass. Hang from authored pegs (picture, no collider, no Light2D).
    // Unlit glow sits above the night veil like a firefly. Sway is the
    // same LateUpdate path as laundry.
    private void PlantLanterns(int seed, ref int swayWrite)
    {
        if (lanternGlow == null || sway == null)
            return;

        int write = 0;
        LevelTest1.LanternPeg[] pegs = LevelTest1.LanternPegs;
        for (int i = 0; i < pegs.Length && write < lanternGlow.Length; i++)
        {
            LevelTest1.LanternPeg peg = pegs[i];
            if (peg.x < chunkLeft + 0.08f || peg.x > chunkRight - 0.08f)
                continue;

            Transform root = MakeLantern(
                new Vector3(peg.x, peg.y, 0f),
                peg.hang,
                out SpriteRenderer glow);
            BindLantern(write++, glow, Hash01(seed, 1200 + i) * 6.28f);
            if (swayWrite < sway.Length)
            {
                BindSway(
                    swayWrite++,
                    root,
                    Hash01(seed, 1210 + i) * 6.28f,
                    2.4f + Hash01(seed, 1220 + i) * 1.2f,
                    0.72f + Hash01(seed, 1230 + i) * 0.22f);
            }
        }

        if (write < lanternGlow.Length)
            TrimLanterns(write);
    }

    private void TickLanterns(float t, float night)
    {
        if (lanternGlow == null)
            return;

        float dusk = 1f - Mathf.Abs(night * 2f - 1f);
        if (dusk < 0f)
            dusk = 0f;
        Color warm = Color.Lerp(
            new Color(1f, 0.78f, 0.32f, 1f),
            new Color(1f, 0.48f, 0.14f, 1f),
            dusk);

        for (int i = 0; i < lanternGlow.Length; i++)
        {
            SpriteRenderer sr = lanternGlow[i];
            if (sr == null)
                continue;

            float flicker = 0.88f + 0.12f * Mathf.Sin(t * 4.1f + lanternPhase[i]);
            float a = (0.16f + night * 0.72f) * flicker;
            warm.a = a;
            sr.color = warm;
            sr.enabled = a > 0.04f;
        }
    }

    // Gate posts were two sticks with lanterns; the 1.5 m bay did not read
    // as a gate. A short leaf on the west post (picture, no collider) leaves
    // a walk-through. Same LateUpdate sway as laundry — not TreeRig.Sway.
    private void PlantGates(int seed, ref int swayWrite)
    {
        if (sway == null)
            return;

        LevelTest1.GateHinge[] hinges = LevelTest1.GateHinges;
        for (int i = 0; i < hinges.Length && swayWrite < sway.Length; i++)
        {
            LevelTest1.GateHinge hinge = hinges[i];
            if (hinge.x < chunkLeft + 0.08f || hinge.x > chunkRight - 0.08f)
                continue;

            Transform root = MakeGate(hinge);
            BindSway(
                swayWrite++,
                root,
                Hash01(seed, 1300 + i) * 6.28f,
                2.6f + Hash01(seed, 1310 + i) * 1.1f,
                0.62f + Hash01(seed, 1320 + i) * 0.18f);
        }
    }

    // Sparrows hop the high rail and tuck in place at night — no home.
    // A cup on authored pegs (picture, no collider) reads as a nest.
    // Sitter fades with Night01; not Bird, no takeoff. Same LateUpdate
    // sway as laundry, tiny so twigs shiver.
    private void PlantNests(int seed, ref int swayWrite)
    {
        if (nestSit == null)
            return;

        int write = 0;
        LevelTest1.NestPeg[] pegs = LevelTest1.NestPegs;
        for (int i = 0; i < pegs.Length && write < nestSit.Length; i++)
        {
            LevelTest1.NestPeg peg = pegs[i];
            if (peg.x < chunkLeft + 0.10f || peg.x > chunkRight - 0.10f)
                continue;

            Transform root = MakeNest(
                new Vector3(peg.x, peg.y, 0f),
                peg.face,
                out SpriteRenderer sit,
                out SpriteRenderer sitHead,
                out Transform sitXf);
            BindNest(write++, sit, sitHead, sitXf, Hash01(seed, 1400 + i) * 6.28f);
            if (swayWrite < sway.Length)
            {
                BindSway(
                    swayWrite++,
                    root,
                    Hash01(seed, 1410 + i) * 6.28f,
                    1.6f + Hash01(seed, 1420 + i) * 0.8f,
                    0.68f + Hash01(seed, 1430 + i) * 0.22f);
            }
        }

        if (write < nestSit.Length)
            TrimNests(write);
    }

    // Fence and posts now read as a yard; GardenWalk was still a dirt
    // slab. A terracotta (picture, no collider) on authored pegs is a
    // garden, not PlantFlower (those stay on scene Tree). Stem uses the
    // same LateUpdate sway as grass — the pot stays put.
    private void PlantPots(int seed, ref int swayWrite)
    {
        if (sway == null)
            return;

        LevelTest1.FlowerPotPeg[] pegs = LevelTest1.FlowerPotPegs;
        for (int i = 0; i < pegs.Length && swayWrite < sway.Length; i++)
        {
            LevelTest1.FlowerPotPeg peg = pegs[i];
            if (peg.x < chunkLeft + 0.10f || peg.x > chunkRight - 0.10f)
                continue;

            float surface = LevelTest1.SurfaceAt(peg.x);
            Transform stem = MakeFlowerPot(
                new Vector3(peg.x, surface, 0f),
                peg.face,
                Hash01(seed, 1500 + i));
            BindSway(
                swayWrite++,
                stem,
                Hash01(seed, 1510 + i) * 6.28f,
                3.4f + Hash01(seed, 1520 + i) * 1.4f,
                0.82f + Hash01(seed, 1530 + i) * 0.24f);
        }
    }

    // Sparrows hop the rail and tuck into cups at night; the posts were
    // still bare lumber. A box (picture, no collider) on authored pegs
    // reads as a house. Occupant fades with Night01; not Bird, no takeoff.
    // Stake houses draw a pole; the east-post one sits between the rails.
    // Same LateUpdate sway as laundry.
    private void PlantHouses(int seed, ref int swayWrite)
    {
        if (houseSit == null || sway == null)
            return;

        int write = 0;
        LevelTest1.BirdhousePeg[] pegs = LevelTest1.BirdhousePegs;
        for (int i = 0; i < pegs.Length && write < houseSit.Length && swayWrite < sway.Length; i++)
        {
            LevelTest1.BirdhousePeg peg = pegs[i];
            if (peg.x < chunkLeft + 0.10f || peg.x > chunkRight - 0.10f)
                continue;

            Transform root = MakeBirdhouse(peg, out SpriteRenderer sit);
            BindHouse(write++, sit);
            BindSway(
                swayWrite++,
                root,
                Hash01(seed, 1600 + i) * 6.28f,
                1.4f + Hash01(seed, 1610 + i) * 0.7f,
                0.64f + Hash01(seed, 1620 + i) * 0.20f);
        }

        if (write < houseSit.Length)
            TrimHouses(write);
    }

    // Sparrows hop the rail and tuck into cups / houses at night; the slab
    // still had nowhere to drink. A pedestal (picture, no collider) on
    // authored pegs is a bath — not a DrainDip puddle and not Bird. Unlit
    // water follows Night01. The drinker fades as Night01 rises so the
    // sitters can take the nests. Stone does not BindSway.
    private void PlantBaths(int seed)
    {
        if (bathWater == null)
            return;

        int write = 0;
        LevelTest1.BirdBathPeg[] pegs = LevelTest1.BirdBathPegs;
        for (int i = 0; i < pegs.Length && write < bathWater.Length; i++)
        {
            LevelTest1.BirdBathPeg peg = pegs[i];
            if (peg.x < chunkLeft + 0.10f || peg.x > chunkRight - 0.10f)
                continue;

            float surface = LevelTest1.SurfaceAt(peg.x);
            Transform water;
            Transform glint;
            Transform drinkXf;
            SpriteRenderer drink;
            SpriteRenderer drinkHead;
            MakeBirdBath(
                new Vector3(peg.x, surface, 0f),
                peg.face,
                out water,
                out glint,
                out drinkXf,
                out drink,
                out drinkHead);
            BindBath(write++, water, glint, drinkXf, drink, drinkHead, Hash01(seed, 1700 + i) * 6.28f);
        }

        if (write < bathWater.Length)
            TrimBaths(write);
    }

    // Puddles were still empty water. A toad (picture, no collider) on
    // authored pegs sits the rim — not Insect, not Bird. Day hop; night
    // the Unlit throat pulses. Stone / clay nearby does not BindSway.
    private void PlantFrogs(int seed)
    {
        if (frogs == null)
            return;

        int write = 0;
        LevelTest1.FrogPeg[] pegs = LevelTest1.FrogPegs;
        for (int i = 0; i < pegs.Length && write < frogs.Length; i++)
        {
            LevelTest1.FrogPeg peg = pegs[i];
            if (peg.x < chunkLeft + 0.10f || peg.x > chunkRight - 0.10f)
                continue;

            float surface = LevelTest1.SurfaceAt(peg.x);
            Transform root = MakeFrog(
                new Vector3(peg.x, surface + 0.024f, 0f),
                Hash01(seed, 1800 + i),
                out SpriteRenderer throat);
            BindFrog(write++, root, throat, peg.x, surface + 0.024f, peg.face, Hash01(seed, 1810 + i) * 6.28f);
        }

        if (write < frogs.Length)
            TrimFrogs(write);
    }

    // Frogs sit the rim; the water column was still empty. A darter
    // (picture, no collider) on authored pegs hovers the bowl or grate —
    // not Insect, not Bird. Day station hover; Night01 roosts them.
    private void PlantDragonflies(int seed)
    {
        if (dragonflies == null)
            return;

        int write = 0;
        LevelTest1.DragonflyPeg[] pegs = LevelTest1.DragonflyPegs;
        for (int i = 0; i < pegs.Length && write < dragonflies.Length; i++)
        {
            LevelTest1.DragonflyPeg peg = pegs[i];
            if (peg.x < chunkLeft + 0.10f || peg.x > chunkRight - 0.10f)
                continue;

            float surface = LevelTest1.SurfaceAt(peg.x);
            Transform root = MakeDragonfly(
                new Vector3(peg.x, surface + 0.40f, 0f),
                peg.face,
                out Transform wingL,
                out Transform wingR);
            BindDragonfly(
                write++,
                root,
                wingL,
                wingR,
                peg.x,
                surface + 0.40f,
                peg.face,
                Hash01(seed, 1910 + i) * 6.28f);
        }

        if (write < dragonflies.Length)
            TrimDragonflies(write);
    }

    // Dragonflies own the water column; the blooms were still empty air
    // (chunk flies wander grass). A bee (picture, no collider) on the
    // pot pegs stations the flower — not Insect, not Bird. Day hover;
    // Night01 roosts them in the bloom.
    private void PlantBees(int seed)
    {
        if (bees == null)
            return;

        int write = 0;
        LevelTest1.FlowerPotPeg[] pegs = LevelTest1.FlowerPotPegs;
        for (int i = 0; i < pegs.Length && write < bees.Length; i++)
        {
            LevelTest1.FlowerPotPeg peg = pegs[i];
            if (peg.x < chunkLeft + 0.10f || peg.x > chunkRight - 0.10f)
                continue;

            float surface = LevelTest1.SurfaceAt(peg.x);
            Transform root = MakeBee(
                new Vector3(peg.x, surface + 0.28f, 0f),
                peg.face,
                out Transform wingL,
                out Transform wingR);
            BindBee(
                write++,
                root,
                wingL,
                wingR,
                peg.x,
                surface + 0.28f,
                peg.face,
                Hash01(seed, 2010 + i) * 6.28f);
        }

        if (write < bees.Length)
            TrimBees(write);
    }

    // Bees own the bloom air; the clay was still a dead box. A snail
    // (picture, no collider) on the same pot pegs climbs the wall — not
    // Insect, not Bird. Slow crawl all day so the opening shot sees them;
    // Night01 biases the path toward the rim (damp). Antenna twitch only.
    private void PlantSnails(int seed)
    {
        if (snails == null)
            return;

        int write = 0;
        LevelTest1.FlowerPotPeg[] pegs = LevelTest1.FlowerPotPegs;
        for (int i = 0; i < pegs.Length && write < snails.Length; i++)
        {
            LevelTest1.FlowerPotPeg peg = pegs[i];
            if (peg.x < chunkLeft + 0.10f || peg.x > chunkRight - 0.10f)
                continue;

            float surface = LevelTest1.SurfaceAt(peg.x);
            Transform root = MakeSnail(
                new Vector3(peg.x + peg.face * 0.050f, surface + 0.028f, 0f),
                peg.face,
                out Transform antL,
                out Transform antR);
            BindSnail(
                write++,
                root,
                antL,
                antR,
                peg.x,
                surface + 0.028f,
                peg.face,
                Hash01(seed, 2110 + i) * 6.28f);
        }

        if (write < snails.Length)
            TrimSnails(write);
    }

    // Snails own the clay; the posts were still bare lumber (lanterns
    // hang, houses sit, wood itself had no life). A lizard (picture, no
    // collider) on authored pegs basks the face — not Insect, not Bird,
    // not Dog. Day push-up so noon Play reads them; Night01 flattens
    // them into the wood. Tail flick only.
    private void PlantLizards(int seed)
    {
        if (lizards == null)
            return;

        int write = 0;
        LevelTest1.LizardPeg[] pegs = LevelTest1.LizardPegs;
        for (int i = 0; i < pegs.Length && write < lizards.Length; i++)
        {
            LevelTest1.LizardPeg peg = pegs[i];
            if (peg.x < chunkLeft + 0.10f || peg.x > chunkRight - 0.10f)
                continue;

            Transform root = MakeLizard(
                new Vector3(peg.x + peg.face * 0.055f, peg.y, 0f),
                peg.face,
                out Transform tail);
            BindLizard(
                write++,
                root,
                tail,
                peg.x,
                peg.y,
                peg.face,
                Hash01(seed, 2210 + i) * 6.28f);
        }

        if (write < lizards.Length)
            TrimLizards(write);
    }

    // Lizards own the lumber; the dry grass between furniture was still
    // empty (frogs sit wet rims, flies wander the air). A hopper (picture,
    // no collider) on authored pegs sits the blades — not Insect, not
    // Bird. Day hop so noon Play reads the big hind; Night01 hides them
    // in the grass. Antenna twitch; louder after dusk.
    private void PlantGrasshoppers(int seed)
    {
        if (hoppers == null)
            return;

        int write = 0;
        LevelTest1.GrasshopperPeg[] pegs = LevelTest1.GrasshopperPegs;
        for (int i = 0; i < pegs.Length && write < hoppers.Length; i++)
        {
            LevelTest1.GrasshopperPeg peg = pegs[i];
            if (peg.x < chunkLeft + 0.10f || peg.x > chunkRight - 0.10f)
                continue;

            float surface = LevelTest1.SurfaceAt(peg.x);
            Transform root = MakeGrasshopper(
                new Vector3(peg.x, surface + 0.016f, 0f),
                peg.face,
                out Transform hind,
                out Transform ant);
            BindGrasshopper(
                write++,
                root,
                hind,
                ant,
                peg.x,
                surface + 0.016f,
                peg.face,
                Hash01(seed, 2310 + i) * 6.28f);
        }

        if (write < hoppers.Length)
            TrimGrasshoppers(write);
    }

    // Hoppers own the blades; the bare dirt between furniture was still
    // empty (frogs sit wet rims). A trail (picture, no collider) on
    // authored pegs crawls the slab — not Insect, not Bird. Day walk
    // so noon Play reads the line of bodies; Night01 sinks them into
    // the dirt. Antenna twitch; quieter after dusk.
    private void PlantAnts(int seed)
    {
        if (ants == null)
            return;

        int write = 0;
        LevelTest1.AntPeg[] pegs = LevelTest1.AntPegs;
        for (int i = 0; i < pegs.Length && write < ants.Length; i++)
        {
            LevelTest1.AntPeg peg = pegs[i];
            if (peg.x < chunkLeft + 0.10f || peg.x > chunkRight - 0.10f)
                continue;

            float surface = LevelTest1.SurfaceAt(peg.x);
            Transform root = MakeAntTrail(
                new Vector3(peg.x, surface + 0.008f, 0f),
                peg.face,
                out Transform ant);
            BindAnt(
                write++,
                root,
                ant,
                peg.x,
                surface + 0.008f,
                peg.face,
                Hash01(seed, 2410 + i) * 6.28f);
        }

        if (write < ants.Length)
            TrimAnts(write);
    }

    // Ants own the day dirt; hoppers hide after dusk, so the blades
    // were empty. A cricket (picture, no collider) on authored pegs
    // sits the grass — not Insect, not Bird. Night chirp so Play
    // reads the long feelers; day sinks them into the tuft. Wing
    // pulse is the chirp, not a hop (hoppers own that).
    private void PlantCrickets(int seed)
    {
        if (crickets == null)
            return;

        int write = 0;
        LevelTest1.CricketPeg[] pegs = LevelTest1.CricketPegs;
        for (int i = 0; i < pegs.Length && write < crickets.Length; i++)
        {
            LevelTest1.CricketPeg peg = pegs[i];
            if (peg.x < chunkLeft + 0.10f || peg.x > chunkRight - 0.10f)
                continue;

            float surface = LevelTest1.SurfaceAt(peg.x);
            Transform root = MakeCricket(
                new Vector3(peg.x, surface + 0.014f, 0f),
                peg.face,
                out Transform wing,
                out Transform ant);
            BindCricket(
                write++,
                root,
                wing,
                ant,
                peg.x,
                surface + 0.014f,
                peg.face,
                Hash01(seed, 2510 + i) * 6.28f);
        }

        if (write < crickets.Length)
            TrimCrickets(write);
    }

    // Crickets own the night blades; ants sink, so the dirt was empty.
    // A worm (picture, no collider) on authored pegs crawls the slab —
    // not Insect, not Bird. Night crawl so Play reads the pale Unlit
    // sheen; day sinks them into the dirt. Peristalsis is the motion,
    // not a hop (hoppers own that) and not a trail (ants own that).
    private void PlantWorms(int seed)
    {
        if (worms == null)
            return;

        int write = 0;
        LevelTest1.WormPeg[] pegs = LevelTest1.WormPegs;
        for (int i = 0; i < pegs.Length && write < worms.Length; i++)
        {
            LevelTest1.WormPeg peg = pegs[i];
            if (peg.x < chunkLeft + 0.10f || peg.x > chunkRight - 0.10f)
                continue;

            float surface = LevelTest1.SurfaceAt(peg.x);
            Transform root = MakeWorm(
                new Vector3(peg.x, surface + 0.006f, 0f),
                peg.face,
                out Transform mid,
                out SpriteRenderer sheen);
            BindWorm(
                write++,
                root,
                mid,
                sheen,
                peg.x,
                surface + 0.006f,
                peg.face,
                Hash01(seed, 2610 + i) * 6.28f);
        }

        if (write < worms.Length)
            TrimWorms(write);
    }

    // Worms own the night dirt; frogs sit the rim and dragonflies the
    // grate air, so the puddle film was empty. A strider (picture, no
    // collider) on authored pegs skates the ellipse — not Insect, not
    // Bird. Day skate so Play reads the long mid-legs on the water;
    // Night01 sinks them at the rim. Dimple is Unlit surface tension,
    // not a collider and not a puddle rewrite.
    private void PlantStriders(int seed)
    {
        if (striders == null)
            return;

        int write = 0;
        LevelTest1.StriderPeg[] pegs = LevelTest1.StriderPegs;
        for (int i = 0; i < pegs.Length && write < striders.Length; i++)
        {
            LevelTest1.StriderPeg peg = pegs[i];
            if (peg.x < chunkLeft + 0.10f || peg.x > chunkRight - 0.10f)
                continue;

            float surface = LevelTest1.SurfaceAt(peg.x);
            Transform root = MakeStrider(
                new Vector3(peg.x, surface + 0.026f, 0f),
                peg.face,
                out Transform stroke,
                out SpriteRenderer dimple);
            BindStrider(
                write++,
                root,
                stroke,
                dimple,
                peg.x,
                surface + 0.026f,
                peg.face,
                Hash01(seed, 2710 + i) * 6.28f);
        }

        if (write < striders.Length)
            TrimStriders(write);
    }

    // Striders own the puddle film; moths own the campfire; sky flocks
    // roost when Night01 rises, so lantern air was empty. A bat (picture,
    // no collider) on authored pegs hunts the bowl — not Bird, not Insect.
    // Night orbit so Play reads the silhouette against the Unlit glow;
    // day roosts them (SetActive). Flap is LateUpdate, not a lift force.
    private void PlantBats(int seed)
    {
        if (bats == null)
            return;

        int write = 0;
        LevelTest1.BatPeg[] pegs = LevelTest1.BatPegs;
        for (int i = 0; i < pegs.Length && write < bats.Length; i++)
        {
            LevelTest1.BatPeg peg = pegs[i];
            if (peg.x < chunkLeft + 0.10f || peg.x > chunkRight - 0.10f)
                continue;

            float cx = peg.x + peg.hang;
            float cy = peg.y - 0.118f;
            Transform root = MakeBat(
                new Vector3(cx + 0.22f * peg.face, cy, 0f),
                peg.face,
                out Transform wingL,
                out Transform wingR,
                out SpriteRenderer eye);
            root.gameObject.SetActive(false);
            BindBat(
                write++,
                root,
                wingL,
                wingR,
                eye,
                cx,
                cy,
                Hash01(seed, 2810 + i) * 6.28f);
        }

        if (write < bats.Length)
            TrimBats(write);
    }

    // Bats own lantern air; sparrows hop the high-rail top, laundry the
    // post line, cats the low rail, so the bay BETWEEN the rails was
    // empty. A web (picture, no collider) on authored pegs spans that
    // gap — not Insect, not Bird. Spider crawls the silk in LateUpdate;
    // Unlit dew follows Night01 so Play reads the strands after dusk.
    private void PlantWebs(int seed)
    {
        if (spiders == null)
            return;

        int write = 0;
        LevelTest1.WebPeg[] pegs = LevelTest1.WebPegs;
        for (int i = 0; i < pegs.Length && write < spiders.Length; i++)
        {
            LevelTest1.WebPeg peg = pegs[i];
            if (peg.x < chunkLeft + 0.10f || peg.x > chunkRight - 0.10f)
                continue;

            Transform spider = MakeWeb(peg, out Transform legs, out SpriteRenderer dew);
            BindWeb(
                write++,
                spider,
                legs,
                dew,
                peg.x,
                peg.y,
                peg.width * 0.5f,
                peg.height * 0.5f,
                peg.face,
                Hash01(seed, 2910 + i) * 6.28f);
        }

        if (write < spiders.Length)
            TrimWebs(write);
    }

    // Webs own the rail bay; worms own a thin night sheen, so the slab
    // under the loafing cat and at the fire crumbs was empty. A mouse
    // (picture, no collider) on authored pegs darts after dusk — not Dog,
    // not Insect, not Hedgehog. Day sinks them into the dirt; Unlit eyes
    // follow Night01 so Play reads the scavenger against the cat / glow.
    private void PlantMice(int seed)
    {
        if (mice == null)
            return;

        int write = 0;
        LevelTest1.MousePeg[] pegs = LevelTest1.MousePegs;
        for (int i = 0; i < pegs.Length && write < mice.Length; i++)
        {
            LevelTest1.MousePeg peg = pegs[i];
            if (peg.x < chunkLeft + 0.10f || peg.x > chunkRight - 0.10f)
                continue;

            float surface = LevelTest1.SurfaceAt(peg.x);
            Transform root = MakeMouse(
                new Vector3(peg.x, surface + 0.016f, 0f),
                peg.face,
                out Transform tail,
                out SpriteRenderer eye);
            BindMouse(
                write++,
                root,
                tail,
                eye,
                peg.x,
                surface + 0.016f,
                peg.face,
                Hash01(seed, 3010 + i) * 6.28f);
        }

        if (write < mice.Length)
            TrimMice(write);
    }

    // Mice own the night slab; hoppers own the insect hop in the blades,
    // so day grass at mammal scale was empty. A rabbit (picture, no
    // collider) on authored pegs nibbles and freeze-hops — not Dog, not
    // Insect, not Hedgehog. Night01 sinks them into the grass so Play
    // does not stack a day grazer on the scavenger mice.
    private void PlantRabbits(int seed)
    {
        if (rabbits == null)
            return;

        int write = 0;
        LevelTest1.RabbitPeg[] pegs = LevelTest1.RabbitPegs;
        for (int i = 0; i < pegs.Length && write < rabbits.Length; i++)
        {
            LevelTest1.RabbitPeg peg = pegs[i];
            if (peg.x < chunkLeft + 0.10f || peg.x > chunkRight - 0.10f)
                continue;

            float surface = LevelTest1.SurfaceAt(peg.x);
            Transform root = MakeRabbit(
                new Vector3(peg.x, surface + 0.032f, 0f),
                peg.face,
                out Transform head,
                out Transform ear);
            BindRabbit(
                write++,
                root,
                head,
                ear,
                peg.x,
                surface + 0.032f,
                peg.face,
                Hash01(seed, 3110 + i) * 6.28f);
        }

        if (write < rabbits.Length)
            TrimRabbits(write);
    }

    // Rabbits own the grass; lizards own the lumber; house sitters take
    // the hole after dusk, so the roofs were empty by day. A squirrel
    // (picture, no collider) on authored pegs freeze-scampers — not Dog,
    // not Insect, not Bird. Night01 hides them so Play hands the house
    // to the sitter instead of stacking a day climber on the hole.
    private void PlantSquirrels(int seed)
    {
        if (squirrels == null)
            return;

        int write = 0;
        LevelTest1.SquirrelPeg[] pegs = LevelTest1.SquirrelPegs;
        for (int i = 0; i < pegs.Length && write < squirrels.Length; i++)
        {
            LevelTest1.SquirrelPeg peg = pegs[i];
            if (peg.x < chunkLeft + 0.10f || peg.x > chunkRight - 0.10f)
                continue;

            Transform root = MakeSquirrel(
                new Vector3(peg.x, peg.y, 0f),
                peg.face,
                out Transform tail);
            BindSquirrel(
                write++,
                root,
                tail,
                peg.x,
                peg.y,
                peg.face,
                Hash01(seed, 3210 + i) * 6.28f);
        }

        if (write < squirrels.Length)
            TrimSquirrels(write);
    }

    // Squirrels own the house roofs by day; bats hunt the hanging bowl
    // after dusk, so the lantern post CAP was empty. An owl (picture, no
    // collider) on authored pegs roosts — not Bird, not Insect, not Dog.
    // Night01 shows them against the Unlit glow; day hides them so the
    // cap stays a post at noon.
    private void PlantOwls(int seed)
    {
        if (owls == null)
            return;

        int write = 0;
        LevelTest1.OwlPeg[] pegs = LevelTest1.OwlPegs;
        for (int i = 0; i < pegs.Length && write < owls.Length; i++)
        {
            LevelTest1.OwlPeg peg = pegs[i];
            if (peg.x < chunkLeft + 0.10f || peg.x > chunkRight - 0.10f)
                continue;

            Transform root = MakeOwl(
                new Vector3(peg.x, peg.y, 0f),
                peg.face,
                out Transform head,
                out SpriteRenderer eye);
            BindOwl(
                write++,
                root,
                head,
                eye,
                peg.x,
                peg.y,
                peg.face,
                Hash01(seed, 3310 + i) * 6.28f);
        }

        if (write < owls.Length)
            TrimOwls(write);
    }

    // Owls own the post cap; mice dart the crumb line, moths the flame
    // air, so the log flank was empty. A raccoon (picture, no collider)
    // on authored pegs washes after dusk — not Dog, not Hedgehog, not
    // Insect. Night01 shows them; day sinks them into the dirt so Play
    // does not stack a night scavenger on the rabbits.
    private void PlantRaccoons(int seed)
    {
        if (raccoons == null)
            return;

        int write = 0;
        LevelTest1.RaccoonPeg[] pegs = LevelTest1.RaccoonPegs;
        for (int i = 0; i < pegs.Length && write < raccoons.Length; i++)
        {
            LevelTest1.RaccoonPeg peg = pegs[i];
            if (peg.x < chunkLeft + 0.10f || peg.x > chunkRight - 0.10f)
                continue;

            float surface = LevelTest1.SurfaceAt(peg.x);
            Transform root = MakeRaccoon(
                new Vector3(peg.x, surface + 0.028f, 0f),
                peg.face,
                out Transform head,
                out Transform tail,
                out SpriteRenderer eye);
            BindRaccoon(
                write++,
                root,
                head,
                tail,
                eye,
                peg.x,
                surface + 0.028f,
                peg.face,
                Hash01(seed, 3410 + i) * 6.28f);
        }

        if (write < raccoons.Length)
            TrimRaccoons(write);
    }

    // Raccoons wash the log flank after dusk; frogs hop the dry rim and
    // striders skate the film, so the puddle BANK was empty. A turtle
    // (picture, no collider) on authored pegs basks — not Insect, not
    // Bird, not Dog. Day crawl; Night01 tucks them into the mud so Play
    // does not stack a shell on the night scavengers.
    private void PlantTurtles(int seed)
    {
        if (turtles == null)
            return;

        int write = 0;
        LevelTest1.TurtlePeg[] pegs = LevelTest1.TurtlePegs;
        for (int i = 0; i < pegs.Length && write < turtles.Length; i++)
        {
            LevelTest1.TurtlePeg peg = pegs[i];
            if (peg.x < chunkLeft + 0.10f || peg.x > chunkRight - 0.10f)
                continue;

            float surface = LevelTest1.SurfaceAt(peg.x);
            Transform root = MakeTurtle(
                new Vector3(peg.x, surface + 0.022f, 0f),
                peg.face,
                out Transform head);
            BindTurtle(
                write++,
                root,
                head,
                peg.x,
                surface + 0.022f,
                peg.face,
                Hash01(seed, 3510 + i) * 6.28f);
        }

        if (write < turtles.Length)
            TrimTurtles(write);
    }

    // Turtles own the mud bank; striders skate the film and drinkers
    // sit the bowl rim, so the water's EDGE was empty. A mallard
    // (picture, no collider) on authored pegs loafs — not Bird, not
    // Insect. Day dabble; Night01 tucks the head so Play does not
    // stack a drake on the night scavengers.
    private void PlantDucks(int seed)
    {
        if (ducks == null)
            return;

        int write = 0;
        LevelTest1.DuckPeg[] pegs = LevelTest1.DuckPegs;
        for (int i = 0; i < pegs.Length && write < ducks.Length; i++)
        {
            LevelTest1.DuckPeg peg = pegs[i];
            if (peg.x < chunkLeft + 0.10f || peg.x > chunkRight - 0.10f)
                continue;

            float surface = LevelTest1.SurfaceAt(peg.x);
            Transform root = MakeDuck(
                new Vector3(peg.x, surface + 0.030f, 0f),
                peg.face,
                out Transform head);
            BindDuck(
                write++,
                root,
                head,
                peg.x,
                surface + 0.030f,
                peg.face,
                Hash01(seed, 3610 + i) * 6.28f);
        }

        if (write < ducks.Length)
            TrimDucks(write);
    }

    // Ducks loaf the edge; striders skate the film and frogs hop the
    // rim, so the dip had no wader. A heron (picture, no collider) on
    // authored pegs spears — not Bird, not Insect. Day stand-and-stab;
    // Night01 folds the neck so Play does not stack a spear on the
    // night scavengers.
    private void PlantHerons(int seed)
    {
        if (herons == null)
            return;

        int write = 0;
        LevelTest1.HeronPeg[] pegs = LevelTest1.HeronPegs;
        for (int i = 0; i < pegs.Length && write < herons.Length; i++)
        {
            LevelTest1.HeronPeg peg = pegs[i];
            if (peg.x < chunkLeft + 0.10f || peg.x > chunkRight - 0.10f)
                continue;

            float surface = LevelTest1.SurfaceAt(peg.x);
            Transform root = MakeHeron(
                new Vector3(peg.x, surface + 0.108f, 0f),
                peg.face,
                out Transform neck);
            BindHeron(
                write++,
                root,
                neck,
                peg.x,
                surface + 0.108f,
                peg.face,
                Hash01(seed, 3710 + i) * 6.28f);
        }

        if (write < herons.Length)
            TrimHerons(write);
    }

    // Herons wade; striders skate the film and ducks loaf the edge, so
    // the dip had no prey under the ellipse. A minnow (picture, no
    // collider) on authored pegs darts — not Insect, not Bird. Day
    // cruise so Play reads a silver flash in the water; Night01 sinks
    // them into the mud so they do not stack on the night scavengers.
    private void PlantMinnows(int seed)
    {
        if (minnows == null)
            return;

        int write = 0;
        LevelTest1.MinnowPeg[] pegs = LevelTest1.MinnowPegs;
        for (int i = 0; i < pegs.Length && write < minnows.Length; i++)
        {
            LevelTest1.MinnowPeg peg = pegs[i];
            if (peg.x < chunkLeft + 0.10f || peg.x > chunkRight - 0.10f)
                continue;

            float surface = LevelTest1.SurfaceAt(peg.x);
            Transform root = MakeMinnow(
                new Vector3(peg.x, surface + 0.012f, 0f),
                peg.face,
                out Transform tail,
                out SpriteRenderer flash);
            BindMinnow(
                write++,
                root,
                tail,
                flash,
                peg.x,
                surface + 0.012f,
                peg.face,
                Hash01(seed, 3810 + i) * 6.28f);
        }

        if (write < minnows.Length)
            TrimMinnows(write);
    }

    // Minnows dart under the film; ducks loaf the edge and turtles the
    // bank, so the wet MARGIN still had no reed. Grass skips IsWetAt,
    // and the bank next to a dip stayed bare dirt. A cattail clump
    // (picture, no collider) on authored pegs frames the water — not
    // PlantFlower, not TreeRig.Sway. Same LateUpdate sway as grass.
    private void PlantCattails(int seed, ref int swayWrite)
    {
        if (sway == null)
            return;

        LevelTest1.CattailPeg[] pegs = LevelTest1.CattailPegs;
        for (int i = 0; i < pegs.Length && swayWrite < sway.Length; i++)
        {
            LevelTest1.CattailPeg peg = pegs[i];
            if (peg.x < chunkLeft + 0.10f || peg.x > chunkRight - 0.10f)
                continue;

            float surface = LevelTest1.SurfaceAt(peg.x);
            Transform root = MakeCattail(
                new Vector3(peg.x, surface, 0f),
                peg.face,
                Hash01(seed, 3910 + i));
            BindSway(
                swayWrite++,
                root,
                Hash01(seed, 3920 + i) * 6.28f,
                3.6f + Hash01(seed, 3930 + i) * 1.2f,
                0.72f + Hash01(seed, 3940 + i) * 0.22f);
        }
    }

    // Cattails stand the bank; minnows dart under the film and
    // striders skate it, so the OPEN ellipse still had no plant.
    // A lily pad (picture, no collider) on authored pegs sits on
    // the film — not PlantFlower, not a puddle collider. Same
    // LateUpdate sway as grass; a small amp so the pad nods, not
    // a stem wave.
    private void PlantLilyPads(int seed, ref int swayWrite)
    {
        if (sway == null)
            return;

        LevelTest1.LilyPadPeg[] pegs = LevelTest1.LilyPadPegs;
        for (int i = 0; i < pegs.Length && swayWrite < sway.Length; i++)
        {
            LevelTest1.LilyPadPeg peg = pegs[i];
            if (peg.x < chunkLeft + 0.10f || peg.x > chunkRight - 0.10f)
                continue;

            float surface = LevelTest1.SurfaceAt(peg.x);
            Transform root = MakeLilyPad(
                new Vector3(peg.x, surface + 0.016f, 0f),
                peg.face,
                Hash01(seed, 4010 + i));
            BindSway(
                swayWrite++,
                root,
                Hash01(seed, 4020 + i) * 6.28f,
                1.6f + Hash01(seed, 4030 + i) * 0.8f,
                0.55f + Hash01(seed, 4040 + i) * 0.20f);
        }
    }

    // Cattails own the wet bank; lily pads the film; pots the
    // terracotta, so DRY grass was still only tufts. A dandelion
    // (picture, no collider) on authored pegs stands the blades —
    // not PlantFlower, not TreeRig.Sway. Same LateUpdate sway as
    // grass; amp like a short tuft, not a cattail stem.
    private void PlantDandelions(int seed, ref int swayWrite)
    {
        if (sway == null)
            return;

        LevelTest1.DandelionPeg[] pegs = LevelTest1.DandelionPegs;
        for (int i = 0; i < pegs.Length && swayWrite < sway.Length; i++)
        {
            LevelTest1.DandelionPeg peg = pegs[i];
            if (peg.x < chunkLeft + 0.10f || peg.x > chunkRight - 0.10f)
                continue;

            float surface = LevelTest1.SurfaceAt(peg.x);
            Transform root = MakeDandelion(
                new Vector3(peg.x, surface, 0f),
                peg.face,
                Hash01(seed, 4110 + i));
            BindSway(
                swayWrite++,
                root,
                Hash01(seed, 4120 + i) * 6.28f,
                4.2f + Hash01(seed, 4130 + i) * 1.4f,
                0.90f + Hash01(seed, 4140 + i) * 0.28f);
        }
    }

    // Dandelions own the dry blades; cattails the wet bank; lily
    // pads the film, so the DAMP MARGIN of a bowl / dip still had
    // no toadstool. A mushroom cluster (picture, no collider) on
    // authored pegs sits the shade — not PlantFlower, not Insect.
    // Same LateUpdate sway as grass; tiny amp so the caps nod,
    // not a stem wave.
    private void PlantMushrooms(int seed, ref int swayWrite)
    {
        if (sway == null)
            return;

        LevelTest1.MushroomPeg[] pegs = LevelTest1.MushroomPegs;
        for (int i = 0; i < pegs.Length && swayWrite < sway.Length; i++)
        {
            LevelTest1.MushroomPeg peg = pegs[i];
            if (peg.x < chunkLeft + 0.10f || peg.x > chunkRight - 0.10f)
                continue;

            float surface = LevelTest1.SurfaceAt(peg.x);
            Transform root = MakeMushroom(
                new Vector3(peg.x, surface, 0f),
                peg.face,
                Hash01(seed, 4210 + i));
            BindSway(
                swayWrite++,
                root,
                Hash01(seed, 4220 + i) * 6.28f,
                1.1f + Hash01(seed, 4230 + i) * 0.5f,
                0.48f + Hash01(seed, 4240 + i) * 0.18f);
        }
    }

    // Mushrooms own the damp margin; lizards the lumber face;
    // lanterns hang; houses sit, so the POST still had no vine.
    // Ivy (picture, no collider) on authored pegs climbs the
    // foot — not PlantFlower, not TreeRig.Sway. Same LateUpdate
    // sway as laundry; small amp so the leaves nod, not a stem
    // wave. Lizard order 6 draws on top of the vine.
    private void PlantIvies(int seed, ref int swayWrite)
    {
        if (sway == null)
            return;

        LevelTest1.IvyPeg[] pegs = LevelTest1.IvyPegs;
        for (int i = 0; i < pegs.Length && swayWrite < sway.Length; i++)
        {
            LevelTest1.IvyPeg peg = pegs[i];
            if (peg.x < chunkLeft + 0.10f || peg.x > chunkRight - 0.10f)
                continue;

            float surface = LevelTest1.SurfaceAt(peg.x);
            Transform root = MakeIvy(
                new Vector3(peg.x, surface, 0f),
                peg.face,
                Hash01(seed, 4310 + i));
            BindSway(
                swayWrite++,
                root,
                Hash01(seed, 4320 + i) * 6.28f,
                2.2f + Hash01(seed, 4330 + i) * 0.9f,
                0.68f + Hash01(seed, 4340 + i) * 0.22f);
        }
    }

    // Ivy owns the post climb; mushrooms the damp margin; dandelions
    // the dry blades, so SHADE at a fence foot / grove still had no
    // frond. A fern clump (picture, no collider) on authored pegs
    // sits the understory — not PlantFlower, not TreeRig.Sway. Same
    // LateUpdate sway as laundry; small amp so the fronds nod, not
    // a stem wave.
    private void PlantFerns(int seed, ref int swayWrite)
    {
        if (sway == null)
            return;

        LevelTest1.FernPeg[] pegs = LevelTest1.FernPegs;
        for (int i = 0; i < pegs.Length && swayWrite < sway.Length; i++)
        {
            LevelTest1.FernPeg peg = pegs[i];
            if (peg.x < chunkLeft + 0.10f || peg.x > chunkRight - 0.10f)
                continue;

            float surface = LevelTest1.SurfaceAt(peg.x);
            Transform root = MakeFern(
                new Vector3(peg.x, surface, 0f),
                peg.face,
                Hash01(seed, 4410 + i));
            BindSway(
                swayWrite++,
                root,
                Hash01(seed, 4420 + i) * 6.28f,
                2.0f + Hash01(seed, 4430 + i) * 0.8f,
                0.62f + Hash01(seed, 4440 + i) * 0.20f);
        }
    }

    // Ferns own the shade understory; dandelions the tall clocks;
    // grass the blades, so DRY lawn between tufts still had no
    // trefoil. A clover clump (picture, no collider) on authored
    // pegs carpets the sunlit slab — not PlantFlower, not
    // TreeRig.Sway. Same LateUpdate sway as laundry; small amp
    // so the leaves nod, not a stem wave.
    private void PlantClovers(int seed, ref int swayWrite)
    {
        if (sway == null)
            return;

        LevelTest1.CloverPeg[] pegs = LevelTest1.CloverPegs;
        for (int i = 0; i < pegs.Length && swayWrite < sway.Length; i++)
        {
            LevelTest1.CloverPeg peg = pegs[i];
            if (peg.x < chunkLeft + 0.10f || peg.x > chunkRight - 0.10f)
                continue;

            float surface = LevelTest1.SurfaceAt(peg.x);
            Transform root = MakeClover(
                new Vector3(peg.x, surface, 0f),
                peg.face,
                Hash01(seed, 4510 + i));
            BindSway(
                swayWrite++,
                root,
                Hash01(seed, 4520 + i) * 6.28f,
                2.2f + Hash01(seed, 4530 + i) * 0.8f,
                0.70f + Hash01(seed, 4540 + i) * 0.22f);
        }
    }

    // Clover owns the low dry carpet; dandelions the tall clocks;
    // grass the blades, so DRY WASTE between furniture still had
    // no spine. A thistle (picture, no collider) on authored pegs
    // stands the disturbed edge — not PlantFlower, not
    // TreeRig.Sway. Same LateUpdate sway as laundry; small amp
    // so the head nods, not a stem wave.
    private void PlantThistles(int seed, ref int swayWrite)
    {
        if (sway == null)
            return;

        LevelTest1.ThistlePeg[] pegs = LevelTest1.ThistlePegs;
        for (int i = 0; i < pegs.Length && swayWrite < sway.Length; i++)
        {
            LevelTest1.ThistlePeg peg = pegs[i];
            if (peg.x < chunkLeft + 0.10f || peg.x > chunkRight - 0.10f)
                continue;

            float surface = LevelTest1.SurfaceAt(peg.x);
            Transform root = MakeThistle(
                new Vector3(peg.x, surface, 0f),
                peg.face,
                Hash01(seed, 4610 + i));
            BindSway(
                swayWrite++,
                root,
                Hash01(seed, 4620 + i) * 6.28f,
                2.0f + Hash01(seed, 4630 + i) * 0.7f,
                0.62f + Hash01(seed, 4640 + i) * 0.20f);
        }
    }

    // Thistles own the dry waste; clover the lawn; ants the dirt
    // crawl, so the TRODDEN path edge still had no spike. A
    // plantain (picture, no collider) on authored pegs sits the
    // walked verge — not PlantFlower, not TreeRig.Sway. Same
    // LateUpdate sway as laundry; small amp so the spike nods,
    // not a stem wave.
    private void PlantPlantains(int seed, ref int swayWrite)
    {
        if (sway == null)
            return;

        LevelTest1.PlantainPeg[] pegs = LevelTest1.PlantainPegs;
        for (int i = 0; i < pegs.Length && swayWrite < sway.Length; i++)
        {
            LevelTest1.PlantainPeg peg = pegs[i];
            if (peg.x < chunkLeft + 0.10f || peg.x > chunkRight - 0.10f)
                continue;

            float surface = LevelTest1.SurfaceAt(peg.x);
            Transform root = MakePlantain(
                new Vector3(peg.x, surface, 0f),
                peg.face,
                Hash01(seed, 4710 + i));
            BindSway(
                swayWrite++,
                root,
                Hash01(seed, 4720 + i) * 6.28f,
                2.1f + Hash01(seed, 4730 + i) * 0.7f,
                0.58f + Hash01(seed, 4740 + i) * 0.18f);
        }
    }

    // Plantain owns the trodden edge; thistles the dry waste; ferns
    // the shade foot; ivy the post climb, so the DAMP nitrogen
    // patch at a fence or grove drip still had no sting. A
    // nettle (picture, no collider) on authored pegs sits that
    // foot — not PlantFlower, not TreeRig.Sway. Same LateUpdate
    // sway as laundry; small amp so the tassels nod, not a stem
    // wave.
    private void PlantNettles(int seed, ref int swayWrite)
    {
        if (sway == null)
            return;

        LevelTest1.NettlePeg[] pegs = LevelTest1.NettlePegs;
        for (int i = 0; i < pegs.Length && swayWrite < sway.Length; i++)
        {
            LevelTest1.NettlePeg peg = pegs[i];
            if (peg.x < chunkLeft + 0.10f || peg.x > chunkRight - 0.10f)
                continue;

            float surface = LevelTest1.SurfaceAt(peg.x);
            Transform root = MakeNettle(
                new Vector3(peg.x, surface, 0f),
                peg.face,
                Hash01(seed, 4810 + i));
            BindSway(
                swayWrite++,
                root,
                Hash01(seed, 4820 + i) * 6.28f,
                2.0f + Hash01(seed, 4830 + i) * 0.7f,
                0.55f + Hash01(seed, 4840 + i) * 0.18f);
        }
    }

    // Nettles own the fence-foot sting; plantain the trodden
    // spike; thistles the dry waste; cattails the wet reed, so
    // the OPEN disturbed verge still had no rusty panicle. A
    // dock (picture, no collider) on authored pegs sits that
    // bank — not PlantFlower, not TreeRig.Sway. Same LateUpdate
    // sway as laundry; small amp so the panicle nods, not a
    // stem wave.
    private void PlantDocks(int seed, ref int swayWrite)
    {
        if (sway == null)
            return;

        LevelTest1.DockPeg[] pegs = LevelTest1.DockPegs;
        for (int i = 0; i < pegs.Length && swayWrite < sway.Length; i++)
        {
            LevelTest1.DockPeg peg = pegs[i];
            if (peg.x < chunkLeft + 0.10f || peg.x > chunkRight - 0.10f)
                continue;

            float surface = LevelTest1.SurfaceAt(peg.x);
            Transform root = MakeDock(
                new Vector3(peg.x, surface, 0f),
                peg.face,
                Hash01(seed, 4910 + i));
            BindSway(
                swayWrite++,
                root,
                Hash01(seed, 4920 + i) * 6.28f,
                2.2f + Hash01(seed, 4930 + i) * 0.7f,
                0.52f + Hash01(seed, 4940 + i) * 0.16f);
        }
    }

    // Docks own the rusty panicle; nettles the fence-foot sting;
    // ivy the post climb; thistles the dry globe, so the FENCE
    // BAY between posts still had no arching hedge. A bramble
    // (picture, no collider) on authored pegs sits that bay —
    // not PlantFlower, not TreeRig.Sway. Same LateUpdate sway
    // as laundry; small amp so the canes nod, not a stem wave.
    private void PlantBrambles(int seed, ref int swayWrite)
    {
        if (sway == null)
            return;

        LevelTest1.BramblePeg[] pegs = LevelTest1.BramblePegs;
        for (int i = 0; i < pegs.Length && swayWrite < sway.Length; i++)
        {
            LevelTest1.BramblePeg peg = pegs[i];
            if (peg.x < chunkLeft + 0.10f || peg.x > chunkRight - 0.10f)
                continue;

            float surface = LevelTest1.SurfaceAt(peg.x);
            Transform root = MakeBramble(
                new Vector3(peg.x, surface, 0f),
                peg.face,
                Hash01(seed, 5010 + i));
            BindSway(
                swayWrite++,
                root,
                Hash01(seed, 5020 + i) * 6.28f,
                1.9f + Hash01(seed, 5030 + i) * 0.6f,
                0.58f + Hash01(seed, 5040 + i) * 0.16f);
        }
    }

    // Brambles own the bay hedge; ivy the post climb; terracotta
    // pots the clay bloom; strawberries the low fruit, so a SUNNY
    // garden still had no tall cultivated spike. A hollyhock
    // (picture, no collider) on authored pegs stands that column
    // — not PlantFlower, not TreeRig.Sway. Same LateUpdate sway
    // as laundry; small amp so the spike nods, not a stem wave.
    private void PlantHollyhocks(int seed, ref int swayWrite)
    {
        if (sway == null)
            return;

        LevelTest1.HollyhockPeg[] pegs = LevelTest1.HollyhockPegs;
        for (int i = 0; i < pegs.Length && swayWrite < sway.Length; i++)
        {
            LevelTest1.HollyhockPeg peg = pegs[i];
            if (peg.x < chunkLeft + 0.10f || peg.x > chunkRight - 0.10f)
                continue;

            float surface = LevelTest1.SurfaceAt(peg.x);
            Transform root = MakeHollyhock(
                new Vector3(peg.x, surface, 0f),
                peg.face,
                Hash01(seed, 5110 + i));
            BindSway(
                swayWrite++,
                root,
                Hash01(seed, 5120 + i) * 6.28f,
                1.5f + Hash01(seed, 5130 + i) * 0.6f,
                0.50f + Hash01(seed, 5140 + i) * 0.16f);
        }
    }

    // Hollyhocks own the tall column; strawberries the low fruit;
    // terracotta pots the clay bloom, so a ROUND cultivated shrub
    // still had no rose. A rose (picture, no collider) on authored
    // pegs sits that bush — not PlantFlower, not TreeRig.Sway.
    // Same LateUpdate sway as laundry; small amp so the bush nods,
    // not a cane wave.
    private void PlantRoses(int seed, ref int swayWrite)
    {
        if (sway == null)
            return;

        LevelTest1.RosePeg[] pegs = LevelTest1.RosePegs;
        for (int i = 0; i < pegs.Length && swayWrite < sway.Length; i++)
        {
            LevelTest1.RosePeg peg = pegs[i];
            if (peg.x < chunkLeft + 0.10f || peg.x > chunkRight - 0.10f)
                continue;

            float surface = LevelTest1.SurfaceAt(peg.x);
            Transform root = MakeRose(
                new Vector3(peg.x, surface, 0f),
                peg.face,
                Hash01(seed, 5210 + i));
            BindSway(
                swayWrite++,
                root,
                Hash01(seed, 5220 + i) * 6.28f,
                1.4f + Hash01(seed, 5230 + i) * 0.5f,
                0.48f + Hash01(seed, 5240 + i) * 0.14f);
        }
    }

    // Hollyhocks own the tall column; roses the round woody shrub;
    // strawberries the low fruit; terracotta pots the clay bloom;
    // docks the rusty weed panicle, so a LOW grey-purple herb
    // border still had no lavender. A lavender (picture, no
    // collider) on authored pegs sits that mound — not PlantFlower,
    // not TreeRig.Sway. Same LateUpdate sway as laundry; small amp
    // so the mound nods, not a spike wave.
    private void PlantLavenders(int seed, ref int swayWrite)
    {
        if (sway == null)
            return;

        LevelTest1.LavenderPeg[] pegs = LevelTest1.LavenderPegs;
        for (int i = 0; i < pegs.Length && swayWrite < sway.Length; i++)
        {
            LevelTest1.LavenderPeg peg = pegs[i];
            if (peg.x < chunkLeft + 0.10f || peg.x > chunkRight - 0.10f)
                continue;

            float surface = LevelTest1.SurfaceAt(peg.x);
            Transform root = MakeLavender(
                new Vector3(peg.x, surface, 0f),
                peg.face,
                Hash01(seed, 5310 + i));
            BindSway(
                swayWrite++,
                root,
                Hash01(seed, 5320 + i) * 6.28f,
                1.3f + Hash01(seed, 5330 + i) * 0.5f,
                0.46f + Hash01(seed, 5340 + i) * 0.14f);
        }
    }

    // Hollyhocks own the tall column; roses the round woody shrub;
    // lavender the low grey-purple herb mound; cattails the wet
    // sausage; ferns the shade pinnae, so a MID sword-leaf flag
    // still had no iris. An iris (picture, no collider) on authored
    // pegs stands that fan — not PlantFlower, not TreeRig.Sway.
    // Same LateUpdate sway as laundry; small amp so the fan nods,
    // not a blade wave.
    private void PlantIrises(int seed, ref int swayWrite)
    {
        if (sway == null)
            return;

        LevelTest1.IrisPeg[] pegs = LevelTest1.IrisPegs;
        for (int i = 0; i < pegs.Length && swayWrite < sway.Length; i++)
        {
            LevelTest1.IrisPeg peg = pegs[i];
            if (peg.x < chunkLeft + 0.10f || peg.x > chunkRight - 0.10f)
                continue;

            float surface = LevelTest1.SurfaceAt(peg.x);
            Transform root = MakeIris(
                new Vector3(peg.x, surface, 0f),
                peg.face,
                Hash01(seed, 5410 + i));
            BindSway(
                swayWrite++,
                root,
                Hash01(seed, 5420 + i) * 6.28f,
                1.35f + Hash01(seed, 5430 + i) * 0.5f,
                0.47f + Hash01(seed, 5440 + i) * 0.14f);
        }
    }

    // Hollyhocks own the tall column; roses the round woody shrub;
    // lavender the low grey-purple herb mound; irises the sword-
    // leaf flag, so a CUP-ON-A-STEM bloom still had no tulip. A
    // tulip (picture, no collider) on authored pegs stands that
    // cup — not PlantFlower, not TreeRig.Sway. Same LateUpdate
    // sway as laundry; small amp so the cup nods, not a stem wave.
    private void PlantTulips(int seed, ref int swayWrite)
    {
        if (sway == null)
            return;

        LevelTest1.TulipPeg[] pegs = LevelTest1.TulipPegs;
        for (int i = 0; i < pegs.Length && swayWrite < sway.Length; i++)
        {
            LevelTest1.TulipPeg peg = pegs[i];
            if (peg.x < chunkLeft + 0.10f || peg.x > chunkRight - 0.10f)
                continue;

            float surface = LevelTest1.SurfaceAt(peg.x);
            Transform root = MakeTulip(
                new Vector3(peg.x, surface, 0f),
                peg.face,
                Hash01(seed, 5510 + i));
            BindSway(
                swayWrite++,
                root,
                Hash01(seed, 5520 + i) * 6.28f,
                1.4f + Hash01(seed, 5530 + i) * 0.5f,
                0.48f + Hash01(seed, 5540 + i) * 0.14f);
        }
    }

    // Hollyhocks own the tall column; roses the round woody shrub;
    // lavender the low grey-purple herb mound; irises the sword-
    // leaf flag; tulips the cup-on-a-stem, so a TRUMPET corona
    // still had no daffodil. A daffodil (picture, no collider) on
    // authored pegs stands that trumpet — not PlantFlower, not
    // TreeRig.Sway. Same LateUpdate sway as laundry; small amp so
    // the corona nods, not a stem wave.
    private void PlantDaffodils(int seed, ref int swayWrite)
    {
        if (sway == null)
            return;

        LevelTest1.DaffodilPeg[] pegs = LevelTest1.DaffodilPegs;
        for (int i = 0; i < pegs.Length && swayWrite < sway.Length; i++)
        {
            LevelTest1.DaffodilPeg peg = pegs[i];
            if (peg.x < chunkLeft + 0.10f || peg.x > chunkRight - 0.10f)
                continue;

            float surface = LevelTest1.SurfaceAt(peg.x);
            Transform root = MakeDaffodil(
                new Vector3(peg.x, surface, 0f),
                peg.face,
                Hash01(seed, 5610 + i));
            BindSway(
                swayWrite++,
                root,
                Hash01(seed, 5620 + i) * 6.28f,
                1.4f + Hash01(seed, 5630 + i) * 0.5f,
                0.48f + Hash01(seed, 5640 + i) * 0.14f);
        }
    }

    // Hollyhocks own the tall column; roses the round woody shrub;
    // lavender the low grey-purple herb mound; irises the sword-
    // leaf flag; tulips the cup-on-a-stem; daffodils the trumpet
    // corona; dandelions the wild clock, so a RAY-DISK bloom still
    // had no daisy. A daisy (picture, no collider) on authored pegs
    // sits that disk — not PlantFlower, not TreeRig.Sway. Same
    // LateUpdate sway as laundry; small amp so the head nods, not
    // a stem wave.
    private void PlantDaisies(int seed, ref int swayWrite)
    {
        if (sway == null)
            return;

        LevelTest1.DaisyPeg[] pegs = LevelTest1.DaisyPegs;
        for (int i = 0; i < pegs.Length && swayWrite < sway.Length; i++)
        {
            LevelTest1.DaisyPeg peg = pegs[i];
            if (peg.x < chunkLeft + 0.10f || peg.x > chunkRight - 0.10f)
                continue;

            float surface = LevelTest1.SurfaceAt(peg.x);
            Transform root = MakeDaisy(
                new Vector3(peg.x, surface, 0f),
                peg.face,
                Hash01(seed, 5710 + i));
            BindSway(
                swayWrite++,
                root,
                Hash01(seed, 5720 + i) * 6.28f,
                1.5f + Hash01(seed, 5730 + i) * 0.5f,
                0.50f + Hash01(seed, 5740 + i) * 0.14f);
        }
    }

    // Hollyhocks own the tall column; roses the round woody shrub;
    // lavender the low grey-purple herb mound; irises the sword-
    // leaf flag; tulips the closed cup; daffodils the trumpet;
    // daisies the ray-disk; dandelions the wild clock, so a PAPER
    // BOWL of four crumpled petals still had no poppy. A poppy
    // (picture, no collider) on authored pegs sits that bowl —
    // not PlantFlower, not TreeRig.Sway. Same LateUpdate sway as
    // laundry; small amp so the head nods, not a stem wave.
    private void PlantPoppies(int seed, ref int swayWrite)
    {
        if (sway == null)
            return;

        LevelTest1.PoppyPeg[] pegs = LevelTest1.PoppyPegs;
        for (int i = 0; i < pegs.Length && swayWrite < sway.Length; i++)
        {
            LevelTest1.PoppyPeg peg = pegs[i];
            if (peg.x < chunkLeft + 0.10f || peg.x > chunkRight - 0.10f)
                continue;

            float surface = LevelTest1.SurfaceAt(peg.x);
            Transform root = MakePoppy(
                new Vector3(peg.x, surface, 0f),
                peg.face,
                Hash01(seed, 5810 + i));
            BindSway(
                swayWrite++,
                root,
                Hash01(seed, 5820 + i) * 6.28f,
                1.4f + Hash01(seed, 5830 + i) * 0.5f,
                0.52f + Hash01(seed, 5840 + i) * 0.14f);
        }
    }

    // Hollyhocks own the tall column; roses the round woody shrub;
    // lavender the low grey-purple herb mound; irises the sword-
    // leaf flag; tulips the closed cup; daffodils the trumpet;
    // daisies the ray-disk; poppies the paper bowl; lily pads the
    // puddle film, so a SIX-POINT flare of pointed tepals still
    // had no lily. A lily (picture, no collider) on authored pegs
    // stands that star — not PlantFlower, not TreeRig.Sway. Same
    // LateUpdate sway as laundry; small amp so the head nods, not
    // a stem wave.
    private void PlantLilies(int seed, ref int swayWrite)
    {
        if (sway == null)
            return;

        LevelTest1.LilyPeg[] pegs = LevelTest1.LilyPegs;
        for (int i = 0; i < pegs.Length && swayWrite < sway.Length; i++)
        {
            LevelTest1.LilyPeg peg = pegs[i];
            if (peg.x < chunkLeft + 0.10f || peg.x > chunkRight - 0.10f)
                continue;

            float surface = LevelTest1.SurfaceAt(peg.x);
            Transform root = MakeLily(
                new Vector3(peg.x, surface, 0f),
                peg.face,
                Hash01(seed, 5910 + i));
            BindSway(
                swayWrite++,
                root,
                Hash01(seed, 5920 + i) * 6.28f,
                1.3f + Hash01(seed, 5930 + i) * 0.5f,
                0.48f + Hash01(seed, 5940 + i) * 0.14f);
        }
    }

    // Hollyhocks own the tall column; roses the round woody shrub;
    // lavender the low grey-purple herb mound; irises the sword-
    // leaf flag; tulips the closed cup; daffodils the trumpet;
    // daisies the small ray-disk; poppies the paper bowl; lilies
    // the six-point flare; dandelions the wild clock, so a GIANT
    // yellow head with a dark seed disk on a thick stalk still
    // had no sunflower. A sunflower (picture, no collider) on
    // authored pegs stands that disk-on-a-stalk — not PlantFlower,
    // not TreeRig.Sway. Same LateUpdate sway as laundry; small amp
    // so the head nods, not a stem wave.
    private void PlantSunflowers(int seed, ref int swayWrite)
    {
        if (sway == null)
            return;

        LevelTest1.SunflowerPeg[] pegs = LevelTest1.SunflowerPegs;
        for (int i = 0; i < pegs.Length && swayWrite < sway.Length; i++)
        {
            LevelTest1.SunflowerPeg peg = pegs[i];
            if (peg.x < chunkLeft + 0.10f || peg.x > chunkRight - 0.10f)
                continue;

            float surface = LevelTest1.SurfaceAt(peg.x);
            Transform root = MakeSunflower(
                new Vector3(peg.x, surface, 0f),
                peg.face,
                Hash01(seed, 6010 + i));
            BindSway(
                swayWrite++,
                root,
                Hash01(seed, 6020 + i) * 6.28f,
                1.2f + Hash01(seed, 6030 + i) * 0.4f,
                0.42f + Hash01(seed, 6040 + i) * 0.12f);
        }
    }

    // Hollyhocks own the tall column; roses the round woody shrub;
    // lavender the grey-purple spike mound; irises the sword-leaf
    // flag; tulips the closed cup; daffodils the trumpet; daisies
    // the small ray-disk; poppies the paper bowl; lilies the
    // six-point flare; sunflowers the giant disk-on-a-stalk, so a
    // LOW five-petal FACE with a yellow throat and heart leaves
    // still had no violet. A violet (picture, no collider) on
    // authored pegs sits that cushion — not PlantFlower, not
    // TreeRig.Sway. Same LateUpdate sway as laundry; small amp
    // so the face nods, not a stem wave.
    private void PlantViolets(int seed, ref int swayWrite)
    {
        if (sway == null)
            return;

        LevelTest1.VioletPeg[] pegs = LevelTest1.VioletPegs;
        for (int i = 0; i < pegs.Length && swayWrite < sway.Length; i++)
        {
            LevelTest1.VioletPeg peg = pegs[i];
            if (peg.x < chunkLeft + 0.10f || peg.x > chunkRight - 0.10f)
                continue;

            float surface = LevelTest1.SurfaceAt(peg.x);
            Transform root = MakeViolet(
                new Vector3(peg.x, surface, 0f),
                peg.face,
                Hash01(seed, 6110 + i));
            BindSway(
                swayWrite++,
                root,
                Hash01(seed, 6120 + i) * 6.28f,
                1.5f + Hash01(seed, 6130 + i) * 0.4f,
                0.55f + Hash01(seed, 6140 + i) * 0.12f);
        }
    }

    // Hollyhocks own the tall column; roses the round woody shrub;
    // lavender the grey-purple spike mound; irises the sword-leaf
    // flag; tulips the closed cup; daffodils the trumpet; daisies
    // the small ray-disk; poppies the paper bowl; lilies the
    // six-point flare; sunflowers the giant disk-on-a-stalk;
    // violets the five-petal face, so a SHORT open GOBLET with
    // grass blades and orange stamens still had no crocus. A
    // crocus (picture, no collider) on authored pegs sits that
    // lawn — not PlantFlower, not TreeRig.Sway. Same LateUpdate
    // sway as laundry; small amp so the cup nods, not a stem wave.
    private void PlantCrocuses(int seed, ref int swayWrite)
    {
        if (sway == null)
            return;

        LevelTest1.CrocusPeg[] pegs = LevelTest1.CrocusPegs;
        for (int i = 0; i < pegs.Length && swayWrite < sway.Length; i++)
        {
            LevelTest1.CrocusPeg peg = pegs[i];
            if (peg.x < chunkLeft + 0.10f || peg.x > chunkRight - 0.10f)
                continue;

            float surface = LevelTest1.SurfaceAt(peg.x);
            Transform root = MakeCrocus(
                new Vector3(peg.x, surface, 0f),
                peg.face,
                Hash01(seed, 6210 + i));
            BindSway(
                swayWrite++,
                root,
                Hash01(seed, 6220 + i) * 6.28f,
                1.6f + Hash01(seed, 6230 + i) * 0.4f,
                0.48f + Hash01(seed, 6240 + i) * 0.12f);
        }
    }

    // Crocuses own the short goblet; violets the five-petal face;
    // lavender the upright spike, so a HANGING cluster of bells on
    // an arching scape still had no bluebell. A bluebell (picture,
    // no collider) on authored pegs sits that damp bank / fence-
    // foot / grove drip — not PlantFlower, not TreeRig.Sway. Same
    // LateUpdate sway as laundry; small amp so the raceme nods,
    // not a stem wave.
    private void PlantBluebells(int seed, ref int swayWrite)
    {
        if (sway == null)
            return;

        LevelTest1.BluebellPeg[] pegs = LevelTest1.BluebellPegs;
        for (int i = 0; i < pegs.Length && swayWrite < sway.Length; i++)
        {
            LevelTest1.BluebellPeg peg = pegs[i];
            if (peg.x < chunkLeft + 0.10f || peg.x > chunkRight - 0.10f)
                continue;

            float surface = LevelTest1.SurfaceAt(peg.x);
            Transform root = MakeBluebell(
                new Vector3(peg.x, surface, 0f),
                peg.face,
                Hash01(seed, 6310 + i));
            BindSway(
                swayWrite++,
                root,
                Hash01(seed, 6320 + i) * 6.28f,
                1.7f + Hash01(seed, 6330 + i) * 0.4f,
                0.50f + Hash01(seed, 6340 + i) * 0.12f);
        }
    }

    // Bluebells own the hanging woodland bell; hollyhocks the
    // bloom column; sunflowers the disk-on-a-stalk; strawberries
    // the low edible bush, so a STAKE with climbing compound
    // leaves and hanging red fruit still had no tomato. A tomato
    // (picture, no collider) on authored pegs sits that kitchen
    // bed — not PlantFlower, not PlantStrawberry, not TreeRig.Sway.
    // Same LateUpdate sway as laundry; small amp so the vine nods,
    // not a cane wave. The stake is on the same pivot.
    private void PlantTomatoes(int seed, ref int swayWrite)
    {
        if (sway == null)
            return;

        LevelTest1.TomatoPeg[] pegs = LevelTest1.TomatoPegs;
        for (int i = 0; i < pegs.Length && swayWrite < sway.Length; i++)
        {
            LevelTest1.TomatoPeg peg = pegs[i];
            if (peg.x < chunkLeft + 0.10f || peg.x > chunkRight - 0.10f)
                continue;

            float surface = LevelTest1.SurfaceAt(peg.x);
            Transform root = MakeTomato(
                new Vector3(peg.x, surface, 0f),
                peg.face,
                Hash01(seed, 6410 + i));
            BindSway(
                swayWrite++,
                root,
                Hash01(seed, 6420 + i) * 6.28f,
                1.4f + Hash01(seed, 6430 + i) * 0.4f,
                0.48f + Hash01(seed, 6440 + i) * 0.12f);
        }
    }

    // Tomatoes own the staked vine; sunflowers the
    // disk-on-a-stalk; hollyhocks the bloom column; mushrooms
    // the toadstool cap, so a WRAPPER of overlapping leaves
    // sitting on the dirt still had no cabbage. A cabbage
    // (picture, no collider) on authored pegs sits that kitchen
    // bed — not PlantFlower, not PlantStrawberry, not TreeRig.Sway.
    // Same LateUpdate sway as laundry; small amp so the head nods,
    // not a roll. The sibling sits on the same pivot.
    private void PlantCabbages(int seed, ref int swayWrite)
    {
        if (sway == null)
            return;

        LevelTest1.CabbagePeg[] pegs = LevelTest1.CabbagePegs;
        for (int i = 0; i < pegs.Length && swayWrite < sway.Length; i++)
        {
            LevelTest1.CabbagePeg peg = pegs[i];
            if (peg.x < chunkLeft + 0.10f || peg.x > chunkRight - 0.10f)
                continue;

            float surface = LevelTest1.SurfaceAt(peg.x);
            Transform root = MakeCabbage(
                new Vector3(peg.x, surface, 0f),
                peg.face,
                Hash01(seed, 6510 + i));
            BindSway(
                swayWrite++,
                root,
                Hash01(seed, 6520 + i) * 6.28f,
                1.2f + Hash01(seed, 6530 + i) * 0.3f,
                0.42f + Hash01(seed, 6540 + i) * 0.10f);
        }
    }

    // Cabbages own the round WRAPPER on dirt; tomatoes the
    // staked vine; sunflowers the disk-on-a-stalk; mushrooms
    // the toadstool cap, so a RIBBED squat gourd sitting on
    // a trailing dirt vine still had no pumpkin. A pumpkin
    // (picture, no collider) on authored pegs sits that kitchen
    // bed — not PlantFlower, not PlantStrawberry, not TreeRig.Sway.
    // Same LateUpdate sway as laundry; small amp so the vine nods,
    // not a roll. The sibling sits on the same pivot.
    private void PlantPumpkins(int seed, ref int swayWrite)
    {
        if (sway == null)
            return;

        LevelTest1.PumpkinPeg[] pegs = LevelTest1.PumpkinPegs;
        for (int i = 0; i < pegs.Length && swayWrite < sway.Length; i++)
        {
            LevelTest1.PumpkinPeg peg = pegs[i];
            if (peg.x < chunkLeft + 0.10f || peg.x > chunkRight - 0.10f)
                continue;

            float surface = LevelTest1.SurfaceAt(peg.x);
            Transform root = MakePumpkin(
                new Vector3(peg.x, surface, 0f),
                peg.face,
                Hash01(seed, 6610 + i));
            BindSway(
                swayWrite++,
                root,
                Hash01(seed, 6620 + i) * 6.28f,
                1.1f + Hash01(seed, 6630 + i) * 0.3f,
                0.40f + Hash01(seed, 6640 + i) * 0.10f);
        }
    }

    // Pumpkins own the ribbed gourd on dirt; cabbages the round
    // WRAPPER; tomatoes the staked vine; sunflowers the
    // disk-on-a-stalk; cattails the brown sausage, so a TALL
    // TASSEL stalk with strap leaves and a husked ear still had
    // no corn. A corn (picture, no collider) on authored pegs
    // stands that kitchen row — not PlantFlower, not
    // PlantStrawberry, not TreeRig.Sway. Same LateUpdate sway as
    // laundry; small amp so the tassel nods, not a cane wave.
    // The sibling sits on the same pivot.
    private void PlantCorns(int seed, ref int swayWrite)
    {
        if (sway == null)
            return;

        LevelTest1.CornPeg[] pegs = LevelTest1.CornPegs;
        for (int i = 0; i < pegs.Length && swayWrite < sway.Length; i++)
        {
            LevelTest1.CornPeg peg = pegs[i];
            if (peg.x < chunkLeft + 0.10f || peg.x > chunkRight - 0.10f)
                continue;

            float surface = LevelTest1.SurfaceAt(peg.x);
            Transform root = MakeCorn(
                new Vector3(peg.x, surface, 0f),
                peg.face,
                Hash01(seed, 6710 + i));
            BindSway(
                swayWrite++,
                root,
                Hash01(seed, 6720 + i) * 6.28f,
                1.2f + Hash01(seed, 6730 + i) * 0.4f,
                0.44f + Hash01(seed, 6740 + i) * 0.12f);
        }
    }

    // Corn owns the tall TASSEL stalk; pumpkins the ribbed
    // gourd; cabbages the round WRAPPER; tomatoes the staked
    // vine; ferns the shade pinna, so a TAPERING orange root
    // in dirt with ferny greens still had no carrot. A carrot
    // (picture, no collider) on authored pegs sits that kitchen
    // row — not PlantFlower, not PlantStrawberry, not
    // TreeRig.Sway. Same LateUpdate sway as laundry; small amp
    // so the greens nod, not a cane wave. The sibling sits on
    // the same pivot.
    private void PlantCarrots(int seed, ref int swayWrite)
    {
        if (sway == null)
            return;

        LevelTest1.CarrotPeg[] pegs = LevelTest1.CarrotPegs;
        for (int i = 0; i < pegs.Length && swayWrite < sway.Length; i++)
        {
            LevelTest1.CarrotPeg peg = pegs[i];
            if (peg.x < chunkLeft + 0.10f || peg.x > chunkRight - 0.10f)
                continue;

            float surface = LevelTest1.SurfaceAt(peg.x);
            Transform root = MakeCarrot(
                new Vector3(peg.x, surface, 0f),
                peg.face,
                Hash01(seed, 6810 + i));
            BindSway(
                swayWrite++,
                root,
                Hash01(seed, 6820 + i) * 6.28f,
                1.3f + Hash01(seed, 6830 + i) * 0.3f,
                0.46f + Hash01(seed, 6840 + i) * 0.12f);
        }
    }

    // Carrots own the TAPERING root; corn the tall TASSEL;
    // tomatoes the staked vine; cattails the brown sausage, so a
    // BLANCHED cylindrical shaft with an upright leaf fan still
    // had no leek. A leek (picture, no collider) on authored pegs
    // stands that kitchen row — not PlantFlower, not
    // PlantStrawberry, not TreeRig.Sway. Same LateUpdate sway as
    // laundry; small amp so the fan nods, not a cane wave. The
    // sibling sits on the same pivot.
    private void PlantLeeks(int seed, ref int swayWrite)
    {
        if (sway == null)
            return;

        LevelTest1.LeekPeg[] pegs = LevelTest1.LeekPegs;
        for (int i = 0; i < pegs.Length && swayWrite < sway.Length; i++)
        {
            LevelTest1.LeekPeg peg = pegs[i];
            if (peg.x < chunkLeft + 0.10f || peg.x > chunkRight - 0.10f)
                continue;

            float surface = LevelTest1.SurfaceAt(peg.x);
            Transform root = MakeLeek(
                new Vector3(peg.x, surface, 0f),
                peg.face,
                Hash01(seed, 6910 + i));
            BindSway(
                swayWrite++,
                root,
                Hash01(seed, 6920 + i) * 6.28f,
                1.2f + Hash01(seed, 6930 + i) * 0.3f,
                0.42f + Hash01(seed, 6940 + i) * 0.12f);
        }
    }

    // Leeks own the BLANCHED spear; tomatoes the staked vine
    // with round red fruit; corn the tall TASSEL; cattails the
    // brown sausage, so a POLE with twining trifoliate leaves
    // and long hanging pods still had no bean. A bean (picture,
    // no collider) on authored pegs climbs that kitchen row —
    // not PlantFlower, not PlantStrawberry, not TreeRig.Sway.
    // Same LateUpdate sway as laundry; small amp so the vine
    // nods, not a cane wave. The sibling sits on the same pivot.
    private void PlantBeans(int seed, ref int swayWrite)
    {
        if (sway == null)
            return;

        LevelTest1.BeanPeg[] pegs = LevelTest1.BeanPegs;
        for (int i = 0; i < pegs.Length && swayWrite < sway.Length; i++)
        {
            LevelTest1.BeanPeg peg = pegs[i];
            if (peg.x < chunkLeft + 0.10f || peg.x > chunkRight - 0.10f)
                continue;

            float surface = LevelTest1.SurfaceAt(peg.x);
            Transform root = MakeBean(
                new Vector3(peg.x, surface, 0f),
                peg.face,
                Hash01(seed, 7010 + i));
            BindSway(
                swayWrite++,
                root,
                Hash01(seed, 7020 + i) * 6.28f,
                1.4f + Hash01(seed, 7030 + i) * 0.4f,
                0.48f + Hash01(seed, 7040 + i) * 0.12f);
        }
    }

    // Beans own the POLE with hanging pods; leeks the
    // BLANCHED spear; carrots the TAPERING cone, so a ROUND
    // papery bulb in dirt with hollow tubular greens still
    // had no onion. An onion (picture, no collider) on
    // authored pegs sits that kitchen row — not
    // PlantFlower, not PlantStrawberry, not TreeRig.Sway.
    // Same LateUpdate sway as laundry; small amp so the
    // tubes nod, not a cane wave. The sibling sits on the
    // same pivot.
    private void PlantOnions(int seed, ref int swayWrite)
    {
        if (sway == null)
            return;

        LevelTest1.OnionPeg[] pegs = LevelTest1.OnionPegs;
        for (int i = 0; i < pegs.Length && swayWrite < sway.Length; i++)
        {
            LevelTest1.OnionPeg peg = pegs[i];
            if (peg.x < chunkLeft + 0.10f || peg.x > chunkRight - 0.10f)
                continue;

            float surface = LevelTest1.SurfaceAt(peg.x);
            Transform root = MakeOnion(
                new Vector3(peg.x, surface, 0f),
                peg.face,
                Hash01(seed, 7110 + i));
            BindSway(
                swayWrite++,
                root,
                Hash01(seed, 7120 + i) * 6.28f,
                1.3f + Hash01(seed, 7130 + i) * 0.3f,
                0.44f + Hash01(seed, 7140 + i) * 0.12f);
        }
    }

    // Onions own the ROUND papery globe; leeks the
    // BLANCHED spear; beans the POLE with hanging pods,
    // so a CLUSTER of cloves under a papery sack still
    // had no garlic. A garlic (picture, no collider) on
    // authored pegs sits that kitchen row — not
    // PlantFlower, not PlantStrawberry, not TreeRig.Sway.
    // Same LateUpdate sway as laundry; small amp so the
    // blades nod, not a cane wave. The sibling sits on
    // the same pivot.
    private void PlantGarlics(int seed, ref int swayWrite)
    {
        if (sway == null)
            return;

        LevelTest1.GarlicPeg[] pegs = LevelTest1.GarlicPegs;
        for (int i = 0; i < pegs.Length && swayWrite < sway.Length; i++)
        {
            LevelTest1.GarlicPeg peg = pegs[i];
            if (peg.x < chunkLeft + 0.10f || peg.x > chunkRight - 0.10f)
                continue;

            float surface = LevelTest1.SurfaceAt(peg.x);
            Transform root = MakeGarlic(
                new Vector3(peg.x, surface, 0f),
                peg.face,
                Hash01(seed, 7210 + i));
            BindSway(
                swayWrite++,
                root,
                Hash01(seed, 7220 + i) * 6.28f,
                1.2f + Hash01(seed, 7230 + i) * 0.3f,
                0.42f + Hash01(seed, 7240 + i) * 0.12f);
        }
    }

    // Garlic owns the CLOVE cluster; onions the ROUND
    // papery globe; carrots the TAPERING cone, so a SMALL
    // ROUND RED root with a white tip and lobed greens
    // still had no radish. A radish (picture, no collider)
    // on authored pegs sits that kitchen row — not
    // PlantFlower, not PlantStrawberry, not TreeRig.Sway.
    // Same LateUpdate sway as laundry; small amp so the
    // lobes nod, not a cane wave. The sibling sits on
    // the same pivot.
    private void PlantRadishes(int seed, ref int swayWrite)
    {
        if (sway == null)
            return;

        LevelTest1.RadishPeg[] pegs = LevelTest1.RadishPegs;
        for (int i = 0; i < pegs.Length && swayWrite < sway.Length; i++)
        {
            LevelTest1.RadishPeg peg = pegs[i];
            if (peg.x < chunkLeft + 0.10f || peg.x > chunkRight - 0.10f)
                continue;

            float surface = LevelTest1.SurfaceAt(peg.x);
            Transform root = MakeRadish(
                new Vector3(peg.x, surface, 0f),
                peg.face,
                Hash01(seed, 7310 + i));
            BindSway(
                swayWrite++,
                root,
                Hash01(seed, 7320 + i) * 6.28f,
                1.25f + Hash01(seed, 7330 + i) * 0.3f,
                0.40f + Hash01(seed, 7340 + i) * 0.12f);
        }
    }

    // Radishes own the SMALL ROUND RED root; onions the
    // ROUND papery globe; carrots the TAPERING cone, so an
    // IRREGULAR brown tuber with eyes and pinnate leaflets
    // still had no potato. A potato (picture, no collider)
    // on authored pegs sits that kitchen row — not
    // PlantFlower, not PlantStrawberry, not TreeRig.Sway.
    // Same LateUpdate sway as laundry; small amp so the
    // leaflets nod, not a cane wave. The sibling sits on
    // the same pivot.
    private void PlantPotatoes(int seed, ref int swayWrite)
    {
        if (sway == null)
            return;

        LevelTest1.PotatoPeg[] pegs = LevelTest1.PotatoPegs;
        for (int i = 0; i < pegs.Length && swayWrite < sway.Length; i++)
        {
            LevelTest1.PotatoPeg peg = pegs[i];
            if (peg.x < chunkLeft + 0.10f || peg.x > chunkRight - 0.10f)
                continue;

            float surface = LevelTest1.SurfaceAt(peg.x);
            Transform root = MakePotato(
                new Vector3(peg.x, surface, 0f),
                peg.face,
                Hash01(seed, 7410 + i));
            BindSway(
                swayWrite++,
                root,
                Hash01(seed, 7420 + i) * 6.28f,
                1.15f + Hash01(seed, 7430 + i) * 0.3f,
                0.38f + Hash01(seed, 7440 + i) * 0.12f);
        }
    }

    // Potatoes own the IRREGULAR brown tuber; radishes
    // the SMALL ROUND RED root; onions the ROUND papery
    // globe, so a ROUND purple crown with RED-VEINED
    // greens still had no beet. A beet (picture, no
    // collider) on authored pegs sits that kitchen row —
    // not PlantFlower, not PlantStrawberry, not
    // TreeRig.Sway. Same LateUpdate sway as laundry; small
    // amp so the leaves nod, not a cane wave. The sibling
    // sits on the same pivot.
    private void PlantBeets(int seed, ref int swayWrite)
    {
        if (sway == null)
            return;

        LevelTest1.BeetPeg[] pegs = LevelTest1.BeetPegs;
        for (int i = 0; i < pegs.Length && swayWrite < sway.Length; i++)
        {
            LevelTest1.BeetPeg peg = pegs[i];
            if (peg.x < chunkLeft + 0.10f || peg.x > chunkRight - 0.10f)
                continue;

            float surface = LevelTest1.SurfaceAt(peg.x);
            Transform root = MakeBeet(
                new Vector3(peg.x, surface, 0f),
                peg.face,
                Hash01(seed, 7510 + i));
            BindSway(
                swayWrite++,
                root,
                Hash01(seed, 7520 + i) * 6.28f,
                1.18f + Hash01(seed, 7530 + i) * 0.3f,
                0.40f + Hash01(seed, 7540 + i) * 0.12f);
        }
    }

    // Beets own the ROUND purple crown; carrots the
    // ORANGE taper; potatoes the IRREGULAR brown tuber,
    // so a CREAMY wide cone with celery-like pinnate
    // greens still had no parsnip. A parsnip (picture,
    // no collider) on authored pegs sits that kitchen
    // row — not PlantFlower, not PlantStrawberry, not
    // TreeRig.Sway. Same LateUpdate sway as laundry;
    // small amp so the leaflets nod, not a cane wave.
    // The sibling sits on the same pivot.
    private void PlantParsnips(int seed, ref int swayWrite)
    {
        if (sway == null)
            return;

        LevelTest1.ParsnipPeg[] pegs = LevelTest1.ParsnipPegs;
        for (int i = 0; i < pegs.Length && swayWrite < sway.Length; i++)
        {
            LevelTest1.ParsnipPeg peg = pegs[i];
            if (peg.x < chunkLeft + 0.10f || peg.x > chunkRight - 0.10f)
                continue;

            float surface = LevelTest1.SurfaceAt(peg.x);
            Transform root = MakeParsnip(
                new Vector3(peg.x, surface, 0f),
                peg.face,
                Hash01(seed, 7610 + i));
            BindSway(
                swayWrite++,
                root,
                Hash01(seed, 7620 + i) * 6.28f,
                1.16f + Hash01(seed, 7630 + i) * 0.3f,
                0.39f + Hash01(seed, 7640 + i) * 0.12f);
        }
    }

    // Parsnips own the CREAMY wide cone; beets the ROUND
    // purple crown; carrots the ORANGE taper, so a ROUND
    // WHITE globe with a PURPLE shoulder and lobed greens
    // still had no turnip. A turnip (picture, no collider)
    // on authored pegs sits that kitchen row — not
    // PlantFlower, not PlantStrawberry, not TreeRig.Sway.
    // Same LateUpdate sway as laundry; small amp so the
    // lobes nod, not a cane wave. The sibling sits on
    // the same pivot.
    private void PlantTurnips(int seed, ref int swayWrite)
    {
        if (sway == null)
            return;

        LevelTest1.TurnipPeg[] pegs = LevelTest1.TurnipPegs;
        for (int i = 0; i < pegs.Length && swayWrite < sway.Length; i++)
        {
            LevelTest1.TurnipPeg peg = pegs[i];
            if (peg.x < chunkLeft + 0.10f || peg.x > chunkRight - 0.10f)
                continue;

            float surface = LevelTest1.SurfaceAt(peg.x);
            Transform root = MakeTurnip(
                new Vector3(peg.x, surface, 0f),
                peg.face,
                Hash01(seed, 7710 + i));
            BindSway(
                swayWrite++,
                root,
                Hash01(seed, 7720 + i) * 6.28f,
                1.14f + Hash01(seed, 7730 + i) * 0.3f,
                0.38f + Hash01(seed, 7740 + i) * 0.12f);
        }
    }

    // Turnips own the ROUND WHITE globe with a purple
    // shoulder; parsnips the CREAMY wide cone; beets the
    // ROUND purple crown, so a LARGER YELLOW globe with a
    // PURPLE cap and glaucous collard greens still had no
    // rutabaga. A rutabaga (picture, no collider) on
    // authored pegs sits that kitchen row — not
    // PlantFlower, not PlantStrawberry, not TreeRig.Sway.
    // Same LateUpdate sway as laundry; small amp so the
    // paddles nod, not a cane wave. The sibling sits on
    // the same pivot.
    private void PlantRutabagas(int seed, ref int swayWrite)
    {
        if (sway == null)
            return;

        LevelTest1.RutabagaPeg[] pegs = LevelTest1.RutabagaPegs;
        for (int i = 0; i < pegs.Length && swayWrite < sway.Length; i++)
        {
            LevelTest1.RutabagaPeg peg = pegs[i];
            if (peg.x < chunkLeft + 0.10f || peg.x > chunkRight - 0.10f)
                continue;

            float surface = LevelTest1.SurfaceAt(peg.x);
            Transform root = MakeRutabaga(
                new Vector3(peg.x, surface, 0f),
                peg.face,
                Hash01(seed, 7810 + i));
            BindSway(
                swayWrite++,
                root,
                Hash01(seed, 7820 + i) * 6.28f,
                1.12f + Hash01(seed, 7830 + i) * 0.3f,
                0.36f + Hash01(seed, 7840 + i) * 0.12f);
        }
    }

    // Rutabagas own the LARGER YELLOW globe in dirt with a
    // purple cap; turnips the ROUND WHITE globe; beets the
    // ROUND purple crown, so a PALE-GREEN swollen STEM
    // sitting ON the slab with long petioles from the crown
    // still had no kohlrabi. A kohlrabi (picture, no
    // collider) on authored pegs sits that kitchen row —
    // not PlantFlower, not PlantStrawberry, not
    // TreeRig.Sway. Same LateUpdate sway as laundry; small
    // amp so the blades nod, not a cane wave. The sibling
    // sits on the same pivot.
    private void PlantKohlrabis(int seed, ref int swayWrite)
    {
        if (sway == null)
            return;

        LevelTest1.KohlrabiPeg[] pegs = LevelTest1.KohlrabiPegs;
        for (int i = 0; i < pegs.Length && swayWrite < sway.Length; i++)
        {
            LevelTest1.KohlrabiPeg peg = pegs[i];
            if (peg.x < chunkLeft + 0.10f || peg.x > chunkRight - 0.10f)
                continue;

            float surface = LevelTest1.SurfaceAt(peg.x);
            Transform root = MakeKohlrabi(
                new Vector3(peg.x, surface, 0f),
                peg.face,
                Hash01(seed, 7910 + i));
            BindSway(
                swayWrite++,
                root,
                Hash01(seed, 7920 + i) * 6.28f,
                1.10f + Hash01(seed, 7930 + i) * 0.3f,
                0.34f + Hash01(seed, 7940 + i) * 0.12f);
        }
    }

    // Kohlrabis own the pale-green swollen STEM ON the
    // slab; leeks the blanched SPEAR; cabbages the round
    // WRAPPER, so RIBBED upright stalks sitting ON the
    // slab with compound leaflets at the crown still had
    // no celery. A celery (picture, no collider) on
    // authored pegs sits that kitchen row — not
    // PlantFlower, not PlantStrawberry, not TreeRig.Sway.
    // Same LateUpdate sway as laundry; small amp so the
    // stalks nod, not a cane wave. The sibling sits on the
    // same pivot.
    private void PlantCeleries(int seed, ref int swayWrite)
    {
        if (sway == null)
            return;

        LevelTest1.CeleryPeg[] pegs = LevelTest1.CeleryPegs;
        for (int i = 0; i < pegs.Length && swayWrite < sway.Length; i++)
        {
            LevelTest1.CeleryPeg peg = pegs[i];
            if (peg.x < chunkLeft + 0.10f || peg.x > chunkRight - 0.10f)
                continue;

            float surface = LevelTest1.SurfaceAt(peg.x);
            Transform root = MakeCelery(
                new Vector3(peg.x, surface, 0f),
                peg.face,
                Hash01(seed, 8010 + i));
            BindSway(
                swayWrite++,
                root,
                Hash01(seed, 8020 + i) * 6.28f,
                1.10f + Hash01(seed, 8030 + i) * 0.3f,
                0.34f + Hash01(seed, 8040 + i) * 0.12f);
        }
    }

    // Celeries own the RIBBED upright stalks ON the slab;
    // leeks the blanched SPEAR; kohlrabis the pale-green
    // swollen STEM, so a SWOLLEN PALE bulb sitting ON the
    // slab with a dill-like feathery crown still had no
    // fennel. A fennel (picture, no collider) on authored
    // pegs sits that kitchen row — not PlantFlower, not
    // PlantStrawberry, not TreeRig.Sway. Same LateUpdate
    // sway as laundry; small amp so the fronds nod, not
    // a cane wave. The sibling sits on the same pivot.
    private void PlantFennels(int seed, ref int swayWrite)
    {
        if (sway == null)
            return;

        LevelTest1.FennelPeg[] pegs = LevelTest1.FennelPegs;
        for (int i = 0; i < pegs.Length && swayWrite < sway.Length; i++)
        {
            LevelTest1.FennelPeg peg = pegs[i];
            if (peg.x < chunkLeft + 0.10f || peg.x > chunkRight - 0.10f)
                continue;

            float surface = LevelTest1.SurfaceAt(peg.x);
            Transform root = MakeFennel(
                new Vector3(peg.x, surface, 0f),
                peg.face,
                Hash01(seed, 8110 + i));
            BindSway(
                swayWrite++,
                root,
                Hash01(seed, 8120 + i) * 6.28f,
                1.10f + Hash01(seed, 8130 + i) * 0.3f,
                0.34f + Hash01(seed, 8140 + i) * 0.12f);
        }
    }

    // Fennels own the SWOLLEN PALE bulb ON the slab;
    // leeks the blanched SPEAR; celeries the RIBBED
    // upright stalks, so THIN GREEN spears from a SCALY
    // crown sitting ON the slab still had no asparagus.
    // An asparagus (picture, no collider) on authored
    // pegs sits that kitchen row — not PlantFlower, not
    // PlantStrawberry, not TreeRig.Sway. Same LateUpdate
    // sway as laundry; small amp so the spears nod, not
    // a cane wave. The sibling sits on the same pivot.
    private void PlantAsparagus(int seed, ref int swayWrite)
    {
        if (sway == null)
            return;

        LevelTest1.AsparagusPeg[] pegs = LevelTest1.AsparagusPegs;
        for (int i = 0; i < pegs.Length && swayWrite < sway.Length; i++)
        {
            LevelTest1.AsparagusPeg peg = pegs[i];
            if (peg.x < chunkLeft + 0.10f || peg.x > chunkRight - 0.10f)
                continue;

            float surface = LevelTest1.SurfaceAt(peg.x);
            Transform root = MakeAsparagus(
                new Vector3(peg.x, surface, 0f),
                peg.face,
                Hash01(seed, 8210 + i));
            BindSway(
                swayWrite++,
                root,
                Hash01(seed, 8220 + i) * 6.28f,
                1.10f + Hash01(seed, 8230 + i) * 0.3f,
                0.34f + Hash01(seed, 8240 + i) * 0.12f);
        }
    }

    // Asparagus owns the THIN GREEN spears ON the slab;
    // leeks the blanched SPEAR; fennels the SWOLLEN PALE
    // bulb, so THICK RED stalks with a HUGE wrinkled leaf
    // sitting ON the slab still had no rhubarb. A rhubarb
    // (picture, no collider) on authored pegs sits that
    // kitchen row — not PlantFlower, not PlantStrawberry,
    // not TreeRig.Sway. Same LateUpdate sway as laundry;
    // small amp so the leaves nod, not a cane wave. The
    // sibling sits on the same pivot.
    private void PlantRhubarbs(int seed, ref int swayWrite)
    {
        if (sway == null)
            return;

        LevelTest1.RhubarbPeg[] pegs = LevelTest1.RhubarbPegs;
        for (int i = 0; i < pegs.Length && swayWrite < sway.Length; i++)
        {
            LevelTest1.RhubarbPeg peg = pegs[i];
            if (peg.x < chunkLeft + 0.10f || peg.x > chunkRight - 0.10f)
                continue;

            float surface = LevelTest1.SurfaceAt(peg.x);
            Transform root = MakeRhubarb(
                new Vector3(peg.x, surface, 0f),
                peg.face,
                Hash01(seed, 8310 + i));
            BindSway(
                swayWrite++,
                root,
                Hash01(seed, 8320 + i) * 6.28f,
                1.10f + Hash01(seed, 8330 + i) * 0.3f,
                0.34f + Hash01(seed, 8340 + i) * 0.12f);
        }
    }

    // Rhubarbs own the THICK RED stalks; cabbage the
    // ROUND WRAPPER head; asparagus the THIN GREEN
    // spears, so CORRUGATED BLUE-GREEN standing leaves
    // in a ROSETTE sitting ON the slab still had no
    // kale. A kale (picture, no collider) on authored
    // pegs sits that kitchen row — not PlantFlower, not
    // PlantStrawberry, not TreeRig.Sway. Same LateUpdate
    // sway as laundry; small amp so the ruffles nod, not
    // a cane wave. The sibling sits on the same pivot.
    private void PlantKales(int seed, ref int swayWrite)
    {
        if (sway == null)
            return;

        LevelTest1.KalePeg[] pegs = LevelTest1.KalePegs;
        for (int i = 0; i < pegs.Length && swayWrite < sway.Length; i++)
        {
            LevelTest1.KalePeg peg = pegs[i];
            if (peg.x < chunkLeft + 0.10f || peg.x > chunkRight - 0.10f)
                continue;

            float surface = LevelTest1.SurfaceAt(peg.x);
            Transform root = MakeKale(
                new Vector3(peg.x, surface, 0f),
                peg.face,
                Hash01(seed, 8410 + i));
            BindSway(
                swayWrite++,
                root,
                Hash01(seed, 8420 + i) * 6.28f,
                1.10f + Hash01(seed, 8430 + i) * 0.3f,
                0.34f + Hash01(seed, 8440 + i) * 0.12f);
        }
    }

    // Kale owns the CORRUGATED BLUE-GREEN rosette;
    // rhubarb the THICK RED stalks; cabbage the ROUND
    // WRAPPER, so BRIGHT stalks with a LOBED leaf sitting
    // ON the slab still had no chard. A chard (picture,
    // no collider) on authored pegs sits that kitchen
    // row — not PlantFlower, not PlantStrawberry, not
    // TreeRig.Sway. Same LateUpdate sway as laundry;
    // small amp so the paddles nod, not a cane wave.
    // The sibling sits on the same pivot.
    private void PlantChards(int seed, ref int swayWrite)
    {
        if (sway == null)
            return;

        LevelTest1.ChardPeg[] pegs = LevelTest1.ChardPegs;
        for (int i = 0; i < pegs.Length && swayWrite < sway.Length; i++)
        {
            LevelTest1.ChardPeg peg = pegs[i];
            if (peg.x < chunkLeft + 0.10f || peg.x > chunkRight - 0.10f)
                continue;

            float surface = LevelTest1.SurfaceAt(peg.x);
            Transform root = MakeChard(
                new Vector3(peg.x, surface, 0f),
                peg.face,
                Hash01(seed, 8510 + i));
            BindSway(
                swayWrite++,
                root,
                Hash01(seed, 8520 + i) * 6.28f,
                1.10f + Hash01(seed, 8530 + i) * 0.3f,
                0.34f + Hash01(seed, 8540 + i) * 0.12f);
        }
    }

    // Chard owns the BRIGHT stalks with a LOBED leaf;
    // kale the CORRUGATED BLUE-GREEN standing rosette;
    // cabbage the ROUND WRAPPER, so SMOOTH DARK-GREEN
    // leaves in a LOW rosette sitting ON the slab still
    // had no spinach. A spinach (picture, no collider)
    // on authored pegs sits that kitchen row — not
    // PlantFlower, not PlantStrawberry, not TreeRig.Sway.
    // Same LateUpdate sway as laundry; small amp so the
    // flats nod, not a cane wave. The sibling sits on
    // the same pivot.
    private void PlantSpinaches(int seed, ref int swayWrite)
    {
        if (sway == null)
            return;

        LevelTest1.SpinachPeg[] pegs = LevelTest1.SpinachPegs;
        for (int i = 0; i < pegs.Length && swayWrite < sway.Length; i++)
        {
            LevelTest1.SpinachPeg peg = pegs[i];
            if (peg.x < chunkLeft + 0.10f || peg.x > chunkRight - 0.10f)
                continue;

            float surface = LevelTest1.SurfaceAt(peg.x);
            Transform root = MakeSpinach(
                new Vector3(peg.x, surface, 0f),
                peg.face,
                Hash01(seed, 8610 + i));
            BindSway(
                swayWrite++,
                root,
                Hash01(seed, 8620 + i) * 6.28f,
                1.10f + Hash01(seed, 8630 + i) * 0.3f,
                0.34f + Hash01(seed, 8640 + i) * 0.12f);
        }
    }

    // Spinach owns the SMOOTH DARK-GREEN LOW rosette;
    // cabbage the TIGHT ROUND WRAPPER; kale the
    // CORRUGATED BLUE-GREEN standing rosette, so a LOOSE
    // PALE-GREEN head sitting ON the slab still had no
    // lettuce. A lettuce (picture, no collider) on
    // authored pegs sits that kitchen row — not
    // PlantFlower, not PlantStrawberry, not TreeRig.Sway.
    // Same LateUpdate sway as laundry; small amp so the
    // cups nod, not a cane wave. The sibling sits on
    // the same pivot.
    private void PlantLettuces(int seed, ref int swayWrite)
    {
        if (sway == null)
            return;

        LevelTest1.LettucePeg[] pegs = LevelTest1.LettucePegs;
        for (int i = 0; i < pegs.Length && swayWrite < sway.Length; i++)
        {
            LevelTest1.LettucePeg peg = pegs[i];
            if (peg.x < chunkLeft + 0.10f || peg.x > chunkRight - 0.10f)
                continue;

            float surface = LevelTest1.SurfaceAt(peg.x);
            Transform root = MakeLettuce(
                new Vector3(peg.x, surface, 0f),
                peg.face,
                Hash01(seed, 8710 + i));
            BindSway(
                swayWrite++,
                root,
                Hash01(seed, 8720 + i) * 6.28f,
                1.10f + Hash01(seed, 8730 + i) * 0.3f,
                0.34f + Hash01(seed, 8740 + i) * 0.12f);
        }
    }

    // Lettuce owns the LOOSE PALE-GREEN cup; cabbage
    // the TIGHT ROUND WRAPPER; spinach the SMOOTH
    // DARK-GREEN LOW rosette, so a TIGHT GREEN floret
    // head on a THICK stem sitting ON the slab still
    // had no broccoli. A broccoli (picture, no
    // collider) on authored pegs sits that kitchen row
    // — not PlantFlower, not PlantStrawberry, not
    // TreeRig.Sway. Same LateUpdate sway as laundry;
    // small amp so the stem nods, not a cane wave.
    // The sibling sits on the same pivot.
    private void PlantBroccolis(int seed, ref int swayWrite)
    {
        if (sway == null)
            return;

        LevelTest1.BroccoliPeg[] pegs = LevelTest1.BroccoliPegs;
        for (int i = 0; i < pegs.Length && swayWrite < sway.Length; i++)
        {
            LevelTest1.BroccoliPeg peg = pegs[i];
            if (peg.x < chunkLeft + 0.10f || peg.x > chunkRight - 0.10f)
                continue;

            float surface = LevelTest1.SurfaceAt(peg.x);
            Transform root = MakeBroccoli(
                new Vector3(peg.x, surface, 0f),
                peg.face,
                Hash01(seed, 8810 + i));
            BindSway(
                swayWrite++,
                root,
                Hash01(seed, 8820 + i) * 6.28f,
                1.10f + Hash01(seed, 8830 + i) * 0.3f,
                0.34f + Hash01(seed, 8840 + i) * 0.12f);
        }
    }

    // Broccoli owns the TIGHT GREEN floret on a THICK
    // stem; cabbage the TIGHT ROUND WRAPPER; lettuce the
    // LOOSE PALE-GREEN cup, so a TIGHT CREAMY-WHITE
    // curd on a THICK stem sitting ON the slab still
    // had no cauliflower. A cauliflower (picture, no
    // collider) on authored pegs sits that kitchen row
    // — not PlantFlower, not PlantStrawberry, not
    // TreeRig.Sway. Same LateUpdate sway as laundry;
    // small amp so the stem nods, not a cane wave.
    // The sibling sits on the same pivot.
    private void PlantCauliflowers(int seed, ref int swayWrite)
    {
        if (sway == null)
            return;

        LevelTest1.CauliflowerPeg[] pegs = LevelTest1.CauliflowerPegs;
        for (int i = 0; i < pegs.Length && swayWrite < sway.Length; i++)
        {
            LevelTest1.CauliflowerPeg peg = pegs[i];
            if (peg.x < chunkLeft + 0.10f || peg.x > chunkRight - 0.10f)
                continue;

            float surface = LevelTest1.SurfaceAt(peg.x);
            Transform root = MakeCauliflower(
                new Vector3(peg.x, surface, 0f),
                peg.face,
                Hash01(seed, 8910 + i));
            BindSway(
                swayWrite++,
                root,
                Hash01(seed, 8920 + i) * 6.28f,
                1.10f + Hash01(seed, 8930 + i) * 0.3f,
                0.34f + Hash01(seed, 8940 + i) * 0.12f);
        }
    }

    // Broccoli owns the TIGHT GREEN floret dome;
    // cauliflower the TIGHT CREAMY-WHITE curd; cabbage
    // the TIGHT ROUND WRAPPER, so TIGHT GREEN sprouts
    // along a TALL stem sitting ON the slab still had
    // no Brussels. A Brussels (picture, no collider)
    // on authored pegs sits that kitchen row — not
    // PlantFlower, not PlantStrawberry, not
    // TreeRig.Sway. Same LateUpdate sway as laundry;
    // small amp so the stem nods, not a cane wave.
    // The sibling sits on the same pivot.
    private void PlantBrussels(int seed, ref int swayWrite)
    {
        if (sway == null)
            return;

        LevelTest1.BrusselsPeg[] pegs = LevelTest1.BrusselsPegs;
        for (int i = 0; i < pegs.Length && swayWrite < sway.Length; i++)
        {
            LevelTest1.BrusselsPeg peg = pegs[i];
            if (peg.x < chunkLeft + 0.10f || peg.x > chunkRight - 0.10f)
                continue;

            float surface = LevelTest1.SurfaceAt(peg.x);
            Transform root = MakeBrussels(
                new Vector3(peg.x, surface, 0f),
                peg.face,
                Hash01(seed, 9010 + i));
            BindSway(
                swayWrite++,
                root,
                Hash01(seed, 9020 + i) * 6.28f,
                1.10f + Hash01(seed, 9030 + i) * 0.3f,
                0.34f + Hash01(seed, 9040 + i) * 0.12f);
        }
    }

    // Broccoli owns the TIGHT GREEN floret dome;
    // cauliflower the TIGHT CREAMY-WHITE curd;
    // Brussels the TIGHT GREEN sprouts along a TALL
    // stem; thistle the PURPLE bloom on the dry verge,
    // so a SCALY edible bud on a THICK stem sitting ON
    // the slab still had no artichoke. An artichoke
    // (picture, no collider) on authored pegs sits that
    // kitchen row — not PlantFlower, not
    // PlantStrawberry, not TreeRig.Sway. Same
    // LateUpdate sway as laundry; small amp so the
    // stem nods, not a cane wave. The sibling sits on
    // the same pivot.
    private void PlantArtichokes(int seed, ref int swayWrite)
    {
        if (sway == null)
            return;

        LevelTest1.ArtichokePeg[] pegs = LevelTest1.ArtichokePegs;
        for (int i = 0; i < pegs.Length && swayWrite < sway.Length; i++)
        {
            LevelTest1.ArtichokePeg peg = pegs[i];
            if (peg.x < chunkLeft + 0.10f || peg.x > chunkRight - 0.10f)
                continue;

            float surface = LevelTest1.SurfaceAt(peg.x);
            Transform root = MakeArtichoke(
                new Vector3(peg.x, surface, 0f),
                peg.face,
                Hash01(seed, 9110 + i));
            BindSway(
                swayWrite++,
                root,
                Hash01(seed, 9120 + i) * 6.28f,
                1.10f + Hash01(seed, 9130 + i) * 0.3f,
                0.34f + Hash01(seed, 9140 + i) * 0.12f);
        }
    }

    // Artichoke owns the SCALY edible bud on a THICK
    // stem; tomato the ROUND RED ball on a stake vine;
    // thistle the PURPLE bloom on the dry verge, so a
    // GLOSSY PURPLE teardrop fruit on a stem sitting ON
    // the slab still had no eggplant. An eggplant
    // (picture, no collider) on authored pegs sits that
    // kitchen row — not PlantFlower, not
    // PlantStrawberry, not TreeRig.Sway. Same
    // LateUpdate sway as laundry; small amp so the
    // stem nods, not a cane wave. The sibling sits on
    // the same pivot.
    private void PlantEggplants(int seed, ref int swayWrite)
    {
        if (sway == null)
            return;

        LevelTest1.EggplantPeg[] pegs = LevelTest1.EggplantPegs;
        for (int i = 0; i < pegs.Length && swayWrite < sway.Length; i++)
        {
            LevelTest1.EggplantPeg peg = pegs[i];
            if (peg.x < chunkLeft + 0.10f || peg.x > chunkRight - 0.10f)
                continue;

            float surface = LevelTest1.SurfaceAt(peg.x);
            Transform root = MakeEggplant(
                new Vector3(peg.x, surface, 0f),
                peg.face,
                Hash01(seed, 9210 + i));
            BindSway(
                swayWrite++,
                root,
                Hash01(seed, 9220 + i) * 6.28f,
                1.10f + Hash01(seed, 9230 + i) * 0.3f,
                0.34f + Hash01(seed, 9240 + i) * 0.12f);
        }
    }

    // Eggplant owns the GLOSSY PURPLE teardrop on a
    // stem; tomato the ROUND RED ball on a stake vine;
    // artichoke the SCALY edible bud on a THICK stem, so
    // a GLOSSY BLOCKY pod sitting ON the slab still had
    // no pepper. A pepper (picture, no collider) on
    // authored pegs sits that kitchen row — not
    // PlantFlower, not PlantStrawberry, not
    // TreeRig.Sway. Same LateUpdate sway as laundry;
    // small amp so the stem nods, not a cane wave. The
    // sibling sits on the same pivot.
    private void PlantPeppers(int seed, ref int swayWrite)
    {
        if (sway == null)
            return;

        LevelTest1.PepperPeg[] pegs = LevelTest1.PepperPegs;
        for (int i = 0; i < pegs.Length && swayWrite < sway.Length; i++)
        {
            LevelTest1.PepperPeg peg = pegs[i];
            if (peg.x < chunkLeft + 0.10f || peg.x > chunkRight - 0.10f)
                continue;

            float surface = LevelTest1.SurfaceAt(peg.x);
            Transform root = MakePepper(
                new Vector3(peg.x, surface, 0f),
                peg.face,
                Hash01(seed, 9310 + i));
            BindSway(
                swayWrite++,
                root,
                Hash01(seed, 9320 + i) * 6.28f,
                1.10f + Hash01(seed, 9330 + i) * 0.3f,
                0.34f + Hash01(seed, 9340 + i) * 0.12f);
        }
    }

    // Pepper owns the GLOSSY BLOCKY pod on a stem;
    // tomato the ROUND RED ball on a stake vine;
    // eggplant the GLOSSY PURPLE hanging teardrop, so
    // a LONG GREEN fruit on a vine sitting ON the slab
    // still had no cucumber. A cucumber (picture, no
    // collider) on authored pegs sits that kitchen row
    // — not PlantFlower, not PlantStrawberry, not
    // TreeRig.Sway. Same LateUpdate sway as laundry;
    // small amp so the vine nods, not a cane wave. The
    // sibling sits on the same pivot.
    private void PlantCucumbers(int seed, ref int swayWrite)
    {
        if (sway == null)
            return;

        LevelTest1.CucumberPeg[] pegs = LevelTest1.CucumberPegs;
        for (int i = 0; i < pegs.Length && swayWrite < sway.Length; i++)
        {
            LevelTest1.CucumberPeg peg = pegs[i];
            if (peg.x < chunkLeft + 0.10f || peg.x > chunkRight - 0.10f)
                continue;

            float surface = LevelTest1.SurfaceAt(peg.x);
            Transform root = MakeCucumber(
                new Vector3(peg.x, surface, 0f),
                peg.face,
                Hash01(seed, 9350 + i));
            BindSway(
                swayWrite++,
                root,
                Hash01(seed, 9360 + i) * 6.28f,
                1.10f + Hash01(seed, 9370 + i) * 0.3f,
                0.34f + Hash01(seed, 9380 + i) * 0.12f);
        }
    }

    // Cucumber owns the LONG GREEN fruit along a vine
    // ON the slab; pumpkin the RIBBED gourd on a dirt
    // vine; pepper the GLOSSY BLOCKY pod, so a NETTED
    // ROUND fruit on a vine sitting ON the slab still
    // had no melon. A melon (picture, no collider) on
    // authored pegs sits that kitchen row — not
    // PlantFlower, not PlantStrawberry, not
    // TreeRig.Sway. Same LateUpdate sway as laundry;
    // small amp so the vine nods, not a cane wave. The
    // sibling sits on the same pivot.
    private void PlantMelons(int seed, ref int swayWrite)
    {
        if (sway == null)
            return;

        LevelTest1.MelonPeg[] pegs = LevelTest1.MelonPegs;
        for (int i = 0; i < pegs.Length && swayWrite < sway.Length; i++)
        {
            LevelTest1.MelonPeg peg = pegs[i];
            if (peg.x < chunkLeft + 0.10f || peg.x > chunkRight - 0.10f)
                continue;

            float surface = LevelTest1.SurfaceAt(peg.x);
            Transform root = MakeMelon(
                new Vector3(peg.x, surface, 0f),
                peg.face,
                Hash01(seed, 9450 + i));
            BindSway(
                swayWrite++,
                root,
                Hash01(seed, 9460 + i) * 6.28f,
                1.10f + Hash01(seed, 9470 + i) * 0.3f,
                0.34f + Hash01(seed, 9480 + i) * 0.12f);
        }
    }

    // Melon owns the NETTED ROUND fruit on a vine
    // ON the slab; cucumber the LONG GREEN fruit;
    // pumpkin the RIBBED gourd on a dirt vine, so a
    // STRIPED OVAL fruit on a vine sitting ON the
    // slab still had no watermelon. A watermelon
    // (picture, no collider) on authored pegs sits
    // that kitchen row — not PlantFlower, not
    // PlantStrawberry, not TreeRig.Sway. Same
    // LateUpdate sway as laundry; small amp so the
    // vine nods, not a cane wave. The sibling sits
    // on the same pivot.
    private void PlantWatermelons(int seed, ref int swayWrite)
    {
        if (sway == null)
            return;

        LevelTest1.WatermelonPeg[] pegs = LevelTest1.WatermelonPegs;
        for (int i = 0; i < pegs.Length && swayWrite < sway.Length; i++)
        {
            LevelTest1.WatermelonPeg peg = pegs[i];
            if (peg.x < chunkLeft + 0.10f || peg.x > chunkRight - 0.10f)
                continue;

            float surface = LevelTest1.SurfaceAt(peg.x);
            Transform root = MakeWatermelon(
                new Vector3(peg.x, surface, 0f),
                peg.face,
                Hash01(seed, 9550 + i));
            BindSway(
                swayWrite++,
                root,
                Hash01(seed, 9560 + i) * 6.28f,
                1.10f + Hash01(seed, 9570 + i) * 0.3f,
                0.34f + Hash01(seed, 9580 + i) * 0.12f);
        }
    }

    // Watermelon owns the STRIPED OVAL fruit on a
    // vine ON the slab; melon the NETTED ROUND fruit;
    // pumpkin the RIBBED gourd on a dirt vine, so a
    // YELLOW HOOK fruit on a vine sitting ON the slab
    // still had no zucchini. A zucchini (picture, no
    // collider) on authored pegs sits that kitchen
    // row — not PlantFlower, not PlantStrawberry, not
    // TreeRig.Sway. Same LateUpdate sway as laundry;
    // small amp so the vine nods, not a cane wave.
    // The sibling sits on the same pivot.
    private void PlantZucchinis(int seed, ref int swayWrite)
    {
        if (sway == null)
            return;

        LevelTest1.ZucchiniPeg[] pegs = LevelTest1.ZucchiniPegs;
        for (int i = 0; i < pegs.Length && swayWrite < sway.Length; i++)
        {
            LevelTest1.ZucchiniPeg peg = pegs[i];
            if (peg.x < chunkLeft + 0.10f || peg.x > chunkRight - 0.10f)
                continue;

            float surface = LevelTest1.SurfaceAt(peg.x);
            Transform root = MakeZucchini(
                new Vector3(peg.x, surface, 0f),
                peg.face,
                Hash01(seed, 9590 + i));
            BindSway(
                swayWrite++,
                root,
                Hash01(seed, 9600 + i) * 6.28f,
                1.10f + Hash01(seed, 9610 + i) * 0.3f,
                0.34f + Hash01(seed, 9620 + i) * 0.12f);
        }
    }

    // Zucchini owns the YELLOW HOOK fruit on a vine
    // ON the slab; watermelon the STRIPED OVAL;
    // pumpkin the RIBBED gourd on a dirt vine, so a
    // YELLOW SCALLOPED DISK on a vine sitting ON the
    // slab still had no pattypan. A pattypan (picture,
    // no collider) on authored pegs sits that kitchen
    // row — not PlantFlower, not PlantStrawberry, not
    // TreeRig.Sway. Same LateUpdate sway as laundry;
    // small amp so the vine nods, not a cane wave.
    // The sibling sits on the same pivot.
    private void PlantPattypans(int seed, ref int swayWrite)
    {
        if (sway == null)
            return;

        LevelTest1.PattypanPeg[] pegs = LevelTest1.PattypanPegs;
        for (int i = 0; i < pegs.Length && swayWrite < sway.Length; i++)
        {
            LevelTest1.PattypanPeg peg = pegs[i];
            if (peg.x < chunkLeft + 0.10f || peg.x > chunkRight - 0.10f)
                continue;

            float surface = LevelTest1.SurfaceAt(peg.x);
            Transform root = MakePattypan(
                new Vector3(peg.x, surface, 0f),
                peg.face,
                Hash01(seed, 9630 + i));
            BindSway(
                swayWrite++,
                root,
                Hash01(seed, 9640 + i) * 6.28f,
                1.10f + Hash01(seed, 9650 + i) * 0.3f,
                0.34f + Hash01(seed, 9660 + i) * 0.12f);
        }
    }

    // Pattypan owns the YELLOW SCALLOPED DISK on a
    // vine ON the slab; zucchini the YELLOW HOOK;
    // pumpkin the RIBBED gourd on a dirt vine, so a
    // BEIGE BOTTLE fruit on a vine sitting ON the
    // slab still had no butternut. A butternut
    // (picture, no collider) on authored pegs sits
    // that kitchen row — not PlantFlower, not
    // PlantStrawberry, not TreeRig.Sway. Same
    // LateUpdate sway as laundry; small amp so the
    // vine nods, not a cane wave. The sibling sits
    // on the same pivot.
    private void PlantButternuts(int seed, ref int swayWrite)
    {
        if (sway == null)
            return;

        LevelTest1.ButternutPeg[] pegs = LevelTest1.ButternutPegs;
        for (int i = 0; i < pegs.Length && swayWrite < sway.Length; i++)
        {
            LevelTest1.ButternutPeg peg = pegs[i];
            if (peg.x < chunkLeft + 0.10f || peg.x > chunkRight - 0.10f)
                continue;

            float surface = LevelTest1.SurfaceAt(peg.x);
            Transform root = MakeButternut(
                new Vector3(peg.x, surface, 0f),
                peg.face,
                Hash01(seed, 9670 + i));
            BindSway(
                swayWrite++,
                root,
                Hash01(seed, 9680 + i) * 6.28f,
                1.10f + Hash01(seed, 9690 + i) * 0.3f,
                0.34f + Hash01(seed, 9700 + i) * 0.12f);
        }
    }

    // Acorn owns the DARK-GREEN RIBBED ACORN fruit
    // on a vine ON the slab; butternut the BEIGE
    // BOTTLE; pattypan the YELLOW SCALLOPED DISK;
    // pumpkin the RIBBED orange gourd on dirt. A
    // heart gourd (picture, no collider) on authored
    // pegs sits that kitchen row — not PlantFlower,
    // not PlantStrawberry, not TreeRig.Sway. Same
    // LateUpdate sway as laundry; small amp so the
    // vine nods, not a cane wave. The sibling sits
    // on the same pivot.
    private void PlantAcorns(int seed, ref int swayWrite)
    {
        if (sway == null)
            return;

        LevelTest1.AcornPeg[] pegs = LevelTest1.AcornPegs;
        for (int i = 0; i < pegs.Length && swayWrite < sway.Length; i++)
        {
            LevelTest1.AcornPeg peg = pegs[i];
            if (peg.x < chunkLeft + 0.10f || peg.x > chunkRight - 0.10f)
                continue;

            float surface = LevelTest1.SurfaceAt(peg.x);
            Transform root = MakeAcorn(
                new Vector3(peg.x, surface, 0f),
                peg.face,
                Hash01(seed, 9710 + i));
            BindSway(
                swayWrite++,
                root,
                Hash01(seed, 9720 + i) * 6.28f,
                1.10f + Hash01(seed, 9730 + i) * 0.3f,
                0.34f + Hash01(seed, 9740 + i) * 0.12f);
        }
    }

    // Spaghetti owns the PALE-YELLOW OBLONG fruit
    // on a vine ON the slab; acorn the DARK-GREEN
    // RIBBED ACORN; butternut the BEIGE BOTTLE;
    // pumpkin the RIBBED orange gourd on dirt. A
    // pale oblong (picture, no collider) on authored
    // pegs sits that kitchen row — not PlantFlower,
    // not PlantStrawberry, not TreeRig.Sway. Same
    // LateUpdate sway as laundry; small amp so the
    // vine nods, not a cane wave. The sibling sits
    // on the same pivot.
    private void PlantSpaghettis(int seed, ref int swayWrite)
    {
        if (sway == null)
            return;

        LevelTest1.SpaghettiPeg[] pegs = LevelTest1.SpaghettiPegs;
        for (int i = 0; i < pegs.Length && swayWrite < sway.Length; i++)
        {
            LevelTest1.SpaghettiPeg peg = pegs[i];
            if (peg.x < chunkLeft + 0.10f || peg.x > chunkRight - 0.10f)
                continue;

            float surface = LevelTest1.SurfaceAt(peg.x);
            Transform root = MakeSpaghetti(
                new Vector3(peg.x, surface, 0f),
                peg.face,
                Hash01(seed, 9750 + i));
            BindSway(
                swayWrite++,
                root,
                Hash01(seed, 9760 + i) * 6.28f,
                1.10f + Hash01(seed, 9770 + i) * 0.3f,
                0.34f + Hash01(seed, 9780 + i) * 0.12f);
        }
    }

    // Delicata owns the CREAM OBLONG with DARK-GREEN
    // STRIPES on a vine ON the slab; spaghetti the
    // PALE-YELLOW OBLONG; acorn the DARK-GREEN
    // RIBBED ACORN; butternut the BEIGE BOTTLE;
    // pumpkin the RIBBED orange gourd on dirt. A
    // cream stripe (picture, no collider) on authored
    // pegs sits that kitchen row — not PlantFlower,
    // not PlantStrawberry, not TreeRig.Sway. Same
    // LateUpdate sway as laundry; small amp so the
    // vine nods, not a cane wave. The sibling sits
    // on the same pivot.
    private void PlantDelicatas(int seed, ref int swayWrite)
    {
        if (sway == null)
            return;

        LevelTest1.DelicataPeg[] pegs = LevelTest1.DelicataPegs;
        for (int i = 0; i < pegs.Length && swayWrite < sway.Length; i++)
        {
            LevelTest1.DelicataPeg peg = pegs[i];
            if (peg.x < chunkLeft + 0.10f || peg.x > chunkRight - 0.10f)
                continue;

            float surface = LevelTest1.SurfaceAt(peg.x);
            Transform root = MakeDelicata(
                new Vector3(peg.x, surface, 0f),
                peg.face,
                Hash01(seed, 9790 + i));
            BindSway(
                swayWrite++,
                root,
                Hash01(seed, 9800 + i) * 6.28f,
                1.10f + Hash01(seed, 9810 + i) * 0.3f,
                0.34f + Hash01(seed, 9820 + i) * 0.12f);
        }
    }

    // Carnival owns the ROUND CREAM globe with
    // ORANGE-GREEN MOTTLE on a vine ON the slab;
    // delicata the CREAM OBLONG with DARK-GREEN
    // STRIPES; spaghetti the PALE-YELLOW OBLONG;
    // acorn the DARK-GREEN RIBBED ACORN; pumpkin
    // the RIBBED orange gourd on dirt. A round
    // cream blotch (picture, no collider) on
    // authored pegs sits that kitchen row — not
    // PlantFlower, not PlantStrawberry, not
    // TreeRig.Sway. Same LateUpdate sway as
    // laundry; small amp so the vine nods, not
    // a cane wave. The sibling sits on the same
    // pivot.
    private void PlantCarnivals(int seed, ref int swayWrite)
    {
        if (sway == null)
            return;

        LevelTest1.CarnivalPeg[] pegs = LevelTest1.CarnivalPegs;
        for (int i = 0; i < pegs.Length && swayWrite < sway.Length; i++)
        {
            LevelTest1.CarnivalPeg peg = pegs[i];
            if (peg.x < chunkLeft + 0.10f || peg.x > chunkRight - 0.10f)
                continue;

            float surface = LevelTest1.SurfaceAt(peg.x);
            Transform root = MakeCarnival(
                new Vector3(peg.x, surface, 0f),
                peg.face,
                Hash01(seed, 9830 + i));
            BindSway(
                swayWrite++,
                root,
                Hash01(seed, 9840 + i) * 6.28f,
                1.10f + Hash01(seed, 9850 + i) * 0.3f,
                0.34f + Hash01(seed, 9860 + i) * 0.12f);
        }
    }

    // Sweet dumpling owns the SMALL ROUND CREAM
    // fruit with DARK-GREEN RIBS on a vine ON the
    // slab; carnival the ROUND CREAM globe with
    // ORANGE-GREEN MOTTLE; delicata the CREAM
    // OBLONG with DARK-GREEN STRIPES; spaghetti
    // the PALE-YELLOW OBLONG; acorn the DARK-GREEN
    // RIBBED ACORN; pumpkin the RIBBED orange
    // gourd on dirt. A small cream ribbed blotch
    // (picture, no collider) on authored pegs sits
    // that kitchen row — not PlantFlower, not
    // PlantStrawberry, not TreeRig.Sway. Same
    // LateUpdate sway as laundry; small amp so the
    // vine nods, not a cane wave. The sibling sits
    // on the same pivot.
    private void PlantSweetDumplings(int seed, ref int swayWrite)
    {
        if (sway == null)
            return;

        LevelTest1.SweetDumplingPeg[] pegs = LevelTest1.SweetDumplingPegs;
        for (int i = 0; i < pegs.Length && swayWrite < sway.Length; i++)
        {
            LevelTest1.SweetDumplingPeg peg = pegs[i];
            if (peg.x < chunkLeft + 0.10f || peg.x > chunkRight - 0.10f)
                continue;

            float surface = LevelTest1.SurfaceAt(peg.x);
            Transform root = MakeSweetDumpling(
                new Vector3(peg.x, surface, 0f),
                peg.face,
                Hash01(seed, 9870 + i));
            BindSway(
                swayWrite++,
                root,
                Hash01(seed, 9880 + i) * 6.28f,
                1.10f + Hash01(seed, 9890 + i) * 0.3f,
                0.34f + Hash01(seed, 9900 + i) * 0.12f);
        }
    }

    // Kabocha owns the SQUAT DARK-GREEN globe with
    // PALE SPECKLES on a vine ON the slab; sweet
    // dumpling the SMALL ROUND CREAM fruit with
    // DARK-GREEN RIBS; carnival the ROUND CREAM
    // globe with ORANGE-GREEN MOTTLE; delicata
    // the CREAM OBLONG with DARK-GREEN STRIPES;
    // pumpkin the RIBBED orange gourd on dirt. A
    // squat dark-green blotch (picture, no
    // collider) on authored pegs sits that kitchen
    // row — not PlantFlower, not PlantStrawberry,
    // not TreeRig.Sway. Same LateUpdate sway as
    // laundry; small amp so the vine nods, not a
    // cane wave. The sibling sits on the same
    // pivot.
    private void PlantKabochas(int seed, ref int swayWrite)
    {
        if (sway == null)
            return;

        LevelTest1.KabochaPeg[] pegs = LevelTest1.KabochaPegs;
        for (int i = 0; i < pegs.Length && swayWrite < sway.Length; i++)
        {
            LevelTest1.KabochaPeg peg = pegs[i];
            if (peg.x < chunkLeft + 0.10f || peg.x > chunkRight - 0.10f)
                continue;

            float surface = LevelTest1.SurfaceAt(peg.x);
            Transform root = MakeKabocha(
                new Vector3(peg.x, surface, 0f),
                peg.face,
                Hash01(seed, 9910 + i));
            BindSway(
                swayWrite++,
                root,
                Hash01(seed, 9920 + i) * 6.28f,
                1.10f + Hash01(seed, 9930 + i) * 0.3f,
                0.34f + Hash01(seed, 9940 + i) * 0.12f);
        }
    }

    private void TickNests(float t, float night)
    {
        if (nestSit == null)
            return;

        float sitA = night >= SparrowRoostNight
            ? 1f
            : Mathf.InverseLerp(SparrowRoostNight - 0.14f, SparrowRoostNight, night);
        if (sitA < 0f)
            sitA = 0f;
        bool show = sitA > 0.04f;
        Color sit = new Color(0.24f, 0.19f, 0.14f, sitA);

        for (int i = 0; i < nestSit.Length; i++)
        {
            SpriteRenderer sr = nestSit[i];
            if (sr == null)
                continue;
            sit.a = sitA;
            sr.color = sit;
            sr.enabled = show;

            SpriteRenderer head = nestSitHead[i];
            if (head != null)
            {
                head.color = sit;
                head.enabled = show;
            }

            Transform xf = nestSitXf[i];
            if (xf == null)
                continue;
            Vector3 p = xf.localPosition;
            p.y = nestSitY[i] + Mathf.Sin(t * 1.45f + nestPhase[i]) * 0.004f;
            xf.localPosition = p;
        }
    }

    private void TickHouses(float night)
    {
        if (houseSit == null)
            return;

        float sitA = night >= SparrowRoostNight
            ? 1f
            : Mathf.InverseLerp(SparrowRoostNight - 0.14f, SparrowRoostNight, night);
        if (sitA < 0f)
            sitA = 0f;
        bool show = sitA > 0.04f;
        Color sit = new Color(0.22f, 0.17f, 0.12f, sitA);

        for (int i = 0; i < houseSit.Length; i++)
        {
            SpriteRenderer sr = houseSit[i];
            if (sr == null)
                continue;
            sit.a = sitA;
            sr.color = sit;
            sr.enabled = show;
        }
    }

    private void TickBaths(float t, float night)
    {
        if (bathWater == null)
            return;

        float dusk = 1f - Mathf.Abs(night * 2f - 1f);
        if (dusk < 0f)
            dusk = 0f;
        Color body = Color.Lerp(
            new Color(0.28f, 0.52f, 0.64f, 0.72f),
            new Color(0.10f, 0.14f, 0.22f, 0.78f),
            night);
        if (dusk > 0.01f)
            body = Color.Lerp(body, new Color(0.68f, 0.34f, 0.18f, 0.74f), dusk * 0.50f);

        Color glint = body;
        glint.r = Mathf.Clamp01(body.r + 0.30f);
        glint.g = Mathf.Clamp01(body.g + 0.26f);
        glint.b = Mathf.Clamp01(body.b + 0.20f);
        glint.a = body.a * 0.50f;

        float drinkA = night >= SparrowRoostNight
            ? 0f
            : 1f - Mathf.InverseLerp(SparrowRoostNight - 0.16f, SparrowRoostNight, night);
        if (drinkA < 0f)
            drinkA = 0f;
        bool showDrink = drinkA > 0.04f;
        Color bird = new Color(0.24f, 0.19f, 0.14f, drinkA);

        for (int i = 0; i < bathWater.Length; i++)
        {
            Transform water = bathWater[i];
            SpriteRenderer wsr = bathWaterSr[i];
            if (water == null || wsr == null)
                continue;

            wsr.color = body;
            float wobble = Mathf.Sin(t * 1.35f + bathPhase[i]) * 0.028f;
            Vector3 rest = bathWaterScale[i];
            water.localScale = new Vector3(rest.x * (1f + wobble), rest.y * (1f - wobble * 0.50f), 1f);

            Transform shine = bathGlint[i];
            SpriteRenderer gsr = bathGlintSr[i];
            if (shine != null && gsr != null)
            {
                gsr.color = glint;
                Vector3 lp = shine.localPosition;
                lp.x = Mathf.Sin(t * 0.70f + bathPhase[i]) * 0.028f;
                shine.localPosition = lp;
            }

            SpriteRenderer drink = bathDrink[i];
            if (drink == null)
                continue;
            bird.a = drinkA;
            drink.color = bird;
            drink.enabled = showDrink;

            SpriteRenderer head = bathDrinkHead[i];
            if (head != null)
            {
                head.color = bird;
                head.enabled = showDrink;
            }

            Transform xf = bathDrinkXf[i];
            if (xf == null)
                continue;
            float sip = Mathf.Abs(Mathf.Sin(t * 2.35f + bathPhase[i])) * 0.010f;
            Vector3 p = xf.localPosition;
            p.y = bathDrinkY[i] - sip * drinkA;
            xf.localPosition = p;
        }
    }

    private void TickFrogs(float t, float night)
    {
        if (frogs == null)
            return;

        float hopA = night > 0.42f ? 0f : 1f;
        float croakOn = night >= 0.28f ? night : 0f;
        Color throat = new Color(0.78f, 0.62f, 0.42f, 0f);
        bool showThroat = croakOn > 0.04f;

        for (int i = 0; i < frogs.Length; i++)
        {
            Transform root = frogs[i];
            if (root == null)
                continue;

            float w = t * 0.22f + frogPhase[i];
            float hop01 = 0f;
            if (hopA > 0.01f)
            {
                float u = Mathf.Repeat(w, 1f);
                if (u < 0.12f)
                    hop01 = u / 0.12f;
            }

            float lift = hop01 * (1f - hop01) * 0.28f;
            float dx = hopA * Mathf.Sin(w * 1.7f) * 0.07f;
            root.position = new Vector3(frogX[i] + dx * frogFace[i], frogY[i] + lift, 0f);

            float breath = 1f + Mathf.Sin(t * 1.15f + frogPhase[i]) * 0.045f;
            root.localScale = new Vector3(frogFace[i] * breath, breath, 1f);

            SpriteRenderer sr = frogThroat[i];
            if (sr == null)
                continue;
            float pulse = 0.5f + 0.5f * Mathf.Sin(t * 3.1f + frogPhase[i]);
            throat.a = croakOn * (0.22f + 0.70f * pulse);
            sr.color = throat;
            sr.enabled = showThroat;
        }
    }

    private void TickDragonflies(float t)
    {
        if (dragonflies == null)
            return;

        for (int i = 0; i < dragonflies.Length; i++)
        {
            Transform root = dragonflies[i];
            if (root == null)
                continue;

            float w = t * 0.55f + dragonPhase[i];
            float u = Mathf.Repeat(t * 0.19f + dragonPhase[i] * 0.13f, 1f);
            float dart = 0f;
            if (u < 0.10f)
                dart = Mathf.Sin(u / 0.10f * Mathf.PI);

            float px = dragonX[i]
                + Mathf.Sin(w) * 0.11f
                + dart * 0.16f * dragonFace[i];
            float py = dragonY[i]
                + Mathf.Sin(w * 1.37f + dragonPhase[i]) * 0.045f
                + dart * 0.035f;
            root.position = new Vector3(px, py, 0f);

            float flap = Mathf.Sin(t * 22f + dragonPhase[i]) * 14f;
            Transform left = dragonWingL[i];
            Transform right = dragonWingR[i];
            if (left != null)
                left.localRotation = Quaternion.Euler(0f, 0f, 8f + flap);
            if (right != null)
                right.localRotation = Quaternion.Euler(0f, 0f, -8f - flap);
        }
    }

    private void TickBees(float t)
    {
        if (bees == null)
            return;

        for (int i = 0; i < bees.Length; i++)
        {
            Transform root = bees[i];
            if (root == null)
                continue;

            float w = t * 0.82f + beePhase[i];
            float u = Mathf.Repeat(t * 0.27f + beePhase[i] * 0.11f, 1f);
            float dart = 0f;
            if (u < 0.12f)
                dart = Mathf.Sin(u / 0.12f * Mathf.PI);

            float px = beeX[i]
                + Mathf.Sin(w) * 0.042f
                + dart * 0.055f * beeFace[i];
            float py = beeY[i]
                + Mathf.Sin(w * 1.61f + beePhase[i]) * 0.022f
                + dart * 0.018f;
            root.position = new Vector3(px, py, 0f);

            float flap = Mathf.Sin(t * 36f + beePhase[i]) * 18f;
            Transform left = beeWingL[i];
            Transform right = beeWingR[i];
            if (left != null)
                left.localRotation = Quaternion.Euler(0f, 0f, 10f + flap);
            if (right != null)
                right.localRotation = Quaternion.Euler(0f, 0f, -10f - flap);
        }
    }

    private void TickSnails(float t, float night)
    {
        if (snails == null)
            return;

        float damp = Mathf.Clamp01(night);

        for (int i = 0; i < snails.Length; i++)
        {
            Transform root = snails[i];
            if (root == null)
                continue;

            float w = t * 0.30f + snailPhase[i];
            float crawl = 0.5f + 0.5f * Mathf.Sin(w);
            // Day stays on the lower clay so noon Play still reads them;
            // dusk/night walks the rim (damp).
            float u = crawl * (0.50f + 0.50f * damp) + 0.38f * damp;
            if (u > 1f)
                u = 1f;
            float px = snailX[i] + snailFace[i] * 0.050f;
            float py = snailY[i] + u * 0.070f;
            root.position = new Vector3(px, py, 0f);
            float vel = Mathf.Cos(w);
            root.localRotation = Quaternion.Euler(0f, 0f, vel * 10f);

            float twitch = Mathf.Sin(t * 3.6f + snailPhase[i]) * 10f;
            Transform left = snailAntL[i];
            Transform right = snailAntR[i];
            if (left != null)
                left.localRotation = Quaternion.Euler(0f, 0f, 58f + twitch);
            if (right != null)
                right.localRotation = Quaternion.Euler(0f, 0f, 42f - twitch * 0.7f);
        }
    }

    private void TickLizards(float t, float night)
    {
        if (lizards == null)
            return;

        float sun = night > 0.42f ? 0f : 1f;
        float hide = night < 0.22f ? 0f : Mathf.Clamp01((night - 0.22f) / 0.28f);

        for (int i = 0; i < lizards.Length; i++)
        {
            Transform root = lizards[i];
            if (root == null)
                continue;

            float w = t * 0.58f + lizardPhase[i];
            float u = Mathf.Repeat(w, 1f);
            float push = 0f;
            if (sun > 0.01f && u < 0.22f)
                push = Mathf.Sin(u / 0.22f * Mathf.PI);

            float offset = (0.055f - hide * 0.018f) * lizardFace[i];
            root.position = new Vector3(
                lizardX[i] + offset,
                lizardY[i] + push * 0.014f * sun,
                0f);

            float squat = 1f - hide * 0.28f;
            root.localScale = new Vector3(
                lizardFace[i] * squat,
                squat + push * 0.08f,
                1f);

            Transform tail = lizardTail[i];
            if (tail != null)
            {
                float flick = Mathf.Sin(t * 3.1f + lizardPhase[i]) * (8f * sun + 2f);
                tail.localRotation = Quaternion.Euler(0f, 0f, -22f + flick);
            }
        }
    }

    private void TickGrasshoppers(float t, float night)
    {
        if (hoppers == null)
            return;

        float sun = night > 0.42f ? 0f : 1f;
        float hide = night < 0.22f ? 0f : Mathf.Clamp01((night - 0.22f) / 0.28f);

        for (int i = 0; i < hoppers.Length; i++)
        {
            Transform root = hoppers[i];
            if (root == null)
                continue;

            float w = t * 0.48f + hopperPhase[i];
            float hop01 = 0f;
            if (sun > 0.01f)
            {
                float u = Mathf.Repeat(w, 1f);
                if (u < 0.10f)
                    hop01 = u / 0.10f;
            }

            float lift = hop01 * (1f - hop01) * 0.22f * sun;
            float dx = hop01 * (1f - hop01) * 0.10f * sun;
            root.position = new Vector3(
                hopperX[i] + dx * hopperFace[i],
                hopperY[i] + lift - hide * 0.010f,
                0f);

            float squat = 1f - hide * 0.42f;
            root.localScale = new Vector3(
                hopperFace[i] * squat,
                squat * (1f + hop01 * (1f - hop01) * 0.18f * sun),
                1f);

            Transform hind = hopperHind[i];
            if (hind != null)
            {
                float kick = hop01 * (1f - hop01) * 55f * sun;
                hind.localRotation = Quaternion.Euler(0f, 0f, -38f - kick);
            }

            Transform ant = hopperAnt[i];
            if (ant != null)
            {
                float twitch = Mathf.Sin(t * (2.4f + 2.2f * hide) + hopperPhase[i]) * (6f + 10f * hide);
                ant.localRotation = Quaternion.Euler(0f, 0f, 48f + twitch);
            }
        }
    }

    private void TickAnts(float t, float night)
    {
        if (ants == null)
            return;

        float sun = night > 0.42f ? 0f : 1f;
        float hide = night < 0.22f ? 0f : Mathf.Clamp01((night - 0.22f) / 0.28f);

        for (int i = 0; i < ants.Length; i++)
        {
            Transform root = ants[i];
            if (root == null)
                continue;

            float crawl = Mathf.Sin(t * 0.28f + antPhase[i]) * 0.14f * sun;
            root.position = new Vector3(
                antX[i] + crawl * antFace[i],
                antY[i] - hide * 0.006f,
                0f);

            float squat = 1f - hide * 0.55f;
            root.localScale = new Vector3(
                antFace[i] * squat,
                squat,
                1f);

            Transform feeler = antAnt[i];
            if (feeler != null)
            {
                float twitch = Mathf.Sin(t * (3.1f + 1.6f * hide) + antPhase[i]) * (8f + 6f * (1f - hide));
                feeler.localRotation = Quaternion.Euler(0f, 0f, 42f + twitch);
            }
        }
    }

    private void TickCrickets(float t, float night)
    {
        if (crickets == null)
            return;

        float show = night < 0.22f ? 0f : Mathf.Clamp01((night - 0.22f) / 0.28f);
        float hide = 1f - show;

        for (int i = 0; i < crickets.Length; i++)
        {
            Transform root = crickets[i];
            if (root == null)
                continue;

            float chirp = 0f;
            if (show > 0.45f)
            {
                float u = Mathf.Repeat(t * 4.2f + cricketPhase[i], 1f);
                if (u < 0.18f)
                    chirp = Mathf.Sin(u / 0.18f * Mathf.PI);
            }

            root.position = new Vector3(
                cricketX[i],
                cricketY[i] + chirp * 0.004f * show - hide * 0.008f,
                0f);

            float squat = 1f - hide * 0.48f;
            root.localScale = new Vector3(
                cricketFace[i] * squat,
                squat * (1f + chirp * 0.10f * show),
                1f);

            Transform wing = cricketWing[i];
            if (wing != null)
                wing.localRotation = Quaternion.Euler(0f, 0f, -10f - chirp * 26f * show);

            Transform ant = cricketAnt[i];
            if (ant != null)
            {
                float twitch = Mathf.Sin(t * (2.8f + 1.8f * show) + cricketPhase[i]) * (7f + 9f * show);
                ant.localRotation = Quaternion.Euler(0f, 0f, 62f + twitch);
            }
        }
    }

    private void TickWorms(float t, float night)
    {
        if (worms == null)
            return;

        float show = night < 0.22f ? 0f : Mathf.Clamp01((night - 0.22f) / 0.28f);
        float hide = 1f - show;
        Color sheen = new Color(0.90f, 0.68f, 0.52f, 0f);
        bool lit = show > 0.04f;

        for (int i = 0; i < worms.Length; i++)
        {
            Transform root = worms[i];
            if (root == null)
                continue;

            float w = t * 2.35f + wormPhase[i];
            float wave = Mathf.Sin(w);
            float crawl = Mathf.Sin(t * 0.18f + wormPhase[i]) * 0.10f * show;
            root.position = new Vector3(
                wormX[i] + crawl * wormFace[i],
                wormY[i] + show * 0.006f - hide * 0.008f,
                0f);

            float squat = (1f - hide * 0.52f) * 0.70f;
            root.localScale = new Vector3(
                wormFace[i] * squat * (1f + wave * 0.06f * show),
                squat * (1f - wave * 0.10f * show),
                1f);

            Transform mid = wormMid[i];
            if (mid != null)
                mid.localPosition = new Vector3(wave * 0.007f * show, 0f, 0f);

            SpriteRenderer sr = wormSheen[i];
            if (sr == null)
                continue;
            float pulse = 0.5f + 0.5f * wave;
            sheen.a = show * (0.28f + 0.50f * pulse);
            sr.color = sheen;
            sr.enabled = lit;
        }
    }

    private void TickStriders(float t, float night)
    {
        if (striders == null)
            return;

        float sun = night > 0.42f ? 0f : 1f - Mathf.Clamp01((night - 0.18f) / 0.24f);
        if (sun < 0f)
            sun = 0f;
        float hide = 1f - sun;
        Color dimple = new Color(0.78f, 0.90f, 0.96f, 0f);
        bool film = sun > 0.04f;

        for (int i = 0; i < striders.Length; i++)
        {
            Transform root = striders[i];
            if (root == null)
                continue;

            float skate = Mathf.Sin(t * 0.52f + striderPhase[i]) * 0.16f * sun;
            root.position = new Vector3(
                striderX[i] + skate * striderFace[i],
                striderY[i] + sun * 0.003f - hide * 0.010f,
                0f);

            float squat = 1f - hide * 0.55f;
            root.localScale = new Vector3(
                striderFace[i] * squat,
                squat,
                1f);

            Transform stroke = striderStroke[i];
            if (stroke != null)
            {
                float row = Mathf.Sin(t * 3.6f + striderPhase[i]) * 14f * sun;
                stroke.localRotation = Quaternion.Euler(0f, 0f, row);
            }

            SpriteRenderer sr = striderDimple[i];
            if (sr == null)
                continue;
            dimple.a = sun * 0.32f;
            sr.color = dimple;
            sr.enabled = film;
        }
    }

    private void TickBats(float t, float night)
    {
        if (bats == null)
            return;

        float hunt = night > BatHuntNight
            ? 1f
            : Mathf.InverseLerp(BatHuntNight - 0.12f, BatHuntNight, night);
        if (hunt < 0f)
            hunt = 0f;
        bool showEye = hunt > 0.55f;
        Color eye = new Color(0.92f, 0.72f, 0.22f, hunt);

        for (int i = 0; i < bats.Length; i++)
        {
            Transform root = bats[i];
            if (root == null)
                continue;

            float w = t * 0.92f + batPhase[i];
            float px = batX[i] + Mathf.Cos(w) * 0.22f;
            float py = batY[i] + Mathf.Sin(w * 1.18f + batPhase[i]) * 0.10f;
            root.position = new Vector3(px, py, 0f);

            float face = -Mathf.Sin(w) >= 0f ? 1f : -1f;
            root.localScale = new Vector3(face, 1f, 1f);

            float flap = Mathf.Sin(t * 16f + batPhase[i]) * 26f;
            Transform left = batWingL[i];
            Transform right = batWingR[i];
            if (left != null)
                left.localRotation = Quaternion.Euler(0f, 0f, 12f + flap);
            if (right != null)
                right.localRotation = Quaternion.Euler(0f, 0f, -12f - flap);

            SpriteRenderer sr = batEyes[i];
            if (sr == null)
                continue;
            eye.a = hunt * 0.85f;
            sr.color = eye;
            sr.enabled = showEye;
        }
    }

    private void TickWebs(float t, float night)
    {
        if (spiders == null)
            return;

        float dewA = night * 0.48f;
        if (dewA < 0f)
            dewA = 0f;
        bool showDew = dewA > 0.04f;
        Color dew = new Color(0.86f, 0.92f, 0.98f, dewA);

        for (int i = 0; i < spiders.Length; i++)
        {
            Transform root = spiders[i];
            if (root == null)
                continue;

            float w = t * 0.22f + webPhase[i];
            float px = webX[i] + Mathf.Cos(w) * webSpanX[i] * 0.34f;
            float py = webY[i] + Mathf.Sin(w * 0.86f + webPhase[i]) * webSpanY[i] * 0.30f;
            root.position = new Vector3(px, py, 0f);

            float face = -Mathf.Sin(w) >= 0f ? webFace[i] : -webFace[i];
            root.localScale = new Vector3(face, 1f, 1f);

            Transform legs = spiderLegs[i];
            if (legs != null)
            {
                float step = Mathf.Sin(t * 5.2f + webPhase[i]) * 12f;
                legs.localRotation = Quaternion.Euler(0f, 0f, step);
            }

            SpriteRenderer sr = webDew[i];
            if (sr == null)
                continue;
            dew.a = dewA;
            sr.color = dew;
            sr.enabled = showDew;
        }
    }

    private void TickMice(float t, float night)
    {
        if (mice == null)
            return;

        float show = night < 0.22f ? 0f : Mathf.Clamp01((night - 0.22f) / 0.28f);
        float hide = 1f - show;
        bool lit = show > 0.18f;
        Color eye = new Color(0.92f, 0.78f, 0.28f, show);

        for (int i = 0; i < mice.Length; i++)
        {
            Transform root = mice[i];
            if (root == null)
                continue;

            // Stop-start dart: two short runs per cycle, pauses at each
            // end so the scavenger reads against the loafing cat, not a
            // continuous crawl (worms / ants own that).
            float u = t * 0.48f + mousePhase[i];
            u = u - Mathf.Floor(u);
            float dart;
            if (u < 0.22f)
                dart = u / 0.22f;
            else if (u < 0.55f)
                dart = 1f;
            else if (u < 0.77f)
                dart = 1f - (u - 0.55f) / 0.22f;
            else
                dart = 0f;
            dart = dart * dart * (3f - 2f * dart);
            float along = (dart * 2f - 1f) * show;
            float face = u < 0.55f ? mouseFace[i] : -mouseFace[i];
            root.position = new Vector3(
                mouseX[i] + along * 0.11f,
                mouseY[i] + show * 0.004f - hide * 0.014f,
                0f);

            float squat = 1f - hide * 0.78f;
            root.localScale = new Vector3(face * squat, squat, 1f);

            Transform tail = mouseTails[i];
            if (tail != null)
            {
                float run = (u < 0.22f || (u > 0.55f && u < 0.77f)) ? show : 0f;
                float flick = Mathf.Sin(t * (3.4f + 8f * run) + mousePhase[i]) * (8f + 16f * run);
                tail.localRotation = mouseTailRest[i] * Quaternion.Euler(0f, 0f, flick);
            }

            SpriteRenderer sr = mouseEyes[i];
            if (sr == null)
                continue;
            eye.a = show * 0.88f;
            sr.color = eye;
            sr.enabled = lit;
        }
    }

    private void TickRabbits(float t, float night)
    {
        if (rabbits == null)
            return;

        float sun = night > 0.42f ? 0f : 1f;
        float hide = night < 0.22f ? 0f : Mathf.Clamp01((night - 0.22f) / 0.28f);

        for (int i = 0; i < rabbits.Length; i++)
        {
            Transform root = rabbits[i];
            if (root == null)
                continue;

            // Freeze-hop, not a hopper bounce: long sit-nibble, one hop
            // out, sit, hop home. Travel stays on the peg so the grazer
            // reads against the grass, not a wander brain.
            float u = t * 0.18f + rabbitPhase[i];
            u = u - Mathf.Floor(u);
            float along;
            float hop01;
            if (u < 0.12f)
            {
                hop01 = u / 0.12f;
                along = hop01;
            }
            else if (u < 0.50f)
            {
                hop01 = 0f;
                along = 1f;
            }
            else if (u < 0.62f)
            {
                hop01 = (u - 0.50f) / 0.12f;
                along = 1f - hop01;
            }
            else
            {
                hop01 = 0f;
                along = 0f;
            }

            hop01 *= sun;
            along *= sun;
            float lift = hop01 * (1f - hop01) * 0.10f;
            float face = rabbitFace[i];
            if (u >= 0.50f && u < 0.62f)
                face = -rabbitFace[i];

            root.position = new Vector3(
                rabbitX[i] + (along * 2f - 1f) * 0.16f * rabbitFace[i],
                rabbitY[i] + lift - hide * 0.016f,
                0f);

            float squat = 1f - hide * 0.72f;
            float squash = 1f - hop01 * (1f - hop01) * 0.22f;
            root.localScale = new Vector3(face * squat, squat * squash, 1f);

            Transform head = rabbitHeads[i];
            if (head != null)
            {
                float nibble = (hop01 < 0.04f ? sun : 0f)
                    * Mathf.Sin(t * 7.2f + rabbitPhase[i]) * 9f;
                head.localRotation = rabbitHeadRest[i] * Quaternion.Euler(0f, 0f, nibble);
            }

            Transform ear = rabbitEars[i];
            if (ear != null)
            {
                float twitch = Mathf.Sin(t * 3.1f + rabbitPhase[i]) * (5f + 4f * sun);
                float fold = hop01 * 18f;
                ear.localRotation = rabbitEarRest[i] * Quaternion.Euler(0f, 0f, twitch - fold);
            }
        }
    }

    private void TickSquirrels(float t, float night)
    {
        if (squirrels == null)
            return;

        float sun = night > 0.42f ? 0f : 1f;
        float hide = night < 0.22f ? 0f : Mathf.Clamp01((night - 0.22f) / 0.28f);

        for (int i = 0; i < squirrels.Length; i++)
        {
            Transform root = squirrels[i];
            if (root == null)
                continue;

            // Freeze-scamper, not a rabbit hop and not a mouse dart on
            // dirt: long sit, one run along the roof, sit, run home.
            // Travel stays on the peg so the climber reads against the
            // house, not a wander brain.
            float u = t * 0.20f + squirrelPhase[i];
            u = u - Mathf.Floor(u);
            float along;
            float run01;
            if (u < 0.14f)
            {
                run01 = u / 0.14f;
                along = run01;
            }
            else if (u < 0.52f)
            {
                run01 = 0f;
                along = 1f;
            }
            else if (u < 0.66f)
            {
                run01 = (u - 0.52f) / 0.14f;
                along = 1f - run01;
            }
            else
            {
                run01 = 0f;
                along = 0f;
            }

            run01 *= sun;
            along *= sun;
            float face = squirrelFace[i];
            if (u >= 0.52f && u < 0.66f)
                face = -squirrelFace[i];

            root.position = new Vector3(
                squirrelX[i] + (along * 2f - 1f) * 0.055f * squirrelFace[i],
                squirrelY[i] + run01 * (1f - run01) * 0.012f - hide * 0.010f,
                0f);

            float squat = 1f - hide * 0.78f;
            float stretch = 1f + run01 * (1f - run01) * 0.16f;
            root.localScale = new Vector3(face * squat * stretch, squat, 1f);

            Transform tail = squirrelTails[i];
            if (tail == null)
                continue;
            float wag = Mathf.Sin(t * (4.2f + 9f * run01) + squirrelPhase[i])
                * (10f + 14f * run01)
                * (1f - hide);
            tail.localRotation = squirrelTailRest[i] * Quaternion.Euler(0f, 0f, wag);
        }
    }

    private void TickOwls(float t, float night)
    {
        if (owls == null)
            return;

        float show = night > OwlRoostNight
            ? 1f
            : Mathf.InverseLerp(OwlRoostNight - 0.12f, OwlRoostNight, night);
        if (show < 0f)
            show = 0f;
        float hide = 1f - show;
        bool lit = show > 0.18f;
        Color eye = new Color(0.92f, 0.78f, 0.28f, show);

        for (int i = 0; i < owls.Length; i++)
        {
            Transform root = owls[i];
            if (root == null)
                continue;

            // Perch, not a bat orbit and not a sparrow hop: the cap is
            // the seat. Breath and a slow head turn so the hunter reads
            // against the glow, not a fly brain.
            float breath = 1f + Mathf.Sin(t * 1.15f + owlPhase[i]) * 0.024f * show;
            float squat = (1f - hide * 0.78f) * breath;
            root.position = new Vector3(
                owlX[i],
                owlY[i] - hide * 0.012f,
                0f);
            root.localScale = new Vector3(owlFace[i] * squat, squat, 1f);

            Transform head = owlHeads[i];
            if (head != null)
            {
                float look = Mathf.Sin(t * 0.48f + owlPhase[i]) * 14f * show;
                head.localRotation = owlHeadRest[i] * Quaternion.Euler(0f, 0f, look);
            }

            SpriteRenderer sr = owlEyes[i];
            if (sr == null)
                continue;
            eye.a = show * 0.90f;
            sr.color = eye;
            sr.enabled = lit;
        }
    }

    private void TickRaccoons(float t, float night)
    {
        if (raccoons == null)
            return;

        float show = night < 0.22f ? 0f : Mathf.Clamp01((night - 0.22f) / 0.28f);
        float hide = 1f - show;
        bool lit = show > 0.18f;
        Color eye = new Color(0.92f, 0.78f, 0.28f, show);

        for (int i = 0; i < raccoons.Length; i++)
        {
            Transform root = raccoons[i];
            if (root == null)
                continue;

            // Sit-wash, not a mouse dart and not a rabbit hop: long
            // loaf at the logs, a short shuffle, wash, shuffle home.
            // Travel stays on the peg so the scavenger reads against
            // the fire, not a wander brain.
            float u = t * 0.22f + raccoonPhase[i];
            u = u - Mathf.Floor(u);
            float along;
            float wash01;
            if (u < 0.14f)
            {
                float s = u / 0.14f;
                along = s * s * (3f - 2f * s);
                wash01 = 0f;
            }
            else if (u < 0.52f)
            {
                along = 1f;
                wash01 = 1f;
            }
            else if (u < 0.66f)
            {
                float s = (u - 0.52f) / 0.14f;
                along = 1f - s * s * (3f - 2f * s);
                wash01 = 0f;
            }
            else
            {
                along = 0f;
                wash01 = 1f;
            }

            along *= show;
            wash01 *= show;
            float face = raccoonFace[i];
            if (u >= 0.52f && u < 0.66f)
                face = -raccoonFace[i];

            root.position = new Vector3(
                raccoonX[i] + (along * 2f - 1f) * 0.10f * raccoonFace[i],
                raccoonY[i] + show * 0.004f - hide * 0.016f,
                0f);

            float squat = 1f - hide * 0.78f;
            root.localScale = new Vector3(face * squat, squat, 1f);

            Transform head = raccoonHeads[i];
            if (head != null)
            {
                float dip = wash01 * (0.55f + 0.45f * Mathf.Sin(t * 4.6f + raccoonPhase[i]));
                head.localRotation = raccoonHeadRest[i] * Quaternion.Euler(0f, 0f, -18f * dip);
            }

            Transform tail = raccoonTails[i];
            if (tail != null)
            {
                float flick = Mathf.Sin(t * 2.4f + raccoonPhase[i]) * (7f + 9f * wash01);
                tail.localRotation = raccoonTailRest[i] * Quaternion.Euler(0f, 0f, flick);
            }

            SpriteRenderer sr = raccoonEyes[i];
            if (sr == null)
                continue;
            eye.a = show * 0.88f;
            sr.color = eye;
            sr.enabled = lit;
        }
    }

    private void TickTurtles(float t, float night)
    {
        if (turtles == null)
            return;

        float show = night >= 0.38f ? 0f : 1f - Mathf.Clamp01(night / 0.38f);
        float hide = 1f - show;

        for (int i = 0; i < turtles.Length; i++)
        {
            Transform root = turtles[i];
            if (root == null)
                continue;

            // Bask-crawl, not a frog hop and not a strider skate: long
            // sit on the bank, a short shuffle toward the water, sit.
            // Travel stays on the peg so the shell reads against the
            // dip, not a wander brain.
            float u = t * 0.16f + turtlePhase[i];
            u = u - Mathf.Floor(u);
            float along;
            if (u < 0.18f)
            {
                float s = u / 0.18f;
                along = s * s * (3f - 2f * s);
            }
            else if (u < 0.58f)
            {
                along = 1f;
            }
            else if (u < 0.76f)
            {
                float s = (u - 0.58f) / 0.18f;
                along = 1f - s * s * (3f - 2f * s);
            }
            else
            {
                along = 0f;
            }

            along *= show;
            float face = turtleFace[i];
            if (u >= 0.58f && u < 0.76f)
                face = -turtleFace[i];

            root.position = new Vector3(
                turtleX[i] + (along * 2f - 1f) * 0.08f * turtleFace[i],
                turtleY[i] + show * 0.003f - hide * 0.014f,
                0f);

            float squat = 1f - hide * 0.72f;
            root.localScale = new Vector3(face * squat, squat, 1f);

            Transform head = turtleHeads[i];
            if (head == null)
                continue;
            Vector3 hp = turtleHeadRest[i];
            float neck = show * (0.82f + 0.18f * Mathf.Sin(t * 1.05f + turtlePhase[i]));
            hp.x *= neck;
            head.localPosition = hp;
        }
    }

    private void TickDucks(float t, float night)
    {
        if (ducks == null)
            return;

        float show = night >= 0.38f ? 0f : 1f - Mathf.Clamp01(night / 0.38f);
        float hide = 1f - show;

        for (int i = 0; i < ducks.Length; i++)
        {
            Transform root = ducks[i];
            if (root == null)
                continue;

            // Loaf-dabble, not a turtle crawl and not a frog hop: long
            // sit on the edge, a short waddle along the water, sit.
            // Travel stays on the peg so the drake reads against the
            // dip, not a wander brain.
            float u = t * 0.14f + duckPhase[i];
            u = u - Mathf.Floor(u);
            float along;
            if (u < 0.16f)
            {
                float s = u / 0.16f;
                along = s * s * (3f - 2f * s);
            }
            else if (u < 0.62f)
            {
                along = 1f;
            }
            else if (u < 0.78f)
            {
                float s = (u - 0.62f) / 0.16f;
                along = 1f - s * s * (3f - 2f * s);
            }
            else
            {
                along = 0f;
            }

            along *= show;
            float face = duckFace[i];
            if (u >= 0.62f && u < 0.78f)
                face = -duckFace[i];

            float waddle = along * (1f - along) * 0.008f;
            root.position = new Vector3(
                duckX[i] + (along * 2f - 1f) * 0.11f * duckFace[i],
                duckY[i] + show * 0.004f + waddle - hide * 0.016f,
                0f);

            float squat = 1f - hide * 0.68f;
            root.localScale = new Vector3(face * squat, squat, 1f);

            Transform head = duckHeads[i];
            if (head == null)
                continue;
            // Dabble while sitting: bill dips toward the film. Hide
            // tucks the head back onto the rump (sleep).
            float sit = (u >= 0.16f && u < 0.62f) || u >= 0.78f ? 1f : 0f;
            float pulse = Mathf.Sin(t * 1.55f + duckPhase[i]);
            float dip = (sit > 0.5f && pulse > 0f) ? pulse * show : 0f;
            float tuck = hide * 55f;
            head.localRotation = duckHeadRest[i] * Quaternion.Euler(0f, 0f, -dip * 28f + tuck);
        }
    }

    private void TickHerons(float t, float night)
    {
        if (herons == null)
            return;

        float show = night >= 0.38f ? 0f : 1f - Mathf.Clamp01(night / 0.38f);
        float hide = 1f - show;

        for (int i = 0; i < herons.Length; i++)
        {
            Transform root = herons[i];
            if (root == null)
                continue;

            // Stand-and-stab, not a duck dabble and not a turtle crawl:
            // long still in the film, a short spear, recover. Travel
            // stays on the peg so the wader reads against the dip, not
            // a wander brain.
            float u = t * 0.11f + heronPhase[i];
            u = u - Mathf.Floor(u);
            float stab;
            if (u < 0.58f)
            {
                stab = 0f;
            }
            else if (u < 0.68f)
            {
                float s = (u - 0.58f) / 0.10f;
                stab = s * s * (3f - 2f * s);
            }
            else if (u < 0.80f)
            {
                stab = 1f;
            }
            else if (u < 0.90f)
            {
                float s = (u - 0.80f) / 0.10f;
                stab = 1f - s * s * (3f - 2f * s);
            }
            else
            {
                stab = 0f;
            }

            float bob = show * 0.004f * Mathf.Sin(t * 1.15f + heronPhase[i]);
            root.position = new Vector3(
                heronX[i],
                heronY[i] + bob - hide * 0.028f,
                0f);

            float squat = 1f - hide * 0.55f;
            root.localScale = new Vector3(heronFace[i] * squat, squat, 1f);

            Transform neck = heronNecks[i];
            if (neck == null)
                continue;
            float dip = stab * show * 52f;
            float tuck = hide * 62f;
            neck.localRotation = heronNeckRest[i] * Quaternion.Euler(0f, 0f, -dip + tuck);
        }
    }

    private void TickMinnows(float t, float night)
    {
        if (minnows == null)
            return;

        float show = night >= 0.38f ? 0f : 1f - Mathf.Clamp01(night / 0.38f);
        float hide = 1f - show;
        Color flash = new Color(0.78f, 0.88f, 0.80f, 0f);
        bool wet = show > 0.04f;

        for (int i = 0; i < minnows.Length; i++)
        {
            Transform root = minnows[i];
            if (root == null)
                continue;

            // Cruise-dart, not a strider skate and not a heron spear:
            // a slow lap under the film, a short flash, reverse. Travel
            // stays on the peg so the fish reads against the dip, not
            // a wander brain.
            float w = t * 0.85f + minnowPhase[i];
            float along = Mathf.Sin(w) * 0.20f * show;
            float bob = show * 0.004f * Mathf.Sin(t * 1.7f + minnowPhase[i]);
            root.position = new Vector3(
                minnowX[i] + along * minnowFace[i],
                minnowY[i] + bob - hide * 0.018f,
                0f);

            float vx = Mathf.Cos(w) * minnowFace[i];
            float face = vx >= 0f ? 1f : -1f;
            float squat = 1f - hide * 0.55f;
            root.localScale = new Vector3(face * squat, squat, 1f);

            Transform tail = minnowTails[i];
            if (tail != null)
            {
                float wag = Mathf.Sin(t * 8.2f + minnowPhase[i]) * 22f * show;
                tail.localRotation = Quaternion.Euler(0f, 0f, wag);
            }

            SpriteRenderer sr = minnowFlash[i];
            if (sr == null)
                continue;
            float pulse = Mathf.Abs(vx);
            flash.a = show * (0.16f + 0.18f * pulse);
            sr.color = flash;
            sr.enabled = wet;
        }
    }

    private void AllocSway(int count)
    {
        sway = new Transform[count];
        swayPhase = new float[count];
        swayAmp = new float[count];
        swaySpeed = new float[count];
        swayRest = new Quaternion[count];
    }

    private void TrimSway(int count)
    {
        System.Array.Resize(ref sway, count);
        System.Array.Resize(ref swayPhase, count);
        System.Array.Resize(ref swayAmp, count);
        System.Array.Resize(ref swaySpeed, count);
        System.Array.Resize(ref swayRest, count);
    }

    private void AllocBugs(int count)
    {
        bugs = new Transform[count];
        wingL = new Transform[count];
        wingR = new Transform[count];
        bugHome = new Vector2[count];
        bugPhase = new float[count];
        bugSpeed = new float[count];
        bugSpanX = new float[count];
        bugSpanY = new float[count];
    }

    private void TrimBugs(int count)
    {
        System.Array.Resize(ref bugs, count);
        System.Array.Resize(ref wingL, count);
        System.Array.Resize(ref wingR, count);
        System.Array.Resize(ref bugHome, count);
        System.Array.Resize(ref bugPhase, count);
        System.Array.Resize(ref bugSpeed, count);
        System.Array.Resize(ref bugSpanX, count);
        System.Array.Resize(ref bugSpanY, count);
    }

    private void AllocFireflies(int count)
    {
        fireflies = new Transform[count];
        fireflySr = new SpriteRenderer[count];
        fireflyHome = new Vector2[count];
        fireflyPhase = new float[count];
        fireflySpeed = new float[count];
        fireflySpanX = new float[count];
        fireflySpanY = new float[count];
        fireflyRate = new float[count];
        fireflyBaseA = new float[count];
    }

    private void TrimFireflies(int count)
    {
        System.Array.Resize(ref fireflies, count);
        System.Array.Resize(ref fireflySr, count);
        System.Array.Resize(ref fireflyHome, count);
        System.Array.Resize(ref fireflyPhase, count);
        System.Array.Resize(ref fireflySpeed, count);
        System.Array.Resize(ref fireflySpanX, count);
        System.Array.Resize(ref fireflySpanY, count);
        System.Array.Resize(ref fireflyRate, count);
        System.Array.Resize(ref fireflyBaseA, count);
    }

    private void BindSway(int i, Transform pivot, float phase, float ampDeg, float speed)
    {
        sway[i] = pivot;
        swayPhase[i] = phase;
        swayAmp[i] = ampDeg;
        swaySpeed[i] = speed;
        swayRest[i] = pivot.localRotation;
    }

    private void BindBug(
        int i,
        Transform root,
        Transform left,
        Transform right,
        float homeX,
        float homeY,
        float speed,
        float spanX,
        float spanY)
    {
        bugs[i] = root;
        wingL[i] = left;
        wingR[i] = right;
        bugHome[i] = new Vector2(homeX, homeY);
        bugPhase[i] = i * 1.7f + homeX * 0.13f;
        bugSpeed[i] = speed;
        bugSpanX[i] = spanX;
        bugSpanY[i] = spanY;
    }

    private void BindFirefly(
        int i,
        Transform root,
        float homeX,
        float homeY,
        float speed,
        float spanX,
        float spanY,
        float rate,
        float baseA)
    {
        fireflies[i] = root;
        fireflySr[i] = root != null ? root.GetComponent<SpriteRenderer>() : null;
        fireflyHome[i] = new Vector2(homeX, homeY);
        fireflyPhase[i] = i * 1.9f + homeX * 0.17f;
        fireflySpeed[i] = speed;
        fireflySpanX[i] = spanX;
        fireflySpanY[i] = spanY;
        fireflyRate[i] = rate;
        fireflyBaseA[i] = baseA;
    }

    private void AllocLeaves(int count)
    {
        leaves = new Transform[count];
        leafPhase = new float[count];
        leafFall = new float[count];
        leafDrift = new float[count];
        leafSpin = new float[count];
        leafHomeX = new float[count];
        leafCeil = new float[count];
    }

    private void TrimLeaves(int count)
    {
        System.Array.Resize(ref leaves, count);
        System.Array.Resize(ref leafPhase, count);
        System.Array.Resize(ref leafFall, count);
        System.Array.Resize(ref leafDrift, count);
        System.Array.Resize(ref leafSpin, count);
        System.Array.Resize(ref leafHomeX, count);
        System.Array.Resize(ref leafCeil, count);
    }

    private void BindLeaf(
        int i,
        Transform root,
        float homeX,
        float ceilY,
        float fall,
        float drift,
        float spin)
    {
        leaves[i] = root;
        leafHomeX[i] = homeX;
        leafCeil[i] = ceilY;
        leafFall[i] = fall;
        leafDrift[i] = drift;
        leafSpin[i] = spin;
        leafPhase[i] = i * 1.37f + homeX * 0.11f;
    }

    private void AllocPuddles(int count)
    {
        puddles = new Transform[count];
        puddleSr = new SpriteRenderer[count];
        puddleGlint = new Transform[count];
        puddleGlintSr = new SpriteRenderer[count];
        puddlePhase = new float[count];
        puddleScale = new Vector3[count];
    }

    private void TrimPuddles(int count)
    {
        System.Array.Resize(ref puddles, count);
        System.Array.Resize(ref puddleSr, count);
        System.Array.Resize(ref puddleGlint, count);
        System.Array.Resize(ref puddleGlintSr, count);
        System.Array.Resize(ref puddlePhase, count);
        System.Array.Resize(ref puddleScale, count);
    }

    private void BindPuddle(int i, Transform root, Transform glint, float phase)
    {
        puddles[i] = root;
        puddleSr[i] = root != null ? root.GetComponent<SpriteRenderer>() : null;
        puddleGlint[i] = glint;
        puddleGlintSr[i] = glint != null ? glint.GetComponent<SpriteRenderer>() : null;
        puddlePhase[i] = phase;
        puddleScale[i] = root != null ? root.localScale : Vector3.one;
    }

    private void AllocSparrows(int count)
    {
        sparrows = new Transform[count];
        sparrowWings = new Transform[count];
        sparrowLeft = new float[count];
        sparrowRight = new float[count];
        sparrowY = new float[count];
        sparrowPhase = new float[count];
        sparrowFace = new float[count];
        sparrowTuck = new float[count];
    }

    private void TrimSparrows(int count)
    {
        System.Array.Resize(ref sparrows, count);
        System.Array.Resize(ref sparrowWings, count);
        System.Array.Resize(ref sparrowLeft, count);
        System.Array.Resize(ref sparrowRight, count);
        System.Array.Resize(ref sparrowY, count);
        System.Array.Resize(ref sparrowPhase, count);
        System.Array.Resize(ref sparrowFace, count);
        System.Array.Resize(ref sparrowTuck, count);
    }

    private void BindSparrow(
        int i,
        Transform root,
        Transform wing,
        float left,
        float right,
        float y,
        float phase)
    {
        sparrows[i] = root;
        sparrowWings[i] = wing;
        sparrowLeft[i] = left;
        sparrowRight[i] = right;
        sparrowY[i] = y;
        sparrowPhase[i] = phase;
        sparrowFace[i] = 1f;
        sparrowTuck[i] = -1f;
    }

    private void AllocCats(int count)
    {
        cats = new Transform[count];
        catTails = new Transform[count];
        catEyes = new SpriteRenderer[count];
        catPhase = new float[count];
        catFace = new float[count];
        catTailRest = new Quaternion[count];
    }

    private void TrimCats(int count)
    {
        System.Array.Resize(ref cats, count);
        System.Array.Resize(ref catTails, count);
        System.Array.Resize(ref catEyes, count);
        System.Array.Resize(ref catPhase, count);
        System.Array.Resize(ref catFace, count);
        System.Array.Resize(ref catTailRest, count);
    }

    private void BindCat(
        int i,
        Transform root,
        Transform tail,
        SpriteRenderer eye,
        float phase,
        float face)
    {
        cats[i] = root;
        catTails[i] = tail;
        catEyes[i] = eye;
        catPhase[i] = phase;
        catFace[i] = face >= 0f ? 1f : -1f;
        catTailRest[i] = tail != null ? tail.localRotation : Quaternion.identity;
        if (root != null)
            root.localScale = new Vector3(catFace[i], 1f, 1f);
    }

    private void AllocLanterns(int count)
    {
        lanternGlow = new SpriteRenderer[count];
        lanternPhase = new float[count];
    }

    private void TrimLanterns(int count)
    {
        System.Array.Resize(ref lanternGlow, count);
        System.Array.Resize(ref lanternPhase, count);
    }

    private void BindLantern(int i, SpriteRenderer glow, float phase)
    {
        lanternGlow[i] = glow;
        lanternPhase[i] = phase;
    }

    private void AllocNests(int count)
    {
        nestSit = new SpriteRenderer[count];
        nestSitHead = new SpriteRenderer[count];
        nestSitXf = new Transform[count];
        nestSitY = new float[count];
        nestPhase = new float[count];
    }

    private void TrimNests(int count)
    {
        System.Array.Resize(ref nestSit, count);
        System.Array.Resize(ref nestSitHead, count);
        System.Array.Resize(ref nestSitXf, count);
        System.Array.Resize(ref nestSitY, count);
        System.Array.Resize(ref nestPhase, count);
    }

    private void BindNest(int i, SpriteRenderer sit, SpriteRenderer sitHead, Transform sitXf, float phase)
    {
        nestSit[i] = sit;
        nestSitHead[i] = sitHead;
        nestSitXf[i] = sitXf;
        nestSitY[i] = sitXf != null ? sitXf.localPosition.y : 0f;
        nestPhase[i] = phase;
        if (sit != null)
            sit.enabled = false;
        if (sitHead != null)
            sitHead.enabled = false;
    }

    private void AllocHouses(int count)
    {
        houseSit = new SpriteRenderer[count];
    }

    private void TrimHouses(int count)
    {
        System.Array.Resize(ref houseSit, count);
    }

    private void BindHouse(int i, SpriteRenderer sit)
    {
        houseSit[i] = sit;
        if (sit != null)
            sit.enabled = false;
    }

    private void AllocBaths(int count)
    {
        bathWater = new Transform[count];
        bathWaterSr = new SpriteRenderer[count];
        bathGlint = new Transform[count];
        bathGlintSr = new SpriteRenderer[count];
        bathDrinkXf = new Transform[count];
        bathDrink = new SpriteRenderer[count];
        bathDrinkHead = new SpriteRenderer[count];
        bathDrinkY = new float[count];
        bathPhase = new float[count];
        bathWaterScale = new Vector3[count];
    }

    private void TrimBaths(int count)
    {
        System.Array.Resize(ref bathWater, count);
        System.Array.Resize(ref bathWaterSr, count);
        System.Array.Resize(ref bathGlint, count);
        System.Array.Resize(ref bathGlintSr, count);
        System.Array.Resize(ref bathDrinkXf, count);
        System.Array.Resize(ref bathDrink, count);
        System.Array.Resize(ref bathDrinkHead, count);
        System.Array.Resize(ref bathDrinkY, count);
        System.Array.Resize(ref bathPhase, count);
        System.Array.Resize(ref bathWaterScale, count);
    }

    private void BindBath(
        int i,
        Transform water,
        Transform glint,
        Transform drinkXf,
        SpriteRenderer drink,
        SpriteRenderer drinkHead,
        float phase)
    {
        bathWater[i] = water;
        bathWaterSr[i] = water != null ? water.GetComponent<SpriteRenderer>() : null;
        bathGlint[i] = glint;
        bathGlintSr[i] = glint != null ? glint.GetComponent<SpriteRenderer>() : null;
        bathDrinkXf[i] = drinkXf;
        bathDrink[i] = drink;
        bathDrinkHead[i] = drinkHead;
        bathDrinkY[i] = drinkXf != null ? drinkXf.localPosition.y : 0f;
        bathPhase[i] = phase;
        bathWaterScale[i] = water != null ? water.localScale : Vector3.one;
        if (drink != null)
            drink.enabled = false;
        if (drinkHead != null)
            drinkHead.enabled = false;
    }

    private void AllocFrogs(int count)
    {
        frogs = new Transform[count];
        frogThroat = new SpriteRenderer[count];
        frogX = new float[count];
        frogY = new float[count];
        frogFace = new float[count];
        frogPhase = new float[count];
    }

    private void TrimFrogs(int count)
    {
        System.Array.Resize(ref frogs, count);
        System.Array.Resize(ref frogThroat, count);
        System.Array.Resize(ref frogX, count);
        System.Array.Resize(ref frogY, count);
        System.Array.Resize(ref frogFace, count);
        System.Array.Resize(ref frogPhase, count);
    }

    private void BindFrog(
        int i,
        Transform root,
        SpriteRenderer throat,
        float x,
        float y,
        float face,
        float phase)
    {
        frogs[i] = root;
        frogThroat[i] = throat;
        frogX[i] = x;
        frogY[i] = y;
        frogFace[i] = face >= 0f ? 1f : -1f;
        frogPhase[i] = phase;
        if (throat != null)
            throat.enabled = false;
    }

    private void AllocDragonflies(int count)
    {
        dragonflies = new Transform[count];
        dragonWingL = new Transform[count];
        dragonWingR = new Transform[count];
        dragonX = new float[count];
        dragonY = new float[count];
        dragonFace = new float[count];
        dragonPhase = new float[count];
    }

    private void TrimDragonflies(int count)
    {
        System.Array.Resize(ref dragonflies, count);
        System.Array.Resize(ref dragonWingL, count);
        System.Array.Resize(ref dragonWingR, count);
        System.Array.Resize(ref dragonX, count);
        System.Array.Resize(ref dragonY, count);
        System.Array.Resize(ref dragonFace, count);
        System.Array.Resize(ref dragonPhase, count);
    }

    private void BindDragonfly(
        int i,
        Transform root,
        Transform wingL,
        Transform wingR,
        float x,
        float y,
        float face,
        float phase)
    {
        dragonflies[i] = root;
        dragonWingL[i] = wingL;
        dragonWingR[i] = wingR;
        dragonX[i] = x;
        dragonY[i] = y;
        dragonFace[i] = face >= 0f ? 1f : -1f;
        dragonPhase[i] = phase;
    }

    private void AllocBees(int count)
    {
        bees = new Transform[count];
        beeWingL = new Transform[count];
        beeWingR = new Transform[count];
        beeX = new float[count];
        beeY = new float[count];
        beeFace = new float[count];
        beePhase = new float[count];
    }

    private void TrimBees(int count)
    {
        System.Array.Resize(ref bees, count);
        System.Array.Resize(ref beeWingL, count);
        System.Array.Resize(ref beeWingR, count);
        System.Array.Resize(ref beeX, count);
        System.Array.Resize(ref beeY, count);
        System.Array.Resize(ref beeFace, count);
        System.Array.Resize(ref beePhase, count);
    }

    private void BindBee(
        int i,
        Transform root,
        Transform wingL,
        Transform wingR,
        float x,
        float y,
        float face,
        float phase)
    {
        bees[i] = root;
        beeWingL[i] = wingL;
        beeWingR[i] = wingR;
        beeX[i] = x;
        beeY[i] = y;
        beeFace[i] = face >= 0f ? 1f : -1f;
        beePhase[i] = phase;
    }

    private void AllocSnails(int count)
    {
        snails = new Transform[count];
        snailAntL = new Transform[count];
        snailAntR = new Transform[count];
        snailX = new float[count];
        snailY = new float[count];
        snailFace = new float[count];
        snailPhase = new float[count];
    }

    private void TrimSnails(int count)
    {
        System.Array.Resize(ref snails, count);
        System.Array.Resize(ref snailAntL, count);
        System.Array.Resize(ref snailAntR, count);
        System.Array.Resize(ref snailX, count);
        System.Array.Resize(ref snailY, count);
        System.Array.Resize(ref snailFace, count);
        System.Array.Resize(ref snailPhase, count);
    }

    private void BindSnail(
        int i,
        Transform root,
        Transform antL,
        Transform antR,
        float x,
        float y,
        float face,
        float phase)
    {
        snails[i] = root;
        snailAntL[i] = antL;
        snailAntR[i] = antR;
        snailX[i] = x;
        snailY[i] = y;
        snailFace[i] = face >= 0f ? 1f : -1f;
        snailPhase[i] = phase;
    }

    private void AllocLizards(int count)
    {
        lizards = new Transform[count];
        lizardTail = new Transform[count];
        lizardX = new float[count];
        lizardY = new float[count];
        lizardFace = new float[count];
        lizardPhase = new float[count];
    }

    private void TrimLizards(int count)
    {
        System.Array.Resize(ref lizards, count);
        System.Array.Resize(ref lizardTail, count);
        System.Array.Resize(ref lizardX, count);
        System.Array.Resize(ref lizardY, count);
        System.Array.Resize(ref lizardFace, count);
        System.Array.Resize(ref lizardPhase, count);
    }

    private void BindLizard(
        int i,
        Transform root,
        Transform tail,
        float x,
        float y,
        float face,
        float phase)
    {
        lizards[i] = root;
        lizardTail[i] = tail;
        lizardX[i] = x;
        lizardY[i] = y;
        lizardFace[i] = face >= 0f ? 1f : -1f;
        lizardPhase[i] = phase;
    }

    private void AllocGrasshoppers(int count)
    {
        hoppers = new Transform[count];
        hopperHind = new Transform[count];
        hopperAnt = new Transform[count];
        hopperX = new float[count];
        hopperY = new float[count];
        hopperFace = new float[count];
        hopperPhase = new float[count];
    }

    private void TrimGrasshoppers(int count)
    {
        System.Array.Resize(ref hoppers, count);
        System.Array.Resize(ref hopperHind, count);
        System.Array.Resize(ref hopperAnt, count);
        System.Array.Resize(ref hopperX, count);
        System.Array.Resize(ref hopperY, count);
        System.Array.Resize(ref hopperFace, count);
        System.Array.Resize(ref hopperPhase, count);
    }

    private void BindGrasshopper(
        int i,
        Transform root,
        Transform hind,
        Transform ant,
        float x,
        float y,
        float face,
        float phase)
    {
        hoppers[i] = root;
        hopperHind[i] = hind;
        hopperAnt[i] = ant;
        hopperX[i] = x;
        hopperY[i] = y;
        hopperFace[i] = face >= 0f ? 1f : -1f;
        hopperPhase[i] = phase;
    }

    private void AllocAnts(int count)
    {
        ants = new Transform[count];
        antAnt = new Transform[count];
        antX = new float[count];
        antY = new float[count];
        antFace = new float[count];
        antPhase = new float[count];
    }

    private void TrimAnts(int count)
    {
        System.Array.Resize(ref ants, count);
        System.Array.Resize(ref antAnt, count);
        System.Array.Resize(ref antX, count);
        System.Array.Resize(ref antY, count);
        System.Array.Resize(ref antFace, count);
        System.Array.Resize(ref antPhase, count);
    }

    private void BindAnt(
        int i,
        Transform root,
        Transform feeler,
        float x,
        float y,
        float face,
        float phase)
    {
        ants[i] = root;
        antAnt[i] = feeler;
        antX[i] = x;
        antY[i] = y;
        antFace[i] = face >= 0f ? 1f : -1f;
        antPhase[i] = phase;
    }

    private void AllocCrickets(int count)
    {
        crickets = new Transform[count];
        cricketWing = new Transform[count];
        cricketAnt = new Transform[count];
        cricketX = new float[count];
        cricketY = new float[count];
        cricketFace = new float[count];
        cricketPhase = new float[count];
    }

    private void TrimCrickets(int count)
    {
        System.Array.Resize(ref crickets, count);
        System.Array.Resize(ref cricketWing, count);
        System.Array.Resize(ref cricketAnt, count);
        System.Array.Resize(ref cricketX, count);
        System.Array.Resize(ref cricketY, count);
        System.Array.Resize(ref cricketFace, count);
        System.Array.Resize(ref cricketPhase, count);
    }

    private void BindCricket(
        int i,
        Transform root,
        Transform wing,
        Transform ant,
        float x,
        float y,
        float face,
        float phase)
    {
        crickets[i] = root;
        cricketWing[i] = wing;
        cricketAnt[i] = ant;
        cricketX[i] = x;
        cricketY[i] = y;
        cricketFace[i] = face >= 0f ? 1f : -1f;
        cricketPhase[i] = phase;
    }

    private void AllocWorms(int count)
    {
        worms = new Transform[count];
        wormMid = new Transform[count];
        wormSheen = new SpriteRenderer[count];
        wormX = new float[count];
        wormY = new float[count];
        wormFace = new float[count];
        wormPhase = new float[count];
    }

    private void TrimWorms(int count)
    {
        System.Array.Resize(ref worms, count);
        System.Array.Resize(ref wormMid, count);
        System.Array.Resize(ref wormSheen, count);
        System.Array.Resize(ref wormX, count);
        System.Array.Resize(ref wormY, count);
        System.Array.Resize(ref wormFace, count);
        System.Array.Resize(ref wormPhase, count);
    }

    private void BindWorm(
        int i,
        Transform root,
        Transform mid,
        SpriteRenderer sheen,
        float x,
        float y,
        float face,
        float phase)
    {
        worms[i] = root;
        wormMid[i] = mid;
        wormSheen[i] = sheen;
        wormX[i] = x;
        wormY[i] = y;
        wormFace[i] = face >= 0f ? 1f : -1f;
        wormPhase[i] = phase;
    }

    private void AllocStriders(int count)
    {
        striders = new Transform[count];
        striderStroke = new Transform[count];
        striderDimple = new SpriteRenderer[count];
        striderX = new float[count];
        striderY = new float[count];
        striderFace = new float[count];
        striderPhase = new float[count];
    }

    private void TrimStriders(int count)
    {
        System.Array.Resize(ref striders, count);
        System.Array.Resize(ref striderStroke, count);
        System.Array.Resize(ref striderDimple, count);
        System.Array.Resize(ref striderX, count);
        System.Array.Resize(ref striderY, count);
        System.Array.Resize(ref striderFace, count);
        System.Array.Resize(ref striderPhase, count);
    }

    private void BindStrider(
        int i,
        Transform root,
        Transform stroke,
        SpriteRenderer dimple,
        float x,
        float y,
        float face,
        float phase)
    {
        striders[i] = root;
        striderStroke[i] = stroke;
        striderDimple[i] = dimple;
        striderX[i] = x;
        striderY[i] = y;
        striderFace[i] = face >= 0f ? 1f : -1f;
        striderPhase[i] = phase;
    }

    private void AllocBats(int count)
    {
        bats = new Transform[count];
        batWingL = new Transform[count];
        batWingR = new Transform[count];
        batEyes = new SpriteRenderer[count];
        batX = new float[count];
        batY = new float[count];
        batPhase = new float[count];
    }

    private void TrimBats(int count)
    {
        System.Array.Resize(ref bats, count);
        System.Array.Resize(ref batWingL, count);
        System.Array.Resize(ref batWingR, count);
        System.Array.Resize(ref batEyes, count);
        System.Array.Resize(ref batX, count);
        System.Array.Resize(ref batY, count);
        System.Array.Resize(ref batPhase, count);
    }

    private void BindBat(
        int i,
        Transform root,
        Transform wingL,
        Transform wingR,
        SpriteRenderer eye,
        float x,
        float y,
        float phase)
    {
        bats[i] = root;
        batWingL[i] = wingL;
        batWingR[i] = wingR;
        batEyes[i] = eye;
        batX[i] = x;
        batY[i] = y;
        batPhase[i] = phase;
    }

    private void AllocWebs(int count)
    {
        spiders = new Transform[count];
        spiderLegs = new Transform[count];
        webDew = new SpriteRenderer[count];
        webX = new float[count];
        webY = new float[count];
        webSpanX = new float[count];
        webSpanY = new float[count];
        webFace = new float[count];
        webPhase = new float[count];
    }

    private void TrimWebs(int count)
    {
        System.Array.Resize(ref spiders, count);
        System.Array.Resize(ref spiderLegs, count);
        System.Array.Resize(ref webDew, count);
        System.Array.Resize(ref webX, count);
        System.Array.Resize(ref webY, count);
        System.Array.Resize(ref webSpanX, count);
        System.Array.Resize(ref webSpanY, count);
        System.Array.Resize(ref webFace, count);
        System.Array.Resize(ref webPhase, count);
    }

    private void BindWeb(
        int i,
        Transform spider,
        Transform legs,
        SpriteRenderer dew,
        float x,
        float y,
        float spanX,
        float spanY,
        float face,
        float phase)
    {
        spiders[i] = spider;
        spiderLegs[i] = legs;
        webDew[i] = dew;
        webX[i] = x;
        webY[i] = y;
        webSpanX[i] = spanX;
        webSpanY[i] = spanY;
        webFace[i] = face >= 0f ? 1f : -1f;
        webPhase[i] = phase;
    }

    private void AllocMice(int count)
    {
        mice = new Transform[count];
        mouseTails = new Transform[count];
        mouseEyes = new SpriteRenderer[count];
        mouseX = new float[count];
        mouseY = new float[count];
        mouseFace = new float[count];
        mousePhase = new float[count];
        mouseTailRest = new Quaternion[count];
    }

    private void TrimMice(int count)
    {
        System.Array.Resize(ref mice, count);
        System.Array.Resize(ref mouseTails, count);
        System.Array.Resize(ref mouseEyes, count);
        System.Array.Resize(ref mouseX, count);
        System.Array.Resize(ref mouseY, count);
        System.Array.Resize(ref mouseFace, count);
        System.Array.Resize(ref mousePhase, count);
        System.Array.Resize(ref mouseTailRest, count);
    }

    private void BindMouse(
        int i,
        Transform root,
        Transform tail,
        SpriteRenderer eye,
        float x,
        float y,
        float face,
        float phase)
    {
        mice[i] = root;
        mouseTails[i] = tail;
        mouseEyes[i] = eye;
        mouseX[i] = x;
        mouseY[i] = y;
        mouseFace[i] = face >= 0f ? 1f : -1f;
        mousePhase[i] = phase;
        mouseTailRest[i] = tail != null ? tail.localRotation : Quaternion.identity;
    }

    private void AllocRabbits(int count)
    {
        rabbits = new Transform[count];
        rabbitHeads = new Transform[count];
        rabbitEars = new Transform[count];
        rabbitX = new float[count];
        rabbitY = new float[count];
        rabbitFace = new float[count];
        rabbitPhase = new float[count];
        rabbitHeadRest = new Quaternion[count];
        rabbitEarRest = new Quaternion[count];
    }

    private void TrimRabbits(int count)
    {
        System.Array.Resize(ref rabbits, count);
        System.Array.Resize(ref rabbitHeads, count);
        System.Array.Resize(ref rabbitEars, count);
        System.Array.Resize(ref rabbitX, count);
        System.Array.Resize(ref rabbitY, count);
        System.Array.Resize(ref rabbitFace, count);
        System.Array.Resize(ref rabbitPhase, count);
        System.Array.Resize(ref rabbitHeadRest, count);
        System.Array.Resize(ref rabbitEarRest, count);
    }

    private void BindRabbit(
        int i,
        Transform root,
        Transform head,
        Transform ear,
        float x,
        float y,
        float face,
        float phase)
    {
        rabbits[i] = root;
        rabbitHeads[i] = head;
        rabbitEars[i] = ear;
        rabbitX[i] = x;
        rabbitY[i] = y;
        rabbitFace[i] = face >= 0f ? 1f : -1f;
        rabbitPhase[i] = phase;
        rabbitHeadRest[i] = head != null ? head.localRotation : Quaternion.identity;
        rabbitEarRest[i] = ear != null ? ear.localRotation : Quaternion.identity;
    }

    private void AllocSquirrels(int count)
    {
        squirrels = new Transform[count];
        squirrelTails = new Transform[count];
        squirrelX = new float[count];
        squirrelY = new float[count];
        squirrelFace = new float[count];
        squirrelPhase = new float[count];
        squirrelTailRest = new Quaternion[count];
    }

    private void TrimSquirrels(int count)
    {
        System.Array.Resize(ref squirrels, count);
        System.Array.Resize(ref squirrelTails, count);
        System.Array.Resize(ref squirrelX, count);
        System.Array.Resize(ref squirrelY, count);
        System.Array.Resize(ref squirrelFace, count);
        System.Array.Resize(ref squirrelPhase, count);
        System.Array.Resize(ref squirrelTailRest, count);
    }

    private void BindSquirrel(
        int i,
        Transform root,
        Transform tail,
        float x,
        float y,
        float face,
        float phase)
    {
        squirrels[i] = root;
        squirrelTails[i] = tail;
        squirrelX[i] = x;
        squirrelY[i] = y;
        squirrelFace[i] = face >= 0f ? 1f : -1f;
        squirrelPhase[i] = phase;
        squirrelTailRest[i] = tail != null ? tail.localRotation : Quaternion.identity;
    }

    private void AllocOwls(int count)
    {
        owls = new Transform[count];
        owlHeads = new Transform[count];
        owlEyes = new SpriteRenderer[count];
        owlX = new float[count];
        owlY = new float[count];
        owlFace = new float[count];
        owlPhase = new float[count];
        owlHeadRest = new Quaternion[count];
    }

    private void TrimOwls(int count)
    {
        System.Array.Resize(ref owls, count);
        System.Array.Resize(ref owlHeads, count);
        System.Array.Resize(ref owlEyes, count);
        System.Array.Resize(ref owlX, count);
        System.Array.Resize(ref owlY, count);
        System.Array.Resize(ref owlFace, count);
        System.Array.Resize(ref owlPhase, count);
        System.Array.Resize(ref owlHeadRest, count);
    }

    private void BindOwl(
        int i,
        Transform root,
        Transform head,
        SpriteRenderer eye,
        float x,
        float y,
        float face,
        float phase)
    {
        owls[i] = root;
        owlHeads[i] = head;
        owlEyes[i] = eye;
        owlX[i] = x;
        owlY[i] = y;
        owlFace[i] = face >= 0f ? 1f : -1f;
        owlPhase[i] = phase;
        owlHeadRest[i] = head != null ? head.localRotation : Quaternion.identity;
    }

    private void AllocRaccoons(int count)
    {
        raccoons = new Transform[count];
        raccoonHeads = new Transform[count];
        raccoonTails = new Transform[count];
        raccoonEyes = new SpriteRenderer[count];
        raccoonX = new float[count];
        raccoonY = new float[count];
        raccoonFace = new float[count];
        raccoonPhase = new float[count];
        raccoonHeadRest = new Quaternion[count];
        raccoonTailRest = new Quaternion[count];
    }

    private void TrimRaccoons(int count)
    {
        System.Array.Resize(ref raccoons, count);
        System.Array.Resize(ref raccoonHeads, count);
        System.Array.Resize(ref raccoonTails, count);
        System.Array.Resize(ref raccoonEyes, count);
        System.Array.Resize(ref raccoonX, count);
        System.Array.Resize(ref raccoonY, count);
        System.Array.Resize(ref raccoonFace, count);
        System.Array.Resize(ref raccoonPhase, count);
        System.Array.Resize(ref raccoonHeadRest, count);
        System.Array.Resize(ref raccoonTailRest, count);
    }

    private void BindRaccoon(
        int i,
        Transform root,
        Transform head,
        Transform tail,
        SpriteRenderer eye,
        float x,
        float y,
        float face,
        float phase)
    {
        raccoons[i] = root;
        raccoonHeads[i] = head;
        raccoonTails[i] = tail;
        raccoonEyes[i] = eye;
        raccoonX[i] = x;
        raccoonY[i] = y;
        raccoonFace[i] = face >= 0f ? 1f : -1f;
        raccoonPhase[i] = phase;
        raccoonHeadRest[i] = head != null ? head.localRotation : Quaternion.identity;
        raccoonTailRest[i] = tail != null ? tail.localRotation : Quaternion.identity;
    }

    private void AllocTurtles(int count)
    {
        turtles = new Transform[count];
        turtleHeads = new Transform[count];
        turtleX = new float[count];
        turtleY = new float[count];
        turtleFace = new float[count];
        turtlePhase = new float[count];
        turtleHeadRest = new Vector3[count];
    }

    private void TrimTurtles(int count)
    {
        System.Array.Resize(ref turtles, count);
        System.Array.Resize(ref turtleHeads, count);
        System.Array.Resize(ref turtleX, count);
        System.Array.Resize(ref turtleY, count);
        System.Array.Resize(ref turtleFace, count);
        System.Array.Resize(ref turtlePhase, count);
        System.Array.Resize(ref turtleHeadRest, count);
    }

    private void BindTurtle(
        int i,
        Transform root,
        Transform head,
        float x,
        float y,
        float face,
        float phase)
    {
        turtles[i] = root;
        turtleHeads[i] = head;
        turtleX[i] = x;
        turtleY[i] = y;
        turtleFace[i] = face >= 0f ? 1f : -1f;
        turtlePhase[i] = phase;
        turtleHeadRest[i] = head != null ? head.localPosition : Vector3.zero;
    }

    private void AllocDucks(int count)
    {
        ducks = new Transform[count];
        duckHeads = new Transform[count];
        duckX = new float[count];
        duckY = new float[count];
        duckFace = new float[count];
        duckPhase = new float[count];
        duckHeadRest = new Quaternion[count];
    }

    private void TrimDucks(int count)
    {
        System.Array.Resize(ref ducks, count);
        System.Array.Resize(ref duckHeads, count);
        System.Array.Resize(ref duckX, count);
        System.Array.Resize(ref duckY, count);
        System.Array.Resize(ref duckFace, count);
        System.Array.Resize(ref duckPhase, count);
        System.Array.Resize(ref duckHeadRest, count);
    }

    private void BindDuck(
        int i,
        Transform root,
        Transform head,
        float x,
        float y,
        float face,
        float phase)
    {
        ducks[i] = root;
        duckHeads[i] = head;
        duckX[i] = x;
        duckY[i] = y;
        duckFace[i] = face >= 0f ? 1f : -1f;
        duckPhase[i] = phase;
        duckHeadRest[i] = head != null ? head.localRotation : Quaternion.identity;
    }

    private void AllocHerons(int count)
    {
        herons = new Transform[count];
        heronNecks = new Transform[count];
        heronX = new float[count];
        heronY = new float[count];
        heronFace = new float[count];
        heronPhase = new float[count];
        heronNeckRest = new Quaternion[count];
    }

    private void TrimHerons(int count)
    {
        System.Array.Resize(ref herons, count);
        System.Array.Resize(ref heronNecks, count);
        System.Array.Resize(ref heronX, count);
        System.Array.Resize(ref heronY, count);
        System.Array.Resize(ref heronFace, count);
        System.Array.Resize(ref heronPhase, count);
        System.Array.Resize(ref heronNeckRest, count);
    }

    private void BindHeron(
        int i,
        Transform root,
        Transform neck,
        float x,
        float y,
        float face,
        float phase)
    {
        herons[i] = root;
        heronNecks[i] = neck;
        heronX[i] = x;
        heronY[i] = y;
        heronFace[i] = face >= 0f ? 1f : -1f;
        heronPhase[i] = phase;
        heronNeckRest[i] = neck != null ? neck.localRotation : Quaternion.identity;
    }

    private void AllocMinnows(int count)
    {
        minnows = new Transform[count];
        minnowTails = new Transform[count];
        minnowFlash = new SpriteRenderer[count];
        minnowX = new float[count];
        minnowY = new float[count];
        minnowFace = new float[count];
        minnowPhase = new float[count];
    }

    private void TrimMinnows(int count)
    {
        System.Array.Resize(ref minnows, count);
        System.Array.Resize(ref minnowTails, count);
        System.Array.Resize(ref minnowFlash, count);
        System.Array.Resize(ref minnowX, count);
        System.Array.Resize(ref minnowY, count);
        System.Array.Resize(ref minnowFace, count);
        System.Array.Resize(ref minnowPhase, count);
    }

    private void BindMinnow(
        int i,
        Transform root,
        Transform tail,
        SpriteRenderer flash,
        float x,
        float y,
        float face,
        float phase)
    {
        minnows[i] = root;
        minnowTails[i] = tail;
        minnowFlash[i] = flash;
        minnowX[i] = x;
        minnowY[i] = y;
        minnowFace[i] = face >= 0f ? 1f : -1f;
        minnowPhase[i] = phase;
    }

    private Transform MakeLantern(Vector3 peg, float hang, out SpriteRenderer glow)
    {
        Color iron = new Color(0.22f, 0.16f, 0.10f, 1f);
        Color bowl = new Color(0.38f, 0.22f, 0.10f, 1f);

        GameObject pivot = new GameObject("YardLantern");
        pivot.transform.SetParent(transform, false);
        pivot.transform.position = peg;

        GameObject arm = new GameObject("Arm");
        arm.transform.SetParent(pivot.transform, false);
        arm.transform.localPosition = new Vector3(hang * 0.5f, -0.012f, 0f);
        SpriteRenderer asr = arm.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            asr,
            new Vector2(Mathf.Abs(hang) + 0.02f, 0.016f),
            HumanVisualShape.Box,
            null,
            iron,
            LanternOrder);

        GameObject chain = new GameObject("Chain");
        chain.transform.SetParent(pivot.transform, false);
        chain.transform.localPosition = new Vector3(hang, -0.055f, 0f);
        SpriteRenderer csr = chain.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            csr,
            new Vector2(0.014f, 0.08f),
            HumanVisualShape.Box,
            null,
            iron,
            LanternOrder);

        GameObject lamp = new GameObject("Bowl");
        lamp.transform.SetParent(pivot.transform, false);
        lamp.transform.localPosition = new Vector3(hang, -0.118f, 0f);
        SpriteRenderer bsr = lamp.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            bsr,
            new Vector2(0.072f, 0.058f),
            HumanVisualShape.Ellipse,
            null,
            bowl,
            LanternOrder + 1);

        GameObject glowGo = new GameObject("Glow");
        glowGo.transform.SetParent(lamp.transform, false);
        glowGo.transform.localPosition = Vector3.zero;
        SpriteRenderer gsr = glowGo.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            gsr,
            new Vector2(0.22f, 0.22f),
            HumanVisualShape.Ellipse,
            null,
            new Color(1f, 0.72f, 0.28f, 0.16f),
            LanternGlowOrder);
        SpriteLighting.ApplyUnlit(gsr);
        glow = gsr;
        return pivot.transform;
    }

    private Transform MakeGate(LevelTest1.GateHinge hinge)
    {
        Color wood = new Color(0.36f, 0.23f, 0.12f, 1f);
        Color dark = new Color(0.22f, 0.14f, 0.08f, 1f);
        Color iron = new Color(0.28f, 0.26f, 0.24f, 1f);

        GameObject pivot = new GameObject("YardGate");
        pivot.transform.SetParent(transform, false);
        pivot.transform.position = new Vector3(hinge.x, hinge.y, 0f);
        pivot.transform.localRotation = Quaternion.Euler(0f, 0f, hinge.restZ);

        float w = hinge.width;
        float h = hinge.height;
        AddGateBar(pivot.transform, "RailTop", new Vector3(w * 0.5f, h * 0.38f, 0f), new Vector2(w, 0.038f), wood, GateOrder + 1);
        AddGateBar(pivot.transform, "RailMid", new Vector3(w * 0.5f, 0f, 0f), new Vector2(w, 0.034f), wood, GateOrder + 1);
        AddGateBar(pivot.transform, "RailBot", new Vector3(w * 0.5f, -h * 0.38f, 0f), new Vector2(w, 0.038f), wood, GateOrder + 1);

        for (int s = 0; s < 4; s++)
        {
            float u = (s + 0.5f) / 4f;
            float lx = 0.06f + u * (w - 0.12f);
            AddGateBar(
                pivot.transform,
                "Slat",
                new Vector3(lx, 0f, 0f),
                new Vector2(0.040f, h * 0.92f),
                dark,
                GateOrder);
        }

        GameObject brace = new GameObject("Brace");
        brace.transform.SetParent(pivot.transform, false);
        brace.transform.localPosition = new Vector3(w * 0.5f, 0f, 0f);
        brace.transform.localRotation = Quaternion.Euler(0f, 0f, -28f);
        SpriteRenderer bsr = brace.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            bsr,
            new Vector2(w * 0.92f, 0.028f),
            HumanVisualShape.Box,
            null,
            wood,
            GateOrder + 1);

        AddGateBar(pivot.transform, "PinTop", new Vector3(0.012f, h * 0.28f, 0f), new Vector2(0.028f, 0.028f), iron, GateOrder + 2);
        AddGateBar(pivot.transform, "PinBot", new Vector3(0.012f, -h * 0.28f, 0f), new Vector2(0.028f, 0.028f), iron, GateOrder + 2);
        AddGateBar(pivot.transform, "Latch", new Vector3(w - 0.04f, 0.02f, 0f), new Vector2(0.055f, 0.022f), iron, GateOrder + 2);
        return pivot.transform;
    }

    private Transform MakeNest(
        Vector3 peg,
        float face,
        out SpriteRenderer sit,
        out SpriteRenderer sitHead,
        out Transform sitXf)
    {
        Color twig = new Color(0.34f, 0.22f, 0.12f, 1f);
        Color cup = new Color(0.42f, 0.28f, 0.14f, 1f);
        Color lining = new Color(0.28f, 0.18f, 0.10f, 1f);

        GameObject pivot = new GameObject("YardNest");
        pivot.transform.SetParent(transform, false);
        pivot.transform.position = peg;

        GameObject bowl = new GameObject("Cup");
        bowl.transform.SetParent(pivot.transform, false);
        bowl.transform.localPosition = new Vector3(0f, 0.022f, 0f);
        SpriteRenderer csr = bowl.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            csr,
            new Vector2(0.11f, 0.048f),
            HumanVisualShape.Ellipse,
            null,
            cup,
            NestOrder);

        GameObject inner = new GameObject("Lining");
        inner.transform.SetParent(pivot.transform, false);
        inner.transform.localPosition = new Vector3(0f, 0.018f, 0f);
        SpriteRenderer lsr = inner.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            lsr,
            new Vector2(0.072f, 0.028f),
            HumanVisualShape.Ellipse,
            null,
            lining,
            NestOrder + 1);

        AddNestTwig(pivot.transform, new Vector3(-0.038f, 0.012f, 0f), new Vector2(0.055f, 0.010f), -28f, twig);
        AddNestTwig(pivot.transform, new Vector3(0.040f, 0.010f, 0f), new Vector2(0.050f, 0.009f), 22f, twig);
        AddNestTwig(pivot.transform, new Vector3(0.004f, -0.004f, 0f), new Vector2(0.070f, 0.008f), 8f, twig);

        GameObject bird = new GameObject("Sitter");
        bird.transform.SetParent(pivot.transform, false);
        bird.transform.localPosition = new Vector3(0.012f, 0.038f, 0f);
        SpriteRenderer bsr = bird.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            bsr,
            new Vector2(0.052f, 0.028f),
            HumanVisualShape.Ellipse,
            null,
            new Color(0.24f, 0.19f, 0.14f, 0f),
            SparrowOrder);
        bsr.enabled = false;

        GameObject head = new GameObject("Head");
        head.transform.SetParent(bird.transform, false);
        head.transform.localPosition = new Vector3(0.022f, 0.008f, 0f);
        SpriteRenderer hsr = head.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            hsr,
            new Vector2(0.022f, 0.018f),
            HumanVisualShape.Ellipse,
            null,
            new Color(0.24f, 0.19f, 0.14f, 0f),
            SparrowOrder + 1);
        hsr.enabled = false;

        float sx = face >= 0f ? 1f : -1f;
        pivot.transform.localScale = new Vector3(sx, 1f, 1f);
        sit = bsr;
        sitHead = hsr;
        sitXf = bird.transform;
        return pivot.transform;
    }

    private static void AddNestTwig(Transform parent, Vector3 local, Vector2 size, float tilt, Color tint)
    {
        GameObject go = new GameObject("Twig");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = local;
        go.transform.localRotation = Quaternion.Euler(0f, 0f, tilt);
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(sr, size, HumanVisualShape.Box, null, tint, NestOrder);
    }

    // Returns the stem pivot so BindSway tips the bloom, not the pot.
    private Transform MakeFlowerPot(Vector3 foot, float face, float tintU)
    {
        Color clay = new Color(0.72f, 0.38f, 0.22f, 1f);
        Color rim = new Color(0.62f, 0.32f, 0.18f, 1f);
        Color soil = new Color(0.22f, 0.14f, 0.08f, 1f);
        Color bloom = FlowerTint(tintU);
        Color leaf = new Color(0.30f, 0.52f, 0.24f, 1f);
        float sx = face >= 0f ? 1f : -1f;

        GameObject root = new GameObject("YardPot");
        root.transform.SetParent(transform, false);
        root.transform.position = foot;
        root.transform.localScale = new Vector3(sx, 1f, 1f);

        GameObject body = new GameObject("Clay");
        body.transform.SetParent(root.transform, false);
        body.transform.localPosition = new Vector3(0f, 0.046f, 0f);
        SpriteRenderer bsr = body.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            bsr,
            new Vector2(0.092f, 0.088f),
            HumanVisualShape.Box,
            null,
            clay,
            PotOrder);

        GameObject lip = new GameObject("Rim");
        lip.transform.SetParent(root.transform, false);
        lip.transform.localPosition = new Vector3(0f, 0.092f, 0f);
        SpriteRenderer rsr = lip.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            rsr,
            new Vector2(0.112f, 0.022f),
            HumanVisualShape.Box,
            null,
            rim,
            PotOrder + 1);

        GameObject dirt = new GameObject("Soil");
        dirt.transform.SetParent(root.transform, false);
        dirt.transform.localPosition = new Vector3(0f, 0.100f, 0f);
        SpriteRenderer dsr = dirt.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            dsr,
            new Vector2(0.078f, 0.016f),
            HumanVisualShape.Ellipse,
            null,
            soil,
            PotOrder + 1);

        GameObject stem = new GameObject("Stem");
        stem.transform.SetParent(root.transform, false);
        stem.transform.localPosition = new Vector3(0f, 0.100f, 0f);

        GameObject stalk = new GameObject("Stalk");
        stalk.transform.SetParent(stem.transform, false);
        stalk.transform.localPosition = new Vector3(0.004f, 0.072f, 0f);
        SpriteRenderer ssr = stalk.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            ssr,
            new Vector2(0.014f, 0.140f),
            HumanVisualShape.Box,
            null,
            new Color(0.28f, 0.46f, 0.20f, 1f),
            PotOrder + 2);

        GameObject blade = new GameObject("Leaf");
        blade.transform.SetParent(stem.transform, false);
        blade.transform.localPosition = new Vector3(-0.028f, 0.062f, 0f);
        blade.transform.localRotation = Quaternion.Euler(0f, 0f, 28f);
        SpriteRenderer lsr = blade.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            lsr,
            new Vector2(0.048f, 0.018f),
            HumanVisualShape.Ellipse,
            null,
            leaf,
            PotOrder + 2);

        AddPotBloom(stem.transform, new Vector3(0.006f, 0.148f, 0f), 0.046f, bloom);
        AddPotBloom(stem.transform, new Vector3(-0.022f, 0.122f, 0f), 0.032f, Color.Lerp(bloom, Color.white, 0.22f));
        AddPotBloom(stem.transform, new Vector3(0.028f, 0.118f, 0f), 0.028f, Color.Lerp(bloom, leaf, 0.18f));
        return stem.transform;
    }

    private Transform MakeBirdhouse(LevelTest1.BirdhousePeg peg, out SpriteRenderer sit)
    {
        float surface = LevelTest1.SurfaceAt(peg.x);
        float houseY = peg.stake ? surface + 0.52f : peg.y;
        Color wood = new Color(0.40f, 0.26f, 0.13f, 1f);
        Color dark = new Color(0.24f, 0.15f, 0.08f, 1f);
        Color roof = new Color(0.50f, 0.24f, 0.14f, 1f);
        Color hole = new Color(0.10f, 0.07f, 0.05f, 1f);
        float sx = peg.face >= 0f ? 1f : -1f;

        GameObject root = new GameObject("YardHouse");
        root.transform.SetParent(transform, false);
        root.transform.position = new Vector3(peg.x, houseY, 0f);
        root.transform.localScale = new Vector3(sx, 1f, 1f);

        if (peg.stake)
        {
            float poleH = houseY - surface - 0.046f;
            if (poleH < 0.12f)
                poleH = 0.12f;
            AddHousePart(
                root.transform,
                "Stake",
                new Vector3(0f, -poleH * 0.5f - 0.046f, 0f),
                new Vector2(0.048f, poleH),
                0f,
                dark,
                HouseOrder,
                HumanVisualShape.Box);
        }

        AddHousePart(
            root.transform,
            "Body",
            Vector3.zero,
            new Vector2(0.100f, 0.092f),
            0f,
            wood,
            HouseOrder,
            HumanVisualShape.Box);
        AddHousePart(
            root.transform,
            "Roof",
            new Vector3(0f, 0.062f, 0f),
            new Vector2(0.134f, 0.036f),
            0f,
            roof,
            HouseOrder + 1,
            HumanVisualShape.Box);
        AddHousePart(
            root.transform,
            "Peak",
            new Vector3(0f, 0.084f, 0f),
            new Vector2(0.048f, 0.028f),
            0f,
            roof,
            HouseOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            root.transform,
            "Hole",
            new Vector3(0.006f, 0.004f, 0f),
            new Vector2(0.036f, 0.036f),
            0f,
            hole,
            HouseOrder + 2,
            HumanVisualShape.Ellipse);
        AddHousePart(
            root.transform,
            "Perch",
            new Vector3(0.058f, -0.020f, 0f),
            new Vector2(0.042f, 0.010f),
            0f,
            dark,
            HouseOrder + 1,
            HumanVisualShape.Box);

        GameObject bird = new GameObject("Sitter");
        bird.transform.SetParent(root.transform, false);
        bird.transform.localPosition = new Vector3(0.006f, 0.004f, 0f);
        SpriteRenderer bsr = bird.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            bsr,
            new Vector2(0.026f, 0.022f),
            HumanVisualShape.Ellipse,
            null,
            new Color(0.22f, 0.17f, 0.12f, 0f),
            SparrowOrder);
        bsr.enabled = false;
        sit = bsr;
        return root.transform;
    }

    private void MakeBirdBath(
        Vector3 foot,
        float face,
        out Transform waterXf,
        out Transform glintXf,
        out Transform drinkXf,
        out SpriteRenderer drink,
        out SpriteRenderer drinkHead)
    {
        Color stone = new Color(0.52f, 0.50f, 0.46f, 1f);
        Color dark = new Color(0.36f, 0.34f, 0.30f, 1f);
        Color rim = new Color(0.60f, 0.58f, 0.52f, 1f);
        float sx = face >= 0f ? 1f : -1f;

        GameObject root = new GameObject("YardBath");
        root.transform.SetParent(transform, false);
        root.transform.position = foot;
        root.transform.localScale = new Vector3(sx, 1f, 1f);

        AddHousePart(
            root.transform,
            "Pedestal",
            new Vector3(0f, 0.11f, 0f),
            new Vector2(0.048f, 0.22f),
            0f,
            dark,
            BathOrder,
            HumanVisualShape.Box);
        AddHousePart(
            root.transform,
            "Bowl",
            new Vector3(0f, 0.232f, 0f),
            new Vector2(0.168f, 0.048f),
            0f,
            stone,
            BathOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            root.transform,
            "Rim",
            new Vector3(0f, 0.250f, 0f),
            new Vector2(0.176f, 0.016f),
            0f,
            rim,
            BathOrder + 1,
            HumanVisualShape.Ellipse);

        GameObject water = new GameObject("Water");
        water.transform.SetParent(root.transform, false);
        water.transform.localPosition = new Vector3(0f, 0.246f, 0f);
        SpriteRenderer wsr = water.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            wsr,
            new Vector2(0.124f, 0.022f),
            HumanVisualShape.Ellipse,
            null,
            new Color(0.28f, 0.52f, 0.64f, 0.72f),
            BathOrder + 2);
        SpriteLighting.ApplyUnlit(wsr);

        GameObject shine = new GameObject("Glint");
        shine.transform.SetParent(water.transform, false);
        shine.transform.localPosition = new Vector3(0.018f, 0.003f, 0f);
        SpriteRenderer gsr = shine.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            gsr,
            new Vector2(0.048f, 0.010f),
            HumanVisualShape.Ellipse,
            null,
            new Color(0.62f, 0.78f, 0.86f, 0.32f),
            BathOrder + 3);
        SpriteLighting.ApplyUnlit(gsr);

        GameObject bird = new GameObject("Drinker");
        bird.transform.SetParent(root.transform, false);
        bird.transform.localPosition = new Vector3(0.058f, 0.278f, 0f);
        SpriteRenderer bsr = bird.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            bsr,
            new Vector2(0.046f, 0.026f),
            HumanVisualShape.Ellipse,
            null,
            new Color(0.24f, 0.19f, 0.14f, 0f),
            SparrowOrder);
        bsr.enabled = false;

        GameObject head = new GameObject("Head");
        head.transform.SetParent(bird.transform, false);
        head.transform.localPosition = new Vector3(-0.018f, -0.002f, 0f);
        SpriteRenderer hsr = head.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            hsr,
            new Vector2(0.020f, 0.016f),
            HumanVisualShape.Ellipse,
            null,
            new Color(0.24f, 0.19f, 0.14f, 0f),
            SparrowOrder + 1);
        hsr.enabled = false;

        waterXf = water.transform;
        glintXf = shine.transform;
        drinkXf = bird.transform;
        drink = bsr;
        drinkHead = hsr;
    }

    private Transform MakeFrog(Vector3 pos, float tintU, out SpriteRenderer throat)
    {
        Color skin = Color.Lerp(
            new Color(0.30f, 0.46f, 0.22f, 1f),
            new Color(0.44f, 0.36f, 0.16f, 1f),
            tintU);
        Color dark = Color.Lerp(skin, new Color(0.16f, 0.18f, 0.08f, 1f), 0.45f);

        GameObject go = new GameObject("YardFrog");
        go.transform.SetParent(transform, false);
        go.transform.position = pos;

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            sr,
            new Vector2(0.108f, 0.048f),
            HumanVisualShape.Ellipse,
            null,
            skin,
            FrogOrder);

        AddHousePart(
            go.transform,
            "Head",
            new Vector3(0.046f, 0.012f, 0f),
            new Vector2(0.046f, 0.038f),
            0f,
            skin,
            FrogOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "EyeL",
            new Vector3(0.038f, 0.030f, 0f),
            new Vector2(0.016f, 0.016f),
            0f,
            dark,
            FrogOrder + 2,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "EyeR",
            new Vector3(0.054f, 0.028f, 0f),
            new Vector2(0.014f, 0.014f),
            0f,
            dark,
            FrogOrder + 2,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Stripe",
            new Vector3(-0.008f, 0.006f, 0f),
            new Vector2(0.050f, 0.016f),
            0f,
            dark,
            FrogOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Hind",
            new Vector3(-0.048f, -0.006f, 0f),
            new Vector2(0.042f, 0.022f),
            -18f,
            dark,
            FrogOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Fore",
            new Vector3(0.028f, -0.012f, 0f),
            new Vector2(0.024f, 0.014f),
            12f,
            dark,
            FrogOrder,
            HumanVisualShape.Ellipse);

        GameObject sack = new GameObject("Throat");
        sack.transform.SetParent(go.transform, false);
        sack.transform.localPosition = new Vector3(0.036f, -0.004f, 0f);
        SpriteRenderer tsr = sack.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            tsr,
            new Vector2(0.036f, 0.022f),
            HumanVisualShape.Ellipse,
            null,
            new Color(0.78f, 0.62f, 0.42f, 0f),
            FrogOrder + 2);
        SpriteLighting.ApplyUnlit(tsr);
        tsr.enabled = false;
        throat = tsr;
        return go.transform;
    }

    private Transform MakeDragonfly(Vector3 pos, float face, out Transform wingL, out Transform wingR)
    {
        Color body = new Color(0.16f, 0.46f, 0.40f, 1f);
        Color dark = new Color(0.10f, 0.28f, 0.24f, 1f);
        Color wing = new Color(0.78f, 0.90f, 0.84f, 0.52f);
        Color far = new Color(0.62f, 0.76f, 0.70f, 0.40f);
        float sign = face >= 0f ? 1f : -1f;

        GameObject go = new GameObject("YardDragonfly");
        go.transform.SetParent(transform, false);
        go.transform.position = pos;
        go.transform.localScale = new Vector3(sign, 1f, 1f);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            sr,
            new Vector2(0.112f, 0.016f),
            HumanVisualShape.Ellipse,
            null,
            body,
            DragonflyOrder);

        AddHousePart(
            go.transform,
            "Thorax",
            new Vector3(0.038f, 0.004f, 0f),
            new Vector2(0.036f, 0.024f),
            0f,
            dark,
            DragonflyOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Head",
            new Vector3(0.062f, 0.006f, 0f),
            new Vector2(0.022f, 0.020f),
            0f,
            body,
            DragonflyOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Eye",
            new Vector3(0.068f, 0.010f, 0f),
            new Vector2(0.012f, 0.012f),
            0f,
            dark,
            DragonflyOrder + 2,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Tail",
            new Vector3(-0.052f, -0.004f, 0f),
            new Vector2(0.028f, 0.010f),
            -8f,
            dark,
            DragonflyOrder,
            HumanVisualShape.Ellipse);

        wingL = MakeDragonWing(go.transform, "WingNear", new Vector3(0.030f, 0.012f, 0f), 8f, wing, DragonflyOrder + 1);
        wingR = MakeDragonWing(go.transform, "WingFar", new Vector3(0.026f, 0.006f, 0f), -8f, far, DragonflyOrder - 1);
        MakeDragonWing(wingL, "HindNear", new Vector3(-0.012f, -0.006f, 0f), -6f, wing, DragonflyOrder + 1);
        MakeDragonWing(wingR, "HindFar", new Vector3(-0.010f, -0.005f, 0f), 6f, far, DragonflyOrder - 1);
        return go.transform;
    }

    private static Transform MakeDragonWing(
        Transform parent,
        string name,
        Vector3 local,
        float tilt,
        Color tint,
        int order)
    {
        GameObject wing = new GameObject(name);
        wing.transform.SetParent(parent, false);
        wing.transform.localPosition = local;
        wing.transform.localRotation = Quaternion.Euler(0f, 0f, tilt);
        SpriteRenderer sr = wing.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            sr,
            new Vector2(0.092f, 0.016f),
            HumanVisualShape.Ellipse,
            null,
            tint,
            order);
        return wing.transform;
    }

    private Transform MakeBee(Vector3 pos, float face, out Transform wingL, out Transform wingR)
    {
        Color gold = new Color(0.90f, 0.68f, 0.18f, 1f);
        Color stripe = new Color(0.18f, 0.12f, 0.06f, 1f);
        Color wing = new Color(0.92f, 0.94f, 0.86f, 0.48f);
        Color far = new Color(0.78f, 0.80f, 0.72f, 0.36f);
        float sign = face >= 0f ? 1f : -1f;

        GameObject go = new GameObject("YardBee");
        go.transform.SetParent(transform, false);
        go.transform.position = pos;
        go.transform.localScale = new Vector3(sign, 1f, 1f);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            sr,
            new Vector2(0.028f, 0.016f),
            HumanVisualShape.Ellipse,
            null,
            gold,
            BeeOrder);

        AddHousePart(
            go.transform,
            "Stripe",
            new Vector3(-0.004f, 0f, 0f),
            new Vector2(0.010f, 0.014f),
            0f,
            stripe,
            BeeOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Thorax",
            new Vector3(0.010f, 0.002f, 0f),
            new Vector2(0.014f, 0.014f),
            0f,
            stripe,
            BeeOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Head",
            new Vector3(0.018f, 0.003f, 0f),
            new Vector2(0.010f, 0.010f),
            0f,
            gold,
            BeeOrder + 1,
            HumanVisualShape.Ellipse);

        wingL = MakeBeeWing(go.transform, "WingNear", new Vector3(0.006f, 0.008f, 0f), 12f, wing, BeeOrder + 1);
        wingR = MakeBeeWing(go.transform, "WingFar", new Vector3(0.004f, 0.004f, 0f), -12f, far, BeeOrder - 1);
        return go.transform;
    }

    private Transform MakeSnail(Vector3 pos, float face, out Transform antL, out Transform antR)
    {
        Color foot = new Color(0.62f, 0.52f, 0.40f, 1f);
        Color shell = new Color(0.70f, 0.42f, 0.24f, 1f);
        Color band = new Color(0.42f, 0.24f, 0.14f, 1f);
        Color dark = new Color(0.28f, 0.18f, 0.10f, 1f);
        float sign = face >= 0f ? 1f : -1f;

        GameObject go = new GameObject("YardSnail");
        go.transform.SetParent(transform, false);
        go.transform.position = pos;
        go.transform.localScale = new Vector3(sign, 1f, 1f);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            sr,
            new Vector2(0.022f, 0.048f),
            HumanVisualShape.Ellipse,
            null,
            foot,
            SnailOrder);

        AddHousePart(
            go.transform,
            "Head",
            new Vector3(0.002f, 0.026f, 0f),
            new Vector2(0.016f, 0.014f),
            0f,
            foot,
            SnailOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Shell",
            new Vector3(0.016f, 0.002f, 0f),
            new Vector2(0.034f, 0.030f),
            12f,
            shell,
            SnailOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Band",
            new Vector3(0.016f, 0.004f, 0f),
            new Vector2(0.018f, 0.012f),
            12f,
            band,
            SnailOrder + 2,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Umbilicus",
            new Vector3(0.012f, 0.002f, 0f),
            new Vector2(0.010f, 0.010f),
            0f,
            dark,
            SnailOrder + 2,
            HumanVisualShape.Ellipse);

        antL = MakeSnailAntenna(go.transform, "AntNear", new Vector3(0.006f, 0.038f, 0f), 58f);
        antR = MakeSnailAntenna(go.transform, "AntFar", new Vector3(0.012f, 0.036f, 0f), 42f);
        return go.transform;
    }

    private static Transform MakeSnailAntenna(Transform parent, string name, Vector3 local, float tilt)
    {
        GameObject ant = new GameObject(name);
        ant.transform.SetParent(parent, false);
        ant.transform.localPosition = local;
        ant.transform.localRotation = Quaternion.Euler(0f, 0f, tilt);
        SpriteRenderer sr = ant.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            sr,
            new Vector2(0.006f, 0.022f),
            HumanVisualShape.Box,
            null,
            new Color(0.48f, 0.38f, 0.28f, 1f),
            SnailOrder + 1);
        return ant.transform;
    }

    private Transform MakeLizard(Vector3 pos, float face, out Transform tail)
    {
        Color skin = new Color(0.40f, 0.48f, 0.22f, 1f);
        Color dark = new Color(0.22f, 0.26f, 0.12f, 1f);
        Color pale = new Color(0.52f, 0.50f, 0.32f, 1f);
        float sign = face >= 0f ? 1f : -1f;

        GameObject go = new GameObject("YardLizard");
        go.transform.SetParent(transform, false);
        go.transform.position = pos;
        go.transform.localScale = new Vector3(sign, 1f, 1f);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            sr,
            new Vector2(0.022f, 0.070f),
            HumanVisualShape.Ellipse,
            null,
            skin,
            LizardOrder);

        AddHousePart(
            go.transform,
            "Belly",
            new Vector3(0.006f, 0.002f, 0f),
            new Vector2(0.010f, 0.048f),
            0f,
            pale,
            LizardOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Head",
            new Vector3(0.004f, 0.042f, 0f),
            new Vector2(0.022f, 0.018f),
            8f,
            skin,
            LizardOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Eye",
            new Vector3(0.010f, 0.046f, 0f),
            new Vector2(0.007f, 0.007f),
            0f,
            dark,
            LizardOrder + 2,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Stripe",
            new Vector3(-0.002f, 0.004f, 0f),
            new Vector2(0.008f, 0.040f),
            0f,
            dark,
            LizardOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "HindNear",
            new Vector3(0.012f, -0.018f, 0f),
            new Vector2(0.018f, 0.008f),
            25f,
            dark,
            LizardOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "HindFar",
            new Vector3(-0.010f, -0.016f, 0f),
            new Vector2(0.016f, 0.007f),
            -20f,
            dark,
            LizardOrder - 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "ForeNear",
            new Vector3(0.012f, 0.016f, 0f),
            new Vector2(0.016f, 0.007f),
            18f,
            dark,
            LizardOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "ForeFar",
            new Vector3(-0.008f, 0.014f, 0f),
            new Vector2(0.014f, 0.006f),
            -14f,
            dark,
            LizardOrder - 1,
            HumanVisualShape.Ellipse);

        tail = MakeLizardTail(go.transform);
        return go.transform;
    }

    private static Transform MakeLizardTail(Transform parent)
    {
        GameObject tail = new GameObject("Tail");
        tail.transform.SetParent(parent, false);
        tail.transform.localPosition = new Vector3(0f, -0.046f, 0f);
        tail.transform.localRotation = Quaternion.Euler(0f, 0f, -22f);
        SpriteRenderer sr = tail.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            sr,
            new Vector2(0.010f, 0.044f),
            HumanVisualShape.Ellipse,
            null,
            new Color(0.34f, 0.40f, 0.18f, 1f),
            LizardOrder);
        return tail.transform;
    }

    private Transform MakeGrasshopper(Vector3 pos, float face, out Transform hind, out Transform ant)
    {
        Color skin = new Color(0.46f, 0.58f, 0.22f, 1f);
        Color dark = new Color(0.22f, 0.30f, 0.10f, 1f);
        Color wing = new Color(0.38f, 0.48f, 0.18f, 1f);
        float sign = face >= 0f ? 1f : -1f;

        GameObject go = new GameObject("YardHopper");
        go.transform.SetParent(transform, false);
        go.transform.position = pos;
        go.transform.localScale = new Vector3(sign, 1f, 1f);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            sr,
            new Vector2(0.062f, 0.018f),
            HumanVisualShape.Ellipse,
            null,
            skin,
            HopperOrder);

        AddHousePart(
            go.transform,
            "Head",
            new Vector3(0.030f, 0.004f, 0f),
            new Vector2(0.018f, 0.016f),
            8f,
            skin,
            HopperOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Eye",
            new Vector3(0.034f, 0.008f, 0f),
            new Vector2(0.006f, 0.006f),
            0f,
            dark,
            HopperOrder + 2,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Wing",
            new Vector3(-0.004f, 0.006f, 0f),
            new Vector2(0.040f, 0.010f),
            -8f,
            wing,
            HopperOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Fore",
            new Vector3(0.018f, -0.008f, 0f),
            new Vector2(0.016f, 0.006f),
            18f,
            dark,
            HopperOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Mid",
            new Vector3(0.004f, -0.008f, 0f),
            new Vector2(0.014f, 0.005f),
            10f,
            dark,
            HopperOrder,
            HumanVisualShape.Ellipse);

        hind = MakeHopperHind(go.transform);
        ant = MakeHopperAntenna(go.transform);
        return go.transform;
    }

    private static Transform MakeHopperHind(Transform parent)
    {
        Color dark = new Color(0.22f, 0.30f, 0.10f, 1f);
        GameObject hip = new GameObject("Hind");
        hip.transform.SetParent(parent, false);
        hip.transform.localPosition = new Vector3(-0.018f, -0.002f, 0f);
        hip.transform.localRotation = Quaternion.Euler(0f, 0f, -38f);
        SpriteRenderer femur = hip.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            femur,
            new Vector2(0.036f, 0.012f),
            HumanVisualShape.Ellipse,
            null,
            dark,
            HopperOrder);

        GameObject shin = new GameObject("Tibia");
        shin.transform.SetParent(hip.transform, false);
        shin.transform.localPosition = new Vector3(-0.016f, -0.014f, 0f);
        shin.transform.localRotation = Quaternion.Euler(0f, 0f, 55f);
        SpriteRenderer tsr = shin.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            tsr,
            new Vector2(0.038f, 0.006f),
            HumanVisualShape.Box,
            null,
            dark,
            HopperOrder);
        return hip.transform;
    }

    private static Transform MakeHopperAntenna(Transform parent)
    {
        GameObject ant = new GameObject("Ant");
        ant.transform.SetParent(parent, false);
        ant.transform.localPosition = new Vector3(0.036f, 0.012f, 0f);
        ant.transform.localRotation = Quaternion.Euler(0f, 0f, 48f);
        SpriteRenderer sr = ant.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            sr,
            new Vector2(0.005f, 0.024f),
            HumanVisualShape.Box,
            null,
            new Color(0.28f, 0.34f, 0.12f, 1f),
            HopperOrder + 1);
        return ant.transform;
    }

    private Transform MakeAntTrail(Vector3 pos, float face, out Transform feeler)
    {
        float sign = face >= 0f ? 1f : -1f;
        GameObject go = new GameObject("YardAnts");
        go.transform.SetParent(transform, false);
        go.transform.position = pos;
        go.transform.localScale = new Vector3(sign, 1f, 1f);

        MakeAntWorker(go.transform, "Lead", new Vector3(0.070f, 0f, 0f), out feeler);
        MakeAntWorker(go.transform, "Mid", Vector3.zero, out _);
        MakeAntWorker(go.transform, "Tail", new Vector3(-0.070f, 0f, 0f), out _);
        return go.transform;
    }

    private static void MakeAntWorker(
        Transform parent,
        string name,
        Vector3 local,
        out Transform feeler)
    {
        Color body = new Color(0.16f, 0.10f, 0.07f, 1f);
        Color dark = new Color(0.10f, 0.07f, 0.05f, 1f);

        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = local;
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            sr,
            new Vector2(0.018f, 0.009f),
            HumanVisualShape.Ellipse,
            null,
            body,
            AntOrder);

        AddHousePart(
            go.transform,
            "Head",
            new Vector3(0.012f, 0.001f, 0f),
            new Vector2(0.008f, 0.007f),
            8f,
            body,
            AntOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Abdomen",
            new Vector3(-0.012f, 0.000f, 0f),
            new Vector2(0.014f, 0.009f),
            -6f,
            dark,
            AntOrder,
            HumanVisualShape.Ellipse);

        GameObject ant = new GameObject("Feel");
        ant.transform.SetParent(go.transform, false);
        ant.transform.localPosition = new Vector3(0.016f, 0.006f, 0f);
        ant.transform.localRotation = Quaternion.Euler(0f, 0f, 42f);
        SpriteRenderer asr = ant.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            asr,
            new Vector2(0.003f, 0.014f),
            HumanVisualShape.Box,
            null,
            dark,
            AntOrder + 1);
        feeler = ant.transform;
    }

    private Transform MakeCricket(Vector3 pos, float face, out Transform wing, out Transform ant)
    {
        Color skin = new Color(0.34f, 0.24f, 0.12f, 1f);
        Color dark = new Color(0.18f, 0.12f, 0.07f, 1f);
        Color cover = new Color(0.42f, 0.32f, 0.16f, 1f);
        float sign = face >= 0f ? 1f : -1f;

        GameObject go = new GameObject("YardCricket");
        go.transform.SetParent(transform, false);
        go.transform.position = pos;
        go.transform.localScale = new Vector3(sign * 0.52f, 0.52f, 1f);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            sr,
            new Vector2(0.048f, 0.014f),
            HumanVisualShape.Ellipse,
            null,
            skin,
            CricketOrder);

        AddHousePart(
            go.transform,
            "Head",
            new Vector3(0.024f, 0.003f, 0f),
            new Vector2(0.014f, 0.012f),
            10f,
            skin,
            CricketOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Eye",
            new Vector3(0.028f, 0.007f, 0f),
            new Vector2(0.005f, 0.005f),
            0f,
            dark,
            CricketOrder + 2,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Fore",
            new Vector3(0.014f, -0.007f, 0f),
            new Vector2(0.012f, 0.005f),
            16f,
            dark,
            CricketOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Mid",
            new Vector3(0.002f, -0.007f, 0f),
            new Vector2(0.011f, 0.004f),
            8f,
            dark,
            CricketOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Hind",
            new Vector3(-0.014f, -0.006f, 0f),
            new Vector2(0.016f, 0.006f),
            -12f,
            dark,
            CricketOrder,
            HumanVisualShape.Ellipse);

        wing = MakeCricketWing(go.transform, cover);
        ant = MakeCricketAntenna(go.transform, dark);
        return go.transform;
    }

    private static Transform MakeCricketWing(Transform parent, Color tint)
    {
        GameObject wing = new GameObject("Wing");
        wing.transform.SetParent(parent, false);
        wing.transform.localPosition = new Vector3(-0.004f, 0.005f, 0f);
        wing.transform.localRotation = Quaternion.Euler(0f, 0f, -10f);
        SpriteRenderer sr = wing.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            sr,
            new Vector2(0.036f, 0.010f),
            HumanVisualShape.Ellipse,
            null,
            tint,
            CricketOrder + 1);
        return wing.transform;
    }

    private static Transform MakeCricketAntenna(Transform parent, Color tint)
    {
        GameObject ant = new GameObject("Ant");
        ant.transform.SetParent(parent, false);
        ant.transform.localPosition = new Vector3(0.030f, 0.010f, 0f);
        ant.transform.localRotation = Quaternion.Euler(0f, 0f, 62f);
        SpriteRenderer sr = ant.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            sr,
            new Vector2(0.004f, 0.038f),
            HumanVisualShape.Box,
            null,
            tint,
            CricketOrder + 1);
        return ant.transform;
    }

    private Transform MakeWorm(Vector3 pos, float face, out Transform mid, out SpriteRenderer sheen)
    {
        Color skin = new Color(0.62f, 0.38f, 0.32f, 1f);
        Color dark = new Color(0.42f, 0.24f, 0.20f, 1f);
        float sign = face >= 0f ? 1f : -1f;

        GameObject go = new GameObject("YardWorm");
        go.transform.SetParent(transform, false);
        go.transform.position = pos;
        go.transform.localScale = new Vector3(sign * 0.70f, 0.70f, 1f);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            sr,
            new Vector2(0.072f, 0.012f),
            HumanVisualShape.Ellipse,
            null,
            skin,
            WormOrder);

        AddHousePart(
            go.transform,
            "Head",
            new Vector3(0.034f, 0.001f, 0f),
            new Vector2(0.016f, 0.012f),
            8f,
            dark,
            WormOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Tail",
            new Vector3(-0.034f, 0.000f, 0f),
            new Vector2(0.018f, 0.010f),
            -6f,
            dark,
            WormOrder,
            HumanVisualShape.Ellipse);

        GameObject ring = new GameObject("Mid");
        ring.transform.SetParent(go.transform, false);
        ring.transform.localPosition = Vector3.zero;
        SpriteRenderer msr = ring.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            msr,
            new Vector2(0.022f, 0.013f),
            HumanVisualShape.Ellipse,
            null,
            dark,
            WormOrder + 1);
        mid = ring.transform;

        GameObject glow = new GameObject("Sheen");
        glow.transform.SetParent(go.transform, false);
        glow.transform.localPosition = new Vector3(0.006f, 0.003f, 0f);
        SpriteRenderer gsr = glow.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            gsr,
            new Vector2(0.048f, 0.008f),
            HumanVisualShape.Ellipse,
            null,
            new Color(0.90f, 0.68f, 0.52f, 0f),
            WormOrder + 2);
        SpriteLighting.ApplyUnlit(gsr);
        gsr.enabled = false;
        sheen = gsr;
        return go.transform;
    }

    private Transform MakeStrider(Vector3 pos, float face, out Transform stroke, out SpriteRenderer dimple)
    {
        Color skin = new Color(0.16f, 0.14f, 0.12f, 1f);
        Color dark = new Color(0.10f, 0.09f, 0.08f, 1f);
        float sign = face >= 0f ? 1f : -1f;

        GameObject go = new GameObject("YardStrider");
        go.transform.SetParent(transform, false);
        go.transform.position = pos;
        go.transform.localScale = new Vector3(sign, 1f, 1f);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            sr,
            new Vector2(0.028f, 0.008f),
            HumanVisualShape.Ellipse,
            null,
            skin,
            StriderOrder);

        AddHousePart(
            go.transform,
            "Head",
            new Vector3(0.014f, 0.001f, 0f),
            new Vector2(0.010f, 0.008f),
            8f,
            dark,
            StriderOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Fore",
            new Vector3(0.012f, -0.004f, 0f),
            new Vector2(0.022f, 0.004f),
            22f,
            dark,
            StriderOrder,
            HumanVisualShape.Box);
        AddHousePart(
            go.transform,
            "Hind",
            new Vector3(-0.012f, -0.004f, 0f),
            new Vector2(0.020f, 0.004f),
            -18f,
            dark,
            StriderOrder,
            HumanVisualShape.Box);

        GameObject mid = new GameObject("Stroke");
        mid.transform.SetParent(go.transform, false);
        mid.transform.localPosition = new Vector3(0f, -0.003f, 0f);
        SpriteRenderer msr = mid.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            msr,
            new Vector2(0.078f, 0.004f),
            HumanVisualShape.Box,
            null,
            dark,
            StriderOrder);
        stroke = mid.transform;

        GameObject film = new GameObject("Dimple");
        film.transform.SetParent(go.transform, false);
        film.transform.localPosition = new Vector3(0f, -0.008f, 0f);
        SpriteRenderer dsr = film.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            dsr,
            new Vector2(0.042f, 0.010f),
            HumanVisualShape.Ellipse,
            null,
            new Color(0.78f, 0.90f, 0.96f, 0f),
            PuddleOrder + 1);
        SpriteLighting.ApplyUnlit(dsr);
        dsr.enabled = false;
        dimple = dsr;
        return go.transform;
    }

    private Transform MakeBat(
        Vector3 pos,
        float face,
        out Transform wingL,
        out Transform wingR,
        out SpriteRenderer eye)
    {
        Color fur = new Color(0.14f, 0.11f, 0.10f, 1f);
        Color dark = new Color(0.08f, 0.07f, 0.06f, 1f);
        Color membrane = new Color(0.22f, 0.12f, 0.10f, 0.88f);
        Color far = new Color(0.16f, 0.10f, 0.08f, 0.62f);
        float sign = face >= 0f ? 1f : -1f;

        GameObject go = new GameObject("YardBat");
        go.transform.SetParent(transform, false);
        go.transform.position = pos;
        go.transform.localScale = new Vector3(sign, 1f, 1f);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            sr,
            new Vector2(0.070f, 0.028f),
            HumanVisualShape.Ellipse,
            null,
            fur,
            BatOrder);

        AddHousePart(
            go.transform,
            "Head",
            new Vector3(0.032f, 0.006f, 0f),
            new Vector2(0.028f, 0.022f),
            10f,
            fur,
            BatOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "EarNear",
            new Vector3(0.028f, 0.022f, 0f),
            new Vector2(0.010f, 0.018f),
            18f,
            dark,
            BatOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "EarFar",
            new Vector3(0.020f, 0.020f, 0f),
            new Vector2(0.008f, 0.016f),
            28f,
            dark,
            BatOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Belly",
            new Vector3(-0.004f, -0.002f, 0f),
            new Vector2(0.036f, 0.014f),
            0f,
            dark,
            BatOrder,
            HumanVisualShape.Ellipse);

        GameObject eyeGo = new GameObject("Eye");
        eyeGo.transform.SetParent(go.transform, false);
        eyeGo.transform.localPosition = new Vector3(0.038f, 0.010f, 0f);
        SpriteRenderer esr = eyeGo.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            esr,
            new Vector2(0.008f, 0.006f),
            HumanVisualShape.Ellipse,
            null,
            new Color(0.92f, 0.72f, 0.22f, 0f),
            BatOrder + 2);
        SpriteLighting.ApplyUnlit(esr);
        esr.enabled = false;
        eye = esr;

        wingL = MakeBatWing(go.transform, "WingNear", new Vector3(0.004f, 0.010f, 0f), 12f, membrane, BatOrder + 1);
        wingR = MakeBatWing(go.transform, "WingFar", new Vector3(-0.002f, 0.004f, 0f), -12f, far, BatOrder - 1);
        return go.transform;
    }

    private static Transform MakeBatWing(
        Transform parent,
        string name,
        Vector3 local,
        float tilt,
        Color tint,
        int order)
    {
        GameObject wing = new GameObject(name);
        wing.transform.SetParent(parent, false);
        wing.transform.localPosition = local;
        wing.transform.localRotation = Quaternion.Euler(0f, 0f, tilt);
        SpriteRenderer sr = wing.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            sr,
            new Vector2(0.110f, 0.048f),
            HumanVisualShape.Ellipse,
            null,
            tint,
            order);
        return wing.transform;
    }

    private Transform MakeWeb(LevelTest1.WebPeg peg, out Transform legs, out SpriteRenderer dew)
    {
        Color silk = new Color(0.78f, 0.80f, 0.76f, 0.42f);
        Color cord = new Color(0.62f, 0.64f, 0.60f, 0.58f);

        GameObject go = new GameObject("YardWeb");
        go.transform.SetParent(transform, false);
        go.transform.position = new Vector3(peg.x, peg.y, 0f);

        float w = peg.width;
        float h = peg.height;
        float diag = Mathf.Sqrt(w * w + h * h);
        float tilt = Mathf.Atan2(h, w) * Mathf.Rad2Deg;

        AddSilk(go.transform, "Warp", Vector3.zero, new Vector2(w, 0.007f), 0f, silk, WebOrder);
        AddSilk(go.transform, "Weft", Vector3.zero, new Vector2(0.007f, h), 0f, silk, WebOrder);
        AddSilk(go.transform, "DiagA", Vector3.zero, new Vector2(diag * 0.92f, 0.006f), tilt, silk, WebOrder);
        AddSilk(go.transform, "DiagB", Vector3.zero, new Vector2(diag * 0.92f, 0.006f), -tilt, silk, WebOrder);
        AddSilk(
            go.transform,
            "RingN",
            new Vector3(0f, h * 0.22f, 0f),
            new Vector2(w * 0.58f, 0.006f),
            0f,
            cord,
            WebOrder);
        AddSilk(
            go.transform,
            "RingS",
            new Vector3(0f, -h * 0.22f, 0f),
            new Vector2(w * 0.58f, 0.006f),
            0f,
            cord,
            WebOrder);
        AddSilk(
            go.transform,
            "RingE",
            new Vector3(w * 0.22f, 0f, 0f),
            new Vector2(0.006f, h * 0.58f),
            0f,
            cord,
            WebOrder);
        AddSilk(
            go.transform,
            "RingW",
            new Vector3(-w * 0.22f, 0f, 0f),
            new Vector2(0.006f, h * 0.58f),
            0f,
            cord,
            WebOrder);

        GameObject dewGo = new GameObject("Dew");
        dewGo.transform.SetParent(go.transform, false);
        dewGo.transform.localPosition = new Vector3(w * 0.16f, h * 0.12f, 0f);
        SpriteRenderer dsr = dewGo.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            dsr,
            new Vector2(0.016f, 0.012f),
            HumanVisualShape.Ellipse,
            null,
            new Color(0.86f, 0.92f, 0.98f, 0f),
            WebOrder + 1);
        SpriteLighting.ApplyUnlit(dsr);
        dsr.enabled = false;
        dew = dsr;

        return MakeSpider(
            new Vector3(peg.x + 0.04f * peg.face, peg.y, 0f),
            peg.face,
            out legs);
    }

    private Transform MakeSpider(Vector3 pos, float face, out Transform legs)
    {
        Color body = new Color(0.18f, 0.12f, 0.10f, 1f);
        Color dark = new Color(0.10f, 0.08f, 0.07f, 1f);
        float sign = face >= 0f ? 1f : -1f;

        GameObject go = new GameObject("YardSpider");
        go.transform.SetParent(transform, false);
        go.transform.position = pos;
        go.transform.localScale = new Vector3(sign, 1f, 1f);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            sr,
            new Vector2(0.018f, 0.014f),
            HumanVisualShape.Ellipse,
            null,
            body,
            SpiderOrder);

        AddHousePart(
            go.transform,
            "Abdomen",
            new Vector3(-0.014f, 0f, 0f),
            new Vector2(0.022f, 0.016f),
            0f,
            dark,
            SpiderOrder,
            HumanVisualShape.Ellipse);

        GameObject stroke = new GameObject("Legs");
        stroke.transform.SetParent(go.transform, false);
        stroke.transform.localPosition = Vector3.zero;
        AddHousePart(
            stroke.transform,
            "ForeNear",
            new Vector3(0.010f, 0.010f, 0f),
            new Vector2(0.028f, 0.005f),
            28f,
            body,
            SpiderOrder - 1,
            HumanVisualShape.Box);
        AddHousePart(
            stroke.transform,
            "HindNear",
            new Vector3(-0.008f, 0.010f, 0f),
            new Vector2(0.026f, 0.005f),
            -32f,
            body,
            SpiderOrder - 1,
            HumanVisualShape.Box);
        AddHousePart(
            stroke.transform,
            "ForeFar",
            new Vector3(0.008f, -0.008f, 0f),
            new Vector2(0.026f, 0.004f),
            -22f,
            dark,
            SpiderOrder - 1,
            HumanVisualShape.Box);
        AddHousePart(
            stroke.transform,
            "HindFar",
            new Vector3(-0.010f, -0.008f, 0f),
            new Vector2(0.024f, 0.004f),
            26f,
            dark,
            SpiderOrder - 1,
            HumanVisualShape.Box);
        legs = stroke.transform;
        return go.transform;
    }

    private Transform MakeMouse(
        Vector3 pos,
        float face,
        out Transform tail,
        out SpriteRenderer eye)
    {
        Color fur = new Color(0.42f, 0.32f, 0.24f, 1f);
        Color dark = new Color(0.22f, 0.16f, 0.12f, 1f);
        float sign = face >= 0f ? 1f : -1f;

        GameObject go = new GameObject("YardMouse");
        go.transform.SetParent(transform, false);
        go.transform.position = pos;
        go.transform.localScale = new Vector3(sign, 1f, 1f);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            sr,
            new Vector2(0.072f, 0.028f),
            HumanVisualShape.Ellipse,
            null,
            fur,
            MouseOrder);

        AddHousePart(
            go.transform,
            "Head",
            new Vector3(0.036f, 0.008f, 0f),
            new Vector2(0.032f, 0.026f),
            8f,
            fur,
            MouseOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Ear",
            new Vector3(0.028f, 0.024f, 0f),
            new Vector2(0.014f, 0.016f),
            18f,
            dark,
            MouseOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Nose",
            new Vector3(0.050f, 0.004f, 0f),
            new Vector2(0.010f, 0.008f),
            0f,
            dark,
            MouseOrder + 2,
            HumanVisualShape.Ellipse);

        GameObject eyeGo = new GameObject("Eye");
        eyeGo.transform.SetParent(go.transform, false);
        eyeGo.transform.localPosition = new Vector3(0.040f, 0.012f, 0f);
        SpriteRenderer esr = eyeGo.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            esr,
            new Vector2(0.008f, 0.006f),
            HumanVisualShape.Ellipse,
            null,
            new Color(0.92f, 0.78f, 0.28f, 0f),
            MouseOrder + 2);
        SpriteLighting.ApplyUnlit(esr);
        esr.enabled = false;
        eye = esr;

        GameObject tailGo = new GameObject("Tail");
        tailGo.transform.SetParent(go.transform, false);
        tailGo.transform.localPosition = new Vector3(-0.038f, 0.002f, 0f);
        tailGo.transform.localRotation = Quaternion.Euler(0f, 0f, 22f);
        GameObject tailVis = new GameObject("Visual");
        tailVis.transform.SetParent(tailGo.transform, false);
        tailVis.transform.localPosition = new Vector3(-0.028f, 0f, 0f);
        SpriteRenderer tsr = tailVis.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            tsr,
            new Vector2(0.056f, 0.008f),
            HumanVisualShape.Ellipse,
            null,
            dark,
            MouseOrder);
        tail = tailGo.transform;
        return go.transform;
    }

    private Transform MakeRabbit(
        Vector3 pos,
        float face,
        out Transform head,
        out Transform ear)
    {
        Color fur = new Color(0.62f, 0.52f, 0.40f, 1f);
        Color dark = new Color(0.38f, 0.28f, 0.20f, 1f);
        Color puff = new Color(0.86f, 0.82f, 0.74f, 1f);
        float sign = face >= 0f ? 1f : -1f;

        GameObject go = new GameObject("YardRabbit");
        go.transform.SetParent(transform, false);
        go.transform.position = pos;
        go.transform.localScale = new Vector3(sign, 1f, 1f);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            sr,
            new Vector2(0.110f, 0.048f),
            HumanVisualShape.Ellipse,
            null,
            fur,
            RabbitOrder);

        AddHousePart(
            go.transform,
            "Haunch",
            new Vector3(-0.032f, -0.004f, 0f),
            new Vector2(0.048f, 0.044f),
            12f,
            dark,
            RabbitOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Tail",
            new Vector3(-0.058f, 0.010f, 0f),
            new Vector2(0.022f, 0.020f),
            0f,
            puff,
            RabbitOrder + 1,
            HumanVisualShape.Ellipse);

        GameObject headGo = new GameObject("Head");
        headGo.transform.SetParent(go.transform, false);
        headGo.transform.localPosition = new Vector3(0.048f, 0.012f, 0f);
        SpriteRenderer hsr = headGo.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            hsr,
            new Vector2(0.042f, 0.034f),
            HumanVisualShape.Ellipse,
            null,
            fur,
            RabbitOrder + 2);
        AddHousePart(
            headGo.transform,
            "Cheek",
            new Vector3(0.016f, -0.006f, 0f),
            new Vector2(0.022f, 0.018f),
            0f,
            dark,
            RabbitOrder + 2,
            HumanVisualShape.Ellipse);
        AddHousePart(
            headGo.transform,
            "Eye",
            new Vector3(0.010f, 0.008f, 0f),
            new Vector2(0.008f, 0.007f),
            0f,
            new Color(0.12f, 0.10f, 0.08f, 1f),
            RabbitOrder + 3,
            HumanVisualShape.Ellipse);

        GameObject earGo = new GameObject("Ear");
        earGo.transform.SetParent(headGo.transform, false);
        earGo.transform.localPosition = new Vector3(-0.004f, 0.028f, 0f);
        earGo.transform.localRotation = Quaternion.Euler(0f, 0f, 12f);
        GameObject earVis = new GameObject("Visual");
        earVis.transform.SetParent(earGo.transform, false);
        earVis.transform.localPosition = new Vector3(0f, 0.028f, 0f);
        SpriteRenderer esr = earVis.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            esr,
            new Vector2(0.016f, 0.056f),
            HumanVisualShape.Ellipse,
            null,
            dark,
            RabbitOrder + 2);
        AddHousePart(
            earGo.transform,
            "EarBack",
            new Vector3(-0.010f, 0.022f, 0f),
            new Vector2(0.014f, 0.048f),
            -8f,
            fur,
            RabbitOrder + 1,
            HumanVisualShape.Ellipse);

        head = headGo.transform;
        ear = earGo.transform;
        return go.transform;
    }

    private Transform MakeSquirrel(Vector3 pos, float face, out Transform tail)
    {
        Color fur = new Color(0.58f, 0.34f, 0.16f, 1f);
        Color dark = new Color(0.38f, 0.20f, 0.10f, 1f);
        Color cream = new Color(0.82f, 0.70f, 0.52f, 1f);
        float sign = face >= 0f ? 1f : -1f;

        GameObject go = new GameObject("YardSquirrel");
        go.transform.SetParent(transform, false);
        go.transform.position = pos;
        go.transform.localScale = new Vector3(sign, 1f, 1f);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            sr,
            new Vector2(0.078f, 0.034f),
            HumanVisualShape.Ellipse,
            null,
            fur,
            SquirrelOrder);

        AddHousePart(
            go.transform,
            "Belly",
            new Vector3(0.006f, -0.006f, 0f),
            new Vector2(0.048f, 0.018f),
            0f,
            cream,
            SquirrelOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Head",
            new Vector3(0.038f, 0.012f, 0f),
            new Vector2(0.034f, 0.028f),
            10f,
            fur,
            SquirrelOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Ear",
            new Vector3(0.030f, 0.028f, 0f),
            new Vector2(0.012f, 0.016f),
            16f,
            dark,
            SquirrelOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Eye",
            new Vector3(0.044f, 0.016f, 0f),
            new Vector2(0.007f, 0.006f),
            0f,
            new Color(0.12f, 0.08f, 0.06f, 1f),
            SquirrelOrder + 2,
            HumanVisualShape.Ellipse);

        GameObject tailGo = new GameObject("Tail");
        tailGo.transform.SetParent(go.transform, false);
        tailGo.transform.localPosition = new Vector3(-0.034f, 0.006f, 0f);
        tailGo.transform.localRotation = Quaternion.Euler(0f, 0f, 48f);
        GameObject tailVis = new GameObject("Visual");
        tailVis.transform.SetParent(tailGo.transform, false);
        tailVis.transform.localPosition = new Vector3(-0.034f, 0.012f, 0f);
        SpriteRenderer tsr = tailVis.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            tsr,
            new Vector2(0.078f, 0.040f),
            HumanVisualShape.Ellipse,
            null,
            dark,
            SquirrelOrder + 1);
        AddHousePart(
            tailGo.transform,
            "TailTip",
            new Vector3(-0.058f, 0.028f, 0f),
            new Vector2(0.042f, 0.030f),
            18f,
            fur,
            SquirrelOrder + 1,
            HumanVisualShape.Ellipse);

        tail = tailGo.transform;
        return go.transform;
    }

    private Transform MakeOwl(Vector3 pos, float face, out Transform head, out SpriteRenderer eye)
    {
        Color plumage = new Color(0.40f, 0.30f, 0.20f, 1f);
        Color dark = new Color(0.26f, 0.18f, 0.12f, 1f);
        Color cream = new Color(0.74f, 0.64f, 0.48f, 1f);
        float sign = face >= 0f ? 1f : -1f;

        GameObject go = new GameObject("YardOwl");
        go.transform.SetParent(transform, false);
        go.transform.position = pos;
        go.transform.localScale = new Vector3(sign, 1f, 1f);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            sr,
            new Vector2(0.072f, 0.078f),
            HumanVisualShape.Ellipse,
            null,
            plumage,
            OwlOrder);

        AddHousePart(
            go.transform,
            "Belly",
            new Vector3(0.006f, -0.008f, 0f),
            new Vector2(0.044f, 0.048f),
            0f,
            cream,
            OwlOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Tail",
            new Vector3(-0.034f, -0.018f, 0f),
            new Vector2(0.028f, 0.022f),
            18f,
            dark,
            OwlOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Foot",
            new Vector3(0.008f, -0.040f, 0f),
            new Vector2(0.028f, 0.010f),
            0f,
            dark,
            OwlOrder + 1,
            HumanVisualShape.Box);

        GameObject headGo = new GameObject("Head");
        headGo.transform.SetParent(go.transform, false);
        headGo.transform.localPosition = new Vector3(0.006f, 0.040f, 0f);
        SpriteRenderer hsr = headGo.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            hsr,
            new Vector2(0.052f, 0.046f),
            HumanVisualShape.Ellipse,
            null,
            plumage,
            OwlOrder + 1);
        AddHousePart(
            headGo.transform,
            "EarFore",
            new Vector3(0.010f, 0.030f, 0f),
            new Vector2(0.014f, 0.022f),
            18f,
            dark,
            OwlOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            headGo.transform,
            "EarBack",
            new Vector3(-0.012f, 0.028f, 0f),
            new Vector2(0.012f, 0.020f),
            -16f,
            dark,
            OwlOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            headGo.transform,
            "Beak",
            new Vector3(0.026f, -0.002f, 0f),
            new Vector2(0.014f, 0.010f),
            -18f,
            new Color(0.62f, 0.42f, 0.16f, 1f),
            OwlOrder + 2,
            HumanVisualShape.Ellipse);

        GameObject eyeGo = new GameObject("Eye");
        eyeGo.transform.SetParent(headGo.transform, false);
        eyeGo.transform.localPosition = new Vector3(0.012f, 0.006f, 0f);
        SpriteRenderer esr = eyeGo.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            esr,
            new Vector2(0.016f, 0.016f),
            HumanVisualShape.Ellipse,
            null,
            new Color(0.92f, 0.78f, 0.28f, 0f),
            OwlOrder + 2);
        SpriteLighting.ApplyUnlit(esr);
        esr.enabled = false;
        AddHousePart(
            eyeGo.transform,
            "Pupil",
            new Vector3(0.002f, 0f, 0f),
            new Vector2(0.007f, 0.007f),
            0f,
            new Color(0.10f, 0.08f, 0.06f, 1f),
            OwlOrder + 3,
            HumanVisualShape.Ellipse);

        head = headGo.transform;
        eye = esr;
        return go.transform;
    }

    private Transform MakeRaccoon(
        Vector3 pos,
        float face,
        out Transform head,
        out Transform tail,
        out SpriteRenderer eye)
    {
        Color fur = new Color(0.42f, 0.36f, 0.30f, 1f);
        Color dark = new Color(0.16f, 0.12f, 0.10f, 1f);
        Color cream = new Color(0.78f, 0.72f, 0.62f, 1f);
        Color ring = new Color(0.70f, 0.64f, 0.52f, 1f);
        float sign = face >= 0f ? 1f : -1f;

        GameObject go = new GameObject("YardRaccoon");
        go.transform.SetParent(transform, false);
        go.transform.position = pos;
        go.transform.localScale = new Vector3(sign, 1f, 1f);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            sr,
            new Vector2(0.132f, 0.052f),
            HumanVisualShape.Ellipse,
            null,
            fur,
            RaccoonOrder);

        AddHousePart(
            go.transform,
            "Haunch",
            new Vector3(-0.038f, -0.004f, 0f),
            new Vector2(0.052f, 0.046f),
            10f,
            dark,
            RaccoonOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Belly",
            new Vector3(0.010f, -0.008f, 0f),
            new Vector2(0.070f, 0.024f),
            0f,
            cream,
            RaccoonOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "FootFore",
            new Vector3(0.036f, -0.028f, 0f),
            new Vector2(0.028f, 0.010f),
            0f,
            dark,
            RaccoonOrder + 1,
            HumanVisualShape.Box);
        AddHousePart(
            go.transform,
            "FootHind",
            new Vector3(-0.034f, -0.028f, 0f),
            new Vector2(0.030f, 0.010f),
            0f,
            dark,
            RaccoonOrder + 1,
            HumanVisualShape.Box);

        GameObject headGo = new GameObject("Head");
        headGo.transform.SetParent(go.transform, false);
        headGo.transform.localPosition = new Vector3(0.058f, 0.012f, 0f);
        SpriteRenderer hsr = headGo.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            hsr,
            new Vector2(0.048f, 0.038f),
            HumanVisualShape.Ellipse,
            null,
            fur,
            RaccoonOrder + 1);
        AddHousePart(
            headGo.transform,
            "Muzzle",
            new Vector3(0.022f, -0.006f, 0f),
            new Vector2(0.028f, 0.018f),
            0f,
            cream,
            RaccoonOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            headGo.transform,
            "Mask",
            new Vector3(0.008f, 0.004f, 0f),
            new Vector2(0.036f, 0.016f),
            8f,
            dark,
            RaccoonOrder + 2,
            HumanVisualShape.Ellipse);
        AddHousePart(
            headGo.transform,
            "Ear",
            new Vector3(-0.008f, 0.024f, 0f),
            new Vector2(0.014f, 0.016f),
            16f,
            dark,
            RaccoonOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            headGo.transform,
            "Nose",
            new Vector3(0.034f, -0.004f, 0f),
            new Vector2(0.010f, 0.008f),
            0f,
            dark,
            RaccoonOrder + 2,
            HumanVisualShape.Ellipse);

        GameObject eyeGo = new GameObject("Eye");
        eyeGo.transform.SetParent(headGo.transform, false);
        eyeGo.transform.localPosition = new Vector3(0.010f, 0.006f, 0f);
        SpriteRenderer esr = eyeGo.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            esr,
            new Vector2(0.010f, 0.008f),
            HumanVisualShape.Ellipse,
            null,
            new Color(0.92f, 0.78f, 0.28f, 0f),
            RaccoonOrder + 3);
        SpriteLighting.ApplyUnlit(esr);
        esr.enabled = false;

        GameObject tailGo = new GameObject("Tail");
        tailGo.transform.SetParent(go.transform, false);
        tailGo.transform.localPosition = new Vector3(-0.062f, 0.008f, 0f);
        tailGo.transform.localRotation = Quaternion.Euler(0f, 0f, 28f);
        AddHousePart(
            tailGo.transform,
            "TailBase",
            new Vector3(-0.018f, 0f, 0f),
            new Vector2(0.040f, 0.018f),
            0f,
            fur,
            RaccoonOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            tailGo.transform,
            "RingA",
            new Vector3(-0.040f, 0.002f, 0f),
            new Vector2(0.028f, 0.016f),
            0f,
            dark,
            RaccoonOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            tailGo.transform,
            "RingB",
            new Vector3(-0.058f, 0.004f, 0f),
            new Vector2(0.024f, 0.014f),
            0f,
            ring,
            RaccoonOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            tailGo.transform,
            "RingC",
            new Vector3(-0.074f, 0.006f, 0f),
            new Vector2(0.022f, 0.012f),
            0f,
            dark,
            RaccoonOrder,
            HumanVisualShape.Ellipse);

        head = headGo.transform;
        tail = tailGo.transform;
        eye = esr;
        return go.transform;
    }

    private Transform MakeTurtle(Vector3 pos, float face, out Transform head)
    {
        Color shell = new Color(0.34f, 0.42f, 0.22f, 1f);
        Color scute = new Color(0.22f, 0.28f, 0.14f, 1f);
        Color skin = new Color(0.42f, 0.40f, 0.22f, 1f);
        Color dark = new Color(0.18f, 0.16f, 0.10f, 1f);
        float sign = face >= 0f ? 1f : -1f;

        GameObject go = new GameObject("YardTurtle");
        go.transform.SetParent(transform, false);
        go.transform.position = pos;
        go.transform.localScale = new Vector3(sign, 1f, 1f);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            sr,
            new Vector2(0.128f, 0.048f),
            HumanVisualShape.Ellipse,
            null,
            shell,
            TurtleOrder);

        AddHousePart(
            go.transform,
            "Dome",
            new Vector3(-0.004f, 0.010f, 0f),
            new Vector2(0.108f, 0.036f),
            0f,
            scute,
            TurtleOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "ScuteA",
            new Vector3(0.018f, 0.012f, 0f),
            new Vector2(0.028f, 0.018f),
            8f,
            dark,
            TurtleOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "ScuteB",
            new Vector3(-0.022f, 0.010f, 0f),
            new Vector2(0.030f, 0.016f),
            -6f,
            dark,
            TurtleOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Plastron",
            new Vector3(0.002f, -0.014f, 0f),
            new Vector2(0.100f, 0.016f),
            0f,
            skin,
            TurtleOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "FootFore",
            new Vector3(0.038f, -0.026f, 0f),
            new Vector2(0.024f, 0.010f),
            8f,
            dark,
            TurtleOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "FootHind",
            new Vector3(-0.036f, -0.026f, 0f),
            new Vector2(0.026f, 0.010f),
            -8f,
            dark,
            TurtleOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Tail",
            new Vector3(-0.064f, -0.006f, 0f),
            new Vector2(0.022f, 0.010f),
            -18f,
            dark,
            TurtleOrder,
            HumanVisualShape.Ellipse);

        GameObject headGo = new GameObject("Head");
        headGo.transform.SetParent(go.transform, false);
        headGo.transform.localPosition = new Vector3(0.068f, 0.004f, 0f);
        SpriteRenderer hsr = headGo.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            hsr,
            new Vector2(0.036f, 0.024f),
            HumanVisualShape.Ellipse,
            null,
            skin,
            TurtleOrder + 1);
        AddHousePart(
            headGo.transform,
            "Snout",
            new Vector3(0.016f, -0.002f, 0f),
            new Vector2(0.018f, 0.014f),
            0f,
            skin,
            TurtleOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            headGo.transform,
            "Eye",
            new Vector3(0.008f, 0.006f, 0f),
            new Vector2(0.008f, 0.008f),
            0f,
            dark,
            TurtleOrder + 2,
            HumanVisualShape.Ellipse);

        head = headGo.transform;
        return go.transform;
    }

    private Transform MakeDuck(Vector3 pos, float face, out Transform head)
    {
        Color body = new Color(0.46f, 0.28f, 0.16f, 1f);
        Color breast = new Color(0.78f, 0.62f, 0.42f, 1f);
        Color wing = new Color(0.28f, 0.22f, 0.16f, 1f);
        Color spec = new Color(0.22f, 0.40f, 0.62f, 1f);
        Color green = new Color(0.16f, 0.40f, 0.28f, 1f);
        Color collar = new Color(0.90f, 0.88f, 0.80f, 1f);
        Color bill = new Color(0.86f, 0.62f, 0.18f, 1f);
        Color dark = new Color(0.14f, 0.12f, 0.08f, 1f);
        float sign = face >= 0f ? 1f : -1f;

        GameObject go = new GameObject("YardDuck");
        go.transform.SetParent(transform, false);
        go.transform.position = pos;
        go.transform.localScale = new Vector3(sign, 1f, 1f);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            sr,
            new Vector2(0.176f, 0.062f),
            HumanVisualShape.Ellipse,
            null,
            body,
            DuckOrder);

        AddHousePart(
            go.transform,
            "Breast",
            new Vector3(0.042f, -0.004f, 0f),
            new Vector2(0.072f, 0.032f),
            0f,
            breast,
            DuckOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Wing",
            new Vector3(-0.012f, 0.008f, 0f),
            new Vector2(0.088f, 0.032f),
            -8f,
            wing,
            DuckOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Speculum",
            new Vector3(0.018f, 0.006f, 0f),
            new Vector2(0.024f, 0.012f),
            -6f,
            spec,
            DuckOrder + 2,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Tail",
            new Vector3(-0.086f, 0.010f, 0f),
            new Vector2(0.040f, 0.016f),
            18f,
            dark,
            DuckOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "FootFore",
            new Vector3(0.028f, -0.030f, 0f),
            new Vector2(0.026f, 0.010f),
            8f,
            bill,
            DuckOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "FootHind",
            new Vector3(-0.030f, -0.030f, 0f),
            new Vector2(0.028f, 0.010f),
            -8f,
            bill,
            DuckOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Collar",
            new Vector3(0.072f, 0.010f, 0f),
            new Vector2(0.018f, 0.020f),
            0f,
            collar,
            DuckOrder + 1,
            HumanVisualShape.Ellipse);

        GameObject headGo = new GameObject("Head");
        headGo.transform.SetParent(go.transform, false);
        headGo.transform.localPosition = new Vector3(0.082f, 0.016f, 0f);
        SpriteRenderer hsr = headGo.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            hsr,
            new Vector2(0.048f, 0.036f),
            HumanVisualShape.Ellipse,
            null,
            green,
            DuckOrder + 1);
        AddHousePart(
            headGo.transform,
            "Bill",
            new Vector3(0.028f, -0.004f, 0f),
            new Vector2(0.032f, 0.012f),
            -8f,
            bill,
            DuckOrder + 2,
            HumanVisualShape.Ellipse);
        AddHousePart(
            headGo.transform,
            "Eye",
            new Vector3(0.006f, 0.008f, 0f),
            new Vector2(0.008f, 0.008f),
            0f,
            dark,
            DuckOrder + 2,
            HumanVisualShape.Ellipse);

        head = headGo.transform;
        return go.transform;
    }

    private Transform MakeHeron(Vector3 pos, float face, out Transform neck)
    {
        Color body = new Color(0.48f, 0.52f, 0.54f, 1f);
        Color belly = new Color(0.80f, 0.80f, 0.76f, 1f);
        Color wing = new Color(0.32f, 0.36f, 0.40f, 1f);
        Color nape = new Color(0.70f, 0.72f, 0.70f, 1f);
        Color ochre = new Color(0.78f, 0.52f, 0.18f, 1f);
        Color bill = new Color(0.86f, 0.62f, 0.18f, 1f);
        Color dark = new Color(0.16f, 0.16f, 0.18f, 1f);
        Color eye = new Color(0.90f, 0.78f, 0.20f, 1f);
        float sign = face >= 0f ? 1f : -1f;

        GameObject go = new GameObject("YardHeron");
        go.transform.SetParent(transform, false);
        go.transform.position = pos;
        go.transform.localScale = new Vector3(sign, 1f, 1f);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            sr,
            new Vector2(0.112f, 0.052f),
            HumanVisualShape.Ellipse,
            null,
            body,
            HeronOrder);

        AddHousePart(
            go.transform,
            "Belly",
            new Vector3(0.018f, -0.006f, 0f),
            new Vector2(0.062f, 0.028f),
            0f,
            belly,
            HeronOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Wing",
            new Vector3(-0.016f, 0.006f, 0f),
            new Vector2(0.072f, 0.030f),
            -10f,
            wing,
            HeronOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Tail",
            new Vector3(-0.062f, 0.004f, 0f),
            new Vector2(0.036f, 0.016f),
            16f,
            dark,
            HeronOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Hip",
            new Vector3(-0.004f, -0.022f, 0f),
            new Vector2(0.028f, 0.018f),
            0f,
            body,
            HeronOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "LegFore",
            new Vector3(0.016f, -0.068f, 0f),
            new Vector2(0.010f, 0.088f),
            6f,
            ochre,
            HeronOrder - 1,
            HumanVisualShape.Box);
        AddHousePart(
            go.transform,
            "LegHind",
            new Vector3(-0.018f, -0.066f, 0f),
            new Vector2(0.010f, 0.084f),
            -8f,
            ochre,
            HeronOrder - 1,
            HumanVisualShape.Box);
        AddHousePart(
            go.transform,
            "FootFore",
            new Vector3(0.028f, -0.108f, 0f),
            new Vector2(0.032f, 0.008f),
            8f,
            ochre,
            HeronOrder - 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "FootHind",
            new Vector3(-0.010f, -0.106f, 0f),
            new Vector2(0.030f, 0.008f),
            -6f,
            ochre,
            HeronOrder - 1,
            HumanVisualShape.Ellipse);

        GameObject neckGo = new GameObject("Neck");
        neckGo.transform.SetParent(go.transform, false);
        neckGo.transform.localPosition = new Vector3(0.046f, 0.018f, 0f);
        neckGo.transform.localRotation = Quaternion.Euler(0f, 0f, 22f);
        AddHousePart(
            neckGo.transform,
            "Shaft",
            new Vector3(0f, 0.046f, 0f),
            new Vector2(0.016f, 0.092f),
            0f,
            nape,
            HeronOrder + 1,
            HumanVisualShape.Box);

        GameObject headGo = new GameObject("Head");
        headGo.transform.SetParent(neckGo.transform, false);
        headGo.transform.localPosition = new Vector3(0.008f, 0.094f, 0f);
        SpriteRenderer hsr = headGo.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            hsr,
            new Vector2(0.036f, 0.022f),
            HumanVisualShape.Ellipse,
            null,
            nape,
            HeronOrder + 2);
        AddHousePart(
            headGo.transform,
            "Bill",
            new Vector3(0.034f, -0.002f, 0f),
            new Vector2(0.052f, 0.008f),
            -4f,
            bill,
            HeronOrder + 3,
            HumanVisualShape.Box);
        AddHousePart(
            headGo.transform,
            "Eye",
            new Vector3(0.006f, 0.006f, 0f),
            new Vector2(0.008f, 0.008f),
            0f,
            eye,
            HeronOrder + 3,
            HumanVisualShape.Ellipse);
        AddHousePart(
            headGo.transform,
            "Pupil",
            new Vector3(0.007f, 0.006f, 0f),
            new Vector2(0.004f, 0.004f),
            0f,
            dark,
            HeronOrder + 4,
            HumanVisualShape.Ellipse);
        AddHousePart(
            headGo.transform,
            "Crest",
            new Vector3(-0.022f, 0.006f, 0f),
            new Vector2(0.028f, 0.008f),
            18f,
            dark,
            HeronOrder + 2,
            HumanVisualShape.Ellipse);

        neck = neckGo.transform;
        return go.transform;
    }

    private Transform MakeMinnow(Vector3 pos, float face, out Transform tail, out SpriteRenderer flash)
    {
        Color body = new Color(0.42f, 0.50f, 0.44f, 1f);
        Color dorsal = new Color(0.28f, 0.34f, 0.30f, 1f);
        Color belly = new Color(0.62f, 0.68f, 0.58f, 1f);
        Color fin = new Color(0.30f, 0.36f, 0.32f, 1f);
        Color dark = new Color(0.10f, 0.10f, 0.10f, 1f);
        float sign = face >= 0f ? 1f : -1f;

        GameObject go = new GameObject("YardMinnow");
        go.transform.SetParent(transform, false);
        go.transform.position = pos;
        go.transform.localScale = new Vector3(sign, 1f, 1f);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            sr,
            new Vector2(0.052f, 0.018f),
            HumanVisualShape.Ellipse,
            null,
            body,
            MinnowOrder);

        AddHousePart(
            go.transform,
            "Dorsal",
            new Vector3(-0.004f, 0.006f, 0f),
            new Vector2(0.040f, 0.008f),
            8f,
            dorsal,
            MinnowOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Belly",
            new Vector3(0.004f, -0.004f, 0f),
            new Vector2(0.028f, 0.008f),
            -6f,
            belly,
            MinnowOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Head",
            new Vector3(0.022f, 0.001f, 0f),
            new Vector2(0.016f, 0.014f),
            0f,
            body,
            MinnowOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Eye",
            new Vector3(0.026f, 0.004f, 0f),
            new Vector2(0.005f, 0.005f),
            0f,
            dark,
            MinnowOrder + 2,
            HumanVisualShape.Ellipse);

        GameObject tailGo = new GameObject("Tail");
        tailGo.transform.SetParent(go.transform, false);
        tailGo.transform.localPosition = new Vector3(-0.028f, 0f, 0f);
        AddHousePart(
            tailGo.transform,
            "Fin",
            new Vector3(-0.008f, 0f, 0f),
            new Vector2(0.018f, 0.016f),
            0f,
            fin,
            MinnowOrder,
            HumanVisualShape.Ellipse);
        tail = tailGo.transform;

        GameObject shine = new GameObject("Flash");
        shine.transform.SetParent(go.transform, false);
        shine.transform.localPosition = new Vector3(0.006f, 0.002f, 0f);
        SpriteRenderer fsr = shine.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            fsr,
            new Vector2(0.018f, 0.006f),
            HumanVisualShape.Ellipse,
            null,
            new Color(0.78f, 0.88f, 0.80f, 0f),
            MinnowOrder + 1);
        SpriteLighting.ApplyUnlit(fsr);
        fsr.enabled = false;
        flash = fsr;
        return go.transform;
    }

    private static void AddSilk(
        Transform parent,
        string name,
        Vector3 local,
        Vector2 size,
        float tilt,
        Color tint,
        int order)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = local;
        if (tilt * tilt > 0.01f)
            go.transform.localRotation = Quaternion.Euler(0f, 0f, tilt);
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(sr, size, HumanVisualShape.Box, null, tint, order);
    }

    private static Transform MakeBeeWing(
        Transform parent,
        string name,
        Vector3 local,
        float tilt,
        Color tint,
        int order)
    {
        GameObject wing = new GameObject(name);
        wing.transform.SetParent(parent, false);
        wing.transform.localPosition = local;
        wing.transform.localRotation = Quaternion.Euler(0f, 0f, tilt);
        SpriteRenderer sr = wing.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            sr,
            new Vector2(0.036f, 0.010f),
            HumanVisualShape.Ellipse,
            null,
            tint,
            order);
        return wing.transform;
    }

    private static void AddHousePart(
        Transform parent,
        string name,
        Vector3 local,
        Vector2 size,
        float tilt,
        Color tint,
        int order,
        HumanVisualShape shape)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = local;
        if (tilt * tilt > 0.01f)
            go.transform.localRotation = Quaternion.Euler(0f, 0f, tilt);
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(sr, size, shape, null, tint, order);
    }

    private static void AddPotBloom(Transform stem, Vector3 local, float size, Color tint)
    {
        GameObject go = new GameObject("Bloom");
        go.transform.SetParent(stem, false);
        go.transform.localPosition = local;
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            sr,
            new Vector2(size, size * 0.72f),
            HumanVisualShape.Ellipse,
            null,
            tint,
            PotOrder + 3);
    }

    private static void AddGateBar(Transform parent, string name, Vector3 local, Vector2 size, Color tint, int order)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = local;
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(sr, size, HumanVisualShape.Box, null, tint, order);
    }

    private Transform MakeCat(
        Vector3 pos,
        float face,
        float tintU,
        out Transform tail,
        out SpriteRenderer eye)
    {
        Color fur = CatTint(tintU);
        Color dark = Color.Lerp(fur, new Color(0.18f, 0.10f, 0.06f, 1f), 0.42f);

        GameObject go = new GameObject("YardCat");
        go.transform.SetParent(transform, false);
        go.transform.position = pos;

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            sr,
            new Vector2(0.16f, 0.068f),
            HumanVisualShape.Ellipse,
            null,
            fur,
            CatOrder);

        GameObject head = new GameObject("Head");
        head.transform.SetParent(go.transform, false);
        head.transform.localPosition = new Vector3(0.072f, 0.018f, 0f);
        SpriteRenderer hsr = head.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            hsr,
            new Vector2(0.068f, 0.056f),
            HumanVisualShape.Ellipse,
            null,
            fur,
            CatOrder + 1);

        GameObject earL = new GameObject("EarL");
        earL.transform.SetParent(head.transform, false);
        earL.transform.localPosition = new Vector3(-0.014f, 0.030f, 0f);
        earL.transform.localRotation = Quaternion.Euler(0f, 0f, 16f);
        SpriteRenderer elsr = earL.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            elsr,
            new Vector2(0.022f, 0.028f),
            HumanVisualShape.Box,
            null,
            dark,
            CatOrder + 1);

        GameObject earR = new GameObject("EarR");
        earR.transform.SetParent(head.transform, false);
        earR.transform.localPosition = new Vector3(0.016f, 0.028f, 0f);
        earR.transform.localRotation = Quaternion.Euler(0f, 0f, -12f);
        SpriteRenderer ersr = earR.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            ersr,
            new Vector2(0.020f, 0.026f),
            HumanVisualShape.Box,
            null,
            dark,
            CatOrder + 1);

        GameObject eyeGo = new GameObject("Eye");
        eyeGo.transform.SetParent(head.transform, false);
        eyeGo.transform.localPosition = new Vector3(0.016f, 0.006f, 0f);
        SpriteRenderer esr = eyeGo.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            esr,
            new Vector2(0.016f, 0.010f),
            HumanVisualShape.Ellipse,
            null,
            new Color(0.82f, 0.92f, 0.28f, 0f),
            CatOrder + 2);
        SpriteLighting.ApplyUnlit(esr);
        esr.enabled = false;
        eye = esr;

        GameObject stripe = new GameObject("Stripe");
        stripe.transform.SetParent(go.transform, false);
        stripe.transform.localPosition = new Vector3(-0.012f, 0.008f, 0f);
        SpriteRenderer ssr = stripe.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            ssr,
            new Vector2(0.055f, 0.022f),
            HumanVisualShape.Ellipse,
            null,
            dark,
            CatOrder + 1);

        GameObject tailGo = new GameObject("Tail");
        tailGo.transform.SetParent(go.transform, false);
        tailGo.transform.localPosition = new Vector3(-0.078f, 0.006f, 0f);
        tailGo.transform.localRotation = Quaternion.Euler(0f, 0f, 28f);
        GameObject tailVis = new GameObject("Visual");
        tailVis.transform.SetParent(tailGo.transform, false);
        tailVis.transform.localPosition = new Vector3(-0.045f, 0f, 0f);
        SpriteRenderer tsr = tailVis.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            tsr,
            new Vector2(0.090f, 0.022f),
            HumanVisualShape.Ellipse,
            null,
            fur,
            CatOrder);
        tail = tailGo.transform;

        float sx = face >= 0f ? 1f : -1f;
        go.transform.localScale = new Vector3(sx, 1f, 1f);
        return go.transform;
    }

    private Transform MakeSparrow(Vector3 pos, out Transform wing)
    {
        Color body = new Color(0.22f, 0.18f, 0.14f, 1f);
        Color belly = new Color(0.42f, 0.36f, 0.28f, 1f);

        GameObject go = new GameObject("YardSparrow");
        go.transform.SetParent(transform, false);
        go.transform.position = pos;

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            sr,
            new Vector2(0.078f, 0.034f),
            HumanVisualShape.Ellipse,
            null,
            body,
            SparrowOrder);

        GameObject head = new GameObject("Head");
        head.transform.SetParent(go.transform, false);
        head.transform.localPosition = new Vector3(0.038f, 0.010f, 0f);
        SpriteRenderer hsr = head.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            hsr,
            new Vector2(0.030f, 0.026f),
            HumanVisualShape.Ellipse,
            null,
            body,
            SparrowOrder + 1);

        GameObject tail = new GameObject("Tail");
        tail.transform.SetParent(go.transform, false);
        tail.transform.localPosition = new Vector3(-0.042f, 0.004f, 0f);
        tail.transform.localRotation = Quaternion.Euler(0f, 0f, 18f);
        SpriteRenderer tsr = tail.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            tsr,
            new Vector2(0.036f, 0.016f),
            HumanVisualShape.Box,
            null,
            belly,
            SparrowOrder);

        GameObject w = new GameObject("Wing");
        w.transform.SetParent(go.transform, false);
        w.transform.localPosition = new Vector3(0.004f, 0.006f, 0f);
        SpriteRenderer wsr = w.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            wsr,
            new Vector2(0.044f, 0.022f),
            HumanVisualShape.Ellipse,
            null,
            belly,
            SparrowOrder + 1);
        wing = w.transform;
        return go.transform;
    }

    private Transform MakePuddle(Vector2 size, Vector3 pos, out Transform glint)
    {
        GameObject go = new GameObject("YardPuddle");
        go.transform.SetParent(transform, false);
        go.transform.position = pos;
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            sr,
            size,
            HumanVisualShape.Ellipse,
            null,
            new Color(0.22f, 0.46f, 0.58f, 0.46f),
            PuddleOrder);
        SpriteLighting.ApplyUnlit(sr);

        GameObject shine = new GameObject("Glint");
        shine.transform.SetParent(go.transform, false);
        shine.transform.localPosition = new Vector3(0.08f, 0.006f, 0f);
        SpriteRenderer gsr = shine.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            gsr,
            new Vector2(size.x * 0.42f, size.y * 0.38f),
            HumanVisualShape.Ellipse,
            null,
            new Color(0.55f, 0.72f, 0.82f, 0.28f),
            PuddleOrder + 1);
        SpriteLighting.ApplyUnlit(gsr);
        glint = shine.transform;
        return go.transform;
    }

    private Transform MakeLeaf(Vector2 size, Vector3 pos, Color tint)
    {
        GameObject go = new GameObject("YardLeaf");
        go.transform.SetParent(transform, false);
        go.transform.position = pos;
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(sr, size, HumanVisualShape.Ellipse, null, tint, LeafOrder);
        return go.transform;
    }

    private Transform MakePivot(string name, Vector2 size, Vector3 worldFoot, Color color, int order)
    {
        GameObject pivot = new GameObject(name);
        pivot.transform.SetParent(transform, false);
        pivot.transform.position = worldFoot;

        GameObject vis = new GameObject("Visual");
        vis.transform.SetParent(pivot.transform, false);
        vis.transform.localPosition = new Vector3(0f, size.y * 0.5f, 0f);
        SpriteRenderer sr = vis.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(sr, size, HumanVisualShape.Box, null, color, order);
        return pivot.transform;
    }

    // Pivot at the peg: the sprite hangs down, so wind torque reads as
    // cloth, not a tuft growing up from the line.
    private Transform MakeHang(string name, Vector2 size, Vector3 peg, Color color, int order)
    {
        GameObject pivot = new GameObject(name);
        pivot.transform.SetParent(transform, false);
        pivot.transform.position = peg;

        GameObject vis = new GameObject("Visual");
        vis.transform.SetParent(pivot.transform, false);
        vis.transform.localPosition = new Vector3(0f, -size.y * 0.5f, 0f);
        SpriteRenderer sr = vis.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(sr, size, HumanVisualShape.Box, null, color, order);

        GameObject clip = new GameObject("Peg");
        clip.transform.SetParent(pivot.transform, false);
        clip.transform.localPosition = Vector3.zero;
        SpriteRenderer psr = clip.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            psr,
            new Vector2(0.018f, 0.016f),
            HumanVisualShape.Box,
            null,
            new Color(0.22f, 0.14f, 0.08f, 1f),
            order + 1);
        return pivot.transform;
    }

    private Transform MakeClothesLine(Vector3 pos, float width)
    {
        GameObject go = new GameObject("YardClothesline");
        go.transform.SetParent(transform, false);
        go.transform.position = pos;
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            sr,
            new Vector2(width, 0.018f),
            HumanVisualShape.Box,
            null,
            new Color(0.30f, 0.20f, 0.12f, 1f),
            ClothesOrder);
        return go.transform;
    }

    private Transform MakeSapling(float x, float surface, int seed)
    {
        float h = 0.32f + Hash01(seed, 1) * 0.16f;
        Transform pivot = MakePivot(
            "Sapling",
            new Vector2(0.040f, h),
            new Vector3(x, surface, 0f),
            new Color(0.32f, 0.22f, 0.12f, 1f),
            GrassOrder);

        AddLeaf(pivot, new Vector3(-0.05f, h * 0.72f, 0f), new Vector2(0.12f, 0.07f), -22f, seed);
        AddLeaf(pivot, new Vector3(0.05f, h * 0.82f, 0f), new Vector2(0.11f, 0.06f), 18f, seed + 3);
        return pivot;
    }

    // Three stems on one foot pivot so LateUpdate sway is one rotation,
    // not a Find or a per-blade array. Size is sprite.size, not scale.
    private Transform MakeCattail(Vector3 foot, float face, float u)
    {
        float side = face >= 0f ? 1f : -1f;
        GameObject pivot = new GameObject("YardCattail");
        pivot.transform.SetParent(transform, false);
        pivot.transform.position = foot;

        Color stem = new Color(0.28f, 0.42f, 0.18f, 1f);
        Color blade = new Color(0.34f, 0.48f, 0.20f, 1f);
        Color head = new Color(0.38f, 0.22f, 0.10f, 1f);

        AddCattailStem(pivot.transform, -0.030f * side, 0.40f + u * 0.04f, -4f * side, stem, blade, head);
        AddCattailStem(pivot.transform, 0.002f * side, 0.50f + u * 0.06f, 2f * side, stem, blade, head);
        AddCattailStem(pivot.transform, 0.026f * side, 0.44f + u * 0.03f, 8f * side, stem, blade, head);
        return pivot.transform;
    }

    // Pivot on the film so LateUpdate sway nods the pad, not a
    // Find or a per-petal array. Size is sprite.size, not scale.
    // Side view: a thin ellipse on the water, bloom on a short stem.
    private Transform MakeLilyPad(Vector3 film, float face, float u)
    {
        float side = face >= 0f ? 1f : -1f;
        GameObject pivot = new GameObject("YardLilyPad");
        pivot.transform.SetParent(transform, false);
        pivot.transform.position = film;

        Color pad = new Color(0.16f, 0.38f, 0.18f, 1f);
        Color rim = new Color(0.12f, 0.30f, 0.14f, 1f);
        Color stem = new Color(0.22f, 0.42f, 0.16f, 1f);
        Color petal = new Color(0.86f, 0.80f, 0.90f, 1f);
        Color yolk = new Color(0.92f, 0.76f, 0.22f, 1f);

        float padW = 0.16f + u * 0.04f;
        float bloomH = 0.078f + u * 0.018f;
        AddHousePart(
            pivot.transform,
            "Pad",
            new Vector3(0f, 0.012f, 0f),
            new Vector2(padW, 0.028f),
            0f,
            pad,
            LilyPadOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            pivot.transform,
            "Rim",
            new Vector3(-0.012f * side, 0.010f, 0f),
            new Vector2(padW * 0.55f, 0.016f),
            8f * side,
            rim,
            LilyPadOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            pivot.transform,
            "Sat",
            new Vector3(0.070f * side, 0.008f, 0f),
            new Vector2(0.090f, 0.018f),
            -6f * side,
            pad,
            LilyPadOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            pivot.transform,
            "Stem",
            new Vector3(0.006f * side, bloomH * 0.5f, 0f),
            new Vector2(0.010f, bloomH),
            6f * side,
            stem,
            LilyPadOrder + 1,
            HumanVisualShape.Box);
        AddHousePart(
            pivot.transform,
            "PetalL",
            new Vector3(-0.018f * side, bloomH + 0.012f, 0f),
            new Vector2(0.028f, 0.022f),
            28f * side,
            petal,
            LilyPadOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            pivot.transform,
            "PetalR",
            new Vector3(0.016f * side, bloomH + 0.010f, 0f),
            new Vector2(0.026f, 0.020f),
            -22f * side,
            petal,
            LilyPadOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            pivot.transform,
            "PetalUp",
            new Vector3(0.002f * side, bloomH + 0.022f, 0f),
            new Vector2(0.022f, 0.024f),
            4f * side,
            petal,
            LilyPadOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            pivot.transform,
            "Yolk",
            new Vector3(0.002f * side, bloomH + 0.008f, 0f),
            new Vector2(0.016f, 0.014f),
            0f,
            yolk,
            LilyPadOrder + 2,
            HumanVisualShape.Ellipse);
        return pivot.transform;
    }

    // Two stems on one foot pivot so LateUpdate sway is one rotation,
    // not a Find or a per-stem array. Size is sprite.size, not scale.
    // Side view: a thin stalk and a yellow bloom or a pale seed clock.
    private Transform MakeDandelion(Vector3 foot, float face, float u)
    {
        float side = face >= 0f ? 1f : -1f;
        GameObject pivot = new GameObject("YardDandelion");
        pivot.transform.SetParent(transform, false);
        pivot.transform.position = foot;

        Color stem = new Color(0.32f, 0.48f, 0.18f, 1f);
        Color leaf = new Color(0.26f, 0.42f, 0.14f, 1f);
        Color bloom = new Color(0.92f, 0.78f, 0.18f, 1f);
        Color clock = new Color(0.92f, 0.92f, 0.86f, 1f);

        AddDandelionStem(
            pivot.transform,
            -0.018f * side,
            0.128f + u * 0.018f,
            -6f * side,
            stem,
            leaf,
            bloom,
            false);
        AddDandelionStem(
            pivot.transform,
            0.016f * side,
            0.108f + u * 0.014f,
            8f * side,
            stem,
            leaf,
            u > 0.45f ? clock : bloom,
            u > 0.45f);
        return pivot.transform;
    }

    // Three caps on one foot pivot so LateUpdate sway is one rotation,
    // not a Find or a per-stem array. Size is sprite.size, not scale.
    // Side view: a thin stalk and a wide cap; red ones get pale spots.
    private Transform MakeMushroom(Vector3 foot, float face, float u)
    {
        float side = face >= 0f ? 1f : -1f;
        GameObject pivot = new GameObject("YardMushroom");
        pivot.transform.SetParent(transform, false);
        pivot.transform.position = foot;

        Color stem = new Color(0.78f, 0.74f, 0.58f, 1f);
        Color gill = new Color(0.88f, 0.82f, 0.64f, 1f);
        bool flyAgaric = u < 0.42f;
        Color cap = flyAgaric
            ? new Color(0.78f, 0.18f, 0.16f, 1f)
            : (u < 0.72f
                ? new Color(0.58f, 0.42f, 0.24f, 1f)
                : new Color(0.86f, 0.82f, 0.68f, 1f));
        Color spot = new Color(0.94f, 0.92f, 0.86f, 1f);

        AddMushroomStem(
            pivot.transform,
            -0.026f * side,
            0.038f + u * 0.008f,
            -8f * side,
            0.036f,
            0.016f,
            stem,
            gill,
            cap,
            false,
            spot);
        AddMushroomStem(
            pivot.transform,
            0.002f * side,
            0.052f + u * 0.010f,
            3f * side,
            0.046f,
            0.020f,
            stem,
            gill,
            cap,
            flyAgaric,
            spot);
        AddMushroomStem(
            pivot.transform,
            0.024f * side,
            0.030f + u * 0.006f,
            10f * side,
            0.028f,
            0.013f,
            stem,
            gill,
            cap,
            false,
            spot);
        return pivot.transform;
    }

    // One vine on one foot pivot so LateUpdate sway is one rotation,
    // not a Find or a per-leaf array. Size is sprite.size, not scale.
    // Side view: a thin climb on the post face and five leaf ellipses.
    // Stays the foot so the birdhouse / lantern / lizard keep the top.
    private Transform MakeIvy(Vector3 foot, float face, float u)
    {
        float side = face >= 0f ? 1f : -1f;
        GameObject pivot = new GameObject("YardIvy");
        pivot.transform.SetParent(transform, false);
        pivot.transform.position = foot;

        float h = 0.30f + u * 0.08f;
        Color vine = new Color(0.28f, 0.38f, 0.16f, 1f);
        Color leaf = new Color(0.22f, 0.46f, 0.18f, 1f);
        Color pale = new Color(0.34f, 0.56f, 0.24f, 1f);

        AddHousePart(
            pivot.transform,
            "Vine",
            new Vector3(side * 0.040f, h * 0.5f, 0f),
            new Vector2(0.012f, h),
            -6f * side,
            vine,
            IvyOrder,
            HumanVisualShape.Box);
        AddIvyLeaf(pivot.transform, side * 0.062f, h * 0.18f, 22f * side, 0.040f, leaf);
        AddIvyLeaf(pivot.transform, side * 0.018f, h * 0.34f, -16f * side, 0.036f, pale);
        AddIvyLeaf(pivot.transform, side * 0.070f, h * 0.52f, 14f * side, 0.042f, leaf);
        AddIvyLeaf(pivot.transform, side * 0.022f, h * 0.70f, -20f * side, 0.034f, pale);
        AddIvyLeaf(pivot.transform, side * 0.058f, h * 0.88f, 10f * side, 0.038f, leaf);
        return pivot.transform;
    }

    // Three fronds on one foot pivot so LateUpdate sway is one rotation,
    // not a Find or a per-frond array. Size is sprite.size, not scale.
    // Side view: arched rachis with pinnae that shrink toward the tip.
    private Transform MakeFern(Vector3 foot, float face, float u)
    {
        float side = face >= 0f ? 1f : -1f;
        GameObject pivot = new GameObject("YardFern");
        pivot.transform.SetParent(transform, false);
        pivot.transform.position = foot;

        Color rachis = new Color(0.22f, 0.36f, 0.14f, 1f);
        Color leaflet = new Color(0.20f, 0.46f, 0.18f, 1f);
        Color pale = new Color(0.32f, 0.54f, 0.24f, 1f);

        AddFernFrond(
            pivot.transform,
            -0.018f * side,
            0.100f + u * 0.012f,
            -22f * side,
            rachis,
            leaflet,
            pale);
        AddFernFrond(
            pivot.transform,
            0.004f * side,
            0.138f + u * 0.018f,
            -5f * side,
            rachis,
            leaflet,
            pale);
        AddFernFrond(
            pivot.transform,
            0.022f * side,
            0.112f + u * 0.010f,
            16f * side,
            rachis,
            leaflet,
            pale);
        return pivot.transform;
    }

    // Two stems on one foot pivot so LateUpdate sway is one rotation,
    // not a Find or a per-stem array. Size is sprite.size, not scale.
    // Side view: a short stalk, a trifoliate of round leaflets, and
    // a pale bloom or a leaf-only clump.
    private Transform MakeClover(Vector3 foot, float face, float u)
    {
        float side = face >= 0f ? 1f : -1f;
        GameObject pivot = new GameObject("YardClover");
        pivot.transform.SetParent(transform, false);
        pivot.transform.position = foot;

        Color stem = new Color(0.28f, 0.46f, 0.16f, 1f);
        Color leaflet = new Color(0.22f, 0.52f, 0.22f, 1f);
        Color pale = new Color(0.34f, 0.58f, 0.28f, 1f);
        Color bloom = u > 0.55f
            ? new Color(0.88f, 0.38f, 0.48f, 1f)
            : new Color(0.92f, 0.88f, 0.90f, 1f);

        AddCloverStem(
            pivot.transform,
            -0.016f * side,
            0.052f + u * 0.010f,
            -8f * side,
            stem,
            leaflet,
            pale,
            bloom,
            true);
        AddCloverStem(
            pivot.transform,
            0.018f * side,
            0.042f + u * 0.008f,
            10f * side,
            stem,
            leaflet,
            pale,
            bloom,
            u > 0.38f);
        return pivot.transform;
    }

    // Two stems on one foot pivot so LateUpdate sway is one rotation,
    // not a Find or a per-stem array. Size is sprite.size, not scale.
    // Side view: a stiff stalk, clasping spiny leaves, and a purple
    // globe — taller than clover, not a dandelion clock.
    private Transform MakeThistle(Vector3 foot, float face, float u)
    {
        float side = face >= 0f ? 1f : -1f;
        GameObject pivot = new GameObject("YardThistle");
        pivot.transform.SetParent(transform, false);
        pivot.transform.position = foot;

        Color stem = new Color(0.30f, 0.42f, 0.16f, 1f);
        Color leaf = new Color(0.24f, 0.38f, 0.14f, 1f);
        Color pale = new Color(0.32f, 0.46f, 0.18f, 1f);
        Color head = new Color(0.56f, 0.24f, 0.58f, 1f);
        Color bract = new Color(0.28f, 0.40f, 0.16f, 1f);

        AddThistleStem(
            pivot.transform,
            -0.014f * side,
            0.148f + u * 0.018f,
            -6f * side,
            stem,
            leaf,
            pale,
            head,
            bract,
            true);
        AddThistleStem(
            pivot.transform,
            0.016f * side,
            0.118f + u * 0.014f,
            9f * side,
            stem,
            leaf,
            pale,
            head,
            bract,
            u > 0.40f);
        return pivot.transform;
    }

    // Two spikes on one foot pivot so LateUpdate sway is one rotation,
    // not a Find or a per-stem array. Size is sprite.size, not scale.
    // Side view: a low lance rosette and a thin brown seed spike —
    // shorter than a thistle, not a clover carpet, not a dandelion clock.
    private Transform MakePlantain(Vector3 foot, float face, float u)
    {
        float side = face >= 0f ? 1f : -1f;
        GameObject pivot = new GameObject("YardPlantain");
        pivot.transform.SetParent(transform, false);
        pivot.transform.position = foot;

        Color stem = new Color(0.32f, 0.40f, 0.18f, 1f);
        Color leaf = new Color(0.26f, 0.42f, 0.18f, 1f);
        Color pale = new Color(0.34f, 0.48f, 0.22f, 1f);
        Color seed = new Color(0.42f, 0.32f, 0.16f, 1f);

        AddHousePart(
            pivot.transform,
            "Rosette",
            new Vector3(-0.028f * side, 0.012f, 0f),
            new Vector2(0.048f, 0.016f),
            -18f * side,
            leaf,
            PlantainOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            pivot.transform,
            "Rosette",
            new Vector3(0.024f * side, 0.010f, 0f),
            new Vector2(0.042f, 0.014f),
            22f * side,
            pale,
            PlantainOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            pivot.transform,
            "Rosette",
            new Vector3(-0.004f * side, 0.016f, 0f),
            new Vector2(0.036f, 0.013f),
            8f * side,
            leaf,
            PlantainOrder,
            HumanVisualShape.Ellipse);

        AddPlantainSpike(
            pivot.transform,
            -0.008f * side,
            0.112f + u * 0.016f,
            -4f * side,
            stem,
            seed,
            true);
        AddPlantainSpike(
            pivot.transform,
            0.012f * side,
            0.086f + u * 0.012f,
            7f * side,
            stem,
            seed,
            u > 0.42f);
        return pivot.transform;
    }

    // Two stems on one foot pivot so LateUpdate sway is one rotation,
    // not a Find or a per-stem array. Size is sprite.size, not scale.
    // Side view: a square-ish shaft, opposite toothed leaves, and
    // hanging catkin tassels — not a thistle globe, not a fern frond,
    // not a plantain spike.
    private Transform MakeNettle(Vector3 foot, float face, float u)
    {
        float side = face >= 0f ? 1f : -1f;
        GameObject pivot = new GameObject("YardNettle");
        pivot.transform.SetParent(transform, false);
        pivot.transform.position = foot;

        Color stem = new Color(0.28f, 0.38f, 0.16f, 1f);
        Color leaf = new Color(0.22f, 0.40f, 0.16f, 1f);
        Color pale = new Color(0.30f, 0.46f, 0.20f, 1f);
        Color catkin = new Color(0.42f, 0.46f, 0.18f, 1f);

        AddNettleStem(
            pivot.transform,
            -0.012f * side,
            0.168f + u * 0.022f,
            -5f * side,
            stem,
            leaf,
            pale,
            catkin,
            true);
        AddNettleStem(
            pivot.transform,
            0.014f * side,
            0.138f + u * 0.016f,
            8f * side,
            stem,
            leaf,
            pale,
            catkin,
            u > 0.34f);
        return pivot.transform;
    }

    // Two stems on one foot pivot so LateUpdate sway is one rotation,
    // not a Find or a per-stem array. Size is sprite.size, not scale.
    // Side view: broad oval basal leaves and a tall rusty seed
    // panicle — not a nettle tassel, not a thistle globe, not a
    // plantain spike, not a cattail reed.
    private Transform MakeDock(Vector3 foot, float face, float u)
    {
        float side = face >= 0f ? 1f : -1f;
        GameObject pivot = new GameObject("YardDock");
        pivot.transform.SetParent(transform, false);
        pivot.transform.position = foot;

        Color stem = new Color(0.46f, 0.32f, 0.16f, 1f);
        Color leaf = new Color(0.24f, 0.40f, 0.16f, 1f);
        Color pale = new Color(0.32f, 0.46f, 0.18f, 1f);
        Color rust = new Color(0.58f, 0.30f, 0.14f, 1f);

        AddDockStem(
            pivot.transform,
            -0.010f * side,
            0.188f + u * 0.024f,
            -4f * side,
            stem,
            leaf,
            pale,
            rust,
            true);
        AddDockStem(
            pivot.transform,
            0.016f * side,
            0.152f + u * 0.016f,
            7f * side,
            stem,
            leaf,
            pale,
            rust,
            u > 0.36f);
        return pivot.transform;
    }

    // Two canes on one foot pivot so LateUpdate sway is one rotation,
    // not a Find or a per-stem array. Size is sprite.size, not scale.
    // Side view: arching woody canes, compound leaflets, and a dark
    // berry cluster — not an upright dock panicle, not ivy on a
    // post, not a thistle globe.
    private Transform MakeBramble(Vector3 foot, float face, float u)
    {
        float side = face >= 0f ? 1f : -1f;
        GameObject pivot = new GameObject("YardBramble");
        pivot.transform.SetParent(transform, false);
        pivot.transform.position = foot;

        Color stem = new Color(0.30f, 0.24f, 0.12f, 1f);
        Color leaf = new Color(0.18f, 0.36f, 0.14f, 1f);
        Color pale = new Color(0.26f, 0.44f, 0.16f, 1f);
        Color berry = new Color(0.16f, 0.06f, 0.14f, 1f);

        AddBrambleCane(
            pivot.transform,
            -0.012f * side,
            0.210f + u * 0.024f,
            -34f * side,
            stem,
            leaf,
            pale,
            berry,
            true);
        AddBrambleCane(
            pivot.transform,
            0.016f * side,
            0.164f + u * 0.018f,
            -48f * side,
            stem,
            leaf,
            pale,
            berry,
            u > 0.30f);
        return pivot.transform;
    }

    // Two spikes on one foot pivot so LateUpdate sway is one rotation,
    // not a Find or a per-stem array. Size is sprite.size, not scale.
    // Side view: a tall green shaft, palmate basal leaves, and stacked
    // round blooms — not a thistle globe, not a dock panicle, not a
    // terracotta pot bloom, not a strawberry bush.
    private Transform MakeHollyhock(Vector3 foot, float face, float u)
    {
        float side = face >= 0f ? 1f : -1f;
        GameObject pivot = new GameObject("YardHollyhock");
        pivot.transform.SetParent(transform, false);
        pivot.transform.position = foot;

        Color stem = new Color(0.28f, 0.44f, 0.16f, 1f);
        Color leaf = new Color(0.22f, 0.48f, 0.18f, 1f);
        Color pale = new Color(0.32f, 0.54f, 0.22f, 1f);
        Color rose = new Color(0.82f, 0.28f, 0.40f, 1f);
        Color blush = new Color(0.92f, 0.62f, 0.68f, 1f);

        AddHollyhockStem(
            pivot.transform,
            -0.010f * side,
            0.332f + u * 0.028f,
            -5f * side,
            stem,
            leaf,
            pale,
            rose,
            blush,
            true);
        AddHollyhockStem(
            pivot.transform,
            0.018f * side,
            0.248f + u * 0.020f,
            8f * side,
            stem,
            leaf,
            pale,
            rose,
            blush,
            u > 0.28f);
        return pivot.transform;
    }

    // Two canes on one foot pivot so LateUpdate sway is one rotation,
    // not a Find or a per-stem array. Size is sprite.size, not scale.
    // Side view: a round woody bush, compound leaflets, and clustered
    // blooms in the crown — not a hollyhock spike, not a strawberry
    // bush, not a terracotta pot bloom, not a wild bramble arch.
    private Transform MakeRose(Vector3 foot, float face, float u)
    {
        float side = face >= 0f ? 1f : -1f;
        GameObject pivot = new GameObject("YardRose");
        pivot.transform.SetParent(transform, false);
        pivot.transform.position = foot;

        Color wood = new Color(0.32f, 0.20f, 0.10f, 1f);
        Color leaf = new Color(0.16f, 0.42f, 0.16f, 1f);
        Color pale = new Color(0.24f, 0.50f, 0.20f, 1f);
        Color bloom = new Color(0.76f, 0.16f, 0.26f, 1f);
        Color blush = new Color(0.90f, 0.46f, 0.52f, 1f);
        Color eye = new Color(0.92f, 0.78f, 0.22f, 1f);

        AddRoseCane(
            pivot.transform,
            -0.012f * side,
            0.176f + u * 0.022f,
            -10f * side,
            wood,
            leaf,
            pale,
            bloom,
            blush,
            eye,
            true);
        AddRoseCane(
            pivot.transform,
            0.016f * side,
            0.142f + u * 0.018f,
            12f * side,
            wood,
            leaf,
            pale,
            bloom,
            blush,
            eye,
            u > 0.26f);
        return pivot.transform;
    }

    // Two tufts on one foot pivot so LateUpdate sway is one rotation,
    // not a Find or a per-stem array. Size is sprite.size, not scale.
    // Side view: a low grey-green mound, needle leaves, and short
    // purple spikes — not a hollyhock column, not a rose shrub, not
    // a dock panicle, not a terracotta pot bloom.
    private Transform MakeLavender(Vector3 foot, float face, float u)
    {
        float side = face >= 0f ? 1f : -1f;
        GameObject pivot = new GameObject("YardLavender");
        pivot.transform.SetParent(transform, false);
        pivot.transform.position = foot;

        Color stem = new Color(0.30f, 0.38f, 0.22f, 1f);
        Color leaf = new Color(0.32f, 0.42f, 0.28f, 1f);
        Color pale = new Color(0.40f, 0.48f, 0.32f, 1f);
        Color spike = new Color(0.48f, 0.32f, 0.62f, 1f);
        Color blush = new Color(0.62f, 0.46f, 0.74f, 1f);

        AddLavenderTuft(
            pivot.transform,
            -0.010f * side,
            0.118f + u * 0.016f,
            -8f * side,
            stem,
            leaf,
            pale,
            spike,
            blush,
            true);
        AddLavenderTuft(
            pivot.transform,
            0.016f * side,
            0.096f + u * 0.012f,
            11f * side,
            stem,
            leaf,
            pale,
            spike,
            blush,
            u > 0.24f);
        return pivot.transform;
    }

    // Two fans on one foot pivot so LateUpdate sway is one rotation,
    // not a Find or a per-stem array. Size is sprite.size, not scale.
    // Side view: a mid sword-leaf fan, a thin scape, and a flag
    // bloom (fall + standard + beard) — not a hollyhock column, not
    // a rose shrub, not a lavender mound, not a cattail sausage,
    // not a fern pinna.
    private Transform MakeIris(Vector3 foot, float face, float u)
    {
        float side = face >= 0f ? 1f : -1f;
        GameObject pivot = new GameObject("YardIris");
        pivot.transform.SetParent(transform, false);
        pivot.transform.position = foot;

        Color stem = new Color(0.30f, 0.40f, 0.18f, 1f);
        Color leaf = new Color(0.22f, 0.46f, 0.20f, 1f);
        Color pale = new Color(0.32f, 0.52f, 0.26f, 1f);
        Color fall = new Color(0.38f, 0.22f, 0.58f, 1f);
        Color flag = new Color(0.52f, 0.36f, 0.72f, 1f);
        Color beard = new Color(0.86f, 0.68f, 0.18f, 1f);

        AddIrisFan(
            pivot.transform,
            -0.010f * side,
            0.236f + u * 0.022f,
            -7f * side,
            stem,
            leaf,
            pale,
            fall,
            flag,
            beard,
            true);
        AddIrisFan(
            pivot.transform,
            0.016f * side,
            0.188f + u * 0.018f,
            10f * side,
            stem,
            leaf,
            pale,
            fall,
            flag,
            beard,
            u > 0.26f);
        return pivot.transform;
    }

    // Two cups on one foot pivot so LateUpdate sway is one rotation,
    // not a Find or a per-stem array. Size is sprite.size, not scale.
    // Side view: a thin scape, two wrapping lance leaves, and an
    // upright cup (outer petal + throat) — not a hollyhock column,
    // not a rose shrub, not a lavender mound, not an iris sword fan.
    private Transform MakeTulip(Vector3 foot, float face, float u)
    {
        float side = face >= 0f ? 1f : -1f;
        GameObject pivot = new GameObject("YardTulip");
        pivot.transform.SetParent(transform, false);
        pivot.transform.position = foot;

        Color stem = new Color(0.28f, 0.42f, 0.16f, 1f);
        Color leaf = new Color(0.20f, 0.48f, 0.18f, 1f);
        Color pale = new Color(0.30f, 0.54f, 0.22f, 1f);
        Color cup = new Color(0.86f, 0.22f, 0.28f, 1f);
        Color throat = new Color(0.94f, 0.46f, 0.42f, 1f);

        AddTulipStem(
            pivot.transform,
            -0.010f * side,
            0.248f + u * 0.022f,
            -6f * side,
            stem,
            leaf,
            pale,
            cup,
            throat,
            true);
        AddTulipStem(
            pivot.transform,
            0.016f * side,
            0.196f + u * 0.016f,
            9f * side,
            stem,
            leaf,
            pale,
            cup,
            throat,
            u > 0.26f);
        return pivot.transform;
    }

    // Two trumpets on one foot pivot so LateUpdate sway is one
    // rotation, not a Find or a per-stem array. Size is sprite.size,
    // not scale. Side view: a thin scape, two upright strap leaves,
    // a pale perianth disc, and a gold corona tube pointing along
    // facing — not a tulip cup, not an iris sword fan, not a
    // hollyhock column.
    private Transform MakeDaffodil(Vector3 foot, float face, float u)
    {
        float side = face >= 0f ? 1f : -1f;
        GameObject pivot = new GameObject("YardDaffodil");
        pivot.transform.SetParent(transform, false);
        pivot.transform.position = foot;

        Color stem = new Color(0.30f, 0.44f, 0.16f, 1f);
        Color leaf = new Color(0.24f, 0.50f, 0.18f, 1f);
        Color pale = new Color(0.34f, 0.56f, 0.22f, 1f);
        Color petal = new Color(0.96f, 0.90f, 0.42f, 1f);
        Color corona = new Color(0.94f, 0.68f, 0.12f, 1f);
        Color rim = new Color(0.90f, 0.48f, 0.10f, 1f);

        AddDaffodilStem(
            pivot.transform,
            -0.010f * side,
            0.232f + u * 0.022f,
            -5f * side,
            side,
            stem,
            leaf,
            pale,
            petal,
            corona,
            rim,
            true);
        AddDaffodilStem(
            pivot.transform,
            0.016f * side,
            0.184f + u * 0.016f,
            8f * side,
            side,
            stem,
            leaf,
            pale,
            petal,
            corona,
            rim,
            u > 0.26f);
        return pivot.transform;
    }

    // Two heads on one foot pivot so LateUpdate sway is one
    // rotation, not a Find or a per-stem array. Size is sprite.size,
    // not scale. Side view: a short scape, two basal lances, white
    // ray ellipses around a gold disk — not a dandelion clock, not
    // a tulip cup, not a daffodil corona, not a lily-pad film bloom.
    private Transform MakeDaisy(Vector3 foot, float face, float u)
    {
        float side = face >= 0f ? 1f : -1f;
        GameObject pivot = new GameObject("YardDaisy");
        pivot.transform.SetParent(transform, false);
        pivot.transform.position = foot;

        Color stem = new Color(0.30f, 0.46f, 0.16f, 1f);
        Color leaf = new Color(0.24f, 0.50f, 0.18f, 1f);
        Color pale = new Color(0.34f, 0.56f, 0.22f, 1f);
        Color ray = new Color(0.96f, 0.96f, 0.90f, 1f);
        Color disk = new Color(0.92f, 0.74f, 0.16f, 1f);

        AddDaisyStem(
            pivot.transform,
            -0.010f * side,
            0.168f + u * 0.020f,
            -6f * side,
            side,
            stem,
            leaf,
            pale,
            ray,
            disk,
            true);
        AddDaisyStem(
            pivot.transform,
            0.016f * side,
            0.132f + u * 0.014f,
            9f * side,
            side,
            stem,
            leaf,
            pale,
            ray,
            disk,
            u > 0.26f);
        return pivot.transform;
    }

    // Two heads on one foot pivot so LateUpdate sway is one
    // rotation, not a Find or a per-stem array. Size is sprite.size,
    // not scale. Side view: a floppy scape, two lobed basal leaves,
    // four crumpled scarlet petals as an open bowl, and a dark
    // blotch — not a tulip cup, not a daisy ray-disk, not a
    // daffodil corona. The second stem is a nodding bud.
    private Transform MakePoppy(Vector3 foot, float face, float u)
    {
        float side = face >= 0f ? 1f : -1f;
        GameObject pivot = new GameObject("YardPoppy");
        pivot.transform.SetParent(transform, false);
        pivot.transform.position = foot;

        Color stem = new Color(0.32f, 0.46f, 0.16f, 1f);
        Color leaf = new Color(0.22f, 0.48f, 0.16f, 1f);
        Color pale = new Color(0.34f, 0.54f, 0.20f, 1f);
        Color petal = new Color(0.86f, 0.12f, 0.10f, 1f);
        Color crease = new Color(0.72f, 0.08f, 0.08f, 1f);
        Color blotch = new Color(0.12f, 0.10f, 0.10f, 1f);

        AddPoppyStem(
            pivot.transform,
            -0.010f * side,
            0.220f + u * 0.022f,
            -10f * side,
            side,
            stem,
            leaf,
            pale,
            petal,
            crease,
            blotch,
            true);
        AddPoppyStem(
            pivot.transform,
            0.016f * side,
            0.172f + u * 0.016f,
            12f * side,
            side,
            stem,
            leaf,
            pale,
            petal,
            crease,
            blotch,
            u > 0.26f);
        return pivot.transform;
    }

    // Two heads on one foot pivot so LateUpdate sway is one
    // rotation, not a Find or a per-stem array. Size is sprite.size,
    // not scale. Side view: a tall scape, two alternate lances,
    // six pointed tepals as an open star, a blush throat and a
    // speckled fall — not a tulip cup, not a daffodil corona,
    // not a poppy bowl, not a daisy ray-disk, not a lily-pad
    // film. The second stem is a nodding bud.
    private Transform MakeLily(Vector3 foot, float face, float u)
    {
        float side = face >= 0f ? 1f : -1f;
        GameObject pivot = new GameObject("YardLily");
        pivot.transform.SetParent(transform, false);
        pivot.transform.position = foot;

        Color stem = new Color(0.30f, 0.48f, 0.16f, 1f);
        Color leaf = new Color(0.20f, 0.50f, 0.18f, 1f);
        Color pale = new Color(0.32f, 0.56f, 0.22f, 1f);
        Color tepal = new Color(0.96f, 0.88f, 0.90f, 1f);
        Color blush = new Color(0.90f, 0.55f, 0.62f, 1f);
        Color speckle = new Color(0.62f, 0.16f, 0.22f, 1f);

        AddLilyStem(
            pivot.transform,
            -0.010f * side,
            0.268f + u * 0.022f,
            -6f * side,
            side,
            stem,
            leaf,
            pale,
            tepal,
            blush,
            speckle,
            true);
        AddLilyStem(
            pivot.transform,
            0.016f * side,
            0.214f + u * 0.016f,
            10f * side,
            side,
            stem,
            leaf,
            pale,
            tepal,
            blush,
            speckle,
            u > 0.26f);
        return pivot.transform;
    }

    // Two heads on one foot pivot so LateUpdate sway is one
    // rotation, not a Find or a per-stem array. Size is sprite.size,
    // not scale. Side view: a thick tall scape, two heart leaves,
    // a huge yellow ray disk and a dark seed face — not a daisy
    // (small white rays), not a dandelion clock, not a hollyhock
    // column of cups, not a lily star. The second stem is a
    // nodding green bud.
    private Transform MakeSunflower(Vector3 foot, float face, float u)
    {
        float side = face >= 0f ? 1f : -1f;
        GameObject pivot = new GameObject("YardSunflower");
        pivot.transform.SetParent(transform, false);
        pivot.transform.position = foot;

        Color stem = new Color(0.28f, 0.46f, 0.14f, 1f);
        Color leaf = new Color(0.18f, 0.48f, 0.16f, 1f);
        Color pale = new Color(0.30f, 0.54f, 0.20f, 1f);
        Color ray = new Color(0.94f, 0.78f, 0.14f, 1f);
        Color rim = new Color(0.90f, 0.62f, 0.10f, 1f);
        Color disk = new Color(0.36f, 0.22f, 0.08f, 1f);

        AddSunflowerStem(
            pivot.transform,
            -0.010f * side,
            0.392f + u * 0.028f,
            -5f * side,
            side,
            stem,
            leaf,
            pale,
            ray,
            rim,
            disk,
            true);
        AddSunflowerStem(
            pivot.transform,
            0.018f * side,
            0.268f + u * 0.020f,
            9f * side,
            side,
            stem,
            leaf,
            pale,
            ray,
            rim,
            disk,
            u > 0.26f);
        return pivot.transform;
    }

    // Two heads on one foot pivot so LateUpdate sway is one
    // rotation, not a Find or a per-stem array. Size is sprite.size,
    // not scale. Side view: a very short scape, two heart leaves,
    // five petals as a viola face and a yellow throat — not an
    // iris flag (sword leaves), not a lavender spike, not a lily
    // star, not a crocus cup. The second stem is a nodding bud.
    private Transform MakeViolet(Vector3 foot, float face, float u)
    {
        float side = face >= 0f ? 1f : -1f;
        GameObject pivot = new GameObject("YardViolet");
        pivot.transform.SetParent(transform, false);
        pivot.transform.position = foot;

        Color stem = new Color(0.28f, 0.46f, 0.16f, 1f);
        Color leaf = new Color(0.16f, 0.42f, 0.16f, 1f);
        Color pale = new Color(0.26f, 0.50f, 0.22f, 1f);
        Color petal = new Color(0.46f, 0.24f, 0.66f, 1f);
        Color deep = new Color(0.32f, 0.14f, 0.52f, 1f);
        Color throat = new Color(0.92f, 0.80f, 0.28f, 1f);

        AddVioletStem(
            pivot.transform,
            -0.008f * side,
            0.108f + u * 0.016f,
            -8f * side,
            side,
            stem,
            leaf,
            pale,
            petal,
            deep,
            throat,
            true);
        AddVioletStem(
            pivot.transform,
            0.014f * side,
            0.082f + u * 0.012f,
            11f * side,
            side,
            stem,
            leaf,
            pale,
            petal,
            deep,
            throat,
            u > 0.26f);
        return pivot.transform;
    }

    // Two cups on one foot pivot so LateUpdate sway is one
    // rotation, not a Find or a per-stem array. Size is sprite.size,
    // not scale. Side view: a very short scape, two grass blades
    // taller than the cup, an open goblet of six tepals and orange
    // stamens — not a tulip closed cup, not a violet face, not an
    // iris flag, not a hanging bluebell. The second stem is a
    // closed spear.
    private Transform MakeCrocus(Vector3 foot, float face, float u)
    {
        float side = face >= 0f ? 1f : -1f;
        GameObject pivot = new GameObject("YardCrocus");
        pivot.transform.SetParent(transform, false);
        pivot.transform.position = foot;

        Color stem = new Color(0.26f, 0.48f, 0.18f, 1f);
        Color leaf = new Color(0.18f, 0.46f, 0.20f, 1f);
        Color pale = new Color(0.28f, 0.54f, 0.26f, 1f);
        Color petal = new Color(0.62f, 0.38f, 0.82f, 1f);
        Color deep = new Color(0.48f, 0.24f, 0.70f, 1f);
        Color throat = new Color(0.94f, 0.62f, 0.18f, 1f);

        AddCrocusStem(
            pivot.transform,
            -0.006f * side,
            0.092f + u * 0.014f,
            -4f * side,
            side,
            stem,
            leaf,
            pale,
            petal,
            deep,
            throat,
            true);
        AddCrocusStem(
            pivot.transform,
            0.012f * side,
            0.072f + u * 0.010f,
            8f * side,
            side,
            stem,
            leaf,
            pale,
            petal,
            deep,
            throat,
            u > 0.26f);
        return pivot.transform;
    }

    // Two racemes on one foot pivot so LateUpdate sway is one
    // rotation, not a Find or a per-stem array. Size is sprite.size,
    // not scale. Side view: strap leaves, an arching scape, hanging
    // bells with pale mouths — not a crocus goblet, not a violet
    // face, not a lavender spike. The second stem is a closed bud.
    private Transform MakeBluebell(Vector3 foot, float face, float u)
    {
        float side = face >= 0f ? 1f : -1f;
        GameObject pivot = new GameObject("YardBluebell");
        pivot.transform.SetParent(transform, false);
        pivot.transform.position = foot;

        Color stem = new Color(0.22f, 0.46f, 0.20f, 1f);
        Color leaf = new Color(0.16f, 0.42f, 0.18f, 1f);
        Color pale = new Color(0.22f, 0.50f, 0.24f, 1f);
        Color petal = new Color(0.38f, 0.42f, 0.82f, 1f);
        Color deep = new Color(0.26f, 0.28f, 0.68f, 1f);
        Color mouth = new Color(0.86f, 0.88f, 0.94f, 1f);

        AddBluebellStem(
            pivot.transform,
            -0.008f * side,
            0.148f + u * 0.018f,
            -10f * side,
            side,
            stem,
            leaf,
            pale,
            petal,
            deep,
            mouth,
            true);
        AddBluebellStem(
            pivot.transform,
            0.016f * side,
            0.118f + u * 0.014f,
            8f * side,
            side,
            stem,
            leaf,
            pale,
            petal,
            deep,
            mouth,
            u > 0.26f);
        return pivot.transform;
    }

    // Two vines on one foot pivot so LateUpdate sway is one
    // rotation, not a Find or a per-stem array. Size is sprite.size,
    // not scale. Side view: one wooden stake, odd-pinnate leaves,
    // hanging red fruit — not a hollyhock column, not a sunflower
    // disk, not a strawberry bush, not a bluebell raceme. The
    // second vine is greener fruit.
    private Transform MakeTomato(Vector3 foot, float face, float u)
    {
        float side = face >= 0f ? 1f : -1f;
        GameObject pivot = new GameObject("YardTomato");
        pivot.transform.SetParent(transform, false);
        pivot.transform.position = foot;

        float stakeH = 0.312f + u * 0.022f;
        Color stake = new Color(0.46f, 0.30f, 0.16f, 1f);
        Color vine = new Color(0.22f, 0.50f, 0.18f, 1f);
        Color leaf = new Color(0.16f, 0.42f, 0.14f, 1f);
        Color pale = new Color(0.30f, 0.56f, 0.20f, 1f);
        Color ripe = new Color(0.86f, 0.18f, 0.14f, 1f);
        Color deep = new Color(0.70f, 0.10f, 0.10f, 1f);
        Color green = new Color(0.42f, 0.58f, 0.16f, 1f);
        Color bloom = new Color(0.96f, 0.86f, 0.22f, 1f);

        AddHousePart(
            pivot.transform,
            "Stake",
            new Vector3(0f, stakeH * 0.5f, 0f),
            new Vector2(0.012f, stakeH),
            0f,
            stake,
            TomatoOrder,
            HumanVisualShape.Box);

        AddTomatoVine(
            pivot.transform,
            -0.010f * side,
            0.268f + u * 0.018f,
            -6f * side,
            side,
            vine,
            leaf,
            pale,
            ripe,
            deep,
            green,
            bloom,
            true);
        AddTomatoVine(
            pivot.transform,
            0.014f * side,
            0.214f + u * 0.014f,
            8f * side,
            side,
            vine,
            leaf,
            pale,
            ripe,
            deep,
            green,
            bloom,
            u > 0.28f);
        return pivot.transform;
    }

    // Two heads on one foot pivot so LateUpdate sway is one
    // rotation, not a Find or a per-leaf array. Size is sprite.size,
    // not scale. Side view: overlapping WRAPPER ellipses sitting on
    // the dirt, a pale heart, and two splayed basal leaves — not a
    // tomato stake, not a sunflower disk, not a hollyhock column,
    // not a strawberry bush, not a mushroom cap. u < 0.38 paints
    // a red head; the second head is a smaller sibling.
    private Transform MakeCabbage(Vector3 foot, float face, float u)
    {
        float side = face >= 0f ? 1f : -1f;
        GameObject pivot = new GameObject("YardCabbage");
        pivot.transform.SetParent(transform, false);
        pivot.transform.position = foot;

        bool red = u < 0.38f;
        Color wrap = red
            ? new Color(0.42f, 0.18f, 0.38f, 1f)
            : new Color(0.28f, 0.48f, 0.22f, 1f);
        Color pale = red
            ? new Color(0.55f, 0.28f, 0.48f, 1f)
            : new Color(0.40f, 0.58f, 0.30f, 1f);
        Color heart = red
            ? new Color(0.82f, 0.72f, 0.78f, 1f)
            : new Color(0.72f, 0.82f, 0.48f, 1f);
        Color vein = red
            ? new Color(0.32f, 0.12f, 0.28f, 1f)
            : new Color(0.22f, 0.38f, 0.16f, 1f);

        AddCabbageHead(
            pivot.transform,
            -0.022f * side,
            0.072f + u * 0.008f,
            -10f * side,
            side,
            wrap,
            pale,
            heart,
            vein,
            false);
        AddCabbageHead(
            pivot.transform,
            0.010f * side,
            0.102f + u * 0.012f,
            4f * side,
            side,
            wrap,
            pale,
            heart,
            vein,
            true);
        return pivot.transform;
    }

    // Two gourds on one foot pivot so LateUpdate sway is one
    // rotation, not a Find or a per-rib array. Size is sprite.size,
    // not scale. Side view: a squat RIBBED ellipse sitting on the
    // dirt, a trailing vine with palmate leaves and a hooked
    // tendril — not a cabbage wrapper, not a tomato stake, not a
    // sunflower disk, not a mushroom cap. u < 0.38 paints a pale
    // cream gourd; the second gourd is a smaller sibling.
    private Transform MakePumpkin(Vector3 foot, float face, float u)
    {
        float side = face >= 0f ? 1f : -1f;
        GameObject pivot = new GameObject("YardPumpkin");
        pivot.transform.SetParent(transform, false);
        pivot.transform.position = foot;

        bool cream = u < 0.38f;
        Color vine = new Color(0.22f, 0.48f, 0.16f, 1f);
        Color leaf = new Color(0.16f, 0.40f, 0.12f, 1f);
        Color pale = new Color(0.30f, 0.54f, 0.18f, 1f);
        Color tendril = new Color(0.28f, 0.52f, 0.16f, 1f);
        Color rind = cream
            ? new Color(0.90f, 0.82f, 0.52f, 1f)
            : new Color(0.88f, 0.46f, 0.12f, 1f);
        Color rib = cream
            ? new Color(0.72f, 0.62f, 0.28f, 1f)
            : new Color(0.70f, 0.28f, 0.08f, 1f);
        Color blush = cream
            ? new Color(0.96f, 0.90f, 0.68f, 1f)
            : new Color(0.94f, 0.62f, 0.22f, 1f);
        Color stem = new Color(0.28f, 0.42f, 0.14f, 1f);

        AddPumpkinVine(
            pivot.transform,
            -0.018f * side,
            0.086f + u * 0.010f,
            -18f * side,
            side,
            vine,
            leaf,
            pale,
            tendril);
        AddPumpkinGourd(
            pivot.transform,
            0.012f * side,
            0.078f + u * 0.010f,
            6f * side,
            side,
            rind,
            rib,
            blush,
            stem,
            true);
        AddPumpkinGourd(
            pivot.transform,
            -0.048f * side,
            0.056f + u * 0.008f,
            -8f * side,
            side,
            rind,
            rib,
            blush,
            stem,
            false);
        return pivot.transform;
    }

    // Two stalks on one foot pivot so LateUpdate sway is one
    // rotation, not a Find or a per-leaf array. Size is sprite.size,
    // not scale. Side view: a TALL jointed stalk, long strap leaves,
    // a husked ear on the flank, and a wispy tassel — not a pumpkin
    // vine, not a cabbage wrapper, not a tomato stake, not a
    // sunflower disk, not a cattail sausage. u < 0.38 paints a
    // ripe gold ear; the second stalk is a shorter sibling.
    private Transform MakeCorn(Vector3 foot, float face, float u)
    {
        float side = face >= 0f ? 1f : -1f;
        GameObject pivot = new GameObject("YardCorn");
        pivot.transform.SetParent(transform, false);
        pivot.transform.position = foot;

        bool ripe = u < 0.38f;
        Color stalk = new Color(0.32f, 0.50f, 0.16f, 1f);
        Color leaf = new Color(0.20f, 0.46f, 0.14f, 1f);
        Color pale = new Color(0.34f, 0.56f, 0.18f, 1f);
        Color husk = ripe
            ? new Color(0.48f, 0.58f, 0.18f, 1f)
            : new Color(0.28f, 0.52f, 0.16f, 1f);
        Color kernel = ripe
            ? new Color(0.92f, 0.72f, 0.18f, 1f)
            : new Color(0.78f, 0.82f, 0.32f, 1f);
        Color silk = ripe
            ? new Color(0.86f, 0.70f, 0.28f, 1f)
            : new Color(0.62f, 0.72f, 0.28f, 1f);
        Color tassel = ripe
            ? new Color(0.78f, 0.62f, 0.22f, 1f)
            : new Color(0.42f, 0.56f, 0.18f, 1f);

        AddCornStalk(
            pivot.transform,
            -0.010f * side,
            0.428f + u * 0.028f,
            -5f * side,
            side,
            stalk,
            leaf,
            pale,
            husk,
            kernel,
            silk,
            tassel,
            true);
        AddCornStalk(
            pivot.transform,
            0.018f * side,
            0.312f + u * 0.020f,
            8f * side,
            side,
            stalk,
            leaf,
            pale,
            husk,
            kernel,
            silk,
            tassel,
            false);
        return pivot.transform;
    }

    // Two tapers on one foot pivot so LateUpdate sway is one
    // rotation, not a Find or a per-leaf array. Size is sprite.size,
    // not scale. Side view: a TAPERING orange root whose shoulder
    // sits on the slab and whose tip hangs into the dirt face,
    // plus a fountain of ferny greens — not a pumpkin gourd, not
    // a cabbage wrapper, not a tomato stake, not a corn tassel,
    // not a fern pinna, not a plantain oval. u < 0.38 paints a
    // ripe deep orange; the second plant is a shorter sibling.
    private Transform MakeCarrot(Vector3 foot, float face, float u)
    {
        float side = face >= 0f ? 1f : -1f;
        GameObject pivot = new GameObject("YardCarrot");
        pivot.transform.SetParent(transform, false);
        pivot.transform.position = foot;

        bool ripe = u < 0.38f;
        Color root = ripe
            ? new Color(0.90f, 0.42f, 0.12f, 1f)
            : new Color(0.86f, 0.62f, 0.28f, 1f);
        Color blush = ripe
            ? new Color(0.78f, 0.28f, 0.08f, 1f)
            : new Color(0.80f, 0.52f, 0.20f, 1f);
        Color leaf = new Color(0.20f, 0.50f, 0.16f, 1f);
        Color pale = new Color(0.32f, 0.58f, 0.20f, 1f);

        AddCarrotPlant(
            pivot.transform,
            -0.012f * side,
            0.118f + u * 0.016f,
            -6f * side,
            side,
            root,
            blush,
            leaf,
            pale,
            true);
        AddCarrotPlant(
            pivot.transform,
            0.022f * side,
            0.086f + u * 0.012f,
            10f * side,
            side,
            root,
            blush,
            leaf,
            pale,
            false);
        return pivot.transform;
    }

    // Two spears on one foot pivot so LateUpdate sway is one
    // rotation, not a Find or a per-leaf array. Size is sprite.size,
    // not scale. Side view: a BLANCHED cylindrical shaft standing
    // on the slab, a green collar, and a fan of upright strap
    // leaves — not a carrot cone in dirt, not a corn tassel, not
    // a tomato stake, not a cattail sausage, not a plantain oval.
    // u < 0.38 paints a thicker harvest-white shaft; the second
    // plant is a shorter sibling.
    private Transform MakeLeek(Vector3 foot, float face, float u)
    {
        float side = face >= 0f ? 1f : -1f;
        GameObject pivot = new GameObject("YardLeek");
        pivot.transform.SetParent(transform, false);
        pivot.transform.position = foot;

        bool ripe = u < 0.38f;
        Color shaft = ripe
            ? new Color(0.94f, 0.92f, 0.82f, 1f)
            : new Color(0.88f, 0.90f, 0.78f, 1f);
        Color collar = ripe
            ? new Color(0.42f, 0.58f, 0.22f, 1f)
            : new Color(0.50f, 0.64f, 0.28f, 1f);
        Color leaf = ripe
            ? new Color(0.22f, 0.50f, 0.16f, 1f)
            : new Color(0.30f, 0.56f, 0.20f, 1f);
        Color pale = ripe
            ? new Color(0.34f, 0.58f, 0.20f, 1f)
            : new Color(0.40f, 0.62f, 0.24f, 1f);

        AddLeekPlant(
            pivot.transform,
            -0.010f * side,
            0.236f + u * 0.018f,
            -4f * side,
            side,
            shaft,
            collar,
            leaf,
            pale,
            true);
        AddLeekPlant(
            pivot.transform,
            0.020f * side,
            0.172f + u * 0.014f,
            8f * side,
            side,
            shaft,
            collar,
            leaf,
            pale,
            false);
        return pivot.transform;
    }

    // Two vines on one foot pivot so LateUpdate sway is one
    // rotation, not a Find or a per-stem array. Size is sprite.size,
    // not scale. Side view: one wooden pole, twining trifoliate
    // leaves, long hanging pods — not a tomato stake with round
    // red fruit, not a leek spear, not a corn tassel, not a
    // cattail sausage, not a carrot cone. u < 0.38 paints plump
    // green / one purple pod; the second vine is a shorter sibling.
    private Transform MakeBean(Vector3 foot, float face, float u)
    {
        float side = face >= 0f ? 1f : -1f;
        GameObject pivot = new GameObject("YardBean");
        pivot.transform.SetParent(transform, false);
        pivot.transform.position = foot;

        float stakeH = 0.348f + u * 0.024f;
        Color stake = new Color(0.40f, 0.26f, 0.14f, 1f);
        Color vine = new Color(0.20f, 0.48f, 0.16f, 1f);
        Color leaf = new Color(0.18f, 0.46f, 0.14f, 1f);
        Color pale = new Color(0.32f, 0.58f, 0.20f, 1f);
        bool ripe = u < 0.38f;
        Color pod = ripe
            ? new Color(0.28f, 0.54f, 0.16f, 1f)
            : new Color(0.70f, 0.72f, 0.28f, 1f);
        Color plump = ripe
            ? new Color(0.44f, 0.22f, 0.50f, 1f)
            : new Color(0.52f, 0.64f, 0.22f, 1f);
        Color bloom = new Color(0.90f, 0.84f, 0.96f, 1f);

        AddHousePart(
            pivot.transform,
            "Stake",
            new Vector3(0f, stakeH * 0.5f, 0f),
            new Vector2(0.010f, stakeH),
            0f,
            stake,
            BeanOrder,
            HumanVisualShape.Box);

        AddBeanVine(
            pivot.transform,
            -0.008f * side,
            0.292f + u * 0.016f,
            -5f * side,
            side,
            vine,
            leaf,
            pale,
            pod,
            plump,
            bloom,
            true);
        AddBeanVine(
            pivot.transform,
            0.012f * side,
            0.228f + u * 0.012f,
            7f * side,
            side,
            vine,
            leaf,
            pale,
            pod,
            plump,
            bloom,
            ripe);
        return pivot.transform;
    }

    // Two globes on one foot pivot so LateUpdate sway is one
    // rotation, not a Find or a per-tube array. Size is
    // sprite.size, not scale. Side view: a ROUND papery bulb
    // sitting in the dirt face, a short neck, hollow tubular
    // greens — not a carrot cone, not a leek spear, not a
    // bean pole with hanging pods, not a cabbage wrapper.
    // u < 0.38 paints a ripe gold / one red-skin globe; the
    // second plant is a shorter sibling.
    private Transform MakeOnion(Vector3 foot, float face, float u)
    {
        float side = face >= 0f ? 1f : -1f;
        GameObject pivot = new GameObject("YardOnion");
        pivot.transform.SetParent(transform, false);
        pivot.transform.position = foot;

        bool ripe = u < 0.38f;
        Color bulb = ripe
            ? new Color(0.78f, 0.58f, 0.18f, 1f)
            : new Color(0.86f, 0.88f, 0.70f, 1f);
        Color blush = ripe
            ? new Color(0.62f, 0.24f, 0.28f, 1f)
            : new Color(0.72f, 0.78f, 0.48f, 1f);
        Color skin = ripe
            ? new Color(0.90f, 0.72f, 0.32f, 1f)
            : new Color(0.92f, 0.92f, 0.78f, 1f);
        Color leaf = new Color(0.26f, 0.50f, 0.24f, 1f);
        Color pale = new Color(0.36f, 0.58f, 0.30f, 1f);

        AddOnionPlant(
            pivot.transform,
            -0.012f * side,
            0.108f + u * 0.014f,
            -5f * side,
            side,
            ripe ? blush : bulb,
            skin,
            leaf,
            pale,
            true);
        AddOnionPlant(
            pivot.transform,
            0.020f * side,
            0.078f + u * 0.010f,
            8f * side,
            side,
            bulb,
            skin,
            leaf,
            pale,
            false);
        return pivot.transform;
    }

    // Side view: a CLUSTER of pointed cloves under a papery
    // sack, not one round onion globe, not a leek spear, not
    // a bean pole. u < 0.38 paints a purple-blushed wrapper;
    // the second plant is a shorter sibling.
    private Transform MakeGarlic(Vector3 foot, float face, float u)
    {
        float side = face >= 0f ? 1f : -1f;
        GameObject pivot = new GameObject("YardGarlic");
        pivot.transform.SetParent(transform, false);
        pivot.transform.position = foot;

        bool ripe = u < 0.38f;
        Color clove = ripe
            ? new Color(0.90f, 0.86f, 0.76f, 1f)
            : new Color(0.94f, 0.92f, 0.82f, 1f);
        Color paper = ripe
            ? new Color(0.78f, 0.62f, 0.78f, 1f)
            : new Color(0.90f, 0.88f, 0.74f, 1f);
        Color blush = ripe
            ? new Color(0.58f, 0.34f, 0.58f, 1f)
            : new Color(0.82f, 0.80f, 0.62f, 1f);
        Color leaf = new Color(0.28f, 0.52f, 0.26f, 1f);
        Color pale = new Color(0.40f, 0.60f, 0.32f, 1f);

        AddGarlicPlant(
            pivot.transform,
            -0.010f * side,
            0.092f + u * 0.012f,
            -4f * side,
            side,
            clove,
            paper,
            blush,
            leaf,
            pale,
            true);
        AddGarlicPlant(
            pivot.transform,
            0.018f * side,
            0.068f + u * 0.008f,
            9f * side,
            side,
            clove,
            paper,
            blush,
            leaf,
            pale,
            false);
        return pivot.transform;
    }

    // Two globes on one foot pivot so LateUpdate sway is one
    // rotation, not a Find or a per-lobe array. Size is
    // sprite.size, not scale. Side view: a SMALL ROUND RED
    // root sitting in the dirt face with a WHITE tip and
    // lobed greens — not a garlic clove bunch, not an onion
    // papery globe, not a carrot taper hanging into the
    // face. u < 0.38 paints a ripe crimson; the second
    // plant is a shorter sibling.
    private Transform MakeRadish(Vector3 foot, float face, float u)
    {
        float side = face >= 0f ? 1f : -1f;
        GameObject pivot = new GameObject("YardRadish");
        pivot.transform.SetParent(transform, false);
        pivot.transform.position = foot;

        bool ripe = u < 0.38f;
        Color bulb = ripe
            ? new Color(0.78f, 0.16f, 0.18f, 1f)
            : new Color(0.86f, 0.48f, 0.50f, 1f);
        Color blush = ripe
            ? new Color(0.92f, 0.42f, 0.44f, 1f)
            : new Color(0.90f, 0.66f, 0.64f, 1f);
        Color tip = ripe
            ? new Color(0.94f, 0.92f, 0.86f, 1f)
            : new Color(0.90f, 0.86f, 0.78f, 1f);
        Color leaf = new Color(0.22f, 0.48f, 0.20f, 1f);
        Color pale = new Color(0.34f, 0.58f, 0.26f, 1f);

        AddRadishPlant(
            pivot.transform,
            -0.010f * side,
            0.072f + u * 0.010f,
            -5f * side,
            side,
            bulb,
            blush,
            tip,
            leaf,
            pale,
            true);
        AddRadishPlant(
            pivot.transform,
            0.016f * side,
            0.054f + u * 0.008f,
            8f * side,
            side,
            bulb,
            blush,
            tip,
            leaf,
            pale,
            false);
        return pivot.transform;
    }

    // Two tubers on one foot pivot so LateUpdate sway is one
    // rotation, not a Find or a per-lump array. Size is
    // sprite.size, not scale. Side view: an IRREGULAR brown
    // tuber sitting in the dirt face with EYES and a short
    // sprout — not a radish round red globule, not an onion
    // papery globe, not a carrot taper hanging into the
    // face. u < 0.38 paints a russet; the second plant is a
    // shorter sibling.
    private Transform MakePotato(Vector3 foot, float face, float u)
    {
        float side = face >= 0f ? 1f : -1f;
        GameObject pivot = new GameObject("YardPotato");
        pivot.transform.SetParent(transform, false);
        pivot.transform.position = foot;

        bool russet = u < 0.38f;
        Color skin = russet
            ? new Color(0.52f, 0.34f, 0.18f, 1f)
            : new Color(0.82f, 0.72f, 0.48f, 1f);
        Color lump = russet
            ? new Color(0.62f, 0.42f, 0.24f, 1f)
            : new Color(0.88f, 0.80f, 0.58f, 1f);
        Color pit = russet
            ? new Color(0.28f, 0.16f, 0.10f, 1f)
            : new Color(0.46f, 0.36f, 0.22f, 1f);
        Color sprout = new Color(0.62f, 0.72f, 0.36f, 1f);
        Color leaf = new Color(0.24f, 0.50f, 0.22f, 1f);
        Color pale = new Color(0.36f, 0.60f, 0.28f, 1f);

        AddPotatoPlant(
            pivot.transform,
            -0.012f * side,
            0.068f + u * 0.010f,
            -4f * side,
            side,
            skin,
            lump,
            pit,
            sprout,
            leaf,
            pale,
            true);
        AddPotatoPlant(
            pivot.transform,
            0.018f * side,
            0.050f + u * 0.008f,
            9f * side,
            side,
            skin,
            lump,
            pit,
            sprout,
            leaf,
            pale,
            false);
        return pivot.transform;
    }

    // Two crowns on one foot pivot so LateUpdate sway is one
    // rotation, not a Find or a per-leaf array. Size is
    // sprite.size, not scale. Side view: a ROUND PURPLE
    // root sitting in the dirt face with RED petioles and
    // veined ovals — not a potato kidney, not a radish
    // crimson globule with a white tip, not an onion
    // papery globe. u < 0.38 paints a ripe burgundy; the
    // second plant is a shorter sibling.
    private Transform MakeBeet(Vector3 foot, float face, float u)
    {
        float side = face >= 0f ? 1f : -1f;
        GameObject pivot = new GameObject("YardBeet");
        pivot.transform.SetParent(transform, false);
        pivot.transform.position = foot;

        bool ripe = u < 0.38f;
        Color skin = ripe
            ? new Color(0.46f, 0.08f, 0.28f, 1f)
            : new Color(0.82f, 0.58f, 0.18f, 1f);
        Color shoulder = ripe
            ? new Color(0.62f, 0.16f, 0.38f, 1f)
            : new Color(0.90f, 0.70f, 0.32f, 1f);
        Color tap = ripe
            ? new Color(0.28f, 0.06f, 0.18f, 1f)
            : new Color(0.58f, 0.36f, 0.10f, 1f);
        Color vein = new Color(0.72f, 0.12f, 0.18f, 1f);
        Color leaf = new Color(0.22f, 0.48f, 0.20f, 1f);
        Color pale = new Color(0.34f, 0.58f, 0.26f, 1f);

        AddBeetPlant(
            pivot.transform,
            -0.012f * side,
            0.086f + u * 0.010f,
            -5f * side,
            side,
            skin,
            shoulder,
            tap,
            vein,
            leaf,
            pale,
            true);
        AddBeetPlant(
            pivot.transform,
            0.018f * side,
            0.064f + u * 0.008f,
            8f * side,
            side,
            skin,
            shoulder,
            tap,
            vein,
            leaf,
            pale,
            false);
        return pivot.transform;
    }

    // Two cones on one foot pivot so LateUpdate sway is one
    // rotation, not a Find or a per-leaf array. Size is
    // sprite.size, not scale. Side view: a CREAMY WIDE
    // cone sitting in the dirt face with celery-like
    // pinnate leaflets — not a carrot orange taper, not
    // a beet purple crown, not a potato kidney. u < 0.38
    // paints a ripe ivory; the second plant is a shorter
    // sibling.
    private Transform MakeParsnip(Vector3 foot, float face, float u)
    {
        float side = face >= 0f ? 1f : -1f;
        GameObject pivot = new GameObject("YardParsnip");
        pivot.transform.SetParent(transform, false);
        pivot.transform.position = foot;

        bool ripe = u < 0.38f;
        Color root = ripe
            ? new Color(0.93f, 0.86f, 0.58f, 1f)
            : new Color(0.90f, 0.88f, 0.70f, 1f);
        Color shoulder = ripe
            ? new Color(0.78f, 0.66f, 0.38f, 1f)
            : new Color(0.84f, 0.78f, 0.52f, 1f);
        Color leaf = new Color(0.24f, 0.52f, 0.18f, 1f);
        Color pale = new Color(0.36f, 0.60f, 0.22f, 1f);

        AddParsnipPlant(
            pivot.transform,
            -0.012f * side,
            0.112f + u * 0.014f,
            -5f * side,
            side,
            root,
            shoulder,
            leaf,
            pale,
            true);
        AddParsnipPlant(
            pivot.transform,
            0.020f * side,
            0.082f + u * 0.010f,
            9f * side,
            side,
            root,
            shoulder,
            leaf,
            pale,
            false);
        return pivot.transform;
    }

    // Two globes on one foot pivot so LateUpdate sway is one
    // rotation, not a Find or a per-lobe array. Size is
    // sprite.size, not scale. Side view: a ROUND WHITE
    // root sitting in the dirt face with a PURPLE shoulder
    // and lobed greens — not a parsnip creamy cone, not a
    // beet purple crown, not a carrot orange taper. u <
    // 0.38 paints a ripe ivory with a purple cap; the
    // second plant is a shorter sibling.
    private Transform MakeTurnip(Vector3 foot, float face, float u)
    {
        float side = face >= 0f ? 1f : -1f;
        GameObject pivot = new GameObject("YardTurnip");
        pivot.transform.SetParent(transform, false);
        pivot.transform.position = foot;

        bool ripe = u < 0.38f;
        Color globe = ripe
            ? new Color(0.94f, 0.92f, 0.82f, 1f)
            : new Color(0.90f, 0.86f, 0.68f, 1f);
        Color shoulder = ripe
            ? new Color(0.52f, 0.18f, 0.42f, 1f)
            : new Color(0.70f, 0.48f, 0.58f, 1f);
        Color tap = ripe
            ? new Color(0.88f, 0.84f, 0.70f, 1f)
            : new Color(0.82f, 0.78f, 0.58f, 1f);
        Color leaf = new Color(0.22f, 0.50f, 0.18f, 1f);
        Color pale = new Color(0.34f, 0.58f, 0.24f, 1f);

        AddTurnipPlant(
            pivot.transform,
            -0.012f * side,
            0.090f + u * 0.012f,
            -5f * side,
            side,
            globe,
            shoulder,
            tap,
            leaf,
            pale,
            true);
        AddTurnipPlant(
            pivot.transform,
            0.020f * side,
            0.066f + u * 0.010f,
            9f * side,
            side,
            globe,
            shoulder,
            tap,
            leaf,
            pale,
            false);
        return pivot.transform;
    }

    // Two globes on one foot pivot so LateUpdate sway is one
    // rotation, not a Find or a per-paddle array. Size is
    // sprite.size, not scale. Side view: a LARGER YELLOW
    // root sitting in the dirt face with a PURPLE cap
    // and glaucous collard greens — not a turnip white
    // globe, not a parsnip creamy cone, not a beet purple
    // crown. u < 0.38 paints a ripe gold with a bronze
    // cap; the second plant is a shorter sibling.
    private Transform MakeRutabaga(Vector3 foot, float face, float u)
    {
        float side = face >= 0f ? 1f : -1f;
        GameObject pivot = new GameObject("YardRutabaga");
        pivot.transform.SetParent(transform, false);
        pivot.transform.position = foot;

        bool ripe = u < 0.38f;
        Color globe = ripe
            ? new Color(0.90f, 0.74f, 0.22f, 1f)
            : new Color(0.88f, 0.82f, 0.42f, 1f);
        Color shoulder = ripe
            ? new Color(0.42f, 0.12f, 0.32f, 1f)
            : new Color(0.62f, 0.36f, 0.40f, 1f);
        Color tap = ripe
            ? new Color(0.82f, 0.62f, 0.18f, 1f)
            : new Color(0.80f, 0.72f, 0.36f, 1f);
        Color leaf = new Color(0.16f, 0.42f, 0.28f, 1f);
        Color pale = new Color(0.28f, 0.54f, 0.30f, 1f);

        AddRutabagaPlant(
            pivot.transform,
            -0.014f * side,
            0.100f + u * 0.012f,
            -4f * side,
            side,
            globe,
            shoulder,
            tap,
            leaf,
            pale,
            true);
        AddRutabagaPlant(
            pivot.transform,
            0.024f * side,
            0.074f + u * 0.010f,
            8f * side,
            side,
            globe,
            shoulder,
            tap,
            leaf,
            pale,
            false);
        return pivot.transform;
    }

    // Two stems on one foot pivot so LateUpdate sway is one
    // rotation, not a Find or a per-blade array. Size is
    // sprite.size, not scale. Side view: a PALE-GREEN
    // swollen STEM sitting ON the slab with long petioles
    // from the crown — not a rutabaga yellow globe in dirt,
    // not a turnip white globe, not a beet purple crown, not
    // a cabbage wrapper. u < 0.38 paints a ripe apple-green;
    // the second plant is a shorter sibling.
    private Transform MakeKohlrabi(Vector3 foot, float face, float u)
    {
        float side = face >= 0f ? 1f : -1f;
        GameObject pivot = new GameObject("YardKohlrabi");
        pivot.transform.SetParent(transform, false);
        pivot.transform.position = foot;

        bool ripe = u < 0.38f;
        Color stem = ripe
            ? new Color(0.48f, 0.78f, 0.36f, 1f)
            : new Color(0.70f, 0.86f, 0.52f, 1f);
        Color blush = ripe
            ? new Color(0.68f, 0.88f, 0.50f, 1f)
            : new Color(0.84f, 0.92f, 0.66f, 1f);
        Color collar = ripe
            ? new Color(0.32f, 0.52f, 0.22f, 1f)
            : new Color(0.48f, 0.64f, 0.32f, 1f);
        Color leaf = new Color(0.18f, 0.48f, 0.26f, 1f);
        Color pale = new Color(0.36f, 0.64f, 0.32f, 1f);

        AddKohlrabiPlant(
            pivot.transform,
            -0.014f * side,
            0.118f + u * 0.012f,
            -4f * side,
            side,
            stem,
            blush,
            collar,
            leaf,
            pale,
            true);
        AddKohlrabiPlant(
            pivot.transform,
            0.024f * side,
            0.086f + u * 0.010f,
            8f * side,
            side,
            stem,
            blush,
            collar,
            leaf,
            pale,
            false);
        return pivot.transform;
    }

    // Two bunches on one foot pivot so LateUpdate sway is
    // one rotation, not a Find or a per-stalk array. Size
    // is sprite.size, not scale. Side view: RIBBED pale
    // petioles sitting ON the slab with compound leaflets
    // at the crown — not a kohlrabi swollen stem, not a
    // leek blanched spear, not a cabbage wrapper. u < 0.38
    // paints a ripe chartreuse; the second plant is a
    // shorter sibling.
    private Transform MakeCelery(Vector3 foot, float face, float u)
    {
        float side = face >= 0f ? 1f : -1f;
        GameObject pivot = new GameObject("YardCelery");
        pivot.transform.SetParent(transform, false);
        pivot.transform.position = foot;

        bool ripe = u < 0.38f;
        Color stalk = ripe
            ? new Color(0.62f, 0.82f, 0.38f, 1f)
            : new Color(0.78f, 0.90f, 0.54f, 1f);
        Color rib = ripe
            ? new Color(0.42f, 0.64f, 0.24f, 1f)
            : new Color(0.58f, 0.76f, 0.38f, 1f);
        Color heart = ripe
            ? new Color(0.86f, 0.92f, 0.62f, 1f)
            : new Color(0.92f, 0.94f, 0.72f, 1f);
        Color leaf = new Color(0.20f, 0.50f, 0.22f, 1f);
        Color pale = new Color(0.36f, 0.66f, 0.30f, 1f);

        AddCeleryPlant(
            pivot.transform,
            -0.012f * side,
            0.126f + u * 0.012f,
            -4f * side,
            side,
            stalk,
            rib,
            heart,
            leaf,
            pale,
            true);
        AddCeleryPlant(
            pivot.transform,
            0.022f * side,
            0.092f + u * 0.010f,
            8f * side,
            side,
            stalk,
            rib,
            heart,
            leaf,
            pale,
            false);
        return pivot.transform;
    }

    // Two plants on one foot pivot so LateUpdate sway is
    // one rotation, not a Find or a per-stalk array. Size
    // is sprite.size, not scale. Side view: SWOLLEN PALE
    // layered bulb sitting ON the slab with a dill-like
    // feathery crown — not celery ribbed stalks, not a
    // leek blanched spear, not a kohlrabi apple-green
    // round stem. u < 0.38 paints a ripe ivory; the
    // second plant is a shorter sibling.
    private Transform MakeFennel(Vector3 foot, float face, float u)
    {
        float side = face >= 0f ? 1f : -1f;
        GameObject pivot = new GameObject("YardFennel");
        pivot.transform.SetParent(transform, false);
        pivot.transform.position = foot;

        bool ripe = u < 0.38f;
        Color bulb = ripe
            ? new Color(0.92f, 0.90f, 0.76f, 1f)
            : new Color(0.88f, 0.90f, 0.80f, 1f);
        Color layer = ripe
            ? new Color(0.84f, 0.88f, 0.66f, 1f)
            : new Color(0.80f, 0.86f, 0.70f, 1f);
        Color blush = ripe
            ? new Color(0.70f, 0.82f, 0.48f, 1f)
            : new Color(0.76f, 0.86f, 0.56f, 1f);
        Color stem = ripe
            ? new Color(0.52f, 0.70f, 0.32f, 1f)
            : new Color(0.60f, 0.76f, 0.40f, 1f);
        Color frond = new Color(0.28f, 0.58f, 0.22f, 1f);
        Color pale = new Color(0.42f, 0.68f, 0.30f, 1f);

        AddFennelPlant(
            pivot.transform,
            -0.012f * side,
            0.118f + u * 0.012f,
            -4f * side,
            side,
            bulb,
            layer,
            blush,
            stem,
            frond,
            pale,
            true);
        AddFennelPlant(
            pivot.transform,
            0.022f * side,
            0.086f + u * 0.010f,
            8f * side,
            side,
            bulb,
            layer,
            blush,
            stem,
            frond,
            pale,
            false);
        return pivot.transform;
    }

    // Two plants on one foot pivot so LateUpdate sway is
    // one rotation, not a Find or a per-spear array. Size
    // is sprite.size, not scale. Side view: THIN GREEN
    // spears from a SCALY crown sitting ON the slab — not
    // a fennel pale layered bulb, not a leek blanched
    // shaft with a leaf fan, not celery ribbed stalks.
    // u < 0.38 paints a ripe green with a purple tip; the
    // second plant is a shorter sibling.
    private Transform MakeAsparagus(Vector3 foot, float face, float u)
    {
        float side = face >= 0f ? 1f : -1f;
        GameObject pivot = new GameObject("YardAsparagus");
        pivot.transform.SetParent(transform, false);
        pivot.transform.position = foot;

        bool ripe = u < 0.38f;
        Color crown = ripe
            ? new Color(0.46f, 0.36f, 0.22f, 1f)
            : new Color(0.52f, 0.42f, 0.26f, 1f);
        Color scale = ripe
            ? new Color(0.38f, 0.30f, 0.18f, 1f)
            : new Color(0.44f, 0.36f, 0.22f, 1f);
        Color spear = ripe
            ? new Color(0.28f, 0.58f, 0.22f, 1f)
            : new Color(0.48f, 0.72f, 0.32f, 1f);
        Color pale = ripe
            ? new Color(0.40f, 0.68f, 0.28f, 1f)
            : new Color(0.58f, 0.78f, 0.38f, 1f);
        Color tip = ripe
            ? new Color(0.48f, 0.28f, 0.42f, 1f)
            : new Color(0.36f, 0.62f, 0.26f, 1f);

        AddAsparagusPlant(
            pivot.transform,
            -0.010f * side,
            0.148f + u * 0.014f,
            -4f * side,
            side,
            crown,
            scale,
            spear,
            pale,
            tip,
            true);
        AddAsparagusPlant(
            pivot.transform,
            0.020f * side,
            0.108f + u * 0.010f,
            8f * side,
            side,
            crown,
            scale,
            spear,
            pale,
            tip,
            false);
        return pivot.transform;
    }

    // Two plants on one foot pivot so LateUpdate sway is
    // one rotation, not a Find or a per-stalk array. Size
    // is sprite.size, not scale. Side view: THICK RED
    // stalks with a HUGE wrinkled leaf sitting ON the
    // slab — not thin green asparagus spears, not a leek
    // blanched shaft with a leaf fan, not a fennel pale
    // layered bulb. u < 0.38 paints a ripe crimson; the
    // second plant is a shorter sibling.
    private Transform MakeRhubarb(Vector3 foot, float face, float u)
    {
        float side = face >= 0f ? 1f : -1f;
        GameObject pivot = new GameObject("YardRhubarb");
        pivot.transform.SetParent(transform, false);
        pivot.transform.position = foot;

        bool ripe = u < 0.38f;
        Color crown = ripe
            ? new Color(0.42f, 0.26f, 0.16f, 1f)
            : new Color(0.48f, 0.32f, 0.20f, 1f);
        Color stalk = ripe
            ? new Color(0.72f, 0.16f, 0.20f, 1f)
            : new Color(0.78f, 0.42f, 0.36f, 1f);
        Color groove = ripe
            ? new Color(0.50f, 0.10f, 0.14f, 1f)
            : new Color(0.58f, 0.28f, 0.24f, 1f);
        Color blush = ripe
            ? new Color(0.62f, 0.38f, 0.22f, 1f)
            : new Color(0.68f, 0.52f, 0.32f, 1f);
        Color leaf = ripe
            ? new Color(0.20f, 0.46f, 0.16f, 1f)
            : new Color(0.30f, 0.56f, 0.22f, 1f);
        Color pale = ripe
            ? new Color(0.28f, 0.54f, 0.20f, 1f)
            : new Color(0.38f, 0.64f, 0.28f, 1f);

        AddRhubarbPlant(
            pivot.transform,
            -0.012f * side,
            0.092f + u * 0.010f,
            -5f * side,
            side,
            crown,
            stalk,
            groove,
            blush,
            leaf,
            pale,
            true);
        AddRhubarbPlant(
            pivot.transform,
            0.022f * side,
            0.068f + u * 0.008f,
            9f * side,
            side,
            crown,
            stalk,
            groove,
            blush,
            leaf,
            pale,
            false);
        return pivot.transform;
    }

    // Two plants on one foot pivot so LateUpdate sway is
    // one rotation, not a Find or a per-leaf array. Size
    // is sprite.size, not scale. Side view: CORRUGATED
    // BLUE-GREEN standing leaves in a ROSETTE sitting ON
    // the slab — not a cabbage round wrapper head, not
    // rhubarb thick red stalks with a huge wrinkled
    // leaf, not asparagus thin green spears. u < 0.38
    // paints a ripe Lacinato teal; the second plant is a
    // shorter sibling.
    private Transform MakeKale(Vector3 foot, float face, float u)
    {
        float side = face >= 0f ? 1f : -1f;
        GameObject pivot = new GameObject("YardKale");
        pivot.transform.SetParent(transform, false);
        pivot.transform.position = foot;

        bool ripe = u < 0.38f;
        Color stem = ripe
            ? new Color(0.28f, 0.36f, 0.20f, 1f)
            : new Color(0.34f, 0.42f, 0.24f, 1f);
        Color leaf = ripe
            ? new Color(0.14f, 0.36f, 0.30f, 1f)
            : new Color(0.22f, 0.50f, 0.32f, 1f);
        Color ruffle = ripe
            ? new Color(0.22f, 0.46f, 0.36f, 1f)
            : new Color(0.34f, 0.60f, 0.38f, 1f);
        Color rib = ripe
            ? new Color(0.78f, 0.82f, 0.70f, 1f)
            : new Color(0.84f, 0.88f, 0.74f, 1f);

        AddKalePlant(
            pivot.transform,
            -0.012f * side,
            0.088f + u * 0.010f,
            -6f * side,
            side,
            stem,
            leaf,
            ruffle,
            rib,
            true);
        AddKalePlant(
            pivot.transform,
            0.022f * side,
            0.064f + u * 0.008f,
            10f * side,
            side,
            stem,
            leaf,
            ruffle,
            rib,
            false);
        return pivot.transform;
    }

    // Two plants on one foot pivot so LateUpdate sway is
    // one rotation, not a Find or a per-leaf array. Size
    // is sprite.size, not scale. Side view: BRIGHT stalks
    // with a LOBED leaf sitting ON the slab — not a kale
    // standing blue-green rosette, not rhubarb thick red
    // stalks with a huge wrinkled paddle, not a cabbage
    // round wrapper head. u < 0.38 paints ripe crimson
    // petioles; the second plant is a shorter sibling.
    private Transform MakeChard(Vector3 foot, float face, float u)
    {
        float side = face >= 0f ? 1f : -1f;
        GameObject pivot = new GameObject("YardChard");
        pivot.transform.SetParent(transform, false);
        pivot.transform.position = foot;

        bool ripe = u < 0.38f;
        Color crown = ripe
            ? new Color(0.22f, 0.28f, 0.14f, 1f)
            : new Color(0.28f, 0.34f, 0.16f, 1f);
        Color stalk = ripe
            ? new Color(0.74f, 0.14f, 0.16f, 1f)
            : new Color(0.86f, 0.62f, 0.12f, 1f);
        Color groove = ripe
            ? new Color(0.90f, 0.32f, 0.28f, 1f)
            : new Color(0.94f, 0.78f, 0.28f, 1f);
        Color leaf = ripe
            ? new Color(0.16f, 0.38f, 0.16f, 1f)
            : new Color(0.24f, 0.48f, 0.18f, 1f);
        Color pale = ripe
            ? new Color(0.22f, 0.48f, 0.20f, 1f)
            : new Color(0.32f, 0.58f, 0.24f, 1f);

        AddChardPlant(
            pivot.transform,
            -0.012f * side,
            0.086f + u * 0.010f,
            -5f * side,
            side,
            crown,
            stalk,
            groove,
            leaf,
            pale,
            true);
        AddChardPlant(
            pivot.transform,
            0.022f * side,
            0.062f + u * 0.008f,
            9f * side,
            side,
            crown,
            stalk,
            groove,
            leaf,
            pale,
            false);
        return pivot.transform;
    }

    // Two plants on one foot pivot so LateUpdate sway is
    // one rotation, not a Find or a per-leaf array. Size
    // is sprite.size, not scale. Side view: SMOOTH DARK
    // leaves in a LOW rosette sitting ON the slab — not
    // chard bright stalks with a lobed paddle, not a kale
    // standing corrugated blue-green rosette, not a
    // cabbage round wrapper head. u < 0.38 paints ripe
    // Bloomsdale-dark; the second plant is a shorter
    // sibling.
    private Transform MakeSpinach(Vector3 foot, float face, float u)
    {
        float side = face >= 0f ? 1f : -1f;
        GameObject pivot = new GameObject("YardSpinach");
        pivot.transform.SetParent(transform, false);
        pivot.transform.position = foot;

        bool ripe = u < 0.38f;
        Color crown = ripe
            ? new Color(0.20f, 0.28f, 0.12f, 1f)
            : new Color(0.24f, 0.34f, 0.14f, 1f);
        Color petiole = ripe
            ? new Color(0.42f, 0.52f, 0.28f, 1f)
            : new Color(0.50f, 0.60f, 0.32f, 1f);
        Color leaf = ripe
            ? new Color(0.08f, 0.28f, 0.12f, 1f)
            : new Color(0.16f, 0.42f, 0.18f, 1f);
        Color vein = ripe
            ? new Color(0.14f, 0.36f, 0.16f, 1f)
            : new Color(0.22f, 0.50f, 0.22f, 1f);

        AddSpinachPlant(
            pivot.transform,
            -0.010f * side,
            0.052f + u * 0.008f,
            -4f * side,
            side,
            crown,
            petiole,
            leaf,
            vein,
            true);
        AddSpinachPlant(
            pivot.transform,
            0.018f * side,
            0.038f + u * 0.006f,
            8f * side,
            side,
            crown,
            petiole,
            leaf,
            vein,
            false);
        return pivot.transform;
    }

    // Two heads on one foot pivot so LateUpdate sway is
    // one rotation, not a Find or a per-leaf array. Size
    // is sprite.size, not scale. Side view: a LOOSE PALE
    // cup sitting ON the slab — not a spinach dark low
    // pancake, not a cabbage tight wrap ball, not a kale
    // standing corrugated blue-green rosette. u < 0.38
    // paints ripe butterhead cream-green; the second
    // head is a shorter sibling.
    private Transform MakeLettuce(Vector3 foot, float face, float u)
    {
        float side = face >= 0f ? 1f : -1f;
        GameObject pivot = new GameObject("YardLettuce");
        pivot.transform.SetParent(transform, false);
        pivot.transform.position = foot;

        bool ripe = u < 0.38f;
        Color crown = ripe
            ? new Color(0.62f, 0.72f, 0.38f, 1f)
            : new Color(0.48f, 0.62f, 0.28f, 1f);
        Color leaf = ripe
            ? new Color(0.58f, 0.78f, 0.38f, 1f)
            : new Color(0.42f, 0.68f, 0.28f, 1f);
        Color pale = ripe
            ? new Color(0.78f, 0.88f, 0.52f, 1f)
            : new Color(0.62f, 0.82f, 0.42f, 1f);
        Color heart = ripe
            ? new Color(0.86f, 0.92f, 0.58f, 1f)
            : new Color(0.72f, 0.86f, 0.48f, 1f);
        Color rib = ripe
            ? new Color(0.70f, 0.82f, 0.44f, 1f)
            : new Color(0.52f, 0.72f, 0.34f, 1f);

        AddLettuceHead(
            pivot.transform,
            -0.012f * side,
            0.078f + u * 0.010f,
            -6f * side,
            side,
            crown,
            leaf,
            pale,
            heart,
            rib,
            true);
        AddLettuceHead(
            pivot.transform,
            0.020f * side,
            0.058f + u * 0.008f,
            10f * side,
            side,
            crown,
            leaf,
            pale,
            heart,
            rib,
            false);
        return pivot.transform;
    }

    // Two heads on one foot pivot so LateUpdate sway is
    // one rotation, not a Find or a per-floret array.
    // Size is sprite.size, not scale. Side view: a
    // TIGHT GREEN floret head on a THICK stem sitting
    // ON the slab — not a lettuce loose pale cup, not
    // a cabbage tight wrap ball, not a spinach dark
    // low pancake. u < 0.38 paints ripe Calabrese
    // forest-green; the second head is a shorter
    // sibling.
    private Transform MakeBroccoli(Vector3 foot, float face, float u)
    {
        float side = face >= 0f ? 1f : -1f;
        GameObject pivot = new GameObject("YardBroccoli");
        pivot.transform.SetParent(transform, false);
        pivot.transform.position = foot;

        bool ripe = u < 0.38f;
        Color crown = ripe
            ? new Color(0.32f, 0.36f, 0.18f, 1f)
            : new Color(0.38f, 0.42f, 0.20f, 1f);
        Color stem = ripe
            ? new Color(0.48f, 0.58f, 0.30f, 1f)
            : new Color(0.56f, 0.68f, 0.36f, 1f);
        Color groove = ripe
            ? new Color(0.38f, 0.48f, 0.22f, 1f)
            : new Color(0.46f, 0.58f, 0.26f, 1f);
        Color leaf = ripe
            ? new Color(0.22f, 0.46f, 0.18f, 1f)
            : new Color(0.32f, 0.56f, 0.22f, 1f);
        Color head = ripe
            ? new Color(0.16f, 0.38f, 0.16f, 1f)
            : new Color(0.28f, 0.52f, 0.20f, 1f);
        Color bead = ripe
            ? new Color(0.22f, 0.44f, 0.18f, 1f)
            : new Color(0.36f, 0.58f, 0.24f, 1f);

        AddBroccoliHead(
            pivot.transform,
            -0.012f * side,
            0.112f + u * 0.012f,
            -5f * side,
            side,
            crown,
            stem,
            groove,
            leaf,
            head,
            bead,
            true);
        AddBroccoliHead(
            pivot.transform,
            0.022f * side,
            0.084f + u * 0.010f,
            9f * side,
            side,
            crown,
            stem,
            groove,
            leaf,
            head,
            bead,
            false);
        return pivot.transform;
    }

    // Two heads on one foot pivot so LateUpdate sway is
    // one rotation, not a Find or a per-curd array.
    // Size is sprite.size, not scale. Side view: a
    // TIGHT CREAMY-WHITE curd on a THICK stem sitting
    // ON the slab — not a broccoli green bead dome, not
    // a cabbage tight wrap ball, not a lettuce loose
    // pale cup. u < 0.38 paints ripe ivory curd; the
    // second head is a shorter sibling.
    private Transform MakeCauliflower(Vector3 foot, float face, float u)
    {
        float side = face >= 0f ? 1f : -1f;
        GameObject pivot = new GameObject("YardCauliflower");
        pivot.transform.SetParent(transform, false);
        pivot.transform.position = foot;

        bool ripe = u < 0.38f;
        Color crown = ripe
            ? new Color(0.52f, 0.58f, 0.32f, 1f)
            : new Color(0.44f, 0.54f, 0.28f, 1f);
        Color stem = ripe
            ? new Color(0.72f, 0.78f, 0.52f, 1f)
            : new Color(0.62f, 0.72f, 0.42f, 1f);
        Color groove = ripe
            ? new Color(0.62f, 0.70f, 0.42f, 1f)
            : new Color(0.52f, 0.64f, 0.34f, 1f);
        Color leaf = ripe
            ? new Color(0.42f, 0.58f, 0.28f, 1f)
            : new Color(0.34f, 0.52f, 0.22f, 1f);
        Color curd = ripe
            ? new Color(0.92f, 0.90f, 0.78f, 1f)
            : new Color(0.82f, 0.88f, 0.62f, 1f);
        Color bead = ripe
            ? new Color(0.88f, 0.84f, 0.68f, 1f)
            : new Color(0.74f, 0.82f, 0.52f, 1f);

        AddCauliflowerHead(
            pivot.transform,
            -0.012f * side,
            0.104f + u * 0.012f,
            -5f * side,
            side,
            crown,
            stem,
            groove,
            leaf,
            curd,
            bead,
            true);
        AddCauliflowerHead(
            pivot.transform,
            0.022f * side,
            0.078f + u * 0.010f,
            9f * side,
            side,
            crown,
            stem,
            groove,
            leaf,
            curd,
            bead,
            false);
        return pivot.transform;
    }

    // Two stalks on one foot pivot so LateUpdate sway is
    // one rotation, not a Find or a per-sprout array.
    // Size is sprite.size, not scale. Side view: TIGHT
    // GREEN sprouts along a TALL stem sitting ON the
    // slab — not a broccoli green bead dome, not a
    // cauliflower cream mound, not a cabbage tight wrap
    // ball. u < 0.38 paints ripe forest-green sprouts;
    // the second stalk is a shorter sibling.
    private Transform MakeBrussels(Vector3 foot, float face, float u)
    {
        float side = face >= 0f ? 1f : -1f;
        GameObject pivot = new GameObject("YardBrussels");
        pivot.transform.SetParent(transform, false);
        pivot.transform.position = foot;

        bool ripe = u < 0.38f;
        Color crown = ripe
            ? new Color(0.36f, 0.42f, 0.20f, 1f)
            : new Color(0.42f, 0.50f, 0.24f, 1f);
        Color stem = ripe
            ? new Color(0.50f, 0.60f, 0.32f, 1f)
            : new Color(0.58f, 0.68f, 0.36f, 1f);
        Color groove = ripe
            ? new Color(0.40f, 0.50f, 0.24f, 1f)
            : new Color(0.48f, 0.58f, 0.28f, 1f);
        Color leaf = ripe
            ? new Color(0.24f, 0.48f, 0.20f, 1f)
            : new Color(0.32f, 0.56f, 0.22f, 1f);
        Color sprout = ripe
            ? new Color(0.20f, 0.42f, 0.18f, 1f)
            : new Color(0.32f, 0.54f, 0.22f, 1f);
        Color wrap = ripe
            ? new Color(0.28f, 0.50f, 0.22f, 1f)
            : new Color(0.40f, 0.60f, 0.26f, 1f);

        AddBrusselsHead(
            pivot.transform,
            -0.010f * side,
            0.168f + u * 0.012f,
            -4f * side,
            side,
            crown,
            stem,
            groove,
            leaf,
            sprout,
            wrap,
            true);
        AddBrusselsHead(
            pivot.transform,
            0.020f * side,
            0.126f + u * 0.010f,
            8f * side,
            side,
            crown,
            stem,
            groove,
            leaf,
            sprout,
            wrap,
            false);
        return pivot.transform;
    }

    // Two buds on one foot pivot so LateUpdate sway is
    // one rotation, not a Find or a per-scale array.
    // Size is sprite.size, not scale. Side view: a
    // SCALY edible bud on a THICK stem sitting ON the
    // slab — not a broccoli green bead dome, not a
    // cauliflower cream mound, not sprouts along a
    // tall stem, not a thistle purple bloom on a thin
    // verge shaft. u < 0.38 paints ripe sage-olive
    // scales; the second bud is a shorter sibling.
    private Transform MakeArtichoke(Vector3 foot, float face, float u)
    {
        float side = face >= 0f ? 1f : -1f;
        GameObject pivot = new GameObject("YardArtichoke");
        pivot.transform.SetParent(transform, false);
        pivot.transform.position = foot;

        bool ripe = u < 0.38f;
        Color crown = ripe
            ? new Color(0.40f, 0.44f, 0.26f, 1f)
            : new Color(0.36f, 0.48f, 0.24f, 1f);
        Color stem = ripe
            ? new Color(0.50f, 0.60f, 0.34f, 1f)
            : new Color(0.46f, 0.64f, 0.30f, 1f);
        Color groove = ripe
            ? new Color(0.40f, 0.50f, 0.26f, 1f)
            : new Color(0.38f, 0.54f, 0.24f, 1f);
        Color leaf = ripe
            ? new Color(0.42f, 0.52f, 0.34f, 1f)
            : new Color(0.36f, 0.54f, 0.26f, 1f);
        Color scale = ripe
            ? new Color(0.34f, 0.46f, 0.28f, 1f)
            : new Color(0.30f, 0.52f, 0.24f, 1f);
        Color rim = ripe
            ? new Color(0.46f, 0.56f, 0.34f, 1f)
            : new Color(0.40f, 0.60f, 0.28f, 1f);

        AddArtichokeHead(
            pivot.transform,
            -0.012f * side,
            0.118f + u * 0.012f,
            -5f * side,
            side,
            crown,
            stem,
            groove,
            leaf,
            scale,
            rim,
            true);
        AddArtichokeHead(
            pivot.transform,
            0.022f * side,
            0.088f + u * 0.010f,
            8f * side,
            side,
            crown,
            stem,
            groove,
            leaf,
            scale,
            rim,
            false);
        return pivot.transform;
    }

    // Two fruits on one foot pivot so LateUpdate sway is
    // one rotation, not a Find or a per-scale array.
    // Size is sprite.size, not scale. Side view: a
    // GLOSSY PURPLE teardrop hanging from a green calyx
    // on a stem sitting ON the slab — not a tomato round
    // red ball on a stake vine, not an artichoke scaly
    // globe on a thick stem, not a thistle purple bloom
    // on a thin verge shaft. u < 0.38 paints ripe deep
    // purple; the second fruit is a shorter sibling.
    private Transform MakeEggplant(Vector3 foot, float face, float u)
    {
        float side = face >= 0f ? 1f : -1f;
        GameObject pivot = new GameObject("YardEggplant");
        pivot.transform.SetParent(transform, false);
        pivot.transform.position = foot;

        bool ripe = u < 0.38f;
        Color crown = ripe
            ? new Color(0.34f, 0.46f, 0.22f, 1f)
            : new Color(0.32f, 0.50f, 0.20f, 1f);
        Color stem = ripe
            ? new Color(0.38f, 0.52f, 0.24f, 1f)
            : new Color(0.36f, 0.56f, 0.22f, 1f);
        Color groove = ripe
            ? new Color(0.30f, 0.44f, 0.18f, 1f)
            : new Color(0.28f, 0.48f, 0.16f, 1f);
        Color leaf = ripe
            ? new Color(0.28f, 0.48f, 0.22f, 1f)
            : new Color(0.26f, 0.52f, 0.20f, 1f);
        Color calyx = ripe
            ? new Color(0.32f, 0.50f, 0.20f, 1f)
            : new Color(0.30f, 0.54f, 0.18f, 1f);
        Color skin = ripe
            ? new Color(0.38f, 0.14f, 0.44f, 1f)
            : new Color(0.34f, 0.26f, 0.40f, 1f);
        Color belly = ripe
            ? new Color(0.26f, 0.08f, 0.32f, 1f)
            : new Color(0.24f, 0.18f, 0.30f, 1f);
        Color gloss = ripe
            ? new Color(0.62f, 0.40f, 0.68f, 1f)
            : new Color(0.50f, 0.42f, 0.52f, 1f);

        AddEggplantHead(
            pivot.transform,
            -0.012f * side,
            0.108f + u * 0.012f,
            -5f * side,
            side,
            crown,
            stem,
            groove,
            leaf,
            calyx,
            skin,
            belly,
            gloss,
            true);
        AddEggplantHead(
            pivot.transform,
            0.022f * side,
            0.082f + u * 0.010f,
            8f * side,
            side,
            crown,
            stem,
            groove,
            leaf,
            calyx,
            skin,
            belly,
            gloss,
            false);
        return pivot.transform;
    }

    // Two heads on one foot pivot so LateUpdate sway is
    // one rotation, not a Find or a per-scale array.
    // Size is sprite.size, not scale. Side view: a
    // GLOSSY BLOCKY pod under a green calyx on a
    // stem sitting ON the slab — not an eggplant hanging
    // purple teardrop, not a tomato round red ball on a
    // stake vine, not an artichoke scaly globe on a
    // thick stem. u < 0.38 paints ripe red skin; the
    // second pod is a shorter sibling.
    private Transform MakePepper(Vector3 foot, float face, float u)
    {
        float side = face >= 0f ? 1f : -1f;
        GameObject pivot = new GameObject("YardPepper");
        pivot.transform.SetParent(transform, false);
        pivot.transform.position = foot;

        bool ripe = u < 0.38f;
        Color crown = ripe
            ? new Color(0.34f, 0.46f, 0.22f, 1f)
            : new Color(0.32f, 0.50f, 0.20f, 1f);
        Color stem = ripe
            ? new Color(0.38f, 0.52f, 0.24f, 1f)
            : new Color(0.36f, 0.56f, 0.22f, 1f);
        Color groove = ripe
            ? new Color(0.30f, 0.44f, 0.18f, 1f)
            : new Color(0.28f, 0.48f, 0.16f, 1f);
        Color leaf = ripe
            ? new Color(0.28f, 0.48f, 0.22f, 1f)
            : new Color(0.26f, 0.52f, 0.20f, 1f);
        Color calyx = ripe
            ? new Color(0.32f, 0.50f, 0.20f, 1f)
            : new Color(0.30f, 0.54f, 0.18f, 1f);
        Color skin = ripe
            ? new Color(0.82f, 0.16f, 0.12f, 1f)
            : new Color(0.36f, 0.58f, 0.16f, 1f);
        Color belly = ripe
            ? new Color(0.62f, 0.08f, 0.08f, 1f)
            : new Color(0.26f, 0.44f, 0.12f, 1f);
        Color gloss = ripe
            ? new Color(0.94f, 0.48f, 0.40f, 1f)
            : new Color(0.62f, 0.78f, 0.42f, 1f);

        AddPepperHead(
            pivot.transform,
            -0.012f * side,
            0.100f + u * 0.012f,
            -5f * side,
            side,
            crown,
            stem,
            groove,
            leaf,
            calyx,
            skin,
            belly,
            gloss,
            true);
        AddPepperHead(
            pivot.transform,
            0.022f * side,
            0.078f + u * 0.010f,
            8f * side,
            side,
            crown,
            stem,
            groove,
            leaf,
            calyx,
            skin,
            belly,
            gloss,
            false);
        return pivot.transform;
    }

    // Size is sprite.size, not scale. Side view: a
    // LONG GREEN fruit on a sprawling vine sitting ON
    // the slab — not a pepper glossy blocky 28×30 mm
    // pod under a calyx, not a tomato round red ball
    // on a stake vine, not an eggplant hanging purple
    // teardrop. u < 0.38 paints ripe dark-green skin;
    // the second fruit is a shorter sibling.
    private Transform MakeCucumber(Vector3 foot, float face, float u)
    {
        float side = face >= 0f ? 1f : -1f;
        GameObject pivot = new GameObject("YardCucumber");
        pivot.transform.SetParent(transform, false);
        pivot.transform.position = foot;

        bool ripe = u < 0.38f;
        Color crown = ripe
            ? new Color(0.32f, 0.44f, 0.20f, 1f)
            : new Color(0.30f, 0.48f, 0.18f, 1f);
        Color vine = ripe
            ? new Color(0.34f, 0.50f, 0.22f, 1f)
            : new Color(0.32f, 0.54f, 0.20f, 1f);
        Color groove = ripe
            ? new Color(0.26f, 0.40f, 0.16f, 1f)
            : new Color(0.24f, 0.44f, 0.14f, 1f);
        Color leaf = ripe
            ? new Color(0.26f, 0.50f, 0.20f, 1f)
            : new Color(0.24f, 0.54f, 0.18f, 1f);
        Color skin = ripe
            ? new Color(0.22f, 0.50f, 0.18f, 1f)
            : new Color(0.46f, 0.62f, 0.22f, 1f);
        Color belly = ripe
            ? new Color(0.16f, 0.38f, 0.14f, 1f)
            : new Color(0.36f, 0.50f, 0.16f, 1f);
        Color stripe = ripe
            ? new Color(0.40f, 0.66f, 0.30f, 1f)
            : new Color(0.58f, 0.74f, 0.34f, 1f);
        Color gloss = ripe
            ? new Color(0.52f, 0.76f, 0.42f, 1f)
            : new Color(0.68f, 0.82f, 0.48f, 1f);
        Color wart = ripe
            ? new Color(0.18f, 0.34f, 0.12f, 1f)
            : new Color(0.32f, 0.46f, 0.14f, 1f);
        Color bloom = ripe
            ? new Color(0.90f, 0.78f, 0.22f, 1f)
            : new Color(0.86f, 0.82f, 0.36f, 1f);

        AddCucumberHead(
            pivot.transform,
            -0.010f * side,
            0.070f + u * 0.010f,
            -8f * side,
            side,
            crown,
            vine,
            groove,
            leaf,
            skin,
            belly,
            stripe,
            gloss,
            wart,
            bloom,
            true);
        AddCucumberHead(
            pivot.transform,
            0.026f * side,
            0.054f + u * 0.008f,
            10f * side,
            side,
            crown,
            vine,
            groove,
            leaf,
            skin,
            belly,
            stripe,
            gloss,
            wart,
            bloom,
            false);
        return pivot.transform;
    }

    // Side view: a NETTED ROUND kitchen fruit on a
    // sprawling vine ON the slab — not a cucumber
    // long green 52×14 mm ellipse, not a pumpkin
    // ribbed 108×70 mm gourd, not a pepper glossy
    // blocky 28×30 mm pod. u < 0.38 paints ripe
    // tan-buff netted skin; the second fruit is a
    // shorter sibling.
    private Transform MakeMelon(Vector3 foot, float face, float u)
    {
        float side = face >= 0f ? 1f : -1f;
        GameObject pivot = new GameObject("YardMelon");
        pivot.transform.SetParent(transform, false);
        pivot.transform.position = foot;

        bool ripe = u < 0.38f;
        Color crown = ripe
            ? new Color(0.32f, 0.48f, 0.20f, 1f)
            : new Color(0.30f, 0.52f, 0.18f, 1f);
        Color vine = ripe
            ? new Color(0.34f, 0.52f, 0.22f, 1f)
            : new Color(0.32f, 0.56f, 0.20f, 1f);
        Color groove = ripe
            ? new Color(0.26f, 0.42f, 0.16f, 1f)
            : new Color(0.24f, 0.46f, 0.14f, 1f);
        Color leaf = ripe
            ? new Color(0.26f, 0.52f, 0.18f, 1f)
            : new Color(0.24f, 0.56f, 0.16f, 1f);
        Color skin = ripe
            ? new Color(0.84f, 0.64f, 0.32f, 1f)
            : new Color(0.58f, 0.66f, 0.28f, 1f);
        Color belly = ripe
            ? new Color(0.70f, 0.50f, 0.22f, 1f)
            : new Color(0.46f, 0.54f, 0.20f, 1f);
        Color net = ripe
            ? new Color(0.56f, 0.42f, 0.20f, 1f)
            : new Color(0.42f, 0.50f, 0.18f, 1f);
        Color gloss = ripe
            ? new Color(0.94f, 0.82f, 0.52f, 1f)
            : new Color(0.78f, 0.82f, 0.48f, 1f);
        Color bloom = ripe
            ? new Color(0.90f, 0.78f, 0.22f, 1f)
            : new Color(0.86f, 0.82f, 0.36f, 1f);

        AddMelonHead(
            pivot.transform,
            -0.010f * side,
            0.070f + u * 0.010f,
            -8f * side,
            side,
            crown,
            vine,
            groove,
            leaf,
            skin,
            belly,
            net,
            gloss,
            bloom,
            true);
        AddMelonHead(
            pivot.transform,
            0.028f * side,
            0.054f + u * 0.008f,
            10f * side,
            side,
            crown,
            vine,
            groove,
            leaf,
            skin,
            belly,
            net,
            gloss,
            bloom,
            false);
        return pivot.transform;
    }

    // Size is sprite.size, not scale. Side view: a
    // STRIPED OVAL kitchen fruit on a sprawling vine
    // ON the slab — not a melon netted round 36×32 mm
    // ellipse, not a cucumber long green 52×14 mm,
    // not a pumpkin ribbed 108×70 mm gourd. u < 0.38
    // paints ripe dark-green striped skin; the second
    // fruit is a shorter sibling.
    private Transform MakeWatermelon(Vector3 foot, float face, float u)
    {
        float side = face >= 0f ? 1f : -1f;
        GameObject pivot = new GameObject("YardWatermelon");
        pivot.transform.SetParent(transform, false);
        pivot.transform.position = foot;

        bool ripe = u < 0.38f;
        Color crown = ripe
            ? new Color(0.30f, 0.46f, 0.18f, 1f)
            : new Color(0.28f, 0.50f, 0.16f, 1f);
        Color vine = ripe
            ? new Color(0.32f, 0.50f, 0.20f, 1f)
            : new Color(0.30f, 0.54f, 0.18f, 1f);
        Color groove = ripe
            ? new Color(0.24f, 0.40f, 0.14f, 1f)
            : new Color(0.22f, 0.44f, 0.12f, 1f);
        Color leaf = ripe
            ? new Color(0.24f, 0.50f, 0.16f, 1f)
            : new Color(0.22f, 0.54f, 0.14f, 1f);
        Color skin = ripe
            ? new Color(0.14f, 0.42f, 0.16f, 1f)
            : new Color(0.28f, 0.56f, 0.20f, 1f);
        Color stripe = ripe
            ? new Color(0.52f, 0.72f, 0.32f, 1f)
            : new Color(0.62f, 0.76f, 0.38f, 1f);
        Color belly = ripe
            ? new Color(0.86f, 0.78f, 0.42f, 1f)
            : new Color(0.70f, 0.76f, 0.40f, 1f);
        Color gloss = ripe
            ? new Color(0.70f, 0.88f, 0.50f, 1f)
            : new Color(0.76f, 0.86f, 0.52f, 1f);

        AddWatermelonHead(
            pivot.transform,
            -0.012f * side,
            0.074f + u * 0.010f,
            -8f * side,
            side,
            crown,
            vine,
            groove,
            leaf,
            skin,
            stripe,
            belly,
            gloss,
            true);
        AddWatermelonHead(
            pivot.transform,
            0.034f * side,
            0.056f + u * 0.008f,
            10f * side,
            side,
            crown,
            vine,
            groove,
            leaf,
            skin,
            stripe,
            belly,
            gloss,
            false);
        return pivot.transform;
    }

    // YELLOW HOOK kitchen fruit on a sprawling vine
    // ON the slab — not a watermelon striped oval
    // 72×46 mm ellipse, not a melon netted round
    // 36×32 mm, not a pumpkin ribbed 108×70 mm
    // gourd. u < 0.38 paints ripe yellow crookneck
    // skin; the second fruit is a shorter sibling.
    private Transform MakeZucchini(Vector3 foot, float face, float u)
    {
        float side = face >= 0f ? 1f : -1f;
        GameObject pivot = new GameObject("YardZucchini");
        pivot.transform.SetParent(transform, false);
        pivot.transform.position = foot;

        bool ripe = u < 0.38f;
        Color crown = ripe
            ? new Color(0.34f, 0.52f, 0.16f, 1f)
            : new Color(0.30f, 0.54f, 0.18f, 1f);
        Color vine = ripe
            ? new Color(0.36f, 0.56f, 0.18f, 1f)
            : new Color(0.32f, 0.58f, 0.20f, 1f);
        Color groove = ripe
            ? new Color(0.26f, 0.44f, 0.12f, 1f)
            : new Color(0.24f, 0.48f, 0.14f, 1f);
        Color leaf = ripe
            ? new Color(0.28f, 0.56f, 0.16f, 1f)
            : new Color(0.26f, 0.58f, 0.18f, 1f);
        Color skin = ripe
            ? new Color(0.90f, 0.76f, 0.16f, 1f)
            : new Color(0.72f, 0.78f, 0.28f, 1f);
        Color speckle = ripe
            ? new Color(0.78f, 0.62f, 0.12f, 1f)
            : new Color(0.60f, 0.66f, 0.20f, 1f);
        Color belly = ripe
            ? new Color(0.96f, 0.88f, 0.42f, 1f)
            : new Color(0.82f, 0.86f, 0.40f, 1f);
        Color gloss = ripe
            ? new Color(0.98f, 0.94f, 0.62f, 1f)
            : new Color(0.90f, 0.92f, 0.58f, 1f);

        AddZucchiniHead(
            pivot.transform,
            -0.012f * side,
            0.074f + u * 0.010f,
            -8f * side,
            side,
            crown,
            vine,
            groove,
            leaf,
            skin,
            speckle,
            belly,
            gloss,
            true);
        AddZucchiniHead(
            pivot.transform,
            0.034f * side,
            0.056f + u * 0.008f,
            10f * side,
            side,
            crown,
            vine,
            groove,
            leaf,
            skin,
            speckle,
            belly,
            gloss,
            false);
        return pivot.transform;
    }

    // YELLOW SCALLOPED DISK kitchen fruit on a
    // sprawling vine ON the slab — not a zucchini
    // yellow hook 42×16 mm neck + 22×18 mm bulb, not
    // a watermelon striped oval 72×46 mm, not a
    // pumpkin ribbed 108×70 mm gourd. u < 0.38 paints
    // ripe cream-yellow skin; the second fruit is a
    // shorter sibling.
    private Transform MakePattypan(Vector3 foot, float face, float u)
    {
        float side = face >= 0f ? 1f : -1f;
        GameObject pivot = new GameObject("YardPattypan");
        pivot.transform.SetParent(transform, false);
        pivot.transform.position = foot;

        bool ripe = u < 0.38f;
        Color crown = ripe
            ? new Color(0.34f, 0.52f, 0.16f, 1f)
            : new Color(0.30f, 0.54f, 0.18f, 1f);
        Color vine = ripe
            ? new Color(0.36f, 0.56f, 0.18f, 1f)
            : new Color(0.32f, 0.58f, 0.20f, 1f);
        Color groove = ripe
            ? new Color(0.26f, 0.44f, 0.12f, 1f)
            : new Color(0.24f, 0.48f, 0.14f, 1f);
        Color leaf = ripe
            ? new Color(0.28f, 0.56f, 0.16f, 1f)
            : new Color(0.26f, 0.58f, 0.18f, 1f);
        Color skin = ripe
            ? new Color(0.93f, 0.88f, 0.48f, 1f)
            : new Color(0.70f, 0.78f, 0.32f, 1f);
        Color speckle = ripe
            ? new Color(0.80f, 0.74f, 0.32f, 1f)
            : new Color(0.56f, 0.66f, 0.22f, 1f);
        Color belly = ripe
            ? new Color(0.97f, 0.94f, 0.68f, 1f)
            : new Color(0.82f, 0.86f, 0.46f, 1f);
        Color gloss = ripe
            ? new Color(0.99f, 0.96f, 0.78f, 1f)
            : new Color(0.90f, 0.92f, 0.62f, 1f);

        AddPattypanHead(
            pivot.transform,
            -0.012f * side,
            0.074f + u * 0.010f,
            -6f * side,
            side,
            crown,
            vine,
            groove,
            leaf,
            skin,
            speckle,
            belly,
            gloss,
            true);
        AddPattypanHead(
            pivot.transform,
            0.034f * side,
            0.056f + u * 0.008f,
            8f * side,
            side,
            crown,
            vine,
            groove,
            leaf,
            skin,
            speckle,
            belly,
            gloss,
            false);
        return pivot.transform;
    }

    // BEIGE BOTTLE kitchen fruit on a sprawling vine
    // ON the slab — not a pattypan yellow scalloped
    // disk 40×20 mm plus six equatorial lobes, not a
    // zucchini yellow hook 42×16 mm neck at 38° +
    // 22×18 mm bulb, not a pumpkin ribbed 108×70 mm
    // gourd. u < 0.38 paints ripe tan-beige skin; the
    // second fruit is a shorter sibling.
    private Transform MakeButternut(Vector3 foot, float face, float u)
    {
        float side = face >= 0f ? 1f : -1f;
        GameObject pivot = new GameObject("YardButternut");
        pivot.transform.SetParent(transform, false);
        pivot.transform.position = foot;

        bool ripe = u < 0.38f;
        Color crown = ripe
            ? new Color(0.34f, 0.52f, 0.16f, 1f)
            : new Color(0.30f, 0.54f, 0.18f, 1f);
        Color vine = ripe
            ? new Color(0.36f, 0.56f, 0.18f, 1f)
            : new Color(0.32f, 0.58f, 0.20f, 1f);
        Color groove = ripe
            ? new Color(0.26f, 0.44f, 0.12f, 1f)
            : new Color(0.24f, 0.48f, 0.14f, 1f);
        Color leaf = ripe
            ? new Color(0.28f, 0.56f, 0.16f, 1f)
            : new Color(0.26f, 0.58f, 0.18f, 1f);
        Color skin = ripe
            ? new Color(0.84f, 0.62f, 0.28f, 1f)
            : new Color(0.58f, 0.66f, 0.28f, 1f);
        Color speckle = ripe
            ? new Color(0.70f, 0.48f, 0.18f, 1f)
            : new Color(0.46f, 0.54f, 0.18f, 1f);
        Color belly = ripe
            ? new Color(0.90f, 0.72f, 0.38f, 1f)
            : new Color(0.70f, 0.74f, 0.38f, 1f);
        Color gloss = ripe
            ? new Color(0.96f, 0.82f, 0.52f, 1f)
            : new Color(0.82f, 0.86f, 0.52f, 1f);

        AddButternutHead(
            pivot.transform,
            -0.012f * side,
            0.074f + u * 0.010f,
            -6f * side,
            side,
            crown,
            vine,
            groove,
            leaf,
            skin,
            speckle,
            belly,
            gloss,
            true);
        AddButternutHead(
            pivot.transform,
            0.034f * side,
            0.056f + u * 0.008f,
            8f * side,
            side,
            crown,
            vine,
            groove,
            leaf,
            skin,
            speckle,
            belly,
            gloss,
            false);
        return pivot.transform;
    }

    // Acorn sits ON the slab. Crown at 0.52·H so
    // the vine foot is dirt, not a hanging bottle.
    // Silhouette is a DARK-GREEN RIBBED ACORN —
    // 42×36 mm heart plus a 16×14 mm blossom point
    // and four vertical ribs, not butternut bottle
    // neck 48×16 mm at 8° plus a 30×26 mm belly, not
    // pattypan disk 40×20 mm plus six lobes, not
    // pumpkin ribbed 108×70 mm. u < 0.38 paints
    // ripe dark-green skin with an amber point; the
    // second fruit is a shorter sibling.
    private Transform MakeAcorn(Vector3 foot, float face, float u)
    {
        float side = face >= 0f ? 1f : -1f;
        GameObject pivot = new GameObject("YardAcorn");
        pivot.transform.SetParent(transform, false);
        pivot.transform.position = foot;

        bool ripe = u < 0.38f;
        Color crown = ripe
            ? new Color(0.22f, 0.44f, 0.14f, 1f)
            : new Color(0.26f, 0.50f, 0.16f, 1f);
        Color vine = ripe
            ? new Color(0.24f, 0.48f, 0.14f, 1f)
            : new Color(0.28f, 0.52f, 0.16f, 1f);
        Color groove = ripe
            ? new Color(0.16f, 0.34f, 0.10f, 1f)
            : new Color(0.18f, 0.40f, 0.12f, 1f);
        Color leaf = ripe
            ? new Color(0.20f, 0.50f, 0.14f, 1f)
            : new Color(0.24f, 0.54f, 0.16f, 1f);
        Color skin = ripe
            ? new Color(0.16f, 0.38f, 0.14f, 1f)
            : new Color(0.24f, 0.50f, 0.16f, 1f);
        Color rib = ripe
            ? new Color(0.10f, 0.26f, 0.10f, 1f)
            : new Color(0.14f, 0.36f, 0.12f, 1f);
        Color blush = ripe
            ? new Color(0.72f, 0.42f, 0.12f, 1f)
            : new Color(0.40f, 0.56f, 0.18f, 1f);
        Color gloss = ripe
            ? new Color(0.32f, 0.54f, 0.22f, 1f)
            : new Color(0.44f, 0.66f, 0.26f, 1f);

        AddAcornHead(
            pivot.transform,
            -0.012f * side,
            0.074f + u * 0.010f,
            -6f * side,
            side,
            crown,
            vine,
            groove,
            leaf,
            skin,
            rib,
            blush,
            gloss,
            true);
        AddAcornHead(
            pivot.transform,
            0.034f * side,
            0.056f + u * 0.008f,
            8f * side,
            side,
            crown,
            vine,
            groove,
            leaf,
            skin,
            rib,
            blush,
            gloss,
            false);
        return pivot.transform;
    }

    // Spaghetti sits ON the slab. Crown at 0.52·H so
    // the vine foot is dirt, not a hanging bottle.
    // Silhouette is a PALE-YELLOW OBLONG — 56×26 mm
    // oval plus a 10×8 mm blossom scar, not acorn
    // heart 42×36 mm plus a 16×14 mm point and four
    // vertical ribs, not butternut bottle neck 48×16
    // mm at 8° plus a 30×26 mm belly, not pumpkin
    // ribbed 108×70 mm. u < 0.38 paints ripe pale-yellow
    // skin; the second fruit is a shorter sibling.
    private Transform MakeSpaghetti(Vector3 foot, float face, float u)
    {
        float side = face >= 0f ? 1f : -1f;
        GameObject pivot = new GameObject("YardSpaghetti");
        pivot.transform.SetParent(transform, false);
        pivot.transform.position = foot;

        bool ripe = u < 0.38f;
        Color crown = ripe
            ? new Color(0.34f, 0.52f, 0.16f, 1f)
            : new Color(0.30f, 0.54f, 0.18f, 1f);
        Color vine = ripe
            ? new Color(0.36f, 0.56f, 0.18f, 1f)
            : new Color(0.32f, 0.58f, 0.20f, 1f);
        Color groove = ripe
            ? new Color(0.26f, 0.44f, 0.12f, 1f)
            : new Color(0.24f, 0.48f, 0.14f, 1f);
        Color leaf = ripe
            ? new Color(0.28f, 0.56f, 0.16f, 1f)
            : new Color(0.26f, 0.58f, 0.18f, 1f);
        Color skin = ripe
            ? new Color(0.94f, 0.84f, 0.38f, 1f)
            : new Color(0.72f, 0.76f, 0.32f, 1f);
        Color speckle = ripe
            ? new Color(0.86f, 0.74f, 0.28f, 1f)
            : new Color(0.58f, 0.64f, 0.22f, 1f);
        Color belly = ripe
            ? new Color(0.97f, 0.90f, 0.58f, 1f)
            : new Color(0.82f, 0.82f, 0.46f, 1f);
        Color gloss = ripe
            ? new Color(0.99f, 0.94f, 0.72f, 1f)
            : new Color(0.90f, 0.90f, 0.56f, 1f);

        AddSpaghettiHead(
            pivot.transform,
            -0.012f * side,
            0.074f + u * 0.010f,
            -6f * side,
            side,
            crown,
            vine,
            groove,
            leaf,
            skin,
            speckle,
            belly,
            gloss,
            true);
        AddSpaghettiHead(
            pivot.transform,
            0.034f * side,
            0.056f + u * 0.008f,
            8f * side,
            side,
            crown,
            vine,
            groove,
            leaf,
            skin,
            speckle,
            belly,
            gloss,
            false);
        return pivot.transform;
    }

    // Delicata sits ON the slab. Crown at 0.52·H so
    // the vine foot is dirt, not a hanging bottle.
    // Silhouette is a CREAM OBLONG with DARK-GREEN
    // STRIPES — 50×28 mm oval plus four 5 mm dark
    // bands, not spaghetti oval 56×26 mm plus two
    // faint cream streaks, not acorn heart 42×36 mm
    // plus a 16×14 mm point and four vertical ribs,
    // not butternut bottle neck 48×16 mm at 8° plus
    // a 30×26 mm belly, not pumpkin ribbed 108×70 mm.
    // u < 0.38 paints ripe cream skin; the second
    // fruit is a shorter sibling.
    private Transform MakeDelicata(Vector3 foot, float face, float u)
    {
        float side = face >= 0f ? 1f : -1f;
        GameObject pivot = new GameObject("YardDelicata");
        pivot.transform.SetParent(transform, false);
        pivot.transform.position = foot;

        bool ripe = u < 0.38f;
        Color crown = ripe
            ? new Color(0.34f, 0.52f, 0.16f, 1f)
            : new Color(0.30f, 0.54f, 0.18f, 1f);
        Color vine = ripe
            ? new Color(0.36f, 0.56f, 0.18f, 1f)
            : new Color(0.32f, 0.58f, 0.20f, 1f);
        Color groove = ripe
            ? new Color(0.26f, 0.44f, 0.12f, 1f)
            : new Color(0.24f, 0.48f, 0.14f, 1f);
        Color leaf = ripe
            ? new Color(0.28f, 0.56f, 0.16f, 1f)
            : new Color(0.26f, 0.58f, 0.18f, 1f);
        Color skin = ripe
            ? new Color(0.92f, 0.84f, 0.58f, 1f)
            : new Color(0.70f, 0.74f, 0.40f, 1f);
        Color stripe = ripe
            ? new Color(0.18f, 0.34f, 0.12f, 1f)
            : new Color(0.16f, 0.40f, 0.14f, 1f);
        Color belly = ripe
            ? new Color(0.96f, 0.90f, 0.70f, 1f)
            : new Color(0.82f, 0.82f, 0.50f, 1f);
        Color gloss = ripe
            ? new Color(0.98f, 0.94f, 0.80f, 1f)
            : new Color(0.88f, 0.88f, 0.58f, 1f);

        AddDelicataHead(
            pivot.transform,
            -0.012f * side,
            0.074f + u * 0.010f,
            -6f * side,
            side,
            crown,
            vine,
            groove,
            leaf,
            skin,
            stripe,
            belly,
            gloss,
            true);
        AddDelicataHead(
            pivot.transform,
            0.034f * side,
            0.056f + u * 0.008f,
            8f * side,
            side,
            crown,
            vine,
            groove,
            leaf,
            skin,
            stripe,
            belly,
            gloss,
            false);
        return pivot.transform;
    }

    // Carnival sits ON the slab. Crown at 0.52·H so
    // the vine foot is dirt, not a hanging bottle.
    // Silhouette is a ROUND CREAM globe with
    // ORANGE-GREEN MOTTLE — 38×34 mm flattened
    // ellipse plus blotch patches, not delicata
    // oblong 50×28 mm plus four 5 mm dark-green
    // stripes, not spaghetti oval 56×26 mm plus
    // two faint cream streaks, not acorn heart
    // 42×36 mm plus a 16×14 mm point and four
    // vertical ribs, not pumpkin ribbed 108×70 mm.
    // u < 0.38 paints ripe cream skin; the second
    // fruit is a shorter sibling.
    private Transform MakeCarnival(Vector3 foot, float face, float u)
    {
        float side = face >= 0f ? 1f : -1f;
        GameObject pivot = new GameObject("YardCarnival");
        pivot.transform.SetParent(transform, false);
        pivot.transform.position = foot;

        bool ripe = u < 0.38f;
        Color crown = ripe
            ? new Color(0.34f, 0.52f, 0.16f, 1f)
            : new Color(0.30f, 0.54f, 0.18f, 1f);
        Color vine = ripe
            ? new Color(0.36f, 0.56f, 0.18f, 1f)
            : new Color(0.32f, 0.58f, 0.20f, 1f);
        Color groove = ripe
            ? new Color(0.26f, 0.44f, 0.12f, 1f)
            : new Color(0.24f, 0.48f, 0.14f, 1f);
        Color leaf = ripe
            ? new Color(0.28f, 0.56f, 0.16f, 1f)
            : new Color(0.26f, 0.58f, 0.18f, 1f);
        Color skin = ripe
            ? new Color(0.94f, 0.88f, 0.70f, 1f)
            : new Color(0.68f, 0.74f, 0.38f, 1f);
        Color orange = ripe
            ? new Color(0.86f, 0.46f, 0.14f, 1f)
            : new Color(0.62f, 0.50f, 0.16f, 1f);
        Color green = ripe
            ? new Color(0.32f, 0.52f, 0.18f, 1f)
            : new Color(0.28f, 0.54f, 0.16f, 1f);
        Color belly = ripe
            ? new Color(0.98f, 0.92f, 0.78f, 1f)
            : new Color(0.80f, 0.82f, 0.48f, 1f);
        Color gloss = ripe
            ? new Color(0.99f, 0.96f, 0.86f, 1f)
            : new Color(0.86f, 0.88f, 0.56f, 1f);

        AddCarnivalHead(
            pivot.transform,
            -0.012f * side,
            0.074f + u * 0.010f,
            -6f * side,
            side,
            crown,
            vine,
            groove,
            leaf,
            skin,
            orange,
            green,
            belly,
            gloss,
            true);
        AddCarnivalHead(
            pivot.transform,
            0.034f * side,
            0.056f + u * 0.008f,
            8f * side,
            side,
            crown,
            vine,
            groove,
            leaf,
            skin,
            orange,
            green,
            belly,
            gloss,
            false);
        return pivot.transform;
    }

    // Sweet dumpling sits ON the slab. Crown at
    // 0.52·H so the vine foot is dirt, not a hanging
    // bottle. Silhouette is a SMALL ROUND CREAM
    // fruit with DARK-GREEN RIBS — 28×26 mm globe
    // plus four 3 mm vertical ribs, not carnival
    // flattened 38×34 mm plus orange-green blotches
    // 10–14 mm, not delicata oblong 50×28 mm plus
    // four 5 mm dark-green stripes, not spaghetti
    // oval 56×26 mm plus two faint cream streaks,
    // not acorn heart 42×36 mm plus a 16×14 mm
    // point and four 5 mm ribs, not pumpkin ribbed
    // 108×70 mm. u < 0.38 paints ripe cream skin;
    // the second fruit is a shorter sibling.
    private Transform MakeSweetDumpling(Vector3 foot, float face, float u)
    {
        float side = face >= 0f ? 1f : -1f;
        GameObject pivot = new GameObject("YardSweetDumpling");
        pivot.transform.SetParent(transform, false);
        pivot.transform.position = foot;

        bool ripe = u < 0.38f;
        Color crown = ripe
            ? new Color(0.34f, 0.52f, 0.16f, 1f)
            : new Color(0.30f, 0.54f, 0.18f, 1f);
        Color vine = ripe
            ? new Color(0.36f, 0.56f, 0.18f, 1f)
            : new Color(0.32f, 0.58f, 0.20f, 1f);
        Color groove = ripe
            ? new Color(0.26f, 0.44f, 0.12f, 1f)
            : new Color(0.24f, 0.48f, 0.14f, 1f);
        Color leaf = ripe
            ? new Color(0.28f, 0.56f, 0.16f, 1f)
            : new Color(0.26f, 0.58f, 0.18f, 1f);
        Color skin = ripe
            ? new Color(0.94f, 0.88f, 0.70f, 1f)
            : new Color(0.72f, 0.76f, 0.42f, 1f);
        Color rib = ripe
            ? new Color(0.18f, 0.38f, 0.12f, 1f)
            : new Color(0.22f, 0.46f, 0.14f, 1f);
        Color belly = ripe
            ? new Color(0.98f, 0.92f, 0.78f, 1f)
            : new Color(0.82f, 0.84f, 0.50f, 1f);
        Color gloss = ripe
            ? new Color(0.99f, 0.96f, 0.86f, 1f)
            : new Color(0.88f, 0.90f, 0.58f, 1f);

        AddSweetDumplingHead(
            pivot.transform,
            -0.012f * side,
            0.074f + u * 0.010f,
            -6f * side,
            side,
            crown,
            vine,
            groove,
            leaf,
            skin,
            rib,
            belly,
            gloss,
            true);
        AddSweetDumplingHead(
            pivot.transform,
            0.034f * side,
            0.056f + u * 0.008f,
            8f * side,
            side,
            crown,
            vine,
            groove,
            leaf,
            skin,
            rib,
            belly,
            gloss,
            false);
        return pivot.transform;
    }

    // Kabocha sits ON the slab. Crown at 0.52·H
    // so the vine foot is dirt, not a hanging
    // bottle. Silhouette is a SQUAT DARK-GREEN
    // globe with PALE SPECKLES — 42×26 mm flattened
    // globe plus four 6–8 mm pale speckles, not
    // sweet dumpling 28×26 mm plus four 3 mm
    // dark-green ribs, not carnival flattened
    // 38×34 mm plus orange-green blotches 10–14 mm,
    // not delicata oblong 50×28 mm plus four 5 mm
    // dark-green stripes, not spaghetti oval 56×26 mm
    // plus two faint cream streaks, not pumpkin
    // ribbed 108×70 mm. u < 0.38 paints ripe dark-
    // green skin; the second fruit is a shorter
    // sibling.
    private Transform MakeKabocha(Vector3 foot, float face, float u)
    {
        float side = face >= 0f ? 1f : -1f;
        GameObject pivot = new GameObject("YardKabocha");
        pivot.transform.SetParent(transform, false);
        pivot.transform.position = foot;

        bool ripe = u < 0.38f;
        Color crown = ripe
            ? new Color(0.28f, 0.48f, 0.14f, 1f)
            : new Color(0.26f, 0.50f, 0.16f, 1f);
        Color vine = ripe
            ? new Color(0.30f, 0.52f, 0.16f, 1f)
            : new Color(0.28f, 0.54f, 0.18f, 1f);
        Color groove = ripe
            ? new Color(0.20f, 0.38f, 0.10f, 1f)
            : new Color(0.22f, 0.42f, 0.12f, 1f);
        Color leaf = ripe
            ? new Color(0.24f, 0.50f, 0.14f, 1f)
            : new Color(0.22f, 0.52f, 0.16f, 1f);
        Color skin = ripe
            ? new Color(0.16f, 0.38f, 0.12f, 1f)
            : new Color(0.28f, 0.48f, 0.16f, 1f);
        Color speckle = ripe
            ? new Color(0.72f, 0.78f, 0.50f, 1f)
            : new Color(0.58f, 0.66f, 0.32f, 1f);
        Color belly = ripe
            ? new Color(0.22f, 0.44f, 0.16f, 1f)
            : new Color(0.34f, 0.54f, 0.20f, 1f);
        Color gloss = ripe
            ? new Color(0.32f, 0.52f, 0.22f, 1f)
            : new Color(0.40f, 0.58f, 0.24f, 1f);

        AddKabochaHead(
            pivot.transform,
            -0.012f * side,
            0.074f + u * 0.010f,
            -6f * side,
            side,
            crown,
            vine,
            groove,
            leaf,
            skin,
            speckle,
            belly,
            gloss,
            true);
        AddKabochaHead(
            pivot.transform,
            0.034f * side,
            0.056f + u * 0.008f,
            8f * side,
            side,
            crown,
            vine,
            groove,
            leaf,
            skin,
            speckle,
            belly,
            gloss,
            false);
        return pivot.transform;
    }

    private static void AddCattailStem(
        Transform parent,
        float x,
        float h,
        float tilt,
        Color stem,
        Color blade,
        Color head)
    {
        GameObject go = new GameObject("Stem");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(x, 0f, 0f);
        if (tilt * tilt > 0.01f)
            go.transform.localRotation = Quaternion.Euler(0f, 0f, tilt);

        AddHousePart(
            go.transform,
            "Shaft",
            new Vector3(0f, h * 0.5f, 0f),
            new Vector2(0.014f, h),
            0f,
            stem,
            CattailOrder,
            HumanVisualShape.Box);
        AddHousePart(
            go.transform,
            "Blade",
            new Vector3(0.016f, h * 0.55f, 0f),
            new Vector2(0.010f, h * 0.42f),
            -18f,
            blade,
            CattailOrder,
            HumanVisualShape.Box);
        AddHousePart(
            go.transform,
            "Head",
            new Vector3(0f, h - 0.028f, 0f),
            new Vector2(0.022f, 0.056f),
            0f,
            head,
            CattailOrder + 1,
            HumanVisualShape.Ellipse);
    }

    private static void AddDandelionStem(
        Transform parent,
        float x,
        float h,
        float tilt,
        Color stem,
        Color leaf,
        Color head,
        bool seedClock)
    {
        GameObject go = new GameObject("Stem");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(x, 0f, 0f);
        if (tilt * tilt > 0.01f)
            go.transform.localRotation = Quaternion.Euler(0f, 0f, tilt);

        AddHousePart(
            go.transform,
            "Shaft",
            new Vector3(0f, h * 0.5f, 0f),
            new Vector2(0.010f, h),
            0f,
            stem,
            DandelionOrder,
            HumanVisualShape.Box);
        AddHousePart(
            go.transform,
            "Leaf",
            new Vector3(0.012f, h * 0.28f, 0f),
            new Vector2(0.028f, 0.010f),
            -22f,
            leaf,
            DandelionOrder,
            HumanVisualShape.Ellipse);
        float headW = seedClock ? 0.036f : 0.026f;
        float headH = seedClock ? 0.034f : 0.024f;
        AddHousePart(
            go.transform,
            seedClock ? "Clock" : "Bloom",
            new Vector3(0f, h + headH * 0.35f, 0f),
            new Vector2(headW, headH),
            0f,
            head,
            DandelionOrder + 1,
            HumanVisualShape.Ellipse);
    }

    private static void AddMushroomStem(
        Transform parent,
        float x,
        float h,
        float tilt,
        float capW,
        float capH,
        Color stem,
        Color gill,
        Color cap,
        bool spots,
        Color spot)
    {
        GameObject go = new GameObject("Stem");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(x, 0f, 0f);
        if (tilt * tilt > 0.01f)
            go.transform.localRotation = Quaternion.Euler(0f, 0f, tilt);

        AddHousePart(
            go.transform,
            "Shaft",
            new Vector3(0f, h * 0.5f, 0f),
            new Vector2(0.010f, h),
            0f,
            stem,
            MushroomOrder,
            HumanVisualShape.Box);
        AddHousePart(
            go.transform,
            "Gill",
            new Vector3(0f, h + capH * 0.08f, 0f),
            new Vector2(capW * 0.82f, capH * 0.42f),
            0f,
            gill,
            MushroomOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Cap",
            new Vector3(0f, h + capH * 0.42f, 0f),
            new Vector2(capW, capH),
            0f,
            cap,
            MushroomOrder + 1,
            HumanVisualShape.Ellipse);
        if (!spots)
            return;

        AddHousePart(
            go.transform,
            "SpotA",
            new Vector3(-capW * 0.18f, h + capH * 0.48f, 0f),
            new Vector2(0.010f, 0.007f),
            0f,
            spot,
            MushroomOrder + 2,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "SpotB",
            new Vector3(capW * 0.16f, h + capH * 0.38f, 0f),
            new Vector2(0.008f, 0.006f),
            0f,
            spot,
            MushroomOrder + 2,
            HumanVisualShape.Ellipse);
    }

    private static void AddIvyLeaf(
        Transform parent,
        float x,
        float y,
        float tilt,
        float span,
        Color tint)
    {
        AddHousePart(
            parent,
            "Leaf",
            new Vector3(x, y, 0f),
            new Vector2(span, span * 0.52f),
            tilt,
            tint,
            IvyOrder + 1,
            HumanVisualShape.Ellipse);
    }

    private static void AddFernFrond(
        Transform parent,
        float x,
        float h,
        float tilt,
        Color rachis,
        Color leaflet,
        Color pale)
    {
        GameObject go = new GameObject("Frond");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(x, 0f, 0f);
        if (tilt * tilt > 0.01f)
            go.transform.localRotation = Quaternion.Euler(0f, 0f, tilt);

        AddHousePart(
            go.transform,
            "Rachis",
            new Vector3(0f, h * 0.5f, 0f),
            new Vector2(0.009f, h),
            0f,
            rachis,
            FernOrder,
            HumanVisualShape.Box);

        AddFernPinna(go.transform, -0.018f, h * 0.28f, -32f, 0.034f, leaflet);
        AddFernPinna(go.transform, 0.020f, h * 0.46f, 28f, 0.030f, pale);
        AddFernPinna(go.transform, -0.016f, h * 0.64f, -26f, 0.026f, leaflet);
        AddFernPinna(go.transform, 0.014f, h * 0.82f, 22f, 0.020f, pale);
    }

    private static void AddCloverStem(
        Transform parent,
        float x,
        float h,
        float tilt,
        Color stem,
        Color leaflet,
        Color pale,
        Color bloom,
        bool flower)
    {
        GameObject go = new GameObject("Stem");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(x, 0f, 0f);
        if (tilt * tilt > 0.01f)
            go.transform.localRotation = Quaternion.Euler(0f, 0f, tilt);

        AddHousePart(
            go.transform,
            "Shaft",
            new Vector3(0f, h * 0.5f, 0f),
            new Vector2(0.008f, h),
            0f,
            stem,
            CloverOrder,
            HumanVisualShape.Box);
        AddHousePart(
            go.transform,
            "Leaflet",
            new Vector3(-0.014f, h * 0.42f, 0f),
            new Vector2(0.022f, 0.016f),
            -28f,
            leaflet,
            CloverOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Leaflet",
            new Vector3(0.014f, h * 0.40f, 0f),
            new Vector2(0.020f, 0.015f),
            24f,
            pale,
            CloverOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Leaflet",
            new Vector3(0.002f, h * 0.56f, 0f),
            new Vector2(0.018f, 0.014f),
            6f,
            leaflet,
            CloverOrder + 1,
            HumanVisualShape.Ellipse);
        if (!flower)
            return;

        AddHousePart(
            go.transform,
            "Bloom",
            new Vector3(0f, h + 0.008f, 0f),
            new Vector2(0.016f, 0.014f),
            0f,
            bloom,
            CloverOrder + 2,
            HumanVisualShape.Ellipse);
    }

    private static void AddThistleStem(
        Transform parent,
        float x,
        float h,
        float tilt,
        Color stem,
        Color leaf,
        Color pale,
        Color head,
        Color bract,
        bool bloom)
    {
        GameObject go = new GameObject("Stem");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(x, 0f, 0f);
        if (tilt * tilt > 0.01f)
            go.transform.localRotation = Quaternion.Euler(0f, 0f, tilt);

        AddHousePart(
            go.transform,
            "Shaft",
            new Vector3(0f, h * 0.5f, 0f),
            new Vector2(0.010f, h),
            0f,
            stem,
            ThistleOrder,
            HumanVisualShape.Box);
        AddHousePart(
            go.transform,
            "Leaf",
            new Vector3(-0.018f, h * 0.28f, 0f),
            new Vector2(0.036f, 0.012f),
            -38f,
            leaf,
            ThistleOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Leaf",
            new Vector3(0.020f, h * 0.46f, 0f),
            new Vector2(0.032f, 0.011f),
            34f,
            pale,
            ThistleOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Leaf",
            new Vector3(-0.016f, h * 0.64f, 0f),
            new Vector2(0.026f, 0.010f),
            -30f,
            leaf,
            ThistleOrder,
            HumanVisualShape.Ellipse);
        if (!bloom)
            return;

        AddHousePart(
            go.transform,
            "Bract",
            new Vector3(0f, h + 0.006f, 0f),
            new Vector2(0.020f, 0.012f),
            0f,
            bract,
            ThistleOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Head",
            new Vector3(0f, h + 0.018f, 0f),
            new Vector2(0.028f, 0.026f),
            0f,
            head,
            ThistleOrder + 2,
            HumanVisualShape.Ellipse);
    }

    private static void AddPlantainSpike(
        Transform parent,
        float x,
        float h,
        float tilt,
        Color stem,
        Color seed,
        bool head)
    {
        GameObject go = new GameObject("Spike");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(x, 0f, 0f);
        if (tilt * tilt > 0.01f)
            go.transform.localRotation = Quaternion.Euler(0f, 0f, tilt);

        AddHousePart(
            go.transform,
            "Shaft",
            new Vector3(0f, h * 0.5f, 0f),
            new Vector2(0.007f, h),
            0f,
            stem,
            PlantainOrder,
            HumanVisualShape.Box);
        if (!head)
            return;

        AddHousePart(
            go.transform,
            "Seed",
            new Vector3(0f, h * 0.78f, 0f),
            new Vector2(0.014f, h * 0.38f),
            0f,
            seed,
            PlantainOrder + 1,
            HumanVisualShape.Ellipse);
    }

    private static void AddNettleStem(
        Transform parent,
        float x,
        float h,
        float tilt,
        Color stem,
        Color leaf,
        Color pale,
        Color catkin,
        bool tassels)
    {
        GameObject go = new GameObject("Stem");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(x, 0f, 0f);
        if (tilt * tilt > 0.01f)
            go.transform.localRotation = Quaternion.Euler(0f, 0f, tilt);

        AddHousePart(
            go.transform,
            "Shaft",
            new Vector3(0f, h * 0.5f, 0f),
            new Vector2(0.009f, h),
            0f,
            stem,
            NettleOrder,
            HumanVisualShape.Box);
        AddHousePart(
            go.transform,
            "Leaf",
            new Vector3(-0.022f, h * 0.34f, 0f),
            new Vector2(0.036f, 0.016f),
            -28f,
            leaf,
            NettleOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Tooth",
            new Vector3(-0.034f, h * 0.30f, 0f),
            new Vector2(0.012f, 0.008f),
            -40f,
            pale,
            NettleOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Leaf",
            new Vector3(0.020f, h * 0.38f, 0f),
            new Vector2(0.032f, 0.014f),
            24f,
            pale,
            NettleOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Leaf",
            new Vector3(-0.018f, h * 0.62f, 0f),
            new Vector2(0.030f, 0.014f),
            -22f,
            pale,
            NettleOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Leaf",
            new Vector3(0.016f, h * 0.66f, 0f),
            new Vector2(0.028f, 0.012f),
            18f,
            leaf,
            NettleOrder,
            HumanVisualShape.Ellipse);
        if (!tassels)
            return;

        AddHousePart(
            go.transform,
            "Catkin",
            new Vector3(-0.010f, h * 0.78f, 0f),
            new Vector2(0.008f, 0.028f),
            18f,
            catkin,
            NettleOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Catkin",
            new Vector3(0.012f, h * 0.84f, 0f),
            new Vector2(0.007f, 0.024f),
            -16f,
            catkin,
            NettleOrder + 1,
            HumanVisualShape.Ellipse);
    }

    private static void AddDockStem(
        Transform parent,
        float x,
        float h,
        float tilt,
        Color stem,
        Color leaf,
        Color pale,
        Color rust,
        bool panicle)
    {
        GameObject go = new GameObject("Stem");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(x, 0f, 0f);
        if (tilt * tilt > 0.01f)
            go.transform.localRotation = Quaternion.Euler(0f, 0f, tilt);

        AddHousePart(
            go.transform,
            "Shaft",
            new Vector3(0f, h * 0.5f, 0f),
            new Vector2(0.010f, h),
            0f,
            stem,
            DockOrder,
            HumanVisualShape.Box);
        AddHousePart(
            go.transform,
            "Basal",
            new Vector3(-0.028f, h * 0.18f, 0f),
            new Vector2(0.052f, 0.018f),
            -18f,
            leaf,
            DockOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Basal",
            new Vector3(0.024f, h * 0.22f, 0f),
            new Vector2(0.046f, 0.016f),
            16f,
            pale,
            DockOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Leaf",
            new Vector3(-0.016f, h * 0.48f, 0f),
            new Vector2(0.036f, 0.013f),
            -12f,
            pale,
            DockOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Leaf",
            new Vector3(0.014f, h * 0.56f, 0f),
            new Vector2(0.030f, 0.012f),
            10f,
            leaf,
            DockOrder,
            HumanVisualShape.Ellipse);
        if (!panicle)
            return;

        AddHousePart(
            go.transform,
            "Panicle",
            new Vector3(0f, h * 0.86f, 0f),
            new Vector2(0.016f, h * 0.28f),
            0f,
            rust,
            DockOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Seed",
            new Vector3(-0.010f, h * 0.80f, 0f),
            new Vector2(0.010f, 0.018f),
            14f,
            rust,
            DockOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Seed",
            new Vector3(0.010f, h * 0.90f, 0f),
            new Vector2(0.009f, 0.016f),
            -12f,
            rust,
            DockOrder + 1,
            HumanVisualShape.Ellipse);
    }

    private static void AddBrambleCane(
        Transform parent,
        float x,
        float h,
        float tilt,
        Color stem,
        Color leaf,
        Color pale,
        Color berry,
        bool fruit)
    {
        GameObject go = new GameObject("Cane");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(x, 0f, 0f);
        if (tilt * tilt > 0.01f)
            go.transform.localRotation = Quaternion.Euler(0f, 0f, tilt);

        AddHousePart(
            go.transform,
            "Shaft",
            new Vector3(0f, h * 0.5f, 0f),
            new Vector2(0.009f, h),
            0f,
            stem,
            BrambleOrder,
            HumanVisualShape.Box);
        AddHousePart(
            go.transform,
            "Leaflet",
            new Vector3(-0.018f, h * 0.32f, 0f),
            new Vector2(0.028f, 0.012f),
            -22f,
            leaf,
            BrambleOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Leaflet",
            new Vector3(0.016f, h * 0.46f, 0f),
            new Vector2(0.024f, 0.011f),
            18f,
            pale,
            BrambleOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Leaflet",
            new Vector3(-0.014f, h * 0.62f, 0f),
            new Vector2(0.022f, 0.010f),
            -16f,
            leaf,
            BrambleOrder,
            HumanVisualShape.Ellipse);

        GameObject bow = new GameObject("Bow");
        bow.transform.SetParent(go.transform, false);
        bow.transform.localPosition = new Vector3(0.006f, h * 0.58f, 0f);
        float droop = tilt >= 0f ? 26f : -26f;
        bow.transform.localRotation = Quaternion.Euler(0f, 0f, droop);
        float bowH = h * 0.48f;
        AddHousePart(
            bow.transform,
            "Shaft",
            new Vector3(0f, bowH * 0.5f, 0f),
            new Vector2(0.008f, bowH),
            0f,
            stem,
            BrambleOrder,
            HumanVisualShape.Box);
        AddHousePart(
            bow.transform,
            "Leaflet",
            new Vector3(-0.012f, bowH * 0.42f, 0f),
            new Vector2(0.020f, 0.009f),
            -20f,
            pale,
            BrambleOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            bow.transform,
            "Leaflet",
            new Vector3(0.012f, bowH * 0.72f, 0f),
            new Vector2(0.018f, 0.008f),
            14f,
            leaf,
            BrambleOrder,
            HumanVisualShape.Ellipse);
        if (!fruit)
            return;

        AddHousePart(
            bow.transform,
            "Berry",
            new Vector3(0.002f, bowH * 0.88f, 0f),
            new Vector2(0.012f, 0.012f),
            0f,
            berry,
            BrambleOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            bow.transform,
            "Berry",
            new Vector3(-0.008f, bowH * 0.80f, 0f),
            new Vector2(0.010f, 0.010f),
            0f,
            berry,
            BrambleOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            bow.transform,
            "Berry",
            new Vector3(0.010f, bowH * 0.76f, 0f),
            new Vector2(0.009f, 0.009f),
            0f,
            berry,
            BrambleOrder + 1,
            HumanVisualShape.Ellipse);
    }

    private static void AddHollyhockStem(
        Transform parent,
        float x,
        float h,
        float tilt,
        Color stem,
        Color leaf,
        Color pale,
        Color rose,
        Color blush,
        bool bloom)
    {
        GameObject go = new GameObject("Stem");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(x, 0f, 0f);
        if (tilt * tilt > 0.01f)
            go.transform.localRotation = Quaternion.Euler(0f, 0f, tilt);

        AddHousePart(
            go.transform,
            "Shaft",
            new Vector3(0f, h * 0.5f, 0f),
            new Vector2(0.011f, h),
            0f,
            stem,
            HollyhockOrder,
            HumanVisualShape.Box);
        AddHousePart(
            go.transform,
            "Leaf",
            new Vector3(-0.028f, h * 0.16f, 0f),
            new Vector2(0.052f, 0.018f),
            -28f,
            leaf,
            HollyhockOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Leaf",
            new Vector3(0.026f, h * 0.22f, 0f),
            new Vector2(0.046f, 0.016f),
            24f,
            pale,
            HollyhockOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Leaf",
            new Vector3(-0.020f, h * 0.36f, 0f),
            new Vector2(0.036f, 0.014f),
            -22f,
            leaf,
            HollyhockOrder,
            HumanVisualShape.Ellipse);
        if (!bloom)
            return;

        AddHousePart(
            go.transform,
            "Bloom",
            new Vector3(-0.008f, h * 0.58f, 0f),
            new Vector2(0.030f, 0.026f),
            -8f,
            rose,
            HollyhockOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Bloom",
            new Vector3(0.010f, h * 0.70f, 0f),
            new Vector2(0.028f, 0.024f),
            6f,
            blush,
            HollyhockOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Bloom",
            new Vector3(-0.006f, h * 0.82f, 0f),
            new Vector2(0.026f, 0.022f),
            -4f,
            rose,
            HollyhockOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Bloom",
            new Vector3(0.004f, h * 0.94f, 0f),
            new Vector2(0.022f, 0.020f),
            3f,
            blush,
            HollyhockOrder + 2,
            HumanVisualShape.Ellipse);
    }

    private static void AddRoseCane(
        Transform parent,
        float x,
        float h,
        float tilt,
        Color wood,
        Color leaf,
        Color pale,
        Color bloom,
        Color blush,
        Color eye,
        bool flower)
    {
        GameObject go = new GameObject("Cane");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(x, 0f, 0f);
        if (tilt * tilt > 0.01f)
            go.transform.localRotation = Quaternion.Euler(0f, 0f, tilt);

        AddHousePart(
            go.transform,
            "Wood",
            new Vector3(0f, h * 0.5f, 0f),
            new Vector2(0.010f, h),
            0f,
            wood,
            RoseOrder,
            HumanVisualShape.Box);
        AddHousePart(
            go.transform,
            "Skirt",
            new Vector3(0.004f, h * 0.42f, 0f),
            new Vector2(0.072f, 0.048f),
            -8f,
            leaf,
            RoseOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Leaf",
            new Vector3(-0.028f, h * 0.28f, 0f),
            new Vector2(0.030f, 0.014f),
            -32f,
            pale,
            RoseOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Leaf",
            new Vector3(0.026f, h * 0.34f, 0f),
            new Vector2(0.028f, 0.013f),
            26f,
            leaf,
            RoseOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Leaf",
            new Vector3(-0.018f, h * 0.56f, 0f),
            new Vector2(0.024f, 0.012f),
            -18f,
            pale,
            RoseOrder,
            HumanVisualShape.Ellipse);
        if (!flower)
            return;

        AddHousePart(
            go.transform,
            "Bloom",
            new Vector3(-0.010f, h * 0.72f, 0f),
            new Vector2(0.028f, 0.026f),
            -6f,
            bloom,
            RoseOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Eye",
            new Vector3(-0.010f, h * 0.72f, 0f),
            new Vector2(0.008f, 0.007f),
            0f,
            eye,
            RoseOrder + 2,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Bloom",
            new Vector3(0.012f, h * 0.86f, 0f),
            new Vector2(0.024f, 0.022f),
            8f,
            blush,
            RoseOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Eye",
            new Vector3(0.012f, h * 0.86f, 0f),
            new Vector2(0.007f, 0.006f),
            0f,
            eye,
            RoseOrder + 2,
            HumanVisualShape.Ellipse);
    }

    private static void AddLavenderTuft(
        Transform parent,
        float x,
        float h,
        float tilt,
        Color stem,
        Color leaf,
        Color pale,
        Color spike,
        Color blush,
        bool bloom)
    {
        GameObject go = new GameObject("Tuft");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(x, 0f, 0f);
        if (tilt * tilt > 0.01f)
            go.transform.localRotation = Quaternion.Euler(0f, 0f, tilt);

        AddHousePart(
            go.transform,
            "Wood",
            new Vector3(0f, h * 0.38f, 0f),
            new Vector2(0.008f, h * 0.76f),
            0f,
            stem,
            LavenderOrder,
            HumanVisualShape.Box);
        AddHousePart(
            go.transform,
            "Mound",
            new Vector3(0.002f, h * 0.36f, 0f),
            new Vector2(0.056f, 0.040f),
            -6f,
            leaf,
            LavenderOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Needle",
            new Vector3(-0.022f, h * 0.22f, 0f),
            new Vector2(0.026f, 0.008f),
            -38f,
            pale,
            LavenderOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Needle",
            new Vector3(0.020f, h * 0.28f, 0f),
            new Vector2(0.024f, 0.007f),
            32f,
            leaf,
            LavenderOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Needle",
            new Vector3(-0.014f, h * 0.48f, 0f),
            new Vector2(0.020f, 0.007f),
            -24f,
            pale,
            LavenderOrder,
            HumanVisualShape.Ellipse);
        if (!bloom)
            return;

        AddHousePart(
            go.transform,
            "Spike",
            new Vector3(-0.006f, h * 0.78f, 0f),
            new Vector2(0.010f, 0.028f),
            -4f,
            spike,
            LavenderOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Spike",
            new Vector3(0.008f, h * 0.92f, 0f),
            new Vector2(0.009f, 0.024f),
            6f,
            blush,
            LavenderOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Spike",
            new Vector3(0.002f, h * 1.04f, 0f),
            new Vector2(0.008f, 0.018f),
            -2f,
            spike,
            LavenderOrder + 2,
            HumanVisualShape.Ellipse);
    }

    private static void AddIrisFan(
        Transform parent,
        float x,
        float h,
        float tilt,
        Color stem,
        Color leaf,
        Color pale,
        Color fall,
        Color flag,
        Color beard,
        bool bloom)
    {
        GameObject go = new GameObject("Fan");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(x, 0f, 0f);
        if (tilt * tilt > 0.01f)
            go.transform.localRotation = Quaternion.Euler(0f, 0f, tilt);

        AddHousePart(
            go.transform,
            "Sword",
            new Vector3(-0.012f, h * 0.46f, 0f),
            new Vector2(0.012f, h * 0.92f),
            -16f,
            leaf,
            IrisOrder,
            HumanVisualShape.Box);
        AddHousePart(
            go.transform,
            "Sword",
            new Vector3(0.010f, h * 0.40f, 0f),
            new Vector2(0.011f, h * 0.80f),
            14f,
            pale,
            IrisOrder,
            HumanVisualShape.Box);
        AddHousePart(
            go.transform,
            "Sword",
            new Vector3(-0.004f, h * 0.34f, 0f),
            new Vector2(0.010f, h * 0.68f),
            -6f,
            leaf,
            IrisOrder,
            HumanVisualShape.Box);
        AddHousePart(
            go.transform,
            "Scape",
            new Vector3(0.002f, h * 0.52f, 0f),
            new Vector2(0.008f, h),
            0f,
            stem,
            IrisOrder,
            HumanVisualShape.Box);
        if (!bloom)
            return;

        AddHousePart(
            go.transform,
            "Fall",
            new Vector3(0.014f, h * 0.90f, 0f),
            new Vector2(0.028f, 0.016f),
            -38f,
            fall,
            IrisOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Standard",
            new Vector3(-0.004f, h * 1.08f, 0f),
            new Vector2(0.016f, 0.024f),
            -8f,
            flag,
            IrisOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Beard",
            new Vector3(0.008f, h * 0.96f, 0f),
            new Vector2(0.010f, 0.006f),
            -12f,
            beard,
            IrisOrder + 2,
            HumanVisualShape.Ellipse);
    }

    private static void AddTulipStem(
        Transform parent,
        float x,
        float h,
        float tilt,
        Color stem,
        Color leaf,
        Color pale,
        Color cup,
        Color throat,
        bool bloom)
    {
        GameObject go = new GameObject("Stem");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(x, 0f, 0f);
        if (tilt * tilt > 0.01f)
            go.transform.localRotation = Quaternion.Euler(0f, 0f, tilt);

        AddHousePart(
            go.transform,
            "Lance",
            new Vector3(-0.014f, h * 0.38f, 0f),
            new Vector2(0.016f, h * 0.58f),
            -22f,
            leaf,
            TulipOrder,
            HumanVisualShape.Box);
        AddHousePart(
            go.transform,
            "Lance",
            new Vector3(0.012f, h * 0.30f, 0f),
            new Vector2(0.014f, h * 0.44f),
            18f,
            pale,
            TulipOrder,
            HumanVisualShape.Box);
        AddHousePart(
            go.transform,
            "Scape",
            new Vector3(0f, h * 0.5f, 0f),
            new Vector2(0.008f, h),
            0f,
            stem,
            TulipOrder,
            HumanVisualShape.Box);
        if (!bloom)
            return;

        AddHousePart(
            go.transform,
            "Cup",
            new Vector3(0f, h + 0.016f, 0f),
            new Vector2(0.032f, 0.044f),
            0f,
            cup,
            TulipOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Throat",
            new Vector3(0.002f, h + 0.022f, 0f),
            new Vector2(0.016f, 0.026f),
            -4f,
            throat,
            TulipOrder + 2,
            HumanVisualShape.Ellipse);
    }

    private static void AddDaffodilStem(
        Transform parent,
        float x,
        float h,
        float tilt,
        float side,
        Color stem,
        Color leaf,
        Color pale,
        Color petal,
        Color corona,
        Color rim,
        bool bloom)
    {
        GameObject go = new GameObject("Stem");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(x, 0f, 0f);
        if (tilt * tilt > 0.01f)
            go.transform.localRotation = Quaternion.Euler(0f, 0f, tilt);

        AddHousePart(
            go.transform,
            "Strap",
            new Vector3(-0.012f, h * 0.42f, 0f),
            new Vector2(0.012f, h * 0.78f),
            -8f,
            leaf,
            DaffodilOrder,
            HumanVisualShape.Box);
        AddHousePart(
            go.transform,
            "Strap",
            new Vector3(0.010f, h * 0.36f, 0f),
            new Vector2(0.011f, h * 0.64f),
            10f,
            pale,
            DaffodilOrder,
            HumanVisualShape.Box);
        AddHousePart(
            go.transform,
            "Scape",
            new Vector3(0f, h * 0.5f, 0f),
            new Vector2(0.007f, h),
            0f,
            stem,
            DaffodilOrder,
            HumanVisualShape.Box);
        if (!bloom)
            return;

        AddHousePart(
            go.transform,
            "Perianth",
            new Vector3(0.002f * side, h + 0.008f, 0f),
            new Vector2(0.038f, 0.020f),
            -6f * side,
            petal,
            DaffodilOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Corona",
            new Vector3(0.016f * side, h + 0.010f, 0f),
            new Vector2(0.026f, 0.016f),
            0f,
            corona,
            DaffodilOrder + 2,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Rim",
            new Vector3(0.028f * side, h + 0.010f, 0f),
            new Vector2(0.008f, 0.018f),
            0f,
            rim,
            DaffodilOrder + 2,
            HumanVisualShape.Ellipse);
    }

    private static void AddDaisyStem(
        Transform parent,
        float x,
        float h,
        float tilt,
        float side,
        Color stem,
        Color leaf,
        Color pale,
        Color ray,
        Color disk,
        bool bloom)
    {
        GameObject go = new GameObject("Stem");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(x, 0f, 0f);
        if (tilt * tilt > 0.01f)
            go.transform.localRotation = Quaternion.Euler(0f, 0f, tilt);

        AddHousePart(
            go.transform,
            "Lance",
            new Vector3(-0.016f, h * 0.28f, 0f),
            new Vector2(0.028f, 0.012f),
            -18f,
            leaf,
            DaisyOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Lance",
            new Vector3(0.014f, h * 0.22f, 0f),
            new Vector2(0.024f, 0.011f),
            16f,
            pale,
            DaisyOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Scape",
            new Vector3(0f, h * 0.5f, 0f),
            new Vector2(0.007f, h),
            0f,
            stem,
            DaisyOrder,
            HumanVisualShape.Box);
        if (!bloom)
            return;

        AddHousePart(
            go.transform,
            "RayUp",
            new Vector3(0.002f * side, h + 0.028f, 0f),
            new Vector2(0.012f, 0.024f),
            8f * side,
            ray,
            DaisyOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "RayBack",
            new Vector3(-0.016f * side, h + 0.012f, 0f),
            new Vector2(0.022f, 0.014f),
            -12f * side,
            ray,
            DaisyOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "RayFore",
            new Vector3(0.018f * side, h + 0.012f, 0f),
            new Vector2(0.024f, 0.014f),
            10f * side,
            ray,
            DaisyOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "RayDown",
            new Vector3(0.002f * side, h + 0.002f, 0f),
            new Vector2(0.012f, 0.016f),
            -6f * side,
            ray,
            DaisyOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Disk",
            new Vector3(0.006f * side, h + 0.014f, 0f),
            new Vector2(0.022f, 0.020f),
            0f,
            disk,
            DaisyOrder + 2,
            HumanVisualShape.Ellipse);
    }

    private static void AddPoppyStem(
        Transform parent,
        float x,
        float h,
        float tilt,
        float side,
        Color stem,
        Color leaf,
        Color pale,
        Color petal,
        Color crease,
        Color blotch,
        bool bloom)
    {
        GameObject go = new GameObject("Stem");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(x, 0f, 0f);
        if (tilt * tilt > 0.01f)
            go.transform.localRotation = Quaternion.Euler(0f, 0f, tilt);

        AddHousePart(
            go.transform,
            "Lobe",
            new Vector3(-0.018f, h * 0.24f, 0f),
            new Vector2(0.034f, 0.014f),
            -28f,
            leaf,
            PoppyOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Lobe",
            new Vector3(0.016f, h * 0.18f, 0f),
            new Vector2(0.028f, 0.012f),
            22f,
            pale,
            PoppyOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Scape",
            new Vector3(0f, h * 0.5f, 0f),
            new Vector2(0.006f, h),
            0f,
            stem,
            PoppyOrder,
            HumanVisualShape.Box);
        if (!bloom)
        {
            AddHousePart(
                go.transform,
                "Bud",
                new Vector3(0.014f * side, h + 0.002f, 0f),
                new Vector2(0.016f, 0.014f),
                48f * side,
                crease,
                PoppyOrder + 1,
                HumanVisualShape.Ellipse);
            return;
        }

        AddHousePart(
            go.transform,
            "Neck",
            new Vector3(0.010f * side, h - 0.006f, 0f),
            new Vector2(0.018f, 0.008f),
            28f * side,
            stem,
            PoppyOrder,
            HumanVisualShape.Box);
        AddHousePart(
            go.transform,
            "PetalBack",
            new Vector3(0.002f * side, h + 0.016f, 0f),
            new Vector2(0.036f, 0.024f),
            -14f * side,
            crease,
            PoppyOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "PetalUp",
            new Vector3(0.008f * side, h + 0.030f, 0f),
            new Vector2(0.024f, 0.018f),
            16f * side,
            petal,
            PoppyOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "PetalFore",
            new Vector3(0.024f * side, h + 0.010f, 0f),
            new Vector2(0.030f, 0.022f),
            32f * side,
            petal,
            PoppyOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "PetalDown",
            new Vector3(0.008f * side, h + 0.000f, 0f),
            new Vector2(0.032f, 0.016f),
            -22f * side,
            crease,
            PoppyOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Blotch",
            new Vector3(0.012f * side, h + 0.014f, 0f),
            new Vector2(0.016f, 0.014f),
            0f,
            blotch,
            PoppyOrder + 2,
            HumanVisualShape.Ellipse);
    }

    private static void AddLilyStem(
        Transform parent,
        float x,
        float h,
        float tilt,
        float side,
        Color stem,
        Color leaf,
        Color pale,
        Color tepal,
        Color blush,
        Color speckle,
        bool bloom)
    {
        GameObject go = new GameObject("Stem");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(x, 0f, 0f);
        if (tilt * tilt > 0.01f)
            go.transform.localRotation = Quaternion.Euler(0f, 0f, tilt);

        AddHousePart(
            go.transform,
            "Lance",
            new Vector3(-0.016f, h * 0.36f, 0f),
            new Vector2(0.014f, h * 0.52f),
            -26f,
            leaf,
            LilyOrder,
            HumanVisualShape.Box);
        AddHousePart(
            go.transform,
            "Lance",
            new Vector3(0.014f, h * 0.58f, 0f),
            new Vector2(0.012f, h * 0.38f),
            20f,
            pale,
            LilyOrder,
            HumanVisualShape.Box);
        AddHousePart(
            go.transform,
            "Scape",
            new Vector3(0f, h * 0.5f, 0f),
            new Vector2(0.007f, h),
            0f,
            stem,
            LilyOrder,
            HumanVisualShape.Box);
        if (!bloom)
        {
            AddHousePart(
                go.transform,
                "Bud",
                new Vector3(0.016f * side, h + 0.006f, 0f),
                new Vector2(0.014f, 0.028f),
                38f * side,
                blush,
                LilyOrder + 1,
                HumanVisualShape.Ellipse);
            return;
        }

        AddHousePart(
            go.transform,
            "TepalBack",
            new Vector3(-0.014f * side, h + 0.018f, 0f),
            new Vector2(0.012f, 0.030f),
            -28f * side,
            tepal,
            LilyOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "TepalUp",
            new Vector3(0.002f * side, h + 0.032f, 0f),
            new Vector2(0.012f, 0.034f),
            -8f * side,
            tepal,
            LilyOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "TepalUpFore",
            new Vector3(0.018f * side, h + 0.024f, 0f),
            new Vector2(0.028f, 0.012f),
            36f * side,
            tepal,
            LilyOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "TepalFore",
            new Vector3(0.030f * side, h + 0.006f, 0f),
            new Vector2(0.032f, 0.012f),
            10f * side,
            tepal,
            LilyOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "TepalDownFore",
            new Vector3(0.016f * side, h - 0.008f, 0f),
            new Vector2(0.026f, 0.012f),
            -40f * side,
            blush,
            LilyOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "TepalDown",
            new Vector3(-0.008f * side, h - 0.004f, 0f),
            new Vector2(0.018f, 0.012f),
            24f * side,
            tepal,
            LilyOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Throat",
            new Vector3(0.008f * side, h + 0.008f, 0f),
            new Vector2(0.016f, 0.014f),
            0f,
            blush,
            LilyOrder + 2,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Spot",
            new Vector3(0.014f * side, h - 0.004f, 0f),
            new Vector2(0.008f, 0.006f),
            -20f * side,
            speckle,
            LilyOrder + 2,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Stamen",
            new Vector3(0.022f * side, h + 0.002f, 0f),
            new Vector2(0.018f, 0.004f),
            8f * side,
            speckle,
            LilyOrder + 2,
            HumanVisualShape.Box);
        AddHousePart(
            go.transform,
            "Anther",
            new Vector3(0.032f * side, h + 0.004f, 0f),
            new Vector2(0.008f, 0.006f),
            0f,
            speckle,
            LilyOrder + 2,
            HumanVisualShape.Ellipse);
    }

    private static void AddSunflowerStem(
        Transform parent,
        float x,
        float h,
        float tilt,
        float side,
        Color stem,
        Color leaf,
        Color pale,
        Color ray,
        Color rim,
        Color disk,
        bool bloom)
    {
        GameObject go = new GameObject("Stem");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(x, 0f, 0f);
        if (tilt * tilt > 0.01f)
            go.transform.localRotation = Quaternion.Euler(0f, 0f, tilt);

        AddHousePart(
            go.transform,
            "Heart",
            new Vector3(-0.022f, h * 0.32f, 0f),
            new Vector2(0.048f, 0.022f),
            -28f,
            leaf,
            SunflowerOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Heart",
            new Vector3(0.020f, h * 0.52f, 0f),
            new Vector2(0.042f, 0.020f),
            22f,
            pale,
            SunflowerOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Scape",
            new Vector3(0f, h * 0.5f, 0f),
            new Vector2(0.014f, h),
            0f,
            stem,
            SunflowerOrder,
            HumanVisualShape.Box);
        if (!bloom)
        {
            AddHousePart(
                go.transform,
                "Bud",
                new Vector3(0.018f * side, h + 0.010f, 0f),
                new Vector2(0.028f, 0.024f),
                32f * side,
                pale,
                SunflowerOrder + 1,
                HumanVisualShape.Ellipse);
            return;
        }

        AddHousePart(
            go.transform,
            "RayUp",
            new Vector3(0.004f * side, h + 0.052f, 0f),
            new Vector2(0.022f, 0.036f),
            8f * side,
            ray,
            SunflowerOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "RayBack",
            new Vector3(-0.034f * side, h + 0.018f, 0f),
            new Vector2(0.040f, 0.022f),
            -14f * side,
            rim,
            SunflowerOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "RayFore",
            new Vector3(0.036f * side, h + 0.018f, 0f),
            new Vector2(0.042f, 0.022f),
            12f * side,
            ray,
            SunflowerOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "RayDown",
            new Vector3(0.004f * side, h - 0.008f, 0f),
            new Vector2(0.022f, 0.028f),
            -6f * side,
            rim,
            SunflowerOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Face",
            new Vector3(0.008f * side, h + 0.020f, 0f),
            new Vector2(0.056f, 0.050f),
            0f,
            ray,
            SunflowerOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Seed",
            new Vector3(0.010f * side, h + 0.020f, 0f),
            new Vector2(0.030f, 0.028f),
            0f,
            disk,
            SunflowerOrder + 2,
            HumanVisualShape.Ellipse);
    }

    private static void AddVioletStem(
        Transform parent,
        float x,
        float h,
        float tilt,
        float side,
        Color stem,
        Color leaf,
        Color pale,
        Color petal,
        Color deep,
        Color throat,
        bool bloom)
    {
        GameObject go = new GameObject("Stem");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(x, 0f, 0f);
        if (tilt * tilt > 0.01f)
            go.transform.localRotation = Quaternion.Euler(0f, 0f, tilt);

        AddHousePart(
            go.transform,
            "Heart",
            new Vector3(-0.018f, h * 0.22f, 0f),
            new Vector2(0.036f, 0.018f),
            -34f,
            leaf,
            VioletOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Heart",
            new Vector3(0.016f, h * 0.16f, 0f),
            new Vector2(0.030f, 0.016f),
            28f,
            pale,
            VioletOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Scape",
            new Vector3(0f, h * 0.5f, 0f),
            new Vector2(0.005f, h),
            0f,
            stem,
            VioletOrder,
            HumanVisualShape.Box);
        if (!bloom)
        {
            AddHousePart(
                go.transform,
                "Bud",
                new Vector3(0.010f * side, h + 0.004f, 0f),
                new Vector2(0.012f, 0.016f),
                36f * side,
                deep,
                VioletOrder + 1,
                HumanVisualShape.Ellipse);
            return;
        }

        AddHousePart(
            go.transform,
            "Spur",
            new Vector3(-0.010f * side, h + 0.004f, 0f),
            new Vector2(0.012f, 0.008f),
            -42f * side,
            deep,
            VioletOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "PetalUp",
            new Vector3(0.002f * side, h + 0.020f, 0f),
            new Vector2(0.016f, 0.018f),
            -8f * side,
            deep,
            VioletOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "PetalBack",
            new Vector3(-0.014f * side, h + 0.010f, 0f),
            new Vector2(0.020f, 0.012f),
            -16f * side,
            deep,
            VioletOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "PetalFore",
            new Vector3(0.016f * side, h + 0.012f, 0f),
            new Vector2(0.022f, 0.012f),
            12f * side,
            petal,
            VioletOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "PetalCheek",
            new Vector3(-0.006f * side, h + 0.000f, 0f),
            new Vector2(0.014f, 0.010f),
            20f * side,
            deep,
            VioletOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "PetalLip",
            new Vector3(0.010f * side, h - 0.002f, 0f),
            new Vector2(0.026f, 0.014f),
            -24f * side,
            petal,
            VioletOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Throat",
            new Vector3(0.006f * side, h + 0.008f, 0f),
            new Vector2(0.012f, 0.010f),
            0f,
            throat,
            VioletOrder + 2,
            HumanVisualShape.Ellipse);
    }

    private static void AddCrocusStem(
        Transform parent,
        float x,
        float h,
        float tilt,
        float side,
        Color stem,
        Color leaf,
        Color pale,
        Color petal,
        Color deep,
        Color throat,
        bool bloom)
    {
        GameObject go = new GameObject("Stem");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(x, 0f, 0f);
        if (tilt * tilt > 0.01f)
            go.transform.localRotation = Quaternion.Euler(0f, 0f, tilt);

        AddHousePart(
            go.transform,
            "Blade",
            new Vector3(-0.012f, h * 0.48f, 0f),
            new Vector2(0.008f, h * 0.96f),
            -12f,
            leaf,
            CrocusOrder,
            HumanVisualShape.Box);
        AddHousePart(
            go.transform,
            "Blade",
            new Vector3(0.010f, h * 0.40f, 0f),
            new Vector2(0.007f, h * 0.78f),
            16f,
            pale,
            CrocusOrder,
            HumanVisualShape.Box);
        AddHousePart(
            go.transform,
            "Scape",
            new Vector3(0f, h * 0.5f, 0f),
            new Vector2(0.006f, h),
            0f,
            stem,
            CrocusOrder,
            HumanVisualShape.Box);
        if (!bloom)
        {
            AddHousePart(
                go.transform,
                "Spear",
                new Vector3(0.004f * side, h + 0.008f, 0f),
                new Vector2(0.010f, 0.022f),
                10f * side,
                deep,
                CrocusOrder + 1,
                HumanVisualShape.Ellipse);
            return;
        }

        AddHousePart(
            go.transform,
            "TepalBack",
            new Vector3(-0.008f * side, h + 0.018f, 0f),
            new Vector2(0.016f, 0.036f),
            -12f * side,
            deep,
            CrocusOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Goblet",
            new Vector3(0.002f * side, h + 0.016f, 0f),
            new Vector2(0.028f, 0.038f),
            0f,
            petal,
            CrocusOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "TepalFore",
            new Vector3(0.012f * side, h + 0.020f, 0f),
            new Vector2(0.014f, 0.032f),
            14f * side,
            petal,
            CrocusOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "TepalTip",
            new Vector3(0.002f * side, h + 0.036f, 0f),
            new Vector2(0.012f, 0.018f),
            0f,
            deep,
            CrocusOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Throat",
            new Vector3(0.004f * side, h + 0.012f, 0f),
            new Vector2(0.012f, 0.016f),
            0f,
            throat,
            CrocusOrder + 2,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Stamen",
            new Vector3(0.006f * side, h + 0.018f, 0f),
            new Vector2(0.006f, 0.010f),
            8f * side,
            throat,
            CrocusOrder + 2,
            HumanVisualShape.Ellipse);
    }

    private static void AddBluebellStem(
        Transform parent,
        float x,
        float h,
        float tilt,
        float side,
        Color stem,
        Color leaf,
        Color pale,
        Color petal,
        Color deep,
        Color mouth,
        bool bloom)
    {
        GameObject go = new GameObject("Stem");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(x, 0f, 0f);
        if (tilt * tilt > 0.01f)
            go.transform.localRotation = Quaternion.Euler(0f, 0f, tilt);

        AddHousePart(
            go.transform,
            "Blade",
            new Vector3(-0.014f, h * 0.42f, 0f),
            new Vector2(0.010f, h * 0.78f),
            -18f,
            leaf,
            BluebellOrder,
            HumanVisualShape.Box);
        AddHousePart(
            go.transform,
            "Blade",
            new Vector3(0.012f, h * 0.34f, 0f),
            new Vector2(0.008f, h * 0.62f),
            14f,
            pale,
            BluebellOrder,
            HumanVisualShape.Box);
        AddHousePart(
            go.transform,
            "Scape",
            new Vector3(0.006f * side, h * 0.52f, 0f),
            new Vector2(0.006f, h),
            12f * side,
            stem,
            BluebellOrder,
            HumanVisualShape.Box);
        if (!bloom)
        {
            AddHousePart(
                go.transform,
                "Bud",
                new Vector3(0.018f * side, h + 0.004f, 0f),
                new Vector2(0.012f, 0.016f),
                28f * side,
                deep,
                BluebellOrder + 1,
                HumanVisualShape.Ellipse);
            return;
        }

        AddHousePart(
            go.transform,
            "Bell",
            new Vector3(0.016f * side, h * 0.78f, 0f),
            new Vector2(0.016f, 0.022f),
            18f * side,
            petal,
            BluebellOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Mouth",
            new Vector3(0.022f * side, h * 0.78f - 0.010f, 0f),
            new Vector2(0.012f, 0.008f),
            8f * side,
            mouth,
            BluebellOrder + 2,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Bell",
            new Vector3(0.028f * side, h * 0.58f, 0f),
            new Vector2(0.018f, 0.024f),
            22f * side,
            petal,
            BluebellOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Mouth",
            new Vector3(0.034f * side, h * 0.58f - 0.011f, 0f),
            new Vector2(0.013f, 0.008f),
            10f * side,
            mouth,
            BluebellOrder + 2,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Bell",
            new Vector3(0.038f * side, h * 0.40f, 0f),
            new Vector2(0.015f, 0.020f),
            26f * side,
            deep,
            BluebellOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Mouth",
            new Vector3(0.044f * side, h * 0.40f - 0.010f, 0f),
            new Vector2(0.011f, 0.007f),
            12f * side,
            mouth,
            BluebellOrder + 2,
            HumanVisualShape.Ellipse);
    }

    private static void AddTomatoVine(
        Transform parent,
        float x,
        float h,
        float tilt,
        float side,
        Color vine,
        Color leaf,
        Color pale,
        Color ripe,
        Color deep,
        Color green,
        Color bloom,
        bool fruit)
    {
        GameObject go = new GameObject("Vine");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(x, 0f, 0f);
        if (tilt * tilt > 0.01f)
            go.transform.localRotation = Quaternion.Euler(0f, 0f, tilt);

        AddHousePart(
            go.transform,
            "Cane",
            new Vector3(0.004f * side, h * 0.52f, 0f),
            new Vector2(0.007f, h),
            4f * side,
            vine,
            TomatoOrder,
            HumanVisualShape.Box);
        AddHousePart(
            go.transform,
            "Leaflet",
            new Vector3(-0.018f, h * 0.28f, 0f),
            new Vector2(0.028f, 0.014f),
            -22f,
            leaf,
            TomatoOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Leaflet",
            new Vector3(0.016f, h * 0.30f, 0f),
            new Vector2(0.024f, 0.012f),
            18f,
            pale,
            TomatoOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Leaflet",
            new Vector3(-0.020f, h * 0.52f, 0f),
            new Vector2(0.030f, 0.015f),
            -16f,
            leaf,
            TomatoOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Leaflet",
            new Vector3(0.018f, h * 0.54f, 0f),
            new Vector2(0.026f, 0.013f),
            14f,
            pale,
            TomatoOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Leaflet",
            new Vector3(0.006f * side, h * 0.76f, 0f),
            new Vector2(0.022f, 0.012f),
            8f * side,
            leaf,
            TomatoOrder,
            HumanVisualShape.Ellipse);

        if (!fruit)
        {
            AddHousePart(
                go.transform,
                "Green",
                new Vector3(0.016f * side, h * 0.46f - 0.010f, 0f),
                new Vector2(0.016f, 0.018f),
                12f * side,
                green,
                TomatoOrder + 1,
                HumanVisualShape.Ellipse);
            AddHousePart(
                go.transform,
                "Bud",
                new Vector3(0.010f * side, h + 0.006f, 0f),
                new Vector2(0.010f, 0.008f),
                18f * side,
                bloom,
                TomatoOrder + 1,
                HumanVisualShape.Ellipse);
            return;
        }

        AddHousePart(
            go.transform,
            "Fruit",
            new Vector3(0.018f * side, h * 0.44f - 0.012f, 0f),
            new Vector2(0.022f, 0.024f),
            10f * side,
            ripe,
            TomatoOrder + 2,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Fruit",
            new Vector3(0.008f * side, h * 0.42f - 0.018f, 0f),
            new Vector2(0.018f, 0.020f),
            -8f * side,
            deep,
            TomatoOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Fruit",
            new Vector3(0.022f * side, h * 0.62f - 0.012f, 0f),
            new Vector2(0.020f, 0.022f),
            14f * side,
            ripe,
            TomatoOrder + 2,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Bloom",
            new Vector3(0.008f * side, h + 0.008f, 0f),
            new Vector2(0.014f, 0.010f),
            6f * side,
            bloom,
            TomatoOrder + 1,
            HumanVisualShape.Ellipse);
    }

    private static void AddCabbageHead(
        Transform parent,
        float x,
        float h,
        float tilt,
        float side,
        Color wrap,
        Color pale,
        Color heart,
        Color vein,
        bool basal)
    {
        GameObject go = new GameObject("Head");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(x, 0f, 0f);
        if (tilt * tilt > 0.01f)
            go.transform.localRotation = Quaternion.Euler(0f, 0f, tilt);

        AddHousePart(
            go.transform,
            "Stub",
            new Vector3(0f, h * 0.12f, 0f),
            new Vector2(0.016f, h * 0.22f),
            0f,
            vein,
            CabbageOrder,
            HumanVisualShape.Box);

        if (basal)
        {
            AddHousePart(
                go.transform,
                "Basal",
                new Vector3(-0.044f * side, h * 0.16f, 0f),
                new Vector2(0.056f, 0.022f),
                -32f * side,
                wrap,
                CabbageOrder,
                HumanVisualShape.Ellipse);
            AddHousePart(
                go.transform,
                "Basal",
                new Vector3(0.040f * side, h * 0.14f, 0f),
                new Vector2(0.050f, 0.020f),
                26f * side,
                pale,
                CabbageOrder,
                HumanVisualShape.Ellipse);
        }

        AddHousePart(
            go.transform,
            "Wrap",
            new Vector3(-0.018f * side, h * 0.50f, 0f),
            new Vector2(0.070f, 0.078f),
            -14f * side,
            wrap,
            CabbageOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Wrap",
            new Vector3(0.016f * side, h * 0.48f, 0f),
            new Vector2(0.066f, 0.074f),
            12f * side,
            pale,
            CabbageOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Wrap",
            new Vector3(0.002f * side, h * 0.56f, 0f),
            new Vector2(0.078f, 0.082f),
            3f * side,
            wrap,
            CabbageOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Heart",
            new Vector3(0.004f * side, h * 0.60f, 0f),
            new Vector2(0.048f, 0.052f),
            5f * side,
            heart,
            CabbageOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Crease",
            new Vector3(0.002f * side, h * 0.62f, 0f),
            new Vector2(0.030f, 0.008f),
            8f * side,
            vein,
            CabbageOrder + 2,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Crown",
            new Vector3(0.006f * side, h * 0.78f, 0f),
            new Vector2(0.040f, 0.028f),
            -6f * side,
            wrap,
            CabbageOrder + 1,
            HumanVisualShape.Ellipse);
    }

    private static void AddPumpkinVine(
        Transform parent,
        float x,
        float h,
        float tilt,
        float side,
        Color vine,
        Color leaf,
        Color pale,
        Color tendril)
    {
        GameObject go = new GameObject("Vine");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(x, 0f, 0f);
        if (tilt * tilt > 0.01f)
            go.transform.localRotation = Quaternion.Euler(0f, 0f, tilt);

        AddHousePart(
            go.transform,
            "Cane",
            new Vector3(-0.028f * side, h * 0.22f, 0f),
            new Vector2(0.088f, 0.010f),
            12f * side,
            vine,
            PumpkinOrder,
            HumanVisualShape.Box);
        AddHousePart(
            go.transform,
            "Leaflet",
            new Vector3(-0.052f * side, h * 0.38f, 0f),
            new Vector2(0.048f, 0.022f),
            -28f * side,
            leaf,
            PumpkinOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Leaflet",
            new Vector3(-0.018f * side, h * 0.42f, 0f),
            new Vector2(0.042f, 0.020f),
            22f * side,
            pale,
            PumpkinOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Leaflet",
            new Vector3(0.016f * side, h * 0.34f, 0f),
            new Vector2(0.038f, 0.018f),
            -16f * side,
            leaf,
            PumpkinOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Tendril",
            new Vector3(-0.062f * side, h * 0.58f, 0f),
            new Vector2(0.028f, 0.008f),
            42f * side,
            tendril,
            PumpkinOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Hook",
            new Vector3(-0.072f * side, h * 0.70f, 0f),
            new Vector2(0.016f, 0.010f),
            -50f * side,
            tendril,
            PumpkinOrder + 1,
            HumanVisualShape.Ellipse);
    }

    private static void AddPumpkinGourd(
        Transform parent,
        float x,
        float h,
        float tilt,
        float side,
        Color rind,
        Color rib,
        Color blush,
        Color stem,
        bool large)
    {
        GameObject go = new GameObject("Gourd");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(x, 0f, 0f);
        if (tilt * tilt > 0.01f)
            go.transform.localRotation = Quaternion.Euler(0f, 0f, tilt);

        float w = large ? 0.108f : 0.072f;
        float bodyH = large ? 0.070f : 0.048f;
        float y = h * 0.52f;

        AddHousePart(
            go.transform,
            "Body",
            new Vector3(0f, y, 0f),
            new Vector2(w, bodyH),
            0f,
            rind,
            PumpkinOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Rib",
            new Vector3(-0.028f * (large ? 1f : 0.72f) * side, y, 0f),
            new Vector2(0.008f, bodyH * 0.82f),
            -8f * side,
            rib,
            PumpkinOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Rib",
            new Vector3(0f, y, 0f),
            new Vector2(0.008f, bodyH * 0.88f),
            0f,
            rib,
            PumpkinOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Rib",
            new Vector3(0.026f * (large ? 1f : 0.72f) * side, y, 0f),
            new Vector2(0.008f, bodyH * 0.80f),
            8f * side,
            rib,
            PumpkinOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Blush",
            new Vector3(0.012f * side, y + bodyH * 0.18f, 0f),
            new Vector2(w * 0.38f, bodyH * 0.32f),
            10f * side,
            blush,
            PumpkinOrder + 2,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Stem",
            new Vector3(0.004f * side, y + bodyH * 0.58f, 0f),
            new Vector2(0.010f, large ? 0.018f : 0.012f),
            14f * side,
            stem,
            PumpkinOrder + 2,
            HumanVisualShape.Box);
    }

    private static void AddCornStalk(
        Transform parent,
        float x,
        float h,
        float tilt,
        float side,
        Color stalk,
        Color leaf,
        Color pale,
        Color husk,
        Color kernel,
        Color silk,
        Color tassel,
        bool large)
    {
        GameObject go = new GameObject("Stalk");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(x, 0f, 0f);
        if (tilt * tilt > 0.01f)
            go.transform.localRotation = Quaternion.Euler(0f, 0f, tilt);

        float scale = large ? 1f : 0.78f;
        AddHousePart(
            go.transform,
            "Heart",
            new Vector3(0f, h * 0.5f, 0f),
            new Vector2(0.016f * scale, h),
            0f,
            stalk,
            CornOrder,
            HumanVisualShape.Box);
        AddHousePart(
            go.transform,
            "Strap",
            new Vector3(-0.034f * side, h * 0.22f, 0f),
            new Vector2(0.078f * scale, 0.018f),
            -38f * side,
            leaf,
            CornOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Strap",
            new Vector3(0.032f * side, h * 0.40f, 0f),
            new Vector2(0.084f * scale, 0.016f),
            32f * side,
            pale,
            CornOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Strap",
            new Vector3(-0.028f * side, h * 0.58f, 0f),
            new Vector2(0.068f * scale, 0.014f),
            -24f * side,
            leaf,
            CornOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Strap",
            new Vector3(0.024f * side, h * 0.74f, 0f),
            new Vector2(0.056f * scale, 0.012f),
            18f * side,
            pale,
            CornOrder,
            HumanVisualShape.Ellipse);

        float earY = h * 0.48f;
        AddHousePart(
            go.transform,
            "Husk",
            new Vector3(0.028f * side, earY, 0f),
            new Vector2(0.036f * scale, 0.054f * scale),
            12f * side,
            husk,
            CornOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Ear",
            new Vector3(0.032f * side, earY, 0f),
            new Vector2(0.020f * scale, 0.038f * scale),
            12f * side,
            kernel,
            CornOrder + 2,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Silk",
            new Vector3(0.034f * side, earY + 0.028f * scale, 0f),
            new Vector2(0.028f * scale, 0.010f),
            28f * side,
            silk,
            CornOrder + 2,
            HumanVisualShape.Ellipse);

        if (!large)
            return;

        AddHousePart(
            go.transform,
            "Tassel",
            new Vector3(0f, h + 0.028f, 0f),
            new Vector2(0.006f, 0.044f),
            0f,
            tassel,
            CornOrder + 1,
            HumanVisualShape.Box);
        AddHousePart(
            go.transform,
            "Tassel",
            new Vector3(-0.018f * side, h + 0.018f, 0f),
            new Vector2(0.006f, 0.034f),
            -28f * side,
            tassel,
            CornOrder + 1,
            HumanVisualShape.Box);
        AddHousePart(
            go.transform,
            "Tassel",
            new Vector3(0.016f * side, h + 0.020f, 0f),
            new Vector2(0.006f, 0.036f),
            22f * side,
            tassel,
            CornOrder + 1,
            HumanVisualShape.Box);
        AddHousePart(
            go.transform,
            "Tassel",
            new Vector3(-0.028f * side, h + 0.008f, 0f),
            new Vector2(0.005f, 0.026f),
            -48f * side,
            tassel,
            CornOrder + 1,
            HumanVisualShape.Box);
        AddHousePart(
            go.transform,
            "Tassel",
            new Vector3(0.026f * side, h + 0.010f, 0f),
            new Vector2(0.005f, 0.028f),
            40f * side,
            tassel,
            CornOrder + 1,
            HumanVisualShape.Box);
    }

    private static void AddCarrotPlant(
        Transform parent,
        float x,
        float h,
        float tilt,
        float side,
        Color root,
        Color blush,
        Color leaf,
        Color pale,
        bool large)
    {
        GameObject go = new GameObject("Plant");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(x, 0f, 0f);
        if (tilt * tilt > 0.01f)
            go.transform.localRotation = Quaternion.Euler(0f, 0f, tilt);

        float scale = large ? 1f : 0.72f;
        float rootH = 0.068f * scale;
        // Shoulder on the slab; the taper hangs into the dirt
        // face so a side-view root crop reads as buried, not a
        // pumpkin sitting on top of the ground.
        AddHousePart(
            go.transform,
            "Root",
            new Vector3(0f, 0.006f - rootH * 0.42f, 0f),
            new Vector2(0.028f * scale, rootH),
            8f * side,
            root,
            CarrotOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Tip",
            new Vector3(0.004f * side, 0.006f - rootH * 0.82f, 0f),
            new Vector2(0.014f * scale, rootH * 0.42f),
            12f * side,
            blush,
            CarrotOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Shoulder",
            new Vector3(0f, 0.010f, 0f),
            new Vector2(0.032f * scale, 0.018f * scale),
            0f,
            root,
            CarrotOrder + 1,
            HumanVisualShape.Ellipse);

        // Fountain of ferny petioles — upright, not fern pinna,
        // not plantain ovals, not corn straps.
        AddHousePart(
            go.transform,
            "Frond",
            new Vector3(0f, h * 0.52f, 0f),
            new Vector2(0.010f, h),
            0f,
            leaf,
            CarrotOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Frond",
            new Vector3(-0.022f * side, h * 0.48f, 0f),
            new Vector2(0.012f, h * 0.78f),
            -22f * side,
            pale,
            CarrotOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Frond",
            new Vector3(0.020f * side, h * 0.46f, 0f),
            new Vector2(0.011f, h * 0.72f),
            18f * side,
            leaf,
            CarrotOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Lobe",
            new Vector3(-0.028f * side, h * 0.72f, 0f),
            new Vector2(0.036f * scale, 0.010f),
            -48f * side,
            pale,
            CarrotOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Lobe",
            new Vector3(0.026f * side, h * 0.68f, 0f),
            new Vector2(0.032f * scale, 0.009f),
            40f * side,
            leaf,
            CarrotOrder + 1,
            HumanVisualShape.Ellipse);
        if (!large)
            return;

        AddHousePart(
            go.transform,
            "Frond",
            new Vector3(-0.012f * side, h * 0.58f, 0f),
            new Vector2(0.009f, h * 0.62f),
            -12f * side,
            pale,
            CarrotOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Lobe",
            new Vector3(0.008f * side, h * 0.88f, 0f),
            new Vector2(0.028f, 0.008f),
            8f * side,
            pale,
            CarrotOrder + 1,
            HumanVisualShape.Ellipse);
    }

    private static void AddLeekPlant(
        Transform parent,
        float x,
        float h,
        float tilt,
        float side,
        Color shaft,
        Color collar,
        Color leaf,
        Color pale,
        bool large)
    {
        GameObject go = new GameObject("Plant");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(x, 0f, 0f);
        if (tilt * tilt > 0.01f)
            go.transform.localRotation = Quaternion.Euler(0f, 0f, tilt);

        float scale = large ? 1f : 0.72f;
        float shaftH = 0.092f * scale;
        // Blanched column on the slab — a SPEAR, not a carrot
        // taper hanging into the dirt face and not a corn stalk
        // with an ear.
        AddHousePart(
            go.transform,
            "Shaft",
            new Vector3(0f, shaftH * 0.5f, 0f),
            new Vector2(0.022f * scale, shaftH),
            0f,
            shaft,
            LeekOrder,
            HumanVisualShape.Box);
        AddHousePart(
            go.transform,
            "Collar",
            new Vector3(0f, shaftH + 0.014f * scale, 0f),
            new Vector2(0.028f * scale, 0.028f * scale),
            0f,
            collar,
            LeekOrder + 1,
            HumanVisualShape.Box);
        AddHousePart(
            go.transform,
            "Green",
            new Vector3(0f, shaftH + 0.036f * scale, 0f),
            new Vector2(0.018f * scale, 0.036f * scale),
            0f,
            pale,
            LeekOrder,
            HumanVisualShape.Box);

        // Tiny root hairs into the dirt face so the spear reads
        // as planted, not a floating column — still not a cone.
        AddHousePart(
            go.transform,
            "Hair",
            new Vector3(-0.006f * side, -0.010f * scale, 0f),
            new Vector2(0.004f, 0.022f * scale),
            -18f * side,
            shaft,
            LeekOrder,
            HumanVisualShape.Box);
        AddHousePart(
            go.transform,
            "Hair",
            new Vector3(0.006f * side, -0.008f * scale, 0f),
            new Vector2(0.004f, 0.018f * scale),
            16f * side,
            shaft,
            LeekOrder,
            HumanVisualShape.Box);

        // Upright leaf fan — a SPEAR, not corn's drooping straps,
        // not carrot fern, not plantain ovals.
        float fanY = shaftH + 0.048f * scale;
        AddHousePart(
            go.transform,
            "Blade",
            new Vector3(0f, fanY + h * 0.22f, 0f),
            new Vector2(0.012f, h * 0.48f),
            0f,
            leaf,
            LeekOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Blade",
            new Vector3(-0.016f * side, fanY + h * 0.18f, 0f),
            new Vector2(0.011f, h * 0.42f),
            -12f * side,
            pale,
            LeekOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Blade",
            new Vector3(0.014f * side, fanY + h * 0.16f, 0f),
            new Vector2(0.010f, h * 0.38f),
            10f * side,
            leaf,
            LeekOrder,
            HumanVisualShape.Ellipse);
        if (!large)
            return;

        AddHousePart(
            go.transform,
            "Blade",
            new Vector3(-0.024f * side, fanY + h * 0.14f, 0f),
            new Vector2(0.010f, h * 0.34f),
            -20f * side,
            pale,
            LeekOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Blade",
            new Vector3(0.022f * side, fanY + h * 0.12f, 0f),
            new Vector2(0.009f, h * 0.32f),
            16f * side,
            leaf,
            LeekOrder + 1,
            HumanVisualShape.Ellipse);
    }

    private static void AddBeanVine(
        Transform parent,
        float x,
        float h,
        float tilt,
        float side,
        Color vine,
        Color leaf,
        Color pale,
        Color pod,
        Color plump,
        Color bloom,
        bool fruit)
    {
        GameObject go = new GameObject("Vine");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(x, 0f, 0f);
        if (tilt * tilt > 0.01f)
            go.transform.localRotation = Quaternion.Euler(0f, 0f, tilt);

        // Twining cane on the pole — not tomato's separate
        // fruiting cane beside the stake.
        AddHousePart(
            go.transform,
            "Cane",
            new Vector3(0.003f * side, h * 0.52f, 0f),
            new Vector2(0.006f, h),
            6f * side,
            vine,
            BeanOrder,
            HumanVisualShape.Box);
        AddHousePart(
            go.transform,
            "Wrap",
            new Vector3(-0.010f * side, h * 0.28f, 0f),
            new Vector2(0.022f, 0.007f),
            38f * side,
            vine,
            BeanOrder,
            HumanVisualShape.Box);
        AddHousePart(
            go.transform,
            "Wrap",
            new Vector3(0.010f * side, h * 0.56f, 0f),
            new Vector2(0.022f, 0.007f),
            -34f * side,
            vine,
            BeanOrder,
            HumanVisualShape.Box);

        // Trifoliate clusters — three leaflets from one node,
        // not tomato's odd-pinnate row.
        AddHousePart(
            go.transform,
            "Leaflet",
            new Vector3(-0.016f * side, h * 0.34f, 0f),
            new Vector2(0.020f, 0.012f),
            -28f * side,
            leaf,
            BeanOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Leaflet",
            new Vector3(0.014f * side, h * 0.36f, 0f),
            new Vector2(0.018f, 0.011f),
            22f * side,
            pale,
            BeanOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Leaflet",
            new Vector3(0.002f * side, h * 0.40f, 0f),
            new Vector2(0.016f, 0.010f),
            4f * side,
            leaf,
            BeanOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Leaflet",
            new Vector3(-0.018f * side, h * 0.62f, 0f),
            new Vector2(0.022f, 0.012f),
            -20f * side,
            pale,
            BeanOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Leaflet",
            new Vector3(0.016f * side, h * 0.64f, 0f),
            new Vector2(0.020f, 0.011f),
            16f * side,
            leaf,
            BeanOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Leaflet",
            new Vector3(0.004f * side, h * 0.68f, 0f),
            new Vector2(0.016f, 0.010f),
            6f * side,
            pale,
            BeanOrder,
            HumanVisualShape.Ellipse);

        if (!fruit)
        {
            AddHousePart(
                go.transform,
                "Bud",
                new Vector3(0.012f * side, h * 0.72f, 0f),
                new Vector2(0.010f, 0.008f),
                14f * side,
                bloom,
                BeanOrder + 1,
                HumanVisualShape.Ellipse);
            AddHousePart(
                go.transform,
                "Green",
                new Vector3(0.014f * side, h * 0.48f - 0.016f, 0f),
                new Vector2(0.006f, 0.022f),
                12f * side,
                pale,
                BeanOrder + 1,
                HumanVisualShape.Ellipse);
            return;
        }

        // Long hanging pods — a POLE crop, not tomato's round
        // red fruit and not a cattail sausage on a reed.
        AddHousePart(
            go.transform,
            "Pod",
            new Vector3(0.016f * side, h * 0.42f - 0.018f, 0f),
            new Vector2(0.007f, 0.040f),
            14f * side,
            pod,
            BeanOrder + 2,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Pod",
            new Vector3(0.008f * side, h * 0.50f - 0.020f, 0f),
            new Vector2(0.006f, 0.036f),
            -10f * side,
            plump,
            BeanOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Pod",
            new Vector3(0.020f * side, h * 0.64f - 0.016f, 0f),
            new Vector2(0.007f, 0.034f),
            18f * side,
            pod,
            BeanOrder + 2,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Bloom",
            new Vector3(0.010f * side, h + 0.006f, 0f),
            new Vector2(0.012f, 0.008f),
            8f * side,
            bloom,
            BeanOrder + 1,
            HumanVisualShape.Ellipse);
    }

    private static void AddOnionPlant(
        Transform parent,
        float x,
        float h,
        float tilt,
        float side,
        Color bulb,
        Color skin,
        Color leaf,
        Color pale,
        bool large)
    {
        GameObject go = new GameObject("Plant");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(x, 0f, 0f);
        if (tilt * tilt > 0.01f)
            go.transform.localRotation = Quaternion.Euler(0f, 0f, tilt);

        float scale = large ? 1f : 0.72f;
        float bulbH = 0.034f * scale;
        float bulbW = 0.040f * scale;
        // Globe sits on the slab with the lower third in the
        // dirt face — a ROUND bulb, not a carrot cone hanging
        // into the face and not a leek shaft standing on it.
        AddHousePart(
            go.transform,
            "Bulb",
            new Vector3(0f, 0.004f - bulbH * 0.18f, 0f),
            new Vector2(bulbW, bulbH),
            0f,
            bulb,
            OnionOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Skin",
            new Vector3(0.005f * side, 0.008f * scale, 0f),
            new Vector2(bulbW * 0.52f, bulbH * 0.42f),
            12f * side,
            skin,
            OnionOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Neck",
            new Vector3(0f, 0.018f * scale, 0f),
            new Vector2(0.012f * scale, 0.014f * scale),
            0f,
            pale,
            OnionOrder,
            HumanVisualShape.Ellipse);

        // Root plate into the dirt face so the globe reads as
        // planted, not a cabbage wrapper sitting on top.
        AddHousePart(
            go.transform,
            "Hair",
            new Vector3(-0.008f * side, -0.012f * scale, 0f),
            new Vector2(0.004f, 0.018f * scale),
            -22f * side,
            skin,
            OnionOrder,
            HumanVisualShape.Box);
        AddHousePart(
            go.transform,
            "Hair",
            new Vector3(0.008f * side, -0.010f * scale, 0f),
            new Vector2(0.004f, 0.016f * scale),
            18f * side,
            skin,
            OnionOrder,
            HumanVisualShape.Box);

        // Hollow tubes — even width, not leek straps, not
        // carrot fern, not bean leaflets.
        float tubeY = 0.022f * scale;
        AddHousePart(
            go.transform,
            "Tube",
            new Vector3(0f, tubeY + h * 0.42f, 0f),
            new Vector2(0.007f, h * 0.72f),
            0f,
            leaf,
            OnionOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Tube",
            new Vector3(-0.012f * side, tubeY + h * 0.36f, 0f),
            new Vector2(0.006f, h * 0.62f),
            -14f * side,
            pale,
            OnionOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Tube",
            new Vector3(0.012f * side, tubeY + h * 0.34f, 0f),
            new Vector2(0.006f, h * 0.58f),
            12f * side,
            leaf,
            OnionOrder,
            HumanVisualShape.Ellipse);
        if (!large)
            return;

        AddHousePart(
            go.transform,
            "Tube",
            new Vector3(-0.020f * side, tubeY + h * 0.28f, 0f),
            new Vector2(0.005f, h * 0.48f),
            -22f * side,
            pale,
            OnionOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Tube",
            new Vector3(0.018f * side, tubeY + h * 0.26f, 0f),
            new Vector2(0.005f, h * 0.44f),
            18f * side,
            leaf,
            OnionOrder + 1,
            HumanVisualShape.Ellipse);
    }

    private static void AddGarlicPlant(
        Transform parent,
        float x,
        float h,
        float tilt,
        float side,
        Color clove,
        Color paper,
        Color blush,
        Color leaf,
        Color pale,
        bool large)
    {
        GameObject go = new GameObject("Plant");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(x, 0f, 0f);
        if (tilt * tilt > 0.01f)
            go.transform.localRotation = Quaternion.Euler(0f, 0f, tilt);

        float scale = large ? 1f : 0.74f;
        // Three overlapping cloves — a BUNCH, not onion's
        // single round globe and not a leek shaft.
        float cloveH = 0.022f * scale;
        float cloveW = 0.016f * scale;
        AddHousePart(
            go.transform,
            "Clove",
            new Vector3(-0.008f * side, 0.002f * scale, 0f),
            new Vector2(cloveW, cloveH),
            -18f * side,
            clove,
            GarlicOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Clove",
            new Vector3(0.008f * side, 0.001f * scale, 0f),
            new Vector2(cloveW * 0.92f, cloveH * 0.90f),
            16f * side,
            clove,
            GarlicOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Clove",
            new Vector3(0f, 0.006f * scale, 0f),
            new Vector2(cloveW * 0.88f, cloveH * 0.86f),
            0f,
            clove,
            GarlicOrder + 1,
            HumanVisualShape.Ellipse);
        // Flattened papery sack around the bunch, plus a
        // pointed crown — garlic, not a round onion bulb.
        AddHousePart(
            go.transform,
            "Paper",
            new Vector3(0.002f * side, 0.008f * scale, 0f),
            new Vector2(0.044f * scale, 0.028f * scale),
            8f * side,
            paper,
            GarlicOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Blush",
            new Vector3(0.008f * side, 0.010f * scale, 0f),
            new Vector2(0.018f * scale, 0.016f * scale),
            14f * side,
            blush,
            GarlicOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Tip",
            new Vector3(0f, 0.022f * scale, 0f),
            new Vector2(0.010f * scale, 0.016f * scale),
            0f,
            paper,
            GarlicOrder,
            HumanVisualShape.Ellipse);

        AddHousePart(
            go.transform,
            "Hair",
            new Vector3(-0.008f * side, -0.010f * scale, 0f),
            new Vector2(0.004f, 0.016f * scale),
            -20f * side,
            paper,
            GarlicOrder,
            HumanVisualShape.Box);
        AddHousePart(
            go.transform,
            "Hair",
            new Vector3(0.007f * side, -0.009f * scale, 0f),
            new Vector2(0.004f, 0.014f * scale),
            16f * side,
            paper,
            GarlicOrder,
            HumanVisualShape.Box);

        // Thin blades — not onion tubes (even width), not
        // leek straps (wide fan), not bean leaflets.
        float bladeY = 0.018f * scale;
        AddHousePart(
            go.transform,
            "Blade",
            new Vector3(0f, bladeY + h * 0.38f, 0f),
            new Vector2(0.005f, h * 0.68f),
            0f,
            leaf,
            GarlicOrder,
            HumanVisualShape.Box);
        AddHousePart(
            go.transform,
            "Blade",
            new Vector3(-0.010f * side, bladeY + h * 0.32f, 0f),
            new Vector2(0.004f, h * 0.56f),
            -12f * side,
            pale,
            GarlicOrder,
            HumanVisualShape.Box);
        if (!large)
            return;

        AddHousePart(
            go.transform,
            "Blade",
            new Vector3(0.010f * side, bladeY + h * 0.30f, 0f),
            new Vector2(0.004f, h * 0.52f),
            10f * side,
            leaf,
            GarlicOrder + 1,
            HumanVisualShape.Box);
    }

    private static void AddRadishPlant(
        Transform parent,
        float x,
        float h,
        float tilt,
        float side,
        Color bulb,
        Color blush,
        Color tip,
        Color leaf,
        Color pale,
        bool large)
    {
        GameObject go = new GameObject("Plant");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(x, 0f, 0f);
        if (tilt * tilt > 0.01f)
            go.transform.localRotation = Quaternion.Euler(0f, 0f, tilt);

        float scale = large ? 1f : 0.72f;
        // Squat red globe — smaller than onion's papery
        // bulb, round not a carrot cone, one body not a
        // garlic clove bunch.
        float bulbH = 0.022f * scale;
        float bulbW = 0.026f * scale;
        AddHousePart(
            go.transform,
            "Bulb",
            new Vector3(0f, 0.002f - bulbH * 0.18f, 0f),
            new Vector2(bulbW, bulbH),
            0f,
            bulb,
            RadishOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Shoulder",
            new Vector3(0.004f * side, 0.006f * scale, 0f),
            new Vector2(bulbW * 0.48f, bulbH * 0.40f),
            14f * side,
            blush,
            RadishOrder + 1,
            HumanVisualShape.Ellipse);
        // White tip into the dirt face — breakfast radish,
        // not onion gold and not carrot orange taper.
        AddHousePart(
            go.transform,
            "Tip",
            new Vector3(0f, -0.010f * scale, 0f),
            new Vector2(0.012f * scale, 0.014f * scale),
            0f,
            tip,
            RadishOrder,
            HumanVisualShape.Ellipse);

        AddHousePart(
            go.transform,
            "Hair",
            new Vector3(-0.006f * side, -0.014f * scale, 0f),
            new Vector2(0.003f, 0.012f * scale),
            -18f * side,
            tip,
            RadishOrder,
            HumanVisualShape.Box);
        AddHousePart(
            go.transform,
            "Hair",
            new Vector3(0.006f * side, -0.012f * scale, 0f),
            new Vector2(0.003f, 0.010f * scale),
            14f * side,
            tip,
            RadishOrder,
            HumanVisualShape.Box);

        // Lobed paddles on thin petioles — not onion tubes
        // (even width), not garlic blades (thin vertical),
        // not carrot ferny fountain.
        float stemY = 0.012f * scale;
        AddHousePart(
            go.transform,
            "Stem",
            new Vector3(0f, stemY + h * 0.28f, 0f),
            new Vector2(0.004f, h * 0.52f),
            0f,
            pale,
            RadishOrder,
            HumanVisualShape.Box);
        AddHousePart(
            go.transform,
            "Lobe",
            new Vector3(-0.008f * side, stemY + h * 0.58f, 0f),
            new Vector2(0.028f * scale, 0.012f * scale),
            -28f * side,
            leaf,
            RadishOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Stem",
            new Vector3(-0.010f * side, stemY + h * 0.22f, 0f),
            new Vector2(0.0035f, h * 0.42f),
            -16f * side,
            pale,
            RadishOrder,
            HumanVisualShape.Box);
        AddHousePart(
            go.transform,
            "Lobe",
            new Vector3(-0.022f * side, stemY + h * 0.48f, 0f),
            new Vector2(0.024f * scale, 0.010f * scale),
            -40f * side,
            leaf,
            RadishOrder,
            HumanVisualShape.Ellipse);
        if (!large)
            return;

        AddHousePart(
            go.transform,
            "Stem",
            new Vector3(0.010f * side, stemY + h * 0.20f, 0f),
            new Vector2(0.0035f, h * 0.38f),
            12f * side,
            pale,
            RadishOrder + 1,
            HumanVisualShape.Box);
        AddHousePart(
            go.transform,
            "Lobe",
            new Vector3(0.018f * side, stemY + h * 0.44f, 0f),
            new Vector2(0.022f * scale, 0.010f * scale),
            32f * side,
            leaf,
            RadishOrder + 1,
            HumanVisualShape.Ellipse);
    }

    private static void AddPotatoPlant(
        Transform parent,
        float x,
        float h,
        float tilt,
        float side,
        Color skin,
        Color lump,
        Color pit,
        Color sprout,
        Color leaf,
        Color pale,
        bool large)
    {
        GameObject go = new GameObject("Plant");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(x, 0f, 0f);
        if (tilt * tilt > 0.01f)
            go.transform.localRotation = Quaternion.Euler(0f, 0f, tilt);

        float scale = large ? 1f : 0.72f;
        // Horizontal kidney in the dirt face — wider than
        // tall, not a radish globe, not an onion round
        // bulb, not a carrot cone hanging into the face.
        float tuberW = 0.048f * scale;
        float tuberH = 0.022f * scale;
        AddHousePart(
            go.transform,
            "Tuber",
            new Vector3(0f, 0.001f - tuberH * 0.22f, 0f),
            new Vector2(tuberW, tuberH),
            8f * side,
            skin,
            PotatoOrder,
            HumanVisualShape.Ellipse);
        // Second lump overlapping — the irregularity; one
        // body is a globe, two offset ellipses is a potato.
        AddHousePart(
            go.transform,
            "Knob",
            new Vector3(0.012f * side, 0.006f * scale, 0f),
            new Vector2(tuberW * 0.52f, tuberH * 0.78f),
            22f * side,
            lump,
            PotatoOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Heel",
            new Vector3(-0.014f * side, -0.006f * scale, 0f),
            new Vector2(tuberW * 0.38f, tuberH * 0.55f),
            -16f * side,
            pit,
            PotatoOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Eye",
            new Vector3(-0.008f * side, 0.004f * scale, 0f),
            new Vector2(0.006f * scale, 0.004f * scale),
            0f,
            pit,
            PotatoOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Eye",
            new Vector3(0.010f * side, -0.002f * scale, 0f),
            new Vector2(0.005f * scale, 0.0035f * scale),
            0f,
            pit,
            PotatoOrder + 1,
            HumanVisualShape.Ellipse);
        // Short pale sprout from an eye — not an onion
        // tube (even width, tall) and not a radish petiole.
        AddHousePart(
            go.transform,
            "Sprout",
            new Vector3(-0.006f * side, 0.014f * scale, 0f),
            new Vector2(0.004f, 0.016f * scale),
            -12f * side,
            sprout,
            PotatoOrder + 1,
            HumanVisualShape.Box);

        // Pinnate leaflets — small ovals along a stem, not
        // radish paddles, not onion tubes, not carrot fern.
        float stemY = 0.016f * scale;
        AddHousePart(
            go.transform,
            "Stem",
            new Vector3(0f, stemY + h * 0.30f, 0f),
            new Vector2(0.004f, h * 0.52f),
            0f,
            pale,
            PotatoOrder,
            HumanVisualShape.Box);
        AddHousePart(
            go.transform,
            "Leaflet",
            new Vector3(-0.010f * side, stemY + h * 0.52f, 0f),
            new Vector2(0.018f * scale, 0.009f * scale),
            -24f * side,
            leaf,
            PotatoOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Leaflet",
            new Vector3(0.010f * side, stemY + h * 0.44f, 0f),
            new Vector2(0.016f * scale, 0.008f * scale),
            20f * side,
            pale,
            PotatoOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Leaflet",
            new Vector3(-0.006f * side, stemY + h * 0.68f, 0f),
            new Vector2(0.014f * scale, 0.008f * scale),
            -8f * side,
            leaf,
            PotatoOrder + 1,
            HumanVisualShape.Ellipse);
        if (!large)
            return;

        AddHousePart(
            go.transform,
            "Stem",
            new Vector3(0.012f * side, stemY + h * 0.22f, 0f),
            new Vector2(0.0035f, h * 0.40f),
            14f * side,
            pale,
            PotatoOrder + 1,
            HumanVisualShape.Box);
        AddHousePart(
            go.transform,
            "Leaflet",
            new Vector3(0.022f * side, stemY + h * 0.42f, 0f),
            new Vector2(0.014f * scale, 0.007f * scale),
            28f * side,
            leaf,
            PotatoOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Leaflet",
            new Vector3(0.016f * side, stemY + h * 0.56f, 0f),
            new Vector2(0.012f * scale, 0.007f * scale),
            12f * side,
            pale,
            PotatoOrder + 1,
            HumanVisualShape.Ellipse);
    }

    private static void AddBeetPlant(
        Transform parent,
        float x,
        float h,
        float tilt,
        float side,
        Color skin,
        Color shoulder,
        Color tap,
        Color vein,
        Color leaf,
        Color pale,
        bool large)
    {
        GameObject go = new GameObject("Plant");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(x, 0f, 0f);
        if (tilt * tilt > 0.01f)
            go.transform.localRotation = Quaternion.Euler(0f, 0f, tilt);

        float scale = large ? 1f : 0.72f;
        // Round crown in the dirt face — larger than a
        // radish globule, round not a potato kidney, not
        // an onion papery globe hanging into the face.
        float bulbW = 0.034f * scale;
        float bulbH = 0.030f * scale;
        AddHousePart(
            go.transform,
            "Crown",
            new Vector3(0f, 0.002f - bulbH * 0.12f, 0f),
            new Vector2(bulbW, bulbH),
            0f,
            skin,
            BeetOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Shoulder",
            new Vector3(0.004f * side, 0.008f * scale, 0f),
            new Vector2(bulbW * 0.52f, bulbH * 0.38f),
            12f * side,
            shoulder,
            BeetOrder + 1,
            HumanVisualShape.Ellipse);
        // Thin tap into the dirt — not a radish white
        // tip (fat cone) and not a carrot orange taper.
        AddHousePart(
            go.transform,
            "Tap",
            new Vector3(0f, -0.016f * scale, 0f),
            new Vector2(0.007f * scale, 0.016f * scale),
            0f,
            tap,
            BeetOrder,
            HumanVisualShape.Ellipse);

        // Red petiole + oval leaf with a red midrib — beet
        // greens, not radish paddles, not onion tubes, not
        // potato pinnate leaflets.
        float stemY = 0.018f * scale;
        AddHousePart(
            go.transform,
            "Petiole",
            new Vector3(-0.006f * side, stemY + h * 0.28f, 0f),
            new Vector2(0.005f, h * 0.52f),
            -14f * side,
            vein,
            BeetOrder,
            HumanVisualShape.Box);
        AddHousePart(
            go.transform,
            "Leaf",
            new Vector3(-0.016f * side, stemY + h * 0.62f, 0f),
            new Vector2(0.028f * scale, 0.016f * scale),
            -22f * side,
            leaf,
            BeetOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Vein",
            new Vector3(-0.016f * side, stemY + h * 0.62f, 0f),
            new Vector2(0.018f * scale, 0.004f * scale),
            -22f * side,
            vein,
            BeetOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Petiole",
            new Vector3(0.008f * side, stemY + h * 0.24f, 0f),
            new Vector2(0.005f, h * 0.46f),
            12f * side,
            vein,
            BeetOrder,
            HumanVisualShape.Box);
        AddHousePart(
            go.transform,
            "Leaf",
            new Vector3(0.018f * side, stemY + h * 0.52f, 0f),
            new Vector2(0.024f * scale, 0.014f * scale),
            18f * side,
            pale,
            BeetOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Vein",
            new Vector3(0.018f * side, stemY + h * 0.52f, 0f),
            new Vector2(0.016f * scale, 0.0035f * scale),
            18f * side,
            vein,
            BeetOrder + 1,
            HumanVisualShape.Ellipse);
        if (!large)
            return;

        AddHousePart(
            go.transform,
            "Petiole",
            new Vector3(0.002f * side, stemY + h * 0.20f, 0f),
            new Vector2(0.0045f, h * 0.40f),
            4f * side,
            vein,
            BeetOrder + 1,
            HumanVisualShape.Box);
        AddHousePart(
            go.transform,
            "Leaf",
            new Vector3(0.008f * side, stemY + h * 0.72f, 0f),
            new Vector2(0.022f * scale, 0.013f * scale),
            6f * side,
            leaf,
            BeetOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Vein",
            new Vector3(0.008f * side, stemY + h * 0.72f, 0f),
            new Vector2(0.014f * scale, 0.0035f * scale),
            6f * side,
            vein,
            BeetOrder + 1,
            HumanVisualShape.Ellipse);
    }

    private static void AddParsnipPlant(
        Transform parent,
        float x,
        float h,
        float tilt,
        float side,
        Color root,
        Color shoulder,
        Color leaf,
        Color pale,
        bool large)
    {
        GameObject go = new GameObject("Plant");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(x, 0f, 0f);
        if (tilt * tilt > 0.01f)
            go.transform.localRotation = Quaternion.Euler(0f, 0f, tilt);

        float scale = large ? 1f : 0.72f;
        float rootH = 0.082f * scale;
        // Broader creamy cone than a carrot: fat shoulder
        // on the slab, ivory taper into the dirt face. Not
        // orange, not a round beet crown, not a potato
        // kidney.
        AddHousePart(
            go.transform,
            "Root",
            new Vector3(0f, 0.006f - rootH * 0.38f, 0f),
            new Vector2(0.040f * scale, rootH),
            6f * side,
            root,
            ParsnipOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Tip",
            new Vector3(0.003f * side, 0.006f - rootH * 0.86f, 0f),
            new Vector2(0.016f * scale, rootH * 0.38f),
            10f * side,
            root,
            ParsnipOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Shoulder",
            new Vector3(0f, 0.012f, 0f),
            new Vector2(0.044f * scale, 0.022f * scale),
            0f,
            shoulder,
            ParsnipOrder + 1,
            HumanVisualShape.Ellipse);

        // Celery-like pinnate: paired ovals on petioles,
        // not carrot ferny fountain, not beet red-veined
        // ovals, not potato leaflets.
        float stemY = 0.016f * scale;
        AddHousePart(
            go.transform,
            "Petiole",
            new Vector3(-0.004f * side, stemY + h * 0.38f, 0f),
            new Vector2(0.006f, h * 0.68f),
            -10f * side,
            leaf,
            ParsnipOrder,
            HumanVisualShape.Box);
        AddHousePart(
            go.transform,
            "Leaflet",
            new Vector3(-0.018f * side, stemY + h * 0.42f, 0f),
            new Vector2(0.020f * scale, 0.011f * scale),
            -28f * side,
            pale,
            ParsnipOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Leaflet",
            new Vector3(-0.014f * side, stemY + h * 0.62f, 0f),
            new Vector2(0.018f * scale, 0.010f * scale),
            -18f * side,
            leaf,
            ParsnipOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Petiole",
            new Vector3(0.008f * side, stemY + h * 0.32f, 0f),
            new Vector2(0.006f, h * 0.58f),
            12f * side,
            pale,
            ParsnipOrder,
            HumanVisualShape.Box);
        AddHousePart(
            go.transform,
            "Leaflet",
            new Vector3(0.020f * side, stemY + h * 0.36f, 0f),
            new Vector2(0.018f * scale, 0.010f * scale),
            24f * side,
            leaf,
            ParsnipOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Leaflet",
            new Vector3(0.016f * side, stemY + h * 0.54f, 0f),
            new Vector2(0.016f * scale, 0.009f * scale),
            16f * side,
            pale,
            ParsnipOrder,
            HumanVisualShape.Ellipse);
        if (!large)
            return;

        AddHousePart(
            go.transform,
            "Petiole",
            new Vector3(0.002f * side, stemY + h * 0.28f, 0f),
            new Vector2(0.005f, h * 0.50f),
            4f * side,
            leaf,
            ParsnipOrder + 1,
            HumanVisualShape.Box);
        AddHousePart(
            go.transform,
            "Leaflet",
            new Vector3(0.006f * side, stemY + h * 0.78f, 0f),
            new Vector2(0.016f * scale, 0.009f * scale),
            6f * side,
            pale,
            ParsnipOrder + 1,
            HumanVisualShape.Ellipse);
    }

    private static void AddTurnipPlant(
        Transform parent,
        float x,
        float h,
        float tilt,
        float side,
        Color globe,
        Color shoulder,
        Color tap,
        Color leaf,
        Color pale,
        bool large)
    {
        GameObject go = new GameObject("Plant");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(x, 0f, 0f);
        if (tilt * tilt > 0.01f)
            go.transform.localRotation = Quaternion.Euler(0f, 0f, tilt);

        float scale = large ? 1f : 0.72f;
        // Round WHITE globe in the dirt face — larger than
        // a radish, not a beet's all-purple crown, not a
        // parsnip's creamy cone hanging into the face.
        float bulbW = 0.038f * scale;
        float bulbH = 0.034f * scale;
        AddHousePart(
            go.transform,
            "Globe",
            new Vector3(0f, 0.002f - bulbH * 0.10f, 0f),
            new Vector2(bulbW, bulbH),
            0f,
            globe,
            TurnipOrder,
            HumanVisualShape.Ellipse);
        // Purple-top cap — beet is purple all over; this
        // stain sits only on the shoulder.
        AddHousePart(
            go.transform,
            "Shoulder",
            new Vector3(0f, 0.012f * scale, 0f),
            new Vector2(bulbW * 0.82f, bulbH * 0.42f),
            0f,
            shoulder,
            TurnipOrder + 1,
            HumanVisualShape.Ellipse);
        // Short cream tap — not a parsnip long cone and
        // not a beet burgundy thread.
        AddHousePart(
            go.transform,
            "Tap",
            new Vector3(0.002f * side, -0.018f * scale, 0f),
            new Vector2(0.008f * scale, 0.014f * scale),
            8f * side,
            tap,
            TurnipOrder,
            HumanVisualShape.Ellipse);

        // Lyrate greens: green petiole + a basal lobe and
        // a paddle — not beet red veins, not parsnip
        // celery pinnate, not radish small paddles.
        float stemY = 0.016f * scale;
        AddHousePart(
            go.transform,
            "Petiole",
            new Vector3(-0.006f * side, stemY + h * 0.30f, 0f),
            new Vector2(0.005f, h * 0.54f),
            -12f * side,
            pale,
            TurnipOrder,
            HumanVisualShape.Box);
        AddHousePart(
            go.transform,
            "Lobe",
            new Vector3(-0.014f * side, stemY + h * 0.38f, 0f),
            new Vector2(0.016f * scale, 0.010f * scale),
            -32f * side,
            leaf,
            TurnipOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Blade",
            new Vector3(-0.018f * side, stemY + h * 0.66f, 0f),
            new Vector2(0.026f * scale, 0.015f * scale),
            -18f * side,
            leaf,
            TurnipOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Petiole",
            new Vector3(0.008f * side, stemY + h * 0.26f, 0f),
            new Vector2(0.005f, h * 0.48f),
            14f * side,
            leaf,
            TurnipOrder,
            HumanVisualShape.Box);
        AddHousePart(
            go.transform,
            "Lobe",
            new Vector3(0.016f * side, stemY + h * 0.34f, 0f),
            new Vector2(0.014f * scale, 0.009f * scale),
            28f * side,
            pale,
            TurnipOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Blade",
            new Vector3(0.020f * side, stemY + h * 0.56f, 0f),
            new Vector2(0.022f * scale, 0.013f * scale),
            16f * side,
            pale,
            TurnipOrder,
            HumanVisualShape.Ellipse);
        if (!large)
            return;

        AddHousePart(
            go.transform,
            "Petiole",
            new Vector3(0.002f * side, stemY + h * 0.22f, 0f),
            new Vector2(0.0045f, h * 0.42f),
            4f * side,
            pale,
            TurnipOrder + 1,
            HumanVisualShape.Box);
        AddHousePart(
            go.transform,
            "Blade",
            new Vector3(0.006f * side, stemY + h * 0.78f, 0f),
            new Vector2(0.020f * scale, 0.012f * scale),
            6f * side,
            leaf,
            TurnipOrder + 1,
            HumanVisualShape.Ellipse);
    }

    private static void AddRutabagaPlant(
        Transform parent,
        float x,
        float h,
        float tilt,
        float side,
        Color globe,
        Color shoulder,
        Color tap,
        Color leaf,
        Color pale,
        bool large)
    {
        GameObject go = new GameObject("Plant");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(x, 0f, 0f);
        if (tilt * tilt > 0.01f)
            go.transform.localRotation = Quaternion.Euler(0f, 0f, tilt);

        float scale = large ? 1f : 0.72f;
        // Larger YELLOW globe in the dirt face — bigger
        // than a turnip, not a beet's all-purple crown,
        // not a parsnip's creamy cone hanging into the
        // face. Oblate, not a white ivory ball.
        float bulbW = 0.046f * scale;
        float bulbH = 0.038f * scale;
        AddHousePart(
            go.transform,
            "Globe",
            new Vector3(0f, 0.002f - bulbH * 0.10f, 0f),
            new Vector2(bulbW, bulbH),
            0f,
            globe,
            RutabagaOrder,
            HumanVisualShape.Ellipse);
        // Purple-bronze cap — beet is purple all over;
        // turnip's stain is a small shoulder; this sits
        // wider on the top of a yellow body.
        AddHousePart(
            go.transform,
            "Shoulder",
            new Vector3(0f, 0.014f * scale, 0f),
            new Vector2(bulbW * 0.90f, bulbH * 0.52f),
            0f,
            shoulder,
            RutabagaOrder + 1,
            HumanVisualShape.Ellipse);
        // Stubby neck where collards leave — turnip has
        // none; beet has none.
        AddHousePart(
            go.transform,
            "Neck",
            new Vector3(0f, 0.022f * scale, 0f),
            new Vector2(0.012f * scale, 0.010f * scale),
            0f,
            globe,
            RutabagaOrder,
            HumanVisualShape.Ellipse);
        // Short gold tap — not a parsnip long cone and
        // not a beet burgundy thread.
        AddHousePart(
            go.transform,
            "Tap",
            new Vector3(0.002f * side, -0.020f * scale, 0f),
            new Vector2(0.010f * scale, 0.016f * scale),
            6f * side,
            tap,
            RutabagaOrder,
            HumanVisualShape.Ellipse);

        // Glaucous collard paddles: thick petiole + a
        // broad blade — not turnip lyrate lobes, not
        // beet red veins, not parsnip celery pinnate.
        float stemY = 0.020f * scale;
        AddHousePart(
            go.transform,
            "Petiole",
            new Vector3(-0.008f * side, stemY + h * 0.32f, 0f),
            new Vector2(0.006f, h * 0.58f),
            -10f * side,
            pale,
            RutabagaOrder,
            HumanVisualShape.Box);
        AddHousePart(
            go.transform,
            "Blade",
            new Vector3(-0.020f * side, stemY + h * 0.62f, 0f),
            new Vector2(0.032f * scale, 0.018f * scale),
            -16f * side,
            leaf,
            RutabagaOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Petiole",
            new Vector3(0.010f * side, stemY + h * 0.28f, 0f),
            new Vector2(0.006f, h * 0.50f),
            12f * side,
            leaf,
            RutabagaOrder,
            HumanVisualShape.Box);
        AddHousePart(
            go.transform,
            "Blade",
            new Vector3(0.022f * side, stemY + h * 0.54f, 0f),
            new Vector2(0.028f * scale, 0.016f * scale),
            14f * side,
            pale,
            RutabagaOrder + 1,
            HumanVisualShape.Ellipse);
        if (!large)
            return;

        AddHousePart(
            go.transform,
            "Petiole",
            new Vector3(0.002f * side, stemY + h * 0.24f, 0f),
            new Vector2(0.005f, h * 0.46f),
            3f * side,
            pale,
            RutabagaOrder + 1,
            HumanVisualShape.Box);
        AddHousePart(
            go.transform,
            "Blade",
            new Vector3(0.006f * side, stemY + h * 0.80f, 0f),
            new Vector2(0.026f * scale, 0.015f * scale),
            5f * side,
            leaf,
            RutabagaOrder + 1,
            HumanVisualShape.Ellipse);
    }

    private static void AddKohlrabiPlant(
        Transform parent,
        float x,
        float h,
        float tilt,
        float side,
        Color stem,
        Color blush,
        Color collar,
        Color leaf,
        Color pale,
        bool large)
    {
        GameObject go = new GameObject("Plant");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(x, 0f, 0f);
        if (tilt * tilt > 0.01f)
            go.transform.localRotation = Quaternion.Euler(0f, 0f, tilt);

        float scale = large ? 1f : 0.72f;
        // Swollen STEM on the slab — not a buried globe.
        // Rounder than a rutabaga, smaller than that yellow
        // body, not a cabbage's layered wraps.
        float bulbW = 0.040f * scale;
        float bulbH = 0.038f * scale;
        float bulbY = bulbH * 0.52f;
        // Tiny collar that only kisses the dirt — turnip /
        // rutabaga / beet bury the body; this sits up.
        AddHousePart(
            go.transform,
            "Collar",
            new Vector3(0f, 0.003f, 0f),
            new Vector2(0.016f * scale, 0.010f * scale),
            0f,
            collar,
            KohlrabiOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Globe",
            new Vector3(0f, bulbY, 0f),
            new Vector2(bulbW, bulbH),
            0f,
            stem,
            KohlrabiOrder,
            HumanVisualShape.Ellipse);
        // Cool highlight on the upper face — cabbage is
        // wraps; this is one smooth stem.
        AddHousePart(
            go.transform,
            "Blush",
            new Vector3(0.005f * side, bulbY + bulbH * 0.12f, 0f),
            new Vector2(bulbW * 0.42f, bulbH * 0.36f),
            0f,
            blush,
            KohlrabiOrder + 1,
            HumanVisualShape.Ellipse);
        // Crown nubs where petioles leave — root greens
        // come from soil, not from the top of a stem.
        AddHousePart(
            go.transform,
            "Crown",
            new Vector3(0f, bulbY + bulbH * 0.44f, 0f),
            new Vector2(0.014f * scale, 0.010f * scale),
            0f,
            pale,
            KohlrabiOrder,
            HumanVisualShape.Ellipse);

        // Long petioles from the CROWN, cabbage-like
        // blades at the far end — not turnip lyrate
        // lobes from dirt, not rutabaga collard paddles
        // on a short neck, not beet red veins.
        float stemY = bulbY + bulbH * 0.38f;
        AddHousePart(
            go.transform,
            "Petiole",
            new Vector3(-0.006f * side, stemY + h * 0.38f, 0f),
            new Vector2(0.005f, h * 0.72f),
            -12f * side,
            pale,
            KohlrabiOrder,
            HumanVisualShape.Box);
        AddHousePart(
            go.transform,
            "Blade",
            new Vector3(-0.024f * side, stemY + h * 0.78f, 0f),
            new Vector2(0.028f * scale, 0.016f * scale),
            -18f * side,
            leaf,
            KohlrabiOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Petiole",
            new Vector3(0.010f * side, stemY + h * 0.32f, 0f),
            new Vector2(0.005f, h * 0.62f),
            14f * side,
            leaf,
            KohlrabiOrder,
            HumanVisualShape.Box);
        AddHousePart(
            go.transform,
            "Blade",
            new Vector3(0.026f * side, stemY + h * 0.68f, 0f),
            new Vector2(0.024f * scale, 0.014f * scale),
            16f * side,
            pale,
            KohlrabiOrder + 1,
            HumanVisualShape.Ellipse);
        if (!large)
            return;

        AddHousePart(
            go.transform,
            "Petiole",
            new Vector3(0.002f * side, stemY + h * 0.28f, 0f),
            new Vector2(0.004f, h * 0.56f),
            4f * side,
            pale,
            KohlrabiOrder + 1,
            HumanVisualShape.Box);
        AddHousePart(
            go.transform,
            "Blade",
            new Vector3(0.008f * side, stemY + h * 0.92f, 0f),
            new Vector2(0.022f * scale, 0.013f * scale),
            6f * side,
            leaf,
            KohlrabiOrder + 1,
            HumanVisualShape.Ellipse);
    }

    private static void AddCeleryPlant(
        Transform parent,
        float x,
        float h,
        float tilt,
        float side,
        Color stalk,
        Color rib,
        Color heart,
        Color leaf,
        Color pale,
        bool large)
    {
        GameObject go = new GameObject("Plant");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(x, 0f, 0f);
        if (tilt * tilt > 0.01f)
            go.transform.localRotation = Quaternion.Euler(0f, 0f, tilt);

        float scale = large ? 1f : 0.72f;
        // Heart sitting ON the slab — not a kohlrabi
        // globe, not a buried root, not a cabbage wrap.
        float heartW = 0.022f * scale;
        float heartH = 0.016f * scale;
        float heartY = heartH * 0.52f;
        AddHousePart(
            go.transform,
            "Heart",
            new Vector3(0f, heartY, 0f),
            new Vector2(heartW, heartH),
            0f,
            heart,
            CeleryOrder,
            HumanVisualShape.Ellipse);

        // Ribbed petioles — several thin boxes, not a
        // leek's one blanched shaft and not a kohlrabi
        // round stem. Ribs are a darker strip on the face.
        float stalkH = h * 0.82f;
        float stalkY = heartY + stalkH * 0.48f;
        AddHousePart(
            go.transform,
            "Stalk",
            new Vector3(-0.008f * side, stalkY, 0f),
            new Vector2(0.010f * scale, stalkH),
            -6f * side,
            stalk,
            CeleryOrder,
            HumanVisualShape.Box);
        AddHousePart(
            go.transform,
            "Rib",
            new Vector3(-0.008f * side, stalkY + stalkH * 0.04f, 0f),
            new Vector2(0.003f, stalkH * 0.78f),
            -6f * side,
            rib,
            CeleryOrder + 1,
            HumanVisualShape.Box);
        AddHousePart(
            go.transform,
            "Stalk",
            new Vector3(0.002f * side, stalkY + 0.006f, 0f),
            new Vector2(0.011f * scale, stalkH * 1.06f),
            3f * side,
            stalk,
            CeleryOrder,
            HumanVisualShape.Box);
        AddHousePart(
            go.transform,
            "Rib",
            new Vector3(0.002f * side, stalkY + stalkH * 0.08f, 0f),
            new Vector2(0.003f, stalkH * 0.82f),
            3f * side,
            rib,
            CeleryOrder + 1,
            HumanVisualShape.Box);
        AddHousePart(
            go.transform,
            "Stalk",
            new Vector3(0.012f * side, stalkY - 0.004f, 0f),
            new Vector2(0.009f * scale, stalkH * 0.88f),
            10f * side,
            pale,
            CeleryOrder,
            HumanVisualShape.Box);
        AddHousePart(
            go.transform,
            "Rib",
            new Vector3(0.012f * side, stalkY + stalkH * 0.02f, 0f),
            new Vector2(0.003f, stalkH * 0.70f),
            10f * side,
            rib,
            CeleryOrder + 1,
            HumanVisualShape.Box);

        // Compound leaflets at the CROWN of the stalks —
        // not a leek spear fan, not kohlrabi long petioles
        // from a globe, not cabbage wraps.
        float leafY = heartY + stalkH * 0.92f;
        AddHousePart(
            go.transform,
            "Leaflet",
            new Vector3(-0.018f * side, leafY + 0.010f, 0f),
            new Vector2(0.018f * scale, 0.010f * scale),
            -22f * side,
            leaf,
            CeleryOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Leaflet",
            new Vector3(0.016f * side, leafY + 0.016f, 0f),
            new Vector2(0.016f * scale, 0.009f * scale),
            18f * side,
            pale,
            CeleryOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Leaflet",
            new Vector3(0.002f * side, leafY + 0.022f, 0f),
            new Vector2(0.014f * scale, 0.009f * scale),
            4f * side,
            leaf,
            CeleryOrder + 1,
            HumanVisualShape.Ellipse);
        if (!large)
            return;

        AddHousePart(
            go.transform,
            "Stalk",
            new Vector3(-0.016f * side, stalkY - 0.008f, 0f),
            new Vector2(0.008f * scale, stalkH * 0.72f),
            -14f * side,
            pale,
            CeleryOrder,
            HumanVisualShape.Box);
        AddHousePart(
            go.transform,
            "Rib",
            new Vector3(-0.016f * side, stalkY, 0f),
            new Vector2(0.003f, stalkH * 0.56f),
            -14f * side,
            rib,
            CeleryOrder + 1,
            HumanVisualShape.Box);
        AddHousePart(
            go.transform,
            "Leaflet",
            new Vector3(-0.028f * side, leafY - 0.004f, 0f),
            new Vector2(0.014f * scale, 0.008f * scale),
            -28f * side,
            pale,
            CeleryOrder + 1,
            HumanVisualShape.Ellipse);
    }

    private static void AddFennelPlant(
        Transform parent,
        float x,
        float h,
        float tilt,
        float side,
        Color bulb,
        Color layer,
        Color blush,
        Color stem,
        Color frond,
        Color pale,
        bool large)
    {
        GameObject go = new GameObject("Plant");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(x, 0f, 0f);
        if (tilt * tilt > 0.01f)
            go.transform.localRotation = Quaternion.Euler(0f, 0f, tilt);

        float scale = large ? 1f : 0.72f;
        // Layered pale bulb ON the slab — packed petiole
        // bases, not a kohlrabi round apple-green stem,
        // not celery's small heart, not an onion buried
        // globe, not a leek blanched column.
        float bulbW = 0.038f * scale;
        float bulbH = 0.026f * scale;
        float bulbY = bulbH * 0.52f;
        AddHousePart(
            go.transform,
            "Bulb",
            new Vector3(0f, bulbY, 0f),
            new Vector2(bulbW, bulbH),
            0f,
            bulb,
            FennelOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Cheek",
            new Vector3(-0.010f * side, bulbY + 0.002f, 0f),
            new Vector2(0.018f * scale, 0.022f * scale),
            -16f * side,
            layer,
            FennelOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Cheek",
            new Vector3(0.010f * side, bulbY + 0.002f, 0f),
            new Vector2(0.018f * scale, 0.022f * scale),
            14f * side,
            blush,
            FennelOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Cap",
            new Vector3(0f, bulbY + bulbH * 0.42f, 0f),
            new Vector2(0.022f * scale, 0.010f * scale),
            0f,
            blush,
            FennelOrder + 1,
            HumanVisualShape.Ellipse);

        // Hollow-looking thin stems — not celery's ribbed
        // 1 cm petioles and not a leek's one thick shaft.
        float stemH = h * 0.78f;
        float stemY = bulbY + bulbH * 0.36f + stemH * 0.42f;
        AddHousePart(
            go.transform,
            "Stem",
            new Vector3(-0.006f * side, stemY, 0f),
            new Vector2(0.004f, stemH),
            -8f * side,
            stem,
            FennelOrder,
            HumanVisualShape.Box);
        AddHousePart(
            go.transform,
            "Stem",
            new Vector3(0.004f * side, stemY + 0.004f, 0f),
            new Vector2(0.004f, stemH * 1.08f),
            4f * side,
            pale,
            FennelOrder,
            HumanVisualShape.Box);
        AddHousePart(
            go.transform,
            "Stem",
            new Vector3(0.012f * side, stemY - 0.002f, 0f),
            new Vector2(0.004f, stemH * 0.88f),
            12f * side,
            stem,
            FennelOrder,
            HumanVisualShape.Box);

        // Dill-like threads at the CROWN — not celery
        // compound ovals, not a leek spear fan, not
        // kohlrabi paddles on long petioles.
        float crownY = stemY + stemH * 0.48f;
        AddHousePart(
            go.transform,
            "Frond",
            new Vector3(-0.016f * side, crownY + 0.012f, 0f),
            new Vector2(0.005f, 0.016f * scale),
            -28f * side,
            frond,
            FennelOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Frond",
            new Vector3(-0.008f * side, crownY + 0.018f, 0f),
            new Vector2(0.004f, 0.018f * scale),
            -12f * side,
            pale,
            FennelOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Frond",
            new Vector3(0.004f * side, crownY + 0.022f, 0f),
            new Vector2(0.004f, 0.016f * scale),
            6f * side,
            frond,
            FennelOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Frond",
            new Vector3(0.014f * side, crownY + 0.014f, 0f),
            new Vector2(0.005f, 0.014f * scale),
            22f * side,
            pale,
            FennelOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Frond",
            new Vector3(0.022f * side, crownY + 0.008f, 0f),
            new Vector2(0.004f, 0.012f * scale),
            34f * side,
            frond,
            FennelOrder + 1,
            HumanVisualShape.Ellipse);
        if (!large)
            return;

        AddHousePart(
            go.transform,
            "Stem",
            new Vector3(-0.014f * side, stemY - 0.006f, 0f),
            new Vector2(0.004f, stemH * 0.72f),
            -16f * side,
            pale,
            FennelOrder,
            HumanVisualShape.Box);
        AddHousePart(
            go.transform,
            "Frond",
            new Vector3(-0.024f * side, crownY + 0.004f, 0f),
            new Vector2(0.004f, 0.014f * scale),
            -36f * side,
            pale,
            FennelOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Frond",
            new Vector3(-0.004f * side, crownY + 0.026f, 0f),
            new Vector2(0.004f, 0.012f * scale),
            -4f * side,
            frond,
            FennelOrder + 1,
            HumanVisualShape.Ellipse);
    }

    private static void AddAsparagusPlant(
        Transform parent,
        float x,
        float h,
        float tilt,
        float side,
        Color crown,
        Color scale,
        Color spear,
        Color pale,
        Color tip,
        bool large)
    {
        GameObject go = new GameObject("Plant");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(x, 0f, 0f);
        if (tilt * tilt > 0.01f)
            go.transform.localRotation = Quaternion.Euler(0f, 0f, tilt);

        float s = large ? 1f : 0.72f;
        // Scaly woody crown ON the slab — not a fennel
        // layered pale bulb, not celery's pale heart, not
        // a leek blanched column sitting as one shaft.
        float crownW = 0.028f * s;
        float crownH = 0.012f * s;
        float crownY = crownH * 0.52f;
        AddHousePart(
            go.transform,
            "Crown",
            new Vector3(0f, crownY, 0f),
            new Vector2(crownW, crownH),
            0f,
            crown,
            AsparagusOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Scale",
            new Vector3(-0.006f * side, crownY + 0.002f, 0f),
            new Vector2(0.010f * s, 0.008f * s),
            -18f * side,
            scale,
            AsparagusOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Scale",
            new Vector3(0.006f * side, crownY + 0.002f, 0f),
            new Vector2(0.010f * s, 0.008f * s),
            16f * side,
            scale,
            AsparagusOrder + 1,
            HumanVisualShape.Ellipse);

        // Thin green spears — not a leek's 2 cm blanched
        // shaft, not celery's ribbed 1 cm petioles, not
        // fennel hollow stems with a dill crown.
        float spearH = h * 0.92f;
        float spearY = crownY + crownH * 0.28f + spearH * 0.46f;
        AddHousePart(
            go.transform,
            "Spear",
            new Vector3(-0.006f * side, spearY, 0f),
            new Vector2(0.006f, spearH),
            -8f * side,
            spear,
            AsparagusOrder,
            HumanVisualShape.Box);
        AddHousePart(
            go.transform,
            "Spear",
            new Vector3(0.002f * side, spearY + 0.006f, 0f),
            new Vector2(0.006f, spearH * 1.08f),
            3f * side,
            pale,
            AsparagusOrder,
            HumanVisualShape.Box);
        AddHousePart(
            go.transform,
            "Spear",
            new Vector3(0.010f * side, spearY - 0.004f, 0f),
            new Vector2(0.006f, spearH * 0.82f),
            12f * side,
            spear,
            AsparagusOrder,
            HumanVisualShape.Box);

        // Overlapping tip scales — a SPEARHEAD, not a
        // leek leaf fan, not fennel dill threads, not
        // celery compound ovals.
        float tipY = spearY + spearH * 0.52f;
        AddHousePart(
            go.transform,
            "Tip",
            new Vector3(-0.006f * side, tipY + 0.004f, 0f),
            new Vector2(0.007f * s, 0.010f * s),
            -8f * side,
            tip,
            AsparagusOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Tip",
            new Vector3(0.002f * side, tipY + 0.012f, 0f),
            new Vector2(0.007f * s, 0.012f * s),
            3f * side,
            tip,
            AsparagusOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Tip",
            new Vector3(0.010f * side, tipY - 0.002f, 0f),
            new Vector2(0.006f * s, 0.009f * s),
            12f * side,
            tip,
            AsparagusOrder + 1,
            HumanVisualShape.Ellipse);
        if (!large)
            return;

        AddHousePart(
            go.transform,
            "Spear",
            new Vector3(-0.014f * side, spearY - 0.008f, 0f),
            new Vector2(0.006f, spearH * 0.68f),
            -16f * side,
            pale,
            AsparagusOrder,
            HumanVisualShape.Box);
        AddHousePart(
            go.transform,
            "Tip",
            new Vector3(-0.014f * side, tipY - 0.010f, 0f),
            new Vector2(0.006f * s, 0.008f * s),
            -16f * side,
            tip,
            AsparagusOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Scale",
            new Vector3(0f, crownY + 0.004f, 0f),
            new Vector2(0.012f * s, 0.006f * s),
            0f,
            scale,
            AsparagusOrder + 1,
            HumanVisualShape.Ellipse);
    }

    private static void AddRhubarbPlant(
        Transform parent,
        float x,
        float h,
        float tilt,
        float side,
        Color crown,
        Color stalk,
        Color groove,
        Color blush,
        Color leaf,
        Color pale,
        bool large)
    {
        GameObject go = new GameObject("Plant");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(x, 0f, 0f);
        if (tilt * tilt > 0.01f)
            go.transform.localRotation = Quaternion.Euler(0f, 0f, tilt);

        float s = large ? 1f : 0.74f;
        // Woody crown ON the slab — not a fennel layered
        // pale bulb, not asparagus scaly spears, not a
        // leek blanched column sitting as one shaft.
        float crownW = 0.034f * s;
        float crownH = 0.014f * s;
        float crownY = crownH * 0.52f;
        AddHousePart(
            go.transform,
            "Crown",
            new Vector3(0f, crownY, 0f),
            new Vector2(crownW, crownH),
            0f,
            crown,
            RhubarbOrder,
            HumanVisualShape.Ellipse);

        // Thick red petioles — wider than celery's 1 cm
        // ribs and asparagus's 6 mm spears, not a leek's
        // one blanched shaft.
        float stalkH = h * 0.78f;
        float stalkY = crownY + crownH * 0.22f + stalkH * 0.46f;
        AddHousePart(
            go.transform,
            "Stalk",
            new Vector3(-0.008f * side, stalkY, 0f),
            new Vector2(0.016f * s, stalkH),
            -8f * side,
            stalk,
            RhubarbOrder,
            HumanVisualShape.Box);
        AddHousePart(
            go.transform,
            "Groove",
            new Vector3(-0.008f * side, stalkY + stalkH * 0.04f, 0f),
            new Vector2(0.004f, stalkH * 0.72f),
            -8f * side,
            groove,
            RhubarbOrder + 1,
            HumanVisualShape.Box);
        AddHousePart(
            go.transform,
            "Stalk",
            new Vector3(0.010f * side, stalkY + 0.004f, 0f),
            new Vector2(0.018f * s, stalkH * 1.08f),
            6f * side,
            stalk,
            RhubarbOrder,
            HumanVisualShape.Box);
        AddHousePart(
            go.transform,
            "Groove",
            new Vector3(0.010f * side, stalkY + stalkH * 0.08f, 0f),
            new Vector2(0.004f, stalkH * 0.78f),
            6f * side,
            groove,
            RhubarbOrder + 1,
            HumanVisualShape.Box);

        // Huge wrinkled leaf — overlapping lobes, not
        // celery compound ovals, not fennel dill threads,
        // not an asparagus spearhead, not a leek fan.
        float leafY = crownY + stalkH * 0.98f;
        AddHousePart(
            go.transform,
            "Blade",
            new Vector3(-0.008f * side, leafY + 0.018f, 0f),
            new Vector2(0.054f * s, 0.036f * s),
            -10f * side,
            leaf,
            RhubarbOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Wrinkle",
            new Vector3(-0.018f * side, leafY + 0.012f, 0f),
            new Vector2(0.028f * s, 0.022f * s),
            -22f * side,
            pale,
            RhubarbOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Wrinkle",
            new Vector3(0.006f * side, leafY + 0.022f, 0f),
            new Vector2(0.024f * s, 0.018f * s),
            8f * side,
            pale,
            RhubarbOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Vein",
            new Vector3(-0.008f * side, leafY + 0.016f, 0f),
            new Vector2(0.004f, 0.028f * s),
            -10f * side,
            blush,
            RhubarbOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Blade",
            new Vector3(0.012f * side, leafY + 0.026f, 0f),
            new Vector2(0.050f * s, 0.034f * s),
            8f * side,
            leaf,
            RhubarbOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Wrinkle",
            new Vector3(0.022f * side, leafY + 0.020f, 0f),
            new Vector2(0.026f * s, 0.020f * s),
            18f * side,
            pale,
            RhubarbOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Vein",
            new Vector3(0.012f * side, leafY + 0.024f, 0f),
            new Vector2(0.004f, 0.026f * s),
            8f * side,
            blush,
            RhubarbOrder + 1,
            HumanVisualShape.Ellipse);
        if (!large)
            return;

        AddHousePart(
            go.transform,
            "Stalk",
            new Vector3(-0.018f * side, stalkY - 0.006f, 0f),
            new Vector2(0.014f * s, stalkH * 0.70f),
            -16f * side,
            stalk,
            RhubarbOrder,
            HumanVisualShape.Box);
        AddHousePart(
            go.transform,
            "Groove",
            new Vector3(-0.018f * side, stalkY, 0f),
            new Vector2(0.004f, stalkH * 0.54f),
            -16f * side,
            groove,
            RhubarbOrder + 1,
            HumanVisualShape.Box);
        AddHousePart(
            go.transform,
            "Blade",
            new Vector3(-0.022f * side, leafY - 0.004f, 0f),
            new Vector2(0.042f * s, 0.028f * s),
            -20f * side,
            leaf,
            RhubarbOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Wrinkle",
            new Vector3(-0.030f * side, leafY - 0.008f, 0f),
            new Vector2(0.022f * s, 0.016f * s),
            -28f * side,
            pale,
            RhubarbOrder + 1,
            HumanVisualShape.Ellipse);
    }

    private static void AddKalePlant(
        Transform parent,
        float x,
        float h,
        float tilt,
        float side,
        Color stem,
        Color leaf,
        Color ruffle,
        Color rib,
        bool large)
    {
        GameObject go = new GameObject("Plant");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(x, 0f, 0f);
        if (tilt * tilt > 0.01f)
            go.transform.localRotation = Quaternion.Euler(0f, 0f, tilt);

        float s = large ? 1f : 0.74f;
        // Woody crown ON the slab — not a cabbage buried
        // wrapper ball, not rhubarb thick red petioles,
        // not asparagus scaly spears.
        float crownW = 0.022f * s;
        float crownH = 0.014f * s;
        float crownY = crownH * 0.52f;
        AddHousePart(
            go.transform,
            "Crown",
            new Vector3(0f, crownY, 0f),
            new Vector2(crownW, crownH),
            0f,
            stem,
            KaleOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Stem",
            new Vector3(0f, crownY + crownH * 0.40f + h * 0.10f, 0f),
            new Vector2(0.010f * s, h * 0.22f),
            0f,
            stem,
            KaleOrder,
            HumanVisualShape.Box);

        // Standing rosette — tall ellipses, not cabbage
        // round wraps. Frill bubbles along the outer
        // edge are the corrugation; a pale midrib is
        // Lacinato, not a rhubarb vein on a huge paddle.
        float leafY = crownY + h * 0.42f;
        AddKaleLeaf(
            go.transform,
            -0.018f * side,
            leafY + 0.006f,
            -20f * side,
            0.026f * s,
            0.058f * s,
            -1f * side,
            leaf,
            ruffle,
            rib);
        AddKaleLeaf(
            go.transform,
            0.016f * side,
            leafY + 0.010f,
            16f * side,
            0.024f * s,
            0.054f * s,
            1f * side,
            leaf,
            ruffle,
            rib);
        AddKaleLeaf(
            go.transform,
            -0.006f * side,
            leafY + 0.016f,
            -8f * side,
            0.028f * s,
            0.066f * s,
            -1f * side,
            leaf,
            ruffle,
            rib);
        AddKaleLeaf(
            go.transform,
            0.008f * side,
            leafY + 0.018f,
            6f * side,
            0.026f * s,
            0.062f * s,
            1f * side,
            leaf,
            ruffle,
            rib);
        if (!large)
            return;

        AddKaleLeaf(
            go.transform,
            -0.024f * side,
            leafY - 0.004f,
            -28f * side,
            0.022f * s,
            0.046f * s,
            -1f * side,
            leaf,
            ruffle,
            rib);
        AddKaleLeaf(
            go.transform,
            0.020f * side,
            leafY + 0.002f,
            24f * side,
            0.020f * s,
            0.042f * s,
            1f * side,
            leaf,
            ruffle,
            rib);
    }

    private static void AddKaleLeaf(
        Transform parent,
        float x,
        float y,
        float tilt,
        float w,
        float h,
        float flare,
        Color leaf,
        Color ruffle,
        Color rib)
    {
        AddHousePart(
            parent,
            "Blade",
            new Vector3(x, y, 0f),
            new Vector2(w, h),
            tilt,
            leaf,
            KaleOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            parent,
            "Frill",
            new Vector3(x + flare * w * 0.42f, y + h * 0.18f, 0f),
            new Vector2(w * 0.42f, h * 0.28f),
            tilt + 12f * flare,
            ruffle,
            KaleOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            parent,
            "Frill",
            new Vector3(x + flare * w * 0.36f, y - h * 0.12f, 0f),
            new Vector2(w * 0.38f, h * 0.24f),
            tilt - 10f * flare,
            ruffle,
            KaleOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            parent,
            "Rib",
            new Vector3(x, y + h * 0.04f, 0f),
            new Vector2(0.004f, h * 0.72f),
            tilt,
            rib,
            KaleOrder + 1,
            HumanVisualShape.Ellipse);
    }

    private static void AddChardPlant(
        Transform parent,
        float x,
        float h,
        float tilt,
        float side,
        Color crown,
        Color stalk,
        Color groove,
        Color leaf,
        Color pale,
        bool large)
    {
        GameObject go = new GameObject("Plant");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(x, 0f, 0f);
        if (tilt * tilt > 0.01f)
            go.transform.localRotation = Quaternion.Euler(0f, 0f, tilt);

        float s = large ? 1f : 0.74f;
        // Woody crown ON the slab — not a kale standing
        // ruffle ball, not rhubarb thick red petioles,
        // not a cabbage buried wrapper.
        float crownW = 0.022f * s;
        float crownH = 0.014f * s;
        float crownY = crownH * 0.52f;
        AddHousePart(
            go.transform,
            "Crown",
            new Vector3(0f, crownY, 0f),
            new Vector2(crownW, crownH),
            0f,
            crown,
            ChardOrder,
            HumanVisualShape.Ellipse);

        // Bright petioles — thinner than rhubarb, not a
        // kale pale midrib. Each ends in a LOBED paddle,
        // wider than tall, not a standing kale ellipse.
        float stalkY = crownY + crownH * 0.30f;
        AddChardLeaf(
            go.transform,
            -0.010f * side,
            stalkY,
            h * 0.92f,
            -18f * side,
            s,
            -1f * side,
            stalk,
            groove,
            leaf,
            pale);
        AddChardLeaf(
            go.transform,
            0.008f * side,
            stalkY,
            h,
            8f * side,
            s,
            1f * side,
            stalk,
            groove,
            leaf,
            pale);
        if (!large)
            return;

        AddChardLeaf(
            go.transform,
            -0.018f * side,
            stalkY,
            h * 0.78f,
            -28f * side,
            s * 0.88f,
            -1f * side,
            stalk,
            groove,
            leaf,
            pale);
        AddChardLeaf(
            go.transform,
            0.016f * side,
            stalkY,
            h * 0.84f,
            20f * side,
            s * 0.90f,
            1f * side,
            stalk,
            groove,
            leaf,
            pale);
    }

    private static void AddChardLeaf(
        Transform parent,
        float x,
        float stalkY,
        float stalkH,
        float tilt,
        float s,
        float flare,
        Color stalk,
        Color groove,
        Color leaf,
        Color pale)
    {
        AddHousePart(
            parent,
            "Stalk",
            new Vector3(x, stalkY + stalkH * 0.50f, 0f),
            new Vector2(0.009f * s, stalkH),
            tilt,
            stalk,
            ChardOrder,
            HumanVisualShape.Box);
        AddHousePart(
            parent,
            "Groove",
            new Vector3(x - 0.002f * flare, stalkY + stalkH * 0.52f, 0f),
            new Vector2(0.003f * s, stalkH * 0.78f),
            tilt,
            groove,
            ChardOrder + 1,
            HumanVisualShape.Box);

        float leafY = stalkY + stalkH + 0.010f * s;
        AddHousePart(
            parent,
            "Blade",
            new Vector3(x - 0.012f * flare, leafY, 0f),
            new Vector2(0.036f * s, 0.024f * s),
            tilt - 8f * flare,
            leaf,
            ChardOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            parent,
            "Lobe",
            new Vector3(x + 0.010f * flare, leafY + 0.004f, 0f),
            new Vector2(0.028f * s, 0.020f * s),
            tilt + 10f * flare,
            pale,
            ChardOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            parent,
            "Rib",
            new Vector3(x, leafY + 0.002f, 0f),
            new Vector2(0.004f, 0.022f * s),
            tilt,
            groove,
            ChardOrder + 1,
            HumanVisualShape.Ellipse);
    }

    private static void AddSpinachPlant(
        Transform parent,
        float x,
        float h,
        float tilt,
        float side,
        Color crown,
        Color petiole,
        Color leaf,
        Color vein,
        bool large)
    {
        GameObject go = new GameObject("Plant");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(x, 0f, 0f);
        if (tilt * tilt > 0.01f)
            go.transform.localRotation = Quaternion.Euler(0f, 0f, tilt);

        float s = large ? 1f : 0.74f;
        // Woody crown ON the slab — not a kale standing
        // ruffle ball, not chard bright petioles, not a
        // cabbage buried wrapper.
        float crownW = 0.020f * s;
        float crownH = 0.012f * s;
        float crownY = crownH * 0.52f;
        AddHousePart(
            go.transform,
            "Crown",
            new Vector3(0f, crownY, 0f),
            new Vector2(crownW, crownH),
            0f,
            crown,
            SpinachOrder,
            HumanVisualShape.Ellipse);

        // LOW spreading rosette — ovate ellipses wider
        // than tall, not kale standing blades, not chard
        // lobed paddles on long stalks, not a cabbage
        // round wrap. Short pale petiole, not crimson.
        float leafY = crownY + h * 0.22f;
        AddSpinachLeaf(
            go.transform,
            -0.016f * side,
            leafY,
            -32f * side,
            0.032f * s,
            0.018f * s,
            -1f * side,
            petiole,
            leaf,
            vein);
        AddSpinachLeaf(
            go.transform,
            0.014f * side,
            leafY + 0.002f,
            26f * side,
            0.030f * s,
            0.016f * s,
            1f * side,
            petiole,
            leaf,
            vein);
        AddSpinachLeaf(
            go.transform,
            -0.004f * side,
            leafY + 0.006f,
            -10f * side,
            0.034f * s,
            0.020f * s,
            -1f * side,
            petiole,
            leaf,
            vein);
        if (!large)
            return;

        AddSpinachLeaf(
            go.transform,
            0.008f * side,
            leafY + 0.004f,
            14f * side,
            0.028f * s,
            0.016f * s,
            1f * side,
            petiole,
            leaf,
            vein);
        AddSpinachLeaf(
            go.transform,
            -0.022f * side,
            leafY - 0.002f,
            -42f * side,
            0.026f * s,
            0.014f * s,
            -1f * side,
            petiole,
            leaf,
            vein);
    }

    private static void AddSpinachLeaf(
        Transform parent,
        float x,
        float y,
        float tilt,
        float w,
        float h,
        float flare,
        Color petiole,
        Color leaf,
        Color vein)
    {
        // Short pale petiole under the blade — not a
        // chard crimson stalk, not a kale midrib.
        AddHousePart(
            parent,
            "Petiole",
            new Vector3(x * 0.35f, y - h * 0.22f, 0f),
            new Vector2(0.006f, h * 0.42f),
            tilt * 0.55f,
            petiole,
            SpinachOrder,
            HumanVisualShape.Box);
        AddHousePart(
            parent,
            "Blade",
            new Vector3(x, y, 0f),
            new Vector2(w, h),
            tilt,
            leaf,
            SpinachOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            parent,
            "Vein",
            new Vector3(x - 0.002f * flare, y + h * 0.04f, 0f),
            new Vector2(w * 0.22f, h * 0.55f),
            tilt,
            vein,
            SpinachOrder + 1,
            HumanVisualShape.Ellipse);
    }

    private static void AddLettuceHead(
        Transform parent,
        float x,
        float h,
        float tilt,
        float side,
        Color crown,
        Color leaf,
        Color pale,
        Color heart,
        Color rib,
        bool large)
    {
        GameObject go = new GameObject("Head");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(x, 0f, 0f);
        if (tilt * tilt > 0.01f)
            go.transform.localRotation = Quaternion.Euler(0f, 0f, tilt);

        float s = large ? 1f : 0.76f;
        // Woody crown ON the slab — not a cabbage buried
        // wrapper stub, not a spinach low pancake.
        float crownW = 0.022f * s;
        float crownH = 0.014f * s;
        float crownY = crownH * 0.52f;
        AddHousePart(
            go.transform,
            "Crown",
            new Vector3(0f, crownY, 0f),
            new Vector2(crownW, crownH),
            0f,
            crown,
            LettuceOrder,
            HumanVisualShape.Ellipse);

        // LOOSE cup — outer leaves splay and droop, not
        // cabbage wraps at ±12°, not spinach flats on
        // short petioles, not kale standing ruffles.
        float leafY = crownY + h * 0.42f;
        AddLettuceLeaf(
            go.transform,
            -0.022f * side,
            leafY - 0.004f,
            -48f * side,
            0.044f * s,
            0.030f * s,
            -1f * side,
            leaf,
            pale,
            rib);
        AddLettuceLeaf(
            go.transform,
            0.020f * side,
            leafY - 0.002f,
            42f * side,
            0.040f * s,
            0.028f * s,
            1f * side,
            leaf,
            pale,
            rib);
        AddLettuceLeaf(
            go.transform,
            -0.008f * side,
            leafY + 0.008f,
            -18f * side,
            0.036f * s,
            0.028f * s,
            -1f * side,
            pale,
            heart,
            rib);
        AddLettuceLeaf(
            go.transform,
            0.010f * side,
            leafY + 0.010f,
            14f * side,
            0.034f * s,
            0.026f * s,
            1f * side,
            pale,
            heart,
            rib);
        AddHousePart(
            go.transform,
            "Heart",
            new Vector3(0.002f * side, leafY + 0.016f, 0f),
            new Vector2(0.024f * s, 0.022f * s),
            4f * side,
            heart,
            LettuceOrder + 1,
            HumanVisualShape.Ellipse);
        if (!large)
            return;

        AddLettuceLeaf(
            go.transform,
            -0.028f * side,
            leafY - 0.008f,
            -62f * side,
            0.038f * s,
            0.024f * s,
            -1f * side,
            leaf,
            pale,
            rib);
        AddLettuceLeaf(
            go.transform,
            0.026f * side,
            leafY - 0.006f,
            54f * side,
            0.036f * s,
            0.022f * s,
            1f * side,
            leaf,
            pale,
            rib);
    }

    private static void AddLettuceLeaf(
        Transform parent,
        float x,
        float y,
        float tilt,
        float w,
        float h,
        float flare,
        Color leaf,
        Color pale,
        Color rib)
    {
        // Cupped blade with a paler frill — loose leaf,
        // not a cabbage wrap stack, not a spinach ovate
        // on a short petiole, not a kale midrib.
        AddHousePart(
            parent,
            "Blade",
            new Vector3(x, y, 0f),
            new Vector2(w, h),
            tilt,
            leaf,
            LettuceOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            parent,
            "Frill",
            new Vector3(x + 0.003f * flare, y + h * 0.08f, 0f),
            new Vector2(w * 0.72f, h * 0.42f),
            tilt + 8f * flare,
            pale,
            LettuceOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            parent,
            "Rib",
            new Vector3(x - 0.002f * flare, y + h * 0.02f, 0f),
            new Vector2(w * 0.18f, h * 0.48f),
            tilt,
            rib,
            LettuceOrder + 1,
            HumanVisualShape.Ellipse);
    }

    private static void AddBroccoliHead(
        Transform parent,
        float x,
        float h,
        float tilt,
        float side,
        Color crown,
        Color stem,
        Color groove,
        Color leaf,
        Color head,
        Color bead,
        bool large)
    {
        GameObject go = new GameObject("Head");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(x, 0f, 0f);
        if (tilt * tilt > 0.01f)
            go.transform.localRotation = Quaternion.Euler(0f, 0f, tilt);

        float s = large ? 1f : 0.76f;
        // Woody crown ON the slab — not a cabbage buried
        // wrapper stub, not a lettuce tiny cup foot.
        float crownW = 0.026f * s;
        float crownH = 0.014f * s;
        float crownY = crownH * 0.52f;
        AddHousePart(
            go.transform,
            "Crown",
            new Vector3(0f, crownY, 0f),
            new Vector2(crownW, crownH),
            0f,
            crown,
            BroccoliOrder,
            HumanVisualShape.Ellipse);

        // THICK stem — wider than asparagus's 6 mm
        // spears, not a lettuce no-stem cup, not a
        // cabbage stub buried under wraps.
        float stemH = h * 0.62f;
        float stemY = crownY + crownH * 0.22f + stemH * 0.48f;
        AddHousePart(
            go.transform,
            "Stem",
            new Vector3(0f, stemY, 0f),
            new Vector2(0.020f * s, stemH),
            0f,
            stem,
            BroccoliOrder,
            HumanVisualShape.Box);
        AddHousePart(
            go.transform,
            "Groove",
            new Vector3(0.003f * side, stemY, 0f),
            new Vector2(0.006f * s, stemH * 0.88f),
            0f,
            groove,
            BroccoliOrder + 1,
            HumanVisualShape.Box);

        // Lance leaves on the stem — not lettuce splay
        // ±42…62°, not cabbage wraps ±12°, not spinach
        // ovates on short petioles.
        float leafY = stemY + stemH * 0.12f;
        AddHousePart(
            go.transform,
            "Leaf",
            new Vector3(-0.018f * side, leafY, 0f),
            new Vector2(0.028f * s, 0.014f * s),
            -22f * side,
            leaf,
            BroccoliOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Leaf",
            new Vector3(0.016f * side, leafY + 0.004f, 0f),
            new Vector2(0.024f * s, 0.012f * s),
            18f * side,
            leaf,
            BroccoliOrder,
            HumanVisualShape.Ellipse);

        // TIGHT floret cluster — beads packed on a
        // dome, not a cabbage wrap ball, not a lettuce
        // pale heart, not a spinach low pancake.
        float headY = crownY + stemH + 0.018f * s;
        AddHousePart(
            go.transform,
            "Dome",
            new Vector3(0.001f * side, headY, 0f),
            new Vector2(0.040f * s, 0.028f * s),
            0f,
            head,
            BroccoliOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Bead",
            new Vector3(-0.012f * side, headY + 0.010f, 0f),
            new Vector2(0.016f * s, 0.014f * s),
            -8f * side,
            bead,
            BroccoliOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Bead",
            new Vector3(0.012f * side, headY + 0.008f, 0f),
            new Vector2(0.014f * s, 0.012f * s),
            10f * side,
            bead,
            BroccoliOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Bead",
            new Vector3(0f, headY + 0.014f, 0f),
            new Vector2(0.018f * s, 0.014f * s),
            0f,
            bead,
            BroccoliOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Bead",
            new Vector3(-0.006f * side, headY + 0.004f, 0f),
            new Vector2(0.012f * s, 0.012f * s),
            -4f * side,
            head,
            BroccoliOrder + 1,
            HumanVisualShape.Ellipse);
        if (!large)
            return;

        // Side floret on a short branch — broccoli
        // ramifies, cabbage and lettuce do not.
        AddHousePart(
            go.transform,
            "Branch",
            new Vector3(0.016f * side, stemY + stemH * 0.22f, 0f),
            new Vector2(0.018f * s, 0.008f * s),
            28f * side,
            stem,
            BroccoliOrder,
            HumanVisualShape.Box);
        AddHousePart(
            go.transform,
            "Bead",
            new Vector3(0.026f * side, stemY + stemH * 0.36f, 0f),
            new Vector2(0.016f * s, 0.014f * s),
            12f * side,
            bead,
            BroccoliOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Leaf",
            new Vector3(-0.014f * side, stemY - stemH * 0.08f, 0f),
            new Vector2(0.022f * s, 0.010f * s),
            -16f * side,
            leaf,
            BroccoliOrder,
            HumanVisualShape.Ellipse);
    }

    private static void AddCauliflowerHead(
        Transform parent,
        float x,
        float h,
        float tilt,
        float side,
        Color crown,
        Color stem,
        Color groove,
        Color leaf,
        Color curd,
        Color bead,
        bool large)
    {
        GameObject go = new GameObject("Head");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(x, 0f, 0f);
        if (tilt * tilt > 0.01f)
            go.transform.localRotation = Quaternion.Euler(0f, 0f, tilt);

        float s = large ? 1f : 0.76f;
        // Woody crown ON the slab — not a cabbage buried
        // wrapper stub, not a lettuce tiny cup foot.
        float crownW = 0.026f * s;
        float crownH = 0.014f * s;
        float crownY = crownH * 0.52f;
        AddHousePart(
            go.transform,
            "Crown",
            new Vector3(0f, crownY, 0f),
            new Vector2(crownW, crownH),
            0f,
            crown,
            CauliflowerOrder,
            HumanVisualShape.Ellipse);

        // THICK stem — same 20 mm as broccoli, cream-
        // green not Calabrese, not a lettuce no-stem
        // cup, not a cabbage stub buried under wraps.
        float stemH = h * 0.58f;
        float stemY = crownY + crownH * 0.22f + stemH * 0.48f;
        AddHousePart(
            go.transform,
            "Stem",
            new Vector3(0f, stemY, 0f),
            new Vector2(0.020f * s, stemH),
            0f,
            stem,
            CauliflowerOrder,
            HumanVisualShape.Box);
        AddHousePart(
            go.transform,
            "Groove",
            new Vector3(0.003f * side, stemY, 0f),
            new Vector2(0.006f * s, stemH * 0.88f),
            0f,
            groove,
            CauliflowerOrder + 1,
            HumanVisualShape.Box);

        // Jacket leaves hug the WHITE curd — not
        // broccoli lances on the stem at −22°, not
        // lettuce splay ±42…62°, not cabbage wraps
        // ±12° of a green ball.
        float jacketY = stemY + stemH * 0.42f;
        AddHousePart(
            go.transform,
            "Jacket",
            new Vector3(-0.022f * side, jacketY, 0f),
            new Vector2(0.032f * s, 0.022f * s),
            -16f * side,
            leaf,
            CauliflowerOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Jacket",
            new Vector3(0.020f * side, jacketY + 0.004f, 0f),
            new Vector2(0.028f * s, 0.018f * s),
            14f * side,
            leaf,
            CauliflowerOrder,
            HumanVisualShape.Ellipse);

        // TIGHT cream curd — packed mound, wider than
        // broccoli's 40 mm green dome, not a cabbage
        // wrap ball, not a lettuce pale heart.
        float headY = crownY + stemH + 0.016f * s;
        AddHousePart(
            go.transform,
            "Curd",
            new Vector3(0.001f * side, headY, 0f),
            new Vector2(0.052f * s, 0.038f * s),
            0f,
            curd,
            CauliflowerOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Bead",
            new Vector3(-0.014f * side, headY + 0.008f, 0f),
            new Vector2(0.018f * s, 0.016f * s),
            -6f * side,
            bead,
            CauliflowerOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Bead",
            new Vector3(0.014f * side, headY + 0.006f, 0f),
            new Vector2(0.016f * s, 0.014f * s),
            8f * side,
            bead,
            CauliflowerOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Bead",
            new Vector3(0f, headY + 0.012f, 0f),
            new Vector2(0.020f * s, 0.016f * s),
            0f,
            bead,
            CauliflowerOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Bead",
            new Vector3(-0.006f * side, headY + 0.002f, 0f),
            new Vector2(0.014f * s, 0.014f * s),
            -4f * side,
            curd,
            CauliflowerOrder + 1,
            HumanVisualShape.Ellipse);
        if (!large)
            return;

        // Extra packed curd on the large head — no
        // side Branch: broccoli ramifies, cauliflower
        // is one mound.
        AddHousePart(
            go.transform,
            "Bead",
            new Vector3(0.008f * side, headY + 0.010f, 0f),
            new Vector2(0.014f * s, 0.012f * s),
            6f * side,
            bead,
            CauliflowerOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Jacket",
            new Vector3(-0.016f * side, stemY - stemH * 0.06f, 0f),
            new Vector2(0.024f * s, 0.012f * s),
            -12f * side,
            leaf,
            CauliflowerOrder,
            HumanVisualShape.Ellipse);
    }

    private static void AddBrusselsHead(
        Transform parent,
        float x,
        float h,
        float tilt,
        float side,
        Color crown,
        Color stem,
        Color groove,
        Color leaf,
        Color sprout,
        Color wrap,
        bool large)
    {
        GameObject go = new GameObject("Head");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(x, 0f, 0f);
        if (tilt * tilt > 0.01f)
            go.transform.localRotation = Quaternion.Euler(0f, 0f, tilt);

        float s = large ? 1f : 0.76f;
        // Woody crown ON the slab — not a cabbage buried
        // wrapper stub, not a broccoli short thick foot.
        float crownW = 0.024f * s;
        float crownH = 0.014f * s;
        float crownY = crownH * 0.52f;
        AddHousePart(
            go.transform,
            "Crown",
            new Vector3(0f, crownY, 0f),
            new Vector2(crownW, crownH),
            0f,
            crown,
            BrusselsOrder,
            HumanVisualShape.Ellipse);

        // TALL stem — taller than broccoli/cauliflower
        // 20 mm heads, thinner than their 20 mm trunk,
        // thicker than asparagus 6 mm spears. The
        // silhouette is sprouts ON the stalk, not a
        // dome, not a cream mound, not a wrap ball.
        float stemH = h * 0.88f;
        float stemY = crownY + crownH * 0.22f + stemH * 0.48f;
        AddHousePart(
            go.transform,
            "Stem",
            new Vector3(0f, stemY, 0f),
            new Vector2(0.016f * s, stemH),
            0f,
            stem,
            BrusselsOrder,
            HumanVisualShape.Box);
        AddHousePart(
            go.transform,
            "Groove",
            new Vector3(0.003f * side, stemY, 0f),
            new Vector2(0.005f * s, stemH * 0.90f),
            0f,
            groove,
            BrusselsOrder + 1,
            HumanVisualShape.Box);

        // TIGHT GREEN mini-cabbages along the stalk —
        // not broccoli beads packed on a 40 mm dome,
        // not cauliflower ivory curd 52 mm, not one
        // cabbage wrap ball on the slab.
        float sproutW = 0.014f * s;
        float sproutH = 0.013f * s;
        AddHousePart(
            go.transform,
            "Sprout",
            new Vector3(-0.011f * side, stemY - stemH * 0.34f, 0f),
            new Vector2(sproutW, sproutH),
            -6f * side,
            sprout,
            BrusselsOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Sprout",
            new Vector3(0.011f * side, stemY - stemH * 0.18f, 0f),
            new Vector2(sproutW * 0.92f, sproutH * 0.92f),
            8f * side,
            wrap,
            BrusselsOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Sprout",
            new Vector3(-0.010f * side, stemY - stemH * 0.02f, 0f),
            new Vector2(sproutW, sproutH),
            -4f * side,
            sprout,
            BrusselsOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Sprout",
            new Vector3(0.010f * side, stemY + stemH * 0.14f, 0f),
            new Vector2(sproutW * 0.90f, sproutH * 0.90f),
            6f * side,
            wrap,
            BrusselsOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Sprout",
            new Vector3(-0.009f * side, stemY + stemH * 0.30f, 0f),
            new Vector2(sproutW * 0.88f, sproutH * 0.88f),
            -8f * side,
            sprout,
            BrusselsOrder + 1,
            HumanVisualShape.Ellipse);

        // Top tuft — a few small leaves at the stalk
        // tip, not a broccoli dome, not a cream curd.
        float tuftY = stemY + stemH * 0.46f;
        AddHousePart(
            go.transform,
            "Leaf",
            new Vector3(-0.012f * side, tuftY, 0f),
            new Vector2(0.018f * s, 0.010f * s),
            -18f * side,
            leaf,
            BrusselsOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Leaf",
            new Vector3(0.012f * side, tuftY + 0.002f, 0f),
            new Vector2(0.016f * s, 0.009f * s),
            16f * side,
            leaf,
            BrusselsOrder,
            HumanVisualShape.Ellipse);
        if (!large)
            return;

        AddHousePart(
            go.transform,
            "Sprout",
            new Vector3(0.009f * side, stemY + stemH * 0.38f, 0f),
            new Vector2(sproutW * 0.86f, sproutH * 0.86f),
            4f * side,
            wrap,
            BrusselsOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Leaf",
            new Vector3(0f, tuftY + 0.008f, 0f),
            new Vector2(0.014f * s, 0.008f * s),
            0f,
            leaf,
            BrusselsOrder,
            HumanVisualShape.Ellipse);
    }

    private static void AddArtichokeHead(
        Transform parent,
        float x,
        float h,
        float tilt,
        float side,
        Color crown,
        Color stem,
        Color groove,
        Color leaf,
        Color scale,
        Color rim,
        bool large)
    {
        GameObject go = new GameObject("Head");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(x, 0f, 0f);
        if (tilt * tilt > 0.01f)
            go.transform.localRotation = Quaternion.Euler(0f, 0f, tilt);

        float s = large ? 1f : 0.76f;
        // Woody crown ON the slab — not a thistle
        // verge stub, not a Brussels thin foot.
        float crownW = 0.026f * s;
        float crownH = 0.014f * s;
        float crownY = crownH * 0.52f;
        AddHousePart(
            go.transform,
            "Crown",
            new Vector3(0f, crownY, 0f),
            new Vector2(crownW, crownH),
            0f,
            crown,
            ArtichokeOrder,
            HumanVisualShape.Ellipse);

        // THICK stem — same 20 mm as broccoli /
        // cauliflower, not Brussels 16 mm, not
        // thistle 10 mm shaft. The silhouette is ONE
        // scaly bud on the tip, not sprouts along the
        // stalk, not a purple verge bloom.
        float stemH = h * 0.58f;
        float stemY = crownY + crownH * 0.22f + stemH * 0.48f;
        AddHousePart(
            go.transform,
            "Stem",
            new Vector3(0f, stemY, 0f),
            new Vector2(0.020f * s, stemH),
            0f,
            stem,
            ArtichokeOrder,
            HumanVisualShape.Box);
        AddHousePart(
            go.transform,
            "Groove",
            new Vector3(0.003f * side, stemY, 0f),
            new Vector2(0.006f * s, stemH * 0.88f),
            0f,
            groove,
            ArtichokeOrder + 1,
            HumanVisualShape.Box);

        // Silvery lance leaves — modest tilt, not
        // thistle splay ±38°, not Brussels tuft at
        // the tip, not broccoli jacket under a dome.
        float leafY = stemY + stemH * 0.10f;
        AddHousePart(
            go.transform,
            "Leaf",
            new Vector3(-0.018f * side, leafY, 0f),
            new Vector2(0.028f * s, 0.013f * s),
            -18f * side,
            leaf,
            ArtichokeOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Leaf",
            new Vector3(0.016f * side, leafY + 0.004f, 0f),
            new Vector2(0.024f * s, 0.011f * s),
            16f * side,
            leaf,
            ArtichokeOrder,
            HumanVisualShape.Ellipse);

        // SCALY globe — overlapping pointed bracts, not
        // broccoli beads on a 40×28 mm dome, not
        // cauliflower ivory 52×38 mm, not mini
        // cabbages along a tall stem, not a thistle
        // purple 28×26 mm bloom.
        float headY = crownY + stemH + 0.022f * s;
        AddHousePart(
            go.transform,
            "Bud",
            new Vector3(0.001f * side, headY, 0f),
            new Vector2(0.036f * s, 0.038f * s),
            0f,
            scale,
            ArtichokeOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Scale",
            new Vector3(-0.014f * side, headY - 0.006f * s, 0f),
            new Vector2(0.018f * s, 0.016f * s),
            -26f * side,
            rim,
            ArtichokeOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Scale",
            new Vector3(0.014f * side, headY - 0.004f * s, 0f),
            new Vector2(0.016f * s, 0.015f * s),
            22f * side,
            rim,
            ArtichokeOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Scale",
            new Vector3(-0.010f * side, headY + 0.006f * s, 0f),
            new Vector2(0.016f * s, 0.015f * s),
            -14f * side,
            scale,
            ArtichokeOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Scale",
            new Vector3(0.010f * side, headY + 0.008f * s, 0f),
            new Vector2(0.015f * s, 0.014f * s),
            16f * side,
            rim,
            ArtichokeOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Lid",
            new Vector3(0f, headY + 0.016f * s, 0f),
            new Vector2(0.018f * s, 0.014f * s),
            0f,
            rim,
            ArtichokeOrder + 1,
            HumanVisualShape.Ellipse);
        if (!large)
            return;

        AddHousePart(
            go.transform,
            "Heart",
            new Vector3(0.002f * side, headY + 0.002f * s, 0f),
            new Vector2(0.014f * s, 0.012f * s),
            0f,
            rim,
            ArtichokeOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Scale",
            new Vector3(0f, headY + 0.020f * s, 0f),
            new Vector2(0.012f * s, 0.010f * s),
            0f,
            scale,
            ArtichokeOrder + 1,
            HumanVisualShape.Ellipse);
    }

    private static void AddEggplantHead(
        Transform parent,
        float x,
        float h,
        float tilt,
        float side,
        Color crown,
        Color stem,
        Color groove,
        Color leaf,
        Color calyx,
        Color skin,
        Color belly,
        Color gloss,
        bool large)
    {
        GameObject go = new GameObject("Head");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(x, 0f, 0f);
        if (tilt * tilt > 0.01f)
            go.transform.localRotation = Quaternion.Euler(0f, 0f, tilt);

        float s = large ? 1f : 0.76f;
        // Woody crown ON the slab — not a thistle
        // verge stub, not a tomato stake foot.
        float crownW = 0.026f * s;
        float crownH = 0.014f * s;
        float crownY = crownH * 0.52f;
        AddHousePart(
            go.transform,
            "Crown",
            new Vector3(0f, crownY, 0f),
            new Vector2(crownW, crownH),
            0f,
            crown,
            EggplantOrder,
            HumanVisualShape.Ellipse);

        // GREEN stem — 12 mm, not artichoke thick 20 mm,
        // not thistle 10 mm shaft, not a brown tomato
        // stake. The silhouette is ONE hanging teardrop,
        // not a scaly bud on the tip, not round balls
        // along a vine, not a purple verge bloom.
        float stemH = h * 0.52f;
        float stemY = crownY + crownH * 0.22f + stemH * 0.48f;
        AddHousePart(
            go.transform,
            "Stem",
            new Vector3(0f, stemY, 0f),
            new Vector2(0.012f * s, stemH),
            0f,
            stem,
            EggplantOrder,
            HumanVisualShape.Box);
        AddHousePart(
            go.transform,
            "Groove",
            new Vector3(0.003f * side, stemY, 0f),
            new Vector2(0.004f * s, stemH * 0.88f),
            0f,
            groove,
            EggplantOrder + 1,
            HumanVisualShape.Box);

        // Broad kitchen leaves — modest tilt, not
        // thistle splay ±38°, not artichoke silvery
        // lances, not tomato vine leaflets along a cane.
        float leafY = stemY + stemH * 0.08f;
        AddHousePart(
            go.transform,
            "Leaf",
            new Vector3(-0.018f * side, leafY, 0f),
            new Vector2(0.030f * s, 0.014f * s),
            -16f * side,
            leaf,
            EggplantOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Leaf",
            new Vector3(0.016f * side, leafY + 0.004f, 0f),
            new Vector2(0.026f * s, 0.012f * s),
            14f * side,
            leaf,
            EggplantOrder,
            HumanVisualShape.Ellipse);

        // GLOSSY teardrop hanging from a green calyx —
        // 20×46 mm, not tomato round 22×24 mm, not
        // artichoke scaly 36×38 mm globe, not thistle
        // purple 28×26 mm bloom on a thin shaft.
        float calyxY = crownY + stemH + 0.010f * s;
        AddHousePart(
            go.transform,
            "Calyx",
            new Vector3(0.010f * side, calyxY, 0f),
            new Vector2(0.018f * s, 0.010f * s),
            8f * side,
            calyx,
            EggplantOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Sepal",
            new Vector3(0.002f * side, calyxY - 0.004f * s, 0f),
            new Vector2(0.010f * s, 0.008f * s),
            -18f * side,
            calyx,
            EggplantOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Sepal",
            new Vector3(0.016f * side, calyxY - 0.002f * s, 0f),
            new Vector2(0.010f * s, 0.008f * s),
            16f * side,
            calyx,
            EggplantOrder + 1,
            HumanVisualShape.Ellipse);

        float fruitH = 0.046f * s;
        float fruitY = calyxY - fruitH * 0.42f;
        AddHousePart(
            go.transform,
            "Fruit",
            new Vector3(0.012f * side, fruitY, 0f),
            new Vector2(0.020f * s, fruitH),
            6f * side,
            skin,
            EggplantOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Belly",
            new Vector3(0.014f * side, fruitY - 0.006f * s, 0f),
            new Vector2(0.012f * s, fruitH * 0.62f),
            8f * side,
            belly,
            EggplantOrder + 2,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Gloss",
            new Vector3(0.006f * side, fruitY + 0.008f * s, 0f),
            new Vector2(0.007f * s, 0.016f * s),
            -12f * side,
            gloss,
            EggplantOrder + 2,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Tip",
            new Vector3(0.012f * side, fruitY - fruitH * 0.42f, 0f),
            new Vector2(0.010f * s, 0.012f * s),
            4f * side,
            belly,
            EggplantOrder + 1,
            HumanVisualShape.Ellipse);
        if (!large)
            return;

        AddHousePart(
            go.transform,
            "Gloss",
            new Vector3(0.008f * side, fruitY + 0.002f * s, 0f),
            new Vector2(0.005f * s, 0.010f * s),
            -8f * side,
            gloss,
            EggplantOrder + 2,
            HumanVisualShape.Ellipse);
    }

    private static void AddPepperHead(
        Transform parent,
        float x,
        float h,
        float tilt,
        float side,
        Color crown,
        Color stem,
        Color groove,
        Color leaf,
        Color calyx,
        Color skin,
        Color belly,
        Color gloss,
        bool large)
    {
        GameObject go = new GameObject("Head");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(x, 0f, 0f);
        if (tilt * tilt > 0.01f)
            go.transform.localRotation = Quaternion.Euler(0f, 0f, tilt);

        float s = large ? 1f : 0.76f;
        // Woody crown ON the slab — not a thistle
        // verge stub, not a tomato stake foot.
        float crownW = 0.026f * s;
        float crownH = 0.014f * s;
        float crownY = crownH * 0.52f;
        AddHousePart(
            go.transform,
            "Crown",
            new Vector3(0f, crownY, 0f),
            new Vector2(crownW, crownH),
            0f,
            crown,
            PepperOrder,
            HumanVisualShape.Ellipse);

        // GREEN stem — 12 mm, not artichoke thick 20 mm,
        // not thistle 10 mm shaft, not a brown tomato
        // stake. The silhouette is ONE blocky pod, not
        // a hanging teardrop, not round balls along a
        // vine, not a scaly bud on the tip.
        float stemH = h * 0.48f;
        float stemY = crownY + crownH * 0.22f + stemH * 0.48f;
        AddHousePart(
            go.transform,
            "Stem",
            new Vector3(0f, stemY, 0f),
            new Vector2(0.012f * s, stemH),
            0f,
            stem,
            PepperOrder,
            HumanVisualShape.Box);
        AddHousePart(
            go.transform,
            "Groove",
            new Vector3(0.003f * side, stemY, 0f),
            new Vector2(0.004f * s, stemH * 0.88f),
            0f,
            groove,
            PepperOrder + 1,
            HumanVisualShape.Box);

        // Broad kitchen leaves — modest tilt, not
        // thistle splay ±38°, not artichoke silvery
        // lances, not tomato vine leaflets along a cane.
        float leafY = stemY + stemH * 0.08f;
        AddHousePart(
            go.transform,
            "Leaf",
            new Vector3(-0.018f * side, leafY, 0f),
            new Vector2(0.030f * s, 0.014f * s),
            -16f * side,
            leaf,
            PepperOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Leaf",
            new Vector3(0.016f * side, leafY + 0.004f, 0f),
            new Vector2(0.026f * s, 0.012f * s),
            14f * side,
            leaf,
            PepperOrder,
            HumanVisualShape.Ellipse);

        // GLOSSY blocky pod under a green calyx —
        // 28×30 mm BOX, not eggplant hanging teardrop
        // 20×46 mm, not tomato round 22×24 mm, not
        // artichoke scaly 36×38 mm globe.
        float calyxY = crownY + stemH + 0.010f * s;
        AddHousePart(
            go.transform,
            "Calyx",
            new Vector3(0.008f * side, calyxY, 0f),
            new Vector2(0.018f * s, 0.010f * s),
            0f,
            calyx,
            PepperOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Sepal",
            new Vector3(-0.002f * side, calyxY - 0.004f * s, 0f),
            new Vector2(0.010f * s, 0.008f * s),
            -22f * side,
            calyx,
            PepperOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Sepal",
            new Vector3(0.016f * side, calyxY - 0.004f * s, 0f),
            new Vector2(0.010f * s, 0.008f * s),
            22f * side,
            calyx,
            PepperOrder + 1,
            HumanVisualShape.Ellipse);

        float fruitH = 0.030f * s;
        float fruitW = 0.028f * s;
        float fruitY = calyxY - fruitH * 0.42f;
        AddHousePart(
            go.transform,
            "Pod",
            new Vector3(0.008f * side, fruitY, 0f),
            new Vector2(fruitW, fruitH),
            0f,
            skin,
            PepperOrder + 1,
            HumanVisualShape.Box);
        AddHousePart(
            go.transform,
            "Lobe",
            new Vector3(-0.004f * side, fruitY + 0.002f * s, 0f),
            new Vector2(0.014f * s, fruitH * 0.88f),
            -4f * side,
            belly,
            PepperOrder + 1,
            HumanVisualShape.Box);
        AddHousePart(
            go.transform,
            "Lobe",
            new Vector3(0.018f * side, fruitY + 0.002f * s, 0f),
            new Vector2(0.014f * s, fruitH * 0.88f),
            4f * side,
            skin,
            PepperOrder + 1,
            HumanVisualShape.Box);
        AddHousePart(
            go.transform,
            "Shoulder",
            new Vector3(0.008f * side, fruitY + fruitH * 0.28f, 0f),
            new Vector2(fruitW * 0.92f, 0.010f * s),
            0f,
            skin,
            PepperOrder + 2,
            HumanVisualShape.Box);
        AddHousePart(
            go.transform,
            "Belly",
            new Vector3(0.010f * side, fruitY - 0.002f * s, 0f),
            new Vector2(0.016f * s, fruitH * 0.58f),
            0f,
            belly,
            PepperOrder + 2,
            HumanVisualShape.Box);
        AddHousePart(
            go.transform,
            "Gloss",
            new Vector3(0.002f * side, fruitY + 0.006f * s, 0f),
            new Vector2(0.007f * s, 0.012f * s),
            -8f * side,
            gloss,
            PepperOrder + 2,
            HumanVisualShape.Box);
        AddHousePart(
            go.transform,
            "Dimple",
            new Vector3(0.008f * side, fruitY - fruitH * 0.42f, 0f),
            new Vector2(0.010f * s, 0.008f * s),
            0f,
            belly,
            PepperOrder + 1,
            HumanVisualShape.Ellipse);
        if (!large)
            return;

        AddHousePart(
            go.transform,
            "Gloss",
            new Vector3(0.004f * side, fruitY + 0.001f * s, 0f),
            new Vector2(0.005f * s, 0.008f * s),
            -6f * side,
            gloss,
            PepperOrder + 2,
            HumanVisualShape.Box);
    }

    private static void AddCucumberHead(
        Transform parent,
        float x,
        float h,
        float tilt,
        float side,
        Color crown,
        Color vine,
        Color groove,
        Color leaf,
        Color skin,
        Color belly,
        Color stripe,
        Color gloss,
        Color wart,
        Color bloom,
        bool large)
    {
        GameObject go = new GameObject("Head");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(x, 0f, 0f);
        if (tilt * tilt > 0.01f)
            go.transform.localRotation = Quaternion.Euler(0f, 0f, tilt);

        float s = large ? 1f : 0.78f;
        // Vine foot ON the slab — not a pepper crown
        // under an upright stem, not a tomato stake
        // foot, not an eggplant woody stub.
        float crownW = 0.024f * s;
        float crownH = 0.012f * s;
        float crownY = crownH * 0.52f;
        AddHousePart(
            go.transform,
            "Crown",
            new Vector3(0f, crownY, 0f),
            new Vector2(crownW, crownH),
            0f,
            crown,
            CucumberOrder,
            HumanVisualShape.Ellipse);

        // SPRAWLING vine — 8 mm cane, not pepper
        // upright 12 mm, not artichoke thick 20 mm,
        // not a brown tomato stake. The silhouette is
        // a LONG fruit along X, not a blocky hanging
        // pod, not a vertical teardrop, not round
        // balls along a cane.
        float vineW = (0.036f + h * 0.16f) * s;
        float vineH = 0.008f * s;
        float vineY = crownY + 0.010f * s;
        float vineX = 0.016f * side;
        AddHousePart(
            go.transform,
            "Vine",
            new Vector3(vineX, vineY, 0f),
            new Vector2(vineW, vineH),
            -16f * side,
            vine,
            CucumberOrder,
            HumanVisualShape.Box);
        AddHousePart(
            go.transform,
            "Groove",
            new Vector3(vineX, vineY + 0.002f * s, 0f),
            new Vector2(vineW * 0.88f, 0.003f * s),
            -16f * side,
            groove,
            CucumberOrder + 1,
            HumanVisualShape.Box);

        // Palmate kitchen leaf — broader than pepper's
        // modest pair, not thistle splay, not tomato
        // leaflets along a cane.
        float leafY = vineY + 0.010f * s;
        AddHousePart(
            go.transform,
            "Leaf",
            new Vector3(-0.016f * side, leafY, 0f),
            new Vector2(0.038f * s, 0.018f * s),
            -22f * side,
            leaf,
            CucumberOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Lobe",
            new Vector3(-0.028f * side, leafY + 0.004f * s, 0f),
            new Vector2(0.018f * s, 0.012f * s),
            -38f * side,
            leaf,
            CucumberOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Tendril",
            new Vector3(vineX + 0.012f * side, vineY + 0.014f * s, 0f),
            new Vector2(0.004f * s, 0.016f * s),
            42f * side,
            vine,
            CucumberOrder + 1,
            HumanVisualShape.Box);

        // LONG green fruit along the vine — 52×14 mm
        // ELLIPSE, not pepper blocky BOX 28×30 mm, not
        // eggplant hanging teardrop 20×46 mm, not
        // tomato round 22×24 mm. Pedicel is a short
        // nub, not a calyx over a hanging pod.
        float fruitW = 0.052f * s;
        float fruitH = 0.014f * s;
        float fruitX = vineX + 0.010f * side;
        float fruitY = vineY + 0.002f * s;
        AddHousePart(
            go.transform,
            "Pedicel",
            new Vector3(fruitX - 0.022f * side, fruitY + 0.004f * s, 0f),
            new Vector2(0.010f * s, 0.006f * s),
            -12f * side,
            vine,
            CucumberOrder + 1,
            HumanVisualShape.Box);
        AddHousePart(
            go.transform,
            "Fruit",
            new Vector3(fruitX, fruitY, 0f),
            new Vector2(fruitW, fruitH),
            -10f * side,
            skin,
            CucumberOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Belly",
            new Vector3(fruitX, fruitY - 0.003f * s, 0f),
            new Vector2(fruitW * 0.78f, fruitH * 0.42f),
            -10f * side,
            belly,
            CucumberOrder + 2,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Stripe",
            new Vector3(fruitX, fruitY + 0.002f * s, 0f),
            new Vector2(fruitW * 0.72f, 0.004f * s),
            -10f * side,
            stripe,
            CucumberOrder + 2,
            HumanVisualShape.Box);
        AddHousePart(
            go.transform,
            "Wart",
            new Vector3(fruitX - 0.010f * side, fruitY + 0.003f * s, 0f),
            new Vector2(0.005f * s, 0.005f * s),
            0f,
            wart,
            CucumberOrder + 2,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Wart",
            new Vector3(fruitX + 0.008f * side, fruitY - 0.002f * s, 0f),
            new Vector2(0.004f * s, 0.004f * s),
            0f,
            wart,
            CucumberOrder + 2,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Gloss",
            new Vector3(fruitX - 0.006f * side, fruitY + 0.003f * s, 0f),
            new Vector2(0.010f * s, 0.004f * s),
            -10f * side,
            gloss,
            CucumberOrder + 2,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Bloom",
            new Vector3(fruitX + fruitW * 0.42f * side, fruitY, 0f),
            new Vector2(0.008f * s, 0.007f * s),
            0f,
            bloom,
            CucumberOrder + 1,
            HumanVisualShape.Ellipse);
        if (!large)
            return;

        AddHousePart(
            go.transform,
            "Wart",
            new Vector3(fruitX + 0.016f * side, fruitY + 0.002f * s, 0f),
            new Vector2(0.004f * s, 0.004f * s),
            0f,
            wart,
            CucumberOrder + 2,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Gloss",
            new Vector3(fruitX + 0.004f * side, fruitY + 0.001f * s, 0f),
            new Vector2(0.007f * s, 0.003f * s),
            -8f * side,
            gloss,
            CucumberOrder + 2,
            HumanVisualShape.Ellipse);
    }

    private static void AddMelonHead(
        Transform parent,
        float x,
        float h,
        float tilt,
        float side,
        Color crown,
        Color vine,
        Color groove,
        Color leaf,
        Color skin,
        Color belly,
        Color net,
        Color gloss,
        Color bloom,
        bool large)
    {
        GameObject go = new GameObject("Head");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(x, 0f, 0f);
        if (tilt * tilt > 0.01f)
            go.transform.localRotation = Quaternion.Euler(0f, 0f, tilt);

        float s = large ? 1f : 0.78f;
        // Vine foot ON the slab — not a cucumber crown
        // under a long fruit, not a pumpkin gourd
        // sitting high, not a pepper upright stem.
        float crownW = 0.024f * s;
        float crownH = 0.012f * s;
        float crownY = crownH * 0.52f;
        AddHousePart(
            go.transform,
            "Crown",
            new Vector3(0f, crownY, 0f),
            new Vector2(crownW, crownH),
            0f,
            crown,
            MelonOrder,
            HumanVisualShape.Ellipse);

        // SPRAWLING vine — 8 mm cane, not pumpkin
        // 10 mm / 88 mm dirt cane, not pepper upright
        // 12 mm, not a brown tomato stake. The
        // silhouette is a ROUND fruit on the vine,
        // not a LONG cucumber, not a RIBBED gourd.
        float vineW = (0.032f + h * 0.14f) * s;
        float vineH = 0.008f * s;
        float vineY = crownY + 0.010f * s;
        float vineX = 0.014f * side;
        AddHousePart(
            go.transform,
            "Vine",
            new Vector3(vineX, vineY, 0f),
            new Vector2(vineW, vineH),
            -16f * side,
            vine,
            MelonOrder,
            HumanVisualShape.Box);
        AddHousePart(
            go.transform,
            "Groove",
            new Vector3(vineX, vineY + 0.002f * s, 0f),
            new Vector2(vineW * 0.88f, 0.003f * s),
            -16f * side,
            groove,
            MelonOrder + 1,
            HumanVisualShape.Box);

        // Palmate kitchen leaf — broader than pepper's
        // modest pair, not pumpkin's three leaflets on
        // a dirt cane, not cucumber's long-fruit leaf.
        float leafY = vineY + 0.010f * s;
        AddHousePart(
            go.transform,
            "Leaf",
            new Vector3(-0.016f * side, leafY, 0f),
            new Vector2(0.036f * s, 0.018f * s),
            -22f * side,
            leaf,
            MelonOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Lobe",
            new Vector3(-0.026f * side, leafY + 0.004f * s, 0f),
            new Vector2(0.016f * s, 0.012f * s),
            -38f * side,
            leaf,
            MelonOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Tendril",
            new Vector3(vineX + 0.010f * side, vineY + 0.014f * s, 0f),
            new Vector2(0.004f * s, 0.016f * s),
            42f * side,
            vine,
            MelonOrder + 1,
            HumanVisualShape.Box);

        // ROUND netted fruit on the vine — 36×32 mm
        // ELLIPSE, not cucumber long 52×14 mm, not
        // pumpkin ribbed 108×70 mm, not pepper blocky
        // BOX 28×30 mm. Pedicel is a short nub, not a
        // pumpkin cork stem on a sitting gourd.
        float fruitW = 0.036f * s;
        float fruitH = 0.032f * s;
        float fruitX = vineX + 0.008f * side;
        float fruitY = vineY + 0.014f * s;
        AddHousePart(
            go.transform,
            "Pedicel",
            new Vector3(fruitX - 0.016f * side, fruitY - 0.008f * s, 0f),
            new Vector2(0.010f * s, 0.006f * s),
            -12f * side,
            vine,
            MelonOrder + 1,
            HumanVisualShape.Box);
        AddHousePart(
            go.transform,
            "Fruit",
            new Vector3(fruitX, fruitY, 0f),
            new Vector2(fruitW, fruitH),
            0f,
            skin,
            MelonOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Belly",
            new Vector3(fruitX, fruitY - 0.006f * s, 0f),
            new Vector2(fruitW * 0.72f, fruitH * 0.38f),
            0f,
            belly,
            MelonOrder + 2,
            HumanVisualShape.Ellipse);

        // Cork NET — crisscross raised ridges, not
        // pumpkin VERTICAL ribs (8 mm × body height),
        // not cucumber warts / stripes along X.
        AddHousePart(
            go.transform,
            "Net",
            new Vector3(fruitX, fruitY + 0.002f * s, 0f),
            new Vector2(fruitW * 0.62f, 0.003f * s),
            38f * side,
            net,
            MelonOrder + 2,
            HumanVisualShape.Box);
        AddHousePart(
            go.transform,
            "Net",
            new Vector3(fruitX, fruitY - 0.002f * s, 0f),
            new Vector2(fruitW * 0.58f, 0.003f * s),
            -42f * side,
            net,
            MelonOrder + 2,
            HumanVisualShape.Box);
        AddHousePart(
            go.transform,
            "Net",
            new Vector3(fruitX + 0.004f * side, fruitY, 0f),
            new Vector2(fruitW * 0.48f, 0.003f * s),
            72f * side,
            net,
            MelonOrder + 2,
            HumanVisualShape.Box);
        AddHousePart(
            go.transform,
            "Knot",
            new Vector3(fruitX - 0.008f * side, fruitY + 0.006f * s, 0f),
            new Vector2(0.005f * s, 0.005f * s),
            0f,
            net,
            MelonOrder + 2,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Knot",
            new Vector3(fruitX + 0.006f * side, fruitY - 0.004f * s, 0f),
            new Vector2(0.004f * s, 0.004f * s),
            0f,
            net,
            MelonOrder + 2,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Gloss",
            new Vector3(fruitX - 0.006f * side, fruitY + 0.006f * s, 0f),
            new Vector2(0.010f * s, 0.006f * s),
            0f,
            gloss,
            MelonOrder + 2,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Bloom",
            new Vector3(fruitX + fruitW * 0.38f * side, fruitY + 0.004f * s, 0f),
            new Vector2(0.007f * s, 0.006f * s),
            0f,
            bloom,
            MelonOrder + 1,
            HumanVisualShape.Ellipse);
        if (!large)
            return;

        AddHousePart(
            go.transform,
            "Knot",
            new Vector3(fruitX + 0.010f * side, fruitY + 0.002f * s, 0f),
            new Vector2(0.004f * s, 0.004f * s),
            0f,
            net,
            MelonOrder + 2,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Net",
            new Vector3(fruitX - 0.002f * side, fruitY + 0.004f * s, 0f),
            new Vector2(fruitW * 0.42f, 0.003f * s),
            -28f * side,
            net,
            MelonOrder + 2,
            HumanVisualShape.Box);
        AddHousePart(
            go.transform,
            "Gloss",
            new Vector3(fruitX + 0.002f * side, fruitY + 0.002f * s, 0f),
            new Vector2(0.007f * s, 0.004f * s),
            0f,
            gloss,
            MelonOrder + 2,
            HumanVisualShape.Ellipse);
    }

    private static void AddWatermelonHead(
        Transform parent,
        float x,
        float h,
        float tilt,
        float side,
        Color crown,
        Color vine,
        Color groove,
        Color leaf,
        Color skin,
        Color stripe,
        Color belly,
        Color gloss,
        bool large)
    {
        GameObject go = new GameObject("Head");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(x, 0f, 0f);
        if (tilt * tilt > 0.01f)
            go.transform.localRotation = Quaternion.Euler(0f, 0f, tilt);

        float s = large ? 1f : 0.78f;
        // Vine foot ON the slab — not a cucumber crown
        // under a long fruit, not a pumpkin gourd
        // sitting high, not a melon netted round.
        float crownW = 0.026f * s;
        float crownH = 0.012f * s;
        float crownY = crownH * 0.52f;
        AddHousePart(
            go.transform,
            "Crown",
            new Vector3(0f, crownY, 0f),
            new Vector2(crownW, crownH),
            0f,
            crown,
            WatermelonOrder,
            HumanVisualShape.Ellipse);

        // SPRAWLING vine — 8 mm cane, not pumpkin
        // 10 mm / 88 mm dirt cane, not a melon shorter
        // netted cane. The silhouette is an OVAL
        // fruit on the vine, not a ROUND netted melon,
        // not a LONG cucumber, not a RIBBED gourd.
        float vineW = (0.040f + h * 0.16f) * s;
        float vineH = 0.008f * s;
        float vineY = crownY + 0.010f * s;
        float vineX = 0.016f * side;
        AddHousePart(
            go.transform,
            "Vine",
            new Vector3(vineX, vineY, 0f),
            new Vector2(vineW, vineH),
            -16f * side,
            vine,
            WatermelonOrder,
            HumanVisualShape.Box);
        AddHousePart(
            go.transform,
            "Groove",
            new Vector3(vineX, vineY + 0.002f * s, 0f),
            new Vector2(vineW * 0.88f, 0.003f * s),
            -16f * side,
            groove,
            WatermelonOrder + 1,
            HumanVisualShape.Box);

        // Palmate kitchen leaf — broader than melon's
        // pair, not pumpkin's three leaflets on a dirt
        // cane, not cucumber's long-fruit leaf.
        float leafY = vineY + 0.012f * s;
        AddHousePart(
            go.transform,
            "Leaf",
            new Vector3(-0.018f * side, leafY, 0f),
            new Vector2(0.040f * s, 0.020f * s),
            -22f * side,
            leaf,
            WatermelonOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Lobe",
            new Vector3(-0.030f * side, leafY + 0.004f * s, 0f),
            new Vector2(0.018f * s, 0.012f * s),
            -38f * side,
            leaf,
            WatermelonOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Tendril",
            new Vector3(vineX + 0.012f * side, vineY + 0.016f * s, 0f),
            new Vector2(0.004f * s, 0.018f * s),
            42f * side,
            vine,
            WatermelonOrder + 1,
            HumanVisualShape.Box);

        // STRIPED OVAL fruit on the vine — 72×46 mm
        // ELLIPSE, not melon round 36×32 mm, not
        // cucumber long 52×14 mm, not pumpkin ribbed
        // 108×70 mm. Pedicel is a short nub, not a
        // pumpkin cork stem on a sitting gourd.
        float fruitW = 0.072f * s;
        float fruitH = 0.046f * s;
        float fruitX = vineX + 0.012f * side;
        float fruitY = vineY + 0.018f * s;
        AddHousePart(
            go.transform,
            "Pedicel",
            new Vector3(fruitX - 0.028f * side, fruitY - 0.010f * s, 0f),
            new Vector2(0.012f * s, 0.007f * s),
            -12f * side,
            vine,
            WatermelonOrder + 1,
            HumanVisualShape.Box);
        AddHousePart(
            go.transform,
            "Fruit",
            new Vector3(fruitX, fruitY, 0f),
            new Vector2(fruitW, fruitH),
            0f,
            skin,
            WatermelonOrder + 1,
            HumanVisualShape.Ellipse);

        // Pale LONGITUDINAL stripes — broad color
        // bands on green oval skin, not pumpkin
        // VERTICAL ribs (8 mm × body height raised
        // ellipses on an orange gourd), not melon cork
        // NET (thin diagonal boxes), not cucumber
        // warts / stripes along X.
        AddHousePart(
            go.transform,
            "Stripe",
            new Vector3(fruitX - 0.018f * s * side, fruitY, 0f),
            new Vector2(0.010f * s, fruitH * 0.78f),
            0f,
            stripe,
            WatermelonOrder + 2,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Stripe",
            new Vector3(fruitX, fruitY, 0f),
            new Vector2(0.012f * s, fruitH * 0.86f),
            0f,
            stripe,
            WatermelonOrder + 2,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Stripe",
            new Vector3(fruitX + 0.018f * s * side, fruitY, 0f),
            new Vector2(0.010f * s, fruitH * 0.74f),
            0f,
            stripe,
            WatermelonOrder + 2,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Belly",
            new Vector3(fruitX, fruitY - 0.012f * s, 0f),
            new Vector2(fruitW * 0.58f, fruitH * 0.28f),
            0f,
            belly,
            WatermelonOrder + 2,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Gloss",
            new Vector3(fruitX - 0.012f * side, fruitY + 0.010f * s, 0f),
            new Vector2(0.014f * s, 0.008f * s),
            0f,
            gloss,
            WatermelonOrder + 2,
            HumanVisualShape.Ellipse);
        if (!large)
            return;

        AddHousePart(
            go.transform,
            "Stripe",
            new Vector3(fruitX + 0.030f * s * side, fruitY, 0f),
            new Vector2(0.008f * s, fruitH * 0.62f),
            0f,
            stripe,
            WatermelonOrder + 2,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Gloss",
            new Vector3(fruitX + 0.006f * side, fruitY + 0.006f * s, 0f),
            new Vector2(0.008f * s, 0.005f * s),
            0f,
            gloss,
            WatermelonOrder + 2,
            HumanVisualShape.Ellipse);
    }

    private static void AddZucchiniHead(
        Transform parent,
        float x,
        float h,
        float tilt,
        float side,
        Color crown,
        Color vine,
        Color groove,
        Color leaf,
        Color skin,
        Color speckle,
        Color belly,
        Color gloss,
        bool large)
    {
        GameObject go = new GameObject("Head");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(x, 0f, 0f);
        if (tilt * tilt > 0.01f)
            go.transform.localRotation = Quaternion.Euler(0f, 0f, tilt);

        float s = large ? 1f : 0.78f;
        // Vine foot ON the slab — not a watermelon
        // striped oval sitting high, not a pumpkin
        // gourd on dirt, not a melon netted round.
        float crownW = 0.024f * s;
        float crownH = 0.012f * s;
        float crownY = crownH * 0.52f;
        AddHousePart(
            go.transform,
            "Crown",
            new Vector3(0f, crownY, 0f),
            new Vector2(crownW, crownH),
            0f,
            crown,
            ZucchiniOrder,
            HumanVisualShape.Ellipse);

        // SPRAWLING vine — 8 mm cane, not pumpkin
        // 10 mm / 88 mm dirt cane, not a watermelon
        // oval cane. The silhouette is a YELLOW
        // HOOK fruit on the vine, not a STRIPED
        // OVAL, not a ROUND netted melon, not a
        // RIBBED gourd.
        float vineW = (0.038f + h * 0.14f) * s;
        float vineH = 0.008f * s;
        float vineY = crownY + 0.010f * s;
        float vineX = 0.016f * side;
        AddHousePart(
            go.transform,
            "Vine",
            new Vector3(vineX, vineY, 0f),
            new Vector2(vineW, vineH),
            -16f * side,
            vine,
            ZucchiniOrder,
            HumanVisualShape.Box);
        AddHousePart(
            go.transform,
            "Groove",
            new Vector3(vineX, vineY + 0.002f * s, 0f),
            new Vector2(vineW * 0.88f, 0.003f * s),
            -16f * side,
            groove,
            ZucchiniOrder + 1,
            HumanVisualShape.Box);

        // Palmate kitchen leaf — smaller than
        // watermelon's pair, not pumpkin's three
        // leaflets on a dirt cane.
        float leafY = vineY + 0.012f * s;
        AddHousePart(
            go.transform,
            "Leaf",
            new Vector3(-0.016f * side, leafY, 0f),
            new Vector2(0.036f * s, 0.018f * s),
            -22f * side,
            leaf,
            ZucchiniOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Lobe",
            new Vector3(-0.028f * side, leafY + 0.004f * s, 0f),
            new Vector2(0.016f * s, 0.010f * s),
            -38f * side,
            leaf,
            ZucchiniOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Tendril",
            new Vector3(vineX + 0.012f * side, vineY + 0.016f * s, 0f),
            new Vector2(0.004f * s, 0.016f * s),
            42f * side,
            vine,
            ZucchiniOrder + 1,
            HumanVisualShape.Box);

        // YELLOW HOOK fruit on the vine — 42×16 mm
        // NECK ellipse at 38° plus a 22×18 mm BULB
        // at the blossom end, not watermelon oval
        // 72×46 mm, not melon round 36×32 mm, not
        // cucumber long 52×14 mm, not pumpkin ribbed
        // 108×70 mm. Pedicel is a short nub.
        float neckW = 0.042f * s;
        float neckH = 0.016f * s;
        float fruitX = vineX + 0.014f * side;
        float fruitY = vineY + 0.016f * s;
        float crook = -38f * side;
        AddHousePart(
            go.transform,
            "Pedicel",
            new Vector3(fruitX - 0.018f * side, fruitY - 0.006f * s, 0f),
            new Vector2(0.010f * s, 0.006f * s),
            -12f * side,
            vine,
            ZucchiniOrder + 1,
            HumanVisualShape.Box);
        AddHousePart(
            go.transform,
            "Neck",
            new Vector3(fruitX, fruitY, 0f),
            new Vector2(neckW, neckH),
            crook,
            skin,
            ZucchiniOrder + 1,
            HumanVisualShape.Ellipse);

        // Blossom-end bulb — the hook's head, not a
        // watermelon oval, not a pattypan disk.
        float bulbX = fruitX + 0.016f * side;
        float bulbY = fruitY + 0.010f * s;
        AddHousePart(
            go.transform,
            "Bulb",
            new Vector3(bulbX, bulbY, 0f),
            new Vector2(0.022f * s, 0.018f * s),
            crook * 0.35f,
            skin,
            ZucchiniOrder + 1,
            HumanVisualShape.Ellipse);

        // Speckles on yellow skin — small dots, not
        // watermelon LONGITUDINAL stripes, not
        // pumpkin VERTICAL ribs, not melon cork NET.
        AddHousePart(
            go.transform,
            "Speckle",
            new Vector3(bulbX - 0.004f * side, bulbY + 0.002f * s, 0f),
            new Vector2(0.005f * s, 0.004f * s),
            0f,
            speckle,
            ZucchiniOrder + 2,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Speckle",
            new Vector3(bulbX + 0.004f * side, bulbY - 0.003f * s, 0f),
            new Vector2(0.004f * s, 0.003f * s),
            0f,
            speckle,
            ZucchiniOrder + 2,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Belly",
            new Vector3(bulbX, bulbY - 0.005f * s, 0f),
            new Vector2(0.012f * s, 0.006f * s),
            0f,
            belly,
            ZucchiniOrder + 2,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Gloss",
            new Vector3(bulbX - 0.004f * side, bulbY + 0.004f * s, 0f),
            new Vector2(0.008f * s, 0.004f * s),
            0f,
            gloss,
            ZucchiniOrder + 2,
            HumanVisualShape.Ellipse);
        if (!large)
            return;

        AddHousePart(
            go.transform,
            "Speckle",
            new Vector3(fruitX, fruitY, 0f),
            new Vector2(0.005f * s, 0.003f * s),
            crook,
            speckle,
            ZucchiniOrder + 2,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Gloss",
            new Vector3(fruitX - 0.006f * side, fruitY + 0.003f * s, 0f),
            new Vector2(0.006f * s, 0.003f * s),
            0f,
            gloss,
            ZucchiniOrder + 2,
            HumanVisualShape.Ellipse);
    }

    private static void AddPattypanHead(
        Transform parent,
        float x,
        float h,
        float tilt,
        float side,
        Color crown,
        Color vine,
        Color groove,
        Color leaf,
        Color skin,
        Color speckle,
        Color belly,
        Color gloss,
        bool large)
    {
        GameObject go = new GameObject("Head");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(x, 0f, 0f);
        if (tilt * tilt > 0.01f)
            go.transform.localRotation = Quaternion.Euler(0f, 0f, tilt);

        float s = large ? 1f : 0.78f;
        // Vine foot ON the slab — not a zucchini
        // hook sitting high, not a pumpkin gourd on
        // dirt, not a watermelon oval.
        float crownW = 0.024f * s;
        float crownH = 0.012f * s;
        float crownY = crownH * 0.52f;
        AddHousePart(
            go.transform,
            "Crown",
            new Vector3(0f, crownY, 0f),
            new Vector2(crownW, crownH),
            0f,
            crown,
            PattypanOrder,
            HumanVisualShape.Ellipse);

        // SPRAWLING vine — 8 mm cane, not pumpkin
        // 10 mm / 88 mm dirt cane, not a zucchini
        // hook cane. The silhouette is a YELLOW
        // SCALLOPED DISK on the vine, not a YELLOW
        // HOOK, not a STRIPED OVAL, not a RIBBED
        // gourd.
        float vineW = (0.038f + h * 0.14f) * s;
        float vineH = 0.008f * s;
        float vineY = crownY + 0.010f * s;
        float vineX = 0.016f * side;
        AddHousePart(
            go.transform,
            "Vine",
            new Vector3(vineX, vineY, 0f),
            new Vector2(vineW, vineH),
            -16f * side,
            vine,
            PattypanOrder,
            HumanVisualShape.Box);
        AddHousePart(
            go.transform,
            "Groove",
            new Vector3(vineX, vineY + 0.002f * s, 0f),
            new Vector2(vineW * 0.88f, 0.003f * s),
            -16f * side,
            groove,
            PattypanOrder + 1,
            HumanVisualShape.Box);

        float leafY = vineY + 0.012f * s;
        AddHousePart(
            go.transform,
            "Leaf",
            new Vector3(-0.016f * side, leafY, 0f),
            new Vector2(0.036f * s, 0.018f * s),
            -22f * side,
            leaf,
            PattypanOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Lobe",
            new Vector3(-0.028f * side, leafY + 0.004f * s, 0f),
            new Vector2(0.016f * s, 0.010f * s),
            -38f * side,
            leaf,
            PattypanOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Tendril",
            new Vector3(vineX + 0.012f * side, vineY + 0.016f * s, 0f),
            new Vector2(0.004f * s, 0.016f * s),
            42f * side,
            vine,
            PattypanOrder + 1,
            HumanVisualShape.Box);

        // YELLOW SCALLOPED DISK fruit on the vine —
        // 40×20 mm flattened ellipse sitting level,
        // not zucchini hook neck 42×16 mm at 38°
        // plus a 22×18 mm bulb, not watermelon oval
        // 72×46 mm, not pumpkin ribbed 108×70 mm.
        // Pedicel is a short nub on top of the disk.
        float diskW = 0.040f * s;
        float diskH = 0.020f * s;
        float fruitX = vineX + 0.014f * side;
        float fruitY = vineY + 0.014f * s;
        AddHousePart(
            go.transform,
            "Pedicel",
            new Vector3(fruitX - 0.004f * side, fruitY + 0.010f * s, 0f),
            new Vector2(0.008f * s, 0.006f * s),
            -8f * side,
            vine,
            PattypanOrder + 1,
            HumanVisualShape.Box);
        AddHousePart(
            go.transform,
            "Disk",
            new Vector3(fruitX, fruitY, 0f),
            new Vector2(diskW, diskH),
            0f,
            skin,
            PattypanOrder + 1,
            HumanVisualShape.Ellipse);

        // Scalloped rim — six equatorial lobes, not
        // a zucchini crook, not pumpkin VERTICAL
        // ribs, not watermelon LONGITUDINAL stripes.
        float lobeW = 0.010f * s;
        float lobeH = 0.008f * s;
        float rimX = 0.018f * s;
        float rimY = 0.007f * s;
        AddHousePart(
            go.transform,
            "Scallop",
            new Vector3(fruitX + rimX * side, fruitY, 0f),
            new Vector2(lobeW, lobeH),
            0f,
            skin,
            PattypanOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Scallop",
            new Vector3(fruitX - rimX * side, fruitY, 0f),
            new Vector2(lobeW, lobeH),
            0f,
            skin,
            PattypanOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Scallop",
            new Vector3(fruitX + 0.010f * side, fruitY + rimY, 0f),
            new Vector2(lobeW, lobeH),
            18f * side,
            skin,
            PattypanOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Scallop",
            new Vector3(fruitX - 0.010f * side, fruitY + rimY, 0f),
            new Vector2(lobeW, lobeH),
            18f * side,
            skin,
            PattypanOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Scallop",
            new Vector3(fruitX + 0.010f * side, fruitY - rimY, 0f),
            new Vector2(lobeW, lobeH),
            -18f * side,
            skin,
            PattypanOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Scallop",
            new Vector3(fruitX - 0.010f * side, fruitY - rimY, 0f),
            new Vector2(lobeW, lobeH),
            -18f * side,
            skin,
            PattypanOrder + 1,
            HumanVisualShape.Ellipse);

        AddHousePart(
            go.transform,
            "Scar",
            new Vector3(fruitX, fruitY + 0.002f * s, 0f),
            new Vector2(0.008f * s, 0.006f * s),
            0f,
            speckle,
            PattypanOrder + 2,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Belly",
            new Vector3(fruitX, fruitY - 0.004f * s, 0f),
            new Vector2(0.018f * s, 0.008f * s),
            0f,
            belly,
            PattypanOrder + 2,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Gloss",
            new Vector3(fruitX - 0.006f * side, fruitY + 0.004f * s, 0f),
            new Vector2(0.010f * s, 0.005f * s),
            0f,
            gloss,
            PattypanOrder + 2,
            HumanVisualShape.Ellipse);
        if (!large)
            return;

        AddHousePart(
            go.transform,
            "Speckle",
            new Vector3(fruitX + 0.006f * side, fruitY - 0.002f * s, 0f),
            new Vector2(0.005f * s, 0.004f * s),
            0f,
            speckle,
            PattypanOrder + 2,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Gloss",
            new Vector3(fruitX + 0.004f * side, fruitY + 0.003f * s, 0f),
            new Vector2(0.006f * s, 0.003f * s),
            0f,
            gloss,
            PattypanOrder + 2,
            HumanVisualShape.Ellipse);
    }

    private static void AddButternutHead(
        Transform parent,
        float x,
        float h,
        float tilt,
        float side,
        Color crown,
        Color vine,
        Color groove,
        Color leaf,
        Color skin,
        Color speckle,
        Color belly,
        Color gloss,
        bool large)
    {
        GameObject go = new GameObject("Head");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(x, 0f, 0f);
        if (tilt * tilt > 0.01f)
            go.transform.localRotation = Quaternion.Euler(0f, 0f, tilt);

        float s = large ? 1f : 0.78f;
        // Vine foot ON the slab — not a pattypan
        // scalloped disk sitting high, not a
        // zucchini hook, not a pumpkin gourd on dirt.
        float crownW = 0.024f * s;
        float crownH = 0.012f * s;
        float crownY = crownH * 0.52f;
        AddHousePart(
            go.transform,
            "Crown",
            new Vector3(0f, crownY, 0f),
            new Vector2(crownW, crownH),
            0f,
            crown,
            ButternutOrder,
            HumanVisualShape.Ellipse);

        // SPRAWLING vine — 8 mm cane, not pumpkin
        // 10 mm / 88 mm dirt cane, not a pattypan
        // disk cane. The silhouette is a BEIGE
        // BOTTLE fruit on the vine, not a YELLOW
        // SCALLOPED DISK, not a YELLOW HOOK, not a
        // RIBBED gourd.
        float vineW = (0.038f + h * 0.14f) * s;
        float vineH = 0.008f * s;
        float vineY = crownY + 0.010f * s;
        float vineX = 0.016f * side;
        AddHousePart(
            go.transform,
            "Vine",
            new Vector3(vineX, vineY, 0f),
            new Vector2(vineW, vineH),
            -16f * side,
            vine,
            ButternutOrder,
            HumanVisualShape.Box);
        AddHousePart(
            go.transform,
            "Groove",
            new Vector3(vineX, vineY + 0.002f * s, 0f),
            new Vector2(vineW * 0.88f, 0.003f * s),
            -16f * side,
            groove,
            ButternutOrder + 1,
            HumanVisualShape.Box);

        float leafY = vineY + 0.012f * s;
        AddHousePart(
            go.transform,
            "Leaf",
            new Vector3(-0.016f * side, leafY, 0f),
            new Vector2(0.036f * s, 0.018f * s),
            -22f * side,
            leaf,
            ButternutOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Lobe",
            new Vector3(-0.028f * side, leafY + 0.004f * s, 0f),
            new Vector2(0.016f * s, 0.010f * s),
            -38f * side,
            leaf,
            ButternutOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Tendril",
            new Vector3(vineX + 0.012f * side, vineY + 0.016f * s, 0f),
            new Vector2(0.004f * s, 0.016f * s),
            42f * side,
            vine,
            ButternutOrder + 1,
            HumanVisualShape.Box);

        // BEIGE BOTTLE fruit on the vine — 48×16 mm
        // NECK ellipse at 8° plus a 30×26 mm BELLY
        // at the blossom end, not zucchini hook neck
        // 42×16 mm at 38° plus a 22×18 mm bulb, not
        // pattypan disk 40×20 mm plus six lobes, not
        // pumpkin ribbed 108×70 mm. Pedicel is a
        // short nub at the stem end of the bottle.
        float neckW = 0.048f * s;
        float neckH = 0.016f * s;
        float fruitX = vineX + 0.014f * side;
        float fruitY = vineY + 0.016f * s;
        float bottle = -8f * side;
        AddHousePart(
            go.transform,
            "Pedicel",
            new Vector3(fruitX - 0.020f * side, fruitY + 0.004f * s, 0f),
            new Vector2(0.010f * s, 0.006f * s),
            -8f * side,
            vine,
            ButternutOrder + 1,
            HumanVisualShape.Box);
        AddHousePart(
            go.transform,
            "Neck",
            new Vector3(fruitX, fruitY, 0f),
            new Vector2(neckW, neckH),
            bottle,
            skin,
            ButternutOrder + 1,
            HumanVisualShape.Ellipse);

        // Distal belly — the bottle's heavy end,
        // not a zucchini crook bulb, not a pattypan
        // disk, not pumpkin VERTICAL ribs.
        float bellyX = fruitX + 0.022f * side;
        float bellyY = fruitY - 0.004f * s;
        AddHousePart(
            go.transform,
            "Belly",
            new Vector3(bellyX, bellyY, 0f),
            new Vector2(0.030f * s, 0.026f * s),
            bottle * 0.25f,
            skin,
            ButternutOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Stripe",
            new Vector3(fruitX, fruitY + 0.002f * s, 0f),
            new Vector2(neckW * 0.72f, 0.004f * s),
            bottle,
            speckle,
            ButternutOrder + 2,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Scar",
            new Vector3(bellyX + 0.010f * side, bellyY, 0f),
            new Vector2(0.006f * s, 0.006f * s),
            0f,
            speckle,
            ButternutOrder + 2,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Blush",
            new Vector3(bellyX - 0.004f * side, bellyY - 0.004f * s, 0f),
            new Vector2(0.014f * s, 0.010f * s),
            0f,
            belly,
            ButternutOrder + 2,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Gloss",
            new Vector3(fruitX - 0.008f * side, fruitY + 0.004f * s, 0f),
            new Vector2(0.012f * s, 0.005f * s),
            bottle,
            gloss,
            ButternutOrder + 2,
            HumanVisualShape.Ellipse);
        if (!large)
            return;

        AddHousePart(
            go.transform,
            "Speckle",
            new Vector3(bellyX + 0.004f * side, bellyY + 0.004f * s, 0f),
            new Vector2(0.006f * s, 0.004f * s),
            0f,
            speckle,
            ButternutOrder + 2,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Gloss",
            new Vector3(bellyX - 0.006f * side, bellyY + 0.006f * s, 0f),
            new Vector2(0.008f * s, 0.004f * s),
            0f,
            gloss,
            ButternutOrder + 2,
            HumanVisualShape.Ellipse);
    }

    private static void AddAcornHead(
        Transform parent,
        float x,
        float h,
        float tilt,
        float side,
        Color crown,
        Color vine,
        Color groove,
        Color leaf,
        Color skin,
        Color rib,
        Color blush,
        Color gloss,
        bool large)
    {
        GameObject go = new GameObject("Head");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(x, 0f, 0f);
        if (tilt * tilt > 0.01f)
            go.transform.localRotation = Quaternion.Euler(0f, 0f, tilt);

        float s = large ? 1f : 0.78f;
        // Vine foot ON the slab — not a butternut
        // bottle sitting high, not a pattypan
        // scalloped disk, not a pumpkin gourd on dirt.
        float crownW = 0.024f * s;
        float crownH = 0.012f * s;
        float crownY = crownH * 0.52f;
        AddHousePart(
            go.transform,
            "Crown",
            new Vector3(0f, crownY, 0f),
            new Vector2(crownW, crownH),
            0f,
            crown,
            AcornOrder,
            HumanVisualShape.Ellipse);

        // SPRAWLING vine — 8 mm cane, not pumpkin
        // 10 mm / 88 mm dirt cane, not a butternut
        // bottle cane. The silhouette is a DARK-GREEN
        // RIBBED ACORN fruit on the vine, not a BEIGE
        // BOTTLE, not a YELLOW SCALLOPED DISK, not a
        // RIBBED orange gourd.
        float vineW = (0.038f + h * 0.14f) * s;
        float vineH = 0.008f * s;
        float vineY = crownY + 0.010f * s;
        float vineX = 0.016f * side;
        AddHousePart(
            go.transform,
            "Vine",
            new Vector3(vineX, vineY, 0f),
            new Vector2(vineW, vineH),
            -16f * side,
            vine,
            AcornOrder,
            HumanVisualShape.Box);
        AddHousePart(
            go.transform,
            "Groove",
            new Vector3(vineX, vineY + 0.002f * s, 0f),
            new Vector2(vineW * 0.88f, 0.003f * s),
            -16f * side,
            groove,
            AcornOrder + 1,
            HumanVisualShape.Box);

        float leafY = vineY + 0.012f * s;
        AddHousePart(
            go.transform,
            "Leaf",
            new Vector3(-0.016f * side, leafY, 0f),
            new Vector2(0.036f * s, 0.018f * s),
            -22f * side,
            leaf,
            AcornOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Lobe",
            new Vector3(-0.028f * side, leafY + 0.004f * s, 0f),
            new Vector2(0.016f * s, 0.010f * s),
            -38f * side,
            leaf,
            AcornOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Tendril",
            new Vector3(vineX + 0.012f * side, vineY + 0.016f * s, 0f),
            new Vector2(0.004f * s, 0.016f * s),
            42f * side,
            vine,
            AcornOrder + 1,
            HumanVisualShape.Box);

        // DARK-GREEN RIBBED ACORN fruit on the vine —
        // 42×36 mm HEART ellipse plus a 16×14 mm
        // blossom POINT, not butternut neck 48×16 mm
        // at 8° plus a 30×26 mm belly, not pattypan
        // disk 40×20 mm plus six lobes, not pumpkin
        // ribbed 108×70 mm. Pedicel is a short woody
        // nub at the wide stem end of the heart.
        float bodyW = 0.042f * s;
        float bodyH = 0.036f * s;
        float fruitX = vineX + 0.014f * side;
        float fruitY = vineY + 0.022f * s;
        AddHousePart(
            go.transform,
            "Pedicel",
            new Vector3(fruitX - 0.004f * side, fruitY + bodyH * 0.52f, 0f),
            new Vector2(0.008f * s, 0.010f * s),
            12f * side,
            vine,
            AcornOrder + 1,
            HumanVisualShape.Box);
        AddHousePart(
            go.transform,
            "Heart",
            new Vector3(fruitX, fruitY, 0f),
            new Vector2(bodyW, bodyH),
            0f,
            skin,
            AcornOrder + 1,
            HumanVisualShape.Ellipse);

        // Blossom point — the acorn tip, not a
        // butternut bottle belly, not a pattypan
        // disk, not pumpkin VERTICAL round body.
        float pointX = fruitX + 0.002f * side;
        float pointY = fruitY - bodyH * 0.42f;
        AddHousePart(
            go.transform,
            "Point",
            new Vector3(pointX, pointY, 0f),
            new Vector2(0.016f * s, 0.014f * s),
            0f,
            skin,
            AcornOrder + 1,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Rib",
            new Vector3(fruitX - 0.012f * s * side, fruitY, 0f),
            new Vector2(0.005f * s, bodyH * 0.78f),
            -10f * side,
            rib,
            AcornOrder + 2,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Rib",
            new Vector3(fruitX, fruitY, 0f),
            new Vector2(0.005f * s, bodyH * 0.88f),
            0f,
            rib,
            AcornOrder + 2,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Rib",
            new Vector3(fruitX + 0.012f * s * side, fruitY, 0f),
            new Vector2(0.005f * s, bodyH * 0.78f),
            10f * side,
            rib,
            AcornOrder + 2,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Rib",
            new Vector3(fruitX + 0.006f * s * side, fruitY - 0.004f * s, 0f),
            new Vector2(0.004f * s, bodyH * 0.62f),
            4f * side,
            rib,
            AcornOrder + 2,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Blush",
            new Vector3(pointX, pointY - 0.002f * s, 0f),
            new Vector2(0.010f * s, 0.008f * s),
            0f,
            blush,
            AcornOrder + 2,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Gloss",
            new Vector3(fruitX - 0.008f * side, fruitY + 0.008f * s, 0f),
            new Vector2(0.012f * s, 0.006f * s),
            0f,
            gloss,
            AcornOrder + 2,
            HumanVisualShape.Ellipse);
        if (!large)
            return;

        AddHousePart(
            go.transform,
            "Speckle",
            new Vector3(fruitX + 0.008f * side, fruitY + 0.004f * s, 0f),
            new Vector2(0.006f * s, 0.004f * s),
            0f,
            rib,
            AcornOrder + 2,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Gloss",
            new Vector3(fruitX - 0.006f * side, fruitY + 0.012f * s, 0f),
            new Vector2(0.008f * s, 0.004f * s),
            0f,
            gloss,
            AcornOrder + 2,
            HumanVisualShape.Ellipse);
    }

    private static void AddSpaghettiHead(
        Transform parent,
        float x,
        float h,
        float tilt,
        float side,
        Color crown,
        Color vine,
        Color groove,
        Color leaf,
        Color skin,
        Color speckle,
        Color belly,
        Color gloss,
        bool large)
    {
        GameObject go = new GameObject("Head");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(x, 0f, 0f);
        if (tilt * tilt > 0.01f)
            go.transform.localRotation = Quaternion.Euler(0f, 0f, tilt);

        float s = large ? 1f : 0.78f;
        // Vine foot ON the slab — not an acorn heart
        // sitting high, not a butternut bottle, not a
        // pumpkin gourd on dirt.
        float crownW = 0.024f * s;
        float crownH = 0.012f * s;
        float crownY = crownH * 0.52f;
        AddHousePart(
            go.transform,
            "Crown",
            new Vector3(0f, crownY, 0f),
            new Vector2(crownW, crownH),
            0f,
            crown,
            SpaghettiOrder,
            HumanVisualShape.Ellipse);

        // SPRAWLING vine — 8 mm cane, not pumpkin
        // 10 mm / 88 mm dirt cane, not an acorn
        // heart cane. The silhouette is a PALE-YELLOW
        // OBLONG fruit on the vine, not a DARK-GREEN
        // RIBBED ACORN, not a BEIGE BOTTLE, not a
        // RIBBED orange gourd.
        float vineW = (0.038f + h * 0.14f) * s;
        float vineH = 0.008f * s;
        float vineY = crownY + 0.010f * s;
        float vineX = 0.016f * side;
        AddHousePart(
            go.transform,
            "Vine",
            new Vector3(vineX, vineY, 0f),
            new Vector2(vineW, vineH),
            -16f * side,
            vine,
            SpaghettiOrder,
            HumanVisualShape.Box);
        AddHousePart(
            go.transform,
            "Groove",
            new Vector3(vineX, vineY + 0.002f * s, 0f),
            new Vector2(vineW * 0.88f, 0.003f * s),
            -16f * side,
            groove,
            SpaghettiOrder + 1,
            HumanVisualShape.Box);

        float leafY = vineY + 0.012f * s;
        AddHousePart(
            go.transform,
            "Leaf",
            new Vector3(-0.016f * side, leafY, 0f),
            new Vector2(0.036f * s, 0.018f * s),
            -22f * side,
            leaf,
            SpaghettiOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Lobe",
            new Vector3(-0.028f * side, leafY + 0.004f * s, 0f),
            new Vector2(0.016f * s, 0.010f * s),
            -38f * side,
            leaf,
            SpaghettiOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Tendril",
            new Vector3(vineX + 0.012f * side, vineY + 0.016f * s, 0f),
            new Vector2(0.004f * s, 0.016f * s),
            42f * side,
            vine,
            SpaghettiOrder + 1,
            HumanVisualShape.Box);

        // PALE-YELLOW OBLONG fruit on the vine —
        // 56×26 mm OVAL, not acorn heart 42×36 mm plus
        // a 16×14 mm point and four 5 mm ribs, not
        // butternut neck 48×16 mm at 8° plus a 30×26
        // mm belly, not pumpkin ribbed 108×70 mm.
        // Pedicel is a short woody nub at the stem
        // end. Hang is 14°, not a zucchini 38° crook.
        float bodyW = 0.056f * s;
        float bodyH = 0.026f * s;
        float fruitX = vineX + 0.018f * side;
        float fruitY = vineY + 0.018f * s;
        float hang = -14f * side;
        AddHousePart(
            go.transform,
            "Pedicel",
            new Vector3(fruitX - 0.022f * side, fruitY + 0.004f * s, 0f),
            new Vector2(0.010f * s, 0.006f * s),
            12f * side,
            vine,
            SpaghettiOrder + 1,
            HumanVisualShape.Box);
        AddHousePart(
            go.transform,
            "Oval",
            new Vector3(fruitX, fruitY, 0f),
            new Vector2(bodyW, bodyH),
            hang,
            skin,
            SpaghettiOrder + 1,
            HumanVisualShape.Ellipse);

        // Blossom scar — a small pale nipple at the
        // far end, not an acorn blossom POINT, not a
        // butternut bottle belly, not pumpkin VERTICAL
        // round body.
        float scarX = fruitX + 0.022f * side;
        float scarY = fruitY - 0.006f * s;
        AddHousePart(
            go.transform,
            "Scar",
            new Vector3(scarX, scarY, 0f),
            new Vector2(0.010f * s, 0.008f * s),
            hang,
            skin,
            SpaghettiOrder + 1,
            HumanVisualShape.Ellipse);

        // Faint cream streaks — not acorn DARK 5 mm
        // vertical ribs, not watermelon LONGITUDINAL
        // stripes, not pumpkin VERTICAL ribs.
        AddHousePart(
            go.transform,
            "Streak",
            new Vector3(fruitX, fruitY + 0.004f * s, 0f),
            new Vector2(bodyW * 0.72f, 0.003f * s),
            hang,
            speckle,
            SpaghettiOrder + 2,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Streak",
            new Vector3(fruitX, fruitY - 0.004f * s, 0f),
            new Vector2(bodyW * 0.62f, 0.003f * s),
            hang,
            speckle,
            SpaghettiOrder + 2,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Belly",
            new Vector3(fruitX, fruitY - 0.004f * s, 0f),
            new Vector2(0.022f * s, 0.010f * s),
            hang,
            belly,
            SpaghettiOrder + 2,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Gloss",
            new Vector3(fruitX - 0.010f * side, fruitY + 0.006f * s, 0f),
            new Vector2(0.014f * s, 0.006f * s),
            hang,
            gloss,
            SpaghettiOrder + 2,
            HumanVisualShape.Ellipse);
        if (!large)
            return;

        AddHousePart(
            go.transform,
            "Speckle",
            new Vector3(fruitX + 0.010f * side, fruitY + 0.002f * s, 0f),
            new Vector2(0.006f * s, 0.004f * s),
            hang,
            speckle,
            SpaghettiOrder + 2,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Gloss",
            new Vector3(fruitX - 0.008f * side, fruitY + 0.008f * s, 0f),
            new Vector2(0.010f * s, 0.004f * s),
            hang,
            gloss,
            SpaghettiOrder + 2,
            HumanVisualShape.Ellipse);
    }

    private static void AddDelicataHead(
        Transform parent,
        float x,
        float h,
        float tilt,
        float side,
        Color crown,
        Color vine,
        Color groove,
        Color leaf,
        Color skin,
        Color stripe,
        Color belly,
        Color gloss,
        bool large)
    {
        GameObject go = new GameObject("Head");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(x, 0f, 0f);
        if (tilt * tilt > 0.01f)
            go.transform.localRotation = Quaternion.Euler(0f, 0f, tilt);

        float s = large ? 1f : 0.78f;
        // Vine foot ON the slab — not a spaghetti
        // oval sitting high, not a butternut bottle,
        // not a pumpkin gourd on dirt.
        float crownW = 0.024f * s;
        float crownH = 0.012f * s;
        float crownY = crownH * 0.52f;
        AddHousePart(
            go.transform,
            "Crown",
            new Vector3(0f, crownY, 0f),
            new Vector2(crownW, crownH),
            0f,
            crown,
            DelicataOrder,
            HumanVisualShape.Ellipse);

        // SPRAWLING vine — 8 mm cane, not pumpkin
        // 10 mm / 88 mm dirt cane, not a spaghetti
        // oval cane. The silhouette is a CREAM
        // OBLONG with DARK-GREEN STRIPES on the
        // vine, not a PALE-YELLOW OBLONG, not a
        // DARK-GREEN RIBBED ACORN, not a BEIGE
        // BOTTLE, not a RIBBED orange gourd.
        float vineW = (0.038f + h * 0.14f) * s;
        float vineH = 0.008f * s;
        float vineY = crownY + 0.010f * s;
        float vineX = 0.016f * side;
        AddHousePart(
            go.transform,
            "Vine",
            new Vector3(vineX, vineY, 0f),
            new Vector2(vineW, vineH),
            -16f * side,
            vine,
            DelicataOrder,
            HumanVisualShape.Box);
        AddHousePart(
            go.transform,
            "Groove",
            new Vector3(vineX, vineY + 0.002f * s, 0f),
            new Vector2(vineW * 0.88f, 0.003f * s),
            -16f * side,
            groove,
            DelicataOrder + 1,
            HumanVisualShape.Box);

        float leafY = vineY + 0.012f * s;
        AddHousePart(
            go.transform,
            "Leaf",
            new Vector3(-0.016f * side, leafY, 0f),
            new Vector2(0.036f * s, 0.018f * s),
            -22f * side,
            leaf,
            DelicataOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Lobe",
            new Vector3(-0.028f * side, leafY + 0.004f * s, 0f),
            new Vector2(0.016f * s, 0.010f * s),
            -38f * side,
            leaf,
            DelicataOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Tendril",
            new Vector3(vineX + 0.012f * side, vineY + 0.016f * s, 0f),
            new Vector2(0.004f * s, 0.016f * s),
            42f * side,
            vine,
            DelicataOrder + 1,
            HumanVisualShape.Box);

        // CREAM OBLONG fruit on the vine — 50×28 mm
        // OVAL, not spaghetti oval 56×26 mm plus two
        // faint cream streaks, not acorn heart 42×36
        // mm plus a 16×14 mm point and four 5 mm ribs,
        // not butternut neck 48×16 mm at 8° plus a
        // 30×26 mm belly, not pumpkin ribbed 108×70 mm.
        // Pedicel is a short woody nub at the stem
        // end. Hang is 10°, not a spaghetti 14° oval
        // and not a zucchini 38° crook.
        float bodyW = 0.050f * s;
        float bodyH = 0.028f * s;
        float fruitX = vineX + 0.018f * side;
        float fruitY = vineY + 0.018f * s;
        float hang = -10f * side;
        AddHousePart(
            go.transform,
            "Pedicel",
            new Vector3(fruitX - 0.020f * side, fruitY + 0.004f * s, 0f),
            new Vector2(0.010f * s, 0.006f * s),
            12f * side,
            vine,
            DelicataOrder + 1,
            HumanVisualShape.Box);
        AddHousePart(
            go.transform,
            "Oval",
            new Vector3(fruitX, fruitY, 0f),
            new Vector2(bodyW, bodyH),
            hang,
            skin,
            DelicataOrder + 1,
            HumanVisualShape.Ellipse);

        // Blossom scar — a small cream nipple at the
        // far end, not a spaghetti pale scar, not an
        // acorn blossom POINT, not a butternut bottle
        // belly, not pumpkin VERTICAL round body.
        float scarX = fruitX + 0.020f * side;
        float scarY = fruitY - 0.005f * s;
        AddHousePart(
            go.transform,
            "Scar",
            new Vector3(scarX, scarY, 0f),
            new Vector2(0.008f * s, 0.007f * s),
            hang,
            skin,
            DelicataOrder + 1,
            HumanVisualShape.Ellipse);

        // DARK-GREEN LONGITUDINAL stripes — four 5 mm
        // bands, not spaghetti FAINT cream streaks,
        // not acorn DARK 5 mm vertical ribs, not
        // watermelon LONGITUDINAL dark-on-green,
        // not pumpkin VERTICAL ribs.
        AddHousePart(
            go.transform,
            "Stripe",
            new Vector3(fruitX, fruitY + 0.008f * s, 0f),
            new Vector2(bodyW * 0.78f, 0.005f * s),
            hang,
            stripe,
            DelicataOrder + 2,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Stripe",
            new Vector3(fruitX, fruitY + 0.002f * s, 0f),
            new Vector2(bodyW * 0.84f, 0.005f * s),
            hang,
            stripe,
            DelicataOrder + 2,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Stripe",
            new Vector3(fruitX, fruitY - 0.004f * s, 0f),
            new Vector2(bodyW * 0.76f, 0.005f * s),
            hang,
            stripe,
            DelicataOrder + 2,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Stripe",
            new Vector3(fruitX, fruitY - 0.009f * s, 0f),
            new Vector2(bodyW * 0.62f, 0.004f * s),
            hang,
            stripe,
            DelicataOrder + 2,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Belly",
            new Vector3(fruitX, fruitY - 0.001f * s, 0f),
            new Vector2(0.018f * s, 0.006f * s),
            hang,
            belly,
            DelicataOrder + 2,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Gloss",
            new Vector3(fruitX - 0.010f * side, fruitY + 0.006f * s, 0f),
            new Vector2(0.012f * s, 0.005f * s),
            hang,
            gloss,
            DelicataOrder + 2,
            HumanVisualShape.Ellipse);
        if (!large)
            return;

        AddHousePart(
            go.transform,
            "Stripe",
            new Vector3(fruitX + 0.008f * side, fruitY + 0.005f * s, 0f),
            new Vector2(0.010f * s, 0.004f * s),
            hang,
            stripe,
            DelicataOrder + 2,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Gloss",
            new Vector3(fruitX - 0.008f * side, fruitY + 0.008f * s, 0f),
            new Vector2(0.010f * s, 0.004f * s),
            hang,
            gloss,
            DelicataOrder + 2,
            HumanVisualShape.Ellipse);
    }

    private static void AddCarnivalHead(
        Transform parent,
        float x,
        float h,
        float tilt,
        float side,
        Color crown,
        Color vine,
        Color groove,
        Color leaf,
        Color skin,
        Color orange,
        Color green,
        Color belly,
        Color gloss,
        bool large)
    {
        GameObject go = new GameObject("Head");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(x, 0f, 0f);
        if (tilt * tilt > 0.01f)
            go.transform.localRotation = Quaternion.Euler(0f, 0f, tilt);

        float s = large ? 1f : 0.78f;
        // Vine foot ON the slab — not a delicata
        // oblong sitting high, not a spaghetti
        // oval, not a pumpkin gourd on dirt.
        float crownW = 0.024f * s;
        float crownH = 0.012f * s;
        float crownY = crownH * 0.52f;
        AddHousePart(
            go.transform,
            "Crown",
            new Vector3(0f, crownY, 0f),
            new Vector2(crownW, crownH),
            0f,
            crown,
            CarnivalOrder,
            HumanVisualShape.Ellipse);

        // SPRAWLING vine — 8 mm cane, not pumpkin
        // 10 mm / 88 mm dirt cane, not a delicata
        // oblong cane. The silhouette is a ROUND
        // CREAM globe with ORANGE-GREEN MOTTLE on
        // the vine, not a CREAM OBLONG with
        // DARK-GREEN STRIPES, not a PALE-YELLOW
        // OBLONG, not a DARK-GREEN RIBBED ACORN,
        // not a RIBBED orange gourd.
        float vineW = (0.038f + h * 0.14f) * s;
        float vineH = 0.008f * s;
        float vineY = crownY + 0.010f * s;
        float vineX = 0.016f * side;
        AddHousePart(
            go.transform,
            "Vine",
            new Vector3(vineX, vineY, 0f),
            new Vector2(vineW, vineH),
            -16f * side,
            vine,
            CarnivalOrder,
            HumanVisualShape.Box);
        AddHousePart(
            go.transform,
            "Groove",
            new Vector3(vineX, vineY + 0.002f * s, 0f),
            new Vector2(vineW * 0.88f, 0.003f * s),
            -16f * side,
            groove,
            CarnivalOrder + 1,
            HumanVisualShape.Box);

        float leafY = vineY + 0.012f * s;
        AddHousePart(
            go.transform,
            "Leaf",
            new Vector3(-0.016f * side, leafY, 0f),
            new Vector2(0.036f * s, 0.018f * s),
            -22f * side,
            leaf,
            CarnivalOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Lobe",
            new Vector3(-0.028f * side, leafY + 0.004f * s, 0f),
            new Vector2(0.016f * s, 0.010f * s),
            -38f * side,
            leaf,
            CarnivalOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Tendril",
            new Vector3(vineX + 0.012f * side, vineY + 0.016f * s, 0f),
            new Vector2(0.004f * s, 0.016f * s),
            42f * side,
            vine,
            CarnivalOrder + 1,
            HumanVisualShape.Box);

        // ROUND CREAM globe on the vine — 38×34 mm
        // FLATTENED ELLIPSE, not delicata oblong
        // 50×28 mm plus four 5 mm dark-green
        // stripes, not spaghetti oval 56×26 mm
        // plus two faint cream streaks, not acorn
        // heart 42×36 mm plus a 16×14 mm point
        // and four 5 mm ribs, not pumpkin ribbed
        // 108×70 mm. Pedicel is a short woody nub
        // at the stem end. Hang is 6°, not a
        // delicata 10° oblong and not a spaghetti
        // 14° oval.
        float bodyW = 0.038f * s;
        float bodyH = 0.034f * s;
        float fruitX = vineX + 0.018f * side;
        float fruitY = vineY + 0.018f * s;
        float hang = -6f * side;
        AddHousePart(
            go.transform,
            "Pedicel",
            new Vector3(fruitX - 0.016f * side, fruitY + 0.006f * s, 0f),
            new Vector2(0.010f * s, 0.006f * s),
            12f * side,
            vine,
            CarnivalOrder + 1,
            HumanVisualShape.Box);
        AddHousePart(
            go.transform,
            "Globe",
            new Vector3(fruitX, fruitY, 0f),
            new Vector2(bodyW, bodyH),
            hang,
            skin,
            CarnivalOrder + 1,
            HumanVisualShape.Ellipse);

        // Blossom scar — a small cream ring at
        // the far end, not a delicata cream
        // nipple, not a spaghetti pale scar, not
        // an acorn blossom POINT, not pumpkin
        // VERTICAL round body.
        float scarX = fruitX + 0.012f * side;
        float scarY = fruitY - 0.006f * s;
        AddHousePart(
            go.transform,
            "Scar",
            new Vector3(scarX, scarY, 0f),
            new Vector2(0.008f * s, 0.007f * s),
            hang,
            skin,
            CarnivalOrder + 1,
            HumanVisualShape.Ellipse);

        // ORANGE-GREEN MOTTLE — short blotch
        // ellipses, not delicata LONGITUDINAL
        // 5 mm dark-green stripes, not spaghetti
        // FAINT cream streaks, not acorn DARK
        // 5 mm vertical ribs, not pumpkin
        // VERTICAL ribs.
        AddHousePart(
            go.transform,
            "Blotch",
            new Vector3(fruitX - 0.006f * side, fruitY + 0.006f * s, 0f),
            new Vector2(0.014f * s, 0.010f * s),
            hang + 18f * side,
            orange,
            CarnivalOrder + 2,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Blotch",
            new Vector3(fruitX + 0.008f * side, fruitY + 0.002f * s, 0f),
            new Vector2(0.012f * s, 0.009f * s),
            hang - 22f * side,
            green,
            CarnivalOrder + 2,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Blotch",
            new Vector3(fruitX - 0.002f * side, fruitY - 0.008f * s, 0f),
            new Vector2(0.013f * s, 0.008f * s),
            hang + 8f * side,
            orange,
            CarnivalOrder + 2,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Blotch",
            new Vector3(fruitX + 0.006f * side, fruitY - 0.004f * s, 0f),
            new Vector2(0.010f * s, 0.008f * s),
            hang - 12f * side,
            green,
            CarnivalOrder + 2,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Belly",
            new Vector3(fruitX, fruitY - 0.002f * s, 0f),
            new Vector2(0.014f * s, 0.006f * s),
            hang,
            belly,
            CarnivalOrder + 2,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Gloss",
            new Vector3(fruitX - 0.008f * side, fruitY + 0.008f * s, 0f),
            new Vector2(0.010f * s, 0.005f * s),
            hang,
            gloss,
            CarnivalOrder + 2,
            HumanVisualShape.Ellipse);
        if (!large)
            return;

        AddHousePart(
            go.transform,
            "Blotch",
            new Vector3(fruitX + 0.004f * side, fruitY + 0.008f * s, 0f),
            new Vector2(0.008f * s, 0.006f * s),
            hang + 28f * side,
            orange,
            CarnivalOrder + 2,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Gloss",
            new Vector3(fruitX - 0.006f * side, fruitY + 0.010f * s, 0f),
            new Vector2(0.008f * s, 0.004f * s),
            hang,
            gloss,
            CarnivalOrder + 2,
            HumanVisualShape.Ellipse);
    }

    private static void AddSweetDumplingHead(
        Transform parent,
        float x,
        float h,
        float tilt,
        float side,
        Color crown,
        Color vine,
        Color groove,
        Color leaf,
        Color skin,
        Color rib,
        Color belly,
        Color gloss,
        bool large)
    {
        GameObject go = new GameObject("Head");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(x, 0f, 0f);
        if (tilt * tilt > 0.01f)
            go.transform.localRotation = Quaternion.Euler(0f, 0f, tilt);

        float s = large ? 1f : 0.78f;
        // Vine foot ON the slab — not a carnival
        // mottled globe sitting high, not a
        // delicata oblong, not a spaghetti oval,
        // not a pumpkin gourd on dirt.
        float crownW = 0.024f * s;
        float crownH = 0.012f * s;
        float crownY = crownH * 0.52f;
        AddHousePart(
            go.transform,
            "Crown",
            new Vector3(0f, crownY, 0f),
            new Vector2(crownW, crownH),
            0f,
            crown,
            SweetDumplingOrder,
            HumanVisualShape.Ellipse);

        // SPRAWLING vine — 8 mm cane, not pumpkin
        // 10 mm / 88 mm dirt cane, not a carnival
        // mottled cane. The silhouette is a SMALL
        // ROUND CREAM fruit with DARK-GREEN RIBS
        // on the vine, not a ROUND CREAM globe
        // with ORANGE-GREEN MOTTLE, not a CREAM
        // OBLONG with DARK-GREEN STRIPES, not a
        // PALE-YELLOW OBLONG, not a DARK-GREEN
        // RIBBED ACORN, not a RIBBED orange gourd.
        float vineW = (0.038f + h * 0.14f) * s;
        float vineH = 0.008f * s;
        float vineY = crownY + 0.010f * s;
        float vineX = 0.016f * side;
        AddHousePart(
            go.transform,
            "Vine",
            new Vector3(vineX, vineY, 0f),
            new Vector2(vineW, vineH),
            -16f * side,
            vine,
            SweetDumplingOrder,
            HumanVisualShape.Box);
        AddHousePart(
            go.transform,
            "Groove",
            new Vector3(vineX, vineY + 0.002f * s, 0f),
            new Vector2(vineW * 0.88f, 0.003f * s),
            -16f * side,
            groove,
            SweetDumplingOrder + 1,
            HumanVisualShape.Box);

        float leafY = vineY + 0.012f * s;
        AddHousePart(
            go.transform,
            "Leaf",
            new Vector3(-0.016f * side, leafY, 0f),
            new Vector2(0.036f * s, 0.018f * s),
            -22f * side,
            leaf,
            SweetDumplingOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Lobe",
            new Vector3(-0.028f * side, leafY + 0.004f * s, 0f),
            new Vector2(0.016f * s, 0.010f * s),
            -38f * side,
            leaf,
            SweetDumplingOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Tendril",
            new Vector3(vineX + 0.012f * side, vineY + 0.016f * s, 0f),
            new Vector2(0.004f * s, 0.016f * s),
            42f * side,
            vine,
            SweetDumplingOrder + 1,
            HumanVisualShape.Box);

        // SMALL ROUND CREAM globe on the vine —
        // 28×26 mm ELLIPSE, not carnival flattened
        // 38×34 mm plus orange-green blotches
        // 10–14 mm, not delicata oblong 50×28 mm
        // plus four 5 mm dark-green stripes, not
        // spaghetti oval 56×26 mm plus two faint
        // cream streaks, not acorn heart 42×36 mm
        // plus a 16×14 mm point and four 5 mm
        // ribs, not pumpkin ribbed 108×70 mm.
        // Pedicel is a short woody nub at the
        // stem end. Hang is 4°, not a carnival
        // 6° globe and not a delicata 10° oblong.
        float bodyW = 0.028f * s;
        float bodyH = 0.026f * s;
        float fruitX = vineX + 0.016f * side;
        float fruitY = vineY + 0.016f * s;
        float hang = -4f * side;
        AddHousePart(
            go.transform,
            "Pedicel",
            new Vector3(fruitX - 0.012f * side, fruitY + 0.006f * s, 0f),
            new Vector2(0.008f * s, 0.006f * s),
            12f * side,
            vine,
            SweetDumplingOrder + 1,
            HumanVisualShape.Box);
        AddHousePart(
            go.transform,
            "Globe",
            new Vector3(fruitX, fruitY, 0f),
            new Vector2(bodyW, bodyH),
            hang,
            skin,
            SweetDumplingOrder + 1,
            HumanVisualShape.Ellipse);

        // Blossom scar — a small cream ring at
        // the far end, not a carnival cream
        // scar, not a delicata cream nipple,
        // not a spaghetti pale scar, not an
        // acorn blossom POINT, not pumpkin
        // VERTICAL round body.
        float scarX = fruitX + 0.010f * side;
        float scarY = fruitY - 0.005f * s;
        AddHousePart(
            go.transform,
            "Scar",
            new Vector3(scarX, scarY, 0f),
            new Vector2(0.006f * s, 0.005f * s),
            hang,
            skin,
            SweetDumplingOrder + 1,
            HumanVisualShape.Ellipse);

        // DARK-GREEN VERTICAL ribs — four 3 mm
        // bands, not carnival ORANGE-GREEN
        // blotch ellipses 10–14 mm, not delicata
        // LONGITUDINAL 5 mm dark-green stripes,
        // not spaghetti FAINT cream streaks,
        // not acorn DARK 5 mm vertical ribs on
        // a 42×36 mm heart, not pumpkin VERTICAL
        // ribs.
        AddHousePart(
            go.transform,
            "Rib",
            new Vector3(fruitX - 0.008f * side, fruitY, 0f),
            new Vector2(0.003f * s, bodyH * 0.78f),
            hang - 8f * side,
            rib,
            SweetDumplingOrder + 2,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Rib",
            new Vector3(fruitX, fruitY, 0f),
            new Vector2(0.003f * s, bodyH * 0.88f),
            hang,
            rib,
            SweetDumplingOrder + 2,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Rib",
            new Vector3(fruitX + 0.008f * side, fruitY, 0f),
            new Vector2(0.003f * s, bodyH * 0.78f),
            hang + 8f * side,
            rib,
            SweetDumplingOrder + 2,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Rib",
            new Vector3(fruitX + 0.004f * side, fruitY - 0.002f * s, 0f),
            new Vector2(0.003f * s, bodyH * 0.62f),
            hang + 4f * side,
            rib,
            SweetDumplingOrder + 2,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Belly",
            new Vector3(fruitX, fruitY - 0.002f * s, 0f),
            new Vector2(0.010f * s, 0.005f * s),
            hang,
            belly,
            SweetDumplingOrder + 2,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Gloss",
            new Vector3(fruitX - 0.006f * side, fruitY + 0.006f * s, 0f),
            new Vector2(0.008f * s, 0.004f * s),
            hang,
            gloss,
            SweetDumplingOrder + 2,
            HumanVisualShape.Ellipse);
        if (!large)
            return;

        AddHousePart(
            go.transform,
            "Rib",
            new Vector3(fruitX - 0.004f * side, fruitY + 0.002f * s, 0f),
            new Vector2(0.003f * s, bodyH * 0.70f),
            hang - 4f * side,
            rib,
            SweetDumplingOrder + 2,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Gloss",
            new Vector3(fruitX - 0.004f * side, fruitY + 0.008f * s, 0f),
            new Vector2(0.006f * s, 0.003f * s),
            hang,
            gloss,
            SweetDumplingOrder + 2,
            HumanVisualShape.Ellipse);
    }

    private static void AddKabochaHead(
        Transform parent,
        float x,
        float h,
        float tilt,
        float side,
        Color crown,
        Color vine,
        Color groove,
        Color leaf,
        Color skin,
        Color speckle,
        Color belly,
        Color gloss,
        bool large)
    {
        GameObject go = new GameObject("Head");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(x, 0f, 0f);
        if (tilt * tilt > 0.01f)
            go.transform.localRotation = Quaternion.Euler(0f, 0f, tilt);

        float s = large ? 1f : 0.78f;
        // Vine foot ON the slab — not a sweet
        // dumpling ribbed cream globe sitting high,
        // not a carnival mottled globe, not a
        // pumpkin gourd on dirt.
        float crownW = 0.024f * s;
        float crownH = 0.012f * s;
        float crownY = crownH * 0.52f;
        AddHousePart(
            go.transform,
            "Crown",
            new Vector3(0f, crownY, 0f),
            new Vector2(crownW, crownH),
            0f,
            crown,
            KabochaOrder,
            HumanVisualShape.Ellipse);

        // SPRAWLING vine — 8 mm cane, not pumpkin
        // 10 mm / 88 mm dirt cane, not a dumpling
        // cream cane. The silhouette is a SQUAT
        // DARK-GREEN globe with PALE SPECKLES on
        // the vine, not a SMALL ROUND CREAM fruit
        // with DARK-GREEN RIBS, not a ROUND CREAM
        // globe with ORANGE-GREEN MOTTLE, not a
        // CREAM OBLONG with DARK-GREEN STRIPES,
        // not a RIBBED orange gourd.
        float vineW = (0.038f + h * 0.14f) * s;
        float vineH = 0.008f * s;
        float vineY = crownY + 0.010f * s;
        float vineX = 0.016f * side;
        AddHousePart(
            go.transform,
            "Vine",
            new Vector3(vineX, vineY, 0f),
            new Vector2(vineW, vineH),
            -16f * side,
            vine,
            KabochaOrder,
            HumanVisualShape.Box);
        AddHousePart(
            go.transform,
            "Groove",
            new Vector3(vineX, vineY + 0.002f * s, 0f),
            new Vector2(vineW * 0.88f, 0.003f * s),
            -16f * side,
            groove,
            KabochaOrder + 1,
            HumanVisualShape.Box);

        float leafY = vineY + 0.012f * s;
        AddHousePart(
            go.transform,
            "Leaf",
            new Vector3(-0.016f * side, leafY, 0f),
            new Vector2(0.036f * s, 0.018f * s),
            -22f * side,
            leaf,
            KabochaOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Lobe",
            new Vector3(-0.028f * side, leafY + 0.004f * s, 0f),
            new Vector2(0.016f * s, 0.010f * s),
            -38f * side,
            leaf,
            KabochaOrder,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Tendril",
            new Vector3(vineX + 0.012f * side, vineY + 0.016f * s, 0f),
            new Vector2(0.004f * s, 0.016f * s),
            42f * side,
            vine,
            KabochaOrder + 1,
            HumanVisualShape.Box);

        // SQUAT DARK-GREEN globe on the vine —
        // 42×26 mm FLATTENED ELLIPSE, not sweet
        // dumpling 28×26 mm plus four 3 mm
        // dark-green ribs, not carnival flattened
        // 38×34 mm plus orange-green blotches
        // 10–14 mm, not delicata oblong 50×28 mm
        // plus four 5 mm dark-green stripes, not
        // spaghetti oval 56×26 mm plus two faint
        // cream streaks, not pumpkin ribbed
        // 108×70 mm. Pedicel is a short woody nub
        // at the stem end. Hang is 5°, not a
        // dumpling 4° globe and not a carnival
        // 6° globe.
        float bodyW = 0.042f * s;
        float bodyH = 0.026f * s;
        float fruitX = vineX + 0.018f * side;
        float fruitY = vineY + 0.016f * s;
        float hang = -5f * side;
        AddHousePart(
            go.transform,
            "Pedicel",
            new Vector3(fruitX - 0.016f * side, fruitY + 0.006f * s, 0f),
            new Vector2(0.009f * s, 0.006f * s),
            12f * side,
            vine,
            KabochaOrder + 1,
            HumanVisualShape.Box);
        AddHousePart(
            go.transform,
            "Globe",
            new Vector3(fruitX, fruitY, 0f),
            new Vector2(bodyW, bodyH),
            hang,
            skin,
            KabochaOrder + 1,
            HumanVisualShape.Ellipse);

        // Blossom scar — a small dark ring at
        // the far end, not a dumpling cream scar,
        // not a carnival cream scar, not a
        // delicata cream nipple, not pumpkin
        // VERTICAL round body.
        float scarX = fruitX + 0.014f * side;
        float scarY = fruitY - 0.004f * s;
        AddHousePart(
            go.transform,
            "Scar",
            new Vector3(scarX, scarY, 0f),
            new Vector2(0.007f * s, 0.006f * s),
            hang,
            skin,
            KabochaOrder + 1,
            HumanVisualShape.Ellipse);

        // PALE SPECKLES — four 6–8 mm dusty
        // ellipses, not dumpling DARK-GREEN
        // VERTICAL 3 mm ribs, not carnival
        // ORANGE-GREEN blotch ellipses 10–14 mm,
        // not delicata LONGITUDINAL 5 mm
        // dark-green stripes, not pumpkin VERTICAL
        // ribs.
        AddHousePart(
            go.transform,
            "Speckle",
            new Vector3(fruitX - 0.010f * side, fruitY + 0.004f * s, 0f),
            new Vector2(0.008f * s, 0.006f * s),
            hang + 16f * side,
            speckle,
            KabochaOrder + 2,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Speckle",
            new Vector3(fruitX + 0.008f * side, fruitY + 0.002f * s, 0f),
            new Vector2(0.007f * s, 0.005f * s),
            hang - 20f * side,
            speckle,
            KabochaOrder + 2,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Speckle",
            new Vector3(fruitX + 0.002f * side, fruitY - 0.004f * s, 0f),
            new Vector2(0.006f * s, 0.006f * s),
            hang + 8f * side,
            speckle,
            KabochaOrder + 2,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Speckle",
            new Vector3(fruitX - 0.004f * side, fruitY - 0.002f * s, 0f),
            new Vector2(0.007f * s, 0.005f * s),
            hang - 12f * side,
            speckle,
            KabochaOrder + 2,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Belly",
            new Vector3(fruitX, fruitY - 0.002f * s, 0f),
            new Vector2(0.014f * s, 0.006f * s),
            hang,
            belly,
            KabochaOrder + 2,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Gloss",
            new Vector3(fruitX - 0.008f * side, fruitY + 0.006f * s, 0f),
            new Vector2(0.010f * s, 0.004f * s),
            hang,
            gloss,
            KabochaOrder + 2,
            HumanVisualShape.Ellipse);
        if (!large)
            return;

        AddHousePart(
            go.transform,
            "Speckle",
            new Vector3(fruitX + 0.010f * side, fruitY - 0.002f * s, 0f),
            new Vector2(0.006f * s, 0.004f * s),
            hang + 24f * side,
            speckle,
            KabochaOrder + 2,
            HumanVisualShape.Ellipse);
        AddHousePart(
            go.transform,
            "Gloss",
            new Vector3(fruitX - 0.004f * side, fruitY + 0.008f * s, 0f),
            new Vector2(0.007f * s, 0.003f * s),
            hang,
            gloss,
            KabochaOrder + 2,
            HumanVisualShape.Ellipse);
    }

    private static void AddFernPinna(
        Transform parent,
        float x,
        float y,
        float tilt,
        float span,
        Color tint)
    {
        AddHousePart(
            parent,
            "Pinna",
            new Vector3(x, y, 0f),
            new Vector2(span, span * 0.42f),
            tilt,
            tint,
            FernOrder + 1,
            HumanVisualShape.Ellipse);
    }

    private static void AddLeaf(Transform sapling, Vector3 local, Vector2 size, float tilt, int seed)
    {
        GameObject leaf = new GameObject("Leaf");
        leaf.transform.SetParent(sapling, false);
        leaf.transform.localPosition = local;
        leaf.transform.localRotation = Quaternion.Euler(0f, 0f, tilt);
        SpriteRenderer sr = leaf.AddComponent<SpriteRenderer>();
        Color tint = BladeTint(Hash01(seed, 9));
        HumanSegment.ApplyPicture(sr, size, HumanVisualShape.Box, null, tint, GrassOrder + 1);
    }

    private Transform MakeBug(string name, Vector2 size, Vector3 pos, Color color, int order, bool world)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);
        if (world)
            go.transform.position = pos;
        else
            go.transform.localPosition = pos;
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(sr, size, HumanVisualShape.Box, null, color, order);
        return go.transform;
    }

    // Above the night veil so a blink reads as a light, not a dim speck in the dark.
    private Transform MakeFirefly(Vector2 size, Vector3 pos)
    {
        GameObject go = new GameObject("YardFirefly");
        go.transform.SetParent(transform, false);
        go.transform.position = pos;
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            sr,
            size,
            HumanVisualShape.Ellipse,
            null,
            new Color(0.78f, 0.98f, 0.28f, 0f),
            FireflyOrder);
        SpriteLighting.ApplyUnlit(sr);
        sr.enabled = false;
        go.SetActive(false);
        return go.transform;
    }

    private Transform MakeButterfly(float x, float y, Color wingTint, out Transform left, out Transform right)
    {
        Transform body = MakeBug(
            "YardButterfly",
            new Vector2(0.040f, 0.028f),
            new Vector3(x, y, 0f),
            new Color(0.22f, 0.18f, 0.14f, 1f),
            BugOrder + 1,
            true);

        left = MakeWing(body, "WingL", new Vector3(-0.028f, 0.006f, 0f), 18f, wingTint);
        right = MakeWing(body, "WingR", new Vector3(0.028f, 0.006f, 0f), -18f, wingTint);
        return body;
    }

    private static Transform MakeWing(Transform body, string name, Vector3 local, float tilt, Color tint)
    {
        GameObject wing = new GameObject(name);
        wing.transform.SetParent(body, false);
        wing.transform.localPosition = local;
        wing.transform.localRotation = Quaternion.Euler(0f, 0f, tilt);
        SpriteRenderer sr = wing.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(sr, new Vector2(0.055f, 0.032f), HumanVisualShape.Box, null, tint, BugOrder);
        return wing.transform;
    }

    private static Color BladeTint(float u)
    {
        return Color.Lerp(
            new Color(0.28f, 0.52f, 0.22f, 1f),
            new Color(0.38f, 0.62f, 0.28f, 1f),
            u);
    }

    private static Color ButterflyTint(float u)
    {
        if (u < 0.5f)
            return new Color(0.92f, 0.55f, 0.18f, 0.90f);
        return new Color(0.72f, 0.78f, 0.92f, 0.88f);
    }

    private static Color LeafTint(float u)
    {
        if (u < 0.34f)
            return new Color(0.34f, 0.52f, 0.22f, 0.92f);
        if (u < 0.67f)
            return new Color(0.72f, 0.55f, 0.18f, 0.90f);
        return new Color(0.58f, 0.28f, 0.12f, 0.90f);
    }

    private static Vector2 LaundrySize(int i)
    {
        if (i == 0)
            return new Vector2(0.16f, 0.09f);
        if (i == 1)
            return new Vector2(0.11f, 0.10f);
        if (i == 2)
            return new Vector2(0.09f, 0.07f);
        return new Vector2(0.13f, 0.08f);
    }

    private static Color LaundryTint(float u)
    {
        if (u < 0.25f)
            return new Color(0.92f, 0.90f, 0.84f, 1f);
        if (u < 0.50f)
            return new Color(0.42f, 0.58f, 0.72f, 1f);
        if (u < 0.75f)
            return new Color(0.86f, 0.62f, 0.48f, 1f);
        return new Color(0.78f, 0.28f, 0.30f, 1f);
    }

    private static Color CatTint(float u)
    {
        if (u < 0.34f)
            return new Color(0.72f, 0.42f, 0.18f, 1f);
        if (u < 0.67f)
            return new Color(0.22f, 0.20f, 0.18f, 1f);
        return new Color(0.86f, 0.82f, 0.74f, 1f);
    }

    private static Color FlowerTint(float u)
    {
        if (u < 0.25f)
            return new Color(0.86f, 0.28f, 0.32f, 1f);
        if (u < 0.50f)
            return new Color(0.92f, 0.72f, 0.22f, 1f);
        if (u < 0.75f)
            return new Color(0.62f, 0.38f, 0.78f, 1f);
        return new Color(0.94f, 0.55f, 0.62f, 1f);
    }

    private static bool NearFurniture(float x)
    {
        if (LevelTest1.NearDecor(x, 0.55f))
            return true;

        LevelTest1.FlowerPotPeg[] pots = LevelTest1.FlowerPotPegs;
        for (int i = 0; i < pots.Length; i++)
        {
            float d = x - pots[i].x;
            if (d * d < 0.1225f)
                return true;
        }

        LevelTest1.BirdhousePeg[] houses = LevelTest1.BirdhousePegs;
        for (int i = 0; i < houses.Length; i++)
        {
            float d = x - houses[i].x;
            if (d * d < 0.1225f)
                return true;
        }

        LevelTest1.BirdBathPeg[] baths = LevelTest1.BirdBathPegs;
        for (int i = 0; i < baths.Length; i++)
        {
            float d = x - baths[i].x;
            if (d * d < 0.1225f)
                return true;
        }

        LevelTest1.FrogPeg[] toads = LevelTest1.FrogPegs;
        for (int i = 0; i < toads.Length; i++)
        {
            float d = x - toads[i].x;
            if (d * d < 0.1225f)
                return true;
        }

        LevelTest1.LizardPeg[] sunners = LevelTest1.LizardPegs;
        for (int i = 0; i < sunners.Length; i++)
        {
            float d = x - sunners[i].x;
            if (d * d < 0.1225f)
                return true;
        }

        LevelTest1.GrasshopperPeg[] jumpers = LevelTest1.GrasshopperPegs;
        for (int i = 0; i < jumpers.Length; i++)
        {
            float d = x - jumpers[i].x;
            if (d * d < 0.1225f)
                return true;
        }

        LevelTest1.AntPeg[] trails = LevelTest1.AntPegs;
        for (int i = 0; i < trails.Length; i++)
        {
            float d = x - trails[i].x;
            if (d * d < 0.1225f)
                return true;
        }

        LevelTest1.CricketPeg[] singers = LevelTest1.CricketPegs;
        for (int i = 0; i < singers.Length; i++)
        {
            float d = x - singers[i].x;
            if (d * d < 0.1225f)
                return true;
        }

        LevelTest1.WormPeg[] crawlers = LevelTest1.WormPegs;
        for (int i = 0; i < crawlers.Length; i++)
        {
            float d = x - crawlers[i].x;
            if (d * d < 0.1225f)
                return true;
        }

        LevelTest1.StriderPeg[] skaters = LevelTest1.StriderPegs;
        for (int i = 0; i < skaters.Length; i++)
        {
            float d = x - skaters[i].x;
            if (d * d < 0.1225f)
                return true;
        }

        LevelTest1.BatPeg[] hunters = LevelTest1.BatPegs;
        for (int i = 0; i < hunters.Length; i++)
        {
            float d = x - hunters[i].x;
            if (d * d < 0.1225f)
                return true;
        }

        LevelTest1.MousePeg[] dart = LevelTest1.MousePegs;
        for (int i = 0; i < dart.Length; i++)
        {
            float d = x - dart[i].x;
            if (d * d < 0.1225f)
                return true;
        }

        LevelTest1.RabbitPeg[] grazers = LevelTest1.RabbitPegs;
        for (int i = 0; i < grazers.Length; i++)
        {
            float d = x - grazers[i].x;
            if (d * d < 0.1225f)
                return true;
        }

        LevelTest1.SquirrelPeg[] climbers = LevelTest1.SquirrelPegs;
        for (int i = 0; i < climbers.Length; i++)
        {
            float d = x - climbers[i].x;
            if (d * d < 0.1225f)
                return true;
        }

        LevelTest1.RaccoonPeg[] washers = LevelTest1.RaccoonPegs;
        for (int i = 0; i < washers.Length; i++)
        {
            float d = x - washers[i].x;
            if (d * d < 0.1225f)
                return true;
        }

        LevelTest1.TurtlePeg[] shells = LevelTest1.TurtlePegs;
        for (int i = 0; i < shells.Length; i++)
        {
            float d = x - shells[i].x;
            if (d * d < 0.1225f)
                return true;
        }

        LevelTest1.DuckPeg[] drakes = LevelTest1.DuckPegs;
        for (int i = 0; i < drakes.Length; i++)
        {
            float d = x - drakes[i].x;
            if (d * d < 0.1600f)
                return true;
        }

        LevelTest1.HeronPeg[] waders = LevelTest1.HeronPegs;
        for (int i = 0; i < waders.Length; i++)
        {
            float d = x - waders[i].x;
            if (d * d < 0.1600f)
                return true;
        }

        LevelTest1.MinnowPeg[] fry = LevelTest1.MinnowPegs;
        for (int i = 0; i < fry.Length; i++)
        {
            float d = x - fry[i].x;
            if (d * d < 0.1024f)
                return true;
        }

        LevelTest1.CattailPeg[] reeds = LevelTest1.CattailPegs;
        for (int i = 0; i < reeds.Length; i++)
        {
            float d = x - reeds[i].x;
            if (d * d < 0.1225f)
                return true;
        }

        LevelTest1.LilyPadPeg[] pads = LevelTest1.LilyPadPegs;
        for (int i = 0; i < pads.Length; i++)
        {
            float d = x - pads[i].x;
            if (d * d < 0.1225f)
                return true;
        }

        LevelTest1.DandelionPeg[] clocks = LevelTest1.DandelionPegs;
        for (int i = 0; i < clocks.Length; i++)
        {
            float d = x - clocks[i].x;
            if (d * d < 0.1225f)
                return true;
        }

        LevelTest1.MushroomPeg[] toadstools = LevelTest1.MushroomPegs;
        for (int i = 0; i < toadstools.Length; i++)
        {
            float d = x - toadstools[i].x;
            if (d * d < 0.1225f)
                return true;
        }

        LevelTest1.IvyPeg[] vines = LevelTest1.IvyPegs;
        for (int i = 0; i < vines.Length; i++)
        {
            float d = x - vines[i].x;
            if (d * d < 0.1225f)
                return true;
        }

        LevelTest1.FernPeg[] fronds = LevelTest1.FernPegs;
        for (int i = 0; i < fronds.Length; i++)
        {
            float d = x - fronds[i].x;
            if (d * d < 0.1225f)
                return true;
        }

        LevelTest1.CloverPeg[] trefoils = LevelTest1.CloverPegs;
        for (int i = 0; i < trefoils.Length; i++)
        {
            float d = x - trefoils[i].x;
            if (d * d < 0.1225f)
                return true;
        }

        LevelTest1.ThistlePeg[] spines = LevelTest1.ThistlePegs;
        for (int i = 0; i < spines.Length; i++)
        {
            float d = x - spines[i].x;
            if (d * d < 0.1225f)
                return true;
        }

        LevelTest1.PlantainPeg[] ribs = LevelTest1.PlantainPegs;
        for (int i = 0; i < ribs.Length; i++)
        {
            float d = x - ribs[i].x;
            if (d * d < 0.1225f)
                return true;
        }

        LevelTest1.NettlePeg[] stings = LevelTest1.NettlePegs;
        for (int i = 0; i < stings.Length; i++)
        {
            float d = x - stings[i].x;
            if (d * d < 0.1225f)
                return true;
        }

        LevelTest1.DockPeg[] panicles = LevelTest1.DockPegs;
        for (int i = 0; i < panicles.Length; i++)
        {
            float d = x - panicles[i].x;
            if (d * d < 0.1225f)
                return true;
        }

        LevelTest1.BramblePeg[] canes = LevelTest1.BramblePegs;
        for (int i = 0; i < canes.Length; i++)
        {
            float d = x - canes[i].x;
            if (d * d < 0.1225f)
                return true;
        }

        LevelTest1.HollyhockPeg[] spikes = LevelTest1.HollyhockPegs;
        for (int i = 0; i < spikes.Length; i++)
        {
            float d = x - spikes[i].x;
            if (d * d < 0.1225f)
                return true;
        }

        LevelTest1.RosePeg[] bushes = LevelTest1.RosePegs;
        for (int i = 0; i < bushes.Length; i++)
        {
            float d = x - bushes[i].x;
            if (d * d < 0.1225f)
                return true;
        }

        LevelTest1.LavenderPeg[] herbs = LevelTest1.LavenderPegs;
        for (int i = 0; i < herbs.Length; i++)
        {
            float d = x - herbs[i].x;
            if (d * d < 0.1225f)
                return true;
        }

        LevelTest1.IrisPeg[] fans = LevelTest1.IrisPegs;
        for (int i = 0; i < fans.Length; i++)
        {
            float d = x - fans[i].x;
            if (d * d < 0.1225f)
                return true;
        }

        LevelTest1.TulipPeg[] cups = LevelTest1.TulipPegs;
        for (int i = 0; i < cups.Length; i++)
        {
            float d = x - cups[i].x;
            if (d * d < 0.1225f)
                return true;
        }

        LevelTest1.DaffodilPeg[] trumpets = LevelTest1.DaffodilPegs;
        for (int i = 0; i < trumpets.Length; i++)
        {
            float d = x - trumpets[i].x;
            if (d * d < 0.1225f)
                return true;
        }

        LevelTest1.DaisyPeg[] disks = LevelTest1.DaisyPegs;
        for (int i = 0; i < disks.Length; i++)
        {
            float d = x - disks[i].x;
            if (d * d < 0.1225f)
                return true;
        }

        LevelTest1.PoppyPeg[] bowls = LevelTest1.PoppyPegs;
        for (int i = 0; i < bowls.Length; i++)
        {
            float d = x - bowls[i].x;
            if (d * d < 0.1225f)
                return true;
        }

        LevelTest1.LilyPeg[] stars = LevelTest1.LilyPegs;
        for (int i = 0; i < stars.Length; i++)
        {
            float d = x - stars[i].x;
            if (d * d < 0.1225f)
                return true;
        }

        LevelTest1.SunflowerPeg[] heads = LevelTest1.SunflowerPegs;
        for (int i = 0; i < heads.Length; i++)
        {
            float d = x - heads[i].x;
            if (d * d < 0.1225f)
                return true;
        }

        LevelTest1.VioletPeg[] faces = LevelTest1.VioletPegs;
        for (int i = 0; i < faces.Length; i++)
        {
            float d = x - faces[i].x;
            if (d * d < 0.1225f)
                return true;
        }

        LevelTest1.CrocusPeg[] croci = LevelTest1.CrocusPegs;
        for (int i = 0; i < croci.Length; i++)
        {
            float d = x - croci[i].x;
            if (d * d < 0.1225f)
                return true;
        }

        LevelTest1.BluebellPeg[] bells = LevelTest1.BluebellPegs;
        for (int i = 0; i < bells.Length; i++)
        {
            float d = x - bells[i].x;
            if (d * d < 0.1225f)
                return true;
        }

        LevelTest1.TomatoPeg[] tomatoVines = LevelTest1.TomatoPegs;
        for (int i = 0; i < tomatoVines.Length; i++)
        {
            float d = x - tomatoVines[i].x;
            if (d * d < 0.1225f)
                return true;
        }

        LevelTest1.CabbagePeg[] wraps = LevelTest1.CabbagePegs;
        for (int i = 0; i < wraps.Length; i++)
        {
            float d = x - wraps[i].x;
            if (d * d < 0.1225f)
                return true;
        }

        LevelTest1.PumpkinPeg[] gourds = LevelTest1.PumpkinPegs;
        for (int i = 0; i < gourds.Length; i++)
        {
            float d = x - gourds[i].x;
            if (d * d < 0.1225f)
                return true;
        }

        LevelTest1.CornPeg[] tassels = LevelTest1.CornPegs;
        for (int i = 0; i < tassels.Length; i++)
        {
            float d = x - tassels[i].x;
            if (d * d < 0.1225f)
                return true;
        }

        LevelTest1.CarrotPeg[] tapers = LevelTest1.CarrotPegs;
        for (int i = 0; i < tapers.Length; i++)
        {
            float d = x - tapers[i].x;
            if (d * d < 0.1225f)
                return true;
        }

        LevelTest1.LeekPeg[] spears = LevelTest1.LeekPegs;
        for (int i = 0; i < spears.Length; i++)
        {
            float d = x - spears[i].x;
            if (d * d < 0.1225f)
                return true;
        }

        LevelTest1.BeanPeg[] pods = LevelTest1.BeanPegs;
        for (int i = 0; i < pods.Length; i++)
        {
            float d = x - pods[i].x;
            if (d * d < 0.1225f)
                return true;
        }

        LevelTest1.OnionPeg[] globes = LevelTest1.OnionPegs;
        for (int i = 0; i < globes.Length; i++)
        {
            float d = x - globes[i].x;
            if (d * d < 0.1225f)
                return true;
        }

        LevelTest1.GarlicPeg[] cloves = LevelTest1.GarlicPegs;
        for (int i = 0; i < cloves.Length; i++)
        {
            float d = x - cloves[i].x;
            if (d * d < 0.1225f)
                return true;
        }

        LevelTest1.RadishPeg[] globules = LevelTest1.RadishPegs;
        for (int i = 0; i < globules.Length; i++)
        {
            float d = x - globules[i].x;
            if (d * d < 0.1225f)
                return true;
        }

        LevelTest1.PotatoPeg[] tubers = LevelTest1.PotatoPegs;
        for (int i = 0; i < tubers.Length; i++)
        {
            float d = x - tubers[i].x;
            if (d * d < 0.1225f)
                return true;
        }

        LevelTest1.BeetPeg[] crowns = LevelTest1.BeetPegs;
        for (int i = 0; i < crowns.Length; i++)
        {
            float d = x - crowns[i].x;
            if (d * d < 0.1225f)
                return true;
        }

        LevelTest1.ParsnipPeg[] creams = LevelTest1.ParsnipPegs;
        for (int i = 0; i < creams.Length; i++)
        {
            float d = x - creams[i].x;
            if (d * d < 0.1225f)
                return true;
        }

        LevelTest1.TurnipPeg[] caps = LevelTest1.TurnipPegs;
        for (int i = 0; i < caps.Length; i++)
        {
            float d = x - caps[i].x;
            if (d * d < 0.1225f)
                return true;
        }

        LevelTest1.RutabagaPeg[] swedes = LevelTest1.RutabagaPegs;
        for (int i = 0; i < swedes.Length; i++)
        {
            float d = x - swedes[i].x;
            if (d * d < 0.1225f)
                return true;
        }

        LevelTest1.KohlrabiPeg[] stems = LevelTest1.KohlrabiPegs;
        for (int i = 0; i < stems.Length; i++)
        {
            float d = x - stems[i].x;
            if (d * d < 0.1225f)
                return true;
        }

        LevelTest1.CeleryPeg[] celeryRibs = LevelTest1.CeleryPegs;
        for (int i = 0; i < celeryRibs.Length; i++)
        {
            float d = x - celeryRibs[i].x;
            if (d * d < 0.1225f)
                return true;
        }

        LevelTest1.FennelPeg[] bulbs = LevelTest1.FennelPegs;
        for (int i = 0; i < bulbs.Length; i++)
        {
            float d = x - bulbs[i].x;
            if (d * d < 0.1225f)
                return true;
        }

        LevelTest1.AsparagusPeg[] asparagusSpears = LevelTest1.AsparagusPegs;
        for (int i = 0; i < asparagusSpears.Length; i++)
        {
            float d = x - asparagusSpears[i].x;
            if (d * d < 0.1225f)
                return true;
        }

        LevelTest1.RhubarbPeg[] petioles = LevelTest1.RhubarbPegs;
        for (int i = 0; i < petioles.Length; i++)
        {
            float d = x - petioles[i].x;
            if (d * d < 0.1225f)
                return true;
        }

        LevelTest1.KalePeg[] rosettes = LevelTest1.KalePegs;
        for (int i = 0; i < rosettes.Length; i++)
        {
            float d = x - rosettes[i].x;
            if (d * d < 0.1225f)
                return true;
        }

        LevelTest1.ChardPeg[] paddles = LevelTest1.ChardPegs;
        for (int i = 0; i < paddles.Length; i++)
        {
            float d = x - paddles[i].x;
            if (d * d < 0.1225f)
                return true;
        }

        LevelTest1.SpinachPeg[] flats = LevelTest1.SpinachPegs;
        for (int i = 0; i < flats.Length; i++)
        {
            float d = x - flats[i].x;
            if (d * d < 0.1225f)
                return true;
        }

        LevelTest1.LettucePeg[] lettuceCups = LevelTest1.LettucePegs;
        for (int i = 0; i < lettuceCups.Length; i++)
        {
            float d = x - lettuceCups[i].x;
            if (d * d < 0.1225f)
                return true;
        }

        LevelTest1.BroccoliPeg[] florets = LevelTest1.BroccoliPegs;
        for (int i = 0; i < florets.Length; i++)
        {
            float d = x - florets[i].x;
            if (d * d < 0.1225f)
                return true;
        }

        LevelTest1.CauliflowerPeg[] curds = LevelTest1.CauliflowerPegs;
        for (int i = 0; i < curds.Length; i++)
        {
            float d = x - curds[i].x;
            if (d * d < 0.1225f)
                return true;
        }

        LevelTest1.BrusselsPeg[] sprouts = LevelTest1.BrusselsPegs;
        for (int i = 0; i < sprouts.Length; i++)
        {
            float d = x - sprouts[i].x;
            if (d * d < 0.1225f)
                return true;
        }

        LevelTest1.ArtichokePeg[] buds = LevelTest1.ArtichokePegs;
        for (int i = 0; i < buds.Length; i++)
        {
            float d = x - buds[i].x;
            if (d * d < 0.1225f)
                return true;
        }

        LevelTest1.EggplantPeg[] skins = LevelTest1.EggplantPegs;
        for (int i = 0; i < skins.Length; i++)
        {
            float d = x - skins[i].x;
            if (d * d < 0.1225f)
                return true;
        }

        LevelTest1.PepperPeg[] pepperPods = LevelTest1.PepperPegs;
        for (int i = 0; i < pepperPods.Length; i++)
        {
            float d = x - pepperPods[i].x;
            if (d * d < 0.1225f)
                return true;
        }

        LevelTest1.CucumberPeg[] cukes = LevelTest1.CucumberPegs;
        for (int i = 0; i < cukes.Length; i++)
        {
            float d = x - cukes[i].x;
            if (d * d < 0.1225f)
                return true;
        }

        LevelTest1.MelonPeg[] nets = LevelTest1.MelonPegs;
        for (int i = 0; i < nets.Length; i++)
        {
            float d = x - nets[i].x;
            if (d * d < 0.1225f)
                return true;
        }

        LevelTest1.WatermelonPeg[] ovals = LevelTest1.WatermelonPegs;
        for (int i = 0; i < ovals.Length; i++)
        {
            float d = x - ovals[i].x;
            if (d * d < 0.1225f)
                return true;
        }

        LevelTest1.ZucchiniPeg[] hooks = LevelTest1.ZucchiniPegs;
        for (int i = 0; i < hooks.Length; i++)
        {
            float d = x - hooks[i].x;
            if (d * d < 0.1225f)
                return true;
        }

        LevelTest1.PattypanPeg[] scallops = LevelTest1.PattypanPegs;
        for (int i = 0; i < scallops.Length; i++)
        {
            float d = x - scallops[i].x;
            if (d * d < 0.1225f)
                return true;
        }

        LevelTest1.ButternutPeg[] bottles = LevelTest1.ButternutPegs;
        for (int i = 0; i < bottles.Length; i++)
        {
            float d = x - bottles[i].x;
            if (d * d < 0.1225f)
                return true;
        }

        LevelTest1.AcornPeg[] acorns = LevelTest1.AcornPegs;
        for (int i = 0; i < acorns.Length; i++)
        {
            float d = x - acorns[i].x;
            if (d * d < 0.1225f)
                return true;
        }

        LevelTest1.SpaghettiPeg[] strands = LevelTest1.SpaghettiPegs;
        for (int i = 0; i < strands.Length; i++)
        {
            float d = x - strands[i].x;
            if (d * d < 0.1225f)
                return true;
        }

        LevelTest1.DelicataPeg[] delicataCreams = LevelTest1.DelicataPegs;
        for (int i = 0; i < delicataCreams.Length; i++)
        {
            float d = x - delicataCreams[i].x;
            if (d * d < 0.1225f)
                return true;
        }

        LevelTest1.CarnivalPeg[] mottles = LevelTest1.CarnivalPegs;
        for (int i = 0; i < mottles.Length; i++)
        {
            float d = x - mottles[i].x;
            if (d * d < 0.1225f)
                return true;
        }

        LevelTest1.SweetDumplingPeg[] dumplings = LevelTest1.SweetDumplingPegs;
        for (int i = 0; i < dumplings.Length; i++)
        {
            float d = x - dumplings[i].x;
            if (d * d < 0.1225f)
                return true;
        }

        LevelTest1.KabochaPeg[] speckles = LevelTest1.KabochaPegs;
        for (int i = 0; i < speckles.Length; i++)
        {
            float d = x - speckles[i].x;
            if (d * d < 0.1225f)
                return true;
        }

        return false;
    }

    private static bool NearGroveTree(float x)
    {
        // Chunks stream in Begin() before LevelGrove.Plant, so Planted is
        // still null on the first tape. Authored Xs / AppleX are enough.
        float[] xs = LevelGrove.Xs;
        for (int i = 0; i < xs.Length; i++)
        {
            float d = x - xs[i];
            if (d * d < 1.21f)
                return true;
        }

        float apple = x - LevelGrove.AppleX;
        return apple * apple < 1.21f;
    }

    private static Transform ChildNamed(Transform parent, string name)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform ch = parent.GetChild(i);
            if (ch.name == name)
                return ch;
        }
        return null;
    }

    private static int Mix(int a, int b)
    {
        unchecked
        {
            return a * 397 ^ b * -1640531527;
        }
    }

    private static float Hash01(int a, int b)
    {
        unchecked
        {
            uint h = (uint)(a * 374761393 + b * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            return (h & 0xFFFFFFu) / 16777215f;
        }
    }
}
