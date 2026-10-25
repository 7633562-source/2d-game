using System.IO;
using System.Text;
using UnityEngine;

// WORLD-1 (req-285): the world's own recorder. Separate files only (_world.csv,
// _worldev.csv, _world.json), so every existing reader keeps its bytes.
public sealed class WorldStats
{
    public int Steps;
    public float T0;
    public int BrainCount;
    public int BrainTicks;
    public double TickUs;
    public int BenchTicks;
    public long AllocMax;
    public int AllocSteps;
    public int[] TickCounts = new int[0];
    public int[] ActCounts = new int[0];
    public int EventCount;
    public int GaitGapTicks;
    public float HeroMaxSpeed, HeroMaxJump, HeroMaxVx;
    public int HeroSteps;
    public int[] PostureCount = new int[4];

    public void Init(int brains)
    {
        BrainCount = brains;
        TickCounts = new int[brains];
        ActCounts = new int[brains * 8];
    }
}

public static class WorldNum
{
    // Fixed decimals into a StringBuilder without a string per number and without
    // the machine's culture (a comma decimal would break every CSV reader).
    public static void F(StringBuilder sb, float v, int dec)
    {
        if (float.IsNaN(v)) { sb.Append("nan"); return; }
        if (float.IsInfinity(v)) { sb.Append(v > 0f ? "inf" : "-inf"); return; }
        bool neg = v < 0f;
        if (neg) v = -v;
        if (neg) sb.Append('-');
        long scale = 1;
        for (int i = 0; i < dec; i++) scale *= 10;
        long ip = (long)v;
        long frac = (long)Mathf.Round((v - ip) * scale);
        if (frac >= scale) { ip++; frac -= scale; }
        sb.Append(ip);
        if (dec > 0)
        {
            sb.Append('.');
            long d = scale / 10;
            while (d > 0)
            {
                sb.Append((char)('0' + (int)((frac / d) % 10)));
                d /= 10;
            }
        }
    }
}

public sealed class WorldRecorder
{
    public const string Header =
        "step,t,actor,id,act,prev,why,tgtX,tgtY,gait,applied,u1,u1Act,u2,u2Act,noiseEp,draws,n1,n2,n3,n4,x,y,vx,ctl,heard,roll,rollP";

    public int Log = 1;
    public bool Visual;
    public string Label = "";
    public int Seed = 1;
    public WorldStats Stats = new WorldStats();

    readonly StringBuilder csv = new StringBuilder(1 << 20);
    readonly StringBuilder ev = new StringBuilder(1 << 16);
    readonly StringBuilder row = new StringBuilder(512);
    StreamWriter csvOut, evOut;
    bool begun;

    public int CsvRows { get; private set; }
    public int EvRows { get; private set; }

    public void Begin()
    {
        if (begun) return;
        begun = true;
        if (!Visual) return;
        // -visual: the owner closes the window and there is no OnApplicationQuit,
        // so the rows go out as they are made.
        csvOut = new StreamWriter(new FileStream(Path.Combine(Folder, Label + "_world.csv"),
            FileMode.Create, FileAccess.Write, FileShare.Read), new UTF8Encoding(false));
        evOut = new StreamWriter(new FileStream(Path.Combine(Folder, Label + "_worldev.csv"),
            FileMode.Create, FileAccess.Write, FileShare.Read), new UTF8Encoding(false));
        csvOut.AutoFlush = true;
        evOut.AutoFlush = true;
        csvOut.WriteLine(Header);
        evOut.WriteLine("step,t,actor,id,event,x,y,other,detail");
    }

    public string Folder = ".";

    public void SetFolder(string folder) { Folder = folder; }

    void EmitRow()
    {
        CsvRows++;
        if (csvOut != null)
        {
            csvOut.Write(row.ToString());
            csvOut.Write('\n');
        }
        else
        {
            csv.Append(row);
            csv.Append('\n');
        }
    }

    void EmitEvent()
    {
        EvRows++;
        if (evOut != null)
        {
            evOut.Write(row.ToString());
            evOut.Write('\n');
        }
        else
        {
            ev.Append(row);
            ev.Append('\n');
        }
    }

    static void PostureName(StringBuilder sb, HeroPosture p)
    {
        switch (p)
        {
            case HeroPosture.Walk: sb.Append("Walk"); break;
            case HeroPosture.Squat: sb.Append("Squat"); break;
            case HeroPosture.Fallen: sb.Append("Fallen"); break;
            default: sb.Append("Stand"); break;
        }
    }

    public void HeroRow(int step, float t, ref HeroState h, HeroPosture prev, int heardMask)
    {
        row.Length = 0;
        row.Append(step);
        row.Append(',');
        WorldNum.F(row, t, 3);
        row.Append(",H,0,");
        PostureName(row, h.posture);
        row.Append(',');
        PostureName(row, prev);
        row.Append(",-,0,0,-,-,0,-1,0,-1,-1,0,");
        WorldNum.F(row, h.stillT, 3); row.Append(',');
        row.Append((int)h.posture); row.Append(',');
        WorldNum.F(row, h.crowFid, 3); row.Append(",0,");
        WorldNum.F(row, h.x, 4); row.Append(',');
        WorldNum.F(row, h.y, 4); row.Append(',');
        WorldNum.F(row, h.vx, 4); row.Append(',');
        PostureName(row, h.posture); row.Append(',');
        row.Append(heardMask); row.Append(",-1,-1");
        EmitRow();
    }

    public void BrainRow(WorldBrain b, char actor, int id, int step, float t, Vector2 p, float vx, int ctlCode)
    {
        row.Length = 0;
        row.Append(step);
        row.Append(',');
        WorldNum.F(row, t, 3);
        row.Append(',');
        row.Append(actor);
        row.Append(',');
        row.Append(id);
        row.Append(',');
        row.Append(b.ActName(b.Act));
        row.Append(',');
        row.Append(b.ActName(b.PrevAct));
        row.Append(',');
        row.Append(WorldWhy.Names[b.Why >= 0 && b.Why < WorldWhy.Names.Length ? b.Why : 0]);
        row.Append(',');
        WorldNum.F(row, b.TgtX, 3); row.Append(',');
        WorldNum.F(row, b.TgtY, 3); row.Append(',');
        row.Append(b.GaitName); row.Append(',');
        row.Append(b.AppliedName); row.Append(',');
        WorldNum.F(row, b.LastBestU, 3); row.Append(',');
        row.Append(b.LastBestAct); row.Append(',');
        WorldNum.F(row, b.LastSecondU, 3); row.Append(',');
        row.Append(b.LastSecondAct); row.Append(',');
        row.Append(b.NoiseEpoch); row.Append(',');
        row.Append(b.Draws); row.Append(',');
        WorldNum.F(row, b.N1, 3); row.Append(',');
        WorldNum.F(row, b.N2, 3); row.Append(',');
        WorldNum.F(row, b.N3, 3); row.Append(',');
        WorldNum.F(row, b.N4, 3); row.Append(',');
        WorldNum.F(row, p.x, 4); row.Append(',');
        WorldNum.F(row, p.y, 4); row.Append(',');
        WorldNum.F(row, vx, 4); row.Append(',');
        row.Append(ctlCode); row.Append(',');
        row.Append(b.HeardMask); row.Append(',');
        WorldNum.F(row, b.RollIdx, 0); row.Append(',');
        WorldNum.F(row, b.RollP, 3);
        EmitRow();
    }

    public void Event(int step, float t, char actor, int id, string evName, Vector2 p, int other, string detail)
    {
        row.Length = 0;
        row.Append(step);
        row.Append(',');
        WorldNum.F(row, t, 3);
        row.Append(',');
        row.Append(actor);
        row.Append(',');
        row.Append(id);
        row.Append(',');
        row.Append(evName);
        row.Append(',');
        WorldNum.F(row, p.x, 4); row.Append(',');
        WorldNum.F(row, p.y, 4); row.Append(',');
        row.Append(other);
        row.Append(',');
        row.Append(detail);
        EmitEvent();
    }

    // Take end: the rows and the summary. In -visual the rows are already on disk.
    public void Write(string folder, string label)
    {
        if (!string.IsNullOrEmpty(folder)) Folder = folder;
        if (!string.IsNullOrEmpty(label)) Label = label;
        if (!begun) Begin();
        if (csvOut != null) { csvOut.Flush(); csvOut.Dispose(); csvOut = null; }
        if (evOut != null) { evOut.Flush(); evOut.Dispose(); evOut = null; }

        var utf8 = new UTF8Encoding(false);
        string csvBody = Header + "\n" + csv.ToString();
        string evBody = "step,t,actor,id,event,x,y,other,detail\n" + ev.ToString();
        if (!Visual)
        {
            File.WriteAllText(Path.Combine(Folder, Label + "_world.csv"), csvBody, utf8);
            File.WriteAllText(Path.Combine(Folder, Label + "_worldev.csv"), evBody, utf8);
        }
        string md5 = Md5Of(utf8.GetBytes(csvBody));
        File.WriteAllText(Path.Combine(Folder, Label + "_world.json"), Json(md5), utf8);
    }

    static string Md5Of(byte[] data)
    {
        using (var md5 = System.Security.Cryptography.MD5.Create())
        {
            byte[] h = md5.ComputeHash(data);
            var sb = new StringBuilder(32);
            for (int i = 0; i < h.Length; i++) sb.Append(h[i].ToString("x2"));
            return sb.ToString();
        }
    }

    string Json(string md5)
    {
        var sb = new StringBuilder(2048);
        sb.Append("{\n");
        sb.Append(" \"label\": \"").Append(Label).Append("\",\n");
        sb.Append(" \"seed\": ").Append(Seed).Append(",\n");
        sb.Append(" \"log\": ").Append(Log).Append(",\n");
        sb.Append(" \"visual\": ").Append(Visual ? "true" : "false").Append(",\n");
        sb.Append(" \"steps\": ").Append(Stats.Steps).Append(",\n");
        sb.Append(" \"t0\": ");
        WorldNum.F(sb, Stats.T0, 4);
        sb.Append(",\n \"ticks\": ").Append(Stats.BrainTicks).Append(",\n");
        sb.Append(" \"rows\": ").Append(CsvRows).Append(",\n");
        sb.Append(" \"events\": ").Append(Stats.EventCount).Append(",\n");
        sb.Append(" \"md5\": \"").Append(md5).Append("\",\n");
        sb.Append(" \"brains\": [");
        for (int i = 0; i < Stats.BrainCount; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append("\n  {\"i\": ").Append(i).Append(", \"ticks\": ").Append(Stats.TickCounts[i]).Append(", \"acts\": [");
            for (int a = 0; a < 8; a++) { if (a > 0) sb.Append(','); sb.Append(Stats.ActCounts[i * 8 + a]); }
            sb.Append("]}");
        }
        sb.Append("\n ],\n");
        sb.Append(" \"hero\": {\"steps\": ").Append(Stats.HeroSteps).Append(", \"posture\": [");
        for (int a = 0; a < 4; a++) { if (a > 0) sb.Append(','); sb.Append(Stats.PostureCount[a]); }
        sb.Append("]},\n");
        sb.Append(" \"rails\": {\"heroMaxSpeed\": ");
        WorldNum.F(sb, Stats.HeroMaxSpeed, 4);
        sb.Append(", \"heroMaxJump\": ");
        WorldNum.F(sb, Stats.HeroMaxJump, 5);
        sb.Append(", \"heroMaxVx\": ");
        WorldNum.F(sb, Stats.HeroMaxVx, 4);
        sb.Append("},\n");
        sb.Append(" \"bench\": {\"ticks\": ").Append(Stats.BenchTicks).Append(", \"usPerTick\": ");
        WorldNum.F(sb, Stats.BenchTicks > 0 ? (float)(Stats.TickUs / Stats.BenchTicks) : 0f, 4);
        sb.Append(", \"allocMaxPerStep\": ").Append(Stats.AllocMax).Append(", \"allocSteps\": ").Append(Stats.AllocSteps).Append("},\n");
        sb.Append(" \"gaitGap\": ");
        WorldNum.F(sb, Stats.BrainTicks > 0 ? (float)Stats.GaitGapTicks / Stats.BrainTicks : 0f, 4);
        sb.Append("\n}\n");
        return sb.ToString();
    }
}
