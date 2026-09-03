using System.Collections.Generic;
using UnityEngine;

public enum TreeRig
{
    // Recursive hinges: trunk and forks sway. Scene Tree, not a grove.
    Sway = 0,
    // Picture only: no Rigidbody2D, no wood collider. Sit pads stay.
    Static = 1
}

// Growth recipe. Oak is the hold/perch canon. Auto picks 0…4 from the seed.
public enum TreeKind
{
    Oak = 0,
    Pine = 1,
    Willow = 2,
    Bush = 3,
    Poplar = 4,
    Auto = 5
}

// Recursive tree build. Plant layer, not human and not level.
// Class is not Tree: Unity has UnityEngine.Tree (Terrain).
// A segment is HumanSegment: box + Visual, no human anatomy.
// No muscles: wood is TreeSpring + JointFriction. Wind is a force on the segment.
public class PlantTree : MonoBehaviour
{
    [Header("Сборка")]
    public TreeRig rig = TreeRig.Sway;
    [Tooltip("Oak is the stand canon. Auto = seed % 5.")]
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

    [Header("Leaves — picture only")]
    public bool growLeaves = true;
    public Vector2 leafSize = new Vector2(0.10f, 0.14f);

    [Header("Picture")]
    public float jointMarkerDiameter = 0.025f;

    public int SegmentCount { get; private set; }
    public int DynamicBodyCount { get; private set; }
    public int RigidbodyCount { get; private set; }
    public int WoodColliderCount { get; private set; }
    public int PerchPadCount { get; private set; }
    public TreeKind ResolvedKind { get; private set; }

    private Color barkStump = new Color(0.36f, 0.24f, 0.14f, 1f);
    private Color barkTrunk = new Color(0.42f, 0.28f, 0.16f, 1f);
    private Color barkTwig = new Color(0.50f, 0.34f, 0.18f, 1f);
    private Color leafColor = new Color(0.28f, 0.52f, 0.24f, 1f);
    private float leaderForkShare = 0.28f;
    private float sideLengthScale = 1f;
    private bool skipSideOnEvenDepth;
    private bool bothSidesAtRoot;

    private readonly List<Node> nodes = new List<Node>(16);
    private static readonly List<PlantTree> Live = new List<PlantTree>(4);
    private Bounds perchBounds;
    private bool perchBoundsValid;
    private bool birdPerchesReady;
    public const int MaxBirdPerches = 20;
    private System.Random rng;
    private int growDepthCap;
    private const float AirDensity = 1.2f;

    private struct Node
    {
        public HumanSegment segment;
        public float heading;
        public int depth;
        public bool dynamic;
        public int parentIndex;
    }

    private static Color DimFar(Color c)
    {
        return new Color(c.r * 0.62f, c.g * 0.62f, c.b * 0.62f, 1f);
    }

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

    public static TreeKind ParseKind(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return TreeKind.Oak;
        switch (raw.Trim().ToLowerInvariant())
        {
            case "pine": return TreeKind.Pine;
            case "willow": return TreeKind.Willow;
            case "bush": return TreeKind.Bush;
            case "poplar": return TreeKind.Poplar;
            case "auto": return TreeKind.Auto;
            default: return TreeKind.Oak;
        }
    }

    public void BuildTree()
    {
        foreach (Transform child in transform)
            Destroy(child.gameObject);
        TreeDriver oldDriver = GetComponent<TreeDriver>();
        if (oldDriver != null)
            Destroy(oldDriver);

        nodes.Clear();
        SegmentCount = 0;
        DynamicBodyCount = 0;
        ResolvedKind = kind == TreeKind.Auto ? KindFromPlantIndex(seed) : kind;
        if ((int)ResolvedKind < 0 || (int)ResolvedKind > 4)
            ResolvedKind = TreeKind.Oak;
        ApplyKind(ResolvedKind);
        rng = new System.Random(seed);
        bool live = rig == TreeRig.Sway;
        // Sway is one Human of bodies. Static is a picture: hundreds of
        // twigs must not become Box2D fixtures.
        growDepthCap = Mathf.Clamp(maxDepth, 1, live ? 5 : 8);
        int cap = Mathf.Clamp(maxSegments, 3, live ? 31 : 255);

        HumanSegment stump = CreateWood(
            "Stump",
            new Vector2(stumpWidth, stumpHeight),
            woodDensity * stumpWidth * stumpWidth * stumpHeight,
            barkStump,
            0,
            live,
            false);
        stump.transform.localPosition = new Vector3(0f, stumpHeight * 0.5f, 0f);
        stump.transform.localRotation = Quaternion.identity;
        if (stump.rb != null)
            stump.rb.bodyType = RigidbodyType2D.Static;
        nodes.Add(new Node { segment = stump, heading = 0f, depth = -1, dynamic = false, parentIndex = -1 });

        Grow(stump, 0f, 0, trunkLength, trunkWidth, 0f, cap);

        if (normalizeMass)
            NormalizeDynamicMass();

        if (live)
        {
            DisableCollisionsBetweenSegments();
            MarkWoodAsPerch();
        }
        Physics2D.SyncTransforms();
        BindGravityHold();
        RebuildPerchBounds();
        RecountBodies();
        RegisterLive();

        if (rig == TreeRig.Sway)
        {
            TreeDriver driver = gameObject.AddComponent<TreeDriver>();
            driver.RebuildCache(this);
        }
    }

    void OnDestroy()
    {
        Live.Remove(this);
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
        List<(int depth, float y, Vector2 p)> acc = new List<(int, float, Vector2)>(nodes.Count * 2);
        for (int i = 0; i < nodes.Count; i++)
        {
            Node n = nodes[i];
            if (n.segment == null || n.depth < 0)
                continue;
            // Trunk is a sit, but a flock of 50 must fill twigs first.
            Bounds b = SegmentWorldBounds(n.segment);
            int points = n.depth >= 2 ? 3 : n.depth >= 1 ? 2 : 1;
            for (int k = 0; k < points; k++)
            {
                float t = points == 1 ? 0.5f : (k + 0.5f) / points;
                acc.Add((n.depth, b.max.y, new Vector2(Mathf.Lerp(b.min.x, b.max.x, t), b.max.y)));
            }
        }

        acc.Sort((a, b) =>
        {
            int byDepth = b.depth.CompareTo(a.depth);
            if (byDepth != 0) return byDepth;
            return b.y.CompareTo(a.y);
        });

        Vector2[] slots = new Vector2[acc.Count];
        for (int i = 0; i < acc.Count; i++)
            slots[i] = acc[i].p;
        return slots;
    }

    // Thin Ground pads at the sit points. Wood stays ghost to birds.
    public void EnsureBirdPerches()
    {
        if (birdPerchesReady)
            return;
        birdPerchesReady = true;

        List<(int depth, float y, Vector2 p, HumanSegment wood)> acc =
            new List<(int, float, Vector2, HumanSegment)>(nodes.Count * 2);
        for (int i = 0; i < nodes.Count; i++)
        {
            Node n = nodes[i];
            if (n.segment == null || n.depth < 0)
                continue;
            Bounds b = SegmentWorldBounds(n.segment);
            int points = n.depth >= 2 ? 3 : n.depth >= 1 ? 2 : 1;
            for (int k = 0; k < points; k++)
            {
                float t = points == 1 ? 0.5f : (k + 0.5f) / points;
                Vector2 world = new Vector2(Mathf.Lerp(b.min.x, b.max.x, t), b.max.y);
                acc.Add((n.depth, b.max.y, world, n.segment));
            }
        }

        acc.Sort((a, b) =>
        {
            int byDepth = b.depth.CompareTo(a.depth);
            if (byDepth != 0) return byDepth;
            return b.y.CompareTo(a.y);
        });

        int nPad = Mathf.Min(MaxBirdPerches, acc.Count);
        for (int i = 0; i < nPad; i++)
        {
            HumanSegment wood = acc[i].wood;
            if (wood == null)
                continue;
            GameObject pad = new GameObject("BirdPerch");
            pad.AddComponent<BirdPerch>();
            pad.transform.SetParent(wood.transform, true);
            Vector2 world = acc[i].p;
            pad.transform.position = new Vector3(world.x, world.y - 0.012f, 0f);
            BoxCollider2D col = pad.AddComponent<BoxCollider2D>();
            col.size = new Vector2(0.12f, 0.024f);
            GroundLayers.Apply(pad);
        }

        RecountBodies();
    }

    public float GetMaxAbsJointAngle()
    {
        float max = 0f;
        for (int i = 0; i < nodes.Count; i++)
        {
            HumanSegment seg = nodes[i].segment;
            if (seg == null || seg.joint == null) continue;
            TreeSpring spring = seg.GetComponent<TreeSpring>();
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
            TreeSpring spring = seg.GetComponent<TreeSpring>();
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
        barkStump = new Color(0.36f, 0.24f, 0.14f, 1f);
        barkTrunk = new Color(0.42f, 0.28f, 0.16f, 1f);
        barkTwig = new Color(0.50f, 0.34f, 0.18f, 1f);
        leafColor = new Color(0.28f, 0.52f, 0.24f, 1f);
        leafSize = new Vector2(0.10f, 0.14f);
        maxDepth = 3;
        maxSegments = 15;
        trunkLength = 0.82f;
        trunkWidth = 0.16f;
        stumpHeight = 0.22f;
        stumpWidth = 0.20f;
        lengthDecay = 0.72f;
        widthDecay = 0.65f;
        forkAngle = 28f;
        forkJitter = 4f;
        totalMass = 28f;

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
            leafSize = new Vector2(0.06f, 0.10f);
            barkStump = new Color(0.22f, 0.16f, 0.12f, 1f);
            barkTrunk = new Color(0.28f, 0.20f, 0.14f, 1f);
            barkTwig = new Color(0.32f, 0.24f, 0.14f, 1f);
            leafColor = new Color(0.16f, 0.38f, 0.22f, 1f);
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
            leafSize = new Vector2(0.08f, 0.18f);
            barkStump = new Color(0.34f, 0.28f, 0.16f, 1f);
            barkTrunk = new Color(0.40f, 0.36f, 0.18f, 1f);
            barkTwig = new Color(0.46f, 0.42f, 0.20f, 1f);
            leafColor = new Color(0.42f, 0.58f, 0.22f, 1f);
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
            leafSize = new Vector2(0.12f, 0.12f);
            barkStump = new Color(0.40f, 0.22f, 0.12f, 1f);
            barkTrunk = new Color(0.48f, 0.28f, 0.14f, 1f);
            barkTwig = new Color(0.56f, 0.34f, 0.16f, 1f);
            leafColor = new Color(0.34f, 0.58f, 0.20f, 1f);
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
            leafSize = new Vector2(0.07f, 0.09f);
            barkStump = new Color(0.38f, 0.36f, 0.32f, 1f);
            barkTrunk = new Color(0.46f, 0.44f, 0.40f, 1f);
            barkTwig = new Color(0.54f, 0.52f, 0.46f, 1f);
            leafColor = new Color(0.30f, 0.56f, 0.32f, 1f);
        }
    }

    // Sail: ½ ρ v² S, only in FixedUpdate through TreeDriver.
    public void ApplyWind()
    {
        if (rig != TreeRig.Sway) return;
        if (windScale <= 0f) return;

        float gust = 1f;
        if (windPeriod > 0.05f)
            gust += windGust * Mathf.Sin(Time.time * (2f * Mathf.PI / windPeriod));
        float v = windSpeed * gust;
        if (Mathf.Abs(v) < 0.01f) return;

        float dynamicPressure = 0.5f * AirDensity * v * Mathf.Abs(v) * windScale;
        Vector2 dir = new Vector2(Mathf.Sign(v), 0f);

        for (int i = 0; i < nodes.Count; i++)
        {
            Node n = nodes[i];
            if (!n.dynamic || n.segment == null || n.segment.rb == null)
                continue;
            float area = n.segment.size.x * n.segment.size.y;
            n.segment.rb.AddForce(dir * (dynamicPressure * area), ForceMode2D.Force);
        }
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
        Color bark = Color.Lerp(barkTrunk, barkTwig, depth / Mathf.Max(1f, (float)growDepthCap));
        if (heading < parentHeading - 0.01f)
            bark = DimFar(bark);

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

        if (isDynamic)
        {
            seg.ConnectTo(
                parent,
                new Vector2(0f, -length * 0.5f),
                new Vector2(0f, parent.size.y * 0.5f),
                -BendLimit(width),
                BendLimit(width));
            AddWoodActuators(seg, width, length, heading);
            DynamicBodyCount++;
        }
        else if (seg.rb != null)
        {
            seg.rb.bodyType = RigidbodyType2D.Static;
        }

        nodes.Add(new Node
        {
            segment = seg,
            heading = heading,
            depth = depth,
            dynamic = isDynamic,
            parentIndex = IndexOf(parent)
        });
        SegmentCount++;

        if (depth >= growDepthCap)
        {
            AttachLeaves(seg, heading < parentHeading);
            return;
        }

        float jitter = NextJitter();
        float side = (depth % 2 == 0) ? 1f : -1f;
        float nextLen = length * lengthDecay;
        float nextWid = width * widthDecay;
        // Leader near the axis, side takes the full fork. Alternate the
        // side so the crown does not list and CoM stays over the stump.
        Grow(seg, heading, depth + 1, nextLen, nextWid,
            heading + forkAngle * leaderForkShare + jitter * 0.25f, cap);

        bool growSide = !skipSideOnEvenDepth || (depth % 2 == 1);
        if (growSide)
        {
            float sideLen = nextLen * sideLengthScale;
            Grow(seg, heading, depth + 1, sideLen, nextWid,
                heading - side * (forkAngle + jitter), cap);
            if (bothSidesAtRoot && depth == 0)
            {
                Grow(seg, heading, depth + 1, sideLen, nextWid,
                    heading + side * (forkAngle + jitter * 0.4f), cap);
            }
        }
    }

    private void AttachLeaves(HumanSegment twig, bool farSide)
    {
        if (!growLeaves || HeadlessTrial.Active) return;

        Color color = farSide ? DimFar(leafColor) : leafColor;
        float[] sides = { -1f, 1f };
        for (int i = 0; i < sides.Length; i++)
        {
            float side = sides[i];
            GameObject leafObject = new GameObject(twig.name + "_Leaf" + i);
            HumanSegment leaf = leafObject.AddComponent<HumanSegment>();
            leaf.InitializeVisualOnly(
                leafObject.name,
                leafSize,
                color,
                twig.transform,
                twig.sortingOrder + 2,
                HumanVisualShape.Ellipse,
                ArtLibrary.Leaf);
            leaf.transform.localPosition = new Vector3(
                side * twig.size.x * 0.35f,
                twig.size.y * 0.42f,
                0f);
            leaf.transform.localRotation = Quaternion.Euler(0f, 0f, side * 38f);
        }
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
                HumanVisualShape.Capsule,
                ArtLibrary.Bark);
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
                HumanVisualShape.Capsule,
                ArtLibrary.Bark);
        }
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
        spring.worldStiffness = Mathf.Lerp(55f, 280f, thick) * lengthScale;
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
            TreeSpring spring = n.segment.GetComponent<TreeSpring>();
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

    private void RebuildPerchBounds()
    {
        perchBoundsValid = false;
        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodes[i].segment == null)
                continue;
            Bounds b = SegmentWorldBounds(nodes[i].segment);
            if (!perchBoundsValid)
            {
                perchBounds = b;
                perchBoundsValid = true;
            }
            else
                perchBounds.Encapsulate(b);
        }
    }

    private void RecountBodies()
    {
        Rigidbody2D[] bodies = GetComponentsInChildren<Rigidbody2D>(true);
        RigidbodyCount = bodies != null ? bodies.Length : 0;
        WoodColliderCount = 0;
        PerchPadCount = 0;
        Collider2D[] cols = GetComponentsInChildren<Collider2D>(true);
        if (cols == null)
            return;
        for (int i = 0; i < cols.Length; i++)
        {
            if (cols[i] == null)
                continue;
            if (cols[i].GetComponent<BirdPerch>() != null)
                PerchPadCount++;
            else
                WoodColliderCount++;
        }
    }

    private void RegisterLive()
    {
        if (!Live.Contains(this))
            Live.Add(this);
    }
}
