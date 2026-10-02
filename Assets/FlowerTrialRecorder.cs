using System.Globalization;
using System.Text;

// Flower stand: picture plants, no joints. Read-only.
public class FlowerTrialRecorder
{
    private readonly PlantFlower[] flowers;
    private readonly TrialRunInfo info;

    public FlowerTrialRecorder(PlantFlower[] flowers, TrialRunInfo info)
    {
        this.flowers = flowers;
        this.info = info;
    }

    public string BuildSummaryJson(float simulatedSeconds, float wallSeconds = -1f)
    {
        StringBuilder json = new StringBuilder(512);
        json.Append("{");
        AppendNum(json, "simulatedSeconds", simulatedSeconds, false);
        if (wallSeconds >= 0f)
            AppendNum(json, "wallPerSim", simulatedSeconds > 0f ? wallSeconds / simulatedSeconds : 0f);
        int n = flowers != null ? flowers.Length : 0;
        json.Append(",\"flowerCount\":").Append(n.ToString(CultureInfo.InvariantCulture));
        json.Append(",\"kinds\":[");
        int parts = 0;
        float maxStem = 0f;
        for (int i = 0; i < n; i++)
        {
            PlantFlower f = flowers[i];
            if (i > 0) json.Append(',');
            json.Append('"');
            if (f != null)
            {
                json.Append(f.ResolvedKind.ToString());
                parts += f.PartCount;
                if (f.StemHeight > maxStem) maxStem = f.StemHeight;
            }
            json.Append('"');
        }
        json.Append(']');
        json.Append(",\"flowerPartCount\":").Append(parts.ToString(CultureInfo.InvariantCulture));
        AppendNum(json, "maxStemHeight", maxStem);
        if (info != null)
        {
            AppendNum(json, "duration", info.Duration);
            json.Append(",\"sourceFingerprint\":\"");
            if (!string.IsNullOrEmpty(info.SourceFingerprint))
                json.Append(info.SourceFingerprint.Replace("\\", "\\\\").Replace("\"", "\\\""));
            json.Append('"');
        }
        json.Append("}");
        return json.ToString();
    }

    private static void AppendNum(StringBuilder json, string key, float value, bool comma = true)
    {
        if (comma) json.Append(',');
        json.Append('"').Append(key).Append("\":");
        json.Append(value.ToString("0.########", CultureInfo.InvariantCulture));
    }
}
