using UnityEngine;

// Ground bird: Sit / Walk / Peck. No Fly, Glide, or perch return.
// Seeded — the stand must repeat.
public sealed class ChickenWander
{
    public int walkCount;
    public int sitCount;
    public int peckCount;
    public int facingFlipCount;

    public float peckMin = 0.28f;
    public float peckMax = 0.55f;
    public float peckGapMin = 0.55f;
    public float peckGapMax = 1.4f;

    private System.Random rng;
    private enum Phase
    {
        Sit,
        Walk
    }

    private Phase phase = Phase.Sit;
    private float phaseEnds;
    private float nextTurn;
    private float nextPeck;
    private float peckEnds;
    private bool pecking;
    private float facing = 1f;
    private float tempo = 1f;

    // Insect hunt does not Step. An expired Sit bout used to EnterWalk
    // (and Peck) the first Forage frame after the eat.
    public void HoldHuntSit(float now, float sitMin, float sitMax)
    {
        if (phase == Phase.Walk)
        {
            EnterSit(now, sitMin, sitMax);
            return;
        }

        pecking = false;
        if (phase == Phase.Sit && now >= phaseEnds)
            phaseEnds = now + Tempo(Rand(0.85f, 2.2f));
    }

    public void Reset(int seed, float now, float sitMin, float sitMax, float startFacing)
    {
        rng = new System.Random(seed);
        tempo = 0.78f + (float)rng.NextDouble() * 0.50f;
        facing = startFacing >= 0f ? 1f : -1f;
        phase = Phase.Sit;
        phaseEnds = now + Tempo(Rand(sitMin, sitMax));
        nextTurn = now + 999f;
        walkCount = 0;
        sitCount = 0;
        peckCount = 0;
        facingFlipCount = 0;
        pecking = false;
        nextPeck = now + 999f;
        peckEnds = now;
    }

    public void Step(
        float x,
        float homeX,
        float now,
        float sitMin,
        float sitMax,
        float walkMin,
        float walkMax,
        float turnMin,
        float turnMax,
        float leash,
        out BirdMode mode,
        out float face)
    {
        if (phase == Phase.Sit && now >= phaseEnds)
            EnterWalk(now, walkMin, walkMax, turnMin, turnMax);

        if (phase == Phase.Walk)
        {
            if (leash > 0.01f && Mathf.Abs(x - homeX) > leash)
                SetFace(x > homeX ? -1f : 1f);
            else if (now >= nextTurn)
            {
                SetFace(-facing);
                nextTurn = now + Rand(turnMin, turnMax);
            }

            if (now >= phaseEnds)
                EnterSit(now, sitMin, sitMax);
            else
                TickPeck(now);
        }

        mode = phase == Phase.Sit
            ? BirdMode.Sit
            : (pecking ? BirdMode.Peck : BirdMode.Walk);
        face = facing;
    }

    private void TickPeck(float now)
    {
        if (pecking)
        {
            if (now >= peckEnds)
            {
                pecking = false;
                nextPeck = now + Tempo(Rand(peckGapMin, peckGapMax));
            }

            return;
        }

        if (now >= nextPeck)
        {
            pecking = true;
            peckCount++;
            peckEnds = now + Tempo(Rand(peckMin, peckMax));
        }
    }

    private void EnterSit(float now, float sitMin, float sitMax)
    {
        phase = Phase.Sit;
        sitCount++;
        phaseEnds = now + Tempo(Rand(sitMin, sitMax));
        nextTurn = now + 999f;
        pecking = false;
        nextPeck = now + 999f;
    }

    private void EnterWalk(float now, float walkMin, float walkMax, float turnMin, float turnMax)
    {
        phase = Phase.Walk;
        walkCount++;
        if (rng.Next(0, 2) == 0)
            SetFace(-facing);
        phaseEnds = now + Tempo(Rand(walkMin, walkMax));
        nextTurn = now + Rand(turnMin, turnMax);
        pecking = false;
        nextPeck = now + Tempo(Rand(peckGapMin, peckGapMax));
    }

    private void SetFace(float sign)
    {
        float next = sign >= 0f ? 1f : -1f;
        if (next != facing)
            facingFlipCount++;
        facing = next;
    }

    private float Tempo(float seconds)
    {
        return seconds * tempo;
    }

    private float Rand(float min, float max)
    {
        if (max < min)
            max = min;
        return (float)(min + rng.NextDouble() * (max - min));
    }
}
