using UnityEngine;

// Ground hen. Heavier, short-winged hop. Forage is ChickenWander (agent ai).
// Loose leader + neighbor peck are drive overlays, not a new wander FSM.
public class Chicken : Bird
{
    public const float FollowSlack = 1.35f;
    public const float PeckTogether = 0.9f;

    public override bool WantsTreeSeat => false;
    public override bool ScalesFlapWithAirspeed => false;
    public override bool UsesDirtLandingFlap => false;
    public override BirdMode FleeBodyMode => BirdMode.Walk;
    public override BirdForageStyle ForageStyle => BirdForageStyle.ChickenGround;
    public override float HitHealth => 8f;
    public override float PeckDamage => 0.35f;

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
        b.kind = BirdKind.Chicken;
        b.totalMass = 1.8f;
        b.bodySize = new Vector2(0.18f, 0.10f);
        b.neckSize = new Vector2(0.024f, 0.048f);
        b.headSize = new Vector2(0.040f, 0.036f);
        // Comb/wattle are picture on Head Visual in Bird.Build — not sizes.
        b.beakSize = new Vector2(0.046f, 0.016f);
        b.tailSize = new Vector2(0.055f, 0.040f);
        b.wingHumerusSize = new Vector2(0.028f, 0.040f);
        b.wingUlnaSize = new Vector2(0.030f, 0.048f);
        b.wingHandSize = new Vector2(0.042f, 0.058f);
        b.thighSize = new Vector2(0.030f, 0.050f);
        b.shankSize = new Vector2(0.022f, 0.060f);
        b.footSize = new Vector2(0.055f, 0.024f);
        b.minBodyInertia = 0.028f;
    }

    internal static void WriteFlight(BirdFlight flight)
    {
        if (flight == null) return;
        // < 1: a hop, not a cruise. Brain should prefer Walk/Sit.
        flight.hoverMean = 0.55f;
        flight.forwardThrust = 0.12f;
        flight.glideLift = 0.32f;
        flight.glideSinkSpeed = 0f;
        flight.pitchTarget = 4f;
        flight.flarePitch = 14f;
        flight.flareHeight = 0.45f;
        flight.verticalDamping = 5.5f;
    }

    internal static void WriteControl(BirdController control)
    {
        if (control == null) return;
        control.flapFrequency = 4f;
        control.walkThrust = 1.6f;
        control.walkSpeed = 0.40f;
        control.takeoffDelay = 0f;
        control.sitHipAngle = 18f;
        // ChickenWander already writes Walk; 0 kept the crow stride.
        control.walkHopAmp = 1f;
        control.mode = BirdMode.Sit;
    }
}
