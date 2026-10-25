using UnityEngine;

// WORLD-1 (req-285): the world brain template. Order is fixed in Tick:
// Perceive, Needs, Reflex, HardEnd, dwell, Score, Choose, Enter, Emit.
// One System.Random per actor, the actor's only stream; no shared draws.
public static class WorldWhy
{
    public const int None = 0, Choice = 1, Roll = 2, Reflex = 3, Timeout = 4;
    public const int Lost = 5, Refuge = 6, Far = 7, Call = 8, Fallen = 9;
    public static readonly string[] Names =
    {
        "-", "choice", "roll", "reflex", "timeout", "lost", "refuge", "far", "call", "fallen"
    };
}

public abstract class WorldBrain
{
    protected readonly System.Random rng;
    public readonly int Offset;
    public readonly int ActCount;

    public int Act, PrevAct, Why, Draws, NoiseEpoch;
    public float ActT0, LastTick;

    protected readonly float[] U;       // per act; -Infinity = not available
    protected readonly float[] Noise;   // held Gumbel noise
    readonly float[] scored;
    protected float noiseUntil, commit, temp, minDwell;

    // Recorder reads these; a brain fills what it has (step 1 has no consumer).
    public float N1, N2, N3, N4;
    public float TgtX, TgtY;
    public int RollIdx = -1;
    public float RollP = -1f;
    public int HeardMask;
    protected int lastHeardStep;
    protected readonly WorldSoundEvent[] heardBuf = new WorldSoundEvent[8];
    public string GaitName = "-";
    public string AppliedName = "-";
    public float LastBestU, LastSecondU;
    public int LastBestAct = -1, LastSecondAct = -1;

    protected WorldBrain(int seed, int offset, int actCount, float commitGain, float temperature, float dwell)
    {
        rng = new System.Random(seed);
        Offset = offset;
        ActCount = actCount;
        U = new float[actCount];
        Noise = new float[actCount];
        scored = new float[actCount];
        for (int a = 0; a < actCount; a++) U[a] = float.NegativeInfinity;
        commit = commitGain;
        temp = temperature;
        minDwell = dwell;
        Act = 0;
        PrevAct = 0;
        Why = WorldWhy.None;
    }

    public void Tick(ref WorldSnapshot s, WorldSoundBus bus, WorldClock c)
    {
        float now = c.Now;
        float dtb = now - LastTick;
        LastTick = now;

        Perceive(ref s, bus, c);
        Needs(dtb);
        if (Reflex(now)) { Emit(); return; }
        if (HardEnd(now)) { Emit(); return; }
        if (now - ActT0 < minDwell) { Emit(); return; }
        Score(now);
        Why = WorldWhy.Choice;
        int a = Choose(now);
        if (a != Act) Enter(a, now);
        Emit();
    }

    // Held Gumbel noise, refreshed every 2-3 s with exactly actCount + 1 draws.
    // Ties go to the lower act index.
    protected int Choose(float now)
    {
        if (now >= noiseUntil)
        {
            for (int a = 0; a < ActCount; a++) Noise[a] = Gumbel();
            NoiseEpoch++;
            noiseUntil = now + 2f + Next01();
        }
        int best = -1, second = -1;
        float bu = float.NegativeInfinity, su = float.NegativeInfinity;
        for (int a = 0; a < ActCount; a++)
        {
            float v = U[a] + (a == Act ? commit : 0f) + temp * Noise[a];
            scored[a] = v;
            if (v > bu) { su = bu; second = best; bu = v; best = a; }
            else if (v > su) { su = v; second = a; }
        }
        LastBestU = bu;
        LastBestAct = best;
        LastSecondU = su;
        LastSecondAct = second;
        return best < 0 ? Act : best;
    }

    protected float Next01()
    {
        Draws++;
        return (float)rng.NextDouble();
    }

    protected bool Roll(float p)
    {
        Draws++;
        return (float)rng.NextDouble() < p;
    }

    protected float Gumbel()
    {
        double u = (double)Mathf.Clamp((float)rng.NextDouble(), 1e-9f, 1f - 1e-9f);
        Draws++;
        return (float)(-System.Math.Log(-System.Math.Log(u)));
    }

    // Sounds heard since this brain's last tick; returns the kind bitmask.
    protected int AcceptSounds(WorldSoundBus bus, int nowStep, Vector2 p)
    {
        if (bus == null) return 0;
        int n = bus.Heard(lastHeardStep, nowStep, p, heardBuf);
        lastHeardStep = nowStep;
        int mask = 0;
        for (int i = 0; i < n && i < heardBuf.Length; i++) mask |= 1 << (int)heardBuf[i].kind;
        HeardMask = mask;
        return mask;
    }

    protected virtual void Enter(int act, float now)
    {
        PrevAct = Act;
        Act = act;
        ActT0 = now;
    }

    protected virtual void Emit()
    {
        AppliedName = GaitName;
    }

    public abstract string ActName(int a);
    protected abstract void Perceive(ref WorldSnapshot s, WorldSoundBus bus, WorldClock c);
    protected abstract void Needs(float dtb);
    protected abstract bool Reflex(float now);
    protected abstract bool HardEnd(float now);
    protected abstract void Score(float now);
}
