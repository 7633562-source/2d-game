using System.Globalization;
using System.Text;
using UnityEngine;

// Apple-tree stand metrics. Read-only, no forces. Emits the same flat keys
// as PlantTrialRecorder so the shared probe metrics script reads it unchanged.
public class AppleTrialRecorder
{
    private readonly TreeBase tree;
    private readonly TrialRunInfo info;
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

    public AppleTrialRecorder(TreeBase tree, TrialRunInfo info)
    {
        this.tree = tree;
        this.info = info;
        if (tree != null)
            startCrownY = tree.GetCrownY();
    }

    public void Sample(float time)
    {
        if (tree == null) return;
        float abs = tree.GetMaxAbsJointAngle();
        float crown = tree.GetCrownY();
        if (samples == 0)
            startCrownY = crown;
        lastAbsJoint = abs;
        lastCrownY = crown;
        if (abs > maxAbsJoint) maxAbsJoint = abs;
        if (crown < minCrownY) minCrownY = crown;
        sumAbsJoint += abs;
        sumJointSq += abs * abs;
        if (!settled && time >= 2f)
        {
            settleAbsJoint = abs;
            settled = true;
        }
        samples++;
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
        if (tree != null)
        {
            json.Append(",\"treeSegmentCount\":").Append(tree.SegmentCount.ToString(CultureInfo.InvariantCulture));
            json.Append(",\"treeDynamicCount\":").Append(tree.DynamicBodyCount.ToString(CultureInfo.InvariantCulture));
            json.Append(",\"treeRigidbodyCount\":").Append(tree.RigidbodyCount.ToString(CultureInfo.InvariantCulture));
            json.Append(",\"treeWoodColliderCount\":").Append(tree.WoodColliderCount.ToString(CultureInfo.InvariantCulture));
            json.Append(",\"treePerchPadCount\":0");
            AppendStr(json, "treeRig", "Sway");
            AppendStr(json, "treeKind", tree.KindName);
            json.Append(",\"appleSeed\":").Append(tree.seed.ToString(CultureInfo.InvariantCulture));
            if (tree is AppleTree apple)
                json.Append(",\"appleHeightM\":").Append(apple.appleHeightM.ToString("0.###", CultureInfo.InvariantCulture));
            AppendNum(json, "appleHeightActualM", tree.HeightMeters);
            json.Append(",\"treeSeed\":").Append(tree.seed.ToString(CultureInfo.InvariantCulture));
            json.Append(",\"treeLeafCount\":").Append(tree.LeafCount.ToString(CultureInfo.InvariantCulture));
            json.Append(",\"treeFruitCount\":").Append(tree.FruitCount.ToString(CultureInfo.InvariantCulture));
            AppendNum(json, "treeWindSpeed", tree.windSpeed);
            AppendNum(json, "treeWindScale", tree.windScale);
            AppendNum(json, "treeWindGust", tree.windGust);
            AppendNum(json, "treeWindPeriod", tree.windPeriod);
            tree.GetHoldStats(out float meanHold, out float maxHold);
            AppendNum(json, "meanAbsHoldTorque", meanHold);
            AppendNum(json, "maxAbsHoldTorque", maxHold);
        }
    }

    private static void AppendNum(StringBuilder json, string key, float value, bool comma = true)
    {
        if (comma) json.Append(',');
        json.Append('"').Append(key).Append("\":");
        json.Append(value.ToString("0.########", CultureInfo.InvariantCulture));
    }

    private static void AppendStr(StringBuilder json, string key, string value)
    {
        json.Append(",\"").Append(key).Append("\":\"");
        if (!string.IsNullOrEmpty(value))
            json.Append(value.Replace("\\", "\\\\").Replace("\"", "\\\""));
        json.Append('"');
    }
}
