using UnityEngine;

// Tree drummer: long thin beak, hop not cruise, Sit+Peck on the pad.
// A perched Peck pulse startles nearby crows via BirdSocial.Drum — not Alarm.
// Crows ForceFly once, then drumStartleCool; the pulse is a rising edge, not a weld.
public class Woodpecker : Bird
{
    public const float DrumPulseHold = 0.7f;

    public override bool WantsTreeSeat => true;
    public override bool ScalesFlapWithAirspeed => false;
    public override bool UsesDirtLandingFlap => false;
    public override BirdMode FleeBodyMode => BirdMode.Fly;
    public override BirdForageStyle ForageStyle => BirdForageStyle.WoodpeckerDrum;
    public override float HitHealth => 10f;
    public override float PeckDamage => 0.7f;

    protected override void ApplySpeciesLayout()
    {
        WriteLayout(this);
    }

    protected override void ApplySpeciesFlight()
    {
        WriteFlight(flight);
    }

    protected override void ApplySpeciesControl()
    {
        WriteControl(controller);
    }

    internal static void WriteLayout(Bird b)
    {
        b.kind = BirdKind.Woodpecker;
        b.totalMass = 0.45f;
        b.bodySize = new Vector2(0.13f, 0.055f);
        b.neckSize = new Vector2(0.024f, 0.032f);
        b.headSize = new Vector2(0.038f, 0.034f);
        // Long thin beak — the species mark. Drumming is a closed-bill
        // hammer on Peck mode (BirdController), not a wood force.
        b.beakSize = new Vector2(0.078f, 0.010f);
        b.tailSize = new Vector2(0.07f, 0.020f);
        b.wingHumerusSize = new Vector2(0.028f, 0.046f);
        b.wingUlnaSize = new Vector2(0.032f, 0.055f);
        b.wingHandSize = new Vector2(0.048f, 0.072f);
        b.thighSize = new Vector2(0.022f, 0.040f);
        b.shankSize = new Vector2(0.018f, 0.055f);
        b.footSize = new Vector2(0.05f, 0.022f);
        b.beakMuscleTorque = 0.10f;
        b.neckMuscleTorque = 0.16f;
    }

    internal static void WriteFlight(BirdFlight flight)
    {
        if (flight == null) return;
        // Hop between trunks. hoverMean < 1 cannot hold a cruise.
        flight.hoverMean = 0.62f;
        flight.forwardThrust = 0.16f;
        flight.glideLift = 0.38f;
        flight.glideSinkSpeed = 0f;
        flight.pitchTarget = 6f;
        flight.flarePitch = 14f;
        flight.flareHeight = 0.55f;
        flight.verticalDamping = 5.0f;
    }

    internal static void WriteControl(BirdController control)
    {
        if (control == null) return;
        control.flapFrequency = 8f;
        control.walkThrust = 1.1f;
        control.walkSpeed = 0.22f;
        control.takeoffDelay = 0f;
        control.sitHipAngle = 22f;
        control.peckDipDegrees = 32f;
        control.mode = BirdMode.Sit;
    }
}

// Sit on the pad and drum. Rare short Fly, then Glide home. Dirt is not home.
public sealed class WoodpeckerWander
{
    public int takeoffCount;
    public int walkCount;
    public int sitCount;
    public int peckCount;
    public int facingFlipCount;
    public int perchLandCount;
    public int dirtRejectCount;

    public float peckMin = 0.14f;
    public float peckMax = 0.28f;
    public float peckGapMin = 0.35f;
    public float peckGapMax = 0.85f;

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
    private float nextPeck;
    private float peckEnds;
    private bool pecking;
    // True while the Sit bout is in a Peck stroke — the drum. Overlay reads this.
    public bool Drumming => pecking;
    private float facing = 1f;
    private bool wasAirborne;

    // Insect hunt does not Step. A leftover Fly/Glide clock used to
    // take off the first Forage frame after the eat.
    public void HoldHuntSit(float now, float sitMin, float sitMax)
    {
        if (phase == Phase.Fly || phase == Phase.Glide)
        {
            EnterSit(now, sitMin, sitMax);
            return;
        }

        pecking = false;
        if (phase == Phase.Sit && now >= phaseEnds)
            phaseEnds = now + Rand(0.85f, 2.2f);
    }

    public void Reset(int seed, float now, float sitMin, float sitMax, float startFacing)
    {
        rng = new System.Random(seed);
        facing = startFacing >= 0f ? 1f : -1f;
        phase = Phase.Sit;
        phaseEnds = now + Rand(sitMin, sitMax);
        nextTurn = now + Rand(2.2f, 5.5f);
        wasAirborne = false;
        pecking = false;
        nextPeck = now + Rand(peckGapMin, peckGapMax);
        peckEnds = now;
        takeoffCount = 0;
        walkCount = 0;
        sitCount = 0;
        peckCount = 0;
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
                dirtRejectCount++;
                EnterFly(now, flyMin, flyMax, turnMin, turnMax);
                SetFace(x > homeX ? -1f : 1f);
            }
            else
            {
                sitCount++;
                if (wantPerch && onPerch)
                    perchLandCount++;
                EnterSit(now, sitMin, sitMax);
            }
        }

        if (phase == Phase.Sit && grounded)
        {
            if (now >= nextTurn)
            {
                SetFace(-facing);
                nextTurn = now + Rand(turnMin, turnMax);
            }

            TickPeck(now);

            if (now >= phaseEnds)
            {
                // About one sit in seven leaves the pad for a short hop.
                if (rng.Next(0, 7) == 0)
                    EnterFly(now, flyMin, flyMax, turnMin, turnMax);
                else
                    EnterSit(now, sitMin, sitMax);
            }
        }

        if (phase == Phase.Fly)
        {
            if (leash > 0.01f && Mathf.Abs(x - homeX) > leash)
                SetFace(x > homeX ? -1f : 1f);
            else if (now >= nextTurn)
            {
                SetFace(-facing);
                nextTurn = now + Rand(turnMin, turnMax);
            }

            float lead = Mathf.Max(0f, glideLead);
            if (now >= phaseEnds - lead)
                phase = Phase.Glide;
        }

        if (phase == Phase.Glide && wantPerch)
        {
            float dx = x - homeX;
            if (Mathf.Abs(dx) > 0.15f)
                SetFace(dx > 0f ? -1f : 1f);
        }

        if (phase == Phase.Sit)
            mode = pecking ? BirdMode.Peck : BirdMode.Sit;
        else if (phase == Phase.Fly)
            mode = BirdMode.Fly;
        else
            mode = BirdMode.Glide;

        face = facing;
    }

    private void TickPeck(float now)
    {
        if (pecking)
        {
            if (now >= peckEnds)
            {
                pecking = false;
                nextPeck = now + Rand(peckGapMin, peckGapMax);
            }

            return;
        }

        if (now < nextPeck)
            return;

        pecking = true;
        peckCount++;
        peckEnds = now + Rand(peckMin, peckMax);
    }

    private void EnterSit(float now, float sitMin, float sitMax)
    {
        phase = Phase.Sit;
        phaseEnds = now + Rand(sitMin, sitMax);
        pecking = false;
        nextPeck = now + Rand(peckGapMin, peckGapMax);
    }

    private void EnterFly(float now, float flyMin, float flyMax, float turnMin, float turnMax)
    {
        phase = Phase.Fly;
        takeoffCount++;
        pecking = false;
        if (rng.Next(0, 2) == 0)
            SetFace(-facing);
        phaseEnds = now + Rand(flyMin, flyMax);
        nextTurn = now + Rand(turnMin, turnMax);
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
