using System.Collections.Generic;
using UnityEngine;

public enum TreeRig
{
    // Recursive hinges: trunk and forks sway. Scene Tree, not a grove.
    Sway = 0,
    // Picture only: no Rigidbody2D, no wood collider. Sit pads stay.
    // Static Apple also look-sways Visual / leaf / fruit (Level1 yard).
    Static = 1
}

// Growth recipe. Oak is the hold/perch canon. Auto picks 0…4 from the seed.
// Apple is opt-in (ParseKind "apple"); the yard cycle does not include it.
// Lilac / Hazel / Cedar / Plum are Play-gallery kinds (KindFromGalleryIndex).
public enum TreeKind
{
    Oak = 0,
    Pine = 1,
    Willow = 2,
    Bush = 3,
    Poplar = 4,
    Apple = 5,
    Lilac = 6,
    Hazel = 7,
    Cedar = 8,
    Plum = 9,
    Auto = 10
}

// Picture tint only. Hinges, masses, and fork angles stay on the grow recipe.
public enum PlantSeason
{
    Auto = 0,
    Spring = 1,
    Summer = 2,
    Autumn = 3,
    Winter = 4
}

// Bark plate layout. ApplyPicture is a rectangle, so kind reads from
// how the boxes sit, not from a PNG grain.
enum BarkLook
{
    Furrow = 0,
    Scale = 1,
    Strip = 2,
    Lenticel = 3,
    Plate = 4
}

// Recursive tree build. Plant layer, not human and not level.
// Class is not Tree: Unity has UnityEngine.Tree (Terrain).
// A segment is HumanSegment: box + Visual, no human anatomy.
// No muscles: wood is TreeSpring + JointFriction. Wind is a force on the segment.
public class PlantTree : MonoBehaviour
{
    [Header("Build")]
    public TreeRig rig = TreeRig.Sway;
    [Tooltip("Oak is the stand canon. Auto = seed % 5 (yard). Scene Tree uses the 9-kind gallery.")]
    public TreeKind kind = TreeKind.Oak;
    [Tooltip("Total mass of live segments after normalize, kg. Stump not included.")]
    public float totalMass = 28f;
    public bool normalizeMass = true;
    [Tooltip("Density, kg/m³. 2D-slice thickness = segment width.")]
    public float woodDensity = 700f;

    [Header("Recursion")]
    [Tooltip("Fork depth. 3 → up to 15 segments (2^(d+1)−1), like one Human.")]
    public int maxDepth = 3;
    [Tooltip("Hard body cap. Growth stops; the solver does not explode.")]
    public int maxSegments = 15;
    public float trunkLength = 0.82f;
    public float trunkWidth = 0.16f;
    public float stumpHeight = 0.22f;
    public float stumpWidth = 0.20f;
    public float lengthDecay = 0.72f;
    public float widthDecay = 0.65f;
    [Tooltip("Fork angle from the parent axis, degrees. Leader takes a fraction, side takes the full angle.")]
    public float forkAngle = 28f;
    public float minWidth = 0.035f;
    public float minLength = 0.12f;
    public int seed = 1;
    [Tooltip("Fork scatter, degrees. Local Random, not UnityEngine.Random.")]
    public float forkJitter = 4f;

    [Header("Wood")]
    public float trunkBendLimit = 16f;
    public float twigBendLimit = 40f;
    [Tooltip("Trunk stiffness, N·m/rad.")]
    public float trunkStiffness = 180f;
    public float twigStiffness = 22f;
    public float trunkFriction = 8f;
    public float twigFriction = 0.35f;
    public float trunkSpringMaxTorque = 250f;
    public float twigSpringMaxTorque = 40f;
    public float minSegmentInertia = 0.008f;

    [Header("Wind")]
    [Tooltip("Oncoming air speed, m/s. Force ~ ½ρv²·area.")]
    public float windSpeed = 2.2f;
    public float windGust = 1.1f;
    public float windPeriod = 4.5f;
    [Tooltip("Sail scale. 1 = formula as written.")]
    public float windScale = 0.12f;

    [Header("Bird wake")]
    [Tooltip("How far a flying bird still rustles a branch, m.")]
    public float birdWakeRange = 1.15f;
    [Tooltip("Peak horizontal force on a twig from a 4 m/s fly-by at contact, N. 0 = off.")]
    public float birdWakeForce = 2.8f;
    [Tooltip("Ignore slower birds, m/s. Sit and walk stay quiet.")]
    public float birdWakeMinSpeed = 0.7f;

    [Header("Look sway — Static Apple")]
    [Tooltip("Tip picture bend from wind, degrees. 0 = frozen. Sway trees ignore this.")]
    public float lookSwayDegrees = 5.5f;
    [Tooltip("Trunk share of lookSwayDegrees. Tips get the full angle.")]
    public float lookSwayTrunkShare = 0.22f;
    [Tooltip("Extra tip picture bend from one close fly-by, degrees.")]
    public float lookBirdSwayDegrees = 8f;

    [Header("Leaves — picture only")]
    public bool growLeaves = true;
    public Vector2 leafSize = new Vector2(0.10f, 0.14f);
    [Tooltip("Tip width of the wood picture as a fraction of collider width. 1 = no taper.")]
    public float visualTaper = 0.58f;
    [Tooltip("Sway-only leaf flutter, degrees. Static grove stays frozen.")]
    public float leafSwayDegrees = 8f;

    [Header("Season — picture only")]
    [Tooltip("Auto = seed % 4 (Spring…Winter). Default Summer so the World grove stays in leaf.")]
    public PlantSeason season = PlantSeason.Summer;

    [Header("Fruit — picture only (Apple)")]
    [Tooltip("Apple only. Sprites on twigs; sit contact stays on BirdPerch wood pads.")]
    public bool growFruit = false;
    public Vector2 fruitSize = new Vector2(0.05f, 0.05f);

    [Header("Picture")]
    public float jointMarkerDiameter = 0.025f;

    public int SegmentCount { get; private set; }
    public int DynamicBodyCount { get; private set; }
    public int RigidbodyCount { get; private set; }
    public int WoodColliderCount { get; private set; }
    public int PerchPadCount { get; private set; }
    public int FruitCount { get; private set; }
    public int BloomCount { get; private set; }
    public TreeKind ResolvedKind { get; private set; }
    public PlantSeason ResolvedSeason { get; private set; }
    // Picture tint after ApplySeason. Stand JSON reads these, not Play eye.
    // Far is the shade / spent walk (autumn russet, winter twig).
    public float LeafTintR => palLeafNear.r;
    public float LeafTintG => palLeafNear.g;
    public float LeafTintB => palLeafNear.b;
    public float LeafTintFarR => palLeafFar.r;
    public float LeafTintFarG => palLeafFar.g;
    public float LeafTintFarB => palLeafFar.b;
    public int LeafCount { get; private set; }
    public bool HasLookSway { get; private set; }
    public bool HasLeafSway { get; private set; }
    // LateUpdate flutter is world +X (same axis as ½ρv²). Local XY on a
    // ±fork limb rustled along the stick after the first gust. Stand JSON.
    public bool LeafSwayWorldGust { get; private set; }
    public int BarkPlateCount => barkPlateCount;
    public int CrownMassCount => crownMassCount;
    // Envelope after ApplyLookRecipe. Spring/autumn shrink the cloud;
    // fork / collider size stay. Stand JSON can see it without -visual.
    public float CrownSizeX => crownSize.x;
    public float CrownSizeY => crownSize.y;
    public int RootFlareCount => rootFlareCount;
    public int ForkCrotchCount => forkCrotchCount;
    // Peak |heading| of wood at depth ≥ 1 after Grow. Stand JSON can
    // see a spreading crown without Play. Oak forkAngle 28 stays.
    public float MaxAbsBranchHeading { get; private set; }
    public int LeafClusterCount => leafClusterCount;
    public int LeafSwayCount => leafSwayXforms.Length;
    // After ApplyLookRecipe. Autumn deciduous droops the tuft so the
    // stand sees spent hang without a Play eye. Oak fork 28° stays.
    public float LeafTiltDegrees => leafTilt;
    // After ApplyLookRecipe. Autumn drops a leaflet so the stand can
    // see a thinner crown without a Play eye.
    public int LeafSpriteCount => leafSpriteCount;
    public int BudCount => budCount;
    // Pack-tip kinds (oak) put the tuft on true tips only. Internodes
    // stay wood so a shoot is not two stars along the last fifth.
    public bool LeafPackTip => leafPackTip;
    // Picture-only side twigs on first-fork wood. Not a hinge, not a
    // collider — oak fork 28° stays the grow recipe.
    public int BranchSpurCount => branchSpurCount;
    // Canopy sail multipliers on ½ρv². Trunk area is large; without
    // this the bole shoves the spine like a pole. Tips carry the leaves.
    public float WindSailTrunk => WindSail(0);
    public float WindSailTip => WindSail(Mathf.Max(1, growDepthCap));
    // Picture/force sail vs summer. Hold (-treeWind 0) still bails.
    public float WindSailSeason => SeasonSailMul();
    // LateUpdate look / leaf amp vs summer. Same table as ½ρv², plus
    // winter deciduous 0.80 on the woody cloud (force already *0.70).
    public float PictureSeasonSail => PictureSeasonAmp();
    // Extra ½ρv² on a 45° limb vs the leader. Laterals catch the gust;
    // the bole stays a pole. public windScale 0.12 stays.
    public float WindSailLean => HeadingSailMul(45f);
    // Tuft hub rest at 45° (oak). Matches crown flatten PalmateTrack.
    // 0 on pine / cedar / poplar / willow hang. Stand JSON sees it
    // without Play.
    public float LeafClusterRestDegrees => LeafClusterRest(45f);
    // Oak leader is fork × leaderForkShare (~7.8°). Stand JSON saw 0
    // while the 8° deadzone skipped PalmateTrack. SpreadLimbDeg 5
    // flattens that tuft. forkAngle 28 stays.
    public float LeafClusterRestLeader =>
        LeafClusterRest(forkAngle * leaderForkShare);
    // Nodal hang pad vs wood width. Spring flush sits tight; autumn
    // spent tufts droop off the stick. Summer 0.28 stays.
    public float LeafClusterHangPad => ClusterHangMul();
    // How much extra wood bend LateUpdate peels off the picture so a
    // palmate stays a canopy (0.70) or fruit hangs world-down (1).
    // Pine / poplar stay 0 — needles ride the twig. Stand JSON.
    public float LeafLiveTrack => KindPictureTrack();
    // Peak HeadingSailMul of a hung / plate crown at grow. Leader
    // wood is ~8° of fork 28, so wood sail alone left the dome as a
    // pole. Stand JSON, not Play eye.
    public float LeafCrownHangSail => leafCrownHangSail;
    // Same plate sail on a nodal tuft, lilac raceme, and hazel catkin.
    // Hub at 0.04 of wood width left HeadingSailMul of ~8° as pole
    // flutter. Stand JSON, not Play eye.
    public float LeafClusterHangSail => leafClusterHangSail;
    // TreeDriver LateUpdate. No Find, no name scan.
    public bool WantsLookSwayTick =>
        HasLookSway && (Mathf.Abs(windSpeed) >= 0.01f || lookBirdSwayDegrees > 0f);
    public bool WantsLeafSwayTick =>
        rig == TreeRig.Sway && HasLeafSway && LeafSwayCount > 0;
    // Child picture base / collider at depth 1. < 1 means the first fork
    // joins the trunk tip instead of a mushroom on the grow box.
    public float WoodJoinScale => WoodBaseScale(1);
    // Same join at depth 2. 05:02 closed depth 1; WoodTipScale(1) sat
    // at widthDecay so this clamped to 1 and the next fork mushroomed.
    public float WoodJoinScaleInner => WoodBaseScale(2);
    // Picture tip / collider on the leader. < 1 is a cone, not a post.
    public float WoodTipScaleLeader => WoodTipScale(0);
    // After ApplyLookRecipe. Stand JSON can see bark wood without -visual.
    public float WoodTwigR => palTwig.r;
    public float WoodTwigG => palTwig.g;
    public float WoodTwigB => palTwig.b;
    // Kind plates after season lerp. Winter orchard must not keep a
    // summer russet flake on a grey bole. Stand JSON, not Play eye.
    public float BarkLiteR => palBarkLite.r;
    public float BarkLiteG => palBarkLite.g;
    public float BarkLiteB => palBarkLite.b;
    // Bare deciduous keeps a twig cloud. Lilac winter is a grey cane.
    public bool WinterWoodyCrown => winterWoodyCrown;

    // Play scene Tree. Yard grove still KindFromPlantIndex (Oak…Poplar).
    private static readonly TreeKind[] SceneGallery =
    {
        TreeKind.Oak, TreeKind.Pine, TreeKind.Willow, TreeKind.Bush, TreeKind.Poplar,
        TreeKind.Lilac, TreeKind.Hazel, TreeKind.Cedar, TreeKind.Plum
    };

    // Flat bark plates + crown masses + leaf pose. No PNG. Collider sizes stay.
    private float leaderForkShare = 0.28f;
    private float sideLengthScale = 1f;
    private bool skipSideOnEvenDepth;
    private bool bothSidesAtRoot;
    // Extra opposite fork through this depth inclusive. −1 = off.
    // Static Apple uses 1 so the crown is ternary on the first two levels.
    private int bothSidesMaxDepth = -1;
    // True Y-split: two equal children, no leader bias (Lilac).
    private bool binaryFork;
    // Extra trunks from the stump besides the first Grow (Hazel).
    private int extraRootStems;
    private float rootStemAngle = 22f;
    // Leader plus two sides at every depth (Cedar layers).
    private bool whorlFork;
    // Side wood is a tip: leaves/bloom, no further fork.
    private bool sidesAreTips;
    private bool growBloom;
    private int bloomClusterCount = 4;
    private Vector2 bloomSize = new Vector2(0.07f, 0.07f);
    private HumanVisualShape bloomShape = HumanVisualShape.Box;
    private HumanVisualShape fruitShape = HumanVisualShape.Box;
    private HumanVisualShape leafShape = HumanVisualShape.Box;
    private int leafSpriteCount = 2;
    private bool fruitOnInnerWood;
    private bool useKindPalette;
    private bool woodAxisPerch;
    private float worldHoldScale;
    private Color palStump;
    private Color palTrunk;
    private Color palBranch;
    private Color palTwig;
    private Color palTip;
    private Color palLeafNear;
    private Color palLeafFar;
    private Color palBloom;
    private Color palFruit;
    private Color palBarkDark;
    private Color palBarkLite;
    private int barkPlateCount;
    private int rootFlareCount;
    private int forkCrotchCount;
    private int leafClusterCount;
    private int budCount;
    private int branchSpurCount;
    private float leafTilt;
    private bool leafHangWorldDown;
    private bool leafPackTip;
    // Spring flush on kinds that are not already bloom shrubs (lilac).
    // One axillary packet, not a 3-spray on the packed tuft.
    private bool seasonAddedBloom;
    private bool crownStackOnLeader;
    private int crownMassCount;
    private Vector2 crownSize;
    // Winter deciduous: kind cloud in twig/bark, not a green Leaf bake.
    // Zeroing crownMassCount left every leafless tree a telephone pole.
    private bool winterWoodyCrown;
    private float leafCrownHangSail = 1f;
    private float leafClusterHangSail = 1f;
    // ApplyPicture ignores Ellipse/Capsule — silhouette is overlapping boxes.
    private int leafLobes;
    private int crownLobes;
    private BarkLook barkLook;

    private readonly List<Node> nodes = new List<Node>(16);
    private static readonly List<PlantTree> Live = new List<PlantTree>(64);
    private readonly List<PerchPoint> perchAcc = new List<PerchPoint>(256);
    private Vector2[] perchSlots = System.Array.Empty<Vector2>();
    private Bounds perchBounds;
    private bool perchBoundsValid;
    private bool birdPerchesReady;
    private bool liveRegistered;
    public const int MaxBirdPerches = 20;
    // Static Apple crown is denser than oak; pads stay cheap (no RB).
    public const int MaxAppleBirdPerches = 48;
    private System.Random rng;
    private int growDepthCap;
    private const float AirDensity = 1.2f;
    private const float BirdWakeRefSpeed = 4f;
    // Shared fly-by list. Find is scene-wide and must not run every frame.
    // Positions and velocities are still read live from the cached refs.
    private const float WakeBirdsRefreshSeconds = 0.75f;
    private static BirdController[] wakeBirds = System.Array.Empty<BirdController>();
    private static float wakeBirdsAt = -999f;
    private LookSprite[] lookSprites = System.Array.Empty<LookSprite>();
    private float[] lookHeading = System.Array.Empty<float>();
    private Vector2[] lookCenter = System.Array.Empty<Vector2>();
    private float lookPhase;
    private readonly List<Transform> leafSwayAcc = new List<Transform>(64);
    private readonly List<float> leafSwayAmpAcc = new List<float>(64);
    private readonly List<float> leafSwayPhaseAcc = new List<float>(64);
    private Transform[] leafSwayXforms = System.Array.Empty<Transform>();
    private Quaternion[] leafSwayRest = System.Array.Empty<Quaternion>();
    private Vector3[] leafSwayRestPos = System.Array.Empty<Vector3>();
    private float[] leafSwaySign = System.Array.Empty<float>();
    private float[] leafSwayAmp = System.Array.Empty<float>();
    private float[] leafSwayPhase = System.Array.Empty<float>();
    private float[] leafSwayTrack = System.Array.Empty<float>();
    private int[] leafSwayWood = System.Array.Empty<int>();
    private Vector3[] leafSwayStation = System.Array.Empty<Vector3>();
    private float[] leafSwayDrop = System.Array.Empty<float>();
    // Palmate / crown peel toward world-up. 0.55 left leftover heading
    // (flag on the stick). Fruit / willow hang use 1.
    private const float PalmateTrack = 0.70f;
    // Outward laterals vs a near-axis leader. Oak fork 28 × 0.28 ≈ 7.8°,
    // so an 8° cut left the canon crown and tuft as a hat on the pole
    // (spur already used 5°). Grow, clusters, and crown share this.
    // forkAngle 28 stays the hinge recipe.
    public const float SpreadLimbDeg = 5f;

    private struct Node
    {
        public HumanSegment segment;
        public float heading;
        public int depth;
        public bool dynamic;
        public int parentIndex;
        public TreeSpring spring;
    }

    // Picture child of a Static wood box. Wood and BirdPerch stay at the
    // grow pose so Ground pads are not dragged every frame.
    private struct LookSprite
    {
        public Transform xform;
        public int nodeIndex;
        public Vector3 restLocalPos;
        public Quaternion restLocalRot;
        // 0 = glued to fake wood; PalmateTrack / 1 = keep canopy / hang
        // as the look pose bends. Wood Visual stays 0.
        public float track;
    }

    private struct PerchPoint
    {
        public int depth;
        public float y;
        public Vector2 p;
        public HumanSegment wood;
    }

    private Color WoodColor(int depth, bool farSide)
    {
        Color c;
        if (depth < 0) c = palStump;
        else if (depth == 0) c = palTrunk;
        else if (depth == 1) c = palBranch;
        else if (depth == 2) c = palTwig;
        else c = palTip;
        if (farSide)
        {
            c.r *= 0.82f;
            c.g *= 0.82f;
            c.b *= 0.82f;
        }
        return c;
    }

    // Per-cluster tint so a canopy is mixed patches, not one green card.
    // Kind sets the family (oak russet, poplar gold, willow straw);
    // the index only walks that family. Pine/cedar stay the recipe pair.
    private Color LeafTint(int i, bool farSide)
    {
        bool evergreen = ResolvedKind == TreeKind.Pine || ResolvedKind == TreeKind.Cedar;
        Color near = palLeafNear;
        Color far = palLeafFar;
        if (!evergreen && ResolvedSeason == PlantSeason.Autumn)
        {
            int k = i % 3;
            if (k == 0)
                near = palLeafNear;
            else if (k == 1)
                near = Color.Lerp(palLeafNear, palLeafFar, 0.45f);
            else
                near = palLeafFar;
            far = Color.Lerp(near, palBarkDark, 0.18f);
        }
        else if (!evergreen && ResolvedSeason == PlantSeason.Summer)
        {
            // Sun leaflet vs shade. A second lime lerp here would stack
            // on ApplySeason; summer palette is already the recipe.
            int k = i % 3;
            if (k == 1)
                near = Color.Lerp(palLeafNear, palLeafFar, 0.32f);
            else if (k == 2)
                near = Color.Lerp(palLeafNear, new Color(0.30f, 0.54f, 0.14f, 1f), 0.22f);
            far = Color.Lerp(near, palLeafFar, 0.40f);
        }
        else if (!evergreen && ResolvedSeason == PlantSeason.Spring)
        {
            // Flush vs older leaf. ApplySeason already did the 0.62 lime
            // family lerp — only a short walk, not neon.
            if ((i & 1) == 1)
                near = Color.Lerp(palLeafNear, palLeafFar, 0.28f);
            far = Color.Lerp(near, palLeafFar, 0.35f);
        }
        return (farSide || (i & 1) == 1) ? far : near;
    }

    // Sun vs shade inside one crown mass. Oak already mixes LobeR;
    // bush / willow / orchard extras were one russet card after LeafTint.
    // Artist outer plates (pine SlopeR, cedar FanR/M2, oak FingerL)
    // still shared the parent card after they left the spine.
    private Color CrownPatch(Color near, bool farPatch)
    {
        if (winterWoodyCrown || !farPatch)
            return near;
        if (ResolvedSeason == PlantSeason.Autumn)
            return palLeafFar;
        if (ResolvedSeason == PlantSeason.Spring)
            return Color.Lerp(near, palLeafFar, 0.28f);
        return Color.Lerp(near, palLeafFar, 0.22f);
    }

    // Visual 12:52: odd fans / Twig1 / ToothR / Reach mix stem 0.16 so
    // they are not the parent card after they left the spine. Two
    // discrete colours, not a gradient inside one silhouette.
    private Color OddLeaflet(Color near)
    {
        return Color.Lerp(near, palBarkDark, 0.16f);
    }

    // Winter cloud uses bark bake. palTwig on willow / bush / poplar
    // stays a green stick after the 0.35 winter lerp — that leftover
    // lime is a summer envelope in another colour. Two discrete bark
    // colours, one flat box each. Not a gradient inside a silhouette.
    private Color WoodCrownTint(int i)
    {
        return (i & 1) == 0 ? palBarkLite : palBarkDark;
    }

    private string CrownBake =>
        winterWoodyCrown ? ArtLibrary.Bark : ArtLibrary.Leaf;

    public static float RootY(float groundY = -2f)
    {
        return groundY;
    }

    public static TreeKind KindFromPlantIndex(int index)
    {
        const int n = 5;
        int k = index % n;
        if (k < 0) k += n;
        return (TreeKind)k;
    }

    // Scene Tree Play gallery. Does not include Apple (Level1 opt-in).
    // Yard grove must keep KindFromPlantIndex so World silhouettes stay Oak…Poplar.
    public static TreeKind KindFromGalleryIndex(int index)
    {
        int n = SceneGallery.Length;
        int k = index % n;
        if (k < 0) k += n;
        return SceneGallery[k];
    }

    public static TreeKind ParseKind(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return TreeKind.Oak;
        switch (raw.Trim().ToLowerInvariant())
        {
            case "pine": return TreeKind.Pine;
            case "willow": return TreeKind.Willow;
            case "bush": return TreeKind.Bush;
            case "poplar": return TreeKind.Poplar;
            case "apple": return TreeKind.Apple;
            case "lilac": return TreeKind.Lilac;
            case "hazel": return TreeKind.Hazel;
            case "cedar": return TreeKind.Cedar;
            case "plum": return TreeKind.Plum;
            case "auto": return TreeKind.Auto;
            default: return TreeKind.Oak;
        }
    }

    // Picture tint only. Grow fork / masses stay on ApplyKind.
    public static PlantSeason ParseSeason(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return PlantSeason.Summer;
        switch (raw.Trim().ToLowerInvariant())
        {
            case "spring": return PlantSeason.Spring;
            case "autumn":
            case "fall": return PlantSeason.Autumn;
            case "winter": return PlantSeason.Winter;
            case "auto": return PlantSeason.Auto;
            default: return PlantSeason.Summer;
        }
    }

    // Headless CLI without editing HeadlessTrial. Empty = flag absent.
    public static string ReadPlantArg(string name)
    {
        if (string.IsNullOrEmpty(name))
            return "";
        string[] argv = System.Environment.GetCommandLineArgs();
        for (int argI = 0; argI < argv.Length - 1; argI++)
        {
            if (string.Equals(argv[argI], name, System.StringComparison.OrdinalIgnoreCase))
                return argv[argI + 1] ?? "";
        }
        return "";
    }

    public void BuildTree()
    {
        foreach (Transform child in transform)
            Destroy(child.gameObject);
        TreeDriver oldDriver = GetComponent<TreeDriver>();
        if (oldDriver != null)
            Destroy(oldDriver);

        nodes.Clear();
        perchAcc.Clear();
        perchSlots = System.Array.Empty<Vector2>();
        birdPerchesReady = false;
        PerchPadCount = 0;
        SegmentCount = 0;
        DynamicBodyCount = 0;
        ResolvedKind = kind == TreeKind.Auto ? KindFromPlantIndex(seed) : kind;
        if ((int)ResolvedKind < 0 || (int)ResolvedKind >= (int)TreeKind.Auto)
            ResolvedKind = TreeKind.Oak;
        FruitCount = 0;
        BloomCount = 0;
        LeafCount = 0;
        forkCrotchCount = 0;
        MaxAbsBranchHeading = 0f;
        leafCrownHangSail = 1f;
        leafClusterHangSail = 1f;
        leafClusterCount = 0;
        budCount = 0;
        branchSpurCount = 0;
        HasLookSway = false;
        HasLeafSway = false;
        lookSprites = System.Array.Empty<LookSprite>();
        leafSwayAcc.Clear();
        leafSwayAmpAcc.Clear();
        leafSwayPhaseAcc.Clear();
        leafSwayXforms = System.Array.Empty<Transform>();
        leafSwayPhase = System.Array.Empty<float>();
        leafSwayRestPos = System.Array.Empty<Vector3>();
        leafSwayTrack = System.Array.Empty<float>();
        leafSwayWood = System.Array.Empty<int>();
        leafSwayStation = System.Array.Empty<Vector3>();
        leafSwayDrop = System.Array.Empty<float>();
        if (HeadlessTrial.Active)
        {
            string seasonArg = ReadPlantArg("-treeSeason");
            if (seasonArg.Length > 0)
                season = ParseSeason(seasonArg);
        }
        ApplyKind(ResolvedKind);
        rng = new System.Random(seed);
        bool live = rig == TreeRig.Sway;
        // Sway is one Human of bodies. Static is a picture: hundreds of
        // twigs must not become Box2D fixtures.
        growDepthCap = Mathf.Clamp(maxDepth, 1, live ? 5 : 8);
        int cap = Mathf.Clamp(maxSegments, 3, live ? 31 : 255);
        if (nodes.Capacity < cap + 1)
            nodes.Capacity = cap + 1;

        Color stumpBark = WoodColor(-1, false);
        HumanSegment stump = CreateWood(
            "Stump",
            new Vector2(stumpWidth, stumpHeight),
            woodDensity * stumpWidth * stumpWidth * stumpHeight,
            stumpBark,
            0,
            live,
            false);
        stump.transform.localPosition = new Vector3(0f, stumpHeight * 0.5f, 0f);
        stump.transform.localRotation = Quaternion.identity;
        if (stump.rb != null)
            stump.rb.bodyType = RigidbodyType2D.Static;
        PaintWoodTaper(stump, stumpWidth, stumpHeight, stumpBark, -1, 0f);
        PaintRootFlare(stump);
        nodes.Add(new Node { segment = stump, heading = 0f, depth = -1, dynamic = false, parentIndex = -1, spring = null });

        Grow(stump, 0f, 0, trunkLength, trunkWidth, 0f, cap);
        if (extraRootStems >= 1)
            Grow(stump, 0f, 0, trunkLength * 0.92f, trunkWidth * 0.88f, -rootStemAngle, cap);
        if (extraRootStems >= 2)
            Grow(stump, 0f, 0, trunkLength * 0.88f, trunkWidth * 0.84f, rootStemAngle, cap);
        if (extraRootStems >= 3)
            Grow(stump, 0f, 0, trunkLength * 0.80f, trunkWidth * 0.78f, -rootStemAngle * 1.65f, cap);

        TallyBranchSpread();
        AttachCrowns();

        if (live && normalizeMass)
            NormalizeDynamicMass();

        if (live)
        {
            DisableCollisionsBetweenSegments();
            MarkWoodAsPerch();
            Physics2D.SyncTransforms();
            BindGravityHold();
        }
        CollectPerchAndBounds();
        TallyBodies();
        BindLookSway();
        BindLeafSway();
        RegisterLive();

        if (rig == TreeRig.Sway || HasLookSway)
        {
            TreeDriver driver = gameObject.AddComponent<TreeDriver>();
            driver.RebuildCache(this);
        }
    }

    void OnDestroy()
    {
        Live.Remove(this);
        liveRegistered = false;
    }

    public static bool NearPerch(Vector2 pos, float radius)
    {
        // No Bounds.Expand / Vector3 alloc — flock Update may call this per bird.
        for (int i = 0; i < Live.Count; i++)
        {
            PlantTree tree = Live[i];
            if (tree == null || !tree.perchBoundsValid)
                continue;
            Bounds b = tree.perchBounds;
            if (pos.x >= b.min.x - radius && pos.x <= b.max.x + radius
                && pos.y >= b.min.y - radius && pos.y <= b.max.y + radius)
                return true;
        }
        return false;
    }

    public Vector2[] GetPerchSlots()
    {
        return perchSlots;
    }

    // Thin Ground pads at the sit points. Wood stays ghost to birds.
    public void EnsureBirdPerches()
    {
        if (birdPerchesReady)
            return;
        birdPerchesReady = true;

        int padCap = UsesDensePerch() ? MaxAppleBirdPerches : MaxBirdPerches;
        int nPad = Mathf.Min(padCap, perchAcc.Count);
        int made = 0;
        for (int i = 0; i < nPad; i++)
        {
            HumanSegment wood = perchAcc[i].wood;
            if (wood == null)
                continue;
            GameObject pad = new GameObject("BirdPerch");
            pad.AddComponent<BirdPerch>();
            pad.transform.SetParent(wood.transform, true);
            Vector2 world = perchAcc[i].p;
            pad.transform.position = new Vector3(world.x, world.y - 0.012f, 0f);
            BoxCollider2D col = pad.AddComponent<BoxCollider2D>();
            col.size = new Vector2(0.12f, 0.024f);
            GroundLayers.Apply(pad);
            made++;
        }

        PerchPadCount = made;
    }

    public float GetMaxAbsJointAngle()
    {
        float max = 0f;
        for (int i = 0; i < nodes.Count; i++)
        {
            HumanSegment seg = nodes[i].segment;
            if (seg == null || seg.joint == null) continue;
            // Spring is bound at Grow. Do not GetComponent here: the
            // stand samples this every physics step (200 Hz).
            TreeSpring spring = nodes[i].spring;
            float rest = spring != null ? spring.restAngle : 0f;
            float abs = Mathf.Abs(Mathf.DeltaAngle(rest, seg.joint.jointAngle));
            if (abs > max) max = abs;
        }
        return max;
    }

    public void GetHoldStats(out float meanAbsHold, out float maxAbsHold)
    {
        float sum = 0f;
        float max = 0f;
        int n = 0;
        for (int i = 0; i < nodes.Count; i++)
        {
            HumanSegment seg = nodes[i].segment;
            if (seg == null) continue;
            TreeSpring spring = nodes[i].spring;
            if (spring == null) continue;
            float abs = Mathf.Abs(spring.holdTorque);
            sum += abs;
            if (abs > max) max = abs;
            n++;
        }
        meanAbsHold = n > 0 ? sum / n : 0f;
        maxAbsHold = max;
    }

    public static void SeatBirds(Bird[] birds, PlantTree tree)
    {
        if (birds == null || tree == null) return;
        Bird.GhostTreeWood(birds, tree);
        Vector2[] slots = tree.GetPerchSlots();
        if (slots.Length == 0) return;
        List<Collider2D> birdCols = new List<Collider2D>(64);
        List<Collider2D> tmp = new List<Collider2D>(8);
        for (int i = 0; i < birds.Length; i++)
        {
            Bird bird = birds[i];
            if (bird == null) continue;
            Vector2 slot = slots[i % slots.Length];
            int stack = i / slots.Length;
            float x = slot.x + (stack % 5 - 2) * 0.11f;
            float y = SeatRootY(bird, slot.y) + (stack / 5) * 0.03f;
            bird.transform.position = new Vector2(x, y);
            if (bird.controller != null)
            {
                bird.controller.mode = BirdMode.Sit;
                bird.controller.takeoffDelay = 0f;
            }

            tmp.Clear();
            bird.GetComponentsInChildren(tmp);
            birdCols.AddRange(tmp);
        }

        for (int i = 0; i < birdCols.Count; i++)
        {
            for (int j = i + 1; j < birdCols.Count; j++)
            {
                if (birdCols[i] != null && birdCols[j] != null)
                    Physics2D.IgnoreCollision(birdCols[i], birdCols[j], true);
            }
        }
    }

    private static float SeatRootY(Bird bird, float perchY)
    {
        if (bird.perchCollider != null)
        {
            float bottom = bird.perchCollider.offset.y - bird.perchCollider.size.y * 0.5f;
            return perchY - bottom;
        }

        return bird.StandingRootOffset(perchY);
    }

    public float GetCrownY()
    {
        float y = transform.position.y;
        for (int i = 0; i < nodes.Count; i++)
        {
            HumanSegment seg = nodes[i].segment;
            if (seg == null) continue;
            float top = seg.transform.position.y + seg.size.y * 0.5f;
            if (top > y) y = top;
        }
        return y;
    }

    public Vector2 GetFollowPoint()
    {
        float massSum = 0f;
        Vector2 weighted = Vector2.zero;
        for (int i = 0; i < nodes.Count; i++)
        {
            Node n = nodes[i];
            if (n.segment == null || n.segment.rb == null || !n.dynamic)
                continue;
            float m = n.segment.rb.mass;
            massSum += m;
            weighted += (Vector2)n.segment.rb.worldCenterOfMass * m;
        }

        if (massSum > 0.001f)
            return weighted / massSum;
        return (Vector2)transform.position + new Vector2(0f, 1.4f);
    }

    // Presets overwrite public grow fields. Oak matches the hold canon.
    private void ApplyKind(TreeKind recipe)
    {
        leaderForkShare = 0.28f;
        sideLengthScale = 1f;
        skipSideOnEvenDepth = false;
        bothSidesAtRoot = false;
        bothSidesMaxDepth = -1;
        binaryFork = false;
        extraRootStems = 0;
        rootStemAngle = 22f;
        whorlFork = false;
        sidesAreTips = false;
        growBloom = false;
        seasonAddedBloom = false;
        bloomClusterCount = 4;
        bloomSize = new Vector2(0.07f, 0.07f);
        bloomShape = HumanVisualShape.Box;
        fruitShape = HumanVisualShape.Box;
        leafShape = HumanVisualShape.Box;
        leafSpriteCount = 2;
        fruitOnInnerWood = false;
        useKindPalette = false;
        woodAxisPerch = false;
        worldHoldScale = 0f;
        palStump = new Color(0.28f, 0.16f, 0.08f, 1f);
        palTrunk = new Color(0.38f, 0.22f, 0.10f, 1f);
        palBranch = new Color(0.42f, 0.26f, 0.12f, 1f);
        palTwig = new Color(0.46f, 0.30f, 0.14f, 1f);
        palTip = new Color(0.50f, 0.34f, 0.16f, 1f);
        palLeafNear = new Color(0.22f, 0.50f, 0.16f, 1f);
        palLeafFar = new Color(0.16f, 0.38f, 0.12f, 1f);
        palBloom = new Color(0.86f, 0.62f, 0.78f, 1f);
        palFruit = new Color(0.78f, 0.16f, 0.14f, 1f);
        palBarkDark = palStump;
        palBarkLite = palTrunk;
        barkPlateCount = 4;
        rootFlareCount = 3;
        leafTilt = 38f;
        leafHangWorldDown = false;
        leafPackTip = true;
        crownStackOnLeader = false;
        crownMassCount = 2;
        crownSize = new Vector2(0.34f, 0.26f);
        leafLobes = 2;
        crownLobes = 3;
        barkLook = BarkLook.Furrow;
        growLeaves = true;
        leafSize = new Vector2(0.10f, 0.14f);
        growFruit = false;
        fruitSize = new Vector2(0.05f, 0.05f);
        maxDepth = 3;
        maxSegments = 15;
        trunkLength = 0.82f;
        trunkWidth = 0.16f;
        stumpHeight = 0.22f;
        stumpWidth = 0.20f;
        lengthDecay = 0.72f;
        widthDecay = 0.65f;
        minWidth = 0.035f;
        minLength = 0.12f;
        forkAngle = 28f;
        forkJitter = 4f;
        totalMass = 28f;
        trunkStiffness = 180f;
        twigStiffness = 22f;
        trunkFriction = 8f;
        twigFriction = 0.35f;
        trunkSpringMaxTorque = 250f;
        twigSpringMaxTorque = 40f;
        trunkBendLimit = 16f;
        twigBendLimit = 40f;

        if (recipe == TreeKind.Pine)
        {
            maxDepth = 4;
            trunkLength = 1.08f;
            trunkWidth = 0.13f;
            stumpHeight = 0.18f;
            stumpWidth = 0.16f;
            lengthDecay = 0.78f;
            widthDecay = 0.70f;
            forkAngle = 15f;
            forkJitter = 3f;
            leaderForkShare = 0.10f;
            sideLengthScale = 0.50f;
            skipSideOnEvenDepth = true;
            totalMass = 24f;
            leafShape = HumanVisualShape.Box;
            leafSpriteCount = 6;
            leafSize = new Vector2(0.06f, 0.10f);
        }
        else if (recipe == TreeKind.Willow)
        {
            trunkLength = 0.70f;
            trunkWidth = 0.14f;
            lengthDecay = 0.75f;
            widthDecay = 0.68f;
            forkAngle = 32f;
            forkJitter = 5f;
            leaderForkShare = 0.16f;
            totalMass = 22f;
            leafShape = HumanVisualShape.Box;
            leafSpriteCount = 5;
            leafSize = new Vector2(0.08f, 0.18f);
        }
        else if (recipe == TreeKind.Bush)
        {
            maxDepth = 2;
            trunkLength = 0.34f;
            trunkWidth = 0.18f;
            stumpHeight = 0.16f;
            stumpWidth = 0.22f;
            lengthDecay = 0.80f;
            widthDecay = 0.72f;
            forkAngle = 40f;
            forkJitter = 5f;
            bothSidesAtRoot = true;
            twigStiffness = 36f;
            twigSpringMaxTorque = 50f;
            totalMass = 16f;
            leafShape = HumanVisualShape.Box;
            leafSpriteCount = 5;
            leafSize = new Vector2(0.12f, 0.12f);
        }
        else if (recipe == TreeKind.Poplar)
        {
            trunkLength = 1.18f;
            trunkWidth = 0.11f;
            stumpHeight = 0.20f;
            stumpWidth = 0.14f;
            lengthDecay = 0.80f;
            widthDecay = 0.72f;
            forkAngle = 12f;
            forkJitter = 3f;
            leaderForkShare = 0.20f;
            totalMass = 22f;
            leafShape = HumanVisualShape.Box;
            leafSpriteCount = 5;
            leafSize = new Vector2(0.07f, 0.09f);
        }
        else if (recipe == TreeKind.Apple)
        {
            // Orchard: stump + leader (depth 0…3) = 3 m. Wide crown, upright
            // spine. Oak stiffness on this lever wept like willow; twigs
            // are stiffer so hold stays at the grow pose and wind sways.
            // Sway keeps the Human-sized 15-body cap. Static (Level1) may
            // grow a denser picture crown — no Box2D, only sprites.
            trunkLength = 1.04f;
            trunkWidth = 0.15f;
            stumpHeight = 0.20f;
            stumpWidth = 0.18f;
            lengthDecay = 0.74f;
            widthDecay = 0.66f;
            forkAngle = 36f;
            forkJitter = 5f;
            leaderForkShare = 0.08f;
            totalMass = 26f;
            leafShape = HumanVisualShape.Box;
            leafSpriteCount = 4;
            leafSize = new Vector2(0.11f, 0.12f);
            trunkStiffness = 240f;
            twigStiffness = 40f;
            twigSpringMaxTorque = 55f;
            growFruit = true;
            fruitOnInnerWood = true;
            fruitSize = new Vector2(0.10f, 0.10f);
            worldHoldScale = 1f;
            woodAxisPerch = true;
            if (rig == TreeRig.Static)
            {
                maxDepth = 5;
                maxSegments = 96;
                forkAngle = 40f;
                forkJitter = 6f;
                lengthDecay = 0.72f;
                widthDecay = 0.70f;
                minWidth = 0.022f;
                minLength = 0.10f;
                bothSidesAtRoot = true;
                bothSidesMaxDepth = 1;
            }
        }
        else if (recipe == TreeKind.Lilac)
        {
            // Colored stem that Y-splits, then splits again; blooms at the tips.
            useKindPalette = true;
            palStump = new Color(0.18f, 0.42f, 0.16f, 1f);
            palTrunk = new Color(0.28f, 0.62f, 0.24f, 1f);
            palBranch = new Color(0.34f, 0.70f, 0.28f, 1f);
            palTwig = new Color(0.40f, 0.76f, 0.32f, 1f);
            palTip = new Color(0.46f, 0.80f, 0.36f, 1f);
            palLeafNear = new Color(0.22f, 0.50f, 0.20f, 1f);
            palLeafFar = new Color(0.18f, 0.40f, 0.16f, 1f);
            palBloom = new Color(0.72f, 0.38f, 0.84f, 1f);
            palFruit = palBloom;
            binaryFork = true;
            growLeaves = false;
            growBloom = true;
            bloomClusterCount = 5;
            bloomSize = new Vector2(0.09f, 0.09f);
            bloomShape = HumanVisualShape.Box;
            woodAxisPerch = true;
            worldHoldScale = 0.6f;
            forkAngle = 26f;
            forkJitter = 4f;
            lengthDecay = 0.78f;
            widthDecay = 0.72f;
            trunkLength = 0.70f;
            trunkWidth = 0.11f;
            stumpHeight = 0.16f;
            stumpWidth = 0.14f;
            totalMass = 20f;
            if (rig == TreeRig.Sway)
            {
                maxDepth = 4;
                maxSegments = 31;
            }
            else
            {
                maxDepth = 6;
                maxSegments = 80;
                minWidth = 0.020f;
                minLength = 0.09f;
            }
        }
        else if (recipe == TreeKind.Hazel)
        {
            // Several trunks from one stump, then binary forks.
            useKindPalette = true;
            palStump = new Color(0.28f, 0.16f, 0.08f, 1f);
            palTrunk = new Color(0.42f, 0.26f, 0.12f, 1f);
            palBranch = new Color(0.50f, 0.34f, 0.16f, 1f);
            palTwig = new Color(0.56f, 0.40f, 0.18f, 1f);
            palTip = new Color(0.60f, 0.46f, 0.22f, 1f);
            palLeafNear = new Color(0.38f, 0.58f, 0.18f, 1f);
            palLeafFar = new Color(0.30f, 0.48f, 0.14f, 1f);
            palBloom = new Color(0.86f, 0.78f, 0.28f, 1f);
            palFruit = palBloom;
            extraRootStems = 2;
            rootStemAngle = 24f;
            binaryFork = true;
            growBloom = true;
            bloomClusterCount = 3;
            bloomSize = new Vector2(0.05f, 0.08f);
            bloomShape = HumanVisualShape.Box;
            leafShape = HumanVisualShape.Box;
            leafSpriteCount = 4;
            leafSize = new Vector2(0.11f, 0.08f);
            woodAxisPerch = true;
            worldHoldScale = 0.4f;
            trunkLength = 0.58f;
            trunkWidth = 0.10f;
            stumpHeight = 0.18f;
            stumpWidth = 0.24f;
            forkAngle = 22f;
            lengthDecay = 0.76f;
            widthDecay = 0.70f;
            totalMass = 22f;
            if (rig == TreeRig.Sway)
            {
                maxDepth = 2;
                maxSegments = 31;
            }
            else
            {
                maxDepth = 4;
                maxSegments = 96;
                minWidth = 0.020f;
                minLength = 0.09f;
            }
        }
        else if (recipe == TreeKind.Cedar)
        {
            // Layered crown: a vertical leader and near-horizontal tip fans.
            useKindPalette = true;
            palStump = new Color(0.32f, 0.14f, 0.10f, 1f);
            palTrunk = new Color(0.42f, 0.18f, 0.12f, 1f);
            palBranch = new Color(0.22f, 0.38f, 0.28f, 1f);
            palTwig = new Color(0.20f, 0.40f, 0.32f, 1f);
            palTip = new Color(0.18f, 0.42f, 0.34f, 1f);
            palLeafNear = new Color(0.16f, 0.40f, 0.34f, 1f);
            palLeafFar = new Color(0.12f, 0.32f, 0.28f, 1f);
            palBloom = palLeafNear;
            palFruit = palLeafNear;
            whorlFork = true;
            sidesAreTips = true;
            leaderForkShare = 0.05f;
            forkAngle = 68f;
            forkJitter = 3f;
            sideLengthScale = 0.55f;
            leafShape = HumanVisualShape.Box;
            leafSpriteCount = 4;
            leafSize = new Vector2(0.16f, 0.055f);
            woodAxisPerch = true;
            worldHoldScale = 0.8f;
            trunkLength = 0.62f;
            trunkWidth = 0.14f;
            stumpHeight = 0.20f;
            stumpWidth = 0.18f;
            lengthDecay = 0.84f;
            widthDecay = 0.78f;
            totalMass = 26f;
            if (rig == TreeRig.Sway)
            {
                maxDepth = 4;
                maxSegments = 31;
            }
            else
            {
                maxDepth = 6;
                maxSegments = 64;
                minWidth = 0.022f;
                minLength = 0.10f;
            }
        }
        else if (recipe == TreeKind.Plum)
        {
            // Fruit clusters of flat squares on a deeper forked crown.
            useKindPalette = true;
            palStump = new Color(0.18f, 0.10f, 0.12f, 1f);
            palTrunk = new Color(0.28f, 0.14f, 0.18f, 1f);
            palBranch = new Color(0.36f, 0.18f, 0.24f, 1f);
            palTwig = new Color(0.42f, 0.22f, 0.28f, 1f);
            palTip = new Color(0.48f, 0.26f, 0.32f, 1f);
            palLeafNear = new Color(0.28f, 0.50f, 0.22f, 1f);
            palLeafFar = new Color(0.22f, 0.40f, 0.18f, 1f);
            palBloom = new Color(0.78f, 0.42f, 0.62f, 1f);
            palFruit = new Color(0.58f, 0.16f, 0.40f, 1f);
            growFruit = true;
            fruitOnInnerWood = true;
            fruitShape = HumanVisualShape.Box;
            fruitSize = new Vector2(0.07f, 0.07f);
            leafShape = HumanVisualShape.Box;
            leafSpriteCount = 4;
            leafSize = new Vector2(0.10f, 0.08f);
            woodAxisPerch = true;
            worldHoldScale = 1f;
            forkAngle = 34f;
            forkJitter = 5f;
            leaderForkShare = 0.10f;
            trunkLength = 0.90f;
            trunkWidth = 0.13f;
            stumpHeight = 0.18f;
            stumpWidth = 0.16f;
            lengthDecay = 0.74f;
            widthDecay = 0.68f;
            totalMass = 24f;
            trunkStiffness = 220f;
            twigStiffness = 36f;
            if (rig == TreeRig.Sway)
            {
                maxDepth = 3;
                maxSegments = 15;
            }
            else
            {
                maxDepth = 5;
                maxSegments = 96;
                forkAngle = 38f;
                minWidth = 0.022f;
                minLength = 0.10f;
                bothSidesAtRoot = true;
                bothSidesMaxDepth = 1;
            }
        }

        if (!useKindPalette)
            ApplyFlatBark(recipe);
        ApplySeason();
        ApplyLookRecipe();
    }

    // Picture only. Kind silhouette at 1x: bark plates, leaf pose, crown mass.
    // Does not touch fork angles, masses, or collider size.
    // ApplyPicture bakes a rectangle; Ellipse/Capsule do not draw. Kind
    // reads from overlapping boxes (leaf tip, crown lobes) and plate layout.
    private void ApplyLookRecipe()
    {
        // 0.55 dark sat too close to stump: chips vanished into the
        // cone at 1x (grey-on-grey). 0.04 / 2.24 still soft after
        // TuftFill under TuftLTip / oak Skirt Tip still one-tent —
        // punch one more notch so a furrow is a crack, not a tint.
        // Kind overrides below still win. Collider / fork 28 stay.
        palBarkDark = new Color(palStump.r * 0.03f, palStump.g * 0.022f, palStump.b * 0.020f, 1f);
        palBarkLite = new Color(
            Mathf.Min(1f, palTrunk.r * 2.32f),
            Mathf.Min(1f, palTrunk.g * 2.04f),
            Mathf.Min(1f, palTrunk.b * 1.76f),
            1f);
        barkPlateCount = 4;
        rootFlareCount = 3;
        leafTilt = 38f;
        leafHangWorldDown = false;
        leafPackTip = true;
        crownStackOnLeader = false;
        crownMassCount = 2;
        // Oak crown envelope at 1x. Collider sizes stay on the grow recipe.
        crownSize = new Vector2(0.50f, 0.34f);
        leafShape = HumanVisualShape.Box;
        leafLobes = 2;
        crownLobes = 3;
        barkLook = BarkLook.Furrow;

        switch (ResolvedKind)
        {
            case TreeKind.Pine:
                barkPlateCount = 5;
                barkLook = BarkLook.Scale;
                leafLobes = 1;
                crownLobes = 1;
                leafTilt = 10f;
                leafPackTip = false;
                crownStackOnLeader = true;
                crownMassCount = 3;
                crownSize = new Vector2(0.84f, 0.22f);
                // leafSize stays the grow tuft (0.06×0.10). Envelope
                // 84×22 is hidden; PaintPineCrownCone owns the fir.
                break;
            case TreeKind.Willow:
                barkPlateCount = 2;
                barkLook = BarkLook.Strip;
                leafLobes = 1;
                crownLobes = 1;
                leafHangWorldDown = true;
                leafPackTip = false;
                leafTilt = 6f;
                crownMassCount = 2;
                crownSize = new Vector2(0.11f, 0.64f);
                break;
            case TreeKind.Bush:
                barkPlateCount = 3;
                barkLook = BarkLook.Furrow;
                leafLobes = 1;
                leafTilt = 12f;
                leafPackTip = false;
                crownLobes = 2;
                crownMassCount = 2;
                crownSize = new Vector2(0.44f, 0.22f);
                break;
            case TreeKind.Poplar:
                // Three marks. Six 5% dashes were a zipper on the 15 cm
                // column, not birch lenticels.
                barkPlateCount = 3;
                barkLook = BarkLook.Lenticel;
                leafLobes = 1;
                leafTilt = 18f;
                leafPackTip = false;
                crownLobes = 1;
                crownMassCount = 2;
                crownSize = new Vector2(0.15f, 0.54f);
                palBarkLite = new Color(0.78f, 0.72f, 0.58f, 1f);
                palBarkDark = new Color(0.28f, 0.22f, 0.16f, 1f);
                break;
            case TreeKind.Apple:
                barkPlateCount = 4;
                barkLook = BarkLook.Plate;
                leafLobes = 1;
                // Three lobes, not the bush/hazel mound pair: orchard
                // is a round cloud at 1x, not a two-hump hedge.
                crownLobes = 3;
                crownMassCount = 2;
                crownSize = new Vector2(0.48f, 0.32f);
                // Russet flakes, not generic oak brown on a fruit bole.
                palBarkLite = new Color(0.62f, 0.42f, 0.22f, 1f);
                palBarkDark = new Color(0.28f, 0.14f, 0.08f, 1f);
                break;
            case TreeKind.Lilac:
                barkPlateCount = 2;
                // Green cane, not oak furrows: internodal rings on the
                // lilac stem so a still frame is a Y-shrub, not a tiny oak.
                barkLook = BarkLook.Strip;
                crownMassCount = 0;
                crownLobes = 1;
                break;
            case TreeKind.Hazel:
                barkPlateCount = 3;
                barkLook = BarkLook.Furrow;
                leafLobes = 1;
                crownLobes = 2;
                crownMassCount = 2;
                crownSize = new Vector2(0.24f, 0.16f);
                break;
            case TreeKind.Cedar:
                barkPlateCount = 3;
                barkLook = BarkLook.Scale;
                leafLobes = 1;
                crownLobes = 1;
                leafTilt = 4f;
                leafPackTip = false;
                crownStackOnLeader = true;
                crownMassCount = 3;
                // Envelope only. The 94×11 cm slab hid behind fans;
                // extras paint layered tables, not a pine chevron.
                crownSize = new Vector2(0.94f, 0.11f);
                leafSize = new Vector2(0.16f, 0.040f);
                break;
            case TreeKind.Plum:
                barkPlateCount = 4;
                barkLook = BarkLook.Plate;
                leafLobes = 1;
                crownLobes = 2;
                crownMassCount = 2;
                crownSize = new Vector2(0.34f, 0.24f);
                palBarkLite = new Color(0.48f, 0.22f, 0.28f, 1f);
                palBarkDark = new Color(0.20f, 0.08f, 0.12f, 1f);
                break;
            default:
                leafTilt = 42f;
                barkLook = BarkLook.Furrow;
                crownSize = new Vector2(0.50f, 0.34f);
                // Two discrete bark colours, not a gradient. Stronger
                // split so the cone ridges read at 1x against the bole.
                palBarkDark = new Color(
                    palStump.r * 0.40f, palStump.g * 0.38f, palStump.b * 0.36f, 1f);
                palBarkLite = new Color(
                    Mathf.Min(1f, palTrunk.r * 1.40f),
                    Mathf.Min(1f, palTrunk.g * 1.22f),
                    Mathf.Min(1f, palTrunk.b * 1.12f),
                    1f);
                break;
        }

        // Poplar silver / orchard russet / plum mauve are hardcoded
        // after ApplySeason. Winter palTrunk is already grey, so the
        // bole went wood-grey with a summer flake (flag analog on the
        // bark). Oak already derives plates from palStump. Picture
        // only — fork 28° / visualTaper 0.58 stay.
        bool kindPlates = ResolvedKind == TreeKind.Poplar
            || ResolvedKind == TreeKind.Apple
            || ResolvedKind == TreeKind.Plum;
        if (kindPlates && ResolvedSeason == PlantSeason.Winter)
        {
            palBarkLite = Color.Lerp(palBarkLite, palTrunk, 0.52f);
            palBarkDark = Color.Lerp(palBarkDark, palStump, 0.38f);
        }
        else if (kindPlates && ResolvedSeason == PlantSeason.Autumn)
        {
            palBarkLite = Color.Lerp(palBarkLite, palTrunk, 0.18f);
            palBarkDark = Color.Lerp(palBarkDark, palStump, 0.12f);
        }

        winterWoodyCrown = false;
        if (!growLeaves)
        {
            // Bare wood still needs a kind silhouette at 1x. Dropping the
            // cloud left oak/willow/poplar/orchard as the same stick plus
            // two bud dashes. Lilac is a Y-cane (crown already 0). Pine
            // and cedar keep growLeaves. Picture only — colliders stay.
            if (ResolvedKind != TreeKind.Lilac && crownMassCount > 0)
            {
                winterWoodyCrown = true;
                crownSize = new Vector2(crownSize.x * 0.70f, crownSize.y * 0.76f);
            }
            else
                crownMassCount = 0;
        }
        else if (ResolvedSeason == PlantSeason.Spring
            && ResolvedKind != TreeKind.Pine
            && ResolvedKind != TreeKind.Cedar)
        {
            // Picture only. Spring flush is smaller than the summer leaf.
            // Leaflet shrink alone left the crown envelope as a summer
            // cloud with lime tint. Colliders / fork 28° stay.
            leafSize = new Vector2(leafSize.x * 0.86f, leafSize.y * 0.86f);
            crownSize = new Vector2(crownSize.x * 0.82f, crownSize.y * 0.86f);
        }
        else if (ResolvedSeason == PlantSeason.Autumn
            && ResolvedKind != TreeKind.Pine
            && ResolvedKind != TreeKind.Cedar)
        {
            // Oak summer tuft is 3. Max(3, n-2) left autumn as a full
            // summer pack. One fewer leaflet is a thinner crown, not a
            // bare twig. Willow 5 still drops two. Colliders stay.
            int drop = leafSpriteCount >= 5 ? 2 : 1;
            leafSpriteCount = Mathf.Max(2, leafSpriteCount - drop);
            // Lobe count stays so oak palmate sinus (artist) is not a
            // one-mass potato. The envelope shrinks; rust is not a
            // full summer cloud.
            crownSize = new Vector2(crownSize.x * 0.78f, crownSize.y * 0.84f);
            // Spent hang. +22 left oak autumn still summer-ish at
            // ortho 5 beside hang pad 0.50 / bark 0.03; +24 reads
            // spent without a hinge. Willow already drips world-down.
            // Pine / cedar keep needle tilt. Picture only — oak
            // fork 28° stays.
            if (!leafHangWorldDown)
                leafTilt = Mathf.Min(62f, leafTilt + 24f);
        }

        SnapDeciduousWoodToBark();
    }

    // ApplyFlatBark painted willow / bush / poplar / apple palTwig lime
    // so WoodColor(depth 2) grew a green stick. Crown masses already
    // bake palBarkLite. Lilac summer is a green Y-cane — that IS the
    // silhouette. Pine / cedar keep needle-green on inner wood.
    // Picture only — oak fork 28° / visualTaper 0.58 stay.
    private void SnapDeciduousWoodToBark()
    {
        if (ResolvedKind == TreeKind.Pine || ResolvedKind == TreeKind.Cedar)
            return;
        if (ResolvedKind == TreeKind.Lilac && ResolvedSeason != PlantSeason.Winter)
            return;
        palBranch = Color.Lerp(palBranch, palTrunk, 0.72f);
        palTwig = Color.Lerp(palTwig, palBarkLite, 0.50f);
        palTwig = Color.Lerp(palTwig, palTrunk, 0.40f);
        palTip = Color.Lerp(palTip, palTwig, 0.45f);
    }

    // After the shared 0.62 lime. Autumn already walks a family;
    // spring without this left oak/poplar/willow as one flush card.
    // Picture only — fork 28° / visualTaper 0.58 stay.
    private void ApplySpringLeafKind()
    {
        switch (ResolvedKind)
        {
            case TreeKind.Poplar:
                palLeafNear = Color.Lerp(palLeafNear, new Color(0.70f, 0.84f, 0.22f, 1f), 0.35f);
                palLeafFar = Color.Lerp(palLeafFar, new Color(0.52f, 0.68f, 0.16f, 1f), 0.28f);
                break;
            case TreeKind.Willow:
                palLeafNear = Color.Lerp(palLeafNear, new Color(0.62f, 0.76f, 0.18f, 1f), 0.28f);
                palLeafFar = Color.Lerp(palLeafFar, new Color(0.50f, 0.62f, 0.14f, 1f), 0.22f);
                break;
            case TreeKind.Hazel:
                palLeafNear = Color.Lerp(palLeafNear, new Color(0.58f, 0.80f, 0.24f, 1f), 0.22f);
                break;
            case TreeKind.Apple:
            case TreeKind.Plum:
                palLeafNear = Color.Lerp(palLeafNear, new Color(0.40f, 0.70f, 0.22f, 1f), 0.20f);
                palLeafFar = Color.Lerp(palLeafFar, new Color(0.32f, 0.56f, 0.16f, 1f), 0.18f);
                break;
            case TreeKind.Bush:
                palLeafNear = Color.Lerp(palLeafNear, new Color(0.48f, 0.74f, 0.22f, 1f), 0.18f);
                break;
            case TreeKind.Lilac:
                // Bloom-only Y-cane. Leaf tint is unused; do not walk
                // the oak lime onto palLeafNear for stand JSON.
                break;
            default:
                // Shared 0.62 lime plus the cases above left canon oak
                // as the leftover flush card (plant 16:40 walked every
                // other deciduous). A cooler lime is oak, not bush.
                // Picture only — fork 28° stays.
                palLeafNear = Color.Lerp(palLeafNear, new Color(0.48f, 0.76f, 0.20f, 1f), 0.16f);
                palLeafFar = Color.Lerp(palLeafFar, new Color(0.36f, 0.58f, 0.14f, 1f), 0.14f);
                break;
        }
    }

    private void ApplyFlatBark(TreeKind recipe)
    {
        useKindPalette = true;
        palBloom = new Color(0.86f, 0.62f, 0.78f, 1f);
        palFruit = new Color(0.78f, 0.16f, 0.14f, 1f);
        leafShape = HumanVisualShape.Box;
        // Oak starts at 2. A bump to 5 packed one origin into a star
        // (five lobed blades + spray). Three is a tuft, not a comb.
        if (leafSpriteCount < 3)
            leafSpriteCount = 3;

        switch (recipe)
        {
            case TreeKind.Pine:
                palStump = new Color(0.32f, 0.22f, 0.14f, 1f);
                palTrunk = new Color(0.40f, 0.28f, 0.16f, 1f);
                palBranch = new Color(0.34f, 0.38f, 0.18f, 1f);
                palTwig = new Color(0.26f, 0.42f, 0.20f, 1f);
                palTip = new Color(0.20f, 0.40f, 0.18f, 1f);
                palLeafNear = new Color(0.16f, 0.38f, 0.22f, 1f);
                palLeafFar = new Color(0.12f, 0.30f, 0.18f, 1f);
                break;
            case TreeKind.Willow:
                palStump = new Color(0.36f, 0.28f, 0.14f, 1f);
                palTrunk = new Color(0.44f, 0.36f, 0.16f, 1f);
                palBranch = new Color(0.46f, 0.42f, 0.16f, 1f);
                palTwig = new Color(0.50f, 0.50f, 0.18f, 1f);
                palTip = new Color(0.52f, 0.54f, 0.18f, 1f);
                palLeafNear = new Color(0.42f, 0.58f, 0.18f, 1f);
                palLeafFar = new Color(0.34f, 0.48f, 0.14f, 1f);
                break;
            case TreeKind.Bush:
                palStump = new Color(0.26f, 0.16f, 0.08f, 1f);
                palTrunk = new Color(0.34f, 0.22f, 0.10f, 1f);
                palBranch = new Color(0.30f, 0.40f, 0.14f, 1f);
                palTwig = new Color(0.28f, 0.48f, 0.16f, 1f);
                palTip = new Color(0.26f, 0.50f, 0.16f, 1f);
                palLeafNear = new Color(0.24f, 0.52f, 0.16f, 1f);
                palLeafFar = new Color(0.18f, 0.40f, 0.12f, 1f);
                break;
            case TreeKind.Poplar:
                palStump = new Color(0.42f, 0.34f, 0.22f, 1f);
                palTrunk = new Color(0.52f, 0.42f, 0.26f, 1f);
                palBranch = new Color(0.48f, 0.50f, 0.24f, 1f);
                palTwig = new Color(0.40f, 0.54f, 0.22f, 1f);
                palTip = new Color(0.36f, 0.56f, 0.20f, 1f);
                palLeafNear = new Color(0.40f, 0.62f, 0.28f, 1f);
                palLeafFar = new Color(0.56f, 0.60f, 0.48f, 1f);
                break;
            case TreeKind.Apple:
                palStump = new Color(0.30f, 0.16f, 0.10f, 1f);
                palTrunk = new Color(0.40f, 0.20f, 0.12f, 1f);
                palBranch = new Color(0.36f, 0.32f, 0.12f, 1f);
                palTwig = new Color(0.32f, 0.42f, 0.14f, 1f);
                palTip = new Color(0.30f, 0.46f, 0.16f, 1f);
                palLeafNear = new Color(0.28f, 0.54f, 0.18f, 1f);
                palLeafFar = new Color(0.22f, 0.42f, 0.14f, 1f);
                palFruit = new Color(0.82f, 0.18f, 0.12f, 1f);
                palBloom = new Color(0.92f, 0.68f, 0.78f, 1f);
                break;
            default:
                palStump = new Color(0.28f, 0.16f, 0.08f, 1f);
                palTrunk = new Color(0.38f, 0.22f, 0.10f, 1f);
                palBranch = new Color(0.42f, 0.26f, 0.12f, 1f);
                palTwig = new Color(0.46f, 0.30f, 0.14f, 1f);
                palTip = new Color(0.50f, 0.34f, 0.16f, 1f);
                palLeafNear = new Color(0.22f, 0.50f, 0.16f, 1f);
                palLeafFar = new Color(0.16f, 0.38f, 0.12f, 1f);
                break;
        }
    }

    private void ApplySeason()
    {
        ResolvedSeason = season;
        if (ResolvedSeason == PlantSeason.Auto)
            ResolvedSeason = (PlantSeason)((Mathf.Abs(seed) % 4) + 1);

        bool evergreen = ResolvedKind == TreeKind.Pine || ResolvedKind == TreeKind.Cedar;
        switch (ResolvedSeason)
        {
            case PlantSeason.Spring:
                palLeafNear = Color.Lerp(palLeafNear, new Color(0.55f, 0.78f, 0.28f, 1f), 0.62f);
                palLeafFar = Color.Lerp(palLeafFar, new Color(0.42f, 0.64f, 0.20f, 1f), 0.62f);
                palBloom = Color.Lerp(palBloom, new Color(0.92f, 0.62f, 0.78f, 1f), 0.40f);
                // Shared lime left every deciduous as the same flush
                // card (autumn family analog). Kind walks the lime so
                // stand JSON treeLeafTint* is not one spring. Bark
                // stays the recipe — moss on palTrunk is closed.
                if (!evergreen)
                    ApplySpringLeafKind();
                // Flush is leaves and blossom. A 0.10 green lerp on
                // palTrunk painted the bole as moss — season colour
                // must not recast bark. Colliders stay.
                // Deciduous kinds that do not already bloom (lilac) get a
                // spring flower packet. Picture only — not a perch.
                // Orchard keeps a short cluster; oak/willow/bush get one
                // axillary bud so the packed tuft is not a second star.
                if (!evergreen && !growBloom)
                {
                    growBloom = true;
                    seasonAddedBloom = true;
                    bool orchard = ResolvedKind == TreeKind.Apple
                        || ResolvedKind == TreeKind.Plum;
                    bloomClusterCount = orchard ? 3 : 1;
                    bloomSize = new Vector2(0.045f, 0.040f);
                    bloomShape = HumanVisualShape.Box;
                }
                break;
            case PlantSeason.Autumn:
                if (evergreen)
                {
                    palLeafNear = new Color(0.28f, 0.38f, 0.18f, 1f);
                    palLeafFar = new Color(0.22f, 0.30f, 0.14f, 1f);
                }
                else
                {
                    // Family first, then LeafTint walks yellow→orange→red
                    // inside that family so an oak is not a poplar.
                    switch (ResolvedKind)
                    {
                        case TreeKind.Poplar:
                            // Spent gold at ortho 5 after TuftFill —
                            // 0.64/0.40 still bright beside bark 0.04;
                            // deepen spent straw so the flame reads
                            // spent at 1x.
                            palLeafNear = new Color(0.58f, 0.34f, 0.03f, 1f);
                            palLeafFar = new Color(0.34f, 0.14f, 0.02f, 1f);
                            break;
                        case TreeKind.Willow:
                            // Spent straw at ortho 5 after Tip sickle —
                            // 0.34/0.12 still gold on hang beside bark
                            // 0.04; deepen spent.
                            palLeafNear = new Color(0.28f, 0.08f, 0.02f, 1f);
                            palLeafFar = new Color(0.14f, 0.03f, 0.01f, 1f);
                            break;
                        case TreeKind.Apple:
                        case TreeKind.Plum:
                            palLeafNear = new Color(0.72f, 0.18f, 0.10f, 1f);
                            palLeafFar = new Color(0.42f, 0.08f, 0.08f, 1f);
                            break;
                        case TreeKind.Bush:
                            palLeafNear = new Color(0.70f, 0.32f, 0.10f, 1f);
                            palLeafFar = new Color(0.48f, 0.16f, 0.08f, 1f);
                            break;
                        case TreeKind.Hazel:
                            palLeafNear = new Color(0.88f, 0.72f, 0.18f, 1f);
                            palLeafFar = new Color(0.64f, 0.42f, 0.08f, 1f);
                            break;
                        default:
                            // Oak canon. 0.32/0.06 still orange beside
                            // bark 0.04 / TuftFill; deepen spent
                            // russet so the thinner autumn crown reads
                            // at 1x beside hang pad 0.50.
                            palLeafNear = new Color(0.26f, 0.04f, 0.02f, 1f);
                            palLeafFar = new Color(0.10f, 0.02f, 0.01f, 1f);
                            break;
                    }
                }
                palFruit = Color.Lerp(palFruit, new Color(0.72f, 0.12f, 0.08f, 1f), 0.40f);
                palStump = Color.Lerp(palStump, new Color(0.34f, 0.14f, 0.06f, 1f), 0.22f);
                palTrunk = Color.Lerp(palTrunk, new Color(0.48f, 0.22f, 0.08f, 1f), 0.18f);
                palBranch = Color.Lerp(palBranch, palTrunk, 0.12f);
                palTwig = Color.Lerp(palTwig, new Color(0.52f, 0.30f, 0.10f, 1f), 0.16f);
                palTip = Color.Lerp(palTip, new Color(0.54f, 0.34f, 0.12f, 1f), 0.12f);
                // Lilac has no leaf mass. Generic rust above turned the
                // green Y-cane into a tiny oak; straw spent racemes keep
                // autumn readable on the stem.
                if (ResolvedKind == TreeKind.Lilac)
                {
                    palTrunk = Color.Lerp(palTrunk, new Color(0.46f, 0.44f, 0.16f, 1f), 0.42f);
                    palBranch = Color.Lerp(palBranch, palTrunk, 0.18f);
                    palTwig = Color.Lerp(palTwig, new Color(0.50f, 0.48f, 0.18f, 1f), 0.32f);
                    palBloom = Color.Lerp(palBloom, new Color(0.46f, 0.30f, 0.28f, 1f), 0.50f);
                }
                break;
            case PlantSeason.Winter:
                palStump = Color.Lerp(palStump, new Color(0.22f, 0.18f, 0.14f, 1f), 0.35f);
                palTrunk = Color.Lerp(palTrunk, new Color(0.30f, 0.24f, 0.18f, 1f), 0.35f);
                palBranch = Color.Lerp(palBranch, palTrunk, 0.25f);
                palTwig = Color.Lerp(palTwig, palTrunk, 0.35f);
                palTip = Color.Lerp(palTip, palTrunk, 0.45f);
                if (evergreen)
                {
                    palLeafNear = new Color(0.16f, 0.32f, 0.22f, 1f);
                    palLeafFar = new Color(0.12f, 0.26f, 0.18f, 1f);
                }
                else
                {
                    growLeaves = false;
                    // Bare wood. Hazel keeps lamb's tails (late winter);
                    // lilac raceme is spent.
                    growBloom = ResolvedKind == TreeKind.Hazel;
                    // Artist winter cloud is bark. Leaving palLeafNear on
                    // the summer recipe made stand JSON a green leafless
                    // oak. Twig / spent-bud greys match the picture.
                    palLeafNear = new Color(0.34f, 0.30f, 0.22f, 1f);
                    palLeafFar = new Color(0.24f, 0.20f, 0.16f, 1f);
                }
                // Kind palette is a green Y-cane. 0.35 toward brown left
                // winter lilac as a summer shrub. Wood grey is the
                // leafless shoot; raceme is spent. Not a perch.
                if (ResolvedKind == TreeKind.Lilac)
                {
                    palStump = Color.Lerp(palStump, new Color(0.24f, 0.20f, 0.14f, 1f), 0.55f);
                    palTrunk = Color.Lerp(palTrunk, new Color(0.30f, 0.26f, 0.16f, 1f), 0.72f);
                    palBranch = Color.Lerp(palBranch, palTrunk, 0.22f);
                    palTwig = Color.Lerp(palTwig, palTrunk, 0.30f);
                    palTip = Color.Lerp(palTip, palTrunk, 0.38f);
                    palBloom = Color.Lerp(palBloom, new Color(0.42f, 0.36f, 0.28f, 1f), 0.65f);
                }
                break;
        }

        // Hazel catkins belong on bare / early wood. Leaf-out hid them
        // under yellow packets on green (lilac-bloom analog).
        if (ResolvedKind == TreeKind.Hazel
            && (ResolvedSeason == PlantSeason.Summer || ResolvedSeason == PlantSeason.Autumn))
            growBloom = false;

        // Orchard calendar. ApplyKind turns fruit on for Apple / Plum;
        // spring already added blossom. Fruit on that flush is a
        // decorated stick. Winter baubles on bare wood are the same
        // hole. Summer and autumn keep the crop. Not a perch.
        if (ResolvedKind == TreeKind.Apple || ResolvedKind == TreeKind.Plum)
        {
            if (ResolvedSeason == PlantSeason.Spring
                || ResolvedSeason == PlantSeason.Winter)
                growFruit = false;
        }
    }

    // Sail: ½ ρ v² S, only in FixedUpdate through TreeDriver.
    public void ApplyWind()
    {
        if (rig != TreeRig.Sway) return;
        if (windScale <= 0f) return;

        float omega = windPeriod > 0.05f ? (2f * Mathf.PI / windPeriod) : 0f;
        // FixedUpdate and LateUpdate share WindClock (fixedTime). Time.time
        // is the frame clock: the same Extra wind wandered, and the leaf
        // picture drifted off the wood. Hold (-treeWind 0) still bails.
        float t = WindClock();
        float q = 0.5f * AirDensity * windScale;

        for (int i = 0; i < nodes.Count; i++)
        {
            Node n = nodes[i];
            if (!n.dynamic || n.segment == null || n.segment.rb == null)
                continue;
            // Tips lag the bole so a gust reads as a canopy, not a pole.
            // Seed offsets the grove so two Sway trees are not a metronome.
            // Bole amplitude is quiet: the fat trunk must not paddle.
            // Same ½ρv²; no up force. windScale 0 still bails above.
            float gust = 1f;
            if (omega > 0f)
            {
                float depth01 = growDepthCap <= 0
                    ? 1f
                    : Mathf.Clamp01(n.depth / (float)growDepthCap);
                gust += windGust * Mathf.Sin(
                    t * omega + n.depth * 0.62f + seed * 0.17f)
                    * Mathf.Lerp(0.32f, 1f, depth01);
            }
            float v = windSpeed * gust;
            if (Mathf.Abs(v) < 0.01f)
                continue;
            float area = n.segment.size.x * n.segment.size.y;
            // Collider is the fat grow box. Sail follows the picture
            // cone so a tapered bole is not a fence-post paddle. No
            // alloc; WoodBase/Tip are arithmetic. Not an up force.
            float pic = 0.5f * (WoodBaseScale(n.depth) + WoodTipScale(n.depth));
            if (pic < 0.995f)
                area *= Mathf.Clamp(pic, 0.45f, 1f);
            n.segment.rb.AddForce(
                new Vector2(
                    Mathf.Sign(v) * q * v * Mathf.Abs(v) * area
                    * WindSail(n.depth) * HeadingSailMul(LiveWoodHeading(n)),
                    0f),
                ForceMode2D.Force);
        }
    }

    // Depth 0 = bole (small). Twigs take leaf area on top of wood.
    // Winter deciduous drops growLeaves; a woody crown still takes
    // 0.70 of the wood sail (not a second half). Bloom-only kinds
    // (lilac raceme, hazel catkin) still catch sail — growLeaves false
    // must not treat a flower spike as bare wood.
    // Evergreen winter keeps needles. Kind scales the canopy: a pine
    // is sparse, a willow curtain is a sail, oak stays 1. Oak fork
    // 28° and windScale 0.12 stay. No alloc, no Find.
    private float WindSail(int depth)
    {
        if (depth <= 0)
            return 0.18f;
        float sail = 0.70f + 0.55f * depth;
        if (growLeaves)
            sail += depth >= 2 ? 0.85f : 0.25f;
        else if (growBloom)
            sail += depth >= 2 ? 0.50f : 0.12f;
        else if (winterWoodyCrown)
            // Bare deciduous still has a twig cloud. Halving the wood
            // sail twice (no leaves AND no cloud) left winter Sway as
            // a pole. Hold (-treeWind 0) still bails. Oak fork 28°.
            sail *= 0.70f;
        else
            sail *= 0.50f;
        switch (ResolvedKind)
        {
            case TreeKind.Pine:
                // Sparse needles, not a leaf cloud.
                sail *= 0.78f;
                break;
            case TreeKind.Cedar:
                // Layered tables catch the gust; the bole is still a
                // cone, not a fence-post paddle. Pine 0.78 must not
                // starve these fans. Oak fork 28° / windScale 0.12 stay.
                sail *= depth >= 2 ? 1.10f : 0.82f;
                break;
            case TreeKind.Poplar:
                sail *= 0.88f;
                break;
            case TreeKind.Willow:
                if (depth >= 2)
                    sail *= 1.12f;
                break;
            case TreeKind.Bush:
                sail *= 0.70f;
                break;
            case TreeKind.Hazel:
                sail *= 0.82f;
                break;
            case TreeKind.Lilac:
                sail *= 0.92f;
                break;
            case TreeKind.Apple:
            case TreeKind.Plum:
                sail *= 1.05f;
                break;
        }
        return sail * SeasonSailMul();
    }

    // Horizontal sail grows with |sin heading|: a ±fork limb is more
    // of a plate to +X wind than the leader. Depth sail still owns the
    // bole quiet / tip cloud split. No alloc. windScale 0.12 stays.
    private static float HeadingSailMul(float headingDeg)
    {
        float lean01 = Mathf.Abs(Mathf.Sin(headingDeg * Mathf.Deg2Rad));
        return Mathf.Lerp(1f, 1.22f, lean01);
    }

    // Hung / wide crown plate vs the wood axis. Leader heading is
    // ~8° of fork 28, so HeadingSailMul(wood) left the dome as pole
    // flutter (pine-hat analog). Picture only — windScale 0.12 stays.
    private static float PicturePlateSail(
        float woodHeadingDeg,
        float hangX,
        float alongY,
        float plateHalfX)
    {
        float plateX = Mathf.Max(Mathf.Abs(hangX), plateHalfX);
        float sailLean = woodHeadingDeg;
        if (plateX > 0.001f)
        {
            float plateSign = hangX < 0f ? -1f : 1f;
            sailLean += plateSign * Mathf.Atan2(
                plateX,
                Mathf.Max(0.02f, Mathf.Abs(alongY))) * Mathf.Rad2Deg;
        }
        return HeadingSailMul(sailLean);
    }

    // Hang pad vs wood width. Summer nodal 0.28 / willow 0.36 stay.
    // Spring flush sits tight; autumn spent tufts droop off the stick
    // (leafTilt +10 analog). Pine / cedar keep the summer pad.
    // Picture only — fork 28 / visualTaper 0.58 stay.
    private float ClusterHangMul()
    {
        bool evergreen = ResolvedKind == TreeKind.Pine
            || ResolvedKind == TreeKind.Cedar;
        if (leafHangWorldDown)
        {
            if (!evergreen && ResolvedSeason == PlantSeason.Autumn)
                return 0.50f;
            if (!evergreen && ResolvedSeason == PlantSeason.Spring)
                return 0.30f;
            return 0.36f;
        }
        if (evergreen)
            return 0.28f;
        if (ResolvedSeason == PlantSeason.Spring)
            return 0.22f;
        if (ResolvedSeason == PlantSeason.Autumn)
            // 0.46 still summer-tight beside leafTilt +16 at ortho 5.
            // Spent pad matches willow autumn hang; evergreen 0.28.
            // fork 28 stays.
            return 0.50f;
        return 0.28f;
    }

    // Headless without -visual still sees the tuft as a plate. Wood
    // heading ~8° of fork 28 left JSON at pole sail 1. No alloc.
    private float ClusterPlateSail(
        HumanSegment twig,
        bool farSide,
        int depth,
        float heading,
        int clusters)
    {
        if (twig == null)
            return 1f;
        float along;
        if (leafHangWorldDown)
            along = Mathf.Lerp(0.22f, 0.86f, clusters <= 1 ? 0.55f : 0f);
        else if (leafPackTip)
            along = 0.84f;
        else
            along = Mathf.Lerp(0.42f, 0.88f, clusters <= 1 ? 0.72f : 0f);
        float y = (along - 0.5f) * twig.size.y;
        float wAt = PictureWidthAt(twig.size.x, along, depth);
        float side0 = farSide ? -1f : 1f;
        float hangMul = ClusterHangMul();
        float clusterHang;
        if (leafHangWorldDown)
        {
            Vector3 downDir = twig.transform.InverseTransformDirection(Vector3.down);
            clusterHang = downDir.x * wAt * hangMul;
        }
        else
            clusterHang = side0 * wAt * hangMul;
        return PicturePlateSail(heading, clusterHang, y, leafSize.x * 0.35f);
    }

    // Lilac raceme hub sat at 0.04 of wood width — still the stick
    // (cluster-hub analog after Wrist left the sinus). Same hang pad
    // as a nodal tuft (ClusterHangMul). Headless without -visual still
    // Notes the plate. bloomSize / fork 28° stay.
    private float LilacRacemePlateSail(
        HumanSegment twig,
        bool farSide,
        int depth,
        float heading,
        out float hang,
        out float y)
    {
        hang = 0f;
        y = 0f;
        if (twig == null)
            return 1f;
        float wAt = PictureWidthAt(twig.size.x, 0.70f, depth);
        hang = (farSide ? -1f : 1f) * wAt * ClusterHangMul();
        y = twig.size.y * 0.16f;
        return PicturePlateSail(heading, hang, y, bloomSize.x * 0.35f);
    }

    private float LilacRacemePlateSail(
        HumanSegment twig, bool farSide, int depth, float heading)
    {
        return LilacRacemePlateSail(twig, farSide, depth, heading, out _, out _);
    }

    // Hazel catkin hub at 0.12 of tip width was a tick on the twig.
    // Same 0.28 pad; lamb's tail hangs world-down from there.
    private float HazelCatkinPlateSail(
        HumanSegment twig,
        bool farSide,
        int depth,
        float heading,
        out float hang,
        out float y)
    {
        hang = 0f;
        y = 0f;
        if (twig == null)
            return 1f;
        float tipW = PictureWidthAt(twig.size.x, 0.88f, depth);
        hang = (farSide ? -1f : 1f) * tipW * ClusterHangMul();
        y = twig.size.y * 0.42f;
        return PicturePlateSail(heading, hang, y, bloomSize.x * 0.35f);
    }

    private float HazelCatkinPlateSail(
        HumanSegment twig, bool farSide, int depth, float heading)
    {
        return HazelCatkinPlateSail(twig, farSide, depth, heading, out _, out _);
    }

    private void NoteCrownHangSail(float sail)
    {
        if (sail > leafCrownHangSail)
            leafCrownHangSail = sail;
    }

    private void NoteClusterHangSail(float sail)
    {
        if (sail > leafClusterHangSail)
            leafClusterHangSail = sail;
    }

    // ½ρv² / wake use the live wood angle, not grow rest. A limb that
    // already leaned is more of a plate; rest heading left it a pole
    // sail after the first gust. Hold (-treeWind 0) still bails. No alloc.
    private static float LiveWoodHeading(Node n)
    {
        if (n.segment != null && n.segment.rb != null)
            return n.segment.rb.rotation;
        if (n.segment != null)
            return n.segment.transform.eulerAngles.z;
        return n.heading;
    }

    // Hub rest so a palmate on a spread limb sits world-up, not as a
    // green flag along the stick. Crown masses already flatten 0.70;
    // 0.55 left 45% of the heading on the tuft (pole analog). Needles /
    // Lombardy / willow hang skip. An 8° deadzone skipped the oak
    // leader (~7.8° of fork 28). SpreadLimbDeg matches Grow.
    // Picture only — oak fork 28° stays.
    private float LeafClusterRest(float headingDeg)
    {
        if (leafHangWorldDown)
            return 0f;
        if (barkLook == BarkLook.Scale)
            return 0f;
        if (ResolvedKind == TreeKind.Poplar)
            return 0f;
        if (Mathf.Abs(headingDeg) <= SpreadLimbDeg)
            return 0f;
        return -headingDeg * PalmateTrack;
    }

    // Kind default for stand JSON. Per-sprite names can still hang (fruit)
    // or glue (spur). Grow-time only.
    private float KindPictureTrack()
    {
        if (leafHangWorldDown)
            return 1f;
        if (barkLook == BarkLook.Scale || ResolvedKind == TreeKind.Poplar)
            return 0f;
        return PalmateTrack;
    }

    // Canopy peels extra wood bend so a tuft is not a flag. Fruit and
    // willow hang keep world-down. Wood spur / fork stay glued. Name
    // walk is Bind only — LateUpdate uses the cached float.
    private float PictureTrackFromName(string n)
    {
        if (string.IsNullOrEmpty(n))
            return 0f;
        if (n.IndexOf("Spur", System.StringComparison.Ordinal) >= 0
            || n.IndexOf("Fork", System.StringComparison.Ordinal) >= 0)
            return 0f;
        bool hangPart = n.IndexOf("Fruit", System.StringComparison.Ordinal) >= 0
            || n.IndexOf("Catkin", System.StringComparison.Ordinal) >= 0;
        bool canopy = n.IndexOf("Leaf", System.StringComparison.Ordinal) >= 0
            || n.IndexOf("Crown", System.StringComparison.Ordinal) >= 0
            || n.IndexOf("Bloom", System.StringComparison.Ordinal) >= 0
            || n.IndexOf("Spray", System.StringComparison.Ordinal) >= 0
            || n.IndexOf("Raceme", System.StringComparison.Ordinal) >= 0
            || n.IndexOf("SpringBud", System.StringComparison.Ordinal) >= 0;
        if (hangPart)
            return 1f;
        if (!canopy)
            return 0f;
        return KindPictureTrack();
    }

    // Parent wood node of a picture child. Bind only, no LateUpdate Find.
    private int FindWoodNodeIndex(Transform x)
    {
        Transform walk = x;
        while (!ReferenceEquals(walk, null) && !ReferenceEquals(walk, transform))
        {
            for (int nodeI = 0; nodeI < nodes.Count; nodeI++)
            {
                HumanSegment wood = nodes[nodeI].segment;
                if (wood != null && ReferenceEquals(wood.transform, walk))
                    return nodeI;
            }
            walk = walk.parent;
        }
        return -1;
    }

    // Picture and ½ρv² share this. Winter deciduous already halves
    // via growLeaves false — do not stack. Hold (-treeWind 0) bails
    // before ApplyWind. Oak fork 28° / windScale 0.12 stay.
    private float SeasonSailMul()
    {
        bool evergreen = ResolvedKind == TreeKind.Pine || ResolvedKind == TreeKind.Cedar;
        if (ResolvedSeason == PlantSeason.Spring && !evergreen)
            return 0.88f;
        if (ResolvedSeason == PlantSeason.Autumn)
            // Deciduous spent crown still catches more sail than a
            // summer green cloud at the same Extra wind. Evergreen
            // needles stay quieter. windScale 0.12 / fork 28 stay.
            return evergreen ? 0.95f : 1.18f;
        if (ResolvedSeason == PlantSeason.Winter && evergreen)
            return 0.90f;
        return 1f;
    }

    // Look / leaf LateUpdate. A private 0.90 / 1.18 / 0.80 table left
    // the picture off the ½ρv² season sail after HeadingSailMul landed.
    // Winter deciduous woody cloud keeps 0.80 — force already *0.70.
    private float PictureSeasonAmp()
    {
        float amp = SeasonSailMul();
        bool evergreen = ResolvedKind == TreeKind.Pine || ResolvedKind == TreeKind.Cedar;
        if (!evergreen && ResolvedSeason == PlantSeason.Winter)
            amp *= 0.80f;
        return amp;
    }

    // Local horizontal gust from a flying bird. Wood is ghost to the
    // bird, so a fly-by never hits a collider; this is the reaction.
    // Same sign as wind (along X), not an up force. Sit / Walk skipped.
    public void ApplyBirdWake()
    {
        if (rig != TreeRig.Sway) return;
        if (birdWakeForce <= 0f || birdWakeRange <= 0f) return;
        // Headless hold has no birds. One empty Find is enough — do not
        // scan every 0.25 s at 200 Hz.
        if (HeadlessTrial.Active && wakeBirds.Length == 0 && wakeBirdsAt > 0f)
            return;

        RefreshWakeBirds();
        if (wakeBirds.Length == 0) return;

        for (int i = 0; i < nodes.Count; i++)
        {
            Node n = nodes[i];
            if (!n.dynamic || n.depth < 1 || n.segment == null || n.segment.rb == null)
                continue;

            float u = BirdWakeUnit(n.segment.rb.worldCenterOfMass);
            if (Mathf.Abs(u) < 1e-5f)
                continue;

            n.segment.rb.AddForce(
                new Vector2(u * birdWakeForce * HeadingSailMul(LiveWoodHeading(n)), 0f),
                ForceMode2D.Force);
        }
    }

    // Static Apple picture: wind + fly-by. Wood transforms stay so
    // BirdPerch pads do not become moving static colliders. One Human
    // of hinges in Level1 is too dear next to the flock.
    public void ApplyLookSway()
    {
        if (!HasLookSway) return;
        if (lookSprites.Length == 0 || nodes.Count == 0) return;

        float omega = windPeriod > 0.05f ? (2f * Mathf.PI / windPeriod) : 0f;
        float t = WindClock();
        float gust = omega > 0f ? Mathf.Sin(t * omega + lookPhase) : 0f;
        // Inspector default windSpeed is 2.2. Still air must not wave
        // the picture; a fly-by still bends tips. No alloc.
        float wind01 = Mathf.Clamp(Mathf.Abs(windSpeed) / 2.2f, 0f, 1.6f);
        if (lookBirdSwayDegrees > 0f)
            RefreshWakeBirds();

        for (int i = 0; i < nodes.Count; i++)
        {
            Node n = nodes[i];
            float extra = 0f;
            if (n.depth >= 0 && n.segment != null)
            {
                float amp = LookJointDegrees(n.depth);
                // Outward laterals are more horizontal: they take more of
                // the picture gust so the canopy moves, not the pole.
                // ½ρv² already uses live wood + HeadingSailMul. A private
                // 0.90…1.16 lerp left Static laterals off that sail.
                // Last lookHeading is the picture pose (Bind inits grow
                // rest). lookSwayDegrees 5.5 stays. No alloc.
                float liveH = n.heading;
                if (i < lookHeading.Length)
                    liveH = lookHeading[i];
                amp *= HeadingSailMul(liveH);
                extra = amp * gust * wind01;
                if (omega > 0f)
                    extra += amp * 0.18f * Mathf.Sin(t * omega * 1.61f + i * 0.41f + lookPhase) * wind01;
                if (n.depth >= 1)
                    extra += lookBirdSwayDegrees * PictureSeasonAmp()
                        * BirdWakeUnit(n.segment.transform.position);
            }

            if (n.parentIndex < 0)
            {
                lookHeading[i] = n.heading + extra;
                lookCenter[i] = n.segment != null ? (Vector2)n.segment.transform.localPosition : Vector2.zero;
                continue;
            }

            Node parent = nodes[n.parentIndex];
            if (n.segment == null || parent.segment == null)
            {
                lookHeading[i] = n.heading;
                lookCenter[i] = n.segment != null ? (Vector2)n.segment.transform.localPosition : Vector2.zero;
                continue;
            }
            float rel = n.heading - parent.heading;
            lookHeading[i] = lookHeading[n.parentIndex] + rel + extra;
            Vector2 parentTip = lookCenter[n.parentIndex]
                + HeadingDir(lookHeading[n.parentIndex]) * parent.segment.size.y * 0.5f;
            lookCenter[i] = parentTip + HeadingDir(lookHeading[i]) * n.segment.size.y * 0.5f;
        }

        for (int s = 0; s < lookSprites.Length; s++)
        {
            LookSprite part = lookSprites[s];
            int i = part.nodeIndex;
            if (part.xform == null || i < 0 || i >= nodes.Count || nodes[i].segment == null)
                continue;

            float growH = nodes[i].heading;
            Vector2 growC = nodes[i].segment.transform.localPosition;
            Vector2 rest = part.restLocalPos;
            Vector2 lookTree = lookCenter[i] + RotateZ(rest, lookHeading[i]);
            Vector2 local = UnrotateZ(lookTree - growC, growH);
            part.xform.localPosition = new Vector3(local.x, local.y, part.restLocalPos.z);
            // Grow rest already flattened / hung. Extra look heading
            // would re-flag the canopy (pole analog on Static Apple).
            // Peel PalmateTrack / hang 1. Wood Visual track is 0.
            float woodExtra = lookHeading[i] - growH;
            part.xform.localRotation = Quaternion.Euler(0f, 0f, woodExtra * (1f - part.track))
                * part.restLocalRot;
        }
    }

    // Picture-only leaf / crown flutter on Sway. Wood hinges already take
    // ½ρv² through windScale. Flutter follows windSpeed so a 2 m/s breeze
    // is readable; the 0.12 sail coefficient must not shrink the picture.
    public void ApplyLeafSway()
    {
        if (rig != TreeRig.Sway) return;
        if (windScale <= 0f || leafSwayDegrees <= 0f) return;
        if (leafSwayXforms.Length == 0) return;
        if (leafSwayAmp.Length != leafSwayXforms.Length) return;
        if (leafSwayPhase.Length != leafSwayXforms.Length) return;
        if (leafSwayRestPos.Length != leafSwayXforms.Length) return;
        if (leafSwayTrack.Length != leafSwayXforms.Length) return;
        if (leafSwayWood.Length != leafSwayXforms.Length) return;
        if (leafSwayStation.Length != leafSwayXforms.Length) return;
        if (leafSwayDrop.Length != leafSwayXforms.Length) return;

        float omega = windPeriod > 0.05f ? (2f * Mathf.PI / windPeriod) : 1.4f;
        float t = WindClock();
        float gust = Mathf.Sin(t * omega + seed * 0.17f);
        float rustle = Mathf.Sin(t * omega * 2.7f + seed * 0.51f);
        float speed01 = Mathf.Clamp(Mathf.Abs(windSpeed) / 3.5f, 0.25f, 1.6f);
        // Gust scales the picture so a trough is quiet and a peak is
        // readable. Wood still takes ½ρv² in FixedUpdate.
        float amp = leafSwayDegrees * speed01 * (0.55f + 0.45f * Mathf.Abs(gust));
        for (int swayI = 0; swayI < leafSwayXforms.Length; swayI++)
        {
            Transform x = leafSwayXforms[swayI];
            if (ReferenceEquals(x, null)) continue;
            // Phase bound at grow: outer leaflets lag the bole so a gust
            // is a canopy, not one metronome. No name scan, no alloc.
            float flutter = gust + 0.35f * rustle
                + 0.28f * Mathf.Sin(t * omega * 3.3f + leafSwayPhase[swayI] + seed * 0.11f);
            // ½ρv² already uses live wood. Grow rest flatten left a
            // flag once the hinge moved. Peel PalmateTrack / hang 1.
            // Amp was frozen at grow HeadingSailMul; a bent lateral
            // must catch the same extra sail as the force.
            float woodExtra = 0f;
            float leanMul = 1f;
            int woodI = leafSwayWood[swayI];
            if (woodI >= 0 && woodI < nodes.Count)
            {
                float liveH = LiveWoodHeading(nodes[woodI]);
                woodExtra = Mathf.DeltaAngle(nodes[woodI].heading, liveH);
                float growSail = HeadingSailMul(nodes[woodI].heading);
                if (growSail > 0.05f)
                    leanMul = HeadingSailMul(liveH) / growSail;
            }
            float a = flutter * amp * leafSwayAmp[swayI] * leafSwaySign[swayI] * leanMul;
            float track = leafSwayTrack[swayI];
            // Sail is world +X (½ρv²). Local XY on a ±fork limb rustled
            // along the stick after the first gust. 4.6 cm along the
            // gust, not a teleport. InverseTransformDirection, no alloc.
            float pushX = flutter * 0.046f * speed01 * leafSwayAmp[swayI] * leafSwaySign[swayI] * leanMul;
            float pushY = rustle * 0.024f * speed01 * leafSwayAmp[swayI] * leanMul;
            Vector3 restPos = leafSwayRestPos[swayI];
            if (track > 0.95f && !ReferenceEquals(x.parent, null))
            {
                Vector3 liveDown = x.parent.InverseTransformDirection(Vector3.down);
                restPos = leafSwayStation[swayI] + liveDown * leafSwayDrop[swayI];
            }
            Vector3 worldPush = new Vector3(pushX, pushY, 0f);
            Vector3 localPush = worldPush;
            if (!ReferenceEquals(x.parent, null))
                localPush = x.parent.InverseTransformDirection(worldPush);
            x.localPosition = restPos + localPush;
            x.localRotation = leafSwayRest[swayI]
                * Quaternion.Euler(0f, 0f, a - woodExtra * track);
        }
    }

    // Physics step clock. Shared by ½ρv², Static look-sway, and Sway
    // leaf flutter so a gust is one canopy. Time.time is the frame
    // clock and wandered the Extra wind trial. Hold still bails on
    // windScale 0. No alloc.
    private float WindClock()
    {
        return Time.fixedTime;
    }

    private float LookJointDegrees(int depth)
    {
        if (depth < 0 || lookSwayDegrees <= 0f)
            return 0f;
        float t = growDepthCap <= 0 ? 1f : Mathf.Clamp01(depth / (float)growDepthCap);
        float amp = Mathf.Lerp(lookSwayDegrees * lookSwayTrunkShare, lookSwayDegrees, t);
        // Same season gate as BindLeafSway / ½ρv². Picture only; hinges stay.
        amp *= PictureSeasonAmp();
        return amp;
    }

    // −1…+1, one close fly-by is the ceiling. Shared by Sway force and look bend.
    private float BirdWakeUnit(Vector2 wood)
    {
        if (wakeBirds.Length == 0 || birdWakeRange <= 0f)
            return 0f;

        float range = birdWakeRange;
        float rangeSq = range * range;
        float minSpeed = birdWakeMinSpeed;
        float refSpeedSq = BirdWakeRefSpeed * BirdWakeRefSpeed;
        float unit = 0f;

        for (int b = 0; b < wakeBirds.Length; b++)
        {
            BirdController control = wakeBirds[b];
            if (control == null || control.energyRest)
                continue;
            BirdMode mode = control.mode;
            if (mode != BirdMode.Fly && mode != BirdMode.Glide && mode != BirdMode.Attack)
                continue;
            Rigidbody2D body = control.BodyRb;
            if (body == null || body.bodyType != RigidbodyType2D.Dynamic)
                continue;

            float vx = body.linearVelocity.x;
            if (Mathf.Abs(vx) < minSpeed)
                continue;

            float d2 = ((Vector2)body.worldCenterOfMass - wood).sqrMagnitude;
            if (d2 > rangeSq)
                continue;

            float falloff = 1f - Mathf.Sqrt(d2) / range;
            float q = (vx * vx) / refSpeedSq;
            unit += q * falloff * Mathf.Sign(vx);
        }

        return Mathf.Clamp(unit, -1f, 1f);
    }

    private static void RefreshWakeBirds()
    {
        float now = Time.time;
        if (now - wakeBirdsAt < WakeBirdsRefreshSeconds)
            return;
        wakeBirdsAt = now;
        wakeBirds = Object.FindObjectsByType<BirdController>(FindObjectsSortMode.None);
        if (wakeBirds == null)
            wakeBirds = System.Array.Empty<BirdController>();
    }

    private void Grow(
        HumanSegment parent,
        float parentHeading,
        int depth,
        float length,
        float width,
        float heading,
        int cap)
    {
        if (SegmentCount >= cap) return;
        if (width < minWidth || length < minLength) return;

        bool isDynamic = rig == TreeRig.Sway;
        bool farSide = heading < parentHeading - 0.01f;
        Color bark = WoodColor(depth, farSide);

        string name = $"Wood_{depth}_{SegmentCount}";
        HumanSegment seg = CreateWood(
            name,
            new Vector2(width, length),
            woodDensity * width * width * length,
            bark,
            depth,
            isDynamic,
            isDynamic);

        Vector2 parentTip = TipLocal(parent, parentHeading);
        Vector2 axis = HeadingDir(heading);
        seg.transform.localPosition = parentTip + axis * (length * 0.5f);
        seg.transform.localRotation = Quaternion.Euler(0f, 0f, heading);

        TreeSpring spring = null;
        if (isDynamic)
        {
            seg.ConnectTo(
                parent,
                new Vector2(0f, -length * 0.5f),
                new Vector2(0f, parent.size.y * 0.5f),
                -BendLimit(width),
                BendLimit(width));
            AddWoodActuators(seg, width, length, heading);
            spring = seg.GetComponent<TreeSpring>();
            DynamicBodyCount++;
        }

        PaintWoodTaper(seg, width, length, bark, depth, heading);
        // Bark plates are Visual children; collider size is unchanged.
        nodes.Add(new Node
        {
            segment = seg,
            heading = heading,
            depth = depth,
            dynamic = isDynamic,
            parentIndex = IndexOf(parent),
            spring = spring
        });
        SegmentCount++;

        if (growFruit && fruitOnInnerWood && depth >= 2)
            AttachFruit(seg, farSide, depth, heading);

        // Pack-tip (oak) already sits one tuft on the true tip. A second
        // station on the last internode is two stars along one shoot.
        // Willow / pine / poplar keep internodal leaves — that IS the
        // curtain / needles / column. Fork 28° and colliders stay.
        if (growLeaves && !leafPackTip && depth == growDepthCap - 1 && depth >= 1)
            AttachLeaves(seg, farSide, Mathf.Max(2, leafSpriteCount / 2), depth, heading);
        // Leader stays near the parent axis (oak ~8° of 28°). Painting
        // that as a crotch made two Ys on one tip. Cut is halfway
        // between the leader share and the full fork, so poplar's 12°
        // side still paints and oak's 28° recipe does not change.
        // Each real daughter paints its own join (lilac Y included).
        // Doubling the web from the first arm glued a T-bar on the tip.
        float relFork = Mathf.DeltaAngle(parentHeading, heading);
        float crotchCut = binaryFork
            ? Mathf.Abs(forkAngle) * 0.35f
            : (Mathf.Abs(forkAngle * leaderForkShare) + Mathf.Abs(forkAngle)) * 0.5f;
        if (Mathf.Abs(relFork) > crotchCut + 0.5f)
        {
            forkCrotchCount++;
            PaintBranchCrotch(parent, seg, relFork, width, bark, depth);
            PaintBranchCollar(seg, bark, depth);
        }

        if (depth >= growDepthCap)
        {
            AttachLeaves(seg, farSide, leafSpriteCount, depth, heading);
            AttachWinterBuds(seg, farSide, depth);
            if (growBloom)
                AttachBloom(seg, farSide, depth, heading);
            if (growFruit && !fruitOnInnerWood)
                AttachFruit(seg, farSide, depth, heading);
            return;
        }

        float jitter = NextJitter();
        float nextLen = length * lengthDecay;
        float nextWid = width * widthDecay;

        if (binaryFork)
        {
            // Equal Y-split. CoM stays over the stump because sides alternate
            // by heading sign, not by a leader that always leans one way.
            Grow(seg, heading, depth + 1, nextLen, nextWid,
                heading - (forkAngle + jitter), cap);
            Grow(seg, heading, depth + 1, nextLen, nextWid,
                heading + (forkAngle + jitter * 0.35f), cap);
            return;
        }

        // Leader near the parent axis (oak ~8° of 28°). The side used
        // to flip by depth: a −28° limb then grew a child at 0° and
        // the silhouette was a zigzag pole, not a crown. First fork
        // still alternates about the trunk (CoM over the stump). A
        // limb that already left the axis keeps going outward, with a
        // shallower secondary fork so the first side does not pile
        // all the mass on one flank. Clamp keeps cedar 68° from
        // folding past horizontal. forkAngle 28 stays the recipe.
        Grow(seg, heading, depth + 1, nextLen, nextWid,
            heading + forkAngle * leaderForkShare + jitter * 0.25f, cap);

        bool growSide = !skipSideOnEvenDepth || (depth % 2 == 1);
        if (whorlFork && depth == 0)
            growSide = false;
        if (growSide)
        {
            float sideLen = nextLen * sideLengthScale;
            int sideDepth = sidesAreTips ? growDepthCap : depth + 1;
            Grow(seg, heading, sideDepth, sideLen, nextWid,
                OutwardSideHeading(parentHeading, heading, depth, jitter, false), cap);
            bool extraFork = whorlFork
                || (bothSidesAtRoot && depth == 0)
                || (bothSidesMaxDepth >= 0 && depth <= bothSidesMaxDepth);
            if (extraFork)
            {
                Grow(seg, heading, sideDepth, sideLen, nextWid,
                    OutwardSideHeading(parentHeading, heading, depth, jitter, true), cap);
            }
        }
    }

    // World heading of a side (or the inner extra of a Y). Picture and
    // hinge rest share this — Static and Sway must match. Collider
    // size and public forkAngle stay.
    private float OutwardSideHeading(
        float parentHeading,
        float heading,
        int depth,
        float jitter,
        bool extra)
    {
        float arrived = Mathf.DeltaAngle(parentHeading, heading);
        float outward;
        if (Mathf.Abs(arrived) > SpreadLimbDeg)
            outward = Mathf.Sign(arrived);
        else
            outward = (depth % 2 == 0) ? -1f : 1f;
        float delta;
        if (extra)
            delta = -outward * (forkAngle + jitter * 0.4f);
        else
        {
            float forkDeg = Mathf.Abs(arrived) > SpreadLimbDeg
                ? forkAngle * 0.62f
                : forkAngle;
            delta = outward * (forkDeg + jitter);
        }
        return Mathf.Clamp(heading + delta, -78f, 78f);
    }

    private void AttachLeaves(HumanSegment twig, bool farSide, int count, int depth, float heading)
    {
        if (!growLeaves || twig == null) return;
        bool paint = !(HeadlessTrial.Active && !HeadlessTrial.VisualCapture);
        int n = Mathf.Max(1, count);
        // Pack-tip kinds (oak) share one origin plus the tip spray.
        // Two stations in the outer fifth still read as a comb at 1x.
        int clusters = leafPackTip ? 1 : LeafNodeCountFor(n);
        if (!paint)
        {
            LeafCount += n;
            leafClusterCount += clusters;
            TallyTipCluster(false);
            // Headless without -visual still reports the tuft plate.
            // Wood heading ~8° of fork 28 left JSON at pole sail 1.
            NoteClusterHangSail(ClusterPlateSail(
                twig, farSide, depth, heading, clusters));
            return;
        }
        float side0 = farSide ? -1f : 1f;
        float clusterRest = LeafClusterRest(heading);
        Vector3 down = Vector3.zero;
        float hangTilt = 0f;
        if (leafHangWorldDown)
        {
            down = twig.transform.InverseTransformDirection(Vector3.down);
            hangTilt = Mathf.Atan2(down.x, down.y) * Mathf.Rad2Deg;
        }

        int placed = 0;
        int leafI = 0;
        for (int c = 0; c < clusters; c++)
        {
            int left = clusters - c;
            int remaining = n - placed;
            int inThis = c == clusters - 1 ? remaining : Mathf.Max(1, remaining / left);
            if (inThis == 1 && remaining >= 2 && left > 1)
                inThis = 2;
            if (c == clusters - 1)
                inThis = remaining;

            float along;
            if (leafHangWorldDown)
                along = Mathf.Lerp(0.22f, 0.86f, clusters == 1 ? 0.55f : c / (float)(clusters - 1));
            else if (leafPackTip)
                along = 0.84f;
            else
                // Distal packing: internodal tufts sit as a crown, not
                // a star halfway down the last stick. Willow curtain
                // keeps the hang lerp above. Oak fork 28° stays.
                along = Mathf.Lerp(0.42f, 0.88f, clusters == 1 ? 0.72f : c / (float)(clusters - 1));

            Color hubColor = LeafTint(c * 3, farSide);
            float y = (along - 0.5f) * twig.size.y;
            float wAt = PictureWidthAt(twig.size.x, along, depth);
            leafClusterCount++;
            // Leader wood is ~8° of fork 28. The tuft is a plate
            // (hang + leaflet envelope); HeadingSailMul(wood) left
            // the hub as pole flutter after Wrist left the sinus.
            float clusterHang = leafHangWorldDown
                ? down.x * wAt * ClusterHangMul()
                : side0 * wAt * ClusterHangMul();
            float clusterSail = PicturePlateSail(
                heading, clusterHang, y, leafSize.x * 0.35f);
            NoteClusterHangSail(clusterSail);

            Transform clusterRoot = twig.transform;
            bool rideCluster = false;
            if (inThis >= 2 && maxSegments <= 31)
            {
                clusterRoot = AddFlatPicture(
                    twig.transform,
                    twig.name + "_LeafNode" + c,
                    new Vector2(Mathf.Max(0.010f, wAt * 0.22f), leafSize.y * 0.22f),
                    Color.Lerp(hubColor, palTwig, 0.35f),
                    twig.sortingOrder + 1,
                    new Vector3(clusterHang, y, 0f),
                    clusterRest,
                    ArtLibrary.Bark);
                RememberLeafSway(clusterRoot, 0.85f * clusterSail, along * 1.4f);
                rideCluster = clusterRoot != null;
                if (!rideCluster)
                    clusterRoot = twig.transform;
                else
                    // Hub is a brown card at the tuft origin. Hide it so
                    // the lobed blades own 1x, same as SprayHub.
                    HidePictureBody(clusterRoot);
            }

            for (int k = 0; k < inThis; k++)
            {
                Color leafletColor = LeafTint(c * 3 + k, farSide);
                float u = inThis == 1 ? 0f : (k / (float)(inThis - 1) - 0.5f) * 2f;
                float hang;
                float tilt;
                if (leafHangWorldDown)
                {
                    hang = down.x * wAt * (0.36f + 0.10f * k);
                    tilt = hangTilt + u * 8f;
                }
                else if (inThis == 3 && k == 1)
                {
                    // hang 0 sat IN the sinus between the laterals
                    // (oak Dome analog after Wrist left LobeL). Peak
                    // rides the left pad; leafSize / fork 28 stay.
                    hang = side0 * wAt * 0.50f;
                    tilt = hang >= 0f ? 8f : -8f;
                }
                else
                {
                    float sideK = (k & 1) == 0 ? side0 : -side0;
                    hang = sideK * wAt * (0.28f + 0.22f * Mathf.Abs(u));
                    // Unequal tilt so a tuft is not a radial comb.
                    tilt = (hang >= 0f ? leafTilt : -leafTilt) + u * 14f
                        + (k == 0 ? -6f : (k == 2 ? 8f : 0f));
                }
                if (!rideCluster)
                    tilt += clusterRest;
                if (rideCluster)
                    hang -= clusterHang;
                // Slight along scatter so the node is a bunch, not a wheel.
                float bunch = inThis <= 1
                    ? 0f
                    : (k - (inThis - 1) * 0.5f) * leafSize.y * 0.16f;
                // Petiole must point at the leaflet. A fixed 8° stick
                // at 42% hang left a brown tick beside a card — the
                // blade sat at full hang / leafTilt. Pine sheath /
                // willow sickle skip: those already own the join.
                // Extra boxes ride the cluster cache — LateUpdate
                // does not Find. Colliders stay. Oak fork 28° stays.
                if (rideCluster && !leafHangWorldDown && barkLook != BarkLook.Scale)
                {
                    float petioleReach = Mathf.Sqrt(hang * hang + bunch * bunch);
                    if (petioleReach > 0.008f)
                    {
                        Transform petioleXform = AddFlatPicture(
                            clusterRoot,
                            clusterRoot.name + "_Petiole" + k,
                            new Vector2(
                                Mathf.Max(0.007f, wAt * 0.14f),
                                Mathf.Max(0.014f, petioleReach * 0.62f)),
                            Color.Lerp(leafletColor, palTwig, 0.45f),
                            twig.sortingOrder + 1,
                            new Vector3(hang * 0.38f, bunch * 0.38f, 0f),
                            0f,
                            ArtLibrary.Bark);
                        AimPictureAlongY(petioleXform, new Vector3(hang, bunch, 0f));
                        Transform petioleTipXform = AddFlatPicture(
                            clusterRoot,
                            clusterRoot.name + "_Petiole" + k + "Tip",
                            new Vector2(
                                Mathf.Max(0.005f, wAt * 0.08f),
                                Mathf.Max(0.008f, petioleReach * 0.22f)),
                            Color.Lerp(leafletColor, palTwig, 0.45f),
                            twig.sortingOrder + 1,
                            new Vector3(hang * 0.52f, bunch * 0.52f, 0f),
                            0f,
                            ArtLibrary.Bark);
                        // Spur PetioleTip closed the internodal stick.
                        // Packed-tuft Petiole without a lip stayed a
                        // brown card after BaseTip (CarmB before
                        // CarmBTip). Hang further along +Y; leafSize
                        // / fork 28 stay.
                        AimPictureAlongY(
                            petioleTipXform, new Vector3(hang, bunch, 0f), 0.52f);
                    }
                }
                GameObject leafObject = new GameObject(twig.name + "_Leaf" + leafI);
                leafObject.transform.SetParent(clusterRoot, false);
                SpriteRenderer leafSprite = leafObject.AddComponent<SpriteRenderer>();
                HumanSegment.ApplyPicture(
                    leafSprite,
                    leafSize,
                    leafShape,
                    ArtLibrary.Leaf,
                    leafletColor,
                    twig.sortingOrder + 2);
                float leafY = rideCluster ? bunch : y + bunch;
                leafObject.transform.localPosition = new Vector3(hang, leafY, 0f);
                leafObject.transform.localRotation = Quaternion.Euler(0f, 0f, tilt);
                AttachLeafLobes(leafObject.transform, leafletColor, twig.sortingOrder + 2);
                LeafCount++;
                // Leaflets ride the node so a gust is one tuft, not a comb.
                if (!rideCluster)
                    RememberLeafSway(
                        leafObject.transform,
                        1f * clusterSail,
                        along * 2.2f + k * 0.55f);
                leafI++;
            }
            placed += inThis;
        }
        TallyTipCluster(true);
        AttachTipCluster(twig, farSide, depth, heading);
    }

    private void AttachBloom(HumanSegment twig, bool farSide, int depth, float heading)
    {
        if (!growBloom || twig == null) return;
        bool paint = !(HeadlessTrial.Active && !HeadlessTrial.VisualCapture);
        Color color = useKindPalette ? palBloom : new Color(0.78f, 0.42f, 0.82f, 1f);
        // Spring add-on on a packed tuft: one axillary bud. A 3-spray
        // on the same origin is the second star 06:59 already closed
        // for leaves.
        if (seasonAddedBloom && bloomClusterCount <= 1)
        {
            BloomCount++;
            NoteClusterHangSail(ClusterPlateSail(twig, farSide, depth, heading, 1));
            if (paint)
                PaintAxillaryBloom(twig, farSide, depth, color, heading);
            return;
        }
        // Lilac raceme / hazel catkin own 1x. AttachCluster on the same
        // tip was a second star (pack-tip analog). Tally florets even
        // without -visual. Hub hang sail too — wood heading ~8° of
        // fork 28 left JSON at pole 1 when paint skipped.
        if (ResolvedKind == TreeKind.Lilac)
        {
            BloomCount += LilacRacemeCount();
            NoteClusterHangSail(LilacRacemePlateSail(twig, farSide, depth, heading));
            if (paint)
                PaintLilacRaceme(twig, farSide, depth, heading);
            return;
        }
        if (ResolvedKind == TreeKind.Hazel)
        {
            BloomCount += HazelCatkinCount();
            NoteClusterHangSail(HazelCatkinPlateSail(twig, farSide, depth, heading));
            if (paint)
                PaintHazelCatkin(twig, farSide, depth, heading);
            return;
        }
        if (!paint)
        {
            BloomCount += Mathf.Max(1, bloomClusterCount);
            return;
        }
        AttachCluster(twig, farSide, bloomClusterCount, bloomSize, color, bloomShape, "Bloom", 3, depth, heading);
    }

    private int LilacRacemeCount()
    {
        return maxSegments > 31 ? 4 : 6;
    }

    private int HazelCatkinCount()
    {
        return maxSegments > 31 ? 2 : 3;
    }

    // Lilac crown is bloom, not leaf mass. A zipper of 0° cards along Y
    // still read as apple blossom on a stick (and each LateUpdate amp
    // combed). One rachis + overlapping florets on a hidden hub: a
    // panicle at 1x, one flutter. Extra boxes only.
    private void PaintLilacRaceme(HumanSegment twig, bool farSide, int depth, float heading)
    {
        if (twig == null)
            return;
        float side = farSide ? -1f : 1f;
        int n = LilacRacemeCount();
        float spikeH = twig.size.y * (maxSegments > 31 ? 0.42f : 0.52f);
        float hubHang;
        float hubY;
        float racemeSail = LilacRacemePlateSail(
            twig, farSide, depth, heading, out hubHang, out hubY);
        Transform hub = AddFlatPicture(
            twig.transform,
            twig.name + "_RacemeHub",
            new Vector2(0.010f, 0.010f),
            palBloom,
            twig.sortingOrder + 3,
            new Vector3(hubHang, hubY, 0f),
            0f,
            null);
        if (hub != null)
            HidePictureBody(hub);
        RememberLeafSway(hub, 1.25f * racemeSail, 2.4f);
        Transform parent = hub != null ? hub : twig.transform;
        // Hub already sits on the 0.28 pad. Rachis at the hub origin
        // was a green mast through the panicle (pine Needle1 analog);
        // florets at 0.08…0.22 of a 9 cm bloom still hugged that stick.
        // Stem rides the left pad; florets open around it. bloomSize /
        // fork 28 stay.
        AddFlatPicture(
            parent,
            twig.name + "_Rachis",
            new Vector2(
                Mathf.Max(0.010f, PictureWidthAt(twig.size.x, 0.70f, depth) * 0.22f),
                spikeH),
            palTwig,
            twig.sortingOrder + 2,
            new Vector3(-bloomSize.x * 0.14f, spikeH * 0.22f, 0f),
            side * 4f,
            ArtLibrary.Bark);
        AddFlatPicture(
            parent,
            twig.name + "_RachisTip",
            new Vector2(
                Mathf.Max(0.008f, PictureWidthAt(twig.size.x, 0.70f, depth) * 0.14f),
                Mathf.Max(0.010f, spikeH * 0.18f)),
            palTwig,
            twig.sortingOrder + 2,
            // PetioleTip closed the ovate stalk. Lilac Rachis without
            // a lip stayed a green mast after FloretTip (CarmB before
            // CarmBTip). Hang off the left pad; bloomSize stays.
            new Vector3(-bloomSize.x * 0.14f, spikeH * 0.42f, 0f),
            side * 8f,
            ArtLibrary.Bark);
        for (int i = 0; i < n; i++)
        {
            float u = n == 1 ? 0f : i / (float)(n - 1);
            float col = (i & 1) == 0 ? -1f : 1f;
            AddFlatPicture(
                parent,
                twig.name + "_Raceme" + i,
                new Vector2(
                    bloomSize.x * Mathf.Lerp(0.48f, 0.22f, u),
                    bloomSize.y * Mathf.Lerp(0.48f, 0.22f, u)),
                palBloom,
                twig.sortingOrder + 3,
                new Vector3(
                    // 0.70 at ±0.38 still reached 0.03 and zipped the
                    // panicle into a sausage (CheekL analog). Florets
                    // open off the rachis. bloomSize stays.
                    col * bloomSize.x * Mathf.Lerp(0.42f, 0.28f, u),
                    spikeH * Mathf.Lerp(-0.06f, 0.48f, u),
                    0f),
                col * (12f - 5f * u),
                null);
            float racemeW = bloomSize.x * Mathf.Lerp(0.48f, 0.22f, u);
            float racemeH = bloomSize.y * Mathf.Lerp(0.48f, 0.22f, u);
            AddFlatPicture(
                parent,
                twig.name + "_Raceme" + i + "Tip",
                new Vector2(racemeW * 0.48f, racemeH * 0.42f),
                palBloom,
                twig.sortingOrder + 3,
                // Garden FloretTip closed the bed. Tree Raceme without
                // a lip stayed a panicle coin after CalyxTip (Cap
                // before CapTip). Hang off the floret; bloomSize stays.
                new Vector3(
                    col * bloomSize.x * Mathf.Lerp(0.50f, 0.34f, u),
                    spikeH * Mathf.Lerp(-0.02f, 0.56f, u),
                    0f),
                col * (16f - 5f * u),
                null);
        }
    }

    // Hazel catkin: a hanging lamb's tail. Equal 0.70 cards at ±0.18
    // zipped across the hub (lilac 0.70 floret analog). Narrower
    // packets hang off the tail. One hub, one LateUpdate amp.
    private void PaintHazelCatkin(HumanSegment twig, bool farSide, int depth, float heading)
    {
        if (twig == null)
            return;
        Vector3 down = twig.transform.InverseTransformDirection(Vector3.down);
        float tilt = Mathf.Atan2(down.x, down.y) * Mathf.Rad2Deg;
        int n = HazelCatkinCount();
        float hubHang;
        float hubY;
        float catkinSail = HazelCatkinPlateSail(
            twig, farSide, depth, heading, out hubHang, out hubY);
        Transform hub = AddFlatPicture(
            twig.transform,
            twig.name + "_CatkinHub",
            new Vector2(0.010f, 0.010f),
            palBloom,
            twig.sortingOrder + 3,
            new Vector3(hubHang, hubY, 0f),
            0f,
            null);
        if (hub != null)
            HidePictureBody(hub);
        RememberLeafSway(hub, 1.45f * catkinSail, 2.8f);
        Transform parent = hub != null ? hub : twig.transform;
        // Hub already sits on the 0.28 pad. Tail at the hub origin
        // was a green mast through the lamb's tail (lilac Rachis
        // analog). Stem rides the left pad; packets open around it.
        // bloomSize / fork 28 stay.
        AddFlatPicture(
            parent,
            twig.name + "_CatkinTail",
            new Vector2(bloomSize.x * 0.22f, bloomSize.y * (n + 0.4f) * 0.55f),
            palTwig,
            twig.sortingOrder + 2,
            new Vector3(
                -bloomSize.x * 0.14f + down.x * bloomSize.y * 0.55f,
                down.y * bloomSize.y * 0.50f,
                0f),
            tilt,
            ArtLibrary.Bark);
        AddFlatPicture(
            parent,
            twig.name + "_CatkinTailTip",
            new Vector2(bloomSize.x * 0.12f, bloomSize.y * 0.18f),
            palTwig,
            twig.sortingOrder + 2,
            // RachisTip closed the panicle mast. CatkinTail without a
            // lip stayed a green post after FloretTip (CarmB before
            // CarmBTip). Hang off the distal tail; bloomSize stays.
            new Vector3(
                -bloomSize.x * 0.14f + down.x * bloomSize.y * 0.78f,
                down.y * bloomSize.y * 0.72f,
                0f),
            tilt,
            ArtLibrary.Bark);
        for (int i = 0; i < n; i++)
        {
            float u = n == 1 ? 0f : i / (float)(n - 1);
            float along = (i + 1) / (n + 0.5f);
            float col = (i & 1) == 0 ? -1f : 1f;
            AddFlatPicture(
                parent,
                twig.name + "_Catkin" + i,
                new Vector2(
                    bloomSize.x * Mathf.Lerp(0.42f, 0.22f, u),
                    bloomSize.y * Mathf.Lerp(0.48f, 0.28f, u)),
                palBloom,
                twig.sortingOrder + 3,
                new Vector3(
                    down.x * bloomSize.y * along * 1.15f
                        + col * bloomSize.x * Mathf.Lerp(0.36f, 0.22f, u),
                    down.y * bloomSize.y * along * 1.05f,
                    0f),
                tilt + col * (8f - 3f * u),
                null);
            float catkinW = bloomSize.x * Mathf.Lerp(0.42f, 0.22f, u);
            float catkinH = bloomSize.y * Mathf.Lerp(0.48f, 0.28f, u);
            AddFlatPicture(
                parent,
                twig.name + "_Catkin" + i + "Tip",
                new Vector2(catkinW * 0.48f, catkinH * 0.40f),
                palBloom,
                twig.sortingOrder + 3,
                // RacemeTip closed the panicle coin. Catkin packet
                // without a lip stayed a lamb-tail card (Cap before
                // CapTip). Hang off the packet; bloomSize stays.
                new Vector3(
                    down.x * bloomSize.y * along * 1.28f
                        + col * bloomSize.x * Mathf.Lerp(0.44f, 0.28f, u),
                    down.y * bloomSize.y * along * 1.18f,
                    0f),
                tilt + col * (12f - 3f * u),
                null);
        }
    }

    // One spring packet under the packed tuft. Not a 3-spray, not a perch.
    private void PaintAxillaryBloom(HumanSegment twig, bool farSide, int depth, Color color, float heading)
    {
        if (twig == null)
            return;
        float along = 0.70f;
        float wAt = PictureWidthAt(twig.size.x, along, depth);
        float side = farSide ? -1f : 1f;
        float budHang = side * wAt * ClusterHangMul();
        float budY = (along - 0.5f) * twig.size.y;
        float budSail = PicturePlateSail(heading, budHang, budY, bloomSize.x * 0.35f);
        Transform bud = AddFlatPicture(
            twig.transform,
            twig.name + "_SpringBud",
            bloomSize,
            color,
            twig.sortingOrder + 3,
            new Vector3(budHang, budY, 0f),
            side * 16f + LeafClusterRest(heading),
            null);
        PaintBloomPetals(bud, bloomSize, color, twig.sortingOrder + 3);
        RememberLeafSway(bud, 0.90f * budSail, along * 2.0f);
    }

    // Picture only. Does not add a perch pad — crows sit on wood BirdPerch.
    // Inner Apple / Plum wood gets one fruit; outer twigs two; tips three+.
    private void AttachFruit(HumanSegment twig, bool farSide, int depth, float heading)
    {
        if (!growFruit || twig == null) return;

        int count = 1;
        if (fruitOnInnerWood)
        {
            if (depth >= growDepthCap)
                count = ResolvedKind == TreeKind.Plum ? 5 : 3;
            else if (depth >= growDepthCap - 1)
                count = ResolvedKind == TreeKind.Plum ? 3 : 2;
            else if (depth < 2)
                return;
        }

        Color color = useKindPalette
            ? palFruit
            : new Color(0.82f, 0.16f, 0.14f, 0.5f);
        bool paint = !(HeadlessTrial.Active && !HeadlessTrial.VisualCapture);
        if (!paint)
        {
            FruitCount += count;
            return;
        }
        AttachCluster(twig, farSide, count, fruitSize, color, fruitShape, "Fruit", 3, depth, heading);
    }

    private void AttachCluster(
        HumanSegment twig,
        bool farSide,
        int count,
        Vector2 size,
        Color color,
        HumanVisualShape shape,
        string tag,
        int orderDelta,
        int depth,
        float heading)
    {
        if (twig == null || count < 1) return;

        float side = farSide ? -1f : 1f;
        int nodeCount = count <= 1 ? 1 : 2;
        int placedFruit = 0;
        float bloomRest = tag == "Bloom" ? LeafClusterRest(heading) : 0f;
        Vector3 down = Vector3.zero;
        if (tag == "Fruit")
            down = twig.transform.InverseTransformDirection(Vector3.down);
        for (int nodeI = 0; nodeI < nodeCount; nodeI++)
        {
            int nodesLeft = nodeCount - nodeI;
            int remaining = count - placedFruit;
            int inThis = nodeI == nodeCount - 1
                ? remaining
                : Mathf.Max(1, remaining / nodesLeft);
            float alongNode = nodeCount == 1
                ? 0.90f
                : (nodeI == 0 ? 0.78f : 0.92f);
            float wAt = PictureWidthAt(twig.size.x, alongNode, depth);
            for (int k = 0; k < inThis; k++)
            {
                float u = inThis == 1 ? 0f : (k / (float)(inThis - 1) - 0.5f) * 2f;
                float sideK = ((placedFruit + k) & 1) == 0 ? side : -side;
                float hangK = sideK * wAt * (0.22f + 0.20f * Mathf.Abs(u));
                GameObject part = new GameObject(twig.name + "_" + tag + (placedFruit + k));
                part.transform.SetParent(twig.transform, false);
                SpriteRenderer sprite = part.AddComponent<SpriteRenderer>();
                HumanSegment.ApplyPicture(
                    sprite,
                    size,
                    shape,
                    null,
                    color,
                    twig.sortingOrder + orderDelta);
                // Fruit hung along wood +X (a flag on the stick). Drop
                // toward world-down so an apple on a ±fork limb is a
                // crop, not a red tile. Bloom flattens like the tuft.
                // Grow-time only — no FixedUpdate alloc. fruitSize stays.
                Vector3 localPos;
                float localRot;
                if (tag == "Fruit")
                {
                    float drop = wAt * (0.18f + 0.12f * Mathf.Abs(u)) + size.y * 0.22f;
                    localPos = new Vector3(
                        down.x * drop + hangK * 0.35f,
                        (alongNode - 0.5f) * twig.size.y + down.y * drop,
                        0f);
                    localRot = Mathf.Atan2(down.x, down.y) * Mathf.Rad2Deg;
                }
                else
                {
                    localPos = new Vector3(
                        hangK, (alongNode - 0.5f) * twig.size.y, 0f);
                    localRot = bloomRest;
                }
                part.transform.localPosition = localPos;
                part.transform.localRotation = Quaternion.Euler(0f, 0f, localRot);
                float plateSail = PicturePlateSail(
                    heading, localPos.x, localPos.y, size.x * 0.35f);
                NoteClusterHangSail(plateSail);
                RememberLeafSway(
                    part.transform, 1.1f * plateSail, alongNode * 2.0f + k * 0.4f);
                if (tag == "Fruit")
                {
                    FruitCount++;
                    PaintFruitLobes(part.transform, size, color, twig.sortingOrder + orderDelta);
                }
                else if (tag == "Bloom")
                {
                    BloomCount++;
                    PaintBloomPetals(part.transform, size, color, twig.sortingOrder + orderDelta);
                }
            }
            placedFruit += inThis;
        }
    }

    // Picture only. Apple 10×10 cm grow square was a red card. Hide it
    // and fill fruitSize with overlapping boxes. Plum is a droplet with
    // a suture, not a cube cluster.
    private void PaintFruitLobes(Transform fruit, Vector2 size, Color color, int sortingOrder)
    {
        if (fruit == null)
            return;
        HidePictureBody(fruit);
        if (ResolvedKind == TreeKind.Plum)
        {
            // Cube+chip still two boxes (bush-coin analog). Two unequal
            // ovals plus a suture read as a droplet at 1x. fruitSize stays.
            AddFlatPicture(
                fruit,
                fruit.name + "_Drop",
                new Vector2(size.x * 0.70f, size.y * 0.88f),
                color,
                sortingOrder,
                new Vector3(-size.x * 0.08f, -size.y * 0.04f, 0f),
                -10f,
                null);
            AddFlatPicture(
                fruit,
                fruit.name + "_DropTip",
                new Vector2(size.x * 0.32f, size.y * 0.22f),
                color,
                sortingOrder,
                // ScallopTip closed the apple hem. Plum Drop without a
                // lip stayed a droplet card (Droop before DroopTip).
                // Hang off Drop; fruitSize stays.
                new Vector3(-size.x * 0.12f, -size.y * 0.36f, 0f),
                -6f,
                null);
            AddFlatPicture(
                fruit,
                fruit.name + "_DropR",
                new Vector2(size.x * 0.58f, size.y * 0.76f),
                color,
                sortingOrder,
                new Vector3(size.x * 0.12f, -size.y * 0.02f, 0f),
                12f,
                null);
            AddFlatPicture(
                fruit,
                fruit.name + "_DropRTip",
                new Vector2(size.x * 0.26f, size.y * 0.18f),
                color,
                sortingOrder,
                new Vector3(size.x * 0.18f, -size.y * 0.30f, 0f),
                8f,
                null);
            AddFlatPicture(
                fruit,
                fruit.name + "_Suture",
                new Vector2(size.x * 0.16f, size.y * 0.52f),
                palBarkDark,
                sortingOrder,
                // x=0.02 sat IN the sinus between Drop/DropR after
                // Stem left for Drop (oak Dome analog). Groove rides
                // Drop; DropR stays the outer cheek. fruitSize stays.
                new Vector3(-size.x * 0.10f, -size.y * 0.04f, 0f),
                -8f,
                ArtLibrary.Bark);
            AddFlatPicture(
                fruit,
                fruit.name + "_SutureTip",
                new Vector2(size.x * 0.08f, size.y * 0.14f),
                palBarkDark,
                sortingOrder,
                // DropTip closed the droplet. Plum Suture without a
                // lip stayed a bark post in the groove (CarmB before
                // CarmBTip). Hang off Drop; fruitSize stays.
                new Vector3(-size.x * 0.14f, -size.y * 0.22f, 0f),
                -12f,
                ArtLibrary.Bark);
            AddFlatPicture(
                fruit,
                fruit.name + "_Stem",
                new Vector2(size.x * 0.12f, size.y * 0.28f),
                palTwig,
                sortingOrder,
                // x=0 sat IN the suture between Drop/DropR (oak Dome
                // analog). Peak rides Drop. fruitSize stays.
                new Vector3(-size.x * 0.10f, size.y * 0.46f, 0f),
                12f,
                ArtLibrary.Bark);
            AddFlatPicture(
                fruit,
                fruit.name + "_StemTip",
                new Vector2(size.x * 0.07f, size.y * 0.10f),
                palTwig,
                sortingOrder,
                // PetioleTip closed the ovate stalk. Plum Stem without
                // a lip stayed a twig post after DropTip (CarmB before
                // CarmBTip). Hang off Drop; fruitSize stays.
                new Vector3(-size.x * 0.10f, size.y * 0.58f, 0f),
                16f,
                ArtLibrary.Bark);
            return;
        }

        AddFlatPicture(
            fruit,
            fruit.name + "_CheekL",
            new Vector2(size.x * 0.42f, size.y * 0.68f),
            color,
            sortingOrder,
            // 0.72 at −0.12 overlapped CheekR into a red disc (crown
            // Scallop1-in-the-bite analog). 0.56 at −0.22 still reached
            // +0.06 and kissed CheekR (Needle1 analog). Narrow left
            // cheek; sinus stays empty. fruitSize stays.
            new Vector3(-size.x * 0.28f, -size.y * 0.06f, 0f),
            -10f,
            null);
        AddFlatPicture(
            fruit,
            fruit.name + "_CheekLTip",
            new Vector2(size.x * 0.22f, size.y * 0.22f),
            color,
            sortingOrder,
            // ScallopTip closed the orchard hem. CheekL without a lip
            // stayed a coin on the apple (Drop before DropTip). Hang
            // off CheekL; sinus stays empty. fruitSize stays.
            new Vector3(-size.x * 0.38f, -size.y * 0.28f, 0f),
            -16f,
            null);
        AddFlatPicture(
            fruit,
            fruit.name + "_CheekR",
            new Vector2(size.x * 0.42f, size.y * 0.52f),
            color,
            sortingOrder,
            new Vector3(size.x * 0.28f, 0.02f * size.y, 0f),
            12f,
            null);
        AddFlatPicture(
            fruit,
            fruit.name + "_CheekRTip",
            new Vector2(size.x * 0.20f, size.y * 0.18f),
            color,
            sortingOrder,
            new Vector3(size.x * 0.38f, -size.y * 0.18f, 0f),
            18f,
            null);
        AddFlatPicture(
            fruit,
            fruit.name + "_Stem",
            new Vector2(size.x * 0.14f, size.y * 0.28f),
            palTwig,
            sortingOrder,
            // x=0 sat IN the bite between CheekL/R (apple Core analog).
            // Peak rides CheekL. fruitSize stays.
            new Vector3(-size.x * 0.28f, size.y * 0.42f, 0f),
            8f,
            ArtLibrary.Bark);
        AddFlatPicture(
            fruit,
            fruit.name + "_StemTip",
            new Vector2(size.x * 0.08f, size.y * 0.10f),
            palTwig,
            sortingOrder,
            // PetioleTip closed the ovate stalk. Apple Stem without a
            // lip stayed a twig post after CheekLTip (CarmB before
            // CarmBTip). Hang off CheekL; fruitSize stays.
            new Vector3(-size.x * 0.28f, size.y * 0.54f, 0f),
            12f,
            ArtLibrary.Bark);
        // Two overlapping cheeks were a pair of coins (bush analog).
        // Sepals ride the pads, not the bite. fruitSize stays.
        AddFlatPicture(
            fruit,
            fruit.name + "_SepalL",
            new Vector2(size.x * 0.22f, size.y * 0.18f),
            palTwig,
            sortingOrder,
            new Vector3(-size.x * 0.28f, -size.y * 0.36f, 0f),
            -16f,
            ArtLibrary.Bark);
        AddFlatPicture(
            fruit,
            fruit.name + "_SepalLTip",
            new Vector2(size.x * 0.12f, size.y * 0.08f),
            palTwig,
            sortingOrder,
            // CalyxTip closed the bloom cup. Fruit SepalL without a
            // lip stayed a bark card under the cheeks (Sheath before
            // SheathTip). Hang off CheekL; fruitSize stays.
            new Vector3(-size.x * 0.36f, -size.y * 0.44f, 0f),
            -22f,
            ArtLibrary.Bark);
        AddFlatPicture(
            fruit,
            fruit.name + "_SepalR",
            new Vector2(size.x * 0.20f, size.y * 0.16f),
            palTwig,
            sortingOrder,
            new Vector3(size.x * 0.28f, -size.y * 0.34f, 0f),
            14f,
            ArtLibrary.Bark);
        AddFlatPicture(
            fruit,
            fruit.name + "_SepalRTip",
            new Vector2(size.x * 0.10f, size.y * 0.07f),
            palTwig,
            sortingOrder,
            new Vector3(size.x * 0.36f, -size.y * 0.42f, 0f),
            20f,
            ArtLibrary.Bark);
    }

    // Picture only. A bloomSize square was a tile (apple fruit-card analog).
    // Hide it and fill the envelope with overlapping petals. Two colours:
    // bloom + twig calyx. Not a PNG. bloomSize stays.
    private void PaintBloomPetals(Transform bloom, Vector2 size, Color color, int sortingOrder)
    {
        if (bloom == null)
            return;
        HidePictureBody(bloom);
        AddFlatPicture(
            bloom,
            bloom.name + "_PetalL",
            new Vector2(size.x * 0.48f, size.y * 0.48f),
            color,
            sortingOrder,
            // −0.28 right −0.04 / PetalR left +0.07 left a 0.11 sky
            // slit on the head (pine Body analog). Width 0.48 kisses
            // x=0 from the left. Calyx / Tip ride this pad. bloomSize stays.
            new Vector3(-size.x * 0.24f, -size.y * 0.06f, 0f),
            -22f,
            null);
        AddFlatPicture(
            bloom,
            bloom.name + "_PetalLTip",
            new Vector2(size.x * 0.22f, size.y * 0.18f),
            color,
            sortingOrder,
            // PetalTip closed the head peak. PetalL without a lip
            // stayed a cup coin after CalyxTip (Cap before CapTip).
            // Hang off PetalL; kiss at x=0 stays. bloomSize stays.
            new Vector3(-size.x * 0.36f, -size.y * 0.18f, 0f),
            -28f,
            null);
        AddFlatPicture(
            bloom,
            bloom.name + "_PetalR",
            new Vector2(size.x * 0.42f, size.y * 0.44f),
            color,
            sortingOrder,
            // Width 0.42; left edge 0 against PetalL right 0 (pine
            // BodyR analog). Far cup. bloomSize stays.
            new Vector3(size.x * 0.21f, -size.y * 0.04f, 0f),
            18f,
            null);
        AddFlatPicture(
            bloom,
            bloom.name + "_PetalRTip",
            new Vector2(size.x * 0.20f, size.y * 0.16f),
            color,
            sortingOrder,
            new Vector3(size.x * 0.32f, -size.y * 0.16f, 0f),
            24f,
            null);
        AddFlatPicture(
            bloom,
            bloom.name + "_PetalTip",
            new Vector2(size.x * 0.48f, size.y * 0.40f),
            color,
            sortingOrder,
            // Width 0.48 kisses x=0; PetalTipR is the far peak
            // (poplar ApexR analog). bloomSize stays.
            new Vector3(-size.x * 0.24f, size.y * 0.18f, 0f),
            8f,
            null);
        AddFlatPicture(
            bloom,
            bloom.name + "_PetalTipR",
            new Vector2(size.x * 0.42f, size.y * 0.36f),
            color,
            sortingOrder,
            // Left-only PetalTip was a one-tent head after PetalL/R
            // kissed. Left edge 0 against PetalTip right 0.
            // bloomSize stays.
            new Vector3(size.x * 0.21f, size.y * 0.16f, 0f),
            -6f,
            null);
        AddFlatPicture(
            bloom,
            bloom.name + "_Calyx",
            new Vector2(size.x * 0.28f, size.y * 0.24f),
            palTwig,
            sortingOrder,
            // Pad rides PetalL; sinus between the cups stays the
            // calyx join, not a mast. bloomSize stays.
            new Vector3(-size.x * 0.24f, -size.y * 0.22f, 0f),
            6f,
            ArtLibrary.Bark);
        AddFlatPicture(
            bloom,
            bloom.name + "_CalyxTip",
            new Vector2(size.x * 0.16f, size.y * 0.12f),
            palTwig,
            sortingOrder,
            // SepalTip closed the garden cup. Tree Calyx without a
            // lip stayed a bark card under the flush (Sheath before
            // SheathTip). Hang off PetalL; bloomSize stays.
            new Vector3(-size.x * 0.24f, -size.y * 0.36f, 0f),
            10f,
            ArtLibrary.Bark);
    }

    private HumanSegment CreateWood(
        string name,
        Vector2 size,
        float mass,
        Color color,
        int sortingOrder,
        bool livePhysics,
        bool floorInertia)
    {
        GameObject segmentObject = new GameObject(name);
        HumanSegment segment = segmentObject.AddComponent<HumanSegment>();
        if (livePhysics)
        {
            segment.Initialize(
                name,
                size,
                Mathf.Max(0.02f, mass),
                color,
                transform,
                sortingOrder,
                HumanVisualShape.Box);
        }
        else
        {
            // Grove / Static: sprite only. Sit contact is BirdPerch.
            segment.InitializeVisualOnly(
                name,
                size,
                color,
                transform,
                sortingOrder,
                HumanVisualShape.Box);
        }
        if (livePhysics)
            segment.jointMarkerDiameter = jointMarkerDiameter;
        if (segment.rb != null && floorInertia && minSegmentInertia > 0f
            && segment.rb.inertia < minSegmentInertia)
            segment.rb.inertia = minSegmentInertia;
        return segment;
    }

    private void AddWoodActuators(HumanSegment segment, float width, float length, float heading)
    {
        if (segment.joint == null) return;

        float t = ThicknessT(width);
        // Stiffness closer to width³: a thin twig barely holds a moment.
        float thick = t * t * t;
        float lengthScale = trunkLength / Mathf.Max(0.05f, length);

        TreeSpring spring = segment.gameObject.AddComponent<TreeSpring>();
        spring.stiffness = Mathf.Lerp(twigStiffness, trunkStiffness, thick) * lengthScale;
        spring.restAngle = 0f;
        spring.restWorldAngle = heading;
        // World-pose is safe only on the stump (Static). Oak keeps the
        // joint spring alone — that hold is the 30.08 canon. Apple's 3 m
        // trunk needs world-up or mean wind lists the whole orchard tree.
        spring.worldStiffness = worldHoldScale > 0.001f
            ? worldHoldScale * Mathf.Lerp(55f, 280f, thick) * lengthScale
            : 0f;
        spring.maxTorque = Mathf.Lerp(twigSpringMaxTorque, trunkSpringMaxTorque, thick);

        JointFriction friction = segment.gameObject.AddComponent<JointFriction>();
        friction.damping = Mathf.Lerp(twigFriction, trunkFriction, t);
        friction.maxTorque = spring.maxTorque;
    }

    private float BendLimit(float width)
    {
        return Mathf.Lerp(twigBendLimit, trunkBendLimit, ThicknessT(width));
    }

    private float ThicknessT(float width)
    {
        return Mathf.Clamp01((width - minWidth) / Mathf.Max(0.001f, trunkWidth - minWidth));
    }

    private void NormalizeDynamicMass()
    {
        if (totalMass <= 0.01f) return;

        float sum = 0f;
        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodes[i].dynamic && nodes[i].segment != null)
                sum += nodes[i].segment.mass;
        }
        if (sum <= 0.01f) return;

        float scale = totalMass / sum;
        for (int i = 0; i < nodes.Count; i++)
        {
            Node n = nodes[i];
            if (!n.dynamic || n.segment == null || n.segment.rb == null)
                continue;
            float mass = n.segment.mass * scale;
            n.segment.mass = mass;
            n.segment.rb.mass = mass;
            if (minSegmentInertia > 0f && n.segment.rb.inertia < minSegmentInertia)
                n.segment.rb.inertia = minSegmentInertia;
        }
    }

    private void DisableCollisionsBetweenSegments()
    {
        Collider2D[] all = GetComponentsInChildren<Collider2D>();
        for (int i = 0; i < all.Length; i++)
        {
            for (int j = i + 1; j < all.Length; j++)
                Physics2D.IgnoreCollision(all[i], all[j], true);
        }
    }

    private static Vector2 HeadingDir(float headingDeg)
    {
        float rad = headingDeg * Mathf.Deg2Rad;
        return new Vector2(-Mathf.Sin(rad), Mathf.Cos(rad));
    }

    private static Vector2 RotateZ(Vector2 v, float deg)
    {
        float rad = deg * Mathf.Deg2Rad;
        float c = Mathf.Cos(rad);
        float s = Mathf.Sin(rad);
        return new Vector2(c * v.x - s * v.y, s * v.x + c * v.y);
    }

    private static Vector2 UnrotateZ(Vector2 v, float deg)
    {
        return RotateZ(v, -deg);
    }

    private void BindLookSway()
    {
        HasLookSway = false;
        lookSprites = System.Array.Empty<LookSprite>();
        lookHeading = System.Array.Empty<float>();
        lookCenter = System.Array.Empty<Vector2>();
        // Level1 apple is Static (LevelGrove). A Sway rig here would add
        // one Human of bodies next to the flock. Picture sway is the cheap path.
        // New gallery kinds look-sway the same way. Oak…Poplar Static stay frozen
        // so the World grove silhouette does not start waving.
        if (rig != TreeRig.Static || !UsesLookSwayKind())
            return;
        if (lookSwayDegrees <= 0f && lookBirdSwayDegrees <= 0f)
            return;

        lookPhase = seed * 0.17f;
        lookHeading = new float[nodes.Count];
        lookCenter = new Vector2[nodes.Count];
        // First LateUpdate must not treat 0 as live lean (a pole) on
        // a ±fork limb. Grow rest is the still pose. No alloc later.
        for (int h = 0; h < nodes.Count; h++)
            lookHeading[h] = nodes[h].heading;

        int nSprite = 0;
        for (int i = 0; i < nodes.Count; i++)
        {
            HumanSegment seg = nodes[i].segment;
            if (seg == null)
                continue;
            if (seg.visual != null)
                nSprite++;
            Transform t = seg.transform;
            for (int c = 0; c < t.childCount; c++)
            {
                Transform ch = t.GetChild(c);
                if (IsLookDecor(ch, seg.visual))
                    nSprite++;
            }
        }

        lookSprites = new LookSprite[nSprite];
        int w = 0;
        for (int i = 0; i < nodes.Count; i++)
        {
            HumanSegment seg = nodes[i].segment;
            if (seg == null)
                continue;
            if (seg.visual != null)
                lookSprites[w++] = MakeLookSprite(seg.visual, i, 0f);
            Transform t = seg.transform;
            for (int c = 0; c < t.childCount; c++)
            {
                Transform ch = t.GetChild(c);
                if (IsLookDecor(ch, seg.visual))
                    lookSprites[w++] = MakeLookSprite(ch, i, PictureTrackFromName(ch.name));
            }
        }

        HasLookSway = w > 0;
        if (w < lookSprites.Length)
            System.Array.Resize(ref lookSprites, w);
    }

    private void BindLeafSway()
    {
        HasLeafSway = false;
        LeafSwayWorldGust = false;
        leafSwayXforms = System.Array.Empty<Transform>();
        leafSwayRest = System.Array.Empty<Quaternion>();
        leafSwayRestPos = System.Array.Empty<Vector3>();
        leafSwaySign = System.Array.Empty<float>();
        leafSwayAmp = System.Array.Empty<float>();
        leafSwayPhase = System.Array.Empty<float>();
        if (rig != TreeRig.Sway || leafSwayDegrees <= 0f)
            return;
        int nCache = leafSwayAcc.Count;
        if (nCache > 0)
        {
                leafSwayXforms = new Transform[nCache];
                leafSwayRest = new Quaternion[nCache];
                leafSwayRestPos = new Vector3[nCache];
                leafSwaySign = new float[nCache];
                leafSwayAmp = new float[nCache];
                leafSwayPhase = new float[nCache];
                leafSwayTrack = new float[nCache];
                leafSwayWood = new int[nCache];
                leafSwayStation = new Vector3[nCache];
                leafSwayDrop = new float[nCache];
                // Same season table as ½ρv². Kind sail on the picture.
                // Needles are quiet; cedar pancakes and a willow curtain
                // catch the gust. Bound at grow — LateUpdate does not
                // scan names.
                float seasonAmp = PictureSeasonAmp();
                if (ResolvedKind == TreeKind.Pine)
                    seasonAmp *= 0.82f;
                else if (ResolvedKind == TreeKind.Cedar)
                    seasonAmp *= 1.16f;
                else if (ResolvedKind == TreeKind.Willow)
                    seasonAmp *= 1.10f;
                for (int cacheI = 0; cacheI < nCache; cacheI++)
                {
                    Transform x = leafSwayAcc[cacheI];
                    leafSwayXforms[cacheI] = x;
                    leafSwayRest[cacheI] = x != null ? x.localRotation : Quaternion.identity;
                    leafSwayRestPos[cacheI] = x != null ? x.localPosition : Vector3.zero;
                    leafSwaySign[cacheI] = (cacheI & 1) == 0 ? 1f : -1f;
                    // Amp and phase bound at Attach / RememberLeafSway.
                    // LateUpdate does not scan names. No Find.
                    leafSwayAmp[cacheI] = (cacheI < leafSwayAmpAcc.Count
                        ? leafSwayAmpAcc[cacheI]
                        : 1f) * seasonAmp;
                    leafSwayPhase[cacheI] = cacheI < leafSwayPhaseAcc.Count
                        ? leafSwayPhaseAcc[cacheI]
                        : cacheI * 0.67f;
                    leafSwayWood[cacheI] = FindWoodNodeIndex(x);
                    float track = PictureTrackFromName(x != null ? x.name : null);
                    leafSwayTrack[cacheI] = track;
                    Vector3 restP = leafSwayRestPos[cacheI];
                    Vector3 growDown = Vector3.down;
                    if (x != null && !ReferenceEquals(x.parent, null))
                        growDown = x.parent.InverseTransformDirection(Vector3.down);
                    float drop = Vector3.Dot(restP, growDown);
                    leafSwayDrop[cacheI] = drop;
                    leafSwayStation[cacheI] = restP - growDown * drop;
                }
        }
        // Capability follows the sail, not the GO. Headless without
        // -visual never paints leaves, so the cache is empty — LateUpdate
        // no-ops. Hold (-treeWind 0) zeroes windScale. A zero windSpeed
        // with leftover scale is still air — match TreeDriver LateUpdate.
        HasLeafSway = windScale > 0f && Mathf.Abs(windSpeed) >= 0.01f;
        // Flutter path is world +X even when this run has no painted
        // cache (headless without -visual) or still air (windScale 0).
        LeafSwayWorldGust = true;
    }

    // Stacked flat boxes on Visual. Collider size stays the grow box.
    // Stump and trunk keep bark plates; every depth tapers so twigs are
    // not fat layout rectangles. More bands on the leader so it reads as
    // a cone, not a fence post. Apple 96 stays 3 so GO stay cheap.
    // Depth ≥ 1 starts at the parent tip (WoodBaseScale), so a fork is
    // not a mushroom on the grow box. 04:41 only joined the stump;
    // 05:02 joined depth 1. Tip of depth ≥ 1 must stay below widthDecay
    // or WoodBaseScale(next) clamps to 1 and the inner fork mushrooms.
    private float WoodTipScale(int depth)
    {
        if (depth < 0)
        {
            float join = stumpWidth > 0.001f ? trunkWidth / stumpWidth : 0.80f;
            return Mathf.Clamp(join, 0.62f, 0.92f);
        }
        if (depth == 0)
            return visualTaper;
        float decay = widthDecay > 0.05f ? widthDecay : 0.65f;
        float t = Mathf.Clamp01((depth - 1) / 3f);
        // Depth 0 keeps public visualTaper 0.58. Inner picture must stay
        // under widthDecay or WoodBaseScale(next) mushrooms. 0.90 still
        // left twigs as fat posts on the last fork.
        float tip = Mathf.Lerp(visualTaper, visualTaper * 0.72f, t);
        return Mathf.Min(tip, decay * 0.84f);
    }

    private float WoodBaseScale(int depth)
    {
        if (depth <= 0)
            return 1f;
        float parentTip = WoodTipScale(depth - 1);
        float decay = widthDecay > 0.05f ? widthDecay : 0.65f;
        return Mathf.Clamp(parentTip / decay, 0.40f, 1f);
    }

    private void PaintWoodTaper(HumanSegment seg, float width, float length, Color bark, int depth, float heading)
    {
        float tip = WoodTipScale(depth);
        // One trapezoid (core + two cone edges). Apple 96 still stacked
        // three Taper bands — a ladder at 1x, the seam fused on Sway.
        // ConeFill is the same three boxes and reads as a bole. Collider
        // size unchanged. Oak fork 28° / visualTaper 0.58 stay.
        PaintVisualTaper(seg, width, length, bark, tip, 1, WoodBaseScale(depth));
        PaintBarkPlates(seg, width, length, depth);
        PaintTrunkButtress(seg, width, length, bark, depth);
        PaintBranchSpurs(seg, width, length, bark, depth, heading);
        // Rime follows PictureWidthAt. Small trees frost every stick;
        // Apple 96 keeps the bole (depth 0…2) so GO stay cheap.
        if (ResolvedSeason == PlantSeason.Winter && depth >= 0
            && (depth <= 2 || maxSegments <= 31))
            PaintTrunkRime(seg, width, length, depth);
    }

    // Picture only. Two cone chips at the leader base so the trunk
    // does not sit as a post on the stump (root flare is on the
    // stump, this is the bole). Apple 96 skips. Collider stays.
    private void PaintTrunkButtress(
        HumanSegment seg,
        float width,
        float length,
        Color bark,
        int depth)
    {
        if (depth != 0 || seg == null || seg.visual == null)
            return;
        if (maxSegments > 31)
            return;
        if (HeadlessTrial.Active && !HeadlessTrial.VisualCapture)
            return;
        float wAt = PictureWidthAt(width, 0.08f, 0);
        float tilt = WoodConeTiltDeg(width, length, 0);
        AddFlatPicture(
            seg.visual,
            "ButtressL",
            new Vector2(wAt * 0.32f, length * 0.18f),
            palBarkDark,
            seg.sortingOrder,
            new Vector3(-wAt * 0.34f, -length * 0.40f, 0f),
            -tilt,
            ArtLibrary.Bark);
        AddFlatPicture(
            seg.visual,
            "ButtressR",
            new Vector2(wAt * 0.28f, length * 0.16f),
            bark,
            seg.sortingOrder,
            new Vector3(wAt * 0.30f, -length * 0.38f, 0f),
            tilt,
            ArtLibrary.Bark);
        // Two cone chips without a distal lip stayed a plank at the
        // butts (Collar before CollarTip). Hang off the flare; collider
        // / fork 28 / taper 0.58 stay.
        AddFlatPicture(
            seg.visual,
            "ButtressLTip",
            new Vector2(wAt * 0.18f, length * 0.08f),
            bark,
            seg.sortingOrder,
            new Vector3(-wAt * 0.42f, -length * 0.44f, 0f),
            -tilt - 8f,
            ArtLibrary.Bark);
        AddFlatPicture(
            seg.visual,
            "ButtressRTip",
            new Vector2(wAt * 0.16f, length * 0.07f),
            palBarkLite,
            seg.sortingOrder,
            new Vector3(wAt * 0.38f, -length * 0.42f, 0f),
            tilt + 7f,
            ArtLibrary.Bark);
    }

    // Leftover: PaintVisualConeFill already draws the two edges.
    // Do not call from PaintWoodTaper — a second rim is a barcode.
    private void PaintTaperKeel(HumanSegment seg, float width, float length, Color bark, int depth)
    {
        if (seg == null || seg.visual == null)
            return;
        if (maxSegments > 31)
            return;
        if (HeadlessTrial.Active && !HeadlessTrial.VisualCapture)
            return;
        if (width < 0.004f || length < 0.004f)
            return;

        float baseW = width * WoodBaseScale(depth);
        float tipW = width * WoodTipScale(depth);
        float halfDrop = (baseW - tipW) * 0.5f;
        if (halfDrop < 0.004f)
            return;

        float edgeLen = Mathf.Sqrt(halfDrop * halfDrop + length * length);
        float rot = WoodConeTiltDeg(width, length, depth);
        float edgeW = Mathf.Max(0.010f, Mathf.Min(baseW, tipW) * 0.16f);
        float x = (baseW + tipW) * 0.25f;
        AddFlatPicture(
            seg.visual,
            "TaperKeelL",
            new Vector2(edgeW, edgeLen),
            bark,
            seg.sortingOrder,
            new Vector3(-x, 0f, 0f),
            -rot,
            ArtLibrary.Bark);
        AddFlatPicture(
            seg.visual,
            "TaperKeelR",
            new Vector2(edgeW, edgeLen),
            bark,
            seg.sortingOrder,
            new Vector3(x, 0f, 0f),
            rot,
            ArtLibrary.Bark);
    }

    // Picture only. Two cone-following buttresses plus soil chips so
    // the stump reads as a flare, not a plank. Collider stays
    // stumpWidth × stumpHeight.
    private void PaintRootFlare(HumanSegment stump)
    {
        if (rootFlareCount < 1 || stump == null || stump.visual == null)
            return;
        if (HeadlessTrial.Active && !HeadlessTrial.VisualCapture)
            return;

        float w = stump.size.x;
        float h = stump.size.y;
        float tilt = WoodConeTiltDeg(w, h, -1);
        // 1.58-wide RootSoil was a plank under every bole (cedar-slab
        // analog). Two soil chips sit at the butts, not a table.
        AddFlatPicture(
            stump.visual,
            "RootSoilL",
            new Vector2(w * 0.42f, h * 0.12f),
            palBarkDark,
            stump.sortingOrder,
            new Vector3(-w * 0.38f, -h * 0.46f, 0f),
            -6f,
            ArtLibrary.Bark);
        AddFlatPicture(
            stump.visual,
            "RootSoilR",
            new Vector2(w * 0.38f, h * 0.10f),
            palBarkDark,
            stump.sortingOrder,
            new Vector3(w * 0.36f, -h * 0.44f, 0f),
            5f,
            ArtLibrary.Bark);
        AddFlatPicture(
            stump.visual,
            "RootButtL",
            new Vector2(w * 0.22f, h * 0.58f),
            palStump,
            stump.sortingOrder,
            new Vector3(-w * 0.36f, -h * 0.16f, 0f),
            -tilt,
            ArtLibrary.Bark);
        AddFlatPicture(
            stump.visual,
            "RootButtR",
            new Vector2(w * 0.20f, h * 0.52f),
            palBarkLite,
            stump.sortingOrder,
            new Vector3(w * 0.34f, -h * 0.14f, 0f),
            tilt,
            ArtLibrary.Bark);
        // Soil chips and butts without a distal lip stayed a plank on
        // the slab after bole ButtressLTip (Collar before CollarTip).
        // Hang off the flare; collider / fork 28 / taper 0.58 stay.
        AddFlatPicture(
            stump.visual,
            "RootSoilLTip",
            new Vector2(w * 0.22f, h * 0.055f),
            palBarkLite,
            stump.sortingOrder,
            new Vector3(-w * 0.52f, -h * 0.48f, 0f),
            -12f,
            ArtLibrary.Bark);
        AddFlatPicture(
            stump.visual,
            "RootSoilRTip",
            new Vector2(w * 0.20f, h * 0.048f),
            palStump,
            stump.sortingOrder,
            new Vector3(w * 0.50f, -h * 0.46f, 0f),
            10f,
            ArtLibrary.Bark);
        AddFlatPicture(
            stump.visual,
            "RootButtLTip",
            new Vector2(w * 0.12f, h * 0.22f),
            palBarkLite,
            stump.sortingOrder,
            new Vector3(-w * 0.48f, -h * 0.38f, 0f),
            -tilt - 8f,
            ArtLibrary.Bark);
        AddFlatPicture(
            stump.visual,
            "RootButtRTip",
            new Vector2(w * 0.11f, h * 0.20f),
            palBarkDark,
            stump.sortingOrder,
            new Vector3(w * 0.46f, -h * 0.36f, 0f),
            tilt + 7f,
            ArtLibrary.Bark);
        if (ResolvedSeason == PlantSeason.Winter)
        {
            // 0.70×0.10 at 0° was a table on the butt (cedar-slab analog).
            // Two chips follow the cone, same language as TrunkRime.
            float frostTilt = WoodConeTiltDeg(w, h, -1);
            Color frost = new Color(0.76f, 0.78f, 0.80f, 0.80f);
            AddFlatPicture(
                stump.visual,
                "FrostCap0",
                new Vector2(w * 0.22f, h * 0.08f),
                frost,
                stump.sortingOrder + 1,
                // 0.28 at +0.12 inner −0.02 — a table on the butt
                // (TrunkRime0 analog). Right chip. Collider stays.
                new Vector3(w * 0.24f, h * 0.38f, 0f),
                frostTilt * 0.50f,
                null);
            AddFlatPicture(
                stump.visual,
                "FrostCap1",
                new Vector2(w * 0.18f, h * 0.06f),
                frost,
                stump.sortingOrder + 1,
                new Vector3(-w * 0.20f, h * 0.28f, 0f),
                -frostTilt * 0.40f,
                null);
            // Cap chips without a distal lip stayed a frost table on
            // the butt (TrunkRime before RimeTip). Hang off the cone.
            AddFlatPicture(
                stump.visual,
                "FrostCap0Tip",
                new Vector2(w * 0.12f, h * 0.04f),
                frost,
                stump.sortingOrder + 1,
                new Vector3(w * 0.34f, h * 0.42f, 0f),
                frostTilt * 0.70f,
                null);
            AddFlatPicture(
                stump.visual,
                "FrostCap1Tip",
                new Vector2(w * 0.10f, h * 0.035f),
                frost,
                stump.sortingOrder + 1,
                new Vector3(-w * 0.30f, h * 0.32f, 0f),
                -frostTilt * 0.55f,
                null);
        }
    }

    // Join pad at the parent tip plus a 0.88 fill on the daughter base.
    // A 14 cm Fork stick on parent.transform along the heading was a
    // third branch (T-bar at 1x). Oak fork 28° stays the hinge recipe.
    private void PaintBranchCrotch(
        HumanSegment parent,
        HumanSegment child,
        float relHeading,
        float childWidth,
        Color bark,
        int childDepth)
    {
        if (parent == null || parent.visual == null)
            return;
        if (HeadlessTrial.Active && !HeadlessTrial.VisualCapture)
            return;
        float parentTipW = PictureWidthAt(parent.size.x, 1f, childDepth - 1);
        float childBaseW = PictureWidthAt(childWidth, 0f, childDepth);
        float joinW = Mathf.Max(parentTipW, childBaseW);
        // Pad, not a stick. parent.size.y * 0.12 was 10 cm on the oak
        // bole — a short extra limb. 0.28 of relHeading left the web
        // on the parent axis (T-bar at 1x). Follow the daughter so a
        // 28° fork is a Y. Collider / hinge stay.
        float webLen = Mathf.Clamp(joinW * 0.48f, 0.016f, 0.048f);
        float webW = Mathf.Min(parentTipW, childBaseW) * 0.70f;
        float sideSign = relHeading >= 0f ? 1f : -1f;
        Vector3 webAt = new Vector3(
            sideSign * Mathf.Min(parentTipW * 0.18f, 0.028f),
            parent.size.y * 0.48f,
            0f);
        AddFlatPicture(
            parent.visual,
            parent.name + "_ForkWeb" + SegmentCount,
            new Vector2(webW, webLen),
            bark,
            parent.sortingOrder + 1,
            webAt,
            relHeading * 0.55f,
            ArtLibrary.Bark);
        if (maxSegments > 31 || child == null || child.visual == null)
            return;
        // 0.88 at 0.18 of fillW still spanned the daughter (outer
        // +0.32 of the belt) — a T-bar leftover after the comment
        // said inner crotch (pine Fill analog). Lip |x| > half so
        // a 28° fork stays a Y. Collider / hinge stay.
        float fillW = childBaseW * 0.36f;
        float fillH = Mathf.Clamp(joinW * 0.62f, 0.018f, 0.055f);
        Vector3 fillAt = new Vector3(
            -Mathf.Sign(relHeading) * fillW * 0.56f,
            -child.size.y * 0.46f,
            0f);
        AddFlatPicture(
            child.visual,
            child.name + "_ForkFill",
            new Vector2(fillW, fillH),
            bark,
            child.sortingOrder + 1,
            fillAt,
            -relHeading * 0.12f,
            ArtLibrary.Bark);
        // Rectangle web/fill without a distal chip stayed a T-bar at
        // fork 28 (bole Furrow without CorkTip analog). Lip hangs off
        // the pad; collider / hinge / visualTaper 0.58 stay.
        AddFlatPicture(
            parent.visual,
            parent.name + "_ForkWebLip" + SegmentCount,
            new Vector2(webW * 0.42f, webLen * 0.55f),
            palBarkDark,
            parent.sortingOrder + 1,
            webAt + new Vector3(
                sideSign * webW * 0.14f,
                webLen * 0.22f,
                0f),
            relHeading * 0.72f,
            ArtLibrary.Bark);
        AddFlatPicture(
            child.visual,
            child.name + "_ForkFillLip",
            new Vector2(fillW * 0.48f, fillH * 0.52f),
            palBarkDark,
            child.sortingOrder + 1,
            fillAt + new Vector3(
                -Mathf.Sign(relHeading) * fillW * 0.18f,
                -fillH * 0.22f,
                0f),
            -relHeading * 0.22f,
            ArtLibrary.Bark);
        // Visual 19:46 leftover: ForkFillTip still sat on the belt
        // after artist 19:32 (Cap before CapTip). Hang further off
        // the Y so the crotch is a join, not a seat. Collider /
        // hinge / fork 28 / taper 0.58 stay.
        AddFlatPicture(
            child.visual,
            child.name + "_ForkFillTip",
            new Vector2(fillW * 0.24f, fillH * 0.30f),
            palBarkLite,
            child.sortingOrder + 1,
            fillAt + new Vector3(
                -Mathf.Sign(relHeading) * fillW * 0.52f,
                -fillH * 0.62f,
                0f),
            -relHeading * 0.40f,
            ArtLibrary.Bark);
    }

    // Swelling at the child base so a side fork reads as a branch
    // collar, not a stick glued to a post. Collider stays the grow box.
    // Dense Static Apple (96) skips the extra GO.
    private void PaintBranchCollar(HumanSegment child, Color bark, int depth)
    {
        if (child == null || child.visual == null)
            return;
        if (maxSegments > 31)
            return;
        if (HeadlessTrial.Active && !HeadlessTrial.VisualCapture)
            return;
        float w = PictureWidthAt(child.size.x, 0f, depth) * 1.18f;
        float h = Mathf.Min(child.size.y * 0.20f, child.size.x * 1.6f);
        // 0.52 at −0.22 / 0.46 at +0.20 still crossed the daughter
        // (outer +0.04 / inner −0.03) — the 0° mushroom after two
        // chips (oak Flake2 analog). Flanks only. Collider stays.
        AddFlatPicture(
            child.visual,
            "CollarL",
            new Vector2(w * 0.28f, h * 0.88f),
            bark,
            child.sortingOrder,
            new Vector3(-w * 0.36f, -child.size.y * 0.42f, 0f),
            -8f,
            ArtLibrary.Bark);
        AddFlatPicture(
            child.visual,
            "CollarR",
            new Vector2(w * 0.24f, h * 0.78f),
            palBarkLite,
            child.sortingOrder,
            new Vector3(w * 0.34f, -child.size.y * 0.40f, 0f),
            7f,
            ArtLibrary.Bark);
        // Visual 19:46 leftover: 0.36/0.32 collars still a 0° mushroom
        // after ForkFillTip left the belt (bole Furrow without
        // CorkTip). Narrower flanks; lip hangs further off. Collider
        // / hinge / fork 28 / taper 0.58 stay.
        AddFlatPicture(
            child.visual,
            "CollarLTip",
            new Vector2(w * 0.16f, h * 0.36f),
            palBarkLite,
            child.sortingOrder,
            new Vector3(-w * 0.50f, -child.size.y * 0.48f, 0f),
            -16f,
            ArtLibrary.Bark);
        AddFlatPicture(
            child.visual,
            "CollarRTip",
            new Vector2(w * 0.14f, h * 0.32f),
            palBarkDark,
            child.sortingOrder,
            new Vector3(w * 0.48f, -child.size.y * 0.46f, 0f),
            14f,
            ArtLibrary.Bark);
    }

    // Picture-only side twigs on first-fork internodes. Grow forkAngle
    // (oak 28°) is the recipe, not a new hinge: the spur rotates by a
    // fraction of that angle so a still frame reads as extra branching,
    // not a bare pole between two slabs. Collider stays. Apple 96 skips.
    private int SpurCountFor(int depth)
    {
        if (maxSegments > 31)
            return 0;
        if (ResolvedKind == TreeKind.Pine || ResolvedKind == TreeKind.Cedar)
            return 0;
        // Bole: one epicormic twig so the trunk is not a telephone pole.
        // Depth 1 keeps the staggered pair. Oak fork 28° stays the recipe.
        if (depth >= growDepthCap)
            return 0;
        if (depth == 0)
            return 1;
        if (depth == 2)
        {
            // Last internode before the packed tuft. A bare stick there
            // reads as a telegraph pole under the crown. Willow curtain
            // / poplar flame / lilac Y-cane already fill; extra sticks
            // are a comb. Oak fork 28° stays the hinge recipe.
            if (ResolvedKind == TreeKind.Willow
                || ResolvedKind == TreeKind.Poplar
                || ResolvedKind == TreeKind.Lilac)
                return 0;
            return 1;
        }
        if (depth != 1)
            return 0;
        if (ResolvedKind == TreeKind.Willow)
            return 1;
        return 2;
    }

    private int SpurLeafletCount()
    {
        if (!growLeaves)
            return 0;
        if (ResolvedSeason == PlantSeason.Autumn)
            return 2;
        return 3;
    }

    private void PaintBranchSpurs(HumanSegment seg, float width, float length, Color bark, int depth, float heading)
    {
        int nSpur = SpurCountFor(depth);
        if (nSpur < 1 || seg == null)
            return;
        if (HeadlessTrial.Active && !HeadlessTrial.VisualCapture)
        {
            branchSpurCount += nSpur;
            TallySpurTufts(nSpur);
            return;
        }
        if (seg.visual == null)
        {
            branchSpurCount += nSpur;
            TallySpurTufts(nSpur);
            return;
        }

        Color spurBark = Color.Lerp(bark, palTwig, 0.35f);
        for (int spurI = 0; spurI < nSpur; spurI++)
        {
            // Same station ±0.62 forkAngle is a tuning-fork comb at 1x.
            // Stagger along the cone and break the mirror so internodes
            // read as extra branching. Oak fork 28° stays the grow recipe.
            bool bole = depth == 0;
            float alongSpur = bole
                ? 0.38f
                : (nSpur == 1 ? 0.40f : (spurI == 0 ? 0.32f : 0.54f));
            float wAt = PictureWidthAt(width, alongSpur, depth);
            float spurW = Mathf.Max(0.012f, wAt * (bole || spurI == 0 ? 0.42f : 0.30f));
            float spurH = length * (bole
                ? 0.22f
                : (ResolvedKind == TreeKind.Willow
                    ? 0.40f
                    : (spurI == 0 ? 0.30f : 0.18f)));
            float sign = bole || spurI == 0 ? -1f : 1f;
            // Grow laterals already keep going outward (10:33). Picture
            // spurs on that wood still used spurI==0 → −fork, so the first
            // internodal twig folded back to the axis (zigzag analog).
            // Spread limbs send both spurs out. Bole heading 0 keeps the
            // epicormic tick. Oak leader is fork 28 × 0.28 ≈ 7.8°, so
            // a >8° cut left that last internode folding back (the
            // same zigzag). Match OutwardSideHeading's SpreadLimbDeg.
            // Collider / hinge / fork 28 stay.
            bool spreadLimb = Mathf.Abs(heading) > SpreadLimbDeg;
            if (!bole && spreadLimb)
                sign = Mathf.Sign(heading);
            // Grow fork 28° is the hinge recipe. Picture spurs used to
            // sit at 14° on the bole — a telephone pole with a tick.
            // Clamp so poplar's 12° still reads as a twig and bush 40°
            // does not lie down. Collider / hinge stay.
            float forkAbs = Mathf.Abs(forkAngle);
            float rot;
            if (bole)
                rot = -Mathf.Clamp(forkAbs * 1.25f, 22f, 42f);
            else if (ResolvedKind == TreeKind.Willow)
            {
                float weep = spreadLimb ? Mathf.Sign(heading) : -1f;
                rot = weep * Mathf.Clamp(forkAbs * 0.70f, 18f, 36f);
            }
            else
                rot = sign * Mathf.Clamp(
                    forkAbs * (spurI == 0 ? 0.95f : 0.58f), 14f, 40f);
            Transform spur = AddFlatPicture(
                seg.visual,
                "Spur" + spurI,
                new Vector2(spurW, spurH),
                spurBark,
                seg.sortingOrder,
                new Vector3(sign * wAt * 0.22f, (alongSpur - 0.5f) * length, 0f),
                rot,
                ArtLibrary.Bark);
            RememberLeafSway(spur, (bole ? 0.28f : 0.42f) * HeadingSailMul(heading), bole ? 0.35f : 0.55f + spurI * 0.22f);
            // Uniform spur box was a telegraph stick between two
            // slabs (bole fence-post analog). Cone rides this
            // transform so LateUpdate flutter stays one cache.
            // visualTaper 0.58 / fork 28 stay on the grow wood.
            PaintPictureCone(spur, spurW, spurH, spurBark, 0.62f, seg.sortingOrder);
            PaintSpurTuft(spur, spurW, spurH, spurI);
            branchSpurCount++;
        }
    }

    // Headless without -visual still tallies the internodal tuft so
    // treeLeafClusterCount matches a painted Sway oak.
    private void TallySpurTufts(int nSpur)
    {
        if (nSpur < 1)
            return;
        if (growLeaves)
        {
            leafClusterCount += nSpur;
            LeafCount += nSpur * SpurLeafletCount();
        }
        else if (ResolvedSeason == PlantSeason.Winter)
            budCount += nSpur;
    }

    // Picture leaflets on a spur so a 28° fork reads as a branch, not
    // a bare brown stick between two slabs. Children ride the spur
    // sway cache — no extra LateUpdate Find. Extra boxes only.
    private void PaintSpurTuft(Transform spur, float spurW, float spurH, int spurI)
    {
        if (spur == null)
            return;
        if (!growLeaves)
        {
            if (ResolvedSeason != PlantSeason.Winter)
                return;
            budCount++;
            SpriteRenderer budSprite = spur.GetComponent<SpriteRenderer>();
            int budOrder = budSprite != null ? budSprite.sortingOrder + 1 : 2;
            // A 0° 1.6 cm dash was a tick, not a bud. Overlapping boxes
            // sit along the spur. spurW stays.
            AddFlatPicture(
                spur,
                spur.name + "_Bud",
                new Vector2(Mathf.Max(0.010f, spurW * 0.38f), Mathf.Max(0.018f, spurH * 0.16f)),
                Color.Lerp(palTip, palBarkLite, 0.35f),
                budOrder,
                // x=0.04 sat ON the spur spine after Tip left the
                // blade (ovate Petiole analog). Peak rides the left
                // pad. spurW stays.
                new Vector3(-spurW * 0.18f, spurH * 0.36f, 0f),
                18f,
                null);
            AddFlatPicture(
                spur,
                spur.name + "_BudTip",
                new Vector2(Mathf.Max(0.008f, spurW * 0.22f), Mathf.Max(0.012f, spurH * 0.10f)),
                palBarkLite,
                budOrder,
                new Vector3(-spurW * 0.12f, spurH * 0.48f, 0f),
                26f,
                null);
            AddFlatPicture(
                spur,
                spur.name + "_BudR",
                new Vector2(Mathf.Max(0.010f, spurW * 0.34f), Mathf.Max(0.016f, spurH * 0.14f)),
                Color.Lerp(palTip, palBarkLite, 0.28f),
                budOrder,
                // Left-only Bud was a one-tent winter spur after
                // summer TipR (oak Wrist analog). Far bud rides the
                // right pad. spurW stays.
                new Vector3(spurW * 0.16f, spurH * 0.32f, 0f),
                -16f,
                null);
            AddFlatPicture(
                spur,
                spur.name + "_BudTipR",
                new Vector2(Mathf.Max(0.008f, spurW * 0.20f), Mathf.Max(0.010f, spurH * 0.08f)),
                palBarkLite,
                budOrder,
                new Vector3(spurW * 0.10f, spurH * 0.44f, 0f),
                -22f,
                null);
            return;
        }

        leafClusterCount++;
        SpriteRenderer tuftSprite = spur.GetComponent<SpriteRenderer>();
        int tuftOrder = tuftSprite != null ? tuftSprite.sortingOrder + 1 : 2;
        Color tuftColor = LeafTint(4 + spurI, spurI == 1);
        // Pair of side cards was a comb. Terminal plus laterals.
        // Autumn keeps the terminal and one lateral so the spur is not
        // a summer trifoliate tinted rust.
        int nLeaflet = SpurLeafletCount();
        for (int tuftK = 0; tuftK < nLeaflet; tuftK++)
        {
            bool terminal = tuftK == nLeaflet - 1;
            float tuftSign = terminal ? -1f : (tuftK == 0 ? -1f : 1f);
            float hang = leafHangWorldDown
                ? tuftSign * spurW * 0.18f
                : tuftSign * spurW * (terminal ? 0.36f : 0.42f);
            float along = leafHangWorldDown
                ? spurH * (0.18f + 0.16f * tuftK)
                : spurH * (terminal ? 0.46f : 0.32f);
            float tilt = leafHangWorldDown
                ? tuftSign * 8f
                : (terminal ? 4f : tuftSign * (leafTilt * 0.50f + tuftK * 6f));
            if (!leafHangWorldDown)
            {
                float petioleReach = Mathf.Sqrt(hang * hang + along * along);
                Transform petioleXform = AddFlatPicture(
                    spur,
                    spur.name + "_Petiole" + tuftK,
                    new Vector2(
                        Mathf.Max(0.006f, spurW * 0.18f),
                        Mathf.Max(0.010f, petioleReach * 0.42f)),
                    Color.Lerp(tuftColor, palTwig, 0.40f),
                    tuftOrder,
                    new Vector3(hang * 0.35f, along * 0.35f, 0f),
                    0f,
                    ArtLibrary.Bark);
                AimPictureAlongY(petioleXform, new Vector3(hang, along, 0f), 0.35f);
                Transform petioleTipXform = AddFlatPicture(
                    spur,
                    spur.name + "_Petiole" + tuftK + "Tip",
                    new Vector2(
                        Mathf.Max(0.005f, spurW * 0.10f),
                        Mathf.Max(0.008f, petioleReach * 0.16f)),
                    Color.Lerp(tuftColor, palTwig, 0.40f),
                    tuftOrder,
                    new Vector3(hang * 0.52f, along * 0.52f, 0f),
                    0f,
                    ArtLibrary.Bark);
                // Ovate PetioleTip closed the leaf stalk. Spur Petiole
                // without a lip stayed an internodal stick after
                // FloretTip (CarmB before CarmBTip). Hang further
                // along +Y; leafSize stays.
                AimPictureAlongY(petioleTipXform, new Vector3(hang, along, 0f), 0.52f);
            }
            // Equal 0.34×0.40 cards were a comb of green tiles on the
            // internodal stick. Kind aspect: oak palmate, poplar kite,
            // else a short ovate. Extra boxes ride the spur cache.
            float bladeW = leafSize.x * (terminal ? 0.26f : 0.32f);
            float bladeH = leafSize.y * (terminal ? 0.52f : 0.40f);
            if (leafLobes >= 2)
            {
                bladeW = leafSize.x * (terminal ? 0.30f : 0.28f);
                bladeH = leafSize.y * (terminal ? 0.34f : 0.22f);
                tilt = terminal ? 6f : tuftSign * 32f;
            }
            else if (ResolvedKind == TreeKind.Poplar)
            {
                bladeW = leafSize.x * (terminal ? 0.42f : 0.28f);
                bladeH = leafSize.y * (terminal ? 0.36f : 0.26f);
                tilt = terminal ? 8f : tuftSign * 28f;
            }
            Transform blade = AddFlatPicture(
                spur,
                spur.name + "_Tuft" + tuftK,
                new Vector2(bladeW, bladeH),
                CrownPatch(tuftColor, !terminal),
                tuftOrder,
                new Vector3(hang, along, 0f),
                tilt,
                ArtLibrary.Leaf);
            if (blade != null && !leafHangWorldDown)
            {
                // Packed ovate/kite hid the grow paddle so Base+CheekR
                // own 1x. Spur left that card on and extras rode the
                // left pad — a comma after plant CheekR 16:21. Hide
                // on the Sway cap; Apple 96 keeps the tile. leafSize stays.
                if (maxSegments <= 31)
                    HidePictureBody(blade);
                if (leafLobes >= 2 && maxSegments <= 31)
                {
                    // Size/tilt alone left the spur a green tile. Side
                    // lobe sits OUT of the blade, so the sinus is empty.
                    // Tip at (0.06, 0.38) sat IN that sinus (packed
                    // LobeTip analog). After Hide, Lobe at −0.42 was a
                    // left mitten. Lobe kisses x=0; LobeR is the far
                    // half. Peak rides Lobe. leafSize stays.
                    AddFlatPicture(
                        blade,
                        blade.name + "_Lobe",
                        new Vector2(bladeW * 0.52f, bladeH * 0.70f),
                        tuftColor,
                        tuftOrder,
                        new Vector3(-bladeW * 0.26f, bladeH * 0.04f, 0f),
                        -38f,
                        ArtLibrary.Leaf);
                    AddFlatPicture(
                        blade,
                        blade.name + "_Lobe0Tip",
                        new Vector2(bladeW * 0.24f, bladeH * 0.22f),
                        tuftColor,
                        tuftOrder,
                        // Leaflet LobeL0 without a lip stayed a blunt
                        // inner lobe after LobeL1Tip closed the outer
                        // tooth (Cap before CapTip). Hang off Lobe;
                        // sinus stays empty. leafSize stays.
                        new Vector3(-bladeW * 0.42f, bladeH * 0.10f, 0f),
                        -46f,
                        ArtLibrary.Leaf);
                    AddFlatPicture(
                        blade,
                        blade.name + "_LobeR",
                        new Vector2(bladeW * 0.48f, bladeH * 0.58f),
                        CrownPatch(tuftColor, true),
                        tuftOrder,
                        new Vector3(bladeW * 0.24f, bladeH * 0.02f, 0f),
                        18f,
                        ArtLibrary.Leaf);
                    AddFlatPicture(
                        blade,
                        blade.name + "_LobeR0Tip",
                        new Vector2(bladeW * 0.22f, bladeH * 0.18f),
                        CrownPatch(tuftColor, true),
                        tuftOrder,
                        new Vector3(bladeW * 0.38f, bladeH * 0.08f, 0f),
                        24f,
                        ArtLibrary.Leaf);
                    AddFlatPicture(
                        blade,
                        blade.name + "_Tip",
                        new Vector2(bladeW * 0.48f, bladeH * 0.42f),
                        tuftColor,
                        tuftOrder,
                        new Vector3(-bladeW * 0.26f, bladeH * 0.28f, 0f),
                        -12f,
                        ArtLibrary.Leaf);
                    AddFlatPicture(
                        blade,
                        blade.name + "_TipTip",
                        new Vector2(bladeW * 0.24f, bladeH * 0.16f),
                        tuftColor,
                        tuftOrder,
                        // WristRTip closed the far stalk. Packed palmate
                        // Tip without a lip stayed a peak paddle after
                        // BladeTip (Cap before CapTip). Hang off Tip;
                        // sinus stays empty. leafSize stays.
                        new Vector3(-bladeW * 0.34f, bladeH * 0.42f, 0f),
                        -18f,
                        ArtLibrary.Leaf);
                    AddFlatPicture(
                        blade,
                        blade.name + "_TipR",
                        new Vector2(bladeW * 0.42f, bladeH * 0.36f),
                        CrownPatch(tuftColor, true),
                        tuftOrder,
                        // Left-only Tip was a one-tent palmate spur
                        // after Lobe/LobeR (packed poplar TipR; oak
                        // skipped). Left edge 0. leafSize stays.
                        new Vector3(bladeW * 0.24f, bladeH * 0.24f, 0f),
                        10f,
                        ArtLibrary.Leaf);
                    AddFlatPicture(
                        blade,
                        blade.name + "_TipRTip",
                        new Vector2(bladeW * 0.20f, bladeH * 0.14f),
                        CrownPatch(tuftColor, true),
                        tuftOrder,
                        // TipTip closed the near peak. Packed TipR
                        // without a lip stayed a far paddle after
                        // WristRTip (Drop before DropTip). Hang off
                        // TipR; sinus stays empty. leafSize stays.
                        new Vector3(bladeW * 0.32f, bladeH * 0.36f, 0f),
                        16f,
                        ArtLibrary.Leaf);
                    AddFlatPicture(
                        blade,
                        blade.name + "_Wrist",
                        new Vector2(bladeW * 0.22f, bladeH * 0.28f),
                        tuftColor,
                        tuftOrder,
                        new Vector3(-bladeW * 0.26f, -bladeH * 0.28f, 0f),
                        6f,
                        ArtLibrary.Leaf);
                    AddFlatPicture(
                        blade,
                        blade.name + "_WristTip",
                        new Vector2(bladeW * 0.12f, bladeH * 0.12f),
                        tuftColor,
                        tuftOrder,
                        // Leaflet WristTip closed the palmate stalk.
                        // Packed Wrist without a lip stayed a petiole
                        // stick after Lobe0Tip (CarmB before CarmBTip).
                        // Hang off the caudal pad; sinus stays empty.
                        // leafSize stays.
                        new Vector3(-bladeW * 0.26f, -bladeH * 0.40f, 0f),
                        10f,
                        ArtLibrary.Leaf);
                    AddFlatPicture(
                        blade,
                        blade.name + "_WristR",
                        new Vector2(bladeW * 0.18f, bladeH * 0.22f),
                        CrownPatch(tuftColor, true),
                        tuftOrder,
                        // Packed palmate skipped caudal after TipR
                        // (full leaf Wrist before WristR). Far join
                        // rides LobeR; sinus stays empty. leafSize stays.
                        new Vector3(bladeW * 0.24f, -bladeH * 0.24f, 0f),
                        -6f,
                        ArtLibrary.Leaf);
                    AddFlatPicture(
                        blade,
                        blade.name + "_WristRTip",
                        new Vector2(bladeW * 0.10f, bladeH * 0.10f),
                        CrownPatch(tuftColor, true),
                        tuftOrder,
                        // WristTip closed the near palmate stalk.
                        // Packed WristR without a lip stayed a far
                        // petiole card after BaseTip (CarmB before
                        // CarmBTip). Hang off LobeR; sinus stays
                        // empty. leafSize stays.
                        new Vector3(bladeW * 0.24f, -bladeH * 0.34f, 0f),
                        -10f,
                        ArtLibrary.Leaf);
                    AddFlatPicture(
                        blade,
                        blade.name + "_ToothL",
                        new Vector2(bladeW * 0.22f, bladeH * 0.22f),
                        tuftColor,
                        tuftOrder,
                        // Full palmate ToothL sits on LobeL0. Packed
                        // spur stopped at WristR — a mitten on the
                        // left. Outer tooth on Lobe; sinus stays
                        // empty. leafSize stays.
                        new Vector3(-bladeW * 0.40f, bladeH * 0.12f, 0f),
                        -32f,
                        ArtLibrary.Leaf);
                    AddFlatPicture(
                        blade,
                        blade.name + "_ToothLTip",
                        new Vector2(bladeW * 0.12f, bladeH * 0.12f),
                        tuftColor,
                        tuftOrder,
                        new Vector3(-bladeW * 0.50f, bladeH * 0.14f, 0f),
                        -38f,
                        ArtLibrary.Leaf);
                    AddFlatPicture(
                        blade,
                        blade.name + "_LobeL1",
                        new Vector2(bladeW * 0.18f, bladeH * 0.16f),
                        tuftColor,
                        tuftOrder,
                        // Leaflet LobeL1 sits past ToothL. Packed
                        // spur stopped at ToothL after LobeR1 closed
                        // the right — a mitten on the left. Outer
                        // pad; sinus stays empty. leafSize stays.
                        new Vector3(-bladeW * 0.52f, bladeH * 0.10f, 0f),
                        -38f,
                        ArtLibrary.Leaf);
                    AddFlatPicture(
                        blade,
                        blade.name + "_FingerL",
                        new Vector2(bladeW * 0.16f, bladeH * 0.14f),
                        tuftColor,
                        tuftOrder,
                        // Full palmate FingerL sits between ToothL
                        // and LobeL1. Packed stopped at LobeL1 — a
                        // mitten on the left (leaflet FingerL).
                        // Inner tooth on Lobe; sinus stays empty.
                        // leafSize stays.
                        new Vector3(-bladeW * 0.46f, bladeH * 0.11f, 0f),
                        -35f,
                        ArtLibrary.Leaf);
                    AddFlatPicture(
                        blade,
                        blade.name + "_FingerLTip",
                        new Vector2(bladeW * 0.10f, bladeH * 0.08f),
                        tuftColor,
                        tuftOrder,
                        new Vector3(-bladeW * 0.56f, bladeH * 0.13f, 0f),
                        -41f,
                        ArtLibrary.Leaf);
                    AddFlatPicture(
                        blade,
                        blade.name + "_LobeL1Tip",
                        new Vector2(bladeW * 0.12f, bladeH * 0.10f),
                        tuftColor,
                        tuftOrder,
                        // Crown LobeL1 without a lip stayed a blunt
                        // outer lobe (Cap before CapTip). Hang off
                        // LobeL1; sinus stays empty. leafSize stays.
                        new Vector3(-bladeW * 0.62f, bladeH * 0.12f, 0f),
                        -44f,
                        ArtLibrary.Leaf);
                    AddFlatPicture(
                        blade,
                        blade.name + "_ToothR",
                        new Vector2(bladeW * 0.20f, bladeH * 0.20f),
                        CrownPatch(tuftColor, true),
                        tuftOrder,
                        new Vector3(bladeW * 0.38f, bladeH * 0.10f, 0f),
                        28f,
                        ArtLibrary.Leaf);
                    AddFlatPicture(
                        blade,
                        blade.name + "_ToothRTip",
                        new Vector2(bladeW * 0.12f, bladeH * 0.10f),
                        CrownPatch(tuftColor, true),
                        tuftOrder,
                        new Vector3(bladeW * 0.48f, bladeH * 0.12f, 0f),
                        34f,
                        ArtLibrary.Leaf);
                    AddFlatPicture(
                        blade,
                        blade.name + "_LobeR1",
                        new Vector2(bladeW * 0.18f, bladeH * 0.16f),
                        CrownPatch(tuftColor, true),
                        tuftOrder,
                        // Full palmate LobeR1 sits past ToothR. Packed
                        // spur stopped at ToothR — a mitten on the
                        // right (crown FingerR without LobeR1). Outer
                        // pad; sinus stays empty. leafSize stays.
                        new Vector3(bladeW * 0.50f, bladeH * 0.08f, 0f),
                        34f,
                        ArtLibrary.Leaf);
                    AddFlatPicture(
                        blade,
                        blade.name + "_FingerR",
                        new Vector2(bladeW * 0.14f, bladeH * 0.12f),
                        CrownPatch(tuftColor, true),
                        tuftOrder,
                        // Full palmate FingerR sits between ToothR
                        // and LobeR1. Packed stopped at LobeR1 — a
                        // mitten on the right. Outer tooth; sinus
                        // stays empty. leafSize stays.
                        new Vector3(bladeW * 0.44f, bladeH * 0.09f, 0f),
                        31f,
                        ArtLibrary.Leaf);
                    AddFlatPicture(
                        blade,
                        blade.name + "_FingerRTip",
                        new Vector2(bladeW * 0.08f, bladeH * 0.08f),
                        CrownPatch(tuftColor, true),
                        tuftOrder,
                        new Vector3(bladeW * 0.54f, bladeH * 0.11f, 0f),
                        37f,
                        ArtLibrary.Leaf);
                    AddFlatPicture(
                        blade,
                        blade.name + "_LobeR1Tip",
                        new Vector2(bladeW * 0.12f, bladeH * 0.10f),
                        CrownPatch(tuftColor, true),
                        tuftOrder,
                        new Vector3(bladeW * 0.60f, bladeH * 0.10f, 0f),
                        40f,
                        ArtLibrary.Leaf);
                }
                else if (maxSegments <= 31 && ResolvedKind != TreeKind.Poplar)
                {
                    // Tip-only card was still a tile on orchard / hedge
                    // (spray lance analog). After Hide, Waist/Tip at
                    // −0.22 stayed a left comma (packed ovate before
                    // CheekR). Base kisses x=0; CheekR is the far egg.
                    // leafSize stays.
                    AddFlatPicture(
                        blade,
                        blade.name + "_Base",
                        new Vector2(bladeW * 0.52f, bladeH * 0.78f),
                        tuftColor,
                        tuftOrder,
                        new Vector3(-bladeW * 0.26f, 0f, 0f),
                        -8f,
                        ArtLibrary.Leaf);
                    AddFlatPicture(
                        blade,
                        blade.name + "_BaseTip",
                        new Vector2(bladeW * 0.26f, bladeH * 0.22f),
                        tuftColor,
                        tuftOrder,
                        // WaistTip closed the packed midrib. Ovate Base
                        // without a lip stayed a spur egg after
                        // KiteL2Tip (Cap before CapTip). Hang off Base;
                        // kiss at x=0 stays. leafSize stays.
                        new Vector3(-bladeW * 0.40f, -bladeH * 0.18f, 0f),
                        -14f,
                        ArtLibrary.Leaf);
                    AddFlatPicture(
                        blade,
                        blade.name + "_CheekR",
                        new Vector2(bladeW * 0.50f, bladeH * 0.70f),
                        CrownPatch(tuftColor, true),
                        tuftOrder,
                        new Vector3(bladeW * 0.25f, 0f, 0f),
                        8f,
                        ArtLibrary.Leaf);
                    AddFlatPicture(
                        blade,
                        blade.name + "_CheekRTip",
                        new Vector2(bladeW * 0.24f, bladeH * 0.20f),
                        CrownPatch(tuftColor, true),
                        tuftOrder,
                        // BaseTip closed the near egg. Far CheekR
                        // without a lip stayed a coin (Drop before
                        // DropTip). Hang off CheekR; kiss at x=0 stays.
                        // leafSize stays.
                        new Vector3(bladeW * 0.38f, -bladeH * 0.16f, 0f),
                        14f,
                        ArtLibrary.Leaf);
                    AddFlatPicture(
                        blade,
                        blade.name + "_Waist",
                        new Vector2(bladeW * 0.28f, bladeH * 0.28f),
                        CrownPatch(tuftColor, true),
                        tuftOrder,
                        new Vector3(-bladeW * 0.26f, bladeH * 0.04f, 0f),
                        4f,
                        ArtLibrary.Leaf);
                    AddFlatPicture(
                        blade,
                        blade.name + "_WaistTip",
                        new Vector2(bladeW * 0.14f, bladeH * 0.12f),
                        CrownPatch(tuftColor, true),
                        tuftOrder,
                        // KiteL2Tip closed the hem. Packed ovate Waist
                        // without a lip stayed a midrib card after
                        // ApexTip (Cap before CapTip). Hang off Waist;
                        // kiss at x=0 stays. leafSize stays.
                        new Vector3(-bladeW * 0.34f, bladeH * 0.12f, 0f),
                        10f,
                        ArtLibrary.Leaf);
                    AddFlatPicture(
                        blade,
                        blade.name + "_Tip",
                        new Vector2(bladeW * 0.32f, bladeH * 0.36f),
                        tuftColor,
                        tuftOrder,
                        new Vector3(-bladeW * 0.26f, bladeH * 0.28f, 0f),
                        -8f,
                        ArtLibrary.Leaf);
                    AddFlatPicture(
                        blade,
                        blade.name + "_TipTip",
                        new Vector2(bladeW * 0.16f, bladeH * 0.14f),
                        tuftColor,
                        tuftOrder,
                        // Packed palmate TipTip closed the oak peak.
                        // Packed ovate Tip without a lip stayed a spur
                        // coin after CheekRTip (Cap before CapTip).
                        // Hang off Tip; kiss at x=0 stays. leafSize stays.
                        new Vector3(-bladeW * 0.34f, bladeH * 0.40f, 0f),
                        -14f,
                        ArtLibrary.Leaf);
                    AddFlatPicture(
                        blade,
                        blade.name + "_TipR",
                        new Vector2(bladeW * 0.28f, bladeH * 0.30f),
                        CrownPatch(tuftColor, true),
                        tuftOrder,
                        // Left-only Tip was a one-tent ovate spur after
                        // Base/CheekR (packed poplar kite TipR analog).
                        // Left edge 0. leafSize stays.
                        new Vector3(bladeW * 0.25f, bladeH * 0.24f, 0f),
                        8f,
                        ArtLibrary.Leaf);
                    AddFlatPicture(
                        blade,
                        blade.name + "_TipRTip",
                        new Vector2(bladeW * 0.14f, bladeH * 0.12f),
                        CrownPatch(tuftColor, true),
                        tuftOrder,
                        // TipTip closed the near ovate peak. Packed
                        // TipR without a lip stayed a far coin after
                        // CheekRTip (Drop before DropTip). Hang off
                        // TipR; leafSize stays.
                        new Vector3(bladeW * 0.33f, bladeH * 0.34f, 0f),
                        14f,
                        ArtLibrary.Leaf);
                    AddFlatPicture(
                        blade,
                        blade.name + "_WaistR",
                        new Vector2(bladeW * 0.24f, bladeH * 0.24f),
                        CrownPatch(tuftColor, true),
                        tuftOrder,
                        // Left-only packed Waist was a one-tent ovate
                        // midrib after TipR/CheekR (leaflet Waist
                        // before WaistR). Far chip rides CheekR.
                        // leafSize stays.
                        new Vector3(bladeW * 0.25f, bladeH * 0.04f, 0f),
                        -4f,
                        ArtLibrary.Leaf);
                    AddFlatPicture(
                        blade,
                        blade.name + "_WaistRTip",
                        new Vector2(bladeW * 0.12f, bladeH * 0.10f),
                        CrownPatch(tuftColor, true),
                        tuftOrder,
                        new Vector3(bladeW * 0.33f, bladeH * 0.10f, 0f),
                        -10f,
                        ArtLibrary.Leaf);
                }
                else if (maxSegments <= 31)
                {
                    // Poplar spur had no far kite; Tip 0.06 was a mast
                    // on the kite (packed Tip analog). After Hide, Tip
                    // at −0.22 was a left comma (crown flame analog).
                    // KiteL/KiteR kiss at x=0. leafSize stays.
                    AddFlatPicture(
                        blade,
                        blade.name + "_KiteL",
                        new Vector2(bladeW * 0.52f, bladeH * 0.70f),
                        tuftColor,
                        tuftOrder,
                        new Vector3(-bladeW * 0.26f, 0f, 0f),
                        -18f,
                        ArtLibrary.Leaf);
                    AddFlatPicture(
                        blade,
                        blade.name + "_KiteLTip",
                        new Vector2(bladeW * 0.26f, bladeH * 0.24f),
                        tuftColor,
                        tuftOrder,
                        // Crown KiteLTip closed the flame pad. Packed
                        // KiteL without a lip stayed a blunt spur kite
                        // after ApexTip (Cap before CapTip). Hang off
                        // the left kite; kiss at x=0 stays. leafSize stays.
                        new Vector3(-bladeW * 0.42f, bladeH * 0.12f, 0f),
                        -24f,
                        ArtLibrary.Leaf);
                    AddFlatPicture(
                        blade,
                        blade.name + "_KiteR",
                        new Vector2(bladeW * 0.50f, bladeH * 0.56f),
                        CrownPatch(tuftColor, true),
                        tuftOrder,
                        new Vector3(bladeW * 0.25f, 0f, 0f),
                        12f,
                        ArtLibrary.Leaf);
                    AddFlatPicture(
                        blade,
                        blade.name + "_KiteRTip",
                        new Vector2(bladeW * 0.24f, bladeH * 0.20f),
                        CrownPatch(tuftColor, true),
                        tuftOrder,
                        new Vector3(bladeW * 0.40f, bladeH * 0.10f, 0f),
                        18f,
                        ArtLibrary.Leaf);
                    AddFlatPicture(
                        blade,
                        blade.name + "_Tip",
                        new Vector2(bladeW * 0.52f, bladeH * 0.36f),
                        tuftColor,
                        tuftOrder,
                        // Width 0.52 kisses x=0; TipR is the far peak
                        // (packed leaflet ApexR analog). leafSize stays.
                        new Vector3(-bladeW * 0.26f, bladeH * 0.28f, 0f),
                        -12f,
                        ArtLibrary.Leaf);
                    AddFlatPicture(
                        blade,
                        blade.name + "_TipTip",
                        new Vector2(bladeW * 0.26f, bladeH * 0.14f),
                        tuftColor,
                        tuftOrder,
                        // Packed ovate TipTip closed the spur egg peak.
                        // Packed kite Tip without a lip stayed a flame
                        // paddle after WristRTip (Cap before CapTip).
                        // Hang off Tip; valley stays the kites. leafSize stays.
                        new Vector3(-bladeW * 0.34f, bladeH * 0.40f, 0f),
                        -18f,
                        ArtLibrary.Leaf);
                    AddFlatPicture(
                        blade,
                        blade.name + "_TipR",
                        new Vector2(bladeW * 0.48f, bladeH * 0.30f),
                        CrownPatch(tuftColor, true),
                        tuftOrder,
                        // Left-only Tip was a one-tent spur kite after
                        // KiteL/R kissed. Left edge 0. leafSize stays.
                        new Vector3(bladeW * 0.24f, bladeH * 0.24f, 0f),
                        8f,
                        ArtLibrary.Leaf);
                    AddFlatPicture(
                        blade,
                        blade.name + "_TipRTip",
                        new Vector2(bladeW * 0.24f, bladeH * 0.12f),
                        CrownPatch(tuftColor, true),
                        tuftOrder,
                        // TipTip closed the near kite peak. Packed TipR
                        // without a lip stayed a far paddle after
                        // WristRTip (Drop before DropTip). Hang off
                        // TipR; leafSize stays.
                        new Vector3(bladeW * 0.32f, bladeH * 0.34f, 0f),
                        14f,
                        ArtLibrary.Leaf);
                    AddFlatPicture(
                        blade,
                        blade.name + "_Waist",
                        new Vector2(bladeW * 0.28f, bladeH * 0.24f),
                        CrownPatch(tuftColor, true),
                        tuftOrder,
                        new Vector3(-bladeW * 0.26f, -bladeH * 0.08f, 0f),
                        4f,
                        ArtLibrary.Leaf);
                    AddFlatPicture(
                        blade,
                        blade.name + "_WaistTip",
                        new Vector2(bladeW * 0.14f, bladeH * 0.10f),
                        CrownPatch(tuftColor, true),
                        tuftOrder,
                        // KiteL2Tip closed the hem. Packed kite Waist
                        // without a lip stayed a midrib card after
                        // ApexTip (Cap before CapTip). Hang off Waist;
                        // valley stays the kites. leafSize stays.
                        new Vector3(-bladeW * 0.34f, -bladeH * 0.16f, 0f),
                        10f,
                        ArtLibrary.Leaf);
                    AddFlatPicture(
                        blade,
                        blade.name + "_WaistR",
                        new Vector2(bladeW * 0.24f, bladeH * 0.20f),
                        CrownPatch(tuftColor, true),
                        tuftOrder,
                        // Packed kite skipped midrib after TipR (crown
                        // Waist before WaistR). Far chip rides KiteR;
                        // valley stays the kites. leafSize stays.
                        new Vector3(bladeW * 0.25f, -bladeH * 0.06f, 0f),
                        -4f,
                        ArtLibrary.Leaf);
                    AddFlatPicture(
                        blade,
                        blade.name + "_WaistRTip",
                        new Vector2(bladeW * 0.12f, bladeH * 0.08f),
                        CrownPatch(tuftColor, true),
                        tuftOrder,
                        new Vector3(bladeW * 0.33f, -bladeH * 0.12f, 0f),
                        -10f,
                        ArtLibrary.Leaf);
                }
                else if (ResolvedKind != TreeKind.Poplar)
                {
                    // Apple 96: keep the tile. Waist/Tip on the left
                    // pad, no Hide (GO budget). fruitSize stays.
                    AddFlatPicture(
                        blade,
                        blade.name + "_Waist",
                        new Vector2(bladeW * 0.28f, bladeH * 0.28f),
                        CrownPatch(tuftColor, true),
                        tuftOrder,
                        new Vector3(-bladeW * 0.22f, bladeH * 0.04f, 0f),
                        4f,
                        ArtLibrary.Leaf);
                    AddFlatPicture(
                        blade,
                        blade.name + "_WaistTip",
                        new Vector2(bladeW * 0.14f, bladeH * 0.12f),
                        CrownPatch(tuftColor, true),
                        tuftOrder,
                        // KiteL2Tip closed the hem. Apple-96 Waist
                        // without a lip stayed a tile midrib (Cap
                        // before CapTip). Hang off Waist; fruitSize stays.
                        new Vector3(-bladeW * 0.30f, bladeH * 0.12f, 0f),
                        10f,
                        ArtLibrary.Leaf);
                    AddFlatPicture(
                        blade,
                        blade.name + "_Tip",
                        new Vector2(bladeW * 0.32f, bladeH * 0.36f),
                        tuftColor,
                        tuftOrder,
                        new Vector3(-bladeW * 0.22f, bladeH * 0.28f, 0f),
                        -8f,
                        ArtLibrary.Leaf);
                    AddFlatPicture(
                        blade,
                        blade.name + "_TipTip",
                        new Vector2(bladeW * 0.16f, bladeH * 0.14f),
                        tuftColor,
                        tuftOrder,
                        // Packed ovate TipTip closed the spur peak.
                        // Apple-96 Tip without a lip stayed a tile
                        // paddle after WaistTip (Cap before CapTip).
                        // Hang off Tip; fruitSize stays. No TipR
                        // (GO budget).
                        new Vector3(-bladeW * 0.28f, bladeH * 0.40f, 0f),
                        -14f,
                        ArtLibrary.Leaf);
                }
            }
            LeafCount++;
        }
    }

    // Winter rime on the trunk picture. Collider stays. Not a force.
    // Width follows the bole cone — a collider-sized dash hung off the
    // taper the same way bark did before 04:41.
    private void PaintTrunkRime(HumanSegment trunk, float width, float length, int depth)
    {
        if (trunk == null || trunk.visual == null)
            return;
        if (HeadlessTrial.Active && !HeadlessTrial.VisualCapture)
            return;
        float along = depth <= 0 ? 0.70f : 0.58f;
        float wAt = PictureWidthAt(width, along, depth);
        float dashH = length * (depth <= 0 ? 0.08f : 0.055f);
        float a = depth <= 0 ? 0.70f : 0.55f;
        Color rime = new Color(0.78f, 0.80f, 0.82f, a);
        float tilt = WoodConeTiltDeg(width, length, depth);
        // A 0° dash on the cone was a barcode (oak BarkRidge analog).
        // Two short chips follow the taper. Collider stays.
        AddFlatPicture(
            trunk.visual,
            "TrunkRime0",
            new Vector2(wAt * 0.22f, dashH),
            rime,
            trunk.sortingOrder + 1,
            // 0.28 at +0.14 inner 0 — a frost barcode on the bole
            // (CollarL analog). Right flank. Collider stays.
            new Vector3(wAt * 0.22f, (along - 0.5f) * length, 0f),
            tilt * 0.55f,
            null);
        AddFlatPicture(
            trunk.visual,
            "TrunkRime1",
            new Vector2(wAt * 0.18f, dashH * 0.78f),
            rime,
            trunk.sortingOrder + 1,
            // 0.22 at −0.10 outer +0.01 still kissed the spine.
            // Left flank opposite Rime0. Collider stays.
            new Vector3(-wAt * 0.18f, (along - 0.62f) * length, 0f),
            -tilt * 0.40f,
            null);
        // Rime chips without a distal lip stayed a frost barcode on
        // the bole after CollarLTip (Furrow before CorkTip). Hang off
        // the flank; collider / fork 28 / taper 0.58 stay.
        AddFlatPicture(
            trunk.visual,
            "TrunkRime0Tip",
            new Vector2(wAt * 0.12f, dashH * 0.45f),
            rime,
            trunk.sortingOrder + 1,
            new Vector3(wAt * 0.32f, (along - 0.5f) * length + dashH * 0.12f, 0f),
            tilt * 0.72f,
            null);
        AddFlatPicture(
            trunk.visual,
            "TrunkRime1Tip",
            new Vector2(wAt * 0.10f, dashH * 0.38f),
            rime,
            trunk.sortingOrder + 1,
            new Vector3(-wAt * 0.28f, (along - 0.62f) * length + dashH * 0.08f, 0f),
            -tilt * 0.55f,
            null);
    }

    // Same cone as PaintWoodTaper. Plates must sit on the picture bole —
    // full collider width hung off the 04:41 stump join as stickers.
    // Depth ≥ 1 also starts at the parent tip, not the grow box.
    private float PictureWidthAt(float width, float along, int depth)
    {
        return Mathf.Lerp(
            width * WoodBaseScale(depth),
            width * WoodTipScale(depth),
            Mathf.Clamp01(along));
    }

    // Edge slope of the picture cone, degrees. Bark ridges follow this
    // so a still frame is a taper, not a barcode of vertical strips.
    private float WoodConeTiltDeg(float width, float length, int depth)
    {
        float halfDrop = (width * WoodBaseScale(depth) - width * WoodTipScale(depth)) * 0.5f;
        if (halfDrop < 0.004f || length < 0.004f)
            return 0f;
        return Mathf.Atan2(halfDrop, length) * Mathf.Rad2Deg;
    }

    // One lite flake across the bole. Strip fibres / C-lenticels /
    // cork dashes / plum seams / hedge stools without it were ticks
    // on a pole (willow analog). Picture only — collider stays.
    private void PaintLiteBolePeel(HumanSegment seg, float width, float length, int depth)
    {
        if (seg == null || seg.visual == null)
            return;
        float tilt = WoodConeTiltDeg(width, length, depth);
        float wMid = PictureWidthAt(width, 0.48f, depth);
        // 0.34 at +0.06 spanned −0.11…+0.23 — a lite belt on the spine
        // (pine Fill mast analog). Near-side flake + dark lip; Peel2
        // is the far flank so willow / poplar / hedge / cane / plum
        // are not a one-sided tick on a pole. Pine/cedar shingles
        // share this helper — no second BarkPeel2 below. Collider stays.
        AddFlatPicture(
            seg.visual,
            "BarkPeel",
            new Vector2(wMid * 0.26f, length * 0.048f),
            palBarkLite,
            seg.sortingOrder,
            new Vector3(wMid * 0.18f, (0.44f - 0.5f) * length, 0f),
            tilt * 0.28f,
            ArtLibrary.Bark);
        AddFlatPicture(
            seg.visual,
            "BarkPeelLip",
            new Vector2(wMid * 0.12f, length * 0.032f),
            palBarkDark,
            seg.sortingOrder,
            new Vector3(wMid * 0.24f, (0.40f - 0.5f) * length, 0f),
            tilt * 0.40f + 14f,
            ArtLibrary.Bark);
        AddFlatPicture(
            seg.visual,
            "BarkPeel2",
            new Vector2(wMid * 0.20f, length * 0.040f),
            palBarkDark,
            seg.sortingOrder,
            // −0.18 × 0.20 right −0.08: left flank opposite Peel,
            // not a spine belt (oak Peel2 analog). Collider stays.
            new Vector3(-wMid * 0.18f, (0.66f - 0.5f) * length, 0f),
            -tilt * 0.48f,
            ArtLibrary.Bark);
        // Near Peel has a dark lip so the flake is bark, not a
        // brick. Far Peel2 without one stayed a tick on the pole.
        // Lite chip off Peel2; collider stays.
        AddFlatPicture(
            seg.visual,
            "BarkPeel2Lip",
            new Vector2(wMid * 0.10f, length * 0.028f),
            palBarkLite,
            seg.sortingOrder,
            new Vector3(-wMid * 0.26f, (0.62f - 0.5f) * length, 0f),
            -tilt * 0.60f - 12f,
            ArtLibrary.Bark);
    }

    // Discrete bark plates: two flat colours, staggered. Not a gradient
    // inside one silhouette, and not a PNG. Stump and trunk get the full
    // kind layout; first fork (depth 1) gets two plates; inner wood
    // (depth ≥ 2) gets one plate on WoodJoinScaleInner so the 05:18
    // cone is not a bare taper. Collider size stays.
    // Furrow = oak cracks on the cone (bush/hazel share chips, not cracks).
    // Scale = pine/cedar chips on the fork; bole is overlapping scales.
    // Strip = lilac internodal rings (willow skip: pith was a barcode).
    // Lenticel = poplar C (tall stem + pith arms as thick as the stem),
    // not 46° diamonds and not 6 mm hyphens.
    // Plate = apple peel chips / plum seam (bole skips the zipper loop).
    private void PaintBarkPlates(HumanSegment seg, float width, float length, int depth)
    {
        if (barkPlateCount < 1 || seg == null || seg.visual == null)
            return;
        if (HeadlessTrial.Active && !HeadlessTrial.VisualCapture)
            return;
        // Depth ≥ 1 sits on WoodBaseScale (parent tip). Apple 96 still
        // skips the extra GO. Inner forks used to return here and read
        // as hash-less sticks after plant closed the mushroom join.
        if (depth >= 1 && maxSegments > 31)
            return;

        bool branchWood = depth >= 1;
        bool oakFurrow = barkLook == BarkLook.Furrow && ResolvedKind == TreeKind.Oak;
        // Pine/cedar bole already paints overlapping shingles. Five Scale
        // plates under those shingles read as a brick wall at 1x.
        bool boleShingles = barkLook == BarkLook.Scale && depth <= 0;
        int n = depth < 0 ? Mathf.Max(2, barkPlateCount - 1) : barkPlateCount;
        if (branchWood)
            n = depth == 1
                ? Mathf.Min(2, Mathf.Max(1, barkPlateCount))
                : 1;
        // Oak furrow is short staggered cracks, not four stacked tiles.
        // Apple/plum bole: Plate+Knob at even t was a russet zipper
        // under Peel (pine-tile analog). Skip that loop on the leader;
        // irregular chips + crossing peels own orchard bark. Twigs
        // still take one fork flake. Collider stays.
        bool orchardBolePlates = barkLook == BarkLook.Plate && !branchWood;
        if (!oakFurrow && !boleShingles && !orchardBolePlates)
        {
            for (int i = 0; i < n; i++)
            {
            if (barkLook == BarkLook.Strip && i >= 2)
                break;

            float t = n == 1 ? 0.5f : i / (float)(n - 1);
            float sign = (i & 1) == 0 ? -1f : 1f;
            float along;
            float plateW;
            float plateH;
            float x;
            float rot;
            if (barkLook == BarkLook.Scale)
            {
                // Bole already has shingles. 0.90 belts on the fork were
                // pine tiles (apple 0.72 analog). Short chips wrap the
                // inner cone. Collider stays.
                along = Mathf.Lerp(0.22f, 0.78f, t);
                float wAt = PictureWidthAt(width, along, depth);
                plateW = wAt * (0.32f + 0.10f * (i % 2));
                plateH = length * (0.072f + 0.018f * (i % 2));
                x = sign * wAt * 0.18f;
                rot = sign * (8f + 4f * (i % 2));
            }
            else if (barkLook == BarkLook.Strip)
            {
                // Lilac internodal rings are painted after this loop.
                // A 0.78 neighbour at 0° was a second pith (barcode).
                // Willow full-height strips sat on ConeEdge and read as
                // a barcode, not bark.
                bool cane = ResolvedKind == TreeKind.Lilac;
                if (cane)
                    continue;
                // 0.32-tall neighbour was a pith barcode (lilac analog).
                // Short fibres along the cone are willow bark at 1x.
                along = i == 0 ? 0.34f : 0.68f;
                float wAt = PictureWidthAt(width, along, depth);
                plateW = wAt * 0.16f;
                plateH = length * (i == 0 ? 0.14f : 0.11f);
                x = sign * wAt * 0.20f;
                rot = sign * (WoodConeTiltDeg(width, length, depth) * 0.55f + 10f);
            }
            else if (barkLook == BarkLook.Lenticel)
            {
                // 46° diamond was an X; the 8° dash alone was a 3 cm
                // hyphen on a silver pole (willow-tick analog). A C
                // tall in Y (visual 11:29) plus that dash is a
                // lenticel. Collider stays.
                along = Mathf.Lerp(0.22f, 0.80f, t);
                float wAt = PictureWidthAt(width, along, depth);
                plateW = Mathf.Max(0.016f, wAt * 0.10f);
                plateH = Mathf.Max(0.028f, length * 0.055f);
                x = sign * wAt * 0.20f;
                rot = sign * 8f;
            }
            else if (barkLook == BarkLook.Plate)
            {
                // Apple/plum 0.72-wide belts were pine 0.86 tiles: a
                // barcode of russet shelves, not orchard bark. Short
                // staggered flakes wrap the cone. fruitSize stays.
                along = Mathf.Lerp(0.16f, 0.84f, t);
                float wAt = PictureWidthAt(width, along, depth);
                plateW = wAt * (0.36f + 0.10f * (i % 2));
                plateH = length * (0.072f + 0.018f * (i % 2));
                x = sign * wAt * 0.22f;
                rot = sign * (8f + 4f * (i % 2));
            }
            else
            {
                // Bush / hazel share Furrow. 0.42-tall plates were a
                // second ladder on the cone (oak barcode analog). Short
                // chips, not fence tiles. After PetioleTip the leftover
                // 0.20×0.05 plate + 0.70 lip was still a brick — the
                // flake shrinks, the distal lip is the scale. Oak
                // skips this loop.
                along = Mathf.Lerp(0.18f, 0.82f, t);
                float wAt = PictureWidthAt(width, along, depth);
                plateW = wAt * (0.15f + 0.04f * (i % 2));
                plateH = length * (0.034f + 0.010f * (i % 2));
                x = sign * wAt * 0.24f;
                rot = sign * (14f + 6f * (i % 2));
            }
            Color c = (i & 1) == 0 ? palBarkDark : palBarkLite;
            // Lenticel: skip the belt plate. The C below is the mark.
            if (barkLook != BarkLook.Lenticel)
            {
                AddFlatPicture(
                    seg.visual,
                    "BarkPlate" + i,
                    new Vector2(plateW, plateH),
                    c,
                    seg.sortingOrder,
                    new Vector3(x, (along - 0.5f) * length, 0f),
                    rot,
                    ArtLibrary.Bark);
            }
            if (barkLook == BarkLook.Strip)
            {
                // CorkTip hangs off the flake. Willow fibres without a
                // lip stayed two ticks on a yellow pole. Distal chip.
                // Collider stays.
                AddFlatPicture(
                    seg.visual,
                    "BarkFibreTip" + i,
                    new Vector2(plateW * 0.55f, plateH * 0.28f),
                    palBarkDark,
                    seg.sortingOrder,
                    new Vector3(
                        x + sign * plateW * 0.10f,
                        (along - 0.5f) * length - plateH * 0.42f,
                        0f),
                    rot + sign * 12f,
                    ArtLibrary.Bark);
            }
            else if (barkLook == BarkLook.Plate)
            {
                AddFlatPicture(
                    seg.visual,
                    "BarkKnob" + i,
                    new Vector2(plateW * 0.38f, plateH * 0.55f),
                    palBarkDark,
                    seg.sortingOrder,
                    new Vector3(x + sign * plateW * 0.12f, (along - 0.5f) * length, 0f),
                    sign * 6f,
                    ArtLibrary.Bark);
                // CorkTip hangs off hazel cork. Orchard knobs without a
                // lip stayed ticks on the twig (branch Flake before
                // FlakeTip). Distal chip; fruitSize / colliders stay.
                AddFlatPicture(
                    seg.visual,
                    "BarkKnobTip" + i,
                    new Vector2(plateW * 0.22f, plateH * 0.28f),
                    palBarkLite,
                    seg.sortingOrder,
                    new Vector3(
                        x + sign * plateW * 0.22f,
                        (along - 0.5f) * length - plateH * 0.22f,
                        0f),
                    sign * 14f,
                    ArtLibrary.Bark);
            }
            else if (barkLook == BarkLook.Scale)
            {
                // Bole shingles already carry ScaleTip. Fork plates
                // without a lip stayed pine tiles on the inner cone
                // (CorkTip analog). Distal chip; colliders stay.
                AddFlatPicture(
                    seg.visual,
                    "BarkScaleTip" + i,
                    new Vector2(plateW * 0.55f, Mathf.Max(plateH * 0.70f, length * 0.042f)),
                    (i & 1) == 0 ? palBarkLite : palBarkDark,
                    seg.sortingOrder,
                    new Vector3(
                        x + sign * plateW * 0.14f,
                        (along - 0.5f) * length - Mathf.Max(plateH * 0.62f, length * 0.024f),
                        0f),
                    rot + sign * 18f,
                    ArtLibrary.Bark);
            }
            else if (barkLook != BarkLook.Lenticel)
            {
                // Bush/hazel 0.20×0.05 plates with a 0.70 lip were
                // still bricks after PetioleTip (Furrow before
                // CorkTip). Visual 19:46 leftover: PlateTip 1.15 of
                // a 3 cm flake was still a hyphen on the bole. Lip
                // is a length fraction, hanging further off.
                // Colliders stay.
                AddFlatPicture(
                    seg.visual,
                    "BarkPlateTip" + i,
                    new Vector2(plateW * 0.70f, Mathf.Max(plateH * 1.4f, length * 0.050f)),
                    palBarkLite,
                    seg.sortingOrder,
                    new Vector3(
                        x + sign * plateW * 0.32f,
                        (along - 0.5f) * length - Mathf.Max(plateH * 1.05f, length * 0.028f),
                        0f),
                    rot + sign * 26f,
                    ArtLibrary.Bark);
            }
            else if (barkLook == BarkLook.Lenticel)
            {
                AddFlatPicture(
                    seg.visual,
                    "BarkLens" + i,
                    new Vector2(plateW, plateH),
                    palBarkLite,
                    seg.sortingOrder,
                    new Vector3(x, (along - 0.5f) * length, 0f),
                    rot,
                    ArtLibrary.Bark);
                // Stem is tall in Y (11:59). Arms at plateH*0.22 of the
                // 2.8 cm floor were 6 mm ticks — a hyphen, not a C.
                // Thickness follows the stem width so the opening
                // reads at 1x. Toward pith. Collider stays.
                float armThick = Mathf.Max(plateW * 1.20f, 0.012f);
                float armLong = Mathf.Max(plateW * 2.40f, plateH * 0.55f);
                AddFlatPicture(
                    seg.visual,
                    "BarkCarm" + i,
                    new Vector2(armLong, armThick),
                    palBarkLite,
                    seg.sortingOrder,
                    new Vector3(
                        x - sign * (plateW * 0.45f + armLong * 0.28f),
                        (along - 0.5f) * length + plateH * 0.38f,
                        0f),
                    rot,
                    ArtLibrary.Bark);
                AddFlatPicture(
                    seg.visual,
                    "BarkCarmB" + i,
                    new Vector2(armLong * 0.82f, armThick * 0.90f),
                    palBarkLite,
                    seg.sortingOrder,
                    new Vector3(
                        x - sign * (plateW * 0.35f + armLong * 0.22f),
                        (along - 0.5f) * length - plateH * 0.36f,
                        0f),
                    rot,
                    ArtLibrary.Bark);
                // Stem and C-arms without a distal chip stayed hyphens
                // on the silver pole after RootButtTip (Furrow before
                // CorkTip). Hang off the opening; collider / fork 28 /
                // taper 0.58 stay.
                AddFlatPicture(
                    seg.visual,
                    "BarkLensTip" + i,
                    new Vector2(plateW * 0.70f, plateH * 0.28f),
                    palBarkDark,
                    seg.sortingOrder,
                    new Vector3(
                        x + sign * plateW * 0.18f,
                        (along - 0.5f) * length + plateH * 0.42f,
                        0f),
                    rot + sign * 12f,
                    ArtLibrary.Bark);
                AddFlatPicture(
                    seg.visual,
                    "BarkCarmTip" + i,
                    new Vector2(armLong * 0.38f, armThick * 0.80f),
                    palBarkDark,
                    seg.sortingOrder,
                    new Vector3(
                        x - sign * (plateW * 0.55f + armLong * 0.42f),
                        (along - 0.5f) * length + plateH * 0.38f,
                        0f),
                    rot + sign * 8f,
                    ArtLibrary.Bark);
                // Upper CarmTip closed the pith dash. Lower CarmB
                // without a lip stayed a hyphen on the silver pole
                // (Lens before LensTip). Hang off the opening;
                // collider / fork 28 / taper 0.58 stay.
                AddFlatPicture(
                    seg.visual,
                    "BarkCarmBTip" + i,
                    new Vector2(armLong * 0.32f, armThick * 0.72f),
                    palBarkDark,
                    seg.sortingOrder,
                    new Vector3(
                        x - sign * (plateW * 0.48f + armLong * 0.36f),
                        (along - 0.5f) * length - plateH * 0.36f,
                        0f),
                    rot - sign * 8f,
                    ArtLibrary.Bark);
            }
        }
        }

        if (barkLook == BarkLook.Scale && depth <= 0)
            PaintBarkShingles(seg, width, length, depth);
        else if (barkLook == BarkLook.Strip && ResolvedKind == TreeKind.Lilac)
        {
            // 0.62-wide ring at 0° was still a pith barcode (willow
            // analog). An incomplete scar on one flank is a cane node,
            // not a belt around the stick. Collider stays.
            int nNode = maxSegments > 31 ? 2 : 3;
            for (int node = 0; node < nNode; node++)
            {
                float u = nNode == 1 ? 0.5f : node / (float)(nNode - 1);
                float along = Mathf.Lerp(0.22f, 0.78f, u);
                float wAt = PictureWidthAt(width, along, depth);
                float sign = (node & 1) == 0 ? -1f : 1f;
                AddFlatPicture(
                    seg.visual,
                    "BarkNode" + node,
                    new Vector2(wAt * 0.36f, length * 0.034f),
                    palBarkLite,
                    seg.sortingOrder,
                    new Vector3(sign * wAt * 0.22f, (along - 0.5f) * length, 0f),
                    sign * (10f + WoodConeTiltDeg(width, length, depth) * 0.20f),
                    ArtLibrary.Bark);
                AddFlatPicture(
                    seg.visual,
                    "BarkNodeTip" + node,
                    new Vector2(wAt * 0.18f, length * 0.022f),
                    palBarkDark,
                    seg.sortingOrder,
                    new Vector3(
                        sign * wAt * 0.30f,
                        (along - 0.5f) * length - length * 0.022f,
                        0f),
                    sign * 16f,
                    ArtLibrary.Bark);
            }
        }

        // Bole-only marks. Branch plates already carry the kind on the
        // inner cone; oak cracks / stool / cork / plum seam stay on the
        // leader so a twig does not inherit a fence-post strip.
        // First fork without a flake still read as ticks on a stick
        // (lilac scars / Scale chips / C-lenticels — willow bole analog).
        // One lite peel on depth 1. Apple 96 already returned above.
        if (branchWood)
        {
            if (oakFurrow)
                PaintOakFurrow(seg, width, length, depth, false);
            else if (depth == 1)
                PaintLiteBolePeel(seg, width, length, depth);
            return;
        }

        // Oak furrow on stump and trunk. Bush / hazel share BarkLook.Furrow
        // for plates but must not inherit the oak bole cracks — at 1x that
        // made every deciduous shrub a copy of the hold oak.
        if (oakFurrow && depth <= 0)
            PaintOakFurrow(seg, width, length, depth, true);
        else if (ResolvedKind == TreeKind.Bush && depth == 0)
        {
            // 1.22 × 0.16 at 0° was a table under the hedge
            // (cedar-slab analog). Two chips at the butts.
            float stoolAlong = 0.12f;
            float stoolW = PictureWidthAt(width, stoolAlong, depth);
            AddFlatPicture(
                seg.visual,
                "BarkStoolL",
                new Vector2(stoolW * 0.52f, length * 0.12f),
                palBarkDark,
                seg.sortingOrder,
                new Vector3(-stoolW * 0.28f, (stoolAlong - 0.5f) * length, 0f),
                -8f,
                ArtLibrary.Bark);
            AddFlatPicture(
                seg.visual,
                "BarkStoolR",
                new Vector2(stoolW * 0.46f, length * 0.10f),
                palBarkLite,
                seg.sortingOrder,
                new Vector3(stoolW * 0.26f, (stoolAlong - 0.5f) * length, 0f),
                7f,
                ArtLibrary.Bark);
            // Two stools without a distal chip stayed ticks on the
            // hedge butts (Collar before CollarTip). Hang off the
            // flare; collider stays.
            AddFlatPicture(
                seg.visual,
                "BarkStoolLTip",
                new Vector2(stoolW * 0.28f, length * 0.055f),
                palBarkLite,
                seg.sortingOrder,
                new Vector3(-stoolW * 0.38f, (stoolAlong - 0.5f) * length - length * 0.050f, 0f),
                -14f,
                ArtLibrary.Bark);
            AddFlatPicture(
                seg.visual,
                "BarkStoolRTip",
                new Vector2(stoolW * 0.24f, length * 0.048f),
                palBarkDark,
                seg.sortingOrder,
                new Vector3(stoolW * 0.36f, (stoolAlong - 0.5f) * length - length * 0.044f, 0f),
                12f,
                ArtLibrary.Bark);
            // Two stools without a flake were ticks on a hedge pole
            // (willow analog). One lite peel across the cone. Collider stays.
            PaintLiteBolePeel(seg, width, length, depth);
        }
        else if (ResolvedKind == TreeKind.Hazel && depth <= 0)
        {
            // Short staggered chips, not three 0° dashes (pine barcode analog).
            int nCork = 4;
            for (int cork = 0; cork < nCork; cork++)
            {
                float u = nCork == 1 ? 0.5f : cork / (float)(nCork - 1);
                float sign = (cork & 1) == 0 ? -1f : 1f;
                float along = Mathf.Lerp(0.18f, 0.78f, u);
                float wAt = PictureWidthAt(width, along, depth);
                AddFlatPicture(
                    seg.visual,
                    "BarkCork" + cork,
                    new Vector2(wAt * 0.22f, length * 0.045f),
                    palBarkLite,
                    seg.sortingOrder,
                    new Vector3(sign * wAt * 0.18f, (along - 0.5f) * length, 0f),
                    sign * 12f,
                    ArtLibrary.Bark);
                // Pine ScaleTip hangs off the flake. Cork without a
                // lip stayed four ticks on the coppice (brick analog).
                // Distal chip; collider stays.
                AddFlatPicture(
                    seg.visual,
                    "BarkCorkTip" + cork,
                    new Vector2(wAt * 0.12f, length * 0.028f),
                    palBarkDark,
                    seg.sortingOrder,
                    new Vector3(
                        sign * wAt * 0.26f,
                        (along - 0.5f) * length - length * 0.028f,
                        0f),
                    sign * 18f,
                    ArtLibrary.Bark);
            }
            // Four cork dashes without a flake were ticks on a coppice
            // pole (willow analog). One lite peel across the cone.
            PaintLiteBolePeel(seg, width, length, depth);
        }
        else if (ResolvedKind == TreeKind.Willow && depth <= 0)
        {
            // Two Strip fibres were ticks on a yellow pole (plum two-seam
            // analog). One lite peel across the cone is willow bark, not
            // a third rung. Collider stays.
            PaintLiteBolePeel(seg, width, length, depth);
        }
        else if (ResolvedKind == TreeKind.Poplar && depth <= 0)
        {
            // C-lenticels without a flake still read as ticks on a
            // silver pole (willow two-Strip analog). One lite peel
            // across the cone is birch, not a fourth C. Collider stays.
            PaintLiteBolePeel(seg, width, length, depth);
        }
        else if (ResolvedKind == TreeKind.Lilac && depth <= 0)
        {
            // Three flank scars without a flake were ticks on a green
            // cane (willow Strip analog). One lite peel across the cone.
            // Collider stays.
            PaintLiteBolePeel(seg, width, length, depth);
        }
        else if (barkLook == BarkLook.Plate && depth == 0)
        {
            float tilt = WoodConeTiltDeg(width, length, depth);
            float wMid = PictureWidthAt(width, 0.48f, depth);
            if (ResolvedKind == TreeKind.Apple)
            {
                // Plate+Knob zipper skipped above. Two uneven chips
                // plus crossing peels are orchard, not a russet ladder.
                // fruitSize / colliders stay.
                float wChip0 = PictureWidthAt(width, 0.20f, depth);
                AddFlatPicture(
                    seg.visual,
                    "BarkChip0",
                    new Vector2(wChip0 * 0.22f, length * 0.055f),
                    palBarkDark,
                    seg.sortingOrder,
                    new Vector3(-wChip0 * 0.18f, (0.20f - 0.5f) * length, 0f),
                    -tilt * 0.22f,
                    ArtLibrary.Bark);
                AddFlatPicture(
                    seg.visual,
                    "BarkChip0Tip",
                    new Vector2(wChip0 * 0.12f, length * 0.032f),
                    palBarkLite,
                    seg.sortingOrder,
                    new Vector3(-wChip0 * 0.26f, (0.20f - 0.5f) * length - length * 0.028f, 0f),
                    -tilt * 0.30f - 10f,
                    ArtLibrary.Bark);
                float wChip1 = PictureWidthAt(width, 0.78f, depth);
                AddFlatPicture(
                    seg.visual,
                    "BarkChip1",
                    new Vector2(wChip1 * 0.18f, length * 0.048f),
                    palBarkLite,
                    seg.sortingOrder,
                    new Vector3(wChip1 * 0.16f, (0.78f - 0.5f) * length, 0f),
                    tilt * 0.16f,
                    ArtLibrary.Bark);
                AddFlatPicture(
                    seg.visual,
                    "BarkChip1Tip",
                    new Vector2(wChip1 * 0.10f, length * 0.028f),
                    palBarkDark,
                    seg.sortingOrder,
                    new Vector3(wChip1 * 0.24f, (0.78f - 0.5f) * length - length * 0.024f, 0f),
                    tilt * 0.22f + 12f,
                    ArtLibrary.Bark);
                AddFlatPicture(
                    seg.visual,
                    "BarkPeel",
                    new Vector2(wMid * 0.24f, length * 0.048f),
                    palBarkLite,
                    seg.sortingOrder,
                    // 0.34 at +0.06 spanned −0.11…+0.23 — a russet belt
                    // on the orchard spine (oak bole Peel analog). Near
                    // flank + lip; Peel2 keeps the far chip. fruitSize stays.
                    new Vector3(wMid * 0.18f, (0.42f - 0.5f) * length, 0f),
                    tilt * 0.28f,
                    ArtLibrary.Bark);
                AddFlatPicture(
                    seg.visual,
                    "BarkPeel2",
                    new Vector2(wMid * 0.22f, length * 0.042f),
                    palBarkDark,
                    seg.sortingOrder,
                    // −0.08 × 0.24 spanned −0.20…+0.04 (oak Peel2 analog).
                    // Left flank opposite Peel. fruitSize / colliders stay.
                    new Vector3(-wMid * 0.16f, (0.66f - 0.5f) * length, 0f),
                    -tilt * 0.48f,
                    ArtLibrary.Bark);
                // Oak PeelLip closed the brick of chips. Apple Peel +
                // Peel2 without a lip stayed the same russet brick.
                // One dark tick off Peel is orchard, not a fourth
                // Plate rung. fruitSize / colliders stay.
                AddFlatPicture(
                    seg.visual,
                    "BarkPeelLip",
                    new Vector2(wMid * 0.12f, length * 0.032f),
                    palBarkDark,
                    seg.sortingOrder,
                    new Vector3(wMid * 0.24f, (0.38f - 0.5f) * length, 0f),
                    tilt * 0.36f + 14f,
                    ArtLibrary.Bark);
                AddFlatPicture(
                    seg.visual,
                    "BarkPeel2Lip",
                    new Vector2(wMid * 0.10f, length * 0.026f),
                    palBarkDark,
                    seg.sortingOrder,
                    new Vector3(-wMid * 0.24f, (0.62f - 0.5f) * length, 0f),
                    -tilt * 0.55f - 12f,
                    ArtLibrary.Bark);
            }
            else if (ResolvedKind == TreeKind.Plum)
            {
                // A 0.72 keel duplicated ConeEdge (oak BarkRidge analog).
                // Two short chips are a plum seam, not a barcode. Plate
                // zipper skipped on the bole — one extra flake so the
                // purple stick is not only two ticks. Collider stays.
                for (int seam = 0; seam < 2; seam++)
                {
                    float along = seam == 0 ? 0.32f : 0.62f;
                    float wAt = PictureWidthAt(width, along, depth);
                    AddFlatPicture(
                        seg.visual,
                        "BarkSeam" + seam,
                        new Vector2(wAt * 0.10f, length * 0.16f),
                        palBarkDark,
                        seg.sortingOrder,
                        // Seam1 at −0.04 sat ON the purple spine after
                        // oak BarkBreak left the flank (Furrow3 analog).
                        // Both seams ride opposite flanks. Collider stays.
                        new Vector3(
                            (seam == 0 ? -1f : 1f) * wAt * 0.22f,
                            (along - 0.5f) * length,
                            0f),
                        seam == 0 ? -6f : 5f,
                        ArtLibrary.Bark);
                    AddFlatPicture(
                        seg.visual,
                        "BarkSeamTip" + seam,
                        new Vector2(wAt * 0.08f, length * 0.06f),
                        palBarkLite,
                        seg.sortingOrder,
                        new Vector3(
                            (seam == 0 ? -1f : 1f) * wAt * 0.28f,
                            (along - 0.5f) * length - length * 0.08f,
                            0f),
                        seam == 0 ? -14f : 12f,
                        ArtLibrary.Bark);
                }
                float wChip = PictureWidthAt(width, 0.48f, depth);
                AddFlatPicture(
                    seg.visual,
                    "BarkChip",
                    new Vector2(wChip * 0.20f, length * 0.050f),
                    palBarkLite,
                    seg.sortingOrder,
                    new Vector3(wChip * 0.14f, (0.48f - 0.5f) * length, 0f),
                    tilt * 0.20f,
                    ArtLibrary.Bark);
                AddFlatPicture(
                    seg.visual,
                    "BarkChipTip",
                    new Vector2(wChip * 0.12f, length * 0.028f),
                    palBarkDark,
                    seg.sortingOrder,
                    new Vector3(wChip * 0.22f, (0.48f - 0.5f) * length - length * 0.028f, 0f),
                    tilt * 0.28f + 10f,
                    ArtLibrary.Bark);
                // Two seams + chip without a flake were ticks on a
                // purple pole (willow analog). One lite peel across
                // the cone. fruitSize / colliders stay.
                PaintLiteBolePeel(seg, width, length, depth);
            }
        }
    }

    // Oak bark at 1x: irregular dark chips and lite flakes on the cone.
    // Crack0/1/2 at 0.24/0.48/0.72 were a three-rung ladder after the
    // furrow pass. Bole is Furrow0–3 + peel only. Branch: one crack
    // plus a lite flake. Two discrete colours, no gradient.
    private void PaintOakFurrow(HumanSegment seg, float width, float length, int depth, bool bole)
    {
        if (seg == null || seg.visual == null)
            return;
        float tilt = WoodConeTiltDeg(width, length, depth);
        float wMid = PictureWidthAt(width, 0.50f, depth);
        if (!bole)
        {
            AddFlatPicture(
                seg.visual,
                "BarkCrack",
                new Vector2(wMid * 0.11f, length * 0.18f),
                palBarkDark,
                seg.sortingOrder,
                new Vector3(-wMid * 0.14f, length * 0.02f, 0f),
                -tilt * 0.35f,
                ArtLibrary.Bark);
            // One crack on a smooth cone still read as a tick, not
            // oak. A lite flake sits inside the taper. Collider stays.
            AddFlatPicture(
                seg.visual,
                "BarkFlake",
                new Vector2(wMid * 0.22f, length * 0.070f),
                palBarkLite,
                seg.sortingOrder,
                new Vector3(wMid * 0.10f, -length * 0.08f, 0f),
                tilt * 0.18f,
                ArtLibrary.Bark);
            // One flake on a long outward limb was still a tick on a
            // stick. A second lite chip at a different Y is bark, not
            // a ladder (Crack0/1/2 analog). Collider stays.
            AddFlatPicture(
                seg.visual,
                "BarkFlake2",
                new Vector2(wMid * 0.10f, length * 0.028f),
                palBarkLite,
                seg.sortingOrder,
                // Visual 19:46 leftover: Flake2 0.12×0.036 still a
                // brick after Flake2Tip. Shorter chip, left flank.
                // Collider stays.
                new Vector3(-wMid * 0.22f, length * 0.12f, 0f),
                -tilt * 0.22f,
                ArtLibrary.Bark);
            // Bole FurrowTip closed the trunk ticks. Branch Crack/
            // Flake without a lip stayed ticks on a stick (same analog).
            // Distal chip; collider / fork 28 / taper 0.58 stay.
            AddFlatPicture(
                seg.visual,
                "BarkCrackTip",
                new Vector2(wMid * 0.07f, length * 0.070f),
                palBarkLite,
                seg.sortingOrder,
                new Vector3(-wMid * 0.20f, length * 0.02f - length * 0.080f, 0f),
                -tilt * 0.48f - 8f,
                ArtLibrary.Bark);
            AddFlatPicture(
                seg.visual,
                "BarkFlakeTip",
                new Vector2(wMid * 0.12f, length * 0.032f),
                palBarkDark,
                seg.sortingOrder,
                new Vector3(wMid * 0.16f, -length * 0.08f - length * 0.036f, 0f),
                tilt * 0.28f + 12f,
                ArtLibrary.Bark);
            AddFlatPicture(
                seg.visual,
                "BarkFlake2Tip",
                new Vector2(wMid * 0.07f, length * 0.032f),
                palBarkDark,
                seg.sortingOrder,
                new Vector3(-wMid * 0.30f, length * 0.12f - length * 0.034f, 0f),
                -tilt * 0.46f - 14f,
                ArtLibrary.Bark);
            return;
        }

        // Crack0/1/2 sat at 0.24 / 0.48 / 0.72 — a three-rung ladder
        // even after the irregular furrows (stripe analog). Drop the
        // even cracks; flakes + peel + Furrow0–3 are the bole. Collider stays.
        // Lite flakes: chips of bark across the bole, not a second keel.
        AddFlatPicture(
            seg.visual,
            "BarkFlake0",
            new Vector2(wMid * 0.22f, length * 0.070f),
            palBarkLite,
            seg.sortingOrder,
            // 0.28 at −0.10 still reached +0.04 on the bole spine
            // after BarkSeam1 left the flank (Flake analog of
            // Furrow3). Chip rides Furrow0. Collider stays.
            new Vector3(-wMid * 0.22f, (0.34f - 0.5f) * length, 0f),
            tilt * 0.12f,
            ArtLibrary.Bark);
        AddFlatPicture(
            seg.visual,
            "BarkFlake1",
            new Vector2(wMid * 0.22f, length * 0.058f),
            palBarkLite,
            seg.sortingOrder,
            new Vector3(wMid * 0.12f, (0.62f - 0.5f) * length, 0f),
            -tilt * 0.10f,
            ArtLibrary.Bark);
        float wBreak = PictureWidthAt(width, 0.28f, depth);
        AddFlatPicture(
            seg.visual,
            "BarkBreak",
            new Vector2(wBreak * 0.32f, length * 0.042f),
            palBarkLite,
            seg.sortingOrder,
            // 0.04 at mid-Y sat ON the bole spine after Flake0 left
            // the flank (Scallop1 analog). Chip rides Furrow0.
            // Collider stays.
            new Vector3(-wMid * 0.18f, (0.28f - 0.5f) * length, 0f),
            tilt * 0.18f,
            ArtLibrary.Bark);
        // Branch FlakeTip closed the twig ticks. Bole Flake0/1 and
        // Break without a lip stayed bricks on the trunk (Furrow
        // before FurrowTip). Distal chip off the flank; collider /
        // fork 28 / taper 0.58 stay.
        AddFlatPicture(
            seg.visual,
            "BarkFlake0Tip",
            new Vector2(wMid * 0.12f, length * 0.032f),
            palBarkDark,
            seg.sortingOrder,
            new Vector3(-wMid * 0.30f, (0.34f - 0.5f) * length - length * 0.036f, 0f),
            tilt * 0.22f - 10f,
            ArtLibrary.Bark);
        AddFlatPicture(
            seg.visual,
            "BarkFlake1Tip",
            new Vector2(wMid * 0.12f, length * 0.028f),
            palBarkDark,
            seg.sortingOrder,
            new Vector3(wMid * 0.20f, (0.62f - 0.5f) * length - length * 0.032f, 0f),
            -tilt * 0.20f + 12f,
            ArtLibrary.Bark);
        AddFlatPicture(
            seg.visual,
            "BarkBreakTip",
            new Vector2(wMid * 0.16f, length * 0.022f),
            palBarkDark,
            seg.sortingOrder,
            new Vector3(-wMid * 0.26f, (0.28f - 0.5f) * length - length * 0.024f, 0f),
            tilt * 0.28f - 8f,
            ArtLibrary.Bark);
        // Four chips at 0.18 cadence plus Crack0/1/2 still marched as
        // a dotted ladder (stripe analog). Irregular Y and a lite peel
        // across two chips: furrowed oak, not a barcode. Collider stays.
        AddFlatPicture(
            seg.visual,
            "BarkFurrow0",
            new Vector2(wMid * 0.10f, length * 0.09f),
            palBarkDark,
            seg.sortingOrder,
            new Vector3(-wMid * 0.22f, (0.17f - 0.5f) * length, 0f),
            -tilt * 0.62f,
            ArtLibrary.Bark);
        AddFlatPicture(
            seg.visual,
            "BarkFurrow1",
            new Vector2(wMid * 0.08f, length * 0.11f),
            palBarkDark,
            seg.sortingOrder,
            new Vector3(wMid * 0.24f, (0.35f - 0.5f) * length, 0f),
            tilt * 0.48f,
            ArtLibrary.Bark);
        AddFlatPicture(
            seg.visual,
            "BarkFurrow2",
            new Vector2(wMid * 0.07f, length * 0.08f),
            palBarkDark,
            seg.sortingOrder,
            new Vector3(-wMid * 0.16f, (0.61f - 0.5f) * length, 0f),
            -tilt * 0.28f,
            ArtLibrary.Bark);
        AddFlatPicture(
            seg.visual,
            "BarkFurrow3",
            new Vector2(wMid * 0.09f, length * 0.10f),
            palBarkDark,
            seg.sortingOrder,
            // 0.06 sat ON the tip spine (BarkBreak analog). Crack
            // rides the right flank like Furrow1. Collider stays.
            new Vector3(wMid * 0.22f, (0.83f - 0.5f) * length, 0f),
            tilt * 0.18f,
            ArtLibrary.Bark);
        AddFlatPicture(
            seg.visual,
            "BarkFurrow0Tip",
            new Vector2(wMid * 0.06f, length * 0.040f),
            palBarkLite,
            seg.sortingOrder,
            new Vector3(-wMid * 0.28f, (0.17f - 0.5f) * length - length * 0.040f, 0f),
            -tilt * 0.74f - 10f,
            ArtLibrary.Bark);
        AddFlatPicture(
            seg.visual,
            "BarkFurrow1Tip",
            new Vector2(wMid * 0.05f, length * 0.036f),
            palBarkLite,
            seg.sortingOrder,
            new Vector3(wMid * 0.30f, (0.35f - 0.5f) * length - length * 0.038f, 0f),
            tilt * 0.58f + 12f,
            ArtLibrary.Bark);
        AddFlatPicture(
            seg.visual,
            "BarkFurrow2Tip",
            new Vector2(wMid * 0.05f, length * 0.032f),
            palBarkLite,
            seg.sortingOrder,
            new Vector3(-wMid * 0.22f, (0.61f - 0.5f) * length - length * 0.036f, 0f),
            -tilt * 0.40f - 8f,
            ArtLibrary.Bark);
        AddFlatPicture(
            seg.visual,
            "BarkFurrow3Tip",
            new Vector2(wMid * 0.06f, length * 0.038f),
            palBarkLite,
            seg.sortingOrder,
            // CorkTip hangs off hazel cork. Oak cracks without a lip
            // stayed four ticks on the bole (brick analog). Distal
            // chip; collider stays.
            new Vector3(wMid * 0.28f, (0.83f - 0.5f) * length - length * 0.042f, 0f),
            tilt * 0.28f + 10f,
            ArtLibrary.Bark);
        AddFlatPicture(
            seg.visual,
            "BarkPeel",
            new Vector2(wMid * 0.24f, length * 0.048f),
            palBarkLite,
            seg.sortingOrder,
            // 0.38 at +0.08 spanned −0.11…+0.27 — a lite belt on the
            // oak spine (apple Peel analog). Near-side flake; Peel2
            // and Furrow keep the far chips. Collider stays.
            new Vector3(wMid * 0.20f, (0.44f - 0.5f) * length, 0f),
            tilt * 0.22f,
            ArtLibrary.Bark);
        // Crossing flake so the remaining chips are not a second ladder.
        AddFlatPicture(
            seg.visual,
            "BarkPeel2",
            new Vector2(wMid * 0.22f, length * 0.048f),
            palBarkLite,
            seg.sortingOrder,
            // −0.06 × 0.26 spanned −0.19…+0.07 — a flake on the
            // bole spine (pine Fill analog). Left flank opposite
            // Peel. Collider stays.
            new Vector3(-wMid * 0.16f, (0.71f - 0.5f) * length, 0f),
            -tilt * 0.55f,
            ArtLibrary.Bark);
        // Pine ScaleTip hangs off the flake. Oak peel without a lip
        // stayed a brick of chips. One dark tick off Peel is furrow,
        // not a fourth Crack rung. Collider stays.
        AddFlatPicture(
            seg.visual,
            "BarkPeelLip",
            new Vector2(wMid * 0.14f, length * 0.036f),
            palBarkDark,
            seg.sortingOrder,
            new Vector3(wMid * 0.26f, (0.40f - 0.5f) * length, 0f),
            tilt * 0.40f + 16f,
            ArtLibrary.Bark);
        // Far Peel2 was a dark tick after the near lip closed the
        // brick (helper Peel2 analog). Lite chip off Peel2.
        // Collider stays.
        AddFlatPicture(
            seg.visual,
            "BarkPeel2Lip",
            new Vector2(wMid * 0.10f, length * 0.028f),
            palBarkDark,
            seg.sortingOrder,
            new Vector3(-wMid * 0.24f, (0.67f - 0.5f) * length, 0f),
            -tilt * 0.62f - 12f,
            ArtLibrary.Bark);
    }

    // Overlapping pine/cedar shingles. Picture only. Cedar 0.90 wrap
    // was a table (pine 0.86 analog). Short staggered chips wrap the bole.
    private void PaintBarkShingles(HumanSegment seg, float width, float length, int depth)
    {
        if (seg == null || seg.visual == null)
            return;
        bool cedar = ResolvedKind == TreeKind.Cedar;
        int n = cedar ? 3 : 4;
        for (int i = 0; i < n; i++)
        {
            // Even (i+0.5)/n stacked as a brick ladder. Irregular Y.
            float along = cedar
                ? (i == 0 ? 0.20f : (i == 1 ? 0.52f : 0.84f))
                : (i == 0 ? 0.14f : (i == 1 ? 0.36f : (i == 2 ? 0.62f : 0.88f)));
            float sign = (i & 1) == 0 ? 1f : -1f;
            float wAt = PictureWidthAt(width, along, depth);
            float shH;
            float shW;
            float x;
            float rot;
            if (cedar)
            {
                // 0.34 chip without a lip was still a brick on the bole
                // (pine-tile analog). Visual 19:46 leftover: ScaleTip
                // 0.72 of a 3 cm flake was still a hyphen. Shorter
                // chip, lip as a length fraction. Collider stays.
                shH = length * (0.028f + 0.010f * (i % 2));
                shW = wAt * (0.18f + 0.05f * (i % 2));
                x = sign * wAt * 0.28f;
                rot = -sign * (12f + 6f * (i % 2));
            }
            else
            {
                // 0.36 belts with even t were still pine tiles.
                // Visual 19:46 leftover: 0.040 of bole length still a
                // brick shelf. Shorter overlapping scales. Collider stays.
                float tilt = WoodConeTiltDeg(width, length, depth);
                shH = length * (0.032f + 0.012f * (i % 2));
                shW = wAt * (0.18f + 0.04f * (i % 2));
                x = sign * wAt * 0.22f;
                rot = -sign * (tilt * 0.70f + 8f + 6f * (i % 2));
            }
            AddFlatPicture(
                seg.visual,
                "BarkShingle" + i,
                new Vector2(shW, shH),
                (i & 1) == 0 ? palBarkLite : palBarkDark,
                seg.sortingOrder,
                new Vector3(x, (along - 0.5f) * length, 0f),
                rot,
                ArtLibrary.Bark);
            // Lip on pine and cedar: a pointed tip hanging off the
            // flake. Cedar without it stayed a brick. Collider stays.
            AddFlatPicture(
                seg.visual,
                "BarkScaleTip" + i,
                new Vector2(shW * 0.58f, Mathf.Max(shH * 0.95f, length * 0.045f)),
                (i & 1) == 0 ? palBarkDark : palBarkLite,
                seg.sortingOrder,
                new Vector3(
                    x + sign * shW * 0.22f,
                    (along - 0.5f) * length - Mathf.Max(shH * 0.88f, length * 0.026f),
                    0f),
                rot + sign * 22f,
                ArtLibrary.Bark);
        }
        // Shingles + ScaleTip without a crossing flake still marched
        // as a brick ladder (oak Crack0/1/2 analog). Lite peel + far
        // Peel2 live in the helper so pine/cedar match willow bark.
        // Collider stays.
        PaintLiteBolePeel(seg, width, length, depth);
    }

    // Crown mass so the tree reads at 1x. Picture only; no collider.
    // Winter deciduous keeps the kind envelope in twig/bark (not Leaf).
    private void AttachCrowns()
    {
        if (crownMassCount < 1)
            return;
        if (!growLeaves && !winterWoodyCrown)
            return;
        if (HeadlessTrial.Active && !HeadlessTrial.VisualCapture)
            return;

        if (crownStackOnLeader)
        {
            HumanSegment spine = null;
            int bestDepth = -1;
            float bestAbs = 999f;
            for (int i = 0; i < nodes.Count; i++)
            {
                Node hit = nodes[i];
                if (hit.segment == null || hit.depth < 0)
                    continue;
                float ah = Mathf.Abs(hit.heading);
                if (hit.depth > bestDepth || (hit.depth == bestDepth && ah < bestAbs))
                {
                    spine = hit.segment;
                    bestDepth = hit.depth;
                    bestAbs = ah;
                }
            }
            if (spine == null)
                return;
            float len = spine.size.y;
            int massCount = crownMassCount;
            for (int i = 0; i < massCount; i++)
            {
                float u = massCount == 1 ? 0f : i / (float)(massCount - 1);
                float along = Mathf.Lerp(0.22f, 1.04f, u);
                // Wide base → sharp tip: a cone at 1x, not three equal slabs.
                float w = crownSize.x * Mathf.Lerp(1.12f, 0.22f, u);
                Color c = winterWoodyCrown
                    ? WoodCrownTint(i)
                    : LeafTint(i, (i & 1) == 1);
                // 0.04 of the 84/94 cm envelope was a stamp on the
                // spine. 0.08 / 0.14 still left three hats on the pole
                // (visual FanM2 analog). Hang the layer into the fork.
                // Envelope / colliders stay.
                float hang = ((i & 1) == 0 ? -1f : 1f)
                    * w * (ResolvedKind == TreeKind.Cedar ? 0.22f : 0.18f);
                // Pine 0° hats stacked as shelves on the pole (cedar
                // pancake analog). Alternate tilt; cedar tables stay
                // flat. Envelope / colliders stay.
                float layerTilt = ResolvedKind == TreeKind.Pine
                    ? ((i & 1) == 0 ? -7f : 6f)
                    : 0f;
                Transform mass = AddFlatPicture(
                    spine.transform,
                    spine.name + "_Crown" + i,
                    new Vector2(w, crownSize.y),
                    c,
                    spine.sortingOrder + 1,
                    new Vector3(hang, (along - 0.5f) * len, 0f),
                    layerTilt,
                    CrownBake);
                // Pine 84 cm brick and cedar 94 cm table are grow
                // envelopes. Hide them so extras own 1x. A tip box on
                // a cedar shelf is a fir — pine apex only.
                if (ResolvedKind == TreeKind.Pine || ResolvedKind == TreeKind.Cedar)
                    HidePictureBody(mass);
                AttachCrownLobes(mass, c, spine.sortingOrder + 1, w);
                if (i == massCount - 1 && ResolvedKind == TreeKind.Pine)
                {
                    // 0.42×0.55 at 0° was a green post on the tip
                    // (willow-pole analog). 0.04×0.38 was still that
                    // post (PaintPineCrownCone Cap analog). Peak rides
                    // SlopeL of this hat. Envelope stays.
                    AddFlatPicture(
                        mass,
                        mass.name + "_Apex",
                        new Vector2(w * 0.24f, crownSize.y * 0.36f),
                        c,
                        spine.sortingOrder + 1,
                        // −0.28 × 0.28 sat wholly left of 0, so the
                        // tip hat was one tent after Cap/CapR kissed
                        // inside the cone (FanDome analog). Width 0.24
                        // kisses x=0; ApexR is the far peak. Envelope stays.
                        new Vector3(-w * 0.12f, crownSize.y * 0.28f, 0f),
                        -8f,
                        ArtLibrary.Leaf);
                    AddFlatPicture(
                        mass,
                        mass.name + "_ApexTip",
                        new Vector2(w * 0.14f, crownSize.y * 0.14f),
                        c,
                        spine.sortingOrder + 1,
                        // After Cap/CapR body kiss: ApexTip at −0.16
                        // right −0.09 / ApexRTip left +0.10 left a
                        // 0.19 peak slit above ApexR top (fir CapTip
                        // analog). Kiss at x=0; envelope stays.
                        new Vector3(-w * 0.07f, crownSize.y * 0.42f, 0f),
                        -12f,
                        ArtLibrary.Leaf);
                    AddFlatPicture(
                        mass,
                        mass.name + "_ApexR",
                        new Vector2(w * 0.22f, crownSize.y * 0.36f),
                        CrownPatch(c, true),
                        spine.sortingOrder + 1,
                        // Left edge 0 against Apex right 0. Match Apex
                        // top so CapTip peak slit does not reopen above
                        // a short far pad. Envelope stays.
                        new Vector3(w * 0.11f, crownSize.y * 0.28f, 0f),
                        6f,
                        ArtLibrary.Leaf);
                    AddFlatPicture(
                        mass,
                        mass.name + "_ApexRTip",
                        new Vector2(w * 0.12f, crownSize.y * 0.12f),
                        CrownPatch(c, true),
                        spine.sortingOrder + 1,
                        // Left edge 0 against ApexTip right 0. Envelope stays.
                        new Vector3(w * 0.06f, crownSize.y * 0.40f, 0f),
                        10f,
                        ArtLibrary.Leaf);
                    // CapFill rides Cap under CapTip. Tip-hat Apex /
                    // ApexR kissed at mid-Y, but no shoulder chip under
                    // ApexTip left the fir tip a one-tent after CapFill
                    // closed the cone peak. Envelope / fork 28 stay.
                    AddFlatPicture(
                        mass,
                        mass.name + "_ApexFill",
                        new Vector2(w * 0.12f, crownSize.y * 0.16f),
                        c,
                        spine.sortingOrder + 1,
                        // After FanFill under FanBodyTip: tip Apex had
                        // no CapFill-language chip (CapFill −0.12 slit
                        // analog on the fir tip hat). Chip rides Apex
                        // under ApexTip; right edge toward ApexTip left.
                        // Envelope stays.
                        new Vector3(-w * 0.14f, crownSize.y * 0.32f, 0f),
                        -6f,
                        ArtLibrary.Leaf);
                    AddFlatPicture(
                        mass,
                        mass.name + "_ApexFillTip",
                        new Vector2(w * 0.07f, crownSize.y * 0.07f),
                        c,
                        spine.sortingOrder + 1,
                        // ApexTip closed the fir tip hat. ApexFill
                        // without a lip stayed a peak mast chip
                        // (CapFill before CapFillTip). Hang off Apex
                        // toward ApexTip; right edge kisses ApexTip
                        // left. Envelope stays.
                        new Vector3(-w * 0.16f, crownSize.y * 0.38f, 0f),
                        -12f,
                        ArtLibrary.Leaf);
                    AddFlatPicture(
                        mass,
                        mass.name + "_ApexFillR",
                        new Vector2(w * 0.12f, crownSize.y * 0.14f),
                        CrownPatch(c, true),
                        spine.sortingOrder + 1,
                        // On ApexR under ApexRTip (not the mid peak).
                        // Envelope stays.
                        new Vector3(w * 0.14f, crownSize.y * 0.30f, 0f),
                        5f,
                        ArtLibrary.Leaf);
                    AddFlatPicture(
                        mass,
                        mass.name + "_ApexFillRTip",
                        new Vector2(w * 0.07f, crownSize.y * 0.06f),
                        CrownPatch(c, true),
                        spine.sortingOrder + 1,
                        // Left edge kisses ApexRTip right. Envelope stays.
                        new Vector3(w * 0.15f, crownSize.y * 0.36f, 0f),
                        11f,
                        ArtLibrary.Leaf);
                }
                // Wide base hats catch more gust than the tip cap.
                // One amp on all three was a metronome on the pole.
                // Artist hang 0.18…0.22 sits the plate off the spine;
                // grow heading is still ~0° so HeadingSailMul(wood)
                // left those hats as pole sail. Lean from hang + tilt.
                float layerSail = 1f + 0.10f * (massCount - 1 - i);
                float alongY = (along - 0.5f) * len;
                float hatSail = PicturePlateSail(layerTilt, hang, alongY, 0f);
                NoteCrownHangSail(hatSail);
                RememberLeafSway(
                    mass,
                    (winterWoodyCrown ? 0.90f : 1.35f) * layerSail * hatSail,
                    2.5f + i * 0.35f);
            }
            return;
        }

        // Max-Y is the leader (near 0°) after outward laterals. Two
        // potatoes on the spine left the new ±fork limbs as bare
        // sticks at 1x. Dome on the tallest tip; next masses on the
        // spread flanks. forkAngle 28 / colliders stay.
        int pDome = PickCrownByY(-1, -1);
        int pOut = PickCrownByAbsHeading(pDome, -1, 0f);
        if (pOut < 0 || Mathf.Abs(nodes[pOut].heading) < 8f)
            pOut = PickCrownByY(pDome, -1);
        float outSign = 0f;
        if (pOut >= 0)
            outSign = Mathf.Sign(nodes[pOut].heading);
        int pFlank = outSign != 0f
            ? PickCrownByAbsHeading(pDome, pOut, -outSign)
            : PickCrownByY(pDome, pOut);
        if (pFlank < 0)
            pFlank = PickCrownByY(pDome, pOut);

        AttachCrownOn(pDome, 0);
        if (crownMassCount >= 2)
            AttachCrownOn(pOut, 1);
        if (crownMassCount >= 3)
            AttachCrownOn(pFlank, 2);
    }

    // Tallest depth≥1 wood in tree-local Y. Skip used slots.
    private int PickCrownByY(int skipA, int skipB)
    {
        int best = -1;
        float bestY = float.NegativeInfinity;
        for (int i = 0; i < nodes.Count; i++)
        {
            if (i == skipA || i == skipB)
                continue;
            Node hit = nodes[i];
            if (hit.segment == null || hit.depth < 1)
                continue;
            float y = hit.segment.transform.localPosition.y;
            if (y > bestY)
            {
                bestY = y;
                best = i;
            }
        }
        return best;
    }

    // Spread depth≥1 wood. requiredSign 0 = any flank; else that sign
    // with |heading| > 5° so a leader at 8° of 28° does not steal the
    // side cloud. No alloc.
    private int PickCrownByAbsHeading(int skipA, int skipB, float requiredSign)
    {
        int best = -1;
        float bestAh = -1f;
        for (int i = 0; i < nodes.Count; i++)
        {
            if (i == skipA || i == skipB)
                continue;
            Node hit = nodes[i];
            if (hit.segment == null || hit.depth < 1)
                continue;
            if (requiredSign != 0f && hit.heading * requiredSign < SpreadLimbDeg)
                continue;
            float ah = Mathf.Abs(hit.heading);
            if (ah > bestAh)
            {
                bestAh = ah;
                best = i;
            }
        }
        return best;
    }

    private void AttachCrownOn(int nodeIndex, int slot)
    {
        if (nodeIndex < 0 || nodeIndex >= nodes.Count)
            return;
        HumanSegment wood = nodes[nodeIndex].segment;
        if (wood == null)
            return;

        // Sit the cloud on the inner tip. Mid-internode (0.82) hung the
        // mass on the grow box after 05:18 thinned the picture cone.
        float along = 0.90f;
        int woodDepth = nodes[nodeIndex].depth;
        float heading = nodes[nodeIndex].heading;
        bool spreadLimb = Mathf.Abs(heading) > SpreadLimbDeg;
        float outSign = spreadLimb
            ? Mathf.Sign(heading)
            : ((slot & 1) == 0 ? -1f : 1f);
        // Slot parity hung left/right of the leader. Outward laterals
        // need the cloud on the outside of the fork, not a hat on the
        // stick. Colliders stay.
        float hang = outSign
            * PictureWidthAt(wood.size.x, along, woodDepth)
            * (spreadLimb ? 0.52f : 0.35f);
        float tilt = 0f;
        if (leafHangWorldDown)
        {
            Vector3 down = wood.transform.InverseTransformDirection(Vector3.down);
            tilt = Mathf.Atan2(down.x, down.y) * Mathf.Rad2Deg;
            hang = down.x * PictureWidthAt(wood.size.x, 0.55f, woodDepth) * 0.20f;
            along = 0.55f;
        }
        else if (ResolvedKind != TreeKind.Poplar && spreadLimb)
        {
            // Wood already carries heading. A 0° cloud is a green flag
            // on the stick (pole analog). Flatten toward world-up so
            // palmate / hedge / orchard sit as a canopy. Lombardy stays
            // a column. BindLeafSway caches this rest. No Find.
            tilt = -heading * PalmateTrack;
        }

        Color c = winterWoodyCrown
            ? WoodCrownTint(slot + 3)
            : LeafTint(slot + 3, slot == 1);
        Transform mass = AddFlatPicture(
            wood.transform,
            wood.name + "_Crown" + slot,
            crownSize,
            c,
            wood.sortingOrder + 1,
            new Vector3(hang, (along - 0.5f) * wood.size.y, 0f),
            tilt,
            CrownBake);
        // Grow box is a slab. Cloud / hedge / orchard / willow curtain
        // read from extra boxes; leaving the slab on made every deciduous
        // a capsule and the willow a green pole. Poplar's 15×54 cm
        // column is the same capsule — hide it so the flame owns 1x.
        if (leafHangWorldDown || crownLobes >= 2 || ResolvedKind == TreeKind.Poplar)
            HidePictureBody(mass);
        AttachCrownLobes(mass, c, wood.sortingOrder + 1, -1f, slot, heading);
        // Leader wood is ~8° of fork 28. The cloud is a plate (hang +
        // envelope); wood sail left the dome as pole flutter. Willow
        // hang and Lombardy stay on the stick. Picture only.
        float alongLocalY = (along - 0.5f) * wood.size.y;
        float plateHalf = (!leafHangWorldDown && ResolvedKind != TreeKind.Poplar)
            ? crownSize.x * 0.22f
            : 0f;
        float crownSail = PicturePlateSail(heading, hang, alongLocalY, plateHalf);
        NoteCrownHangSail(crownSail);
        RememberLeafSway(mass, (winterWoodyCrown ? 0.90f : 1.35f) * crownSail, 2.6f + slot * 0.4f);
    }

    // Same tint as the parent box so extras are one silhouette, not a shade.
    // They ride the leaf transform — do not add them to leafSwayAcc.
    private void AttachLeafLobes(Transform leaf, Color color, int sortingOrder)
    {
        if (leaf == null)
            return;
        bool dense = maxSegments > 31;
        if (barkLook == BarkLook.Scale)
        {
            if (ResolvedKind == TreeKind.Cedar)
            {
                // 16×4 cm grow paddle is a green ruler (willow-card analog).
                // Hide it; layered pads, not one overlapping ruler.
                // Same leafSize envelope. Y-span 0.22 collapsed three
                // pancakes into one shelf; 0.48 fills the 4 cm tuft.
                HidePictureBody(leaf);
                int fans = dense ? 2 : 3;
                for (int f = 0; f < fans; f++)
                {
                    float u = fans == 1 ? 0f : (f / (float)(fans - 1) - 0.5f) * 2f;
                    float fanW = leafSize.x * (f == 1 ? 0.50f : (f == 0 ? 0.44f : 0.38f));
                    float fanH = leafSize.y * (f == 0 ? 0.36f : 0.22f);
                    float fanAng = f == 0 ? -12f : (f == 1 ? 4f : 14f);
                    // |x| 0.16 put Fan1 on the 16 cm paddle spine
                    // (crown FanM analog). Visual Fan2 |x|≥0.36.
                    // Fan1 0.62 at −0.36 right −0.05 / Fan2 0.44 at
                    // +0.46 left +0.24 left a 0.29 sky slit (poplar
                    // KiteL/R analog). Kiss x=0; Fan0 stays the outer
                    // left table. leafSize stays.
                    float fanX = u * leafSize.x * 0.46f;
                    if (fans == 3 && f == 1)
                    {
                        fanW = leafSize.x * 0.28f;
                        fanX = -leafSize.x * 0.14f;
                    }
                    else if (fans == 3 && f == 2)
                    {
                        fanW = leafSize.x * 0.30f;
                        fanX = leafSize.x * 0.15f;
                    }
                    else if (fans == 2)
                    {
                        // Static cedar 64: two fans were tents
                        // (f==0 right −0.21 / f==1 left +0.15).
                        // Same kiss as the 3-fan Sway tuft.
                        fanW = leafSize.x * (f == 0 ? 0.28f : 0.30f);
                        fanX = leafSize.x * (f == 0 ? -0.14f : 0.15f);
                    }
                    AddFlatPicture(
                        leaf,
                        leaf.name + "_Fan" + f,
                        new Vector2(fanW, fanH),
                        f == 1 ? OddLeaflet(color) : CrownPatch(color, f == fans - 1),
                        sortingOrder,
                        new Vector3(
                            fanX,
                            (f - (fans - 1) * 0.5f) * leafSize.y * 0.48f,
                            0f),
                        fanAng,
                        ArtLibrary.Leaf);
                    float tipSign = fanX >= 0f ? 1f : -1f;
                    AddFlatPicture(
                        leaf,
                        leaf.name + "_FanTip" + f,
                        new Vector2(fanW * 0.28f, fanH * 0.18f),
                        f == 1 ? OddLeaflet(color) : CrownPatch(color, f == fans - 1),
                        sortingOrder,
                        // Visual 19:46 leftover: Fan2 a mid pancake
                        // after FanTip. Thinner pad; tip hangs further
                        // off. Sinus stays empty. leafSize stays.
                        new Vector3(
                            fanX + tipSign * fanW * 0.52f,
                            (f - (fans - 1) * 0.5f) * leafSize.y * 0.48f
                                - fanH * 0.42f,
                            0f),
                        fanAng + tipSign * 14f,
                        ArtLibrary.Leaf);
                }
                // 0.58 at −16° was a belly curtain on the 4 cm tuft
                // (crown fringe 0.80 analog). Short needles 8–10°.
                // leafSize stays.
                AddFlatPicture(
                    leaf,
                    leaf.name + "_FringeL",
                    new Vector2(leafSize.x * 0.10f, leafSize.y * 0.36f),
                    color,
                    sortingOrder,
                    new Vector3(-leafSize.x * 0.38f, -leafSize.y * 0.42f, 0f),
                    -10f,
                    ArtLibrary.Leaf);
                AddFlatPicture(
                    leaf,
                    leaf.name + "_FringeR",
                    new Vector2(leafSize.x * 0.08f, leafSize.y * 0.32f),
                    OddLeaflet(color),
                    sortingOrder,
                    new Vector3(leafSize.x * 0.34f, -leafSize.y * 0.36f, 0f),
                    8f,
                    ArtLibrary.Leaf);
                // −0.12 sat IN the fan sinus after Fan1 left for 0.36
                // (apple Scallop1 analog). Drip hangs off Fan1.
                // leafSize stays.
                AddFlatPicture(
                    leaf,
                    leaf.name + "_FringeM",
                    new Vector2(leafSize.x * 0.07f, leafSize.y * 0.30f),
                    color,
                    sortingOrder,
                    new Vector3(-leafSize.x * 0.36f, -leafSize.y * 0.40f, 0f),
                    9f,
                    ArtLibrary.Leaf);
                AddFlatPicture(
                    leaf,
                    leaf.name + "_FringeLTip",
                    new Vector2(leafSize.x * 0.07f, leafSize.y * 0.18f),
                    color,
                    sortingOrder,
                    // Crown FringeLTip closed the shelf needle.
                    // Leaflet Fringe without a lip stayed green posts
                    // on the tuft. Hang off the outer needle;
                    // leafSize stays.
                    new Vector3(-leafSize.x * 0.44f, -leafSize.y * 0.50f, 0f),
                    -16f,
                    ArtLibrary.Leaf);
                AddFlatPicture(
                    leaf,
                    leaf.name + "_FringeRTip",
                    new Vector2(leafSize.x * 0.055f, leafSize.y * 0.16f),
                    OddLeaflet(color),
                    sortingOrder,
                    new Vector3(leafSize.x * 0.40f, -leafSize.y * 0.44f, 0f),
                    14f,
                    ArtLibrary.Leaf);
                AddFlatPicture(
                    leaf,
                    leaf.name + "_FringeMTip",
                    new Vector2(leafSize.x * 0.05f, leafSize.y * 0.14f),
                    color,
                    sortingOrder,
                    new Vector3(-leafSize.x * 0.42f, -leafSize.y * 0.48f, 0f),
                    14f,
                    ArtLibrary.Leaf);
                return;
            }
            // Fascicle: bark cup + 2–3 overlapping needles. Five spread
            // fingers were a comb; 0.16 posts at ±6° were green sticks.
            // Sheath wholly left of 0 piled the blades on one lip.
            // Cup kisses x=0; left / on-cup / right fan. leafSize stays.
            HidePictureBody(leaf);
            int nNeedles = dense ? 2 : 3;
            AddFlatPicture(
                leaf,
                leaf.name + "_Sheath",
                new Vector2(leafSize.x * 0.24f, leafSize.y * 0.22f),
                palBarkLite,
                sortingOrder,
                // 0.28 at −0.22 sat wholly left of 0, so two needles
                // piled on that cup and Needle2 at +0.16 never touched
                // it (comma analog). Cup kisses x=0; blades fan off
                // both lips. leafSize stays.
                new Vector3(-leafSize.x * 0.10f, -leafSize.y * 0.38f, 0f),
                4f,
                ArtLibrary.Bark);
            AddFlatPicture(
                leaf,
                leaf.name + "_SheathTip",
                new Vector2(leafSize.x * 0.14f, leafSize.y * 0.12f),
                palBarkLite,
                sortingOrder,
                // CapTip closed the fir peak. Sheath without a lip
                // stayed a bark card under the fascicle (CarmB
                // before CarmBTip). Hang off the cup; leafSize stays.
                new Vector3(-leafSize.x * 0.16f, -leafSize.y * 0.46f, 0f),
                10f,
                ArtLibrary.Bark);
            for (int k = 0; k < nNeedles; k++)
            {
                bool last = k == nNeedles - 1;
                float lenMul = k == 0 ? 0.32f : (last ? 0.28f : 0.36f);
                float ang = k == 0 ? -8f : (last ? 10f : 2f);
                // Left blade / on-cup / right fan. Dense n=2 skips the
                // mid needle so the pair still reads as a bundle, not
                // a left comb. Sheath right +0.02. leafSize stays.
                float needleX = k == 0
                    ? -leafSize.x * 0.24f
                    : (last ? leafSize.x * 0.12f : -leafSize.x * 0.08f);
                // Visual 19:46 leftover: 0.78/0.70/0.96 posts plus a
                // coaxial cap were still green sticks. Short shaft at
                // the cup, long thin distal — a fascicle, not a post.
                // leafSize stays.
                float needleW = k == 0 ? 0.11f : (last ? 0.09f : 0.08f);
                float needleY = k == 0
                    ? -leafSize.y * 0.18f
                    : (last ? -leafSize.y * 0.08f : -leafSize.y * 0.12f);
                Color needleC = last ? OddLeaflet(color) : color;
                AddFlatPicture(
                    leaf,
                    leaf.name + "_Needle" + k,
                    new Vector2(
                        leafSize.x * needleW,
                        leafSize.y * lenMul),
                    needleC,
                    sortingOrder,
                    new Vector3(needleX, needleY, 0f),
                    ang,
                    ArtLibrary.Leaf);
                float tipAlong = k == 0 ? 0.28f : (last ? 0.22f : 0.34f);
                float tipSign = needleX >= 0f ? 1f : -1f;
                AddFlatPicture(
                    leaf,
                    leaf.name + "_NeedleTip" + k,
                    new Vector2(
                        leafSize.x * (last || k == 0 ? 0.07f : 0.06f),
                        leafSize.y * (k == 0 ? 0.50f : (last ? 0.42f : 0.48f))),
                    needleC,
                    sortingOrder,
                    new Vector3(
                        needleX + tipSign * leafSize.x * 0.02f,
                        leafSize.y * tipAlong,
                        0f),
                    ang - 8f,
                    ArtLibrary.Leaf);
            }
            return;
        }
        if (leafHangWorldDown)
        {
            // 06:52 kept the grow paddle so a 2 cm drip would not vanish.
            // The paddle is an 8×18 cm card; at 1x a willow is a green
            // stick, not a lance. Hide it and rebuild one sickle that
            // still fills leafSize. Offset drip = weeping, not a post.
            HidePictureBody(leaf);
            // 0.16×0.62 Blade + drip was still a green pole (Hang analog).
            // Hang already aims +Y at world down. 0.22×0.36 of leafSize
            // 0.08×0.18 is a 1.8 cm tick (crown Hang 0.14×crown.x analog).
            // C-sickle fills the 8×18 cm envelope. 0.70 at stepX×0.08
            // still sat on the hang spine after Tip left for the drip
            // (Needle1 analog). Narrower pad rides the Tip station.
            // leafSize stays.
            float stepX = Mathf.Max(leafSize.x * 0.42f, 0.028f);
            float drop = Mathf.Max(leafSize.y * 0.28f, 0.032f);
            // Visual 19:26 leftover: Blade a sickle paddle, BladeTip
            // on the spine. Short C at the origin; tip hangs off the
            // sickle. leafSize stays.
            AddFlatPicture(
                leaf,
                leaf.name + "_Blade",
                new Vector2(leafSize.x * 0.22f, leafSize.y * 0.22f),
                color,
                sortingOrder,
                new Vector3(stepX * 0.42f, drop * 0.06f, 0f),
                18f,
                ArtLibrary.Leaf);
            AddFlatPicture(
                leaf,
                leaf.name + "_BladeTip",
                new Vector2(leafSize.x * 0.12f, leafSize.y * 0.10f),
                color,
                sortingOrder,
                new Vector3(stepX * 0.82f, drop * 0.48f, 0f),
                28f,
                ArtLibrary.Leaf);
            AddFlatPicture(
                leaf,
                leaf.name + "_Tip",
                new Vector2(leafSize.x * 0.22f, leafSize.y * 0.14f),
                color,
                sortingOrder,
                // After Hang0–3 curtain kiss: Tip 0.26 at 1.12 still a
                // mid-lance paddle between BladeTip and Drip (crown
                // CapTip peak slit analog). Narrower distal sickle;
                // TipTip owns the peak. leafSize stays.
                new Vector3(stepX * 1.18f, drop * 1.12f, 0f),
                36f,
                ArtLibrary.Leaf);
            AddFlatPicture(
                leaf,
                leaf.name + "_TipTip",
                new Vector2(leafSize.x * 0.12f, leafSize.y * 0.09f),
                color,
                sortingOrder,
                new Vector3(stepX * 1.38f, drop * 1.36f, 0f),
                42f,
                ArtLibrary.Leaf);
            AddFlatPicture(
                leaf,
                leaf.name + "_Drip",
                new Vector2(leafSize.x * 0.36f, leafSize.y * 0.40f),
                CrownPatch(color, true),
                sortingOrder,
                new Vector3(stepX * 1.55f, drop * 1.55f, 0f),
                48f,
                ArtLibrary.Leaf);
            AddFlatPicture(
                leaf,
                leaf.name + "_DripTip",
                new Vector2(leafSize.x * 0.20f, leafSize.y * 0.18f),
                CrownPatch(color, true),
                sortingOrder,
                // Crown DripTip closed the curtain. Leaflet Drip
                // without a lip stayed a paddle (Droop before
                // DroopTip). Hang off the sickle; leafSize stays.
                new Vector3(stepX * 1.85f, drop * 1.85f, 0f),
                54f,
                ArtLibrary.Leaf);
            // Blade/Tip/Drip all at +stepX was a hanging comma
            // (bush Blade before CheekR). BladeR kisses the join
            // from the right; TipR/DripR are distal lips (CorkTip
            // analog). One sickle, not a second curtain. leafSize stays.
            AddFlatPicture(
                leaf,
                leaf.name + "_BladeR",
                new Vector2(leafSize.x * 0.28f, leafSize.y * 0.36f),
                CrownPatch(color, true),
                sortingOrder,
                new Vector3(stepX * 0.38f, drop * 0.10f, 0f),
                14f,
                ArtLibrary.Leaf);
            AddFlatPicture(
                leaf,
                leaf.name + "_BladeRTip",
                new Vector2(leafSize.x * 0.14f, leafSize.y * 0.14f),
                CrownPatch(color, true),
                sortingOrder,
                // BladeTip closed the hedge ovate. Willow BladeR
                // without a lip stayed a sickle card after DripTip
                // (Cap before CapTip). Hang off the far join;
                // leafSize stays.
                new Vector3(stepX * 0.52f, drop * 0.22f, 0f),
                20f,
                ArtLibrary.Leaf);
            AddFlatPicture(
                leaf,
                leaf.name + "_TipR",
                // After Tip walked distal: TipR at 1.22 still sat on
                // the mid-lance and rebuilt a second curtain. Far lip
                // rides the new Tip station; TipRTip owns the peak.
                // leafSize stays.
                new Vector2(leafSize.x * 0.20f, leafSize.y * 0.12f),
                CrownPatch(color, true),
                sortingOrder,
                new Vector3(stepX * 1.28f, drop * 1.16f, 0f),
                40f,
                ArtLibrary.Leaf);
            AddFlatPicture(
                leaf,
                leaf.name + "_TipRTip",
                new Vector2(leafSize.x * 0.12f, leafSize.y * 0.09f),
                CrownPatch(color, true),
                sortingOrder,
                // TipTip closed the near sickle peak. TipR without a
                // distal lip stayed a far paddle after BladeRTip.
                // Hang between TipR and DripR; leafSize stays.
                new Vector3(stepX * 1.48f, drop * 1.38f, 0f),
                48f,
                ArtLibrary.Leaf);
            AddFlatPicture(
                leaf,
                leaf.name + "_DripR",
                new Vector2(leafSize.x * 0.28f, leafSize.y * 0.32f),
                OddLeaflet(color),
                sortingOrder,
                new Vector3(stepX * 1.82f, drop * 1.78f, 0f),
                52f,
                ArtLibrary.Leaf);
            AddFlatPicture(
                leaf,
                leaf.name + "_DripRTip",
                new Vector2(leafSize.x * 0.16f, leafSize.y * 0.16f),
                OddLeaflet(color),
                sortingOrder,
                new Vector3(stepX * 2.08f, drop * 2.02f, 0f),
                58f,
                ArtLibrary.Leaf);
            return;
        }
        // Petiole on a lobed oak is a second stick under the 92% rib
        // (plus at 1x) — Wrist on LobeL owns that join. Ovate stalk
        // used to sit at x=0.02 under the bite (Wrist analog) and
        // filled the sinus from below. It rides Base after Hide.
        // Poplar/bush rebuild their own wrist/blade after HidePictureBody.
        if (leafLobes >= 2)
        {
            // Oak: palmate one silhouette. LobeR0 0.18 at 0.22 was a
            // mitten (left mass, stub right). Outer right lobe + FingerR
            // past ToothR; sinus between LobeL0 and LobeR0 stays empty.
            // leafSize stays.
            HidePictureBody(leaf);
            AddFlatPicture(
                leaf,
                leaf.name + "_Wrist",
                new Vector2(leafSize.x * 0.22f, leafSize.y * 0.28f),
                color,
                sortingOrder,
                // (0, −0.40) sat IN the sinus from below (crown Core
                // analog). Pad rides LobeL0; FingerR stays the outer
                // tooth. leafSize stays.
                new Vector3(-leafSize.x * 0.22f, -leafSize.y * 0.36f, 0f),
                6f,
                ArtLibrary.Leaf);
            AddFlatPicture(
                leaf,
                leaf.name + "_WristTip",
                new Vector2(leafSize.x * 0.12f, leafSize.y * 0.12f),
                color,
                sortingOrder,
                // Wrist without a distal chip stayed a petiole stick
                // after LobeL1Tip closed the outer lobe (CarmB before
                // CarmBTip). Hang off the caudal pad; sinus stays
                // empty. leafSize stays.
                new Vector3(-leafSize.x * 0.22f, -leafSize.y * 0.48f, 0f),
                10f,
                ArtLibrary.Leaf);
            AddFlatPicture(
                leaf,
                leaf.name + "_LobeL0",
                new Vector2(leafSize.x * 0.42f, leafSize.y * 0.26f),
                color,
                sortingOrder,
                new Vector3(-leafSize.x * 0.32f, leafSize.y * 0.02f, 0f),
                -44f,
                ArtLibrary.Leaf);
            AddFlatPicture(
                leaf,
                leaf.name + "_LobeL0Tip",
                new Vector2(leafSize.x * 0.22f, leafSize.y * 0.14f),
                color,
                sortingOrder,
                // LobeL1Tip closed the outer tooth. Inner LobeL0
                // without a lip stayed a blunt palmate pad (Cap
                // before CapTip). Hang off LobeL0; sinus stays
                // empty. leafSize stays.
                new Vector3(-leafSize.x * 0.48f, leafSize.y * 0.08f, 0f),
                -52f,
                ArtLibrary.Leaf);
            AddFlatPicture(
                leaf,
                leaf.name + "_LobeR0",
                new Vector2(leafSize.x * 0.28f, leafSize.y * 0.20f),
                color,
                sortingOrder,
                new Vector3(leafSize.x * 0.34f, leafSize.y * 0.10f, 0f),
                32f,
                ArtLibrary.Leaf);
            AddFlatPicture(
                leaf,
                leaf.name + "_LobeR0Tip",
                new Vector2(leafSize.x * 0.16f, leafSize.y * 0.12f),
                color,
                sortingOrder,
                new Vector3(leafSize.x * 0.48f, leafSize.y * 0.16f, 0f),
                38f,
                ArtLibrary.Leaf);
            if (dense)
                return;
            AddFlatPicture(
                leaf,
                leaf.name + "_LobeL1",
                new Vector2(leafSize.x * 0.22f, leafSize.y * 0.16f),
                color,
                sortingOrder,
                new Vector3(-leafSize.x * 0.64f, leafSize.y * 0.26f, 0f),
                -48f,
                ArtLibrary.Leaf);
            AddFlatPicture(
                leaf,
                leaf.name + "_LobeL1Tip",
                new Vector2(leafSize.x * 0.14f, leafSize.y * 0.10f),
                color,
                sortingOrder,
                // Crown LobeL1 without a lip stayed a blunt outer
                // lobe after plant closed the mitten (Cap before
                // CapTip). Hang off LobeL1; sinus stays empty.
                // leafSize stays.
                new Vector3(-leafSize.x * 0.76f, leafSize.y * 0.30f, 0f),
                -54f,
                ArtLibrary.Leaf);
            // Crown FingerL sits on LobeL. Leaflet stopped at LobeL1 —
            // a mitten on the left (FingerR 0.56 stub analog). Outer
            // tooth; sinus between LobeL0 and LobeR0 stays empty.
            // leafSize stays.
            AddFlatPicture(
                leaf,
                leaf.name + "_FingerL",
                new Vector2(leafSize.x * 0.16f, leafSize.y * 0.12f),
                color,
                sortingOrder,
                new Vector3(-leafSize.x * 0.52f, leafSize.y * 0.18f, 0f),
                -42f,
                ArtLibrary.Leaf);
            AddFlatPicture(
                leaf,
                leaf.name + "_FingerLTip",
                new Vector2(leafSize.x * 0.10f, leafSize.y * 0.08f),
                color,
                sortingOrder,
                // LobeL0Tip closed the inner pad. FingerL without a
                // lip stayed a mitten stub after LobeL1Tip (Cap before
                // CapTip). Hang off FingerL; sinus stays empty.
                // leafSize stays.
                new Vector3(-leafSize.x * 0.62f, leafSize.y * 0.22f, 0f),
                -48f,
                ArtLibrary.Leaf);
            AddFlatPicture(
                leaf,
                leaf.name + "_ToothL",
                new Vector2(leafSize.x * 0.14f, leafSize.y * 0.12f),
                color,
                sortingOrder,
                // Bush ToothR closed the far serration. Palmate stopped
                // at FingerL — a mitten on the left (crown ToothR
                // without ToothL). Inner tooth on LobeL0; sinus stays
                // empty. leafSize stays.
                new Vector3(-leafSize.x * 0.44f, leafSize.y * 0.14f, 0f),
                -36f,
                ArtLibrary.Leaf);
            AddFlatPicture(
                leaf,
                leaf.name + "_ToothLTip",
                new Vector2(leafSize.x * 0.08f, leafSize.y * 0.08f),
                color,
                sortingOrder,
                new Vector3(-leafSize.x * 0.52f, leafSize.y * 0.18f, 0f),
                -42f,
                ArtLibrary.Leaf);
            AddFlatPicture(
                leaf,
                leaf.name + "_LobeTip",
                new Vector2(leafSize.x * 0.20f, leafSize.y * 0.24f),
                color,
                sortingOrder,
                // (0.04, 0.28) sat IN the sinus between LobeL0 and
                // LobeR0 (crown Dome analog). Tip rides LobeL; FingerR
                // stays the outer tooth. leafSize stays.
                new Vector3(-leafSize.x * 0.22f, leafSize.y * 0.22f, 0f),
                -12f,
                ArtLibrary.Leaf);
            AddFlatPicture(
                leaf,
                leaf.name + "_LobeTipR",
                new Vector2(leafSize.x * 0.16f, leafSize.y * 0.18f),
                OddLeaflet(color),
                sortingOrder,
                // Left-only LobeTip was a one-tent palmate after
                // LobeL2/R2 (crown Dome before DomeR). Far peak rides
                // LobeR0 at |X|; sinus stays empty. leafSize stays.
                new Vector3(leafSize.x * 0.34f, leafSize.y * 0.20f, 0f),
                14f,
                ArtLibrary.Leaf);
            AddFlatPicture(
                leaf,
                leaf.name + "_ToothR",
                new Vector2(leafSize.x * 0.16f, leafSize.y * 0.14f),
                OddLeaflet(color),
                sortingOrder,
                new Vector3(leafSize.x * 0.56f, leafSize.y * 0.24f, 0f),
                40f,
                ArtLibrary.Leaf);
            AddFlatPicture(
                leaf,
                leaf.name + "_ToothRTip",
                new Vector2(leafSize.x * 0.10f, leafSize.y * 0.08f),
                OddLeaflet(color),
                sortingOrder,
                new Vector3(leafSize.x * 0.66f, leafSize.y * 0.28f, 0f),
                46f,
                ArtLibrary.Leaf);
            // Crown FingerR sits outside LobeR. The leaflet stopped at
            // ToothR 0.56 — a mitten stub (crown FingerR 0.12 analog).
            // Outer tip; sinus between LobeL0 and LobeR0 stays empty.
            // leafSize stays.
            AddFlatPicture(
                leaf,
                leaf.name + "_FingerR",
                new Vector2(leafSize.x * 0.14f, leafSize.y * 0.12f),
                OddLeaflet(color),
                sortingOrder,
                new Vector3(leafSize.x * 0.68f, leafSize.y * 0.30f, 0f),
                46f,
                ArtLibrary.Leaf);
            AddFlatPicture(
                leaf,
                leaf.name + "_FingerRTip",
                new Vector2(leafSize.x * 0.08f, leafSize.y * 0.08f),
                OddLeaflet(color),
                sortingOrder,
                new Vector3(leafSize.x * 0.78f, leafSize.y * 0.34f, 0f),
                52f,
                ArtLibrary.Leaf);
            AddFlatPicture(
                leaf,
                leaf.name + "_LobeR1",
                new Vector2(leafSize.x * 0.18f, leafSize.y * 0.14f),
                OddLeaflet(color),
                sortingOrder,
                // LobeL1 is the outer left lobe. Palmate stopped at
                // FingerR — a mitten on the right (LobeL1 without
                // LobeR1). Outer pad past ToothR; sinus stays empty.
                // leafSize stays.
                new Vector3(leafSize.x * 0.80f, leafSize.y * 0.22f, 0f),
                44f,
                ArtLibrary.Leaf);
            AddFlatPicture(
                leaf,
                leaf.name + "_LobeR1Tip",
                new Vector2(leafSize.x * 0.12f, leafSize.y * 0.10f),
                OddLeaflet(color),
                sortingOrder,
                new Vector3(leafSize.x * 0.92f, leafSize.y * 0.24f, 0f),
                50f,
                ArtLibrary.Leaf);
            // Crown Skirt is caudal on LobeL. Leaflet stopped at
            // Wrist — a one-tent hem under the palmate (crown KiteL2
            // analog after the lips kissed). LobeL2 rides LobeL0;
            // LobeR2 rides LobeR0. Sinus between LobeL0/R0 stays
            // empty (not a kiss at x=0). leafSize stays.
            AddFlatPicture(
                leaf,
                leaf.name + "_LobeL2",
                new Vector2(leafSize.x * 0.32f, leafSize.y * 0.18f),
                color,
                sortingOrder,
                new Vector3(-leafSize.x * 0.32f, -leafSize.y * 0.14f, 0f),
                -18f,
                ArtLibrary.Leaf);
            AddFlatPicture(
                leaf,
                leaf.name + "_LobeL2Tip",
                new Vector2(leafSize.x * 0.18f, leafSize.y * 0.10f),
                color,
                sortingOrder,
                // LobeL1Tip closed the outer tooth. Caudal LobeL2
                // without a lip stayed a blunt hem (Skirt before
                // SkirtTip). Hang off LobeL0; sinus stays empty.
                // leafSize stays.
                new Vector3(-leafSize.x * 0.44f, -leafSize.y * 0.22f, 0f),
                -24f,
                ArtLibrary.Leaf);
            AddFlatPicture(
                leaf,
                leaf.name + "_LobeR2",
                new Vector2(leafSize.x * 0.24f, leafSize.y * 0.16f),
                OddLeaflet(color),
                sortingOrder,
                new Vector3(leafSize.x * 0.34f, -leafSize.y * 0.06f, 0f),
                14f,
                ArtLibrary.Leaf);
            AddFlatPicture(
                leaf,
                leaf.name + "_LobeR2Tip",
                new Vector2(leafSize.x * 0.14f, leafSize.y * 0.10f),
                OddLeaflet(color),
                sortingOrder,
                new Vector3(leafSize.x * 0.46f, -leafSize.y * 0.12f, 0f),
                20f,
                ArtLibrary.Leaf);
            AddFlatPicture(
                leaf,
                leaf.name + "_WristR",
                new Vector2(leafSize.x * 0.18f, leafSize.y * 0.22f),
                OddLeaflet(color),
                sortingOrder,
                // Left-only Wrist was a one-tent stalk after LobeTipR
                // / LobeR2 (crown Core before CoreR). Far petiole
                // rides LobeR2 at |X|; sinus stays empty. leafSize stays.
                new Vector3(leafSize.x * 0.34f, -leafSize.y * 0.28f, 0f),
                -6f,
                ArtLibrary.Leaf);
            AddFlatPicture(
                leaf,
                leaf.name + "_WristRTip",
                new Vector2(leafSize.x * 0.10f, leafSize.y * 0.10f),
                OddLeaflet(color),
                sortingOrder,
                // WristTip closed the near palmate stalk. Leaflet
                // WristR without a lip stayed a far petiole card
                // after BaseTip (CarmB before CarmBTip). Hang off
                // LobeR2; sinus stays empty. leafSize stays.
                new Vector3(leafSize.x * 0.34f, -leafSize.y * 0.38f, 0f),
                -10f,
                ArtLibrary.Leaf);
            return;
        }

        if (ResolvedKind == TreeKind.Bush)
        {
            // Coin 1.05×0.92 at 0° was a square card. 0.42×0.82 Blade
            // was still a tile. Narrower ovate + tooth fills leafSize.
            // Extra boxes only.
            HidePictureBody(leaf);
            AddFlatPicture(
                leaf,
                leaf.name + "_Blade",
                new Vector2(leafSize.x * 0.28f, leafSize.y * 0.70f),
                color,
                sortingOrder,
                // −0.28 right −0.14 left a sky comma (plum Base analog
                // before CheekR). Blade 0.28 kisses x=0 from the left;
                // CheekR is the far egg. Tooth/Tip ride this pad.
                // leafSize stays.
                new Vector3(-leafSize.x * 0.14f, -leafSize.y * 0.04f, 0f),
                -12f,
                ArtLibrary.Leaf);
            AddFlatPicture(
                leaf,
                leaf.name + "_BladeTip",
                new Vector2(leafSize.x * 0.16f, leafSize.y * 0.16f),
                color,
                sortingOrder,
                // KiteLTip closed the flame pad. Bush Blade without a
                // lip stayed a hedge card after CheekR (Cap before
                // CapTip). Hang off Blade; leafSize stays.
                new Vector3(-leafSize.x * 0.20f, leafSize.y * 0.28f, 0f),
                -16f,
                ArtLibrary.Leaf);
            AddFlatPicture(
                leaf,
                leaf.name + "_Tooth",
                new Vector2(leafSize.x * 0.32f, leafSize.y * 0.28f),
                color,
                sortingOrder,
                new Vector3(-leafSize.x * 0.14f, leafSize.y * 0.04f, 0f),
                -26f,
                ArtLibrary.Leaf);
            AddFlatPicture(
                leaf,
                leaf.name + "_ToothTip",
                new Vector2(leafSize.x * 0.16f, leafSize.y * 0.14f),
                color,
                sortingOrder,
                new Vector3(-leafSize.x * 0.22f, leafSize.y * 0.12f, 0f),
                -32f,
                ArtLibrary.Leaf);
            AddFlatPicture(
                leaf,
                leaf.name + "_Tip",
                new Vector2(leafSize.x * 0.36f, leafSize.y * 0.40f),
                color,
                sortingOrder,
                // Peak rides Blade after that pad moved in to kiss.
                // The ovate is Blade+CheekR, not a left comma.
                // leafSize stays.
                new Vector3(-leafSize.x * 0.14f, leafSize.y * 0.22f, 0f),
                -8f,
                ArtLibrary.Leaf);
            AddFlatPicture(
                leaf,
                leaf.name + "_TipTip",
                new Vector2(leafSize.x * 0.18f, leafSize.y * 0.16f),
                color,
                sortingOrder,
                // BladeTip closed the hedge blade. Bush Tip without a
                // lip stayed a peak coin after CheekRTip (Cap before
                // CapTip). Hang off Tip; leafSize stays.
                new Vector3(-leafSize.x * 0.20f, leafSize.y * 0.36f, 0f),
                -14f,
                ArtLibrary.Leaf);
            AddFlatPicture(
                leaf,
                leaf.name + "_TipR",
                new Vector2(leafSize.x * 0.32f, leafSize.y * 0.34f),
                CrownPatch(color, true),
                sortingOrder,
                // Left-only Tip was a one-tent hedge after Blade/CheekR
                // kissed (poplar leaflet Tip before TipR). Left edge
                // 0 against Tip. leafSize stays.
                new Vector3(leafSize.x * 0.14f, leafSize.y * 0.20f, 0f),
                8f,
                ArtLibrary.Leaf);
            AddFlatPicture(
                leaf,
                leaf.name + "_TipRTip",
                new Vector2(leafSize.x * 0.16f, leafSize.y * 0.14f),
                CrownPatch(color, true),
                sortingOrder,
                // TipTip closed the near hedge peak. Bush TipR without
                // a lip stayed a far coin after CheekRTip (Drop before
                // DropTip). Hang off TipR; leafSize stays.
                new Vector3(leafSize.x * 0.20f, leafSize.y * 0.32f, 0f),
                14f,
                ArtLibrary.Leaf);
            AddFlatPicture(
                leaf,
                leaf.name + "_CheekR",
                new Vector2(leafSize.x * 0.28f, leafSize.y * 0.56f),
                CrownPatch(color, true),
                sortingOrder,
                // Left edge 0 against Blade right 0 (plum CheekR /
                // pine BodyR analog). Far egg half; tooth stays left.
                // leafSize stays.
                new Vector3(leafSize.x * 0.14f, -leafSize.y * 0.02f, 0f),
                10f,
                ArtLibrary.Leaf);
            AddFlatPicture(
                leaf,
                leaf.name + "_CheekRTip",
                new Vector2(leafSize.x * 0.14f, leafSize.y * 0.16f),
                CrownPatch(color, true),
                sortingOrder,
                // Packed CheekRTip closed the spur egg. Bush CheekR
                // without a lip stayed a hedge coin after BaseTip
                // (Drop before DropTip). Hang off the far egg;
                // leafSize stays.
                new Vector3(leafSize.x * 0.22f, -leafSize.y * 0.18f, 0f),
                16f,
                ArtLibrary.Leaf);
            AddFlatPicture(
                leaf,
                leaf.name + "_ToothR",
                new Vector2(leafSize.x * 0.28f, leafSize.y * 0.24f),
                CrownPatch(color, true),
                sortingOrder,
                // Left-only Tooth was a one-serration hedge after
                // TipR/CheekR (apple ToothL/R analog). Far tooth
                // rides CheekR. leafSize stays.
                new Vector3(leafSize.x * 0.14f, leafSize.y * 0.02f, 0f),
                22f,
                ArtLibrary.Leaf);
            AddFlatPicture(
                leaf,
                leaf.name + "_ToothRTip",
                new Vector2(leafSize.x * 0.14f, leafSize.y * 0.12f),
                CrownPatch(color, true),
                sortingOrder,
                new Vector3(leafSize.x * 0.22f, leafSize.y * 0.08f, 0f),
                28f,
                ArtLibrary.Leaf);
            return;
        }

        if (ResolvedKind == TreeKind.Poplar)
        {
            // Two similar rhombs at ±22/18 were remex-plus. Unequal kite
            // plus a short wrist: lombardy at 1x. Grow leafSize stays.
            HidePictureBody(leaf);
            AddFlatPicture(
                leaf,
                leaf.name + "_Wrist",
                new Vector2(leafSize.x * 0.18f, leafSize.y * 0.22f),
                color,
                sortingOrder,
                // (0, −0.40) sat IN the kite valley from below (crown
                // Apex analog after Apex left KiteL). Pad rides KiteL
                // after the lips moved in to kiss. leafSize stays.
                new Vector3(-leafSize.x * 0.17f, -leafSize.y * 0.32f, 0f),
                4f,
                ArtLibrary.Leaf);
            AddFlatPicture(
                leaf,
                leaf.name + "_WristTip",
                new Vector2(leafSize.x * 0.10f, leafSize.y * 0.10f),
                color,
                sortingOrder,
                // Palmate WristTip closed the oak stalk. Poplar Wrist
                // without a lip stayed a kite petiole after ApexTip
                // (CarmB before CarmBTip). Hang off KiteL; valley
                // stays the kites. leafSize stays.
                new Vector3(-leafSize.x * 0.17f, -leafSize.y * 0.42f, 0f),
                8f,
                ArtLibrary.Leaf);
            AddFlatPicture(
                leaf,
                leaf.name + "_KiteL",
                new Vector2(leafSize.x * 0.34f, leafSize.y * 0.48f),
                color,
                sortingOrder,
                // −0.22 right −0.05 / KiteR left +0.10 left a 0.15
                // sky slit on the 7 cm tuft (crown flame analog).
                // A palmate sinus is oak; a kite kisses at x=0 like
                // the column. Waist stays a chip on KiteL, not a
                // sausage in the valley. leafSize stays.
                new Vector3(-leafSize.x * 0.17f, 0.02f * leafSize.y, 0f),
                -34f,
                ArtLibrary.Leaf);
            AddFlatPicture(
                leaf,
                leaf.name + "_KiteLTip",
                new Vector2(leafSize.x * 0.18f, leafSize.y * 0.18f),
                color,
                sortingOrder,
                // Crown KiteLTip closed the flame pad. Leaflet KiteL
                // without a lip stayed a blunt kite after ApexTip
                // (Cap before CapTip). Hang off the left kite; kiss
                // at x=0 stays the kites. leafSize stays.
                new Vector3(-leafSize.x * 0.28f, leafSize.y * 0.14f, 0f),
                -40f,
                ArtLibrary.Leaf);
            AddFlatPicture(
                leaf,
                leaf.name + "_KiteR",
                new Vector2(leafSize.x * 0.36f, leafSize.y * 0.32f),
                CrownPatch(color, true),
                sortingOrder,
                // Left edge 0 against KiteL right 0 (crown KiteR /
                // pine BodyR analog). Far kite stays the larger pad.
                // leafSize stays.
                new Vector3(leafSize.x * 0.18f, 0f, 0f),
                16f,
                ArtLibrary.Leaf);
            AddFlatPicture(
                leaf,
                leaf.name + "_KiteRTip",
                new Vector2(leafSize.x * 0.18f, leafSize.y * 0.14f),
                CrownPatch(color, true),
                sortingOrder,
                new Vector3(leafSize.x * 0.28f, leafSize.y * 0.10f, 0f),
                22f,
                ArtLibrary.Leaf);
            // Crown Waist 0.28 sits on KiteL. Two 0.42 kites here were
            // still remex (crown 0.78/0.72 analog). −0.12 × 0.28 still
            // reached +0.02 and filled the kite valley (Needle1 analog
            // after Sheath left the pad). Pad rides KiteL. leafSize stays.
            AddFlatPicture(
                leaf,
                leaf.name + "_Waist",
                new Vector2(leafSize.x * 0.28f, leafSize.y * 0.24f),
                CrownPatch(color, true),
                sortingOrder,
                new Vector3(-leafSize.x * 0.17f, -leafSize.y * 0.08f, 0f),
                4f,
                ArtLibrary.Leaf);
            AddFlatPicture(
                leaf,
                leaf.name + "_WaistTip",
                new Vector2(leafSize.x * 0.14f, leafSize.y * 0.10f),
                CrownPatch(color, true),
                sortingOrder,
                // KiteL2Tip closed the hem. Leaflet Waist without a
                // lip stayed a kite midrib card after ApexTip (Cap
                // before CapTip). Hang off Waist; valley stays the
                // kites. leafSize stays.
                new Vector3(-leafSize.x * 0.26f, -leafSize.y * 0.16f, 0f),
                10f,
                ArtLibrary.Leaf);
            AddFlatPicture(
                leaf,
                leaf.name + "_WaistR",
                new Vector2(leafSize.x * 0.24f, leafSize.y * 0.20f),
                CrownPatch(color, true),
                sortingOrder,
                // Left-only Waist was a one-tent kite midrib after
                // WristR/TipR (ovate Waist analog). Far chip rides
                // KiteR; valley stays the kites. leafSize stays.
                new Vector3(leafSize.x * 0.18f, -leafSize.y * 0.06f, 0f),
                -4f,
                ArtLibrary.Leaf);
            AddFlatPicture(
                leaf,
                leaf.name + "_WaistRTip",
                new Vector2(leafSize.x * 0.12f, leafSize.y * 0.08f),
                CrownPatch(color, true),
                sortingOrder,
                new Vector3(leafSize.x * 0.26f, -leafSize.y * 0.12f, 0f),
                -10f,
                ArtLibrary.Leaf);
            AddFlatPicture(
                leaf,
                leaf.name + "_Tip",
                new Vector2(leafSize.x * 0.34f, leafSize.y * 0.36f),
                color,
                sortingOrder,
                // (0.02, 0.34) filled the kite valley (crown Apex analog).
                // Width 0.34 kisses x=0; TipR is the far peak (crown
                // ApexR analog). Waist stays on KiteL. leafSize stays.
                new Vector3(-leafSize.x * 0.17f, leafSize.y * 0.28f, 0f),
                -12f,
                ArtLibrary.Leaf);
            AddFlatPicture(
                leaf,
                leaf.name + "_TipTip",
                new Vector2(leafSize.x * 0.18f, leafSize.y * 0.14f),
                color,
                sortingOrder,
                // Packed kite TipTip closed the spur flame. Leaflet
                // Tip without a lip stayed a kite paddle after
                // ApexTip (Cap before CapTip). Hang off Tip; valley
                // stays the kites. leafSize stays.
                new Vector3(-leafSize.x * 0.24f, leafSize.y * 0.40f, 0f),
                -18f,
                ArtLibrary.Leaf);
            AddFlatPicture(
                leaf,
                leaf.name + "_TipR",
                new Vector2(leafSize.x * 0.30f, leafSize.y * 0.30f),
                CrownPatch(color, true),
                sortingOrder,
                // Left-only Tip was a one-tent kite after KiteL/R
                // kissed (crown Apex before ApexR). Left edge 0
                // against Tip right 0. leafSize stays.
                new Vector3(leafSize.x * 0.15f, leafSize.y * 0.26f, 0f),
                8f,
                ArtLibrary.Leaf);
            AddFlatPicture(
                leaf,
                leaf.name + "_TipRTip",
                new Vector2(leafSize.x * 0.16f, leafSize.y * 0.12f),
                CrownPatch(color, true),
                sortingOrder,
                // TipTip closed the near kite peak. Leaflet TipR
                // without a lip stayed a far paddle after WaistRTip
                // (Drop before DropTip). Hang off TipR; leafSize stays.
                new Vector3(leafSize.x * 0.22f, leafSize.y * 0.36f, 0f),
                14f,
                ArtLibrary.Leaf);
            // 0.64 KiteL was still a paddle on the 7×9 cm tuft
            // (crown sausage analog). 0.34 kite + Waist + KiteL2 is a
            // flame, not a card. leafSize stays.
            AddFlatPicture(
                leaf,
                leaf.name + "_KiteL2",
                new Vector2(leafSize.x * 0.36f, leafSize.y * 0.28f),
                color,
                sortingOrder,
                // −0.30 right −0.12 left a one-tent hem after KiteL/R
                // kissed (crown KiteL2 analog). Width 0.36 kisses x=0
                // from the left; KiteR2 is the far hem. leafSize stays.
                new Vector3(-leafSize.x * 0.18f, -leafSize.y * 0.16f, 0f),
                -18f,
                ArtLibrary.Leaf);
            AddFlatPicture(
                leaf,
                leaf.name + "_KiteL2Tip",
                new Vector2(leafSize.x * 0.18f, leafSize.y * 0.12f),
                color,
                sortingOrder,
                // Crown KiteLTip closed the flame pad. Leaflet KiteL2
                // without a lip stayed a blunt hem after ApexTip
                // (Cap before CapTip). Hang off KiteL2; kiss at x=0
                // stays the kites. leafSize stays.
                new Vector3(-leafSize.x * 0.28f, -leafSize.y * 0.24f, 0f),
                -24f,
                ArtLibrary.Leaf);
            AddFlatPicture(
                leaf,
                leaf.name + "_KiteR2",
                new Vector2(leafSize.x * 0.34f, leafSize.y * 0.24f),
                CrownPatch(color, true),
                sortingOrder,
                // Left edge 0 against KiteL2 right 0 (KiteR analog).
                // Far hem; Waist stays on KiteL. leafSize stays.
                new Vector3(leafSize.x * 0.17f, -leafSize.y * 0.14f, 0f),
                14f,
                ArtLibrary.Leaf);
            AddFlatPicture(
                leaf,
                leaf.name + "_KiteR2Tip",
                new Vector2(leafSize.x * 0.16f, leafSize.y * 0.10f),
                CrownPatch(color, true),
                sortingOrder,
                new Vector3(leafSize.x * 0.26f, -leafSize.y * 0.22f, 0f),
                20f,
                ArtLibrary.Leaf);
            AddFlatPicture(
                leaf,
                leaf.name + "_Base",
                new Vector2(leafSize.x * 0.28f, leafSize.y * 0.16f),
                CrownPatch(color, true),
                sortingOrder,
                // Crown flame got Base/BaseR under KiteL2/R2; the
                // leaflet stopped at KiteR2 so the tuft was a one-tent
                // hem (crown Base 16:50 analog). Width 0.28 rides
                // KiteL2 (outer < 0). leafSize stays.
                new Vector3(-leafSize.x * 0.18f, -leafSize.y * 0.24f, 0f),
                -16f,
                ArtLibrary.Leaf);
            AddFlatPicture(
                leaf,
                leaf.name + "_BaseTip",
                new Vector2(leafSize.x * 0.14f, leafSize.y * 0.08f),
                CrownPatch(color, true),
                sortingOrder,
                // Crown BaseTip closed the flame hem. Leaflet Base
                // without a lip stayed a tuft card after WaistTip
                // (Cap before CapTip). Hang off KiteL2; valley stays
                // the kites. leafSize stays.
                new Vector3(-leafSize.x * 0.26f, -leafSize.y * 0.32f, 0f),
                -22f,
                ArtLibrary.Leaf);
            AddFlatPicture(
                leaf,
                leaf.name + "_BaseR",
                new Vector2(leafSize.x * 0.26f, leafSize.y * 0.14f),
                CrownPatch(color, true),
                sortingOrder,
                new Vector3(leafSize.x * 0.17f, -leafSize.y * 0.22f, 0f),
                12f,
                ArtLibrary.Leaf);
            AddFlatPicture(
                leaf,
                leaf.name + "_BaseRTip",
                new Vector2(leafSize.x * 0.13f, leafSize.y * 0.07f),
                CrownPatch(color, true),
                sortingOrder,
                new Vector3(leafSize.x * 0.25f, -leafSize.y * 0.30f, 0f),
                18f,
                ArtLibrary.Leaf);
            AddFlatPicture(
                leaf,
                leaf.name + "_WristR",
                new Vector2(leafSize.x * 0.16f, leafSize.y * 0.18f),
                CrownPatch(color, true),
                sortingOrder,
                // Left-only Wrist was a one-tent kite stalk after
                // TipR/BaseR (oak Wrist before WristR). Far join
                // rides KiteR2; valley stays the kites. leafSize stays.
                new Vector3(leafSize.x * 0.18f, -leafSize.y * 0.28f, 0f),
                -4f,
                ArtLibrary.Leaf);
            AddFlatPicture(
                leaf,
                leaf.name + "_WristRTip",
                new Vector2(leafSize.x * 0.08f, leafSize.y * 0.08f),
                CrownPatch(color, true),
                sortingOrder,
                new Vector3(leafSize.x * 0.18f, -leafSize.y * 0.36f, 0f),
                -8f,
                ArtLibrary.Leaf);
            return;
        }

        // Ovate: narrow base + waist + tip (apple / plum / hazel).
        // Hide the grow paddle. 0.82 then 0.58 Base was still a green
        // card (willow-paddle analog). Egg 0.44 + waist + tip. Teeth
        // sit outside the waist. leafSize stays.
        HidePictureBody(leaf);
        if (ResolvedKind == TreeKind.Hazel && !dense)
        {
            AddFlatPicture(
                leaf,
                leaf.name + "_EarL",
                new Vector2(leafSize.x * 0.30f, leafSize.y * 0.26f),
                color,
                sortingOrder,
                new Vector3(-leafSize.x * 0.34f, -leafSize.y * 0.20f, 0f),
                -22f,
                ArtLibrary.Leaf);
            AddFlatPicture(
                leaf,
                leaf.name + "_EarLTip",
                new Vector2(leafSize.x * 0.16f, leafSize.y * 0.12f),
                color,
                sortingOrder,
                // FingerLTip closed the palmate tooth. Hazel EarL
                // without a lip stayed a cordate stub (Cap before
                // CapTip). Hang off the ear; leafSize stays.
                new Vector3(-leafSize.x * 0.46f, -leafSize.y * 0.26f, 0f),
                -28f,
                ArtLibrary.Leaf);
            AddFlatPicture(
                leaf,
                leaf.name + "_EarR",
                new Vector2(leafSize.x * 0.26f, leafSize.y * 0.22f),
                color,
                sortingOrder,
                new Vector3(leafSize.x * 0.32f, -leafSize.y * 0.18f, 0f),
                18f,
                ArtLibrary.Leaf);
            AddFlatPicture(
                leaf,
                leaf.name + "_EarRTip",
                new Vector2(leafSize.x * 0.14f, leafSize.y * 0.10f),
                color,
                sortingOrder,
                new Vector3(leafSize.x * 0.42f, -leafSize.y * 0.24f, 0f),
                24f,
                ArtLibrary.Leaf);
        }
        AddFlatPicture(
            leaf,
            leaf.name + "_Base",
            new Vector2(leafSize.x * 0.36f, leafSize.y * 0.36f),
            color,
            sortingOrder,
            // (0.04, −0.12) sat in the ovate bite (apple Core analog).
            // 0.44 at −0.16 still reached +0.06 after Waist left
            // (Needle1 analog). Width 0.36 kisses x=0 from the left
            // against CheekR. ToothL stays the outer left serration.
            // leafSize stays.
            new Vector3(-leafSize.x * 0.18f, -leafSize.y * 0.12f, 0f),
            -10f,
            ArtLibrary.Leaf);
        AddFlatPicture(
            leaf,
            leaf.name + "_BaseTip",
            new Vector2(leafSize.x * 0.18f, leafSize.y * 0.14f),
            color,
            sortingOrder,
            // WaistTip closed the ovate midrib. Base without a lip
            // stayed an egg card after KiteL2Tip (Cap before CapTip).
            // Hang off Base; orchard bite stays empty. leafSize stays.
            new Vector3(-leafSize.x * 0.28f, -leafSize.y * 0.24f, 0f),
            -16f,
            ArtLibrary.Leaf);
        if (!dense)
        {
            // x=0.02 / −0.46 sat IN the bite from below after Base
            // left for ToothL (oak Wrist analog). Stalk rides Base
            // after that pad moved in to kiss. ToothL/R keep the
            // orchard notch empty. leafSize stays.
            AddFlatPicture(
                leaf,
                leaf.name + "_Petiole",
                new Vector2(leafSize.x * 0.16f, leafSize.y * 0.36f),
                color,
                sortingOrder,
                new Vector3(-leafSize.x * 0.18f, -leafSize.y * 0.36f, 0f),
                8f,
                ArtLibrary.Leaf);
            AddFlatPicture(
                leaf,
                leaf.name + "_PetioleTip",
                new Vector2(leafSize.x * 0.10f, leafSize.y * 0.12f),
                color,
                sortingOrder,
                // WristTip closed the palmate stalk. Ovate Petiole
                // without a lip stayed a stick after LobeL0Tip (CarmB
                // before CarmBTip). Hang off Base; bite stays empty.
                // leafSize stays.
                new Vector3(-leafSize.x * 0.18f, -leafSize.y * 0.50f, 0f),
                12f,
                ArtLibrary.Leaf);
            AddFlatPicture(
                leaf,
                leaf.name + "_PetioleR",
                new Vector2(leafSize.x * 0.14f, leafSize.y * 0.28f),
                CrownPatch(color, true),
                sortingOrder,
                // Left-only Petiole was a one-tent ovate stalk after
                // TipR/CheekR (oak Wrist before WristR). Far join
                // rides CheekR; orchard bite stays empty. leafSize stays.
                new Vector3(leafSize.x * 0.18f, -leafSize.y * 0.32f, 0f),
                -8f,
                ArtLibrary.Leaf);
            AddFlatPicture(
                leaf,
                leaf.name + "_PetioleRTip",
                new Vector2(leafSize.x * 0.08f, leafSize.y * 0.10f),
                CrownPatch(color, true),
                sortingOrder,
                // PetioleTip closed the near stalk. Far PetioleR
                // without a lip stayed a stick after WaistR (CarmB
                // before CarmBTip). Hang off CheekR; bite stays empty.
                // leafSize stays.
                new Vector3(leafSize.x * 0.18f, -leafSize.y * 0.44f, 0f),
                -12f,
                ArtLibrary.Leaf);
        }
        if (ResolvedKind == TreeKind.Apple && !dense)
        {
            // 1.02-wide Base was a paddle (willow-card analog). Two
            // serrations read as orchard, not a green disc. leafSize stays.
            AddFlatPicture(
                leaf,
                leaf.name + "_ToothL",
                new Vector2(leafSize.x * 0.28f, leafSize.y * 0.18f),
                color,
                sortingOrder,
                new Vector3(-leafSize.x * 0.32f, -leafSize.y * 0.06f, 0f),
                -22f,
                ArtLibrary.Leaf);
            AddFlatPicture(
                leaf,
                leaf.name + "_ToothLTip",
                new Vector2(leafSize.x * 0.14f, leafSize.y * 0.10f),
                color,
                sortingOrder,
                new Vector3(-leafSize.x * 0.42f, -leafSize.y * 0.02f, 0f),
                -28f,
                ArtLibrary.Leaf);
            AddFlatPicture(
                leaf,
                leaf.name + "_ToothR",
                new Vector2(leafSize.x * 0.24f, leafSize.y * 0.16f),
                OddLeaflet(color),
                sortingOrder,
                new Vector3(leafSize.x * 0.30f, -leafSize.y * 0.04f, 0f),
                18f,
                ArtLibrary.Leaf);
            AddFlatPicture(
                leaf,
                leaf.name + "_ToothRTip",
                new Vector2(leafSize.x * 0.12f, leafSize.y * 0.08f),
                OddLeaflet(color),
                sortingOrder,
                new Vector3(leafSize.x * 0.40f, 0f, 0f),
                24f,
                ArtLibrary.Leaf);
        }
        if (!dense)
        {
            AddFlatPicture(
                leaf,
                leaf.name + "_Waist",
                new Vector2(leafSize.x * 0.28f, leafSize.y * 0.24f),
                CrownPatch(color, true),
                sortingOrder,
                // 0.52 at x=0.06 was a sausage on the ovate (poplar
                // crown Waist 0.50 analog). Sit on Base after that pad
                // moved in to kiss CheekR. leafSize stays.
                new Vector3(-leafSize.x * 0.18f, leafSize.y * 0.08f, 0f),
                4f,
                ArtLibrary.Leaf);
            AddFlatPicture(
                leaf,
                leaf.name + "_WaistTip",
                new Vector2(leafSize.x * 0.14f, leafSize.y * 0.10f),
                CrownPatch(color, true),
                sortingOrder,
                // KiteL2Tip closed the hem. Ovate Waist without a lip
                // stayed a midrib card after PetioleTip (Cap before
                // CapTip). Hang off Waist; orchard bite stays empty.
                // leafSize stays.
                new Vector3(-leafSize.x * 0.26f, leafSize.y * 0.16f, 0f),
                10f,
                ArtLibrary.Leaf);
            AddFlatPicture(
                leaf,
                leaf.name + "_WaistR",
                new Vector2(leafSize.x * 0.24f, leafSize.y * 0.20f),
                CrownPatch(color, true),
                sortingOrder,
                // Left-only Waist was a one-tent ovate midrib after
                // PetioleR/TipR (poplar kite Waist before WaistR).
                // Far chip rides CheekR; orchard bite stays empty.
                // Apple 96 skips extra GO. leafSize stays.
                new Vector3(leafSize.x * 0.18f, leafSize.y * 0.06f, 0f),
                -4f,
                ArtLibrary.Leaf);
            AddFlatPicture(
                leaf,
                leaf.name + "_WaistRTip",
                new Vector2(leafSize.x * 0.12f, leafSize.y * 0.08f),
                CrownPatch(color, true),
                sortingOrder,
                new Vector3(leafSize.x * 0.26f, leafSize.y * 0.12f, 0f),
                -10f,
                ArtLibrary.Leaf);
        }
        AddFlatPicture(
            leaf,
            leaf.name + "_Tip",
            new Vector2(leafSize.x * 0.36f, leafSize.y * 0.40f),
            color,
            sortingOrder,
            // (0.08, 0.30) sat between ToothL/R (oak Dome analog).
            // Width 0.36 kisses x=0; TipR is the far peak (poplar
            // leaflet TipR analog). Orchard bite stays empty.
            // leafSize stays.
            new Vector3(-leafSize.x * 0.18f, leafSize.y * 0.26f, 0f),
            -8f,
            ArtLibrary.Leaf);
        AddFlatPicture(
            leaf,
            leaf.name + "_TipTip",
            new Vector2(leafSize.x * 0.18f, leafSize.y * 0.16f),
            color,
            sortingOrder,
            // Bush TipTip closed the hedge peak. Ovate Tip without a
            // lip stayed a cup paddle after CheekRTip (Cap before
            // CapTip). Hang off Tip; orchard bite stays empty.
            // leafSize stays.
            new Vector3(-leafSize.x * 0.24f, leafSize.y * 0.38f, 0f),
            -14f,
            ArtLibrary.Leaf);
        if (!dense)
        {
            AddFlatPicture(
                leaf,
                leaf.name + "_TipR",
                new Vector2(leafSize.x * 0.32f, leafSize.y * 0.34f),
                CrownPatch(color, true),
                sortingOrder,
                // Left-only Tip was a one-tent ovate after CheekR
                // (packed poplar TipR; apple/plum/hazel skipped).
                // Left edge 0 against Tip. Apple 96 skips extra GO.
                // leafSize stays.
                new Vector3(leafSize.x * 0.16f, leafSize.y * 0.24f, 0f),
                8f,
                ArtLibrary.Leaf);
            AddFlatPicture(
                leaf,
                leaf.name + "_TipRTip",
                new Vector2(leafSize.x * 0.16f, leafSize.y * 0.14f),
                CrownPatch(color, true),
                sortingOrder,
                // TipTip closed the near ovate peak. Leaflet TipR
                // without a lip stayed a far coin after CheekRTip
                // (Drop before DropTip). Hang off TipR; orchard bite
                // stays empty. Apple 96 skips extra GO. leafSize stays.
                new Vector3(leafSize.x * 0.22f, leafSize.y * 0.34f, 0f),
                14f,
                ArtLibrary.Leaf);
            // Apple ToothR / hazel EarR were a stub on the far pad, so
            // Base/Waist/Tip at −0.22 stayed a left comma (bush Blade
            // before CheekR). Far egg half kisses x=0; teeth stay the
            // outer serration. Bite / petiole stay on Base. leafSize stays.
            AddFlatPicture(
                leaf,
                leaf.name + "_CheekR",
                new Vector2(leafSize.x * 0.36f, leafSize.y * 0.32f),
                CrownPatch(color, true),
                sortingOrder,
                // Width 0.36; left edge 0 against Base (pine BodyR /
                // bush CheekR analog). Far egg, not a midrib chip.
                // leafSize stays.
                new Vector3(leafSize.x * 0.18f, leafSize.y * 0.06f, 0f),
                10f,
                ArtLibrary.Leaf);
            AddFlatPicture(
                leaf,
                leaf.name + "_CheekRTip",
                new Vector2(leafSize.x * 0.18f, leafSize.y * 0.14f),
                CrownPatch(color, true),
                sortingOrder,
                // BaseTip closed the near egg. Far CheekR without a
                // lip stayed a coin after WaistTip (Drop before
                // DropTip). Hang off CheekR; bite stays empty.
                // leafSize stays.
                new Vector3(leafSize.x * 0.30f, -leafSize.y * 0.04f, 0f),
                16f,
                ArtLibrary.Leaf);
        }
        if (ResolvedKind == TreeKind.Plum)
        {
            AddFlatPicture(
                leaf,
                leaf.name + "_Notch",
                new Vector2(leafSize.x * 0.22f, leafSize.y * 0.28f),
                color,
                sortingOrder,
                // −0.14 sat on the ovate spine after Tip left for
                // Waist (packed LobeTip analog). Peak rides Tip after
                // Base moved in to kiss. Orchard bite stays empty.
                // leafSize stays.
                new Vector3(-leafSize.x * 0.18f, leafSize.y * 0.42f, 0f),
                -8f,
                ArtLibrary.Leaf);
            AddFlatPicture(
                leaf,
                leaf.name + "_NotchTip",
                new Vector2(leafSize.x * 0.12f, leafSize.y * 0.12f),
                color,
                sortingOrder,
                // ApexTip closed the flame. Plum Notch without a lip
                // stayed a blunt ovate peak (Cap before CapTip). Hang
                // off Tip; bite stays empty. leafSize stays.
                new Vector3(-leafSize.x * 0.22f, leafSize.y * 0.54f, 0f),
                -12f,
                ArtLibrary.Leaf);
            if (!dense)
            {
                AddFlatPicture(
                    leaf,
                    leaf.name + "_NotchR",
                    new Vector2(leafSize.x * 0.18f, leafSize.y * 0.22f),
                    CrownPatch(color, true),
                    sortingOrder,
                    // Left-only Notch was a one-tent plum after TipR/
                    // CheekR (oak LobeTip before LobeTipR). Far peak
                    // rides CheekR; bite stays empty. leafSize stays.
                    new Vector3(leafSize.x * 0.18f, leafSize.y * 0.38f, 0f),
                    8f,
                    ArtLibrary.Leaf);
                AddFlatPicture(
                    leaf,
                    leaf.name + "_NotchRTip",
                    new Vector2(leafSize.x * 0.10f, leafSize.y * 0.10f),
                    CrownPatch(color, true),
                    sortingOrder,
                    // NotchTip closed the near peak. Far NotchR without
                    // a lip stayed a blunt ovate after CheekR (Cap
                    // before CapTip). Hang off CheekR; bite stays empty.
                    // leafSize stays.
                    new Vector3(leafSize.x * 0.22f, leafSize.y * 0.50f, 0f),
                    12f,
                    ArtLibrary.Leaf);
            }
        }
    }

    private void AttachCrownLobes(
        Transform mass,
        Color color,
        int sortingOrder,
        float massWidth = -1f,
        int slot = 0,
        float woodHeading = 0f)
    {
        if (mass == null)
            return;
        // Bark bake on the summer extras left a grey leaf-cloud
        // (willow-paddle analog). Winter deciduous is a twig spray.
        // Pine / cedar keep growLeaves, so they never hit this gate.
        if (winterWoodyCrown)
        {
            PaintWinterTwigCrown(mass, color, sortingOrder);
            return;
        }
        if (crownStackOnLeader)
        {
            if (ResolvedKind == TreeKind.Cedar)
            {
                PaintCedarCrownFans(mass, color, sortingOrder, massWidth);
                return;
            }
            PaintPineCrownCone(mass, color, sortingOrder, massWidth);
            return;
        }
        if (leafHangWorldDown)
        {
            PaintWillowCrownCurtain(mass, color, sortingOrder);
            return;
        }
        if (crownLobes < 2)
        {
            PaintPoplarCrownFlame(mass, color, sortingOrder);
            return;
        }
        if (ResolvedKind == TreeKind.Oak)
        {
            PaintOakCrownCloud(mass, color, sortingOrder, slot, woodHeading);
            return;
        }
        if (ResolvedKind == TreeKind.Apple || ResolvedKind == TreeKind.Plum)
        {
            PaintOrchardCrownCloud(mass, color, sortingOrder);
            return;
        }
        if (ResolvedKind == TreeKind.Hazel)
        {
            PaintHazelCrownTufts(mass, color, sortingOrder);
            return;
        }
        PaintBushCrownHedge(mass, color, sortingOrder);
    }

    // Leafless deciduous at 1x: a twig spray, not the summer leaf
    // extras recolored. Bark bake on palmate / sickle / kite left a
    // grey cloud (willow-paddle analog). Thin sticks, kind pose.
    // crownSize stays. Extra boxes ride the mass sway cache.
    private void PaintWinterTwigCrown(Transform mass, Color color, int sortingOrder)
    {
        if (mass == null)
            return;
        bool dense = maxSegments > 31;
        bool weep = leafHangWorldDown;
        bool flame = ResolvedKind == TreeKind.Poplar;
        bool coppice = ResolvedKind == TreeKind.Bush || ResolvedKind == TreeKind.Hazel;
        bool orchard = ResolvedKind == TreeKind.Apple || ResolvedKind == TreeKind.Plum;
        int n = dense ? 3 : (coppice ? 4 : 5);
        for (int i = 0; i < n; i++)
        {
            float u = n == 1 ? 0f : (i / (float)(n - 1) - 0.5f) * 2f;
            float twigW;
            float twigH;
            float x;
            float y;
            float rot;
            if (weep)
            {
                twigW = crownSize.x * (i == 1 ? 0.14f : 0.10f);
                twigH = crownSize.y * (0.52f + 0.10f * (i % 2));
                x = u * crownSize.x * 0.50f;
                y = -crownSize.y * (0.08f + 0.10f * (i % 2));
                rot = u * 14f + (i == 2 ? -8f : 10f);
            }
            else if (flame)
            {
                twigW = crownSize.x * (i == 1 ? 0.16f : 0.11f);
                twigH = crownSize.y * (0.38f + 0.12f * (1f - Mathf.Abs(u)));
                x = u * crownSize.x * 0.28f;
                y = crownSize.y * (0.06f + 0.08f * (1f - Mathf.Abs(u)));
                rot = u * 12f;
            }
            else if (coppice)
            {
                twigW = crownSize.x * (i == 1 ? 0.18f : 0.14f);
                twigH = crownSize.y * (0.36f + 0.08f * (i % 2));
                x = u * crownSize.x * 0.34f;
                y = crownSize.y * (0.02f - 0.06f * Mathf.Abs(u));
                rot = u * 16f;
            }
            else if (orchard)
            {
                twigW = crownSize.x * (i == 2 ? 0.16f : 0.12f);
                twigH = crownSize.y * (0.32f + 0.08f * (i % 2));
                x = u * crownSize.x * 0.36f;
                y = crownSize.y * (0.04f - 0.10f * Mathf.Abs(u));
                rot = u * 18f + (ResolvedKind == TreeKind.Plum ? 4f : 0f);
            }
            else
            {
                // Oak: spreading forks, not a palmate Finger cloud.
                // Centre-high y (1 − |u|) was a flag on the pole
                // (visual TwigF analog). Outer twigs sit higher.
                // crownSize stays.
                twigW = crownSize.x * (i == 2 ? 0.14f : 0.10f);
                twigH = crownSize.y * (0.36f + 0.08f * (i % 2));
                x = u * crownSize.x * 0.50f;
                y = crownSize.y * (-0.04f + 0.10f * Mathf.Abs(u));
                rot = u * 28f + (i == 1 ? -10f : 8f);
            }
            if (Mathf.Abs(u) < 0.15f)
            {
                // Centre stick sat ON the leafless pole (TwigDome analog
                // after Dome left TwigF). Ride the left pad. crownSize stays.
                x = -crownSize.x * (flame ? 0.22f
                    : (weep ? 0.50f
                    : (coppice ? 0.26f
                    : (orchard ? 0.32f : 0.48f))));
            }
            AddFlatPicture(
                mass,
                mass.name + "_Twig" + i,
                new Vector2(twigW, twigH),
                (i & 1) == 1 ? OddLeaflet(color) : color,
                sortingOrder,
                new Vector3(x, y, 0f),
                rot,
                ArtLibrary.Bark);
            // Bare sticks without a distal chip stayed a comb of posts
            // after RootButtTip (bole Furrow before CorkTip). Hang off
            // the outer end; Apple 96 skips. crownSize stays.
            if (!dense)
            {
                float tipSign = x >= 0f ? 1f : -1f;
                AddFlatPicture(
                    mass,
                    mass.name + "_TwigTip" + i,
                    new Vector2(twigW * 0.62f, twigH * 0.20f),
                    (i & 1) == 1 ? OddLeaflet(color) : color,
                    sortingOrder,
                    new Vector3(
                        x + tipSign * twigW * 0.10f,
                        y + twigH * 0.36f,
                        0f),
                    rot + tipSign * 10f,
                    ArtLibrary.Bark);
            }
        }
        if (dense)
            return;
        // One short cross-twig so the spray is a fork, not parallel
        // hairs (pine-barcode analog). crownSize stays.
        AddFlatPicture(
            mass,
            mass.name + "_TwigFork",
            new Vector2(crownSize.x * 0.12f, crownSize.y * 0.28f),
            color,
            sortingOrder,
            // −0.10 × 0.12 still reached −0.04 on the leafless spine
            // (BarkSeam analog). Fork rides TwigF. crownSize stays.
            new Vector3(-crownSize.x * 0.22f, crownSize.y * 0.08f, 0f),
            weep ? 14f : -28f,
            ArtLibrary.Bark);
        AddFlatPicture(
            mass,
            mass.name + "_TwigForkR",
            new Vector2(crownSize.x * 0.10f, crownSize.y * 0.24f),
            OddLeaflet(color),
            sortingOrder,
            // One left fork was a comma on the leafless spray (bush
            // Blade before CheekR). Far stick at +fork, not a mast
            // at x=0. crownSize stays.
            new Vector3(crownSize.x * 0.22f, crownSize.y * 0.06f, 0f),
            weep ? -12f : 28f,
            ArtLibrary.Bark);
        // Fork sticks without a distal chip stayed two posts on the
        // leafless spray (Twig before TwigTip). Hang off the outer
        // end; crownSize stays.
        AddFlatPicture(
            mass,
            mass.name + "_TwigForkTip",
            new Vector2(crownSize.x * 0.07f, crownSize.y * 0.10f),
            color,
            sortingOrder,
            new Vector3(-crownSize.x * 0.28f, crownSize.y * 0.18f, 0f),
            weep ? 18f : -34f,
            ArtLibrary.Bark);
        AddFlatPicture(
            mass,
            mass.name + "_TwigForkRTip",
            new Vector2(crownSize.x * 0.06f, crownSize.y * 0.09f),
            OddLeaflet(color),
            sortingOrder,
            new Vector3(crownSize.x * 0.28f, crownSize.y * 0.16f, 0f),
            weep ? -16f : 34f,
            ArtLibrary.Bark);
        if (weep)
        {
            // Oak got TwigDomeR; willow returned after ForkR so the
            // leafless curtain stayed a one-tent peak (summer Hang5
            // without Hang6). Kiss at max-Y. crownSize stays.
            AddFlatPicture(
                mass,
                mass.name + "_TwigDome",
                new Vector2(crownSize.x * 0.20f, crownSize.y * 0.16f),
                color,
                sortingOrder,
                new Vector3(-crownSize.x * 0.10f, crownSize.y * 0.22f, 0f),
                8f,
                ArtLibrary.Bark);
            AddFlatPicture(
                mass,
                mass.name + "_TwigDomeTip",
                new Vector2(crownSize.x * 0.10f, crownSize.y * 0.08f),
                color,
                sortingOrder,
                // HangTip closed the summer sickle. Winter TwigDome
                // without a lip stayed a blunt leafless peak after
                // ApexTip (Cap before CapTip). Hang off the left
                // pad; kiss at max-Y. crownSize stays.
                new Vector3(-crownSize.x * 0.16f, crownSize.y * 0.32f, 0f),
                14f,
                ArtLibrary.Bark);
            AddFlatPicture(
                mass,
                mass.name + "_TwigDomeR",
                new Vector2(crownSize.x * 0.18f, crownSize.y * 0.14f),
                OddLeaflet(color),
                sortingOrder,
                new Vector3(crownSize.x * 0.09f, crownSize.y * 0.20f, 0f),
                -6f,
                ArtLibrary.Bark);
            AddFlatPicture(
                mass,
                mass.name + "_TwigDomeRTip",
                new Vector2(crownSize.x * 0.09f, crownSize.y * 0.07f),
                OddLeaflet(color),
                sortingOrder,
                new Vector3(crownSize.x * 0.16f, crownSize.y * 0.28f, 0f),
                -12f,
                ArtLibrary.Bark);
            // CapFill rides Cap under CapTip. Summer DomeFill closed
            // leafy peaks; winter weep TwigDome / TwigDomeR still had
            // no shoulder chip under TwigDomeTip — one-tent leafless
            // peak after DomeFill. Chip rides TwigDome under
            // TwigDomeTip; kiss at max-Y. fork 28 / taper 0.58 stay.
            AddFlatPicture(
                mass,
                mass.name + "_TwigDomeFill",
                new Vector2(crownSize.x * 0.10f, crownSize.y * 0.08f),
                color,
                sortingOrder,
                new Vector3(-crownSize.x * 0.12f, crownSize.y * 0.25f, 0f),
                10f,
                ArtLibrary.Bark);
            AddFlatPicture(
                mass,
                mass.name + "_TwigDomeFillTip",
                new Vector2(crownSize.x * 0.06f, crownSize.y * 0.05f),
                color,
                sortingOrder,
                new Vector3(-crownSize.x * 0.14f, crownSize.y * 0.28f, 0f),
                12f,
                ArtLibrary.Bark);
            AddFlatPicture(
                mass,
                mass.name + "_TwigDomeFillR",
                new Vector2(crownSize.x * 0.09f, crownSize.y * 0.07f),
                OddLeaflet(color),
                sortingOrder,
                new Vector3(crownSize.x * 0.11f, crownSize.y * 0.23f, 0f),
                -8f,
                ArtLibrary.Bark);
            AddFlatPicture(
                mass,
                mass.name + "_TwigDomeFillRTip",
                new Vector2(crownSize.x * 0.05f, crownSize.y * 0.04f),
                OddLeaflet(color),
                sortingOrder,
                new Vector3(crownSize.x * 0.13f, crownSize.y * 0.26f, 0f),
                -10f,
                ArtLibrary.Bark);
            return;
        }
        if (flame)
        {
            // Summer ApexR; winter flame skipped DomeR so the leafless
            // column was a one-tent peak. Kiss at max-Y. crownSize stays.
            AddFlatPicture(
                mass,
                mass.name + "_TwigDome",
                new Vector2(crownSize.x * 0.18f, crownSize.y * 0.16f),
                color,
                sortingOrder,
                new Vector3(-crownSize.x * 0.09f, crownSize.y * 0.28f, 0f),
                -10f,
                ArtLibrary.Bark);
            AddFlatPicture(
                mass,
                mass.name + "_TwigDomeTip",
                new Vector2(crownSize.x * 0.10f, crownSize.y * 0.08f),
                color,
                sortingOrder,
                new Vector3(-crownSize.x * 0.16f, crownSize.y * 0.38f, 0f),
                -16f,
                ArtLibrary.Bark);
            AddFlatPicture(
                mass,
                mass.name + "_TwigDomeR",
                new Vector2(crownSize.x * 0.16f, crownSize.y * 0.14f),
                OddLeaflet(color),
                sortingOrder,
                new Vector3(crownSize.x * 0.08f, crownSize.y * 0.26f, 0f),
                8f,
                ArtLibrary.Bark);
            AddFlatPicture(
                mass,
                mass.name + "_TwigDomeRTip",
                new Vector2(crownSize.x * 0.08f, crownSize.y * 0.07f),
                OddLeaflet(color),
                sortingOrder,
                new Vector3(crownSize.x * 0.15f, crownSize.y * 0.36f, 0f),
                14f,
                ArtLibrary.Bark);
            // CapFill rides Cap under CapTip. Summer ApexFill closed
            // flame tips; winter flame TwigDome still one-tent under
            // TwigDomeTip. Chip rides TwigDome under TwigDomeTip;
            // kiss at max-Y. fork 28 / taper 0.58 stay.
            AddFlatPicture(
                mass,
                mass.name + "_TwigDomeFill",
                new Vector2(crownSize.x * 0.09f, crownSize.y * 0.08f),
                color,
                sortingOrder,
                new Vector3(-crownSize.x * 0.11f, crownSize.y * 0.31f, 0f),
                -12f,
                ArtLibrary.Bark);
            AddFlatPicture(
                mass,
                mass.name + "_TwigDomeFillTip",
                new Vector2(crownSize.x * 0.06f, crownSize.y * 0.05f),
                color,
                sortingOrder,
                new Vector3(-crownSize.x * 0.13f, crownSize.y * 0.34f, 0f),
                -14f,
                ArtLibrary.Bark);
            AddFlatPicture(
                mass,
                mass.name + "_TwigDomeFillR",
                new Vector2(crownSize.x * 0.08f, crownSize.y * 0.07f),
                OddLeaflet(color),
                sortingOrder,
                new Vector3(crownSize.x * 0.10f, crownSize.y * 0.29f, 0f),
                10f,
                ArtLibrary.Bark);
            AddFlatPicture(
                mass,
                mass.name + "_TwigDomeFillRTip",
                new Vector2(crownSize.x * 0.05f, crownSize.y * 0.04f),
                OddLeaflet(color),
                sortingOrder,
                new Vector3(crownSize.x * 0.12f, crownSize.y * 0.32f, 0f),
                12f,
                ArtLibrary.Bark);
            return;
        }
        if (coppice)
        {
            // Summer Tuft2R; winter coppice skipped the far peak so
            // three trunks read as one tent. Far |X|, sinus empty.
            // crownSize stays.
            AddFlatPicture(
                mass,
                mass.name + "_TwigDome",
                new Vector2(crownSize.x * 0.16f, crownSize.y * 0.14f),
                color,
                sortingOrder,
                new Vector3(-crownSize.x * 0.32f, crownSize.y * 0.28f, 0f),
                -10f,
                ArtLibrary.Bark);
            AddFlatPicture(
                mass,
                mass.name + "_TwigDomeTip",
                new Vector2(crownSize.x * 0.08f, crownSize.y * 0.08f),
                color,
                sortingOrder,
                new Vector3(-crownSize.x * 0.42f, crownSize.y * 0.36f, 0f),
                -16f,
                ArtLibrary.Bark);
            AddFlatPicture(
                mass,
                mass.name + "_TwigDomeR",
                new Vector2(crownSize.x * 0.14f, crownSize.y * 0.12f),
                OddLeaflet(color),
                sortingOrder,
                new Vector3(crownSize.x * 0.28f, crownSize.y * 0.24f, 0f),
                10f,
                ArtLibrary.Bark);
            AddFlatPicture(
                mass,
                mass.name + "_TwigDomeRTip",
                new Vector2(crownSize.x * 0.08f, crownSize.y * 0.07f),
                OddLeaflet(color),
                sortingOrder,
                new Vector3(crownSize.x * 0.38f, crownSize.y * 0.32f, 0f),
                16f,
                ArtLibrary.Bark);
            // CapFill rides Cap under CapTip. Summer DomeFill closed
            // bush peaks; winter coppice TwigDome still one-tent under
            // TwigDomeTip. Chip rides TwigDome under TwigDomeTip;
            // far |X|, sinus empty. fork 28 / taper 0.58 stay.
            AddFlatPicture(
                mass,
                mass.name + "_TwigDomeFill",
                new Vector2(crownSize.x * 0.08f, crownSize.y * 0.07f),
                color,
                sortingOrder,
                new Vector3(-crownSize.x * 0.35f, crownSize.y * 0.30f, 0f),
                -12f,
                ArtLibrary.Bark);
            AddFlatPicture(
                mass,
                mass.name + "_TwigDomeFillTip",
                new Vector2(crownSize.x * 0.05f, crownSize.y * 0.05f),
                color,
                sortingOrder,
                new Vector3(-crownSize.x * 0.38f, crownSize.y * 0.33f, 0f),
                -14f,
                ArtLibrary.Bark);
            AddFlatPicture(
                mass,
                mass.name + "_TwigDomeFillR",
                new Vector2(crownSize.x * 0.07f, crownSize.y * 0.06f),
                OddLeaflet(color),
                sortingOrder,
                new Vector3(crownSize.x * 0.31f, crownSize.y * 0.26f, 0f),
                12f,
                ArtLibrary.Bark);
            AddFlatPicture(
                mass,
                mass.name + "_TwigDomeFillRTip",
                new Vector2(crownSize.x * 0.05f, crownSize.y * 0.04f),
                OddLeaflet(color),
                sortingOrder,
                new Vector3(crownSize.x * 0.34f, crownSize.y * 0.29f, 0f),
                14f,
                ArtLibrary.Bark);
            return;
        }
        if (orchard)
        {
            // Summer SkirtR; winter orchard skipped caudal far so the
            // leafless cloud was a left comma. Far |X|; bite empty.
            // crownSize stays.
            AddFlatPicture(
                mass,
                mass.name + "_TwigDome",
                new Vector2(crownSize.x * 0.14f, crownSize.y * 0.12f),
                color,
                sortingOrder,
                new Vector3(-crownSize.x * 0.32f, -crownSize.y * 0.18f, 0f),
                -8f,
                ArtLibrary.Bark);
            AddFlatPicture(
                mass,
                mass.name + "_TwigDomeTip",
                new Vector2(crownSize.x * 0.08f, crownSize.y * 0.06f),
                color,
                sortingOrder,
                new Vector3(-crownSize.x * 0.42f, -crownSize.y * 0.26f, 0f),
                -14f,
                ArtLibrary.Bark);
            AddFlatPicture(
                mass,
                mass.name + "_TwigDomeR",
                new Vector2(crownSize.x * 0.12f, crownSize.y * 0.10f),
                OddLeaflet(color),
                sortingOrder,
                new Vector3(crownSize.x * 0.34f, -crownSize.y * 0.22f, 0f),
                8f,
                ArtLibrary.Bark);
            AddFlatPicture(
                mass,
                mass.name + "_TwigDomeRTip",
                new Vector2(crownSize.x * 0.06f, crownSize.y * 0.05f),
                OddLeaflet(color),
                sortingOrder,
                new Vector3(crownSize.x * 0.44f, -crownSize.y * 0.28f, 0f),
                14f,
                ArtLibrary.Bark);
            // CapFill rides Cap under CapTip. Summer DomeFill closed
            // orchard peaks; winter orchard TwigDome still one-tent
            // under TwigDomeTip. Chip rides TwigDome under
            // TwigDomeTip; far |X|, bite empty. fork 28 / taper 0.58 stay.
            AddFlatPicture(
                mass,
                mass.name + "_TwigDomeFill",
                new Vector2(crownSize.x * 0.07f, crownSize.y * 0.06f),
                color,
                sortingOrder,
                new Vector3(-crownSize.x * 0.35f, -crownSize.y * 0.20f, 0f),
                -10f,
                ArtLibrary.Bark);
            AddFlatPicture(
                mass,
                mass.name + "_TwigDomeFillTip",
                new Vector2(crownSize.x * 0.05f, crownSize.y * 0.04f),
                color,
                sortingOrder,
                new Vector3(-crownSize.x * 0.38f, -crownSize.y * 0.23f, 0f),
                -12f,
                ArtLibrary.Bark);
            AddFlatPicture(
                mass,
                mass.name + "_TwigDomeFillR",
                new Vector2(crownSize.x * 0.06f, crownSize.y * 0.05f),
                OddLeaflet(color),
                sortingOrder,
                new Vector3(crownSize.x * 0.37f, -crownSize.y * 0.24f, 0f),
                10f,
                ArtLibrary.Bark);
            AddFlatPicture(
                mass,
                mass.name + "_TwigDomeFillRTip",
                new Vector2(crownSize.x * 0.04f, crownSize.y * 0.04f),
                OddLeaflet(color),
                sortingOrder,
                new Vector3(crownSize.x * 0.40f, -crownSize.y * 0.26f, 0f),
                12f,
                ArtLibrary.Bark);
            return;
        }
        // Visual 11:00: TwigF sat at max-Y (flag on the leader).
        // Dome at max-Y; next stick on caudal max |X|. crownSize stays.
        AddFlatPicture(
            mass,
            mass.name + "_TwigDome",
            new Vector2(crownSize.x * 0.16f, crownSize.y * 0.14f),
            color,
            sortingOrder,
            // (−0.02, 0.36) was a flag on the leafless leader (cedar
            // FanDome analog). Peak rides TwigF at max |X| — not a
            // kiss at x=0 (that rebuilt the flag). crownSize stays.
            new Vector3(-crownSize.x * 0.48f, crownSize.y * 0.28f, 0f),
            -20f,
            ArtLibrary.Bark);
        AddFlatPicture(
            mass,
            mass.name + "_TwigDomeTip",
            new Vector2(crownSize.x * 0.08f, crownSize.y * 0.07f),
            color,
            sortingOrder,
            // HangTip closed the summer curtain. Winter oak Dome
            // without a lip stayed a blunt leafless flag after
            // ApexTip (Cap before CapTip). Hang off TwigF at max
            // |X|; not a kiss at x=0. crownSize stays.
            new Vector3(-crownSize.x * 0.58f, crownSize.y * 0.36f, 0f),
            -26f,
            ArtLibrary.Bark);
        AddFlatPicture(
            mass,
            mass.name + "_TwigDomeR",
            new Vector2(crownSize.x * 0.14f, crownSize.y * 0.12f),
            OddLeaflet(color),
            sortingOrder,
            // Left-only Dome/F was a one-tent winter oak (cedar
            // FanDome without FanDomeR). Far peak at +max |X|, not
            // on the leader. crownSize stays.
            new Vector3(crownSize.x * 0.48f, crownSize.y * 0.26f, 0f),
            20f,
            ArtLibrary.Bark);
        AddFlatPicture(
            mass,
            mass.name + "_TwigDomeRTip",
            new Vector2(crownSize.x * 0.07f, crownSize.y * 0.06f),
            OddLeaflet(color),
            sortingOrder,
            new Vector3(crownSize.x * 0.58f, crownSize.y * 0.34f, 0f),
            26f,
            ArtLibrary.Bark);
        // CapFill rides Cap under CapTip. Summer DomeFill closed oak
        // leafy peaks; winter oak TwigDome / TwigDomeR still had no
        // shoulder chip under TwigDomeTip — one-tent leafless flag
        // after DomeFill. Chip rides TwigDome under TwigDomeTip at
        // max |X|; not a kiss at x=0. fork 28 / taper 0.58 stay.
        AddFlatPicture(
            mass,
            mass.name + "_TwigDomeFill",
            new Vector2(crownSize.x * 0.08f, crownSize.y * 0.07f),
            color,
            sortingOrder,
            new Vector3(-crownSize.x * 0.51f, crownSize.y * 0.30f, 0f),
            -22f,
            ArtLibrary.Bark);
        AddFlatPicture(
            mass,
            mass.name + "_TwigDomeFillTip",
            new Vector2(crownSize.x * 0.05f, crownSize.y * 0.05f),
            color,
            sortingOrder,
            new Vector3(-crownSize.x * 0.54f, crownSize.y * 0.33f, 0f),
            -24f,
            ArtLibrary.Bark);
        AddFlatPicture(
            mass,
            mass.name + "_TwigDomeFillR",
            new Vector2(crownSize.x * 0.07f, crownSize.y * 0.06f),
            OddLeaflet(color),
            sortingOrder,
            new Vector3(crownSize.x * 0.51f, crownSize.y * 0.28f, 0f),
            22f,
            ArtLibrary.Bark);
        AddFlatPicture(
            mass,
            mass.name + "_TwigDomeFillRTip",
            new Vector2(crownSize.x * 0.04f, crownSize.y * 0.04f),
            OddLeaflet(color),
            sortingOrder,
            new Vector3(crownSize.x * 0.54f, crownSize.y * 0.31f, 0f),
            24f,
            ArtLibrary.Bark);
        AddFlatPicture(
            mass,
            mass.name + "_TwigF",
            new Vector2(crownSize.x * 0.18f, crownSize.y * 0.22f),
            color,
            sortingOrder,
            new Vector3(-crownSize.x * 0.48f, crownSize.y * 0.04f, 0f),
            -20f,
            ArtLibrary.Bark);
        AddFlatPicture(
            mass,
            mass.name + "_TwigFTip",
            new Vector2(crownSize.x * 0.10f, crownSize.y * 0.08f),
            color,
            sortingOrder,
            new Vector3(-crownSize.x * 0.58f, crownSize.y * 0.14f, 0f),
            -26f,
            ArtLibrary.Bark);
        AddFlatPicture(
            mass,
            mass.name + "_TwigFR",
            new Vector2(crownSize.x * 0.16f, crownSize.y * 0.20f),
            OddLeaflet(color),
            sortingOrder,
            new Vector3(crownSize.x * 0.48f, crownSize.y * 0.02f, 0f),
            20f,
            ArtLibrary.Bark);
        AddFlatPicture(
            mass,
            mass.name + "_TwigFRTip",
            new Vector2(crownSize.x * 0.08f, crownSize.y * 0.07f),
            OddLeaflet(color),
            sortingOrder,
            new Vector3(crownSize.x * 0.58f, crownSize.y * 0.12f, 0f),
            26f,
            ArtLibrary.Bark);
    }

    // Bush at 1x: a low round hedge. Two mounds plus a 0.52 hem were a
    // sofa (cedar-slab analog). Overlapping discs, no table edge.
    // Same tint; crownSize stays.
    private void PaintBushCrownHedge(Transform mass, Color color, int sortingOrder)
    {
        if (mass == null)
            return;
        AddFlatPicture(
            mass,
            mass.name + "_Core",
            new Vector2(crownSize.x * 0.22f, crownSize.y * 0.28f),
            color,
            sortingOrder,
            // 0.34 at the origin stitched TuftL/R into a sofa (apple
            // Core-in-the-bite analog). Pad sits on TuftL. crownSize stays.
            new Vector3(-crownSize.x * 0.16f, -0.04f * crownSize.y, 0f),
            8f,
            CrownBake);
        AddFlatPicture(
            mass,
            mass.name + "_CoreTip",
            new Vector2(crownSize.x * 0.12f, crownSize.y * 0.12f),
            color,
            sortingOrder,
            // Orchard CoreTip closed the bitten basal. Hedge Core
            // without a lip stayed a blunt sofa pad after LobeLTip
            // (CarmB before CarmBTip). Hang off TuftL; sofa sinus
            // stays the tuft kiss. crownSize stays.
            new Vector3(-crownSize.x * 0.24f, -0.16f * crownSize.y, 0f),
            14f,
            CrownBake);
        AddFlatPicture(
            mass,
            mass.name + "_CoreR",
            new Vector2(crownSize.x * 0.18f, crownSize.y * 0.22f),
            CrownPatch(color, true),
            sortingOrder,
            // Left-only Core was a one-tent hedge stalk after
            // DomeR/FillR (oak Core before CoreR). Far basal rides
            // TuftR; the sofa sinus stays the tuft kiss. crownSize stays.
            new Vector3(crownSize.x * 0.14f, -0.02f * crownSize.y, 0f),
            -6f,
            CrownBake);
        AddFlatPicture(
            mass,
            mass.name + "_CoreRTip",
            new Vector2(crownSize.x * 0.10f, crownSize.y * 0.10f),
            CrownPatch(color, true),
            sortingOrder,
            new Vector3(crownSize.x * 0.22f, -0.12f * crownSize.y, 0f),
            -12f,
            CrownBake);
        // CapFill rides Cap under CapTip. TwigDomeFill closed winter
        // peaks; bush Core / CoreR still had Tip but no shoulder chip
        // under CoreTip — one-tent sofa stalk after TwigDomeFill.
        // Chip rides Core under CoreTip on TuftL; sofa sinus stays
        // the tuft kiss. fork 28 / taper 0.58 stay.
        AddFlatPicture(
            mass,
            mass.name + "_CoreFill",
            new Vector2(crownSize.x * 0.10f, crownSize.y * 0.12f),
            color,
            sortingOrder,
            // After TwigDomeFill under TwigDomeTip: hedge Core had no
            // CapFill-language chip (CapFill −0.12 slit analog on the
            // sofa stalk). Chip rides Core under CoreTip; right edge
            // toward CoreTip left. Sofa sinus stays the tufts.
            // crownSize stays.
            new Vector3(-crownSize.x * 0.18f, -0.08f * crownSize.y, 0f),
            6f,
            CrownBake);
        AddFlatPicture(
            mass,
            mass.name + "_CoreFillTip",
            new Vector2(crownSize.x * 0.06f, crownSize.y * 0.06f),
            color,
            sortingOrder,
            // CoreTip closed the sofa basal. CoreFill without a lip
            // stayed a stalk mast chip (CapFill before CapFillTip).
            // Hang off Core toward CoreTip; right edge kisses
            // CoreTip left. Sofa sinus stays the tufts. crownSize stays.
            new Vector3(-crownSize.x * 0.20f, -0.12f * crownSize.y, 0f),
            10f,
            CrownBake);
        AddFlatPicture(
            mass,
            mass.name + "_CoreFillR",
            new Vector2(crownSize.x * 0.10f, crownSize.y * 0.10f),
            CrownPatch(color, true),
            sortingOrder,
            // On CoreR under CoreRTip (not the sofa sinus).
            // crownSize stays.
            new Vector3(crownSize.x * 0.16f, -0.06f * crownSize.y, 0f),
            -4f,
            CrownBake);
        AddFlatPicture(
            mass,
            mass.name + "_CoreFillRTip",
            new Vector2(crownSize.x * 0.06f, crownSize.y * 0.05f),
            CrownPatch(color, true),
            sortingOrder,
            // Left edge kisses CoreRTip right. Sofa sinus stays the
            // tufts. crownSize stays.
            new Vector3(crownSize.x * 0.18f, -0.10f * crownSize.y, 0f),
            -8f,
            CrownBake);
        AddFlatPicture(
            mass,
            mass.name + "_TuftL",
            new Vector2(crownSize.x * 0.46f, crownSize.y * 0.32f),
            color,
            sortingOrder,
            // −0.30 right −0.07 / TuftR left +0.14 left a 0.21 sky
            // sofa (pine Body slit analog). A hedge is overlapping
            // discs, not two tents. TuftL right 0. crownSize stays.
            new Vector3(-crownSize.x * 0.23f, -crownSize.y * 0.02f, 0f),
            -16f,
            CrownBake);
        AddFlatPicture(
            mass,
            mass.name + "_TuftLTip",
            new Vector2(crownSize.x * 0.22f, crownSize.y * 0.14f),
            color,
            sortingOrder,
            // Orchard LobeLTip closed the cloud pad. Hedge TuftL
            // without a lip stayed a blunt mound after CoreTip
            // (Cap before CapTip). Hang off the left disc; kiss
            // at x=0 stays the tufts. crownSize stays.
            new Vector3(-crownSize.x * 0.38f, crownSize.y * 0.08f, 0f),
            -22f,
            CrownBake);
        AddFlatPicture(
            mass,
            mass.name + "_TuftR",
            new Vector2(crownSize.x * 0.28f, crownSize.y * 0.36f),
            CrownPatch(color, true),
            sortingOrder,
            // Left edge 0 against TuftL right 0 (pine BodyR analog).
            // Unequal far mound. crownSize stays.
            new Vector3(crownSize.x * 0.14f, crownSize.y * 0.04f, 0f),
            18f,
            CrownBake);
        AddFlatPicture(
            mass,
            mass.name + "_TuftRTip",
            new Vector2(crownSize.x * 0.14f, crownSize.y * 0.16f),
            CrownPatch(color, true),
            sortingOrder,
            new Vector3(crownSize.x * 0.24f, crownSize.y * 0.16f, 0f),
            24f,
            CrownBake);
        // CapFill rides Cap under CapTip. CoreFill closed sofa stalks;
        // bush TuftL / TuftR still had Tip but no shoulder chip under
        // TuftLTip — one-tent hedge mound after CoreFill. Chip rides
        // TuftL under TuftLTip; kiss at x=0 stays the tufts.
        // fork 28 / taper 0.58 stay.
        AddFlatPicture(
            mass,
            mass.name + "_TuftFill",
            new Vector2(crownSize.x * 0.16f, crownSize.y * 0.12f),
            color,
            sortingOrder,
            // After CoreFill under CoreTip: hedge TuftL had no CapFill-
            // language chip. Chip rides TuftL under TuftLTip; right
            // edge toward TuftLTip left. Sofa sinus stays the tufts.
            // crownSize stays.
            new Vector3(-crownSize.x * 0.28f, crownSize.y * 0.02f, 0f),
            -14f,
            CrownBake);
        AddFlatPicture(
            mass,
            mass.name + "_TuftFillTip",
            new Vector2(crownSize.x * 0.08f, crownSize.y * 0.06f),
            color,
            sortingOrder,
            // TuftLTip closed the mound. TuftFill without a lip stayed
            // a mound mast chip (CapFill before CapFillTip). Hang off
            // TuftL toward TuftLTip; right edge kisses TuftLTip left.
            // crownSize stays.
            new Vector3(-crownSize.x * 0.32f, crownSize.y * 0.06f, 0f),
            -18f,
            CrownBake);
        AddFlatPicture(
            mass,
            mass.name + "_TuftFillR",
            new Vector2(crownSize.x * 0.14f, crownSize.y * 0.12f),
            CrownPatch(color, true),
            sortingOrder,
            // On TuftR under TuftRTip (not the sofa sinus).
            // crownSize stays.
            new Vector3(crownSize.x * 0.18f, crownSize.y * 0.08f, 0f),
            16f,
            CrownBake);
        AddFlatPicture(
            mass,
            mass.name + "_TuftFillRTip",
            new Vector2(crownSize.x * 0.07f, crownSize.y * 0.05f),
            CrownPatch(color, true),
            sortingOrder,
            // Left edge kisses TuftRTip right. Sofa sinus stays the
            // tufts. crownSize stays.
            new Vector3(crownSize.x * 0.21f, crownSize.y * 0.12f, 0f),
            20f,
            CrownBake);
        AddFlatPicture(
            mass,
            mass.name + "_Dome",
            new Vector2(crownSize.x * 0.30f, crownSize.y * 0.28f),
            color,
            sortingOrder,
            // (0.02, 0.28) sat between the tufts (oak Dome 0.04 analog).
            // Peak rides TuftL after that pad moved in to kiss.
            // DomeR is the far peak. crownSize stays.
            new Vector3(-crownSize.x * 0.15f, crownSize.y * 0.28f, 0f),
            -8f,
            CrownBake);
        AddFlatPicture(
            mass,
            mass.name + "_DomeTip",
            new Vector2(crownSize.x * 0.16f, crownSize.y * 0.12f),
            color,
            sortingOrder,
            // Oak DomeTip closed the palmate peak. Hedge Dome
            // without a lip stayed a blunt hat after TuftLTip
            // (Cap before CapTip). Hang off TuftL; kiss at x=0
            // at max-Y. crownSize stays.
            new Vector3(-crownSize.x * 0.24f, crownSize.y * 0.40f, 0f),
            -14f,
            CrownBake);
        AddFlatPicture(
            mass,
            mass.name + "_DomeR",
            new Vector2(crownSize.x * 0.28f, crownSize.y * 0.26f),
            CrownPatch(color, true),
            sortingOrder,
            // Left-only Dome was a one-tent hedge after TuftL/R
            // kissed (cedar FanDome without FanDomeR). Far peak
            // rides TuftR; kiss at x=0 at max-Y. crownSize stays.
            new Vector3(crownSize.x * 0.14f, crownSize.y * 0.26f, 0f),
            8f,
            CrownBake);
        AddFlatPicture(
            mass,
            mass.name + "_DomeRTip",
            new Vector2(crownSize.x * 0.14f, crownSize.y * 0.10f),
            CrownPatch(color, true),
            sortingOrder,
            new Vector3(crownSize.x * 0.22f, crownSize.y * 0.36f, 0f),
            14f,
            CrownBake);
        // CapFill rides Cap under CapTip. Hedge Dome / DomeR kissed at
        // max-Y, but no shoulder chip under DomeTip left the bush peak
        // a one-tent after ApexFill closed fir/poplar. Chip rides Dome
        // under DomeTip; kiss at x=0 at max-Y stays. crownSize stays.
        AddFlatPicture(
            mass,
            mass.name + "_DomeFill",
            new Vector2(crownSize.x * 0.12f, crownSize.y * 0.14f),
            color,
            sortingOrder,
            // After ApexFill under ApexTip: hedge Dome had no CapFill-
            // language chip. Chip rides Dome under DomeTip; right edge
            // toward DomeTip left. crownSize stays.
            new Vector3(-crownSize.x * 0.18f, crownSize.y * 0.32f, 0f),
            -6f,
            CrownBake);
        AddFlatPicture(
            mass,
            mass.name + "_DomeFillTip",
            new Vector2(crownSize.x * 0.07f, crownSize.y * 0.07f),
            color,
            sortingOrder,
            // DomeTip closed the hedge peak. DomeFill without a lip
            // stayed a peak mast chip (CapFill before CapFillTip). Hang
            // off Dome toward DomeTip; right edge kisses DomeTip left.
            // crownSize stays.
            new Vector3(-crownSize.x * 0.20f, crownSize.y * 0.36f, 0f),
            -12f,
            CrownBake);
        AddFlatPicture(
            mass,
            mass.name + "_DomeFillR",
            new Vector2(crownSize.x * 0.12f, crownSize.y * 0.12f),
            CrownPatch(color, true),
            sortingOrder,
            // On DomeR under DomeRTip (not the mid peak). crownSize stays.
            new Vector3(crownSize.x * 0.16f, crownSize.y * 0.30f, 0f),
            5f,
            CrownBake);
        AddFlatPicture(
            mass,
            mass.name + "_DomeFillRTip",
            new Vector2(crownSize.x * 0.07f, crownSize.y * 0.06f),
            CrownPatch(color, true),
            sortingOrder,
            // Left edge kisses DomeRTip right. crownSize stays.
            new Vector3(crownSize.x * 0.18f, crownSize.y * 0.34f, 0f),
            11f,
            CrownBake);
        AddFlatPicture(
            mass,
            mass.name + "_Fill",
            new Vector2(crownSize.x * 0.14f, crownSize.y * 0.14f),
            CrownPatch(color, true),
            sortingOrder,
            new Vector3(-crownSize.x * 0.32f, -crownSize.y * 0.22f, 0f),
            -8f,
            CrownBake);
        AddFlatPicture(
            mass,
            mass.name + "_FillTip",
            new Vector2(crownSize.x * 0.08f, crownSize.y * 0.08f),
            CrownPatch(color, true),
            sortingOrder,
            new Vector3(-crownSize.x * 0.42f, -crownSize.y * 0.32f, 0f),
            -14f,
            CrownBake);
        // Visual 19:46 leftover: FillWaist 0.18×0.10 still a sofa
        // after FillTip. Shorter chip + lip further off TuftL; sofa
        // sinus stays the tuft kiss. crownSize stays.
        AddFlatPicture(
            mass,
            mass.name + "_FillWaist",
            new Vector2(crownSize.x * 0.12f, crownSize.y * 0.08f),
            CrownPatch(color, true),
            sortingOrder,
            new Vector3(-crownSize.x * 0.28f, -crownSize.y * 0.12f, 0f),
            8f,
            CrownBake);
        AddFlatPicture(
            mass,
            mass.name + "_FillWaistTip",
            new Vector2(crownSize.x * 0.08f, crownSize.y * 0.05f),
            CrownPatch(color, true),
            sortingOrder,
            new Vector3(-crownSize.x * 0.40f, -crownSize.y * 0.18f, 0f),
            14f,
            CrownBake);
        AddFlatPicture(
            mass,
            mass.name + "_FillR",
            new Vector2(crownSize.x * 0.12f, crownSize.y * 0.12f),
            CrownPatch(color, true),
            sortingOrder,
            new Vector3(crownSize.x * 0.20f, -crownSize.y * 0.18f, 0f),
            6f,
            CrownBake);
        AddFlatPicture(
            mass,
            mass.name + "_FillRTip",
            new Vector2(crownSize.x * 0.08f, crownSize.y * 0.06f),
            CrownPatch(color, true),
            sortingOrder,
            new Vector3(crownSize.x * 0.30f, -crownSize.y * 0.26f, 0f),
            12f,
            CrownBake);
    }

    // Hazel at 1x: coppice tufts on three trunks, not the bush sofa.
    // Smaller pads, slight X offset. Same tint; crownSize stays.
    private void PaintHazelCrownTufts(Transform mass, Color color, int sortingOrder)
    {
        if (mass == null)
            return;
        AddFlatPicture(
            mass,
            mass.name + "_Tuft0",
            new Vector2(crownSize.x * 0.38f, crownSize.y * 0.42f),
            color,
            sortingOrder,
            // 0.50 at −0.26 still reached −0.01 and filled the coppice
            // sinus (plum Core sausage analog). Pad rides left of Tuft1.
            // crownSize stays.
            new Vector3(-crownSize.x * 0.32f, 0.02f * crownSize.y, 0f),
            -12f,
            CrownBake);
        AddFlatPicture(
            mass,
            mass.name + "_Tuft0Tip",
            new Vector2(crownSize.x * 0.18f, crownSize.y * 0.16f),
            color,
            sortingOrder,
            // Hedge TuftLTip closed the mound. Coppice Tuft0
            // without a lip stayed a blunt trunk hat after CoreTip
            // (Cap before CapTip). Hang off Tuft0; sinus between
            // Tuft0/Tuft1 stays empty. crownSize stays.
            new Vector3(-crownSize.x * 0.46f, crownSize.y * 0.14f, 0f),
            -18f,
            CrownBake);
        AddFlatPicture(
            mass,
            mass.name + "_Tuft1",
            new Vector2(crownSize.x * 0.28f, crownSize.y * 0.38f),
            CrownPatch(color, true),
            sortingOrder,
            new Vector3(crownSize.x * 0.28f, 0.10f * crownSize.y, 0f),
            16f,
            CrownBake);
        AddFlatPicture(
            mass,
            mass.name + "_Tuft1Tip",
            new Vector2(crownSize.x * 0.14f, crownSize.y * 0.14f),
            CrownPatch(color, true),
            sortingOrder,
            new Vector3(crownSize.x * 0.40f, crownSize.y * 0.22f, 0f),
            22f,
            CrownBake);
        AddFlatPicture(
            mass,
            mass.name + "_Tuft2",
            new Vector2(crownSize.x * 0.22f, crownSize.y * 0.28f),
            color,
            sortingOrder,
            // −0.02 was a hat on the coppice spine (cedar FanL2 analog).
            // Peak sits above Tuft0, off the pole. crownSize stays.
            new Vector3(-crownSize.x * 0.36f, crownSize.y * 0.32f, 0f),
            -8f,
            CrownBake);
        AddFlatPicture(
            mass,
            mass.name + "_Tuft2Tip",
            new Vector2(crownSize.x * 0.12f, crownSize.y * 0.12f),
            color,
            sortingOrder,
            new Vector3(-crownSize.x * 0.46f, crownSize.y * 0.42f, 0f),
            -14f,
            CrownBake);
        AddFlatPicture(
            mass,
            mass.name + "_Tuft2R",
            new Vector2(crownSize.x * 0.18f, crownSize.y * 0.24f),
            CrownPatch(color, true),
            sortingOrder,
            // Left-only Tuft2 was a one-tent coppice (winter TwigDome
            // without DomeR). Far peak rides Tuft1; sinus between
            // Tuft0/Tuft1 stays empty (not a bush sofa). crownSize stays.
            new Vector3(crownSize.x * 0.32f, crownSize.y * 0.28f, 0f),
            10f,
            CrownBake);
        AddFlatPicture(
            mass,
            mass.name + "_Tuft2RTip",
            new Vector2(crownSize.x * 0.10f, crownSize.y * 0.10f),
            CrownPatch(color, true),
            sortingOrder,
            new Vector3(crownSize.x * 0.42f, crownSize.y * 0.36f, 0f),
            16f,
            CrownBake);
        // CapFill rides Cap under CapTip. CoreFill / bush TuftFill
        // closed mound stalks; hazel Tuft0/1/2 still had Tip but no
        // shoulder chip under Tuft0Tip — one-tent coppice hat after
        // CoreFill. Chip rides Tuft0 under Tuft0Tip; sinus between
        // Tuft0/Tuft1 stays empty. fork 28 / taper 0.58 stay.
        AddFlatPicture(
            mass,
            mass.name + "_TuftFill0",
            new Vector2(crownSize.x * 0.14f, crownSize.y * 0.12f),
            color,
            sortingOrder,
            new Vector3(-crownSize.x * 0.36f, crownSize.y * 0.06f, 0f),
            -10f,
            CrownBake);
        AddFlatPicture(
            mass,
            mass.name + "_TuftFill0Tip",
            new Vector2(crownSize.x * 0.07f, crownSize.y * 0.06f),
            color,
            sortingOrder,
            // Tuft0Tip closed the trunk hat. TuftFill0 without a lip
            // stayed a coppice mast chip (CapFill before CapFillTip).
            // Hang off Tuft0 toward Tuft0Tip. crownSize stays.
            new Vector3(-crownSize.x * 0.40f, crownSize.y * 0.10f, 0f),
            -14f,
            CrownBake);
        AddFlatPicture(
            mass,
            mass.name + "_TuftFill1",
            new Vector2(crownSize.x * 0.12f, crownSize.y * 0.10f),
            CrownPatch(color, true),
            sortingOrder,
            new Vector3(crownSize.x * 0.32f, crownSize.y * 0.14f, 0f),
            14f,
            CrownBake);
        AddFlatPicture(
            mass,
            mass.name + "_TuftFill1Tip",
            new Vector2(crownSize.x * 0.06f, crownSize.y * 0.05f),
            CrownPatch(color, true),
            sortingOrder,
            new Vector3(crownSize.x * 0.36f, crownSize.y * 0.18f, 0f),
            18f,
            CrownBake);
        AddFlatPicture(
            mass,
            mass.name + "_TuftFill2",
            new Vector2(crownSize.x * 0.10f, crownSize.y * 0.10f),
            color,
            sortingOrder,
            new Vector3(-crownSize.x * 0.40f, crownSize.y * 0.36f, 0f),
            -6f,
            CrownBake);
        AddFlatPicture(
            mass,
            mass.name + "_TuftFill2Tip",
            new Vector2(crownSize.x * 0.05f, crownSize.y * 0.05f),
            color,
            sortingOrder,
            new Vector3(-crownSize.x * 0.43f, crownSize.y * 0.39f, 0f),
            -10f,
            CrownBake);
        AddFlatPicture(
            mass,
            mass.name + "_TuftFill2R",
            new Vector2(crownSize.x * 0.09f, crownSize.y * 0.08f),
            CrownPatch(color, true),
            sortingOrder,
            new Vector3(crownSize.x * 0.36f, crownSize.y * 0.32f, 0f),
            8f,
            CrownBake);
        AddFlatPicture(
            mass,
            mass.name + "_TuftFill2RTip",
            new Vector2(crownSize.x * 0.05f, crownSize.y * 0.04f),
            CrownPatch(color, true),
            sortingOrder,
            new Vector3(crownSize.x * 0.39f, crownSize.y * 0.34f, 0f),
            12f,
            CrownBake);
        AddFlatPicture(
            mass,
            mass.name + "_Fill",
            new Vector2(crownSize.x * 0.14f, crownSize.y * 0.14f),
            color,
            sortingOrder,
            new Vector3(-crownSize.x * 0.34f, -crownSize.y * 0.24f, 0f),
            -8f,
            CrownBake);
        AddFlatPicture(
            mass,
            mass.name + "_FillTip",
            new Vector2(crownSize.x * 0.08f, crownSize.y * 0.08f),
            color,
            sortingOrder,
            new Vector3(-crownSize.x * 0.44f, -crownSize.y * 0.32f, 0f),
            -14f,
            CrownBake);
        AddFlatPicture(
            mass,
            mass.name + "_FillWaist",
            new Vector2(crownSize.x * 0.12f, crownSize.y * 0.08f),
            color,
            sortingOrder,
            new Vector3(-crownSize.x * 0.30f, -crownSize.y * 0.14f, 0f),
            8f,
            CrownBake);
        AddFlatPicture(
            mass,
            mass.name + "_FillWaistTip",
            new Vector2(crownSize.x * 0.07f, crownSize.y * 0.05f),
            color,
            sortingOrder,
            new Vector3(-crownSize.x * 0.42f, -crownSize.y * 0.20f, 0f),
            14f,
            CrownBake);
        AddFlatPicture(
            mass,
            mass.name + "_FillR",
            new Vector2(crownSize.x * 0.16f, crownSize.y * 0.14f),
            CrownPatch(color, true),
            sortingOrder,
            new Vector3(crownSize.x * 0.28f, -crownSize.y * 0.18f, 0f),
            8f,
            CrownBake);
        AddFlatPicture(
            mass,
            mass.name + "_FillRTip",
            new Vector2(crownSize.x * 0.08f, crownSize.y * 0.07f),
            CrownPatch(color, true),
            sortingOrder,
            new Vector3(crownSize.x * 0.36f, -crownSize.y * 0.26f, 0f),
            14f,
            CrownBake);
    }

    // Fir at 1x: a closed cone. Shared SlopeL/R at ±26° on the 84 cm
    // envelope were roof slats. 0.28×0.88 Spine at 0° was a green pole
    // (willow analog). Overlapping wedges plus BaseL/R — no 0° mast.
    // Same tint; crownSize stays.
    private void PaintPineCrownCone(Transform mass, Color color, int sortingOrder, float massWidth)
    {
        if (mass == null)
            return;
        float span = massWidth > 0.01f ? massWidth : crownSize.x;
        // SlopeL2/R2 at |x| 0.14/0.10 were a mast through the cone
        // (cedar FanM analog). Overlap the outer slopes. Whorls hang
        // off those pads, not the spine. crownSize stays.
        AddFlatPicture(
            mass,
            mass.name + "_SlopeL",
            new Vector2(span * 0.26f, crownSize.y * 0.38f),
            color,
            sortingOrder,
            new Vector3(-span * 0.28f, -crownSize.y * 0.08f, 0f),
            -10f,
            ArtLibrary.Leaf);
        AddFlatPicture(
            mass,
            mass.name + "_SlopeL2",
            new Vector2(span * 0.22f, crownSize.y * 0.32f),
            color,
            sortingOrder,
            new Vector3(-span * 0.36f, -crownSize.y * 0.04f, 0f),
            -8f,
            ArtLibrary.Leaf);
        AddFlatPicture(
            mass,
            mass.name + "_SlopeL2Tip",
            new Vector2(span * 0.12f, crownSize.y * 0.14f),
            color,
            sortingOrder,
            // SlopeLTip closed the outer roof slat. Inner SlopeL2
            // without a lip stayed a plank in the cone (Layer before
            // LayerTip). Hang off SlopeL2; crownSize stays.
            new Vector3(-span * 0.48f, -crownSize.y * 0.10f, 0f),
            -14f,
            ArtLibrary.Leaf);
            AddFlatPicture(
                mass,
                mass.name + "_SlopeR",
                new Vector2(span * 0.24f, crownSize.y * 0.34f),
                CrownPatch(color, true),
                sortingOrder,
            new Vector3(span * 0.22f, -crownSize.y * 0.10f, 0f),
            8f,
            ArtLibrary.Leaf);
            AddFlatPicture(
                mass,
                mass.name + "_SlopeR2",
                new Vector2(span * 0.20f, crownSize.y * 0.28f),
                CrownPatch(color, true),
                sortingOrder,
            new Vector3(span * 0.32f, -crownSize.y * 0.06f, 0f),
            9f,
            ArtLibrary.Leaf);
        AddFlatPicture(
            mass,
            mass.name + "_SlopeR2Tip",
            new Vector2(span * 0.10f, crownSize.y * 0.12f),
            CrownPatch(color, true),
            sortingOrder,
            new Vector3(span * 0.44f, -crownSize.y * 0.12f, 0f),
            15f,
            ArtLibrary.Leaf);
        // Slope wedges without a distal chip stayed roof slats on the
        // 84 cm envelope (bole Flake before FlakeTip). Lip hangs off
        // the outer pad; crownSize stays.
        AddFlatPicture(
            mass,
            mass.name + "_SlopeLTip",
            new Vector2(span * 0.14f, crownSize.y * 0.16f),
            color,
            sortingOrder,
            new Vector3(-span * 0.44f, -crownSize.y * 0.12f, 0f),
            -16f,
            ArtLibrary.Leaf);
        AddFlatPicture(
            mass,
            mass.name + "_SlopeRTip",
            new Vector2(span * 0.12f, crownSize.y * 0.14f),
            CrownPatch(color, true),
            sortingOrder,
            new Vector3(span * 0.40f, -crownSize.y * 0.14f, 0f),
            14f,
            ArtLibrary.Leaf);
        AddFlatPicture(
            mass,
            mass.name + "_LayerL",
            new Vector2(span * 0.26f, crownSize.y * 0.24f),
            color,
            sortingOrder,
            // 0.30 at −0.22 still reached −0.07 after BaseL left the
            // hem (BaseL analog). Mid wedges ride SlopeL / SlopeR.
            // crownSize stays.
            new Vector3(-span * 0.28f, crownSize.y * 0.10f, 0f),
            -12f,
            ArtLibrary.Leaf);
        AddFlatPicture(
            mass,
            mass.name + "_LayerLTip",
            new Vector2(span * 0.14f, crownSize.y * 0.12f),
            color,
            sortingOrder,
            // SlopeLTip closed the outer roof slat. Mid LayerL without
            // a lip stayed a plank in the cone (Flake before FlakeTip).
            // Hang off the outer pad; crownSize stays.
            new Vector3(-span * 0.38f, crownSize.y * 0.06f, 0f),
            -18f,
            ArtLibrary.Leaf);
        AddFlatPicture(
            mass,
            mass.name + "_Body",
            // Artist FanBody kissed FanR; pine Body 0.28 at −0.12
            // right +0.02 / BodyR left +0.13 still left a 0.11 fir
            // slit (cedar table analog). Wider near pad kisses the
            // outer BodyR. Hem BaseL/R stay off-axis. crownSize stays.
            new Vector2(span * 0.40f, crownSize.y * 0.34f),
            color,
            sortingOrder,
            new Vector3(-span * 0.02f, crownSize.y * 0.02f, 0f),
            -8f,
            ArtLibrary.Leaf);
        AddFlatPicture(
            mass,
            mass.name + "_BodyTip",
            new Vector2(span * 0.14f, crownSize.y * 0.14f),
            color,
            sortingOrder,
            // SlopeL2Tip closed the roof slat. Body without a lip
            // stayed a plank in the fir cone (FanBody before
            // FanBodyTip). Hang off the near pad; crownSize stays.
            new Vector3(-span * 0.18f, -crownSize.y * 0.08f, 0f),
            -14f,
            ArtLibrary.Leaf);
        AddFlatPicture(
            mass,
            mass.name + "_BodyR",
            new Vector2(span * 0.18f, crownSize.y * 0.22f),
            CrownPatch(color, true),
            sortingOrder,
            // Left edge +0.18 against Body right +0.18 (FanR kiss).
            // Outer hem; SlopeR / BaseR stay far. crownSize stays.
            new Vector3(span * 0.27f, -crownSize.y * 0.02f, 0f),
            6f,
            ArtLibrary.Leaf);
        AddFlatPicture(
            mass,
            mass.name + "_BodyRTip",
            new Vector2(span * 0.10f, crownSize.y * 0.10f),
            CrownPatch(color, true),
            sortingOrder,
            new Vector3(span * 0.36f, -crownSize.y * 0.10f, 0f),
            12f,
            ArtLibrary.Leaf);
        AddFlatPicture(
            mass,
            mass.name + "_Fill",
            new Vector2(span * 0.12f, crownSize.y * 0.22f),
            color,
            sortingOrder,
            // Chip rides Body (outer < 0), not the mid slit after
            // the kiss. crownSize stays.
            new Vector3(-span * 0.16f, crownSize.y * 0.02f, 0f),
            -6f,
            ArtLibrary.Leaf);
        AddFlatPicture(
            mass,
            mass.name + "_FillTip",
            new Vector2(span * 0.07f, crownSize.y * 0.10f),
            color,
            sortingOrder,
            // BodyTip closed the fir plank. Fill without a lip stayed
            // a mast chip after SlopeL2Tip (Cap before CapTip). Hang
            // off Body; crownSize stays.
            new Vector3(-span * 0.26f, -crownSize.y * 0.06f, 0f),
            -12f,
            ArtLibrary.Leaf);
        AddFlatPicture(
            mass,
            mass.name + "_FillR",
            // On BodyR pad (left ≥ +0.18), not the fir mid slit
            // (cedar FanFillR analog). crownSize stays.
            new Vector2(span * 0.12f, crownSize.y * 0.20f),
            CrownPatch(color, true),
            sortingOrder,
            new Vector3(span * 0.32f, 0f, 0f),
            5f,
            ArtLibrary.Leaf);
        AddFlatPicture(
            mass,
            mass.name + "_FillRTip",
            new Vector2(span * 0.07f, crownSize.y * 0.09f),
            CrownPatch(color, true),
            sortingOrder,
            new Vector3(span * 0.40f, -crownSize.y * 0.08f, 0f),
            11f,
            ArtLibrary.Leaf);
            AddFlatPicture(
                mass,
                mass.name + "_LayerR",
                new Vector2(span * 0.24f, crownSize.y * 0.20f),
                CrownPatch(color, true),
                sortingOrder,
            new Vector3(span * 0.28f, crownSize.y * 0.08f, 0f),
            10f,
            ArtLibrary.Leaf);
        AddFlatPicture(
            mass,
            mass.name + "_LayerRTip",
            new Vector2(span * 0.12f, crownSize.y * 0.10f),
            CrownPatch(color, true),
            sortingOrder,
            // LayerLTip closed the near mid-wedge. Far LayerR without
            // a lip stayed a plank (SlopeR before SlopeRTip). Hang off
            // the outer pad; crownSize stays.
            new Vector3(span * 0.38f, crownSize.y * 0.04f, 0f),
            16f,
            ArtLibrary.Leaf);
        AddFlatPicture(
            mass,
            mass.name + "_BaseL",
            new Vector2(span * 0.30f, crownSize.y * 0.28f),
            color,
            sortingOrder,
            // 0.36 at −0.22 still reached −0.04; BaseR 0.34 at +0.20
            // reached +0.03 — a fused hem on the fir mast (KiteR
            // analog after Needle1 left Sheath). Pads ride SlopeL2 /
            // SlopeR2. crownSize stays.
            new Vector3(-span * 0.32f, -crownSize.y * 0.32f, 0f),
            -10f,
            ArtLibrary.Leaf);
        AddFlatPicture(
            mass,
            mass.name + "_BaseLTip",
            new Vector2(span * 0.16f, crownSize.y * 0.12f),
            color,
            sortingOrder,
            // SlopeLTip closed the outer roof. BaseL without a lip
            // stayed a hem plank (Layer before LayerTip). Hang off
            // SlopeL2; crownSize stays.
            new Vector3(-span * 0.42f, -crownSize.y * 0.38f, 0f),
            -16f,
            ArtLibrary.Leaf);
            AddFlatPicture(
                mass,
                mass.name + "_BaseR",
                new Vector2(span * 0.28f, crownSize.y * 0.26f),
                CrownPatch(color, true),
                sortingOrder,
            new Vector3(span * 0.32f, -crownSize.y * 0.34f, 0f),
            8f,
            ArtLibrary.Leaf);
        AddFlatPicture(
            mass,
            mass.name + "_BaseRTip",
            new Vector2(span * 0.14f, crownSize.y * 0.10f),
            CrownPatch(color, true),
            sortingOrder,
            // BaseLTip closed the near hem. Far BaseR without a lip
            // stayed a plank (SlopeR before SlopeRTip). Hang off
            // SlopeR2; crownSize stays.
            new Vector3(span * 0.42f, -crownSize.y * 0.40f, 0f),
            14f,
            ArtLibrary.Leaf);
        AddFlatPicture(
            mass,
            mass.name + "_Cap",
            new Vector2(span * 0.22f, crownSize.y * 0.32f),
            color,
            sortingOrder,
            // (0.04, 0.28) was a mast hat between SlopeL/R (oak Dome
            // analog). Cap/CapR kiss at x=0 like Body/BodyR. CapFill
            // stays a chip on Cap, not a peak belt. crownSize stays.
            new Vector3(-span * 0.10f, crownSize.y * 0.24f, 0f),
            -8f,
            ArtLibrary.Leaf);
        AddFlatPicture(
            mass,
            mass.name + "_CapR",
            new Vector2(span * 0.20f, crownSize.y * 0.32f),
            CrownPatch(color, true),
            sortingOrder,
            // Cap body kissed at mid-Y, but CapR top 0.36 vs Cap 0.40
            // left a 0.12 peak slit above the far pad (Hang5/6 tip
            // analog). Match Cap height; CapTip/CapRTip kiss at x=0.
            // Hem stays on SlopeR. crownSize stays.
            new Vector3(span * 0.10f, crownSize.y * 0.24f, 0f),
            6f,
            ArtLibrary.Leaf);
        AddFlatPicture(
            mass,
            mass.name + "_CapFill",
            new Vector2(span * 0.12f, crownSize.y * 0.18f),
            color,
            sortingOrder,
            // After CapTip peak kiss: CapFill at −0.16 / CapFillTip at
            // −0.24 left a 0.08 shoulder slit under CapTip left −0.12
            // (Body Fill after Body kiss analog). Chip rides Cap under
            // CapTip; CapFillTip kisses CapTip. crownSize stays.
            new Vector3(-span * 0.12f, crownSize.y * 0.28f, 0f),
            -6f,
            ArtLibrary.Leaf);
        AddFlatPicture(
            mass,
            mass.name + "_CapFillTip",
            new Vector2(span * 0.07f, crownSize.y * 0.08f),
            color,
            sortingOrder,
            // CapTip closed the fir peak. CapFill without a lip
            // stayed a peak mast chip (Fill before FillTip). Hang
            // off Cap toward CapTip; right edge kisses CapTip left.
            // crownSize stays.
            new Vector3(-span * 0.14f, crownSize.y * 0.34f, 0f),
            -12f,
            ArtLibrary.Leaf);
        AddFlatPicture(
            mass,
            mass.name + "_CapFillR",
            new Vector2(span * 0.12f, crownSize.y * 0.16f),
            CrownPatch(color, true),
            sortingOrder,
            // On CapR under CapRTip (not the mid peak slit). crownSize stays.
            new Vector3(span * 0.12f, crownSize.y * 0.26f, 0f),
            5f,
            ArtLibrary.Leaf);
        AddFlatPicture(
            mass,
            mass.name + "_CapFillRTip",
            new Vector2(span * 0.07f, crownSize.y * 0.07f),
            CrownPatch(color, true),
            sortingOrder,
            // Left edge kisses CapRTip right. crownSize stays.
            new Vector3(span * 0.13f, crownSize.y * 0.32f, 0f),
            11f,
            ArtLibrary.Leaf);
        AddFlatPicture(
            mass,
            mass.name + "_CapTip",
            new Vector2(span * 0.12f, crownSize.y * 0.14f),
            color,
            sortingOrder,
            // CapTip at −0.18 right −0.12 / CapRTip left +0.13 left
            // a 0.25 peak slit above CapR (ApexTip analog). Kiss at
            // x=0; crownSize stays.
            new Vector3(-span * 0.06f, crownSize.y * 0.36f, 0f),
            -12f,
            ArtLibrary.Leaf);
        AddFlatPicture(
            mass,
            mass.name + "_CapRTip",
            new Vector2(span * 0.10f, crownSize.y * 0.12f),
            CrownPatch(color, true),
            sortingOrder,
            // Left edge 0 against CapTip right 0. crownSize stays.
            new Vector3(span * 0.05f, crownSize.y * 0.34f, 0f),
            10f,
            ArtLibrary.Leaf);
        // CapFillR rides CapR (outer > 0). Smooth SlopeL/R still read
        // as two wedges (cedar-fan analog).
        // Short hem needles 8–10° are a fir fringe, not a 22° comb.
        // crownSize stays.
        AddFlatPicture(
            mass,
            mass.name + "_FringeL",
            new Vector2(span * 0.08f, crownSize.y * 0.28f),
            color,
            sortingOrder,
            new Vector3(-span * 0.34f, -crownSize.y * 0.36f, 0f),
            -10f,
            ArtLibrary.Leaf);
            AddFlatPicture(
                mass,
                mass.name + "_FringeR",
                new Vector2(span * 0.07f, crownSize.y * 0.24f),
                OddLeaflet(color),
                sortingOrder,
            new Vector3(span * 0.30f, -crownSize.y * 0.38f, 0f),
            8f,
            ArtLibrary.Leaf);
        AddFlatPicture(
            mass,
            mass.name + "_FringeLTip",
            new Vector2(span * 0.05f, crownSize.y * 0.12f),
            color,
            sortingOrder,
            // WhorlLTip closed the mid needle. FringeL without a lip
            // stayed a green post on the fir hem (Whorl before
            // WhorlTip). Hang off the outer needle; crownSize stays.
            new Vector3(-span * 0.40f, -crownSize.y * 0.44f, 0f),
            -16f,
            ArtLibrary.Leaf);
        AddFlatPicture(
            mass,
            mass.name + "_FringeRTip",
            new Vector2(span * 0.04f, crownSize.y * 0.10f),
            OddLeaflet(color),
            sortingOrder,
            new Vector3(span * 0.36f, -crownSize.y * 0.46f, 0f),
            14f,
            ArtLibrary.Leaf);
        // SlopeL/R without a mid tuft still read as two wedges. Short
        // needles between the slopes are a fir whorl. crownSize stays.
        AddFlatPicture(
            mass,
            mass.name + "_WhorlL",
            new Vector2(span * 0.07f, crownSize.y * 0.12f),
            color,
            sortingOrder,
            new Vector3(-span * 0.38f, -crownSize.y * 0.26f, 0f),
            -10f,
            ArtLibrary.Leaf);
        AddFlatPicture(
            mass,
            mass.name + "_WhorlLTip",
            new Vector2(span * 0.045f, crownSize.y * 0.18f),
            color,
            sortingOrder,
            // Visual 19:46 leftover: fascicle Needle a stick; crown
            // WhorlL was the same post after NeedleTip. Short shaft,
            // long distal off the hem. crownSize stays.
            new Vector3(-span * 0.46f, -crownSize.y * 0.38f, 0f),
            -16f,
            ArtLibrary.Leaf);
            AddFlatPicture(
                mass,
                mass.name + "_WhorlR",
                new Vector2(span * 0.06f, crownSize.y * 0.10f),
                OddLeaflet(color),
                sortingOrder,
            new Vector3(span * 0.34f, -crownSize.y * 0.28f, 0f),
            9f,
            ArtLibrary.Leaf);
        AddFlatPicture(
            mass,
            mass.name + "_WhorlRTip",
            new Vector2(span * 0.04f, crownSize.y * 0.16f),
            OddLeaflet(color),
            sortingOrder,
            new Vector3(span * 0.42f, -crownSize.y * 0.40f, 0f),
            14f,
            ArtLibrary.Leaf);
    }

    // Cedar at 1x: layered tables. Slope ±26° on the 94 cm slab was a
    // pine cone. 0.92 Shelf at 0° was the table after the envelope hide
    // (apple-hem analog). Fans at ±8° still stacked as a chevron. Flat
    // pancakes, staggered in Y. Same tint; crownSize stays.
    private void PaintCedarCrownFans(Transform mass, Color color, int sortingOrder, float massWidth)
    {
        if (mass == null)
            return;
        float span = massWidth > 0.01f ? massWidth : crownSize.x;
        // 0.72 FanL was still a table after the fringe ticks
        // (cedar-slab analog). Two overlapping pads, not one shelf.
        // crownSize stays.
        // FanL 0.42 pancakes were still tables after FanM2 (coin pile).
        // Narrower pads + a hem drip under each: ragged cedar, not coins.
        AddFlatPicture(
            mass,
            mass.name + "_FanL",
            new Vector2(span * 0.26f, crownSize.y * 0.24f),
            color,
            sortingOrder,
            new Vector3(-span * 0.28f, -crownSize.y * 0.40f, 0f),
            -14f,
            ArtLibrary.Leaf);
        AddFlatPicture(
            mass,
            mass.name + "_FanLTip",
            new Vector2(span * 0.12f, crownSize.y * 0.10f),
            color,
            sortingOrder,
            new Vector3(-span * 0.42f, -crownSize.y * 0.50f, 0f),
            -22f,
            ArtLibrary.Leaf);
        AddFlatPicture(
            mass,
            mass.name + "_FanBody",
            // Plant FanR outer left FanBody 0.24 at −0.12 right-edge 0
            // while FanR left sat at +0.18 — sky slit through the
            // cedar table (pine Body after BodyR). Wider near pad
            // kisses the new FanR. crownSize stays.
            new Vector2(span * 0.40f, crownSize.y * 0.26f),
            color,
            sortingOrder,
            new Vector3(-span * 0.02f, -crownSize.y * 0.12f, 0f),
            -10f,
            ArtLibrary.Leaf);
        AddFlatPicture(
            mass,
            mass.name + "_FanBodyTip",
            new Vector2(span * 0.12f, crownSize.y * 0.12f),
            color,
            sortingOrder,
            // FanLTip closed the outer pancake. FanBody without a
            // lip stayed a coin on the table (FanL2 before FanL2Tip).
            // Hang off the near pad; crownSize stays.
            new Vector3(-span * 0.18f, -crownSize.y * 0.20f, 0f),
            -16f,
            ArtLibrary.Leaf);
        AddFlatPicture(
            mass,
            mass.name + "_FanFill",
            new Vector2(span * 0.12f, crownSize.y * 0.16f),
            color,
            sortingOrder,
            // After FanDomeFill under FanDomeTip: FanFill at −0.16 /
            // Y −0.10 sat ON the FanBody table mid (CapFill shoulder
            // slit analog, tip is down). Chip rides FanBody under
            // FanBodyTip; FanFillTip kisses FanBodyTip. crownSize stays.
            new Vector3(-span * 0.14f, -crownSize.y * 0.15f, 0f),
            -8f,
            ArtLibrary.Leaf);
        AddFlatPicture(
            mass,
            mass.name + "_FanFillTip",
            new Vector2(span * 0.07f, crownSize.y * 0.08f),
            color,
            sortingOrder,
            // FanBodyTip closed the pancake. FanFill without a lip
            // stayed a table mast chip after FanLTip (Cap before
            // CapTip). Hang off FanBody toward FanBodyTip; right edge
            // kisses FanBodyTip left. crownSize stays.
            new Vector3(-span * 0.20f, -crownSize.y * 0.22f, 0f),
            -14f,
            ArtLibrary.Leaf);
        AddFlatPicture(
            mass,
            mass.name + "_FanFillR",
            new Vector2(span * 0.12f, crownSize.y * 0.14f),
            CrownPatch(color, true),
            sortingOrder,
            // On FanR under FanRTip (not the FanBody mid table).
            // crownSize stays.
            new Vector3(span * 0.34f, -crownSize.y * 0.10f, 0f),
            6f,
            ArtLibrary.Leaf);
        AddFlatPicture(
            mass,
            mass.name + "_FanFillRTip",
            new Vector2(span * 0.07f, crownSize.y * 0.07f),
            CrownPatch(color, true),
            sortingOrder,
            // Left edge kisses FanRTip right. crownSize stays.
            new Vector3(span * 0.38f, -crownSize.y * 0.16f, 0f),
            12f,
            ArtLibrary.Leaf);
        AddFlatPicture(
            mass,
            mass.name + "_FanL2",
            new Vector2(span * 0.22f, crownSize.y * 0.18f),
            OddLeaflet(color),
            sortingOrder,
            // Visual 19:46 leftover: Fan2 a pancake after FanTip.
            // |x|≈0.04 sat on the spine (pine hang 0.18 analog).
            // Thinner outer table; FanDome keeps max-Y. crownSize stays.
            new Vector3(-span * 0.44f, -crownSize.y * 0.18f, 0f),
            -8f,
            ArtLibrary.Leaf);
        AddFlatPicture(
            mass,
            mass.name + "_FanL2Tip",
            new Vector2(span * 0.10f, crownSize.y * 0.08f),
            OddLeaflet(color),
            sortingOrder,
            new Vector3(-span * 0.58f, -crownSize.y * 0.28f, 0f),
            -16f,
            ArtLibrary.Leaf);
        AddFlatPicture(
            mass,
            mass.name + "_FanR",
            new Vector2(span * 0.20f, crownSize.y * 0.16f),
            CrownPatch(color, true),
            sortingOrder,
            // Visual 19:46 left FanL2/FanR2 off-spine; FanR 0.28 at
            // +0.12 still crossed x=0 (Fill mast analog). Thinner
            // outer table. crownSize stays.
            new Vector3(span * 0.28f, -crownSize.y * 0.06f, 0f),
            12f,
            ArtLibrary.Leaf);
        AddFlatPicture(
            mass,
            mass.name + "_FanRTip",
            new Vector2(span * 0.10f, crownSize.y * 0.08f),
            CrownPatch(color, true),
            sortingOrder,
            // Hang off the new FanR pad; crownSize stays.
            new Vector3(span * 0.40f, -crownSize.y * 0.14f, 0f),
            18f,
            ArtLibrary.Leaf);
        AddFlatPicture(
            mass,
            mass.name + "_FanR2",
            new Vector2(span * 0.22f, crownSize.y * 0.16f),
            CrownPatch(color, true),
            sortingOrder,
            new Vector3(span * 0.40f, crownSize.y * 0.12f, 0f),
            8f,
            ArtLibrary.Leaf);
        AddFlatPicture(
            mass,
            mass.name + "_FanR2Tip",
            new Vector2(span * 0.10f, crownSize.y * 0.08f),
            CrownPatch(color, true),
            sortingOrder,
            new Vector3(span * 0.54f, crownSize.y * 0.04f, 0f),
            16f,
            ArtLibrary.Leaf);
        AddFlatPicture(
            mass,
            mass.name + "_FanM",
            new Vector2(span * 0.28f, crownSize.y * 0.22f),
            color,
            sortingOrder,
            // |x| 0.14 was a hat on the spine (visual Fan2 analog).
            // Outer table; FanDome keeps max-Y. crownSize stays.
            new Vector3(-span * 0.38f, crownSize.y * 0.12f, 0f),
            -11f,
            ArtLibrary.Leaf);
        AddFlatPicture(
            mass,
            mass.name + "_FanMTip",
            new Vector2(span * 0.14f, crownSize.y * 0.10f),
            color,
            sortingOrder,
            // FanLTip closed the near pancake. FanM without a lip
            // stayed a coin under the dome (FanL2 before FanL2Tip).
            // Hang off the outer table; crownSize stays.
            new Vector3(-span * 0.48f, crownSize.y * 0.06f, 0f),
            -16f,
            ArtLibrary.Leaf);
        // FanM2 at (0.10, 0.34) was the second hat on the spine
        // (visual 11:00). Dome stays at max-Y; FanM2 on max |X|.
        // crownSize stays.
        AddFlatPicture(
            mass,
            mass.name + "_FanDome",
            new Vector2(span * 0.18f, crownSize.y * 0.20f),
            color,
            sortingOrder,
            // 0.16 at −0.42 sat wholly left of 0, so the cedar peak
            // was one tent (pine Cap without CapR analog). FanDome /
            // FanDomeR kiss at x=0 at max-Y. FanL2 stays the outer
            // left table. crownSize stays.
            new Vector3(-span * 0.09f, crownSize.y * 0.32f, 0f),
            -8f,
            ArtLibrary.Leaf);
        AddFlatPicture(
            mass,
            mass.name + "_FanDomeTip",
            new Vector2(span * 0.10f, crownSize.y * 0.10f),
            color,
            sortingOrder,
            // FanDomeTip at −0.16 right −0.11 / FanDomeRTip left +0.10
            // left a 0.21 peak slit (pine CapTip analog). Kiss at
            // x=0; crownSize stays.
            new Vector3(-span * 0.05f, crownSize.y * 0.40f, 0f),
            -14f,
            ArtLibrary.Leaf);
        AddFlatPicture(
            mass,
            mass.name + "_FanDomeR",
            new Vector2(span * 0.16f, crownSize.y * 0.20f),
            CrownPatch(color, true),
            sortingOrder,
            // Left edge 0 against FanDome right 0. Match FanDome top
            // so CapTip peak slit does not reopen. FanM2 keeps the
            // far lower table. crownSize stays.
            new Vector3(span * 0.08f, crownSize.y * 0.32f, 0f),
            6f,
            ArtLibrary.Leaf);
        AddFlatPicture(
            mass,
            mass.name + "_FanDomeRTip",
            new Vector2(span * 0.08f, crownSize.y * 0.08f),
            CrownPatch(color, true),
            sortingOrder,
            // Left edge 0 against FanDomeTip right 0. crownSize stays.
            new Vector3(span * 0.04f, crownSize.y * 0.38f, 0f),
            12f,
            ArtLibrary.Leaf);
        // CapFill rides Cap under CapTip. FanDome / FanDomeR kissed at
        // mid-Y, but no shoulder chip under FanDomeTip left the cedar
        // peak a one-tent after CapFill closed the fir (pine CapFill
        // before CapFillTip). FanFill now rides FanBody under
        // FanBodyTip (same CapFill language). crownSize / fork 28 /
        // taper 0.58 stay.
        AddFlatPicture(
            mass,
            mass.name + "_FanDomeFill",
            new Vector2(span * 0.10f, crownSize.y * 0.14f),
            color,
            sortingOrder,
            // After CapFill under CapTip: FanDome peak had no shoulder
            // chip (CapFill −0.16 slit analog on the cedar dome). Chip
            // rides FanDome under FanDomeTip; right edge toward
            // FanDomeTip left. crownSize stays.
            new Vector3(-span * 0.10f, crownSize.y * 0.34f, 0f),
            -6f,
            ArtLibrary.Leaf);
        AddFlatPicture(
            mass,
            mass.name + "_FanDomeFillTip",
            new Vector2(span * 0.06f, crownSize.y * 0.07f),
            color,
            sortingOrder,
            // FanDomeTip closed the cedar peak. FanDomeFill without a
            // lip stayed a dome mast chip (CapFill before CapFillTip).
            // Hang off FanDome toward FanDomeTip; right edge kisses
            // FanDomeTip left. crownSize stays.
            new Vector3(-span * 0.12f, crownSize.y * 0.38f, 0f),
            -12f,
            ArtLibrary.Leaf);
        AddFlatPicture(
            mass,
            mass.name + "_FanDomeFillR",
            new Vector2(span * 0.10f, crownSize.y * 0.12f),
            CrownPatch(color, true),
            sortingOrder,
            // On FanDomeR under FanDomeRTip (not the mid peak slit).
            // crownSize stays.
            new Vector3(span * 0.10f, crownSize.y * 0.32f, 0f),
            5f,
            ArtLibrary.Leaf);
        AddFlatPicture(
            mass,
            mass.name + "_FanDomeFillRTip",
            new Vector2(span * 0.06f, crownSize.y * 0.06f),
            CrownPatch(color, true),
            sortingOrder,
            // Left edge kisses FanDomeRTip right. crownSize stays.
            new Vector3(span * 0.11f, crownSize.y * 0.36f, 0f),
            11f,
            ArtLibrary.Leaf);
        AddFlatPicture(
            mass,
            mass.name + "_FanM2",
            new Vector2(span * 0.24f, crownSize.y * 0.20f),
            CrownPatch(color, true),
            sortingOrder,
            new Vector3(span * 0.40f, crownSize.y * 0.06f, 0f),
            9f,
            ArtLibrary.Leaf);
        AddFlatPicture(
            mass,
            mass.name + "_FanM2Tip",
            new Vector2(span * 0.12f, crownSize.y * 0.08f),
            CrownPatch(color, true),
            sortingOrder,
            new Vector3(span * 0.50f, 0f, 0f),
            14f,
            ArtLibrary.Leaf);
        AddFlatPicture(
            mass,
            mass.name + "_Droop",
            new Vector2(span * 0.34f, crownSize.y * 0.24f),
            CrownPatch(color, true),
            sortingOrder,
            new Vector3(span * 0.32f, -crownSize.y * 0.22f, 0f),
            5f,
            ArtLibrary.Leaf);
        AddFlatPicture(
            mass,
            mass.name + "_DroopTip",
            new Vector2(span * 0.16f, crownSize.y * 0.12f),
            CrownPatch(color, true),
            sortingOrder,
            // HemRTip closed the far needle. Droop without a lip
            // stayed a coin on the belly (FanBody before FanBodyTip).
            // Hang off the far hang; crownSize stays.
            new Vector3(span * 0.42f, -crownSize.y * 0.30f, 0f),
            12f,
            ArtLibrary.Leaf);
        AddFlatPicture(
            mass,
            mass.name + "_DroopL",
            new Vector2(span * 0.30f, crownSize.y * 0.22f),
            color,
            sortingOrder,
            // Right-only Droop was a one-tent cedar belly after
            // FanDomeR (HemL analog). Far hang stays Droop; this
            // pad rides FanL. crownSize stays.
            new Vector3(-span * 0.30f, -crownSize.y * 0.28f, 0f),
            -8f,
            ArtLibrary.Leaf);
        AddFlatPicture(
            mass,
            mass.name + "_DroopLTip",
            new Vector2(span * 0.14f, crownSize.y * 0.10f),
            color,
            sortingOrder,
            new Vector3(-span * 0.40f, -crownSize.y * 0.36f, 0f),
            -14f,
            ArtLibrary.Leaf);
        // 0.80 at −14° was a belly curtain on the 11 cm layer.
        // Short needles 8–10° hang off the outer fans. crownSize stays.
        float fringeH = Mathf.Max(crownSize.y * 0.28f, span * 0.022f);
        AddFlatPicture(
            mass,
            mass.name + "_FringeL",
            new Vector2(span * 0.06f, fringeH),
            color,
            sortingOrder,
            new Vector3(-span * 0.42f, -crownSize.y * 0.36f, 0f),
            -10f,
            ArtLibrary.Leaf);
        AddFlatPicture(
            mass,
            mass.name + "_FringeR",
            new Vector2(span * 0.05f, fringeH * 0.88f),
            OddLeaflet(color),
            sortingOrder,
            new Vector3(span * 0.36f, -crownSize.y * 0.18f, 0f),
            8f,
            ArtLibrary.Leaf);
        AddFlatPicture(
            mass,
            mass.name + "_FringeM",
            new Vector2(span * 0.05f, fringeH * 0.72f),
            color,
            sortingOrder,
            new Vector3(span * 0.22f, crownSize.y * 0.02f, 0f),
            9f,
            ArtLibrary.Leaf);
        AddFlatPicture(
            mass,
            mass.name + "_FringeMTip",
            new Vector2(span * 0.035f, fringeH * 0.32f),
            color,
            sortingOrder,
            // FringeLTip closed the outer needle. Mid FringeM
            // without a lip stayed a post on the shelf. Hang off
            // Fan1; crownSize stays.
            new Vector3(span * 0.28f, -crownSize.y * 0.06f, 0f),
            14f,
            ArtLibrary.Leaf);
        // Hem drips so stacked pads are a ragged shelf, not a coin pile.
        AddFlatPicture(
            mass,
            mass.name + "_HemL",
            new Vector2(span * 0.06f, fringeH * 0.90f),
            color,
            sortingOrder,
            new Vector3(-span * 0.48f, -crownSize.y * 0.48f, 0f),
            -10f,
            ArtLibrary.Leaf);
        AddFlatPicture(
            mass,
            mass.name + "_HemR",
            new Vector2(span * 0.05f, fringeH * 0.78f),
            OddLeaflet(color),
            sortingOrder,
            new Vector3(span * 0.44f, -crownSize.y * 0.22f, 0f),
            8f,
            ArtLibrary.Leaf);
        AddFlatPicture(
            mass,
            mass.name + "_FringeLTip",
            new Vector2(span * 0.04f, fringeH * 0.40f),
            color,
            sortingOrder,
            // FanLTip closed the pancake. Fringe/Hem without a lip
            // stayed green posts on the shelf (pine Fringe before
            // FringeTip). Hang off the outer needle; crownSize stays.
            new Vector3(-span * 0.48f, -crownSize.y * 0.44f, 0f),
            -16f,
            ArtLibrary.Leaf);
        AddFlatPicture(
            mass,
            mass.name + "_FringeRTip",
            new Vector2(span * 0.035f, fringeH * 0.36f),
            OddLeaflet(color),
            sortingOrder,
            new Vector3(span * 0.42f, -crownSize.y * 0.26f, 0f),
            14f,
            ArtLibrary.Leaf);
        AddFlatPicture(
            mass,
            mass.name + "_HemLTip",
            new Vector2(span * 0.04f, fringeH * 0.36f),
            color,
            sortingOrder,
            new Vector3(-span * 0.54f, -crownSize.y * 0.56f, 0f),
            -16f,
            ArtLibrary.Leaf);
        AddFlatPicture(
            mass,
            mass.name + "_HemRTip",
            new Vector2(span * 0.035f, fringeH * 0.32f),
            OddLeaflet(color),
            sortingOrder,
            new Vector3(span * 0.50f, -crownSize.y * 0.30f, 0f),
            14f,
            ArtLibrary.Leaf);
    }

    // Oak crown at 1x: palmate envelope, not a potato of five similar
    // ovals (0.50/0.40/0.36). Unequal lobes + a sinus, same language as
    // the leaf. Slot 1 is a shoulder (0.62). Autumn tints LobeR / Skirt
    // from palLeafFar; sun mix sits on FingerL so Dome/LobeL fuse.
    // Same grow size; extra boxes only.
    private void PaintOakCrownCloud(
        Transform mass,
        Color color,
        int sortingOrder,
        int slot,
        float woodHeading)
    {
        if (mass == null)
            return;
        float s = slot > 0 ? 0.62f : 1f;
        Color lobeFar = color;
        Color dome = color;
        if (ResolvedSeason == PlantSeason.Autumn)
        {
            lobeFar = palLeafFar;
            dome = Color.Lerp(color, palLeafNear, 0.40f);
        }
        AddFlatPicture(
            mass,
            mass.name + "_Core",
            new Vector2(crownSize.x * 0.12f * s, crownSize.y * 0.10f * s),
            color,
            sortingOrder,
            new Vector3(-crownSize.x * 0.22f * s, -0.16f * crownSize.y * s, 0f),
            6f,
            CrownBake);
        AddFlatPicture(
            mass,
            mass.name + "_CoreTip",
            new Vector2(crownSize.x * 0.08f * s, crownSize.y * 0.06f * s),
            color,
            sortingOrder,
            // WristTip closed the palmate stalk. Crown Core without a
            // lip stayed a blunt basal card (CarmB before CarmBTip).
            // Hang off LobeL; sinus stays empty. crownSize stays.
            new Vector3(-crownSize.x * 0.28f * s, -0.22f * crownSize.y * s, 0f),
            12f,
            CrownBake);
        AddFlatPicture(
            mass,
            mass.name + "_LobeL",
            new Vector2(crownSize.x * 0.48f * s, crownSize.y * 0.32f * s),
            color,
            sortingOrder,
            new Vector3(-crownSize.x * 0.40f * s, crownSize.y * 0.08f * s, 0f),
            -32f,
            CrownBake);
        AddFlatPicture(
            mass,
            mass.name + "_LobeLTip",
            new Vector2(crownSize.x * 0.22f * s, crownSize.y * 0.16f * s),
            color,
            sortingOrder,
            // Leaflet LobeL0Tip closed the inner palmate. Crown LobeL
            // without a lip stayed a blunt cloud pad after LobeL1Tip
            // (Cap before CapTip). Hang off LobeL; sinus stays empty.
            // crownSize stays.
            new Vector3(-crownSize.x * 0.58f * s, crownSize.y * 0.14f * s, 0f),
            -40f,
            CrownBake);
        AddFlatPicture(
            mass,
            mass.name + "_LobeR",
            new Vector2(crownSize.x * 0.24f * s, crownSize.y * 0.22f * s),
            lobeFar,
            sortingOrder,
            new Vector3(crownSize.x * 0.34f * s, -crownSize.y * 0.04f * s, 0f),
            22f,
            CrownBake);
        AddFlatPicture(
            mass,
            mass.name + "_LobeRTip",
            new Vector2(crownSize.x * 0.14f * s, crownSize.y * 0.12f * s),
            lobeFar,
            sortingOrder,
            new Vector3(crownSize.x * 0.48f * s, -crownSize.y * 0.02f * s, 0f),
            28f,
            CrownBake);
        AddFlatPicture(
            mass,
            mass.name + "_CoreR",
            new Vector2(crownSize.x * 0.10f * s, crownSize.y * 0.08f * s),
            OddLeaflet(lobeFar),
            sortingOrder,
            // Left-only Core was a one-tent palmate stalk after
            // DomeR/SkirtR (leaflet Wrist before WristR). Far basal
            // rides LobeR at |X|; sinus stays empty. crownSize stays.
            new Vector3(crownSize.x * 0.34f * s, -crownSize.y * 0.12f * s, 0f),
            -4f,
            CrownBake);
        AddFlatPicture(
            mass,
            mass.name + "_CoreRTip",
            new Vector2(crownSize.x * 0.06f * s, crownSize.y * 0.05f * s),
            OddLeaflet(lobeFar),
            sortingOrder,
            // CoreTip closed the near basal. CoreR without a lip
            // stayed a blunt far stalk after LobeRTip (CarmB before
            // CarmBTip). Hang off LobeR at |X|; sinus stays empty.
            // crownSize stays.
            new Vector3(crownSize.x * 0.42f * s, -crownSize.y * 0.18f * s, 0f),
            -10f,
            CrownBake);
        // CapFill rides Cap under CapTip. TwigDomeFill closed winter
        // peaks; oak Core / CoreR still had Tip but no shoulder chip
        // under CoreTip — one-tent palmate stalk after TwigDomeFill.
        // Chip rides Core under CoreTip on LobeL; sinus between
        // LobeL/R stays empty. fork 28 / taper 0.58 stay.
        AddFlatPicture(
            mass,
            mass.name + "_CoreFill",
            new Vector2(crownSize.x * 0.08f * s, crownSize.y * 0.07f * s),
            color,
            sortingOrder,
            // After TwigDomeFill under TwigDomeTip: oak Core had no
            // CapFill-language chip (CapFill −0.12 slit analog on the
            // palmate stalk). Chip rides Core under CoreTip; right
            // edge toward CoreTip left. Sinus stays empty.
            // crownSize stays.
            new Vector3(-crownSize.x * 0.24f * s, -0.18f * crownSize.y * s, 0f),
            4f,
            CrownBake);
        AddFlatPicture(
            mass,
            mass.name + "_CoreFillTip",
            new Vector2(crownSize.x * 0.05f * s, crownSize.y * 0.04f * s),
            color,
            sortingOrder,
            // CoreTip closed the palmate basal. CoreFill without a lip
            // stayed a stalk mast chip (CapFill before CapFillTip).
            // Hang off Core toward CoreTip; right edge kisses
            // CoreTip left. Sinus stays empty. crownSize stays.
            new Vector3(-crownSize.x * 0.26f * s, -0.20f * crownSize.y * s, 0f),
            8f,
            CrownBake);
        AddFlatPicture(
            mass,
            mass.name + "_CoreFillR",
            new Vector2(crownSize.x * 0.07f * s, crownSize.y * 0.06f * s),
            OddLeaflet(lobeFar),
            sortingOrder,
            // On CoreR under CoreRTip (not the sinus). crownSize stays.
            new Vector3(crownSize.x * 0.37f * s, -crownSize.y * 0.14f * s, 0f),
            -2f,
            CrownBake);
        AddFlatPicture(
            mass,
            mass.name + "_CoreFillRTip",
            new Vector2(crownSize.x * 0.04f * s, crownSize.y * 0.035f * s),
            OddLeaflet(lobeFar),
            sortingOrder,
            // Left edge kisses CoreRTip right. Sinus stays empty.
            // crownSize stays.
            new Vector3(crownSize.x * 0.39f * s, -crownSize.y * 0.16f * s, 0f),
            -6f,
            CrownBake);
        AddFlatPicture(
            mass,
            mass.name + "_Dome",
            new Vector2(crownSize.x * 0.26f * s, crownSize.y * 0.28f * s),
            color,
            sortingOrder,
            // (0.04, 0.30) filled the palmate valley (leaflet LobeTip
            // analog). Dome rides LobeL; sinus between LobeL/R stays
            // empty. FingerR 0.68 is the outer tooth. Autumn sun mix
            // sits on FingerL so overlapping Dome/LobeL is one lobe.
            // crownSize stays.
            new Vector3(-crownSize.x * 0.28f * s, crownSize.y * 0.26f * s, 0f),
            -10f,
            CrownBake);
        AddFlatPicture(
            mass,
            mass.name + "_DomeTip",
            new Vector2(crownSize.x * 0.14f * s, crownSize.y * 0.12f * s),
            color,
            sortingOrder,
            // LobeLTip closed the palmate pad. Dome without a lip
            // stayed a blunt peak after LobeL0Tip (Cap before CapTip).
            // Hang off LobeL; sinus stays empty. crownSize stays.
            new Vector3(-crownSize.x * 0.36f * s, crownSize.y * 0.38f * s, 0f),
            -16f,
            CrownBake);
        AddFlatPicture(
            mass,
            mass.name + "_DomeR",
            new Vector2(crownSize.x * 0.20f * s, crownSize.y * 0.22f * s),
            OddLeaflet(lobeFar),
            sortingOrder,
            // Left-only Dome was a one-tent palmate after SkirtR
            // (bush Dome before DomeR). Far peak rides LobeR at
            // |X|; do not kiss x=0 — that fills the sinus. crownSize stays.
            new Vector3(crownSize.x * 0.34f * s, crownSize.y * 0.22f * s, 0f),
            10f,
            CrownBake);
        AddFlatPicture(
            mass,
            mass.name + "_DomeRTip",
            new Vector2(crownSize.x * 0.12f * s, crownSize.y * 0.10f * s),
            OddLeaflet(lobeFar),
            sortingOrder,
            // DomeTip closed the near peak. DomeR without a lip stayed
            // a blunt far hat after LobeRTip (Cap before CapTip). Hang
            // off LobeR at |X|; sinus stays empty. crownSize stays.
            new Vector3(crownSize.x * 0.44f * s, crownSize.y * 0.30f * s, 0f),
            14f,
            CrownBake);
        // CapFill rides Cap under CapTip. ApexFill closed fir/poplar
        // tips; oak Dome / DomeR still had no shoulder chip under
        // DomeTip — one-tent palmate peak after ApexFill. Chip rides
        // Dome under DomeTip on LobeL; sinus between LobeL/R stays
        // empty. fork 28 / taper 0.58 stay.
        AddFlatPicture(
            mass,
            mass.name + "_DomeFill",
            new Vector2(crownSize.x * 0.12f * s, crownSize.y * 0.14f * s),
            color,
            sortingOrder,
            // After ApexFill under ApexTip: oak Dome had no CapFill-
            // language chip (CapFill −0.12 slit analog on the palmate
            // peak). Chip rides Dome under DomeTip; right edge toward
            // DomeTip left. Sinus stays empty. crownSize stays.
            new Vector3(-crownSize.x * 0.30f * s, crownSize.y * 0.30f * s, 0f),
            -8f,
            CrownBake);
        AddFlatPicture(
            mass,
            mass.name + "_DomeFillTip",
            new Vector2(crownSize.x * 0.07f * s, crownSize.y * 0.07f * s),
            color,
            sortingOrder,
            // DomeTip closed the palmate peak. DomeFill without a lip
            // stayed a peak mast chip (CapFill before CapFillTip). Hang
            // off Dome toward DomeTip; right edge kisses DomeTip left.
            // Sinus stays empty. crownSize stays.
            new Vector3(-crownSize.x * 0.32f * s, crownSize.y * 0.34f * s, 0f),
            -14f,
            CrownBake);
        AddFlatPicture(
            mass,
            mass.name + "_DomeFillR",
            new Vector2(crownSize.x * 0.12f * s, crownSize.y * 0.12f * s),
            OddLeaflet(lobeFar),
            sortingOrder,
            // On DomeR under DomeRTip (not the sinus). crownSize stays.
            new Vector3(crownSize.x * 0.38f * s, crownSize.y * 0.24f * s, 0f),
            8f,
            CrownBake);
        AddFlatPicture(
            mass,
            mass.name + "_DomeFillRTip",
            new Vector2(crownSize.x * 0.07f * s, crownSize.y * 0.06f * s),
            OddLeaflet(lobeFar),
            sortingOrder,
            // Left edge kisses DomeRTip right. Sinus stays empty.
            // crownSize stays.
            new Vector3(crownSize.x * 0.40f * s, crownSize.y * 0.28f * s, 0f),
            12f,
            CrownBake);
        AddFlatPicture(
            mass,
            mass.name + "_Skirt",
            new Vector2(crownSize.x * 0.30f * s, crownSize.y * 0.20f * s),
            lobeFar,
            sortingOrder,
            new Vector3(-crownSize.x * 0.32f * s, -crownSize.y * 0.24f * s, 0f),
            16f,
            CrownBake);
        AddFlatPicture(
            mass,
            mass.name + "_SkirtTip",
            new Vector2(crownSize.x * 0.16f * s, crownSize.y * 0.10f * s),
            lobeFar,
            sortingOrder,
            // Leaflet LobeL2Tip closed the caudal palmate. Crown Skirt
            // without a lip stayed a blunt hem after LobeL1Tip (Droop
            // before DroopTip). Hang off LobeL; sinus stays empty.
            // crownSize stays.
            new Vector3(-crownSize.x * 0.42f * s, -crownSize.y * 0.34f * s, 0f),
            22f,
            CrownBake);
        AddFlatPicture(
            mass,
            mass.name + "_SkirtR",
            new Vector2(crownSize.x * 0.22f * s, crownSize.y * 0.16f * s),
            OddLeaflet(lobeFar),
            sortingOrder,
            // Left-only Skirt was a one-tent hem under the palmate
            // after LobeL/R (poplar KiteL2 analog). Far caudal rides
            // LobeR; sinus between LobeL/R stays empty. crownSize stays.
            new Vector3(crownSize.x * 0.34f * s, -crownSize.y * 0.20f * s, 0f),
            -12f,
            CrownBake);
        AddFlatPicture(
            mass,
            mass.name + "_SkirtRTip",
            new Vector2(crownSize.x * 0.12f * s, crownSize.y * 0.08f * s),
            OddLeaflet(lobeFar),
            sortingOrder,
            new Vector3(crownSize.x * 0.44f * s, -crownSize.y * 0.28f * s, 0f),
            -18f,
            CrownBake);
        // LobeL/R with Finger in the valley filled the sinus (leaf
        // LobeL1 analog). FingerR 0.12 at 0.50 was a mitten stub.
        // Outer teeth on the lobe tips; the notch stays empty.
        // crownSize stays.
        AddFlatPicture(
            mass,
            mass.name + "_FingerL",
            new Vector2(crownSize.x * 0.18f * s, crownSize.y * 0.14f * s),
            dome,
            sortingOrder,
            new Vector3(-crownSize.x * 0.54f * s, crownSize.y * 0.20f * s, 0f),
            -40f,
            CrownBake);
        AddFlatPicture(
            mass,
            mass.name + "_FingerLTip",
            new Vector2(crownSize.x * 0.10f * s, crownSize.y * 0.08f * s),
            dome,
            sortingOrder,
            // Leaflet LobeL0Tip closed the inner pad. Crown FingerL
            // without a lip stayed a mitten stub after LobeL1Tip
            // (Cap before CapTip). Hang off FingerL; sinus stays
            // empty. crownSize stays.
            new Vector3(-crownSize.x * 0.64f * s, crownSize.y * 0.24f * s, 0f),
            -46f,
            CrownBake);
        AddFlatPicture(
            mass,
            mass.name + "_ToothL",
            new Vector2(crownSize.x * 0.14f * s, crownSize.y * 0.10f * s),
            dome,
            sortingOrder,
            // ToothR closed the far serration. Crown stopped at
            // FingerL — a mitten on the left (leaflet ToothL). Inner
            // tooth on LobeL; sinus stays empty. crownSize stays.
            new Vector3(-crownSize.x * 0.46f * s, crownSize.y * 0.12f * s, 0f),
            -34f,
            CrownBake);
        AddFlatPicture(
            mass,
            mass.name + "_ToothLTip",
            new Vector2(crownSize.x * 0.08f * s, crownSize.y * 0.06f * s),
            dome,
            sortingOrder,
            new Vector3(-crownSize.x * 0.54f * s, crownSize.y * 0.16f * s, 0f),
            -40f,
            CrownBake);
        AddFlatPicture(
            mass,
            mass.name + "_LobeL1",
            new Vector2(crownSize.x * 0.16f * s, crownSize.y * 0.12f * s),
            dome,
            sortingOrder,
            // Leaflet LobeL1 is the outer left lobe. Crown stopped
            // at ToothL after LobeR1 closed the right — a mitten
            // on the left. Outer pad; sinus stays empty.
            // crownSize stays.
            new Vector3(-crownSize.x * 0.68f * s, crownSize.y * 0.16f * s, 0f),
            -44f,
            CrownBake);
        AddFlatPicture(
            mass,
            mass.name + "_LobeL1Tip",
            new Vector2(crownSize.x * 0.10f * s, crownSize.y * 0.08f * s),
            dome,
            sortingOrder,
            // Leaflet LobeL1 without a lip stayed a blunt outer
            // lobe after plant closed the mitten (Cap before
            // CapTip). Hang off LobeL1; sinus stays empty.
            // crownSize stays.
            new Vector3(-crownSize.x * 0.80f * s, crownSize.y * 0.18f * s, 0f),
            -50f,
            CrownBake);
        // Leaflet ToothR 0.56 / FingerR 0.68. Crown FingerR sat at
        // 0.56 — the same mitten stub. Outer tooth then finger;
        // sinus between LobeL/R stays empty. crownSize stays.
        AddFlatPicture(
            mass,
            mass.name + "_ToothR",
            new Vector2(crownSize.x * 0.16f * s, crownSize.y * 0.12f * s),
            OddLeaflet(lobeFar),
            sortingOrder,
            new Vector3(crownSize.x * 0.50f * s, crownSize.y * 0.08f * s, 0f),
            36f,
            CrownBake);
        AddFlatPicture(
            mass,
            mass.name + "_ToothRTip",
            new Vector2(crownSize.x * 0.10f * s, crownSize.y * 0.08f * s),
            OddLeaflet(lobeFar),
            sortingOrder,
            new Vector3(crownSize.x * 0.60f * s, crownSize.y * 0.10f * s, 0f),
            42f,
            CrownBake);
        AddFlatPicture(
            mass,
            mass.name + "_FingerR",
            new Vector2(crownSize.x * 0.18f * s, crownSize.y * 0.14f * s),
            OddLeaflet(lobeFar),
            sortingOrder,
            new Vector3(crownSize.x * 0.68f * s, crownSize.y * 0.14f * s, 0f),
            40f,
            CrownBake);
        AddFlatPicture(
            mass,
            mass.name + "_FingerRTip",
            new Vector2(crownSize.x * 0.10f * s, crownSize.y * 0.08f * s),
            OddLeaflet(lobeFar),
            sortingOrder,
            new Vector3(crownSize.x * 0.80f * s, crownSize.y * 0.16f * s, 0f),
            46f,
            CrownBake);
        AddFlatPicture(
            mass,
            mass.name + "_LobeR1",
            new Vector2(crownSize.x * 0.16f * s, crownSize.y * 0.12f * s),
            OddLeaflet(lobeFar),
            sortingOrder,
            // Leaflet LobeR1 is the outer right lobe. Crown stopped at
            // FingerR — a mitten on the right (LobeL1 without LobeR1).
            // Outer pad past ToothR; sinus stays empty. crownSize stays.
            new Vector3(crownSize.x * 0.80f * s, crownSize.y * 0.10f * s, 0f),
            42f,
            CrownBake);
        AddFlatPicture(
            mass,
            mass.name + "_LobeR1Tip",
            new Vector2(crownSize.x * 0.10f * s, crownSize.y * 0.08f * s),
            OddLeaflet(lobeFar),
            sortingOrder,
            new Vector3(crownSize.x * 0.92f * s, crownSize.y * 0.08f * s, 0f),
            48f,
            CrownBake);
        // After flatten, a palmate hat still sat inside the fork.
        // One outer reach on the limb's world-out side fills the
        // canopy; the sinus between LobeL/R stays empty. crownSize stays.
        if (Mathf.Abs(woodHeading) > SpreadLimbDeg)
        {
            float reachSign = Mathf.Sign(woodHeading);
            Color reachC = OddLeaflet(reachSign > 0f ? lobeFar : color);
            AddFlatPicture(
                mass,
                mass.name + "_Reach",
                new Vector2(crownSize.x * 0.20f * s, crownSize.y * 0.16f * s),
                reachC,
                sortingOrder,
                new Vector3(
                    reachSign * crownSize.x * 0.76f * s,
                    crownSize.y * 0.04f * s,
                    0f),
                reachSign * 24f,
                CrownBake);
            AddFlatPicture(
                mass,
                mass.name + "_ReachTip",
                new Vector2(crownSize.x * 0.12f * s, crownSize.y * 0.10f * s),
                reachC,
                sortingOrder,
                // FingerLTip closed the palmate tooth. Reach without a
                // lip stayed a blunt outer pad (Cap before CapTip).
                // Hang off the limb; sinus stays empty. crownSize stays.
                new Vector3(
                    reachSign * crownSize.x * 0.88f * s,
                    crownSize.y * 0.06f * s,
                    0f),
                reachSign * 30f,
                CrownBake);
        }
    }

    // Lombardy at 1x: a pointed flame. 0° Apex on the 15×54 cm envelope
    // was still a sausage tip. Twin kites 0.78/0.72 were remex-plus.
    // Unequal kite + silver far, same as the leaf. crownSize stays.
    private void PaintPoplarCrownFlame(Transform mass, Color color, int sortingOrder)
    {
        if (mass == null)
            return;
        Color accent = winterWoodyCrown
            ? Color.Lerp(color, palBarkDark, 0.40f)
            : palLeafFar;
        AddFlatPicture(
            mass,
            mass.name + "_KiteL",
            new Vector2(crownSize.x * 0.34f, crownSize.y * 0.36f),
            color,
            sortingOrder,
            // −0.22 right −0.05 / KiteR left +0.10 left a 0.15 sky
            // slit down the 15 cm column (pine Slope tents analog).
            // A palmate sinus is oak; a flame kisses at x=0 like
            // Body/BodyR. Leaflet kites keep their valley. crownSize stays.
            new Vector3(-crownSize.x * 0.17f, crownSize.y * 0.04f, 0f),
            -28f,
            CrownBake);
        AddFlatPicture(
            mass,
            mass.name + "_KiteLTip",
            new Vector2(crownSize.x * 0.18f, crownSize.y * 0.16f),
            color,
            sortingOrder,
            // BodyTip closed the fir hem. KiteL without a lip stayed a
            // blunt flame pad (Cap before CapTip). Hang off the left
            // kite; kiss at x=0 stays the kites. crownSize stays.
            new Vector3(-crownSize.x * 0.28f, crownSize.y * 0.12f, 0f),
            -34f,
            CrownBake);
        AddFlatPicture(
            mass,
            mass.name + "_KiteR",
            new Vector2(crownSize.x * 0.36f, crownSize.y * 0.32f),
            accent,
            sortingOrder,
            // Left edge 0 against KiteL right 0 (pine BodyR analog).
            // Far kite stays the larger pad. crownSize stays.
            new Vector3(crownSize.x * 0.18f, 0f, 0f),
            16f,
            CrownBake);
        AddFlatPicture(
            mass,
            mass.name + "_KiteRTip",
            new Vector2(crownSize.x * 0.18f, crownSize.y * 0.14f),
            accent,
            sortingOrder,
            new Vector3(crownSize.x * 0.28f, crownSize.y * 0.08f, 0f),
            22f,
            CrownBake);
        AddFlatPicture(
            mass,
            mass.name + "_Apex",
            new Vector2(crownSize.x * 0.34f, crownSize.y * 0.26f),
            color,
            sortingOrder,
            // Peak rides KiteL after the lips moved in to kiss.
            // Width 0.34 kisses x=0; ApexR is the far peak (pine
            // ApexR / bush DomeR analog). Fill stays a chip.
            // crownSize stays.
            new Vector3(-crownSize.x * 0.17f, crownSize.y * 0.32f, 0f),
            -12f,
            CrownBake);
        AddFlatPicture(
            mass,
            mass.name + "_ApexTip",
            new Vector2(crownSize.x * 0.18f, crownSize.y * 0.12f),
            color,
            sortingOrder,
            // ApexTip at −0.22 right −0.13 / ApexRTip left +0.10 left
            // a 0.23 peak slit above ApexR (pine CapTip analog). Kiss
            // at x=0; the flame stays the kites. crownSize stays.
            new Vector3(-crownSize.x * 0.09f, crownSize.y * 0.46f, 0f),
            -16f,
            CrownBake);
        AddFlatPicture(
            mass,
            mass.name + "_ApexR",
            new Vector2(crownSize.x * 0.30f, crownSize.y * 0.26f),
            accent,
            sortingOrder,
            // Left-only Apex was a one-tent flame after KiteL/R
            // kissed (pine tip hat before ApexR). Left edge 0
            // against Apex right 0. Match Apex top so CapTip peak
            // slit does not reopen. KiteR stays the outer hem.
            // crownSize stays.
            new Vector3(crownSize.x * 0.15f, crownSize.y * 0.32f, 0f),
            8f,
            CrownBake);
        AddFlatPicture(
            mass,
            mass.name + "_ApexRTip",
            new Vector2(crownSize.x * 0.16f, crownSize.y * 0.10f),
            accent,
            sortingOrder,
            // Left edge 0 against ApexTip right 0. crownSize stays.
            new Vector3(crownSize.x * 0.08f, crownSize.y * 0.44f, 0f),
            12f,
            CrownBake);
        // CapFill rides Cap under CapTip. Apex / ApexR kissed at mid-Y,
        // but no shoulder chip under ApexTip left the flame peak a
        // one-tent after CapFill closed the fir (pine CapFill before
        // CapFillTip). Fill stays the kite valley chip. crownSize /
        // fork 28 / taper 0.58 stay.
        AddFlatPicture(
            mass,
            mass.name + "_ApexFill",
            new Vector2(crownSize.x * 0.16f, crownSize.y * 0.14f),
            color,
            sortingOrder,
            // After FanFill under FanBodyTip: poplar Apex peak had no
            // CapFill-language chip (CapFill −0.12 slit analog on the
            // flame). Chip rides Apex under ApexTip; right edge toward
            // ApexTip left. crownSize stays.
            new Vector3(-crownSize.x * 0.20f, crownSize.y * 0.38f, 0f),
            -8f,
            CrownBake);
        AddFlatPicture(
            mass,
            mass.name + "_ApexFillTip",
            new Vector2(crownSize.x * 0.09f, crownSize.y * 0.07f),
            color,
            sortingOrder,
            // ApexTip closed the sausage peak. ApexFill without a lip
            // stayed a peak mast chip (CapFill before CapFillTip).
            // Hang off Apex toward ApexTip; right edge kisses ApexTip
            // left. crownSize stays.
            new Vector3(-crownSize.x * 0.22f, crownSize.y * 0.44f, 0f),
            -14f,
            CrownBake);
        AddFlatPicture(
            mass,
            mass.name + "_ApexFillR",
            new Vector2(crownSize.x * 0.14f, crownSize.y * 0.12f),
            accent,
            sortingOrder,
            // On ApexR under ApexRTip (not the mid peak slit).
            // crownSize stays.
            new Vector3(crownSize.x * 0.18f, crownSize.y * 0.36f, 0f),
            6f,
            CrownBake);
        AddFlatPicture(
            mass,
            mass.name + "_ApexFillRTip",
            new Vector2(crownSize.x * 0.08f, crownSize.y * 0.06f),
            accent,
            sortingOrder,
            // Left edge kisses ApexRTip right. crownSize stays.
            new Vector3(crownSize.x * 0.20f, crownSize.y * 0.42f, 0f),
            12f,
            CrownBake);
            AddFlatPicture(
                mass,
                mass.name + "_Waist",
                new Vector2(crownSize.x * 0.28f, crownSize.y * 0.24f),
                accent,
                sortingOrder,
                new Vector3(-crownSize.x * 0.17f, -crownSize.y * 0.08f, 0f),
                4f,
                CrownBake);
            AddFlatPicture(
                mass,
                mass.name + "_WaistTip",
                new Vector2(crownSize.x * 0.14f, crownSize.y * 0.10f),
                accent,
                sortingOrder,
                // KiteL2Tip closed the flame hem. Crown Waist without
                // a lip stayed a midrib card after ApexTip (Cap
                // before CapTip). Hang off Waist; the flame stays
                // the kites. crownSize stays.
                new Vector3(-crownSize.x * 0.26f, -crownSize.y * 0.16f, 0f),
                10f,
                CrownBake);
            AddFlatPicture(
                mass,
                mass.name + "_WaistR",
                new Vector2(crownSize.x * 0.24f, crownSize.y * 0.20f),
                accent,
                sortingOrder,
                // Left-only Waist was a one-tent flame midrib after
                // ApexR/BaseR (leaflet Waist before WaistR). Far chip
                // rides KiteR; the flame is the kites. crownSize stays.
                new Vector3(crownSize.x * 0.18f, -crownSize.y * 0.06f, 0f),
                -4f,
                CrownBake);
            AddFlatPicture(
                mass,
                mass.name + "_WaistRTip",
                new Vector2(crownSize.x * 0.12f, crownSize.y * 0.08f),
                accent,
                sortingOrder,
                new Vector3(crownSize.x * 0.26f, -crownSize.y * 0.12f, 0f),
                -10f,
                CrownBake);
        AddFlatPicture(
            mass,
            mass.name + "_Fill",
            new Vector2(crownSize.x * 0.14f, crownSize.y * 0.22f),
            color,
            sortingOrder,
            // Chip rides KiteL (outer < 0); FillR rides KiteR.
            // Lips of the flame are the kites, not these chips.
            // crownSize stays.
            new Vector3(-crownSize.x * 0.22f, crownSize.y * 0.02f, 0f),
            -10f,
            CrownBake);
        AddFlatPicture(
            mass,
            mass.name + "_FillTip",
            new Vector2(crownSize.x * 0.08f, crownSize.y * 0.10f),
            color,
            sortingOrder,
            // ApexTip closed the sausage peak. Flame Fill without a
            // lip stayed a valley card after KiteL2Tip (Cap before
            // CapTip). Hang off KiteL; the flame stays the kites.
            // crownSize stays.
            new Vector3(-crownSize.x * 0.30f, -crownSize.y * 0.06f, 0f),
            -16f,
            CrownBake);
        AddFlatPicture(
            mass,
            mass.name + "_FillR",
            new Vector2(crownSize.x * 0.14f, crownSize.y * 0.20f),
            accent,
            sortingOrder,
            new Vector3(crownSize.x * 0.22f, 0f, 0f),
            10f,
            CrownBake);
        AddFlatPicture(
            mass,
            mass.name + "_FillRTip",
            new Vector2(crownSize.x * 0.08f, crownSize.y * 0.08f),
            accent,
            sortingOrder,
            new Vector3(crownSize.x * 0.30f, -crownSize.y * 0.08f, 0f),
            16f,
            CrownBake);
        // 0.50 Waist at x=0.02 was a sausage on the 15 cm column
        // (willow-pole analog). −0.12 × 0.28 still reached +0.02 and
        // filled the kite valley (leaflet Waist analog). Pad rides
        // KiteL; the flame is the kites, not a belt. crownSize stays.
        AddFlatPicture(
            mass,
            mass.name + "_KiteL2",
            new Vector2(crownSize.x * 0.32f, crownSize.y * 0.28f),
            color,
            sortingOrder,
            // −0.28 right −0.12 left a one-tent hem after the flame
            // lips kissed (leaflet KiteL2 analog). Width 0.32 kisses
            // x=0; KiteR2 is the far hem. crownSize stays.
            new Vector3(-crownSize.x * 0.16f, -crownSize.y * 0.16f, 0f),
            -18f,
            CrownBake);
        AddFlatPicture(
            mass,
            mass.name + "_KiteL2Tip",
            new Vector2(crownSize.x * 0.16f, crownSize.y * 0.12f),
            color,
            sortingOrder,
            // Leaflet KiteLTip closed the tuft pad. Crown KiteL2
            // without a lip stayed a blunt flame hem after ApexTip
            // (Cap before CapTip). Hang off KiteL2; kiss at x=0
            // stays the kites. crownSize stays.
            new Vector3(-crownSize.x * 0.26f, -crownSize.y * 0.24f, 0f),
            -24f,
            CrownBake);
        AddFlatPicture(
            mass,
            mass.name + "_KiteR2",
            new Vector2(crownSize.x * 0.30f, crownSize.y * 0.24f),
            accent,
            sortingOrder,
            // Left edge 0 against KiteL2 right 0 (leaflet KiteR2).
            // Far hem; Base rides KiteL2, BaseR rides KiteR2.
            // crownSize stays.
            new Vector3(crownSize.x * 0.15f, -crownSize.y * 0.14f, 0f),
            14f,
            CrownBake);
        AddFlatPicture(
            mass,
            mass.name + "_KiteR2Tip",
            new Vector2(crownSize.x * 0.14f, crownSize.y * 0.10f),
            accent,
            sortingOrder,
            new Vector3(crownSize.x * 0.24f, -crownSize.y * 0.22f, 0f),
            20f,
            CrownBake);
        AddFlatPicture(
            mass,
            mass.name + "_Base",
            new Vector2(crownSize.x * 0.28f, crownSize.y * 0.16f),
            accent,
            sortingOrder,
            // 0.40 at −0.16 right +0.04 filled the hem valley after
            // KiteL2/R2 kissed (Waist sausage analog). Width 0.28
            // rides KiteL2 (outer < 0); BaseR is the far hem.
            // crownSize stays.
            new Vector3(-crownSize.x * 0.16f, -crownSize.y * 0.34f, 0f),
            -18f,
            CrownBake);
        AddFlatPicture(
            mass,
            mass.name + "_BaseTip",
            new Vector2(crownSize.x * 0.14f, crownSize.y * 0.08f),
            accent,
            sortingOrder,
            // WaistTip closed the kite midrib. Flame Base without a
            // lip stayed a hem card after KiteL2Tip (Cap before
            // CapTip). Hang off KiteL2; valley stays the kites.
            // crownSize stays.
            new Vector3(-crownSize.x * 0.24f, -crownSize.y * 0.42f, 0f),
            -24f,
            CrownBake);
        AddFlatPicture(
            mass,
            mass.name + "_BaseR",
            new Vector2(crownSize.x * 0.26f, crownSize.y * 0.14f),
            accent,
            sortingOrder,
            // Left edge 0 against KiteR2. Far caudal; the flame is
            // the kites, not a belt. crownSize stays.
            new Vector3(crownSize.x * 0.15f, -crownSize.y * 0.32f, 0f),
            12f,
            CrownBake);
        AddFlatPicture(
            mass,
            mass.name + "_BaseRTip",
            new Vector2(crownSize.x * 0.13f, crownSize.y * 0.07f),
            accent,
            sortingOrder,
            // BaseTip closed the near hem. Far BaseR without a lip
            // stayed a caudal card (SlopeR before SlopeRTip). Hang
            // off KiteR2; valley stays the kites. crownSize stays.
            new Vector3(crownSize.x * 0.23f, -crownSize.y * 0.40f, 0f),
            18f,
            CrownBake);
    }

        // Slot 1 at x=−0.08 sat on the wood axis (cedar FanM analog).
        // C-sickles off the pole; hang aims +Y down. crownSize stays.
    private void PaintWillowCrownCurtain(Transform mass, Color color, int sortingOrder)
    {
        if (mass == null)
            return;
        PaintWillowHang(mass, color, sortingOrder, 0,
            new Vector2(crownSize.x * 0.62f, crownSize.y * 0.36f),
            // After Hang5/6 mid-kiss: Hang0 at −0.50 right −0.41 /
            // Hang1 at −0.30 left −0.37 left a 0.04 sky slit (pine
            // Body analog on the outer left). Closer left sickle;
            // tall C pad 0.28 stays. crownSize stays.
            new Vector3(-crownSize.x * 0.42f, -crownSize.y * 0.02f, 0f),
            14f);
        PaintWillowHang(mass, color, sortingOrder, 1,
            new Vector2(crownSize.x * 0.52f, crownSize.y * 0.32f),
            // Right edge −0.19 against Hang0 right −0.33 and Hang5
            // left −0.20 (Hang5/6 kiss chain). Off the 11 cm spine.
            // crownSize stays.
            new Vector3(-crownSize.x * 0.26f, -crownSize.y * 0.12f, 0f),
            10f);
        PaintWillowHang(mass, color, sortingOrder, 2,
            new Vector2(crownSize.x * 0.40f, crownSize.y * 0.34f),
            // Hang4 right +0.22 / Hang2 at +0.36 left +0.30 left a
            // 0.08 sky slit (Hang0/1 analog on the right). Closer
            // outer right; tall C pad 0.28 stays. crownSize stays.
            new Vector3(crownSize.x * 0.28f, -crownSize.y * 0.08f, 0f),
            -10f);
        PaintWillowHang(mass, color, sortingOrder, 4,
            new Vector2(crownSize.x * 0.30f, crownSize.y * 0.30f),
            // Mid-right sickle; left edge +0.14 against Hang6 right
            // +0.18; right +0.22 kisses Hang2 left. crownSize stays.
            new Vector3(crownSize.x * 0.18f, -crownSize.y * 0.10f, 0f),
            -6f);
        PaintWillowHang(mass, color, sortingOrder, 5,
            new Vector2(crownSize.x * 0.28f, crownSize.y * 0.28f),
            // Mid-curtain kiss owned by PaintWillowHang (slot 5/6
            // pad 0.72): Hang5 right 0 / Hang6 left 0. Outer Hang0–4
            // keep the tall C (0.28) so they are not a hyphen paddle.
            // Not the −0.10 overlap that rebuilt a mast. crownSize stays.
            new Vector3(-crownSize.x * 0.10f, -crownSize.y * 0.08f, 0f),
            8f);
        PaintWillowHang(mass, color, sortingOrder, 6,
            new Vector2(crownSize.x * 0.26f, crownSize.y * 0.26f),
            // Mid pad 0.72 of 0.26 = 0.19. Left edge 0 against Hang5
            // right 0 (pine BodyR analog). crownSize stays.
            new Vector3(crownSize.x * 0.09f, -crownSize.y * 0.10f, 0f),
            -6f);
        PaintWillowHang(mass, color, sortingOrder, 3,
            new Vector2(crownSize.x * 0.40f, crownSize.y * 0.28f),
            // Hang2 right +0.33 / Hang3 at +0.50 left +0.44 left a
            // 0.11 outer slit. Kiss Hang2; far hem stays. crownSize stays.
            new Vector3(crownSize.x * 0.39f, -crownSize.y * 0.20f, 0f),
            -16f);
    }

    private void PaintWillowHang(
        Transform mass,
        Color color,
        int sortingOrder,
        int slot,
        Vector2 blade,
        Vector3 at,
        float tilt)
    {
        float rotSide = tilt >= 0f ? 1f : -1f;
        // Mass already aims +Y at world down (AttachCrownOn hang).
        // Visual 11:00: extras at −Y climbed back up the wood (a pole).
        // Step sideways and along +Y. Blade.x was 0.14 of crownSize.x
        // (1.5 cm on the 11 cm envelope — the 4 mm tick). C-sickle
        // fills crownSize. rotSide followed tilt, so left hangs
        // stepped +X and right hangs −X — both Arc/Drip walked INTO
        // the mast (Hang2 analog). Step away from the wood axis.
        // crownSize stays.
        float outX = at.x >= 0f ? 1f : -1f;
        float stepX = Mathf.Max(crownSize.x * 0.42f, 0.032f);
        float drop = Mathf.Max(blade.y * 0.55f, 0.040f);
        // Visual 19:46 leftover: Hang 0.38×0.16 of the blade was a
        // hyphen C after HangTip. Outer slots keep a tall C (0.28) at
        // origin. Mid Hang5/6 after that C left a ~0.11 sky slit at
        // x=0 (pine Body after BodyR) — the comment assumed pad 0.72
        // but every slot got 0.28. Mid pads kiss at x=0; tip still
        // hangs off the C, not a curtain paddle. Outer Hang0–3
        // stations were then walked in so tall-C pads kiss each
        // other (Hang0/1, Hang4/2/3) without widening to a paddle.
        // crownSize stays.
        bool midKiss = slot == 5 || slot == 6;
        float hangPad = midKiss ? 0.72f : 0.28f;
        float hangTipPad = midKiss ? 0.36f : 0.18f;
        AddFlatPicture(
            mass,
            mass.name + "_Hang" + slot,
            new Vector2(blade.x * hangPad, blade.y * 0.32f),
            color,
            sortingOrder,
            at,
            tilt,
            CrownBake);
        AddFlatPicture(
            mass,
            mass.name + "_HangTip" + slot,
            new Vector2(blade.x * hangTipPad, blade.y * 0.14f),
            color,
            sortingOrder,
            at + new Vector3(outX * blade.x * (midKiss ? 0.55f : 0.42f), drop * 0.12f, 0f),
            tilt + rotSide * 12f,
            CrownBake);
        AddFlatPicture(
            mass,
            mass.name + "_Arc" + slot,
            new Vector2(blade.x * 0.56f, blade.y * 0.28f),
            color,
            sortingOrder,
            at + new Vector3(outX * stepX, drop, 0f),
            tilt + rotSide * 12f,
            CrownBake);
        AddFlatPicture(
            mass,
            mass.name + "_ArcTip" + slot,
            new Vector2(blade.x * 0.28f, blade.y * 0.12f),
            color,
            sortingOrder,
            at + new Vector3(outX * stepX * 1.28f, drop * 1.22f, 0f),
            tilt + rotSide * 16f,
            CrownBake);
        AddFlatPicture(
            mass,
            mass.name + "_Drip" + slot,
            new Vector2(blade.x * 0.40f, blade.y * 0.32f),
            CrownPatch(color, (slot & 1) != 0),
            sortingOrder,
            at + new Vector3(outX * stepX * 1.65f, drop * 1.70f, 0f),
            tilt + rotSide * 18f,
            CrownBake);
        AddFlatPicture(
            mass,
            mass.name + "_DripTip" + slot,
            new Vector2(blade.x * 0.22f, blade.y * 0.16f),
            CrownPatch(color, (slot & 1) != 0),
            sortingOrder,
            // HemTip closed the cedar shelf. Willow Drip without a
            // lip stayed a paddle at the curtain end (Droop before
            // DroopTip). Hang off the sickle; crownSize stays.
            at + new Vector3(outX * stepX * 2.05f, drop * 2.05f, 0f),
            tilt + rotSide * 24f,
            CrownBake);
    }

    // Orchard cloud: apple is a bitten round (unequal sinus), plum a
    // taller droplet. Equal Core/Lobe/Dome ovals were a raspberry
    // (oak-potato analog). Apple Core/Dome ride LobeL so the bite
    // stays empty (oak 12:14 analog). Plum Core 0.36×0.58 still filled
    // the droplet after the X pad (sausage analog). Core is a small
    // pad on LobeL, not a second fruit. Plum Knot at +0.10 filled
    // that sinus after Core left (Scallop analog). Apple Scallop1 at
    // u×0.30 = −0.084 still sat in the bite after that pad comment —
    // X is the LobeL/LobeR pad, not a scale of u.
    private void PaintOrchardCrownCloud(Transform mass, Color color, int sortingOrder)
    {
        if (mass == null)
            return;
        bool plum = ResolvedKind == TreeKind.Plum;
        AddFlatPicture(
            mass,
            mass.name + "_Core",
            new Vector2(
                crownSize.x * (plum ? 0.14f : 0.12f),
                crownSize.y * (plum ? 0.22f : 0.18f)),
            color,
            sortingOrder,
            // Apple Core at (0.02, 0.02) still sat in the bite (oak
            // Core-on-axis analog). Plum 0.36×0.58 at −0.22 still filled
            // the droplet (KiteR analog). Small pad on LobeL.
            // crownSize stays.
            new Vector3(
                -crownSize.x * 0.22f,
                plum ? -0.12f * crownSize.y : -0.16f * crownSize.y,
                0f),
            plum ? 4f : 8f,
            CrownBake);
        AddFlatPicture(
            mass,
            mass.name + "_CoreTip",
            new Vector2(
                crownSize.x * (plum ? 0.08f : 0.08f),
                crownSize.y * (plum ? 0.10f : 0.08f)),
            color,
            sortingOrder,
            // Oak WristTip closed the palmate stalk. Orchard Core
            // without a lip stayed a blunt basal card (CarmB before
            // CarmBTip). Hang off LobeL; bite stays empty.
            // crownSize stays.
            new Vector3(
                -crownSize.x * 0.28f,
                plum ? -0.22f * crownSize.y : -0.24f * crownSize.y,
                0f),
            plum ? 10f : 14f,
            CrownBake);
        AddFlatPicture(
            mass,
            mass.name + "_LobeL",
            new Vector2(
                crownSize.x * (plum ? 0.28f : 0.56f),
                crownSize.y * (plum ? 0.32f : 0.36f)),
            color,
            sortingOrder,
            new Vector3(-crownSize.x * (plum ? 0.22f : 0.32f), -crownSize.y * 0.04f, 0f),
            plum ? -10f : -22f,
            CrownBake);
        AddFlatPicture(
            mass,
            mass.name + "_LobeLTip",
            new Vector2(
                crownSize.x * (plum ? 0.14f : 0.26f),
                crownSize.y * (plum ? 0.14f : 0.16f)),
            color,
            sortingOrder,
            // Oak crown LobeLTip closed the palmate pad. Orchard LobeL
            // without a lip stayed a blunt cloud after ScallopTip
            // (Cap before CapTip). Hang off LobeL; bite stays empty.
            // crownSize stays.
            new Vector3(
                -crownSize.x * (plum ? 0.34f : 0.48f),
                plum ? 0.04f * crownSize.y : 0.06f * crownSize.y,
                0f),
            plum ? -16f : -30f,
            CrownBake);
        AddFlatPicture(
            mass,
            mass.name + "_LobeR",
            new Vector2(
                crownSize.x * (plum ? 0.22f : 0.24f),
                crownSize.y * (plum ? 0.28f : 0.22f)),
            CrownPatch(color, true),
            sortingOrder,
            new Vector3(crownSize.x * (plum ? 0.24f : 0.34f), -crownSize.y * 0.08f, 0f),
            plum ? 8f : 18f,
            CrownBake);
        AddFlatPicture(
            mass,
            mass.name + "_LobeRTip",
            new Vector2(
                crownSize.x * (plum ? 0.12f : 0.14f),
                crownSize.y * (plum ? 0.12f : 0.12f)),
            CrownPatch(color, true),
            sortingOrder,
            new Vector3(
                crownSize.x * (plum ? 0.36f : 0.48f),
                plum ? -0.02f * crownSize.y : -0.04f * crownSize.y,
                0f),
            plum ? 14f : 24f,
            CrownBake);
        AddFlatPicture(
            mass,
            mass.name + "_CoreR",
            new Vector2(
                crownSize.x * (plum ? 0.12f : 0.10f),
                crownSize.y * (plum ? 0.18f : 0.14f)),
            CrownPatch(color, true),
            sortingOrder,
            // Left-only Core was a one-tent orchard stalk after
            // DomeR/SkirtR (oak Core before CoreR). Far basal rides
            // LobeR; bite stays empty. crownSize stays.
            new Vector3(
                crownSize.x * (plum ? 0.24f : 0.34f),
                plum ? -0.10f * crownSize.y : -0.14f * crownSize.y,
                0f),
            plum ? -4f : -6f,
            CrownBake);
        AddFlatPicture(
            mass,
            mass.name + "_CoreRTip",
            new Vector2(
                crownSize.x * (plum ? 0.07f : 0.06f),
                crownSize.y * (plum ? 0.08f : 0.06f)),
            CrownPatch(color, true),
            sortingOrder,
            // Oak CoreRTip closed the palmate far stalk. Orchard
            // CoreR without a lip stayed a blunt basal card after
            // WaistTip (CarmB before CarmBTip). Hang off LobeR;
            // bite stays empty. crownSize stays.
            new Vector3(
                crownSize.x * (plum ? 0.32f : 0.42f),
                plum ? -0.18f * crownSize.y : -0.22f * crownSize.y,
                0f),
            plum ? -10f : -12f,
            CrownBake);
        // CapFill rides Cap under CapTip. TwigDomeFill closed winter
        // peaks; orchard Core / CoreR still had Tip but no shoulder
        // chip under CoreTip — one-tent bitten stalk after
        // TwigDomeFill. Chip rides Core under CoreTip on LobeL; bite
        // stays empty. fork 28 / taper 0.58 stay.
        AddFlatPicture(
            mass,
            mass.name + "_CoreFill",
            new Vector2(
                crownSize.x * (plum ? 0.08f : 0.08f),
                crownSize.y * (plum ? 0.12f : 0.10f)),
            color,
            sortingOrder,
            // After TwigDomeFill under TwigDomeTip: orchard Core had
            // no CapFill-language chip (CapFill −0.12 slit analog on
            // the bitten stalk). Chip rides Core under CoreTip;
            // right edge toward CoreTip left. Bite stays empty.
            // crownSize stays.
            new Vector3(
                -crownSize.x * 0.24f,
                plum ? -0.15f * crownSize.y : -0.19f * crownSize.y,
                0f),
            plum ? 2f : 6f,
            CrownBake);
        AddFlatPicture(
            mass,
            mass.name + "_CoreFillTip",
            new Vector2(
                crownSize.x * (plum ? 0.05f : 0.05f),
                crownSize.y * (plum ? 0.06f : 0.05f)),
            color,
            sortingOrder,
            // CoreTip closed the orchard basal. CoreFill without a
            // lip stayed a stalk mast chip (CapFill before
            // CapFillTip). Hang off Core toward CoreTip; right edge
            // kisses CoreTip left. Bite stays empty. crownSize stays.
            new Vector3(
                -crownSize.x * 0.26f,
                plum ? -0.18f * crownSize.y : -0.21f * crownSize.y,
                0f),
            plum ? 6f : 10f,
            CrownBake);
        AddFlatPicture(
            mass,
            mass.name + "_CoreFillR",
            new Vector2(
                crownSize.x * (plum ? 0.08f : 0.07f),
                crownSize.y * (plum ? 0.10f : 0.08f)),
            CrownPatch(color, true),
            sortingOrder,
            // On CoreR under CoreRTip (not the bite). crownSize stays.
            new Vector3(
                crownSize.x * (plum ? 0.27f : 0.37f),
                plum ? -0.13f * crownSize.y : -0.17f * crownSize.y,
                0f),
            plum ? -2f : -4f,
            CrownBake);
        AddFlatPicture(
            mass,
            mass.name + "_CoreFillRTip",
            new Vector2(
                crownSize.x * (plum ? 0.045f : 0.04f),
                crownSize.y * (plum ? 0.05f : 0.04f)),
            CrownPatch(color, true),
            sortingOrder,
            // Left edge kisses CoreRTip right. Bite stays empty.
            // crownSize stays.
            new Vector3(
                crownSize.x * (plum ? 0.29f : 0.39f),
                plum ? -0.15f * crownSize.y : -0.19f * crownSize.y,
                0f),
            plum ? -6f : -8f,
            CrownBake);
        AddFlatPicture(
            mass,
            mass.name + "_Skirt",
            new Vector2(
                crownSize.x * (plum ? 0.22f : 0.28f),
                crownSize.y * (plum ? 0.18f : 0.20f)),
            color,
            sortingOrder,
            // Oak Skirt sits under LobeL; orchard skipped the near
            // caudal so SkirtR was a one-tent hem (oak Skirt analog).
            // Near hem rides LobeL; bite stays empty. crownSize stays.
            new Vector3(
                -crownSize.x * (plum ? 0.22f : 0.32f),
                -crownSize.y * (plum ? 0.22f : 0.28f),
                0f),
            plum ? 10f : 14f,
            CrownBake);
        AddFlatPicture(
            mass,
            mass.name + "_SkirtTip",
            new Vector2(
                crownSize.x * (plum ? 0.12f : 0.16f),
                crownSize.y * (plum ? 0.10f : 0.10f)),
            color,
            sortingOrder,
            // Oak SkirtTip closed the palmate hem. Orchard Skirt
            // without a lip stayed a blunt caudal (Droop before
            // DroopTip). Hang off LobeL; bite stays empty.
            // crownSize stays.
            new Vector3(
                -crownSize.x * (plum ? 0.30f : 0.42f),
                -crownSize.y * (plum ? 0.32f : 0.38f),
                0f),
            plum ? 16f : 20f,
            CrownBake);
        AddFlatPicture(
            mass,
            mass.name + "_SkirtR",
            new Vector2(
                crownSize.x * (plum ? 0.18f : 0.20f),
                crownSize.y * (plum ? 0.16f : 0.18f)),
            CrownPatch(color, true),
            sortingOrder,
            // Oak SkirtR closed the palmate hem; orchard stopped at
            // LobeR so the cloud was a one-tent caudal after the bite
            // stayed empty. Far hem rides LobeR; Core/Dome stay on
            // LobeL. crownSize stays.
            new Vector3(
                crownSize.x * (plum ? 0.24f : 0.34f),
                -crownSize.y * (plum ? 0.26f : 0.30f),
                0f),
            plum ? -8f : -10f,
            CrownBake);
        AddFlatPicture(
            mass,
            mass.name + "_SkirtRTip",
            new Vector2(
                crownSize.x * (plum ? 0.10f : 0.12f),
                crownSize.y * (plum ? 0.08f : 0.08f)),
            CrownPatch(color, true),
            sortingOrder,
            new Vector3(
                crownSize.x * (plum ? 0.34f : 0.44f),
                -crownSize.y * (plum ? 0.34f : 0.38f),
                0f),
            plum ? -14f : -16f,
            CrownBake);
        AddFlatPicture(
            mass,
            mass.name + "_Dome",
            new Vector2(
                crownSize.x * (plum ? 0.24f : 0.30f),
                crownSize.y * (plum ? 0.22f : 0.28f)),
            color,
            sortingOrder,
            // Apple (0.04, 0.30) filled the bite (oak Dome analog).
            // Plum 0.04 was the same hat between LobeL/R. Peak rides
            // LobeL; sinus stays empty. crownSize stays.
            new Vector3(-crownSize.x * (plum ? 0.22f : 0.28f),
                crownSize.y * (plum ? 0.32f : 0.26f),
                0f),
            -6f,
            CrownBake);
        AddFlatPicture(
            mass,
            mass.name + "_DomeTip",
            new Vector2(
                crownSize.x * (plum ? 0.14f : 0.16f),
                crownSize.y * (plum ? 0.12f : 0.14f)),
            color,
            sortingOrder,
            // Oak LobeLTip closed the palmate. Orchard Dome without a
            // lip stayed a blunt peak after ScallopTip (Cap before
            // CapTip). Hang off LobeL; bite stays empty. crownSize stays.
            new Vector3(
                -crownSize.x * (plum ? 0.30f : 0.38f),
                crownSize.y * (plum ? 0.42f : 0.38f),
                0f),
            -12f,
            CrownBake);
        AddFlatPicture(
            mass,
            mass.name + "_DomeR",
            new Vector2(
                crownSize.x * (plum ? 0.20f : 0.24f),
                crownSize.y * (plum ? 0.18f : 0.22f)),
            CrownPatch(color, true),
            sortingOrder,
            // SkirtR closed the caudal; summer Dome stayed a one-tent
            // peak on LobeL (oak Dome before DomeR). Far peak rides
            // LobeR at |X|; bite between LobeL/R stays empty.
            // crownSize stays.
            new Vector3(
                crownSize.x * (plum ? 0.24f : 0.34f),
                crownSize.y * (plum ? 0.28f : 0.22f),
                0f),
            8f,
            CrownBake);
        AddFlatPicture(
            mass,
            mass.name + "_DomeRTip",
            new Vector2(
                crownSize.x * (plum ? 0.12f : 0.14f),
                crownSize.y * (plum ? 0.10f : 0.12f)),
            CrownPatch(color, true),
            sortingOrder,
            new Vector3(
                crownSize.x * (plum ? 0.34f : 0.44f),
                crownSize.y * (plum ? 0.36f : 0.32f),
                0f),
            12f,
            CrownBake);
        // CapFill rides Cap under CapTip. Orchard Dome / DomeR still
        // had no shoulder chip under DomeTip after ApexFill closed
        // fir/poplar — one-tent peak on LobeL. Chip rides Dome under
        // DomeTip; bite between LobeL/R stays empty. crownSize stays.
        AddFlatPicture(
            mass,
            mass.name + "_DomeFill",
            new Vector2(
                crownSize.x * (plum ? 0.10f : 0.12f),
                crownSize.y * (plum ? 0.12f : 0.14f)),
            color,
            sortingOrder,
            // After ApexFill under ApexTip: orchard Dome had no
            // CapFill-language chip. Chip rides Dome under DomeTip;
            // right edge toward DomeTip left. Bite stays empty.
            new Vector3(
                -crownSize.x * (plum ? 0.24f : 0.30f),
                crownSize.y * (plum ? 0.36f : 0.30f),
                0f),
            -6f,
            CrownBake);
        AddFlatPicture(
            mass,
            mass.name + "_DomeFillTip",
            new Vector2(
                crownSize.x * (plum ? 0.06f : 0.07f),
                crownSize.y * (plum ? 0.06f : 0.07f)),
            color,
            sortingOrder,
            // DomeTip closed the orchard peak. DomeFill without a lip
            // stayed a peak mast chip (CapFill before CapFillTip). Hang
            // off Dome toward DomeTip; bite stays empty. crownSize stays.
            new Vector3(
                -crownSize.x * (plum ? 0.26f : 0.32f),
                crownSize.y * (plum ? 0.40f : 0.34f),
                0f),
            -12f,
            CrownBake);
        AddFlatPicture(
            mass,
            mass.name + "_DomeFillR",
            new Vector2(
                crownSize.x * (plum ? 0.10f : 0.12f),
                crownSize.y * (plum ? 0.10f : 0.12f)),
            CrownPatch(color, true),
            sortingOrder,
            // On DomeR under DomeRTip (not the bite). crownSize stays.
            new Vector3(
                crownSize.x * (plum ? 0.28f : 0.38f),
                crownSize.y * (plum ? 0.30f : 0.24f),
                0f),
            5f,
            CrownBake);
        AddFlatPicture(
            mass,
            mass.name + "_DomeFillRTip",
            new Vector2(
                crownSize.x * (plum ? 0.06f : 0.07f),
                crownSize.y * (plum ? 0.05f : 0.06f)),
            CrownPatch(color, true),
            sortingOrder,
            // Left edge kisses DomeRTip right. Bite stays empty.
            new Vector3(
                crownSize.x * (plum ? 0.30f : 0.40f),
                crownSize.y * (plum ? 0.34f : 0.28f),
                0f),
            11f,
            CrownBake);
        if (ResolvedKind == TreeKind.Apple)
        {
            // Dense Apple 96 used a 0.50 hem — a table under the cloud
            // (cedar-slab analog). Unequal scallops are orchard, not
            // three coins. fruitSize stays.
            int nScallop = maxSegments > 31 ? 2 : 3;
            for (int s = 0; s < nScallop; s++)
            {
                // s==1 at u×0.30 = −0.084 sat IN the bite after the
                // comment said it rode LobeL (oak Dome analog). X is
                // the pad, not a 0.30 scale of u. LobeL −0.32 / LobeR
                // +0.34; sinus stays empty. crownSize stays.
                float scX;
                float scY;
                float scW;
                float scH;
                float scRot;
                bool rightHem = nScallop == 2 ? s == 1 : s == 2;
                if (rightHem)
                {
                    scX = crownSize.x * 0.34f;
                    scY = -crownSize.y * 0.34f;
                    scW = 0.22f;
                    scH = 0.18f;
                    scRot = 8f;
                }
                else if (s == 1)
                {
                    scX = -crownSize.x * 0.32f;
                    scY = -crownSize.y * 0.18f;
                    scW = 0.38f;
                    scH = 0.22f;
                    scRot = -6f;
                }
                else
                {
                    scX = -crownSize.x * 0.32f;
                    scY = -crownSize.y * 0.28f;
                    scW = 0.28f;
                    scH = 0.18f;
                    scRot = -8f;
                }
                AddFlatPicture(
                    mass,
                    mass.name + "_Scallop" + s,
                    new Vector2(crownSize.x * scW, crownSize.y * scH),
                    color,
                    sortingOrder,
                    new Vector3(scX, scY, 0f),
                    scRot,
                    CrownBake);
                AddFlatPicture(
                    mass,
                    mass.name + "_ScallopTip" + s,
                    new Vector2(crownSize.x * scW * 0.48f, crownSize.y * scH * 0.42f),
                    color,
                    sortingOrder,
                    // LobeL1Tip closed the palmate outer lobe. Apple
                    // Scallop without a lip stayed a coin on the
                    // bitten hem (Droop before DroopTip). Hang off
                    // the pad; bite stays empty. crownSize stays.
                    new Vector3(
                        scX + (rightHem ? 1f : -1f) * crownSize.x * scW * 0.28f,
                        scY - crownSize.y * scH * 0.22f,
                        0f),
                    scRot + (rightHem ? 8f : -8f),
                    CrownBake);
            }
        }
        else if (plum)
        {
            AddFlatPicture(
                mass,
                mass.name + "_Knot",
                new Vector2(crownSize.x * 0.22f, crownSize.y * 0.34f),
                color,
                sortingOrder,
                // +0.10 sat IN the droplet sinus after Core/Dome left
                // for LobeL (apple Scallop1 analog). Pad rides LobeL;
                // LobeR stays the outer cheek. crownSize stays.
                new Vector3(-crownSize.x * 0.22f, -crownSize.y * 0.18f, 0f),
                -8f,
                CrownBake);
            AddFlatPicture(
                mass,
                mass.name + "_KnotTip",
                new Vector2(crownSize.x * 0.12f, crownSize.y * 0.14f),
                color,
                sortingOrder,
                // ScallopTip closed the apple hem. Plum Knot without
                // a lip stayed a droplet card (Droop before DroopTip).
                // Hang off LobeL; bite stays empty. crownSize stays.
                new Vector3(-crownSize.x * 0.32f, -crownSize.y * 0.28f, 0f),
                -14f,
                CrownBake);
        }
    }

    private void RememberLeafSway(Transform xform, float amp)
    {
        RememberLeafSway(xform, amp, leafSwayAcc.Count * 0.67f);
    }

    private void RememberLeafSway(Transform xform, float amp, float phase)
    {
        if (xform == null)
            return;
        leafSwayAcc.Add(xform);
        leafSwayAmpAcc.Add(amp);
        leafSwayPhaseAcc.Add(phase);
    }

    // Terminal spray at the twig tip. Needles stay along the twig;
    // willow hang is the curtain; Apple 96 skips extra GO.
    // Spray is a small lance tuft — pack-tip already owns the kind leaf.
    private bool WantsTipCluster()
    {
        if (!growLeaves)
            return false;
        if (maxSegments > 31)
            return false;
        if (barkLook == BarkLook.Scale)
            return false;
        // Lombardy already fills the column; oak spray on top of that
        // is a second tuft on a pole.
        if (barkLook == BarkLook.Lenticel)
            return false;
        if (leafHangWorldDown)
            return false;
        // Pack-tip already sits one tuft at the distal origin.
        // Spray lances on oak lobes read as a second star at 1x.
        if (leafPackTip)
            return false;
        return true;
    }

    // How many nodal tufts for n leaves. 2–3 leaves share an origin so
    // the shoot is not a comb. Spray (AttachTipCluster) is extra.
    private static int LeafNodeCountFor(int n)
    {
        if (n <= 3) return 1;
        if (n <= 5) return 2;
        return 3;
    }

    private void TallyTipCluster(bool paint)
    {
        if (!WantsTipCluster())
            return;
        leafClusterCount++;
        if (!paint)
            LeafCount += 3;
    }

    private void AttachTipCluster(HumanSegment twig, bool farSide, int depth, float heading)
    {
        if (!WantsTipCluster() || twig == null)
            return;

        const int sprayCount = 3;
        Vector2 leaflet = new Vector2(leafSize.x * 0.40f, leafSize.y * 0.52f);
        Color color = LeafTint(9, farSide);
        float y = twig.size.y * 0.46f;
        float sprayRest = LeafClusterRest(heading);
        float sprayHang = (farSide ? -1f : 1f)
            * PictureWidthAt(twig.size.x, 0.96f, depth) * ClusterHangMul();
        float spraySail = PicturePlateSail(
            heading, sprayHang, y, leaflet.x * 0.50f);
        NoteClusterHangSail(spraySail);
        Transform sprayHub = AddFlatPicture(
            twig.transform,
            twig.name + "_SprayHub",
            new Vector2(0.012f, 0.012f),
            color,
            twig.sortingOrder + 2,
            new Vector3(sprayHang, y, 0f),
            sprayRest,
            ArtLibrary.Leaf);
        if (sprayHub != null)
        {
            HidePictureBody(sprayHub);
            RememberLeafSway(sprayHub, 1.55f * spraySail, 3.0f);
        }
        for (int spray = 0; spray < sprayCount; spray++)
        {
            // Unequal fan so the tip is a tuft, not a 12° comb.
            float fan = spray == 0 ? -18f : (spray == 1 ? 6f : 24f);
            Transform sprayParent = sprayHub != null ? sprayHub : twig.transform;
            // Hub origin + fan 6° was a mast through the tuft (oak
            // Dome analog). Centre and laterals all sit off the hub;
            // fallback without a hub uses sprayHang. leafSize / fork 28 stay.
            Vector3 sprayPos;
            if (sprayHub != null)
            {
                // Centre already rides the left pad. Laterals at the
                // hub origin still stacked as a mast (Needle1 analog)
                // once HidePictureBody hid the fan cards. Fan stations
                // sit off the hub; waist stays on each lance.
                // leafSize / fork 28 stay.
                if (spray == 0)
                    sprayPos = new Vector3(-leaflet.x * 0.20f, leaflet.y * 0.04f, 0f);
                else if (spray == 1)
                    sprayPos = new Vector3(-leaflet.x * 0.28f, leaflet.y * 0.08f, 0f);
                else
                    sprayPos = new Vector3(leaflet.x * 0.18f, leaflet.y * 0.10f, 0f);
            }
            else
                sprayPos = new Vector3(sprayHang, y, 0f);
            Transform sprayRoot = AddFlatPicture(
                sprayParent,
                twig.name + "_Spray" + spray,
                leaflet,
                color,
                twig.sortingOrder + 2,
                sprayPos,
                fan,
                ArtLibrary.Leaf);
            if (sprayRoot != null)
            {
                HidePictureBody(sprayRoot);
                // 0.28×0.78 lance was a pine needle on orchard / hedge
                // (fascicle analog). Kind ovate fills the spray envelope;
                // waist on the left pad so the blade is not a stick.
                // leafSize / fork 28 stay.
                bool orchard = ResolvedKind == TreeKind.Apple
                    || ResolvedKind == TreeKind.Plum;
                float lanceW = leaflet.x * (orchard ? 0.52f
                    : (ResolvedKind == TreeKind.Bush ? 0.48f : 0.44f));
                float lanceH = leaflet.y * (ResolvedKind == TreeKind.Bush ? 0.70f : 0.58f);
                // −0.18 × 1.00 outer +0.32 of the lance still crossed
                // the spray origin (ForkFill analog). |x| > half; the
                // right spray (spray==2) rides +X so it does not reach
                // back through the hub. leafSize stays.
                float lanceSign = spray == 2 ? 1f : -1f;
                AddFlatPicture(
                    sprayRoot,
                    sprayRoot.name + "_Lance",
                    new Vector2(lanceW, lanceH),
                    spray == 2 ? OddLeaflet(color) : color,
                    twig.sortingOrder + 2,
                    new Vector3(lanceSign * lanceW * 0.52f, lanceH * 0.12f, 0f),
                    8f,
                    ArtLibrary.Leaf);
                AddFlatPicture(
                    sprayRoot,
                    sprayRoot.name + "_LanceTip",
                    new Vector2(lanceW * 0.42f, lanceH * 0.22f),
                    spray == 2 ? OddLeaflet(color) : color,
                    twig.sortingOrder + 2,
                    // KiteL2Tip closed the flame hem. Spray Lance
                    // without a lip stayed a paddle after PetioleTip
                    // (Cap before CapTip). Hang off the lance;
                    // leafSize stays.
                    new Vector3(lanceSign * lanceW * 0.68f, lanceH * 0.28f, 0f),
                    14f,
                    ArtLibrary.Leaf);
                AddFlatPicture(
                    sprayRoot,
                    sprayRoot.name + "_Waist",
                    new Vector2(lanceW * 0.28f, lanceH * 0.32f),
                    CrownPatch(color, true),
                    twig.sortingOrder + 2,
                    new Vector3(lanceSign * lanceW * 0.52f, -lanceH * 0.06f, 0f),
                    4f,
                    ArtLibrary.Leaf);
                AddFlatPicture(
                    sprayRoot,
                    sprayRoot.name + "_WaistTip",
                    new Vector2(lanceW * 0.14f, lanceH * 0.14f),
                    CrownPatch(color, true),
                    twig.sortingOrder + 2,
                    // LanceTip closed the spray paddle. Spray Waist
                    // without a lip stayed a midrib card (Cap before
                    // CapTip). Hang off Waist; leafSize stays.
                    new Vector3(lanceSign * lanceW * 0.64f, -lanceH * 0.16f, 0f),
                    10f,
                    ArtLibrary.Leaf);
                // Lance+Waist on one lip was a comma after Hide
                // (packed ovate before CheekR). Far egg kisses x=0
                // from the other side. leafSize stays.
                AddFlatPicture(
                    sprayRoot,
                    sprayRoot.name + "_CheekR",
                    new Vector2(lanceW * 0.48f, lanceH * 0.58f),
                    CrownPatch(color, spray != 2),
                    twig.sortingOrder + 2,
                    new Vector3(-lanceSign * lanceW * 0.24f, lanceH * 0.08f, 0f),
                    -8f,
                    ArtLibrary.Leaf);
                AddFlatPicture(
                    sprayRoot,
                    sprayRoot.name + "_CheekRTip",
                    new Vector2(lanceW * 0.22f, lanceH * 0.20f),
                    CrownPatch(color, spray != 2),
                    twig.sortingOrder + 2,
                    // LanceTip closed the spray paddle. Far CheekR
                    // without a lip stayed an egg card after WaistTip
                    // (Drop before DropTip). Hang off CheekR; hub
                    // sinus stays empty. leafSize stays.
                    new Vector3(-lanceSign * lanceW * 0.38f, -lanceH * 0.04f, 0f),
                    -14f,
                    ArtLibrary.Leaf);
                AddFlatPicture(
                    sprayRoot,
                    sprayRoot.name + "_WaistR",
                    new Vector2(lanceW * 0.24f, lanceH * 0.26f),
                    CrownPatch(color, spray == 2),
                    twig.sortingOrder + 2,
                    // Lance+Waist on one lip was still a comma after
                    // CheekR (packed ovate Waist before WaistR). Far
                    // chip rides CheekR. leafSize stays.
                    new Vector3(-lanceSign * lanceW * 0.24f, -lanceH * 0.04f, 0f),
                    -4f,
                    ArtLibrary.Leaf);
                AddFlatPicture(
                    sprayRoot,
                    sprayRoot.name + "_WaistRTip",
                    new Vector2(lanceW * 0.12f, lanceH * 0.12f),
                    CrownPatch(color, spray == 2),
                    twig.sortingOrder + 2,
                    new Vector3(-lanceSign * lanceW * 0.32f, -lanceH * 0.12f, 0f),
                    -10f,
                    ArtLibrary.Leaf);
            }
            LeafCount++;
        }
    }

    // Winter deciduous: a pair of buds at the tip so the leafless twig
    // still reads as a tree, not a stick. Evergreen keeps needles.
    // Picture only — no collider, not a perch.
    private void AttachWinterBuds(HumanSegment twig, bool farSide, int depth)
    {
        if (twig == null || growLeaves)
            return;
        if (ResolvedSeason != PlantSeason.Winter)
            return;
        if (ResolvedKind == TreeKind.Pine || ResolvedKind == TreeKind.Cedar)
            return;

        budCount++;
        if (HeadlessTrial.Active && !HeadlessTrial.VisualCapture)
            return;

        float side0 = farSide ? -1f : 1f;
        float tipW = PictureWidthAt(twig.size.x, 0.92f, depth);
        Color color = Color.Lerp(palTip, palBarkLite, 0.35f);
        for (int i = 0; i < 2; i++)
        {
            float sign = i == 0 ? side0 : -side0;
            float alongBud = i == 0 ? 0.38f : 0.48f;
            // 0.018-tall ticks at ±18° were cards on the tip, not buds.
            // Staggered overlapping boxes sit along the twig. Collider stays.
            Transform bud = AddFlatPicture(
                twig.transform,
                twig.name + "_Bud" + i,
                new Vector2(Mathf.Max(0.012f, tipW * 0.32f), 0.028f),
                color,
                twig.sortingOrder + 1,
                new Vector3(sign * tipW * 0.16f, twig.size.y * alongBud, 0f),
                sign * 48f,
                null);
            if (bud != null)
            {
                AddFlatPicture(
                    bud,
                    bud.name + "_Tip",
                    new Vector2(Mathf.Max(0.008f, tipW * 0.18f), 0.016f),
                    palBarkLite,
                    twig.sortingOrder + 1,
                    // 0.004 sat ON the bud spine (ovate Tip analog).
                    // Peak rides the left of the bud. tipW stays.
                    new Vector3(-Mathf.Max(0.006f, tipW * 0.10f), 0.014f, 0f),
                    sign * 8f,
                    null);
                AddFlatPicture(
                    bud,
                    bud.name + "_TipTip",
                    new Vector2(Mathf.Max(0.006f, tipW * 0.10f), 0.010f),
                    palBarkLite,
                    twig.sortingOrder + 1,
                    // Leaflet TipTip closed the ovate peak. Winter
                    // bud Tip without a lip stayed a scale card after
                    // TwigDomeTip (Cap before CapTip). Hang off Tip;
                    // tipW stays.
                    new Vector3(-Mathf.Max(0.008f, tipW * 0.14f), 0.022f, 0f),
                    sign * 12f,
                    null);
                AddFlatPicture(
                    bud,
                    bud.name + "_TipR",
                    new Vector2(Mathf.Max(0.008f, tipW * 0.16f), 0.014f),
                    palBarkLite,
                    twig.sortingOrder + 1,
                    // Left-only Tip was a one-tent winter bud after
                    // summer leaflet TipR. Far peak rides the bud
                    // right. tipW stays.
                    new Vector3(Mathf.Max(0.006f, tipW * 0.10f), 0.012f, 0f),
                    -sign * 8f,
                    null);
                AddFlatPicture(
                    bud,
                    bud.name + "_TipRTip",
                    new Vector2(Mathf.Max(0.006f, tipW * 0.09f), 0.009f),
                    palBarkLite,
                    twig.sortingOrder + 1,
                    // TipTip closed the near winter peak. Bud TipR
                    // without a lip stayed a far scale after
                    // TwigDomeTip (Drop before DropTip). Hang off
                    // TipR; tipW stays.
                    new Vector3(Mathf.Max(0.008f, tipW * 0.14f), 0.020f, 0f),
                    -sign * 12f,
                    null);
            }
        }
    }

    // Picture-only cone on a Transform (internodal spur). Same
    // language as PaintVisualConeFill on wood HumanSegment. Hidden
    // parent box + tip core + two edges. Children (tuft) ride this
    // cache — LateUpdate does not Find. visualTaper 0.58 stays on
    // the grow wood. Not a force.
    private void PaintPictureCone(
        Transform parent,
        float width,
        float length,
        Color color,
        float tipScale,
        int sortingOrder)
    {
        if (parent == null)
            return;
        if (HeadlessTrial.Active && !HeadlessTrial.VisualCapture)
            return;
        if (width < 0.006f || length < 0.008f)
            return;
        HidePictureBody(parent);
        float tip = Mathf.Clamp(tipScale, 0.40f, 0.92f);
        float tipW = Mathf.Max(0.008f, width * tip);
        // Visual 19:46 leftover: internodal Cone 0.52 still a
        // telegraph post after ConeTip (grow ConeCore analog).
        // Shorter core; edges own the taper. visualTaper 0.58 stays.
        float coreH = length * 0.40f;
        float coreY = -length * 0.12f;
        AddFlatPicture(
            parent,
            parent.name + "_Cone",
            new Vector2(tipW, coreH),
            color,
            sortingOrder,
            new Vector3(0f, coreY, 0f),
            0f,
            ArtLibrary.Bark);
        // Artist 19:32 short core left ConeTip past a 0.52 post.
        // Core is 0.40 now; hang the lip off that distal.
        // visualTaper 0.58 stays on the grow wood.
        float coreDistal = coreY + coreH * 0.5f;
        AddFlatPicture(
            parent,
            parent.name + "_ConeTip",
            new Vector2(
                Mathf.Max(0.006f, tipW * 0.55f),
                Mathf.Max(0.006f, length * 0.12f)),
            color,
            sortingOrder,
            new Vector3(0f, coreDistal + length * 0.06f, 0f),
            0f,
            ArtLibrary.Bark);
        float halfDrop = (width - tipW) * 0.5f;
        if (halfDrop < 0.003f)
            return;
        float edgeLen = Mathf.Sqrt(halfDrop * halfDrop + length * length);
        float rot = Mathf.Atan2(halfDrop, length) * Mathf.Rad2Deg;
        float edgeW = Mathf.Max(0.008f, halfDrop * 1.15f);
        float x = (width + tipW) * 0.25f;
        AddFlatPicture(
            parent,
            parent.name + "_ConeL",
            new Vector2(edgeW, edgeLen),
            color,
            sortingOrder,
            new Vector3(-x, 0f, 0f),
            -rot,
            ArtLibrary.Bark);
        AddFlatPicture(
            parent,
            parent.name + "_ConeR",
            new Vector2(edgeW, edgeLen),
            color,
            sortingOrder,
            new Vector3(x, 0f, 0f),
            rot,
            ArtLibrary.Bark);
        // Edge wedges without a distal chip stayed a telegraph stick
        // after RootButtTip (bole Furrow before CorkTip). Hang off the
        // outer end; visualTaper 0.58 stays on the grow wood.
        AddFlatPicture(
            parent,
            parent.name + "_ConeLTip",
            new Vector2(edgeW * 0.55f, edgeLen * 0.18f),
            color,
            sortingOrder,
            new Vector3(-x - edgeW * 0.10f, length * 0.38f, 0f),
            -rot - 10f,
            ArtLibrary.Bark);
        AddFlatPicture(
            parent,
            parent.name + "_ConeRTip",
            new Vector2(edgeW * 0.50f, edgeLen * 0.16f),
            color,
            sortingOrder,
            new Vector3(x + edgeW * 0.08f, length * 0.36f, 0f),
            rot + 9f,
            ArtLibrary.Bark);
    }

    // Grow-time only. Turns off the collider-sized paddle so overlapping
    // boxes own the silhouette. Not a FixedUpdate path.
    private static void HidePictureBody(Transform t)
    {
        if (t == null)
            return;
        SpriteRenderer sr = t.GetComponent<SpriteRenderer>();
        if (sr != null)
            sr.enabled = false;
    }

    private static Transform AddFlatPicture(
        Transform parent,
        string name,
        Vector2 size,
        Color color,
        int sortingOrder,
        Vector3 localPos,
        float zRot,
        string albedoKey)
    {
        if (parent == null)
            return null;
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            sr,
            size,
            HumanVisualShape.Box,
            albedoKey,
            color,
            sortingOrder);
        go.transform.localPosition = localPos;
        if (zRot != 0f)
            go.transform.localRotation = Quaternion.Euler(0f, 0f, zRot);
        return go.transform;
    }

    public static void PaintVisualTaper(
        HumanSegment seg,
        float width,
        float length,
        Color color,
        float tipScale)
    {
        PaintVisualTaper(seg, width, length, color, tipScale, 3);
    }

    public static void PaintVisualTaper(
        HumanSegment seg,
        float width,
        float length,
        Color color,
        float tipScale,
        int bands)
    {
        PaintVisualTaper(seg, width, length, color, tipScale, bands, 1f);
    }

    public static void PaintVisualTaper(
        HumanSegment seg,
        float width,
        float length,
        Color color,
        float tipScale,
        int bands,
        float baseScale)
    {
        if (seg == null || seg.visual == null)
            return;
        if (HeadlessTrial.Active && !HeadlessTrial.VisualCapture)
            return;
        if (width < 0.004f || length < 0.004f)
            return;

        if (seg.sprite != null)
            seg.sprite.enabled = false;

        int nBand = Mathf.Clamp(bands, 1, 6);
        float tip = Mathf.Clamp(tipScale, 0.35f, 1f);
        float base01 = Mathf.Clamp(baseScale, 0.35f, 1f);
        // One band is a trapezoid (core at tip width + two cone edges),
        // not a mid-width post and not stacked stairs.
        if (nBand == 1)
        {
            PaintVisualConeFill(seg, width, length, color, tip, base01);
            return;
        }
        float bandH = length / nBand;
        // Two bands need a deep overlap or they seam as a ladder at 1x.
        float overlap = nBand <= 2 ? 1.42f : 1.18f;
        for (int band = 0; band < nBand; band++)
        {
            float along = (band + 0.5f) / nBand;
            float w = Mathf.Lerp(width * base01, width * tip, along);
            GameObject bandObject = new GameObject("Taper" + band);
            bandObject.transform.SetParent(seg.visual, false);
            SpriteRenderer sr = bandObject.AddComponent<SpriteRenderer>();
            HumanSegment.ApplyPicture(
                sr,
                new Vector2(w, bandH * overlap),
                HumanVisualShape.Box,
                ArtLibrary.Bark,
                color,
                seg.sortingOrder);
            bandObject.transform.localPosition = new Vector3(0f, (along - 0.5f) * length, 0f);
        }
        // Last-band stair without a distal chip stayed a telegraph
        // stack after ConeCoreTip (Furrow before CorkTip). Hang off
        // +Y; collider / visualTaper 0.58 stay.
        float tipAlong = 0.88f;
        float wTip = Mathf.Lerp(width * base01, width * tip, tipAlong);
        GameObject taperTip = new GameObject("TaperTip");
        taperTip.transform.SetParent(seg.visual, false);
        SpriteRenderer tipSr = taperTip.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            tipSr,
            new Vector2(Mathf.Max(0.006f, wTip * 0.55f), bandH * 0.42f),
            HumanVisualShape.Box,
            ArtLibrary.Bark,
            color,
            seg.sortingOrder);
        taperTip.transform.localPosition = new Vector3(0f, (tipAlong - 0.5f) * length, 0f);
    }

    // Sprite along +Y. Unity Z is CCW, so +Y → (x,y) is Atan2(-x, y).
    // Picture only. Garden leaflets and tree tuft petioles share this
    // so a 22° tick is not a second join. Colliders stay.
    public static void AimPictureAlongY(Transform picture, Vector3 localTarget, float along = 0.38f)
    {
        if (picture == null)
            return;
        float reach = Mathf.Sqrt(localTarget.x * localTarget.x + localTarget.y * localTarget.y);
        if (reach < 0.004f)
            return;
        picture.localPosition = localTarget * along;
        picture.localRotation = Quaternion.Euler(
            0f, 0f, Mathf.Atan2(-localTarget.x, localTarget.y) * Mathf.Rad2Deg);
    }

    // Tip-width core plus two edge wedges. Same colour, so the silhouette
    // is a cone, not a barcode of Taper0/Taper1. Collider stays.
    private static void PaintVisualConeFill(
        HumanSegment seg,
        float width,
        float length,
        Color color,
        float tipScale,
        float baseScale)
    {
        float baseW = width * baseScale;
        float tipW = width * tipScale;
        GameObject core = new GameObject("ConeCore");
        core.transform.SetParent(seg.visual, false);
        SpriteRenderer coreSr = core.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            coreSr,
            new Vector2(Mathf.Max(0.008f, tipW), length),
            HumanVisualShape.Box,
            ArtLibrary.Bark,
            color,
            seg.sortingOrder);
        // Spur ConeLTip closed the internodal telegraph. Grow ConeCore
        // without a distal chip stayed a tip-width post (Furrow before
        // CorkTip). Hang off +Y; collider / visualTaper 0.58 stay.
        GameObject coreTip = new GameObject("ConeCoreTip");
        coreTip.transform.SetParent(seg.visual, false);
        SpriteRenderer coreTipSr = coreTip.AddComponent<SpriteRenderer>();
        HumanSegment.ApplyPicture(
            coreTipSr,
            new Vector2(
                Mathf.Max(0.006f, tipW * 0.55f),
                Mathf.Max(0.006f, length * 0.12f)),
            HumanVisualShape.Box,
            ArtLibrary.Bark,
            color,
            seg.sortingOrder);
        coreTip.transform.localPosition = new Vector3(0f, length * 0.42f, 0f);

        float halfDrop = (baseW - tipW) * 0.5f;
        if (halfDrop < 0.004f || length < 0.004f)
            return;

        float edgeLen = Mathf.Sqrt(halfDrop * halfDrop + length * length);
        float rot = Mathf.Atan2(halfDrop, length) * Mathf.Rad2Deg;
        // Overlap the core so the silhouette is a closed cone, not a
        // tip-width post with two detached wedges.
        float edgeW = Mathf.Max(0.010f, halfDrop * 1.15f);
        float x = (baseW + tipW) * 0.25f;
        for (int side = 0; side < 2; side++)
        {
            float sign = side == 0 ? -1f : 1f;
            GameObject edge = new GameObject(side == 0 ? "ConeEdgeL" : "ConeEdgeR");
            edge.transform.SetParent(seg.visual, false);
            SpriteRenderer edgeSr = edge.AddComponent<SpriteRenderer>();
            HumanSegment.ApplyPicture(
                edgeSr,
                new Vector2(edgeW, edgeLen),
                HumanVisualShape.Box,
                ArtLibrary.Bark,
                color,
                seg.sortingOrder);
            edge.transform.localPosition = new Vector3(sign * x, 0f, 0f);
            edge.transform.localRotation = Quaternion.Euler(0f, 0f, sign * rot);
            GameObject edgeTip = new GameObject(side == 0 ? "ConeEdgeLTip" : "ConeEdgeRTip");
            edgeTip.transform.SetParent(seg.visual, false);
            SpriteRenderer edgeTipSr = edgeTip.AddComponent<SpriteRenderer>();
            HumanSegment.ApplyPicture(
                edgeTipSr,
                new Vector2(
                    Mathf.Max(0.006f, edgeW * 0.52f),
                    Mathf.Max(0.006f, edgeLen * 0.16f)),
                HumanVisualShape.Box,
                ArtLibrary.Bark,
                color,
                seg.sortingOrder);
            edgeTip.transform.localPosition = new Vector3(
                sign * (x + edgeW * 0.10f),
                length * 0.38f,
                0f);
            edgeTip.transform.localRotation = Quaternion.Euler(
                0f, 0f, sign * rot + sign * 10f);
        }
    }

    private static bool IsLookDecor(Transform child, Transform visual)
    {
        if (child == null || child == visual)
            return false;
        string name = child.name;
        return name.IndexOf("Leaf", System.StringComparison.Ordinal) >= 0
            || name.IndexOf("Fruit", System.StringComparison.Ordinal) >= 0
            || name.IndexOf("Bloom", System.StringComparison.Ordinal) >= 0
            || name.IndexOf("Crown", System.StringComparison.Ordinal) >= 0
            || name.IndexOf("Fork", System.StringComparison.Ordinal) >= 0
            || name.IndexOf("Spray", System.StringComparison.Ordinal) >= 0
            || name.IndexOf("Bud", System.StringComparison.Ordinal) >= 0
            || name.IndexOf("Raceme", System.StringComparison.Ordinal) >= 0
            || name.IndexOf("Catkin", System.StringComparison.Ordinal) >= 0
            || name.IndexOf("Spur", System.StringComparison.Ordinal) >= 0;
    }

    private static LookSprite MakeLookSprite(Transform xform, int nodeIndex, float track)
    {
        Vector3 pos = xform.localPosition;
        return new LookSprite
        {
            xform = xform,
            nodeIndex = nodeIndex,
            restLocalPos = pos,
            restLocalRot = xform.localRotation,
            track = track
        };
    }

    private static Vector2 TipLocal(HumanSegment segment, float headingDeg)
    {
        return (Vector2)segment.transform.localPosition
            + HeadingDir(headingDeg) * (segment.size.y * 0.5f);
    }

    private float NextJitter()
    {
        if (forkJitter <= 0.01f || rng == null) return 0f;
        return (float)(rng.NextDouble() * 2.0 - 1.0) * forkJitter;
    }

    private int IndexOf(HumanSegment segment)
    {
        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodes[i].segment == segment)
                return i;
        }
        return -1;
    }

    private void MarkWoodAsPerch()
    {
        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodes[i].segment != null)
                GroundLayers.Apply(nodes[i].segment.gameObject);
        }
    }

    // Hold torque = minus the gravity moment of this segment and its
    // descendants about the hinge, taken at the grow pose. Extra mass
    // (a bird) is not in the sum, so the spring still bends.
    private void BindGravityHold()
    {
        if (rig != TreeRig.Sway) return;
        Vector2 g = Physics2D.gravity;
        for (int i = 0; i < nodes.Count; i++)
        {
            Node n = nodes[i];
            if (!n.dynamic || n.segment == null || n.segment.joint == null)
                continue;
            TreeSpring spring = n.spring;
            if (spring == null) continue;

            // worldCenterOfMass is not ready on the build frame; the box
            // center is the transform we just placed.
            Vector2 jointWorld = n.segment.transform.TransformPoint(
                new Vector2(0f, -n.segment.size.y * 0.5f));
            float moment = 0f;
            for (int j = 0; j < nodes.Count; j++)
            {
                if (!IsDescendantOrSelf(j, i)) continue;
                HumanSegment seg = nodes[j].segment;
                if (seg == null || seg.rb == null) continue;
                Vector2 com = seg.transform.position;
                moment += (com.x - jointWorld.x) * seg.rb.mass * g.y;
            }

            spring.holdTorque = -moment;
            spring.maxTorque = Mathf.Max(spring.maxTorque, Mathf.Abs(moment) * 0.5f + 16f);
            // Lock rest to the angle Unity reports after SyncTransforms,
            // not assumed 0 — a pre-rotated fork can spawn with a bias.
            spring.restAngle = n.segment.joint.jointAngle;
        }
    }

    private bool IsDescendantOrSelf(int nodeIndex, int ancestorIndex)
    {
        int walk = nodeIndex;
        int guard = 0;
        while (walk >= 0 && guard++ < nodes.Count)
        {
            if (walk == ancestorIndex) return true;
            walk = nodes[walk].parentIndex;
        }
        return false;
    }

    private static Bounds SegmentWorldBounds(HumanSegment segment)
    {
        if (segment == null)
            return new Bounds();
        if (segment.collider != null)
            return segment.collider.bounds;

        // Same AABB BoxCollider2D would report for an unscaled box.
        Vector2 size = segment.size;
        Vector3 hx = segment.transform.TransformVector(new Vector3(size.x * 0.5f, 0f, 0f));
        Vector3 hy = segment.transform.TransformVector(new Vector3(0f, size.y * 0.5f, 0f));
        Vector3 extents = new Vector3(
            Mathf.Abs(hx.x) + Mathf.Abs(hy.x),
            Mathf.Abs(hx.y) + Mathf.Abs(hy.y),
            0f);
        return new Bounds(segment.transform.position, extents * 2f);
    }

    // along 0 = parent joint (bend), 1 = distal tip (end).
    private static Vector2 WoodAlong(HumanSegment wood, float along)
    {
        return (Vector2)wood.transform.TransformPoint(
            new Vector3(0f, (along - 0.5f) * wood.size.y, 0f));
    }

    private void AddApplePerch(Node n, float along)
    {
        Vector2 p = WoodAlong(n.segment, along);
        perchAcc.Add(new PerchPoint
        {
            depth = n.depth,
            y = p.y,
            p = p,
            wood = n.segment
        });
    }

    private void CollectPerchAndBounds()
    {
        perchAcc.Clear();
        perchBoundsValid = false;
        for (int i = 0; i < nodes.Count; i++)
        {
            Node n = nodes[i];
            if (n.segment == null)
                continue;
            Bounds b = SegmentWorldBounds(n.segment);
            if (!perchBoundsValid)
            {
                perchBounds = b;
                perchBoundsValid = true;
            }
            else
                perchBounds.Encapsulate(b);

            if (n.depth < 0)
                continue;
            if (woodAxisPerch)
            {
                // Wood axis: tip = end or fork crook (child base shares it),
                // mid-span = sit on the limb bend. AABB top is not the twig.
                AddApplePerch(n, 1f);
                if (n.depth >= 1)
                    AddApplePerch(n, 0.5f);
                if (n.depth >= 2)
                    AddApplePerch(n, 0.78f);
                continue;
            }

            // Trunk is a sit, but a flock of 50 must fill twigs first.
            int points = n.depth >= 2 ? 3 : n.depth >= 1 ? 2 : 1;
            for (int k = 0; k < points; k++)
            {
                float t = points == 1 ? 0.5f : (k + 0.5f) / points;
                perchAcc.Add(new PerchPoint
                {
                    depth = n.depth,
                    y = b.max.y,
                    p = new Vector2(Mathf.Lerp(b.min.x, b.max.x, t), b.max.y),
                    wood = n.segment
                });
            }
        }

        perchAcc.Sort((a, b) =>
        {
            int byDepth = b.depth.CompareTo(a.depth);
            if (byDepth != 0) return byDepth;
            return b.y.CompareTo(a.y);
        });

        perchSlots = new Vector2[perchAcc.Count];
        for (int i = 0; i < perchAcc.Count; i++)
            perchSlots[i] = perchAcc[i].p;
    }

    private void TallyBranchSpread()
    {
        float peak = 0f;
        for (int nodeI = 0; nodeI < nodes.Count; nodeI++)
        {
            if (nodes[nodeI].depth < 1)
                continue;
            float absH = Mathf.Abs(nodes[nodeI].heading);
            if (absH > peak)
                peak = absH;
        }
        MaxAbsBranchHeading = peak;
    }

    private void TallyBodies()
    {
        if (rig == TreeRig.Sway)
        {
            // Stump is Static and not in DynamicBodyCount.
            RigidbodyCount = DynamicBodyCount + 1;
            WoodColliderCount = RigidbodyCount;
        }
        else
        {
            RigidbodyCount = 0;
            WoodColliderCount = 0;
        }
        PerchPadCount = 0;
    }

    private void RegisterLive()
    {
        if (liveRegistered)
            return;
        Live.Add(this);
        liveRegistered = true;
    }

    private bool UsesLookSwayKind()
    {
        return ResolvedKind == TreeKind.Apple
            || ResolvedKind == TreeKind.Lilac
            || ResolvedKind == TreeKind.Hazel
            || ResolvedKind == TreeKind.Cedar
            || ResolvedKind == TreeKind.Plum;
    }

    private bool UsesDensePerch()
    {
        return UsesLookSwayKind();
    }
}
