using UnityEngine;

// Simplified flight: not a plate on every link, but one force on the body.
// The flap is a switch: Fly gives thrust on the beat; Glide is a slow sink
// (crow) or a stall (chicken). Without a flap there is no lift. Pitch is a
// spring, otherwise a fall goes as a somersault.
[DefaultExecutionOrder(50)]
public class BirdFlight : MonoBehaviour
{
    [Header("Thrust")]
    [Tooltip("Mean lift as a fraction of weight under a continuous flap. >1 — slowly gains height.")]
    public float hoverMean = 1.12f;
    [Tooltip("Thrust fraction on the upstroke. Below 1 — the bird pulses slightly with the flap.")]
    [Range(0f, 1f)]
    public float upstrokeLift = 0.4f;
    [Tooltip("Glide: fraction of weight. Crow dirt descent is a slow sink, not a stall.")]
    [Range(0f, 1.2f)]
    public float glideLift = 0.86f;
    [Tooltip("Glide sink cap, m/s. 0 — no cap (chicken hop). Crow holds this downward speed.")]
    public float glideSinkSpeed = 0.65f;
    [Tooltip("Forward thrust along the body, N, in time with the flap.")]
    public float forwardThrust = 0.35f;
    [Tooltip("Vertical viscosity, N·s/m. So it does not fly off like a rocket.")]
    public float verticalDamping = 3.5f;
    [Tooltip("Bleed leftover ±X after a facing flip, N·s/m. 0 — coast. Applied against facing, not along cruise.")]
    public float horizontalDamping = 0f;
    [Tooltip("Cap |vx| along facing in Fly, m/s. 0 — no cap. Glide ignores this.")]
    public float cruiseSpeed = 0f;
    [Tooltip("Pitch hold in flight, N·m/rad. Otherwise the flap gesture loops.")]
    public float pitchStiffness = 6f;
    [Tooltip("Pitch damper, N·m·s/rad.")]
    public float pitchDamping = 0.45f;
    [Tooltip("Target pitch in flight, degrees. Plus — nose up.")]
    public float pitchTarget = 8f;
    [Tooltip("Ground flare, degrees. Kills vertical speed before touchdown.")]
    public float flarePitch = 22f;
    [Tooltip("Flare height above stance, m. Start early or a 4 m glide still hits.")]
    public float flareHeight = 1.8f;
    [Tooltip("Stoop: lift as a fraction of weight. Below 1 — sinks nose-forward.")]
    public float attackLift = 0.42f;
    [Tooltip("Stoop: thrust along the nose, N.")]
    public float attackThrust = 1.15f;
    [Tooltip("Stoop: pitch, degrees. Minus — nose down at facing +1.")]
    public float attackPitch = -22f;
    [Tooltip("Hover fraction when above cruise. <1 — settle back to perchCruiseHeight.")]
    [Range(0f, 1f)]
    public float perchCruiseClimbScale = 0.35f;
    [Tooltip("Hold this height over stance, m. Excess hover fades here so the flock circles, not bobs.")]
    public float perchCruiseHeight = 4.4f;

    [Header("Cruise path")]
    [Tooltip("Thrust oscillation in Fly. 0 — constant speed. Out of phase with heave: a phugoid oval, not a straight cruise.")]
    [Range(0f, 0.8f)]
    public float cruiseSurge = 0.38f;
    [Tooltip("Lift oscillation as a fraction of weight. Climb and sink trade with speed.")]
    [Range(0f, 0.5f)]
    public float cruiseHeave = 0.16f;
    [Tooltip("Phugoid rate, rad/s. Slow — a long ellipse, not a buzz.")]
    public float cruiseOrbitRate = 0.85f;
    [Tooltip("Second, slower heave, fraction of weight. A second frequency keeps the oval from repeating.")]
    [Range(0f, 0.3f)]
    public float cruiseHeave2 = 0.07f;
    [Tooltip("Second orbit rate, rad/s.")]
    public float cruiseOrbitRate2 = 0.37f;
    [Tooltip("Pitch weave on top of pitchTarget, degrees. Heading follows the oval.")]
    public float cruisePitchWeave = 7f;

    [Header("Cruise variety")]
    [Tooltip("Chance a Fly pocket holds the wing (slow sink). 0 — phugoid only. Seeded per bird.")]
    [Range(0f, 1f)]
    public float cruiseGlideChance = 0.40f;
    [Tooltip("Held-wing pocket length, s.")]
    public float cruiseGlideHold = 1.35f;
    [Tooltip("Seconds between glide-pocket attempts. flapPhaseOffset staggers the flock.")]
    public float cruiseGlidePeriod = 6.4f;
    [Tooltip("Look-bank amplitude in Fly, −1…1. Picture only — sagittal world has no yaw motor.")]
    [Range(0f, 1f)]
    public float cruiseBankLook = 0.38f;
    [Tooltip("Heave dip around a facing flip, fraction of weight. A banked turn, not a stall.")]
    [Range(0f, 0.3f)]
    public float turnBankHeave = 0.10f;
    [Tooltip("Dirt approach: shallow dive starts this many metres above flareHeight. 0 — no swoop.")]
    public float swoopLead = 1.55f;
    [Tooltip("Swoop nose-down, degrees. Subtracted from the shallow glide pitch.")]
    public float swoopPitch = 11f;
    [Tooltip("Swoop extra sink as a fraction of weight.")]
    [Range(0f, 0.25f)]
    public float swoopHeave = 0.11f;

    [Header("State (read only)")]
    public Vector2 lastForce;
    public float lastLiftY;
    // 0…1. BirdController spreads the wing when Fly is in a held-wing pocket.
    [System.NonSerialized] public float cruiseGlideBlend;
    // Look roll, −1…1. Composed with brain playRoll in LateUpdate.
    [System.NonSerialized] public float bankLook;
    // 0…1. Height inside flareHeight. Picture only (tail fan), not a force.
    [System.NonSerialized] public float flareLook;
    // 0…1. Copy of the dirt-swoop envelope. Picture only.
    [System.NonSerialized] public float swoopLook;

    private Rigidbody2D rb;
    private Bird bird;
    private BirdController control;
    private BirdSensors sensors;
    private BodyAero aero;
    private float lastFace = 1f;
    private float flipSoftUntil;
    private bool faceLatched;
    private float swoopBlend;
    private bool huntLiftLatch;

    // true — BirdController calls Tick; own FixedUpdate returns.
    [System.NonSerialized]
    public bool drivenExternally;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        bird = GetComponentInParent<Bird>();
        control = bird != null ? bird.controller : GetComponentInParent<BirdController>();
        aero = GetComponent<BodyAero>();
        if (rb == null)
            Debug.LogError("BirdFlight needs a Rigidbody2D on the body.");
    }

    void FixedUpdate()
    {
        if (drivenExternally) return;
        Tick();
    }

    public void Tick()
    {
        lastForce = Vector2.zero;
        lastLiftY = 0f;
        cruiseGlideBlend = 0f;
        bankLook = 0f;
        flareLook = 0f;
        swoopLook = 0f;
        swoopBlend = 0f;
        if (rb == null) return;
        if (control == null && bird != null)
            control = bird.controller;
        if (control == null) return;
        if (control.energyRest)
            return;
        if (sensors == null && bird != null)
            sensors = bird.sensors;

        bool flapping = control.mode == BirdMode.Fly;
        bool gliding = control.mode == BirdMode.Glide;
        bool attacking = control.mode == BirdMode.Attack;
        bool landFlap = control.WantsDirtLandingFlap();
        // Flock: the spring is always on. After touchdown, perch friction spins the body
        // (bird_flock_coast: landing at 1°, already 55° after 0.4 s).
        bool holdPitch = flapping || gliding || attacking || control.flockRig;
        // Dirt eat, including one Update where preferDirtLand dropped
        // before Hurt. IsHuntSoil also covers dirt Walk near the plate
        // and dirt-height Peck with a leftover pad (AI drop hasPerch;
        // body ReleasePerch lagged) so lift/aero stay off and HuntBeak
        // can Rel Hostile. Body HoldSimulatedSupport / walk thrust now
        // use the same IsHuntSoil gate (not preferDirtLand alone). Pad
        // Peck (woodpecker drum, high) still uses the perch pitch spring —
        // flattening there hops the bill off the trunk.
        // Wander Glide near dirt stays off this gate so landFlap keeps lift.
        bool huntSoil = control.IsHuntSoil();
        if (huntSoil)
            huntLiftLatch = true;
        else if (huntLiftLatch)
        {
            huntSoil = true;
            huntLiftLatch = false;
        }

        float mass = bird != null ? bird.totalMass : rb.mass;
        float weight = mass * Mathf.Abs(Physics2D.gravity.y);

        // Leftover Fly/Glide/Attack while the brain already asked for dirt
        // (insect Walk/Peck) still applied hoverMean or landFlap > weight
        // and never entered OnDirtPlate / Peck. Gravity takes that body
        // to the plate. Pad Glide still beats (WantsDirtLandingFlap is
        // false on preferDirtLand). Lift stays a flap on real Fly/Glide/Attack.
        if ((flapping || gliding || attacking) && !huntSoil)
            ApplyLiftAndThrust(flapping, gliding, attacking, landFlap, weight);

        // Dirt hunt: horizon this step so BeakStrike Rel Hostile is an
        // axis-aligned slab. A pitch spring on leftover Fly fought that
        // snap (eulerZ ≠ 0) and aero ate walkThrust on the close.
        if (aero != null && !huntSoil)
            aero.Apply();
        if (huntSoil)
        {
            // Sleeping leftover Sit kept pitch until Walk WakeUp, so
            // leftover Fly rotation yanked walkThrust off ±X. Snap
            // Transform too: autoSyncTransforms is off. HuntBeak itself
            // is a world-axis Default query (physics pose, live Dynamic
            // solid vs the Insect kinematic trigger, park unsim on
            // Walk, not body-space, not two trigger sensors). A live
            // Dynamic on hunt Walk sat in the flock 200 Hz island.
            if (control != null)
                control.SnapHuntHorizon();
            else
            {
                // Same as AlignHuntBodyHorizon: leftover Fly pitch yanks
                // walkThrust. HuntBeak is a world-axis physics pose, not this Transform.
                if (rb.IsSleeping())
                    rb.WakeUp();
                rb.angularVelocity = 0f;
                if (Mathf.Abs(rb.rotation) > 0.0001f)
                    rb.rotation = 0f;
                Transform bodyT = rb.transform;
                Vector2 pose = rb.position;
                Vector3 tp = bodyT.position;
                bodyT.SetPositionAndRotation(
                    new Vector3(pose.x, pose.y, tp.z),
                    Quaternion.identity);
            }
            Vector2 v = rb.linearVelocity;
            float stanceH = rb.position.y - Bird.StanceRootY(bird);
            // Brain Pecks in InPeckWindow (Pursue close), not only on
            // the 8 cm film. Halt vx on the whole Peck so leftover Fly
            // does not walk HuntBeak off Rel Hostile. Walk still caps
            // on the film only — an air cap at 0.45 m killed a 4 m/s close.
            if (control.mode == BirdMode.Peck)
            {
                v.x = 0f;
            }
            else if (stanceH < BirdController.HuntSoilFilm)
            {
                float cap = control.walkSpeed > 0.01f ? control.walkSpeed : 0.7f;
                if (v.x > cap) v.x = cap;
                else if (v.x < -cap) v.x = -cap;
            }
            // Ragdoll leftover Fly has no HoldSimulatedSupport. A down
            // punch through the plate moved the HuntBeak slab off the
            // Insect (BeakStrike never Rel Hostile). Snap only when the
            // body is below stance: a position write every film step is
            // a teleport and froze walkThrust (comX stuck, Peck never
            // sat on Rel Hostile). Kill downward vy on the film. Not a
            // flap. Air hunt still falls.
            if (stanceH < 0f)
            {
                Vector2 p = rb.position;
                p.y = Bird.StanceRootY(bird);
                rb.position = p;
                v.y = 0f;
            }
            else if (stanceH < 0.04f && v.y < 0f)
                v.y = 0f;
            rb.linearVelocity = v;
            // HuntBeak pose is Rigidbody2D.position (Default query
            // body, not a Body child). Strip is body-X centred,
            // PeckHold both ways — leftover SetFacing must not teleport
            // Rel Hostile off the Insect. Wake bakes offset 0 at dirt
            // while unsim, then simulated=true — ParkY baked into the
            // fixture left OverlapCollider at dirt+80. A Y snap after
            // PoseLiveHuntSlab used to leave a compound slab a metre off
            // the Insect. Rewrite X after this pose so Rel Hostile still
            // tracks walkThrust. Hunt Walk keeps HuntBeak parked
            // (BIRD-12: no 200 Hz Dynamic island). Peck/Attack eat
            // is the live query. No GetContacts, no SyncTransforms.
            // Eat stays BeakStrike.
            if (control != null
                && (control.mode == BirdMode.Peck || control.mode == BirdMode.Attack))
                control.RefreshHuntBeakPose();
        }
        else if (sensors != null && pitchStiffness > 0f && holdPitch)
            ApplyPitch(PitchGoal(flapping, gliding, attacking));

        if ((flapping || gliding) && !huntSoil)
            UpdateLookBank(flapping);

        swoopLook = swoopBlend;
        if ((flapping || gliding) && rb != null)
        {
            float height = rb.position.y - Bird.StanceRootY(bird);
            flareLook = 1f - Mathf.Clamp01(height / Mathf.Max(0.08f, flareHeight));
        }
    }

    private void ApplyLiftAndThrust(
        bool flapping, bool gliding, bool attacking, bool landFlap, float weight)
    {
        Vector2 velocity = rb.linearVelocity;
        float lift;
        float thrustN;
        float extraDrag = 0f;
        bool glideSink = false;
        float speedCap = cruiseSpeed;
        float heightNow = rb.position.y - Bird.StanceRootY(bird);
        // Dirt swoop lives above flareHeight. landFlap is already inside
        // the flare (h ≤ flare) so it never sees the window — do not
        // compute it there. Fly must still descend (vy < 0) or takeoff
        // through 1.8…3.3 m would nose down on the climb.
        if (gliding || flapping)
            swoopBlend = ComputeSwoopBlend(heightNow);

        if (attacking)
        {
            // Stoop: not a hover. Lift below weight — sinks along the nose.
            lift = weight * attackLift;
            thrustN = attackThrust;
        }
        else if (landFlap)
        {
            // Dirt landing still beats the wing. Held-wing Glide is the
            // pad approach; soil under the stump is a slow flap, not a stall.
            float down = Mathf.Clamp(control.downstrokeFraction, 0.25f, 0.5f);
            float beat = control.debugDownstroke ? 1f : upstrokeLift;
            float meanBeat = down * 1f + (1f - down) * upstrokeLift;
            float envelope = beat / Mathf.Max(0.05f, meanBeat);
            float height = rb.position.y - Bird.StanceRootY(bird);
            float flareT = 1f - Mathf.Clamp01(height / Mathf.Max(0.08f, flareHeight));
            float mean = Mathf.Lerp(1.04f, 1.01f, flareT * flareT);
            if (control.IsHuntSoil())
                mean = Mathf.Lerp(glideLift, 0.94f, flareT * flareT);
            lift = weight * mean * Mathf.Lerp(1f, envelope, 0.32f);
            thrustN = forwardThrust * Mathf.Lerp(0.08f, 0.03f, flareT) * envelope;
            glideSink = hoverMean > 1.01f && glideSinkSpeed > 0.02f;
            float speedNow = velocity.magnitude;
            if (speedNow > 0.3f)
                extraDrag = Mathf.Min(0.5f * 1.2f * speedNow * speedNow * 0.04f * 0.7f, 8f);
        }
        else if (flapping)
        {
            float down = Mathf.Clamp(control.downstrokeFraction, 0.25f, 0.5f);
            float beat = control.debugDownstroke ? 1f : upstrokeLift;
            float meanBeat = down * 1f + (1f - down) * upstrokeLift;
            float envelope = beat / Mathf.Max(0.05f, meanBeat);
            // Excess hover is takeoff. A hard *scale at the cap (and only
            // inside NearPerch) climbed then dumped — a vertical bob, not
            // an orbit. Fade to a hold at cruise; the brain's SetFacing
            // then has a body that still moves along X.
            float hover = HoverForHeight();
            float surge = 1f;
            float heave = 0f;
            // Hunt Fly must sink onto dirt. CruiseOrbit heave (0.16+0.07)
            // on leftover hover climbed to 4.4 m; BeakStrike never Rel Hostile.
            bool huntSoil = control.IsHuntSoil();
            if (hoverMean > 1.01f && !huntSoil)
                CruiseOrbit(out surge, out heave, out speedCap);
            lift = weight * (hover + heave) * envelope;
            thrustN = forwardThrust * envelope * surge;
            if (huntSoil)
            {
                // Leave a twig, then drop. Leftover heading*thrust plus
                // verticalDamping on vy held a 0.17 m/s hover — 4.4 m
                // never reached the plate in a 20 s hunt. glideSink caps
                // at glideSinkSpeed. WalkSpeed caps leftover cruise so
                // PeckHold still covers the Insect. Not a muscle.
                thrustN = forwardThrust * 0.04f * envelope;
                glideSink = glideSinkSpeed > 0.02f;
                speedCap = control.walkSpeed > 0.01f ? control.walkSpeed : 0.7f;
            }
            float pocket = hoverMean > 1.01f && !huntSoil ? CruiseGlideEnvelope() : 0f;
            cruiseGlideBlend = pocket;
            if (pocket > 0.01f)
            {
                // Held wing: most of weight, slow sink. Still Fly — the brain
                // did not write Glide. Blend so the pocket does not stall.
                float glideL = weight * glideLift;
                float glideT = forwardThrust * 0.12f;
                lift = Mathf.Lerp(lift, glideL, pocket);
                thrustN = Mathf.Lerp(thrustN, glideT, pocket);
                glideSink = pocket > 0.45f && glideSinkSpeed > 0.02f;
                float speedPocket = velocity.magnitude;
                if (speedPocket > 0.3f)
                    extraDrag = Mathf.Min(
                        0.5f * 1.2f * speedPocket * speedPocket * 0.04f * 0.5f * pocket, 8f);
            }
            if (swoopBlend > 0.02f)
            {
                lift -= weight * swoopHeave * swoopBlend;
                thrustN += forwardThrust * 0.22f * swoopBlend;
            }
        }
        else
        {
            // Crow dirt descent: most of weight, slow sink. The old
            // min(v², glideLift) stalled at ~0.12 weight from 4 m — a dive.
            // Chicken hop keeps the stall (hoverMean < 1, no sink cap).
            // Pad Glide still uses this held-wing path (WantsDirtLandingFlap
            // is false above a twig).
            float height = rb.position.y - Bird.StanceRootY(bird);
            float flareT = 1f - Mathf.Clamp01(height / Mathf.Max(0.08f, flareHeight));
            glideSink = gliding && hoverMean > 1.01f && glideSinkSpeed > 0.02f;
            if (glideSink)
            {
                float mean = Mathf.Lerp(glideLift, 1.02f, flareT * flareT);
                lift = weight * mean;
                thrustN = forwardThrust * Mathf.Lerp(0.12f, 0.04f, flareT);
            }
            else
            {
                float speed = velocity.magnitude;
                float q = 0.5f * 1.2f * speed * speed;
                const float glideArea = 0.035f;
                float speedLift = q * glideArea * 1.1f;
                lift = Mathf.Min(weight * glideLift, speedLift + weight * 0.12f);
                thrustN = forwardThrust * 0.15f;
            }

            if (swoopBlend > 0.02f)
            {
                lift -= weight * swoopHeave * swoopBlend;
                thrustN += forwardThrust * 0.28f * swoopBlend;
            }

            float speedNow = velocity.magnitude;
            if (speedNow > 0.3f)
                extraDrag = Mathf.Min(0.5f * 1.2f * speedNow * speedNow * 0.04f * 0.7f, 8f);
        }

        if (glideSink)
        {
            float vy = velocity.y;
            if (vy > 0f)
                lift -= verticalDamping * vy;
            else if (vy < -glideSinkSpeed)
                lift += verticalDamping * (-vy - glideSinkSpeed);
        }
        else
        {
            lift -= verticalDamping * velocity.y;
            if (lift < 0f) lift = 0f;
        }

        // Nose is facing, not transform.right: do not rotate the body 180° (perches would point up).
        float face = control.FacingSign();
        float rad = rb.rotation * Mathf.Deg2Rad;
        Vector2 heading = new Vector2(Mathf.Cos(rad) * face, Mathf.Sin(rad) * face);
        Vector2 force = Vector2.up * lift + heading * thrustN;
        if (extraDrag > 0f)
            force += -velocity.normalized * extraDrag;
        ApplyHorizontalDrag(flapping && !landFlap, attacking, face, velocity, speedCap, ref force);
        rb.AddForce(force, ForceMode2D.Force);
        lastForce = force;
        lastLiftY = force.y;
    }

    // Crow takeoff uses hoverMean > 1. At cruise, hold weight so the
    // remaining flap force is heading (orbit), not another climb pulse.
    private float HoverForHeight()
    {
        if (hoverMean <= 1.01f)
            return hoverMean;
        // Hunt asked for soil. 0.88 * downstroke envelope still climbed
        // (1.40 weight) and never entered OnDirtPlate / Peck.
        if (control != null && control.IsHuntSoil())
            return 0.62f;

        float height = rb.position.y - Bird.StanceRootY(bird);
        float cruise = Mathf.Max(0.2f, perchCruiseHeight);
        if (height < cruise)
            return Mathf.Lerp(hoverMean, 1.02f, height / cruise);

        float overshoot = Mathf.Clamp01((height - cruise) / 0.8f);
        return Mathf.Lerp(1.02f, 0.92f, overshoot);
    }

    // Phugoid: surge (speed) and heave (height) a quarter-cycle apart so
    // the track is an oval, not a constant-speed line. A second slower
    // term keeps the loop from repeating the same ellipse.
    private void CruiseOrbit(out float surge, out float heave, out float speedCap)
    {
        float p1 = OrbitPhase(cruiseOrbitRate);
        float p2 = OrbitPhase(cruiseOrbitRate2);
        float a = Mathf.Sin(p1);
        float b = Mathf.Sin(p2);
        surge = 1f + cruiseSurge * Mathf.Cos(p1) + cruiseSurge * 0.35f * b;
        surge = Mathf.Clamp(surge, 0.55f, 1.55f);
        heave = cruiseHeave * a + cruiseHeave2 * b;
        if (turnBankHeave > 0.005f && Time.fixedTime < flipSoftUntil)
        {
            // Facing just flipped: dip then recover. Picture bank is lookRoot.
            float u = 1f - (flipSoftUntil - Time.fixedTime) / 0.4f;
            u = Mathf.Clamp01(u);
            heave -= turnBankHeave * Mathf.Sin(u * Mathf.PI);
        }
        speedCap = cruiseSpeed > 0.01f
            ? cruiseSpeed * Mathf.Clamp(surge, 0.6f, 1.45f)
            : 0f;
    }

    private float OrbitPhase(float rate)
    {
        float seed = control != null ? control.flapPhaseOffset : 0f;
        return Time.fixedTime * Mathf.Max(0.05f, rate) + seed * 6.2831853f;
    }

    // Isotropic K·vx killed cruise: 0.26 N / 2.4 N·s/m → ~0.1 m/s, so
    // SetFacing never left a hover. Bleed only leftover against the nose;
    // Fly caps along-track speed; Glide dumps so a weave pulse sheds X.
    private void ApplyHorizontalDrag(
        bool flapping, bool attacking, float face, Vector2 velocity, float speedCap, ref Vector2 force)
    {
        if (attacking || horizontalDamping <= 0f)
            return;

        float vx = velocity.x;
        float along = vx * face;
        if (!flapping || along < 0f)
        {
            force.x -= horizontalDamping * vx;
            return;
        }

        if (speedCap > 0.01f && along > speedCap)
            force.x -= horizontalDamping * (vx - face * speedCap);
    }

    private float PitchGoal(bool flapping, bool gliding, bool attacking)
    {
        // Nose up when flying toward −X is a negative world angle, not +8°.
        float face = control.FacingSign();
        // Sit/stance/walk — horizon. Otherwise the 12° flare fired near the ground
        // and Sit held the nose as on landing.
        if (control.mode == BirdMode.Sit
            || control.mode == BirdMode.Stand
            || control.mode == BirdMode.Walk
            || control.mode == BirdMode.Peck)
            return 0f;
        if (attacking) return attackPitch * face;
        if (flapping)
        {
            // Hunt asked for soil. A 16° climb through heading*thrust
            // still lifts leftover Fly to perchCruiseHeight.
            if (control.IsHuntSoil())
                return 0f;
            float flightHeight = rb.position.y - Bird.StanceRootY(bird);
            float goal = pitchTarget;
            // Nose-up only while climbing to the crown. Holding 16° all
            // the way to perchCruiseHeight points heading at the sky.
            float climbUntil = perchCruiseHeight * 0.55f;
            if (hoverMean > 1.01f && flightHeight < climbUntil && swoopBlend < 0.15f)
                goal = Mathf.Max(pitchTarget, 16f);
            if (hoverMean > 1.01f)
            {
                float p1 = OrbitPhase(cruiseOrbitRate);
                float p2 = OrbitPhase(cruiseOrbitRate2);
                goal += cruisePitchWeave * Mathf.Sin(p1)
                    + cruisePitchWeave * 0.4f * Mathf.Sin(p2);
                if (cruiseGlideBlend > 0.15f)
                    goal = Mathf.Lerp(goal, 6f, cruiseGlideBlend);
            }
            if (swoopBlend > 0.02f)
                goal -= swoopPitch * swoopBlend;
            return goal * face;
        }

        float height = rb.position.y - Bird.StanceRootY(bird);
        // Same as TickFlock: dirt uses StandingRootY, a twig uses NearPerch.
        // Descent is a shallow nose-up, not a stoop. Attack keeps attackPitch.
        if (gliding)
        {
            float flareT = 1f - Mathf.Clamp01(height / Mathf.Max(0.08f, flareHeight));
            // Extra nose-up on a high twig. The NearPerch AABB also covers
            // dirt under the stump — flaring there blocks a dirt descent.
            if ((control == null || !control.IsHuntSoil())
                && sensors.nearPerch && height > 1.0f)
                flareT = Mathf.Max(flareT, 0.65f);
            float goal = Mathf.Lerp(8f, flarePitch, flareT);
            if (swoopBlend > 0.02f)
                goal -= swoopPitch * swoopBlend;
            return goal * face;
        }
        return 0f;
    }

    private void ApplyPitch(float target)
    {
        // A brain turn flips the target sign by 16°. A stiff spring loops.
        float face = control.FacingSign();
        if (!faceLatched)
        {
            lastFace = face;
            faceLatched = true;
        }
        else if (Mathf.Abs(face - lastFace) > 0.5f)
        {
            // Do not extend the window: Glide-to-homeX may flip every
            // Update around the slot (BIRD-5). Chatter would pin K at 0.4.
            if (Time.fixedTime >= flipSoftUntil)
                flipSoftUntil = Time.fixedTime + 0.4f;
            lastFace = face;
        }

        float stiff = pitchStiffness;
        if (Time.fixedTime < flipSoftUntil)
            stiff *= 0.4f;

        float errRad = Mathf.DeltaAngle(sensors.bodyPitch, target) * Mathf.Deg2Rad;
        float rateRad = sensors.bodyPitchRate * Mathf.Deg2Rad;
        float torque = stiff * errRad - pitchDamping * rateRad;
        rb.AddTorque(torque, ForceMode2D.Force);
    }

    // Deterministic held-wing pockets. Hash of cycle + flapPhaseOffset so
    // fifty birds do not all spread at once. No Random, no alloc.
    private float CruiseGlideEnvelope()
    {
        if (cruiseGlideChance < 0.02f || cruiseGlidePeriod < 0.5f)
            return 0f;
        float seed = control != null ? control.flapPhaseOffset : 0f;
        float period = Mathf.Max(2.5f, cruiseGlidePeriod);
        float hold = Mathf.Clamp(cruiseGlideHold, 0.35f, period * 0.42f);
        float t = Time.fixedTime + seed * period;
        int cycle = Mathf.FloorToInt(t / period);
        if (CycleGate(cycle, seed) > cruiseGlideChance)
            return 0f;
        float u = t - cycle * period;
        if (u > hold)
            return 0f;
        const float edge = 0.22f;
        float rise = Mathf.Clamp01(u / edge);
        float fall = Mathf.Clamp01((hold - u) / edge);
        return Mathf.Min(rise, fall);
    }

    // Dirt approach only. Pad Glide stays the shallow flare (NearPerch).
    // Window sits above flareHeight so flare still kills leftover fall.
    // Fly takeoff climbs through this band — skip unless already sinking.
    private float ComputeSwoopBlend(float height)
    {
        if (swoopLead < 0.08f || swoopHeave < 0.01f)
            return 0f;
        if (hoverMean <= 1.01f)
            return 0f;
        if (sensors != null && sensors.nearPerch
            && (control == null || !control.IsHuntSoil()))
            return 0f;
        if (rb != null && rb.linearVelocity.y > -0.08f)
            return 0f;
        float flare = Mathf.Max(0.4f, flareHeight);
        float top = flare + swoopLead;
        if (height <= flare || height >= top)
            return 0f;
        float u = (height - flare) / swoopLead;
        return Mathf.Sin(u * Mathf.PI);
    }

    private void UpdateLookBank(bool flapping)
    {
        if (hoverMean <= 1.01f)
            return;
        float bank = 0f;
        float amp = flapping ? cruiseBankLook : cruiseBankLook * 0.55f;
        if (amp > 0.01f)
            bank = amp * Mathf.Sin(OrbitPhase(cruiseOrbitRate2));
        if (Time.fixedTime < flipSoftUntil)
        {
            float u = 1f - (flipSoftUntil - Time.fixedTime) / 0.4f;
            u = Mathf.Clamp01(u);
            bank += 0.62f * Mathf.Sin(u * Mathf.PI) * lastFace;
        }
        if (swoopBlend > 0.05f)
            bank += 0.20f * swoopBlend * Mathf.Sign(Mathf.Sin(OrbitPhase(0.91f) + 0.7f));
        bankLook = Mathf.Clamp(bank, -1.15f, 1.15f);
    }

    private static float CycleGate(int cycle, float seed)
    {
        int n = cycle * 1103515245 + (int)(seed * 10007f);
        n = (n << 13) ^ n;
        n = n * (n * n * 15731 + 789221) + 1376312589;
        return (n & 0x7fffffff) * (1f / 2147483647f);
    }
}
