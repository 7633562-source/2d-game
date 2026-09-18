using System.Collections.Generic;
using UnityEngine;

// A plant with a trunk and recursive branches on hinges. No muscles: wood is
// a TreeSpring torsion hold (cancels self-weight at the grow pose) plus
// JointFriction damping. Wind is a ½ρv² force on each segment. This class
// knows nothing about fruit or leaves — a subclass decorates the wood with
// picture-only ornaments (see AppleTree). The stump is static: the tree
// sways, it does not fall like a human ragdoll.
public abstract class TreeBase : PlantBase, IWoodSensorSource
{
    [Header("Build")]
    [Tooltip("Total mass of live wood after normalize, kg. Stump not included.")]
    public float totalMass = 28f;
    public bool normalizeMass = true;
    [Tooltip("Density, kg/m³. 2D-slice thickness = segment width.")]
    public float woodDensity = 700f;

    [Header("Recursion")]
    public int maxDepth = 3;
    [Tooltip("Hard body cap. Growth stops; the solver does not explode.")]
    public int maxSegments = 15;
    public float trunkLength = 1.0f;
    public float trunkWidth = 0.20f;
    public float stumpHeight = 0.30f;
    public float stumpWidth = 0.26f;
    public float lengthDecay = 0.68f;
    public float widthDecay = 0.62f;
    public float forkAngle = 32f;
    public float minWidth = 0.04f;
    public float minLength = 0.18f;
    public int seed = 1;
    [Tooltip("Fork scatter, degrees. Local Random, not UnityEngine.Random.")]
    public float forkJitter = 4f;

    [Header("Per-order wood — Yury 18.09: the stem is strong, the crown sways")]
    [Tooltip("Bend limit per branch order, deg. Order 0 = trunk (near rigid), 1 = main limbs, 2 = secondary, 3 = twigs. The sway lives in the crown, never in the stem.")]
    public float trunkBendLimit = 1.5f;
    public float limbBendLimit = 5f;
    public float secondaryBendLimit = 12f;
    public float twigBendLimit = 28f;
    [Tooltip("Torsion stiffness per order, N·m/rad. The stem is very stiff; stiffness falls by order.")]
    public float trunkStiffness = 500f;
    public float limbStiffness = 120f;
    public float secondaryStiffness = 45f;
    public float twigStiffness = 22f;
    [Tooltip("Joint damping per order, N·m·s/rad.")]
    public float trunkFriction = 9f;
    public float limbFriction = 2.2f;
    public float secondaryFriction = 0.6f;
    public float twigFriction = 0.35f;
    [Tooltip("Spring torque cap per order, N·m.")]
    public float trunkSpringMaxTorque = 600f;
    public float twigSpringMaxTorque = 40f;
    public float minSegmentInertia = 0.008f;
    [Tooltip("World hold on the trunk, 0..1. Keeps the stem near vertical (the static stump absorbs the reaction); forks stay on the joint spring so a world error cannot yank the crown.")]
    public float trunkWorldHold = 1f;

    // Leader stays close to the parent axis; the side takes the full fork.
    // A subclass may change these for its crown recipe.
    protected float leaderForkShare = 0.34f;
    // Crown catches more sail (leaves, fruit). Wind-only; no visual here.
    protected bool growsLeaves = true;
    // Side branches are this fraction of the leader length.
    protected float sideLengthScale = 1f;

    public int SegmentCount { get; private set; }       // wood segments (stump excluded)
    public int DynamicBodyCount { get; private set; }
    public int RigidbodyCount { get; private set; }     // stump + wood
    public int WoodColliderCount { get; private set; }  // stump + wood
    public virtual int FruitCount => 0;
    public virtual int LeafCount => 0;
    public virtual string KindName => "Tree";
    public float HeightMeters => GetCrownY() - transform.position.y;
    // Build-time identity of the rig (sorted grow-pose positions and sizes,
    // fnv1a64). Read by the T1 trace: two runs on two builds cannot be compared
    // unless the rig hash matches. A constant of the build, never a metric.
    public string RigHash { get; private set; } = "";

    protected struct Node
    {
        public HumanSegment segment;
        public float heading;
        public int depth;
        public bool dynamic;
        public int parentIndex;
        public TreeSpring spring;
    }

    protected readonly List<Node> nodes = new List<Node>(16);
    private System.Random rng;
    private TreeSpring[] springs = System.Array.Empty<TreeSpring>();
    private JointFriction[] frictions = System.Array.Empty<JointFriction>();
    private float baseSpawnX;
    private const float AirDensity = 1.2f;
    private const float SpreadLimbDeg = 5f;

    // Subclass hook: set the crown recipe before the stump is grown.
    protected virtual void ApplyRecipe() { }

    // Subclass hook: ornament a just-grown wood segment (picture only).
    protected virtual void DecorateNode(HumanSegment seg, int depth, float heading, bool farSide, bool isTip) { }

    public override void Build()
    {
        foreach (Transform child in transform)
            Destroy(child.gameObject);

        nodes.Clear();
        springs = System.Array.Empty<TreeSpring>();
        frictions = System.Array.Empty<JointFriction>();
        SegmentCount = 0;
        DynamicBodyCount = 0;
        RigidbodyCount = 0;
        WoodColliderCount = 0;
        rng = new System.Random(seed);

        ApplyRecipe();

        int cap = Mathf.Clamp(maxSegments, 3, 31);
        if (nodes.Capacity < cap + 1)
            nodes.Capacity = cap + 1;

        // Static stump. The tree stands on it; it does not fall like a ragdoll.
        HumanSegment stump = CreateWood(
            "Stump",
            new Vector2(stumpWidth, stumpHeight),
            woodDensity * stumpWidth * stumpWidth * stumpHeight,
            BarkColor(-1, false),
            0);
        stump.transform.localPosition = new Vector3(0f, stumpHeight * 0.5f, 0f);
        stump.transform.localRotation = Quaternion.identity;
        stump.rb.bodyType = RigidbodyType2D.Static;
        baseSpawnX = stump.transform.position.x;
        nodes.Add(new Node
        {
            segment = stump,
            heading = 0f,
            depth = -1,
            dynamic = false,
            parentIndex = -1,
            spring = null
        });
        RigidbodyCount++;
        WoodColliderCount++;

        Grow(stump, 0f, 0, trunkLength, trunkWidth, 0f, cap);

        if (normalizeMass)
            NormalizeDynamicMass();

        DisableCollisionsBetweenSegments();
        MarkWoodLayer();
        Physics2D.SyncTransforms();
        BindGravityHold();
        CollectActuators();
        // Hash the rig at the grow pose, before any step can move it.
        RigHash = ComputeRigHash();
    }

    // One deterministic step: spring hold → joint damping → wind. The
    // springs/frictions are driven here so their own FixedUpdate (same
    // DefaultExecutionOrder) cannot race the ordering.
    void FixedUpdate()
    {
        if (springs.Length == 0)
            return;
        for (int i = 0; i < springs.Length; i++)
            if (springs[i] != null) springs[i].Apply();
        for (int i = 0; i < frictions.Length; i++)
            if (frictions[i] != null) frictions[i].Apply();
        if (windScale > 0f)
            ApplyWind();
    }

    private HumanSegment CreateWood(string name, Vector2 size, float mass, Color color, int sortingOrder)
    {
        GameObject segmentObject = new GameObject(name);
        HumanSegment segment = segmentObject.AddComponent<HumanSegment>();
        segment.Initialize(name, size, Mathf.Max(0.02f, mass), color, transform, sortingOrder, HumanVisualShape.Box);
        segment.jointMarkerDiameter = 0.025f;
        if (segment.rb != null && minSegmentInertia > 0f && segment.rb.inertia < minSegmentInertia)
            segment.rb.inertia = minSegmentInertia;
        // Explicit wood material on every collider (rule + skill).
        if (segment.collider != null)
            segment.collider.sharedMaterial = WoodMaterial();
        return segment;
    }

    private void Grow(HumanSegment parent, float parentHeading, int depth, float length, float width, float heading, int cap)
    {
        if (SegmentCount >= cap) return;
        if (width < minWidth || length < minLength) return;

        bool farSide = heading < parentHeading - 0.01f;
        Color bark = BarkColor(depth, farSide);

        HumanSegment seg = CreateWood(
            "Wood_" + depth + "_" + SegmentCount,
            new Vector2(width, length),
            woodDensity * width * width * length,
            bark,
            depth + 1);

        Vector2 parentTip = TipLocal(parent, parentHeading);
        Vector2 axis = HeadingDir(heading);
        seg.transform.localPosition = parentTip + axis * (length * 0.5f);
        seg.transform.localRotation = Quaternion.Euler(0f, 0f, heading);

        seg.ConnectTo(
            parent,
            new Vector2(0f, -length * 0.5f),
            new Vector2(0f, parent.size.y * 0.5f),
            -BendLimitForOrder(OrderOfDepth(depth)),
            BendLimitForOrder(OrderOfDepth(depth)));
        AddWoodActuators(seg, width, length, heading, depth);
        TreeSpring spring = seg.GetComponent<TreeSpring>();

        nodes.Add(new Node
        {
            segment = seg,
            heading = heading,
            depth = depth,
            dynamic = true,
            parentIndex = IndexOf(parent),
            spring = spring
        });
        SegmentCount++;
        DynamicBodyCount++;
        RigidbodyCount++;
        WoodColliderCount++;

        DecorateNode(seg, depth, heading, farSide, depth >= maxDepth);

        if (depth >= maxDepth)
            return;

        float jitter = NextJitter();
        float nextLen = length * lengthDecay;
        float nextWid = width * widthDecay;

        // Leader stays near the parent axis (oak ~8° of fork 28). The side
        // alternates outward so the centre of mass stays over the stump.
        Grow(seg, heading, depth + 1, nextLen, nextWid,
            heading + forkAngle * leaderForkShare + jitter * 0.25f, cap);

        float arrived = Mathf.DeltaAngle(parentHeading, heading);
        float outward = Mathf.Abs(arrived) > SpreadLimbDeg
            ? Mathf.Sign(arrived)
            : (depth % 2 == 0 ? -1f : 1f);
        float forkDeg = Mathf.Abs(arrived) > SpreadLimbDeg ? forkAngle * 0.62f : forkAngle;
        float sideHeading = Mathf.Clamp(heading + outward * (forkDeg + jitter), -78f, 78f);
        Grow(seg, heading, depth + 1, nextLen * sideLengthScale, nextWid, sideHeading, cap);
    }

    private void AddWoodActuators(HumanSegment segment, float width, float length, float heading, int depth)
    {
        int order = OrderOfDepth(depth);
        float lengthScale = trunkLength / Mathf.Max(0.05f, length);

        TreeSpring spring = segment.gameObject.AddComponent<TreeSpring>();
        spring.stiffness = StiffnessForOrder(order) * lengthScale;
        spring.restAngle = 0f;
        spring.restWorldAngle = heading;
        // World hold on the trunk only. The stem stays near vertical while the
        // forks sway on their own joint spring (a world error cannot yank the
        // crown). TreeSpring.Apply only honours worldStiffness when the parent
        // is static — the trunk — so deeper orders are joint-spring only.
        spring.worldStiffness = order == 0 && trunkWorldHold > 0.001f
            ? trunkWorldHold * trunkStiffness * lengthScale
            : 0f;
        spring.maxTorque = order == 0 ? trunkSpringMaxTorque : twigSpringMaxTorque;

        JointFriction friction = segment.gameObject.AddComponent<JointFriction>();
        friction.damping = DampingForOrder(order);
        friction.maxTorque = spring.maxTorque;
    }

    // Branch order from growth depth: 0 = trunk, 1 = main limbs, 2 = secondary,
    // 3 = twigs. Deeper growth is clamped to the twig recipe.
    private static int OrderOfDepth(int depth)
    {
        return Mathf.Clamp(depth, 0, 3);
    }

    private float BendLimitForOrder(int order)
    {
        switch (order)
        {
            case 0: return trunkBendLimit;
            case 1: return limbBendLimit;
            case 2: return secondaryBendLimit;
            default: return twigBendLimit;
        }
    }

    private float StiffnessForOrder(int order)
    {
        switch (order)
        {
            case 0: return trunkStiffness;
            case 1: return limbStiffness;
            case 2: return secondaryStiffness;
            default: return twigStiffness;
        }
    }

    private float DampingForOrder(int order)
    {
        switch (order)
        {
            case 0: return trunkFriction;
            case 1: return limbFriction;
            case 2: return secondaryFriction;
            default: return twigFriction;
        }
    }

    protected virtual Color BarkColor(int depth, bool farSide)
    {
        Color c;
        if (depth < 0) c = new Color(0.32f, 0.20f, 0.11f, 1f);
        else if (depth == 0) c = new Color(0.38f, 0.24f, 0.13f, 1f);
        else if (depth == 1) c = new Color(0.44f, 0.29f, 0.15f, 1f);
        else c = new Color(0.48f, 0.32f, 0.17f, 1f);
        if (farSide)
        {
            c.r *= 0.82f;
            c.g *= 0.82f;
            c.b *= 0.82f;
        }
        return c;
    }

    private void NormalizeDynamicMass()
    {
        if (totalMass <= 0.01f) return;
        float sum = 0f;
        for (int i = 0; i < nodes.Count; i++)
            if (nodes[i].dynamic && nodes[i].segment != null)
                sum += nodes[i].segment.mass;
        if (sum <= 0.01f) return;

        float scale = totalMass / sum;
        for (int i = 0; i < nodes.Count; i++)
        {
            Node n = nodes[i];
            if (!n.dynamic || n.segment == null || n.segment.rb == null) continue;
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
            for (int j = i + 1; j < all.Length; j++)
                Physics2D.IgnoreCollision(all[i], all[j], true);
    }

    private void MarkWoodLayer()
    {
        for (int i = 0; i < nodes.Count; i++)
            if (nodes[i].segment != null)
                GroundLayers.Apply(nodes[i].segment.gameObject);
    }

    // Hold torque = minus the gravity moment of this segment and its
    // descendants about the hinge, taken at the grow pose. Extra mass is not
    // in the sum, so the spring still bends under load.
    private void BindGravityHold()
    {
        Vector2 g = Physics2D.gravity;
        for (int i = 0; i < nodes.Count; i++)
        {
            Node n = nodes[i];
            if (!n.dynamic || n.segment == null || n.segment.joint == null) continue;
            TreeSpring spring = n.spring;
            if (spring == null) continue;

            Vector2 jointWorld = n.segment.transform.TransformPoint(new Vector2(0f, -n.segment.size.y * 0.5f));
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

    private void CollectActuators()
    {
        List<TreeSpring> sp = new List<TreeSpring>(nodes.Count);
        List<JointFriction> fr = new List<JointFriction>(nodes.Count);
        for (int i = 0; i < nodes.Count; i++)
        {
            HumanSegment seg = nodes[i].segment;
            if (seg == null) continue;
            if (nodes[i].spring != null)
            {
                nodes[i].spring.drivenExternally = true;
                sp.Add(nodes[i].spring);
            }
            JointFriction f = seg.GetComponent<JointFriction>();
            if (f != null)
            {
                f.drivenExternally = true;
                fr.Add(f);
            }
        }
        springs = sp.ToArray();
        frictions = fr.ToArray();
    }

    private static Vector2 HeadingDir(float headingDeg)
    {
        float rad = headingDeg * Mathf.Deg2Rad;
        return new Vector2(-Mathf.Sin(rad), Mathf.Cos(rad));
    }

    private static Vector2 TipLocal(HumanSegment segment, float headingDeg)
    {
        return (Vector2)segment.transform.localPosition + HeadingDir(headingDeg) * (segment.size.y * 0.5f);
    }

    private float NextJitter()
    {
        if (forkJitter <= 0.01f || rng == null) return 0f;
        return (float)(rng.NextDouble() * 2.0 - 1.0) * forkJitter;
    }

    private int IndexOf(HumanSegment segment)
    {
        for (int i = 0; i < nodes.Count; i++)
            if (nodes[i].segment == segment)
                return i;
        return -1;
    }

    public float GetMaxAbsJointAngle()
    {
        float max = 0f;
        for (int i = 0; i < nodes.Count; i++)
        {
            HumanSegment seg = nodes[i].segment;
            if (seg == null || seg.joint == null) continue;
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

    // T1 instrumentation port (TREES plan, 18.09): the same sensor the oak
    // hands out, so one TreeJointTrace reads both trees. Raw Unity handles plus
    // build-time identity only — no metric logic, no forces. The recorder does
    // its arithmetic from rb/joint primitives, never from a helper a fix could
    // rewrite (GetMaxAbsJointAngle / GetTrunkLeanDeg / GetHingePinnedFraction
    // / GetHoldStats).
    public void GetWoodSensors(List<WoodSensor> into)
    {
        if (into == null) return;
        into.Clear();
        for (int i = 0; i < nodes.Count; i++)
        {
            HumanSegment seg = nodes[i].segment;
            if (seg == null) continue;
            TreeSpring spring = nodes[i].spring;
            into.Add(new WoodSensor
            {
                segment = seg,
                spring = spring,
                friction = seg.GetComponent<JointFriction>(),
                order = nodes[i].depth,
                bindDeg = spring != null ? spring.restAngle : 0f,
                buildHeadingDeg = spring != null ? spring.restWorldAngle : 0f
            });
        }
    }

    // Rig identity: sorted segment positions and sizes, hashed (fnv1a64).
    // Same construction as PlantTree.ComputeRigHash, so one build hash kind
    // covers the whole plant family.
    private string ComputeRigHash()
    {
        List<string> parts = new List<string>(nodes.Count);
        for (int i = 0; i < nodes.Count; i++)
        {
            HumanSegment seg = nodes[i].segment;
            if (seg == null) continue;
            Vector2 p = seg.transform.position;
            parts.Add(p.x.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture)
                + "|" + p.y.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture)
                + "|" + seg.size.x.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture)
                + "|" + seg.size.y.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture));
        }
        parts.Sort(System.StringComparer.Ordinal);
        ulong hash = 14695981039346656037UL;
        for (int i = 0; i < parts.Count; i++)
        {
            string s = parts[i];
            for (int c = 0; c < s.Length; c++)
            {
                hash ^= s[c];
                hash *= 1099511628211UL;
            }
            hash ^= 10UL;
            hash *= 1099511628211UL;
        }
        return hash.ToString("x16", System.Globalization.CultureInfo.InvariantCulture);
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

    // Trunk lean from world-up, deg. The stem must stay near vertical; the
    // trunk hinge is limited to +-trunkBendLimit, so this reads the real bend.
    public float GetTrunkLeanDeg()
    {
        for (int i = 0; i < nodes.Count; i++)
        {
            Node n = nodes[i];
            if (n.depth != 0 || !n.dynamic || n.segment == null) continue;
            if (n.segment.rb != null)
                return Mathf.Abs(Mathf.DeltaAngle(0f, n.segment.rb.rotation));
            return Mathf.Abs(Mathf.DeltaAngle(0f, n.segment.transform.eulerAngles.z));
        }
        return 0f;
    }

    // Fraction of dynamic hinges sitting AT their limit (pinned). 0 = none.
    // A hinge pinned at its stop means the tree has sagged or blown to a limit.
    public float GetHingePinnedFraction()
    {
        int dynamic = 0, pinned = 0;
        const float epsDeg = 0.2f;
        for (int i = 0; i < nodes.Count; i++)
        {
            Node n = nodes[i];
            if (!n.dynamic || n.segment == null || n.segment.joint == null) continue;
            dynamic++;
            JointAngleLimits2D lim = n.segment.joint.limits;
            float angle = n.segment.joint.jointAngle;
            if (Mathf.Abs(angle - lim.min) <= epsDeg || Mathf.Abs(angle - lim.max) <= epsDeg)
                pinned++;
        }
        return dynamic > 0 ? (float)pinned / dynamic : 0f;
    }

    // Base slide, m. The stump is static, so this is 0 by construction — but
    // it is read from the actual stump transform, not hardcoded.
    public float GetBaseSlideM()
    {
        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodes[i].depth == -1 && nodes[i].segment != null)
                return Mathf.Abs(nodes[i].segment.transform.position.x - baseSpawnX);
        }
        return 0f;
    }

    // Highest crown tip in world space, along the segment's own heading, so
    // the horizontal excursion is the sway rather than a fixed box corner.
    public Vector2 GetCrownTip()
    {
        Vector2 best = transform.position;
        float bestY = float.MinValue;
        for (int i = 0; i < nodes.Count; i++)
        {
            HumanSegment seg = nodes[i].segment;
            if (seg == null) continue;
            Vector2 tip = (Vector2)seg.transform.position
                + HeadingDir(seg.transform.eulerAngles.z) * (seg.size.y * 0.5f);
            if (tip.y > bestY) { bestY = tip.y; best = tip; }
        }
        return best;
    }

    public Vector2 GetFollowPoint()
    {
        float massSum = 0f;
        Vector2 weighted = Vector2.zero;
        for (int i = 0; i < nodes.Count; i++)
        {
            Node n = nodes[i];
            if (n.segment == null || n.segment.rb == null || !n.dynamic) continue;
            float m = n.segment.rb.mass;
            massSum += m;
            weighted += (Vector2)n.segment.rb.worldCenterOfMass * m;
        }
        if (massSum > 0.001f)
            return weighted / massSum;
        return transform.position;
    }

    public void ApplyWind()
    {
        if (windScale <= 0f) return;
        float omega = windPeriod > 0.05f ? (2f * Mathf.PI / windPeriod) : 0f;
        float t = Time.fixedTime;
        float q = 0.5f * AirDensity * windScale;

        for (int i = 0; i < nodes.Count; i++)
        {
            Node n = nodes[i];
            if (!n.dynamic || n.segment == null || n.segment.rb == null) continue;

            float gust = 1f;
            if (omega > 0f)
            {
                float depth01 = maxDepth <= 0 ? 1f : Mathf.Clamp01(n.depth / (float)maxDepth);
                gust += windGust * Mathf.Sin(t * omega + n.depth * 0.62f + seed * 0.17f)
                    * Mathf.Lerp(0.32f, 1f, depth01);
            }
            float v = windSpeed * gust;
            if (Mathf.Abs(v) < 0.01f) continue;

            float area = n.segment.size.x * n.segment.size.y;
            float heading = n.segment.rb.rotation;
            n.segment.rb.AddForce(
                new Vector2(
                    Mathf.Sign(v) * q * v * Mathf.Abs(v) * area
                    * WindSail(n.depth) * HeadingSailMul(heading),
                    0f),
                ForceMode2D.Force);
        }
    }

    private float WindSail(int depth)
    {
        if (depth <= 0) return 0.18f;
        float sail = 0.70f + 0.55f * depth;
        if (growsLeaves)
            sail += depth >= 2 ? 0.85f : 0.25f;
        return sail;
    }

    private static float HeadingSailMul(float headingDeg)
    {
        float lean01 = Mathf.Abs(Mathf.Sin(headingDeg * Mathf.Deg2Rad));
        return Mathf.Lerp(1f, 1.22f, lean01);
    }
}
