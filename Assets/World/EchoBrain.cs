using UnityEngine;

// WORLD-1 (req-285): a bench-only brain. Three acts with constant utilities plus
// the held noise of the base template. Exists to exercise WorldBrain and to be
// timed by -worldBench 1; it reads the snapshot and writes nothing.
public sealed class EchoBrain : WorldBrain
{
    public EchoBrain(int seed, int offset, int who)
        : base(seed, offset, 3, 0.05f, 0.45f, 0.05f)
    {
        Who = who;
        U[0] = 0.62f;
        U[1] = 0.58f;
        U[2] = 0.54f;
        N1 = seed;
    }

    public readonly int Who;

    public override string ActName(int a)
    {
        if (a == 0) return "peck";
        if (a == 1) return "hop";
        return "gaze";
    }

    protected override void Perceive(ref WorldSnapshot s, WorldSoundBus bus, WorldClock c)
    {
        AcceptSounds(bus, c.Step, new Vector2(s.hero.x, s.hero.y));
        N2 = s.hero.stillT;
        N3 = s.hasDog ? 1f : 0f;
        N4 = 0f;
    }

    protected override void Needs(float dtb)
    {
        // constant utilities: nothing to integrate
    }

    protected override bool Reflex(float now)
    {
        return false;
    }

    protected override bool HardEnd(float now)
    {
        return false;
    }

    protected override void Score(float now)
    {
        U[0] = 0.62f;
        U[1] = 0.58f;
        U[2] = 0.54f;
    }
}
