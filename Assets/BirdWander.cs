using UnityEngine;

// Случайный полёт без сил: только mode и знак носа.
// Сид обязателен — на стенде повтор должен совпасть.
public sealed class BirdWander
{
    public int takeoffCount;
    public int landCount;
    public int facingFlipCount;
    public int perchLandCount;
    public int dirtRejectCount;

    private System.Random rng;
    private enum Phase
    {
        Sit,
        Fly,
        Glide
    }

    private Phase phase = Phase.Sit;
    private float phaseEnds;
    private float nextTurn;
    private float facing = 1f;
    private bool wasAirborne;

    public void Reset(int seed, float now, float sitMin, float sitMax, float startFacing)
    {
        rng = new System.Random(seed);
        facing = startFacing >= 0f ? 1f : -1f;
        phase = Phase.Sit;
        phaseEnds = now + Rand(sitMin, sitMax);
        nextTurn = now + 999f;
        wasAirborne = false;
        takeoffCount = 0;
        landCount = 0;
        facingFlipCount = 0;
        perchLandCount = 0;
        dirtRejectCount = 0;
    }

    public void Step(
        bool grounded,
        float x,
        float homeX,
        float now,
        float sitMin,
        float sitMax,
        float flyMin,
        float flyMax,
        float glideLead,
        float turnMin,
        float turnMax,
        float leash,
        bool wantPerch,
        bool onPerch,
        float homeDeadzone,
        out BirdMode mode,
        out float face)
    {
        if (!grounded)
            wasAirborne = true;
        else if (wasAirborne)
        {
            wasAirborne = false;
            if (wantPerch && !onPerch)
            {
                // Земля — не дом. Снова в воздух, нос к слоту.
                dirtRejectCount++;
                EnterFly(now, flyMin, flyMax, turnMin, turnMax);
                SetFace(x > homeX ? -1f : 1f);
            }
            else
            {
                landCount++;
                if (wantPerch && onPerch)
                    perchLandCount++;
                EnterSit(now, sitMin, sitMax);
            }
        }

        if (phase == Phase.Sit && grounded && now >= phaseEnds)
            EnterFly(now, flyMin, flyMax, turnMin, turnMax);

        if (phase == Phase.Fly)
        {
            if (leash > 0.01f && Mathf.Abs(x - homeX) > leash)
                SetFace(x > homeX ? -1f : 1f);
            else if (!wantPerch && now >= nextTurn)
            {
                SetFace(-facing);
                nextTurn = now + Rand(turnMin, turnMax);
            }

            // Glide lead: leave Fly this many seconds before phaseEnds.
            // lead <= 0 keeps the old edge (glide at phaseEnds). No new mode.
            float lead = Mathf.Max(0f, glideLead);
            if (now >= phaseEnds - lead)
                phase = Phase.Glide;
        }

        // Glide-to-slot: x chatters through homeX every Update.
        // Inside the deadzone keep the nose — do not flip ±X each frame.
        if (phase == Phase.Glide && wantPerch)
            FaceHome(x, homeX, homeDeadzone);

        if (phase == Phase.Sit)
            mode = BirdMode.Sit;
        else if (phase == Phase.Fly)
            mode = BirdMode.Fly;
        else
            mode = BirdMode.Glide;

        face = facing;
    }

    private void EnterSit(float now, float sitMin, float sitMax)
    {
        phase = Phase.Sit;
        phaseEnds = now + Rand(sitMin, sitMax);
        nextTurn = now + 999f;
    }

    private void EnterFly(float now, float flyMin, float flyMax, float turnMin, float turnMax)
    {
        phase = Phase.Fly;
        takeoffCount++;
        if (rng.Next(0, 2) == 0)
            SetFace(-facing);
        phaseEnds = now + Rand(flyMin, flyMax);
        nextTurn = now + Rand(turnMin, turnMax);
    }

    private void FaceHome(float x, float homeX, float deadzone)
    {
        float dx = x - homeX;
        if (Mathf.Abs(dx) <= Mathf.Max(0f, deadzone))
            return;
        SetFace(dx > 0f ? -1f : 1f);
    }

    private void SetFace(float sign)
    {
        float next = sign >= 0f ? 1f : -1f;
        if (next != facing)
            facingFlipCount++;
        facing = next;
    }

    private float Rand(float min, float max)
    {
        if (max < min)
            max = min;
        return (float)(min + rng.NextDouble() * (max - min));
    }
}
