using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

// Apple-tree stand metrics. Read-only, no forces.
//
// T1 (TREES plan, 18.09): every metric here is computed from Unity primitives
// — Rigidbody2D.rotation/mass/inertia, HingeJoint2D.jointAngle/limits,
// transform positions — and never from a TreeBase helper
// (GetMaxAbsJointAngle / GetTrunkLeanDeg / GetHingePinnedFraction /
// GetHoldStats / GetCrownY / GetCrownTip). A metric the fix can rewrite is not
// a measurement.
//
// The JSON is split in two: the run block carries MEASUREMENTS only, and the
// `build` block carries every build constant (counts, recipe, springs, limits,
// bind pose, hold torques, rig hash). A constant inside a run block is a
// constant-suspect key: tree-const-assert.py flags any run-block key that is
// bit-identical across two runs with different wind.
//
// Same key names as before this cycle for everything that existed; the keys
// that are build constants moved into `build:` and no key was renamed.
public class AppleTrialRecorder
{
    private struct Sensor
    {
        public WoodSensor wood;
        public HingeJoint2D joint;
        public Rigidbody2D rb;
    }

    private readonly TreeBase tree;
    private readonly TrialRunInfo info;
    private readonly TreeJointTrace trace;
    private readonly List<WoodSensor> wood = new List<WoodSensor>(40);
    private readonly List<Sensor> sensors = new List<Sensor>(32);

    private int samples;
    private double sumAbsJoint;
    private double sumJointSq;
    private float maxAbsJoint;
    private float startCrownY;
    private float minCrownY = float.MaxValue;
    private float lastAbsJoint;
    private float lastCrownY;
    private float settleAbsJoint;
    private bool settled;
    // Sway-never-topple metrics, same four keys as PlantTrialRecorder so the
    // oak and the apple stand are read by one probe script.
    private float maxTrunkLeanDeg;
    private float maxPinnedFraction;
    private float maxBaseSlideM;
    private float startCrownTipX;
    private float swayAmpM;
    private float stumpSpawnX;
    private readonly float[] orderMaxAngle = new float[4];

    // Build constants, read once when the recorder is wired (before the first
    // step): they describe the rig, so they belong in `build:`.
    private readonly int segmentCount;
    private readonly int dynamicCount;
    private readonly int rigidbodyCount;
    private readonly int woodColliderCount;
    private readonly string kindName = "";
    private readonly int seed;
    private readonly float appleHeightM;
    private readonly int leafCount;
    private readonly int fruitCount;
    private readonly float windSpeed;
    private readonly float windScale;
    private readonly float windGust;
    private readonly float windPeriod;
    private readonly float meanHoldTorque;
    private readonly float maxHoldTorque;
    private double sumHoldTorque;
    private int holdTorques;

    public AppleTrialRecorder(TreeBase tree, TrialRunInfo info)
    {
        this.tree = tree;
        this.info = info;
        if (tree != null)
        {
            tree.GetWoodSensors(wood);
            for (int i = 0; i < wood.Count; i++)
            {
                HumanSegment seg = wood[i].segment;
                if (seg == null) continue;
                if (wood[i].order < 0)
                {
                    // The stump: static, no hinge. Base slide is measured from it.
                    stumpSpawnX = seg.transform.position.x;
                    continue;
                }
                sensors.Add(new Sensor { wood = wood[i], joint = seg.joint, rb = seg.rb });
                TreeSpring spring = wood[i].spring;
                if (spring != null)
                {
                    float abs = Mathf.Abs(spring.holdTorque);
                    if (abs > maxHoldTorque) maxHoldTorque = abs;
                    sumHoldTorque += abs;
                    holdTorques++;
                }
            }
            startCrownY = CrownY();
            startCrownTipX = CrownTip().x;
            segmentCount = tree.SegmentCount;
            dynamicCount = tree.DynamicBodyCount;
            rigidbodyCount = tree.RigidbodyCount;
            woodColliderCount = tree.WoodColliderCount;
            kindName = tree.KindName;
            seed = tree.seed;
            appleHeightM = tree is AppleTree apple ? apple.appleHeightM : 0f;
            leafCount = tree.LeafCount;
            fruitCount = tree.FruitCount;
            windSpeed = tree.windSpeed;
            windScale = tree.windScale;
            windGust = tree.windGust;
            windPeriod = tree.windPeriod;
            meanHoldTorque = holdTorques > 0 ? (float)(sumHoldTorque / holdTorques) : 0f;
        }
        trace = new TreeJointTrace(tree, Time.fixedDeltaTime);
    }

    public int JointColumnCount => trace.JointColumnCount;
    public string BuildJointCsv()
    {
        return trace.BuildCsv();
    }

    public void Sample(float time)
    {
        if (tree == null) return;
        float abs = MaxAbsJointAngle();
        float crown = CrownY();
        if (samples == 0)
            startCrownY = crown;
        lastAbsJoint = abs;
        lastCrownY = crown;
        if (abs > maxAbsJoint) maxAbsJoint = abs;
        if (crown < minCrownY) minCrownY = crown;
        sumAbsJoint += abs;
        sumJointSq += abs * abs;
        // Sway-never-topple: stem lean, hinge pinning, base slide and the
        // crown tip's horizontal excursion over the whole run.
        float trunkLean = TrunkLeanDeg();
        if (trunkLean > maxTrunkLeanDeg) maxTrunkLeanDeg = trunkLean;
        float pinned = HingePinnedFraction();
        if (pinned > maxPinnedFraction) maxPinnedFraction = pinned;
        float baseSlide = BaseSlideM();
        if (baseSlide > maxBaseSlideM) maxBaseSlideM = baseSlide;
        Vector2 crownTip = CrownTip();
        if (samples == 0) startCrownTipX = crownTip.x;
        float amp = Mathf.Abs(crownTip.x - startCrownTipX);
        if (amp > swayAmpM) swayAmpM = amp;
        SampleOrderMaxAngles(orderMaxAngle);
        if (!settled && time >= 2f)
        {
            settleAbsJoint = abs;
            settled = true;
        }
        samples++;
        trace.Step(time);
    }

    public string BuildSummaryJson(float simulatedSeconds, float wallSeconds = -1f)
    {
        StringBuilder json = new StringBuilder(512);
        json.Append('{');
        AppendNum(json, "simulatedSeconds", simulatedSeconds, false);
        if (wallSeconds >= 0f)
            AppendNum(json, "wallPerSim", simulatedSeconds > 0f ? wallSeconds / simulatedSeconds : 0f);
        json.Append(",\"samples\":").Append(samples.ToString(CultureInfo.InvariantCulture));
        AppendMetrics(json);
        if (info != null)
        {
            AppendNum(json, "duration", info.Duration);
            AppendStr(json, "sourceFingerprint", info.SourceFingerprint);
        }
        json.Append('}');
        return json.ToString();
    }

    // Run block: measurements only. Every build constant lives in `build`.
    private void AppendMetrics(StringBuilder json)
    {
        float mean = samples > 0 ? (float)(sumAbsJoint / samples) : 0f;
        float rms = samples > 0 ? (float)System.Math.Sqrt(sumJointSq / samples) : 0f;
        float drop = startCrownY - minCrownY;
        AppendNum(json, "maxAbsJointAngle", maxAbsJoint);
        AppendNum(json, "meanAbsJointAngle", mean);
        AppendNum(json, "rmsJointAngle", rms);
        AppendNum(json, "settleAbsJointAngle", settleAbsJoint);
        AppendNum(json, "finalAbsJointAngle", lastAbsJoint);
        AppendNum(json, "crownDrop", drop);
        AppendNum(json, "startCrownY", startCrownY);
        AppendNum(json, "minCrownY", minCrownY < float.MaxValue ? minCrownY : 0f);
        AppendNum(json, "finalCrownY", lastCrownY);
        // Crown height above the root as it stands at the end of the run — the
        // key this recorder has always written, read from the segments.
        AppendNum(json, "appleHeightActualM", CrownY() - tree.transform.position.y);
        AppendNum(json, "maxTrunkLeanDeg", maxTrunkLeanDeg);
        AppendNum(json, "hingePinnedFraction", maxPinnedFraction);
        AppendNum(json, "baseSlideM", maxBaseSlideM);
        AppendNum(json, "swayAmpM", swayAmpM);
        // The trunk joint angle is read as a PAIR with its own limit: a lean
        // number without the stop it is measured against says nothing.
        AppendNum(json, "trunkJointAngleDeg", trace.TrunkJointAngleDeg);
        AppendNum(json, "trunkLimitDeg", trace.TrunkLimitDeg);
        // Per-step, per-joint trace metrics (TreeJointTrace, Unity primitives).
        AppendNum(json, "maxAbsAppliedTorqueNm", trace.MaxAbsAppliedTorqueNm);
        AppendNum(json, "springSatFraction", trace.SpringSatFraction);
        AppendNum(json, "holdResidualNm", trace.HoldResidualNm);
        AppendNum(json, "maxAbsSegmentWorldDevDeg", trace.MaxAbsSegmentWorldDevDeg);
        AppendNum(json, "crownComDxM", trace.CrownComDxM);
        for (int o = 0; o < orderMaxAngle.Length; o++)
            AppendNum(json, "treeOrder" + o + "MaxAngleDeg", orderMaxAngle[o]);
        AppendBuild(json);
    }

    // Build block: constants of the rig, read once and written into their own
    // object. Nothing here may move with the wind — if it does, it was not a
    // constant.
    private void AppendBuild(StringBuilder json)
    {
        if (tree == null) return;
        StringBuilder b = new StringBuilder(4096);
        if (trace.JointColumnCount > 0)
        {
            AppendStr(b, "buildHash", trace.BuildHash);
            AppendStr(b, "buildHashKind", trace.BuildHashKind);
            AppendNum(b, "fixedDelta", trace.FixedDelta);
            AppendNum(b, "jointColumnCount", trace.JointColumnCount);
            AppendNum(b, "bodyCount", trace.BodyCount);
            AppendNum(b, "kOverStabilityBound", trace.MaxStiffnessLimitBound);
        }
        AppendNum(b, "stumpSpawnX", stumpSpawnX);
        for (int o = 0; o < 4; o++)
        {
            AppendNum(b, "treeOrder" + o + "BindDeg", OrderBindDeg(o));
            AppendNum(b, "treeOrder" + o + "LimitDeg", OrderLimitDeg(o));
            AppendNum(b, "treeOrder" + o + "Stiffness", OrderStiffness(o));
            AppendNum(b, "treeOrder" + o + "GravityGradient", OrderGravityGradient(o));
        }
        b.Append(",\"treeSegmentCount\":").Append(segmentCount.ToString(CultureInfo.InvariantCulture));
        b.Append(",\"treeDynamicCount\":").Append(dynamicCount.ToString(CultureInfo.InvariantCulture));
        b.Append(",\"treeRigidbodyCount\":").Append(rigidbodyCount.ToString(CultureInfo.InvariantCulture));
        b.Append(",\"treeWoodColliderCount\":").Append(woodColliderCount.ToString(CultureInfo.InvariantCulture));
        b.Append(",\"treePerchPadCount\":0");
        AppendStr(b, "treeRig", "Sway");
        AppendStr(b, "treeKind", kindName);
        b.Append(",\"appleSeed\":").Append(seed.ToString(CultureInfo.InvariantCulture));
        AppendNumRaw(b, "appleHeightM", appleHeightM.ToString("0.###", CultureInfo.InvariantCulture));
        b.Append(",\"treeSeed\":").Append(seed.ToString(CultureInfo.InvariantCulture));
        b.Append(",\"treeLeafCount\":").Append(leafCount.ToString(CultureInfo.InvariantCulture));
        b.Append(",\"treeFruitCount\":").Append(fruitCount.ToString(CultureInfo.InvariantCulture));
        AppendNum(b, "treeWindSpeed", windSpeed);
        AppendNum(b, "treeWindScale", windScale);
        AppendNum(b, "treeWindGust", windGust);
        AppendNum(b, "treeWindPeriod", windPeriod);
        // Hold torques are build-time feedforward, not a measurement: computed
        // here from the springs the wood carries, never through GetHoldStats.
        AppendNum(b, "meanAbsHoldTorque", meanHoldTorque);
        AppendNum(b, "maxAbsHoldTorque", maxHoldTorque);
        trace.AppendJointBuildJson(b);

        string body = b.ToString();
        json.Append(",\"build\":{");
        json.Append(body.StartsWith(",") ? body.Substring(1) : body);
        json.Append('}');
    }

    // ---- sensor math: Unity primitives only ---------------------------------

    private float CrownY()
    {
        float y = tree.transform.position.y;
        for (int i = 0; i < wood.Count; i++)
        {
            HumanSegment seg = wood[i].segment;
            if (seg == null) continue;
            float top = seg.transform.position.y + seg.size.y * 0.5f;
            if (top > y) y = top;
        }
        return y;
    }

    private float MaxAbsJointAngle()
    {
        float max = 0f;
        for (int i = 0; i < sensors.Count; i++)
        {
            HingeJoint2D joint = sensors[i].joint;
            if (joint == null) continue;
            float abs = Mathf.Abs(Mathf.DeltaAngle(sensors[i].wood.bindDeg, joint.jointAngle));
            if (abs > max) max = abs;
        }
        return max;
    }

    private void SampleOrderMaxAngles(float[] maxAngleDeg)
    {
        if (maxAngleDeg == null) return;
        for (int i = 0; i < sensors.Count; i++)
        {
            HingeJoint2D joint = sensors[i].joint;
            if (joint == null) continue;
            int o = Mathf.Clamp(sensors[i].wood.order, 0, maxAngleDeg.Length - 1);
            float abs = Mathf.Abs(Mathf.DeltaAngle(sensors[i].wood.bindDeg, joint.jointAngle));
            if (abs > maxAngleDeg[o]) maxAngleDeg[o] = abs;
        }
    }

    private float TrunkLeanDeg()
    {
        for (int i = 0; i < sensors.Count; i++)
        {
            if (sensors[i].wood.order != 0) continue;
            HumanSegment seg = sensors[i].wood.segment;
            if (seg == null) continue;
            if (sensors[i].rb != null)
                return Mathf.Abs(Mathf.DeltaAngle(0f, sensors[i].rb.rotation));
            return Mathf.Abs(Mathf.DeltaAngle(0f, seg.transform.eulerAngles.z));
        }
        return 0f;
    }

    private float HingePinnedFraction()
    {
        int dynamic = 0, pinned = 0;
        const float epsDeg = 0.2f;
        for (int i = 0; i < sensors.Count; i++)
        {
            HingeJoint2D joint = sensors[i].joint;
            if (joint == null) continue;
            dynamic++;
            JointAngleLimits2D lim = joint.limits;
            float angle = joint.jointAngle;
            if (Mathf.Abs(angle - lim.min) <= epsDeg || Mathf.Abs(angle - lim.max) <= epsDeg)
                pinned++;
        }
        return dynamic > 0 ? (float)pinned / dynamic : 0f;
    }

    private float BaseSlideM()
    {
        for (int i = 0; i < wood.Count; i++)
        {
            if (wood[i].order >= 0) continue;
            HumanSegment seg = wood[i].segment;
            if (seg == null) continue;
            return Mathf.Abs(seg.transform.position.x - stumpSpawnX);
        }
        return 0f;
    }

    private Vector2 CrownTip()
    {
        Vector2 best = tree.transform.position;
        float bestY = float.MinValue;
        for (int i = 0; i < wood.Count; i++)
        {
            HumanSegment seg = wood[i].segment;
            if (seg == null) continue;
            float rad = seg.transform.eulerAngles.z * Mathf.Deg2Rad;
            Vector2 tip = (Vector2)seg.transform.position
                + new Vector2(-Mathf.Sin(rad), Mathf.Cos(rad)) * (seg.size.y * 0.5f);
            if (tip.y > bestY) { bestY = tip.y; best = tip; }
        }
        return best;
    }

    // ---- per-order build snapshot ------------------------------------------

    private float OrderBindDeg(int order)
    {
        float max = 0f;
        for (int i = 0; i < sensors.Count; i++)
        {
            if (Mathf.Clamp(sensors[i].wood.order, 0, 3) != order) continue;
            float abs = Mathf.Abs(sensors[i].wood.bindDeg);
            if (abs > max) max = abs;
        }
        return max;
    }

    private float OrderLimitDeg(int order)
    {
        float max = 0f;
        for (int i = 0; i < sensors.Count; i++)
        {
            if (Mathf.Clamp(sensors[i].wood.order, 0, 3) != order) continue;
            HingeJoint2D joint = sensors[i].joint;
            if (joint == null) continue;
            JointAngleLimits2D lim = joint.limits;
            float half = (lim.max - lim.min) * 0.5f;
            if (half > max) max = half;
        }
        return max;
    }

    private float OrderStiffness(int order)
    {
        float max = 0f;
        for (int i = 0; i < sensors.Count; i++)
        {
            if (Mathf.Clamp(sensors[i].wood.order, 0, 3) != order) continue;
            TreeSpring spring = sensors[i].wood.spring;
            if (spring == null) continue;
            if (spring.stiffness > max) max = spring.stiffness;
        }
        return max;
    }

    private float OrderGravityGradient(int order)
    {
        float max = 0f;
        for (int i = 0; i < sensors.Count; i++)
        {
            if (Mathf.Clamp(sensors[i].wood.order, 0, 3) != order) continue;
            TreeSpring spring = sensors[i].wood.spring;
            if (spring == null) continue;
            if (spring.gravityGradient > max) max = spring.gravityGradient;
        }
        return max;
    }

    private static void AppendNum(StringBuilder json, string key, float value, bool comma = true)
    {
        if (comma) json.Append(',');
        json.Append('"').Append(key).Append("\":");
        json.Append(value.ToString("0.########", CultureInfo.InvariantCulture));
    }

    // For keys whose value string is already formatted (appleHeightM was
    // written with 0.### before this cycle and keeps that shape).
    private static void AppendNumRaw(StringBuilder json, string key, string value)
    {
        json.Append(',').Append('"').Append(key).Append("\":").Append(value);
    }

    private static void AppendStr(StringBuilder json, string key, string value)
    {
        json.Append(",\"").Append(key).Append("\":\"");
        if (!string.IsNullOrEmpty(value))
            json.Append(value.Replace("\\", "\\\\").Replace("\"", "\\\""));
        json.Append('"');
    }
}
