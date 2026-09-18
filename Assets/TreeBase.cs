using System.Collections.Generic;
using UnityEngine;

// A plant with a trunk and recursive branches on hinges. No muscles: wood is
// a TreeSpring torsion hold (cancels self-weight at the grow pose) plus
// JointFriction damping. Wind is a ½ρv² force on each segment. This class
// knows nothing about fruit or leaves — a subclass decorates the wood with
// picture-only ornaments (see AppleTree). The stump is static: the tree
// sways, it does not fall like a human ragdoll.
public abstract class TreeBase : PlantBase
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
    [Tooltip("1 = the trunk also holds world-up (the static stump absorbs the reaction); 0 = joint spring only.")]
    public float worldHoldScale = 1f;

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
            -BendLimit(width),
            BendLimit(width));
        AddWoodActuators(seg, width, length, heading);
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

    private void AddWoodActuators(HumanSegment segment, float width, float length, float heading)
    {
        float t = ThicknessT(width);
        float thick = t * t * t;
        float lengthScale = trunkLength / Mathf.Max(0.05f, length);

        TreeSpring spring = segment.gameObject.AddComponent<TreeSpring>();
        spring.stiffness = Mathf.Lerp(twigStiffness, trunkStiffness, thick) * lengthScale;
        spring.restAngle = 0f;
        spring.restWorldAngle = heading;
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
