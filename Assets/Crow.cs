using UnityEngine;

// Flying canon. Same body build as Bird; this type holds the crow recipe.
// Pack mob / drum startle are drive overlays (BirdDrive / BirdFlockDrive), not forces.
// Drum startle is one takeoff plus drumStartleCool — not a weld to the pecker.
public class Crow : Bird
{
    public const float MobNotice = 12f;
    public const float MobHold = 3.4f;
    public const float MobStoop = 6f;
    public const float DrumStartle = 5f;
    // After a drum takeoff, ignore further pulses. 0 on the drive = old weld.
    public const float DrumStartleCool = 8f;

    public override bool WantsTreeSeat => true;
    public override bool ScalesFlapWithAirspeed => true;
    public override bool UsesDirtLandingFlap => true;
    public override BirdMode FleeBodyMode => BirdMode.Fly;
    public override BirdForageStyle ForageStyle => BirdForageStyle.CrowCruise;
    public override float HitHealth => 10f;
    public override float PeckDamage => 0.5f;

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
        b.kind = BirdKind.Crow;
        b.totalMass = 0.5f;
        b.bodySize = new Vector2(0.14f, 0.06f);
        b.neckSize = new Vector2(0.028f, 0.035f);
        b.headSize = new Vector2(0.042f, 0.038f);
        b.beakSize = new Vector2(0.032f, 0.012f);
        b.tailSize = new Vector2(0.09f, 0.022f);
        b.wingHumerusSize = new Vector2(0.032f, 0.055f);
        b.wingUlnaSize = new Vector2(0.040f, 0.070f);
        b.wingHandSize = new Vector2(0.070f, 0.110f);
        b.thighSize = new Vector2(0.022f, 0.040f);
        b.shankSize = new Vector2(0.018f, 0.055f);
        b.footSize = new Vector2(0.05f, 0.022f);
    }

    internal static void WriteFlight(BirdFlight flight)
    {
        if (flight == null) return;
        // Climb over a 3 m apple, then circle. Excess hover fades to a hold
        // at perchCruiseHeight. Thrust along facing must beat leftover-X bleed.
        // Glide is a slow sink onto dirt, not the old v² stall-dive.
        flight.hoverMean = 1.58f;
        flight.forwardThrust = 1.8f;
        flight.pitchTarget = 8f;
        flight.glideLift = 0.86f;
        flight.glideSinkSpeed = 0.65f;
        flight.flareHeight = 1.8f;
        flight.perchCruiseHeight = 4.4f;
        flight.horizontalDamping = 2.4f;
        flight.cruiseSpeed = 2.2f;
        // B-TAKEOFF-2: the scripted heave is off. A crow's height is not a
        // sine — it is bursts of flaps and the glides between them; the
        // intermittent flap-glide carries that shape now. The surge (speed)
        // stays: the phugoid oval is the KEEP, the scripted BOB is the trap.
        flight.cruiseHeave = 0f;
        flight.cruiseHeave2 = 0f;
        flight.cruiseGlideChance = 0.85f;
        flight.cruiseGlideHold = 0.85f;
        flight.cruiseGlidePeriod = 3.4f;
    }

    internal static void WriteControl(BirdController control)
    {
        // Crow keeps the BirdController field defaults (cruise flap, walk),
        // except the cadence: B-TAKEOFF-2 measured the old 7 Hz base against
        // the airspeed scale — the crow flapped at 7.7…8.2 Hz in the takeoff
        // burst and 8.2 Hz in cruise (a blur at 25 fps, and no burst at all).
        // 3.3 Hz base × the 1.28 top scale = ~4.2 Hz at cruise, inside the
        // 3.5…4.5 Hz crow band, and the takeoff's own 6.2 Hz burst stands
        // 1.45x over it.
        control.flapFrequency = 3.3f;
    }
}
