using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

// T1 instrumentation (TREES plan, 18.09). Per-step, per-joint trace of one
// tree trial plus the build block that identifies the rig.
//
// READ-ONLY SENSOR. It samples Unity primitives — Rigidbody2D.rotation /
// position / mass / inertia, HingeJoint2D.jointAngle / limits, the hinge graph
// (joint.connectedBody) — and the torques the actuators recorded while pushing
// (TreeSpring.lastSpringTorque, JointFriction.lastFrictionTorque). It never
// calls a PlantTree metric helper (GetMaxAbsJointAngle / GetTrunkLeanDeg /
// GetHingePinnedFraction / GetHoldStats): a metric the fix can rewrite is not
// a measurement.
//
// It applies no force and touches no solver state, so a run it observes keeps
// the physics of a run without it, bit for bit.
//
// T1 covers both hingenous trees: PlantTree (the five kinds) and TreeBase
// (AppleTree). One trace, one CSV layout, so the oak and the apple are read by
// the same scripts.
public struct WoodSensor
{
    public HumanSegment segment;
    public TreeSpring spring;
    public JointFriction friction;
    public int order;              // branch order 0..3; -1 = stump
    public float bindDeg;          // grow-pose joint angle (spring.restAngle)
    public float buildHeadingDeg;  // grow-pose world heading (spring.restWorldAngle)
}

// Sensor port of a tree: raw Unity handles plus build-time identity ONLY.
// There is no metric logic behind it, so wiring a trace into a run can never be
// mistaken for a measurement, and nothing read through the port applies a
// force.
public interface IWoodSensorSource
{
    string RigHash { get; }
    void GetWoodSensors(List<WoodSensor> into);
}

public class TreeJointTrace
{
    private const string NumFormat = "0.######";

    private struct Joint
    {
        public HumanSegment segment;
        public TreeSpring spring;
        public JointFriction friction;
        public Rigidbody2D rb;
        public HingeJoint2D hinge;
        public int bodyIndex;
        public int order;
        public float bindDeg;
        public float buildHeadingDeg;
        public float maxTorque;
        public float stiffness;
        public float worldStiffness;
        public float stiffnessLimitBound;
    }

    private readonly IWoodSensorSource tree;
    private readonly float fixedDelta;
    private readonly List<WoodSensor> wood = new List<WoodSensor>(40);
    private readonly List<Joint> joints = new List<Joint>(32);
    private readonly List<Rigidbody2D> bodies = new List<Rigidbody2D>(32);
    private readonly List<HumanSegment> bodySegments = new List<HumanSegment>(32);
    private readonly List<int> bodyParent = new List<int>(32);
    private readonly List<List<int>> jointSubtree = new List<List<int>>(32);
    private readonly List<int> jointOfBody = new List<int>(32);
    private readonly StringBuilder csv = new StringBuilder(1 << 21);
    private readonly StringBuilder line = new StringBuilder(4096);

    private float stumpSpawnX;
    private float buildWoodComX;
    private bool ready;

    private int steps;
    private long jointSamples;
    private int saturatedJointSamples;
    private float maxAbsAppliedTorqueNm;
    private float holdResidualNm;
    private float maxAbsSegmentWorldDevDeg;
    private float maxAbsWoodComDxM;
    private float trunkJointAngleDeg;
    private float trunkLimitDeg;

    public string BuildHash { get; private set; }
    public string BuildHashKind => "fnv1a64";
    public float FixedDelta => fixedDelta;
    public int SegmentCount => wood.Count;
    public int JointColumnCount => joints.Count;
    public int BodyCount => bodies.Count;
    public float MaxAbsAppliedTorqueNm => maxAbsAppliedTorqueNm;
    public float HoldResidualNm => holdResidualNm;
    public float MaxAbsSegmentWorldDevDeg => maxAbsSegmentWorldDevDeg;
    public float CrownComDxM => maxAbsWoodComDxM;
    public float TrunkJointAngleDeg => trunkJointAngleDeg;
    public float TrunkLimitDeg => trunkLimitDeg;
    public float MaxStiffnessLimitBound { get; private set; }
    public float SpringSatFraction =>
        jointSamples > 0 ? (float)saturatedJointSamples / jointSamples : 0f;

    public TreeJointTrace(IWoodSensorSource tree, float fixedDelta)
    {
        this.tree = tree;
        this.fixedDelta = fixedDelta > 0f ? fixedDelta : Time.fixedDeltaTime;
        Build();
    }

    private void Build()
    {
        if (tree == null) return;
        tree.GetWoodSensors(wood);

        // Hinge graph, read once: every live wood body and its parent. The
        // gravity moment of a joint needs the bodies hanging under it, and that
        // structure is a build constant.
        Dictionary<Rigidbody2D, int> bodyOfRb = new Dictionary<Rigidbody2D, int>(bodies.Capacity);
        Dictionary<HumanSegment, int> bodyOfSegment = new Dictionary<HumanSegment, int>(bodies.Capacity);
        int[] bodyWoodIndex = new int[wood.Count];
        for (int i = 0; i < wood.Count; i++)
        {
            bodyWoodIndex[i] = -1;
            WoodSensor w = wood[i];
            HumanSegment seg = w.segment;
            if (seg == null) continue;
            if (w.order < 0)
            {
                // The stump: static, no hinge. Base slide is measured from it.
                stumpSpawnX = seg.transform.position.x;
                continue;
            }
            if (seg.joint == null || seg.rb == null) continue;
            bodyWoodIndex[i] = bodies.Count;
            bodyOfRb[seg.rb] = bodies.Count;
            bodyOfSegment[seg] = bodies.Count;
            bodies.Add(seg.rb);
            bodySegments.Add(seg);
            bodyParent.Add(-1);
            jointOfBody.Add(-1);
            jointSubtree.Add(new List<int>(8));
        }

        for (int i = 0; i < wood.Count; i++)
        {
            int body = bodyWoodIndex[i];
            if (body < 0) continue;
            WoodSensor w = wood[i];
            HumanSegment seg = w.segment;
            if (seg.joint.connectedBody != null
                && bodyOfRb.TryGetValue(seg.joint.connectedBody, out int parent))
                bodyParent[body] = parent;

            float selfInertia = seg.rb.inertia;
            float parentInertia = seg.joint.connectedBody != null ? seg.joint.connectedBody.inertia : 0f;
            float invSum = 0f;
            if (selfInertia > 0f) invSum += 1f / selfInertia;
            if (parentInertia > 0f) invSum += 1f / parentInertia;
            float effectiveInertia = invSum > 0f ? 1f / invSum : 0f;
            // Explicit-integration bound of the pair, 4I/dt^2 (N.m/rad). A
            // spring above it is clamped by the solver, not by the wood.
            float bound = effectiveInertia > 0f
                ? 4f * effectiveInertia / (fixedDelta * fixedDelta)
                : 0f;
            float stiffness = w.spring != null ? w.spring.stiffness : 0f;
            float ratio = bound > 0f ? stiffness / bound : 0f;
            if (ratio > MaxStiffnessLimitBound) MaxStiffnessLimitBound = ratio;

            JointAngleLimits2D limits = seg.joint.limits;
            if (w.order == 0)
            {
                trunkLimitDeg = Mathf.Max(Mathf.Abs(limits.min), Mathf.Abs(limits.max));
                trunkJointAngleDeg = seg.joint.jointAngle;
            }

            jointOfBody[body] = joints.Count;
            joints.Add(new Joint
            {
                segment = seg,
                spring = w.spring,
                friction = w.friction,
                rb = seg.rb,
                hinge = seg.joint,
                bodyIndex = body,
                order = w.order,
                bindDeg = w.bindDeg,
                buildHeadingDeg = w.buildHeadingDeg,
                maxTorque = w.spring != null ? w.spring.maxTorque : 0f,
                stiffness = stiffness,
                worldStiffness = w.spring != null ? w.spring.worldStiffness : 0f,
                stiffnessLimitBound = ratio
            });
        }

        // Subtree of each joint: walk every body up to the root.
        for (int b = 0; b < bodies.Count; b++)
        {
            int walk = b;
            int guard = 0;
            while (walk >= 0 && guard++ <= bodies.Count)
            {
                int owner = jointOfBody[walk];
                if (owner >= 0) jointSubtree[owner].Add(b);
                walk = bodyParent[walk];
            }
        }

        buildWoodComX = WoodComX();
        // The rig identity comes from the build (PlantTree.RigHash), not from
        // here: hashing at recorder construction made a wind run hash a rig
        // that had already moved (hold aa7807e3 vs wind 0607ea1b, same rig).
        BuildHash = !string.IsNullOrEmpty(tree.RigHash) ? tree.RigHash : RigHash();
        ready = joints.Count > 0;
        WriteHeader();
    }

    // One row per FixedUpdate: t plus ten columns per joint — the nine the plan
    // names, plus the gravity moment of the joint's subtree (the load the hold
    // is there to cancel).
    public void Step(float time)
    {
        if (!ready) return;
        steps++;
        line.Clear();
        line.Append(time.ToString(NumFormat, CultureInfo.InvariantCulture));
        float gravityY = Physics2D.gravity.y;

        for (int i = 0; i < joints.Count; i++)
        {
            Joint j = joints[i];
            JointAngleLimits2D lim = j.hinge.limits;
            float angle = j.hinge.jointAngle;
            float springRaw = j.spring != null ? j.spring.lastSpringTorqueRaw : 0f;
            float spring = j.spring != null ? j.spring.lastSpringTorque : 0f;
            float hold = j.spring != null ? j.spring.holdTorque : 0f;
            float friction = j.friction != null ? j.friction.lastFrictionTorque : 0f;
            float rotation = j.rb.rotation;
            float inertia = j.rb.inertia;
            float mass = j.rb.mass;
            float moment = GravityMoment(i, gravityY);

            Append(line, angle);
            Append(line, lim.min);
            Append(line, lim.max);
            Append(line, spring);
            Append(line, hold);
            Append(line, friction);
            Append(line, rotation);
            Append(line, inertia);
            Append(line, mass);
            Append(line, moment);

            float absSpring = Mathf.Abs(spring);
            if (absSpring > maxAbsAppliedTorqueNm) maxAbsAppliedTorqueNm = absSpring;
            if (j.maxTorque > 0f && Mathf.Abs(springRaw) >= j.maxTorque) saturatedJointSamples++;
            jointSamples++;

            float dev = Mathf.Abs(Mathf.DeltaAngle(j.buildHeadingDeg, rotation));
            if (dev > maxAbsSegmentWorldDevDeg) maxAbsSegmentWorldDevDeg = dev;

            // Residual of the hold: feedforward + spring + the gravity moment
            // the pose actually has now. Zero at the bind by construction, so
            // this is the part the hold no longer cancels — it moves with wind.
            float residual = Mathf.Abs(hold + spring + moment);
            if (residual > holdResidualNm) holdResidualNm = residual;

            if (j.order == 0) trunkJointAngleDeg = angle;
        }

        line.Append('\n');
        csv.Append(line);

        float comDx = Mathf.Abs(WoodComX() - buildWoodComX);
        if (comDx > maxAbsWoodComDxM) maxAbsWoodComDxM = comDx;
    }

    public string BuildCsv()
    {
        return csv.ToString();
    }

    // Gravity moment of this joint's subtree about the hinge, N.m. Same
    // formula BindGravityHold uses at build (box centre, not the collider COM),
    // so the bind value stays comparable.
    private float GravityMoment(int jointIndex, float gravityY)
    {
        Joint j = joints[jointIndex];
        Vector2 anchor = j.segment.transform.TransformPoint(
            new Vector2(0f, -j.segment.size.y * 0.5f));
        List<int> subtree = jointSubtree[jointIndex];
        float moment = 0f;
        for (int k = 0; k < subtree.Count; k++)
        {
            int b = subtree[k];
            Vector2 p = bodySegments[b].transform.position;
            moment += (p.x - anchor.x) * bodies[b].mass * gravityY;
        }
        return moment;
    }

    private void WriteHeader()
    {
        csv.Clear();
        csv.Append("t");
        for (int i = 0; i < joints.Count; i++)
        {
            string p = ",J" + i.ToString("00", CultureInfo.InvariantCulture) + "_";
            csv.Append(p).Append("angle");
            csv.Append(p).Append("limMin");
            csv.Append(p).Append("limMax");
            csv.Append(p).Append("springTorque");
            csv.Append(p).Append("holdTorque");
            csv.Append(p).Append("frictionTorque");
            csv.Append(p).Append("rotation");
            csv.Append(p).Append("inertia");
            csv.Append(p).Append("mass");
            csv.Append(p).Append("gravityMoment");
        }
        csv.Append('\n');
    }

    // Build block: one entry per joint, read once at bind. Not a metric — a
    // constant of the rig, so it belongs in `build:` and never in a run block.
    public void AppendJointBuildJson(StringBuilder json)
    {
        json.Append(",\"joints\":[");
        for (int i = 0; i < joints.Count; i++)
        {
            Joint j = joints[i];
            JointAngleLimits2D lim = j.hinge.limits;
            if (i > 0) json.Append(',');
            json.Append('{');
            json.Append("\"i\":").Append(i.ToString(CultureInfo.InvariantCulture));
            json.Append(",\"order\":").Append(j.order.ToString(CultureInfo.InvariantCulture));
            AppendNum(json, "bindDeg", j.bindDeg, true);
            AppendNum(json, "limitMinDeg", lim.min, true);
            AppendNum(json, "limitMaxDeg", lim.max, true);
            AppendNum(json, "stiffness", j.stiffness, true);
            AppendNum(json, "worldStiffness", j.worldStiffness, true);
            AppendNum(json, "maxTorque", j.maxTorque, true);
            AppendNum(json, "holdTorque", j.spring != null ? j.spring.holdTorque : 0f, true);
            AppendNum(json, "frictionDamping", j.friction != null ? j.friction.damping : 0f, true);
            AppendNum(json, "frictionMaxTorque", j.friction != null ? j.friction.maxTorque : 0f, true);
            AppendNum(json, "gravityGradient", j.spring != null ? j.spring.gravityGradient : 0f, true);
            AppendNum(json, "mass", j.rb.mass, true);
            AppendNum(json, "inertia", j.rb.inertia, true);
            AppendNum(json, "sizeX", j.segment.size.x, true);
            AppendNum(json, "sizeY", j.segment.size.y, true);
            AppendNum(json, "buildHeadingDeg", j.buildHeadingDeg, true);
            AppendNum(json, "kOverStabilityBound", j.stiffnessLimitBound, true);
            json.Append(",\"subtreeBodies\":").Append(jointSubtree[i].Count.ToString(CultureInfo.InvariantCulture));
            json.Append('}');
        }
        json.Append(']');
    }

    // Mass-weighted centre of mass of the live wood, read from the rigidbodies.
    private float WoodComX()
    {
        float massSum = 0f;
        float weighted = 0f;
        for (int i = 0; i < bodies.Count; i++)
        {
            float m = bodies[i].mass;
            massSum += m;
            weighted += bodies[i].worldCenterOfMass.x * m;
        }
        return massSum > 0.001f ? weighted / massSum : 0f;
    }

    // Fallback rig hash for a tree that did not report one (Static pictures).
    // Rig identity: sorted segment positions and sizes, hashed. Two runs on two
    // builds cannot be compared unless the rig hash matches.
    private string RigHash()
    {
        List<string> parts = new List<string>(wood.Count);
        for (int i = 0; i < wood.Count; i++)
        {
            HumanSegment seg = wood[i].segment;
            if (seg == null) continue;
            Vector2 p = seg.transform.position;
            parts.Add(p.x.ToString("0.######", CultureInfo.InvariantCulture)
                + "|" + p.y.ToString("0.######", CultureInfo.InvariantCulture)
                + "|" + seg.size.x.ToString("0.######", CultureInfo.InvariantCulture)
                + "|" + seg.size.y.ToString("0.######", CultureInfo.InvariantCulture));
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
        return hash.ToString("x16", CultureInfo.InvariantCulture);
    }

    private static void Append(StringBuilder sb, float value)
    {
        sb.Append(',');
        sb.Append(value.ToString(NumFormat, CultureInfo.InvariantCulture));
    }

    private static void AppendNum(StringBuilder json, string key, float value, bool comma)
    {
        if (comma) json.Append(',');
        json.Append('"').Append(key).Append("\":");
        json.Append(value.ToString("0.########", CultureInfo.InvariantCulture));
    }
}
